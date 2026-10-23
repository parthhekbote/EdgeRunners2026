using UnityEngine;

namespace Tron.Terminal
{
    /// <summary>
    /// Audio engine for the TRON Terminal.
    /// Manages background music (Nine Inch Nails - Init), ambient cyberspace drone hum,
    /// and procedural synthesized audio clicks, chirps, and terminal beeps.
    /// </summary>
    public class TronTerminalAudioEngine : MonoBehaviour
    {
        [Header("Audio Clips")]
        public AudioClip musicClip;
        public AudioClip ambientDroneClip;

        [Header("Audio Sources")]
        public AudioSource musicSource;
        public AudioSource ambientSource;
        public AudioSource sfxSource;

        [Header("Volume Controls")]
        [Range(0f, 1f)] public float musicVolume = 0.75f;
        [Range(0f, 1f)] public float ambientVolume = 0.35f;
        [Range(0f, 1f)] public float sfxVolume = 0.65f;

        // Procedural audio clips generated at runtime
        private AudioClip _keyClickClip;
        private AudioClip _enterChimeClip;
        private AudioClip _errorBeepClip;
        private AudioClip _digitizeClip;

        private void Awake()
        {
            SetupAudioSources();
            GenerateProceduralAudioClips();
        }

        private void Start()
        {
            if (musicSource != null && musicClip != null)
            {
                musicSource.clip = musicClip;
                musicSource.loop = true;
                musicSource.volume = musicVolume;
                musicSource.Play();
            }

            if (ambientSource != null && ambientDroneClip != null)
            {
                ambientSource.clip = ambientDroneClip;
                ambientSource.loop = true;
                ambientSource.volume = ambientVolume;
                ambientSource.Play();
            }
        }

        private void SetupAudioSources()
        {
            if (musicSource == null)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
                musicSource.playOnAwake = false;
                musicSource.spatialBlend = 0f; // 2D Stereo
            }

            if (ambientSource == null)
            {
                ambientSource = gameObject.AddComponent<AudioSource>();
                ambientSource.playOnAwake = false;
                ambientSource.spatialBlend = 0f;
            }

            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
                sfxSource.playOnAwake = false;
                sfxSource.spatialBlend = 0f;
            }
        }

        private void GenerateProceduralAudioClips()
        {
            _keyClickClip = CreateProceduralChirp("KeyClick", 0.022f, 1800f, 900f, 0.4f);
            _enterChimeClip = CreateProceduralChirp("EnterChime", 0.09f, 880f, 1760f, 0.5f);
            _errorBeepClip = CreateProceduralSquareWave("ErrorBeep", 0.12f, 220f, 0.45f);
            _digitizeClip = CreateProceduralNoiseSweep("DigitizeSweep", 0.65f, 0.6f);
        }

        private AudioClip CreateProceduralChirp(string name, float duration, float freqStart, float freqEnd, float maxGain)
        {
            int sampleRate = 44100;
            int samples = Mathf.CeilToInt(duration * sampleRate);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float currentFreq = Mathf.Lerp(freqStart, freqEnd, t);
                float envelope = Mathf.Sin(t * Mathf.PI); // Smooth bell curve
                data[i] = Mathf.Sin(2f * Mathf.PI * currentFreq * (i / (float)sampleRate)) * envelope * maxGain;
            }

            AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateProceduralSquareWave(string name, float duration, float freq, float maxGain)
        {
            int sampleRate = 44100;
            int samples = Mathf.CeilToInt(duration * sampleRate);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float phase = Mathf.Repeat(t * duration * freq, 1.0f);
                float sample = phase < 0.5f ? 1.0f : -1.0f;
                float envelope = 1.0f - t;
                data[i] = sample * envelope * maxGain;
            }

            AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateProceduralNoiseSweep(string name, float duration, float maxGain)
        {
            int sampleRate = 44100;
            int samples = Mathf.CeilToInt(duration * sampleRate);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float noise = (Random.value * 2f - 1f);
                float tone = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(300f, 3200f, t * t) * (i / (float)sampleRate));
                float envelope = Mathf.Sin(t * Mathf.PI);
                data[i] = (noise * 0.4f + tone * 0.6f) * envelope * maxGain;
            }

            AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public void PlayKeyClick(float pitchVariance = 0.1f)
        {
            if (sfxSource == null || _keyClickClip == null) return;
            sfxSource.pitch = 1.0f + Random.Range(-pitchVariance, pitchVariance);
            sfxSource.PlayOneShot(_keyClickClip, sfxVolume * 0.6f);
        }

        public void PlayEnterChime()
        {
            if (sfxSource == null || _enterChimeClip == null) return;
            sfxSource.pitch = 1.0f;
            sfxSource.PlayOneShot(_enterChimeClip, sfxVolume);
        }

        public void PlayErrorBeep()
        {
            if (sfxSource == null || _errorBeepClip == null) return;
            sfxSource.pitch = 1.0f;
            sfxSource.PlayOneShot(_errorBeepClip, sfxVolume * 0.8f);
        }

        public void PlayDigitizeSweep()
        {
            if (sfxSource == null || _digitizeClip == null) return;
            sfxSource.pitch = 1.0f;
            sfxSource.PlayOneShot(_digitizeClip, sfxVolume * 0.9f);
        }

        public void ToggleMusic()
        {
            if (musicSource == null) return;
            if (musicSource.isPlaying) musicSource.Pause();
            else musicSource.Play();
        }

        public void SetMusicVolume(float vol)
        {
            musicVolume = Mathf.Clamp01(vol);
            if (musicSource != null) musicSource.volume = musicVolume;
        }
    }
}
