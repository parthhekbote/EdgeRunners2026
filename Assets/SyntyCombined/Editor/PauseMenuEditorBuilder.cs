using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace SyntyCombined.Editor
{
    /// <summary>
    /// Editor Utilities for constructing and wiring the AAA Cyberpunk Pause Menu.
    /// Accessible via Tools > Cyberpunk > Build Pause Menu and Tools > Cyberpunk > Wire Pause Refs.
    /// </summary>
    public static class PauseMenuEditorBuilder
    {
        [MenuItem("Tools/Cyberpunk/Build Pause Menu", false, 10)]
        public static void BuildPauseMenuInScene()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            Debug.Log($"[PauseMenuEditor] Building AAA Cyberpunk Pause Menu in '{activeScene.name}'...");

            // 1. Ensure EventSystem exists with InputSystemUIInputModule or StandaloneInputModule
            if (Object.FindAnyObjectByType<EventSystem>() == null)
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
                Undo.RegisterCreatedObjectUndo(eventSys, "Create EventSystem");
            }

            // 2. Remove any old Canvas_PauseMenu if it exists
            InGamePauseManager.DestroyAllPauseCanvasesInScene();

            // 3. Find or Create Controller Manager
            var pauseMgr = Object.FindAnyObjectByType<InGamePauseManager>();
            if (pauseMgr == null)
            {
                GameObject controllerObj = new GameObject("InGamePauseManagerController");
                pauseMgr = controllerObj.AddComponent<InGamePauseManager>();
                Undo.RegisterCreatedObjectUndo(controllerObj, "Create InGamePauseManagerController");
            }

            var settingsMgr = pauseMgr.GetComponent<SettingsManager>();
            if (settingsMgr == null)
            {
                settingsMgr = Undo.AddComponent<SettingsManager>(pauseMgr.gameObject);
            }

            var controlsDisplay = pauseMgr.GetComponent<ControlsMenuDisplay>();
            if (controlsDisplay == null)
            {
                controlsDisplay = Undo.AddComponent<ControlsMenuDisplay>(pauseMgr.gameObject);
            }

            // 4. Build Menu Hierarchy
            GameObject canvasObj = PauseMenuBuilder.BuildPauseMenu(pauseMgr, settingsMgr, controlsDisplay);
            if (canvasObj != null)
            {
                Undo.RegisterCreatedObjectUndo(canvasObj, "Build Canvas_PauseMenu");
            }

            EditorSceneManager.MarkSceneDirty(activeScene);
            Debug.Log("[PauseMenuEditor] AAA Cyberpunk Pause Menu built and wired successfully!");
        }

        [MenuItem("Tools/Cyberpunk/Wire Pause Refs", false, 11)]
        public static void WirePauseRefs()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            var pauseMgr = Object.FindAnyObjectByType<InGamePauseManager>();
            if (pauseMgr == null)
            {
                Debug.LogWarning("[PauseMenuEditor] No InGamePauseManager found in scene to wire.");
                return;
            }

            var canvas = InGamePauseManager.FindPauseCanvasInScene() ?? GameObject.Find("Canvas_InGamePauseOverlay");
            if (canvas == null)
            {
                Debug.LogWarning("[PauseMenuEditor] No pause canvas found in scene.");
                return;
            }

            Undo.RecordObject(pauseMgr, "Wire Pause Refs");
            pauseMgr.pauseCanvasOverlay = canvas;
            pauseMgr.canvasGroup = canvas.GetComponent<CanvasGroup>();

            // Find Panels
            Transform mainPanel = canvas.transform.Find("Panel_PauseMain") ?? canvas.transform.Find("PauseRoot/RightStack/Panel_Home");
            Transform setPanel = canvas.transform.Find("Panel_PauseSettings") ?? canvas.transform.Find("PauseRoot/RightStack/Panel_Settings");
            Transform ctrlPanel = canvas.transform.Find("Panel_PauseControls") ?? canvas.transform.Find("PauseRoot/RightStack/Panel_Controls");

            pauseMgr.pauseMainPanel = mainPanel?.gameObject;
            pauseMgr.settingsPanel = setPanel?.gameObject;
            pauseMgr.controlsPanel = ctrlPanel?.gameObject;

            // Find Left Nav Buttons
            Transform btnStack = canvas.transform.Find("Panel_PauseMain/LeftMenuContainer/ButtonStack") ??
                                 canvas.transform.Find("PauseRoot/LeftNav/ButtonStack");

            Button[] buttons = (btnStack != null) ? btnStack.GetComponentsInChildren<Button>(true) : canvas.GetComponentsInChildren<Button>(true);
            foreach (var b in buttons)
            {
                string bName = b.name.ToUpper();
                if (bName.Contains("RESUME")) pauseMgr.resumeButton = b;
                else if (bName.Contains("SETTING")) pauseMgr.settingsButton = b;
                else if (bName.Contains("CONTROL")) pauseMgr.controlsButton = b;
                else if (bName.Contains("PHOTO")) pauseMgr.photoModeButton = b;
                else if (bName.Contains("RESTART")) pauseMgr.restartButton = b;
                else if (bName.Contains("MAIN") || bName.Contains("HUB")) pauseMgr.mainMenuButton = b;
                else if (bName.Contains("DESKTOP") || bName.Contains("QUIT")) pauseMgr.quitDesktopButton = b;
            }

            // Find Confirm Popup
            Transform popup = canvas.transform.Find("Popup_Confirm") ?? canvas.transform.Find("Popup_SciFiMenus_Quit_Game_01");
            if (popup != null)
            {
                pauseMgr.confirmPopupRoot = popup.gameObject;
                pauseMgr.confirmTitleText = popup.Find("ConfirmBox/Text_Title")?.GetComponent<TMP_Text>();
                pauseMgr.confirmDescText = popup.Find("ConfirmBox/Text_Desc")?.GetComponent<TMP_Text>();
                pauseMgr.confirmYesButton = popup.Find("ConfirmBox/Btn_ConfirmYes")?.GetComponent<Button>();
                pauseMgr.confirmNoButton = popup.Find("ConfirmBox/Btn_ConfirmNo")?.GetComponent<Button>();
            }

            EditorUtility.SetDirty(pauseMgr);
            EditorSceneManager.MarkSceneDirty(activeScene);
            Debug.Log("[PauseMenuEditor] Pause References successfully verified and wired!");
        }

        [MenuItem("Tools/Cyberpunk/Create Master AudioMixer", false, 12)]
        public static void CreateMasterAudioMixer()
        {
            string path = "Assets/Audio/CyberpunkMaster.mixer";
            if (System.IO.File.Exists(path))
            {
                Debug.Log("[PauseMenuEditor] AudioMixer already exists at: " + path);
                return;
            }

            var controllerType = System.Type.GetType("UnityEditor.Audio.AudioMixerController, UnityEditor");
            if (controllerType != null)
            {
                var createMethod = controllerType.GetMethod("CreateMixerControllerAtPath",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (createMethod != null)
                {
                    createMethod.Invoke(null, new object[] { path });
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    Debug.Log("[PauseMenuEditor] Successfully created AudioMixer at: " + path);
                    return;
                }
            }

            Debug.LogWarning("[PauseMenuEditor] Could not invoke CreateMixerControllerAtPath. Audio parameters are managed via AudioListener fallback.");
        }
    }
}
