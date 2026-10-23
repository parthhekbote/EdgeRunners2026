using System.Collections;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Cyberpunk.Mars
{
    /// <summary>
    /// AAA-Grade Martian Dust Storm System for Mars (Scene_Mars_FinalBattle).
    /// Features:
    /// 1. Dynamic Atmospheric Entry: Smoothly ramps fog density, ambient bounce, and sun dimming
    ///    as Alita breaches the upper atmosphere into the raging Cydonia dust storm.
    /// 2. Camera-Tethered Gale Dust Emitters: Multi-layered particles (fast sand grit, billowing storm clouds,
    ///    flying regolith grains) dynamically sweeping across the active camera viewport.
    /// 3. Starship Re-entry Plasma & Dust Wake: Atmospheric friction shockwave flaring across the starship
    ///    leading edges upon orbital penetration.
    /// 4. Procedural Martian Gale Audio Synthesizer: Synthesizes low-frequency wind rumble, whistling gusts,
    ///    and sand grit noise without any external asset dependencies.
    /// 5. Seamless Cutscene & Combat Continuity: Runs throughout the arrival cutscene and continues
    ///    into the final battle against Colossus-06.
    /// </summary>
    public class MartianDustStormSystem : MonoBehaviour
    {
        public static MartianDustStormSystem Instance { get; private set; }

        [Header("Storm Intensity (0 = High Orbit Clear, 1 = Raging Martian Tempest)")]
        [Range(0f, 1f)] public float stormIntensity = 0.1f;
        public float targetIntensity = 1.0f;
        public float intensityRampSpeed = 0.65f;

        [Header("Wind Dynamics")]
        public Vector3 windDirection = new Vector3(26.0f, -3.2f, 18.0f);
        public float windSpeed = 32.0f;

        [Header("Atmosphere & Fog Ranges")]
        public float orbitFogDensity = 0.0028f;
        public float stormFogDensity = 0.0098f;
        public Color orbitFogColor = new Color(0.84f, 0.44f, 0.24f);
        public Color stormFogColor = new Color(0.88f, 0.36f, 0.16f);

        [Header("Sunlight Lighting")]
        public Light martianSun;
        public float orbitSunIntensity = 2.4f;
        public float stormSunIntensity = 1.55f;

        [Header("Starship References")]
        public GameObject starship;
        public Light reEntryPlasmaLight;
        public ParticleSystem reEntryWakePS;

        [Header("Particle System")]
        public ParticleSystem cameraDustVolume;
        private ParticleSystem _billowCloudPS;
        private ParticleSystem _needleGritPS;

        [Header("Audio")]
        public bool enableProceduralWindAudio = true;
        [Range(0f, 1f)] public float masterWindVolume = 0.65f;
        private AudioSource _windAudioSource;
        private AudioSource _gustAudioSource;
        private AudioClip _proceduralWindLoop;
        private AudioClip _proceduralGustClip;

        private Camera _activeCamera;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnsureCameraAndSun();
            SetupProceduralAudio();
            SetupCameraDustVolume();
            SetupStarshipReEntryVFX();
        }

        private void Start()
        {
            ApplyAtmosphere(stormIntensity);
        }

        private void Update()
        {
            // Smoothly ramp towards target intensity
            if (Mathf.Abs(stormIntensity - targetIntensity) > 0.001f)
            {
                stormIntensity = Mathf.MoveTowards(stormIntensity, targetIntensity, Time.deltaTime * intensityRampSpeed);
                ApplyAtmosphere(stormIntensity);
            }

            // Keep camera dust volume tracking active camera
            UpdateCameraDustPosition();

            // Modulate audio volume
            UpdateAudioLevels();
        }

        public void SetStormIntensity(float target, float rampSpeed = -1f)
        {
            targetIntensity = Mathf.Clamp01(target);
            if (rampSpeed > 0f) intensityRampSpeed = rampSpeed;
        }

        public void EnsureCameraAndSun()
        {
            if (_activeCamera == null)
            {
                _activeCamera = Camera.main;
                if (_activeCamera == null) _activeCamera = FindFirstObjectByType<Camera>();
            }

            if (martianSun == null)
            {
                var sunGO = GameObject.Find("Martian_Sun");
                if (sunGO != null) martianSun = sunGO.GetComponent<Light>();
            }

            if (starship == null)
            {
                starship = GameObject.Find("Alita_Martian_Starship") ?? GameObject.Find("Alita_Lost_Martian_Starship");
            }
        }

        private void ApplyAtmosphere(float t)
        {
            // 1. Fog density and color
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = Mathf.Lerp(orbitFogDensity, stormFogDensity, t);
            RenderSettings.fogColor = Color.Lerp(orbitFogColor, stormFogColor, t);

            // 2. Ambient light warmth
            RenderSettings.ambientLight = Color.Lerp(
                new Color(0.74f, 0.40f, 0.25f),
                new Color(0.82f, 0.36f, 0.18f),
                t
            );

            // 3. Sun intensity
            if (martianSun != null)
            {
                martianSun.intensity = Mathf.Lerp(orbitSunIntensity, stormSunIntensity, t);
            }

            // 4. Modulate particle emission rates
            if (cameraDustVolume != null)
            {
                var em = cameraDustVolume.emission;
                em.rateOverTime = Mathf.Lerp(15f, 180f, t);
            }

            if (_billowCloudPS != null)
            {
                var em = _billowCloudPS.emission;
                em.rateOverTime = Mathf.Lerp(3f, 35f, t);
            }

            if (_needleGritPS != null)
            {
                var em = _needleGritPS.emission;
                em.rateOverTime = Mathf.Lerp(20f, 240f, t);
            }

            // 5. Starship re-entry heat glow
            if (reEntryPlasmaLight != null)
            {
                // Active when penetrating atmosphere high speed (t > 0.25)
                reEntryPlasmaLight.intensity = t > 0.2f ? Mathf.Lerp(0f, 6.0f, (t - 0.2f) / 0.8f) : 0f;
            }
        }

        private void SetupCameraDustVolume()
        {
            if (cameraDustVolume != null) return;

            var existing = GameObject.Find("Camera_Martian_Dust_Volume");
            if (existing != null)
            {
                cameraDustVolume = existing.GetComponent<ParticleSystem>();
                return;
            }

            GameObject dustGO = new GameObject("Camera_Martian_Dust_Volume");
            dustGO.transform.position = Vector3.zero;

            // Load materials
            Material dustMat = null;
            Material sparkMat = null;
#if UNITY_EDITOR
            dustMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Synty/PolygonParticles/Materials/PolygonParticleFX_Dust.mat");
            sparkMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Synty/PolygonParticles/Materials/PolygonParticleFX_Cutout_Round.mat");
#endif
            if (dustMat == null) dustMat = Resources.Load<Material>("FX_Addivitve_01");
            if (sparkMat == null) sparkMat = dustMat;

            // 1. Layer 1: High-Speed Sand Grit (Main)
            cameraDustVolume = dustGO.AddComponent<ParticleSystem>();
            var main = cameraDustVolume.main;
            main.playOnAwake = true;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(22f, 34f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.5f, 3.8f);
            main.startColor = new Color(0.92f, 0.44f, 0.20f, 0.55f);
            main.maxParticles = 600;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = cameraDustVolume.emission;
            emission.rateOverTime = 160f;

            var shape = cameraDustVolume.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(32f, 16f, 28f);

            var velOverLife = cameraDustVolume.velocityOverLifetime;
            velOverLife.enabled = true;
            velOverLife.space = ParticleSystemSimulationSpace.World;
            velOverLife.x = new ParticleSystem.MinMaxCurve(windDirection.x * 0.8f, windDirection.x * 1.2f);
            velOverLife.y = new ParticleSystem.MinMaxCurve(windDirection.y - 1f, windDirection.y + 1f);
            velOverLife.z = new ParticleSystem.MinMaxCurve(windDirection.z * 0.8f, windDirection.z * 1.2f);

            var colorOverLife = cameraDustVolume.colorOverLifetime;
            colorOverLife.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(0.92f, 0.44f, 0.20f), 0f), new GradientColorKey(new Color(0.85f, 0.35f, 0.15f), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.6f, 0.2f), new GradientAlphaKey(0.5f, 0.7f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLife.color = grad;

            var psr = dustGO.GetComponent<ParticleSystemRenderer>();
            if (dustMat != null) psr.sharedMaterial = dustMat;

            // 2. Layer 2: Billowing Dust Sheets (Child)
            GameObject billowGO = new GameObject("Billowing_Dust_Sheets");
            billowGO.transform.SetParent(dustGO.transform, false);
            _billowCloudPS = billowGO.AddComponent<ParticleSystem>();
            var bMain = _billowCloudPS.main;
            bMain.playOnAwake = true;
            bMain.loop = true;
            bMain.startLifetime = new ParticleSystem.MinMaxCurve(3.0f, 4.5f);
            bMain.startSpeed = new ParticleSystem.MinMaxCurve(12f, 20f);
            bMain.startSize = new ParticleSystem.MinMaxCurve(8f, 16f);
            bMain.startColor = new Color(0.86f, 0.38f, 0.18f, 0.30f);
            bMain.maxParticles = 180;
            bMain.simulationSpace = ParticleSystemSimulationSpace.World;

            var bEm = _billowCloudPS.emission;
            bEm.rateOverTime = 30f;

            var bShape = _billowCloudPS.shape;
            bShape.shapeType = ParticleSystemShapeType.Box;
            bShape.scale = new Vector3(36f, 18f, 30f);

            var bVel = _billowCloudPS.velocityOverLifetime;
            bVel.enabled = true;
            bVel.space = ParticleSystemSimulationSpace.World;
            bVel.x = windDirection.x * 0.65f;
            bVel.y = windDirection.y * 0.5f;
            bVel.z = windDirection.z * 0.65f;

            var bColor = _billowCloudPS.colorOverLifetime;
            bColor.enabled = true;
            bColor.color = grad;

            var bPsr = billowGO.GetComponent<ParticleSystemRenderer>();
            if (dustMat != null) bPsr.sharedMaterial = dustMat;

            // 3. Layer 3: High-Speed Sand Grit Streaks (Child)
            GameObject gritGO = new GameObject("Needle_Sand_Grit");
            gritGO.transform.SetParent(dustGO.transform, false);
            _needleGritPS = gritGO.AddComponent<ParticleSystem>();
            var gMain = _needleGritPS.main;
            gMain.playOnAwake = true;
            gMain.loop = true;
            gMain.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.3f);
            gMain.startSpeed = new ParticleSystem.MinMaxCurve(32f, 48f);
            gMain.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.32f);
            gMain.startColor = new Color(1.0f, 0.65f, 0.30f, 0.85f);
            gMain.maxParticles = 500;
            gMain.simulationSpace = ParticleSystemSimulationSpace.World;

            var gEm = _needleGritPS.emission;
            gEm.rateOverTime = 200f;

            var gShape = _needleGritPS.shape;
            gShape.shapeType = ParticleSystemShapeType.Box;
            gShape.scale = new Vector3(28f, 14f, 24f);

            var gVel = _needleGritPS.velocityOverLifetime;
            gVel.enabled = true;
            gVel.space = ParticleSystemSimulationSpace.World;
            gVel.x = windDirection.x * 1.3f;
            gVel.y = windDirection.y;
            gVel.z = windDirection.z * 1.3f;

            var gPsr = gritGO.GetComponent<ParticleSystemRenderer>();
            gPsr.renderMode = ParticleSystemRenderMode.Stretch;
            gPsr.velocityScale = 0.06f;
            gPsr.lengthScale = 2.8f;
            if (sparkMat != null) gPsr.sharedMaterial = sparkMat;
        }

        private void SetupStarshipReEntryVFX()
        {
            if (starship == null) return;

            // 1. Re-Entry Plasma Point Light
            if (reEntryPlasmaLight == null)
            {
                var existingLight = starship.transform.Find("ReEntry_Plasma_Light");
                if (existingLight != null)
                {
                    reEntryPlasmaLight = existingLight.GetComponent<Light>();
                }
                else
                {
                    GameObject lightGO = new GameObject("ReEntry_Plasma_Light");
                    lightGO.transform.SetParent(starship.transform, false);
                    lightGO.transform.localPosition = new Vector3(0f, 0.2f, 4.5f); // At nose
                    reEntryPlasmaLight = lightGO.AddComponent<Light>();
                    reEntryPlasmaLight.type = LightType.Point;
                    reEntryPlasmaLight.color = new Color(1.0f, 0.48f, 0.12f);
                    reEntryPlasmaLight.range = 30f;
                    reEntryPlasmaLight.intensity = 0f;
                }
            }

            // 2. Trailing Re-entry Wake Dust ParticleSystem
            if (reEntryWakePS == null)
            {
                var existingWake = starship.transform.Find("ReEntry_Wake_Dust");
                if (existingWake != null)
                {
                    reEntryWakePS = existingWake.GetComponent<ParticleSystem>();
                }
                else
                {
                    GameObject wakeGO = new GameObject("ReEntry_Wake_Dust");
                    wakeGO.transform.SetParent(starship.transform, false);
                    wakeGO.transform.localPosition = new Vector3(0f, 0f, -4.0f);

                    reEntryWakePS = wakeGO.AddComponent<ParticleSystem>();
                    var main = reEntryWakePS.main;
                    main.playOnAwake = true;
                    main.loop = true;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 14f);
                    main.startSize = new ParticleSystem.MinMaxCurve(3.5f, 7.5f);
                    main.startColor = new Color(0.92f, 0.42f, 0.16f, 0.45f);
                    main.simulationSpace = ParticleSystemSimulationSpace.World;

                    var em = reEntryWakePS.emission;
                    em.rateOverTime = 80f;

                    var shape = reEntryWakePS.shape;
                    shape.shapeType = ParticleSystemShapeType.Cone;
                    shape.angle = 18f;
                    shape.radius = 1.8f;
                    shape.rotation = new Vector3(0f, 180f, 0f);

                    var psr = wakeGO.GetComponent<ParticleSystemRenderer>();
#if UNITY_EDITOR
                    var dMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Synty/PolygonParticles/Materials/PolygonParticleFX_Dust.mat");
                    if (dMat != null) psr.sharedMaterial = dMat;
#endif
                }
            }
        }

        private void UpdateCameraDustPosition()
        {
            if (_activeCamera == null)
            {
                _activeCamera = Camera.main;
                if (_activeCamera == null) _activeCamera = FindFirstObjectByType<Camera>();
            }

            if (_activeCamera != null && cameraDustVolume != null)
            {
                // Position dust volume slightly in front of camera along wind offset
                Vector3 targetPos = _activeCamera.transform.position + _activeCamera.transform.forward * 10f - windDirection.normalized * 5f;
                cameraDustVolume.transform.position = targetPos;
            }
        }

        private void SetupProceduralAudio()
        {
            _windAudioSource = gameObject.AddComponent<AudioSource>();
            _windAudioSource.loop = true;
            _windAudioSource.playOnAwake = false;
            _windAudioSource.volume = 0f;

            _gustAudioSource = gameObject.AddComponent<AudioSource>();
            _gustAudioSource.loop = true;
            _gustAudioSource.playOnAwake = false;
            _gustAudioSource.volume = 0f;

            if (enableProceduralWindAudio)
            {
                // Authored seamless beds (Tools/SfxGen/make_mars_ambience.py); the in-engine synth is only a fallback
                _proceduralWindLoop = Resources.Load<AudioClip>("MarsSfx/Mars_Wind_Loop") ?? GenerateWindLoop(4.0f, 44100);
                _proceduralGustClip = Resources.Load<AudioClip>("MarsSfx/Mars_Gust_Loop") ?? GenerateGustLoop(5.5f, 44100);

                if (_proceduralWindLoop != null)
                {
                    _windAudioSource.clip = _proceduralWindLoop;
                    _windAudioSource.Play();
                }

                if (_proceduralGustClip != null)
                {
                    _gustAudioSource.clip = _proceduralGustClip;
                    _gustAudioSource.Play();
                }
            }
        }

        private void UpdateAudioLevels()
        {
            float targetVol = stormIntensity * masterWindVolume;
            if (_windAudioSource != null)
            {
                _windAudioSource.volume = Mathf.Lerp(_windAudioSource.volume, targetVol, Time.deltaTime * 3.5f);
                _windAudioSource.pitch = Mathf.Lerp(0.96f, 1.04f, stormIntensity);
            }

            if (_gustAudioSource != null)
            {
                // LFO surging gust volume
                float gustLFO = Mathf.Sin(Time.time * 1.8f) * 0.25f + 0.75f;
                _gustAudioSource.volume = Mathf.Lerp(_gustAudioSource.volume, targetVol * gustLFO * 0.75f, Time.deltaTime * 2.5f);
            }
        }

        private AudioClip GenerateWindLoop(float duration, int sampleRate)
        {
            int count = Mathf.FloorToInt(duration * sampleRate);
            float[] data = new float[count];

            float lowpass = 0f;
            float lowpass2 = 0f;

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;

                // Pink-ish filtered white noise for deep planetary wind rumble
                float white = Random.value * 2f - 1f;
                lowpass = Mathf.Lerp(lowpass, white, 0.045f);
                lowpass2 = Mathf.Lerp(lowpass2, lowpass, 0.055f);

                // Sub-bass resonant tone (52 Hz to 68 Hz undulating)
                float freq = 58f + Mathf.Sin(t * 2.1f) * 12f;
                float sub = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.35f;

                // Whistling wind resonance
                float whistleFreq = 280f + Mathf.Sin(t * 1.2f) * 80f;
                float whistle = Mathf.Sin(2f * Mathf.PI * whistleFreq * t) * 0.12f * (Mathf.Sin(t * 3.5f) * 0.5f + 0.5f);

                // Grit hiss
                float grit = (Random.value * 2f - 1f) * 0.04f;

                data[i] = Mathf.Clamp((lowpass2 * 1.8f + sub + whistle + grit) * 0.70f, -0.98f, 0.98f);
            }

            var clip = AudioClip.Create("Martian_Dust_Storm_Wind", count, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip GenerateGustLoop(float duration, int sampleRate)
        {
            int count = Mathf.FloorToInt(duration * sampleRate);
            float[] data = new float[count];

            float filter = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                float white = Random.value * 2f - 1f;
                filter = Mathf.Lerp(filter, white, 0.08f);

                // Howling pitch wave
                float pitchMod = Mathf.Sin(t * 0.9f) * 45f + 180f;
                float howl = Mathf.Sin(2f * Mathf.PI * pitchMod * t) * 0.22f;

                data[i] = Mathf.Clamp((filter * 1.2f + howl) * 0.65f, -0.98f, 0.98f);
            }

            var clip = AudioClip.Create("Martian_Dust_Storm_Gusts", count, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
