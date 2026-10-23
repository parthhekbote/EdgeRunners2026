using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using InsaneGun.Combat;
using SyntyCombined;
using CyberCombat.Coordination;

namespace Cyberpunk.Missions
{
    /// <summary>
    /// Master Manager for MISSION 1 - "Street Trial" in Demo_City.
    /// Manages:
    /// 1. Player spawn point storage and universal controller binding (SyntySwordLocomotionController + InsaneGunSwordController).
    /// 2. Relocated Mission Zone to Second Skybridge (picks nearest bridge platform >80m from spawn, NavMesh + clearance verified).
    /// 3. Minimal aesthetic cyan marker (flat 2.4m Ground_HoloRing, 0.35m LightShaft, bobbing mission quad icon, point light).
    /// 4. Kiroshi AR GPS Ground Tracer (LineRenderer ribbon + pulsing chevrons leading across walkways to the bridge).
    /// 5. Dr. Ido modern 2025 glass holo-call popup with voice, ringtone, typewriter and mission dialog.
    /// 6. 3x Razor Punk combat bots (175 HP - exact 5 hits to kill, 11 dmg, 1.06x scale, dark carbon + glowing red eyes, triangle formation).
    /// 7. Intense Mission BGM (fade-in, ducking during calls, fade-out on complete/death) and combat SFX.
    /// 8. Two-way damage verification, objective tracking (0/3 -> 3/3), and player reboot sequence.
    /// </summary>
    public class Mission1_StreetTrialManager : MonoBehaviour
    {
        public static Mission1_StreetTrialManager Instance { get; private set; }

        public enum MissionState
        {
            Awakening,
            BriefingAtSpawn,
            TravelingToBridge,
            CombatInArena,
            Completed
        }

        [Header("Mission Flow & State")]
        public MissionState state = MissionState.Awakening;
        public int killCount = 0;
        public const int RequiredKills = 3;

        [Header("Player & Spawn Points")]
        public GameObject playerGameObject;
        public Transform playerTransform;
        public SyntySwordLocomotionController playerSynty;
        public InsaneGunSwordController playerInsaneGun;

        public Vector3 mission1SpawnPosition = new Vector3(19.00f, -17.98f, 49.50f);
        public Quaternion mission1SpawnRotation = Quaternion.Euler(0f, 0f, 0f);

        [Header("Mission Zone & Minimal Marker")]
        public GameObject mission1Zone;
        public Vector3 zoneCenterPosition = new Vector3(0.00f, 0.08f, -8.75f);
        public GameObject missionMarkerObj;

        [Header("Kiroshi GPS Ground Tracer")]
        public GameObject groundTracerObj;
        private LineRenderer _tracerLineRenderer;
        private List<GameObject> _tracerChevrons = new List<GameObject>();
        private NavMeshPath _tracerNavPath;
        private float _lastTracerUpdateTime = 0f;
        private Vector3 _lastPlayerTracerPos = Vector3.zero;
        private Material _tracerLineMaterial;
        private Material _tracerChevronMaterial;
        private Sprite _chevronSprite;

        [Header("Enemy Setup (Diverse Combat Squad)")]
        public GameObject enemyPrefab;
        public GameObject[] squadEnemyPrefabs;
        public bool enableRivalInfighting = true;
        public List<InsaneGunSwordController> activeEnemies = new List<InsaneGunSwordController>();
        public List<Mission1_EnemyTracker> enemyTrackers = new List<Mission1_EnemyTracker>();

        [Header("UI System Reference")]
        public Mission1_UIManager uiManager;

        // Private State
        private bool _hasTriggeredIntro = false;
        private bool _isRespawning = false;
        private bool _bridgeFightStarted = false;
        // True when BeginBridgeFight arrived via the prologue handoff while still
        // in Awakening: the Ido holo-call briefing is shown first, and arena
        // combat engages from OnGigAcceptedAtSpawn once the player accepts.
        private bool _pendingHandoffCombat = false;
        // Ido escort tracking (prologue walkthrough): while active, the AR trail
        // + waypoint HUD point at Ido instead of the bridge, and arena combat
        // cannot auto-engage (the handoff briefing + accept run first).
        private Transform _escortIdoTarget;
        private bool _escortActive;
        private bool _followShown;
        private float _lastWaitToastTime = -10f;

        /// <summary>
        /// Called by the prologue escort when Ido starts/finishes each walking leg.
        /// Idempotent. Switches the AR trail + waypoint HUD onto Ido and posts the
        /// FOLLOW objective. Never locks input.
        /// </summary>
        public void SetEscortTarget(Transform idoTransform)
        {
            if (idoTransform == null)
            {
                Debug.LogWarning("[Mission1] SetEscortTarget: null Ido transform; ignored.");
                return;
            }
            _escortIdoTarget = idoTransform;
            if (!_escortActive)
            {
                _escortActive = true;
                Debug.Log("[Mission1] Escort tracking ON: AR trail + waypoint now follow Dr. Ido to the bridge.");
            }
            if (CyberpunkHUDController.Instance != null)
            {
                CyberpunkHUDController.Instance.SetQuestObjective(
                    "// STAY WITH IDO",
                    "> Follow Ido on foot to the bridge\n> Stay within 8.5m or he waits for you");
            }
        }

        public void ClearEscortTarget()
        {
            if (!_escortActive && _escortIdoTarget == null) return;
            _escortActive = false;
            _escortIdoTarget = null;
            _followShown = false;
            if (uiManager != null) uiManager.ShowFollowPopup(false);
            Debug.Log("[Mission1] Escort tracking OFF: trail + waypoint revert to bridge.");
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (ShouldSuppressMission1())
            {
                Destroy(gameObject);
                return;
            }

            EnsureUIManager();
            EnsureAudioManager();
            Alita.AlitaCityPrologueDirector.PrologueHandoffEvent += OnPrologueHandoffEvent;
            Debug.Log("[Mission1] Awake: subscribed to prologue handoff event; UI/audio managers ensured.");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            IsMission1Completed = false;
            Instance = null;
            PlayerPrefs.DeleteKey("Mission1_Completed");
            PlayerPrefs.DeleteKey("Prologue_Completed");
            PlayerPrefs.DeleteKey("Prologue_Beat0_Completed");
            PlayerPrefs.DeleteKey("Mission3_Active");
        }

        public static bool IsMission1Completed = false;

        public static bool ShouldSuppressMission1()
        {
            return IsMission1Completed
                || Cyberpunk.World.PitArenaDoorTrigger.IsReturningFromPit
                || (Cyberpunk.World.PitArenaDoorTrigger.PendingSpawnPos.HasValue && Cyberpunk.World.PitArenaDoorTrigger.HasVisitedPit)
                || IdoAssassinationDirector.Instance != null
                || IdoAssassinationDirector.HasPlayed;
        }

        /// <summary>
        /// Runtime self-ensure for Demo_City: spawns the Mission 1 manager trio
        /// (Mission1_StreetTrialManager + Mission1_UIManager + Mission1_AudioManager)
        /// if the loaded Demo_City scene contains none. No .unity hand-edit required;
        /// Awake() wires the UI/audio managers onto the spawned manager.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureMission1InDemoCityRuntime()
        {
            SceneManager.sceneLoaded += OnMission1SceneLoadedRuntime;
            EnsureMission1TrioIfInDemoCity(SceneManager.GetActiveScene());
        }

        private static void OnMission1SceneLoadedRuntime(Scene scene, LoadSceneMode mode)
        {
            EnsureMission1TrioIfInDemoCity(scene);
        }

        public static void EnsureMission1TrioIfInDemoCity(Scene scene)
        {
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.name)) return;
            if (scene.name.IndexOf("Demo_City", System.StringComparison.OrdinalIgnoreCase) < 0) return;
            if (ShouldSuppressMission1())
            {
                Debug.Log($"[Mission1] Suppressed Mission 1 in '{scene.name}': post-prologue/pit state active.");
                return;
            }
            if (Instance != null || UnityEngine.Object.FindAnyObjectByType<Mission1_StreetTrialManager>() != null)
            {
                return;
            }

            GameObject trioGO = new GameObject("Mission1_StreetTrial_Runtime");
            trioGO.AddComponent<Mission1_StreetTrialManager>();
            Debug.Log($"[Mission1] Self-ensure: spawned Mission1_StreetTrialManager trio in '{scene.name}' (Awake wires UI/audio managers).");
        }

        private void Start()
        {
            StartCoroutine(InitSequenceRoutine());
        }

        [Header("Prologue Walkthrough (Runtime Only - Zero Map Changes)")]
        public bool enablePrologueWalkthrough = true;

        private IEnumerator InitSequenceRoutine()
        {
            if (ShouldSuppressMission1())
            {
                Debug.Log("[Mission1] InitSequenceRoutine: aborted because Mission 1 is already completed or post-Pit.");
                Destroy(gameObject);
                yield break;
            }

            state = MissionState.Awakening;

            // Wait 2 frames so CyberCityStartPointReceiver positions player at spawn
            yield return null;
            yield return null;

            if (ShouldSuppressMission1())
            {
                Debug.Log("[Mission1] InitSequenceRoutine: aborted after wait because ShouldSuppressMission1() is true.");
                Destroy(gameObject);
                yield break;
            }

            InitializePlayerAndSpawn();
            SetupMissionZone();
            SetupEnemies();

            // Runtime-Only City Walkthrough Prologue with Dr. Ido (Zero map changes)
            if (enablePrologueWalkthrough)
            {
                if (Alita.AlitaCityPrologueDirector.Instance == null)
                {
                    GameObject dirGO = new GameObject("Alita_City_Prologue_Runtime");
                    dirGO.AddComponent<Alita.AlitaCityPrologueDirector>();
                    Debug.Log("[Mission1] Spawned in-memory AlitaCityPrologueDirector for runtime walkthrough!");
                }
                else
                {
                    Debug.Log("[Mission1] Found existing AlitaCityPrologueDirector; awaiting its handoff.");
                }

                // Beat4 (bridge arrival) MUST equal the final zone: the handoff fires
                // only when Ido physically reaches the arena the bots spawn on.
                SyncBeat4ToZone();

                // Wait (unscaled) for the prologue to reach HandoffToCombat/Completed.
                // The prologue handoff drives BeginBridgeFight() itself (direct call +
                // static event); the drive below only fires if the handoff has not
                // already engaged combat.
                Debug.Log("[Mission1] Awaiting prologue HandoffToCombat/Completed...");
                const float kPrologueHandoffTimeout = 600f;
                float handoffWaitStart = Time.unscaledTime;
                while (true)
                {
                    if (_bridgeFightStarted)
                    {
                        Debug.Log("[Mission1] Bridge fight already started via handoff event; exiting wait loop.");
                        break;
                    }
                    var prologue = Alita.AlitaCityPrologueDirector.Instance;
                    // Wait for Completed (not HandoffToCombat): the prologue plays Ido's
                    // 6s rogue-bots briefing first, then fires BeginBridgeFight itself
                    // (direct call + event). This drive is the timeout/poll backup only.
                    if (prologue != null &&
                        prologue.currentPhase == Alita.AlitaCityPrologueDirector.ProloguePhase.Completed)
                    {
                        Debug.Log($"[Mission1] Prologue handoff detected (phase={prologue.currentPhase}).");
                        break;
                    }
                    if (Time.unscaledTime - handoffWaitStart > kPrologueHandoffTimeout)
                    {
                        Debug.LogWarning("[Mission1] Prologue handoff wait timed out; forcing combat handoff.");
                        break;
                    }
                    yield return null;
                }

                if (state == MissionState.CombatInArena || state == MissionState.Completed)
                {
                    Debug.Log($"[Mission1] Combat already engaged via prologue handoff (state={state}); no further action.");
                    yield break;
                }
                Debug.Log("[Mission1] Prologue wait over; converging on BeginBridgeFight().");
                BeginBridgeFight();
                yield break;
            }

            // Fallback: Wait 7.0s into the game before Dr. Ido's standalone holo-call pops up
            yield return new WaitForSeconds(7.0f);

            TriggerBriefingAtSpawn();
        }

        private void Update()
        {
            // Only the post-Mission-4 assassination finale retires a running Mission 1. Do NOT use
            // ShouldSuppressMission1() here: it includes IsMission1Completed, which becomes true when
            // Mission 1 finishes and would destroy this manager before the Mission 2 handoff runs.
            if (IdoAssassinationDirector.HasPlayed || IdoAssassinationDirector.Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            // Fallback check: if player reference was lost, re-acquire
            if (playerTransform == null && playerGameObject != null)
            {
                playerTransform = playerGameObject.transform;
            }
            if (playerTransform == null) return;

            // Health watchdog: if player dies during combat, trigger death sequence immediately
            if (!_isRespawning && state == MissionState.CombatInArena)
            {
                bool isDead = false;
                if (playerSynty != null && playerSynty.currentHealth <= 0f) isDead = true;
                if (playerInsaneGun != null && (playerInsaneGun.currentHealth <= 0f || playerInsaneGun.IsDead)) isDead = true;
                if (isDead)
                {
                    OnPlayerDiedInternal();
                    return;
                }

                // Drive relentless AI aggression and coordinated sword attacks matching Blackwall Rogue AI
                DriveActiveAggression();
            }

            // Escort guidance: while the prologue walk is active, Ido is the target.
            // (Suppressed once the handoff starts/finishes — no nagging mid-ambush.)
            var prologue = Alita.AlitaCityPrologueDirector.Instance;
            bool handoffDone = prologue != null &&
                (prologue.currentPhase == Alita.AlitaCityPrologueDirector.ProloguePhase.HandoffToCombat ||
                 prologue.currentPhase == Alita.AlitaCityPrologueDirector.ProloguePhase.Completed);
            bool escortGuiding = _escortActive && _escortIdoTarget != null && state == MissionState.Awakening && !handoffDone;
            Vector3 guideTarget = escortGuiding ? _escortIdoTarget.position : zoneCenterPosition;

            // Waypoint HUD: while traveling post-accept, plus following Ido himself
            // during the escort walk (STAY WITH IDO).
            if (uiManager != null)
            {
                bool showWaypoint = (state == MissionState.TravelingToBridge) || escortGuiding;
                uiManager.UpdateWaypointHUD(guideTarget, showWaypoint, escortGuiding ? "DR. IDO" : null);
            }

            // Escort follow pill: STAY WITH IDO within 7m — Ido waits for you
            // (prologue side), pill + throttled toast keep you honest (M1 side).
            if (escortGuiding && _escortIdoTarget != null)
            {
                float idoGap = Vector3.Distance(playerTransform.position, _escortIdoTarget.position);
                if (uiManager != null)
                {
                    if (!_followShown) { uiManager.ShowFollowPopup(true); _followShown = true; }
                    uiManager.UpdateFollowDistance(idoGap, 8.5f);
                }
                if (idoGap > 8.5f && Time.time - _lastWaitToastTime > 8f)
                {
                    _lastWaitToastTime = Time.time;
                    Debug.Log($"[Mission1] Escort gap {idoGap:F1}m > 8.5m; Ido waits — catch up.");
                    if (CyberpunkHUDController.Instance != null)
                        CyberpunkHUDController.Instance.ShowToast("STAY WITH IDO // HE WAITS FOR YOU", 2.5f);
                }
            }

            // Player sprinted to the bridge early without Ido: teleport Ido in beside
            // you and start the handoff instead of stranding either of you.
            if (escortGuiding && playerTransform != null)
            {
                float distEarly = Vector3.Distance(playerTransform.position, zoneCenterPosition);
                if (distEarly <= 12.0f)
                {
                    float idoToZone = (prologue != null && prologue.doctorTransform != null)
                        ? Vector3.Distance(prologue.doctorTransform.position, zoneCenterPosition)
                        : float.MaxValue;
                    if (prologue != null && prologue.doctorTransform != null && idoToZone > prologue.waypointRadius * 2f)
                    {
                        Vector3 toZone = zoneCenterPosition - playerTransform.position;
                        toZone.y = 0f;
                        Vector3 idoSpot = playerTransform.position +
                            (toZone.sqrMagnitude > 0.01f ? toZone.normalized * 2.0f : new Vector3(1.5f, 0f, 0f));
                        Debug.Log($"[Mission1] Player rushed to bridge ({distEarly:F1}m); Ido was {idoToZone:F1}m out — teleporting him in.");
                        if (CyberpunkHUDController.Instance != null)
                            CyberpunkHUDController.Instance.ShowToast("IDO INBOUND // GUIDE CATCHING UP", 3.0f);
                        prologue.TeleportDoctorTo(idoSpot);
                        prologue.TriggerCombatHandoff();
                    }
                    else if (Time.time - _lastWaitToastTime > 5.0f)
                    {
                        _lastWaitToastTime = Time.time;
                        Debug.Log($"[Mission1] Player at bridge ({distEarly:F1}m); Ido nearly here — holding for arrival.");
                        if (CyberpunkHUDController.Instance != null)
                            CyberpunkHUDController.Instance.ShowToast("WAIT FOR IDO // STAY WITH YOUR GUIDE", 3.0f);
                    }
                }
            }

            // Update Kiroshi GPS Ground Tracer path and chevrons (post-accept travel
            // only — the escort needs no trail UI since Ido leads on foot).
            if (state == MissionState.TravelingToBridge && !_isRespawning)
            {
                UpdateGroundTracer();

                // Proximity check: arriving at destination bridge arena triggers combat!
                // (Only after the handoff briefing accept — never mid-escort.)
                if (state == MissionState.TravelingToBridge)
                {
                    float distToBridge = Vector3.Distance(playerTransform.position, zoneCenterPosition);
                    if (distToBridge <= 8.0f)
                    {
                        EnterBridgeArenaCombat();
                    }
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Alita.AlitaCityPrologueDirector.PrologueHandoffEvent -= OnPrologueHandoffEvent;

            UnbindPlayerCallbacks();
        }

        private void EnsureUIManager()
        {
            if (uiManager == null)
            {
                uiManager = GetComponentInChildren<Mission1_UIManager>()
                         ?? UnityEngine.Object.FindAnyObjectByType<Mission1_UIManager>();
            }

            if (uiManager == null)
            {
                GameObject uiGo = new GameObject("Mission1_UIManager");
                uiGo.transform.SetParent(transform);
                uiManager = uiGo.AddComponent<Mission1_UIManager>();
            }
        }

        private void EnsureAudioManager()
        {
            var audioMgr = GetComponent<Mission1_AudioManager>()
                        ?? UnityEngine.Object.FindAnyObjectByType<Mission1_AudioManager>();
            if (audioMgr == null)
            {
                gameObject.AddComponent<Mission1_AudioManager>();
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // 1. UNIVERSAL PLAYER SETUP & SPONSOR BINDING
        // ─────────────────────────────────────────────────────────────────────
        private void InitializePlayerAndSpawn()
        {
            // 1. Try finding Synty player first
            playerSynty = UnityEngine.Object.FindAnyObjectByType<SyntySwordLocomotionController>();
            if (playerSynty != null)
            {
                playerGameObject = playerSynty.gameObject;
                playerTransform = playerSynty.transform;
            }

            // 2. Try finding InsaneGun combat controller
            if (playerGameObject == null)
            {
                var combatControllers = UnityEngine.Object.FindObjectsByType<InsaneGunSwordController>();
                foreach (var ctrl in combatControllers)
                {
                    if (ctrl.controlMode == InsaneGunSwordController.ControlMode.PlayerControlled)
                    {
                        playerInsaneGun = ctrl;
                        playerGameObject = ctrl.gameObject;
                        playerTransform = ctrl.transform;
                        break;
                    }
                }
            }
            else
            {
                playerInsaneGun = playerGameObject.GetComponent<InsaneGunSwordController>();
            }

            // 3. Fallback to tag "Player"
            if (playerGameObject == null)
            {
                playerGameObject = GameObject.FindWithTag("Player");
                if (playerGameObject != null) playerTransform = playerGameObject.transform;
            }

            if (playerGameObject != null)
            {
                float distToBridge = Vector3.Distance(playerGameObject.transform.position, zoneCenterPosition);
                if (distToBridge > 25f)
                {
                    mission1SpawnPosition = playerGameObject.transform.position;
                    mission1SpawnRotation = playerGameObject.transform.rotation;
                }
                BindPlayerCallbacks();
                Debug.Log($"[Mission1] Bound to Player '{playerGameObject.name}' at {mission1SpawnPosition} (distToBridge: {distToBridge:F1}m)");
            }
            else
            {
                Debug.LogWarning("[Mission1] Player object not found at Start, using default plaza spawn.");
            }
        }

        private void BindPlayerCallbacks()
        {
            if (playerSynty != null)
            {
                playerSynty.maxHealth = 300f;
                playerSynty.currentHealth = 300f;
                playerSynty.OnDamaged += OnSyntyPlayerDamaged;
                playerSynty.OnDeath += OnSyntyPlayerDied;
            }

            if (playerInsaneGun != null)
            {
                playerInsaneGun.maxHealth = 300f;
                playerInsaneGun.currentHealth = 300f;
                playerInsaneGun.OnDamaged += OnInsanePlayerDamaged;
                playerInsaneGun.OnDeath += OnInsanePlayerDied;
            }
        }

        private void UnbindPlayerCallbacks()
        {
            if (playerSynty != null)
            {
                playerSynty.OnDamaged -= OnSyntyPlayerDamaged;
                playerSynty.OnDeath -= OnSyntyPlayerDied;
            }

            if (playerInsaneGun != null)
            {
                playerInsaneGun.OnDamaged -= OnInsanePlayerDamaged;
                playerInsaneGun.OnDeath -= OnInsanePlayerDied;
            }
        }

        private void OnSyntyPlayerDamaged(SyntySwordLocomotionController hero, float dmg)
        {
            OnPlayerDamagedInternal(dmg);
        }

        private void OnSyntyPlayerDied(SyntySwordLocomotionController hero)
        {
            OnPlayerDiedInternal();
        }

        private void OnInsanePlayerDamaged(InsaneGunSwordController hero, float dmg)
        {
            OnPlayerDamagedInternal(dmg);
        }

        private void OnInsanePlayerDied(InsaneGunSwordController hero)
        {
            OnPlayerDiedInternal();
        }

        public void LockPlayerInput(bool locked)
        {
            if (playerSynty != null) playerSynty.inputLocked = locked;
            if (playerInsaneGun != null) playerInsaneGun.inputLocked = locked;
        }

        // ─────────────────────────────────────────────────────────────────────
        // 2. DYNAMIC BRIDGE SEARCH & MINIMAL CYAN MARKER (TASK 1)
        // ─────────────────────────────────────────────────────────────────────
        private void SetupMissionZone()
        {
            if (mission1Zone != null) return;

            // Find all Transforms with "Bridge" in name (also matching "Grating" for resilience)
            var allTransforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
            var bridgeCandidates = new List<Transform>();

            foreach (var t in allTransforms)
            {
                if (t == null) continue;
                bool isBridge = t.name.IndexOf("Bridge", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                t.name.IndexOf("Grating", System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (!isBridge) continue;

                // Exclude any within 40m of spawn bridge
                float dist = Vector3.Distance(mission1SpawnPosition, t.position);
                if (dist <= 40f) continue;

                bridgeCandidates.Add(t);
            }

            // Filter candidates: NavMesh sampled within 4m, clear of walls within 7m, flat (normal.y > 0.9)
            var validBridges = new List<(Vector3 pos, float dist)>();
            foreach (var candidate in bridgeCandidates)
            {
                if (NavMesh.SamplePosition(candidate.position, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                {
                    // Check clear of walls
                    var nearbyColliders = Physics.OverlapSphere(hit.position + Vector3.up * 1f, 7f);
                    bool hasWall = false;
                    foreach (var c in nearbyColliders)
                    {
                        if (c.gameObject.name.IndexOf("wall", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            hasWall = true;
                            break;
                        }
                    }

                    if (!hasWall && Physics.Raycast(hit.position + Vector3.up * 2f, Vector3.down, out RaycastHit rHit, 5f))
                    {
                        if (rHit.normal.y > 0.9f)
                        {
                            validBridges.Add((hit.position, Vector3.Distance(mission1SpawnPosition, hit.position)));
                        }
                    }
                }
            }

            // Pick bridge platform: prefer > 80m away per spec; fallback to furthest > 40m away if none > 80m
            Vector3 pickedBridgePos = new Vector3(0.00f, 0.08f, -9.33f); // Second skybridge default
            if (validBridges.Count > 0)
            {
                var farCandidates = validBridges.Where(v => v.dist >= 80f).ToList();
                if (farCandidates.Count > 0)
                {
                    pickedBridgePos = farCandidates.OrderBy(v => v.dist).First().pos;
                }
                else
                {
                    // Pick nearest to 80m away from valid candidates
                    pickedBridgePos = validBridges.OrderBy(v => Mathf.Abs(v.dist - 80f)).First().pos;
                }
            }

            // Mid-deck snapped to NavMesh + 0.1m
            if (NavMesh.SamplePosition(pickedBridgePos, out NavMeshHit finalHit, 4f, NavMesh.AllAreas))
            {
                pickedBridgePos = finalHit.position;
            }
            if (Physics.Raycast(pickedBridgePos + Vector3.up * 3f, Vector3.down, out RaycastHit gHit, 6f))
            {
                pickedBridgePos.y = gHit.point.y + 0.10f;
            }
            else
            {
                pickedBridgePos.y += 0.10f;
            }

            zoneCenterPosition = pickedBridgePos;
            Debug.Log($"[Mission1] Bridge candidates found: {bridgeCandidates.Count}, picked bridge at {zoneCenterPosition}");

            // Create Mission Zone Trigger on the Skybridge
            mission1Zone = new GameObject("Mission1_Zone");
            mission1Zone.transform.position = zoneCenterPosition;
            mission1Zone.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            BoxCollider col = mission1Zone.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(8f, 4f, 4f);
            col.center = new Vector3(0f, 2f, 0f);

            var triggerReceiver = mission1Zone.AddComponent<Mission1_ZoneTrigger>();
            triggerReceiver.manager = this;

            // ─── Minimal Aesthetic Cyan Marker (Task 1.3) ──────────────────────
            CreateMissionMarker();

            // Initialize Kiroshi GPS Ground Tracer from player to bridge
            InitGroundTracer();
        }

        public void CreateMissionMarker()
        {
            if (missionMarkerObj != null)
            {
                Destroy(missionMarkerObj);
                missionMarkerObj = null;
            }

            if (mission1Zone == null) return;

            missionMarkerObj = new GameObject("Mission1_Marker");
            missionMarkerObj.transform.SetParent(mission1Zone.transform, false);
            missionMarkerObj.transform.localPosition = Vector3.zero;

            Color kCyan = new Color(0.000f, 0.941f, 1.000f, 1.0f); // #00F0FF
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit")
                              ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                              ?? Shader.Find("Unlit/Color");

            // 1. Ground_HoloRing: 2.4m diameter, height 0.02m at y+0.04m, alpha 0.55
            GameObject holoRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            holoRing.name = "Ground_HoloRing";
            holoRing.transform.SetParent(missionMarkerObj.transform, false);
            holoRing.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            holoRing.transform.localScale = new Vector3(2.4f, 0.015f, 2.4f);
            Destroy(holoRing.GetComponent<Collider>());

            var ringMat = new Material(unlitShader);
            Color ringColor = new Color(0f, 0.941f, 1f, 0.55f);
            ringMat.color = ringColor;
            if (ringMat.HasProperty("_BaseColor")) ringMat.SetColor("_BaseColor", ringColor);
            holoRing.GetComponent<Renderer>().sharedMaterial = ringMat;

            var pulser = holoRing.AddComponent<BeaconPulseAnimator>();
            pulser.pulseSpeed = 4.18f;
            pulser.scaleMagnitude = 0.04f;

            // 2. LightShaft: 0.35m diameter, 6m tall at y+3m, alpha 0.18
            GameObject lightShaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            lightShaft.name = "LightShaft";
            lightShaft.transform.SetParent(missionMarkerObj.transform, false);
            lightShaft.transform.localPosition = new Vector3(0f, 3.0f, 0f);
            lightShaft.transform.localScale = new Vector3(0.35f, 3.0f, 0.35f);
            Destroy(lightShaft.GetComponent<Collider>());

            var shaftMat = new Material(unlitShader);
            Color shaftColor = new Color(0f, 0.941f, 1f, 0.18f);
            shaftMat.color = shaftColor;
            if (shaftMat.HasProperty("_BaseColor")) shaftMat.SetColor("_BaseColor", shaftColor);
            lightShaft.GetComponent<Renderer>().sharedMaterial = shaftMat;

            // 3. Icon: Quad 0.7x0.7m at y+1.8m with SPR_SciFiMenus_Icon_Missions_01.png bobbing, billboard to camera
            GameObject iconQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            iconQuad.name = "Icon_MissionQuad";
            iconQuad.transform.SetParent(missionMarkerObj.transform, false);
            iconQuad.transform.localPosition = new Vector3(0f, 1.8f, 0f);
            iconQuad.transform.localScale = new Vector3(0.7f, 0.7f, 1.0f);
            Destroy(iconQuad.GetComponent<Collider>());

            Sprite iconSprite = null;
#if UNITY_EDITOR
            iconSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceSciFiMenus/Sprites/Icons_SciFiMenus/SPR_SciFiMenus_Icon_Missions_01.png");
#endif
            var iconMat = new Material(Shader.Find("Sprites/Default") ?? unlitShader);
            if (iconSprite != null) iconMat.mainTexture = iconSprite.texture;
            iconMat.color = kCyan;
            iconQuad.GetComponent<Renderer>().sharedMaterial = iconMat;
            iconQuad.AddComponent<MissionIconBillboardBob>();

            // 4. PointLight: cyan #00F0FF, intensity 4, range 8, at y+2m
            GameObject lightObj = new GameObject("Marker_PointLight");
            lightObj.transform.SetParent(missionMarkerObj.transform, false);
            lightObj.transform.localPosition = new Vector3(0f, 2.0f, 0f);
            Light pLight = lightObj.AddComponent<Light>();
            pLight.type = LightType.Point;
            pLight.color = kCyan;
            pLight.intensity = 4.0f;
            pLight.range = 8.0f;
        }

        // ─── KIROSHI AR GPS GROUND TRACER ────────────────────────────────────
        private void InitGroundTracer()
        {
            if (groundTracerObj != null) Destroy(groundTracerObj);
            _tracerChevrons.Clear();

            _tracerNavPath = new NavMeshPath();
            groundTracerObj = new GameObject("Mission1_KiroshiGroundTracer");
            groundTracerObj.transform.SetParent(transform, false);
            groundTracerObj.SetActive(false); // hidden until the gig is accepted (no trail UI during the Ido escort)

            _tracerLineRenderer = groundTracerObj.AddComponent<LineRenderer>();
            _tracerLineRenderer.startWidth = 0.45f;
            _tracerLineRenderer.endWidth = 0.45f;
            _tracerLineRenderer.positionCount = 0;
            _tracerLineRenderer.useWorldSpace = true;
            _tracerLineRenderer.textureMode = LineTextureMode.Tile;
            _tracerLineRenderer.alignment = LineAlignment.TransformZ;
            _tracerLineRenderer.numCapVertices = 4;
            _tracerLineRenderer.numCornerVertices = 4;
            _tracerLineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _tracerLineRenderer.receiveShadows = false;

            Shader pShader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                          ?? Shader.Find("Universal Render Pipeline/Unlit")
                          ?? Shader.Find("Sprites/Default");

            _tracerLineMaterial = new Material(pShader);
            Color cCyan = new Color(0f, 0.941f, 1f, 0.85f);
            _tracerLineMaterial.color = cCyan;
            if (_tracerLineMaterial.HasProperty("_BaseColor")) _tracerLineMaterial.SetColor("_BaseColor", cCyan);
            if (_tracerLineMaterial.HasProperty("_EmissionColor"))
            {
                _tracerLineMaterial.EnableKeyword("_EMISSION");
                _tracerLineMaterial.SetColor("_EmissionColor", cCyan * 2.5f);
            }
            _tracerLineRenderer.sharedMaterial = _tracerLineMaterial;

#if UNITY_EDITOR
            _chevronSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceSciFiSoldierHUD/Sprites/HUD/SPR_HUD_SciFi_Arrow_Clean.png");
#endif
            _tracerChevronMaterial = new Material(Shader.Find("Sprites/Default") ?? pShader);
            if (_chevronSprite != null) _tracerChevronMaterial.mainTexture = _chevronSprite.texture;
            _tracerChevronMaterial.color = cCyan;
        }

        private void UpdateGroundTracer()
        {
            var prologue = Alita.AlitaCityPrologueDirector.Instance;
            bool handoffDone = prologue != null &&
                (prologue.currentPhase == Alita.AlitaCityPrologueDirector.ProloguePhase.HandoffToCombat ||
                 prologue.currentPhase == Alita.AlitaCityPrologueDirector.ProloguePhase.Completed);
            bool escortGuiding = !handoffDone && _escortActive && _escortIdoTarget != null && state == MissionState.Awakening;
            if ((state != MissionState.TravelingToBridge) || groundTracerObj == null || playerTransform == null)
            {
                if (groundTracerObj != null && groundTracerObj.activeSelf)
                {
                    groundTracerObj.SetActive(false);
                }
                return;
            }

            // Hard guard: a null native path throws inside CalculatePath every frame.
            // Rebuild (with a one-time log) instead of spamming 500+ exceptions.
            if (_tracerNavPath == null)
            {
                Debug.LogWarning("[Mission1] Tracer path missing at update; rebuilding on the spot.");
                InitGroundTracer();
                if (_tracerNavPath == null || groundTracerObj == null) return;
            }

            if (!groundTracerObj.activeSelf) groundTracerObj.SetActive(true);

            Vector3 pPos = playerTransform.position;

            // Throttle path calculation: recalculate every 0.15s or when moved > 0.3m
            if (Time.time - _lastTracerUpdateTime > 0.15f || Vector3.Distance(pPos, _lastPlayerTracerPos) > 0.3f)
            {
                _lastTracerUpdateTime = Time.time;
                _lastPlayerTracerPos = pPos;

                Vector3 startNavPos = pPos;
                if (NavMesh.SamplePosition(pPos, out NavMeshHit sHit, 4f, NavMesh.AllAreas))
                {
                    startNavPos = sHit.position;
                }

                Vector3 endNavPos = escortGuiding && _escortIdoTarget != null
                    ? _escortIdoTarget.position
                    : zoneCenterPosition;
                if (NavMesh.SamplePosition(zoneCenterPosition, out NavMeshHit eHit, 4f, NavMesh.AllAreas))
                {
                    endNavPos = eHit.position;
                }

                bool hasPath = false;
                try
                {
                    hasPath = NavMesh.CalculatePath(startNavPos, endNavPos, NavMesh.AllAreas, _tracerNavPath);
                }
                catch
                {
                    _tracerNavPath = new NavMeshPath();
                    try { hasPath = NavMesh.CalculatePath(startNavPos, endNavPos, NavMesh.AllAreas, _tracerNavPath); } catch {}
                }
                if (hasPath && _tracerNavPath.status != NavMeshPathStatus.PathInvalid && _tracerNavPath.corners.Length >= 2)
                {
                    Vector3[] corners = _tracerNavPath.corners;
                    Vector3[] elevated = new Vector3[corners.Length];
                    for (int i = 0; i < corners.Length; i++)
                    {
                        elevated[i] = corners[i] + Vector3.up * 0.14f;
                    }
                    _tracerLineRenderer.positionCount = elevated.Length;
                    _tracerLineRenderer.SetPositions(elevated);

                    UpdateChevronsAlongCorners(corners);
                }
                else
                {
                    Vector3[] direct = new Vector3[] { pPos + Vector3.up * 0.14f, zoneCenterPosition + Vector3.up * 0.14f };
                    _tracerLineRenderer.positionCount = 2;
                    _tracerLineRenderer.SetPositions(direct);
                    UpdateChevronsAlongCorners(direct);
                }
            }
        }

        private void UpdateChevronsAlongCorners(Vector3[] corners)
        {
            float totalLen = 0f;
            for (int i = 0; i < corners.Length - 1; i++)
            {
                totalLen += Vector3.Distance(corners[i], corners[i + 1]);
            }

            float interval = 2.2f;
            var pts = new List<Vector3>();
            var dirs = new List<Vector3>();

            float accumulated = 0f;
            float nextTarget = 0.5f;

            for (int i = 0; i < corners.Length - 1; i++)
            {
                Vector3 p0 = corners[i];
                Vector3 p1 = corners[i + 1];
                float segLen = Vector3.Distance(p0, p1);
                if (segLen < 0.01f) continue;
                Vector3 segDir = (p1 - p0).normalized;

                while (accumulated + segLen >= nextTarget)
                {
                    float remaining = nextTarget - accumulated;
                    Vector3 samplePt = p0 + segDir * remaining;
                    pts.Add(samplePt);
                    dirs.Add(segDir);
                    nextTarget += interval;
                }
                accumulated += segLen;
            }

            for (int i = 0; i < pts.Count; i++)
            {
                GameObject chevron;
                if (i < _tracerChevrons.Count)
                {
                    chevron = _tracerChevrons[i];
                    if (chevron == null)
                    {
                        chevron = CreateChevronObject();
                        _tracerChevrons[i] = chevron;
                    }
                }
                else
                {
                    chevron = CreateChevronObject();
                    _tracerChevrons.Add(chevron);
                }

                chevron.SetActive(true);
                chevron.transform.position = pts[i] + Vector3.up * 0.14f;

                float yaw = Mathf.Atan2(dirs[i].x, dirs[i].z) * Mathf.Rad2Deg;
                chevron.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
            }

            for (int i = pts.Count; i < _tracerChevrons.Count; i++)
            {
                if (_tracerChevrons[i] != null) _tracerChevrons[i].SetActive(false);
            }
        }

        private GameObject CreateChevronObject()
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Tracer_Chevron";
            quad.transform.SetParent(groundTracerObj.transform, false);
            quad.transform.localScale = new Vector3(0.65f, 0.65f, 1.0f);
            Destroy(quad.GetComponent<Collider>());

            var rend = quad.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.sharedMaterial = _tracerChevronMaterial;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rend.receiveShadows = false;
            }
            return quad;
        }

        // ─────────────────────────────────────────────────────────────────────
        // 3. ENEMY SETUP: RAZOR PUNKS IN TRIANGLE FORMATION (TASK 2)
        // ─────────────────────────────────────────────────────────────────────
        private void SetupEnemies()
        {
            if (ShouldSuppressMission1()) return;
            if (activeEnemies.Count > 0) return;

            // Wire trial protection: pre-combat M1 bots whiff instead of taking damage.
            InsaneGunSwordController.IsTrialProtected = IsBotTrialProtected;

            // Clean up any stale pre-existing scene instances
            var existingTrackers = UnityEngine.Object.FindObjectsByType<Mission1_EnemyTracker>();
            foreach (var et in existingTrackers)
            {
                if (et != null && !enemyTrackers.Contains(et))
                {
                    Destroy(et.gameObject);
                }
            }

            // Triangle offsets (-3.5, 0, 0), (3.5, 0, 0), (0, 0, 4.5) around zone center
            Vector3[] triangleOffsets = new Vector3[]
            {
                new Vector3(-3.5f, 0f, 0f),
                new Vector3(3.5f, 0f, 0f),
                new Vector3(0f, 0f, 4.5f)
            };

            float[] activationDelays = new float[] { 0.0f, 0.8f, 1.6f };
            string[] names = new string[] { "⛧ SHADOW RONIN", "⛧ HEAVY ENFORCER", "⛧ CORRUPTED COMBAT BOT" };
            string[] fallbackPaths = new string[]
            {
                "Assets/Prefabs/Mission1_Enemy_Ninja.prefab",
                "Assets/Prefabs/Mission1_Enemy_Enforcer.prefab",
                "Assets/Prefabs/Mission1_Enemy_ProtoMech.prefab"
            };

            for (int i = 0; i < triangleOffsets.Length; i++)
            {
                Vector3 rawPos = zoneCenterPosition + triangleOffsets[i];
                Vector3 worldSpawn = rawPos;

                // Snap with NavMesh.SamplePosition 6m
                if (NavMesh.SamplePosition(rawPos, out NavMeshHit nmHit, 6.0f, NavMesh.AllAreas))
                {
                    worldSpawn = nmHit.position;
                }

                // Snap with ground raycast
                if (Physics.Raycast(worldSpawn + Vector3.up * 3.0f, Vector3.down, out RaycastHit hit, 8f))
                {
                    worldSpawn.y = hit.point.y + 0.05f;
                }

                // Face incoming player down the bridge
                Vector3 toPlayer = (mission1SpawnPosition - worldSpawn);
                toPlayer.y = 0f;
                Quaternion facePlayer = toPlayer.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toPlayer.normalized) : Quaternion.Euler(0f, 90f, 0f);

                GameObject thugObj = SpawnThugInstance(worldSpawn, facePlayer, names[i], i, fallbackPaths);
                var ctrl = thugObj.GetComponent<InsaneGunSwordController>();
                var tracker = thugObj.GetComponent<Mission1_EnemyTracker>();

                tracker.activationDelay = activationDelays[i];
                tracker.aggroDistance = 12.0f;
                tracker.SetIdentity(names[i], playerTransform);

                // Pre-combat trial lock: bots cannot be damaged until ActivateCombatAI.
                ctrl.isInvincible = true;
                ctrl.controlMode = InsaneGunSwordController.ControlMode.TrainingDummy;
                ctrl.inputLocked = true;

                activeEnemies.Add(ctrl);
                enemyTrackers.Add(tracker);

                ctrl.OnDeath += OnEnemyDied;
            }

            // Enable dynamic rival infighting / sparring between rival units if configured
            if (enableRivalInfighting && enemyTrackers.Count >= 2)
            {
                enemyTrackers[0].rivalTarget = enemyTrackers[1].transform;
                enemyTrackers[1].rivalTarget = enemyTrackers[0].transform;
            }

            Debug.Log($"[Mission1] Spawned {activeEnemies.Count} Diverse Combat Units in tactical formation on Skybridge.");
        }

        /// Trial-protection predicate for InsaneGunSwordController: an M1 bot with a
        /// tracker that has not combat-activated cannot be damaged (whiff + toast).
        private static bool IsBotTrialProtected(InsaneGunSwordController ctrl)
        {
            if (ctrl == null) return false;
            var tracker = ctrl.GetComponent<Mission1_EnemyTracker>();
            return tracker != null && !tracker.isCombatActivated;
        }

        private GameObject SpawnThugInstance(Vector3 pos, Quaternion rot, string thugName, int enemyIndex = 0, string[] fallbackPaths = null)
        {
            GameObject thug = null;

            GameObject prefabToUse = null;
            if (squadEnemyPrefabs != null && enemyIndex < squadEnemyPrefabs.Length && squadEnemyPrefabs[enemyIndex] != null)
            {
                prefabToUse = squadEnemyPrefabs[enemyIndex];
            }
#if UNITY_EDITOR
            if (prefabToUse == null && fallbackPaths != null && enemyIndex < fallbackPaths.Length)
            {
                prefabToUse = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(fallbackPaths[enemyIndex]);
            }
#endif
            if (prefabToUse == null)
            {
                prefabToUse = enemyPrefab;
            }

            if (prefabToUse != null)
            {
                thug = Instantiate(prefabToUse, pos, rot);
            }
            else
            {
                // Fallback to SM_Chr_Robot_01
                string robotModelPath = "Assets/PolygonCyberCity/Prefabs/Characters/SM_Chr_Robot_01.prefab";
                GameObject modelAsset = null;
#if UNITY_EDITOR
                modelAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(robotModelPath);
#endif
                thug = modelAsset != null ? Instantiate(modelAsset, pos, rot) : new GameObject(thugName);
                thug.name = thugName;
                thug.transform.localScale = Vector3.one * 1.06f;

                var cc = thug.GetComponent<CharacterController>();
                if (cc == null) cc = thug.AddComponent<CharacterController>();
                cc.center = new Vector3(0f, 0.93f * 1.06f, 0f);
                cc.height = 1.80f * 1.06f;
                cc.radius = 0.35f * 1.06f;

                var anim = thug.GetComponent<Animator>() ?? thug.GetComponentInChildren<Animator>();
                if (anim == null) anim = thug.AddComponent<Animator>();
                anim.applyRootMotion = false;

#if UNITY_EDITOR
                var rac = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/InsaneGun_Sword_Set/AC_InsaneGun_CombatMaster.controller");
                if (rac != null) anim.runtimeAnimatorController = rac;
                var av = UnityEditor.AssetDatabase.LoadAssetAtPath<Avatar>("Assets/PolygonCyberCity/Models/Characters.fbx");
                if (av != null) anim.avatar = av;
#endif

                var ctrl = thug.GetComponent<InsaneGunSwordController>();
                if (ctrl == null) ctrl = thug.AddComponent<InsaneGunSwordController>();
                ctrl.controlMode = InsaneGunSwordController.ControlMode.TrainingDummy;
                ctrl.inputLocked = true;

                // Blackwall Rogue AI combat stats baseline
                ctrl.maxHealth = 105f;
                ctrl.currentHealth = 105f;
                ctrl.attackDamage = 8.5f;
                ctrl.heavyAttackDamage = 14.0f;
                ctrl.gunDamagePerShot = 4.0f;
                ctrl.runSpeed = 5.8f;
                ctrl.walkSpeed = 3.2f;
                ctrl.minAttackInterval = 0.40f;
                ctrl.comboAcceptWindowStart = 0.65f;
                ctrl.autoReviveOnDeath = false;
                ctrl.enemyLayers = (1 << 8) | (1 << 0);
                ctrl.groundLayers = ~0;
                ctrl.aiParryChance = 0.0f;
                ctrl.aiBlockChance = 0.05f;
                ctrl.aiBlockMitigation = 0.35f;
                ctrl.aiDodgeReactionChance = 0.10f;
                ctrl.maxPoise = 20f;
                ctrl.currentPoise = 20f;
                ctrl.hasHyperArmor = false;
                ctrl.isSwordDrawn = true;

                var ragdoll = thug.GetComponent<EuphoriaRagdollController>();
                if (ragdoll == null) ragdoll = thug.AddComponent<EuphoriaRagdollController>();
                ragdoll.autoRecover = false;

                EquipThugWeapon(thug.transform, ctrl, anim);

                var tracker = thug.GetComponent<Mission1_EnemyTracker>();
                if (tracker == null) tracker = thug.AddComponent<Mission1_EnemyTracker>();
                tracker.aggroDistance = 12.0f;
                tracker.SetIdentity(thugName, playerTransform);
            }

            thug.name = thugName;
            thug.tag = "Enemy";
            thug.layer = 9; // Layer 9 = Enemy
            foreach (Transform t in thug.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = 9;
            }

            return thug;
        }

        private void EquipThugWeapon(Transform thug, InsaneGunSwordController ctrl, Animator anim)
        {
            Transform rHand = anim != null && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.RightHand) : null;
            if (rHand == null) rHand = thug.Find("hand_r") ?? thug;

            GameObject swordAsset = null;
#if UNITY_EDITOR
            swordAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonCyberCity/Prefabs/Weapons/SM_Wep_Sword_01.prefab");
#endif
            GameObject katana = swordAsset != null ? Instantiate(swordAsset, rHand) : new GameObject("Thug_Katana");
            katana.transform.localPosition = new Vector3(0.02f, 0.05f, 0.01f);
            katana.transform.localRotation = Quaternion.Euler(0f, 90f, -90f);

            Transform tip = katana.transform.Find("Blade_Tip");
            if (tip == null)
            {
                var tipGo = new GameObject("Blade_Tip");
                tipGo.transform.SetParent(katana.transform, false);
                tipGo.transform.localPosition = new Vector3(0f, 0.75f, 0f);
                tip = tipGo.transform;
            }

            var trail = tip.GetComponent<TrailRenderer>();
            if (trail == null) trail = tip.gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.22f;
            trail.startWidth = 0.70f;
            trail.endWidth = 0.02f;
            ctrl.swordTrail = trail;
            ctrl.swordInstance = katana.transform;
            ctrl.rightHandSocket = rHand;
        }

        // ─────────────────────────────────────────────────────────────────────
        // 4. BRIEFING AT SPAWN & ARENA COMBAT INITIATION
        // ─────────────────────────────────────────────────────────────────────
        public void TriggerBriefingAtSpawn()
        {
            if (_hasTriggeredIntro || state != MissionState.Awakening) return;
            _hasTriggeredIntro = true;
            state = MissionState.BriefingAtSpawn;

            Debug.Log("[Mission1] Starting Mission 1 at spawn with Dr. Ido dialogue box...");

            // Freeze player locomotion during briefing transmission
            LockPlayerInput(true);

            if (uiManager == null)
            {
                uiManager = GetComponentInChildren<Mission1_UIManager>() ?? UnityEngine.Object.FindAnyObjectByType<Mission1_UIManager>();
            }

            if (uiManager != null)
            {
                uiManager.ShowIntroPopup(OnGigAcceptedAtSpawn);
            }
            else
            {
                OnGigAcceptedAtSpawn();
            }
        }

        public void OnGigAcceptedAtSpawn()
        {
            Debug.Log("[Mission1] Gig Accepted at Spawn! Navigating to Sector 7 Skybridge...");
            if (uiManager == null)
            {
                uiManager = GetComponentInChildren<Mission1_UIManager>() ?? UnityEngine.Object.FindAnyObjectByType<Mission1_UIManager>();
                if (uiManager == null) Debug.LogWarning("[Mission1] OnGigAcceptedAtSpawn: uiManager null; waypoint HUD skipped.");
                else Debug.Log("[Mission1] OnGigAcceptedAtSpawn: resolved uiManager at call time.");
            }
            state = MissionState.TravelingToBridge;

            // Unfreeze player locomotion
            LockPlayerInput(false);

            // Activate Kiroshi AR Ground Trail leading to destination bridge
            InitGroundTracer();
            if (groundTracerObj != null) groundTracerObj.SetActive(true);

            // Prologue-handoff path: the gig was accepted from the Ido holo-call
            // at the bridge doorstep — engage the arena immediately if the player
            // is already inside the combat trigger radius. Otherwise the Update()
            // proximity check / zone trigger engages on arrival via the AR trail.
            if (_pendingHandoffCombat)
            {
                _pendingHandoffCombat = false;
                float distToArena = playerTransform != null
                    ? Vector3.Distance(playerTransform.position, zoneCenterPosition)
                    : float.MaxValue;
                if (distToArena <= 12.0f)
                {
                    Debug.Log($"[Mission1] Handoff gig accepted at bridge ({distToArena:F1}m); engaging arena combat now.");
                    EnterBridgeArenaCombat();
                }
                else
                {
                    Debug.Log($"[Mission1] Handoff gig accepted ({distToArena:F1}m from bridge); follow AR trail — proximity will engage combat.");
                }
            }

            // Update main Cyberpunk HUD if present
            if (CyberpunkHUDController.Instance != null)
            {
                CyberpunkHUDController.Instance.ShowToast("PATROL BRIEFING ACCEPTED // FOLLOW AR TRAIL", 4.0f);
                CyberpunkHUDController.Instance.SetQuestObjective(
                    "// OBJECTIVE: REACH SECTOR 7 SKYBRIDGE",
                    "> Follow the glowing Kiroshi AR trail across the walkways\n> Infiltrate the elevated bridge arena\n> Stance: [TAB] Blade Stance | [SPACE] Evade");
            }
        }

        public void EnterBridgeArenaCombat()
        {
            if (state != MissionState.TravelingToBridge) return;
            if (_isRespawning) return;

            // Extra safeguard: verify player is genuinely within 12m of the bridge
            if (playerTransform != null)
            {
                float dist = Vector3.Distance(playerTransform.position, zoneCenterPosition);
                if (dist > 12.0f)
                {
                    Debug.LogWarning($"[Mission1] EnterBridgeArenaCombat blocked: player {dist:F1}m from bridge (need <=12m).");
                    return;
                }
            }

            state = MissionState.CombatInArena;
            ActivateArenaCombat();
        }

        /// <summary>
        /// Shared arena-engage body: hides the trail, starts combat BGM, activates the
        /// bots, shows the kill tracker + quest objective. Called from the proximity
        /// path (EnterBridgeArenaCombat), the force path, and the Ido ambush.
        /// </summary>
        private void ActivateArenaCombat()
        {
            Debug.Log("[Mission1] Player reached Sector 7 Skybridge! STARTING INTENSE COMBAT BGM & ACTIVATING BOTS!");
            if (uiManager != null) uiManager.ShowFollowPopup(false);

            // 1. Hide ground navigation trail and floating waypoint
            if (groundTracerObj != null) groundTracerObj.SetActive(false);
            if (uiManager != null) uiManager.UpdateWaypointHUD(zoneCenterPosition, false);
            if (missionMarkerObj != null)
            {
                Destroy(missionMarkerObj);
                missionMarkerObj = null;
            }

            // 2. START INTENSE COMBAT BGM NOW (paulyudin-intense-intense-music-574004)!
            var audioMgr = GetComponent<Mission1_AudioManager>() ?? Mission1_AudioManager.Instance;
            audioMgr?.StartMissionBGM();

            // 3. Activate Razor Punks in combat AI mode with stagger & group coordination
            foreach (var tracker in enemyTrackers)
            {
                if (tracker != null)
                {
                    tracker.SetIdentity(tracker.displayName, playerTransform);
                    tracker.ActivateCombatAI();
                }
            }
            foreach (var ctrl in activeEnemies)
            {
                if (ctrl != null && !ctrl.IsDead)
                {
                    EnemyGroupCombatCoordinator.Instance?.RegisterCombatant(ctrl, playerTransform);
                }
            }

            // 4. Show combat objective tracker on HUD
            if (uiManager != null)
            {
                uiManager.UpdateKillTracker(killCount, RequiredKills);
                Debug.Log($"[Mission1] Kill tracker live: {killCount}/{RequiredKills}; {activeEnemies.Count} bots engaged.");
            }
            else Debug.LogWarning("[Mission1] EnterBridgeArenaCombat: uiManager null; kill tracker NOT shown.");

            // 5. Update main Cyberpunk HUD if present
            if (CyberpunkHUDController.Instance != null)
            {
                CyberpunkHUDController.Instance.SetQuestObjective(
                    "// VENGEANCE FOR IDO: SECTOR 7 SKYBRIDGE",
                    "> Neutralize Vulcan's recovery bots: 0/3\n> Avenge Dr. Dyson Ido\n> Stance: [TAB] Blade | [LMB] Light Slash | [SPACE] Evade");
                CyberpunkHUDController.Instance.ShowToast("[!] VOW OF VENGEANCE // DESTROY VULCAN'S SQUAD", 3.5f);
            }
        }

        public void OnPlayerEnteredZone()
        {
            var prologue = Alita.AlitaCityPrologueDirector.Instance;
            bool handoffDone = prologue != null &&
                (prologue.currentPhase == Alita.AlitaCityPrologueDirector.ProloguePhase.HandoffToCombat ||
                 prologue.currentPhase == Alita.AlitaCityPrologueDirector.ProloguePhase.Completed);
            if (_escortActive && !handoffDone)
            {
                // Early bridge arrival mid-escort: hold position, wait for Ido arrival first.
                if (Time.time - _lastWaitToastTime > 5.0f)
                {
                    _lastWaitToastTime = Time.time;
                    Debug.Log("[Mission1] Zone trigger hit mid-escort; holding for Ido arrival (no combat yet).");
                    if (CyberpunkHUDController.Instance != null)
                        CyberpunkHUDController.Instance.ShowToast("WAIT FOR IDO // STAY WITH YOUR GUIDE", 3.0f);
                }
                return;
            }
            if (state == MissionState.TravelingToBridge && !_isRespawning)
            {
                float dist = playerTransform != null ? Vector3.Distance(playerTransform.position, zoneCenterPosition) : 0f;
                if (dist <= 12.0f)
                {
                    EnterBridgeArenaCombat();
                }
            }
        }

        /// <summary>
        /// Drops the 3 bridge bots in a tight ring around Dr. Ido (1.7m, facing him)
        /// for the corrupt-city ambush scare. Bots stay dormant TrainingDummies —
        /// activation happens in BeginBridgeFight(true) after the scare dialogue.
        /// Ido is never targeted (bots aggro Alita on activation).
        /// </summary>
        /// <summary>
        /// Smoothly marches the 3 rogue bots forward along the skybridge into a tight
        /// standoff formation (~3.8m - 4.4m) in front of Dr. Ido and Alita.
        /// Bots physically walk on foot with walking animations and drawn weapons,
        /// NEVER snapping or teleporting.
        /// </summary>
        public void StageAmbushOnIdo()
        {
            StartCoroutine(WalkAmbushBotsToStandoffRoutine());
        }

        private static bool IsOnScreen(Vector3 worldPos)
        {
            var cam = Camera.main;
            if (cam == null) return false;
            Vector3 v = cam.WorldToViewportPoint(worldPos);
            return v.z > 0f && v.x > -0.1f && v.x < 1.1f && v.y > -0.1f && v.y < 1.1f;
        }

        public IEnumerator WalkAmbushBotsToStandoffRoutine()
        {
            var prologue = Alita.AlitaCityPrologueDirector.Instance;
            Transform ido = (prologue != null) ? prologue.doctorTransform : null;
            Transform player = playerTransform != null ? playerTransform : (playerGameObject != null ? playerGameObject.transform : null);

            Vector3 targetCenter = ido != null ? ido.position : (player != null ? player.position : zoneCenterPosition);

            if (enemyTrackers.Count == 0)
            {
                Debug.LogWarning("[Mission1] WalkAmbushBotsToStandoff: no bot trackers; ambush skipped.");
                yield break;
            }

            // Determine bridge direction pointing forward from Ido/Alita towards the bridge deck
            Vector3 bridgeDir = Vector3.forward;
            if (ido != null)
            {
                Vector3 toArena = zoneCenterPosition - ido.position;
                toArena.y = 0f;
                if (toArena.sqrMagnitude > 1.0f)
                {
                    bridgeDir = toArena.normalized;
                }
                else
                {
                    bridgeDir = ido.forward;
                    bridgeDir.y = 0f;
                    if (bridgeDir.sqrMagnitude < 0.01f) bridgeDir = Vector3.forward;
                    else bridgeDir.Normalize();
                }
            }
            Vector3 bridgeRight = Vector3.Cross(Vector3.up, bridgeDir).normalized;

            // Standoff offsets in front of Ido & Alita (leader in front at 3.8m, flankers left/right at 4.4m)
            Vector3[] standoffOffsets = new Vector3[]
            {
                bridgeDir * 3.8f,
                bridgeDir * 4.4f - bridgeRight * 2.2f,
                bridgeDir * 4.4f + bridgeRight * 2.2f
            };

            // Advance staging start positions down the bridge (~9.5m - 12m ahead)
            Vector3[] advanceStartOffsets = new Vector3[]
            {
                bridgeDir * 10.5f,
                bridgeDir * 11.5f - bridgeRight * 2.4f,
                bridgeDir * 11.5f + bridgeRight * 2.4f
            };

            var walkCoroutines = new List<Coroutine>();
            float maxWalkTime = 4.2f;

            for (int i = 0; i < enemyTrackers.Count && i < standoffOffsets.Length; i++)
            {
                var tr = enemyTrackers[i];
                if (tr == null) continue;

                // Ensure bot starts at a clean position ahead on the bridge if out of bounds or too close
                float currentDistToTarget = Vector3.Distance(tr.transform.position, targetCenter);
                // A bot the camera can see is never moved: it walks in from where it stands. Only off-screen bots are re-staged.
                bool botVisible = IsOnScreen(tr.transform.position + Vector3.up * 1.0f);
                if ((currentDistToTarget < 6.0f || currentDistToTarget > 18.0f) && !botVisible)
                {
                    Vector3 startPos = targetCenter + advanceStartOffsets[i];
                    if (NavMesh.SamplePosition(startPos, out NavMeshHit sHit, 4f, NavMesh.AllAreas)) startPos = sHit.position;
                    if (Physics.Raycast(startPos + Vector3.up * 2f, Vector3.down, out RaycastHit gHit, 4f)) startPos.y = gHit.point.y + 0.05f;

                    Vector3 toTarget = targetCenter - startPos;
                    toTarget.y = 0f;
                    Quaternion startRot = toTarget.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toTarget.normalized) : Quaternion.LookRotation(-bridgeDir);

                    var cc = tr.GetComponent<CharacterController>();
                    if (cc != null) cc.enabled = false;
                    tr.transform.position = startPos;
                    tr.transform.rotation = startRot;
                    Physics.SyncTransforms();
                    if (cc != null) cc.enabled = true;
                }

                // Standoff destination
                Vector3 rawDest = targetCenter + standoffOffsets[i];
                Vector3 finalDest = rawDest;
                if (NavMesh.SamplePosition(rawDest, out NavMeshHit dHit, 4f, NavMesh.AllAreas)) finalDest = dHit.position;
                if (Physics.Raycast(finalDest + Vector3.up * 2f, Vector3.down, out RaycastHit dgHit, 4f)) finalDest.y = dgHit.point.y + 0.05f;

                Vector3 faceIdo = targetCenter - finalDest;
                faceIdo.y = 0f;
                Quaternion finalRot = faceIdo.sqrMagnitude > 0.01f ? Quaternion.LookRotation(faceIdo.normalized) : Quaternion.LookRotation(-bridgeDir);

                // Start physical on-foot walk towards Ido/Alita
                // Close the real distance on foot: faster (jog) when the bot starts far away, with a timeout that fits
                float walkDist = Vector3.Distance(tr.transform.position, finalDest);
                float speed = Mathf.Clamp(walkDist / 5.0f, 2.3f, 4.2f);
                float walkTimeout = walkDist / speed + 1.5f;
                maxWalkTime = Mathf.Max(maxWalkTime, walkTimeout);
                var co = tr.StartWalkToStandoff(finalDest, finalRot, walkSpeed: speed, timeout: walkTimeout);
                if (co != null) walkCoroutines.Add(co);
            }

            Debug.Log($"[Mission1] Ambush walk initiated: {walkCoroutines.Count} bots walking into formation towards Ido & Alita.");

            // Wait for all bots to finish walking into formation (max 4.2 seconds safety)
            float timer = 0f;
            while (timer < maxWalkTime)
            {
                timer += Time.deltaTime;
                bool allArrived = true;
                for (int i = 0; i < enemyTrackers.Count && i < standoffOffsets.Length; i++)
                {
                    var tr = enemyTrackers[i];
                    if (tr == null) continue;
                    Vector3 dest = targetCenter + standoffOffsets[i];
                    dest.y = tr.transform.position.y;
                    if (Vector3.Distance(tr.transform.position, dest) > 0.60f)
                    {
                        allArrived = false;
                        break;
                    }
                }
                if (allArrived) break;
                yield return null;
            }

            Debug.Log($"[Mission1] Ambush bots arrived at standoff positions in front of Ido & Alita in {timer:F2}s.");
        }

        /// <summary>
        /// Idempotent entry for the prologue -> combat handoff. Invoked from the prologue
        /// handoff event, the prologue wait-loop backup, or a timeout force. Every path
        /// converges here; double-fire is impossible (guarded by _bridgeFightStarted).
        /// fromPrologueEscort=true skips the holo-call (Ido is standing right there)
        /// and runs the in-person bridge ambush instead.
        /// </summary>
        public void BeginBridgeFight()
        {
            BeginBridgeFight(false);
        }

        public void BeginBridgeFight(bool fromPrologueEscort)
        {
            if (this == null) return;
            if (_bridgeFightStarted)
            {
                Debug.Log($"[Mission1] BeginBridgeFight: already started (state={state}); ignoring duplicate handoff.");
                return;
            }
            if (state == MissionState.Completed)
            {
                Debug.Log("[Mission1] BeginBridgeFight: mission already Completed; ignoring handoff.");
                return;
            }
            if (state == MissionState.CombatInArena)
            {
                Debug.Log("[Mission1] BeginBridgeFight: already in combat; refreshing tracker + bot activation.");
                _bridgeFightStarted = true;
                foreach (var t in enemyTrackers)
                {
                    if (t != null)
                    {
                        t.SetIdentity(t.displayName, playerTransform);
                        t.ActivateCombatAI();
                    }
                }
                if (uiManager != null) uiManager.UpdateKillTracker(killCount, RequiredKills);
                return;
            }
            _bridgeFightStarted = true;
            Debug.Log($"[Mission1] BeginBridgeFight: handoff accepted (prevState={state}).");

            try
            {
                HealMissionReferences();
                RecenterArenaToPlayerIfMismatched();
                ClearEscortTarget();
                if (state == MissionState.Awakening)
                {
                    if (fromPrologueEscort)
                    {
                        // In-person bridge ambush: Ido is standing right here, no call.
                        // Bots were staged around him during the scare dialogue; engage.
                        Debug.Log("[Mission1] BeginBridgeFight: escort handoff -> in-person ambush combat (no holo-call).");
                        state = MissionState.CombatInArena;
                        ActivateArenaCombat();
                        Debug.Log($"[Mission1] BeginBridgeFight done (ambush): state={state}, bots={enemyTrackers.Count}, tracker={killCount}/{RequiredKills}.");
                        return;
                    }
                    // Prologue handoff path: Doc Ido holo-call FIRST. The briefing
                    // popup (ringtone + Ido voice + typewriter) is presented here;
                    // arena combat engages from OnGigAcceptedAtSpawn on accept.
                    // Combat is NOT forced underneath the popup (input is locked
                    // while the call UI is open).
                    _pendingHandoffCombat = true;
                    Debug.Log("[Mission1] BeginBridgeFight: Awakening -> TriggerBriefingAtSpawn (Ido holo-call); combat deferred to gig accept.");
                    TriggerBriefingAtSpawn();
                    Debug.Log($"[Mission1] BeginBridgeFight: briefing shown, awaiting accept (state={state}).");
                    return;
                }
                Debug.Log($"[Mission1] BeginBridgeFight: engaging combat (state={state}).");
                TriggerCombatArenaDirect();
                Debug.Log($"[Mission1] BeginBridgeFight done: state={state}, bots={enemyTrackers.Count}, tracker={killCount}/{RequiredKills}.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Mission1] BeginBridgeFight exception: {ex}");
            }
        }

        private void OnPrologueHandoffEvent()
        {
            if (this == null) return;
            Debug.Log("[Mission1] Prologue handoff EVENT received; converging on BeginBridgeFight().");
            BeginBridgeFight();
        }

        private void HealMissionReferences()
        {
            if (uiManager == null)
            {
                uiManager = GetComponentInChildren<Mission1_UIManager>()
                         ?? UnityEngine.Object.FindAnyObjectByType<Mission1_UIManager>();
                if (uiManager == null) Debug.LogWarning("[Mission1] HealRefs: Mission1_UIManager still missing after resolve.");
                else Debug.Log("[Mission1] HealRefs: resolved Mission1_UIManager at call time.");
            }
            if (playerTransform == null && playerGameObject != null) playerTransform = playerGameObject.transform;
            if (playerTransform == null)
            {
                var p = GameObject.FindWithTag("Player");
                if (p != null)
                {
                    playerGameObject = p;
                    playerTransform = p.transform;
                    Debug.Log("[Mission1] HealRefs: re-acquired player via tag.");
                }
                else Debug.LogWarning("[Mission1] HealRefs: playerTransform null and no Player tag found.");
            }
            if (enemyTrackers.Count == 0)
            {
                var orphans = UnityEngine.Object.FindObjectsByType<Mission1_EnemyTracker>(FindObjectsInactive.Include);
                foreach (var o in orphans)
                {
                    if (o != null && !enemyTrackers.Contains(o)) enemyTrackers.Add(o);
                }
                Debug.LogWarning($"[Mission1] HealRefs: enemyTrackers was empty; adopted {enemyTrackers.Count} tracker(s) from scene.");
            }
            else Debug.Log($"[Mission1] HealRefs: {enemyTrackers.Count} bot tracker(s) present.");
        }

        /// <summary>
        /// Pins the prologue's final waypoint (Beat4, bridge arrival) to the recalculated
        /// arena so Ido physically walks onto the bot arena before the handoff can fire.
        /// </summary>
        private void SyncBeat4ToZone()
        {
            var prologue = Alita.AlitaCityPrologueDirector.Instance;
            if (prologue == null)
            {
                Debug.LogWarning("[Mission1] SyncBeat4: no prologue director; skipped.");
                return;
            }
            if (prologue.narrativeBeats == null || prologue.narrativeBeats.Count < 5)
            {
                Debug.LogWarning("[Mission1] SyncBeat4: beats not ready; skipped.");
                return;
            }
            prologue.narrativeBeats[4].waypointPosition = zoneCenterPosition;
            Debug.Log($"[Mission1] SyncBeat4: Beat4 waypoint pinned to zone {zoneCenterPosition}.");
        }

        private void RecenterArenaToPlayerIfMismatched()
        {
            if (playerTransform == null) { Debug.LogWarning("[Mission1] Recenter: skipped (no player)."); return; }
            float distToArena = Vector3.Distance(playerTransform.position, zoneCenterPosition);
            if (distToArena <= 25f)
            {
                Debug.Log($"[Mission1] Arena co-located with player (dist={distToArena:F1}m); keeping bridge at {zoneCenterPosition}.");
                return;
            }
            Vector3 target = playerTransform.position;
            if (NavMesh.SamplePosition(target, out NavMeshHit navHit, 6f, NavMesh.AllAreas)) target = navHit.position;
            if (Physics.Raycast(target + Vector3.up * 3f, Vector3.down, out RaycastHit gHit, 6f)) target.y = gHit.point.y + 0.10f;
            else target.y += 0.10f;
            Debug.Log($"[Mission1] Arena coordinate updated to player ({distToArena:F1}m delta). No bot teleportation (bots advance on foot).");
            zoneCenterPosition = target;
            if (mission1Zone != null) mission1Zone.transform.position = zoneCenterPosition;

            // Live combatants physically walk/run to the player; NEVER snap or teleport!
            for (int i = 0; i < activeEnemies.Count; i++)
            {
                var ctrl = activeEnemies[i];
                if (ctrl != null && !ctrl.IsDead)
                {
                    ctrl.targetEnemy = playerTransform;
                }
            }
        }

        public void TriggerCombatArenaDirect()
        {
            Debug.Log($"[Mission1] TriggerCombatArenaDirect() entry (state={state}).");
            state = MissionState.TravelingToBridge;
            EnterBridgeArenaCombat();
            if (state != MissionState.CombatInArena)
            {
                Debug.LogWarning("[Mission1] EnterBridgeArenaCombat did not engage (likely distance guard); forcing CombatInArena with full combat setup.");
                // Force state if distance check in EnterBridgeArenaCombat prevented it
                state = MissionState.CombatInArena;
                if (uiManager != null) uiManager.ShowFollowPopup(false);
                if (groundTracerObj != null) groundTracerObj.SetActive(false);
                if (uiManager != null) uiManager.UpdateWaypointHUD(zoneCenterPosition, false);
                else Debug.LogWarning("[Mission1] Force-combat: uiManager null; waypoint HUD left as-is.");
                if (missionMarkerObj != null)
                {
                    Destroy(missionMarkerObj);
                    missionMarkerObj = null;
                }
                var audioMgr = GetComponent<Mission1_AudioManager>() ?? Mission1_AudioManager.Instance;
                if (audioMgr != null) audioMgr.StartMissionBGM();
                else Debug.LogWarning("[Mission1] Force-combat: no Mission1_AudioManager found; BGM skipped.");
                int activated = 0;
                foreach (var tracker in enemyTrackers)
                {
                    if (tracker != null)
                    {
                        tracker.SetIdentity(tracker.displayName, playerTransform);
                        tracker.ActivateCombatAI();
                        activated++;
                    }
                    else Debug.LogWarning("[Mission1] Force-combat: null entry in enemyTrackers; skipped.");
                }
                Debug.Log($"[Mission1] Force-combat: activated {activated}/{enemyTrackers.Count} bots.");
                if (uiManager != null)
                {
                    uiManager.UpdateKillTracker(killCount, RequiredKills);
                    Debug.Log($"[Mission1] Kill tracker live: {killCount}/{RequiredKills}.");
                }
                else Debug.LogWarning("[Mission1] Force-combat: uiManager null; kill tracker NOT shown.");
                if (CyberpunkHUDController.Instance != null)
                {
                    CyberpunkHUDController.Instance.SetQuestObjective(
                        "// VENGEANCE FOR IDO: SECTOR 7 SKYBRIDGE",
                        "> Neutralize Vulcan's recovery bots: 0/3\n> Avenge Dr. Dyson Ido\n> Stance: [TAB] Blade | [LMB] Light Slash | [SPACE] Evade");
                    CyberpunkHUDController.Instance.ShowToast("[!] VOW OF VENGEANCE // DESTROY VULCAN'S SQUAD", 3.5f);
                }
                else Debug.LogWarning("[Mission1] Force-combat: no CyberpunkHUDController; HUD objective skipped.");
            }
            else Debug.Log($"[Mission1] TriggerCombatArenaDirect: combat engaged normally (state={state}).");
        }

        // ─────────────────────────────────────────────────────────────────────
        // 5. TWO-WAY COMBAT & OBJECTIVE TRACKING
        // ─────────────────────────────────────────────────────────────────────
        private void OnPlayerDamagedInternal(float dmg)
        {
            if (uiManager != null)
            {
                uiManager.FlashDamageVignette(0.65f);
            }
            Mission1_AudioManager.Instance?.PlayHit(true);
        }

        private float _lastGroupAttackTime = 0f;

        /// <summary>
        /// Relentlessly drives living Mission 1 enemies to attack Alita in coordinated rhythm,
        /// matching the Blackwall Rogue AI combat behavior.
        /// Integrates with EnemyGroupCombatCoordinator so when 2 or more enemies are active,
        /// they surround Alita first and coordinate attacks one at a time via single-token cadence.
        /// </summary>
        private void DriveActiveAggression()
        {
            if (playerTransform == null) return;

            var coordinator = EnemyGroupCombatCoordinator.Instance;
            if (coordinator != null && coordinator.IsGroupSurroundActive(playerTransform))
            {
                // Multi-Enemy Coordination Active:
                // EnemyGroupCombatCoordinator manages the surrounding ring formation and
                // grants attack tokens one by one. Do not force uncoordinated attacks!
                return;
            }

            if (Time.time - _lastGroupAttackTime < 0.65f) return;

            for (int i = 0; i < activeEnemies.Count; i++)
            {
                var ctrl = activeEnemies[i];
                if (ctrl == null || ctrl.IsDead || ctrl.inputLocked) continue;

                float dist = Vector3.Distance(ctrl.transform.position, playerTransform.position);
                float strikeRange = ctrl.transform.localScale.x > 1.25f ? 4.2f : 3.2f;

                // If within striking distance and not currently attacking, unleash sword strikes & combos!
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
                // Mid-range approach handled purely on foot by AICirclingEnemy (smooth running/walking, no teleport lunges)

            }
        }

        private void OnEnemyDied(InsaneGunSwordController enemy)
        {
            if (enemy != null)
            {
                EnemyGroupCombatCoordinator.Instance?.UnregisterCombatant(enemy);
            }
            if (state != MissionState.CombatInArena) return;

            killCount++;
            try
            {
                var invType = System.Type.GetType("Cyberpunk.Inventory.CyberpunkInventory, Assembly-CSharp");
                if (invType != null)
                {
                    var instProp = invType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    var inst = instProp?.GetValue(null, null);
                    if (inst != null)
                    {
                        invType.GetMethod("RollBotDrop", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)?.Invoke(inst, null);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[Mission1] RollBotDrop lookup: " + ex.Message);
            }
            Debug.Log($"[Mission1] Razor Punk neutralized! Progress: {killCount}/{RequiredKills}");

            if (uiManager != null)
            {
                uiManager.UpdateKillTracker(killCount, RequiredKills);
            }

            if (CyberpunkHUDController.Instance != null)
            {
                CyberpunkHUDController.Instance.ShowToast($"ROGUE BOT NEUTRALIZED ({killCount}/{RequiredKills})", 2.0f);
            }

            if (killCount >= RequiredKills)
            {
                StartCoroutine(CompleteMissionRoutine());
            }
        }

        private IEnumerator CompleteMissionRoutine()
        {
            state = MissionState.Completed;
            if (uiManager != null) uiManager.ShowFollowPopup(false);

            // Full repair on victory — Ido's verdict: the body is working properly.
            // (Restoring HP also clears the low-health red vignette from the HUD.)
            if (playerSynty != null)
            {
                playerSynty.currentHealth = playerSynty.maxHealth;
            }
            if (playerInsaneGun != null)
            {
                playerInsaneGun.currentHealth = playerInsaneGun.maxHealth;
            }
            Debug.Log("[Mission1] Victory repair: player restored to full HP; red low-health filter cleared.");
            yield return new WaitForSeconds(1.5f);

            LockPlayerInput(true);

            // Fade out BGM on completion
            Mission1_AudioManager.Instance?.StopMissionBGM(2.0f);

            if (uiManager != null)
            {
                uiManager.ShowCompletedPopup(OnMissionCompletedFinal);
            }
            else
            {
                OnMissionCompletedFinal();
            }
        }

        private void OnMissionCompletedFinal()
        {
            IsMission1Completed = true;

            LockPlayerInput(false);

            if (CyberpunkHUDController.Instance != null)
            {
                CyberpunkHUDController.Instance.ShowToast("[*] MISSION 1: COMPLETE // SECTOR SKYBRIDGE SECURED", 5.0f);
                CyberpunkHUDController.Instance.SetQuestObjective(
                    "// MISSION 1: COMPLETE",
                    "> All rogue combat bots neutralized\n> Bridge corridor secured\n> Explore the Scrapyard / Iron City");
            }

            try
            {
                var m2Type = System.Type.GetType("Cyberpunk.Missions.Mission2_ArenaPassStealManager, Assembly-CSharp");
                if (m2Type != null)
                {
                    m2Type.GetMethod("BeginAfterMission1", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.Invoke(null, null);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[Mission1] Mission2 start lookup: " + ex.Message);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // 6. PLAYER DEATH & REBOOT SEQUENCE
        // ─────────────────────────────────────────────────────────────────────
        private void OnPlayerDiedInternal()
        {
            if (_isRespawning) return;
            _isRespawning = true;

            Debug.Log("[Mission1] Player died during Skybridge Trial! Stopping battle music & initiating reboot sequence...");

            // 1. Stop battle BGM immediately on death
            var audioMgr = GetComponent<Mission1_AudioManager>() ?? Mission1_AudioManager.Instance;
            audioMgr?.StopMissionBGMImmediate();

            // 2. Hide kill tracker HUD
            if (uiManager != null)
            {
                uiManager.HideKillTracker();
            }

            // 3. Freeze active enemies in TrainingDummy mode immediately so they stop attacking corpse
            foreach (var tracker in enemyTrackers)
            {
                if (tracker != null && tracker.combatController != null)
                {
                    tracker.combatController.controlMode = InsaneGunSwordController.ControlMode.TrainingDummy;
                    tracker.combatController.inputLocked = true;
                }
            }

            if (uiManager != null)
            {
                uiManager.TriggerRebootBlackout(1.0f, () =>
                {
                    RespawnPlayerAtStart();
                });
            }
            else
            {
                RespawnPlayerAtStart();
            }
        }

        private void RespawnPlayerAtStart()
        {
            Vector3 targetSpawnPos = mission1SpawnPosition;
            if (NavMesh.SamplePosition(mission1SpawnPosition, out NavMeshHit sHit, 6f, NavMesh.AllAreas))
            {
                targetSpawnPos = sHit.position + Vector3.up * 0.05f;
            }

            // 1. Teleport player GameObject with CharacterController disabled + sync
            if (playerGameObject != null)
            {
                var cc = playerGameObject.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                playerGameObject.transform.position = targetSpawnPos;
                playerGameObject.transform.rotation = mission1SpawnRotation;
                Physics.SyncTransforms();
                if (cc != null) cc.enabled = true;
            }

            // 2. Call controller respawn hooks
            if (playerSynty != null)
            {
                playerSynty.Respawn(targetSpawnPos, mission1SpawnRotation);
            }
            else if (playerInsaneGun != null)
            {
                playerInsaneGun.Revive(targetSpawnPos);
                playerInsaneGun.transform.rotation = mission1SpawnRotation;
                playerInsaneGun.inputLocked = false;
            }

            // 3. Snap third-person camera to player at spawn
            var orbitCam = UnityEngine.Object.FindAnyObjectByType<SmoothOrbitCamera>();
            if (orbitCam != null) orbitCam.SnapToTarget();

            // 4. Reset trial state, stop any residual music, and recreate navigation path
            ResetMissionTrial();
            _isRespawning = false;
        }

        private void ResetMissionTrial()
        {
            state = MissionState.TravelingToBridge;
            killCount = 0;

            // 1. Ensure combat music is completely stopped upon respawn
            var audioMgr = GetComponent<Mission1_AudioManager>() ?? Mission1_AudioManager.Instance;
            if (audioMgr != null)
            {
                audioMgr.StopMissionBGMImmediate();
            }

            if (uiManager != null)
            {
                uiManager.HideKillTracker();
            }

            // 2. Reset Razor Punks back to initial positions on the skybridge
            Vector3[] bridgeOffsets = new Vector3[]
            {
                new Vector3(-3.5f, 0f, 0f),
                new Vector3(3.5f, 0f, 0f),
                new Vector3(0f, 0f, 4.5f)
            };

            for (int i = 0; i < enemyTrackers.Count; i++)
            {
                if (enemyTrackers[i] != null)
                {
                    Vector3 rPos = zoneCenterPosition + bridgeOffsets[Mathf.Min(i, bridgeOffsets.Length - 1)];
                    if (NavMesh.SamplePosition(rPos, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                    {
                        rPos = hit.position;
                    }
                    Quaternion fRot = Quaternion.Euler(0f, 90f, 0f);
                    enemyTrackers[i].ResetToInitial(rPos, fRot);
                }
            }

            // 3. Re-create minimal cyan marker and GPS tracer from spawn to bridge
            CreateMissionMarker();
            InitGroundTracer();
            if (groundTracerObj != null) groundTracerObj.SetActive(true);
            _lastTracerUpdateTime = 0f;
            _lastPlayerTracerPos = Vector3.zero;
            UpdateGroundTracer();

            // 4. Re-enable waypoint HUD towards destination bridge
            if (uiManager != null)
            {
                uiManager.UpdateWaypointHUD(zoneCenterPosition, true);
            }

            // 5. Restore Cyberpunk HUD quest objective & toast
            if (CyberpunkHUDController.Instance != null)
            {
                CyberpunkHUDController.Instance.ShowToast("[!] REBOOT COMPLETE // RETRY COMBAT TRIAL", 4.0f);
                CyberpunkHUDController.Instance.SetQuestObjective(
                    "// OBJECTIVE: REACH SECTOR 7 SKYBRIDGE",
                    "> Follow the glowing Kiroshi AR trail across the walkways\n> Infiltrate the elevated bridge arena\n> Stance: [TAB] Blade Stance | [SPACE] Evade");
            }

            LockPlayerInput(false);
            Debug.Log("[Mission1] Trial successfully reset: BGM stopped, AR trail restored, awaiting bridge arrival.");
        }
    }

    public class Mission1_ZoneTrigger : MonoBehaviour
    {
        public Mission1_StreetTrialManager manager;

        private void OnTriggerEnter(Collider other)
        {
            if (manager == null) return;
            if (other.CompareTag("Player") || other.GetComponent<SyntySwordLocomotionController>() != null || other.GetComponent<InsaneGunSwordController>() != null)
            {
                manager.OnPlayerEnteredZone();
            }
        }
    }

    public class BeaconPulseAnimator : MonoBehaviour
    {
        public float pulseSpeed = 4.18f;
        public float scaleMagnitude = 0.04f;
        private Vector3 _baseScale;

        private void Start()
        {
            _baseScale = transform.localScale;
        }

        private void Update()
        {
            float wave = 1f + Mathf.Sin(Time.time * pulseSpeed) * scaleMagnitude;
            transform.localScale = new Vector3(_baseScale.x * wave, _baseScale.y, _baseScale.z * wave);
        }
    }

    public class MissionIconBillboardBob : MonoBehaviour
    {
        public float bobSpeed = 2.5f;
        public float bobAmount = 0.12f;
        private Vector3 _basePos;

        private void Start()
        {
            _basePos = transform.localPosition;
        }

        private void Update()
        {
            transform.localPosition = _basePos + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobAmount);
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 dir = transform.position - cam.transform.position;
                if (dir.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.LookRotation(dir);
                }
            }
        }
    }
}

