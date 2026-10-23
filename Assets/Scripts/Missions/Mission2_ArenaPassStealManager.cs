using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using InsaneGun.Combat;
using SyntyCombined;
using CyberpunkDev.Traffic;

namespace Cyberpunk.Missions
{
    /// <summary>
    /// MISSION 2 - "STEAL THE PIT PASS" in Demo_City.
    /// 
    /// Narrative & Gameplay Flow:
    /// 1. M1 bridge clear -> Dr. Ido holo-call: a Razor scalper on the bridge has a spare Pit pass.
    /// 2. Player accepts -> Scalper is spotted on the bridge deck with a floating gold target marker.
    /// 3. Approaching within 12m spooks the Scalper -> Solo chase sequence begins!
    /// 4. 210m+ verified NavMesh route: Bridge -> Terrace Mid-Landing -> Clinic Plaza -> Lower Alley Van.
    /// 5. Thin glowing AI navigation tracer continuously points the path from Player to Scalper.
    /// 6. Chase lasts a MINIMUM of 30 seconds with dynamic rubber-banding and sprint bursts.
    /// 7. Once 30s have elapsed and the player corners him (or reaches the getaway van):
    ///    The Scalper turns around and engages in an intense 1-on-1 sword duel.
    /// 8. At 30% HP, Scalper drops to his knees: [E] RIP PASS.
    /// 9. Loot pass -> Arena Pass secured -> Mission 2 Complete -> Mission 3 Unlocked!
    /// </summary>
    public class Mission2_ArenaPassStealManager : MonoBehaviour
    {
        public static Mission2_ArenaPassStealManager Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Instance = null;
        }

        public enum M2State
        {
            Inactive,
            Briefing,
            HuntingMark,
            Chasing,
            CorneredFight,
            LootPass,
            Completed,
            Failed_Escaped
        }

        [Header("State")]
        public M2State state = M2State.Inactive;

        // NavMesh route checkpoints spanning from the Skybridge down to the Lower Alley Van (~212m).
        private static readonly Vector3[] RouteCheckpoints = new Vector3[]
        {
            new Vector3(-10.00f, 0.08f, -8.75f),     // 0: Skybridge deck (Scalper spawn point)
            new Vector3(-14.50f, -5.92f, 14.17f),    // 1: Terrace / Mid-Landing
            new Vector3(12.55f, -17.92f, 56.83f),    // 2: Clinic Plaza
            new Vector3(-16.83f, -20.92f, 8.17f)     // 3: Lower District Alley (Van dead-end)
        };

        private static readonly Vector3 M1BridgeDefault = new Vector3(5.00f, 0.08f, -8.75f);
        private static readonly Vector3 ScalperInitialSpawn = new Vector3(-10.00f, 0.08f, -8.75f);

        public enum EscapeReason
        {
            Timeout,
            VanEscape,
            TooFar
        }

        // Locomotion tuning
        private const float ScalperBaseSpeed = 5.2f;
        private const float ScalperBurstSpeed = 6.2f;
        private const float ScalperEaseSpeed = 4.4f;
        private const float SpookDist = 11.0f;
        private const float CornerProximityDist = 6.5f;
        private const float ChaseMinDuration = 30.0f;
        private const float ChaseTimeout = 120.0f;
        private const float MaxEscapeDist = 22.0f;
        private const float EscapeWarningDist = 16.0f;
        private const float LootDist = 2.5f;
        private const float KneeFrac = 0.30f;

        // Route data
        private List<Vector3> _route = new List<Vector3>();
        private int _routeLeg = 0;
        private Vector3 _vanPoint;
        private Vector3 _lastScalperPos = Vector3.zero;
        private float _cornerStuckTimer = 0f;

        // Chase state
        private float _chaseTime = 0f;
        private float _timeLeft = ChaseTimeout;
        private int _lastShownSecond = -1;
        private bool _spookFired = false;
        private bool _toast30Shown = false;
        private float _lastStrayWarningTime = -100f;
        private float _lastBurstCalloutTime = -100f;
        private float _strayTetherTimer = -1f;

        // Fight & Loot state
        private bool _cornerCombatLive = false;
        private bool _kneed = false;
        private bool _corpseLootable = false;
        private bool _looted = false;
        private bool _deathHandled = false;

        // Actor references
        private GameObject _playerGO;
        private Transform _playerTransform;
        private SyntySwordLocomotionController _playerSynty;
        private InsaneGunSwordController _playerInsaneGun;

        private GameObject _scalperObj;
        private InsaneGunSwordController _scalperCtrl;
        private CharacterController _scalperCC;
        private Mission1_EnemyTracker _scalperTracker;
        private Animator _scalperAnim;
        private bool _scalperAnimHasMoveSpeed;

        // Presentation & UI
        private GameObject _vanProp;
        private GameObject _goldMarker;
        private GameObject _tracerObj;
        private LineRenderer _tracerLine;
        private NavMeshPath _tracerNavPath;
        private float _lastTracerCalcTime = 0f;

        private Mission1_UIManager uiManager;
        private Mission1_StreetTrialManager m1;

        // Frosted-glass OnGUI subtitle system
        private class SubMsg
        {
            public string speaker;
            public string title;
            public string text;
            public float dur;
            public SubMsg(string s, string t, string x, float d) { speaker = s; title = t; text = x; dur = d; }
        }
        private readonly Queue<SubMsg> _subQueue = new Queue<SubMsg>();
        private SubMsg _activeSub;
        private float _subTimer;
        private Texture2D _blackPixel;
        private Texture2D _whitePixel;

        public static void BeginAfterMission1()
        {
            if (Instance != null)
            {
                Debug.Log("[Mission2] BeginAfterMission1: already running; ignored.");
                return;
            }
            GameObject go = new GameObject("Mission2_ArenaPassSteal");
            go.AddComponent<Mission2_ArenaPassStealManager>();
            Debug.Log("[Mission2] Mission 2 bootstrapped after M1 3/3.");
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _blackPixel = new Texture2D(1, 1);
            _blackPixel.SetPixel(0, 0, new Color(0.02f, 0.025f, 0.04f, 0.92f));
            _blackPixel.Apply();
            _whitePixel = new Texture2D(1, 1);
            _whitePixel.SetPixel(0, 0, Color.white);
            _whitePixel.Apply();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            UnbindFighterEvents();
            // Never leave the city silent if the manager goes away mid-mission.
            Cyberpunk.Audio.CyberBackgroundMusic.Instance?.ResumeAfterMission();
        }

        private void Start()
        {
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (sceneName.IndexOf("Demo_City", StringComparison.OrdinalIgnoreCase) < 0)
            {
                Debug.LogError($"[Mission2] Refusing to start in '{sceneName}': Mission 2 only runs in Demo_City.");
                state = M2State.Inactive;
                return;
            }

            ResolvePlayer();
            uiManager = Mission1_UIManager.Instance ?? FindAnyObjectByType<Mission1_UIManager>();
            m1 = Mission1_StreetTrialManager.Instance ?? FindAnyObjectByType<Mission1_StreetTrialManager>();

            StartCoroutine(BriefingRoutine());
        }

        // ─────────────────────────────────────────────────────────────────────
        // NAVMESH ROUTE GENERATOR
        // ─────────────────────────────────────────────────────────────────────
        private List<Vector3> BuildNavMeshChaseRoute()
        {
            var unified = new List<Vector3>();

            for (int i = 0; i < RouteCheckpoints.Length - 1; i++)
            {
                Vector3 pStart = RouteCheckpoints[i];
                Vector3 pEnd = RouteCheckpoints[i + 1];

                if (NavMesh.SamplePosition(pStart, out NavMeshHit sHit, 6.0f, NavMesh.AllAreas))
                    pStart = sHit.position;
                if (NavMesh.SamplePosition(pEnd, out NavMeshHit eHit, 6.0f, NavMesh.AllAreas))
                    pEnd = eHit.position;

                NavMeshPath path = new NavMeshPath();
                if (NavMesh.CalculatePath(pStart, pEnd, NavMesh.AllAreas, path)
                    && path.status != NavMeshPathStatus.PathInvalid && path.corners.Length >= 2)
                {
                    for (int k = 0; k < path.corners.Length; k++)
                    {
                        Vector3 c = path.corners[k];
                        if (unified.Count > 0 && Vector3.Distance(unified[unified.Count - 1], c) < 0.2f)
                            continue;
                        unified.Add(c);
                    }
                }
                else
                {
                    Debug.LogWarning($"[Mission2] NavMesh path leg {i}->{i+1} failed; using direct checkpoint interpolation.");
                    if (unified.Count == 0 || Vector3.Distance(unified[unified.Count - 1], pStart) > 0.2f)
                        unified.Add(pStart);
                    unified.Add(pEnd);
                }
            }

            float len = 0f;
            for (int i = 1; i < unified.Count; i++) len += Vector3.Distance(unified[i - 1], unified[i]);
            Debug.Log($"[Mission2] Built NavMesh chase route: {unified.Count} corners, total distance = {len:F1}m (Skybridge -> Lower Alley).");
            return unified;
        }

        // ─────────────────────────────────────────────────────────────────────
        // BRIEFING
        // ─────────────────────────────────────────────────────────────────────
        private IEnumerator BriefingRoutine()
        {
            state = M2State.Briefing;
            // Mission 2 has its own soundtrack: silence the city BGM until the mission ends.
            Cyberpunk.Audio.CyberBackgroundMusic.Instance?.SuspendForMission(1.5f);
            yield return new WaitForSecondsRealtime(3.0f);
            if (state != M2State.Briefing) yield break;

            // Ensure player is at the bridge for the narrative start if far away
            if (_playerTransform != null && Vector3.Distance(_playerTransform.position, M1BridgeDefault) > 40.0f)
            {
                SendPlayerToBridgeStart();
            }

            if (uiManager != null)
            {
                string brief =
                    "Alita... if you are decrypting this emergency cache, the Factory hit squads found us on the bridge.\n\n" +
                    "They can never erase who you are. A Razor scalper right here by the bridge is flashing a spare Pit pass.\n\n" +
                    "Take the pass with your blade. Enter the Pit arena... and find who ordered the strike.";
                uiManager.ShowIntelPopup("Dr. Ido [Final Cache]", brief, "[ TRACK THE SCALPER ]", "[Enter] Jack In", OnBriefAccepted, null);
                Debug.Log("[Mission2] M2 briefing emergency cache shown.");
                StartCoroutine(BriefingFailsafeRoutine());
            }
            else
            {
                Debug.LogWarning("[Mission2] No UIManager for briefing; starting hunt directly.");
                OnBriefAccepted();
            }
        }

        /// <summary>
        /// If another system hijacks the dialogue box during the briefing, the accept callback can be lost
        /// and Mission 2 would sit in Briefing forever. Start the hunt once the dialogue HUD has been idle for a while.
        /// </summary>
        private IEnumerator BriefingFailsafeRoutine()
        {
            float idle = 0f;
            yield return new WaitForSecondsRealtime(2.0f);
            while (state == M2State.Briefing)
            {
                var hud = Alita.UI.CyberpunkDialogueHUD.Instance;
                bool open = hud != null && hud.isDialogueActive;
                idle = open ? 0f : idle + Time.unscaledDeltaTime;
                if (idle > 2.5f)
                {
                    Debug.LogWarning("[Mission2] Briefing callback was lost; starting the hunt via failsafe.");
                    if (uiManager != null) uiManager.DismissPopup();
                    if (state == M2State.Briefing) OnBriefAccepted();
                    yield break;
                }
                yield return null;
            }
        }

        private void OnBriefAccepted()
        {
            Debug.Log("[Mission2] Briefing accepted; spawning hunt at bridge.");
            SpawnHunt();
            state = M2State.HuntingMark;
            SetHuntQuest();
            EnqueueBriefingLines();
        }

        private void EnqueueBriefingLines()
        {
            ClearSubtitles();
            EnqueueSub("DR. DYSON IDO [POST-MORTEM CACHE]", "EMERGENCY CONTINGENCY LOG", "Alita... the hit squads will scour the bridge. You need to get into the Pit.", 4.0f);
            EnqueueSub("DR. DYSON IDO [POST-MORTEM CACHE]", "EMERGENCY CONTINGENCY LOG", "There's a Razor scalper with a pass at the far end of the bridge. Track him down!", 4.0f);
        }

        // ─────────────────────────────────────────────────────────────────────
        // HUNT SPAWN
        // ─────────────────────────────────────────────────────────────────────
        private void SpawnHunt()
        {
            DespawnFighter();
            if (_playerTransform == null) ResolvePlayer();

            // Build verified 210m+ NavMesh route
            _route = BuildNavMeshChaseRoute();
            if (_route.Count < 2)
            {
                Debug.LogError("[Mission2] NavMesh route construction failed completely.");
                state = M2State.Inactive;
                return;
            }
            _vanPoint = _route[_route.Count - 1];

            // Scalper spawns on the bridge deck along the route
            Vector3 spawnCandidate = ScalperInitialSpawn;
            if (NavMesh.SamplePosition(spawnCandidate, out NavMeshHit hit, 4.0f, NavMesh.AllAreas))
            {
                spawnCandidate = hit.position;
            }
            else if (_route.Count > 0)
            {
                spawnCandidate = _route[0];
            }

            string scalperPath = "Assets/Prefabs/Mission2_Scalper_Mercenary.prefab";
            _scalperObj = SpawnFighter(spawnCandidate, "GOLD SCALPER", scalperPath);
            if (_scalperObj == null)
            {
                Debug.LogError("[Mission2] Scalper spawn failed; aborting Mission 2.");
                state = M2State.Inactive;
                return;
            }

            // Ensure Sleek Cyber-Mercenary visual hierarchy (never Doctor Ido or generic street model)
            ApplyScalperMercenaryLook(_scalperObj);

            _scalperCtrl = _scalperObj.GetComponent<InsaneGunSwordController>();
            _scalperCC = _scalperObj.GetComponent<CharacterController>();
            _scalperAnim = _scalperObj.GetComponentInChildren<Animator>();
            _scalperAnimHasMoveSpeed = HasAnimParam(_scalperAnim, "MoveSpeed");

            // Disable CharacterController and set all bone colliders to trigger during chase to prevent getting stuck on map props/railings
            if (_scalperCC != null) _scalperCC.enabled = false;
            var cols = _scalperObj.GetComponentsInChildren<Collider>(true);
            foreach (var col in cols)
            {
                if (col is CharacterController) continue;
                col.isTrigger = true;
            }

            ApplyCustomStats(_scalperCtrl, 160f, 12f, 18f);
            if (_scalperCtrl != null)
            {
                _scalperCtrl.controlMode = InsaneGunSwordController.ControlMode.TrainingDummy;
                _scalperCtrl.inputLocked = true;
                _scalperCtrl.autoReviveOnDeath = false;
                _scalperCtrl.OnDeath += OnScalperDied;
            }

            SetChaseAnim(_scalperAnim, _scalperAnimHasMoveSpeed, 0f);

            // Presentation: Markers, Van & Navigation Tracer
            BuildGoldMarker();
            BuildVanProp();
            BuildTracerLine();

            // Reset Chase variables
            _routeLeg = 0;
            _chaseTime = 0f;
            _timeLeft = ChaseTimeout;
            _spookFired = false;
            _toast30Shown = false;
            _kneed = false;
            _corpseLootable = false;
            _looted = false;
            _deathHandled = false;
            _cornerCombatLive = false;
            _lastScalperPos = _scalperObj.transform.position;
            _cornerStuckTimer = 0f;

            Debug.Log($"[Mission2] Hunt live: Scalper standing at {_scalperObj.transform.position}. Van parked at {_vanPoint}.");
        }

        private GameObject SpawnFighter(Vector3 pos, string fighterName, string prefabPath)
        {
            GameObject prefabToUse = null;
#if UNITY_EDITOR
            if (fighterName == "GOLD SCALPER")
            {
                prefabToUse = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Mission2_Scalper_Mercenary.prefab");
            }
#endif
            if (prefabToUse == null && fighterName != "GOLD SCALPER" && m1 != null && m1.squadEnemyPrefabs != null && m1.squadEnemyPrefabs.Length > 0 && m1.squadEnemyPrefabs[0] != null)
            {
                prefabToUse = m1.squadEnemyPrefabs[0];
            }
#if UNITY_EDITOR
            if (prefabToUse == null)
            {
                prefabToUse = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            }
#endif
            if (prefabToUse == null && m1 != null) prefabToUse = m1.enemyPrefab;
            if (prefabToUse == null)
            {
                Debug.LogError($"[Mission2] No prefab found for '{fighterName}'.");
                return null;
            }

            Vector3 toPlayer = (_playerTransform != null ? _playerTransform.position : M1BridgeDefault) - pos;
            toPlayer.y = 0f;
            Quaternion rot = toPlayer.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toPlayer.normalized) : Quaternion.identity;

            GameObject go = Instantiate(prefabToUse, pos, rot);
            go.name = fighterName;

            // Remove any leftover Mission1_EnemyTracker from the prefab until cornered combat
            var tracker = go.GetComponent<Mission1_EnemyTracker>();
            if (tracker != null) Destroy(tracker);

            if (fighterName == "GOLD SCALPER")
            {
                ApplyScalperMercenaryLook(go);
            }

            return go;
        }

        private void ApplyScalperMercenaryLook(GameObject go)
        {
            if (go == null) return;

            // Activate SM_Chr_Mercenary_Male_01 mesh and disable any other body meshes
            Transform[] allChildren = go.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in allChildren)
            {
                if (t.name == "SM_Chr_Mercenary_Male_01")
                {
                    t.gameObject.SetActive(true);
                }
                else if (t.name.StartsWith("SM_Chr_") && !t.name.Contains("Attach"))
                {
                    t.gameObject.SetActive(false);
                }
            }

            // Configure Amber-Gold cyber rim light
            var lights = go.GetComponentsInChildren<Light>(true);
            foreach (var l in lights)
            {
                l.color = new Color(1.0f, 0.75f, 0.08f, 1.0f);
                l.intensity = Mathf.Max(l.intensity, 2.5f);
                l.enabled = true;
            }
        }

        private void ApplyCustomStats(InsaneGunSwordController ctrl, float hp, float dmg, float heavy)
        {
            if (ctrl == null) return;
            ctrl.maxHealth = hp;
            ctrl.currentHealth = hp;
            ctrl.attackDamage = dmg;
            ctrl.heavyAttackDamage = heavy;
        }

        private void UnbindFighterEvents()
        {
            if (_scalperCtrl != null) _scalperCtrl.OnDeath -= OnScalperDied;
        }

        private void DespawnFighter()
        {
            UnbindFighterEvents();
            if (_scalperObj != null) Destroy(_scalperObj);
            var existing = GameObject.Find("GOLD SCALPER");
            if (existing != null) Destroy(existing);
            _scalperObj = null;
            _scalperCtrl = null;
            _scalperCC = null;
            _scalperTracker = null;
            _scalperAnim = null;
        }

        // ─────────────────────────────────────────────────────────────────────
        // UPDATE: STATE MACHINE & CHASE CONTROLLER
        // ─────────────────────────────────────────────────────────────────────
        private void Update()
        {
            PumpSubtitles();

            // Debug hotkeys for rapid testing
            if (Input.GetKeyDown(KeyCode.F2))
            {
                Debug.Log("[Mission2] F2 pressed -> Fast trigger / restart Mission 2.");
                DebugStartMission2();
            }

            if (state == M2State.Inactive || state == M2State.Briefing) return;
            if (_playerTransform == null || _scalperObj == null) return;

            // Handle player death mid-mission
            if (!_deathHandled && IsPlayerDead())
            {
                _deathHandled = true;
                Debug.Log("[Mission2] Player downed; restarting hunt.");
                ResetHunt();
                return;
            }
            if (_deathHandled && !IsPlayerDead()) _deathHandled = false;

            Vector3 sPos = _scalperObj.transform.position;
            Vector3 pPos = _playerTransform.position;
            float distPlayerScalper = Vector3.Distance(pPos, sPos);

            if (state == M2State.HuntingMark)
            {
                // Spook trigger at 12m
                if (distPlayerScalper <= SpookDist && !_spookFired)
                {
                    TriggerSpook(distPlayerScalper);
                }
            }
            else if (state == M2State.Chasing)
            {
                UpdateChase(distPlayerScalper);
            }
            else if (state == M2State.CorneredFight)
            {
                PollKnee();
                TryLoot(distPlayerScalper);
            }
            else if (state == M2State.LootPass)
            {
                TryLoot(distPlayerScalper);
            }

            UpdateAIWaypointAndTracer(distPlayerScalper);
        }

        private void LateUpdate()
        {
            if (state == M2State.Chasing)
            {
                MoveScalperAlongNavMeshRoute();
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // CHASE LOGIC & CORNER CONDITIONS
        // ─────────────────────────────────────────────────────────────────────
        private void TriggerSpook(float dist)
        {
            ClearSubtitles();
            _spookFired = true;
            state = M2State.Chasing;
            _chaseTime = 0f;
            _timeLeft = ChaseTimeout;
            _lastShownSecond = -1;
            _strayTetherTimer = -1f;

            Debug.Log("[Mission2] Scalper SPOOKED! 30s+ chase sequence engaged.");
            Mission1_AudioManager.Instance?.StartChaseBGM();

            if (CyberpunkHUDController.Instance != null)
                CyberpunkHUDController.Instance.ShowToast("HE MADE YOU! RUN HIM DOWN", 3.0f);

            EnqueueSub("GOLD SCALPER", "RAZOR PASS SCALPER", "That's the bridge psycho! RUN!", 2.5f);
            EnqueueSub("DR. DYSON IDO", "CYBER-SURGEON & REPAIR SPECIALIST", "He made you! RUN HIM DOWN, don't let him reach his van!", 3.0f);

            SetChaseQuest(_timeLeft, dist);
        }

        private void UpdateChase(float distPlayerScalper)
        {
            _chaseTime += Time.deltaTime;
            _timeLeft -= Time.deltaTime;

            int whole = Mathf.Max(0, Mathf.CeilToInt(_timeLeft));
            if (whole != _lastShownSecond)
            {
                _lastShownSecond = whole;
                SetChaseQuest(_timeLeft, distPlayerScalper);
            }

            // Milestone: 30s minimum chase reached
            if (!_toast30Shown && _chaseTime >= ChaseMinDuration)
            {
                _toast30Shown = true;
                Debug.Log("[Mission2] 30s MINIMUM CHASE REACHED! Scalper is tiring out.");
                if (CyberpunkHUDController.Instance != null)
                    CyberpunkHUDController.Instance.ShowToast("30S ON HIS HEELS // KEEP PRESSURE! CORNER HIM NOW", 3.0f);
                EnqueueSub("DR. DYSON IDO", "CYBER-SURGEON & REPAIR SPECIALIST", "He's losing his breath! Cut him off, corner him now!", 3.0f);
            }

            // Chase timeout check (120s limit)
            if (_timeLeft <= 0f)
            {
                Debug.LogWarning("[Mission2] Chase clock expired (120s); mark escaped.");
                EnterFailedEscaped(EscapeReason.Timeout);
                return;
            }

            // Hard 22m Escape Check: If the Scalper gets > 22 meters away, fail mission!
            if (distPlayerScalper > MaxEscapeDist)
            {
                Debug.LogWarning($"[Mission2] Scalper escaped! Distance: {distPlayerScalper:F1}m > {MaxEscapeDist}m. Failing mission.");
                EnterFailedEscaped(EscapeReason.TooFar);
                return;
            }

            // Warning callouts when scalper is pulling away (> 16m)
            if (distPlayerScalper > EscapeWarningDist)
            {
                if (Time.time - _lastStrayWarningTime > 5.0f)
                {
                    _lastStrayWarningTime = Time.time;
                    EnqueueSub("DR. DYSON IDO", "CYBER-SURGEON & REPAIR SPECIALIST", "He's pulling away! Stay close or he'll lose you!", 2.5f);
                    if (CyberpunkHUDController.Instance != null)
                        CyberpunkHUDController.Instance.ShowToast($"[!] DANGER: TARGET PULLING AWAY ({distPlayerScalper:F0}m / 22m MAX)", 2.0f);
                }
            }

            // CORNERED TRIGGER:
            // 1. Minimum 30s chase duration MUST be fulfilled.
            // 2. Either player closes within CornerProximityDist (6.5m) OR Scalper reaches the final alley van.
            bool minTimeReached = _chaseTime >= ChaseMinDuration;
            bool closeProximity = distPlayerScalper <= CornerProximityDist;
            bool reachedVan = _routeLeg >= _route.Count - 1;

            if (minTimeReached && (closeProximity || reachedVan))
            {
                Debug.Log($"[Mission2] Scalper CORNERED! chaseTime={_chaseTime:F1}s, dist={distPlayerScalper:F1}m, reachedVan={reachedVan}. Entering combat!");
                EnterCorneredFight();
            }
        }

        private void MoveScalperAlongNavMeshRoute()
        {
            if (_scalperObj == null || _scalperCtrl == null || _scalperCtrl.IsDead) return;
            if (_playerTransform == null || _route == null || _route.Count == 0) return;

            if (_routeLeg >= _route.Count) _routeLeg = _route.Count - 1;
            Vector3 targetCorner = _route[_routeLeg];

            float dist3D = Vector3.Distance(_scalperObj.transform.position, targetCorner);
            float dist2D = Vector2.Distance(
                new Vector2(_scalperObj.transform.position.x, _scalperObj.transform.position.z),
                new Vector2(targetCorner.x, targetCorner.z)
            );

            // Precise corner advance: prevents cutting corner 0.8m early into buildings/props
            bool cornerReached = (dist3D <= 0.40f) || (dist2D <= 0.35f && Mathf.Abs(_scalperObj.transform.position.y - targetCorner.y) < 1.2f);

            // Anti-stuck watchdog: if blocked against obstacle for >0.7s, advance corner
            if (Vector3.Distance(_scalperObj.transform.position, _lastScalperPos) < 0.04f)
            {
                _cornerStuckTimer += Time.deltaTime;
                if (_cornerStuckTimer > 0.70f)
                {
                    cornerReached = true;
                    _cornerStuckTimer = 0f;
                }
            }
            else
            {
                _cornerStuckTimer = 0f;
            }
            _lastScalperPos = _scalperObj.transform.position;

            if (cornerReached)
            {
                if (_routeLeg < _route.Count - 1)
                {
                    _routeLeg++;
                    targetCorner = _route[_routeLeg];
                }
                else
                {
                    // Reached final waypoint at the van
                    if (_chaseTime >= ChaseMinDuration)
                    {
                        EnterCorneredFight();
                        return;
                    }
                    else
                    {
                        // Defensive hold near van until 30s minimum chase time expires
                        Vector3 facePlayer = _playerTransform.position - _scalperObj.transform.position;
                        facePlayer.y = 0f;
                        if (facePlayer.sqrMagnitude > 0.01f)
                        {
                            Quaternion lookRot = Quaternion.LookRotation(facePlayer.normalized);
                            _scalperObj.transform.rotation = Quaternion.Slerp(_scalperObj.transform.rotation, lookRot, Time.deltaTime * 6.0f);
                        }
                        SetChaseAnim(_scalperAnim, _scalperAnimHasMoveSpeed, 0f);
                        return;
                    }
                }
            }

            // Pacing rubber-banding
            float distToPlayer = Vector3.Distance(_playerTransform.position, _scalperObj.transform.position);
            float currentSpeed = ScalperBaseSpeed;

            if (distToPlayer < 5.0f && _chaseTime < ChaseMinDuration)
            {
                // Burst sprint if player catches up before 30s
                currentSpeed = ScalperBurstSpeed;
                if (Time.time - _lastBurstCalloutTime > 8.0f)
                {
                    _lastBurstCalloutTime = Time.time;
                    EnqueueSub("GOLD SCALPER", "RAZOR PASS SCALPER", "Get off my back! You won't catch me that easy!", 2.5f);
                }
            }
            else if (distToPlayer > 18.0f)
            {
                // Ease pace so player doesn't lose sight
                currentSpeed = ScalperEaseSpeed;
            }

            Vector3 toTarget = targetCorner - _scalperObj.transform.position;
            Vector3 moveDir = toTarget;
            moveDir.y = 0f;

            // Move step along NavMesh
            Vector3 step = Vector3.MoveTowards(_scalperObj.transform.position, targetCorner, currentSpeed * Time.deltaTime);

            // Sample ground strictly on NavMesh surface without accidental vertical level hopping
            if (NavMesh.SamplePosition(step, out NavMeshHit navHit, 0.6f, NavMesh.AllAreas))
            {
                if (Mathf.Abs(navHit.position.y - step.y) < 1.0f)
                {
                    step = navHit.position;
                }
            }

            _scalperObj.transform.position = step;

            if (moveDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(moveDir.normalized);
                _scalperObj.transform.rotation = Quaternion.Slerp(_scalperObj.transform.rotation, targetRot, Time.deltaTime * 8.0f);
            }

            SetChaseAnim(_scalperAnim, _scalperAnimHasMoveSpeed, currentSpeed);
        }

        private void SetChaseAnim(Animator anim, bool hasParam, float speed)
        {
            if (anim == null || !hasParam || !anim.isActiveAndEnabled) return;
            anim.SetFloat("MoveSpeed", speed);
        }

        private bool HasAnimParam(Animator anim, string paramName)
        {
            if (anim == null || anim.runtimeAnimatorController == null) return false;
            foreach (var p in anim.parameters)
            {
                if (p.name == paramName) return true;
            }
            return false;
        }

        // ─────────────────────────────────────────────────────────────────────
        // CORNERED FIGHT (1-on-1 DUEL)
        // ─────────────────────────────────────────────────────────────────────
        private void EnterCorneredFight()
        {
            if (state == M2State.CorneredFight) return;
            state = M2State.CorneredFight;
            _cornerCombatLive = false;

            // Turn Scalper to face the player
            if (_scalperObj != null && _playerTransform != null)
            {
                Vector3 toPlayer = _playerTransform.position - _scalperObj.transform.position;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude > 0.01f)
                    _scalperObj.transform.rotation = Quaternion.LookRotation(toPlayer.normalized);
            }

            SetChaseAnim(_scalperAnim, _scalperAnimHasMoveSpeed, 0f);

            Debug.Log("[Mission2] Scalper cornered! Preparing 1-on-1 sword fight.");
            ClearSubtitles();
            EnqueueSub("GOLD SCALPER", "RAZOR PASS SCALPER", "Dead end... fine! You want this Arena Pass?! Come take it!", 2.8f);
            EnqueueSub("DR. DYSON IDO", "CYBER-SURGEON & REPAIR SPECIALIST", "He's cornered! Draw your blade, disarm him!", 2.8f);

            StartCoroutine(ActivateFightAfterDialogue());
        }

        private IEnumerator ActivateFightAfterDialogue()
        {
            yield return new WaitForSecondsRealtime(3.0f);
            if (state != M2State.CorneredFight || _scalperObj == null) yield break;

            // Restore all bone colliders to physical hitboxes for combat
            var cols = _scalperObj.GetComponentsInChildren<Collider>(true);
            foreach (var col in cols)
            {
                if (col is CharacterController) continue;
                col.isTrigger = false;
            }

            // Enable CharacterController for physics/combat
            if (_scalperCC != null) _scalperCC.enabled = true;

            // Attach combat tracker and unleash combat AI
            if (_scalperCtrl != null && !_scalperCtrl.IsDead)
            {
                _scalperTracker = _scalperObj.GetComponent<Mission1_EnemyTracker>();
                if (_scalperTracker == null) _scalperTracker = _scalperObj.AddComponent<Mission1_EnemyTracker>();
                _scalperTracker.activationDelay = 0f;
                _scalperTracker.aggroDistance = 15f;
                _scalperTracker.SetIdentity("GOLD SCALPER", _playerTransform);
                _scalperTracker.ActivateCombatAI();

                _scalperCtrl.isInvincible = false;
                _scalperCtrl.inputLocked = false;
                ApplyCustomStats(_scalperCtrl, 160f, 12f, 18f);
            }

            _cornerCombatLive = true;

            if (CyberpunkHUDController.Instance != null)
            {
                CyberpunkHUDController.Instance.SetQuestObjective(
                    "// CORNERED // DEFEAT THE SCALPER",
                    "> Drop the scalper to 30% HP — then RIP the pass [E]");
                CyberpunkHUDController.Instance.ShowToast("CORNERED // TAKE THE PASS", 3.0f);
            }
        }

        private void PollKnee()
        {
            if (_kneed || _corpseLootable || _scalperCtrl == null || _scalperCtrl.IsDead) return;
            if (_scalperCtrl.maxHealth <= 0f) return;

            if (_scalperCtrl.currentHealth / _scalperCtrl.maxHealth <= KneeFrac)
            {
                _kneed = true;
                if (_scalperTracker != null) _scalperTracker.enabled = false;
                _scalperCtrl.controlMode = InsaneGunSwordController.ControlMode.TrainingDummy;
                _scalperCtrl.inputLocked = true;
                SetChaseAnim(_scalperAnim, _scalperAnimHasMoveSpeed, 0f);

                Debug.Log("[Mission2] Scalper on knees at 30% HP! RIP PASS prompt active.");
                ClearSubtitles();
                EnqueueSub("GOLD SCALPER", "RAZOR PASS SCALPER", "Gah! Stop... take the pass! Don't scrap me!", 3.0f);
                if (CyberpunkHUDController.Instance != null)
                    CyberpunkHUDController.Instance.ShowToast("PRESS [E] // RIP PASS", 3.5f);
            }
        }

        private void OnScalperDied(InsaneGunSwordController enemy)
        {
            Debug.Log("[Mission2] Scalper defeated; body lootable for pass.");
            SetChaseAnim(_scalperAnim, _scalperAnimHasMoveSpeed, 0f);
            if (!_looted)
            {
                _corpseLootable = true;
                if (CyberpunkHUDController.Instance != null)
                    CyberpunkHUDController.Instance.ShowToast("HE DROPPED IT // [E] RIP PASS", 3.5f);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // LOOT PASS & MISSION COMPLETE
        // ─────────────────────────────────────────────────────────────────────
        private void TryLoot(float distPlayerScalper)
        {
            if (_looted || (!_kneed && !_corpseLootable)) return;
            if (uiManager != null && uiManager.IsPopupOpen()) return;
            if (Cyberpunk.Inventory.CyberpunkInventoryUI.IsOpen) return;
            if (!Input.GetKeyDown(KeyCode.E)) return;
            if (distPlayerScalper > LootDist) return;

            LootPass();
        }

        private void LootPass()
        {
            _looted = true;
            state = M2State.LootPass;

            Mission1_AudioManager.Instance?.StopMissionBGM(2.0f);
            Mission1_AudioManager.Instance?.PlayUIConfirm();

            if (_tracerObj != null) _tracerObj.SetActive(false);
            if (_goldMarker != null) _goldMarker.SetActive(false);
            if (uiManager != null) uiManager.UpdateWaypointHUD(_scalperObj.transform.position, false);

            // Add Arena Pass to player inventory
            Cyberpunk.Inventory.CyberpunkInventory.EnsureInScene();
            if (Cyberpunk.Inventory.CyberpunkInventory.Instance != null)
                Cyberpunk.Inventory.CyberpunkInventory.Instance.AddItem("arena_pass", 1);

            Debug.Log("[Mission2] ARENA PASS // STOLEN.");
            if (CyberpunkHUDController.Instance != null)
                CyberpunkHUDController.Instance.ShowToast("ARENA PASS // STOLEN", 4.0f);

            ClearSubtitles();
            EnqueueSub("DR. DYSON IDO", "CYBER-SURGEON & REPAIR SPECIALIST", "That's it... that's our ticket. The ARENA PASS.", 3.8f);
            EnqueueSub("ALITA", "MARTIAN RELIC", "Stealing to fight for money... is this what Iron City is?", 3.5f);
            EnqueueSub("DR. DYSON IDO", "CYBER-SURGEON & REPAIR SPECIALIST", "This is how we survive, ghost. Tomorrow night you walk into the Pit legal. Rest that blade.", 4.8f);

            StartCoroutine(FinishMissionRoutine(12.5f));
        }

        private IEnumerator FinishMissionRoutine(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            if (state != M2State.LootPass) yield break;
            state = M2State.Completed;
            Cyberpunk.Audio.CyberBackgroundMusic.Instance?.ResumeAfterMission();

            // Activate Mission 3 navigation trail and radar marker
            Cyberpunk.World.PitSkyLiftSystem.ActivateMission3();

            if (CyberpunkHUDController.Instance != null)
            {
                CyberpunkHUDController.Instance.SetQuestObjective(
                    "// M3: REACH THE PIT ARENA",
                    "> Follow the navigation trail to the Far Sky Lift\n> Ascend to Upper Plaza and enter The Pit");
                CyberpunkHUDController.Instance.ShowToast("[*] MISSION 2 COMPLETE // M3 UNLOCKED: THE PIT", 5.0f);
            }
            Debug.Log("[Mission2] MISSION 2: COMPLETE // M3 UNLOCKED: THE PIT.");

            if (uiManager != null)
            {
                uiManager.ShowIntelPopup("Dr. Ido",
                    "ARENA PASS secured, ghost. Flash that chip at the Pit door tomorrow night and they have to let you in.\n\nYou fight for real money now — Corp-free, crowd-loud, no questions.",
                    "[ ENTER THE PIT ]", "[Enter] Continue", null, null);
            }
        }

        private void EnterFailedEscaped(EscapeReason reason)
        {
            state = M2State.Failed_Escaped;
            Mission1_AudioManager.Instance?.StopMissionBGM(1.5f);

            string toastMsg;
            string subText;
            string popupBody;

            switch (reason)
            {
                case EscapeReason.TooFar:
                    toastMsg = "[-] MISSION FAILED // THE SCALPER ESCAPED (>22M)";
                    subText = "He got away! The scalper broke away and escaped into the crowd!";
                    popupBody = "MISSION FAILED: TARGET ESCAPED!\n\nThe Scalper pulled more than 22 meters away and slipped into the lower alleys.\n\nReturn to the Sector 7 Skybridge to acquire his trail again.";
                    break;
                case EscapeReason.Timeout:
                    toastMsg = "[-] MISSION FAILED // TIME EXPIRED";
                    subText = "He vanished into the service alleys! Get back to the bridge and pick up his trail again!";
                    popupBody = "MISSION FAILED: CHASE TIMED OUT!\n\nThe trail went cold. The Scalper gave you the slip in the service corridors.\n\nReturn to the bridge deck to retry.";
                    break;
                default: // VanEscape
                    toastMsg = "[-] MISSION FAILED // REACHED GETAWAY VAN";
                    subText = "He reached his getaway van! He escaped into the Upper City!";
                    popupBody = "MISSION FAILED: TARGET ESCAPED!\n\nThe Scalper reached his getaway vehicle and extracted from Sector 7.\n\nReturn to the bridge deck to cut him off.";
                    break;
            }

            Debug.LogWarning($"[Mission2] Chase failed ({reason}).");

            ClearSubtitles();
            EnqueueSub("DR. DYSON IDO", "CYBER-SURGEON & REPAIR SPECIALIST", subText, 4.0f);

            if (CyberpunkHUDController.Instance != null)
            {
                CyberpunkHUDController.Instance.ShowToast(toastMsg, 4.5f);
                CyberpunkHUDController.Instance.SetQuestObjective(
                    "// MISSION FAILED",
                    "> The Scalper escaped\n> Return to the Skybridge to retry the hunt");
            }

            if (uiManager != null)
            {
                uiManager.ShowIntelPopup("Dr. Ido", popupBody,
                    "[ RETRY HUNT ]", "[Enter] Retry", () => ResetHunt(true), null);
            }
            else
            {
                ResetHunt(true);
            }
        }

        public void ResetHunt(bool sendPlayerToBridge = true)
        {
            Debug.Log("[Mission2] Resetting hunt...");
            Mission1_AudioManager.Instance?.StopMissionBGM(1.0f);
            if (sendPlayerToBridge) SendPlayerToBridgeStart();
            ClearSubtitles();
            SpawnHunt();
            state = M2State.HuntingMark;
            SetHuntQuest();
        }

        private void SendPlayerToBridgeStart()
        {
            if (_playerTransform == null || _playerGO == null) ResolvePlayer();
            if (_playerTransform == null || _playerGO == null) return;

            Vector3 spot = M1BridgeDefault;
            if (NavMesh.SamplePosition(spot, out NavMeshHit h, 4f, NavMesh.AllAreas))
                spot = h.position;

            CharacterController cc = _playerGO.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            _playerTransform.position = spot;
            if (cc != null) cc.enabled = true;
            Debug.Log($"[Mission2] Player reset to Skybridge deck {spot}.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // AI NAVIGATION TRAIL & FLOATING TARGET MARKER
        // ─────────────────────────────────────────────────────────────────────
        private void BuildTracerLine()
        {
            if (_tracerObj != null) Destroy(_tracerObj);
            _tracerObj = new GameObject("Mission2_AITracerLine");
            _tracerObj.transform.SetParent(transform, false);

            _tracerLine = _tracerObj.AddComponent<LineRenderer>();
            _tracerLine.startWidth = 0.20f; // Minimalist thin line
            _tracerLine.endWidth = 0.20f;
            _tracerLine.positionCount = 0;
            _tracerLine.useWorldSpace = true;
            _tracerLine.alignment = LineAlignment.TransformZ;
            _tracerLine.numCapVertices = 4;
            _tracerLine.numCornerVertices = 4;
            _tracerLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _tracerLine.receiveShadows = false;

            Shader pShader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                          ?? Shader.Find("Universal Render Pipeline/Unlit")
                          ?? Shader.Find("Sprites/Default");

            Material mat = new Material(pShader);
            Color cyan = new Color(0f, 0.941f, 1f, 0.90f);
            mat.color = cyan;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", cyan);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", cyan * 2.5f);
            }
            _tracerLine.sharedMaterial = mat;
            _tracerNavPath = new NavMeshPath();
        }

        private void BuildGoldMarker()
        {
            if (_scalperObj == null) return;
            if (_goldMarker != null) Destroy(_goldMarker);

            _goldMarker = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _goldMarker.name = "GoldChip_Marker";
            _goldMarker.transform.SetParent(_scalperObj.transform, false);
            _goldMarker.transform.localPosition = new Vector3(0f, 2.3f, 0f);
            _goldMarker.transform.localRotation = Quaternion.Euler(0f, 0f, 45f); // Diamond orientation
            _goldMarker.transform.localScale = new Vector3(0.55f, 0.55f, 1f);
            Destroy(_goldMarker.GetComponent<Collider>());

            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                       ?? Shader.Find("Universal Render Pipeline/Unlit")
                       ?? Shader.Find("Sprites/Default");
            Material mat = new Material(sh);
            Color gold = new Color(1f, 0.78f, 0f, 1f);
            mat.color = gold;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", gold);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", gold * 2.8f);
            }
            _goldMarker.GetComponent<Renderer>().sharedMaterial = mat;
            _goldMarker.AddComponent<MissionIconBillboardBob>();
        }

        private void BuildVanProp()
        {
            if (_vanProp != null) Destroy(_vanProp);

            GameObject vanPrefab = null;
#if UNITY_EDITOR
            vanPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Vehicles_Hover/Hover_Food_Van.prefab")
                     ?? UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonCyberCity/Prefabs/Vehicles/SM_Veh_Luxury_Car_01.prefab");
#endif
            if (vanPrefab != null)
            {
                _vanProp = Instantiate(vanPrefab);
                _vanProp.name = "Scalper_Van_Runtime";
                _vanProp.transform.position = _vanPoint + new Vector3(-2.2f, 0.1f, 0.5f);
                _vanProp.transform.rotation = Quaternion.Euler(0f, 25f, 0f);
                var hoverCar = _vanProp.GetComponent<CyberpunkDev.Traffic.HoverCar>();
                if (hoverCar != null) hoverCar.enabled = false;
                var rb = _vanProp.GetComponent<Rigidbody>();
                if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }
                foreach (var c in _vanProp.GetComponentsInChildren<Collider>()) c.isTrigger = true;
            }
            else
            {
                _vanProp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _vanProp.name = "Scalper_Van_Runtime";
                _vanProp.transform.position = _vanPoint + new Vector3(-2.2f, 0.7f, 0.5f);
                _vanProp.transform.localScale = new Vector3(1.8f, 1.4f, 2.4f);
                Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                Material dark = new Material(sh);
                dark.color = new Color(0.05f, 0.05f, 0.07f, 1f);
                _vanProp.GetComponent<Renderer>().sharedMaterial = dark;
                var col = _vanProp.GetComponent<Collider>();
                if (col != null) col.isTrigger = true;
            }
            Debug.Log($"[Mission2] Scalper getaway van parked offset from {_vanPoint}.");
        }

        private void UpdateAIWaypointAndTracer(float distPlayerScalper)
        {
            bool show = (state == M2State.HuntingMark || state == M2State.Chasing || state == M2State.CorneredFight)
                        && _playerTransform != null && _scalperObj != null;

            if (uiManager != null)
            {
                string label;
                if (_kneed || _corpseLootable) label = "RIP PASS";
                else if (state == M2State.Chasing)
                {
                    int t = Mathf.Max(0, Mathf.CeilToInt(_timeLeft));
                    label = $"SCALPER // {distPlayerScalper:F0}m ({t / 60}:{t % 60:D2})";
                }
                else if (state == M2State.CorneredFight) label = "DEFEAT SCALPER";
                else label = $"SCALPER // {distPlayerScalper:F0}m";

                uiManager.UpdateWaypointHUD(_scalperObj != null ? _scalperObj.transform.position : M1BridgeDefault, show, label);
            }

            if (!show || _tracerLine == null)
            {
                if (_tracerObj != null && _tracerObj.activeSelf) _tracerObj.SetActive(false);
                return;
            }

            if (!_tracerObj.activeSelf) _tracerObj.SetActive(true);

            // Throttle path calculation to ~10 Hz
            if (Time.time - _lastTracerCalcTime < 0.10f && _tracerLine.positionCount > 0) return;
            _lastTracerCalcTime = Time.time;

            if (state == M2State.CorneredFight && distPlayerScalper < 4.0f)
            {
                _tracerLine.positionCount = 0;
                return;
            }

            Vector3 startPos = _playerTransform.position;
            if (NavMesh.SamplePosition(startPos, out NavMeshHit sHit, 4.0f, NavMesh.AllAreas))
                startPos = sHit.position;

            Vector3 endPos = _scalperObj.transform.position;
            if (NavMesh.SamplePosition(endPos, out NavMeshHit eHit, 4.0f, NavMesh.AllAreas))
                endPos = eHit.position;

            if (_tracerNavPath == null) _tracerNavPath = new NavMeshPath();

            bool hasPath = false;
            try
            {
                hasPath = NavMesh.CalculatePath(startPos, endPos, NavMesh.AllAreas, _tracerNavPath);
            }
            catch
            {
                _tracerNavPath = new NavMeshPath();
                try { hasPath = NavMesh.CalculatePath(startPos, endPos, NavMesh.AllAreas, _tracerNavPath); } catch {}
            }
            if (hasPath && _tracerNavPath.status != NavMeshPathStatus.PathInvalid && _tracerNavPath.corners.Length >= 2)
            {
                Vector3[] elevated = new Vector3[_tracerNavPath.corners.Length];
                for (int i = 0; i < _tracerNavPath.corners.Length; i++)
                {
                    elevated[i] = _tracerNavPath.corners[i] + Vector3.up * 0.12f;
                }
                _tracerLine.positionCount = elevated.Length;
                _tracerLine.SetPositions(elevated);
            }
            else
            {
                Vector3[] direct = new Vector3[] { startPos + Vector3.up * 0.12f, endPos + Vector3.up * 0.12f };
                _tracerLine.positionCount = 2;
                _tracerLine.SetPositions(direct);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // PLAYER / HUD / SUBTITLES
        // ─────────────────────────────────────────────────────────────────────
        private void ResolvePlayer()
        {
            var synty = FindAnyObjectByType<SyntySwordLocomotionController>();
            if (synty != null)
            {
                _playerSynty = synty;
                _playerGO = synty.gameObject;
                _playerTransform = synty.transform;
                var insane = _playerGO.GetComponent<InsaneGunSwordController>();
                if (insane != null) _playerInsaneGun = insane;
                return;
            }
            var ctrls = FindObjectsByType<InsaneGunSwordController>(FindObjectsSortMode.None);
            foreach (var c in ctrls)
            {
                if (c != null && c.controlMode == InsaneGunSwordController.ControlMode.PlayerControlled)
                {
                    _playerInsaneGun = c;
                    _playerGO = c.gameObject;
                    _playerTransform = c.transform;
                    return;
                }
            }
            var tagged = GameObject.FindWithTag("Player");
            if (tagged != null)
            {
                _playerGO = tagged;
                _playerTransform = tagged.transform;
            }
        }

        private bool IsPlayerDead()
        {
            if (_playerSynty != null && _playerSynty.currentHealth <= 0f) return true;
            if (_playerInsaneGun != null && (_playerInsaneGun.currentHealth <= 0f || _playerInsaneGun.IsDead)) return true;
            return false;
        }

        private void SetHuntQuest()
        {
            if (CyberpunkHUDController.Instance != null)
            {
                CyberpunkHUDController.Instance.SetQuestObjective(
                    "// STEAL THE ARENA PASS",
                    "> Find the gold-chip scalper on the bridge\n> Follow the cyan navigation tracer");
            }
        }

        private void SetChaseQuest(float timeLeft = ChaseTimeout, float currentDist = -1f)
        {
            if (CyberpunkHUDController.Instance != null)
            {
                int t = Mathf.Max(0, Mathf.CeilToInt(timeLeft));
                string distSuffix = currentDist >= 0f ? $" [DIST: {currentDist:F0}m / 22m]" : "";
                CyberpunkHUDController.Instance.SetQuestObjective(
                    "// CHASE THE SCALPER",
                    $"> Stay within 22m — don't lose him!{distSuffix} ({t / 60}:{t % 60:D2})\n> Follow the navigation tracer to his getaway van");
            }
        }

        private void EnqueueSub(string speaker, string title, string text, float dur)
        {
            _subQueue.Enqueue(new SubMsg(speaker, title, text, dur));
        }

        private void ClearSubtitles()
        {
            _subQueue.Clear();
            _activeSub = null;
            _subTimer = 0f;
        }

        private void PumpSubtitles()
        {
            if (_activeSub != null)
            {
                _subTimer += Time.deltaTime;
                if (_subTimer >= _activeSub.dur)
                {
                    _activeSub = null;
                    if (_subQueue.Count == 0)
                    {
                        Alita.UI.CyberpunkDialogueHUD.Instance?.CloseDialogue();
                    }
                }
                else return;
            }
            if (_subQueue.Count > 0)
            {
                _activeSub = _subQueue.Dequeue();
                _subTimer = 0f;
                Mission1_AudioManager.Instance?.PlayButtonHover();

                // Route to AAA CyberpunkDialogueHUD
                Alita.UI.CyberpunkDialogueHUD.GetOrCreate().ShowDialogue(
                    _activeSub.speaker,
                    _activeSub.text,
                    null,
                    _activeSub.title,
                    null,
                    _activeSub.dur
                );
            }
        }

        private void OnGUI()
        {
            // Subtitles are rendered via AAA CyberpunkDialogueHUD (Canvas uGUI)
        }

        // ─────────────────────────────────────────────────────────────────────
        // DEBUG CONTEXT MENUS & TEST METHODS
        // ─────────────────────────────────────────────────────────────────────
        [ContextMenu("Debug Start Mission 2")]
        public void DebugStartMission2()
        {
            StopAllCoroutines();
            if (m1 != null)
            {
                m1.state = Mission1_StreetTrialManager.MissionState.Completed;
            }
            if (Alita.AlitaCityPrologueDirector.Instance != null)
            {
                Alita.AlitaCityPrologueDirector.Instance.currentPhase = Alita.AlitaCityPrologueDirector.ProloguePhase.Completed;
            }
            ResolvePlayer();
            SendPlayerToBridgeStart();
            OnBriefAccepted();
        }

        [ContextMenu("Debug Skip To Chase")]
        public void DebugSkipToChase()
        {
            if (state == M2State.Inactive || _scalperObj == null)
            {
                DebugStartMission2();
            }
            TriggerSpook(10.0f);
        }

        [ContextMenu("Debug Corner Scalper Now")]
        public void DebugCornerScalperNow()
        {
            _chaseTime = ChaseMinDuration + 1.0f;
            EnterCorneredFight();
        }
    }
}
