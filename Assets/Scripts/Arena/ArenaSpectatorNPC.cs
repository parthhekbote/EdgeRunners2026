using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace InsaneGun.Arena
{
    /// <summary>
    /// Controls a spectator NPC outside the combat arena cage.
    /// Handles randomized cheering/hype animation playback, cycle desynchronization,
    /// dynamic look-at tracking toward the central duelists, and crowd audio chatter.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class ArenaSpectatorNPC : MonoBehaviour
    {
        public enum SpectatorCheerType
        {
            DoubleWaveCheer,
            FistPumpCheer,
            SmallWaveCheer,
            AggressiveHype,
            VideoRecording,
            DrinkWatching,
            RailingLean
        }

        [Header("Spectator Persona")]
        public SpectatorCheerType cheerType = SpectatorCheerType.DoubleWaveCheer;
        [Range(0.80f, 1.25f)] public float playbackSpeed = 1.0f;
        public bool lookAtArenaCenter = true;

        [Header("Target Tracking")]
        public Transform fightFocusTarget;

        [Header("Audio Murmurs")]
        public AudioClip[] chatterClips;
        [Range(0.1f, 1.0f)] public float chatterVolume = 0.45f;
        public float minChatterInterval = 8.0f;
        public float maxChatterInterval = 22.0f;

        private Animator _animator;
        private AudioSource _audioSource;
        private Vector3 _lookCenter = new Vector3(0f, 0.86f, 0f);
        private float _baseSpeed = 1.0f;
        private Coroutine _cheerCoroutine;
        private float _hypeEndTime = -10f;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            if (_animator != null)
            {
                // Desynchronize playback speed so crowd doesn't animate like robots
                _baseSpeed = Random.Range(0.88f, 1.18f);
                playbackSpeed = _baseSpeed;
                _animator.speed = _baseSpeed;
            }

            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null && chatterClips != null && chatterClips.Length > 0)
            {
                // Only 1 in 8 spectators needs a dedicated 3D spatial audio emitter to avoid voice limit exhaustion
                if (Random.value < 0.14f)
                {
                    _audioSource = gameObject.AddComponent<AudioSource>();
                    _audioSource.spatialBlend = 1.0f; // 3D spatial sound
                    _audioSource.minDistance = 3.0f;
                    _audioSource.maxDistance = 22.0f;
                    _audioSource.rolloffMode = AudioRolloffMode.Linear;
                    _audioSource.playOnAwake = false;
                }
            }
        }

        private void Start()
        {
            if (_animator != null)
            {
                // Random normalized time start offset so every NPC is at a different point in the loop
                float randomOffset = Random.Range(0.0f, 1.0f);
                _animator.Play(0, 0, randomOffset);
            }

            if (chatterClips != null && chatterClips.Length > 0 && _audioSource != null)
            {
                StartCoroutine(RandomChatterRoutine());
            }
        }

        private void Update()
        {
            if (lookAtArenaCenter)
            {
                Vector3 targetPos = (fightFocusTarget != null) ? fightFocusTarget.position : _lookCenter;
                Vector3 dir = targetPos - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.05f)
                {
                    Quaternion desiredRot = Quaternion.LookRotation(dir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, Time.deltaTime * 3.5f);
                }
            }
        }

        public void SetArenaFocus(Vector3 center, Transform dynamicTarget = null)
        {
            _lookCenter = center;
            fightFocusTarget = dynamicTarget;
        }

        private IEnumerator RandomChatterRoutine()
        {
            // Initial random delay
            yield return new WaitForSeconds(Random.Range(1.5f, 8.0f));

            while (true)
            {
                float wait = Random.Range(minChatterInterval, maxChatterInterval);
                yield return new WaitForSeconds(wait);

                if (_audioSource != null && chatterClips != null && chatterClips.Length > 0)
                {
                    AudioClip clip = chatterClips[Random.Range(0, chatterClips.Length)];
                    if (clip != null && !_audioSource.isPlaying)
                    {
                        _audioSource.pitch = Random.Range(0.92f, 1.08f);
                        _audioSource.PlayOneShot(clip, chatterVolume);
                    }
                }
            }
        }

        /// <summary>
        /// Trigger a burst of excited cheering when a heavy impact / hit lands in the ring.
        /// CHEAP PATH: if already hyped, just extend the timer — no Stop/Start
        /// churn, so 550 spectators don't hitch the hit frame and delay audio.
        /// </summary>
        public void CheerBurst(float boostDuration = 2.0f)
        {
            if (_animator == null || !gameObject.activeInHierarchy) return;
            float now = Time.time;
            if (_cheerCoroutine != null && now < _hypeEndTime)
            {
                _hypeEndTime = now + boostDuration;
                return;
            }
            _hypeEndTime = now + boostDuration;
            if (_cheerCoroutine != null) StopCoroutine(_cheerCoroutine);
            _cheerCoroutine = StartCoroutine(CheerBurstRoutine(boostDuration));
        }

        private IEnumerator CheerBurstRoutine(float duration)
        {
            _animator.speed = _baseSpeed * Random.Range(1.35f, 1.65f);
            while (Time.time < _hypeEndTime)
                yield return null;
            if (_animator != null) _animator.speed = _baseSpeed;
            _cheerCoroutine = null;
        }
    }
}
