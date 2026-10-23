using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Alita;
using InsaneGun.Combat;
using SyntyCombined;

namespace Cyberpunk.World
{
    /// <summary>
    /// Interactive Doorway Trigger for Dr. Dyson Ido's Cyber Clinic.
    /// Connects the clinic storefront in Demo_City with the furnished medical lab in Demo_Interior.
    /// 
    /// Features:
    /// - Interactive [E] prompt HUD with cyan cyberpunk styling
    /// - Automatic pneumatic sliding door panels on proximity
    /// - Seamless scene transition with CyberpunkLoadingScreen
    /// - CharacterController-safe teleportation & camera synchronization
    /// - 100% in-memory runtime binding (zero scene file modifications)
    /// </summary>
    [SelectionBase]
    public class ClinicDoorTrigger : MonoBehaviour
    {
        public static ClinicDoorTrigger Instance { get; private set; }

        public const string CitySceneName = "Demo_City";
        public const string InteriorSceneName = "Demo_Interior";

        [Header("Transit Target Settings")]
        public string targetSceneName = InteriorSceneName;
        public Vector3 targetSpawnPosition = new Vector3(-24.25f, 0.08f, 2.70f);
        public float targetSpawnYaw = 0f;
        public bool isExitDoor = false;

        [Header("Interaction Settings")]
        public float interactionRadius = 3.2f;
        public KeyCode interactKey = KeyCode.E;
        public string promptAction = "[E] ENTER DR. IDO'S CLINIC";
        public string promptSubtitle = "CYBER-REPAIR & RECONSTRUCTION LAB";

        [Header("Door Panel Animation (Optional)")]
        public Transform leftDoorPanel;
        public Transform rightDoorPanel;
        public float slideDistance = 1.25f;
        public float slideSpeed = 4.0f;

        [Header("Audio")]
        public AudioSource audioSource;
        public AudioClip doorOpenSound;
        public AudioClip doorCloseSound;

        // Static persistence across scene loads
        public static Vector3? PendingSpawnPos = null;
        public static float PendingSpawnYaw = 0f;
        public static bool IsInTransit = false;

        // Internal State
        private Transform _playerTransform;
        private bool _isPlayerInRange = false;
        private float _currentSlideProgress = 0f;
        private Vector3 _leftClosedLocal;
        private Vector3 _rightClosedLocal;
        private Vector3 _leftOpenLocal;
        private Vector3 _rightOpenLocal;

        // HUD Styling
        private GUIStyle _hudBoxStyle;
        private GUIStyle _hudTitleStyle;
        private GUIStyle _hudSubStyle;
        private Texture2D _hudBgTex;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitSceneLoadedHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            DeduplicatePlayers(scene);

            string sName = scene.name;
            if (sName == CitySceneName)
            {
                // Fresh boot check: if player just pressed Play in Demo_City without transit
                if (!IsInTransit && !PendingSpawnPos.HasValue &&
                    !PitArenaDoorTrigger.IsReturningFromPit &&
                    !PitArenaDoorTrigger.HasVisitedPit &&
                    !PitArenaDoorTrigger.PendingSpawnPos.HasValue)
                {
                    Debug.Log("[ClinicDoorTrigger] Fresh boot in Demo_City -> Booting into Dr. Ido's Clinic Lab (Demo_Interior) for awakening sequence.");
                    PlayerPrefs.DeleteKey("Prologue_Beat0_Completed");
                    PlayerPrefs.Save();
                    SceneManager.LoadScene(InteriorSceneName);
                    return;
                }

                SetupCityClinicDoor();
            }
            else if (sName == InteriorSceneName)
            {
                SetupInteriorClinicDoor();
            }
        }

        private static void DeduplicatePlayers(Scene scene)
        {
            var players = GameObject.FindGameObjectsWithTag("Player");
            if (players.Length > 1)
            {
                Debug.LogWarning($"[ClinicDoorTrigger] Found {players.Length} Player objects on loading '{scene.name}'. Deduplicating...");
                GameObject keeper = null;
                for (int i = 0; i < players.Length; i++)
                {
                    if (players[i] != null && players[i].scene == scene && keeper == null)
                    {
                        keeper = players[i];
                    }
                    else if (players[i] != null)
                    {
                        Destroy(players[i]);
                    }
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnSceneLoadedRuntime()
        {
            Scene scene = SceneManager.GetActiveScene();
            string sName = scene.name;

            if (sName == CitySceneName)
            {
                if (!IsInTransit && !PendingSpawnPos.HasValue &&
                    !PitArenaDoorTrigger.IsReturningFromPit &&
                    !PitArenaDoorTrigger.HasVisitedPit &&
                    !PitArenaDoorTrigger.PendingSpawnPos.HasValue)
                {
                    Debug.Log("[ClinicDoorTrigger] Fresh boot in Demo_City (AfterSceneLoad) -> Booting into Dr. Ido's Clinic Lab (Demo_Interior).");
                    PlayerPrefs.DeleteKey("Prologue_Beat0_Completed");
                    PlayerPrefs.Save();
                    SceneManager.LoadScene(InteriorSceneName);
                    return;
                }

                SetupCityClinicDoor();
            }
            else if (sName == InteriorSceneName)
            {
                SetupInteriorClinicDoor();
            }
        }

        private static void SetupCityClinicDoor()
        {
            // Exact target door selected by user: SM_Bld_Door_Single_Large_01 (2) at (19.50, -18.00, 47.50)
            Vector3 exteriorDoorPos = new Vector3(19.50f, -18.00f, 47.50f);

            var existing = FindAnyObjectByType<ClinicDoorTrigger>();
            if (existing != null && !existing.isExitDoor) return;

            // Locate the exact scene door
            GameObject doorObj = GameObject.Find("SM_Bld_Door_Single_Large_01 (2)");
            if (doorObj == null)
            {
                var allDoors = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
                foreach (var t in allDoors)
                {
                    if (t.name.Contains("SM_Bld_Door_Single_Large_01") && Vector3.Distance(t.position, exteriorDoorPos) < 2.5f)
                    {
                        doorObj = t.gameObject;
                        break;
                    }
                }
            }

            if (doorObj == null)
            {
                doorObj = new GameObject("Dr_Ido_Clinic_Door_Trigger");
                doorObj.transform.position = exteriorDoorPos;
                doorObj.transform.rotation = Quaternion.identity;
            }

            var trigger = doorObj.GetComponent<ClinicDoorTrigger>() ?? doorObj.AddComponent<ClinicDoorTrigger>();
            trigger.targetSceneName = InteriorSceneName;
            trigger.targetSpawnPosition = new Vector3(-20.50f, 0.08f, 3.50f);
            trigger.targetSpawnYaw = 270f;
            trigger.isExitDoor = false;
            trigger.interactionRadius = 3.5f;
            trigger.slideDistance = 1.25f;
            trigger.promptAction = "[E] ENTER DR. IDO'S CLINIC";
            trigger.promptSubtitle = "CYBER-SURGERY & RECONSTRUCTION LAB";

            // Wire up the sliding door panels
            Transform d1 = doorObj.transform.Find("SM_Bld_Door_Single_Large_01_Door_01");
            Transform d2 = doorObj.transform.Find("SM_Bld_Door_Single_Large_01_Door_02");
            trigger.leftDoorPanel = d1;
            trigger.rightDoorPanel = d2;

            // Cyan access indicator light above the door
            var lightGo = doorObj.transform.Find("Clinic_AccessLight")?.gameObject;
            if (lightGo == null)
            {
                lightGo = new GameObject("Clinic_AccessLight");
                lightGo.transform.SetParent(doorObj.transform, false);
                lightGo.transform.localPosition = new Vector3(0f, 2.85f, 0.35f);
                var l = lightGo.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 4.0f;
                l.intensity = 2.5f;
                l.color = new Color(0.00f, 0.95f, 1.0f);
            }

            // Holographic sign above the door
            var signGo = doorObj.transform.Find("Clinic_HoloSign")?.gameObject;
            if (signGo == null)
            {
                signGo = new GameObject("Clinic_HoloSign");
                signGo.transform.SetParent(doorObj.transform, false);
                signGo.transform.localPosition = new Vector3(0f, 2.70f, 0.20f);
                signGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

                var tm = signGo.AddComponent<TextMesh>();
                tm.text = "✚ DR. IDO'S CYBER CLINIC ✚\nCYBER-SURGERY & AUGMENTATION";
                tm.characterSize = 0.08f;
                tm.fontSize = 32;
                tm.alignment = TextAlignment.Center;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.color = new Color(0.00f, 0.95f, 1.0f, 0.95f);
            }

            Debug.Log("[ClinicDoorTrigger] Dr. Ido's Clinic door trigger bound to " + doorObj.name + " at " + doorObj.transform.position);
        }

        private static void SetupInteriorClinicDoor()
        {
            // Ensure fallback player if opened directly in Demo_Interior
            EnsurePlayerInInterior();

            // Interior exit door inside TearingDoc room (SM_Bld_Door_Single_01 (24) at the front entrance)
            Vector3 interiorExitPos = new Vector3(-19.25f, -0.13f, 4.37f);

            var existing = FindAnyObjectByType<ClinicDoorTrigger>();
            if (existing != null && existing.isExitDoor) return;

            var exitObj = new GameObject("Dr_Ido_Clinic_Exit_Trigger");
            exitObj.transform.position = interiorExitPos;
            exitObj.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            var col = exitObj.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(2.5f, 2.5f, 2.5f);
            col.center = new Vector3(0f, 1.25f, 0f);

            var trigger = exitObj.AddComponent<ClinicDoorTrigger>();
            trigger.targetSceneName = CitySceneName;
            // Spawn right outside the target door in Demo_City facing outward toward the plaza!
            trigger.targetSpawnPosition = new Vector3(19.50f, -17.95f, 49.50f);
            trigger.targetSpawnYaw = 0f;
            trigger.isExitDoor = true;
            trigger.interactionRadius = 4.0f;
            trigger.promptAction = "[E] EXIT TO SECTOR 4 PLAZA";
            trigger.promptSubtitle = "STEP OUTSIDE INTO IRON CITY";

            // Locate existing interior door mesh and ensure child door collider is a trigger
            var nearbyDoors = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
            foreach (var t in nearbyDoors)
            {
                if (t.name.Contains("Door") && Vector3.Distance(t.position, interiorExitPos) < 2.5f)
                {
                    trigger.leftDoorPanel = t.Find("SM_Bld_Door_Single_01_Door_01") ?? t;
                    var doorCol = trigger.leftDoorPanel.GetComponent<Collider>();
                    if (doorCol != null) doorCol.isTrigger = true;
                    break;
                }
            }

            Debug.Log("[ClinicDoorTrigger] Dr. Ido's Clinic interior exit trigger initialized at " + interiorExitPos);

            if (Alita.AlitaCityPrologueDirector.Instance == null && Object.FindAnyObjectByType<Alita.AlitaCityPrologueDirector>() == null)
            {
                GameObject dirGO = new GameObject("Alita_City_Prologue_Runtime");
                dirGO.AddComponent<Alita.AlitaCityPrologueDirector>();
                Debug.Log("[ClinicDoorTrigger] Auto-spawned AlitaCityPrologueDirector in Demo_Interior for Clinic Lab Awakening.");
            }

            // Ensure Tron chamber floor and walls have solid colliders so Alita cannot fall through
            Cyberpunk.World.TronDiscRotator.EnsureChamberColliders();

            // Thicken interior clinic ceiling blocker so camera and jump never pop above the roof
            var blocker = GameObject.Find("Ceiling_Omni_Overhang_Blocker");
            if (blocker != null)
            {
                var box = blocker.GetComponent<BoxCollider>() ?? blocker.AddComponent<BoxCollider>();
                box.size = new Vector3(1f, 2.0f, 1f);
                box.center = new Vector3(0f, 1.0f, 0f);
            }
        }

        private static void EnsurePlayerInInterior()
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                var cc = Object.FindAnyObjectByType<CharacterController>();
                if (cc != null) player = cc.gameObject;
            }

            Vector3 labSpawnPos = new Vector3(-24.25f, 0.08f, 2.70f);
            Quaternion labSpawnRot = Quaternion.Euler(0f, 0f, 0f);

            if (player == null)
            {
                GameObject prefab = Resources.Load<GameObject>("Prefabs/Alita_BattleAngel_Hero");
#if UNITY_EDITOR
                if (prefab == null)
                {
                    prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Alita_BattleAngel_Hero.prefab")
                          ?? UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/AnimationBaseLocomotion/Samples/Prefabs/PF_PolygonPlayer.prefab");
                }
#endif
                if (prefab != null)
                {
                    player = Instantiate(prefab, labSpawnPos, labSpawnRot);
                    player.name = "Alita_BattleAngel_Hero";
                    player.tag = "Player";
                    Debug.Log("[ClinicDoorTrigger] Spawned Alita beside surgical bed inside Dr. Ido's Clinic Lab.");
                }
            }
            else
            {
                CharacterController cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                player.transform.position = labSpawnPos;
                player.transform.rotation = labSpawnRot;
                if (cc != null) cc.enabled = true;
            }
        }

        private void Awake()
        {
            if (Instance == null && !isExitDoor) Instance = this;
            InitializeDoorPanels();
            EnsureAudioSource();
            BuildGUIStyles();
        }

        private void Start()
        {
            LocatePlayer();
        }

        private void InitializeDoorPanels()
        {
            bool isSingleDoor = (leftDoorPanel != null && leftDoorPanel.name.Contains("Door_Single")) ||
                                (rightDoorPanel != null && rightDoorPanel.name.Contains("Door_Single"));

            if (leftDoorPanel != null)
            {
                _leftClosedLocal = leftDoorPanel.localPosition;
                _leftOpenLocal = _leftClosedLocal + (isSingleDoor ? Vector3.right : Vector3.left) * slideDistance;
            }

            if (rightDoorPanel != null)
            {
                _rightClosedLocal = rightDoorPanel.localPosition;
                _rightOpenLocal = _rightClosedLocal + Vector3.right * slideDistance;
            }
        }

        private void EnsureAudioSource()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                    audioSource.spatialBlend = 0.85f;
                    audioSource.minDistance = 2.0f;
                    audioSource.maxDistance = 20.0f;
                    audioSource.playOnAwake = false;
                }
            }
        }

        private void LocatePlayer()
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null)
            {
                _playerTransform = p.transform;
                return;
            }

            var insane = Object.FindAnyObjectByType<InsaneGunSwordController>();
            if (insane != null)
            {
                _playerTransform = insane.transform;
                return;
            }

            var synty = Object.FindAnyObjectByType<SyntySwordLocomotionController>();
            if (synty != null)
            {
                _playerTransform = synty.transform;
            }
        }

        private void Update()
        {
            if (_playerTransform == null)
            {
                LocatePlayer();
                return;
            }

            float dist = Vector3.Distance(transform.position, _playerTransform.position);
            _isPlayerInRange = dist <= interactionRadius;

            // Slide door open smoothly when near
            float targetProgress = _isPlayerInRange ? 1f : 0f;
            _currentSlideProgress = Mathf.MoveTowards(_currentSlideProgress, targetProgress, slideSpeed * Time.deltaTime);

            UpdatePanelPositions();

            // Check interaction: ONLY when user explicitly presses [E] key while in range
            bool keyTriggered = false;
            try
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null && kb.eKey.wasPressedThisFrame) keyTriggered = true;
            }
            catch { }
            if (!keyTriggered)
            {
                try { if (Input.GetKeyDown(interactKey)) keyTriggered = true; } catch { }
            }

            if (_isPlayerInRange && !IsInTransit && keyTriggered)
            {
                StartClinicTransit();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // Do NOT auto-trigger scene transit on trigger enter.
            // Player must explicitly press [E] while in range.
        }

        private void UpdatePanelPositions()
        {
            float t = Mathf.SmoothStep(0f, 1f, _currentSlideProgress);

            if (leftDoorPanel != null)
            {
                leftDoorPanel.localPosition = Vector3.Lerp(_leftClosedLocal, _leftOpenLocal, t);
            }

            if (rightDoorPanel != null)
            {
                rightDoorPanel.localPosition = Vector3.Lerp(_rightClosedLocal, _rightOpenLocal, t);
            }
        }

        private void StartClinicTransit()
        {
            if (isExitDoor)
            {
                PlayerPrefs.SetInt("Prologue_Beat0_Completed", 1);
                PlayerPrefs.Save();
            }

            IsInTransit = true;
            PendingSpawnPos = targetSpawnPosition;
            PendingSpawnYaw = targetSpawnYaw;

            var runnerObj = new GameObject("[ClinicDoor_TransitionRunner]");
            DontDestroyOnLoad(runnerObj);
            var runner = runnerObj.AddComponent<ClinicTransitionRunner>();
            runner.Execute(
                targetSceneName,
                targetSpawnPosition,
                targetSpawnYaw,
                isExitDoor ? "SECTOR 04 — IRON CITY PLAZA" : "DR. IDO'S CYBER CLINIC",
                isExitDoor ? "RETURNING TO SECTOR 4 PLAZA..." : "ACCESSING CYBER CLINIC LAB...",
                _playerTransform
            );
        }

        private void BuildGUIStyles()
        {
            _hudBgTex = new Texture2D(1, 1);
            _hudBgTex.SetPixel(0, 0, new Color(0.01f, 0.03f, 0.06f, 0.90f));
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
                normal = { textColor = new Color(0.00f, 0.95f, 1.0f) } // Cyber Cyan
            };

            _hudSubStyle = new GUIStyle
            {
                fontSize = 11,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1.0f, 0.85f, 0.2f, 0.9f) } // Yellow telemetry
            };
        }

        private void OnGUI()
        {
            if (IsInTransit) return;
            if (!_isPlayerInRange) return;

            string action = isExitDoor ? "Exit to Sector 4 Plaza" : "Enter Clinic Lab";
            Cyberpunk.UI.CyberpunkInteractionPromptHUD.DrawPromptImmediate("E", action);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.00f, 0.95f, 1.0f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, interactionRadius);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_hudBgTex != null) Destroy(_hudBgTex);
        }
    }

    /// <summary>
    /// Persistent transition runner that survives scene load and handles CharacterController warping.
    /// </summary>
    public class ClinicTransitionRunner : MonoBehaviour
    {
        public void Execute(
            string targetScene,
            Vector3 targetSpawnPos,
            float targetSpawnYaw,
            string title,
            string status,
            Transform playerT)
        {
            StartCoroutine(Routine(targetScene, targetSpawnPos, targetSpawnYaw, title, status, playerT));
        }

        private IEnumerator Routine(
            string targetScene,
            Vector3 targetSpawnPos,
            float targetSpawnYaw,
            string title,
            string status,
            Transform playerT)
        {
            // 1. Lock player movement
            SetPlayerInputLocked(playerT, true);

            // 2. Set static pending spawn for scene arrival
            ClinicDoorTrigger.PendingSpawnPos = targetSpawnPos;
            ClinicDoorTrigger.PendingSpawnYaw = targetSpawnYaw;

            // 3. Show Cyberpunk Loading Screen
            CyberpunkLoadingScreen.Show(title);
            CyberpunkLoadingScreen.SetStatus(status);

            float elapsed = 0f;
            float duration = 0.40f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration) * 0.45f;
                CyberpunkLoadingScreen.SetProgress(progress);
                yield return null;
            }

            // 4. Load Target Scene Asynchronously
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(targetScene);
            if (asyncLoad != null)
            {
                asyncLoad.allowSceneActivation = true;
                while (!asyncLoad.isDone)
                {
                    float p = Mathf.Lerp(0.45f, 0.90f, asyncLoad.progress / 0.9f);
                    CyberpunkLoadingScreen.SetProgress(p);
                    yield return null;
                }
            }

            // 5. Let newly loaded scene settle
            yield return null;
            yield return new WaitForEndOfFrame();

            // 6. Disable Showroom Static Camera in Demo_Interior
            if (targetScene == "Demo_Interior")
            {
                var cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
                foreach (var cam in cameras)
                {
                    if (cam.gameObject.name == "Camera" && cam.transform.parent == null)
                    {
                        cam.gameObject.SetActive(false);
                    }
                }
            }

            // 7. Find Player in loaded scene and Warp
            playerT = null;
            var players = GameObject.FindGameObjectsWithTag("Player");
            if (players.Length > 0)
            {
                var activeSc = SceneManager.GetActiveScene();
                GameObject keeper = null;
                foreach (var pl in players)
                {
                    if (pl.scene == activeSc && keeper == null)
                    {
                        keeper = pl;
                    }
                    else if (keeper != null)
                    {
                        Destroy(pl);
                    }
                }
                if (keeper == null) keeper = players[0];
                playerT = keeper.transform;
            }

            if (playerT != null)
            {
                var cc = playerT.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;

                playerT.position = targetSpawnPos;
                playerT.rotation = Quaternion.Euler(0f, targetSpawnYaw, 0f);
                Physics.SyncTransforms();

                if (cc != null) cc.enabled = true;

                // Sync camera
                if (SmoothOrbitCamera.Instance != null)
                {
                    SmoothOrbitCamera.Instance.tightPositionFollow = true;
                    Vector3 camOffset = Quaternion.Euler(15f, targetSpawnYaw, 0f) * new Vector3(0f, 0.40f, -4.8f);
                    SmoothOrbitCamera.Instance.transform.position = targetSpawnPos + new Vector3(0f, 1.45f, 0f) + camOffset;
                    SmoothOrbitCamera.Instance.transform.rotation = Quaternion.Euler(15f, targetSpawnYaw, 0f);
                    SmoothOrbitCamera.Instance.SnapToTarget();
                }
                else if (Camera.main != null)
                {
                    Vector3 camOffset = Quaternion.Euler(15f, targetSpawnYaw, 0f) * new Vector3(0f, 0.40f, -4.8f);
                    Camera.main.transform.position = targetSpawnPos + new Vector3(0f, 1.45f, 0f) + camOffset;
                    Camera.main.transform.rotation = Quaternion.Euler(15f, targetSpawnYaw, 0f);
                }

                SetPlayerInputLocked(playerT, false);
            }

            // 8. Complete Loading Screen
            CyberpunkLoadingScreen.SetProgress(1.0f);
            yield return new WaitForSecondsRealtime(0.20f);
            CyberpunkLoadingScreen.Hide();

            ClinicDoorTrigger.PendingSpawnPos = null;
            ClinicDoorTrigger.IsInTransit = false;

            Destroy(gameObject);
        }

        private void SetPlayerInputLocked(Transform playerT, bool locked)
        {
            if (playerT == null) return;

            var insane = playerT.GetComponent<InsaneGunSwordController>();
            if (insane != null) insane.inputLocked = locked;

            var synty = playerT.GetComponent<SyntySwordLocomotionController>();
            if (synty != null) synty.inputLocked = locked;
        }
    }
}
