using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SyntyCombined;
using InsaneGun.Combat;
using CyberCombat.Coordination;

namespace InsaneGun.Combat
{
    /// <summary>
    /// Cyberpunk 2077 Blackwall Rogue AI Multi-Wave Incursion Controller.
    /// Attached to RogueAI_Sparks_FX in front of the 200m Blackwall LiDAR matrix.
    /// 
    /// Architecture:
    /// - Proximity Trigger: [E] prompt appears within 5.5m of RogueAI_Sparks_FX.
    /// - Music Transition: Switches to "Nine Inch Nails - Forked Reality.mp3" at full combat volume.
    /// - Wave System: Spawns 3 progressive combat waves emerging directly out of the Blackwall barrier:
    ///     * Wave 1: 2 Holographic Infiltrator Robots (SM_Chr_Robot_01 & SM_Chr_Robot_Female_01)
    ///     * Wave 2: 3 Holographic Assault Robots (Flanking & aggressive multi-angle strikes)
    ///     * Wave 3: 1 Master Rogue AI Overseer (Scale 1.15x, 450 HP, Hyper Armor) + 2 Elite Guards
    /// - Anti-Mercenary Sanitization: Strictly deactivates all cyborg/mercenary models and removes any EnemyDuelHUD.
    /// - Full Combat AI: Uses InsaneGunSwordController in AICirclingEnemy mode for responsive, aggressive 1v1 combat.
    /// - Holographic Optics: Full URP Cyberpunk/Holographic_RogueAI shader with crimson core and magenta rim fresnel.
    /// </summary>
    public class BlackwallRogueAIEncounter : MonoBehaviour
    {
        public static BlackwallRogueAIEncounter Instance { get; private set; }

        [Header("Interaction & Wave Settings")]
        [Tooltip("Interaction distance from sparks to trigger the encounter.")]
        public float interactionRadius = 5.5f;
        public KeyCode interactKey = KeyCode.E;
        public bool hasTriggered = false;
        public int currentWave = 0;
        public int totalWaves = 3;
        public bool isWaveSpawning = false;
        public bool isEncounterComplete = false;
        public bool hasCommuned = false;
        private bool _isCommunePromptActive = false;
        private float _breachHoldTimer = 0f;
        private float _communeHoldTimer = 0f;
        private const float INTERACT_HOLD_DURATION = 0.45f;

        [Header("Audio & Music")]
        [Tooltip("Nine Inch Nails - Forked Reality combat music.")]
        public AudioClip battleMusicClip;
        public AudioSource battleMusicSource;
        public AudioSource wallAmbientAudioSource;
        public AudioSource serverRoomMusicSource;
        [Range(0f, 1f)] public float battleMusicVolume = 0.85f;

        [Header("Enemy Prefabs")]
        [Tooltip("Primary ninja combat robot prefab with InsaneGunSwordController.")]
        public GameObject robotEnemyPrefab;
        [Tooltip("Heavy enforcer combat robot prefab for wave 3 boss.")]
        public GameObject enforcerBossPrefab;
        public Shader holographicShader;

        [Header("Boss Configuration")]
        [Tooltip("Scale multiplier for the Final Wave Rogue AI Master Overseer Boss.")]
        [Range(1.2f, 2.5f)]
        public float bossScale = 1.65f;

        [Header("VFX & Screen Shake")]
        public ParticleSystem sparksFX;
        public ParticleSystem lightningFX;
        public Camera mainCamera;

        [Header("Holographic Palette")]
        [ColorUsage(true, true)] public Color holoCoreColor = new Color(1.0f, 0.04f, 0.20f, 1.0f); // Crimson
        [ColorUsage(true, true)] public Color holoRimColor = new Color(5.0f, 0.2f, 1.5f, 1.0f);  // HDR Magenta
        [ColorUsage(true, true)] public Color edgeDissolveColor = new Color(8.0f, 1.8f, 0.4f, 1.0f);

        // Active State
        private Transform _playerTransform;
        private readonly List<GameObject> _activeEnemies = new List<GameObject>();
        private InsaneGunSwordController _bossController;
        private float _waveIntermissionTimer = 0f;
        private float _lastGroupAttackTime = 0f;
        private string _statusBannerText = "";
        private float _statusBannerTimer = 0f;
        private float _shakeTimer = 0f;
        private float _shakeIntensity = 0f;
        private bool _isPlayerInRange = false;

        // UI Textures
        private Texture2D _boxBgTexture;
        private Texture2D _barFillTexture;
        private Texture2D _barBgTexture;

        private void Awake()
        {
            Instance = this;
            hasTriggered = false;
            currentWave = 0;
            isWaveSpawning = false;
            isEncounterComplete = false;
            _waveIntermissionTimer = 0f;
            _lastGroupAttackTime = 0f;
            _activeEnemies.Clear();
            _isPlayerInRange = false;

            EnsureTextures();
            FindPlayer();

            if (mainCamera == null) mainCamera = Camera.main;

            if (battleMusicSource == null)
            {
                var bmGO = new GameObject("RogueAI_BattleMusic_AudioSource");
                bmGO.transform.SetParent(transform, false);
                battleMusicSource = bmGO.AddComponent<AudioSource>();
                battleMusicSource.loop = true;
                battleMusicSource.playOnAwake = false;
                battleMusicSource.spatialBlend = 0.0f; // 2D full stereo soundtrack!
            }
        }

        private void Start()
        {
            FindPlayer();
            EnsureAssets();
        }

        private void EnsureAssets()
        {
#if UNITY_EDITOR
            if (battleMusicClip == null)
            {
                battleMusicClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/Nine Inch Nails - Forked Reality.mp3");
            }

            if (robotEnemyPrefab == null)
            {
                robotEnemyPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Mission1_Enemy_Ninja.prefab");
            }

            if (enforcerBossPrefab == null)
            {
                enforcerBossPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Mission1_Enemy_Enforcer.prefab");
            }

            if (holographicShader == null)
            {
                holographicShader = Shader.Find("Cyberpunk/Holographic_RogueAI");
            }
#endif
        }

        private void FindPlayer()
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null) { _playerTransform = p.transform; return; }

            var hero = GameObject.Find("Alita_BattleAngel_Hero");
            if (hero != null) { _playerTransform = hero.transform; return; }

            var allCtrl = Object.FindObjectsByType<InsaneGunSwordController>();
            foreach (var c in allCtrl)
            {
                if (c.controlMode == InsaneGunSwordController.ControlMode.PlayerControlled)
                {
                    _playerTransform = c.transform;
                    return;
                }
            }
        }

        private void Update()
        {
            if (_playerTransform == null) FindPlayer();

            // Screen shake
            if (_shakeTimer > 0f && mainCamera != null)
            {
                _shakeTimer -= Time.deltaTime;
                Vector3 shakeOffset = Random.insideUnitSphere * _shakeIntensity;
                mainCamera.transform.position += shakeOffset;
            }

            // Status banner decay
            if (_statusBannerTimer > 0f)
            {
                _statusBannerTimer -= Time.deltaTime;
            }

            // Proximity check before encounter is triggered
            if (!hasTriggered && _playerTransform != null)
            {
                float dist = Vector3.Distance(transform.position, _playerTransform.position);
                _isPlayerInRange = dist <= interactionRadius;

                if (_isPlayerInRange)
                {
                    bool isHolding = Input.GetKey(KeyCode.E) || Input.GetKey(interactKey) || Input.GetKey(KeyCode.F);
                    if (isHolding)
                    {
                        _breachHoldTimer += Time.deltaTime;
                        if (_breachHoldTimer >= INTERACT_HOLD_DURATION)
                        {
                            _breachHoldTimer = 0f;
                            TriggerEncounter();
                        }
                    }
                    else
                    {
                        _breachHoldTimer = Mathf.Max(0f, _breachHoldTimer - Time.deltaTime * 3.5f);
                    }
                }
                else
                {
                    _breachHoldTimer = 0f;
                }
                return;
            }

            // Wave progression update (only active once Wave 1 has actually begun)
            if (hasTriggered && !isEncounterComplete && currentWave > 0)
            {
                // Clean up dead/destroyed enemies from active list
                _activeEnemies.RemoveAll(e => e == null || (e.GetComponent<InsaneGunSwordController>() != null && e.GetComponent<InsaneGunSwordController>().IsDead));

                // Drive relentless AI aggression and coordinated sword attacks
                DriveActiveAggression();

                // If wave is active and all enemies are eliminated
                if (!isWaveSpawning && _activeEnemies.Count == 0)
                {
                    if (currentWave < totalWaves)
                    {
                        // Progress to next wave after a generous 4-second breather
                        if (_waveIntermissionTimer <= 0f)
                        {
                            _waveIntermissionTimer = 4.0f;
                            ShowStatusBanner($"/// WAVE {currentWave} DEFEATED // NEXT INCURSION IN 4 SECONDS ///", 4.0f);
                        }
                        else
                        {
                            _waveIntermissionTimer -= Time.deltaTime;
                            if (_waveIntermissionTimer <= 0f)
                            {
                                currentWave++;
                                StartCoroutine(SpawnWaveRoutine(currentWave));
                            }
                        }
                    }
                    else if (currentWave >= totalWaves)
                    {
                        // All waves cleared!
                        isEncounterComplete = true;
                        OnEncounterVictory();
                    }
                }
            }

            // Post-encounter communion check with the Giant Blackwall Female LiDAR Face
            if (isEncounterComplete && !hasCommuned && _playerTransform != null)
            {
                if (BlackwallStoryDialogueHUD.Instance != null && BlackwallStoryDialogueHUD.Instance.isDialogueActive)
                {
                    _isCommunePromptActive = false;
                    _communeHoldTimer = 0f;
                }
                else
                {
                    float dist = Vector3.Distance(transform.position, _playerTransform.position);
                    _isCommunePromptActive = dist <= 9.5f;

                    if (_isCommunePromptActive)
                    {
                        bool isHolding = Input.GetKey(KeyCode.E) || Input.GetKey(interactKey) || Input.GetKey(KeyCode.F);
                        if (isHolding)
                        {
                            _communeHoldTimer += Time.deltaTime;
                            if (_communeHoldTimer >= INTERACT_HOLD_DURATION)
                            {
                                hasCommuned = true;
                                _isCommunePromptActive = false;
                                _communeHoldTimer = 0f;
                                if (BlackwallStoryDialogueHUD.Instance != null)
                                {
                                    BlackwallStoryDialogueHUD.Instance.StartBlackwallDialogue();
                                }
                            }
                        }
                        else
                        {
                            _communeHoldTimer = Mathf.Max(0f, _communeHoldTimer - Time.deltaTime * 3.5f);
                        }
                    }
                    else
                    {
                        _communeHoldTimer = 0f;
                    }
                }
            }
        }

        /// <summary>
        /// Starts the encounter when the player presses [E] at RogueAI_Sparks_FX.
        /// </summary>
        public void TriggerEncounter()
        {
            if (hasTriggered) return;
            hasTriggered = true;
            _isPlayerInRange = false;

            Debug.Log("<color=#FF0055><b>[Rogue AI Encounter]</b></color> [E] pressed! Initializing Blackwall Multi-Wave Incursion...");

            // Visual shock & screen rumble
            if (sparksFX != null) sparksFX.Play();
            if (lightningFX != null) lightningFX.Play();
            TriggerScreenShake(0.8f, 0.25f);

            // Audio transition to Nine Inch Nails - Forked Reality
            StartCoroutine(TransitionToBattleMusicRoutine());

            // Dramatic telegraph: give player 2.5 seconds to back up, draw sword, and prepare!
            ShowStatusBanner("/// CRITICAL WARNING: BLACKWALL BREACH INITIATING IN 3... 2... 1... ///", 2.8f);

            // Lock wave spawning flag so intermission doesn't prematurely trigger
            isWaveSpawning = true;
            StartCoroutine(DelayedStartWaveOneRoutine());
        }

        private IEnumerator DelayedStartWaveOneRoutine()
        {
            isWaveSpawning = true;
            yield return new WaitForSeconds(2.5f);
            currentWave = 1;
            StartCoroutine(SpawnWaveRoutine(currentWave));
        }

        private IEnumerator TransitionToBattleMusicRoutine()
        {
            float fadeTimer = 0f;
            float initialWallVol = wallAmbientAudioSource != null ? wallAmbientAudioSource.volume : 0f;
            float initialServerVol = serverRoomMusicSource != null ? serverRoomMusicSource.volume : 0f;

            if (battleMusicClip != null && battleMusicSource != null)
            {
                battleMusicSource.clip = battleMusicClip;
                battleMusicSource.volume = 0f;
                battleMusicSource.Play();
            }

            while (fadeTimer < 1.0f)
            {
                fadeTimer += Time.deltaTime;
                float t = fadeTimer / 1.0f;

                if (wallAmbientAudioSource != null) wallAmbientAudioSource.volume = Mathf.Lerp(initialWallVol, 0.65f, t);
                if (serverRoomMusicSource != null) serverRoomMusicSource.volume = Mathf.Lerp(initialServerVol, 0.0f, t);
                if (battleMusicSource != null) battleMusicSource.volume = Mathf.Lerp(0f, battleMusicVolume, t);

                yield return null;
            }

            if (battleMusicSource != null) battleMusicSource.volume = battleMusicVolume;
        }

        /// <summary>
        /// Spawns an incursion wave emerging out of the Blackwall plane (Z ~ 75.5m -> 71.5m).
        /// </summary>
        private IEnumerator SpawnWaveRoutine(int waveNumber)
        {
            isWaveSpawning = true;
            EnsureAssets();

            string waveTitle = waveNumber switch
            {
                1 => "/// BLACKWALL INVASION // WAVE 1 / 3: RECON INFILTRATORS ///",
                2 => "/// BLACKWALL INVASION // WAVE 2 / 3: COMBAT SWARM PROTOCOL ///",
                3 => "/// BLACKWALL INVASION // FINAL WAVE: PROTOCOL ZERO OVERSEER ///",
                _ => $"/// BLACKWALL INVASION // WAVE {waveNumber} ///"
            };

            ShowStatusBanner(waveTitle, 4.0f);

            // Shockwave particles & camera shake on wave breach
            if (lightningFX != null) lightningFX.Play();
            if (sparksFX != null) sparksFX.Play();
            TriggerScreenShake(0.6f, 0.20f);

            yield return new WaitForSeconds(0.8f);

            float spawnZ = 74.0f; // Directly on the Blackwall LiDAR barrier plane (Z=75)

            if (waveNumber == 1)
            {
                // Wave 1: 2 Infiltrator Robots (X = -4.0m and +4.0m)
                SpawnSingleRogueAI(new Vector3(-4.0f, 0.05f, spawnZ), isBoss: false, chassisIndex: 0);
                SpawnSingleRogueAI(new Vector3(4.0f, 0.05f, spawnZ), isBoss: false, chassisIndex: 1);
            }
            else if (waveNumber == 2)
            {
                // Wave 2: 3 Swarm Robots (X = -6.0m, 0.0m, +6.0m)
                SpawnSingleRogueAI(new Vector3(-5.5f, 0.05f, spawnZ), isBoss: false, chassisIndex: 0);
                SpawnSingleRogueAI(new Vector3(0.0f, 0.05f, spawnZ + 0.3f), isBoss: false, chassisIndex: 2);
                SpawnSingleRogueAI(new Vector3(5.5f, 0.05f, spawnZ), isBoss: false, chassisIndex: 1);
            }
            else if (waveNumber == 3)
            {
                // Wave 3: 1 Master Rogue AI Overseer (Center, scale 1.15x, 260 HP) + 2 Elite Escorts
                SpawnSingleRogueAI(new Vector3(0.0f, 0.05f, spawnZ + 0.3f), isBoss: true, chassisIndex: 0);
                SpawnSingleRogueAI(new Vector3(-4.5f, 0.05f, spawnZ), isBoss: false, chassisIndex: 1);
                SpawnSingleRogueAI(new Vector3(4.5f, 0.05f, spawnZ), isBoss: false, chassisIndex: 0);
            }

            yield return new WaitForSeconds(2.0f);
            isWaveSpawning = false;
        }

        /// <summary>
        /// Instantiates, cleanses (NO mercenary/cyborg), holographs, and launches a Rogue AI robot.
        /// </summary>
        public GameObject SpawnSingleRogueAI(Vector3 spawnPos, bool isBoss, int chassisIndex)
        {
            GameObject prefabToUse = (isBoss && enforcerBossPrefab != null) ? enforcerBossPrefab : robotEnemyPrefab;
            if (prefabToUse == null) prefabToUse = robotEnemyPrefab;
            if (prefabToUse == null) return null;

            Quaternion spawnRot = Quaternion.Euler(0f, 180f, 0f); // Facing forward towards player (-Z)
            GameObject enemyGO = Instantiate(prefabToUse, spawnPos, spawnRot);
            enemyGO.name = isBoss ? "RogueAI_Master_Overseer" : $"RogueAI_Unit_{_activeEnemies.Count + 1}";
            enemyGO.tag = "Enemy";

            if (isBoss)
            {
                enemyGO.transform.localScale = Vector3.one * bossScale;

                var cc = enemyGO.GetComponent<CharacterController>();
                if (cc != null)
                {
                    cc.stepOffset = 0.5f;
                }
            }

            // 1. CRITICAL: Destroy Mission1_EnemyTracker IMMEDIATELY to prevent it from setting TrainingDummy & inputLocked!
            var trackers = enemyGO.GetComponentsInChildren<Cyberpunk.Missions.Mission1_EnemyTracker>(true);
            foreach (var t in trackers)
            {
                if (t != null) DestroyImmediate(t);
            }

            // 2. Remove all colliders on child weapons to prevent CharacterController self-collision
            foreach (var col in enemyGO.GetComponentsInChildren<Collider>(true))
            {
                if (col != null && col != enemyGO.GetComponent<CharacterController>() &&
                    (col.gameObject.name.Contains("Katana") || col.gameObject.name.Contains("Blade") || col.gameObject.name.Contains("Sword")))
                {
                    DestroyImmediate(col);
                }
            }

            // 2.5 Deactivate all firearm/gun meshes so robots strictly wield swords only
            foreach (var t in enemyGO.GetComponentsInChildren<Transform>(true))
            {
                if (t != null && t.gameObject != enemyGO &&
                    (t.gameObject.name.Contains("Gun") || t.gameObject.name.Contains("Pistol") || t.gameObject.name.Contains("Rifle") || t.gameObject.name.Contains("Firearm")))
                {
                    if (t.GetComponent<MonoBehaviour>() == null)
                    {
                        t.gameObject.SetActive(false);
                    }
                }
            }

            // 3. Remove any EnemyDuelHUD with "CYBORG MERCENARY" text and all Razor Punk billboards!
            StripEnemyDuelHUD(enemyGO);

            // 4. Ensure Layer 9 (Enemy) recursively on all colliders & bones
            SetLayerRecursively(enemyGO, 9);

            // 5. STRICT REQUIREMENT: Deactivate all mercenary, cyborg, soldier, street meshes!
            // Activate exclusively Robot chassis (SM_Chr_Robot_01, SM_Chr_Robot_Female_01, SM_Chr_Robot_Damaged_01)
            SanitizeAndSelectRobotMesh(enemyGO, chassisIndex);

            // 6. Apply full Cyberpunk crimson/magenta holographic shader
            List<Material> holoMats = ApplyHolographicShader(enemyGO);

            // 7. Explicitly resolve Hand_R socket and sword weapon transform
            Transform handR = null;
            Transform blade = null;
            foreach (var t in enemyGO.GetComponentsInChildren<Transform>(true))
            {
                if (handR == null && (t.name == "Hand_R" || t.name.Contains("RightHand"))) handR = t;
                if (blade == null && (t.name.Contains("Katana") || t.name.Contains("Blade") || t.name.Contains("Sword"))) blade = t;
            }

            // 8. Configure InsaneGunSwordController combat AI
            var ctrl = enemyGO.GetComponent<InsaneGunSwordController>();
            if (ctrl != null)
            {
                ctrl.EnsureComponents();
                if (_playerTransform == null) FindPlayer();
                ctrl.targetEnemy = _playerTransform;
                ctrl.controlMode = InsaneGunSwordController.ControlMode.AICirclingEnemy;
                if (handR != null) ctrl.rightHandSocket = handR;
                if (blade != null) ctrl.swordInstance = blade;
                ctrl.isSwordDrawn = true;
                ctrl.inputLocked = true; // Lock during 1.8s emergence so player has ample time to react
                ctrl.enabled = true;
                ctrl.autoReviveOnDeath = false;
                ctrl.enemyLayers = (1 << 8) | (1 << 0);
                ctrl.groundLayers = ~0;

                // Configure Defense & Poise so enemies are satisfying and fair to defeat
                ctrl.aiParryChance = isBoss ? 0.04f : 0.0f;          // 0% parry for grunts (player hits land cleanly)
                ctrl.aiBlockChance = isBoss ? 0.12f : 0.05f;         // Very rare block
                ctrl.aiBlockMitigation = 0.35f;                      // 65% damage taken even when blocking
                ctrl.aiDodgeReactionChance = isBoss ? 0.15f : 0.08f;
                ctrl.maxPoise = isBoss ? 60f : 15f;                  // Grunts flinch and stagger immediately on hit
                ctrl.currentPoise = ctrl.maxPoise;
                ctrl.hasHyperArmor = isBoss;                         // Only boss has hyper armor

                if (isBoss)
                {
                    ctrl.maxHealth = 320f;
                    ctrl.currentHealth = 320f;
                    ctrl.attackDamage = 16f;
                    ctrl.heavyAttackDamage = 24f;
                    ctrl.walkSpeed = 3.6f;
                    ctrl.runSpeed = 6.2f;
                    ctrl.minAttackInterval = 0.35f;
                    ctrl.maxPoise = 85f;
                    ctrl.currentPoise = 85f;
                    _bossController = ctrl;
                }
                else
                {
                    float baseHp = 85f + (currentWave * 15f);
                    ctrl.maxHealth = baseHp;
                    ctrl.currentHealth = baseHp;
                    ctrl.attackDamage = 8f + (currentWave * 1.5f);
                    ctrl.heavyAttackDamage = 13f + (currentWave * 2f);
                    ctrl.walkSpeed = 3.2f + (currentWave * 0.15f);
                    ctrl.runSpeed = 5.8f + (currentWave * 0.2f);
                    ctrl.minAttackInterval = 0.40f;
                }
            }

            // 9. Start holographic emergence materialization routine
            StartCoroutine(EmergenceGlideRoutine(enemyGO, holoMats));

            _activeEnemies.Add(enemyGO);
            return enemyGO;
        }

        private IEnumerator EmergenceGlideRoutine(GameObject enemyGO, List<Material> holoMats)
        {
            if (enemyGO == null) yield break;

            float duration = 1.8f; // Generous 1.8s emergence so player has ample time to react
            float elapsed = 0f;

            while (elapsed < duration && enemyGO != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                foreach (var mat in holoMats)
                {
                    if (mat != null)
                    {
                        mat.SetFloat("_Dissolve", t);
                        mat.SetFloat("_GlitchIntensity", Mathf.Lerp(0.85f, 0.32f, t));
                    }
                }

                yield return null;
            }

            if (enemyGO != null)
            {
                foreach (var mat in holoMats)
                {
                    if (mat != null)
                    {
                        mat.SetFloat("_Dissolve", 1.0f);
                        mat.SetFloat("_GlitchIntensity", 0.32f);
                    }
                }

                var ctrl = enemyGO.GetComponent<InsaneGunSwordController>();
                if (ctrl != null)
                {
                    if (_playerTransform == null) FindPlayer();
                    ctrl.targetEnemy = _playerTransform;
                    ctrl.controlMode = InsaneGunSwordController.ControlMode.AICirclingEnemy;
                    ctrl.inputLocked = false;
                    ctrl.isSwordDrawn = true;
                    EnemyGroupCombatCoordinator.Instance?.RegisterCombatant(ctrl, _playerTransform);
                }
            }
        }

        /// <summary>
        /// Relentlessly drives living Rogue AI enemies to attack Alita in coordinated rhythm.
        /// Integrates with EnemyGroupCombatCoordinator so when 2 or more enemies are active,
        /// they surround Alita first and coordinate attacks one at a time via single-token cadence.
        /// </summary>
        private void DriveActiveAggression()
        {
            if (_playerTransform == null) return;

            var coordinator = EnemyGroupCombatCoordinator.Instance;
            if (coordinator != null && coordinator.IsGroupSurroundActive(_playerTransform))
            {
                // Multi-Enemy Coordination Active:
                // EnemyGroupCombatCoordinator manages the surrounding ring formation and
                // grants attack tokens one by one. Do not force uncoordinated attacks!
                return;
            }

            if (Time.time - _lastGroupAttackTime < 0.65f) return;

            for (int i = 0; i < _activeEnemies.Count; i++)
            {
                var enemyGO = _activeEnemies[i];
                if (enemyGO == null) continue;

                var ctrl = enemyGO.GetComponent<InsaneGunSwordController>();
                if (ctrl == null || ctrl.IsDead || ctrl.inputLocked) continue;

                float dist = Vector3.Distance(enemyGO.transform.position, _playerTransform.position);
                float strikeRange = ctrl.transform.localScale.x > 1.25f ? 4.2f : 3.2f;

                // If within striking distance and not currently attacking, unleash sword strikes!
                if (dist <= strikeRange && !ctrl.IsAttacking && !ctrl.IsComboRunning)
                {
                    float r = Random.value;
                    if (r < 0.40f)
                    {
                        ctrl.StartLightAttack(Vector3.zero);
                    }
                    else if (r < 0.70f)
                    {
                        ctrl.StartAIGunSwordCombo(InsaneGunSwordController.AIGunSwordComboType.ExecutionerShred);
                    }
                    else
                    {
                        ctrl.StartAIGunSwordCombo(InsaneGunSwordController.AIGunSwordComboType.BlitzkriegOverdrive);
                    }
                    _lastGroupAttackTime = Time.time;
                    break;
                }
                // If enemy is mid-range (3.2m to 7.0m) and not attacking, trigger gap closer
                else if (dist > 3.2f && dist <= 7.0f && !ctrl.IsAttacking && !ctrl.IsComboRunning)
                {
                    if (Random.value < 0.20f)
                    {
                        ctrl.StartAIGunSwordCombo(InsaneGunSwordController.AIGunSwordComboType.StalkerLunge);
                        _lastGroupAttackTime = Time.time;
                        break;
                    }
                }
            }
        }

        private void SetLayerRecursively(GameObject obj, int newLayer)
        {
            if (obj == null) return;
            obj.layer = newLayer;
            foreach (Transform child in obj.transform)
            {
                if (child != null) SetLayerRecursively(child.gameObject, newLayer);
            }
        }

        /// <summary>
        /// Selects only the Robot chassis (Robot_01 / Robot_Female_01 / Robot_Damaged_01)
        /// and permanently deactivates all Cyborg, Mercenary, Soldier, and civilian models.
        /// </summary>
        private void SanitizeAndSelectRobotMesh(GameObject enemyGO, int chassisIndex)
        {
            var renderers = enemyGO.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            string targetName = chassisIndex switch
            {
                1 => "SM_Chr_Robot_Female_01",
                2 => "SM_Chr_Robot_Damaged_01",
                _ => "SM_Chr_Robot_01"
            };

            bool foundTarget = false;

            // 1. Activate preferred robot mesh
            foreach (var smr in renderers)
            {
                if (smr.name == targetName)
                {
                    smr.gameObject.SetActive(true);
                    foundTarget = true;
                    break;
                }
            }

            // Fallback: If target wasn't found, pick SM_Chr_Robot_01
            if (!foundTarget)
            {
                foreach (var smr in renderers)
                {
                    if (smr.name == "SM_Chr_Robot_01")
                    {
                        smr.gameObject.SetActive(true);
                        foundTarget = true;
                        break;
                    }
                }
            }

            // 2. Disable ALL other character meshes
            foreach (var smr in renderers)
            {
                string n = smr.name;
                if (n.StartsWith("SM_Chr_") && n != targetName && (!foundTarget || n != "SM_Chr_Robot_01"))
                {
                    smr.gameObject.SetActive(false);
                }
            }

            // 3. Disable all attachments (hats, pouches, straps, masks, hair)
            foreach (var t in enemyGO.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                if (n.Contains("Attach") || n.Contains("Mercenary") || n.Contains("Cyborg"))
                {
                    t.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Strips any EnemyDuelHUD component or floating billboard text saying "CYBORG MERCENARY".
        /// </summary>
        private void StripEnemyDuelHUD(GameObject enemyGO)
        {
            var duelHUDs = enemyGO.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var mb in duelHUDs)
            {
                if (mb == null) continue;
                string typeName = mb.GetType().Name;
                if (typeName.Contains("EnemyDuelHUD") || typeName.Contains("DuelHUD") || typeName.Contains("EnemyTracker"))
                {
                    DestroyImmediate(mb);
                }
            }

            // Also destroy any world-space canvases or text children with CYBORG, RAZOR, PUNK in their name
            foreach (var t in enemyGO.GetComponentsInChildren<Transform>(true))
            {
                if (t != null)
                {
                    string n = t.name.ToUpperInvariant();
                    if (n.Contains("HUD_SCIFI") || n.Contains("DUELHUD") || n.Contains("ENEMYTRACKER") || 
                        n.Contains("FLOATINGBAR") || n.Contains("RAZOR") || n.Contains("PUNK") || n.Contains("MERCENARY"))
                    {
                        DestroyImmediate(t.gameObject);
                    }
                }
            }
        }

        /// <summary>
        /// Applies the glowing Cyberpunk Rogue AI Holographic shader to all renderers.
        /// </summary>
        private List<Material> ApplyHolographicShader(GameObject characterGO)
        {
            var mats = new List<Material>();
            if (holographicShader == null) holographicShader = Shader.Find("Cyberpunk/Holographic_RogueAI");
            if (holographicShader == null) return mats;

            var allRenderers = characterGO.GetComponentsInChildren<Renderer>(false);
            foreach (var r in allRenderers)
            {
                if (r == null || !r.gameObject.activeInHierarchy) continue;

                var origMats = r.sharedMaterials;
                var newMats = new Material[origMats.Length];

                for (int i = 0; i < origMats.Length; i++)
                {
                    var holoMat = new Material(holographicShader);
                    holoMat.name = $"Holo_{r.name}_{i}";
                    holoMat.SetColor("_HoloColor", holoCoreColor);
                    holoMat.SetColor("_RimColor", holoRimColor);
                    holoMat.SetColor("_DissolveEdgeColor", edgeDissolveColor);
                    holoMat.SetFloat("_CoreAlpha", 0.35f);
                    holoMat.SetFloat("_RimPower", 2.2f);
                    holoMat.SetFloat("_ScanlineDensity", 160.0f);
                    holoMat.SetFloat("_ScanlineIntensity", 0.45f);
                    holoMat.SetFloat("_GridIntensity", 0.30f);
                    holoMat.SetFloat("_GlitchIntensity", 0.40f);
                    holoMat.SetFloat("_Dissolve", 0.0f);

                    if (origMats[i] != null && origMats[i].mainTexture != null)
                    {
                        holoMat.SetTexture("_BaseMap", origMats[i].mainTexture);
                    }

                    newMats[i] = holoMat;
                    mats.Add(holoMat);
                }

                r.materials = newMats;
            }

            return mats;
        }

        public void TriggerScreenShake(float duration = 0.5f, float intensity = 0.2f)
        {
            _shakeTimer = duration;
            _shakeIntensity = intensity;
        }

        private void ShowStatusBanner(string text, float duration = 3.5f)
        {
            _statusBannerText = text;
            _statusBannerTimer = duration;
        }

        private void OnEncounterVictory()
        {
            ShowStatusBanner("/// ALL ROGUE AI ENTITIES PURGED // BLACKWALL RESTORED ///", 8.0f);
            Debug.Log("<color=#00FFAA><b>[Rogue AI Encounter]</b></color> All waves purged! Blackwall firewall restored.");
            StartCoroutine(FadeOutMusicOnDefeat());
        }

        private IEnumerator FadeOutMusicOnDefeat()
        {
            yield return new WaitForSeconds(3.0f);
            float timer = 0f;
            float startVol = battleMusicSource != null ? battleMusicSource.volume : 0f;

            while (timer < 4.0f)
            {
                timer += Time.deltaTime;
                if (battleMusicSource != null)
                {
                    battleMusicSource.volume = Mathf.Lerp(startVol, 0.2f, timer / 4.0f);
                }
                yield return null;
            }
        }

        // ══════════════════════════════════════════════════════════════════════════
        // OnGUI CYBERPUNK HUD (Interaction Prompt, Wave Tracker, Boss Bar)
        // ══════════════════════════════════════════════════════════════════════════
        private void EnsureTextures()
        {
            if (_boxBgTexture == null)
            {
                _boxBgTexture = new Texture2D(1, 1);
                _boxBgTexture.SetPixel(0, 0, new Color(0.04f, 0.01f, 0.06f, 0.88f));
                _boxBgTexture.Apply();
            }

            if (_barFillTexture == null)
            {
                _barFillTexture = new Texture2D(1, 1);
                _barFillTexture.SetPixel(0, 0, new Color(1.0f, 0.08f, 0.25f, 1.0f));
                _barFillTexture.Apply();
            }

            if (_barBgTexture == null)
            {
                _barBgTexture = new Texture2D(1, 1);
                _barBgTexture.SetPixel(0, 0, new Color(0.12f, 0.02f, 0.04f, 0.85f));
                _barBgTexture.Apply();
            }
        }

        private void OnGUI()
        {
            EnsureTextures();

            // 1. Interaction Prompt before encounter
            if (!hasTriggered && _isPlayerInRange)
            {
                DrawCyberpunkInteractPrompt("E", "Breach [Hold]", _breachHoldTimer / INTERACT_HOLD_DURATION);
            }

            // 2. Active Wave HUD
            if (hasTriggered)
            {
                DrawWaveTrackerHUD();
            }

            // 3. Status Banner
            if (_statusBannerTimer > 0f)
            {
                DrawStatusBanner();
            }

            // 4. Wave 3 Boss Health Bar
            if (currentWave == 3 && _bossController != null && !_bossController.IsDead)
            {
                DrawBossHealthBar();
            }

            // 5. Post-combat Communion Prompt with Giant Blackwall Face
            if (isEncounterComplete && !hasCommuned && _isCommunePromptActive)
            {
                DrawCyberpunkInteractPrompt("E", "Commune [Hold]", _communeHoldTimer / INTERACT_HOLD_DURATION);
            }
        }

        /// <summary>
        /// Renders authentic Cyberpunk 2077 interaction prompt:
        /// [ F ]  Action [Hold]
        /// Features glowing electric cyan border, dark cyber-badge, centered key label,
        /// bottom-to-top hold gauge fill, and sleek cyan action title.
        /// </summary>
        private void DrawCyberpunkInteractPrompt(string keyLabel, string actionLabel, float progress)
        {
            Cyberpunk.UI.CyberpunkInteractionPromptHUD.DrawPromptImmediate(keyLabel, actionLabel, progress);
        }

        private void DrawWaveTrackerHUD()
        {
            float boxWidth = 320f;
            float boxHeight = 36f;
            float x = (Screen.width - boxWidth) * 0.5f;
            float y = 16f;

            GUI.color = new Color(0.04f, 0.01f, 0.06f, 0.75f);
            GUI.DrawTexture(new Rect(x, y, boxWidth, boxHeight), _boxBgTexture);

            GUI.color = new Color(1.0f, 0.1f, 0.3f, 0.8f);
            GUI.DrawTexture(new Rect(x, y, boxWidth, 1.5f), _barFillTexture);
            GUI.DrawTexture(new Rect(x, y + boxHeight - 1.5f, boxWidth, 1.5f), _barFillTexture);

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            style.normal.textColor = new Color(1.0f, 0.25f, 0.4f, 1.0f);

            string waveInfo = isEncounterComplete 
                ? "BLACKWALL SECURE // ALL HOSTILES PURGED" 
                : $"WAVE {currentWave} / {totalWaves}  |  HOSTILES REMAINING: {_activeEnemies.Count}";

            GUI.Label(new Rect(x, y + 7f, boxWidth, 22f), waveInfo, style);
            GUI.color = Color.white;
        }

        private void DrawStatusBanner()
        {
            float boxWidth = 560f;
            float boxHeight = 44f;
            float x = (Screen.width - boxWidth) * 0.5f;
            float y = 60f;

            GUI.color = new Color(0.03f, 0.01f, 0.05f, 0.90f);
            GUI.DrawTexture(new Rect(x, y, boxWidth, boxHeight), _boxBgTexture);

            GUI.color = isEncounterComplete ? new Color(0.2f, 1.0f, 0.6f, 0.9f) : new Color(1.0f, 0.15f, 0.35f, 0.9f);
            GUI.DrawTexture(new Rect(x, y, boxWidth, 2f), _barFillTexture);
            GUI.DrawTexture(new Rect(x, y + boxHeight - 2f, boxWidth, 2f), _barFillTexture);

            var bannerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            bannerStyle.normal.textColor = isEncounterComplete ? new Color(0.2f, 1.0f, 0.6f, 1.0f) : new Color(1.0f, 0.3f, 0.45f, 1.0f);

            GUI.Label(new Rect(x, y + 10f, boxWidth, 24f), _statusBannerText, bannerStyle);
            GUI.color = Color.white;
        }

        private void DrawBossHealthBar()
        {
            float barWidth = Mathf.Min(680f, Screen.width * 0.55f);
            float barHeight = 16f;
            float x = (Screen.width - barWidth) * 0.5f;
            float y = 112f;

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            titleStyle.normal.textColor = new Color(1.0f, 0.2f, 0.35f, 1.0f);

            var subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter
            };
            subStyle.normal.textColor = new Color(0.75f, 0.75f, 0.85f, 0.8f);

            GUI.Label(new Rect(x - 50f, y - 22f, barWidth + 100f, 20f), "/// MASTER ROGUE AI OVERSEER - PROTOCOL ZERO ///", titleStyle);

            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(x, y, barWidth, barHeight), _barBgTexture);

            float pct = Mathf.Clamp01(_bossController.currentHealth / _bossController.maxHealth);
            GUI.color = new Color(1.0f, 0.1f, 0.25f, 1.0f);
            GUI.DrawTexture(new Rect(x, y, barWidth * pct, barHeight), _barFillTexture);

            GUI.color = new Color(1.0f, 0.3f, 0.5f, 0.9f);
            GUI.DrawTexture(new Rect(x, y, barWidth, 1.5f), _barFillTexture);
            GUI.DrawTexture(new Rect(x, y + barHeight - 1.5f, barWidth, 1.5f), _barFillTexture);
            GUI.DrawTexture(new Rect(x, y, 1.5f, barHeight), _barFillTexture);
            GUI.DrawTexture(new Rect(x + barWidth - 1.5f, y, 1.5f, barHeight), _barFillTexture);

            GUI.Label(new Rect(x, y + barHeight + 4f, barWidth, 18f), $"THREAT: CRITICAL  |  HEALTH: {Mathf.CeilToInt(_bossController.currentHealth)} / {Mathf.CeilToInt(_bossController.maxHealth)}  |  HYPER ARMOR ACTIVE", subStyle);

            GUI.color = Color.white;
        }
    }
}
