using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CyberCombat.Audio;
using InsaneGun.Arena;

namespace Cyberpunk.Audio
{
    /// <summary>
    /// Master Audio Manager for the Underground Pit Combat Arena Crowd.
    /// Manages 6 pooled audio sources:
    /// 1. Bed: Continuous 2D looping gladiator ambient room murmur.
    /// 2. Cheer: 2D dynamic swell triggered by hits, combos, finishers, and round wins.
    /// 3. Clap: 2D rhythmic clapping loop during round breathers.
    /// 4. Sting: 2D boo groans and perfect dodge "OOOH!" gasps.
    /// 5 & 6. Spatial 1 & 2: 3D positional emitters centered over the cage at (0, 3, 0).
    /// </summary>
    public class PitCrowdAudioManager : MonoBehaviour
    {
        public static PitCrowdAudioManager Instance { get; private set; }

        [Header("Audio Sources (Drums, Crowd & Ambience)")]
        private AudioSource _drumSource;
        private AudioSource _bedSource;
        private AudioSource _cheerSource;
        private AudioSource _clapSource;
        private AudioSource _stingSource;
        private AudioSource _spatialSource1;
        private AudioSource _spatialSource2;

        [Header("New Imported Audio Clips")]
        public AudioClip drumsClip;
        public AudioClip crowdCheerClip;

        [Header("Tuning")]
        public float baseDrumVolume = 0.88f;
        public float baseBedVolume = 0.25f;
        public float baseCheerVolume = 0.48f;
        public float baseClapVolume = 0.55f;
        public float baseStingVolume = 0.60f;

        private float _lastCheerTime = -10f;
        private float _lastStingTime = -10f;
        private float _lastHypeTime = -10f;
        private Coroutine _clapFadeRoutine;

        private ArenaSpectatorNPC[] _cachedSpectators;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrapInArena()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;

            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (activeScene.name.IndexOf("Combat_Arena", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                activeScene.name.IndexOf("Indoor_Arena", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                EnsureInArena();
            }
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (scene.name.IndexOf("Combat_Arena", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                scene.name.IndexOf("Indoor_Arena", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                EnsureInArena();
            }
        }

        public static PitCrowdAudioManager EnsureInArena()
        {
            EnsureAudioListener();

            if (Instance != null) return Instance;
            var existing = Object.FindAnyObjectByType<PitCrowdAudioManager>();
            if (existing != null)
            {
                Instance = existing;
                return Instance;
            }

            GameObject go = new GameObject("[Pit_Crowd_Audio_Manager]");
            Instance = go.AddComponent<PitCrowdAudioManager>();
            return Instance;
        }

        private static void EnsureAudioListener()
        {
            var listener = Object.FindAnyObjectByType<AudioListener>();
            if (listener == null)
            {
                var cam = Camera.main ?? Object.FindAnyObjectByType<Camera>();
                if (cam != null)
                {
                    cam.gameObject.AddComponent<AudioListener>();
                    Debug.Log("[PitCrowdAudio] Added missing AudioListener to " + cam.gameObject.name);
                }
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            LoadClips();
            BuildAudioPool();
        }

        private void LoadClips()
        {
#if UNITY_EDITOR
            var spiderVerseClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/Across_The_Spider_Verse_Intro.mp3");
            if (spiderVerseClip != null)
            {
                drumsClip = spiderVerseClip;
            }
            else if (drumsClip == null)
            {
                drumsClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Crowd/tatamusic-battle-warrior-fighting-drums-478210.mp3");
            }

            if (crowdCheerClip == null)
                crowdCheerClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Crowd/u_xg7ssi08yr-crowd-cheering-379666.mp3");
#endif
            if (drumsClip == null)
                drumsClip = Resources.Load<AudioClip>("Audio/Music/Across_The_Spider_Verse_Intro");
            if (drumsClip == null)
                drumsClip = Resources.Load<AudioClip>("Audio/Crowd/tatamusic-battle-warrior-fighting-drums-478210");
            if (crowdCheerClip == null)
                crowdCheerClip = Resources.Load<AudioClip>("Audio/Crowd/u_xg7ssi08yr-crowd-cheering-379666");
        }

        private void Start()
        {
            _cachedSpectators = Object.FindObjectsByType<ArenaSpectatorNPC>(FindObjectsSortMode.None);

            // 1. Mute/Stop old metal music on Arena_Combat_Manager so Drums + Crowd Cheering are the star soundtrack!
            var allSources = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
            foreach (var src in allSources)
            {
                if (src == _drumSource || src == _bedSource || src == _cheerSource || src == _clapSource || src == _stingSource || src == _spatialSource1 || src == _spatialSource2)
                    continue;
                if (src.clip != null && src.clip.name.IndexOf("Metal_Heart", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    src.Stop();
                    src.volume = 0f;
                    src.mute = true;
                }
            }

            // 2. Play Battle Fighting Drums loop!
            if (_drumSource != null && drumsClip != null)
            {
                _drumSource.clip = drumsClip;
                _drumSource.volume = baseDrumVolume;
                _drumSource.loop = true;
                _drumSource.Play();
            }

            // 3. Play Crowd Cheering Bed loop!
            if (_bedSource != null)
            {
                _bedSource.clip = crowdCheerClip != null ? crowdCheerClip : CyberCombatAudioSynth.GetCrowdMurmurBed();
                _bedSource.volume = baseBedVolume;
                _bedSource.loop = true;
                _bedSource.Play();
            }

            // 4. Initial entrance hype roar!
            StartCoroutine(EntranceCheerRoutine());
        }

        private IEnumerator EntranceCheerRoutine()
        {
            yield return new WaitForSeconds(0.2f);
            PlayCheer(isEntrance: true);
            TriggerSpectatorHype(3.8f);
        }

        private void BuildAudioPool()
        {
            // 1. Drums (2D looping warrior battle drums)
            _drumSource = gameObject.AddComponent<AudioSource>();
            _drumSource.spatialBlend = 0.0f;
            _drumSource.loop = true;
            _drumSource.playOnAwake = false;

            // 2. Bed (2D looping crowd cheering ambient layer)
            _bedSource = gameObject.AddComponent<AudioSource>();
            _bedSource.spatialBlend = 0.0f;
            _bedSource.loop = true;
            _bedSource.playOnAwake = false;

            // 3. Cheer (2D swell for hits)
            _cheerSource = gameObject.AddComponent<AudioSource>();
            _cheerSource.spatialBlend = 0.0f;
            _cheerSource.loop = false;
            _cheerSource.playOnAwake = false;

            // 4. Clap (2D breather applause loop)
            _clapSource = gameObject.AddComponent<AudioSource>();
            _clapSource.spatialBlend = 0.0f;
            _clapSource.loop = true;
            _clapSource.playOnAwake = false;

            // 5. Sting (2D boo/gasp)
            _stingSource = gameObject.AddComponent<AudioSource>();
            _stingSource.spatialBlend = 0.0f;
            _stingSource.loop = false;
            _stingSource.playOnAwake = false;

            // 6 & 7. 3D Spatial sources over the ring at (0, 3, 0)
            GameObject spatialRoot = new GameObject("Pit_Spatial_Audio_Rig");
            spatialRoot.transform.SetParent(transform, false);
            spatialRoot.transform.position = new Vector3(0f, 3.0f, 0f);

            _spatialSource1 = spatialRoot.AddComponent<AudioSource>();
            _spatialSource1.spatialBlend = 0.85f;
            _spatialSource1.minDistance = 3.0f;
            _spatialSource1.maxDistance = 35.0f;
            _spatialSource1.rolloffMode = AudioRolloffMode.Linear;
            _spatialSource1.playOnAwake = false;

            _spatialSource2 = spatialRoot.AddComponent<AudioSource>();
            _spatialSource2.spatialBlend = 0.85f;
            _spatialSource2.minDistance = 3.0f;
            _spatialSource2.maxDistance = 35.0f;
            _spatialSource2.rolloffMode = AudioRolloffMode.Linear;
            _spatialSource2.playOnAwake = false;
        }

        /// <summary>
        /// Plays dynamic crowd cheer the SAME FRAME as the hit lands.
        /// TIGHT HIT-REACTION policy:
        /// - Per-hit stingers use the short fast-attack procedural cheer (1.3s,
        ///   25ms attack) so the transient lands on the blade impact, not a slow
        ///   mp3 swell. The imported mp3 stays as bed loop only.
        /// - Fixed pitch (no random 0.97-1.14 drift) so consecutive hits stay
        ///   rhythmically aligned instead of smearing.
        /// - Single 2D punch source; spatial reinforcement throttled to avoid
        ///   same-frame flam. Spectator hype is rate-limited (see below).
        /// Cooldown stays ultra-fast (0.12s) so every slash answers instantly.
        /// </summary>
        public void PlayCheer(bool isEntrance = false, bool isHeavyHit = false)
        {
            float now = Time.time;
            float minInterval = isEntrance ? 0f : (isHeavyHit ? 0.20f : 0.12f);
            if (!isEntrance && now - _lastCheerTime < minInterval) return;
            _lastCheerTime = now;

            float vol = isEntrance ? baseCheerVolume : (isHeavyHit ? baseCheerVolume * 0.85f : baseCheerVolume * 0.70f);
            // Entrance/round-win keeps the big imported swell; every combat hit
            // uses the punchy procedural stinger for instant transient sync.
            AudioClip clip = isEntrance
                ? ((crowdCheerClip != null) ? crowdCheerClip : CyberCombatAudioSynth.GetCrowdCheer(2))
                : CyberCombatAudioSynth.GetCrowdCheer(isHeavyHit ? 1 : 0);

            if (_cheerSource != null && clip != null)
            {
                _cheerSource.pitch = 1.0f;
                _cheerSource.PlayOneShot(clip, vol);
            }

            // Spatial reinforcement: heavy/entrance only, fixed pitch — per-hit
            // double-play of the same clip caused flam smear that read as "late".
            if (isEntrance || isHeavyHit)
            {
                if (_spatialSource1 != null && clip != null)
                {
                    _spatialSource1.pitch = 1.0f;
                    _spatialSource1.PlayOneShot(clip, vol * 0.50f);
                }
            }

            TriggerSpectatorHype(isEntrance ? 3.5f : (isHeavyHit ? 2.4f : 1.4f));
        }

        // Backward compatibility overload
        public void PlayCheer(float favor01, bool isEntrance = false, bool isHeavyHit = false)
        {
            PlayCheer(isEntrance, isHeavyHit);
        }

        public void PlayHitReaction(float damage)
        {
            bool isHeavy = (damage >= 45f);
            PlayCheer(isEntrance: false, isHeavyHit: isHeavy);
        }

        public void PlayHitReaction(float damage, float favor01)
        {
            PlayHitReaction(damage);
        }

        /// <summary>
        /// Starts rhythmic applause clapping during the 4-second breather between rounds.
        /// </summary>
        public void StartClap()
        {
            if (_clapSource == null) return;
            if (_clapFadeRoutine != null) StopCoroutine(_clapFadeRoutine);
            _clapFadeRoutine = StartCoroutine(FadeClapRoutine(true));
        }

        /// <summary>
        /// Instantly cuts drums, ambient murmur, and applause for dramatic blackout / boss arrival.
        /// </summary>
        public void CutDrumsAndAmbience()
        {
            if (_drumSource != null) _drumSource.Stop();
            if (_bedSource != null) _bedSource.Stop();
            if (_clapSource != null) _clapSource.Stop();
            if (_cheerSource != null) _cheerSource.Stop();
        }

        /// <summary>
        /// Resumes normal arena ambient murmur and battle drums.
        /// </summary>
        public void ResumeDrumsAndAmbience()
        {
            if (_drumSource != null && !_drumSource.isPlaying) _drumSource.Play();
            if (_bedSource != null && !_bedSource.isPlaying) _bedSource.Play();
        }

        /// <summary>
        /// Smoothly stops applause clapping when round begins.
        /// </summary>
        public void StopClap()
        {
            if (_clapSource == null) return;
            if (_clapFadeRoutine != null) StopCoroutine(_clapFadeRoutine);
            _clapFadeRoutine = StartCoroutine(FadeClapRoutine(false));
        }

        private IEnumerator FadeClapRoutine(bool fadeIn)
        {
            if (fadeIn)
            {
                _clapSource.clip = CyberCombatAudioSynth.GetCrowdClap();
                _clapSource.volume = 0f;
                _clapSource.loop = true;
                _clapSource.Play();

                float elapsed = 0f;
                float dur = 0.6f;
                while (elapsed < dur)
                {
                    elapsed += Time.unscaledDeltaTime;
                    _clapSource.volume = Mathf.Lerp(0f, baseClapVolume, elapsed / dur);
                    yield return null;
                }
            }
            else
            {
                float startVol = _clapSource.volume;
                float elapsed = 0f;
                float dur = 0.5f;
                while (elapsed < dur)
                {
                    elapsed += Time.unscaledDeltaTime;
                    _clapSource.volume = Mathf.Lerp(startVol, 0f, elapsed / dur);
                    yield return null;
                }
                _clapSource.Stop();
            }
        }

        /// <summary>
        /// Plays negative crowd booing when favor is very low or player turtles too long.
        /// </summary>
        public void PlayBoo()
        {
            float now = Time.time;
            if (now - _lastStingTime < 3.0f) return;
            _lastStingTime = now;

            AudioClip clip = CyberCombatAudioSynth.GetCrowdBoo();
            if (_stingSource != null && clip != null)
            {
                _stingSource.pitch = Random.Range(0.92f, 1.05f);
                _stingSource.PlayOneShot(clip, baseStingVolume);
            }
            if (_spatialSource2 != null && clip != null)
            {
                _spatialSource2.PlayOneShot(clip, baseStingVolume * 0.65f);
            }
        }

        /// <summary>
        /// Plays crowd gasp ("OOOH!") on close call dodges.
        /// </summary>
        public void PlayGasp()
        {
            float now = Time.time;
            if (now - _lastStingTime < 2.0f) return;
            _lastStingTime = now;

            AudioClip clip = CyberCombatAudioSynth.GetCrowdGasp();
            if (_stingSource != null && clip != null)
            {
                _stingSource.pitch = Random.Range(0.95f, 1.12f);
                _stingSource.PlayOneShot(clip, baseStingVolume * 0.90f);
            }
        }

        /// <summary>
        /// Synchronizes spectators with animation speed boosts. RATE-LIMITED:
        /// restarting 550 coroutines per hit hitched the main thread and pushed
        /// audio scheduling late. Per-hit hype now reuses running bursts and
        /// re-fires at most every 0.45s; audio itself plays instantly regardless.
        /// </summary>
        public void TriggerSpectatorHype(float boostDuration)
        {
            float now = Time.time;
            if (now - _lastHypeTime < 0.45f) return;
            _lastHypeTime = now;

            if (_cachedSpectators == null || _cachedSpectators.Length == 0)
            {
                _cachedSpectators = Object.FindObjectsByType<ArenaSpectatorNPC>(FindObjectsSortMode.None);
            }

            if (_cachedSpectators != null)
            {
                foreach (var spec in _cachedSpectators)
                {
                    if (spec != null) spec.CheerBurst(boostDuration);
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
