using System.Collections;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Alita
{
    /// <summary>
    /// Dynamic Weather & Atmospheric Storm System for the Scrapyard.
    /// Manages real-time rain tracking with the camera, dynamic multi-pulse lightning flashes,
    /// real-time shadow casting, continuous calming rain audio, and thunder audio.
    /// </summary>
    public class ScrapyardStormSystem : MonoBehaviour
    {
        [Header("Rain System")]
        public GameObject rainPrefab;
        public GameObject rainSplashPrefab;
        public ParticleSystem rainParticleSystem;
        public ParticleSystem rainSplashSystem;
        public Camera targetCamera;
        public float rainHeightAboveCam = 8.5f;

        [Header("Lightning Light")]
        public Light lightningLight;
        public Color lightningColor = new Color(0.82f, 0.90f, 1.0f);
        public float minStrikeInterval = 9.0f;
        public float maxStrikeInterval = 18.0f;
        public float maxLightningIntensity = 5.8f;

        [Header("Audio")]
        public AudioSource rainAudioSource;
        public AudioSource thunderAudioSource;
        public AudioClip customRainLoop;
        public AudioClip customThunderClip;
        [Range(0f, 1f)] public float rainVolume = 0.85f;
        [Range(0f, 1f)] public float thunderVolume = 0.95f;

        [Header("Storm State")]
        public bool isStormActive = true;

        private float _nextStrikeTimer = 0f;
        private bool _isFlashing = false;

        private void Awake()
        {
            SetupLightningLight();
            SetupAudioSources();
            EnsureRainParticlesInstantiated();
        }

        private void Start()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            // Start ambient rain sound loop immediately
            if (rainAudioSource != null && customRainLoop != null)
            {
                rainAudioSource.clip = customRainLoop;
                rainAudioSource.loop = true;
                rainAudioSource.volume = rainVolume;
                if (!rainAudioSource.isPlaying) rainAudioSource.Play();
            }

            _nextStrikeTimer = Random.Range(3.0f, 6.0f); // First lightning strike fairly soon
        }

        public void EnsureRainParticlesInstantiated()
        {
            // If rainParticleSystem is not set, instantiate or find it
            if (rainParticleSystem == null)
            {
                var existing = GameObject.Find("FX_Cutscene_Rain_Instance");
                if (existing != null)
                {
                    rainParticleSystem = existing.GetComponentInChildren<ParticleSystem>();
                }
                else
                {
#if UNITY_EDITOR
                    if (rainPrefab == null)
                    {
                        rainPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonParticles/Prefabs/FX_Rain_01.prefab");
                    }
#endif
                    if (rainPrefab != null)
                    {
                        var rainInstance = Instantiate(rainPrefab, transform);
                        rainInstance.name = "FX_Cutscene_Rain_Instance";
                        rainParticleSystem = rainInstance.GetComponentInChildren<ParticleSystem>();
                    }
                    else
                    {
                        // Fallback procedural rain particle system
                        var rainGo = new GameObject("FX_Cutscene_Rain_Instance");
                        rainGo.transform.SetParent(transform);
                        rainParticleSystem = rainGo.AddComponent<ParticleSystem>();
                        var main = rainParticleSystem.main;
                        main.startLifetime = 1.6f;
                        main.startSpeed = 22f;
                        main.startSize = 0.08f;
                        main.maxParticles = 3000;
                        var emission = rainParticleSystem.emission;
                        emission.rateOverTime = 800f;
                        var shape = rainParticleSystem.shape;
                        shape.shapeType = ParticleSystemShapeType.Box;
                        shape.scale = new Vector3(36f, 36f, 1f);
                        rainGo.transform.rotation = Quaternion.Euler(85f, 0f, 0f);
                    }
                }
            }

            if (rainParticleSystem != null)
            {
                // Ensure shape covers a wide area around the camera
                var shape = rainParticleSystem.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(38f, 38f, 1f);

                var emission = rainParticleSystem.emission;
                if (emission.rateOverTime.constant < 500f) emission.rateOverTime = 850f;

                if (!rainParticleSystem.isPlaying) rainParticleSystem.Play();
            }

            // Splashes
            if (rainSplashSystem == null)
            {
#if UNITY_EDITOR
                if (rainSplashPrefab == null)
                {
                    rainSplashPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonParticles/Prefabs/FX_Rain_Collision_01.prefab");
                }
#endif
                if (rainSplashPrefab != null)
                {
                    var splashInstance = Instantiate(rainSplashPrefab, transform);
                    splashInstance.name = "FX_Cutscene_Rain_Splashes";
                    rainSplashSystem = splashInstance.GetComponentInChildren<ParticleSystem>();
                }
            }
        }

        private void SetupLightningLight()
        {
            if (lightningLight == null)
            {
                var lightGo = GameObject.Find("FX_Lightning_Sky_Flash");
                if (lightGo == null)
                {
                    lightGo = new GameObject("FX_Lightning_Sky_Flash");
                    lightGo.transform.SetParent(transform);
                    lightningLight = lightGo.AddComponent<Light>();
                    lightningLight.type = LightType.Directional;
                    lightningLight.color = lightningColor;
                    lightningLight.intensity = 0f;
                    lightningLight.shadows = LightShadows.Soft;
                    lightningLight.shadowStrength = 0.85f;
                    lightningLight.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
                }
                else
                {
                    lightningLight = lightGo.GetComponent<Light>();
                }
            }
            if (lightningLight != null) lightningLight.intensity = 0f;
        }

        private void SetupAudioSources()
        {
            // Rain loop audio source
            if (rainAudioSource == null)
            {
                var rainAudioGo = new GameObject("Audio_Rain_Loop");
                rainAudioGo.transform.SetParent(transform);
                rainAudioSource = rainAudioGo.AddComponent<AudioSource>();
                rainAudioSource.spatialBlend = 0.0f; // 2D ambient rain
                rainAudioSource.playOnAwake = false;
                rainAudioSource.loop = true;
            }

            // Thunder strike audio source
            if (thunderAudioSource == null)
            {
                var thunderAudioGo = new GameObject("Audio_Thunder_Strike");
                thunderAudioGo.transform.SetParent(transform);
                thunderAudioSource = thunderAudioGo.AddComponent<AudioSource>();
                thunderAudioSource.spatialBlend = 0.0f;
                thunderAudioSource.playOnAwake = false;
                thunderAudioSource.loop = false;
            }

#if UNITY_EDITOR
            if (customRainLoop == null)
            {
                customRainLoop = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Weather/liecio-calming-rain-257596.mp3");
            }
            if (customThunderClip == null)
            {
                customThunderClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Weather/dragon-studio-dry-thunder-364468.mp3");
            }
#endif
        }

        private void Update()
        {
            if (!isStormActive) return;

            UpdateRainTracking();

            // Handle lightning strike timer
            _nextStrikeTimer -= Time.deltaTime;
            if (_nextStrikeTimer <= 0f && !_isFlashing)
            {
                TriggerLightningStrike();
                _nextStrikeTimer = Random.Range(minStrikeInterval, maxStrikeInterval);
            }
        }

        private void UpdateRainTracking()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
                if (targetCamera == null) return;
            }

            Vector3 camPos = targetCamera.transform.position;

            if (rainParticleSystem != null)
            {
                // Position rain cloud directly above camera
                rainParticleSystem.transform.position = new Vector3(camPos.x, camPos.y + rainHeightAboveCam, camPos.z);
                if (!rainParticleSystem.isPlaying) rainParticleSystem.Play();
            }

            if (rainSplashSystem != null)
            {
                rainSplashSystem.transform.position = new Vector3(camPos.x, camPos.y - 1.5f, camPos.z);
                if (!rainSplashSystem.isPlaying) rainSplashSystem.Play();
            }
        }

        public void TriggerLightningStrike()
        {
            if (_isFlashing) return;
            StartCoroutine(LightningSequenceRoutine());
        }

        private IEnumerator LightningSequenceRoutine()
        {
            _isFlashing = true;

            // 1. Initial flash
            if (lightningLight != null)
            {
                lightningLight.intensity = maxLightningIntensity * 0.6f;
                lightningLight.enabled = true;
            }
            yield return new WaitForSeconds(0.045f);

            // 2. Dark dip
            if (lightningLight != null) lightningLight.intensity = 0.2f;
            yield return new WaitForSeconds(0.035f);

            // 3. Main colossal strike
            if (lightningLight != null) lightningLight.intensity = maxLightningIntensity;
            yield return new WaitForSeconds(0.09f);

            // 4. Secondary flicker
            if (lightningLight != null) lightningLight.intensity = maxLightningIntensity * 0.45f;
            yield return new WaitForSeconds(0.04f);

            // 5. Smooth decay
            float decayElapsed = 0f;
            float decayDuration = 0.25f;
            float startVal = lightningLight != null ? lightningLight.intensity : 0f;
            while (decayElapsed < decayDuration)
            {
                decayElapsed += Time.deltaTime;
                if (lightningLight != null)
                {
                    lightningLight.intensity = Mathf.Lerp(startVal, 0f, decayElapsed / decayDuration);
                }
                yield return null;
            }

            if (lightningLight != null) lightningLight.intensity = 0f;
            _isFlashing = false;

            // Delayed thunder rumble
            float delay = Random.Range(0.25f, 0.65f);
            yield return new WaitForSeconds(delay);

            PlayThunderSound();
        }

        private void PlayThunderSound()
        {
            if (thunderAudioSource != null && customThunderClip != null)
            {
                thunderAudioSource.pitch = Random.Range(0.92f, 1.05f);
                thunderAudioSource.PlayOneShot(customThunderClip, thunderVolume);
            }
        }
    }
}
