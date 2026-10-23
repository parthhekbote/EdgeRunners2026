using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using InsaneGun.Combat;
using SyntyCombined;
using CyberCombat.Coordination;

namespace Cyberpunk.Missions
{
    /// <summary>
    /// Tracks individual Mission 1 enemy state:
    /// - Billboarded floating health bar + red nameplate + red diamond marker
    /// - Menacing idle patrol / rival sparring until mission accepted
    /// - Proximity (12m) and on-damage aggro trigger
    /// - Staggered activation delays (Thug 1 instant, Thug 2 0.8s, Thug 3 1.6s)
    /// - True Blackwall Rogue AI combat characteristics:
    ///   * Relentless aggression, fast 0.40s attack intervals, 5.8m/s run speed
    ///   * Multi-layered combos: ExecutionerShred, BlitzkriegOverdrive, StalkerLunge gap closers
    ///   * Fair defense: 0% parry, 35% block mitigation (punches through), responsive stagger on 15-25 poise
    ///   * Multi-enemy tactical surround via EnemyGroupCombatCoordinator
    /// - Smooth health bar drain and death cleanup
    /// - Triggers metallic combat hit sounds and death sting via Mission1_AudioManager
    /// </summary>
    [RequireComponent(typeof(InsaneGunSwordController))]
    public class Mission1_EnemyTracker : MonoBehaviour
    {
        [Header("Display Identity")]
        public string displayName = "RAZOR PUNK 1";
        public float headOffset = 2.30f;

        [Header("Aggro & Stagger Settings")]
        public float aggroDistance = 12.0f;
        public float activationDelay = 0.0f; // staggered activation delay
        public bool isAggroed = false;
        public bool isCombatActivated = false;

        [Header("References")]
        public InsaneGunSwordController combatController;
        public Transform playerTransform;
        public Transform rivalTarget;

        // Floating UI Elements
        private GameObject _uiCanvasObj;
        private Canvas _worldCanvas;
        private Image _healthFillImage;
        private Image _ghostFillImage;
        private TextMeshProUGUI _nameText;
        private float _ghostRatio = 1.0f;
        private Camera _mainCamera;

        // Menacing Patrol State (Idle before ACCEPT)
        private Vector3 _initialSpawnPos;
        private Quaternion _initialSpawnRot;
        private float _patrolTimer = 1.0f;

        // Animation & Locomotion Sync
        private Animator _animator;
        private CharacterController _characterController;
        private static readonly int HashMoveSpeed = Animator.StringToHash("MoveSpeed");
        private Vector3 _lastFramePosition;
        private float _smoothedMoveSpeed = 0f;

        // Coroutine & Aggression tracking
        private Coroutine _delayedActivationCoroutine;
        private Coroutine _walkToStandoffCoroutine;
        private float _lastSoloAttackTime = 0f;

        private void Awake()
        {
            combatController = GetComponent<InsaneGunSwordController>();
            _characterController = GetComponent<CharacterController>();
            _animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
            _initialSpawnPos = transform.position;
            _initialSpawnRot = transform.rotation;
            _lastFramePosition = transform.position;
            _patrolTimer = 1.0f;
            ApplyTutorialStats();
        }

        private void Start()
        {
            _mainCamera = Camera.main;
            BuildFloatingHealthBar();

            if (combatController != null)
            {
                combatController.OnDamaged += OnTookDamage;
                combatController.OnDeath += OnDied;
            }

            // Start idle in TrainingDummy mode until mission acceptance.
            // Pre-combat bots are INVINCIBLE: player damage is impossible (see
            // InsaneGunSwordController trial-protection hook). Cleared on activation.
            if (!isCombatActivated && combatController != null)
            {
                combatController.controlMode = InsaneGunSwordController.ControlMode.TrainingDummy;
                combatController.inputLocked = true;
                combatController.isInvincible = true;
            }
        }

        private void OnDestroy()
        {
            if (combatController != null)
            {
                EnemyGroupCombatCoordinator.Instance?.UnregisterCombatant(combatController);
                combatController.OnDamaged -= OnTookDamage;
                combatController.OnDeath -= OnDied;
            }

            if (_uiCanvasObj != null)
            {
                Destroy(_uiCanvasObj);
            }
        }

        public void ApplyTutorialStats()
        {
            if (combatController == null) return;

            // Blackwall Combat AI characteristics: Aggressive, agile, combo-driven, fair defense & poise
            float hp = 105f;
            float lightDmg = 8.0f;
            float heavyDmg = 13.0f;
            float run = 5.8f;
            float walk = 3.2f;
            float attackInterval = 0.40f;
            float poise = 20f;
            float dodgeChance = 0.10f;
            float blockChance = 0.05f;

            string upper = (displayName ?? "").ToUpperInvariant();
            if (upper.Contains("RONIN") || upper.Contains("NINJA"))
            {
                // Shadow Ronin: Fast, agile, elusive (95 HP, 8 dmg, 0.35s interval, 6.2 run speed)
                hp = 95f;
                lightDmg = 8.0f;
                heavyDmg = 13.0f;
                run = 6.2f;
                walk = 3.4f;
                attackInterval = 0.35f;
                poise = 15f;
                dodgeChance = 0.15f;
                blockChance = 0.04f;
            }
            else if (upper.Contains("ENFORCER"))
            {
                // Heavy Enforcer: Sturdy, crushing strikes (120 HP, 10 dmg, 0.45s interval, 5.5 run speed)
                hp = 120f;
                lightDmg = 10.0f;
                heavyDmg = 15.0f;
                run = 5.5f;
                walk = 3.0f;
                attackInterval = 0.45f;
                poise = 25f;
                dodgeChance = 0.06f;
                blockChance = 0.08f;
            }
            else if (upper.Contains("COMBAT BOT") || upper.Contains("MECH") || upper.Contains("PUNK"))
            {
                // Corrupted Combat Bot / Razor Punk: Pure Blackwall Rogue AI baseline
                hp = 105f;
                lightDmg = 8.5f;
                heavyDmg = 14.0f;
                run = 5.8f;
                walk = 3.2f;
                attackInterval = 0.40f;
                poise = 20f;
                dodgeChance = 0.10f;
                blockChance = 0.05f;
            }

            combatController.maxHealth = hp;
            combatController.currentHealth = hp;
            combatController.attackDamage = lightDmg;
            combatController.heavyAttackDamage = heavyDmg;
            combatController.gunDamagePerShot = 4.0f;
            combatController.runSpeed = run;
            combatController.walkSpeed = walk;
            combatController.minAttackInterval = attackInterval;
            combatController.comboAcceptWindowStart = 0.65f;
            combatController.autoReviveOnDeath = false;
            combatController.enemyLayers = (1 << 8) | (1 << 0);
            combatController.groundLayers = ~0;

            // Defense & Poise stats matching Blackwall AI (visceral stagger, 0% parry, slight block)
            combatController.aiParryChance = 0.0f;
            combatController.aiBlockChance = blockChance;
            combatController.aiBlockMitigation = 0.35f; // 65% damage taken even when blocking
            combatController.aiDodgeReactionChance = dodgeChance;
            combatController.maxPoise = poise;
            combatController.currentPoise = poise;
            combatController.hasHyperArmor = false;
        }

        public void SetIdentity(string name, Transform player)
        {
            displayName = name;
            playerTransform = player;
            if (_nameText != null)
            {
                _nameText.text = displayName;
            }
        }

        /// <summary>
        /// Starts the cinematic standoff advance on foot towards targetPos.
        /// Draws weapon and physically walks with CharacterController and MoveSpeed = 1.0f animation.
        /// </summary>
        public Coroutine StartWalkToStandoff(Vector3 targetPos, Quaternion finalRot, float walkSpeed = 2.3f)
        {
            if (_walkToStandoffCoroutine != null) StopCoroutine(_walkToStandoffCoroutine);
            _walkToStandoffCoroutine = StartCoroutine(WalkToStandoffRoutine(targetPos, finalRot, walkSpeed));
            return _walkToStandoffCoroutine;
        }

        public IEnumerator WalkToStandoffRoutine(Vector3 targetPos, Quaternion finalRot, float walkSpeed = 2.3f)
        {
            if (combatController != null)
            {
                combatController.inputLocked = true;
                combatController.isInvincible = true;
                combatController.DrawSword();
            }
            if (_animator == null) _animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
            var cc = _characterController != null ? _characterController : GetComponent<CharacterController>();

            float timeout = 4.5f;
            float elapsed = 0f;

            while (elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                Vector3 toTarget = targetPos - transform.position;
                toTarget.y = 0f;
                float dist = toTarget.magnitude;

                if (dist <= 0.35f) break;

                Vector3 moveDir = toTarget.normalized;
                if (cc != null && cc.enabled)
                {
                    Vector3 delta = moveDir * (walkSpeed * Time.deltaTime);
                    delta.y = -9.81f * Time.deltaTime;
                    cc.Move(delta);
                }
                else
                {
                    transform.position += moveDir * (walkSpeed * Time.deltaTime);
                }

                if (moveDir.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.RotateTowards(
                        transform.rotation,
                        Quaternion.LookRotation(moveDir),
                        280f * Time.deltaTime
                    );
                }

                if (_animator != null)
                {
                    _animator.SetFloat(HashMoveSpeed, 1.0f);
                }

                yield return null;
            }

            // Arrived at standoff spot: smoothly face final rotation (Ido/Alita) and stop walking
            float alignTimer = 0f;
            while (alignTimer < 0.45f)
            {
                alignTimer += Time.deltaTime;
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    finalRot,
                    360f * Time.deltaTime
                );
                if (_animator != null)
                {
                    _animator.SetFloat(HashMoveSpeed, 0f);
                }
                yield return null;
            }

            if (_animator != null)
            {
                _animator.SetFloat(HashMoveSpeed, 0f);
            }
            _walkToStandoffCoroutine = null;
        }

        public void ActivateCombatAI()
        {
            isCombatActivated = true;
            if (activationDelay > 0.05f)
            {
                if (_delayedActivationCoroutine != null) StopCoroutine(_delayedActivationCoroutine);
                _delayedActivationCoroutine = StartCoroutine(DelayedActivationRoutine(activationDelay));
            }
            else
            {
                ApplyCombatActivation();
            }
        }

        private IEnumerator DelayedActivationRoutine(float delay)
        {
            if (combatController != null)
            {
                combatController.inputLocked = true;
                combatController.controlMode = InsaneGunSwordController.ControlMode.TrainingDummy;
            }
            yield return new WaitForSeconds(delay);
            _delayedActivationCoroutine = null;
            ApplyCombatActivation();
        }

        private void ApplyCombatActivation()
        {
            if (_delayedActivationCoroutine != null)
            {
                StopCoroutine(_delayedActivationCoroutine);
                _delayedActivationCoroutine = null;
            }
            if (_walkToStandoffCoroutine != null)
            {
                StopCoroutine(_walkToStandoffCoroutine);
                _walkToStandoffCoroutine = null;
            }

            if (combatController != null && !combatController.IsDead)
            {
                combatController.isInvincible = false;
                combatController.inputLocked = false;
                combatController.isSwordDrawn = true;
                if (playerTransform == null)
                {
                    var p = GameObject.FindWithTag("Player");
                    if (p != null) playerTransform = p.transform;
                }
                combatController.targetEnemy = playerTransform;
                combatController.controlMode = InsaneGunSwordController.ControlMode.AICirclingEnemy;
                ApplyTutorialStats();

                // Register with EnemyGroupCombatCoordinator so enemies surround Alita and coordinate attacks
                EnemyGroupCombatCoordinator.Instance?.RegisterCombatant(combatController, playerTransform);
            }
        }

        private void Update()
        {
            if (combatController == null || combatController.IsDead)
            {
                if (_uiCanvasObj != null && _uiCanvasObj.activeSelf)
                {
                    _uiCanvasObj.SetActive(false);
                }
                return;
            }

            // Ensure player reference
            if (playerTransform == null)
            {
                var p = GameObject.FindWithTag("Player");
                if (p != null) playerTransform = p.transform;
            }

            // Menacing Idle Patrol or Rival Sparring behavior before mission is accepted
            if (!isCombatActivated)
            {
                if (rivalTarget != null)
                {
                    UpdateRivalSparring();
                }
                else
                {
                    UpdateIdleMenacingPatrol();
                }
            }
            else
            {
                // Aggro check when combat is active
                if (!isAggroed && playerTransform != null)
                {
                    float dist = Vector3.Distance(transform.position, playerTransform.position);
                    if (dist <= aggroDistance)
                    {
                        TriggerAggro();
                    }
                }

                // If Mission1_StreetTrialManager is not present or driving (e.g. standalone test scene),
                // drive solo AI aggression matching Blackwall Rogue AI behavior
                if (Mission1_StreetTrialManager.Instance == null)
                {
                    DriveSoloAggression();
                }
            }

            UpdateFloatingHUD();
        }

        private void LateUpdate()
        {
            if (_animator == null)
            {
                _animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
                if (_animator == null) return;
            }

            if (combatController != null && combatController.IsDead)
            {
                return;
            }

            if (isCombatActivated)
            {
                // In active combat, InsaneGunSwordController natively drives MoveSpeed
                // and animator blend trees for running, strafing, lunging and combos!
                return;
            }

            // Measure horizontal movement over this frame (for idle patrol & sparring)
            Vector3 currentPos = transform.position;
            Vector3 disp = currentPos - _lastFramePosition;
            disp.y = 0f;
            float currentHorizontalSpeed = disp.magnitude / Mathf.Max(0.0001f, Time.deltaTime);
            _lastFramePosition = currentPos;

            // Check if currently performing an attack
            bool isAttacking = combatController != null && combatController.IsAttacking;
            AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
            if (isAttacking || state.IsTag("Attack") || state.IsName("Attack"))
            {
                _smoothedMoveSpeed = Mathf.MoveTowards(_smoothedMoveSpeed, 0f, Time.deltaTime * 10f);
                return;
            }

            // Target animation MoveSpeed: 0 = Idle, 1 = Walk, 2 = Run
            float targetMoveSpeed = 0f;
            if (currentHorizontalSpeed > 3.6f)
            {
                targetMoveSpeed = 2.0f; // Running
            }
            else if (currentHorizontalSpeed > 0.10f)
            {
                targetMoveSpeed = 1.0f; // Walking
            }
            else
            {
                targetMoveSpeed = 0f; // Idle
            }

            _smoothedMoveSpeed = Mathf.MoveTowards(_smoothedMoveSpeed, targetMoveSpeed, Time.deltaTime * 6f);
            _animator.SetFloat(HashMoveSpeed, _smoothedMoveSpeed);
        }

        /// <summary>
        /// Drives solo aggression when not managed by Mission1_StreetTrialManager,
        /// matching the Blackwall Rogue AI combat behavior.
        /// </summary>
        private void DriveSoloAggression()
        {
            if (playerTransform == null || combatController == null || combatController.IsDead || combatController.inputLocked) return;

            var coordinator = EnemyGroupCombatCoordinator.Instance;
            if (coordinator != null && coordinator.IsGroupSurroundActive(playerTransform))
            {
                return;
            }

            if (Time.time - _lastSoloAttackTime < 0.65f) return;

            float dist = Vector3.Distance(transform.position, playerTransform.position);
            float strikeRange = transform.localScale.x > 1.25f ? 4.2f : 3.2f;

            if (dist <= strikeRange && !combatController.IsAttacking && !combatController.IsComboRunning)
            {
                float r = Random.value;
                if (r < 0.40f)
                {
                    combatController.StartLightAttack(Vector3.zero);
                }
                else if (r < 0.70f)
                {
                    combatController.StartAIGunSwordCombo(InsaneGunSwordController.AIGunSwordComboType.ExecutionerShred);
                }
                else
                {
                    combatController.StartAIGunSwordCombo(InsaneGunSwordController.AIGunSwordComboType.BlitzkriegOverdrive);
                }
                _lastSoloAttackTime = Time.time;
            }
            // Mid-range gap closing is handled purely on foot by AICirclingEnemy (running/walking on foot, no teleport lunges)
        }

        /// <summary>
        /// Rival sparring behavior: enemies circle and clash weapons dynamically.
        /// </summary>
        private void UpdateRivalSparring()
        {
            if (rivalTarget == null || combatController == null || combatController.IsDead) return;

            Vector3 diff = rivalTarget.position - transform.position;
            diff.y = 0f;
            float dist = diff.magnitude;

            if (diff.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(diff.normalized), Time.deltaTime * 4f);
            }

            var cc = _characterController != null ? _characterController : GetComponent<CharacterController>();
            if (cc != null && cc.enabled)
            {
                Vector3 tangent = Vector3.Cross(Vector3.up, diff.normalized);
                Vector3 move = tangent * Mathf.Sin(Time.time * 1.5f + Mathf.Abs((float)(GetHashCode() % 5))) * 1.2f;
                if (dist > 3.4f) move += diff.normalized * 1.4f;
                else if (dist < 2.0f) move -= diff.normalized * 1.4f;
                move.y = -9.81f;
                cc.Move(move * Time.deltaTime);
            }
        }

        /// <summary>
        /// Menacing idle pacing (2m forward and back along initial facing angle).
        /// </summary>
        private void UpdateIdleMenacingPatrol()
        {
            _patrolTimer += Time.deltaTime * 0.75f;
            float offset = Mathf.PingPong(_patrolTimer, 2.0f) - 1.0f; // -1m to +1m (2m total span)
            Vector3 targetPos = _initialSpawnPos + (_initialSpawnRot * Vector3.forward) * offset;

            var cc = _characterController != null ? _characterController : GetComponent<CharacterController>();
            if (cc != null && cc.enabled)
            {
                Vector3 toTarget = targetPos - transform.position;
                toTarget.y = 0f;
                Vector3 moveDelta = toTarget.normalized * (1.1f * Time.deltaTime);
                if (toTarget.magnitude < moveDelta.magnitude) moveDelta = toTarget;
                moveDelta.y = -9.81f * Time.deltaTime; // gravity
                cc.Move(moveDelta);
            }
            else
            {
                transform.position = targetPos;
            }

            // Turn in direction of patrol
            float dirSign = Mathf.Sign(Mathf.Cos(_patrolTimer * Mathf.PI));
            Quaternion targetRot = _initialSpawnRot * (dirSign > 0 ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f));
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 3.5f);
        }

        private void TriggerAggro()
        {
            if (!isCombatActivated) return;
            isAggroed = true;
            rivalTarget = null;
            if (combatController != null && !combatController.IsDead)
            {
                combatController.controlMode = InsaneGunSwordController.ControlMode.AICirclingEnemy;
                combatController.targetEnemy = playerTransform;
                combatController.inputLocked = false;
                combatController.isSwordDrawn = true;
                EnemyGroupCombatCoordinator.Instance?.RegisterCombatant(combatController, playerTransform);
            }
        }

        private void OnTookDamage(InsaneGunSwordController self, float damage)
        {
            // Pre-combat: ALL damage ignored — no HP loss, no aggro, no death.
            // (TakeDamage already early-outs via isInvincible; this restores any
            // chip damage, e.g. rival infighting, and stays silent.)
            if (!isCombatActivated)
            {
                if (combatController != null)
                    combatController.currentHealth = combatController.maxHealth;
                return;
            }

            // If struck while in staggered delay, wake up immediately into combat!
            if (combatController != null && combatController.inputLocked)
            {
                ApplyCombatActivation();
            }

            // Play metallic impact sound and grunt through Mission1_AudioManager
            Mission1_AudioManager.Instance?.PlayHit(false);

            if (!isAggroed)
            {
                TriggerAggro();
            }
        }

        private void OnDied(InsaneGunSwordController self)
        {
            EnemyGroupCombatCoordinator.Instance?.UnregisterCombatant(self);

            // Play death sting + kill chime through Mission1_AudioManager
            Mission1_AudioManager.Instance?.PlayKill();

            if (_uiCanvasObj != null)
            {
                _uiCanvasObj.SetActive(false);
            }
        }

        public void ResetToInitial(Vector3 pos, Quaternion rot)
        {
            if (_walkToStandoffCoroutine != null)
            {
                StopCoroutine(_walkToStandoffCoroutine);
                _walkToStandoffCoroutine = null;
            }
            _initialSpawnPos = pos;
            _initialSpawnRot = rot;
            isAggroed = false;
            isCombatActivated = false;
            _patrolTimer = 1.0f;
            _smoothedMoveSpeed = 0f;
            _lastFramePosition = pos;

            if (combatController != null)
            {
                EnemyGroupCombatCoordinator.Instance?.UnregisterCombatant(combatController);
                if (combatController.IsDead)
                {
                    combatController.Revive(pos);
                }
                var ragdoll = GetComponent<EuphoriaRagdollController>();
                if (ragdoll != null)
                {
                    ragdoll.autoRecover = false;
                    ragdoll.ResetRagdollImmediate(pos);
                }

                var cc = GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                transform.position = pos;
                transform.rotation = rot;
                Physics.SyncTransforms();
                if (cc != null) cc.enabled = true;

                combatController.controlMode = InsaneGunSwordController.ControlMode.TrainingDummy;
                combatController.inputLocked = true;
                combatController.isInvincible = true;
                ApplyTutorialStats();
            }
            else
            {
                var cc = GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                transform.position = pos;
                transform.rotation = rot;
                Physics.SyncTransforms();
                if (cc != null) cc.enabled = true;
            }

            if (_uiCanvasObj != null)
            {
                _uiCanvasObj.SetActive(true);
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // FLOATING WORLD SPACE HEALTH BAR & BILLBOARDING (RED CYBERPUNK THEME)
        // ─────────────────────────────────────────────────────────────────────────
        private void BuildFloatingHealthBar()
        {
            _uiCanvasObj = new GameObject($"{gameObject.name}_FloatingHUD", typeof(RectTransform));
            _uiCanvasObj.transform.SetParent(transform, false);
            _uiCanvasObj.transform.localPosition = new Vector3(0f, headOffset, 0f);

            _worldCanvas = _uiCanvasObj.AddComponent<Canvas>();
            _worldCanvas.renderMode = RenderMode.WorldSpace;
            _worldCanvas.sortingOrder = 20;

            RectTransform canvasRt = _uiCanvasObj.GetComponent<RectTransform>();
            canvasRt.sizeDelta = new Vector2(200f, 60f);
            canvasRt.localScale = Vector3.one * 0.0075f; // Scale down for world space

            // Red Diamond Marker (rot 45 deg Image)
            GameObject diamondObj = new GameObject("Marker_Diamond", typeof(RectTransform));
            diamondObj.transform.SetParent(_uiCanvasObj.transform, false);
            RectTransform diamondRt = diamondObj.GetComponent<RectTransform>();
            diamondRt.anchoredPosition = new Vector2(0f, 22f);
            diamondRt.sizeDelta = new Vector2(10f, 10f);
            diamondRt.localRotation = Quaternion.Euler(0f, 0f, 45f);
            UnityEngine.UI.Image diamondImg = diamondObj.AddComponent<UnityEngine.UI.Image>();
            diamondImg.color = new Color(1.0f, 0.0f, 0.235f, 1.0f); // Neon Crimson
            diamondImg.raycastTarget = false;

            // Nameplate ("⛧ RAZOR PUNK X")
            GameObject nameObj = new GameObject("Text_Nameplate", typeof(RectTransform));
            nameObj.transform.SetParent(_uiCanvasObj.transform, false);
            RectTransform nameRt = nameObj.GetComponent<RectTransform>();
            nameRt.anchoredPosition = new Vector2(0f, 4f);
            nameRt.sizeDelta = new Vector2(220f, 20f);
            _nameText = nameObj.AddComponent<TextMeshProUGUI>();
            _nameText.text = displayName;
            _nameText.fontSize = 13f;
            _nameText.fontStyle = FontStyles.Bold;
            _nameText.alignment = TextAlignmentOptions.Center;
            _nameText.color = new Color(1.0f, 0.15f, 0.25f, 1.0f); // Intense red
            _nameText.raycastTarget = false;

            // Health Bar Background
            GameObject bgObj = new GameObject("Bar_Background", typeof(RectTransform));
            bgObj.transform.SetParent(_uiCanvasObj.transform, false);
            RectTransform bgRt = bgObj.GetComponent<RectTransform>();
            bgRt.anchoredPosition = new Vector2(0f, -12f);
            bgRt.sizeDelta = new Vector2(140f, 9f);
            Image bgImg = bgObj.AddComponent<Image>();
            bgImg.color = new Color(0.12f, 0.02f, 0.04f, 0.95f);
            bgImg.raycastTarget = false;

            // Health Bar Ghost Trail Fill (Orange-red)
            GameObject ghostObj = new GameObject("Bar_Ghost", typeof(RectTransform));
            ghostObj.transform.SetParent(bgObj.transform, false);
            RectTransform ghostRt = ghostObj.GetComponent<RectTransform>();
            ghostRt.anchorMin = new Vector2(0f, 0f);
            ghostRt.anchorMax = new Vector2(1f, 1f);
            ghostRt.offsetMin = Vector2.zero;
            ghostRt.offsetMax = Vector2.zero;
            _ghostFillImage = ghostObj.AddComponent<Image>();
            _ghostFillImage.color = new Color(1.0f, 0.45f, 0.25f, 0.75f);
            _ghostFillImage.type = Image.Type.Filled;
            _ghostFillImage.fillMethod = Image.FillMethod.Horizontal;
            _ghostFillImage.fillOrigin = 0;
            _ghostFillImage.raycastTarget = false;

            // Health Bar Active Fill (Neon Crimson #FF003C)
            GameObject fillObj = new GameObject("Bar_ActiveFill", typeof(RectTransform));
            fillObj.transform.SetParent(bgObj.transform, false);
            RectTransform fillRt = fillObj.GetComponent<RectTransform>();
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(1f, 1f);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            _healthFillImage = fillObj.AddComponent<Image>();
            _healthFillImage.color = new Color(1.0f, 0.00f, 0.235f, 1.0f);
            _healthFillImage.type = Image.Type.Filled;
            _healthFillImage.fillMethod = Image.FillMethod.Horizontal;
            _healthFillImage.fillOrigin = 0;
            _healthFillImage.raycastTarget = false;
        }

        private void UpdateFloatingHUD()
        {
            if (_uiCanvasObj == null) return;
            if (_mainCamera == null) _mainCamera = Camera.main;
            if (_mainCamera == null) return;

            // Position above head
            _uiCanvasObj.transform.position = transform.position + Vector3.up * headOffset;

            // Look directly at camera (billboard)
            Vector3 camDir = _uiCanvasObj.transform.position - _mainCamera.transform.position;
            if (camDir.sqrMagnitude > 0.001f)
            {
                _uiCanvasObj.transform.rotation = Quaternion.LookRotation(camDir);
            }

            // Update health fill ratio
            if (combatController != null && _healthFillImage != null)
            {
                float targetRatio = Mathf.Clamp01(combatController.currentHealth / combatController.maxHealth);
                _healthFillImage.fillAmount = targetRatio;

                if (_ghostFillImage != null)
                {
                    _ghostRatio = Mathf.MoveTowards(_ghostRatio, targetRatio, Time.deltaTime * 0.6f);
                    _ghostFillImage.fillAmount = _ghostRatio;
                }
            }
        }
    }
}
