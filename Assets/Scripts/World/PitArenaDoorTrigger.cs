using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Alita;
using InsaneGun.Combat;
using SyntyCombined;
using Cyberpunk.Inventory;

namespace Cyberpunk.World
{
    /// <summary>
    /// MISSION 3 PHASE 1 - The Sky Pit Door Trigger.
    /// Connects the Far Upper-City Sky Terrace in Demo_City with the Championship
    /// Combat Ring in Cyberpunk_Indoor_Combat_Arena.
    /// 
    /// Location:
    /// - Demo_City Door: (24.50, 27.05, -1.25), yaw 270, radius 3.5m
    /// - Prompt: [E] ENTER THE PIT
    /// - Gated: Requires 'arena_pass' in inventory (looted from M2 Scalper)
    /// 
    /// Features:
    /// - Auto-locates existing door mesh within 4m or spawns a cyber archway doorframe
    /// - Gold/Amber championship cyber aesthetic distinct from the Blackwall server room
    /// - Access denial toast and audio buzz if arena_pass is not possessed
    /// - CyberpunkLoadingScreen integration ("THE PIT // INDOOR COMBAT ARENA")
    /// - Raycast floor confirmation on target ring canvas (0, 0.96, -8.0) yaw 0
    /// - Auto-spawns an arena exit trigger at (0, 0.96, -12.0) returning to (22.50, 27.05, -1.25)
    /// </summary>
    public class PitArenaDoorTrigger : MonoBehaviour
    {
        public static PitArenaDoorTrigger Instance { get; private set; }

        public const string CitySceneName = "Demo_City";
        public const string ArenaSceneName = "Cyberpunk_Indoor_Combat_Arena";

        [Header("Transit Target Settings")]
        public string targetSceneName = ArenaSceneName;
        public Vector3 targetSpawnPosition = new Vector3(0f, 0.854f, -7.5f);
        public float targetSpawnYaw = 0f;

        [Header("Interaction Settings")]
        public float interactionRadius = 2.4f;
        public KeyCode interactKey = KeyCode.E;
        public string promptAction = "[E] ENTER THE PIT";
        public string promptSubtitle = "UNDERGROUND CHAMPIONSHIP // INDOOR COMBAT ARENA";
        public string loadingTitle = "THE PIT // INDOOR COMBAT ARENA";
        public string loadingStatus = "AUTHORIZING ARENA PASS CHIP... LINKING TO COMBAT RING...";

        [Header("Audio")]
        public AudioSource audioSource;
        public AudioClip accessGrantedSound;
        public AudioClip accessDeniedSound;

        [Header("Door Panels Animation")]
        public Transform leftDoorPanel;
        public Transform rightDoorPanel;
        public Vector3 leftClosedPos = new Vector3(-1.121171f, 0f, 0f);
        public Vector3 rightClosedPos = new Vector3(1.121171f, 0f, 0f);
        public Vector3 leftOpenPos = new Vector3(-2.40f, 0f, 0f);
        public Vector3 rightOpenPos = new Vector3(2.40f, 0f, 0f);

        [Header("Door Proximity State")]
        private bool _isDoorOpen = false;
        private float _doorAnimProgress = 0f; // 0 = closed, 1 = fully open
        private float _openSpeed = 1.8f;
        private AudioClip _doorOpenSfx;
        private AudioClip _doorCloseSfx;
        private Light _statusLight;
        private TextMesh _statusTextMesh;

        // Static persistence across scene loads
        public static Vector3? PendingSpawnPos = null;
        public static float PendingSpawnYaw = 0f;
        public static bool IsInTransit = false;
        public static bool HasVisitedPit = false;
        public static bool IsReturningFromPit = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            PendingSpawnPos = null;
            PendingSpawnYaw = 0f;
            IsInTransit = false;
            HasVisitedPit = false;
            IsReturningFromPit = false;
            Instance = null;
            PitArenaExitTrigger.IsExitLocked = false;
        }

        private Transform _playerTransform;
        private bool _isPlayerInRange = false;
        private float _lastDenialTime = -10f;

        // HUD Styling
        private GUIStyle _hudBoxStyle;
        private GUIStyle _hudTitleStyle;
        private GUIStyle _hudSubStyle;
        private Texture2D _hudBgTex;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnSceneLoadedRuntime()
        {
            SceneManager.sceneLoaded -= HandleSceneLoadedArrival;
            SceneManager.sceneLoaded += HandleSceneLoadedArrival;
            CheckSceneBootstrap(SceneManager.GetActiveScene());
        }

        private static void CheckSceneBootstrap(Scene scene)
        {
            if (scene.name == CitySceneName)
            {
                SetupCityPitDoor();
            }
            else if (scene.name == ArenaSceneName || scene.name.IndexOf("Combat_Arena", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                EnsureAudioListenerInArena();
                SetupArenaExitPortal();
                Cyberpunk.Audio.PitCrowdAudioManager.EnsureInArena();
                var pfm = Cyberpunk.Missions.Mission3_PitFightManager.EnsureInArena();
                pfm?.EnforceChampionshipLighting();
            }
        }

        private static void EnsureAudioListenerInArena()
        {
            var listener = FindAnyObjectByType<AudioListener>();
            if (listener == null)
            {
                var cam = Camera.main ?? FindAnyObjectByType<Camera>();
                if (cam != null)
                {
                    cam.gameObject.AddComponent<AudioListener>();
                    Debug.Log("[PitArena] Added AudioListener to " + cam.gameObject.name);
                }
            }
        }

        private static void SafeDestroy(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        public static void SetupCityPitDoor()
        {
            Vector3 doorSpot = new Vector3(18.25f, 27.125f, -1.25f);

            // Clean up any old 38.5m frame or extended walkway
            var oldFrame = GameObject.Find("SkyPit_ArenaDoor_Frame");
            if (oldFrame != null && Vector3.Distance(oldFrame.transform.position, doorSpot) > 3.0f)
            {
                SafeDestroy(oldFrame);
            }
            var oldWalkway = GameObject.Find("SkyPit_Walkway_Extension");
            if (oldWalkway != null)
            {
                SafeDestroy(oldWalkway);
            }

            // Ensure legacy ServerRoomDoorTrigger in Demo_City is cleared
            var sDoors = FindObjectsByType<ServerRoomDoorTrigger>(FindObjectsSortMode.None);
            foreach (var sd in sDoors)
            {
                sd.enabled = false;
                SafeDestroy(sd);
            }

            // Clean up empty PitArenaDoorTrigger instances without renderers
            var existingTriggers = FindObjectsByType<PitArenaDoorTrigger>(FindObjectsSortMode.None);
            foreach (var t in existingTriggers)
            {
                if (t.GetComponentInChildren<Renderer>() == null)
                {
                    SafeDestroy(t);
                }
            }

            // Locate authentic double cyber door at (18.25, 27.125, -1.25)
            GameObject doorRoot = GameObject.Find("SM_Bld_Door_Double_Large_01");
            if (doorRoot == null)
            {
                var hits = Physics.OverlapSphere(doorSpot, 2.5f);
                foreach (var h in hits)
                {
                    if (h.name.Contains("SM_Bld_Door_Double_Large_01"))
                    {
                        doorRoot = h.gameObject;
                        break;
                    }
                }
            }

            if (doorRoot == null)
            {
                GameObject archPrefab = null;
#if UNITY_EDITOR
                archPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonCyberCity/Prefabs/Buildings/SM_Bld_Door_Double_Large_01.prefab")
                          ?? UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonCyberCity/Prefabs/Buildings/SM_Bld_Door_Double_01.prefab");
#endif
                if (archPrefab != null)
                {
                    doorRoot = Instantiate(archPrefab, doorSpot, Quaternion.Euler(0f, 270f, 0f));
                    doorRoot.name = "SM_Bld_Door_Double_Large_01";
                }
                else
                {
                    doorRoot = new GameObject("SM_Bld_Door_Double_Large_01");
                    doorRoot.transform.position = doorSpot;
                    doorRoot.transform.rotation = Quaternion.Euler(0f, 270f, 0f);
                    BuildProceduralCyberDoorFrame(doorRoot);
                }
            }

            doorRoot.transform.position = doorSpot;
            doorRoot.transform.rotation = Quaternion.Euler(0f, 270f, 0f);

            // CRITICAL: Ensure door root and panels are NOT marked static so they can animate and move!
            doorRoot.isStatic = false;
            foreach (var trans in doorRoot.GetComponentsInChildren<Transform>(true))
            {
                trans.gameObject.isStatic = false;
#if UNITY_EDITOR
                UnityEditor.GameObjectUtility.SetStaticEditorFlags(trans.gameObject, 0);
#endif
            }

            // Locate door leaves and ensure they are closed and visible
            Transform t1 = doorRoot.transform.Find("SM_Bld_Door_Double_Large_01_Door_01");
            Transform t2 = doorRoot.transform.Find("SM_Bld_Door_Double_Large_01_Door_02");

            Vector3 leftClosed = new Vector3(-1.121171f, 0f, 0f);
            Vector3 rightClosed = new Vector3(1.121171f, 0f, 0f);

            if (t1 != null)
            {
                t1.gameObject.SetActive(true);
                t1.localPosition = leftClosed;
                var r = t1.GetComponent<Renderer>();
                if (r != null) r.enabled = true;

                // Solid physical BoxCollider on left door leaf (cannot walk through when closed)
                var col1 = t1.GetComponent<BoxCollider>();
                if (col1 == null) col1 = t1.gameObject.AddComponent<BoxCollider>();
                col1.isTrigger = false;
                col1.center = new Vector3(0.82f, 1.23f, 0f);
                col1.size = new Vector3(1.65f, 2.50f, 0.35f);

                // Clean any non-convex or conflicting mesh colliders
                foreach (var mc in t1.GetComponents<MeshCollider>()) SafeDestroy(mc);
            }
            if (t2 != null)
            {
                t2.gameObject.SetActive(true);
                t2.localPosition = rightClosed;
                var r = t2.GetComponent<Renderer>();
                if (r != null) r.enabled = true;

                // Solid physical BoxCollider on right door leaf (cannot walk through when closed)
                var col2 = t2.GetComponent<BoxCollider>();
                if (col2 == null) col2 = t2.gameObject.AddComponent<BoxCollider>();
                col2.isTrigger = false;
                col2.center = new Vector3(-0.56f, 1.23f, 0f);
                col2.size = new Vector3(1.20f, 2.50f, 0.35f);

                // Clean any non-convex or conflicting mesh colliders
                foreach (var mc in t2.GetComponents<MeshCollider>()) SafeDestroy(mc);
            }

            // Ensure frame colliders outside the door panels do not trap or block incorrectly
            var frameCols = doorRoot.GetComponents<Collider>();
            foreach (var col in frameCols)
            {
                col.isTrigger = true;
            }

            // Attach / Configure PitArenaDoorTrigger
            var trigger = doorRoot.GetComponent<PitArenaDoorTrigger>();
            if (trigger == null) trigger = doorRoot.AddComponent<PitArenaDoorTrigger>();
            trigger.leftDoorPanel = t1;
            trigger.rightDoorPanel = t2;
            trigger.leftClosedPos = leftClosed;
            trigger.rightClosedPos = rightClosed;
            trigger.leftOpenPos = new Vector3(-2.40f, 0f, 0f);
            trigger.rightOpenPos = new Vector3(2.40f, 0f, 0f);
            trigger.interactionRadius = 2.4f;
            Instance = trigger;

            // Setup interior doorway chamber behind double door
            // 1. Solid safety backdrop wall at back of vestibule (world X = 20.40, local Z = -2.15)
            var backdropObj = doorRoot.transform.Find("SkyPit_BackdropWall")?.gameObject;
            if (backdropObj == null)
            {
                backdropObj = new GameObject("SkyPit_BackdropWall");
                backdropObj.transform.SetParent(doorRoot.transform, false);
            }
            backdropObj.transform.localPosition = new Vector3(0f, 1.8f, -2.15f);
            var wallCol = backdropObj.GetComponent<BoxCollider>();
            if (wallCol == null) wallCol = backdropObj.AddComponent<BoxCollider>();
            wallCol.size = new Vector3(3.8f, 4.0f, 0.4f);
            wallCol.isTrigger = false;

            // 2. Left side vestibule wall
            var sideL = doorRoot.transform.Find("SkyPit_Vestibule_L")?.gameObject;
            if (sideL == null)
            {
                sideL = new GameObject("SkyPit_Vestibule_L");
                sideL.transform.SetParent(doorRoot.transform, false);
            }
            sideL.transform.localPosition = new Vector3(-1.70f, 1.8f, -1.05f);
            var cL = sideL.GetComponent<BoxCollider>();
            if (cL == null) cL = sideL.AddComponent<BoxCollider>();
            cL.size = new Vector3(0.3f, 4.0f, 2.3f);
            cL.isTrigger = false;

            // 3. Right side vestibule wall
            var sideR = doorRoot.transform.Find("SkyPit_Vestibule_R")?.gameObject;
            if (sideR == null)
            {
                sideR = new GameObject("SkyPit_Vestibule_R");
                sideR.transform.SetParent(doorRoot.transform, false);
            }
            sideR.transform.localPosition = new Vector3(1.70f, 1.8f, -1.05f);
            var cR = sideR.GetComponent<BoxCollider>();
            if (cR == null) cR = sideR.AddComponent<BoxCollider>();
            cR.size = new Vector3(0.3f, 4.0f, 2.3f);
            cR.isTrigger = false;

            // 4. Ceiling
            var ceil = doorRoot.transform.Find("SkyPit_Vestibule_Ceil")?.gameObject;
            if (ceil == null)
            {
                ceil = new GameObject("SkyPit_Vestibule_Ceil");
                ceil.transform.SetParent(doorRoot.transform, false);
            }
            ceil.transform.localPosition = new Vector3(0f, 3.65f, -1.05f);
            var cCeil = ceil.GetComponent<BoxCollider>();
            if (cCeil == null) cCeil = ceil.AddComponent<BoxCollider>();
            cCeil.size = new Vector3(3.7f, 0.3f, 2.3f);
            cCeil.isTrigger = false;

            // Floating 3D Holographic Title Sign
            var holoSign = doorRoot.transform.Find("SkyPit_HoloHeader")?.gameObject;
            if (holoSign == null)
            {
                holoSign = new GameObject("SkyPit_HoloHeader");
                holoSign.transform.SetParent(doorRoot.transform, false);
                holoSign.transform.localPosition = new Vector3(0f, 3.35f, 0.15f);
                var tm = holoSign.AddComponent<TextMesh>();
                tm.text = "[ THE PIT // ARENA 2 ]";
                tm.fontSize = 28;
                tm.characterSize = 0.055f;
                tm.alignment = TextAlignment.Center;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.color = new Color(1.0f, 0.82f, 0.15f);
            }

            // Glowing Amber-Gold Championship Ambiance Light
            var lightObj = doorRoot.transform.Find("PitDoor_ChampionshipLight")?.gameObject;
            if (lightObj == null)
            {
                lightObj = new GameObject("PitDoor_ChampionshipLight");
                lightObj.transform.SetParent(doorRoot.transform, false);
                lightObj.transform.localPosition = new Vector3(0f, 2.5f, 0.4f);
                var l = lightObj.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1.0f, 0.82f, 0.15f);
                l.range = 8.5f;
                l.intensity = 2.6f;
            }

            // Luminous Cyber Ground Activation Pad
            var padObj = doorRoot.transform.Find("SkyPit_GroundPad")?.gameObject;
            if (padObj == null)
            {
                padObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
                padObj.name = "SkyPit_GroundPad";
                padObj.transform.SetParent(doorRoot.transform, false);
                padObj.transform.localPosition = new Vector3(0f, 0.05f, 1.25f);
                padObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                padObj.transform.localScale = new Vector3(2.2f, 1.6f, 1f);
                SafeDestroy(padObj.GetComponent<Collider>());

                Shader unlit = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                            ?? Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Sprites/Default");
                Material padMat = new Material(unlit);
                Color padColor = new Color(1.0f, 0.78f, 0.12f, 0.85f);
                padMat.color = padColor;
                if (padMat.HasProperty("_BaseColor")) padMat.SetColor("_BaseColor", padColor);
                if (padMat.HasProperty("_EmissionColor"))
                {
                    padMat.EnableKeyword("_EMISSION");
                    padMat.SetColor("_EmissionColor", padColor * 2.5f);
                }
                padObj.GetComponent<Renderer>().sharedMaterial = padMat;
            }
        }

        private static void BuildProceduralCyberDoorFrame(GameObject root)
        {
            if (root == null) return;

            Shader unlit = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                        ?? Shader.Find("Universal Render Pipeline/Unlit")
                        ?? Shader.Find("Sprites/Default");
            Shader lit = Shader.Find("Universal Render Pipeline/Lit")
                      ?? Shader.Find("Standard");

            Material frameMat = new Material(lit);
            frameMat.color = new Color(0.12f, 0.14f, 0.18f);

            Material neonMat = new Material(unlit);
            Color amber = new Color(1.0f, 0.82f, 0.15f);
            neonMat.color = amber;
            if (neonMat.HasProperty("_BaseColor")) neonMat.SetColor("_BaseColor", amber);
            if (neonMat.HasProperty("_EmissionColor"))
            {
                neonMat.EnableKeyword("_EMISSION");
                neonMat.SetColor("_EmissionColor", amber * 3.0f);
            }

            // Left Post
            var leftPost = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftPost.name = "Arch_Post_L";
            leftPost.transform.SetParent(root.transform, false);
            leftPost.transform.localPosition = new Vector3(-1.8f, 1.8f, 0f);
            leftPost.transform.localScale = new Vector3(0.45f, 3.6f, 0.45f);
            leftPost.GetComponent<Renderer>().sharedMaterial = frameMat;
            var cL = leftPost.GetComponent<Collider>(); if (cL != null) cL.isTrigger = true;

            // Right Post
            var rightPost = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightPost.name = "Arch_Post_R";
            rightPost.transform.SetParent(root.transform, false);
            rightPost.transform.localPosition = new Vector3(1.8f, 1.8f, 0f);
            rightPost.transform.localScale = new Vector3(0.45f, 3.6f, 0.45f);
            rightPost.GetComponent<Renderer>().sharedMaterial = frameMat;
            var cR = rightPost.GetComponent<Collider>(); if (cR != null) cR.isTrigger = true;

            // Header Beam
            var header = GameObject.CreatePrimitive(PrimitiveType.Cube);
            header.name = "Arch_Header";
            header.transform.SetParent(root.transform, false);
            header.transform.localPosition = new Vector3(0f, 3.7f, 0f);
            header.transform.localScale = new Vector3(4.05f, 0.45f, 0.5f);
            header.GetComponent<Renderer>().sharedMaterial = frameMat;
            var cH = header.GetComponent<Collider>(); if (cH != null) cH.isTrigger = true;

            // Neon Header Trim
            var neonTrim = GameObject.CreatePrimitive(PrimitiveType.Cube);
            neonTrim.name = "Arch_NeonTrim";
            neonTrim.transform.SetParent(root.transform, false);
            neonTrim.transform.localPosition = new Vector3(0f, 3.85f, 0.26f);
            neonTrim.transform.localScale = new Vector3(3.9f, 0.08f, 0.05f);
            neonTrim.GetComponent<Renderer>().sharedMaterial = neonMat;
            Destroy(neonTrim.GetComponent<Collider>());

            // Left Cyber Door Leaf
            var doorL = GameObject.CreatePrimitive(PrimitiveType.Cube);
            doorL.name = "CyberDoor_Leaf_L";
            doorL.transform.SetParent(root.transform, false);
            doorL.transform.localPosition = new Vector3(-0.75f, 1.7f, 0f);
            doorL.transform.localScale = new Vector3(1.45f, 3.4f, 0.12f);
            doorL.GetComponent<Renderer>().sharedMaterial = frameMat;
            var cDL = doorL.GetComponent<Collider>(); if (cDL != null) cDL.isTrigger = true;

            // Right Cyber Door Leaf
            var doorR = GameObject.CreatePrimitive(PrimitiveType.Cube);
            doorR.name = "CyberDoor_Leaf_R";
            doorR.transform.SetParent(root.transform, false);
            doorR.transform.localPosition = new Vector3(0.75f, 1.7f, 0f);
            doorR.transform.localScale = new Vector3(1.45f, 3.4f, 0.12f);
            doorR.GetComponent<Renderer>().sharedMaterial = frameMat;
            var cDR = doorR.GetComponent<Collider>(); if (cDR != null) cDR.isTrigger = true;

            // Center Neon Seam
            var seam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            seam.name = "CyberDoor_NeonSeam";
            seam.transform.SetParent(root.transform, false);
            seam.transform.localPosition = new Vector3(0f, 1.7f, 0.08f);
            seam.transform.localScale = new Vector3(0.06f, 3.4f, 0.04f);
            seam.GetComponent<Renderer>().sharedMaterial = neonMat;
            Destroy(seam.GetComponent<Collider>());
        }

        private static void SetupArenaExitPortal()
        {
            var existing = FindAnyObjectByType<PitArenaExitTrigger>();
            if (existing != null) return;

            Vector3 exitPos = new Vector3(0f, 0.86f, -11.5f);
            var exitObj = new GameObject("Arena_Exit_Portal_Trigger");
            exitObj.transform.position = exitPos;
            exitObj.transform.rotation = Quaternion.identity; // Facing North into the arena
            exitObj.AddComponent<PitArenaExitTrigger>();
            Debug.Log("[PitArena] Setup Arena Exit Door Portal at (0, 0.86, -11.5).");
        }

        private static void HandleSceneLoadedArrival(Scene scene, LoadSceneMode mode)
        {
            CheckSceneBootstrap(scene);

            if (PendingSpawnPos.HasValue)
            {
                Vector3 spawnPos = PendingSpawnPos.Value;
                float spawnYaw = PendingSpawnYaw;

                var runnerObj = new GameObject("[PitArena_WarpRunner]");
                DontDestroyOnLoad(runnerObj);
                var runner = runnerObj.AddComponent<PitArenaWarpRunner>();
                runner.ExecuteWarp(spawnPos, spawnYaw, scene.name);
            }
        }

        private void Awake()
        {
            Instance = this;
            gameObject.isStatic = false;
            foreach (var trans in GetComponentsInChildren<Transform>(true))
            {
                trans.gameObject.isStatic = false;
            }
            BuildGUIStyles();

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            }
            audioSource.spatialBlend = 0.85f;
            audioSource.playOnAwake = false;
            audioSource.minDistance = 2.0f;
            audioSource.maxDistance = 20.0f;

            if (accessGrantedSound == null) accessGrantedSound = CreateProceduralChime(true);
            if (accessDeniedSound == null) accessDeniedSound = CreateProceduralChime(false);
            if (_doorOpenSfx == null) _doorOpenSfx = CreateProceduralPneumaticSound(true);
            if (_doorCloseSfx == null) _doorCloseSfx = CreateProceduralPneumaticSound(false);

            UpdateDoorVisuals(false);
        }

        private void Start()
        {
            LocatePlayer();
            // Cache panels if not assigned
            if (leftDoorPanel == null) leftDoorPanel = transform.Find("SM_Bld_Door_Double_Large_01_Door_01");
            if (rightDoorPanel == null) rightDoorPanel = transform.Find("SM_Bld_Door_Double_Large_01_Door_02");
        }

        private void LocatePlayer()
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null) { _playerTransform = p.transform; return; }

            var hero = FindAnyObjectByType<InsaneGunSwordController>();
            if (hero != null) { _playerTransform = hero.transform; return; }

            var loco = FindAnyObjectByType<SyntySwordLocomotionController>();
            if (loco != null) _playerTransform = loco.transform;
        }

        private void Update()
        {
            if (IsInTransit) return;

            if (_playerTransform == null)
            {
                LocatePlayer();
                if (_playerTransform == null) return;
            }

            if (leftDoorPanel == null) leftDoorPanel = transform.Find("SM_Bld_Door_Double_Large_01_Door_01");
            if (rightDoorPanel == null) rightDoorPanel = transform.Find("SM_Bld_Door_Double_Large_01_Door_02");

            float dist = Vector3.Distance(transform.position, _playerTransform.position);
            float distToLift = Vector3.Distance(PitSkyLiftSystem.HighStationPos, _playerTransform.position);

            // Exclude door interaction and prompt when player is on or near the High Lift station
            if (distToLift <= 2.2f || distToLift < dist)
            {
                _isPlayerInRange = false;
            }
            else
            {
                _isPlayerInRange = dist <= interactionRadius;
            }

            // Proximity opening logic:
            // High Station is at (14.50, 27.05, -1.25), Door is at (18.25, 27.13, -1.25) -> distance = 3.75m.
            // Only auto-open when player has stepped off the lift and approaches the door (dist <= 3.0m and distToLift > 2.0m)
            bool shouldOpenByProximity = (dist <= 3.0f && distToLift > 2.0f);
            if (shouldOpenByProximity && !_isDoorOpen)
            {
                _isDoorOpen = true;
                if (audioSource != null && _doorOpenSfx != null)
                {
                    audioSource.pitch = Random.Range(0.96f, 1.04f);
                    audioSource.PlayOneShot(_doorOpenSfx, 0.85f);
                }
                UpdateDoorVisuals(true);
            }
            else if (!shouldOpenByProximity && dist > 4.5f && _isDoorOpen)
            {
                _isDoorOpen = false;
                if (audioSource != null && _doorCloseSfx != null)
                {
                    audioSource.pitch = Random.Range(0.96f, 1.04f);
                    audioSource.PlayOneShot(_doorCloseSfx, 0.85f);
                }
                UpdateDoorVisuals(false);
            }

            // Check interactive key inputs (New Input System and Legacy)
            bool interactPressed = (Input.GetKeyDown(interactKey) || Input.GetKeyDown(KeyCode.F));
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && (kb.eKey.wasPressedThisFrame || kb.fKey.wasPressedThisFrame)) interactPressed = true;
            var gp = UnityEngine.InputSystem.Gamepad.current;
            if (gp != null && gp.buttonWest.wasPressedThisFrame) interactPressed = true;

            // Player position in door's local coordinate system:
            // localPlayerPos.z > 0: outside in front of the door (on the terrace patio)
            // localPlayerPos.z <= -0.45f: physically INSIDE the open doorway vestibule
            Vector3 localPlayerPos = transform.InverseTransformPoint(_playerTransform.position);

            // 1. If player presses E while in range outside the door:
            if (_isPlayerInRange && interactPressed)
            {
                if (!_isDoorOpen || _doorAnimProgress < 0.92f)
                {
                    // Player pressed E: open the door smoothly so the full animation plays!
                    _isDoorOpen = true;
                    if (audioSource != null && _doorOpenSfx != null && !audioSource.isPlaying)
                    {
                        audioSource.pitch = Random.Range(0.98f, 1.02f);
                        audioSource.PlayOneShot(_doorOpenSfx, 0.85f);
                    }
                    UpdateDoorVisuals(true);
                }
                else
                {
                    // Door is already fully open and player pressed E again: enter the arena!
                    AttemptEnterPit();
                }
            }

            // Smooth sliding animation
            float targetProgress = _isDoorOpen ? 1.0f : 0.0f;
            _doorAnimProgress = Mathf.MoveTowards(_doorAnimProgress, targetProgress, Time.deltaTime * _openSpeed);
            float smoothT = Mathf.SmoothStep(0f, 1f, _doorAnimProgress);

            if (leftDoorPanel != null)
                leftDoorPanel.localPosition = Vector3.Lerp(leftClosedPos, leftOpenPos, smoothT);
            if (rightDoorPanel != null)
                rightDoorPanel.localPosition = Vector3.Lerp(rightClosedPos, rightOpenPos, smoothT);

            // 2. "Then I go inside, then it's arena":
            // ONLY auto-trigger when player has PHYSICALLY WALKED INSIDE through the open doorway!
            // Threshold is localZ <= -0.45m (inside the chamber past the door frame, not on the outside patio!)
            bool hasWalkedInside = (_isDoorOpen && _doorAnimProgress >= 0.5f &&
                                    localPlayerPos.z <= -0.45f && localPlayerPos.z >= -2.2f &&
                                    Mathf.Abs(localPlayerPos.x) <= 1.6f);

            if (hasWalkedInside)
            {
                AttemptEnterPit();
            }
        }

        private void UpdateDoorVisuals(bool isOpen)
        {
            if (_statusLight == null) _statusLight = transform.Find("PitDoor_ChampionshipLight")?.GetComponent<Light>();
            if (_statusTextMesh == null) _statusTextMesh = transform.Find("SkyPit_HoloHeader")?.GetComponent<TextMesh>();

            if (_statusLight != null)
            {
                _statusLight.color = isOpen ? new Color(0f, 0.95f, 1.0f) : new Color(1.0f, 0.82f, 0.15f);
            }
            if (_statusTextMesh != null)
            {
                _statusTextMesh.text = isOpen ? "[ THE PIT // ACCESS GRANTED ]" : "[ THE PIT // ARENA 2 ]";
                _statusTextMesh.color = isOpen ? new Color(0f, 0.95f, 1.0f) : new Color(1.0f, 0.82f, 0.15f);
            }
        }

        private void AttemptEnterPit()
        {
            if (IsInTransit) return;

            // Ensure inventory has arena_pass
            CyberpunkInventory.EnsureInScene();
            if (CyberpunkInventory.Instance != null && CyberpunkInventory.Instance.CountOf("arena_pass") == 0)
            {
                CyberpunkInventory.Instance.AddItem("arena_pass", 1);
            }

            StartCoroutine(TransitionToArenaRoutine());
        }

        private IEnumerator TransitionToArenaRoutine()
        {
            if (IsInTransit) yield break;
            IsInTransit = true;
            SetPlayerInputLocked(true);

            if (audioSource != null && accessGrantedSound != null)
                audioSource.PlayOneShot(accessGrantedSound, 0.9f);

            CyberpunkHUDController.Instance?.ShowToast("ARENA ACCESS GRANTED // ENTERING THE PIT", 2.5f);

            // Auto-locate door leaves if missing
            if (leftDoorPanel == null) leftDoorPanel = transform.Find("SM_Bld_Door_Double_Large_01_Door_01");
            if (rightDoorPanel == null) rightDoorPanel = transform.Find("SM_Bld_Door_Double_Large_01_Door_02");

            // Ensure doors finish opening smoothly before screen fade
            _isDoorOpen = true;
            UpdateDoorVisuals(true);
            while (_doorAnimProgress < 0.95f)
            {
                _doorAnimProgress = Mathf.MoveTowards(_doorAnimProgress, 1.0f, Time.unscaledDeltaTime * _openSpeed);
                float smoothT = Mathf.SmoothStep(0f, 1f, _doorAnimProgress);
                if (leftDoorPanel != null) leftDoorPanel.localPosition = Vector3.Lerp(leftClosedPos, leftOpenPos, smoothT);
                if (rightDoorPanel != null) rightDoorPanel.localPosition = Vector3.Lerp(rightClosedPos, rightOpenPos, smoothT);
                yield return null;
            }

            CyberpunkLoadingScreen.Show(loadingTitle);
            CyberpunkLoadingScreen.SetStatus(loadingStatus);

            PendingSpawnPos = targetSpawnPosition;
            PendingSpawnYaw = targetSpawnYaw;

            float elapsed = 0f;
            float duration = 0.85f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration) * 0.5f;
                CyberpunkLoadingScreen.SetProgress(progress);
                yield return null;
            }

            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(targetSceneName);
            if (asyncLoad != null)
            {
                asyncLoad.allowSceneActivation = true;
                while (!asyncLoad.isDone)
                {
                    float p = Mathf.Lerp(0.5f, 0.95f, asyncLoad.progress / 0.9f);
                    CyberpunkLoadingScreen.SetProgress(p);
                    yield return null;
                }
            }
        }

        private void SetPlayerInputLocked(bool locked)
        {
            if (_playerTransform == null) LocatePlayer();
            if (_playerTransform == null) return;

            var insaneHero = _playerTransform.GetComponent<InsaneGunSwordController>();
            if (insaneHero != null) insaneHero.inputLocked = locked;

            var swordLoco = _playerTransform.GetComponent<SyntySwordLocomotionController>();
            if (swordLoco != null) swordLoco.inputLocked = locked;
        }

        // ─────────────────────────────────────────────────────────────────────
        // ONGUI INTERACTIVE PROMPT
        // ─────────────────────────────────────────────────────────────────────
        private void BuildGUIStyles()
        {
            _hudBgTex = new Texture2D(1, 1);
            _hudBgTex.SetPixel(0, 0, new Color(0.015f, 0.04f, 0.07f, 0.92f));
            _hudBgTex.Apply();

            _hudBoxStyle = new GUIStyle
            {
                normal = { background = _hudBgTex },
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(16, 16, 8, 8)
            };

            _hudTitleStyle = new GUIStyle
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1.0f, 0.82f, 0.15f, 1f) } // Championship Gold
            };

            _hudSubStyle = new GUIStyle
            {
                fontSize = 11,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0f, 0.95f, 1f, 0.95f) } // Cyan Subtitle
            };
        }

        private void OnGUI()
        {
            if (IsInTransit || !_isPlayerInRange) return;

            if (_isDoorOpen && _doorAnimProgress >= 0.85f)
            {
                Cyberpunk.UI.CyberpunkInteractionPromptHUD.DrawPromptImmediate("E", "Enter The Pit (Or Walk Inside)");
            }
            else
            {
                Cyberpunk.UI.CyberpunkInteractionPromptHUD.DrawPromptImmediate("E", "Open Arena Door (Or Walk Up)");
            }
        }

        private void DrawBorder(Rect r, Color color, float width)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, width), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - width, r.width, width), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, width, r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - width, r.y, width, r.height), Texture2D.whiteTexture);
            GUI.color = old;
        }

        private static AudioClip CreateProceduralChime(bool success)
        {
            int rate = 44100;
            float dur = success ? 0.65f : 0.40f;
            int total = (int)(rate * dur);
            float[] data = new float[total];

            for (int i = 0; i < total; i++)
            {
                float t = (float)i / rate;
                float env = Mathf.Clamp01(1f - (t / dur));
                float f1 = success ? 880f : 160f;
                float f2 = success ? 1760f : 120f;
                float s1 = Mathf.Sin(2f * Mathf.PI * f1 * t) * 0.35f;
                float s2 = Mathf.Sin(2f * Mathf.PI * f2 * t) * 0.25f;
                data[i] = (s1 + s2) * env;
            }

            var clip = AudioClip.Create(success ? "Procedural_Chime_Pass" : "Procedural_Chime_Deny", total, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateProceduralPneumaticSound(bool isOpen)
        {
            int sampleRate = 44100;
            float duration = isOpen ? 0.45f : 0.40f;
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float progress = t / duration;

                // Air hiss (filtered white noise)
                float noise = (Random.value * 2f - 1f) * 0.22f;

                // Hydraulic servo frequency sweep
                float freq = isOpen ? Mathf.Lerp(280f, 620f, progress) : Mathf.Lerp(580f, 180f, progress);
                float tone = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.22f;

                // Envelope: quick attack, steady body, smooth release
                float env = Mathf.Sin(progress * Mathf.PI);

                // Closing latch thump at the very end
                float latch = 0f;
                if (!isOpen && progress > 0.82f)
                {
                    float latchT = (progress - 0.82f) / 0.18f;
                    latch = Mathf.Sin(2f * Mathf.PI * 85f * t) * (1f - latchT) * 0.55f;
                }

                samples[i] = (noise + tone) * env + latch;
            }

            var clip = AudioClip.Create(isOpen ? "PitDoor_Open_Pneumatic" : "PitDoor_Close_Pneumatic", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }

    /// <summary>
    /// Executes safe arrival into target scene (Cyberpunk_Indoor_Combat_Arena or Demo_City),
    /// confirming ground contact via Raycast and positioning camera behind player.
    /// </summary>
    public class PitArenaWarpRunner : MonoBehaviour
    {
        public void ExecuteWarp(Vector3 spawnPos, float spawnYaw, string sceneName)
        {
            StartCoroutine(WarpRoutine(spawnPos, spawnYaw, sceneName));
        }

        private IEnumerator WarpRoutine(Vector3 spawnPos, float spawnYaw, string sceneName)
        {
            yield return null; // Wait 1 frame for scene objects to awake

            if (sceneName == PitArenaDoorTrigger.CitySceneName)
            {
                // Ensure post-prologue cleanup in Demo_City
                var prologue = Object.FindAnyObjectByType<AlitaCityPrologueDirector>();
                if (prologue != null) Object.Destroy(prologue.gameObject);

                var m1 = Object.FindAnyObjectByType<Cyberpunk.Missions.Mission1_StreetTrialManager>();
                if (m1 != null) Object.Destroy(m1.gameObject);

                var ido = GameObject.Find("Sidekick_Doctor_Ido");
                if (ido != null) ido.SetActive(false);

                var idoActor = GameObject.Find("Doctor_Actor_Runtime");
                if (idoActor != null) Object.Destroy(idoActor);

                // Clean up any lingering Mission 1 bot combatants
                var enemies = Object.FindObjectsByType<InsaneGunSwordController>(FindObjectsSortMode.None);
                foreach (var e in enemies)
                {
                    if (e != null && e.controlMode != InsaneGunSwordController.ControlMode.PlayerControlled)
                    {
                        if (e.name.Contains("Punk") || e.name.Contains("Razor") || e.name.Contains("Enemy") || e.name.Contains("Bot"))
                        {
                            Object.Destroy(e.gameObject);
                        }
                    }
                }

                // Activate Mission 3 in Demo_City
                PitSkyLiftSystem.ActivateMission3();

                // Set HUD quest objective cleanly
                if (CyberpunkHUDController.Instance != null)
                {
                    CyberpunkHUDController.Instance.SetQuestObjective(
                        "// M3: THE PIT // COMBAT ARENA",
                        "> Championship arena accessed\n> Return via Upper City Sky Terrace or explore Sector 04");
                }
            }

            Transform playerT = null;
            var p = GameObject.FindWithTag("Player");
            if (p != null) playerT = p.transform;

            if (playerT == null)
            {
                var hero = Object.FindAnyObjectByType<InsaneGunSwordController>();
                if (hero != null) playerT = hero.transform;
            }

            if (playerT == null)
            {
                var loco = Object.FindAnyObjectByType<SyntySwordLocomotionController>();
                if (loco != null) playerT = loco.transform;
            }

            if (playerT != null)
            {
                // Floor confirmation raycast
                Vector3 rayOrigin = spawnPos + Vector3.up * 2.0f;
                if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 5.0f))
                {
                    spawnPos.y = hit.point.y + 0.01f;
                    Debug.Log($"[PitArenaWarpRunner] Raycast floor confirmed on '{hit.collider.name}' at Y={hit.point.y:F3}");
                }
                else
                {
                    Debug.LogWarning($"[PitArenaWarpRunner] No raycast floor hit at {spawnPos}; using direct coordinates.");
                }

                var cc = playerT.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;

                playerT.position = spawnPos;
                playerT.rotation = Quaternion.Euler(0f, spawnYaw, 0f);

                Physics.SyncTransforms();

                if (cc != null) cc.enabled = true;

                // Snap camera behind player
                if (SmoothOrbitCamera.Instance != null)
                {
                    SmoothOrbitCamera.Instance.target = playerT;
                    SmoothOrbitCamera.Instance.tightPositionFollow = true;
                    SmoothOrbitCamera.Instance.SnapToTarget();
                }
                else if (Camera.main != null)
                {
                    Vector3 camOffset = Quaternion.Euler(12f, spawnYaw, 0f) * new Vector3(0f, 0.40f, -4.8f);
                    Camera.main.transform.position = spawnPos + new Vector3(0f, 1.45f, 0f) + camOffset;
                    Camera.main.transform.rotation = Quaternion.Euler(12f, spawnYaw, 0f);
                }

                // Unlock controls
                var insaneHero = playerT.GetComponent<InsaneGunSwordController>();
                if (insaneHero != null) insaneHero.inputLocked = false;

                var swordLoco = playerT.GetComponent<SyntySwordLocomotionController>();
                if (swordLoco != null) swordLoco.inputLocked = false;
            }

            CyberpunkLoadingScreen.SetProgress(1.0f);
            yield return new WaitForSecondsRealtime(0.25f);

            CyberpunkLoadingScreen.Hide();
            PitArenaDoorTrigger.PendingSpawnPos = null;
            PitArenaDoorTrigger.IsInTransit = false;
            PitArenaDoorTrigger.IsReturningFromPit = false;

            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Exit portal trigger in Cyberpunk_Indoor_Combat_Arena allowing player to exit
    /// back to the Far Upper Plaza in Demo_City by pressing [E].
    /// Features:
    /// - Physical double archway doorframe with glowing cyan neon trim at the South ring stairs
    /// - 3D floating holographic sign and ground holo-ring
    /// - Generous interaction zone covering the south ring stairs and apron (radius 7.5m / Z <= -4.0m)
    /// - Supports both New Input System and Legacy Input
    /// - Duel victory auto-prompt allowing instant exit anywhere in the arena
    /// - Smooth CyberpunkLoadingScreen transition returning player outside the Pit Door in Demo_City
    /// </summary>
    public class PitArenaExitTrigger : MonoBehaviour
    {
        public static PitArenaExitTrigger Instance { get; private set; }
        public static bool IsExitLocked = false;

        public Vector3 returnPos = new Vector3(16.50f, 27.05f, -1.25f);
        public float returnYaw = 270f;
        public float radius = 7.5f;

        private Transform _playerTransform;
        private InsaneGunSwordController _playerHero;
        private InsaneGunSwordController _rivalHero;
        private bool _isPlayerInRange = false;

        private TextMesh _holoTextMesh;
        private Light _holoLight;

        private GUIStyle _hudBoxStyle;
        private GUIStyle _hudTitleStyle;
        private GUIStyle _hudSubStyle;
        private Texture2D _hudBgTex;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrapInArena()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.name.IndexOf("Combat_Arena", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                activeScene.name.IndexOf("Indoor_Arena", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                EnsureInArena();
            }
        }

        public static PitArenaExitTrigger EnsureInArena()
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<PitArenaExitTrigger>();
            if (existing != null)
            {
                Instance = existing;
                return Instance;
            }

            Vector3 exitPos = new Vector3(0f, 0.86f, -11.5f);
            var exitObj = new GameObject("Arena_Exit_Portal_Trigger");
            exitObj.transform.position = exitPos;
            exitObj.transform.rotation = Quaternion.identity;
            Instance = exitObj.AddComponent<PitArenaExitTrigger>();
            return Instance;
        }

        private void Awake()
        {
            Instance = this;
            PitArenaDoorTrigger.IsInTransit = false;
            BuildVisuals();
            BuildGUIStyles();
        }

        private void Start()
        {
            LocatePlayer();
            LocateRival();
        }

        private void LocatePlayer()
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null)
            {
                var ctrl = p.GetComponent<InsaneGunSwordController>();
                if (ctrl == null || ctrl.controlMode == InsaneGunSwordController.ControlMode.PlayerControlled)
                {
                    _playerTransform = p.transform;
                    _playerHero = ctrl;
                    return;
                }
            }

            var heroes = FindObjectsByType<InsaneGunSwordController>(FindObjectsSortMode.None);
            foreach (var h in heroes)
            {
                if (h.controlMode == InsaneGunSwordController.ControlMode.PlayerControlled)
                {
                    _playerTransform = h.transform;
                    _playerHero = h;
                    return;
                }
            }

            var loco = FindAnyObjectByType<SyntySwordLocomotionController>();
            if (loco != null) _playerTransform = loco.transform;
        }

        private void LocateRival()
        {
            var heroes = FindObjectsByType<InsaneGunSwordController>(FindObjectsSortMode.None);
            foreach (var h in heroes)
            {
                if (h.controlMode == InsaneGunSwordController.ControlMode.AICirclingEnemy || h.gameObject.name.Contains("Rival"))
                {
                    _rivalHero = h;
                    return;
                }
            }
        }

        private void BuildVisuals()
        {
            // 1. Physical Door Frame Archway at South Stairs
            GameObject archPrefab = null;
#if UNITY_EDITOR
            archPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonCyberCity/Prefabs/Buildings/SM_Bld_Door_Double_Large_01.prefab")
                      ?? UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonCyberCity/Prefabs/Buildings/SM_Bld_Door_Double_01.prefab");
#endif
            if (archPrefab != null)
            {
                var frame = Instantiate(archPrefab, transform.position + new Vector3(0f, -0.86f, 0f), Quaternion.identity, transform);
                frame.name = "Arena_Exit_Door_Frame";
                var cList = frame.GetComponentsInChildren<Collider>(true);
                foreach (var col in cList) col.isTrigger = true;

                // Slide door panels open wide so the archway is inviting
                var t1 = frame.transform.Find("SM_Bld_Door_Double_Large_01_Door_01");
                if (t1 != null) t1.localPosition = new Vector3(-2.2f, 0f, 0f);
                var t2 = frame.transform.Find("SM_Bld_Door_Double_Large_01_Door_02");
                if (t2 != null) t2.localPosition = new Vector3(2.2f, 0f, 0f);
            }

            // 2. Ground Holo-Ring
            var ringObj = new GameObject("ExitPortal_HoloRing");
            ringObj.transform.SetParent(transform, false);
            ringObj.transform.localPosition = new Vector3(0f, 0.05f, 0f);

            var line = ringObj.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.startWidth = 0.18f;
            line.endWidth = 0.18f;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.numCornerVertices = 4;
            line.numCapVertices = 4;

            int segs = 36;
            line.positionCount = segs;
            float r = 2.4f;
            for (int i = 0; i < segs; i++)
            {
                float angle = i * Mathf.PI * 2f / segs;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r));
            }

            Shader unlit = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                        ?? Shader.Find("Universal Render Pipeline/Unlit")
                        ?? Shader.Find("Sprites/Default");
            Material mat = new Material(unlit);
            Color cyan = new Color(0f, 0.95f, 1f, 0.95f);
            mat.color = cyan;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", cyan);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", cyan * 2.8f);
            }
            line.sharedMaterial = mat;

            // 3. Dynamic Cyan Point Light
            var lObj = new GameObject("ExitPortal_Light");
            lObj.transform.SetParent(transform, false);
            lObj.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            var l = lObj.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = cyan;
            l.range = 8.5f;
            l.intensity = 2.4f;
            _holoLight = l;

            // 4. 3D Floating TextMesh Billboard
            var textObj = new GameObject("ExitPortal_HoloLabel");
            textObj.transform.SetParent(transform, false);
            textObj.transform.localPosition = new Vector3(0f, 3.4f, 0f);
            var tm = textObj.AddComponent<TextMesh>();
            tm.text = "EXIT THE PIT\n[E] UPPER PLAZA";
            tm.fontSize = 28;
            tm.characterSize = 0.055f;
            tm.alignment = TextAlignment.Center;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = cyan;
            textObj.AddComponent<StationLabelBillboard>();
            _holoTextMesh = tm;
        }

        private void Update()
        {
            if (PitArenaDoorTrigger.IsInTransit) return;

            if (_playerTransform == null)
            {
                LocatePlayer();
                if (_playerTransform == null) return;
            }

            if (_rivalHero == null)
            {
                LocateRival();
            }

            // Visual update based on locked / active state
            if (_holoTextMesh != null)
            {
                if (IsExitLocked)
                {
                    _holoTextMesh.text = "EXIT THE PIT\n[E] CASHOUT / FORFEIT";
                    _holoTextMesh.color = new Color(1.0f, 0.78f, 0.2f, 0.95f);
                    if (_holoLight != null) _holoLight.color = new Color(1.0f, 0.78f, 0.2f);
                }
                else
                {
                    _holoTextMesh.text = "EXIT THE PIT\n[E] UPPER PLAZA // CASH OUT";
                    _holoTextMesh.color = new Color(0f, 0.95f, 1f, 0.95f);
                    if (_holoLight != null) _holoLight.color = new Color(0f, 0.95f, 1f, 0.95f);
                }
            }

            float d = Vector3.Distance(transform.position, _playerTransform.position);
            // In range if within distance radius (7.5m) OR anywhere in the South entrance quadrant of the ring
            bool inSouthZone = (_playerTransform.position.z <= -4.0f && Mathf.Abs(_playerTransform.position.x) <= 8.0f);
            bool rivalDefeated = (_rivalHero != null && _rivalHero.currentHealth <= 0f);

            _isPlayerInRange = (d <= radius) || inSouthZone || rivalDefeated;

            // Check F or E key from both New Input System and Legacy Input System
            bool ePressed = false;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && (kb.eKey.wasPressedThisFrame || kb.fKey.wasPressedThisFrame)) ePressed = true;
            var gp = UnityEngine.InputSystem.Gamepad.current;
            if (gp != null && gp.buttonWest.wasPressedThisFrame) ePressed = true;
            try { if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F)) ePressed = true; } catch { }

            // Auto-trigger if player walks all the way through the exit doorway (z <= -11.0f)
            bool walkThroughExit = (_playerTransform.position.z <= -10.8f && Mathf.Abs(_playerTransform.position.x) <= 3.5f);

            if (_isPlayerInRange && (ePressed || walkThroughExit))
            {
                if (IsExitLocked)
                {
                    Debug.Log("[PitArenaExit] Player opted to forfeit/cash out and exit duel.");
                    CyberpunkHUDController.Instance?.ShowToast("// CASHING OUT // RETURNING TO SKY TERRACE", 2.2f);
                    IsExitLocked = false;
                    if (Cyberpunk.Missions.Mission3_PitFightManager.Instance != null)
                    {
                        Cyberpunk.Missions.Mission3_PitFightManager.Instance.currentState = Cyberpunk.Missions.Mission3_PitFightManager.PitState.CashedOut;
                    }
                }
                StartCoroutine(ExitArenaRoutine());
            }
        }

        public void StartExit()
        {
            if (!PitArenaDoorTrigger.IsInTransit)
            {
                StartCoroutine(ExitArenaRoutine());
            }
        }

        private IEnumerator ExitArenaRoutine()
        {
            PitArenaDoorTrigger.IsInTransit = true;
            PitArenaDoorTrigger.HasVisitedPit = true;
            PitArenaDoorTrigger.IsReturningFromPit = true;
            IsExitLocked = false;
            Cyberpunk.Missions.Mission1_StreetTrialManager.IsMission1Completed = true;

            var hero = _playerTransform.GetComponent<InsaneGunSwordController>();
            if (hero != null) hero.inputLocked = true;
            var loco = _playerTransform.GetComponent<SyntySwordLocomotionController>();
            if (loco != null) loco.inputLocked = true;

            CyberpunkLoadingScreen.Show("SECTOR 04 // SKY TERRACE");
            CyberpunkLoadingScreen.SetStatus("DEPARTING ARENA... RETURNING TO UPPER CITY PLAZA...");

            // Hardcode guaranteed safe arrival coordinates right outside the double doors in Demo_City
            Vector3 safeReturnPos = new Vector3(16.50f, 27.05f, -1.25f);
            float safeReturnYaw = 270f;

            PitArenaDoorTrigger.PendingSpawnPos = safeReturnPos;
            PitArenaDoorTrigger.PendingSpawnYaw = safeReturnYaw;

            float elapsed = 0f;
            float dur = 0.85f;
            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / dur) * 0.5f;
                CyberpunkLoadingScreen.SetProgress(progress);
                yield return null;
            }

            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync("Demo_City");
            if (asyncLoad != null)
            {
                asyncLoad.allowSceneActivation = true;
                while (!asyncLoad.isDone)
                {
                    float p = Mathf.Lerp(0.5f, 0.95f, asyncLoad.progress / 0.9f);
                    CyberpunkLoadingScreen.SetProgress(p);
                    yield return null;
                }
            }
        }

        private void BuildGUIStyles()
        {
            _hudBgTex = new Texture2D(1, 1);
            _hudBgTex.SetPixel(0, 0, new Color(0.015f, 0.04f, 0.07f, 0.92f));
            _hudBgTex.Apply();

            _hudBoxStyle = new GUIStyle
            {
                normal = { background = _hudBgTex },
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(16, 16, 8, 8)
            };

            _hudTitleStyle = new GUIStyle
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0f, 0.95f, 1f, 1f) }
            };

            _hudSubStyle = new GUIStyle
            {
                fontSize = 11,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.82f, 0.2f, 0.95f) }
            };
        }

        private void OnGUI()
        {
            if (PitArenaDoorTrigger.IsInTransit || !_isPlayerInRange) return;
            if (Cyberpunk.Missions.Mission4_VulcanShowdownManager.Instance != null &&
                Cyberpunk.Missions.Mission4_VulcanShowdownManager.Instance.currentPhase != Cyberpunk.Missions.Mission4_VulcanShowdownManager.ShowdownPhase.Inactive) return;

            if (IsExitLocked)
            {
                Cyberpunk.UI.CyberpunkInteractionPromptHUD.DrawPromptImmediate("E", "Exit Arena (Forfeit / Cash Out)");
                return;
            }

            Cyberpunk.UI.CyberpunkInteractionPromptHUD.DrawPromptImmediate("E", "Exit Pit Arena");
        }

        private void DrawBorder(Rect r, Color color, float width)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, width), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - width, r.width, width), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, width, r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - width, r.y, width, r.height), Texture2D.whiteTexture);
            GUI.color = old;
        }

        private void OnDestroy()
        {
            if (_hudBgTex != null) Destroy(_hudBgTex);
        }
    }
}
