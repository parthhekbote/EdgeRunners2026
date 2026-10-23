using System.Collections;
using UnityEngine;
using CyberCombat.Coordination;

namespace SyntyCombined
{
    /// <summary>
    /// Tactical 1v1 Sword Combat Enemy AI — Cyborg Mercenary Edition.
    ///
    /// Attack Repertoire:
    ///   PHASE 1: Light Combo (1-3 hits), Heavy Combo (1-2 hits), Heavy Stab,
    ///            Leaping Attack, Spinning Cyclone, Shoulder Charge, Overhead Slam
    ///   PHASE 2: + Cyber Blink Slash, Pommel Cross, Grenade Toss, 6-hit Flurry
    ///
    /// VFX: Spawns existing project FX prefabs on hit impacts, blink teleport,
    ///      charge impact, Phase 2 activation, and death.
    ///
    /// Camera Shake: Fires on Overhead Slam hit, Shoulder Charge impact, Phase 2 activation.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class SwordEnemyAI : MonoBehaviour, ISyntyDamageable, IGroupCombatant
    {
        // ── IGroupCombatant Group Coordination ──────────────────────────────
        private bool _hasAttackToken = false;
        Transform IGroupCombatant.CombatantTransform => transform;
        Transform IGroupCombatant.CombatTarget => playerTarget;
        bool IGroupCombatant.IsCombatActive => _currentState != AIState.Dead && isActiveAndEnabled;
        bool IGroupCombatant.IsCurrentlyAttacking => _currentState == AIState.Attack || _isActionLocked;
        void IGroupCombatant.OnAttackTokenGranted() { _hasAttackToken = true; }
        void IGroupCombatant.OnAttackTokenRevoked() { _hasAttackToken = false; }
        public enum AIState
        {
            Idle,
            Intro,
            StalkOrbit,
            Advance,
            Attack,
            Block,
            ParryCounter,
            Dodge,
            Staggered,
            HitReact,
            MenacingIdle,
            Knockdown,
            // ── Special & Combo Moves ──────────────────
            BlinkSlash,
            SpinningCyclone,
            OverheadSlam,
            ShoulderCharge,
            PommelCross,
            GrenadeToss,
            GunKataBurst,
            GunSwordCombo,
            // ──────────────────────────────────────────
            OverdriveTransition,
            Dead
        }

        // ─── Inspector: References ───────────────────────────────────────────────
        [Header("References")]
        [Tooltip("The animator component driving character animation.")]
        public Animator animator;
        [Tooltip("Clip database asset with all Synty clips and timings.")]
        public SyntyAnimClipData clips;
        [Tooltip("Weapon socket controller managing CyberCity swords.")]
        public WeaponSocketController weaponSocket;
        [Tooltip("Transform of the player target. Auto-detected by tag or type if unassigned.")]
        public Transform playerTarget;

        [Header("Layer Configuration")]
        public int locomotionLayerIndex = 0;
        public int swordArmLayerIndex   = 1;
        public int swordHandLayerIndex  = 2;
        public int combatLayerIndex     = 3;

        // ─── Inspector: VFX ───────────────────────────────────────────────────────
        [Header("VFX Prefabs (Assign from Assets/PolygonCyberCity/Prefabs/FX etc.)")]
        [Tooltip("Spawned on successful sword strike. e.g. FX_Sparks_01 (PolygonCyberCity)")]
        public GameObject vfxSwordHit;
        [Tooltip("Spawned on block impact. e.g. FX_Sparks_01 at bigger scale.")]
        public GameObject vfxBlockImpact;
        [Tooltip("Spawned at origin on Blink Slash teleport. e.g. FX_Smoke_Dark_01 (PolygonMech)")]
        public GameObject vfxBlinkSmoke;
        [Tooltip("Spawned on Shoulder Charge ground impact. e.g. FX_Footstep_01 (PolygonMech)")]
        public GameObject vfxChargeImpact;
        [Tooltip("Spawned on Phase 2 Overdrive activation. e.g. FX_Sparks_01 (PolygonMech)")]
        public GameObject vfxOverdriveBurst;
        [Tooltip("Spawned on Grenade explosion. e.g. FX_Sparks_01 (PolygonCyberCity)")]
        public GameObject vfxGrenadeBurst;
        [Tooltip("Spawned on death. e.g. FX_Smoke_Dark_01 (PolygonMech)")]
        public GameObject vfxDeath;

        // ─── Inspector: Combat Distances & Spacing ────────────────────────────────
        [Header("Combat Spacing (Configurable)")]
        [Tooltip("Ideal circling / strafing distance from the player (meters).")]
        public float idealOrbitDistance = 3.2f;
        [Tooltip("Maximum distance to initiate sword attacks.")]
        public float attackRange = 2.0f;
        [Tooltip("Distance at which AI will advance rapidly or leap.")]
        public float chaseDistance = 6.0f;
        [Tooltip("Buffer distance to prevent jittering during orbit.")]
        public float orbitTolerance = 0.5f;

        // ─── Inspector: Movement Speeds ───────────────────────────────────────────
        [Header("Movement Speeds (Configurable)")]
        public float walkSpeed    = 2.2f;
        public float runSpeed     = 4.8f;
        public float strafeSpeed  = 2.0f;
        public float rotationSpeed = 10f;
        public float gravity      = 20f;

        // ─── Inspector: Defensive Stats ───────────────────────────────────────────
        [Header("Defensive Reactions (0.0 to 1.0)")]
        [Range(0f, 1f)] public float blockChance = 0.80f;
        [Range(0f, 1f)] public float parryChance = 0.45f;
        [Range(0f, 1f)] public float dodgeChance = 0.40f;
        [Tooltip("Damage reduction when blocking frontal attacks (0.8 = 80% reduced).")]
        [Range(0f, 1f)] public float blockMitigation = 0.88f;
        [Tooltip("Angle in front within which attacks can be blocked or parried (degrees).")]
        public float defenseArc = 140f;

        // ─── Inspector: Health & Posture ─────────────────────────────────────────
        [Header("Health & Posture (Configurable)")]
        public float maxHealth   = 500f;
        public float currentHealth = 500f;
        public bool IsDead => _currentState == AIState.Dead || currentHealth <= 0f;
        public float maxPosture  = 150f;
        public float currentPosture = 0f;
        [Tooltip("Super armor absorbs hits without flinching or canceling special combos")]
        public bool  hasSuperArmor = false;
        [Tooltip("Rate at which posture decays back to 0 (points/sec).")]
        public float postureDecayRate = 8f;
        [Tooltip("Duration of stagger vulnerable window after posture break (seconds).")]
        public float postureBreakDuration = 2.5f;

        // ─── Inspector: Attack Pacing ─────────────────────────────────────────────
        [Header("Attack Decision Pacing")]
        [Tooltip("Minimum time between attack combo sequences (seconds).")]
        public float minAttackCooldown = 0.8f;
        [Tooltip("Maximum time between attack combo sequences (seconds).")]
        public float maxAttackCooldown = 1.8f;
        [Tooltip("Chance to continue to hit 2 and hit 3 in a combo chain.")]
        [Range(0f, 1f)] public float comboChainChance = 0.85f;
        public float lightAttackDamage = 22f;
        public float heavyAttackDamage = 42f;

        // ─── Inspector: Special Move Config ──────────────────────────────────────
        [Header("Special Move Config")]
        [Tooltip("Distance the Blink Slash teleports toward the player (meters).")]
        public float blinkSlashTeleportDistance = 3.0f;
        [Tooltip("Distance the Shoulder Charge travels (meters).")]
        public float shoulderChargeDashDistance = 6.0f;
        [Tooltip("Speed of the Shoulder Charge dash.")]
        public float shoulderChargeSpeed = 14f;
        [Tooltip("Radius of Spinning Cyclone arc sweep (meters).")]
        public float cycloneRadius = 2.2f;
        [Tooltip("Number of cyclone sub-hits that spin (2-4 recommended).")]
        [Range(2, 4)] public int cycloneHitCount = 3;
        [Tooltip("AoE radius of the Grenade explosion.")]
        public float grenadeExplosionRadius = 3.5f;
        [Tooltip("How hard the camera shakes on a heavy hit (0.1-0.6 range).")]
        [Range(0f, 1f)] public float cameraShakeMagnitude = 0.25f;
        [Tooltip("Duration of camera shake (seconds).")]
        public float cameraShakeDuration = 0.35f;

        // ─── Inspector: Phase 2 Overdrive ────────────────────────────────────────
        [Header("Overdrive Phase 2 (Configurable)")]
        [Range(0f, 1f)] public float overdriveThreshold = 0.40f;
        public float overdriveDamageMultiplier = 1.35f;
        public float overdriveMoveMultiplier   = 1.25f;
        public static float difficultyDamageMultiplier = 1.0f;

        // ─── Runtime State ────────────────────────────────────────────────────────
        [Header("Runtime Info (Read Only)")]
        [SerializeField] private AIState _currentState = AIState.Idle;
        public AIState CurrentState => _currentState;
        [SerializeField] private bool _isPhase2 = false;
        public bool IsPhase2 => _isPhase2;

        private CharacterController _cc;
        private float _verticalVelocity = 0f;
        private float _nextAttackTime   = 0f;
        private int   _strafeDirection  = 1;
        private float _strafeDirectionTimer = 0f;
        private bool  _isActionLocked   = false;
        private Coroutine _activeActionCoroutine;
        private Collider[] _hitResults = new Collider[8];
        private CardinalDirection _lastEnemyDodgeDir = CardinalDirection.Back;
        private float _nextPersonalityIdleTime = 5f;

        // Dynamic layer weight state
        private float _combatLayerWeight   = 0f;
        private float _swordArmLayerWeight  = 0f;
        private float _swordHandLayerWeight = 0f;

        // Events for UI HUD
        public System.Action<float, float> OnHealthChanged;
        public System.Action<float, float> OnPostureChanged;
        public System.Action<AIState>      OnStateChanged;
        public System.Action               OnOverdriveActivated;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            if (animator    == null) animator    = GetComponentInChildren<Animator>();
            if (weaponSocket == null) weaponSocket = GetComponent<WeaponSocketController>();

            currentHealth = maxHealth;
            currentPosture = 0f;
        }

        private void OnDisable()
        {
            CyberCombat.Coordination.EnemyGroupCombatCoordinator.Instance?.UnregisterCombatant(this);
        }

        private void OnDestroy()
        {
            CyberCombat.Coordination.EnemyGroupCombatCoordinator.Instance?.UnregisterCombatant(this);
        }

        private void Start()
        {
            FindPlayerTarget();
            if (playerTarget != null)
            {
                CyberCombat.Coordination.EnemyGroupCombatCoordinator.Instance?.RegisterCombatant(this, playerTarget);
            }
            _nextAttackTime = Time.time + Random.Range(minAttackCooldown, maxAttackCooldown);
            if (weaponSocket != null)
            {
                weaponSocket.SetDrawn(false);
            }
            if (animator != null && animator.isActiveAndEnabled && clips != null)
            {
                animator.SetBool(clips.paramSwordDrawn, false);
                animator.SetLayerWeight(combatLayerIndex, 0f);
                animator.SetLayerWeight(swordArmLayerIndex, 0f);
                animator.SetLayerWeight(swordHandLayerIndex, 0f);
            }
            StartCombatAction(ExecuteIntroRoutine());
        }

        private void Update()
        {
            if (_currentState == AIState.Dead) return;

            // Decay posture if not in combat action
            if (_currentState != AIState.Staggered && currentPosture > 0f)
            {
                currentPosture = Mathf.Max(0f, currentPosture - postureDecayRate * Time.deltaTime);
                OnPostureChanged?.Invoke(currentPosture, maxPosture);
            }

            // Target tracking
            if (playerTarget == null)
            {
                FindPlayerTarget();
                if (playerTarget == null) return;
            }

            switch (_currentState)
            {
                case AIState.StalkOrbit: UpdateStalkOrbit(); break;
                case AIState.Advance:    UpdateAdvance();    break;
                // All other states handled by coroutines
                default:                 ApplyGravityOnly(); break;
            }

            UpdateLayerWeights();
        }

        private void FindPlayerTarget()
        {
            var player = FindAnyObjectByType<SyntySwordLocomotionController>();
            if (player != null) { playerTarget = player.transform; return; }
            var playerGO = GameObject.FindWithTag("Player");
            if (playerGO != null) playerTarget = playerGO.transform;
        }

        // ══════════════════════════════════════════════════════════════════════════
        // LOCOMOTION
        // ══════════════════════════════════════════════════════════════════════════
        private void UpdateStalkOrbit()
        {
            if (_isActionLocked) return;

            Vector3 toPlayer = playerTarget.position - transform.position;
            toPlayer.y = 0f;
            float distance = toPlayer.magnitude;

            if (toPlayer.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(toPlayer.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }

            // ── Multi-Enemy Group Coordination (Surround First + Coordinated Attack Sequencing) ──
            var coordinator = CyberCombat.Coordination.EnemyGroupCombatCoordinator.Instance;
            if (coordinator != null && playerTarget != null)
            {
                coordinator.RegisterCombatant(this, playerTarget);

                if (coordinator.IsGroupSurroundActive(playerTarget))
                {
                    bool hasToken = _hasAttackToken || coordinator.HasAttackToken(this);

                    if (!hasToken)
                    {
                        // ── SURROUND & PERIMETER GUARD MODE (WAITING TURN) ──
                        Vector3 slotPos = coordinator.GetSurroundSlotPosition(this, playerTarget, -1f);
                        Vector3 toSlot = slotPos - transform.position;
                        toSlot.y = 0f;
                        float distToSlot = toSlot.magnitude;

                        // Perimeter spacing buffer: If player steps close (< 85% of surroundRadius), actively back up to keep distance!
                        if (distance < coordinator.surroundRadius * 0.85f)
                        {
                            Vector3 backDir = -toPlayer.normalized;
                            ApplyMovement(backDir * (walkSpeed * 1.15f));
                            UpdateAnimatorLocomotion(backDir, walkSpeed * 1.15f);
                            FacePlayer();
                        }
                        else if (distToSlot > 1.1f)
                        {
                            Vector3 slotMoveDir = toSlot.normalized;
                            float slotSpeed = distToSlot > 3.5f ? runSpeed : walkSpeed;
                            ApplyMovement(slotMoveDir * slotSpeed);
                            UpdateAnimatorLocomotion(slotMoveDir, slotSpeed);
                            FacePlayer();
                        }
                        else
                        {
                            // In position on perimeter: face player
                            FacePlayer();

                            // Maintain subtle menacing combat strafe bob along perimeter arc
                            float sway = Mathf.Sin(Time.time * 2f + (float)(GetHashCode() % 100) * 0.1f) * 0.35f;
                            Vector3 bob = transform.right * sway;
                            ApplyMovement(bob);
                            UpdateAnimatorLocomotion(bob, 0.35f);
                        }

                        // Suppress attack initiation while awaiting turn in surround formation
                        return;
                    }
                }
            }

            _strafeDirectionTimer -= Time.deltaTime;
            if (_strafeDirectionTimer <= 0f)
            {
                _strafeDirection      = Random.value > 0.5f ? 1 : -1;
                _strafeDirectionTimer = Random.Range(2.5f, 5.0f);
            }

            Vector3 moveDir    = Vector3.zero;
            float   targetSpeed = strafeSpeed;

            if (distance > idealOrbitDistance + orbitTolerance)
            {
                moveDir    += toPlayer.normalized * 0.7f;
                targetSpeed = walkSpeed;
            }
            else if (distance < idealOrbitDistance - orbitTolerance)
            {
                moveDir -= toPlayer.normalized * 0.7f;
            }

            Vector3 strafeVector = Vector3.Cross(Vector3.up, toPlayer.normalized) * _strafeDirection;
            moveDir += strafeVector;
            moveDir.Normalize();

            ApplyMovement(moveDir * targetSpeed);
            UpdateAnimatorLocomotion(moveDir, targetSpeed);

            if (CanPlayPersonalityIdle())
            {
                StartCombatAction(ExecuteMenacingIdleRoutine());
                return;
            }

            if (Time.time >= _nextAttackTime)
            {
                if (distance <= attackRange + 0.5f)
                {
                    ChangeState(AIState.Attack);
                    StartCombatAction(ExecuteAttackComboRoutine());
                }
                else
                {
                    ChangeState(AIState.Advance);
                }
            }
        }

        private void UpdateAdvance()
        {
            if (_isActionLocked) return;

            var coordinator = CyberCombat.Coordination.EnemyGroupCombatCoordinator.Instance;
            if (coordinator != null && coordinator.IsGroupSurroundActive(playerTarget))
            {
                bool hasToken = _hasAttackToken || coordinator.HasAttackToken(this);
                if (!hasToken)
                {
                    ChangeState(AIState.StalkOrbit);
                    return;
                }
            }

            Vector3 toPlayer = playerTarget.position - transform.position;
            toPlayer.y = 0f;
            float distance = toPlayer.magnitude;

            if (toPlayer.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(toPlayer.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * 1.5f * Time.deltaTime);
            }

            if (distance <= attackRange)
            {
                ChangeState(AIState.Attack);
                StartCombatAction(ExecuteAttackComboRoutine());
                return;
            }

            float speed   = distance > chaseDistance ? runSpeed : walkSpeed;
            Vector3 dir   = toPlayer.normalized;
            ApplyMovement(dir * speed);
            UpdateAnimatorLocomotion(dir, speed);
        }

        private void ApplyMovement(Vector3 horizontalVelocity)
        {
            if (_cc.isGrounded) _verticalVelocity = -2f;
            else                _verticalVelocity -= gravity * Time.deltaTime;
            _cc.Move((horizontalVelocity + Vector3.up * _verticalVelocity) * Time.deltaTime);
        }

        private void ApplyGravityOnly()
        {
            if (_cc.isGrounded) _verticalVelocity = -2f;
            else                _verticalVelocity -= gravity * Time.deltaTime;
            _cc.Move(Vector3.up * _verticalVelocity * Time.deltaTime);
        }

        private void UpdateAnimatorLocomotion(Vector3 worldMoveDir, float currentSpeed)
        {
            if (animator == null) return;
            Vector3 localDir = transform.InverseTransformDirection(worldMoveDir);
            animator.SetFloat(clips.paramMoveSpeed,          currentSpeed);
            animator.SetFloat(clips.paramStrafeX,            localDir.x);
            animator.SetFloat(clips.paramStrafeZ,            localDir.z);
            animator.SetFloat(clips.paramIsStrafing,         Mathf.Abs(localDir.x) > 0.2f ? 1f : 0f);
            animator.SetBool(clips.paramIsWalking,           currentSpeed <= walkSpeed + 0.1f && currentSpeed > 0.1f);
            animator.SetBool(clips.paramIsGrounded,          _cc.isGrounded);
            animator.SetBool(clips.paramSwordDrawn,          true);
            animator.SetFloat(clips.paramSwordDrawnWeight,   1f);
        }

        // ══════════════════════════════════════════════════════════════════════════
        // ATTACK COMBO DECISION TREE
        // ══════════════════════════════════════════════════════════════════════════
        private IEnumerator ExecuteAttackComboRoutine()
        {
            CyberCombat.Coordination.EnemyGroupCombatCoordinator.Instance?.NotifyAttackStarted(this);
            _isActionLocked = true;
            animator.SetFloat(clips.paramMoveSpeed, 0f);
            animator.SetLayerWeight(combatLayerIndex, 1f);

            float dmgMult = _isPhase2 ? overdriveDamageMultiplier : 1f;
            float choice  = Random.value;

            Vector3 toPlayer = playerTarget != null
                ? (playerTarget.position - transform.position)
                : transform.forward;
            toPlayer.y = 0f;
            float distance = toPlayer.magnitude;

            // ── PHASE 2 EXCLUSIVE ────────────────────────────────────────────────
            if (_isPhase2)
            {
                if (choice < 0.14f)
                {
                    // Cyber Blink Slash
                    ChangeState(AIState.BlinkSlash);
                    yield return StartCoroutine(ExecuteBlinkSlashRoutine(dmgMult));
                    EndCombatAction(); yield break;
                }
                if (choice < 0.25f)
                {
                    // Pommel Cross
                    ChangeState(AIState.PommelCross);
                    yield return StartCoroutine(ExecutePommelCrossRoutine(dmgMult));
                    EndCombatAction(); yield break;
                }
                if (choice < 0.38f)
                {
                    // Grenade Toss
                    ChangeState(AIState.GrenadeToss);
                    yield return StartCoroutine(ExecuteGrenadeTossRoutine(dmgMult));
                    EndCombatAction(); yield break;
                }
                if (choice < 0.50f)
                {
                    // 6-hit Flurry
                    yield return StartCoroutine(ExecuteFlurryRoutine(dmgMult, 6));
                    EndCombatAction(); yield break;
                }
            }

            // ── PHASE 1 + PHASE 2 SHARED ─────────────────────────────────────────
            if (distance > attackRange + 2.5f)
            {
                // Mid-to-Far range: Gun-Kata Burst, Shoulder Charge, or Leaping Attack
                if (Random.value < 0.45f)
                {
                    ChangeState(AIState.GunKataBurst);
                    yield return StartCoroutine(ExecuteGunKataRoutine(dmgMult));
                }
                else if (Random.value < 0.5f)
                {
                    ChangeState(AIState.ShoulderCharge);
                    yield return StartCoroutine(ExecuteShoulderChargeRoutine(dmgMult));
                }
                else if (clips.lightLeaping.clip != null)
                {
                    yield return PlaySingleAttack(clips.lightLeaping, heavyAttackDamage * dmgMult, isHeavy: true, stepForward: 3.5f);
                }
            }
            else if (choice < 0.22f)
            {
                // Insane Gun-Sword Hybrid Combo: Slash -> Slash -> Point-Blank Laser Blast -> Heavy Finisher!
                ChangeState(AIState.GunSwordCombo);
                yield return StartCoroutine(ExecuteGunSwordComboRoutine(dmgMult));
            }
            else if (choice < 0.38f)
            {
                // Overhead Slam (telegraphed, high reward)
                ChangeState(AIState.OverheadSlam);
                yield return StartCoroutine(ExecuteOverheadSlamRoutine(dmgMult));
            }
            else if (choice < 0.52f)
            {
                // Spinning Cyclone
                ChangeState(AIState.SpinningCyclone);
                yield return StartCoroutine(ExecuteSpinningCycloneRoutine(dmgMult));
            }
            else if (choice < 0.76f)
            {
                // Multi-hit light combo (1-3 hits)
                int maxHits = Random.value < comboChainChance
                    ? (Random.value < 0.5f ? 3 : 2) : 1;
                for (int hit = 0; hit < maxHits; hit++)
                {
                    var entry = clips.GetLightCombo(hit + 1);
                    if (entry.clip != null)
                    {
                        yield return PlaySingleAttack(entry,
                            (lightAttackDamage + hit * 4f) * dmgMult,
                            isHeavy: false,
                            stepForward: 1.0f);
                        if (hit < maxHits - 1)
                            yield return new WaitForSeconds(_isPhase2 ? 0.07f : 0.12f);
                    }
                }
            }
            else if (choice < 0.90f)
            {
                // Heavy combo
                int maxHits = Random.value < 0.5f ? 2 : 1;
                for (int hit = 0; hit < maxHits; hit++)
                {
                    var entry = clips.GetHeavyCombo(hit + 1);
                    if (entry.clip != null)
                    {
                        yield return PlaySingleAttack(entry,
                            (heavyAttackDamage + hit * 6f) * dmgMult,
                            isHeavy: true,
                            stepForward: 1.4f);
                    }
                }
            }
            else if (clips.heavyStab.clip != null)
            {
                // Heavy Stab finisher
                yield return PlaySingleAttack(clips.heavyStab,
                    (heavyAttackDamage + 10f) * dmgMult,
                    isHeavy: true,
                    stepForward: 2.0f);
            }

            EndCombatAction();
        }

        // ══════════════════════════════════════════════════════════════════════════
        // SPECIAL MOVE: CYBER BLINK SLASH (Phase 2)
        // Teleports 3m toward player in a smoke puff, then executes heavy stab.
        // ══════════════════════════════════════════════════════════════════════════
        private IEnumerator ExecuteBlinkSlashRoutine(float dmgMult)
        {
            _isActionLocked = true;
            animator.SetLayerWeight(combatLayerIndex, 1f);

            if (playerTarget != null)
            {
                // Smoke at origin before teleport
                SpawnVFX(vfxBlinkSmoke, transform.position + Vector3.up * 0.5f, 2.0f);

                // Teleport toward player
                Vector3 toPlayer = (playerTarget.position - transform.position);
                toPlayer.y = 0f;
                Vector3 dir         = toPlayer.normalized;
                float   moveDist    = Mathf.Min(blinkSlashTeleportDistance, toPlayer.magnitude - attackRange * 0.8f);
                if (moveDist > 0f)
                {
                    // CharacterController doesn't support instant position set, use warp
                    _cc.enabled = false;
                    transform.position += dir * moveDist;
                    _cc.enabled = true;
                }

                // Smoke at arrival
                SpawnVFX(vfxBlinkSmoke, transform.position + Vector3.up * 0.5f, 2.0f);

                // Face player
                if (toPlayer.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.LookRotation(dir);
            }

            yield return new WaitForSeconds(0.08f);

            // Execute heavy stab
            var entry = clips.heavyStab.clip != null ? clips.heavyStab : clips.GetHeavyCombo(1);
            yield return PlaySingleAttack(entry, (heavyAttackDamage + 15f) * dmgMult, isHeavy: true, stepForward: 0.5f);
        }

        // ══════════════════════════════════════════════════════════════════════════
        // SPECIAL MOVE: GUN-KATA BURST & GUN-SWORD COMBOS
        // Rapid 3-burst laser fire -> tactical combat dash -> heavy cleave finisher!
        // ══════════════════════════════════════════════════════════════════════════
        private IEnumerator ExecuteGunKataRoutine(float dmgMult)
        {
            _isActionLocked = true;
            hasSuperArmor = true;
            ChangeState(AIState.GunKataBurst);
            animator.SetLayerWeight(combatLayerIndex, 1f);

            FacePlayer();
            Vector3 aimTarget = playerTarget != null ? playerTarget.position + Vector3.up * 1.2f : transform.position + transform.forward * 4f + Vector3.up * 1.2f;

            // 3-round rapid fire burst
            for (int i = 0; i < 3; i++)
            {
                FacePlayer();
                aimTarget = playerTarget != null ? playerTarget.position + Vector3.up * 1.2f : transform.position + transform.forward * 4f + Vector3.up * 1.2f;
                FireCyberGunShot(aimTarget);
                yield return new WaitForSeconds(0.12f);
            }

            yield return new WaitForSeconds(0.10f);

            // High-speed step lunge toward player
            Vector3 dashDir = playerTarget != null ? (playerTarget.position - transform.position).normalized : transform.forward;
            dashDir.y = 0f;
            float dashTime = 0.22f;
            float elapsed = 0f;
            while (elapsed < dashTime)
            {
                elapsed += Time.deltaTime;
                _cc.Move(dashDir * 12f * Time.deltaTime);
                yield return null;
            }

            // Heavy sword follow-up strike
            var followUp = clips.heavyComboA.clip != null ? clips.heavyComboA : clips.GetHeavyCombo(1);
            if (followUp.clip != null)
            {
                yield return PlaySingleAttack(followUp, heavyAttackDamage * 1.25f * dmgMult, isHeavy: true, stepForward: 1.5f);
            }

            hasSuperArmor = false;
            EndCombatAction();
        }

        private IEnumerator ExecuteGunSwordComboRoutine(float dmgMult)
        {
            _isActionLocked = true;
            hasSuperArmor = true;
            ChangeState(AIState.GunSwordCombo);
            animator.SetLayerWeight(combatLayerIndex, 1f);

            // Slash 1
            FacePlayer();
            var l1 = clips.GetLightCombo(1);
            if (l1.clip != null) yield return PlaySingleAttack(l1, lightAttackDamage * dmgMult, isHeavy: false, stepForward: 1.2f);

            yield return new WaitForSeconds(0.08f);

            // Slash 2
            FacePlayer();
            var l2 = clips.GetLightCombo(2);
            if (l2.clip != null) yield return PlaySingleAttack(l2, (lightAttackDamage + 6f) * dmgMult, isHeavy: false, stepForward: 1.2f);

            yield return new WaitForSeconds(0.10f);

            // Point-blank gun blasts
            FacePlayer();
            for (int i = 0; i < 2; i++)
            {
                Vector3 aimTarget = playerTarget != null ? playerTarget.position + Vector3.up * 1.2f : transform.position + transform.forward * 3f + Vector3.up * 1.2f;
                FireCyberGunShot(aimTarget);
                yield return new WaitForSeconds(0.12f);
            }

            yield return new WaitForSeconds(0.08f);

            // Heavy cleave finisher
            FacePlayer();
            var hFinisher = clips.heavyComboB.clip != null ? clips.heavyComboB : clips.GetHeavyCombo(1);
            if (hFinisher.clip != null)
            {
                yield return PlaySingleAttack(hFinisher, heavyAttackDamage * 1.4f * dmgMult, isHeavy: true, stepForward: 2.0f);
            }

            hasSuperArmor = false;
            EndCombatAction();
        }

        private void FireCyberGunShot(Vector3 targetPos)
        {
            Vector3 muzzlePos = transform.position + Vector3.up * 1.3f + transform.forward * 0.45f;
            Vector3 dir = (targetPos - muzzlePos).normalized;
            Vector3 spread = Random.insideUnitSphere * 0.04f;
            dir = (dir + spread).normalized;

            Vector3 endPos = muzzlePos + dir * 25f;

            RaycastHit hit;
            if (Physics.Raycast(muzzlePos, dir, out hit, 25f, ~0, QueryTriggerInteraction.Ignore))
            {
                endPos = hit.point;
                var dmgTarget = hit.collider.GetComponentInParent<ISyntyDamageable>() ?? hit.collider.GetComponent<ISyntyDamageable>();
                if (dmgTarget != null && hit.collider.gameObject != gameObject && !hit.collider.transform.IsChildOf(transform))
                {
                    dmgTarget.TakeDamage(18f * difficultyDamageMultiplier, hit.point, dir, false);
                }
            }

            // Spawn laser tracer line renderer
            SpawnLaserTracer(muzzlePos, endPos);

            // Audio & muzzle flash
            var gunSound = CyberCombat.Audio.CyberCombatAudioSynth.GetGunShot();
            if (gunSound != null) CyberCombat.Audio.CyberCombatAudioSynth.PlayAtPoint(gunSound, muzzlePos, 0.95f);
        }

        private void SpawnLaserTracer(Vector3 start, Vector3 end)
        {
            GameObject g = new GameObject("AI_LaserTracer_FX");
            var ln = g.AddComponent<LineRenderer>();
            ln.startWidth = 0.07f; ln.endWidth = 0.02f;
            ln.material = new Material(Shader.Find("Sprites/Default"));
            ln.startColor = new Color(1f, 0.15f, 0.25f, 0.95f);
            ln.endColor = new Color(1f, 0.6f, 0.1f, 0.35f);
            ln.SetPosition(0, start);
            ln.SetPosition(1, end);
            Destroy(g, 0.08f);
        }

        // ══════════════════════════════════════════════════════════════════════════
        // SPECIAL MOVE: SPINNING CYCLONE
        // Spins in place landing cycloneHitCount hits in a wide arc.
        // ══════════════════════════════════════════════════════════════════════════
        private IEnumerator ExecuteSpinningCycloneRoutine(float dmgMult)
        {
            _isActionLocked = true;
            animator.SetLayerWeight(combatLayerIndex, 1f);

            // Use cyclone clip or fallback to lightComboC
            string clipName = (clips.spinningCyclone.clip != null)
                ? clips.spinningCyclone.clip.name
                : (clips.lightComboC.clip != null ? clips.lightComboC.clip.name : "A_Attack_LightCombo01C_Sword");

            float clipDuration = (clips.spinningCyclone.clip != null)
                ? clips.spinningCyclone.Duration
                : (clips.lightComboC.clip != null ? clips.lightComboC.Duration : 1.2f);

            animator.CrossFadeInFixedTime(clipName, 0.1f, combatLayerIndex);

            float intervalBetweenHits = clipDuration / cycloneHitCount;
            float elapsed = 0f;
            int   hitsDealt = 0;

            while (elapsed < clipDuration)
            {
                elapsed += Time.deltaTime;

                // Rotate during the spin
                transform.Rotate(Vector3.up, 360f * Time.deltaTime / clipDuration);

                // Deal one arc hit per interval
                if (hitsDealt < cycloneHitCount && elapsed >= intervalBetweenHits * (hitsDealt + 1))
                {
                    DealArcDamage(lightAttackDamage * 0.9f * dmgMult, cycloneRadius, isHeavy: false);
                    hitsDealt++;
                }

                yield return null;
            }
        }

        // ══════════════════════════════════════════════════════════════════════════
        // SPECIAL MOVE: OVERHEAD SLAM
        // Long windup (~0.8s) → massive smash with screen shake + posture damage.
        // ══════════════════════════════════════════════════════════════════════════
        private IEnumerator ExecuteOverheadSlamRoutine(float dmgMult)
        {
            _isActionLocked = true;
            animator.SetLayerWeight(combatLayerIndex, 1f);

            // Face player
            FacePlayer();

            // Play heavy slam clip (heavyComboC or overheadSlam)
            string clipName = (clips.overheadSlam.clip != null)
                ? clips.overheadSlam.clip.name
                : (clips.heavyComboC.clip != null ? clips.heavyComboC.clip.name : "A_Attack_HeavyCombo01C_Sword");
            float clipDuration = (clips.overheadSlam.clip != null)
                ? clips.overheadSlam.Duration
                : (clips.heavyComboC.clip != null ? clips.heavyComboC.Duration : 1.5f);

            animator.CrossFadeInFixedTime(clipName, 0.1f, combatLayerIndex);

            // ── Windup ── (first 55% of clip = telegraph)
            float windupTime = clipDuration * 0.55f;
            float elapsed    = 0f;
            while (elapsed < windupTime)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            // ── SLAM IMPACT ──────────────────────────────────────────────────────
            FacePlayer();
            float slamDamage = (heavyAttackDamage * 1.6f) * dmgMult;
            DealArcDamage(slamDamage, 2.8f, isHeavy: true, extraPostureDamage: 45f);
            SpawnVFX(vfxChargeImpact, transform.position + transform.forward * 1.5f, 2.0f);
            StartCoroutine(ShakeCamera(cameraShakeMagnitude * 1.4f, cameraShakeDuration * 1.2f));

            // Wait for clip to finish
            yield return new WaitForSeconds(clipDuration * 0.45f);
        }

        // ══════════════════════════════════════════════════════════════════════════
        // SPECIAL MOVE: SHOULDER CHARGE
        // Dashes shoulderChargeDashDistance meters at speed, deals damage on contact.
        // ══════════════════════════════════════════════════════════════════════════
        private IEnumerator ExecuteShoulderChargeRoutine(float dmgMult)
        {
            _isActionLocked = true;
            animator.SetLayerWeight(combatLayerIndex, 1f);

            FacePlayer();

            // Use leaping clip or shoulder charge clip
            string clipName = (clips.shoulderCharge.clip != null)
                ? clips.shoulderCharge.clip.name
                : (clips.lightLeaping.clip != null ? clips.lightLeaping.clip.name : "A_Attack_LightLeaping01_Sword");
            float clipDuration = (clips.shoulderCharge.clip != null)
                ? clips.shoulderCharge.Duration
                : (clips.lightLeaping.clip != null ? clips.lightLeaping.Duration : 1.0f);

            animator.CrossFadeInFixedTime(clipName, 0.1f, combatLayerIndex);

            Vector3 chargeDir  = transform.forward;
            float   elapsed    = 0f;
            float   chargeDuration = Mathf.Min(clipDuration, shoulderChargeDashDistance / shoulderChargeSpeed);
            bool    hitDealt   = false;

            while (elapsed < chargeDuration)
            {
                elapsed += Time.deltaTime;

                // Dash forward
                _cc.Move(chargeDir * shoulderChargeSpeed * Time.deltaTime);

                // Check contact with player during charge
                if (!hitDealt && playerTarget != null)
                {
                    float d = Vector3.Distance(transform.position, playerTarget.position);
                    if (d <= 1.6f)
                    {
                        hitDealt = true;
                        DealSwordDamage((heavyAttackDamage * 0.85f) * dmgMult, isHeavy: true);
                        SpawnVFX(vfxChargeImpact, transform.position + transform.forward * 1.0f, 2.0f);
                        StartCoroutine(ShakeCamera(cameraShakeMagnitude, cameraShakeDuration));
                        break; // Stop charging after impact
                    }
                }

                yield return null;
            }

            // Settle clip remainder
            yield return new WaitForSeconds(clipDuration - chargeDuration);
        }

        // ══════════════════════════════════════════════════════════════════════════
        // SPECIAL MOVE: POMMEL CROSS (Phase 2)
        // Short-range pommel headbutt: low damage, but forces a large posture stagger.
        // ══════════════════════════════════════════════════════════════════════════
        private IEnumerator ExecutePommelCrossRoutine(float dmgMult)
        {
            _isActionLocked = true;
            animator.SetLayerWeight(combatLayerIndex, 1f);
            FacePlayer();

            // Use pommelStrike clip or counterShove fallback
            string clipName = (clips.pommelStrike.clip != null)
                ? clips.pommelStrike.clip.name
                : (clips.counterShove.clip != null ? clips.counterShove.clip.name : "A_Parry_F_CounterShove_Sword");
            float clipDuration = (clips.pommelStrike.clip != null)
                ? clips.pommelStrike.Duration
                : (clips.counterShove.clip != null ? clips.counterShove.Duration : 0.7f);

            animator.CrossFadeInFixedTime(clipName, 0.08f, combatLayerIndex);
            yield return new WaitForSeconds(clipDuration * 0.30f);

            // Impact: low damage but massive posture hit
            DealSwordDamage(lightAttackDamage * 0.55f * dmgMult, isHeavy: false, extraPostureDamage: 55f);
            SpawnVFX(vfxSwordHit, transform.position + transform.forward * 0.8f + Vector3.up * 1.5f, 1.5f);

            yield return new WaitForSeconds(clipDuration * 0.70f);
        }

        // ══════════════════════════════════════════════════════════════════════════
        // SPECIAL MOVE: GRENADE TOSS (Phase 2)
        // Tosses a grenade arc to player position → AoE explosion after 0.8s delay.
        // ══════════════════════════════════════════════════════════════════════════
        private IEnumerator ExecuteGrenadeTossRoutine(float dmgMult)
        {
            _isActionLocked = true;
            animator.SetLayerWeight(combatLayerIndex, 1f);
            FacePlayer();

            // Use flourish clip as throw animation
            string clipName = (clips.heavyFlourish.clip != null)
                ? clips.heavyFlourish.clip.name
                : "A_Attack_HeavyFlourish01_Sword";
            float clipDuration = (clips.heavyFlourish.clip != null) ? clips.heavyFlourish.Duration : 1.0f;

            animator.CrossFadeInFixedTime(clipName, 0.12f, combatLayerIndex);

            // Wait for "throw" moment
            yield return new WaitForSeconds(clipDuration * 0.35f);

            // Record target position at time of throw
            Vector3 targetPos = (playerTarget != null)
                ? playerTarget.position
                : transform.position + transform.forward * 4f;

            // Spawn projectile VFX arc (use smoke as proxy grenade)
            if (vfxGrenadeBurst != null)
            {
                StartCoroutine(ArcGrenade(targetPos, 0.75f, dmgMult));
            }
            else
            {
                // No prefab: just deal damage after delay
                StartCoroutine(DelayedAoEDamage(targetPos, grenadeExplosionRadius, heavyAttackDamage * dmgMult, 0.75f));
            }

            // Finish throw animation
            yield return new WaitForSeconds(clipDuration * 0.65f);
        }

        private IEnumerator ArcGrenade(Vector3 targetPos, float flightTime, float dmgMult)
        {
            // Spawn a small FX at origin and lerp it toward target
            GameObject grenadeFX = SpawnVFX(vfxSwordHit, transform.position + Vector3.up * 1.2f, flightTime + 1.5f);

            if (grenadeFX != null)
            {
                Vector3 start  = grenadeFX.transform.position;
                float   elapsed = 0f;
                while (elapsed < flightTime)
                {
                    elapsed += Time.deltaTime;
                    float t  = elapsed / flightTime;
                    // Parabolic arc
                    Vector3 pos = Vector3.Lerp(start, targetPos, t);
                    pos.y += Mathf.Sin(t * Mathf.PI) * 2.5f;
                    if (grenadeFX != null) grenadeFX.transform.position = pos;
                    yield return null;
                }
            }
            else
            {
                yield return new WaitForSeconds(flightTime);
            }

            // EXPLOSION
            SpawnVFX(vfxGrenadeBurst, targetPos + Vector3.up * 0.2f, 2.5f);
            StartCoroutine(ShakeCamera(cameraShakeMagnitude * 1.2f, cameraShakeDuration));
            DealAoEDamage(targetPos, grenadeExplosionRadius, (heavyAttackDamage * 1.3f) * dmgMult, isHeavy: true);
        }

        private IEnumerator DelayedAoEDamage(Vector3 pos, float radius, float damage, float delay)
        {
            yield return new WaitForSeconds(delay);
            DealAoEDamage(pos, radius, damage, isHeavy: true);
        }

        // ══════════════════════════════════════════════════════════════════════════
        // PHASE 2 FLURRY (6 hits in Phase 2)
        // ══════════════════════════════════════════════════════════════════════════
        private IEnumerator ExecuteFlurryRoutine(float dmgMult, int hitCount = 4)
        {
            for (int i = 0; i < hitCount; i++)
            {
                var entry = clips.GetLightCombo((i % 3) + 1);
                if (entry.clip != null)
                {
                    yield return PlaySingleAttack(entry,
                        lightAttackDamage * dmgMult * 0.75f,
                        isHeavy: false,
                        stepForward: 0.8f);
                    yield return new WaitForSeconds(0.06f);
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════════════
        // CORE ATTACK PLAYBACK
        // ══════════════════════════════════════════════════════════════════════════
        private IEnumerator PlaySingleAttack(SyntyAnimClipData.ClipEntry hitEntry, float damage, bool isHeavy, float stepForward)
        {
            if (hitEntry.clip == null) yield break;

            FacePlayer();

            animator.CrossFadeInFixedTime(hitEntry.clip.name, 0.1f, combatLayerIndex);
            CyberCombat.Audio.CyberCombatAudioSynth.PlayAtPoint(
                CyberCombat.Audio.CyberCombatAudioSynth.GetSwordSlash(1, isHeavy),
                transform.position + transform.forward * 1.0f + Vector3.up,
                isHeavy ? 0.95f : 0.85f,
                Random.Range(0.95f, 1.05f)
            );
            InsaneGun.Combat.AAACyberCombatVFX.SpawnSlashArc(transform, isHeavy ? 5 : 1, isHeavy);

            float duration  = hitEntry.Duration;
            float hitTime   = duration * 0.45f;
            float elapsed   = 0f;
            bool  hitDealt  = false;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;

                if (elapsed < hitTime && stepForward > 0f)
                    _cc.Move(transform.forward * (stepForward * 2f) * Time.deltaTime);

                if (!hitDealt && elapsed >= hitTime)
                {
                    hitDealt = true;
                    DealSwordDamage(damage, isHeavy);
                }

                yield return null;
            }
        }

        // ══════════════════════════════════════════════════════════════════════════
        // DAMAGE DEALERS
        // ══════════════════════════════════════════════════════════════════════════
        private void DealSwordDamage(float damage, bool isHeavy, float extraPostureDamage = 0f)
        {
            Vector3 hitCenter = transform.position + transform.forward * 1.4f + Vector3.up * 1.0f;
            float hitRadius   = 1.3f;
            int count = Physics.OverlapSphereNonAlloc(hitCenter, hitRadius, _hitResults);

            bool hitAnything = false;
            for (int i = 0; i < count; i++)
            {
                Collider col = _hitResults[i];
                if (col == null || col.gameObject == gameObject || col.transform.IsChildOf(transform)) continue;

                var dmgTarget = col.GetComponentInParent<ISyntyDamageable>() ?? col.GetComponent<ISyntyDamageable>();
                if (dmgTarget != null)
                {
                    dmgTarget.TakeDamage(damage * difficultyDamageMultiplier, col.ClosestPoint(hitCenter), transform.forward, isHeavy);
                    hitAnything = true;
                }
            }

            // Apply extra posture damage (e.g. Pommel Cross) to self-tracked posture of enemy
            // (This normally targets the player; extra posture here means bonus stagger on the player)
            if (hitAnything)
            {
                SpawnVFX(vfxSwordHit, hitCenter, 1.5f);
                CyberCombat.Audio.CyberCombatAudioSynth.PlayAtPoint(
                    isHeavy ? CyberCombat.Audio.CyberCombatAudioSynth.GetHeavyImpact() : CyberCombat.Audio.CyberCombatAudioSynth.GetSwordImpact(),
                    hitCenter, 1f, 1f);
            }
        }

        private void DealArcDamage(float damage, float radius, bool isHeavy, float extraPostureDamage = 0f)
        {
            Vector3 hitCenter = transform.position + Vector3.up * 1.0f;
            int count = Physics.OverlapSphereNonAlloc(hitCenter, radius, _hitResults);

            bool hitAnything = false;
            for (int i = 0; i < count; i++)
            {
                Collider col = _hitResults[i];
                if (col == null || col.gameObject == gameObject || col.transform.IsChildOf(transform)) continue;
                var dmgTarget = col.GetComponentInParent<ISyntyDamageable>() ?? col.GetComponent<ISyntyDamageable>();
                if (dmgTarget != null)
                {
                    dmgTarget.TakeDamage(damage, hitCenter, transform.forward, isHeavy);
                    hitAnything = true;
                }
            }

            if (hitAnything)
            {
                SpawnVFX(vfxSwordHit, hitCenter, 1.5f);
                CyberCombat.Audio.CyberCombatAudioSynth.PlayAtPoint(
                    isHeavy ? CyberCombat.Audio.CyberCombatAudioSynth.GetHeavyImpact() : CyberCombat.Audio.CyberCombatAudioSynth.GetSwordImpact(),
                    hitCenter, 1f, 1f);
            }
        }

        private void DealAoEDamage(Vector3 origin, float radius, float damage, bool isHeavy)
        {
            int count = Physics.OverlapSphereNonAlloc(origin, radius, _hitResults);
            bool anyHit = false;
            for (int i = 0; i < count; i++)
            {
                Collider col = _hitResults[i];
                if (col == null || col.gameObject == gameObject || col.transform.IsChildOf(transform)) continue;
                var dmgTarget = col.GetComponentInParent<ISyntyDamageable>() ?? col.GetComponent<ISyntyDamageable>();
                if (dmgTarget != null)
                {
                    dmgTarget.TakeDamage(damage, col.ClosestPoint(origin), (col.transform.position - origin).normalized, isHeavy);
                    anyHit = true;
                }
            }
            if (anyHit)
            {
                CyberCombat.Audio.CyberCombatAudioSynth.PlayAtPoint(
                    isHeavy ? CyberCombat.Audio.CyberCombatAudioSynth.GetHeavyImpact() : CyberCombat.Audio.CyberCombatAudioSynth.GetSwordImpact(),
                    origin, 1f, 0.9f);
            }
        }

        // ══════════════════════════════════════════════════════════════════════════
        // DEFENSIVE REACTIONS & ISyntyDamageable
        // ══════════════════════════════════════════════════════════════════════════
        public void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitDirection, bool isHeavy)
        {
            if (_currentState == AIState.Dead) return;

            // Strict interruption: if enemy is in menacing idle, getting attacked interrupts immediately
            if (_currentState == AIState.MenacingIdle)
            {
                InterruptMenacing();
            }

            Vector3 toAttacker = -hitDirection;
            toAttacker.y = 0f;
            float angle    = Vector3.Angle(transform.forward, toAttacker);
            bool  isFrontal = angle <= defenseArc * 0.5f;

            float phase2Bonus = _isPhase2 ? 1.25f : 1f;

            if (isFrontal && _currentState != AIState.Staggered && _currentState != AIState.Knockdown && _currentState != AIState.OverdriveTransition)
            {
                float effectiveParry = Mathf.Min(0.85f, parryChance * phase2Bonus);
                if (Random.value <= effectiveParry && clips.parryForward.clip != null)
                {
                    StartCombatAction(ExecuteParryRoutine(hitDirection));
                    return;
                }

                float effectiveDodge = isHeavy
                    ? Mathf.Min(0.65f, dodgeChance * 1.5f * phase2Bonus)
                    : dodgeChance * phase2Bonus;
                if (Random.value <= effectiveDodge)
                {
                    StartCombatAction(ExecuteDodgeRoutine(hitDirection));
                    return;
                }

                float effectiveBlock = Mathf.Min(0.95f, blockChance * phase2Bonus);
                if (Random.value <= effectiveBlock && clips.blockLoop != null)
                {
                    StartCombatAction(ExecuteBlockRoutine(damage * (1f - blockMitigation), isHeavy));
                    return;
                }
            }

            ApplyDamage(damage, isHeavy, hitDirection);
        }

        private void ApplyDamage(float damage, bool isHeavy, Vector3 hitDirection = default)
        {
            if (hasSuperArmor)
            {
                // Super armor: absorb damage with 50% mitigation, do NOT play hit reaction or interrupt!
                currentHealth = Mathf.Max(0f, currentHealth - damage * 0.5f);
                OnHealthChanged?.Invoke(currentHealth, maxHealth);
                if (currentHealth <= 0f) { Die(hitDirection); return; }
                AddPosture(isHeavy ? 15f : 8f);
                SpawnVFX(vfxSwordHit, transform.position + Vector3.up * 1.2f, 0.8f);
                return;
            }

            // Poise buffer: if light attack and posture is not broken, take damage and flash, but do NOT interrupt!
            if (!isHeavy && currentPosture < maxPosture * 0.8f)
            {
                currentHealth = Mathf.Max(0f, currentHealth - damage);
                OnHealthChanged?.Invoke(currentHealth, maxHealth);
                if (currentHealth <= 0f) { Die(hitDirection); return; }
                AddPosture(15f);
                SpawnVFX(vfxSwordHit, transform.position + Vector3.up * 1.2f, 0.8f);
                return;
            }

            currentHealth = Mathf.Max(0f, currentHealth - damage);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            if (currentHealth <= 0f) { Die(hitDirection); return; }

            if (!_isPhase2 && currentHealth <= maxHealth * overdriveThreshold)
            {
                StartCombatAction(ActivateOverdrivePhase());
                return;
            }

            AddPosture(isHeavy ? 35f : 20f);

            if (_currentState != AIState.Staggered && _currentState != AIState.Knockdown)
                StartCombatAction(ExecuteHitReactRoutine(isHeavy, hitDirection));
        }

        // ══════════════════════════════════════════════════════════════════════════
        // OVERDRIVE PHASE 2 ACTIVATION
        // ══════════════════════════════════════════════════════════════════════════
        private IEnumerator ActivateOverdrivePhase()
        {
            _isPhase2      = true;
            _isActionLocked = true;
            ChangeState(AIState.OverdriveTransition);
            animator.SetLayerWeight(combatLayerIndex, 1f);

            string clip = clips.hitStaggerBack.clip != null
                ? clips.hitStaggerBack.clip.name
                : "A_Hit_B_Stagger_Sword";
            animator.CrossFadeInFixedTime(clip, 0.1f, combatLayerIndex);

            // Big burst of sparks at feet + camera shake
            SpawnVFX(vfxOverdriveBurst, transform.position, 3.0f);
            StartCoroutine(ShakeCamera(cameraShakeMagnitude * 1.6f, cameraShakeDuration * 1.5f));
            OnOverdriveActivated?.Invoke();

            walkSpeed   *= overdriveMoveMultiplier;
            runSpeed    *= overdriveMoveMultiplier;
            strafeSpeed *= overdriveMoveMultiplier;
            minAttackCooldown = Mathf.Max(0.4f, minAttackCooldown * 0.65f);
            maxAttackCooldown = Mathf.Max(0.8f, maxAttackCooldown * 0.65f);

            yield return new WaitForSeconds(2.0f);
            EndCombatAction();
        }

        private void AddPosture(float amount)
        {
            currentPosture += amount;
            OnPostureChanged?.Invoke(currentPosture, maxPosture);
            if (currentPosture >= maxPosture)
            {
                if (_isPhase2 || currentHealth < maxHealth * 0.5f)
                    StartCombatAction(ExecuteKnockdownRoutine());
                else
                    StartCombatAction(ExecuteStaggerRoutine());
            }
        }

        private IEnumerator ExecuteBlockRoutine(float mitigatedDamage, bool isHeavy)
        {
            _isActionLocked = true;
            ChangeState(AIState.Block);
            animator.SetLayerWeight(combatLayerIndex, 1f);

            currentHealth = Mathf.Max(0f, currentHealth - mitigatedDamage);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            AddPosture(isHeavy ? 45f : 25f);

            if (currentHealth <= 0f) { Die(); yield break; }

            string clipName = clips.blockBegin != null ? clips.blockBegin.name : "A_Block_Begin_Sword";
            animator.CrossFadeInFixedTime(clipName, 0.08f, combatLayerIndex);

            SpawnVFX(vfxBlockImpact, transform.position + transform.forward * 0.5f + Vector3.up * 1.2f, 1.5f);

            float knockback = isHeavy ? 2.5f : 1.0f;
            float t = 0f;
            while (t < 0.35f)
            {
                t += Time.deltaTime;
                _cc.Move(-transform.forward * knockback * (1f - t / 0.35f) * Time.deltaTime);
                yield return null;
            }

            yield return new WaitForSeconds(0.15f);
            EndCombatAction();
        }

        private IEnumerator ExecuteParryRoutine(Vector3 hitDirection)
        {
            _isActionLocked = true;
            ChangeState(AIState.ParryCounter);
            animator.SetLayerWeight(combatLayerIndex, 1f);

            string parryClip = clips.parryForward.clip != null
                ? clips.parryForward.clip.name : "A_Parry_F_Sword";
            animator.CrossFadeInFixedTime(parryClip, 0.08f, combatLayerIndex);
            yield return new WaitForSeconds(clips.parryForward.Duration * 0.35f);

            string counterClip = clips.counterShove.clip != null
                ? clips.counterShove.clip.name : "A_Parry_F_CounterShove_Sword";
            animator.CrossFadeInFixedTime(counterClip, 0.08f, combatLayerIndex);
            yield return new WaitForSeconds(clips.counterShove.Duration * 0.30f);

            DealSwordDamage(heavyAttackDamage, isHeavy: true);

            if (_isPhase2)
            {
                yield return new WaitForSeconds(0.18f);
                var followUp = clips.GetLightCombo(2);
                if (followUp.clip != null)
                {
                    animator.CrossFadeInFixedTime(followUp.clip.name, 0.07f, combatLayerIndex);
                    yield return new WaitForSeconds(followUp.Duration * 0.40f);
                    DealSwordDamage(lightAttackDamage * overdriveDamageMultiplier, isHeavy: false);
                    yield return new WaitForSeconds(followUp.Duration * 0.35f);
                }
            }
            else
            {
                yield return new WaitForSeconds(clips.counterShove.Duration * 0.40f);
            }

            EndCombatAction();
        }

        // ── Intelligent Directional Dodge Evaluation ──────────────────────────────
        private CardinalDirection EvaluateBestDodgeDirection(Vector3 incomingAttackDir)
        {
            CardinalDirection[] candidates = new CardinalDirection[] {
                CardinalDirection.Back,
                CardinalDirection.Left,
                CardinalDirection.Right,
                CardinalDirection.Front
            };

            float bestScore = -9999f;
            CardinalDirection bestDir = CardinalDirection.Back;

            foreach (var dir in candidates)
            {
                Vector3 moveWorld;
                switch (dir)
                {
                    case CardinalDirection.Left:  moveWorld = -transform.right; break;
                    case CardinalDirection.Right: moveWorld = transform.right; break;
                    case CardinalDirection.Front: moveWorld = transform.forward; break;
                    default:                      moveWorld = -transform.forward; break;
                }

                float score = 0f;

                // 1. Evade incoming attack trajectory (lateral flanking dodges score highest)
                if (incomingAttackDir.sqrMagnitude > 0.01f)
                {
                    float attackDot = Vector3.Dot(moveWorld, incomingAttackDir.normalized);
                    score += (1f - Mathf.Abs(attackDot)) * 30f;
                    if (Vector3.Dot(moveWorld, -incomingAttackDir.normalized) > 0.5f)
                        score -= 40f; // Don't dodge into attack!
                }
                else
                {
                    if (dir == CardinalDirection.Left || dir == CardinalDirection.Right) score += 20f;
                }

                // 2. Maintain combat orbit distance (~2.5m - 4.5m)
                if (playerTarget != null)
                {
                    Vector3 projectedPos = transform.position + moveWorld * 2.5f;
                    float projectedDist = Vector3.Distance(projectedPos, playerTarget.position);
                    if (projectedDist >= 2.5f && projectedDist <= 4.8f) score += 25f;
                    else if (projectedDist < 2.0f) score -= 35f;
                }

                // 3. Obstacle clearance
                RaycastHit hit;
                if (Physics.SphereCast(transform.position + Vector3.up * 0.9f, 0.4f, moveWorld, out hit, 2.5f, ~0, QueryTriggerInteraction.Ignore))
                {
                    score -= 50f;
                }
                else
                {
                    score += 15f;
                }

                // 4. Variety
                if (dir == _lastEnemyDodgeDir) score -= 15f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestDir   = dir;
                }
            }

            _lastEnemyDodgeDir = bestDir;
            return bestDir;
        }

        private IEnumerator ExecuteDodgeRoutine(Vector3 incomingAttackDir = default)
        {
            _isActionLocked = true;
            ChangeState(AIState.Dodge);
            animator.SetLayerWeight(combatLayerIndex, 1f);

            CardinalDirection bestDir = EvaluateBestDodgeDirection(incomingAttackDir);
            bool roll = Random.value < 0.35f;
            var dodgeEntry = SyntyCombatAnimationResolver.GetDodge(clips, bestDir, roll);
            string clipName = (dodgeEntry.clip != null) ? dodgeEntry.clip.name : "A_Dodge_B_Sword";

            animator.CrossFadeInFixedTime(clipName, 0.1f, combatLayerIndex);

            Vector3 moveDir;
            switch (bestDir)
            {
                case CardinalDirection.Left:  moveDir = -transform.right; break;
                case CardinalDirection.Right: moveDir = transform.right; break;
                case CardinalDirection.Front: moveDir = transform.forward; break;
                default:                      moveDir = -transform.forward; break;
            }

            float duration   = dodgeEntry.Duration > 0.1f ? dodgeEntry.Duration : 0.6f;
            float dodgeSpeed = roll ? 7.0f : 5.0f;
            float elapsed    = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                if (elapsed < duration * 0.65f)
                    _cc.Move(moveDir * dodgeSpeed * Time.deltaTime);
                yield return null;
            }

            EndCombatAction();
        }

        // ── Menacing Idle with Strict Interruption Rules ──────────────────────────
        private bool CanPlayPersonalityIdle()
        {
            if (_isActionLocked || _currentState != AIState.StalkOrbit) return false;
            if (playerTarget == null) return false;
            float dist = Vector3.Distance(transform.position, playerTarget.position);
            if (dist < 3.2f || dist > 6.0f) return false;
            if (Time.time < _nextPersonalityIdleTime) return false;
            if (Time.time >= _nextAttackTime - 1.5f) return false;
            return true;
        }

        private bool ShouldInterruptMenacing()
        {
            if (_currentState != AIState.MenacingIdle) return true;
            if (playerTarget == null) return true;
            float dist = Vector3.Distance(transform.position, playerTarget.position);
            if (dist < 2.8f) return true;

            var playerCtrl = playerTarget.GetComponent<SyntySwordLocomotionController>();
            if (playerCtrl != null && playerCtrl.IsInCombat) return true;

            if (Time.time >= _nextAttackTime) return true;

            return false;
        }

        private void InterruptMenacing()
        {
            _nextPersonalityIdleTime = Time.time + Random.Range(6f, 12f);
            EndCombatAction();
        }

        private IEnumerator ExecuteIntroRoutine()
        {
            _isActionLocked = true;
            ChangeState(AIState.Intro);

            // Phase 1: Ensure sword is sheathed initially
            if (weaponSocket != null) weaponSocket.SetDrawn(false);
            if (animator != null && clips != null) animator.SetBool(clips.paramSwordDrawn, false);

            // Phase 2: Walk toward the player until within intimidation/duel range (~5.5m)
            float approachDist = 5.5f;
            float maxWalkTime = 4.0f;
            float walkTimer = 0f;

            while (walkTimer < maxWalkTime)
            {
                if (playerTarget == null) FindPlayerTarget();
                if (playerTarget == null) break;

                Vector3 toP = playerTarget.position - transform.position;
                toP.y = 0f;
                float dist = toP.magnitude;

                if (dist <= approachDist) break;

                // Face target
                Vector3 moveDir = toP.normalized;
                Quaternion targetRot = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);

                ApplyMovement(moveDir * walkSpeed);
                UpdateAnimatorLocomotion(moveDir, walkSpeed);

                walkTimer += Time.deltaTime;
                yield return null;
            }

            // Stop walking locomotion
            UpdateAnimatorLocomotion(Vector3.zero, 0f);
            FacePlayer();

            // Phase 3: Play Draw Sword animation
            if (clips != null && clips.drawSword != null && animator != null)
            {
                _combatLayerWeight = 1f;
                animator.SetLayerWeight(combatLayerIndex, 1f);
                animator.CrossFadeInFixedTime(clips.drawSword.name, 0.12f, combatLayerIndex);

                float drawDuration = clips.drawSword.length;
                float swapTime = drawDuration * Mathf.Clamp01(clips.drawSwapNormTime > 0f ? clips.drawSwapNormTime : 0.35f);
                float elapsed = 0f;
                bool swapped = false;

                while (elapsed < drawDuration)
                {
                    FacePlayer();
                    ApplyGravityOnly();

                    if (!swapped && elapsed >= swapTime)
                    {
                        swapped = true;
                        if (weaponSocket != null) weaponSocket.SetDrawn(true);
                        animator.SetBool(clips.paramSwordDrawn, true);
                    }

                    elapsed += Time.deltaTime;
                    yield return null;
                }

                if (!swapped)
                {
                    if (weaponSocket != null) weaponSocket.SetDrawn(true);
                    animator.SetBool(clips.paramSwordDrawn, true);
                }
            }
            else
            {
                if (weaponSocket != null) weaponSocket.SetDrawn(true);
                if (animator != null && clips != null) animator.SetBool(clips.paramSwordDrawn, true);
            }

            // Phase 4: Intimidating Sword Swing / Flourish
            AnimationClip flourishClip = null;
            if (clips != null)
            {
                if (clips.heavyFlourish.clip != null) flourishClip = clips.heavyFlourish.clip;
                else if (clips.swordIdleFlourish != null) flourishClip = clips.swordIdleFlourish;
                else if (clips.heavyComboA.clip != null) flourishClip = clips.heavyComboA.clip;
            }

            if (flourishClip != null && animator != null)
            {
                animator.CrossFadeInFixedTime(flourishClip.name, 0.12f, combatLayerIndex);
                float elapsed = 0f;
                while (elapsed < flourishClip.length)
                {
                    FacePlayer();
                    ApplyGravityOnly();
                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }

            // Phase 5: Menacing Stance
            var beginClip = SyntyCombatAnimationResolver.GetIdle(clips, IdlePersonalityType.MenacingBegin);
            var loopClip  = SyntyCombatAnimationResolver.GetIdle(clips, IdlePersonalityType.MenacingLoop);
            if (beginClip != null && animator != null)
            {
                animator.CrossFadeInFixedTime(beginClip.name, 0.12f, combatLayerIndex);
                float elapsed = 0f;
                while (elapsed < beginClip.length)
                {
                    FacePlayer();
                    ApplyGravityOnly();
                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }

            if (loopClip != null && animator != null)
            {
                animator.CrossFadeInFixedTime(loopClip.name, 0.15f, combatLayerIndex);
                float elapsed = 0f;
                while (elapsed < 1.2f)
                {
                    FacePlayer();
                    ApplyGravityOnly();
                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }

            // Phase 6: Enter Active Combat
            EndCombatAction();
        }

        private IEnumerator ExecuteMenacingIdleRoutine()
        {
            _isActionLocked = true;
            ChangeState(AIState.MenacingIdle);
            _combatLayerWeight = 1f;
            if (animator != null && animator.isActiveAndEnabled)
                animator.SetLayerWeight(combatLayerIndex, 1f);

            var beginClip = SyntyCombatAnimationResolver.GetIdle(clips, IdlePersonalityType.MenacingBegin);
            var loopClip  = SyntyCombatAnimationResolver.GetIdle(clips, IdlePersonalityType.MenacingLoop);
            var endClip   = SyntyCombatAnimationResolver.GetIdle(clips, IdlePersonalityType.MenacingEnd);

            bool interrupted = false;

            if (beginClip != null)
            {
                animator.CrossFadeInFixedTime(beginClip.name, 0.12f, combatLayerIndex);
                float elapsed = 0f;
                while (elapsed < beginClip.length)
                {
                    if (ShouldInterruptMenacing()) { interrupted = true; break; }
                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }

            if (!interrupted && loopClip != null)
            {
                animator.CrossFadeInFixedTime(loopClip.name, 0.15f, combatLayerIndex);
                float loopDuration = Random.Range(2.0f, 3.5f);
                float elapsed = 0f;
                while (elapsed < loopDuration)
                {
                    if (ShouldInterruptMenacing()) { interrupted = true; break; }
                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }

            if (!interrupted && endClip != null)
            {
                animator.CrossFadeInFixedTime(endClip.name, 0.15f, combatLayerIndex);
                float elapsed = 0f;
                while (elapsed < endClip.length)
                {
                    if (ShouldInterruptMenacing()) { interrupted = true; break; }
                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }

            _nextPersonalityIdleTime = Time.time + Random.Range(8f, 16f);
            EndCombatAction();
        }

        // ── Knockdown Lifecycle ───────────────────────────────────────────────────
        private IEnumerator ExecuteKnockdownRoutine()
        {
            _isActionLocked = true;
            ChangeState(AIState.Knockdown);
            animator.SetLayerWeight(combatLayerIndex, 1f);

            var begin = SyntyCombatAnimationResolver.GetKnockdown(clips, KnockdownPhase.Begin);
            var loop  = SyntyCombatAnimationResolver.GetKnockdown(clips, KnockdownPhase.Loop);
            var end   = SyntyCombatAnimationResolver.GetKnockdown(clips, KnockdownPhase.End);

            string beginName = begin != null ? begin.name : "A_KnockDown_Begin_Sword";
            animator.CrossFadeInFixedTime(beginName, 0.10f, combatLayerIndex);
            yield return new WaitForSeconds(begin != null ? begin.length : 1.2f);

            string loopName = loop != null ? loop.name : "A_KnockDown_Loop_Sword";
            animator.CrossFadeInFixedTime(loopName, 0.10f, combatLayerIndex);
            yield return new WaitForSeconds(1.2f); // Ground duration

            string endName = end != null ? end.name : "A_KnockDown_End_Sword";
            animator.CrossFadeInFixedTime(endName, 0.10f, combatLayerIndex);
            yield return new WaitForSeconds(end != null ? end.length : 1.5f);

            currentPosture = 0f;
            OnPostureChanged?.Invoke(currentPosture, maxPosture);
            EndCombatAction();
        }

        private IEnumerator ExecuteStaggerRoutine()
        {
            _isActionLocked = true;
            ChangeState(AIState.Staggered);
            animator.SetLayerWeight(combatLayerIndex, 1f);

            var entry = SyntyCombatAnimationResolver.GetHitStagger(clips, CardinalDirection.Back);
            string staggerClip = (entry.clip != null) ? entry.clip.name : "A_Hit_B_Stagger_Sword";
            animator.CrossFadeInFixedTime(staggerClip, 0.1f, combatLayerIndex);

            yield return new WaitForSeconds(postureBreakDuration);

            currentPosture = 0f;
            OnPostureChanged?.Invoke(currentPosture, maxPosture);
            EndCombatAction();
        }

        private IEnumerator ExecuteHitReactRoutine(bool isHeavy, Vector3 hitDir = default)
        {
            _isActionLocked = true;
            ChangeState(AIState.HitReact);
            animator.SetLayerWeight(combatLayerIndex, 1f);

            Vector3 local = hitDir.sqrMagnitude > 0.01f ? transform.InverseTransformDirection(hitDir) : -Vector3.forward;
            CardinalDirection dir = SyntyCombatAnimationResolver.GetDominantDirection(local);

            var entry = isHeavy
                ? SyntyCombatAnimationResolver.GetHitStagger(clips, dir)
                : SyntyCombatAnimationResolver.GetHitReaction(clips, dir);
            string clip = (entry.clip != null)
                ? entry.clip.name
                : (isHeavy ? "A_Hit_B_Stagger_Sword" : "A_Hit_L_React_Sword");

            animator.CrossFadeInFixedTime(clip, 0.08f, combatLayerIndex);
            yield return new WaitForSeconds(entry.Duration * 0.8f);

            EndCombatAction();
        }

        private void Die(Vector3 hitDir = default)
        {
            CyberCombat.Coordination.EnemyGroupCombatCoordinator.Instance?.UnregisterCombatant(this);
            ChangeState(AIState.Dead);
            _isActionLocked = true;
            if (_activeActionCoroutine != null) StopCoroutine(_activeActionCoroutine);

            Vector3 fatalDir = hitDir != Vector3.zero ? hitDir : transform.forward;
            Vector3 fatalPoint = transform.position + Vector3.up * 1.1f;

            SpawnVFX(vfxDeath, fatalPoint, 4.0f);

            var ragdoll = GetComponent<InsaneGun.Combat.EuphoriaRagdollController>();
            if (ragdoll == null) ragdoll = gameObject.AddComponent<InsaneGun.Combat.EuphoriaRagdollController>();
            ragdoll.TriggerEuphoriaRagdoll(fatalPoint, fatalDir, 18f, isFatal: true);
        }

        private IEnumerator DirectionalRecoilImpulseRoutine(Vector3 hitDir)
        {
            var ragdoll = GetComponent<InsaneGun.Combat.EuphoriaRagdollController>();
            if (ragdoll != null && ragdoll.IsRagdoll) yield break;

            float duration = 0.35f;
            float elapsed = 0f;
            Vector3 pushDir = hitDir.sqrMagnitude > 0.01f ? hitDir.normalized : -transform.forward;
            pushDir.y = 0f;
            if (pushDir.sqrMagnitude < 0.01f) pushDir = -transform.forward;

            float pushForce = 2.2f;

            while (elapsed < duration && _currentState == AIState.Dead)
            {
                elapsed += Time.deltaTime;
                float speed = Mathf.Lerp(pushForce, 0f, elapsed / duration);
                transform.position += pushDir * (speed * Time.deltaTime);
                yield return null;
            }
        }

        private IEnumerator FreezeAnimatorAfterDeath(float duration)
        {
            yield return new WaitForSeconds(Mathf.Max(0.8f, duration - 0.1f));
            if (animator != null && _currentState == AIState.Dead)
            {
                animator.speed = 0f;
            }
        }

        // ══════════════════════════════════════════════════════════════════════════
        // VFX HELPERS
        // ══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Spawns a VFX prefab at <paramref name="position"/> and auto-destroys it
        /// after <paramref name="lifetime"/> seconds. Returns the spawned GameObject.
        /// </summary>
        private GameObject SpawnVFX(GameObject prefab, Vector3 position, float lifetime)
        {
            if (prefab == null) return null;
            GameObject fx = Object.Instantiate(prefab, position, Quaternion.identity);
            Object.Destroy(fx, lifetime);
            return fx;
        }

        // ══════════════════════════════════════════════════════════════════════════
        // CAMERA SHAKE
        // ══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Coroutine-based positional shake on Camera.main. No Cinemachine required.
        /// </summary>
        private IEnumerator ShakeCamera(float magnitude, float duration)
        {
            Camera cam = Camera.main;
            if (cam == null) yield break;

            Vector3 originalPos = cam.transform.localPosition;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float strength = Mathf.Lerp(magnitude, 0f, elapsed / duration);
                cam.transform.localPosition = originalPos
                    + new Vector3(
                        Random.Range(-1f, 1f) * strength,
                        Random.Range(-1f, 1f) * strength,
                        0f);
                yield return null;
            }

            cam.transform.localPosition = originalPos;
        }

        // ══════════════════════════════════════════════════════════════════════════
        // HELPERS
        // ══════════════════════════════════════════════════════════════════════════
        private void FacePlayer()
        {
            if (playerTarget == null) return;
            Vector3 toP = playerTarget.position - transform.position;
            toP.y = 0f;
            if (toP.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(toP);
        }

        private void StartCombatAction(IEnumerator routine)
        {
            if (_activeActionCoroutine != null) StopCoroutine(_activeActionCoroutine);
            _combatLayerWeight = 1f;
            if (animator != null && animator.isActiveAndEnabled)
                animator.SetLayerWeight(combatLayerIndex, 1f);
            _activeActionCoroutine = StartCoroutine(routine);
        }

        private void EndCombatAction()
        {
            CyberCombat.Coordination.EnemyGroupCombatCoordinator.Instance?.NotifyAttackFinished(this);
            _isActionLocked = false;
            _nextAttackTime = Time.time + Random.Range(minAttackCooldown, maxAttackCooldown);
            _combatLayerWeight = 0f;
            if (animator != null && animator.isActiveAndEnabled)
            {
                animator.SetLayerWeight(combatLayerIndex, 0f);
                animator.CrossFadeInFixedTime("Combat_Empty", 0.15f, combatLayerIndex);
            }
            ChangeState(AIState.StalkOrbit);
        }

        private void UpdateLayerWeights()
        {
            if (animator == null || !animator.isActiveAndEnabled) return;

            bool isDead = _currentState == AIState.Dead;
            bool inCombatAction = _isActionLocked || isDead ||
                                 _currentState == AIState.Attack ||
                                 _currentState == AIState.Block ||
                                 _currentState == AIState.ParryCounter ||
                                 _currentState == AIState.Dodge ||
                                 _currentState == AIState.HitReact ||
                                 _currentState == AIState.Staggered ||
                                 _currentState == AIState.Knockdown ||
                                 _currentState == AIState.OverdriveTransition ||
                                 _currentState == AIState.BlinkSlash ||
                                 _currentState == AIState.PommelCross ||
                                 _currentState == AIState.GrenadeToss ||
                                 _currentState == AIState.ShoulderCharge ||
                                 _currentState == AIState.OverheadSlam ||
                                 _currentState == AIState.SpinningCyclone ||
                                 _currentState == AIState.GunKataBurst ||
                                 _currentState == AIState.GunSwordCombo ||
                                 _currentState == AIState.Intro ||
                                 _currentState == AIState.MenacingIdle;

            float targetCombat = inCombatAction ? 1f : 0f;
            _combatLayerWeight = Mathf.MoveTowards(_combatLayerWeight, targetCombat, 10f * Time.deltaTime);
            animator.SetLayerWeight(combatLayerIndex, _combatLayerWeight);

            // Stance & Grip (Layers 1 & 2)
            bool armActive = !inCombatAction && !isDead;
            float targetArm  = armActive ? 0.70f : 0f;
            float targetHand = armActive ? 1.00f : 0f;

            _swordArmLayerWeight  = Mathf.MoveTowards(_swordArmLayerWeight, targetArm, 8f * Time.deltaTime);
            _swordHandLayerWeight = Mathf.MoveTowards(_swordHandLayerWeight, targetHand, 10f * Time.deltaTime);

            animator.SetLayerWeight(swordArmLayerIndex, _swordArmLayerWeight);
            animator.SetLayerWeight(swordHandLayerIndex, _swordHandLayerWeight);
        }

        private void ChangeState(AIState newState)
        {
            _currentState = newState;
            OnStateChanged?.Invoke(newState);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, idealOrbitDistance);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);

            Gizmos.color = new Color(1f, 0.5f, 0f);
            Gizmos.DrawWireSphere(transform.position, cycloneRadius);

            Gizmos.color = Color.yellow;
            Vector3 leftArc  = Quaternion.Euler(0f, -defenseArc * 0.5f, 0f) * transform.forward;
            Vector3 rightArc = Quaternion.Euler(0f,  defenseArc * 0.5f, 0f) * transform.forward;
            Gizmos.DrawRay(transform.position + Vector3.up, leftArc  * 2f);
            Gizmos.DrawRay(transform.position + Vector3.up, rightArc * 2f);

            // Shoulder charge range
            Gizmos.color = Color.magenta;
            Gizmos.DrawRay(transform.position, transform.forward * shoulderChargeDashDistance);
        }
    }
}
