using System.Collections.Generic;
using UnityEngine;

namespace CyberpunkDev.Traffic
{
    /// <summary>
    /// AAA Procedural 3D Audio Manager for Flying Cyberpunk Air Traffic.
    /// Manages a fixed pool of 6 spatialized 3D AudioSources dynamically mapped to the nearest cars,
    /// plus a 2D ambient city traffic drone bed.
    /// Clips are synthesized in memory following the CyberCombatAudioSynth pattern (zero external audio files needed).
    /// </summary>
    public class FlyingCarAudioManager : MonoBehaviour
    {
        [Header("Settings Reference")]
        [SerializeField] private TrafficSettings settings;

        private AudioSource m_AmbientDroneSource;
        private AudioSource[] m_Pooled3DSources;
        private HoverCar[] m_AssignedCars;
        private float[] m_CarLastDistances;

        private AudioClip m_EngineHumLoop;
        private AudioClip m_PassByWhoosh;
        private AudioClip m_AmbientDroneLoop;

        private Transform m_ListenerTransform;
        private float m_NextReassignTime;
        private const int SampleRate = 44100;

        public void Initialize(TrafficSettings trafficSettings, Transform listener = null)
        {
            settings = trafficSettings;
            m_ListenerTransform = listener;

            if (m_ListenerTransform == null)
            {
                var mainCam = Camera.main;
                if (mainCam != null)
                    m_ListenerTransform = mainCam.transform;
            }

            GenerateProceduralAudioClips();
            SetupAudioPool();
            SetupAmbientDrone();
        }

        private void Awake()
        {
            if (settings != null && m_Pooled3DSources == null)
            {
                Initialize(settings);
            }
        }

        private void Update()
        {
            if (settings == null) return;

            // Resolve listener if missing
            if (m_ListenerTransform == null)
            {
                var mainCam = Camera.main;
                if (mainCam != null) m_ListenerTransform = mainCam.transform;
                if (m_ListenerTransform == null) return;
            }

            Vector3 listenerPos = m_ListenerTransform.position;
            float maxAudibleDist = settings.audioMaxDistance;
            float dt = Time.deltaTime;

            // Sync pooled audio sources to their assigned cars (strictly proximity-only)
            if (m_Pooled3DSources != null && m_AssignedCars != null)
            {
                for (int i = 0; i < m_Pooled3DSources.Length; i++)
                {
                    var source = m_Pooled3DSources[i];
                    var car = m_AssignedCars[i];

                    if (source == null) continue;

                    if (car != null && car.gameObject.activeInHierarchy && car.IsMeshVisible)
                    {
                        float currentDist = Vector3.Distance(car.transform.position, listenerPos);

                        // If car is beyond max audible distance, silence it immediately
                        if (currentDist > maxAudibleDist)
                        {
                            if (source.isPlaying) source.Stop();
                            m_AssignedCars[i] = null;
                            continue;
                        }

                        source.transform.position = car.transform.position;
                        if (!source.isPlaying) source.Play();

                        // Dynamic pitch based on car speed
                        float speedRatio = car.currentSpeed / Mathf.Max(settings.minCarSpeed, 1f);
                        source.pitch = Mathf.Lerp(0.92f, 1.15f, (speedRatio - 0.8f) * 0.7f);

                        // Dynamic proximity volume attenuation
                        float proximityT = 1.0f - Mathf.Clamp01(currentDist / maxAudibleDist);
                        source.volume = proximityT * settings.engineHumVolume * settings.masterTrafficVolume;

                        // Clean flyby whoosh detection at closest point of approach
                        float prevDist = m_CarLastDistances[i];
                        m_CarLastDistances[i] = currentDist;

                        if (car.whooshCooldown > 0f) car.whooshCooldown -= dt;

                        if (currentDist < settings.passByTriggerDistance)
                        {
                            if (prevDist > currentDist)
                            {
                                car.isApproachingListener = true;
                            }
                            else if (car.isApproachingListener && currentDist > prevDist && car.whooshCooldown <= 0f)
                            {
                                // Crossed closest point of approach: play one clean flyby whoosh
                                car.isApproachingListener = false;
                                car.whooshCooldown = 4.0f;
                                source.PlayOneShot(m_PassByWhoosh, settings.passByWhooshVolume * settings.masterTrafficVolume);
                            }
                        }
                        else
                        {
                            car.isApproachingListener = false;
                        }
                    }
                    else
                    {
                        if (source.isPlaying) source.Stop();
                    }
                }
            }
        }

        /// <summary>
        /// Periodically updates which hover cars are assigned the nearest pooled 3D sound voices.
        /// STRICT PROXIMITY RULE: Only cars within settings.audioMaxDistance are ever assigned.
        /// </summary>
        public void UpdateAudioAssignments(List<HoverCar> activeCars)
        {
            if (settings == null || activeCars == null || activeCars.Count == 0) return;
            if (m_ListenerTransform == null)
            {
                var mainCam = Camera.main;
                if (mainCam != null) m_ListenerTransform = mainCam.transform;
                if (m_ListenerTransform == null) return;
            }

            if (Time.time < m_NextReassignTime) return;
            m_NextReassignTime = Time.time + settings.audioReassignInterval;

            int poolSize = m_Pooled3DSources != null ? m_Pooled3DSources.Length : 0;
            if (poolSize == 0) return;

            Vector3 listenerPos = m_ListenerTransform.position;
            float maxDistSqr = settings.audioMaxDistance * settings.audioMaxDistance;

            int maxTop = Mathf.Min(poolSize, activeCars.Count);
            for (int slot = 0; slot < maxTop; slot++)
            {
                // Find nearest car that hasn't been chosen yet AND is within max audible distance
                float nearestDistSqr = maxDistSqr;
                HoverCar nearestCar = null;

                for (int c = 0; c < activeCars.Count; c++)
                {
                    var candidate = activeCars[c];
                    if (candidate == null || !candidate.gameObject.activeInHierarchy) continue;

                    bool alreadyPicked = false;
                    for (int prev = 0; prev < slot; prev++)
                    {
                        if (m_AssignedCars[prev] == candidate)
                        {
                            alreadyPicked = true;
                            break;
                        }
                    }
                    if (alreadyPicked) continue;

                    float dSqr = (candidate.transform.position - listenerPos).sqrMagnitude;
                    if (dSqr < nearestDistSqr)
                    {
                        nearestDistSqr = dSqr;
                        nearestCar = candidate;
                    }
                }

                m_AssignedCars[slot] = nearestCar;
                if (nearestCar != null)
                {
                    m_Pooled3DSources[slot].transform.position = nearestCar.transform.position;
                    m_CarLastDistances[slot] = Mathf.Sqrt(nearestDistSqr);
                    if (!m_Pooled3DSources[slot].isPlaying)
                    {
                        m_Pooled3DSources[slot].Play();
                    }
                }
                else
                {
                    if (m_Pooled3DSources[slot].isPlaying)
                    {
                        m_Pooled3DSources[slot].Stop();
                    }
                }
            }

            // Silence any remaining unassigned pool sources
            for (int slot = maxTop; slot < poolSize; slot++)
            {
                m_AssignedCars[slot] = null;
                if (m_Pooled3DSources[slot].isPlaying)
                    m_Pooled3DSources[slot].Stop();
            }
        }

        private void SetupAudioPool()
        {
            Transform existingPool = transform.Find("3D_Traffic_Audio_Pool");
            if (existingPool != null)
            {
                if (Application.isPlaying) Destroy(existingPool.gameObject);
                else DestroyImmediate(existingPool.gameObject);
            }

            int poolSize = settings != null ? settings.maxPooledAudioSources : 4;
            m_Pooled3DSources = new AudioSource[poolSize];
            m_AssignedCars = new HoverCar[poolSize];
            m_CarLastDistances = new float[poolSize];

            GameObject poolRoot = new GameObject("3D_Traffic_Audio_Pool");
            poolRoot.transform.SetParent(transform);

            for (int i = 0; i < poolSize; i++)
            {
                GameObject voiceGO = new GameObject($"HoverCar_Voice_{i:D2}");
                voiceGO.transform.SetParent(poolRoot.transform);

                AudioSource src = voiceGO.AddComponent<AudioSource>();
                src.clip = m_EngineHumLoop;
                src.loop = true;
                src.playOnAwake = false;
                src.spatialBlend = 1.0f; // Pure 3D spatial sound
                src.minDistance = settings.audioMinDistance;
                src.maxDistance = settings.audioMaxDistance;
                src.rolloffMode = AudioRolloffMode.Linear;
                src.dopplerLevel = 0.8f;
                src.spread = 35f;
                src.volume = settings.engineHumVolume * settings.masterTrafficVolume;

                m_Pooled3DSources[i] = src;
                m_CarLastDistances[i] = 999f;
            }
        }

        private void SetupAmbientDrone()
        {
            Transform existingDrone = transform.Find("2D_Ambient_Traffic_Bed");
            if (existingDrone != null)
            {
                if (Application.isPlaying) Destroy(existingDrone.gameObject);
                else DestroyImmediate(existingDrone.gameObject);
            }

            if (settings == null || !settings.enableAmbientDrone)
            {
                // Drone is disabled per user request: silence unless cars are directly near
                return;
            }

            GameObject droneGO = new GameObject("2D_Ambient_Traffic_Bed");
            droneGO.transform.SetParent(transform);

            m_AmbientDroneSource = droneGO.AddComponent<AudioSource>();
            m_AmbientDroneSource.clip = m_AmbientDroneLoop;
            m_AmbientDroneSource.loop = true;
            m_AmbientDroneSource.playOnAwake = true;
            m_AmbientDroneSource.spatialBlend = 0.0f;
            m_AmbientDroneSource.volume = settings.ambientCityTrafficVolume * settings.masterTrafficVolume;
            m_AmbientDroneSource.Play();
        }

        // =========================================================================
        // PROCEDURAL AUDIO SYNTHESIS (Zero External File Dependencies)
        // Warm, cinematic, pleasant cyberpunk repulsor sound (no harsh buzzy hum)
        // =========================================================================
        private void GenerateProceduralAudioClips()
        {
            m_EngineHumLoop = SynthesizeEngineHumLoop();
            m_PassByWhoosh = SynthesizePassByWhoosh();
            m_AmbientDroneLoop = SynthesizeAmbientTrafficBed();
        }

        /// <summary>
        /// Generates a warm, smooth, futuristic sci-fi repulsor jet turbine hum.
        /// Uses gentle harmonic sines and soft low-pass air glide (eliminates harsh buzzy hum and hiss).
        /// </summary>
        private AudioClip SynthesizeEngineHumLoop()
        {
            float duration = 2.0f;
            int samples = (int)(SampleRate * duration);
            float[] buffer = new float[samples];

            float f0 = 98.0f;  // Warm G2 fundamental
            float f1 = 196.0f; // Soft octave harmonic
            float f2 = 293.6f; // Fifth overtone

            float prevFilter = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;

                // Gentle modulation (hover repulsor pulse)
                float pulse = 1.0f + 0.06f * Mathf.Sin(2f * Mathf.PI * 3.2f * t);

                // Warm harmonic oscillators
                float s0 = Mathf.Sin(2f * Mathf.PI * f0 * t);
                float s1 = Mathf.Sin(2f * Mathf.PI * f1 * t + 0.4f) * 0.35f;
                float s2 = Mathf.Sin(2f * Mathf.PI * f2 * t + 0.9f) * 0.15f;

                // Soft low-passed air glide (warm aerodynamic air flow)
                float noise = (Random.value * 2f - 1f) * 0.035f;
                prevFilter = Mathf.Lerp(prevFilter, noise, 0.15f);

                float sampleVal = ((s0 * 0.6f + s1 + s2) * pulse + prevFilter) * 0.45f;

                // Seamless loop boundary cross-fade
                float edgeFade = 1.0f;
                float edgeSamples = SampleRate * 0.04f;
                if (i < edgeSamples) edgeFade = (float)i / edgeSamples;
                else if (i > samples - edgeSamples) edgeFade = (float)(samples - i) / edgeSamples;

                buffer[i] = sampleVal * edgeFade;
            }

            AudioClip clip = AudioClip.Create("Procedural_HoverCar_Hum", samples, 1, SampleRate, false);
            clip.SetData(buffer, 0);
            return clip;
        }

        /// <summary>
        /// Generates a smooth, aerodynamic jet pass-by whoosh for close flybys.
        /// </summary>
        private AudioClip SynthesizePassByWhoosh()
        {
            float duration = 0.9f;
            int samples = (int)(SampleRate * duration);
            float[] buffer = new float[samples];

            float prevFilter = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float progress = t / duration;

                // Smooth bell envelope
                float env = Mathf.Sin(progress * Mathf.PI);
                env = env * env; // Smooth raised cosine

                // Sweeping tone
                float freq = Mathf.Lerp(380f, 95f, progress * progress);
                float tone = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.3f;

                // Filtered aerodynamic air whoosh
                float rawNoise = (Random.value * 2f - 1f) * 0.4f;
                prevFilter = Mathf.Lerp(prevFilter, rawNoise, 0.25f);

                buffer[i] = (tone + prevFilter) * env * 0.5f;
            }

            AudioClip clip = AudioClip.Create("Procedural_PassBy_Whoosh", samples, 1, SampleRate, false);
            clip.SetData(buffer, 0);
            return clip;
        }

        /// <summary>
        /// Generates a subtle, low atmospheric city traffic drone bed.
        /// </summary>
        private AudioClip SynthesizeAmbientTrafficBed()
        {
            float duration = 4.0f;
            int samples = (int)(SampleRate * duration);
            float[] buffer = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float drone1 = Mathf.Sin(2f * Mathf.PI * 50f * t);
                float drone2 = Mathf.Sin(2f * Mathf.PI * 60f * t + 0.5f);
                float modWave = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 0.2f * t);

                float sampleVal = (drone1 * 0.4f + drone2 * 0.3f) * (0.6f + 0.4f * modWave);
                buffer[i] = sampleVal * 0.15f;
            }

            AudioClip clip = AudioClip.Create("Procedural_Ambient_CityTraffic", samples, 1, SampleRate, false);
            clip.SetData(buffer, 0);
            return clip;
        }
    }
}

