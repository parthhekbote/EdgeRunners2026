using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Cyberpunk.Missions
{
    /// <summary>
    /// Master Audio Manager for Mission 1:
    /// - Intense Mission BGM with fade-in on accept, ducking during Ido popups, and fade-out on completion/death.
    /// - High-fidelity pooled combat SFX (light slash whoosh, metallic blade clashes, enemy grunts, player hurt thud).
    /// - Futuristic UI audio (hologram comms ring, Dr. Ido voice line, terminal typewriter clicks, glitch whoosh).
    /// </summary>
    public class Mission1_AudioManager : MonoBehaviour
    {
        public static Mission1_AudioManager Instance { get; private set; }

        [Header("Audio Clips (Auto-Loaded)")]
        public AudioClip bgmClip;
        public AudioClip bgmFallbackClip;
        [Header("Mission 2 Chase BGM — Robbery Heist Chase Music (ikoliks_aj)")]
        [Tooltip("File: Assets/Resources/Audio/ikoliks_aj-robbery-heist-chase-music-434840.mp3")]
        public AudioClip chaseBGMClip;
        public AudioClip idoVoiceNotice;
        public AudioClip idoVoiceBriefing;
        public AudioClip idoVoiceComplete;
        public AudioClip phoneChatterClip;
        public AudioClip[] mercGruntClips;

        [Header("Audio Sources")]
        private AudioSource _bgmSource;
        private List<AudioSource> _sfxPool = new List<AudioSource>();
        private const int PoolSize = 6;
        private int _poolIndex = 0;

        // Procedural Audio Clips
        private AudioClip _phoneRingtoneClip;
        private AudioClip _slashWhooshClip;
        private AudioClip _metalImpactClip;
        private AudioClip _playerHurtClip;
        private AudioClip _deathStingClip;
        private AudioClip _xpKillBlipClip;
        private AudioClip _buttonHoverClip;
        private AudioClip _glitchWhooshClip;

        // BGM State
        private Coroutine _bgmFadeCoroutine;
        private bool _isMissionActive = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            LoadAudioAssets();
            GenerateProceduralSFX();
            SetupAudioSources();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            // Detect LMB attack swing while mission is running to play light slash whoosh
            if (_isMissionActive && Input.GetMouseButtonDown(0))
            {
                // Only play if not in a UI popup or paused
                if (Mission1_UIManager.Instance == null || !Mission1_UIManager.Instance.IsPopupOpen())
                {
                    PlaySlashWhoosh();
                }
            }
        }

        private void LoadAudioAssets()
        {
            // 1. Try Resources load first (standalone build + runtime compatibility)
            var resBgm = Resources.Load<AudioClip>("Audio/paulyudin-intense-intense-music-574004");
            if (resBgm != null)
            {
                bgmClip = resBgm;
            }

            // M2 chase theme: Robbery Heist Chase Music by ikoliks_aj (in-Assets drop-in).
            var resChase = Resources.Load<AudioClip>("Audio/ikoliks_aj-robbery-heist-chase-music-434840");
            if (resChase != null)
            {
                chaseBGMClip = resChase;
            }

#if UNITY_EDITOR
            if (chaseBGMClip == null)
            {
                chaseBGMClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/Audio/ikoliks_aj-robbery-heist-chase-music-434840.mp3");
            }
            if (bgmClip == null)
            {
                bgmClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/paulyudin-intense-intense-music-574004.mp3");
            }
            if (bgmClip == null)
            {
                bgmClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/Cyberpunk_Combat_Metal_Heart.mp3");
            }
            if (bgmClip == null)
            {
                bgmClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/Cyberpunk_Combat_Regime.mp3");
            }
            if (bgmClip == null)
            {
                bgmClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/Nine Inch Nails - Init.mp3");
            }
            bgmFallbackClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/NIN_Init_Full_0_to_120.wav");
            idoVoiceBriefing = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Voice/Ido/ido_mission1_briefing.mp3");
            idoVoiceComplete = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Voice/Ido/ido_mission1_complete.mp3");
            idoVoiceNotice = idoVoiceBriefing != null ? idoVoiceBriefing : UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Voice/Ido/ido_02_notice.wav");
            phoneChatterClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/NPC_Chatter/npc_phone_chatter_01.wav");

            var grunt1 = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/NPC_Chatter/npc_merc_grunt_01.wav");
            var grunt2 = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/NPC_Chatter/npc_merc_grunt_02.wav");
            if (grunt1 != null && grunt2 != null)
            {
                mercGruntClips = new AudioClip[] { grunt1, grunt2 };
            }
#endif
            if (bgmClip == null && bgmFallbackClip != null)
            {
                bgmClip = bgmFallbackClip;
            }
        }

        private void SetupAudioSources()
        {
            // 1. Dedicated BGM Source (Tuned for maximum clarity and presence)
            _bgmSource = gameObject.AddComponent<AudioSource>();
            _bgmSource.clip = bgmClip;
            _bgmSource.loop = true;
            _bgmSource.playOnAwake = false;
            _bgmSource.volume = 0f;
            _bgmSource.spatialBlend = 0f; // Pure 2D Stereo
            _bgmSource.priority = 0;      // Top hardware priority: never culled or stolen by ambient sources
            _bgmSource.bypassEffects = true;
            _bgmSource.bypassListenerEffects = true;
            _bgmSource.bypassReverbZones = true;
            _bgmSource.dopplerLevel = 0f;
            _bgmSource.spread = 0;

            // 2. SFX Pool (max 6 concurrent) - balanced to prevent drowning out BGM
            for (int i = 0; i < PoolSize; i++)
            {
                var sfxSource = gameObject.AddComponent<AudioSource>();
                sfxSource.playOnAwake = false;
                sfxSource.spatialBlend = 0f;
                sfxSource.priority = 64;
                sfxSource.volume = 0.60f;
                _sfxPool.Add(sfxSource);
            }
        }

        private AudioSource GetAvailableSFXSource()
        {
            var source = _sfxPool[_poolIndex];
            _poolIndex = (_poolIndex + 1) % PoolSize;
            source.pitch = 1.0f;
            return source;
        }

        // ─────────────────────────────────────────────────────────────────────
        // PROCEDURAL AUDIO SYNTHESIZERS
        // ─────────────────────────────────────────────────────────────────────
        private void GenerateProceduralSFX()
        {
            const int sampleRate = 44100;

            // 1. Dual-Tone Ringtone (880Hz + 1320Hz double-beep, 0.55s)
            {
                int samples = (int)(sampleRate * 0.55f);
                float[] buffer = new float[samples];
                for (int i = 0; i < samples; i++)
                {
                    float t = (float)i / sampleRate;
                    // Two 0.15s beeps separated by 0.08s silence
                    float env = 0f;
                    if (t >= 0.02f && t < 0.18f) env = Mathf.Sin((t - 0.02f) / 0.16f * Mathf.PI);
                    else if (t >= 0.26f && t < 0.42f) env = Mathf.Sin((t - 0.26f) / 0.16f * Mathf.PI);

                    float f1 = Mathf.Sin(2f * Mathf.PI * 880f * t);
                    float f2 = Mathf.Sin(2f * Mathf.PI * 1320f * t);
                    buffer[i] = (f1 * 0.6f + f2 * 0.4f) * env * 0.35f;
                }
                _phoneRingtoneClip = AudioClip.Create("PhoneDoubleBeep", samples, 1, sampleRate, false);
                _phoneRingtoneClip.SetData(buffer, 0);
            }

            // 2. Light Slash Whoosh (0.12s aerodynamic noise burst)
            {
                int samples = (int)(sampleRate * 0.12f);
                float[] buffer = new float[samples];
                for (int i = 0; i < samples; i++)
                {
                    float t = (float)i / samples;
                    float freq = Mathf.Lerp(620f, 180f, t);
                    float env = Mathf.Sin(t * Mathf.PI);
                    float noise = (Random.value * 2f - 1f) * 0.55f;
                    float tone = Mathf.Sin(2f * Mathf.PI * freq * ((float)i / sampleRate)) * 0.45f;
                    buffer[i] = (noise + tone) * env * 0.4f;
                }
                _slashWhooshClip = AudioClip.Create("LightSlashWhoosh", samples, 1, sampleRate, false);
                _slashWhooshClip.SetData(buffer, 0);
            }

            // 3. Metallic Impact Clang (180Hz square decay + high ring)
            {
                int samples = (int)(sampleRate * 0.22f);
                float[] buffer = new float[samples];
                for (int i = 0; i < samples; i++)
                {
                    float t = (float)i / sampleRate;
                    float decay = Mathf.Exp(-t * 22f);
                    float sq = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 180f * t)) * 0.45f;
                    float ring = Mathf.Sin(2f * Mathf.PI * 2240f * t) * 0.35f * Mathf.Exp(-t * 14f);
                    float noise = (Random.value * 2f - 1f) * 0.2f * Mathf.Exp(-t * 40f);
                    buffer[i] = (sq + ring + noise) * decay * 0.55f;
                }
                _metalImpactClip = AudioClip.Create("MetalImpactClang", samples, 1, sampleRate, false);
                _metalImpactClip.SetData(buffer, 0);
            }

            // 4. Player Hurt Sub-Bass Thud (80Hz thump)
            {
                int samples = (int)(sampleRate * 0.18f);
                float[] buffer = new float[samples];
                for (int i = 0; i < samples; i++)
                {
                    float t = (float)i / sampleRate;
                    float freq = Mathf.Lerp(90f, 35f, (float)i / samples);
                    float decay = Mathf.Exp(-t * 16f);
                    buffer[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * decay * 0.65f;
                }
                _playerHurtClip = AudioClip.Create("PlayerHurtThud", samples, 1, sampleRate, false);
                _playerHurtClip.SetData(buffer, 0);
            }

            // 5. Kill Death Sting (440Hz -> 110Hz pitch bend down)
            {
                int samples = (int)(sampleRate * 0.35f);
                float[] buffer = new float[samples];
                for (int i = 0; i < samples; i++)
                {
                    float progress = (float)i / samples;
                    float freq = Mathf.Lerp(440f, 110f, progress * progress);
                    float env = (1f - progress) * Mathf.Sin(Mathf.Clamp01(progress * 12f) * Mathf.PI * 0.5f);
                    buffer[i] = Mathf.Sin(2f * Mathf.PI * freq * ((float)i / sampleRate)) * env * 0.5f;
                }
                _deathStingClip = AudioClip.Create("DeathSting", samples, 1, sampleRate, false);
                _deathStingClip.SetData(buffer, 0);
            }

            // 6. Kill XP Blip (1760Hz -> 2640Hz chime)
            {
                int samples = (int)(sampleRate * 0.20f);
                float[] buffer = new float[samples];
                for (int i = 0; i < samples; i++)
                {
                    float t = (float)i / sampleRate;
                    float chime = Mathf.Sin(2f * Mathf.PI * 1760f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 2640f * t) * 0.5f;
                    buffer[i] = chime * Mathf.Exp(-t * 18f) * 0.45f;
                }
                _xpKillBlipClip = AudioClip.Create("XPKillBlip", samples, 1, sampleRate, false);
                _xpKillBlipClip.SetData(buffer, 0);
            }

            // 7. Button Hover Tick
            {
                int samples = (int)(sampleRate * 0.03f);
                float[] buffer = new float[samples];
                for (int i = 0; i < samples; i++)
                {
                    float t = (float)i / sampleRate;
                    buffer[i] = Mathf.Sin(2f * Mathf.PI * 1200f * t) * (1f - (float)i / samples) * 0.18f;
                }
                _buttonHoverClip = AudioClip.Create("ButtonHoverTick", samples, 1, sampleRate, false);
                _buttonHoverClip.SetData(buffer, 0);
            }

            // 8. Glitch Whoosh (0.28s filtered noise sweep)
            {
                int samples = (int)(sampleRate * 0.28f);
                float[] buffer = new float[samples];
                for (int i = 0; i < samples; i++)
                {
                    float t = (float)i / samples;
                    float noise = (Random.value * 2f - 1f) * Mathf.Sin(t * Mathf.PI);
                    buffer[i] = noise * 0.35f;
                }
                _glitchWhooshClip = AudioClip.Create("GlitchWhoosh", samples, 1, sampleRate, false);
                _glitchWhooshClip.SetData(buffer, 0);
            }
        }

        public AudioSource BgmSource
        {
            get
            {
                if (_bgmSource == null)
                {
                    var sources = GetComponents<AudioSource>();
                    foreach (var s in sources)
                    {
                        if (s != null && s.clip != null && (s.clip == bgmClip || s.clip == bgmFallbackClip || s.clip.name.Contains("paulyudin")))
                        {
                            _bgmSource = s;
                            break;
                        }
                    }
                    if (_bgmSource == null && sources.Length > 0)
                    {
                        _bgmSource = sources[0];
                    }
                    if (_bgmSource == null)
                    {
                        _bgmSource = gameObject.AddComponent<AudioSource>();
                        _bgmSource.playOnAwake = false;
                        _bgmSource.loop = true;
                    }
                }
                return _bgmSource;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // BGM CONTROLS
        // ─────────────────────────────────────────────────────────────────────
        public void StartMissionBGM(AudioClip customClip = null, bool force = false)
        {
            // CRITICAL GUARD: Battle music must ONLY play if the trial is actively in bridge combat!
            // (force bypasses for scripted sequences like the M2 chase.)
            if (!force && Mission1_StreetTrialManager.Instance != null && 
                Mission1_StreetTrialManager.Instance.state != Mission1_StreetTrialManager.MissionState.CombatInArena)
            {
                Debug.LogWarning($"[Mission1_Audio] Refusing to start combat BGM while mission state is '{Mission1_StreetTrialManager.Instance.state}' (must be CombatInArena).");
                return;
            }

            _isMissionActive = true;
            var src = BgmSource;
            if (src == null) return;

            if (customClip != null)
            {
                src.clip = customClip;
            }
            else if (bgmClip != null)
            {
                src.clip = bgmClip;
            }
            else if (src.clip == null)
            {
                src.clip = bgmFallbackClip;
            }
            if (src.clip == null) return;

            // Enforce clear stereo settings
            src.spatialBlend = 0f;
            src.priority = 0;
            src.bypassEffects = true;
            src.bypassListenerEffects = true;
            src.bypassReverbZones = true;

            if (!src.isPlaying)
            {
                src.time = 0f;
                src.volume = 0f;
                src.Play();
            }

            if (_bgmFadeCoroutine != null) StopCoroutine(_bgmFadeCoroutine);
            _bgmFadeCoroutine = StartCoroutine(FadeBGMRoutine(0.92f, 1.0f));
        }

        public void StartChaseBGM()
        {
            // Mission 2 spook: crossfade whatever is playing into the 136 BPM chase
            // theme (looped 2D stereo, duck-compatible). Falls back to mission BGM
            // when the licensed file is not in the project yet.
            if (chaseBGMClip == null)
            {
                Debug.LogWarning("[Mission1_Audio] Heist chase file missing (Assets/Resources/Audio/ikoliks_aj-robbery-heist-chase-music-434840.mp3); using mission BGM.");
                StartMissionBGM(null, true);
                return;
            }
            _isMissionActive = true;
            var src = BgmSource;
            if (src == null) return;
            if (_bgmFadeCoroutine != null) StopCoroutine(_bgmFadeCoroutine);
            _bgmFadeCoroutine = StartCoroutine(SwapChaseBGMRoutine(src, chaseBGMClip));
        }

        private System.Collections.IEnumerator SwapChaseBGMRoutine(AudioSource src, AudioClip chase)
        {
            // 0.6s fade-out of the outgoing track, hard swap, 1.5s fade-in.
            float startVol = src.isPlaying ? src.volume : 0f;
            float t = 0f;
            while (t < 0.6f && src.isPlaying)
            {
                t += Time.unscaledDeltaTime;
                src.volume = Mathf.Lerp(startVol, 0f, t / 0.6f);
                yield return null;
            }
            src.Stop();
            src.clip = chase;
            src.loop = true;
            src.spatialBlend = 0f;
            src.priority = 0;
            src.bypassEffects = true;
            src.bypassListenerEffects = true;
            src.bypassReverbZones = true;
            src.time = 0f;
            src.volume = 0f;
            src.Play();
            _bgmFadeCoroutine = StartCoroutine(FadeBGMRoutine(0.92f, 1.5f));
        }

        public void StopMissionBGM(float fadeTime = 2.0f)
        {
            _isMissionActive = false;
            var src = BgmSource;
            if (src == null) return;

            if (_bgmFadeCoroutine != null) StopCoroutine(_bgmFadeCoroutine);
            _bgmFadeCoroutine = StartCoroutine(FadeAndStopBGMRoutine(fadeTime));
        }

        public void StopMissionBGMImmediate()
        {
            _isMissionActive = false;
            if (_bgmFadeCoroutine != null)
            {
                StopCoroutine(_bgmFadeCoroutine);
                _bgmFadeCoroutine = null;
            }

            var src = BgmSource;
            if (src != null)
            {
                src.Stop();
                src.time = 0f;
                src.volume = 0f;
            }

            // Universal sweep: silence ANY AudioSource attached to this manager or scene playing the battle track
            var allSources = GetComponentsInChildren<AudioSource>(true);
            foreach (var s in allSources)
            {
                if (s != null && s.clip != null && (s.clip == bgmClip || s.clip == bgmFallbackClip || s.clip == chaseBGMClip || s.clip.name.Contains("ikoliks") || s.clip.name.Contains("heist") || s.clip.name.Contains("chase")))
                {
                    s.Stop();
                    s.time = 0f;
                    s.volume = 0f;
                }
            }
        }

        public void DuckBGM(bool duck)
        {
            var src = BgmSource;
            if (src == null || !src.isPlaying) return;

            float target = duck ? 0.32f : (_isMissionActive ? 0.92f : 0f);
            if (_bgmFadeCoroutine != null) StopCoroutine(_bgmFadeCoroutine);
            _bgmFadeCoroutine = StartCoroutine(FadeBGMRoutine(target, 0.35f));
        }

        private IEnumerator FadeBGMRoutine(float targetVol, float duration)
        {
            var src = BgmSource;
            if (src == null) yield break;

            float startVol = src.volume;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                if (src == null) yield break;
                src.volume = Mathf.Lerp(startVol, targetVol, elapsed / duration);
                yield return null;
            }
            if (src != null) src.volume = targetVol;
            _bgmFadeCoroutine = null;
        }

        private IEnumerator FadeAndStopBGMRoutine(float duration)
        {
            var src = BgmSource;
            if (src == null) yield break;

            float startVol = src.volume;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                if (src == null) yield break;
                src.volume = Mathf.Lerp(startVol, 0f, elapsed / duration);
                yield return null;
            }
            if (src != null)
            {
                src.volume = 0f;
                src.Stop();
            }
            _bgmFadeCoroutine = null;
        }

        // ─────────────────────────────────────────────────────────────────────
        // COMBAT SFX API
        // ─────────────────────────────────────────────────────────────────────
        public void PlaySlashWhoosh()
        {
            var src = GetAvailableSFXSource();
            src.pitch = Random.Range(0.95f, 1.10f);
            src.PlayOneShot(_slashWhooshClip, 0.45f);
        }

        public void PlayHit(bool wasPlayerHit)
        {
            var src = GetAvailableSFXSource();

            if (wasPlayerHit)
            {
                // Enemy hit player: sub-bass thump
                src.pitch = Random.Range(0.9f, 1.05f);
                src.PlayOneShot(_playerHurtClip, 0.75f);
            }
            else
            {
                // Player hit enemy: metallic clang + merc grunt
                src.pitch = Random.Range(0.92f, 1.08f);
                src.PlayOneShot(_metalImpactClip, 0.70f);

                if (mercGruntClips != null && mercGruntClips.Length > 0)
                {
                    var gruntSrc = GetAvailableSFXSource();
                    gruntSrc.pitch = Random.Range(0.90f, 1.10f);
                    var clip = mercGruntClips[Random.Range(0, mercGruntClips.Length)];
                    gruntSrc.PlayOneShot(clip, 0.65f);
                }
            }
        }

        public void PlayKill()
        {
            var src = GetAvailableSFXSource();
            src.pitch = 1.0f;
            src.PlayOneShot(_deathStingClip, 0.65f);

            var chimeSrc = GetAvailableSFXSource();
            chimeSrc.pitch = 1.0f;
            chimeSrc.PlayOneShot(_xpKillBlipClip, 0.60f);
        }

        // ─────────────────────────────────────────────────────────────────────
        // UI SFX API
        // ─────────────────────────────────────────────────────────────────────
        public void PlayUICall()
        {
            // Sci-fi holo-connection ring
            if (_phoneRingtoneClip != null)
            {
                var rSrc = GetAvailableSFXSource();
                rSrc.pitch = 1.0f;
                rSrc.PlayOneShot(_phoneRingtoneClip, 0.50f);
            }

            // Dr. Ido clear cinematic briefing voice
            var clip = idoVoiceBriefing != null ? idoVoiceBriefing : idoVoiceNotice;
            if (clip != null)
            {
                StartCoroutine(DelayedVoiceRoutine(clip, 0.35f));
            }
        }

        public void PlayRingOnly()
        {
            if (_phoneRingtoneClip != null)
            {
                var rSrc = GetAvailableSFXSource();
                rSrc.pitch = 1.0f;
                rSrc.PlayOneShot(_phoneRingtoneClip, 0.50f);
            }
        }

        public void PlayRingAndVoice(AudioClip voice)
        {
            PlayRingOnly();
            if (voice != null)
            {
                StartCoroutine(DelayedVoiceRoutine(voice, 0.35f));
            }
        }

        public void PlayUICompleteCall()
        {
            if (_phoneRingtoneClip != null)
            {
                var rSrc = GetAvailableSFXSource();
                rSrc.pitch = 1.0f;
                rSrc.PlayOneShot(_phoneRingtoneClip, 0.50f);
            }

            if (idoVoiceComplete != null)
            {
                StartCoroutine(DelayedVoiceRoutine(idoVoiceComplete, 0.35f));
            }
        }

        private IEnumerator DelayedVoiceRoutine(AudioClip clip, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            var vSrc = GetAvailableSFXSource();
            vSrc.pitch = 1.0f;
            vSrc.PlayOneShot(clip, 1.0f);
        }

        public void PlayUIConfirm()
        {
            var src = GetAvailableSFXSource();
            src.pitch = 1.0f;
            src.PlayOneShot(_glitchWhooshClip, 0.45f);
        }

        public void PlayButtonHover()
        {
            var src = GetAvailableSFXSource();
            src.pitch = 1.0f;
            src.PlayOneShot(_buttonHoverClip, 0.35f);
        }

        public void PlayGlitchWhoosh()
        {
            var src = GetAvailableSFXSource();
            src.pitch = Random.Range(0.95f, 1.05f);
            src.PlayOneShot(_glitchWhooshClip, 0.40f);
        }
    }
}
