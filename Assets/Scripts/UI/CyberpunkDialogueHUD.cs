using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Alita.UI
{
    /// <summary>
    /// AAA Cyberpunk Dialogue HUD replicating the exact visual UI of Alita & Dr. Ido.
    /// Features:
    /// - Left-anchored stylized hexagonal portrait frame with neon glow (Purple for Alita, Cyan for Dr. Ido)
    /// - Stepped speaker name tab with hazard speed chevrons (///) and telemetry lines
    /// - Translucent dark obsidian/navy cyber glass container with 45° chamfered right corners and neon bracket caps
    /// - Cyberpunk typewriter text reveal with procedural high-tech audio chirps
    /// - Pulsing glowing downward advance triangle (▼)
    /// - Spacebar / [E] / Left-Click advance and line skip
    /// - Global integration across Prologue, Missions 1-3, Pit Arena, and Vulcan Showdown
    /// </summary>
    public class CyberpunkDialogueHUD : MonoBehaviour
    {
        public static CyberpunkDialogueHUD Instance { get; private set; }

        public enum DialogueTheme
        {
            Alita,      // Neon Purple (#A855F7) + Magenta backdrop (#EC4899)
            DoctorIdo,  // Electric Cyan (#00F0FF) + Crimson Red backdrop (#EF4444)
            RogueBot,   // Hostile Crimson Red (#FF2A3A) + Dark Carbon Chassis
            Scalper,    // Electric Cyber Gold (#FFD700) + Scrapper Cybernetics
            Vulcan,     // Fiery Amber/Orange (#FF5500)
            Custom
        }

        [Header("Hierarchy References")]
        public Canvas canvas;
        public CanvasGroup canvasGroup;
        public RectTransform rootPanel;
        public Image frameImage;
        public RectTransform portraitContainer;
        public Image portraitMaskImage;
        public Image portraitImage;
        public TextMeshProUGUI speakerNameText;
        public TextMeshProUGUI dialogueBodyText;
        public Image advanceArrowImage;
        public TextMeshProUGUI advancePromptText;
        public AudioSource audioSource;

        [Header("Theme Sprites")]
        public Sprite purpleFrameSprite;
        public Sprite cyanFrameSprite;
        public Sprite redFrameSprite;
        public Sprite goldFrameSprite;
        public Sprite alitaPortraitSprite;
        public Sprite idoPortraitSprite;
        public Sprite rogueBotPortraitSprite;
        public Sprite scalperPortraitSprite;
        public Sprite vulcanPortraitSprite;
        public Sprite hexMaskSprite;
        public Sprite purpleNextSprite;
        public Sprite cyanNextSprite;
        public Sprite redNextSprite;
        public Sprite goldNextSprite;
        public Sprite vulcanNextSprite;
        public Sprite vulcanFrameSprite;

        [Header("TMP Fonts")]
        public TMP_FontAsset headerFont;
        public TMP_FontAsset bodyFont;

        [Header("Tuning")]
        public float typeSpeed = 0.020f;
        public bool isDialogueActive = false;
        public bool inputLockedDuringDialogue = false;

        // Runtime State
        private Coroutine _typewriterCoroutine;
        private Coroutine _fadeCoroutine;
        private Coroutine _autoAdvanceCoroutine;
        private Action _onLineCompleteCallback;
        private Action _onConversationCompleteCallback;
        private List<DialogueLineData> _conversationQueue = new List<DialogueLineData>();
        private int _currentQueueIndex = 0;
        private string _targetFullText = "";
        private bool _isTyping = false;
        private float _advancePulseTimer = 0f;
        private int _openedFrame = -1;
        private float _lastInputTime = 0f;
        private const float MIN_ADVANCE_COOLDOWN = 0.22f;

        // Procedural Audio Clips
        private static AudioClip _commsOpenClip;
        private static AudioClip _typeChirpClip;
        private static AudioClip _confirmClickClip;

        // Colors
        private static readonly Color ColorAlitaPurple  = new Color(0.75f, 0.52f, 0.99f, 1f);   // #C084FC
        private static readonly Color ColorIdoCyan      = new Color(0f, 0.94f, 1f, 1f);         // #00F0FF
        private static readonly Color ColorRogueRed     = new Color(1f, 0.22f, 0.27f, 1f);      // #FF3845 Threat Red
        private static readonly Color ColorScalperGold  = new Color(1f, 0.84f, 0f, 1f);         // #FFD700 Electric Gold
        private static readonly Color ColorVulcanAmber  = new Color(1f, 0.35f, 0f, 1f);          // #FF5500

        [System.Serializable]
        public class DialogueLineData
        {
            public string speakerName;
            public string dialogueText;
            public Sprite customPortrait;
            public string speakerTitle;
            public AudioClip voiceClip;
            public float autoAdvanceTime;
            public Action onFinished;

            public DialogueLineData(string speaker, string text, Sprite portrait = null, string title = "", float autoAdvance = 0f, Action onFinish = null)
            {
                speakerName = speaker;
                dialogueText = text;
                customPortrait = portrait;
                speakerTitle = title;
                autoAdvanceTime = autoAdvance;
                onFinished = onFinish;
            }
        }

        public static CyberpunkDialogueHUD GetOrCreate()
        {
            if (Instance != null) return Instance;

            var existing = FindAnyObjectByType<CyberpunkDialogueHUD>();
            if (existing != null)
            {
                Instance = existing;
                return Instance;
            }

            GameObject go = new GameObject("CyberpunkDialogueHUD_Master");
            if (Application.isPlaying) DontDestroyOnLoad(go);
            Instance = go.AddComponent<CyberpunkDialogueHUD>();
            Instance.EnsureInitialized();
            return Instance;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrapInAllScenes()
        {
            GetOrCreate();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (Application.isPlaying) DontDestroyOnLoad(gameObject);
            EnsureInitialized();
        }

        public void EnsureInitialized()
        {
            LoadAssets();
            BuildVisualHierarchy();
            InitAudioClips();
            HideImmediate();
        }

        private void LoadAssets()
        {
#if UNITY_EDITOR
            if (purpleFrameSprite == null)
                purpleFrameSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/Dialogue/UI_Dialogue_Frame_Purple.png");
            if (cyanFrameSprite == null)
                cyanFrameSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/Dialogue/UI_Dialogue_Frame_Cyan.png");
            if (redFrameSprite == null)
                redFrameSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/Dialogue/UI_Dialogue_Frame_Red.png");
            if (goldFrameSprite == null)
                goldFrameSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/Dialogue/UI_Dialogue_Frame_Gold.png");
            if (alitaPortraitSprite == null)
                alitaPortraitSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/Dialogue/T_Alita_Dialogue_Portrait.png");
            if (idoPortraitSprite == null)
                idoPortraitSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/Dialogue/T_DoctorIdo_Dialogue_Portrait.png");
            if (rogueBotPortraitSprite == null)
                rogueBotPortraitSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/Dialogue/T_RogueBot_Dialogue_Portrait.png");
            if (scalperPortraitSprite == null)
                scalperPortraitSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/Dialogue/T_Scalper_Dialogue_Portrait.png");
            if (vulcanPortraitSprite == null)
                vulcanPortraitSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/Dialogue/T_Vulcan_Dialogue_Portrait.png");
            if (vulcanFrameSprite == null)
                vulcanFrameSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/Dialogue/UI_Dialogue_Frame_Vulcan.png");
            if (hexMaskSprite == null)
                hexMaskSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/Dialogue/UI_Dialogue_Portrait_Hex_Mask.png");
            if (purpleNextSprite == null)
                purpleNextSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/Dialogue/UI_Dialogue_Next_Purple.png");
            if (cyanNextSprite == null)
                cyanNextSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/Dialogue/UI_Dialogue_Next_Cyan.png");
            if (redNextSprite == null)
                redNextSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/Dialogue/UI_Dialogue_Next_Red.png");
            if (goldNextSprite == null)
                goldNextSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/Dialogue/UI_Dialogue_Next_Gold.png");
            if (vulcanNextSprite == null)
                vulcanNextSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/Dialogue/UI_Dialogue_Next_Amber.png");

            if (headerFont == null)
                headerFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UI/Shared/Fonts/Orbitron/Orbitron-ExtraBold SDF.asset");
            if (bodyFont == null)
                bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UI/Shared/Fonts/Orbitron/Orbitron-ExtraBold SDF.asset");

            var fallbackFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Roboto-Bold SDF.asset");
            if (bodyFont != null && fallbackFont != null)
            {
                if (bodyFont.fallbackFontAssetTable == null) bodyFont.fallbackFontAssetTable = new List<TMP_FontAsset>();
                if (!bodyFont.fallbackFontAssetTable.Contains(fallbackFont)) bodyFont.fallbackFontAssetTable.Add(fallbackFont);
            }
#endif
        }

        private void BuildVisualHierarchy()
        {
            if (canvas == null)
            {
                canvas = GetComponent<Canvas>();
                if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 1000; // Above gameplay & cutscene bars (GotG titlecard is 950)

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

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f;
                audioSource.volume = 0.75f;
            }

            // Clear any previous/stale children to guarantee a 100% clean hierarchy
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                if (Application.isPlaying) Destroy(transform.GetChild(i).gameObject);
                else DestroyImmediate(transform.GetChild(i).gameObject);
            }

            // 1. Root Dialogue Panel (Bottom-Centered)
            GameObject panelGO = new GameObject("Dialogue_Panel_Root");
            panelGO.transform.SetParent(transform, false);
            rootPanel = panelGO.AddComponent<RectTransform>();
            rootPanel.anchorMin = new Vector2(0.5f, 0f);
            rootPanel.anchorMax = new Vector2(0.5f, 0f);
            rootPanel.pivot = new Vector2(0.5f, 0f);
            rootPanel.anchoredPosition = new Vector2(0f, 42f);
            rootPanel.sizeDelta = new Vector2(1200f, 184f);

            // Click interceptor to advance on clicking the dialogue panel (avoids uGUI Selectable index bug)
            var clicker = panelGO.AddComponent<DialoguePanelClickInterceptor>();
            clicker.OnClick = OnAdvanceInput;

            // 2. Portrait Image (Sibling 0: sits BEHIND the frame, peeking through the transparent hex window)
            GameObject portraitGO = new GameObject("Portrait_Image");
            portraitGO.transform.SetParent(panelGO.transform, false);
            var pRect = portraitGO.AddComponent<RectTransform>();
            pRect.anchorMin = new Vector2(0f, 0.5f);
            pRect.anchorMax = new Vector2(0f, 0.5f);
            pRect.pivot = new Vector2(0.5f, 0.5f);
            pRect.anchoredPosition = new Vector2(139f, -3.5f);
            pRect.sizeDelta = new Vector2(172f, 157f);

            portraitImage = portraitGO.AddComponent<Image>();
            portraitImage.sprite = alitaPortraitSprite;
            portraitImage.preserveAspect = true;
            portraitImage.raycastTarget = false;

            // 3. Main Frame Image (Sibling 1: sits ON TOP of portrait, framing it with the glowing hex border)
            GameObject frameGO = new GameObject("Frame_Image");
            frameGO.transform.SetParent(panelGO.transform, false);
            var fRect = frameGO.AddComponent<RectTransform>();
            fRect.anchorMin = Vector2.zero;
            fRect.anchorMax = Vector2.one;
            fRect.offsetMin = Vector2.zero;
            fRect.offsetMax = Vector2.zero;

            frameImage = frameGO.AddComponent<Image>();
            frameImage.sprite = purpleFrameSprite;
            frameImage.type = Image.Type.Simple;
            frameImage.preserveAspect = false;
            frameImage.raycastTarget = false;

            // 4. Speaker Name Text (Sibling 2: anchored inside top stepped tab)
            GameObject nameGO = new GameObject("Speaker_Name_Text");
            nameGO.transform.SetParent(panelGO.transform, false);
            var nRect = nameGO.AddComponent<RectTransform>();
            nRect.anchorMin = new Vector2(0f, 1f);
            nRect.anchorMax = new Vector2(0f, 1f);
            nRect.pivot = new Vector2(0f, 1f);
            nRect.anchoredPosition = new Vector2(265f, -28f);
            nRect.sizeDelta = new Vector2(460f, 32f);

            speakerNameText = nameGO.AddComponent<TextMeshProUGUI>();
            if (headerFont != null) speakerNameText.font = headerFont;
            speakerNameText.text = "ALITA";
            speakerNameText.fontSize = 20f;
            speakerNameText.characterSpacing = 8f; // Clean cyber tracking
            speakerNameText.fontStyle = FontStyles.Bold;
            speakerNameText.color = ColorAlitaPurple;
            speakerNameText.alignment = TextAlignmentOptions.Left;
            speakerNameText.textWrappingMode = TextWrappingModes.NoWrap;
            speakerNameText.overflowMode = TextOverflowModes.Ellipsis;
            speakerNameText.raycastTarget = false;

            // 5. Dialogue Body Text (Sibling 3: crisp cyber geometric font with full unicode character support)
            GameObject bodyGO = new GameObject("Dialogue_Body_Text");
            bodyGO.transform.SetParent(panelGO.transform, false);
            var bRect = bodyGO.AddComponent<RectTransform>();
            bRect.anchorMin = new Vector2(0f, 0f);
            bRect.anchorMax = new Vector2(1f, 1f);
            bRect.offsetMin = new Vector2(265f, 22f);
            bRect.offsetMax = new Vector2(-115f, -65f);

            dialogueBodyText = bodyGO.AddComponent<TextMeshProUGUI>();
            if (bodyFont != null) dialogueBodyText.font = bodyFont;
            dialogueBodyText.text = "";
            dialogueBodyText.fontSize = 20.5f;
            dialogueBodyText.characterSpacing = 3f;
            dialogueBodyText.lineSpacing = 8f;
            dialogueBodyText.color = new Color(0.96f, 0.98f, 1f, 1f); // Crisp high-tech silver/white
            dialogueBodyText.alignment = TextAlignmentOptions.TopLeft;
            dialogueBodyText.textWrappingMode = TextWrappingModes.Normal;
            dialogueBodyText.raycastTarget = false;

            // 6. Advance Indicator Arrow (Sibling 4: glowing ▼)
            GameObject arrowGO = new GameObject("Advance_Indicator_Arrow");
            arrowGO.transform.SetParent(panelGO.transform, false);
            var aRect = arrowGO.AddComponent<RectTransform>();
            aRect.anchorMin = new Vector2(1f, 0f);
            aRect.anchorMax = new Vector2(1f, 0f);
            aRect.pivot = new Vector2(0.5f, 0.5f);
            aRect.anchoredPosition = new Vector2(-76f, 44f);
            aRect.sizeDelta = new Vector2(20f, 16f);

            advanceArrowImage = arrowGO.AddComponent<Image>();
            advanceArrowImage.sprite = purpleNextSprite;
            advanceArrowImage.preserveAspect = true;
            advanceArrowImage.raycastTarget = false;

            // 7. Advance Prompt Text (crisp [E] CONTINUE indicator)
            GameObject promptGO = new GameObject("Advance_Prompt_Text");
            promptGO.transform.SetParent(panelGO.transform, false);
            var prRect = promptGO.AddComponent<RectTransform>();
            prRect.anchorMin = new Vector2(1f, 0f);
            prRect.anchorMax = new Vector2(1f, 0f);
            prRect.pivot = new Vector2(1f, 0.5f);
            prRect.anchoredPosition = new Vector2(-96f, 44f);
            prRect.sizeDelta = new Vector2(220f, 26f);

            advancePromptText = promptGO.AddComponent<TextMeshProUGUI>();
            if (headerFont != null) advancePromptText.font = headerFont;
            advancePromptText.text = "[E] CONTINUE";
            advancePromptText.fontSize = 13.5f;
            advancePromptText.characterSpacing = 3f;
            advancePromptText.fontStyle = FontStyles.Bold;
            advancePromptText.color = new Color(0.85f, 0.88f, 1f, 0.85f);
            advancePromptText.alignment = TextAlignmentOptions.Right;
            advancePromptText.raycastTarget = false;
        }

        private void Update()
        {
            if (!isDialogueActive) return;

            // Critical Guard 1: Ignore input on the exact frame the dialogue was opened.
            // When the player presses [E] or [F] to talk to an NPC, GetKeyDown is true
            // across all scripts for that frame. Ignoring frame 0 prevents the opening keypress
            // from accidentally skipping the typewriter or advancing to line 2.
            if (Time.frameCount == _openedFrame) return;

            // Critical Guard 2: Cooldown debounce to prevent rapid double-taps / frame jitters
            if (Time.unscaledTime - _lastInputTime < MIN_ADVANCE_COOLDOWN) return;

            // Player input: Space, E, F, Enter, or Mouse 0
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F) ||
                Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0))
            {
                _lastInputTime = Time.unscaledTime;
                OnAdvanceInput();
            }

            // Animate pulsing advance arrow and prompt when waiting for input
            if (!_isTyping)
            {
                _advancePulseTimer += Time.unscaledDeltaTime * 6f;
                float alpha = Mathf.PingPong(_advancePulseTimer, 0.55f) + 0.45f;
                if (advanceArrowImage != null && advanceArrowImage.gameObject.activeSelf)
                {
                    Color c = advanceArrowImage.color;
                    c.a = alpha;
                    advanceArrowImage.color = c;
                }
                if (advancePromptText != null && advancePromptText.gameObject.activeSelf)
                {
                    Color pc = advancePromptText.color;
                    pc.a = alpha;
                    advancePromptText.color = pc;
                }
            }
        }

        public void OnAdvanceInput()
        {
            if (!isDialogueActive) return;

            if (_isTyping)
            {
                // Instant complete current line
                CompleteCurrentTextInstant();
            }
            else
            {
                // Advance to next line or complete
                AdvanceNext();
            }
        }

        /// <summary>
        /// Display a single line of dialogue with the full Cyberpunk UI theme.
        /// </summary>
        public void ShowDialogue(
            string speaker,
            string text,
            Sprite customPortrait = null,
            string speakerTitle = "",
            Action onComplete = null,
            float autoAdvanceDuration = 0f)
        {
            EnsureInitialized();

            // Sanitize speaker and text if text begins with "Speaker: \"body\""
            if (!string.IsNullOrEmpty(text))
            {
                text = text.Trim();
                int colonIdx = text.IndexOf(':');
                if (colonIdx > 0 && colonIdx < 35)
                {
                    string candSpk = text.Substring(0, colonIdx).Trim();
                    string candBody = text.Substring(colonIdx + 1).Trim();
                    if (!candSpk.Contains("<") && !candSpk.Contains("\n") && !candSpk.Contains("http"))
                    {
                        if (string.IsNullOrEmpty(speaker) || speaker.Equals("DR. DYSON IDO", StringComparison.OrdinalIgnoreCase) ||
                            speaker.Equals("ALITA", StringComparison.OrdinalIgnoreCase) || speaker.Equals("SYSTEM", StringComparison.OrdinalIgnoreCase))
                        {
                            speaker = candSpk;
                        }
                        text = candBody;
                    }
                }

                // Strip surrounding quotes: "Text" -> Text
                if (text.StartsWith("\"") && text.EndsWith("\"") && text.Length >= 2)
                {
                    text = text.Substring(1, text.Length - 2).Trim();
                }
            }

            _conversationQueue.Clear();
            _conversationQueue.Add(new DialogueLineData(speaker, text, customPortrait, speakerTitle, autoAdvanceDuration, onComplete));
            _currentQueueIndex = 0;
            _onLineCompleteCallback = onComplete;
            _onConversationCompleteCallback = null;

            _openedFrame = Time.frameCount;
            _lastInputTime = Time.unscaledTime;

            isDialogueActive = true;
            StartFade(1f, 0.2f);
            PlayCommsOpenSFX();
            DisplayQueueLine(0);
        }

        /// <summary>
        /// Universal subtitle display method. Automatically parses speaker and body text if prefixed with "Speaker: Text".
        /// </summary>
        public void ShowSubtitle(string rawText, float autoAdvanceDuration = 0f, Action onComplete = null)
        {
            ShowDialogue("", rawText, null, "", onComplete, autoAdvanceDuration);
        }

        /// <summary>
        /// Universal subtitle display method with explicit speaker name.
        /// </summary>
        public void ShowSubtitle(string speaker, string text, float autoAdvanceDuration = 0f, Action onComplete = null)
        {
            ShowDialogue(speaker, text, null, "", onComplete, autoAdvanceDuration);
        }

        /// <summary>
        /// Start a sequential multi-line conversation.
        /// </summary>
        public void StartConversation(List<DialogueLineData> lines, Action onAllComplete = null)
        {
            if (lines == null || lines.Count == 0)
            {
                onAllComplete?.Invoke();
                return;
            }

            EnsureInitialized();

            _conversationQueue = new List<DialogueLineData>(lines);
            _currentQueueIndex = 0;
            _onConversationCompleteCallback = onAllComplete;
            _onLineCompleteCallback = null;

            _openedFrame = Time.frameCount;
            _lastInputTime = Time.unscaledTime;

            isDialogueActive = true;
            StartFade(1f, 0.2f);
            PlayCommsOpenSFX();
            DisplayQueueLine(0);
        }

        private void DisplayQueueLine(int index)
        {
            if (index < 0 || index >= _conversationQueue.Count)
            {
                CloseDialogue();
                return;
            }

            var line = _conversationQueue[index];
            if (line.onFinished != null)
            {
                _onLineCompleteCallback = line.onFinished;
            }
            _targetFullText = line.dialogueText;

            // Apply Theme
            DialogueTheme theme = DetermineTheme(line.speakerName);
            ApplyTheme(theme, line.speakerName, line.customPortrait, line.speakerTitle);

            // Play voice clip if available: a clip set on the line wins, otherwise the generated voice for this text
            if (line.voiceClip != null && audioSource != null)
            {
                Cyberpunk.Audio.DialogueVoice.Stop();
                audioSource.clip = line.voiceClip;
                audioSource.Play();
            }
            else
            {
                var spoken = Cyberpunk.Audio.DialogueVoice.Play(line.speakerName, line.dialogueText);
                if (spoken != null && line.autoAdvanceTime > 0f)
                {
                    // Never cut a spoken line off: auto-advance waits for the voice to finish
                    line.autoAdvanceTime = Mathf.Max(line.autoAdvanceTime, spoken.length + 0.45f);
                }
            }

            // Hide advance indicators until typewriter finishes
            if (advanceArrowImage != null) advanceArrowImage.gameObject.SetActive(false);
            if (advancePromptText != null) advancePromptText.gameObject.SetActive(false);

            // Start Typewriter
            if (_typewriterCoroutine != null) StopCoroutine(_typewriterCoroutine);
            _typewriterCoroutine = StartCoroutine(TypewriterRoutine(line.dialogueText, line.autoAdvanceTime));
        }

        private DialogueTheme DetermineTheme(string speaker)
        {
            if (string.IsNullOrEmpty(speaker)) return DialogueTheme.Alita;
            string upper = speaker.ToUpper();
            if (upper.Contains("IDO") || upper.Contains("DOCTOR") || upper.Contains("DYSON") || upper.Contains("SURGEON"))
                return DialogueTheme.DoctorIdo;
            if (upper.Contains("BOT") || upper.Contains("ROUGE") || upper.Contains("ROGUE") ||
                upper.Contains("ENFORCER") || upper.Contains("RECOVERY") || upper.Contains("SQUAD") ||
                upper.Contains("VECTOR") || upper.Contains("COMBAT UNIT") || upper.Contains("DROID") ||
                upper.Contains("CORPO") || upper.Contains("CYNOSURE") || upper.Contains("EREBUS") ||
                upper.Contains("BLACKWALL") || upper.Contains("SWITCHBLADE") || upper.Contains("MECH") || upper.Contains("TITAN") ||
                upper.Contains("HAMMER") || upper.Contains("DRIFT CONTROL") || upper.Contains("SECURITY"))
                return DialogueTheme.RogueBot;
            if (upper.Contains("SCALPER") || upper.Contains("RAZOR") || upper.Contains("PUNK") ||
                upper.Contains("SCRAPPER") || upper.Contains("NINJA") || upper.Contains("THUG") ||
                upper.Contains("VOSS") || upper.Contains("KAELEN") || upper.Contains("MERCHANT") ||
                upper.Contains("FIXER") || upper.Contains("MARKET") || upper.Contains("COLLECTOR"))
                return DialogueTheme.Scalper;
            if (upper.Contains("VULCAN") || upper.Contains("FORGE") || upper.Contains("LORD"))
                return DialogueTheme.Vulcan;
            if (upper.Contains("RADIO") || upper.Contains("COMMS") || upper.Contains("COCKPIT") ||
                upper.Contains("TRANSMISSION") || upper.Contains("TERMINAL") || upper.Contains("REFINERY") ||
                upper.Contains("MAINFRAME") || upper.Contains("SYSTEM") || upper.Contains("DISPATCH"))
                return DialogueTheme.DoctorIdo;
            return DialogueTheme.Alita;
        }

        private void ApplyTheme(DialogueTheme theme, string speakerName, Sprite customPortrait, string speakerTitle = "")
        {
            switch (theme)
            {
                case DialogueTheme.Alita:
                    if (frameImage != null) frameImage.sprite = purpleFrameSprite;
                    if (advanceArrowImage != null) advanceArrowImage.sprite = purpleNextSprite;
                    if (speakerNameText != null)
                    {
                        string name = string.IsNullOrEmpty(speakerName) ? "ALITA" : speakerName.ToUpper();
                        if (name.Contains("ALITA")) name = "ALITA";
                        string formatted = name;
                        if (!string.IsNullOrEmpty(speakerTitle))
                        {
                            formatted = $"{name}  <size=70%><color=#88FFFFFF>// {speakerTitle}</color></size>";
                        }
                        speakerNameText.text = formatted;
                        speakerNameText.color = ColorAlitaPurple;
                    }
                    if (portraitImage != null)
                    {
                        portraitImage.sprite = customPortrait != null ? customPortrait : alitaPortraitSprite;
                    }
                    break;

                case DialogueTheme.DoctorIdo:
                    if (frameImage != null) frameImage.sprite = cyanFrameSprite;
                    if (advanceArrowImage != null) advanceArrowImage.sprite = cyanNextSprite;
                    if (speakerNameText != null)
                    {
                        string name = string.IsNullOrEmpty(speakerName) ? "DOCTOR IDO" : speakerName.ToUpper();
                        if (name.Contains("IDO") || name.Contains("DYSON")) name = "DOCTOR IDO";
                        speakerNameText.text = name;
                        speakerNameText.color = ColorIdoCyan;
                    }
                    if (portraitImage != null)
                    {
                        portraitImage.sprite = customPortrait != null ? customPortrait : idoPortraitSprite;
                    }
                    break;

                case DialogueTheme.RogueBot:
                    if (frameImage != null) frameImage.sprite = redFrameSprite != null ? redFrameSprite : cyanFrameSprite;
                    if (advanceArrowImage != null) advanceArrowImage.sprite = redNextSprite != null ? redNextSprite : cyanNextSprite;
                    if (speakerNameText != null)
                    {
                        string name = speakerName.ToUpper();
                        if (name.Contains("CYNOSURE") || name.Contains("EREBUS")) name = "CYNOSURE-09 // EREBUS";
                        else if (name.Contains("BLACKWALL")) name = "BLACKWALL OVERMIND";
                        else if (name.Contains("DRIFT CONTROL") || name.Contains("SECURITY")) name = "DRIFT STATION SECURITY";
                        else if (string.IsNullOrEmpty(speakerName)) name = "ROGUE COMBAT UNIT";
                        speakerNameText.text = name;
                        speakerNameText.color = ColorRogueRed;
                    }
                    if (portraitImage != null)
                    {
                        portraitImage.sprite = customPortrait != null ? customPortrait : rogueBotPortraitSprite;
                    }
                    break;

                case DialogueTheme.Scalper:
                    if (frameImage != null) frameImage.sprite = goldFrameSprite != null ? goldFrameSprite : purpleFrameSprite;
                    if (advanceArrowImage != null) advanceArrowImage.sprite = goldNextSprite != null ? goldNextSprite : purpleNextSprite;
                    if (speakerNameText != null)
                    {
                        string name = speakerName.ToUpper();
                        if (name.Contains("VOSS") || name.Contains("KAELEN") || name.Contains("COLLECTOR"))
                            name = "KAELEN VOSS // THE COLLECTOR";
                        else if (string.IsNullOrEmpty(speakerName))
                            name = "GOLD SCALPER";
                        string formatted = name;
                        if (!string.IsNullOrEmpty(speakerTitle))
                        {
                            formatted = $"{name}  <size=70%><color=#88FFFFFF>// {speakerTitle}</color></size>";
                        }
                        speakerNameText.text = formatted;
                        speakerNameText.color = ColorScalperGold;
                    }
                    if (portraitImage != null)
                    {
                        portraitImage.sprite = customPortrait != null ? customPortrait : scalperPortraitSprite;
                    }
                    break;

                case DialogueTheme.Vulcan:
                    if (frameImage != null) frameImage.sprite = vulcanFrameSprite != null ? vulcanFrameSprite : redFrameSprite;
                    if (advanceArrowImage != null) advanceArrowImage.sprite = vulcanNextSprite != null ? vulcanNextSprite : redNextSprite;
                    if (speakerNameText != null)
                    {
                        speakerNameText.text = "FORGE-LORD VULCAN";
                        speakerNameText.color = ColorVulcanAmber;
                    }
                    if (portraitImage != null)
                    {
                        portraitImage.sprite = customPortrait != null ? customPortrait : vulcanPortraitSprite;
                    }
                    break;

                default:
                    if (frameImage != null) frameImage.sprite = cyanFrameSprite;
                    if (advanceArrowImage != null) advanceArrowImage.sprite = cyanNextSprite;
                    if (speakerNameText != null)
                    {
                        speakerNameText.text = speakerName.ToUpper();
                        speakerNameText.color = ColorIdoCyan;
                    }
                    if (portraitImage != null)
                    {
                        portraitImage.sprite = customPortrait != null ? customPortrait : idoPortraitSprite;
                    }
                    break;
            }
        }

        private IEnumerator TypewriterRoutine(string fullText, float autoAdvanceTime)
        {
            _isTyping = true;
            if (dialogueBodyText != null) dialogueBodyText.text = "";

            int visibleLength = 0;
            int totalLength = fullText.Length;

            while (visibleLength < totalLength)
            {
                // Skip HTML tags if present (e.g. <color=#FF0000>)
                if (fullText[visibleLength] == '<')
                {
                    int tagClose = fullText.IndexOf('>', visibleLength);
                    if (tagClose != -1)
                    {
                        visibleLength = tagClose + 1;
                        if (dialogueBodyText != null) dialogueBodyText.text = fullText.Substring(0, visibleLength);
                        continue;
                    }
                }

                visibleLength++;
                if (dialogueBodyText != null) dialogueBodyText.text = fullText.Substring(0, visibleLength);

                // Play high-tech typewriter audio chirp every 2-3 characters
                if (visibleLength % 3 == 0)
                {
                    PlayTypeChirpSFX();
                }

                yield return new WaitForSecondsRealtime(typeSpeed);
            }

            _isTyping = false;
            OnTypingFinished(autoAdvanceTime);
        }

        private void OnTypingFinished(float autoAdvanceTime)
        {
            _isTyping = false;
            if (advanceArrowImage != null)
            {
                advanceArrowImage.gameObject.SetActive(true);
                _advancePulseTimer = 0f;
            }
            if (advancePromptText != null)
            {
                advancePromptText.gameObject.SetActive(true);
            }

            // Auto-advance if requested
            if (autoAdvanceTime > 0f)
            {
                if (_autoAdvanceCoroutine != null) StopCoroutine(_autoAdvanceCoroutine);
                _autoAdvanceCoroutine = StartCoroutine(AutoAdvanceRoutine(autoAdvanceTime));
            }
        }

        private IEnumerator AutoAdvanceRoutine(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            AdvanceNext();
        }

        public void CompleteCurrentTextInstant()
        {
            if (!_isTyping) return;

            if (_typewriterCoroutine != null) StopCoroutine(_typewriterCoroutine);
            _isTyping = false;
            if (dialogueBodyText != null) dialogueBodyText.text = _targetFullText;
            _lastInputTime = Time.unscaledTime;

            OnTypingFinished(_conversationQueue.Count > _currentQueueIndex ? _conversationQueue[_currentQueueIndex].autoAdvanceTime : 0f);
        }

        public void AdvanceNext()
        {
            if (_autoAdvanceCoroutine != null) StopCoroutine(_autoAdvanceCoroutine);
            Cyberpunk.Audio.DialogueVoice.Stop();

            PlayConfirmClickSFX();

            // Case 1: Multi-line queue with subsequent line waiting
            _currentQueueIndex++;
            if (_currentQueueIndex < _conversationQueue.Count)
            {
                var lineCb = _onLineCompleteCallback;
                _onLineCompleteCallback = null;
                lineCb?.Invoke();

                DisplayQueueLine(_currentQueueIndex);
                return;
            }

            // Case 2: End of queue (single-line ShowDialogue or final line of StartConversation).
            // Fire BOTH the last line's callback and the conversation-complete callback;
            // a per-line callback must never swallow the conversation-complete one.
            var lineDoneCb = _onLineCompleteCallback;
            var convDoneCb = _onConversationCompleteCallback;
            _onLineCompleteCallback = null;
            _onConversationCompleteCallback = null;

            var queueBefore = _conversationQueue;
            int queueIndexBefore = _currentQueueIndex;

            lineDoneCb?.Invoke();
            convDoneCb?.Invoke();

            // If a callback loaded a new dialogue line (e.g. ShowDialogue) or started another
            // conversation, keep the dialogue HUD active and do not close it!
            if (isDialogueActive && (!ReferenceEquals(_conversationQueue, queueBefore) || _currentQueueIndex != queueIndexBefore))
            {
                return;
            }

            // No further lines were queued by the callback: close cleanly
            CloseDialogue();
        }

        public void CloseDialogue(float fadeDuration = 0.25f)
        {
            Cyberpunk.Audio.DialogueVoice.Stop();
            if (_typewriterCoroutine != null) StopCoroutine(_typewriterCoroutine);
            if (_autoAdvanceCoroutine != null) StopCoroutine(_autoAdvanceCoroutine);
            _isTyping = false;
            isDialogueActive = false;

            StartFade(0f, fadeDuration, () =>
            {
                var convCb = _onConversationCompleteCallback;
                _onConversationCompleteCallback = null;
                convCb?.Invoke();
            });
        }

        public void HideImmediate()
        {
            Cyberpunk.Audio.DialogueVoice.Stop();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
            if (advanceArrowImage != null) advanceArrowImage.gameObject.SetActive(false);
            if (advancePromptText != null) advancePromptText.gameObject.SetActive(false);
            isDialogueActive = false;
        }

        private void StartFade(float targetAlpha, float duration, Action onFinished = null)
        {
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeRoutine(targetAlpha, duration, onFinished));
        }

        private IEnumerator FadeRoutine(float targetAlpha, float duration, Action onFinished)
        {
            if (canvasGroup == null) yield break;

            canvasGroup.interactable = targetAlpha > 0.5f;
            canvasGroup.blocksRaycasts = targetAlpha > 0.5f;

            float startAlpha = canvasGroup.alpha;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
                yield return null;
            }
            canvasGroup.alpha = targetAlpha;
            onFinished?.Invoke();
        }

        #region Procedural Audio SFX Generation

        private void InitAudioClips()
        {
            if (_commsOpenClip == null) _commsOpenClip = GenerateCommsOpenClip();
            if (_typeChirpClip == null) _typeChirpClip = GenerateTypeChirpClip();
            if (_confirmClickClip == null) _confirmClickClip = GenerateConfirmClickClip();
        }

        private void PlayCommsOpenSFX()
        {
            if (audioSource == null) return;
            if (_commsOpenClip == null) _commsOpenClip = GenerateCommsOpenClip();
            audioSource.pitch = 1.0f;
            audioSource.PlayOneShot(_commsOpenClip, 0.65f);
        }

        private void PlayTypeChirpSFX()
        {
            if (audioSource == null) return;
            if (_typeChirpClip == null) _typeChirpClip = GenerateTypeChirpClip();
            audioSource.pitch = UnityEngine.Random.Range(0.96f, 1.04f);
            audioSource.PlayOneShot(_typeChirpClip, 0.40f);
        }

        private void PlayConfirmClickSFX()
        {
            if (audioSource == null) return;
            if (_confirmClickClip == null) _confirmClickClip = GenerateConfirmClickClip();
            audioSource.pitch = 1.05f;
            audioSource.PlayOneShot(_confirmClickClip, 0.70f);
        }

        // Futuristic 70ms comms squelch / radio open beep
        private static AudioClip GenerateCommsOpenClip()
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * 0.085f);
            AudioClip clip = AudioClip.Create("SFX_Comms_Open", samples, 1, sampleRate, false);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float env = Mathf.Sin(t / 0.085f * Mathf.PI);
                float f1 = Mathf.Sin(t * Mathf.PI * 2f * 2400f);
                float f2 = Mathf.Sin(t * Mathf.PI * 2f * 1850f);
                data[i] = (f1 * 0.6f + f2 * 0.4f) * env * 0.25f;
            }
            clip.SetData(data, 0);
            return clip;
        }

        // Subtle 12ms tactile cyber typewriter click
        private static AudioClip GenerateTypeChirpClip()
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * 0.015f);
            AudioClip clip = AudioClip.Create("SFX_Type_Chirp", samples, 1, sampleRate, false);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                float env = 1f - t;
                data[i] = (Mathf.Sin(t * Mathf.PI * 18f) * 0.7f + (UnityEngine.Random.value * 2f - 1f) * 0.3f) * env * 0.18f;
            }
            clip.SetData(data, 0);
            return clip;
        }

        // Crisp 45ms futuristic confirmation advance beep
        private static AudioClip GenerateConfirmClickClip()
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * 0.055f);
            AudioClip clip = AudioClip.Create("SFX_Confirm_Click", samples, 1, sampleRate, false);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float env = Mathf.Exp(-t * 50f);
                float f = Mathf.Sin(t * Mathf.PI * 2f * 1480f);
                data[i] = f * env * 0.28f;
            }
            clip.SetData(data, 0);
            return clip;
        }

        #endregion
    }

    internal class DialoguePanelClickInterceptor : MonoBehaviour, UnityEngine.EventSystems.IPointerClickHandler
    {
        public System.Action OnClick;
        public void OnPointerClick(UnityEngine.EventSystems.PointerEventData eventData) => OnClick?.Invoke();
    }
}
