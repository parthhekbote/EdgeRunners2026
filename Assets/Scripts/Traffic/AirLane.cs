using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CyberpunkDev.Traffic
{
    [ExecuteAlways]
    public class AirLane : MonoBehaviour
    {
        [Header("Lane Configuration")]
        [Tooltip("Descriptive name for this air lane.")]
        public string laneName = "Lane";

        [Tooltip("Nominal altitude of this lane.")]
        public float altitude = -10.0f;

        [Tooltip("Lane altitude offset.")]
        public float altitudeOffset = 0.0f;

        [Tooltip("If true, path is a continuous closed loop. If false, it is a linear thoroughfare / one-way transit line.")]
        public bool isLoop = false;

        [Tooltip("True for forward travel from start to end, false for reverse.")]
        public bool isClockwise = true;

        [Tooltip("Gizmo display color.")]
        public Color pathColor = new Color(0f, 0.95f, 1f, 0.9f);

        [Tooltip("List of waypoint transforms forming the path.")]
        public List<Transform> waypoints = new List<Transform>();

        [Header("Gizmo Settings")]
        public bool showGizmos = true;
        [Range(10, 40)] public int gizmoSamplesPerSegment = 20;

        // Spline arc-length precomputed table for constant velocity
        private const int SamplesPerSegment = 32;
        private float[] m_CumulativeDistances;
        private float m_TotalLength;
        private Vector3[] m_CachedPositions;
        private bool m_IsInitialized;

        public float TotalLength => m_TotalLength > 0.01f ? m_TotalLength : 100f;
        public int WaypointCount => waypoints != null ? waypoints.Count : 0;
        public Vector3 StartPoint => (waypoints != null && waypoints.Count > 0 && waypoints[0] != null) ? waypoints[0].position : transform.position;
        public Vector3 EndPoint => (waypoints != null && waypoints.Count > 0 && waypoints[waypoints.Count - 1] != null) ? waypoints[waypoints.Count - 1].position : transform.position;

        private void Awake()
        {
            RebuildSpline();
        }

        private void OnEnable()
        {
            RebuildSpline();
        }

        private void OnValidate()
        {
            RebuildSpline();
        }

        /// <summary>
        /// Collects child transforms if waypoints list is empty and precomputes arc length tables.
        /// </summary>
        public void RebuildSpline()
        {
            if (waypoints == null || waypoints.Count == 0)
            {
                waypoints = new List<Transform>();
                for (int i = 0; i < transform.childCount; i++)
                {
                    var child = transform.GetChild(i);
                    if (child.gameObject.activeSelf)
                    {
                        waypoints.Add(child);
                    }
                }
            }

            // Remove any null entries
            for (int i = waypoints.Count - 1; i >= 0; i--)
            {
                if (waypoints[i] == null)
                    waypoints.RemoveAt(i);
            }

            int count = waypoints.Count;
            if (count < 2)
            {
                m_IsInitialized = false;
                m_TotalLength = 1f;
                return;
            }

            m_CachedPositions = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                m_CachedPositions[i] = waypoints[i].position;
            }

            int totalSamples = count * SamplesPerSegment;
            m_CumulativeDistances = new float[totalSamples + 1];
            m_CumulativeDistances[0] = 0f;

            Vector3 prevPos = EvaluateRawPosition(0f);
            float accum = 0f;

            for (int i = 1; i <= totalSamples; i++)
            {
                float t = (float)i / totalSamples;
                Vector3 currPos = EvaluateRawPosition(t);
                accum += Vector3.Distance(prevPos, currPos);
                m_CumulativeDistances[i] = accum;
                prevPos = currPos;
            }

            m_TotalLength = accum;
            m_IsInitialized = true;
        }

        /// <summary>
        /// Evaluates world position along the spline at an absolute arc-length distance.
        /// </summary>
        public Vector3 GetPointAtDistance(float distance)
        {
            if (!m_IsInitialized || m_TotalLength < 0.01f)
            {
                RebuildSpline();
                if (!m_IsInitialized) return transform.position;
            }

            if (isLoop)
            {
                distance = Mathf.Repeat(distance, m_TotalLength);
            }
            else
            {
                distance = Mathf.Clamp(distance, 0f, m_TotalLength);
            }

            // If counter-clockwise and configured to reverse direction, invert distance
            if (!isClockwise)
            {
                distance = m_TotalLength - distance;
            }

            float normalizedT = DistanceToNormalizedT(distance);
            Vector3 pos = EvaluateRawPosition(normalizedT);
            pos.y += altitudeOffset;
            return pos;
        }

        /// <summary>
        /// Evaluates world tangent (normalized travel forward direction) at a distance.
        /// </summary>
        public Vector3 GetTangentAtDistance(float distance)
        {
            if (!m_IsInitialized || m_TotalLength < 0.01f)
            {
                RebuildSpline();
                if (!m_IsInitialized) return Vector3.forward;
            }

            if (isLoop)
            {
                distance = Mathf.Repeat(distance, m_TotalLength);
            }
            else
            {
                distance = Mathf.Clamp(distance, 0f, m_TotalLength);
            }

            float evalDist = !isClockwise ? (m_TotalLength - distance) : distance;
            float normalizedT = DistanceToNormalizedT(evalDist);

            Vector3 rawTangent = EvaluateRawTangent(normalizedT);
            if (!isClockwise)
            {
                rawTangent = -rawTangent;
            }

            return rawTangent.sqrMagnitude > 0.001f ? rawTangent.normalized : Vector3.forward;
        }

        /// <summary>
        /// Converts an arc-length distance into normalized parameter t in [0, 1] using binary search on cumulative table.
        /// </summary>
        private float DistanceToNormalizedT(float distance)
        {
            int totalSamples = m_CumulativeDistances.Length - 1;
            int low = 0;
            int high = totalSamples;

            while (low <= high)
            {
                int mid = (low + high) / 2;
                if (m_CumulativeDistances[mid] < distance)
                    low = mid + 1;
                else
                    high = mid - 1;
            }

            int index = Mathf.Clamp(high, 0, totalSamples - 1);
            float d0 = m_CumulativeDistances[index];
            float d1 = m_CumulativeDistances[index + 1];
            float segmentT = (d1 > d0) ? Mathf.Clamp01((distance - d0) / (d1 - d0)) : 0f;

            return ((float)index + segmentT) / totalSamples;
        }

        /// <summary>
        /// Standard Centripetal/Uniform Catmull-Rom spline position evaluation.
        /// parameter t in [0, 1]. Supports both closed loops and open linear thoroughfares.
        /// </summary>
        private Vector3 EvaluateRawPosition(float t)
        {
            int count = m_CachedPositions != null ? m_CachedPositions.Length : waypoints.Count;
            if (count < 2) return transform.position;

            Vector3 p0, p1, p2, p3;
            float u;

            if (isLoop)
            {
                float scaledT = Mathf.Repeat(t, 1f) * count;
                int i = Mathf.FloorToInt(scaledT);
                u = scaledT - i;

                p0 = m_CachedPositions[(i - 1 + count) % count];
                p1 = m_CachedPositions[i % count];
                p2 = m_CachedPositions[(i + 1) % count];
                p3 = m_CachedPositions[(i + 2) % count];
            }
            else
            {
                int segs = count - 1;
                float scaledT = Mathf.Clamp(t, 0f, 0.9999f) * segs;
                int i = Mathf.FloorToInt(scaledT);
                u = scaledT - i;

                p0 = m_CachedPositions[Mathf.Max(0, i - 1)];
                p1 = m_CachedPositions[i];
                p2 = m_CachedPositions[Mathf.Min(count - 1, i + 1)];
                p3 = m_CachedPositions[Mathf.Min(count - 1, i + 2)];
            }

            float u2 = u * u;
            float u3 = u2 * u;

            return 0.5f * (
                (2f * p1) +
                (-p0 + p2) * u +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * u3
            );
        }

        /// <summary>
        /// Evaluates analytic first derivative (velocity/tangent vector) along the Catmull-Rom spline.
        /// </summary>
        private Vector3 EvaluateRawTangent(float t)
        {
            int count = m_CachedPositions != null ? m_CachedPositions.Length : waypoints.Count;
            if (count < 2) return Vector3.forward;

            Vector3 p0, p1, p2, p3;
            float u;

            if (isLoop)
            {
                float scaledT = Mathf.Repeat(t, 1f) * count;
                int i = Mathf.FloorToInt(scaledT);
                u = scaledT - i;

                p0 = m_CachedPositions[(i - 1 + count) % count];
                p1 = m_CachedPositions[i % count];
                p2 = m_CachedPositions[(i + 1) % count];
                p3 = m_CachedPositions[(i + 2) % count];
            }
            else
            {
                int segs = count - 1;
                float scaledT = Mathf.Clamp(t, 0f, 0.9999f) * segs;
                int i = Mathf.FloorToInt(scaledT);
                u = scaledT - i;

                p0 = m_CachedPositions[Mathf.Max(0, i - 1)];
                p1 = m_CachedPositions[i];
                p2 = m_CachedPositions[Mathf.Min(count - 1, i + 1)];
                p3 = m_CachedPositions[Mathf.Min(count - 1, i + 2)];
            }

            float u2 = u * u;

            return 0.5f * (
                (-p0 + p2) +
                2f * (2f * p0 - 5f * p1 + 4f * p2 - p3) * u +
                3f * (-p0 + 3f * p1 - 3f * p2 + p3) * u2
            );
        }


#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!showGizmos) return;

            if (waypoints == null || waypoints.Count < 2)
            {
                RebuildSpline();
                if (waypoints == null || waypoints.Count < 2) return;
            }

            Gizmos.color = pathColor;
            int totalSteps = (waypoints.Count - 1) * gizmoSamplesPerSegment;
            Vector3 lastPoint = EvaluateRawPosition(0f);

            for (int i = 1; i <= totalSteps; i++)
            {
                float t = (float)i / totalSteps;
                Vector3 currentPoint = EvaluateRawPosition(t);
                Gizmos.DrawLine(lastPoint, currentPoint);
                lastPoint = currentPoint;
            }

            GUIStyle labelStyle = new GUIStyle();
            labelStyle.fontSize = 11;
            labelStyle.fontStyle = FontStyle.Bold;

            // Draw waypoints and clear Start/End spawn/despawn markers
            int count = waypoints.Count;
            for (int i = 0; i < count; i++)
            {
                var wp = waypoints[i];
                if (wp == null) continue;

                bool isStart = (i == 0);
                bool isEnd = (i == count - 1);

                if (isStart)
                {
                    // Green: Vehicle Spawn Point
                    Gizmos.color = new Color(0.1f, 1.0f, 0.3f, 0.95f);
                    Gizmos.DrawWireSphere(wp.position, 1.8f);
                    Gizmos.DrawSphere(wp.position, 0.8f);

                    labelStyle.normal.textColor = new Color(0.2f, 1.0f, 0.4f);
                    Handles.Label(wp.position + Vector3.up * 2.2f, $"[SPAWN START] {laneName}", labelStyle);
                }
                else if (isEnd)
                {
                    // Red: Vehicle Despawn Point
                    Gizmos.color = new Color(1.0f, 0.2f, 0.2f, 0.95f);
                    Gizmos.DrawWireSphere(wp.position, 1.8f);
                    Gizmos.DrawSphere(wp.position, 0.8f);

                    labelStyle.normal.textColor = new Color(1.0f, 0.3f, 0.3f);
                    Handles.Label(wp.position + Vector3.up * 2.2f, $"[DESPAWN END] {laneName}", labelStyle);
                }
                else
                {
                    Gizmos.color = new Color(pathColor.r, pathColor.g, pathColor.b, 0.9f);
                    Gizmos.DrawWireSphere(wp.position, 1.2f);
                    Gizmos.DrawSphere(wp.position, 0.4f);

                    labelStyle.normal.textColor = Color.white;
                    Handles.Label(wp.position + Vector3.up * 1.5f, $"{laneName} WP #{i + 1}", labelStyle);
                }

                // Direction arrow
                if (!isEnd)
                {
                    float dist = ((float)i / (count - 1)) * TotalLength;
                    Vector3 tangent = GetTangentAtDistance(dist);
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawRay(wp.position, tangent * 3.5f);
                }
            }
        }
#endif
    }
}
