using System.Collections;
using UnityEngine;
using SyntyCombined;

namespace PolygonMech
{
    public enum BossState
    {
        Spawning,
        Chasing,
        MeleeSwipe,
        GroundSlam,
        SpinAttack,
        SlideAttack,
        ChargeAttack,
        SeismicStomp,
        MissileBarrage,
        GatlingBurst,
        Staggered,
        EnragedTransition,
        Dead
    }

    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Animator))]
    public class BossMechAI : MonoBehaviour, ISyntyDamageable
    {
        [Header("Boss Identity")]
        public string bossName = "GOLIATH-02 // TITAN BLADE MECH";
        public float maxHealth = 3500f;
        public float currentHealth { get; private set; }

        [Header("Target & Movement")]
        [SerializeField] private Transform target;
        [SerializeField] private float walkSpeed        = 4.5f;
        [SerializeField] private float runSpeed         = 7.5f;
        [SerializeField] private float turnSpeed        = 5.5f;
        [SerializeField] private float gravity          = 18f;

        [Header("Combat Distances & Spacing")]
        [SerializeField] private float meleeRange           = 7.5f;
        [SerializeField] private float idealOrbitDistance   = 7.0f;
        [SerializeField] private float chargeMinRange       = 11.0f;
        [SerializeField] private float missileRange         = 20.0f;
        [SerializeField] private float crowdSpacingDistance = 5.5f;
        [SerializeField] private float minHardBufferDistance = 4.8f;

        [Header("Attack Cooldowns")]
        [SerializeField] private float phase1Cooldown = 1.3f;   // Fluid aggressive duel pacing
        [SerializeField] private float phase2Cooldown = 0.5f;   // Relentless in phase 2 overdrive

        [Header("Super Armor & Poise System")]
        [SerializeField] private float maxPoise                    = 600f;
        [SerializeField] private float poiseDecayRate              = 20f;
        [SerializeField] private float poiseDecayDelay             = 5.0f;
        [SerializeField] private float criticalVulnerabilityMultiplier = 1.8f;
        public  float currentPoise { get; private set; } = 0f;
        private float lastDamageTime = 0f;
        private bool  isStaggered    = false;

        [Header("VFX Prefabs")]
        [SerializeField] private GameObject groundSlamFXPrefab;
        [SerializeField] private GameObject missileBarrageFXPrefab;
        [SerializeField] private GameObject exhaustFlameFXPrefab;
        [SerializeField] private GameObject sparksDeathFXPrefab;

        [Header("VFX Spawn Points")]
        [SerializeField] private Transform groundSlamPoint;
        [SerializeField] private Transform shoulderPodLeft;
        [SerializeField] private Transform shoulderPodRight;
        [SerializeField] private Transform exhaustVentLeft;
        [SerializeField] private Transform exhaustVentRight;

        // Components
        private CharacterController controller;
        private Animator            anim;
        private BossHealthBar       healthBar;
        public  TitanMechProceduralMaster proceduralMaster { get; private set; }
        public  MechBladeCombatVFX         bladeVFX         { get; private set; }

        // State Machine
        public  BossState currentState { get; private set; } = BossState.Spawning;
        private bool  isPhase2           = false;
        private bool  isAttacking        = false;
        private float nextAttackTime     = 0f;
        private float verticalVelocity   = 0f;

        // Tactical Movement / Orbiting
        private float orbitDirection      = 1f;
        private float nextOrbitSwitchTime = 0f;

        // Phase 2 aggression: consecutive attack chains
        private int comboChainCount = 0;

        // Animator Parameter Hashes (Locomotion & Backward Compatible)
        private static readonly int ParamMoveX         = Animator.StringToHash("MoveX");
        private static readonly int ParamMoveZ         = Animator.StringToHash("MoveZ");
        private static readonly int ParamSpeed         = Animator.StringToHash("Speed");
        private static readonly int ParamMeleeSwipe    = Animator.StringToHash("MeleeSwipe");
        private static readonly int ParamGroundSlam    = Animator.StringToHash("GroundSlam");
        private static readonly int ParamSpinAttack    = Animator.StringToHash("SpinAttack");
        private static readonly int ParamSlideAttack   = Animator.StringToHash("SlideAttack");
        private static readonly int ParamSeismicStomp  = Animator.StringToHash("SeismicStomp");
        private static readonly int ParamMissileBarrage= Animator.StringToHash("MissileBarrage");
        private static readonly int ParamGatlingBurst  = Animator.StringToHash("GatlingBurst");
        private static readonly int ParamRoar          = Animator.StringToHash("Roar");
        private static readonly int ParamStagger       = Animator.StringToHash("Stagger");
        private static readonly int ParamDie           = Animator.StringToHash("Die");

        // 32-Move Mechanical Arsenal Hashes
        private static readonly int ParamSlashCleave     = Animator.StringToHash("SlashCleave");
        private static readonly int ParamSlashOverhead   = Animator.StringToHash("SlashOverhead");
        private static readonly int ParamSlashLaunch     = Animator.StringToHash("SlashLaunch");
        private static readonly int ParamSlashCrossCut   = Animator.StringToHash("SlashCrossCut");
        private static readonly int ParamSlashDecapitate = Animator.StringToHash("SlashDecapitate");
        private static readonly int ParamPistonThrust    = Animator.StringToHash("PistonThrust");
        private static readonly int ParamCycloneSpin     = Animator.StringToHash("CycloneSpin");
        private static readonly int ParamRocketDash      = Animator.StringToHash("RocketDash");
        private static readonly int ParamJumpCleave      = Animator.StringToHash("JumpCleave");
        private static readonly int ParamPneumaticPunch  = Animator.StringToHash("PneumaticPunch");
        private static readonly int ParamClawSwipe       = Animator.StringToHash("ClawSwipe");
        private static readonly int ParamHydraulicKick   = Animator.StringToHash("HydraulicKick");
        private static readonly int ParamSeismicHeel     = Animator.StringToHash("SeismicHeel");
        private static readonly int ParamBladeShield     = Animator.StringToHash("BladeShield");
        private static readonly int ParamHeavyBrace      = Animator.StringToHash("HeavyBrace");
        private static readonly int ParamRiposteParry    = Animator.StringToHash("RiposteParry");
        private static readonly int ParamTurn180         = Animator.StringToHash("Turn180");
        private static readonly int ParamTurn90R         = Animator.StringToHash("Turn90R");
        private static readonly int ParamTurn45L         = Animator.StringToHash("Turn45L");
        private static readonly int ParamReactorPowerUp  = Animator.StringToHash("ReactorPowerUp");

        private void Awake() => EnsureInitialized();

        public void EnsureInitialized()
        {
            if (controller == null) controller = GetComponent<CharacterController>();
            if (anim       == null) anim       = GetComponent<Animator>();
            if (anim       != null) anim.speed = 0.95f;
            if (healthBar  == null) healthBar  = GetComponent<BossHealthBar>() ?? gameObject.AddComponent<BossHealthBar>();

            if (proceduralMaster == null)
            {
                proceduralMaster = GetComponent<TitanMechProceduralMaster>() ?? gameObject.AddComponent<TitanMechProceduralMaster>();
                proceduralMaster.target = target;
            }

            if (bladeVFX == null)
            {
                bladeVFX = GetComponent<MechBladeCombatVFX>() ?? gameObject.AddComponent<MechBladeCombatVFX>();
            }

            if (currentHealth <= 0f) currentHealth = maxHealth;
            if (healthBar != null)
            {
                healthBar.bossName = bossName;
                healthBar.Initialize(maxHealth);
                healthBar.SetPosture(currentPoise, maxPoise);
            }

            if (target == null)
            {
                GameObject p = GameObject.Find("Alita_03_Final_NanoBerserker")
                            ?? GameObject.Find("Alita_BattleAngel_Hero")
                            ?? GameObject.Find("Alita")
                            ?? GameObject.Find("Alitha")
                            ?? GameObject.Find("Player_LucyCombat")
                            ?? GameObject.Find("PF_DualKatanaPlayer")
                            ?? GameObject.Find("PF_SyntySwordPlayer")
                            ?? GameObject.FindGameObjectWithTag("Player");
                if (p != null) target = p.transform;
            }
        }

        public bool isTargetSmallCombatant
        {
            get
            {
                if (target == null) return false;
                var cc = target.GetComponent<CharacterController>();
                if (cc != null && cc.height < 2.5f) return true;
                var col = target.GetComponent<Collider>();
                if (col != null && col.bounds.size.y < 2.5f) return true;
                return target.name.Contains("Alita") || target.name.Contains("Alitha") || target.name.Contains("Player");
            }
        }

        private void Start()
        {
            FindTarget();
            StartCoroutine(SpawnSequence());
        }

        private void Update()
        {
            if (currentState == BossState.Dead) return;

            if (target == null || !target.gameObject.activeInHierarchy)
                FindTarget();

            ApplyGravity();

            // Poise decay
            if (!isStaggered && Time.time - lastDamageTime > poiseDecayDelay && currentPoise > 0f)
            {
                currentPoise = Mathf.Max(0f, currentPoise - poiseDecayRate * Time.deltaTime);
                healthBar?.SetPosture(currentPoise, maxPoise);
            }

            if (currentState == BossState.Chasing)
                HandleTacticalLocomotion();
        }

        private void FindTarget()
        {
            GameObject p = GameObject.Find("Player_LucyCombat")
                        ?? GameObject.Find("PF_DualKatanaPlayer")
                        ?? GameObject.Find("PF_SyntySwordPlayer")
                        ?? GameObject.FindGameObjectWithTag("Player");
            if (p != null) target = p.transform;
        }

        private void ApplyGravity()
        {
            if (controller.isGrounded) verticalVelocity = -2f;
            else verticalVelocity -= gravity * Time.deltaTime;
            controller.Move(new Vector3(0, verticalVelocity * Time.deltaTime, 0));
        }

        private IEnumerator SpawnSequence()
        {
            currentState = BossState.Spawning;
            anim.SetFloat(ParamMoveX, 0f);
            anim.SetFloat(ParamMoveZ, 0f);
            anim.SetFloat(ParamSpeed, 0f);

            proceduralMaster?.SetThrusterBoost(0.6f);

            float approachDistance = 7.5f;
            float maxApproachTime  = 4.0f;
            float timer = 0f;

            while (timer < maxApproachTime)
            {
                if (target == null) FindTarget();
                if (target == null) break;

                Vector3 toTarget = (target.position - transform.position); toTarget.y = 0;
                float distance = toTarget.magnitude;
                if (distance <= approachDistance) break;

                Vector3 fwd = toTarget.normalized;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(fwd), Time.deltaTime * turnSpeed);
                controller.Move(fwd * walkSpeed * Time.deltaTime);

                anim.SetFloat(ParamMoveX, 0f, 0.15f, Time.deltaTime);
                anim.SetFloat(ParamMoveZ, 1.0f, 0.15f, Time.deltaTime);
                anim.SetFloat(ParamSpeed, 1.0f, 0.15f, Time.deltaTime);

                timer += Time.deltaTime;
                yield return null;
            }

            anim.SetFloat(ParamMoveX, 0f, 0.1f, Time.deltaTime);
            anim.SetFloat(ParamMoveZ, 0f, 0.1f, Time.deltaTime);
            anim.SetFloat(ParamSpeed, 0f, 0.1f, Time.deltaTime);
            proceduralMaster?.SetThrusterBoost(0.3f);
            yield return new WaitForSeconds(0.3f);

            FaceTarget();
            anim.SetTrigger(ParamRoar);
            proceduralMaster?.TriggerPelvisCompression(0.18f);
            yield return new WaitForSeconds(2.0f);

            anim.SetTrigger(ParamMeleeSwipe);
            yield return new WaitForSeconds(1.6f);

            currentState         = BossState.Chasing;
            nextOrbitSwitchTime  = Time.time + Random.Range(2.5f, 5f);
            nextAttackTime       = Time.time + Random.Range(phase1Cooldown * 0.6f, phase1Cooldown);
        }

        // ══════════════════════════════════════════════════════════════════════
        // TACTICAL LOCOMOTION — 2D Directional Stride & True Mech Decision Tree
        // ══════════════════════════════════════════════════════════════════════
        private void HandleTacticalLocomotion()
        {
            if (isAttacking || target == null) return;

            Vector3 toTarget  = (target.position - transform.position); toTarget.y = 0;
            float   distance  = toTarget.magnitude;
            Vector3 fwd       = toTarget.normalized;

            FaceTarget();

            if (Time.time >= nextOrbitSwitchTime)
            {
                orbitDirection      = (Random.value < 0.5f) ? 1f : -1f;
                nextOrbitSwitchTime = Time.time + Random.Range(2.5f, 5f);
            }

            // Hydraulic Pivot Turns when target flanks behind
            if (toTarget.sqrMagnitude > 0.01f && Time.time >= nextAttackTime)
            {
                float angleToTarget = Vector3.SignedAngle(transform.forward, fwd, Vector3.up);
                if (Mathf.Abs(angleToTarget) > 115f)
                {
                    StartCoroutine(PerformTurn180());
                    return;
                }
                else if (angleToTarget > 65f)
                {
                    StartCoroutine(PerformTurn90R());
                    return;
                }
                else if (angleToTarget < -65f)
                {
                    StartCoroutine(PerformTurn45L());
                    return;
                }
            }

            // Attack decision
            if (Time.time >= nextAttackTime && !isAttacking)
            {
                float roll = Random.value;

                if (distance <= meleeRange)
                {
                    // If target is a small agile combatant like Alita crowding the ankles, prioritize ground stomps & shin sweeps
                    if (isTargetSmallCombatant && distance <= minHardBufferDistance + 0.6f)
                    {
                        if      (roll < 0.35f) StartCoroutine(AttackSeismicHeel());
                        else if (roll < 0.65f) StartCoroutine(AttackHydraulicKick());
                        else                   StartCoroutine(AttackMeleeSwipe());
                        return;
                    }

                    // Full 32-Move Curated Melee Arsenal
                    if (isPhase2)
                    {
                        if      (roll < 0.14f) StartCoroutine(AttackMeleeSwipe());
                        else if (roll < 0.28f) StartCoroutine(AttackSlashOverhead());
                        else if (roll < 0.42f) StartCoroutine(AttackSlashLaunch());
                        else if (roll < 0.56f) StartCoroutine(AttackPneumaticPunch());
                        else if (roll < 0.70f) StartCoroutine(AttackHydraulicKick());
                        else if (roll < 0.84f) StartCoroutine(AttackSeismicHeel());
                        else                   StartCoroutine(AttackSpinAttack());
                    }
                    else
                    {
                        if      (roll < 0.18f) StartCoroutine(AttackMeleeSwipe());
                        else if (roll < 0.34f) StartCoroutine(AttackSlashOverhead());
                        else if (roll < 0.50f) StartCoroutine(AttackSlashCrossCut());
                        else if (roll < 0.66f) StartCoroutine(AttackPneumaticPunch());
                        else if (roll < 0.82f) StartCoroutine(AttackHydraulicKick());
                        else                   StartCoroutine(AttackSeismicStomp());
                    }
                    return;
                }
                else if (distance <= chargeMinRange && distance > minHardBufferDistance + 1.5f)
                {
                    if      (roll < 0.25f) StartCoroutine(AttackPistonThrust());   // Core lunging impale
                    else if (roll < 0.50f) StartCoroutine(AttackSlideAttack());   // Skater gap closer
                    else if (roll < 0.70f) StartCoroutine(AttackSlashLaunch());   // Leaping launcher
                    else if (roll < 0.85f) StartCoroutine(AttackGatlingBurst());  // Forearm cannon sweep
                    else                   StartCoroutine(AttackGroundSlam());    // Titan Leap Split
                    return;
                }
                else if (distance > chargeMinRange)
                {
                    if      (roll < 0.40f) StartCoroutine(AttackMissileBarrage());
                    else if (roll < 0.70f) StartCoroutine(AttackSlideAttack());    // Rocket dash
                    else                   StartCoroutine(AttackGatlingBurst());
                    return;
                }
            }

            // Locomotion Vector calculation
            Vector3 moveVector = Vector3.zero;

            if (distance < crowdSpacingDistance)
            {
                float backSpd = (distance < minHardBufferDistance) ? runSpeed * 1.2f : walkSpeed * 1.4f;
                Vector3 strafe = Vector3.Cross(Vector3.up, fwd) * (orbitDirection * 0.4f);
                moveVector = (-fwd + strafe).normalized * backSpd;
            }
            else if (distance > idealOrbitDistance + 1.0f)
            {
                float stalkSpd = (distance > idealOrbitDistance * 1.8f && isPhase2) ? runSpeed : walkSpeed;
                moveVector = fwd * stalkSpd;
            }
            else
            {
                Vector3 strafeDir = Vector3.Cross(Vector3.up, fwd) * orbitDirection;
                float   distErr   = distance - idealOrbitDistance;
                moveVector = (strafeDir + fwd * (distErr * 0.5f)).normalized * walkSpeed;
            }

            if (moveVector.sqrMagnitude > 0.01f)
            {
                controller.Move(moveVector * Time.deltaTime);

                // Relative 2D direction in Mech's local frame
                Vector3 localMove = transform.InverseTransformDirection(moveVector.normalized);
                float speedFactor = moveVector.magnitude / walkSpeed;

                anim.SetFloat(ParamMoveX, localMove.x * speedFactor, 0.12f, Time.deltaTime);
                anim.SetFloat(ParamMoveZ, localMove.z * speedFactor, 0.12f, Time.deltaTime);
                anim.SetFloat(ParamSpeed, moveVector.magnitude, 0.12f, Time.deltaTime);

                if (proceduralMaster != null)
                    proceduralMaster.SetThrusterBoost(speedFactor > 1.2f ? 1.0f : 0.65f);
            }
            else
            {
                anim.SetFloat(ParamMoveX, 0f, 0.15f, Time.deltaTime);
                anim.SetFloat(ParamMoveZ, 0f, 0.15f, Time.deltaTime);
                anim.SetFloat(ParamSpeed, 0f, 0.15f, Time.deltaTime);

                if (proceduralMaster != null)
                    proceduralMaster.SetThrusterBoost(0.3f);
            }
        }

        private void FaceTarget()
        {
            if (target == null) return;
            Vector3 d = target.position - transform.position; d.y = 0;
            if (d.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), Time.deltaTime * turnSpeed);
        }

        // ══════════════════════════════════════════════════════════════════════
        // WaitForAnimation — now with safe null-state fallback
        // ══════════════════════════════════════════════════════════════════════
        private IEnumerator WaitForAnimation(string stateName, float impactNormTime,
            System.Action onImpact, System.Action<float> onProgress = null)
        {
            yield return null;
            float timeout = 0.7f;
            while (!anim.GetCurrentAnimatorStateInfo(0).IsName(stateName) && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            bool impactFired = false;
            while (anim.GetCurrentAnimatorStateInfo(0).IsName(stateName))
            {
                var info = anim.GetCurrentAnimatorStateInfo(0);
                onProgress?.Invoke(info.normalizedTime);

                if (!impactFired && info.normalizedTime >= impactNormTime)
                {
                    impactFired = true;
                    onImpact?.Invoke();
                }

                if (info.normalizedTime >= 0.98f) break;
                yield return null;
            }

            if (!impactFired) onImpact?.Invoke();
        }

        // ══════════════════════════════════════════════════════════════════════
        // 32-MOVE COMBAT SEQUENCES & COMBO CHAINS
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>1. Horizontal Cleave — wide sweeping arc.</summary>
        private IEnumerator AttackMeleeSwipe()
        {
            isAttacking  = true;
            currentState = BossState.MeleeSwipe;
            anim.SetFloat(ParamSpeed, 0f);
            anim.SetTrigger(ParamMeleeSwipe);

            bladeVFX?.StartSwingTrail();
            Vector3 lungeDir = transform.forward;

            yield return StartCoroutine(WaitForAnimation("MeleeSwipe", 0.44f,
                onImpact: () =>
                {
                    if (isTargetSmallCombatant)
                        bladeVFX?.EmitAntiSmallGroundCleave();
                    else
                        bladeVFX?.EmitMechSlashArc(1); // Cleave Arc

                    if (target != null && Vector3.Distance(transform.position, target.position) <= meleeRange + 2.0f)
                        DealDamageToTarget(50f * (isPhase2 ? 1.35f : 1f), 9f);
                },
                onProgress: normT =>
                {
                    if (normT >= 0.12f && normT <= 0.44f)
                    {
                        float d = target != null ? Vector3.Distance(transform.position, target.position) : 10f;
                        if (d > minHardBufferDistance + 0.6f)
                            controller.Move(lungeDir * (walkSpeed * 1.1f) * Time.deltaTime);
                    }
                    if (normT < 0.18f) FaceTarget();
                }
            ));

            bladeVFX?.StopSwingTrail();

            // Fluid combo chaining: MeleeSwipe into SlashCrossCut
            if (isPhase2 || Random.value < 0.6f)
            {
                if (target != null && Vector3.Distance(transform.position, target.position) <= meleeRange + 2.0f)
                {
                    yield return StartCoroutine(AttackSlashCrossCut());
                    yield break;
                }
            }

            ResetAttackState();
        }

        /// <summary>2. Double Cross Cut — rapid X-pattern twin slashes.</summary>
        private IEnumerator AttackSlashCrossCut()
        {
            isAttacking  = true;
            currentState = BossState.MeleeSwipe;
            anim.SetFloat(ParamSpeed, 0f);
            anim.SetTrigger(ParamSlashCrossCut);

            bladeVFX?.StartSwingTrail();
            if (target != null) FaceTarget();

            yield return StartCoroutine(WaitForAnimation("SlashCrossCut", 0.32f,
                onImpact: () =>
                {
                    bladeVFX?.EmitMechSlashArc(2); // Rising Cross Cut
                    if (target != null && Vector3.Distance(transform.position, target.position) <= meleeRange + 1.5f)
                        DealDamageToTarget(42f * (isPhase2 ? 1.3f : 1f), 8f);
                },
                onProgress: normT =>
                {
                    if (normT < 0.25f) FaceTarget();
                }
            ));

            bladeVFX?.StopSwingTrail();

            // Chain into Decapitation Finisher!
            if (isPhase2 || Random.value < 0.5f)
            {
                if (target != null && Vector3.Distance(transform.position, target.position) <= meleeRange + 2.5f)
                {
                    yield return StartCoroutine(AttackSlashDecapitate());
                    yield break;
                }
            }

            ResetAttackState();
        }

        /// <summary>3. Decapitation Sweep — colossal full-arc executioner strike.</summary>
        private IEnumerator AttackSlashDecapitate()
        {
            isAttacking  = true;
            currentState = BossState.MeleeSwipe;
            anim.SetFloat(ParamSpeed, 0f);
            anim.SetTrigger(ParamSlashDecapitate);

            bladeVFX?.StartSwingTrail();
            if (target != null) FaceTarget();

            yield return StartCoroutine(WaitForAnimation("SlashDecapitate", 0.40f,
                onImpact: () =>
                {
                    bladeVFX?.EmitMechSlashArc(7); // Colossal Finisher Arc!
                    proceduralMaster?.TriggerPelvisCompression(0.25f);
                    if (target != null && Vector3.Distance(transform.position, target.position) <= meleeRange + 2.8f)
                        DealDamageToTarget(78f * (isPhase2 ? 1.4f : 1f), 20f);
                },
                onProgress: normT =>
                {
                    if (normT < 0.30f) FaceTarget();
                }
            ));

            bladeVFX?.StopSwingTrail();
            ResetAttackState();
        }

        /// <summary>4. Overhead Execution Split — massive two-handed vertical hydraulic cleave.</summary>
        private IEnumerator AttackSlashOverhead()
        {
            isAttacking  = true;
            currentState = BossState.GroundSlam;
            anim.SetFloat(ParamSpeed, 0f);
            anim.SetTrigger(ParamSlashOverhead);

            bladeVFX?.StartSwingTrail();
            proceduralMaster?.SetThrusterBoost(1.5f);
            if (target != null) FaceTarget();

            Vector3 stepFwd = transform.forward;
            Vector3 impactPos = transform.position + transform.forward * 2.8f;
            bool telegraphSpawned = false;

            yield return StartCoroutine(WaitForAnimation("SlashOverhead", 0.52f,
                onImpact: () =>
                {
                    if (isTargetSmallCombatant)
                        bladeVFX?.EmitAntiSmallOverheadSplit(impactPos, isPhase2 ? 8.0f : 6.5f);
                    else
                        bladeVFX?.EmitMechSlashArc(5); // Vertical Cleave Arc

                    proceduralMaster?.EmitSeismicGroundSlam(impactPos, isPhase2 ? 8.0f : 6.5f, 85f);
                    proceduralMaster?.TriggerPelvisCompression(0.35f);

                    Collider[] hits = Physics.OverlapSphere(impactPos, isPhase2 ? 8.0f : 6.5f);
                    foreach (var h in hits)
                        if (h.transform == target || h.CompareTag("Player"))
                            DealDamageToTarget(85f * (isPhase2 ? 1.35f : 1f), 22f);

                    DealShockwaveDamage(impactPos, isPhase2 ? 8.0f : 6.5f, 50f * (isPhase2 ? 1.35f : 1f), 15f);
                },
                onProgress: normT =>
                {
                    if (normT < 0.35f) FaceTarget();
                    if (!telegraphSpawned && normT >= 0.16f)
                    {
                        telegraphSpawned = true;
                        bladeVFX?.SpawnGroundTelegraph(impactPos, isPhase2 ? 8.0f : 6.5f, 0.36f);
                    }
                    if (normT >= 0.20f && normT <= 0.50f)
                    {
                        float d = target != null ? Vector3.Distance(transform.position, target.position) : 10f;
                        if (d > minHardBufferDistance + 0.8f)
                            controller.Move(stepFwd * (walkSpeed * 1.2f) * Time.deltaTime);
                    }
                }
            ));

            bladeVFX?.StopSwingTrail();

            // Overhead chains into Upward Launcher
            if (isPhase2 || Random.value < 0.45f)
            {
                if (target != null && Vector3.Distance(transform.position, target.position) <= meleeRange + 2.0f)
                {
                    yield return StartCoroutine(AttackSlashLaunch());
                    yield break;
                }
            }

            ResetAttackState();
        }

        /// <summary>5. Upward Launcher Cleave — high rising uppercut launching player.</summary>
        private IEnumerator AttackSlashLaunch()
        {
            isAttacking  = true;
            currentState = BossState.MeleeSwipe;
            anim.SetFloat(ParamSpeed, 0f);
            anim.SetTrigger(ParamSlashLaunch);

            bladeVFX?.StartSwingTrail();
            if (target != null) FaceTarget();

            yield return StartCoroutine(WaitForAnimation("SlashLaunch", 0.38f,
                onImpact: () =>
                {
                    bladeVFX?.EmitMechSlashArc(3); // Upward Uppercut Arc
                    proceduralMaster?.TriggerPelvisCompression(0.20f);
                    if (target != null && Vector3.Distance(transform.position, target.position) <= meleeRange + 2.2f)
                        DealDamageToTarget(58f * (isPhase2 ? 1.3f : 1f), 18f);
                },
                onProgress: normT =>
                {
                    if (normT < 0.28f) FaceTarget();
                }
            ));

            bladeVFX?.StopSwingTrail();

            // Chains directly into Ground Slam leap!
            if (isPhase2)
            {
                yield return StartCoroutine(AttackGroundSlam());
                yield break;
            }

            ResetAttackState();
        }

        /// <summary>6. Ground Slam — leap into ground blade crater.</summary>
        private IEnumerator AttackGroundSlam()
        {
            isAttacking  = true;
            currentState = BossState.GroundSlam;
            anim.SetFloat(ParamSpeed, 0f);
            anim.SetTrigger(ParamGroundSlam);

            proceduralMaster?.SetThrusterBoost(1.8f);

            Vector3 leapDir = (target != null) ? (target.position - transform.position).normalized : transform.forward;
            leapDir.y = 0;
            Vector3 landingPos = groundSlamPoint != null ? groundSlamPoint.position : transform.position + transform.forward * 2.5f;
            bool telegraphSpawned = false;

            yield return StartCoroutine(WaitForAnimation("GroundSlam", 0.50f,
                onImpact: () =>
                {
                    Vector3 pos = groundSlamPoint != null ? groundSlamPoint.position : transform.position + transform.forward * 2.5f;
                    SpawnFX(groundSlamFXPrefab, pos);
                    proceduralMaster?.EmitSeismicGroundSlam(pos, isPhase2 ? 8.5f : 6.5f, 70f);
                    proceduralMaster?.TriggerPelvisCompression(0.35f);
                    proceduralMaster?.SetThrusterBoost(0.3f);

                    if (isTargetSmallCombatant)
                        bladeVFX?.EmitAntiSmallOverheadSplit(pos, isPhase2 ? 8.5f : 7.0f);
                    else
                        bladeVFX?.EmitMechSlashArc(5);

                    float radius = isPhase2 ? 8.5f : 6.5f;
                    Collider[] hits = Physics.OverlapSphere(pos, radius);
                    foreach (var h in hits)
                        if (h.transform == target || h.CompareTag("Player"))
                            DealDamageToTarget(70f * (isPhase2 ? 1.35f : 1f), 18f);

                    DealShockwaveDamage(pos, radius, 45f * (isPhase2 ? 1.35f : 1f), 14f);
                },
                onProgress: normT =>
                {
                    if (!telegraphSpawned && normT >= 0.12f)
                    {
                        telegraphSpawned = true;
                        bladeVFX?.SpawnGroundTelegraph(landingPos, isPhase2 ? 8.5f : 7.0f, 0.38f);
                    }
                    if (normT >= 0.15f && normT <= 0.50f)
                    {
                        float d = target != null ? Vector3.Distance(transform.position, target.position) : 10f;
                        if (d > minHardBufferDistance + 1.0f)
                            controller.Move(leapDir * (runSpeed * 1.1f) * Time.deltaTime);
                    }
                }
            ));

            proceduralMaster?.SetThrusterBoost(0.3f);
            ResetAttackState();
        }

        /// <summary>7. Core Piston Thrust — forward lunging impale.</summary>
        private IEnumerator AttackPistonThrust()
        {
            isAttacking  = true;
            currentState = BossState.SlideAttack;
            anim.SetFloat(ParamSpeed, 1.5f);
            anim.SetTrigger(ParamPistonThrust);

            bladeVFX?.StartSwingTrail();
            proceduralMaster?.SetThrusterBoost(2.0f);
            if (target != null) FaceTarget();

            Vector3 lungeDir = transform.forward;

            yield return StartCoroutine(WaitForAnimation("PistonThrust", 0.38f,
                onImpact: () =>
                {
                    Vector3 floorPos = (target != null) ? target.position : (transform.position + transform.forward * 3.5f);
                    if (isTargetSmallCombatant)
                        bladeVFX?.EmitAntiSmallImpaleThrust(floorPos);
                    else
                        bladeVFX?.EmitMechSlashArc(4); // Focused piercing tunnel

                    proceduralMaster?.TriggerPelvisCompression(0.22f);
                    if (target != null && Vector3.Distance(transform.position, target.position) <= meleeRange + 2.5f)
                        DealDamageToTarget(65f * (isPhase2 ? 1.3f : 1f), 16f);
                },
                onProgress: normT =>
                {
                    if (normT >= 0.10f && normT <= 0.40f)
                    {
                        float d = target != null ? Vector3.Distance(transform.position, target.position) : 10f;
                        if (d > minHardBufferDistance + 0.6f)
                            controller.Move(lungeDir * (runSpeed * 1.2f) * Time.deltaTime);
                    }
                }
            ));

            bladeVFX?.StopSwingTrail();

            if (isPhase2)
            {
                yield return StartCoroutine(AttackSpinAttack());
                yield break;
            }

            ResetAttackState();
        }

        /// <summary>8. Cyclone Whirlwind — 360-degree perimeter sweep.</summary>
        private IEnumerator AttackSpinAttack()
        {
            isAttacking  = true;
            currentState = BossState.SpinAttack;
            anim.SetFloat(ParamSpeed, 0f);
            anim.SetTrigger(ParamSpinAttack);

            bladeVFX?.StartSwingTrail();

            yield return StartCoroutine(WaitForAnimation("SpinAttack", 0.35f,
                onImpact: () =>
                {
                    proceduralMaster?.TriggerPelvisCompression(0.35f);
                    if (isTargetSmallCombatant)
                        bladeVFX?.EmitAntiSmallLowWhirlwind(meleeRange + 2.5f);
                    else
                        bladeVFX?.EmitMechSlashArc(6); // 360-degree perimeter energy wall!

                    Collider[] hits = Physics.OverlapSphere(transform.position, meleeRange + 2.5f);
                    foreach (var h in hits)
                    {
                        if (h.transform == target || h.CompareTag("Player"))
                            DealDamageToTarget(62f * (isPhase2 ? 1.3f : 1f), 16f);
                    }
                    DealShockwaveDamage(transform.position, meleeRange + 2.5f, 40f * (isPhase2 ? 1.3f : 1f), 12f);
                },
                onProgress: normT =>
                {
                    if (normT >= 0.08f && normT <= 0.75f)
                    {
                        float d = target != null ? Vector3.Distance(transform.position, target.position) : 10f;
                        if (d > minHardBufferDistance + 0.5f)
                            controller.Move(transform.forward * (walkSpeed * 0.8f) * Time.deltaTime);
                    }
                }
            ));

            bladeVFX?.StopSwingTrail();

            if (isPhase2)
            {
                yield return StartCoroutine(AttackSlideAttack());
                yield break;
            }

            ResetAttackState();
        }

        /// <summary>9. Hydraulic Slide Gap-Closer — high speed skater dash.</summary>
        private IEnumerator AttackSlideAttack()
        {
            isAttacking  = true;
            currentState = BossState.SlideAttack;
            anim.SetFloat(ParamSpeed, 2f);
            anim.SetTrigger(ParamSlideAttack);

            proceduralMaster?.SetThrusterBoost(2.4f);
            proceduralMaster?.EmitSkaterDashSparks();

            if (target != null) FaceTarget();

            Vector3 slideDir = target != null ? (target.position - transform.position).normalized : transform.forward;
            slideDir.y = 0;
            float slideSpd = runSpeed * (isPhase2 ? 1.6f : 1.3f);

            yield return StartCoroutine(WaitForAnimation("SlideAttack", 0.45f,
                onImpact: () =>
                {
                    proceduralMaster?.TriggerPelvisCompression(0.20f);
                    bladeVFX?.EmitMechSlashArc(1);
                    Collider[] hits = Physics.OverlapSphere(transform.position + transform.forward * 2f, meleeRange + 1.5f);
                    foreach (var h in hits)
                        if (h.transform == target || h.CompareTag("Player"))
                            DealDamageToTarget(48f * (isPhase2 ? 1.3f : 1f), 12f);
                },
                onProgress: normT =>
                {
                    if (normT < 0.45f)
                    {
                        float d = target != null ? Vector3.Distance(transform.position, target.position) : 10f;
                        if (d > minHardBufferDistance + 1.2f)
                            controller.Move(slideDir * slideSpd * Time.deltaTime);
                    }
                }
            ));

            proceduralMaster?.SetThrusterBoost(0.3f);
            ResetAttackState();
        }

        /// <summary>10. Pneumatic Fist Punch — heavy left arm piston punch.</summary>
        private IEnumerator AttackPneumaticPunch()
        {
            isAttacking  = true;
            currentState = BossState.MeleeSwipe;
            anim.SetFloat(ParamSpeed, 0f);
            anim.SetTrigger(ParamPneumaticPunch);

            if (target != null) FaceTarget();

            yield return StartCoroutine(WaitForAnimation("PneumaticPunch", 0.40f,
                onImpact: () =>
                {
                    proceduralMaster?.TriggerPelvisCompression(0.20f);
                    bladeVFX?.FlashBladeLight();
                    if (target != null && Vector3.Distance(transform.position, target.position) <= meleeRange + 1.2f)
                        DealDamageToTarget(54f * (isPhase2 ? 1.3f : 1f), 14f);
                },
                onProgress: normT =>
                {
                    if (normT < 0.30f) FaceTarget();
                }
            ));

            // Brawler combo: Punch -> Claw Swipe
            if (isPhase2 || Random.value < 0.6f)
            {
                yield return StartCoroutine(AttackClawSwipe());
                yield break;
            }

            ResetAttackState();
        }

        /// <summary>11. Claw Swipe — heavy backhand claw sweep.</summary>
        private IEnumerator AttackClawSwipe()
        {
            isAttacking  = true;
            currentState = BossState.MeleeSwipe;
            anim.SetFloat(ParamSpeed, 0f);
            anim.SetTrigger(ParamClawSwipe);

            if (target != null) FaceTarget();

            yield return StartCoroutine(WaitForAnimation("ClawSwipe", 0.40f,
                onImpact: () =>
                {
                    proceduralMaster?.TriggerPelvisCompression(0.18f);
                    if (target != null && Vector3.Distance(transform.position, target.position) <= meleeRange + 1.6f)
                        DealDamageToTarget(48f * (isPhase2 ? 1.3f : 1f), 12f);
                },
                onProgress: normT =>
                {
                    if (normT < 0.30f) FaceTarget();
                }
            ));

            // Chain Claw Swipe -> Hydraulic Kick
            if (isPhase2 || Random.value < 0.5f)
            {
                yield return StartCoroutine(AttackHydraulicKick());
                yield break;
            }

            ResetAttackState();
        }

        /// <summary>12. Hydraulic Push Kick — heavy front kick.</summary>
        private IEnumerator AttackHydraulicKick()
        {
            isAttacking  = true;
            currentState = BossState.MeleeSwipe;
            anim.SetFloat(ParamSpeed, 0f);
            anim.SetTrigger(ParamHydraulicKick);

            if (target != null) FaceTarget();

            yield return StartCoroutine(WaitForAnimation("HydraulicKick", 0.40f,
                onImpact: () =>
                {
                    proceduralMaster?.TriggerPelvisCompression(0.22f);
                    if (target != null && Vector3.Distance(transform.position, target.position) <= meleeRange + 1.0f)
                        DealDamageToTarget(48f * (isPhase2 ? 1.3f : 1f), 22f); // Big knockback
                },
                onProgress: normT =>
                {
                    if (normT < 0.30f) FaceTarget();
                }
            ));

            // Chain Kick -> Seismic Heel
            if (isPhase2)
            {
                yield return StartCoroutine(AttackSeismicHeel());
                yield break;
            }

            ResetAttackState();
        }

        /// <summary>13. Seismic Heel Crush — two-footed seismic stomp crater.</summary>
        private IEnumerator AttackSeismicHeel()
        {
            isAttacking  = true;
            currentState = BossState.SeismicStomp;
            anim.SetFloat(ParamSpeed, 0f);
            anim.SetTrigger(ParamSeismicHeel);

            if (target != null) FaceTarget();
            Vector3 stompPos = transform.position + transform.forward * 1.5f;
            bool telegraphSpawned = false;

            yield return StartCoroutine(WaitForAnimation("SeismicStomp", 0.48f,
                onImpact: () =>
                {
                    proceduralMaster?.EmitSeismicGroundSlam(stompPos, isPhase2 ? 8.0f : 6.5f, 75f);
                    proceduralMaster?.TriggerPelvisCompression(0.35f);
                    Color waveColor = isPhase2 ? new Color(5.5f, 0.4f, 1.4f) : new Color(4.8f, 1.8f, 0.2f);
                    InsaneGun.Combat.TronSlashArcGenerator.SpawnFloorGridRing(stompPos, waveColor, isPhase2 ? 8.0f : 6.5f);

                    Collider[] hits = Physics.OverlapSphere(stompPos, isPhase2 ? 8.0f : 6.5f);
                    foreach (var h in hits)
                        if (h.transform == target || h.CompareTag("Player"))
                            DealDamageToTarget(70f * (isPhase2 ? 1.35f : 1f), 18f);

                    DealShockwaveDamage(stompPos, isPhase2 ? 8.0f : 6.5f, 45f * (isPhase2 ? 1.35f : 1f), 14f);
                },
                onProgress: normT =>
                {
                    if (normT < 0.28f) FaceTarget();
                    if (!telegraphSpawned && normT >= 0.15f)
                    {
                        telegraphSpawned = true;
                        bladeVFX?.SpawnGroundTelegraph(stompPos, isPhase2 ? 8.0f : 6.5f, 0.33f);
                    }
                }
            ));

            ResetAttackState();
        }

        /// <summary>14. Seismic Stomp — hydraulic downward foot crush with shockwave.</summary>
        private IEnumerator AttackSeismicStomp()
        {
            isAttacking  = true;
            currentState = BossState.SeismicStomp;
            anim.SetFloat(ParamSpeed, 0f);
            anim.SetTrigger(ParamSeismicStomp);

            if (target != null) FaceTarget();
            Vector3 stompPos = transform.position + transform.forward * 1.2f;
            bool telegraphSpawned = false;

            yield return StartCoroutine(WaitForAnimation("SeismicStomp", 0.48f,
                onImpact: () =>
                {
                    proceduralMaster?.EmitSeismicGroundSlam(stompPos, isPhase2 ? 7.5f : 6.0f, 60f);
                    proceduralMaster?.TriggerPelvisCompression(0.32f);
                    Color waveColor = isPhase2 ? new Color(5.5f, 0.4f, 1.4f) : new Color(4.8f, 1.8f, 0.2f);
                    InsaneGun.Combat.TronSlashArcGenerator.SpawnFloorGridRing(stompPos, waveColor, isPhase2 ? 7.5f : 6.0f);

                    float radius = isPhase2 ? 7.5f : 6.0f;
                    Collider[] hits = Physics.OverlapSphere(stompPos, radius);
                    foreach (var h in hits)
                        if (h.transform == target || h.CompareTag("Player"))
                            DealDamageToTarget(60f * (isPhase2 ? 1.3f : 1f), 16f);

                    DealShockwaveDamage(stompPos, radius, 40f * (isPhase2 ? 1.3f : 1f), 12f);
                },
                onProgress: normT =>
                {
                    if (normT < 0.25f) FaceTarget();
                    if (!telegraphSpawned && normT >= 0.15f)
                    {
                        telegraphSpawned = true;
                        bladeVFX?.SpawnGroundTelegraph(stompPos, isPhase2 ? 7.5f : 6.0f, 0.33f);
                    }
                }
            ));

            ResetAttackState();
        }

        /// <summary>15. Forearm Gatling Burst — high-speed autocannon sweep.</summary>
        private IEnumerator AttackGatlingBurst()
        {
            isAttacking  = true;
            currentState = BossState.GatlingBurst;
            anim.SetFloat(ParamSpeed, 0f);
            anim.SetTrigger(ParamGatlingBurst);

            if (target != null) FaceTarget();

            proceduralMaster?.FireGatlingBurst(target, 2.0f);

            float burstDuration = 2.0f;
            float elapsed = 0f;

            while (elapsed < burstDuration)
            {
                FaceTarget();
                elapsed += 0.25f;

                if (target != null)
                {
                    Vector3 toT = target.position - transform.position;
                    if (Vector3.Angle(transform.forward, toT) < 45f && toT.magnitude < missileRange)
                    {
                        DealDamageToTarget(9f * (isPhase2 ? 1.25f : 1f), 2f);
                    }
                }
                yield return new WaitForSeconds(0.25f);
            }

            ResetAttackState();
        }

        /// <summary>16. Shoulder Pod Missile Barrage — swarm micro-missiles.</summary>
        private IEnumerator AttackMissileBarrage()
        {
            isAttacking  = true;
            currentState = BossState.MissileBarrage;
            anim.SetFloat(ParamSpeed, 0f);
            anim.SetTrigger(ParamMissileBarrage);

            if (target != null) FaceTarget();

            yield return StartCoroutine(WaitForAnimation("MissileBarrage", 0.22f, () =>
            {
                proceduralMaster?.FireMissileBarrage(target, isPhase2 ? 8 : 5);

                if (missileBarrageFXPrefab != null)
                {
                    if (shoulderPodLeft  != null) SpawnFX(missileBarrageFXPrefab, shoulderPodLeft.position);
                    if (shoulderPodRight != null) SpawnFX(missileBarrageFXPrefab, shoulderPodRight.position);
                }

                if (target != null)
                {
                    Vector3 toT = target.position - transform.position;
                    if (Vector3.Angle(transform.forward, toT) < 70f && toT.magnitude < missileRange)
                        DealDamageToTarget(48f, 7f);
                }
            }));

            // Second volley in phase 2
            if (isPhase2)
            {
                yield return new WaitForSeconds(0.4f);
                proceduralMaster?.FireMissileBarrage(target, 4);
                if (target != null)
                {
                    Vector3 toT = target.position - transform.position;
                    if (toT.magnitude < missileRange)
                        DealDamageToTarget(35f, 7f);
                }
            }

            ResetAttackState();
        }

        // ══════════════════════════════════════════════════════════════════════
        // DEFENSIVE MOVES: BLADE SHIELD & RIPOSTE PARRY
        // ══════════════════════════════════════════════════════════════════════

        public IEnumerator DefendBladeShield()
        {
            isAttacking  = true;
            currentState = BossState.MeleeSwipe;
            anim.SetFloat(ParamSpeed, 0f);
            anim.SetTrigger(ParamBladeShield);

            bladeVFX?.StartSwingTrail();
            bladeVFX?.FlashBladeLight();

            yield return StartCoroutine(WaitForAnimation("BladeShield", 0.30f,
                onImpact: () =>
                {
                    Vector3 shieldPos = transform.position + transform.forward * 1.5f + Vector3.up * 2.0f;
                    SpawnFX(sparksDeathFXPrefab, shieldPos);
                }
            ));

            bladeVFX?.StopSwingTrail();

            // 50% chance to immediately counter with RiposteParry
            if (Random.value < 0.5f)
            {
                yield return StartCoroutine(DefendRiposteParry());
                yield break;
            }

            ResetAttackState();
        }

        public IEnumerator DefendRiposteParry()
        {
            isAttacking  = true;
            currentState = BossState.MeleeSwipe;
            anim.SetFloat(ParamSpeed, 0f);
            anim.SetTrigger(ParamRiposteParry);

            bladeVFX?.StartSwingTrail();
            bladeVFX?.FlashBladeLight();

            yield return StartCoroutine(WaitForAnimation("RiposteParry", 0.30f,
                onImpact: () =>
                {
                    bladeVFX?.EmitMechSlashArc(1); // Counter slash arc
                    if (target != null && Vector3.Distance(transform.position, target.position) <= meleeRange + 1.5f)
                        DealDamageToTarget(55f * (isPhase2 ? 1.4f : 1f), 16f);
                }
            ));

            bladeVFX?.StopSwingTrail();
            ResetAttackState();
        }

        public IEnumerator DefendHeavyBrace()
        {
            isAttacking  = true;
            currentState = BossState.MeleeSwipe;
            anim.SetFloat(ParamSpeed, 0f);
            anim.SetTrigger(ParamHeavyBrace);

            yield return StartCoroutine(WaitForAnimation("HeavyBrace", 0.40f,
                onImpact: () =>
                {
                    proceduralMaster?.TriggerPelvisCompression(0.20f);
                }
            ));

            ResetAttackState();
        }

        // ══════════════════════════════════════════════════════════════════════
        // HYDRAULIC PIVOT TURNS
        // ══════════════════════════════════════════════════════════════════════

        private IEnumerator PerformTurn180()
        {
            isAttacking = true;
            anim.SetTrigger(ParamTurn180);
            yield return StartCoroutine(WaitForAnimation("Turn180", 0.50f, onImpact: null));
            if (target != null) FaceTarget();
            ResetAttackState();
        }

        private IEnumerator PerformTurn90R()
        {
            isAttacking = true;
            anim.SetTrigger(ParamTurn90R);
            yield return StartCoroutine(WaitForAnimation("Turn90R", 0.50f, onImpact: null));
            if (target != null) FaceTarget();
            ResetAttackState();
        }

        private IEnumerator PerformTurn45L()
        {
            isAttacking = true;
            anim.SetTrigger(ParamTurn45L);
            yield return StartCoroutine(WaitForAnimation("Turn45L", 0.50f, onImpact: null));
            if (target != null) FaceTarget();
            ResetAttackState();
        }

        private IEnumerator AttackReactorPowerUp()
        {
            isAttacking = true;
            anim.SetTrigger(ParamReactorPowerUp);
            proceduralMaster?.SetThrusterBoost(2.5f);
            bladeVFX?.SetPhase2(true);
            healthBar?.SetEnraged(true);

            yield return StartCoroutine(WaitForAnimation("ReactorPowerUp", 0.45f,
                onImpact: () =>
                {
                    SpawnFX(groundSlamFXPrefab, transform.position);
                    AudioClip alarmClip = MechAudioSynth.GetOverdriveAlarm();
                    AudioSource.PlayClipAtPoint(alarmClip, transform.position, 1.4f);
                }
            ));

            ResetAttackState();
        }

        private void ResetAttackState()
        {
            isAttacking  = false;
            anim.SetFloat(ParamSpeed, 0f);
            currentState = BossState.Chasing;
            nextAttackTime = Time.time + (isPhase2 ? phase2Cooldown : phase1Cooldown);
        }

        // ══════════════════════════════════════════════════════════════════════
        // SUPER ARMOR & POISE
        // ══════════════════════════════════════════════════════════════════════
        public void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitDirection, bool isHeavy)
        {
            EnsureInitialized();
            if (currentState == BossState.Dead) return;

            // Tactical Parry & Guard Reactive Check:
            // If not currently attacking, and player is attacking from the front, chance to parry or block!
            if (!isAttacking && !isStaggered && Vector3.Angle(transform.forward, -hitDirection) < 75f)
            {
                float parryRoll = Random.value;
                if (parryRoll < 0.18f)
                {
                    StartCoroutine(DefendRiposteParry());
                    return;
                }
                else if (parryRoll < 0.36f)
                {
                    StartCoroutine(DefendBladeShield());
                    damage *= 0.15f; // Absorbs 85% damage on heavy blade guard
                }
            }

            lastDamageTime = Time.time;

            float actualDamage = isStaggered ? damage * criticalVulnerabilityMultiplier : damage;
            currentHealth -= actualDamage;
            healthBar?.SetHealth(currentHealth);

            // Phase 2 transition
            if (!isPhase2 && currentHealth <= maxHealth * 0.50f)
            {
                StopAllCoroutines();
                isAttacking = false;
                StartCoroutine(Phase2Enrage());
                return;
            }

            // Poise
            if (!isStaggered)
            {
                float poiseDmg = isHeavy ? damage * 1.8f : damage * 1.1f;
                currentPoise += poiseDmg;
                healthBar?.SetPosture(currentPoise, maxPoise);

                if (currentPoise >= maxPoise)
                {
                    StopAllCoroutines();
                    isAttacking = false;
                    StartCoroutine(StaggerSequence());
                    return;
                }
            }

            if (currentHealth <= 0f) Die();
        }

        private IEnumerator StaggerSequence()
        {
            isStaggered  = true;
            isAttacking  = true;
            currentPoise = maxPoise;
            currentState = BossState.Staggered;
            anim.SetFloat(ParamSpeed, 0f);
            anim.SetTrigger(ParamStagger);

            healthBar?.SetPosture(maxPoise, maxPoise);
            healthBar?.SetStaggered(true);

            SpawnFX(sparksDeathFXPrefab, transform.position + Vector3.up * 2.0f);

            yield return new WaitForSeconds(3.5f);  // Longer punish window

            currentPoise = 0f;
            isStaggered  = false;
            healthBar?.SetPosture(0f, maxPoise);
            healthBar?.SetStaggered(false);

            // Vengeful shockwave on recovery
            if (target != null && Vector3.Distance(transform.position, target.position) < 5.0f)
                DealDamageToTarget(20f, 14f);

            isAttacking = false;
            ResetAttackState();
        }

        private IEnumerator Phase2Enrage()
        {
            isPhase2     = true;
            isAttacking  = true;
            currentState = BossState.EnragedTransition;
            anim.SetFloat(ParamSpeed, 0f);
            anim.SetTrigger(ParamRoar);

            healthBar?.SetEnraged(true);
            bladeVFX?.SetPhase2(true);

            // Significant stat boost
            if (anim != null) anim.speed = 1.05f;
            walkSpeed  *= 1.30f;
            runSpeed   *= 1.30f;
            turnSpeed  *= 1.40f;

            if (exhaustFlameFXPrefab != null)
            {
                if (exhaustVentLeft  != null) Instantiate(exhaustFlameFXPrefab, exhaustVentLeft);
                if (exhaustVentRight != null) Instantiate(exhaustFlameFXPrefab, exhaustVentRight);
            }

            if (proceduralMaster != null)
            {
                proceduralMaster.SetThrusterBoost(1.5f);
                AudioClip alarmClip = MechAudioSynth.GetOverdriveAlarm();
                AudioSource.PlayClipAtPoint(alarmClip, transform.position, 1.2f);
            }

            yield return new WaitForSeconds(1.8f);

            isAttacking = false;
            ResetAttackState();
        }

        private void Die()
        {
            currentState = BossState.Dead;
            StopAllCoroutines();
            anim.SetTrigger(ParamDie);
            healthBar?.SetPosture(0f, maxPoise);
            healthBar?.SetStaggered(false);
            SpawnFX(sparksDeathFXPrefab, transform.position + Vector3.up * 1.5f);
            controller.enabled = false;
            Debug.Log($"[BossMech] {bossName} DEFEATED!");
        }

        private void DealDamageToTarget(float damage, float knockback)
        {
            if (target == null) return;
            var dmgable = target.GetComponent<ISyntyDamageable>()
                       ?? target.GetComponentInParent<ISyntyDamageable>()
                       ?? target.GetComponentInChildren<ISyntyDamageable>();
            if (dmgable != null)
            {
                Vector3 dir = (target.position - transform.position).normalized;
                dmgable.TakeDamage(damage, target.position, dir, knockback > 8f);
                bladeVFX?.TriggerHitstop(knockback > 12f ? 0.08f : 0.05f);
                CyberCombat.Audio.CyberCombatAudioSynth.PlayAtPoint(
                    knockback > 8f
                        ? CyberCombat.Audio.CyberCombatAudioSynth.GetHeavyImpact()
                        : CyberCombat.Audio.CyberCombatAudioSynth.GetSwordImpact(),
                    target.position, 1.1f, 0.8f);
            }
        }

        /// <summary>
        /// Deals planar ground shockwave damage radiating from an impact crater.
        /// Only strikes targets who are on or near the floor, allowing agile targets like Alita to jump over tremors.
        /// </summary>
        public void DealShockwaveDamage(Vector3 center, float radius, float damage, float knockback)
        {
            if (target == null) return;
            Vector2 flatCenter = new Vector2(center.x, center.z);
            Vector2 flatTarget = new Vector2(target.position.x, target.position.z);
            float dist = Vector2.Distance(flatCenter, flatTarget);

            if (dist <= radius && Mathf.Abs(target.position.y - center.y) < 1.6f)
            {
                DealDamageToTarget(damage, knockback);
            }
        }

        private void SpawnFX(GameObject prefab, Vector3 pos)
        {
            if (prefab == null) return;
            Destroy(Instantiate(prefab, pos, Quaternion.identity), 5f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;    Gizmos.DrawWireSphere(transform.position, meleeRange);
            Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, idealOrbitDistance);
            Gizmos.color = Color.cyan;   Gizmos.DrawWireSphere(transform.position, chargeMinRange);
        }
    }
}
