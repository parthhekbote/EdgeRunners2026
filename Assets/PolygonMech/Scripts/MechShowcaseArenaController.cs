using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PolygonMech
{
    /// <summary>
    /// Interactive Showcase & Animation Test Controller for Demo.unity.
    /// Lets the player inspect, trigger, and battle the Sword Mech and Hammer Mech in real time.
    /// </summary>
    public class MechShowcaseArenaController : MonoBehaviour
    {
        public enum CameraViewMode
        {
            DuelOverview,
            FocusSwordMech,
            FocusHammerMech,
            FreeOrbit
        }

        [Header("Mech References")]
        public GameObject swordMechObject;
        public GameObject hammerMechObject;

        [Header("Camera & Controls")]
        public Camera mainCamera;
        public CameraViewMode currentView = CameraViewMode.DuelOverview;
        public float cameraDistance = 12f;
        public float cameraHeight = 3.5f;
        public float orbitSpeed = 25f;
        public float smoothSpeed = 6f;

        [Header("State & Simulation")]
        public bool isAIDuelActive = false;
        public float currentTimeScale = 1.0f;

        // Internals
        private Animator swordAnim;
        private Animator hammerAnim;
        private BossMechAI swordAI;
        private BossMechAI hammerAI;

        private float currentYaw = 0f;
        private float currentPitch = 15f;
        private Vector3 currentCamLookTarget;
        private Vector3 currentCamPos;

        // GUI
        private GUIStyle panelStyle;
        private GUIStyle headerStyle;
        private GUIStyle subHeaderStyle;
        private GUIStyle buttonStyle;
        private GUIStyle activeBtnStyle;
        private GUIStyle labelStyle;
        private bool stylesInitialized = false;

        private void Start()
        {
            if (mainCamera == null) mainCamera = Camera.main;

            if (swordMechObject != null)
            {
                swordAnim = swordMechObject.GetComponentInChildren<Animator>();
                swordAI = swordMechObject.GetComponent<BossMechAI>();
            }

            if (hammerMechObject != null)
            {
                hammerAnim = hammerMechObject.GetComponentInChildren<Animator>();
                hammerAI = hammerMechObject.GetComponent<BossMechAI>();
            }

            SetTimeScale(1.0f);
            SetViewMode(CameraViewMode.DuelOverview);
        }

        private void Update()
        {
            HandleInput();
            UpdateCamera();
        }

        private void HandleInput()
        {
            // Right-click drag to orbit
            if (Input.GetMouseButton(1))
            {
                currentYaw += Input.GetAxis("Mouse X") * 3.5f;
                currentPitch -= Input.GetAxis("Mouse Y") * 2.5f;
                currentPitch = Mathf.Clamp(currentPitch, -10f, 75f);
                currentView = CameraViewMode.FreeOrbit;
            }

            // Scroll wheel for zoom
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                cameraDistance = Mathf.Clamp(cameraDistance - scroll * 5f, 4f, 35f);
            }

            // Quick Hotkeys
            if (Input.GetKeyDown(KeyCode.Alpha1)) TriggerSwordAttack("MeleeSwipe");
            if (Input.GetKeyDown(KeyCode.Alpha2)) TriggerSwordAttack("SpinAttack");
            if (Input.GetKeyDown(KeyCode.Alpha3)) TriggerSwordAttack("GroundSlam");
            if (Input.GetKeyDown(KeyCode.Alpha4)) TriggerHammerAttack("HammerSweep");
            if (Input.GetKeyDown(KeyCode.Alpha5)) TriggerHammerAttack("WhirlwindSpin");
            if (Input.GetKeyDown(KeyCode.Alpha6)) TriggerHammerAttack("GroundSlam");
            if (Input.GetKeyDown(KeyCode.Space)) ToggleAIDuel();
        }

        private void UpdateCamera()
        {
            if (mainCamera == null) return;

            Vector3 targetCenter = Vector3.zero;

            switch (currentView)
            {
                case CameraViewMode.FocusSwordMech:
                    if (swordMechObject != null) targetCenter = swordMechObject.transform.position + Vector3.up * 2.5f;
                    break;
                case CameraViewMode.FocusHammerMech:
                    if (hammerMechObject != null) targetCenter = hammerMechObject.transform.position + Vector3.up * 2.5f;
                    break;
                case CameraViewMode.DuelOverview:
                    if (swordMechObject != null && hammerMechObject != null)
                    {
                        targetCenter = (swordMechObject.transform.position + hammerMechObject.transform.position) * 0.5f + Vector3.up * 2.2f;
                    }
                    else if (swordMechObject != null) targetCenter = swordMechObject.transform.position + Vector3.up * 2.2f;
                    break;
                case CameraViewMode.FreeOrbit:
                    // Keep looking at current target
                    targetCenter = currentCamLookTarget;
                    break;
            }

            currentCamLookTarget = Vector3.Lerp(currentCamLookTarget, targetCenter, Time.unscaledDeltaTime * smoothSpeed);

            // Compute orbit position
            Quaternion rot = Quaternion.Euler(currentPitch, currentYaw, 0f);
            Vector3 desiredPos = currentCamLookTarget + rot * new Vector3(0f, 0f, -cameraDistance) + Vector3.up * (cameraHeight * 0.3f);

            mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, desiredPos, Time.unscaledDeltaTime * smoothSpeed);
            mainCamera.transform.LookAt(currentCamLookTarget);
        }

        public void SetViewMode(CameraViewMode mode)
        {
            currentView = mode;
            switch (mode)
            {
                case CameraViewMode.FocusSwordMech:
                    currentYaw = 45f;
                    currentPitch = 18f;
                    cameraDistance = 8.5f;
                    break;
                case CameraViewMode.FocusHammerMech:
                    currentYaw = -45f;
                    currentPitch = 18f;
                    cameraDistance = 8.5f;
                    break;
                case CameraViewMode.DuelOverview:
                    currentYaw = 0f;
                    currentPitch = 14f;
                    cameraDistance = 14f;
                    break;
            }
        }

        public void TriggerSwordAttack(string trigger)
        {
            if (swordAnim != null)
            {
                swordAnim.SetTrigger(trigger);
            }
        }

        public void TriggerHammerAttack(string trigger)
        {
            if (hammerAnim != null)
            {
                hammerAnim.SetTrigger(trigger);
            }
        }

        public void ToggleAIDuel()
        {
            isAIDuelActive = !isAIDuelActive;
            if (swordAI != null) swordAI.enabled = isAIDuelActive;
            if (hammerAI != null) hammerAI.enabled = isAIDuelActive;
        }

        public void SetTimeScale(float scale)
        {
            currentTimeScale = scale;
            Time.timeScale = scale;
        }

        private void InitGUIStyles()
        {
            if (stylesInitialized) return;

            panelStyle = new GUIStyle(GUI.skin.box);
            panelStyle.normal.background = MakeTex(2, 2, new Color(0.05f, 0.08f, 0.12f, 0.88f));

            headerStyle = new GUIStyle(GUI.skin.label);
            headerStyle.fontSize = 14;
            headerStyle.fontStyle = FontStyle.Bold;
            headerStyle.normal.textColor = new Color(0.3f, 0.85f, 1.0f);

            subHeaderStyle = new GUIStyle(GUI.skin.label);
            subHeaderStyle.fontSize = 11;
            subHeaderStyle.fontStyle = FontStyle.Bold;
            subHeaderStyle.normal.textColor = new Color(1.0f, 0.75f, 0.2f);

            buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fontSize = 11;
            buttonStyle.fontStyle = FontStyle.Bold;
            buttonStyle.normal.textColor = Color.white;

            activeBtnStyle = new GUIStyle(buttonStyle);
            activeBtnStyle.normal.textColor = new Color(0.2f, 1.0f, 0.5f);

            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = 10;
            labelStyle.normal.textColor = new Color(0.7f, 0.8f, 0.9f);

            stylesInitialized = true;
        }

        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; ++i) pix[i] = col;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        private void OnGUI()
        {
            InitGUIStyles();

            // Top Header Banner
            GUILayout.BeginArea(new Rect(15, 15, 380, 750), panelStyle);
            GUILayout.Space(8);
            GUILayout.Label("CYBERPUNK MECH BOSS SHOWCASE", headerStyle);
            GUILayout.Label("Sword Titan vs Hammer Juggernaut Live Controller", labelStyle);
            GUILayout.Space(6);

            // Camera Views
            GUILayout.Label("CAMERA VIEW MODES", subHeaderStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Duel Overview", currentView == CameraViewMode.DuelOverview ? activeBtnStyle : buttonStyle)) SetViewMode(CameraViewMode.DuelOverview);
            if (GUILayout.Button("Sword Mech", currentView == CameraViewMode.FocusSwordMech ? activeBtnStyle : buttonStyle)) SetViewMode(CameraViewMode.FocusSwordMech);
            if (GUILayout.Button("Hammer Mech", currentView == CameraViewMode.FocusHammerMech ? activeBtnStyle : buttonStyle)) SetViewMode(CameraViewMode.FocusHammerMech);
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // SWORD MECH CONTROLS
            GUILayout.Label("SWORD MECH // TITAN BLADE (GOLIATH-02)", subHeaderStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Melee Slash [1]", buttonStyle)) TriggerSwordAttack("MeleeSwipe");
            if (GUILayout.Button("Overhead Cleave", buttonStyle)) TriggerSwordAttack("SlashOverhead");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Cyclone Spin [2]", buttonStyle)) TriggerSwordAttack("SpinAttack");
            if (GUILayout.Button("Piston Thrust", buttonStyle)) TriggerSwordAttack("PistonThrust");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Ground Slam [3]", buttonStyle)) TriggerSwordAttack("GroundSlam");
            if (GUILayout.Button("Hydraulic Kick", buttonStyle)) TriggerSwordAttack("HydraulicKick");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Blade Shield", buttonStyle)) TriggerSwordAttack("BladeShield");
            if (GUILayout.Button("Battle Roar", buttonStyle)) TriggerSwordAttack("Roar");
            if (GUILayout.Button("Stagger", buttonStyle)) TriggerSwordAttack("Stagger");
            GUILayout.EndHorizontal();

            GUILayout.Space(12);

            // HAMMER MECH CONTROLS
            GUILayout.Label("HAMMER MECH // WAR JUGGERNAUT (COLOSSUS-05)", subHeaderStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Hammer Sweep [4]", buttonStyle)) TriggerHammerAttack("HammerSweep");
            if (GUILayout.Button("Overhead Crush", buttonStyle)) TriggerHammerAttack("OverheadCrush");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Whirlwind Spin [5]", buttonStyle)) TriggerHammerAttack("WhirlwindSpin");
            if (GUILayout.Button("Piston Ram", buttonStyle)) TriggerHammerAttack("PistonThrust");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Colossal Slam [6]", buttonStyle)) TriggerHammerAttack("GroundSlam");
            if (GUILayout.Button("Seismic Stomp", buttonStyle)) TriggerHammerAttack("SeismicStomp");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Pneumatic Punch", buttonStyle)) TriggerHammerAttack("PneumaticPunch");
            if (GUILayout.Button("Exhaust Roar", buttonStyle)) TriggerHammerAttack("OverdriveRoar");
            if (GUILayout.Button("Stagger", buttonStyle)) TriggerHammerAttack("Stagger");
            GUILayout.EndHorizontal();

            GUILayout.Space(12);

            // SIMULATION & TIME SCALE
            GUILayout.Label("SIMULATION & PLAYBACK SPEED", subHeaderStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(isAIDuelActive ? "Stop AI Duel [Space]" : "Start AI Duel [Space]", isAIDuelActive ? activeBtnStyle : buttonStyle)) ToggleAIDuel();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("0.2x (Slow)", currentTimeScale == 0.2f ? activeBtnStyle : buttonStyle)) SetTimeScale(0.2f);
            if (GUILayout.Button("0.5x", currentTimeScale == 0.5f ? activeBtnStyle : buttonStyle)) SetTimeScale(0.5f);
            if (GUILayout.Button("1.0x (Normal)", currentTimeScale == 1.0f ? activeBtnStyle : buttonStyle)) SetTimeScale(1.0f);
            if (GUILayout.Button("1.5x (Fast)", currentTimeScale == 1.5f ? activeBtnStyle : buttonStyle)) SetTimeScale(1.5f);
            GUILayout.EndHorizontal();

            GUILayout.Space(8);
            GUILayout.Label("Right-Click Drag: Free Orbit Camera | Scroll: Zoom", labelStyle);

            GUILayout.EndArea();
        }
    }
}
