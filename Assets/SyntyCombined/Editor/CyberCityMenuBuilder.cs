using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.IO;
using System.Collections.Generic;
using Alita;
using Alita.Editor;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;

namespace SyntyCombined.Editor
{
    /// <summary>
    /// Editor tool that builds the aesthetic Alita Scrapyard Start Menu Scene (Scene_MainMenu.unity)
    /// Features:
    /// - 3D Scrapyard environment with broken Alita cyborg, scrap piles, Zalem sunbeam, and floating dust motes
    /// - Cinematic Camera with continuous low-angle orbit and raycast obstruction avoidance
    /// - Reactive Cyberpunk HUD overlay with animated ECG heartbeat monitor, telemetry, and scanlines
    /// - Procedural sci-fi UI sounds and interactive hover states
    /// - Awakening sequence with Cutscene Hook before launching Cyber City (Demo_City)
    /// - Full integration with Tactical Sector Select, System Settings, and Combat Archives panels
    /// </summary>
    [InitializeOnLoad]
    public static class CyberCityMenuBuilder
    {
        static CyberCityMenuBuilder()
        {
            EditorApplication.delayCall += AutoBuildIfNotExists;
        }

        private static void AutoBuildIfNotExists()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorPrefs.GetBool("AlitaScrapyardMenuBuilt_v4", false))
            {
                EditorPrefs.SetBool("AlitaScrapyardMenuBuilt_v4", true);
                BuildMainMenuScene();
            }
        }

        [MenuItem("CyberCity/Build AAA Start Menu Hub Scene", false, 10)]
        [MenuItem("Tools/Alita Battle Angel/Build Alita Scrapyard Start Menu", false, 1)]
        public static void BuildMainMenuScene()
        {
            string scenesFolder = "Assets/Scenes";
            if (!Directory.Exists(scenesFolder))
            {
                Directory.CreateDirectory(scenesFolder);
            }

            string scenePath = Path.Combine(scenesFolder, "Scene_MainMenu.unity");
            UnityEngine.SceneManagement.Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ==================== 1. 3D SCRAPYARD ENVIRONMENT ====================
            AlitaScrapyardSceneSetup.BuildScrapyardEnvironment();

            // ==================== 2. BROKEN ALITA CYBORG ====================
            GameObject alitaGo = AlitaScrapyardSceneSetup.BuildBrokenAlitaCyborg();
            AlitaBrokenCyborg alitaCyborg = alitaGo.GetComponent<AlitaBrokenCyborg>();
            if (alitaCyborg != null)
            {
                alitaCyborg.showLegacyGUI = false; // Disable legacy debug GUI in favor of modern Cyberpunk Start Menu HUD
            }

            // ==================== 3. CINEMATIC ORBIT CAMERA WITH OBSTRUCTION AVOIDANCE ====================
            GameObject cameraObj = new GameObject("Main Camera");
            cameraObj.tag = "MainCamera";
            Camera cam = cameraObj.AddComponent<Camera>();
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.1f;
            cam.clearFlags = CameraClearFlags.Skybox;
            cameraObj.AddComponent<AudioListener>();

            AlitaCinematicCamera cineCam = cameraObj.AddComponent<AlitaCinematicCamera>();
            cineCam.target = alitaGo.transform;
            cineCam.lookOffset = new Vector3(0.0f, 0.35f, 0.0f);
            cineCam.screenFramingOffset = new Vector3(0.12f, 0f, 0f);
            cineCam.baseDistance = 1.95f;
            cineCam.minAllowedDistance = 0.85f;
            cineCam.orbitSpeed = 5.2f;
            cineCam.basePitch = 13.5f;
            cineCam.pitchAmplitude = 3.5f;
            cineCam.verticalBobAmplitude = 0.05f;
            cineCam.enableObstructionAvoidance = true;
            cineCam.obstacleSphereRadius = 0.18f;
            cineCam.collisionSafetyBuffer = 0.22f;
            cineCam.enableMouseParallax = true;
            cineCam.mouseParallaxStrength = 3.2f;

            // ==================== 4. ATMOSPHERIC LIGHTING & ZALEM SUNBEAM ====================
            AlitaScrapyardSceneSetup.SetupScrapyardLighting(alitaGo.transform.position);

            // ==================== 5. EVENT SYSTEM ====================
            GameObject eventSys = new GameObject("EventSystem");
            eventSys.AddComponent<EventSystem>();
            var inputSysType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputSysType != null) eventSys.AddComponent(inputSysType);
            else eventSys.AddComponent<StandaloneInputModule>();

            // ==================== 6. START MENU CANVAS ====================
            GameObject canvasObj = new GameObject("Canvas_MainMenuHub");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();

            // Load Synty's Prefabs for secondary screens
            GameObject settingsScreenPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/InterfaceSciFiMenus/Prefabs/Screens/Screen_SciFiMenus_Settings_01.prefab");
            GameObject controlsPanelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/InterfaceSciFiMenus/Prefabs/Panels/Panel_SciFiMenus_Input_01.prefab");
            GameObject loadingScreenPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/InterfaceSciFiMenus/Prefabs/Screens/Screen_SciFiMenus_Loading_01.prefab");
            GameObject popupPanelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/InterfaceSciFiMenus/Prefabs/Panels/Panel_SciFiMenus_Popup_01.prefab");
            GameObject syntyBtnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/InterfaceSciFiMenus/Prefabs/Buttons/Button_SciFiMenus_Menu_Item_01.prefab");

            // ==================== 7. AESTHETIC CYBERPUNK START MENU HUD ====================
            GameObject mainHubObj = CreateChild(canvasObj, "Panel_AlitaStartHub", Vector2.zero, Vector2.zero);
            StretchFull(mainHubObj.GetComponent<RectTransform>());
            CanvasGroup mainHubCg = mainHubObj.AddComponent<CanvasGroup>();

            // Ambient Scanlines Overlay
            GameObject scanlineGo = CreateChild(mainHubObj, "Scanline_Overlay", Vector2.zero, Vector2.zero);
            StretchFull(scanlineGo.GetComponent<RectTransform>());
            RawImage scanlineRaw = scanlineGo.AddComponent<RawImage>();
            scanlineRaw.color = new Color(0f, 0.85f, 1f, 0.08f);
            scanlineRaw.raycastTarget = false;

            // --- Top HUD Bar ---
            GameObject topBar = CreateFallbackPanel(mainHubObj, "HUD_TopBar", new Vector2(0, 500), new Vector2(1920, 60), new Color(0.01f, 0.04f, 0.08f, 0.85f));
            CreateBorderLine(topBar, new Vector2(0, -30), new Vector2(1920, 2), new Color(0f, 0.85f, 1f, 0.5f));
            CreateText(topBar, "Text_TopLeft", "MARS TECHNARCHY // SECTOR 07 SCRAP CANAL [ZALEM JUNK CHUTE 04]", 15, TextAlignmentOptions.MidlineLeft, new Vector2(40, 0), new Vector2(850, 40), false, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
            CreateText(topBar, "Text_TopRight", "GRID: 34.82° N, 118.24° W   |   ELEV: -142M   |   STATUS: SUSPENDED ANIMATION", 14, TextAlignmentOptions.MidlineRight, new Vector2(-40, 0), new Vector2(850, 40), false, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));

            // --- Bottom HUD Bar ---
            GameObject btmBar = CreateFallbackPanel(mainHubObj, "HUD_BottomBar", new Vector2(0, -500), new Vector2(1920, 50), new Color(0.01f, 0.04f, 0.08f, 0.85f));
            CreateBorderLine(btmBar, new Vector2(0, 25), new Vector2(1920, 2), new Color(0f, 0.85f, 1f, 0.5f));
            CreateText(btmBar, "Text_BtmLeft", "URM MINIATURE ANTIMATTER CORE // POWER OUTPUT: 87.4% NOMINAL", 14, TextAlignmentOptions.MidlineLeft, new Vector2(40, 0), new Vector2(850, 40), false, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
            CreateText(btmBar, "Text_BtmRight", "[ READY FOR AWAKENING — CUTSCENE HOOK ARMED ]", 14, TextAlignmentOptions.MidlineRight, new Vector2(-40, 0), new Vector2(850, 40), false, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));

            // --- Left Action & Title Panel ---
            // Width: 620, Height: 800. Center: (-540, 10). Left edge is -310 relative to center, right is +310.
            GameObject leftPanel = CreateFallbackPanel(mainHubObj, "Panel_LeftActions", new Vector2(-540, 10), new Vector2(620, 800), new Color(0.01f, 0.04f, 0.08f, 0.82f));
            CreateBorderLine(leftPanel, new Vector2(-308, 0), new Vector2(4, 800), new Color(0f, 0.95f, 1f, 0.9f)); // Left cyan accent
            CreateBorderLine(leftPanel, new Vector2(0, 398), new Vector2(620, 2), new Color(0f, 0.85f, 1f, 0.4f));
            CreateBorderLine(leftPanel, new Vector2(0, -398), new Vector2(620, 2), new Color(0f, 0.85f, 1f, 0.4f));

            // All content inside Left Panel has width 550, centered at X = 5.
            // Left boundary is at 5 - 275 = -270 (comfortably inside the box, 38px from the left cyan accent line).
            // Right boundary is at 5 + 275 = +280 (30px inside right border).

            // Sub-Badge
            var txtBadge = CreateText(leftPanel, "Text_ProtocolBadge", "[ MARS BERSERKER RECOVERY PROTOCOL ]", 15, TextAlignmentOptions.MidlineLeft, new Vector2(5, 345), new Vector2(550, 32));
            txtBadge.color = new Color(0f, 0.85f, 1f, 0.9f);

            // Movie Title
            var txtTitle = CreateText(leftPanel, "Text_MovieTitle", "ALITA: BATTLE ANGEL", 40, TextAlignmentOptions.MidlineLeft, new Vector2(5, 290), new Vector2(550, 56));
            txtTitle.color = new Color(0.95f, 0.98f, 1.0f);
            txtTitle.fontStyle = FontStyles.Bold;
            txtTitle.enableAutoSizing = true;
            txtTitle.fontSizeMin = 26;
            txtTitle.fontSizeMax = 40;

            // Subtitle
            var txtSub = CreateText(leftPanel, "Text_Subtitle", "SCRAPYARD DISCOVERY // THE AWAKENING", 17, TextAlignmentOptions.MidlineLeft, new Vector2(5, 240), new Vector2(550, 30));
            txtSub.color = new Color(1.0f, 0.84f, 0.0f, 0.95f); // Neon Gold

            // Primary Awakening Button (Prominent & Eye-Catching)
            Button btnAwakening = CreateCyberButton(leftPanel, "Btn_StartAwakening", "[ 01 ]  INITIALIZE AWAKENING", new Vector2(5, 120), new Vector2(550, 72), 23, true);

            // Secondary Navigation Buttons
            Button btnSectorMap = CreateCyberButton(leftPanel, "Btn_SectorMap", "[ 02 ]  TACTICAL SECTOR DISPATCH", new Vector2(5, 35), new Vector2(550, 56), 19, false);
            Button btnSettings = CreateCyberButton(leftPanel, "Btn_Settings", "[ 03 ]  SYSTEM CONFIGURATION", new Vector2(5, -45), new Vector2(550, 56), 19, false);
            Button btnControls = CreateCyberButton(leftPanel, "Btn_Controls", "[ 04 ]  COMBAT OPERATIONAL ARCHIVE", new Vector2(5, -125), new Vector2(550, 56), 19, false);
            Button btnQuit = CreateCyberButton(leftPanel, "Btn_Quit", "[ 05 ]  ABORT / EXIT SYSTEM", new Vector2(5, -205), new Vector2(550, 56), 19, false);

            // Tip footer
            var txtTip = CreateText(leftPanel, "Text_ControlTip", "INTERACTION: Mouse Orbit Parallax Enabled | Press Start to Awaken", 13, TextAlignmentOptions.MidlineLeft, new Vector2(5, -320), new Vector2(550, 30));
            txtTip.color = new Color(0.5f, 0.7f, 0.85f, 0.7f);

            // --- Right Holographic Telemetry Card ---
            // Width: 540, Height: 800. Bounds: X: [-270, +270]. Right cyan accent at +268.
            GameObject rightPanel = CreateFallbackPanel(mainHubObj, "Panel_RightTelemetry", new Vector2(580, 10), new Vector2(540, 800), new Color(0.01f, 0.04f, 0.08f, 0.82f));
            CreateBorderLine(rightPanel, new Vector2(268, 0), new Vector2(4, 800), new Color(0f, 0.95f, 1f, 0.9f)); // Right cyan accent
            CreateBorderLine(rightPanel, new Vector2(0, 398), new Vector2(540, 2), new Color(0f, 0.85f, 1f, 0.4f));
            CreateBorderLine(rightPanel, new Vector2(0, -398), new Vector2(540, 2), new Color(0f, 0.85f, 1f, 0.4f));

            // All content inside Right Panel has width 480, centered at X = -5.
            // Bounds: [-245, +235]. Padded 25px from left edge and 33px from right accent line.

            var txtDiagHeader = CreateText(rightPanel, "Text_DiagHeader", "// URM CYBERNETIC DIAGNOSTIC", 22, TextAlignmentOptions.MidlineLeft, new Vector2(-5, 345), new Vector2(480, 36));
            txtDiagHeader.color = new Color(0f, 0.95f, 1.0f);
            txtDiagHeader.fontStyle = FontStyles.Bold;

            var txtLocHeader = CreateText(rightPanel, "Text_LocHeader", "SUBJECT RECOVERY: ZALEM CHUTE 04", 15, TextAlignmentOptions.MidlineLeft, new Vector2(-5, 312), new Vector2(480, 26));
            txtLocHeader.color = new Color(0.6f, 0.75f, 0.9f);

            // ECG Heartbeat Monitor Box
            GameObject ecgBox = CreateFallbackPanel(rightPanel, "Box_ECGMonitor", new Vector2(-5, 210), new Vector2(480, 110), new Color(0.02f, 0.07f, 0.12f, 0.95f));
            CreateBorderLine(ecgBox, new Vector2(0, 54), new Vector2(480, 2), new Color(0f, 0.85f, 1f, 0.6f));
            CreateBorderLine(ecgBox, new Vector2(0, -54), new Vector2(480, 2), new Color(0f, 0.85f, 1f, 0.6f));

            GameObject ecgRawGo = CreateChild(ecgBox, "RawImage_ECGWave", Vector2.zero, new Vector2(460, 90));
            RawImage ecgRaw = ecgRawGo.AddComponent<RawImage>();

            // Live Pulse Status Text
            var txtPulseStatus = CreateText(rightPanel, "Text_PulseStatus", "> [URM CORE PULSE: HARMONIC DETECTED]", 16, TextAlignmentOptions.MidlineLeft, new Vector2(-5, 125), new Vector2(480, 30));
            txtPulseStatus.color = new Color(0.2f, 1.0f, 0.5f);
            txtPulseStatus.fontStyle = FontStyles.Bold;

            // Telemetry Specs
            string specs = "• CLASSIFICATION: Mars Technarchy URM Berserker\n" +
                           "• POWER CORE: Miniature Antimatter Reactor [ACTIVE]\n" +
                           "• NEOCORTEX: Human Brain 100% Viable & Functional\n" +
                           "• CHASSIS: Severe Detachment / Scrap Corrosion\n" +
                           "• SPINE: Exposed Vertebrae Segment Assembly Intact\n" +
                           "• RESCUE RECOVERY: Dr. Dyson Ido [Scrapyard Sector 07]";
            var txtSpecs = CreateText(rightPanel, "Text_TelemetrySpecs", specs, 15, TextAlignmentOptions.TopLeft, new Vector2(-5, -10), new Vector2(480, 190), true);
            txtSpecs.color = new Color(0.85f, 0.92f, 0.98f);
            txtSpecs.lineSpacing = 12f;

            // Quote Box
            GameObject quoteBox = CreateFallbackPanel(rightPanel, "Box_Quote", new Vector2(-5, -185), new Vector2(480, 115), new Color(0.02f, 0.08f, 0.14f, 0.9f));
            CreateBorderLine(quoteBox, new Vector2(-236, 0), new Vector2(4, 115), new Color(1f, 0.84f, 0f, 0.9f));
            var txtQuote = CreateText(quoteBox, "Text_Quote", "Dr. Dyson Ido:\n\"Her brain is intact... and her heart is still beating. 300 years old, and it has enough power to run all of Iron City.\"", 14, TextAlignmentOptions.TopLeft, new Vector2(12, 0), new Vector2(440, 100), true);
            txtQuote.color = new Color(1.0f, 0.9f, 0.65f);
            txtQuote.fontStyle = FontStyles.Italic;

            // Glitch Overlay for transition
            GameObject glitchGo = CreateChild(mainHubObj, "Glitch_Overlay", Vector2.zero, Vector2.zero);
            StretchFull(glitchGo.GetComponent<RectTransform>());
            CanvasGroup glitchCg = glitchGo.AddComponent<CanvasGroup>();
            glitchCg.alpha = 0f;
            glitchCg.blocksRaycasts = false;
            glitchCg.interactable = false;
            Image glitchImg = glitchGo.AddComponent<Image>();
            glitchImg.color = new Color(0f, 0.9f, 1f, 0.35f);
            glitchImg.raycastTarget = false;

            // Attach & Configure AlitaStartMenuHUD
            AlitaStartMenuHUD startHUD = mainHubObj.AddComponent<AlitaStartMenuHUD>();
            startHUD.alitaCyborg = alitaCyborg;
            startHUD.alitaCamera = cineCam;
            startHUD.hudCanvasGroup = mainHubCg;
            startHUD.mainHubContainer = mainHubObj;
            startHUD.glitchOverlayGroup = glitchCg;
            startHUD.btnStartAwakening = btnAwakening;
            startHUD.btnSectorSelect = btnSectorMap;
            startHUD.btnSettings = btnSettings;
            startHUD.btnControls = btnControls;
            startHUD.btnQuit = btnQuit;
            startHUD.ecgMonitorImage = ecgRaw;
            startHUD.ecgPulseStatusText = txtPulseStatus;
            startHUD.scanlineImage = scanlineRaw;

            // ==================== 8. SECONDARY PANELS ====================
            // --- A. Sector Select Panel ---
            GameObject sectorPanelObj = CreateFallbackPanel(canvasObj, "Panel_SectorSelect");
            if (popupPanelPrefab != null)
            {
                GameObject frame = (GameObject)PrefabUtility.InstantiatePrefab(popupPanelPrefab, sectorPanelObj.transform);
                frame.name = "Synty_SectorFrame";
                RectTransform rt = frame.GetComponent<RectTransform>();
                if (rt) { rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f); rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(1600, 900); }
                CleanUpSyntyPopupFrame(frame);
            }

            CreateText(sectorPanelObj, "Text_Header", "TACTICAL MAP OVERVIEW // CYBER CITY", 34, TextAlignmentOptions.MidlineLeft, new Vector2(-280, 380), new Vector2(800, 50));
            Button btnBackSector = CreateSyntyButton(sectorPanelObj, syntyBtnPrefab, "Btn_BackSector", "< BACK TO HUB", new Vector2(-550, 380), new Vector2(220, 50));

            GameObject mapGraphicBox = CreateFallbackPanel(sectorPanelObj, "Box_MapGraphic", new Vector2(-380, -20), new Vector2(750, 560));
            GameObject mapImgObj = CreateChild(mapGraphicBox, "Image_MapPreview", Vector2.zero, new Vector2(730, 540));
            Image imgMapPreview = mapImgObj.AddComponent<Image>();
            imgMapPreview.sprite = CyberCityMapAssets.GetSectorMapSprite(0);

            Button pinPlaza = CreateSyntyButton(mapGraphicBox, syntyBtnPrefab, "Pin_Plaza", "[01] PLAZA", new Vector2(-180, 100), new Vector2(160, 50));
            Button pinAlley = CreateSyntyButton(mapGraphicBox, syntyBtnPrefab, "Pin_Alley", "[02] ALLEY", new Vector2(150, -100), new Vector2(160, 50));
            Button pinTower = CreateSyntyButton(mapGraphicBox, syntyBtnPrefab, "Pin_Tower", "[03] TOWER", new Vector2(0, 180), new Vector2(160, 50));

            GameObject cardBox = CreateFallbackPanel(sectorPanelObj, "Card_SectorDetails", new Vector2(400, -20), new Vector2(650, 560));
            TMP_Dropdown dropdownSector = CreateDropdown(cardBox, "Dropdown_Sector", new Vector2(0, 220), new Vector2(580, 50));
            TextMeshProUGUI txtSectorTitle = CreateText(cardBox, "Text_SectorTitle", "SECTOR 01 // MAIN CYBER PLAZA", 28, TextAlignmentOptions.MidlineLeft, new Vector2(0, 140), new Vector2(580, 45));
            txtSectorTitle.color = new Color(0.2f, 0.85f, 1f);

            TextMeshProUGUI txtThreatLevel = CreateText(cardBox, "Text_ThreatLevel", "THREAT LEVEL: LOW (CIVILIAN HUB)", 19, TextAlignmentOptions.MidlineLeft, new Vector2(0, 95), new Vector2(580, 35));
            txtThreatLevel.color = new Color(1f, 0.85f, 0.2f);

            TextMeshProUGUI txtCoords = CreateText(cardBox, "Text_Coords", "GRID: 34.82 N // 118.24 W", 17, TextAlignmentOptions.MidlineLeft, new Vector2(0, 60), new Vector2(580, 30));
            txtCoords.color = new Color(0.6f, 0.7f, 0.8f);

            TextMeshProUGUI txtDesc = CreateText(cardBox, "Text_Desc", "Central urban hub with neutral civilian traffic and primary dueling arena.", 18, TextAlignmentOptions.TopLeft, new Vector2(0, -5), new Vector2(580, 70), true);

            TextMeshProUGUI txtPoi = CreateText(cardBox, "Text_Poi", "POINTS OF INTEREST:\n• Central Market Square\n• Duelist Training Arena\n• Skyway Transit Terminal", 17, TextAlignmentOptions.TopLeft, new Vector2(0, -105), new Vector2(580, 100), true);
            txtPoi.color = new Color(0.7f, 0.9f, 1f);

            Button btnLaunchSector = CreateSyntyButton(cardBox, syntyBtnPrefab, "Btn_LaunchSector", "DEPLOY TO SECTOR", new Vector2(0, -210), new Vector2(400, 60));

            // --- B. Settings Screen ---
            GameObject settingsObj = null;
            if (settingsScreenPrefab != null)
            {
                settingsObj = (GameObject)PrefabUtility.InstantiatePrefab(settingsScreenPrefab, canvasObj.transform);
                settingsObj.name = "Panel_Settings";
            }
            else
            {
                settingsObj = CreateFallbackPanel(canvasObj, "Panel_Settings");
            }

            Button btnBackSettings = FindButton(settingsObj, "Button_Back") ?? FindButton(settingsObj, "Back") ?? FindButton(settingsObj, "Close");
            if (btnBackSettings == null)
            {
                btnBackSettings = CreateSyntyButton(settingsObj, syntyBtnPrefab, "Btn_BackSettings", "BACK TO HUB", new Vector2(0, -420), new Vector2(250, 50));
            }

            // --- C. Controls Screen ---
            GameObject controlsObj = null;
            if (controlsPanelPrefab != null)
            {
                controlsObj = (GameObject)PrefabUtility.InstantiatePrefab(controlsPanelPrefab, canvasObj.transform);
                controlsObj.name = "Panel_Controls";
            }
            else
            {
                controlsObj = CreateFallbackPanel(canvasObj, "Panel_Controls");
            }

            Button btnBackControls = FindButton(controlsObj, "Button_Back") ?? FindButton(controlsObj, "Back") ?? FindButton(controlsObj, "Close");
            if (btnBackControls == null)
            {
                btnBackControls = CreateSyntyButton(controlsObj, syntyBtnPrefab, "Btn_BackControls", "BACK TO HUB", new Vector2(0, -420), new Vector2(250, 50));
            }

            Button btnKeyTab = FindButton(controlsObj, "Keyboard") ?? CreateSyntyButton(controlsObj, syntyBtnPrefab, "Btn_TabKeyboard", "KEYBOARD & MOUSE", new Vector2(-250, 320), new Vector2(240, 45));
            Button btnXboxTab = FindButton(controlsObj, "Xbox") ?? CreateSyntyButton(controlsObj, syntyBtnPrefab, "Btn_TabXbox", "XBOX CONTROLLER", new Vector2(0, 320), new Vector2(240, 45));
            Button btnPSTab = FindButton(controlsObj, "PlayStation") ?? CreateSyntyButton(controlsObj, syntyBtnPrefab, "Btn_TabPS", "PLAYSTATION", new Vector2(250, 320), new Vector2(240, 45));

            GameObject kbDiagram = CreateFallbackPanel(controlsObj, "Diagram_Keyboard", new Vector2(0, -20), new Vector2(850, 400));
            GameObject xboxDiagram = CreateFallbackPanel(controlsObj, "Diagram_Xbox", new Vector2(0, -20), new Vector2(850, 400));
            GameObject psDiagram = CreateFallbackPanel(controlsObj, "Diagram_PS", new Vector2(0, -20), new Vector2(850, 400));

            TextMeshProUGUI kbText = CreateText(kbDiagram, "Text_KBDetails",
                "KEYBOARD COMBAT CONTROLS:\n\n" +
                "• WASD - Player Locomotion / Movement\n" +
                "• Left Mouse Click - Light & Heavy Sword Attacks\n" +
                "• Right Mouse Click - Guard / Blade Parry\n" +
                "• Spacebar - Dash / Evade Roll\n" +
                "• Shift - Target Lock-On Toggle\n" +
                "• ESC Key - Pause Menu & In-Game Hub",
                20, TextAlignmentOptions.TopLeft, Vector2.zero, new Vector2(780, 350), true);
            kbText.color = Color.white;

            TextMeshProUGUI xboxText = CreateText(xboxDiagram, "Text_XboxDetails",
                "XBOX CONTROLLER SCHEME:\n\n" +
                "• Left Stick - Locomotion\n" +
                "• Right Stick - Camera Direction\n" +
                "• X / RB - Light / Heavy Sword Strike\n" +
                "• LB / LT - Defensive Guard & Parry\n" +
                "• A Button - Dash / Dodge\n" +
                "• RS Click - Lock-on Target\n" +
                "• Menu Button - Pause Game",
                20, TextAlignmentOptions.TopLeft, Vector2.zero, new Vector2(780, 350), true);
            xboxText.color = Color.white;

            TextMeshProUGUI psText = CreateText(psDiagram, "Text_PSDetails",
                "PLAYSTATION CONTROLLER SCHEME:\n\n" +
                "• Left Stick - Locomotion\n" +
                "• Right Stick - Camera Direction\n" +
                "• Square / R1 - Light / Heavy Sword Strike\n" +
                "• L1 / L2 - Defensive Guard & Parry\n" +
                "• Cross (X) - Dash / Dodge\n" +
                "• R3 Click - Lock-on Target\n" +
                "• Options Button - Pause Game",
                20, TextAlignmentOptions.TopLeft, Vector2.zero, new Vector2(780, 350), true);
            psText.color = Color.white;

            // --- D. Loading Screen ---
            GameObject loadingObj = null;
            if (loadingScreenPrefab != null)
            {
                loadingObj = (GameObject)PrefabUtility.InstantiatePrefab(loadingScreenPrefab, canvasObj.transform);
                loadingObj.name = "Panel_LoadingScreen";
            }
            else
            {
                loadingObj = CreateFallbackPanel(canvasObj, "Panel_LoadingScreen");
            }

            Slider loadingBar = loadingObj.GetComponentInChildren<Slider>(true);
            TMP_Text loadingPercentText = FindText(loadingObj, "Percent") ?? FindText(loadingObj, "Progress");
            TMP_Text loadingStatusText = FindText(loadingObj, "Status") ?? FindText(loadingObj, "Tip");

            // ==================== 9. HUB MANAGER OBJECT ====================
            GameObject hubManagerObj = new GameObject("MainMenuManager");
            MainMenuController mainController = hubManagerObj.AddComponent<MainMenuController>();
            SettingsManager settingsManager = hubManagerObj.AddComponent<SettingsManager>();
            ControlsMenuDisplay controlsDisplay = hubManagerObj.AddComponent<ControlsMenuDisplay>();

            // Wire MainMenuController
            mainController.mainHubPanel = mainHubObj;
            mainController.sectorSelectPanel = sectorPanelObj;
            mainController.settingsPanel = settingsObj;
            mainController.controlsPanel = controlsObj;
            mainController.loadingScreenPanel = loadingObj;

            mainController.quickStartButton = btnAwakening;
            mainController.sectorSelectNavButton = btnSectorMap;
            mainController.settingsNavButton = btnSettings;
            mainController.controlsNavButton = btnControls;
            mainController.exitNavButton = btnQuit;

            mainController.backFromSectorButton = btnBackSector;
            mainController.backFromSettingsButton = btnBackSettings;
            mainController.backFromControlsButton = btnBackControls;

            mainController.launchSectorButton = btnLaunchSector;
            mainController.sectorDropdown = dropdownSector;
            mainController.sectorNameTitleText = txtSectorTitle;
            mainController.sectorThreatLevelText = txtThreatLevel;
            mainController.sectorCoordinatesText = txtCoords;
            mainController.sectorDescriptionText = txtDesc;
            mainController.sectorPoiListText = txtPoi;
            mainController.sectorPreviewImage = imgMapPreview;
            mainController.sectorNodePlazaButton = pinPlaza;
            mainController.sectorNodeAlleyButton = pinAlley;
            mainController.sectorNodeTowerButton = pinTower;

            mainController.loadingProgressBar = loadingBar;
            mainController.loadingPercentText = loadingPercentText;
            mainController.loadingStatusText = loadingStatusText;

            // Wire Controls
            controlsDisplay.keyboardDiagramPanel = kbDiagram;
            controlsDisplay.xboxDiagramPanel = xboxDiagram;
            controlsDisplay.playstationDiagramPanel = psDiagram;
            controlsDisplay.keyboardTabButton = btnKeyTab;
            controlsDisplay.xboxTabButton = btnXboxTab;
            controlsDisplay.playstationTabButton = btnPSTab;

            // Wire Settings
            settingsManager.masterVolumeSlider = settingsObj.GetComponentInChildren<Slider>(true);
            settingsManager.qualityDropdown = FindDropdown(settingsObj, "Quality") ?? settingsObj.GetComponentInChildren<TMP_Dropdown>(true);
            settingsManager.fpsCapDropdown = FindDropdown(settingsObj, "FPS") ?? FindDropdown(settingsObj, "Frame");
            settingsManager.fullscreenToggle = settingsObj.GetComponentInChildren<Toggle>(true);

            // Initial visibility state
            sectorPanelObj.SetActive(false);
            settingsObj.SetActive(false);
            controlsObj.SetActive(false);
            loadingObj.SetActive(false);
            mainHubObj.SetActive(true);

            // Save Scene & Add to Build Settings
            EditorSceneManager.SaveScene(newScene, scenePath);
            AddScenesToBuildSettings(scenePath, "Assets/PolygonCyberCity/Scenes/Demo_City.unity");

            Debug.Log($"[CyberCityMenuBuilder] Successfully built and saved aesthetic Alita Scrapyard Start Menu to {scenePath}");
        }

        private static void AddScenesToBuildSettings(string mainMenuPath, string gameplayPath)
        {
            List<string> targetScenes = new List<string>
            {
                mainMenuPath,
                gameplayPath,
                "Assets/Scenes/InsaneGun_Sword_Showcase.unity",
                "Assets/Scenes/SyntyCombatDemo.unity",
                "Assets/Scenes/Scene_Alita_Scrapyard_Discovery.unity"
            };

            List<EditorBuildSettingsScene> buildScenes = new List<EditorBuildSettingsScene>();
            var existing = EditorBuildSettings.scenes;
            HashSet<string> addedPaths = new HashSet<string>();

            foreach (string p in targetScenes)
            {
                if (File.Exists(p) && !addedPaths.Contains(p))
                {
                    buildScenes.Add(new EditorBuildSettingsScene(p, true));
                    addedPaths.Add(p);
                }
            }

            if (existing != null)
            {
                foreach (var s in existing)
                {
                    if (s != null && !string.IsNullOrEmpty(s.path) && !addedPaths.Contains(s.path) && File.Exists(s.path))
                    {
                        buildScenes.Add(s);
                        addedPaths.Add(s.path);
                    }
                }
            }

            EditorBuildSettings.scenes = buildScenes.ToArray();
            Debug.Log($"[CyberCityMenuBuilder] Build settings updated with {buildScenes.Count} scenes.");
        }

        private static Button CreateCyberButton(GameObject parent, string name, string labelText, Vector2 pos, Vector2 size, float fontSize, bool isPrimary)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent.transform, false);

            RectTransform rect = btnObj.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            Image img = btnObj.AddComponent<Image>();
            Color bgColor = isPrimary 
                ? new Color(0.04f, 0.20f, 0.32f, 0.95f) 
                : new Color(0.02f, 0.08f, 0.16f, 0.90f);
            img.color = bgColor;

            // Border outline
            Color borderColor = isPrimary ? new Color(0f, 0.95f, 1f, 0.9f) : new Color(0f, 0.75f, 0.95f, 0.5f);
            CreateBorderLine(btnObj, new Vector2(0, size.y * 0.5f - 1), new Vector2(size.x, 2), borderColor);
            CreateBorderLine(btnObj, new Vector2(0, -size.y * 0.5f + 1), new Vector2(size.x, 2), borderColor);
            CreateBorderLine(btnObj, new Vector2(-size.x * 0.5f + 1, 0), new Vector2(2, size.y), borderColor);
            CreateBorderLine(btnObj, new Vector2(size.x * 0.5f - 1, 0), new Vector2(2, size.y), borderColor);

            // Left Neon Bracket Marker
            Color markerColor = isPrimary ? new Color(1f, 0.84f, 0f, 1f) : new Color(0f, 0.95f, 1f, 0.8f);
            CreateBorderLine(btnObj, new Vector2(-size.x * 0.5f + 4, 0), new Vector2(6, size.y * 0.7f), markerColor);

            Button btn = btnObj.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.2f, 1.2f, 1.2f);
            cb.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            btn.colors = cb;

            // Button Text
            GameObject txtObj = new GameObject("Text");
            txtObj.transform.SetParent(btnObj.transform, false);
            RectTransform txtRt = txtObj.AddComponent<RectTransform>();
            txtRt.anchoredPosition = new Vector2(16, 0);
            txtRt.sizeDelta = new Vector2(size.x - 32, size.y);

            TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
            tmp.text = labelText;
            tmp.fontSize = fontSize;
            tmp.fontStyle = isPrimary ? FontStyles.Bold : FontStyles.Normal;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.color = isPrimary ? new Color(0f, 0.95f, 1f) : Color.white;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.raycastTarget = false;

            return btn;
        }

        private static GameObject CreateBorderLine(GameObject parent, Vector2 pos, Vector2 size, Color color)
        {
            GameObject line = new GameObject("Border");
            line.transform.SetParent(parent.transform, false);
            RectTransform rt = line.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            Image img = line.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return line;
        }

        private static void StretchFull(RectTransform rect)
        {
            if (rect == null) return;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
        }

        private static GameObject CreateFallbackPanel(GameObject parent, string name, Vector2 pos = default, Vector2 size = default, Color? color = null)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent.transform, false);

            RectTransform rect = panel.AddComponent<RectTransform>();
            if (size == Vector2.zero)
            {
                StretchFull(rect);
            }
            else
            {
                rect.anchoredPosition = pos;
                rect.sizeDelta = size;
            }

            Image img = panel.AddComponent<Image>();
            img.color = color ?? new Color(0.02f, 0.04f, 0.08f, 0.92f);
            return panel;
        }

        private static Button CreateSyntyButton(GameObject parent, GameObject prefab, string name, string labelText, Vector2 pos, Vector2 size)
        {
            GameObject btnObj = null;
            if (prefab != null)
            {
                btnObj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
                btnObj.name = name;
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
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = pos;
                rect.sizeDelta = size;
            }

            Button button = btnObj.GetComponent<Button>();
            if (button == null) button = btnObj.AddComponent<Button>();

            SetButtonLabel(button, labelText);
            return button;
        }

        private static void SetButtonLabel(Button btn, string labelText)
        {
            if (btn == null) return;
            TMP_Text tmp = btn.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null)
            {
                tmp.text = labelText;
                tmp.enableAutoSizing = true;
                tmp.fontSizeMin = 12;
                tmp.fontSizeMax = 28;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.overflowMode = TextOverflowModes.Overflow;
                tmp.alignment = TextAlignmentOptions.Center;
            }
            else
            {
                Text txt = btn.GetComponentInChildren<Text>(true);
                if (txt != null)
                {
                    txt.text = labelText;
                    txt.alignment = TextAnchor.MiddleCenter;
                }
            }
        }

        private static Button FindButton(GameObject root, string containsName)
        {
            if (root == null) return null;
            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            foreach (Button b in buttons)
            {
                if (b.gameObject.name.IndexOf(containsName, System.StringComparison.OrdinalIgnoreCase) >= 0) return b;
            }
            return null;
        }

        private static TMP_Dropdown FindDropdown(GameObject root, string containsName)
        {
            if (root == null) return null;
            TMP_Dropdown[] dropdowns = root.GetComponentsInChildren<TMP_Dropdown>(true);
            foreach (TMP_Dropdown d in dropdowns)
            {
                if (d.gameObject.name.IndexOf(containsName, System.StringComparison.OrdinalIgnoreCase) >= 0) return d;
            }
            return null;
        }

        private static TMP_Text FindText(GameObject root, string containsName)
        {
            if (root == null) return null;
            TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text t in texts)
            {
                if (t.gameObject.name.IndexOf(containsName, System.StringComparison.OrdinalIgnoreCase) >= 0) return t;
            }
            return null;
        }

        private static TextMeshProUGUI CreateText(
            GameObject parent, 
            string name, 
            string text, 
            float fontSize, 
            TextAlignmentOptions align, 
            Vector2 pos, 
            Vector2 size = default, 
            bool wordWrap = false,
            Vector2? pivot = null,
            Vector2? anchorMin = null,
            Vector2? anchorMax = null)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent.transform, false);

            RectTransform rect = obj.AddComponent<RectTransform>();
            if (anchorMin.HasValue) rect.anchorMin = anchorMin.Value;
            if (anchorMax.HasValue) rect.anchorMax = anchorMax.Value;
            if (pivot.HasValue) rect.pivot = pivot.Value;

            rect.anchoredPosition = pos;
            if (size != default && size != Vector2.zero)
            {
                rect.sizeDelta = size;
            }
            else
            {
                RectTransform parentRt = parent.GetComponent<RectTransform>();
                float defaultW = (parentRt != null && parentRt.sizeDelta.x > 50) ? Mathf.Max(200f, parentRt.sizeDelta.x - 40f) : 540f;
                rect.sizeDelta = new Vector2(defaultW, 60f);
            }

            TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = align;
            tmp.color = Color.white;
            tmp.textWrappingMode = wordWrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            tmp.overflowMode = wordWrap ? TextOverflowModes.Truncate : TextOverflowModes.Ellipsis;

            return tmp;
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

        private static GameObject CreateChild(GameObject parent, string name, Vector2 pos, Vector2 size)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);

            RectTransform rect = child.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            return child;
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
#endif
