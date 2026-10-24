using System;
using System.Collections;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Cyberpunk.Mars
{
    /// <summary>
    /// Ancient URM Antimatter Berserker Core and Pedestal on Mars.
    /// Manages:
    /// - Crystalline containment pedestal with levitating, pulsating Antimatter Singularity Core.
    /// - High-contrast cyan/crimson HDR emissions, particle embers, and volumetric light flare.
    /// - Interactive/Cinematic Absorption Sequence: Core detaches, spirals smoothly toward Alita's cybernetic chest,
    ///   and fuses with her Berserker heart in an explosive blinding flash of plasma energy.
    /// </summary>
    public class MarsAntimatterCorePedestal : MonoBehaviour
    {
        public static MarsAntimatterCorePedestal Instance { get; private set; }

        [Header("Prefabs & Models")]
        public GameObject pedestalBasePrefab;
        public GameObject coreFxFxPrefab;

        [Header("Components")]
        public Transform levitatingCoreTransform;
        public Light corePointLight;
        public ParticleSystem coreEmbersFX;

        [Header("Audio Tracks")]
        public AudioClip coreHumLoopAudio;
        public AudioClip coreAbsorbSurgeAudio;

        [Header("State")]
        public bool isCoreAbsorbed = false;
        public bool isAbsorbing = false;

        private AudioSource _audioSource;
        private AudioSource _humLoopSource;
        private Vector3 _coreOriginalLocalPos;
        private Material _coreMaterial;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0.6f;

            _humLoopSource = gameObject.AddComponent<AudioSource>();
            _humLoopSource.loop = true;
            _humLoopSource.playOnAwake = false;
            _humLoopSource.volume = 0.35f;
            _humLoopSource.spatialBlend = 0.7f;

            LoadDefaultAssets();
            BuildCoreVisualsIfMissing();
        }

        private void Start()
        {
            if (coreHumLoopAudio != null && !isCoreAbsorbed)
            {
                _humLoopSource.clip = coreHumLoopAudio;
                _humLoopSource.Play();
            }
        }

        public void LoadDefaultAssets()
        {
#if UNITY_EDITOR
            if (pedestalBasePrefab == null)
                pedestalBasePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonSciFiWorlds/Prefabs/Environments/SM_Env_Core_01.prefab");
            if (coreFxFxPrefab == null)
                coreFxFxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonSciFiWorlds/Prefabs/FX/FX_Core_01.prefab");

            if (coreAbsorbSurgeAudio == null)
                coreAbsorbSurgeAudio = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Weather/dragon-studio-dry-thunder-364468.mp3");
            if (coreHumLoopAudio == null)
                coreHumLoopAudio = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Cutscenes/Astropulse_Celestial_Drone.mp3");
#endif
        }

        public void BuildCoreVisualsIfMissing()
        {
            // 1. Pedestal Base
            if (transform.Find("Pedestal_Base") == null)
            {
                GameObject baseGO = null;
                if (pedestalBasePrefab != null)
                {
                    baseGO = Instantiate(pedestalBasePrefab, transform);
                    baseGO.name = "Pedestal_Base";
                    baseGO.transform.localPosition = Vector3.zero;
                    baseGO.transform.localRotation = Quaternion.identity;
                    baseGO.transform.localScale = Vector3.one * 0.75f;
                }
                else
                {
                    // Procedural sleek metallic pedestal column
                    baseGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    baseGO.name = "Pedestal_Base";
                    baseGO.transform.SetParent(transform, false);
                    baseGO.transform.localPosition = new Vector3(0f, 0.45f, 0f);
                    baseGO.transform.localScale = new Vector3(1.1f, 0.45f, 1.1f);

                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    mat.color = new Color(0.12f, 0.12f, 0.15f);
                    mat.SetFloat("_Metallic", 0.95f);
                    mat.SetFloat("_Smoothness", 0.85f);
                    baseGO.GetComponent<Renderer>().sharedMaterial = mat;
                }
            }

            // 2. Levitating Core Node
            if (levitatingCoreTransform == null)
            {
                var existingNode = transform.Find("Levitating_URM_Core");
                if (existingNode != null)
                {
                    levitatingCoreTransform = existingNode;
                }
                else
                {
                    GameObject coreGO = new GameObject("Levitating_URM_Core");
                    coreGO.transform.SetParent(transform, false);
                    coreGO.transform.localPosition = new Vector3(0f, 1.45f, 0f);
                    levitatingCoreTransform = coreGO.transform;

                    // Inner Singular Sphere
                    GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    sphere.name = "Antimatter_Sphere";
                    sphere.transform.SetParent(coreGO.transform, false);
                    sphere.transform.localScale = Vector3.one * 0.55f;
                    Destroy(sphere.GetComponent<Collider>()); // Visual only

                    _coreMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                    _coreMaterial.color = new Color(1.0f, 0.20f, 0.12f);
                    if (_coreMaterial.HasProperty("_EmissionColor"))
                    {
                        _coreMaterial.EnableKeyword("_EMISSION");
                        _coreMaterial.SetColor("_EmissionColor", new Color(5.5f, 0.70f, 0.25f));
                    }
                    sphere.GetComponent<Renderer>().sharedMaterial = _coreMaterial;

                    // Outer Containment Ring (tilted)
                    GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    ring.name = "Containment_Ring";
                    ring.transform.SetParent(coreGO.transform, false);
                    ring.transform.localScale = new Vector3(0.85f, 0.02f, 0.85f);
                    ring.transform.localRotation = Quaternion.Euler(35f, 25f, 15f);
                    Destroy(ring.GetComponent<Collider>());

                    var ringMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    ringMat.color = new Color(0.85f, 0.15f, 0.25f);
                    if (ringMat.HasProperty("_EmissionColor"))
                    {
                        ringMat.EnableKeyword("_EMISSION");
                        ringMat.SetColor("_EmissionColor", new Color(3.5f, 0.4f, 0.6f));
                    }
                    ring.GetComponent<Renderer>().sharedMaterial = ringMat;
                }
            }

            if (levitatingCoreTransform != null)
            {
                _coreOriginalLocalPos = levitatingCoreTransform.localPosition;
            }

            // 3. Radiant Point Light
            if (corePointLight == null)
            {
                var lightGO = transform.Find("Core_Point_Light");
                if (lightGO != null)
                {
                    corePointLight = lightGO.GetComponent<Light>();
                }
                else
                {
                    GameObject lGO = new GameObject("Core_Point_Light");
                    lGO.transform.SetParent(levitatingCoreTransform ?? transform, false);
                    lGO.transform.localPosition = Vector3.zero;
                    corePointLight = lGO.AddComponent<Light>();
                    corePointLight.type = LightType.Point;
                    corePointLight.color = new Color(1.0f, 0.24f, 0.12f);
                    corePointLight.range = 14f;
                    corePointLight.intensity = 4.5f;
                }
            }

            // 4. Rising Embers Particle System
            if (coreEmbersFX == null)
            {
                var psGO = transform.Find("Core_Embers_FX");
                if (psGO != null)
                {
                    coreEmbersFX = psGO.GetComponent<ParticleSystem>();
                }
                else
                {
                    GameObject fxGO = new GameObject("Core_Embers_FX");
                    fxGO.transform.SetParent(levitatingCoreTransform ?? transform, false);
                    fxGO.transform.localPosition = Vector3.zero;
                    coreEmbersFX = fxGO.AddComponent<ParticleSystem>();

                    var main = coreEmbersFX.main;
                    main.startLifetime = 1.2f;
                    main.startSpeed = 0.6f;
                    main.startSize = 0.14f;
                    main.startColor = new Color(1.0f, 0.26f, 0.12f, 0.95f);
                    main.simulationSpace = ParticleSystemSimulationSpace.World;

                    var shape = coreEmbersFX.shape;
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    shape.radius = 0.35f;

                    var emission = coreEmbersFX.emission;
                    emission.rateOverTime = 16f;

                    var rend = fxGO.GetComponent<ParticleSystemRenderer>();
                    var pMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Universal Render Pipeline/Unlit"));
                    pMat.mainTexture = AlitaSuperchargeAura.GetOrCreateSoftCircleTexture();
                    pMat.color = new Color(1.0f, 0.22f, 0.12f);
                    if (pMat.HasProperty("_EmissionColor"))
                    {
                        pMat.EnableKeyword("_EMISSION");
                        pMat.SetColor("_EmissionColor", new Color(5.0f, 0.6f, 0.2f));
                    }
                    rend.sharedMaterial = pMat;
                }
            }
        }

        private void Update()
        {
            if (isCoreAbsorbed || isAbsorbing) return;

            // Hovering Bobbing and Rotation
            if (levitatingCoreTransform != null)
            {
                float bob = Mathf.Sin(Time.time * 2.5f) * 0.08f;
                levitatingCoreTransform.localPosition = _coreOriginalLocalPos + new Vector3(0f, bob, 0f);
                levitatingCoreTransform.Rotate(Vector3.up * 48f * Time.deltaTime, Space.World);
            }

            // Light Pulsing
            if (corePointLight != null)
            {
                float pulse = Mathf.PingPong(Time.time * 3.2f, 1.8f);
                corePointLight.intensity = 3.5f + pulse;
            }
        }

        /// <summary>
        /// Begins the absorption cinematic sequence: Core glides into Alita's chest,
        /// unleashing an electrical shockwave and supercharging her cybernetic systems!
        /// </summary>
        public void StartAbsorptionSequence(Transform alitaTransform, Action onAbsorbed = null)
        {
            if (isCoreAbsorbed || isAbsorbing) return;
            isAbsorbing = true;
            StartCoroutine(AbsorbCoreRoutine(alitaTransform, onAbsorbed));
        }

        private IEnumerator AbsorbCoreRoutine(Transform alitaTransform, Action onAbsorbed)
        {
            if (levitatingCoreTransform == null || alitaTransform == null)
            {
                FastForwardAbsorption(alitaTransform);
                onAbsorbed?.Invoke();
                yield break;
            }

            // Detach core from pedestal hierarchy to move in world space
            levitatingCoreTransform.SetParent(null, true);

            Vector3 startPos = levitatingCoreTransform.position;
            Vector3 targetChest = alitaTransform.position + Vector3.up * 1.25f;

            float duration = 1.85f;
            float timer = 0f;

            // Play surge buildup
            if (_humLoopSource != null)
            {
                _humLoopSource.pitch = 1.4f;
                _humLoopSource.volume = 0.7f;
            }

            while (timer < duration)
            {
                timer += Time.deltaTime;
                float norm = Mathf.Clamp01(timer / duration);
                float smooth = Mathf.SmoothStep(0f, 1f, norm);

                // Arc trajectory towards chest
                Vector3 currentTarget = alitaTransform.position + Vector3.up * 1.25f;
                Vector3 lerpPos = Vector3.Lerp(startPos, currentTarget, smooth);

                // Add spiral curve
                float spiralAngle = norm * Mathf.PI * 4f;
                float spiralRadius = (1f - norm) * 0.65f;
                lerpPos.x += Mathf.Cos(spiralAngle) * spiralRadius;
                lerpPos.y += Mathf.Sin(norm * Mathf.PI) * 0.45f;

                levitatingCoreTransform.position = lerpPos;
                levitatingCoreTransform.Rotate(Vector3.up * 360f * Time.deltaTime);

                if (corePointLight != null)
                {
                    corePointLight.intensity = Mathf.Lerp(4.5f, 12f, norm);
                    corePointLight.range = Mathf.Lerp(14f, 22f, norm);
                }

                yield return null;
            }

            // ════════════════════════════════════════════════════════════════════════
            // IMPACT FLASH: CORE FUSES INTO ALITA'S CHEST!
            // ════════════════════════════════════════════════════════════════════════
            if (_audioSource != null && coreAbsorbSurgeAudio != null)
            {
                _audioSource.PlayOneShot(coreAbsorbSurgeAudio, 1.0f);
            }
            if (_humLoopSource != null) _humLoopSource.Stop();

            // Spawn chest surge light flash
            GameObject flashGO = new GameObject("Core_Absorption_Flash");
            flashGO.transform.position = alitaTransform.position + Vector3.up * 1.25f;
            var flashLight = flashGO.AddComponent<Light>();
            flashLight.type = LightType.Point;
            flashLight.color = new Color(1.0f, 0.22f, 0.12f);
            flashLight.range = 30f;
            flashLight.intensity = 15f;
            Destroy(flashGO, 0.35f);

            // Hide/Destroy levitating core object
            Destroy(levitatingCoreTransform.gameObject);
            levitatingCoreTransform = null;

            isCoreAbsorbed = true;
            isAbsorbing = false;

            // Activate Alita's Supercharge Aura component
            var aura = alitaTransform.GetComponent<AlitaSuperchargeAura>();
            if (aura == null) aura = alitaTransform.gameObject.AddComponent<AlitaSuperchargeAura>();
            aura.ActivateSupercharge();

            onAbsorbed?.Invoke();
        }

        public void FastForwardAbsorption(Transform alitaTransform)
        {
            isCoreAbsorbed = true;
            isAbsorbing = false;

            if (_humLoopSource != null) _humLoopSource.Stop();

            if (levitatingCoreTransform != null)
            {
                Destroy(levitatingCoreTransform.gameObject);
                levitatingCoreTransform = null;
            }

            if (alitaTransform != null)
            {
                var aura = alitaTransform.GetComponent<AlitaSuperchargeAura>();
                if (aura == null) aura = alitaTransform.gameObject.AddComponent<AlitaSuperchargeAura>();
                aura.ActivateSupercharge();
            }
        }
    }
}
