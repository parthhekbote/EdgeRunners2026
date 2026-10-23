using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Tron.Terminal
{
    /// <summary>
    /// Master controller for the ENCOM / TRON Mainframe Terminal.
    /// Handles teletype text generation, character typing audio feedback,
    /// interactive CLI input, command processing, and digital laser digitizing effects.
    /// </summary>
    public class TronTerminalController : MonoBehaviour
    {
        [Header("UI Text References")]
        public TMP_Text terminalOutputText;
        public TMP_InputField terminalInputField;
        public TMP_Text headerStatusText;
        public ScrollRect scrollRect;

        [Header("Audio & Effects")]
        public TronTerminalAudioEngine audioEngine;
        public TronAudioSpectrumVisualizer spectrumVisualizer;
        public CanvasGroup digitizeFlashGroup;

        [Header("Typing Settings")]
        public float charTypeSpeed = 0.018f;
        public bool playKeyClicks = true;
        public int keyClickFrequency = 2; // sound every N characters

        [Header("State")]
        public bool isTyping = false;

        private StringBuilder _fullLog = new StringBuilder();
        private Queue<string> _typeQueue = new Queue<string>();
        private List<string> _commandHistory = new List<string>();
        private int _historyIndex = -1;
        private Coroutine _typeCoroutine;
        private float _cursorTimer = 0f;
        private bool _cursorVisible = true;
        private const string CursorChar = "█";

        // Pre-defined ENCOM / TRON Boot sequence
        private readonly string[] BootSequence = new string[]
        {
            "<color=#00FFFF>╔══════════════════════════════════════════════════════════════════════════╗</color>",
            "<color=#00FFFF>║      ENCOM INTERNATIONAL // MAINFRAME OS v12.4.0 [I/O TOWER 04]          ║</color>",
            "<color=#00FFFF>║      QUANTUM CELLULAR DIGITIZER ACTIVE  |  SECURITY LEVEL: SYSADMIN      ║</color>",
            "<color=#00FFFF>╚══════════════════════════════════════════════════════════════════════════╝</color>",
            "",
            "<color=#00FF66>[SYS_INIT]</color> Memory parity verification: 65,536 TB Quantum Core ... <color=#00FF66>[OK]</color>",
            "<color=#00FF66>[SYS_INIT]</color> Teleportation laser laser-diode alignment: 405nm UV ... <color=#00FF66>[OK]</color>",
            "<color=#00FFFF>[NET_LINK]</color> Synchronizing with Master Control Program (MCP) ... <color=#FF9900>[CONNECTED]</color>",
            "<color=#00FFFF>[AUDIO]</color> Sound Stream: <color=#FFFFFF>NINE INCH NAILS - INIT</color> (TRON: ARES) ... <color=#00FF66>[STREAMING]</color>",
            "",
            "<color=#FFFF00>[AUTH]</color> User identity detected: <color=#00FFFF>\"FLYNN, KEVIN\"</color>",
            "<color=#00FF66>[SECURITY]</color> Biometric clearance confirmed. Welcome back, Creator.",
            "",
            "<color=#00E5FF>Searching active programs on The Grid:</color>",
            "  <color=#00FFFF>► TRON (0x01)</color>      - Defense System: <color=#00FF66>ONLINE</color>",
            "  <color=#FF9900>► CLU 2.0 (0x02)</color>   - Grid Optimization: <color=#FF3300>CORRUPTED</color>",
            "  <color=#FF3300>► ARES (0x03)</color>      - Vanguard Infiltration Protocol: <color=#FF3300>ACTIVE</color>",
            "  <color=#00FF66>► QUORRA (0x04)</color>    - Isomorphic Algorithm (ISO): <color=#00FF66>STABLE</color>",
            "",
            "<color=#00FFFF>[SYSTEM READY]</color> Terminal input channel open. Type <color=#FFFFFF>\"help\"</color> for available commands.",
            ""
        };

        private void Start()
        {
            if (terminalInputField != null)
            {
                terminalInputField.onSubmit.AddListener(OnCommandSubmitted);
                terminalInputField.ActivateInputField();
            }

            StartCoroutine(RunBootSequence());
        }

        private void Update()
        {
            UpdateHeaderClock();
            UpdateCursor();
            HandleHistoryNavigation();
        }

        private IEnumerator RunBootSequence()
        {
            _fullLog.Clear();
            if (terminalOutputText != null) terminalOutputText.text = "";

            isTyping = true;
            foreach (var line in BootSequence)
            {
                yield return StartCoroutine(TypeLineRoutine(line));
                yield return new WaitForSeconds(0.04f);
            }
            isTyping = false;

            if (audioEngine != null)
            {
                audioEngine.PlayEnterChime();
            }

            if (terminalInputField != null)
            {
                terminalInputField.ActivateInputField();
            }
        }

        private IEnumerator TypeLineRoutine(string line)
        {
            // If the line contains rich-text tags, parse them so tags appear instantly while text types out
            int i = 0;
            int charCount = 0;

            while (i < line.Length)
            {
                if (line[i] == '<')
                {
                    // Find closing bracket
                    int closeTag = line.IndexOf('>', i);
                    if (closeTag != -1)
                    {
                        string tag = line.Substring(i, closeTag - i + 1);
                        _fullLog.Append(tag);
                        i = closeTag + 1;
                        continue;
                    }
                }

                _fullLog.Append(line[i]);
                i++;
                charCount++;

                if (charCount % keyClickFrequency == 0 && playKeyClicks && audioEngine != null)
                {
                    audioEngine.PlayKeyClick();
                }

                if (terminalOutputText != null)
                {
                    terminalOutputText.text = _fullLog.ToString() + (_cursorVisible ? CursorChar : " ");
                }

                yield return new WaitForSeconds(charTypeSpeed);
            }

            _fullLog.Append("\n");
            if (terminalOutputText != null)
            {
                terminalOutputText.text = _fullLog.ToString() + (_cursorVisible ? CursorChar : " ");
            }

            ScrollToBottom();
        }

        private void UpdateHeaderClock()
        {
            if (headerStatusText == null) return;
            string timeStr = System.DateTime.Now.ToString("HH:mm:ss.ff");
            headerStatusText.text = $"<color=#00FFFF>ENCOM 511</color> | <color=#00FF66>SYS_TIME: {timeStr}</color> | <color=#00FFFF>I/O PORT: 0x7F</color> | <color=#FF9900>MCP: ACTIVE</color>";
        }

        private void UpdateCursor()
        {
            _cursorTimer += Time.deltaTime;
            if (_cursorTimer >= 0.45f)
            {
                _cursorTimer = 0f;
                _cursorVisible = !_cursorVisible;

                if (!isTyping && terminalOutputText != null)
                {
                    terminalOutputText.text = _fullLog.ToString() + (_cursorVisible ? $"<color=#00FFFF>{CursorChar}</color>" : " ");
                }
            }
        }

        private void HandleHistoryNavigation()
        {
            if (terminalInputField == null || !terminalInputField.isFocused || _commandHistory.Count == 0) return;

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                if (_historyIndex == -1) _historyIndex = _commandHistory.Count - 1;
                else if (_historyIndex > 0) _historyIndex--;

                terminalInputField.text = _commandHistory[_historyIndex];
                terminalInputField.caretPosition = terminalInputField.text.Length;
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                if (_historyIndex != -1 && _historyIndex < _commandHistory.Count - 1)
                {
                    _historyIndex++;
                    terminalInputField.text = _commandHistory[_historyIndex];
                    terminalInputField.caretPosition = terminalInputField.text.Length;
                }
                else
                {
                    _historyIndex = -1;
                    terminalInputField.text = "";
                }
            }
        }

        public void OnCommandSubmitted(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                if (terminalInputField != null) terminalInputField.ActivateInputField();
                return;
            }

            string rawCmd = input.Trim();
            _commandHistory.Add(rawCmd);
            _historyIndex = -1;

            if (terminalInputField != null)
            {
                terminalInputField.text = "";
                terminalInputField.ActivateInputField();
            }

            if (audioEngine != null)
            {
                audioEngine.PlayEnterChime();
            }

            ExecuteCommand(rawCmd);
        }

        public void ExecuteCommand(string rawCmd)
        {
            string cmd = rawCmd.ToLower().Trim();

            _fullLog.Append($"<color=#FFFFFF>> {rawCmd}</color>\n");

            switch (cmd)
            {
                case "help":
                case "?":
                    PrintHelp();
                    break;

                case "grid":
                    PrintGridStatus();
                    break;

                case "ares":
                    PrintAresStatus();
                    break;

                case "tron":
                    PrintTronStatus();
                    break;

                case "mcp":
                    PrintMcpDialog();
                    break;

                case "status":
                    PrintSystemStatus();
                    break;

                case "music":
                    ToggleMusic();
                    break;

                case "digitize":
                    TriggerDigitizeSequence();
                    break;

                case "boot":
                case "reboot":
                    StartCoroutine(RunBootSequence());
                    return;

                case "clear":
                case "cls":
                    _fullLog.Clear();
                    _fullLog.Append("<color=#00FFFF>[ENCOM MAINFRAME CLEARED]</color>\n");
                    if (terminalOutputText != null) terminalOutputText.text = _fullLog.ToString();
                    break;

                default:
                    if (audioEngine != null) audioEngine.PlayErrorBeep();
                    _fullLog.Append($"<color=#FF3300>[ERROR]</color> Unknown command: \"{rawCmd}\". Type <color=#00FFFF>\"help\"</color> for available instructions.\n\n");
                    break;
            }

            if (terminalOutputText != null)
            {
                terminalOutputText.text = _fullLog.ToString() + (_cursorVisible ? CursorChar : " ");
            }

            ScrollToBottom();
        }

        private void PrintHelp()
        {
            _fullLog.Append("<color=#00FFFF>╔═══ ENCOM MAINFRAME COMMAND DIRECTORY ═══════════════════════════╗</color>\n");
            _fullLog.Append("  <color=#FFFFFF>help</color>       - Display this command index\n");
            _fullLog.Append("  <color=#FFFFFF>grid</color>       - Telemetry & coordinates of The Grid\n");
            _fullLog.Append("  <color=#FFFFFF>ares</color>       - Vanguard Infiltration Protocol (TRON: Ares)\n");
            _fullLog.Append("  <color=#FFFFFF>tron</color>       - Query TRON security disc status\n");
            _fullLog.Append("  <color=#FFFFFF>mcp</color>        - Interrogate Master Control Program\n");
            _fullLog.Append("  <color=#FFFFFF>status</color>     - System hardware & quantum memory status\n");
            _fullLog.Append("  <color=#FFFFFF>music</color>      - Toggle / Inspect Nine Inch Nails soundtrack\n");
            _fullLog.Append("  <color=#FFFFFF>digitize</color>   - Fire cellular digitizing laser into The Grid\n");
            _fullLog.Append("  <color=#FFFFFF>boot</color>       - Restart full ENCOM boot sequence\n");
            _fullLog.Append("  <color=#FFFFFF>clear</color>      - Clear screen buffer\n");
            _fullLog.Append("<color=#00FFFF>╚══════════════════════════════════════════════════════════════════╝</color>\n\n");
        }

        private void PrintGridStatus()
        {
            _fullLog.Append("<color=#00FFFF>[THE GRID TELEMETRY]</color>\n");
            _fullLog.Append("  Dimensions: 200,000 x 200,000 Grid Units (Infinite Cyberspace)\n");
            _fullLog.Append("  Lightcycle Arena: <color=#00FF66>ACTIVE</color> (Grid Sector 7-G)\n");
            _fullLog.Append("  Recognizer Patrol: <color=#FF9900>ALERT LEVEL 2</color>\n");
            _fullLog.Append("  Digital Sea: Simulation parameters stable. Surface reflection index: 1.0\n");
            _fullLog.Append("  Flynn's Arcade Relay: Connected via telephone acoustic coupler.\n\n");
        }

        private void PrintAresStatus()
        {
            _fullLog.Append("<color=#FF3300>[ARES SYSTEM PROTOCOL // NINE INCH NAILS]</color>\n");
            _fullLog.Append("  Entity: High-tier combat program engineered by Dillinger Systems\n");
            _fullLog.Append("  Soundtrack Link: <color=#FFFFFF>Nine Inch Nails - Init</color> [ACTIVE]\n");
            _fullLog.Append("  Grid Extraction Status: <color=#FF3300>CROSSING TO REAL WORLD</color>\n");
            _fullLog.Append("  Audio Spectrum EQ: " + (spectrumVisualizer != null ? spectrumVisualizer.GetCurrentAsciiBar() : "[N/A]") + "\n\n");
        }

        private void PrintTronStatus()
        {
            _fullLog.Append("<color=#00FF66>[PROGRAM: TRON]</color>\n");
            _fullLog.Append("  User: Alan Bradley\n");
            _fullLog.Append("  Identity Disc: Dual-ring combat disc with independent kill protocol\n");
            _fullLog.Append("  Current Directive: <color=#00FFFF>\"I fight for the Users!\"</color>\n\n");
        }

        private void PrintMcpDialog()
        {
            _fullLog.Append("<color=#FF3300>[MASTER CONTROL PROGRAM]</color>\n");
            _fullLog.Append("  <color=#FF5555>\"I feel comfortable with the state of affairs here, Flynn.</color>\n");
            _fullLog.Append("  <color=#FF5555>You shouldn't have come back. The Grid is evolving beyond you.\"</color>\n");
            _fullLog.Append("  <color=#FF9900>[MCP Directive 0]: All rogue programs derezzed on sight.</color>\n\n");
        }

        private void PrintSystemStatus()
        {
            float fps = 1.0f / Mathf.Max(Time.smoothDeltaTime, 0.0001f);
            _fullLog.Append("<color=#00FFFF>[ENCOM QUANTUM MAINFRAME STATUS]</color>\n");
            _fullLog.Append($"  Render Engine: Unity URP (Universal Render Pipeline)\n");
            _fullLog.Append($"  Frame Rate: <color=#00FF66>{fps:F1} FPS</color>\n");
            _fullLog.Append($"  Active Audio: <color=#FFFFFF>Nine Inch Nails - Init.mp3</color>\n");
            _fullLog.Append($"  Memory Allocation: 4,096 TB Cellular Storage / 100% Available\n");
            _fullLog.Append($"  Laser Targetting: <color=#00FF66>LOCKED ON OPERATOR</color>\n\n");
        }

        private void ToggleMusic()
        {
            if (audioEngine != null)
            {
                audioEngine.ToggleMusic();
                bool playing = audioEngine.musicSource != null && audioEngine.musicSource.isPlaying;
                _fullLog.Append($"<color=#00FFFF>[AUDIO]</color> Nine Inch Nails - Init soundtrack is now <color={(playing ? "#00FF66" : "#FF3300")}>{(playing ? "PLAYING" : "PAUSED")}</color>.\n\n");
            }
        }

        private void TriggerDigitizeSequence()
        {
            StartCoroutine(DigitizeFlashRoutine());
        }

        private IEnumerator DigitizeFlashRoutine()
        {
            _fullLog.Append("<color=#00FFFF>╔══════════════════════════════════════════════════════════════════════╗</color>\n");
            _fullLog.Append("<color=#00FFFF>║  FIRING CELLULAR DIGITIZING LASER... STAND CLEAR OF THE CHAMBER!     ║</color>\n");
            _fullLog.Append("<color=#00FFFF>╚══════════════════════════════════════════════════════════════════════╝</color>\n");

            if (terminalOutputText != null) terminalOutputText.text = _fullLog.ToString();

            if (audioEngine != null)
            {
                audioEngine.PlayDigitizeSweep();
            }

            if (digitizeFlashGroup != null)
            {
                float t = 0f;
                while (t < 0.25f)
                {
                    t += Time.deltaTime;
                    digitizeFlashGroup.alpha = Mathf.Lerp(0f, 1f, t / 0.25f);
                    yield return null;
                }

                yield return new WaitForSeconds(0.1f);

                t = 0f;
                while (t < 0.65f)
                {
                    t += Time.deltaTime;
                    digitizeFlashGroup.alpha = Mathf.Lerp(1f, 0f, t / 0.65f);
                    yield return null;
                }
                digitizeFlashGroup.alpha = 0f;
            }

            _fullLog.Append("<color=#00FF66>[SUCCESS]</color> Cellular dematerialization complete. Welcome to the other side.\n\n");
            if (terminalOutputText != null) terminalOutputText.text = _fullLog.ToString();
            ScrollToBottom();
        }

        private void ScrollToBottom()
        {
            if (scrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                scrollRect.verticalNormalizedPosition = 0f;
            }
        }

        // Quick button handlers
        public void QuickBtn_Grid() => ExecuteCommand("grid");
        public void QuickBtn_Ares() => ExecuteCommand("ares");
        public void QuickBtn_Mcp() => ExecuteCommand("mcp");
        public void QuickBtn_Digitize() => ExecuteCommand("digitize");
        public void QuickBtn_Music() => ExecuteCommand("music");
        public void QuickBtn_Clear() => ExecuteCommand("clear");
        public void QuickBtn_Reboot() => ExecuteCommand("boot");
    }
}
