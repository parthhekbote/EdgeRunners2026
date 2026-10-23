using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Alita;
using InsaneGun.Combat;
using SyntyCombined;

namespace Cyberpunk.World
{
    /// <summary>
    /// AAA Interactive Door Transition between Demo_City and Scene_Blackwall_Cyberspace (Server Room).
    /// Features:
    /// - Interactive proximity detection with Cyberpunk-styled [E] prompt HUD
    /// - Smooth pneumatic double-sliding door panel animation
    /// - Procedural sci-fi hydraulic airlock sound effects
    /// - Full integration with CyberpunkLoadingScreen for seamless scene transitions
    /// - CharacterController-safe repositioning, yaw orientation, and camera sync
    /// </summary>
    public class ServerRoomDoorTrigger : MonoBehaviour
    {
        [Header("Scene Transition Settings")]
        [Tooltip("The name of the scene to load")]
        public string targetSceneName = "Scene_Blackwall_Cyberspace";

        [Tooltip("Where the player will appear in the target scene")]
        public Vector3 targetSpawnPosition = new Vector3(0f, 0.08f, -92.0f);

        [Tooltip("Yaw rotation (in degrees) for the player when appearing in the target scene")]
        public float targetSpawnYaw = 0f;

        [Header("Door Panels Animation")]
        public Transform leftDoorPanel;
        public Transform rightDoorPanel;
        public float slideDistance = 1.85f;
        public float slideSpeed = 3.5f;

        [Header("Interaction Settings")]
        public float interactionRadius = 3.5f;
        public KeyCode interactKey = KeyCode.E;

        [Header("HUD Display")]
        public string promptAction = "[E] ENTER SERVER ROOM";
        public string promptSubtitle = "BLACKWALL DATACENTER // CLASSIFIED ARCHIVE";
        public string loadingTitle = "BLACKWALL DATACENTER // SERVER ARCHIVE 09";
        public string loadingStatus = "BYPASSING SUB-NET FIREWALL... LINKING TO SERVER ROOM...";

        [Header("Audio")]
        public AudioSource audioSource;
        public AudioClip doorOpenSound;
        public AudioClip doorCloseSound;

        // Static transit persistence across scene loads
        public static Vector3? PendingSpawnPos = null;
        public static float PendingSpawnYaw = 0f;
        public static bool IsInTransition = false;

        private Transform _playerTransform;
        private bool _isPlayerInRange = false;
        private bool _isOpen = false;
        private Vector3 _leftClosedLocalPos;
        private Vector3 _rightClosedLocalPos;
        private bool _initializedPanels = false;

        // HUD Styling
        private GUIStyle _hudBoxStyle;
        private GUIStyle _hudTitleStyle;
        private GUIStyle _hudSubStyle;
        private Texture2D _hudBgTex;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnRuntimeMethodLoad()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (PendingSpawnPos.HasValue)
            {
                Vector3 spawnPos = PendingSpawnPos.Value;
                float spawnYaw = PendingSpawnYaw;
                PendingSpawnPos = null;

                // Create a runner to complete the warp and reveal after the scene settles
                var runnerObj = new GameObject("[ServerRoomTransition_WarpRunner]");
                DontDestroyOnLoad(runnerObj);
                var runner = runnerObj.AddComponent<TransitionWarpRunner>();
                runner.ExecuteWarp(spawnPos, spawnYaw, scene.name);
            }
        }

        private void Awake()
        {
            if (SceneManager.GetActiveScene().name == "Demo_City")
            {
                InitDoorPanels();
                if (leftDoorPanel != null) leftDoorPanel.localPosition = _leftClosedLocalPos;
                if (rightDoorPanel != null) rightDoorPanel.localPosition = _rightClosedLocalPos;
                enabled = false;
                Destroy(this);
                return;
            }

            InitDoorPanels();
            BuildGUIStyles();

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            }

            audioSource.spatialBlend = 0.85f;
            audioSource.playOnAwake = false;
            audioSource.minDistance = 2.0f;
            audioSource.maxDistance = 25.0f;

            if (doorOpenSound == null) doorOpenSound = CreateProceduralAirlockSound(true);
            if (doorCloseSound == null) doorCloseSound = CreateProceduralAirlockSound(false);
        }

        private void Start()
        {
            LocatePlayer();
        }

        private void InitDoorPanels()
        {
            if (_initializedPanels) return;

            // Auto-locate panels if not explicitly linked
            if (leftDoorPanel == null)
            {
                var t1 = transform.Find("SM_Bld_Door_Double_Large_01_Door_01");
                if (t1 == null) t1 = transform.Find("SM_Bld_Door_Double_04_Door_01");
                leftDoorPanel = t1;
            }

            if (rightDoorPanel == null)
            {
                var t2 = transform.Find("SM_Bld_Door_Double_Large_01_Door_02");
                if (t2 == null) t2 = transform.Find("SM_Bld_Door_Double_04_Door_02");
                rightDoorPanel = t2;
            }

            if (leftDoorPanel != null)
            {
                if (Mathf.Abs(leftDoorPanel.localPosition.x) < 0.5f) _leftClosedLocalPos = new Vector3(-1.12f, 0f, 0f);
                else _leftClosedLocalPos = new Vector3(Mathf.Sign(leftDoorPanel.localPosition.x) * Mathf.Min(Mathf.Abs(leftDoorPanel.localPosition.x), 1.12f), 0f, 0f);
                leftDoorPanel.localPosition = _leftClosedLocalPos;
            }

            if (rightDoorPanel != null)
            {
                if (Mathf.Abs(rightDoorPanel.localPosition.x) < 0.5f) _rightClosedLocalPos = new Vector3(1.12f, 0f, 0f);
                else _rightClosedLocalPos = new Vector3(Mathf.Sign(rightDoorPanel.localPosition.x) * Mathf.Min(Mathf.Abs(rightDoorPanel.localPosition.x), 1.12f), 0f, 0f);
                rightDoorPanel.localPosition = _rightClosedLocalPos;
            }

            _initializedPanels = true;
        }

        private void LocatePlayer()
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null)
            {
                _playerTransform = p.transform;
                return;
            }

            var insaneHero = Object.FindAnyObjectByType<InsaneGunSwordController>();
            if (insaneHero != null)
            {
                _playerTransform = insaneHero.transform;
                return;
            }

            var swordLoco = Object.FindAnyObjectByType<SyntySwordLocomotionController>();
            if (swordLoco != null)
            {
                _playerTransform = swordLoco.transform;
            }
        }

        private void Update()
        {
            if (IsInTransition) return;

            if (_playerTransform == null)
            {
                LocatePlayer();
                if (_playerTransform == null) return;
            }

            float dist = Vector3.Distance(transform.position, _playerTransform.position);
            _isPlayerInRange = dist <= interactionRadius;

            // Door does not open - stays closed as requested
            // Interactive input: just press E to enter
            if (_isPlayerInRange && (Input.GetKeyDown(interactKey) || Input.GetKeyDown(KeyCode.F)))
            {
                TriggerTransition();
            }
        }

        public void TriggerTransition()
        {
            if (IsInTransition) return;
            StartCoroutine(TransitionRoutine());
        }

        private IEnumerator TransitionRoutine()
        {
            IsInTransition = true;

            // Lock controls
            SetPlayerInputLocked(true);

            // Play open sound
            if (audioSource != null && doorOpenSound != null)
            {
                audioSource.PlayOneShot(doorOpenSound, 0.9f);
            }

            // Show Cyberpunk Loading Screen
            CyberpunkLoadingScreen.Show(loadingTitle);
            CyberpunkLoadingScreen.SetStatus(loadingStatus);

            // Store pending destination for the static arrival handler
            PendingSpawnPos = targetSpawnPosition;
            PendingSpawnYaw = targetSpawnYaw;

            // Animate progress smoothly
            float elapsed = 0f;
            float duration = 0.85f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration) * 0.5f;
                CyberpunkLoadingScreen.SetProgress(progress);
                yield return null;
            }

            // Begin async load of target scene
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

        #region HUD Prompt Rendering
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
                normal = { textColor = new Color(0.05f, 0.95f, 1f, 1f) }
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
            if (IsInTransition) return;
            if (!_isPlayerInRange) return;

            Cyberpunk.UI.CyberpunkInteractionPromptHUD.DrawPromptImmediate("E", "Enter Server Room");
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
        #endregion

        #region Procedural Audio
        private static AudioClip CreateProceduralAirlockSound(bool isOpen)
        {
            int sampleRate = 44100;
            float duration = 1.1f;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Clamp01(1f - (t / duration));

                // Pneumatic hiss
                float noise = (Random.value * 2f - 1f) * Mathf.Exp(-t * 4.0f) * 0.40f;

                // Motor / servo
                float startFreq = isOpen ? 120f : 180f;
                float endFreq = isOpen ? 180f : 90f;
                float freq = Mathf.Lerp(startFreq, endFreq, t / duration);
                float servo = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.28f * env;

                // Heavy latch thud
                float thud = Mathf.Sin(2f * Mathf.PI * 65f * t) * Mathf.Exp(-t * 8f) * 0.55f;

                samples[i] = Mathf.Clamp(noise + servo + thud, -1f, 1f);
            }

            string clipName = isOpen ? "Procedural_Airlock_Open" : "Procedural_Airlock_Close";
            var clip = AudioClip.Create(clipName, totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
        #endregion
    }

    /// <summary>
    /// Helper runner instantiated across scene loads to safely position the player,
    /// re-enable character controller, sync camera, and fade out loading screen.
    /// </summary>
    public class TransitionWarpRunner : MonoBehaviour
    {
        public void ExecuteWarp(Vector3 spawnPos, float spawnYaw, string sceneName)
        {
            StartCoroutine(WarpRoutine(spawnPos, spawnYaw, sceneName));
        }

        private IEnumerator WarpRoutine(Vector3 spawnPos, float spawnYaw, string sceneName)
        {
            // Settle a frame for scene initialization
            yield return null;

            // Locate Player in newly loaded scene
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
                // Disable CharacterController to ensure physics safe teleport
                var cc = playerT.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;

                playerT.position = spawnPos;
                playerT.rotation = Quaternion.Euler(0f, spawnYaw, 0f);

                Physics.SyncTransforms();

                if (cc != null) cc.enabled = true;

                // If loaded into Server Room (Scene_Blackwall_Cyberspace), make sure Server_Room_Interior is active
                if (sceneName == "Scene_Blackwall_Cyberspace")
                {
                    var interior = GameObject.Find("Server_Room_Interior");
                    if (interior != null && !interior.activeSelf)
                    {
                        interior.SetActive(true);
                    }
                }

                // Snap camera
                if (SmoothOrbitCamera.Instance != null)
                {
                    SmoothOrbitCamera.Instance.tightPositionFollow = true;
                    Vector3 camOffset = Quaternion.Euler(12f, spawnYaw, 0f) * new Vector3(0f, 0.40f, -4.8f);
                    SmoothOrbitCamera.Instance.transform.position = spawnPos + new Vector3(0f, 1.45f, 0f) + camOffset;
                    SmoothOrbitCamera.Instance.transform.rotation = Quaternion.Euler(12f, spawnYaw, 0f);
                }
                else if (Camera.main != null)
                {
                    Vector3 camOffset = Quaternion.Euler(12f, spawnYaw, 0f) * new Vector3(0f, 0.40f, -4.8f);
                    Camera.main.transform.position = spawnPos + new Vector3(0f, 1.45f, 0f) + camOffset;
                    Camera.main.transform.rotation = Quaternion.Euler(12f, spawnYaw, 0f);
                }

                // Unlock player input
                var insaneHero = playerT.GetComponent<InsaneGunSwordController>();
                if (insaneHero != null) insaneHero.inputLocked = false;

                var swordLoco = playerT.GetComponent<SyntySwordLocomotionController>();
                if (swordLoco != null) swordLoco.inputLocked = false;
            }

            // Set loading bar to 100%
            CyberpunkLoadingScreen.SetProgress(1.0f);
            yield return new WaitForSecondsRealtime(0.25f);

            // Fade out loading screen
            CyberpunkLoadingScreen.Hide();

            ServerRoomDoorTrigger.IsInTransition = false;

            // Self-destroy runner
            Destroy(gameObject);
        }
    }
}
