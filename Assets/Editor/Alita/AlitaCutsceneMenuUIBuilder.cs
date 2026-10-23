using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using Alita;

namespace Alita.EditorScripts
{
    public static class AlitaCutsceneMenuUIBuilder
    {
        private const string SCENE_PATH = "Assets/Scenes/Alita_Discovery_Cutscene.unity";

        // Fonts
        private const string FONT_ZEN_DOTS = "Assets/Synty/InterfaceSciFiMenus/Fonts/Zen_Dots/ZenDots-Regular SDF.asset";
        private const string FONT_QUANTICO_BOLD = "Assets/Synty/InterfaceSciFiMenus/Fonts/Quantico/Quantico-Bold SDF.asset";

        // Sprites
        private const string SPR_PANEL_GRADIENT = "Assets/Synty/InterfaceSciFiMenus/Sprites/General/SPR_SciFiMenus_Gradient_Horizontal_01.png";
        private const string SPR_HOVER_GLOW = "Assets/Synty/InterfaceSciFiSoldierHUD/Sprites/FX/SPR_FX_SciFi_Box_Glowy.png";

        [MenuItem("Tools/Alita Battle Angel/Upgrade Cutscene UI (AAA Cyberpunk Glassmorphism)", false, 6)]
        public static void UpgradeCutsceneUI()
        {
            var scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
            Debug.Log($"[AlitaCutsceneMenuUIBuilder] Constructing ultra-clean Cyberpunk Menu in {scene.path}...");

            var canvasGo = GameObject.Find("SciFi_Menu_Canvas");
            if (canvasGo == null)
            {
                Debug.LogError("[AlitaCutsceneMenuUIBuilder] SciFi_Menu_Canvas not found!");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(canvasGo, "Upgrade Cutscene UI");

            var canvasGroup = canvasGo.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = canvasGo.AddComponent<CanvasGroup>();

            // Load Fonts & Sprites
            var fontZenDots = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_ZEN_DOTS);
            var fontQuantico = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_QUANTICO_BOLD);
            var sprGradient = AssetDatabase.LoadAssetAtPath<Sprite>(SPR_PANEL_GRADIENT);
            var sprGlow = AssetDatabase.LoadAssetAtPath<Sprite>(SPR_HOVER_GLOW);

            // Hide old template
            var oldSyntyScreen = canvasGo.transform.Find("Screen_SciFiMenus_Title");
            if (oldSyntyScreen != null) oldSyntyScreen.gameObject.SetActive(false);

            // Master Left Menu Panel
            Transform panelTrans = canvasGo.transform.Find("Panel_Cyberpunk_LeftMenu");
            GameObject panelGo;
            if (panelTrans == null)
            {
                panelGo = new GameObject("Panel_Cyberpunk_LeftMenu", typeof(RectTransform));
                panelGo.transform.SetParent(canvasGo.transform, false);
            }
            else
            {
                panelGo = panelTrans.gameObject;
            }

            for (int i = panelGo.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(panelGo.transform.GetChild(i).gameObject);
            }

            // Panel: Seamless full-height gradient from left edge
            var panelRt = panelGo.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0f, 0f);
            panelRt.anchorMax = new Vector2(0f, 1f);
            panelRt.pivot = new Vector2(0f, 0.5f);
            panelRt.anchoredPosition = Vector2.zero;
            panelRt.sizeDelta = new Vector2(620f, 0f);

            var panelImg = panelGo.GetComponent<Image>() ?? panelGo.AddComponent<Image>();
            panelImg.sprite = sprGradient;
            panelImg.type = Image.Type.Simple;
            // Dark obsidian gradient fading seamlessly into 3D scene
            panelImg.color = new Color(0.01f, 0.04f, 0.07f, 0.72f);
            panelImg.raycastTarget = false;

            // Sleek 1.5px vertical hairline on the far left
            var leftHairlineGo = CreateChild(panelGo, "Hairline_Left");
            var leftHairlineRt = leftHairlineGo.GetComponent<RectTransform>();
            leftHairlineRt.anchorMin = new Vector2(0f, 0.22f);
            leftHairlineRt.anchorMax = new Vector2(0f, 0.78f);
            leftHairlineRt.pivot = new Vector2(0f, 0.5f);
            leftHairlineRt.anchoredPosition = new Vector2(80f, 0f);
            leftHairlineRt.sizeDelta = new Vector2(1.5f, 0f);
            var leftHairlineImg = leftHairlineGo.AddComponent<Image>();
            leftHairlineImg.color = new Color(0f, 0.85f, 1f, 0.35f);

            // Inner Content (Centered vertically, cleanly spaced)
            var contentGo = CreateChild(panelGo, "Content_Container");
            var contentRt = contentGo.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 0.5f);
            contentRt.anchorMax = new Vector2(0f, 0.5f);
            contentRt.pivot = new Vector2(0f, 0.5f);
            contentRt.anchoredPosition = new Vector2(105f, 0f);
            contentRt.sizeDelta = new Vector2(420f, 440f);

            // ==========================================
            // HEADER: Bold, Iconic, Zero Clutter
            // ==========================================
            var headerGo = CreateChild(contentGo, "Header");
            var headerRt = headerGo.GetComponent<RectTransform>();
            SetTopAnchored(headerRt, 0f, 0f, 108f);

            // Title: ALITA
            var titleGo = CreateChild(headerGo, "Title_Alita");
            var titleRt = titleGo.GetComponent<RectTransform>();
            SetTopAnchored(titleRt, 0f, 0f, 62f);
            var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
            titleTmp.font = fontZenDots != null ? fontZenDots : titleTmp.font;
            titleTmp.fontSize = 58f;
            titleTmp.characterSpacing = 16f;
            titleTmp.text = "ALITA";
            titleTmp.color = Color.white;

            // Subtitle: BATTLE ANGEL
            var subGo = CreateChild(headerGo, "Subtitle_BattleAngel");
            var subRt = subGo.GetComponent<RectTransform>();
            SetTopAnchored(subRt, 0f, -64f, 26f);
            var subTmp = subGo.AddComponent<TextMeshProUGUI>();
            subTmp.font = fontQuantico != null ? fontQuantico : subTmp.font;
            subTmp.fontSize = 20f;
            subTmp.characterSpacing = 7f;
            subTmp.text = "B A T T L E   A N G E L";
            subTmp.color = new Color(0f, 0.95f, 1f, 1f);

            // 1.5px Hairline Divider
            var divGo = CreateChild(headerGo, "Divider");
            var divRt = divGo.GetComponent<RectTransform>();
            SetTopAnchored(divRt, 0f, -98f, 1.5f);
            var divImg = divGo.AddComponent<Image>();
            divImg.color = new Color(0f, 0.9f, 1f, 0.35f);

            // ==========================================
            // BUTTONS: Clean, Confident, Single-Line
            // ==========================================
            var btnStackGo = CreateChild(contentGo, "Button_Stack");
            var btnStackRt = btnStackGo.GetComponent<RectTransform>();
            SetTopAnchored(btnStackRt, 0f, -140f, 220f);

            // Button 1: START DISCOVERY
            var btn1Go = CreateCleanButton(
                btnStackGo,
                "Btn_StartJourney",
                0f,
                56f,
                "01",
                "START DISCOVERY",
                new Color(0f, 0.95f, 1f, 1f),
                fontQuantico,
                sprGlow,
                out Image glow1
            );

            // Button 2: CLINIC AWAKENING
            var btn2Go = CreateCleanButton(
                btnStackGo,
                "Btn_ClinicAwakening",
                -68f,
                56f,
                "02",
                "CLINIC AWAKENING",
                new Color(1f, 0.55f, 0.1f, 1f),
                fontQuantico,
                sprGlow,
                out Image glow2
            );

            // Button 3: EXIT SIMULATION
            var btn3Go = CreateCleanButton(
                btnStackGo,
                "Btn_Quit",
                -136f,
                56f,
                "03",
                "EXIT SIMULATION",
                new Color(0.55f, 0.72f, 0.88f, 1f),
                fontQuantico,
                sprGlow,
                out Image glow3
            );

            // ==========================================
            // FOOTER: Clean Keycap Prompt Only
            // ==========================================
            var footerGo = CreateChild(contentGo, "Footer");
            var footerRt = footerGo.GetComponent<RectTransform>();
            footerRt.anchorMin = new Vector2(0f, 0f);
            footerRt.anchorMax = new Vector2(1f, 0f);
            footerRt.pivot = new Vector2(0f, 0f);
            footerRt.anchoredPosition = Vector2.zero;
            footerRt.sizeDelta = new Vector2(0f, 32f);

            var promptGo = CreateChild(footerGo, "Label_Prompt");
            var promptRt = promptGo.GetComponent<RectTransform>();
            promptRt.anchorMin = Vector2.zero;
            promptRt.anchorMax = Vector2.one;
            promptRt.offsetMin = Vector2.zero;
            promptRt.offsetMax = Vector2.zero;
            var promptTmp = promptGo.AddComponent<TextMeshProUGUI>();
            promptTmp.font = fontQuantico != null ? fontQuantico : promptTmp.font;
            promptTmp.fontSize = 12f;
            promptTmp.characterSpacing = 2.5f;
            promptTmp.alignment = TextAlignmentOptions.MidlineLeft;
            promptTmp.text = "<color=#00F5FF>[ SPACE ]</color>  OR  <color=#00F5FF>[ ENTER ]</color>  TO ENGAGE";
            promptTmp.color = new Color(0.85f, 0.95f, 1f, 0.9f);

            // ==========================================
            // WIRE CONTROLLER
            // ==========================================
            var menuController = canvasGo.GetComponent<AlitaCutsceneSciFiMenu>();
            if (menuController == null) menuController = canvasGo.AddComponent<AlitaCutsceneSciFiMenu>();

            menuController.canvasGroup = canvasGroup;
            menuController.btnStartJourney = btn1Go.GetComponent<Button>();
            menuController.btnClinicAwakening = btn2Go.GetComponent<Button>();
            menuController.btnQuit = btn3Go.GetComponent<Button>();

            menuController.startJourneyGlow = glow1;
            menuController.clinicAwakeningGlow = glow2;
            menuController.quitGlow = glow3;

            menuController.ecgMonitorImage = null;
            menuController.ecgStatusText = null;
            menuController.beaconIndicator = null;
            menuController.keyPromptText = promptTmp;

            var cutsceneController = Object.FindAnyObjectByType<AlitaCutsceneController>();
            if (cutsceneController != null)
            {
                cutsceneController.scifiMenuCanvasGroup = canvasGroup;
                if (cutsceneController.cutsceneCamera != null)
                {
                    Vector3 focus = cutsceneController.GetAlitaFocusCenter();
                    cutsceneController.cutsceneCamera.transform.position = focus + new Vector3(-0.55f, 0.12f, -1.85f);
                    cutsceneController.cutsceneCamera.transform.LookAt(focus + new Vector3(-0.35f, -0.06f, 0f));
                    cutsceneController.cutsceneCamera.fieldOfView = 34f;
                    EditorUtility.SetDirty(cutsceneController.cutsceneCamera);
                }
                EditorUtility.SetDirty(cutsceneController);
            }

            EditorUtility.SetDirty(menuController);
            EditorUtility.SetDirty(panelGo);
            EditorUtility.SetDirty(canvasGo);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[AlitaCutsceneMenuUIBuilder] Ultra-clean Cyberpunk Menu built successfully!");
        }

        private static GameObject CreateChild(GameObject parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        private static void SetTopAnchored(RectTransform rt, float posX, float posY, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(posX, posY);
            rt.sizeDelta = new Vector2(0f, height);
        }

        private static GameObject CreateCleanButton(
            GameObject parent,
            string btnName,
            float posY,
            float height,
            string indexStr,
            string title,
            Color accentColor,
            TMP_FontAsset fontTitle,
            Sprite sprGlow,
            out Image glowImage)
        {
            var btnGo = CreateChild(parent, btnName);
            var btnRt = btnGo.GetComponent<RectTransform>();
            SetTopAnchored(btnRt, 0f, posY, height);

            // Dark subtle backing
            var bgImg = btnGo.AddComponent<Image>();
            bgImg.color = new Color(0.02f, 0.07f, 0.12f, 0.45f);
            bgImg.raycastTarget = true;

            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = bgImg;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            btn.colors = colors;

            // Glowing left accent pip / bar
            var accentBar = CreateChild(btnGo, "Accent_Bar");
            var accentBarRt = accentBar.GetComponent<RectTransform>();
            accentBarRt.anchorMin = new Vector2(0f, 0.15f);
            accentBarRt.anchorMax = new Vector2(0f, 0.85f);
            accentBarRt.pivot = new Vector2(0f, 0.5f);
            accentBarRt.anchoredPosition = Vector2.zero;
            accentBarRt.sizeDelta = new Vector2(3.5f, 0f);
            var accentBarImg = accentBar.AddComponent<Image>();
            accentBarImg.color = accentColor;
            accentBarImg.raycastTarget = false;

            // Hover glow sheen
            var glowGo = CreateChild(btnGo, "Hover_Glow");
            var glowRt = glowGo.GetComponent<RectTransform>();
            glowRt.anchorMin = Vector2.zero;
            glowRt.anchorMax = Vector2.one;
            glowRt.offsetMin = Vector2.zero;
            glowRt.offsetMax = Vector2.zero;
            glowImage = glowGo.AddComponent<Image>();
            glowImage.sprite = sprGlow;
            glowImage.type = Image.Type.Sliced;
            glowImage.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0f);
            glowImage.raycastTarget = false;

            // Index tag e.g. "01"
            var idxGo = CreateChild(btnGo, "Index_Tag");
            var idxRt = idxGo.GetComponent<RectTransform>();
            idxRt.anchorMin = new Vector2(0f, 0.5f);
            idxRt.anchorMax = new Vector2(0f, 0.5f);
            idxRt.pivot = new Vector2(0f, 0.5f);
            idxRt.anchoredPosition = new Vector2(16f, 0f);
            idxRt.sizeDelta = new Vector2(26f, 24f);
            var idxTmp = idxGo.AddComponent<TextMeshProUGUI>();
            idxTmp.font = fontTitle;
            idxTmp.fontSize = 12.5f;
            idxTmp.characterSpacing = 1.5f;
            idxTmp.alignment = TextAlignmentOptions.MidlineLeft;
            idxTmp.text = indexStr;
            idxTmp.color = accentColor;
            idxTmp.raycastTarget = false;

            // Title
            var titleGo = CreateChild(btnGo, "Title");
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 0.5f);
            titleRt.anchorMax = new Vector2(1f, 0.5f);
            titleRt.pivot = new Vector2(0f, 0.5f);
            titleRt.anchoredPosition = new Vector2(48f, 0f);
            titleRt.sizeDelta = new Vector2(-54f, 26f);
            var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
            titleTmp.font = fontTitle;
            titleTmp.fontSize = 17f;
            titleTmp.characterSpacing = 2.5f;
            titleTmp.alignment = TextAlignmentOptions.MidlineLeft;
            titleTmp.text = title;
            titleTmp.color = Color.white;
            titleTmp.raycastTarget = false;

            return btnGo;
        }
    }
}
