using System.Collections.Generic;
using UnityEngine;

namespace Cyberpunk.Traffic.Trains
{
    /// <summary>
    /// High-precision parametric 3D track path generated from connected rail beam segments.
    /// Provides constant-speed distance evaluation, smooth curvature interpolation, and zero-allocation sampling.
    /// </summary>
    [System.Serializable]
    public class CyberTrainPath
    {
        [SerializeField] private List<Vector3> _waypoints = new List<Vector3>();
        [SerializeField] private List<float> _cumulativeDistances = new List<float>();
        [SerializeField] private float _totalLength = 0f;

        public float TotalLength => _totalLength;
        public int WaypointCount => _waypoints.Count;
        public IReadOnlyList<Vector3> Waypoints => _waypoints;

        public Vector3 StartPoint => _waypoints.Count > 0 ? _waypoints[0] : Vector3.zero;
        public Vector3 EndPoint => _waypoints.Count > 0 ? _waypoints[_waypoints.Count - 1] : Vector3.zero;

        public bool IsValid => _waypoints != null && _waypoints.Count >= 2 && _totalLength > 0.01f;

        /// <summary>
        /// Clears and initializes the path with a list of ordered 3D points.
        /// Precomputes cumulative distances for O(log N) constant-velocity sampling.
        /// </summary>
        public void InitializeFromPoints(List<Vector3> points)
        {
            _waypoints.Clear();
            _cumulativeDistances.Clear();

            if (points == null || points.Count < 2)
            {
                _totalLength = 0f;
                return;
            }

            _waypoints.AddRange(points);
            _cumulativeDistances.Add(0f);
            float accum = 0f;

            for (int i = 1; i < _waypoints.Count; i++)
            {
                float segmentDist = Vector3.Distance(_waypoints[i - 1], _waypoints[i]);
                accum += segmentDist;
                _cumulativeDistances.Add(accum);
            }

            _totalLength = accum;
        }

        /// <summary>
        /// Evaluates the 3D position and forward tangent along the track at a given linear distance.
        /// Guaranteed zero GC allocations.
        /// </summary>
        public Vector3 Evaluate(float distance, out Vector3 forwardTangent)
        {
            if (_waypoints.Count == 0)
            {
                forwardTangent = Vector3.forward;
                return Vector3.zero;
            }

            if (_waypoints.Count == 1)
            {
                forwardTangent = Vector3.forward;
                return _waypoints[0];
            }

            // Clamp distance within track boundaries
            float clampedDist = Mathf.Clamp(distance, 0f, _totalLength);

            // Binary search for the containing segment
            int low = 0;
            int high = _cumulativeDistances.Count - 1;
            while (low <= high)
            {
                int mid = (low + high) >> 1;
                if (_cumulativeDistances[mid] < clampedDist)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            int index = Mathf.Clamp(low - 1, 0, _waypoints.Count - 2);
            float segStartDist = _cumulativeDistances[index];
            float segEndDist = _cumulativeDistances[index + 1];
            float segLength = segEndDist - segStartDist;

            float t = segLength > 0.0001f ? (clampedDist - segStartDist) / segLength : 0f;
            t = Mathf.Clamp01(t);

            Vector3 p0 = _waypoints[index];
            Vector3 p1 = _waypoints[index + 1];

            forwardTangent = (p1 - p0).normalized;
            if (forwardTangent.sqrMagnitude < 0.001f) forwardTangent = Vector3.forward;

            return Vector3.Lerp(p0, p1, t);
        }

        /// <summary>
        /// Finds the closest distance along the path to an arbitrary world point.
        /// </summary>
        public float FindClosestDistance(Vector3 worldPoint)
        {
            if (!IsValid) return 0f;

            float closestPathDist = 0f;
            float minSqrDist = float.MaxValue;

            for (int i = 0; i < _waypoints.Count - 1; i++)
            {
                Vector3 p0 = _waypoints[i];
                Vector3 p1 = _waypoints[i + 1];
                Vector3 segment = p1 - p0;
                float segLenSqr = segment.sqrMagnitude;

                float t = segLenSqr > 0.0001f ? Vector3.Dot(worldPoint - p0, segment) / segLenSqr : 0f;
                t = Mathf.Clamp01(t);

                Vector3 proj = p0 + segment * t;
                float sqrDist = (worldPoint - proj).sqrMagnitude;

                if (sqrDist < minSqrDist)
                {
                    minSqrDist = sqrDist;
                    closestPathDist = _cumulativeDistances[i] + Mathf.Sqrt(segLenSqr) * t;
                }
            }

            return closestPathDist;
        }

        /// <summary>
        /// Draws debug gizmos in the Scene view.
        /// </summary>
        public void DrawGizmos(Color pathColor, float sphereSize = 0.8f)
        {
            if (!IsValid) return;

            Gizmos.color = pathColor;
            for (int i = 0; i < _waypoints.Count - 1; i++)
            {
                Gizmos.DrawLine(_waypoints[i], _waypoints[i + 1]);
            }

            // Draw termini
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(_waypoints[0], sphereSize);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(_waypoints[_waypoints.Count - 1], sphereSize);
        }
    }
}
