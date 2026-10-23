using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using InsaneGun.Combat;

namespace SyntyCombined
{
    /// <summary>
    /// Cyberpunk 2077 "Blackwall" Audio-Reactive Rogue AI Barrier Controller.
    /// Recreates the iconic visual and acoustic experience of the Blackwall:
    /// - Audio-Reactive Shader Modulation: Real-time FFT spectrum analysis drives the wall's
    ///   breathing pulse, neural tendril writhing, and horizontal glitch tearing in direct sync
    ///   with the ghost attack audio.
    /// - Ghost Screech Glitch Spikes: High-frequency screaming audio peaks violently tear the
    ///   scanlines and spawn electrical sparks and lightning arcs.
    /// - Deep Net Bass Pulsing: Sub-bass droning surges illuminate the red beacon lights and
    ///   cause the deep red emission to throb.
    /// - Proximity Agitation: The closer the player gets, the more violent the Rogue AI reaction.
    /// - Violent Physical Repulsion & Fatal Ragdoll Physics upon contact.
    /// </summary>
    [ExecuteAlways]
    public class BlackwallRogueAIBarrier : MonoBehaviour
    {
        [Header("Blackwall Atmosphere & Audio-Reactive")]
        [ColorUsage(true, true)]
        public Color rogueAIColor = new Color(5.0f, 0.02f, 0.08f, 1.0f); // Pure HDR Crimson
        [ColorUsage(true, true)]
        public Color glitchFlashColor = new Color(6.5f, 0.35f, 0.5f, 1.0f);
        public float basePulseSpeed = 1.4f;

        [Header("Audio-Reactive Engine")]
        public AudioClip blackwallSoundLoop; // "cyberpunk-2077-blackwall-ghost-attack"
        public AudioClip shockAudio;
        public bool audioReactive = true;
        [Range(1f, 30f)] public float bassSensitivity = 12.0f;
        [Range(1f, 40f)] public float screechSensitivity = 22.0f;
        [Range(0f, 1f)]  public float proximityInfluence = 0.6f;

        [Header("Audio-Reactive 3D Z Displacement")]
        public bool enableZDisplacement = true;
        [Range(0.5f, 10.0f)] public float masterZScale = 1.8f;
        [Range(0.5f, 15.0f)] public float bassZScale = 6.0f;
        [Range(0.5f, 10.0f)] public float voiceZScale = 3.0f;
        [Range(0.5f, 20.0f)] public float screechZScale = 8.0f;

        [Header("Repulsion & Shock Physics")]
        public float repulsionForce = 22f;
        public float upwardRepulsion = 4.0f;
        public float shockDamage = 45f;
        public float contactCooldown = 0.45f;

        [Header("Particle Systems")]
        public ParticleSystem rogueAISparksVFX;
        public ParticleSystem rogueAILightningVFX;

        public static BlackwallRogueAIBarrier Instance { get; private set; }
        public AudioSource audioSource => _audioSource;

        private List<Renderer> _laserRenderers = new List<Renderer>();
        private List<Light> _beaconLights = new List<Light>();
        private MaterialPropertyBlock _propBlock;
        private AudioSource _audioSource;
        private AudioSource _shockAudioSource;
        private Transform _playerTransform;

        private readonly Dictionary<GameObject, float> _lastContactTime = new Dictionary<GameObject, float>();
        private float[] _spectrum = new float[64];
        private float _smoothBass = 0f;
        private float _smoothVoice = 0f;
        private float _smoothHigh = 0f;
        private float _glitchCooldown = 0f;

        private void Awake()
        {
            Instance = this;
            _propBlock = new MaterialPropertyBlock();
            
            // Primary AudioSource for continuous Blackwall sound loop
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }
            _audioSource.spatialBlend = 0.70f;
            _audioSource.playOnAwake = true;
            _audioSource.loop = true;
            _audioSource.minDistance = 6.0f;
            _audioSource.maxDistance = 65.0f;
            _audioSource.rolloffMode = AudioRolloffMode.Linear;
            if (_audioSource.volume < 0.25f)
            {
                _audioSource.volume = 0.85f;
            }

            // Secondary AudioSource for instant contact shock / screech spikes
            var shockGO = new GameObject("ShockAudioSource");
            shockGO.transform.SetParent(transform, false);
            _shockAudioSource = shockGO.AddComponent<AudioSource>();
            _shockAudioSource.spatialBlend = 0.85f;
            _shockAudioSource.playOnAwake = false;
            _shockAudioSource.maxDistance = 40f;

            CollectRenderersAndLights();
            FindPlayer();
        }

        private void Start()
        {
            if (_audioSource != null)
            {
                if (blackwallSoundLoop != null && _audioSource.clip == null)
                {
                    _audioSource.clip = blackwallSoundLoop;
                }
                if (!_audioSource.isPlaying && _audioSource.clip != null)
                {
                    _audioSource.loop = true;
                    _audioSource.Play();
                }
            }
        }

        public void CollectRenderersAndLights()
        {
            _laserRenderers.Clear();
            _beaconLights.Clear();

            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r.gameObject.name.Contains("Laser") || r.gameObject.name.Contains("Beam") || 
                    r.gameObject.name.Contains("Forcefield") || r.gameObject.name.Contains("Blackwall") || 
                    r.gameObject.name.Contains("Holo") || r.gameObject.name.Contains("Curtain"))
                {
                    _laserRenderers.Add(r);
                }
            }

            foreach (var l in GetComponentsInChildren<Light>(true))
            {
                if (l.gameObject.name.Contains("Beacon") || l.gameObject.name.Contains("Blackwall") || 
                    l.gameObject.name.Contains("Pillar"))
                {
                    _beaconLights.Add(l);
                }
            }
        }

        private void FindPlayer()
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null) _playerTransform = p.transform;
            else
            {
                var hero = GameObject.Find("Sidekick_Boy_Hero");
                if (hero != null) _playerTransform = hero.transform;
            }
        }

        private void Update()
        {
            if (_playerTransform == null) FindPlayer();

            // Freeze Blackwall to complete static state while the giant face is talking:
            bool isDialogueActive = BlackwallStoryDialogueHUD.Instance != null && BlackwallStoryDialogueHUD.Instance.isDialogueActive;
            if (isDialogueActive)
            {
                _smoothBass = 0f;
                _smoothVoice = 0f;
                _smoothHigh = 0f;
                if (_audioSource != null && _audioSource.isPlaying)
                {
                    _audioSource.volume = Mathf.MoveTowards(_audioSource.volume, 0f, Time.deltaTime * 3.0f);
                }
                ApplyShaderParameters(rogueAIColor * 0.90f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, staticMode: 1.0f);
                for (int i = 0; i < _beaconLights.Count; i++)
                {
                    if (_beaconLights[i] != null) _beaconLights[i].intensity = 2.5f;
                }
                return;
            }

            // Self-healing: if audio stopped or was unassigned, ensure loop playback continues for FFT
            if (_audioSource != null)
            {
                if (!_audioSource.isPlaying && blackwallSoundLoop != null)
                {
                    if (_audioSource.clip == null) _audioSource.clip = blackwallSoundLoop;
                    _audioSource.loop = true;
                    _audioSource.Play();
                }
            }

            // 1. Audio-Reactive Spectrum Analysis
            float bassEnergy = 0f;
            float voiceEnergy = 0f;
            float screechEnergy = 0f;

            if (audioReactive && _audioSource != null && _audioSource.isPlaying)
            {
                _audioSource.GetSpectrumData(_spectrum, 0, FFTWindow.BlackmanHarris);

                // Sub-bass & low frequencies (bands 0-3: ~20Hz to ~180Hz)
                for (int i = 0; i <= 3; i++)
                {
                    bassEnergy += _spectrum[i];
                }

                // Mid voice & vocal formant frequencies (bands 4-13: ~200Hz to ~1500Hz)
                for (int i = 4; i <= 13; i++)
                {
                    voiceEnergy += _spectrum[i];
                }

                // High frequencies / ghost screeches & digital noise (bands 14-45: ~1500Hz to ~9000Hz)
                for (int i = 14; i <= 45; i++)
                {
                    screechEnergy += _spectrum[i];
                }

                // Volume normalization compensation:
                // Prevents FFT spectrum response from collapsing when ambient track is ducked for combat
                float effectiveVol = Mathf.Clamp(_audioSource.volume, 0.08f, 1.0f);
                bassEnergy /= effectiveVol;
                voiceEnergy /= effectiveVol;
                screechEnergy /= effectiveVol;
            }

            // Procedural neural fallback: guarantees the Blackwall never goes inert even if audio is muted
            if (bassEnergy < 0.001f && screechEnergy < 0.001f)
            {
                float t = Time.time;
                bassEnergy = (Mathf.Sin(t * 3.8f) * 0.5f + 0.5f) * (Mathf.Sin(t * 1.3f) * 0.4f + 0.6f) * 0.22f;
                voiceEnergy = (Mathf.Cos(t * 6.5f) * 0.5f + 0.5f) * 0.12f;
                if (Mathf.Sin(t * 7.5f) > 0.72f)
                {
                    screechEnergy = (Mathf.Sin(t * 22.0f) * 0.5f + 0.5f) * 0.35f;
                }
            }

            // Smooth audio signals to prevent visual strobe instability
            float dt = Time.deltaTime > 0f ? Time.deltaTime : 0.016f;
            _smoothBass = Mathf.Lerp(_smoothBass, bassEnergy, dt * 12.0f);
            _smoothVoice = Mathf.Lerp(_smoothVoice, voiceEnergy, dt * 14.0f);
            _smoothHigh = Mathf.Lerp(_smoothHigh, screechEnergy, dt * 18.0f);

            float bassBoost = Mathf.Clamp01(_smoothBass * bassSensitivity);
            float screechSpike = Mathf.Clamp01(_smoothHigh * screechSensitivity);

            // 2. 3D Z-Displacement Calculations
            float bassZ = enableZDisplacement ? Mathf.Clamp(_smoothBass * bassZScale, 0f, 6.0f) : 0f;
            float voiceZ = enableZDisplacement ? Mathf.Clamp(_smoothVoice * voiceZScale, 0f, 3.5f) : 0f;
            float screechZ = enableZDisplacement ? Mathf.Clamp(_smoothHigh * screechZScale, 0f, 7.5f) : 0f;

            // 3. Proximity Factor (Player closeness to this wall)
            float proximityFactor = 0f;
            if (_playerTransform != null)
            {
                float distToWall = Mathf.Abs(Vector3.Dot(transform.forward, _playerTransform.position - transform.position));
                proximityFactor = Mathf.Clamp01(1.0f - (distToWall / 20.0f)) * proximityInfluence;
            }

            // 4. Audio-Reactive Breathing Pulse & Emission
            float baseWave = Mathf.Sin(Time.time * basePulseSpeed) * 0.25f + 0.75f;
            float totalEnergy = baseWave + (bassBoost * 1.4f) + (proximityFactor * 0.5f);
            Color activeEmission = rogueAIColor * totalEnergy;

            // 5. Audio-Reactive Glitch Tearing & Phase Jump
            float glitchIntensity = 0.35f + (screechSpike * 1.4f) + (proximityFactor * 0.35f);
            float glitchPhase = 0f;

            if (screechSpike > 0.38f && Time.time >= _glitchCooldown)
            {
                // Audio ghost screech peak: force horizontal matrix tearing!
                glitchPhase = Random.value;
                _glitchCooldown = Time.time + Random.Range(0.04f, 0.12f);

                // High screech burst: emit electric sparks & digital embers
                if (screechSpike > 0.65f && rogueAISparksVFX != null)
                {
                    rogueAISparksVFX.Emit(Mathf.RoundToInt(screechSpike * 6));
                }
            }

            // 6. Tendril writhing power modulated by sub-bass
            float tendrilPower = 2.2f + (bassBoost * 3.5f) + (proximityFactor * 1.2f);

            // Apply all audio reactive & 3D Z-displacement parameters to wall renderers
            ApplyShaderParameters(activeEmission, glitchIntensity, glitchPhase, tendrilPower, bassBoost, screechSpike, bassZ, voiceZ, screechZ);

            // 7. Pulse Ground Beacons with Sub-Bass
            float lightPulse = 3.5f + (bassBoost * 7.0f) + (screechSpike * 3.0f);
            for (int i = 0; i < _beaconLights.Count; i++)
            {
                if (_beaconLights[i] != null)
                {
                    _beaconLights[i].intensity = lightPulse;
                }
            }
        }

        private void ApplyShaderParameters(Color emission, float glitchIntensity, float glitchPhase, float tendrilIntensity, float bassEnergy = 0f, float screechEnergy = 0f, float bassZ = 0f, float voiceZ = 0f, float screechZ = 0f, float staticMode = 0f)
        {
            if (_laserRenderers.Count == 0) return;

            for (int i = 0; i < _laserRenderers.Count; i++)
            {
                var r = _laserRenderers[i];
                if (r == null) continue;

                if (_propBlock == null) _propBlock = new MaterialPropertyBlock();
                r.GetPropertyBlock(_propBlock);
                _propBlock.SetColor("_EmissionColor", emission);
                _propBlock.SetFloat("_GlitchIntensity", glitchIntensity);
                _propBlock.SetFloat("_GlitchPhase", glitchPhase);
                _propBlock.SetFloat("_TendrilIntensity", tendrilIntensity);
                _propBlock.SetFloat("_BassEnergy", bassEnergy);
                _propBlock.SetFloat("_ScreechEnergy", screechEnergy);
                _propBlock.SetFloat("_AudioZDisplacement", staticMode > 0.5f ? 0f : masterZScale);
                _propBlock.SetFloat("_BassZPush", bassZ);
                _propBlock.SetFloat("_VoiceZWave", voiceZ);
                _propBlock.SetFloat("_ScreechZSpike", screechZ);
                _propBlock.SetFloat("_StaticMode", staticMode);
                r.SetPropertyBlock(_propBlock);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            HandleContact(other);
        }

        private void OnTriggerStay(Collider other)
        {
            HandleContact(other);
        }

        private void OnCollisionEnter(Collision collision)
        {
            HandleContact(collision.collider);
        }

        private void HandleContact(Collider other)
        {
            if (other == null) return;

            // Rogue AI entities originating from the Blackwall are immune to barrier shock
            if (other.GetComponentInParent<InsaneGun.Combat.HolographicBlackwallEmergence>() != null || 
                other.name.Contains("RogueAI") || 
                (other.transform.parent != null && other.transform.parent.name.Contains("RogueAI")))
            {
                return;
            }
            {
                if (!other.CompareTag("Player") && !other.CompareTag("Enemy")) return;
            }

            GameObject targetGO = other.attachedRigidbody != null
                ? other.attachedRigidbody.gameObject
                : other.gameObject;

            var swordCtrl  = targetGO.GetComponentInParent<InsaneGunSwordController>();
            var enemyAI    = targetGO.GetComponentInParent<SwordEnemyAI>();
            var dummy      = targetGO.GetComponentInParent<SyntyTrainingDummy>();
            var damageable = targetGO.GetComponentInParent<ISyntyDamageable>();

            GameObject rootGO = swordCtrl != null ? swordCtrl.gameObject :
                               (enemyAI != null ? enemyAI.gameObject :
                               (dummy != null ? dummy.gameObject : targetGO));

            if (_lastContactTime.TryGetValue(rootGO, out float lastTime))
            {
                if (Time.time - lastTime < contactCooldown) return;
            }
            _lastContactTime[rootGO] = Time.time;

            Vector3 contactPos = other.ClosestPoint(transform.position);
            Vector3 pushDir = -transform.forward;
            pushDir.y = 0f;
            if (pushDir.sqrMagnitude < 0.01f)
            {
                pushDir = (Vector3.zero - rootGO.transform.position).normalized;
                pushDir.y = 0f;
            }
            pushDir.Normalize();

            Vector3 launchVelocity = (pushDir + Vector3.up * (upwardRepulsion / repulsionForce)).normalized * repulsionForce;

            // Trigger Shock VFX & Audio
            TriggerShockVFX(contactPos, pushDir);

            // Apply Shock Damage
            bool fatalShock = false;
            if (swordCtrl != null)
            {
                swordCtrl.TakeDamage(shockDamage, contactPos, pushDir, isHeavy: true);
                if (swordCtrl.IsDead) fatalShock = true;
            }
            else if (enemyAI != null)
            {
                enemyAI.TakeDamage(shockDamage, contactPos, pushDir, isHeavy: true);
                if (enemyAI.currentHealth <= 0f) fatalShock = true;
            }
            else if (dummy != null)
            {
                dummy.TakeDamage(shockDamage, contactPos, pushDir, isHeavy: true);
                if (dummy.currentHealth <= 0f) fatalShock = true;
            }
            else if (damageable != null)
            {
                damageable.TakeDamage(shockDamage, contactPos, pushDir, isHeavy: true);
            }

            // Fatal Euphoria Ragdoll Blast
            var ragdoll = rootGO.GetComponent<EuphoriaRagdollController>() ?? rootGO.GetComponentInChildren<EuphoriaRagdollController>();
            if (ragdoll != null && (ragdoll.IsRagdoll || fatalShock))
            {
                if (!ragdoll.IsRagdoll)
                {
                    ragdoll.TriggerEuphoriaRagdoll(contactPos, launchVelocity, repulsionForce, isFatal: true);
                }
                else
                {
                    var rbs = rootGO.GetComponentsInChildren<Rigidbody>();
                    foreach (var rb in rbs)
                    {
                        if (rb != null && !rb.isKinematic)
                        {
                            rb.linearVelocity = launchVelocity;
                            rb.AddTorque(Random.insideUnitSphere * 18f, ForceMode.Impulse);
                        }
                    }
                }
                return;
            }

            // Living character controller push
            var cc = rootGO.GetComponent<CharacterController>() ?? rootGO.GetComponentInChildren<CharacterController>();
            if (cc != null && cc.enabled)
            {
                StartCoroutine(CharacterControllerRepulsionRoutine(cc, pushDir, repulsionForce, 0.35f));
            }
            else if (other.attachedRigidbody != null && !other.attachedRigidbody.isKinematic)
            {
                other.attachedRigidbody.linearVelocity = launchVelocity;
            }
        }

        private void TriggerShockVFX(Vector3 point, Vector3 normal)
        {
            if (rogueAISparksVFX != null)
            {
                rogueAISparksVFX.transform.position = point;
                rogueAISparksVFX.transform.rotation = Quaternion.LookRotation(normal);
                rogueAISparksVFX.Emit(45);
            }

            if (rogueAILightningVFX != null)
            {
                rogueAILightningVFX.transform.position = point;
                rogueAILightningVFX.Emit(16);
            }

            AudioClip clipToPlay = shockAudio != null ? shockAudio : blackwallSoundLoop;
            if (_shockAudioSource != null && clipToPlay != null)
            {
                _shockAudioSource.pitch = Random.Range(0.9f, 1.25f);
                _shockAudioSource.PlayOneShot(clipToPlay, 1.0f);
            }

            // Blinding Glitch Overdrive Flash
            StartCoroutine(FlashWallEmission(glitchFlashColor * 4.5f, 0.15f));

            // Camera shake
            SmoothOrbitCamera.ShakeImpact(1.1f, -normal * 0.35f);
        }

        private IEnumerator FlashWallEmission(Color color, float duration)
        {
            ApplyShaderParameters(color, 2.0f, Random.value, 6.0f);
            yield return new WaitForSeconds(duration);
            ApplyShaderParameters(rogueAIColor, 0.6f, 0.0f, 2.2f);
        }

        private IEnumerator CharacterControllerRepulsionRoutine(CharacterController cc, Vector3 direction, float initialSpeed, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (cc == null || !cc.enabled) yield break;
                float currentSpeed = Mathf.Lerp(initialSpeed, 0f, elapsed / duration);
                cc.Move(direction * (currentSpeed * Time.deltaTime));
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
    }
}
