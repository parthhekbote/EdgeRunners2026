using UnityEngine;

namespace Cyberpunk.Audio
{
    /// <summary>
    /// Background music controller for Cyberpunk Demo City.
    /// Manages non-diegetic soundtrack playback with smooth fade-in, looping,
    /// priority handling to prevent voice-stealing, and runtime volume controls.
    /// </summary>
    [DisallowMultipleComponent]
    public class CyberBackgroundMusic : MonoBehaviour
    {
        private static CyberBackgroundMusic _instance;
        public static CyberBackgroundMusic Instance => _instance;

        [Header("Soundtrack Settings")]
        [Tooltip("The main background music audio clip.")]
        public AudioClip musicClip;

        [Range(0f, 1f)]
        [Tooltip("Target master volume for the background music.")]
        public float volume = 0.45f;

        [Tooltip("Duration in seconds for smooth volume fade-in on start.")]
        public float fadeInDuration = 2.5f;

        [Tooltip("Whether to loop playback indefinitely.")]
        public bool loop = true;

        [Tooltip("Whether to automatically begin playing when the scene starts.")]
        public bool playOnAwake = true;

        [Header("Runtime Controls")]
        [Tooltip("Press this key to toggle background music on/off.")]
        public KeyCode toggleMuteKey = KeyCode.M;

        private AudioSource _audioSource;
        private float _currentVolume = 0f;
        private bool _isFadingIn = true;
        private float _fadeTimer = 0f;
        private bool _isMuted = false;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            SetupAudioSource();
        }

        private void Start()
        {
            if (playOnAwake && _audioSource != null && musicClip != null)
            {
                PlayMusic();
            }
        }

        private void Update()
        {
            // Optional hotkey toggle for user convenience
            if (Input.GetKeyDown(toggleMuteKey))
            {
                ToggleMute();
            }

            if (_isFadingIn && !_isMuted && _audioSource != null && _audioSource.isPlaying)
            {
                _fadeTimer += Time.deltaTime;
                float progress = Mathf.Clamp01(_fadeTimer / Mathf.Max(0.1f, fadeInDuration));
                _currentVolume = Mathf.Lerp(0f, volume, progress);
                _audioSource.volume = _currentVolume;

                if (progress >= 1f)
                {
                    _isFadingIn = false;
                }
            }
            else if (!_isMuted && _audioSource != null && !_isFadingIn)
            {
                _audioSource.volume = volume;
            }
        }

        public void SetupAudioSource()
        {
            if (_audioSource == null)
            {
                _audioSource = GetComponent<AudioSource>();
                if (_audioSource == null)
                {
                    _audioSource = gameObject.AddComponent<AudioSource>();
                }
            }

            _audioSource.clip = musicClip;
            _audioSource.loop = loop;
            _audioSource.playOnAwake = false;
            // 2D non-diegetic sound
            _audioSource.spatialBlend = 0f;
            // Highest priority (0 = most important, never cut off by Unity sound limit)
            _audioSource.priority = 0;
            _audioSource.volume = 0f;
            _audioSource.bypassEffects = false;
            _audioSource.bypassListenerEffects = false;
            _audioSource.ignoreListenerPause = true;
        }

        public void PlayMusic()
        {
            if (_audioSource == null) SetupAudioSource();
            if (musicClip == null) return;

            _audioSource.clip = musicClip;
            _audioSource.time = 0f;
            _audioSource.Play();
            _isFadingIn = true;
            _fadeTimer = 0f;
            _audioSource.volume = 0f;
        }

        public void StopMusic()
        {
            if (_audioSource != null)
            {
                _audioSource.Stop();
            }
        }

        public void ToggleMute()
        {
            _isMuted = !_isMuted;
            if (_audioSource != null)
            {
                _audioSource.volume = _isMuted ? 0f : volume;
            }
        }

        public void SetVolume(float newVolume)
        {
            volume = Mathf.Clamp01(newVolume);
            if (!_isMuted && !_isFadingIn && _audioSource != null)
            {
                _audioSource.volume = volume;
            }
        }
    }
}
