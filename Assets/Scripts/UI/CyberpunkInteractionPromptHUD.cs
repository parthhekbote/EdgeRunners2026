using System;
using UnityEngine;

namespace Cyberpunk.UI
{
    /// <summary>
    /// Unified AAA Cyberpunk Interaction Prompt HUD widget.
    /// Replaces all legacy/mismatched interaction prompts across the game with a 
    /// high-fidelity, Star Citizen / Cyberpunk 2077 styled keycap badge and action label:
    /// 
    ///     [ F ]  Embark [Hold]
    /// 
    /// Features:
    /// - Procedurally generated signed-distance-field (SDF) anti-aliased rounded keycap badge
    /// - Glowing electric cyan border (#00F0FF / #38EFDF) and translucent dark slate interior
    /// - Centered key label matching the luminous cyan hue
    /// - Sleek cyan action text with high-contrast drop shadow
    /// - Bottom-to-top hold gauge fill + animated lower progress line when holding
    /// - Usable via immediate-mode (DrawPromptImmediate) or per-frame registration (ShowPrompt)
    /// - Auto-instantiated singleton with DontDestroyOnLoad
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class CyberpunkInteractionPromptHUD : MonoBehaviour
    {
        public static CyberpunkInteractionPromptHUD Instance { get; private set; }

        // Colors matching reference image (luminous electric cyan / dark slate)
        public static readonly Color CyanPrimary = new Color(0.15f, 0.94f, 0.98f, 1.0f);
        public static readonly Color CyanGlow = new Color(0.20f, 0.98f, 1.0f, 0.70f);
        public static readonly Color DarkSlateBg = new Color(0.04f, 0.09f, 0.13f, 0.88f);
        public static readonly Color ShadowColor = new Color(0.0f, 0.0f, 0.0f, 0.88f);

        // Active prompt state for singleton per-frame usage
        private static string _activeKey;
        private static string _activeAction;
        private static float _activeHoldProgress;
        private static Vector2? _activeScreenPos;
        private static float _lastRegisteredTime;

        // Multi-prompt frame stacking & deduplication
        private static int _lastDrawnFrame = -1;
        private static EventType _lastEventType = EventType.Ignore;
        private static int _promptsDrawnThisFrame = 0;
        private static readonly System.Collections.Generic.HashSet<string> _drawnKeysThisFrame = new System.Collections.Generic.HashSet<string>();

        // Cached GUI Resources
        private static Texture2D _keycapTex;
        private static Texture2D _keycapSolidTex;
        private static Texture2D _whiteTex;
        private static GUIStyle _keyStyle;
        private static GUIStyle _actionStyle;
        private static GUIStyle _shadowStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance == null)
            {
                var go = new GameObject("[Cyberpunk_InteractionPromptHUD]");
                DontDestroyOnLoad(go);
                Instance = go.AddComponent<CyberpunkInteractionPromptHUD>();
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
            DontDestroyOnLoad(gameObject);
            EnsureTextures();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Call this in Update() from any trigger script. The prompt will be displayed
        /// during the current frame's OnGUI render pass.
        /// </summary>
        public static void ShowPrompt(string keyLabel, string actionLabel, float holdProgress = 0f, Vector2? screenPos = null)
        {
            _activeKey = keyLabel;
            _activeAction = actionLabel;
            _activeHoldProgress = Mathf.Clamp01(holdProgress);
            _activeScreenPos = screenPos;
            _lastRegisteredTime = Time.unscaledTime;
        }

        /// <summary>
        /// Clears the currently registered prompt.
        /// </summary>
        public static void HidePrompt()
        {
            _lastRegisteredTime = 0f;
        }

        private void OnGUI()
        {
            if (SyntyCombined.Cutscene.GlobalCutsceneState.IsCutsceneActive) return;

            // Only draw if registered in the last 0.1s
            if (Time.unscaledTime - _lastRegisteredTime < 0.12f && !string.IsNullOrEmpty(_activeAction))
            {
                DrawPromptImmediate(_activeKey, _activeAction, _activeHoldProgress, _activeScreenPos);
            }
        }

        /// <summary>
        /// Immediate-mode draw call that can be executed directly inside any OnGUI() method.
        /// Automatically stacks multiple simultaneous prompts vertically to prevent overlapping text collisions.
        /// </summary>
        public static void DrawPromptImmediate(string keyLabel, string actionLabel, float holdProgress = 0f, Vector2? customScreenPos = null)
        {
            if (SyntyCombined.Cutscene.GlobalCutsceneState.IsCutsceneActive) return;

            EnsureTextures();
            EnsureStyles();

            if (string.IsNullOrEmpty(keyLabel)) keyLabel = "E";
            if (string.IsNullOrEmpty(actionLabel)) return;

            // Track frame / event pass to stack multiple prompts cleanly
            int currentFrame = Time.frameCount;
            EventType currentType = Event.current != null ? Event.current.type : EventType.Repaint;

            if (currentFrame != _lastDrawnFrame || currentType != _lastEventType)
            {
                _lastDrawnFrame = currentFrame;
                _lastEventType = currentType;
                _promptsDrawnThisFrame = 0;
                _drawnKeysThisFrame.Clear();
            }

            string promptSignature = $"{keyLabel}:{actionLabel}";
            if (_drawnKeysThisFrame.Contains(promptSignature)) return;
            _drawnKeysThisFrame.Add(promptSignature);

            float badgeSize = 34f;
            float gap = 10f;
            float p = Mathf.Clamp01(holdProgress);

            // Compute action text dimensions
            var textContent = new GUIContent(actionLabel);
            Vector2 textSize = _actionStyle.CalcSize(textContent);
            float totalW = badgeSize + gap + textSize.x + 8f;
            float totalH = badgeSize;

            // Determine layout coordinates
            float startX;
            float startY;

            if (customScreenPos.HasValue)
            {
                startX = customScreenPos.Value.x - (totalW * 0.5f);
                startY = customScreenPos.Value.y - (totalH * 0.5f);
            }
            else
            {
                // Standard AAA lower-third center with vertical stacking for multiple prompts
                float verticalOffset = _promptsDrawnThisFrame * (badgeSize + 12f);
                startX = (Screen.width - totalW) * 0.5f;
                startY = Screen.height * 0.66f + verticalOffset;
                _promptsDrawnThisFrame++;
            }

            // Clamp on screen
            startX = Mathf.Clamp(startX, 16f, Screen.width - totalW - 16f);
            startY = Mathf.Clamp(startY, 16f, Screen.height - totalH - 16f);

            Rect badgeRect = new Rect(startX, startY, badgeSize, badgeSize);
            Rect textRect = new Rect(startX + badgeSize + gap, startY + (badgeSize - textSize.y) * 0.5f, textSize.x + 8f, textSize.y);

            // 1. Draw Keycap Badge Background (Dark slate with anti-aliased rounded corners)
            GUI.color = Color.white;
            GUI.DrawTexture(badgeRect, _keycapTex, ScaleMode.StretchToFill, true);

            // 2. Draw Hold Progress Fill (Luminous cyan rising from bottom of keycap)
            if (p > 0.001f)
            {
                float innerMargin = 3f;
                float fillMaxH = badgeSize - (innerMargin * 2f);
                float fillH = fillMaxH * p;
                float fillW = badgeSize - (innerMargin * 2f);

                Rect fillRect = new Rect(startX + innerMargin, startY + badgeSize - innerMargin - fillH, fillW, fillH);
                Rect uvRect = new Rect(0f, 0f, 1f, p);

                GUI.color = CyanGlow;
                GUI.DrawTextureWithTexCoords(fillRect, _keycapSolidTex, uvRect);

                // Sleek progress bar beneath the action text
                Rect barBgRect = new Rect(textRect.x, startY + badgeSize - 2f, textSize.x, 2.5f);
                GUI.color = new Color(0.03f, 0.08f, 0.12f, 0.75f);
                GUI.DrawTexture(barBgRect, _whiteTex);

                Rect barFillRect = new Rect(textRect.x, startY + badgeSize - 2f, textSize.x * p, 2.5f);
                GUI.color = CyanPrimary;
                GUI.DrawTexture(barFillRect, _whiteTex);
            }

            // 3. Draw Centered Key Label inside Keycap ("F" or "E")
            // Dark drop shadow behind key
            Color prevColor = GUI.color;
            GUI.color = Color.white;
            _keyStyle.normal.textColor = ShadowColor;
            GUI.Label(new Rect(badgeRect.x + 1.2f, badgeRect.y + 1.2f, badgeSize, badgeSize), keyLabel, _keyStyle);

            // Luminous cyan key character (turns white when holding)
            _keyStyle.normal.textColor = (p > 0.001f) ? Color.Lerp(CyanPrimary, Color.white, p) : CyanPrimary;
            GUI.Label(badgeRect, keyLabel, _keyStyle);

            // 4. Draw Action Label Text ("Embark [Hold]", "Enter Clinic Lab", etc.)
            // Sharp drop shadow
            _shadowStyle.normal.textColor = ShadowColor;
            GUI.Label(new Rect(textRect.x + 1.5f, textRect.y + 1.5f, textRect.width, textRect.height), actionLabel, _shadowStyle);

            // Luminous cyan action label
            _actionStyle.normal.textColor = CyanPrimary;
            GUI.Label(textRect, actionLabel, _actionStyle);

            GUI.color = prevColor;
        }

        // ─────────────────────────────────────────────────────────────────────
        // TEXTURE GENERATION (Anti-Aliased Signed Distance Field Rounded Rects)
        // ─────────────────────────────────────────────────────────────────────
        private static void EnsureTextures()
        {
            if (_keycapTex != null && _keycapSolidTex != null && _whiteTex != null) return;

            if (_whiteTex == null)
            {
                _whiteTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                _whiteTex.SetPixels(new Color[] { Color.white, Color.white, Color.white, Color.white });
                _whiteTex.Apply();
            }

            // High-resolution 128x128 SDF badge with 24px corner radius and 7px border
            _keycapTex = GenerateRoundedKeycapTexture(128, 24f, 7.5f, DarkSlateBg, CyanPrimary);
            _keycapSolidTex = GenerateRoundedSolidTexture(128, 20f, CyanPrimary);
        }

        private static Texture2D GenerateRoundedKeycapTexture(int size, float cornerRadius, float borderWidth, Color fillColor, Color borderColor)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            float half = size * 0.5f;
            float r = cornerRadius;
            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = Mathf.Abs((x + 0.5f) - half);
                    float py = Mathf.Abs((y + 0.5f) - half);

                    float dx = Mathf.Max(px - (half - r), 0f);
                    float dy = Mathf.Max(py - (half - r), 0f);
                    float distToOuterEdge = Mathf.Sqrt(dx * dx + dy * dy) - r;

                    // Smooth anti-aliased outer edge (1.5 px band)
                    float outerAlpha = Mathf.Clamp01(0.75f - distToOuterEdge);

                    if (outerAlpha <= 0.001f)
                    {
                        pixels[y * size + x] = Color.clear;
                    }
                    else
                    {
                        // Border factor across inner edge
                        float borderFactor = Mathf.Clamp01((distToOuterEdge + borderWidth + 0.75f) / 1.5f);
                        Color c = Color.Lerp(fillColor, borderColor, borderFactor);
                        c.a = Mathf.Lerp(fillColor.a, borderColor.a, borderFactor) * outerAlpha;
                        pixels[y * size + x] = c;
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private static Texture2D GenerateRoundedSolidTexture(int size, float cornerRadius, Color color)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            float half = size * 0.5f;
            float r = cornerRadius;
            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = Mathf.Abs((x + 0.5f) - half);
                    float py = Mathf.Abs((y + 0.5f) - half);

                    float dx = Mathf.Max(px - (half - r), 0f);
                    float dy = Mathf.Max(py - (half - r), 0f);
                    float distToOuterEdge = Mathf.Sqrt(dx * dx + dy * dy) - r;

                    float alpha = Mathf.Clamp01(0.75f - distToOuterEdge);
                    Color c = color;
                    c.a *= alpha;
                    pixels[y * size + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private static void EnsureStyles()
        {
            if (_keyStyle != null && _actionStyle != null && _shadowStyle != null) return;

            _keyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _actionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };

            _shadowStyle = new GUIStyle(_actionStyle);
        }
    }
}
