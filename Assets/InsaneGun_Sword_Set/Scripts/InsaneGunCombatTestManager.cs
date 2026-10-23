using UnityEngine;
using InsaneGun.Combat;

namespace InsaneGun.Testing
{
    /// <summary>
    /// Test Sandbox Manager for InsaneGun Sword & Gun Combat System.
    /// Provides real-time animation state monitoring, combo chain visualizer,
    /// single-click heavy attack monitoring, Euphoria ragdoll inspection,
    /// enemy mode toggling (Dummy vs AI Duelist), and instant respawn tools.
    /// </summary>
    public class InsaneGunCombatTestManager : MonoBehaviour
    {
        public static InsaneGunCombatTestManager Instance { get; private set; }

        [Header("Combatants")]
        public InsaneGunSwordController player;
        public InsaneGunSwordController enemy;
        public Vector3 enemySpawnPos = new Vector3(0f, 0f, 2.5f);
        public Quaternion enemySpawnRot = Quaternion.Euler(0f, 180f, 0f);
        public Vector3 playerSpawnPos = Vector3.zero;
        public Quaternion playerSpawnRot = Quaternion.identity;

        [Header("State Tracking")]
        public bool showHelpOverlay = true;

        private Animator _enemyAnim;
        private string _lastEnemyStateName = "Idle / Moving";

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            if (player == null || !player.gameObject.activeInHierarchy)
            {
                var pGos = GameObject.FindGameObjectsWithTag("Player");
                foreach (var go in pGos)
                {
                    if (go.activeInHierarchy)
                    {
                        var ctrl = go.GetComponent<InsaneGunSwordController>();
                        if (ctrl != null) { player = ctrl; break; }
                    }
                }
            }

            if (player != null)
            {
                playerSpawnPos = player.transform.position;
                playerSpawnRot = player.transform.rotation;
            }

            if (enemy == null || !enemy.gameObject.activeInHierarchy)
            {
                var eGos = GameObject.FindGameObjectsWithTag("Enemy");
                foreach (var go in eGos)
                {
                    if (go.activeInHierarchy)
                    {
                        var ctrl = go.GetComponent<InsaneGunSwordController>();
                        if (ctrl != null) { enemy = ctrl; break; }
                    }
                }
            }

            if (enemy != null && enemy.gameObject.activeInHierarchy)
            {
                _enemyAnim = enemy.GetComponentInChildren<Animator>();
                enemy.controlMode = InsaneGunSwordController.ControlMode.AICirclingEnemy;
                enemy.autoReviveOnDeath = true;
                enemy.autoReviveDelay = 2.5f;
            }

            // Ensure Blackwall Holographic Enemy Spawner is active
            if (BlackwallHolographicSpawner.Instance == null)
            {
                var spawnerGo = new GameObject("Blackwall_Holographic_Enemy_Spawner");
                spawnerGo.AddComponent<BlackwallHolographicSpawner>();
            }
        }

        private void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var gp = UnityEngine.InputSystem.Gamepad.current;

            bool rPressed = (kb != null && kb.rKey.wasPressedThisFrame);
            if (gp != null && (gp.buttonWest.wasPressedThisFrame || gp.startButton.wasPressedThisFrame)) rPressed = true;
            try { if (Input.GetKeyDown(KeyCode.R)) rPressed = true; } catch { }

            bool key1Pressed = (kb != null && kb.digit1Key.wasPressedThisFrame);
            try { if (Input.GetKeyDown(KeyCode.Alpha1)) key1Pressed = true; } catch { }

            bool key2Pressed = (kb != null && kb.digit2Key.wasPressedThisFrame);
            try { if (Input.GetKeyDown(KeyCode.Alpha2)) key2Pressed = true; } catch { }

            bool key3Pressed = (kb != null && kb.digit3Key.wasPressedThisFrame);
            try { if (Input.GetKeyDown(KeyCode.Alpha3)) key3Pressed = true; } catch { }

            bool key4Pressed = (kb != null && kb.digit4Key.wasPressedThisFrame);
            try { if (Input.GetKeyDown(KeyCode.Alpha4)) key4Pressed = true; } catch { }

            bool cPressed = (kb != null && kb.cKey.wasPressedThisFrame);
            try { if (Input.GetKeyDown(KeyCode.C)) cPressed = true; } catch { }

            bool hPressed = (kb != null && kb.hKey.wasPressedThisFrame);
            try { if (Input.GetKeyDown(KeyCode.H)) hPressed = true; } catch { }

            // Keyboard shortcuts for testing
            if (rPressed)
            {
                ResetCombatArena();
            }

            if (key1Pressed)
            {
                SetEnemyMode(InsaneGunSwordController.ControlMode.TrainingDummy);
            }

            if (key2Pressed)
            {
                SetEnemyMode(InsaneGunSwordController.ControlMode.AICirclingEnemy);
            }

            if (key3Pressed && player != null)
            {
                player.currentHealth = player.maxHealth;
                player.currentSlowMotionEnergy = player.maxSlowMotionEnergy;
            }

            // [4] Test Euphoria Ragdoll Launch on Enemy
            if (key4Pressed && enemy != null)
            {
                var ragdoll = enemy.GetComponent<EuphoriaRagdollController>();
                if (ragdoll != null)
                {
                    ragdoll.TriggerEuphoriaRagdoll(enemy.transform.position + Vector3.up * 1.1f, -enemy.transform.forward, 16f, isFatal: false);
                }
            }

            if (cPressed)
            {
                SwapControlledCharacter();
            }

            if (hPressed)
            {
                showHelpOverlay = !showHelpOverlay;
            }

            // Track enemy animation and ragdoll state
            var enemyRagdoll = enemy != null ? enemy.GetComponent<EuphoriaRagdollController>() : null;
            if (enemyRagdoll != null && enemyRagdoll.IsRagdoll)
            {
                _lastEnemyStateName = "<color=#ff00ff><b>EUPHORIA RAGDOLL (PHYSICS TUMBLE)</b></color>";
            }
            else if (_enemyAnim != null && _enemyAnim.isActiveAndEnabled && _enemyAnim.runtimeAnimatorController != null)
            {
                var info = _enemyAnim.GetCurrentAnimatorStateInfo(0);
                if (info.IsTag("Hit")) _lastEnemyStateName = "Hit Reaction / Knockback";
                else if (info.IsTag("Death")) _lastEnemyStateName = "Death (Euphoria / Ragdoll)";
                else if (info.IsTag("Attack")) _lastEnemyStateName = "Attacking (AI)";
                else if (info.IsTag("Dodge")) _lastEnemyStateName = "Dodging";
                else _lastEnemyStateName = "Idle / Locomotion (Synty)";
            }
        }

        public void SwapControlledCharacter()
        {
            if (player == null || enemy == null) return;

            var temp = player;
            player = enemy;
            enemy = temp;

            player.controlMode = InsaneGunSwordController.ControlMode.PlayerControlled;
            player.gameObject.layer = 8;
            player.tag = "Player";
            player.enemyLayers = 1 << 9;

            enemy.controlMode = InsaneGunSwordController.ControlMode.TrainingDummy;
            enemy.gameObject.layer = 9;
            enemy.tag = "Enemy";
            enemy.enemyLayers = 1 << 8;
            enemy.targetEnemy = player.transform;

            _enemyAnim = enemy.GetComponent<Animator>();

            var cam = Object.FindAnyObjectByType<SmoothOrbitCamera>();
            if (cam != null)
            {
                cam.target = player.transform;
            }
        }

        public void ResetEnemy()
        {
            if (enemy != null)
            {
                enemy.Revive(enemySpawnPos);
                enemy.transform.rotation = enemySpawnRot;
            }
        }

        public void ResetCombatArena()
        {
            ResetEnemy();
            if (player != null)
            {
                Vector3 targetPos = playerSpawnPos != Vector3.zero ? playerSpawnPos : player.transform.position;
                player.Revive(targetPos);
                player.transform.rotation = playerSpawnRot != Quaternion.identity ? playerSpawnRot : player.transform.rotation;
                player.currentHealth = player.maxHealth;
                player.currentSlowMotionEnergy = player.maxSlowMotionEnergy;
            }
            if (BlackwallHolographicSpawner.Instance != null)
            {
                BlackwallHolographicSpawner.Instance.ClearAllEnemies();
            }
            if (SyntyCombined.CyberpunkHUDController.Instance != null)
            {
                SyntyCombined.CyberpunkHUDController.Instance.ShowToast("ARENA REBOOT // HERO & ENEMY RESET", 2.5f);
            }
            Debug.Log("<color=#00FFFF>[CombatTestManager] Arena Reboot Executed: Player and Enemy fully revived and reset.</color>");
        }

        public void SetEnemyMode(InsaneGunSwordController.ControlMode mode)
        {
            if (enemy != null)
            {
                enemy.controlMode = mode;
                if (mode == InsaneGunSwordController.ControlMode.TrainingDummy)
                {
                    enemy.transform.position = enemySpawnPos;
                    enemy.transform.rotation = enemySpawnRot;
                }
            }
        }

        [Header("HUD Display")]
        public bool showHUD = false;

        private void OnGUI()
        {
            if (!showHUD) return;
            DrawComboChainHUD();
            DrawEnemyMonitorHUD();
            if (showHelpOverlay) DrawTestControlsHUD();
        }

        private void DrawComboChainHUD()
        {
            if (player == null) return;

            int lightCombo = player.CurrentComboCount;
            int heavyCombo = player.CurrentHeavyComboCount;
            bool winOpen = player.IsComboWindowOpen;
            float sw = Screen.width;

            float panelWidth = 600f;
            float panelHeight = 94f;
            float px = (sw - panelWidth) * 0.5f;
            float py = 20f;

            GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
            GUILayout.BeginArea(new Rect(px, py, panelWidth, panelHeight), boxStyle);

            GUILayout.BeginHorizontal();
            GUILayout.Label("<b><size=15>ALITA: BATTLE ANGEL — COMBAT ENGINE</size></b>", new GUIStyle(GUI.skin.label) { richText = true });
            GUILayout.FlexibleSpace();
            string bladeStatus = player.IsSwordDrawn ? "<color=#00ff88><b>[SWORD DRAWN]</b></color>" : "<color=#ffaa00><b>[SHEATHED]</b></color>";
            GUILayout.Label(bladeStatus, new GUIStyle(GUI.skin.label) { richText = true });
            GUILayout.Space(12);
            GUILayout.Label(player.IsSlowMotionActive ? "<color=#00e5ff><b>BULLET-TIME ACTIVE</b></color>" : "<color=#888888>NORMAL TIME</color>", new GUIStyle(GUI.skin.label) { richText = true });
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            if (heavyCombo > 0)
            {
                // Heavy Attack 3-Stage Progress Display
                GUILayout.BeginHorizontal();
                string[] heavyLabels = { "HEAVY 1\nSLASH (65)", "HEAVY 2\nCLEAVE (95)", "HEAVY 3\nFINISHER (140)" };
                for (int h = 1; h <= 3; h++)
                {
                    bool isCurrent = (heavyCombo == h);
                    bool isPast = (heavyCombo > h);

                    Color blockCol = isCurrent
                        ? (h == 3 ? new Color(1f, 0.1f, 0.7f) : new Color(1f, 0.7f, 0f))
                        : (isPast ? new Color(0.9f, 0.4f, 0.1f, 0.6f) : new Color(0.25f, 0.2f, 0.2f, 0.6f));

                    GUI.color = blockCol;
                    GUILayout.Box(heavyLabels[h - 1], GUILayout.Width(186), GUILayout.Height(36));
                    GUI.color = Color.white;
                }
                GUILayout.EndHorizontal();
            }
            else
            {
                // 7 Light Combo Hit Blocks
                GUILayout.BeginHorizontal();
                for (int i = 1; i <= 7; i++)
                {
                    bool isCurrent = (lightCombo == i);
                    bool isPast = (lightCombo > i);

                    Color blockCol;
                    if (isCurrent) blockCol = (i == 7) ? new Color(1f, 0.2f, 0.8f) : new Color(0f, 1f, 0.5f);
                    else if (isPast) blockCol = new Color(0.2f, 0.7f, 1f, 0.7f);
                    else blockCol = new Color(0.2f, 0.2f, 0.25f, 0.6f);

                    GUI.color = blockCol;
                    string label = (i == 7) ? "HIT 7\nFINISHER" : $"HIT {i}";
                    GUILayout.Box(label, GUILayout.Width(78), GUILayout.Height(36));
                    GUI.color = Color.white;
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.EndArea();

            // Active Combo Window Flash Banner
            if (winOpen)
            {
                float bannerY = py + panelHeight + 8f;
                Rect bannerRect = new Rect((sw - 480f) * 0.5f, bannerY, 480f, 28f);

                GUI.color = new Color(0f, 1f, 0.5f, 0.95f);
                if (heavyCombo > 0)
                {
                    int nextH = Mathf.Min(heavyCombo + 1, 3);
                    GUI.Box(bannerRect, $"★ HEAVY CHAIN WINDOW ACTIVE — PRESS [RMB] FOR HEAVY {nextH} ★");
                }
                else
                {
                    int nextL = (lightCombo % 7) + 1;
                    GUI.Box(bannerRect, $"★ CHAIN WINDOW ACTIVE — [LMB] FOR HIT {nextL}  |  [RMB] FOR HEAVY FINISHER ★");
                }
                GUI.color = Color.white;
            }
        }

        private void DrawEnemyMonitorHUD()
        {
            if (enemy == null) return;

            float panelWidth = 330f;
            float panelHeight = 160f;
            float px = Screen.width - panelWidth - 25f;
            float py = 25f;

            GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
            GUILayout.BeginArea(new Rect(px, py, panelWidth, panelHeight), boxStyle);

            string modeStr = enemy.controlMode == InsaneGunSwordController.ControlMode.TrainingDummy
                ? "<color=#00e5ff>[TRAINING DUMMY]</color>"
                : "<color=#ffaa00>[AI DUELIST (ATTACKING)]</color>";

            GUILayout.Label($"<b>TARGET ENEMY STATUS</b>  {modeStr}", new GUIStyle(GUI.skin.label) { richText = true, fontSize = 12 });
            GUILayout.Space(4);

            // Health bar
            float hpPct = enemy.HealthPercent;
            GUI.color = Color.Lerp(Color.red, Color.green, hpPct);
            GUILayout.HorizontalSlider(enemy.currentHealth, 0, enemy.maxHealth);
            GUI.color = Color.white;
            GUILayout.Label($"Health: {Mathf.CeilToInt(enemy.currentHealth)} / {enemy.maxHealth} ({(enemy.IsDead ? "<color=red>DEAD (RAGDOLL COLLAPSE)</color>" : "ALIVE")})", new GUIStyle(GUI.skin.label) { richText = true });

            GUILayout.Space(4);
            GUILayout.Label($"<b>State:</b> {_lastEnemyStateName}", new GUIStyle(GUI.skin.label) { richText = true });
            GUILayout.Label("Finisher / Heavy 3 / Lethal: Full Euphoria Ragdoll Launch\nAuto-Recovers after 2.2s get-up timer", new GUIStyle(GUI.skin.label) { fontSize = 10 });

            GUILayout.EndArea();
        }

        private void DrawTestControlsHUD()
        {
            float panelWidth = 330f;
            float panelHeight = 315f;
            float px = Screen.width - panelWidth - 25f;
            float py = Screen.height - panelHeight - 25f;

            GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
            GUILayout.BeginArea(new Rect(px, py, panelWidth, panelHeight), boxStyle);

            GUILayout.Label("<b><size=13>COMBAT CONTROLS & SANDBOX</size></b>", new GUIStyle(GUI.skin.label) { richText = true });
            GUILayout.Space(4);
            GUILayout.Label("  <b>[LMB]</b>      1-Click Light Attack (Chain 1–7)", new GUIStyle(GUI.skin.label) { richText = true });
            GUILayout.Label("  <b>[RMB]</b>      1-Click Heavy Strike (Chain 1–3 Finisher)", new GUIStyle(GUI.skin.label) { richText = true });
            GUILayout.Label("  <b>[Space]</b>    Athletic Combat Jump (In-Air Boost)", new GUIStyle(GUI.skin.label) { richText = true });
            GUILayout.Label("  <b>[X / Z]</b>    Draw / Sheathe Sword (Auto-Draws)", new GUIStyle(GUI.skin.label) { richText = true });
            GUILayout.Label("  <b>[LAlt]</b>     Directional Dodge (Attack Cancel)", new GUIStyle(GUI.skin.label) { richText = true });
            GUILayout.Label("  <b>[F]</b>        Block / Timed Perfect Parry", new GUIStyle(GUI.skin.label) { richText = true });
            GUILayout.Label("  <b>[Q]</b>        Gun-Kata 3-Shot Burst", new GUIStyle(GUI.skin.label) { richText = true });
            GUILayout.Label("  <b>[T]</b>        Toggle Slow Motion Bullet-Time", new GUIStyle(GUI.skin.label) { richText = true });
            GUILayout.Label("  <b>[C]</b>        Swap Character (Alita ⟷ Rival Boy)", new GUIStyle(GUI.skin.label) { richText = true });
            GUILayout.Label("  <b>[4]</b>        Trigger Test Euphoria Ragdoll Launch", new GUIStyle(GUI.skin.label) { richText = true });
            GUILayout.Label("  <b>[R]</b>        Reboot Combat Arena (Revive Hero & Enemy)", new GUIStyle(GUI.skin.label) { richText = true });
            GUILayout.Label("  <b>[1 / 2]</b>    Enemy: [1] Dummy | [2] AI Duelist", new GUIStyle(GUI.skin.label) { richText = true });
            GUILayout.Label("  <b>[3]</b>        Restore Player Health & Energy", new GUIStyle(GUI.skin.label) { richText = true });
            GUILayout.Label("  <b>[H]</b>        Toggle Controls Help Overlay", new GUIStyle(GUI.skin.label) { richText = true });

            GUILayout.EndArea();
        }
    }
}
