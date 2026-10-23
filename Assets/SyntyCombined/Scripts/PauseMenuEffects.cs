using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace SyntyCombined
{
    /// <summary>
    /// Static-friendly procedural tween helper for the pause experience.
    /// Every routine is driven by Time.unscaledDeltaTime (works at timeScale = 0),
    /// guards destroyed / missing objects each frame, and never throws outward:
    /// callers additionally wrap each call in try/catch so effects can never
    /// hard-break the pause menu. No reflection, no external packages.
    /// </summary>
    public static class PauseMenuEffects
    {
        #region Fade
        public static Coroutine PlayFadeIn(MonoBehaviour host, CanvasGroup group, float duration = 0.18f, float targetAlpha = 1f)
        {
            try
            {
                if (host == null || group == null) return null;
                float from = 1f;
                try { from = group.alpha; } catch (Exception ex) { Debug.LogException(ex); }
                return host.StartCoroutine(FadeRoutine(group, from, targetAlpha, duration, null));
            }
            catch (Exception ex) { Debug.LogException(ex); return null; }
        }

        public static Coroutine PlayFadeOut(MonoBehaviour host, CanvasGroup group, float duration = 0.1f, Action onDone = null)
        {
            try
            {
                if (host == null)
                {
                    try { onDone?.Invoke(); } catch (Exception ex) { Debug.LogException(ex); }
                    return null;
                }
                if (group == null)
                {
                    try { onDone?.Invoke(); } catch (Exception ex) { Debug.LogException(ex); }
                    return null;
                }
                float from = 0f;
                try { from = group.alpha; } catch (Exception ex) { Debug.LogException(ex); }
                return host.StartCoroutine(FadeRoutine(group, from, 0f, duration, onDone));
            }
            catch (Exception ex) { Debug.LogException(ex); return null; }
        }

        private static IEnumerator FadeRoutine(CanvasGroup group, float from, float to, float duration, Action onDone)
        {
            // NOTE: no outer try/catch here — C# forbids yield inside try-with-catch (CS1626).
            {
                if (group == null)
                {
                    try { onDone?.Invoke(); } catch (Exception ex) { Debug.LogException(ex); }
                    yield break;
                }
                if (duration <= 0.01f) duration = 0.1f;
                float elapsed = 0f;
                try { group.alpha = from; } catch (Exception ex) { Debug.LogException(ex); }
                while (elapsed < duration)
                {
                    float dt = 0.016f;
                    try { dt = Time.unscaledDeltaTime; } catch (Exception ex) { Debug.LogException(ex); }
                    elapsed += dt;
                    float t = Mathf.Clamp01(elapsed / duration);
                    bool alive = false;
                    try { alive = group != null; } catch (Exception ex) { Debug.LogException(ex); }
                    if (!alive) yield break;
                    bool ok = true;
                    try { group.alpha = Mathf.Lerp(from, to, t); }
                    catch (Exception ex) { Debug.LogException(ex); ok = false; }
                    if (!ok) yield break;
                    yield return null;
                }
                try { if (group != null) group.alpha = to; }
                catch (Exception ex) { Debug.LogException(ex); }
                try { onDone?.Invoke(); } catch (Exception ex) { Debug.LogException(ex); }
            }
        }
        #endregion

        #region Slide Cascade
        public static Coroutine PlaySlideCascade(MonoBehaviour host, RectTransform[] items, Vector2 direction, float stagger = 0.045f, float duration = 0.22f)
        {
            try
            {
                if (host == null || items == null || items.Length == 0) return null;
                return host.StartCoroutine(SlideCascadeRoutine(items, direction, stagger, duration));
            }
            catch (Exception ex) { Debug.LogException(ex); return null; }
        }

        private static IEnumerator SlideCascadeRoutine(RectTransform[] items, Vector2 direction, float stagger, float duration)
        {
            Vector2[] basePos = null;
            if (items == null) yield break;
            try
            {
                basePos = new Vector2[items.Length];
                for (int i = 0; i < items.Length; i++)
                {
                    try
                    {
                        if (items[i] != null)
                        {
                            Vector2 pos = items[i].anchoredPosition;
                            if (Mathf.Abs(direction.x) > 0.01f && Mathf.Abs(pos.x - direction.x) < 2f)
                                pos.x = 0f;
                            basePos[i] = pos;
                        }
                        else basePos[i] = Vector2.zero;
                    }
                    catch (Exception ex) { Debug.LogException(ex); basePos[i] = Vector2.zero; }
                }
            }
            catch (Exception ex) { Debug.LogException(ex); }
            if (basePos == null) yield break;

            if (duration <= 0.01f) duration = 0.18f;
            if (stagger < 0f) stagger = 0f;
            float total = duration + stagger * Mathf.Max(0, items.Length - 1);

            for (int i = 0; i < items.Length; i++)
            {
                try { if (items[i] != null) items[i].anchoredPosition = basePos[i] + direction; }
                catch (Exception ex) { Debug.LogException(ex); }
            }

            float elapsed = 0f;
            while (elapsed <= total + 0.05f)
            {
                float dt = 0.016f;
                try { dt = Time.unscaledDeltaTime; } catch (Exception ex) { Debug.LogException(ex); }
                elapsed += dt;
                bool anyAlive = false;
                for (int i = 0; i < items.Length; i++)
                {
                    RectTransform rt = null;
                    try { rt = items[i]; } catch (Exception ex) { Debug.LogException(ex); continue; }
                    if (rt == null) continue;
                    anyAlive = true;
                    try
                    {
                        float lt = (elapsed - stagger * i) / duration;
                        lt = Mathf.Clamp01(lt);
                        float e = 1f - Mathf.Pow(1f - lt, 3f);
                        rt.anchoredPosition = basePos[i] + direction * (1f - e);
                    }
                    catch (Exception ex) { Debug.LogException(ex); }
                }
                if (!anyAlive) yield break;
                yield return null;
            }
            for (int i = 0; i < items.Length; i++)
            {
                try { if (items[i] != null) items[i].anchoredPosition = basePos[i]; }
                catch (Exception ex) { Debug.LogException(ex); }
            }
        }
        #endregion

        #region Punch Scale
        public static Coroutine PlayPunchScale(MonoBehaviour host, Button button, float punch = 1.1f, float duration = 0.12f)
        {
            try
            {
                if (host == null || button == null) return null;
                return host.StartCoroutine(PunchScaleRoutine(button, punch, duration));
            }
            catch (Exception ex) { Debug.LogException(ex); return null; }
        }

        private static IEnumerator PunchScaleRoutine(Button button, float punch, float duration)
        {
            Vector3 initial = Vector3.one;
            if (button == null) yield break;
            try
            {
                initial = button.transform.localScale;
            }
            catch (Exception ex) { Debug.LogException(ex); }
            if (duration <= 0.01f) duration = 0.12f;
            if (punch <= 1f) punch = 1.1f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float dt = 0.016f;
                try { dt = Time.unscaledDeltaTime; } catch (Exception ex) { Debug.LogException(ex); }
                elapsed += dt;
                bool alive = false;
                try { alive = button != null; } catch (Exception ex) { Debug.LogException(ex); }
                if (!alive) yield break;
                bool ok = true;
                try
                {
                    float t = Mathf.Clamp01(elapsed / duration);
                    float s = 1f + (punch - 1f) * Mathf.Sin(Mathf.PI * t);
                    button.transform.localScale = initial * s;
                }
                catch (Exception ex) { Debug.LogException(ex); ok = false; }
                if (!ok) yield break;
                yield return null;
            }
            try { if (button != null) button.transform.localScale = initial; }
            catch (Exception ex) { Debug.LogException(ex); }
        }
        #endregion

        #region Blinking Cursor
        public static Coroutine PlayBlinkingCursor(MonoBehaviour host, TMP_Text text, float blinkRate = 2f, float minAlpha = 0.25f)
        {
            try
            {
                if (host == null || text == null) return null;
                return host.StartCoroutine(BlinkTmpRoutine(text, blinkRate, minAlpha));
            }
            catch (Exception ex) { Debug.LogException(ex); return null; }
        }

        public static Coroutine PlayBlinkingCursor(MonoBehaviour host, Text text, float blinkRate = 2f, float minAlpha = 0.25f)
        {
            try
            {
                if (host == null || text == null) return null;
                return host.StartCoroutine(BlinkTextRoutine(text, blinkRate, minAlpha));
            }
            catch (Exception ex) { Debug.LogException(ex); return null; }
        }

        private static IEnumerator BlinkTmpRoutine(TMP_Text text, float blinkRate, float minAlpha)
        {
            float t = 0f;
            if (blinkRate <= 0.1f) blinkRate = 2f;
            minAlpha = Mathf.Clamp01(minAlpha);
            while (true)
            {
                bool alive = false;
                try { alive = text != null; } catch (Exception ex) { Debug.LogException(ex); }
                if (!alive) yield break;
                float dt = 0.016f;
                try { dt = Time.unscaledDeltaTime; } catch (Exception ex) { Debug.LogException(ex); }
                t += dt;
                bool ok = true;
                try
                {
                    float wave = 0.5f + 0.5f * Mathf.Sin(t * blinkRate * Mathf.PI * 2f);
                    text.alpha = Mathf.Lerp(minAlpha, 1f, wave);
                }
                catch (Exception ex) { Debug.LogException(ex); ok = false; }
                if (!ok) yield break;
                yield return null;
            }
        }

        private static IEnumerator BlinkTextRoutine(Text text, float blinkRate, float minAlpha)
        {
            float t = 0f;
            if (blinkRate <= 0.1f) blinkRate = 2f;
            minAlpha = Mathf.Clamp01(minAlpha);
            while (true)
            {
                bool alive = false;
                try { alive = text != null; } catch (Exception ex) { Debug.LogException(ex); }
                if (!alive) yield break;
                float dt = 0.016f;
                try { dt = Time.unscaledDeltaTime; } catch (Exception ex) { Debug.LogException(ex); }
                t += dt;
                bool ok = true;
                try
                {
                    float wave = 0.5f + 0.5f * Mathf.Sin(t * blinkRate * Mathf.PI * 2f);
                    Color c = text.color;
                    c.a = Mathf.Lerp(minAlpha, 1f, wave);
                    text.color = c;
                }
                catch (Exception ex) { Debug.LogException(ex); ok = false; }
                if (!ok) yield break;
                yield return null;
            }
        }
        #endregion

        #region Selection Tick (AAA pause accent)
        private static Coroutine _tickRoutine;
        private static GameObject _tickObj;
        private static MonoBehaviour _tickHost;

        /// <summary>
        /// Spawns a small slow-pulsing yellow tick that follows the Y of the currently
        /// EventSystem-selected button while paused. Null-safe; stops on resume.
        /// </summary>
        public static void BeginSelectionTick(MonoBehaviour host, GameObject panelRoot)
        {
            try
            {
                try { StopSelectionTick(); } catch (Exception ex) { Debug.LogException(ex); }
                if (host == null || panelRoot == null) return;
                GameObject tick = null;
                try
                {
                    tick = new GameObject("PauseSelectionTick");
                    tick.transform.SetParent(panelRoot.transform, false);
                    RectTransform rt = tick.AddComponent<RectTransform>();
                    rt.sizeDelta = new Vector2(14f, 22f);
                    Image img = tick.AddComponent<Image>();
                    try { img.sprite = PauseMenuBuilder.GetPillSprite(); }
                    catch (Exception ex) { Debug.LogException(ex); }
                    img.type = Image.Type.Sliced;
                    img.color = new Color(0f, 0.90f, 1f, 0.95f);
                    img.raycastTarget = false;
                }
                catch (Exception ex) { Debug.LogException(ex); return; }
                _tickObj = tick;
                _tickHost = host;
                try { _tickRoutine = host.StartCoroutine(SelectionTickRoutine(host, panelRoot, tick)); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
            catch (Exception ex) { Debug.LogException(ex); }
        }

        public static void StopSelectionTick()
        {
            try
            {
                try
                {
                    if (_tickHost != null && _tickRoutine != null)
                        _tickHost.StopCoroutine(_tickRoutine);
                }
                catch (Exception ex) { Debug.LogException(ex); }
            }
            catch (Exception ex) { Debug.LogException(ex); }
            finally
            {
                _tickRoutine = null;
                try { if (_tickObj != null) UnityEngine.Object.Destroy(_tickObj); }
                catch (Exception ex) { Debug.LogException(ex); }
                _tickObj = null;
                _tickHost = null;
            }
        }

        private static IEnumerator SelectionTickRoutine(MonoBehaviour host, GameObject panelRoot, GameObject tick)
        {
            float t = 0f;
            while (true)
            {
                float dt = 0.016f;
                try { dt = Time.unscaledDeltaTime; } catch (Exception ex) { Debug.LogException(ex); }
                t += dt;
                bool alive = false;
                try { alive = host != null && tick != null && panelRoot != null && panelRoot.activeInHierarchy && InGamePauseManager.IsPaused; }
                catch (Exception ex) { Debug.LogException(ex); alive = false; }
                if (!alive) yield break;
                try
                {
                    RectTransform tickRt = null;
                    RectTransform panelRt = null;
                    try { tickRt = tick.GetComponent<RectTransform>(); } catch (Exception ex) { Debug.LogException(ex); }
                    try { panelRt = panelRoot.GetComponent<RectTransform>(); } catch (Exception ex) { Debug.LogException(ex); }
                    EventSystem es = null;
                    try { es = EventSystem.current; } catch (Exception ex) { Debug.LogException(ex); }
                    GameObject sel = null;
                    try { sel = es != null ? es.currentSelectedGameObject : null; } catch (Exception ex) { Debug.LogException(ex); }
                    Button selBtn = null;
                    if (sel != null)
                    {
                        try { selBtn = sel.GetComponent<Button>(); } catch (Exception ex) { Debug.LogException(ex); }
                    }
                    bool underRoot = false;
                    try { underRoot = selBtn != null && selBtn.transform.IsChildOf(panelRoot.transform); }
                    catch (Exception ex) { Debug.LogException(ex); }
                    Image img = null;
                    try { img = tick.GetComponent<Image>(); } catch (Exception ex) { Debug.LogException(ex); }
                    if (!underRoot || tickRt == null || panelRt == null)
                    {
                        if (img != null) img.enabled = false;
                    }
                    else
                    {
                        if (img != null) img.enabled = true;
                        RectTransform btnRt = null;
                        try { btnRt = selBtn.GetComponent<RectTransform>(); } catch (Exception ex) { Debug.LogException(ex); }
                        if (btnRt != null)
                        {
                            Vector3 world = btnRt.TransformPoint(btnRt.rect.center);
                            Vector3 local = panelRt.InverseTransformPoint(world);
                            tickRt.anchoredPosition = new Vector2(local.x - btnRt.rect.width * 0.5f - 16f, local.y);
                            float pulse = 1f + 0.18f * Mathf.Sin(t * 6f);
                            tickRt.localScale = new Vector3(pulse, pulse, 1f);
                            if (img != null)
                            {
                                Color c = img.color;
                                c.a = 0.65f + 0.35f * (0.5f + 0.5f * Mathf.Sin(t * 6f));
                                img.color = c;
                            }
                        }
                    }
                }
                catch (Exception ex) { Debug.LogException(ex); }
                yield return null;
            }
        }
        #endregion
    }
}
