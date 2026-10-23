using System.Collections;
using UnityEngine;
using Alita.UI;

namespace SyntyCombined.BlackMarket
{
    /// <summary>
    /// Cyberpunk Dialogue & Quest HUD for the Black Market.
    /// Renders sleek, neon-bordered dialogue cards with role badges,
    /// typewritten lore text, [E] prompt progression, and mission unlock notifications.
    /// </summary>
    public class BlackMarketNPCDialogueHUD : MonoBehaviour
    {
        public static BlackMarketNPCDialogueHUD Instance { get; private set; }

        private bool _isShowingDialogue = false;
        private BlackMarketNPC _activeNPC;
        private string _activeDialogueText = "";
        private int _currentStep = 1;
        private int _totalSteps = 1;

        // Mission banner state
        private bool _isShowingBanner = false;
        private string _bannerText = "";
        private float _bannerTimer = 0f;
        private const float BANNER_DURATION = 6.0f;

        // Story transmission state (comms / internal log lines)
        private bool _isShowingTransmission = false;
        private string _txSpeaker = "";
        private string _txRole = "";
        private string _txText = "";
        private Color _txColor = Color.white;
        private float _txTimer = 0f;

        // A transmission requested while dialogue is open plays once the card closes
        private bool _hasPendingTransmission = false;
        private string _pendingSpeaker = "";
        private string _pendingRole = "";
        private string _pendingText = "";
        private Color _pendingColor = Color.white;
        private float _pendingDuration = 6f;

        private Texture2D _darkBoxTex;
        private Texture2D _bannerBgTex;

        public static BlackMarketNPCDialogueHUD GetOrCreate()
        {
            if (Instance != null) return Instance;

            var existing = FindFirstObjectByType<BlackMarketNPCDialogueHUD>();
            if (existing != null)
            {
                Instance = existing;
                return Instance;
            }

            GameObject go = new GameObject("[BlackMarket_Dialogue_HUD]");
            Instance = go.AddComponent<BlackMarketNPCDialogueHUD>();
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(go);
            }
            return Instance;
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            InitTextures();
        }

        private void InitTextures()
        {
            if (_darkBoxTex == null)
            {
                _darkBoxTex = new Texture2D(1, 1);
                _darkBoxTex.SetPixel(0, 0, new Color(0.04f, 0.06f, 0.12f, 0.94f));
                _darkBoxTex.Apply();
            }

            if (_bannerBgTex == null)
            {
                _bannerBgTex = new Texture2D(1, 1);
                _bannerBgTex.SetPixel(0, 0, new Color(0.02f, 0.22f, 0.35f, 0.92f));
                _bannerBgTex.Apply();
            }
        }

        private void Update()
        {
            if (_isShowingBanner)
            {
                _bannerTimer -= Time.deltaTime;
                if (_bannerTimer <= 0f)
                {
                    _isShowingBanner = false;
                }
            }

            if (_isShowingTransmission)
            {
                _txTimer -= Time.deltaTime;
                if (_txTimer <= 0f)
                {
                    _isShowingTransmission = false;
                }
            }

            // Release a queued transmission once dialogue has closed
            if (!_isShowingDialogue && !_isShowingTransmission && _hasPendingTransmission)
            {
                BeginTransmission(_pendingSpeaker, _pendingRole, _pendingText, _pendingColor, _pendingDuration);
                _hasPendingTransmission = false;
            }

            // Close dialogue on ESC
            if (_isShowingDialogue && Input.GetKeyDown(KeyCode.Escape))
            {
                if (_activeNPC != null)
                {
                    _activeNPC.CloseConversation();
                }
                else
                {
                    HideDialogue();
                }
            }
        }

        public void DisplayDialogue(BlackMarketNPC npc, string text, int step, int totalSteps)
        {
            _isShowingTransmission = false;
            _activeNPC = npc;
            _activeDialogueText = text;
            _currentStep = step;
            _totalSteps = totalSteps;
            _isShowingDialogue = true;

            string speaker = npc != null ? npc.characterName : "CITIZEN";
            string role = npc != null ? npc.roleBadgeTitle : "";
            Sprite portrait = npc != null ? npc.portraitIcon : null;

            if (text.StartsWith("Alita: ", System.StringComparison.OrdinalIgnoreCase))
            {
                speaker = "ALITA";
                role = "MARTIAN CYBORG";
                text = text.Substring(7).Trim();
                portrait = null;
            }
            else if (text.StartsWith("[Alita]: ", System.StringComparison.OrdinalIgnoreCase))
            {
                speaker = "ALITA";
                role = "MARTIAN CYBORG";
                text = text.Substring(9).Trim();
                portrait = null;
            }
            else if (text.StartsWith("[Alita] ", System.StringComparison.OrdinalIgnoreCase))
            {
                speaker = "ALITA";
                role = "MARTIAN CYBORG";
                text = text.Substring(8).Trim();
                portrait = null;
            }
            else if (text.StartsWith("Voss: ", System.StringComparison.OrdinalIgnoreCase))
            {
                speaker = "KAELEN VOSS";
                role = "THE COLLECTOR";
                text = text.Substring(6).Trim();
            }
            else if (text.StartsWith("[Voss]: ", System.StringComparison.OrdinalIgnoreCase))
            {
                speaker = "KAELEN VOSS";
                role = "THE COLLECTOR";
                text = text.Substring(8).Trim();
            }
            else if (text.StartsWith("[Voss] ", System.StringComparison.OrdinalIgnoreCase))
            {
                speaker = "KAELEN VOSS";
                role = "THE COLLECTOR";
                text = text.Substring(7).Trim();
            }

            if (text.StartsWith("\"") && text.EndsWith("\"") && text.Length >= 2)
            {
                text = text.Substring(1, text.Length - 2).Trim();
            }

            string stepIndicator = totalSteps > 1 ? $"{role} [{step}/{totalSteps}]" : role;

            var dialogueHud = CyberpunkDialogueHUD.GetOrCreate();
            // Vendors and informants without their own portrait must not borrow Alita's card
            if (speaker != "ALITA" && speaker != "KAELEN VOSS" && !string.IsNullOrEmpty(speaker))
            {
                CyberpunkDialogueHUD.StreetNpcSpeakers.Add(speaker);
                if (portrait == null) portrait = dialogueHud.StreetNpcPortrait;
            }

            dialogueHud.ShowDialogue(
                speaker,
                text,
                portrait,
                stepIndicator,
                () => {
                    if (_activeNPC != null) _activeNPC.AdvanceConversation();
                }
            );
        }

        public void HideDialogue()
        {
            _isShowingDialogue = false;
            _activeNPC = null;
            CyberpunkDialogueHUD.Instance?.CloseDialogue();
        }

        /// <summary>
        /// Shows a cinematic comms / internal-log card. Auto-dismisses after the duration.
        /// If a dialogue card is open the transmission is queued until it closes.
        /// </summary>
        public void ShowTransmission(string speaker, string role, string text, Color color, float duration = 6f)
        {
            if (_isShowingDialogue)
            {
                _hasPendingTransmission = true;
                _pendingSpeaker = speaker;
                _pendingRole = role;
                _pendingText = text;
                _pendingColor = color;
                _pendingDuration = duration;
                return;
            }

            BeginTransmission(speaker, role, text, color, duration);
        }

        private void BeginTransmission(string speaker, string role, string text, Color color, float duration)
        {
            _txSpeaker = speaker;
            _txRole = role;
            _txText = text;
            _txColor = color;
            _txTimer = Mathf.Max(1f, duration);
            _isShowingTransmission = true;

            CyberpunkDialogueHUD.GetOrCreate().ShowDialogue(
                speaker,
                text,
                null,
                role,
                null,
                duration
            );
        }

        public void ShowMissionBanner(string missionText)
        {
            _bannerText = missionText;
            _bannerTimer = BANNER_DURATION;
            _isShowingBanner = true;
        }

        private void OnGUI()
        {
            if (SyntyCombined.Cutscene.GlobalCutsceneState.IsCutsceneActive) return;

            InitTextures();

            // 1. Mission Unlock Banner (Top Center)
            if (_isShowingBanner)
            {
                float bannerWidth = Mathf.Min(800f, Screen.width * 0.85f);
                float bannerHeight = 54f;
                float bannerX = (Screen.width - bannerWidth) * 0.5f;
                float bannerY = 24f;
                Rect bannerRect = new Rect(bannerX, bannerY, bannerWidth, bannerHeight);

                Color prevColor = GUI.color;
                GUI.color = Color.white;
                GUI.DrawTexture(bannerRect, _bannerBgTex);

                // Cyan border
                GUI.color = new Color(0.22f, 0.74f, 0.97f);
                GUI.Box(new Rect(bannerRect.x, bannerRect.y, bannerRect.width, 2f), GUIContent.none);
                GUI.Box(new Rect(bannerRect.x, bannerRect.y + bannerRect.height - 2f, bannerRect.width, 2f), GUIContent.none);

                GUIStyle bannerStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 15,
                    fontStyle = FontStyle.Bold
                };
                bannerStyle.normal.textColor = Color.white;
                GUI.Label(bannerRect, "🚀 " + _bannerText, bannerStyle);
                GUI.color = prevColor;
            }

            // 2. Transmissions & NPC Dialogues rendered via AAA CyberpunkDialogueHUD (Canvas uGUI)
        }
    }
}
