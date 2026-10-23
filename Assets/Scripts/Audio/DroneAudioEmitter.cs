using UnityEngine;

namespace Cyberpunk.Audio
{
    /// <summary>
    /// Attaches 3D spatialized sci-fi engine audio to surveillance and patrol drones.
    /// STRICTLY SPATIALIZED: Only audible when directly near the drone (e.g. 1m - 8m).
    /// Uses Linear rolloff so volume drops to absolute zero beyond maxDistance.
    /// </summary>
    [DisallowMultipleComponent]
    public class DroneAudioEmitter : MonoBehaviour
    {
        [Header("Audio Settings")]
        [Tooltip("The sci-fi drone sound effect audio clip.")]
        public AudioClip droneAudioClip;

        [Range(0f, 1f)]
        [Tooltip("Volume of the drone engine when standing right next to it.")]
        public float volume = 0.50f;

        [Range(0.5f, 2f)]
        [Tooltip("Base audio pitch.")]
        public float basePitch = 1.0f;

        [Tooltip("Full volume distance. Only within this radius is the sound at 100%.")]
        public float minDistance = 1.0f;

        [Tooltip("Hard cutoff distance. BEYOND THIS DISTANCE, THE DRONE IS COMPLETELY SILENT (0.0 volume).")]
        public float maxDistance = 8.0f;

        [Header("Engine Simulation")]
        [Tooltip("Subtle pitch oscillation simulating turbine / rotor thruster hover adjustments.")]
        public bool enableEngineModulation = true;

        [Range(0f, 0.1f)]
        public float pitchModulationAmount = 0.035f;

        [Range(0.5f, 5f)]
        public float pitchModulationSpeed = 2.2f;

        private AudioSource _audioSource;
        private float _phaseOffset;

        private void Awake()
        {
            _phaseOffset = Random.Range(0f, 100f);
            InitializeAudioSource();
        }

        private void OnEnable()
        {
            if (_audioSource != null && !_audioSource.isPlaying && _audioSource.clip != null)
            {
                _audioSource.Play();
            }
        }

        public void InitializeAudioSource()
        {
            // If there's an animated Drone_Locator child, attach the AudioSource to it so sound travels strictly with the drone mesh
            Transform soundTarget = transform.Find("Drone_Locator");
            if (soundTarget == null)
            {
                soundTarget = transform;
            }

            _audioSource = soundTarget.GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = soundTarget.gameObject.AddComponent<AudioSource>();
            }

            // Assign clip if not already set or default
            if (droneAudioClip == null)
            {
                droneAudioClip = Resources.Load<AudioClip>("Audio/Drones/sci-fi-drone-sound-effect");
            }

            if (_audioSource.clip == null && droneAudioClip != null)
            {
                _audioSource.clip = droneAudioClip;
            }

            _audioSource.volume = volume;
            _audioSource.pitch = basePitch;
            _audioSource.loop = true;
            _audioSource.playOnAwake = true;
            
            // STRICT 3D SPATIAL AUDIO:
            // 1.0 = 100% 3D spatial (0% 2D bleed). Sound position is strictly evaluated in 3D space.
            _audioSource.spatialBlend = 1.0f; 
            
            // Linear rolloff drops strictly to 0 at maxDistance (unlike Logarithmic which bleeds infinitely)
            _audioSource.rolloffMode = AudioRolloffMode.Linear;
            _audioSource.minDistance = Mathf.Max(0.5f, minDistance);
            _audioSource.maxDistance = Mathf.Max(minDistance + 1f, maxDistance);
            _audioSource.spread = 0f;
            _audioSource.dopplerLevel = 0.2f;

            if (_audioSource.clip != null && !_audioSource.isPlaying)
            {
                _audioSource.Play();
            }
        }

        private void Update()
        {
            if (!enableEngineModulation || _audioSource == null) return;

            float mod = Mathf.Sin(Time.time * pitchModulationSpeed + _phaseOffset) * pitchModulationAmount;
            _audioSource.pitch = basePitch + mod;
        }

        public void SetClip(AudioClip clip)
        {
            droneAudioClip = clip;
            if (_audioSource != null)
            {
                _audioSource.clip = clip;
                if (isActiveAndEnabled && !_audioSource.isPlaying && clip != null)
                {
                    _audioSource.Play();
                }
            }
        }

        private void OnValidate()
        {
            if (_audioSource != null)
            {
                _audioSource.spatialBlend = 1.0f;
                _audioSource.rolloffMode = AudioRolloffMode.Linear;
                _audioSource.minDistance = minDistance;
                _audioSource.maxDistance = maxDistance;
                _audioSource.volume = volume;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Transform t = transform.Find("Drone_Locator") ?? transform;
            Gizmos.color = new Color(0f, 1f, 0.2f, 0.4f);
            Gizmos.DrawWireSphere(t.position, minDistance);
            Gizmos.color = new Color(1f, 0.2f, 0f, 0.25f);
            Gizmos.DrawWireSphere(t.position, maxDistance);
        }
    }
}
