using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Alita.UI
{
    /// <summary>
    /// Aesthetic Cyberpunk holographic notification popup displayed when the Server Room
    /// is unlocked following Dr. Ido's assassination on the Sector 7 skybridge.
    /// Features:
    /// - High-tech glassmorphic panel with neon cyan & amber glowing border accents
    /// - Holographic badge header, chromatic title, and directive intel
    /// - Smooth elastic punch-in animation and gentle audio chime
    /// </summary>
    public class ServerRoomUnlockNotificationUI : MonoBehaviour
    {
        public static ServerRoomUnlockNotificationUI Instance { get; private set; }

        private Canvas _canvas;
        private CanvasGroup _canvasGroup;
        private RectTransform _panelRect;
        private Image _bgImage;
        private Image _glowBorder;
        private TextMeshProUGUI _badgeText;
        private TextMeshProUGUI _titleText;
        private TextMeshProUGUI _subtitleText;
        private AudioSource _audioSource;
        private AudioClip _unlockChime;

        private Coroutine _displayRoutine;

        public static ServerRoomUnlockNotificationUI GetOrCreate()
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<ServerRoomUnlockNotificationUI>();
            if (existing != null) { Instance = existing; return Instance; }

            var go = new GameObject("[ServerRoom_Unlock_Notification_UI]");
            Instance = go.AddComponent<ServerRoomUnlockNotificationUI>();
            return Instance;
        }

        public static void Show(string title = "SERVER ROOM UNLOCKED",
                                string badge = "SUB-NET PROTOCOL : ACCESS GRANTED",
                                string directive = "> Dr. Ido's final transmission decrypted: Blackwall Datacenter Archive 09 is accessible.\n> Follow cyber-nav trail to Server Room entrance [E]",
                                float duration = 5.5f)
        {
            var ui = GetOrCreate();
            ui.DisplayNotification(title, badge, directive, duration);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            BuildUI();
        }

        private void BuildUI()
        {
            _canvas = GetComponent<Canvas>();
            if (_canvas == null) _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 960; // In front of normal HUD

            var scaler = GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();

            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;

            // Audio
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f;

#if UNITY_EDITOR
            _unlockChime = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Combat/Dash_Cyber_01.wav")
                        ?? AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Combat/Sword_Slash_Piercing_Thrust_01.wav");
#endif

            // Main Notification Panel (Top-Center)
            var panelObj = new GameObject("Notification_Panel");
            panelObj.transform.SetParent(transform, false);
            _panelRect = panelObj.AddComponent<RectTransform>();
            _panelRect.anchorMin = new Vector2(0.5f, 1.0f);
            _panelRect.anchorMax = new Vector2(0.5f, 1.0f);
            _panelRect.pivot = new Vector2(0.5f, 1.0f);
            _panelRect.anchoredPosition = new Vector2(0f, -65f);
            _panelRect.sizeDelta = new Vector2(720f, 130f);

            // Background Glass Panel
            _bgImage = panelObj.AddComponent<Image>();
            _bgImage.color = new Color(0.015f, 0.04f, 0.07f, 0.93f);

            // Sprite if available
            Sprite boxSprite = null;
#if UNITY_EDITOR
            boxSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/SyntyUI/Sprites/HUD/SPR_HUD_SciFi_Box_Glass_A.png")
                     ?? AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/SyntyUI/Sprites/HUD/SPR_HUD_SciFi_Frame_Corners_A.png");
#endif
            if (boxSprite == null)
            {
                boxSprite = Resources.Load<Sprite>("SyntyUI/Sprites/HUD/SPR_HUD_SciFi_Box_Glass_A");
            }
            if (boxSprite != null)
            {
                _bgImage.sprite = boxSprite;
                _bgImage.type = Image.Type.Sliced;
            }

            // Glow Border Accent
            var borderObj = new GameObject("Glow_Border");
            borderObj.transform.SetParent(panelObj.transform, false);
            var bRect = borderObj.AddComponent<RectTransform>();
            bRect.anchorMin = Vector2.zero;
            bRect.anchorMax = Vector2.one;
            bRect.sizeDelta = new Vector2(4f, 4f);
            _glowBorder = borderObj.AddComponent<Image>();
            _glowBorder.color = new Color(0f, 0.92f, 1.0f, 0.45f);

            Sprite borderSprite = null;
#if UNITY_EDITOR
            borderSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/SyntyUI/Sprites/HUD/SPR_HUD_SciFi_Frame_Corners_A.png");
#endif
            if (borderSprite != null)
            {
                _glowBorder.sprite = borderSprite;
                _glowBorder.type = Image.Type.Sliced;
            }

            // Cyan Accent Top Stripe
            var stripeObj = new GameObject("Cyan_Stripe");
            stripeObj.transform.SetParent(panelObj.transform, false);
            var sRect = stripeObj.AddComponent<RectTransform>();
            sRect.anchorMin = new Vector2(0f, 1f);
            sRect.anchorMax = new Vector2(1f, 1f);
            sRect.pivot = new Vector2(0.5f, 1f);
            sRect.anchoredPosition = Vector2.zero;
            sRect.sizeDelta = new Vector2(0f, 3.5f);
            var stripeImg = stripeObj.AddComponent<Image>();
            stripeImg.color = new Color(0f, 0.95f, 1f, 1f);

            // 1. Badge Text (Top amber / gold header)
            var badgeObj = new GameObject("Badge_Text");
            badgeObj.transform.SetParent(panelObj.transform, false);
            var badgeRect = badgeObj.AddComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0f, 1f);
            badgeRect.anchorMax = new Vector2(1f, 1f);
            badgeRect.pivot = new Vector2(0f, 1f);
            badgeRect.anchoredPosition = new Vector2(24f, -14f);
            badgeRect.sizeDelta = new Vector2(670f, 22f);

            _badgeText = badgeObj.AddComponent<TextMeshProUGUI>();
            _badgeText.fontSize = 12.5f;
            _badgeText.fontStyle = FontStyles.Bold;
            _badgeText.characterSpacing = 2.5f;
            _badgeText.color = new Color(1.0f, 0.78f, 0.15f, 0.95f); // Amber / Gold
            _badgeText.alignment = TextAlignmentOptions.Left;

            // 2. Title Text (Vibrant Neon Cyan)
            var titleObj = new GameObject("Title_Text");
            titleObj.transform.SetParent(panelObj.transform, false);
            var titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0f, 1f);
            titleRect.anchoredPosition = new Vector2(24f, -38f);
            titleRect.sizeDelta = new Vector2(670f, 38f);

            _titleText = titleObj.AddComponent<TextMeshProUGUI>();
            _titleText.fontSize = 24f;
            _titleText.fontStyle = FontStyles.Bold;
            _titleText.characterSpacing = 3.5f;
            _titleText.color = new Color(0f, 0.95f, 1f, 1f); // Kiroshi Neon Cyan
            _titleText.alignment = TextAlignmentOptions.Left;

            // 3. Subtitle / Directive Text
            var subObj = new GameObject("Subtitle_Text");
            subObj.transform.SetParent(panelObj.transform, false);
            var subRect = subObj.AddComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0f, 0f);
            subRect.anchorMax = new Vector2(1f, 0f);
            subRect.pivot = new Vector2(0f, 0f);
            subRect.anchoredPosition = new Vector2(24f, 12f);
            subRect.sizeDelta = new Vector2(670f, 42f);

            _subtitleText = subObj.AddComponent<TextMeshProUGUI>();
            _subtitleText.fontSize = 13f;
            _subtitleText.lineSpacing = 1.2f;
            _subtitleText.color = new Color(0.85f, 0.88f, 0.94f, 0.90f);
            _subtitleText.alignment = TextAlignmentOptions.Left;
        }

        public void DisplayNotification(string title, string badge, string directive, float duration)
        {
            if (_panelRect == null) BuildUI();

            if (_badgeText != null) _badgeText.text = badge;
            if (_titleText != null) _titleText.text = title;
            if (_subtitleText != null) _subtitleText.text = directive;

            if (_displayRoutine != null) StopCoroutine(_displayRoutine);
            _displayRoutine = StartCoroutine(NotificationRoutine(duration));
        }

        private IEnumerator NotificationRoutine(float duration)
        {
            if (_audioSource != null)
            {
                if (_unlockChime != null) _audioSource.PlayOneShot(_unlockChime, 0.85f);
                else PlayProceduralCyberChime();
            }

            _canvasGroup.alpha = 0f;
            Vector2 startPos = new Vector2(0f, -30f);
            Vector2 targetPos = new Vector2(0f, -65f);
            _panelRect.anchoredPosition = startPos;
            _panelRect.localScale = Vector3.one * 0.92f;

            // Punch In
            float elapsed = 0f;
            float inDur = 0.40f;
            while (elapsed < inDur)
            {
                float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                elapsed += dt;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / inDur));
                _canvasGroup.alpha = t;
                _panelRect.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
                _panelRect.localScale = Vector3.Lerp(Vector3.one * 0.92f, Vector3.one, Mathf.Sin(t * Mathf.PI * 0.5f));
                yield return null;
            }
            _canvasGroup.alpha = 1f;
            _panelRect.anchoredPosition = targetPos;
            _panelRect.localScale = Vector3.one;

            // Hold & subtle breathing border pulse
            float holdTime = 0f;
            while (holdTime < duration)
            {
                float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                holdTime += dt;
                if (_glowBorder != null)
                {
                    float pulse = 0.4f + Mathf.PingPong(holdTime * 2.5f, 0.45f);
                    _glowBorder.color = new Color(0f, 0.92f, 1.0f, pulse);
                }
                yield return null;
            }

            // Fade & Slide Out
            elapsed = 0f;
            float outDur = 0.50f;
            Vector2 exitPos = new Vector2(0f, -20f);
            while (elapsed < outDur)
            {
                float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                elapsed += dt;
                float t = Mathf.Clamp01(elapsed / outDur);
                _canvasGroup.alpha = 1f - t;
                _panelRect.anchoredPosition = Vector2.Lerp(targetPos, exitPos, t);
                yield return null;
            }
            _canvasGroup.alpha = 0f;
            _displayRoutine = null;
        }

        private void PlayProceduralCyberChime()
        {
            int sampleRate = 44100;
            float dur = 0.45f;
            int numSamples = (int)(sampleRate * dur);
            float[] samples = new float[numSamples];
            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float f1 = 880f; // A5
                float f2 = 1320f; // E6
                float env = Mathf.Exp(-t * 9f);
                samples[i] = (Mathf.Sin(2f * Mathf.PI * f1 * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * f2 * t) * 0.5f) * env * 0.45f;
            }
            AudioClip clip = AudioClip.Create("Proc_CyberChime", numSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            _audioSource.PlayOneShot(clip, 0.8f);
        }
    }
}
