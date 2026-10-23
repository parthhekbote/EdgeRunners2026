using System.Collections;
using UnityEngine;

namespace PolygonMech
{
    public enum BoosterFlameState
    {
        Off,
        Standby,
        Thrust,
        Overdrive
    }

    /// <summary>
    /// Controls the rocket booster propulsion effects on the rear nozzle of the Titan Mech's massive hammer.
    /// Manages multi-layered supersonic jet flames, directional trailing embers, billowing exhaust smoke,
    /// dynamic combustion lighting, and synchronized thruster audio.
    /// </summary>
    public class HammerRocketBooster : MonoBehaviour
    {
        [Header("State")]
        [SerializeField] private BoosterFlameState currentState = BoosterFlameState.Standby;
        public BoosterFlameState CurrentState => currentState;

        [Header("Particle Systems")]
        public ParticleSystem pilotFlamePS;
        public ParticleSystem coreFlamePS;
        public ParticleSystem outerFlamePS;
        public ParticleSystem exhaustEmbersPS;
        public ParticleSystem exhaustSmokePS;
        public ParticleSystem impactBackfirePS;

        [Header("Dynamic Lighting")]
        public Light boosterLight;
        public float standbyLightIntensity = 1.0f;
        public float thrustLightIntensity = 6.0f;
        public float overdriveLightIntensity = 12.0f;
        public Color thrustLightColor = new Color(1.0f, 0.45f, 0.12f, 1.0f);
        public float lightFlickerSpeed = 38f;
        public float lightFlickerAmount = 0.25f;

        [Header("Audio")]
        public AudioSource audioSource;
        public AudioClip thrusterIgnitionSFX;
        public AudioClip thrusterLoopSFX;
        [Range(0f, 1f)] public float thrusterVolume = 0.85f;

        // Internal State
        private Coroutine activeBurstCoroutine;
        private float currentLightTargetIntensity = 1.0f;
        private float currentLightActualIntensity = 1.0f;
        private bool isAudioLooping = false;

        private void Awake()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                    audioSource.spatialBlend = 1.0f;
                    audioSource.minDistance = 3.0f;
                    audioSource.maxDistance = 65.0f;
                    audioSource.rolloffMode = AudioRolloffMode.Linear;
                }
            }

            if (boosterLight == null)
            {
                boosterLight = GetComponentInChildren<Light>();
            }

            // Default to standby pilot flame
            SetState(BoosterFlameState.Standby);
        }

        private void Update()
        {
            UpdateDynamicLighting();
        }

        private void UpdateDynamicLighting()
        {
            if (boosterLight == null) return;

            // Smooth lerp to target intensity
            currentLightActualIntensity = Mathf.Lerp(currentLightActualIntensity, currentLightTargetIntensity, Time.deltaTime * 18f);

            // Apply realistic turbulence combustion flicker during active thrust
            float flicker = 1.0f;
            if (currentState == BoosterFlameState.Thrust || currentState == BoosterFlameState.Overdrive)
            {
                float noise = Mathf.PerlinNoise(Time.time * lightFlickerSpeed, 0.0f);
                flicker = 1.0f + (noise - 0.5f) * lightFlickerAmount * 2.0f;
            }

            boosterLight.intensity = currentLightActualIntensity * flicker;
            boosterLight.enabled = boosterLight.intensity > 0.05f;
        }

        public void SetState(BoosterFlameState newState)
        {
            currentState = newState;

            switch (newState)
            {
                case BoosterFlameState.Off:
                    StopAllParticles();
                    currentLightTargetIntensity = 0f;
                    StopAudioLoop();
                    break;

                case BoosterFlameState.Standby:
                    // Only gentle pilot flame active
                    SafePlay(pilotFlamePS);
                    SafeStop(coreFlamePS);
                    SafeStop(outerFlamePS);
                    SafeStop(exhaustEmbersPS);
                    SafeStop(exhaustSmokePS);

                    currentLightTargetIntensity = standbyLightIntensity;
                    if (boosterLight != null) boosterLight.color = new Color(1.0f, 0.40f, 0.15f);
                    StopAudioLoop();
                    break;

                case BoosterFlameState.Thrust:
                    // Full supersonic rocket plume ignited
                    SafePlay(pilotFlamePS);
                    SafePlay(coreFlamePS);
                    SafePlay(outerFlamePS);
                    SafePlay(exhaustEmbersPS);
                    SafePlay(exhaustSmokePS);

                    currentLightTargetIntensity = thrustLightIntensity;
                    if (boosterLight != null) boosterLight.color = thrustLightColor;
                    PlayIgnitionSound();
                    break;

                case BoosterFlameState.Overdrive:
                    // Extreme rocket burst for heavy slams & leaps
                    SafePlay(pilotFlamePS, 1.2f);
                    SafePlay(coreFlamePS, 1.5f);
                    SafePlay(outerFlamePS, 1.5f);
                    SafePlay(exhaustEmbersPS, 1.8f);
                    SafePlay(exhaustSmokePS, 1.6f);

                    currentLightTargetIntensity = overdriveLightIntensity;
                    if (boosterLight != null) boosterLight.color = new Color(1.0f, 0.65f, 0.25f);
                    PlayIgnitionSound();
                    break;
            }
        }

        /// <summary>
        /// Fires a violent rocket booster burst for a specified duration (e.g. during a hammer swing or dash).
        /// Automatically returns to Standby when finished.
        /// </summary>
        public void TriggerBurst(float duration, bool overdrive = false)
        {
            if (activeBurstCoroutine != null)
            {
                StopCoroutine(activeBurstCoroutine);
            }
            activeBurstCoroutine = StartCoroutine(BurstRoutine(duration, overdrive));
        }

        private IEnumerator BurstRoutine(float duration, bool overdrive)
        {
            SetState(overdrive ? BoosterFlameState.Overdrive : BoosterFlameState.Thrust);
            yield return new WaitForSeconds(duration);
            SetState(BoosterFlameState.Standby);
            activeBurstCoroutine = null;
        }

        /// <summary>
        /// Triggers an explosive backfire pop and spark eruption on ground impact.
        /// </summary>
        public void TriggerImpactBackfire()
        {
            if (impactBackfirePS != null)
            {
                impactBackfirePS.Play();
            }

            // Instantaneous flash
            currentLightActualIntensity = overdriveLightIntensity * 1.5f;

            if (audioSource != null)
            {
                AudioClip popSFX = MechAudioSynth.GetPneumaticRelease();
                if (popSFX != null)
                {
                    audioSource.pitch = Random.Range(0.85f, 1.15f);
                    audioSource.PlayOneShot(popSFX, 0.8f);
                }
            }
        }

        private void PlayIgnitionSound()
        {
            if (audioSource == null) return;

            AudioClip clip = thrusterIgnitionSFX != null ? thrusterIgnitionSFX : MechAudioSynth.GetThrusterIgnition();
            if (clip != null)
            {
                audioSource.pitch = Random.Range(0.92f, 1.08f);
                audioSource.PlayOneShot(clip, thrusterVolume);
            }
        }

        private void StopAudioLoop()
        {
            if (audioSource != null && audioSource.isPlaying && isAudioLooping)
            {
                audioSource.Stop();
                isAudioLooping = false;
            }
        }

        private void SafePlay(ParticleSystem ps, float multiplier = 1.0f)
        {
            if (ps == null) return;
            var em = ps.emission;
            em.enabled = true;
            em.rateOverTimeMultiplier = multiplier;
            if (!ps.isPlaying) ps.Play();
        }

        private void SafeStop(ParticleSystem ps)
        {
            if (ps == null) return;
            var em = ps.emission;
            em.enabled = false;
        }

        private void StopAllParticles()
        {
            SafeStop(pilotFlamePS);
            SafeStop(coreFlamePS);
            SafeStop(outerFlamePS);
            SafeStop(exhaustEmbersPS);
            SafeStop(exhaustSmokePS);
            SafeStop(impactBackfirePS);
        }
    }
}
