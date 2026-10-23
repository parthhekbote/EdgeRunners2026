using System;
using System.Collections;
using UnityEngine;

namespace Alita
{
    /// <summary>
    /// Minimalist Retro-Arcade Cyberpunk Fullscreen Loading Screen.
    /// Re-styled to match the clean, retro-cyberpunk boot sequence:
    /// 
    ///         ┌                               ┐
    ///                 SYSTEM: BOOTING_
    /// 
    ///            +       LOADING ■ ■ ■       +
    /// 
    ///               [===================]
    /// 
    ///                    INSERT COIN
    ///         └                               ┘
    /// 
    /// Features:
    /// - Deep purple-midnight void background (#100B1B)
    /// - 4 neon magenta corner reticle brackets (#E01F60)
    /// - Lateral crosshair markers (+ in cyan on left, + in magenta on right)
    /// - "SYSTEM: BOOTING_" status line with animated blinking cursor
    /// - "LOADING" hero title in bright neon cyan (#38EFD7) with animated pixel block dots (■ ■ ■)
    /// - Neon magenta framed progress bar with smooth cyan fill gauge
    /// - Silk-smooth fade-in and fade-out transitions
    /// - Preserves 100% of existing API: Show(), SetProgress(), SetStatus(), Hide()
    /// </summary>
    public class CyberpunkLoadingScreen : MonoBehaviour
    {
        // ─── Singleton ────────────────────────────────────────────────────────
        public static CyberpunkLoadingScreen Instance { get; private set; }

        // ─── State ────────────────────────────────────────────────────────────
        private bool   _visible        = false;
        private float  _progress       = 0f;          // 0..1
        private float  _targetProgress = 0f;
        private string _mainTitle      = "LOADING";
        private string _statusLine     = "SYSTEM:  BOOTING";
        private Action _onHideComplete;

        // ─── Animation Timers ─────────────────────────────────────────────────
        private float _time          = 0f;
        private float _fadeAlpha     = 1f;          // 1 = fully shown, 0 = hidden
        private bool  _fading        = false;

        // Audio
        private AudioSource _audioSource;

        // ─── Procedural Colors (#100B1B, #E01F60, #38EFD7) ───────────────────
        private static readonly Color BgColor      = new Color(0.063f, 0.043f, 0.106f, 1.0f); // #100B1B
        private static readonly Color MagentaColor = new Color(0.88f, 0.12f, 0.38f, 1.0f);     // #E01F60
        private static readonly Color CyanColor    = new Color(0.22f, 0.94f, 0.85f, 1.0f);     // #38EFD7
        private static readonly Color DarkFillCol  = new Color(0.04f, 0.03f, 0.07f, 0.95f);

        // ─── Procedural Textures, Fonts & Styles ─────────────────────────────
        private Texture2D _whiteTex;
        private Texture2D _bgTex;
        private Font      _customFont;
        private GUIStyle  _systemBootStyle;
        private GUIStyle  _loadingTitleStyle;

        // ─── Public API (Preserved 100%) ──────────────────────────────────────
        public static void Show(string title = "LOADING...", Action onDone = null)
        {
            Ensure();
            Instance._mainTitle      = "LOADING";
            Instance._onHideComplete = onDone;
            Instance._progress       = 0f;
            Instance._targetProgress = 0.05f;
            Instance._fadeAlpha      = 1f;
            Instance._fading         = false;
            Instance._visible        = true;

            // Optional custom system status if a specific destination was passed
            if (!string.IsNullOrEmpty(title) && !title.Equals("LOADING...", StringComparison.OrdinalIgnoreCase))
            {
                Instance._statusLine = "SYSTEM:  " + title.ToUpperInvariant();
            }
            else
            {
                Instance._statusLine = "SYSTEM:  BOOTING";
            }

            Instance.PlayChirpSound(520f, 0.08f);
        }

        public static void SetProgress(float t)
        {
            if (Instance == null) return;
            Instance._targetProgress = Mathf.Clamp01(t);
        }

        public static void SetStatus(string line)
        {
            if (Instance == null) return;
            if (!string.IsNullOrEmpty(line))
            {
                Instance._statusLine = "SYSTEM:  " + line.ToUpperInvariant();
            }
        }

        public static void Hide()
        {
            if (Instance == null) return;
            Instance.StartCoroutine(Instance.FadeOutRoutine());
        }

        // ─── Lifecycle ────────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
            BuildTextures();
            SetupAudio();
        }

        private static void Ensure()
        {
            if (Instance != null) return;
            var go = new GameObject("[CyberpunkLoadingScreen]");
            Instance = go.AddComponent<CyberpunkLoadingScreen>();
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(go);
            }
        }

        private void SetupAudio()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f;
        }

        private void PlayChirpSound(float frequency, float duration)
        {
            if (_audioSource == null) return;
            int sampleRate = 44100;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = 1f - (float)i / samples;
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * env * 0.15f;
            }
            var clip = AudioClip.Create("BootChirp", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            _audioSource.PlayOneShot(clip, 0.45f);
        }

        private void BuildTextures()
        {
            if (_whiteTex == null)
            {
                _whiteTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                _whiteTex.SetPixels(new Color[] { Color.white, Color.white, Color.white, Color.white });
                _whiteTex.Apply();
            }

            if (_bgTex == null)
            {
                _bgTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                _bgTex.SetPixels(new Color[] { BgColor, BgColor, BgColor, BgColor });
                _bgTex.Apply();
            }
        }

        private void EnsureStyles(int sh)
        {
            if (_customFont == null)
            {
                _customFont = Resources.Load<Font>("Fonts/ZenDots");
                if (_customFont == null)
                    _customFont = Resources.Load<Font>("SyntyUI/Fonts/Orbitron/Orbitron-ExtraBold");
            }

            float s = Mathf.Clamp(sh / 1080f, 0.5f, 3.0f);

            if (_systemBootStyle == null)
            {
                _systemBootStyle = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
            }
            _systemBootStyle.fontSize = Mathf.RoundToInt(18f * s);
            if (_customFont != null) _systemBootStyle.font = _customFont;

            if (_loadingTitleStyle == null)
            {
                _loadingTitleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft
                };
            }
            _loadingTitleStyle.fontSize = Mathf.RoundToInt(38f * s);
            if (_customFont != null) _loadingTitleStyle.font = _customFont;
        }

        private void Update()
        {
            if (!_visible && !_fading) return;
            _time += Time.unscaledDeltaTime;

            // Smooth progress interpolation
            _progress = Mathf.MoveTowards(_progress, _targetProgress, Time.unscaledDeltaTime * 1.5f);
        }

        private IEnumerator FadeOutRoutine()
        {
            _fading = true;
            PlayChirpSound(880f, 0.12f);

            float t = 0f;
            float startAlpha = _fadeAlpha;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / 0.40f;
                _fadeAlpha = Mathf.Lerp(startAlpha, 0f, t);
                yield return null;
            }
            _fadeAlpha = 0f;
            _visible   = false;
            _fading    = false;
            _onHideComplete?.Invoke();
        }

        // ─── Rendering ────────────────────────────────────────────────────────
        private void OnGUI()
        {
            if (!_visible && !_fading) return;

            GUI.depth = -2000;
            BuildTextures();

            int sw = Screen.width;
            int sh = Screen.height;
            EnsureStyles(sh);

            // 1. Deep Midnight Purple Background
            Color oldGUIColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, _fadeAlpha);
            GUI.DrawTexture(new Rect(0, 0, sw, sh), _bgTex);

            // 2. Corner Reticle Brackets (4 corners, Neon Magenta)
            DrawCornerBrackets(sw, sh);

            // 3. Lateral Crosshair Markers (+ in Cyan on Left, + in Magenta on Right)
            DrawLateralCrosshairs(sw, sh);

            // 4. Top System Boot Line (SYSTEM: BOOTING_)
            DrawSystemBootLine(sw, sh);

            // 5. Hero Title (LOADING ■ ■ ■)
            DrawHeroLoadingTitle(sw, sh);

            // 6. Progress Bar (Magenta Frame with Smooth Cyan Fill)
            DrawProgressBar(sw, sh);

            GUI.color = oldGUIColor;
        }

        private void DrawCornerBrackets(int sw, int sh)
        {
            float s = Mathf.Clamp(sh / 1080f, 0.5f, 3.0f);
            float insetX = sw * 0.055f;
            float insetY = sh * 0.085f;
            float armLen = Mathf.Clamp(sw * 0.045f, 32f * s, 60f * s);
            float thick  = Mathf.Max(3f, 4f * s);

            Color col = new Color(MagentaColor.r, MagentaColor.g, MagentaColor.b, MagentaColor.a * _fadeAlpha);
            GUI.color = col;

            // Top-Left ┌
            GUI.DrawTexture(new Rect(insetX, insetY, armLen, thick), _whiteTex);
            GUI.DrawTexture(new Rect(insetX, insetY, thick, armLen), _whiteTex);

            // Top-Right ┐
            GUI.DrawTexture(new Rect(sw - insetX - armLen, insetY, armLen, thick), _whiteTex);
            GUI.DrawTexture(new Rect(sw - insetX - thick, insetY, thick, armLen), _whiteTex);

            // Bottom-Left └
            GUI.DrawTexture(new Rect(insetX, sh - insetY - thick, armLen, thick), _whiteTex);
            GUI.DrawTexture(new Rect(insetX, sh - insetY - armLen, thick, armLen), _whiteTex);

            // Bottom-Right ┘
            GUI.DrawTexture(new Rect(sw - insetX - armLen, sh - insetY - thick, armLen, thick), _whiteTex);
            GUI.DrawTexture(new Rect(sw - insetX - thick, sh - insetY - armLen, thick, armLen), _whiteTex);
        }

        private void DrawLateralCrosshairs(int sw, int sh)
        {
            float s = Mathf.Clamp(sh / 1080f, 0.5f, 3.0f);
            float centerY = sh * 0.505f;
            float marginX = sw * 0.115f;
            float crossArm = Mathf.RoundToInt(18f * s);
            float crossThick = Mathf.Max(3f, 4f * s);

            // Left Cross (+) in Cyan
            Color cyanCross = new Color(CyanColor.r, CyanColor.g, CyanColor.b, CyanColor.a * _fadeAlpha);
            GUI.color = cyanCross;
            float lx = marginX;
            GUI.DrawTexture(new Rect(lx - crossArm * 0.5f, centerY - crossThick * 0.5f, crossArm, crossThick), _whiteTex);
            GUI.DrawTexture(new Rect(lx - crossThick * 0.5f, centerY - crossArm * 0.5f, crossThick, crossArm), _whiteTex);

            // Right Cross (+) in Magenta
            Color magCross = new Color(MagentaColor.r, MagentaColor.g, MagentaColor.b, MagentaColor.a * _fadeAlpha);
            GUI.color = magCross;
            float rx = sw - marginX;
            GUI.DrawTexture(new Rect(rx - crossArm * 0.5f, centerY - crossThick * 0.5f, crossArm, crossThick), _whiteTex);
            GUI.DrawTexture(new Rect(rx - crossThick * 0.5f, centerY - crossArm * 0.5f, crossThick, crossArm), _whiteTex);
        }

        private void DrawSystemBootLine(int sw, int sh)
        {
            float y = sh * 0.215f;
            bool blink = ((int)(_time * 3f) % 2 == 0);
            string line = _statusLine + (blink ? "_" : " ");

            _systemBootStyle.normal.textColor = new Color(MagentaColor.r, MagentaColor.g, MagentaColor.b, 0.95f * _fadeAlpha);
            GUI.color = new Color(1f, 1f, 1f, _fadeAlpha);
            GUI.Label(new Rect(0, y, sw, 32f), line, _systemBootStyle);
        }

        private void DrawHeroLoadingTitle(int sw, int sh)
        {
            float s = Mathf.Clamp(sh / 1080f, 0.5f, 3.0f);
            float y = sh * 0.40f;
            string word = "LOADING";

            Vector2 wordSize = _loadingTitleStyle.CalcSize(new GUIContent(word));

            // Animated pixel block dots (■ ■ ■)
            float dotSize = Mathf.RoundToInt(14f * s);
            float dotGap  = Mathf.RoundToInt(10f * s);
            float totalDotsWidth = (dotSize * 3f) + (dotGap * 2f);
            float totalHeroWidth = wordSize.x + (16f * s) + totalDotsWidth;

            float startX = (sw - totalHeroWidth) * 0.5f;

            // Draw "LOADING" in Cyan
            _loadingTitleStyle.normal.textColor = new Color(CyanColor.r, CyanColor.g, CyanColor.b, 1.0f * _fadeAlpha);
            GUI.color = new Color(1f, 1f, 1f, _fadeAlpha);
            GUI.Label(new Rect(startX, y, wordSize.x, wordSize.y), word, _loadingTitleStyle);

            // Draw 3 Pixel Block Dots
            int activeStep = ((int)(_time * 3.5f) % 4); // 0=none, 1=one, 2=two, 3=three
            float dotsStartX = startX + wordSize.x + (16f * s);
            float dotsCenterY = y + (wordSize.y * 0.5f) - (dotSize * 0.5f);

            for (int i = 0; i < 3; i++)
            {
                float dx = dotsStartX + i * (dotSize + dotGap);
                bool isActive = (i < activeStep);

                Color dotColor = isActive 
                    ? new Color(CyanColor.r, CyanColor.g, CyanColor.b, 1.0f * _fadeAlpha)
                    : new Color(CyanColor.r * 0.25f, CyanColor.g * 0.25f, CyanColor.b * 0.25f, 0.35f * _fadeAlpha);

                GUI.color = dotColor;
                GUI.DrawTexture(new Rect(dx, dotsCenterY, dotSize, dotSize), _whiteTex);
            }
        }

        private void DrawProgressBar(int sw, int sh)
        {
            float s = Mathf.Clamp(sh / 1080f, 0.5f, 3.0f);
            float barW = Mathf.Clamp(sw * 0.45f, 380f * s, 600f * s);
            float barH = Mathf.RoundToInt(28f * s);
            float barX = (sw - barW) * 0.5f;
            float barY = sh * 0.545f;
            float borderThick = Mathf.Max(3f, 3.5f * s);

            // 1. Dark interior fill
            GUI.color = new Color(DarkFillCol.r, DarkFillCol.g, DarkFillCol.b, _fadeAlpha);
            GUI.DrawTexture(new Rect(barX, barY, barW, barH), _whiteTex);

            // 2. Cyan Progress Fill Bar (inner)
            float p = Mathf.Clamp01(_progress);
            if (p > 0.002f)
            {
                float innerMargin = borderThick;
                float fillW = (barW - (innerMargin * 2f)) * p;
                float fillH = barH - (innerMargin * 2f);

                GUI.color = new Color(CyanColor.r, CyanColor.g, CyanColor.b, 1.0f * _fadeAlpha);
                GUI.DrawTexture(new Rect(barX + innerMargin, barY + innerMargin, fillW, fillH), _whiteTex);
            }

            // 3. Magenta Frame Border
            GUI.color = new Color(MagentaColor.r, MagentaColor.g, MagentaColor.b, 1.0f * _fadeAlpha);
            // Top border
            GUI.DrawTexture(new Rect(barX, barY, barW, borderThick), _whiteTex);
            // Bottom border
            GUI.DrawTexture(new Rect(barX, barY + barH - borderThick, barW, borderThick), _whiteTex);
            // Left border
            GUI.DrawTexture(new Rect(barX, barY, borderThick, barH), _whiteTex);
            // Right border
            GUI.DrawTexture(new Rect(barX + barW - borderThick, barY, borderThick, barH), _whiteTex);
        }
    }
}
