using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using SyntyCombined.BlackMarket;
using SyntyCombined.Cutscene;
using SyntyCombined.World;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SyntyCombined.BlackMarket
{
    public enum BlackMarketStoryState
    {
        InvestigateMarket,       // Inquire with merchants to track down Voss
        InfiltrateRefineryTask,  // (unused) older refinery data-core hack; Voss now asks for the asteroid cores
        CollectCores,            // Fly the Nightshade out, tractor in the 4 core matrices, land on the Pod Pad
        ReturnToVoss,            // Deliver the cores back to Voss
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
        public SyndicateHoverTankAI siegeTankAI;

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
            else if (Instance != this)
            {
                Destroy(this);
                return;
            }

            // Cleanly deactivate any legacy conflicting missions in scene
            var legacyMission = Object.FindAnyObjectByType<SyntyCombined.World.BlackMarketCoreRetrievalMission>();
            if (legacyMission != null)
            {
                legacyMission.gameObject.SetActive(false);
                Debug.Log("<color=#38BDF8>[Story Director]</color> Deactivated legacy BlackMarketCoreRetrievalMission to ensure linear narrative.");
            }
            var legacyBetrayal = Object.FindAnyObjectByType<SyntyCombined.World.CollectorBetrayalManager>();
            if (legacyBetrayal != null)
            {
                legacyBetrayal.gameObject.SetActive(false);
                Debug.Log("<color=#38BDF8>[Story Director]</color> Deactivated legacy CollectorBetrayalManager.");
            }
        }

        private void Start()
        {
            FindPlayer();
            AutoBindSceneReferences();
            SetupEmergencyLight();
            LoadAudioClipsIfMissing();

            // Set initial state
            SetStoryState(BlackMarketStoryState.InvestigateMarket);

            StartCoroutine(IntroStoryBeats());
        }

        private void AutoBindSceneReferences()
        {
            if (fixerVossNPC == null)
            {
                var all = Object.FindObjectsByType<BlackMarketNPC>(FindObjectsSortMode.None);
                foreach (var n in all)
                {
                    if (n.isStoryObjectiveNPC || n.characterName.Contains("Voss") || n.name.Contains("Voss"))
                    {
                        fixerVossNPC = n;
                        break;
                    }
                }
            }
            if (refineryTerminal == null)
            {
                refineryTerminal = Object.FindAnyObjectByType<RefineryDataTerminal>();
            }
            if (catwalkFallTrigger == null)
            {
                catwalkFallTrigger = Object.FindAnyObjectByType<BlackMarketCatwalkFallTrigger>();
            }
            if (landingPadCenter == null)
            {
                var pad = GameObject.Find("SM_Bld_Corp_LandingPad_02") ?? GameObject.Find("LandingPad_B04") ?? GameObject.Find("Pad_B04");
                if (pad != null) landingPadCenter = pad.transform;
            }
            if (starshipTransform == null)
            {
                // Note: the "Vehicle" tag is not defined in this project (FindGameObjectWithTag would throw
                // and abort Start), so the ship is found by name only.
                var ship = GameObject.Find("SS-04_Nightshade_Striker") ?? GameObject.Find("Nightshade_Striker") ??
                           GameObject.Find("SS04_Nightshade_Striker") ?? GameObject.Find("Alita_Lost_Martian_Starship");
                if (ship != null) starshipTransform = ship.transform;
            }
            if (siegeTankAI == null)
            {
                siegeTankAI = Object.FindAnyObjectByType<SyndicateHoverTankAI>();
                if (siegeTankAI == null)
                {
                    siegeTankAI = EnsureSiegeTank();
                }
            }
        }

        public SyndicateHoverTankAI EnsureSiegeTank()
        {
            var existing = GameObject.Find("SM_Veh_HoverTank_03");
            if (existing != null)
            {
                var ai = existing.GetComponent<SyndicateHoverTankAI>() ?? existing.AddComponent<SyndicateHoverTankAI>();
                return ai;
            }

            GameObject prefab = null;
#if UNITY_EDITOR
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonSciFiWorlds/Prefabs/Props/Vehicles/SM_Veh_HoverTank_03.prefab");
#endif
            if (prefab == null) prefab = Resources.Load<GameObject>("SM_Veh_HoverTank_03");

            if (prefab != null)
            {
                Vector3 parkPos = new Vector3(-4.75f, -0.58f, -30.0f);
                Quaternion parkRot = Quaternion.Euler(0f, 0f, 0f);
                var tankGo = Instantiate(prefab, parkPos, parkRot);
                tankGo.name = "SM_Veh_HoverTank_03";
                var ai = tankGo.AddComponent<SyndicateHoverTankAI>();
                ai.garageParkedPos = parkPos;
                ai.combatDeployPos = new Vector3(-4.87f, -0.58f, -22.50f);
                ai.FindReferences();
                return ai;
            }
            return null;
        }

        // ==================== Landing pad ====================

        /// <summary>
        /// Where Alita has to stand to board for the escape: next to the Nightshade, which is parked on whichever
        /// pad it last landed on (the arrival pad at first, the Pod Pad after the core run). Falls back to the
        /// pad object's own centre. The pad prefab's pivot sits below the deck, so heights come from the floor.
        /// </summary>
        public Vector3 GetEscapePadPosition()
        {
            Vector3 fallback = new Vector3(22.8f, 1.88f, 33.5f);

            Vector3 centre;
            float topY;
            if (starshipTransform != null)
            {
                centre = starshipTransform.position;
                topY = centre.y + 1f;
            }
            else if (landingPadCenter != null)
            {
                var rends = landingPadCenter.GetComponentsInChildren<Renderer>();
                if (rends.Length == 0) return landingPadCenter.position;
                Bounds b = rends[0].bounds;
                foreach (var rd in rends) b.Encapsulate(rd.bounds);
                centre = b.center;
                topY = b.max.y;
            }
            else return fallback;

            float y = fallback.y;
            var hits = Physics.RaycastAll(new Vector3(centre.x, topY + 4f, centre.z), Vector3.down, 30f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity;
            foreach (var h in hits)
            {
                if (h.collider.GetComponentInParent<CharacterController>() != null) continue;
                if (starshipTransform != null && h.collider.transform.IsChildOf(starshipTransform)) continue;
                if (h.point.y > best && h.point.y < topY + 0.5f) best = h.point.y;
            }
            if (!float.IsNegativeInfinity(best)) y = best;

            return new Vector3(centre.x, y, centre.z);
        }

        private bool IsPlayerOnEscapePad()
        {
            if (_playerTransform == null) return false;
            Vector3 pad = GetEscapePadPosition();
            Vector3 p = _playerTransform.position;
            float horiz = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(pad.x, pad.z));
            return horiz <= 8.0f && Mathf.Abs(p.y - pad.y) <= 4.5f;
        }

        // ==================== Investigation Leads ====================

        public bool IsLeadFound(string leadId) => !string.IsNullOrEmpty(leadId) && _leadsFound.Contains(leadId);

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

        // Voss's job is the flight mission: hand over to the core-retrieval mission (kept inactive until now).
        private void StartCoreRetrievalMission()
        {
            var mission = FindCoreMission();
            if (mission == null)
            {
                Debug.LogWarning("[Story Director] No core-retrieval mission in the scene; skipping straight to Voss.");
                SetStoryState(BlackMarketStoryState.ReturnToVoss);
                return;
            }

            // Voss already briefed her, so the mission starts at the collecting stage
            mission.currentStage = SyntyCombined.World.CoreRetrievalStage.CollectingMatrices;
            mission.enabled = true;
            mission.gameObject.SetActive(true);
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

                case BlackMarketStoryState.CollectCores:
                    hud.ShowMissionBanner("OBJECTIVE: Board the Nightshade Striker and collect the 4 asteroid core matrices");
                    if (CyberpunkHUDController.Instance != null)
                    {
                        CyberpunkHUDController.Instance.SetQuestObjective(
                            "THE SALVAGE MAN",
                            "► Board the Nightshade Striker, fly out through the broken dome and tractor in the 4 core matrices [T]"
                        );
                    }
                    StartCoreRetrievalMission();
                    break;

                case BlackMarketStoryState.ReturnToVoss:
                    hud.ShowMissionBanner("OBJECTIVE: Bring the 4 core matrices to Kaelen Voss in his Sanctuary Pod");
                    if (CyberpunkHUDController.Instance != null)
                    {
                        CyberpunkHUDController.Instance.SetQuestObjective(
                            "THE SALVAGE MAN",
                            "► Deliver the core matrices to Kaelen Voss [E]"
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
                    // Handled by BlackMarketCatwalkFallTrigger; shut down upper-street tank laser
                    if (siegeTankAI != null && siegeTankAI.targetingLaser != null)
                    {
                        siegeTankAI.targetingLaser.enabled = false;
                    }
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
            SetStoryState(BlackMarketStoryState.CollectCores);
        }

        /// <summary>
        /// Called by the core-retrieval mission once all four core matrices are secured and the ship has landed.
        /// </summary>
        public void OnCoresSecured()
        {
            if (currentState != BlackMarketStoryState.CollectCores) return;

            var mission = FindCoreMission();
            if (mission != null) mission.enabled = false; // its job is done; the story director takes over

            SetStoryState(BlackMarketStoryState.ReturnToVoss);
        }

        private static SyntyCombined.World.BlackMarketCoreRetrievalMission FindCoreMission()
        {
            foreach (var m in Resources.FindObjectsOfTypeAll<SyntyCombined.World.BlackMarketCoreRetrievalMission>())
                if (m != null && m.gameObject.scene.IsValid()) return m;
            return null;
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

            // 2. Activate Syndicate Heavy Hover Tank & start shooting!
            if (siegeTankAI != null)
            {
                if (_playerTransform == null) FindPlayer();
                siegeTankAI.targetPlayer = _playerTransform;
                siegeTankAI.ActivateTank();
            }
            else
            {
                var tank = SyndicateHoverTankAI.Instance ?? Object.FindAnyObjectByType<SyndicateHoverTankAI>();
                if (tank != null)
                {
                    if (_playerTransform == null) FindPlayer();
                    tank.targetPlayer = _playerTransform;
                    tank.ActivateTank();
                }
            }

            yield return new WaitForSeconds(1.0f);

            // 3. Spawn Alley Hunters
            SpawnAlleyAmbushSquad();

            // 4. Switch to Chase State
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
                spawnPos.y = BlackMarketRuntimeSpawner.GetFloorY(spawnPos);
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
                spawnPos.y = BlackMarketRuntimeSpawner.GetFloorY(spawnPos);
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

            // All enemies eliminated (or none could be spawned, so the story never soft-locks)
            if (_activeEnemies.Count == 0)
            {
                if (_totalEnemiesSpawned <= 0)
                    Debug.LogWarning("[Story Director] No trench hunters were spawned (prefab missing?); advancing to escape.");
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
            if (IsPlayerOnEscapePad() && Input.GetKeyDown(KeyCode.R))
            {
                SetStoryState(BlackMarketStoryState.HyperspaceWarp);
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
                if (_playerTransform != null)
                {
                    if (IsPlayerOnEscapePad())
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
