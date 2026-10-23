using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CyberCombat.Audio;
using CyberCombat.Coordination;

namespace InsaneGun.Combat
{
    /// <summary>
    /// AAA Character Combat Controller – v3.0 (Combat Redesign)
    ///
    /// BUGS FIXED:
    ///   1. Self-damage: enemyLayers auto-corrected to exclude player layer.
    ///   2. Spam attack: attacks require minAttackInterval between triggers.
    ///   3. Animation desync: combo timing driven by animator normalizedTime,
    ///      not hardcoded WaitForSeconds timers.
    ///
    /// NEW FEATURES:
    ///   • Combo window system: press attack in the green window to chain.
    ///   • Gun burst fire: gunBurstCount shots with recoil per shot.
    ///   • Bullet hole decals on gun hit surfaces.
    ///   • InterruptAttack() cleanly cancels all coroutines on dodge/parry.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Animator))]
    public class InsaneGunSwordController : MonoBehaviour, SyntyCombined.ISyntyDamageable, IGroupCombatant
    {
        public enum ControlMode { PlayerControlled, AICirclingEnemy, TrainingDummy }

        [Header("Character Control Mode")]
        public ControlMode controlMode = ControlMode.PlayerControlled;
        public Transform targetEnemy;

        // ── IGroupCombatant Group Coordination ──────────────────────────────
        private bool _hasAttackToken = false;
        Transform IGroupCombatant.CombatantTransform => transform;
        Transform IGroupCombatant.CombatTarget => targetEnemy;
        bool IGroupCombatant.IsCombatActive => controlMode == ControlMode.AICirclingEnemy && !_isDead && isActiveAndEnabled;
        bool IGroupCombatant.IsCurrentlyAttacking => _isAttacking || _aiComboRunning;
        void IGroupCombatant.OnAttackTokenGranted() { _hasAttackToken = true; }
        void IGroupCombatant.OnAttackTokenRevoked() { _hasAttackToken = false; }

        [Header("Movement Settings")]
        public float walkSpeed     = 2.4f;
        public float runSpeed      = 6.2f;
        public float rotationSpeed = 720f;
        public float dodgeDistance = 6.0f; // High-velocity AAA Cyberpunk dash
        public float dodgeDuration = 0.30f;
        public float gravity       = 22f;

        [Header("Ground Detection")]
        public LayerMask groundLayers    = ~0;
        public float groundCheckRadius   = 0.15f;

        [Header("Combat & Stats")]
        public float maxHealth         = 250f;
        public float currentHealth     = 250f;
        public float attackDamage      = 35f;
        public float heavyAttackDamage = 75f;
        public bool  isInvincible      = false;
        public bool  inputLocked       = false;
        // M1 trial protection: when set, TakeDamage on an invincible victim that the
        // hook flags as trial-protected plays a whiff spark + throttled "HOLD" toast
        // instead of damage. Wired by Mission1_StreetTrialManager at boot; null = off.
        public static System.Func<InsaneGunSwordController, bool> IsTrialProtected;
        private static float _trialToastTime = -10f;
        [Tooltip("MUST NOT include the Player layer. Set to only Enemy/NPC layers.")]
        public LayerMask enemyLayers;

        [Header("AI Advanced Combat & Poise")]
        [Tooltip("Default Max Health for AI Enemies")]
        public float aiMaxHealth             = 100f;
        [Tooltip("Maximum Poise for AI enemies before breaking into hit stagger / Euphoria ragdoll")]
        public float maxPoise                = 50f;
        public float currentPoise            = 50f;
        public float poiseRecoveryRate       = 20f;
        public float poiseBrokenDuration     = 1.5f;
        [Tooltip("When true, character absorbs attacks without flinching/interrupting (Super Armor)")]
        public bool  hasHyperArmor           = false;
        [Range(0f, 1f)] public float aiBlockChance         = 0.10f;
        [Range(0f, 1f)] public float aiParryChance         = 0.0f;
        [Range(0f, 1f)] public float aiDodgeReactionChance = 0.15f;
        [Range(0f, 1f)] public float aiBlockMitigation     = 0.40f;

        public enum AIGunSwordComboType
        {
            None,
            ExecutionerShred,       // Sword 1 -> Sword 2 -> Point-Blank Gun Burst!
            StalkerLunge,           // Suppressive Gun Fire -> High-Speed Dash -> Heavy Piercing Thrust!
            CycloneCrossfire,       // Heavy 360 Whirlwind Spin -> Evasive Roll -> Gun Burst!
            ParryRiposte,           // Immediate Deflect Counter-Slash + Point-Blank Gun Blast!
            BlitzkriegOverdrive     // Relentless 4-Hit String: Light 1 -> Light 2 -> Heavy Spin -> Gun Finisher!
        }
        public AIGunSwordComboType activeAICombo = AIGunSwordComboType.None;
        private float _lastPoiseDamageTime   = 0f;
        private bool  _isPoiseBroken         = false;
        private Coroutine _activeAIComboRoutine;

        public System.Action<InsaneGunSwordController> OnDeath;
        public System.Action<InsaneGunSwordController, float> OnDamaged;

        [Header("Combo System")]
        [Tooltip("Normalized time at which the next combo hit can be queued.")]
        [Range(0.15f, 0.85f)] public float comboAcceptWindowStart = 0.22f;
        [Range(0.55f, 1.0f)]  public float comboAcceptWindowEnd   = 0.85f;
        [Tooltip("Min seconds between consecutive attack triggers (anti-spam).")]
        public float minAttackInterval = 0.18f;
        [Tooltip("Seconds idle before combo resets.")]
        public float comboResetDelay   = 1.35f;

        [Header("Slow Motion / Bullet Time")]
        public bool    enableSlowMotion        = true;
        public KeyCode slowMotionKey           = KeyCode.T;
        public float   slowMotionTimeScale     = 0.25f;
        public float   maxSlowMotionEnergy     = 100f;
        public float   currentSlowMotionEnergy = 100f;
        public float   slowMotionDrainRate     = 25f;
        public float   slowMotionRechargeRate  = 18f;

        [Header("Lock-On & Defense")]
        public KeyCode   lockOnKey     = KeyCode.Tab;
        public KeyCode   blockKey      = KeyCode.F;
        public Transform lockedTarget  = null;
        public bool      enableLockOn  = true;

        [Header("Custom Animation Action (M Key)")]
        public KeyCode customAnimationKey = KeyCode.M;
        public string customAnimationStateName = "X_Bot_M_Action";
        private Coroutine _customAnimationRoutine;

        [Header("Weapon Sockets & Visuals")]
        public Transform      rightHandSocket;
        public Transform      leftHandSocket;
        public GameObject     swordPrefab;
        public GameObject     gunPrefab;
        public TrailRenderer  swordTrail;
        public ParticleSystem gunMuzzleVFX;

        [Header("Sword Holster & Sheathe")]
        public Transform      hipSocket;
        public Transform      swordInstance;
        public bool           isSwordDrawn            = true;
        public KeyCode        drawSheatheKey          = KeyCode.Z;
        public KeyCode        drawSheatheAltKey       = KeyCode.None;
        public Vector3        swordHandLocalPos       = new Vector3(0.11780f, 0.03410f, -0.00920f);
        public Vector3        swordHandLocalRot       = new Vector3(341.39560f, 99.66290f, 276.94530f);
        public Vector3        swordHipLocalPos        = Vector3.zero;
        public Vector3        swordHipLocalRot        = Vector3.zero;

        [Header("Jump Physics")]
        public KeyCode        jumpKey                 = KeyCode.Space;
        public float          jumpForce               = 8.5f;
        [Tooltip("Downward gravity multiplier when falling to prevent floaty/drifty jumps")]
        public float          fallGravityMultiplier   = 1.6f;
        [Tooltip("Minimum cooldown in seconds between jumps to prevent spam")]
        public float          jumpCooldown            = 0.22f;
        [Tooltip("Grace period in seconds after walking off a ledge where jump is still allowed")]
        public float          coyoteTimeDuration      = 0.12f;

        [Header("Gun Combat")]
        [Range(1,6)] public int   gunBurstCount   = 3;
        public float gunBurstInterval = 0.08f;
        public float gunDamagePerShot = 28f;
        public float gunRange         = 30f;
        public float gunRecoilPitch   = 1.8f;

        [Header("Audio")]
        public AudioSource audioSource;
        public AudioClip[] swordSwingSounds;
        public AudioClip[] swordImpactSounds;
        public AudioClip[] gunShotSounds;
        public AudioClip[] dodgeSounds;
        public AudioClip[] dashSounds;
        public AudioClip[] jumpSounds;

        // ── Private State ───────────────────────────────────────────────────
        private CharacterController _cc;
        private Animator            _animator;
        private Camera              _mainCamera;
        private Vector3  _moveDirection     = Vector3.zero;
        private Vector3  _knockbackVelocity = Vector3.zero;
        private float    _verticalVelocity  = 0f;
        private Vector3  _initialSpawnPosition;
        private Quaternion _initialSpawnRotation = Quaternion.identity;
        public Vector3 InitialSpawnPosition => _initialSpawnPosition;
        public Quaternion InitialSpawnRotation => _initialSpawnRotation;
        private float    _staggerEndTime    = 0f;
        private bool     _isGrounded        = false;
        private bool     _isAttacking       = false;
        private bool     _isDodging         = false;
        private bool     _isDead            = false;
        private bool     _isBlocking        = false;
        private bool     _isSlowMotionActive = false;
        private bool     _isJumping         = false;
        private float    _jumpCooldownTimer = 0f;
        private float    _coyoteTimer       = 0f;
        private float    _airborneTime      = 0f;
        private static readonly RaycastHit[] _groundHitsBuffer = new RaycastHit[10];
        private bool     _isDrawingOrSheathing = false;
        private bool     _targetSwordDrawn     = true;
        private int      _comboIndex        = 0;
        private int      _heavyComboIndex   = 0;
        private bool     _comboWindowOpen   = false;
        private bool     _comboQueued       = false;
        private float    _lastAttackTime    = -99f;
        private float    _blockStartTime    = 0f;

        private Coroutine _lightAttackRoutine;
        private Coroutine _heavyAttackRoutine;
        private Coroutine _gunAttackRoutine;
        private Coroutine _drawSheatheRoutine;
        private Renderer[] _renderers;
        private HolographicGhostEmitter _ghostEmitter;
        private MaterialPropertyBlock   _propBlock;
        private Coroutine               _flashCoroutine;
        private ParticleSystem          _swordSparkles;

        private readonly HashSet<int> _animParamHashes = new HashSet<int>();

        public void CacheAnimatorParameters()
        {
            _animParamHashes.Clear();
            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                int count = _animator.parameterCount;
                for (int i = 0; i < count; i++)
                {
                    _animParamHashes.Add(_animator.GetParameter(i).nameHash);
                }
            }
        }

        private void SafeSetBool(int hash, bool val)
        {
            if (_animator != null && _animParamHashes.Contains(hash)) _animator.SetBool(hash, val);
        }

        private void SafeSetInteger(int hash, int val)
        {
            if (_animator != null && _animParamHashes.Contains(hash)) _animator.SetInteger(hash, val);
        }

        private void SafeSetTrigger(int hash)
        {
            if (_animator != null && _animParamHashes.Contains(hash)) _animator.SetTrigger(hash);
        }

        private void SafeResetTrigger(int hash)
        {
            if (_animator != null && _animParamHashes.Contains(hash)) _animator.ResetTrigger(hash);
        }

        private void SafeSetFloat(int hash, float val)
        {
            if (_animator != null && _animParamHashes.Contains(hash)) _animator.SetFloat(hash, val);
        }

        private void SafeSetFloat(int hash, float val, float dampTime, float deltaTime)
        {
            if (_animator != null && _animParamHashes.Contains(hash)) _animator.SetFloat(hash, val, dampTime, deltaTime);
        }

        private void SafeCrossFade(string stateName, float transitionDuration, int layer = 0, float time = 0f)
        {
            if (_animator != null && _animator.HasState(layer, Animator.StringToHash(stateName)))
            {
                _animator.CrossFadeInFixedTime(stateName, transitionDuration, layer, time);
            }
        }

        private void SetSwordTrailEmitting(bool active)
        {
            if (swordInstance != null)
            {
                var trails = swordInstance.GetComponentsInChildren<TrailRenderer>(true);
                foreach (var t in trails)
                {
                    t.emitting = active;
                }
            }
            else if (swordTrail != null)
            {
                swordTrail.emitting = active;
            }

            if (_swordSparkles == null && swordInstance != null)
                _swordSparkles = swordInstance.GetComponentInChildren<ParticleSystem>(true);
            if (_swordSparkles != null)
            {
                var em = _swordSparkles.emission;
                em.enabled = active;
            }
        }

        // ── Animator Hashes ─────────────────────────────────────────────────
        private static readonly int HashMoveSpeed         = Animator.StringToHash("MoveSpeed");
        private static readonly int HashAttackTrigger     = Animator.StringToHash("Attack");
        private static readonly int HashAttackIndex       = Animator.StringToHash("AttackIndex");
        private static readonly int HashHeavyComboTrigger = Animator.StringToHash("HeavyCombo");
        private static readonly int HashHeavyIndex        = Animator.StringToHash("HeavyIndex");
        private static readonly int HashGunAttackTrigger  = Animator.StringToHash("GunAttack");
        private static readonly int HashGunIndex          = Animator.StringToHash("GunIndex");
        private static readonly int HashDodgeTrigger      = Animator.StringToHash("Dodge");
        private static readonly int HashDodgeDir          = Animator.StringToHash("DodgeDir");
        private static readonly int HashHitTrigger        = Animator.StringToHash("Hit");
        private static readonly int HashHitDir            = Animator.StringToHash("HitDir");
        private static readonly int HashHitType           = Animator.StringToHash("HitType");
        private static readonly int HashDeadIndex         = Animator.StringToHash("DeadIndex");
        private static readonly int HashDieTrigger        = Animator.StringToHash("Die");
        private static readonly int HashDrawTrigger       = Animator.StringToHash("Draw");
        private static readonly int HashSheatheTrigger    = Animator.StringToHash("Sheathe");
        private static readonly int HashIsSwordDrawn      = Animator.StringToHash("IsSwordDrawn");
        private static readonly int HashJumpTrigger       = Animator.StringToHash("Jump");
        private static readonly int HashIsJumping         = Animator.StringToHash("IsJumping");
        private static readonly int HashIsBlocking        = Animator.StringToHash("IsBlocking");
        private static readonly int HashTriggerXBotM      = Animator.StringToHash("Trigger_XBot_M");

        public static InsaneGunSwordController Instance { get; private set; }

        public bool  IsDead                  => _isDead;
        public bool  IsSlowMotionActive      => _isSlowMotionActive;
        public bool  IsBlocking              => _isBlocking;
        public bool  IsJumping               => _isJumping;
        public bool  IsSwordDrawn            => isSwordDrawn;
        public bool  IsDrawingOrSheathing    => _isDrawingOrSheathing;
        public float HealthPercent           => Mathf.Clamp01(currentHealth / maxHealth);
        public float SlowMotionEnergyPercent => Mathf.Clamp01(currentSlowMotionEnergy / maxSlowMotionEnergy);
        public int   CurrentComboCount       => _comboIndex;
        public int   CurrentHeavyComboCount  => _heavyComboIndex;
        public bool  IsComboWindowOpen       => _comboWindowOpen;
        public bool  IsAttacking             => _isAttacking;
        public bool  IsDodging               => _isDodging;
        public Animator AnimatorComponent    => _animator;
        public bool  IsComboRunning          => _aiComboRunning;

        // ── Component Safety & Domain Reload Recovery ───────────────────────
        public void EnsureComponents()
        {
            if (_cc == null) _cc = GetComponent<CharacterController>();
            if (_animator == null)
            {
                _animator = GetComponent<Animator>();
                CacheAnimatorParameters();
            }
            else if (_animParamHashes.Count == 0)
            {
                CacheAnimatorParameters();
            }
            if (_mainCamera == null) _mainCamera = Camera.main;
            if (_ghostEmitter == null) _ghostEmitter = GetComponent<HolographicGhostEmitter>();
            if (audioSource == null) audioSource = GetComponent<AudioSource>();

            if (groundLayers == ~0 || groundLayers == 0)
            {
                int playerLayer = LayerMask.NameToLayer("Player");
                int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
                int maskToExclude = 0;
                if (playerLayer >= 0) maskToExclude |= (1 << playerLayer);
                if (ignoreRaycastLayer >= 0) maskToExclude |= (1 << ignoreRaycastLayer);
                groundLayers = ~maskToExclude;
            }

            if (swordTrail == null) swordTrail = GetComponentInChildren<TrailRenderer>(true);
            if (swordTrail != null)
            {
                AAACyberCombatVFX.ConfigureBladeTrail(swordTrail, controlMode == ControlMode.PlayerControlled);
                if (_swordSparkles == null) _swordSparkles = swordTrail.GetComponentInChildren<ParticleSystem>(true);
            }
        }

        // ── Awake ───────────────────────────────────────────────────────────
        private void Awake()
        {
            if (controlMode == ControlMode.PlayerControlled)
            {
                Instance = this;
                showCombatHUD = false;
            }
            EnsureComponents();
            _initialSpawnPosition = transform.position;
            _initialSpawnRotation = transform.rotation;
            if (controlMode == ControlMode.AICirclingEnemy)
            {
                if (maxHealth <= 0f) maxHealth = aiMaxHealth;
            }
            currentHealth = maxHealth;
            currentPoise  = maxPoise;
            _renderers  = GetComponentsInChildren<Renderer>();
            _propBlock  = new MaterialPropertyBlock();
            if (_ghostEmitter == null) _ghostEmitter = gameObject.AddComponent<HolographicGhostEmitter>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 0.5f;
                audioSource.playOnAwake  = false;
            }
            // Intelligent layer & enemyLayers setup
            int pLayer = LayerMask.NameToLayer("Player");
            int eLayer = LayerMask.NameToLayer("Enemy");

            if (controlMode == ControlMode.PlayerControlled || CompareTag("Player"))
            {
                if (pLayer != -1 && gameObject.layer == 0) gameObject.layer = pLayer;
                if (enemyLayers == 0 || enemyLayers == ~0)
                {
                    enemyLayers = (eLayer != -1 ? (1 << eLayer) : 0) | (1 << 0);
                }
                // City NPCs live on the Default layer; the player must always be able to strike them.
                enemyLayers |= (1 << 0);
            }
            else
            {
                if (eLayer != -1 && gameObject.layer == 0) gameObject.layer = eLayer;
                if (enemyLayers == 0 || enemyLayers == ~0)
                {
                    enemyLayers = (pLayer != -1 ? (1 << pLayer) : 0) | (1 << 0);
                }
            }
            // Eliminate character motion blur/ghosting during locomotion and protect against occlusion culling
            var smrs = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var smr in smrs)
            {
                if (smr != null)
                {
                    smr.skinnedMotionVectors = false;
                    smr.updateWhenOffscreen = true;
                    smr.allowOcclusionWhenDynamic = false;
                }
            }
            // Ensure all renderers on character ignore dynamic occlusion culling
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r != null) r.allowOcclusionWhenDynamic = false;
            }
            SetupWeaponSockets();
        }

        private void SetupWeaponSockets()
        {
            EnsureComponents();
            if (rightHandSocket == null && _animator != null)
            { var rh = _animator.GetBoneTransform(HumanBodyBones.RightHand); if (rh != null) rightHandSocket = rh; }
            if (leftHandSocket == null && _animator != null)
            { var lh = _animator.GetBoneTransform(HumanBodyBones.LeftHand);  if (lh != null) leftHandSocket  = lh; }
            if (hipSocket == null && _animator != null)
            {
                var hip = _animator.GetBoneTransform(HumanBodyBones.Hips);
                if (hip != null)
                {
                    var existingHs = hip.Find("Hip_Sheath_Socket");
                    if (existingHs != null) hipSocket = existingHs;
                }
            }
            if (rightHandSocket != null) DisableWeaponColliders(rightHandSocket.gameObject);
            if (leftHandSocket != null)  DisableWeaponColliders(leftHandSocket.gameObject);
            if (hipSocket != null)       DisableWeaponColliders(hipSocket.gameObject);

            if (rightHandSocket != null && rightHandSocket.childCount == 0 && swordPrefab != null)
                Instantiate(swordPrefab, rightHandSocket);

            if (swordInstance == null && rightHandSocket != null)
            {
                var blade = rightHandSocket.Find("Alita_Damascus_Blade") ?? rightHandSocket.Find("Damascus_Blade")
                         ?? rightHandSocket.Find("Player_Cyber_Katana") ?? rightHandSocket.Find("Enemy_Cyber_Katana")
                         ?? rightHandSocket.Find("Thug_Cyber_Katana");

                if (blade == null)
                {
                    foreach (Transform child in rightHandSocket.GetComponentsInChildren<Transform>(true))
                    {
                        if (child != rightHandSocket && (child.name.Contains("Katana") || child.name.Contains("Blade") || child.name.Contains("Sword")))
                        {
                            blade = child;
                            break;
                        }
                    }
                }
                if (blade != null) swordInstance = blade;
            }

            if (swordInstance != null)
            {
                DisableWeaponColliders(swordInstance.gameObject);

                // Anti-culling protection: Ensure weapon is never culled by Occlusion Culling or Frustum Culling
                foreach (var r in swordInstance.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null) continue;
                    r.allowOcclusionWhenDynamic = false;
                    if (r is MeshRenderer mr)
                    {
                        var mf = mr.GetComponent<MeshFilter>();
                        if (mf != null && mf.sharedMesh != null)
                        {
                            mf.sharedMesh.bounds = new Bounds(Vector3.zero, new Vector3(30f, 30f, 30f));
                        }
                    }
                    else if (r is SkinnedMeshRenderer smr)
                    {
                        smr.updateWhenOffscreen = true;
                        smr.localBounds = new Bounds(Vector3.zero, new Vector3(30f, 30f, 30f));
                    }
                }

                if (swordTrail == null)
                {
                    swordTrail = swordInstance.GetComponentInChildren<TrailRenderer>(true);
                    if (swordTrail == null)
                    {
                        var trailGo = new GameObject("Tron_Blade_Edge_Trail");
                        trailGo.transform.SetParent(swordInstance, false);
                        trailGo.transform.localPosition = new Vector3(0f, 0f, -0.85f);
                        swordTrail = trailGo.AddComponent<TrailRenderer>();
                    }
                }
                AAACyberCombatVFX.ConfigureBladeTrail(swordTrail, controlMode == ControlMode.PlayerControlled);
                swordTrail.emitting = false;

                if (swordInstance.parent == rightHandSocket)
                {
                    swordHandLocalPos = swordInstance.localPosition;
                    swordHandLocalRot = swordInstance.localEulerAngles;
                    isSwordDrawn = true;
                }
                else if (hipSocket != null && swordInstance.parent == hipSocket)
                {
                    swordHipLocalPos = swordInstance.localPosition;
                    swordHipLocalRot = swordInstance.localEulerAngles;
                    isSwordDrawn = false;
                }
            }

            SafeSetBool(HashIsSwordDrawn, isSwordDrawn);
            SafeSetBool(HashIsJumping, false);
        }

        /// <summary>
        /// Disables physics colliders on weapon meshes so they never collide with the ground or character controller.
        /// </summary>
        public static void DisableWeaponColliders(GameObject go)
        {
            if (go == null) return;
            var cols = go.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null) cols[i].enabled = false;
            }
        }

        // ── Main Loop ───────────────────────────────────────────────────────
        private void Update()
        {
            EnsureComponents();
            if (_isDead)
            {
                if (controlMode == ControlMode.PlayerControlled)
                {
                    bool rPressed = false;
                    var kb = UnityEngine.InputSystem.Keyboard.current;
                    if (kb != null && kb.rKey.wasPressedThisFrame) rPressed = true;
                    var gp = UnityEngine.InputSystem.Gamepad.current;
                    if (gp != null && (gp.buttonWest.wasPressedThisFrame || gp.buttonSouth.wasPressedThisFrame || gp.startButton.wasPressedThisFrame)) rPressed = true;
                    try { if (Input.GetKeyDown(KeyCode.R)) rPressed = true; } catch { }

                    if (rPressed)
                    {
                        if (SyntyCombined.CyberpunkHUDController.Instance != null)
                        {
                            SyntyCombined.CyberpunkHUDController.Instance.TriggerReboot();
                        }
                        else
                        {
                            Vector3 respawnPos = _initialSpawnPosition != Vector3.zero ? _initialSpawnPosition : (transform.position + Vector3.up * 0.15f);
                            Revive(respawnPos);
                            transform.rotation = _initialSpawnRotation;
                            currentHealth = maxHealth;
                            currentSlowMotionEnergy = maxSlowMotionEnergy;
                        }
                    }
                }
                return;
            }
            HandleSlowMotion();
            UpdateComboWindow();
            switch (controlMode)
            {
                case ControlMode.PlayerControlled: HandlePlayerInput(); break;
                case ControlMode.AICirclingEnemy:
                    HandleAIBehavior();
                    if (Time.time - _lastPoiseDamageTime > 1.4f && currentPoise < maxPoise)
                    {
                        currentPoise = Mathf.Min(maxPoise, currentPoise + poiseRecoveryRate * Time.deltaTime);
                        _isPoiseBroken = false;
                    }
                    break;
            }
            GroundCheck();
            ApplyMovementAndGravity();

            // Out-of-bounds void safety recovery fallback
            if (transform.position.y < -30f)
            {
                _verticalVelocity = 0f;
                _knockbackVelocity = Vector3.zero;
                _moveDirection = Vector3.zero;
                if (_cc != null) _cc.enabled = false;
                transform.position = _initialSpawnPosition + Vector3.up * 0.15f;
                if (_cc != null) _cc.enabled = true;
                Debug.LogWarning("[InsaneGunSwordController] Player fell out of bounds! Safely recovered to initial spawn position.");
            }
        }

        // ── Combo Window (animation-driven) ─────────────────────────────────
        /// <summary>
        /// Polls animator normalizedTime to manage the combo-accept window.
        /// When the window opens with a queued attack, immediately fires the next hit.
        /// Also supports input buffering so clicks during windup or recovery chain seamlessly.
        /// </summary>
        private void UpdateComboWindow()
        {
            if (!_isAttacking)
            {
                if (Time.unscaledTime - _lastAttackTime > comboResetDelay && (_comboIndex > 0 || _heavyComboIndex > 0))
                {
                    _comboIndex = 0;
                    _heavyComboIndex = 0;
                    _comboQueued = false;
                    _comboWindowOpen = false;
                }
                return;
            }

            if (_animator == null || _animator.runtimeAnimatorController == null) return;

            AnimatorStateInfo si = _animator.GetCurrentAnimatorStateInfo(0);
            bool inAttack = si.IsTag("Attack") || si.IsName("Attack") ||
                            (si.shortNameHash == Animator.StringToHash("Attack_0" + _comboIndex));

            if (!inAttack && !_animator.IsInTransition(0))
            {
                _isAttacking = false;
                _comboWindowOpen = false;
                _heavyComboIndex = 0;
                SetSwordTrailEmitting(false);
                if (controlMode == ControlMode.AICirclingEnemy)
                {
                    EnemyGroupCombatCoordinator.Instance?.NotifyAttackFinished(this);
                }

                // If player buffered a combo hit while transitioning back, fire immediately!
                if (_comboQueued)
                {
                    _comboQueued = false;
                    int nextHit = (_comboIndex % 7) + 1;
                    ExecuteLightAttack(nextHit, Vector3.zero);
                }
                return;
            }

            float n = si.normalizedTime;
            float norm = n > 1f ? (n % 1f) : n;
            bool win = norm >= comboAcceptWindowStart && norm <= comboAcceptWindowEnd;
            _comboWindowOpen = win;

            // If combo cancel window is open and user buffered an attack, chain immediately!
            if (win && _comboQueued)
            {
                _comboQueued = false;
                int nextHit = (_comboIndex % 7) + 1;
                ExecuteLightAttack(nextHit, Vector3.zero);
            }
        }

        public void TriggerNextComboHit()
        {
            int nextHit = (_comboIndex % 7) + 1;
            ExecuteLightAttack(nextHit, Vector3.zero);
        }

        private void OnDisable()
        {
            EnemyGroupCombatCoordinator.Instance?.UnregisterCombatant(this);
        }

        private void OnDestroy()
        {
            EnemyGroupCombatCoordinator.Instance?.UnregisterCombatant(this);
            if (Instance == this) Instance = null;
        }

        // ── Slow Motion ─────────────────────────────────────────────────────
        private void HandleSlowMotion()
        {
            if (SyntyCombined.InGamePauseManager.IsPaused) return;

            if (!enableSlowMotion || controlMode != ControlMode.PlayerControlled || _isDead)
            { if (_isSlowMotionActive) { _isSlowMotionActive = false; Time.timeScale = 1f; Time.fixedDeltaTime = 0.02f; } return; }
            if (Input.GetKeyDown(slowMotionKey)) _isSlowMotionActive = !_isSlowMotionActive;
            if (_isSlowMotionActive && currentSlowMotionEnergy > 0f)
            { currentSlowMotionEnergy -= slowMotionDrainRate * Time.unscaledDeltaTime; if (currentSlowMotionEnergy <= 0f) { currentSlowMotionEnergy = 0f; _isSlowMotionActive = false; } }
            else
            { if (currentSlowMotionEnergy <= 0f && _isSlowMotionActive) _isSlowMotionActive = false;
              currentSlowMotionEnergy = Mathf.Min(maxSlowMotionEnergy, currentSlowMotionEnergy + slowMotionRechargeRate * Time.unscaledDeltaTime); }
            Time.timeScale      = Mathf.Lerp(Time.timeScale, _isSlowMotionActive ? slowMotionTimeScale : 1f, Time.unscaledDeltaTime * 12f);
            Time.fixedDeltaTime = 0.02f * Time.timeScale;
            if (audioSource != null) audioSource.pitch = Mathf.Lerp(audioSource.pitch, _isSlowMotionActive ? 0.75f : 1f, Time.unscaledDeltaTime * 10f);
        }

        // ── Player Input ────────────────────────────────────────────────────
        private void HandlePlayerInput()
        {
            if (SyntyCombined.InGamePauseManager.IsPaused || inputLocked)
            {
                _moveDirection = Vector3.zero;
                if (_animator != null) _animator.SetFloat(HashMoveSpeed, 0f, 0.12f, Time.deltaTime);
                return;
            }

            if (enableLockOn && (Input.GetKeyDown(lockOnKey) || Input.GetMouseButtonDown(2))) ToggleLockOn();
            if (Input.GetKeyDown(blockKey)) ExecuteBlock();
            if (Input.GetKeyUp(blockKey))   _isBlocking = false;

            float h  = Input.GetAxisRaw("Horizontal");
            float v  = Input.GetAxisRaw("Vertical");
            bool run = Input.GetKey(KeyCode.LeftShift);
            Vector3 input = new Vector3(h, 0f, v).normalized;

            bool lmb   = Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.J);
            bool rmb   = Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.K);
            bool gun   = Input.GetKeyDown(KeyCode.Q);
            bool dodge = Input.GetKeyDown(KeyCode.LeftAlt) || Input.GetKeyDown(KeyCode.RightAlt);
            bool jump  = Input.GetKeyDown(jumpKey);
            bool toggleSword = Input.GetKeyDown(drawSheatheKey) || Input.GetKeyDown(drawSheatheAltKey);
            float tsl  = Time.unscaledTime - _lastAttackTime;

            bool mAction = false;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.mKey.wasPressedThisFrame) mAction = true;
            try { if (Input.GetKeyDown(customAnimationKey)) mAction = true; } catch { }

            if (mAction && !_isDodging && !_isDrawingOrSheathing && !_isDead)
            {
                ExecuteCustomAnimationAction();
            }

            if (toggleSword && !_isAttacking && !_isDodging && !_isDrawingOrSheathing)
            {
                ToggleSwordDraw();
            }

            if (jump && !_isAttacking && !_isDodging)
            {
                if (_isDrawingOrSheathing)
                {
                    CancelDrawSheatheEarly();
                }
                TryJump(input);
            }

            if (!_isDodging && !_isDrawingOrSheathing)
            {
                if (lmb)
                {
                    if (!isSwordDrawn)
                    {
                        DrawSword(queueAttackAfter: 1);
                    }
                    else if (!_isAttacking)
                    {
                        StartLightAttack(input);
                    }
                    else
                    {
                        if (_comboWindowOpen)
                        {
                            _comboQueued = false;
                            _comboWindowOpen = false;
                            int nextHit = (_comboIndex % 7) + 1;
                            ExecuteLightAttack(nextHit, input);
                        }
                        else
                        {
                            // Buffer the input so it triggers the instant combo window opens!
                            _comboQueued = true;
                        }
                    }
                }
                // Single-Click Heavy Attack: 1 RMB click = 1 heavy strike. Chains only if clicked in combo window!
                if (rmb && tsl >= minAttackInterval)
                {
                    if (!isSwordDrawn)
                    {
                        DrawSword(queueAttackAfter: 2);
                    }
                    else if (!_isAttacking)
                    {
                        ExecuteHeavyAttack(1, input);
                    }
                    else if (_comboWindowOpen && _heavyComboIndex > 0)
                    {
                        int nextHeavy = Mathf.Min(_heavyComboIndex + 1, 3);
                        _comboQueued = false;
                        _comboWindowOpen = false;
                        ExecuteHeavyAttack(nextHeavy, input);
                    }
                    else if (_comboWindowOpen && _comboIndex > 0)
                    {
                        _comboQueued = false;
                        _comboWindowOpen = false;
                        ExecuteHeavyAttack(3, input);
                    }
                }
                if (gun && !_isAttacking && tsl >= minAttackInterval) StartGunAttack(input);
                if (dodge)
                { if (_isAttacking && tsl > 0.22f) { InterruptAttack(); ExecuteDodge(input); } else if (!_isAttacking) ExecuteDodge(input); }
            }

            if (_isDodging || _isAttacking || _isDrawingOrSheathing)
            {
                _moveDirection = Vector3.zero; // STRICTLY ZERO VELOCITY DURING ATTACK/DRAW ANIMATIONS: ELIMINATES DRIFT
                _animator.SetFloat(HashMoveSpeed, 0f, 0.08f, Time.deltaTime); // KEEP LOCOMOTION ROOTED
                return;
            }
            UpdateLocomotionAndRotation(input, run);
        }

        private void UpdateLocomotionAndRotation(Vector3 input, bool run)
        {
            if (lockedTarget != null)
            {
                if (!lockedTarget || !lockedTarget.gameObject.activeInHierarchy)
                {
                    lockedTarget = null;
                }
                else
                {
                    var ins = lockedTarget.GetComponentInParent<InsaneGunSwordController>() ?? lockedTarget.GetComponent<InsaneGunSwordController>();
                    var npc = lockedTarget.GetComponentInParent<PolygonCyberCity.NPC.Spawner.CyberNPCController>() ?? lockedTarget.GetComponent<PolygonCyberCity.NPC.Spawner.CyberNPCController>();
                    if ((ins != null && ins.IsDead) || (npc != null && npc.IsDead)) lockedTarget = null;
                }
            }

            if (lockedTarget != null)
            {
                Vector3 toT = lockedTarget.position - transform.position; toT.y = 0f;
                if (toT.sqrMagnitude > 0.01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toT.normalized), rotationSpeed * Time.deltaTime);
                if (input.magnitude > 0.05f) { _moveDirection = CamDir(input) * (run ? runSpeed : walkSpeed); _animator.SetFloat(HashMoveSpeed, run ? 2f : 1f, 0.10f, Time.deltaTime); }
                else                         { _moveDirection = Vector3.zero;                                   _animator.SetFloat(HashMoveSpeed, 0f,            0.12f, Time.deltaTime); }
            }
            else if (input.magnitude > 0.05f)
            {
                Vector3 wd = CamDir(input);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(wd), rotationSpeed * Time.deltaTime);
                _moveDirection = wd * (run ? runSpeed : walkSpeed);
                _animator.SetFloat(HashMoveSpeed, run ? 2f : 1f, 0.10f, Time.deltaTime);
            }
            else { _moveDirection = Vector3.zero; _animator.SetFloat(HashMoveSpeed, 0f, 0.12f, Time.deltaTime); }
        }

        private void UpdateLocomotionAnim(Vector3 input, bool run)
        { _animator.SetFloat(HashMoveSpeed, input.magnitude > 0.05f ? (run ? 2f : 1f) : 0f, 0.12f, Time.deltaTime); }

        private Vector3 CamDir(Vector3 input)
        {
            Vector3 cf = _mainCamera != null ? _mainCamera.transform.forward : Vector3.forward;
            Vector3 cr = _mainCamera != null ? _mainCamera.transform.right   : Vector3.right;
            cf.y = 0f; cf.Normalize(); cr.y = 0f; cr.Normalize();
            return (cf * input.z + cr * input.x).normalized;
        }

        // ── Light Attack & Full 7-Hit Combo System ──────────────────────────
        public void StartLightAttack(Vector3 input)
        {
            int nextStep = 1;
            if (Time.unscaledTime - _lastAttackTime <= comboResetDelay && _comboIndex >= 1 && _comboIndex < 7)
            {
                nextStep = _comboIndex + 1;
            }
            ExecuteLightAttack(nextStep, input);
        }

        public void ExecuteLightAttack(int step, Vector3 input)
        {
            EnsureComponents();
            _comboIndex      = Mathf.Clamp(step, 1, 7);
            _heavyComboIndex = 0;
            _lastAttackTime  = Time.unscaledTime;
            _comboQueued     = false;
            _comboWindowOpen = false;
            _isAttacking     = true;
            _moveDirection   = Vector3.zero; // STRICTLY ZERO VELOCITY: PREVENTS DRIFTING

            bool isFinisher = (_comboIndex == 7);
            float dur = isFinisher ? 0.22f : 0.14f;

            // Smart combat magnetism: prioritize facing nearby target enemy within 150 deg arc
            Transform lt = controlMode == ControlMode.PlayerControlled ? FindBestLungeTarget(10f, 150f) : targetEnemy;
            if (lt != null)
            {
                Vector3 td = lt.position - transform.position; td.y = 0f;
                if (td.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(td.normalized);
                float d2 = td.magnitude;
                float lungeDist = Mathf.Clamp(d2 - 1.35f, 0f, isFinisher ? 3.5f : 3.0f);
                if (lungeDist > 0.08f)
                {
                    StartCoroutine(CombatStep(td.normalized * (lungeDist / dur), dur));
                }
                else
                {
                    // Already in striking distance: grounded forward posture adjustment
                    StartCoroutine(CombatStep(td.normalized * 0.8f, 0.12f));
                }
                float stepDist = isFinisher ? 0.9f : 0.5f;
                StartCoroutine(CombatStep(transform.forward * (stepDist / 0.14f), 0.14f));
            }
            else
            {
                float stepDist = isFinisher ? 1.0f : 0.6f;
                StartCoroutine(CombatStep(transform.forward * (stepDist / dur), dur));
            }

            SafeSetInteger(HashAttackIndex, _comboIndex);
            SafeSetTrigger(HashAttackTrigger);
            SafeCrossFade("Attack_0" + _comboIndex, 0.06f, 0, 0f);

            PlaySwordSlash(_comboIndex, isFinisher);
            AAACyberCombatVFX.SpawnSlashArc(transform, _comboIndex, isFinisher);
            if (_ghostEmitter != null)
            {
                Color comboColor = isFinisher ? new Color(0.95f, 0.70f, 0.15f, 0.65f) : new Color(0.0f, 0.75f, 0.95f, 0.55f);
                _ghostEmitter.EmitBurst(0.12f, comboColor, 0.055f, 0.18f);
            }

            float dmg = isFinisher ? attackDamage * 2.5f : attackDamage * (1f + 0.08f * (_comboIndex - 1));
            int impactCi = isFinisher ? 3 : 0; // 7th hit finisher launches into Euphoria Ragdoll!
            StartCoroutine(CheckMeleeHit(0.08f, dmg, impactCi));

            SetSwordTrailEmitting(true);
            StartCoroutine(DisableTrailAfter(dur + 0.16f));

            if (_lightAttackRoutine != null) StopCoroutine(_lightAttackRoutine);
            _lightAttackRoutine = StartCoroutine(LightAttackFallback());
        }

        private IEnumerator DisableTrailAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (!_isAttacking || !_comboQueued)
            {
                SetSwordTrailEmitting(false);
            }
        }

        // Fallback: clears attack state if animator polling fails to do so in time
        private IEnumerator LightAttackFallback()
        {
            yield return new WaitForSeconds(0.95f);
            if (_isAttacking)
            {
                if (_comboQueued)
                {
                    _comboQueued = false;
                    int nextHit = (_comboIndex % 7) + 1;
                    ExecuteLightAttack(nextHit, Vector3.zero);
                }
                else
                {
                    _isAttacking = false;
                    _comboWindowOpen = false;
                    SetSwordTrailEmitting(false);
                }
            }
            _lightAttackRoutine = null;
        }

        public void ExecuteBlock()
        { _isBlocking = true; _blockStartTime = Time.time; SafeSetTrigger(HashHitTrigger); }

        // ── Single-Click Heavy Attack System ────────────────────────────────
        public void StartHeavyCombo()
        {
            ExecuteHeavyAttack(1, Vector3.zero);
        }

        public void ExecuteHeavyAttack(int step, Vector3 input)
        {
            EnsureComponents();
            _heavyComboIndex = step;
            _comboIndex = 0;
            _comboQueued = false;
            _comboWindowOpen = false;
            _lastAttackTime = Time.unscaledTime;
            _isAttacking = true;
            _moveDirection = Vector3.zero; // STRICTLY ZERO VELOCITY: PREVENTS DRIFTING

            bool isFinisher = (step == 3);
            float dur = isFinisher ? 0.24f : 0.18f;
            float stepDist = (step == 1) ? 2.2f : (step == 2 ? 2.8f : 3.8f);

            SafeSetInteger(HashHeavyIndex, step);
            SafeSetTrigger(HashHeavyComboTrigger);
            PlaySwordSlash(step, true);
            SetSwordTrailEmitting(true);
            StartCoroutine(DisableTrailAfter(dur + 0.18f));
            AAACyberCombatVFX.SpawnSlashArc(transform, step == 3 ? 7 : (step == 2 ? 6 : 1), isFinisher);
            if (_ghostEmitter != null)
            {
                Color heavyColor = isFinisher ? new Color(0.95f, 0.70f, 0.15f, 0.65f) : _ghostEmitter.heavyComboColor;
                _ghostEmitter.EmitBurst(0.16f, heavyColor, 0.055f, 0.20f);
            }

            Transform lt = controlMode == ControlMode.PlayerControlled ? FindBestLungeTarget(12f, 150f) : targetEnemy;
            if (lt != null)
            {
                Vector3 td = lt.position - transform.position; td.y = 0f;
                if (td.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(td.normalized);
                float d = td.magnitude;
                float lungeDist = Mathf.Clamp(d - 1.35f, 0f, stepDist + 1.5f);
                if (lungeDist > 0.08f) StartCoroutine(CombatStep(td.normalized * (lungeDist / dur), dur));
                else StartCoroutine(CombatStep(td.normalized * 0.9f, 0.14f));
            }
            else
            {
                if (input.magnitude > 0.1f && controlMode == ControlMode.PlayerControlled)
                {
                    Vector3 d = CamDir(input);
                    if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(d);
                }
                float forwardStep = isFinisher ? 1.2f : 0.8f;
                StartCoroutine(CombatStep(transform.forward * (forwardStep / dur), dur));
            }

            string heavyState = (step == 1) ? "Combo_01_Phase1" : (step == 2 ? "Combo_02_Phase1" : "Attack_07");
            SafeCrossFade(heavyState, 0.06f, 0, 0f);

            float dmg = (step == 1) ? heavyAttackDamage * 0.90f : (step == 2 ? heavyAttackDamage * 1.30f : heavyAttackDamage * 2.0f);
            int impactCi = step; // 1 = heavy stagger, 2 = heavy cleave stumble, 3 = EUPHORIA RAGDOLL LAUNCH
            StartCoroutine(CheckMeleeHit(0.12f, dmg, impactCi));

            if (_heavyAttackRoutine != null) StopCoroutine(_heavyAttackRoutine);
            _heavyAttackRoutine = StartCoroutine(HeavyAttackFallback(step));
        }

        private IEnumerator HeavyAttackFallback(int step)
        {
            float timeout = (step == 3) ? 1.15f : 0.85f;
            yield return new WaitForSeconds(timeout);
            if (_isAttacking && !_comboQueued)
            {
                _isAttacking = false;
                _heavyComboIndex = 0;
                SetSwordTrailEmitting(false);
            }
            _heavyAttackRoutine = null;
        }

        // ── Gun Attack ──────────────────────────────────────────────────────
        public void StartGunAttack(Vector3 input)
        {
            _lastAttackTime = Time.unscaledTime; _comboIndex = 0;
            if (input.magnitude > 0.1f && controlMode == ControlMode.PlayerControlled)
            { Vector3 d = CamDir(input); if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(d); }
            int gunIdx = Random.Range(1, 5);
            // CRITICAL FIX: Reset the trigger and clear GunIndex so Animator never re-evaluates and replays the animation twice
            SafeResetTrigger(HashGunAttackTrigger);
            SafeSetInteger(HashGunIndex, 0);
            SafeCrossFade("Gun_Attack_0" + gunIdx, 0.05f, 0, 0f);
            _isAttacking = true;
            if (_gunAttackRoutine != null) StopCoroutine(_gunAttackRoutine);
            _gunAttackRoutine = StartCoroutine(GunBurstRoutine());
        }

        private IEnumerator GunBurstRoutine()
        {
            yield return new WaitForSeconds(0.12f);
            for (int i = 0; i < gunBurstCount; i++) { FireGunShot(); yield return new WaitForSeconds(gunBurstInterval); }
            yield return new WaitForSeconds(0.30f);
            SafeResetTrigger(HashGunAttackTrigger);
            SafeSetInteger(HashGunIndex, 0);
            _isAttacking = false; _gunAttackRoutine = null;
        }

        public void ExecuteCustomAnimationAction()
        {
            EnsureComponents();
            if (_customAnimationRoutine != null)
            {
                CancelCustomAnimationAction();
                return;
            }

            _lastAttackTime = Time.unscaledTime;
            _comboIndex = 0;
            _heavyComboIndex = 0;
            _comboQueued = false;
            _comboWindowOpen = false;
            _moveDirection = Vector3.zero;

            SafeResetTrigger(HashTriggerXBotM);
            SafeSetTrigger(HashTriggerXBotM);
            SafeCrossFade(customAnimationStateName, 0.15f, 0, 0f);
            _isAttacking = true;

            _customAnimationRoutine = StartCoroutine(CustomAnimationActionRoutine());
        }

        public void CancelCustomAnimationAction()
        {
            if (_customAnimationRoutine != null)
            {
                StopCoroutine(_customAnimationRoutine);
                _customAnimationRoutine = null;
            }
            _isAttacking = false;
            SafeCrossFade("Locomotion", 0.2f, 0, 0f);
        }

        private IEnumerator CustomAnimationActionRoutine()
        {
            float elapsed = 0f;
            float maxDuration = 15.2f;

            while (elapsed < maxDuration)
            {
                elapsed += Time.deltaTime;

                float h = Input.GetAxisRaw("Horizontal");
                float v = Input.GetAxisRaw("Vertical");
                if (Mathf.Abs(h) > 0.15f || Mathf.Abs(v) > 0.15f)
                {
                    CancelCustomAnimationAction();
                    yield break;
                }

                yield return null;
            }

            _isAttacking = false;
            _customAnimationRoutine = null;
            SafeCrossFade("Locomotion", 0.25f, 0, 0f);
        }

        private void FireGunShot()
        {
            PlaySound(gunShotSounds);
            if (gunMuzzleVFX != null) gunMuzzleVFX.Play();
            Vector3 start  = transform.position + Vector3.up * 1.3f + transform.forward * 0.4f;
            Vector3 aimDir;
            if (controlMode == ControlMode.AICirclingEnemy && targetEnemy != null)
            {
                Vector3 targetCenter = targetEnemy.position + Vector3.up * 1.20f;
                aimDir = (targetCenter - start).normalized;
                Vector3 spread = Random.insideUnitSphere * 0.035f;
                aimDir = (aimDir + spread).normalized;
            }
            else
            {
                aimDir = _mainCamera != null ? _mainCamera.transform.forward : transform.forward;
            }
            Vector3 end    = start + aimDir * gunRange;
            if (Physics.Raycast(new Ray(start, aimDir), out RaycastHit hit, gunRange, enemyLayers))
            {
                end = hit.point;
                // Resolve victim first: trial-protected pre-combat bots are
                // pass-through (no heavy spark, no decal — TakeDamage whiffs).
                var insPre = hit.collider.GetComponentInParent<InsaneGunSwordController>() ?? hit.collider.GetComponent<InsaneGunSwordController>();
                bool gunWhiffed = insPre != null && insPre != this && insPre.isInvincible
                    && IsTrialProtected != null && IsTrialProtected(insPre);
                if (!gunWhiffed)
                {
                    SpawnHitEffect(end, true);
                    SpawnBulletHole(hit);
                }
                if (hit.collider.transform.root != transform.root)
                {
                    var ins = hit.collider.GetComponentInParent<InsaneGunSwordController>() ?? hit.collider.GetComponent<InsaneGunSwordController>();
                    if (ins != null && ins != this)
                    {
                        ins.TakeDamage(gunDamagePerShot, end, aimDir, 0);
                        if (ins.controlMode == ControlMode.PlayerControlled)
                        {
                            SmoothOrbitCamera.ShakeImpact(0.45f, aimDir * 0.15f);
                        }
                    }
                    else
                    {
                        var dmg = hit.collider.GetComponentInParent<SyntyCombined.ISyntyDamageable>() ?? hit.collider.GetComponent<SyntyCombined.ISyntyDamageable>();
                        if (dmg != null && dmg != (SyntyCombined.ISyntyDamageable)this) dmg.TakeDamage(gunDamagePerShot, end, aimDir, false);
                    }
                }
            }
            SpawnLaserTracer(start, end);
            SmoothOrbitCamera.ShakeImpact(0.18f, transform.forward * 0.06f);
        }

        private void SpawnBulletHole(RaycastHit hit)
        {
            GameObject g = new GameObject("BulletHole_FX");
            g.transform.position = hit.point + hit.normal * 0.005f;
            g.transform.rotation = Quaternion.LookRotation(-hit.normal);
            var ps = g.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main; m.duration = 0.05f; m.startLifetime = 0.05f; m.startSpeed = 0f; m.startSize = 0.12f;
            m.startColor = new Color(0.05f, 0.05f, 0.05f, 0.9f); m.stopAction = ParticleSystemStopAction.Destroy;
            ps.emission.SetBursts(new ParticleSystem.Burst[]{ new ParticleSystem.Burst(0f, 1) });
            ps.Play(); SafeDestroy(g, 8f);
        }

        // ── Dodge ───────────────────────────────────────────────────────────
        public void ExecuteDodge(Vector3 input)
        {
            Vector3 wd; int di;
            if (input.magnitude > 0.1f)
            { wd = CamDir(input); Vector3 loc = transform.InverseTransformDirection(wd); float ax = Mathf.Abs(loc.x), az = Mathf.Abs(loc.z); di = az >= ax ? (loc.z >= 0f ? 0 : 1) : (loc.x > 0f ? 3 : 2); }
            else { wd = -transform.forward; di = 1; }
            StartCoroutine(DodgeRoutine(wd, di));
        }

        private IEnumerator DodgeRoutine(Vector3 dir, int di)
        {
            InterruptAttack(); _isDodging = true;
            SafeSetInteger(HashDodgeDir, di); SafeSetTrigger(HashDodgeTrigger);
            PlaySound(dashSounds != null && dashSounds.Length > 0 ? dashSounds : dodgeSounds);
            AAACyberCombatVFX.SpawnDodgeFX(transform.position, dir);
            if (_ghostEmitter != null)
            {
                Color dashCol = _ghostEmitter.dodgeGhostColor;
                _ghostEmitter.EmitBurst(dodgeDuration * 0.85f, dashCol, 0.055f, 0.22f);
            }
            float elapsed = 0f;
            while (elapsed < dodgeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dodgeDuration);
                // Kinetic, high-velocity Cyberpunk dash curve covering 6 meters cleanly
                float speed = (dodgeDistance / dodgeDuration) * (1f - t * 0.65f) * 1.30f;
                Vector3 moveDelta = dir * speed;
                if (_isGrounded) moveDelta.y = -4.5f; // Ground hugging keeps character grounded over curbs, ramps, and steps
                if (_cc != null && _cc.enabled && _cc.gameObject.activeInHierarchy)
                    _cc.Move(moveDelta * Time.deltaTime);
                yield return null;
            }
            _isDodging = false;
        }

        private void InterruptAttack()
        {
            if (_customAnimationRoutine != null) { StopCoroutine(_customAnimationRoutine); _customAnimationRoutine = null; }
            if (_lightAttackRoutine != null) { StopCoroutine(_lightAttackRoutine); _lightAttackRoutine = null; }
            if (_heavyAttackRoutine != null) { StopCoroutine(_heavyAttackRoutine); _heavyAttackRoutine = null; }
            if (_gunAttackRoutine   != null) { StopCoroutine(_gunAttackRoutine);   _gunAttackRoutine   = null; }
            if (_drawSheatheRoutine != null) { StopCoroutine(_drawSheatheRoutine); _drawSheatheRoutine = null; }
            _isDrawingOrSheathing = false;
            SetSwordTrailEmitting(false);
            SafeResetTrigger(HashGunAttackTrigger);
            SafeSetInteger(HashGunIndex, 0);
            _isAttacking = false; _comboQueued = false; _comboWindowOpen = false; _heavyComboIndex = 0;
            if (controlMode == ControlMode.AICirclingEnemy)
            {
                EnemyGroupCombatCoordinator.Instance?.NotifyAttackFinished(this);
            }
        }

        private IEnumerator CombatStep(Vector3 vel, float dur)
        {
            float e = 0f;
            while (e < dur)
            { e += Time.deltaTime; float t = Mathf.Clamp01(e / dur); float ease = Mathf.Sin((1f - t) * Mathf.PI * 0.5f); if (_cc != null && _cc.enabled && _cc.gameObject.activeInHierarchy) _cc.Move(vel * ease * Time.deltaTime); yield return null; }
        }

        private IEnumerator CheckMeleeHit(float delay, float dmg, int ci = 0)
        {
            yield return new WaitForSeconds(delay);
            float elapsed = 0f;
            float window = 0.35f;
            var hitControllers = new HashSet<InsaneGunSwordController>();
            var hitDamageables = new HashSet<SyntyCombined.ISyntyDamageable>();

            while (elapsed < window)
            {
                elapsed += Time.deltaTime;
                Vector3 hc = transform.position + transform.forward * 1.1f + Vector3.up * 1.0f;
                LayerMask hitMask = (enemyLayers.value != 0) ? enemyLayers : (LayerMask)(~0);
                Collider[] hits = Physics.OverlapSphere(hc, 2.25f, hitMask);
                foreach (var col in hits)
                {
                    if (col.gameObject == gameObject || col.transform.root == transform.root) continue;
                    Vector3 hp = col.ClosestPoint(hc);
                    var ins = col.GetComponentInParent<InsaneGunSwordController>() ?? col.GetComponent<InsaneGunSwordController>();
                    var tgt = col.GetComponentInParent<SyntyCombined.ISyntyDamageable>() ?? col.GetComponent<SyntyCombined.ISyntyDamageable>();
                    if (ins != null && ins != this && hitControllers.Add(ins))
                    {
                        // Trial-protected pre-combat bot: pass-through, no hit-stop,
                        // no impact VFX, no blood. TakeDamage plays the whiff spark.
                        bool whiffed = ins.isInvincible && IsTrialProtected != null && IsTrialProtected(ins);
                        ins.TakeDamage(dmg, hp, transform.forward, ci);
                        if (!whiffed)
                        {
                            AAACyberCombatVFX.SpawnHitImpact(hp, transform.forward, _comboIndex, ci >= 3);
                            PlaySound(swordImpactSounds, ci);
                            TriggerImpactFeedback(ci);
                        }
                    }
                    else if (tgt != null && tgt != (SyntyCombined.ISyntyDamageable)this && hitDamageables.Add(tgt))
                    {
                        tgt.TakeDamage(dmg, hp, transform.forward, ci > 0);
                        AAACyberCombatVFX.SpawnHitImpact(hp, transform.forward, _comboIndex, ci >= 3);
                        PlaySound(swordImpactSounds, ci);
                        TriggerImpactFeedback(ci);
                    }
                }
                // Direct striking fallback for locked/tracked target within 3m
                if (hitControllers.Count == 0 && hitDamageables.Count == 0 && targetEnemy != null)
                {
                    Vector3 td = targetEnemy.position - transform.position; td.y = 0f;
                    if (td.sqrMagnitude <= 9.0f)
                    {
                        var tgtIns = targetEnemy.GetComponent<InsaneGunSwordController>();
                        if (tgtIns != null && tgtIns != this && hitControllers.Add(tgtIns))
                        {
                            Vector3 hp = targetEnemy.position + Vector3.up * 1.0f;
                            tgtIns.TakeDamage(dmg, hp, transform.forward, ci);
                            AAACyberCombatVFX.SpawnHitImpact(hp, transform.forward, _comboIndex, ci >= 3);
                            PlaySound(swordImpactSounds, ci);
                            TriggerImpactFeedback(ci);
                        }
                    }
                }
                if (hitControllers.Count > 0 || hitDamageables.Count > 0)
                {
                    yield return new WaitForSeconds(0.04f);
                    break;
                }
                yield return null;
            }
        }

        private void TriggerImpactFeedback(int ci)
        {
            if (controlMode != ControlMode.PlayerControlled) return;
            // Crisp, tactile hitstop and trauma kick for light attacks (ci == 0) through heavy finishers (ci == 3)
            float sh = 0.32f, hs = 0.030f; Vector3 k = transform.forward * 0.08f;
            if (ci == 1) { sh = 0.52f; hs = 0.045f; k = transform.forward * 0.16f; }
            else if (ci == 2) { sh = 0.68f; hs = 0.060f; k = transform.forward * 0.22f; }
            else if (ci >= 3) { sh = 0.92f; hs = 0.085f; k = (transform.forward + Vector3.up * 0.35f) * 0.35f; }
            SmoothOrbitCamera.ShakeImpact(sh, k);
            if (hs > 0f) SmoothOrbitCamera.TriggerHitStop(hs);
        }

        private IEnumerator ExecutePerfectParry(Vector3 hitPoint, Vector3 hitDir)
        {
            Time.timeScale = 0.15f; Time.fixedDeltaTime = 0.02f * Time.timeScale;
            SpawnParryFX(hitPoint);
            SmoothOrbitCamera.ShakeImpact(0.75f, -hitDir * 0.25f);
            if (_ghostEmitter != null) _ghostEmitter.EmitBurst(0.40f, new Color(1f, 0.85f, 0.2f), 0.02f);
            PlaySound(swordImpactSounds, 2);
            yield return new WaitForSecondsRealtime(0.35f);
            Time.timeScale = _isSlowMotionActive ? slowMotionTimeScale : 1f;
            Time.fixedDeltaTime = 0.02f * Time.timeScale;
        }

        // ── AI Combat State ──────────────────────────────────────────────────
        private enum AICombatPhase { Approach, Circle, Pressure, Blitz, Retreat }
        private AICombatPhase _aiPhase        = AICombatPhase.Approach;
        private float _aiPhaseTimer           = 0f;
        private float _aiCircleAngle          = 0f;
        private float _aiCircleDir            = 1f;
        private float _aiNextPhaseChange      = 0f;
        private float _aiThreatMeter          = 0f;   // 0-1: ramps as enemy hits player
        private int   _aiComboTarget          = 0;    // how many hits to chain this burst
        private int   _aiComboFired           = 0;
        private bool  _aiComboRunning         = false;
        private float _aiDodgeCooldown        = 0f;
        private float _aiFeintTimer           = 0f;
        private bool  _aiWasAttacking         = false;

        private void HandleAIBehavior()
        {
            if (inputLocked)
            {
                _moveDirection = Vector3.zero;
                if (_animator != null) _animator.SetFloat(HashMoveSpeed, 0f, 0.12f, Time.deltaTime);
                return;
            }

            // Stagger lock-out
            if (Time.time < _staggerEndTime)
            {
                _moveDirection = Vector3.zero;
                _animator.SetFloat(HashMoveSpeed, 0f, 0.15f, Time.deltaTime);
                _aiComboRunning = false;
                return;
            }

            // Auto-find player
            if (targetEnemy == null)
            {
                var p = GameObject.FindWithTag("Player");
                if (p != null) targetEnemy = p.transform;
                else
                {
                    var hero = GameObject.Find("Alita_BattleAngel_Hero");
                    if (hero != null) targetEnemy = hero.transform;
                }
                if (targetEnemy == null) return;
            }

            float dist = Vector3.Distance(transform.position, targetEnemy.position);
            Vector3 toTarget = (targetEnemy.position - transform.position);
            Vector3 dirFlat  = toTarget; dirFlat.y = 0f;
            if (dirFlat.sqrMagnitude > 0.01f) dirFlat.Normalize();

            // Always face target
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, Quaternion.LookRotation(dirFlat), rotationSpeed * Time.deltaTime);

            // ── Multi-Enemy Group Coordination (Surround First + Coordinated Attack Sequencing) ──
            var coordinator = EnemyGroupCombatCoordinator.Instance;
            if (coordinator != null && targetEnemy != null)
            {
                coordinator.RegisterCombatant(this, targetEnemy);

                if (coordinator.IsGroupSurroundActive(targetEnemy))
                {
                    bool hasToken = _hasAttackToken || coordinator.HasAttackToken(this);

                    if (!hasToken)
                    {
                        // ── SURROUND & PERIMETER GUARD MODE (WAITING TURN) ──
                        // Suppress attack execution. Maintain wide perimeter standoff around player.
                        Vector3 slotPos = coordinator.GetSurroundSlotPosition(this, targetEnemy, -1f);
                        Vector3 toSlot = slotPos - transform.position;
                        toSlot.y = 0f;
                        float distToSlot = toSlot.magnitude;

                        // Perimeter spacing buffer: If player steps close (< 85% of surroundRadius), actively back up to keep distance!
                        if (dist < coordinator.surroundRadius * 0.85f)
                        {
                            Vector3 backStep = -dirFlat * (walkSpeed * 1.15f);
                            _moveDirection = backStep;
                            _animator.SetFloat(HashMoveSpeed, 1.0f, 0.12f, Time.deltaTime);
                            if (dirFlat.sqrMagnitude > 0.01f)
                            {
                                transform.rotation = Quaternion.RotateTowards(
                                    transform.rotation, Quaternion.LookRotation(dirFlat), rotationSpeed * Time.deltaTime);
                            }
                        }
                        else if (distToSlot > 1.1f)
                        {
                            // Move into assigned surrounding slot around player
                            Vector3 moveDir = toSlot.normalized;
                            bool shouldRun = distToSlot > 3.5f;
                            _moveDirection = moveDir * (shouldRun ? runSpeed : walkSpeed * 1.25f);
                            _animator.SetFloat(HashMoveSpeed, shouldRun ? 2f : 1.2f, 0.12f, Time.deltaTime);

                            Vector3 lookDir = distToSlot > 2.5f ? moveDir : dirFlat;
                            if (lookDir.sqrMagnitude > 0.01f)
                            {
                                transform.rotation = Quaternion.RotateTowards(
                                    transform.rotation, Quaternion.LookRotation(lookDir.normalized), rotationSpeed * Time.deltaTime);
                            }
                        }
                        else
                        {
                            // In position on perimeter: face target
                            if (dirFlat.sqrMagnitude > 0.01f)
                            {
                                transform.rotation = Quaternion.RotateTowards(
                                    transform.rotation, Quaternion.LookRotation(dirFlat), rotationSpeed * Time.deltaTime);
                            }

                            // Maintain subtle menacing combat strafe bob along perimeter arc
                            float strafeBob = Mathf.Sin(Time.time * 2.0f + (float)(GetHashCode() % 100) * 0.1f) * 0.35f;
                            _moveDirection = transform.right * strafeBob;
                            _animator.SetFloat(HashMoveSpeed, 0.35f, 0.15f, Time.deltaTime);
                        }

                        // Cancel any uncoordinated combo burst
                        _aiComboRunning = false;
                        return;
                    }
                else
                {
                    // ── WE HOLD THE ATTACK TOKEN: COORDINATED OFFENSIVE STRIKE ──
                    float strikeRange = transform.localScale.x > 1.25f ? 3.0f : 2.2f;
                    if (dist > strikeRange && !_isAttacking && !_aiComboRunning)
                    {
                        _moveDirection = dirFlat * runSpeed;
                        _animator.SetFloat(HashMoveSpeed, 2f, 0.08f, Time.deltaTime);
                        return;
                    }
                    else if (dist <= strikeRange && !_isAttacking && !_aiComboRunning)
                    {
                        _moveDirection = Vector3.zero;
                        _animator.SetFloat(HashMoveSpeed, 0f, 0.1f, Time.deltaTime);

                        coordinator.NotifyAttackStarted(this);
                        float roll = Random.value;
                        if (roll < 0.45f)
                            StartAIGunSwordCombo(AIGunSwordComboType.ExecutionerShred);
                        else if (roll < 0.80f)
                            StartAIGunSwordCombo(AIGunSwordComboType.BlitzkriegOverdrive);
                        else
                            AITriggerComboBurst(2, 3);
                        return;
                    }
                }
            }
        }

            // ── Threat Meter Decay ───────────────────────────────────────────
            _aiThreatMeter = Mathf.Clamp01(_aiThreatMeter - 0.04f * Time.deltaTime);

            // ── Dodge cooldown ───────────────────────────────────────────────
            _aiDodgeCooldown -= Time.deltaTime;

            // ── Phase State Machine ──────────────────────────────────────────
            _aiPhaseTimer += Time.deltaTime;

            // Phase transitions — driven by distance, threat and random timing
            if (Time.time >= _aiNextPhaseChange && !_isAttacking)
            {
                SelectNextAIPhase(dist);
            }

            // ── Per-Phase Behaviour ──────────────────────────────────────────
            switch (_aiPhase)
            {
                case AICombatPhase.Approach:
                    AIPhaseApproach(dist, dirFlat);
                    break;

                case AICombatPhase.Circle:
                    AIPhaseCircle(dist, dirFlat);
                    break;

                case AICombatPhase.Pressure:
                    AIPhasePressure(dist, dirFlat);
                    break;

                case AICombatPhase.Blitz:
                    AIPhaseBlitz(dist, dirFlat);
                    break;

                case AICombatPhase.Retreat:
                    AIPhaseRetreat(dist, dirFlat);
                    break;
            }

            // ── Combo Chain Follow-Through ───────────────────────────────────
            // If a burst combo is running, chain more hits through combo windows
            if (_aiComboRunning && !_isAttacking)
            {
                if (_aiComboFired < _aiComboTarget)
                {
                    float timeSinceLast = Time.unscaledTime - _lastAttackTime;
                    if (timeSinceLast > minAttackInterval)
                    {
                        _aiComboFired++;
                        if (_aiComboFired == _aiComboTarget)
                            ExecuteHeavyAttack(Random.Range(1, 3), Vector3.zero);
                        else
                            StartLightAttack(Vector3.zero);
                    }
                }
                else
                {
                    _aiComboRunning = false;
                }
            }

            _aiWasAttacking = _isAttacking;
        }

        private void SelectNextAIPhase(float dist)
        {
            // Highly aggressive phase selector: priority is close combat and rapid combo strikes
            float r = Random.value;

            if (dist > 5.5f)
            {
                _aiPhase = AICombatPhase.Blitz;
                _aiNextPhaseChange = Time.time + Random.Range(0.8f, 1.4f);
            }
            else if (dist <= 3.2f)
            {
                // Close range → Alternate Blitz and relentless Pressure
                _aiPhase = r < 0.65f ? AICombatPhase.Blitz : AICombatPhase.Pressure;
                _aiNextPhaseChange = Time.time + Random.Range(0.5f, 1.1f);
            }
            else
            {
                // Mid range (3.2m - 5.5m) → Blitz sprint or rapid Pressure advance
                _aiPhase = r < 0.60f ? AICombatPhase.Blitz : AICombatPhase.Approach;
                _aiNextPhaseChange = Time.time + Random.Range(0.6f, 1.2f);
            }
        }

        // ── Approach: sprint/walk straight in until striking range ──────────
        private void AIPhaseApproach(float dist, Vector3 dirFlat)
        {
            float strikeRange = transform.localScale.x > 1.25f ? 3.0f : 2.2f;
            if (dist > strikeRange && !_isAttacking && !_aiComboRunning)
            {
                bool shouldRun = dist > 3.0f || _aiThreatMeter > 0.2f;
                _moveDirection = dirFlat * (shouldRun ? runSpeed : walkSpeed * 1.35f);
                _animator.SetFloat(HashMoveSpeed, shouldRun ? 2f : 1.2f, 0.1f, Time.deltaTime);

                // Mid-range tactical opener: Stalker Lunge (suppressive gun burst + dash + heavy lunge)
                if (dist >= 3.2f && dist <= 7.0f && Time.unscaledTime - _lastAttackTime > 1.0f)
                {
                    _moveDirection = Vector3.zero;
                    StartAIGunSwordCombo(AIGunSwordComboType.StalkerLunge);
                }
            }
            else
            {
                _moveDirection = Vector3.zero;
                _animator.SetFloat(HashMoveSpeed, 0f, 0.12f, Time.deltaTime);
                if (!_isAttacking && !_aiComboRunning && Time.unscaledTime - _lastAttackTime > 0.20f)
                {
                    float roll = Random.value;
                    if (roll < 0.40f)
                        StartAIGunSwordCombo(AIGunSwordComboType.ExecutionerShred);
                    else if (roll < 0.70f)
                        StartAIGunSwordCombo(AIGunSwordComboType.BlitzkriegOverdrive);
                    else
                        AITriggerComboBurst(2, 4);
                }
            }
        }

        // ── Circle: close-range aggressive strafe with rapid strike triggers ─
        private void AIPhaseCircle(float dist, Vector3 dirFlat)
        {
            if (_isAttacking || _aiComboRunning)
            {
                _moveDirection = Vector3.zero;
                return;
            }

            // Attack immediately if within range
            if (Time.unscaledTime - _lastAttackTime > 0.35f)
            {
                if (dist <= 3.2f)
                {
                    float roll = Random.value;
                    if (roll < 0.45f)
                        StartAIGunSwordCombo(AIGunSwordComboType.ExecutionerShred);
                    else if (roll < 0.80f)
                        StartAIGunSwordCombo(AIGunSwordComboType.BlitzkriegOverdrive);
                    else
                        AITriggerComboBurst(2, 4);
                    return;
                }
                else if (dist <= 7.0f)
                {
                    StartAIGunSwordCombo(AIGunSwordComboType.StalkerLunge);
                    return;
                }
            }

            _aiCircleAngle += _aiCircleDir * 110f * Time.deltaTime;
            float idealDist = 2.4f;
            Vector3 offset = new Vector3(Mathf.Sin(_aiCircleAngle * Mathf.Deg2Rad), 0f,
                                         Mathf.Cos(_aiCircleAngle * Mathf.Deg2Rad)) * idealDist;
            Vector3 desiredPos = targetEnemy.position + offset;
            Vector3 strafDir = (desiredPos - transform.position);
            strafDir.y = 0f;
            float strafMag = strafDir.magnitude;
            if (strafMag > 0.15f)
            {
                strafDir /= strafMag;
                _moveDirection = strafDir * walkSpeed * 1.3f;
                _animator.SetFloat(HashMoveSpeed, 1f, 0.12f, Time.deltaTime);
            }
            else
            {
                _moveDirection = Vector3.zero;
                _animator.SetFloat(HashMoveSpeed, 0f, 0.12f, Time.deltaTime);
            }

            // Feint — step in aggressively, then retreat
            _aiFeintTimer -= Time.deltaTime;
            if (_aiFeintTimer <= 0f && dist < 4f && Random.value < 0.25f)
            {
                _aiFeintTimer = Random.Range(1.8f, 3.5f);
                StartCoroutine(AIFeintRoutine());
            }
        }

        // ── Pressure: continuous attack pressure with combo chains ───────────
        private void AIPhasePressure(float dist, Vector3 dirFlat)
        {
            if (dist > 2.4f && !_isAttacking && !_aiComboRunning)
            {
                _moveDirection = dirFlat * walkSpeed * 1.5f;
                _animator.SetFloat(HashMoveSpeed, 1.5f, 0.1f, Time.deltaTime);
                return;
            }

            _moveDirection = Vector3.zero;
            _animator.SetFloat(HashMoveSpeed, 0f, 0.12f, Time.deltaTime);

            if (!_isAttacking && !_aiComboRunning)
            {
                float gap = Time.unscaledTime - _lastAttackTime;
                if (gap > 0.22f)
                {
                    float roll = Random.value;
                    if (roll < 0.40f)
                        StartAIGunSwordCombo(AIGunSwordComboType.ExecutionerShred);
                    else if (roll < 0.75f)
                        StartAIGunSwordCombo(AIGunSwordComboType.BlitzkriegOverdrive);
                    else
                        AITriggerComboBurst(2, 4);
                }
            }
        }

        // ── Blitz: full-speed sprint in and unleash maximum combo burst ───────
        private void AIPhaseBlitz(float dist, Vector3 dirFlat)
        {
            if (dist > 2.2f && !_isAttacking && !_aiComboRunning)
            {
                _moveDirection = dirFlat * runSpeed;
                _animator.SetFloat(HashMoveSpeed, 2f, 0.08f, Time.deltaTime);
                return;
            }

            _moveDirection = Vector3.zero;
            _animator.SetFloat(HashMoveSpeed, 0f, 0.1f, Time.deltaTime);

            if (!_isAttacking && !_aiComboRunning && Time.unscaledTime - _lastAttackTime > 0.20f)
            {
                float roll = Random.value;
                if (roll < 0.55f)
                    StartAIGunSwordCombo(AIGunSwordComboType.BlitzkriegOverdrive);
                else
                    StartAIGunSwordCombo(AIGunSwordComboType.ExecutionerShred);
                _aiThreatMeter = Mathf.Min(1f, _aiThreatMeter + 0.35f);
            }

            if (!_isAttacking && !_aiComboRunning)
            {
                _aiPhase = AICombatPhase.Retreat;
                _aiNextPhaseChange = Time.time + Random.Range(1.2f, 2.2f);
            }
        }

        // ── Retreat: back off to reset, possibly dodge ───────────────────────
        private void AIPhaseRetreat(float dist, Vector3 dirFlat)
        {
            if (_isAttacking || _aiComboRunning) return;

            if (dist < 5.5f)
            {
                _moveDirection = -dirFlat * walkSpeed * 1.15f;
                _animator.SetFloat(HashMoveSpeed, 1f, 0.12f, Time.deltaTime);
            }
            else
            {
                _moveDirection = Vector3.zero;
                _animator.SetFloat(HashMoveSpeed, 0f, 0.12f, Time.deltaTime);
            }

            if (_aiDodgeCooldown <= 0f && dist < 4.2f && Random.value < 0.45f * Time.deltaTime * 10f)
            {
                _aiDodgeCooldown = Random.Range(2.0f, 3.8f);
                Vector3 side = _aiCircleDir > 0f ? transform.right : -transform.right;
                ExecuteDodge(transform.InverseTransformDirection(side));
            }

            _aiThreatMeter = Mathf.Max(0f, _aiThreatMeter - 0.15f * Time.deltaTime);
        }

        // ── Initiate a timed combo burst of N hits ───────────────────────────
        private void AITriggerComboBurst(int minHits, int maxHits, bool forceHeavyFinish = false)
        {
            if (_isAttacking || _isDead) return;
            _aiComboTarget  = Random.Range(minHits, maxHits + 1);
            _aiComboFired   = 1;
            _aiComboRunning = true;
            _aiThreatMeter  = Mathf.Min(1f, _aiThreatMeter + 0.12f * _aiComboTarget);

            if (forceHeavyFinish && _aiComboTarget == 1)
                ExecuteHeavyAttack(Random.Range(1, 3), Vector3.zero);
            else
                StartLightAttack(Vector3.zero);
        }

        public void StartAIGunSwordCombo(AIGunSwordComboType combo)
        {
            if (_isAttacking || _isDead || targetEnemy == null) return;
            if (_activeAIComboRoutine != null) StopCoroutine(_activeAIComboRoutine);
            _activeAIComboRoutine = StartCoroutine(ExecuteAIGunSwordComboRoutine(combo));
        }

        private IEnumerator ExecuteAIGunSwordComboRoutine(AIGunSwordComboType combo)
        {
            activeAICombo = combo;
            _isAttacking = true;
            _aiComboRunning = true;
            _lastAttackTime = Time.unscaledTime;

            AlignToTargetEnemy();

            switch (combo)
            {
                case AIGunSwordComboType.ExecutionerShred:
                {
                    // Pure Sword Combo: Slash 1 -> Slash 2 -> Heavy Spin Cleave Finisher!
                    ExecuteLightAttack(1, Vector3.zero);
                    yield return new WaitForSeconds(0.28f);
                    if (_isDead) yield break;

                    AlignToTargetEnemy();
                    ExecuteLightAttack(2, Vector3.zero);
                    yield return new WaitForSeconds(0.32f);
                    if (_isDead) yield break;

                    AlignToTargetEnemy();
                    ExecuteHeavyAttack(2, Vector3.zero);
                    yield return new WaitForSeconds(0.48f);
                    break;
                }

                case AIGunSwordComboType.StalkerLunge:
                {
                    // Pure Sword Rush: High-Speed Dash Lunge -> Horizontal Slash -> Heavy Piercing Thrust!
                    AlignToTargetEnemy();
                    if (_ghostEmitter != null) _ghostEmitter.EmitBurst(0.22f, new Color(0.85f, 0.15f, 0.15f, 0.60f), 0.055f, 0.20f);
                    Vector3 dashDir = targetEnemy != null ? (targetEnemy.position - transform.position).normalized : transform.forward;
                    dashDir.y = 0f;
                    StartCoroutine(CombatStep(dashDir * 9f, 0.20f));
                    yield return new WaitForSeconds(0.16f);
                    if (_isDead) yield break;

                    AlignToTargetEnemy();
                    ExecuteLightAttack(3, Vector3.zero);
                    yield return new WaitForSeconds(0.30f);
                    if (_isDead) yield break;

                    AlignToTargetEnemy();
                    ExecuteHeavyAttack(1, Vector3.zero);
                    yield return new WaitForSeconds(0.50f);
                    break;
                }

                case AIGunSwordComboType.CycloneCrossfire:
                {
                    // Pure Blade Whirlwind: Heavy 360 Spin Cleave -> Double Crescent Slashes!
                    AlignToTargetEnemy();
                    ExecuteHeavyAttack(2, Vector3.zero);
                    yield return new WaitForSeconds(0.44f);
                    if (_isDead) yield break;

                    AlignToTargetEnemy();
                    ExecuteLightAttack(4, Vector3.zero);
                    yield return new WaitForSeconds(0.28f);
                    if (_isDead) yield break;

                    AlignToTargetEnemy();
                    ExecuteLightAttack(5, Vector3.zero);
                    yield return new WaitForSeconds(0.42f);
                    break;
                }

                case AIGunSwordComboType.ParryRiposte:
                {
                    // Sword Riposte: Counter Slash 1 -> Quick Follow-Up Slash 2!
                    AlignToTargetEnemy();
                    ExecuteLightAttack(1, Vector3.zero);
                    yield return new WaitForSeconds(0.25f);
                    if (_isDead) yield break;

                    AlignToTargetEnemy();
                    ExecuteLightAttack(2, Vector3.zero);
                    yield return new WaitForSeconds(0.35f);
                    break;
                }

                case AIGunSwordComboType.BlitzkriegOverdrive:
                {
                    // Relentless 4-Hit Sword Flurry: Light 1 -> Light 2 -> Light 3 -> Finisher Slash 7!
                    ExecuteLightAttack(1, Vector3.zero);
                    yield return new WaitForSeconds(0.26f);
                    if (_isDead) yield break;

                    AlignToTargetEnemy();
                    ExecuteLightAttack(2, Vector3.zero);
                    yield return new WaitForSeconds(0.28f);
                    if (_isDead) yield break;

                    AlignToTargetEnemy();
                    ExecuteLightAttack(3, Vector3.zero);
                    yield return new WaitForSeconds(0.28f);
                    if (_isDead) yield break;

                    AlignToTargetEnemy();
                    ExecuteLightAttack(7, Vector3.zero);
                    yield return new WaitForSeconds(0.48f);
                    break;
                }
            }

            _isAttacking = false;
            _aiComboRunning = false;
            activeAICombo = AIGunSwordComboType.None;
            EnemyGroupCombatCoordinator.Instance?.NotifyAttackFinished(this);
        }

        private void AlignToTargetEnemy()
        {
            if (targetEnemy == null) return;
            Vector3 toT = (targetEnemy.position - transform.position);
            toT.y = 0f;
            if (toT.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(toT.normalized);
        }

        // ── Feint: step in aggressively then retreat without attacking ────────
        private IEnumerator AIFeintRoutine()
        {
            if (_isAttacking || _isDead) yield break;
            // Quick step towards target
            Vector3 feintDir = targetEnemy != null
                ? (targetEnemy.position - transform.position).normalized
                : transform.forward;
            feintDir.y = 0f;
            float t = 0f;
            if (_animator != null) _animator.SetFloat(HashMoveSpeed, 1f, 0.08f, Time.deltaTime);
            while (t < 0.18f && !_isAttacking)
            {
                t += Time.deltaTime;
                if (_cc != null && _cc.enabled) _cc.Move(feintDir * 5f * Time.deltaTime);
                yield return null;
            }
            // Then back off sharply
            t = 0f;
            while (t < 0.22f && !_isAttacking)
            {
                t += Time.deltaTime;
                if (_cc != null && _cc.enabled) _cc.Move(-feintDir * 4.5f * Time.deltaTime);
                yield return null;
            }
            if (_animator != null) _animator.SetFloat(HashMoveSpeed, 0f, 0.1f, Time.deltaTime);
        }

        // ── Public hook: call when enemy successfully hits player ─────────────
        public void NotifyHitLanded()
        {
            _aiThreatMeter = Mathf.Min(1f, _aiThreatMeter + 0.18f);
        }

        private bool CheckGroundBelow()
        {
            if (_cc == null) return false;

            // SphereCast downwards from just above the base of the character
            Vector3 origin = transform.position + Vector3.up * 0.35f;
            float radius = Mathf.Max(0.12f, _cc.radius * 0.75f);
            float maxDist = groundCheckRadius + 0.35f;

            // Ensure Player layer is excluded from query
            int mask = groundLayers.value & ~(1 << gameObject.layer);

            int count = Physics.SphereCastNonAlloc(origin, radius, Vector3.down, _groundHitsBuffer, maxDist, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var col = _groundHitsBuffer[i].collider;
                if (col != null && !col.transform.IsChildOf(transform) && col.transform.root != transform.root)
                {
                    return true;
                }
            }

            // Fallback direct raycast below pivot
            RaycastHit hit;
            if (Physics.Raycast(transform.position + Vector3.up * 0.25f, Vector3.down, out hit, groundCheckRadius + 0.40f, mask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider != null && !hit.collider.transform.IsChildOf(transform) && hit.collider.transform.root != transform.root)
                {
                    return true;
                }
            }

            return false;
        }

        private void GroundCheck()
        {
            if (_cc == null) return;

            // If ascending from a jump, character is strictly in mid-air
            if (_verticalVelocity > 0.15f)
            {
                _isGrounded = false;
                return;
            }

            // Only check ground if falling or stationary
            bool hasGroundBelow = CheckGroundBelow();
            _isGrounded = (_cc.isGrounded && _verticalVelocity <= 0.05f) || hasGroundBelow;
        }

        private void ApplyMovementAndGravity()
        {
            // Cooldown timers
            if (_jumpCooldownTimer > 0f) _jumpCooldownTimer -= Time.deltaTime;

            if (_isGrounded)
            {
                _coyoteTimer = coyoteTimeDuration;

                // Handle landing from jump
                if (_isJumping && _verticalVelocity <= 0.05f && _airborneTime > 0.06f)
                {
                    _isJumping = false;
                    SafeSetBool(HashIsJumping, false);
                    AudioClip landingClip = CyberCombatAudioSynth.GetJumpLanding();
                    if (audioSource != null && landingClip != null)
                    {
                        audioSource.pitch = Random.Range(0.94f, 1.03f);
                        audioSource.PlayOneShot(landingClip, 0.95f);
                    }
                    else
                    {
                        CyberCombatAudioSynth.PlayAtPoint(landingClip, transform.position, 0.95f);
                    }
                }

                // Downward stick force while on ground to prevent bouncing down stairs or slopes
                if (_verticalVelocity < 0f && !_isJumping)
                {
                    _verticalVelocity = -4.5f;
                }

                _airborneTime = 0f;
            }
            else
            {
                _coyoteTimer -= Time.deltaTime;
                _airborneTime += Time.deltaTime;

                // Punchy action-game asymmetric gravity: downward fall accelerates 1.6x faster than upward climb
                float currentGravity = (_verticalVelocity < 0f) ? (gravity * fallGravityMultiplier) : gravity;
                _verticalVelocity -= currentGravity * Time.deltaTime;

                // Clamp terminal fall velocity
                if (_verticalVelocity < -42f) _verticalVelocity = -42f;
            }

            // STRICT SAFETY: if character is attacking or drawing/sheathing, activeMove is locked to zero
            Vector3 activeMove = (_isAttacking || _isDrawingOrSheathing || _isDodging) ? Vector3.zero : _moveDirection;
            Vector3 total = activeMove + _knockbackVelocity; total.y = _verticalVelocity;
            if (_cc != null && _cc.enabled && _cc.gameObject.activeInHierarchy)
            {
                CollisionFlags flags = _cc.Move(total * Time.deltaTime);
                if ((flags & CollisionFlags.Above) != 0 && _verticalVelocity > 0f)
                {
                    _verticalVelocity = -0.5f; // Bonk head and descend immediately, preventing clipping through ceilings
                }
            }
            _knockbackVelocity = Vector3.MoveTowards(_knockbackVelocity, Vector3.zero, 18f * Time.deltaTime);
        }

        // ── Jump System ──────────────────────────────────────────────────────
        public void TryJump(Vector3 input)
        {
            if (_isDrawingOrSheathing) CancelDrawSheatheEarly();
            bool canJump = (_isGrounded || _coyoteTimer > 0f) && !_isJumping && _jumpCooldownTimer <= 0f;
            if (!canJump || _isDodging || _isAttacking || _isDead) return;

            _isJumping = true;
            _isGrounded = false;
            _coyoteTimer = 0f;
            _airborneTime = 0f;
            _jumpCooldownTimer = jumpCooldown;
            _verticalVelocity = jumpForce;

            SafeSetTrigger(HashJumpTrigger);
            SafeSetBool(HashIsJumping, true);

            // Servo hop on takeoff; the armoured landing thud plays on ground contact
            AudioClip hopClip = CyberCombatAudioSynth.GetJumpTakeoff();
            if (audioSource != null && hopClip != null)
            {
                audioSource.pitch = Random.Range(0.96f, 1.05f);
                audioSource.PlayOneShot(hopClip, 0.6f);
            }
            else
            {
                CyberCombatAudioSynth.PlayAtPoint(hopClip, transform.position, 0.6f);
            }
        }

        // ── Sword Holster & Sheathe / Draw System ───────────────────────────
        public void ToggleSwordDraw()
        {
            if (_isDrawingOrSheathing || _isAttacking || _isDodging || _isDead) return;
            if (isSwordDrawn) SheatheSword();
            else DrawSword();
        }

        public void SheatheSword()
        {
            if (!isSwordDrawn || _isAttacking || _isDodging || _isDead) return;
            _targetSwordDrawn = false;
            if (_drawSheatheRoutine != null) StopCoroutine(_drawSheatheRoutine);
            _drawSheatheRoutine = StartCoroutine(SheatheSwordRoutine());
        }

        public void DrawSword(int queueAttackAfter = 0)
        {
            if (isSwordDrawn) return;
            _targetSwordDrawn = true;
            if (_drawSheatheRoutine != null) StopCoroutine(_drawSheatheRoutine);
            _drawSheatheRoutine = StartCoroutine(DrawSwordRoutine(queueAttackAfter));
        }

        public void CancelDrawSheatheEarly()
        {
            if (_drawSheatheRoutine != null)
            {
                StopCoroutine(_drawSheatheRoutine);
                _drawSheatheRoutine = null;
            }
            isSwordDrawn = _targetSwordDrawn;
            SetSwordParent(isSwordDrawn);
            SetSwordTrailEmitting(isSwordDrawn);
            SafeSetBool(HashIsSwordDrawn, isSwordDrawn);
            _isDrawingOrSheathing = false;
        }

        private IEnumerator SheatheSwordRoutine()
        {
            _isDrawingOrSheathing = true;
            _targetSwordDrawn = false;
            SafeSetTrigger(HashSheatheTrigger);
            CyberCombatAudioSynth.PlayAtPoint(CyberCombatAudioSynth.GetSwordSheathe(), transform.position, 0.85f);

            // Wait for hands to align blade with hip scabbard
            yield return new WaitForSeconds(0.38f);
            SetSwordParent(false);
            SetSwordTrailEmitting(false);
            isSwordDrawn = false;
            SafeSetBool(HashIsSwordDrawn, false);

            yield return new WaitForSeconds(0.45f);
            _isDrawingOrSheathing = false;
            _drawSheatheRoutine = null;
        }

        private IEnumerator DrawSwordRoutine(int queueAttackAfter)
        {
            _isDrawingOrSheathing = true;
            _targetSwordDrawn = true;
            SafeSetTrigger(HashDrawTrigger);
            CyberCombatAudioSynth.PlayAtPoint(CyberCombatAudioSynth.GetSwordDraw(), transform.position, 0.90f);

            // Wait for hand to grip hilt at hip and draw forward
            yield return new WaitForSeconds(0.28f);
            SetSwordParent(true);
            SetSwordTrailEmitting(true);
            isSwordDrawn = true;
            SafeSetBool(HashIsSwordDrawn, true);

            yield return new WaitForSeconds(0.24f);
            _isDrawingOrSheathing = false;
            _drawSheatheRoutine = null;

            if (queueAttackAfter == 1)
            {
                StartLightAttack(Vector3.zero);
            }
            else if (queueAttackAfter == 2)
            {
                ExecuteHeavyAttack(1, Vector3.zero);
            }
        }

        public void SetSwordParent(bool drawn)
        {
            if (swordInstance == null) return;
            DisableWeaponColliders(swordInstance.gameObject);
            if (drawn && rightHandSocket != null)
            {
                swordInstance.SetParent(rightHandSocket, false);
                swordInstance.localPosition = swordHandLocalPos;
                swordInstance.localRotation = Quaternion.Euler(swordHandLocalRot);
            }
            else if (!drawn && hipSocket != null)
            {
                swordInstance.SetParent(hipSocket, false);
                swordInstance.localPosition = swordHipLocalPos;
                swordInstance.localRotation = Quaternion.Euler(swordHipLocalRot);
            }
            DisableWeaponColliders(swordInstance.gameObject);
        }

        // ── Damage ──────────────────────────────────────────────────────────
        public void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitDirection, bool isHeavy)
        { TakeDamage(damage, hitPoint, hitDirection, isHeavy ? 3 : 0); }

        // ── AI Active Defense & Counters ──────────────────────────────────
        private bool HandleAIDefenseReaction(float damage, Vector3 hitPoint, Vector3 hitDirection, int ci)
        {
            if (_isDead || _isPoiseBroken || Time.time < _staggerEndTime) return false;

            // 1. Evasive Dodge Reaction (Chance to roll away from heavy attacks or combo finishers)
            if (ci >= 2 && Random.value < aiDodgeReactionChance && !_isDodging)
            {
                Vector3 dodgeSide = (Random.value < 0.5f ? transform.right : -transform.right);
                Vector3 dodgeDir = (Random.value < 0.35f ? -transform.forward : dodgeSide).normalized;
                ExecuteDodge(transform.InverseTransformDirection(dodgeDir));
                StartCoroutine(DelayedDodgeCounter());
                return true; // Completely evaded!
            }

            // 2. Deflection / Perfect Parry (Chance to cleanly parry player's strike and riposte)
            if (Random.value < aiParryChance && !_isAttacking)
            {
                SpawnParryFX(hitPoint);
                SmoothOrbitCamera.ShakeImpact(0.85f, -hitDirection * 0.35f);
                SmoothOrbitCamera.TriggerHitStop(0.08f);
                PlaySound(swordImpactSounds, 2);

                if (targetEnemy != null)
                {
                    var playerCtrl = targetEnemy.GetComponent<InsaneGunSwordController>();
                    if (playerCtrl != null) playerCtrl.TriggerImpactFeedback(2);
                }

                // AI instantly counter-attacks with vicious Parry Riposte Gun-Sword combo!
                StartAIGunSwordCombo(AIGunSwordComboType.ParryRiposte);
                return true; // Negates 100% damage and counters!
            }

            // 3. Active Block Reaction
            if (Random.value < aiBlockChance && !_isAttacking)
            {
                float mitigatedDamage = damage * (1f - aiBlockMitigation);
                currentHealth -= mitigatedDamage;
                currentPoise = Mathf.Max(0f, currentPoise - mitigatedDamage * 0.5f);
                _lastPoiseDamageTime = Time.time;
                OnDamaged?.Invoke(this, mitigatedDamage);

                SpawnHitEffect(hitPoint, false);
                CyberCombatAudioSynth.PlayAtPoint(CyberCombatAudioSynth.GetSwordImpact(), hitPoint, 0.9f);

                if (currentHealth <= 0f) { Die(hitPoint, hitDirection, ci); return true; }

                StartCoroutine(DelayedBlockCounter());
                return true; // Handled with heavy mitigation!
            }

            return false;
        }

        private IEnumerator DelayedDodgeCounter()
        {
            yield return new WaitForSeconds(0.24f);
            if (_isDead || targetEnemy == null) yield break;
            AlignToTargetEnemy();
            StartAIGunSwordCombo(AIGunSwordComboType.StalkerLunge);
        }

        private IEnumerator DelayedBlockCounter()
        {
            _isBlocking = true;
            SafeSetBool(HashIsBlocking, true);
            yield return new WaitForSeconds(0.20f);
            _isBlocking = false;
            SafeSetBool(HashIsBlocking, false);
            if (_isDead || targetEnemy == null) yield break;
            AlignToTargetEnemy();
            if (Random.value < 0.55f)
                StartAIGunSwordCombo(AIGunSwordComboType.ExecutionerShred);
            else
                StartAIGunSwordCombo(AIGunSwordComboType.CycloneCrossfire);
        }

        public void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitDirection, int ci)
        {
            // Pre-combat trial bots (M1): damage is impossible — whiff spark + toast,
            // no HP loss, no aggro, no death, no kill-tracker increment.
            if (isInvincible && IsTrialProtected != null && IsTrialProtected(this))
            {
                SpawnHitEffect(hitPoint, false);
                if (Time.time - _trialToastTime > 3f)
                {
                    _trialToastTime = Time.time;
                    var hud = SyntyCombined.CyberpunkHUDController.Instance;
                    if (hud != null) hud.ShowToast("HOLD // TRIAL NOT LIVE", 2.0f);
                }
                return;
            }
            if (_isDead || isInvincible) return;

            // Player active block / perfect parry
            if (_isBlocking && controlMode == ControlMode.PlayerControlled)
            {
                if (Time.time - _blockStartTime <= 0.22f) { StartCoroutine(ExecutePerfectParry(hitPoint, hitDirection)); return; }
                else { damage *= 0.20f; SpawnHitEffect(hitPoint, false); }
            }

            // AI intelligent active defense
            if (controlMode != ControlMode.PlayerControlled)
            {
                if (HandleAIDefenseReaction(damage, hitPoint, hitDirection, ci))
                {
                    return; // Defense reaction succeeded (parried, dodged, or mitigated block)
                }

                // Hyper Armor check: when executing devastating combo moves, AI absorbs hit with 50% damage reduction and NO interruption
                if (hasHyperArmor)
                {
                    damage *= 0.50f;
                    currentHealth -= damage;
                    OnDamaged?.Invoke(this, damage);
                    if (currentHealth <= 0f) { Die(hitPoint, hitDirection, ci); return; }

                    if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
                    _flashCoroutine = StartCoroutine(HitFlashRoutine());
                    SpawnHitEffect(hitPoint, true);
                    return; // Super Armor: powers through without flinching or interrupting combo!
                }

                // Poise buffer: enemy does not get stunlocked on light attacks
                _lastPoiseDamageTime = Time.time;
                currentPoise -= damage * (ci > 0 ? 1.6f : 1.0f);

                if (currentPoise > 0f && ci < 2)
                {
                    currentHealth -= damage;
                    OnDamaged?.Invoke(this, damage);
                    if (currentHealth <= 0f) { Die(hitPoint, hitDirection, ci); return; }

                    if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
                    _flashCoroutine = StartCoroutine(HitFlashRoutine());
                    SpawnHitEffect(hitPoint, false);
                    return; // Poise held: takes damage and flashes, but keeps pressing forward!
                }

                // Poise broken or heavy strike (ci >= 2): break poise and trigger stagger
                currentPoise = 0f;
                _isPoiseBroken = true;
                InterruptAttack();
            }

            currentHealth -= damage;
            OnDamaged?.Invoke(this, damage);
            if (currentHealth <= 0f) { Die(hitPoint, hitDirection, ci); return; }

            Vector3 pd = hitDirection != Vector3.zero ? hitDirection : (transform.position - hitPoint);
            pd.y = 0; if (pd.sqrMagnitude < 0.001f) pd = -transform.forward; pd.Normalize();

            // Face attacker on impact for physical sync
            Vector3 faceAttacker = -pd;
            if (faceAttacker.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(faceAttacker.normalized);

            // EUPHORIA RAGDOLL REACTION:
            // Heavy Finisher (ci == 3) launches target into Rockstar-style full Euphoria Ragdoll!
            var ragdoll = GetComponent<EuphoriaRagdollController>();
            if (ragdoll != null && ci >= 3 && controlMode != ControlMode.PlayerControlled)
            {
                ragdoll.TriggerEuphoriaRagdoll(hitPoint, pd, 16f, isFatal: false);
                SmoothOrbitCamera.ShakeImpact(0.85f, -pd * 0.30f);
                SmoothOrbitCamera.TriggerHitStop(0.065f);
                PlaySound(swordImpactSounds, 2);
                if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
                _flashCoroutine = StartCoroutine(HitFlashRoutine());
                return;
            }

            Vector3 loc2 = transform.InverseTransformDirection(pd); float ax2 = Mathf.Abs(loc2.x), az2 = Mathf.Abs(loc2.z);
            int hd = az2 >= ax2 ? (loc2.z >= 0f ? 0 : 1) : (loc2.x > 0f ? 3 : 2);
            // Alternate between body hit (0) and head snap (5) during combo chains
            int ht = ci > 0 && controlMode != ControlMode.PlayerControlled ? ci : ci > 0 ? 1 : (_comboIndex % 2 == 0 ? 5 : 0);
            float kbf = 0.9f;
            if (ci == 1) kbf = 4.5f; else if (ci == 2) kbf = 7.5f; else if (ci >= 3) kbf = 13.5f; else if (ht == 1) kbf = 4f;
            _knockbackVelocity = pd * kbf;

            if (controlMode != ControlMode.PlayerControlled)
            {
                _staggerEndTime = Time.time + (ci > 0 ? 0.75f : 0.35f);
                SafeSetInteger(HashHitDir, hd); SafeSetInteger(HashHitType, ht); SafeSetTrigger(HashHitTrigger);
            }
            PlaySound(swordImpactSounds);
            if (controlMode == ControlMode.PlayerControlled)
            { float tr = ci > 0 ? 0.75f : 0.40f; SmoothOrbitCamera.ShakeImpact(tr, -pd * (ci > 0 ? 0.25f : 0.12f)); SmoothOrbitCamera.TriggerHitStop(ci > 0 ? 0.045f : 0.025f); }
            if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
            _flashCoroutine = StartCoroutine(HitFlashRoutine());
        }

        private IEnumerator HitFlashRoutine()
        {
            if (_renderers == null || _renderers.Length == 0) yield break;
            if (_propBlock == null) _propBlock = new MaterialPropertyBlock();
            Color fc = new Color(1.5f, 0.4f, 0.4f, 1f);
            for (int i = 0; i < _renderers.Length; i++) { if (_renderers[i] != null) { _renderers[i].GetPropertyBlock(_propBlock); _propBlock.SetColor("_EmissionColor", fc); _renderers[i].SetPropertyBlock(_propBlock); } }
            yield return new WaitForSeconds(0.08f);
            for (int i = 0; i < _renderers.Length; i++) { if (_renderers[i] != null) { _renderers[i].GetPropertyBlock(_propBlock); _propBlock.SetColor("_EmissionColor", Color.black); _renderers[i].SetPropertyBlock(_propBlock); } }
        }

        [Header("Respawn & Sandbox Testing")]
        public bool autoReviveOnDeath = false;
        public float autoReviveDelay  = 2.5f;

        private void Die(Vector3 hitPoint = default, Vector3 hitDirection = default, int ci = 0)
        {
            if (_customAnimationRoutine != null) { StopCoroutine(_customAnimationRoutine); _customAnimationRoutine = null; }
            if (_lightAttackRoutine != null) { StopCoroutine(_lightAttackRoutine); _lightAttackRoutine = null; }
            if (_heavyAttackRoutine != null) { StopCoroutine(_heavyAttackRoutine); _heavyAttackRoutine = null; }
            if (_gunAttackRoutine != null)   { StopCoroutine(_gunAttackRoutine);   _gunAttackRoutine   = null; }
            if (_drawSheatheRoutine != null) { StopCoroutine(_drawSheatheRoutine); _drawSheatheRoutine = null; }
            _isDrawingOrSheathing = false;
            _isJumping = false;
            SafeSetBool(HashIsJumping, false);
            SetSwordTrailEmitting(false);
            _isDead = true;
            EnemyGroupCombatCoordinator.Instance?.UnregisterCombatant(this);
            OnDeath?.Invoke(this);

            Vector3 fatalDir = hitDirection != Vector3.zero ? hitDirection : transform.forward;
            Vector3 fatalPoint = hitPoint != default ? hitPoint : (transform.position + Vector3.up * 1.1f);

            // Rockstar / Euphoria Style Ragdoll Death — Guaranteed ragdoll on Health 0
            var ragdoll = GetComponent<EuphoriaRagdollController>();
            if (ragdoll == null) ragdoll = gameObject.AddComponent<EuphoriaRagdollController>();
            ragdoll.TriggerEuphoriaRagdoll(fatalPoint, fatalDir, 18f, isFatal: true);

            if (controlMode == ControlMode.PlayerControlled)
            { SmoothOrbitCamera.ShakeImpact(0.95f, Vector3.down * 0.35f); SmoothOrbitCamera.TriggerHitStop(0.12f, 0.02f); }
            else { SmoothOrbitCamera.ShakeImpact(0.65f); SmoothOrbitCamera.TriggerHitStop(0.06f); }
            if (autoReviveOnDeath && gameObject.activeInHierarchy) StartCoroutine(AutoReviveRoutine());
        }

        private IEnumerator AutoReviveRoutine()
        {
            yield return new WaitForSeconds(autoReviveDelay);
            Revive();
        }

        public void Revive(Vector3? spawnPosition = null)
        {
            _isDead = false;
            currentHealth = (controlMode != ControlMode.PlayerControlled) ? aiMaxHealth : maxHealth;
            currentPoise = maxPoise;
            _isPoiseBroken = false;
            hasHyperArmor = false;
            if (_customAnimationRoutine != null) { StopCoroutine(_customAnimationRoutine); _customAnimationRoutine = null; }
            _knockbackVelocity = Vector3.zero;
            _moveDirection = Vector3.zero;
            _isAttacking = false;
            _isDodging = false;
            _isBlocking = false;
            _isJumping = false;
            _isDrawingOrSheathing = false;
            _verticalVelocity = 0f;
            SafeSetBool(HashIsJumping, false);
            _comboIndex = 0;
            _heavyComboIndex = 0;
            _comboWindowOpen = false;
            _comboQueued = false;
            _staggerEndTime = 0f;

            var ragdoll = GetComponent<EuphoriaRagdollController>();
            if (ragdoll != null && ragdoll.IsRagdoll)
            {
                ragdoll.RecoverToCombat(spawnPosition);
                return;
            }

            if (_cc != null)
            {
                _cc.enabled = false;
                if (spawnPosition.HasValue) transform.position = spawnPosition.Value;
                _cc.enabled = true;
            }
            else if (spawnPosition.HasValue)
            {
                transform.position = spawnPosition.Value;
            }
            if (_animator != null)
            {
                _animator.Rebind();
                _animator.Update(0f);
                _animator.Play("Locomotion", 0, 0f);
            }
        }

        public void OnKnockdownRecovered(Vector3 pos)
        {
            _isAttacking = false;
            _isDodging = false;
            _knockbackVelocity = Vector3.zero;
            _moveDirection = Vector3.zero;
            _staggerEndTime = 0f;
            if (_cc != null)
            {
                _cc.enabled = false;
                transform.position = pos;
                _cc.enabled = true;
            }
            else
            {
                transform.position = pos;
            }
        }

        private void ToggleLockOn()
        {
            if (lockedTarget != null) { lockedTarget = null; if (SmoothOrbitCamera.Instance != null) SmoothOrbitCamera.Instance.targetOffset = new Vector3(0f, 1.4f, 0f); return; }
            lockedTarget = FindBestLungeTarget(18f, 90f);
        }

        private Transform FindBestLungeTarget(float mr = 8.5f, float ma = 120f)
        {
            Collider[] cands = Physics.OverlapSphere(transform.position, mr, enemyLayers);
            Transform best = null; float cd = float.MaxValue;
            Vector3 fwd = _mainCamera != null ? _mainCamera.transform.forward : transform.forward;
            fwd.y = 0f; if (fwd.sqrMagnitude < 0.001f) fwd = transform.forward; fwd.Normalize();
            foreach (var col in cands)
            {
                if (col.gameObject == gameObject || col.transform.root == transform.root) continue;
                var dmg = col.GetComponentInParent<SyntyCombined.ISyntyDamageable>() ?? col.GetComponent<SyntyCombined.ISyntyDamageable>();
                var ins = col.GetComponentInParent<InsaneGunSwordController>() ?? col.GetComponent<InsaneGunSwordController>();
                if (dmg == null && ins == null) continue;
                if (dmg == (SyntyCombined.ISyntyDamageable)this || ins == this) continue;
                var npc = col.GetComponentInParent<PolygonCyberCity.NPC.Spawner.CyberNPCController>() ?? col.GetComponent<PolygonCyberCity.NPC.Spawner.CyberNPCController>();
                if (npc != null && npc.IsDead) continue; if (ins != null && ins.IsDead) continue;
                Vector3 td = col.transform.position - transform.position; td.y = 0f; float dist = td.magnitude; if (dist < 0.2f) continue;
                float camAngle = Vector3.Angle(fwd, td);
                float charAngle = Vector3.Angle(transform.forward, td);
                if ((camAngle <= ma || charAngle <= ma) && dist < cd) { cd = dist; best = col.transform; }
            }
            if (best == null && targetEnemy != null)
            {
                Vector3 td = targetEnemy.position - transform.position; td.y = 0f;
                if (td.sqrMagnitude <= mr * mr) best = targetEnemy;
            }
            return best;
        }

        private void SpawnParryFX(Vector3 pos)
        {
            GameObject g = new GameObject("Parry_FX"); g.transform.position = pos;
            var ps = g.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main; m.duration = 0.20f; m.startLifetime = 0.25f; m.startSpeed = 12f; m.startSize = 0.18f;
            m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f,0.9f,0.2f), Color.white); m.stopAction = ParticleSystemStopAction.Destroy;
            ps.emission.SetBursts(new ParticleSystem.Burst[]{ new ParticleSystem.Burst(0f, 36) });
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.15f;
            ps.Play(); Destroy(g, 0.5f);
        }

        private void SpawnHitEffect(Vector3 pos, bool heavy = false)
        {
            GameObject g = new GameObject("HitSpark_FX"); g.transform.position = pos;
            var ps = g.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main; m.duration = 0.15f; m.startLifetime = 0.18f; m.startSpeed = heavy ? 8.5f : 6f; m.startSize = heavy ? 0.14f : 0.09f;
            Color sc = heavy ? new Color(1f,0.2f,0.8f) : new Color(0f,0.95f,1f);
            m.startColor = new ParticleSystem.MinMaxGradient(sc, new Color(1f,0.9f,0.4f)); m.stopAction = ParticleSystemStopAction.Destroy;
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World; vel.radial = new ParticleSystem.MinMaxCurve(heavy ? 6f : 4f);
            ps.emission.SetBursts(new ParticleSystem.Burst[]{ new ParticleSystem.Burst(0f, heavy ? 36 : 20) });
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.10f;
            ps.Play(); SafeDestroy(g, 0.40f);
        }

        private void SpawnSlashArcFX(Vector3 pos, Quaternion rot, bool heavy)
        {
            GameObject g = new GameObject("SlashArc_FX"); g.transform.position = pos; g.transform.rotation = rot;
            var ps = g.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main; m.duration = 0.12f; m.startLifetime = new ParticleSystem.MinMaxCurve(0.10f,0.16f);
            m.startSpeed = heavy ? 10f : 7f; m.startSize = heavy ? 1.6f : 1.1f;
            Color ac = heavy ? new Color(1f,0.2f,0.8f) : new Color(0f,0.95f,1f);
            m.startColor = new ParticleSystem.MinMaxGradient(ac, Color.white); m.stopAction = ParticleSystemStopAction.Destroy; m.simulationSpace = ParticleSystemSimulationSpace.World;
            ps.emission.SetBursts(new ParticleSystem.Burst[]{ new ParticleSystem.Burst(0f, heavy ? 24 : 14) });
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 30f; sh.radius = 0.05f;
            var col = ps.colorOverLifetime; col.enabled = true;
            Gradient gr = new Gradient();
            gr.SetKeys(new GradientColorKey[]{ new GradientColorKey(ac,0f), new GradientColorKey(Color.white,1f) }, new GradientAlphaKey[]{ new GradientAlphaKey(1f,0f), new GradientAlphaKey(0f,1f) });
            col.color = new ParticleSystem.MinMaxGradient(gr);
            ps.Play(); SafeDestroy(g, 0.30f);
        }

        private void SpawnLaserTracer(Vector3 start, Vector3 end)
        {
            GameObject g = new GameObject("LaserTracer_FX");
            var ln = g.AddComponent<LineRenderer>();
            ln.startWidth = 0.08f; ln.endWidth = 0.02f; ln.material = new Material(Shader.Find("Sprites/Default"));
            ln.startColor = new Color(0f,0.95f,1f,0.95f); ln.endColor = new Color(1f,0.20f,0.8f,0.30f);
            ln.SetPosition(0, start); ln.SetPosition(1, end);
            SafeDestroy(g, 0.08f);
        }

        private static void SafeDestroy(GameObject go, float delay = 0f)
        {
            if (go == null) return;
            if (Application.isPlaying) Destroy(go, delay);
            else DestroyImmediate(go);
        }

        private void PlaySound(AudioClip[] clips, int ci = 0)
        {
            if (audioSource == null) return;
            if (clips != null && clips.Length > 0)
            {
                var c = clips[Random.Range(0, clips.Length)];
                if (c != null) { audioSource.PlayOneShot(c); return; }
            }
            AudioClip fallback = null;
            if      (clips == swordSwingSounds)  fallback = CyberCombat.Audio.CyberCombatAudioSynth.GetSwordSlash(ci > 0 ? ci : 1, ci >= 3);
            else if (clips == swordImpactSounds) fallback = ci > 0 ? CyberCombat.Audio.CyberCombatAudioSynth.GetHeavyImpact() : CyberCombat.Audio.CyberCombatAudioSynth.GetSwordImpact();
            else if (clips == gunShotSounds)     fallback = CyberCombat.Audio.CyberCombatAudioSynth.GetGunShot();
            else if (clips == dodgeSounds || clips == dashSounds) fallback = CyberCombat.Audio.CyberCombatAudioSynth.GetDodgeWhoosh();
            if (fallback != null) audioSource.PlayOneShot(fallback);
        }

        private void PlaySwordSlash(int comboStep = 1, bool isHeavy = false)
        {
            if (audioSource == null) return;
            if (swordSwingSounds != null && swordSwingSounds.Length > 0)
            {
                var c = swordSwingSounds[Random.Range(0, swordSwingSounds.Length)];
                if (c != null)
                {
                    audioSource.pitch = Random.Range(0.96f, 1.05f);
                    audioSource.PlayOneShot(c, isHeavy ? 1.0f : 0.85f);
                    return;
                }
            }
            AudioClip clip = CyberCombatAudioSynth.GetSwordSlash(comboStep, isHeavy);
            if (clip != null)
            {
                audioSource.pitch = Random.Range(0.97f, 1.05f);
                audioSource.PlayOneShot(clip, isHeavy ? 1.0f : 0.88f);
            }
        }

        [Header("HUD Display")]
        public bool showCombatHUD = false;

        private void OnGUI()
        {
            if (!showCombatHUD) return;
            if (SyntyCombined.InGamePauseManager.IsPaused) return;
            if (controlMode != ControlMode.PlayerControlled)
            {
                if (_isDead) return; if (_mainCamera == null) _mainCamera = Camera.main; if (_mainCamera == null) return;
                Vector3 sp = _mainCamera.WorldToScreenPoint(transform.position + Vector3.up * 2.05f);
                if (sp.z > 0.5f && sp.z < 28f)
                {
                    float bw = 75f, bh = 7f, x = sp.x - bw*0.5f, y = Screen.height - sp.y;
                    GUI.color = new Color(0.08f,0.08f,0.08f,0.85f); GUI.DrawTexture(new Rect(x-2,y-2,bw+4,bh+4), Texture2D.whiteTexture);
                    float hp = HealthPercent; GUI.color = Color.Lerp(new Color(1f,0.2f,0.2f), new Color(0.2f,1f,0.4f), hp);
                    GUI.DrawTexture(new Rect(x, y, bw*hp, bh), Texture2D.whiteTexture); GUI.color = Color.white;
                }
                return;
            }

            // Player Immediate-Mode OnGUI box ("INSANEGUN SWORD & GUN v3.0") permanently removed.
            // All health and combat diagnostics are rendered through the AAA Cyberpunk 2077 HUD.
            if (lockedTarget != null && lockedTarget.gameObject != null && _mainCamera != null)
            {
                Vector3 sp = _mainCamera.WorldToScreenPoint(lockedTarget.position + Vector3.up * 1.2f);
                if (sp.z > 0) { float s = 32f, lx = sp.x-s*0.5f, ly = Screen.height-sp.y-s*0.5f; GUI.color = new Color(0f,0.95f,1f,0.9f); GUI.Box(new Rect(lx,ly,s,s),"O"); GUI.color = Color.white; }
            }
        }

        private void OnDrawGizmosSelected()
        {
            var cc = GetComponent<CharacterController>(); if (cc == null) return;
            float half = cc.height * 0.5f, iso = half - cc.radius;
            Vector3 feet = transform.position + new Vector3(0f, cc.center.y - iso, 0f);
            Gizmos.color = _isGrounded ? Color.green : Color.red; Gizmos.DrawWireSphere(feet, cc.radius + groundCheckRadius);
            Gizmos.color = Color.yellow; Gizmos.DrawSphere(transform.position + new Vector3(0f, cc.center.y - half, 0f), 0.05f);
            if (_isAttacking) { Gizmos.color = new Color(1f,0f,0.5f,0.35f); Gizmos.DrawSphere(transform.position + transform.forward * 1.4f + Vector3.up, 1.35f); }
        }
    }
}
