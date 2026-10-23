#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using CyberpunkDev.Traffic;

namespace CyberpunkTrafficEditor
{
    [CustomEditor(typeof(AirLane))]
    [CanEditMultipleObjects]
    public class AirLaneEditor : Editor
    {
        private int _selectedWaypointIndex = -1;

        public override void OnInspectorGUI()
        {
            AirLane lane = (AirLane)target;

            DrawDefaultInspector();

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Interactive Corridor Tools", EditorStyles.boldLabel);

            // 1. DUPLICATE LANE BUTTON
            GUI.backgroundColor = new Color(0.2f, 0.9f, 0.5f);
            if (GUILayout.Button("Duplicate This Lane (Create Parallel Corridor)", GUILayout.Height(32)))
            {
                DuplicateLane(lane);
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();

            // 2. ADD WAYPOINT BUTTON
            if (GUILayout.Button("Add Waypoint at End", GUILayout.Height(26)))
            {
                AddWaypointAtEnd(lane);
            }

            // 3. REVERSE DIRECTION BUTTON
            if (GUILayout.Button("Reverse Direction", GUILayout.Height(26)))
            {
                ReverseLaneDirection(lane);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);
            // 4. REBUILD SPLINE BUTTON
            if (GUILayout.Button("Rebuild Spline & Refresh", GUILayout.Height(24)))
            {
                lane.RebuildSpline();
                EditorUtility.SetDirty(lane);
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox(
                "HOW TO EDIT IN SCENE VIEW:\n" +
                "• Select any waypoint (WP_xx) in the Hierarchy or click on its waypoint marker in the Scene view.\n" +
                "• Use the Move Tool (W key) to drag waypoints around.\n" +
                "• The colored flight trajectory updates dynamically in real time!",
                MessageType.Info);
        }

        private void OnSceneGUI()
        {
            AirLane lane = (AirLane)target;
            if (lane == null || lane.waypoints == null || lane.waypoints.Count == 0) return;

            // Render interactive Position Handles on waypoints in Scene View
            for (int i = 0; i < lane.waypoints.Count; i++)
            {
                Transform wp = lane.waypoints[i];
                if (wp == null) continue;

                EditorGUI.BeginChangeCheck();
                Vector3 newPos = Handles.PositionHandle(wp.position, wp.rotation);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(wp, "Move Waypoint");
                    wp.position = newPos;
                    lane.RebuildSpline();
                    EditorUtility.SetDirty(lane);
                }

                // Clickable handle sphere
                float handleSize = HandleUtility.GetHandleSize(wp.position) * 0.25f;
                Handles.color = (i == 0) ? Color.green : (i == lane.waypoints.Count - 1) ? Color.red : lane.pathColor;
                if (Handles.Button(wp.position, Quaternion.identity, handleSize, handleSize * 1.2f, Handles.SphereHandleCap))
                {
                    _selectedWaypointIndex = i;
                    Selection.activeTransform = wp;
                }
            }
        }

        public static void DuplicateLane(AirLane sourceLane)
        {
            if (sourceLane == null) return;

            // Instantiate duplicate GameObject
            GameObject newLaneObj = Instantiate(sourceLane.gameObject, sourceLane.transform.parent);
            newLaneObj.name = sourceLane.name + "_Parallel";

            AirLane newLane = newLaneObj.GetComponent<AirLane>();
            newLane.laneName = sourceLane.laneName + " Parallel";

            // Offset all waypoints by +8m laterally in X or Z so the new lane is clearly visible and separated
            Vector3 offset = new Vector3(8.0f, 0f, 0f);
            newLane.waypoints = new List<Transform>();

            for (int i = 0; i < newLaneObj.transform.childCount; i++)
            {
                Transform wp = newLaneObj.transform.GetChild(i);
                wp.position += offset;
                newLane.waypoints.Add(wp);
            }

            // Assign distinct color
            newLane.pathColor = new Color(
                Mathf.Repeat(sourceLane.pathColor.r + 0.35f, 1f),
                Mathf.Repeat(sourceLane.pathColor.g + 0.25f, 1f),
                Mathf.Repeat(sourceLane.pathColor.b + 0.45f, 1f),
                0.95f
            );

            newLane.RebuildSpline();

            // Register with FlyingCarManager
            FlyingCarManager manager = Object.FindAnyObjectByType<FlyingCarManager>();
            if (manager != null)
            {
                var lanesField = typeof(FlyingCarManager).GetField("lanes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (lanesField != null)
                {
                    AirLane[] currentLanes = (AirLane[])lanesField.GetValue(manager);
                    List<AirLane> updatedList = new List<AirLane>(currentLanes ?? new AirLane[0]);
                    if (!updatedList.Contains(newLane))
                    {
                        updatedList.Add(newLane);
                        lanesField.SetValue(manager, updatedList.ToArray());
                    }
                }
                EditorUtility.SetDirty(manager);
            }

            Undo.RegisterCreatedObjectUndo(newLaneObj, "Duplicate Air Lane");
            Selection.activeGameObject = newLaneObj;
            Debug.Log($"<color=#00FF99>[AirLaneEditor] Duplicated lane '{sourceLane.name}' as '{newLaneObj.name}' with {newLane.waypoints.Count} waypoints! Registered with FlyingCarManager.</color>");
        }

        public static void AddWaypointAtEnd(AirLane lane)
        {
            if (lane == null) return;

            Transform lastWp = (lane.waypoints != null && lane.waypoints.Count > 0)
                ? lane.waypoints[lane.waypoints.Count - 1]
                : null;

            Vector3 newPos = lastWp != null ? lastWp.position + lastWp.forward * 30f : lane.transform.position + Vector3.forward * 30f;

            GameObject newWp = new GameObject($"WP_{lane.transform.childCount + 1:D2}");
            newWp.transform.SetParent(lane.transform);
            newWp.transform.position = newPos;

            lane.waypoints.Add(newWp.transform);
            lane.RebuildSpline();

            Undo.RegisterCreatedObjectUndo(newWp, "Add Waypoint");
            Selection.activeGameObject = newWp;
            EditorUtility.SetDirty(lane);
        }

        public static void ReverseLaneDirection(AirLane lane)
        {
            if (lane == null || lane.waypoints == null || lane.waypoints.Count < 2) return;

            Undo.RecordObject(lane, "Reverse Lane Direction");
            lane.waypoints.Reverse();

            // Rename waypoints in sequential order
            for (int i = 0; i < lane.waypoints.Count; i++)
            {
                if (lane.waypoints[i] != null)
                {
                    Undo.RecordObject(lane.waypoints[i].gameObject, "Rename Waypoint");
                    lane.waypoints[i].name = $"WP_{i + 1:D2}";
                    lane.waypoints[i].SetSiblingIndex(i);
                }
            }

            lane.RebuildSpline();
            EditorUtility.SetDirty(lane);
            Debug.Log($"<color=#00FFFF>[AirLaneEditor] Reversed direction of lane '{lane.laneName}'.</color>");
        }
    }
}
#endif
