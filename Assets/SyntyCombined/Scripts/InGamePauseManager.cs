using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using TMPro;
using Alita;
using InsaneGun.Combat;

namespace SyntyCombined
{
    /// <summary>
    /// AAA Cyberpunk Pause Menu & System State Controller.
    /// Handles unscaled pause timeline, bullet-time preservation, edge-case denial (cutscenes/loading),
    /// multi-platform dual input (Input System & Legacy), sub-panel navigation, and modal confirmation dialogs.
    /// </summary>
    public class InGamePauseManager : MonoBehaviour
    {
        public static InGamePauseManager Instance { get; private set; }

        public static bool IsPaused { get; private set; }
        public bool isPaused => IsPaused;
        public bool IsGamePaused => IsPaused;
        public static event Action<bool> OnPauseChanged;

        [Header("Pause Canvas & Panels")]
        public GameObject pauseCanvasOverlay;
        public GameObject pauseMainPanel;
        public GameObject settingsPanel;
        public GameObject controlsPanel;
        public CanvasGroup canvasGroup;
        public RectTransform mainColumnRect;

        [Header("Left Navigation Buttons")]
        public Button resumeButton;
        public Button settingsButton;
        public Button controlsButton;
        public Button photoModeButton;
        public Button restartButton;
        public Button mainMenuButton;
        public Button quitDesktopButton;

        [Header("Back Buttons")]
        public Button settingsBackButton;
        public Button controlsBackButton;

        [Header("Confirmation Modal UI")]
        public GameObject confirmPopupRoot;
        public TMP_Text confirmTitleText;
        public TMP_Text confirmDescText;
        public Button confirmYesButton;
        public Button confirmNoButton;

        [Header("Header Status Readouts")]
        public TMP_Text headerTitleText;
        public TMP_Text headerStatusText;
        public TMP_Text footerHintText;

        [Header("Audio Feedback")]
        public AudioSource uiAudioSource;

        private float _lastToggleRealtime = -1f;
        private const float DEBOUNCE_INTERVAL = 0.25f;
        private float _lastCutsceneSweepTime = -10f;

        private float _prevTimeScale = 1f;
        private float _prevFixedDelta = 0.02f;
        private bool _wasBulletTimeActive = false;
        private bool _playerWasEnabled = true;

        private Action _pendingConfirmAction;
        private Action _pendingCancelAction;

        public Action<int> OnCycleTab;
        public Action<int> OnSelectTab;
        public Action OnResetSettings;

        /// <summary>
        /// Gameplay-return hub: the NEW Discovery cutscene that starts the canonical
        /// Discovery -&gt; Tron Awakening -&gt; Demo_City flow. Never Scene_MainMenu.
        /// </summary>
        public const string RETURN_HUB_SCENE = "Alita_Discovery_Cutscene";

        /// <summary>
        /// True when the scene is a cinematic cutscene. Pause/HUD systems stay off these
        /// scenes so the cutscene's own UI (scifi menu canvas, subtitles, letterbox) owns the screen.
        /// </summary>
        public static bool IsCutsceneScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;
            string lower = sceneName.ToLowerInvariant();
            return lower.Contains("discovery")
                || lower.Contains("clinic_workshop")
                || lower.Contains("clinic_awakening")
                || lower.Contains("tron_awakening")
                || lower.Contains("scrapyard_discovery")
                || lower.Contains("alita_discovery")
                || lower.Contains("blackout")
                || lower.Contains("moon_battle");
        }

        /// <summary>
        /// Destroys pause/HUD orphans by EXACT object name only — never touches the
        /// cutscene's own UI — then forces Resume state (unpause + realtime + audio).
        /// </summary>
        public static void DestroyPauseOrphansAndForceResume(string reason)
        {
            // Resume first while the manager still exists (restores timescale/cursor hooks).
            if (IsPaused && Instance != null)
            {
                try { Instance.ResumeGame(); }
                catch (Exception ex) { Debug.LogException(ex); }
            }

            string[] orphanNames = new string[]
            {
                "Canvas_InGameHUD",
                "Canvas_PauseMenu",
                "InGamePauseManagerController",
                "CyberCityStartPointReceiver",
                "MinimapRenderCamera"
            };
            foreach (string orphanName in orphanNames)
            {
                GameObject orphan = GameObject.Find(orphanName);
                if (orphan != null)
                {
                    Debug.Log($"[InGamePauseManager] {reason}: destroying orphan '{orphanName}'.");
                    Destroy(orphan);
                }
            }

            // Force Resume state even if the controller was destroyed while paused
            // (OnDestroy already clears IsPaused; this covers the manager-less path).
            IsPaused = false;
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
            AudioListener.pause = false;
        }

        private Coroutine _fadeCoroutine;
        private Coroutine _menuTransitionCoroutine;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoEnsurePauseManager()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            string sceneName = activeScene.name;
            if (string.IsNullOrEmpty(sceneName) ||
                sceneName.Equals("Scene_MainMenu", StringComparison.OrdinalIgnoreCase) ||
                sceneName.IndexOf("MainMenu", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return;
            }

            // Cutscene guard: never spawn the pause controller/canvas on cinematic
            // scenes; destroy persisted orphans and force Resume state instead.
            if (IsCutsceneScene(sceneName))
            {
                DestroyPauseOrphansAndForceResume($"Cutscene '{sceneName}' active");
                return;
            }

            EnsureEventSystem();

            InGamePauseManager mgr = UnityEngine.Object.FindAnyObjectByType<InGamePauseManager>();
            if (mgr == null)
            {
                GameObject go = new GameObject("InGamePauseManagerController");
                mgr = go.AddComponent<InGamePauseManager>();
                var sMgr = go.AddComponent<SettingsManager>();
                var cDisp = go.AddComponent<ControlsMenuDisplay>();
                DontDestroyOnLoad(go);

                var canvasObj = PauseMenuBuilder.BuildPauseMenu(mgr, sMgr, cDisp);
                if (canvasObj != null)
                {
                    DontDestroyOnLoad(canvasObj);
                }
                Debug.Log($"[InGamePauseManager] Auto-spawned persistent pause controller & canvas for '{sceneName}'.");
            }
        }

        public static GameObject FindPauseCanvasInScene()
        {
            var go = GameObject.Find("Canvas_PauseMenu");
            if (go != null) return go;

            var canvases = Resources.FindObjectsOfTypeAll<Canvas>();
            foreach (var c in canvases)
            {
                if (c != null && c.gameObject.name == "Canvas_PauseMenu" && c.gameObject.scene.isLoaded)
                {
                    return c.gameObject;
                }
            }
            return null;
        }

        public static void DestroyAllPauseCanvasesInScene(GameObject keepCanvas = null)
        {
            var canvases = Resources.FindObjectsOfTypeAll<Canvas>();
            foreach (var c in canvases)
            {
                if (c != null && c.gameObject.name == "Canvas_PauseMenu" && c.gameObject.scene.isLoaded)
                {
                    if (keepCanvas != null && c.gameObject == keepCanvas) continue;
                    if (Application.isPlaying)
                        Destroy(c.gameObject);
                    else
                        DestroyImmediate(c.gameObject);
                }
            }
        }

        public static void EnsurePauseOverlay()
        {
            var mgr = Instance != null ? Instance : UnityEngine.Object.FindAnyObjectByType<InGamePauseManager>();
            if (mgr == null)
            {
                var mgrGo = new GameObject("InGamePauseManagerController");
                mgr = mgrGo.AddComponent<InGamePauseManager>();
                mgrGo.AddComponent<SettingsManager>();
                mgrGo.AddComponent<ControlsMenuDisplay>();
            }
            else
            {
                if (mgr.pauseCanvasOverlay == null)
                {
                    var existingCanvas = FindPauseCanvasInScene();
                    if (existingCanvas != null)
                    {
                        mgr.pauseCanvasOverlay = existingCanvas;
                    }
                    else
                    {
                        var sMgr = mgr.GetComponent<SettingsManager>() ?? mgr.gameObject.AddComponent<SettingsManager>();
                        var cDisp = mgr.GetComponent<ControlsMenuDisplay>() ?? mgr.gameObject.AddComponent<ControlsMenuDisplay>();
                        mgr.pauseCanvasOverlay = PauseMenuBuilder.BuildPauseMenu(mgr, sMgr, cDisp);
                    }
                }
            }

            if (mgr != null)
            {
                mgr.AutoBindCanvasReferences();
                mgr.HookButtons();
            }
        }

        public void AutoBindCanvasReferences()
        {
            if (pauseCanvasOverlay == null)
            {
                var existingCanvas = FindPauseCanvasInScene();
                if (existingCanvas != null)
                {
                    pauseCanvasOverlay = existingCanvas;
                }
            }
            if (pauseCanvasOverlay == null) return;

            if (canvasGroup == null)
            {
                canvasGroup = pauseCanvasOverlay.GetComponent<CanvasGroup>();
            }

            // Find main panels
            Transform[] allTransforms = pauseCanvasOverlay.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in allTransforms)
            {
                if (pauseMainPanel == null && t.name == "Panel_PauseMain") pauseMainPanel = t.gameObject;
                else if (settingsPanel == null && t.name == "Panel_PauseSettings") settingsPanel = t.gameObject;
                else if (confirmPopupRoot == null && t.name == "Popup_Confirm") confirmPopupRoot = t.gameObject;
            }

            if (mainColumnRect == null && pauseCanvasOverlay != null)
            {
                Transform col = pauseCanvasOverlay.transform.Find("Panel_PauseMain/Column_PhantomLiberty");
                if (col != null) mainColumnRect = col.GetComponent<RectTransform>();
            }

            // Deactivate any legacy blue key prompt footer badges ([ESC] BACK / [ENTER] SELECT)
            foreach (Transform t in allTransforms)
            {
                if (t != null && (t.name == "FooterHints" || t.name == "Hint_Esc" || t.name == "Hint_Enter"))
                {
                    t.gameObject.SetActive(false);
                }
            }

            // Find buttons
            Button[] allButtons = pauseCanvasOverlay.GetComponentsInChildren<Button>(true);
            foreach (Button b in allButtons)
            {
                switch (b.name)
                {
                    case "Btn_Resume": resumeButton = b; break;
                    case "Btn_Settings": settingsButton = b; break;
                    case "Btn_Controls": controlsButton = b; break;
                    case "Btn_PhotoMode": photoModeButton = b; break;
                    case "Btn_Restart": restartButton = b; break;
                    case "Btn_MainMenu": mainMenuButton = b; break;
                    case "Btn_Quit":
                    case "Btn_QuitDesktop": quitDesktopButton = b; break;
                    case "Btn_TopBack": settingsBackButton = b; break;
                    case "Btn_ConfirmYes": confirmYesButton = b; break;
                    case "Btn_ConfirmNo": confirmNoButton = b; break;
                }
            }

            // Find texts
            TMP_Text[] allTexts = pauseCanvasOverlay.GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text txt in allTexts)
            {
                if (confirmTitleText == null && txt.name == "Text_Title" && txt.transform.IsChildOf(confirmPopupRoot != null ? confirmPopupRoot.transform : pauseCanvasOverlay.transform))
                {
                    confirmTitleText = txt;
                }
                else if (confirmDescText == null && txt.name == "Text_Desc" && txt.transform.IsChildOf(confirmPopupRoot != null ? confirmPopupRoot.transform : pauseCanvasOverlay.transform))
                {
                    confirmDescText = txt;
                }
                else if (headerTitleText == null && txt.name == "Text_Paused")
                {
                    headerTitleText = txt;
                }
            }
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current == null && UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
            {
                GameObject eventSys = new GameObject("EventSystem");
                eventSys.AddComponent<EventSystem>();

                var inputSysType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
                if (inputSysType != null)
                {
                    eventSys.AddComponent(inputSysType);
                }
                else
                {
                    eventSys.AddComponent<StandaloneInputModule>();
                }
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            EnsureUIAudioSource();
            EnsureEventSystem();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                IsPaused = false;
                Instance = null;
            }
        }

        private void Start()
        {
            EnsureEventSystem();

            if (Application.isPlaying)
            {
                DestroyAllPauseCanvasesInScene();
                var sMgr = GetComponent<SettingsManager>() ?? gameObject.AddComponent<SettingsManager>();
                var cDisp = GetComponent<ControlsMenuDisplay>() ?? gameObject.AddComponent<ControlsMenuDisplay>();
                pauseCanvasOverlay = PauseMenuBuilder.BuildPauseMenu(this, sMgr, cDisp);
            }
            else if (pauseCanvasOverlay == null)
            {
                var existingCanvas = FindPauseCanvasInScene();
                if (existingCanvas != null)
                {
                    pauseCanvasOverlay = existingCanvas;
                }
                else
                {
                    var sMgr = GetComponent<SettingsManager>() ?? gameObject.AddComponent<SettingsManager>();
                    var cDisp = GetComponent<ControlsMenuDisplay>() ?? gameObject.AddComponent<ControlsMenuDisplay>();
                    pauseCanvasOverlay = PauseMenuBuilder.BuildPauseMenu(this, sMgr, cDisp);
                }
            }

            if (pauseCanvasOverlay != null)
            {
                pauseCanvasOverlay.SetActive(false);
            }
            if (confirmPopupRoot != null)
            {
                confirmPopupRoot.SetActive(false);
            }

            AutoBindCanvasReferences();
            HookButtons();
            DestroyOrphanPauseCanvases();
            PauseMenuBuilder.ApplyCyberpunkHardwareCursor();

            // Gentle blink on the footer version strip so the paused frame feels alive.
            try
            {
                if (pauseCanvasOverlay != null)
                {
                    TMP_Text[] texts = pauseCanvasOverlay.GetComponentsInChildren<TMP_Text>(true);
                    foreach (TMP_Text t in texts)
                    {
                        if (t != null && t.name == "Text_VersionStrip")
                        {
                            PauseMenuEffects.PlayBlinkingCursor(this, t, 1.5f, 0.65f);
                            break;
                        }
                    }
                }
            }
            catch (Exception ex) { Debug.LogException(ex); }
        }

        private bool IsValidPauseCanvas(GameObject c)
        {
            if (c == null) return false;
            bool hasResume = false;
            bool hasMain = false;
            Button[] buttons = c.GetComponentsInChildren<Button>(true);
            foreach (Button b in buttons)
            {
                if (b != null && b.name == "Btn_Resume") { hasResume = true; break; }
            }
            if (!hasResume) return false;
            Transform[] all = c.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in all)
            {
                if (t != null && t.name == "Panel_PauseMain") { hasMain = true; break; }
            }
            return hasMain;
        }

        private void DestroyOrphanPauseCanvases()
        {
            Canvas[] all = Resources.FindObjectsOfTypeAll<Canvas>();
            foreach (Canvas c in all)
            {
                if (c == null) continue;
                GameObject go = c.gameObject;
                if (go == null || go.name != "Canvas_PauseMenu") continue;
                if (go == pauseCanvasOverlay) continue;
                if (!go.scene.IsValid()) continue;
                Destroy(go);
            }
        }

        private void Update()
        {
            // If in main menu scene, keep pauser completely inactive
            string activeScene = SceneManager.GetActiveScene().name;
            if (activeScene.Equals("Scene_MainMenu", StringComparison.OrdinalIgnoreCase) ||
                activeScene.IndexOf("MainMenu", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (IsPaused) ResumeGame();
                return;
            }

            // Cutscene guard: pauser stays fully inactive on cinematic scenes so the
            // cutscene's own UI owns the screen. Sweep persisted orphans (throttled)
            // and force Resume state.
            if (IsCutsceneScene(activeScene))
            {
                if (IsPaused) ResumeGame();
                if (Time.unscaledTime - _lastCutsceneSweepTime > 1.0f)
                {
                    _lastCutsceneSweepTime = Time.unscaledTime;
                    DestroyPauseOrphansAndForceResume($"Cutscene '{activeScene}' active");
                }
                else
                {
                    Time.timeScale = 1f;
                    AudioListener.pause = false;
                }
                return;
            }

            // Real-time debounce check
            if (Time.realtimeSinceStartup - _lastToggleRealtime < DEBOUNCE_INTERVAL)
            {
                return;
            }

            bool escPressed = false;
            bool gamepadStartPressed = false;
            bool gamepadBackPressed = false;

            // 1. New Input System checks
            if (Keyboard.current != null)
            {
                if (Keyboard.current.escapeKey.wasPressedThisFrame ||
                    Keyboard.current.pKey.wasPressedThisFrame)
                {
                    escPressed = true;
                }
            }

            if (Gamepad.current != null)
            {
                if (Gamepad.current.startButton.wasPressedThisFrame)
                {
                    gamepadStartPressed = true;
                }
                if (Gamepad.current.bButton.wasPressedThisFrame)
                {
                    gamepadBackPressed = true;
                }
            }

            // 2. Legacy Input fallback (wrapped in try/catch in case legacy input is disabled)
            try
            {
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
                {
                    escPressed = true;
                }
                if (Input.GetKeyDown(KeyCode.JoystickButton7)) // Xbox Start
                {
                    gamepadStartPressed = true;
                }
                if (Input.GetKeyDown(KeyCode.JoystickButton1)) // Xbox B
                {
                    gamepadBackPressed = true;
                }
            }
            catch { }

            // Handle Gamepad B / Back when menu is open
            if (IsPaused && gamepadBackPressed)
            {
                HandleBackNavigation();
                return;
            }

            // Toggle Pause or Back
            if (escPressed || gamepadStartPressed)
            {
                Debug.Log($"[InGamePauseManager] Pause input triggered (esc={escPressed}, start={gamepadStartPressed}). IsPaused={IsPaused}, CanPause={CanPause()}");
                if (!IsPaused)
                {
                    // Check edge cases: cannot pause during loading or during cutscenes
                    if (CanPause())
                    {
                        PauseGame();
                    }
                    else
                    {
                        Debug.LogWarning("[InGamePauseManager] Pause request denied by CanPause().");
                        PlayDenyAudio();
                    }
                }
                else
                {
                    HandleBackNavigation();
                }
            }

            // In-Settings Shortcuts: Q/E cycle tabs, R reset to default
            if (IsPaused && settingsPanel != null && settingsPanel.activeSelf)
            {
                bool qPressed = false, ePressed = false, rPressed = false;

                if (Keyboard.current != null)
                {
                    if (Keyboard.current.qKey.wasPressedThisFrame) qPressed = true;
                    if (Keyboard.current.eKey.wasPressedThisFrame) ePressed = true;
                    if (Keyboard.current.rKey.wasPressedThisFrame) rPressed = true;
                }

                if (Gamepad.current != null)
                {
                    if (Gamepad.current.leftShoulder.wasPressedThisFrame) qPressed = true;
                    if (Gamepad.current.rightShoulder.wasPressedThisFrame) ePressed = true;
                    if (Gamepad.current.xButton.wasPressedThisFrame) rPressed = true;
                }

                try
                {
                    if (Input.GetKeyDown(KeyCode.Q)) qPressed = true;
                    if (Input.GetKeyDown(KeyCode.E)) ePressed = true;
                    if (Input.GetKeyDown(KeyCode.R)) rPressed = true;
                }
                catch { }

                if (qPressed) OnCycleTab?.Invoke(-1);
                if (ePressed) OnCycleTab?.Invoke(1);
                if (rPressed) OnResetSettings?.Invoke();
            }

            // Update realtime header clock and FPS if paused
            if (IsPaused && headerStatusText != null)
            {
                float fps = Time.unscaledDeltaTime > 0.0001f ? (1f / Time.unscaledDeltaTime) : 60f;
                headerStatusText.text = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} // {activeScene} // {Mathf.RoundToInt(fps)} FPS";
            }
        }

        private bool CanPause()
        {
            // 1. Loading check
            if (MainMenuController.Instance != null && MainMenuController.Instance.IsLoading)
            {
                return false;
            }

            // 2. Cutscene check: pause is denied on cinematic scenes.
            if (IsCutsceneScene(SceneManager.GetActiveScene().name))
            {
                return false;
            }

            return true;
        }

        private void HandleBackNavigation()
        {
            _lastToggleRealtime = Time.realtimeSinceStartup;

            // If confirmation popup is visible, close it
            if (confirmPopupRoot != null && confirmPopupRoot.activeSelf)
            {
                HideConfirmPopup();
                PlayCancelAudio();
                return;
            }

            // If a subpanel is active, return to Main pause panel
            if ((settingsPanel != null && settingsPanel.activeSelf) ||
                (controlsPanel != null && controlsPanel.activeSelf))
            {
                ShowPauseMain();
                PlayBackAudio();
                return;
            }

            // Otherwise resume the game
            ResumeGame();
        }

        public void TogglePause()
        {
            if (IsPaused) ResumeGame();
            else if (CanPause()) PauseGame();
            else PlayDenyAudio();
        }

        public void PauseGame()
        {
            if (IsPaused) return;

            _lastToggleRealtime = Time.realtimeSinceStartup;
            IsPaused = true;

            EnsureEventSystem();

            // Self-heal canvas overlay if somehow null
            if (pauseCanvasOverlay == null)
            {
                var existingCanvas = FindPauseCanvasInScene();
                if (existingCanvas != null)
                {
                    pauseCanvasOverlay = existingCanvas;
                }
                else
                {
                    var sMgr = GetComponent<SettingsManager>() ?? gameObject.AddComponent<SettingsManager>();
                    var cDisp = GetComponent<ControlsMenuDisplay>() ?? gameObject.AddComponent<ControlsMenuDisplay>();
                    pauseCanvasOverlay = PauseMenuBuilder.BuildPauseMenu(this, sMgr, cDisp);
                }
            }

            AutoBindCanvasReferences();
            HookButtons();

            // 1. Cache current timescale & slow-motion state
            _prevTimeScale = Time.timeScale;
            _prevFixedDelta = Time.fixedDeltaTime;

            var playerCtrl = InsaneGun.Combat.InsaneGunSwordController.Instance;
            if (playerCtrl != null)
            {
                _wasBulletTimeActive = playerCtrl.IsSlowMotionActive;
                _playerWasEnabled = playerCtrl.enabled;
                playerCtrl.enabled = false; // Freeze player locomotion/input during pause
            }
            else
            {
                _wasBulletTimeActive = false;
            }

            // 2. Freeze simulation
            Time.timeScale = 0f;
            Time.fixedDeltaTime = 0f;
            AudioListener.pause = true;

            // 3. Unlock & show cursor
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            InsaneGun.Combat.SmoothOrbitCamera.cursorLockOverride = true;
            PauseMenuBuilder.ApplyCyberpunkHardwareCursor();

            // 4. Show Pause UI & start smooth pop-in transition
            if (pauseCanvasOverlay != null)
            {
                pauseCanvasOverlay.SetActive(true);
            }
            ShowPauseMain(false);
            PlayOpenAudio();
            StartSmoothPopIn();

            OnPauseChanged?.Invoke(true);
        }

        public void ResumeGame()
        {
            if (!IsPaused) return;

            _lastToggleRealtime = Time.realtimeSinceStartup;
            IsPaused = false;

            // 1. Restore time scale (restore bullet time if active previously)
            var playerCtrl = InsaneGun.Combat.InsaneGunSwordController.Instance;
            if (_wasBulletTimeActive && playerCtrl != null)
            {
                Time.timeScale = playerCtrl.slowMotionTimeScale;
                Time.fixedDeltaTime = 0.02f * Time.timeScale;
            }
            else
            {
                Time.timeScale = 1f;
                Time.fixedDeltaTime = 0.02f;
            }

            AudioListener.pause = false;

            // 2. Lock cursor
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            InsaneGun.Combat.SmoothOrbitCamera.cursorLockOverride = false;

            // 3. Re-enable player controller
            if (playerCtrl != null && _playerWasEnabled)
            {
                playerCtrl.enabled = true;
            }

            // 4. Close confirmation popup & smooth exit transition
            HideConfirmPopup();
            try { PauseMenuEffects.StopSelectionTick(); }
            catch (Exception ex) { Debug.LogException(ex); }

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }

            PlayCloseAudio();
            StartSmoothExit();

            OnPauseChanged?.Invoke(false);
        }

        public void ShowPauseMain(bool playCascade = true)
        {
            HideConfirmPopup();
            if (pauseMainPanel != null) pauseMainPanel.SetActive(true);
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (controlsPanel != null) controlsPanel.SetActive(false);

            if (resumeButton != null && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);
            }

            if (playCascade && _menuTransitionCoroutine == null)
            {
                try { PauseMenuEffects.PlaySlideCascade(this, GetMainPanelItemRects(), new Vector2(-32f, 0f), 0.03f, 0.18f); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
        }

        public void ShowSettings()
        {
            if (pauseMainPanel != null) pauseMainPanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(true);
            if (controlsPanel != null) controlsPanel.SetActive(false);
            HideConfirmPopup();
            PlayClickAudio();

            EnsureSettingsWired();
            SelectSettingsTab(_activeSettingsTabIndex >= 0 ? _activeSettingsTabIndex : 0);
        }

        public void ShowControls()
        {
            if (pauseMainPanel != null) pauseMainPanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(true);
            if (controlsPanel != null) controlsPanel.SetActive(false);
            HideConfirmPopup();
            PlayClickAudio();

            EnsureSettingsWired();
            SelectSettingsTab(2);
        }

        public void ShowPhotoMode()
        {
            PlayClickAudio();
            Debug.Log("[PauseMenu] Photo Mode triggered.");
        }

        public void RequestRestartCheckpoint()
        {
            PlayClickAudio();
            ShowConfirmPopup(
                "RESTART CHECKPOINT",
                "RELOAD RECENT SECTOR CHECKPOINT?\nALL UNSAVED TACTICAL DATA WILL BE RESET.",
                () =>
                {
                    Time.timeScale = 1f;
                    Time.fixedDeltaTime = 0.02f;
                    AudioListener.pause = false;
                    IsPaused = false;
                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                }
            );
        }

        public void RequestReturnToMainMenu()
        {
            PlayClickAudio();
            ShowConfirmPopup(
                "RETURN TO DISCOVERY",
                "RESTART THE PROLOGUE FROM SCRAPYARD DISCOVERY?\nALL UNSAVED TACTICAL DATA WILL BE RESET.",
                () =>
                {
                    Time.timeScale = 1f;
                    Time.fixedDeltaTime = 0.02f;
                    AudioListener.pause = false;
                    IsPaused = false;
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                    SceneManager.LoadScene(RETURN_HUB_SCENE);
                }
            );
        }

        public void RequestQuitDesktop()
        {
            PlayClickAudio();
            ShowConfirmPopup(
                "ABORT SIMULATION",
                "CONFIRM TERMINATION OF SCENARIO PROTOCOL?\nEXIT SYSTEM ENVIRONMENT TO DESKTOP.",
                () =>
                {
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                }
            );
        }

        public void ShowConfirmPopup(string title, string desc, Action onConfirm, Action onCancel = null)
        {
            _pendingConfirmAction = onConfirm;
            _pendingCancelAction = onCancel;

            if (confirmPopupRoot != null)
            {
                if (confirmTitleText != null) confirmTitleText.text = title.ToUpper();
                if (confirmDescText != null) confirmDescText.text = desc;
                confirmPopupRoot.SetActive(true);

                if (confirmYesButton != null && EventSystem.current != null)
                {
                    EventSystem.current.SetSelectedGameObject(confirmYesButton.gameObject);
                }
            }
            else
            {
                // Fallback direct execution if no popup configured
                onConfirm?.Invoke();
            }
        }

        public void HideConfirmPopup()
        {
            if (confirmPopupRoot != null)
            {
                confirmPopupRoot.SetActive(false);
            }
            _pendingConfirmAction = null;
            _pendingCancelAction = null;
        }

        public void OnConfirmYesClicked()
        {
            PlayClickAudio();
            Action act = _pendingConfirmAction;
            HideConfirmPopup();
            act?.Invoke();
        }

        public void OnConfirmNoClicked()
        {
            PlayCancelAudio();
            Action act = _pendingCancelAction;
            HideConfirmPopup();
            act?.Invoke();
        }

        public void HookButtons()
        {
            AutoBindCanvasReferences();

            if (resumeButton)
            {
                resumeButton.onClick.RemoveAllListeners();
                resumeButton.onClick.AddListener(() =>
                {
                    try { PauseMenuEffects.PlayPunchScale(this, resumeButton, 1.1f, 0.12f); }
                    catch (Exception ex) { Debug.LogException(ex); }
                    try { ResumeGame(); }
                    catch (Exception ex) { Debug.LogException(ex); }
                });
            }

            if (settingsButton)
            {
                settingsButton.onClick.RemoveAllListeners();
                settingsButton.onClick.AddListener(ShowSettings);
            }

            if (controlsButton)
            {
                controlsButton.onClick.RemoveAllListeners();
                controlsButton.onClick.AddListener(ShowControls);
            }

            if (photoModeButton)
            {
                photoModeButton.onClick.RemoveAllListeners();
                photoModeButton.onClick.AddListener(ShowPhotoMode);
            }

            if (restartButton)
            {
                restartButton.onClick.RemoveAllListeners();
                restartButton.onClick.AddListener(RequestRestartCheckpoint);
            }

            if (mainMenuButton)
            {
                mainMenuButton.onClick.RemoveAllListeners();
                mainMenuButton.onClick.AddListener(RequestReturnToMainMenu);
            }

            if (quitDesktopButton)
            {
                quitDesktopButton.onClick.RemoveAllListeners();
                quitDesktopButton.onClick.AddListener(RequestQuitDesktop);
            }

            if (settingsBackButton)
            {
                settingsBackButton.onClick.RemoveAllListeners();
                settingsBackButton.onClick.AddListener(() => { PlayBackAudio(); ShowPauseMain(); });
            }

            if (controlsBackButton)
            {
                controlsBackButton.onClick.RemoveAllListeners();
                controlsBackButton.onClick.AddListener(() => { PlayBackAudio(); ShowPauseMain(); });
            }

            if (confirmYesButton)
            {
                confirmYesButton.onClick.RemoveAllListeners();
                confirmYesButton.onClick.AddListener(OnConfirmYesClicked);
            }

            if (confirmNoButton)
            {
                confirmNoButton.onClick.RemoveAllListeners();
                confirmNoButton.onClick.AddListener(OnConfirmNoClicked);
            }

            EnsureSettingsWired();
        }

        #region Runtime Settings Tab Switching
        private int _activeSettingsTabIndex = 0;

        public void CycleSettingsTab(int dir)
        {
            int next = _activeSettingsTabIndex + dir;
            if (next < 0) next = 4;
            if (next > 4) next = 0;
            SelectSettingsTab(next);
        }

        public void SelectSettingsTab(int idx)
        {
            EnsureSettingsWired();

            idx = Mathf.Clamp(idx, 0, 4);
            _activeSettingsTabIndex = idx;

            if (settingsPanel == null) return;

            var tabBar = settingsPanel.transform.Find("SettingsWindow/TabBar");
            var bodyContainer = settingsPanel.transform.Find("SettingsWindow/BodyContainer");
            if (tabBar == null || bodyContainer == null) return;

            var leftStack = bodyContainer.Find("LeftOptionsStack");
            var rightStack = bodyContainer.Find("RightStack");
            if (leftStack == null || rightStack == null) return;

            var titleText = settingsPanel.transform.Find("SettingsWindow/HeaderBar/Text_Title")?.GetComponent<TMP_Text>()
                         ?? settingsPanel.transform.Find("SettingsWindow/HeaderBar/TitleText")?.GetComponent<TMP_Text>();
            string[] breadcrumbTitles = new string[]
            {
                "SETTINGS // GRAPHICS",
                "SETTINGS  >  AUDIO",
                "SETTINGS  >  CONTROLS",
                "SETTINGS  >  GAMEPLAY",
                "SETTINGS  >  INTERFACE"
            };
            if (titleText != null && idx < breadcrumbTitles.Length)
                titleText.text = breadcrumbTitles[idx];

            string[] tabNames = new string[] { "Tab_Graphics", "Tab_Audio", "Tab_Controls", "Tab_Gameplay", "Tab_Interface" };
            string[] sectionNames = new string[] { "Section_Graphics", "Section_Audio", "Section_Controls", "Section_Gameplay", "Section_Interface" };

            for (int i = 0; i < 5; i++)
            {
                var tabBtn = tabBar.Find(tabNames[i])?.GetComponent<Button>();
                if (tabBtn != null)
                {
                    PauseMenuBuilder.SetTabActiveStyle(tabBtn, i == idx);
                }

                var sec = leftStack.Find(sectionNames[i]);
                if (sec != null)
                {
                    sec.gameObject.SetActive(i == idx);
                }
            }

            var cardPreview = rightStack.Find("Card_Preview");
            var cardControls = rightStack.Find("Card_ControlsSchematic");
            bool isControls = (idx == 2);
            if (cardPreview != null) cardPreview.gameObject.SetActive(!isControls);
            if (cardControls != null) cardControls.gameObject.SetActive(isControls);

            if (isControls)
            {
                SelectControlsSubTab(0);
            }
        }

        public void SelectControlsSubTab(int subIdx)
        {
            if (settingsPanel == null) return;
            var bodyContainer = settingsPanel.transform.Find("SettingsWindow/BodyContainer");
            if (bodyContainer == null) return;
            var secControls = bodyContainer.Find("LeftOptionsStack/Section_Controls");
            var rightCard = bodyContainer.Find("RightStack/Card_ControlsSchematic");
            if (secControls == null) return;

            subIdx = Mathf.Clamp(subIdx, 0, 2);

            string[] subTabNames = new string[] { "SubTab_KM", "SubTab_Ctrl", "SubTab_Cust" };
            string[] panelNames = new string[] { "Panel_KM", "Panel_Ctrl", "Panel_Cust" };
            string[] schematicNames = new string[] { "Schematic_KM", "Schematic_Ctrl", "Schematic_Cust" };

            var subTabBar = secControls.Find("SubTabBar");
            var contentBox = secControls.Find("SubTabContentBox");

            for (int i = 0; i < 3; i++)
            {
                if (subTabBar != null)
                {
                    var btn = subTabBar.Find(subTabNames[i])?.GetComponent<Button>();
                    if (btn != null) PauseMenuBuilder.SetTabActiveStyle(btn, i == subIdx);
                }

                Transform cont = null;
                if (contentBox != null) cont = contentBox.Find(panelNames[i]);
                if (cont == null) cont = secControls.Find(panelNames[i]);
                if (cont != null) cont.gameObject.SetActive(i == subIdx);

                if (rightCard != null)
                {
                    var sch = rightCard.Find(schematicNames[i]);
                    if (sch != null) sch.gameObject.SetActive(i == subIdx);
                }
            }
        }

        public void EnsureSettingsWired()
        {
            if (settingsPanel == null)
            {
                AutoBindCanvasReferences();
                if (settingsPanel == null) return;
            }

            var tabBar = settingsPanel.transform.Find("SettingsWindow/TabBar");
            if (tabBar == null) return;

            var bodyContainer = settingsPanel.transform.Find("SettingsWindow/BodyContainer");
            if (bodyContainer == null) return;

            var leftStack = bodyContainer.Find("LeftOptionsStack");
            var rightStack = bodyContainer.Find("RightStack");
            if (leftStack == null || rightStack == null) return;

            var btnTopBack = settingsPanel.transform.Find("SettingsWindow/HeaderBar/Btn_TopBack")?.GetComponent<Button>();
            if (btnTopBack != null)
            {
                btnTopBack.onClick.RemoveAllListeners();
                btnTopBack.onClick.AddListener(() => { PlayBackAudio(); ShowPauseMain(); });
            }

            string[] tabNames = new string[] { "Tab_Graphics", "Tab_Audio", "Tab_Controls", "Tab_Gameplay", "Tab_Interface" };
            for (int i = 0; i < tabNames.Length; i++)
            {
                var tabBtn = tabBar.Find(tabNames[i])?.GetComponent<Button>();
                if (tabBtn != null)
                {
                    int tabIdx = i;
                    tabBtn.onClick.RemoveAllListeners();
                    tabBtn.onClick.AddListener(() =>
                    {
                        PlayClickAudio();
                        SelectSettingsTab(tabIdx);
                    });
                }
            }

            var btnKeyQ = tabBar.Find("Btn_KeyQ")?.GetComponent<Button>();
            if (btnKeyQ != null)
            {
                btnKeyQ.onClick.RemoveAllListeners();
                btnKeyQ.onClick.AddListener(() =>
                {
                    PlayClickAudio();
                    CycleSettingsTab(-1);
                });
            }

            var btnKeyE = tabBar.Find("Btn_KeyE")?.GetComponent<Button>();
            if (btnKeyE != null)
            {
                btnKeyE.onClick.RemoveAllListeners();
                btnKeyE.onClick.AddListener(() =>
                {
                    PlayClickAudio();
                    CycleSettingsTab(1);
                });
            }

            OnSelectTab = SelectSettingsTab;
            OnCycleTab = CycleSettingsTab;

            var secControls = leftStack.Find("Section_Controls");
            if (secControls != null)
            {
                var subTabBar = secControls.Find("SubTabBar");
                if (subTabBar != null)
                {
                    string[] subTabNames = new string[] { "SubTab_KM", "SubTab_Ctrl", "SubTab_Cust" };
                    for (int s = 0; s < subTabNames.Length; s++)
                    {
                        var sBtn = subTabBar.Find(subTabNames[s])?.GetComponent<Button>();
                        if (sBtn != null)
                        {
                            int subIdx = s;
                            sBtn.onClick.RemoveAllListeners();
                            sBtn.onClick.AddListener(() =>
                            {
                                PlayClickAudio();
                                SelectControlsSubTab(subIdx);
                            });
                        }
                    }
                }
            }

            var footerBar = settingsPanel.transform.Find("SettingsWindow/FooterBar");
            if (footerBar != null)
            {
                var btnReset = footerBar.Find("Btn_ResetDefaults")?.GetComponent<Button>();
                if (btnReset != null)
                {
                    btnReset.onClick.RemoveAllListeners();
                    btnReset.onClick.AddListener(() =>
                    {
                        PlayClickAudio();
                        var sMgr = GetComponent<SettingsManager>() ?? SettingsManager.Instance;
                        if (sMgr != null) sMgr.ResetToDefaults();
                        SelectSettingsTab(_activeSettingsTabIndex);
                    });
                }

                var btnApply = footerBar.Find("Btn_Apply")?.GetComponent<Button>();
                if (btnApply != null)
                {
                    btnApply.onClick.RemoveAllListeners();
                    btnApply.onClick.AddListener(() =>
                    {
                        PlayClickAudio();
                        var sMgr = GetComponent<SettingsManager>() ?? SettingsManager.Instance;
                        if (sMgr != null)
                        {
                            sMgr.SaveAll();
                            sMgr.LoadAll();
                        }
                    });
                }
            }
        }
        #endregion

        /// <summary>Main pause buttons as cascade targets (hierarchy order, capped).</summary>
        private RectTransform[] GetMainPanelItemRects()
        {
            try
            {
                if (pauseMainPanel == null) return new RectTransform[0];
                Button[] btns = pauseMainPanel.GetComponentsInChildren<Button>(true);
                System.Collections.Generic.List<RectTransform> outRects = new System.Collections.Generic.List<RectTransform>();
                foreach (Button b in btns)
                {
                    if (b == null || outRects.Count >= 12) continue;
                    RectTransform rt = null;
                    try { rt = b.GetComponent<RectTransform>(); } catch (Exception ex) { Debug.LogException(ex); }
                    if (rt != null) outRects.Add(rt);
                }
                return outRects.ToArray();
            }
            catch (Exception ex) { Debug.LogException(ex); return new RectTransform[0]; }
        }

        /// <summary>Settings tab-bar preserves layout strictly; cascade not applied to individual tabs.</summary>
        private RectTransform[] GetSettingsPanelItemRects()
        {
            return new RectTransform[0];
        }

        #region Smooth Pop-In & Exit Transitions
        public void StartSmoothPopIn()
        {
            if (_menuTransitionCoroutine != null)
            {
                StopCoroutine(_menuTransitionCoroutine);
                _menuTransitionCoroutine = null;
            }
            _menuTransitionCoroutine = StartCoroutine(SmoothPopInRoutine());
        }

        private IEnumerator SmoothPopInRoutine()
        {
            float duration = 0.22f;
            float elapsed = 0f;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = true;
            }

            if (mainColumnRect == null && pauseMainPanel != null)
            {
                Transform col = pauseMainPanel.transform.Find("Column_PhantomLiberty");
                if (col != null) mainColumnRect = col.GetComponent<RectTransform>();
            }

            Vector2 startPos = new Vector2(-65f, 0f);
            Vector2 targetPos = Vector2.zero;
            Vector3 startScale = new Vector3(0.95f, 0.95f, 1f);
            Vector3 targetScale = Vector3.one;

            if (mainColumnRect != null)
            {
                mainColumnRect.anchoredPosition = startPos;
                mainColumnRect.localScale = startScale;
            }

            // Staggered slide cascade on buttons synchronized with pop-in
            try
            {
                PauseMenuEffects.PlaySlideCascade(this, GetMainPanelItemRects(), new Vector2(-36f, 0f), 0.025f, 0.20f);
            }
            catch (Exception ex) { Debug.LogException(ex); }

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Smooth cubic ease-out for snappy, visceral entrance
                float ease = 1f - Mathf.Pow(1f - t, 3f);
                float alphaT = Mathf.SmoothStep(0f, 1f, t);

                if (canvasGroup != null)
                {
                    canvasGroup.alpha = alphaT;
                }

                if (mainColumnRect != null)
                {
                    mainColumnRect.anchoredPosition = Vector2.Lerp(startPos, targetPos, ease);
                    mainColumnRect.localScale = Vector3.Lerp(startScale, targetScale, ease);
                }

                yield return null;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            if (mainColumnRect != null)
            {
                mainColumnRect.anchoredPosition = targetPos;
                mainColumnRect.localScale = targetScale;
            }

            if (resumeButton != null && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);
            }

            try
            {
                PauseMenuEffects.BeginSelectionTick(this, pauseMainPanel);
            }
            catch (Exception ex) { Debug.LogException(ex); }

            _menuTransitionCoroutine = null;
        }

        public void StartSmoothExit()
        {
            if (_menuTransitionCoroutine != null)
            {
                StopCoroutine(_menuTransitionCoroutine);
                _menuTransitionCoroutine = null;
            }
            _menuTransitionCoroutine = StartCoroutine(SmoothExitRoutine());
        }

        private IEnumerator SmoothExitRoutine()
        {
            float duration = 0.12f;
            float elapsed = 0f;

            if (canvasGroup != null)
            {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            float startAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;
            Vector2 startPos = mainColumnRect != null ? mainColumnRect.anchoredPosition : Vector2.zero;
            Vector2 targetPos = new Vector2(-45f, 0f);
            Vector3 startScale = mainColumnRect != null ? mainColumnRect.localScale : Vector3.one;
            Vector3 targetScale = new Vector3(0.97f, 0.97f, 1f);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                if (canvasGroup != null)
                {
                    canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
                }

                if (mainColumnRect != null)
                {
                    mainColumnRect.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
                    mainColumnRect.localScale = Vector3.Lerp(startScale, targetScale, t);
                }

                yield return null;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }

            if (mainColumnRect != null)
            {
                mainColumnRect.anchoredPosition = Vector2.zero;
                mainColumnRect.localScale = Vector3.one;
            }

            if (pauseCanvasOverlay != null)
            {
                pauseCanvasOverlay.SetActive(false);
            }

            _menuTransitionCoroutine = null;
        }

        private void AnimatePanelFade(bool fadeIn)
        {
            if (fadeIn) StartSmoothPopIn();
            else StartSmoothExit();
        }
        #endregion

        #region Procedural UI Audio Feedback
        private void EnsureUIAudioSource()
        {
            if (uiAudioSource == null)
            {
                uiAudioSource = gameObject.GetComponent<AudioSource>();
                if (uiAudioSource == null) uiAudioSource = gameObject.AddComponent<AudioSource>();
            }
            uiAudioSource.playOnAwake = false;
            uiAudioSource.ignoreListenerPause = true; // Crucial: must play when AudioListener.pause is true!
        }

        public void PlayHoverAudio()  => PlayTone(880f, 0.04f, 0.12f);
        public void PlayClickAudio()  => PlayTone(1400f, 0.05f, 0.25f);
        public void PlayBackAudio()   => PlayTone(600f, 0.06f, 0.2f);
        public void PlayCancelAudio() => PlayTone(480f, 0.07f, 0.22f);
        public void PlayDenyAudio()   => PlayTone(220f, 0.12f, 0.35f);
        public void PlayOpenAudio()   => PlayTone(1050f, 0.08f, 0.22f);
        public void PlayCloseAudio()  => PlayTone(520f, 0.06f, 0.18f);

        private void PlayTone(float frequency, float length, float volume)
        {
            EnsureUIAudioSource();
            if (uiAudioSource == null) return;

            int sampleRate = 44100;
            int sampleCount = Mathf.CeilToInt(sampleRate * length);
            AudioClip clip = AudioClip.Create("UI_Tone", sampleCount, 1, sampleRate, false);

            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Clamp01(1f - (t / length));
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope;
            }

            clip.SetData(samples, 0);
            float masterVol = PlayerPrefs.GetFloat(SettingsManager.OPT_MASTER_VOL, 100f) / 100f;
            float uiVol = PlayerPrefs.GetFloat(SettingsManager.OPT_UI_VOL, 90f) / 100f;
            float finalVolume = volume * Mathf.Clamp01(masterVol) * Mathf.Clamp01(uiVol);
            if (finalVolume > 0.001f)
            {
                uiAudioSource.PlayOneShot(clip, finalVolume);
            }
        }
        #endregion
    }
}
