using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SyntyCombined;
using InsaneGun.Combat;

namespace PolygonMech
{
    public enum MechCombatState
    {
        Spawning,
        TacticalStalking,
        ClosingDistance,
        ThrusterEvasion,
        MeleeCombo,
        KineticSlide,
        CycloneWhirlwind,
        LeapGroundSlam,
        SeismicQuake,
        DefensiveGuard,
        GuardCounter,
        HydraulicVenting,
        Staggered,
        Stunned,
        Dead
    }

    /// <summary>
    /// Highly intelligent, tactical combat AI for the Titan Heavy Hammer Mech.
    /// Not unfairly difficult, but deeply intelligent:
    /// - Evaluates player distance, angle, flank attempts, and evasion vectors.
    /// - Punishes blind flanking with 360° centrifugal cyclone spins.
    /// - Punishes distance kiting with seismic ground fractures and mid-air leap slams.
    /// - Uses directional thrusters (forward/backward/lateral dashes) for spatial dominance.
    /// - Implements tactical hammer blocking and reactive counter-cleaves.
    /// - Features clear, telegraphed windups and predictable hydraulic cooling windows (punish windows).
    /// - Fully compatible with Mecanim, HammerTwoHandedGripIK, and ISyntyDamageable.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Animator))]
    public class HammerMechAI : MonoBehaviour, ISyntyDamageable
    {
        [Header("Mech Boss Identity")]
        public string bossName = "COLOSSUS-06 // TITAN WAR MECH";
        public string bossSubTitle = "HEAVY KINETIC SIEGE UNIT";
        public float maxHealth = 4200f;
        public float currentHealth { get; private set; }

        [Header("Target & Sensors")]
        [SerializeField] public Transform target;
        public float detectionRadius = 35.0f;
        public float meleeRange = 4.8f;
        public float midRange = 10.5f;
        public float longRange = 22.0f;

        [Header("Movement & Locomotion")]
        public float walkSpeed = 3.8f;
        public float runSpeed = 6.8f;
        public float turnSpeed = 4.5f;
        public float gravity = 20.0f;

        [Header("Super Armor & Poise System")]
        public float maxPoise = 320f;
        public float currentPoise { get; private set; }
        public float poiseRecoveryRate = 25f;
        public float poiseRecoveryDelay = 3.5f;
        private float lastDamageTime = 0f;

        [Header("VFX & Audio Feedback")]
        public GameObject groundSlamVFX;
        public GameObject metalImpactVFX;
        public Transform hammerHeadSocket;
        [Header("Hammer Rocket Booster System")]
        public HammerRocketBooster hammerBooster;
        [Header("Titan Mech Ragdoll Physics System")]
        public MechTitanRagdollController ragdollController;
        public AudioClip sfxHammerSlam;
        public AudioClip sfxThrusterBurst;
        public AudioClip sfxBlockClang;
        public AudioClip sfxStaggerAlarm;

        [Header("Authentic Heavy Titan Mech Audio")]
        public AudioClip[] sfxTitanFootsteps;
        public AudioClip sfxChassisReactorLoop;
        public AudioClip[] sfxHydraulicActuators;

        [Header("Rock Eruption & Earth Stomp FX")]
        public GameObject rockExplosionPrefab;
        public GameObject groundCrackPrefab;
        public GameObject stoneImpactPrefab;
        public GameObject dustBigPrefab;
        public Transform leftFootSocket;
        public Transform rightFootSocket;

        // Components
        private CharacterController controller;
        private Animator animator;
        private HammerTwoHandedGripIK gripIK;
        private AudioSource audioSource;
        private AudioSource servoLoopAudioSource;
        private Vector3 lastMechPosition;
        private float lastMechRotationY;
        private float currentServoVolume = 0f;
        private float nextMoveServoWhineTime = 0f;
        private BossHealthBar healthBar;

        // State Machine
        public MechCombatState currentState { get; private set; } = MechCombatState.Spawning;
        private float stateTimer = 0f;
        private float verticalVelocity = 0f;
        private Vector3 externalVelocity = Vector3.zero;

        // Tactical Brain Variables
        private float nextDecisionTime = 0f;
        private float distanceToTarget = 0f;
        private float angleToTarget = 0f;
        private Vector3 targetDir = Vector3.forward;
        private Vector3 lastTargetPos;
        private Vector3 targetVelocity;
        private int comboStep = 0;
        private int attacksSinceCooling = 0;
        private bool isAttacking = false;
        private bool isActionLocked = false;
        private bool isAirborneLeap = false;
        private bool isPhase2 = false;
        private float currentActionDuration = 1.0f;

        // Tactical Ability Cooldowns
        [Header("Tactical Ability Cooldowns")]
        public float leapSlamCooldown = 9.0f;
        public float leapSlamLethalRadius = 5.0f;
        private float lastLeapSlamTime = -10.0f;
        public float seismicCooldown = 6.0f;
        private float lastSeismicTime = -10.0f;
        public float dashCooldown = 3.5f;
        private float lastDashTime = -10.0f;

        // Combat Parameters
        private const float PacingDecisionInterval = 0.35f;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            animator = GetComponent<Animator>();
            gripIK = GetComponent<HammerTwoHandedGripIK>();
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            healthBar = GetComponent<BossHealthBar>() ?? FindAnyObjectByType<BossHealthBar>();

#if UNITY_EDITOR
            if (sfxTitanFootsteps == null || sfxTitanFootsteps.Length == 0)
            {
                sfxTitanFootsteps = new AudioClip[] {
                    UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Mechs/Mech_Footstep_Titan_Heavy_01.wav"),
                    UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Mechs/Mech_Footstep_Titan_Heavy_02.wav"),
                    UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Mechs/Mech_Footstep_Titan_Heavy_03.wav")
                };
            }
            if (sfxChassisReactorLoop == null)
                sfxChassisReactorLoop = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Mechs/Mech_Chassis_Reactor_Loop.wav");
            if (sfxHydraulicActuators == null || sfxHydraulicActuators.Length == 0)
            {
                sfxHydraulicActuators = new AudioClip[] {
                    UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Mechs/Mech_Hydraulic_Actuator_01.wav"),
                    UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Mechs/Mech_Hydraulic_Actuator_02.wav"),
                    UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Mechs/Mech_Hydraulic_Actuator_03.wav")
                };
            }
#endif

            // Setup Dedicated 3D Looping Mechanical Movement AudioSource (Deep reactor/chassis turbine)
            GameObject servoObj = new GameObject("Audio_MechMovementServos");
            servoObj.transform.SetParent(transform, false);
            servoLoopAudioSource = servoObj.AddComponent<AudioSource>();
            servoLoopAudioSource.spatialBlend = 1.0f; // full 3D sound
            servoLoopAudioSource.rolloffMode = AudioRolloffMode.Linear;
            servoLoopAudioSource.minDistance = 4f;
            servoLoopAudioSource.maxDistance = 55f;
            servoLoopAudioSource.loop = true;
            servoLoopAudioSource.clip = sfxChassisReactorLoop != null ? sfxChassisReactorLoop : MechAudioSynth.GetMovementServoLoop();
            servoLoopAudioSource.volume = 0f;
            servoLoopAudioSource.pitch = 1.0f;
            servoLoopAudioSource.playOnAwake = false;
            servoLoopAudioSource.Play();

            lastMechPosition = transform.position;
            lastMechRotationY = transform.eulerAngles.y;

            currentHealth = maxHealth;
            currentPoise = maxPoise;

            EnsureComponents();
        }

        private void Start()
        {
            if (target == null) FindPlayerTarget();
            if (target != null) lastTargetPos = target.position;

            if (healthBar != null)
            {
                healthBar.bossName = bossName;
                healthBar.bossSub = bossSubTitle;
                healthBar.Initialize(maxHealth);
                healthBar.SetPosture(maxPoise - currentPoise, maxPoise);
            }

            animator.enabled = true;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.applyRootMotion = false;

            StartCoroutine(SpawnSequence());
        }

        private void EnsureComponents()
        {
            if (hammerHeadSocket == null)
            {
                var tip = transform.Find("Root/Pelvis/Spine_01/Spine_02/Spine_03/Clavicle_R/UpperArm_R/LowerArm_R/Hand_R/SM_Wep_Hammer_01/HammerTip");
                if (tip != null) hammerHeadSocket = tip;
                else
                {
                    var allT = GetComponentsInChildren<Transform>(true);
                    foreach (var t in allT)
                    {
                        if (t.name == "HammerTip" || t.name == "SM_Wep_Hammer_01") { hammerHeadSocket = t; break; }
                    }
                }
            }

            if (hammerBooster == null)
            {
                hammerBooster = GetComponentInChildren<HammerRocketBooster>(true);
            }

            if (ragdollController == null)
            {
                ragdollController = GetComponent<MechTitanRagdollController>();
                if (ragdollController == null)
                {
                    ragdollController = gameObject.AddComponent<MechTitanRagdollController>();
                }
            }

            if (leftFootSocket == null || rightFootSocket == null)
            {
                var anim = animator ?? GetComponent<Animator>();
                if (anim != null)
                {
                    var lf = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
                    var rf = anim.GetBoneTransform(HumanBodyBones.RightFoot);
                    if (lf != null) leftFootSocket = lf;
                    if (rf != null) rightFootSocket = rf;
                }
            }

            if (groundSlamVFX == null)
            {
                groundSlamVFX = Resources.Load<GameObject>("FX_Jump_Impact_01");
            }

#if UNITY_EDITOR
            if (rockExplosionPrefab == null)
                rockExplosionPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonParticles/Prefabs/FX_ShardRock_Explosion_01.prefab");
            if (groundCrackPrefab == null)
                groundCrackPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonParticles/Prefabs/FX_GroundCrack_Blast_01.prefab");
            if (stoneImpactPrefab == null)
                stoneImpactPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonParticles/Prefabs/FX_Impact_Stone_01.prefab");
            if (dustBigPrefab == null)
                dustBigPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonParticles/Prefabs/FX_Dust_Big_01.prefab");
#endif

            // High-fidelity procedural audio clips
            if (sfxHammerSlam == null) sfxHammerSlam = MechAudioSynth.GetSeismicSlam();
            if (sfxThrusterBurst == null) sfxThrusterBurst = MechAudioSynth.GetThrusterIgnition();
            if (sfxStaggerAlarm == null) sfxStaggerAlarm = MechAudioSynth.GetOverdriveAlarm();
            if (sfxBlockClang == null) sfxBlockClang = CyberCombat.Audio.CyberCombatAudioSynth.GetHeavyImpact();
        }

        private void FindPlayerTarget()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
            else
            {
                var dmg = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
                foreach (var mb in dmg)
                {
                    if (mb is ISyntyDamageable && mb.gameObject != this.gameObject && mb.gameObject.name.ToLower().Contains("player"))
                    {
                        target = mb.transform;
                        break;
                    }
                }
            }
        }

        private void Update()
        {
            if (currentState == MechCombatState.Dead)
            {
                if (servoLoopAudioSource != null && servoLoopAudioSource.isPlaying)
                {
                    servoLoopAudioSource.volume = Mathf.MoveTowards(servoLoopAudioSource.volume, 0f, Time.deltaTime * 4f);
                }
                return;
            }

            UpdateSensors();
            UpdatePoise();
            ApplyGravityAndMovement();
            UpdateMechanicalMovementAudio();

            if (currentState == MechCombatState.Spawning) return;

            stateTimer += Time.deltaTime;

            if (!isActionLocked)
            {
                if (Time.time >= nextDecisionTime)
                {
                    nextDecisionTime = Time.time + PacingDecisionInterval;
                    EvaluateTacticalBehavior();
                }
            }
        }

        private void UpdateMechanicalMovementAudio()
        {
            if (servoLoopAudioSource == null) return;

            float dt = Mathf.Max(0.0001f, Time.deltaTime);
            Vector3 posDelta = transform.position - lastMechPosition;
            posDelta.y = 0f;
            float linearSpeed = posDelta.magnitude / dt;

            float currentRotY = transform.eulerAngles.y;
            float angularSpeed = Mathf.Abs(Mathf.DeltaAngle(lastMechRotationY, currentRotY)) / dt;

            lastMechPosition = transform.position;
            lastMechRotationY = currentRotY;

            bool isMoving = (linearSpeed > 0.15f || angularSpeed > 10f) && currentState != MechCombatState.Dead;
            float targetVol = 0f;
            float targetPitch = 1.0f;

            if (isMoving)
            {
                float speedNorm = Mathf.Clamp01(linearSpeed / Mathf.Max(1f, runSpeed));
                float turnNorm = Mathf.Clamp01(angularSpeed / 80f);
                float activity = Mathf.Max(speedNorm, turnNorm);

                // Authentic Heavy Industrial Diesel-Electric / Reactor Chug
                targetVol = Mathf.Lerp(0.12f, 0.38f, activity);
                targetPitch = Mathf.Lerp(0.92f, 1.10f, speedNorm);

                // Trigger hydraulic actuator stroke when pivoting / turning heavily
                if (angularSpeed > 30f && Time.time >= nextMoveServoWhineTime)
                {
                    nextMoveServoWhineTime = Time.time + UnityEngine.Random.Range(1.1f, 1.7f);
                    PlayHydraulicActuator(0.40f);
                }
            }

            currentServoVolume = Mathf.MoveTowards(currentServoVolume, targetVol, dt * 2.5f);
            servoLoopAudioSource.volume = currentServoVolume;
            servoLoopAudioSource.pitch = Mathf.Lerp(servoLoopAudioSource.pitch, targetPitch, dt * 3.0f);
        }

        public void PlayMechFootstep(float volume = 1.0f)
        {
            if (audioSource == null) return;

            // 1. Primary Hollywood Titan Stomp (Sub-bass tremor + Steel Plate Slam + Concrete Crunch + Valve Purge)
            AudioClip stepClip = null;
            if (sfxTitanFootsteps != null && sfxTitanFootsteps.Length > 0)
            {
                int idx = UnityEngine.Random.Range(0, sfxTitanFootsteps.Length);
                stepClip = sfxTitanFootsteps[idx];
            }
            if (stepClip == null) stepClip = MechAudioSynth.GetFootstepStomp();

            audioSource.pitch = UnityEngine.Random.Range(0.95f, 1.05f);
            audioSource.PlayOneShot(stepClip, volume);

            // 2. Secondary hydraulic actuator piston displacement
            PlayHydraulicActuator(volume * 0.40f);
        }

        public void PlayHydraulicActuator(float volume = 0.45f)
        {
            if (audioSource == null || sfxHydraulicActuators == null || sfxHydraulicActuators.Length == 0) return;
            AudioClip actClip = sfxHydraulicActuators[UnityEngine.Random.Range(0, sfxHydraulicActuators.Length)];
            if (actClip != null)
            {
                audioSource.pitch = UnityEngine.Random.Range(0.94f, 1.06f);
                audioSource.PlayOneShot(actClip, volume);
            }
        }

        #region Spatial Intelligence & Sensors

        private void UpdateSensors()
        {
            if (target == null)
            {
                FindPlayerTarget();
                if (target == null) return;
            }

            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;
            distanceToTarget = toTarget.magnitude;
            targetDir = distanceToTarget > 0.001f ? toTarget.normalized : transform.forward;

            // Compute signed horizontal angle (-180 to +180)
            angleToTarget = Vector3.SignedAngle(transform.forward, targetDir, Vector3.up);

            // Calculate player velocity / movement direction
            if (Time.deltaTime > 0.0001f)
            {
                targetVelocity = (target.position - lastTargetPos) / Time.deltaTime;
                lastTargetPos = target.position;
            }
        }

        private void UpdatePoise()
        {
            if (currentState == MechCombatState.Staggered || currentState == MechCombatState.Stunned) return;

            if (Time.time - lastDamageTime > poiseRecoveryDelay && currentPoise < maxPoise)
            {
                currentPoise = Mathf.MoveTowards(currentPoise, maxPoise, Time.deltaTime * poiseRecoveryRate);
                if (healthBar != null) healthBar.SetPosture(maxPoise - currentPoise, maxPoise);
            }
        }

        private void ApplyGravityAndMovement()
        {
            if (controller == null || !controller.enabled) return;
            if (isAirborneLeap) return; // Dedicated ballistic kinematic flight takes complete control during leap!

            if (controller.isGrounded)
            {
                verticalVelocity = -2.0f;
            }
            else
            {
                verticalVelocity -= gravity * Time.deltaTime;
            }

            Vector3 moveVec = externalVelocity * Time.deltaTime;
            moveVec.y += verticalVelocity * Time.deltaTime;

            controller.Move(moveVec);

            // Damp external dash velocity
            externalVelocity = Vector3.MoveTowards(externalVelocity, Vector3.zero, Time.deltaTime * 24.0f);
        }

        #endregion

        #region Tactical Brain & Move Selection

        /// <summary>
        /// Core intelligent decision tree. Evaluates range, flank threat, player rolling/backpedaling,
        /// and cooldowns to select the most appropriate realistic mech behavior.
        /// </summary>
        private void EvaluateTacticalBehavior()
        {
            if (target == null) return;

            // 1. FLANK & BACK-ATTACK DETECTION
            // If player is maneuvering behind or flanking wide (>70 degrees off-center)
            if (Mathf.Abs(angleToTarget) > 70f && distanceToTarget <= meleeRange * 1.5f)
            {
                // Flanked! Thruster burst to the flank to immediately re-center and face the player
                if (angleToTarget > 0) ExecuteThrusterDash("DashRight", transform.right * 9f);
                else ExecuteThrusterDash("DashLeft", -transform.right * 9f);
                return;
            }

            // 2. COOLING / OVERHEAT VENTILATION WINDOW ("Not Difficult, But Intelligent")
            // After 3 consecutive attack actions, the mech vents hydraulic steam and pauses to prevent unfair spam
            if (attacksSinceCooling >= 3)
            {
                attacksSinceCooling = 0;
                StartCoroutine(HydraulicVentingWindow(1.4f));
                return;
            }

            // 3. DEFENSIVE BRACING / COUNTER-GUARD READ
            // If player is close and aggressively moving in while mech is ready, 30% chance to brace hammer shaft
            if (distanceToTarget <= meleeRange && targetVelocity.magnitude > 2.5f && Vector3.Dot(targetVelocity.normalized, -targetDir) > 0.5f)
            {
                if (UnityEngine.Random.value < 0.35f)
                {
                    StartCoroutine(DefensiveGuardRoutine(1.1f));
                    return;
                }
            }

            // 4. RANGE-TIERED MOVE SELECTION WITH DIVERSE TACTICAL ABILITIES
            bool canLeapSlam = Time.time >= lastLeapSlamTime + leapSlamCooldown;
            bool canSeismic = Time.time >= lastSeismicTime + seismicCooldown;
            bool canDash = Time.time >= lastDashTime + dashCooldown;

            if (distanceToTarget <= meleeRange)
            {
                // In close melee range: NEVER leap slam.
                // 25% chance if off cooldown and not right in player face (> 2.8m) to unleash Earth Stomp!
                if (canSeismic && distanceToTarget > 2.8f && UnityEngine.Random.value < 0.25f)
                {
                    ExecuteSeismicShockwave();
                }
                else
                {
                    ExecuteCloseMeleeCombo();
                }
            }
            else if (distanceToTarget <= midRange)
            {
                // Mid Range (4.8m - 10.5m):
                // Check if player is running away (kiting)
                bool isPlayerFleeing = Vector3.Dot(targetVelocity.normalized, targetDir) > 0.4f;

                if (isPlayerFleeing && canLeapSlam)
                {
                    // Player is actively fleeing: 50% chance to punish escape with ballistic rocket leap slam
                    if (UnityEngine.Random.value < 0.50f)
                    {
                        ExecuteLeapGroundSlam();
                        return;
                    }
                }

                // Diverse Tactical Mix at mid-range:
                float roll = UnityEngine.Random.value;
                if (canSeismic && roll < 0.35f)
                {
                    // Seismic Earth Stomp: Pulverize ground with foot stomp and erupting rock fissures
                    ExecuteSeismicShockwave();
                }
                else if (canDash && roll < 0.60f)
                {
                    // Tactical Thruster Dash: Reposition / flank Alita with lateral rocket burst
                    if (UnityEngine.Random.value < 0.5f) ExecuteThrusterDash("DashLeft", -transform.right * 8f);
                    else ExecuteThrusterDash("DashRight", transform.right * 8f);
                }
                else if (roll < 0.85f)
                {
                    // Close distance aggressively using heavy sprint to enter melee combo
                    StartCoroutine(TacticalSprintApproach(1.3f));
                }
                else
                {
                    // Intimidating stalk approach with heavy stomping footsteps
                    StartCoroutine(TacticalStalkApproach(1.2f));
                }
            }
            else if (distanceToTarget <= longRange)
            {
                // Long Range (10.5m - 22m):
                // Priority 1: Seismic Shockwave fissure if available
                if (canSeismic && UnityEngine.Random.value < 0.55f)
                {
                    ExecuteSeismicShockwave();
                }
                // Priority 2: Ballistic Leap Ground Slam if off cooldown
                else if (canLeapSlam && UnityEngine.Random.value < 0.45f)
                {
                    ExecuteLeapGroundSlam();
                }
                else
                {
                    // Aggressive tactical sprint forward to close distance
                    StartCoroutine(TacticalSprintApproach(1.6f));
                }
            }
            else
            {
                // Out of combat range (> 22m):
                if (canLeapSlam && UnityEngine.Random.value < 0.40f)
                {
                    ExecuteLeapGroundSlam();
                }
                else
                {
                    StartCoroutine(TacticalSprintApproach(2.0f));
                }
            }
        }

        #endregion

        #region Attack Implementations

        private void ExecuteCloseMeleeCombo()
        {
            attacksSinceCooling++;

            // If player is extremely close (< 2.4m), 35% chance to perform a fast independent sweep
            if (distanceToTarget < 2.5f && UnityEngine.Random.value < 0.35f)
            {
                StartCoroutine(PerformAttackAction("Attack2_solo", 1.20f, 65f, 0.42f, 2.6f, false));
                return;
            }

            comboStep = (comboStep % 3) + 1;

            switch (comboStep)
            {
                case 1:
                    StartCoroutine(PerformAttackAction("Attack1", 1.35f, 60f, 0.50f, 2.2f, true));
                    break;
                case 2:
                    StartCoroutine(PerformAttackAction("Attack2", 1.25f, 70f, 0.45f, 2.6f, false));
                    break;
                case 3:
                    // Heavy finisher slam with earth pulverize
                    StartCoroutine(PerformAttackAction("Attack3_solo", 1.55f, 100f, 0.58f, 3.2f, true));
                    break;
            }
        }

        private void ExecuteKineticSlideStrike()
        {
            // Special / slide attacks disabled per user request - fallback to standard melee combo
            ExecuteCloseMeleeCombo();
        }

        private void ExecuteSpecialMoveAttack()
        {
            // Special attack completely disabled per user request - fallback to standard melee combo
            ExecuteCloseMeleeCombo();
        }

        private void ExecuteCycloneWhirlwind()
        {
            // Special whirlwind disabled - fallback to thruster flank repositioning
            if (angleToTarget > 0) ExecuteThrusterDash("DashRight", transform.right * 9f);
            else ExecuteThrusterDash("DashLeft", -transform.right * 9f);
        }

        private void ExecuteLeapGroundSlam()
        {
            attacksSinceCooling++;
            lastLeapSlamTime = Time.time;
            StartCoroutine(LeapSlamRoutine());
        }

        private void ExecuteSeismicShockwave()
        {
            attacksSinceCooling++;
            lastSeismicTime = Time.time;
            StartCoroutine(SeismicShockwaveRoutine());
        }

        private void ExecuteThrusterDash(string dashAnim, Vector3 dashForce)
        {
            lastDashTime = Time.time;
            StartCoroutine(ThrusterDashRoutine(dashAnim, dashForce));
        }

        #endregion

        #region Attack Routines & Action Locks

        private IEnumerator SpawnSequence()
        {
            currentState = MechCombatState.Spawning;
            isActionLocked = true;
            PlayAnim("Idle");
            yield return new WaitForSeconds(1.0f);
            isActionLocked = false;
            currentState = MechCombatState.TacticalStalking;
        }

        private IEnumerator PerformAttackAction(string animName, float duration, float damage, float hitNormTime, float hitRadius, bool isSlam)
        {
            isActionLocked = true;
            isAttacking = true;
            currentState = MechCombatState.MeleeCombo;

            // Turn towards target smoothly at start of attack
            SmoothFaceTarget(targetDir, 0.25f);
            PlayAnim(animName);

            // ROCKET BOOSTER IGNITION: Fire backward thruster plume to propel hammer swing!
            if (hammerBooster != null)
            {
                hammerBooster.TriggerBurst(duration * 0.70f, isSlam);
            }

            float timer = 0f;
            bool hitExecuted = false;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                float norm = timer / duration;

                // Tracking window: slight tracking during early windup only
                if (norm < hitNormTime * 0.6f && target != null)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(targetDir), Time.deltaTime * turnSpeed);
                }

                // Hit frame delivery
                if (!hitExecuted && norm >= hitNormTime)
                {
                    hitExecuted = true;
                    DealHammerDamage(damage, hitRadius, isSlam);
                    if (isSlam)
                    {
                        TriggerGroundSlamImpact();
                        if (hammerBooster != null) hammerBooster.TriggerImpactBackfire();
                    }
                }

                yield return null;
            }

            isAttacking = false;
            isActionLocked = false;
            currentState = MechCombatState.TacticalStalking;
            PlayAnim("Idle");
        }

        private IEnumerator KineticSlideRoutine()
        {
            // Special / slide attacks completely disabled per user request - fallback to standard attack
            yield return PerformAttackAction("Attack1", 1.4f, 55f, 0.52f, 1.8f, true);
        }

        private IEnumerator SpecialMoveAttackRoutine()
        {
            // Special attacks completely disabled per user request - fallback to standard attack
            yield return PerformAttackAction("Attack1", 1.4f, 55f, 0.52f, 1.8f, true);
        }

        private IEnumerator LeapSlamRoutine()
        {
            isActionLocked = true;
            isAttacking = true;
            isAirborneLeap = true;
            currentState = MechCombatState.LeapGroundSlam;

            // Phase 1: Heavy Windup & Crouch with Thruster Charging (0.35s)
            SmoothFaceTarget(targetDir, 0.2f);
            
            // Force frame 0 of Jump-Attack1 with zero crossfade delay so animation starts instantly
            if (animator != null) animator.Play("Jump-Attack1", 0, 0f);
            PlaySFX(MechAudioSynth.GetThrusterIgnition(), 0.95f);

            // Spawn pre-launch charging dust burst at feet
            SpawnLaunchBurst(transform.position);

            // Ground crouch windup delay (animation crouches and pre-charges thrusters until t=0.35s)
            yield return new WaitForSeconds(0.35f);

            // Phase 2: High-Altitude Ballistic Flight Parabola (1.00s)
            // Exactly 1.00s flight duration + 0.35s windup = 1.35s total.
            // 1.35s is the exact frame where the hammer impacts the ground in the greatsword jump animation!
            Vector3 startPos = transform.position;
            
            // LAND DIRECTLY ON ALITA!
            Vector3 targetLandingPos = target != null ? target.position : (startPos + transform.forward * 14f);
            targetLandingPos.y = 0.05f;

            // Direction from boss takeoff to Alita
            Vector3 toTarget = targetLandingPos - startPos;
            toTarget.y = 0f;
            Vector3 dirToTarget = toTarget.sqrMagnitude > 0.01f ? toTarget.normalized : transform.forward;

            float flightDuration = 1.00f;
            float peakHeight = 8.5f; // Majestic, soaring ballistic arc into the Martian sky
            float elapsed = 0f;

            // Spawn Holographic Danger Telegraph Decal with targeting crosshairs DIRECTLY under Alita!
            BossTelegraphDecal.Create(targetLandingPos, flightDuration, leapSlamLethalRadius);

            // Takeoff blast FX & audio
            PlaySFX(sfxThrusterBurst != null ? sfxThrusterBurst : MechAudioSynth.GetThrusterIgnition(), 1.0f);
            SpawnLaunchBurst(startPos);
            if (hammerBooster != null) hammerBooster.SetState(BoosterFlameState.Overdrive);

            // Disable CharacterController during ballistic flight so physics capsules don't deflect the boss off Alita
            if (controller != null) controller.enabled = false;

            while (elapsed < flightDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / flightDuration);

                // Smooth horizontal progression
                float hT = Mathf.SmoothStep(0f, 1f, t);
                Vector3 currentHorizontal = Vector3.Lerp(startPos, targetLandingPos, hT);

                // Pure parabolic vertical arc (reaches peakHeight at t = 0.5)
                float currentY = Mathf.Lerp(startPos.y, targetLandingPos.y, t) + 4f * peakHeight * t * (1f - t);

                Vector3 targetPos = new Vector3(currentHorizontal.x, currentY, currentHorizontal.z);
                transform.position = targetPos;

                // Face target smoothly during the leap
                if (dirToTarget.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dirToTarget), Time.deltaTime * 450f);
                }

                yield return null;
            }

            // Snap cleanly to exact landing coordinates directly on Alita's position
            Vector3 finalGroundedPos = new Vector3(targetLandingPos.x, 0.05f, targetLandingPos.z);
            transform.position = finalGroundedPos;
            if (controller != null) controller.enabled = true;

            // Phase 3: Cataclysmic Ground Impact at t = 1.35s!
            TriggerGroundSlamImpact();
            if (hammerBooster != null)
            {
                hammerBooster.TriggerImpactBackfire();
                hammerBooster.SetState(BoosterFlameState.Standby);
            }

            // STRICT HITBOX CHECK: Damage & knockback ONLY apply if entity is inside the visual telegraph circle!
            // If the player rolled or stepped outside the holographic danger ring, they take ZERO damage!
            DealLeapSlamLethalDamage(finalGroundedPos, leapSlamLethalRadius);

            // Violent Camera Trauma Shake & Hit-Stop
            SmoothOrbitCamera.ShakeImpact(2.5f, Vector3.down * 1.1f);
            SmoothOrbitCamera.TriggerHitStop(0.18f, 0.02f);

            // Cataclysmic radiating rock surges: smaller near center, colossal farther away!
            Vector3[] surgeDirs = new Vector3[]
            {
                dirToTarget,
                Quaternion.Euler(0, 90f, 0) * dirToTarget,
                Quaternion.Euler(0, -90f, 0) * dirToTarget,
                -dirToTarget
            };
            foreach (var sDir in surgeDirs)
            {
                StartCoroutine(PropagateRadialRockSurge(finalGroundedPos, sDir, 4, 2.3f));
            }

            // Phase 2: Expanding secondary plasma shockwave ring
            if (isPhase2)
            {
                StartCoroutine(PropagateExpandingPlasmaRing(finalGroundedPos, 18.0f, 1.2f));
            }

            // Phase 4: Ground Impact Recovery Window & Hydraulic Steam Venting (0.85s)
            SpawnSteamVents(finalGroundedPos);
            PlaySFX(MechAudioSynth.GetPneumaticRelease(), 0.95f);
            yield return new WaitForSeconds(0.85f);

            isAirborneLeap = false;
            isAttacking = false;
            isActionLocked = false;
            currentState = MechCombatState.TacticalStalking;
            PlayAnim("Idle");
        }

        private IEnumerator SeismicShockwaveRoutine()
        {
            isActionLocked = true;
            isAttacking = true;
            currentState = MechCombatState.SeismicQuake;

            SmoothFaceTarget(targetDir, 0.3f);
            PlayAnim("RangeAttack1");

            float timer = 0f;
            float duration = 1.95f;
            bool footStomped = false;
            bool hammerSlammed = false;

            while (timer < duration)
            {
                timer += Time.deltaTime;

                // Event 1: Heavy Left Foot Stomp at t = 0.70s (RangeAttack1 animation foot plant!)
                if (!footStomped && timer >= 0.70f)
                {
                    footStomped = true;
                    Vector3 footPos = leftFootSocket != null 
                        ? leftFootSocket.position 
                        : (transform.position + transform.forward * 0.4f - transform.right * 0.5f);
                    
                    // Initial foot stomp: localized crack and medium rocks (1.1f)
                    SpawnRockStompEffect(footPos, 1.15f, true);
                    PlaySFX(MechAudioSynth.GetFootstepStomp(), 1.0f);
                    SmoothOrbitCamera.ShakeImpact(1.6f, Vector3.down * 0.8f);
                    SmoothOrbitCamera.TriggerHitStop(0.08f, 0.02f);
                }

                // Event 2: Cataclysmic Hammer Impact at t = 1.15s (shockwave erupts forward!)
                if (!hammerSlammed && timer >= 1.12f)
                {
                    hammerSlammed = true;
                    if (hammerBooster != null)
                    {
                        hammerBooster.TriggerBurst(0.70f, true);
                        hammerBooster.TriggerImpactBackfire();
                    }
                    TriggerGroundSlamImpact();

                    // Center ground fracture with rock eruption trail
                    StartCoroutine(PropagateSeismicFracture(transform.position + transform.forward * 1.5f, transform.forward));

                    // In Phase 2: Triple-line fan fissure!
                    if (isPhase2)
                    {
                        Vector3 leftDir = Quaternion.Euler(0, -22f, 0) * transform.forward;
                        Vector3 rightDir = Quaternion.Euler(0, 22f, 0) * transform.forward;
                        StartCoroutine(PropagateSeismicFracture(transform.position + leftDir * 1.5f, leftDir));
                        StartCoroutine(PropagateSeismicFracture(transform.position + rightDir * 1.5f, rightDir));
                    }
                }
                yield return null;
            }

            isAttacking = false;
            isActionLocked = false;
            currentState = MechCombatState.TacticalStalking;
            PlayAnim("Idle");
        }

        private IEnumerator PropagateSeismicFracture(Vector3 origin, Vector3 dir)
        {
            int totalNodes = 7;
            float stepDistance = 2.4f;

            for (int i = 0; i < totalNodes; i++)
            {
                Vector3 shockPoint = origin + dir * (i * stepDistance);
                shockPoint.y = 0.05f;

                // Progressive escalation requested by user:
                // Near to the mech is small (0.55x), and as the eruption travels farther away it becomes huge/colossal (3.2x)!
                float t = (float)i / (totalNodes - 1); // 0.0 at origin -> 1.0 at maximum distance
                float rockScale = Mathf.Lerp(0.55f, 3.25f, Mathf.Pow(t, 1.25f));

                // Violent 3D rock shards erupting upward - escalating in size with distance!
                SpawnRockStompEffect(shockPoint, rockScale, i >= 4);

                if (groundSlamVFX != null)
                {
                    var fx = Instantiate(groundSlamVFX, shockPoint, Quaternion.LookRotation(dir));
                    fx.transform.localScale = Vector3.one * (0.8f + t * 1.5f);
                    Destroy(fx, 2.5f);
                }

                // Dynamic damage and hit radius scaling with the escalating rock size
                float hitRadius = Mathf.Lerp(2.0f, 4.8f, t);
                float damage = Mathf.Lerp(50f, 95f, t);

                Collider[] hits = Physics.OverlapSphere(shockPoint, hitRadius);
                foreach (var col in hits)
                {
                    if (col.gameObject == gameObject) continue;
                    var dmg = col.GetComponentInParent<ISyntyDamageable>();
                    if (dmg != null && !(dmg is HammerMechAI))
                    {
                        dmg.TakeDamage(damage, shockPoint, dir, true);
                    }

                    var heroCC = col.GetComponentInParent<CharacterController>();
                    if (heroCC != null && heroCC.enabled && col.gameObject != gameObject)
                    {
                        // Visceral upward and outward kinetic impulse from erupting earth
                        Vector3 launchVec = dir * 2.2f + Vector3.up * (2.0f + t * 3.0f);
                        heroCC.Move(launchVec * Time.deltaTime * 12f);
                    }
                }

                // Camera trauma crescendo as the colossal distant boulders erupt
                if (i >= 4)
                {
                    SmoothOrbitCamera.ShakeImpact(0.35f + t * 1.3f, Vector3.up * 0.7f);
                }

                yield return new WaitForSeconds(0.075f);
            }
        }

        private IEnumerator PropagateRadialRockSurge(Vector3 center, Vector3 dir, int steps = 4, float stepDist = 2.3f)
        {
            for (int i = 0; i < steps; i++)
            {
                Vector3 pt = center + dir * ((i + 1) * stepDist);
                pt.y = 0.05f;

                float t = (float)i / (steps - 1);
                // Near is smaller (0.75x), farther expands into colossal rocks (2.6x)
                float scale = Mathf.Lerp(0.75f, 2.60f, t);

                SpawnRockStompEffect(pt, scale, i >= 2);

                yield return new WaitForSeconds(0.065f);
            }
        }

        private IEnumerator ThrusterDashRoutine(string dashAnim, Vector3 dashForce)
        {
            isActionLocked = true;
            currentState = MechCombatState.ThrusterEvasion;

            PlayAnim(dashAnim);
            PlaySFX(sfxThrusterBurst, 0.7f);
            if (hammerBooster != null) hammerBooster.TriggerBurst(0.50f, false);
            externalVelocity = dashForce;

            yield return new WaitForSeconds(0.65f);

            isActionLocked = false;
            currentState = MechCombatState.TacticalStalking;
            PlayAnim("Idle");
        }

        private IEnumerator DefensiveGuardRoutine(float duration)
        {
            isActionLocked = true;
            currentState = MechCombatState.DefensiveGuard;

            PlayAnim("Block");
            float timer = 0f;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                if (target != null)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(targetDir), Time.deltaTime * turnSpeed);
                }
                yield return null;
            }

            // End guard, return to stalking
            isActionLocked = false;
            currentState = MechCombatState.TacticalStalking;
            PlayAnim("Idle");
        }

        private IEnumerator HydraulicVentingWindow(float duration)
        {
            isActionLocked = true;
            currentState = MechCombatState.HydraulicVenting;

            // Paces slowly or stands while venting steam
            PlayAnim("WalkSlow");

            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.deltaTime;
                if (target != null)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(targetDir), Time.deltaTime * 2.0f);
                    controller.Move(transform.forward * (walkSpeed * 0.4f * Time.deltaTime));
                }
                yield return null;
            }

            isActionLocked = false;
            currentState = MechCombatState.TacticalStalking;
            PlayAnim("Idle");
        }

        private IEnumerator TacticalStalkApproach(float duration)
        {
            isActionLocked = true;
            currentState = MechCombatState.TacticalStalking;
            PlayAnim("WalkForward");

            float timer = 0f;
            float stepTimer = 0f;
            bool leftStep = false;
            while (timer < duration && distanceToTarget > meleeRange * 0.9f)
            {
                timer += Time.deltaTime;
                stepTimer += Time.deltaTime;
                if (stepTimer >= 0.58f)
                {
                    stepTimer = 0f;
                    leftStep = !leftStep;
                    Vector3 footPos = leftStep 
                        ? (leftFootSocket != null ? leftFootSocket.position : transform.position - transform.right * 0.8f)
                        : (rightFootSocket != null ? rightFootSocket.position : transform.position + transform.right * 0.8f);

                    SpawnRockStompEffect(footPos, 0.40f, false);
                    PlayMechFootstep(0.80f);
                    SmoothOrbitCamera.ShakeImpact(0.12f);
                }

                if (target != null)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(targetDir), Time.deltaTime * turnSpeed);
                    controller.Move(transform.forward * (walkSpeed * Time.deltaTime));
                }
                yield return null;
            }

            isActionLocked = false;
            PlayAnim("Idle");
        }

        private IEnumerator TacticalSprintApproach(float duration)
        {
            isActionLocked = true;
            currentState = MechCombatState.ClosingDistance;
            PlayAnim("Run");

            float timer = 0f;
            float stepTimer = 0f;
            bool leftStep = false;
            while (timer < duration && distanceToTarget > midRange * 0.8f)
            {
                timer += Time.deltaTime;
                stepTimer += Time.deltaTime;
                if (stepTimer >= 0.38f)
                {
                    stepTimer = 0f;
                    leftStep = !leftStep;
                    Vector3 footPos = leftStep 
                        ? (leftFootSocket != null ? leftFootSocket.position : transform.position - transform.right * 0.8f)
                        : (rightFootSocket != null ? rightFootSocket.position : transform.position + transform.right * 0.8f);

                    SpawnRockStompEffect(footPos, 0.55f, false);
                    PlayMechFootstep(1.0f);
                    SmoothOrbitCamera.ShakeImpact(0.18f);
                }

                if (target != null)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(targetDir), Time.deltaTime * turnSpeed);
                    controller.Move(transform.forward * (runSpeed * Time.deltaTime));
                }
                yield return null;
            }

            isActionLocked = false;
            PlayAnim("Idle");
        }

        #endregion

        #region Damage Dealing & Receiving (ISyntyDamageable)

        private void DealHammerDamage(float damage, float radius, bool isHeavy)
        {
            HashSet<GameObject> hitRoots = new HashSet<GameObject>();
            Vector3 forwardCenter = transform.position + transform.forward * 2.8f + Vector3.up * 1.2f;
            Collider[] hitsForward = Physics.OverlapSphere(forwardCenter, Mathf.Max(radius, 3.2f));
            Collider[] hitsSocket = hammerHeadSocket != null ? Physics.OverlapSphere(hammerHeadSocket.position, radius + 1.2f) : new Collider[0];

            List<Collider> allHits = new List<Collider>(hitsForward);
            allHits.AddRange(hitsSocket);

            foreach (var col in allHits)
            {
                if (col.gameObject == gameObject) continue;
                GameObject root = col.transform.root.gameObject;
                if (hitRoots.Contains(root)) continue;
                hitRoots.Add(root);

                var dmg = col.GetComponentInParent<ISyntyDamageable>();
                if (dmg != null && !(dmg is HammerMechAI))
                {
                    dmg.TakeDamage(damage, forwardCenter, transform.forward, isHeavy);
                }

                var warrior = col.GetComponentInParent<WarriorAnims.WarriorController>();
                if (warrior != null)
                {
                    warrior.GetHit();
                }
            }
        }

        private void DealRadialDamage(float damage, float radius)
        {
            HashSet<GameObject> hitRoots = new HashSet<GameObject>();
            Vector3 center = transform.position + Vector3.up * 1.2f;
            Collider[] hits = Physics.OverlapSphere(center, radius + 1.2f);

            foreach (var col in hits)
            {
                if (col.gameObject == gameObject) continue;
                GameObject root = col.transform.root.gameObject;
                if (hitRoots.Contains(root)) continue;
                hitRoots.Add(root);

                var dmg = col.GetComponentInParent<ISyntyDamageable>();
                if (dmg != null && !(dmg is HammerMechAI))
                {
                    Vector3 pushDir = (col.transform.position - transform.position).normalized;
                    dmg.TakeDamage(damage, col.transform.position, pushDir, true);
                }

                var warrior = col.GetComponentInParent<WarriorAnims.WarriorController>();
                if (warrior != null)
                {
                    warrior.GetHit();
                }
            }
        }

        /// <summary>
        /// Strictly checks entities within the Leap Slam landing radius.
        /// If the player is outside the visual holographic telegraph decal, they take ZERO damage,
        /// ZERO hit reaction, and ZERO knockback!
        /// </summary>
        private void DealLeapSlamLethalDamage(Vector3 landingPos, float radius)
        {
            HashSet<GameObject> hitRoots = new HashSet<GameObject>();
            Collider[] hits = Physics.OverlapSphere(landingPos, radius);

            foreach (var col in hits)
            {
                if (col.gameObject == gameObject) continue;
                GameObject root = col.transform.root.gameObject;
                if (hitRoots.Contains(root)) continue;
                hitRoots.Add(root);

                // Exact horizontal 2D distance check from landing center
                Vector3 hitFlat = new Vector3(col.transform.position.x, 0f, col.transform.position.z);
                Vector3 centerFlat = new Vector3(landingPos.x, 0f, landingPos.z);
                float dist = Vector3.Distance(hitFlat, centerFlat);

                // STRICT HITBOX COMPLIANCE: If the player has dodged outside the holographic decal radius, 0 damage!
                if (dist > radius) continue;

                // Falloff: Devastating 160 damage at epicenter, scaling to 80 damage at outer boundary
                float damage = Mathf.Lerp(160f, 80f, dist / radius);
                Vector3 knockDir = (hitFlat - centerFlat).normalized;
                if (knockDir.sqrMagnitude < 0.01f) knockDir = transform.forward;

                var dmg = col.GetComponentInParent<ISyntyDamageable>();
                if (dmg != null && !(dmg is HammerMechAI))
                {
                    dmg.TakeDamage(damage, landingPos, knockDir, true);
                }

                var warrior = col.GetComponentInParent<WarriorAnims.WarriorController>();
                if (warrior != null)
                {
                    warrior.GetHit();
                }

                // Kinetic shockwave knockback ONLY applies if caught inside the hazard radius
                var heroCC = col.GetComponentInParent<CharacterController>();
                if (heroCC != null && heroCC.enabled)
                {
                    heroCC.Move(knockDir * 3.8f + Vector3.up * 0.4f);
                }
            }
        }

        private void TriggerGroundSlamImpact()
        {
            PlaySFX(sfxHammerSlam != null ? sfxHammerSlam : MechAudioSynth.GetSeismicSlam(), 1.0f);

            Vector3 impactPos = hammerHeadSocket != null ? hammerHeadSocket.position : transform.position + transform.forward * 2.2f;
            impactPos.y = 0.05f;

            // Blinding HDR Point-Light Flash on ground and duelists
            SpawnImpactPointLight(impactPos, isPhase2 ? new Color(3.5f, 0.4f, 0.1f) : new Color(3.2f, 1.2f, 0.3f), 18.0f, 0.16f);

            if (groundSlamVFX != null)
            {
                var fx = Instantiate(groundSlamVFX, impactPos, Quaternion.identity);
                Destroy(fx, 3.5f);
            }

            // Cataclysmic Earth & Rock Eruption
            SpawnRockStompEffect(impactPos, 1.5f, true);

#if UNITY_EDITOR
            // Layer: Devastating explosive shockwave
            var impactPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonParticles/Prefabs/FX_Impact_Large_01.prefab");
            if (impactPrefab != null)
            {
                var impFX = Instantiate(impactPrefab, impactPos, Quaternion.identity);
                Destroy(impFX, 3.5f);
            }
#endif
        }

        /// <summary>
        /// Spawns visceral 3D rock shards, ground cracks, and dust clouds erupting from the ground
        /// on stomps, heavy footsteps, and seismic impacts.
        /// </summary>
        public void SpawnRockStompEffect(Vector3 worldPos, float scale = 1.0f, bool isHeavy = true)
        {
            Vector3 groundPos = new Vector3(worldPos.x, 0.05f, worldPos.z);

            // 1. Shattered 3D Rocks exploding upward from ground
            if (rockExplosionPrefab != null)
            {
                var rockFX = Instantiate(rockExplosionPrefab, groundPos, Quaternion.identity);
                rockFX.transform.localScale = Vector3.one * scale;

                // Ensure non-looping for clean burst and destroy after lifetime
                var pss = rockFX.GetComponentsInChildren<ParticleSystem>();
                foreach (var ps in pss)
                {
                    var main = ps.main;
                    main.loop = false;
                }
                Destroy(rockFX, 3.5f);
            }

            // 2. Cracked bedrock fracture blast (for medium/heavy stomps)
            if (groundCrackPrefab != null && scale >= 0.5f)
            {
                var crackFX = Instantiate(groundCrackPrefab, groundPos, Quaternion.identity);
                crackFX.transform.localScale = Vector3.one * scale;
                var pss = crackFX.GetComponentsInChildren<ParticleSystem>();
                foreach (var ps in pss)
                {
                    var main = ps.main;
                    main.loop = false;
                }
                Destroy(crackFX, 3.5f);
            }

            // 3. Stone impact debris & stone chunk dispersion
            if (stoneImpactPrefab != null)
            {
                var stoneFX = Instantiate(stoneImpactPrefab, groundPos, Quaternion.identity);
                stoneFX.transform.localScale = Vector3.one * (scale * 1.1f);
                Destroy(stoneFX, 2.5f);
            }

            // 4. Heavy atmospheric dust plume on heavy impacts
            if (isHeavy && dustBigPrefab != null)
            {
                var dustFX = Instantiate(dustBigPrefab, groundPos, Quaternion.identity);
                dustFX.transform.localScale = Vector3.one * scale;
                Destroy(dustFX, 3.0f);
            }
        }

        private void SpawnLaunchBurst(Vector3 pos)
        {
            if (groundSlamVFX != null)
            {
                var fx = Instantiate(groundSlamVFX, pos, Quaternion.identity);
                Destroy(fx, 2.0f);
            }

#if UNITY_EDITOR
            var dustPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonParticles/Prefabs/FX_Dust_Big_01.prefab");
            if (dustPrefab != null)
            {
                var fx = Instantiate(dustPrefab, pos, Quaternion.identity);
                Destroy(fx, 2.5f);
            }
#endif
        }

        private void SpawnSteamVents(Vector3 pos)
        {
#if UNITY_EDITOR
            var steamPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonParticles/Prefabs/FX_Steam_02.prefab");
            if (steamPrefab != null)
            {
                var fx = Instantiate(steamPrefab, pos + Vector3.up * 1.6f, Quaternion.Euler(-90, 0, 0));
                Destroy(fx, 2.5f);
            }
#endif
        }

        public void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitDirection, bool isHeavy)
        {
            if (currentState == MechCombatState.Dead) return;

            lastDamageTime = Time.time;

            // Check if actively guarding with war hammer shaft
            if (currentState == MechCombatState.DefensiveGuard)
            {
                // Guarded! 85% damage mitigation + deflection sparks
                float mitigated = damage * 0.15f;
                currentHealth = Mathf.Max(0f, currentHealth - mitigated);
                AudioClip clang = sfxBlockClang != null ? sfxBlockClang : CyberCombat.Audio.CyberCombatAudioSynth.GetHeavyImpact();
                PlaySFX(clang, 1.0f);
                PlayAnim("BlockHitReact");

                // Blinding needle sparks from Damascus blade clashing against war hammer
                SpawnBlockSparks(hitPoint);

                // Immediate counter-attack!
                StopAllCoroutines();
                StartCoroutine(GuardCounterRoutine());
                return;
            }

            // Normal damage processing
            currentHealth = Mathf.Max(0f, currentHealth - damage);
            if (healthBar != null) healthBar.SetHealth(currentHealth);

            // Poise calculation
            float poiseDmg = isHeavy ? damage * 1.5f : damage * 0.6f;
            currentPoise = Mathf.Max(0f, currentPoise - poiseDmg);
            if (healthBar != null) healthBar.SetPosture(maxPoise - currentPoise, maxPoise);

            if (currentHealth <= 0f)
            {
                Die(hitDirection, hitPoint);
                return;
            }

            // Check Phase 2 Berserk Overdrive threshold (50% HP)
            if (!isPhase2 && currentHealth <= maxHealth * 0.5f)
            {
                StopAllCoroutines();
                StartCoroutine(Phase2TransitionRoutine());
                return;
            }

            // Check poise break
            if (currentPoise <= 0f && currentState != MechCombatState.Staggered && currentState != MechCombatState.Stunned)
            {
                TriggerStagger();
            }
        }

        private IEnumerator Phase2TransitionRoutine()
        {
            isPhase2 = true;
            isActionLocked = true;
            currentState = MechCombatState.HydraulicVenting;

            // Emergency Klaxon Alarm
            AudioClip alarm = sfxStaggerAlarm != null ? sfxStaggerAlarm : MechAudioSynth.GetOverdriveAlarm();
            PlaySFX(alarm, 1.0f);

            if (healthBar != null) healthBar.SetEnraged(true);
            Cyberpunk.Mars.MarsFinalBattleDirector.Instance?.TriggerAlert("[!] CRITICAL: TITAN REACTOR MELTDOWN // BERSERK OVERDRIVE ENGAGED [!]", 3.5f, true);

            // Boss raises war hammer, prepares thermal purge
            PlayAnim("Block");
            yield return new WaitForSeconds(0.40f);

            // Thermal steam explosion & radial shockwave
            TriggerGroundSlamImpact();
            DealRadialDamage(45f, 7.5f);
            SmoothOrbitCamera.ShakeImpact(1.8f, Vector3.up * 0.6f);

            // Overclock combat attributes
            runSpeed = 9.8f;
            walkSpeed = 4.8f;
            turnSpeed = 6.5f;
            poiseRecoveryRate = 45f;

            yield return new WaitForSeconds(1.0f);

            isActionLocked = false;
            currentState = MechCombatState.TacticalStalking;
            PlayAnim("Idle");
        }

        private IEnumerator PropagateExpandingPlasmaRing(Vector3 center, float maxRadius, float duration)
        {
            GameObject ringObj = new GameObject("Phase2_PlasmaShockwaveRing");
            ringObj.transform.position = new Vector3(center.x, 0.1f, center.z);
            LineRenderer lr = ringObj.AddComponent<LineRenderer>();
            var s = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            lr.material = new Material(s);
            lr.startWidth = 0.4f;
            lr.endWidth = 0.4f;
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = 36;
            Color col = new Color(3.5f, 0.2f, 0.4f, 0.9f); // Intense Overdrive Magenta-Red
            lr.startColor = col;
            lr.endColor = col;

            float elapsed = 0f;
            HashSet<GameObject> hitObjects = new HashSet<GameObject>();

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / duration;
                float radius = Mathf.Lerp(1.5f, maxRadius, Mathf.Pow(progress, 0.6f));

                for (int i = 0; i < 36; i++)
                {
                    float rad = Mathf.Deg2Rad * (i * 10f);
                    lr.SetPosition(i, new Vector3(Mathf.Cos(rad) * radius, 0.05f, Mathf.Sin(rad) * radius));
                }

                float alpha = 1f - progress;
                Color curCol = new Color(col.r, col.g, col.b, alpha);
                lr.startColor = curCol;
                lr.endColor = curCol;

                Collider[] hits = Physics.OverlapSphere(center, radius + 0.6f);
                foreach (var c in hits)
                {
                    if (c.gameObject == gameObject) continue;
                    float d = Vector3.Distance(new Vector3(c.transform.position.x, 0, c.transform.position.z), new Vector3(center.x, 0, center.z));
                    if (Mathf.Abs(d - radius) < 1.2f && hitObjects.Add(c.transform.root.gameObject))
                    {
                        var dmg = c.GetComponentInParent<ISyntyDamageable>();
                        if (dmg != null && !(dmg is HammerMechAI))
                        {
                            Vector3 dir = (c.transform.position - center).normalized;
                            dmg.TakeDamage(55f, c.transform.position, dir, true);
                            SmoothOrbitCamera.ShakeImpact(0.8f, dir * 0.3f);
                        }
                    }
                }

                yield return null;
            }

            Destroy(ringObj);
        }

        private void SpawnImpactPointLight(Vector3 pos, Color color, float range, float duration)
        {
            GameObject lightObj = new GameObject("HammerImpactLight");
            lightObj.transform.position = pos + Vector3.up * 0.8f;
            Light l = lightObj.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.intensity = 5.0f;
            Destroy(lightObj, duration);
        }

        private void SpawnBlockSparks(Vector3 pos)
        {
            GameObject sparkObj = new GameObject("BlockDeflectionSparks");
            sparkObj.transform.position = pos;
            var ps = sparkObj.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main;
            m.duration = 0.20f;
            m.startLifetime = 0.25f;
            m.startSpeed = 14f;
            m.startSize = 0.16f;
            m.startColor = new ParticleSystem.MinMaxGradient(new Color(2.5f, 1.8f, 0.4f), Color.white);
            m.stopAction = ParticleSystemStopAction.Destroy;
            ps.emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 32) });
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = 0.12f;
            ps.Play();
            Destroy(sparkObj, 0.5f);
        }

        private IEnumerator GuardCounterRoutine()
        {
            isActionLocked = true;
            isAttacking = true;
            currentState = MechCombatState.GuardCounter;

            yield return new WaitForSeconds(0.2f);

            SmoothFaceTarget(targetDir, 0.15f);
            PlayAnim("Attack2_solo"); // Fast counter cleave!
            DealHammerDamage(80f, 2.5f, false);

            yield return new WaitForSeconds(0.9f);

            isAttacking = false;
            isActionLocked = false;
            currentState = MechCombatState.TacticalStalking;
            PlayAnim("Idle");
        }

        private void TriggerStagger()
        {
            StopAllCoroutines();
            StartCoroutine(StaggerRoutine());
        }

        private IEnumerator StaggerRoutine()
        {
            isActionLocked = true;
            isAttacking = false;
            currentState = MechCombatState.Staggered;

            if (healthBar != null) healthBar.SetStaggered(true);
            PlaySFX(sfxStaggerAlarm, 0.85f);
            PlayAnim("LightHit");

            currentPoise = maxPoise * 0.5f; // Recover half poise
            if (healthBar != null) healthBar.SetPosture(maxPoise - currentPoise, maxPoise);
            yield return new WaitForSeconds(1.1f);

            // Reset back to combat
            if (healthBar != null) healthBar.SetStaggered(false);
            isActionLocked = false;
            currentState = MechCombatState.TacticalStalking;
            PlayAnim("Idle");
        }

        private void Die(Vector3 hitDirection = default, Vector3 hitPoint = default)
        {
            StopAllCoroutines();
            currentState = MechCombatState.Dead;
            isActionLocked = true;
            isAttacking = false;

            if (healthBar != null) healthBar.SetHealth(0f);
            if (hammerBooster != null) hammerBooster.SetState(BoosterFlameState.Off);

            // Trigger full physical ragdoll collapse
            if (ragdollController != null)
            {
                ragdollController.TriggerRagdollDeath(hitDirection, hitPoint);
            }
            else
            {
                PlayAnim("Death");
                if (controller != null) controller.enabled = false;
            }
        }

        #endregion

        #region Helpers

        private void PlayAnim(string stateName)
        {
            if (animator == null) return;
            // Strict enforcement: special attacks and move attacks are completely disabled
            if (stateName == "SpecialAttack1" || stateName == "MoveAttack1")
            {
                stateName = "Attack1";
            }
            animator.CrossFadeInFixedTime(stateName, 0.15f, 0, 0f);
        }

        private void SmoothFaceTarget(Vector3 dir, float time)
        {
            if (dir.sqrMagnitude < 0.001f) return;
            transform.rotation = Quaternion.LookRotation(dir);
        }

        private void PlaySFX(AudioClip clip, float volume = 1f)
        {
            if (clip != null && audioSource != null)
            {
                audioSource.PlayOneShot(clip, volume);
            }
        }

        #endregion
    }
}
