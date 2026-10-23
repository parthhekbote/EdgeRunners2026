using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using TMPro;

namespace SyntyCombined
{
    /// <summary>
    /// Runtime initializer that automatically spawns Synty's Sci-Fi Minimap HUD,
    /// In-Game Pause Overlay, Settings, Controls, and Sector Receiver as soon as Demo_City loads.
    /// Requires ZERO manual editor setup steps!
    /// </summary>
    public static class CyberCityRuntimeHUDInitializer
    {
        /// <summary>
        /// True when the scene is a cinematic cutscene (Alita prologue/awakening chain
        /// or other non-gameplay cinematics). Hub/HUD systems must stay off these scenes
        /// so the cutscene's own UI (scifi menu canvas, subtitles, letterbox) owns the screen.
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
        /// Destroys Hub/HUD orphans by EXACT object name only — never touches the
        /// cutscene's own UI (AlitaCutsceneController.scifiMenuCanvasGroup,
        /// AlitaCutsceneSciFiMenu, subtitles, letterbox) — then forces Resume state.
        /// </summary>
        public static void DestroyHubOrphansAndForceResume(string reason)
        {
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
                    Debug.Log($"[CyberCityHUD] {reason}: destroying Hub/HUD orphan '{orphanName}'.");
                    Object.Destroy(orphan);
                }
            }

            InGamePauseManager pauseMgr = Object.FindAnyObjectByType<InGamePauseManager>();
            if (pauseMgr != null && InGamePauseManager.IsPaused)
            {
                try { pauseMgr.ResumeGame(); }
                catch (System.Exception ex) { Debug.LogException(ex); }
            }
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
            AudioListener.pause = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnSceneLoadedRuntime()
        {
            SceneManager.sceneLoaded += OnSceneLoadedHandler;
            CheckAndBuildHUD(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoadedHandler(Scene scene, LoadSceneMode mode)
        {
            CheckAndBuildHUD(scene);
        }

        [Tooltip("Master toggle for the Cyberpunk 2077 in-game HUD overlay.")]
        public static bool enableCyberpunkHUD = true;

        public static void CheckAndBuildHUD(Scene scene)
        {
            if (scene.name.Equals("Scene_MainMenu", System.StringComparison.OrdinalIgnoreCase) ||
                scene.name.IndexOf("MainMenu", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                scene.name.IndexOf("Showcase", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return;
            }

            // Cutscene guard: Hub/HUD systems stay off cinematic scenes. Destroy any
            // persisted orphans and force Resume state so cutscene UI owns the screen.
            if (IsCutsceneScene(scene.name))
            {
                DestroyHubOrphansAndForceResume($"Cutscene '{scene.name}' active");
                return;
            }

            // 1. Ensure EventSystem
            InGamePauseManager.EnsureEventSystem();

            // 2. Ensure Pause System exists
            InGamePauseManager pauseMgr = Object.FindAnyObjectByType<InGamePauseManager>();
            if (pauseMgr == null)
            {
                GameObject pauseManagerControllerObj = new GameObject("InGamePauseManagerController");
                pauseMgr = pauseManagerControllerObj.AddComponent<InGamePauseManager>();
                pauseManagerControllerObj.AddComponent<SettingsManager>();
                pauseManagerControllerObj.AddComponent<ControlsMenuDisplay>();
            }

            if (pauseMgr.pauseCanvasOverlay == null)
            {
                GameObject existingCanvas = InGamePauseManager.FindPauseCanvasInScene();
                if (existingCanvas != null)
                {
                    pauseMgr.pauseCanvasOverlay = existingCanvas;
                }
                else
                {
                    SettingsManager sMgr = pauseMgr.GetComponent<SettingsManager>() ?? pauseMgr.gameObject.AddComponent<SettingsManager>();
                    ControlsMenuDisplay cDisplay = pauseMgr.GetComponent<ControlsMenuDisplay>() ?? pauseMgr.gameObject.AddComponent<ControlsMenuDisplay>();
                    pauseMgr.pauseCanvasOverlay = PauseMenuBuilder.BuildPauseMenu(pauseMgr, sMgr, cDisplay);
                }
            }
            pauseMgr.AutoBindCanvasReferences();
            pauseMgr.HookButtons();

            // 3. User requested HUD removal: clean up any stray HUD objects and exit
            if (!enableCyberpunkHUD)
            {
                GameObject strayHUD = GameObject.Find("Canvas_InGameHUD");
                if (strayHUD != null) Object.Destroy(strayHUD);
                return;
            }

            string lower = scene.name.ToLowerInvariant();
            if (lower.Contains("mainmenu") || lower.Contains("tron_terminal") || lower.Contains("alita_tron_awakening")
                || IsCutsceneScene(scene.name))
            {
                return;
            }

            // Check if CyberCity HUD canvas already exists
            if (Object.FindAnyObjectByType<CyberpunkHUDController>() != null ||
                GameObject.Find("Canvas_InGameHUD") != null)
            {
                return;
            }

            Debug.Log($"[CyberCityHUD] Initializing Cyberpunk HUD System for '{scene.name}'...");

            // CyberCityStartPointReceiver - ONLY in Demo_City or CyberCity environments
            if (scene.name.IndexOf("Demo_City", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                scene.name.IndexOf("CyberCity", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (Object.FindAnyObjectByType<CyberCityStartPointReceiver>() == null)
                {
                    GameObject receiverObj = new GameObject("CyberCityStartPointReceiver");
                    receiverObj.AddComponent<CyberCityStartPointReceiver>();
                }
            }

            // 4. Create Canvas_InGameHUD with Cyberpunk 2077 Authentic HUD
            GameObject hudCanvasObj = new GameObject("Canvas_InGameHUD");
            Canvas canvas = hudCanvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 15;

            CanvasScaler scaler = hudCanvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            hudCanvasObj.AddComponent<GraphicRaycaster>();

            // Add MinimapController & Cyberpunk 2077 Kiroshi HUD Controller
            MinimapController minimapCtrl = hudCanvasObj.AddComponent<MinimapController>();
            CyberpunkHUDController cyberpunkHUD = hudCanvasObj.AddComponent<CyberpunkHUDController>();

            // --- 5. Verify Pause Canvas Overlay is hooked ---
            if (pauseMgr != null && pauseMgr.pauseCanvasOverlay == null)
            {
                BuildPauseMenu(pauseMgr);
            }
            Debug.Log("[CyberCityHUD] Real-Time Minimap & HUD system initialized successfully.");
        }

        public static GameObject BuildPauseMenu(InGamePauseManager mgr = null)
        {
            if (mgr == null)
            {
                mgr = Object.FindAnyObjectByType<InGamePauseManager>();
                if (mgr == null)
                {
                    GameObject go = new GameObject("InGamePauseManagerController");
                    mgr = go.AddComponent<InGamePauseManager>();
                }
            }

            SettingsManager sMgr = mgr.GetComponent<SettingsManager>();
            if (sMgr == null) sMgr = mgr.gameObject.AddComponent<SettingsManager>();

            ControlsMenuDisplay cDisplay = mgr.GetComponent<ControlsMenuDisplay>();
            if (cDisplay == null) cDisplay = mgr.gameObject.AddComponent<ControlsMenuDisplay>();

            return PauseMenuBuilder.BuildPauseMenu(mgr, sMgr, cDisplay);
        }

        private static GameObject CreateChild(GameObject parent, string name, Vector2 pos, Vector2 size)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);

            RectTransform rect = child.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            return child;
        }

        private static Button CreateButton(GameObject parent, GameObject prefab, string name, string labelText, Vector2 pos, Vector2 size)
        {
            GameObject btnObj = null;
            if (prefab != null)
            {
                btnObj = Object.Instantiate(prefab, parent.transform);
                btnObj.name = name;

                // Remove duplicate template text children from instantiated prefab
                TMP_Text[] existingTmps = btnObj.GetComponentsInChildren<TMP_Text>(true);
                for (int i = 1; i < existingTmps.Length; i++)
                {
                    Object.DestroyImmediate(existingTmps[i].gameObject);
                }
            }
            else
            {
                btnObj = new GameObject(name);
                btnObj.transform.SetParent(parent.transform, false);
                Image img = btnObj.AddComponent<Image>();
                img.color = new Color(0.08f, 0.25f, 0.40f, 0.9f);
                btnObj.AddComponent<Button>();
            }

            RectTransform rect = btnObj.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchoredPosition = pos;
                rect.sizeDelta = size;
            }

            Button button = btnObj.GetComponent<Button>();
            if (button == null) button = btnObj.AddComponent<Button>();

            TMP_Text tmp = btnObj.GetComponentInChildren<TMP_Text>(true);
            if (tmp == null)
            {
                GameObject textObj = new GameObject("Text_Label");
                textObj.transform.SetParent(btnObj.transform, false);
                RectTransform textRect = textObj.AddComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;
                tmp = textObj.AddComponent<TextMeshProUGUI>();
            }

            tmp.text = labelText;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 14;
            tmp.fontSizeMax = 24;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.fontStyle = FontStyles.Bold;

            return button;
        }

        private static TextMeshProUGUI CreateText(GameObject parent, string name, string text, float fontSize, TextAlignmentOptions align, Vector2 pos)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent.transform, false);

            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(800, 60);

            TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = align;
            tmp.color = Color.white;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;

            return tmp;
        }

        private static Slider CreateSlider(GameObject parent, string name, Vector2 pos)
        {
            GameObject sliderObj = new GameObject(name);
            sliderObj.transform.SetParent(parent.transform, false);

            RectTransform rect = sliderObj.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(300, 24);

            Image bg = sliderObj.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.2f, 0.3f, 0.9f);

            GameObject fillArea = CreateChild(sliderObj, "Fill Area", Vector2.zero, Vector2.zero);
            RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;

            GameObject fill = CreateChild(fillArea, "Fill", Vector2.zero, Vector2.zero);
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;

            Image fillImg = fill.AddComponent<Image>();
            fillImg.color = new Color(0.2f, 0.85f, 1f);

            Slider slider = sliderObj.AddComponent<Slider>();
            slider.fillRect = fillRect;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0.8f;

            return slider;
        }

        private static TMP_Dropdown CreateDropdown(GameObject parent, string name, Vector2 pos, Vector2 size)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent.transform, false);

            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            Image img = obj.AddComponent<Image>();
            img.color = new Color(0.06f, 0.16f, 0.28f, 0.95f);

            TMP_Dropdown dropdown = obj.AddComponent<TMP_Dropdown>();
            return dropdown;
        }

        private static Toggle CreateToggle(GameObject parent, string name, Vector2 pos)
        {
            GameObject toggleObj = new GameObject(name);
            toggleObj.transform.SetParent(parent.transform, false);

            RectTransform rect = toggleObj.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(32, 32);

            Image bg = toggleObj.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.2f, 0.3f, 0.9f);

            GameObject checkmark = CreateChild(toggleObj, "Checkmark", Vector2.zero, new Vector2(-6, -6));
            RectTransform checkRect = checkmark.GetComponent<RectTransform>();
            checkRect.anchorMin = Vector2.zero;
            checkRect.anchorMax = Vector2.one;

            Image checkImg = checkmark.AddComponent<Image>();
            checkImg.color = new Color(0.2f, 0.95f, 1f);

            Toggle toggle = toggleObj.AddComponent<Toggle>();
            toggle.graphic = checkImg;
            toggle.targetGraphic = bg;
            toggle.isOn = true;

            return toggle;
        }

        private static void CleanUpSyntyPopupFrame(GameObject popupObj)
        {
            if (popupObj == null) return;
            foreach (Transform child in popupObj.GetComponentsInChildren<Transform>(true))
            {
                if (child == popupObj.transform) continue;
                string cName = child.name.ToUpper();
                if (cName.Contains("TITLE") || cName.Contains("DESC") || cName.Contains("CONFIRM") || cName.Contains("GROUP_BUTTON"))
                {
                    child.gameObject.SetActive(false);
                }
            }
            foreach (TMP_Text txt in popupObj.GetComponentsInChildren<TMP_Text>(true))
            {
                string t = txt.text.ToUpper();
                if (t.Contains("ERROR") || t.Contains("OCCURRED") || t.Contains("RESTART") || t.Contains("CONFIRM"))
                {
                    txt.gameObject.SetActive(false);
                }
            }
        }
    }
}
