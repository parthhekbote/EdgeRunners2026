using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace InsaneGun.Combat
{
    /// <summary>
    /// AAA Cyberpunk Graphics & Visual Fidelity Master.
    /// Implements next-gen visual pipeline upgrades:
    /// 1. ACES Filmic Tonemapping & High-Dynamic-Range Bloom
    /// 2. Split-Toned Cyberpunk Grading (Cyan Shadows / Neon Magenta Highlights)
    /// 3. Physical 35mm Film Grain, Motion Blur & Chromatic Aberration
    /// 4. Cinematic Depth of Field targeting combatants with blurred skyline
    /// 5. Wet Asphalt & Neon Puddle Reflection Material Enhancer
    /// 6. Volumetric Atmospheric Fog & Overhead God-Ray Light Shafts
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("InsaneGun/AAA Cyberpunk Graphics Master")]
    public class AAACyberpunkGraphicsMaster : MonoBehaviour
    {
        public static AAACyberpunkGraphicsMaster Instance { get; private set; }

        [Header("Global Fidelity Toggle")]
        public bool enableNextGenGraphics = true;
        public bool enableWetGroundReflections = true;
        public bool enableRealtimeReflections = true;
        public bool enableCameraRainDroplets = false; // Disabled
        public bool enableVolumetricLightBeams = true;
        public bool enableAtmosphericMist = true;
        public bool enableDepthOfField = false; // Disabled by default so 200m Blackwall stays razor sharp

        [Header("Post-Processing Settings")]
        [Tooltip("Motion blur is disabled by default to eliminate character ghosting and smearing during locomotion and combat.")]
        public bool enableMotionBlur = false;
        [Range(0.5f, 3.5f)] public float bloomIntensity = 2.2f;
        [Range(0.5f, 1.2f)] public float bloomThreshold = 0.85f;
        [Range(0f, 40f)]   public float contrastBoost = 22f;
        [Range(-10f, 30f)] public float saturationBoost = 12f;
        [Range(0f, 0.45f)] public float vignetteIntensity = 0.28f;
        [Range(0f, 0.4f)]  public float filmGrainIntensity = 0.19f;
        [Range(0f, 0.35f)] public float chromaticAberrationIntensity = 0.14f;

        [Header("Wet PBR Ground Settings")]
        [Range(0.7f, 0.98f)] public float wetRoadSmoothness = 0.92f;
        [Range(0.0f, 0.5f)]  public float wetRoadMetallic   = 0.22f;
        public Color wetRoadSpecularTint = new Color(0.28f, 0.32f, 0.38f, 1.0f);

        [Header("Target Tracking (DoF)")]
        public Transform focusTarget;

        private Volume _volume;
        private VolumeProfile _profile;
        private Tonemapping _tonemapping;
        private Bloom _bloom;
        private ColorAdjustments _colorAdjustments;
        private SplitToning _splitToning;
        private Vignette _vignette;
        private FilmGrain _filmGrain;
        private ChromaticAberration _chromaticAberration;
        private MotionBlur _motionBlur;
        private DepthOfField _depthOfField;

        private readonly List<Material> _modifiedMaterials = new List<Material>();

        private void Awake()
        {
            Instance = this;
            SetupPipeline();
        }

        private void Start()
        {
            SetupPipeline();
        }

        private void OnEnable()
        {
            SetupPipeline();
        }

        [ContextMenu("Apply Next-Gen Visual Upgrades")]
        public void SetupPipeline()
        {
            if (!enableNextGenGraphics) return;

            // Ensure Main Camera has post processing enabled
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                mainCam.farClipPlane = Mathf.Max(mainCam.farClipPlane, 1000f);
                var uad = mainCam.GetComponent<UniversalAdditionalCameraData>();
                if (uad == null) uad = mainCam.gameObject.AddComponent<UniversalAdditionalCameraData>();
                uad.renderPostProcessing = true;
                uad.antialiasing = AntialiasingMode.None;
                uad.dithering = true;
            }

            SetupPostProcessingVolume();
            if (enableWetGroundReflections) ApplyWetGroundReflections();
            if (enableRealtimeReflections) SetupReflectionProbe();
            if (enableCameraRainDroplets) SetupCameraRainFX();
            else RemoveCameraRainFX();
            if (enableAtmosphericMist) SetupAtmosphericFog();
            if (enableVolumetricLightBeams) SetupVolumetricBeams();
        }

        private void SetupReflectionProbe()
        {
            var probeMaster = GetComponent<CyberpunkReflectionProbeMaster>();
            if (probeMaster == null) probeMaster = gameObject.AddComponent<CyberpunkReflectionProbeMaster>();
            probeMaster.SetupReflectionProbe();
        }

        private void SetupCameraRainFX()
        {
            var cam = Camera.main;
            if (cam != null)
            {
                var rainFX = cam.GetComponent<CameraRainLensFX>();
                if (rainFX == null) rainFX = cam.gameObject.AddComponent<CameraRainLensFX>();
                rainFX.enableRainDroplets = true;
            }
        }

        private void RemoveCameraRainFX()
        {
            var cam = Camera.main;
            if (cam != null)
            {
                var rainFX = cam.GetComponent<CameraRainLensFX>();
                if (rainFX != null)
                {
                    if (Application.isPlaying) Destroy(rainFX);
                    else DestroyImmediate(rainFX);
                }

                Transform canvasTr = cam.transform.Find("Camera_Rain_Lens_Canvas");
                if (canvasTr != null)
                {
                    if (Application.isPlaying) Destroy(canvasTr.gameObject);
                    else DestroyImmediate(canvasTr.gameObject);
                }
            }

            var orphanCanvas = GameObject.Find("Camera_Rain_Lens_Canvas");
            if (orphanCanvas != null)
            {
                if (Application.isPlaying) Destroy(orphanCanvas);
                else DestroyImmediate(orphanCanvas);
            }
        }

        // ── 1. Post-Processing Stack ─────────────────────────────────────────
        private void SetupPostProcessingVolume()
        {
            // Ensure Volume GameObject
            var volGo = GameObject.Find("AAA_Cyberpunk_PostProcess_Volume");
            if (volGo == null)
            {
                volGo = new GameObject("AAA_Cyberpunk_PostProcess_Volume");
                volGo.transform.SetParent(transform);
            }

            _volume = volGo.GetComponent<Volume>();
            if (_volume == null) _volume = volGo.AddComponent<Volume>();

            _volume.isGlobal = true;
            _volume.priority = 100f;

            if (_volume.profile == null)
            {
                _profile = ScriptableObject.CreateInstance<VolumeProfile>();
                _profile.name = "AAA_Cyberpunk_RuntimeProfile";
                _volume.profile = _profile;
            }
            else
            {
                _profile = _volume.profile;
            }

            // 1. ACES Tonemapping
            if (!_profile.TryGet(out _tonemapping)) _tonemapping = _profile.Add<Tonemapping>(true);
            _tonemapping.active = true;
            _tonemapping.mode.overrideState = true;
            _tonemapping.mode.value = TonemappingMode.ACES;

            // 2. High-Dynamic-Range Bloom
            if (!_profile.TryGet(out _bloom)) _bloom = _profile.Add<Bloom>(true);
            _bloom.active = true;
            _bloom.threshold.overrideState = true;
            _bloom.threshold.value = bloomThreshold;
            _bloom.intensity.overrideState = true;
            _bloom.intensity.value = bloomIntensity;
            _bloom.scatter.overrideState = true;
            _bloom.scatter.value = 0.70f;
            _bloom.highQualityFiltering.overrideState = true;
            _bloom.highQualityFiltering.value = true;
            _bloom.tint.overrideState = true;
            _bloom.tint.value = new Color(0.95f, 0.95f, 1.0f); // Radiant glow tint

            // 3. Color Adjustments
            if (!_profile.TryGet(out _colorAdjustments)) _colorAdjustments = _profile.Add<ColorAdjustments>(true);
            _colorAdjustments.active = true;
            _colorAdjustments.postExposure.overrideState = true;
            _colorAdjustments.postExposure.value = 0.35f;
            _colorAdjustments.contrast.overrideState = true;
            _colorAdjustments.contrast.value = contrastBoost;
            _colorAdjustments.saturation.overrideState = true;
            _colorAdjustments.saturation.value = saturationBoost;
            _colorAdjustments.colorFilter.overrideState = true;
            _colorAdjustments.colorFilter.value = new Color(0.96f, 0.98f, 1.0f);

            // 4. Split Toning (Cyberpunk Iconic Teal & Hot Magenta)
            if (!_profile.TryGet(out _splitToning)) _splitToning = _profile.Add<SplitToning>(true);
            _splitToning.active = true;
            _splitToning.shadows.overrideState = true;
            _splitToning.shadows.value = new Color(0.0f, 0.88f, 1.0f); // Electric Cyan
            _splitToning.highlights.overrideState = true;
            _splitToning.highlights.value = new Color(1.0f, 0.16f, 0.55f); // Neon Hot Magenta
            _splitToning.balance.overrideState = true;
            _splitToning.balance.value = -12f;

            // 5. Film Grain (Kills digital color banding, adds 35mm Hollywood film grain)
            if (!_profile.TryGet(out _filmGrain)) _filmGrain = _profile.Add<FilmGrain>(true);
            _filmGrain.active = true;
            _filmGrain.type.overrideState = true;
            _filmGrain.type.value = FilmGrainLookup.Medium1;
            _filmGrain.intensity.overrideState = true;
            _filmGrain.intensity.value = filmGrainIntensity;
            _filmGrain.response.overrideState = true;
            _filmGrain.response.value = 0.85f;

            // 6. Chromatic Aberration (Optical lens dispersion at edges)
            if (!_profile.TryGet(out _chromaticAberration)) _chromaticAberration = _profile.Add<ChromaticAberration>(true);
            _chromaticAberration.active = true;
            _chromaticAberration.intensity.overrideState = true;
            _chromaticAberration.intensity.value = chromaticAberrationIntensity;

            // 7. Vignette (Focuses eye on central melee duel)
            if (!_profile.TryGet(out _vignette)) _vignette = _profile.Add<Vignette>(true);
            _vignette.active = true;
            _vignette.intensity.overrideState = true;
            _vignette.intensity.value = vignetteIntensity;
            _vignette.smoothness.overrideState = true;
            _vignette.smoothness.value = 0.45f;
            _vignette.color.overrideState = true;
            _vignette.color.value = new Color(0.01f, 0.02f, 0.05f);

            // 8. Motion Blur (Disabled by default to keep character & combat animations crisp and razor-sharp with 0 ghosting)
            if (!_profile.TryGet(out _motionBlur)) _motionBlur = _profile.Add<MotionBlur>(true);
            _motionBlur.active = enableMotionBlur;
            _motionBlur.mode.overrideState = true;
            _motionBlur.mode.value = MotionBlurMode.CameraOnly;
            _motionBlur.quality.overrideState = true;
            _motionBlur.quality.value = MotionBlurQuality.Low;
            _motionBlur.intensity.overrideState = true;
            _motionBlur.intensity.value = enableMotionBlur ? 0.20f : 0f;

            // 9. Depth of Field (Disabled by default to keep Blackwall background crisp)
            if (!_profile.TryGet(out _depthOfField)) _depthOfField = _profile.Add<DepthOfField>(true);
            _depthOfField.active = enableDepthOfField;
            _depthOfField.mode.overrideState = true;
            _depthOfField.mode.value = DepthOfFieldMode.Gaussian;
            _depthOfField.gaussianStart.overrideState = true;
            _depthOfField.gaussianStart.value = 24f;
            _depthOfField.gaussianEnd.overrideState = true;
            _depthOfField.gaussianEnd.value = 75f;
            _depthOfField.gaussianMaxRadius.overrideState = true;
            _depthOfField.gaussianMaxRadius.value = 1.4f;
        }

        // ── 2. Wet Asphalt & Neon Reflection System ─────────────────────────
        private void ApplyWetGroundReflections()
        {
            var renderers = FindObjectsByType<MeshRenderer>();
            foreach (var r in renderers)
            {
                if (r == null || r.sharedMaterial == null) continue;

                string n = r.gameObject.name.ToLower();
                string matName = r.sharedMaterial.name.ToLower();

                // Check if this mesh is ground/floor/pavement/road
                bool isGround = n.Contains("floor") || n.Contains("ground") || n.Contains("road") ||
                                n.Contains("street") || n.Contains("pavement") || n.Contains("arena") ||
                                matName.Contains("floor") || matName.Contains("road") || matName.Contains("asphalt");

                if (isGround)
                {
                    // Ensure instance material so we don't permanently corrupt source asset
                    var mats = Application.isPlaying ? r.materials : r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        var m = mats[i];
                        if (m != null && m.HasProperty("_Smoothness"))
                        {
                            m.SetFloat("_Smoothness", wetRoadSmoothness);
                            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", wetRoadMetallic);
                            if (m.HasProperty("_SpecularHighlights")) m.SetFloat("_SpecularHighlights", 1f);
                            if (m.HasProperty("_EnvironmentReflections")) m.SetFloat("_EnvironmentReflections", 1f);
                        }
                    }
                }
            }
        }

        // ── 3. Atmospheric Cyberpunk Fog ─────────────────────────────────────
        private void SetupAtmosphericFog()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0095f;
            RenderSettings.fogColor = new Color(0.02f, 0.05f, 0.12f, 1.0f); // Deep midnight blue mist

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.07f, 0.11f, 0.20f);
        }

        // ── 4. Volumetric Light Beams (God-Rays) ─────────────────────────────
        private void SetupVolumetricBeams()
        {
            var spotlights = FindObjectsByType<Light>();
            foreach (var lt in spotlights)
            {
                if (lt == null || lt.type != LightType.Spot) continue;

                // Check if beam cone already exists
                Transform existingBeam = lt.transform.Find("Volumetric_Beam_Cone");
                if (existingBeam != null) continue;

                var beamGo = CreateVolumetricConeMesh(lt);
                if (beamGo != null)
                {
                    beamGo.name = "Volumetric_Beam_Cone";
                    beamGo.transform.SetParent(lt.transform, false);
                }
            }
        }

        private GameObject CreateVolumetricConeMesh(Light spot)
        {
            var coneGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            coneGo.name = "Volumetric_Beam_Cone";

            // Remove collider
            var col = coneGo.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);

            float range = spot.range * 0.75f;
            float radius = Mathf.Tan(spot.spotAngle * 0.5f * Mathf.Deg2Rad) * range * 1.1f;

            coneGo.transform.localPosition = new Vector3(0f, 0f, range * 0.5f);
            coneGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            coneGo.transform.localScale = new Vector3(radius, range * 0.5f, radius);

            var mr = coneGo.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                // Create soft additive translucent beam material
                var beamShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (beamShader == null) beamShader = Shader.Find("Sprites/Default");

                var beamMat = new Material(beamShader)
                {
                    name = "Volumetric_LightBeam_Mat"
                };

                Color beamCol = spot.color;
                beamCol.a = 0.045f; // Ultra-soft subtle haze
                if (beamMat.HasProperty("_BaseColor")) beamMat.SetColor("_BaseColor", beamCol);
                if (beamMat.HasProperty("_Color")) beamMat.SetColor("_Color", beamCol);

                // Additive blend mode
                beamMat.SetFloat("_Surface", 1); // Transparent
                beamMat.SetFloat("_Blend", 0);   // Alpha
                beamMat.renderQueue = 3100;
                mr.sharedMaterial = beamMat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }

            return coneGo;
        }

        private void Update()
        {
            // Dynamic Depth of Field focus tracking (only active if enableDepthOfField is true)
            if (Application.isPlaying && enableDepthOfField && _depthOfField != null && _depthOfField.active)
            {
                if (focusTarget == null)
                {
                    var player = GameObject.FindWithTag("Player");
                    if (player != null) focusTarget = player.transform;
                }

                if (focusTarget != null && Camera.main != null)
                {
                    float dist = Vector3.Distance(Camera.main.transform.position, focusTarget.position);
                    _depthOfField.gaussianStart.value = Mathf.Max(12f, dist + 16f);
                    _depthOfField.gaussianEnd.value = Mathf.Max(45f, dist + 60f);
                }
            }
        }
    }
}
