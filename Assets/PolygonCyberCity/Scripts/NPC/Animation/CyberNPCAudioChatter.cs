using System.Collections.Generic;
using UnityEngine;
using PolygonCyberCity.NPC.Spawner;
using PolygonCyberCity.NPC.BT;

namespace PolygonCyberCity.NPC.Animation
{
    /// <summary>
    /// Immersive 3D Spatial Audio Chatter System for Cyber City NPCs.
    /// Manages contextual urban vocalizations:
    /// - Phone calls & comms chatter
    /// - Conversational murmurs and dialogue responses
    /// - HelperBot synthetic telemetry chirps and servo sweeps
    /// - Vehicle evasion startle gasps
    /// - Mercenary tactical radio barks
    /// Includes a global proximity voice coordinator to prevent audio cacophony.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class CyberNPCAudioChatter : MonoBehaviour
    {
        [Header("Audio Clips")]
        public AudioClip[] phoneChatterClips;
        public AudioClip[] civilianMurmurClips;
        public AudioClip[] robotChirpClips;
        public AudioClip[] startleReactionClips;
        public AudioClip[] mercGruntClips;

        [Header("Tuning")]
        [Tooltip("Completely muted by default to eliminate unwanted vocal buzz/fart noises.")]
        public bool enableNPCVocalNoises = false;
        [Range(0f, 1f)] public float chatterVolume = 0f;
        public float minChatterInterval = 12.0f;
        public float maxChatterInterval = 24.0f;

        private AudioSource _audioSource;
        private CyberNPCController _controller;
        private float _chatterTimer = 0f;
        private float _nextChatterTime = 8.0f;
        private Transform _playerTransform;

        // Global voice concurrency control
        private const int MaxConcurrentNearbySpeakers = 4;
        private const float HearableDistance = 14.0f;
        private static readonly List<CyberNPCAudioChatter> CurrentlySpeaking = new List<CyberNPCAudioChatter>();

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            _controller = GetComponent<CyberNPCController>();
            ConfigureAudioSource();
            LoadAudioClipsIfMissing();
        }

        private void Start()
        {
            var playerGo = GameObject.FindWithTag("Player");
            if (playerGo != null)
            {
                _playerTransform = playerGo.transform;
            }
            _nextChatterTime = Random.Range(3.0f, maxChatterInterval);
        }

        private void OnDisable()
        {
            if (CurrentlySpeaking.Contains(this))
            {
                CurrentlySpeaking.Remove(this);
            }
        }

        private void ConfigureAudioSource()
        {
            if (_audioSource == null) return;

            _audioSource.spatialBlend = 1.0f; // Pure 3D spatialized
            _audioSource.dopplerLevel = 0.2f;
            _audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            _audioSource.minDistance = 1.5f;
            _audioSource.maxDistance = HearableDistance;
            _audioSource.playOnAwake = false;
            _audioSource.loop = false;
        }

        public void LoadAudioClipsIfMissing()
        {
#if UNITY_EDITOR
            if (phoneChatterClips == null || phoneChatterClips.Length == 0)
            {
                phoneChatterClips = LoadClips("Assets/Audio/NPC_Chatter/npc_phone_chatter");
                civilianMurmurClips = LoadClips("Assets/Audio/NPC_Chatter/npc_civilian_murmur");
                robotChirpClips = LoadClips("Assets/Audio/NPC_Chatter/npc_helperbot_chirp");
                startleReactionClips = LoadClips("Assets/Audio/NPC_Chatter/npc_reaction_startle");
                mercGruntClips = LoadClips("Assets/Audio/NPC_Chatter/npc_merc_grunt");
            }
#endif
        }

#if UNITY_EDITOR
        private static AudioClip[] LoadClips(string prefix)
        {
            List<AudioClip> clips = new List<AudioClip>();
            for (int i = 1; i <= 5; i++)
            {
                string path = $"{prefix}_{i:D2}.wav";
                var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null) clips.Add(clip);
            }
            return clips.ToArray();
        }
#endif

        private void Update()
        {
            if (!enableNPCVocalNoises) return;
            if (_controller == null || _controller.IsDead) return;

            // Remove from speaking list if done playing
            if (CurrentlySpeaking.Contains(this) && (!_audioSource.isPlaying || _audioSource.clip == null))
            {
                CurrentlySpeaking.Remove(this);
            }

            // Periodic ambient chatter
            _chatterTimer += Time.deltaTime;
            if (_chatterTimer >= _nextChatterTime)
            {
                _chatterTimer = 0f;
                _nextChatterTime = Random.Range(minChatterInterval, maxChatterInterval);

                if (_controller.Blackboard != null && _controller.Blackboard.CurrentState == NPCMacroState.Normal)
                {
                    PlayAmbientChatterForArchetype();
                }
            }
        }

        private bool CanSpeakNearby()
        {
            if (_playerTransform == null)
            {
                var playerGo = GameObject.FindWithTag("Player");
                if (playerGo != null) _playerTransform = playerGo.transform;
            }

            if (_playerTransform != null)
            {
                float dist = Vector3.Distance(transform.position, _playerTransform.position);
                if (dist > HearableDistance) return false;
            }

            // Clean stale references
            CurrentlySpeaking.RemoveAll(s => s == null || s._audioSource == null || !s._audioSource.isPlaying);

            return CurrentlySpeaking.Count < MaxConcurrentNearbySpeakers;
        }

        public void PlayPhoneChatter()
        {
            PlayRandomFromList(phoneChatterClips, volumeMul: 0.75f, pitchVariance: 0.08f);
        }

        public void PlayConversationalMurmur()
        {
            PlayRandomFromList(civilianMurmurClips, volumeMul: 0.70f, pitchVariance: 0.12f);
        }

        public void PlayRobotTelemetry()
        {
            PlayRandomFromList(robotChirpClips, volumeMul: 0.85f, pitchVariance: 0.15f);
        }

        public void PlayStartledReaction()
        {
            // Startled reaction bypasses normal cooldown for dynamic responsiveness
            PlayRandomFromList(startleReactionClips, volumeMul: 1.0f, pitchVariance: 0.10f, force: true);
        }

        public void PlayMercComms()
        {
            PlayRandomFromList(mercGruntClips, volumeMul: 0.80f, pitchVariance: 0.06f);
        }

        public void PlayAmbientChatterForArchetype()
        {
            if (_controller == null) return;

            switch (_controller.npcType)
            {
                case NPCType.HelperBot:
                    PlayRobotTelemetry();
                    break;
                case NPCType.Mercenary:
                case NPCType.StreetThug:
                    PlayMercComms();
                    break;
                case NPCType.Civilian:
                case NPCType.Cyborg:
                case NPCType.RichCitizen:
                default:
                    // If in phone idle state, play phone chatter
                    if (_controller.animator != null && _controller.animator.GetInteger(NPCAnimatorHashes.RandomIdle) == 2)
                    {
                        PlayPhoneChatter();
                    }
                    else
                    {
                        PlayConversationalMurmur();
                    }
                    break;
            }
        }

        private void PlayRandomFromList(AudioClip[] clips, float volumeMul = 1.0f, float pitchVariance = 0.1f, bool force = false)
        {
            if (!enableNPCVocalNoises || chatterVolume <= 0.001f) return;
            if (clips == null || clips.Length == 0 || _audioSource == null) return;
            if (!force && !CanSpeakNearby()) return;
            if (_audioSource.isPlaying && !force) return;

            AudioClip clip = clips[Random.Range(0, clips.Length)];
            if (clip == null) return;

            _audioSource.pitch = 1.0f + Random.Range(-pitchVariance, pitchVariance);
            _audioSource.PlayOneShot(clip, chatterVolume * volumeMul);

            if (!CurrentlySpeaking.Contains(this))
            {
                CurrentlySpeaking.Add(this);
            }
        }
    }
}
