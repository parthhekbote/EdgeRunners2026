using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SyntyCombined
{
    // ─────────────────────────────────────────────────────────────────────────────
    // SyntySwordLocomotionController.cs  –  v2.0
    //
    // Combines Synty Animation Base Locomotion (Feminine) with Synty Sword Combat.
    //
    // Architecture:
    //   • Three-layer Animator Controller
    //       Layer 0  –  Locomotion            (full-body blend trees, idle sword blend, crouch, jump)
    //       Layer 1  –  UpperBody_SwordStance (arm-masked ready stance while moving with sword)
    //       Layer 2  –  Combat_Actions        (full-body override for attacks, dodges, parries, reactions)
    //   • All clip references live in SyntyAnimClipData ScriptableObject
    //   • Clip durations read from AnimationClip.length (never hardcoded)
    //   • CrossFadeInFixedTime drives Animator states matching exact clip names
    //   • Natural forward step momentum on attack swings
    //   • Directional dodge & dodge rolls from move-input vector
    //   • Prop_R Synty bone auto-detection for perfect weapon grip
    //   • Sheath attachment on hip with frame-exact draw/sheathe swaps
    //   • Smooth camera orbit with collision SphereCast & scroll zoom
    //   • On-screen cyberpunk combat HUD (toggle with H)
    // ─────────────────────────────────────────────────────────────────────────────

    [RequireComponent(typeof(CharacterController))]
    public class SyntySwordLocomotionController : MonoBehaviour, ISyntyDamageable
    {
        // ─── Inspector ────────────────────────────────────────────────────────────
        #region Inspector

        [Header("Combat & Health Stats")]
        public float maxHealth = 150f;
        public float currentHealth = 150f;
        [Tooltip("When enabled, the player cannot take damage or die.")]
        public bool isInvincible = false;
        [Tooltip("When true, player movement, combat, and camera input are frozen.")]
        public bool inputLocked = false;
        [Range(0f, 1f)] public float blockMitigation = 0.85f;
        public System.Action<float, float> OnPlayerHealthChanged;
        public System.Action<SyntySwordLocomotionController, float> OnDamaged;
        public System.Action<SyntySwordLocomotionController> OnDeath;

        [Header("References")]
        public Transform    characterMesh;
        public Animator     animator;
        [Tooltip("All animation clip references; assign the SyntyAnimClipData asset.")]
        public SyntyAnimClipData clips;

        [Header("Weapon Sockets")]
        [Tooltip("Local-space position offset if Prop_R bone is not present.")]
        public Vector3 handSwordOffset   = new Vector3(0.08f, 0.01f, -0.01f);
        public Vector3 handSwordRotation = new Vector3(0f, 180f, 90f);
        public Vector3 hipSwordOffset    = new Vector3(0.18f, -0.05f, -0.08f);
        public Vector3 hipSwordRotation  = new Vector3(-10f, 15f, 110f);

        [Header("Weapons")]
        public GameObject swordPrefab;
        public GameObject sheathPrefab;
        public Mesh       swordMesh;
        public Material   swordMaterial;
        public Vector3    swordScale = Vector3.one;

        [Header("Camera")]
        public Camera    playerCamera;
        public Transform cameraTarget;
        public float cameraDistance    = 4.5f;
        public float cameraMinDist     = 1.5f;
        public float cameraMaxDist     = 12f;
        public float cameraZoomSpeed   = 4f;
        public float cameraZoomSmooth  = 10f;
        [Range(-45f, 0f)]  public float camMinPitch = -20f;
        [Range(10f,  80f)] public float camMaxPitch =  55f;
        public float cameraSensitivity = 180f;
        public float cameraLagSpeed    =  14f;
        public LayerMask cameraCollisionMask;

        [Header("First Person View")]
        [Tooltip("When enabled, camera is positioned at eye level with direct mouse look.")]
        public bool firstPersonView = false;
        [Tooltip("Offset relative to character/cameraTarget for eye position in first person.")]
        public Vector3 firstPersonEyeOffset = new Vector3(0f, 0.15f, 0.12f);
        [Tooltip("Key to toggle between First Person and Third Person.")]
        public KeyCode togglePerspectiveKey = KeyCode.V;

        [Header("Locomotion")]
        public float walkSpeed     = 1.40f;
        public float runSpeed      = 2.50f;
        public float sprintSpeed   = 7.00f;
        public float crouchSpeed   = 1.10f;
        public float blockMoveSpeed = 1.20f;
        public float acceleration  = 14f;
        public float deceleration  = 18f;
        public float rotationSpeed  = 12f;
        public float gravity        = -19.6f;
        [Tooltip("When true, character strafes facing camera when sword is drawn.")]
        public bool  combatStrafeMode = true;

        [Header("Jump")]
        public float jumpForce  = 7.5f;
        [Range(0.1f, 1f)]
        public float airControl = 0.85f;

        [Header("Capsule")]
        public float standHeight   = 1.80f;
        public float crouchHeight  = 1.20f;
        public float capsuleRadius = 0.28f;

        [Header("Combat Tuning")]
        [Tooltip("Minimum seconds between consecutive attack triggers.")]
        public float actionCooldown         = 0.08f;
        public float blockLockoutAfterAttack = 0.25f;
        public float combatLayerFadeSpeed    = 12f;

        [Header("Animator Layer Indices (Synty 4-Layer System)")]
        public int locomotionLayerIndex = 0;
        public int swordArmLayerIndex    = 1;
        public int swordHandLayerIndex   = 2;
        public int combatLayerIndex      = 3;

        [Header("HUD & Debug")]
        public bool showCombatHUD = false;

        #endregion

        // ─── Private State ────────────────────────────────────────────────────────
        #region Private State

        private CharacterController _cc;

        // Cached animator parameter hashes
        private int _hMoveSpeed, _hCurrentGait, _hIsWalking;
        private int _hIsCrouching, _hIsGrounded, _hIsJumping;
        private int _hStrafeX, _hStrafeZ, _hIsStrafing;
        private int _hLeanValue, _hFallingDuration;
        private int _hMovementHeld, _hMovementTapped, _hIsStopped, _hIsStarting;
        private int _hSwordDrawn, _hSwordDrawnWeight, _hIsSliding;
        private int _hCombatState, _hAttackID, _hDodgeDirection, _hHitDirection;

        // Layer weights
        private float _combatLayerWeight     = 0f;
        private float _swordArmLayerWeight   = 0f;
        private float _swordHandLayerWeight  = 0f;

        // Camera
        private float   _camYaw, _camPitch;
        private float   _camDistTarget, _camDistCurrent;
        private Vector3 _smoothCamPos;
        private bool    _camInit;
        private Transform _headBone;

        // Input
        private Vector2 _moveInput;
        private Vector2 _lookInput;
        private float   _scrollInput;
        private bool    _sprintHeld;
        private bool    _crouching;
        private bool    _blockHeld;
        private bool    _isWalkToggle;

        // Physics
        private float _moveSpeed;
        private float _verticalVel;
        private bool  _isJumping;
        private float _groundGrace;
        private float _fallingTime;

        // Slide
        [Header("Locomotion – Slide")]
        [Tooltip("Duration of the sprint slide in seconds.")]
        public float slideDuration = 0.85f;
        [Tooltip("Initial impulse multiplier on sprintSpeed when entering slide.")]
        public float slideSpeedMultiplier = 1.35f;
        private bool    _isSliding;
        private float   _slideTimer;
        private float   _slideSpeed;
        private Vector3 _slideDir = Vector3.forward;

        // Impulses
        private float   _attackStepSpeed   = 0f;
        private string  _currentAttackName = "IDLE";
        private float   _dodgeTimer        = 0f;
        private float   _dodgeSpeed        = 0f;
        private Vector3 _dodgeWorldDir     = Vector3.forward;

        // Animator smoothing
        private float _animSpeed    = 1f;
        private float _animSpeedVel = 0f;

        // Weapon
        private bool       _swordDrawn;
        private float      _swordDrawnWeight;
        private Transform  _handSocket, _hipSocket;
        private GameObject _sword;
        private GameObject _sheath;

        // Combat state machine
        public enum CombatState
        {
            None, LightAttack, HeavyAttack, SpecialAttack,
            Block, Parry, Dodge, Draw, Sheathe, HitReact, KnockDown, Stun, Death
        }
        private CombatState _combatState    = CombatState.None;
        private float       _actionTimer    = 0f;
        private float       _actionCooldownTimer = 0f;
        private bool        _weaponSwapDone = false;
        private bool        _exitingCombat  = false;

        // Multi-phase state tracking
        private KnockdownPhase _knockdownPhase     = KnockdownPhase.Begin;
        private float          _knockdownLoopTimer = 0f;
        private StunPhase      _stunPhase          = StunPhase.Begin;
        private float          _stunLoopTimer      = 0f;

        // Active clip timing
        private float _activeClipDuration = 1f;
        private float _activeComboWindow  = 0.5f;
        private float _activeExitWindow   = 0.8f;
        private int   _activeStateHash    = 0;

        // Combo tracking
        private int   _lightComboStep  = 0;
        private int   _heavyComboStep  = 0;
        private float _comboExpiry     = 0f;
        private const float ComboWindowSec = 1.3f;

        // Queued input buffer (max 1 hit)
        private enum QueuedInput { None, LightAttack, HeavyAttack }
        private QueuedInput _queuedInput = QueuedInput.None;

        // Normalised time sword swap
        private float _weaponSwapNormTime = -1f;

        // Combat lockout timer
        private float _blockLockoutTimer       = 0f;
        private float _lastHitReactTime        = -10f;

        // Hit detection
        private bool _hitProcessedForCurrentAttack = false;
        private readonly Collider[] _hitResults = new Collider[16];

        // Strafe calculation
        private float _strafeDirectionX = 0f;
        private float _strafeDirectionZ = 0f;

        // Public getters for Cyberpunk HUD integration
        public bool IsSwordDrawn => _swordDrawn;
        public CombatState CurrentCombatState => _combatState;
        public string CurrentAttackName => _currentAttackName;
        public float CurrentMoveSpeed => _moveSpeed;
        public bool IsCrouchingState => _crouching;
        public bool IsSprintingState => _sprintHeld;
        public bool IsWalkingToggle => _isWalkToggle;
        public bool IsGroundedState => _cc != null && _cc.isGrounded;

        /// <summary>
        /// Teleports the player to a target position and resets falling/vertical momentum.
        /// Safely toggles CharacterController to bypass physics tunneling and position locks.
        /// </summary>
        public void Teleport(Vector3 position, Quaternion? rotation = null)
        {
            if (_cc != null) _cc.enabled = false;
            transform.position = position;
            if (rotation.HasValue)
            {
                transform.rotation = rotation.Value;
                _camYaw = rotation.Value.eulerAngles.y;
            }
            _verticalVel = 0f;
            _fallingTime = 0f;
            _isJumping = false;
            _groundGrace = 0.2f;
            if (_cc != null) _cc.enabled = true;
        }

        #endregion

        // ─── Unity Lifecycle ──────────────────────────────────────────────────────
        #region Lifecycle

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            if (_cc != null)
            {
                _cc.height          = standHeight;
                _cc.radius          = capsuleRadius;
                _cc.center          = new Vector3(0f, standHeight * 0.5f, 0f);
                _cc.minMoveDistance = 0.001f;
                _cc.skinWidth       = 0.008f;
            }

            // Resolve animator
            if (animator == null && characterMesh != null)
                animator = characterMesh.GetComponent<Animator>();
            if (animator == null)
                animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
            if (animator != null) animator.applyRootMotion = false;

            // Auto-load controller & data if not assigned
            #if UNITY_EDITOR
            if (animator != null && animator.runtimeAnimatorController == null)
                animator.runtimeAnimatorController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/SyntyCombined/AC_SyntyCombined.controller");
            if (clips == null)
                clips = UnityEditor.AssetDatabase.LoadAssetAtPath<SyntyAnimClipData>("Assets/SyntyCombined/SyntyAnimClipData.asset");
            #endif

            // Cache parameter hashes
            if (clips != null) CacheParamHashes();

            // Camera setup
            if (playerCamera == null) playerCamera = Camera.main;
            if (cameraTarget == null)
            {
                Transform found = transform.Find("CameraTarget");
                if (found != null)
                {
                    cameraTarget = found;
                }
                else
                {
                    var go = new GameObject("CameraTarget");
                    go.transform.SetParent(transform);
                    go.transform.localPosition = new Vector3(0f, 1.45f, 0f);
                    cameraTarget = go.transform;
                }
            }

            if (animator != null)
            {
                _headBone = animator.GetBoneTransform(HumanBodyBones.Head);
            }

            SetupCameraPostProcessing();

            _camYaw         = transform.eulerAngles.y;
            _camPitch       = firstPersonView ? 0f : 20f;
            _camDistTarget  = cameraDistance;
            _camDistCurrent = cameraDistance;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
        }

        private void SetupCameraPostProcessing()
        {
            Camera cam = playerCamera != null ? playerCamera : Camera.main;
            if (cam == null) return;

            cam.allowHDR = true;
            cam.fieldOfView = firstPersonView ? 75f : 60f;
            cam.nearClipPlane = 0.03f;

            var uac = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (uac == null) uac = cam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            uac.renderPostProcessing = true;
            uac.volumeLayerMask = ~0; // Everything
            uac.volumeTrigger = cam.transform;
            uac.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            uac.antialiasingQuality = UnityEngine.Rendering.Universal.AntialiasingQuality.High;
            uac.renderShadows = true;

            // Deactivate any duplicate static scene cameras
            var otherCams = FindObjectsByType<Camera>(FindObjectsInactive.Exclude);
            foreach (var oc in otherCams)
            {
                if (oc != cam && oc.gameObject.name == "Camera" && oc.transform.parent == null)
                {
                    oc.gameObject.SetActive(false);
                }
            }
        }

        private void Start()
        {
            SetupWeaponSockets();
            SpawnWeapons();
            SetSwordParent(drawn: false); // start sheathed
        }

        private void Update()
        {
            if (InGamePauseManager.IsPaused)
                return;

            GatherInput();
            TickCooldowns();
            UpdateCombatState();
            UpdatePhysics();
            UpdateLocomotionParams();
            UpdateLayerWeights();
        }

        private void LateUpdate()
        {
            if (InGamePauseManager.IsPaused)
                return;

            UpdateCamera();
        }

        #endregion

        // ─── Parameter Hashes ─────────────────────────────────────────────────────
        #region Param Hashes

        private void CacheParamHashes()
        {
            _hMoveSpeed        = Animator.StringToHash(clips.paramMoveSpeed);
            _hCurrentGait      = Animator.StringToHash(clips.paramCurrentGait);
            _hIsWalking        = Animator.StringToHash(clips.paramIsWalking);
            _hIsCrouching      = Animator.StringToHash(clips.paramIsCrouching);
            _hIsGrounded       = Animator.StringToHash(clips.paramIsGrounded);
            _hIsJumping        = Animator.StringToHash(clips.paramIsJumping);
            _hStrafeX          = Animator.StringToHash(clips.paramStrafeX);
            _hStrafeZ          = Animator.StringToHash(clips.paramStrafeZ);
            _hIsStrafing       = Animator.StringToHash(clips.paramIsStrafing);
            _hLeanValue        = Animator.StringToHash(clips.paramLeanValue);
            _hFallingDuration  = Animator.StringToHash(clips.paramFallingDuration);
            _hMovementHeld     = Animator.StringToHash(clips.paramMovementInputHeld);
            _hMovementTapped   = Animator.StringToHash(clips.paramMovementInputTapped);
            _hIsStopped        = Animator.StringToHash(clips.paramIsStopped);
            _hIsStarting       = Animator.StringToHash(clips.paramIsStarting);
            _hSwordDrawn       = Animator.StringToHash(clips.paramSwordDrawn);
            _hSwordDrawnWeight = Animator.StringToHash(clips.paramSwordDrawnWeight);
            _hIsSliding        = Animator.StringToHash(clips.paramIsSliding);
            _hCombatState      = Animator.StringToHash(clips.paramCombatState);
            _hAttackID         = Animator.StringToHash(clips.paramAttackID);
            _hDodgeDirection   = Animator.StringToHash(clips.paramDodgeDirection);
            _hHitDirection     = Animator.StringToHash(clips.paramHitDirection);
        }

        #endregion

        // ─── Weapon System ────────────────────────────────────────────────────────
        #region Weapons

        private void SetupWeaponSockets()
        {
            if (animator == null) return;

            Transform rh   = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            Transform hips = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Hips)      : null;

            if (rh   == null) rh   = FindChild(transform, "Prop_R") ?? FindChild(transform, "Hand_R") ?? FindChild(transform, "hand_r");
            if (hips == null) hips = FindChild(transform, "pelvis") ?? FindChild(transform, "Hips")   ?? transform;

            // Check if Synty's dedicated Prop_R bone exists under right hand
            Transform propR = rh != null ? (rh.Find("Prop_R") ?? FindChild(rh, "Prop_R")) : null;
            Transform handParent = propR != null ? propR : rh;

            if (handParent != null && _handSocket == null)
            {
                Vector3 pos = propR != null ? Vector3.zero : handSwordOffset;
                Vector3 rot = propR != null ? Vector3.zero : handSwordRotation;
                _handSocket = MakeSocket(handParent, "Socket_RHand", pos, rot);
            }

            if (hips != null && _hipSocket == null)
            {
                _hipSocket = MakeSocket(hips, "Socket_Hip", hipSwordOffset, hipSwordRotation);
            }
        }

        private Transform MakeSocket(Transform parent, string name, Vector3 lpos, Vector3 lrot)
        {
            Transform ex = parent.Find(name);
            if (ex != null) return ex;
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.localPosition    = lpos;
            go.transform.localEulerAngles = lrot;
            go.transform.localScale       = Vector3.one;
            return go.transform;
        }

        private void SpawnWeapons()
        {
            #if UNITY_EDITOR
            if (swordPrefab == null)
                swordPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonCyberCity/Prefabs/Weapons/SM_Wep_Sword_01.prefab");
            if (sheathPrefab == null)
                sheathPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonCyberCity/Prefabs/Weapons/SM_Wep_Sword_01_Sheath_01.prefab");
            #endif

            // Spawn sword
            if (swordPrefab != null)
            {
                _sword = Instantiate(swordPrefab);
                _sword.name = "Sword";
                DisableColliders(_sword);
            }
            else
            {
                _sword = new GameObject("Sword");
                if (swordMesh != null)
                {
                    _sword.AddComponent<MeshFilter>().sharedMesh = swordMesh;
                    var mr = _sword.AddComponent<MeshRenderer>();
                    if (swordMaterial != null) mr.sharedMaterial = swordMaterial;
                }
            }

            // Spawn permanent hip sheath
            if (sheathPrefab != null && _hipSocket != null)
            {
                _sheath = Instantiate(sheathPrefab, _hipSocket);
                _sheath.name = "Sheath";
                _sheath.transform.localPosition = Vector3.zero;
                _sheath.transform.localRotation = Quaternion.identity;
                _sheath.transform.localScale    = swordScale;
                DisableColliders(_sheath);
            }
        }

        private static void DisableColliders(GameObject go)
        {
            if (go == null) return;
            foreach (var col in go.GetComponentsInChildren<Collider>(true))
            {
                if (col != null) col.enabled = false;
            }
        }

        private void SetSwordParent(bool drawn)
        {
            _swordDrawn = drawn;
            Transform target = drawn ? _handSocket : _hipSocket;
            if (_sword == null || target == null) return;
            _sword.transform.SetParent(target, worldPositionStays: false);
            _sword.transform.localPosition = Vector3.zero;
            _sword.transform.localRotation = Quaternion.identity;
            _sword.transform.localScale    = swordScale;
            DisableColliders(_sword);
            if (_sheath != null) DisableColliders(_sheath);
        }

        private void PollWeaponSwap()
        {
            if (_weaponSwapDone || _weaponSwapNormTime < 0f || animator == null) return;
            var info = animator.GetCurrentAnimatorStateInfo(combatLayerIndex);
            bool inState = info.shortNameHash == _activeStateHash || info.fullPathHash == _activeStateHash;
            if (inState && info.normalizedTime >= _weaponSwapNormTime)
            {
                _weaponSwapDone = true;
                SetSwordParent(_combatState == CombatState.Draw);
            }
        }

        private static Transform FindChild(Transform t, string name)
        {
            if (t.name.Equals(name, System.StringComparison.OrdinalIgnoreCase)) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var r = FindChild(t.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }

        #endregion

        // ─── Input ────────────────────────────────────────────────────────────────
        #region Input

        private void GatherInput()
        {
            if (inputLocked)
            {
                _moveInput = Vector2.zero;
                _lookInput = Vector2.zero;
                _sprintHeld = false;
                _crouching = false;
                _blockHeld = false;

                if (currentHealth <= 0f)
                {
                    bool r = false;
                    var k = Keyboard.current;
                    if (k != null && k.rKey.wasPressedThisFrame) r = true;
                    var gpDev = Gamepad.current;
                    if (gpDev != null && (gpDev.buttonWest.wasPressedThisFrame || gpDev.startButton.wasPressedThisFrame)) r = true;
                    try { if (Input.GetKeyDown(KeyCode.R)) r = true; } catch { }

                    if (r)
                    {
                        if (CyberpunkHUDController.Instance != null)
                        {
                            CyberpunkHUDController.Instance.TriggerReboot();
                        }
                        else if (FadePlaneRespawn.Instance != null)
                        {
                            FadePlaneRespawn.Instance.RespawnPlayer("Manual Neural Reboot [R]");
                        }
                        else
                        {
                            Respawn(transform.position, transform.rotation);
                        }
                    }
                }
                return;
            }

            var kb    = Keyboard.current;
            var mouse = Mouse.current;
            var gp    = Gamepad.current;

            // Move
            _moveInput = Vector2.zero;
            if (kb != null)
            {
                if (kb.wKey.isPressed) _moveInput.y += 1f;
                if (kb.sKey.isPressed) _moveInput.y -= 1f;
                if (kb.aKey.isPressed) _moveInput.x -= 1f;
                if (kb.dKey.isPressed) _moveInput.x += 1f;
            }
            if (gp != null)
            {
                var ls = gp.leftStick.ReadValue();
                if (ls.magnitude > 0.15f) _moveInput = ls;
            }
            _moveInput = Vector2.ClampMagnitude(_moveInput, 1f);

            // Look
            _lookInput = Vector2.zero;
            if (mouse != null) _lookInput = mouse.delta.ReadValue() * 0.1f;
            if (gp != null)
            {
                var rs = gp.rightStick.ReadValue();
                if (rs.magnitude > 0.15f) _lookInput = rs * (Time.deltaTime * 200f);
            }

            // Zoom
            _scrollInput = 0f;
            if (mouse != null) _scrollInput = mouse.scroll.ReadValue().y;
            if (gp != null)   _scrollInput += (gp.leftTrigger.ReadValue() - gp.rightTrigger.ReadValue()) * 200f;

            // Sprint / Walk / Crouch
            _sprintHeld = (kb != null && kb.leftShiftKey.isPressed) ||
                          (gp != null && gp.leftStickButton.isPressed);

            if (kb != null && (kb.zKey.wasPressedThisFrame || kb.capsLockKey.wasPressedThisFrame))
                _isWalkToggle = !_isWalkToggle;

            bool crouchPressed = (kb != null && (kb.leftCtrlKey.wasPressedThisFrame || (! _swordDrawn && kb.cKey.wasPressedThisFrame))) ||
                                 (gp != null && gp.rightStickButton.wasPressedThisFrame);
            if (crouchPressed)
            {
                if (_sprintHeld && _moveSpeed > runSpeed * 0.75f && _cc != null && _cc.isGrounded && !_isSliding && !IsInCombat)
                {
                    StartSlide();
                }
                else if (!_isSliding)
                {
                    _crouching = !_crouching;
                    if (_cc != null)
                    {
                        _cc.height = _crouching ? crouchHeight : standHeight;
                        _cc.center = new Vector3(0f, _cc.height * 0.5f, 0f);
                    }
                }
            }

            // Jump
            if ((kb != null && kb.spaceKey.wasPressedThisFrame) ||
                (gp != null && gp.buttonSouth.wasPressedThisFrame))
                TryJump();

            // Draw / Sheathe
            bool drawPressed = (kb != null && (kb.tKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame)) ||
                               (gp != null && gp.dpad.up.wasPressedThisFrame);
            if (drawPressed) TryDrawSheathe();

            // Block (hold Q or MMB)
            bool newBlock = (kb != null && kb.qKey.isPressed) ||
                            (mouse != null && mouse.middleButton.isPressed) ||
                            (gp != null && gp.buttonEast.isPressed);
            if (newBlock && !_blockHeld && _blockLockoutTimer <= 0f) TryStartBlock();
            else if (!newBlock && _blockHeld)                         TryEndBlock();
            _blockHeld = newBlock && _blockLockoutTimer <= 0f;

            // Parry (tap Q or F / LB while blocking or standalone)
            bool parryPressed = (kb != null && (kb.qKey.wasPressedThisFrame || kb.fKey.wasPressedThisFrame)) ||
                                (gp != null && gp.leftShoulder.wasPressedThisFrame);
            if (parryPressed && _swordDrawn) TryParry();

            // Dodge / Roll
            bool dodgePressed = (kb != null && kb.cKey.wasPressedThisFrame) ||
                                (gp != null && gp.rightShoulder.wasPressedThisFrame);
            if (dodgePressed && _swordDrawn) TryDodge(_sprintHeld);

            // Light Attack (LMB / West)
            bool lightPressed = (mouse != null && mouse.leftButton.wasPressedThisFrame) ||
                                (gp != null && gp.buttonWest.wasPressedThisFrame);
            if (lightPressed) HandleAttack(heavy: false);

            // Heavy Attack (RMB / North)
            bool heavyPressed = (mouse != null && mouse.rightButton.wasPressedThisFrame) ||
                                (gp != null && gp.buttonNorth.wasPressedThisFrame);
            if (heavyPressed) HandleAttack(heavy: true);

            // Invincibility toggle (I key / F2)
            if (kb != null && (kb.iKey.wasPressedThisFrame || kb.f2Key.wasPressedThisFrame))
            {
                isInvincible = !isInvincible;
                Debug.Log($"[SyntySwordController] Invincibility: {(isInvincible ? "ENABLED" : "DISABLED")}");
            }

            // HUD toggle
            if (kb != null && (kb.hKey.wasPressedThisFrame || kb.f1Key.wasPressedThisFrame))
                showCombatHUD = !showCombatHUD;

            // Cursor lock
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.Locked == Cursor.lockState
                    ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = Cursor.lockState != CursorLockMode.Locked;
            }
        }

        private void TickCooldowns()
        {
            if (_actionCooldownTimer > 0f) _actionCooldownTimer -= Time.deltaTime;
            if (_blockLockoutTimer   > 0f) _blockLockoutTimer   -= Time.deltaTime;
            if (_comboExpiry         > 0f)
            {
                _comboExpiry -= Time.deltaTime;
                if (_comboExpiry <= 0f) { _lightComboStep = 0; _heavyComboStep = 0; }
            }
        }

        #endregion

        // ─── Combat State Machine ─────────────────────────────────────────────────
        #region Combat

        public bool IsInCombat => _combatState != CombatState.None || _exitingCombat;

        // ── Draw / Sheathe ────────────────────────────────────────────────────────
        private void TryDrawSheathe()
        {
            if (IsInCombat || clips == null) return;
            if (_swordDrawn) StartSheathe(); else StartDraw();
        }

        private void StartDraw()
        {
            if (clips.drawSword == null) { SetSwordParent(true); return; }
            _weaponSwapNormTime = clips.drawSwapNormTime;
            PlayCombatClip(CombatState.Draw, clips.drawSword,
                           comboWindowNorm: clips.drawSwapNormTime, exitWindowNorm: 0.90f);
        }

        private void StartSheathe()
        {
            if (clips.sheatheSword == null) { SetSwordParent(false); return; }
            _weaponSwapNormTime = clips.sheatheSwapNormTime;
            PlayCombatClip(CombatState.Sheathe, clips.sheatheSword,
                           comboWindowNorm: 0f, exitWindowNorm: 0.92f);
        }

        // ── Attacks ───────────────────────────────────────────────────────────────
        private void HandleAttack(bool heavy)
        {
            if (clips == null) return;

            // Auto-draw if sheathed
            if (!_swordDrawn)
            {
                StartDraw();
                _queuedInput = heavy ? QueuedInput.HeavyAttack : QueuedInput.LightAttack;
                return;
            }

            if (_actionCooldownTimer > 0f) return;

            // If in HitReact, allow the player to INSTANTLY counter-attack out of the hit reaction!
            if (_combatState == CombatState.HitReact)
            {
                ExecuteAttack(heavy);
                return;
            }

            // If not in an active combat state (or exiting combat), attack immediately
            if (_combatState == CombatState.None)
            {
                ExecuteAttack(heavy);
                return;
            }

            // Buffer while drawing sword
            if (_combatState == CombatState.Draw)
            {
                _queuedInput = heavy ? QueuedInput.HeavyAttack : QueuedInput.LightAttack;
                return;
            }

            if (_combatState == CombatState.LightAttack || _combatState == CombatState.HeavyAttack || _combatState == CombatState.SpecialAttack)
            {
                bool isFinisher = (heavy && _heavyComboStep >= 3) || (!heavy && _lightComboStep >= 3);

                if (isFinisher)
                {
                    // During finisher, allow queuing the next combo once the strike has landed (after 50% duration)
                    if (_actionTimer >= _activeClipDuration * 0.50f)
                    {
                        _queuedInput = heavy ? QueuedInput.HeavyAttack : QueuedInput.LightAttack;
                    }
                    return;
                }

                // If already at or past combo window, execute next combo step immediately!
                if (_actionTimer >= _activeComboWindow)
                {
                    ExecuteAttack(heavy);
                    return;
                }

                // Otherwise buffer the input if pressed anytime after startup
                if (_actionTimer >= 0.05f)
                {
                    _queuedInput = heavy ? QueuedInput.HeavyAttack : QueuedInput.LightAttack;
                }
            }
        }

        /// <summary>
        /// Finds the best combat target (enemy or dummy with ISyntyDamageable) in front of the player.
        /// </summary>
        private Transform FindBestCombatTarget(float maxDistance = 7.5f, float maxAngle = 75f)
        {
            Camera cam = playerCamera != null ? playerCamera : Camera.main;
            Vector3 forwardRef = cam != null ? cam.transform.forward : transform.forward;
            forwardRef.y = 0f;
            if (forwardRef.sqrMagnitude < 0.001f) forwardRef = transform.forward;
            forwardRef.Normalize();

            Collider[] hits = Physics.OverlapSphere(transform.position, maxDistance);
            Transform bestTarget = null;
            float bestScore = float.MaxValue;

            for (int i = 0; i < hits.Length; i++)
            {
                var col = hits[i];
                if (col == null || col.gameObject == gameObject || col.transform.IsChildOf(transform)) continue;

                var dmg = col.GetComponentInParent<ISyntyDamageable>() ?? col.GetComponent<ISyntyDamageable>();
                if (dmg == null) continue;

                Transform targetT = (dmg as Component)?.transform ?? col.transform;
                Vector3 toTarget = targetT.position - transform.position;
                toTarget.y = 0f;
                float dist = toTarget.magnitude;
                if (dist < 0.1f) continue;

                float angle = Vector3.Angle(forwardRef, toTarget.normalized);
                if (angle > maxAngle) continue;

                // Priority score: closer distance + smaller angle from aiming direction
                float score = dist + (angle / maxAngle) * 2.0f;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestTarget = targetT;
                }
            }

            return bestTarget;
        }

        private void ExecuteAttack(bool heavy)
        {
            if (clips == null) return;
            if (!_swordDrawn) SetSwordParent(true);
            _blockLockoutTimer = blockLockoutAfterAttack;

            Transform target = FindBestCombatTarget(maxDistance: 7.5f, maxAngle: 75f);
            float distToTarget = float.MaxValue;

            if (target != null)
            {
                Vector3 toTarget = target.position - transform.position;
                toTarget.y = 0f;
                distToTarget = toTarget.magnitude;

                // Soft-lock rotation towards target for precise landing
                if (toTarget.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.LookRotation(toTarget.normalized);
                }
            }
            else
            {
                // Snap character forward toward camera target during attack
                Camera cam = playerCamera != null ? playerCamera : Camera.main;
                if (cam != null)
                {
                    Vector3 camFwd = cam.transform.forward;
                    camFwd.y = 0f;
                    if (camFwd.sqrMagnitude > 0.01f)
                        transform.rotation = Quaternion.LookRotation(camFwd.normalized);
                }
            }

            SyntyAnimClipData.ClipEntry entry;
            CombatState state;

            // ── DYNAMIC DISTANCE-BASED COMBAT MOVES ────────────────────────────────────
            // Long Range (> 3.6m to 7.5m): Gap-Closing Leaping Strike (Light) / Lunging Stab (Heavy)
            if (target != null && distToTarget > 3.6f && distToTarget <= 7.5f)
            {
                if (heavy)
                {
                    entry = clips.heavyStab.clip != null ? clips.heavyStab : clips.GetHeavyCombo(1);
                    state = CombatState.SpecialAttack;
                    _attackStepSpeed = 5.2f;
                    _actionCooldownTimer = 0.25f;
                    _currentAttackName = "Heavy Piercing Stab";
                    _heavyComboStep = 1;
                    _lightComboStep = 0;
                }
                else
                {
                    entry = clips.lightLeaping.clip != null ? clips.lightLeaping : clips.GetLightCombo(1);
                    state = CombatState.SpecialAttack;
                    _attackStepSpeed = 6.0f;
                    _actionCooldownTimer = 0.20f;
                    _currentAttackName = "Leaping Strike";
                    _lightComboStep = 1;
                    _heavyComboStep = 0;
                }
            }
            // Mid Range (2.0m to 3.6m): Initiating Fencing Lunge (Light) / Advance Flourish (Heavy)
            else if (target != null && distToTarget >= 2.0f && distToTarget <= 3.6f && _lightComboStep == 0 && _heavyComboStep == 0)
            {
                if (heavy)
                {
                    entry = clips.heavyFlourish.clip != null ? clips.heavyFlourish : clips.GetHeavyCombo(1);
                    state = CombatState.SpecialAttack;
                    _attackStepSpeed = 3.2f;
                    _actionCooldownTimer = 0.20f;
                    _currentAttackName = "Heavy Flourish";
                    _heavyComboStep = 1;
                    _lightComboStep = 0;
                }
                else
                {
                    entry = clips.lightFencing.clip != null ? clips.lightFencing : clips.GetLightCombo(1);
                    state = CombatState.SpecialAttack;
                    _attackStepSpeed = 3.5f;
                    _actionCooldownTimer = 0.12f;
                    _currentAttackName = "Fencing Lunge";
                    _lightComboStep = 1;
                    _heavyComboStep = 0;
                }
            }
            // Close Range (< 2.0m), Chained Combos, or No Enemy Target: Standard Combos
            else
            {
                if (heavy)
                {
                    _heavyComboStep = (_heavyComboStep % 3) + 1;
                    _lightComboStep = 0;
                    entry = clips.GetHeavyCombo(_heavyComboStep);
                    state = CombatState.HeavyAttack;
                    _attackStepSpeed = _heavyComboStep == 3 ? 3.8f : (_heavyComboStep == 2 ? 3.0f : 2.4f);
                    _actionCooldownTimer = _heavyComboStep == 3 ? 0.30f : 0.15f;
                    _currentAttackName = $"Heavy Combo {_heavyComboStep}/3";
                }
                else
                {
                    _lightComboStep = (_lightComboStep % 3) + 1;
                    _heavyComboStep = 0;
                    entry = clips.GetLightCombo(_lightComboStep);
                    state = CombatState.LightAttack;
                    _attackStepSpeed = _lightComboStep == 3 ? 3.2f : (_lightComboStep == 2 ? 2.5f : 2.0f);
                    _actionCooldownTimer = _lightComboStep == 3 ? 0.20f : 0.08f;
                    _currentAttackName = $"Light Combo {_lightComboStep}/3";
                }
            }

            // Combo window lasts for full clip duration + generous grace period
            float clipLen = entry.clip != null ? entry.clip.length : 1.5f;
            _comboExpiry = clipLen + ComboWindowSec;
            PlayCombatClipEntry(state, entry);

            int comboStep = heavy ? _heavyComboStep : _lightComboStep;
            CyberCombat.Audio.CyberCombatAudioSynth.PlayAtPoint(
                CyberCombat.Audio.CyberCombatAudioSynth.GetSwordSlash(comboStep > 0 ? comboStep : 1, heavy),
                transform.position + transform.forward * 1.2f + Vector3.up * 1.0f,
                heavy ? 0.95f : 0.85f,
                Random.Range(0.97f, 1.05f)
            );
            InsaneGun.Combat.AAACyberCombatVFX.SpawnSlashArc(transform, comboStep > 0 ? comboStep : 1, heavy && _heavyComboStep == 3);
        }

        // ── Special Attacks ───────────────────────────────────────────────────────
        private enum SpecialKind { HeavyFlourish, HeavyStab, LightFencing, LightLeaping }
        private void TrySpecialAttack(SpecialKind kind)
        {
            if (IsInCombat || clips == null || _actionCooldownTimer > 0f) return;
            if (!_swordDrawn) { StartDraw(); return; }

            SyntyAnimClipData.ClipEntry e = kind switch
            {
                SpecialKind.HeavyFlourish => clips.heavyFlourish,
                SpecialKind.HeavyStab     => clips.heavyStab,
                SpecialKind.LightFencing  => clips.lightFencing,
                _                         => clips.lightLeaping,
            };

            _attackStepSpeed = kind switch
            {
                SpecialKind.HeavyFlourish => 1.8f,
                SpecialKind.HeavyStab     => 4.2f,
                SpecialKind.LightFencing  => 2.5f,
                SpecialKind.LightLeaping  => 5.5f,
                _                         => 2.0f
            };

            PlayCombatClipEntry(CombatState.SpecialAttack, e);
            bool isHeavySpecial = kind == SpecialKind.HeavyFlourish || kind == SpecialKind.HeavyStab;
            CyberCombat.Audio.CyberCombatAudioSynth.PlayAtPoint(
                CyberCombat.Audio.CyberCombatAudioSynth.GetSwordSlash(2, isHeavySpecial),
                transform.position + transform.forward * 1.3f + Vector3.up * 1.0f,
                0.90f,
                Random.Range(0.97f, 1.04f)
            );
            InsaneGun.Combat.AAACyberCombatVFX.SpawnSlashArc(transform, isHeavySpecial ? 5 : 2, isHeavySpecial);
        }

        // ── Block ─────────────────────────────────────────────────────────────────
        private void TryStartBlock()
        {
            if (IsInCombat || clips == null || !_swordDrawn) return;
            _combatState   = CombatState.Block;
            _exitingCombat = false;
            CrossFadeCombat(clips.blockBegin, 0.10f);
        }

        private void TryEndBlock()
        {
            if (_combatState != CombatState.Block || clips == null) return;
            CrossFadeCombat(clips.blockEnd, 0.10f);
            EndCombatAction(0.20f);
        }

        // ── Parry ─────────────────────────────────────────────────────────────────
        private void TryParry()
        {
            if (clips == null || _actionCooldownTimer > 0f) return;
            bool wasBlocking = _combatState == CombatState.Block;
            SyntyAnimClipData.ClipEntry entry = wasBlocking ? clips.counterShove : clips.parryForward;
            PlayCombatClipEntry(CombatState.Parry, entry);
        }

        // ── Dodge ─────────────────────────────────────────────────────────────────
        private void TryDodge(bool roll)
        {
            if (IsInCombat || clips == null || _actionCooldownTimer > 0f) return;

            Vector3 moveDir = GetCamRelativeDir();
            if (moveDir.sqrMagnitude < 0.01f) moveDir = transform.forward;
            _dodgeWorldDir = moveDir.normalized;

            if (roll)
            {
                _dodgeSpeed = 7.5f;
                _dodgeTimer = 0.55f;
                transform.rotation = Quaternion.LookRotation(_dodgeWorldDir);
            }
            else
            {
                _dodgeSpeed = 4.5f;
                _dodgeTimer = 0.35f;
            }

            Vector2 localDir = _moveInput.sqrMagnitude > 0.1f ? _moveInput.normalized : Vector2.up;
            CardinalDirection dir = SyntyCombatAnimationResolver.GetMoveDirection(localDir);
            if (animator != null && _hDodgeDirection != 0) animator.SetInteger(_hDodgeDirection, (int)dir);
            SyntyAnimClipData.ClipEntry entry = SyntyCombatAnimationResolver.GetDodge(clips, dir, roll);
            PlayCombatClipEntry(CombatState.Dodge, entry);
            CyberCombat.Audio.CyberCombatAudioSynth.PlayAtPoint(
                CyberCombat.Audio.CyberCombatAudioSynth.GetDodgeWhoosh(),
                transform.position,
                0.90f,
                Random.Range(0.97f, 1.05f)
            );
        }

        // ── Hit / Knockdown / Stun / Death (called externally) ────────────────────
        public void TriggerHitReact(Vector3 worldHitDir)
        {
            if (clips == null) return;
            Vector3 local = transform.InverseTransformDirection(worldHitDir);
            CardinalDirection dir = SyntyCombatAnimationResolver.GetDominantDirection(local);
            if (animator != null && _hHitDirection != 0) animator.SetInteger(_hHitDirection, (int)dir);
            SyntyAnimClipData.ClipEntry entry = SyntyCombatAnimationResolver.GetHitReaction(clips, dir);
            PlayCombatClipEntry(CombatState.HitReact, entry);
        }

        public void TriggerHitStagger(Vector3 worldHitDir)
        {
            if (clips == null) return;
            Vector3 local = transform.InverseTransformDirection(worldHitDir);
            CardinalDirection dir = SyntyCombatAnimationResolver.GetDominantDirection(local);
            if (animator != null && _hHitDirection != 0) animator.SetInteger(_hHitDirection, (int)dir);
            SyntyAnimClipData.ClipEntry entry = SyntyCombatAnimationResolver.GetHitStagger(clips, dir);
            PlayCombatClipEntry(CombatState.HitReact, entry);
        }

        public void TriggerKnockDown()
        {
            if (clips == null) return;
            _combatState        = CombatState.KnockDown;
            _knockdownPhase     = KnockdownPhase.Begin;
            _exitingCombat      = false;
            _actionTimer        = 0f;
            var clip = SyntyCombatAnimationResolver.GetKnockdown(clips, KnockdownPhase.Begin);
            CrossFadeCombat(clip, 0.10f);
            _activeClipDuration = clip != null ? clip.length : 1.2f;
            _activeExitWindow   = float.MaxValue;
        }

        public void TriggerStun(float duration = 2.0f)
        {
            if (clips == null) return;
            _combatState        = CombatState.Stun;
            _stunPhase          = StunPhase.Begin;
            _stunLoopTimer      = duration;
            _exitingCombat      = false;
            _actionTimer        = 0f;
            var clip = SyntyCombatAnimationResolver.GetStun(clips, StunPhase.Begin);
            CrossFadeCombat(clip, 0.10f);
            _activeClipDuration = clip != null ? clip.length : 0.8f;
            _activeExitWindow   = float.MaxValue;
        }

        public void TriggerDeath(Vector3 worldHitDir)
        {
            if (clips == null) return;
            Vector3 local = transform.InverseTransformDirection(worldHitDir);
            CardinalDirection dir = SyntyCombatAnimationResolver.GetDominantDirection(local);
            AnimationClip dc = SyntyCombatAnimationResolver.GetDeath(clips, dir);
            _combatState   = CombatState.Death;
            _exitingCombat = false;
            CrossFadeCombat(dc, 0.15f);
            _activeClipDuration = dc != null ? dc.length : 2f;
            _activeExitWindow   = float.MaxValue;
            inputLocked = true;
        }

        // ── ISyntyDamageable Implementation ──────────────────────────────────────
        public void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitDirection, bool isHeavy)
        {
            if (isInvincible || _combatState == CombatState.Death) return;

            // Check if player is actively blocking or parrying
            if (_combatState == CombatState.Block)
            {
                // Frontal block check
                float dot = Vector3.Dot(transform.forward, -hitDirection);
                if (dot > 0.15f)
                {
                    // Mitigate damage
                    float mitigated = damage * (1f - blockMitigation);
                    currentHealth = Mathf.Max(0f, currentHealth - mitigated);
                    OnPlayerHealthChanged?.Invoke(currentHealth, maxHealth);

                    // Block recoil
                    if (_cc != null && _cc.enabled)
                    {
                        _cc.Move(-transform.forward * (isHeavy ? 1.5f : 0.6f));
                    }
                    return;
                }
            }
            else if (_combatState == CombatState.Parry)
            {
                // Attack parried by player! Deflect and no damage
                return;
            }

            // Unblocked hit
            currentHealth = Mathf.Max(0f, currentHealth - damage);
            OnPlayerHealthChanged?.Invoke(currentHealth, maxHealth);
            OnDamaged?.Invoke(this, damage);

            if (currentHealth <= 0f)
            {
                OnDeath?.Invoke(this);
                TriggerDeath(hitDirection);
                return;
            }

            // Player poise & anti-stunlock:
            // 1. If currently performing an attack, grant hyper-armor against normal hits so attacks aren't canceled
            bool isAttacking = (_combatState == CombatState.LightAttack || _combatState == CombatState.HeavyAttack || _combatState == CombatState.SpecialAttack);

            if (isAttacking && !isHeavy)
            {
                // Attack hyper-armor absorbs the hit without interrupting swing!
                return;
            }

            // 2. Add hit react cooldown so player isn't perpetually interrupted by rapid hits
            if (Time.time < _lastHitReactTime + 1.2f)
            {
                return;
            }

            _lastHitReactTime = Time.time;
            if (isHeavy)
            {
                TriggerHitStagger(hitDirection);
            }
            else
            {
                TriggerHitReact(hitDirection);
            }
        }

        public void Respawn(Vector3 pos, Quaternion rot)
        {
            currentHealth = maxHealth;
            _combatState = CombatState.None;
            _exitingCombat = false;
            enabled = true;
            if (_cc == null) _cc = GetComponent<CharacterController>();
            if (_cc != null)
            {
                _cc.enabled = false;
                transform.position = pos;
                transform.rotation = rot;
                Physics.SyncTransforms();
                _cc.enabled = true;
            }
            else
            {
                transform.position = pos;
                transform.rotation = rot;
                Physics.SyncTransforms();
            }
            inputLocked = false;
            if (animator != null)
            {
                animator.Rebind();
                animator.Update(0f);
            }
            OnPlayerHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        // ── Core PlayCombatClip helpers ───────────────────────────────────────────

        private void PlayCombatClipEntry(CombatState state, SyntyAnimClipData.ClipEntry entry)
        {
            if (entry.clip == null) return;
            PlayCombatClip(state, entry.clip, entry.comboWindowNorm, entry.exitWindowNorm);
        }

        private void PlayCombatClip(CombatState state, AnimationClip clip,
                                    float comboWindowNorm, float exitWindowNorm)
        {
            if (clip == null) return;

            StopAllCoroutines();
            _exitingCombat       = false;
            _combatState         = state;
            _actionTimer         = 0f;
            _weaponSwapDone      = false;
            _queuedInput         = QueuedInput.None;
            _actionCooldownTimer = Mathf.Max(_actionCooldownTimer, actionCooldown);
            _hitProcessedForCurrentAttack = false;

            _activeClipDuration = clip.length;
            _activeComboWindow  = clip.length * comboWindowNorm;
            _activeExitWindow   = clip.length * exitWindowNorm;
            _activeStateHash    = Animator.StringToHash(clip.name);

            if (animator != null && animator.isActiveAndEnabled)
            {
                animator.speed = 1f;
                _animSpeed     = 1f;
                _animSpeedVel  = 0f;
                _combatLayerWeight = 1f;
                animator.SetLayerWeight(combatLayerIndex, 1f);
                animator.CrossFadeInFixedTime(_activeStateHash, 0.08f, combatLayerIndex);
            }
        }

        private void CrossFadeCombat(AnimationClip clip, float blendTime)
        {
            if (clip == null || animator == null || !animator.isActiveAndEnabled) return;
            int h = Animator.StringToHash(clip.name);
            _combatLayerWeight = 1f;
            animator.SetLayerWeight(combatLayerIndex, 1f);
            animator.CrossFadeInFixedTime(h, blendTime, combatLayerIndex);
        }

        private void UpdateCombatState()
        {
            if (_combatState == CombatState.None) return;

            _actionTimer += Time.deltaTime;

            // KnockDown lifecycle: Begin -> Loop -> End
            if (_combatState == CombatState.KnockDown)
            {
                if (_knockdownPhase == KnockdownPhase.Begin)
                {
                    if (_actionTimer >= _activeClipDuration)
                    {
                        _knockdownPhase = KnockdownPhase.Loop;
                        _actionTimer = 0f;
                        _knockdownLoopTimer = 1.0f; // 1s on ground
                        var loopClip = SyntyCombatAnimationResolver.GetKnockdown(clips, KnockdownPhase.Loop);
                        CrossFadeCombat(loopClip, 0.10f);
                        _activeClipDuration = loopClip != null ? loopClip.length : 1f;
                    }
                }
                else if (_knockdownPhase == KnockdownPhase.Loop)
                {
                    _knockdownLoopTimer -= Time.deltaTime;
                    if (_knockdownLoopTimer <= 0f)
                    {
                        _knockdownPhase = KnockdownPhase.End;
                        _actionTimer = 0f;
                        var endClip = SyntyCombatAnimationResolver.GetKnockdown(clips, KnockdownPhase.End);
                        CrossFadeCombat(endClip, 0.10f);
                        _activeClipDuration = endClip != null ? endClip.length : 1.5f;
                    }
                }
                else if (_knockdownPhase == KnockdownPhase.End)
                {
                    if (_actionTimer >= _activeClipDuration)
                    {
                        EndCombatAction(0.20f);
                    }
                }
                return;
            }

            // Stun lifecycle: Begin -> Loop -> End
            if (_combatState == CombatState.Stun)
            {
                if (_stunPhase == StunPhase.Begin)
                {
                    if (_actionTimer >= _activeClipDuration)
                    {
                        _stunPhase = StunPhase.Loop;
                        _actionTimer = 0f;
                        var loopClip = SyntyCombatAnimationResolver.GetStun(clips, StunPhase.Loop);
                        CrossFadeCombat(loopClip, 0.10f);
                        _activeClipDuration = loopClip != null ? loopClip.length : 1f;
                    }
                }
                else if (_stunPhase == StunPhase.Loop)
                {
                    _stunLoopTimer -= Time.deltaTime;
                    if (_stunLoopTimer <= 0f)
                    {
                        _stunPhase = StunPhase.End;
                        _actionTimer = 0f;
                        var endClip = SyntyCombatAnimationResolver.GetStun(clips, StunPhase.End);
                        CrossFadeCombat(endClip, 0.10f);
                        _activeClipDuration = endClip != null ? endClip.length : 0.8f;
                    }
                }
                else if (_stunPhase == StunPhase.End)
                {
                    if (_actionTimer >= _activeClipDuration)
                    {
                        EndCombatAction(0.20f);
                    }
                }
                return;
            }

            // Weapon swap polling (Draw / Sheathe)
            if (_combatState == CombatState.Draw || _combatState == CombatState.Sheathe)
                PollWeaponSwap();

            // Active attack hit registration during Hit phase
            if ((_combatState == CombatState.LightAttack || _combatState == CombatState.HeavyAttack || _combatState == CombatState.SpecialAttack)
                && !_hitProcessedForCurrentAttack
                && _actionTimer >= _activeClipDuration * 0.22f
                && _actionTimer <= _activeClipDuration * 0.70f)
            {
                CheckAttackHit();
            }

            // Block loop
            if (_combatState == CombatState.Block && clips != null &&
                _actionTimer >= (clips.blockBegin != null ? clips.blockBegin.length : 0.4f))
            {
                if (clips.blockLoop != null)
                    CrossFadeCombat(clips.blockLoop, 0.10f);
                _actionTimer = 0f;
            }

            // Queued combo execution at combo window or exit window
            if (_queuedInput != QueuedInput.None && (_actionTimer >= _activeComboWindow || _actionTimer >= _activeExitWindow))
            {
                bool heavy = _queuedInput == QueuedInput.HeavyAttack;
                _queuedInput = QueuedInput.None;
                ExecuteAttack(heavy);
                return;
            }

            // Movement cancel after exit window
            if (_moveInput.sqrMagnitude > 0.04f && _actionTimer >= _activeExitWindow)
            {
                EndCombatAction(0.20f);
                return;
            }

            // Natural completion
            if (_actionTimer >= _activeClipDuration)
                EndCombatAction(0.18f);
        }

        private void CheckAttackHit()
        {
            if (_hitProcessedForCurrentAttack) return;

            bool isHeavy = _combatState == CombatState.HeavyAttack ||
                           (_combatState == CombatState.SpecialAttack && (_currentAttackName.Contains("Heavy") || _currentAttackName.Contains("Flourish") || _currentAttackName.Contains("Stab")));
            Vector3 hitCenter = transform.position + transform.forward * (isHeavy ? 1.6f : 1.3f) + Vector3.up * 1.0f;
            float hitRadius = isHeavy ? 1.6f : 1.3f;
            int count = Physics.OverlapSphereNonAlloc(hitCenter, hitRadius, _hitResults);

            float baseDamage = isHeavy ? 35f : 20f;
            if (_combatState == CombatState.LightAttack) baseDamage += _lightComboStep * 5f;
            if (_combatState == CombatState.HeavyAttack) baseDamage += _heavyComboStep * 10f;
            if (_combatState == CombatState.SpecialAttack) baseDamage += isHeavy ? 15f : 10f;

            bool anyHit = false;
            for (int i = 0; i < count; i++)
            {
                Collider col = _hitResults[i];
                if (col == null || col.gameObject == gameObject || col.transform.IsChildOf(transform)) continue;

                var damageable = col.GetComponentInParent<ISyntyDamageable>() ?? col.GetComponent<ISyntyDamageable>();
                if (damageable != null)
                {
                    damageable.TakeDamage(baseDamage, col.ClosestPoint(hitCenter), transform.forward, isHeavy);
                    anyHit = true;
                }
            }

            if (anyHit)
            {
                _hitProcessedForCurrentAttack = true;
                CyberCombat.Audio.CyberCombatAudioSynth.PlayAtPoint(
                    isHeavy ? CyberCombat.Audio.CyberCombatAudioSynth.GetHeavyImpact() : CyberCombat.Audio.CyberCombatAudioSynth.GetSwordImpact(),
                    hitCenter, 1f, 1f);
            }
        }

        private void EndCombatAction(float crossFadeTime)
        {
            // Guarantee draw/sheathe weapon state correct
            if (_combatState == CombatState.Draw)    SetSwordParent(drawn: true);
            if (_combatState == CombatState.Sheathe) SetSwordParent(drawn: false);

            if (_combatState == CombatState.HeavyAttack && _heavyComboStep >= 3)
                _heavyComboStep = 0;
            if (_combatState == CombatState.LightAttack && _lightComboStep >= 3)
                _lightComboStep = 0;

            _combatState = CombatState.None;
            _currentAttackName = "IDLE";

            if (animator != null && animator.isActiveAndEnabled)
            {
                animator.CrossFadeInFixedTime("Combat_Empty", crossFadeTime, combatLayerIndex);
            }

            StartCoroutine(DeferredExitCombat(crossFadeTime));
        }

        private IEnumerator DeferredExitCombat(float crossFadeTime)
        {
            _exitingCombat = true;
            yield return new WaitForSeconds(crossFadeTime * 0.5f);
            _exitingCombat = false;
        }

        private void UpdateLayerWeights()
        {
            if (animator == null) return;

            // Layer 3: Combat Actions (1 when active, smoothly fading to 0 when finished)
            float combatTarget = IsInCombat ? 1f : 0f;
            _combatLayerWeight = Mathf.MoveTowards(
                _combatLayerWeight, combatTarget, combatLayerFadeSpeed * Time.deltaTime);
            animator.SetLayerWeight(combatLayerIndex, _combatLayerWeight);

            // Layers 1 & 2: SwordArm and SwordHand (Official Synty 2-layer override architecture)
            // When sword is drawn and no full-body combat action is overriding:
            //   - SwordArm:  0.70f override (retains natural torso/leg locomotion motion)
            //   - SwordHand: 1.00f override (locks fingers and dedicated Prop_R grip without jitter)
            bool armActive = _swordDrawn && !IsInCombat && !_isSliding;
            float armTarget  = armActive ? 0.70f : 0f;
            float handTarget = armActive ? 1.00f : 0f;

            _swordArmLayerWeight = Mathf.MoveTowards(
                _swordArmLayerWeight, armTarget, 8f * Time.deltaTime);
            _swordHandLayerWeight = Mathf.MoveTowards(
                _swordHandLayerWeight, handTarget, 10f * Time.deltaTime);

            animator.SetLayerWeight(swordArmLayerIndex, _swordArmLayerWeight);
            animator.SetLayerWeight(swordHandLayerIndex, _swordHandLayerWeight);

            // Sword drawn weight for idle blend in Layer 0
            float drawnWeightTarget = _swordDrawn ? 1f : 0f;
            _swordDrawnWeight = Mathf.MoveTowards(_swordDrawnWeight, drawnWeightTarget, 5f * Time.deltaTime);
            animator.SetFloat(_hSwordDrawnWeight, _swordDrawnWeight);
            animator.SetBool(_hSwordDrawn, _swordDrawn);
            if (_hCombatState != 0) animator.SetInteger(_hCombatState, (int)_combatState);
        }

        private void TryJump()
        {
            if (_cc == null) return;

            // If player is currently sheathing or drawing sword, immediately complete it so jump executes smoothly
            if (_combatState == CombatState.Sheathe || _combatState == CombatState.Draw)
            {
                EndCombatAction(0.08f);
            }

            bool grounded = _cc.isGrounded || _groundGrace > 0f || CheckGroundBelow();
            if (!grounded || (IsInCombat && _combatState != CombatState.Sheathe && _combatState != CombatState.Draw)) return;

            if (_isSliding)
            {
                // Slide-jump: cancel slide into jump while carrying forward momentum!
                EndSlide(toCrouch: false);
            }

            _verticalVel = jumpForce;
            _isJumping   = true;
            _groundGrace = 0f;
            if (animator != null)
            {
                animator.SetBool(_hIsJumping, true);
                animator.speed = 1f;
            }
            // Silent jump takeoff (punchy landing audio plays on ground contact, Cyberpunk 2077 / Elden Ring style)
        }

        private bool CheckGroundBelow()
        {
            if (_cc == null) return false;
            Vector3 origin = transform.position + Vector3.up * 0.35f;
            float radius = Mathf.Max(0.12f, _cc.radius * 0.75f);
            float maxDist = 0.45f;
            int mask = ~((1 << gameObject.layer) | (1 << 2)); // Exclude player layer and IgnoreRaycast
            RaycastHit hit;
            if (Physics.SphereCast(origin, radius, Vector3.down, out hit, maxDist, mask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider != null && !hit.collider.transform.IsChildOf(transform) && hit.collider.transform.root != transform.root)
                    return true;
            }
            if (Physics.Raycast(transform.position + Vector3.up * 0.25f, Vector3.down, out hit, 0.45f, mask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider != null && !hit.collider.transform.IsChildOf(transform) && hit.collider.transform.root != transform.root)
                    return true;
            }
            return false;
        }

        private void StartSlide()
        {
            _isSliding   = true;
            _slideTimer  = slideDuration;
            _slideSpeed  = Mathf.Max(_moveSpeed * slideSpeedMultiplier, sprintSpeed * 1.15f);

            Vector3 moveDir = GetCamRelativeDir();
            if (moveDir.sqrMagnitude < 0.01f) moveDir = transform.forward;
            _slideDir = moveDir.normalized;
            transform.rotation = Quaternion.LookRotation(_slideDir);

            if (_cc != null)
            {
                _cc.height = crouchHeight;
                _cc.center = new Vector3(0f, crouchHeight * 0.5f, 0f);
            }

            if (animator != null)
                animator.SetBool(_hIsSliding, true);
        }

        private void EndSlide(bool toCrouch)
        {
            _isSliding  = false;
            _slideTimer = 0f;
            _slideSpeed = 0f;
            _crouching  = toCrouch;

            if (_cc != null)
            {
                _cc.height = _crouching ? crouchHeight : standHeight;
                _cc.center = new Vector3(0f, _cc.height * 0.5f, 0f);
            }

            if (animator != null)
                animator.SetBool(_hIsSliding, false);
        }

        #endregion

        // ─── Physics ──────────────────────────────────────────────────────────────
        #region Physics

        private void UpdatePhysics()
        {
            if (_cc == null) return;

            bool grounded = _cc.isGrounded || CheckGroundBelow();
            if (grounded)
            {
                _groundGrace = 0.12f;
                _fallingTime = 0f;
                if (_verticalVel < 0f) _verticalVel = -2.5f;
                if (_isJumping)
                {
                    _isJumping = false;
                    if (animator != null) animator.SetBool(_hIsJumping, false);
                    CyberCombat.Audio.CyberCombatAudioSynth.PlayAtPoint(
                        CyberCombat.Audio.CyberCombatAudioSynth.GetJumpLanding(),
                        transform.position,
                        0.90f,
                        Random.Range(0.95f, 1.04f)
                    );
                }
            }
            else
            {
                _groundGrace -= Time.deltaTime;
                _fallingTime += Time.deltaTime;
                _verticalVel += gravity * Time.deltaTime;
            }

            // Target speed & slide physics
            Vector3 velocity = Vector3.zero;
            Vector3 moveDir = GetCamRelativeDir();

            if (_isSliding)
            {
                _slideTimer -= Time.deltaTime;
                _slideSpeed = Mathf.MoveTowards(_slideSpeed, crouchSpeed, (sprintSpeed * 1.5f) * Time.deltaTime);
                _moveSpeed = _slideSpeed;
                velocity = _slideDir * _slideSpeed;

                if (_slideTimer <= 0f || !grounded)
                {
                    var kb = Keyboard.current;
                    bool stayCrouch = (kb != null && (kb.leftCtrlKey.isPressed || kb.cKey.isPressed)) && grounded;
                    EndSlide(stayCrouch);
                }
            }
            else
            {
                float targetSpeed = 0f;
                if (!IsInCombat || _combatState == CombatState.Block)
                {
                    float mag = _moveInput.magnitude;
                    if (mag >= 0.08f)
                    {
                        if (_combatState == CombatState.Block) targetSpeed = blockMoveSpeed;
                        else if (_crouching)                  targetSpeed = crouchSpeed;
                        else if (_sprintHeld)                 targetSpeed = sprintSpeed;
                        else if (_isWalkToggle || mag < 0.55f) targetSpeed = walkSpeed;
                        else                                  targetSpeed = runSpeed;
                    }
                    if (!grounded && _groundGrace <= 0f) targetSpeed *= airControl;
                }

                _moveSpeed = Mathf.MoveTowards(_moveSpeed, targetSpeed,
                    (targetSpeed > _moveSpeed ? acceleration : deceleration) * Time.deltaTime);

                velocity = moveDir * _moveSpeed;
            }

            // Character rotation
            Camera cam = playerCamera != null ? playerCamera : Camera.main;
            bool shouldStrafe = combatStrafeMode && _swordDrawn && !_sprintHeld && !_isSliding && cam != null;

            if (firstPersonView)
            {
                // In first person, character body aligns with camera yaw
                transform.rotation = Quaternion.Euler(0f, _camYaw, 0f);
            }
            else if (_isSliding)
            {
                if (_slideDir.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation, Quaternion.LookRotation(_slideDir), rotationSpeed * 1.5f * Time.deltaTime);
                }
            }
            else if (shouldStrafe && !IsInCombat)
            {
                // Face camera forward when in sword stance
                Vector3 camFwd = cam.transform.forward; camFwd.y = 0f;
                if (camFwd.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation, Quaternion.LookRotation(camFwd.normalized), rotationSpeed * Time.deltaTime);
                }
            }
            else if (!IsInCombat && moveDir.sqrMagnitude > 0.01f)
            {
                // Free-look / Sprint: Face movement direction
                float rs = Mathf.Lerp(rotationSpeed * 0.8f, rotationSpeed,
                                      _moveSpeed / Mathf.Max(0.1f, runSpeed));
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, Quaternion.LookRotation(moveDir), rs * Time.deltaTime);
            }

            // Apply dodge impulse
            if (_dodgeTimer > 0f)
            {
                _dodgeTimer -= Time.deltaTime;
                velocity += _dodgeWorldDir * _dodgeSpeed;
            }

            // Apply attack step impulse
            if (_attackStepSpeed > 0.01f)
            {
                _attackStepSpeed = Mathf.MoveTowards(_attackStepSpeed, 0f, 10f * Time.deltaTime);
                velocity += transform.forward * _attackStepSpeed;
            }

            velocity.y = _verticalVel;
            _cc.Move(velocity * Time.deltaTime);
        }

        private Vector3 GetCamRelativeDir()
        {
            if (_moveInput.sqrMagnitude < 0.005f) return Vector3.zero;
            Camera cam = playerCamera != null ? playerCamera : Camera.main;
            if (cam == null) return Vector3.zero;
            Vector3 fwd   = cam.transform.forward; fwd.y   = 0f; fwd.Normalize();
            Vector3 right = cam.transform.right;   right.y = 0f; right.Normalize();
            return Vector3.ClampMagnitude(fwd * _moveInput.y + right * _moveInput.x, 1f);
        }

        #endregion

        // ─── Locomotion Animator Params ───────────────────────────────────────────
        #region Locomotion Params

        private void UpdateLocomotionParams()
        {
            if (animator == null || clips == null) return;

            bool grounded = _cc != null && _cc.isGrounded;
            bool hasInput = _moveInput.sqrMagnitude > 0.02f;
            bool isMoving = _moveSpeed > 0.15f;

            // Gait: 0=Walk 1=Run 2=Sprint
            int gait = _sprintHeld ? 2 : (_isWalkToggle || _moveInput.magnitude < 0.55f) ? 0 : 1;

            animator.SetFloat(_hMoveSpeed,        _moveSpeed);
            animator.SetInteger(_hCurrentGait,    gait);
            animator.SetBool(_hIsWalking,         gait == 0 && isMoving);
            animator.SetBool(_hIsCrouching,       _crouching);
            animator.SetBool(_hIsGrounded,        grounded);
            animator.SetBool(_hIsJumping,         _isJumping);
            animator.SetFloat(_hFallingDuration,  _fallingTime);
            animator.SetBool(_hMovementHeld,      hasInput);
            animator.SetBool(_hMovementTapped,    hasInput && !isMoving);
            animator.SetBool(_hIsStopped,         !isMoving);
            animator.SetBool(_hIsStarting,        hasInput && !isMoving);

            // Strafe parameters
            Camera cam = playerCamera != null ? playerCamera : Camera.main;
            bool strafing = combatStrafeMode && _swordDrawn && !_sprintHeld && !_isSliding && cam != null;
            if (strafing && hasInput)
            {
                Vector3 camDir = GetCamRelativeDir();
                float targetX = Vector3.Dot(transform.right, camDir);
                float targetZ = Vector3.Dot(transform.forward, camDir);
                _strafeDirectionX = Mathf.Lerp(_strafeDirectionX, targetX, 10f * Time.deltaTime);
                _strafeDirectionZ = Mathf.Lerp(_strafeDirectionZ, targetZ, 10f * Time.deltaTime);
                animator.SetFloat(_hIsStrafing, 1f);
            }
            else
            {
                _strafeDirectionX = Mathf.Lerp(_strafeDirectionX, 0f, 10f * Time.deltaTime);
                _strafeDirectionZ = Mathf.Lerp(_strafeDirectionZ, 1f, 10f * Time.deltaTime);
                animator.SetFloat(_hIsStrafing, 0f);
            }
            animator.SetFloat(_hStrafeX, _strafeDirectionX);
            animator.SetFloat(_hStrafeZ, _strafeDirectionZ);
            animator.SetBool(_hIsSliding, _isSliding);

            // Lean
            animator.SetFloat(_hLeanValue, _moveInput.x * 0.5f);

            // Animator speed matching
            if (!IsInCombat)
            {
                float target = 1f;
                if (_moveSpeed >= 0.05f)
                {
                    float clipSpeed = gait == 2 ? clips.sprintClipSpeed
                                    : gait == 0 ? clips.walkClipSpeed
                                    : _crouching ? clips.crouchClipSpeed
                                    : clips.runClipSpeed;
                    target = Mathf.Clamp(_moveSpeed / Mathf.Max(0.01f, clipSpeed), 0.65f, 1.45f);
                }
                _animSpeed = Mathf.SmoothDamp(_animSpeed, target, ref _animSpeedVel, 0.10f);
                animator.speed = _animSpeed;
            }
        }

        #endregion

        // ─── Camera ───────────────────────────────────────────────────────────────
        #region Camera

        private void UpdateCamera()
        {
            Camera cam = playerCamera != null ? playerCamera : Camera.main;
            if (cam == null || cameraTarget == null) return;

            // Perspective toggle (V or P key)
            var kb = Keyboard.current;
            if ((kb != null && (kb.vKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame)) || Input.GetKeyDown(togglePerspectiveKey))
            {
                firstPersonView = !firstPersonView;
                _camInit = false;
            }

            _camYaw   += _lookInput.x * cameraSensitivity * Time.deltaTime;
            _camPitch -= _lookInput.y * cameraSensitivity * Time.deltaTime;

            if (firstPersonView)
            {
                _camPitch = Mathf.Clamp(_camPitch, -85f, 85f);
                Quaternion rot = Quaternion.Euler(_camPitch, _camYaw, 0f);

                // Compute accurate eye level (standing ~1.63m, crouching ~1.10m)
                Vector3 eyePos;
                if (_headBone != null)
                {
                    eyePos = _headBone.position + Vector3.up * 0.07f + transform.forward * 0.08f;
                }
                else
                {
                    float eyeHeight = _crouching ? 1.10f : 1.63f;
                    eyePos = transform.position + Vector3.up * eyeHeight + transform.forward * 0.08f;
                }

                cam.transform.position = eyePos;
                cam.transform.rotation = rot;
                cam.fieldOfView = 75f;
                cam.nearClipPlane = 0.03f;

                // Hide head mesh so it never clips into camera view
                if (_headBone != null)
                {
                    _headBone.localScale = Vector3.zero;
                }
                return;
            }
            else
            {
                // Restore head scale in third person
                if (_headBone != null && _headBone.localScale != Vector3.one)
                {
                    _headBone.localScale = Vector3.one;
                }
            }

            _camPitch  = Mathf.Clamp(_camPitch, camMinPitch, camMaxPitch);

            // Scroll zoom
            if (Mathf.Abs(_scrollInput) > 0.01f)
            {
                _camDistTarget -= _scrollInput * cameraZoomSpeed * 0.01f;
                _camDistTarget  = Mathf.Clamp(_camDistTarget, cameraMinDist, cameraMaxDist);
            }
            _camDistCurrent = Mathf.Lerp(_camDistCurrent, _camDistTarget, cameraZoomSmooth * Time.deltaTime);

            Quaternion rot3rd  = Quaternion.Euler(_camPitch, _camYaw, 0f);
            Vector3 desired = cameraTarget.position - rot3rd * Vector3.forward * _camDistCurrent;

            float dist = _camDistCurrent;
            if (cameraCollisionMask != 0 && Physics.SphereCast(
                cameraTarget.position, 0.18f,
                (desired - cameraTarget.position).normalized,
                out RaycastHit hit, _camDistCurrent, cameraCollisionMask))
            {
                dist    = Mathf.Clamp(hit.distance - 0.15f, 0.5f, _camDistCurrent);
                desired = cameraTarget.position - rot3rd * Vector3.forward * dist;
            }

            if (!_camInit) { _smoothCamPos = desired; _camInit = true; }
            _smoothCamPos          = Vector3.Lerp(_smoothCamPos, desired, cameraLagSpeed * Time.deltaTime);
            cam.transform.position = _smoothCamPos;
            cam.transform.LookAt(cameraTarget.position);
        }

        #endregion

        // ─── OnGUI Combat HUD ─────────────────────────────────────────────────────
        #region HUD

        private void OnGUI()
        {
            // Legacy Immediate Mode GUI disabled in favor of AAA Cyberpunk 2077 UGUI HUD
            return;
        }

        #endregion

        #if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (cameraTarget != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(cameraTarget.position, 0.12f);
            }
        }
        #endif
    }
}
