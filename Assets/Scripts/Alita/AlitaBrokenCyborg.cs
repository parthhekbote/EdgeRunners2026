using System.Collections;
using UnityEngine;

namespace Alita
{
    /// <summary>
    /// Controls the broken Alita cyborg in the scrapyard discovery scene.
    /// Manages the pulsating URM antimatter core, severed cable sparks,
    /// heartbeat audio, and interactive movie diagnostic scan.
    /// </summary>
    [SelectionBase]
    public class AlitaBrokenCyborg : MonoBehaviour
    {
        [Header("URM Antimatter Core")]
        public Renderer coreRenderer;
        public Light coreLight;
        public Color coreColor = new Color(0.1f, 0.85f, 1.0f);
        public float baseCoreIntensity = 2.0f;
        public float pulseSpeed = 1.0f; // Heartbeat tempo (~55 BPM)

        [Header("Severed Cables & Damage FX")]
        public ParticleSystem[] severedCableSparks;
        public Transform severedSpineRoot;
        public Transform severedArmRoot;

        [Header("Heartbeat & Audio")]
        public AudioSource audioSource;
        public AudioClip heartbeatClip;

        [Header("Cinematic State")]
        public bool isExamining = false;
        public float examineProgress = 0f;
        public bool isSurgingAwakening = false;
        public bool showLegacyGUI = false; // Disabled by default in favor of modern Cyberpunk Start Menu HUD

        /// <summary>
        /// Current normalized pulse value (0..1) of the URM core heartbeat, accessible by UI telemetry.
        /// </summary>
        public float currentNormalizedPulse { get; private set; }

        private Material _coreMaterial;
        private float _heartbeatTimer = 0f;
        private bool _hasBeatOnce = false;

        private void Awake()
        {
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 0.6f;
                audioSource.playOnAwake = false;
            }

            if (coreRenderer != null)
            {
                _coreMaterial = coreRenderer.material;
                _coreMaterial.EnableKeyword("_EMISSION");
            }
        }

        private void Update()
        {
            AnimateCoreHeartbeat();
            HandleInput();
        }

        private void AnimateCoreHeartbeat()
        {
            // Movie-authentic URM core heartbeat: double-beat rhythm ("lub-dub... lub-dub...")
            _heartbeatTimer += Time.deltaTime * pulseSpeed;
            float cycle = _heartbeatTimer % 1.2f;

            float pulse = 0f;
            if (cycle < 0.15f)
            {
                // First beat (lub)
                pulse = Mathf.Sin((cycle / 0.15f) * Mathf.PI);
                if (!_hasBeatOnce)
                {
                    PlayHeartbeatSound(1.0f, 0.95f);
                    _hasBeatOnce = true;
                }
            }
            else if (cycle >= 0.22f && cycle < 0.35f)
            {
                // Second beat (dub)
                pulse = Mathf.Sin(((cycle - 0.22f) / 0.13f) * Mathf.PI) * 0.75f;
            }
            else
            {
                _hasBeatOnce = false;
            }

            // If examining or surging, flare the core to full brightness
            float examineFlare = isExamining ? Mathf.PingPong(Time.time * 2f, 1.5f) : 0f;
            float surgeFlare = isSurgingAwakening ? 3.5f : 0f;
            float finalIntensity = baseCoreIntensity + (pulse * 2.8f) + examineFlare + surgeFlare;
            currentNormalizedPulse = Mathf.Clamp01(pulse + (surgeFlare > 0f ? 0.8f : 0f));

            if (coreLight != null)
            {
                coreLight.intensity = finalIntensity;
                coreLight.color = coreColor;
            }

            if (_coreMaterial != null)
            {
                Color emission = coreColor * (finalIntensity * 1.5f);
                _coreMaterial.SetColor("_EmissionColor", emission);
            }
        }

        /// <summary>
        /// Triggers a surge of power in Alita's core when awakening (sparks fly and core flares brightly).
        /// </summary>
        public void TriggerAwakeningSurge()
        {
            isSurgingAwakening = true;
            pulseSpeed = 2.2f; // Fast, racing heartbeat
            if (severedCableSparks != null)
            {
                foreach (var ps in severedCableSparks)
                {
                    if (ps != null) ps.Play();
                }
            }
        }

        private void PlayHeartbeatSound(float volume, float pitch)
        {
            // Heart noise sound disabled per user request
            return;
        }

        private void CreateProceduralHeartbeat(float volume)
        {
            // Low-frequency heartbeat synth
            int sampleRate = 44100;
            int samples = (int)(sampleRate * 0.12f);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float freq = Mathf.Lerp(65f, 40f, t / 0.12f);
                float envelope = Mathf.Sin(t / 0.12f * Mathf.PI);
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * envelope * volume;
            }
            AudioClip clip = AudioClip.Create("Heartbeat_Pulse", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            audioSource.PlayOneShot(clip);
        }

        private void HandleInput()
        {
            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
            {
                TriggerDiagnosticScan();
            }
        }

        public void TriggerDiagnosticScan()
        {
            StopAllCoroutines();
            StartCoroutine(DiagnosticScanRoutine());
        }

        private IEnumerator DiagnosticScanRoutine()
        {
            isExamining = true;
            examineProgress = 0f;

            // Trigger sparks at severed cables
            if (severedCableSparks != null)
            {
                foreach (var ps in severedCableSparks)
                {
                    if (ps != null) ps.Play();
                }
            }

            // Flare core and simulate cybernetic diagnostic
            float duration = 6.0f;
            while (examineProgress < 1.0f)
            {
                examineProgress += Time.deltaTime / duration;
                yield return null;
            }

            isExamining = false;
        }

        private void OnGUI()
        {
            if (!showLegacyGUI) return;

            int sw = Screen.width;
            int sh = Screen.height;

            // 1. Cinematic 2.39:1 Letterbox Bars (Movie Aspect Ratio)
            int barHeight = Mathf.Max(35, (int)(sh * 0.08f));
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, sw, barHeight), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, sh - barHeight, sw, barHeight), Texture2D.whiteTexture);

            // 2. Movie Title & Prompt
            GUI.color = Color.white;
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Clamp(sw / 75, 14, 22),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            titleStyle.normal.textColor = new Color(0.85f, 0.95f, 1f, 0.9f);

            GUI.Label(new Rect(0, barHeight + 8, sw, 30), "ALITA: BATTLE ANGEL — SCRAPYARD DISCOVERY", titleStyle);

            GUIStyle promptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Clamp(sw / 90, 11, 16),
                alignment = TextAnchor.MiddleCenter
            };
            promptStyle.normal.textColor = new Color(0f, 0.85f, 1f, 0.85f);

            string promptText = isExamining
                ? "▶ [SCAN IN PROGRESS] RE-ENGAGING URM QUANTUM POWER CORE..."
                : "▶ Press [E] or [SPACE] to Scan & Examine Cyborg Core";
            GUI.Label(new Rect(0, sh - barHeight - 32, sw, 28), promptText, promptStyle);

            // 3. Cybernetic Diagnostic Readout Overlay
            if (isExamining)
            {
                DrawDiagnosticHUD(sw, sh);
            }
        }

        private void DrawDiagnosticHUD(int sw, int sh)
        {
            float boxWidth = Mathf.Clamp(sw * 0.38f, 360f, 520f);
            float boxHeight = 220f;
            float boxX = sw * 0.04f;
            float boxY = sh * 0.18f;

            // Dark semi-transparent cybernetic box
            GUI.color = new Color(0.02f, 0.05f, 0.10f, 0.85f);
            GUI.DrawTexture(new Rect(boxX, boxY, boxWidth, boxHeight), Texture2D.whiteTexture);

            // Cyan border outline
            GUI.color = new Color(0f, 0.85f, 1f, 0.9f);
            GUI.DrawTexture(new Rect(boxX, boxY, boxWidth, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(boxX, boxY + boxHeight - 2, boxWidth, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(boxX, boxY, 2, boxHeight), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(boxX + boxWidth - 2, boxY, 2, boxHeight), Texture2D.whiteTexture);

            GUIStyle hudHeader = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };
            hudHeader.normal.textColor = new Color(0f, 0.9f, 1f);

            GUIStyle hudText = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Normal
            };
            hudText.normal.textColor = Color.white;

            float lineY = boxY + 12f;
            GUI.Label(new Rect(boxX + 16, lineY, boxWidth - 32, 22), "◈ URM CYBERNETIC DIAGNOSTIC [ZALEM SCRAP RECOVERY]", hudHeader);

            lineY += 28f;
            GUI.Label(new Rect(boxX + 16, lineY, boxWidth - 32, 20), "• MODEL: Mars Technarchy URM Berserker Cyborg", hudText);
            lineY += 20f;
            GUI.Label(new Rect(boxX + 16, lineY, boxWidth - 32, 20), "• CHASSIS: Severely Damaged (Discarded from Zalem)", hudText);
            lineY += 20f;
            GUI.Label(new Rect(boxX + 16, lineY, boxWidth - 32, 20), "• LIMBS: Lower Chassis Severed, Left Arm Destroyed", hudText);
            lineY += 20f;
            GUI.Label(new Rect(boxX + 16, lineY, boxWidth - 32, 20), "• HUMAN BRAIN: 100% INTACT & FUNCTIONAL", hudText);
            lineY += 20f;
            GUI.Label(new Rect(boxX + 16, lineY, boxWidth - 32, 20), "• CORE: Miniature Antimatter Reactor ACTIVE (Pulsing)", hudText);

            lineY += 28f;
            GUI.color = new Color(0.1f, 0.9f, 0.4f);
            GUI.Label(new Rect(boxX + 16, lineY, boxWidth - 32, 22), "▶ Dr. Dyson Ido: \"Her brain is intact... and her heart is still beating!\"", hudHeader);
        }
    }
}
