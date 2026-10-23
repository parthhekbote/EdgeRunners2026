using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Alita
{
    /// <summary>
    /// Aesthetic, reactive Cyberpunk Start Menu HUD for Alita: Battle Angel.
    /// Provides:
    /// - Holographic Cyberpunk UI styling with neon accents and scanlines
    /// - Real-time animated ECG heartbeat monitor synced with Alita's antimatter core
    /// - Real-time Martian URM Berserker telemetry readout
    /// - Procedural sci-fi UI sound effects (hover, click, awakening surge)
    /// - Reactive button states (scale, glow, corner brackets)
    /// - Awakening sequence and seamless Cutscene Hook before launching Cyber City
    /// </summary>
    public class AlitaStartMenuHUD : MonoBehaviour
    {
        public static AlitaStartMenuHUD Instance { get; private set; }

        [Header("Scene References")]
        public AlitaBrokenCyborg alitaCyborg;
        public AlitaCinematicCamera alitaCamera;
        public string gameplaySceneName = "Demo_City";

        [Header("Cutscene Hook Settings")]
        [Tooltip("If true, pauses after Awakening sequence and waits for your custom cutscene to call CompleteCutsceneAndStartGame(). If false, automatically transitions to Cyber City.")]
        public bool hasCustomCutscene = false;
        public UnityEngine.Events.UnityEvent onAwakeningCutsceneTriggered;

        [Header("UI Panels & Canvas")]
        public CanvasGroup hudCanvasGroup;
        public GameObject mainHubContainer;
        public CanvasGroup glitchOverlayGroup;

        [Header("Buttons")]
        public Button btnStartAwakening;
        public Button btnSectorSelect;
        public Button btnSettings;
        public Button btnControls;
        public Button btnQuit;

        [Header("ECG Heartbeat Monitor")]
        public RawImage ecgMonitorImage;
        public TMP_Text ecgPulseStatusText;
        public TMP_Text ecgBpmText;

        [Header("Holographic Scanline & Effects")]
        public RawImage scanlineImage;
        public Image pulseIndicatorGlow;

        [Header("Audio")]
        public AudioSource uiAudioSource;

        private Texture2D _ecgTexture;
        private Color32[] _ecgPixels;
        private const int ECG_WIDTH = 256;
        private const int ECG_HEIGHT = 64;
        private float _ecgTimer = 0f;
        private bool _isTransitioning = false;

        private void Awake()
        {
            Instance = this;

            if (glitchOverlayGroup != null)
            {
                glitchOverlayGroup.alpha = 0f;
                glitchOverlayGroup.blocksRaycasts = false;
                glitchOverlayGroup.interactable = false;
            }

            if (hudCanvasGroup != null)
            {
                hudCanvasGroup.alpha = 1f;
                hudCanvasGroup.blocksRaycasts = true;
                hudCanvasGroup.interactable = true;
            }

            if (uiAudioSource == null)
            {
                uiAudioSource = gameObject.AddComponent<AudioSource>();
                uiAudioSource.playOnAwake = false;
                uiAudioSource.spatialBlend = 0f; // 2D UI sound
            }

            InitECGTexture();
            EnforceHUDAlignment();
        }

        private void OnEnable()
        {
            _isTransitioning = false;
            if (hudCanvasGroup != null)
            {
                hudCanvasGroup.alpha = 1f;
                hudCanvasGroup.blocksRaycasts = true;
                hudCanvasGroup.interactable = true;
            }
            if (glitchOverlayGroup != null)
            {
                glitchOverlayGroup.alpha = 0f;
                glitchOverlayGroup.blocksRaycasts = false;
                glitchOverlayGroup.interactable = false;
            }
        }

        private void Start()
        {
            if (alitaCyborg == null) alitaCyborg = FindAnyObjectByType<AlitaBrokenCyborg>();
            if (alitaCamera == null && Camera.main != null) alitaCamera = Camera.main.GetComponent<AlitaCinematicCamera>();

            SetupButtonListeners();
            EnforceHUDAlignment();
        }

        private void InitECGTexture()
        {
            _ecgTexture = new Texture2D(ECG_WIDTH, ECG_HEIGHT, TextureFormat.RGBA32, false);
            _ecgTexture.filterMode = FilterMode.Bilinear;
            _ecgTexture.wrapMode = TextureWrapMode.Clamp;
            _ecgPixels = new Color32[ECG_WIDTH * ECG_HEIGHT];

            Color32 bg = new Color32(4, 14, 22, 180);
            for (int i = 0; i < _ecgPixels.Length; i++) _ecgPixels[i] = bg;
            _ecgTexture.SetPixels32(_ecgPixels);
            _ecgTexture.Apply();

            if (ecgMonitorImage != null)
            {
                ecgMonitorImage.texture = _ecgTexture;
            }
        }

        private void SetupButtonListeners()
        {
            if (btnStartAwakening == null) btnStartAwakening = FindButtonInHierarchy("Btn_StartAwakening", "START", "AWAKENING");
            if (btnSectorSelect == null) btnSectorSelect = FindButtonInHierarchy("Btn_SectorMap", "SECTOR", "MAP");
            if (btnSettings == null) btnSettings = FindButtonInHierarchy("Btn_Settings", "SETTINGS");
            if (btnControls == null) btnControls = FindButtonInHierarchy("Btn_Controls", "Btn_OperationalArchive", "CONTROLS", "ARCHIVE");
            if (btnQuit == null) btnQuit = FindButtonInHierarchy("Btn_Quit", "Btn_AbortProtocol", "QUIT", "EXIT");

            PrepareButton(btnStartAwakening, OnStartAwakeningClicked);
            PrepareButton(btnSectorSelect, () =>
            {
                PlayClickSound();
                var hub = FindAnyObjectByType<SyntyCombined.MainMenuController>();
                if (hub != null) hub.ShowSectorSelect();
            });
            PrepareButton(btnSettings, () =>
            {
                PlayClickSound();
                var hub = FindAnyObjectByType<SyntyCombined.MainMenuController>();
                if (hub != null) hub.ShowSettings();
            });
            PrepareButton(btnControls, () =>
            {
                PlayClickSound();
                var hub = FindAnyObjectByType<SyntyCombined.MainMenuController>();
                if (hub != null) hub.ShowControls();
            });
            PrepareButton(btnQuit, () =>
            {
                PlayClickSound();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            });
        }

        private void PrepareButton(Button btn, UnityEngine.Events.UnityAction action)
        {
            if (btn == null) return;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(action);

            // Ensure child texts and decorative images do not intercept or flicker mouse raycasts
            foreach (var g in btn.GetComponentsInChildren<Graphic>(true))
            {
                if (g.gameObject != btn.gameObject)
                {
                    g.raycastTarget = false;
                }
            }

            AddReactiveButtonHover(btn);
        }

        private Button FindButtonInHierarchy(params string[] searchNames)
        {
            Transform root = mainHubContainer != null ? mainHubContainer.transform : transform;
            Button[] all = root.GetComponentsInChildren<Button>(true);
            foreach (var b in all)
            {
                string bName = b.name.ToUpper();
                TMP_Text tmp = b.GetComponentInChildren<TMP_Text>(true);
                string tStr = tmp != null ? tmp.text.ToUpper() : "";

                foreach (var s in searchNames)
                {
                    string target = s.ToUpper();
                    if (bName.Contains(target) || (!string.IsNullOrEmpty(tStr) && tStr.Contains(target)))
                    {
                        return b;
                    }
                }
            }
            return null;
        }

        private void AddReactiveButtonHover(Button btn)
        {
            if (btn == null) return;
            var trigger = btn.gameObject.GetComponent<UnityEngine.EventSystems.EventTrigger>() 
                          ?? btn.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            trigger.triggers.Clear();

            // Pointer Enter
            var enterEntry = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
            enterEntry.callback.AddListener((_) =>
            {
                PlayHoverSound();
                StartCoroutine(ScaleButtonRoutine(btn.transform, 1.04f, 0.12f));
            });
            trigger.triggers.Add(enterEntry);

            // Pointer Exit
            var exitEntry = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit };
            exitEntry.callback.AddListener((_) =>
            {
                StartCoroutine(ScaleButtonRoutine(btn.transform, 1.0f, 0.12f));
            });
            trigger.triggers.Add(exitEntry);
        }

        private IEnumerator ScaleButtonRoutine(Transform tr, float targetScale, float duration)
        {
            if (tr == null) yield break;
            Vector3 start = tr.localScale;
            Vector3 end = Vector3.one * targetScale;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (tr == null) yield break;
                elapsed += Time.unscaledDeltaTime;
                tr.localScale = Vector3.Lerp(start, end, elapsed / duration);
                yield return null;
            }
            if (tr != null) tr.localScale = end;
        }

        private void Update()
        {
            UpdateECGMonitor();
            UpdateScanlineEffect();
        }

        private void UpdateECGMonitor()
        {
            if (_ecgTexture == null) return;

            // Shift texture columns 1 pixel to the left
            for (int y = 0; y < ECG_HEIGHT; y++)
            {
                for (int x = 0; x < ECG_WIDTH - 1; x++)
                {
                    _ecgPixels[y * ECG_WIDTH + x] = _ecgPixels[y * ECG_WIDTH + (x + 1)];
                }
            }

            // Calculate current Y plot on right edge
            float pulse = (alitaCyborg != null) ? alitaCyborg.currentNormalizedPulse : 0f;
            _ecgTimer += Time.deltaTime * 3.5f;

            float baselineY = ECG_HEIGHT * 0.45f;
            float targetY = baselineY + (pulse * (ECG_HEIGHT * 0.42f));
            // Add subtle electronic noise
            targetY += Mathf.Sin(_ecgTimer * 12f) * 1.5f;
            int plotY = Mathf.Clamp(Mathf.RoundToInt(targetY), 1, ECG_HEIGHT - 2);

            Color32 bg = new Color32(4, 14, 22, 180);
            Color32 cyanLine = new Color32(0, 245, 255, 255);
            Color32 cyanGlow = new Color32(0, 180, 220, 120);

            // Clear right column
            for (int y = 0; y < ECG_HEIGHT; y++)
            {
                _ecgPixels[y * ECG_WIDTH + (ECG_WIDTH - 1)] = bg;
            }

            // Draw line point with glow
            _ecgPixels[plotY * ECG_WIDTH + (ECG_WIDTH - 1)] = cyanLine;
            if (plotY > 0) _ecgPixels[(plotY - 1) * ECG_WIDTH + (ECG_WIDTH - 1)] = cyanGlow;
            if (plotY < ECG_HEIGHT - 1) _ecgPixels[(plotY + 1) * ECG_WIDTH + (ECG_WIDTH - 1)] = cyanGlow;

            _ecgTexture.SetPixels32(_ecgPixels);
            _ecgTexture.Apply(false);

            // Update UI texts & indicators
            if (pulseIndicatorGlow != null)
            {
                pulseIndicatorGlow.color = new Color(0f, 0.95f, 1f, Mathf.Clamp01(pulse * 1.5f + 0.15f));
            }

            if (ecgPulseStatusText != null)
            {
                ecgPulseStatusText.text = pulse > 0.35f 
                    ? "> [URM CORE PULSE: HARMONIC DETECTED]" 
                    : "// [CORE FREQUENCY: 54.2 BPM DORMANT]";
                ecgPulseStatusText.color = pulse > 0.35f ? new Color(0.2f, 1f, 0.5f) : new Color(0f, 0.85f, 1f);
            }
        }

        private void UpdateScanlineEffect()
        {
            if (scanlineImage != null)
            {
                // Subtle scanline breathing
                float alpha = 0.12f + Mathf.Sin(Time.time * 2.5f) * 0.04f;
                scanlineImage.color = new Color(0f, 0.8f, 1f, alpha);
            }
        }

        // ==================== REACTIVE AUDIO SYNTHESIS ====================

        public void PlayHoverSound()
        {
            PlayProceduralTone(780f, 0.035f, 0.08f);
        }

        public void PlayClickSound()
        {
            PlayProceduralTone(1100f, 0.06f, 0.18f);
        }

        public void PlayAwakeningSurgeSound()
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * 1.6f);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                // Heavy sub-bass building into high-frequency resonant charge
                float freq = Mathf.Lerp(45f, 480f, Mathf.Pow(t / 1.6f, 2.0f));
                float amp = Mathf.Sin(t / 1.6f * Mathf.PI);
                data[i] = Mathf.Sin(2f * PI() * freq * t) * amp * 0.45f;
            }
            AudioClip clip = AudioClip.Create("Awakening_Surge", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            if (uiAudioSource != null) uiAudioSource.PlayOneShot(clip, 0.85f);
        }

        private static float PI() => Mathf.PI;

        private void PlayProceduralTone(float freq, float duration, float volume)
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Sin(t / duration * Mathf.PI);
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * envelope * volume;
            }
            AudioClip clip = AudioClip.Create("UI_Tone", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            if (uiAudioSource != null) uiAudioSource.PlayOneShot(clip, volume);
        }

        // ==================== AWAKENING & CUTSCENE HOOK ====================

        public void OnStartAwakeningClicked()
        {
            if (_isTransitioning) return;
            _isTransitioning = true;

            StartCoroutine(AwakeningSequenceRoutine());
        }

        private IEnumerator AwakeningSequenceRoutine()
        {
            // 1. Play Awakening power surge audio
            PlayAwakeningSurgeSound();

            // 2. Trigger Alita's core flare & cable sparks
            if (alitaCyborg != null)
            {
                alitaCyborg.TriggerAwakeningSurge();
            }

            // 3. Cinematic Camera zoom towards Alita's glowing heart
            if (alitaCamera != null)
            {
                alitaCamera.TriggerAwakeningCinematic(0.85f, 2.2f);
            }

            // 4. Glitch & Fade UI
            float elapsed = 0f;
            float duration = 1.8f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / duration;

                if (hudCanvasGroup != null)
                {
                    hudCanvasGroup.alpha = Mathf.Lerp(1f, 0f, progress);
                }

                if (glitchOverlayGroup != null)
                {
                    glitchOverlayGroup.blocksRaycasts = false;
                    glitchOverlayGroup.alpha = Mathf.PingPong(progress * 8f, 0.45f);
                }

                yield return null;
            }

            if (hudCanvasGroup != null) hudCanvasGroup.alpha = 0f;
            if (glitchOverlayGroup != null)
            {
                glitchOverlayGroup.alpha = 0f;
                glitchOverlayGroup.blocksRaycasts = false;
            }

            // 5. Fire Cutscene Hook Event
            onAwakeningCutsceneTriggered?.Invoke();

            // 6. Check if custom cutscene is handled
            if (hasCustomCutscene)
            {
                Debug.Log("[AlitaStartMenu] Custom cutscene triggered. Waiting for cutscene completion callback...");
                // The user's cutscene will call CompleteCutsceneAndStartGame() when finished!
            }
            else
            {
                // Default immediate transition to Cyber City gameplay
                yield return new WaitForSeconds(0.4f);
                ProceedToGameScene();
            }
        }

        /// <summary>
        /// Public method for your custom cutscene (or Timeline/VideoPlayer) to call when finished,
        /// seamlessly launching the player straight into Cyber City!
        /// </summary>
        public void CompleteCutsceneAndStartGame()
        {
            ProceedToGameScene();
        }

        private void ProceedToGameScene()
        {
            var hubController = FindAnyObjectByType<SyntyCombined.MainMenuController>();
            if (hubController != null)
            {
                hubController.LoadSceneDirect(gameplaySceneName);
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(gameplaySceneName);
            }
        }

        /// <summary>
        /// Ensures all texts and interactive controls in the Cyberpunk HUD are precisely
        /// aligned, padded, and safely contained within their bounding frames.
        /// Prevents edge clipping or text spilling outside panel borders.
        /// </summary>
        public void EnforceHUDAlignment()
        {
            Transform root = mainHubContainer != null ? mainHubContainer.transform : transform;

            // 1. Top HUD Bar
            Transform topBar = FindChildRecursive(root, "HUD_TopBar");
            if (topBar != null)
            {
                AlignText(topBar, "Text_TopLeft", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(850f, 40f), TextAlignmentOptions.MidlineLeft);
                AlignText(topBar, "Text_TopRight", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-40f, 0f), new Vector2(850f, 40f), TextAlignmentOptions.MidlineRight);
            }

            // 2. Bottom HUD Bar
            Transform btmBar = FindChildRecursive(root, "HUD_BottomBar");
            if (btmBar != null)
            {
                AlignText(btmBar, "Text_BtmLeft", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(850f, 40f), TextAlignmentOptions.MidlineLeft);
                AlignText(btmBar, "Text_BtmRight", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-40f, 0f), new Vector2(850f, 40f), TextAlignmentOptions.MidlineRight);
            }

            // 3. Left Panel (Panel_LeftActions)
            Transform leftPanel = FindChildRecursive(root, "Panel_LeftActions");
            if (leftPanel != null)
            {
                AlignText(leftPanel, "Text_ProtocolBadge", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(5f, 345f), new Vector2(550f, 32f), TextAlignmentOptions.MidlineLeft);
                AlignText(leftPanel, "Text_MovieTitle", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(5f, 290f), new Vector2(550f, 56f), TextAlignmentOptions.MidlineLeft, true, 26f, 40f);
                AlignText(leftPanel, "Text_Subtitle", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(5f, 240f), new Vector2(550f, 30f), TextAlignmentOptions.MidlineLeft);
                AlignText(leftPanel, "Text_ControlTip", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(5f, -320f), new Vector2(550f, 30f), TextAlignmentOptions.MidlineLeft);

                AlignRect(leftPanel, "Btn_StartAwakening", new Vector2(5f, 120f), new Vector2(550f, 72f));
                AlignRect(leftPanel, "Btn_SectorMap", new Vector2(5f, 35f), new Vector2(550f, 56f));
                AlignRect(leftPanel, "Btn_Settings", new Vector2(5f, -45f), new Vector2(550f, 56f));
                AlignRect(leftPanel, "Btn_Controls", new Vector2(5f, -125f), new Vector2(550f, 56f));
                AlignRect(leftPanel, "Btn_Quit", new Vector2(5f, -205f), new Vector2(550f, 56f));
            }

            // 4. Right Panel (Panel_RightTelemetry)
            Transform rightPanel = FindChildRecursive(root, "Panel_RightTelemetry");
            if (rightPanel != null)
            {
                AlignText(rightPanel, "Text_DiagHeader", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-5f, 345f), new Vector2(480f, 36f), TextAlignmentOptions.MidlineLeft);
                AlignText(rightPanel, "Text_LocHeader", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-5f, 312f), new Vector2(480f, 26f), TextAlignmentOptions.MidlineLeft);
                AlignRect(rightPanel, "Box_ECGMonitor", new Vector2(-5f, 210f), new Vector2(480f, 110f));
                AlignText(rightPanel, "Text_PulseStatus", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-5f, 125f), new Vector2(480f, 30f), TextAlignmentOptions.MidlineLeft);
                AlignText(rightPanel, "Text_TelemetrySpecs", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-5f, -10f), new Vector2(480f, 190f), TextAlignmentOptions.TopLeft, false, 0, 0, true);

                AlignRect(rightPanel, "Box_Quote", new Vector2(-5f, -185f), new Vector2(480f, 115f));
                Transform quoteBox = FindChildRecursive(rightPanel, "Box_Quote");
                if (quoteBox != null)
                {
                    AlignRect(quoteBox, "Border", new Vector2(-236f, 0f), new Vector2(4f, 115f));
                    AlignText(quoteBox, "Text_Quote", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(12f, 0f), new Vector2(440f, 100f), TextAlignmentOptions.TopLeft, false, 0, 0, true);
                }
            }
        }

        private static void AlignText(Transform parent, string childName, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size, TextAlignmentOptions align, bool autoSize = false, float minSize = 0, float maxSize = 0, bool wordWrap = false)
        {
            Transform t = FindChildRecursive(parent, childName);
            if (t == null) return;
            RectTransform rt = t.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = anchorMin;
                rt.anchorMax = anchorMax;
                rt.pivot = pivot;
                rt.anchoredPosition = pos;
                rt.sizeDelta = size;
            }
            TMP_Text tmp = t.GetComponent<TMP_Text>();
            if (tmp != null)
            {
                tmp.alignment = align;
                if (autoSize)
                {
                    tmp.enableAutoSizing = true;
                    tmp.fontSizeMin = minSize;
                    tmp.fontSizeMax = maxSize;
                }
                tmp.textWrappingMode = wordWrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
                tmp.overflowMode = wordWrap ? TextOverflowModes.Truncate : TextOverflowModes.Ellipsis;
            }
        }

        private static void AlignRect(Transform parent, string childName, Vector2 pos, Vector2 size)
        {
            Transform t = FindChildRecursive(parent, childName);
            if (t == null) return;
            RectTransform rt = t.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = pos;
                rt.sizeDelta = size;
            }
        }

        private static Transform FindChildRecursive(Transform parent, string name)
        {
            if (parent == null) return null;
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                var f = FindChildRecursive(parent.GetChild(i), name);
                if (f != null) return f;
            }
            return null;
        }
    }
}
