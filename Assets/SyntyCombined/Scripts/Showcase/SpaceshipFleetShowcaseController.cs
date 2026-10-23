using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SyntyCombined.Showcase
{
    [System.Serializable]
    public class SpaceshipEntry
    {
        public string displayName;
        public string shipClass;
        public string tacticalRole;
        public Vector3 dimensions;
        public GameObject shipInstance;
        public Vector3 lineupPosition;
        public Quaternion lineupRotation;
        public Vector3 armadaPosition;
        public Quaternion armadaRotation;
        public float cameraDistance = 25f;
        public Vector3 cameraOffset = new Vector3(0f, 4f, 0f);
        public TextMesh badgeTextMesh;
    }

    /// <summary>
    /// Interactive Controller for the Spaceship Fleet Showcase Scene.
    /// Provides smooth camera transitions, ship inspection orbit controls,
    /// grand overview mode, supersonic runway flyby mode, formation toggling,
    /// and a cyberpunk tactical telemetry HUD.
    /// </summary>
    public class SpaceshipFleetShowcaseController : MonoBehaviour
    {
        [Header("Fleet Registry")]
        public List<SpaceshipEntry> fleet = new List<SpaceshipEntry>();

        [Header("Camera Control")]
        public Camera showcaseCamera;
        public Transform cameraRig;
        public float cameraSmoothSpeed = 5.0f;
        public float orbitSpeed = 120.0f;
        public float zoomSpeed = 10.0f;

        [Header("Flyby Settings")]
        public float flybySpeed = 60.0f;
        public Vector3 flybyStartPos = new Vector3(-25f, 15f, -30f);
        public Vector3 flybyEndPos = new Vector3(-25f, 25f, 650f);

        [Header("Overview Camera")]
        public Vector3 overviewCamPos = new Vector3(120f, 95f, 220f);
        public Vector3 overviewCamTarget = new Vector3(0f, 10f, 260f);

        // State
        private int currentIndex = 0;
        private bool isOverview = false;
        private bool isFlyby = false;
        private bool isArmada = false;
        private float flybyT = 0f;

        // Orbit Angles
        private float currentYaw = 35.0f;
        private float currentPitch = 18.0f;
        private float currentDistance = 25.0f;
        private Vector2 lastMousePos;
        private bool isDragging = false;

        private GUIStyle headerStyle;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle badgeStyle;
        private GUIStyle btnStyle;
        private Texture2D panelTex;

        private void Start()
        {
            if (showcaseCamera == null)
            {
                showcaseCamera = Camera.main;
            }

            if (showcaseCamera != null && cameraRig == null)
            {
                GameObject rigGo = new GameObject("Showcase_CameraRig");
                cameraRig = rigGo.transform;
                showcaseCamera.transform.SetParent(cameraRig);
            }

            if (fleet.Count > 0)
            {
                FocusOnShip(0, true);
            }
        }

        private void Update()
        {
            HandleInput();

            if (isFlyby)
            {
                UpdateFlyby();
            }
            else if (isOverview)
            {
                UpdateOverview();
            }
            else
            {
                UpdateShipOrbit();
            }

            UpdateShipFormations();
        }

        private void HandleInput()
        {
            // Previous / Next Ship
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            {
                PrevShip();
            }
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            {
                NextShip();
            }

            // Overview Mode
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.O))
            {
                ToggleOverview();
            }

            // Flyby Mode
            if (Input.GetKeyDown(KeyCode.F))
            {
                ToggleFlyby();
            }

            // Formation Toggle
            if (Input.GetKeyDown(KeyCode.T))
            {
                ToggleFormation();
            }

            // Mouse Orbit Dragging
            if (!isFlyby && !isOverview)
            {
                if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
                {
                    isDragging = true;
                    lastMousePos = Input.mousePosition;
                }
                if (Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1))
                {
                    isDragging = false;
                }

                if (isDragging)
                {
                    Vector2 delta = (Vector2)Input.mousePosition - lastMousePos;
                    currentYaw += delta.x * (orbitSpeed * 0.05f) * Time.deltaTime;
                    currentPitch -= delta.y * (orbitSpeed * 0.05f) * Time.deltaTime;
                    currentPitch = Mathf.Clamp(currentPitch, -15f, 75f);
                    lastMousePos = Input.mousePosition;
                }

                // Scroll Wheel Zoom
                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    currentDistance -= scroll * zoomSpeed * 5f;
                    currentDistance = Mathf.Clamp(currentDistance, 8f, 150f);
                }
            }
        }

        public void FocusOnShip(int index, bool instant = false)
        {
            if (fleet.Count == 0) return;
            currentIndex = (index + fleet.Count) % fleet.Count;
            isOverview = false;
            isFlyby = false;

            var ship = fleet[currentIndex];
            currentDistance = ship.cameraDistance;
            currentYaw = 35.0f;
            currentPitch = 18.0f;

            if (instant && cameraRig != null)
            {
                Vector3 target = ship.shipInstance.transform.position + ship.cameraOffset;
                Quaternion rot = Quaternion.Euler(currentPitch, currentYaw, 0f);
                Vector3 desiredPos = target - (rot * Vector3.forward * currentDistance);
                cameraRig.position = desiredPos;
                cameraRig.rotation = rot;
            }
        }

        public void NextShip()
        {
            FocusOnShip(currentIndex + 1);
        }

        public void PrevShip()
        {
            FocusOnShip(currentIndex - 1);
        }

        public void ToggleOverview()
        {
            isOverview = !isOverview;
            isFlyby = false;
        }

        public void ToggleFlyby()
        {
            isFlyby = !isFlyby;
            isOverview = false;
            if (isFlyby)
            {
                flybyT = 0f;
            }
        }

        public void ToggleFormation()
        {
            isArmada = !isArmada;
        }

        private void UpdateShipOrbit()
        {
            if (fleet.Count == 0 || cameraRig == null) return;

            var ship = fleet[currentIndex];
            Vector3 target = ship.shipInstance.transform.position + ship.cameraOffset;
            Quaternion rot = Quaternion.Euler(currentPitch, currentYaw, 0f);
            Vector3 desiredPos = target - (rot * Vector3.forward * currentDistance);

            cameraRig.position = Vector3.Lerp(cameraRig.position, desiredPos, Time.deltaTime * cameraSmoothSpeed);
            cameraRig.rotation = Quaternion.Slerp(cameraRig.rotation, rot, Time.deltaTime * cameraSmoothSpeed);
        }

        private void UpdateOverview()
        {
            if (cameraRig == null) return;
            cameraRig.position = Vector3.Lerp(cameraRig.position, overviewCamPos, Time.deltaTime * 3.0f);
            Quaternion targetRot = Quaternion.LookRotation((overviewCamTarget - overviewCamPos).normalized, Vector3.up);
            cameraRig.rotation = Quaternion.Slerp(cameraRig.rotation, targetRot, Time.deltaTime * 3.0f);
        }

        private void UpdateFlyby()
        {
            if (cameraRig == null) return;
            float totalDist = Vector3.Distance(flybyStartPos, flybyEndPos);
            float duration = totalDist / flybySpeed;
            flybyT += Time.deltaTime / duration;

            if (flybyT > 1.0f)
            {
                flybyT = 0f;
            }

            Vector3 currentPos = Vector3.Lerp(flybyStartPos, flybyEndPos, flybyT);
            currentPos.y += Mathf.Sin(flybyT * Mathf.PI * 4f) * 3f; // subtle banking wave

            Vector3 lookAhead = Vector3.Lerp(flybyStartPos, flybyEndPos, Mathf.Clamp01(flybyT + 0.08f));
            lookAhead.x += 18f; // Look towards the ships on the runway

            cameraRig.position = Vector3.Lerp(cameraRig.position, currentPos, Time.deltaTime * 12f);
            Quaternion targetRot = Quaternion.LookRotation((lookAhead - currentPos).normalized, Vector3.up);
            cameraRig.rotation = Quaternion.Slerp(cameraRig.rotation, targetRot, Time.deltaTime * 8f);
        }

        private void UpdateShipFormations()
        {
            for (int i = 0; i < fleet.Count; i++)
            {
                var entry = fleet[i];
                if (entry.shipInstance == null) continue;

                Vector3 targetPos = isArmada ? entry.armadaPosition : entry.lineupPosition;
                Quaternion targetRot = isArmada ? entry.armadaRotation : entry.lineupRotation;

                entry.shipInstance.transform.position = Vector3.Lerp(entry.shipInstance.transform.position, targetPos, Time.deltaTime * 3.0f);
                entry.shipInstance.transform.rotation = Quaternion.Slerp(entry.shipInstance.transform.rotation, targetRot, Time.deltaTime * 3.0f);

                // Update 3D badge orientation towards camera
                if (entry.badgeTextMesh != null && showcaseCamera != null)
                {
                    entry.badgeTextMesh.transform.rotation = Quaternion.LookRotation(entry.badgeTextMesh.transform.position - showcaseCamera.transform.position);
                }
            }
        }

        private void InitStyles()
        {
            if (headerStyle != null) return;

            panelTex = new Texture2D(1, 1);
            panelTex.SetPixel(0, 0, new Color(0.04f, 0.08f, 0.16f, 0.88f));
            panelTex.Apply();

            headerStyle = new GUIStyle();
            headerStyle.fontSize = 18;
            headerStyle.fontStyle = FontStyle.Bold;
            headerStyle.normal.textColor = new Color(0.0f, 0.95f, 1.0f);

            titleStyle = new GUIStyle();
            titleStyle.fontSize = 22;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.normal.textColor = Color.white;

            bodyStyle = new GUIStyle();
            bodyStyle.fontSize = 13;
            bodyStyle.normal.textColor = new Color(0.8f, 0.88f, 0.95f);

            badgeStyle = new GUIStyle();
            badgeStyle.fontSize = 12;
            badgeStyle.fontStyle = FontStyle.Bold;
            badgeStyle.normal.textColor = new Color(1.0f, 0.75f, 0.15f);

            btnStyle = new GUIStyle(GUI.skin.button);
            btnStyle.fontSize = 13;
            btnStyle.fontStyle = FontStyle.Bold;
            btnStyle.normal.textColor = Color.white;
        }

        private void OnGUI()
        {
            InitStyles();

            // Top Header Banner
            GUI.DrawTexture(new Rect(20, 15, Screen.width - 40, 48), panelTex);
            GUI.Box(new Rect(20, 15, Screen.width - 40, 48), GUIContent.none);
            GUI.Label(new Rect(35, 20, 500, 24), "FLEET INSPECTION REVIEW // POLYGON SCI-FI SPACE", headerStyle);
            GUI.Label(new Rect(35, 42, 600, 20), string.Format("TOTAL SHIPS: {0}  |  ACTIVE FORMATION: {1}  |  MODE: {2}", fleet.Count, isArmada ? "BATTLEGROUP ARMADA" : "PARALLEL RUNWAY LINEUP", isFlyby ? "CINEMATIC FLYBY" : (isOverview ? "GRAND OVERVIEW" : "INSPECT SHIP")), bodyStyle);

            // Left Telemetry Box (Current Ship)
            if (fleet.Count > 0 && !isFlyby && !isOverview)
            {
                var ship = fleet[currentIndex];
                float panelW = 340;
                float panelH = 240;
                Rect telemetryRect = new Rect(20, 75, panelW, panelH);
                GUI.DrawTexture(telemetryRect, panelTex);
                GUI.Box(telemetryRect, GUIContent.none);

                GUILayout.BeginArea(new Rect(35, 85, panelW - 30, panelH - 20));
                GUILayout.Label(string.Format("[VESSEL {0:D2} / {1:D2}]", currentIndex + 1, fleet.Count), badgeStyle);
                GUILayout.Label(ship.displayName, titleStyle);
                GUILayout.Space(4);
                GUILayout.Label("CLASS:  " + ship.shipClass, bodyStyle);
                GUILayout.Label("ROLE:   " + ship.tacticalRole, bodyStyle);
                GUILayout.Label(string.Format("DIMENSIONS: {0:F1}m x {1:F1}m x {2:F1}m", ship.dimensions.x, ship.dimensions.y, ship.dimensions.z), bodyStyle);
                GUILayout.Label("DRIVE:  Hyper-Matter Plasma Thrusters", bodyStyle);
                GUILayout.Label("HULL:   Multi-Layer Composite Titanium Plating", bodyStyle);
                GUILayout.Space(8);
                GUILayout.Label("<color=#22D3EE>Drag Mouse to Orbit  •  Scroll to Zoom</color>", bodyStyle);
                GUILayout.EndArea();
            }

            // Bottom Navigation Bar
            float barW = Mathf.Min(820, Screen.width - 40);
            float barX = (Screen.width - barW) * 0.5f;
            float barY = Screen.height - 65;
            GUI.DrawTexture(new Rect(barX, barY, barW, 50), panelTex);
            GUI.Box(new Rect(barX, barY, barW, 50), GUIContent.none);

            GUILayout.BeginArea(new Rect(barX + 10, barY + 10, barW - 20, 32));
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("◄ PREV SHIP [A]", btnStyle, GUILayout.Width(130), GUILayout.Height(30)))
            {
                PrevShip();
            }
            if (GUILayout.Button("NEXT SHIP [D] ►", btnStyle, GUILayout.Width(130), GUILayout.Height(30)))
            {
                NextShip();
            }

            GUILayout.FlexibleSpace();

            GUI.backgroundColor = isOverview ? new Color(0.2f, 0.8f, 1.0f) : Color.white;
            if (GUILayout.Button(isOverview ? "EXIT OVERVIEW" : "OVERVIEW [Space]", btnStyle, GUILayout.Width(150), GUILayout.Height(30)))
            {
                ToggleOverview();
            }

            GUI.backgroundColor = isFlyby ? new Color(1.0f, 0.4f, 0.2f) : Color.white;
            if (GUILayout.Button(isFlyby ? "STOP FLYBY" : "RUNWAY FLYBY [F]", btnStyle, GUILayout.Width(150), GUILayout.Height(30)))
            {
                ToggleFlyby();
            }

            GUI.backgroundColor = isArmada ? new Color(0.9f, 0.7f, 0.1f) : Color.white;
            if (GUILayout.Button(isArmada ? "FORMATION: ARMADA" : "FORMATION: LINEUP [T]", btnStyle, GUILayout.Width(170), GUILayout.Height(30)))
            {
                ToggleFormation();
            }
            GUI.backgroundColor = Color.white;

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
