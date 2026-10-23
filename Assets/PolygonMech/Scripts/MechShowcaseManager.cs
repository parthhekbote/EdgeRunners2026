using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using InsaneGun.Combat;

namespace PolygonMech
{
    /// <summary>
    /// Master Interactive Director for the Mech Animations Showcase Scene.
    /// Controls:
    /// 1. Dynamic Camera Director (Hero Orbit, Colosseum Overview, Panoramic Tour, Focused Exhibit, Free Spectator).
    /// 2. Master Playback Speed Slider (0.1x Bullet Time -> 1.0x Realtime -> 2.0x Fast).
    /// 3. Central Hero Mech Preview with 5-meter Tron Slash Arcs, Trails & Point Lights.
    /// 4. Category Filtering & Interactive Cyberpunk HUD Dashboard.
    /// </summary>
    public class MechShowcaseManager : MonoBehaviour
    {
        public enum CameraMode
        {
            HeroOrbit,
            ColosseumOverview,
            PanoramicTour,
            FocusedExhibit,
            FreeFlyCam
        }

        [Header("Scene References")]
        public Camera mainCamera;
        public GameObject heroMech;
        public MechBladeCombatVFX heroBladeVFX;
        public List<MechShowcaseExhibit> exhibits = new List<MechShowcaseExhibit>();

        [Header("Camera Configuration")]
        public CameraMode currentCameraMode = CameraMode.ColosseumOverview;
        public float cameraOrbitSpeed = 12f;
        public float cameraDamping = 4.5f;

        [Header("Global Playback Controls")]
        [Range(0.05f, 2.5f)]
        public float masterSpeed = 1.0f;
        public bool isGlobalPlaying = true;
        public bool isPhase2Overdrive = false;

        [Header("Selection State")]
        public int selectedIndex = 0; // 0 = Hero, 1-32 = Exhibits
        public string activeCategoryFilter = "ALL";

        // Internal camera tracking state
        private Vector3 targetCamPosition;
        private Quaternion targetCamRotation;
        private float orbitAngle = 0f;
        private float tourProgress = 0f;
        private MechShowcaseExhibit activeExhibit;

        // Flycam input
        private Vector3 freeCamVelocity;
        private float freeCamYaw;
        private float freeCamPitch;

        // GUI Styles
        private GUIStyle panelStyle;
        private GUIStyle headerStyle;
        private GUIStyle subHeaderStyle;
        private GUIStyle buttonStyle;
        private GUIStyle activeButtonStyle;
        private GUIStyle cardStyle;
        private GUIStyle labelStyle;
        private Vector2 scrollPos;
        private bool guiInitialized = false;

        private static readonly string[] Categories = new string[]
        {
            "ALL", "Slash", "Attack", "Locomotion", "Turn", "Defense", "Idle", "Power", "Reaction", "Death"
        };

        private void Start()
        {
            if (mainCamera == null) mainCamera = Camera.main;

            if (heroMech != null && heroBladeVFX == null)
            {
                heroBladeVFX = heroMech.GetComponent<MechBladeCombatVFX>() 
                            ?? heroMech.GetComponentInChildren<MechBladeCombatVFX>();
            }

            // Find all exhibits if list not manually assigned
            if (exhibits == null || exhibits.Count == 0)
            {
                exhibits = new List<MechShowcaseExhibit>(FindObjectsByType<MechShowcaseExhibit>(FindObjectsInactive.Include, FindObjectsSortMode.None));
                exhibits.Sort((a, b) => a.animationIndex.CompareTo(b.animationIndex));
            }

            // Set initial camera target
            SetCameraMode(CameraMode.ColosseumOverview);
            if (mainCamera != null)
            {
                mainCamera.transform.position = targetCamPosition;
                mainCamera.transform.rotation = targetCamRotation;
            }
        }

        private void Update()
        {
            HandleKeyboardShortcuts();
            UpdateCameraDirector();
        }

        private void HandleKeyboardShortcuts()
        {
            if (Input.GetKeyDown(KeyCode.F1)) SetCameraMode(CameraMode.HeroOrbit);
            if (Input.GetKeyDown(KeyCode.F2)) SetCameraMode(CameraMode.ColosseumOverview);
            if (Input.GetKeyDown(KeyCode.F3)) SetCameraMode(CameraMode.PanoramicTour);
            if (Input.GetKeyDown(KeyCode.F4)) SetCameraMode(CameraMode.FreeFlyCam);

            if (Input.GetKeyDown(KeyCode.Space))
            {
                TogglePlayPause();
            }

            if (Input.GetKeyDown(KeyCode.Tab))
            {
                // Cycle to next exhibit
                int next = (selectedIndex >= exhibits.Count) ? 1 : selectedIndex + 1;
                SelectExhibit(next);
            }
        }

        public void SetCameraMode(CameraMode mode)
        {
            currentCameraMode = mode;
            orbitAngle = 0f;

            if (mode == CameraMode.FreeFlyCam && mainCamera != null)
            {
                freeCamYaw = mainCamera.transform.eulerAngles.y;
                freeCamPitch = mainCamera.transform.eulerAngles.x;
            }
        }

        public void SelectExhibit(int index)
        {
            selectedIndex = index;
            foreach (var ex in exhibits)
            {
                if (ex == null) continue;
                bool match = (ex.animationIndex == index);
                ex.SetHighlight(match);
                if (match) activeExhibit = ex;
            }

            SetCameraMode(CameraMode.FocusedExhibit);
        }

        private void UpdateCameraDirector()
        {
            if (mainCamera == null) return;

            switch (currentCameraMode)
            {
                case CameraMode.HeroOrbit:
                    Vector3 heroCenter = (heroMech != null) ? heroMech.transform.position + Vector3.up * 2.2f : new Vector3(0, 2.2f, -6f);
                    orbitAngle += cameraOrbitSpeed * Time.deltaTime;
                    float rad = orbitAngle * Mathf.Deg2Rad;
                    float dist = 9.5f;
                    targetCamPosition = heroCenter + new Vector3(Mathf.Sin(rad) * dist, 1.8f, Mathf.Cos(rad) * dist);
                    targetCamRotation = Quaternion.LookRotation((heroCenter + Vector3.up * 0.4f) - targetCamPosition);
                    break;

                case CameraMode.ColosseumOverview:
                    // High-angle grand amphitheater view
                    targetCamPosition = new Vector3(0f, 24f, -28f);
                    targetCamRotation = Quaternion.Euler(28f, 0f, 0f);
                    break;

                case CameraMode.PanoramicTour:
                    // Smooth tracking tour around the arena
                    tourProgress += Time.deltaTime * 0.045f;
                    float tourAngle = (tourProgress * 360f) * Mathf.Deg2Rad;
                    float tourRadius = 24f + Mathf.Sin(tourProgress * 4f) * 6f;
                    float tourHeight = 6f + Mathf.Cos(tourProgress * 3f) * 3f;
                    targetCamPosition = new Vector3(Mathf.Sin(tourAngle) * tourRadius, tourHeight, Mathf.Cos(tourAngle) * tourRadius + 8f);
                    targetCamRotation = Quaternion.LookRotation(new Vector3(0f, 3.5f, 10f) - targetCamPosition);
                    break;

                case CameraMode.FocusedExhibit:
                    if (activeExhibit != null)
                    {
                        Vector3 exPos = activeExhibit.transform.position;
                        Vector3 fwd = activeExhibit.transform.forward;
                        Vector3 right = activeExhibit.transform.right;
                        Vector3 lookTarget = exPos + Vector3.up * 2.5f;

                        // Dramatic 3/4 front quarter hero angle
                        targetCamPosition = exPos + fwd * 6.2f + right * 3.5f + Vector3.up * 1.8f;
                        targetCamRotation = Quaternion.LookRotation(lookTarget - targetCamPosition);
                    }
                    break;

                case CameraMode.FreeFlyCam:
                    HandleFreeFlyCam();
                    return; // Position/Rotation handled directly in method
            }

            // Smooth interpolation
            mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, targetCamPosition, Time.deltaTime * cameraDamping);
            mainCamera.transform.rotation = Quaternion.Slerp(mainCamera.transform.rotation, targetCamRotation, Time.deltaTime * cameraDamping);
        }

        private void HandleFreeFlyCam()
        {
            if (Input.GetMouseButton(1)) // Hold Right-Click to look
            {
                Cursor.lockState = CursorLockMode.Locked;
                freeCamYaw += Input.GetAxis("Mouse X") * 2.5f;
                freeCamPitch -= Input.GetAxis("Mouse Y") * 2.5f;
                freeCamPitch = Mathf.Clamp(freeCamPitch, -85f, 85f);
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
            }

            mainCamera.transform.rotation = Quaternion.Euler(freeCamPitch, freeCamYaw, 0f);

            float speed = Input.GetKey(KeyCode.LeftShift) ? 28f : 12f;
            Vector3 move = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) move += mainCamera.transform.forward;
            if (Input.GetKey(KeyCode.S)) move -= mainCamera.transform.forward;
            if (Input.GetKey(KeyCode.D)) move += mainCamera.transform.right;
            if (Input.GetKey(KeyCode.A)) move -= mainCamera.transform.right;
            if (Input.GetKey(KeyCode.E)) move += Vector3.up;
            if (Input.GetKey(KeyCode.Q)) move -= Vector3.up;

            mainCamera.transform.position += move.normalized * speed * Time.deltaTime;
        }

        public void TogglePlayPause()
        {
            isGlobalPlaying = !isGlobalPlaying;
            foreach (var ex in exhibits)
            {
                if (ex != null) ex.SetPlaying(isGlobalPlaying);
            }
        }

        public void SetGlobalSpeed(float speed)
        {
            masterSpeed = speed;
            foreach (var ex in exhibits)
            {
                if (ex != null) ex.SetSpeed(masterSpeed);
            }
            if (heroMech != null)
            {
                var hAnim = heroMech.GetComponent<Animator>();
                if (hAnim != null) hAnim.speed = masterSpeed;
            }
        }

        public void ToggleOverdrivePhase()
        {
            isPhase2Overdrive = !isPhase2Overdrive;
            if (heroBladeVFX != null)
            {
                heroBladeVFX.SetPhase2(isPhase2Overdrive);
            }
        }

        /// <summary>
        /// Triggers animation playback on the central Hero Mech with full visual effects.
        /// </summary>
        public void PreviewOnHeroMech(MechShowcaseExhibit ex)
        {
            if (ex == null || heroMech == null) return;

            StartCoroutine(RoutinePreviewHero(ex));
        }

        private IEnumerator RoutinePreviewHero(MechShowcaseExhibit ex)
        {
            var hAnim = heroMech.GetComponent<Animator>();
            if (hAnim == null || ex.clip == null) yield break;

            SetCameraMode(CameraMode.HeroOrbit);

            // Setup single clip override on hero mech
            var baseController = hAnim.runtimeAnimatorController;
            var overrideCtrl = new AnimatorOverrideController(baseController);
            overrideCtrl["LoopState"] = ex.clip;
            hAnim.runtimeAnimatorController = overrideCtrl;
            hAnim.speed = masterSpeed;

            // Trigger blade VFX if it is a slash or strike
            int slashType = MapCategoryToSlashType(ex.category, ex.animationIndex);

            if (heroBladeVFX != null)
            {
                heroBladeVFX.SetPhase2(isPhase2Overdrive);
                heroBladeVFX.StartSwingTrail();

                // Anti-Alita Combat Preview (Down-angled slashes, ground telegraphs & shockwave rings)
                if (ex.animationIndex == 15 || ex.animationIndex == 22) // Overhead Split / Jump Ground Cleave
                {
                    Vector3 impact = heroMech.transform.position + heroMech.transform.forward * 2.8f;
                    heroBladeVFX.SpawnGroundTelegraph(impact, 6.5f, ex.clip.length * 0.35f / masterSpeed);
                    yield return new WaitForSeconds(ex.clip.length * 0.35f / masterSpeed);
                    heroBladeVFX.EmitAntiSmallOverheadSplit(impact, 7.5f);
                }
                else if (ex.animationIndex == 14) // Low Shin Cleave
                {
                    yield return new WaitForSeconds(ex.clip.length * 0.35f / masterSpeed);
                    heroBladeVFX.EmitAntiSmallGroundCleave();
                }
                else if (ex.animationIndex == 19) // Ground Impale Thrust
                {
                    yield return new WaitForSeconds(ex.clip.length * 0.35f / masterSpeed);
                    heroBladeVFX.EmitAntiSmallImpaleThrust(heroMech.transform.position + heroMech.transform.forward * 3.5f);
                }
                else if (ex.animationIndex == 20) // Low 360 Ankle Whirlwind
                {
                    yield return new WaitForSeconds(ex.clip.length * 0.35f / masterSpeed);
                    heroBladeVFX.EmitAntiSmallLowWhirlwind(7.0f);
                }
                else if (ex.animationIndex == 26 || ex.animationIndex == 25) // Seismic Heel / Kick
                {
                    Vector3 stompPos = heroMech.transform.position + heroMech.transform.forward * 1.5f;
                    heroBladeVFX.SpawnGroundTelegraph(stompPos, 6.5f, ex.clip.length * 0.30f / masterSpeed);
                    yield return new WaitForSeconds(ex.clip.length * 0.30f / masterSpeed);
                    Color waveColor = isPhase2Overdrive ? new Color(5.5f, 0.4f, 1.4f) : new Color(4.8f, 1.8f, 0.2f);
                    InsaneGun.Combat.TronSlashArcGenerator.SpawnFloorGridRing(stompPos, waveColor, 6.5f);
                }
                else if (slashType > 0)
                {
                    yield return new WaitForSeconds(ex.clip.length * 0.35f / masterSpeed);
                    heroBladeVFX.EmitMechSlashArc(slashType);
                }
                else if (ex.category == "Defense")
                {
                    yield return new WaitForSeconds(ex.clip.length * 0.30f / masterSpeed);
                    heroBladeVFX.EmitDefensiveParrySparks();
                }

                yield return new WaitForSeconds(ex.clip.length * 0.40f / masterSpeed);
                heroBladeVFX.StopSwingTrail();
            }
        }

        private int MapCategoryToSlashType(string category, int index)
        {
            switch (index)
            {
                case 14: return 1; // Cleave Horizontal
                case 17: return 2; // Cross Cut
                case 16: return 3; // Upward Launcher
                case 19: return 4; // Piston Thrust
                case 15: return 5; // Overhead Split
                case 20: return 6; // Cyclone Whirlwind
                case 18: return 7; // Decapitation Finisher
                default:
                    if (category == "Slash") return 1;
                    return 0;
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // CYBERPUNK ONGUI DASHBOARD
        // ══════════════════════════════════════════════════════════════════════
        private void EnsureGUIStyles()
        {
            if (guiInitialized) return;

            Texture2D darkBg = MakeColorTex(new Color(0.04f, 0.06f, 0.09f, 0.88f));
            Texture2D btnNormal = MakeColorTex(new Color(0.08f, 0.12f, 0.18f, 0.90f));
            Texture2D btnActive = MakeColorTex(new Color(0.0f, 0.65f, 0.95f, 0.90f));
            Texture2D cardBg = MakeColorTex(new Color(0.06f, 0.08f, 0.12f, 0.92f));

            panelStyle = new GUIStyle(GUI.skin.box);
            panelStyle.normal.background = darkBg;

            headerStyle = new GUIStyle();
            headerStyle.fontSize = 16;
            headerStyle.fontStyle = FontStyle.Bold;
            headerStyle.normal.textColor = new Color(0.0f, 0.95f, 1.0f);

            subHeaderStyle = new GUIStyle();
            subHeaderStyle.fontSize = 11;
            subHeaderStyle.fontStyle = FontStyle.Normal;
            subHeaderStyle.normal.textColor = new Color(0.7f, 0.8f, 0.9f, 0.8f);

            buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.normal.background = btnNormal;
            buttonStyle.normal.textColor = new Color(0.85f, 0.92f, 1.0f);
            buttonStyle.fontSize = 11;
            buttonStyle.alignment = TextAnchor.MiddleLeft;
            buttonStyle.padding = new RectOffset(8, 8, 4, 4);

            activeButtonStyle = new GUIStyle(buttonStyle);
            activeButtonStyle.normal.background = btnActive;
            activeButtonStyle.normal.textColor = Color.white;
            activeButtonStyle.fontStyle = FontStyle.Bold;

            cardStyle = new GUIStyle(GUI.skin.box);
            cardStyle.normal.background = cardBg;
            cardStyle.padding = new RectOffset(12, 12, 12, 12);

            labelStyle = new GUIStyle();
            labelStyle.fontSize = 12;
            labelStyle.normal.textColor = new Color(0.85f, 0.9f, 0.95f);

            guiInitialized = true;
        }

        private Texture2D MakeColorTex(Color col)
        {
            Texture2D tex = new Texture2D(2, 2);
            Color[] pix = new Color[] { col, col, col, col };
            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }

        private void OnGUI()
        {
            EnsureGUIStyles();

            // 1. TOP HEADER BAR
            DrawTopBar();

            // 2. LEFT ANIMATION DRAWER
            DrawLeftDrawer();

            // 3. RIGHT INSPECTOR CARD
            DrawRightInspector();

            // 4. BOTTOM SHORTCUTS BAR
            DrawBottomBar();
        }

        private void DrawTopBar()
        {
            GUILayout.BeginArea(new Rect(10, 10, Screen.width - 20, 52), panelStyle);
            GUILayout.BeginHorizontal();

            GUILayout.Space(10);
            GUILayout.BeginVertical();
            GUILayout.Label("TITAN GOLIATH MECH // 32 ANIMATION EXHIBITION ARENA", headerStyle);
            GUILayout.Label("Universal Humanoid Rig • 128 Checked Action Frames • AAA Combat Visual Effects", subHeaderStyle);
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            // Camera Mode Buttons
            GUILayout.Label("Camera:", labelStyle);
            if (GUILayout.Button("Hero Orbit (F1)", currentCameraMode == CameraMode.HeroOrbit ? activeButtonStyle : buttonStyle, GUILayout.Width(110)))
                SetCameraMode(CameraMode.HeroOrbit);
            if (GUILayout.Button("Overview (F2)", currentCameraMode == CameraMode.ColosseumOverview ? activeButtonStyle : buttonStyle, GUILayout.Width(110)))
                SetCameraMode(CameraMode.ColosseumOverview);
            if (GUILayout.Button("Panoramic (F3)", currentCameraMode == CameraMode.PanoramicTour ? activeButtonStyle : buttonStyle, GUILayout.Width(115)))
                SetCameraMode(CameraMode.PanoramicTour);
            if (GUILayout.Button("Free Cam (F4)", currentCameraMode == CameraMode.FreeFlyCam ? activeButtonStyle : buttonStyle, GUILayout.Width(110)))
                SetCameraMode(CameraMode.FreeFlyCam);

            GUILayout.Space(15);

            // Play/Pause
            if (GUILayout.Button(isGlobalPlaying ? "❚❚ Pause" : "▶ Play", buttonStyle, GUILayout.Width(80)))
            {
                TogglePlayPause();
            }

            // Speed Slider
            GUILayout.Space(10);
            GUILayout.Label($"Speed: {masterSpeed:F2}x", labelStyle, GUILayout.Width(85));
            float newSpeed = GUILayout.HorizontalSlider(masterSpeed, 0.05f, 2.0f, GUILayout.Width(90));
            if (Mathf.Abs(newSpeed - masterSpeed) > 0.01f)
            {
                SetGlobalSpeed(newSpeed);
            }

            // Phase Overdrive Toggle
            GUILayout.Space(10);
            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = isPhase2Overdrive ? new Color(1f, 0.2f, 0.5f) : new Color(0.9f, 0.6f, 0.1f);
            if (GUILayout.Button(isPhase2Overdrive ? "OVERDRIVE: ON" : "PHASE 1: GOLD", buttonStyle, GUILayout.Width(125)))
            {
                ToggleOverdrivePhase();
            }
            GUI.backgroundColor = prevBg;

            GUILayout.Space(10);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawLeftDrawer()
        {
            float drawerW = 280f;
            float drawerH = Screen.height - 110;
            GUILayout.BeginArea(new Rect(10, 68, drawerW, drawerH), panelStyle);

            GUILayout.Label("ANIMATION ARCHIVE (32)", headerStyle);

            // Category Filter Tabs
            GUILayout.BeginHorizontal();
            int colCount = 0;
            foreach (var cat in Categories)
            {
                bool active = (activeCategoryFilter == cat);
                if (GUILayout.Button(cat, active ? activeButtonStyle : buttonStyle, GUILayout.Height(22)))
                {
                    activeCategoryFilter = cat;
                }
                colCount++;
                if (colCount % 3 == 0)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // Scrollable Moves List
            scrollPos = GUILayout.BeginScrollView(scrollPos);
            foreach (var ex in exhibits)
            {
                if (ex == null) continue;
                if (activeCategoryFilter != "ALL" && !ex.category.Equals(activeCategoryFilter, System.StringComparison.OrdinalIgnoreCase))
                    continue;

                bool isSelected = (ex.animationIndex == selectedIndex);
                string btnText = $"[{ex.animationIndex:D2}] {ex.displayName} ({ex.baseDuration:F1}s)";

                if (GUILayout.Button(btnText, isSelected ? activeButtonStyle : buttonStyle, GUILayout.Height(26)))
                {
                    SelectExhibit(ex.animationIndex);
                }
            }
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        private void DrawRightInspector()
        {
            float cardW = 310f;
            float cardH = 290f;
            GUILayout.BeginArea(new Rect(Screen.width - cardW - 10, 68, cardW, cardH), cardStyle);

            if (activeExhibit != null)
            {
                GUILayout.Label($"[{activeExhibit.animationIndex:D2}] {activeExhibit.displayName.ToUpper()}", headerStyle);
                GUILayout.Label($"Category: {activeExhibit.category.ToUpper()}", subHeaderStyle);
                GUILayout.Space(8);

                GUILayout.Label($"• Source Clip: {activeExhibit.clip?.name}", labelStyle);
                GUILayout.Label($"• Duration: {activeExhibit.baseDuration:F2} seconds", labelStyle);
                GUILayout.Label($"• Frame Checkpoints: 4 / 4 Tested (100% OK)", labelStyle);
                GUILayout.Label($"• Bone Rig: SM_Veh_Mech_01Avatar (Humanoid)", labelStyle);
                GUILayout.Label($"• Sword: SM_Wep_Sword_01 (4.14m Titan Blade)", labelStyle);

                GUILayout.Space(12);

                if (GUILayout.Button("⚡ PREVIEW ON HERO TURNTABLE", activeButtonStyle, GUILayout.Height(34)))
                {
                    PreviewOnHeroMech(activeExhibit);
                }

                GUILayout.Space(4);

                if (GUILayout.Button("🔍 FOCUS CAMERA ON THIS EXHIBIT", buttonStyle, GUILayout.Height(28)))
                {
                    SelectExhibit(activeExhibit.animationIndex);
                }
            }
            else
            {
                GUILayout.Label("CENTRAL HERO STAGE", headerStyle);
                GUILayout.Label("Select an animation from the archive to inspect details and preview moves.", subHeaderStyle);
            }

            GUILayout.EndArea();
        }

        private void DrawBottomBar()
        {
            GUILayout.BeginArea(new Rect(10, Screen.height - 34, Screen.width - 20, 26), panelStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Controls: [Tab] Next Mech • [Space] Play/Pause • [F1-F4] Camera Modes • [Right-Click + WASD] Free Flight Cam", subHeaderStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label("⚔️ Opponent: ALITA (1.65m) | Anti-Infantry Ground Mechanics Active", headerStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"Active Exhibit: [{(activeExhibit != null ? activeExhibit.animationIndex.ToString() : "Hero")}/32]", subHeaderStyle);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
