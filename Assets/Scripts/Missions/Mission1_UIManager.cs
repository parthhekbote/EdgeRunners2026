using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using InsaneGun.Combat;

namespace Cyberpunk.Missions
{
    /// <summary>
    /// Modern 2025 Glass-Style UI Manager for Mission 1:
    /// - Authentic Cyberpunk 2077 Holo-Call UI for Dr. Ido (860x520 glass panel, #05070D 0.97,
    ///   gradient cyan->magenta 1px border, animated decrypting data bar, 240x240 glitching portrait,
    ///   animated audio waveform equalizer bars, live blinking red badge, typewriter text 0.02s/char,
    ///   "[ JACK IN — ACCEPT GIG ]" 240x60 button).
    /// - Integrated with Mission1_AudioManager (incoming ring, Ido voice line, typewriter blips, whoosh).
    /// - 3D Screen-Space & Screen-Edge Waypoint Navigation HUD with Kiroshi Cyan theme.
    /// - Top-left persistent objective tracker HUD ("Defeat the rogue combat bots: 0/3").
    /// - Fullscreen red damage flash vignette on player hit.
    /// - Fullscreen "SIGNAL LOST - REBOOTING..." blackout on player death.
    /// </summary>
    public class Mission1_UIManager : MonoBehaviour
    {
        public static Mission1_UIManager Instance { get; private set; }

        // ─── Cyberpunk Color Palette ─────────────────────────────────────────
        public static readonly Color KiroshiCyan   = new Color(0.000f, 0.941f, 1.000f, 1.0f); // #00F0FF
        public static readonly Color CyberYellow   = new Color(0.988f, 0.933f, 0.039f, 1.0f); // #FCEE0A
        public static readonly Color NeonMagenta   = new Color(1.000f, 0.050f, 0.650f, 1.0f); // #FF0DA6
        public static readonly Color NeonCrimson   = new Color(1.000f, 0.000f, 0.235f, 1.0f); // #FF003C
        public static readonly Color DarkObsidian  = new Color(0.020f, 0.028f, 0.051f, 0.97f); // #05070D
        public static readonly Color GlassDimBg    = new Color(0.010f, 0.015f, 0.030f, 0.75f);
        public static readonly Color TextWhite     = new Color(0.960f, 0.980f, 1.000f, 1.0f);
        public static readonly Color TextCyanSoft  = new Color(0.910f, 0.957f, 1.000f, 1.0f); // #E8F4FF
        public static readonly Color TextGreyHint  = new Color(0.545f, 0.608f, 0.706f, 1.0f); // #8B9BB4

        [Header("Dr. Ido Visuals & Fonts")]
        public Sprite idoPortraitSprite;
        public TMP_FontAsset headerFont;
        public TMP_FontAsset boldFont;
        public TMP_FontAsset bodyFont;

        // UI Root Canvas & Panels
        private Canvas _missionCanvas;
        private GameObject _dimBackdrop;
        private GameObject _popupPanel;
        private RectTransform _popupRect;
        private TextMeshProUGUI _popupHeaderBarText;
        private TextMeshProUGUI _popupSpeakerNameText;
        private TextMeshProUGUI _popupBodyText;
        private TextMeshProUGUI _popupButtonText;
        private TextMeshProUGUI _popupShortcutHintText;
        private Image _portraitImage;
        private RectTransform _portraitContainerRect;
        private Button _popupButton;
        private Image _popupButtonBg;
        private TextMeshProUGUI _liveBadgeText;
        private RectTransform _holoSweepBar;

        // Audio Waveform Equalizer Bars (14 animated bars)
        private List<RectTransform> _waveformBars = new List<RectTransform>();


        // Waypoint Guidance HUD (Cyan Theme)
        private GameObject _waypointPanel;
        private RectTransform _waypointRect;
        private TextMeshProUGUI _waypointText;
        private RectTransform _waypointArrowRect;
        private TextMeshProUGUI _waypointArrowText;

        // Follow-Ido Escort Pill (top-center, visible only while Ido waits for the player)
        private GameObject _followPillPanel;
        private RectTransform _followPillRect;
        private TextMeshProUGUI _followPillText;
        private Image _followPillBg;

        // Tracker HUD
        private GameObject _trackerPanel;
        private TextMeshProUGUI _trackerHeader;
        private TextMeshProUGUI _trackerObjective;

        // Fullscreen Overlays
        private Image _damageVignette;
        private GameObject _rebootBlackoutPanel;
        private TextMeshProUGUI _rebootText;
        private Image _blackoutBg;

        // Typewriter & Animation State
        private Coroutine _typewriterCoroutine;
        private Coroutine _portraitGlitchCoroutine;
        private string _targetFullText = "";
        private bool _isTyping = false;
        private Action _onPopupConfirmed;
        private float _shimmerTimer = 0f;

        // Audio & Terminal Blips
        private AudioSource _localAudioSource;
        private static AudioClip _terminalBlipClip;

        private void Awake()
        {
            Instance = this;
            LoadFontsAndSprites();
            EnsureAudioSource();
            BuildMissionCanvas();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public bool IsPopupOpen()
        {
            return _popupPanel != null && _popupPanel.activeSelf;
        }

        private void LoadFontsAndSprites()
        {
            headerFont = Resources.Load<TMP_FontAsset>("SyntyUI/Fonts/Font_SciFi_Header");
            boldFont   = Resources.Load<TMP_FontAsset>("SyntyUI/Fonts/Font_SciFi_Bold");
            bodyFont   = Resources.Load<TMP_FontAsset>("SyntyUI/Fonts/Font_SciFi_Body");

#if UNITY_EDITOR
            // High-resolution Dr. Daisuke Ido Cyberpunk Holographic Portrait
            idoPortraitSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Characters/Doctor/Textures/T_Doctor_Ido_HoloPortrait.png");
            if (idoPortraitSprite == null)
            {
                idoPortraitSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Characters/Doctor/Textures/T_Doctor_Ido_HoloPortrait.jpg");
            }
            if (idoPortraitSprite == null)
            {
                idoPortraitSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceSciFiSoldierHUD/Samples/Sprites/SPR_HUD_SciFi_Example_Comms_Portrait.png");
            }
#endif
            if (idoPortraitSprite == null)
            {
                idoPortraitSprite = Resources.Load<Sprite>("SyntyUI/Sprites/SPR_HUD_SciFi_Example_Comms_Portrait")
                                 ?? Resources.Load<Sprite>("SPR_HUD_SciFi_Example_Comms_Portrait");
            }
        }

        private void EnsureAudioSource()
        {
            _localAudioSource = GetComponent<AudioSource>();
            if (_localAudioSource == null)
            {
                _localAudioSource = gameObject.AddComponent<AudioSource>();
                _localAudioSource.playOnAwake = false;
                _localAudioSource.spatialBlend = 0f;
            }
        }

        private static AudioClip GetTerminalBlip()
        {
            if (_terminalBlipClip == null)
            {
                int sampleCount = 44100 / 70; // ~14ms short click blip
                float[] samples = new float[sampleCount];
                for (int i = 0; i < sampleCount; i++)
                {
                    float t = (float)i / sampleCount;
                    samples[i] = Mathf.Sin(2f * Mathf.PI * 2100f * (float)i / 44100f) * (1f - t) * 0.14f;
                }
                _terminalBlipClip = AudioClip.Create("TerminalClickBlip", sampleCount, 1, 44100, false);
                _terminalBlipClip.SetData(samples, 0);
            }
            return _terminalBlipClip;
        }

        private void Update()
        {
            // Keyboard advancement for popups
            if (_popupPanel != null && _popupPanel.activeSelf)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
                    Input.GetKeyDown(KeyCode.Space)  || Input.GetKeyDown(KeyCode.E))
                {
                    OnPopupAdvancePressed();
                }

                // Blinking red LIVE badge
                if (_liveBadgeText != null)
                {
                    float blink = Mathf.PingPong(Time.unscaledTime * 3.5f, 1f);
                    _liveBadgeText.color = new Color(NeonCrimson.r, NeonCrimson.g, NeonCrimson.b, blink > 0.45f ? 1f : 0.15f);
                }

                // Animated top data-bar decryption shimmer
                _shimmerTimer += Time.unscaledDeltaTime * 4.5f;
                int dots = (int)(_shimmerTimer % 4);
                string dotStr = new string('.', dots);
                if (_popupHeaderBarText != null)
                {
                    _popupHeaderBarText.text = $"[//] INCOMING HOLO-CALL // ENCRYPTED 0x7F-SECTOR7 -- DECRYPTING{dotStr}";
                }

                // Gentle portrait idle float / breathing motion
                if (_portraitContainerRect != null)
                {
                    float bob = Mathf.Sin(Time.unscaledTime * 2.2f) * 3.0f;
                    _portraitContainerRect.anchoredPosition = new Vector2(36f, -65f + bob);
                }

                // Hologram Sweeping Laser Scanline (moves down continuously)
                if (_holoSweepBar != null)
                {
                    float sweep = Mathf.Repeat(Time.unscaledTime * 110f, 240f);
                    _holoSweepBar.anchoredPosition = new Vector2(0f, -sweep);
                }

                // Holographic Micro-Glitch & Chromatic Flicker
                if (_portraitImage != null)
                {
                    float glitchVal = Mathf.PerlinNoise(Time.unscaledTime * 9f, 42.5f);
                    if (glitchVal > 0.84f)
                    {
                        float flicker = UnityEngine.Random.Range(0.65f, 1.25f);
                        _portraitImage.color = new Color(0.55f * flicker, 1.0f * flicker, 1.0f * flicker, 0.95f);
                        _portraitImage.rectTransform.anchoredPosition = new Vector2(UnityEngine.Random.Range(-2.5f, 2.5f), UnityEngine.Random.Range(-1.5f, 1.5f));
                    }
                    else
                    {
                        _portraitImage.color = new Color(0.88f, 1.0f, 1.0f, 1.0f);
                        _portraitImage.rectTransform.anchoredPosition = Vector2.zero;
                    }
                }

                // Live Audio Waveform Equalizer Animation (bouncing frequency bars)
                if (_waveformBars != null && _waveformBars.Count > 0)
                {
                    for (int i = 0; i < _waveformBars.Count; i++)
                    {
                        var bar = _waveformBars[i];
                        if (bar != null)
                        {
                            float noise = Mathf.PerlinNoise(Time.unscaledTime * 7.5f, (float)i * 0.45f);
                            float barHeight = Mathf.Lerp(4f, 26f, noise);
                            bar.sizeDelta = new Vector2(9f, barHeight);
                        }
                    }
                }
            }

            // Follow-Ido escort pill pulse (runs independent of the call popup)
            if (_followPillPanel != null && _followPillPanel.activeSelf && _followPillBg != null)
            {
                float pulse = 0.62f + 0.38f * Mathf.PingPong(Time.unscaledTime * 2.0f, 1f);
                Color c = _followPillBg.color;
                _followPillBg.color = new Color(c.r, c.g, c.b, 0.88f * pulse);
            }


        }

        // ─────────────────────────────────────────────────────────────────────
        // 1. POPUP DISPLAY & TYPEWRITER METHODS
        // ─────────────────────────────────────────────────────────────────────
        public void ShowIntroPopup(Action onAccepted)
        {
            string body =
                "Alita, telemetry shows a rogue Vector security detachment has locked down the Sector 7 Skybridge above the Scrapyard. Corrupted cores, attacking scavengers on sight.\n\n" +
                "I've routed the optical waypoint trail to your cybernetic HUD. Escort me through the district or move ahead to dismantle those units.\n\n" +
                "Let's verify your neural interface under real combat pressure. Stay sharp.";

            ShowPopup("Dr. Ido", body, "[ INITIALIZE — COMMENCE PATROL ]", "[Enter] Accept Briefing", onAccepted);
        }

        public void ShowCompletedPopup(Action onClosed)
        {
            string body =
                "Combat telemetry confirmed. All three corrupted enforcer cores have suffered total shutdown. The bridge corridor is clear.\n\n" +
                "Your reaction times and motor sync exceeded my calculations... that combat instinct wasn't built in any Scrapyard clinic.\n\n" +
                "Catch your breath, Alita. The lower district is open to explore, but keep your guard up.";
            ShowPopup("Dr. Ido", body, "[ COMPLETE MISSION ]", "[Enter] Return", onClosed, true);
        }

        public void ShowIntelPopup(string speakerName, string body, string buttonText, string shortcutHint, Action onConfirmed, AudioClip voice = null)
        {
            ShowPopup(speakerName, body, buttonText, shortcutHint, onConfirmed, false, voice, false);
        }

        public void ShowPopup(string speakerName, string body, string buttonText, string shortcutHint, Action onConfirmed, bool isComplete = false, AudioClip voiceOverride = null, bool playVoiceLine = true)
        {
            _targetFullText = body;
            _onPopupConfirmed = onConfirmed;

            if (_dimBackdrop != null) _dimBackdrop.SetActive(true);
            if (_popupPanel != null) _popupPanel.SetActive(true);

            if (_popupSpeakerNameText != null) _popupSpeakerNameText.text = speakerName;
            if (_popupButtonText != null) _popupButtonText.text = buttonText;
            if (_popupShortcutHintText != null) _popupShortcutHintText.text = shortcutHint;

            // Unlock and reveal mouse cursor
            SmoothOrbitCamera.cursorLockOverride = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Audio: incoming ring + Ido voice line + duck background music
            Mission1_AudioManager.Instance?.DuckBGM(true);
            if (isComplete)
                Mission1_AudioManager.Instance?.PlayUICompleteCall();
            else if (voiceOverride != null)
                Mission1_AudioManager.Instance?.PlayRingAndVoice(voiceOverride);
            else if (playVoiceLine)
                Mission1_AudioManager.Instance?.PlayUICall();
            else
                Mission1_AudioManager.Instance?.PlayRingOnly();

            // Glitch-in animation on portrait
            if (_portraitGlitchCoroutine != null) StopCoroutine(_portraitGlitchCoroutine);
            _portraitGlitchCoroutine = StartCoroutine(PortraitGlitchInRoutine());

            // Start typewriter
            if (_typewriterCoroutine != null) StopCoroutine(_typewriterCoroutine);
            _typewriterCoroutine = StartCoroutine(TypewriterRoutine(body));
        }

        private IEnumerator PortraitGlitchInRoutine()
        {
            if (_portraitImage == null) yield break;

            float elapsed = 0f;
            const float duration = 0.40f;
            Vector3 originalScale = Vector3.one;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float flicker = (UnityEngine.Random.value > 0.35f) ? 1.0f : 0.25f;
                _portraitImage.color = new Color(0.2f, 1f, 1f, flicker); // Cyan holographic tint

                // Slight RGB scale stutter
                float jitter = UnityEngine.Random.Range(-0.04f, 0.04f);
                _portraitImage.transform.localScale = originalScale + new Vector3(jitter, -jitter, 0f);

                yield return new WaitForSecondsRealtime(0.04f);
            }

            _portraitImage.color = new Color(0.85f, 1f, 1f, 1.0f);
            _portraitImage.transform.localScale = originalScale;
            _portraitGlitchCoroutine = null;
        }

        private void OnPopupAdvancePressed()
        {
            if (_isTyping)
            {
                // Fast-forward / complete typing instantly
                if (_typewriterCoroutine != null) StopCoroutine(_typewriterCoroutine);
                if (_popupBodyText != null) _popupBodyText.text = _targetFullText;
                _isTyping = false;
            }
            else
            {
                DismissPopup();
            }
        }

        public void DismissPopup()
        {
            if (_typewriterCoroutine != null)
            {
                StopCoroutine(_typewriterCoroutine);
                _typewriterCoroutine = null;
            }
            _isTyping = false;

            if (_popupPanel != null) _popupPanel.SetActive(false);
            if (_dimBackdrop != null) _dimBackdrop.SetActive(false);

            // Restore mouse cursor lock
            SmoothOrbitCamera.cursorLockOverride = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Audio: glitch whoosh + restore BGM
            Mission1_AudioManager.Instance?.PlayUIConfirm();
            Mission1_AudioManager.Instance?.DuckBGM(false);

            var cb = _onPopupConfirmed;
            _onPopupConfirmed = null;
            cb?.Invoke();
        }

        private IEnumerator TypewriterRoutine(string fullText)
        {
            _isTyping = true;
            if (_popupBodyText != null) _popupBodyText.text = "";

            var blip = GetTerminalBlip();

            for (int i = 0; i <= fullText.Length; i++)
            {
                if (_popupBodyText != null) _popupBodyText.text = fullText.Substring(0, i);

                // Play audio click blip per 2 characters
                if (i < fullText.Length && fullText[i] != ' ' && fullText[i] != '\n' && (i % 2 == 0))
                {
                    if (_localAudioSource != null && blip != null)
                    {
                        _localAudioSource.PlayOneShot(blip, 0.09f);
                    }
                }

                yield return new WaitForSecondsRealtime(0.020f);
            }

            _isTyping = false;
            _typewriterCoroutine = null;
        }

        // ─────────────────────────────────────────────────────────────────────
        // 2. WAYPOINT ASSISTANCE HUD (KIROSHI CYAN THEME)
        // ─────────────────────────────────────────────────────────────────────
        public void UpdateWaypointHUD(Vector3 targetWorldPos, bool active, string labelOverride = null)
        {
            if (_waypointPanel == null) return;

            if (!active)
            {
                if (_waypointPanel.activeSelf) _waypointPanel.SetActive(false);
                return;
            }

            if (!_waypointPanel.activeSelf) _waypointPanel.SetActive(true);

            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 markerWorldPos = targetWorldPos + Vector3.up * 2.0f;
            float dist = Vector3.Distance(cam.transform.position, targetWorldPos);
            Vector3 screenPos = cam.WorldToScreenPoint(markerWorldPos);

            bool isBehind = screenPos.z < 0f;
            float sw = Screen.width;
            float sh = Screen.height;
            Vector2 screenCenter = new Vector2(sw * 0.5f, sh * 0.5f);

            Vector2 projectedPos = new Vector2(screenPos.x, screenPos.y);
            if (isBehind)
            {
                projectedPos = screenCenter - (projectedPos - screenCenter);
            }

            // Safe Clamping Margins: Avoid overlapping top bio-monitor/minimap (y > sh - 180) and bottom hotkeys/weapon card (y < 210)
            float minX = 140f;
            float maxX = sw - 140f;
            float minY = 210f;
            float maxY = sh - 180f;

            bool isOffScreen = isBehind || projectedPos.x < minX || projectedPos.x > maxX
                                        || projectedPos.y < minY || projectedPos.y > maxY;

            if (isOffScreen)
            {
                Vector2 dir = (projectedPos - screenCenter).normalized;
                if (dir.sqrMagnitude < 0.001f) dir = Vector2.up;

                float halfW = (maxX - minX) * 0.5f;
                float halfH = (maxY - minY) * 0.5f;
                Vector2 boxCenter = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);

                float scaleX = Mathf.Abs(dir.x) > 0.0001f ? halfW / Mathf.Abs(dir.x) : float.MaxValue;
                float scaleY = Mathf.Abs(dir.y) > 0.0001f ? halfH / Mathf.Abs(dir.y) : float.MaxValue;
                float scale = Mathf.Min(scaleX, scaleY);
                Vector2 clamped = boxCenter + dir * scale;

                _waypointRect.position = clamped;
                _waypointRect.sizeDelta = new Vector2(190f, 36f);

                if (_waypointArrowRect != null)
                {
                    _waypointArrowRect.gameObject.SetActive(true);
                    float deg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                    _waypointArrowRect.rotation = Quaternion.Euler(0f, 0f, deg);
                    _waypointArrowRect.anchoredPosition = dir.x >= 0f ? new Vector2(78f, 0f) : new Vector2(-78f, 0f);
                }

                if (_waypointText != null)
                {
                    string tag = string.IsNullOrEmpty(labelOverride) ? "SKYBRIDGE" : labelOverride;
                    _waypointText.text = $"<color=#00F0FF>{tag}</color>  <b>{dist:F0}m</b>";
                }
            }
            else
            {
                _waypointRect.position = projectedPos;
                _waypointRect.sizeDelta = new Vector2(210f, 40f);

                if (_waypointArrowRect != null)
                {
                    _waypointArrowRect.gameObject.SetActive(false);
                }

                if (_waypointText != null)
                {
                    string tag = string.IsNullOrEmpty(labelOverride) ? "◇ SKYBRIDGE ARENA" : "◇ " + labelOverride;
                    _waypointText.text = $"<color=#00F0FF>{tag}</color>\n<size=11><b>{dist:F0}m</b></size>";
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // 2b. FOLLOW-IDO ESCORT PILL (driven by the prologue escort, never blocks input)
        // ─────────────────────────────────────────────────────────────────────
        public void ShowFollowPopup(bool show)
        {
            if (_followPillPanel == null)
            {
                if (!show) return;
                if (_missionCanvas != null) BuildFollowIdoPill(_missionCanvas.gameObject);
                if (_followPillPanel == null) return;
            }
            if (_followPillPanel.activeSelf != show)
            {
                _followPillPanel.SetActive(show);
                Debug.Log($"[Mission1] Follow pill {(show ? "SHOWN" : "hidden")}.");
            }
        }

        public void UpdateFollowDistance(float dist, float limit)
        {
            if (_followPillText == null) return;
            _followPillText.text = $"◈ STAY WITH IDO ({dist:F0}m / {limit:F0}m)";
            if (_followPillPanel != null && !_followPillPanel.activeSelf) _followPillPanel.SetActive(true);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 3. OBJECTIVE TRACKER HUD
        // ─────────────────────────────────────────────────────────────────────
        public void UpdateKillTracker(int current, int total)
        {
            if (SyntyCombined.CyberpunkHUDController.Instance != null)
            {
                string objText = current >= total
                    ? $"Rogue combat units: {current}/{total}"
                    : $"Neutralize rogue bots: {current}/{total}";
                SyntyCombined.CyberpunkHUDController.Instance.SetQuestObjective("MISSION 1 // SKYBRIDGE TRIAL", objText);

                if (_trackerPanel != null) _trackerPanel.SetActive(false);
                return;
            }

            if (_trackerPanel == null) return;

            if (current == 0 && total == 0)
            {
                _trackerPanel.SetActive(false);
                return;
            }

            _trackerPanel.SetActive(true);

            if (_trackerObjective != null)
            {
                if (current >= total)
                {
                    _trackerObjective.text = $"<color=#00F0FF>✓ Rogue combat units neutralized: {current}/{total}</color>";
                }
                else
                {
                    _trackerObjective.text = $"• Neutralize rogue combat bots: <color=#00F0FF><b>{current}/{total}</b></color>";
                }
            }
        }

        public void HideKillTracker()
        {
            if (_trackerPanel != null) _trackerPanel.SetActive(false);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 4. DAMAGE VIGNETTE FLASH
        // ─────────────────────────────────────────────────────────────────────
        public void FlashDamageVignette(float peakAlpha = 0.65f)
        {
            if (_damageVignette == null) return;
            StopCoroutine(nameof(DamageVignetteRoutine));
            StartCoroutine(DamageVignetteRoutine(peakAlpha));
        }

        private IEnumerator DamageVignetteRoutine(float peak)
        {
            float duration = 0.35f;
            float elapsed = 0f;
            if (_damageVignette != null && _damageVignette.sprite == null)
            {
                _damageVignette.sprite = SyntyCombined.CyberpunkHUDController.GetSideVignetteSprite();
            }

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float a = Mathf.Sin(t * Mathf.PI) * peak;
                _damageVignette.color = new Color(0.95f, 0.0f, 0.15f, a);
                yield return null;
            }

            _damageVignette.color = Color.clear;
        }

        // ─────────────────────────────────────────────────────────────────────
        // 5. PLAYER REBOOT BLACKOUT SEQUENCE
        // ─────────────────────────────────────────────────────────────────────
        public void TriggerRebootBlackout(float duration, Action onFadedOut)
        {
            StartCoroutine(RebootSequence(duration, onFadedOut));
        }

        private IEnumerator RebootSequence(float duration, Action onFadedOut)
        {
            if (_rebootBlackoutPanel == null)
            {
                BuildMissionCanvas();
            }
            if (_rebootBlackoutPanel == null || _blackoutBg == null || _rebootText == null)
            {
                onFadedOut?.Invoke();
                yield break;
            }

            _rebootBlackoutPanel.SetActive(true);
            _blackoutBg.color = Color.clear;
            _rebootText.color = Color.clear;

            // Fade to Black
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                _blackoutBg.color = new Color(0.01f, 0.01f, 0.02f, t);
                _rebootText.color = new Color(1f, 0.05f, 0.2f, t);
                yield return null;
            }

            _blackoutBg.color = new Color(0.01f, 0.01f, 0.02f, 1f);
            _rebootText.color = new Color(1f, 0.05f, 0.2f, 1f);

            // Execute Respawn Callback
            onFadedOut?.Invoke();

            yield return new WaitForSecondsRealtime(0.6f);

            // Fade back in
            elapsed = 0f;
            while (elapsed < 0.6f)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = 1f - Mathf.Clamp01(elapsed / 0.6f);
                _blackoutBg.color = new Color(0.01f, 0.01f, 0.02f, t);
                _rebootText.color = new Color(1f, 0.05f, 0.2f, t);
                yield return null;
            }

            _rebootBlackoutPanel.SetActive(false);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 6. CANVAS & COMPONENT BUILDER (AAA 2025 GLASS-STYLE POPUP 860x520)
        // ─────────────────────────────────────────────────────────────────────
        private void BuildMissionCanvas()
        {
            if (_missionCanvas != null) return;

            GameObject canvasObj = new GameObject("Canvas_Mission1_UI", typeof(RectTransform));
            canvasObj.transform.SetParent(transform, false);

            _missionCanvas = canvasObj.AddComponent<Canvas>();
            _missionCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _missionCanvas.sortingOrder = 40; // Above general HUD

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            BuildDamageVignette(canvasObj);
            BuildRebootBlackout(canvasObj);
            BuildWaypointHUD(canvasObj);
            BuildFollowIdoPill(canvasObj);
            BuildTrackerHUD(canvasObj);
            BuildModernGlassCallModal(canvasObj);
        }

        private void BuildDamageVignette(GameObject root)
        {
            GameObject vigObj = new GameObject("FX_DamageFlashVignette", typeof(RectTransform));
            vigObj.transform.SetParent(root.transform, false);
            SetFullStretch(vigObj);

            _damageVignette = vigObj.AddComponent<Image>();
            _damageVignette.sprite = SyntyCombined.CyberpunkHUDController.GetSideVignetteSprite();
            _damageVignette.color = Color.clear;
            _damageVignette.raycastTarget = false;
        }

        private void BuildRebootBlackout(GameObject root)
        {
            _rebootBlackoutPanel = new GameObject("Panel_RebootBlackout", typeof(RectTransform));
            _rebootBlackoutPanel.transform.SetParent(root.transform, false);
            SetFullStretch(_rebootBlackoutPanel);

            _blackoutBg = _rebootBlackoutPanel.AddComponent<Image>();
            _blackoutBg.color = Color.clear;

            GameObject textObj = new GameObject("Text_SignalLost", typeof(RectTransform));
            textObj.transform.SetParent(_rebootBlackoutPanel.transform, false);
            RectTransform rt = textObj.GetComponent<RectTransform>();
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(900f, 200f);

            _rebootText = textObj.AddComponent<TextMeshProUGUI>();
            _rebootText.text = "SIGNAL LOST\n<size=28><color=#00F0FF>// INITIATING NEURAL REBOOT PROTOCOL //</color></size>";
            _rebootText.fontSize = 54f;
            _rebootText.fontStyle = FontStyles.Bold;
            _rebootText.alignment = TextAlignmentOptions.Center;
            _rebootText.color = Color.clear;
            if (headerFont != null) _rebootText.font = headerFont;

            _rebootBlackoutPanel.SetActive(false);
        }

        private void BuildWaypointHUD(GameObject root)
        {
            _waypointPanel = new GameObject("Zone_Waypoint_NavMarker", typeof(RectTransform));
            _waypointPanel.transform.SetParent(root.transform, false);

            _waypointRect = _waypointPanel.GetComponent<RectTransform>();
            _waypointRect.sizeDelta = new Vector2(210f, 40f);
            _waypointRect.pivot = new Vector2(0.5f, 0.5f);

            // Dark Obsidian Backing
            Image bg = _waypointPanel.AddComponent<Image>();
            bg.color = new Color(0.02f, 0.03f, 0.05f, 0.90f);
            bg.raycastTarget = false;
            AddBorder(_waypointPanel, KiroshiCyan, 1.2f);

            // Left Cyan Accent
            GameObject strip = CreateChild(_waypointPanel, "Strip_Accent", Vector2.zero, new Vector2(4f, 40f));
            SetAnchorLeft(strip, 0f);
            strip.AddComponent<Image>().color = KiroshiCyan;

            // Edge Pointer Arrow (shows when off-screen)
            GameObject arrowObj = CreateChild(_waypointPanel, "Icon_EdgeArrow", new Vector2(-78f, 0f), new Vector2(20f, 20f));
            _waypointArrowRect = arrowObj.GetComponent<RectTransform>();
            _waypointArrowText = arrowObj.AddComponent<TextMeshProUGUI>();
            _waypointArrowText.text = ">";
            _waypointArrowText.fontSize = 18f;
            _waypointArrowText.alignment = TextAlignmentOptions.Center;
            _waypointArrowText.color = CyberYellow;
            _waypointArrowText.raycastTarget = false;
            if (boldFont != null) _waypointArrowText.font = boldFont;

            // Main Label Text
            GameObject textObj = CreateChild(_waypointPanel, "Text_WaypointLabel", Vector2.zero, new Vector2(200f, 38f));
            _waypointText = textObj.AddComponent<TextMeshProUGUI>();
            _waypointText.text = "<color=#00F0FF>// SKYBRIDGE ARENA</color>\n<size=11><b>-- m</b></size>";
            _waypointText.fontSize = 12f;
            _waypointText.fontStyle = FontStyles.Bold;
            _waypointText.alignment = TextAlignmentOptions.Center;
            _waypointText.color = TextWhite;
            _waypointText.raycastTarget = false;
            if (boldFont != null) _waypointText.font = boldFont;

            _waypointPanel.SetActive(true);
        }

        private void BuildFollowIdoPill(GameObject root)
        {
            _followPillPanel = new GameObject("Escort_FollowIdo_Pill", typeof(RectTransform));
            _followPillPanel.transform.SetParent(root.transform, false);

            _followPillRect = _followPillPanel.GetComponent<RectTransform>();
            _followPillRect.anchorMin = new Vector2(0.5f, 1f);
            _followPillRect.anchorMax = new Vector2(0.5f, 1f);
            _followPillRect.pivot = new Vector2(0.5f, 0.5f);
            _followPillRect.anchoredPosition = new Vector2(0f, -110f);
            _followPillRect.sizeDelta = new Vector2(640f, 30f);

            // Dark Obsidian Backing + Cyan Border (never blocks input)
            _followPillBg = _followPillPanel.AddComponent<Image>();
            _followPillBg.color = new Color(0.02f, 0.025f, 0.04f, 0.88f);
            _followPillBg.raycastTarget = false;
            AddBorder(_followPillPanel, KiroshiCyan, 1f);

            GameObject textObj = CreateChild(_followPillPanel, "Text_FollowIdo", Vector2.zero, new Vector2(620f, 28f));
            _followPillText = textObj.AddComponent<TextMeshProUGUI>();
            _followPillText.text = "◈ STAY WITH IDO";
            _followPillText.fontSize = 16f;
            _followPillText.fontStyle = FontStyles.Bold;
            _followPillText.alignment = TextAlignmentOptions.Center;
            _followPillText.color = KiroshiCyan;
            _followPillText.raycastTarget = false;
            if (boldFont != null) _followPillText.font = boldFont;

            _followPillPanel.SetActive(false);
        }

        private void BuildTrackerHUD(GameObject root)
        {
            _trackerPanel = new GameObject("Panel_MissionTracker", typeof(RectTransform));
            _trackerPanel.transform.SetParent(root.transform, false);

            RectTransform rt = _trackerPanel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(35f, -165f);
            rt.sizeDelta = new Vector2(360f, 96f);

            Image bg = _trackerPanel.AddComponent<Image>();
            bg.color = new Color(0.015f, 0.025f, 0.045f, 0.88f);
            bg.raycastTarget = false;
            AddBorder(_trackerPanel, new Color(0f, 0.941f, 1f, 0.40f), 1f);

            // Cyan header accent bar
            GameObject bar = CreateChild(_trackerPanel, "Bar_TopHeader", new Vector2(0f, 0f), new Vector2(360f, 3f));
            SetAnchorTop(bar, 0f);
            bar.AddComponent<Image>().color = KiroshiCyan;

            // Mission Title Header
            GameObject hObj = CreateChild(_trackerPanel, "Text_TrackerHeader", new Vector2(16f, -12f), new Vector2(330f, 24f));
            SetAnchorTop(hObj, -12f);
            SetAnchorLeft(hObj, 16f);
            _trackerHeader = hObj.AddComponent<TextMeshProUGUI>();
            _trackerHeader.text = "MISSION 1 // SKYBRIDGE COMBAT TRIAL";
            _trackerHeader.fontSize = 13f;
            _trackerHeader.fontStyle = FontStyles.Bold;
            _trackerHeader.color = KiroshiCyan;
            if (boldFont != null) _trackerHeader.font = boldFont;

            // Objective line
            GameObject objLine = CreateChild(_trackerPanel, "Text_ObjectiveLine", new Vector2(16f, -44f), new Vector2(330f, 38f));
            SetAnchorTop(objLine, -44f);
            SetAnchorLeft(objLine, 16f);
            _trackerObjective = objLine.AddComponent<TextMeshProUGUI>();
            _trackerObjective.text = "• Neutralize rogue combat bots: <color=#00F0FF><b>0/3</b></color>";
            _trackerObjective.fontSize = 14f;
            _trackerObjective.color = TextWhite;
            if (bodyFont != null) _trackerObjective.font = bodyFont;

            _trackerPanel.SetActive(false);
        }

        private void BuildModernGlassCallModal(GameObject root)
        {
            // Fullscreen Dim & Blur Backdrop (0.80 alpha)
            _dimBackdrop = new GameObject("Backdrop_DimBlur", typeof(RectTransform));
            _dimBackdrop.transform.SetParent(root.transform, false);
            SetFullStretch(_dimBackdrop);
            var dimImg = _dimBackdrop.AddComponent<Image>();
            dimImg.color = GlassDimBg;
            dimImg.raycastTarget = true;

            // Subtle scanlines across fullscreen
            GameObject scanlines = CreateChild(_dimBackdrop, "Overlay_Scanlines", Vector2.zero, Vector2.zero);
            SetFullStretch(scanlines);
            var slImg = scanlines.AddComponent<Image>();
            slImg.color = new Color(0f, 0.941f, 1f, 0.025f);
            slImg.raycastTarget = false;

            // Main Popup Panel 860x520
            _popupPanel = new GameObject("Panel_ModernGlass_DrIdoCall", typeof(RectTransform));
            _popupPanel.transform.SetParent(root.transform, false);

            _popupRect = _popupPanel.GetComponent<RectTransform>();
            _popupRect.anchorMin = new Vector2(0.5f, 0.5f);
            _popupRect.anchorMax = new Vector2(0.5f, 0.5f);
            _popupRect.pivot = new Vector2(0.5f, 0.5f);
            _popupRect.anchoredPosition = new Vector2(0f, 20f);
            _popupRect.sizeDelta = new Vector2(860f, 520f);

            // 1. DarkObsidian Backing Frame #05070D alpha 0.98
            Image bg = _popupPanel.AddComponent<Image>();
            bg.color = DarkObsidian;

            // 1.5px Gradient Cyan-to-Magenta Border
            AddGradientBorder(_popupPanel, KiroshiCyan, NeonMagenta, 1.5f);

            // Outer Soft Glow Border
            CreateEdge(_popupPanel, "Glow_T", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 4f), new Color(0f, 0.941f, 1f, 0.25f));
            CreateEdge(_popupPanel, "Glow_B", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Color(1f, 0.05f, 0.65f, 0.20f));

            // 2. Animated Top Data-Bar with progress shimmer
            GameObject headerBar = CreateChild(_popupPanel, "Bar_TopDataBar", new Vector2(0f, 0f), new Vector2(860f, 42f));
            SetAnchorTop(headerBar, 0f);
            Image hbImg = headerBar.AddComponent<Image>();
            hbImg.color = new Color(0.015f, 0.045f, 0.08f, 0.96f);
            AddBorder(headerBar, new Color(0f, 0.941f, 1f, 0.45f), 1f);

            // Header Title Text ("[//] INCOMING HOLO-CALL // ENCRYPTED 0x7F-SECTOR7")
            GameObject hTextObj = CreateChild(headerBar, "Text_CallHeader", new Vector2(24f, 0f), new Vector2(740f, 26f));
            SetAnchorLeft(hTextObj, 24f);
            _popupHeaderBarText = hTextObj.AddComponent<TextMeshProUGUI>();
            _popupHeaderBarText.text = "[//] INCOMING HOLO-CALL // ENCRYPTED 0x7F-SECTOR7 -- DECRYPTING...";
            _popupHeaderBarText.fontSize = 13f;
            _popupHeaderBarText.fontStyle = FontStyles.Bold;
            _popupHeaderBarText.color = KiroshiCyan;
            if (boldFont != null) _popupHeaderBarText.font = boldFont;

            // Header Close Button [X]
            GameObject closeBtnObj = CreateChild(headerBar, "Button_CloseX", new Vector2(-16f, 0f), new Vector2(30f, 26f));
            RectTransform cbRt = closeBtnObj.GetComponent<RectTransform>();
            cbRt.anchorMin = new Vector2(1f, 0.5f);
            cbRt.anchorMax = new Vector2(1f, 0.5f);
            cbRt.pivot = new Vector2(1f, 0.5f);
            cbRt.anchoredPosition = new Vector2(-16f, 0f);
            TextMeshProUGUI closeX = closeBtnObj.AddComponent<TextMeshProUGUI>();
            closeX.text = "[X]";
            closeX.fontSize = 16f;
            closeX.color = new Color(0.7f, 0.85f, 1f, 0.7f);
            closeX.alignment = TextAlignmentOptions.Center;
            Button cbBtn = closeBtnObj.AddComponent<Button>();
            cbBtn.onClick.AddListener(DismissPopup);

            // 3. LEFT 240x240 PORTRAIT PANEL
            GameObject portraitContainer = CreateChild(_popupPanel, "Panel_PortraitContainer", new Vector2(36f, -65f), new Vector2(240f, 240f));
            _portraitContainerRect = portraitContainer.GetComponent<RectTransform>();
            SetAnchorTop(portraitContainer, -65f);
            SetAnchorLeft(portraitContainer, 36f);

            Image pcBg = portraitContainer.AddComponent<Image>();
            pcBg.color = new Color(0.015f, 0.035f, 0.06f, 0.98f);
            AddBorder(portraitContainer, new Color(0f, 0.941f, 1f, 0.75f), 1.5f);

            // Cyan Corner Brackets on Portrait
            AddCornerBrackets(portraitContainer, KiroshiCyan, 14f, 2f);

            // Portrait Image (preserve aspect)
            GameObject pImgObj = CreateChild(portraitContainer, "Image_PortraitFace", Vector2.zero, new Vector2(230f, 230f));
            SetFullStretch(pImgObj);
            _portraitImage = pImgObj.AddComponent<Image>();
            _portraitImage.preserveAspect = true;
            _portraitImage.color = new Color(0.85f, 1f, 1f, 1f); // Hologram cyan tint
            if (idoPortraitSprite != null) _portraitImage.sprite = idoPortraitSprite;

            // Live scanline overlay over face
            GameObject scanOverlay = CreateChild(portraitContainer, "FX_Scanlines", Vector2.zero, Vector2.zero);
            SetFullStretch(scanOverlay);
            Image soImg = scanOverlay.AddComponent<Image>();
#if UNITY_EDITOR
            var scanSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceSciFiSoldierHUD/Sprites/HUD/SPR_HUD_SciFi_Comms_Scanlines.png");
            if (scanSprite != null) soImg.sprite = scanSprite;
#endif
            soImg.color = new Color(0f, 0.941f, 1f, 0.22f);
            soImg.raycastTarget = false;

            // Sweeping Holographic Laser Scanline Bar (3px high cyan glow)
            GameObject sweepBarObj = CreateChild(portraitContainer, "FX_HoloLaserSweep", new Vector2(0f, 0f), new Vector2(236f, 3f));
            SetAnchorTop(sweepBarObj, 0f);
            _holoSweepBar = sweepBarObj.GetComponent<RectTransform>();
            Image sweepImg = sweepBarObj.AddComponent<Image>();
            sweepImg.color = new Color(0f, 0.941f, 1f, 0.85f);
            sweepImg.raycastTarget = false;

            // Projector Emitter Strips (Top & Bottom cyan micro-emitters)
            GameObject topEmitter = CreateChild(portraitContainer, "Emitter_Top", new Vector2(0f, 0f), new Vector2(240f, 2f));
            SetAnchorTop(topEmitter, 0f);
            topEmitter.AddComponent<Image>().color = KiroshiCyan;

            GameObject botEmitter = CreateChild(portraitContainer, "Emitter_Bot", new Vector2(0f, 0f), new Vector2(240f, 2f));
            RectTransform beRt = botEmitter.GetComponent<RectTransform>();
            beRt.anchorMin = new Vector2(0f, 0f);
            beRt.anchorMax = new Vector2(1f, 0f);
            beRt.pivot = new Vector2(0.5f, 0f);
            beRt.anchoredPosition = Vector2.zero;
            beRt.sizeDelta = new Vector2(0f, 2f);
            botEmitter.AddComponent<Image>().color = KiroshiCyan;

            // Dynamic Audio Waveform Equalizer under portrait (14 animated vertical bars)
            GameObject wavePanel = CreateChild(_popupPanel, "Panel_AudioWaveform", new Vector2(36f, -315f), new Vector2(240f, 30f));
            SetAnchorTop(wavePanel, -315f);
            SetAnchorLeft(wavePanel, 36f);
            _waveformBars.Clear();

            const int barCount = 14;
            float totalW = 240f;
            float barW = 10f;
            float spacing = (totalW - (barCount * barW)) / (barCount - 1);

            for (int b = 0; b < barCount; b++)
            {
                float xPos = b * (barW + spacing);
                GameObject barObj = CreateChild(wavePanel, $"Bar_Freq_{b}", new Vector2(xPos, 0f), new Vector2(barW, 12f));
                RectTransform barRt = barObj.GetComponent<RectTransform>();
                barRt.anchorMin = new Vector2(0f, 0.5f);
                barRt.anchorMax = new Vector2(0f, 0.5f);
                barRt.pivot = new Vector2(0f, 0.5f);
                barRt.anchoredPosition = new Vector2(xPos, 0f);

                Image barImg = barObj.AddComponent<Image>();
                barImg.color = new Color(0.0f, 0.941f, 1.0f, 0.85f);
                barImg.raycastTarget = false;
                _waveformBars.Add(barRt);
            }

            // Label under waveform: "DR. DAISUKE IDO"
            GameObject docLabelObj = CreateChild(_popupPanel, "Text_DocLabel", new Vector2(36f, -355f), new Vector2(240f, 22f));
            SetAnchorTop(docLabelObj, -355f);
            SetAnchorLeft(docLabelObj, 36f);
            TextMeshProUGUI docLabel = docLabelObj.AddComponent<TextMeshProUGUI>();
            docLabel.text = "DR. DAISUKE IDO";
            docLabel.fontSize = 13f;
            docLabel.fontStyle = FontStyles.Bold;
            docLabel.color = TextWhite;
            docLabel.alignment = TextAlignmentOptions.Center;
            if (boldFont != null) docLabel.font = boldFont;

            // Subtitle: "CHIEF RIPPERDOC // CLINIC 01"
            GameObject docSubObj = CreateChild(_popupPanel, "Text_DocSubtitle", new Vector2(36f, -377f), new Vector2(240f, 18f));
            SetAnchorTop(docSubObj, -377f);
            SetAnchorLeft(docSubObj, 36f);
            TextMeshProUGUI docSub = docSubObj.AddComponent<TextMeshProUGUI>();
            docSub.text = "CHIEF RIPPERDOC // SECTOR 7";
            docSub.fontSize = 10f;
            docSub.color = KiroshiCyan;
            docSub.alignment = TextAlignmentOptions.Center;
            if (boldFont != null) docSub.font = boldFont;

            // Red Blinking "● LIVE FEED" badge
            GameObject liveBadgeObj = CreateChild(_popupPanel, "Text_LiveBadge", new Vector2(36f, -398f), new Vector2(240f, 20f));
            SetAnchorTop(liveBadgeObj, -398f);
            SetAnchorLeft(liveBadgeObj, 36f);
            _liveBadgeText = liveBadgeObj.AddComponent<TextMeshProUGUI>();
            _liveBadgeText.text = "● LIVE COMMS FEED";
            _liveBadgeText.fontSize = 11f;
            _liveBadgeText.fontStyle = FontStyles.Bold;
            _liveBadgeText.color = NeonCrimson;
            _liveBadgeText.alignment = TextAlignmentOptions.Center;
            if (headerFont != null) _liveBadgeText.font = headerFont;

            // 4. RIGHT CONTENT AREA
            // Title: "Dr. Ido" 26px bold white with cyan decorative line
            GameObject titleObj = CreateChild(_popupPanel, "Text_SpeakerTitle", new Vector2(310f, -65f), new Vector2(510f, 36f));
            SetAnchorTop(titleObj, -65f);
            SetAnchorLeft(titleObj, 310f);
            _popupSpeakerNameText = titleObj.AddComponent<TextMeshProUGUI>();
            _popupSpeakerNameText.text = "Dr. Ido";
            _popupSpeakerNameText.fontSize = 26f;
            _popupSpeakerNameText.fontStyle = FontStyles.Bold;
            _popupSpeakerNameText.color = TextWhite;
            if (headerFont != null) _popupSpeakerNameText.font = headerFont;

            // Decorative cyan underline
            GameObject decLine = CreateChild(_popupPanel, "DecLine_Speaker", new Vector2(310f, -100f), new Vector2(240f, 2f));
            SetAnchorTop(decLine, -100f);
            SetAnchorLeft(decLine, 310f);
            decLine.AddComponent<Image>().color = KiroshiCyan;

            // Body TextMeshPro 18px #E8F4FF, lineSpacing 26
            GameObject bodyObj = CreateChild(_popupPanel, "Text_BodyContent", new Vector2(310f, -115f), new Vector2(510f, 290f));
            SetAnchorTop(bodyObj, -115f);
            SetAnchorLeft(bodyObj, 310f);
            _popupBodyText = bodyObj.AddComponent<TextMeshProUGUI>();
            _popupBodyText.text = "";
            _popupBodyText.fontSize = 17f;
            _popupBodyText.lineSpacing = 24f;
            _popupBodyText.color = TextCyanSoft;
            _popupBodyText.alignment = TextAlignmentOptions.TopLeft;
            _popupBodyText.textWrappingMode = TextWrappingModes.Normal;
            if (bodyFont != null) _popupBodyText.font = bodyFont;

            // 5. BOTTOM-RIGHT BUTTON: "[ JACK IN — ACCEPT GIG ]" 240x60
            GameObject btnObj = CreateChild(_popupPanel, "Button_AcceptGig", new Vector2(-40f, 36f), new Vector2(240f, 60f));
            RectTransform btnRt = btnObj.GetComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(1f, 0f);
            btnRt.anchorMax = new Vector2(1f, 0f);
            btnRt.pivot = new Vector2(1f, 0f);
            btnRt.anchoredPosition = new Vector2(-40f, 36f);

            _popupButtonBg = btnObj.AddComponent<Image>();
            _popupButtonBg.color = KiroshiCyan;
            _popupButton = btnObj.AddComponent<Button>();
            _popupButton.onClick.AddListener(OnPopupAdvancePressed);

            // Button Hover FX Trigger
            var eventTrigger = btnObj.AddComponent<EventTrigger>();
            var pointerEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            pointerEnter.callback.AddListener((data) => {
                Mission1_AudioManager.Instance?.PlayButtonHover();
                if (_popupButtonBg != null) _popupButtonBg.color = Color.white;
            });
            var pointerExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            pointerExit.callback.AddListener((data) => {
                if (_popupButtonBg != null) _popupButtonBg.color = KiroshiCyan;
            });
            eventTrigger.triggers.Add(pointerEnter);
            eventTrigger.triggers.Add(pointerExit);

            // Button Label
            GameObject btnTxtObj = CreateChild(btnObj, "Text_BtnLabel", Vector2.zero, new Vector2(240f, 60f));
            SetFullStretch(btnTxtObj);
            _popupButtonText = btnTxtObj.AddComponent<TextMeshProUGUI>();
            _popupButtonText.text = "[ JACK IN — ACCEPT GIG ]";
            _popupButtonText.fontSize = 15f;
            _popupButtonText.fontStyle = FontStyles.Bold;
            _popupButtonText.alignment = TextAlignmentOptions.Center;
            _popupButtonText.color = Color.black;
            if (boldFont != null) _popupButtonText.font = boldFont;

            // Shortcut Hint under button: "[Enter / Space / E] Accept" (12px grey #8B9BB4)
            GameObject hintObj = CreateChild(_popupPanel, "Text_ShortcutHint", new Vector2(-40f, 14f), new Vector2(240f, 18f));
            RectTransform hRt = hintObj.GetComponent<RectTransform>();
            hRt.anchorMin = new Vector2(1f, 0f);
            hRt.anchorMax = new Vector2(1f, 0f);
            hRt.pivot = new Vector2(1f, 0f);
            hRt.anchoredPosition = new Vector2(-40f, 14f);
            _popupShortcutHintText = hintObj.AddComponent<TextMeshProUGUI>();
            _popupShortcutHintText.text = "[Enter / Space / E] Accept Transmission";
            _popupShortcutHintText.fontSize = 11f;
            _popupShortcutHintText.alignment = TextAlignmentOptions.Center;
            _popupShortcutHintText.color = TextGreyHint;

            _dimBackdrop.SetActive(false);
            _popupPanel.SetActive(false);
        }

        // ─── UI Layout Helpers ───────────────────────────────────────────────
        private static GameObject CreateChild(GameObject parent, string name, Vector2 pos, Vector2 size)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent.transform, false);
            RectTransform rt = child.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return child;
        }

        private static void SetFullStretch(GameObject obj)
        {
            RectTransform rt = obj.GetComponent<RectTransform>();
            if (rt == null) rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void SetAnchorTop(GameObject obj, float topOffset)
        {
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, topOffset);
        }

        private static void SetAnchorLeft(GameObject obj, float leftOffset)
        {
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, rt.anchorMin.y);
            rt.anchorMax = new Vector2(0f, rt.anchorMax.y);
            rt.pivot = new Vector2(0f, rt.pivot.y);
            rt.anchoredPosition = new Vector2(leftOffset, rt.anchoredPosition.y);
        }

        private static void AddBorder(GameObject parent, Color color, float thickness)
        {
            CreateEdge(parent, "Border_T", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, thickness), color);
            CreateEdge(parent, "Border_B", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, thickness), color);
            CreateEdge(parent, "Border_L", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(thickness, 0f), color);
            CreateEdge(parent, "Border_R", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(thickness, 0f), color);
        }

        private static void AddGradientBorder(GameObject parent, Color colCyan, Color colMagenta, float thickness)
        {
            CreateEdge(parent, "Border_T", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, thickness), colCyan);
            CreateEdge(parent, "Border_L", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(thickness, 0f), colCyan);
            CreateEdge(parent, "Border_B", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, thickness), colMagenta);
            CreateEdge(parent, "Border_R", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(thickness, 0f), colMagenta);
        }

        private static void AddCornerBrackets(GameObject parent, Color color, float length, float thickness)
        {
            CreateEdge(parent, "Bracket_TL_H", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(length, thickness), color);
            CreateEdge(parent, "Bracket_TL_V", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(thickness, length), color);
            CreateEdge(parent, "Bracket_TR_H", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(length, thickness), color);
            CreateEdge(parent, "Bracket_TR_V", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(thickness, length), color);
            CreateEdge(parent, "Bracket_BL_H", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(length, thickness), color);
            CreateEdge(parent, "Bracket_BL_V", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(thickness, length), color);
            CreateEdge(parent, "Bracket_BR_H", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(length, thickness), color);
            CreateEdge(parent, "Bracket_BR_V", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(thickness, length), color);
        }

        private static GameObject CreateEdge(GameObject parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 size, Color color)
        {
            GameObject edge = new GameObject(name, typeof(RectTransform));
            edge.transform.SetParent(parent.transform, false);
            RectTransform rt = edge.GetComponent<RectTransform>();
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.pivot = pivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
            Image img = edge.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return edge;
        }
    }
}
