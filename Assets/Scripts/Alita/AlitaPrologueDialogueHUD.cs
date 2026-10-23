using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Alita
{
    /// <summary>
    /// Cyberpunk 2077 / TRON inspired Holographic Comms Dialogue HUD
    /// for Dr. Dyson Ido's Iron City Walkthrough Prologue.
    /// Features:
    /// - Emissive cyber frame with frosted dark glass backdrop
    /// - Holo-comms Doctor Ido portrait with cyan glitch scanline overlay
    /// - Smooth animated typewriter text with cyber audio chirps
    /// - Interactive landmark prompt badge [E] and [SPACE] Continue prompt
    /// </summary>
    public class AlitaPrologueDialogueHUD : MonoBehaviour
    {
        public static AlitaPrologueDialogueHUD Instance { get; private set; }

        [Header("UI References")]
        public Canvas canvas;
        public CanvasGroup canvasGroup;
        public Image portraitImage;
        public TextMeshProUGUI speakerNameText;
        public TextMeshProUGUI speakerTitleText;
        public TextMeshProUGUI dialogueBodyText;
        public GameObject promptContainer;
        public TextMeshProUGUI promptText;
        public TextMeshProUGUI skipKeyText;
        public AudioSource audioSource;

        [Header("Tuning")]
        public float typeSpeed = 0.022f; // Seconds per character
        public Sprite defaultDoctorPortrait;
        public Sprite defaultAlitaPortrait;

        private Coroutine _typewriterCoroutine;
        private Action _onDialogueFinished;
        private bool _isTyping = false;
        private string _currentFullText = "";
        private PrologueDialogueBeat _activeBeat;
        private int _currentLineIndex = 0;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnsureCanvasAndComponents();
            HideImmediate();
        }

        public void EnsureCanvasAndComponents()
        {
            if (canvas == null)
            {
                canvas = GetComponent<Canvas>();
                if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 250;

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
                audioSource.spatialBlend = 0f; // 2D UI
                audioSource.volume = 0.65f;
            }

#if UNITY_EDITOR
            if (defaultAlitaPortrait == null)
            {
                defaultAlitaPortrait = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Characters/Alita/Textures/T_Alita_HoloPortrait.png");
            }
            if (defaultDoctorPortrait == null)
            {
                defaultDoctorPortrait = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Characters/Doctor/Textures/T_Doctor_Ido_HoloPortrait.png");
            }
#endif

            if (portraitImage == null || dialogueBodyText == null)
            {
                BuildVisualHierarchy();
            }
        }

        private void BuildVisualHierarchy()
        {
            // Root panel (Bottom of screen)
            GameObject panelGO = new GameObject("Dialogue_Panel_Root");
            panelGO.transform.SetParent(transform, false);
            var panelRect = panelGO.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0f, 38f);
            panelRect.sizeDelta = new Vector2(1180f, 210f);

            var bgImg = panelGO.AddComponent<Image>();
            bgImg.color = new Color(0.015f, 0.022f, 0.035f, 0.92f); // Dark obsidian cyber glass

            // Neon Cyan Top & Bottom Border Accents
            GameObject borderTop = new GameObject("Border_Top_Cyan");
            borderTop.transform.SetParent(panelGO.transform, false);
            var btRect = borderTop.AddComponent<RectTransform>();
            btRect.anchorMin = new Vector2(0f, 1f);
            btRect.anchorMax = new Vector2(1f, 1f);
            btRect.sizeDelta = new Vector2(0f, 2.5f);
            btRect.anchoredPosition = new Vector2(0f, -1.25f);
            var btImg = borderTop.AddComponent<Image>();
            btImg.color = new Color(0f, 0.90f, 1f, 0.95f); // Glowing Cyan

            // Text Content Root (Full width subtitles - no image)
            GameObject contentGO = new GameObject("Text_Content_Root");
            contentGO.transform.SetParent(panelGO.transform, false);
            var cRect = contentGO.AddComponent<RectTransform>();
            cRect.anchorMin = new Vector2(0f, 0f);
            cRect.anchorMax = new Vector2(1f, 1f);
            cRect.offsetMin = new Vector2(32f, 15f);
            cRect.offsetMax = new Vector2(-32f, -15f);

            // Speaker Header
            GameObject nameGO = new GameObject("Speaker_Name");
            nameGO.transform.SetParent(contentGO.transform, false);
            var nameRect = nameGO.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(0f, 1f);
            nameRect.anchoredPosition = new Vector2(0f, 0f);
            nameRect.sizeDelta = new Vector2(0f, 26f);

            speakerNameText = nameGO.AddComponent<TextMeshProUGUI>();
            speakerNameText.text = "DR. DYSON IDO";
            speakerNameText.fontSize = 20f;
            speakerNameText.fontStyle = FontStyles.Bold;
            speakerNameText.color = new Color(0.1f, 0.95f, 1.0f); // Bright cyber cyan

            // Speaker Subtitle / Title
            GameObject titleGO = new GameObject("Speaker_Title");
            titleGO.transform.SetParent(contentGO.transform, false);
            var titleRect = titleGO.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0f, 1f);
            titleRect.anchoredPosition = new Vector2(175f, 0f);
            titleRect.sizeDelta = new Vector2(0f, 26f);

            speakerTitleText = titleGO.AddComponent<TextMeshProUGUI>();
            speakerTitleText.text = "// HUNTER-WARRIOR & CYBER-SURGEON";
            speakerTitleText.fontSize = 14f;
            speakerTitleText.fontStyle = FontStyles.Normal;
            speakerTitleText.color = new Color(0.85f, 0.65f, 0.2f); // Warm TRON Amber

            // Dialogue Body Text
            GameObject bodyGO = new GameObject("Dialogue_Body_Text");
            bodyGO.transform.SetParent(contentGO.transform, false);
            var bodyRect = bodyGO.AddComponent<RectTransform>();
            bodyRect.anchorMin = new Vector2(0f, 0f);
            bodyRect.anchorMax = new Vector2(1f, 1f);
            bodyRect.offsetMin = new Vector2(0f, 32f);
            bodyRect.offsetMax = new Vector2(0f, -32f);

            dialogueBodyText = bodyGO.AddComponent<TextMeshProUGUI>();
            dialogueBodyText.text = "";
            dialogueBodyText.fontSize = 17f;
            dialogueBodyText.color = new Color(0.92f, 0.94f, 0.98f);
            dialogueBodyText.lineSpacing = 12f;

            // Bottom Footer Row (Prompts)
            promptContainer = new GameObject("Prompts_Footer_Row");
            promptContainer.transform.SetParent(contentGO.transform, false);
            var fRect = promptContainer.AddComponent<RectTransform>();
            fRect.anchorMin = new Vector2(0f, 0f);
            fRect.anchorMax = new Vector2(1f, 0f);
            fRect.pivot = new Vector2(0f, 0f);
            fRect.sizeDelta = new Vector2(0f, 26f);
            fRect.anchoredPosition = Vector2.zero;

            // Interactive Landmark Prompt (Left Footer)
            GameObject pTextGO = new GameObject("Interactive_Prompt_Text");
            pTextGO.transform.SetParent(promptContainer.transform, false);
            var ptRect = pTextGO.AddComponent<RectTransform>();
            ptRect.anchorMin = new Vector2(0f, 0f);
            ptRect.anchorMax = new Vector2(0.7f, 1f);
            promptText = pTextGO.AddComponent<TextMeshProUGUI>();
            promptText.text = "[E] GAZE UP AT ZALEM";
            promptText.fontSize = 14f;
            promptText.fontStyle = FontStyles.Bold;
            promptText.color = new Color(1.0f, 0.85f, 0.2f); // Glowing Amber

            // Skip Prompt (Right Footer)
            GameObject sTextGO = new GameObject("Skip_Key_Text");
            sTextGO.transform.SetParent(promptContainer.transform, false);
            var stRect = sTextGO.AddComponent<RectTransform>();
            stRect.anchorMin = new Vector2(0.7f, 0f);
            stRect.anchorMax = new Vector2(1f, 1f);
            skipKeyText = sTextGO.AddComponent<TextMeshProUGUI>();
            skipKeyText.alignment = TextAlignmentOptions.Right;
            skipKeyText.text = "[SPACE] CONTINUE";
            skipKeyText.fontSize = 13f;
            skipKeyText.color = new Color(0.6f, 0.65f, 0.75f, 0.85f);
        }

        public void DisplayBeat(PrologueDialogueBeat beat, Action onFinished = null)
        {
            if (beat == null) return;

            _activeBeat = beat;
            _onDialogueFinished = onFinished;
            _currentLineIndex = 0;

            // Interactive Look-At Prompt badge
            if (promptContainer != null)
            {
                promptContainer.SetActive(true);
                if (promptText != null)
                {
                    promptText.gameObject.SetActive(beat.hasInteractiveLookAt);
                    promptText.text = beat.lookAtPromptText;
                }
            }

            // Show HUD
            StartCoroutine(FadeHUD(1f, 0.25f));

            if (beat.dialogueLines != null && beat.dialogueLines.Count > 0)
            {
                DisplayLine(beat.dialogueLines[0]);
            }
            else
            {
                DialogueLine fallback = new DialogueLine(beat.speakerName, beat.speakerTitle, beat.dialogueText);
                fallback.voiceClip = beat.voiceClip;
                DisplayLine(fallback);
            }
        }

        private void DisplayLine(DialogueLine line)
        {
            if (line == null) return;

            _currentFullText = line.text;

            bool isAlita = !string.IsNullOrEmpty(line.speakerName) && line.speakerName.ToUpper().Contains("ALITA");

            if (speakerNameText != null)
            {
                speakerNameText.text = line.speakerName;
                speakerNameText.color = isAlita ? new Color(0.1f, 0.95f, 1.0f) : new Color(1.0f, 0.78f, 0.28f);
            }

            if (speakerTitleText != null)
            {
                speakerTitleText.text = $"// {line.speakerTitle}";
                speakerTitleText.color = isAlita ? new Color(0.2f, 0.85f, 0.95f) : new Color(0.85f, 0.65f, 0.2f);
            }

            if (portraitImage != null)
            {
                if (line.customPortrait != null)
                {
                    portraitImage.sprite = line.customPortrait;
                }
                else if (isAlita)
                {
                    portraitImage.sprite = defaultAlitaPortrait != null ? defaultAlitaPortrait : defaultDoctorPortrait;
                }
                else
                {
                    portraitImage.sprite = defaultDoctorPortrait;
                }
            }

            // Voice audio playback
            if (line.voiceClip != null && audioSource != null)
            {
                audioSource.clip = line.voiceClip;
                audioSource.Play();
            }

            // Start Typewriter
            if (_typewriterCoroutine != null) StopCoroutine(_typewriterCoroutine);
            _typewriterCoroutine = StartCoroutine(TypewriterRoutine(line.text));
        }

        private IEnumerator TypewriterRoutine(string fullText)
        {
            _isTyping = true;
            if (dialogueBodyText != null) dialogueBodyText.text = "";

            for (int i = 0; i <= fullText.Length; i++)
            {
                if (dialogueBodyText != null)
                {
                    dialogueBodyText.text = fullText.Substring(0, i);
                }

                // Procedural audio chirp every 3 characters
                if (i % 3 == 0 && audioSource != null && audioSource.clip == null)
                {
                    PlayProceduralChirp();
                }

                yield return new WaitForSeconds(typeSpeed);
            }

            _isTyping = false;
        }

        public void CompleteCurrentTextInstant()
        {
            if (_isTyping)
            {
                if (_typewriterCoroutine != null) StopCoroutine(_typewriterCoroutine);
                _isTyping = false;
                if (dialogueBodyText != null) dialogueBodyText.text = _currentFullText;
            }
            else
            {
                // Advance to next line if available in the current conversational beat
                if (_activeBeat != null && _activeBeat.dialogueLines != null && _currentLineIndex + 1 < _activeBeat.dialogueLines.Count)
                {
                    _currentLineIndex++;
                    DisplayLine(_activeBeat.dialogueLines[_currentLineIndex]);
                }
                else
                {
                    // Advance to next dialogue beat / walk
                    _onDialogueFinished?.Invoke();
                }
            }
        }

        public void Hide(float fadeDuration = 0.3f)
        {
            if (_typewriterCoroutine != null) StopCoroutine(_typewriterCoroutine);
            StartCoroutine(FadeHUD(0f, fadeDuration));
        }

        public void HideImmediate()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }

        private IEnumerator FadeHUD(float targetAlpha, float duration)
        {
            if (canvasGroup == null) yield break;

            float startAlpha = canvasGroup.alpha;
            float elapsed = 0f;

            canvasGroup.interactable = targetAlpha > 0.5f;
            canvasGroup.blocksRaycasts = targetAlpha > 0.5f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
                yield return null;
            }
            canvasGroup.alpha = targetAlpha;
        }

        private void PlayProceduralChirp()
        {
            // Gentle high-tech radio frequency blip
            float pitch = UnityEngine.Random.Range(1.8f, 2.2f);
            audioSource.pitch = pitch;
            // Play a 12ms synthesized click
            audioSource.PlayOneShot(GetOrCreateClickClip());
        }

        private static AudioClip _clickClip;
        private static AudioClip GetOrCreateClickClip()
        {
            if (_clickClip == null)
            {
                int samples = 512;
                _clickClip = AudioClip.Create("CyberChirp", samples, 1, 44100, false);
                float[] data = new float[samples];
                for (int i = 0; i < samples; i++)
                {
                    float t = i / (float)samples;
                    data[i] = Mathf.Sin(t * Mathf.PI * 16f) * (1f - t) * 0.15f;
                }
                _clickClip.SetData(data, 0);
            }
            return _clickClip;
        }
    }
}
