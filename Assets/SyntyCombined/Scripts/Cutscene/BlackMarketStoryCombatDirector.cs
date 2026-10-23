using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using SyntyCombined.BlackMarket;
using SyntyCombined.Cutscene;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SyntyCombined.BlackMarket
{
    public enum BlackMarketStoryState
    {
        InvestigateMarket,       // Inquire with merchants to track down Voss
        InfiltrateRefineryTask,  // Navigate to Refinery Terminal & hack Data Core
        ReturnToVoss,            // Deliver Data Core back to Voss
        BetrayalAmbush,          // Voss reveals 10M credit bounty & calls bounty hunters
        FleeAcrossCatwalk,       // Alita runs across elevated gantry catwalk
        CatwalkCollapse_Fall,    // Explosion detonates, catwalk shatters, Alita plummets to lower trench
        LowerTrenchCombat,       // Recover from fall, eliminate surviving pursuing bounty hunters
        EscapeToStarship,        // Sprint to Pad B-04
        HyperspaceWarp           // Press [R] to engage warp to Mars
    }

    /// <summary>
    /// Master Story & Combat Director for DRIFT Sector 04 (Black Market).
    /// Orchestrates the revised Act I cinematic narrative arc:
    /// 1. Investigate Market: Question merchants to locate Voss
    /// 2. Task Offered: Voss demands an encrypted Data Core from the refinery terminal in exchange for Mars coordinates
    /// 3. Infiltrate & Retrieve: Alita hacks refinery terminal and extracts the Data Core
    /// 4. Double-Cross: Voss takes the core, reveals Omni-Corp's 10M credit bounty on her head, triggers station alarm
    /// 5. Chase & Fall: Syndicate bounty hunters ambush; Alita flees across the high catwalk;
    ///    an explosive blast shatters the gantry and knocks Alita 4 meters down into the lower cargo alley
    /// 6. Lower Trench Combat: Alita recovers, slashes through pursuing bounty hunters with Damascus blade
    /// 7. Emergency Starship Escape: Sprints to Pad B-04 and engages [R] Hyperspace Warp to Mars Cydonia Vault
    /// </summary>
    public class BlackMarketStoryCombatDirector : MonoBehaviour
    {
        public static BlackMarketStoryCombatDirector Instance { get; private set; }

        [Header("Story Progression State")]
        public BlackMarketStoryState currentState = BlackMarketStoryState.InvestigateMarket;

        [Header("Investigation Leads")]
        [Tooltip("How many market leads Alita must cross-reference before Voss will deal with her")]
        public int requiredLeads = 2;

        [Header("Ambush & Combat Settings")]
        [Tooltip("Spawn positions for alleyway bounty hunters upon betrayal")]
        public Vector3[] alleyAmbushPoints = new Vector3[]
        {
            new Vector3(17.5f, 1.88f, 32.5f),
            new Vector3(21.0f, 1.88f, 29.5f)
        };

        [Tooltip("Spawn positions for lower cargo trench pursuers after Alita falls")]
        public Vector3[] lowerTrenchAmbushPoints = new Vector3[]
        {
            new Vector3(14.5f, -2.25f, 24.0f),
            new Vector3(10.5f, -2.25f, 22.0f)
        };

        [Header("Audio & Lighting FX")]
        public Color emergencyRedLightColor = new Color(1.0f, 0.05f, 0.05f);
        public AudioClip alarmSirenSFX;
        public AudioClip explosionSFX;
        public AudioClip fallLandingSFX;
        public AudioClip missionUpdateSFX;
        public AudioClip warpDriveSFX;

        [Header("Scene References")]
        public Transform landingPadCenter;
        public Transform starshipTransform;
        public BlackMarketNPC fixerVossNPC;
        public RefineryDataTerminal refineryTerminal;
        public BlackMarketCatwalkFallTrigger catwalkFallTrigger;

        // Runtime state
        private readonly HashSet<string> _leadsFound = new HashSet<string>();
        private List<GameObject> _activeEnemies = new List<GameObject>();
        private int _totalEnemiesSpawned = 0;
        private Light _emergencyStrobeLight;
        private bool _isStrobeActive = false;
        private float _strobeTimer = 0f;
        private Transform _playerTransform;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
        }

        private void Start()
        {
            FindPlayer();
            SetupEmergencyLight();
            LoadAudioClipsIfMissing();

            // Set initial state
            SetStoryState(BlackMarketStoryState.InvestigateMarket);

            StartCoroutine(IntroStoryBeats());
        }

        // ==================== Investigation Leads ====================

        public int LeadsFound => _leadsFound.Count;
        public int RequiredLeads => Mathf.Max(1, requiredLeads);
        public bool HasEnoughLeads => LeadsFound >= RequiredLeads;

        /// <summary>
        /// Registers a market lead gathered from an informant NPC.
        /// Two matching whispers are enough for Voss to stop stonewalling Alita.
        /// </summary>
        public void RegisterLead(string leadId, string leadTitle, string leadInsight)
        {
            if (currentState != BlackMarketStoryState.InvestigateMarket) return;
            if (string.IsNullOrEmpty(leadId) || !_leadsFound.Add(leadId)) return;

            bool wasEnough = LeadsFound - 1 >= RequiredLeads;
            int shown = Mathf.Min(LeadsFound, RequiredLeads);
            var hud = BlackMarketNPCDialogueHUD.GetOrCreate();

            if (!wasEnough && !string.IsNullOrEmpty(leadTitle))
            {
                hud.ShowMissionBanner($"LEAD {shown}/{RequiredLeads} — {leadTitle}");
            }

            if (CyberpunkHUDController.Instance != null)
            {
                if (HasEnoughLeads)
                {
                    CyberpunkHUDController.Instance.SetQuestObjective(
                        "THE SALVAGE MAN",
                        "► Confront Kaelen Voss beneath the gantry"
                    );
                }
                else
                {
                    CyberpunkHUDController.Instance.SetQuestObjective(
                        "THE SALVAGE MAN",
                        $"► Cross-reference the market's whispers about Voss ({shown}/{RequiredLeads})"
                    );
                }

                if (!string.IsNullOrEmpty(leadInsight))
                {
                    CyberpunkHUDController.Instance.ShowToast(leadInsight, 5.0f);
                }
            }

            if (!wasEnough && HasEnoughLeads)
            {
                hud.ShowMissionBanner("VOSS LOCATED // LOWER GANTRY, BENEATH THE MARKET");
            }
        }

        /// <summary>
        /// Opening atmosphere: the station notices Alita before Alita notices the station.
        /// </summary>
        private IEnumerator IntroStoryBeats()
        {
            var hud = BlackMarketNPCDialogueHUD.GetOrCreate();

            // Never talk over the arrival cinematic; wait for it to release the player
            yield return new WaitForSeconds(2.0f);
            var arrival = Object.FindAnyObjectByType<BlackMarketArrivalCutsceneDirector>();
            while (arrival != null && arrival.isPlaying)
            {
                yield return null;
                arrival = Object.FindAnyObjectByType<BlackMarketArrivalCutsceneDirector>();
            }

            yield return new WaitForSeconds(2.5f);
            if (currentState != BlackMarketStoryState.InvestigateMarket) yield break;

            hud.ShowTransmission(
                "DRIFT TRAFFIC CONTROL",
                "STATION // WELCOME PROTOCOL",
                "Unregistered Martian vessel, Pad B-04. You are cleared to disembark. Bounty registry updated. One new entry.",
                new Color(0.22f, 0.74f, 0.97f),
                7f
            );

            yield return new WaitForSeconds(8.5f);
            if (currentState != BlackMarketStoryState.InvestigateMarket) yield break;

            hud.ShowTransmission(
                "ALITA",
                "URM // INTERNAL LOG",
                "So that's how it is. Fine. Let them watch.",
                new Color(0.90f, 0.95f, 1.0f),
                5f
            );
        }

        private void FindPlayer()
        {
            var p = GameObject.FindGameObjectWithTag("Player") ?? GameObject.Find("Alita") ?? GameObject.Find("Alita_Hero") ?? GameObject.Find("Alita_BattleAngel_Hero");
            if (p != null) _playerTransform = p.transform;
        }

        private void SetupEmergencyLight()
        {
            if (_emergencyStrobeLight != null) return;
            GameObject lightGo = new GameObject("Emergency_Alarm_Strobe");
            lightGo.transform.position = new Vector3(16.0f, 6.0f, 28.0f);
            _emergencyStrobeLight = lightGo.AddComponent<Light>();
            _emergencyStrobeLight.type = LightType.Point;
            _emergencyStrobeLight.color = emergencyRedLightColor;
            _emergencyStrobeLight.intensity = 0f;
            _emergencyStrobeLight.range = 35f;
        }

        private void LoadAudioClipsIfMissing()
        {
#if UNITY_EDITOR
            if (explosionSFX == null)
            {
                explosionSFX = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/dragon-studio-dry-thunder-364468.mp3");
            }
            if (fallLandingSFX == null)
            {
                fallLandingSFX = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Combat/Jump_Landing_Boots_01.wav");
            }
            if (warpDriveSFX == null)
            {
                warpDriveSFX = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Combat/SciFi_Starship_Flyby_Pass.mp3");
            }
#endif
        }

        private void Update()
        {
            if (_playerTransform == null)
            {
                FindPlayer();
            }

            // Animate red emergency alarm strobe
            if (_isStrobeActive && _emergencyStrobeLight != null)
            {
                _strobeTimer += Time.deltaTime * 7.0f;
                _emergencyStrobeLight.intensity = Mathf.PingPong(_strobeTimer, 1.0f) * 7.5f;
            }

            // Monitor combat in Lower Trench
            if (currentState == BlackMarketStoryState.LowerTrenchCombat)
            {
                MonitorCombat();
            }

            // Monitor Escape to Starship
            if (currentState == BlackMarketStoryState.EscapeToStarship)
            {
                MonitorStarshipEscape();
            }
        }

        public void SetStoryState(BlackMarketStoryState newState)
        {
            currentState = newState;
            var hud = BlackMarketNPCDialogueHUD.GetOrCreate();

            switch (currentState)
            {
                case BlackMarketStoryState.InvestigateMarket:
                    hud.ShowMissionBanner("OBJECTIVE: Find the salvager who sold your core — Kaelen Voss");
                    if (CyberpunkHUDController.Instance != null)
                    {
                        CyberpunkHUDController.Instance.SetQuestObjective(
                            "THE SALVAGE MAN",
                            $"► Cross-reference the market's whispers about Voss ({LeadsFound}/{RequiredLeads})"
                        );
                    }
                    break;

                case BlackMarketStoryState.InfiltrateRefineryTask:
                    hud.ShowMissionBanner("OBJECTIVE: Infiltrate Refinery Depot & Hack Secure Terminal for Encrypted Data Core");
                    if (CyberpunkHUDController.Instance != null)
                    {
                        CyberpunkHUDController.Instance.SetQuestObjective(
                            "THE SALVAGE MAN",
                            "► Hack the Refinery Secure Terminal & extract the Data Core [E]"
                        );
                    }
                    break;

                case BlackMarketStoryState.ReturnToVoss:
                    hud.ShowMissionBanner("OBJECTIVE: Return to Kaelen Voss in Sector 4 Alley with the Data Core");
                    if (CyberpunkHUDController.Instance != null)
                    {
                        CyberpunkHUDController.Instance.SetQuestObjective(
                            "THE SALVAGE MAN",
                            "► Deliver the Data Core to Kaelen Voss [E]"
                        );
                    }
                    break;

                case BlackMarketStoryState.BetrayalAmbush:
                    StartCoroutine(ExecuteBetrayalSequence());
                    break;

                case BlackMarketStoryState.FleeAcrossCatwalk:
                    hud.ShowMissionBanner("🚨 SECTOR COMPROMISED! FLEE ACROSS THE UPPER GANTRY CATWALK!");
                    if (CyberpunkHUDController.Instance != null)
                    {
                        CyberpunkHUDController.Instance.SetQuestObjective(
                            "CRITICAL ESCAPE",
                            "► Run across Upper Gantry Catwalk to escape ambush!"
                        );
                        CyberpunkHUDController.Instance.ShowToast("WARNING: 10M BOUNTY ACTIVE // BOUNTY HUNTERS IN PURSUIT", 4.0f);
                    }
                    break;

                case BlackMarketStoryState.CatwalkCollapse_Fall:
                    // Handled by BlackMarketCatwalkFallTrigger
                    break;

                case BlackMarketStoryState.LowerTrenchCombat:
                    UpdateCombatHUD();
                    break;

                case BlackMarketStoryState.EscapeToStarship:
                    _isStrobeActive = true;
                    hud.ShowMissionBanner("🚨 PURSUIT NEUTRALIZED! SPRINT UP TO PAD B-04 & PRESS [R] TO WARP TO MARS!");
                    if (CyberpunkHUDController.Instance != null)
                    {
                        CyberpunkHUDController.Instance.SetQuestObjective(
                            "EMERGENCY EVAC",
                            "► Sprint to Landing Pad B-04 and board starship [R]"
                        );
                        CyberpunkHUDController.Instance.ShowToast("LANDING PAD CLEAR // HYPERSPACE WARP READY", 4.0f);
                    }
                    StartCoroutine(EscapeStoryTransmissions());
                    break;

                case BlackMarketStoryState.HyperspaceWarp:
                    StartCoroutine(ExecuteMarsHyperspaceJump());
                    break;
            }
        }

        // ==================== Quest Progression Callbacks ====================

        /// <summary>
        /// Called when Alita finishes first dialogue with Voss, accepting the task
        /// </summary>
        public void OnVossTaskAssigned()
        {
            SetStoryState(BlackMarketStoryState.InfiltrateRefineryTask);
        }

        /// <summary>
        /// Called when Alita interacts with and hacks the refinery terminal
        /// </summary>
        public void OnDataCoreRetrieved()
        {
            SetStoryState(BlackMarketStoryState.ReturnToVoss);
        }

        /// <summary>
        /// Called when Alita finishes return dialogue with Voss and double-cross is revealed
        /// </summary>
        public void OnVossBetrayalRevealed()
        {
            SetStoryState(BlackMarketStoryState.BetrayalAmbush);
        }

        private IEnumerator ExecuteBetrayalSequence()
        {
            _isStrobeActive = true;

            // 1. Play siren alarm
            if (alarmSirenSFX != null)
            {
                AudioSource.PlayClipAtPoint(alarmSirenSFX, transform.position, 1.0f);
            }

            var hud = BlackMarketNPCDialogueHUD.GetOrCreate();
            hud.ShowMissionBanner("🚨 DOUBLE-CROSS! 10 MILLION BOUNTY ON ALITA! SYNDICATE STRIKE FORCES INCOMING!");

            if (CyberpunkHUDController.Instance != null)
            {
                CyberpunkHUDController.Instance.ShowToast("ALARM: 10M CREDIT BOUNTY // TERMINATE ALITA", 3.5f);
            }

            yield return new WaitForSeconds(1.2f);

            // 2. Spawn Alley Hunters
            SpawnAlleyAmbushSquad();

            // 3. Switch to Chase State
            SetStoryState(BlackMarketStoryState.FleeAcrossCatwalk);

            // 4. Voss's confession lands while the hunters close in
            yield return new WaitForSeconds(2.6f);
            hud.ShowTransmission(
                "KAELEN VOSS",
                "COMMS // HIJACKED FREQUENCY",
                "Alita — the core. Shielded partition, under the manifest. The coordinates were always yours. I just needed you to make it out with them.",
                new Color(0.22f, 0.74f, 0.97f),
                7f
            );

            yield return new WaitForSeconds(8.0f);
            hud.ShowTransmission(
                "DRIFT CONTROL",
                "STATION // SECURITY",
                "Warning. Gantry supports severed on Sector 04's east span. All personnel clear the catwalk.",
                new Color(1.0f, 0.35f, 0.25f),
                5.5f
            );
        }

        private void SpawnAlleyAmbushSquad()
        {
            GameObject enemyPrefab = Resources.Load<GameObject>("BlackMarketNPCs/PF_SwordEnemy_CyberCity");

            for (int i = 0; i < alleyAmbushPoints.Length; i++)
            {
                Vector3 spawnPos = alleyAmbushPoints[i];
                Quaternion spawnRot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                GameObject enemy = null;
                if (enemyPrefab != null)
                {
                    enemy = Instantiate(enemyPrefab, spawnPos, spawnRot);
                    enemy.name = "Syndicate_Hunter_Alley_0" + (i + 1);
                }
                else
                {
                    var alt = Resources.Load<GameObject>("BlackMarketNPCs/NPC_Enforcer_01");
                    if (alt != null) enemy = Instantiate(alt, spawnPos, spawnRot);
                }

                if (enemy != null)
                {
                    _activeEnemies.Add(enemy);
                }
            }
            Debug.Log($"<color=#EF4444>[Bounty Ambush]</color> Spawned {alleyAmbushPoints.Length} bounty hunters in alley!");
        }

        /// <summary>
        /// Called when Alita crosses the catwalk and explosion detonates
        /// </summary>
        public void OnCatwalkExploded()
        {
            currentState = BlackMarketStoryState.CatwalkCollapse_Fall;
        }

        /// <summary>
        /// Called when Alita lands in the lower cargo trench
        /// </summary>
        public void OnCatwalkFallen()
        {
            // Spawn lower trench pursuers
            SpawnLowerTrenchAmbushSquad();
            SetStoryState(BlackMarketStoryState.LowerTrenchCombat);

            BlackMarketNPCDialogueHUD.GetOrCreate().ShowTransmission(
                "ALITA",
                "URM // INTERNAL LOG",
                "Six meters down. Loose gravel, one blade, and company. Fine. Let's finish this.",
                new Color(0.90f, 0.95f, 1.0f),
                5f
            );
        }

        private void SpawnLowerTrenchAmbushSquad()
        {
            _activeEnemies.Clear();
            GameObject enemyPrefab = Resources.Load<GameObject>("BlackMarketNPCs/PF_SwordEnemy_CyberCity");

            for (int i = 0; i < lowerTrenchAmbushPoints.Length; i++)
            {
                Vector3 spawnPos = lowerTrenchAmbushPoints[i];
                Quaternion spawnRot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                GameObject enemy = null;
                if (enemyPrefab != null)
                {
                    enemy = Instantiate(enemyPrefab, spawnPos, spawnRot);
                    enemy.name = "Syndicate_Hunter_Trench_0" + (i + 1);
                }
                else
                {
                    var alt = Resources.Load<GameObject>("BlackMarketNPCs/NPC_Enforcer_02");
                    if (alt != null) enemy = Instantiate(alt, spawnPos, spawnRot);
                }

                if (enemy != null)
                {
                    _activeEnemies.Add(enemy);
                }
            }

            _totalEnemiesSpawned = _activeEnemies.Count;
            Debug.Log($"<color=#EF4444>[Trench Ambush]</color> Spawned {_totalEnemiesSpawned} elite pursuers in lower cargo trench!");
        }

        private void MonitorCombat()
        {
            // Remove dead or inactive enemies
            _activeEnemies.RemoveAll(e => e == null || !e.activeInHierarchy || IsEnemyDead(e));

            UpdateCombatHUD();

            // All enemies eliminated
            if (_activeEnemies.Count == 0 && _totalEnemiesSpawned > 0)
            {
                SetStoryState(BlackMarketStoryState.EscapeToStarship);
            }
        }

        private bool IsEnemyDead(GameObject e)
        {
            if (e == null || !e.activeInHierarchy) return true;

            var swordAI = e.GetComponent<SwordEnemyAI>();
            if (swordAI != null && swordAI.IsDead) return true;

            return false;
        }

        private void UpdateCombatHUD()
        {
            int remaining = _activeEnemies.Count;
            if (CyberpunkHUDController.Instance != null)
            {
                CyberpunkHUDController.Instance.SetQuestObjective(
                    "LOWER TRENCH CLASH",
                    $"► Neutralize Pursuing Bounty Hunters ({remaining} Remaining)"
                );
            }
        }

        private void MonitorStarshipEscape()
        {
            Vector3 padPos = landingPadCenter != null ? landingPadCenter.position : new Vector3(22.8f, 1.88f, 33.5f);

            if (_playerTransform != null)
            {
                float distToPad = Vector3.Distance(_playerTransform.position, padPos);

                if (distToPad <= 7.0f)
                {
                    if (Input.GetKeyDown(KeyCode.R))
                    {
                        SetStoryState(BlackMarketStoryState.HyperspaceWarp);
                    }
                }
            }
        }

        /// <summary>
        /// The quiet after the fight: the station stands down, and Voss gets the last word.
        /// </summary>
        private IEnumerator EscapeStoryTransmissions()
        {
            var hud = BlackMarketNPCDialogueHUD.GetOrCreate();

            yield return new WaitForSeconds(1.4f);
            hud.ShowTransmission(
                "DRIFT CONTROL",
                "STATION // TRAFFIC",
                "All hunter signatures flatlined in Sector 04. Pad B-04 unlocked. You have ninety seconds before Omni-Corp reroutes a gunship.",
                new Color(0.22f, 0.74f, 0.97f),
                7f
            );

            yield return new WaitForSeconds(8.2f);
            hud.ShowTransmission(
                "KAELEN VOSS",
                "COMMS // DEAD MAN'S SWITCH",
                "Cydonia. Sub-level nine. And Alita... there are rows of them down there. Cores. Bodies. All wearing your face.",
                new Color(0.85f, 0.35f, 0.35f),
                8f
            );
        }

        private IEnumerator ExecuteMarsHyperspaceJump()
        {
            var hud = BlackMarketNPCDialogueHUD.GetOrCreate();
            hud.ShowTransmission(
                "ALITA",
                "URM // INTERNAL LOG",
                "I have the coordinates. I have a blade. Mars can keep the rest.",
                new Color(0.90f, 0.95f, 1.0f),
                5f
            );
            hud.ShowMissionBanner("🚀 WARP CORE DETONATING... JUMPING TO MARS CYDONIA VAULT!");

            if (CyberpunkHUDController.Instance != null)
            {
                CyberpunkHUDController.Instance.ShowToast("HYPERSPACE JUMP // TARGET: MARS", 4.0f);
            }

            if (warpDriveSFX != null && _playerTransform != null)
            {
                AudioSource.PlayClipAtPoint(warpDriveSFX, _playerTransform.position, 1.2f);
            }

            yield return new WaitForSeconds(2.5f);

            // Transition to Mars Scene if available
            string marsScene = "Scene_Mars_FinalBattle";
            if (Application.CanStreamedLevelBeLoaded(marsScene))
            {
                SceneManager.LoadScene(marsScene);
            }
            else
            {
                Debug.Log("<color=#38BDF8>[Story Transition]</color> Hyperspace jump completed! Target: Mars Cydonia Ruins.");
            }
        }

        private void OnGUI()
        {
            if (SyntyCombined.Cutscene.GlobalCutsceneState.IsCutsceneActive) return;
            if (currentState == BlackMarketStoryState.EscapeToStarship)
            {
                Vector3 padPos = landingPadCenter != null ? landingPadCenter.position : new Vector3(22.8f, 1.88f, 33.5f);
                if (_playerTransform != null)
                {
                    float dist = Vector3.Distance(_playerTransform.position, padPos);
                    if (dist <= 7.0f)
                    {
                        float width = 380f;
                        float height = 52f;
                        Rect rect = new Rect((Screen.width - width) * 0.5f, Screen.height * 0.72f, width, height);

                        Color prev = GUI.color;
                        GUI.color = new Color(0.04f, 0.08f, 0.16f, 0.94f);
                        GUI.Box(rect, GUIContent.none);

                        GUI.color = new Color(0.22f, 0.74f, 0.97f);
                        GUI.Box(new Rect(rect.x, rect.y, rect.width, 2.5f), GUIContent.none);

                        GUIStyle style = new GUIStyle(GUI.skin.label)
                        {
                            alignment = TextAnchor.MiddleCenter,
                            fontSize = 15,
                            fontStyle = FontStyle.Bold
                        };
                        style.normal.textColor = Color.white;
                        GUI.Label(rect, "<b>[R] INITIATE EMERGENCY WARP TO MARS</b>", style);
                        GUI.color = prev;
                    }
                }
            }
        }
    }
}
