using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
// NavMesh removed: escort uses Transform.MoveTowards instead
using InsaneGun.Combat;
using SyntyCombined;
using Cyberpunk.Missions;
using Cyberpunk.World;
using Random = UnityEngine.Random;

namespace Alita
{
    /// <summary>
    /// Master Runtime Director for the Iron City Walkthrough Prologue.
    /// Operates PURELY in memory at runtime without adding any objects, geometry,
    /// or props to the Demo_City map file on disk (100% map preservation).
    /// 
    /// Narrative Flow:
    /// 1. Spawns Dr. Dyson Ido's Cyber-Repair Clinic / Lab at spawn coordinates (12.55, -17.92, 57.0).
    /// 2. Spawns Alita inside the clinic with 100% immediate player control (WASD, camera, sprint, jump).
    /// 3. Spawns Dr. Ido inside the clinic facing Alita.
    /// 4. Subtitles rendered with exact cutscene frosted dark glass OnGUI style.
    /// 5. Initial conversation inside Ido's Lab:
    ///    - Alita asks where she is -> Ido explains it's his cyber-repair clinic.
    ///    - Alita asks who she is -> Ido says no identity chip, ghost in the corporate database.
    ///    - Ido invites her to step outside into the city.
    /// 6. Clinic sliding doors open smoothly; Ido steps out onto the street.
    /// 7. Alita follows Ido outside into the open city.
    /// 8. Escort walk through city streets with Ido narrating the cruel corporate reality:
    ///    - Megacorporations own everything (cyberware, transit, air).
    ///    - High tech low life: board executives vs gutter scavengers.
    ///    - Corpo Enforcers & private hit squads.
    ///    - Alita's combat reflex indexing & handoff to combat arena!
    /// </summary>
    public class AlitaCityPrologueDirector : MonoBehaviour
    {
        public static AlitaCityPrologueDirector Instance { get; private set; }

        public enum ProloguePhase
        {
            InsideClinicLab,
            DoorsOpening,
            WalkingToWaypoint,
            DialogueAtWaypoint,
            InteractiveLookAt,
            HandoffToCombat,
            Completed
        }

        [Header("Director State")]
        public ProloguePhase currentPhase = ProloguePhase.InsideClinicLab;
        public int currentWaypointIndex = 0;
        public bool autoStartPrologueOnAwake = true;
        [Tooltip("If true, overrides spawn to upper walkway Server Room door (for testing server transition). If false, spawns at Dr. Ido's clinic for Mission 1.")]
        public bool spawnAtServerRoomDoor = false;
        public bool handoffFired = false;
        public static event Action PrologueHandoffEvent;

        [Header("Actor References (Runtime Spawned)")]
        public GameObject playerObject;
        public Transform playerTransform;
        public GameObject doctorActor;
        public Transform doctorTransform;
        // NavMeshAgent removed: doctor uses simple transform locomotion
        public Animator doctorAnimator;
        public GameObject doctorPrefab;

        [Header("Camera & Controls")]
        public Camera mainCamera;
        public SmoothOrbitCamera smoothOrbitCamera;
        [HideInInspector] public bool isCinematicCameraActive = false;

        [Header("Narrative Waypoint Beats")]
        public List<PrologueDialogueBeat> narrativeBeats = new List<PrologueDialogueBeat>();

        // Escort parameters
        public float walkSpeed = 2.4f;
        public float followCatchupDist = 4.5f;
        public float waitDistance = 8.5f; // If Alita falls >8.5m behind, Ido pauses
        private Vector3 _currentEscortDest = Vector3.zero;
        private bool _escortActive = false;
        public float waypointRadius = 2.2f;

        private bool _waitingForPlayer = false;

        private Mission1_UIManager _missionUI; // cached mission UI (null-guarded; OnGUI hint is the fallback)

        private Mission1_UIManager MissionUI()
        {
            if (_missionUI == null) _missionUI = FindAnyObjectByType<Mission1_UIManager>();
            return _missionUI;
        }

        private void HideFollowPill()
        {
            var ui = MissionUI();
            if (ui != null) ui.ShowFollowPopup(false);
        }

        // Trial-route escort: Ido walks the ONE standard trial funnel (plaza spawn to
        // bridge, precomputed once) instead of beelining beat to beat. Beats fire by
        // proximity as he passes; no separate per-leg funnel to dive underground.
        private readonly List<Vector3> _trialRoute = new List<Vector3>();
        private int _trialCorner;
        private bool _trialRouteActive;
        private float _trialMinY;
        private Vector3 _idoStuckPos = Vector3.zero;
        private float _idoStuckTime;
        private float _heartbeatTimer;


        // Subtitle & Dialogue State (Exact Cutscene OnGUI System)
        private DialogueLine _activeDialogueLine = null;
        private float _lineTimer = 0f;
        private float _lineDuration = 6.0f;
        private int _currentBeatLineIndex = 0;
        private PrologueDialogueBeat _activeBeat = null;
        private Action _onBeatCompletedCallback = null;

        // Visual Assets for Cutscene Subtitles
        private Texture2D _blackPixel;
        private Texture2D _whitePixel;
        private Sprite _defaultDoctorPortrait;
        private Sprite _defaultAlitaPortrait;

        // Screen Fade In (disabled: 0f for immediate clear visibility)
        private float _introFadeAlpha = 0.0f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Remove any legacy runtime clinic box if it exists
            var legacyClinic = GameObject.Find("Ido_Cyber_Clinic_Runtime");
            if (legacyClinic != null) Destroy(legacyClinic);

            InitTextures();
            LoadPortraits();
            InitializeNarrativeBeats();
        }

        private void Start()
        {
            if (autoStartPrologueOnAwake)
            {
                StartCoroutine(BootPrologueRoutine());
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            DestroyIdoMarker();
            Debug.Log("[Prologue] OnDestroy: Instance cleared.");
        }

        private void InitTextures()
        {
            _blackPixel = new Texture2D(1, 1);
            _blackPixel.SetPixel(0, 0, new Color(0.02f, 0.025f, 0.04f, 0.92f));
            _blackPixel.Apply();

            _whitePixel = new Texture2D(1, 1);
            _whitePixel.SetPixel(0, 0, Color.white);
            _whitePixel.Apply();
        }

        private void LoadPortraits()
        {
#if UNITY_EDITOR
            _defaultDoctorPortrait = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Characters/Doctor/Textures/T_Doctor_Ido_HoloPortrait.png");
            _defaultAlitaPortrait = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Characters/Alita/Textures/T_Alita_HoloPortrait.png");
#endif
        }

        private IEnumerator BootPrologueRoutine()
        {
            // Wait 2 frames for scene initialization
            yield return null;
            yield return null;

            // 1. Acquire Player and position on open plaza outside Dr. Ido's clinic
            AcquirePlayerAndPosition();
            yield return null;

            // 2. Spawn Dr. Dyson Ido on the plaza facing Alita
            SpawnDoctorOnPlaza();
            yield return null;

            // 3. Ensure camera is following player with full unobstructed view
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera != null && smoothOrbitCamera == null)
            {
                smoothOrbitCamera = mainCamera.GetComponent<SmoothOrbitCamera>() ?? mainCamera.gameObject.AddComponent<SmoothOrbitCamera>();
            }
            if (smoothOrbitCamera != null)
            {
                smoothOrbitCamera.enabled = true;
                smoothOrbitCamera.target = playerTransform;
                smoothOrbitCamera.tightPositionFollow = true;
                smoothOrbitCamera.SnapToTarget();
            }

            // Unlock player movement immediately
            LockPlayerMovement(false);

            // 4. Start Beat (Dialogue in Lab or Plaza)
            bool isInterior = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.IndexOf("Interior", System.StringComparison.OrdinalIgnoreCase) >= 0;
            bool enteredFromCityDoor = Cyberpunk.World.ClinicDoorTrigger.PendingSpawnPos.HasValue;
            bool beat0Done = PlayerPrefs.GetInt("Prologue_Beat0_Completed", 0) == 1;

            if (isInterior)
            {
                if (enteredFromCityDoor && beat0Done)
                {
                    // Re-entering lab after awakening sequence: free exploration mode
                    currentPhase = ProloguePhase.Completed;
                    _activeDialogueLine = null;
                    _activeBeat = null;
                    HideFollowPill();

                    if (doctorTransform != null)
                    {
                        doctorTransform.position = new Vector3(-24.50f, 0.08f, 2.30f);
                        doctorTransform.rotation = Quaternion.Euler(0f, 315f, 0f);
                        SetDoctorAnimatorState(false, 0f);
                    }

                    Debug.Log("[Prologue] Awakening sequence already completed. Re-entered Dr. Ido's lab in free exploration mode.");
                    yield break;
                }

                // Fresh awakening inside Dr. Ido's cyber clinic lab
                currentPhase = ProloguePhase.InsideClinicLab;
                currentWaypointIndex = 0;
                Debug.Log("[Prologue] Starting Dr. Ido's Clinic Lab Awakening Dialogue (Beat 0).");
                if (narrativeBeats.Count > 0)
                {
                    TriggerDialogueBeat(narrativeBeats[0], () =>
                    {
                        PlayerPrefs.SetInt("Prologue_Beat0_Completed", 1);
                        // Lab dialogue already covered the intro; don't replay it on the plaza after exiting
                        PlayerPrefs.SetInt("Prologue_CityIntro_Completed", 1);
                        PlayerPrefs.Save();
                        StartCoroutine(LeadAlitaToClinicExitRoutine());
                    });
                }
                yield break;
            }

            // Outside in Demo_City
            bool cityIntroDone = PlayerPrefs.GetInt("Prologue_CityIntro_Completed", 0) == 1;
            Debug.Log($"[Prologue] Demo_City active. cityIntroDone={cityIntroDone}");

            if (!cityIntroDone)
            {
                currentPhase = ProloguePhase.DialogueAtWaypoint;
                currentWaypointIndex = 0;
                Debug.Log("[Prologue] Starting Sector 4 Plaza Storefront Dialogue outside clinic (Beat 0).");
                if (narrativeBeats.Count > 0)
                {
                    TriggerDialogueBeat(narrativeBeats[0], () =>
                    {
                        PlayerPrefs.SetInt("Prologue_CityIntro_Completed", 1);
                        PlayerPrefs.Save();
                        StartCoroutine(TransitionToCityWalkRoutine());
                    });
                }
            }
            else
            {
                currentPhase = ProloguePhase.WalkingToWaypoint;
                currentWaypointIndex = 1;
                StartCoroutine(TransitionToCityWalkRoutine());
            }
        }

        private IEnumerator LeadAlitaToClinicExitRoutine()
        {
            currentPhase = ProloguePhase.WalkingToWaypoint;
            Vector3 exitDoorPos = new Vector3(-19.25f, -0.13f, 4.37f);
            _currentEscortDest = exitDoorPos;
            _escortActive = true;
            _waitingForPlayer = false;
            SetDoctorAnimatorState(true, 1.0f);

            if (SyntyCombined.CyberpunkHUDController.Instance != null)
            {
                SyntyCombined.CyberpunkHUDController.Instance.ShowToast("[!] NEURAL CALIBRATION COMPLETE // FOLLOW DR. IDO TO PLAZA", 5.0f);
                SyntyCombined.CyberpunkHUDController.Instance.SetQuestObjective(
                    "// OBJECTIVE: EXIT TO SECTOR 4",
                    "> Follow Dr. Ido to the clinic exit\n> Step outside through the door [E] to Sector 4 Plaza");
            }

            while (doctorTransform != null && Vector3.Distance(doctorTransform.position, exitDoorPos) > 1.2f)
            {
                yield return null;
            }

            if (doctorTransform != null)
            {
                doctorTransform.rotation = Quaternion.Euler(0f, 180f, 0f);
                SetDoctorAnimatorState(false, 0f);
            }
        }

        private void AcquirePlayerAndPosition()
        {
            if (playerObject == null) playerObject = GameObject.FindWithTag("Player");
            if (playerObject == null)
            {
                var locomotion = FindAnyObjectByType<SyntySwordLocomotionController>();
                if (locomotion != null) playerObject = locomotion.gameObject;
            }

            if (playerObject != null)
            {
                playerTransform = playerObject.transform;

                // If warping from another scene (e.g. Server Room door or Clinic Door), do not override position
                if (Cyberpunk.World.ServerRoomDoorTrigger.PendingSpawnPos.HasValue ||
                    Cyberpunk.World.ClinicDoorTrigger.PendingSpawnPos.HasValue)
                {
                    return;
                }

                CharacterController cc = playerObject.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;

                bool isInterior = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.IndexOf("Interior", System.StringComparison.OrdinalIgnoreCase) >= 0;

                if (spawnAtServerRoomDoor)
                {
                    // Spawn directly in front of the Server Room vault door on upper walkway
                    playerTransform.position = new Vector3(16.50f, 27.05f, -1.25f);
                    playerTransform.rotation = Quaternion.Euler(0f, 90f, 0f); // Facing door (+X)
                }
                else if (isInterior)
                {
                    if (ClinicDoorTrigger.PendingSpawnPos.HasValue)
                    {
                        playerTransform.position = ClinicDoorTrigger.PendingSpawnPos.Value;
                        playerTransform.rotation = Quaternion.Euler(0f, ClinicDoorTrigger.PendingSpawnYaw, 0f);
                    }
                    else if (PlayerPrefs.GetInt("Prologue_Beat0_Completed", 0) == 1)
                    {
                        // Entering lab after awakening completed: spawn near the clinic entrance door
                        playerTransform.position = new Vector3(-19.50f, 0.08f, 3.50f);
                        playerTransform.rotation = Quaternion.Euler(0f, 270f, 0f);
                    }
                    else
                    {
                        // First boot awakening: Position player beside surgical bed facing glowing Tron Red Room
                        Vector3 labBedSpawnPos = new Vector3(-24.25f, 0.08f, 2.70f);
                        playerTransform.position = labBedSpawnPos;
                        playerTransform.rotation = Quaternion.Euler(0f, 0f, 0f); // Facing glowing Tron Red Room portal
                    }
                }
                else
                {
                    // Position player right outside Dr. Ido's Clinic storefront in Demo_City
                    Vector3 clinicSpawnPos = new Vector3(19.00f, -17.98f, 49.50f);
                    playerTransform.position = clinicSpawnPos;
                    playerTransform.rotation = Quaternion.Euler(0f, 0f, 0f); // Facing Dr. Ido & Sector 4 Plaza
                }

                if (cc != null) cc.enabled = true;
            }
        }

        private void SpawnDoctorOnPlaza()
        {
            if (doctorActor != null) return;

            // Disable any static placeholder Doctor Ido to prevent duplicate twin models
            var staticIdo = GameObject.Find("Sidekick_Doctor_Ido");
            if (staticIdo != null)
            {
                staticIdo.SetActive(false);
            }

            // Position Doctor at Clinic storefront or inside Lab facing Alita
            bool isInterior = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.IndexOf("Interior", System.StringComparison.OrdinalIgnoreCase) >= 0;
            Vector3 idoPos = isInterior ? new Vector3(-24.50f, 0.08f, 2.30f) : new Vector3(18.50f, -17.98f, 51.00f);
            Quaternion idoRot = isInterior ? Quaternion.Euler(0f, 315f, 0f) : Quaternion.Euler(0f, 160f, 0f);

            doctorActor = new GameObject("Doctor_Actor_Runtime");
            doctorActor.transform.position = idoPos;
            doctorActor.transform.rotation = idoRot;

            // Load Doctor Model
            GameObject prefab = doctorPrefab;
#if UNITY_EDITOR
            if (prefab == null)
            {
                prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/Doctor/Doctor.prefab")
                      ?? UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonCyberCity/Prefabs/Characters/SM_Chr_Rich_Male_01.prefab");
            }
#endif
            GameObject modelInstance;
            if (prefab != null)
            {
                modelInstance = Instantiate(prefab, doctorActor.transform);
            }
            else
            {
                modelInstance = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                modelInstance.transform.SetParent(doctorActor.transform, false);
            }

            modelInstance.name = "Doctor_Model";
            modelInstance.transform.localPosition = Vector3.zero;
            modelInstance.transform.localRotation = Quaternion.identity;

            // Ensure runtime Doctor colliders do not push the player or intercept ground raycasts
            var allCols = doctorActor.GetComponentsInChildren<Collider>(true);
            foreach (var col in allCols)
            {
                col.isTrigger = true;
            }
            // Set layer to Ignore Raycast (Layer 2)
            doctorActor.layer = 2;
            foreach (Transform t in doctorActor.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = 2;
            }

            // Remove any NavMeshAgent on the prefab so it does not interfere with transform locomotion
            var agent = doctorActor.GetComponentInChildren<NavMeshAgent>();
            if (agent != null) Destroy(agent);

            // Simple transform escort (no NavMesh required)
            _currentEscortDest = idoPos;
            _escortActive = false;

            // Animator
            doctorAnimator = modelInstance.GetComponent<Animator>();
#if UNITY_EDITOR
            if (doctorAnimator != null && doctorAnimator.runtimeAnimatorController == null)
            {
                var ac = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Alita/AC_CyberNPC.controller")
                      ?? UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Alita/AC_Ido_Discovery.controller");
                if (ac != null) doctorAnimator.runtimeAnimatorController = ac;
            }
#endif
            doctorTransform = doctorActor.transform;
            BuildIdoMarker();
        }

        // DR IDO tag: plain OnGUI nameplate over his head (same renderer as the
        // subtitles, so it can never mirror, mis-scale, or detach). Visible while
        // he guides (clinic lab through waypoint dialogue), hidden at handoff.
        private void BuildIdoMarker()
        {
            Debug.Log("[Prologue] Ido nameplate on (OnGUI).");
        }

        private void DestroyIdoMarker()
        {
        }

        private IEnumerator TransitionToCityWalkRoutine()
        {
            currentPhase = ProloguePhase.WalkingToWaypoint;
            Debug.Log("[Prologue] Phase=WalkingToWaypoint; escorting to waypoint 1.");

            // Advance to Waypoint 1 (Street Plaza outside)
            currentWaypointIndex = 1;
            BuildTrialRoute();
            StartWalkToNextWaypoint();
            yield break;
        }

        // The standard trial route: ONE NavMesh funnel from the plaza spawn to the
        // bridge arena, walked corner by corner. Beats trigger by proximity as he
        // passes them — no beelines, no per-leg funnels, no underground dives.
        // Verified multi-leg trial route: connects Clinic -> Catwalk -> Beats 1-4 -> Bridge Arena.
        // Ido walks verified NavMesh corners and hugs the ground without ever floating in air.
        private void BuildTrialRoute()
        {
            _trialRoute.Clear();
            _trialCorner = 0;
            _trialRouteActive = false;
            if (doctorTransform == null) return;

            Vector3 startPos = doctorTransform.position;
            var keyTargets = new List<Vector3>();

            // If starting near the clinic storefront (Z < 54), connect cleanly along the sidewalk to the bridge entrance
            if (startPos.z < 54.0f)
            {
                keyTargets.Add(new Vector3(18.80f, -17.98f, 55.50f));
            }

            // Target beats 1 to 4
            for (int b = 1; b < narrativeBeats.Count; b++)
            {
                keyTargets.Add(narrativeBeats[b].waypointPosition);
            }

            var mission = FindAnyObjectByType<Mission1_StreetTrialManager>();
            if (mission != null && keyTargets.Count > 0)
            {
                keyTargets[keyTargets.Count - 1] = mission.zoneCenterPosition;
            }

            _trialRoute.Add(startPos);
            Vector3 cur = startPos;

            for (int i = 0; i < keyTargets.Count; i++)
            {
                Vector3 target = keyTargets[i];

                // Direct sidewalk leg between clinic door and bridge entrance
                if (cur.z < 54.0f && Mathf.Abs(target.z - 55.50f) < 0.8f)
                {
                    _trialRoute.Add(target);
                    cur = target;
                    continue;
                }

                Vector3 sNav = cur;
                if (NavMesh.SamplePosition(cur, out NavMeshHit sh, 2.5f, NavMesh.AllAreas)) sNav = sh.position;
                Vector3 eNav = target;
                if (NavMesh.SamplePosition(target, out NavMeshHit eh, 2.5f, NavMesh.AllAreas)) eNav = eh.position;

                NavMeshPath legPath = new NavMeshPath();
                if (NavMesh.CalculatePath(sNav, eNav, NavMesh.AllAreas, legPath)
                    && legPath.status == NavMeshPathStatus.PathComplete && legPath.corners.Length >= 2)
                {
                    for (int c = 1; c < legPath.corners.Length; c++)
                    {
                        _trialRoute.Add(legPath.corners[c]);
                    }
                    cur = target;
                }
                else
                {
                    _trialRoute.Add(target);
                    cur = target;
                }
            }

            if (_trialRoute.Count >= 2)
            {
                _trialRouteActive = true;
                _trialMinY = float.MaxValue;
                foreach (var c in _trialRoute)
                {
                    if (c.y < _trialMinY) _trialMinY = c.y;
                }
                _idoStuckPos = doctorTransform.position;
                _idoStuckTime = Time.time;
                _heartbeatTimer = 0f;
                Debug.Log($"[Prologue] Trial route built: {_trialRoute.Count} corners, clinic -> beats 1-4 -> bridge. Ido walks the verified route.");
            }
            else
            {
                Debug.LogWarning("[Prologue] Trial route failed; escort falls back to beat-direct walking.");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // 2. ESCORT PACING & CITY NAVIGATION
        // ─────────────────────────────────────────────────────────────────────
        private void Update()
        {
            // Intro fade from black
            if (_introFadeAlpha > 0f)
            {
                _introFadeAlpha = Mathf.MoveTowards(_introFadeAlpha, 0f, Time.deltaTime * 1.5f);
            }

            // Dialogue keyboard advance and typewriter are managed by CyberpunkDialogueHUD
            if (_activeDialogueLine != null)
            {
                _lineTimer += Time.deltaTime;
            }

            // Escort walk navigation
            if (currentPhase == ProloguePhase.WalkingToWaypoint)
            {
                UpdateEscortWalk();
            }

            // Escort heartbeat (15s): phase/beat/Ido position/follow gap for the log.
            if (doctorTransform != null &&
                (currentPhase == ProloguePhase.WalkingToWaypoint || currentPhase == ProloguePhase.DialogueAtWaypoint))
            {
                _heartbeatTimer -= Time.deltaTime;
                if (_heartbeatTimer <= 0f)
                {
                    _heartbeatTimer = 15.0f;
                    float gap = (playerTransform != null) ? Vector3.Distance(doctorTransform.position, playerTransform.position) : -1f;
                    Debug.Log($"[Prologue] Escort heartbeat: phase={currentPhase} beat={currentWaypointIndex} ido={doctorTransform.position} gap={gap:F1}m waiting={_waitingForPlayer}");
                }
            }
        }

        private void UpdateEscortWalk()
        {
            if (doctorTransform == null || currentWaypointIndex >= narrativeBeats.Count) return;

            var currentBeat = narrativeBeats[currentWaypointIndex];
            bool isInterior = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.IndexOf("Interior", System.StringComparison.OrdinalIgnoreCase) >= 0;

            if (isInterior)
            {
                Vector3 exitDest = _currentEscortDest != Vector3.zero ? _currentEscortDest : new Vector3(-19.25f, -0.13f, 4.37f);
                Vector3 toExitFlat = exitDest - doctorTransform.position;
                toExitFlat.y = 0f;
                float distToExit = toExitFlat.magnitude;

                if (distToExit <= 1.2f)
                {
                    _escortActive = false;
                    SetDoctorAnimatorState(false, 0f);
                    doctorTransform.rotation = Quaternion.Euler(0f, 180f, 0f);
                    return;
                }

                if (_escortActive)
                {
                    Vector3 moveDir = toExitFlat.normalized;
                    Vector3 nextPos = Vector3.MoveTowards(doctorTransform.position, exitDest, walkSpeed * Time.deltaTime);
                    doctorTransform.position = SnapToGround(nextPos, referenceY: exitDest.y);
                    if (toExitFlat.sqrMagnitude > 0.001f)
                    {
                        doctorTransform.rotation = Quaternion.Slerp(doctorTransform.rotation, Quaternion.LookRotation(moveDir), Time.deltaTime * 6f);
                    }
                    SetDoctorAnimatorState(true, 1.0f);
                }
                return;
            }

            // Safety watchdog: only recover if Ido falls off the world into the abyss below map geometry
            if (doctorTransform.position.y < _trialMinY - 6.0f)
            {
                int rescueIdx = (_trialRouteActive && _trialRoute.Count > 0) ? Mathf.Clamp(_trialCorner, 0, _trialRoute.Count - 1) : 0;
                Vector3 rescuePos = (_trialRouteActive && _trialRoute.Count > 0) ? _trialRoute[rescueIdx] : currentBeat.waypointPosition;
                doctorTransform.position = SnapToGround(rescuePos, referenceY: rescuePos.y, clampToStep: false);
                _waitingForPlayer = false;
                _escortActive = true;
                SetDoctorAnimatorState(true, 1.0f);
                HideFollowPill();
                Debug.LogWarning($"[Prologue] Ido fell below world (y={doctorTransform.position.y:F2}); recovered to corner {rescueIdx} at {doctorTransform.position}.");
            }

            // 0. Pacing: Check Alita's distance horizontally (ignoring Y differences on stairs/levels)
            float distToAlita = (playerTransform != null)
                ? Vector2.Distance(new Vector2(doctorTransform.position.x, doctorTransform.position.z),
                                   new Vector2(playerTransform.position.x, playerTransform.position.z))
                : 0f;

            // 1. Proximity check for narrative beat dialogue:
            // When Ido reaches within 2.5m of current beat's waypoint, trigger beat dialogue!
            if (currentPhase == ProloguePhase.WalkingToWaypoint)
            {
                Vector3 toBeat = currentBeat.waypointPosition - doctorTransform.position;
                toBeat.y = 0f;
                float distToWaypoint = toBeat.magnitude;
                float verticalToWaypoint = Mathf.Abs(currentBeat.waypointPosition.y - doctorTransform.position.y);

                if (distToWaypoint <= 2.5f && verticalToWaypoint <= 2.2f)
                {
                    if (distToAlita > 8.0f)
                    {
                        // Ido arrived at the waypoint, but Alita is still catching up. Hold at waypoint!
                        _waitingForPlayer = true;
                        _escortActive = false;
                        SetDoctorAnimatorState(false, 0f);
                        Vector3 toAlita = playerTransform != null ? (playerTransform.position - doctorTransform.position) : Vector3.zero;
                        toAlita.y = 0f;
                        if (toAlita.sqrMagnitude > 0.01f) doctorTransform.rotation = Quaternion.LookRotation(toAlita.normalized);
                        return;
                    }

                    OnArrivedAtWaypoint(currentBeat);
                    return;
                }
            }

            // 2. Next destination corner along the trial route
            Vector3 dest;
            if (_trialRouteActive && _trialCorner < _trialRoute.Count) dest = _trialRoute[_trialCorner];
            else dest = currentBeat.waypointPosition;

            Vector3 toDestFlat = dest - doctorTransform.position;
            toDestFlat.y = 0f;
            float distToTargetCorner = toDestFlat.magnitude;
            float verticalGap = Mathf.Abs(dest.y - doctorTransform.position.y);

            // Advance corner index when close enough horizontally (stairs have vertical rise)
            bool reachedCorner = false;
            if (_trialRouteActive && _trialCorner < _trialRoute.Count)
            {
                if (distToTargetCorner <= 0.65f) reachedCorner = true;
                else if (distToTargetCorner <= 1.35f && verticalGap <= 3.5f) reachedCorner = true;
            }

            if (reachedCorner)
            {
                if (_trialCorner < _trialRoute.Count - 1)
                {
                    _trialCorner++;
                    dest = _trialRoute[_trialCorner];
                    toDestFlat = dest - doctorTransform.position;
                    toDestFlat.y = 0f;
                    distToTargetCorner = toDestFlat.magnitude;
                }
            }

            // Check if player pushed ahead towards the current beat
            bool playerAhead = false;
            if (playerTransform != null)
            {
                Vector3 wpFromPlayer = currentBeat.waypointPosition - playerTransform.position; wpFromPlayer.y = 0f;
                Vector3 wpFromIdo = currentBeat.waypointPosition - doctorTransform.position; wpFromIdo.y = 0f;
                playerAhead = wpFromPlayer.magnitude + 2.0f < wpFromIdo.magnitude;
            }

            // Pacing: Check if Alita falls behind (>waitDistance) - doctor pauses and turns toward her
            if (distToAlita > waitDistance && !_waitingForPlayer && !playerAhead)
            {
                _waitingForPlayer = true;
                _escortActive = false;
                Debug.Log($"[Prologue] Ido waiting for Alita (dist={distToAlita:F1}m > {waitDistance:F1}m); follow him to advance.");
                SetDoctorAnimatorState(false, 0f);

                Vector3 toAlita = (playerTransform.position - doctorTransform.position);
                toAlita.y = 0f;
                if (toAlita.sqrMagnitude > 0.01f) doctorTransform.rotation = Quaternion.LookRotation(toAlita.normalized);
                TriggerDoctorGesture("Idle_Wave");
            }
            else if (_waitingForPlayer && (distToAlita <= followCatchupDist || playerAhead))
            {
                _waitingForPlayer = false;
                _escortActive = true;
                if (playerAhead && distToAlita > followCatchupDist)
                    Debug.Log($"[Prologue] Player pushed ahead ({distToAlita:F1}m); Ido moving up route.");
                else
                    Debug.Log($"[Prologue] Alita caught up (dist={distToAlita:F1}m <= {followCatchupDist:F1}m); Ido resuming escort.");
                HideFollowPill();
                SetDoctorAnimatorState(true, 1.0f);
            }

            // Live pill distance refresh while Ido holds position
            if (_waitingForPlayer)
            {
                var followUI = MissionUI();
                if (followUI != null && _activeDialogueLine == null)
                {
                    followUI.UpdateFollowDistance(distToAlita, waitDistance);
                }
            }

            // 4. Movement along the verified route
            if (_escortActive)
            {
                float stepSpeed = (playerAhead && distToAlita > followCatchupDist) ? walkSpeed * 1.35f : walkSpeed;

                if (distToTargetCorner > 0.15f)
                {
                    Vector3 moveDir = toDestFlat.normalized;

                    // Interpolate expected slope height along the current corner segment for natural stair climbing
                    Vector3 startCorner = (_trialRouteActive && _trialCorner > 0 && _trialCorner <= _trialRoute.Count) ? _trialRoute[_trialCorner - 1] : doctorTransform.position;
                    float totalSegDist = Vector2.Distance(new Vector2(startCorner.x, startCorner.z), new Vector2(dest.x, dest.z));
                    float curDistToDest = toDestFlat.magnitude;
                    float segT = totalSegDist > 0.1f ? Mathf.Clamp01(1f - (curDistToDest / totalSegDist)) : 1f;
                    float expectedWalkY = Mathf.Lerp(startCorner.y, dest.y, segT);

                    Vector3 nextHoriz = Vector3.MoveTowards(
                        doctorTransform.position,
                        new Vector3(dest.x, expectedWalkY, dest.z),
                        stepSpeed * Time.deltaTime);

                    Vector3 groundedPos = SnapToGround(nextHoriz, referenceY: expectedWalkY);

                    // Smooth vertical ascent/descent on stairs so Dr. Ido steps naturally without popping
                    float smoothY = Mathf.MoveTowards(doctorTransform.position.y, groundedPos.y, 6.0f * Time.deltaTime);
                    doctorTransform.position = new Vector3(groundedPos.x, smoothY, groundedPos.z);

                    if (toDestFlat.sqrMagnitude > 0.001f)
                    {
                        doctorTransform.rotation = Quaternion.Slerp(
                            doctorTransform.rotation,
                            Quaternion.LookRotation(moveDir),
                            Time.deltaTime * 6f);
                    }
                    SetDoctorAnimatorState(true, 1.0f);
                }
                else
                {
                    SetDoctorAnimatorState(false, 0f);
                }

                // Wedge watchdog (only fires if actively moving and stuck)
                if (Time.time - _idoStuckTime > 6.0f)
                {
                    Vector3 net = doctorTransform.position - _idoStuckPos;
                    net.y = 0f;
                    if (net.magnitude < 0.3f && distToTargetCorner > 0.5f)
                    {
                        Vector3 pushDir = toDestFlat.sqrMagnitude > 0.01f ? toDestFlat.normalized : doctorTransform.forward;
                        Vector3 nudge = doctorTransform.position + pushDir * 0.8f;
                        doctorTransform.position = SnapToGround(nudge, referenceY: dest.y, clampToStep: false);
                        Debug.LogWarning("[Prologue] Ido wedged on the trial line; nudge forward.");
                    }
                    _idoStuckPos = doctorTransform.position;
                    _idoStuckTime = Time.time;
                }
            }
            else if (!_escortActive)
            {
                // Holding position (waiting / arriving): stay firmly glued to current ground
                doctorTransform.position = SnapToGround(doctorTransform.position, referenceY: doctorTransform.position.y);
                _idoStuckTime = Time.time;
            }
        }

        // Ground snap: Samples NavMesh along current route vertical level,
        // falling back to a downward Physics raycast that strictly ignores triggers,
        // characters, and Doctor Ido's own colliders so he NEVER lifts into the air or falls through stairs.
        private Vector3 SnapToGround(Vector3 pos, float referenceY = float.NaN, bool clampToStep = true)
        {
            float targetRefY = float.IsNaN(referenceY) ? pos.y : referenceY;

            // 1. Precise NavMesh check at reference height + 0.35m
            Vector3 queryPos = new Vector3(pos.x, targetRefY + 0.35f, pos.z);
            if (NavMesh.SamplePosition(queryPos, out NavMeshHit hit, 2.5f, NavMesh.AllAreas))
            {
                if (!clampToStep || Mathf.Abs(hit.position.y - targetRefY) <= 3.5f)
                {
                    pos.y = hit.position.y;
                    return pos;
                }
            }

            // 2. Physics Raycast straight down from 2.5m above targetRefY
            Vector3 origin = new Vector3(pos.x, targetRefY + 2.5f, pos.z);
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 7.0f, ~0, QueryTriggerInteraction.Ignore);
            float bestY = float.MinValue;
            float bestDist = float.MaxValue;
            foreach (var h in hits)
            {
                if (h.collider.isTrigger) continue;
                if (doctorTransform != null && h.collider.transform.IsChildOf(doctorTransform)) continue;
                if (playerTransform != null && (h.collider.transform == playerTransform || h.collider.transform.IsChildOf(playerTransform))) continue;

                string colName = h.collider.name;
                if (colName.Contains("Doctor") || colName.Contains("Alita") || colName.Contains("Hero") || colName.Contains("Sidekick")) continue;
                if (colName.Contains("Train") || colName.Contains("Car")) continue;

                float dist = Mathf.Abs(h.point.y - targetRefY);
                if (dist <= 3.5f && dist < bestDist)
                {
                    bestDist = dist;
                    bestY = h.point.y;
                }
            }

            if (bestY > float.MinValue)
            {
                pos.y = bestY;
                return pos;
            }

            return pos;
        }

        /// <summary>
        /// Drops Ido at a bridge-side position (ground-snapped, facing you). Used when
        /// you rush to the battle area early — he joins you instead of holding you up.
        /// </summary>
        public void TeleportDoctorTo(Vector3 worldPos)
        {
            if (doctorTransform == null)
            {
                Debug.LogWarning("[Prologue] TeleportDoctorTo: no doctor actor; ignored.");
                return;
            }
            doctorTransform.position = SnapToGround(worldPos, clampToStep: false);
            if (playerTransform != null)
            {
                Vector3 face = playerTransform.position - doctorTransform.position;
                face.y = 0f;
                if (face.sqrMagnitude > 0.01f) doctorTransform.rotation = Quaternion.LookRotation(face.normalized);
            }
            _escortActive = false;
            _waitingForPlayer = false;
            SetDoctorAnimatorState(false, 0f);
            HideFollowPill();
            Debug.Log($"[Prologue] Ido rushed to bridge {doctorTransform.position} (player arrived early).");
        }

        private int NearestRouteCorner(Vector3 p)
        {
            if (_trialRoute == null || _trialRoute.Count == 0) return -1;
            int best = Mathf.Clamp(_trialCorner, 0, _trialRoute.Count - 1);
            float bestD = float.MaxValue;
            int startIdx = Mathf.Max(0, _trialCorner - 1);
            int endIdx = Mathf.Min(_trialRoute.Count, _trialCorner + 6);
            for (int i = startIdx; i < endIdx; i++)
            {
                float dd = Vector3.Distance(p, _trialRoute[i]);
                if (dd < bestD) { bestD = dd; best = i; }
            }
            return best;
        }

        private void StartWalkToNextWaypoint()
        {
            if (handoffFired) return; // handoff already went out (e.g. early-arrival teleport); never walk back out
            if (currentWaypointIndex >= narrativeBeats.Count)
            {
                TriggerCombatHandoff();
                return;
            }

            var nextBeat = narrativeBeats[currentWaypointIndex];
            currentPhase = ProloguePhase.WalkingToWaypoint;
            _currentEscortDest = nextBeat.waypointPosition;
            _escortActive = true;
            _waitingForPlayer = false;
            SetDoctorAnimatorState(true, 1.0f);

            // Re-anchor corner index to nearest point ahead
            if (doctorTransform != null)
            {
                int near = NearestRouteCorner(doctorTransform.position);
                if (near >= 0) _trialCorner = Mathf.Max(_trialCorner, near);
            }

            // Register Ido as the mission AR-trail target (idempotent; never locks input)
            var mission = FindAnyObjectByType<Mission1_StreetTrialManager>();
            if (mission != null) mission.SetEscortTarget(doctorTransform);
            else Debug.LogWarning("[Prologue] StartWalk: no Mission1 manager for escort trail; Ido walks without AR trail.");
        }

        private void OnArrivedAtWaypoint(PrologueDialogueBeat beat)
        {
            currentPhase = ProloguePhase.DialogueAtWaypoint;
            Debug.Log($"[Prologue] Phase=DialogueAtWaypoint; arrived at '{beat?.beatTitle}' (index {currentWaypointIndex}).");
            _escortActive = false;
            _waitingForPlayer = false;
            SetDoctorAnimatorState(false, 0f);
            HideFollowPill();

            // Turn Doctor towards Alita
            if (playerTransform != null && doctorTransform != null)
            {
                Vector3 dir = (playerTransform.position - doctorTransform.position).normalized;
                dir.y = 0f;
                if (dir != Vector3.zero) doctorTransform.rotation = Quaternion.LookRotation(dir);
            }

            if (!string.IsNullOrEmpty(beat.idoAnimTrigger))
            {
                TriggerDoctorGesture(beat.idoAnimTrigger);
            }

            // Trigger narrative dialogue
            TriggerDialogueBeat(beat, () =>
            {
                if (handoffFired) return; // early-arrival teleport already handed off; stay put
                currentWaypointIndex++;
                if (currentWaypointIndex >= narrativeBeats.Count)
                {
                    TriggerCombatHandoff();
                }
                else
                {
                    StartWalkToNextWaypoint();
                }
            });
        }

        // ─────────────────────────────────────────────────────────────────────
        // 3. DIALOGUE SYSTEM & CUTSCENE-STYLE OnGUI SUBTITLES
        private void TriggerDialogueBeat(PrologueDialogueBeat beat, Action onComplete)
        {
            if (beat == null || beat.dialogueLines == null || beat.dialogueLines.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            _activeBeat = beat;
            _onBeatCompletedCallback = onComplete;

            if (!string.IsNullOrEmpty(beat.idoAnimTrigger))
            {
                TriggerDoctorGesture(beat.idoAnimTrigger);
            }

            var hudLines = new List<Alita.UI.CyberpunkDialogueHUD.DialogueLineData>();
            foreach (var l in beat.dialogueLines)
            {
                var lineData = new Alita.UI.CyberpunkDialogueHUD.DialogueLineData(
                    l.speakerName,
                    l.text,
                    l.customPortrait,
                    l.speakerTitle,
                    autoAdvance: 0f,
                    onFinish: () =>
                    {
                        if (l.givesCityWarning)
                        {
                            PolygonCyberCity.NPC.Spawner.AlitaReputation.CityWarningGiven = true;
                            // No scripted scuffle: random NPCs never start a fight with Alita
                        }
                        if (!string.IsNullOrEmpty(l.animTrigger))
                        {
                            TriggerDoctorGesture(l.animTrigger);
                        }
                    }
                );
                hudLines.Add(lineData);
            }

            Debug.Log($"[Prologue] TriggerDialogueBeat: starting '{beat.beatTitle}' with {hudLines.Count} dialogue lines via CyberpunkDialogueHUD.");
            Alita.UI.CyberpunkDialogueHUD.GetOrCreate().StartConversation(hudLines, () =>
            {
                Debug.Log($"[Prologue] Completed dialogue beat '{beat.beatTitle}'.");
                _activeDialogueLine = null;
                _activeBeat = null;
                var cb = _onBeatCompletedCallback;
                _onBeatCompletedCallback = null;
                cb?.Invoke();
            });
        }

        private void OnGUI()
        {
            // 1. Screen Fade from Black on Scene Start
            if (_introFadeAlpha > 0.005f)
            {
                GUI.color = new Color(0f, 0f, 0f, _introFadeAlpha);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            // 1b. DR IDO nameplate (plain text over his head; hidden at handoff).
            if (doctorTransform != null && Camera.main != null &&
                (currentPhase == ProloguePhase.InsideClinicLab ||
                 currentPhase == ProloguePhase.WalkingToWaypoint ||
                 currentPhase == ProloguePhase.DialogueAtWaypoint))
            {
                Vector3 sp = Camera.main.WorldToScreenPoint(doctorTransform.position + Vector3.up * 2.35f);
                if (sp.z > 0f)
                {
                    GUIStyle tagStyle = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 15,
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter
                    };
                    Rect tagRect = new Rect(sp.x - 110f, Screen.height - sp.y - 14f, 220f, 28f);
                    tagStyle.normal.textColor = new Color(0f, 0f, 0f, 0.9f);
                    GUI.Label(new Rect(tagRect.x + 1, tagRect.y + 1, tagRect.width, tagRect.height), "DR IDO", tagStyle);
                    tagStyle.normal.textColor = new Color(0.1f, 0.95f, 1f);
                    GUI.Label(tagRect, "DR IDO", tagStyle);
                }
            }

            // 2. Dialogue is rendered via AAA CyberpunkDialogueHUD (Canvas uGUI)

        // 3. On-screen escort hint: Ido pauses when Alita lags behind (>waitDistance)
        // and only resumes within followCatchupDist. Surface that on screen (not just
        // the console) so the walkthrough can never appear silently stalled.
        if (_waitingForPlayer && _activeDialogueLine == null && currentPhase == ProloguePhase.WalkingToWaypoint)
        {
            float distToPlayer = (playerTransform != null && doctorTransform != null)
                ? Vector3.Distance(doctorTransform.position, playerTransform.position)
                : 0f;
            string followHint = $"STAY WITH IDO ({distToPlayer:F0}m / {waitDistance:F0}m)";

            GUIStyle followStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            followStyle.normal.textColor = Color.black;
            int fw = Mathf.Min(640, (int)(Screen.width * 0.6f));
            Rect followRect = new Rect((Screen.width - fw) / 2, 84, fw, 30);
            GUI.Label(new Rect(followRect.x + 1, followRect.y + 1, followRect.width, followRect.height), followHint, followStyle);
            followStyle.normal.textColor = new Color(0.1f, 0.95f, 1f);
            GUI.Label(followRect, followHint, followStyle);
        }
        }

        // ─────────────────────────────────────────────────────────────────────
        // 4. COMBAT HANDOFF (public: the mission manager may force it when the
        // player rushes the bridge early and Ido is teleported in)
        // ─────────────────────────────────────────────────────────────────────
        public void TriggerCombatHandoff()
        {
            if (handoffFired)
            {
                Debug.LogWarning("[Prologue] TriggerCombatHandoff: duplicate call ignored (already fired).");
                return;
            }
            handoffFired = true;
            Debug.Log("[Prologue] Entering HandoffToCombat; notifying Mission1_StreetTrialManager.");
            currentPhase = ProloguePhase.HandoffToCombat;
            _activeDialogueLine = null;
            _escortActive = false;
            SetDoctorAnimatorState(false, 0f);
            HideFollowPill();
            DestroyIdoMarker();

            // Handoff requires Ido physically at the bridge (Beat4 is pinned to the
            // mission zone by the manager; this assert-logs the invariant).
            var handoffMission = FindAnyObjectByType<Mission1_StreetTrialManager>();
            if (handoffMission != null && doctorTransform != null)
            {
                float idoToZone = Vector3.Distance(doctorTransform.position, handoffMission.zoneCenterPosition);
                Debug.Log($"[Prologue] Handoff check: Ido {idoToZone:F1}m from bridge zone (radius {waypointRadius:F1}m).");
            }

            // Ido's rogue-bots briefing plays first (6s subtitle); the combat
            // handoff (direct call + static event) fires after it lands.
            StartCoroutine(HandoffMessageRoutine());
        }

        /// <summary>
        /// A few seconds after Ido's warning, two locals jump Alita so the warning
        /// is felt immediately. Far-away NPCs are brought in near the player.
        /// </summary>
        private IEnumerator StreetScuffleRoutine()
        {
            yield return new WaitForSeconds(5.0f);

            if (playerObject == null) yield break;
            Vector3 center = playerObject.transform.position;

            var candidates = new List<PolygonCyberCity.NPC.Spawner.CyberNPCController>();
            foreach (var n in FindObjectsByType<PolygonCyberCity.NPC.Spawner.CyberNPCController>(FindObjectsSortMode.None))
            {
                if (n == null || !n.IsAlive || !n.gameObject.activeInHierarchy) continue;
                if (n.npcType == PolygonCyberCity.NPC.Spawner.NPCType.HelperBot ||
                    n.npcType == PolygonCyberCity.NPC.Spawner.NPCType.Mercenary) continue;
                candidates.Add(n);
            }
            candidates.Sort((a, b) =>
                (a.transform.position - center).sqrMagnitude.CompareTo((b.transform.position - center).sqrMagnitude));

            int sent = 0;
            for (int i = 0; i < candidates.Count && sent < 2; i++)
            {
                var npc = candidates[i];
                if (npc == null) continue;
                float d = (npc.transform.position - center).magnitude;
                if (d > 22f) continue; // Only engage nearby locals; never warp or teleport distant NPCs!
                npc.ForceAttackAlita();
                sent++;
            }
            Debug.Log($"[Prologue] Street scuffle: {sent} locals attack Alita after Ido's warning.");
        }

        private IEnumerator HandoffMessageRoutine()
        {
            // Walk ambush bots smoothly into standoff formation along the bridge (never teleport!)
            try
            {
                var stage = FindAnyObjectByType<Mission1_StreetTrialManager>();
                if (stage != null)
                {
                    yield return stage.WalkAmbushBotsToStandoffRoutine();
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Prologue] Ambush staging exception: {ex}");
            }

            // Standoff: the rogue bots demand Alita's surrender and Ido pleads,
            // then the ambush springs and Alita takes over mid-fight.
            var hud = Alita.UI.CyberpunkDialogueHUD.GetOrCreate();
            LockPlayerMovement(true);
            bool standoffDone = false;
            var standoff = new List<Alita.UI.CyberpunkDialogueHUD.DialogueLineData>
            {
                new Alita.UI.CyberpunkDialogueHUD.DialogueLineData(
                    "ROGUE ENFORCER",
                    "Halt! Target locked: unregistered chassis, no serial, no owner. Surrender the unknown cyborg, Doctor, or you will face the consequences.",
                    title: "Corporate Bounty Unit"),
                new Alita.UI.CyberpunkDialogueHUD.DialogueLineData(
                    "DR. DYSON IDO",
                    "Please... don't. She's just a girl, she's done nothing to you. Take what you want from my clinic, but leave us alone!",
                    title: "Cyber-Surgeon & Hunter-Warrior"),
                new Alita.UI.CyberpunkDialogueHUD.DialogueLineData(
                    "ROGUE ENFORCER",
                    "The core is worth more than your whole district. Seal the bridge. Take the chassis. Terminate anything that interferes.",
                    title: "Corporate Bounty Unit"),
            };
            hud.StartConversation(standoff, () => standoffDone = true);
            while (!standoffDone) yield return null;

            LockPlayerMovement(false);

            // Ambush springs: bots go hostile on Alita.
            try
            {
                var mission1 = FindAnyObjectByType<Mission1_StreetTrialManager>();
                if (mission1 != null) mission1.BeginBridgeFight(fromPrologueEscort: true);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Prologue] BeginBridgeFight exception: {ex}");
            }

            yield return new WaitForSecondsRealtime(0.8f);
            hud.ShowSubtitle("ALITA", "Stay behind me, Ido. I've got this handled.", 4.0f);

            try
            {
                PrologueHandoffEvent?.Invoke();
                Debug.Log("[Prologue] HandoffToCombat: static event fired.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Prologue] Handoff event exception: {ex}");
            }

            currentPhase = ProloguePhase.Completed;
            Debug.Log("[Prologue] Phase=Completed.");
            yield break;
        }

        private void SetDoctorAnimatorState(bool isWalking, float speed)
        {
            if (doctorAnimator == null) return;
            if (doctorAnimator.isActiveAndEnabled)
            {
                doctorAnimator.SetBool("IsWalking", isWalking);
                doctorAnimator.SetFloat("MoveSpeed", speed);
                doctorAnimator.SetFloat("Speed", speed);
            }
        }

        private void TriggerDoctorGesture(string triggerName)
        {
            if (doctorAnimator == null || string.IsNullOrEmpty(triggerName)) return;
            if (doctorAnimator.isActiveAndEnabled)
                doctorAnimator.Play(triggerName);
        }

        private void LockPlayerMovement(bool locked)
        {
            var synty = playerObject != null ? playerObject.GetComponent<SyntySwordLocomotionController>() : null;
            if (synty != null) synty.inputLocked = locked;

            var insane = playerObject != null ? playerObject.GetComponent<InsaneGunSwordController>() : null;
            if (insane != null) insane.inputLocked = locked;
        }

        // ─────────────────────────────────────────────────────────────────────
        // 5. NARRATIVE LORE DATA (CYBERPUNK STORY: CRUEL BIG CORPOS)
        // ─────────────────────────────────────────────────────────────────────
        public void InitializeNarrativeBeats()
        {
            narrativeBeats.Clear();

            bool isInteriorBeat = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.IndexOf("Interior", System.StringComparison.OrdinalIgnoreCase) >= 0;

            // Beat 0: Clinic Lab / Awakening
            var beat0 = new PrologueDialogueBeat()
            {
                beatTitle = isInteriorBeat ? "Dr. Ido's Clinic Lab // Awakening" : "Dr. Ido's Clinic // Awakening",
                waypointPosition = isInteriorBeat ? new Vector3(-19.25f, -0.13f, 4.37f) : new Vector3(18.50f, -17.98f, 51.00f),
                idoAnimTrigger = "Idle_HeadNod"
            };
            beat0.dialogueLines.Add(new DialogueLine("ALITA", "MARTIAN CYBORG", "My hands... they feel different. Heavier. Where am I?"));
            if (isInteriorBeat)
            {
                beat0.dialogueLines.Add(new DialogueLine("DR. DYSON IDO", "CYBER-SURGEON & HUNTER-WARRIOR", "Easy, Alita. Give the neural bus a moment to calibrate. You're inside my cyber-repair clinic, down in the Scrapyard. I found your core in the slag heaps and rebuilt you on this examination slab."));
                beat0.dialogueLines.Add(new DialogueLine("ALITA", "MARTIAN CYBORG", "I don't... I don't remember anything. There's only static where my memories should be."));
                beat0.dialogueLines.Add(new DialogueLine("DR. DYSON IDO", "CYBER-SURGEON & HUNTER-WARRIOR", "Your quantum core was intact, but your memory blocks were wiped clean. No serial tags, no registry footprint. To the city, you don't exist."));
                beat0.dialogueLines.Add(new DialogueLine("DR. DYSON IDO", "CYBER-SURGEON & HUNTER-WARRIOR", "The doll body is holding together, but you need to test the motor relays on real ground. Walk with me to the door. Let's head outside to Sector 4 plaza."));
            }
            else
            {
                beat0.dialogueLines.Add(new DialogueLine("DR. DYSON IDO", "CYBER-SURGEON & HUNTER-WARRIOR", "Easy, Alita. Give the neural bus a moment to calibrate. We've just stepped outside my clinic into the lower alleys of the Scrapyard. I found your core in the slag heaps and rebuilt you."));
                beat0.dialogueLines.Add(new DialogueLine("ALITA", "MARTIAN CYBORG", "I don't... I don't remember anything. There's only static where my memories should be."));
                beat0.dialogueLines.Add(new DialogueLine("DR. DYSON IDO", "CYBER-SURGEON & HUNTER-WARRIOR", "Your quantum core was intact, but your memory blocks were wiped clean. No serial tags, no registry footprint. To the city, you don't exist."));
                beat0.dialogueLines.Add(new DialogueLine("DR. DYSON IDO", "CYBER-SURGEON & HUNTER-WARRIOR", "The doll body is holding together, but you need to test the motor relays on real ground. Walk with me up through the catwalks to the Sector 4 plaza."));
            }
            narrativeBeats.Add(beat0);

            // Beat 1: Street Plaza Outside Clinic (The Scrapyard & The Sky City)
            var beat1 = new PrologueDialogueBeat()
            {
                beatTitle = "Sector 4 Plaza // Shadow of Zalem",
                waypointPosition = new Vector3(8.50f, -17.92f, 56.25f),
                idoAnimTrigger = "Idle_Look_Sky",
                hasInteractiveLookAt = true,
                lookAtPromptText = "[E] GAZE AT SKYLINE",
                lookAtWorldTarget = new Vector3(8.5f, 60.0f, 80.0f),
                lookAtFov = 42f,
                lookAtDuration = 3.5f
            };
            beat1.dialogueLines.Add(new DialogueLine("DR. DYSON IDO", "CYBER-SURGEON & HUNTER-WARRIOR", "Take a look up. That shadow hanging over us is Zalem—the floating city. Everything down here is what they discard and cast into the dirt."));
            beat1.dialogueLines.Add(new DialogueLine("ALITA", "MARTIAN CYBORG", "The air smells like burned copper and ozone. It feels... strangely familiar."));
            beat1.dialogueLines.Add(new DialogueLine("DR. DYSON IDO", "CYBER-SURGEON & HUNTER-WARRIOR", "Keep your guard up. The Factory controls the district through bounty hunter Hunter-Warriors. Out here, unregistered tech like yours is a target."));
            beat1.dialogueLines.Add(new DialogueLine("DR. DYSON IDO", "CYBER-SURGEON & HUNTER-WARRIOR", "Before we go further, take a moment to explore the city. Walk the plaza, get a feel for the streets, and talk to the locals. Learn how people live down here."));
            beat1.dialogueLines.Add(new DialogueLine("ALITA", "MARTIAN CYBORG", "Is there something I should be careful about?"));
            beat1.dialogueLines.Add(new DialogueLine("DR. DYSON IDO", "CYBER-SURGEON & HUNTER-WARRIOR", "Be warned: this city is messed up. You're a stranger with no registry tag, and people here are paranoid. Some of them will take a swing at you just for being new. Not everyone will be hostile, but don't be surprised if you get hit."));
            beat1.dialogueLines[beat1.dialogueLines.Count - 1].givesCityWarning = true;
            beat1.dialogueLines.Add(new DialogueLine("ALITA", "MARTIAN CYBORG", "I'll keep my guard up. And if they come at me?"));
            beat1.dialogueLines.Add(new DialogueLine("DR. DYSON IDO", "CYBER-SURGEON & HUNTER-WARRIOR", "Handle it, but remember they're mostly scared people, not soldiers. Once the city learns what you are, and what you can do, that will change."));
            narrativeBeats.Add(beat1);

            // Beat 2: Lower Bazaar & Industrial Strip (The Scrapyard Economy)
            var beat2 = new PrologueDialogueBeat()
            {
                beatTitle = "Lower Bazaar // Scrap & Chrome",
                waypointPosition = new Vector3(-12.83f, -17.92f, 55.67f),
                idoAnimTrigger = "Idle_Thoughtful",
                hasInteractiveLookAt = true,
                lookAtPromptText = "[E] SCAN LOWER ALLEYWAYS",
                lookAtWorldTarget = new Vector3(-12.83f, 15f, 65f),
                lookAtFov = 45f,
                lookAtDuration = 3.5f
            };
            beat2.dialogueLines.Add(new DialogueLine("DR. DYSON IDO", "CYBER-SURGEON & HUNTER-WARRIOR", "See those salvage queues down the alley? Half the district is running on borrowed chrome and leaking coolant. You break down out here, scavengers strip you before sundown."));
            beat2.dialogueLines.Add(new DialogueLine("ALITA", "MARTIAN CYBORG", "I can feel the power lines humming through the catwalks. My optic telemetry is tracking every signal in the crowd."));
            beat2.dialogueLines.Add(new DialogueLine("DR. DYSON IDO", "CYBER-SURGEON & HUNTER-WARRIOR", "That's your sensor array filtering interference. Good sign—your plasma reactor is running clean and cold."));
            narrativeBeats.Add(beat2);

            // Beat 3: Mid-Level Skywalk (Enforcer Patrols)
            var beat3 = new PrologueDialogueBeat()
            {
                beatTitle = "Mid-Level Skywalk // Enforcer Gantries",
                waypointPosition = new Vector3(-14.50f, -5.92f, 14.17f),
                idoAnimTrigger = "Idle_HandsOnHips",
                hasInteractiveLookAt = true,
                lookAtPromptText = "[E] SCAN OVERHEAD GANTRIES",
                lookAtWorldTarget = new Vector3(-10f, 8f, 22f),
                lookAtFov = 38f,
                lookAtDuration = 3.5f
            };
            beat3.dialogueLines.Add(new DialogueLine("ALITA", "MARTIAN CYBORG", "Heavy patrol drones on the upper gantries. Optics locked in our direction."));
            beat3.dialogueLines.Add(new DialogueLine("DR. DYSON IDO", "CYBER-SURGEON & HUNTER-WARRIOR", "Vector enforcers. Factory hit squads. Keep your head down and stay near the railing. If their scanners flag your core frequency, they won't hesitate to engage."));
            narrativeBeats.Add(beat3);

            // Beat 4: Sector 7 Skybridge Entrance (Martian Instincts Awaken)
            var beat4 = new PrologueDialogueBeat()
            {
                beatTitle = "Sector 7 Skybridge // The Combat Trial",
                waypointPosition = new Vector3(-2.50f, 0.08f, -9.33f),
                idoAnimTrigger = "Idle_Base",
                hasInteractiveLookAt = true,
                lookAtPromptText = "[E] OVERLOOK SKYBRIDGE ARENA",
                lookAtWorldTarget = new Vector3(0f, 0.5f, -8.75f),
                lookAtFov = 40f,
                lookAtDuration = 3.5f
            };
            beat4.dialogueLines.Add(new DialogueLine("ALITA", "MARTIAN CYBORG", "Ido... wait. My heart. The pulse is accelerating, but it's not fear. My arms... I know how to hold this blade. My body remembers what my mind lost."));
            beat4.dialogueLines.Add(new DialogueLine("DR. DYSON IDO", "CYBER-SURGEON & HUNTER-WARRIOR", "That reflex posture... it's pure martial instinct. Ancient Panzer Kunst from the Lost Age. I've only read about that in pre-collapse military codices."));
            beat4.dialogueLines.Add(new DialogueLine("DR. DYSON IDO", "CYBER-SURGEON & HUNTER-WARRIOR", "Rogue combat units have locked down the bridge ahead and cut off transit. There's no talking our way through. Let your instincts take over, Alita. I've got your back."));
            narrativeBeats.Add(beat4);
        }
    }
}
