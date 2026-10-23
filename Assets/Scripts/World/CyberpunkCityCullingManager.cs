using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cyberpunk.World
{
    /// <summary>
    /// Master Runtime Culling and Performance Optimizer for Demo_City.
    /// Operates purely at runtime to maximize FPS on CPU and GPU:
    /// 
    /// 1. Dynamic Light & Shadow Culling:
    ///    - Solves the 100+ shadow maps atlas bottleneck!
    ///    - Disables real-time shadows on distant lights (> 22m).
    ///    - Disables minor decorative lights (> 45m).
    ///    - Time-sliced round-robin evaluation (zero GC, zero frame spikes).
    /// 
    /// 2. Camera Layer Distance Culling:
    ///    - Clamps camera farClipPlane from 1000m to 350m in the city.
    ///    - Applies per-layer culling distances for small props (35m), medium (75m).
    /// 
    /// 3. Small Prop Distance Culling:
    ///    - Culls distant debris, trash, wires, small greebles beyond 40m.
    /// </summary>
    [DisallowMultipleComponent]
    public class CyberpunkCityCullingManager : MonoBehaviour
    {
        public static CyberpunkCityCullingManager Instance { get; private set; }

        public const string CitySceneName = "Demo_City";

        [Header("Camera Distance Culling")]
        public float cityFarClipPlane = 350.0f;
        public float smallPropsCullDist = 38.0f;
        public float mediumPropsCullDist = 80.0f;
        public float detailCullDist = 130.0f;

        [Header("Light & Shadow Culling")]
        [Tooltip("Distance at which dynamic point/spot lights can cast real-time shadows")]
        public float lightShadowMaxDistance = 22.0f;
        [Tooltip("Distance at which decorative accent lights are disabled")]
        public float lightCutoffDistance = 50.0f;
        [Tooltip("Number of lights evaluated per frame (time-sliced to prevent lag)")]
        public int lightsEvaluatedPerFrame = 8;

        [Header("Clutter / Small Props Culling")]
        public float smallPropCullDistance = 42.0f;
        public int propsEvaluatedPerFrame = 16;

        // Tracked Lights
        private struct ManagedLight
        {
            public Light lightComponent;
            public Vector3 position;
            public LightShadows originalShadows;
            public bool isHeroOrLargeLight;
            public float originalIntensity;
        }

        // Tracked Props
        private struct ManagedProp
        {
            public Renderer renderer;
            public Vector3 position;
        }

        private readonly List<ManagedLight> _managedLights = new List<ManagedLight>();
        private readonly List<ManagedProp> _managedProps = new List<ManagedProp>();

        private Transform _cameraTransform;
        private Camera _mainCamera;
        private int _currentLightIndex = 0;
        private int _currentPropIndex = 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnSceneLoadedAutoInit()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.name.IndexOf("City", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                EnsureCullingManagerInScene();
            }
        }

        public static CyberpunkCityCullingManager EnsureCullingManagerInScene()
        {
            var existing = FindAnyObjectByType<CyberpunkCityCullingManager>();
            if (existing != null) return existing;

            var go = new GameObject("[CyberpunkCity_CullingManager]");
            var manager = go.AddComponent<CyberpunkCityCullingManager>();
            Debug.Log("[CityCulling] CyberpunkCityCullingManager initialized in " + SceneManager.GetActiveScene().name);
            return manager;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            AcquireCamera();
            ConfigureCameraCulling();
            ScanAndRegisterLights();
            ScanAndRegisterSmallProps();
            EnsurePlayerWeaponVisibility();
        }

        public void EnsurePlayerWeaponVisibility()
        {
            var renderers = FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include);
            foreach (var r in renderers)
            {
                if (r == null) continue;
                string n = r.gameObject.name.ToLower();
                if (n.Contains("blade") || n.Contains("sword") || n.Contains("katana"))
                {
                    r.enabled = true;
                    r.allowOcclusionWhenDynamic = false;
                }
            }
        }

        private void AcquireCamera()
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null)
            {
                var cams = FindObjectsByType<Camera>(FindObjectsInactive.Exclude);
                foreach (var c in cams)
                {
                    if (c.CompareTag("MainCamera") || c.name.Contains("Camera"))
                    {
                        _mainCamera = c;
                        break;
                    }
                }
            }

            if (_mainCamera != null)
            {
                _cameraTransform = _mainCamera.transform;
            }
        }

        private void ConfigureCameraCulling()
        {
            if (_mainCamera == null) return;

            // Clamp absurdly far clip plane (1000m+ in modular city) to city skyline bounds
            if (_mainCamera.farClipPlane > cityFarClipPlane)
            {
                _mainCamera.farClipPlane = cityFarClipPlane;
            }

            // Layer cull distances array (32 layers in Unity)
            float[] cullDistances = new float[32];
            for (int i = 0; i < 32; i++)
            {
                cullDistances[i] = cityFarClipPlane; // default
            }

            // Check if specific layers exist in project
            int smallPropLayer = LayerMask.NameToLayer("SmallProps");
            if (smallPropLayer >= 0) cullDistances[smallPropLayer] = smallPropsCullDist;

            int medPropLayer = LayerMask.NameToLayer("MediumProps");
            if (medPropLayer >= 0) cullDistances[medPropLayer] = mediumPropsCullDist;

            int detailLayer = LayerMask.NameToLayer("CityDetail");
            if (detailLayer >= 0) cullDistances[detailLayer] = detailCullDist;

            _mainCamera.layerCullDistances = cullDistances;
            Debug.Log($"[CityCulling] Configured Camera FarClip: {cityFarClipPlane}m");
        }

        private void ScanAndRegisterLights()
        {
            _managedLights.Clear();
            var allLights = FindObjectsByType<Light>(FindObjectsInactive.Exclude);

            int shadowCastersCount = 0;
            foreach (var l in allLights)
            {
                if (l.type == LightType.Directional) continue; // Skip sun / moon

                bool isHero = l.range >= 15f || l.intensity >= 4f || l.gameObject.name.Contains("Hero") || l.gameObject.name.Contains("Sun");

                // Decorative lights don't need real-time shadows at all
                if (!isHero && l.shadows != LightShadows.None)
                {
                    l.shadows = LightShadows.None;
                }

                if (l.shadows != LightShadows.None)
                {
                    shadowCastersCount++;
                }

                _managedLights.Add(new ManagedLight
                {
                    lightComponent = l,
                    position = l.transform.position,
                    originalShadows = isHero ? l.shadows : LightShadows.None,
                    isHeroOrLargeLight = isHero,
                    originalIntensity = l.intensity
                });
            }

            Debug.Log($"[CityCulling] Registered {_managedLights.Count} dynamic city lights (active shadow casters reduced to {shadowCastersCount}).");
        }

        private void ScanAndRegisterSmallProps()
        {
            _managedProps.Clear();
            var renderers = FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude);

            foreach (var r in renderers)
            {
                // CRITICAL: NEVER cull the player, weapons, blades, characters, or NPCs!
                if (r.transform.root.CompareTag("Player") ||
                    r.GetComponentInParent<CharacterController>() != null ||
                    r.GetComponentInParent<Animator>() != null ||
                    r.gameObject.name.ToLower().Contains("blade") ||
                    r.gameObject.name.ToLower().Contains("sword") ||
                    r.gameObject.name.ToLower().Contains("katana") ||
                    r.gameObject.name.ToLower().Contains("alita") ||
                    r.gameObject.name.ToLower().Contains("hero"))
                {
                    continue;
                }

                string n = r.gameObject.name.ToLower();
                // Filter small clutter: bottles, cans, debris, wires, greebles, garbage
                if (n.Contains("junk") || n.Contains("can_0") || n.Contains("bottle") ||
                    n.Contains("debris") || n.Contains("paper") || n.Contains("greebles") ||
                    n.Contains("wires_") || n.Contains("trash") || n.Contains("cardboard"))
                {
                    _managedProps.Add(new ManagedProp
                    {
                        renderer = r,
                        position = r.transform.position
                    });
                }
            }

            Debug.Log($"[CityCulling] Registered {_managedProps.Count} small clutter props for dynamic distance culling.");
        }

        private void Update()
        {
            if (_cameraTransform == null)
            {
                AcquireCamera();
                if (_cameraTransform == null) return;
            }

            Vector3 camPos = _cameraTransform.position;

            // 1. Time-Sliced Dynamic Light & Shadow Culling
            UpdateLightsTimeSliced(camPos);

            // 2. Time-Sliced Small Prop Culling
            UpdatePropsTimeSliced(camPos);
        }

        private void UpdateLightsTimeSliced(Vector3 camPos)
        {
            int count = _managedLights.Count;
            if (count == 0) return;

            int evaluated = 0;
            while (evaluated < lightsEvaluatedPerFrame)
            {
                _currentLightIndex = (_currentLightIndex + 1) % count;
                var item = _managedLights[_currentLightIndex];
                var l = item.lightComponent;

                if (l != null)
                {
                    float distSq = (item.position - camPos).sqrMagnitude;
                    float shadowMaxDistSq = lightShadowMaxDistance * lightShadowMaxDistance;
                    float cutoffDistSq = lightCutoffDistance * lightCutoffDistance;

                    if (distSq > cutoffDistSq)
                    {
                        // Beyond cutoff: disable decorative lights completely
                        if (!item.isHeroOrLargeLight && l.enabled)
                        {
                            l.enabled = false;
                        }
                    }
                    else
                    {
                        if (!l.enabled) l.enabled = true;

                        // Beyond shadow distance: disable shadow map pass
                        if (distSq > shadowMaxDistSq)
                        {
                            if (l.shadows != LightShadows.None)
                            {
                                l.shadows = LightShadows.None;
                            }
                        }
                        else
                        {
                            // Within shadow distance: restore original shadow
                            if (l.shadows != item.originalShadows)
                            {
                                l.shadows = item.originalShadows;
                            }
                        }
                    }
                }

                evaluated++;
            }
        }

        private void UpdatePropsTimeSliced(Vector3 camPos)
        {
            int count = _managedProps.Count;
            if (count == 0) return;

            float cullDistSq = smallPropCullDistance * smallPropCullDistance;
            int evaluated = 0;

            while (evaluated < propsEvaluatedPerFrame)
            {
                _currentPropIndex = (_currentPropIndex + 1) % count;
                var item = _managedProps[_currentPropIndex];
                var r = item.renderer;

                if (r != null)
                {
                    float distSq = (item.position - camPos).sqrMagnitude;
                    bool shouldBeVisible = distSq <= cullDistSq;
                    if (r.enabled != shouldBeVisible)
                    {
                        r.enabled = shouldBeVisible;
                    }
                }

                evaluated++;
            }
        }
    }
}
