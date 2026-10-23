using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SyntyCombined.Cutscene
{
    /// <summary>
    /// Interactive Scene-View Waypoint Node.
    /// Allows the user to select and drag this GameObject directly in the Scene View with standard Unity Move tools (W).
    /// Automatically updates the ScriptableObject flight path and 3D Trajectory Line in real time!
    /// </summary>
    [ExecuteAlways]
    [SelectionBase]
    public class FlightWaypointNode : MonoBehaviour
    {
        [Header("Waypoint Data Link")]
        public int waypointIndex = 0;
        public BlackMarketFlightPathSO targetSO;
        public BlackMarketArrivalCutsceneDirector director;

        [Header("Bézier Tangents (Local to this waypoint)")]
        public Vector3 handleIn = new Vector3(0f, 0f, -10f);
        public Vector3 handleOut = new Vector3(0f, 0f, 10f);

        [Header("Flight Dynamics")]
        [Range(-65f, 65f)] public float rollBankAngle = 0f;
        public float targetSpeed = 60f;
        [Range(0.1f, 2.5f)] public float thrusterPower = 1.0f;
        [Range(40f, 95f)] public float cameraFov = 60f;

        private Vector3 _lastPosition;
        private Quaternion _lastRotation;

        private void OnEnable()
        {
            _lastPosition = transform.position;
            _lastRotation = transform.rotation;
        }

        private void OnValidate()
        {
            // Do not force asset dirtying during deserialization or GUI layout repaints
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            SyncToScriptableObject(false);
        }

        private void Update()
        {
            if (Application.isPlaying) return;

            // Detect if the user dragged or rotated this GameObject in the Scene View
            if (transform.position != _lastPosition || transform.rotation != _lastRotation)
            {
                _lastPosition = transform.position;
                _lastRotation = transform.rotation;
                SyncToScriptableObject(true);
            }
        }

        public void InitializeFromWaypoint(int idx, FlightWaypoint wp, BlackMarketFlightPathSO so, BlackMarketArrivalCutsceneDirector dir)
        {
            waypointIndex = idx;
            targetSO = so;
            director = dir;

            transform.position = wp.position;
            handleIn = wp.handleIn;
            handleOut = wp.handleOut;
            rollBankAngle = wp.rollBankAngle;
            targetSpeed = wp.targetSpeed;
            thrusterPower = wp.thrusterPower;
            cameraFov = wp.cameraFov;

            _lastPosition = transform.position;
            _lastRotation = transform.rotation;
        }

        public void SyncToScriptableObject(bool markDirty = true)
        {
            if (targetSO == null) return;
            if (waypointIndex < 0 || waypointIndex >= targetSO.waypoints.Count) return;

            // Guardrail: clamp tangents to prevent runaway values from distorting the flight path
            handleIn = Vector3.ClampMagnitude(handleIn, 90f);
            handleOut = Vector3.ClampMagnitude(handleOut, 90f);

            var wp = targetSO.waypoints[waypointIndex];
            wp.position = transform.position;
            wp.handleIn = handleIn;
            wp.handleOut = handleOut;
            wp.rollBankAngle = rollBankAngle;
            wp.targetSpeed = targetSpeed;
            wp.thrusterPower = thrusterPower;
            wp.cameraFov = cameraFov;

#if UNITY_EDITOR
            if (markDirty && !Application.isPlaying)
            {
                EditorUtility.SetDirty(targetSO);
            }
#endif

            if (director != null)
            {
                director.SetupTrajectoryLineRenderer();
            }
        }

        private void OnDrawGizmos()
        {
            // Never draw any trajectory lines or gizmos during gameplay / play mode
            if (Application.isPlaying) return;

            // Draw spherical node marker
            Gizmos.color = (waypointIndex == 0) ? Color.magenta : (waypointIndex == 6 ? Color.green : Color.cyan);
            Gizmos.DrawSphere(transform.position, 1.2f);

            // Draw Tangent lines
            Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.8f);
            Gizmos.DrawLine(transform.position, transform.position + handleIn);
            Gizmos.DrawWireSphere(transform.position + handleIn, 0.45f);

            Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.8f);
            Gizmos.DrawLine(transform.position, transform.position + handleOut);
            Gizmos.DrawWireSphere(transform.position + handleOut, 0.45f);

#if UNITY_EDITOR
            GUIStyle style = new GUIStyle();
            style.normal.textColor = Color.white;
            style.fontStyle = FontStyle.Bold;
            style.fontSize = 11;
            Handles.Label(transform.position + Vector3.up * 2f, $"[{waypointIndex + 1}] {gameObject.name} (Roll: {rollBankAngle:F0}°)", style);
#endif
        }
    }
}
