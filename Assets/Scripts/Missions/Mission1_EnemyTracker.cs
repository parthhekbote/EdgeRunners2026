using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using InsaneGun.Combat;

namespace Cyberpunk.Missions
{
    /// <summary>
    /// Tracks individual Mission 1 enemy state ("RAZOR PUNKS"):
    /// - Billboarded floating health bar + red nameplate ("⛧ RAZOR PUNK X") + red diamond marker
    /// - Menacing idle patrol (paces 2m) until mission accepted
    /// - Proximity (12m) and on-damage aggro trigger
    /// - Staggered activation delays (Thug 1 instant, Thug 2 0.8s, Thug 3 1.6s)
    /// - Enforces exact 5-hit stats: 175 HP (35x5 dmg), 11 dmg, no blitz/combos
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

        // Reflection handles for AI combat suppression
        private static readonly FieldInfo _aiComboTargetField = 
            typeof(InsaneGunSwordController).GetField("_aiComboTarget", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo _aiPhaseField = 
            typeof(InsaneGunSwordController).GetField("_aiPhase", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo _aiThreatField = 
            typeof(InsaneGunSwordController).GetField("_aiThreatMeter", BindingFlags.NonPublic | BindingFlags.Instance);

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
            // Scaled-down Mission 1 Combat Stats: Accessible tutorial pacing (~3-4 hits to kill, light damage, slow intervals)
            float hp = 115f;
            float lightDmg = 7.0f;
            float heavyDmg = 10.0f;
            float run = 3.8f;
            float walk = 2.0f;
            float attackInterval = 2.50f;

            string upper = (displayName ?? "").ToUpperInvariant();
            if (upper.Contains("RONIN") || upper.Contains("NINJA"))
            {
                // Shadow Ronin: Agile but fragile (105 HP, light strikes 6 dmg, 2.20s interval)
                hp = 105f;
                lightDmg = 6.0f;
                heavyDmg = 9.0f;
                run = 4.0f;
                walk = 2.2f;
                attackInterval = 2.20f;
            }
            else if (upper.Contains("ENFORCER"))
            {
                // Heavy Enforcer: Sturdy but slow windups (130 HP, 8 dmg, 2.80s interval)
                hp = 130f;
                lightDmg = 8.0f;
                heavyDmg = 12.0f;
                run = 3.5f;
                walk = 1.8f;
                attackInterval = 2.80f;
            }
            else if (upper.Contains("COMBAT BOT") || upper.Contains("MECH"))
            {
                // Corrupted Combat Bot: Steady robotic advance (115 HP, 7 dmg, 2.50s interval)
                hp = 115f;
                lightDmg = 7.0f;
                heavyDmg = 10.0f;
                run = 3.8f;
                walk = 2.0f;
                attackInterval = 2.50f;
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
            combatController.enemyLayers = (1 << 8) | (1 << 3) | (1 << 0);
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

        public void ActivateCombatAI()
        {
            isCombatActivated = true;
            if (activationDelay > 0.05f)
            {
                StartCoroutine(DelayedActivationRoutine(activationDelay));
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
            ApplyCombatActivation();
        }

        private void ApplyCombatActivation()
        {
            if (combatController != null && !combatController.IsDead)
            {
                combatController.isInvincible = false;
                combatController.inputLocked = false;
                combatController.targetEnemy = playerTransform;
                combatController.controlMode = InsaneGunSwordController.ControlMode.AICirclingEnemy;
                ApplyTutorialStats();
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
                // Controlled combat pacing: limit to single strikes for scaled-down difficulty
                if (_aiComboTargetField != null)
                {
                    _aiComboTargetField.SetValue(combatController, 1);
                }
                _aiThreatField?.SetValue(combatController, 0f);
                if (_aiPhaseField != null)
                {
                    int phase = (int)_aiPhaseField.GetValue(combatController);
                    if (phase == 3) // Blitz phase
                    {
                        _aiPhaseField.SetValue(combatController, 1); // Reset to Circle phase
                    }
                }

                // Aggro check when combat is active
                if (!isAggroed && playerTransform != null)
                {
                    float dist = Vector3.Distance(transform.position, playerTransform.position);
                    if (dist <= aggroDistance)
                    {
                        TriggerAggro();
                    }
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

            // Measure horizontal movement over this frame
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
            if (currentHorizontalSpeed > 3.2f)
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

            // Play metallic impact sound and grunt through Mission1_AudioManager
            Mission1_AudioManager.Instance?.PlayHit(false);

            if (!isAggroed && isCombatActivated)
            {
                TriggerAggro();
            }
        }

        private void OnDied(InsaneGunSwordController self)
        {
            // Play death sting + kill chime through Mission1_AudioManager
            Mission1_AudioManager.Instance?.PlayKill();

            if (_uiCanvasObj != null)
            {
                _uiCanvasObj.SetActive(false);
            }
        }

        public void ResetToInitial(Vector3 pos, Quaternion rot)
        {
            _initialSpawnPos = pos;
            _initialSpawnRot = rot;
            isAggroed = false;
            isCombatActivated = false;
            _patrolTimer = 1.0f;
            _smoothedMoveSpeed = 0f;
            _lastFramePosition = pos;

            if (combatController != null)
            {
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
