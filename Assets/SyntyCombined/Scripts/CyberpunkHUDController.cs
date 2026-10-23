using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SyntyCombined
{
    /// <summary>
    /// AAA Cyberpunk 2077 Authentic In-Game HUD (Kiroshi Optics AR Overlay).
    /// Replaces legacy debug text overlays with a diegetic, futuristic UI inspired by CD Projekt Red's Cyberpunk 2077.
    /// Features:
    /// 1. Top-Left: Kiroshi Bio-Monitor, Street Cred, red angled Health gauge, ghost lag fill, Sub-Dermal Armor overclock, Cyberdeck Cyber-RAM slots, Stamina.
    /// 2. Top-Right: District location banner, real-time clock, compass heading, NCPD threat alert level, high-tech chamfered radar frame with live 3D top-down Minimap.
    /// 3. Center-Right: Active Gig / Quest tracker with dynamic combat objectives and Cyber Yellow accent strip.
    /// 4. Bottom-Right: Active Weapon diagnostic card (Arasaka Katana, Iconic tier, stance, combo readout, DPS telemetry), MaxDoc Mk.3 hypo pack.
    /// 5. Bottom-Left: Contextual diegetic action chips ([TAB], [LMB], [RMB], [Q], [C], [SPACE], [V], [P], [ESC]) & Fixer holo-comms.
    /// 6. Center: Tactical Kiroshi AR Reticle with dynamic raycast rangefinder & target scanner.
    /// 7. Clean Screen: Zero camera scanline filters, zero CRT noise, clean edges without cluttered corner text.
    /// </summary>
    public class CyberpunkHUDController : MonoBehaviour
    {
        public static CyberpunkHUDController Instance { get; private set; }

        // ─── Cyberpunk 2077 Authentic Palette (CDPR HUD Painter values) ──────────
        // Yellow = stamina/nav/warning, Red = danger/health, Blue = quest/info, Green = ok.
        public static readonly Color CyberYellow    = new Color(0.988f, 0.933f, 0.039f, 1.0f); // #FCEE0A stamina/nav
        public static readonly Color NeonCrimson    = new Color(1.000f, 0.000f, 0.235f, 1.0f); // #FF003C health/danger
        public static readonly Color CrimsonDim     = new Color(0.12f, 0.10f, 0.10f, 0.92f); // HP empty (dark, neutral — not red)
        public static readonly Color KiroshiCyan    = new Color(0.000f, 0.847f, 1.000f, 1.0f); // quest blue
        public static readonly Color CyanDim        = new Color(0.050f, 0.180f, 0.230f, 0.90f); // Empty RAM cell
        public static readonly Color StanceOrange   = new Color(1.000f, 0.518f, 0.000f, 1.0f); // iconic tier
        public static readonly Color DarkObsidian   = new Color(0.035f, 0.035f, 0.040f, 0.88f); // flat near-black panel (CP2077 uses flat, not gradient)
        public static readonly Color TextWhite      = new Color(0.960f, 0.970f, 0.960f, 1.0f);
        public static readonly Color TextMuted      = new Color(0.620f, 0.660f, 0.680f, 1.0f);
        public static readonly Color OkGreen        = new Color(0.180f, 0.900f, 0.350f, 1.0f); // Area: Safe
        private static Sprite _angledCutSprite;
        private static Sprite _circleMaskSprite;

        [Header("Target Controller")]
        public SyntySwordLocomotionController playerController;
        private InsaneGun.Combat.InsaneGunSwordController _insaneHero;

        [Header("UI Resources Cache")]
        public TMP_FontAsset headerFont;
        public TMP_FontAsset boldFont;
        public TMP_FontAsset bodyFont;

        [Header("Master HUD Toggle")]
        [Tooltip("Master switch to enable or disable the entire Cyberpunk HUD overlay.")]
        public bool isHUDEnabled = true;

        [Header("HUD Visibility Options")]
        [Tooltip("Shows 'REIGN IN NIGHT CITY' quest tracker below the minimap")]
        public bool showQuestTracker = true;
        [Tooltip("Shows middle AR reticle, rangefinder, and hostile scanner text")]
        public bool showCenterReticle = true;
        [Tooltip("Shows bottom-middle action chips key prompts strip")]
        public bool showActionChips = true;

        // Top-Left References
        private RectTransform _healthFillRect;
        private RectTransform _ghostHealthFillRect;
        private TextMeshProUGUI _healthText;
        private TextMeshProUGUI _maxHealthText;
        private TextMeshProUGUI _levelBadgeText;
        private RectTransform _topShieldBarRect;
        private Image _topShieldBarImage;
        private GameObject _armorBadgeObj;
        private RectTransform _staminaFillRect;
        private TextMeshProUGUI _staminaLabel;
        private Image[] _ramCellImages;
        private float _currentStamina = 1.0f;
        private float _ghostHealthRatio = 1.0f;
        private float _lastHp = -1f;

        private static Sprite _chamferHealthBarSprite;
        private static Sprite _chamferLevelBoxSprite;
        private static Sprite _sideVignetteSprite;

        // Top-Right References
        private TextMeshProUGUI _clockText;
        private TextMeshProUGUI _compassText;
        private TextMeshProUGUI _threatBadgeText;
        private Image _threatBadgeBg;
        public RawImage minimapRawImage;
        public RectTransform playerIconRect;
        public Transform enemyBlipsContainer;
        private MinimapController _minimapCtrl;
        private Image _minimapRingImage;
        private GameObject[] _minimapBorderObjs;
        private GameObject _noSignalOverlay;
        private TextMeshProUGUI _noSignalTitle;
        private TextMeshProUGUI _noSignalSubtitle;
        private GameObject _northMarkerObj;

        // Center-Right References
        private TextMeshProUGUI _questGigTitleText;
        private TextMeshProUGUI _questObjectiveText;

        // Bottom-Right References
        private TextMeshProUGUI _weaponNameText;
        private TextMeshProUGUI _weaponTierText;
        private TextMeshProUGUI _weaponStanceText;
        private TextMeshProUGUI _weaponActionText;
        private TextMeshProUGUI _hypoCountText;
        private TextMeshProUGUI _gadgetCountText;
        private int _maxDocCount = 3;

        // Center Reticle References
        private RectTransform _reticleLeftBracket;
        private RectTransform _reticleRightBracket;
        private TextMeshProUGUI _rangefinderText;
        private TextMeshProUGUI _targetScannerText;
        private TextMeshProUGUI _perspectiveChipText;

        // Screen Overlays
        private Image _lowHealthVignette;
        private Image _damageFlash;
        private GameObject _deathOverlayObj;
        private TextMeshProUGUI _deathText;
        private GameObject _toastBannerObj;
        private TextMeshProUGUI _toastBannerText;
        private float _toastTimer = 0f;

        // Boss bar (top-center)
        private GameObject _bossBarObj;
        private RectTransform _bossFillRect;
        private TextMeshProUGUI _bossNameText;

        // World Telemetry Simulation
        private float _inGameMinutes = 639f; // 10:39 AM in minutes
        private Camera _mainCam;
        private CanvasGroup _canvasGroup;

        private void Awake()
        {
            if (!isHUDEnabled)
            {
                gameObject.SetActive(false);
                return;
            }
            Instance = this;
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            LoadFonts();
        }

        private void Start()
        {
            if (!isHUDEnabled) return;
            FindPlayerReference();
            _mainCam = Camera.main;
            BuildCyberpunkHUD();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void LoadFonts()
        {
            headerFont = Resources.Load<TMP_FontAsset>("SyntyUI/Fonts/Font_SciFi_Header");
            boldFont   = Resources.Load<TMP_FontAsset>("SyntyUI/Fonts/Font_SciFi_Bold");
            bodyFont   = Resources.Load<TMP_FontAsset>("SyntyUI/Fonts/Font_SciFi_Body");
        }

        private void FindPlayerReference()
        {
            if (playerController == null)
            {
                playerController = Object.FindAnyObjectByType<SyntySwordLocomotionController>();
            }
            if (_insaneHero == null)
            {
                var heroes = Object.FindObjectsByType<InsaneGun.Combat.InsaneGunSwordController>(FindObjectsInactive.Exclude);
                foreach (var h in heroes)
                {
                    if (h.controlMode == InsaneGun.Combat.InsaneGunSwordController.ControlMode.PlayerControlled || h.CompareTag("Player"))
                    {
                        _insaneHero = h;
                        break;
                    }
                }
            }
        }

        public void SetQuestObjective(string gigTitle, string objectiveText)
        {
            if (_questGigTitleText != null && !string.IsNullOrEmpty(gigTitle))
            {
                string clean = gigTitle.TrimStart('/', ' ', '#').Trim();
                _questGigTitleText.text = clean.ToUpper();
            }
            if (_questObjectiveText != null && !string.IsNullOrEmpty(objectiveText))
            {
                string sanitized = objectiveText
                    .Replace("►", "")
                    .Replace("◆", "")
                    .Replace("\u25C6", "")
                    .TrimStart(' ', '-', '>');
                _questObjectiveText.text = sanitized;
                _questObjectiveText.color = KiroshiCyan;
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // BUILD CYBERPUNK 2077 HUD CANVAS
        // ─────────────────────────────────────────────────────────────────────────
        public void BuildCyberpunkHUD()
        {
            // Clean up any existing children from this canvas
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = transform.GetChild(i).gameObject;
                if (Application.isPlaying)
                    Destroy(child);
                else
                    DestroyImmediate(child);
            }

            // 1. Background Cinematic Overlays (Vignette & Damage Flash sit behind HUD panels)
            BuildCinematicOverlays();

            // 2. Interactive HUD Panels (Render cleanly on top of screen edge overlays)
            BuildTopLeftBioMonitor();
            BuildTopRightTelemetryAndMinimap();
            BuildTopCenterBossBar();
            if (showQuestTracker) BuildCenterRightQuestTracker();
            BuildBottomRightWeaponDiagnostics();
            if (showActionChips) BuildBottomLeftActionChips();
            if (showCenterReticle) BuildCenterKiroshiReticle();

            // 3. Foreground banners (Death flatline & Toast banner sit on top of all HUD)
            if (_deathOverlayObj != null) _deathOverlayObj.transform.SetAsLastSibling();
            if (_toastBannerObj != null) _toastBannerObj.transform.SetAsLastSibling();

            ShowToast("NEURAL LINK ONLINE — MOTOR MATRIX V4.2", 3.5f);
        }

        // ─── 1. TOP-LEFT: CYBERPUNK 2077 HEALTH BAR & BIOMONITOR ──────────────────
        private void BuildTopLeftBioMonitor()
        {
            GameObject root = CreateAnchorPanel("Zone_TopLeft_BioMonitor",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(28f, -24f), new Vector2(560f, 80f));

            // 1. Level Badge (Left): Chamfered bottom-left corner with thin cyan outline, displaying level "8"
            GameObject lvlBox = CreateChild(root, "Badge_Level", new Vector2(0f, 0f), new Vector2(32f, 32f));
            RectTransform lvlRt = lvlBox.GetComponent<RectTransform>();
            lvlRt.anchorMin = new Vector2(0f, 1f);
            lvlRt.anchorMax = new Vector2(0f, 1f);
            lvlRt.pivot = new Vector2(0f, 1f);
            lvlRt.anchoredPosition = new Vector2(0f, 0f);
            Image lvlImg = lvlBox.AddComponent<Image>();
            lvlImg.sprite = GetChamferLevelBoxSprite();
            lvlImg.type = Image.Type.Sliced;
            _levelBadgeText = CreateText(lvlBox, "Text_Level", "8", 15f, TextAlignmentOptions.Center, Vector2.zero, Color.white, boldFont);
            _levelBadgeText.fontStyle = FontStyles.Bold;
            SetFullStretch(_levelBadgeText.gameObject);

            // X-offset for the health bar components: right next to level box
            float barStartX = 38f;

            // 2. Top Rail Line & Cyberware Shield Bar (Above Health Bar)
            // Cyberware shield bar (~75px wide x 3.5px high cyan) + dark rail line (~255px)
            GameObject topRailContainer = CreateChild(root, "Container_TopRail", new Vector2(barStartX, 0f), new Vector2(330f, 4f));
            RectTransform railRt = topRailContainer.GetComponent<RectTransform>();
            railRt.anchorMin = new Vector2(0f, 1f);
            railRt.anchorMax = new Vector2(0f, 1f);
            railRt.pivot = new Vector2(0f, 1f);
            railRt.anchoredPosition = new Vector2(barStartX, 0f);

            GameObject shieldObj = CreateChild(topRailContainer, "Bar_TopShield", Vector2.zero, new Vector2(75f, 3.5f));
            RectTransform shieldRt = shieldObj.GetComponent<RectTransform>();
            shieldRt.anchorMin = new Vector2(0f, 0.5f);
            shieldRt.anchorMax = new Vector2(0f, 0.5f);
            shieldRt.pivot = new Vector2(0f, 0.5f);
            shieldRt.anchoredPosition = Vector2.zero;
            shieldRt.sizeDelta = new Vector2(75f, 3.5f);
            Image shieldImg = shieldObj.AddComponent<Image>();
            shieldImg.color = new Color(0.0f, 0.94f, 1.0f, 1.0f); // #00F0FF cyan
            _topShieldBarRect = shieldRt;
            _topShieldBarImage = shieldImg;

            GameObject railLineObj = CreateChild(topRailContainer, "Line_TopRail", new Vector2(76f, 0f), new Vector2(254f, 1.5f));
            RectTransform lineRt = railLineObj.GetComponent<RectTransform>();
            lineRt.anchorMin = new Vector2(0f, 0.5f);
            lineRt.anchorMax = new Vector2(0f, 0.5f);
            lineRt.pivot = new Vector2(0f, 0.5f);
            lineRt.anchoredPosition = new Vector2(76f, 0f);
            lineRt.sizeDelta = new Vector2(254f, 1.5f);
            Image railLineImg = railLineObj.AddComponent<Image>();
            railLineImg.color = new Color(0.24f, 0.28f, 0.32f, 0.85f); // subtle dark rail

            // 3. Main Health Bar Container (~330px wide, ~18px high, chamfered bottom-right)
            GameObject hpContainer = CreateChild(root, "Container_Health", new Vector2(barStartX, -5f), new Vector2(330f, 18f));
            RectTransform hpContainerRt = hpContainer.GetComponent<RectTransform>();
            hpContainerRt.anchorMin = new Vector2(0f, 1f);
            hpContainerRt.anchorMax = new Vector2(0f, 1f);
            hpContainerRt.pivot = new Vector2(0f, 1f);
            hpContainerRt.anchoredPosition = new Vector2(barStartX, -5f);
            hpContainerRt.sizeDelta = new Vector2(330f, 18f);

            // Container background is dark burgundy/crimson (#4C141B)
            Image hpBg = hpContainer.AddComponent<Image>();
            hpBg.sprite = GetChamferHealthBarSprite();
            hpBg.type = Image.Type.Sliced;
            hpBg.color = new Color(0.30f, 0.08f, 0.11f, 0.95f); // #4C141B dark burgundy

            // Mask so active fill and ghost lag are trimmed to the bottom-right chamfer
            Mask hpMask = hpContainer.AddComponent<Mask>();
            hpMask.showMaskGraphic = true;

            // Ghost damage lag bar (trailing soft light-red)
            GameObject ghostHpBar = CreateChild(hpContainer, "Bar_GhostHealth", Vector2.zero, new Vector2(330f, 18f));
            SetFullStretch(ghostHpBar);
            Image ghostImg = ghostHpBar.AddComponent<Image>();
            ghostImg.color = new Color(1.0f, 0.42f, 0.46f, 0.40f);
            _ghostHealthFillRect = ghostHpBar.GetComponent<RectTransform>();
            _ghostHealthFillRect.pivot = new Vector2(0f, 0.5f);

            // Active neon red health fill (#FF3A44)
            GameObject hpBar = CreateChild(hpContainer, "Bar_ActiveHealth", Vector2.zero, new Vector2(330f, 18f));
            SetFullStretch(hpBar);
            Image hpImg = hpBar.AddComponent<Image>();
            hpImg.color = new Color(1.0f, 0.23f, 0.27f, 1.0f); // #FF3A44 neon coral-crimson
            _healthFillRect = hpBar.GetComponent<RectTransform>();
            _healthFillRect.pivot = new Vector2(0f, 0.5f);

            // 4. Cyberware RAM / Stamina Ticks (Bottom Left - 5 vertical cyan bars |||||)
            GameObject ticksContainer = CreateChild(root, "Container_CyberTicks", new Vector2(barStartX, -25f), new Vector2(75f, 10f));
            RectTransform ticksRt = ticksContainer.GetComponent<RectTransform>();
            ticksRt.anchorMin = new Vector2(0f, 1f);
            ticksRt.anchorMax = new Vector2(0f, 1f);
            ticksRt.pivot = new Vector2(0f, 1f);
            ticksRt.anchoredPosition = new Vector2(barStartX, -25f);

            _ramCellImages = new Image[5];
            float tickW = 4.0f;
            float tickH = 9.0f;
            float tickGap = 3.5f;
            for (int r = 0; r < 5; r++)
            {
                float xPos = r * (tickW + tickGap);
                GameObject tickObj = CreateChild(ticksContainer, $"Tick_{r}", new Vector2(xPos, 0f), new Vector2(tickW, tickH));
                RectTransform tRt = tickObj.GetComponent<RectTransform>();
                tRt.anchorMin = new Vector2(0f, 0.5f);
                tRt.anchorMax = new Vector2(0f, 0.5f);
                tRt.pivot = new Vector2(0f, 0.5f);
                tRt.anchoredPosition = new Vector2(xPos, 0f);
                tRt.sizeDelta = new Vector2(tickW, tickH);
                Image tImg = tickObj.AddComponent<Image>();
                tImg.color = new Color(0.0f, 0.94f, 1.0f, 1.0f); // #00F0FF cyan
                _ramCellImages[r] = tImg;
            }

            // 5. Numeric Readout (Right): Current HP (large bold red) + Max HP (smaller raised light-coral)
            GameObject hpTextObj = CreateChild(root, "Text_NumericHP", new Vector2(barStartX + 342f, -2f), new Vector2(180f, 26f));
            RectTransform txtRt = hpTextObj.GetComponent<RectTransform>();
            txtRt.anchorMin = new Vector2(0f, 1f);
            txtRt.anchorMax = new Vector2(0f, 1f);
            txtRt.pivot = new Vector2(0f, 1f);
            txtRt.anchoredPosition = new Vector2(barStartX + 342f, -2f);
            txtRt.sizeDelta = new Vector2(180f, 26f);

            GameObject curHpObj = CreateChild(hpTextObj, "Text_CurHP", new Vector2(0f, 0f), new Vector2(62f, 26f));
            RectTransform curRt = curHpObj.GetComponent<RectTransform>();
            curRt.anchorMin = new Vector2(0f, 0.5f);
            curRt.anchorMax = new Vector2(0f, 0.5f);
            curRt.pivot = new Vector2(0f, 0.5f);
            curRt.anchoredPosition = new Vector2(0f, 0f);
            curRt.sizeDelta = new Vector2(62f, 26f);
            _healthText = CreateText(curHpObj, "Text_Val", "284", 22f, TextAlignmentOptions.Left, Vector2.zero, new Color(1.0f, 0.22f, 0.28f, 1.0f), boldFont);
            _healthText.fontStyle = FontStyles.Bold;

            GameObject maxHpObj = CreateChild(hpTextObj, "Text_MaxHP", new Vector2(64f, 2.5f), new Vector2(70f, 20f));
            RectTransform maxRt = maxHpObj.GetComponent<RectTransform>();
            maxRt.anchorMin = new Vector2(0f, 0.5f);
            maxRt.anchorMax = new Vector2(0f, 0.5f);
            maxRt.pivot = new Vector2(0f, 0.5f);
            maxRt.anchoredPosition = new Vector2(64f, 2.5f);
            maxRt.sizeDelta = new Vector2(70f, 20f);
            _maxHealthText = CreateText(maxHpObj, "Text_Val", "/ 350", 14f, TextAlignmentOptions.Left, Vector2.zero, new Color(1.0f, 0.45f, 0.50f, 1.0f), boldFont);
            _maxHealthText.fontStyle = FontStyles.Bold;

            // Subdermal armor badge (godmode badge, hidden unless godmode)
            _armorBadgeObj = CreateChild(root, "Badge_SubDermalArmor", new Vector2(barStartX, -44f), new Vector2(250f, 18f));
            RectTransform abRt = _armorBadgeObj.GetComponent<RectTransform>();
            abRt.anchorMin = new Vector2(0f, 1f);
            abRt.anchorMax = new Vector2(0f, 1f);
            abRt.pivot = new Vector2(0f, 1f);
            abRt.anchoredPosition = new Vector2(barStartX, -44f);
            Image armorBg = _armorBadgeObj.AddComponent<Image>();
            armorBg.color = CyberYellow;
            ApplyAngledCut(armorBg, 6f);
            CreateText(_armorBadgeObj, "Text_Armor", "SUB-DERMAL OVERCLOCKED", 10f, TextAlignmentOptions.Center, Vector2.zero, Color.black, boldFont);
            _armorBadgeObj.SetActive(false);
        }

        // ─── 2. TOP-RIGHT: 2077 MINIMAP + TIME + COMBAT RING ──────────────────────
        private void BuildTopRightTelemetryAndMinimap()
        {
            GameObject root = CreateAnchorPanel("Zone_TopRight_TelemetryMinimap",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-24f, -20f), new Vector2(280f, 330f));

            // SQUARE minimap: dark frame + square RawImage + 2px border (border color = combat state).
            // Pinned to root's top-right with fixed anchors so it can never leave the screen.
            GameObject minimapFrame = CreateChild(root, "Frame_Minimap", new Vector2(0f, -28f), new Vector2(230f, 230f));
            PinTopRight(minimapFrame, -28f);
            Image frameBg = minimapFrame.AddComponent<Image>();
            frameBg.color = new Color(0.02f, 0.05f, 0.10f, 1f); // minimap camera clear color
            _minimapRingImage = frameBg;

            // Time — directly above top-left corner of the minimap box, left-aligned in electric cyan
            GameObject timeObj = CreateChild(root, "Text_Time", new Vector2(-230f, -4f), new Vector2(160f, 22f));
            RectTransform timeRt = timeObj.GetComponent<RectTransform>();
            timeRt.anchorMin = new Vector2(1f, 1f);
            timeRt.anchorMax = new Vector2(1f, 1f);
            timeRt.pivot = new Vector2(0f, 1f);
            timeRt.anchoredPosition = new Vector2(-230f, -4f);
            timeRt.sizeDelta = new Vector2(160f, 22f);
            _clockText = CreateText(timeObj, "Text_Clock", "10:39 AM", 15f, TextAlignmentOptions.Left, Vector2.zero, KiroshiCyan, boldFont);
            _clockText.fontStyle = FontStyles.Bold;

            // Hidden district & compass objects (kept initialized so no null-refs occur in logic)
            GameObject distObj = CreateChild(root, "Hidden_District", Vector2.zero, new Vector2(10f, 10f));
            distObj.SetActive(false);
            GameObject hiddenCompass = CreateChild(root, "Hidden_Compass", Vector2.zero, new Vector2(10f, 10f));
            hiddenCompass.SetActive(false);
            _compassText = CreateText(hiddenCompass, "Txt", "", 8f, TextAlignmentOptions.Left, Vector2.zero, TextMuted, headerFont);

            // Hidden threat status object (border pulse on minimap itself handles combat alerts)
            GameObject threatObj = CreateChild(root, "Hidden_ThreatStatus", Vector2.zero, new Vector2(10f, 10f));
            threatObj.SetActive(false);
            _threatBadgeBg = threatObj.AddComponent<Image>();
            _threatBadgeBg.color = new Color(0.05f, 0.05f, 0.05f, 0.85f);
            _threatBadgeText = CreateText(threatObj, "Text_Threat", "AREA: SAFE", 11f, TextAlignmentOptions.Right, Vector2.zero, OkGreen, boldFont);
            _threatBadgeText.fontStyle = FontStyles.Bold;

            GameObject rawImgObj = CreateChild(minimapFrame, "RawImage_Minimap", Vector2.zero, new Vector2(226f, 226f));
            RectTransform rawRt = rawImgObj.GetComponent<RectTransform>();
            rawRt.anchorMin = new Vector2(0.5f, 0.5f);
            rawRt.anchorMax = new Vector2(0.5f, 0.5f);
            rawRt.pivot = new Vector2(0.5f, 0.5f);
            rawRt.anchoredPosition = Vector2.zero;
            minimapRawImage = rawImgObj.AddComponent<RawImage>();
            minimapRawImage.color = Color.white;
            AddBorder(minimapFrame, new Color(0.25f, 0.25f, 0.25f, 0.9f), 2f);
            _minimapBorderObjs = new GameObject[4];
            int bi = 0;
            foreach (Transform ch in minimapFrame.transform)
            {
                if (ch.name.StartsWith("Border_") && bi < 4) _minimapBorderObjs[bi++] = ch.gameObject;
            }

            // Player arrow + enemy blips inside square view (same wiring as before)
            GameObject playerIconObj = CreateChild(rawImgObj, "Icon_Player", Vector2.zero, new Vector2(16f, 16f));
            Image pBlipImg = playerIconObj.AddComponent<Image>();
            pBlipImg.color = TextWhite;
            Sprite coneSprite = Resources.Load<Sprite>("SyntyUI/Sprites/HUD/SPR_HUD_SciFi_Map_PlayerViewCone");
            if (coneSprite != null) { pBlipImg.sprite = coneSprite; }
            playerIconRect = playerIconObj.GetComponent<RectTransform>();
            playerIconRect.anchoredPosition = Vector2.zero;

            GameObject enemyBlips = CreateChild(rawImgObj, "Container_EnemyBlips", Vector2.zero, new Vector2(226f, 226f));
            SetFullStretch(enemyBlips);
            enemyBlipsContainer = enemyBlips.transform;

            GameObject northObj = CreateChild(minimapFrame, "Marker_N", new Vector2(0f, 100f), new Vector2(24f, 18f));
            RectTransform nRt = northObj.GetComponent<RectTransform>();
            nRt.anchorMin = new Vector2(0.5f, 1f);
            nRt.anchorMax = new Vector2(0.5f, 1f);
            nRt.pivot = new Vector2(0.5f, 1f);
            nRt.anchoredPosition = new Vector2(0f, 100f);
            TextMeshProUGUI nTxt = CreateText(northObj, "Txt_N", "N", 13f, TextAlignmentOptions.Center, Vector2.zero, TextWhite, boldFont);
            _northMarkerObj = northObj;

            // ── NO SIGNAL OVERLAY (Active inside Dr. Ido's Lab / Underground) ──
            _noSignalOverlay = CreateChild(minimapFrame, "Overlay_NoSignal", Vector2.zero, new Vector2(226f, 226f));
            SetFullStretch(_noSignalOverlay);
            Image noSigBg = _noSignalOverlay.AddComponent<Image>();
            noSigBg.color = new Color(0.015f, 0.035f, 0.07f, 1f);

            GameObject iconObj = CreateChild(_noSignalOverlay, "Icon_Warning", new Vector2(0f, 34f), new Vector2(200f, 22f));
            var iconTxt = CreateText(iconObj, "Txt_Warn", "▲ GPS OFFLINE ▲", 11f, TextAlignmentOptions.Center, Vector2.zero, new Color(1f, 0.35f, 0.35f, 0.95f), boldFont);
            iconTxt.fontStyle = FontStyles.Bold;

            GameObject titleObj = CreateChild(_noSignalOverlay, "Text_NoSignal", new Vector2(0f, 2f), new Vector2(210f, 38f));
            _noSignalTitle = CreateText(titleObj, "Txt_Title", "NO SIGNAL", 21f, TextAlignmentOptions.Center, Vector2.zero, new Color(1f, 0.22f, 0.22f, 1f), boldFont);
            _noSignalTitle.fontStyle = FontStyles.Bold;

            GameObject subObj = CreateChild(_noSignalOverlay, "Text_NoSignalSub", new Vector2(0f, -36f), new Vector2(210f, 32f));
            _noSignalSubtitle = CreateText(subObj, "Txt_Sub", "LOCAL RELAY BLOCKED\n// UNDERGROUND CLINIC", 9f, TextAlignmentOptions.Center, Vector2.zero, new Color(0.42f, 0.65f, 0.75f, 0.9f), headerFont);

            // Connect to MinimapController
            _minimapCtrl = GetComponent<MinimapController>() ?? Object.FindAnyObjectByType<MinimapController>();
            if (_minimapCtrl == null)
            {
                _minimapCtrl = gameObject.AddComponent<MinimapController>();
            }
            if (_minimapCtrl != null)
            {
                _minimapCtrl.SetupMinimapCamera();
                _minimapCtrl.SetMinimapTarget(minimapRawImage, playerIconRect, enemyBlipsContainer);
            }
        }

        // ─── 2b. TOP-CENTER: BOSS HP BAR (hidden until SetBossActive) ───────────
        private void BuildTopCenterBossBar()
        {
            GameObject root = CreateAnchorPanel("Zone_TopCenter_BossBar",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -24f), new Vector2(560f, 54f));

            _bossNameText = CreateText(root, "Text_BossName", "GOLIATH-02 // TITAN MECH", 13f,
                TextAlignmentOptions.Center, new Vector2(0f, 14f), NeonCrimson, boldFont);
            _bossNameText.fontStyle = FontStyles.Bold;
            SetAnchorTop(_bossNameText.gameObject, -2f);

            GameObject barBg = CreateChild(root, "Bar_BossBg", new Vector2(0f, -30f), new Vector2(560f, 14f));
            SetAnchorTop(barBg, -26f);
            Image bgImg = barBg.AddComponent<Image>();
            bgImg.color = new Color(0.10f, 0.02f, 0.03f, 0.95f);
            AddBorder(barBg, NeonCrimson, 1.2f);

            GameObject fill = CreateChild(barBg, "Bar_BossFill", Vector2.zero, new Vector2(560f, 14f));
            SetFullStretch(fill);
            Image fillImg = fill.AddComponent<Image>();
            fillImg.color = NeonCrimson;
            _bossFillRect = fill.GetComponent<RectTransform>();

            _bossBarObj = root;
            _bossBarObj.SetActive(false);
        }

        public void SetBossActive(string bossName)
        {
            if (_bossBarObj != null) _bossBarObj.SetActive(true);
            if (_bossNameText != null && !string.IsNullOrEmpty(bossName))
                _bossNameText.text = bossName.ToUpper();
        }

        public void SetBossHealth(float ratio, string bossName = null)
        {
            if (_bossBarObj != null && !_bossBarObj.activeSelf) _bossBarObj.SetActive(true);
            if (_bossFillRect != null) _bossFillRect.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
            if (bossName != null && _bossNameText != null) _bossNameText.text = bossName.ToUpper();
            if (ratio <= 0f && _bossBarObj != null) _bossBarObj.SetActive(false);
        }

        public void HideBossBar()
        {
            if (_bossBarObj != null) _bossBarObj.SetActive(false);
        }

        // ─── 3. RIGHT: 2077 QUEST TRACKER (directly below minimap, cyan [!] badge) ─
        private void BuildCenterRightQuestTracker()
        {
            // Anchored directly underneath the minimap box (-24f X, -288f Y)
            GameObject root = CreateAnchorPanel("Zone_CenterRight_GigTracker",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-24f, -288f), new Vector2(230f, 140f));

            // Gig / Mission Title: bold electric cyan, left-aligned with minimap edge
            GameObject titleObj = CreateChild(root, "Obj_GigTitle", new Vector2(0f, 0f), new Vector2(230f, 22f));
            RectTransform titleRt = titleObj.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0f, 1f);
            titleRt.anchoredPosition = Vector2.zero;
            titleRt.sizeDelta = new Vector2(0f, 22f);

            _questGigTitleText = CreateText(titleObj, "Text_GigTitle", "GIG: WAKAKO'S FAVORITE", 13f, TextAlignmentOptions.Left, Vector2.zero, KiroshiCyan, boldFont);
            _questGigTitleText.fontStyle = FontStyles.Bold;
            SetFullStretch(_questGigTitleText.gameObject);

            // Objective Row: starts directly below the title (Y = -24f)
            GameObject objRow = CreateChild(root, "Row_Objective", new Vector2(0f, -24f), new Vector2(230f, 110f));
            RectTransform rowRt = objRow.GetComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0f, 1f);
            rowRt.anchorMax = new Vector2(1f, 1f);
            rowRt.pivot = new Vector2(0f, 1f);
            rowRt.anchoredPosition = new Vector2(0f, -24f);
            rowRt.sizeDelta = new Vector2(0f, 110f);

            // Chamfered Cyan Alert Badge [ ! ]
            GameObject badgeObj = CreateChild(objRow, "Badge_QuestAlert", new Vector2(0f, -2f), new Vector2(17f, 17f));
            RectTransform badgeRt = badgeObj.GetComponent<RectTransform>();
            badgeRt.anchorMin = new Vector2(0f, 1f);
            badgeRt.anchorMax = new Vector2(0f, 1f);
            badgeRt.pivot = new Vector2(0f, 1f);
            badgeRt.anchoredPosition = new Vector2(0f, -2f);
            badgeRt.sizeDelta = new Vector2(17f, 17f);

            Image badgeImg = badgeObj.AddComponent<Image>();
            badgeImg.sprite = GetQuestAlertBadgeSprite();
            badgeImg.color = KiroshiCyan;

            TextMeshProUGUI exclText = CreateText(badgeObj, "Text_Excl", "!", 12f, TextAlignmentOptions.Center, Vector2.zero, Color.black, boldFont);
            exclText.fontStyle = FontStyles.Bold;
            SetFullStretch(exclText.gameObject);

            // Objective Body Text: padded 23px to the right of the badge, wrapping cleanly in cyan
            GameObject textObj = CreateChild(objRow, "Container_Text", new Vector2(23f, 0f), new Vector2(207f, 110f));
            RectTransform textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0f, 1f);
            textRt.anchorMax = new Vector2(1f, 1f);
            textRt.pivot = new Vector2(0f, 1f);
            textRt.anchoredPosition = new Vector2(23f, 0f);
            textRt.sizeDelta = new Vector2(0f, 110f);

            _questObjectiveText = CreateText(textObj, "Text_Objective",
                "Find out what happened to\nChang-Hoon Nam.",
                11.5f, TextAlignmentOptions.Left, Vector2.zero, KiroshiCyan, bodyFont);
            _questObjectiveText.textWrappingMode = TextWrappingModes.Normal;
            _questObjectiveText.lineSpacing = 16f;
            SetFullStretch(_questObjectiveText.gameObject);
        }

        // ─── 4. BOTTOM-RIGHT: 2077 WEAPON DIAGNOSTICS CARD ────────────────────────
        private void BuildBottomRightWeaponDiagnostics()
        {
            GameObject root = CreateAnchorPanel("Zone_BottomRight_WeaponDiagnostics",
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-24f, 24f), new Vector2(360f, 88f));

            GameObject weaponCard = CreateChild(root, "Card_ActiveWeapon", Vector2.zero, new Vector2(360f, 84f));
            SetAnchorBottom(weaponCard, 0f);
            Image cardBg = weaponCard.AddComponent<Image>();
            cardBg.color = new Color(0.04f, 0.04f, 0.045f, 0.85f);
            ApplyAngledCut(cardBg, 16f);

            // Yellow top tick
            GameObject tick = CreateChild(weaponCard, "Tick_Top", new Vector2(0f, 0f), new Vector2(360f, 4f));
            RectTransform tickRt = tick.GetComponent<RectTransform>();
            tickRt.anchorMin = new Vector2(0f, 1f); tickRt.anchorMax = new Vector2(1f, 1f); tickRt.pivot = new Vector2(0.5f, 1f);
            Image tickImg = tick.AddComponent<Image>();
            tickImg.color = CyberYellow;

            _weaponNameText = CreateText(weaponCard, "Text_WeaponName", "TSUNAGI THERMAL BLADE", 15f, TextAlignmentOptions.Left, new Vector2(14f, 20f), TextWhite, boldFont);
            _weaponNameText.fontStyle = FontStyles.Bold;

            _weaponTierText = CreateText(weaponCard, "Text_WeaponTier", "TIER 5++ ICONIC", 10.5f, TextAlignmentOptions.Left, new Vector2(14f, 2f), CyberYellow, boldFont);

            _weaponStanceText = CreateText(weaponCard, "Text_StanceBadge", "DRAWN", 12f, TextAlignmentOptions.Right, new Vector2(-14f, 2f), OkGreen, boldFont);
            _weaponStanceText.fontStyle = FontStyles.Bold;

            _weaponActionText = CreateText(weaponCard, "Text_LiveAction", "IDLE STANCE", 11.5f, TextAlignmentOptions.Left, new Vector2(14f, -18f), TextWhite, headerFont);

            CreateText(weaponCard, "Text_Stats", "DPS 642  •  SPD 2.20  •  RNG DYN", 9.5f, TextAlignmentOptions.Left, new Vector2(14f, -34f), TextMuted, headerFont);
        }

        // ─── 5. BOTTOM-LEFT: AUTHENTIC 2077 INVENTORY QUICK SLOTS ─────────────────
        private void BuildBottomLeftActionChips()
        {
            BuildBottomLeftInventoryQuickSlots();
        }

        private void BuildBottomLeftInventoryQuickSlots()
        {
            GameObject root = CreateAnchorPanel("Zone_BottomLeft_QuickInventory",
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(28f, 26f), new Vector2(250f, 140f));

            // ─── LEFT COLUMN: PHONE (9+), VEHICLE, RADIO ──────────────────────────
            // 1. Phone / Fixer Comms Slot (Top of column)
            GameObject phonePrompt = CreateChild(root, "Prompt_Phone", new Vector2(0f, 84f), new Vector2(22f, 22f));
            RectTransform ppRt = phonePrompt.GetComponent<RectTransform>();
            ppRt.anchorMin = new Vector2(0f, 0f); ppRt.anchorMax = new Vector2(0f, 0f); ppRt.pivot = new Vector2(0f, 0f);
            Image ppImg = phonePrompt.AddComponent<Image>();
            ppImg.sprite = GetDPadCircleSprite(0, KiroshiCyan);

            GameObject phoneSlot = CreateChild(root, "Slot_Phone", new Vector2(26f, 76f), new Vector2(38f, 38f));
            RectTransform psRt = phoneSlot.GetComponent<RectTransform>();
            psRt.anchorMin = new Vector2(0f, 0f); psRt.anchorMax = new Vector2(0f, 0f); psRt.pivot = new Vector2(0f, 0f);
            Image psImg = phoneSlot.AddComponent<Image>();
            psImg.sprite = GetSlotBoxSprite();
            psImg.color = KiroshiCyan;
            psImg.type = Image.Type.Sliced;

            GameObject phoneIcon = CreateChild(phoneSlot, "Icon_Phone", Vector2.zero, new Vector2(24f, 24f));
            SetFullStretch(phoneIcon);
            Image piImg = phoneIcon.AddComponent<Image>();
            piImg.sprite = GetPhoneIconSprite();
            piImg.color = Color.white;

            // Cyan notification badge: "9+" on top-right of phone slot
            GameObject badgePhone = CreateChild(phoneSlot, "Badge_Unread", new Vector2(32f, 34f), new Vector2(22f, 13f));
            RectTransform bpRt = badgePhone.GetComponent<RectTransform>();
            bpRt.anchorMin = new Vector2(0f, 0f); bpRt.anchorMax = new Vector2(0f, 0f); bpRt.pivot = new Vector2(0.5f, 0.5f);
            bpRt.anchoredPosition = new Vector2(32f, 34f);
            Image bpImg = badgePhone.AddComponent<Image>();
            bpImg.sprite = GetQuestAlertBadgeSprite();
            bpImg.color = KiroshiCyan;
            TextMeshProUGUI bpTxt = CreateText(badgePhone, "Txt_Count", "9+", 9.5f, TextAlignmentOptions.Center, Vector2.zero, Color.black, boldFont);
            bpTxt.fontStyle = FontStyles.Bold;
            SetFullStretch(bpTxt.gameObject);

            // 2. Vehicle Summon Slot (Middle of column)
            GameObject carPrompt = CreateChild(root, "Prompt_Vehicle", new Vector2(0f, 42f), new Vector2(22f, 22f));
            RectTransform cpRt = carPrompt.GetComponent<RectTransform>();
            cpRt.anchorMin = new Vector2(0f, 0f); cpRt.anchorMax = new Vector2(0f, 0f); cpRt.pivot = new Vector2(0f, 0f);
            Image cpImg = carPrompt.AddComponent<Image>();
            cpImg.sprite = GetDPadCircleSprite(1, KiroshiCyan);

            GameObject carSlot = CreateChild(root, "Slot_Vehicle", new Vector2(26f, 34f), new Vector2(38f, 38f));
            RectTransform csRt = carSlot.GetComponent<RectTransform>();
            csRt.anchorMin = new Vector2(0f, 0f); csRt.anchorMax = new Vector2(0f, 0f); csRt.pivot = new Vector2(0f, 0f);
            Image csImg = carSlot.AddComponent<Image>();
            csImg.sprite = GetSlotBoxSprite();
            csImg.color = KiroshiCyan;
            csImg.type = Image.Type.Sliced;

            GameObject carIcon = CreateChild(carSlot, "Icon_Car", Vector2.zero, new Vector2(28f, 16f));
            SetFullStretch(carIcon);
            Image ciImg = carIcon.AddComponent<Image>();
            ciImg.sprite = GetVehicleIconSprite();
            ciImg.color = Color.white;

            // 3. Radio Station Slot (Bottom of column - RED)
            GameObject radioPrompt = CreateChild(root, "Prompt_Radio", new Vector2(0f, 0f), new Vector2(22f, 22f));
            RectTransform rpRt = radioPrompt.GetComponent<RectTransform>();
            rpRt.anchorMin = new Vector2(0f, 0f); rpRt.anchorMax = new Vector2(0f, 0f); rpRt.pivot = new Vector2(0f, 0f);
            Image rpImg = radioPrompt.AddComponent<Image>();
            rpImg.sprite = GetDPadCircleSprite(2, NeonCrimson);

            GameObject radioSlot = CreateChild(root, "Slot_Radio", new Vector2(26f, -8f), new Vector2(38f, 38f));
            RectTransform rsRt = radioSlot.GetComponent<RectTransform>();
            rsRt.anchorMin = new Vector2(0f, 0f); rsRt.anchorMax = new Vector2(0f, 0f); rsRt.pivot = new Vector2(0f, 0f);
            Image rsImg = radioSlot.AddComponent<Image>();
            rsImg.sprite = GetSlotBoxSprite();
            rsImg.color = NeonCrimson;
            rsImg.type = Image.Type.Sliced;

            GameObject radioIcon = CreateChild(radioSlot, "Icon_Equalizer", Vector2.zero, new Vector2(24f, 18f));
            SetFullStretch(radioIcon);
            Image riImg = radioIcon.AddComponent<Image>();
            riImg.sprite = GetRadioIconSprite();
            riImg.color = NeonCrimson;

            // ─── RIGHT SECTION: MAXDOC INHALER & GRENADE / GADGET ─────────────────
            // 4. MaxDoc Inhaler Slot (Large)
            GameObject maxDocSlot = CreateChild(root, "Slot_MaxDocLarge", new Vector2(76f, 0f), new Vector2(52f, 52f));
            RectTransform msRt = maxDocSlot.GetComponent<RectTransform>();
            msRt.anchorMin = new Vector2(0f, 0f); msRt.anchorMax = new Vector2(0f, 0f); msRt.pivot = new Vector2(0f, 0f);
            Image msImg = maxDocSlot.AddComponent<Image>();
            msImg.sprite = GetSlotBoxSprite();
            msImg.color = KiroshiCyan;
            msImg.type = Image.Type.Sliced;

            GameObject maxDocIcon = CreateChild(maxDocSlot, "Icon_MaxDoc", Vector2.zero, new Vector2(36f, 36f));
            SetFullStretch(maxDocIcon);
            Image mdiImg = maxDocIcon.AddComponent<Image>();
            mdiImg.sprite = GetMaxDocIconSprite();
            mdiImg.color = Color.white;

            // Count badge on bottom edge: "3"
            GameObject mdCountObj = CreateChild(maxDocSlot, "Badge_Count", new Vector2(0f, -4f), new Vector2(20f, 15f));
            RectTransform mdcRt = mdCountObj.GetComponent<RectTransform>();
            mdcRt.anchorMin = new Vector2(0.5f, 0f); mdcRt.anchorMax = new Vector2(0.5f, 0f); mdcRt.pivot = new Vector2(0.5f, 0.5f);
            mdcRt.anchoredPosition = new Vector2(0f, -4f);
            Image mdcBg = mdCountObj.AddComponent<Image>();
            mdcBg.color = new Color(0.02f, 0.05f, 0.08f, 0.95f);
            AddBorder(mdCountObj, KiroshiCyan, 1f);
            _hypoCountText = CreateText(mdCountObj, "Txt_Val", $"{_maxDocCount}", 11f, TextAlignmentOptions.Center, Vector2.zero, KiroshiCyan, boldFont);
            _hypoCountText.fontStyle = FontStyles.Bold;
            SetFullStretch(_hypoCountText.gameObject);

            // Prompt below MaxDoc: cyan circle with "+"
            GameObject mdPrompt = CreateChild(root, "Prompt_MaxDoc", new Vector2(91f, -28f), new Vector2(22f, 22f));
            RectTransform mdpRt = mdPrompt.GetComponent<RectTransform>();
            mdpRt.anchorMin = new Vector2(0f, 0f); mdpRt.anchorMax = new Vector2(0f, 0f); mdpRt.pivot = new Vector2(0f, 0f);
            Image mdpImg = mdPrompt.AddComponent<Image>();
            mdpImg.sprite = GetDPadCircleSprite(3, KiroshiCyan);

            // 5. Grenade / Optical Camo Slot (Large)
            GameObject grenadeSlot = CreateChild(root, "Slot_GrenadeLarge", new Vector2(138f, 0f), new Vector2(52f, 52f));
            RectTransform gsRt = grenadeSlot.GetComponent<RectTransform>();
            gsRt.anchorMin = new Vector2(0f, 0f); gsRt.anchorMax = new Vector2(0f, 0f); gsRt.pivot = new Vector2(0f, 0f);
            Image gsImg = grenadeSlot.AddComponent<Image>();
            gsImg.sprite = GetSlotBoxSprite();
            gsImg.color = KiroshiCyan;
            gsImg.type = Image.Type.Sliced;

            GameObject grenadeIcon = CreateChild(grenadeSlot, "Icon_Grenade", Vector2.zero, new Vector2(36f, 36f));
            SetFullStretch(grenadeIcon);
            Image giImg = grenadeIcon.AddComponent<Image>();
            giImg.sprite = GetGrenadeIconSprite();
            giImg.color = Color.white;

            // Count badge on bottom edge: "2"
            GameObject grCountObj = CreateChild(grenadeSlot, "Badge_Count", new Vector2(0f, -4f), new Vector2(20f, 15f));
            RectTransform grcRt = grCountObj.GetComponent<RectTransform>();
            grcRt.anchorMin = new Vector2(0.5f, 0f); grcRt.anchorMax = new Vector2(0.5f, 0f); grcRt.pivot = new Vector2(0.5f, 0.5f);
            grcRt.anchoredPosition = new Vector2(0f, -4f);
            Image grcBg = grCountObj.AddComponent<Image>();
            grcBg.color = new Color(0.02f, 0.05f, 0.08f, 0.95f);
            AddBorder(grCountObj, KiroshiCyan, 1f);
            _gadgetCountText = CreateText(grCountObj, "Txt_Val", "2", 11f, TextAlignmentOptions.Center, Vector2.zero, KiroshiCyan, boldFont);
            _gadgetCountText.fontStyle = FontStyles.Bold;
            SetFullStretch(_gadgetCountText.gameObject);

            // Prompt below Grenade: cyan pill circle with "RB"
            GameObject grPrompt = CreateChild(root, "Prompt_Grenade", new Vector2(152f, -28f), new Vector2(24f, 22f));
            RectTransform grpRt = grPrompt.GetComponent<RectTransform>();
            grpRt.anchorMin = new Vector2(0f, 0f); grpRt.anchorMax = new Vector2(0f, 0f); grpRt.pivot = new Vector2(0f, 0f);
            Image grpImg = grPrompt.AddComponent<Image>();
            grpImg.color = new Color(0.02f, 0.05f, 0.08f, 0.92f);
            AddBorder(grPrompt, KiroshiCyan, 1.2f);
            ApplyAngledCut(grpImg, 5f);
            TextMeshProUGUI rbTxt = CreateText(grPrompt, "Txt_RB", "RB", 9.5f, TextAlignmentOptions.Center, Vector2.zero, KiroshiCyan, boldFont);
            rbTxt.fontStyle = FontStyles.Bold;
            SetFullStretch(rbTxt.gameObject);
        }

        private TextMeshProUGUI CreateKeyChip(GameObject parent, string key, string label, float xPos, float width)
        {
            // 2077 prompts = flat dark tag, yellow key, no border glow
            GameObject chip = CreateChild(parent, $"Chip_{key}", new Vector2(xPos, 0f), new Vector2(width, 24f));
            RectTransform cRt = chip.GetComponent<RectTransform>();
            cRt.anchorMin = new Vector2(0f, 0.5f);
            cRt.anchorMax = new Vector2(0f, 0.5f);
            cRt.pivot = new Vector2(0f, 0.5f);
            cRt.anchoredPosition = new Vector2(xPos, 0f);
            cRt.sizeDelta = new Vector2(width, 24f);

            Image bg = chip.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.04f, 0.045f, 0.88f);
            ApplyAngledCut(bg, 6f);

            string chipText = $"<color=#{ColorUtility.ToHtmlStringRGB(CyberYellow)}>{key}</color>  {label}";
            TextMeshProUGUI tmp = CreateText(chip, "Text_Label", chipText, 10.5f, TextAlignmentOptions.Center, Vector2.zero, TextWhite, boldFont);
            SetFullStretch(tmp.gameObject);
            return tmp;
        }

        public void UpdateActionChipsPerspective(bool isFirstPerson)
        {
            if (_perspectiveChipText != null)
            {
                string label = isFirstPerson ? "3RD PERS" : "1ST PERS";
                _perspectiveChipText.text = $"<color=#{ColorUtility.ToHtmlStringRGB(CyberYellow)}>P</color>  {label}";
            }
        }

        // ─── 6. CENTER: TACTICAL KIROSHI AR RETICLE & RANGEFINDER ────────────────
        private void BuildCenterKiroshiReticle()
        {
            GameObject root = CreateAnchorPanel("Zone_Center_KiroshiReticle",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(160f, 160f));

            // 2077 crosshair = tiny white dot, no chunky cyan brackets
            GameObject dotObj = CreateChild(root, "Dot_ReticleCenter", Vector2.zero, new Vector2(4f, 4f));
            Image dotImg = dotObj.AddComponent<Image>();
            dotImg.sprite = GetCircleSprite();
            dotImg.color = TextWhite;

            GameObject leftBracket = CreateChild(root, "Bracket_Left", new Vector2(-14f, 0f), new Vector2(10f, 2f));
            Image lImg = leftBracket.AddComponent<Image>();
            lImg.color = new Color(1f, 1f, 1f, 0.85f);
            _reticleLeftBracket = leftBracket.GetComponent<RectTransform>();

            GameObject rightBracket = CreateChild(root, "Bracket_Right", new Vector2(14f, 0f), new Vector2(10f, 2f));
            Image rImg = rightBracket.AddComponent<Image>();
            rImg.color = new Color(1f, 1f, 1f, 0.85f);
            _reticleRightBracket = rightBracket.GetComponent<RectTransform>();

            GameObject rangeObj = CreateChild(root, "Text_Rangefinder", new Vector2(0f, -24f), new Vector2(160f, 16f));
            _rangefinderText = CreateText(rangeObj, "Txt_Range", "", 10f, TextAlignmentOptions.Center, Vector2.zero, TextMuted, headerFont);
            _rangefinderText.fontStyle = FontStyles.Bold;

            GameObject targetObj = CreateChild(root, "Text_TargetScanner", new Vector2(0f, -40f), new Vector2(300f, 16f));
            _targetScannerText = CreateText(targetObj, "Txt_Target", "", 10f, TextAlignmentOptions.Center, Vector2.zero, TextMuted, headerFont);
        }

        // ─── 7. CINEMATIC OVERLAYS & TOAST BANNER (CLEAN: NO SCANLINES) ──────────
        private void BuildCinematicOverlays()
        {
            GameObject root = CreateAnchorPanel("Zone_CinematicOverlays",
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            SetFullStretch(root);

            // Note: Camera scanline filter and tiny corner text intentionally omitted
            // to maintain a pristine, razor-sharp AAA presentation.

            // Low-Health Glitch Vignette (Side-only warning glow)
            GameObject vigObj = CreateChild(root, "Overlay_LowHealthVignette", Vector2.zero, Vector2.zero);
            SetFullStretch(vigObj);
            _lowHealthVignette = vigObj.AddComponent<Image>();
            _lowHealthVignette.sprite = GetSideVignetteSprite();
            _lowHealthVignette.color = Color.clear;
            _lowHealthVignette.raycastTarget = false;

            // Instant damage flash (flashes sides on impact)
            GameObject flashObj = CreateChild(root, "Overlay_DamageFlash", Vector2.zero, Vector2.zero);
            SetFullStretch(flashObj);
            _damageFlash = flashObj.AddComponent<Image>();
            _damageFlash.sprite = GetSideVignetteSprite();
            _damageFlash.color = Color.clear;
            _damageFlash.raycastTarget = false;

            // Death overlay: flatline panel
            _deathOverlayObj = CreateChild(root, "Overlay_Death", Vector2.zero, new Vector2(700f, 160f));
            RectTransform deathRt = _deathOverlayObj.GetComponent<RectTransform>();
            deathRt.anchorMin = new Vector2(0.5f, 0.5f);
            deathRt.anchorMax = new Vector2(0.5f, 0.5f);
            deathRt.pivot = new Vector2(0.5f, 0.5f);
            deathRt.anchoredPosition = Vector2.zero;
            Image deathBg = _deathOverlayObj.AddComponent<Image>();
            deathBg.color = new Color(0.05f, 0.005f, 0.01f, 0.96f);
            ApplyAngledCut(deathBg, 20f);
            _deathText = CreateText(_deathOverlayObj, "Text_Death", "FLATLINE — PRESS R TO REBOOT", 16f, TextAlignmentOptions.Center, Vector2.zero, NeonCrimson, boldFont);
            _deathText.fontStyle = FontStyles.Bold;
            _deathOverlayObj.SetActive(false);

            // 2077 toast = flat yellow strip with black text (top-center, like quest updates)
            _toastBannerObj = CreateChild(root, "Banner_CyberToast", new Vector2(0f, 220f), new Vector2(620f, 34f));
            RectTransform toastRt = _toastBannerObj.GetComponent<RectTransform>();
            toastRt.pivot = new Vector2(0.5f, 0.5f);
            toastRt.anchoredPosition = new Vector2(0f, 220f);
            Image tstBg = _toastBannerObj.AddComponent<Image>();
            tstBg.color = CyberYellow;
            ApplyAngledCut(tstBg, 10f);

            _toastBannerText = CreateText(_toastBannerObj, "Text_Toast", "", 12f, TextAlignmentOptions.Center, Vector2.zero, Color.black, boldFont);
            _toastBannerText.fontStyle = FontStyles.Bold;
            _toastBannerObj.SetActive(false);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // REAL-TIME UPDATES (UPDATE LOOP)
        // ─────────────────────────────────────────────────────────────────────────
        private void Update()
        {
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup != null)
            {
                bool isCutscene = SyntyCombined.Cutscene.GlobalCutsceneState.IsCutsceneActive;
                _canvasGroup.alpha = isCutscene ? 0f : 1f;
                _canvasGroup.interactable = !isCutscene;
                _canvasGroup.blocksRaycasts = !isCutscene;
                if (isCutscene) return;
            }

            if (playerController == null && _insaneHero == null)
            {
                FindPlayerReference();
                if (playerController == null && _insaneHero == null) return;
            }

            UpdateBioMonitor();
            UpdateTelemetryAndMinimap();
            UpdateWeaponDiagnostics();
            UpdateReticleAndRangefinder();
            UpdateConsumablesAndHotkeys();
            UpdateToastBanner();
        }

        private void UpdateBioMonitor()
        {
            float maxHp = playerController != null ? playerController.maxHealth : (_insaneHero != null ? _insaneHero.maxHealth : 250f);
            float curHp = playerController != null ? playerController.currentHealth : (_insaneHero != null ? _insaneHero.currentHealth : 250f);
            maxHp = Mathf.Max(maxHp, 1f);
            curHp = Mathf.Clamp(curHp, 0f, maxHp);
            float hpRatio = curHp / maxHp;

            // Damage flash trigger (flashes sides on impact)
            if (_lastHp >= 0f && curHp < _lastHp - 0.5f && _damageFlash != null)
            {
                if (_damageFlash.sprite == null) _damageFlash.sprite = GetSideVignetteSprite();
                _damageFlash.color = new Color(1f, 0f, 0.15f, 0.70f);
            }
            _lastHp = curHp;
            if (_damageFlash != null && _damageFlash.color.a > 0f)
            {
                Color c = _damageFlash.color;
                c.a = Mathf.Max(0f, c.a - Time.unscaledDeltaTime * 2.2f);
                _damageFlash.color = c;
            }

            // Death overlay & reboot prompt
            bool isDead = curHp <= 0f;
            if (_deathOverlayObj != null && _deathOverlayObj.activeSelf != isDead && !_isRebooting)
                _deathOverlayObj.SetActive(isDead);

            if (isDead && !_isRebooting)
            {
                // Pulsing glow effect on death text
                if (_deathText != null)
                {
                    float alpha = 0.70f + Mathf.PingPong(Time.unscaledTime * 2.5f, 0.30f);
                    _deathText.color = new Color(NeonCrimson.r, NeonCrimson.g, NeonCrimson.b, alpha);
                }

                // Check for reboot input
                bool rPressed = false;
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null && kb.rKey.wasPressedThisFrame) rPressed = true;
                var gp = UnityEngine.InputSystem.Gamepad.current;
                if (gp != null && (gp.buttonWest.wasPressedThisFrame || gp.buttonSouth.wasPressedThisFrame || gp.startButton.wasPressedThisFrame)) rPressed = true;
                try { if (Input.GetKeyDown(KeyCode.R)) rPressed = true; } catch { }

                if (rPressed)
                {
                    TriggerReboot();
                }
            }

            // Smooth active health fill
            if (_healthFillRect != null)
            {
                _healthFillRect.anchorMax = new Vector2(hpRatio, 1f);
            }

            // Smooth ghost health trailing
            if (_ghostHealthFillRect != null)
            {
                _ghostHealthRatio = Mathf.MoveTowards(_ghostHealthRatio, hpRatio, Time.deltaTime * 0.45f);
                _ghostHealthFillRect.anchorMax = new Vector2(_ghostHealthRatio, 1f);
            }

            // Numeric HP text: large bold red current HP, smaller raised light-coral max HP (e.g. "284 / 350")
            if (_healthText != null)
            {
                int dispCur = Mathf.Max(0, Mathf.RoundToInt(curHp));
                _healthText.text = $"{dispCur}";
            }
            if (_maxHealthText != null)
            {
                int dispMax = Mathf.Max(1, Mathf.RoundToInt(maxHp));
                _maxHealthText.text = $"/ {dispMax}";
            }

            // Godmode / Subdermal Armor Overclock badge
            if (_armorBadgeObj != null)
            {
                bool isGod = playerController != null ? playerController.isInvincible : (_insaneHero != null ? _insaneHero.isInvincible : false);
                if (_armorBadgeObj.activeSelf != isGod)
                {
                    _armorBadgeObj.SetActive(isGod);
                    if (isGod) ShowToast("SUB-DERMAL ARMOR OVERCLOCKED", 2.5f);
                }
            }

            // Cyberware energy / stamina (for the 5 cyan ticks)
            float energyRatio = 1f;
            bool bulletActive = false;
            if (_insaneHero != null)
            {
                energyRatio = _insaneHero.SlowMotionEnergyPercent;
                bulletActive = _insaneHero.IsSlowMotionActive;
                _currentStamina = energyRatio;
            }
            else
            {
                bool isSprint = playerController != null
                    ? (playerController.IsSprintingState && playerController.CurrentMoveSpeed > 0.5f)
                    : (Input.GetKey(KeyCode.LeftShift));
                if (isSprint) _currentStamina = Mathf.Clamp01(_currentStamina - Time.deltaTime * 0.25f);
                else _currentStamina = Mathf.Clamp01(_currentStamina + Time.deltaTime * 0.35f);
                energyRatio = _currentStamina;
            }

            if (_staminaFillRect != null)
            {
                _staminaFillRect.anchorMax = new Vector2(energyRatio, 1f);
                Image fillImg = _staminaFillRect.GetComponent<Image>();
                if (fillImg != null)
                    fillImg.color = CyberYellow;
            }
            if (_staminaLabel != null)
            {
                _staminaLabel.text = bulletActive ? "SANDEVISTAN ACTIVE" : "";
                _staminaLabel.color = CyberYellow;
            }

            // Cyber-Ticks under health bar (5 cyan ticks mirror stamina/energy)
            if (_ramCellImages != null)
            {
                int lit = Mathf.CeilToInt(energyRatio * _ramCellImages.Length);
                Color activeCyan = new Color(0.0f, 0.94f, 1.0f, 1.0f);
                Color spentDark = new Color(0.04f, 0.21f, 0.25f, 0.40f);
                for (int i = 0; i < _ramCellImages.Length; i++)
                {
                    if (_ramCellImages[i] == null) continue;
                    bool on = i < lit;
                    _ramCellImages[i].color = on ? (bulletActive ? CyberYellow : activeCyan) : spentDark;
                }
            }

            // Low health side vignette (< 30% HP) — only glows on screen sides, keeping center clear
            if (_lowHealthVignette != null)
            {
                if (_lowHealthVignette.sprite == null) _lowHealthVignette.sprite = GetSideVignetteSprite();

                if (hpRatio < 0.30f && curHp > 0f)
                {
                    // Urgency ratio from 0.0 (at 30% HP) to 1.0 (at 0% HP)
                    float urgency = 1f - (hpRatio / 0.30f);

                    // Heartbeat pulse frequency: accelerates from 4.5 rad/s up to 9.5 rad/s as HP drops
                    float pulseSpeed = Mathf.Lerp(4.5f, 9.5f, urgency);

                    // Double-thump heartbeat waveform (lub-dub)
                    float t = Time.unscaledTime * pulseSpeed;
                    float beat = Mathf.Clamp01(Mathf.Sin(t) + 0.35f * Mathf.Sin(t * 2f));

                    // Alpha intensity: subtle at 29% HP, intense deep crimson at < 15% HP
                    float minAlpha = Mathf.Lerp(0.25f, 0.50f, urgency);
                    float maxAlpha = Mathf.Lerp(0.65f, 0.95f, urgency);
                    float currentAlpha = Mathf.Lerp(minAlpha, maxAlpha, beat);

                    // Authentic Cyberpunk 2077 danger crimson strictly on the sides
                    _lowHealthVignette.color = new Color(1.0f, 0.0f, 0.12f, currentAlpha);
                }
                else
                {
                    _lowHealthVignette.color = Color.clear;
                }
            }
        }

        private void UpdateTelemetryAndMinimap()
        {
            // Advance in-game clock
            _inGameMinutes += Time.deltaTime * 2.5f; // 1 real sec = 2.5 in-game mins
            if (_inGameMinutes >= 1440f) _inGameMinutes -= 1440f;
            int hours = Mathf.FloorToInt(_inGameMinutes / 60f);
            int mins = Mathf.FloorToInt(_inGameMinutes % 60f);
            string ampm = hours >= 12 ? "PM" : "AM";
            int displayHours = hours % 12;
            if (displayHours == 0) displayHours = 12;

            if (_clockText != null)
            {
                _clockText.text = $"{displayHours:D2}:{mins:D2} {ampm}";
                _clockText.color = KiroshiCyan;
            }

            // Heading & Compass
            Transform heroT = playerController != null ? playerController.transform : (_insaneHero != null ? _insaneHero.transform : null);
            if (_compassText != null && heroT != null)
            {
                float yRot = heroT.eulerAngles.y;
                string dir = GetCompassDirection(yRot);
                _compassText.text = $"HDG: {yRot:F0}° {dir}";
            }

            // 2077 combat ring: RED pulse in combat, dark when safe. Text: COMBAT / AREA: SAFE.
            bool combatActive = playerController != null
                ? (playerController.CurrentCombatState != SyntySwordLocomotionController.CombatState.None || playerController.IsSwordDrawn)
                : (_insaneHero != null && (_insaneHero.IsBlocking || _insaneHero.CurrentComboCount > 0 || _insaneHero.CurrentHeavyComboCount > 0 || _insaneHero.IsSlowMotionActive));

            if (_threatBadgeText != null)
            {
                if (combatActive)
                {
                    _threatBadgeText.color = NeonCrimson;
                    _threatBadgeText.text = "COMBAT";
                }
                else
                {
                    _threatBadgeText.color = OkGreen;
                    _threatBadgeText.text = "AREA: SAFE";
                }
            }
            if (_minimapRingImage != null)
            {
                // Square frame: tint the 2px border pieces + backdrop.
                Color borderCol;
                if (combatActive)
                {
                    float pulse = (Mathf.Sin(Time.time * 8f) + 1f) * 0.5f;
                    borderCol = Color.Lerp(NeonCrimson, new Color(1f, 0.4f, 0.4f, 1f), pulse);
                }
                else
                {
                    borderCol = new Color(0.25f, 0.25f, 0.25f, 0.9f);
                }
                if (_minimapBorderObjs != null)
                {
                    foreach (GameObject b in _minimapBorderObjs)
                    {
                        if (b == null) continue;
                        Image bImg = b.GetComponent<Image>();
                        if (bImg != null) bImg.color = borderCol;
                    }
                }
            }

            bool isInterior = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.IndexOf("Interior", System.StringComparison.OrdinalIgnoreCase) >= 0;

            // Lazily instantiate _noSignalOverlay if HUD was created before this method
            if (_noSignalOverlay == null && minimapRawImage != null && minimapRawImage.transform.parent != null)
            {
                Transform frame = minimapRawImage.transform.parent;
                _noSignalOverlay = CreateChild(frame.gameObject, "Overlay_NoSignal", Vector2.zero, new Vector2(226f, 226f));
                SetFullStretch(_noSignalOverlay);
                Image noSigBg = _noSignalOverlay.AddComponent<Image>();
                noSigBg.color = new Color(0.015f, 0.035f, 0.07f, 1f);

                GameObject iconObj = CreateChild(_noSignalOverlay, "Icon_Warning", new Vector2(0f, 34f), new Vector2(200f, 22f));
                var iconTxt = CreateText(iconObj, "Txt_Warn", "▲ GPS OFFLINE ▲", 11f, TextAlignmentOptions.Center, Vector2.zero, new Color(1f, 0.35f, 0.35f, 0.95f), boldFont);
                iconTxt.fontStyle = FontStyles.Bold;

                GameObject titleObj = CreateChild(_noSignalOverlay, "Text_NoSignal", new Vector2(0f, 2f), new Vector2(210f, 38f));
                _noSignalTitle = CreateText(titleObj, "Txt_Title", "NO SIGNAL", 21f, TextAlignmentOptions.Center, Vector2.zero, new Color(1f, 0.22f, 0.22f, 1f), boldFont);
                _noSignalTitle.fontStyle = FontStyles.Bold;

                GameObject subObj = CreateChild(_noSignalOverlay, "Text_NoSignalSub", new Vector2(0f, -36f), new Vector2(210f, 32f));
                _noSignalSubtitle = CreateText(subObj, "Txt_Sub", "LOCAL RELAY BLOCKED\n// UNDERGROUND CLINIC", 9f, TextAlignmentOptions.Center, Vector2.zero, new Color(0.42f, 0.65f, 0.75f, 0.9f), headerFont);
            }

            if (_noSignalOverlay != null)
            {
                if (isInterior)
                {
                    if (!_noSignalOverlay.activeSelf) _noSignalOverlay.SetActive(true);
                    if (minimapRawImage != null && minimapRawImage.enabled) minimapRawImage.enabled = false;
                    if (playerIconRect != null && playerIconRect.gameObject.activeSelf) playerIconRect.gameObject.SetActive(false);
                    if (enemyBlipsContainer != null && enemyBlipsContainer.gameObject.activeSelf) enemyBlipsContainer.gameObject.SetActive(false);
                    if (_northMarkerObj != null && _northMarkerObj.activeSelf) _northMarkerObj.SetActive(false);

                    // Subtle breathing pulse on the NO SIGNAL title
                    if (_noSignalTitle != null)
                    {
                        float alpha = Mathf.Lerp(0.60f, 1.0f, (Mathf.Sin(Time.unscaledTime * 5.0f) + 1f) * 0.5f);
                        _noSignalTitle.color = new Color(1f, 0.22f, 0.22f, alpha);
                    }
                }
                else
                {
                    if (_noSignalOverlay.activeSelf) _noSignalOverlay.SetActive(false);
                    if (minimapRawImage != null && !minimapRawImage.enabled) minimapRawImage.enabled = true;
                    if (playerIconRect != null && !playerIconRect.gameObject.activeSelf) playerIconRect.gameObject.SetActive(true);
                    if (enemyBlipsContainer != null && !enemyBlipsContainer.gameObject.activeSelf) enemyBlipsContainer.gameObject.SetActive(true);
                    if (_northMarkerObj != null && !_northMarkerObj.activeSelf) _northMarkerObj.SetActive(true);
                }
            }

            // Ensure minimap target link is kept active when in outside city
            if (!isInterior && minimapRawImage != null && (minimapRawImage.texture == null || (_minimapCtrl != null && _minimapCtrl.minimapRawImage != minimapRawImage)))
            {
                if (_minimapCtrl == null) _minimapCtrl = GetComponent<MinimapController>() ?? Object.FindAnyObjectByType<MinimapController>();
                if (_minimapCtrl != null)
                {
                    _minimapCtrl.SetMinimapTarget(minimapRawImage, playerIconRect, enemyBlipsContainer);
                }
            }
        }

        private void UpdateWeaponDiagnostics()
        {
            bool drawn = playerController != null ? playerController.IsSwordDrawn : (_insaneHero != null ? _insaneHero.isSwordDrawn : true);

            if (_weaponStanceText != null)
            {
                _weaponStanceText.text = drawn ? "DRAWN" : "SHEATHED";
                _weaponStanceText.color = drawn ? OkGreen : TextMuted;
            }

            if (_weaponActionText != null)
            {
                if (playerController != null)
                {
                    var state = playerController.CurrentCombatState;
                    string actionName = state switch
                    {
                        SyntySwordLocomotionController.CombatState.None          => "IDLE STANCE",
                        SyntySwordLocomotionController.CombatState.LightAttack   => playerController.CurrentAttackName.ToUpper(),
                        SyntySwordLocomotionController.CombatState.HeavyAttack   => playerController.CurrentAttackName.ToUpper(),
                        SyntySwordLocomotionController.CombatState.SpecialAttack => playerController.CurrentAttackName.ToUpper(),
                        SyntySwordLocomotionController.CombatState.Block         => "GUARD",
                        SyntySwordLocomotionController.CombatState.Parry         => "PARRY",
                        SyntySwordLocomotionController.CombatState.Dodge         => "DODGE",
                        SyntySwordLocomotionController.CombatState.Draw          => "DRAWING",
                        SyntySwordLocomotionController.CombatState.Sheathe       => "SHEATHING",
                        _                                                        => $"{state}".ToUpper()
                    };
                    _weaponActionText.text = actionName;
                    _weaponActionText.color = state == SyntySwordLocomotionController.CombatState.None ? TextMuted : CyberYellow;
                }
                else if (_insaneHero != null)
                {
                    bool sand = _insaneHero.IsSlowMotionActive;
                    if (_insaneHero.IsBlocking) { _weaponActionText.text = "GUARD" + (sand ? "  •  SANDEVISTAN" : ""); _weaponActionText.color = KiroshiCyan; }
                    else if (_insaneHero.CurrentComboCount > 0) { _weaponActionText.text = $"COMBO {_insaneHero.CurrentComboCount} / 7" + (sand ? "  •  SANDEVISTAN" : ""); _weaponActionText.color = CyberYellow; }
                    else if (_insaneHero.CurrentHeavyComboCount > 0) { _weaponActionText.text = $"HEAVY {_insaneHero.CurrentHeavyComboCount}" + (sand ? "  •  SANDEVISTAN" : ""); _weaponActionText.color = CyberYellow; }
                    else { _weaponActionText.text = sand ? "SANDEVISTAN" : "IDLE"; _weaponActionText.color = sand ? CyberYellow : TextMuted; }
                }
            }
        }

        private void UpdateReticleAndRangefinder()
        {
            if (!showCenterReticle) return;
            if (_mainCam == null) _mainCam = Camera.main;
            if (_mainCam == null) return;

            Ray ray = new Ray(_mainCam.transform.position, _mainCam.transform.forward);
            float dist = 50f;
            string targetInfo = "";
            Color reticleColor = TextWhite;
            bool hasTarget = false;

            if (Physics.Raycast(ray, out RaycastHit hit, 80f))
            {
                dist = hit.distance;
                GameObject hitObj = hit.collider.gameObject;

                if (hitObj.CompareTag("Enemy") || hitObj.GetComponentInParent<SwordEnemyAI>() != null || hitObj.GetComponentInParent<InsaneGun.Combat.InsaneGunSwordController>() != null)
                {
                    targetInfo = "HOSTILE";
                    reticleColor = NeonCrimson;
                    hasTarget = true;
                }
                else if (hitObj.name.Contains("NPC") || hitObj.name.Contains("Pedestrian") || hitObj.name.Contains("Citizen"))
                {
                    targetInfo = "CIVILIAN";
                    reticleColor = TextWhite;
                    hasTarget = true;
                }
                else if (hitObj.name.Contains("Hover") || hitObj.name.Contains("Car") || hitObj.name.Contains("Taxi"))
                {
                    targetInfo = "VEHICLE";
                    reticleColor = CyberYellow;
                    hasTarget = true;
                }
            }

            if (_rangefinderText != null)
            {
                _rangefinderText.text = hasTarget ? $"{dist:F0}M" : "";
                _rangefinderText.color = TextMuted;
            }

            if (_targetScannerText != null)
            {
                _targetScannerText.text = hasTarget ? targetInfo : "";
                _targetScannerText.color = reticleColor;
            }

            if (_reticleLeftBracket != null) _reticleLeftBracket.GetComponent<Image>().color = reticleColor;
            if (_reticleRightBracket != null) _reticleRightBracket.GetComponent<Image>().color = reticleColor;
        }

        private void UpdateConsumablesAndHotkeys()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            // H = heal (X is draw/sheathe on InsaneGun hero — do NOT steal it).
            bool hPressed = (kb != null && kb.hKey.wasPressedThisFrame) || Input.GetKeyDown(KeyCode.H);
            bool vPressed = (kb != null && kb.vKey.wasPressedThisFrame) || Input.GetKeyDown(KeyCode.V);

            // H Key: Consume MaxDoc Mk.3 Hypo
            if (hPressed)
            {
                if (_maxDocCount > 0)
                {
                    _maxDocCount--;
                    float healAmount = 40f;
                    if (playerController != null)
                        playerController.currentHealth = Mathf.Min(playerController.currentHealth + healAmount, playerController.maxHealth);
                    else if (_insaneHero != null)
                        _insaneHero.currentHealth = Mathf.Min(_insaneHero.currentHealth + healAmount, _insaneHero.maxHealth);

                    if (_hypoCountText != null) _hypoCountText.text = $"{_maxDocCount}";
                    ShowToast($"MAXDOC INJECTED +{healAmount:F0} HP ({_maxDocCount} LEFT)", 2.0f);
                }
                else
                {
                    ShowToast("NO MAXDOC LEFT", 2.0f);
                }
            }

            // V Key: Summon Hover Taxi / Autonomous Vehicle
            if (vPressed)
            {
                ShowToast("HOVER TAXI DISPATCHED", 3.0f);
            }

            // R Key: Reboot when flatlined
            bool rPressed = (kb != null && kb.rKey.wasPressedThisFrame);
            try { if (Input.GetKeyDown(KeyCode.R)) rPressed = true; } catch { }
            var gpPad = UnityEngine.InputSystem.Gamepad.current;
            if (gpPad != null && (gpPad.buttonWest.wasPressedThisFrame || gpPad.startButton.wasPressedThisFrame)) rPressed = true;

            if (rPressed && !_isRebooting)
            {
                float curHealth = playerController != null ? playerController.currentHealth : (_insaneHero != null ? _insaneHero.currentHealth : 100f);
                if (curHealth <= 0f || (_insaneHero != null && _insaneHero.IsDead))
                {
                    TriggerReboot();
                }
            }
        }

        private void UpdateToastBanner()
        {
            if (_toastTimer > 0f)
            {
                _toastTimer -= Time.deltaTime;
                if (_toastTimer <= 0f && _toastBannerObj != null)
                {
                    _toastBannerObj.SetActive(false);
                }
            }
        }

        public void ShowToast(string message, float duration = 3.0f)
        {
            if (_toastBannerObj != null && _toastBannerText != null)
            {
                _toastBannerText.text = message;
                _toastBannerObj.SetActive(true);
                _toastTimer = duration;
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // PLAYER REBOOT PROTOCOL (PRESS [R] ON FLATLINE)
        // ─────────────────────────────────────────────────────────────────────────
        private bool _isRebooting = false;

        public void TriggerReboot()
        {
            if (_isRebooting) return;
            StartCoroutine(ExecuteRebootRoutine());
        }

        private IEnumerator ExecuteRebootRoutine()
        {
            _isRebooting = true;
            Debug.Log("<color=#00F0FF>[CyberpunkHUD] Initiating Player Neural Reboot Sequence...</color>");

            if (_deathText != null)
            {
                _deathText.color = new Color(0f, 0.94f, 1f, 1f); // Neon cyan
                _deathText.text = "<color=#00F0FF>INITIATING NEURAL REBOOT PROTOCOL...</color>";
            }

            if (_damageFlash != null)
            {
                _damageFlash.color = new Color(0f, 0.94f, 1f, 0.45f); // Cyan reboot flash
            }

            yield return new WaitForSecondsRealtime(0.35f);

            // 1. Abyss Respawn system check
            if (FadePlaneRespawn.Instance != null)
            {
                FadePlaneRespawn.Instance.RespawnPlayer("Manual Neural Reboot [R]");
            }
            else
            {
                // Direct revive on active player controller
                if (_insaneHero != null)
                {
                    Vector3 respawnPos = _insaneHero.InitialSpawnPosition != Vector3.zero 
                        ? _insaneHero.InitialSpawnPosition 
                        : (_insaneHero.transform.position + Vector3.up * 0.15f);
                    _insaneHero.Revive(respawnPos);
                    _insaneHero.transform.rotation = _insaneHero.InitialSpawnRotation;
                    _insaneHero.currentHealth = _insaneHero.maxHealth;
                    _insaneHero.currentSlowMotionEnergy = _insaneHero.maxSlowMotionEnergy;
                }
                else if (playerController != null)
                {
                    playerController.enabled = true;
                    playerController.Respawn(playerController.transform.position, playerController.transform.rotation);
                    playerController.currentHealth = playerController.maxHealth;
                    playerController.OnPlayerHealthChanged?.Invoke(playerController.currentHealth, playerController.maxHealth);
                }
                else
                {
                    FindPlayerReference();
                    if (_insaneHero != null)
                    {
                        _insaneHero.Revive(_insaneHero.InitialSpawnPosition);
                        _insaneHero.currentHealth = _insaneHero.maxHealth;
                    }
                    else if (playerController != null)
                    {
                        playerController.enabled = true;
                        playerController.Respawn(playerController.transform.position, playerController.transform.rotation);
                        playerController.currentHealth = playerController.maxHealth;
                    }
                }
            }

            // 2. Also reset combat arena if present
            if (InsaneGun.Testing.InsaneGunCombatTestManager.Instance != null)
            {
                InsaneGun.Testing.InsaneGunCombatTestManager.Instance.ResetCombatArena();
            }

            yield return new WaitForSecondsRealtime(0.25f);

            if (_deathOverlayObj != null)
            {
                _deathOverlayObj.SetActive(false);
            }
            if (_deathText != null)
            {
                _deathText.color = NeonCrimson;
                _deathText.text = "FLATLINE — PRESS R TO REBOOT";
            }

            ShowToast("NEURAL REBOOT COMPLETE // SYSTEMS RESTORED", 3.0f);
            _isRebooting = false;
        }

        public void HideDeathOverlay()
        {
            if (_deathOverlayObj != null) _deathOverlayObj.SetActive(false);
            _isRebooting = false;
        }

        private string GetCompassDirection(float deg)
        {
            if (deg >= 337.5f || deg < 22.5f) return "N";
            if (deg >= 22.5f && deg < 67.5f) return "NE";
            if (deg >= 67.5f && deg < 112.5f) return "E";
            if (deg >= 112.5f && deg < 157.5f) return "SE";
            if (deg >= 157.5f && deg < 202.5f) return "S";
            if (deg >= 202.5f && deg < 247.5f) return "SW";
            if (deg >= 247.5f && deg < 292.5f) return "W";
            return "NW";
        }

        // ─────────────────────────────────────────────────────────────────────────
        // HELPER BUILDER METHODS
        // ─────────────────────────────────────────────────────────────────────────
        private GameObject CreateAnchorPanel(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(transform, false);

            RectTransform rt = panel.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            return panel;
        }

        private GameObject CreateChild(GameObject parent, string name, Vector2 pos, Vector2 size)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);

            RectTransform rt = child.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            return child;
        }

        private TextMeshProUGUI CreateText(GameObject parent, string name, string text, float fontSize, TextAlignmentOptions align, Vector2 pos, Color color, TMP_FontAsset font)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent.transform, false);

            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            RectTransform parentRt = parent.GetComponent<RectTransform>();
            float w = parentRt != null && parentRt.rect.width > 0f ? parentRt.rect.width : (parentRt != null && parentRt.sizeDelta.x > 0f ? parentRt.sizeDelta.x : 200f);
            rt.sizeDelta = new Vector2(w, 26f);

            TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = align;
            tmp.color = color;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;

            return tmp;
        }

        private void AddBorder(GameObject target, Color borderColor, float width)
        {
            // Legacy helper — kept for boss bar. New 2077 panels use ApplyAngledCut (flat, no glow).
            GameObject borderTop = CreateChild(target, "Border_T", new Vector2(0f, 0f), new Vector2(0f, width));
            RectTransform rT = borderTop.GetComponent<RectTransform>();
            rT.anchorMin = new Vector2(0f, 1f); rT.anchorMax = new Vector2(1f, 1f); rT.pivot = new Vector2(0.5f, 1f);
            borderTop.AddComponent<Image>().color = borderColor;

            GameObject borderBot = CreateChild(target, "Border_B", new Vector2(0f, 0f), new Vector2(0f, width));
            RectTransform rB = borderBot.GetComponent<RectTransform>();
            rB.anchorMin = new Vector2(0f, 0f); rB.anchorMax = new Vector2(1f, 0f); rB.pivot = new Vector2(0.5f, 0f);
            borderBot.AddComponent<Image>().color = borderColor;

            GameObject borderLeft = CreateChild(target, "Border_L", new Vector2(0f, 0f), new Vector2(width, 0f));
            RectTransform rL = borderLeft.GetComponent<RectTransform>();
            rL.anchorMin = new Vector2(0f, 0f); rL.anchorMax = new Vector2(0f, 1f); rL.pivot = new Vector2(0f, 0.5f);
            borderLeft.AddComponent<Image>().color = borderColor;

            GameObject borderRight = CreateChild(target, "Border_R", new Vector2(0f, 0f), new Vector2(width, 0f));
            RectTransform rR = borderRight.GetComponent<RectTransform>();
            rR.anchorMin = new Vector2(1f, 0f); rR.anchorMax = new Vector2(1f, 1f); rR.pivot = new Vector2(1f, 0.5f);
            borderRight.AddComponent<Image>().color = borderColor;
        }

        // Cyberpunk 2077 Health Bar: 45-degree chamfered bottom-right corner (9-sliced, 18px height)
        private static Sprite GetChamferHealthBarSprite()
        {
            if (_chamferHealthBarSprite != null) return _chamferHealthBarSprite;
            int W = 64, H = 18;
            float cut = 7.0f;
            Texture2D tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    // 45-degree chamfer on bottom-right corner: (W - 1 - cut, 0) to (W - 1, cut)
                    float dist = (x - (W - 1 - cut)) - y;
                    float a = Mathf.Clamp01(0.5f - dist);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            _chamferHealthBarSprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(3, 8, 8, 3));
            return _chamferHealthBarSprite;
        }

        // Cyberpunk 2077 Level Box: Chamfered bottom-left corner with crisp cyan border (32px)
        private static Sprite GetChamferLevelBoxSprite()
        {
            if (_chamferLevelBoxSprite != null) return _chamferLevelBoxSprite;
            int S = 32;
            float cut = 8.0f;
            float borderThickness = 1.6f;
            Texture2D tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color borderColor = new Color(0.50f, 0.90f, 1.0f, 1.0f); // #80E5FF cyan
            Color fillColor = new Color(0.04f, 0.08f, 0.11f, 0.70f); // dark semi-transparent

            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    float dLeft = x;
                    float dBottom = y;
                    float dRight = (S - 1) - x;
                    float dTop = (S - 1) - y;
                    float dChamfer = ((x + y) - cut) * 0.7071f;

                    float d = Mathf.Min(dRight, dTop);
                    if (x + y < cut + 14f)
                    {
                        if (x < cut && y < cut) d = Mathf.Min(d, dChamfer);
                        else if (x < cut) d = Mathf.Min(d, Mathf.Min(dLeft, dChamfer));
                        else if (y < cut) d = Mathf.Min(d, Mathf.Min(dBottom, dChamfer));
                    }
                    else
                    {
                        d = Mathf.Min(d, Mathf.Min(dLeft, dBottom));
                    }

                    Color col;
                    if (d < -0.5f)
                    {
                        col = Color.clear;
                    }
                    else if (d < borderThickness - 0.5f)
                    {
                        float a = Mathf.Clamp01(d + 0.5f);
                        col = new Color(borderColor.r, borderColor.g, borderColor.b, borderColor.a * a);
                    }
                    else if (d < borderThickness + 0.5f)
                    {
                        float t = Mathf.Clamp01(d - (borderThickness - 0.5f));
                        col = Color.Lerp(borderColor, fillColor, t);
                    }
                    else
                    {
                        col = fillColor;
                    }
                    tex.SetPixel(x, y, col);
                }
            }
            tex.Apply();
            _chamferLevelBoxSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(9, 9, 3, 3));
            return _chamferLevelBoxSprite;
        }

        // Cyberpunk 2077 chamfered slot / box sprite for HUD quick inventory
        private static Sprite _questAlertBadgeSprite;
        private static Sprite GetQuestAlertBadgeSprite()
        {
            if (_questAlertBadgeSprite != null) return _questAlertBadgeSprite;
            int S = 32;
            float cut = 7.0f;
            Texture2D tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    float a = (x + y < cut) ? 0f : 1f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            _questAlertBadgeSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
            return _questAlertBadgeSprite;
        }

        private static Sprite _slotBoxSprite;
        private static Sprite GetSlotBoxSprite()
        {
            if (_slotBoxSprite != null) return _slotBoxSprite;
            int S = 64;
            float cutTL = 9.0f;
            float cutBR = 5.0f;
            float border = 2.4f;
            Texture2D tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color borderCol = Color.white;
            Color fillCol = new Color(0.02f, 0.04f, 0.07f, 0.88f);

            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    float dL = x;
                    float dR = (S - 1) - x;
                    float dB = y;
                    float dT = (S - 1) - y;
                    float dTL = (x + dT) - cutTL;
                    float dBR = (dR + dB) - cutBR;

                    float minD = Mathf.Min(Mathf.Min(dL, dR), Mathf.Min(dB, dT));
                    if (x < cutTL + 4f && dT < cutTL + 4f) minD = Mathf.Min(minD, dTL * 0.7071f);
                    if (dR < cutBR + 4f && dB < cutBR + 4f) minD = Mathf.Min(minD, dBR * 0.7071f);

                    if (minD < -0.5f)
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                    else if (minD < border - 0.5f)
                    {
                        float a = Mathf.Clamp01(minD + 0.5f);
                        tex.SetPixel(x, y, new Color(borderCol.r, borderCol.g, borderCol.b, a));
                    }
                    else
                    {
                        tex.SetPixel(x, y, fillCol);
                    }
                }
            }
            tex.Apply();
            _slotBoxSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(12, 12, 12, 12));
            return _slotBoxSprite;
        }

        private static Sprite _phoneIconSprite;
        private static Sprite GetPhoneIconSprite()
        {
            if (_phoneIconSprite != null) return _phoneIconSprite;
            int S = 64;
            Texture2D tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color clear = Color.clear;
            Color bodyCol = new Color(0.92f, 0.95f, 0.98f, 1f);
            Color screenCol = new Color(0.0f, 0.88f, 1.0f, 1f);
            Color antennaCol = new Color(0.0f, 0.88f, 1.0f, 0.95f);

            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    Color c = clear;
                    if (x >= 21 && x <= 24 && y >= 44 && y <= 56)
                    {
                        c = antennaCol;
                    }
                    float distWave1 = Vector2.Distance(new Vector2(x, y), new Vector2(23f, 52f));
                    if (distWave1 >= 6f && distWave1 <= 8.5f && x > 23 && y >= 50)
                    {
                        c = antennaCol;
                    }
                    if (distWave1 >= 11f && distWave1 <= 13.5f && x > 23 && y >= 48)
                    {
                        c = antennaCol * 0.75f;
                    }
                    if (x >= 19 && x <= 45 && y >= 8 && y <= 44)
                    {
                        bool isBorder = (x <= 21 || x >= 43 || y <= 10 || y >= 42);
                        if (isBorder) c = bodyCol;
                        else if (x >= 24 && x <= 40 && y >= 27 && y <= 39)
                        {
                            bool screenBorder = (x <= 25 || x >= 39 || y <= 28 || y >= 38);
                            c = screenBorder ? screenCol : screenCol * 0.45f;
                        }
                        else if (x >= 24 && x <= 40 && (y == 14 || y == 18 || y == 22))
                        {
                            c = bodyCol * 0.85f;
                        }
                    }
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            _phoneIconSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
            return _phoneIconSprite;
        }

        private static Sprite _vehicleIconSprite;
        private static Sprite GetVehicleIconSprite()
        {
            if (_vehicleIconSprite != null) return _vehicleIconSprite;
            int S = 64;
            Texture2D tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color carCol = new Color(0.0f, 0.88f, 1.0f, 1f);
            Color wheelCol = new Color(0.15f, 0.22f, 0.28f, 1f);

            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    Color c = Color.clear;
                    bool inBody = false;
                    if (x >= 8 && x <= 56)
                    {
                        float topY = 22f;
                        if (x < 14) topY = 22f;
                        else if (x <= 26) topY = Mathf.Lerp(22f, 26f, (x - 14f) / 12f);
                        else if (x <= 34) topY = Mathf.Lerp(26f, 34f, (x - 26f) / 8f);
                        else if (x <= 44) topY = 34f;
                        else if (x <= 52) topY = Mathf.Lerp(34f, 26f, (x - 44f) / 8f);
                        else topY = (x >= 54) ? 29f : 25f;

                        if (y >= 19 && y <= topY) inBody = true;
                    }

                    float dWheel1 = Vector2.Distance(new Vector2(x, y), new Vector2(17f, 19f));
                    float dWheel2 = Vector2.Distance(new Vector2(x, y), new Vector2(45f, 19f));
                    if (dWheel1 <= 5.5f || dWheel2 <= 5.5f)
                    {
                        inBody = false;
                        if (dWheel1 <= 4.5f || dWheel2 <= 4.5f) c = wheelCol;
                        if (dWheel1 <= 2.0f || dWheel2 <= 2.0f) c = carCol;
                    }

                    if (inBody)
                    {
                        bool inWindow = (x >= 28 && x <= 46 && y >= 26 && y <= 32);
                        if (inWindow) c = new Color(0.02f, 0.12f, 0.18f, 0.95f);
                        else c = carCol;
                    }

                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            _vehicleIconSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
            return _vehicleIconSprite;
        }

        private static Sprite _radioIconSprite;
        private static Sprite GetRadioIconSprite()
        {
            if (_radioIconSprite != null) return _radioIconSprite;
            int S = 64;
            Texture2D tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color redCol = NeonCrimson;

            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    Color c = Color.clear;
                    if ((x >= 14 && x <= 18 && y >= 18 && y <= 34) ||
                        (x >= 23 && x <= 27 && y >= 18 && y <= 44) ||
                        (x >= 32 && x <= 36 && y >= 18 && y <= 52) ||
                        (x >= 41 && x <= 45 && y >= 18 && y <= 40) ||
                        (x >= 50 && x <= 54 && y >= 18 && y <= 28))
                    {
                        c = redCol;
                    }
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            _radioIconSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
            return _radioIconSprite;
        }

        private static Sprite _maxDocIconSprite;
        private static Sprite GetMaxDocIconSprite()
        {
            if (_maxDocIconSprite != null) return _maxDocIconSprite;
            int S = 64;
            Texture2D tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color redVial = new Color(0.95f, 0.08f, 0.18f, 1f);
            Color silverCap = new Color(0.85f, 0.90f, 0.95f, 1f);
            Color cyanStripe = new Color(0.0f, 0.90f, 1.0f, 1f);

            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    Color c = Color.clear;
                    int[] startsX = { 16, 28, 40 };
                    int[] startsY = { 14, 18, 14 };
                    int height = 30;

                    for (int i = 0; i < 3; i++)
                    {
                        int sx = startsX[i];
                        int sy = startsY[i];
                        if (x >= sx && x <= sx + 8 && y >= sy && y <= sy + height)
                        {
                            if (y >= sy + height - 6)
                                c = silverCap;
                            else if (y >= sy + 14 && y <= sy + 16)
                                c = cyanStripe;
                            else
                            {
                                c = (x == sx + 2) ? new Color(1f, 0.45f, 0.55f, 1f) : redVial;
                            }
                        }
                    }
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            _maxDocIconSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
            return _maxDocIconSprite;
        }

        private static Sprite _grenadeIconSprite;
        private static Sprite GetGrenadeIconSprite()
        {
            if (_grenadeIconSprite != null) return _grenadeIconSprite;
            int S = 64;
            Texture2D tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color bodyCol = new Color(0.28f, 0.32f, 0.36f, 1f);
            Color highlightCol = new Color(0.65f, 0.72f, 0.78f, 1f);
            Color cyanRing = new Color(0.0f, 0.90f, 1.0f, 1f);
            Color pinCol = new Color(0.90f, 0.85f, 0.30f, 1f);

            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    Color c = Color.clear;
                    if (x >= 22 && x <= 42 && y >= 12 && y <= 42)
                    {
                        if (y == 18 || y == 24 || y == 30 || y == 36)
                            c = new Color(0.14f, 0.16f, 0.18f, 1f);
                        else if (y >= 26 && y <= 28)
                            c = cyanRing;
                        else
                            c = (x == 26) ? highlightCol : bodyCol;
                    }
                    else if (x >= 28 && x <= 36 && y >= 42 && y <= 48)
                    {
                        c = highlightCol;
                    }
                    else if (x >= 24 && x <= 38 && y >= 48 && y <= 52)
                    {
                        c = bodyCol;
                    }
                    float dPin = Vector2.Distance(new Vector2(x, y), new Vector2(21f, 47f));
                    if (dPin >= 2.5f && dPin <= 4.5f)
                    {
                        c = pinCol;
                    }

                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            _grenadeIconSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
            return _grenadeIconSprite;
        }

        private static Sprite _dpadUpSprite;
        private static Sprite _dpadRightSprite;
        private static Sprite _dpadDownRedSprite;
        private static Sprite _dpadCrossSprite;
        private static Sprite GetDPadCircleSprite(int activeDir, Color ringColor)
        {
            if (activeDir == 0 && _dpadUpSprite != null) return _dpadUpSprite;
            if (activeDir == 1 && _dpadRightSprite != null) return _dpadRightSprite;
            if (activeDir == 2 && _dpadDownRedSprite != null) return _dpadDownRedSprite;
            if (activeDir == 3 && _dpadCrossSprite != null) return _dpadCrossSprite;

            int S = 48;
            Texture2D tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Vector2 center = new Vector2(S * 0.5f, S * 0.5f);
            Color dimArm = new Color(0.20f, 0.28f, 0.35f, 0.85f);
            Color bgDark = new Color(0.02f, 0.04f, 0.06f, 0.88f);

            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center);
                    Color c = Color.clear;
                    if (d <= 22f)
                    {
                        if (d >= 19.5f)
                        {
                            float a = Mathf.Clamp01((22f - d) * 1.5f);
                            c = new Color(ringColor.r, ringColor.g, ringColor.b, a);
                        }
                        else
                        {
                            c = bgDark;
                            float dx = Mathf.Abs(x - 24f);
                            float dy = Mathf.Abs(y - 24f);
                            bool isCross = (dx <= 4f && dy <= 13f) || (dy <= 4f && dx <= 13f);

                            if (isCross)
                            {
                                c = dimArm;
                                if (activeDir == 0 && y > 24f && dx <= 4.5f)
                                {
                                    c = ringColor;
                                    if (y >= 32f && dx > (37f - y)) c = bgDark;
                                }
                                else if (activeDir == 1 && x > 24f && dy <= 4.5f)
                                {
                                    c = ringColor;
                                    if (x >= 32f && dy > (37f - x)) c = bgDark;
                                }
                                else if (activeDir == 2 && y < 24f && dx <= 4.5f)
                                {
                                    c = ringColor;
                                    if (y <= 16f && dx > (y - 11f)) c = bgDark;
                                }
                                else if (activeDir == 3)
                                {
                                    c = ringColor;
                                }
                            }
                        }
                    }
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            Sprite spr = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
            if (activeDir == 0) _dpadUpSprite = spr;
            else if (activeDir == 1) _dpadRightSprite = spr;
            else if (activeDir == 2) _dpadDownRedSprite = spr;
            else if (activeDir == 3) _dpadCrossSprite = spr;
            return spr;
        }

        // 2077 chamfer: diagonal cut on top-right + bottom-left via sprite mask on Image.
        private void ApplyAngledCut(Image img, float cutPx)
        {
            if (img == null) return;
            if (_angledCutSprite == null)
            {
                int S = 64;
                Texture2D tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Bilinear;
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        float cut = S * 0.22f;
                        float a = 1f;
                        if (x > S - cut + (y / (float)S) * cut * 0.0f && y > S - cut) { }
                        // top-right diagonal + bottom-left diagonal
                        if (x + y > S * 2f - cut) a = 0f;
                        if (x + y < cut * 0.55f) a = 0f;
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                tex.Apply();
                _angledCutSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(8, 8, 14, 8));
            }
            img.sprite = _angledCutSprite;
            img.type = Image.Type.Sliced;
        }

        private Sprite GetCircleSprite()
        {
            if (_circleMaskSprite != null) return _circleMaskSprite;
            int S = 128;
            Texture2D tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Vector2 c = new Vector2(S * 0.5f, S * 0.5f);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), c) / (S * 0.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, d <= 1f ? 1f : 0f));
                }
            tex.Apply();
            _circleMaskSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
            return _circleMaskSprite;
        }

        private Sprite GetRingSprite()
        {
            int S = 256;
            Texture2D tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Vector2 c = new Vector2(S * 0.5f, S * 0.5f);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), c) / (S * 0.5f);
                    float ring = Mathf.Clamp01(1f - Mathf.Abs(d - 0.96f) / 0.03f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, ring));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
        }

        // Side Vignette: Concentrates warning glow strictly on the screen's left and right flanks.
        // The entire center region (>50% screen width) has alpha = 0 so the player's 3D view,
        // crosshair, and character remain 100% crystal clear without red tint or wash.
        public static Sprite GetSideVignetteSprite()
        {
            if (_sideVignetteSprite != null) return _sideVignetteSprite;

            int W = 512;
            int H = 256;
            Texture2D tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < H; y++)
            {
                float v = y / (float)(H - 1);
                float ny = Mathf.Abs(v - 0.5f) * 2f; // 0 center, 1 top/bottom

                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)(W - 1);
                    float nx = Mathf.Abs(u - 0.5f) * 2f; // 0 at screen center, 1 at left/right edges

                    // Central ~60% of the screen horizontally is strictly transparent (alpha = 0)
                    // Glow begins at nx >= 0.58f (the outer ~21% width on each flank)
                    float alpha = 0f;
                    if (nx > 0.58f)
                    {
                        float t = (nx - 0.58f) / 0.42f; // 0.0 to 1.0
                        t = t * t * (3f - 2f * t);      // smoothstep
                        alpha = Mathf.Pow(t, 1.35f);

                        // Subtle corner taper so the side glow rounds nicely into the corners
                        float cornerTaper = 1f - (Mathf.Pow(ny, 4f) * 0.15f);
                        alpha *= cornerTaper;
                    }

                    // Crisp high-tech border rim on extreme flank edge (outer 1.5%)
                    if (nx > 0.985f)
                    {
                        alpha = Mathf.Max(alpha, 0.98f);
                    }

                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            _sideVignetteSprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f));
            return _sideVignetteSprite;
        }

        private void SetAnchorTop(GameObject obj, float topOffset)
        {
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(0f, topOffset);
        }

        // Fixed top-right pin: keeps right-aligned HUD blocks (clock, minimap)
        // at their authored size inside the parent so they never overflow the screen.
        private void PinTopRight(GameObject obj, float topOffset)
        {
            RectTransform rt = obj.GetComponent<RectTransform>();
            Vector2 size = rt.sizeDelta;
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(0f, topOffset);
            rt.sizeDelta = size;
        }

        private void SetAnchorBottom(GameObject obj, float botOffset)
        {
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(0f, botOffset);
        }

        private void SetAnchorLeft(GameObject obj, float leftOffset)
        {
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(leftOffset, 0f);
        }

        private void SetAnchorRight(GameObject obj, float rightOffset)
        {
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(rightOffset, 0f);
        }

        private void SetFullStretch(GameObject obj)
        {
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
