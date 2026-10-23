using UnityEngine;
using UnityEngine.UI;

namespace Alita
{
    /// <summary>
    /// In-world physical 3D Terminal Screen Asset attached directly to Dr. Ido's console.
    /// Renders live boot commands and scrolling diagnostic logs physically onto a 3D Screen Quad
    /// mounted on the console display surface in the 3D scene via a high-resolution CRT RenderTexture.
    /// </summary>
    public class TronTerminalScreenAsset : MonoBehaviour
    {
        [Header("UI References")]
        public Canvas screenCanvas;
        public Text headerText;
        public Text promptText;
        public Text bootLogsText;
        public UnityEngine.UI.Image screenBackground;
        public UnityEngine.UI.Image cursorFlashImage;
        private UnityEngine.UI.Image[] _laserLineImages;

        [Header("3D Screen Render")]
        public GameObject screenQuadGO;
        public MeshRenderer screenQuadRenderer;
        public Material screenMaterial;
        public RenderTexture screenRT;
        private Camera _uiRenderCamera;

        private void Awake()
        {
            SetupScreenCanvas();
        }

        public void SetupScreenCanvas()
        {
            // 1. Create or retrieve high-res RenderTexture (1024x768)
            if (screenRT == null)
            {
                screenRT = new RenderTexture(1024, 768, 24, RenderTextureFormat.ARGB32);
                screenRT.name = "RT_Terminal_Screen";
            }

            // 2. Setup dedicated UI camera for rendering canvas to RenderTexture
            Transform uiCamT = transform.Find("Terminal_UI_Camera");
            if (uiCamT == null)
            {
                GameObject camGO = new GameObject("Terminal_UI_Camera");
                camGO.transform.SetParent(transform, false);
                _uiRenderCamera = camGO.AddComponent<Camera>();
                _uiRenderCamera.clearFlags = CameraClearFlags.SolidColor;
                _uiRenderCamera.backgroundColor = new Color(0.008f, 0.012f, 0.016f, 1f);
                _uiRenderCamera.cullingMask = 1 << LayerMask.NameToLayer("UI");
                _uiRenderCamera.targetTexture = screenRT;
                _uiRenderCamera.enabled = false;
            }
            else
            {
                _uiRenderCamera = uiCamT.GetComponent<Camera>();
                _uiRenderCamera.targetTexture = screenRT;
            }

            // 3. Mount physical 3D Screen Quad directly onto console angled display surface
            Transform quadT = transform.Find("Screen_Quad");
            if (quadT == null)
            {
                screenQuadGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
                screenQuadGO.name = "Screen_Quad";
                screenQuadGO.transform.SetParent(transform, false);
                DestroyImmediate(screenQuadGO.GetComponent<Collider>());
            }
            else
            {
                screenQuadGO = quadT.gameObject;
            }

            // Align precisely with the angled monitor bezel on SM_Prop_Console_01
            Vector3 surfaceNormal = new Vector3(0f, 0.83f, 0.56f).normalized;
            screenQuadGO.transform.localPosition = new Vector3(0f, 0.952f, 0.045f);
            screenQuadGO.transform.localRotation = Quaternion.LookRotation(surfaceNormal, Vector3.up);
            screenQuadGO.transform.localScale = new Vector3(-0.48f, 0.32f, 1f); // Inverted X flips UVs so text is not mirrored

            var unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (screenMaterial == null)
            {
                screenMaterial = new Material(unlitShader);
                screenMaterial.name = "Mat_Terminal_Screen_Quad";
            }
            screenMaterial.SetTexture("_BaseMap", screenRT);
            screenMaterial.SetColor("_BaseColor", Color.white);
            screenMaterial.SetFloat("_Cull", 0f);

            screenQuadRenderer = screenQuadGO.GetComponent<MeshRenderer>();
            screenQuadRenderer.sharedMaterial = screenMaterial;

            // 4. Setup Screen Space Camera Canvas
            var existing = transform.Find("Terminal_Screen_Canvas");
            GameObject canvasGO;
            if (existing != null)
            {
                canvasGO = existing.gameObject;
                screenCanvas = existing.GetComponent<Canvas>();
                headerText = existing.Find("Header_Text")?.GetComponent<Text>();
                promptText = existing.Find("Prompt_Text")?.GetComponent<Text>();
                bootLogsText = existing.Find("BootLogs_Text")?.GetComponent<Text>();
                screenBackground = existing.Find("Screen_BG")?.GetComponent<UnityEngine.UI.Image>();
                cursorFlashImage = existing.Find("Cursor_Flash")?.GetComponent<UnityEngine.UI.Image>();
            }
            else
            {
                canvasGO = new GameObject("Terminal_Screen_Canvas");
                canvasGO.transform.SetParent(transform, false);
                screenCanvas = canvasGO.AddComponent<Canvas>();
                canvasGO.AddComponent<CanvasScaler>();
            }

            canvasGO.layer = LayerMask.NameToLayer("UI");
            screenCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            screenCanvas.worldCamera = _uiRenderCamera;
            screenCanvas.planeDistance = 10f;

            var rt = canvasGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(1024f, 768f);

            if (existing == null || headerText == null || promptText == null)
            {
                // Dark pitch-black CRT background
                GameObject bgGO = new GameObject("Screen_BG");
                bgGO.transform.SetParent(canvasGO.transform, false);
                bgGO.layer = LayerMask.NameToLayer("UI");
                screenBackground = bgGO.AddComponent<UnityEngine.UI.Image>();
                screenBackground.color = new Color(0.005f, 0.008f, 0.012f, 1.0f);
                var bgRT = bgGO.GetComponent<RectTransform>();
                bgRT.anchorMin = Vector2.zero;
                bgRT.anchorMax = Vector2.one;
                bgRT.sizeDelta = Vector2.zero;

                // Terminal Header: Authentic Film "TERMINAL - 04 ALERT"
                GameObject hdrGO = new GameObject("Header_Text");
                hdrGO.transform.SetParent(canvasGO.transform, false);
                hdrGO.layer = LayerMask.NameToLayer("UI");
                headerText = hdrGO.AddComponent<Text>();
                headerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                headerText.fontSize = 28;
                headerText.fontStyle = FontStyle.Bold;
                headerText.color = new Color(0.85f, 0.92f, 1.0f, 0.95f);
                headerText.text = "TERMINAL - 04  ALERT";
                var hdrRT = hdrGO.GetComponent<RectTransform>();
                hdrRT.anchorMin = new Vector2(0f, 1f);
                hdrRT.anchorMax = new Vector2(1f, 1f);
                hdrRT.pivot = new Vector2(0.5f, 1f);
                hdrRT.anchoredPosition = new Vector2(0f, -22f);
                hdrRT.sizeDelta = new Vector2(-60f, 42f);

                // Live Command Prompt
                GameObject promptGO = new GameObject("Prompt_Text");
                promptGO.transform.SetParent(canvasGO.transform, false);
                promptGO.layer = LayerMask.NameToLayer("UI");
                promptText = promptGO.AddComponent<Text>();
                promptText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                promptText.fontSize = 42;
                promptText.fontStyle = FontStyle.Bold;
                promptText.supportRichText = true;
                promptText.color = Color.white;
                promptText.text = "<color=#FFFFFF>#</color> <color=#FF2222>./start_alita_prog</color><color=#FFFFFF>█</color>";
                var pRT = promptGO.GetComponent<RectTransform>();
                pRT.anchorMin = new Vector2(0f, 1f);
                pRT.anchorMax = new Vector2(1f, 1f);
                pRT.pivot = new Vector2(0.5f, 1f);
                pRT.anchoredPosition = new Vector2(0f, -80f);
                pRT.sizeDelta = new Vector2(-60f, 60f);

                // Scrolling System Boot Logs (Authentic Monospace Red/White)
                GameObject logGO = new GameObject("BootLogs_Text");
                logGO.transform.SetParent(canvasGO.transform, false);
                logGO.layer = LayerMask.NameToLayer("UI");
                bootLogsText = logGO.AddComponent<Text>();
                bootLogsText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                bootLogsText.fontSize = 22;
                bootLogsText.fontStyle = FontStyle.Bold;
                bootLogsText.lineSpacing = 1.2f;
                bootLogsText.supportRichText = true;
                bootLogsText.color = Color.white;
                var lRT = logGO.GetComponent<RectTransform>();
                lRT.anchorMin = new Vector2(0f, 0f);
                lRT.anchorMax = new Vector2(1f, 1f);
                lRT.pivot = new Vector2(0.5f, 0f);
                lRT.anchoredPosition = new Vector2(40f, 25f);
                lRT.sizeDelta = new Vector2(-100f, -170f);

                // Incandescent Cursor Detonation Flash Overlay
                GameObject flashGO = new GameObject("Cursor_Flash");
                flashGO.transform.SetParent(canvasGO.transform, false);
                flashGO.layer = LayerMask.NameToLayer("UI");
                cursorFlashImage = flashGO.AddComponent<UnityEngine.UI.Image>();
                cursorFlashImage.color = new Color(1f, 0.45f, 0.15f, 0f);
                var fRT = flashGO.GetComponent<RectTransform>();
                fRT.anchorMin = Vector2.zero;
                fRT.anchorMax = Vector2.one;
                fRT.sizeDelta = Vector2.zero;
            }

            SetupLaserLines(canvasGO);
        }

        private void SetupLaserLines(GameObject canvasGO)
        {
            Transform laserContainer = canvasGO.transform.Find("Laser_Lines");
            if (laserContainer == null)
            {
                GameObject lContainerGO = new GameObject("Laser_Lines");
                lContainerGO.transform.SetParent(canvasGO.transform, false);
                lContainerGO.layer = LayerMask.NameToLayer("UI");
                var lcRT = lContainerGO.AddComponent<RectTransform>();
                lcRT.anchorMin = new Vector2(0.5f, 0.5f);
                lcRT.anchorMax = new Vector2(0.5f, 0.5f);
                lcRT.pivot = new Vector2(0.5f, 0.5f);
                lcRT.anchoredPosition = Vector2.zero;
                laserContainer = lContainerGO.transform;
            }

            _laserLineImages = new UnityEngine.UI.Image[12];
            for (int i = 0; i < 12; i++)
            {
                Transform childT = laserContainer.Find($"Laser_Line_{i}");
                GameObject lineGO;
                if (childT == null)
                {
                    lineGO = new GameObject($"Laser_Line_{i}");
                    lineGO.transform.SetParent(laserContainer, false);
                    lineGO.layer = LayerMask.NameToLayer("UI");
                }
                else
                {
                    lineGO = childT.gameObject;
                }

                _laserLineImages[i] = lineGO.GetComponent<UnityEngine.UI.Image>();
                if (_laserLineImages[i] == null)
                    _laserLineImages[i] = lineGO.AddComponent<UnityEngine.UI.Image>();

                var lrt = lineGO.GetComponent<RectTransform>();
                if (lrt == null) lrt = lineGO.AddComponent<RectTransform>();
                lrt.anchorMin = new Vector2(0.5f, 0.5f);
                lrt.anchorMax = new Vector2(0.5f, 0.5f);
                lrt.pivot = new Vector2(0f, 0.5f); // Radiates outward from center
                lrt.anchoredPosition = Vector2.zero;
                float angle = i * 30f;
                lrt.localRotation = Quaternion.Euler(0f, 0f, angle);
                lrt.sizeDelta = new Vector2(10f, 4f);
                _laserLineImages[i].color = new Color(1f, 0.35f, 0.15f, 0f);
            }
        }

        public void UpdateScreenAtTime(float time)
        {
            if (promptText == null || _uiRenderCamera == null) SetupScreenCanvas();

            var pRT = promptText.GetComponent<RectTransform>();

            if (time < 4.5f)
            {
                // Terminal close-up direct screen view (0.0s - 4.5s)
                // Film match: Header is absent in macro shot, prompt is centered in the display
                if (headerText != null) headerText.text = "";

                pRT.anchorMin = new Vector2(0.5f, 0.5f);
                pRT.anchorMax = new Vector2(0.5f, 0.5f);
                pRT.pivot = new Vector2(0.5f, 0.5f);
                pRT.anchoredPosition = Vector2.zero;
                pRT.sizeDelta = new Vector2(1000f, 200f);
                promptText.alignment = TextAnchor.MiddleCenter;
                promptText.fontSize = 56;

                if (time < 3.0f)
                {
                    // Initial blinking cursor block █ (active at 0.0s, 1.0s, 2.0s matching film reference frames)
                    bool blink = (Mathf.FloorToInt(time * 2f) % 2) == 0;
                    promptText.text = $"<color=#FFFFFF># </color>" + (blink ? "<color=#FFFFFF>█</color>" : "");
                    bootLogsText.text = "";
                    cursorFlashImage.color = new Color(1f, 0.45f, 0.15f, 0f);
                }
                else
                {
                    // Typing # ./star█
                    string fullStr = "./star";
                    int count = Mathf.Clamp(Mathf.FloorToInt((time - 3.0f) / 0.18f), 0, fullStr.Length);
                    bool blink = (Mathf.FloorToInt(time * 4f) % 2) == 0;
                    string typed = fullStr.Substring(0, count);
                    promptText.text = $"<color=#FFFFFF>#</color> <color=#FF2222>{typed}</color>" + (blink ? "<color=#FFFFFF>█</color>" : "");
                    bootLogsText.text = "";
                    cursorFlashImage.color = new Color(1f, 0.45f, 0.15f, 0f);
                }
            }
            else
            {
                // Over-the-shoulder / console monitor view (4.5s - 10.2s)
                if (headerText != null) headerText.text = "TERMINAL - 04  ALERT";

                pRT.anchorMin = new Vector2(0f, 1f);
                pRT.anchorMax = new Vector2(1f, 1f);
                pRT.pivot = new Vector2(0.5f, 1f);
                pRT.anchoredPosition = new Vector2(30f, -80f);
                pRT.sizeDelta = new Vector2(-60f, 60f);
                promptText.alignment = TextAnchor.UpperLeft;
                promptText.fontSize = 42;

                if (time < 7.5f)
                {
                    // Typing # ./start_alita_prog█
                    string fullStr = "./start_alita_prog";
                    int count = Mathf.Clamp(Mathf.FloorToInt(((time - 4.5f) / 2.5f) * (fullStr.Length - 5)) + 6, 6, fullStr.Length);
                    bool blink = (Mathf.FloorToInt(time * 4f) % 2) == 0;
                    string typed = fullStr.Substring(0, count);
                    promptText.text = $"<color=#FFFFFF>#</color> <color=#FF2222>{typed}</color>" + (blink ? "<color=#FFFFFF>█</color>" : "");
                    bootLogsText.text = "";
                    cursorFlashImage.color = new Color(1f, 0.45f, 0.15f, 0f);
                }
                else if (time < 9.5f)
                {
                    // Boot logs stream downward on terminal (1:1 authentic film lines)
                    promptText.text = "<color=#FFFFFF>#</color> <color=#FF2222>./start_alita_prog</color> <color=#FFFFFF>[OK]</color>";

                    string[] logLines = new string[] {
                        "<color=#FF2828>[ 2.35114 ]</color> <color=#FFFFFF>[ OK ]</color> <color=#FF2828>Started Real-World Interaction Layer</color> <color=#FFFFFF>(RWIL)</color>",
                        "<color=#FF2828>[ 2.41005 ]</color> <color=#FFFFFF>[ OK ]</color> <color=#FF2828>Engaged Sentience Calibration Module</color> <color=#FFFFFF>v3.1</color>",
                        "<color=#FF2828>[ 2.48567 ]</color> <color=#FFFFFF>[ OK ]</color> <color=#FF2828>Network Interfaces (GridNet0 / RWNet0)</color> <color=#FFFFFF>Online</color>",
                        "<color=#FF2828>[ 2.55112 ]</color> <color=#FFFFFF>[ OK ]</color> <color=#FF2828>Loaded Primary Directive:</color> <color=#FFFFFF>[REDACTED]. Standby Mode</color>",
                        "<color=#FF2828>[ 2.61034 ]</color> <color=#FFFFFF>[ OK ]</color> <color=#FF2828>Reached target</color> <color=#FFFFFF>Multi-User System</color>",
                        "<color=#FF2828>[ 2.68551 ]</color> <color=#FFFFFF>[ OK ]</color> <color=#FF2828>Reached target</color> <color=#FFFFFF>Graphical Interface (Restricted Control)</color>",
                        "<color=#FF2828>[ 3.13337 ]</color> <color=#FFFFFF>[ OK ]</color> <color=#FF2828>Project status Zero: Ready for</color> <color=#FFFFFF>ALITA Initialization</color>",
                        "<color=#FF2828>[ 3.14528 ]</color> <color=#FFFFFF>[ OK ]</color> <color=#FF2828>Lasers online and ready for</color> <color=#FFFFFF>generative tasks</color>",
                        "<color=#FF2828>[ 3.15425 ]</color> <color=#FF2828>Laser Calibration test:...</color> <color=#FFFFFF>Success</color>",
                        "<color=#FF2828>[ 3.51426 ]</color> <color=#FF2828>Systems ready for final</color> <color=#FFFFFF>ALITA boot.</color>",
                        "<color=#FF2828>[ 3.81945 ]</color> <color=#FF2828>Init ALITA on physical CPU</color> <color=#FFFFFF>0x52555353</color>",
                        "<color=#FF2828>[ 4.12474 ]</color> <color=#FFFFFF>Loading Neural Matrix...█</color>"
                    };

                    int numToShow = Mathf.Clamp(Mathf.FloorToInt(((time - 7.5f) / 2.0f) * logLines.Length) + 1, 1, logLines.Length);
                    string combined = "";
                    for (int i = 0; i < numToShow; i++) combined += logLines[i] + "\n";
                    bootLogsText.text = combined;
                    cursorFlashImage.color = new Color(1f, 0.45f, 0.15f, 0f);
                }
                else if (time < 9.9f)
                {
                    // Enter Slam: macro tight "ing █" with cursor █ DEAD CENTER of the screen
                    if (headerText != null) headerText.text = "";
                    pRT.anchorMin = new Vector2(0.5f, 0.5f);
                    pRT.anchorMax = new Vector2(0.5f, 0.5f);
                    pRT.pivot = new Vector2(0.5f, 0.5f);
                    pRT.anchoredPosition = new Vector2(-115f, 0f); // Offsets "ing" so the cursor █ is dead center!
                    pRT.sizeDelta = new Vector2(1000f, 250f);
                    promptText.alignment = TextAnchor.MiddleCenter;
                    promptText.fontSize = 110;
                    promptText.text = "<color=#FF2828>ing</color> <color=#FFFFFF>█</color>";
                    bootLogsText.text = "";
                    cursorFlashImage.color = new Color(1f, 0.45f, 0.15f, 0f);
                }
                else if (time < 10.2f)
                {
                    // The cursor turns into glowing laser lines which travel outward from center!
                    float tFlash = (time - 9.9f) / 0.3f;
                    // Soft warm glow instead of blinding blowout
                    cursorFlashImage.color = new Color(1.0f, 0.45f, 0.15f, Mathf.Clamp01(tFlash * 0.35f));

                    // Show centered cursor transforming into outward lines
                    pRT.anchorMin = new Vector2(0.5f, 0.5f);
                    pRT.anchorMax = new Vector2(0.5f, 0.5f);
                    pRT.pivot = new Vector2(0.5f, 0.5f);
                    pRT.anchoredPosition = Vector2.zero;
                    promptText.alignment = TextAnchor.MiddleCenter;
                    promptText.fontSize = 110;
                    promptText.text = "<color=#FFFFFF>█</color>";
                }
                else
                {
                    cursorFlashImage.color = new Color(1f, 0.45f, 0.15f, 0f);
                }
            }

            // Animate outward travelling laser lines (9.8s - 10.3s)
            if (time >= 9.8f && time < 10.3f && _laserLineImages != null)
            {
                float tLine = Mathf.Clamp01((time - 9.8f) / 0.35f);
                float lineLen = Mathf.Lerp(15f, 950f, Mathf.Pow(tLine, 0.85f));
                float alpha = (time < 10.1f) ? Mathf.Clamp01(tLine * 3.0f) : (1.0f - Mathf.Clamp01((time - 10.1f) / 0.2f));
                for (int i = 0; i < _laserLineImages.Length; i++)
                {
                    if (_laserLineImages[i] != null)
                    {
                        var rt = _laserLineImages[i].rectTransform;
                        rt.sizeDelta = new Vector2(lineLen, (i % 3 == 0) ? 8f : 4f);
                        Color c = (i % 3 == 0) ? new Color(1.0f, 0.95f, 0.85f, alpha) : new Color(1.0f, 0.35f, 0.15f, alpha * 0.9f);
                        _laserLineImages[i].color = c;
                    }
                }
            }
            else if (_laserLineImages != null)
            {
                for (int i = 0; i < _laserLineImages.Length; i++)
                {
                    if (_laserLineImages[i] != null)
                    {
                        _laserLineImages[i].color = new Color(1f, 0.35f, 0.15f, 0f);
                    }
                }
            }

            // Render live canvas directly to RenderTexture for in-world screen quad
            if (_uiRenderCamera != null && screenRT != null)
            {
                Canvas.ForceUpdateCanvases();
                _uiRenderCamera.targetTexture = screenRT;
                _uiRenderCamera.Render();
            }
        }
    }
}
