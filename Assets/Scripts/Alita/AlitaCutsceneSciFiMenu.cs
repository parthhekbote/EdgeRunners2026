using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace Alita
{
    /// <summary>
    /// Master controller for the AAA Cyberpunk Glassmorphism Title Menu in the Scrapyard Discovery scene.
    /// Provides:
    /// - High-tech reactive buttons with chamfered sci-fi borders, scale dampening, and inner neon glow
    /// - Real-time animated ECG heartbeat monitor synced with Alita's dormant Martian antimatter core
    /// - Pulsing status beacon and atmospheric telemetry readouts
    /// - Procedural sci-fi UI synthesizer (hover chirps, click laser triggers)
    /// - Keyboard/gamepad shortcut handling (SPACE / ENTER to begin journey)
    /// </summary>
    public class AlitaCutsceneSciFiMenu : MonoBehaviour
    {
        public static AlitaCutsceneSciFiMenu Instance { get; private set; }

        [Header("Canvas & Group")]
        public CanvasGroup canvasGroup;

        [Header("Buttons")]
        public Button btnStartJourney;
        public Button btnClinicAwakening;
        public Button btnQuit;

        [Header("Button Visual Elements")]
        public Image startJourneyGlow;
        public Image clinicAwakeningGlow;
        public Image quitGlow;

        [Header("ECG Core Monitor")]
        public RawImage ecgMonitorImage;
        public TMP_Text ecgStatusText;
        private Texture2D _ecgTexture;
        private Color32[] _ecgPixels;
        private const int ECG_WIDTH = 256;
        private const int ECG_HEIGHT = 64;
        private float _ecgTimer = 0f;

        [Header("Pulsing Status Beacon")]
        public Image beaconIndicator;

        [Header("Bottom Prompt")]
        public TMP_Text keyPromptText;

        [Header("Audio")]
        public AudioSource uiAudioSource;
        private AudioClip _hoverSound;
        private AudioClip _clickSound;

        private void Awake()
        {
            Instance = this;
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();

            InitAudio();
            InitECGTexture();
            SetupButtons();
        }

        private void InitAudio()
        {
            if (uiAudioSource == null)
            {
                uiAudioSource = gameObject.GetComponent<AudioSource>();
                if (uiAudioSource == null) uiAudioSource = gameObject.AddComponent<AudioSource>();
                uiAudioSource.playOnAwake = false;
                uiAudioSource.spatialBlend = 0f; // Pure 2D UI sound
            }

            // High-tech electronic hover blip
            int hoverSamples = 1400;
            _hoverSound = AudioClip.Create("UI_Hover_Blip", hoverSamples, 1, 44100, false);
            float[] hoverData = new float[hoverSamples];
            for (int i = 0; i < hoverSamples; i++)
            {
                float t = (float)i / hoverSamples;
                float freq = Mathf.Lerp(1200f, 1800f, t);
                hoverData[i] = Mathf.Sin(t * freq * Mathf.PI * 2f) * Mathf.Exp(-t * 28f) * 0.45f;
            }
            _hoverSound.SetData(hoverData, 0);

            // Resonant sci-fi confirmation click
            int clickSamples = 2200;
            _clickSound = AudioClip.Create("UI_Click_Laser", clickSamples, 1, 44100, false);
            float[] clickData = new float[clickSamples];
            for (int i = 0; i < clickSamples; i++)
            {
                float t = (float)i / clickSamples;
                float tone = Mathf.Sin(t * 880f * Mathf.PI * 2f) * 0.7f + Mathf.Sin(t * 1760f * Mathf.PI * 2f) * 0.3f;
                clickData[i] = tone * Mathf.Exp(-t * 18f) * 0.75f;
            }
            _clickSound.SetData(clickData, 0);
        }

        private void InitECGTexture()
        {
            if (ecgMonitorImage == null) return;

            _ecgTexture = new Texture2D(ECG_WIDTH, ECG_HEIGHT, TextureFormat.RGBA32, false);
            _ecgTexture.filterMode = FilterMode.Bilinear;
            _ecgTexture.wrapMode = TextureWrapMode.Clamp;
            _ecgPixels = new Color32[ECG_WIDTH * ECG_HEIGHT];

            Color32 bg = new Color32(2, 8, 14, 210);
            for (int i = 0; i < _ecgPixels.Length; i++) _ecgPixels[i] = bg;
            _ecgTexture.SetPixels32(_ecgPixels);
            _ecgTexture.Apply();

            ecgMonitorImage.texture = _ecgTexture;
        }

        private void SetupButtons()
        {
            if (btnStartJourney != null)
            {
                btnStartJourney.onClick.RemoveAllListeners();
                btnStartJourney.onClick.AddListener(() =>
                {
                    PlayClickSound();
                    if (AlitaCutsceneController.Instance != null)
                    {
                        AlitaCutsceneController.Instance.StartJourney();
                    }
                });
                AddHoverEffects(btnStartJourney, startJourneyGlow, new Color(0f, 0.95f, 1f, 0.35f));
            }

            if (btnClinicAwakening != null)
            {
                btnClinicAwakening.onClick.RemoveAllListeners();
                btnClinicAwakening.onClick.AddListener(() =>
                {
                    PlayClickSound();
                    if (AlitaCutsceneController.Instance != null)
                    {
                        AlitaCutsceneController.Instance.LoadClinicAwakeningScene();
                    }
                });
                AddHoverEffects(btnClinicAwakening, clinicAwakeningGlow, new Color(1.0f, 0.45f, 0.08f, 0.35f));
            }

            if (btnQuit != null)
            {
                btnQuit.onClick.RemoveAllListeners();
                btnQuit.onClick.AddListener(() =>
                {
                    PlayClickSound();
                    if (AlitaCutsceneController.Instance != null)
                    {
                        AlitaCutsceneController.Instance.QuitGame();
                    }
                });
                AddHoverEffects(btnQuit, quitGlow, new Color(0.6f, 0.75f, 0.9f, 0.25f));
            }
        }

        private void AddHoverEffects(Button btn, Image glowImg, Color targetGlowColor)
        {
            if (btn == null) return;

            var trigger = btn.gameObject.GetComponent<EventTrigger>() ?? btn.gameObject.AddComponent<EventTrigger>();
            trigger.triggers.Clear();

            // Pointer Enter
            var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enterEntry.callback.AddListener((_) =>
            {
                PlayHoverSound();
                StartCoroutine(ScaleRoutine(btn.transform, 1.035f, 0.12f));
                if (glowImg != null) StartCoroutine(FadeGlowRoutine(glowImg, targetGlowColor, 0.12f));
            });
            trigger.triggers.Add(enterEntry);

            // Pointer Exit
            var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exitEntry.callback.AddListener((_) =>
            {
                StartCoroutine(ScaleRoutine(btn.transform, 1.0f, 0.12f));
                if (glowImg != null) StartCoroutine(FadeGlowRoutine(glowImg, new Color(targetGlowColor.r, targetGlowColor.g, targetGlowColor.b, 0f), 0.15f));
            });
            trigger.triggers.Add(exitEntry);
        }

        private IEnumerator ScaleRoutine(Transform tr, float targetScale, float duration)
        {
            if (tr == null) yield break;
            Vector3 start = tr.localScale;
            Vector3 target = Vector3.one * targetScale;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (tr == null) yield break;
                elapsed += Time.unscaledDeltaTime;
                tr.localScale = Vector3.Lerp(start, target, elapsed / duration);
                yield return null;
            }
            if (tr != null) tr.localScale = target;
        }

        private IEnumerator FadeGlowRoutine(Image img, Color targetColor, float duration)
        {
            if (img == null) yield break;
            Color start = img.color;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (img == null) yield break;
                elapsed += Time.unscaledDeltaTime;
                img.color = Color.Lerp(start, targetColor, elapsed / duration);
                yield return null;
            }
            if (img != null) img.color = targetColor;
        }

        private void Update()
        {
            UpdateECGWaveform();
            UpdateBeaconPulse();
            UpdateKeyPromptBreathing();
        }

        private void UpdateECGWaveform()
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

            // Calculate current heartbeat pulse (simulate 52 BPM faint stasis heartbeat)
            _ecgTimer += Time.deltaTime * 3.2f;
            float pulseCycle = Mathf.Repeat(_ecgTimer, 2.4f);

            float spike = 0f;
            if (pulseCycle > 0.4f && pulseCycle < 0.52f)
            {
                spike = -0.3f; // Q wave dip
            }
            else if (pulseCycle >= 0.52f && pulseCycle < 0.68f)
            {
                spike = 1.0f; // R wave sharp spike
            }
            else if (pulseCycle >= 0.68f && pulseCycle < 0.78f)
            {
                spike = -0.4f; // S wave drop
            }
            else if (pulseCycle >= 0.95f && pulseCycle < 1.15f)
            {
                spike = 0.28f; // T wave recovery dome
            }

            float baselineY = ECG_HEIGHT * 0.46f;
            float targetY = baselineY + (spike * (ECG_HEIGHT * 0.38f)) + (Mathf.Sin(_ecgTimer * 10f) * 1.2f);
            int plotY = Mathf.Clamp(Mathf.RoundToInt(targetY), 1, ECG_HEIGHT - 2);

            Color32 bg = new Color32(2, 8, 14, 210);
            Color32 cyanLine = new Color32(0, 245, 255, 255);
            Color32 cyanGlow = new Color32(0, 190, 230, 130);

            // Clear rightmost column
            for (int y = 0; y < ECG_HEIGHT; y++)
            {
                _ecgPixels[y * ECG_WIDTH + (ECG_WIDTH - 1)] = bg;
            }

            // Draw current point with vertical glow
            _ecgPixels[plotY * ECG_WIDTH + (ECG_WIDTH - 1)] = cyanLine;
            if (plotY > 0) _ecgPixels[(plotY - 1) * ECG_WIDTH + (ECG_WIDTH - 1)] = cyanGlow;
            if (plotY < ECG_HEIGHT - 1) _ecgPixels[(plotY + 1) * ECG_WIDTH + (ECG_WIDTH - 1)] = cyanGlow;

            _ecgTexture.SetPixels32(_ecgPixels);
            _ecgTexture.Apply(false);

            if (ecgStatusText != null)
            {
                bool isSpike = Mathf.Abs(spike) > 0.2f;
                ecgStatusText.text = isSpike
                    ? "<color=#00F5FF>HARMONIC SYNC: 52.4 BPM</color> // <color=#33FF88>CORE WARM</color>"
                    : "ANTIMATTER HEART CORE // BIO-STASIS STABLE";
            }
        }

        private void UpdateBeaconPulse()
        {
            if (beaconIndicator != null)
            {
                float pulse = Mathf.PingPong(Time.time * 2.5f, 0.6f) + 0.4f;
                beaconIndicator.color = new Color(0f, 0.95f, 1f, pulse);
            }
        }

        private void UpdateKeyPromptBreathing()
        {
            if (keyPromptText != null)
            {
                float alpha = Mathf.PingPong(Time.time * 1.8f, 0.35f) + 0.65f;
                keyPromptText.alpha = alpha;
            }
        }

        public void PlayHoverSound()
        {
            if (uiAudioSource != null && _hoverSound != null)
            {
                uiAudioSource.PlayOneShot(_hoverSound, 0.45f);
            }
        }

        public void PlayClickSound()
        {
            if (uiAudioSource != null && _clickSound != null)
            {
                uiAudioSource.PlayOneShot(_clickSound, 0.85f);
            }
        }
    }
}
