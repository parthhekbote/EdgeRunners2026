using UnityEngine;

namespace SyntyCombined
{
    /// <summary>
    /// High-tech reinforced blast door at the end of the Datacenter Server Room.
    /// Acts as the containment threshold leading directly into Blackwall Cyberspace.
    /// 
    /// Features:
    /// - Proximity detection: Automatically detects approaching player
    /// - Pneumatic double sliding door panels with smooth mechanical lerp
    /// - Dynamic biometric/security indicator lights (locked crimson -> open cyber cyan)
    /// - High-fidelity sci-fi audio feedback (built-in procedural synthesizer fallback)
    /// - Cyberspace threshold traversal detection & transition events
    /// </summary>
    public class BlackwallGatewayDoor : MonoBehaviour
    {
        [Header("Door Panels")]
        public Transform leftDoorPanel;
        public Transform rightDoorPanel;
        public float openSlideDistance = 1.85f;
        public float openSpeed = 3.5f;

        [Header("Status Indicator")]
        public Light indicatorLight;
        public Color lockedColor = new Color(1.0f, 0.05f, 0.12f, 1f);
        public Color openColor = new Color(0.1f, 0.95f, 1.0f, 1f);

        [Header("Audio")]
        public AudioSource audioSource;
        public AudioClip doorOpenSound;
        public AudioClip transitionGlitchSound;

        [Header("Trigger Settings")]
        public float triggerDistance = 6.5f;
        public bool keepOpenOnceTriggered = false;

        [Header("Threshold FX")]
        public ParticleSystem thresholdFX;

        private Transform _playerTransform;
        private bool _isOpen = false;
        private bool _hasCrossedThreshold = false;
        private Vector3 _leftClosedLocalPos;
        private Vector3 _rightClosedLocalPos;

        private static AudioClip _cachedProceduralOpenSound;
        private static AudioClip _cachedProceduralTransitionSound;

        private void Awake()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
            }

            audioSource.spatialBlend = 0.85f;
            audioSource.playOnAwake = false;
            audioSource.minDistance = 3.0f;
            audioSource.maxDistance = 30.0f;

            if (doorOpenSound == null)
            {
                doorOpenSound = GetOrCreateProceduralDoorSound();
            }

            if (transitionGlitchSound == null)
            {
                transitionGlitchSound = GetOrCreateProceduralGlitchSound();
            }
        }

        private void Start()
        {
            FindPlayer();

            if (leftDoorPanel != null) _leftClosedLocalPos = leftDoorPanel.localPosition;
            if (rightDoorPanel != null) _rightClosedLocalPos = rightDoorPanel.localPosition;

            if (indicatorLight != null)
            {
                indicatorLight.color = lockedColor;
                indicatorLight.intensity = 3.0f;
            }
        }

        private void FindPlayer()
        {
            if (_playerTransform == null)
            {
                var p = GameObject.FindWithTag("Player");
                if (p != null) _playerTransform = p.transform;
            }
        }

        private void Update()
        {
            FindPlayer();
            if (_playerTransform == null) return;

            float dist = Vector3.Distance(transform.position, _playerTransform.position);
            bool shouldOpen = dist <= triggerDistance;

            if (keepOpenOnceTriggered && _isOpen)
            {
                shouldOpen = true;
            }

            if (shouldOpen != _isOpen)
            {
                _isOpen = shouldOpen;
                OnDoorStateChanged(_isOpen);
            }

            // Smooth sliding animation
            if (leftDoorPanel != null)
            {
                Vector3 targetLeft = _leftClosedLocalPos + (_isOpen ? Vector3.left * openSlideDistance : Vector3.zero);
                leftDoorPanel.localPosition = Vector3.Lerp(leftDoorPanel.localPosition, targetLeft, Time.deltaTime * openSpeed);
            }

            if (rightDoorPanel != null)
            {
                Vector3 targetRight = _rightClosedLocalPos + (_isOpen ? Vector3.right * openSlideDistance : Vector3.zero);
                rightDoorPanel.localPosition = Vector3.Lerp(rightDoorPanel.localPosition, targetRight, Time.deltaTime * openSpeed);
            }

            // Dynamic indicator light
            if (indicatorLight != null)
            {
                if (_isOpen)
                {
                    indicatorLight.color = Color.Lerp(indicatorLight.color, openColor, Time.deltaTime * 6f);
                    indicatorLight.intensity = Mathf.Lerp(indicatorLight.intensity, 5.0f, Time.deltaTime * 6f);
                }
                else
                {
                    indicatorLight.color = Color.Lerp(indicatorLight.color, lockedColor, Time.deltaTime * 6f);
                    float pulse = Mathf.Sin(Time.time * 4f) * 0.4f + 0.6f;
                    indicatorLight.intensity = 2.8f * pulse;
                }
            }

            // Threshold traversal check (stepping through into Blackwall cyberspace)
            float doorZ = transform.position.z;
            float playerZ = _playerTransform.position.z;

            if (!_hasCrossedThreshold && playerZ > doorZ + 0.5f)
            {
                _hasCrossedThreshold = true;
                OnCrossedIntoCyberspace();
            }
            else if (_hasCrossedThreshold && playerZ < doorZ - 1.5f)
            {
                _hasCrossedThreshold = false;
                Debug.Log("<color=#FF0055><b>[Blackwall Gateway]</b></color> Player stepped back into Server Room.");
            }
        }

        private void OnDoorStateChanged(bool open)
        {
            if (open)
            {
                if (audioSource != null && doorOpenSound != null)
                {
                    audioSource.pitch = Random.Range(0.96f, 1.04f);
                    audioSource.PlayOneShot(doorOpenSound, 0.85f);
                }
                if (thresholdFX != null && !thresholdFX.isPlaying)
                {
                    thresholdFX.Play();
                }
                Debug.Log("<color=#00FFAA><b>[Blackwall Gateway]</b></color> Heavy blast door unsealed! Opening entrance to Blackwall...");
            }
            else
            {
                if (audioSource != null && doorOpenSound != null)
                {
                    audioSource.pitch = Random.Range(0.85f, 0.92f);
                    audioSource.PlayOneShot(doorOpenSound, 0.6f);
                }
                Debug.Log("<color=#FF0055><b>[Blackwall Gateway]</b></color> Blast door sealed.");
            }
        }

        private void OnCrossedIntoCyberspace()
        {
            if (audioSource != null && transitionGlitchSound != null)
            {
                audioSource.PlayOneShot(transitionGlitchSound, 0.95f);
            }
            Debug.Log("<color=#FF0044><b>[Blackwall Gateway]</b></color> <b>BREACH: Player entered Blackwall Cyberspace realm!</b>");
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                if (!_isOpen)
                {
                    _isOpen = true;
                    OnDoorStateChanged(true);
                }
            }
        }

        public void SetOpenImmediate(bool open)
        {
            _isOpen = open;
            if (leftDoorPanel != null)
            {
                if (_leftClosedLocalPos == Vector3.zero) _leftClosedLocalPos = leftDoorPanel.localPosition;
                leftDoorPanel.localPosition = _leftClosedLocalPos + (open ? Vector3.left * openSlideDistance : Vector3.zero);
            }
            if (rightDoorPanel != null)
            {
                if (_rightClosedLocalPos == Vector3.zero) _rightClosedLocalPos = rightDoorPanel.localPosition;
                rightDoorPanel.localPosition = _rightClosedLocalPos + (open ? Vector3.right * openSlideDistance : Vector3.zero);
            }
            if (indicatorLight != null)
            {
                indicatorLight.color = open ? openColor : lockedColor;
                indicatorLight.intensity = open ? 5.0f : 3.0f;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.2f, 0.3f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, triggerDistance);
        }

        #region Procedural Audio Synthesizer
        private static AudioClip GetOrCreateProceduralDoorSound()
        {
            if (_cachedProceduralOpenSound != null) return _cachedProceduralOpenSound;

            int sampleRate = 44100;
            float duration = 1.25f;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Clamp01(1f - (t / duration));

                // Airlock hiss (filtered noise)
                float noise = (Random.value * 2f - 1f) * Mathf.Exp(-t * 3.5f) * 0.45f;

                // Mechanical servo whine (swept sine)
                float freq = Mathf.Lerp(160f, 85f, t / duration);
                float servo = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.35f * Mathf.Clamp01(t * 8f) * env;

                // Heavy sub thud at beginning
                float thud = Mathf.Sin(2f * Mathf.PI * 55f * t) * Mathf.Exp(-t * 9f) * 0.65f;

                samples[i] = Mathf.Clamp(noise + servo + thud, -1f, 1f);
            }

            _cachedProceduralOpenSound = AudioClip.Create("Procedural_Blackwall_Door_Open", totalSamples, 1, sampleRate, false);
            _cachedProceduralOpenSound.SetData(samples, 0);
            return _cachedProceduralOpenSound;
        }

        private static AudioClip GetOrCreateProceduralGlitchSound()
        {
            if (_cachedProceduralTransitionSound != null) return _cachedProceduralTransitionSound;

            int sampleRate = 44100;
            float duration = 0.85f;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Clamp01(1f - (t / duration));

                // Cybernetic glitch burst
                float digitalNoise = ((i % 16 < 8) ? 1f : -1f) * Random.value * 0.35f * Mathf.Exp(-t * 4f);
                float subDrop = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(220f, 40f, t / duration) * t) * 0.5f * env;

                samples[i] = Mathf.Clamp(digitalNoise + subDrop, -1f, 1f);
            }

            _cachedProceduralTransitionSound = AudioClip.Create("Procedural_Cyberspace_Glitch", totalSamples, 1, sampleRate, false);
            _cachedProceduralTransitionSound.SetData(samples, 0);
            return _cachedProceduralTransitionSound;
        }
        #endregion
    }
}
