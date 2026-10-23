using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

namespace SyntyCombined
{
    /// <summary>
    /// Core AAA Main Menu Hub & Scene Transition Controller.
    /// Manages Title screen, Main Hub navigation, AAA Sector Map Overview,
    /// Settings, Controls, visual map previews, and reliable back navigation.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        public static MainMenuController Instance { get; private set; }

        [Header("Target Scene Settings")]
        public string gameplaySceneName = "Demo_City";

        [Header("Screen Panels")]
        public GameObject titleScreenPanel;
        public GameObject mainHubPanel;
        public GameObject sectorSelectPanel;
        public GameObject settingsPanel;
        public GameObject controlsPanel;
        public GameObject loadingScreenPanel;

        [Header("Main Hub Navigation Buttons")]
        public Button quickStartButton;
        public Button sectorSelectNavButton;
        public Button settingsNavButton;
        public Button controlsNavButton;
        public Button exitNavButton;

        [Header("Back to Hub Buttons")]
        public Button backFromSectorButton;
        public Button backFromSettingsButton;
        public Button backFromControlsButton;

        [Header("AAA Sector Map Selection UI")]
        public Button launchSectorButton;
        public TMP_Dropdown sectorDropdown;
        public TMP_Text sectorNameTitleText;
        public TMP_Text sectorThreatLevelText;
        public TMP_Text sectorCoordinatesText;
        public TMP_Text sectorDescriptionText;
        public TMP_Text sectorPoiListText;
        public Image sectorPreviewImage;
        public Button sectorNodePlazaButton;
        public Button sectorNodeAlleyButton;
        public Button sectorNodeTowerButton;

        [Header("Loading Screen Elements")]
        public Slider loadingProgressBar;
        public TMP_Text loadingPercentText;
        public TMP_Text loadingStatusText;
        public string[] cyberTips = new string[]
        {
            "TIP: Perfect parries stun enemies and expose critical strike windows.",
            "TIP: Light attacks can be chained fluidly into heavy sword combos.",
            "TIP: Keep an eye on your posture meter during intense blade duels.",
            "TIP: Exploration in Cyber City unveils hidden upgrades and elite combatants."
        };

        private bool _isTitlePassed = false;
        private bool _isLoading = false;
        public bool IsLoading => _isLoading;

        public const string PREF_SELECTED_SECTOR = "CyberCity_SelectedSector";

        /// <summary>
        /// Gameplay-return hub: the NEW Discovery cutscene that starts the canonical
        /// Discovery -&gt; Tron Awakening -&gt; Demo_City flow. Never Scene_MainMenu,
        /// never the OLD Scene_Alita_Scrapyard_Discovery.
        /// </summary>
        public const string RETURN_HUB_SCENE = "Alita_Discovery_Cutscene";

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            _isLoading = false;
            Time.timeScale = 1f;

            // Unlock cursor for UI interaction
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Setup initial panel visibility
            if (titleScreenPanel != null)
            {
                titleScreenPanel.SetActive(true);
                if (mainHubPanel) mainHubPanel.SetActive(false);
            }
            else
            {
                _isTitlePassed = true;
                ShowMainHub();
            }

            if (sectorSelectPanel) sectorSelectPanel.SetActive(false);
            if (settingsPanel) settingsPanel.SetActive(false);
            if (controlsPanel) controlsPanel.SetActive(false);
            if (loadingScreenPanel) loadingScreenPanel.SetActive(false);

            HookButtons();
            AutoHookButtons();
            SanitizeSectorSelectUI();
            InitializeSectorDropdown();
        }

        public void AutoHookButtons()
        {
            bool hasAlitaHUD = Object.FindAnyObjectByType<Alita.AlitaStartMenuHUD>() != null;

            if (!hasAlitaHUD && mainHubPanel != null)
            {
                Button[] hubBtns = mainHubPanel.GetComponentsInChildren<Button>(true);
                foreach (Button b in hubBtns)
                {
                    string name = b.name.ToUpper();
                    TMP_Text tmp = b.GetComponentInChildren<TMP_Text>(true);
                    string txt = tmp != null ? tmp.text.ToUpper() : "";

                    if (name.Contains("NEWGAME") || name.Contains("START") || txt.Contains("START DEMO"))
                    {
                        quickStartButton = b;
                        b.onClick.RemoveAllListeners();
                        b.onClick.AddListener(OnQuickStartClicked);
                    }
                    else if (name.Contains("CONTINUE") || name.Contains("LOADGAME") || txt.Contains("TACTICAL MAP") || txt.Contains("OVERVIEW"))
                    {
                        sectorSelectNavButton = b;
                        b.onClick.RemoveAllListeners();
                        b.onClick.AddListener(ShowSectorSelect);
                    }
                    else if (name.Contains("SETTINGS") || txt.Contains("SETTINGS"))
                    {
                        settingsNavButton = b;
                        b.onClick.RemoveAllListeners();
                        b.onClick.AddListener(ShowSettings);
                    }
                    else if (name.Contains("CREDITS") || name.Contains("CONTROLS") || txt.Contains("CONTROLS"))
                    {
                        controlsNavButton = b;
                        b.onClick.RemoveAllListeners();
                        b.onClick.AddListener(ShowControls);
                    }
                    else if (name.Contains("QUIT") || name.Contains("EXIT") || txt.Contains("EXIT"))
                    {
                        exitNavButton = b;
                        b.onClick.RemoveAllListeners();
                        b.onClick.AddListener(QuitGame);
                    }
                }
            }

            if (sectorSelectPanel != null)
            {
                Button[] secBtns = sectorSelectPanel.GetComponentsInChildren<Button>(true);
                foreach (Button b in secBtns)
                {
                    string name = b.name.ToUpper();
                    TMP_Text tmp = b.GetComponentInChildren<TMP_Text>(true);
                    string txt = tmp != null ? tmp.text.ToUpper() : "";

                    if (name.Contains("BACK") || txt.Contains("BACK"))
                    {
                        backFromSectorButton = b;
                        b.onClick.RemoveAllListeners();
                        b.onClick.AddListener(ShowMainHub);
                    }
                    else if (name.Contains("LAUNCH") || txt.Contains("DEPLOY"))
                    {
                        launchSectorButton = b;
                        b.onClick.RemoveAllListeners();
                        b.onClick.AddListener(OnLaunchSelectedSector);
                    }
                    else if (name.Contains("PLAZA") || txt.Contains("PLAZA"))
                    {
                        sectorNodePlazaButton = b;
                        b.onClick.RemoveAllListeners();
                        b.onClick.AddListener(() => SetSectorIndex(0));
                    }
                    else if (name.Contains("ALLEY") || txt.Contains("ALLEY"))
                    {
                        sectorNodeAlleyButton = b;
                        b.onClick.RemoveAllListeners();
                        b.onClick.AddListener(() => SetSectorIndex(1));
                    }
                    else if (name.Contains("TOWER") || txt.Contains("TOWER"))
                    {
                        sectorNodeTowerButton = b;
                        b.onClick.RemoveAllListeners();
                        b.onClick.AddListener(() => SetSectorIndex(2));
                    }
                }
            }

            if (settingsPanel != null)
            {
                foreach (Button b in settingsPanel.GetComponentsInChildren<Button>(true))
                {
                    string name = b.name.ToUpper();
                    TMP_Text tmp = b.GetComponentInChildren<TMP_Text>(true);
                    string txt = tmp != null ? tmp.text.ToUpper() : "";

                    if (name.Contains("BACK") || name.Contains("CLOSE") || txt.Contains("BACK"))
                    {
                        backFromSettingsButton = b;
                        b.onClick.RemoveAllListeners();
                        b.onClick.AddListener(ShowMainHub);
                        break;
                    }
                }
            }

            if (controlsPanel != null)
            {
                foreach (Button b in controlsPanel.GetComponentsInChildren<Button>(true))
                {
                    string name = b.name.ToUpper();
                    TMP_Text tmp = b.GetComponentInChildren<TMP_Text>(true);
                    string txt = tmp != null ? tmp.text.ToUpper() : "";

                    if (name.Contains("BACK") || name.Contains("CLOSE") || txt.Contains("BACK"))
                    {
                        backFromControlsButton = b;
                        b.onClick.RemoveAllListeners();
                        b.onClick.AddListener(ShowMainHub);
                        break;
                    }
                }
            }
        }

        private void Update()
        {
            // Title screen press any key
            if (!_isTitlePassed && titleScreenPanel != null && titleScreenPanel.activeSelf)
            {
                bool anyInput = false;
                if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) anyInput = true;
                else if (Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame)) anyInput = true;
                else if (Input.anyKeyDown) anyInput = true;

                if (anyInput)
                {
                    _isTitlePassed = true;
                    titleScreenPanel.SetActive(false);
                    ShowMainHub();
                }
            }

            // ESC key navigation in Main Menu
            bool escPressed = false;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) escPressed = true;
            else if (Input.GetKeyDown(KeyCode.Escape)) escPressed = true;

            if (escPressed && _isTitlePassed && !_isLoading)
            {
                if (sectorSelectPanel != null && sectorSelectPanel.activeSelf) ShowMainHub();
                else if (settingsPanel != null && settingsPanel.activeSelf) ShowMainHub();
                else if (controlsPanel != null && controlsPanel.activeSelf) ShowMainHub();
            }
        }

        private void HookButtons()
        {
            bool hasAlitaHUD = Object.FindAnyObjectByType<Alita.AlitaStartMenuHUD>() != null;

            if (!hasAlitaHUD)
            {
                if (quickStartButton)
                {
                    quickStartButton.onClick.RemoveAllListeners();
                    quickStartButton.onClick.AddListener(OnQuickStartClicked);
                }

                if (sectorSelectNavButton)
                {
                    sectorSelectNavButton.onClick.RemoveAllListeners();
                    sectorSelectNavButton.onClick.AddListener(ShowSectorSelect);
                }

                if (settingsNavButton)
                {
                    settingsNavButton.onClick.RemoveAllListeners();
                    settingsNavButton.onClick.AddListener(ShowSettings);
                }

                if (controlsNavButton)
                {
                    controlsNavButton.onClick.RemoveAllListeners();
                    controlsNavButton.onClick.AddListener(ShowControls);
                }

                if (exitNavButton)
                {
                    exitNavButton.onClick.RemoveAllListeners();
                    exitNavButton.onClick.AddListener(QuitGame);
                }
            }

            // Back to Hub Buttons
            if (backFromSectorButton)
            {
                backFromSectorButton.onClick.RemoveAllListeners();
                backFromSectorButton.onClick.AddListener(ShowMainHub);
            }

            if (backFromSettingsButton)
            {
                backFromSettingsButton.onClick.RemoveAllListeners();
                backFromSettingsButton.onClick.AddListener(ShowMainHub);
            }

            if (backFromControlsButton)
            {
                backFromControlsButton.onClick.RemoveAllListeners();
                backFromControlsButton.onClick.AddListener(ShowMainHub);
            }

            if (launchSectorButton)
            {
                launchSectorButton.onClick.RemoveAllListeners();
                launchSectorButton.onClick.AddListener(OnLaunchSelectedSector);
            }

            if (sectorDropdown)
            {
                sectorDropdown.onValueChanged.RemoveAllListeners();
                sectorDropdown.onValueChanged.AddListener(OnSectorDropdownChanged);
            }

            // Map Node Pins
            if (sectorNodePlazaButton)
            {
                sectorNodePlazaButton.onClick.RemoveAllListeners();
                sectorNodePlazaButton.onClick.AddListener(() => SetSectorIndex(0));
            }
            if (sectorNodeAlleyButton)
            {
                sectorNodeAlleyButton.onClick.RemoveAllListeners();
                sectorNodeAlleyButton.onClick.AddListener(() => SetSectorIndex(1));
            }
            if (sectorNodeTowerButton)
            {
                sectorNodeTowerButton.onClick.RemoveAllListeners();
                sectorNodeTowerButton.onClick.AddListener(() => SetSectorIndex(2));
            }
        }

        private void InitializeSectorDropdown()
        {
            if (sectorDropdown == null) return;

            sectorDropdown.ClearOptions();
            List<string> options = new List<string>
            {
                "Sector 01: Main Cyber Plaza",
                "Sector 02: Downtown Cyber Alley",
                "Sector 03: Overlook Tower Roof",
                "Boss Arena: Titan & Samurai Mechs",
                "Combat Showcase: InsaneGun Sword Set",
                "Story Prologue: Scrapyard Discovery"
            };
            sectorDropdown.AddOptions(options);

            int savedSector = PlayerPrefs.GetInt(PREF_SELECTED_SECTOR, 0);
            sectorDropdown.value = savedSector;
            sectorDropdown.RefreshShownValue();
            OnSectorDropdownChanged(savedSector);
        }

        public void SetSectorIndex(int index)
        {
            if (sectorDropdown != null)
            {
                sectorDropdown.value = index;
                sectorDropdown.RefreshShownValue();
            }
            OnSectorDropdownChanged(index);
        }

        public void ShowMainHub()
        {
            if (Alita.AlitaStartMenuHUD.Instance != null)
            {
                Alita.AlitaStartMenuHUD.Instance.PlayClickSound();
            }
            HideAllPanels();
            if (mainHubPanel)
            {
                mainHubPanel.SetActive(true);
                CanvasGroup cg = mainHubPanel.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    cg.alpha = 1f;
                    cg.interactable = true;
                    cg.blocksRaycasts = true;
                }
            }
        }

        public void ShowSectorSelect()
        {
            HideAllPanels();
            if (sectorSelectPanel)
            {
                sectorSelectPanel.SetActive(true);
                SanitizeSectorSelectUI();
            }
            OnSectorDropdownChanged(PlayerPrefs.GetInt(PREF_SELECTED_SECTOR, 0));
        }

        public void SanitizeSectorSelectUI()
        {
            if (sectorSelectPanel == null) return;

            // 1. Ensure Back to Hub button exists and is properly positioned
            if (backFromSectorButton == null)
            {
                Button[] allBtns = sectorSelectPanel.GetComponentsInChildren<Button>(true);
                foreach (Button b in allBtns)
                {
                    string n = b.name.ToUpper();
                    TMP_Text txt = b.GetComponentInChildren<TMP_Text>(true);
                    string tStr = txt != null ? txt.text.ToUpper() : "";
                    if (n.Contains("BACK") || tStr.Contains("BACK"))
                    {
                        backFromSectorButton = b;
                        break;
                    }
                }
            }

            if (backFromSectorButton == null)
            {
                // Dynamically create BACK TO HUB button
                GameObject btnPrefab = Resources.Load<GameObject>("SyntyUI/Button_SciFiMenus_Menu_Item_01") ?? Resources.Load<GameObject>("Button_SciFiMenus_Menu_Item_01");
                GameObject backObj = btnPrefab != null ? Instantiate(btnPrefab, sectorSelectPanel.transform) : new GameObject("Btn_BackSector");
                if (btnPrefab == null)
                {
                    backObj.transform.SetParent(sectorSelectPanel.transform, false);
                    Image img = backObj.AddComponent<Image>();
                    img.color = new Color(0.08f, 0.25f, 0.40f, 0.95f);
                }
                backObj.name = "Btn_BackSector";
                backFromSectorButton = backObj.GetComponent<Button>() ?? backObj.AddComponent<Button>();
            }

            if (backFromSectorButton != null)
            {
                backFromSectorButton.gameObject.SetActive(true);
                RectTransform rt = backFromSectorButton.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = new Vector2(-550f, 380f);
                    rt.sizeDelta = new Vector2(220f, 50f);
                }

                TMP_Text tmp = backFromSectorButton.GetComponentInChildren<TMP_Text>(true);
                if (tmp != null)
                {
                    tmp.text = "< BACK TO HUB";
                    tmp.textWrappingMode = TextWrappingModes.NoWrap;
                    tmp.overflowMode = TextOverflowModes.Overflow;
                    tmp.alignment = TextAlignmentOptions.Center;
                }

                backFromSectorButton.onClick.RemoveAllListeners();
                backFromSectorButton.onClick.AddListener(ShowMainHub);
            }

            // 2. Fix map pin anchors and positions so they don't overflow
            FixButtonAnchors(sectorNodePlazaButton, new Vector2(-180f, 100f), new Vector2(160f, 45f), "[01] PLAZA");
            FixButtonAnchors(sectorNodeAlleyButton, new Vector2(150f, -100f), new Vector2(160f, 45f), "[02] ALLEY");
            FixButtonAnchors(sectorNodeTowerButton, new Vector2(0f, 180f), new Vector2(160f, 45f), "[03] TOWER");

            // 3. Fix Launch Sector button anchors and positioning
            if (launchSectorButton != null)
            {
                FixButtonAnchors(launchSectorButton, new Vector2(0f, -210f), new Vector2(400f, 60f), "DEPLOY TO SECTOR");
                launchSectorButton.onClick.RemoveAllListeners();
                launchSectorButton.onClick.AddListener(OnLaunchSelectedSector);
            }
        }

        private void FixButtonAnchors(Button btn, Vector2 pos, Vector2 size, string labelText)
        {
            if (btn == null) return;
            btn.gameObject.SetActive(true);

            RectTransform rt = btn.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = pos;
                rt.sizeDelta = size;
            }

            TMP_Text tmp = btn.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null)
            {
                tmp.text = labelText;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.overflowMode = TextOverflowModes.Overflow;
                tmp.alignment = TextAlignmentOptions.Center;
            }
        }

        public void ShowSettings()
        {
            HideAllPanels();
            if (settingsPanel)
            {
                settingsPanel.SetActive(true);
                SanitizeSettingsUI();
            }
        }

        public void ShowControls()
        {
            HideAllPanels();
            if (controlsPanel)
            {
                controlsPanel.SetActive(true);
                SanitizeControlsUI();
            }
        }

        private void SanitizeSettingsUI()
        {
            if (settingsPanel == null) return;
            if (backFromSettingsButton == null)
            {
                foreach (Button b in settingsPanel.GetComponentsInChildren<Button>(true))
                {
                    string name = b.name.ToUpper();
                    TMP_Text tmp = b.GetComponentInChildren<TMP_Text>(true);
                    string txt = tmp != null ? tmp.text.ToUpper() : "";
                    if (name.Contains("BACK") || name.Contains("CLOSE") || txt.Contains("BACK"))
                    {
                        backFromSettingsButton = b;
                        break;
                    }
                }
            }
            if (backFromSettingsButton != null)
            {
                backFromSettingsButton.gameObject.SetActive(true);
                backFromSettingsButton.onClick.RemoveAllListeners();
                backFromSettingsButton.onClick.AddListener(ShowMainHub);
            }
        }

        private void SanitizeControlsUI()
        {
            if (controlsPanel == null) return;
            if (backFromControlsButton == null)
            {
                foreach (Button b in controlsPanel.GetComponentsInChildren<Button>(true))
                {
                    string name = b.name.ToUpper();
                    TMP_Text tmp = b.GetComponentInChildren<TMP_Text>(true);
                    string txt = tmp != null ? tmp.text.ToUpper() : "";
                    if (name.Contains("BACK") || name.Contains("CLOSE") || txt.Contains("BACK"))
                    {
                        backFromControlsButton = b;
                        break;
                    }
                }
            }
            if (backFromControlsButton != null)
            {
                backFromControlsButton.gameObject.SetActive(true);
                backFromControlsButton.onClick.RemoveAllListeners();
                backFromControlsButton.onClick.AddListener(ShowMainHub);
            }
        }

        private void HideAllPanels()
        {
            if (mainHubPanel) mainHubPanel.SetActive(false);
            if (sectorSelectPanel) sectorSelectPanel.SetActive(false);
            if (settingsPanel) settingsPanel.SetActive(false);
            if (controlsPanel) controlsPanel.SetActive(false);
        }

        private void OnSectorDropdownChanged(int index)
        {
            PlayerPrefs.SetInt(PREF_SELECTED_SECTOR, index);

            // Update real map preview image
            if (sectorPreviewImage != null)
            {
                sectorPreviewImage.sprite = CyberCityMapAssets.GetSectorMapSprite(index);
            }

            switch (index)
            {
                case 0:
                    if (sectorNameTitleText) sectorNameTitleText.text = "SECTOR 01 // MAIN CYBER PLAZA";
                    if (sectorThreatLevelText) sectorThreatLevelText.text = "THREAT LEVEL: LOW (CIVILIAN HUB)";
                    if (sectorCoordinatesText) sectorCoordinatesText.text = "GRID: 34.82 N // 118.24 W";
                    if (sectorDescriptionText) sectorDescriptionText.text = "Central urban hub with neutral civilian traffic and primary dueling arena.";
                    if (sectorPoiListText) sectorPoiListText.text = "POINTS OF INTEREST:\n• Central Market Square\n• Duelist Training Arena\n• Skyway Transit Terminal";
                    break;

                case 1:
                    if (sectorNameTitleText) sectorNameTitleText.text = "SECTOR 02 // DOWNTOWN CYBER ALLEY";
                    if (sectorThreatLevelText) sectorThreatLevelText.text = "THREAT LEVEL: HIGH (COMBAT ZONE)";
                    if (sectorCoordinatesText) sectorCoordinatesText.text = "GRID: 34.89 N // 118.31 W";
                    if (sectorDescriptionText) sectorDescriptionText.text = "Narrow futuristic corridor filled with rogue cyber mercenaries and dark alleyways.";
                    if (sectorPoiListText) sectorPoiListText.text = "POINTS OF INTEREST:\n• Black Market Cyberware\n• Sub-Alley Encounter Zone\n• Drainage Tunnel Waypoint";
                    break;

                case 2:
                    if (sectorNameTitleText) sectorNameTitleText.text = "SECTOR 03 // OVERLOOK TOWER ROOF";
                    if (sectorThreatLevelText) sectorThreatLevelText.text = "THREAT LEVEL: EXTREME (ELITE DUELS)";
                    if (sectorCoordinatesText) sectorCoordinatesText.text = "GRID: 35.01 N // 118.15 W";
                    if (sectorDescriptionText) sectorDescriptionText.text = "High-altitude rooftop arena overlooking the glowing neon skyline of Cyber City.";
                    if (sectorPoiListText) sectorPoiListText.text = "POINTS OF INTEREST:\n• Helipad Duel Ring\n• Corporation Penthouse\n• High-Vantage Drone Platform";
                    break;

                case 3:
                    if (sectorNameTitleText) sectorNameTitleText.text = "COMBAT ARENA // TITAN & SAMURAI MECHS";
                    if (sectorThreatLevelText) sectorThreatLevelText.text = "THREAT LEVEL: FATAL (BOSS ENCOUNTER)";
                    if (sectorCoordinatesText) sectorCoordinatesText.text = "SECTOR X // HEAVY INDUSTRIAL DOCK";
                    if (sectorDescriptionText) sectorDescriptionText.text = "Colossal mech battleground featuring Goliath-02 and Kensei-01 blade mechs.";
                    if (sectorPoiListText) sectorPoiListText.text = "ENEMIES:\n• GOLIATH-02 (Heavy Greatsword Titan Mech)\n• KENSEI-01 (Berserk Samurai Iaido Mech)";
                    break;

                case 4:
                    if (sectorNameTitleText) sectorNameTitleText.text = "COMBAT SHOWCASE // DEAD AS DISCO";
                    if (sectorThreatLevelText) sectorThreatLevelText.text = "THREAT LEVEL: HIGH (COMBAT SHOWCASE)";
                    if (sectorCoordinatesText) sectorCoordinatesText.text = "BOULEVARD PLAZA // NEON DISTRICT";
                    if (sectorDescriptionText) sectorDescriptionText.text = "Dead as Disco combat system with holographic ghost lunges, 7-hit combos, and gun-kata.";
                    if (sectorPoiListText) sectorPoiListText.text = "FEATURES:\n• Flow Dash & Holographic Projection\n• 4-Way Dodging & Heavy Combos\n• Dynamic Street Dummies";
                    break;

                case 5:
                    if (sectorNameTitleText) sectorNameTitleText.text = "STORY PROLOGUE // SCRAPYARD DISCOVERY";
                    if (sectorThreatLevelText) sectorThreatLevelText.text = "THREAT LEVEL: CINEMATIC / DISCOVERY";
                    if (sectorCoordinatesText) sectorCoordinatesText.text = "SCRAP SLAB 09 // BENEATH ZALEM";
                    if (sectorDescriptionText) sectorDescriptionText.text = "Cinematic discovery scene of Broken Alita cyborg and active URM antimatter heart core.";
                    if (sectorPoiListText) sectorPoiListText.text = "FEATURES:\n• Movie-Authentic Broken Alita Cyborg\n• Beating Antimatter Heart Core\n• Cinematic Diagnostic Scan";
                    break;
            }
        }

        public void OnQuickStartClicked()
        {
            LoadSceneDirect(gameplaySceneName);
        }

        public void OnLaunchSelectedSector()
        {
            int sectorIndex = PlayerPrefs.GetInt(PREF_SELECTED_SECTOR, 0);
            string targetScene = gameplaySceneName;
            switch (sectorIndex)
            {
                case 3: targetScene = "SyntyCombatDemo"; break;
                case 4: targetScene = "InsaneGun_Sword_Showcase"; break;
                case 5: targetScene = RETURN_HUB_SCENE; break;
                default: targetScene = gameplaySceneName; break;
            }
            LoadSceneDirect(targetScene);
        }

        public void LoadSceneDirect(string sceneName)
        {
            if (_isLoading) return;
            StartCoroutine(LoadSceneRoutineDirect(sceneName));
        }

        private IEnumerator LoadSceneRoutineDirect(string sceneName)
        {
            _isLoading = true;
            HideAllPanels();

            if (loadingScreenPanel != null)
            {
                loadingScreenPanel.SetActive(true);
            }

            if (loadingStatusText != null && cyberTips != null && cyberTips.Length > 0)
            {
                string randomTip = cyberTips[Random.Range(0, cyberTips.Length)];
                loadingStatusText.text = randomTip;
            }

            if (loadingProgressBar != null) loadingProgressBar.value = 0.5f;
            if (loadingPercentText != null) loadingPercentText.text = "LOADING CYBER CITY...";

            yield return null;

            if (loadingProgressBar != null) loadingProgressBar.value = 1f;
            if (loadingPercentText != null) loadingPercentText.text = "100% - SYSTEM READY";

            yield return new WaitForSecondsRealtime(0.1f);

            SceneManager.LoadScene(sceneName);
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
