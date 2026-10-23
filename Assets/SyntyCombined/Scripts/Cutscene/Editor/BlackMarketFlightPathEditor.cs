#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace SyntyCombined.Cutscene
{
    [CustomEditor(typeof(BlackMarketFlightPathSO))]
    public class BlackMarketFlightPathEditor : UnityEditor.Editor
    {
        private BlackMarketFlightPathSO _targetSO;
        private float _previewT = 0f;
        private bool _showPreview = false;
        private int _selectedWaypointIndex = -1;

        private void OnEnable()
        {
            _targetSO = (BlackMarketFlightPathSO)target;
        }

        [MenuItem("Cyberpunk/Generate Arrival FlightPath Asset")]
        public static void EnsureFlightPathAssetExists()
        {
            string dirPath = "Assets/SyntyCombined/Data";
            if (!System.IO.Directory.Exists(dirPath))
            {
                System.IO.Directory.CreateDirectory(dirPath);
            }

            string assetPath = "Assets/SyntyCombined/Data/BlackMarket_Arrival_FlightPath.asset";
            var existing = AssetDatabase.LoadAssetAtPath<BlackMarketFlightPathSO>(assetPath);
            if (existing == null)
            {
                var newSO = ScriptableObject.CreateInstance<BlackMarketFlightPathSO>();
                newSO.ResetToDefaultCurvyPath();
                AssetDatabase.CreateAsset(newSO, assetPath);
                AssetDatabase.SaveAssets();
                Debug.Log("<color=#00F0FF><b>[Flight Path SO]</b></color> Auto-created default arrival flight path asset at: " + assetPath);
            }
            else
            {
                existing.ResetToDefaultCurvyPath();
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();
                Debug.Log("<color=#00F0FF><b>[Flight Path SO]</b></color> Refreshed arrival flight path asset to match user SceneView angle.");
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("GUARDIANS OF THE GALAXY FLIGHT PATH", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Tweak the 30-Second curvy arrival path into DRIFT (Sector 4 Outlaw Market).\n" +
                "• Drag 3D Waypoint handles directly in the Scene View.\n" +
                "• Drag yellow in/out Bézier handles to adjust turn curvature.\n" +
                "• Adjust Roll Bank Angle (-65° to +65°) for dynamic starship banking.",
                MessageType.Info
            );

            EditorGUILayout.Space(6);

            // Quick action buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset to Default S-Curve", GUILayout.Height(28)))
            {
                if (EditorUtility.DisplayDialog("Reset Flight Path", "Reset flight path waypoints to the default dynamic S-curve weave into DRIFT?", "Yes", "Cancel"))
                {
                    Undo.RecordObject(_targetSO, "Reset Flight Path to Default");
                    _targetSO.ResetToDefaultCurvyPath();
                    SceneView.RepaintAll();
                }
            }

            if (GUILayout.Button("Add Waypoint at End", GUILayout.Height(28)))
            {
                Undo.RecordObject(_targetSO, "Add Waypoint");
                Vector3 newPos = _targetSO.waypoints.Count > 0 
                    ? _targetSO.waypoints[_targetSO.waypoints.Count - 1].position + Vector3.forward * 10f 
                    : Vector3.zero;
                _targetSO.waypoints.Add(new FlightWaypoint(
                    $"Waypoint_{_targetSO.waypoints.Count + 1}",
                    newPos,
                    new Vector3(0f, 0f, -5f),
                    new Vector3(0f, 0f, 5f),
                    0f,
                    35f,
                    1f,
                    65f
                ));
                EditorUtility.SetDirty(_targetSO);
                SceneView.RepaintAll();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // Preview scrubber
            EditorGUILayout.LabelField("30-Second Trajectory Scrubber", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            _showPreview = EditorGUILayout.Toggle("Enable Scene Preview", _showPreview);
            if (_showPreview)
            {
                EditorGUI.BeginChangeCheck();
                _previewT = EditorGUILayout.Slider("Preview Progress (t)", _previewT, 0f, 1f);
                float previewTime = _previewT * 30.0f;
                EditorGUILayout.LabelField($"Simulated Time: {previewTime:F1}s / 30.0s");
                if (EditorGUI.EndChangeCheck())
                {
                    SceneView.RepaintAll();
                }

                _targetSO.Evaluate(_previewT, out Vector3 prevPos, out Quaternion prevRot, out float prevSpd, out float prevThrust, out float prevFov);
                EditorGUILayout.LabelField($"Position: {prevPos.ToString("F1")}  |  Speed: {prevSpd:F1} m/s");
                EditorGUILayout.LabelField($"Bank Roll: {prevRot.eulerAngles.z:F1}°  |  Thruster: {prevThrust * 100f:F0}%  |  FOV: {prevFov:F0}°");
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Standard serialized properties
            DrawDefaultInspector();

            serializedObject.ApplyModifiedProperties();
        }

        private void OnSceneGUI()
        {
            if (_targetSO == null || _targetSO.waypoints == null || _targetSO.waypoints.Count == 0) return;

            // Draw full continuous Bézier curve
            DrawFlightCurve();

            // Draw interactive waypoints and Bézier handles
            for (int i = 0; i < _targetSO.waypoints.Count; i++)
            {
                DrawWaypointHandle(i);
            }

            // Draw preview ship orientation if scrubber enabled
            if (_showPreview)
            {
                DrawPreviewShipMarker();
            }
        }

        private void DrawFlightCurve()
        {
            int segments = _targetSO.curveResolution;
            Vector3 previousPoint = Vector3.zero;

            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                _targetSO.Evaluate(t, out Vector3 currentPoint, out _, out _, out _, out _);

                if (i > 0)
                {
                    Color segmentColor = Color.Lerp(_targetSO.curveColor, _targetSO.handleColor, t);
                    Handles.color = segmentColor;
                    Handles.DrawLine(previousPoint, currentPoint, 3.5f);
                }
                previousPoint = currentPoint;
            }
        }

        private void DrawWaypointHandle(int index)
        {
            var wp = _targetSO.waypoints[index];

            // 1. Waypoint position handle
            Handles.color = (index == 0) ? Color.cyan : (index == _targetSO.waypoints.Count - 1 ? Color.green : _targetSO.waypointColor);
            float handleSize = HandleUtility.GetHandleSize(wp.position) * 0.18f;
            Handles.SphereHandleCap(0, wp.position, Quaternion.identity, handleSize, EventType.Repaint);

            // Waypoint Label
            GUIStyle labelStyle = new GUIStyle();
            labelStyle.normal.textColor = Color.white;
            labelStyle.fontStyle = FontStyle.Bold;
            labelStyle.fontSize = 11;
            Handles.Label(wp.position + Vector3.up * (handleSize * 1.5f), $"[{index + 1}] {wp.label} (Roll: {wp.rollBankAngle:F0}°)", labelStyle);

            // Interactive translation handle
            EditorGUI.BeginChangeCheck();
            Vector3 newPos = Handles.DoPositionHandle(wp.position, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_targetSO, "Move Waypoint Position");
                wp.position = newPos;
                EditorUtility.SetDirty(_targetSO);
            }

            // 2. Bézier Handles (In and Out)
            Vector3 handleInWorld = wp.position + wp.handleIn;
            Vector3 handleOutWorld = wp.position + wp.handleOut;

            Handles.color = _targetSO.handleColor;
            Handles.DrawDottedLine(wp.position, handleInWorld, 3f);
            Handles.DrawDottedLine(wp.position, handleOutWorld, 3f);

            // Handle In
            if (index > 0)
            {
                Handles.color = new Color(0.2f, 0.8f, 1f, 0.9f);
                float inSize = HandleUtility.GetHandleSize(handleInWorld) * 0.12f;
                Handles.DotHandleCap(0, handleInWorld, Quaternion.identity, inSize, EventType.Repaint);

                EditorGUI.BeginChangeCheck();
                Vector3 newIn = Handles.DoPositionHandle(handleInWorld, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_targetSO, "Adjust Waypoint Handle In");
                    wp.handleIn = newIn - wp.position;
                    EditorUtility.SetDirty(_targetSO);
                }
            }

            // Handle Out
            if (index < _targetSO.waypoints.Count - 1)
            {
                Handles.color = new Color(1f, 0.85f, 0.1f, 0.9f);
                float outSize = HandleUtility.GetHandleSize(handleOutWorld) * 0.12f;
                Handles.DotHandleCap(0, handleOutWorld, Quaternion.identity, outSize, EventType.Repaint);

                EditorGUI.BeginChangeCheck();
                Vector3 newOut = Handles.DoPositionHandle(handleOutWorld, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_targetSO, "Adjust Waypoint Handle Out");
                    wp.handleOut = newOut - wp.position;
                    EditorUtility.SetDirty(_targetSO);
                }
            }
        }

        private void DrawPreviewShipMarker()
        {
            _targetSO.Evaluate(_previewT, out Vector3 pos, out Quaternion rot, out _, out _, out _);

            Handles.color = new Color(0f, 1f, 0.4f, 0.9f);
            Handles.SphereHandleCap(0, pos, Quaternion.identity, HandleUtility.GetHandleSize(pos) * 0.22f, EventType.Repaint);

            Handles.color = Color.blue;
            Handles.DrawLine(pos, pos + rot * Vector3.forward * 4f, 3f);
            Handles.color = Color.green;
            Handles.DrawLine(pos, pos + rot * Vector3.up * 2.5f, 3f);
            Handles.color = Color.red;
            Handles.DrawLine(pos, pos + rot * Vector3.right * 2.5f, 3f);

            GUIStyle previewStyle = new GUIStyle();
            previewStyle.normal.textColor = new Color(0f, 1f, 0.4f);
            previewStyle.fontStyle = FontStyle.Bold;
            previewStyle.fontSize = 12;
            Handles.Label(pos + Vector3.up * 3f, $"PREVIEW SHIP (t={_previewT:F2} | {_previewT * 30f:F1}s)", previewStyle);
        }
    }

    [CustomEditor(typeof(BlackMarketArrivalCutsceneDirector))]
    public class BlackMarketArrivalCutsceneDirectorEditor : UnityEditor.Editor
    {
        private int _selectedWaypointIndex = -1;
        private float _previewT = 0f;
        private bool _showPreview = false;

        public override void OnInspectorGUI()
        {
            var director = (BlackMarketArrivalCutsceneDirector)target;

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "Guardians of the Galaxy Arrival Cutscene Director for DRIFT (30.0s).\n" +
                "Plays Guns N' Roses 'Sweet Child O' Mine' while Alita's starship swoops along the Bézier curve into Sector 4.\n" +
                "• Click 'Spawn Draggable Waypoint Objects' to drag path nodes directly with W in Scene View.\n" +
                "• Or select waypoints below/in Scene View to adjust curve handles directly.",
                MessageType.Info
            );

            EditorGUILayout.Space(6);

            // Playback controls
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.2f, 1f, 0.4f);
            if (GUILayout.Button("▶ Play Cutscene (30s)", GUILayout.Height(32)))
            {
                if (Application.isPlaying)
                {
                    director.StartCutscene();
                }
                else
                {
                    EditorUtility.DisplayDialog("Play Mode Required", "Please enter Play Mode to test the 30s cinematic cutscene with real-time audio and cameras.", "OK");
                }
            }
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("Skip / Finish", GUILayout.Height(32)))
            {
                if (Application.isPlaying)
                {
                    director.SkipCutscene();
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            // Interactive Scene Waypoints Management
            EditorGUILayout.LabelField("Interactive Scene Dragging Controls", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0f, 0.9f, 1f);
            if (GUILayout.Button("Spawn Draggable Waypoints in Scene", GUILayout.Height(28)))
            {
                director.SpawnSceneWaypointObjects();
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("Bake Scene to Asset", GUILayout.Height(28)))
            {
                director.BakeSceneWaypointsToSO();
                SceneView.RepaintAll();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear Scene Waypoint Objects", GUILayout.Height(24)))
            {
                director.ClearSceneWaypointObjects();
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("Reset to Default 30s S-Curve", GUILayout.Height(24)))
            {
                if (EditorUtility.DisplayDialog("Reset Flight Path", "Reset flight path waypoints to the default dynamic 30s S-curve into DRIFT?", "Yes", "Cancel"))
                {
                    director.EnsureFlightPathLoaded();
                    if (director.flightPath != null)
                    {
                        Undo.RecordObject(director.flightPath, "Reset Flight Path to Default");
                        director.flightPath.ResetToDefaultCurvyPath();
                        director.SetupTrajectoryLineRenderer();
                        if (director.waypointsParent != null)
                        {
                            director.SpawnSceneWaypointObjects();
                        }
                        SceneView.RepaintAll();
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);

            // Timeline preview scrubber
            EditorGUILayout.LabelField("30-Second Timeline Scrubber", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            _showPreview = EditorGUILayout.Toggle("Enable Timeline Preview", _showPreview);
            if (_showPreview && director.flightPath != null)
            {
                EditorGUI.BeginChangeCheck();
                _previewT = EditorGUILayout.Slider("Cutscene Progress (t)", _previewT, 0f, 1f);
                float previewTime = _previewT * director.cutsceneDuration;
                EditorGUILayout.LabelField($"Timeline: {previewTime:F1}s / {director.cutsceneDuration:F1}s", EditorStyles.boldLabel);
                
                if (EditorGUI.EndChangeCheck())
                {
                    director.flightPath.Evaluate(_previewT, out Vector3 prevPos, out Quaternion prevRot, out _, out _, out _);
                    if (director.starship != null)
                    {
                        Undo.RecordObject(director.starship.transform, "Scrub Cutscene");
                        director.starship.transform.position = prevPos;
                        director.starship.transform.rotation = prevRot;
                    }
                    SceneView.RepaintAll();
                }
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);
            DrawDefaultInspector();
        }

        private void OnSceneGUI()
        {
            var director = (BlackMarketArrivalCutsceneDirector)target;
            if (director == null) return;

            director.EnsureFlightPathLoaded();
            var so = director.flightPath;
            if (so == null || so.waypoints == null || so.waypoints.Count == 0) return;

            // Draw full flight path curve in Scene View
            int segments = so.curveResolution;
            Vector3 prev = Vector3.zero;
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                so.Evaluate(t, out Vector3 curr, out _, out _, out _, out _);
                if (i > 0)
                {
                    Handles.color = Color.Lerp(so.curveColor, so.handleColor, t);
                    Handles.DrawLine(prev, curr, 3.5f);
                }
                prev = curr;
            }

            // Draw interactive waypoints
            for (int i = 0; i < so.waypoints.Count; i++)
            {
                var wp = so.waypoints[i];
                float handleSize = HandleUtility.GetHandleSize(wp.position) * 0.18f;

                // Spherical button to select waypoint
                Handles.color = (i == _selectedWaypointIndex) 
                    ? Color.yellow 
                    : (i == 0 ? Color.cyan : (i == so.waypoints.Count - 1 ? Color.green : so.waypointColor));

                if (Handles.Button(wp.position, Quaternion.identity, handleSize, handleSize * 1.15f, Handles.SphereHandleCap))
                {
                    _selectedWaypointIndex = i;
                    Repaint();
                }

                // Label
                GUIStyle labelStyle = new GUIStyle();
                labelStyle.normal.textColor = (i == _selectedWaypointIndex) ? Color.yellow : Color.white;
                labelStyle.fontStyle = FontStyle.Bold;
                labelStyle.fontSize = 11;
                Handles.Label(wp.position + Vector3.up * (handleSize * 1.6f), $"[{i + 1}] {wp.label} (Roll: {wp.rollBankAngle:F0}°)", labelStyle);

                // If this waypoint is selected, show 3D Position Handles directly in Scene View
                if (i == _selectedWaypointIndex)
                {
                    EditorGUI.BeginChangeCheck();
                    Vector3 newPos = Handles.DoPositionHandle(wp.position, Quaternion.identity);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(so, "Move Waypoint Position");
                        wp.position = newPos;
                        EditorUtility.SetDirty(so);
                        director.SetupTrajectoryLineRenderer();

                        // Also sync matching child GameObject if exists
                        if (director.waypointsParent != null && i < director.waypointsParent.childCount)
                        {
                            director.waypointsParent.GetChild(i).position = newPos;
                        }
                    }

                    // Bézier in and out handles (guarded with clamping)
                    Vector3 handleInWorld = wp.position + wp.handleIn;
                    Vector3 handleOutWorld = wp.position + wp.handleOut;

                    Handles.color = so.handleColor;
                    Handles.DrawDottedLine(wp.position, handleInWorld, 3f);
                    Handles.DrawDottedLine(wp.position, handleOutWorld, 3f);

                    if (i > 0)
                    {
                        EditorGUI.BeginChangeCheck();
                        Vector3 newIn = Handles.DoPositionHandle(handleInWorld, Quaternion.identity);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(so, "Adjust Waypoint Handle In");
                            wp.handleIn = Vector3.ClampMagnitude(newIn - wp.position, 80f);
                            EditorUtility.SetDirty(so);
                            director.SetupTrajectoryLineRenderer();
                        }
                    }

                    if (i < so.waypoints.Count - 1)
                    {
                        EditorGUI.BeginChangeCheck();
                        Vector3 newOut = Handles.DoPositionHandle(handleOutWorld, Quaternion.identity);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(so, "Adjust Waypoint Handle Out");
                            wp.handleOut = Vector3.ClampMagnitude(newOut - wp.position, 80f);
                            EditorUtility.SetDirty(so);
                            director.SetupTrajectoryLineRenderer();
                        }
                    }
                }
            }

            // Draw Scene View 2D Overlay Panel
            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(16, 16, 290, 145), EditorStyles.helpBox);
            GUILayout.Label("<b>DRIFT Arrival Cutscene (30s)</b>", EditorStyles.boldLabel);
            if (_selectedWaypointIndex >= 0 && _selectedWaypointIndex < so.waypoints.Count)
            {
                var selWp = so.waypoints[_selectedWaypointIndex];
                GUILayout.Label($"Selected: [{_selectedWaypointIndex + 1}] {selWp.label}");
                GUILayout.Label($"Speed: {selWp.targetSpeed:F0} m/s | Bank Roll: {selWp.rollBankAngle:F0}°");
            }
            else
            {
                GUILayout.Label("Click any sphere on path to adjust handles.");
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Spawn Nodes in Scene", GUILayout.Height(24)))
            {
                director.SpawnSceneWaypointObjects();
            }
            if (GUILayout.Button("Bake to Asset", GUILayout.Height(24)))
            {
                director.BakeSceneWaypointsToSO();
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Reset 30s S-Curve", GUILayout.Height(22)))
            {
                Undo.RecordObject(so, "Reset Flight Path to Default");
                so.ResetToDefaultCurvyPath();
                director.SetupTrajectoryLineRenderer();
            }

            GUILayout.EndArea();
            Handles.EndGUI();
        }
    }

    [CustomEditor(typeof(FlightWaypointNode))]
    public class FlightWaypointNodeEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var node = (FlightWaypointNode)target;

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                $"Interactive Flight Waypoint [{node.waypointIndex + 1}]: {node.gameObject.name}\n" +
                "Move this GameObject directly with the W Move tool in the Scene View to reshape the flight trajectory.\n" +
                "Changes automatically update the ScriptableObject and LineRenderer live!",
                MessageType.Info
            );

            EditorGUILayout.Space(6);

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.LabelField("Flight Dynamics", EditorStyles.boldLabel);
            node.rollBankAngle = EditorGUILayout.Slider("Roll Bank Angle (°)", node.rollBankAngle, -65f, 65f);
            node.targetSpeed = EditorGUILayout.Slider("Target Speed (m/s)", node.targetSpeed, 5f, 160f);
            node.thrusterPower = EditorGUILayout.Slider("Thruster Power (x)", node.thrusterPower, 0.1f, 2.5f);
            node.cameraFov = EditorGUILayout.Slider("Camera FOV (°)", node.cameraFov, 40f, 95f);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Bézier Tangents (Local)", EditorStyles.boldLabel);
            node.handleIn = EditorGUILayout.Vector3Field("Handle In", node.handleIn);
            node.handleOut = EditorGUILayout.Vector3Field("Handle Out", node.handleOut);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(node, "Modify Waypoint Node");
                node.SyncToScriptableObject();
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space(10);

            // Waypoint navigation buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("◄ Previous Waypoint", GUILayout.Height(26)))
            {
                SelectAdjacentWaypoint(node, -1);
            }
            if (GUILayout.Button("Next Waypoint ►", GUILayout.Height(26)))
            {
                SelectAdjacentWaypoint(node, 1);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);
            if (GUILayout.Button("Sync to ScriptableObject Now", GUILayout.Height(26)))
            {
                node.SyncToScriptableObject();
            }
        }

        private void OnSceneGUI()
        {
            var node = (FlightWaypointNode)target;
            if (node == null) return;

            Vector3 nodePos = node.transform.position;

            // Tangent In Handle
            if (node.waypointIndex > 0)
            {
                Vector3 inWorld = nodePos + node.handleIn;
                Handles.color = new Color(0.2f, 0.85f, 1f, 0.9f);
                Handles.DrawDottedLine(nodePos, inWorld, 3f);

                EditorGUI.BeginChangeCheck();
                Vector3 newIn = Handles.DoPositionHandle(inWorld, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(node, "Move Handle In");
                    node.handleIn = Vector3.ClampMagnitude(newIn - nodePos, 80f);
                    node.SyncToScriptableObject();
                }
            }

            // Tangent Out Handle
            if (node.targetSO == null || node.waypointIndex < node.targetSO.waypoints.Count - 1)
            {
                Vector3 outWorld = nodePos + node.handleOut;
                Handles.color = new Color(1f, 0.85f, 0.1f, 0.9f);
                Handles.DrawDottedLine(nodePos, outWorld, 3f);

                EditorGUI.BeginChangeCheck();
                Vector3 newOut = Handles.DoPositionHandle(outWorld, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(node, "Move Handle Out");
                    node.handleOut = Vector3.ClampMagnitude(newOut - nodePos, 80f);
                    node.SyncToScriptableObject();
                }
            }
        }

        private void SelectAdjacentWaypoint(FlightWaypointNode node, int offset)
        {
            if (node.transform.parent == null) return;
            int targetIdx = node.transform.GetSiblingIndex() + offset;
            if (targetIdx >= 0 && targetIdx < node.transform.parent.childCount)
            {
                Selection.activeGameObject = node.transform.parent.GetChild(targetIdx).gameObject;
            }
        }
    }
}
#endif
