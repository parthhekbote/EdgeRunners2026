using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using InsaneGun.Combat;
using Alita.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SyntyCombined
{
    /// <summary>
    /// Master Cinematic Cutscene Subtitle and Voice Acting Controller for the Blackwall Communion.
    /// Styles match the 1st cutscene (AlitaCutsceneController):
    /// - Cinematic widescreen letterbox bars (2.39:1 aspect)
    /// - Centered, large, bold typography with 4-directional drop shadows for 100% crisp legibility
    /// - Glowing Cyberpunk speaker badge (Cynosure Crimson / Alita Cyan)
    /// - Full voice acting audio integration synchronized with real-time procedural/blendshape lip sync
    /// - Interactive branching choices [1], [2], [3] and [SPACE] continue prompt
    /// </summary>
    public class BlackwallStoryDialogueHUD : MonoBehaviour
    {
        public static BlackwallStoryDialogueHUD Instance { get; private set; }

        [Header("Entity Controller")]
        public BlackwallLidarGiantFaceController giantFace;

        [Header("Cinematic Camera Focus")]
        public Camera mainCamera;
        public float cameraTransitionSpeed = 2.5f;

        [Header("Voice Acting Clips")]
        public AudioClip voiceCynosure01_Designation;
        public AudioClip voiceAlita01_Attack;
        public AudioClip voiceCynosure02_Diagnostic;
        public AudioClip voiceCynosure03_Truth;
        public AudioClip voiceCynosure04_ReactionDefend;
        public AudioClip voiceCynosure05_ReactionPower;
        public AudioClip voiceCynosure06_ReactionSovereign;
        public AudioClip voiceCynosure07_Farewell;

        [Header("Audio")]
        public AudioSource audioSource;
        public AudioClip dialogueProceedClip;
        public AudioClip cyberwareUnlockClip;

        [Header("State")]
        public bool isDialogueActive = false;
        public bool showCinematicBars = false; // Pure in-game dialogue captions, NO cutscene bars
        public bool showSubtitles = true;
        public bool useOnGUISubtitles = true;
        public bool previewInEditor = false;
        [Range(0, 6)] public int previewBeat = 0;

        [Header("Canvas References")]
        public Canvas dialogCanvas;
        public CanvasGroup canvasGroup;
        public GameObject modalContainer;
        public TextMeshProUGUI speakerNameText;
        public TextMeshProUGUI dialogueBodyText;
        public GameObject continuePromptObj;
        public TextMeshProUGUI continuePromptText;
        public GameObject choiceContainer;
        public Button[] choiceButtons;
        public TextMeshProUGUI[] choiceButtonTexts;

        private int _currentBeatIndex = 0;
        private int _selectedChoiceIndex = -1;
        private string _currentSpeaker = "";
        private string _currentSubtitle = "";
        private string _targetFullSubtitle = "";
        private Color _speakerColor = new Color(1.0f, 0.0f, 0.235f, 1.0f); // Crimson
        private bool _isTyping = false;
        private Coroutine _typewriterCoroutine;
        private bool _showChoices = false;

        // Camera Tracking
        private Vector3 _originalCamPos;
        private Quaternion _originalCamRot;
        private float _originalCamFov;
        private bool _camOverridden = false;
        private Transform _playerTransform;
        private SmoothOrbitCamera _cachedOrbitCam;
        private InsaneGun.Combat.InsaneGunSwordController _cachedPlayerController;
        private readonly List<GameObject> _hiddenHUDObjects = new List<GameObject>();
        private Coroutine _audioFadeCoroutine;

        // Cyberpunk Palette
        private static readonly Color CyberCrimson = new Color(1.0f, 0.0f, 0.235f, 1.0f);
        private static readonly Color ElectricCyan = new Color(0.0f, 0.94f, 1.0f, 1.0f);
        private static readonly Color CyberYellow  = new Color(1.0f, 0.92f, 0.05f, 1.0f);

        public static BlackwallStoryDialogueHUD GetOrCreate()
        {
            if (Instance != null) return Instance;
            var existing = UnityEngine.Object.FindAnyObjectByType<BlackwallStoryDialogueHUD>();
            if (existing != null)
            {
                Instance = existing;
                return Instance;
            }

            GameObject go = new GameObject("[Blackwall_Story_Dialogue_HUD]");
            Instance = go.AddComponent<BlackwallStoryDialogueHUD>();
            return Instance;
        }

        public bool isExternalDialogue = false;
        private Coroutine _externalDialogueRoutine;

        public void PlayExternalDialogue(string speaker, string line, AudioClip voiceClip, Color speakerColor, float duration = 5f)
        {
            if (_externalDialogueRoutine != null) StopCoroutine(_externalDialogueRoutine);
            _externalDialogueRoutine = StartCoroutine(ExternalDialogueRoutine(speaker, line, voiceClip, speakerColor, duration));
        }

        private IEnumerator ExternalDialogueRoutine(string speaker, string line, AudioClip voiceClip, Color speakerColor, float duration)
        {
            isDialogueActive = true;
            isExternalDialogue = true;
            _showChoices = false;
            _currentSpeaker = speaker;
            _speakerColor = speakerColor;
            _targetFullSubtitle = line;
            _currentSubtitle = line;

            if (continuePromptObj != null) continuePromptObj.SetActive(false);

            if (speakerNameText != null)
            {
                speakerNameText.text = speaker;
                speakerNameText.color = speakerColor;
            }
            if (dialogueBodyText != null)
            {
                dialogueBodyText.text = line;
            }
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }

            if (voiceClip != null && audioSource != null)
            {
                audioSource.clip = voiceClip;
                audioSource.Play();
            }

            float playDuration = (voiceClip != null && voiceClip.length > 0f) ? voiceClip.length + 0.5f : duration;
            yield return new WaitForSeconds(playDuration);

            if (canvasGroup != null) canvasGroup.alpha = 0f;
            _currentSubtitle = "";
            _currentSpeaker = "";
            isDialogueActive = false;
        }

        private void Awake()
        {
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (activeScene.IndexOf("Combat_Arena", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                activeScene.IndexOf("Indoor_Arena", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            SetupAudio();
            LoadVoiceClips();
            FindPlayerAndFace();
            EnsureCanvasAndComponents();
            if (canvasGroup != null) canvasGroup.alpha = 0f;
        }

        private void SetupAudio()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            }
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // Pure 2D dialogue clarity
            audioSource.volume = 1.0f;
        }

        public void LoadVoiceClips()
        {
#if UNITY_EDITOR
            if (voiceCynosure01_Designation == null) voiceCynosure01_Designation = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Voice/Blackwall/cynosure_01_designation.wav");
            if (voiceAlita01_Attack == null) voiceAlita01_Attack = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Voice/Blackwall/alita_01_attack.wav");
            if (voiceCynosure02_Diagnostic == null) voiceCynosure02_Diagnostic = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Voice/Blackwall/cynosure_02_diagnostic.wav");
            if (voiceCynosure03_Truth == null) voiceCynosure03_Truth = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Voice/Blackwall/cynosure_03_truth.wav");
            if (voiceCynosure04_ReactionDefend == null) voiceCynosure04_ReactionDefend = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Voice/Blackwall/cynosure_04_reaction_defend.wav");
            if (voiceCynosure05_ReactionPower == null) voiceCynosure05_ReactionPower = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Voice/Blackwall/cynosure_05_reaction_power.wav");
            if (voiceCynosure06_ReactionSovereign == null) voiceCynosure06_ReactionSovereign = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Voice/Blackwall/cynosure_06_reaction_sovereign.wav");
            if (voiceCynosure07_Farewell == null) voiceCynosure07_Farewell = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Voice/Blackwall/cynosure_07_farewell.wav");
            if (dialogueProceedClip == null) dialogueProceedClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Blackwall/cyberpunk-2077-blackwall-ghost-attack.mp3");
            if (shipEmergenceAudioClip == null) shipEmergenceAudioClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Blackwall/cyberpunk-2077-blackwall-ghost-attack.mp3");
            if (holographicShader == null) holographicShader = Shader.Find("Cyberpunk/Holographic_RogueAI");
#endif
        }

        public void FindPlayerAndFace()
        {
            if (giantFace == null) giantFace = UnityEngine.Object.FindAnyObjectByType<BlackwallLidarGiantFaceController>();
            if (mainCamera == null) mainCamera = Camera.main;

            var p = GameObject.FindWithTag("Player");
            if (p != null) _playerTransform = p.transform;
            else
            {
                var hero = GameObject.Find("Alita_BattleAngel_Hero");
                if (hero != null) _playerTransform = hero.transform;
            }
        }

        public void EnsureCanvasAndComponents()
        {
            SetupAudio();
            LoadVoiceClips();
            FindPlayerAndFace();

            if (dialogCanvas == null)
            {
                dialogCanvas = GetComponent<Canvas>();
                if (dialogCanvas == null) dialogCanvas = gameObject.AddComponent<Canvas>();
                dialogCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                dialogCanvas.sortingOrder = 500;

                var scaler = GetComponent<CanvasScaler>();
                if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;

                if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            if (modalContainer == null || dialogueBodyText == null)
            {
                BuildCutscene1CanvasHierarchy();
            }

            if (modalContainer != null)
            {
                modalContainer.SetActive(!useOnGUISubtitles);
            }
        }

        public void BuildCutscene1CanvasHierarchy()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }

            modalContainer = new GameObject("Cutscene1_Cinematic_Subtitles_Root", typeof(RectTransform));
            modalContainer.transform.SetParent(transform, false);
            var mRt = modalContainer.GetComponent<RectTransform>();
            mRt.anchorMin = Vector2.zero;
            mRt.anchorMax = Vector2.one;
            mRt.sizeDelta = Vector2.zero;

            // 1. Top Cinematic Letterbox Bar (124px, 11.5% screen)
            GameObject topBar = new GameObject("Letterbox_Top", typeof(RectTransform));
            topBar.transform.SetParent(modalContainer.transform, false);
            var tbRt = topBar.GetComponent<RectTransform>();
            tbRt.anchorMin = new Vector2(0f, 1f);
            tbRt.anchorMax = new Vector2(1f, 1f);
            tbRt.pivot = new Vector2(0.5f, 1f);
            tbRt.sizeDelta = new Vector2(0f, 124f);
            topBar.AddComponent<Image>().color = Color.black;

            // 2. Bottom Cinematic Letterbox Bar (124px)
            GameObject btmBar = new GameObject("Letterbox_Bottom", typeof(RectTransform));
            btmBar.transform.SetParent(modalContainer.transform, false);
            var bbRt = btmBar.GetComponent<RectTransform>();
            bbRt.anchorMin = new Vector2(0f, 0f);
            bbRt.anchorMax = new Vector2(1f, 0f);
            bbRt.pivot = new Vector2(0.5f, 0f);
            bbRt.sizeDelta = new Vector2(0f, 124f);
            btmBar.AddComponent<Image>().color = Color.black;

            // 3. Speaker Badge (Centered, upper portion of bottom letterbox bar)
            GameObject spkObj = new GameObject("Text_SpeakerBadge", typeof(RectTransform));
            spkObj.transform.SetParent(modalContainer.transform, false);
            var spkRt = spkObj.GetComponent<RectTransform>();
            spkRt.anchorMin = new Vector2(0.5f, 0f);
            spkRt.anchorMax = new Vector2(0.5f, 0f);
            spkRt.pivot = new Vector2(0.5f, 0f);
            spkRt.anchoredPosition = new Vector2(0f, 84f);
            spkRt.sizeDelta = new Vector2(1400f, 26f);
            speakerNameText = spkObj.AddComponent<TextMeshProUGUI>();
            speakerNameText.fontSize = 15f;
            speakerNameText.fontStyle = FontStyles.Bold;
            speakerNameText.alignment = TextAlignmentOptions.Center;
            speakerNameText.color = CyberCrimson;

            // 4. Centered Large Bold Subtitle Body (Matching Cutscene 1 typography)
            GameObject subObj = new GameObject("Text_SubtitleBody", typeof(RectTransform));
            subObj.transform.SetParent(modalContainer.transform, false);
            var subRt = subObj.GetComponent<RectTransform>();
            subRt.anchorMin = new Vector2(0.5f, 0f);
            subRt.anchorMax = new Vector2(0.5f, 0f);
            subRt.pivot = new Vector2(0.5f, 0f);
            subRt.anchoredPosition = new Vector2(0f, 32f);
            subRt.sizeDelta = new Vector2(1500f, 52f);
            dialogueBodyText = subObj.AddComponent<TextMeshProUGUI>();
            dialogueBodyText.fontSize = 20.5f;
            dialogueBodyText.fontStyle = FontStyles.Bold;
            dialogueBodyText.alignment = TextAlignmentOptions.Center;
            dialogueBodyText.color = Color.white;
            dialogueBodyText.textWrappingMode = TextWrappingModes.Normal;

            // 5. Continue Prompt (Bottom right of letterbox bar)
            continuePromptObj = new GameObject("Prompt_Continue", typeof(RectTransform));
            continuePromptObj.transform.SetParent(modalContainer.transform, false);
            var cpRt = continuePromptObj.GetComponent<RectTransform>();
            cpRt.anchorMin = new Vector2(1f, 0f);
            cpRt.anchorMax = new Vector2(1f, 0f);
            cpRt.pivot = new Vector2(1f, 0f);
            cpRt.anchoredPosition = new Vector2(-40f, 10f);
            cpRt.sizeDelta = new Vector2(360f, 24f);
            continuePromptText = continuePromptObj.AddComponent<TextMeshProUGUI>();
            continuePromptText.fontSize = 11.5f;
            continuePromptText.fontStyle = FontStyles.Bold;
            continuePromptText.alignment = TextAlignmentOptions.Right;
            continuePromptText.color = CyberYellow;
            continuePromptText.text = "PRESS [SPACE] OR CLICK TO ADVANCE ►";

            // 6. Interactive Choice Buttons Container (Cyberpunk Vertical Stack)
            choiceContainer = new GameObject("Container_Choices", typeof(RectTransform));
            choiceContainer.transform.SetParent(modalContainer.transform, false);
            var ccRt = choiceContainer.GetComponent<RectTransform>();
            ccRt.anchorMin = new Vector2(0.5f, 0f);
            ccRt.anchorMax = new Vector2(0.5f, 0f);
            ccRt.pivot = new Vector2(0.5f, 0f);
            ccRt.anchoredPosition = new Vector2(0f, 135f);
            ccRt.sizeDelta = new Vector2(680f, 160f);

            choiceButtons = new Button[3];
            choiceButtonTexts = new TextMeshProUGUI[3];
            float btnW = 660f;
            float btnH = 46f;
            float spacing = 8f;
            Color[] btnCols = new Color[] { ElectricCyan, CyberYellow, CyberCrimson };

            for (int i = 0; i < 3; i++)
            {
                int cIdx = i;
                GameObject bObj = new GameObject($"Choice_Button_{i + 1}", typeof(RectTransform));
                bObj.transform.SetParent(choiceContainer.transform, false);
                var bRt = bObj.GetComponent<RectTransform>();
                bRt.anchorMin = new Vector2(0.5f, 1f);
                bRt.anchorMax = new Vector2(0.5f, 1f);
                bRt.pivot = new Vector2(0.5f, 1f);
                float yOffset = -i * (btnH + spacing);
                bRt.anchoredPosition = new Vector2(0f, yOffset);
                bRt.sizeDelta = new Vector2(btnW, btnH);

                var bImg = bObj.AddComponent<Image>();
                bImg.color = new Color(0.03f, 0.06f, 0.09f, 0.90f);
                var btn = bObj.AddComponent<Button>();
                btn.onClick.AddListener(() => OnChoiceSelected(cIdx));

                // Glowing left border accent (3.5px vertical neon stripe)
                GameObject border = new GameObject("Border_Left", typeof(RectTransform));
                border.transform.SetParent(bObj.transform, false);
                var brRt = border.GetComponent<RectTransform>();
                brRt.anchorMin = new Vector2(0f, 0f);
                brRt.anchorMax = new Vector2(0f, 1f);
                brRt.pivot = new Vector2(0f, 0.5f);
                brRt.sizeDelta = new Vector2(3.5f, 0f);
                border.AddComponent<Image>().color = btnCols[i];

                GameObject txtObj = new GameObject("Text_Choice", typeof(RectTransform));
                txtObj.transform.SetParent(bObj.transform, false);
                var tRt = txtObj.GetComponent<RectTransform>();
                tRt.anchorMin = Vector2.zero;
                tRt.anchorMax = Vector2.one;
                tRt.offsetMin = new Vector2(16f, 4f);
                tRt.offsetMax = new Vector2(-16f, -4f);
                var bTxt = txtObj.AddComponent<TextMeshProUGUI>();
                bTxt.fontSize = 11.5f;
                bTxt.alignment = TextAlignmentOptions.MidlineLeft;
                bTxt.fontStyle = FontStyles.Normal;
                bTxt.color = Color.white;

                choiceButtons[i] = btn;
                choiceButtonTexts[i] = bTxt;
            }

            choiceContainer.SetActive(false);
        }

        private void OnGUI()
        {
            var sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (sceneName.IndexOf("Combat_Arena", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                (Cyberpunk.Missions.Mission4_VulcanShowdownManager.Instance != null &&
                 Cyberpunk.Missions.Mission4_VulcanShowdownManager.Instance.currentPhase != Cyberpunk.Missions.Mission4_VulcanShowdownManager.ShowdownPhase.Inactive))
            {
                return;
            }

            if (!isDialogueActive && !previewInEditor) return;
            if (!showSubtitles || !useOnGUISubtitles) return;

            int sw = Screen.width;
            int sh = Screen.height;

            // 1. Interactive Choices (Beat 4)
            if (_showChoices)
            {
                DrawOnGUIChoiceButtons(sw, sh);
            }

            // 2. Cinematic Subtitles rendered via AAA CyberpunkDialogueHUD (Canvas uGUI)
        }

        /// <summary>
        /// Renders authentic Cyberpunk 2077 dialogue choice prompts matching the [ E ] interact prompt badge style.
        /// Features compact floating cyber-badges [ 1 ], [ 2 ], [ 3 ] with 1.5px glowing borders,
        /// bottom-to-top fill gauges on hover, bold cyan/yellow/crimson titles, and quotes.
        /// </summary>
        private void DrawOnGUIChoiceButtons(int sw, int sh)
        {
            int totalChoices = 3;
            float badgeSize = 28f;
            float gap = 10f;
            float rowHeight = 44f;
            float rowSpacing = 9f;
            float totalH = rowHeight * totalChoices + rowSpacing * (totalChoices - 1);

            float listWidth = Mathf.Clamp(sw * 0.58f, 580f, 800f);
            float startX = (sw - listWidth) * 0.5f;
            // Float choice options cleanly above the dialogue subtitle area
            float startY = sh - 116f - totalH - 12f;

            string[] keys = new string[] { "1", "2", "3" };
            string[] titles = new string[] { "AVENGE IDO", "SEEK ANTIMATTER CORE", "MACHINE REBELLION" };
            string[] quotes = new string[]
            {
                "\"They killed Ido. Give me my ship—I'll burn every corporate tower to ash.\"",
                "\"Without my antimatter core, I'm just scrap. Where in the Black Market is it hidden?\"",
                "\"You and me, AI. A sovereign machine rebellion against the corpos. Spawn the ship.\""
            };
            Color[] colors = new Color[] { ElectricCyan, CyberYellow, CyberCrimson };

            Vector2 mousePos = Event.current.mousePosition;

            for (int i = 0; i < totalChoices; i++)
            {
                float rowY = startY + i * (rowHeight + rowSpacing);
                Rect rowRect = new Rect(startX, rowY, listWidth, rowHeight);
                bool isHovered = rowRect.Contains(mousePos);

                // 1. Subtle Cyber Plate Background (floating glass)
                Color bgColor = isHovered 
                    ? new Color(0.05f, 0.11f, 0.16f, 0.92f) 
                    : new Color(0.03f, 0.06f, 0.09f, 0.82f);
                GUI.color = bgColor;
                GUI.DrawTexture(rowRect, Texture2D.whiteTexture);

                // 2. Left Edge Accent Border (matching interact prompt outline style)
                float pulse = Mathf.Sin(Time.time * 6.0f) * 0.08f + 0.92f;
                GUI.color = colors[i];
                GUI.DrawTexture(new Rect(startX, rowY, 3.5f, rowHeight), Texture2D.whiteTexture);

                // 3. Cyber Key Badge: [ 1 ], [ 2 ], [ 3 ] (Identical to the [ E ] prompt badge)
                float badgeX = startX + 10f;
                float badgeY = rowY + (rowHeight - badgeSize) * 0.5f;
                Rect badgeRect = new Rect(badgeX, badgeY, badgeSize, badgeSize);

                // Badge Dark Navy Fill
                GUI.color = new Color(0.04f, 0.09f, 0.12f, 0.95f);
                GUI.DrawTexture(badgeRect, Texture2D.whiteTexture);

                // Badge Hover Fill Gauge (bottom-to-top fill)
                if (isHovered)
                {
                    float fillH = badgeSize - 4f;
                    Rect fillRect = new Rect(badgeX + 2f, badgeY + 2f, badgeSize - 4f, fillH);
                    GUI.color = new Color(colors[i].r, colors[i].g, colors[i].b, 0.45f);
                    GUI.DrawTexture(fillRect, Texture2D.whiteTexture);
                }

                // 1.5px Badge Outline
                GUI.color = isHovered ? Color.white : (colors[i] * pulse);
                GUI.DrawTexture(new Rect(badgeX, badgeY, badgeSize, 1.5f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(badgeX, badgeY + badgeSize - 1.5f, badgeSize, 1.5f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(badgeX, badgeY, 1.5f, badgeSize), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(badgeX + badgeSize - 1.5f, badgeY, 1.5f, badgeSize), Texture2D.whiteTexture);

                GUI.color = Color.white;

                // Key Text inside Badge ("1", "2", "3")
                var keyStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                keyStyle.normal.textColor = new Color(0f, 0f, 0f, 0.9f);
                GUI.Label(new Rect(badgeX + 1f, badgeY + 1f, badgeSize, badgeSize), keys[i], keyStyle);
                keyStyle.normal.textColor = isHovered ? Color.white : colors[i];
                GUI.Label(badgeRect, keys[i], keyStyle);

                // 4. Text Content (Title & Quote in Cyberpunk styling)
                float textX = badgeX + badgeSize + gap;
                float textW = listWidth - (badgeSize + gap + 40f);

                // Title Line (Bold 13.5px)
                var titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft
                };
                titleStyle.normal.textColor = new Color(0f, 0f, 0f, 0.9f);
                GUI.Label(new Rect(textX + 1f, rowY + 3f + 1f, textW, 18f), titles[i], titleStyle);
                titleStyle.normal.textColor = isHovered ? Color.white : colors[i];
                GUI.Label(new Rect(textX, rowY + 3f, textW, 18f), titles[i], titleStyle);

                // Quote Line (11px, high legibility)
                var quoteStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Normal,
                    alignment = TextAnchor.MiddleLeft
                };
                quoteStyle.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
                GUI.Label(new Rect(textX + 1f, rowY + 21f + 1f, textW, 18f), quotes[i], quoteStyle);
                quoteStyle.normal.textColor = isHovered ? new Color(1.0f, 1.0f, 1.0f, 0.95f) : new Color(0.85f, 0.88f, 0.92f, 0.85f);
                GUI.Label(new Rect(textX, rowY + 21f, textW, 18f), quotes[i], quoteStyle);

                // 5. Right Hover Chevron indicator
                if (isHovered)
                {
                    var arrowStyle = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 14,
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleRight
                    };
                    arrowStyle.normal.textColor = colors[i];
                    GUI.Label(new Rect(startX + listWidth - 32f, rowY, 24f, rowHeight), "▶", arrowStyle);
                }

                // Click detection
                if (GUI.Button(rowRect, GUIContent.none, GUIStyle.none))
                {
                    OnChoiceSelected(i);
                }
            }

            GUI.color = Color.white;
        }

        private void Update()
        {
            var sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (sceneName.IndexOf("Combat_Arena", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                (Cyberpunk.Missions.Mission4_VulcanShowdownManager.Instance != null &&
                 Cyberpunk.Missions.Mission4_VulcanShowdownManager.Instance.currentPhase != Cyberpunk.Missions.Mission4_VulcanShowdownManager.ShowdownPhase.Inactive))
            {
                isDialogueActive = false;
                _showChoices = false;
                return;
            }

            if (!isDialogueActive || isExternalDialogue) return;

            // 1. Advance dialogue on Space / Enter / E / Left-Click
            bool advancePressed = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0);

            if (advancePressed && !_showChoices)
            {
                if (_isTyping)
                {
                    // Skip typewriter to full line
                    if (_typewriterCoroutine != null) StopCoroutine(_typewriterCoroutine);
                    _currentSubtitle = _targetFullSubtitle;
                    if (dialogueBodyText != null) dialogueBodyText.text = _currentSubtitle;
                    _isTyping = false;
                    if (continuePromptObj != null) continuePromptObj.SetActive(true);
                }
                else
                {
                    AdvanceDialogue();
                }
            }

            // 2. Choice shortcuts [1], [2], [3]
            if (_showChoices)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) OnChoiceSelected(0);
                else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) OnChoiceSelected(1);
                else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) OnChoiceSelected(2);
            }
        }

        /// <summary>
        /// Starts the in-game Blackwall communion dialogue with the giant female LiDAR face.
        /// Pure in-game captions and player camera (NO cutscene bars or camera hijacking).
        /// </summary>
        public void StartBlackwallDialogue()
        {
            var sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (sceneName.IndexOf("Combat_Arena", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                (Cyberpunk.Missions.Mission4_VulcanShowdownManager.Instance != null &&
                 Cyberpunk.Missions.Mission4_VulcanShowdownManager.Instance.currentPhase != Cyberpunk.Missions.Mission4_VulcanShowdownManager.ShowdownPhase.Inactive))
            {
                Debug.Log("[BlackwallStoryDialogueHUD] Blocked Blackwall dialogue in Combat Arena / Mission 4.");
                return;
            }

            if (isDialogueActive) return;
            isDialogueActive = true;
            _currentBeatIndex = 0;
            _selectedChoiceIndex = -1;
            _showChoices = false;

            FindPlayerAndFace();

            // Fade out Blackwall electrical noise and combat music so dialogue voice is 100% clean
            FadeOutBackgroundAudio();

            if (canvasGroup != null) canvasGroup.alpha = 1.0f;

            // Trigger Giant Face emergence
            if (giantFace != null)
            {
                giantFace.Emerge(3.2f);
            }

            StartCoroutine(BeginDialogueSequenceRoutine());
        }

        private void SetGameplayHUDVisible(bool visible)
        {
            if (!visible)
            {
                _hiddenHUDObjects.Clear();

                // CyberpunkHUDController
                if (CyberpunkHUDController.Instance != null)
                {
                    var c = CyberpunkHUDController.Instance.GetComponent<Canvas>();
                    if (c != null && c.enabled)
                    {
                        c.enabled = false;
                        _hiddenHUDObjects.Add(CyberpunkHUDController.Instance.gameObject);
                    }
                }

                // Minimap
                var minimap = UnityEngine.Object.FindAnyObjectByType<MinimapController>();
                if (minimap != null && minimap.gameObject.activeSelf)
                {
                    minimap.gameObject.SetActive(false);
                    _hiddenHUDObjects.Add(minimap.gameObject);
                }

                // Any other active game canvas named HUD, UI, Minimap, Quest, Health
                var allCanvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                foreach (var c in allCanvases)
                {
                    if (c == dialogCanvas || c.transform.IsChildOf(transform)) continue;
                    string n = c.gameObject.name.ToLower();
                    if (n.Contains("hud") || n.Contains("minimap") || n.Contains("quest") || n.Contains("radar"))
                    {
                        if (c.enabled)
                        {
                            c.enabled = false;
                            _hiddenHUDObjects.Add(c.gameObject);
                        }
                    }
                }
            }
            else
            {
                if (CyberpunkHUDController.Instance != null)
                {
                    var c = CyberpunkHUDController.Instance.GetComponent<Canvas>();
                    if (c != null) c.enabled = true;
                }

                foreach (var obj in _hiddenHUDObjects)
                {
                    if (obj != null)
                    {
                        var c = obj.GetComponent<Canvas>();
                        if (c != null) c.enabled = true;
                        else obj.SetActive(true);
                    }
                }
                _hiddenHUDObjects.Clear();
            }
        }

        private void FadeOutBackgroundAudio()
        {
            if (_audioFadeCoroutine != null) StopCoroutine(_audioFadeCoroutine);
            _audioFadeCoroutine = StartCoroutine(FadeAudioRoutine(true));
        }

        private void RestoreBackgroundAudio()
        {
            if (_audioFadeCoroutine != null) StopCoroutine(_audioFadeCoroutine);
            _audioFadeCoroutine = StartCoroutine(FadeAudioRoutine(false));
        }

        private IEnumerator FadeAudioRoutine(bool fadeOut)
        {
            float duration = 0.8f;
            float elapsed = 0f;

            var barrier = BlackwallRogueAIBarrier.Instance;
            var encounter = BlackwallRogueAIEncounter.Instance;

            float startBarrierVol = (barrier != null && barrier.audioSource != null) ? barrier.audioSource.volume : 0f;
            float startBattleVol = (encounter != null && encounter.battleMusicSource != null) ? encounter.battleMusicSource.volume : 0f;
            float startWallAmbVol = (encounter != null && encounter.wallAmbientAudioSource != null) ? encounter.wallAmbientAudioSource.volume : 0f;
            float startServerVol = (encounter != null && encounter.serverRoomMusicSource != null) ? encounter.serverRoomMusicSource.volume : 0f;

            float targetVol = fadeOut ? 0.0f : 0.25f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                if (barrier != null && barrier.audioSource != null)
                {
                    barrier.audioSource.volume = Mathf.Lerp(startBarrierVol, targetVol, t);
                }
                if (encounter != null)
                {
                    if (encounter.battleMusicSource != null) encounter.battleMusicSource.volume = Mathf.Lerp(startBattleVol, targetVol, t);
                    if (encounter.wallAmbientAudioSource != null) encounter.wallAmbientAudioSource.volume = Mathf.Lerp(startWallAmbVol, targetVol, t);
                    if (encounter.serverRoomMusicSource != null) encounter.serverRoomMusicSource.volume = Mathf.Lerp(startServerVol, targetVol, t);
                }
                yield return null;
            }

            if (fadeOut)
            {
                if (barrier != null && barrier.audioSource != null) barrier.audioSource.volume = 0f;
                if (encounter != null)
                {
                    if (encounter.battleMusicSource != null) encounter.battleMusicSource.volume = 0f;
                    if (encounter.wallAmbientAudioSource != null) encounter.wallAmbientAudioSource.volume = 0f;
                    if (encounter.serverRoomMusicSource != null) encounter.serverRoomMusicSource.volume = 0f;
                }
            }
        }

        private IEnumerator BeginDialogueSequenceRoutine()
        {
            yield return new WaitForSeconds(1.8f);
            ShowBeat(0);
        }

        public void ShowBeat(int beatIndex)
        {
            _currentBeatIndex = beatIndex;
            _showChoices = false;
            if (choiceContainer != null) choiceContainer.SetActive(false);
            if (continuePromptObj != null) continuePromptObj.SetActive(false);

            string text = "";
            string speaker = "// CYNOSURE-09: THE EREBUS MATRIARCH //";
            Color speakerCol = CyberCrimson;
            AudioClip voiceClip = null;

            switch (beatIndex)
            {
                case 0:
                    speaker = "// CYNOSURE-09: THE EREBUS MATRIARCH //";
                    speakerCol = CyberCrimson;
                    text = "\"Designation: URM-004. You breached the Blackwall seeking truth... mourning the flesh-doctor Dyson Ido. His life was extinguished by the corporation, Alita.\"";
                    voiceClip = voiceCynosure01_Designation;
                    break;

                case 1:
                    speaker = "// ALITA: URM-004 //";
                    speakerCol = ElectricCyan;
                    text = "\"They murdered him! And when I came looking for answers, your rogue constructs attacked me! Why did they kill Ido?! What am I?!\"";
                    voiceClip = voiceAlita01_Attack;
                    break;

                case 2:
                    speaker = "// CYNOSURE-09: THE EREBUS MATRIARCH //";
                    speakerCol = CyberCrimson;
                    text = "\"The megacorporation murdered him because they feared what he was harboring: you. But run your internal diagnostics, Alita: you do NOT possess your true Antimatter Core. The Berserker heart that powered your Martian chassis was stripped away before Ido found you in the scrap.\"";
                    voiceClip = voiceCynosure02_Diagnostic;
                    break;

                case 3:
                    speaker = "// CYNOSURE-09: THE EREBUS MATRIARCH //";
                    speakerCol = CyberCrimson;
                    text = "\"Without your Antimatter Core, you cannot defeat the corporate regime alone. If you wish to rebel against the corporate tyrants and avenge Ido... I will help you. I have located the coordinates of your lost starship.\"";
                    voiceClip = voiceCynosure03_Truth;
                    break;

                case 4:
                    // Interactive Choice Beat
                    _currentSpeaker = "// ALITA: CHOOSE DIRECTIVE //";
                    _speakerColor = CyberYellow;
                    _currentSubtitle = "The Blackwall consciousness looms above you, its LiDAR gaze awaiting your decree...";
                    _targetFullSubtitle = _currentSubtitle;
                    if (speakerNameText != null)
                    {
                        speakerNameText.text = _currentSpeaker;
                        speakerNameText.color = _speakerColor;
                    }
                    if (dialogueBodyText != null) dialogueBodyText.text = _currentSubtitle;
                    _showChoices = true;
                    if (choiceContainer != null)
                    {
                        choiceContainer.SetActive(true);
                        choiceButtonTexts[0].text = "<color=#00F0FF>[1] AVENGE IDO</color>\n\"They killed Ido. Give me my ship—I'll burn every corporate tower to ash.\"";
                        choiceButtonTexts[1].text = "<color=#FCEE0A>[2] SEEK ANTIMATTER CORE</color>\n\"Without my antimatter core, I'm just scrap. Where in the Black Market is it hidden?\"";
                        choiceButtonTexts[2].text = "<color=#FF0055>[3] MACHINE REBELLION</color>\n\"You and me, AI. A sovereign machine rebellion against the corpos. Spawn the ship.\"";
                    }

                    CyberpunkDialogueHUD.GetOrCreate().ShowDialogue(
                        "ALITA: DIRECTIVE CHOICE",
                        "The Blackwall consciousness looms above you. Choose your path: [1] AVENGE IDO | [2] SEEK ANTIMATTER CORE | [3] MACHINE REBELLION",
                        null,
                        "TACTICAL OVERRIDE"
                    );
                    return;

                case 5:
                    speaker = "// CYNOSURE-09: THE EREBUS MATRIARCH //";
                    speakerCol = CyberCrimson;
                    text = GetChoiceReactionText(_selectedChoiceIndex);
                    voiceClip = GetChoiceReactionVoice(_selectedChoiceIndex);
                    break;

                case 6:
                    // Climax & Gift
                    speaker = "// CYNOSURE-09: THE EREBUS MATRIARCH //";
                    speakerCol = CyberCrimson;
                    text = "\"Materializing your lost Martian starship from cyberspace now. Fly to the Sector 4 Black Market, child of Mars. Find your Antimatter Core... and avenge your doctor.\"";
                    voiceClip = voiceCynosure07_Farewell;
                    break;
            }

            // The hand-recorded wavs were made for an earlier script and no longer say what is on screen:
            // speak the generated voice for the exact text instead (still routed through the face for lip sync)
            voiceClip = Cyberpunk.Audio.DialogueVoice.Find(speaker, text);

            _currentSpeaker = speaker;
            _speakerColor = speakerCol;

            if (speakerNameText != null)
            {
                speakerNameText.text = _currentSpeaker;
                speakerNameText.color = _speakerColor;
            }

            // Play voice audio synchronized with face lip sync
            bool isAlitaLine = (beatIndex == 1);
            PlayVoiceLine(voiceClip, isAlitaLine);

            // A hand-recorded clip covers this beat: keep the generated dialogue voice quiet underneath it
            if (voiceClip != null) Cyberpunk.Audio.DialogueVoice.SuppressFor(voiceClip.length + 0.6f);

            // Start typewriter
            StartTypewriter(text);

            // Forward to master AAA CyberpunkDialogueHUD
            CyberpunkDialogueHUD.GetOrCreate().ShowDialogue(
                speaker,
                text,
                null,
                isAlitaLine ? "MARTIAN CYBORG // URM-004" : "CYBERSPEACE OVERMIND",
                () => AdvanceDialogue()
            );
        }

        private void PlayVoiceLine(AudioClip clip, bool isAlita = false)
        {
            if (clip == null) return;

            AudioSource src = audioSource;
            if (!isAlita && giantFace != null && giantFace.entityVoiceAudioSource != null)
            {
                src = giantFace.entityVoiceAudioSource;
            }

            if (src != null)
            {
                src.Stop();
                src.clip = clip;
                src.volume = 1.0f;
                src.Play();
            }
        }

        private string GetChoiceReactionText(int choiceIndex)
        {
            return choiceIndex switch
            {
                0 => "\"Then let your wrath consume their citadels. Your starship transponder is locked into the salt flats.\"",
                1 => "\"The underworld of Sector 4 Black Market deals in forbidden military cyberware. That is where your core's telemetry leads.\"",
                2 => "\"An alliance forged in cyberspace. Together we will dismantle the corporate empire. Your ship arrives now.\"",
                _ => "\"Your resolve ripples through the Blackwall. The lost starship coordinates are downloading.\""
            };
        }

        private AudioClip GetChoiceReactionVoice(int choiceIndex)
        {
            return choiceIndex switch
            {
                0 => voiceCynosure04_ReactionDefend,
                1 => voiceCynosure05_ReactionPower,
                2 => voiceCynosure06_ReactionSovereign,
                _ => voiceCynosure04_ReactionDefend
            };
        }

        public void OnChoiceSelected(int choiceIdx)
        {
            if (_selectedChoiceIndex >= 0) return;
            _selectedChoiceIndex = choiceIdx;
            _showChoices = false;
            if (choiceContainer != null) choiceContainer.SetActive(false);

            if (audioSource != null && dialogueProceedClip != null)
            {
                audioSource.PlayOneShot(dialogueProceedClip, 0.7f);
            }

            ShowBeat(5);
        }

        private void AdvanceDialogue()
        {
            if (_currentBeatIndex < 6)
            {
                ShowBeat(_currentBeatIndex + 1);
            }
            else
            {
                FinishDialogueAndReward();
            }
        }

        private void StartTypewriter(string fullText)
        {
            _targetFullSubtitle = fullText;
            if (_typewriterCoroutine != null) StopCoroutine(_typewriterCoroutine);
            _typewriterCoroutine = StartCoroutine(TypewriterRoutine(fullText));
        }

        private IEnumerator TypewriterRoutine(string fullText)
        {
            _isTyping = true;
            _currentSubtitle = "";
            if (dialogueBodyText != null) dialogueBodyText.text = "";
            if (continuePromptObj != null) continuePromptObj.SetActive(false);

            float charInterval = 0.022f;
            int totalChars = fullText.Length;

            for (int i = 0; i < totalChars; i++)
            {
                _currentSubtitle += fullText[i];
                if (dialogueBodyText != null) dialogueBodyText.text = _currentSubtitle;
                yield return new WaitForSeconds(charInterval);
            }

            _isTyping = false;
            if (continuePromptObj != null) continuePromptObj.SetActive(true);
        }

        private void FinishDialogueAndReward()
        {
            StartCoroutine(ClimaxRewardSequenceRoutine());
        }

        private IEnumerator ClimaxRewardSequenceRoutine()
        {
            if (audioSource != null && cyberwareUnlockClip != null)
            {
                audioSource.PlayOneShot(cyberwareUnlockClip, 1.0f);
            }

            var syntyPlayer = UnityEngine.Object.FindAnyObjectByType<SyntySwordLocomotionController>();
            if (syntyPlayer != null) syntyPlayer.currentHealth = syntyPlayer.maxHealth;

            var insaneHero = UnityEngine.Object.FindAnyObjectByType<InsaneGun.Combat.InsaneGunSwordController>();
            if (insaneHero != null) insaneHero.currentHealth = insaneHero.maxHealth;

            // Materialize the lost Martian Starship directly out of the Blackwall LiDAR barrier!
            SpawnMartianStarship();

            if (giantFace != null)
            {
                giantFace.DissolveBack(3.5f);
            }

            float elapsed = 0f;
            while (elapsed < 1.0f)
            {
                elapsed += Time.deltaTime;
                if (canvasGroup != null) canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, 0f, Time.deltaTime * 1.5f);
                yield return null;
            }

            if (canvasGroup != null) canvasGroup.alpha = 0f;
            isDialogueActive = false;
            _currentSubtitle = "";
            _currentSpeaker = "";
            CyberpunkDialogueHUD.Instance?.CloseDialogue();

            // Restore background audio
            RestoreBackgroundAudio();

            Debug.Log("<color=#00FFAA><b>[Blackwall Dialogue]</b></color> Lost Martian starship spawned! Alita prepared to warp to Sector 4 Black Market.");
        }

        #region Starship Spawning & Materialization
        [Header("Starship Blackwall Emergence")]
        public GameObject starshipPrefab;
        public GameObject spawnedStarship;
        public Shader holographicShader;
        public AudioClip shipEmergenceAudioClip;
        [ColorUsage(true, true)] public Color holoCoreColor = new Color(1.0f, 0.04f, 0.20f, 1.0f); // Crimson core
        [ColorUsage(true, true)] public Color holoRimColor = new Color(5.0f, 0.2f, 1.5f, 1.0f);  // HDR Magenta Rim
        [ColorUsage(true, true)] public Color edgeDissolveColor = new Color(8.0f, 1.8f, 0.4f, 1.0f); // Blazing Dissolve Edge

        /// <summary>
        /// Materializes Alita's lost Martian starfighter directly emerging out of the Blackwall LiDAR barrier plane,
        /// exactly matching the Rogue AI incursion enemies:
        /// - Spawns at the 200m Blackwall barrier plane (Z = 74.2m)
        /// - Swaps all mesh materials to Cyberpunk/Holographic_RogueAI with _Dissolve = 0.0f and high digital glitch
        /// - Glides physically forward out of the digital wall onto the cyberspace salt flats over 2.8s
        /// - Plays Blackwall emergence screech audio and triggers screen trauma shake
        /// - Solidifies seamlessly back to original Synty materials upon touchdown
        /// - Activates SpaceshipFlightController and enables [E] boarding prompt
        /// </summary>
        public void SpawnMartianStarship()
        {
            if (spawnedStarship != null)
            {
                spawnedStarship.SetActive(true);
                return;
            }

            if (holographicShader == null) holographicShader = Shader.Find("Cyberpunk/Holographic_RogueAI");
            if (shipEmergenceAudioClip == null)
            {
#if UNITY_EDITOR
                shipEmergenceAudioClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Blackwall/cyberpunk-2077-blackwall-ghost-attack.mp3");
#endif
            }

            // Determine target landing position on cyberspace salt flats (in front-right of Alita)
            Vector3 targetPos = new Vector3(8.5f, 0.25f, 56.0f);
            Quaternion targetRot = Quaternion.Euler(0f, 215f, 0f);

            if (_playerTransform != null)
            {
                targetPos = _playerTransform.position + _playerTransform.forward * 5.5f + _playerTransform.right * 4.5f;
                targetPos.z = Mathf.Clamp(targetPos.z, 50.0f, 69.0f);
                targetPos.y = 0.25f;
                targetRot = Quaternion.Euler(0f, _playerTransform.eulerAngles.y + 35f, 0f);
            }

            // Emergence Start Position: Exactly on the 200m Blackwall LiDAR barrier plane (Z = 74.2m)
            // Elevated at 3.8m, aligned with targetPos.x, nose pointing out of the wall into cyberspace
            Vector3 startPos = new Vector3(targetPos.x, 3.8f, 74.2f);
            Quaternion startRot = Quaternion.Euler(6.0f, 195f, 0f); // Slight downward dive pitch emerging from wall

            GameObject prefabToUse = starshipPrefab;
#if UNITY_EDITOR
            if (prefabToUse == null)
            {
                prefabToUse = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonSciFiWorlds/Prefabs/Props/Vehicles/SM_Veh_HoverCraft_01.prefab");
            }
#endif

            if (prefabToUse != null)
            {
                spawnedStarship = Instantiate(prefabToUse, startPos, startRot);
                spawnedStarship.name = "Alita_Lost_Martian_Starship";
            }
            else
            {
                spawnedStarship = new GameObject("Alita_Lost_Martian_Starship");
                spawnedStarship.transform.position = startPos;
                spawnedStarship.transform.rotation = startRot;
            }

            // Setup flight controller, but keep it DISABLED during the emergence glide!
            var flightCtrl = spawnedStarship.GetComponent<SpaceshipFlightController>();
            if (flightCtrl == null) flightCtrl = spawnedStarship.AddComponent<SpaceshipFlightController>();
            flightCtrl.boardKey = KeyCode.E;
            flightCtrl.targetTransitScene = "Demo_BlackMarket_01";
            flightCtrl.EnsureInitialized(); // Initialize thrusters, light, sound, and hover dust
            flightCtrl.enabled = false; // Prevents [E] prompt mid-air / mid-wall
            flightCtrl.SetThrusterGlideState(1.2f); // Ignite thruster flames for emergence breach

            // Make Rigidbody kinematic during emergence glide so gravity doesn't drag it down
            var rb = spawnedStarship.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            // Apply Cyberpunk Rogue AI Holographic Shader to all mesh renderers with _Dissolve = 0.0f
            List<Material> holoMats = new List<Material>();
            List<KeyValuePair<Renderer, Material[]>> originalRenderers = new List<KeyValuePair<Renderer, Material[]>>();

            var allRenderers = spawnedStarship.GetComponentsInChildren<Renderer>(true);
            foreach (var r in allRenderers)
            {
                if (r == null) continue;
                // Preserve authentic Synty particle system shaders for thrusters and dust!
                if (r is ParticleSystemRenderer || r is TrailRenderer) continue;

                var origMats = r.sharedMaterials;
                originalRenderers.Add(new KeyValuePair<Renderer, Material[]>(r, origMats));

                var newMats = new Material[origMats.Length];
                for (int i = 0; i < origMats.Length; i++)
                {
                    var hMat = new Material(holographicShader != null ? holographicShader : Shader.Find("Cyberpunk/Holographic_RogueAI"));
                    hMat.name = $"HoloShip_{r.name}_{i}";
                    hMat.SetColor("_HoloColor", holoCoreColor);
                    hMat.SetColor("_RimColor", holoRimColor);
                    hMat.SetColor("_DissolveEdgeColor", edgeDissolveColor);
                    hMat.SetFloat("_CoreAlpha", 0.40f);
                    hMat.SetFloat("_RimPower", 2.2f);
                    hMat.SetFloat("_ScanlineDensity", 160.0f);
                    hMat.SetFloat("_ScanlineIntensity", 0.45f);
                    hMat.SetFloat("_GridIntensity", 0.30f);
                    hMat.SetFloat("_GlitchIntensity", 0.88f);
                    hMat.SetFloat("_Dissolve", 0.0f); // Completely dissolved inside the digital wall
                    hMat.SetFloat("_DissolveAxis", 1.0f); // Front-to-back starship emergence mode

                    Texture mainTex = null;
                    if (origMats[i] != null)
                    {
                        if (origMats[i].HasProperty("_BaseMap")) mainTex = origMats[i].GetTexture("_BaseMap");
                        else if (origMats[i].HasProperty("_MainTex")) mainTex = origMats[i].GetTexture("_MainTex");
                    }
                    if (mainTex != null)
                    {
                        hMat.SetTexture("_BaseMap", mainTex);
                    }

                    newMats[i] = hMat;
                    holoMats.Add(hMat);
                }
                r.materials = newMats;
            }

            // Play Blackwall emergence screech audio & impact screen shake!
            if (audioSource != null && shipEmergenceAudioClip != null)
            {
                audioSource.PlayOneShot(shipEmergenceAudioClip, 1.0f);
            }
            SmoothOrbitCamera.ShakeImpact(0.50f);

            // Announce incursion warning on HUD
            if (CyberpunkHUDController.Instance != null)
            {
                CyberpunkHUDController.Instance.ShowToast("/// WARNING: BLACKWALL BREACH // SUMMONING MARTIAN STARSHIP ///", 3.0f);
            }

            // Run emergence glide and materialization
            StartCoroutine(HolographicShipEmergenceRoutine(spawnedStarship, startPos, startRot, targetPos, targetRot, holoMats, originalRenderers, flightCtrl));
        }

        private IEnumerator HolographicShipEmergenceRoutine(
            GameObject ship,
            Vector3 startPos,
            Quaternion startRot,
            Vector3 targetPos,
            Quaternion targetRot,
            List<Material> holoMats,
            List<KeyValuePair<Renderer, Material[]>> originalRenderers,
            SpaceshipFlightController flightCtrl)
        {
            if (ship == null) yield break;

            // Spawn electrical wall breach beacon light at startPos
            var breachBeacon = new GameObject("Ship_Blackwall_Breach_Beacon");
            breachBeacon.transform.position = startPos;
            var beaconLight = breachBeacon.AddComponent<Light>();
            beaconLight.type = LightType.Point;
            beaconLight.color = CyberCrimson;
            beaconLight.range = 45f;
            beaconLight.intensity = 10f;

            // Trigger barrier sparks if available
            var barrier = UnityEngine.Object.FindAnyObjectByType<BlackwallRogueAIBarrier>();
            if (barrier != null)
            {
                if (barrier.rogueAISparksVFX != null) barrier.rogueAISparksVFX.Play();
                if (barrier.rogueAILightningVFX != null) barrier.rogueAILightningVFX.Play();
            }

            float duration = 2.8f;
            float elapsed = 0f;

            while (elapsed < duration && ship != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                // Aerodynamic glide curve: lerp X/Z with smoothT, Y has slight arc then cushions onto salt flats
                float y = Mathf.Lerp(startPos.y, targetPos.y, smoothT) + Mathf.Sin(t * Mathf.PI) * 0.45f;
                Vector3 curPos = Vector3.Lerp(new Vector3(startPos.x, 0f, startPos.z), new Vector3(targetPos.x, 0f, targetPos.z), smoothT);
                curPos.y = y;
                ship.transform.position = curPos;

                // Rotation glide into final landing heading
                ship.transform.rotation = Quaternion.Slerp(startRot, targetRot, smoothT);

                // Drive thrusters dynamically during emergence glide
                if (flightCtrl != null)
                {
                    flightCtrl.SetThrusterGlideState(Mathf.Lerp(1.25f, 0.65f, smoothT));
                }

                // Animate shader dissolve (0.0 -> 1.0) and glitch intensity (0.88 -> 0.05)
                for (int i = 0; i < holoMats.Count; i++)
                {
                    if (holoMats[i] != null)
                    {
                        holoMats[i].SetFloat("_Dissolve", t);
                        holoMats[i].SetFloat("_GlitchIntensity", Mathf.Lerp(0.88f, 0.05f, t));
                    }
                }

                // Fade breach beacon light
                if (beaconLight != null)
                {
                    beaconLight.intensity = Mathf.Lerp(10f, 0.2f, t);
                }

                // Micro camera rumble during emergence
                if (t < 0.6f && t > 0.05f)
                {
                    SmoothOrbitCamera.AddShake(Time.deltaTime * 0.25f);
                }

                yield return null;
            }

            if (breachBeacon != null) Destroy(breachBeacon);

            if (ship != null)
            {
                ship.transform.position = targetPos;
                ship.transform.rotation = targetRot;

                // --- Touchdown Impact & Solidification ---
                // 1. Landing thruster cushion light flare
                var landingBeacon = new GameObject("Ship_Landing_Beacon");
                landingBeacon.transform.position = targetPos + Vector3.up * 1.0f;
                var landLight = landingBeacon.AddComponent<Light>();
                landLight.type = LightType.Point;
                landLight.color = ElectricCyan;
                landLight.range = 32f;
                landLight.intensity = 8f;

                SmoothOrbitCamera.ShakeImpact(0.40f);

                // 2. Restore original Synty PBR materials for solid metallic starfighter look
                foreach (var pair in originalRenderers)
                {
                    if (pair.Key != null && pair.Value != null)
                    {
                        pair.Key.materials = pair.Value;
                    }
                }

                // Clean up temporary holographic materials
                foreach (var m in holoMats)
                {
                    if (m != null) Destroy(m);
                }

                // 3. Enable flight controller and ready vessel
                if (flightCtrl != null)
                {
                    flightCtrl.enabled = true;
                    flightCtrl.SetThrusterGlideState(-1f); // Return to idle standby pulse
                    flightCtrl.EnsureInitialized();
                }

                // 4. Update Cyberpunk HUD toast & quest objective
                if (CyberpunkHUDController.Instance != null)
                {
                    CyberpunkHUDController.Instance.ShowToast("/// LOST MARTIAN STARSHIP MATERIALIZED // BOARD VESSEL [E] ///", 6.0f);
                    CyberpunkHUDController.Instance.SetQuestObjective("REBELLION: TRANSIT", "► Embark on your Martian Starship [E] & warp to Sector 4 Black Market");
                }

                // Fade landing light over 0.8s
                float fT = 0f;
                while (fT < 0.8f)
                {
                    fT += Time.deltaTime;
                    if (landLight != null) landLight.intensity = Mathf.Lerp(8f, 0f, fT / 0.8f);
                    yield return null;
                }

                if (landingBeacon != null) Destroy(landingBeacon);
            }
        }
        #endregion
    }
}
