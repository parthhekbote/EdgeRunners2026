using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SyntyCombined;

namespace PolygonMech.Showcase
{
    public class HammerMechShowcaseViewer : MonoBehaviour
    {
        [Header("Target References")]
        [SerializeField] private Animator mechAnimator;
        [SerializeField] private Transform hammerWeapon;
        [SerializeField] private Transform cameraOrbitTarget;
        [SerializeField] private Camera showcaseCamera;

        [Header("Camera Orbit Settings")]
        public float cameraDistance = 4.2f;
        public float cameraHeight = 1.4f;
        public float orbitSpeed = 4.0f;
        public float zoomSpeed = 2.0f;
        public bool autoTurntable = false;
        public float turntableSpeed = 25.0f;

        [Header("Animation Settings")]
        public float playbackSpeed = 1.0f;
        public bool loopCurrentAnimation = false;
        public bool autoReturnToIdle = true;
        public bool applyRootMotion = false;

        [Header("Impact VFX Feedback")]
        public GameObject impactVFXPrefab;
        public bool enableScreenShake = true;

        [Header("Intelligent AI Combat Mode")]
        public bool aiCombatMode = false;
        private HammerMechAI activeAI;
        private GameObject combatDummy;

        private float orbitAngleYaw = 25.0f;
        private float orbitAnglePitch = 12.0f;
        private Vector3 lastMousePos;
        private bool isDraggingMouse = false;
        private float shakeIntensity = 0f;
        private bool hasTriggeredImpact = false;

        private int currentCategoryIndex = 0;
        private int currentAnimIndex = 0;
        private string currentPlayingClipName = "Idle";
        private float currentClipLength = 1.0f;

        public struct AnimItem
        {
            public string displayName;
            public string stateName;
            public string description;

            public AnimItem(string display, string state, string desc)
            {
                displayName = display;
                stateName = state;
                description = desc;
            }
        }

        public class AnimCategory
        {
            public string name;
            public List<AnimItem> items = new List<AnimItem>();

            public AnimCategory(string name) { this.name = name; }
        }

        private List<AnimCategory> categories = new List<AnimCategory>();
        private List<AnimItem> allClipsFlat = new List<AnimItem>();

        private GUIStyle windowStyle;
        private GUIStyle headerStyle;
        private GUIStyle categoryTabStyle;
        private GUIStyle categoryTabActiveStyle;
        private GUIStyle buttonStyle;
        private GUIStyle buttonActiveStyle;
        private GUIStyle hudLabelStyle;
        private GUIStyle hudTitleStyle;
        private Vector2 scrollPos;
        private bool stylesInitialized = false;

        private void Awake()
        {
            if (mechAnimator == null)
            {
                var mech = GameObject.Find("HammerMech_Boss");
                if (mech != null) mechAnimator = mech.GetComponent<Animator>();
                if (mechAnimator == null) mechAnimator = FindAnyObjectByType<Animator>();
            }
            if (mechAnimator != null)
            {
                mechAnimator.enabled = true;
                mechAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            if (showcaseCamera == null) showcaseCamera = Camera.main;
            if (cameraOrbitTarget == null)
            {
                if (mechAnimator != null) cameraOrbitTarget = mechAnimator.transform;
                else cameraOrbitTarget = transform;
            }
            if (hammerWeapon == null && mechAnimator != null)
            {
                var allT = mechAnimator.GetComponentsInChildren<Transform>(true);
                foreach (var t in allT)
                {
                    if (t.name == "SM_Wep_Hammer_01") { hammerWeapon = t; break; }
                }
            }

            SetupCategories();
        }

        private void Start()
        {
            if (mechAnimator != null)
            {
                mechAnimator.enabled = true;
                mechAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                mechAnimator.applyRootMotion = applyRootMotion;
                PlayAnimation("Idle");
            }
            UpdateCameraTransform();
        }

        private void SetupCategories()
        {
            categories.Clear();
            allClipsFlat.Clear();

            // 1. Attacks & Combos
            var catAttacks = new AnimCategory("Attacks (8)");
            catAttacks.items.Add(new AnimItem("Attack 1", "Attack1", "Primary hammer overhead swing"));
            catAttacks.items.Add(new AnimItem("Attack 1 (Stationary)", "Attack1_S", "In-place primary overhead swing"));
            catAttacks.items.Add(new AnimItem("Attack 2", "Attack2", "Secondary horizontal hammer cleave"));
            catAttacks.items.Add(new AnimItem("Attack 2 (Stationary)", "Attack2_S", "In-place secondary horizontal cleave"));
            catAttacks.items.Add(new AnimItem("Attack 2 (Solo Cleave)", "Attack2_solo", "Independent horizontal sweep strike"));
            catAttacks.items.Add(new AnimItem("Attack 3", "Attack3", "Finisher heavy ground pulverizer slam"));
            catAttacks.items.Add(new AnimItem("Attack 3 (Stationary)", "Attack3_S", "In-place heavy ground pulverizer slam"));
            catAttacks.items.Add(new AnimItem("Attack 3 (Solo Slam)", "Attack3_solo", "Independent heavy earth-shatter smash"));
            categories.Add(catAttacks);

            // 2. Special Moves
            var catSpecials = new AnimCategory("Special Moves (5)");
            catSpecials.items.Add(new AnimItem("Special Attack 1 (Cyclone)", "SpecialAttack1", "Full 360° centrifugal hammer whirlwind"));
            catSpecials.items.Add(new AnimItem("Move Attack 1 (Slide)", "MoveAttack1", "Forward dash kinetic hammer strike"));
            catSpecials.items.Add(new AnimItem("Move Attack 1 (Stationary)", "MoveAttack1_S", "In-place kinetic hammer thrust strike"));
            catSpecials.items.Add(new AnimItem("Jump Attack 1 (Ground Slam)", "Jump-Attack1", "Mid-air leap down-smash shockwave"));
            catSpecials.items.Add(new AnimItem("Range Attack 1 (Quake)", "RangeAttack1", "Kinetic ground fracture shockwave surge"));
            categories.Add(catSpecials);

            // 3. Locomotion
            var catLoco = new AnimCategory("Locomotion (11)");
            catLoco.items.Add(new AnimItem("Idle (Combat)", "Idle", "Ready stance holding massive war hammer"));
            catLoco.items.Add(new AnimItem("Walk Forward", "WalkForward", "Steady forward tactical mech stride"));
            catLoco.items.Add(new AnimItem("Walk Backward", "WalkBackward", "Reverse tactical repositioning"));
            catLoco.items.Add(new AnimItem("Walk Left", "WalkLeft", "Left lateral combat strafe"));
            catLoco.items.Add(new AnimItem("Walk Right", "WalkRight", "Right lateral combat strafe"));
            catLoco.items.Add(new AnimItem("Walk Forward-Left", "WalkForward-Left", "Diagonal forward-left flank"));
            catLoco.items.Add(new AnimItem("Walk Forward-Right", "WalkForward-Right", "Diagonal forward-right flank"));
            catLoco.items.Add(new AnimItem("Walk Backward-Left", "WalkBackward-Left", "Diagonal backward-left retreat"));
            catLoco.items.Add(new AnimItem("Walk Backward-Right", "WalkBackward-Right", "Diagonal backward-right retreat"));
            catLoco.items.Add(new AnimItem("Walk Slow", "WalkSlow", "Heavy armored patrol pace"));
            catLoco.items.Add(new AnimItem("Run", "Run", "Heavy sprint charging into melee"));
            categories.Add(catLoco);

            // 4. Dashes & Evasions
            var catDashes = new AnimCategory("Dashes & Evasions (8)");
            catDashes.items.Add(new AnimItem("Dash Forward", "DashForward", "Hydraulic burst forward leap"));
            catDashes.items.Add(new AnimItem("Dash Forward (In-Place)", "DashForward_S", "Stationary forward boost burst"));
            catDashes.items.Add(new AnimItem("Dash Backward", "DashBackward", "Reverse thruster quick backstep"));
            catDashes.items.Add(new AnimItem("Dash Backward (In-Place)", "DashBackward_S", "Stationary reverse quick backstep"));
            catDashes.items.Add(new AnimItem("Dash Left", "DashLeft", "Rapid left evasive strafe dash"));
            catDashes.items.Add(new AnimItem("Dash Left (In-Place)", "DashLeft_S", "Stationary left evasive strafe"));
            catDashes.items.Add(new AnimItem("Dash Right", "DashRight", "Rapid right evasive strafe dash"));
            catDashes.items.Add(new AnimItem("Dash Right (In-Place)", "DashRight_S", "Stationary right evasive strafe"));
            categories.Add(catDashes);

            // 5. Defense & Reactions
            var catDef = new AnimCategory("Defense & Hits (6)");
            catDef.items.Add(new AnimItem("Block", "Block", "Defensive hammer shaft guard stance"));
            catDef.items.Add(new AnimItem("Block Hit React", "BlockHitReact", "Impact absorption through hammer shaft"));
            catDef.items.Add(new AnimItem("Block Break", "BlockBreak", "Guard stagger upon shield puncture"));
            catDef.items.Add(new AnimItem("Block Break (In-Place)", "BlockBreak_S", "In-place guard shatter stagger"));
            catDef.items.Add(new AnimItem("Light Hit (Stagger)", "LightHit", "Kinetic impact flinch and recoil"));
            catDef.items.Add(new AnimItem("Stunned", "Stunned", "Hydraulic lockup / system overload stun"));
            categories.Add(catDef);

            // 6. Stances & Life
            var catStances = new AnimCategory("Stances & States (11)");
            catStances.items.Add(new AnimItem("Weapon Sheath", "WeaponSheath", "Mount war hammer onto back magnetic dock"));
            catStances.items.Add(new AnimItem("Weapon Unsheath", "WeaponUnsheath", "Draw war hammer into ready grip"));
            catStances.items.Add(new AnimItem("Idle (Sheathed)", "Idle-Sheathed", "Relaxed resting stance with hammer docked"));
            catStances.items.Add(new AnimItem("Walk (Sheathed)", "Walk-Sheathed", "Walking with hammer magnetically docked"));
            catStances.items.Add(new AnimItem("Run (Sheathed)", "Run-Sheathed", "Sprinting with hammer docked on back"));
            catStances.items.Add(new AnimItem("Walk Slow (Sheathed)", "WalkSlow-Sheathed", "Armored stroll with docked hammer"));
            catStances.items.Add(new AnimItem("Jump", "Jump", "Hydraulic vertical leap takeoff"));
            catStances.items.Add(new AnimItem("Fall", "Fall", "Mid-air descent pose"));
            catStances.items.Add(new AnimItem("Death", "Death", "Catastrophic reactor failure collapse"));
            catStances.items.Add(new AnimItem("Revive", "Revive", "Emergency reboot & standing recover"));
            categories.Add(catStances);

            foreach (var cat in categories)
            {
                allClipsFlat.AddRange(cat.items);
            }
        }

        private void Update()
        {
            if (aiCombatMode)
            {
                HandleDummyMovement();
            }
            else
            {
                HandleInput();
                HandleAnimationLooping();
            }
            HandleTurntable();
            UpdateCameraTransform();
        }

        public void ToggleAICombatMode()
        {
            aiCombatMode = !aiCombatMode;
            if (activeAI == null && mechAnimator != null)
            {
                activeAI = mechAnimator.GetComponent<HammerMechAI>();
                if (activeAI == null) activeAI = mechAnimator.gameObject.AddComponent<HammerMechAI>();
            }

            if (aiCombatMode)
            {
                if (activeAI != null) activeAI.enabled = true;
                EnsureCombatDummy();
            }
            else
            {
                if (activeAI != null) activeAI.enabled = false;
                if (combatDummy != null) combatDummy.SetActive(false);
                PlayAnimation("Idle");
            }
        }

        private void EnsureCombatDummy()
        {
            if (combatDummy == null)
            {
                combatDummy = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                combatDummy.name = "Combat_Target_Dummy";
                combatDummy.tag = "Player";
                combatDummy.transform.position = transform.position + transform.forward * 6.5f + Vector3.up * 1f;
                combatDummy.transform.localScale = new Vector3(0.8f, 1.2f, 0.8f);

                var ren = combatDummy.GetComponent<Renderer>();
                if (ren != null)
                {
                    ren.material.color = new Color(0f, 0.95f, 1f, 0.8f);
                }

                if (combatDummy.GetComponent<CombatDummyDamageReceiver>() == null)
                {
                    combatDummy.AddComponent<CombatDummyDamageReceiver>();
                }
            }
            combatDummy.SetActive(true);
        }

        private void HandleDummyMovement()
        {
            if (!aiCombatMode || combatDummy == null) return;

            Vector3 move = Vector3.zero;
            if (Input.GetKey(KeyCode.I) || Input.GetKey(KeyCode.UpArrow)) move += Vector3.forward;
            if (Input.GetKey(KeyCode.K) || Input.GetKey(KeyCode.DownArrow)) move += Vector3.back;
            if (Input.GetKey(KeyCode.J) || Input.GetKey(KeyCode.LeftArrow)) move += Vector3.left;
            if (Input.GetKey(KeyCode.L) || Input.GetKey(KeyCode.RightArrow)) move += Vector3.right;

            if (move.sqrMagnitude > 0.01f)
            {
                combatDummy.transform.position += move.normalized * (5.5f * Time.deltaTime);
            }
        }

        private void HandleInput()
        {
            // Keyboard shortcuts
            if (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.LeftArrow))
            {
                PreviousClip();
            }
            else if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                NextClip();
            }

            // Quick Hotkeys
            if (Input.GetKeyDown(KeyCode.Alpha1)) PlayAnimation("Attack1");
            if (Input.GetKeyDown(KeyCode.Alpha2)) PlayAnimation("Attack2");
            if (Input.GetKeyDown(KeyCode.Alpha3)) PlayAnimation("Attack3");
            if (Input.GetKeyDown(KeyCode.Alpha4)) PlayAnimation("SpecialAttack1");
            if (Input.GetKeyDown(KeyCode.Alpha5)) PlayAnimation("MoveAttack1");
            if (Input.GetKeyDown(KeyCode.Alpha6)) PlayAnimation("RangeAttack1");
            if (Input.GetKeyDown(KeyCode.Alpha7)) PlayAnimation("Jump-Attack1");
            if (Input.GetKeyDown(KeyCode.Alpha8)) PlayAnimation("Block");
            if (Input.GetKeyDown(KeyCode.Alpha9)) PlayAnimation("Stunned");
            if (Input.GetKeyDown(KeyCode.Alpha0)) PlayAnimation("Death");

            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (currentPlayingClipName == "Attack1") PlayAnimation("Attack2");
                else if (currentPlayingClipName == "Attack2") PlayAnimation("Attack3");
                else PlayAnimation("Attack1");
            }

            // Mouse orbit
            if (Input.GetMouseButtonDown(1))
            {
                isDraggingMouse = true;
                lastMousePos = Input.mousePosition;
            }
            else if (Input.GetMouseButtonUp(1))
            {
                isDraggingMouse = false;
            }

            if (isDraggingMouse)
            {
                Vector3 delta = Input.mousePosition - lastMousePos;
                orbitAngleYaw += delta.x * orbitSpeed * 0.1f;
                orbitAnglePitch -= delta.y * orbitSpeed * 0.1f;
                orbitAnglePitch = Mathf.Clamp(orbitAnglePitch, -10f, 75f);
                lastMousePos = Input.mousePosition;
            }

            // Mouse zoom
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                cameraDistance -= scroll * zoomSpeed;
                cameraDistance = Mathf.Clamp(cameraDistance, 2.0f, 12.0f);
            }
        }

        private void HandleTurntable()
        {
            if (autoTurntable && !isDraggingMouse)
            {
                orbitAngleYaw += turntableSpeed * Time.deltaTime;
                if (orbitAngleYaw > 360f) orbitAngleYaw -= 360f;
            }
        }

        private void UpdateCameraTransform()
        {
            if (showcaseCamera == null || cameraOrbitTarget == null) return;

            Vector3 shakeOffset = Vector3.zero;
            if (enableScreenShake && shakeIntensity > 0.001f)
            {
                shakeOffset = UnityEngine.Random.insideUnitSphere * shakeIntensity;
                shakeIntensity = Mathf.MoveTowards(shakeIntensity, 0f, Time.deltaTime * 3.5f);
            }

            Quaternion rot = Quaternion.Euler(orbitAnglePitch, orbitAngleYaw, 0f);
            Vector3 focusPoint = cameraOrbitTarget.position + Vector3.up * cameraHeight;
            Vector3 camPos = focusPoint - (rot * Vector3.forward * cameraDistance) + shakeOffset;

            showcaseCamera.transform.position = camPos;
            showcaseCamera.transform.LookAt(focusPoint + shakeOffset * 0.3f);
        }

        private void HandleAnimationLooping()
        {
            if (mechAnimator == null) return;

            AnimatorStateInfo info = mechAnimator.GetCurrentAnimatorStateInfo(0);
            if (info.IsName(currentPlayingClipName))
            {
                CheckImpactTrigger(info);

                if (info.normalizedTime >= 0.98f)
                {
                    if (loopCurrentAnimation)
                    {
                        hasTriggeredImpact = false;
                        mechAnimator.Play(currentPlayingClipName, 0, 0f);
                    }
                    else if (autoReturnToIdle && currentPlayingClipName != "Idle" && !currentPlayingClipName.Contains("Sheathed") && currentPlayingClipName != "Death")
                    {
                        PlayAnimation("Idle");
                    }
                }
            }
        }

        private void CheckImpactTrigger(AnimatorStateInfo info)
        {
            if (hasTriggeredImpact) return;

            bool isSlam = currentPlayingClipName == "Attack1" || currentPlayingClipName == "Attack1_S" ||
                          currentPlayingClipName == "Attack3" || currentPlayingClipName == "Attack3_S" ||
                          currentPlayingClipName == "Attack3_solo" || currentPlayingClipName == "Jump-Attack1" ||
                          currentPlayingClipName == "RangeAttack1" || currentPlayingClipName == "SpecialAttack1";

            float impactNormTime = currentPlayingClipName.Contains("Attack1") ? 0.52f : 0.62f;

            if (isSlam && info.normalizedTime >= impactNormTime && info.normalizedTime < 0.9f)
            {
                hasTriggeredImpact = true;
                if (enableScreenShake) shakeIntensity = 0.18f;

                if (impactVFXPrefab != null)
                {
                    Vector3 impactPos = hammerWeapon != null ? hammerWeapon.position : (cameraOrbitTarget != null ? cameraOrbitTarget.position + cameraOrbitTarget.forward * 1.5f : Vector3.zero);
                    impactPos.y = 0.05f;
                    var fx = Instantiate(impactVFXPrefab, impactPos, Quaternion.identity);
                    Destroy(fx, 3f);
                }
            }
        }

        public void PlayAnimation(string stateName)
        {
            if (mechAnimator == null) return;

            currentPlayingClipName = stateName;
            hasTriggeredImpact = false;
            mechAnimator.speed = playbackSpeed;

            var curr = mechAnimator.GetCurrentAnimatorStateInfo(0);
            if (curr.IsName(stateName))
            {
                mechAnimator.Play(stateName, 0, 0f);
            }
            else
            {
                mechAnimator.CrossFadeInFixedTime(stateName, 0.12f, 0, 0f);
            }

            var booster = GetComponentInChildren<HammerRocketBooster>(true);
            if (booster != null && (stateName.Contains("Attack") || stateName.Contains("Dash") || stateName.Contains("Jump")))
            {
                booster.TriggerBurst(1.2f, stateName.Contains("Jump") || stateName.Contains("Attack3"));
            }

            // Find flat index
            for (int i = 0; i < allClipsFlat.Count; i++)
            {
                if (allClipsFlat[i].stateName == stateName)
                {
                    currentAnimIndex = i;
                    break;
                }
            }
        }

        public void NextClip()
        {
            currentAnimIndex = (currentAnimIndex + 1) % allClipsFlat.Count;
            PlayAnimation(allClipsFlat[currentAnimIndex].stateName);
        }

        public void PreviousClip()
        {
            currentAnimIndex = (currentAnimIndex - 1 + allClipsFlat.Count) % allClipsFlat.Count;
            PlayAnimation(allClipsFlat[currentAnimIndex].stateName);
        }

        public void SetSpeed(float speed)
        {
            playbackSpeed = speed;
            if (mechAnimator != null) mechAnimator.speed = playbackSpeed;
        }

        public void SetCameraPreset(int preset)
        {
            switch (preset)
            {
                case 0: // Front Hero
                    orbitAngleYaw = 0f;
                    orbitAnglePitch = 10f;
                    cameraDistance = 5.0f;
                    cameraHeight = 1.8f;
                    break;
                case 1: // 3/4 Action Angle
                    orbitAngleYaw = 35f;
                    orbitAnglePitch = 15f;
                    cameraDistance = 5.5f;
                    cameraHeight = 1.8f;
                    break;
                case 2: // Side Profile
                    orbitAngleYaw = 90f;
                    orbitAnglePitch = 5f;
                    cameraDistance = 5.5f;
                    cameraHeight = 1.6f;
                    break;
                case 3: // Close Up
                    orbitAngleYaw = 25f;
                    orbitAnglePitch = 8f;
                    cameraDistance = 3.2f;
                    cameraHeight = 2.0f;
                    break;
            }
        }

        private void InitStyles()
        {
            if (stylesInitialized) return;

            windowStyle = new GUIStyle(GUI.skin.window);
            windowStyle.normal.background = MakeTex(2, 2, new Color(0.04f, 0.08f, 0.14f, 0.92f));
            windowStyle.border = new RectOffset(4, 4, 4, 4);

            headerStyle = new GUIStyle(GUI.skin.label);
            headerStyle.fontSize = 14;
            headerStyle.fontStyle = FontStyle.Bold;
            headerStyle.normal.textColor = new Color(0f, 0.95f, 1f); // Neon Cyan

            categoryTabStyle = new GUIStyle(GUI.skin.button);
            categoryTabStyle.fontSize = 11;
            categoryTabStyle.normal.textColor = new Color(0.7f, 0.8f, 0.9f);
            categoryTabStyle.normal.background = MakeTex(2, 2, new Color(0.1f, 0.15f, 0.22f, 0.85f));

            categoryTabActiveStyle = new GUIStyle(categoryTabStyle);
            categoryTabActiveStyle.normal.textColor = Color.white;
            categoryTabActiveStyle.normal.background = MakeTex(2, 2, new Color(0.1f, 0.45f, 0.7f, 0.95f));

            buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fontSize = 12;
            buttonStyle.alignment = TextAnchor.MiddleLeft;
            buttonStyle.normal.textColor = Color.white;
            buttonStyle.normal.background = MakeTex(2, 2, new Color(0.08f, 0.18f, 0.28f, 0.85f));

            buttonActiveStyle = new GUIStyle(buttonStyle);
            buttonActiveStyle.normal.textColor = new Color(1f, 0.85f, 0.2f); // Gold highlight
            buttonActiveStyle.normal.background = MakeTex(2, 2, new Color(0.18f, 0.5f, 0.75f, 1f));

            hudTitleStyle = new GUIStyle(GUI.skin.label);
            hudTitleStyle.fontSize = 18;
            hudTitleStyle.fontStyle = FontStyle.Bold;
            hudTitleStyle.normal.textColor = new Color(0f, 0.95f, 1f);

            hudLabelStyle = new GUIStyle(GUI.skin.label);
            hudLabelStyle.fontSize = 12;
            hudLabelStyle.normal.textColor = new Color(0.85f, 0.92f, 1f);

            stylesInitialized = true;
        }

        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        private void OnGUI()
        {
            InitStyles();

            // 1. Top HUD Header
            GUI.Box(new Rect(15, 12, Screen.width - 30, 48), GUIContent.none, windowStyle);
            GUI.Label(new Rect(30, 16, 450, 24), "TITAN HAMMER MECH // ANIMATION SHOWCASE", hudTitleStyle);
            GUI.Label(new Rect(30, 38, 550, 18), $"Current: [ {currentPlayingClipName} ]  |  Rig: PolygonMech Humanoid  |  Clips: 49 Total", hudLabelStyle);

            // Controls on Top Header
            float aiBtnX = Screen.width - 685;
            if (GUI.Button(new Rect(aiBtnX, 20, 115, 28), aiCombatMode ? "AI Mode: ON" : "AI Mode: OFF", aiCombatMode ? buttonActiveStyle : buttonStyle))
            {
                ToggleAICombatMode();
            }

            float topBtnX = Screen.width - 560;
            if (GUI.Button(new Rect(topBtnX, 20, 55, 28), "0.25x", playbackSpeed == 0.25f ? buttonActiveStyle : buttonStyle)) SetSpeed(0.25f);
            if (GUI.Button(new Rect(topBtnX + 60, 20, 55, 28), "0.5x", playbackSpeed == 0.5f ? buttonActiveStyle : buttonStyle)) SetSpeed(0.5f);
            if (GUI.Button(new Rect(topBtnX + 120, 20, 55, 28), "1.0x", playbackSpeed == 1.0f ? buttonActiveStyle : buttonStyle)) SetSpeed(1.0f);
            if (GUI.Button(new Rect(topBtnX + 180, 20, 75, 28), "Turntable", autoTurntable ? buttonActiveStyle : buttonStyle)) autoTurntable = !autoTurntable;
            if (GUI.Button(new Rect(topBtnX + 260, 20, 65, 28), "Loop", loopCurrentAnimation ? buttonActiveStyle : buttonStyle)) loopCurrentAnimation = !loopCurrentAnimation;
            if (GUI.Button(new Rect(topBtnX + 330, 20, 85, 28), "Auto-Idle", autoReturnToIdle ? buttonActiveStyle : buttonStyle)) autoReturnToIdle = !autoReturnToIdle;
            if (GUI.Button(new Rect(topBtnX + 420, 20, 65, 28), "< Prev", buttonStyle)) PreviousClip();
            if (GUI.Button(new Rect(topBtnX + 490, 20, 65, 28), "Next >", buttonStyle)) NextClip();

            // If in AI Mode, display Tactical Telemetry HUD
            if (aiCombatMode && activeAI != null)
            {
                float aiPanelWidth = 350;
                float aiPanelHeight = 240;
                Rect aiRect = new Rect(15, 68, aiPanelWidth, aiPanelHeight);
                GUI.Box(aiRect, GUIContent.none, windowStyle);

                GUI.Label(new Rect(aiRect.x + 15, aiRect.y + 10, aiPanelWidth - 30, 22), "TACTICAL AI TELEMETRY", headerStyle);
                GUI.Label(new Rect(aiRect.x + 15, aiRect.y + 36, aiPanelWidth - 30, 20), $"State: <color=#00ffff>[ {activeAI.currentState} ]</color>", hudLabelStyle);
                GUI.Label(new Rect(aiRect.x + 15, aiRect.y + 60, aiPanelWidth - 30, 20), $"Chassis Integrity: {activeAI.currentHealth:F0} / {activeAI.maxHealth:F0}", hudLabelStyle);
                GUI.Label(new Rect(aiRect.x + 15, aiRect.y + 84, aiPanelWidth - 30, 20), $"Super Armor Poise: {activeAI.currentPoise:F0} / {activeAI.maxPoise:F0}", hudLabelStyle);

                GUI.Label(new Rect(aiRect.x + 15, aiRect.y + 115, aiPanelWidth - 30, 20), "TARGET DUMMY STEERING:", headerStyle);
                GUI.Label(new Rect(aiRect.x + 15, aiRect.y + 138, aiPanelWidth - 30, 36), "• I / K or Up/Down: Forward / Back\n• J / L or Left/Right: Strafe / Flank", hudLabelStyle);
                GUI.Label(new Rect(aiRect.x + 15, aiRect.y + 180, aiPanelWidth - 30, 48), "<size=10><color=#ffaa00>Flank behind the mech -> Triggers 360° Cyclone!\nMove far away -> Triggers Seismic Quake / Leap!\nClose in -> Triggers Combos & Hammer Guard!</color></size>", hudLabelStyle);
                return;
            }

            // 2. Left Animation Browser Window
            float panelWidth = 330;
            float panelHeight = Screen.height - 80;
            GUI.Box(new Rect(15, 68, panelWidth, panelHeight), GUIContent.none, windowStyle);

            if (categories == null || categories.Count == 0)
            {
                SetupCategories();
                if (categories == null || categories.Count == 0) return;
            }
            currentCategoryIndex = Mathf.Clamp(currentCategoryIndex, 0, categories.Count - 1);

            // Category Tab Buttons
            float tabWidth = (panelWidth - 20) / 3f;
            for (int i = 0; i < categories.Count; i++)
            {
                int row = i / 3;
                int col = i % 3;
                Rect tabRect = new Rect(25 + col * tabWidth, 78 + row * 26, tabWidth - 3, 24);
                string tabName = categories[i].name.Split(' ')[0];
                if (GUI.Button(tabRect, tabName, i == currentCategoryIndex ? categoryTabActiveStyle : categoryTabStyle))
                {
                    currentCategoryIndex = i;
                }
            }

            // Animation items list for active category
            var activeCategory = categories[currentCategoryIndex];
            GUI.Label(new Rect(25, 136, panelWidth - 20, 20), $"--- {activeCategory.name.ToUpper()} ---", headerStyle);

            float scrollAreaY = 160;
            float scrollAreaHeight = panelHeight - 145;
            Rect viewRect = new Rect(0, 0, panelWidth - 45, activeCategory.items.Count * 42);

            scrollPos = GUI.BeginScrollView(new Rect(20, scrollAreaY, panelWidth - 15, scrollAreaHeight), scrollPos, viewRect);
            for (int i = 0; i < activeCategory.items.Count; i++)
            {
                var item = activeCategory.items[i];
                bool isSelected = item.stateName == currentPlayingClipName;
                Rect btnRect = new Rect(5, i * 42, panelWidth - 55, 38);

                string btnText = $"{item.displayName}\n<size=9><color=#88ccff>{item.description}</color></size>";
                if (GUI.Button(btnRect, btnText, isSelected ? buttonActiveStyle : buttonStyle))
                {
                    PlayAnimation(item.stateName);
                }
            }
            GUI.EndScrollView();

            // 3. Bottom Right Camera Presets & Instructions
            float bottomBoxWidth = 320;
            float bottomBoxHeight = 110;
            Rect bottomBox = new Rect(Screen.width - bottomBoxWidth - 15, Screen.height - bottomBoxHeight - 15, bottomBoxWidth, bottomBoxHeight);
            GUI.Box(bottomBox, GUIContent.none, windowStyle);

            GUI.Label(new Rect(bottomBox.x + 10, bottomBox.y + 6, bottomBoxWidth - 20, 20), "CAMERA PRESETS & CONTROLS", headerStyle);
            if (GUI.Button(new Rect(bottomBox.x + 10, bottomBox.y + 28, 70, 24), "Front", buttonStyle)) SetCameraPreset(0);
            if (GUI.Button(new Rect(bottomBox.x + 85, bottomBox.y + 28, 70, 24), "Hero 3/4", buttonStyle)) SetCameraPreset(1);
            if (GUI.Button(new Rect(bottomBox.x + 160, bottomBox.y + 28, 70, 24), "Side", buttonStyle)) SetCameraPreset(2);
            if (GUI.Button(new Rect(bottomBox.x + 235, bottomBox.y + 28, 70, 24), "Close-Up", buttonStyle)) SetCameraPreset(3);

            GUI.Label(new Rect(bottomBox.x + 10, bottomBox.y + 58, bottomBoxWidth - 20, 48),
                "Toggle AI Mode with button above.\nSteer dummy with I/J/K/L or Arrow Keys.\nRight-Click + Drag to orbit camera.", hudLabelStyle);
        }
    }

    public class CombatDummyDamageReceiver : MonoBehaviour, ISyntyDamageable
    {
        public float health = 1500f;

        public void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitDirection, bool isHeavy)
        {
            health = Mathf.Max(0f, health - damage);
            StartCoroutine(FlashDamage());
        }

        private IEnumerator FlashDamage()
        {
            var r = GetComponent<Renderer>();
            if (r != null)
            {
                Color prev = r.material.color;
                r.material.color = Color.red;
                yield return new WaitForSeconds(0.12f);
                if (r != null) r.material.color = prev;
            }
        }
    }
}
