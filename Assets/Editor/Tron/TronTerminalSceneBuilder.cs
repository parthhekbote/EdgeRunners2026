using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TMPro;
using UnityEditor.SceneManagement;
using Tron.Terminal;

namespace Tron.Editor
{
    public static class TronTerminalSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Tron_Terminal.unity";

        [MenuItem("Tools/Tron/Build Complete Tron Terminal Scene")]
        public static void BuildScene()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 1. Clear existing root objects
            var rootObjs = scene.GetRootGameObjects();
            foreach (var go in rootObjs)
            {
                Object.DestroyImmediate(go);
            }

            // 2. Setup Camera
            GameObject camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            camGO.transform.position = new Vector3(0f, 1.25f, -2.15f);
            camGO.transform.rotation = Quaternion.Euler(7f, 0f, 0f);

            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.003f, 0.008f, 0.015f, 1f);
            cam.fieldOfView = 52f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 400f;

            var uad = camGO.AddComponent<UniversalAdditionalCameraData>();
            uad.renderPostProcessing = true;
            uad.volumeLayerMask = ~0;
            uad.antialiasing = AntialiasingMode.None;

            camGO.AddComponent<AudioListener>();
            camGO.AddComponent<TronCameraDrift>();

            // 3. Setup Lights & Atmosphere
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientSkyColor = new Color(0.005f, 0.015f, 0.03f, 1f);
            RenderSettings.fog = false;

            // Subtle console top spotlight
            GameObject spotLight = new GameObject("Light_Console_Spot");
            spotLight.transform.position = new Vector3(0f, 2.6f, -0.2f);
            spotLight.transform.rotation = Quaternion.Euler(65f, 0f, 0f);
            var sl = spotLight.AddComponent<Light>();
            sl.type = LightType.Spot;
            sl.color = new Color(0f, 0.85f, 1f);
            sl.intensity = 2.2f;
            sl.range = 6f;
            sl.spotAngle = 55f;

            // Amber side rim accent
            GameObject light2 = new GameObject("Light_Amber_Accent");
            light2.transform.position = new Vector3(3.5f, 1.8f, 1f);
            var l2 = light2.AddComponent<Light>();
            l2.type = LightType.Point;
            l2.color = new Color(1f, 0.35f, 0.05f);
            l2.intensity = 1.6f;
            l2.range = 7f;

            // Deep blue backlight
            GameObject light3 = new GameObject("Light_DeepBlue_Back");
            light3.transform.position = new Vector3(-3.5f, 2.2f, 1f);
            var l3 = light3.AddComponent<Light>();
            l3.type = LightType.Point;
            l3.color = new Color(0.05f, 0.25f, 0.95f);
            l3.intensity = 2.0f;
            l3.range = 8f;

            // 4. Setup Cyberspace Environment & Grid Floor
            GameObject floorGO = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floorGO.name = "Cyberspace_DarkFloor";
            floorGO.transform.position = new Vector3(0f, 0f, 0f);
            floorGO.transform.localScale = new Vector3(40f, 1f, 40f); // 400x400m infinite floor
            var floorMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Tron/Mat_Tron_Floor_Dark.mat");
            if (floorMat != null) floorGO.GetComponent<MeshRenderer>().sharedMaterial = floorMat;

            GameObject gridGO = GameObject.CreatePrimitive(PrimitiveType.Plane);
            gridGO.name = "Cyberspace_TronGrid";
            gridGO.transform.position = new Vector3(0f, 0.006f, 0f);
            gridGO.transform.localScale = new Vector3(40f, 1f, 40f);
            var gridMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Tron/Mat_Tron_Procedural_Grid.mat");
            if (gridMat != null) gridGO.GetComponent<MeshRenderer>().sharedMaterial = gridMat;
            Object.DestroyImmediate(gridGO.GetComponent<Collider>());
            Object.DestroyImmediate(floorGO.GetComponent<Collider>());

            // 3D Workstation Console Prop & Flanking Server Towers
            var consolePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonCyberCity/Prefabs/Props/SM_Prop_Console_01.prefab");
            if (consolePrefab != null)
            {
                var console = Object.Instantiate(consolePrefab, new Vector3(0f, 0f, 0.5f), Quaternion.Euler(0f, 180f, 0f));
                console.name = "SM_Prop_Console_01";
                console.transform.localScale = Vector3.one;
            }

            var serverPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonCyberCity/Prefabs/Props/SM_Prop_Power_Terminal_01.prefab");
            if (serverPrefab != null)
            {
                var serverL = Object.Instantiate(serverPrefab, new Vector3(-2.8f, 0f, 0.8f), Quaternion.Euler(0f, 35f, 0f));
                serverL.name = "Server_Tower_Left";

                var serverR = Object.Instantiate(serverPrefab, new Vector3(2.8f, 0f, 0.8f), Quaternion.Euler(0f, -35f, 0f));
                serverR.name = "Server_Tower_Right";
            }

            // 5. Setup Post-Processing Volume
            GameObject volGO = new GameObject("Tron_PostProcess_Volume");
            var volume = volGO.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;
            var prof = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/Tron_Terminal_PostProcess_Profile.asset");
            if (prof != null) volume.profile = prof;

            // 6. Setup Audio Manager
            GameObject audioMgrGO = new GameObject("Tron_Audio_Manager");
            var audioEngine = audioMgrGO.AddComponent<TronTerminalAudioEngine>();
            audioEngine.musicClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/Nine Inch Nails - Init.mp3");
            audioEngine.ambientDroneClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Tron_Drone_Sound.mp3");

            // 7. Setup Canvas and UI Hierarchy
            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Synty/InterfaceSciFiSoldierHUD/Fonts/Orbitron/Orbitron-ExtraBold SDF.asset");
            if (fontAsset == null)
            {
                fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Electronic Highway Sign SDF.asset");
            }

            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();

            GameObject canvasGO = new GameObject("Tron_Terminal_Canvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 2.0f;
            canvas.sortingOrder = 10;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            // Main Terminal Frame
            GameObject frameGO = CreateUIPanel(canvasGO.transform, "Terminal_MainFrame", new Vector2(1320f, 840f), new Vector2(0.5f, 0.5f), Vector2.zero);
            var frameImg = frameGO.GetComponent<Image>();
            frameImg.color = new Color(0.002f, 0.005f, 0.012f, 0.98f); // High-contrast deep obsidian

            // Border Line and Corner Tech Brackets
            CreateBorderFrame(frameGO.transform);

            // Header Container
            GameObject headerGO = CreateUIPanel(frameGO.transform, "Header_Bar", new Vector2(1240f, 54f), new Vector2(0.5f, 1f), new Vector2(0f, -38f));
            var headerImg = headerGO.GetComponent<Image>();
            headerImg.color = new Color(0.012f, 0.06f, 0.11f, 0.95f);

            // Header Title Text
            GameObject titleTextGO = CreateUIText(headerGO.transform, "TitleText", "ENCOM 511 // MAINFRAME OS v12.4.0 [NODE 0x7F]", fontAsset, 17, new Color(0f, 0.95f, 1f), TextAlignmentOptions.MidlineLeft);
            var titleRect = titleTextGO.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 0f);
            titleRect.anchorMax = new Vector2(0.6f, 1f);
            titleRect.offsetMin = new Vector2(16f, 0f);
            titleRect.offsetMax = new Vector2(0f, 0f);

            // Header Status / Clock Text
            GameObject clockTextGO = CreateUIText(headerGO.transform, "ClockText", "SYS_TIME: 00:00:00 | MCP: ACTIVE", fontAsset, 13, new Color(0.4f, 1f, 0.6f), TextAlignmentOptions.MidlineRight);
            var clockRect = clockTextGO.GetComponent<RectTransform>();
            clockRect.anchorMin = new Vector2(0.6f, 0f);
            clockRect.anchorMax = new Vector2(0.78f, 1f);
            clockRect.offsetMin = Vector2.zero;
            clockRect.offsetMax = Vector2.zero;

            // Audio Equalizer Container in Header
            GameObject eqContainer = CreateUIPanel(headerGO.transform, "Audio_Spectrum_Container", new Vector2(200f, 38f), new Vector2(1f, 0.5f), new Vector2(-115f, 0f));
            eqContainer.GetComponent<Image>().color = new Color(0.004f, 0.02f, 0.04f, 0.5f);

            RectTransform[] eqBars = new RectTransform[16];
            float barW = 8f;
            float spacing = 3.5f;
            float totalW = 16 * (barW + spacing) - spacing;
            float startX = -totalW * 0.5f + barW * 0.5f;

            for (int b = 0; b < 16; b++)
            {
                GameObject bar = new GameObject($"Bar_{b:00}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                bar.transform.SetParent(eqContainer.transform, false);
                var barRect = bar.GetComponent<RectTransform>();
                barRect.anchorMin = new Vector2(0.5f, 0f);
                barRect.anchorMax = new Vector2(0.5f, 0f);
                barRect.pivot = new Vector2(0.5f, 0f);
                barRect.anchoredPosition = new Vector2(startX + b * (barW + spacing), 6f);
                barRect.sizeDelta = new Vector2(barW, 6f);

                var barImg = bar.GetComponent<Image>();
                barImg.color = Color.Lerp(new Color(0f, 0.85f, 1f), new Color(1f, 0.3f, 0.85f), (float)b / 15f);
                eqBars[b] = barRect;
            }

            var specVis = audioMgrGO.AddComponent<TronAudioSpectrumVisualizer>();
            specVis.targetSource = audioEngine.musicSource;
            specVis.spectrumBars = eqBars;
            specVis.minBarHeight = 4f;
            specVis.maxBarHeight = 28f;

            // Terminal Body Scroll View
            GameObject scrollGO = new GameObject("Terminal_ScrollView", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
            scrollGO.transform.SetParent(frameGO.transform, false);
            var scrollRect = scrollGO.GetComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0f, 0f);
            scrollRect.anchorMax = new Vector2(1f, 1f);
            scrollRect.offsetMin = new Vector2(24f, 120f);
            scrollRect.offsetMax = new Vector2(-24f, -74f);

            var scrollImg = scrollGO.GetComponent<Image>();
            scrollImg.color = new Color(0.004f, 0.012f, 0.025f, 0.75f);

            var sRectComp = scrollGO.GetComponent<ScrollRect>();
            sRectComp.horizontal = false;
            sRectComp.vertical = true;
            sRectComp.movementType = ScrollRect.MovementType.Clamped;

            // Viewport
            GameObject viewportGO = new GameObject("Viewport", typeof(RectTransform), typeof(Mask), typeof(Image));
            viewportGO.transform.SetParent(scrollGO.transform, false);
            var vpRect = viewportGO.GetComponent<RectTransform>();
            vpRect.anchorMin = Vector2.zero;
            vpRect.anchorMax = Vector2.one;
            vpRect.sizeDelta = Vector2.zero;
            var vpMask = viewportGO.GetComponent<Mask>();
            vpMask.showMaskGraphic = false;
            sRectComp.viewport = vpRect;

            // Content Panel
            GameObject contentGO = new GameObject("Content", typeof(RectTransform), typeof(ContentSizeFitter), typeof(VerticalLayoutGroup));
            contentGO.transform.SetParent(viewportGO.transform, false);
            var contentRect = contentGO.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0f, 1f);
            contentRect.sizeDelta = new Vector2(0f, 300f);

            var csf = contentGO.GetComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var vlg = contentGO.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(20, 20, 20, 20);
            vlg.childForceExpandHeight = false;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childControlWidth = true;
            sRectComp.content = contentRect;

            // Terminal Output Text
            GameObject outputTextGO = CreateUIText(contentGO.transform, "TerminalOutputText", "", fontAsset, 16.5f, new Color(0f, 0.95f, 1f), TextAlignmentOptions.TopLeft);
            var outText = outputTextGO.GetComponent<TMP_Text>();
            outText.lineSpacing = 16f;
            outText.textWrappingMode = TextWrappingModes.Normal;
            outText.text = "<color=#00FFFF>╔══════════════════════════════════════════════════════════════════════╗</color>\n" +
                           "<color=#00FFFF>║      ENCOM INTERNATIONAL // MAINFRAME OS v12.4.0 [I/O TOWER 04]      ║</color>\n" +
                           "<color=#00FFFF>║      QUANTUM CELLULAR DIGITIZER ACTIVE  |  SECURITY LEVEL: SYSADMIN  ║</color>\n" +
                           "<color=#00FFFF>╚══════════════════════════════════════════════════════════════════════╝</color>\n\n" +
                           "<color=#00FF66>[SYS_INIT]</color> Memory parity verification: 65,536 TB Quantum Core ... <color=#00FF66>[OK]</color>\n" +
                           "<color=#00FF66>[SYS_INIT]</color> Teleportation laser laser-diode alignment: 405nm UV ... <color=#00FF66>[OK]</color>\n" +
                           "<color=#00FFFF>[NET_LINK]</color> Synchronizing with Master Control Program (MCP) ... <color=#FF9900>[CONNECTED]</color>\n" +
                           "<color=#00FFFF>[AUDIO]</color> Sound Stream: <color=#FFFFFF>NINE INCH NAILS - INIT</color> (TRON: ARES) ... <color=#00FF66>[STREAMING]</color>\n\n" +
                           "<color=#FFFF00>[AUTH]</color> User identity detected: <color=#00FFFF>\"FLYNN, KEVIN\"</color>\n" +
                           "<color=#00FF66>[SECURITY]</color> Biometric clearance confirmed. Welcome back, Creator.\n\n" +
                           "<color=#00E5FF>Searching active programs on The Grid:</color>\n" +
                           "  <color=#00FFFF>► TRON (0x01)</color>      - Defense System: <color=#00FF66>ONLINE</color>\n" +
                           "  <color=#FF9900>► CLU 2.0 (0x02)</color>   - Grid Optimization: <color=#FF3300>CORRUPTED</color>\n" +
                           "  <color=#FF3300>► ARES (0x03)</color>      - Vanguard Infiltration Protocol: <color=#FF3300>ACTIVE</color>\n" +
                           "  <color=#00FF66>► QUORRA (0x04)</color>    - Isomorphic Algorithm (ISO): <color=#00FF66>STABLE</color>\n\n" +
                           "<color=#00FFFF>[SYSTEM READY]</color> Terminal input channel open. Type <color=#FFFFFF>\"help\"</color> for available commands.\n" +
                           "<color=#00FFFF>█</color>";

            // Command Prompt & Input Field Row
            GameObject inputRowGO = CreateUIPanel(frameGO.transform, "Input_Row", new Vector2(1232f, 44f), new Vector2(0.5f, 0f), new Vector2(0f, 68f));
            var inputRowImg = inputRowGO.GetComponent<Image>();
            inputRowImg.color = new Color(0.01f, 0.04f, 0.08f, 0.95f);

            // Prompt Prefix
            GameObject promptTextGO = CreateUIText(inputRowGO.transform, "PromptPrefix", "FLYNN@ENCOM:~$", fontAsset, 16, new Color(0f, 0.85f, 1f), TextAlignmentOptions.MidlineLeft);
            var promptRect = promptTextGO.GetComponent<RectTransform>();
            promptRect.anchorMin = new Vector2(0f, 0f);
            promptRect.anchorMax = new Vector2(0f, 1f);
            promptRect.offsetMin = new Vector2(14f, 0f);
            promptRect.offsetMax = new Vector2(210f, 0f);

            // Input Field
            GameObject inputFieldGO = new GameObject("InputField", typeof(RectTransform), typeof(TMP_InputField));
            inputFieldGO.transform.SetParent(inputRowGO.transform, false);
            var ifRect = inputFieldGO.GetComponent<RectTransform>();
            ifRect.anchorMin = new Vector2(0f, 0f);
            ifRect.anchorMax = new Vector2(1f, 1f);
            ifRect.offsetMin = new Vector2(215f, 4f);
            ifRect.offsetMax = new Vector2(-12f, -4f);

            var inputField = inputFieldGO.GetComponent<TMP_InputField>();

            // Text component inside Input Field
            GameObject ifTextGO = CreateUIText(inputFieldGO.transform, "Text", "", fontAsset, 16, Color.white, TextAlignmentOptions.MidlineLeft);
            var ifTextRect = ifTextGO.GetComponent<RectTransform>();
            ifTextRect.anchorMin = Vector2.zero;
            ifTextRect.anchorMax = Vector2.one;
            ifTextRect.sizeDelta = Vector2.zero;
            inputField.textComponent = ifTextGO.GetComponent<TMP_Text>();

            // Placeholder Text
            GameObject placeholderGO = CreateUIText(inputFieldGO.transform, "Placeholder", "Enter command: help, grid, ares, mcp, digitize, music, clear...", fontAsset, 15, new Color(0.15f, 0.45f, 0.6f, 0.7f), TextAlignmentOptions.MidlineLeft);
            var phRect = placeholderGO.GetComponent<RectTransform>();
            phRect.anchorMin = Vector2.zero;
            phRect.anchorMax = Vector2.one;
            phRect.sizeDelta = Vector2.zero;
            inputField.placeholder = placeholderGO.GetComponent<TMP_Text>();
            inputField.caretColor = new Color(0f, 1f, 1f);
            inputField.caretWidth = 2;

            // Bottom Quick Action Buttons Dock
            GameObject dockGO = CreateUIPanel(frameGO.transform, "Dock_Bar", new Vector2(1232f, 42f), new Vector2(0.5f, 0f), new Vector2(0f, 16f));
            dockGO.GetComponent<Image>().color = new Color(0.005f, 0.02f, 0.04f, 0.6f);

            string[] quickCmds = new string[] { "GRID", "ARES", "MCP", "DIGITIZE", "MUSIC", "STATUS", "CLEAR", "BOOT" };
            float btnW = 142f;
            float btnH = 34f;
            float dockSpacing = 10f;
            float dockTotalW = quickCmds.Length * (btnW + dockSpacing) - dockSpacing;
            float dockStartX = -dockTotalW * 0.5f + btnW * 0.5f;

            Button[] actionButtons = new Button[quickCmds.Length];

            for (int k = 0; k < quickCmds.Length; k++)
            {
                string cmdName = quickCmds[k];
                GameObject btnGO = CreateUIButton(dockGO.transform, $"Btn_{cmdName}", $"[{cmdName}]", fontAsset, new Vector2(btnW, btnH), new Vector2(dockStartX + k * (btnW + dockSpacing), 0f));
                actionButtons[k] = btnGO.GetComponent<Button>();
            }

            // Digitize Full Screen Flash Overlay
            GameObject flashGO = CreateUIPanel(canvasGO.transform, "Digitize_Laser_Flash", new Vector2(1920f, 1080f), new Vector2(0.5f, 0.5f), Vector2.zero);
            flashGO.GetComponent<Image>().color = new Color(0.6f, 0.95f, 1.0f, 1f);
            var flashGroup = flashGO.AddComponent<CanvasGroup>();
            flashGroup.alpha = 0f;
            flashGroup.blocksRaycasts = false;

            // 8. Attach and Wire TronTerminalController
            var controller = canvasGO.AddComponent<TronTerminalController>();
            controller.terminalOutputText = outText;
            controller.terminalInputField = inputField;
            controller.headerStatusText = clockTextGO.GetComponent<TMP_Text>();
            controller.scrollRect = sRectComp;
            controller.audioEngine = audioEngine;
            controller.spectrumVisualizer = specVis;
            controller.digitizeFlashGroup = flashGroup;

            // Wire up quick buttons to controller
            if (actionButtons.Length >= 8)
            {
                actionButtons[0].onClick.AddListener(controller.QuickBtn_Grid);
                actionButtons[1].onClick.AddListener(controller.QuickBtn_Ares);
                actionButtons[2].onClick.AddListener(controller.QuickBtn_Mcp);
                actionButtons[3].onClick.AddListener(controller.QuickBtn_Digitize);
                actionButtons[4].onClick.AddListener(controller.QuickBtn_Music);
                actionButtons[5].onClick.AddListener(() => controller.ExecuteCommand("status"));
                actionButtons[6].onClick.AddListener(controller.QuickBtn_Clear);
                actionButtons[7].onClick.AddListener(controller.QuickBtn_Reboot);
            }

            // Save Scene
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[TronTerminalSceneBuilder] Successfully built and saved Tron_Terminal.unity!");
        }

        private static GameObject CreateUIPanel(Transform parent, string name, Vector2 size, Vector2 anchor, Vector2 anchoredPos)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPos;
            return go;
        }

        private static GameObject CreateUIText(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            return go;
        }

        private static GameObject CreateUIButton(Transform parent, string name, string label, TMP_FontAsset font, Vector2 size, Vector2 pos)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = pos;

            var img = go.GetComponent<Image>();
            img.color = new Color(0.015f, 0.08f, 0.15f, 0.9f);

            var btn = go.GetComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = new Color(0.02f, 0.12f, 0.22f, 0.9f);
            colors.highlightedColor = new Color(0f, 0.5f, 0.8f, 0.95f);
            colors.pressedColor = new Color(0f, 0.85f, 1f, 1f);
            colors.selectedColor = colors.normalColor;
            btn.colors = colors;

            GameObject textGO = CreateUIText(go.transform, "Label", label, font, 13f, new Color(0f, 0.9f, 1f), TextAlignmentOptions.Center);
            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            return go;
        }

        private static void CreateBorderFrame(Transform parent)
        {
            // Subtle glowing cyan top/bottom/left/right border lines
            Color borderCol = new Color(0f, 0.75f, 1f, 0.7f);
            Color bracketCol = new Color(0f, 1f, 1f, 1f);

            // Top
            CreateLine(parent, "Border_Top", new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(0f, 2f), borderCol);
            // Bottom
            CreateLine(parent, "Border_Bottom", new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 2f), borderCol);
            // Left
            CreateLine(parent, "Border_Left", new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(2f, 0f), borderCol);
            // Right
            CreateLine(parent, "Border_Right", new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(2f, 0f), borderCol);

            // Tech Corner Brackets
            float bSize = 24f;
            float bThick = 4f;
            // Top Left
            CreateLine(parent, "Bracket_TL_H", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(bSize * 0.5f, 0f), new Vector2(bSize, bThick), bracketCol);
            CreateLine(parent, "Bracket_TL_V", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -bSize * 0.5f), new Vector2(bThick, bSize), bracketCol);

            // Top Right
            CreateLine(parent, "Bracket_TR_H", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-bSize * 0.5f, 0f), new Vector2(bSize, bThick), bracketCol);
            CreateLine(parent, "Bracket_TR_V", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -bSize * 0.5f), new Vector2(bThick, bSize), bracketCol);

            // Bottom Left
            CreateLine(parent, "Bracket_BL_H", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(bSize * 0.5f, 0f), new Vector2(bSize, bThick), bracketCol);
            CreateLine(parent, "Bracket_BL_V", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, bSize * 0.5f), new Vector2(bThick, bSize), bracketCol);

            // Bottom Right
            CreateLine(parent, "Bracket_BR_H", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-bSize * 0.5f, 0f), new Vector2(bSize, bThick), bracketCol);
            CreateLine(parent, "Bracket_BR_V", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0f, bSize * 0.5f), new Vector2(bThick, bSize), bracketCol);
        }

        private static void CreateLine(Transform parent, string name, Vector2 aMax, Vector2 aMin, Vector2 pos, Vector2 sizeDelta, Color col)
        {
            GameObject line = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            line.transform.SetParent(parent, false);
            var rect = line.GetComponent<RectTransform>();
            rect.anchorMin = aMin;
            rect.anchorMax = aMax;
            rect.sizeDelta = sizeDelta;
            rect.anchoredPosition = pos;
            line.GetComponent<Image>().color = col;
        }
    }
}
