using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SyntyCombined.Cutscene
{
    /// <summary>
    /// Guardians of the Galaxy Style Retro Title Card Splash UI.
    /// Features:
    /// - Cinematic 2.39:1 Letterbox Bars (Top & Bottom black bars)
    /// - Bold 80s retro title slam ("DRIFT") with chromatic glow offset
    /// - Outlaw sector subtitle & coordinates readout
    /// - Dynamic zoom punch & ease-in
    /// - Clean fadeout transition into gameplay
    /// - Built procedurally via Unity UI & TextMeshPro
    /// </summary>
    public class GotGTitleCardUI : MonoBehaviour
    {
        public static GotGTitleCardUI Instance { get; private set; }

        [Header("Title Text Configuration")]
        public string locationTitle = "DRIFT";
        public string locationSubtitle = "SECTOR 04 // UNREGULATED OUTLAW MARKET";
        public string locationDetails = "CYGNUS-9 EXPEDITION  •  LOC: 39.402, -112.84  •  STATUS: UNGOVERNED";

        [Header("Colors & Styling")]
        public Color titleColor = new Color(1.0f, 0.84f, 0.05f, 1.0f);        // Retro 80s Gold / GotG Yellow
        public Color titleShadowColor = new Color(0.95f, 0.12f, 0.35f, 0.75f); // Chromatic Magenta Shadow
        public Color subtitleColor = new Color(0.0f, 0.94f, 1.0f, 1.0f);       // Neon Kiroshi Cyan
        public Color detailsColor = new Color(0.85f, 0.88f, 0.92f, 0.85f);     // Soft Muted White
        public Color letterboxColor = new Color(0.02f, 0.02f, 0.03f, 0.96f);

        [Header("Timing")]
        public float displayDuration = 5.5f;

        // UI references
        private Canvas _canvas;
        private CanvasGroup _mainCanvasGroup;
        private RectTransform _topLetterbox;
        private RectTransform _bottomLetterbox;
        private RectTransform _titleContainer;
        private TextMeshProUGUI _titleMain;
        private TextMeshProUGUI _titleShadow;
        private TextMeshProUGUI _subtitleText;
        private TextMeshProUGUI _detailsText;
        private CanvasGroup _skipPromptGroup;

        private Coroutine _activeAnimationRoutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            BuildUIHierarchy();
        }

        public static GotGTitleCardUI GetOrCreate()
        {
            if (Instance != null) return Instance;

            var existing = Object.FindAnyObjectByType<GotGTitleCardUI>();
            if (existing != null)
            {
                Instance = existing;
                return Instance;
            }

            GameObject go = new GameObject("[GotG_TitleCard_UI]");
            Instance = go.AddComponent<GotGTitleCardUI>();
            return Instance;
        }

        private void BuildUIHierarchy()
        {
            // Canvas setup
            _canvas = GetComponent<Canvas>();
            if (_canvas == null) _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 950; // In front of normal HUD, below pause menu

            var scaler = GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }

            _mainCanvasGroup = GetComponent<CanvasGroup>();
            if (_mainCanvasGroup == null) _mainCanvasGroup = gameObject.AddComponent<CanvasGroup>();
            _mainCanvasGroup.alpha = 0f;
            _mainCanvasGroup.blocksRaycasts = false;

            // Load fonts
            TMP_FontAsset boldFont = Resources.Load<TMP_FontAsset>("SyntyUI/Fonts/Font_SciFi_Bold");
            TMP_FontAsset headerFont = Resources.Load<TMP_FontAsset>("SyntyUI/Fonts/Font_SciFi_Header");
            if (boldFont == null) boldFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

            // 1. Cinematic Letterbox Bars
            _topLetterbox = CreateLetterboxBar("Top_Letterbox", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), 130f);
            _bottomLetterbox = CreateLetterboxBar("Bottom_Letterbox", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), 130f);

            // 2. Title Container (Centered slightly above lower third for classic GotG presentation)
            GameObject titleContainerObj = new GameObject("Title_Container");
            titleContainerObj.transform.SetParent(transform, false);
            _titleContainer = titleContainerObj.AddComponent<RectTransform>();
            _titleContainer.anchorMin = new Vector2(0.5f, 0.42f);
            _titleContainer.anchorMax = new Vector2(0.5f, 0.42f);
            _titleContainer.pivot = new Vector2(0.5f, 0.5f);
            _titleContainer.sizeDelta = new Vector2(1200f, 320f);

            // Decorative Accent Lines (Retro cyan frames)
            CreateAccentLine(_titleContainer, "AccentLine_Top", new Vector2(0f, 85f), 780f, subtitleColor);
            CreateAccentLine(_titleContainer, "AccentLine_Bottom", new Vector2(0f, -65f), 780f, subtitleColor);

            // Chromatic Shadow Title
            _titleShadow = CreateText(_titleContainer.gameObject, "Text_Title_Shadow", locationTitle, 110f,
                TextAlignmentOptions.Center, new Vector2(4f, 8f), titleShadowColor, boldFont);
            _titleShadow.fontStyle = FontStyles.Bold;
            _titleShadow.characterSpacing = 16f;

            // Main GotG Title
            _titleMain = CreateText(_titleContainer.gameObject, "Text_Title_Main", locationTitle, 110f,
                TextAlignmentOptions.Center, new Vector2(0f, 12f), titleColor, boldFont);
            _titleMain.fontStyle = FontStyles.Bold;
            _titleMain.characterSpacing = 16f;

            // Subtitle
            _subtitleText = CreateText(_titleContainer.gameObject, "Text_Subtitle", locationSubtitle, 24f,
                TextAlignmentOptions.Center, new Vector2(0f, -42f), subtitleColor, boldFont);
            _subtitleText.fontStyle = FontStyles.Bold;
            _subtitleText.characterSpacing = 12f;

            // Coordinates & System Details
            _detailsText = CreateText(_titleContainer.gameObject, "Text_Details", locationDetails, 13.5f,
                TextAlignmentOptions.Center, new Vector2(0f, -88f), detailsColor, headerFont);
            _detailsText.characterSpacing = 8f;

            // 3. Skip Cinematic Prompt (Bottom Right)
            GameObject skipObj = new GameObject("Skip_Prompt");
            skipObj.transform.SetParent(transform, false);
            var skipRT = skipObj.AddComponent<RectTransform>();
            skipRT.anchorMin = new Vector2(1f, 0f);
            skipRT.anchorMax = new Vector2(1f, 0f);
            skipRT.pivot = new Vector2(1f, 0f);
            skipRT.anchoredPosition = new Vector2(-40f, 32f);
            skipRT.sizeDelta = new Vector2(300f, 40f);

            _skipPromptGroup = skipObj.AddComponent<CanvasGroup>();
            _skipPromptGroup.alpha = 0.85f;

            var skipText = CreateText(skipObj, "Text_Skip", "[SPACE] / [ESC] SKIP CINEMATIC", 13f,
                TextAlignmentOptions.Right, Vector2.zero, new Color(0.7f, 0.75f, 0.8f, 0.9f), headerFont);
            skipText.characterSpacing = 4f;
        }

        private RectTransform CreateLetterboxBar(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, float height)
        {
            GameObject barObj = new GameObject(name);
            barObj.transform.SetParent(transform, false);
            var rt = barObj.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.sizeDelta = new Vector2(0f, height);
            rt.anchoredPosition = Vector2.zero;

            var img = barObj.AddComponent<Image>();
            img.color = letterboxColor;
            img.raycastTarget = false;
            return rt;
        }

        private void CreateAccentLine(Transform parent, string name, Vector2 pos, float width, Color col)
        {
            GameObject line = new GameObject(name);
            line.transform.SetParent(parent, false);
            var rt = line.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(width, 2.5f);

            var img = line.AddComponent<Image>();
            img.color = new Color(col.r, col.g, col.b, 0.65f);
            img.raycastTarget = false;
        }

        private TextMeshProUGUI CreateText(GameObject parent, string name, string text, float fontSize,
            TextAlignmentOptions align, Vector2 pos, Color color, TMP_FontAsset font)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent.transform, false);
            var rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(1100f, 150f);

            var tmp = obj.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = align;
            tmp.color = color;
            tmp.raycastTarget = false;
            return tmp;
        }

        /// <summary>
        /// Plays the retro GotG slam title animation
        /// </summary>
        public void ShowTitleCard(string title = null, string subtitle = null, string details = null, float duration = -1f)
        {
            if (!string.IsNullOrEmpty(title)) locationTitle = title;
            if (!string.IsNullOrEmpty(subtitle)) locationSubtitle = subtitle;
            if (!string.IsNullOrEmpty(details)) locationDetails = details;
            if (duration > 0f) displayDuration = duration;

            if (_titleMain != null) _titleMain.text = locationTitle;
            if (_titleShadow != null) _titleShadow.text = locationTitle;
            if (_subtitleText != null) _subtitleText.text = locationSubtitle;
            if (_detailsText != null) _detailsText.text = locationDetails;

            if (_activeAnimationRoutine != null) StopCoroutine(_activeAnimationRoutine);
            _activeAnimationRoutine = StartCoroutine(TitleCardAnimationRoutine());
        }

        /// <summary>
        /// Hides the title card immediately or fades it out
        /// </summary>
        public void HideTitleCard(bool instant = false)
        {
            if (_activeAnimationRoutine != null) StopCoroutine(_activeAnimationRoutine);
            if (instant)
            {
                if (_mainCanvasGroup != null) _mainCanvasGroup.alpha = 0f;
            }
            else
            {
                _activeAnimationRoutine = StartCoroutine(FadeOutRoutine(0.5f));
            }
        }

        /// <summary>
        /// Keeps letterbox bars active while fading out title text
        /// </summary>
        public void SetLetterboxOnly(bool active, float fadeDuration = 0.6f)
        {
            StartCoroutine(SetLetterboxRoutine(active, fadeDuration));
        }

        private IEnumerator TitleCardAnimationRoutine()
        {
            // Initial state: letterboxes in, title scaled up and invisible
            _mainCanvasGroup.alpha = 1f;
            _titleContainer.localScale = Vector3.one * 1.45f;
            _titleContainer.gameObject.SetActive(true);

            // Letterbox bar slide in
            float barDuration = 0.45f;
            float elapsed = 0f;
            while (elapsed < barDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / barDuration);
                _topLetterbox.anchoredPosition = new Vector2(0f, Mathf.Lerp(140f, 0f, t));
                _bottomLetterbox.anchoredPosition = new Vector2(0f, Mathf.Lerp(-140f, 0f, t));
                yield return null;
            }
            _topLetterbox.anchoredPosition = Vector2.zero;
            _bottomLetterbox.anchoredPosition = Vector2.zero;

            // Slam in punch animation (1.45 -> 0.96 -> 1.0)
            float slamDuration = 0.35f;
            elapsed = 0f;
            while (elapsed < slamDuration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / slamDuration;
                // Ease out elastic punch
                float scale = Mathf.Lerp(1.45f, 0.96f, Mathf.Sin(progress * Mathf.PI * 0.5f));
                _titleContainer.localScale = Vector3.one * scale;

                // Subtle chromatic jitter during impact
                float jitter = (1f - progress) * 6f;
                _titleShadow.rectTransform.anchoredPosition = new Vector2(
                    4f + Random.Range(-jitter, jitter),
                    8f + Random.Range(-jitter, jitter)
                );
                yield return null;
            }

            // Settle to 1.0
            elapsed = 0f;
            while (elapsed < 0.15f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / 0.15f;
                _titleContainer.localScale = Vector3.Lerp(Vector3.one * 0.96f, Vector3.one, t);
                yield return null;
            }
            _titleContainer.localScale = Vector3.one;
            _titleShadow.rectTransform.anchoredPosition = new Vector2(4f, 8f);

            // Sustained display with subtle camera breathing
            float holdTime = displayDuration;
            elapsed = 0f;
            while (elapsed < holdTime)
            {
                elapsed += Time.deltaTime;
                // Very subtle slow drift (GotG classic 70mm anamorphic feel)
                float slowScale = 1.0f + (elapsed / holdTime) * 0.04f;
                _titleContainer.localScale = Vector3.one * slowScale;
                yield return null;
            }

            // Smooth fade out of title texts (letterbox remains until landing)
            yield return StartCoroutine(FadeTitleTextOnlyRoutine(0.8f));
        }

        private IEnumerator FadeTitleTextOnlyRoutine(float duration)
        {
            float elapsed = 0f;
            Color initMain = _titleMain.color;
            Color initShadow = _titleShadow.color;
            Color initSub = _subtitleText.color;
            Color initDet = _detailsText.color;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = 1f - Mathf.Clamp01(elapsed / duration);

                _titleMain.color = new Color(initMain.r, initMain.g, initMain.b, initMain.a * alpha);
                _titleShadow.color = new Color(initShadow.r, initShadow.g, initShadow.b, initShadow.a * alpha);
                _subtitleText.color = new Color(initSub.r, initSub.g, initSub.b, initSub.a * alpha);
                _detailsText.color = new Color(initDet.r, initDet.g, initDet.b, initDet.a * alpha);
                yield return null;
            }

            _titleContainer.gameObject.SetActive(false);
        }

        private IEnumerator SetLetterboxRoutine(bool active, float duration)
        {
            float elapsed = 0f;
            Vector2 startTop = _topLetterbox.anchoredPosition;
            Vector2 endTop = active ? Vector2.zero : new Vector2(0f, 140f);
            Vector2 startBottom = _bottomLetterbox.anchoredPosition;
            Vector2 endBottom = active ? Vector2.zero : new Vector2(0f, -140f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                _topLetterbox.anchoredPosition = Vector2.Lerp(startTop, endTop, t);
                _bottomLetterbox.anchoredPosition = Vector2.Lerp(startBottom, endBottom, t);
                yield return null;
            }

            _topLetterbox.anchoredPosition = endTop;
            _bottomLetterbox.anchoredPosition = endBottom;

            if (!active)
            {
                _mainCanvasGroup.alpha = 0f;
            }
        }

        private IEnumerator FadeOutRoutine(float duration)
        {
            float startAlpha = _mainCanvasGroup.alpha;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                _mainCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / duration);
                yield return null;
            }
            _mainCanvasGroup.alpha = 0f;
        }
    }
}
