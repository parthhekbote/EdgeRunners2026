using System.Collections;
using UnityEngine;
using SyntyCombined;

namespace PolygonMech
{
    /// <summary>
    /// KENSEI-01 // BLADE MECH — High-aggression samurai mech boss.
    /// Harder version: faster combos, tighter counter window, relentless Iaido pressure,
    /// Phase 2 triple-chain, 4-stage punish counter system.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Animator))]
    public class SamuraiMechAI : MonoBehaviour, ISyntyDamageable
    {
        public enum SamuraiState
        {
            Spawning,
            Stance,
            Advance,
            KatanaSlash,
            IaidoDash,
            BladeWhirlwind,
            CounterStance,
            CounterPunish,
            Staggered,
            BerserkTransition,
            Dead
        }

        [Header("Boss Identity")]
        public string bossName = "KENSEI-01 // BLADE MECH";

        [Header("Stats")]
        public float maxHealth = 2800f;
        public float currentHealth { get; private set; }

        [Header("Combat Distances & Spacing")]
        [SerializeField] private float meleeRange            = 7.5f;
        [SerializeField] private float idealOrbitDistance    = 7.0f;
        [SerializeField] private float dashRange             = 16.0f;
        [SerializeField] private float crowdDistance         = 5.5f;
        [SerializeField] private float minHardBufferDistance = 5.0f;

        [Header("Movement")]
        [SerializeField] private float walkSpeed  = 4.0f;
        [SerializeField] private float runSpeed   = 7.0f;
        [SerializeField] private float dashSpeed  = 24.0f;   // Faster dash (was 18)
        [SerializeField] private float turnSpeed  = 7.0f;
        [SerializeField] private float gravity    = 18f;

        [Header("Attack Cooldowns")]
        [SerializeField] private float phase1Cooldown = 1.4f;
        [SerializeField] private float phase2Cooldown = 0.60f;

        [Header("Damage Values")]
        [SerializeField] private float katanaSlashDamage    = 38f;
        [SerializeField] private float iaidoDashDamage      = 65f;
        [SerializeField] private float whirlwindDamage      = 52f;
        [SerializeField] private float counterPunishDamage  = 85f;

        [Header("Super Armor & Poise System")]
        [SerializeField] private float maxPoise                    = 450f;
        [SerializeField] private float poiseDecayRate              = 22f;
        [SerializeField] private float poiseDecayDelay             = 4.5f;
        [SerializeField] private float criticalVulnerabilityMultiplier = 1.8f;
        public  float currentPoise { get; private set; } = 0f;
        private float lastDamageTime = 0f;
        private bool  isStaggered    = false;

        [Header("Phase 2 Berserk")]
        [Range(0f, 1f)]
        [SerializeField] private float berserkThreshold        = 0.50f;  // Earlier phase 2 (was 0.45)
        [SerializeField] private float berserkDamageMultiplier = 1.45f;
        [SerializeField] private float berserkSpeedMultiplier  = 1.30f;

        [Header("VFX Prefabs")]
        [SerializeField] private GameObject bladeTrailFXPrefab;
        [SerializeField] private GameObject groundSlashFXPrefab;
        [SerializeField] private GameObject whirlwindFXPrefab;
        [SerializeField] private GameObject berserkActivateFXPrefab;
        [SerializeField] private GameObject deathExplosionFXPrefab;

        [Header("VFX Spawn Points")]
        [SerializeField] private Transform katanaTip;
        [SerializeField] private Transform slamPoint;

        [Header("Authentic Heavy Titan Mech Audio")]
        public AudioClip[] sfxTitanFootsteps;
        public AudioClip sfxChassisReactorLoop;
        public AudioClip[] sfxHydraulicActuators;

        // Components
        private CharacterController controller;
        private Animator            anim;
        private BossHealthBar       healthBar;
        private AudioSource         audioSource;
        private AudioSource         servoLoopAudioSource;
        private Vector3             lastMechPosition;
        private float               lastMechRotationY;
        private float               currentServoVolume = 0f;
        private float               nextMoveServoWhineTime = 0f;

        // State Machine
        public  SamuraiState CurrentState { get; private set; } = SamuraiState.Spawning;
        private Transform target;
        private bool  isPhase2    = false;
        private bool  isAttacking = false;
        private float nextAttackTime   = 0f;
        private float verticalVelocity = 0f;

        // Counter-stance
        private bool  inCounterStance    = false;
        private float counterStanceTimer = 0f;
        private const float CounterWindowDuration = 1.8f;  // Tighter window (was 2.2)

        // Combo chain tracking
        private int comboChainCount = 0;

        // Orbiting
        private float orbitDirection      = 1f;
        private float nextOrbitSwitchTime = 0f;

        // Animator hashes
        private static readonly int ParamSpeed      = Animator.StringToHash("Speed");
        private static readonly int ParamSlash      = Animator.StringToHash("KatanaSlash");
        private static readonly int ParamDash       = Animator.StringToHash("IaidoDash");
        private static readonly int ParamWhirlwind  = Animator.StringToHash("BladeWhirlwind");
        private static readonly int ParamCounter    = Animator.StringToHash("CounterStance");
        private static readonly int ParamCounterHit = Animator.StringToHash("CounterPunish");
        private static readonly int ParamBerserk    = Animator.StringToHash("Berserk");
        private static readonly int ParamStagger    = Animator.StringToHash("Stagger");
        private static readonly int ParamDie        = Animator.StringToHash("Die");

        private void Awake() => EnsureInitialized();

        public void EnsureInitialized()
        {
            if (controller == null) controller = GetComponent<CharacterController>();
            if (anim       == null) anim       = GetComponent<Animator>();
            if (anim       != null) anim.speed = 0.95f;
            if (healthBar  == null) healthBar  = GetComponent<BossHealthBar>() ?? gameObject.AddComponent<BossHealthBar>();

            if (currentHealth <= 0f) currentHealth = maxHealth;
            if (healthBar != null)
            {
                healthBar.bossName = bossName;
                healthBar.Initialize(maxHealth);
                healthBar.SetPosture(currentPoise, maxPoise);
            }

            if (audioSource == null) audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();

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

            if (servoLoopAudioSource == null)
            {
                GameObject servoObj = new GameObject("Audio_SamuraiMechMovementServos");
                servoObj.transform.SetParent(transform, false);
                servoLoopAudioSource = servoObj.AddComponent<AudioSource>();
                servoLoopAudioSource.spatialBlend = 1.0f;
                servoLoopAudioSource.rolloffMode = AudioRolloffMode.Linear;
                servoLoopAudioSource.minDistance = 3.5f;
                servoLoopAudioSource.maxDistance = 50.0f;
                servoLoopAudioSource.loop = true;
                servoLoopAudioSource.clip = sfxChassisReactorLoop != null ? sfxChassisReactorLoop : MechAudioSynth.GetMovementServoLoop();
                servoLoopAudioSource.volume = 0f;
                servoLoopAudioSource.pitch = 1.0f;
                servoLoopAudioSource.playOnAwake = false;
                servoLoopAudioSource.Play();
            }

            lastMechPosition = transform.position;
            lastMechRotationY = transform.eulerAngles.y;
        }

        private void Start()
        {
            FindTarget();
            StartCoroutine(SpawnSequence());
        }

        private void Update()
        {
            if (CurrentState == SamuraiState.Dead)
            {
                if (servoLoopAudioSource != null && servoLoopAudioSource.isPlaying)
                {
                    servoLoopAudioSource.volume = Mathf.MoveTowards(servoLoopAudioSource.volume, 0f, Time.deltaTime * 4f);
                }
                return;
            }

            if (target == null || !target.gameObject.activeInHierarchy) FindTarget();

            ApplyGravity();
            UpdateMechanicalMovementAudio();

            // Poise decay
            if (!isStaggered && Time.time - lastDamageTime > poiseDecayDelay && currentPoise > 0f)
            {
                currentPoise = Mathf.Max(0f, currentPoise - poiseDecayRate * Time.deltaTime);
                healthBar?.SetPosture(currentPoise, maxPoise);
            }

            // Counter-stance timeout
            if (inCounterStance)
            {
                counterStanceTimer -= Time.deltaTime;
                if (counterStanceTimer <= 0f)
                {
                    inCounterStance = false;
                    ExitCounterStance();
                }
            }

            if (CurrentState == SamuraiState.Stance || CurrentState == SamuraiState.Advance)
                HandleTacticalCombatLoop();
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

            bool isMoving = (linearSpeed > 0.15f || angularSpeed > 10f) && CurrentState != SamuraiState.Dead;
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

                if (angularSpeed > 28f && Time.time >= nextMoveServoWhineTime)
                {
                    nextMoveServoWhineTime = Time.time + UnityEngine.Random.Range(1.1f, 1.7f);
                    PlayHydraulicActuator(0.40f);
                }
            }

            currentServoVolume = Mathf.MoveTowards(currentServoVolume, targetVol, dt * 2.5f);
            servoLoopAudioSource.volume = currentServoVolume;
            servoLoopAudioSource.pitch = Mathf.Lerp(servoLoopAudioSource.pitch, targetPitch, dt * 3.0f);
        }

        public void FootL() => PlayMechFootstep(1.0f);
        public void FootR() => PlayMechFootstep(1.0f);

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

        private IEnumerator SpawnSequence()
        {
            ChangeState(SamuraiState.Spawning);
            anim.SetFloat(ParamSpeed, 0f);

            float approachDist  = 7.5f;
            float maxApproachT  = 4.0f;
            float timer         = 0f;

            while (timer < maxApproachT)
            {
                if (target == null) FindTarget();
                if (target == null) break;
                Vector3 toT = (target.position - transform.position); toT.y = 0;
                float d = toT.magnitude;
                if (d <= approachDist) break;
                Vector3 fwd = toT.normalized;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(fwd), Time.deltaTime * 5f);
                controller.Move(fwd * walkSpeed * Time.deltaTime);
                anim.SetFloat(ParamSpeed, 1.0f, 0.15f, Time.deltaTime);
                timer += Time.deltaTime;
                yield return null;
            }

            anim.SetFloat(ParamSpeed, 0f, 0.1f, Time.deltaTime);
            yield return new WaitForSeconds(0.3f);
            FaceTarget();

            // Intimidating spin flourish taunt
            anim.SetTrigger(ParamWhirlwind);
            yield return new WaitForSeconds(1.8f);

            ChangeState(SamuraiState.Stance);
            nextOrbitSwitchTime = Time.time + Random.Range(2f, 4f);
            nextAttackTime      = Time.time + Random.Range(phase1Cooldown * 0.5f, phase1Cooldown);
        }

        // ══════════════════════════════════════════════════════════════════════
        // TACTICAL COMBAT LOOP
        // ══════════════════════════════════════════════════════════════════════
        private void HandleTacticalCombatLoop()
        {
            if (isAttacking || target == null) return;

            Vector3 toTarget = (target.position - transform.position); toTarget.y = 0;
            float distance   = toTarget.magnitude;
            Vector3 fwd      = toTarget.normalized;

            FaceTarget();

            if (Time.time >= nextOrbitSwitchTime)
            {
                orbitDirection      = (Random.value < 0.5f) ? 1f : -1f;
                nextOrbitSwitchTime = Time.time + Random.Range(2.5f, 5f);
            }

            if (Time.time >= nextAttackTime)
            {
                float roll = Random.value;

                if (distance <= meleeRange)
                {
                    if (isPhase2)
                    {
                        // Phase 2: aggressive 4-attack mix with higher counter chance
                        if      (roll < 0.35f) StartCoroutine(AttackKatanaSlash());
                        else if (roll < 0.55f) StartCoroutine(AttackBladeWhirlwind());
                        else if (roll < 0.75f) StartCoroutine(AttackCounterStance());
                        else                   StartCoroutine(AttackKatanaSlash()); // Double slash
                    }
                    else
                    {
                        if      (roll < 0.45f) StartCoroutine(AttackKatanaSlash());
                        else if (roll < 0.70f) StartCoroutine(AttackCounterStance());
                        else                   StartCoroutine(AttackBladeWhirlwind());
                    }
                    return;
                }
                else if (distance <= dashRange && distance > minHardBufferDistance + 1.5f)
                {
                    // Relentlessly close with Iaido dash
                    if (roll < 0.75f) StartCoroutine(AttackIaidoDash());
                    else              ChangeState(SamuraiState.Advance);
                    return;
                }
                else
                {
                    // If very far, still dash (never just stand)
                    StartCoroutine(AttackIaidoDash());
                }
            }

            // Movement
            Vector3 moveVector   = Vector3.zero;
            float   animSpeedVal = 0f;

            if (distance < crowdDistance)
            {
                float backSpd = (distance < minHardBufferDistance) ? runSpeed * 1.4f : walkSpeed * 1.5f;
                Vector3 strafe = Vector3.Cross(Vector3.up, fwd) * (orbitDirection * 0.4f);
                moveVector   = (-fwd + strafe).normalized * backSpd;
                animSpeedVal = (distance < minHardBufferDistance) ? 2f : 1f;
            }
            else if (distance > idealOrbitDistance + 1.0f)
            {
                float stalkSpd = (distance > idealOrbitDistance * 1.8f && isPhase2) ? runSpeed : walkSpeed;
                moveVector   = fwd * stalkSpd;
                animSpeedVal = (stalkSpd > walkSpeed) ? 2f : 1f;
            }
            else
            {
                Vector3 strafeDir = Vector3.Cross(Vector3.up, fwd) * orbitDirection;
                float   distErr   = distance - idealOrbitDistance;
                moveVector   = (strafeDir + fwd * (distErr * 0.5f)).normalized * walkSpeed;
                animSpeedVal = 0.9f;
            }

            if (moveVector.sqrMagnitude > 0.01f)
            {
                controller.Move(moveVector * Time.deltaTime);
                anim.SetFloat(ParamSpeed, animSpeedVal, 0.12f, Time.deltaTime);
            }
            else
            {
                anim.SetFloat(ParamSpeed, 0f, 0.15f, Time.deltaTime);
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
        // WaitForAnimation
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
        // ATTACK SEQUENCES
        // ══════════════════════════════════════════════════════════════════════

        private IEnumerator AttackKatanaSlash()
        {
            isAttacking = true;
            ChangeState(SamuraiState.KatanaSlash);
            anim.SetFloat(ParamSpeed, 0f);
            anim.SetTrigger(ParamSlash);

            float dmg = katanaSlashDamage * (isPhase2 ? berserkDamageMultiplier : 1f);
            Vector3 lungeDir = transform.forward;

            yield return StartCoroutine(WaitForAnimation("KatanaSlash", 0.40f,
                onImpact: () =>
                {
                    Vector3 fxPos = katanaTip != null ? katanaTip.position : transform.position + transform.forward * 2f + Vector3.up * 2f;
                    SpawnFX(bladeTrailFXPrefab, fxPos);
                    DealDamage(dmg, meleeRange + 1.8f, 9f);
                },
                onProgress: normT =>
                {
                    if (normT >= 0.12f && normT <= 0.40f)
                    {
                        float d = target != null ? Vector3.Distance(transform.position, target.position) : 10f;
                        if (d > minHardBufferDistance + 0.6f)
                            controller.Move(lungeDir * (walkSpeed * 1.1f) * Time.deltaTime);
                    }
                    if (normT < 0.18f) FaceTarget();
                }
            ));

            // Phase 2: up to 3-hit katana combo
            if (isPhase2 && target != null)
            {
                comboChainCount++;
                if (comboChainCount <= 2)
                {
                    float d = Vector3.Distance(transform.position, target.position);
                    float roll = Random.value;
                    if (d <= meleeRange + 2.0f)
                    {
                        if      (roll < 0.45f) { yield return StartCoroutine(AttackKatanaSlash()); yield break; }
                        else if (roll < 0.70f) { yield return StartCoroutine(AttackBladeWhirlwind()); yield break; }
                        else                   { yield return StartCoroutine(AttackIaidoDash()); yield break; }
                    }
                }
            }

            comboChainCount = 0;
            ResetAttack();
        }

        private IEnumerator AttackIaidoDash()
        {
            if (target == null) { ResetAttack(); yield break; }

            isAttacking = true;
            ChangeState(SamuraiState.IaidoDash);
            anim.SetFloat(ParamSpeed, 0f);
            anim.SetTrigger(ParamDash);

            Vector3 dashDir = (target.position - transform.position); dashDir.y = 0; dashDir.Normalize();
            transform.rotation = Quaternion.LookRotation(dashDir);

            float spd = dashSpeed * (isPhase2 ? berserkSpeedMultiplier : 1f);

            yield return StartCoroutine(WaitForAnimation("IaidoDash", 0.42f,
                onImpact: () =>
                {
                    SpawnFX(groundSlashFXPrefab, transform.position + transform.forward * 2.5f);
                    DealDamage(iaidoDashDamage * (isPhase2 ? berserkDamageMultiplier : 1f), meleeRange + 2.0f, 18f);
                },
                onProgress: normT =>
                {
                    if (normT < 0.42f)
                    {
                        float d = target != null ? Vector3.Distance(transform.position, target.position) : 10f;
                        if (d > minHardBufferDistance + 1.0f)
                            controller.Move(dashDir * spd * Time.deltaTime);
                    }
                }
            ));

            // Phase 2: always follow Iaido with a Slash
            if (isPhase2 && comboChainCount == 0 && Random.value < 0.65f)
            {
                comboChainCount = 1;
                yield return StartCoroutine(AttackKatanaSlash());
                yield break;
            }

            comboChainCount = 0;
            ResetAttack();
        }

        private IEnumerator AttackBladeWhirlwind()
        {
            isAttacking = true;
            ChangeState(SamuraiState.BladeWhirlwind);
            anim.SetFloat(ParamSpeed, 0f);
            anim.SetTrigger(ParamWhirlwind);
            SpawnFX(whirlwindFXPrefab, transform.position);

            float wDmg = whirlwindDamage * (isPhase2 ? berserkDamageMultiplier : 1f);
            // FIX: first tick starts AFTER impactNormTime to avoid double-hit on initial frame
            float nextTick = 0.50f;  // was 0.30 which double-fired with onImpact at 0.35

            yield return StartCoroutine(WaitForAnimation("BladeWhirlwind", 0.35f,
                onImpact: () =>
                {
                    DealDamage(wDmg * 0.5f, meleeRange + 2.0f, 7f);
                },
                onProgress: normT =>
                {
                    if (normT < 0.90f)
                        transform.Rotate(Vector3.up, 360f * Time.deltaTime * 2.5f);

                    if (normT >= nextTick)
                    {
                        DealDamage(wDmg * 0.35f, meleeRange + 2.0f, 7f);
                        nextTick += 0.30f;
                    }
                }
            ));

            ResetAttack();
        }

        private IEnumerator AttackCounterStance()
        {
            isAttacking        = true;
            inCounterStance    = true;
            counterStanceTimer = CounterWindowDuration;
            ChangeState(SamuraiState.CounterStance);
            anim.SetFloat(ParamSpeed, 0f);
            anim.SetTrigger(ParamCounter);

            while (inCounterStance)
            {
                FaceTarget();
                yield return null;
            }
            // inCounterStance was cleared by TakeDamage (counter triggered) or timeout
        }

        private void ExitCounterStance()
        {
            inCounterStance = false;
            if (target != null && Vector3.Distance(transform.position, target.position) <= meleeRange + 1.5f)
                StartCoroutine(AttackKatanaSlash());
            else
                ResetAttack();
        }

        private IEnumerator CounterPunish()
        {
            inCounterStance = false;
            ChangeState(SamuraiState.CounterPunish);
            anim.SetTrigger(ParamCounterHit);

            yield return StartCoroutine(WaitForAnimation("CounterPunish", 0.38f, () =>
            {
                SpawnFX(groundSlashFXPrefab, transform.position + transform.forward * 2.0f);
                DealDamage(counterPunishDamage * (isPhase2 ? berserkDamageMultiplier : 1f), meleeRange + 2.0f, 20f);
            }));

            // After successful counter always chain into Whirlwind in phase 2
            if (isPhase2 && comboChainCount == 0)
            {
                comboChainCount = 1;
                yield return StartCoroutine(AttackBladeWhirlwind());
                yield break;
            }

            comboChainCount = 0;
            ResetAttack();
        }

        // ══════════════════════════════════════════════════════════════════════
        // DAMAGE / POISE / DEATH
        // ══════════════════════════════════════════════════════════════════════
        public void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitDirection, bool isHeavy)
        {
            EnsureInitialized();
            if (CurrentState == SamuraiState.Dead) return;

            // Counter-parry: deflect and punish immediately
            if (inCounterStance)
            {
                inCounterStance = false;
                StopAllCoroutines();
                isAttacking = false;  // FIX: reset so CounterPunish can set it
                StartCoroutine(CounterPunish());
                return;
            }

            lastDamageTime = Time.time;

            float actualDamage = isStaggered ? damage * criticalVulnerabilityMultiplier : damage;
            currentHealth -= actualDamage;
            healthBar?.SetHealth(currentHealth);

            // Phase 2 transition
            if (!isPhase2 && currentHealth <= maxHealth * berserkThreshold)
            {
                StopAllCoroutines();
                isAttacking     = false;  // FIX: reset before coroutine
                inCounterStance = false;
                StartCoroutine(BerserkTransition());
                return;
            }

            // Poise accumulation
            if (!isStaggered)
            {
                float poiseDmg = isHeavy ? damage * 1.8f : damage * 1.1f;
                currentPoise += poiseDmg;
                healthBar?.SetPosture(currentPoise, maxPoise);

                if (currentPoise >= maxPoise)
                {
                    StopAllCoroutines();
                    isAttacking     = false;  // FIX: reset
                    inCounterStance = false;
                    StartCoroutine(DoStagger());
                    return;
                }
            }

            if (currentHealth <= 0f) Die();
        }

        private IEnumerator DoStagger()
        {
            isStaggered  = true;
            isAttacking  = true;
            currentPoise = maxPoise;
            ChangeState(SamuraiState.Staggered);
            anim.SetFloat(ParamSpeed, 0f);
            anim.SetTrigger(ParamStagger);

            healthBar?.SetPosture(maxPoise, maxPoise);
            healthBar?.SetStaggered(true);

            yield return new WaitForSeconds(3.0f);

            currentPoise = 0f;
            isStaggered  = false;
            healthBar?.SetPosture(0f, maxPoise);
            healthBar?.SetStaggered(false);

            isAttacking = false;
            ResetAttack();
        }

        private IEnumerator BerserkTransition()
        {
            isPhase2    = true;
            isAttacking = true;
            ChangeState(SamuraiState.BerserkTransition);
            anim.SetFloat(ParamSpeed, 0f);
            anim.SetTrigger(ParamBerserk);

            healthBar?.SetEnraged(true);
            SpawnFX(berserkActivateFXPrefab, transform.position + Vector3.up * 1.5f);

            if (anim != null) anim.speed = 1.10f;
            walkSpeed      *= berserkSpeedMultiplier;
            runSpeed       *= berserkSpeedMultiplier;
            dashSpeed      *= berserkSpeedMultiplier;
            turnSpeed      *= 1.40f;
            phase1Cooldown  = phase2Cooldown;

            yield return new WaitForSeconds(2.0f);

            isAttacking = false;
            ResetAttack();
        }

        private void Die()
        {
            ChangeState(SamuraiState.Dead);
            StopAllCoroutines();
            anim.SetTrigger(ParamDie);
            healthBar?.SetPosture(0f, maxPoise);
            healthBar?.SetStaggered(false);
            SpawnFX(deathExplosionFXPrefab, transform.position + Vector3.up * 1.0f);
            controller.enabled = false;
            Debug.Log($"[SamuraiMech] {bossName} DEFEATED!");
        }

        // ══════════════════════════════════════════════════════════════════════
        // HELPERS
        // ══════════════════════════════════════════════════════════════════════
        private void DealDamage(float damage, float radius, float knockback)
        {
            if (target == null) return;
            if (Vector3.Distance(transform.position, target.position) > radius) return;
            var dmgable = target.GetComponent<ISyntyDamageable>()
                       ?? target.GetComponentInParent<ISyntyDamageable>()
                       ?? target.GetComponentInChildren<ISyntyDamageable>();
            if (dmgable != null)
            {
                Vector3 dir = (target.position - transform.position).normalized;
                dmgable.TakeDamage(damage, target.position, dir, knockback > 8f);
                CyberCombat.Audio.CyberCombatAudioSynth.PlayAtPoint(
                    knockback > 8f
                        ? CyberCombat.Audio.CyberCombatAudioSynth.GetHeavyImpact()
                        : CyberCombat.Audio.CyberCombatAudioSynth.GetSwordImpact(),
                    target.position, 1.1f, 1.05f);
            }
        }

        private void SpawnFX(GameObject prefab, Vector3 pos)
        {
            if (prefab == null) return;
            Destroy(Instantiate(prefab, pos, Quaternion.identity), 5f);
        }

        private void ResetAttack()
        {
            isAttacking    = false;
            anim.SetFloat(ParamSpeed, 0f);
            ChangeState(SamuraiState.Stance);
            nextAttackTime = Time.time + (isPhase2 ? phase2Cooldown : phase1Cooldown);
        }

        private void ChangeState(SamuraiState state) => CurrentState = state;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;    Gizmos.DrawWireSphere(transform.position, meleeRange);
            Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, idealOrbitDistance);
            Gizmos.color = Color.cyan;   Gizmos.DrawWireSphere(transform.position, dashRange);
        }
    }
}
