using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace SyntyCombined
{
    /// <summary>
    /// Master Procedural Constructor for the AAA Cyberpunk Pause Menu & Settings Interface.
    /// Perfectly replicates and elevates the "ASTRA BEYOND HUMANITY" aesthetic:
    /// - Sleek left-aligned Pause menu with glowing violet-to-blue pill Resume button and reticle icon
    /// - Continuous vertical neon track line connecting interactive node items
    /// - Full tabbed Settings window with holographic corner brackets and glassmorphism styling
    /// - Pixel-perfect Steppers and Sliders with crisp typography
    /// - High-resolution live city preview card and interactive hardware topology schematics
    /// - Full Q/E keyboard cycling, ESC navigation, and real-time audio-visual micro-interactions.
    /// </summary>
    public static class PauseMenuBuilder
    {
        // Cyberpunk AAA Palette (Electric Royal Blue & Cyber Cyan matching reference ASTRA UI)
        public static readonly Color ScrimDark = new Color(0.015f, 0.025f, 0.05f, 0.72f);
        public static readonly Color WindowDark = new Color(0.02f, 0.04f, 0.09f, 0.95f);
        public static readonly Color FrameBorderCyan = new Color(0.0f, 0.90f, 1.0f, 0.85f);
        public static readonly Color FrameBorderDim = new Color(0.0f, 0.75f, 0.95f, 0.25f);
        public static readonly Color AccentBlueGlow = new Color(0.08f, 0.42f, 0.95f, 0.95f);
        public static readonly Color GradientPurple = new Color(0.42f, 0.12f, 0.78f, 0.98f);
        public static readonly Color GradientBlue = new Color(0.08f, 0.38f, 0.92f, 0.98f);
        public static readonly Color CardBgDark = new Color(0.025f, 0.055f, 0.12f, 0.88f);
        public static readonly Color RowBgDark = new Color(0.035f, 0.075f, 0.15f, 0.50f);
        public static readonly Color RowBgHover = new Color(0.06f, 0.15f, 0.30f, 0.75f);
        public static readonly Color TextWhite = new Color(0.96f, 0.98f, 1.0f, 1.0f);
        public static readonly Color TextMutedCyan = new Color(0.45f, 0.70f, 0.90f, 0.80f);
        public static readonly Color BtnInactiveBg = new Color(0.03f, 0.06f, 0.12f, 0.65f);
        public static readonly Color BtnActiveBg = new Color(0.12f, 0.45f, 0.95f, 0.98f);
        public static readonly Color CyberYellow = new Color(0.988f, 0.933f, 0.039f, 1.0f); // #FCEE0A
        public static readonly Color CrimsonMuted = new Color(0.85f, 0.22f, 0.22f, 0.90f);
        public static readonly Color HoloRedBg = new Color(0.12f, 0.015f, 0.025f, 0.92f);

        private static TMP_FontAsset _cachedHeaderFont;
        private static TMP_FontAsset _cachedBodyFont;
        private static Sprite _cachedPillSprite;
        private static Sprite _cachedReticleSprite;
        private static Sprite _cachedStarSprite;
        private static Sprite _cachedGearSprite;
        private static Sprite _cachedCornerSprite;
        private static Sprite _cachedPreviewCitySprite;
        private static Sprite _cachedMouseSprite;
        private static Sprite _cachedGamepadSprite;
        private static Texture2D _cachedCursorTexture;

        public static TMP_FontAsset GetHeaderFont()
        {
            if (_cachedHeaderFont != null) return _cachedHeaderFont;
#if UNITY_EDITOR
            string[] paths = new string[]
            {
                "Assets/Synty/InterfaceSciFiMenus/Fonts/Quantico/Quantico-Bold SDF.asset",
                "Assets/Synty/InterfaceSciFiMenus/Fonts/Zen_Dots/ZenDots-Regular SDF.asset",
                "Assets/Resources/SyntyUI/Fonts/Font_SciFi_Header.asset"
            };
            foreach (var p in paths)
            {
                var f = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(p);
                if (f != null) { _cachedHeaderFont = f; return f; }
            }
#endif
            return GetBodyFont();
        }

        public static TMP_FontAsset GetBodyFont()
        {
            if (_cachedBodyFont != null) return _cachedBodyFont;
#if UNITY_EDITOR
            string[] paths = new string[]
            {
                "Assets/CombatGirlsCharacterPack/UI_&_environmental/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset",
                "Assets/Resources/SyntyUI/Fonts/Font_SciFi_Header.asset"
            };
            foreach (var p in paths)
            {
                var f = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(p);
                if (f != null) { _cachedBodyFont = f; return f; }
            }
#endif
            var fallback = Resources.Load<TMP_FontAsset>("LiberationSans SDF");
            if (fallback != null) { _cachedBodyFont = fallback; return fallback; }
            return null;
        }

        #region Sprites
        public static Sprite GetPillSprite()
        {
            if (_cachedPillSprite != null) return _cachedPillSprite;

            int w = 256, h = 64;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            float r = h * 0.5f;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float cx = x < r ? r : (x > w - r ? w - r : x);
                    float cy = r;
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                    float alpha = Mathf.Clamp01((r - dist) + 0.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            _cachedPillSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(32, 32, 32, 32));
            return _cachedPillSprite;
        }

        public static Sprite GetReticleSprite()
        {
            if (_cachedReticleSprite != null) return _cachedReticleSprite;

            int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center);
                    // Outer crisp ring
                    float ring = Mathf.Clamp01(1f - Mathf.Abs(d - 20f) / 1.8f);
                    // Center core dot
                    float dot = Mathf.Clamp01(1f - d / 4.5f);
                    // 4 notch ticks
                    bool tick = (Mathf.Abs(x - center.x) <= 1.2f && (y <= 10 || y >= size - 10)) ||
                                (Mathf.Abs(y - center.y) <= 1.2f && (x <= 10 || x >= size - 10));
                    float tickA = tick ? 0.95f : 0f;

                    float alpha = Mathf.Max(ring, Mathf.Max(dot, tickA));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            _cachedReticleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return _cachedReticleSprite;
        }

        public static Sprite GetStarDiamondSprite()
        {
            if (_cachedStarSprite != null) return _cachedStarSprite;
#if UNITY_EDITOR
            var sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceSciFiMenus/Sprites/Icons_Menu/ICON_SciFiMenus_Menu_Star_01_Clean.png");
            if (sp != null) { _cachedStarSprite = sp; return sp; }
#endif
            // Procedural 4-point star fallback
            int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Vector2 c = new Vector2(size * 0.5f, size * 0.5f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x - c.x) / (size * 0.5f);
                    float dy = Mathf.Abs(y - c.y) / (size * 0.5f);
                    float d = Mathf.Pow(dx, 0.55f) + Mathf.Pow(dy, 0.55f);
                    float a = Mathf.Clamp01((1f - d) * 4f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            _cachedStarSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return _cachedStarSprite;
        }

        public static Sprite GetGearIconSprite()
        {
            if (_cachedGearSprite != null) return _cachedGearSprite;
#if UNITY_EDITOR
            var sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceSciFiMenus/Sprites/Icons_Menu/ICON_SciFiMenus_Menu_Settings_01_Clean.png");
            if (sp != null) { _cachedGearSprite = sp; return sp; }
#endif
            return GetReticleSprite();
        }

        public static Sprite GetCornerBracketSprite()
        {
            if (_cachedCornerSprite != null) return _cachedCornerSprite;
#if UNITY_EDITOR
            var sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceSciFiMenus/Sprites/General/SPR_SciFiMenus_Indicator_Line_Corner_01.png");
            if (sp != null) { _cachedCornerSprite = sp; return sp; }
#endif
            return null;
        }

        public static Sprite GetPreviewCitySprite()
        {
            if (_cachedPreviewCitySprite != null) return _cachedPreviewCitySprite;

            var res = Resources.Load<Sprite>("SyntyUI/Textures/Preview_CyberCity");
            if (res != null) { _cachedPreviewCitySprite = res; return res; }

#if UNITY_EDITOR
            var sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/SyntyUI/Textures/Preview_CyberCity.png");
            if (sp != null) { _cachedPreviewCitySprite = sp; return sp; }
#endif
            return null;
        }

        public static Sprite GetCyberMouseSprite()
        {
            if (_cachedMouseSprite != null) return _cachedMouseSprite;

            int mw = 280, mh = 440;
            Texture2D mouseTex = new Texture2D(mw, mh, TextureFormat.RGBA32, false);
            mouseTex.filterMode = FilterMode.Bilinear;
            mouseTex.wrapMode = TextureWrapMode.Clamp;

            Color32[] mCols = new Color32[mw * mh];
            Color32 clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < mCols.Length; i++) mCols[i] = clear;

            float mcx = mw * 0.5f;

            Color colChassis = new Color(0.04f, 0.07f, 0.12f, 0.95f);
            Color colChassisPlate = new Color(0.07f, 0.12f, 0.19f, 0.95f);
            Color colCyanBorder = new Color(0.0f, 0.9f, 1.0f, 1.0f);
            Color colCyanGlow = new Color(0.0f, 0.75f, 1.0f, 0.28f);
            Color colCyanBright = new Color(0.45f, 0.98f, 1.0f, 1.0f);
            Color colAccentPink = new Color(1.0f, 0.2f, 0.55f, 0.95f);
            Color colMutedCyan = new Color(0.0f, 0.4f, 0.6f, 0.8f);

            Func<float, float> getMouseProfile = (y) =>
            {
                float t = y / (float)mh;
                if (t < 0.04f || t > 0.94f) return 0f;
                if (t < 0.32f)
                {
                    float bt = (t - 0.04f) / 0.28f;
                    float circ = Mathf.Sqrt(Mathf.Clamp01(1f - Mathf.Pow(1f - bt, 2.2f)));
                    return 72f * circ;
                }
                else if (t < 0.52f)
                {
                    float wt = (t - 0.32f) / 0.20f;
                    return Mathf.Lerp(72f, 62f, Mathf.Sin(wt * Mathf.PI * 0.5f));
                }
                else if (t < 0.78f)
                {
                    float st = (t - 0.52f) / 0.26f;
                    return Mathf.Lerp(62f, 76f, Mathf.Sin(st * Mathf.PI * 0.5f));
                }
                else
                {
                    float tt = (t - 0.78f) / 0.16f;
                    return Mathf.Lerp(76f, 50f, tt * 1.3f);
                }
            };

            for (int y = 0; y < mh; y++)
            {
                float hw = getMouseProfile(y);
                float tY = y / (float)mh;

                for (int x = 0; x < mw; x++)
                {
                    float dx = Mathf.Abs(x - mcx);
                    float sDist = dx - hw;

                    if (sDist > 14f) continue;

                    if (sDist > 0f)
                    {
                        float gA = Mathf.Clamp01(1f - sDist / 14f) * 0.35f;
                        mCols[y * mw + x] = Color.Lerp(clear, colCyanGlow, gA);
                        continue;
                    }

                    Color c = Color.Lerp(colChassis, colChassisPlate, tY * 0.7f);

                    if (tY >= 0.30f && tY <= 0.52f && dx > hw - 10f)
                    {
                        bool stripe = ((x + y) % 8) < 3;
                        if (stripe) c = Color.Lerp(c, colMutedCyan, 0.45f);
                    }

                    if (x < mcx && tY >= 0.38f && tY <= 0.55f)
                    {
                        if (dx >= hw - 8f)
                        {
                            c = Color.Lerp(c, colCyanBorder, 0.85f);
                            if (Mathf.Abs(tY - 0.465f) < 0.008f) c = colChassis;
                        }
                    }

                    if (tY > 0.88f && dx < 14f)
                    {
                        float notchDist = Mathf.Sqrt(dx * dx + Mathf.Pow((tY - 0.94f) * mh, 2f));
                        if (notchDist < 16f)
                        {
                            mCols[y * mw + x] = clear;
                            continue;
                        }
                    }

                    if (tY > 0.50f && dx < 2.0f)
                    {
                        c = new Color(0.01f, 0.02f, 0.04f, 1f);
                    }

                    if (tY >= 0.62f && tY <= 0.88f && Mathf.Abs(dx - 36f) < 1.2f)
                    {
                        c = Color.Lerp(c, colCyanBorder, 0.75f);
                    }

                    if (dx < 16f && tY >= 0.63f && tY <= 0.85f)
                    {
                        c = new Color(0.02f, 0.03f, 0.06f, 1f);
                        if (dx > 13.5f || tY < 0.64f || tY > 0.84f) c = colCyanBorder;

                        if (dx < 9f && tY >= 0.655f && tY <= 0.825f)
                        {
                            bool isTread = (y % 7) <= 3;
                            if (isTread)
                            {
                                c = colCyanBright;
                                if (dx < 3f) c = Color.white;
                            }
                            else
                            {
                                c = new Color(0.02f, 0.15f, 0.25f, 1f);
                            }
                        }
                    }

                    if (dx < 10f && tY >= 0.53f && tY <= 0.60f)
                    {
                        c = new Color(0.05f, 0.09f, 0.15f, 1f);
                        if (dx > 8f || tY < 0.54f || tY > 0.59f) c = colMutedCyan;
                        if (dx < 3f && (Mathf.Abs(tY - 0.555f) < 0.008f || Mathf.Abs(tY - 0.575f) < 0.008f))
                        {
                            c = (tY > 0.565f) ? colAccentPink : colCyanBright;
                        }
                    }

                    if (tY >= 0.14f && tY <= 0.28f)
                    {
                        float py = (tY - 0.21f) * mh;
                        float hexDist = Mathf.Max(dx * 0.866f + Mathf.Abs(py) * 0.5f, Mathf.Abs(py));
                        if (Mathf.Abs(hexDist - 22f) < 1.6f) c = Color.Lerp(c, colCyanBorder, 0.9f);
                        if (Mathf.Abs(hexDist - 12f) < 1.4f) c = Color.Lerp(c, colCyanBright, 0.95f);
                        if (dx < 4f && Mathf.Abs(py) < 4f) c = colCyanBright;
                        if (Mathf.Abs(py) < 1.2f && dx > 23f && dx < 45f) c = colMutedCyan;
                    }

                    if (sDist > -2.4f)
                    {
                        c = Color.Lerp(c, colCyanBorder, Mathf.Clamp01((sDist + 2.4f) / 1.8f));
                    }

                    mCols[y * mw + x] = c;
                }
            }

            mouseTex.SetPixels32(mCols);
            mouseTex.Apply();
            _cachedMouseSprite = Sprite.Create(mouseTex, new Rect(0, 0, mw, mh), new Vector2(0.5f, 0.5f));
            return _cachedMouseSprite;
        }

        public static Sprite GetCyberGamepadSprite()
        {
            if (_cachedGamepadSprite != null) return _cachedGamepadSprite;

            int gw = 460, gh = 320;
            Texture2D padTex = new Texture2D(gw, gh, TextureFormat.RGBA32, false);
            padTex.filterMode = FilterMode.Bilinear;
            padTex.wrapMode = TextureWrapMode.Clamp;

            Color32[] pCols = new Color32[gw * gh];
            Color32 clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < pCols.Length; i++) pCols[i] = clear;

            float gcx = gw * 0.5f;

            Color colChassis = new Color(0.04f, 0.07f, 0.12f, 0.96f);
            Color colChassisPlate = new Color(0.07f, 0.12f, 0.19f, 0.96f);
            Color colCyanBorder = new Color(0.0f, 0.9f, 1.0f, 1.0f);
            Color colCyanGlow = new Color(0.0f, 0.75f, 1.0f, 0.28f);
            Color colCyanBright = new Color(0.45f, 0.98f, 1.0f, 1.0f);
            Color colMutedCyan = new Color(0.0f, 0.45f, 0.65f, 0.8f);

            Func<float, float, float> getPadSDF = (px, py) =>
            {
                float dx = Mathf.Abs(px - gcx);
                float dy = py;

                float bridgeX = dx / 140f;
                float bridgeY = (dy - 170f) / 95f;
                float dBridge = Mathf.Sqrt(bridgeX * bridgeX + bridgeY * bridgeY) - 1f;

                Vector2 p = new Vector2(dx, dy);
                Vector2 gA = new Vector2(130f, 180f);
                Vector2 gB = new Vector2(185f, 45f);
                Vector2 pa = p - gA, ba = gB - gA;
                float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
                float dGrip = (pa - ba * h).magnitude - Mathf.Lerp(44f, 38f, h);

                float dBumper = 999f;
                if (dx >= 60f && dx <= 165f && dy >= 240f && dy <= 295f)
                {
                    float bx = (dx - 112f) / 52f;
                    float by = (dy - 268f) / 22f;
                    dBumper = Mathf.Sqrt(bx * bx + by * by) - 1f;
                }

                float d = Mathf.Min(dBridge * 80f, dGrip);
                d = Mathf.Min(d, dBumper * 20f);

                if (dy < 110f && dx < 100f)
                {
                    float cxDist = Mathf.Sqrt(dx * dx + Mathf.Pow(dy - 40f, 2f));
                    float carve = 90f - cxDist;
                    if (carve > 0f) d = Mathf.Max(d, carve);
                }

                return d;
            };

            for (int y = 0; y < gh; y++)
            {
                for (int x = 0; x < gw; x++)
                {
                    float d = getPadSDF(x, y);
                    float dx = Mathf.Abs(x - gcx);

                    if (d > 14f) continue;

                    if (d > 0f)
                    {
                        float gA = Mathf.Clamp01(1f - d / 14f) * 0.35f;
                        pCols[y * gw + x] = Color.Lerp(clear, colCyanGlow, gA);
                        continue;
                    }

                    Color c = Color.Lerp(colChassis, colChassisPlate, y / (float)gh);

                    if (dx > 160f && y < 140f && ((x + y) % 8) < 3)
                    {
                        c = Color.Lerp(c, colMutedCyan, 0.4f);
                    }

                    Vector2 lStickCenter = new Vector2(gcx - 85f, 175f);
                    float dLStick = Vector2.Distance(new Vector2(x, y), lStickCenter);
                    if (dLStick < 38f)
                    {
                        c = new Color(0.02f, 0.04f, 0.08f, 1f);
                        if (dLStick > 35f) c = colCyanBorder;
                        else if (dLStick < 28f)
                        {
                            c = new Color(0.06f, 0.12f, 0.2f, 1f);
                            if (Mathf.Abs(dLStick - 26f) < 1.2f || Mathf.Abs(dLStick - 16f) < 1.2f) c = colCyanBright;
                            Vector2 off = new Vector2(x, y) - lStickCenter;
                            if ((Mathf.Abs(off.x) < 1.8f && Mathf.Abs(off.y) < 14f) || (Mathf.Abs(off.y) < 1.8f && Mathf.Abs(off.x) < 14f))
                                c = colCyanBright;
                        }
                    }

                    Vector2 rStickCenter = new Vector2(gcx + 55f, 125f);
                    float dRStick = Vector2.Distance(new Vector2(x, y), rStickCenter);
                    if (dRStick < 38f)
                    {
                        c = new Color(0.02f, 0.04f, 0.08f, 1f);
                        if (dRStick > 35f) c = colCyanBorder;
                        else if (dRStick < 28f)
                        {
                            c = new Color(0.06f, 0.12f, 0.2f, 1f);
                            if (Mathf.Abs(dRStick - 26f) < 1.2f || Mathf.Abs(dRStick - 16f) < 1.2f) c = colCyanBright;
                            Vector2 off = new Vector2(x, y) - rStickCenter;
                            if ((Mathf.Abs(off.x) < 1.8f && Mathf.Abs(off.y) < 14f) || (Mathf.Abs(off.y) < 1.8f && Mathf.Abs(off.x) < 14f))
                                c = colCyanBright;
                        }
                    }

                    Vector2 dpadCenter = new Vector2(gcx - 55f, 125f);
                    Vector2 dpOff = new Vector2(x, y) - dpadCenter;
                    float adpX = Mathf.Abs(dpOff.x);
                    float adpY = Mathf.Abs(dpOff.y);
                    bool inDpad = (adpX < 11f && adpY < 32f) || (adpY < 11f && adpX < 32f);
                    if (inDpad)
                    {
                        c = new Color(0.08f, 0.15f, 0.24f, 1f);
                        if ((adpX > 9f && adpY < 32f) || (adpY > 9f && adpX < 32f) || adpX > 30f || adpY > 30f) c = colCyanBorder;
                        if (adpX < 5f && adpY < 5f) c = new Color(0.03f, 0.06f, 0.1f, 1f);
                    }

                    Vector2 abxyCenter = new Vector2(gcx + 85f, 185f);
                    Vector2[] btnOffsets = new Vector2[] { new Vector2(0, 24), new Vector2(24, 0), new Vector2(0, -24), new Vector2(-24, 0) };
                    Color[] btnColors = new Color[] {
                        new Color(1f, 0.85f, 0.1f, 1f),
                        new Color(1f, 0.25f, 0.35f, 1f),
                        new Color(0.15f, 0.95f, 0.45f, 1f),
                        new Color(0.2f, 0.75f, 1f, 1f)
                    };
                    for (int bi = 0; bi < 4; bi++)
                    {
                        float db = Vector2.Distance(new Vector2(x, y), abxyCenter + btnOffsets[bi]);
                        if (db < 12f)
                        {
                            c = new Color(0.03f, 0.06f, 0.1f, 1f);
                            if (db > 9.5f || db < 5f) c = btnColors[bi];
                        }
                    }

                    Vector2 nexusCenter = new Vector2(gcx, 195f);
                    float dNexus = Vector2.Distance(new Vector2(x, y), nexusCenter);
                    if (dNexus < 22f)
                    {
                        c = new Color(0.02f, 0.04f, 0.07f, 1f);
                        if (dNexus > 19f) c = colCyanBorder;
                        else if (Mathf.Abs(dNexus - 12f) < 1.5f || dNexus < 4f) c = colCyanBright;
                    }

                    if (y > 240f && dx > 60f && dx < 165f && Mathf.Abs(y - 252f) < 1.2f) c = colCyanBorder;

                    if (d > -2.4f)
                    {
                        c = Color.Lerp(c, colCyanBorder, Mathf.Clamp01((d + 2.4f) / 1.8f));
                    }

                    pCols[y * gw + x] = c;
                }
            }

            padTex.SetPixels32(pCols);
            padTex.Apply();
            _cachedGamepadSprite = Sprite.Create(padTex, new Rect(0, 0, gw, gh), new Vector2(0.5f, 0.5f));
            return _cachedGamepadSprite;
        }

        #region Phantom Liberty Menu Sprites
        private static Sprite _cachedYellowRulerSprite;
        public static Sprite GetYellowRulerSprite()
        {
            if (_cachedYellowRulerSprite != null) return _cachedYellowRulerSprite;
            int w = 16, h = 128;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Repeat;
            Color32[] cols = new Color32[w * h];
            Color32 clear = new Color32(0, 0, 0, 0);
            Color32 white = new Color32(255, 255, 255, 255);

            for (int i = 0; i < cols.Length; i++) cols[i] = clear;

            for (int y = 0; y < h; y++)
            {
                // Continuous vertical line on left (x = 2)
                cols[y * w + 2] = white;

                // Horizontal tick marks
                if (y % 8 == 0)
                {
                    int tickLen = (y % 32 == 0) ? 14 : 7;
                    for (int x = 2; x <= tickLen; x++)
                    {
                        cols[y * w + x] = white;
                    }
                }
            }

            tex.SetPixels32(cols);
            tex.Apply();
            _cachedYellowRulerSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
            return _cachedYellowRulerSprite;
        }

        private static Sprite _cachedCyanHandSprite;
        public static Sprite GetCyanHandCursorSprite()
        {
            if (_cachedCyanHandSprite != null) return _cachedCyanHandSprite;
            int s = 48;
            Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color32[] cols = new Color32[s * s];
            Color32 clear = new Color32(0, 0, 0, 0);
            Color32 cyanOutline = new Color32(0, 229, 255, 255); // #00E5FF
            Color32 cyanFill = new Color32(0, 180, 215, 220);

            for (int i = 0; i < cols.Length; i++) cols[i] = clear;

            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    bool inHand = false;
                    bool isEdge = false;

                    // Index finger
                    if (y >= 22 && y <= 27 && x >= 22 && x <= 42)
                    {
                        inHand = true;
                        if (y == 22 || y == 27 || x >= 41) isEdge = true;
                    }
                    // Palm
                    else if (y >= 12 && y <= 28 && x >= 14 && x <= 24)
                    {
                        inHand = true;
                        if (x == 14 || (y == 28 && x <= 18) || (y == 12 && x <= 22)) isEdge = true;
                    }
                    // Curled fingers
                    else if (y >= 12 && y <= 21 && x >= 22 && x <= 31)
                    {
                        inHand = true;
                        if (y == 12 || x == 31 || y == 15 || y == 18) isEdge = true;
                    }
                    // Thumb
                    else if (y >= 26 && y <= 33 && x >= 18 && x <= 26)
                    {
                        inHand = true;
                        if (y == 33 || x == 26 || (y >= 30 && x == 18)) isEdge = true;
                    }
                    // Wrist
                    else if (y >= 15 && y <= 25 && x >= 8 && x <= 14)
                    {
                        inHand = true;
                        if (x == 8 || y == 15 || y == 25) isEdge = true;
                    }

                    if (inHand)
                    {
                        cols[y * s + x] = isEdge ? cyanOutline : cyanFill;
                    }
                }
            }

            tex.SetPixels32(cols);
            tex.Apply();
            _cachedCyanHandSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
            return _cachedCyanHandSprite;
        }

        private static Sprite _cachedTechBadgeSprite;
        public static Sprite GetTechBadgeSprite()
        {
            if (_cachedTechBadgeSprite != null) return _cachedTechBadgeSprite;
            int w = 48, h = 28;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            Color32[] cols = new Color32[w * h];
            Color32 clear = new Color32(0, 0, 0, 0);
            Color32 yellow = new Color32(252, 238, 10, 255); // #FCEE0A

            for (int i = 0; i < cols.Length; i++) cols[i] = clear;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // Outer rectangular border
                    if (x == 0 || x == w - 1 || y == 0 || y == h - 1)
                    {
                        cols[y * w + x] = yellow;
                    }
                    // Barcode vertical stripes inside
                    else if (y >= 5 && y <= h - 6)
                    {
                        if (x == 5 || x == 7 || x == 8 || x == 12 || x == 15 || x == 16 || x == 17 ||
                            x == 21 || x == 23 || x == 26 || x == 27 || x == 30 || x == 33 || x == 35 ||
                            x == 38 || x == 40 || x == 41)
                        {
                            cols[y * w + x] = yellow;
                        }
                    }
                }
            }

            tex.SetPixels32(cols);
            tex.Apply();
            _cachedTechBadgeSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
            return _cachedTechBadgeSprite;
        }

        private static Sprite _cachedYellowBorderSprite;
        public static Sprite GetYellowMenuItemBorderSprite()
        {
            if (_cachedYellowBorderSprite != null) return _cachedYellowBorderSprite;
            int s = 64;
            Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color32 yellow = new Color32(252, 238, 10, 255); // #FCEE0A
            Color32 fillAmber = new Color32(48, 8, 10, 110);

            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    bool isBorder = (x <= 1 || x >= s - 2 || y <= 1 || y >= s - 2);
                    if (x + y > (s - 1) * 2 - 4)
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                    else if (x + y < 4)
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                    else if (isBorder)
                    {
                        tex.SetPixel(x, y, yellow);
                    }
                    else
                    {
                        tex.SetPixel(x, y, fillAmber);
                    }
                }
            }
            tex.Apply();
            _cachedYellowBorderSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(8, 8, 8, 8));
            return _cachedYellowBorderSprite;
        }

        private static Sprite _cachedRedHoloSprite;
        public static Sprite GetRedHoloColumnSprite()
        {
            if (_cachedRedHoloSprite != null) return _cachedRedHoloSprite;
            int w = 128, h = 256;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color leftDark = new Color(0.13f, 0.015f, 0.025f, 0.92f);
            Color rightDark = new Color(0.08f, 0.010f, 0.018f, 0.70f);
            Color rightRim = new Color(1.0f, 0.10f, 0.18f, 0.65f);

            for (int y = 0; y < h; y++)
            {
                float scanline = (y % 6 < 3) ? 0.03f : 0.0f;
                for (int x = 0; x < w; x++)
                {
                    float tx = x / (float)(w - 1);
                    Color c = Color.Lerp(leftDark, rightDark, tx);
                    c.a += scanline;
                    if (x >= w - 3)
                    {
                        c = Color.Lerp(c, rightRim, 0.85f);
                    }
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            _cachedRedHoloSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(12, 12, 12, 12));
            return _cachedRedHoloSprite;
        }

        #endregion

        public static void ApplyCyberpunkHardwareCursor()
        {
            if (_cachedCursorTexture == null)
            {
                int cw = 32, ch = 32;
                _cachedCursorTexture = new Texture2D(cw, ch, TextureFormat.RGBA32, false);
                _cachedCursorTexture.filterMode = FilterMode.Point;
                _cachedCursorTexture.wrapMode = TextureWrapMode.Clamp;

                Color32[] cCols = new Color32[cw * ch];
                Color32 clear = new Color32(0, 0, 0, 0);
                for (int i = 0; i < cCols.Length; i++) cCols[i] = clear;

                string[] pattern = new string[]
                {
                    "X...............................",
                    "XX..............................",
                    "XOX.............................",
                    "XOOX............................",
                    "XOOOX...........................",
                    "XOOOOX..........................",
                    "XOOOOOX.........................",
                    "XOOOOOOX........................",
                    "XOOOOOOOX.......................",
                    "XOOOOOOOOX......................",
                    "XOOOOOOOOOX.....................",
                    "XOOOOOOOOOOX....................",
                    "XOOOOOX.XXXX....................",
                    "XOOOOX..XOOX....................",
                    "XOOOX....XOOX...................",
                    "XOOX.....XOOX...................",
                    "XOX.......XOOX..................",
                    "XX........XOOX..................",
                    "X..........XOOX.................",
                    "............XOOX................",
                    "............XX..................",
                    "................................",
                    "................................",
                    "................................",
                    "................................",
                    "................................",
                    "................................",
                    "................................",
                    "................................",
                    "................................",
                    "................................",
                    "................................"
                };

                Color32 borderCol = new Color32(0, 240, 255, 255);
                Color32 fillCol = new Color32(8, 20, 36, 235);
                Color32 tipCol = new Color32(235, 255, 255, 255);

                for (int r = 0; r < 32; r++)
                {
                    int y = 31 - r;
                    string line = pattern[r];
                    for (int x = 0; x < 32; x++)
                    {
                        char c = line[x];
                        if (c == 'X')
                        {
                            cCols[y * cw + x] = (r == 0 && x == 0) ? tipCol : borderCol;
                        }
                        else if (c == 'O')
                        {
                            cCols[y * cw + x] = fillCol;
                        }
                    }
                }

                _cachedCursorTexture.SetPixels32(cCols);
                _cachedCursorTexture.Apply();
            }

            try
            {
                Cursor.SetCursor(_cachedCursorTexture, Vector2.zero, CursorMode.Auto);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
        #endregion

        public static GameObject BuildPauseMenu(InGamePauseManager pauseMgr, SettingsManager settingsMgr, ControlsMenuDisplay controlsDisplay)
        {
            if (pauseMgr == null) return null;
            Cyberpunk.UI.UGUISelectableFix.Repair();

            // 1. Root Canvas
            GameObject canvasObj = new GameObject("Canvas_PauseMenu");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9000;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();
            CanvasGroup rootGroup = canvasObj.AddComponent<CanvasGroup>();
            rootGroup.alpha = 1f;
            rootGroup.interactable = true;
            rootGroup.blocksRaycasts = true;
            pauseMgr.canvasGroup = rootGroup;
            pauseMgr.pauseCanvasOverlay = canvasObj;

            // 2. Fullscreen Scrim Background with Dark Vignette (character & neon city visible behind)
            GameObject dimObj = CreateRectChild(canvasObj, "DimBackground", Vector2.zero, new Vector2(1920, 1080));
            SetStretch(dimObj);
            Image dimImg = dimObj.AddComponent<Image>();
            dimImg.color = ScrimDark;

            // 3. Screen A: PAUSE MAIN PANEL (Bottom-Left in reference image)
            GameObject pauseMainPanel = CreateRectChild(canvasObj, "Panel_PauseMain", Vector2.zero, new Vector2(1920, 1080));
            SetStretch(pauseMainPanel);
            BuildPauseMainView(pauseMainPanel, pauseMgr);
            pauseMgr.pauseMainPanel = pauseMainPanel;

            // 4. Screen B: SETTINGS PANEL (Top-Right, Bottom-Middle, Bottom-Right in reference image)
            GameObject settingsPanel = CreateRectChild(canvasObj, "Panel_PauseSettings", Vector2.zero, new Vector2(1920, 1080));
            SetStretch(settingsPanel);
            try
            {
                BuildSettingsWindow(settingsPanel, settingsMgr, pauseMgr, controlsDisplay);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            pauseMgr.settingsPanel = settingsPanel;
            settingsPanel.SetActive(false);

            // 5. Screen C: Confirmation Modal Dialog
            GameObject confirmPopup = CreateRectChild(canvasObj, "Popup_Confirm", Vector2.zero, new Vector2(1920, 1080));
            SetStretch(confirmPopup);
            BuildConfirmationModal(confirmPopup, pauseMgr);
            confirmPopup.SetActive(false);

            // Hook all pause and subpanel button click listeners
            pauseMgr.AutoBindCanvasReferences();
            pauseMgr.HookButtons();

            canvasObj.SetActive(false);
            return canvasObj;
        }

        #region Screen A: Authentic Cyberpunk 2077 Phantom Liberty Menu
        private static void BuildPauseMainView(GameObject root, InGamePauseManager pauseMgr)
        {
            // Full-height Left Holographic Column (Red/Crimson Phantom Liberty strip)
            GameObject holoColumn = CreateRectChild(root, "Column_PhantomLiberty", Vector2.zero, new Vector2(480, 1080));
            RectTransform colRt = holoColumn.GetComponent<RectTransform>();
            colRt.anchorMin = new Vector2(0f, 0f);
            colRt.anchorMax = new Vector2(0f, 1f);
            colRt.pivot = new Vector2(0f, 0.5f);
            colRt.anchoredPosition = Vector2.zero;
            colRt.sizeDelta = new Vector2(480f, 0f);
            pauseMgr.mainColumnRect = colRt;

            // Deep crimson holographic background with subtle red right-edge rim
            Image colBg = holoColumn.AddComponent<Image>();
            colBg.sprite = GetRedHoloColumnSprite();
            colBg.type = Image.Type.Simple;
            colBg.color = Color.white;
            colBg.raycastTarget = false;

            // Far-left Cyberpunk Yellow Measurement Ruler Ticks
            GameObject rulerObj = CreateRectChild(holoColumn, "Ruler_LeftTicks", Vector2.zero, new Vector2(16, 1080));
            RectTransform rulerRt = rulerObj.GetComponent<RectTransform>();
            rulerRt.anchorMin = new Vector2(0f, 0f);
            rulerRt.anchorMax = new Vector2(0f, 1f);
            rulerRt.pivot = new Vector2(0f, 0.5f);
            rulerRt.anchoredPosition = new Vector2(4f, 0f);
            rulerRt.sizeDelta = new Vector2(16f, 0f);
            Image rulerImg = rulerObj.AddComponent<Image>();
            rulerImg.sprite = GetYellowRulerSprite();
            rulerImg.type = Image.Type.Tiled;
            rulerImg.color = CyberYellow;
            rulerImg.raycastTarget = false;

            // Header Container: Alita Battle Angel Typographical Logo (No character image)
            GameObject headerContainer = CreateRectChild(holoColumn, "Header_AlitaLogo", new Vector2(44f, -36f), new Vector2(400f, 130f));
            RectTransform hRt = headerContainer.GetComponent<RectTransform>();
            hRt.anchorMin = new Vector2(0f, 1f);
            hRt.anchorMax = new Vector2(0f, 1f);
            hRt.pivot = new Vector2(0f, 1f);
            hRt.anchoredPosition = new Vector2(44f, -36f);
            hRt.sizeDelta = new Vector2(400f, 130f);

            // ALITA BATTLE ANGEL Pure Typographical Logo
            TMP_Text logoText = CreateTMP(headerContainer, "Text_AlitaLogo",
                "<color=#FCEE0A><b>ALITA</b></color>\n<size=17><cspace=6><color=#00E5FF>BATTLE ANGEL</color></cspace></size>",
                36, TextAlignmentOptions.Left, new Vector2(0f, 16f), new Vector2(380f, 72f), true);
            logoText.fontStyle = FontStyles.Bold;
            logoText.raycastTarget = false;

            // System Pause Subtitle
            TMP_Text plText = CreateTMP(headerContainer, "Text_PauseProtocol",
                "<color=#FF2A36>SYSTEM PAUSE</color> <color=#777777>//</color> <color=#EEEEEE>TACTICAL PROTOCOL</color>",
                12, TextAlignmentOptions.Left, new Vector2(0f, -42f), new Vector2(380f, 22f), false);
            plText.fontStyle = FontStyles.Bold;
            plText.raycastTarget = false;

            // Subtle red divider rule under header
            GameObject headerRule = CreateRectChild(headerContainer, "Divider_Rule", new Vector2(0f, -66f), new Vector2(380f, 2f));
            Image ruleImg = headerRule.AddComponent<Image>();
            ruleImg.color = new Color(0.85f, 0.18f, 0.22f, 0.50f);
            ruleImg.raycastTarget = false;

            // Vertical Button Stack
            GameObject btnStack = CreateRectChild(holoColumn, "ButtonStack", new Vector2(44f, -196f), new Vector2(400f, 540f));
            RectTransform stackRt = btnStack.GetComponent<RectTransform>();
            stackRt.anchorMin = new Vector2(0f, 1f);
            stackRt.anchorMax = new Vector2(0f, 1f);
            stackRt.pivot = new Vector2(0f, 1f);
            stackRt.anchoredPosition = new Vector2(44f, -196f);
            stackRt.sizeDelta = new Vector2(400f, 540f);

            float yStep = -52f;
            float curY = 0f;

            // 1. RESUME (Selected by default)
            Button btnResume = CreatePhantomLibertyMenuItem(btnStack, "Btn_Resume", "RESUME", new Vector2(0, curY), pauseMgr, true);
            pauseMgr.resumeButton = btnResume;
            btnResume.onClick.AddListener(() =>
            {
                pauseMgr.PlayClickAudio();
                pauseMgr.ResumeGame();
            });
            curY += yStep;

            // 2. SETTINGS
            Button btnSettings = CreatePhantomLibertyMenuItem(btnStack, "Btn_Settings", "SETTINGS", new Vector2(0, curY), pauseMgr, false);
            pauseMgr.settingsButton = btnSettings;
            btnSettings.onClick.AddListener(() =>
            {
                pauseMgr.PlayClickAudio();
                pauseMgr.ShowSettings();
            });
            curY += yStep;

            // 3. CONTROLS
            Button btnControls = CreatePhantomLibertyMenuItem(btnStack, "Btn_Controls", "CONTROLS", new Vector2(0, curY), pauseMgr, false);
            pauseMgr.controlsButton = btnControls;
            btnControls.onClick.AddListener(() =>
            {
                pauseMgr.PlayClickAudio();
                pauseMgr.ShowControls();
            });
            curY += yStep;

            // 4. PHOTO MODE
            Button btnPhotoMode = CreatePhantomLibertyMenuItem(btnStack, "Btn_PhotoMode", "PHOTO MODE", new Vector2(0, curY), pauseMgr, false);
            pauseMgr.photoModeButton = btnPhotoMode;
            btnPhotoMode.onClick.AddListener(() =>
            {
                pauseMgr.PlayClickAudio();
                pauseMgr.ShowPhotoMode();
            });
            curY += yStep;

            // 5. RESTART CHECKPOINT
            Button btnRestart = CreatePhantomLibertyMenuItem(btnStack, "Btn_Restart", "RESTART CHECKPOINT", new Vector2(0, curY), pauseMgr, false);
            pauseMgr.restartButton = btnRestart;
            btnRestart.onClick.AddListener(() =>
            {
                pauseMgr.PlayClickAudio();
                pauseMgr.RequestRestartCheckpoint();
            });
            curY += yStep;

            // 6. QUIT TO MAIN MENU
            Button btnMainMenu = CreatePhantomLibertyMenuItem(btnStack, "Btn_MainMenu", "QUIT TO MAIN MENU", new Vector2(0, curY), pauseMgr, false);
            pauseMgr.mainMenuButton = btnMainMenu;
            btnMainMenu.onClick.AddListener(() =>
            {
                pauseMgr.PlayClickAudio();
                pauseMgr.RequestReturnToMainMenu();
            });
            curY += yStep;

            // 7. QUIT TO DESKTOP
            Button btnQuit = CreatePhantomLibertyMenuItem(btnStack, "Btn_Quit", "QUIT TO DESKTOP", new Vector2(0, curY), pauseMgr, false);
            pauseMgr.quitDesktopButton = btnQuit;
            btnQuit.onClick.AddListener(() =>
            {
                pauseMgr.PlayClickAudio();
                pauseMgr.RequestQuitDesktop();
            });


            // Footer telemetry: "v2.31 // NIGHT CITY OS"
            TMP_Text footerVer = CreateTMP(holoColumn, "Text_FooterVersion", "v2.31 // NIGHT CITY OS", 12f, TextAlignmentOptions.Left, new Vector2(44f, 22f), new Vector2(250f, 20f), false);
            RectTransform fvRt = footerVer.GetComponent<RectTransform>();
            fvRt.anchorMin = new Vector2(0f, 0f);
            fvRt.anchorMax = new Vector2(0f, 0f);
            fvRt.pivot = new Vector2(0f, 0f);
            fvRt.anchoredPosition = new Vector2(44f, 22f);
            footerVer.color = new Color(0.70f, 0.22f, 0.24f, 0.75f);
            footerVer.fontStyle = FontStyles.Bold;
            footerVer.raycastTarget = false;
        }

        private static Button CreatePhantomLibertyMenuItem(GameObject parent, string name, string label, Vector2 pos, InGamePauseManager pauseMgr, bool isDefaultSelected = false)
        {
            GameObject btnObj = CreateRectChild(parent, name, pos, new Vector2(380, 44));
            RectTransform btnRt = btnObj.GetComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0f, 1f);
            btnRt.anchorMax = new Vector2(0f, 1f);
            btnRt.pivot = new Vector2(0f, 1f);
            btnRt.anchoredPosition = pos;
            btnRt.sizeDelta = new Vector2(380, 44);

            // 1. Raycast Hitbox Graphic (CRITICAL: enables GraphicRaycaster to register mouse hover and pointer clicks!)
            Image hitImg = btnObj.AddComponent<Image>();
            hitImg.color = Color.clear;
            hitImg.raycastTarget = true;

            // Selected yellow box outline with subtle dark crimson/amber fill
            GameObject selectedBox = CreateRectChild(btnObj, "Box_SelectedOutline", Vector2.zero, new Vector2(380, 44));
            SetStretch(selectedBox);
            Image boxImg = selectedBox.AddComponent<Image>();
            boxImg.sprite = GetYellowMenuItemBorderSprite();
            boxImg.type = Image.Type.Sliced;
            boxImg.color = Color.white;
            boxImg.raycastTarget = false;

            // Cyan Hand Pointer Icon (👉)
            GameObject handObj = CreateRectChild(selectedBox, "Icon_CyanHand", new Vector2(-66f, 0f), new Vector2(24, 24));
            RectTransform handRt = handObj.GetComponent<RectTransform>();
            handRt.anchorMin = new Vector2(1f, 0.5f);
            handRt.anchorMax = new Vector2(1f, 0.5f);
            handRt.pivot = new Vector2(0.5f, 0.5f);
            handRt.anchoredPosition = new Vector2(-66f, 0f);
            Image handImg = handObj.AddComponent<Image>();
            handImg.sprite = GetCyanHandCursorSprite();
            handImg.color = FrameBorderCyan; // electric cyan (#00E5FF)
            handImg.raycastTarget = false;

            // Yellow Protocol Tech Badge
            GameObject badgeObj = CreateRectChild(selectedBox, "Icon_TechBadge", new Vector2(-26f, 0f), new Vector2(28, 16));
            RectTransform badgeRt = badgeObj.GetComponent<RectTransform>();
            badgeRt.anchorMin = new Vector2(1f, 0.5f);
            badgeRt.anchorMax = new Vector2(1f, 0.5f);
            badgeRt.pivot = new Vector2(0.5f, 0.5f);
            badgeRt.anchoredPosition = new Vector2(-26f, 0f);
            Image badgeImg = badgeObj.AddComponent<Image>();
            badgeImg.sprite = GetTechBadgeSprite();
            badgeImg.color = CyberYellow; // #FCEE0A
            badgeImg.raycastTarget = false;

            // Button Text: Muted Crimson by default, Yellow when selected
            TMP_Text tmp = CreateTMP(btnObj, "Text_Label", label, 19f, TextAlignmentOptions.Left, new Vector2(16f, 0f), new Vector2(250f, 38f), true);
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = isDefaultSelected ? CyberYellow : CrimsonMuted;
            tmp.raycastTarget = false;

            selectedBox.SetActive(isDefaultSelected);

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = hitImg;
            btn.transition = Selectable.Transition.None;

            var itemFx = btnObj.AddComponent<CyberpunkPhantomLibertyMenuItem>();
            itemFx.labelText = tmp;
            itemFx.selectedBox = selectedBox;
            itemFx.pauseMgr = pauseMgr;

            return btn;
        }

        private static void BuildKeyPrompt(GameObject parent, string name, string key, string label, Vector2 pos)
        {
            GameObject container = CreateRectChild(parent, name, pos, new Vector2(160, 36));

            GameObject badge = CreateRectChild(container, "Badge", new Vector2(-35, 0), new Vector2(key.Length > 3 ? 54 : 38, 26));
            Image badgeImg = badge.AddComponent<Image>();
            badgeImg.sprite = GetPillSprite();
            badgeImg.type = Image.Type.Sliced;
            badgeImg.color = new Color(0.04f, 0.08f, 0.16f, 0.85f);

            Outline badgeOutline = badge.AddComponent<Outline>();
            badgeOutline.effectColor = new Color(0f, 0.85f, 1f, 0.45f);

            TMP_Text keyTxt = CreateTMP(badge, "KeyTxt", key, 12, TextAlignmentOptions.Center, Vector2.zero, badge.GetComponent<RectTransform>().sizeDelta, false);
            keyTxt.color = FrameBorderCyan;
            keyTxt.fontStyle = FontStyles.Bold;

            TMP_Text labelTxt = CreateTMP(container, "LabelTxt", label, 13, TextAlignmentOptions.Left, new Vector2(25, 0), new Vector2(80, 26), false);
            labelTxt.color = TextMutedCyan;
            labelTxt.fontStyle = FontStyles.Bold;
        }
        #endregion

        #region Screen B: Full SETTINGS Window (Top-Right / Bottom-Middle / Bottom-Right)
        private static void BuildSettingsWindow(GameObject root, SettingsManager settingsMgr, InGamePauseManager pauseMgr, ControlsMenuDisplay controlsDisplay)
        {
            // Self-heal: never build rows against a null manager (would leave dead/empty pages).
            if (settingsMgr == null)
            {
                try { settingsMgr = SettingsManager.Instance; }
                catch (Exception ex) { Debug.LogException(ex); }
            }
            if (settingsMgr == null && pauseMgr != null)
            {
                try { settingsMgr = pauseMgr.GetComponent<SettingsManager>(); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
            if (settingsMgr == null && pauseMgr != null)
            {
                try { settingsMgr = pauseMgr.gameObject.AddComponent<SettingsManager>(); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
            // Center Floating Holographic Window Frame
            GameObject window = CreateRectChild(root, "SettingsWindow", Vector2.zero, new Vector2(1440, 840));
            Image windowBg = window.AddComponent<Image>();
            windowBg.sprite = GetPillSprite();
            windowBg.type = Image.Type.Sliced;
            windowBg.color = WindowDark;

            Outline winOutline = window.AddComponent<Outline>();
            winOutline.effectColor = FrameBorderDim;
            winOutline.effectDistance = new Vector2(1.5f, -1.5f);

            // Sci-Fi Corner Brackets
            AddCornerBrackets(window);

            // 1. Top Header Bar: Gear Icon + "SETTINGS" + Localized Breadcrumb Title + Back button
            GameObject headerBar = CreateRectChild(window, "HeaderBar", new Vector2(0, 380), new Vector2(1380, 52));

            GameObject gearObj = CreateRectChild(headerBar, "Icon_Gear", new Vector2(-655, 0), new Vector2(28, 28));
            Image gearImg = gearObj.AddComponent<Image>();
            gearImg.sprite = GetGearIconSprite();
            gearImg.color = FrameBorderCyan;

            TMP_Text titleText = CreateTMP(headerBar, "Text_Title", "SETTINGS // GRAPHICS", 22, TextAlignmentOptions.Left, new Vector2(-420, 0), new Vector2(440, 40), true);
            titleText.color = TextWhite;
            titleText.fontStyle = FontStyles.Bold;

            // Back button on top right [ESC] BACK
            Button btnTopBack = CreateActionButton(headerBar, "Btn_TopBack", "[ESC] BACK", new Vector2(610, 0), new Vector2(120, 36), false);
            btnTopBack.onClick.AddListener(() =>
            {
                pauseMgr.PlayBackAudio();
                pauseMgr.ShowPauseMain();
            });
            pauseMgr.settingsBackButton = btnTopBack;

            // Divider Line (decorative: must never intercept row clicks)
            GameObject divLine = CreateRectChild(window, "Line_Divider", new Vector2(0, 348), new Vector2(1380, 1.5f));
            Image divImg = divLine.AddComponent<Image>();
            divImg.color = FrameBorderDim;
            divImg.raycastTarget = false;

            // 2. Navigation Tab Bar: [Q] [GRAPHICS] [AUDIO] [CONTROLS] [GAMEPLAY] [INTERFACE] [E]
            GameObject tabBar = CreateRectChild(window, "TabBar", new Vector2(0, 312), new Vector2(1380, 44));
            HorizontalLayoutGroup tabHlg = tabBar.AddComponent<HorizontalLayoutGroup>();
            tabHlg.spacing = 18f;
            tabHlg.childAlignment = TextAnchor.MiddleCenter;
            tabHlg.childControlWidth = true;
            tabHlg.childControlHeight = true;
            tabHlg.childForceExpandWidth = false;
            tabHlg.childForceExpandHeight = false;

            // Late-bound refs declared BEFORE tab creation so every tab/Q/E listener
            // can attach IMMEDIATELY at creation and never depend on post-build wiring.
            Button[] tabs = new Button[5];
            GameObject[] sections = new GameObject[5];
            GameObject cardPreview = null;
            GameObject cardControlsSchematic = null;
            string[] breadcrumbTitles = new string[]
            {
                "SETTINGS // GRAPHICS",
                "SETTINGS  >  AUDIO",
                "SETTINGS  >  CONTROLS",
                "SETTINGS  >  GAMEPLAY",
                "SETTINGS  >  INTERFACE"
            };
            int activeTabIndex = 0;
            Action<int> switchTab = (idx) =>
            {
                try
                {
                    if (tabs == null || tabs.Length == 0) return;
                    if (idx < 0) idx = tabs.Length - 1;
                    if (idx >= tabs.Length) idx = 0;
                    activeTabIndex = idx;

                    try { if (pauseMgr != null) pauseMgr.PlayClickAudio(); }
                    catch (Exception ex) { Debug.LogException(ex); }

                    try { if (titleText != null && breadcrumbTitles != null && idx >= 0 && idx < breadcrumbTitles.Length) titleText.text = breadcrumbTitles[idx]; }
                    catch (Exception ex) { Debug.LogException(ex); }

                    for (int i = 0; i < tabs.Length; i++)
                    {
                        try
                        {
                            Button t = tabs[i];
                            if (t == null) continue;
                            SetTabActiveStyle(t, i == idx);
                            GameObject s = (sections != null && i < sections.Length) ? sections[i] : null;
                            if (s == null) continue;
                            s.SetActive(i == idx);
                        }
                        catch (Exception ex) { Debug.LogException(ex); }
                    }

                    bool isControls = (idx == 2);
                    try { if (cardPreview != null) cardPreview.SetActive(!isControls); }
                    catch (Exception ex) { Debug.LogException(ex); }
                    try { if (cardControlsSchematic != null) cardControlsSchematic.SetActive(isControls); }
                    catch (Exception ex) { Debug.LogException(ex); }
                }
                catch (Exception ex) { Debug.LogException(ex); }
            };

            Button btnKeyQ = CreateKeyHintButton(tabBar, "Btn_KeyQ", "Q");
            try { btnKeyQ.onClick.AddListener(() => { try { switchTab(activeTabIndex - 1); } catch (Exception ex) { Debug.LogException(ex); } }); }
            catch (Exception ex) { Debug.LogException(ex); }
            Button tabGraphics = CreateTabButton(tabBar, "Tab_Graphics", "GRAPHICS", true);
            tabs[0] = tabGraphics;
            int tabIdx0 = 0;
            try { tabGraphics.onClick.AddListener(() => { try { switchTab(tabIdx0); } catch (Exception ex) { Debug.LogException(ex); } }); }
            catch (Exception ex) { Debug.LogException(ex); }
            Button tabAudio = CreateTabButton(tabBar, "Tab_Audio", "AUDIO", false);
            tabs[1] = tabAudio;
            int tabIdx1 = 1;
            try { tabAudio.onClick.AddListener(() => { try { switchTab(tabIdx1); } catch (Exception ex) { Debug.LogException(ex); } }); }
            catch (Exception ex) { Debug.LogException(ex); }
            Button tabControls = CreateTabButton(tabBar, "Tab_Controls", "CONTROLS", false);
            tabs[2] = tabControls;
            int tabIdx2 = 2;
            try { tabControls.onClick.AddListener(() => { try { switchTab(tabIdx2); } catch (Exception ex) { Debug.LogException(ex); } }); }
            catch (Exception ex) { Debug.LogException(ex); }
            Button tabGameplay = CreateTabButton(tabBar, "Tab_Gameplay", "GAMEPLAY", false);
            tabs[3] = tabGameplay;
            int tabIdx3 = 3;
            try { tabGameplay.onClick.AddListener(() => { try { switchTab(tabIdx3); } catch (Exception ex) { Debug.LogException(ex); } }); }
            catch (Exception ex) { Debug.LogException(ex); }
            Button tabInterface = CreateTabButton(tabBar, "Tab_Interface", "INTERFACE", false);
            tabs[4] = tabInterface;
            int tabIdx4 = 4;
            try { tabInterface.onClick.AddListener(() => { try { switchTab(tabIdx4); } catch (Exception ex) { Debug.LogException(ex); } }); }
            catch (Exception ex) { Debug.LogException(ex); }
            Button btnKeyE = CreateKeyHintButton(tabBar, "Btn_KeyE", "E");
            try { btnKeyE.onClick.AddListener(() => { try { switchTab(activeTabIndex + 1); } catch (Exception ex) { Debug.LogException(ex); } }); }
            catch (Exception ex) { Debug.LogException(ex); }

            try { LayoutRebuilder.ForceRebuildLayoutImmediate(tabBar.GetComponent<RectTransform>()); }
            catch (Exception ex) { Debug.LogException(ex); }

            // 3. Main Body Container (Split into Left Options Stack & Right Preview/Diagram Card)
            GameObject bodyContainer = CreateRectChild(window, "BodyContainer", new Vector2(0, -35), new Vector2(1380, 610));

            // Left Options Column (width 860)
            GameObject leftStack = CreateRectChild(bodyContainer, "LeftOptionsStack", new Vector2(-250, 0), new Vector2(860, 590));

            // Section 1: Graphics (isolated: one bad row must never kill other pages)
            GameObject secGraphics = CreateScrollableSection(leftStack, "Section_Graphics");
            try { BuildGraphicsOptions(secGraphics, settingsMgr); }
            catch (Exception ex) { Debug.LogException(ex); }
            sections[0] = secGraphics;
            try { UpgradeToScrollView(secGraphics, 590f); }
            catch (Exception ex) { Debug.LogException(ex); }

            // Section 2: Audio
            GameObject secAudio = CreateScrollableSection(leftStack, "Section_Audio");
            try { BuildAudioOptions(secAudio, settingsMgr); }
            catch (Exception ex) { Debug.LogException(ex); }
            sections[1] = secAudio;
            try { UpgradeToScrollView(secAudio, 590f); }
            catch (Exception ex) { Debug.LogException(ex); }
            try { secAudio.SetActive(false); } catch (Exception ex) { Debug.LogException(ex); }

            // Right Column Card (width 470)
            GameObject rightStack = CreateRectChild(bodyContainer, "RightStack", new Vector2(435, 0), new Vector2(470, 590));

            // Right Card 1: Graphics Preview Card (Thumbnail image + Title + Description)
            cardPreview = CreateRectChild(rightStack, "Card_Preview", Vector2.zero, new Vector2(470, 590));
            try { BuildPreviewCard(cardPreview, settingsMgr); }
            catch (Exception ex) { Debug.LogException(ex); }

            // Right Card 2: Controls Hardware Schematics Card (Swappable Mouse / Gamepad / Telemetry)
            cardControlsSchematic = CreateRectChild(rightStack, "Card_ControlsSchematic", Vector2.zero, new Vector2(470, 590));
            try { cardControlsSchematic.SetActive(false); } catch (Exception ex) { Debug.LogException(ex); }

            // Section 3: Controls (Integrated sub-tabs & schematics)
            GameObject secControls = CreateRectChild(leftStack, "Section_Controls", Vector2.zero, new Vector2(860, 590));
            SetStretch(secControls);
            try { BuildControlsSystem(secControls, cardControlsSchematic, settingsMgr, pauseMgr); }
            catch (Exception ex) { Debug.LogException(ex); }
            sections[2] = secControls;
            try { secControls.SetActive(false); } catch (Exception ex) { Debug.LogException(ex); }

            // Section 4: Gameplay
            GameObject secGameplay = CreateScrollableSection(leftStack, "Section_Gameplay");
            try { BuildGameplayOptions(secGameplay, settingsMgr); }
            catch (Exception ex) { Debug.LogException(ex); }
            sections[3] = secGameplay;
            try { UpgradeToScrollView(secGameplay, 590f); }
            catch (Exception ex) { Debug.LogException(ex); }
            try { secGameplay.SetActive(false); } catch (Exception ex) { Debug.LogException(ex); }

            // Section 5: Interface
            GameObject secInterface = CreateScrollableSection(leftStack, "Section_Interface");
            try { BuildInterfaceOptions(secInterface, settingsMgr); }
            catch (Exception ex) { Debug.LogException(ex); }
            sections[4] = secInterface;
            try { UpgradeToScrollView(secInterface, 590f); }
            catch (Exception ex) { Debug.LogException(ex); }
            try { secInterface.SetActive(false); } catch (Exception ex) { Debug.LogException(ex); }

            // Tab listeners + Q/E already attached at creation above (no post-build loop).
            // Wire keyboard Q/E hook to pause manager (guarded; switchTab itself is null-safe).
            try
            {
                if (pauseMgr != null)
                {
                    pauseMgr.OnCycleTab = (dir) => { try { switchTab(activeTabIndex + dir); } catch (Exception ex) { Debug.LogException(ex); } };
                    pauseMgr.OnSelectTab = (idx) => { try { switchTab(idx); } catch (Exception ex) { Debug.LogException(ex); } };
                    pauseMgr.OnResetSettings = () =>
                    {
                        try
                        {
                            if (settingsMgr != null) settingsMgr.ResetToDefaults();
                            switchTab(activeTabIndex);
                        }
                        catch (Exception ex) { Debug.LogException(ex); }
                    };
                }
            }
            catch (Exception ex) { Debug.LogException(ex); }

            // 4. Bottom Action Bar: [R] RESET TO DEFAULT & [APPLY]
            GameObject footerBar = CreateRectChild(window, "FooterBar", new Vector2(0, -375), new Vector2(1380, 46));

            Button btnReset = CreateActionButton(footerBar, "Btn_ResetDefaults", "[R]  RESET TO DEFAULT", new Vector2(430, 0), new Vector2(210, 40), false);
            try
            {
                Outline resetOutline = btnReset.GetComponent<Outline>();
                if (resetOutline != null) resetOutline.effectColor = FrameBorderDim;
                var resetFx = btnReset.GetComponent<CyberUIInteractiveEffect>();
                if (resetFx != null) resetFx.hoverColor = RowBgHover;
            }
            catch (Exception ex) { Debug.LogException(ex); }
            try
            {
                btnReset.onClick.AddListener(() =>
                {
                    try
                    {
                        if (pauseMgr != null) pauseMgr.PlayClickAudio();
                        if (settingsMgr != null) settingsMgr.ResetToDefaults();
                        switchTab(activeTabIndex);
                    }
                    catch (Exception ex) { Debug.LogException(ex); }
                });
            }
            catch (Exception ex) { Debug.LogException(ex); }

            Button btnApply = CreateActionButton(footerBar, "Btn_Apply", "APPLY", new Vector2(620, 0), new Vector2(130, 40), true);
            try
            {
                var applyFx = btnApply.GetComponent<CyberUIInteractiveEffect>();
                if (applyFx != null) applyFx.hoverColor = BtnActiveBg;
            }
            catch (Exception ex) { Debug.LogException(ex); }
            try
            {
                btnApply.onClick.AddListener(() =>
                {
                    try
                    {
                        if (pauseMgr != null) pauseMgr.PlayClickAudio();
                        if (settingsMgr == null) return;
                        settingsMgr.SaveAll();
                        settingsMgr.LoadAll();
                    }
                    catch (Exception ex) { Debug.LogException(ex); }
                });
            }
            catch (Exception ex) { Debug.LogException(ex); }

            // Guarantee initial state explicitly so GRAPHICS shows even if something above partially failed.
            try { switchTab(0); }
            catch (Exception ex) { Debug.LogException(ex); }
        }

        private static void AddCornerBrackets(GameObject parent)
        {
            Sprite cornerSp = GetCornerBracketSprite();
            if (cornerSp == null) return;

            Vector2 halfSize = parent.GetComponent<RectTransform>().sizeDelta * 0.5f;
            float offset = 14f;

            // Top-Left
            CreateCorner(parent, "Corner_TL", new Vector2(-halfSize.x + offset, halfSize.y - offset), 0f, cornerSp);
            // Top-Right
            CreateCorner(parent, "Corner_TR", new Vector2(halfSize.x - offset, halfSize.y - offset), -90f, cornerSp);
            // Bottom-Right
            CreateCorner(parent, "Corner_BR", new Vector2(halfSize.x - offset, -halfSize.y + offset), 180f, cornerSp);
            // Bottom-Left
            CreateCorner(parent, "Corner_BL", new Vector2(-halfSize.x + offset, -halfSize.y + offset), 90f, cornerSp);
        }

        private static void CreateCorner(GameObject parent, string name, Vector2 pos, float rotZ, Sprite sp)
        {
            GameObject cObj = CreateRectChild(parent, name, pos, new Vector2(22, 22));
            Image img = cObj.AddComponent<Image>();
            img.sprite = sp;
            img.color = FrameBorderCyan;
            img.raycastTarget = false;
            cObj.transform.localRotation = Quaternion.Euler(0, 0, rotZ);
        }

        private static Button CreateKeyHintButton(GameObject parent, string name, string key)
        {
            GameObject btnObj = CreateRectChild(parent, name, Vector2.zero, new Vector2(36, 36));
            LayoutElement le = btnObj.AddComponent<LayoutElement>();
            le.minWidth = 36f;
            le.preferredWidth = 36f;
            le.minHeight = 36f;
            le.preferredHeight = 36f;
            le.flexibleWidth = 0f;
            le.flexibleHeight = 0f;

            Image bg = btnObj.AddComponent<Image>();
            bg.sprite = GetPillSprite();
            bg.type = Image.Type.Sliced;
            bg.color = BtnInactiveBg;

            Outline outline = btnObj.AddComponent<Outline>();
            outline.effectColor = FrameBorderDim;

            TMP_Text tmp = CreateTMP(btnObj, "Text", key, 14, TextAlignmentOptions.Center, Vector2.zero, new Vector2(34, 32), false);
            tmp.color = TextMutedCyan;
            tmp.fontStyle = FontStyles.Bold;

            Button btn = btnObj.AddComponent<Button>();
            btnObj.AddComponent<CyberUIInteractiveEffect>();
            return btn;
        }

        private static Button CreateTabButton(GameObject parent, string name, string label, bool isActive)
        {
            GameObject btnObj = CreateRectChild(parent, name, Vector2.zero, new Vector2(160, 38));
            LayoutElement le = btnObj.AddComponent<LayoutElement>();
            le.minWidth = 140f;
            le.preferredWidth = 160f;
            le.minHeight = 38f;
            le.preferredHeight = 38f;
            le.flexibleWidth = 0f;
            le.flexibleHeight = 0f;

            Image bg = btnObj.AddComponent<Image>();
            bg.sprite = GetPillSprite();
            bg.type = Image.Type.Sliced;
            bg.color = isActive ? BtnActiveBg : Color.clear;

            Outline outline = btnObj.AddComponent<Outline>();
            outline.effectColor = isActive ? FrameBorderCyan : Color.clear;
            outline.effectDistance = new Vector2(1.2f, -1.2f);

            TMP_Text tmp = CreateTMP(btnObj, "Text", label, 15, TextAlignmentOptions.Center, Vector2.zero, new Vector2(150, 32), false);
            tmp.color = isActive ? Color.black : TextMutedCyan;
            tmp.fontStyle = FontStyles.Bold;

            Button btn = btnObj.AddComponent<Button>();
            var fx = btnObj.AddComponent<CyberUIInteractiveEffect>();
            fx.normalColor = isActive ? BtnActiveBg : Color.clear;
            fx.hoverColor = RowBgHover;
            return btn;
        }

        public static void SetTabActiveStyle(Button btn, bool isActive)
        {
            if (btn == null) return;
            Image bg = btn.GetComponent<Image>();
            if (bg != null) bg.color = isActive ? BtnActiveBg : Color.clear;
            Outline outline = btn.GetComponent<Outline>();
            if (outline != null) outline.effectColor = isActive ? FrameBorderCyan : Color.clear;
            TMP_Text tmp = btn.GetComponentInChildren<TMP_Text>();
            if (tmp != null) tmp.color = isActive ? Color.black : TextMutedCyan;
            var fx = btn.GetComponent<CyberUIInteractiveEffect>();
            if (fx != null)
            {
                fx.normalColor = isActive ? BtnActiveBg : Color.clear;
            }
        }

        private static GameObject CreateScrollableSection(GameObject parent, string name)
        {
            GameObject secObj = CreateRectChild(parent, name, Vector2.zero, new Vector2(860, 590));
            SetStretch(secObj);

            VerticalLayoutGroup vlg = secObj.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(4, 4, 4, 4);

            return secObj;
        }

        /// <summary>
        /// Measures a built section: rows x height + spacing + padding. Only when the
        /// content actually overflows the viewport does it get wrapped in a ScrollRect
        /// (viewport + RectMask2D + slim Scrollbar, elastic off, sensitivity ~30).
        /// Existing rows become the scroll content with a vertical ContentSizeFitter.
        /// No-op when everything fits, so fitting pages keep pixel-identical layout.
        /// </summary>
        private static void UpgradeToScrollView(GameObject sectionRoot, float viewportHeight)
        {
            try
            {
                if (sectionRoot == null) return;
                VerticalLayoutGroup srcVlg = sectionRoot.GetComponent<VerticalLayoutGroup>();
                if (srcVlg == null) return;

                int rowCount = sectionRoot.transform.childCount;
                if (rowCount == 0) return;

                float total = 0f;
                try { total += srcVlg.padding.top + srcVlg.padding.bottom; }
                catch (Exception ex) { Debug.LogException(ex); }
                for (int i = 0; i < rowCount; i++)
                {
                    try
                    {
                        RectTransform child = sectionRoot.transform.GetChild(i) as RectTransform;
                        if (child == null) continue;
                        LayoutElement le = child.GetComponent<LayoutElement>();
                        float h = (le != null)
                            ? Mathf.Max(le.minHeight, Mathf.Max(le.preferredHeight, 1f))
                            : Mathf.Max(child.sizeDelta.y, 1f);
                        total += h;
                    }
                    catch (Exception ex) { Debug.LogException(ex); }
                }
                if (rowCount > 1)
                {
                    try { total += srcVlg.spacing * (rowCount - 1); }
                    catch (Exception ex) { Debug.LogException(ex); }
                }

                // Fits: leave the flat layout untouched.
                if (total <= viewportHeight + 1f) return;

                // Viewport (room left on the right edge for the slim scrollbar).
                GameObject viewport = CreateRectChild(sectionRoot, "Viewport", Vector2.zero, Vector2.zero);
                SetStretch(viewport);
                RectTransform vpRt = viewport.GetComponent<RectTransform>();
                if (vpRt != null) vpRt.offsetMax = new Vector2(-18f, 0f);
                viewport.AddComponent<RectMask2D>();

                // Content keeps the rows + vertical fit.
                GameObject content = new GameObject("Content");
                try { content.transform.SetParent(viewport.transform, false); }
                catch (Exception ex) { Debug.LogException(ex); }
                RectTransform cRt = content.AddComponent<RectTransform>();
                cRt.anchorMin = new Vector2(0f, 1f);
                cRt.anchorMax = new Vector2(1f, 1f);
                cRt.pivot = new Vector2(0.5f, 1f);
                cRt.offsetMin = Vector2.zero;
                cRt.offsetMax = Vector2.zero;
                cRt.sizeDelta = new Vector2(0f, viewportHeight);
                VerticalLayoutGroup dstVlg = content.AddComponent<VerticalLayoutGroup>();
                try
                {
                    dstVlg.spacing = srcVlg.spacing;
                    dstVlg.padding = new RectOffset(srcVlg.padding.left, srcVlg.padding.right, srcVlg.padding.top, srcVlg.padding.bottom);
                    dstVlg.childAlignment = srcVlg.childAlignment;
                }
                catch (Exception ex) { Debug.LogException(ex); }
                dstVlg.childControlWidth = true;
                dstVlg.childControlHeight = false;
                dstVlg.childForceExpandWidth = true;
                dstVlg.childForceExpandHeight = false;
                ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                try { srcVlg.enabled = false; }
                catch (Exception ex) { Debug.LogException(ex); }

                // Reparent existing rows (order preserved) into the content object.
                List<Transform> movers = new List<Transform>();
                foreach (Transform ch in sectionRoot.transform)
                {
                    if (ch == null || ch.gameObject == viewport) continue;
                    movers.Add(ch);
                }
                foreach (Transform m in movers)
                {
                    try { m.SetParent(content.transform, false); }
                    catch (Exception ex) { Debug.LogException(ex); }
                }

                // Slim scrollbar docked on the right edge.
                GameObject trackObj = CreateRectChild(sectionRoot, "Scrollbar", Vector2.zero, new Vector2(14, 0));
                RectTransform sbRt = trackObj.GetComponent<RectTransform>();
                if (sbRt != null)
                {
                    sbRt.anchorMin = new Vector2(1f, 0f);
                    sbRt.anchorMax = new Vector2(1f, 1f);
                    sbRt.pivot = new Vector2(1f, 0.5f);
                    sbRt.offsetMin = new Vector2(-16f, 4f);
                    sbRt.offsetMax = new Vector2(-2f, -4f);
                }
                Image trackImg = trackObj.AddComponent<Image>();
                trackImg.sprite = GetPillSprite();
                trackImg.type = Image.Type.Sliced;
                trackImg.color = BtnInactiveBg;
                GameObject slideArea = CreateRectChild(trackObj, "Sliding Area", Vector2.zero, Vector2.zero);
                SetStretch(slideArea);
                GameObject handleObj = CreateRectChild(slideArea, "Handle", Vector2.zero, new Vector2(12, 24));
                Image handleImg = handleObj.AddComponent<Image>();
                handleImg.sprite = GetPillSprite();
                handleImg.type = Image.Type.Sliced;
                handleImg.color = FrameBorderCyan;
                Scrollbar scrollbar = trackObj.AddComponent<Scrollbar>();
                scrollbar.direction = Scrollbar.Direction.BottomToTop;
                scrollbar.handleRect = handleObj.GetComponent<RectTransform>();
                scrollbar.targetGraphic = handleImg;

                // ScrollRect: vertical only, elastic off, sensitivity ~30 (unscaled-time driven).
                ScrollRect scroll = sectionRoot.AddComponent<ScrollRect>();
                scroll.content = cRt;
                scroll.viewport = vpRt;
                scroll.horizontal = false;
                scroll.vertical = true;
                scroll.movementType = ScrollRect.MovementType.Clamped;
                scroll.elasticity = 0f;
                scroll.inertia = false;
                scroll.scrollSensitivity = 30f;
                scroll.verticalScrollbar = scrollbar;
                scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
                scroll.verticalNormalizedPosition = 1f;
            }
            catch (Exception ex) { Debug.LogException(ex); }
        }
        #endregion

        #region TAB 1: GRAPHICS Content (Screen Top-Right)
        private static void BuildGraphicsOptions(GameObject sec, SettingsManager mgr)
        {
            if (mgr == null) { try { mgr = SettingsManager.Instance; } catch (Exception ex) { Debug.LogException(ex); } }
            CreateStepperRow(sec, "Display Mode", new string[] { "Fullscreen", "Windowed", "Borderless" },
                PlayerPrefs.GetInt(SettingsManager.OPT_DISPLAY_MODE, 0),
                (idx) => { PlayerPrefs.SetInt(SettingsManager.OPT_DISPLAY_MODE, idx); if (mgr == null) { try { mgr = SettingsManager.Instance; } catch (Exception ex) { Debug.LogException(ex); } } if (mgr != null) { try { mgr.ApplyGraphics(); } catch (Exception ex) { Debug.LogException(ex); } } },
                "DISPLAY MODE", "Select display mode between exclusive fullscreen, borderless, or windowed rendering.");

            List<string> resOptions = null;
            try { resOptions = mgr != null ? mgr.GetResolutionOptions() : null; }
            catch (Exception ex) { Debug.LogException(ex); }
            if (resOptions == null || resOptions.Count == 0)
            {
                resOptions = new List<string>();
                try { resOptions.Add($"{Screen.currentResolution.width} x {Screen.currentResolution.height}"); }
                catch (Exception ex) { Debug.LogException(ex); resOptions.Add("1920 x 1080"); }
            }
            int resIdx = 0;
            try { resIdx = mgr != null ? mgr.GetCurrentResolutionIndex() : 0; }
            catch (Exception ex) { Debug.LogException(ex); }
            if (resIdx < 0 || resIdx >= resOptions.Count) resIdx = 0;
            CreateStepperRow(sec, "Resolution", resOptions.ToArray(),
                resIdx,
                (idx) => { PlayerPrefs.SetInt(SettingsManager.OPT_RESOLUTION, idx); if (mgr == null) { try { mgr = SettingsManager.Instance; } catch (Exception ex) { Debug.LogException(ex); } } if (mgr != null) { try { mgr.ApplyGraphics(); } catch (Exception ex) { Debug.LogException(ex); } } },
                "RESOLUTION", "Configure screen rendering display dimensions and pixel fidelity.");

            CreateSliderRow(sec, "Render Scale", 50, 150,
                PlayerPrefs.GetInt(SettingsManager.OPT_RENDER_SCALE, 100), "%",
                (val) => { PlayerPrefs.SetInt(SettingsManager.OPT_RENDER_SCALE, Mathf.RoundToInt(val)); if (mgr == null) { try { mgr = SettingsManager.Instance; } catch (Exception ex) { Debug.LogException(ex); } } if (mgr != null) { try { mgr.ApplyGraphics(); } catch (Exception ex) { Debug.LogException(ex); } } },
                "RENDER SCALE", "Internal resolution scaling factor for balancing visual sharpness and performance.");

            CreateStepperRow(sec, "Graphics Preset", new string[] { "Low", "Medium", "High", "Ultra" },
                PlayerPrefs.GetInt(SettingsManager.OPT_GRAPHICS_PRESET, 2),
                (idx) => { PlayerPrefs.SetInt(SettingsManager.OPT_GRAPHICS_PRESET, idx); if (mgr == null) { try { mgr = SettingsManager.Instance; } catch (Exception ex) { Debug.LogException(ex); } } if (mgr != null) { try { mgr.ApplyGraphics(); } catch (Exception ex) { Debug.LogException(ex); } } },
                "GRAPHICS PRESET", "Adjusts multiple quality settings at once. Higher presets provide better visuals but require more performance.");

            CreateStepperRow(sec, "Texture Quality", new string[] { "Low", "Medium", "High" },
                PlayerPrefs.GetInt(SettingsManager.OPT_TEXTURE_QUALITY, 2),
                (idx) => { PlayerPrefs.SetInt(SettingsManager.OPT_TEXTURE_QUALITY, idx); if (mgr == null) { try { mgr = SettingsManager.Instance; } catch (Exception ex) { Debug.LogException(ex); } } if (mgr != null) { try { mgr.ApplyGraphics(); } catch (Exception ex) { Debug.LogException(ex); } } },
                "TEXTURE QUALITY", "Resolution limit on surface albedo, normal map, and specular cybernetic textures.");

            CreateStepperRow(sec, "Shadow Quality", new string[] { "Off", "Medium", "High" },
                PlayerPrefs.GetInt(SettingsManager.OPT_SHADOW_QUALITY, 2),
                (idx) => { PlayerPrefs.SetInt(SettingsManager.OPT_SHADOW_QUALITY, idx); if (mgr == null) { try { mgr = SettingsManager.Instance; } catch (Exception ex) { Debug.LogException(ex); } } if (mgr != null) { try { mgr.ApplyGraphics(); } catch (Exception ex) { Debug.LogException(ex); } } },
                "SHADOW QUALITY", "Directional and point light shadow map resolution and contact softening filtering.");

            CreateStepperRow(sec, "Anti-Aliasing", new string[] { "Off", "FXAA", "SMAA", "TAA" },
                PlayerPrefs.GetInt(SettingsManager.OPT_ANTI_ALIASING, 3),
                (idx) => { PlayerPrefs.SetInt(SettingsManager.OPT_ANTI_ALIASING, idx); if (mgr == null) { try { mgr = SettingsManager.Instance; } catch (Exception ex) { Debug.LogException(ex); } } if (mgr != null) { try { mgr.ApplyGraphics(); } catch (Exception ex) { Debug.LogException(ex); } } },
                "ANTI-ALIASING", "Edge smoothing algorithm to eliminate geometric stair-stepping and temporal shimmering.");

            CreateStepperRow(sec, "Post Processing", new string[] { "Low", "Medium", "High" },
                PlayerPrefs.GetInt(SettingsManager.OPT_POST_PROCESSING, 2),
                (idx) => { PlayerPrefs.SetInt(SettingsManager.OPT_POST_PROCESSING, idx); if (mgr == null) { try { mgr = SettingsManager.Instance; } catch (Exception ex) { Debug.LogException(ex); } } if (mgr != null) { try { mgr.ApplyGraphics(); } catch (Exception ex) { Debug.LogException(ex); } } },
                "POST PROCESSING", "Fidelity of volumetric fog, chromatic aberration, tone mapping, and lens dirt.");

            CreateStepperRow(sec, "Effects Quality", new string[] { "Low", "Medium", "High" },
                PlayerPrefs.GetInt(SettingsManager.OPT_EFFECTS_QUALITY, 2),
                (idx) => { PlayerPrefs.SetInt(SettingsManager.OPT_EFFECTS_QUALITY, idx); if (mgr == null) { try { mgr = SettingsManager.Instance; } catch (Exception ex) { Debug.LogException(ex); } } if (mgr != null) { try { mgr.ApplyGraphics(); } catch (Exception ex) { Debug.LogException(ex); } } },
                "EFFECTS QUALITY", "Particle density for sword plasma slashes, sparks, laser beams, and shockwaves.");

            CreateStepperRow(sec, "View Distance", new string[] { "Low", "Medium", "High" },
                PlayerPrefs.GetInt(SettingsManager.OPT_VIEW_DISTANCE, 2),
                (idx) => { PlayerPrefs.SetInt(SettingsManager.OPT_VIEW_DISTANCE, idx); if (mgr == null) { try { mgr = SettingsManager.Instance; } catch (Exception ex) { Debug.LogException(ex); } } if (mgr != null) { try { mgr.ApplyGraphics(); } catch (Exception ex) { Debug.LogException(ex); } } },
                "VIEW DISTANCE", "Draw distance horizon for city skyscrapers, traffic hovercars, and distant neon signs.");

            CreateStepperRow(sec, "V-Sync", new string[] { "Off", "On" },
                PlayerPrefs.GetInt(SettingsManager.OPT_VSYNC, 0),
                (idx) => { PlayerPrefs.SetInt(SettingsManager.OPT_VSYNC, idx); if (mgr == null) { try { mgr = SettingsManager.Instance; } catch (Exception ex) { Debug.LogException(ex); } } if (mgr != null) { try { mgr.ApplyGraphics(); } catch (Exception ex) { Debug.LogException(ex); } } },
                "V-SYNC", "Synchronize framerate with display monitor refresh rate to eliminate horizontal screen tearing.");

            CreateStepperRow(sec, "Motion Blur", new string[] { "Off", "On" },
                PlayerPrefs.GetInt(SettingsManager.OPT_MOTION_BLUR, 0),
                (idx) => { PlayerPrefs.SetInt(SettingsManager.OPT_MOTION_BLUR, idx); if (mgr == null) { try { mgr = SettingsManager.Instance; } catch (Exception ex) { Debug.LogException(ex); } } if (mgr != null) { try { mgr.ApplyGraphics(); } catch (Exception ex) { Debug.LogException(ex); } } },
                "MOTION BLUR", "Simulate camera shutter velocity blur during high-speed sword slashes and dodging.");

            CreateSliderRow(sec, "Field of View", 60, 110,
                PlayerPrefs.GetFloat(SettingsManager.OPT_FOV, 90f), "",
                (val) => { PlayerPrefs.SetFloat(SettingsManager.OPT_FOV, val); if (mgr == null) { try { mgr = SettingsManager.Instance; } catch (Exception ex) { Debug.LogException(ex); } } if (mgr != null) { try { mgr.ApplyGraphics(); } catch (Exception ex) { Debug.LogException(ex); } } },
                "FIELD OF VIEW", "Horizontal perspective camera angle of view.");
        }
        #endregion

        #region TAB 2: AUDIO Content (Screen Bottom-Middle)
        private static void BuildAudioOptions(GameObject sec, SettingsManager mgr)
        {
            if (mgr == null) { try { mgr = SettingsManager.Instance; } catch (Exception ex) { Debug.LogException(ex); } }
            CreateSliderRow(sec, "Master Volume", 0, 100,
                PlayerPrefs.GetFloat(SettingsManager.OPT_MASTER_VOL, 100f), "%",
                (val) => { PlayerPrefs.SetFloat(SettingsManager.OPT_MASTER_VOL, val); if (mgr == null) { try { mgr = SettingsManager.Instance; } catch (Exception ex) { Debug.LogException(ex); } } if (mgr != null) { try { mgr.ApplyAudio(); } catch (Exception ex) { Debug.LogException(ex); } } },
                "MASTER VOLUME", "Global acoustic output amplitude level.");

            CreateSliderRow(sec, "Music Volume", 0, 100,
                PlayerPrefs.GetFloat(SettingsManager.OPT_MUSIC_VOL, 80f), "%",
                (val) => { PlayerPrefs.SetFloat(SettingsManager.OPT_MUSIC_VOL, val); if (mgr == null) { try { mgr = SettingsManager.Instance; } catch (Exception ex) { Debug.LogException(ex); } } if (mgr != null) { try { mgr.ApplyAudio(); } catch (Exception ex) { Debug.LogException(ex); } } },
                "MUSIC VOLUME", "Electronic cyberpunk synthwave soundtrack and combat theme score volume.");

            CreateSliderRow(sec, "SFX Volume", 0, 100,
                PlayerPrefs.GetFloat(SettingsManager.OPT_SFX_VOL, 90f), "%",
                (val) => { PlayerPrefs.SetFloat(SettingsManager.OPT_SFX_VOL, val); if (mgr == null) { try { mgr = SettingsManager.Instance; } catch (Exception ex) { Debug.LogException(ex); } } if (mgr != null) { try { mgr.ApplyAudio(); } catch (Exception ex) { Debug.LogException(ex); } } },
                "SFX VOLUME", "Blade collisions, plasma slashes, drone hums, gunshots, and explosion acoustics.");

            CreateSliderRow(sec, "Dialogue Volume", 0, 100,
                PlayerPrefs.GetFloat(SettingsManager.OPT_DIALOGUE_VOL, 100f), "%",
                (val) => { PlayerPrefs.SetFloat(SettingsManager.OPT_DIALOGUE_VOL, val); if (mgr == null) { try { mgr = SettingsManager.Instance; } catch (Exception ex) { Debug.LogException(ex); } } if (mgr != null) { try { mgr.ApplyAudio(); } catch (Exception ex) { Debug.LogException(ex); } } },
                "DIALOGUE VOLUME", "Voice transmissions, character dialogue, and AI comms chatter.");

            CreateSliderRow(sec, "UI Volume", 0, 100,
                PlayerPrefs.GetFloat(SettingsManager.OPT_UI_VOL, 90f), "%",
                (val) => { PlayerPrefs.SetFloat(SettingsManager.OPT_UI_VOL, val); if (mgr == null) { try { mgr = SettingsManager.Instance; } catch (Exception ex) { Debug.LogException(ex); } } if (mgr != null) { try { mgr.ApplyAudio(); } catch (Exception ex) { Debug.LogException(ex); } } },
                "UI VOLUME", "HUD sound effects, menu navigation chimes, tactical click chirps, and alerts.");

            CreateStepperRow(sec, "Dynamic Range", new string[] { "Default", "Night Mode", "Studio" },
                PlayerPrefs.GetInt(SettingsManager.OPT_DYNAMIC_RANGE, 0),
                (idx) => { PlayerPrefs.SetInt(SettingsManager.OPT_DYNAMIC_RANGE, idx); },
                "DYNAMIC RANGE", "Acoustic compression balance between quiet ambiance and loud explosions.");

            CreateStepperRow(sec, "Audio Output Device", new string[] { "Headphones (Default)", "Stereo", "5.1 Surround" },
                PlayerPrefs.GetInt(SettingsManager.OPT_AUDIO_OUTPUT, 0),
                (idx) => { PlayerPrefs.SetInt(SettingsManager.OPT_AUDIO_OUTPUT, idx); },
                "AUDIO OUTPUT DEVICE", "Select audio hardware device output layout.");

            CreateStepperRow(sec, "3D Audio", new string[] { "Off", "On" },
                PlayerPrefs.GetInt(SettingsManager.OPT_SPATIAL_AUDIO, 1),
                (idx) => { PlayerPrefs.SetInt(SettingsManager.OPT_SPATIAL_AUDIO, idx); },
                "3D AUDIO", "Binaural spatial audio simulation for 360-degree acoustic positioning.");

            CreateStepperRow(sec, "Subtitles", new string[] { "Off", "On" },
                PlayerPrefs.GetInt(SettingsManager.OPT_SUBTITLES, 1),
                (idx) => { PlayerPrefs.SetInt(SettingsManager.OPT_SUBTITLES, idx); },
                "SUBTITLES", "Display transcript text for speech and critical tactical mission announcements.");
        }
        #endregion

        #region TAB 3: CONTROLS System (Left SubTabs & Right Hardware Schematics)
        private static void BuildControlsSystem(GameObject sec, GameObject rightSchematic, SettingsManager settingsMgr, InGamePauseManager pauseMgr)
        {
            if (settingsMgr == null)
            {
                try { settingsMgr = SettingsManager.Instance; }
                catch (Exception ex) { Debug.LogException(ex); }
            }

            // Remove any default layout on sec root so children can anchor freely
            var existingVlg = sec.GetComponent<VerticalLayoutGroup>();
            if (existingVlg != null) UnityEngine.Object.DestroyImmediate(existingVlg);

            // 1. Left Column: Fixed Top Sub-Tab Navigation Bar (height 40)
            GameObject subTabBar = CreateRectChild(sec, "SubTabBar", new Vector2(0, 265), new Vector2(850, 40));
            HorizontalLayoutGroup hlg = subTabBar.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 16f;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childAlignment = TextAnchor.MiddleLeft;

            Button btnKM = CreateTabButton(subTabBar, "SubTab_KM", "KEYBOARD & MOUSE", true);
            var leKM = btnKM.GetComponent<LayoutElement>();
            if (leKM != null) leKM.preferredWidth = 210f;

            Button btnCtrl = CreateTabButton(subTabBar, "SubTab_Ctrl", "CONTROLLER", false);
            var leCtrl = btnCtrl.GetComponent<LayoutElement>();
            if (leCtrl != null) leCtrl.preferredWidth = 170f;

            Button btnCust = CreateTabButton(subTabBar, "SubTab_Cust", "CUSTOMIZE", false);
            var leCust = btnCust.GetComponent<LayoutElement>();
            if (leCust != null) leCust.preferredWidth = 170f;

            try { LayoutRebuilder.ForceRebuildLayoutImmediate(subTabBar.GetComponent<RectTransform>()); }
            catch (Exception ex) { Debug.LogException(ex); }

            // 2. Left Column: Content Container (height 520)
            GameObject contentBox = CreateRectChild(sec, "SubTabContentBox", new Vector2(0, -25), new Vector2(850, 520));

            // Panel A: Keyboard & Mouse Keybindings
            GameObject panelKM = CreateRectChild(contentBox, "Panel_KM", Vector2.zero, new Vector2(850, 520));
            SetStretch(panelKM);
            BuildKeyboardMouseList(panelKM);

            // Panel B: Controller Gamepad Bindings
            GameObject panelCtrl = CreateRectChild(contentBox, "Panel_Ctrl", Vector2.zero, new Vector2(850, 520));
            SetStretch(panelCtrl);
            BuildControllerBindingsList(panelCtrl);
            panelCtrl.SetActive(false);

            // Panel C: Controls Customization Options
            GameObject panelCust = CreateRectChild(contentBox, "Panel_Cust", Vector2.zero, new Vector2(850, 520));
            SetStretch(panelCust);
            BuildControlsCustomizeOptions(panelCust, settingsMgr, pauseMgr);
            panelCust.SetActive(false);

            // 3. Right Column: Schematics Container
            Image bg = rightSchematic.GetComponent<Image>() ?? rightSchematic.AddComponent<Image>();
            bg.sprite = GetPillSprite();
            bg.type = Image.Type.Sliced;
            bg.color = CardBgDark;

            Outline outline = rightSchematic.GetComponent<Outline>() ?? rightSchematic.AddComponent<Outline>();
            outline.effectColor = FrameBorderDim;

            AddCornerBrackets(rightSchematic);

            // Sub-Schematic 1: Keyboard & Mouse Topology
            GameObject schematicKM = CreateRectChild(rightSchematic, "Schematic_KM", Vector2.zero, new Vector2(470, 590));
            SetStretch(schematicKM);
            BuildKeyboardMouseSchematic(schematicKM);

            // Sub-Schematic 2: Gamepad Topology
            GameObject schematicCtrl = CreateRectChild(rightSchematic, "Schematic_Ctrl", Vector2.zero, new Vector2(470, 590));
            SetStretch(schematicCtrl);
            BuildGamepadSchematic(schematicCtrl);
            schematicCtrl.SetActive(false);

            // Sub-Schematic 3: Calibration Telemetry
            GameObject schematicCust = CreateRectChild(rightSchematic, "Schematic_Cust", Vector2.zero, new Vector2(470, 590));
            SetStretch(schematicCust);
            BuildCalibrationSchematic(schematicCust);
            schematicCust.SetActive(false);

            // 4. Sub-Tab Switching Logic
            Action<int> switchSubTab = (idx) =>
            {
                try { if (pauseMgr != null) pauseMgr.PlayClickAudio(); } catch (Exception ex) { Debug.LogException(ex); }

                SetTabActiveStyle(btnKM, idx == 0);
                SetTabActiveStyle(btnCtrl, idx == 1);
                SetTabActiveStyle(btnCust, idx == 2);

                panelKM.SetActive(idx == 0);
                panelCtrl.SetActive(idx == 1);
                panelCust.SetActive(idx == 2);

                schematicKM.SetActive(idx == 0);
                schematicCtrl.SetActive(idx == 1);
                schematicCust.SetActive(idx == 2);
            };

            btnKM.onClick.AddListener(() => switchSubTab(0));
            btnCtrl.onClick.AddListener(() => switchSubTab(1));
            btnCust.onClick.AddListener(() => switchSubTab(2));
        }

        private static void BuildKeyboardMouseList(GameObject parent)
        {
            string[][] kmBindings = new string[][]
            {
                new string[] { "Move Forward", "W" },
                new string[] { "Move Backward", "S" },
                new string[] { "Move Left", "A" },
                new string[] { "Move Right", "D" },
                new string[] { "Sprint / Tactical Dash", "Left Shift" },
                new string[] { "Directional Dodge / Evade", "Space" },
                new string[] { "Jump / Mantle", "Space" },
                new string[] { "Light Attack Combo", "Left Mouse (LMB)" },
                new string[] { "Heavy Combo Finisher", "Right Mouse (RMB)" },
                new string[] { "Block / Perfect Parry", "F" },
                new string[] { "Gun-Kata Burst (3 Shots)", "Q" },
                new string[] { "Interact / Loot / Revive", "E" },
                new string[] { "Bullet Time (Slow-Motion)", "T / Left Alt" },
                new string[] { "Toggle Lock-On Target", "Tab / Middle Click" },
                new string[] { "In-Game Pause Menu", "Esc" }
            };

            BuildBindingRowsScrollable(parent, kmBindings);
        }

        private static void BuildControllerBindingsList(GameObject parent)
        {
            string[][] ctrlBindings = new string[][]
            {
                new string[] { "360° Movement Locomotion", "Left Analog Stick" },
                new string[] { "Camera Free Look / Aim", "Right Analog Stick" },
                new string[] { "Sprint / Tactical Dash", "Left Stick Click (LS / L3)" },
                new string[] { "Directional Dodge / Evade", "B Button / Circle" },
                new string[] { "Jump / Mantle", "A Button / Cross" },
                new string[] { "Light Attack Combo (Chain)", "X Button / Square" },
                new string[] { "Heavy Combo Finisher", "Y Button / Triangle" },
                new string[] { "Block / Perfect Parry", "Left Bumper (LB / L1)" },
                new string[] { "Gun-Kata Burst Ability", "Right Bumper (RB / R1)" },
                new string[] { "Bullet Time (Slow-Motion)", "Left Trigger (LT / L2)" },
                new string[] { "Lock-On Target Toggle", "Right Stick Click (RS / R3)" },
                new string[] { "Interact / Loot", "X Button / Square (Hold)" },
                new string[] { "In-Game Pause Menu", "Menu / Options Button" }
            };

            BuildBindingRowsScrollable(parent, ctrlBindings);
        }

        private static void BuildBindingRowsScrollable(GameObject parent, string[][] bindings)
        {
            GameObject scrollObj = CreateRectChild(parent, "BindingScroll", Vector2.zero, Vector2.zero);
            SetStretch(scrollObj);

            GameObject viewport = CreateRectChild(scrollObj, "Viewport", Vector2.zero, Vector2.zero);
            SetStretch(viewport);
            viewport.AddComponent<RectMask2D>();

            GameObject content = CreateRectChild(viewport, "Content", Vector2.zero, new Vector2(850, bindings.Length * 36f));
            RectTransform cRt = content.GetComponent<RectTransform>();
            cRt.anchorMin = new Vector2(0f, 1f);
            cRt.anchorMax = new Vector2(1f, 1f);
            cRt.pivot = new Vector2(0.5f, 1f);
            cRt.offsetMin = Vector2.zero;
            cRt.offsetMax = Vector2.zero;

            VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 3;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect sr = scrollObj.AddComponent<ScrollRect>();
            sr.content = cRt;
            sr.viewport = viewport.GetComponent<RectTransform>();
            sr.horizontal = false;
            sr.vertical = true;
            sr.scrollSensitivity = 25f;
            sr.movementType = ScrollRect.MovementType.Clamped;

            foreach (var b in bindings)
            {
                GameObject row = CreateRectChild(content, $"Row_{b[0]}", Vector2.zero, new Vector2(850, 33));
                LayoutElement le = row.AddComponent<LayoutElement>();
                le.minHeight = 33f;
                le.preferredHeight = 33f;
                le.flexibleHeight = 0f;

                Image rImg = row.AddComponent<Image>();
                rImg.color = RowBgDark;

                row.AddComponent<CyberUIInteractiveEffect>();

                TMP_Text actionText = CreateTMP(row, "Action", b[0], 14, TextAlignmentOptions.Left, new Vector2(-180, 0), new Vector2(440, 26), false);
                actionText.color = TextWhite;

                GameObject badge = CreateRectChild(row, "KeyBadge", new Vector2(250, 0), new Vector2(220, 24));
                Image badgeBg = badge.AddComponent<Image>();
                badgeBg.sprite = GetPillSprite();
                badgeBg.type = Image.Type.Sliced;
                badgeBg.color = BtnInactiveBg;

                Outline badgeOutline = badge.AddComponent<Outline>();
                badgeOutline.effectColor = FrameBorderDim;

                TMP_Text keyText = CreateTMP(badge, "Key", b[1], 12, TextAlignmentOptions.Center, Vector2.zero, new Vector2(210, 22), false);
                keyText.color = FrameBorderCyan;
                keyText.fontStyle = FontStyles.Bold;
            }
        }

        private static void BuildControlsCustomizeOptions(GameObject sec, SettingsManager mgr, InGamePauseManager pauseMgr)
        {
            if (mgr == null) { try { mgr = SettingsManager.Instance; } catch (Exception ex) { Debug.LogException(ex); } }

            GameObject scrollObj = CreateRectChild(sec, "CustScroll", Vector2.zero, Vector2.zero);
            SetStretch(scrollObj);

            GameObject viewport = CreateRectChild(scrollObj, "Viewport", Vector2.zero, Vector2.zero);
            SetStretch(viewport);
            viewport.AddComponent<RectMask2D>();

            GameObject content = CreateRectChild(viewport, "Content", Vector2.zero, new Vector2(850, 480));
            RectTransform cRt = content.GetComponent<RectTransform>();
            cRt.anchorMin = new Vector2(0f, 1f);
            cRt.anchorMax = new Vector2(1f, 1f);
            cRt.pivot = new Vector2(0.5f, 1f);
            cRt.offsetMin = Vector2.zero;
            cRt.offsetMax = Vector2.zero;

            VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 5;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(4, 4, 4, 4);

            ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect sr = scrollObj.AddComponent<ScrollRect>();
            sr.content = cRt;
            sr.viewport = viewport.GetComponent<RectTransform>();
            sr.horizontal = false;
            sr.vertical = true;
            sr.scrollSensitivity = 25f;
            sr.movementType = ScrollRect.MovementType.Clamped;

            CreateSliderRow(content, "Horizontal Sensitivity (X)", 0.2f, 4.0f,
                PlayerPrefs.GetFloat(SettingsManager.OPT_SENSITIVITY_X, 1.5f), "x",
                (val) => { PlayerPrefs.SetFloat(SettingsManager.OPT_SENSITIVITY_X, val); if (mgr != null) mgr.ApplyGameplay(); },
                "AIM SENSITIVITY X", "Camera horizontal yaw rotation speed.");

            CreateSliderRow(content, "Vertical Sensitivity (Y)", 0.2f, 4.0f,
                PlayerPrefs.GetFloat(SettingsManager.OPT_SENSITIVITY_Y, 1.5f), "x",
                (val) => { PlayerPrefs.SetFloat(SettingsManager.OPT_SENSITIVITY_Y, val); if (mgr != null) mgr.ApplyGameplay(); },
                "AIM SENSITIVITY Y", "Camera vertical pitch rotation speed.");

            CreateStepperRow(content, "Invert Pitch (Y)", new string[] { "Off", "On" },
                PlayerPrefs.GetInt(SettingsManager.OPT_INVERT_Y, 0),
                (idx) => { PlayerPrefs.SetInt(SettingsManager.OPT_INVERT_Y, idx); if (mgr != null) mgr.ApplyGameplay(); },
                "INVERT PITCH (Y)", "Invert vertical look axis direction.");

            CreateStepperRow(content, "Mouse Smoothing", new string[] { "Off", "On" },
                PlayerPrefs.GetInt("Opt_MouseSmoothing", 0),
                (idx) => { PlayerPrefs.SetInt("Opt_MouseSmoothing", idx); },
                "MOUSE SMOOTHING", "Bypass hardware mouse smoothing for raw 1:1 sensor input.");

            CreateSliderRow(content, "Vibration Force", 0f, 1f,
                PlayerPrefs.GetFloat("Opt_Vibration", 0.8f), "%",
                (val) => { PlayerPrefs.SetFloat("Opt_Vibration", val); },
                "HAPTIC VIBRATION", "Gamepad rumble vibration feedback intensity.");

            CreateSliderRow(content, "Stick Deadzone", 0.05f, 0.30f,
                PlayerPrefs.GetFloat("Opt_Deadzone", 0.12f), "",
                (val) => { PlayerPrefs.SetFloat("Opt_Deadzone", val); },
                "STICK DEADZONE", "Analog thumbstick inner resting threshold.");

            CreateStepperRow(content, "Input Buffer Window", new string[] { "Strict (60ms)", "Balanced (120ms)", "Forgiving (180ms)" },
                PlayerPrefs.GetInt("Opt_InputBuffer", 1),
                (idx) => { PlayerPrefs.SetInt("Opt_InputBuffer", idx); },
                "INPUT BUFFER WINDOW", "Combat animation cancellation buffer timing tolerance.");

            GameObject resetRow = CreateRectChild(content, "Row_ResetControls", Vector2.zero, new Vector2(850, 44));
            LayoutElement le = resetRow.AddComponent<LayoutElement>();
            le.minHeight = 44f;
            le.preferredHeight = 44f;
            le.flexibleHeight = 0f;

            Button btnReset = CreateActionButton(resetRow, "Btn_ResetBindings", "RESET CONTROLS TO FACTORY DEFAULTS", Vector2.zero, new Vector2(420, 38), false);
            btnReset.onClick.AddListener(() =>
            {
                if (pauseMgr != null) pauseMgr.PlayClickAudio();
                PlayerPrefs.SetFloat(SettingsManager.OPT_SENSITIVITY_X, 1.5f);
                PlayerPrefs.SetFloat(SettingsManager.OPT_SENSITIVITY_Y, 1.5f);
                PlayerPrefs.SetInt(SettingsManager.OPT_INVERT_Y, 0);
                PlayerPrefs.SetInt("Opt_MouseSmoothing", 0);
                PlayerPrefs.SetFloat("Opt_Vibration", 0.8f);
                PlayerPrefs.SetFloat("Opt_Deadzone", 0.12f);
                PlayerPrefs.SetInt("Opt_InputBuffer", 1);
                PlayerPrefs.Save();
                if (mgr != null) mgr.ApplyGameplay();
            });
        }

        private static void BuildKeyboardMouseSchematic(GameObject parent)
        {
            TMP_Text header = CreateTMP(parent, "Header", "HARDWARE TOPOLOGY", 18, TextAlignmentOptions.Center, new Vector2(0, 260), new Vector2(430, 28), true);
            header.color = FrameBorderCyan;
            header.fontStyle = FontStyles.Bold;

            TMP_Text subHeader = CreateTMP(parent, "SubHeader", "NEURAL HID // 1000 HZ POLLING", 11, TextAlignmentOptions.Center, new Vector2(0, 238), new Vector2(430, 20), false);
            subHeader.color = TextMutedCyan;
            subHeader.fontStyle = FontStyles.Bold;

            // 1. WASD Cluster
            GameObject wasdContainer = CreateRectChild(parent, "WASD_Cluster", new Vector2(-130, 65), new Vector2(170, 170));
            CreateKeycap(wasdContainer, "Key_W", "W", new Vector2(0, 34));
            CreateKeycap(wasdContainer, "Key_A", "A", new Vector2(-44, -16));
            CreateKeycap(wasdContainer, "Key_S", "S", new Vector2(0, -16));
            CreateKeycap(wasdContainer, "Key_D", "D", new Vector2(44, -16));

            TMP_Text wasdLabel = CreateTMP(wasdContainer, "Label", "COMBAT LOCOMOTION", 11, TextAlignmentOptions.Center, new Vector2(0, -62), new Vector2(160, 20), false);
            wasdLabel.color = TextMutedCyan;
            wasdLabel.fontStyle = FontStyles.Bold;

            // 2. AAA Cyberpunk Gaming Mouse Diagram
            GameObject mouseContainer = CreateRectChild(parent, "Mouse_Diagram", new Vector2(115, 55), new Vector2(120, 200));

            GameObject mouseGraphic = CreateRectChild(mouseContainer, "MouseGraphic", Vector2.zero, new Vector2(110, 185));
            Image mImg = mouseGraphic.AddComponent<Image>();
            mImg.sprite = GetCyberMouseSprite();
            mImg.preserveAspect = true;
            mImg.raycastTarget = false;

            // Callouts pointing cleanly to mouse buttons without overlapping WASD cluster
            CreateCalloutLine(mouseContainer, "Callout_LMB", new Vector2(-18, 50), new Vector2(-45, 65), "[LMB] ATTACK", 84f);
            CreateCalloutLine(mouseContainer, "Callout_RMB", new Vector2(18, 50), new Vector2(40, 65), "[RMB] FINISH", 80f);
            CreateCalloutLine(mouseContainer, "Callout_Wheel", new Vector2(6, 30), new Vector2(40, 25), "[WHEEL] WEAPON", 86f);
            CreateCalloutLine(mouseContainer, "Callout_M4", new Vector2(-22, -10), new Vector2(-45, -10), "[M4] PARRY", 78f);
            CreateCalloutLine(mouseContainer, "Callout_M5", new Vector2(-22, -35), new Vector2(-45, -35), "[M5] INTERACT", 84f);

            // Diagnostics Text
            string info =
                "HARDWARE BUS: LOW-LATENCY OPTICAL\n" +
                "POLLING RATE: 1000 HZ | SENSOR: 16,000 DPI\n" +
                "RAW INPUT: ENABLED | SMOOTHING: BYPASS";
            TMP_Text infoText = CreateTMP(parent, "DiagInfo", info, 11, TextAlignmentOptions.Center, new Vector2(0, -200), new Vector2(420, 60), false);
            infoText.color = TextMutedCyan;
        }

        private static void BuildGamepadSchematic(GameObject parent)
        {
            TMP_Text header = CreateTMP(parent, "Header", "GAMEPAD TOPOLOGY", 18, TextAlignmentOptions.Center, new Vector2(0, 260), new Vector2(430, 28), true);
            header.color = FrameBorderCyan;
            header.fontStyle = FontStyles.Bold;

            TMP_Text subHeader = CreateTMP(parent, "SubHeader", "WIRELESS HID // XINPUT & DUALSENSE", 11, TextAlignmentOptions.Center, new Vector2(0, 238), new Vector2(430, 20), false);
            subHeader.color = TextMutedCyan;
            subHeader.fontStyle = FontStyles.Bold;

            // AAA Cyberpunk Gamepad Graphic
            GameObject padContainer = CreateRectChild(parent, "Gamepad_Diagram", new Vector2(0, 45), new Vector2(280, 195));

            GameObject padGraphic = CreateRectChild(padContainer, "PadGraphic", Vector2.zero, new Vector2(270, 190));
            Image padImg = padGraphic.AddComponent<Image>();
            padImg.sprite = GetCyberGamepadSprite();
            padImg.preserveAspect = true;
            padImg.raycastTarget = false;

            // Gamepad Callout Lines
            CreateCalloutLine(padContainer, "Callout_LS", new Vector2(-75, 45), new Vector2(-135, 95), "[LS] MOVE", 78f);
            CreateCalloutLine(padContainer, "Callout_RS", new Vector2(45, -5), new Vector2(125, -50), "[RS] CAMERA", 82f);
            CreateCalloutLine(padContainer, "Callout_LT", new Vector2(-95, 100), new Vector2(-135, 135), "[LT] SLOW-MO", 86f);
            CreateCalloutLine(padContainer, "Callout_RT", new Vector2(95, 100), new Vector2(135, 135), "[RT] FINISHER", 86f);
            CreateCalloutLine(padContainer, "Callout_LB", new Vector2(-70, 80), new Vector2(-130, 55), "[LB] PARRY", 78f);
            CreateCalloutLine(padContainer, "Callout_RB", new Vector2(70, 80), new Vector2(130, 55), "[RB] GUN-KATA", 88f);
            CreateCalloutLine(padContainer, "Callout_X", new Vector2(60, 45), new Vector2(125, 20), "[X] ATTACK", 78f);
            CreateCalloutLine(padContainer, "Callout_B", new Vector2(95, 45), new Vector2(135, -10), "[B] DODGE", 78f);
            CreateCalloutLine(padContainer, "Callout_A", new Vector2(75, 20), new Vector2(125, -35), "[A] JUMP", 72f);

            string info =
                "WIRELESS LATENCY: 1.2 MS | HAPTIC: ACTIVE\n" +
                "ANALOG DEADZONE: CALIBRATED | PROFILE: DEFAULT\n" +
                "NEURAL SYNC: OPTIMAL";
            TMP_Text infoText = CreateTMP(parent, "DiagInfo", info, 11, TextAlignmentOptions.Center, new Vector2(0, -200), new Vector2(420, 60), false);
            infoText.color = TextMutedCyan;
        }

        private static void BuildCalibrationSchematic(GameObject parent)
        {
            TMP_Text header = CreateTMP(parent, "Header", "CALIBRATION MONITOR", 18, TextAlignmentOptions.Center, new Vector2(0, 260), new Vector2(430, 28), true);
            header.color = FrameBorderCyan;
            header.fontStyle = FontStyles.Bold;

            TMP_Text subHeader = CreateTMP(parent, "SubHeader", "REAL-TIME TELEMETRY & RESPONSE CURVE", 11, TextAlignmentOptions.Center, new Vector2(0, 238), new Vector2(430, 20), false);
            subHeader.color = TextMutedCyan;
            subHeader.fontStyle = FontStyles.Bold;

            // Calibration Response Curve Box
            GameObject graphBox = CreateRectChild(parent, "GraphBox", new Vector2(0, 50), new Vector2(340, 190));
            Image gbImg = graphBox.AddComponent<Image>();
            gbImg.sprite = GetPillSprite();
            gbImg.type = Image.Type.Sliced;
            gbImg.color = BtnInactiveBg;
            Outline gbOutline = graphBox.AddComponent<Outline>();
            gbOutline.effectColor = FrameBorderDim;

            // Horizontal & Vertical Grid Lines
            for (int i = -3; i <= 3; i++)
            {
                if (i == 0) continue;
                GameObject gl = CreateRectChild(graphBox, $"HGrid_{i}", new Vector2(0, i * 24), new Vector2(310, 1));
                Image gli = gl.AddComponent<Image>();
                gli.color = new Color(0.0f, 0.4f, 0.6f, 0.25f);
                gli.raycastTarget = false;
            }

            // Diagonal Linear Curve Line
            GameObject curveLine = CreateRectChild(graphBox, "CurveLine", Vector2.zero, new Vector2(280, 2.5f));
            Image cli = curveLine.AddComponent<Image>();
            cli.color = FrameBorderCyan;
            cli.raycastTarget = false;
            curveLine.transform.localRotation = Quaternion.Euler(0, 0, 31f);

            TMP_Text graphLabel = CreateTMP(graphBox, "Label", "SENSITIVITY CURVE: 1:1 LINEAR (RAW)", 11, TextAlignmentOptions.Center, new Vector2(0, -75), new Vector2(300, 20), false);
            graphLabel.color = FrameBorderCyan;
            graphLabel.fontStyle = FontStyles.Bold;

            string info =
                "INPUT LATENCY: 0.8 MS | JITTER: 0.02 MS\n" +
                "SMOOTH ACCELERATION: BYPASS (RAW)\n" +
                "NEURAL INPUT BUFFER: 120 MS | STABILITY: 99.98%\n" +
                "STATUS: FULLY CALIBRATED";
            TMP_Text infoText = CreateTMP(parent, "DiagInfo", info, 11, TextAlignmentOptions.Center, new Vector2(0, -195), new Vector2(420, 70), false);
            infoText.color = TextMutedCyan;
        }

        private static void CreateKeycap(GameObject parent, string name, string letter, Vector2 pos)
        {
            GameObject keyObj = CreateRectChild(parent, name, pos, new Vector2(38, 38));
            Image bg = keyObj.AddComponent<Image>();
            bg.sprite = GetPillSprite();
            bg.type = Image.Type.Sliced;
            bg.color = BtnInactiveBg;
            Outline outline = keyObj.AddComponent<Outline>();
            outline.effectColor = FrameBorderCyan;

            TMP_Text tmp = CreateTMP(keyObj, "Txt", letter, 15, TextAlignmentOptions.Center, Vector2.zero, new Vector2(36, 36), false);
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = TextWhite;
        }

        private static void CreateCalloutLine(GameObject parent, string name, Vector2 start, Vector2 end, string label, float badgeWidth = 84f)
        {
            // Anchor node dot at start
            GameObject dotObj = CreateRectChild(parent, $"{name}_Dot", start, new Vector2(5, 5));
            Image dotImg = dotObj.AddComponent<Image>();
            dotImg.sprite = GetReticleSprite();
            dotImg.color = FrameBorderCyan;
            dotImg.raycastTarget = false;

            // Connector line
            GameObject lineObj = CreateRectChild(parent, name, (start + end) * 0.5f, new Vector2(Vector2.Distance(start, end), 1.5f));
            Image lineImg = lineObj.AddComponent<Image>();
            lineImg.color = FrameBorderCyan;
            lineImg.raycastTarget = false;
            float angle = Mathf.Atan2(end.y - start.y, end.x - start.x) * Mathf.Rad2Deg;
            lineObj.transform.localRotation = Quaternion.Euler(0, 0, angle);

            // Callout badge with dark pill frame and cyan border
            float halfBadge = badgeWidth * 0.5f;
            Vector2 badgePos = end + (end.x > start.x ? new Vector2(halfBadge, 0) : new Vector2(-halfBadge, 0));
            GameObject labelObj = CreateRectChild(parent, $"{name}_Badge", badgePos, new Vector2(badgeWidth, 20));
            Image badgeBg = labelObj.AddComponent<Image>();
            badgeBg.sprite = GetPillSprite();
            badgeBg.type = Image.Type.Sliced;
            badgeBg.color = new Color(0.02f, 0.05f, 0.10f, 0.95f);
            badgeBg.raycastTarget = false;

            Outline badgeOutline = labelObj.AddComponent<Outline>();
            badgeOutline.effectColor = FrameBorderDim;

            TMP_Text tmp = CreateTMP(labelObj, "Txt", label, 10, TextAlignmentOptions.Center, Vector2.zero, new Vector2(badgeWidth - 4, 18), false);
            tmp.color = FrameBorderCyan;
            tmp.fontStyle = FontStyles.Bold;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
        }
        #endregion

        #region TAB 4: GAMEPLAY & TAB 5: INTERFACE Content
        private static void BuildGameplayOptions(GameObject sec, SettingsManager mgr)
        {
            if (mgr == null) { try { mgr = SettingsManager.Instance; } catch (Exception ex) { Debug.LogException(ex); } }
            CreateStepperRow(sec, "Combat Difficulty", new string[] { "Story", "Normal", "Hard", "Insane" },
                PlayerPrefs.GetInt(SettingsManager.OPT_DIFFICULTY, 1),
                (idx) => { PlayerPrefs.SetInt(SettingsManager.OPT_DIFFICULTY, idx); if (mgr == null) { try { mgr = SettingsManager.Instance; } catch (Exception ex) { Debug.LogException(ex); } } if (mgr != null) { try { mgr.ApplyGameplay(); } catch (Exception ex) { Debug.LogException(ex); } } },
                "COMBAT DIFFICULTY", "Hostile AI aggression, damage thresholds, and reaction speeds.");

            CreateStepperRow(sec, "Camera Shake", new string[] { "Off", "On" },
                PlayerPrefs.GetInt(SettingsManager.OPT_CAMERA_SHAKE, 1),
                (idx) => { PlayerPrefs.SetInt(SettingsManager.OPT_CAMERA_SHAKE, idx); if (mgr == null) { try { mgr = SettingsManager.Instance; } catch (Exception ex) { Debug.LogException(ex); } } if (mgr != null) { try { mgr.ApplyGameplay(); } catch (Exception ex) { Debug.LogException(ex); } } },
                "CAMERA SHAKE", "Trauma-based screen shake on sword collisions and explosions.");

            CreateSliderRow(sec, "Shake Multiplier", 0f, 2f,
                PlayerPrefs.GetFloat(SettingsManager.OPT_SHAKE_INTENSITY, 1.0f), "x",
                (val) => { PlayerPrefs.SetFloat(SettingsManager.OPT_SHAKE_INTENSITY, val); if (mgr == null) { try { mgr = SettingsManager.Instance; } catch (Exception ex) { Debug.LogException(ex); } } if (mgr != null) { try { mgr.ApplyGameplay(); } catch (Exception ex) { Debug.LogException(ex); } } },
                "SHAKE MULTIPLIER", "Global screen shake intensity factor.");

            CreateStepperRow(sec, "Hit-Stop Feedback", new string[] { "Off", "On" },
                PlayerPrefs.GetInt(SettingsManager.OPT_HIT_STOP, 1),
                (idx) => { PlayerPrefs.SetInt(SettingsManager.OPT_HIT_STOP, idx); },
                "HIT-STOP FEEDBACK", "Micro-freeze frames on heavy sword impacts for tactile weight.");

            CreateStepperRow(sec, "Bullet Time (Slow-Mo)", new string[] { "Disabled", "Enabled" },
                PlayerPrefs.GetInt(SettingsManager.OPT_BULLET_TIME, 1),
                (idx) => { PlayerPrefs.SetInt(SettingsManager.OPT_BULLET_TIME, idx); if (mgr == null) { try { mgr = SettingsManager.Instance; } catch (Exception ex) { Debug.LogException(ex); } } if (mgr != null) { try { mgr.ApplyGameplay(); } catch (Exception ex) { Debug.LogException(ex); } } },
                "BULLET TIME", "Slow-motion temporal dilation when triggering neural stamina.");

            CreateSliderRow(sec, "Horizontal Sensitivity", 0.2f, 4.0f,
                PlayerPrefs.GetFloat(SettingsManager.OPT_SENSITIVITY_X, 1.5f), "x",
                (val) => { PlayerPrefs.SetFloat(SettingsManager.OPT_SENSITIVITY_X, val); if (mgr == null) { try { mgr = SettingsManager.Instance; } catch (Exception ex) { Debug.LogException(ex); } } if (mgr != null) { try { mgr.ApplyGameplay(); } catch (Exception ex) { Debug.LogException(ex); } } },
                "AIM SENSITIVITY X", "Camera horizontal yaw rotation speed.");

            CreateSliderRow(sec, "Vertical Sensitivity", 0.2f, 4.0f,
                PlayerPrefs.GetFloat(SettingsManager.OPT_SENSITIVITY_Y, 1.5f), "x",
                (val) => { PlayerPrefs.SetFloat(SettingsManager.OPT_SENSITIVITY_Y, val); if (mgr == null) { try { mgr = SettingsManager.Instance; } catch (Exception ex) { Debug.LogException(ex); } } if (mgr != null) { try { mgr.ApplyGameplay(); } catch (Exception ex) { Debug.LogException(ex); } } },
                "AIM SENSITIVITY Y", "Camera vertical pitch rotation speed.");

            CreateStepperRow(sec, "Invert Pitch (Y)", new string[] { "Off", "On" },
                PlayerPrefs.GetInt(SettingsManager.OPT_INVERT_Y, 0),
                (idx) => { PlayerPrefs.SetInt(SettingsManager.OPT_INVERT_Y, idx); if (mgr == null) { try { mgr = SettingsManager.Instance; } catch (Exception ex) { Debug.LogException(ex); } } if (mgr != null) { try { mgr.ApplyGameplay(); } catch (Exception ex) { Debug.LogException(ex); } } },
                "INVERT PITCH (Y)", "Invert vertical look axis direction.");
        }

        private static void BuildInterfaceOptions(GameObject sec, SettingsManager mgr)
        {
            if (mgr == null) { try { mgr = SettingsManager.Instance; } catch (Exception ex) { Debug.LogException(ex); } }
            CreateStepperRow(sec, "Objective Markers", new string[] { "Off", "On" },
                PlayerPrefs.GetInt(SettingsManager.OPT_OBJECTIVE_MARKERS, 1),
                (idx) => { PlayerPrefs.SetInt(SettingsManager.OPT_OBJECTIVE_MARKERS, idx); },
                "OBJECTIVE MARKERS", "Show floating cybernetic waypoint reticles on active missions.");

            CreateStepperRow(sec, "Colorblind Profile", new string[] { "None", "Protanopia", "Deuteranopia", "Tritanopia" },
                PlayerPrefs.GetInt(SettingsManager.OPT_COLORBLIND, 0),
                (idx) => { PlayerPrefs.SetInt(SettingsManager.OPT_COLORBLIND, idx); },
                "COLORBLIND FILTER", "Alternative HUD palette mapping for visual accessibility.");

            CreateStepperRow(sec, "Reduce Bright Flashes", new string[] { "Off", "On" },
                PlayerPrefs.GetInt(SettingsManager.OPT_REDUCE_FLASHES, 0),
                (idx) => { PlayerPrefs.SetInt(SettingsManager.OPT_REDUCE_FLASHES, idx); },
                "REDUCE BRIGHT FLASHES", "Attenuate full-screen high-frequency flash VFX during explosions.");

            CreateStepperRow(sec, "Large Interface Text", new string[] { "Off", "On" },
                PlayerPrefs.GetInt(SettingsManager.OPT_LARGE_TEXT, 0),
                (idx) => { PlayerPrefs.SetInt(SettingsManager.OPT_LARGE_TEXT, idx); if (mgr == null) { try { mgr = SettingsManager.Instance; } catch (Exception ex) { Debug.LogException(ex); } } if (mgr != null) { try { mgr.ApplyAccessibility(); } catch (Exception ex) { Debug.LogException(ex); } } },
                "LARGE INTERFACE TEXT", "Increase HUD typography scale for readability.");
        }
        #endregion

        #region Right Preview Card (Matching Top-Right of Reference Screenshot)
        private static void BuildPreviewCard(GameObject parent, SettingsManager mgr)
        {
            Image bg = parent.AddComponent<Image>();
            bg.sprite = GetPillSprite();
            bg.type = Image.Type.Sliced;
            bg.color = CardBgDark;

            Outline outline = parent.AddComponent<Outline>();
            outline.effectColor = FrameBorderDim;

            // Preview Thumbnail Image with Glowing Border
            GameObject imgObj = CreateRectChild(parent, "PreviewImage", new Vector2(0, 140), new Vector2(420, 220));
            Image img = imgObj.AddComponent<Image>();
            img.color = Color.white;
            Sprite citySprite = GetPreviewCitySprite();
            if (citySprite != null)
            {
                img.sprite = citySprite;
            }
            else
            {
                img.color = RowBgDark;
            }
            Outline imgOutline = imgObj.AddComponent<Outline>();
            imgOutline.effectColor = FrameBorderCyan;
            imgOutline.effectDistance = new Vector2(1.5f, -1.5f);

            // Title
            TMP_Text title = CreateTMP(parent, "PreviewTitle", "GRAPHICS PRESET", 19, TextAlignmentOptions.Left, new Vector2(-15, -10), new Vector2(390, 32), true);
            title.color = FrameBorderCyan;
            title.fontStyle = FontStyles.Bold;

            // Description
            TMP_Text desc = CreateTMP(parent, "PreviewDesc",
                "Adjusts multiple quality settings at once. Higher presets provide better visuals but require more performance.",
                14, TextAlignmentOptions.Left, new Vector2(-15, -95), new Vector2(390, 130), false);
            desc.color = TextWhite;

            if (mgr != null)
            {
                mgr.previewTitleText = title;
                mgr.previewDescText = desc;
                mgr.previewCardImage = img;
            }
        }
        #endregion

        #region Interactive Row Helpers (Stepper & Slider)
        private static void CreateStepperRow(GameObject parent, string label, string[] options, int initialIndex, Action<int> onChanged, string previewTitle, string previewDesc)
        {
            GameObject row = CreateRectChild(parent, $"Row_{label}", Vector2.zero, new Vector2(850, 40));
            LayoutElement rowLayout = row.AddComponent<LayoutElement>();
            rowLayout.minHeight = 40f;
            rowLayout.preferredHeight = 40f;
            rowLayout.flexibleHeight = 0f;
            Image rowBg = row.AddComponent<Image>();
            rowBg.sprite = GetPillSprite();
            rowBg.type = Image.Type.Sliced;
            rowBg.color = RowBgDark;

            TMP_Text lbl = CreateTMP(row, "Label", label, 15, TextAlignmentOptions.Left, new Vector2(-190, 0), new Vector2(400, 30), false);
            lbl.color = TextWhite;

            int curIndex = Mathf.Clamp(initialIndex, 0, options.Length - 1);

            // Stepper Container on the right (width 320, centered at x 240)
            GameObject stepperBox = CreateRectChild(row, "StepperBox", new Vector2(240, 0), new Vector2(320, 34));

            Button btnPrev = CreateArrowButton(stepperBox, "Btn_Prev", "<", new Vector2(-140, 0));
            TMP_Text valTxt = CreateTMP(stepperBox, "ValueText", options[curIndex], 14, TextAlignmentOptions.Center, Vector2.zero, new Vector2(240, 28), false);
            valTxt.color = FrameBorderCyan;
            valTxt.fontStyle = FontStyles.Bold;
            valTxt.raycastTarget = false;
            Button btnNext = CreateArrowButton(stepperBox, "Btn_Next", ">", new Vector2(140, 0));

            // Defensive re-attach: both arrows must stay wired + interactable for the
            // lifetime of the row, and a throwing callback must never break the UI.
            try { if (btnPrev != null) btnPrev.interactable = true; }
            catch (Exception ex) { Debug.LogException(ex); }
            try { if (btnNext != null) btnNext.interactable = true; }
            catch (Exception ex) { Debug.LogException(ex); }

            btnPrev.onClick.AddListener(() =>
            {
                try
                {
                    if (valTxt == null || options == null || options.Length == 0) return;
                    curIndex--;
                    if (curIndex < 0) curIndex = options.Length - 1;
                    valTxt.text = options[curIndex];
                    try { onChanged?.Invoke(curIndex); }
                    catch (Exception ex) { Debug.LogException(ex); }
                    try { SettingsManager.Instance?.SetPreview(previewTitle, previewDesc); }
                    catch (Exception ex) { Debug.LogException(ex); }
                }
                catch (Exception ex) { Debug.LogException(ex); }
            });

            btnNext.onClick.AddListener(() =>
            {
                try
                {
                    if (valTxt == null || options == null || options.Length == 0) return;
                    curIndex++;
                    if (curIndex >= options.Length) curIndex = 0;
                    valTxt.text = options[curIndex];
                    try { onChanged?.Invoke(curIndex); }
                    catch (Exception ex) { Debug.LogException(ex); }
                    try { SettingsManager.Instance?.SetPreview(previewTitle, previewDesc); }
                    catch (Exception ex) { Debug.LogException(ex); }
                }
                catch (Exception ex) { Debug.LogException(ex); }
            });

            AddHoverPreview(row, previewTitle, previewDesc);
            row.AddComponent<CyberUIInteractiveEffect>();
        }

        private static void CreateSliderRow(GameObject parent, string label, float min, float max, float initialVal, string unit, Action<float> onChanged, string previewTitle, string previewDesc)
        {
            GameObject row = CreateRectChild(parent, $"Row_{label}", Vector2.zero, new Vector2(850, 40));
            LayoutElement sliderRowLayout = row.AddComponent<LayoutElement>();
            sliderRowLayout.minHeight = 40f;
            sliderRowLayout.preferredHeight = 40f;
            sliderRowLayout.flexibleHeight = 0f;
            Image rowBg = row.AddComponent<Image>();
            rowBg.sprite = GetPillSprite();
            rowBg.type = Image.Type.Sliced;
            rowBg.color = RowBgDark;

            TMP_Text lbl = CreateTMP(row, "Label", label, 15, TextAlignmentOptions.Left, new Vector2(-190, 0), new Vector2(400, 30), false);
            lbl.color = TextWhite;

            // Slider Container on right (width 320, centered at x 240)
            GameObject sliderObj = CreateRectChild(row, "Slider", new Vector2(205, 0), new Vector2(240, 14));
            Image sBg = sliderObj.AddComponent<Image>();
            sBg.sprite = GetPillSprite();
            sBg.type = Image.Type.Sliced;
            sBg.color = BtnInactiveBg;

            GameObject fillArea = CreateRectChild(sliderObj, "Fill Area", Vector2.zero, Vector2.zero);
            SetStretch(fillArea);

            GameObject fill = CreateRectChild(fillArea, "Fill", Vector2.zero, Vector2.zero);
            SetStretch(fill);
            Image fillImg = fill.AddComponent<Image>();
            fillImg.sprite = GetPillSprite();
            fillImg.type = Image.Type.Sliced;
            fillImg.color = FrameBorderCyan;

            // Handle
            GameObject handleArea = CreateRectChild(sliderObj, "Handle Slide Area", Vector2.zero, Vector2.zero);
            SetStretch(handleArea);

            GameObject handle = CreateRectChild(handleArea, "Handle", Vector2.zero, new Vector2(16, 16));
            Image handleImg = handle.AddComponent<Image>();
            handleImg.sprite = GetReticleSprite();
            handleImg.color = Color.white;

            Slider slider = sliderObj.AddComponent<Slider>();
            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.handleRect = handle.GetComponent<RectTransform>();
            slider.minValue = min;
            slider.maxValue = max;
            try { slider.value = initialVal; }
            catch (Exception ex) { Debug.LogException(ex); }
            try { slider.interactable = true; }
            catch (Exception ex) { Debug.LogException(ex); }

            TMP_Text valTxt = CreateTMP(row, "ValText", $"{Mathf.RoundToInt(initialVal)}{unit}", 14, TextAlignmentOptions.Right, new Vector2(365, 0), new Vector2(65, 30), false);
            valTxt.color = FrameBorderCyan;
            valTxt.fontStyle = FontStyles.Bold;
            valTxt.raycastTarget = false;

            slider.onValueChanged.AddListener((v) =>
            {
                try
                {
                    if (valTxt != null)
                    {
                        try { valTxt.text = unit == "%" ? $"{Mathf.RoundToInt(v)}%" : $"{Mathf.RoundToInt(v)}{unit}"; }
                        catch (Exception ex) { Debug.LogException(ex); }
                    }
                    try { onChanged?.Invoke(v); }
                    catch (Exception ex) { Debug.LogException(ex); }
                    try { SettingsManager.Instance?.SetPreview(previewTitle, previewDesc); }
                    catch (Exception ex) { Debug.LogException(ex); }
                }
                catch (Exception ex) { Debug.LogException(ex); }
            });

            AddHoverPreview(row, previewTitle, previewDesc);
            row.AddComponent<CyberUIInteractiveEffect>();
        }

        private static Button CreateArrowButton(GameObject parent, string name, string text, Vector2 pos)
        {
            GameObject btnObj = CreateRectChild(parent, name, pos, new Vector2(26, 26));
            Image img = btnObj.AddComponent<Image>();
            img.sprite = GetPillSprite();
            img.type = Image.Type.Sliced;
            img.color = BtnInactiveBg;

            Outline outline = btnObj.AddComponent<Outline>();
            outline.effectColor = FrameBorderDim;

            Button btn = btnObj.AddComponent<Button>();
            TMP_Text t = CreateTMP(btnObj, "Txt", text, 14, TextAlignmentOptions.Center, Vector2.zero, new Vector2(24, 24), false);
            t.color = FrameBorderCyan;
            t.fontStyle = FontStyles.Bold;

            btnObj.AddComponent<CyberUIInteractiveEffect>();
            return btn;
        }

        private static Button CreateActionButton(GameObject parent, string name, string label, Vector2 pos, Vector2 size, bool isHighlight)
        {
            GameObject btnObj = CreateRectChild(parent, name, pos, size);
            Image img = btnObj.AddComponent<Image>();
            img.sprite = GetPillSprite();
            img.type = Image.Type.Sliced;
            img.color = isHighlight ? BtnActiveBg : BtnInactiveBg;

            Outline outline = btnObj.AddComponent<Outline>();
            outline.effectColor = isHighlight ? FrameBorderCyan : new Color(0f, 0.85f, 1f, 0.35f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            Button btn = btnObj.AddComponent<Button>();
            TMP_Text t = CreateTMP(btnObj, "Label", label, 15, TextAlignmentOptions.Center, Vector2.zero, size, false);
            t.color = isHighlight ? TextWhite : TextMutedCyan;
            t.fontStyle = FontStyles.Bold;

            var fx = btnObj.AddComponent<CyberUIInteractiveEffect>();
            fx.normalColor = isHighlight ? BtnActiveBg : BtnInactiveBg;
            fx.hoverColor = isHighlight ? new Color(0.12f, 0.48f, 0.98f, 1f) : new Color(0.08f, 0.22f, 0.45f, 0.9f);

            return btn;
        }

        private static void AddHoverPreview(GameObject target, string title, string desc)
        {
            EventTrigger trigger = target.AddComponent<EventTrigger>();
            EventTrigger.Entry entry = new EventTrigger.Entry();
            entry.eventID = EventTriggerType.PointerEnter;
            entry.callback.AddListener((data) =>
            {
                SettingsManager.Instance?.SetPreview(title, desc);
            });
            trigger.triggers.Add(entry);
        }
        #endregion

        #region Confirmation Modal Builder
        private static void BuildConfirmationModal(GameObject root, InGamePauseManager pauseMgr)
        {
            Image bg = root.AddComponent<Image>();
            bg.color = new Color(0.01f, 0.02f, 0.05f, 0.92f);

            GameObject box = CreateRectChild(root, "ConfirmBox", Vector2.zero, new Vector2(640, 320));
            Image boxBg = box.AddComponent<Image>();
            boxBg.sprite = GetPillSprite();
            boxBg.type = Image.Type.Sliced;
            boxBg.color = WindowDark;

            Outline boxOutline = box.AddComponent<Outline>();
            boxOutline.effectColor = FrameBorderCyan;
            boxOutline.effectDistance = new Vector2(2f, -2f);

            AddCornerBrackets(box);

            TMP_Text title = CreateTMP(box, "Text_Title", "CONFIRM PROTOCOL", 22, TextAlignmentOptions.Center, new Vector2(0, 100), new Vector2(580, 36), true);
            title.color = FrameBorderCyan;
            title.fontStyle = FontStyles.Bold;
            pauseMgr.confirmTitleText = title;

            TMP_Text desc = CreateTMP(box, "Text_Desc", "ARE YOU SURE YOU WISH TO PROCEED WITH THIS ACTION?", 16, TextAlignmentOptions.Center, new Vector2(0, 15), new Vector2(560, 90), false);
            desc.color = TextWhite;
            pauseMgr.confirmDescText = desc;

            Button btnYes = CreateActionButton(box, "Btn_ConfirmYes", "CONFIRM", new Vector2(-130, -95), new Vector2(200, 46), true);
            Button btnNo = CreateActionButton(box, "Btn_ConfirmNo", "CANCEL", new Vector2(130, -95), new Vector2(200, 46), false);

            pauseMgr.confirmYesButton = btnYes;
            pauseMgr.confirmNoButton = btnNo;
            pauseMgr.confirmPopupRoot = root;
        }
        #endregion

        #region UI Construction Helpers
        private static GameObject CreateRectChild(GameObject parent, string name, Vector2 pos, Vector2 size)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent.transform, false);
            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return obj;
        }

        private static void SetStretch(GameObject obj)
        {
            RectTransform rt = obj.GetComponent<RectTransform>();
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static TMP_Text CreateTMP(GameObject parent, string name, string text, float fontSize, TextAlignmentOptions align, Vector2 pos, Vector2 size, bool isHeader)
        {
            GameObject obj = CreateRectChild(parent, name, pos, size);
            TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset font = isHeader ? GetHeaderFont() : GetBodyFont();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = align;
            tmp.textWrappingMode = size.y > 32f ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            return tmp;
        }
        #endregion
    }
}
