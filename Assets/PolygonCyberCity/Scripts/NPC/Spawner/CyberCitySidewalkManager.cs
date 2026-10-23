using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace PolygonCyberCity.NPC.Spawner
{
    /// <summary>
    /// Urban Sidewalk & Crosswalk Corridor Management System for Cyber City.
    /// Manages designated pedestrian walkways, sidewalk corridors, and street crosswalk nodes:
    /// 1. Differentiates sidewalk pedestrian corridors from vehicular road surfaces.
    /// 2. Guides pedestrian wandering along safe sidewalks rather than road medians.
    /// 3. Provides crosswalk waypoints so NPCs cross roads at realistic intersections.
    /// 4. Auto-discovers sidewalk anchors near shopfronts, benches, and building trims.
    /// </summary>
    public class CyberCitySidewalkManager : MonoBehaviour
    {
        public static CyberCitySidewalkManager Instance { get; private set; }

        [Header("Corridor Settings")]
        [Tooltip("Spacing between sampled sidewalk corridor waypoints.")]
        public float nodeSampleSpacing = 4.0f;
        [Tooltip("Search radius around sidewalks for urban anchors (benches, shops, consoles).")]
        public float urbanAnchorSearchRadius = 3.5f;

        [Header("Crosswalks")]
        [Tooltip("Designated crosswalk connectors across roadways.")]
        public List<CrosswalkZone> crosswalks = new List<CrosswalkZone>();

        [Header("Runtime Cache")]
        [SerializeField] private List<Vector3> _sidewalkNodes = new List<Vector3>();

        public IReadOnlyList<Vector3> SidewalkNodes => _sidewalkNodes;

        [System.Serializable]
        public struct CrosswalkZone
        {
            public string name;
            public Vector3 curbPointA;
            public Vector3 curbPointB;
            public float width;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            if (_sidewalkNodes.Count == 0)
            {
                BakeSidewalkCorridors();
            }
        }

        /// <summary>
        /// Auto-samples and bakes sidewalk corridor nodes based on urban props and pedestrian pathways.
        /// </summary>
        [ContextMenu("Bake Sidewalk Corridors")]
        public void BakeSidewalkCorridors()
        {
            _sidewalkNodes.Clear();
            crosswalks.Clear();

            // 1. Gather all urban anchors (benches, vending machines, shops, consoles)
            var anchors = new List<Vector3>();
            var allGos = FindObjectsByType<GameObject>();

            foreach (var go in allGos)
            {
                string n = go.name.ToLower();
                if (n.Contains("bench") || n.Contains("vending") || n.Contains("shop") || 
                    n.Contains("console") || n.Contains("trash") || n.Contains("recycle"))
                {
                    if (NavMesh.SamplePosition(go.transform.position, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
                    {
                        anchors.Add(hit.position);
                    }
                }
            }

            // 2. Expand clusters along NavMesh to form continuous sidewalk walkways
            HashSet<int> visited = new HashSet<int>();
            for (int i = 0; i < anchors.Count; i++)
            {
                Vector3 anchor = anchors[i];
                if (!IsNearExistingSidewalkNode(anchor, nodeSampleSpacing * 0.75f))
                {
                    _sidewalkNodes.Add(anchor);
                }

                // Sample in 4 cardinal directions around anchor to trace the sidewalk line
                Vector3[] offsets = { Vector3.forward * nodeSampleSpacing, Vector3.back * nodeSampleSpacing, 
                                      Vector3.left * nodeSampleSpacing, Vector3.right * nodeSampleSpacing };
                foreach (var off in offsets)
                {
                    Vector3 testPos = anchor + off;
                    if (NavMesh.SamplePosition(testPos, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
                    {
                        if (!IsNearExistingSidewalkNode(hit.position, nodeSampleSpacing * 0.75f))
                        {
                            _sidewalkNodes.Add(hit.position);
                        }
                    }
                }
            }

            // 3. Generate crosswalk connectors between opposing sidewalk clusters separated by roadway
            GenerateCrosswalksAcrossStreets();

            Debug.Log($"[CyberCitySidewalkManager] Baked {_sidewalkNodes.Count} sidewalk corridor nodes and {crosswalks.Count} crosswalk zones.");
        }

        private bool IsNearExistingSidewalkNode(Vector3 pos, float minDist)
        {
            float sqrMin = minDist * minDist;
            for (int i = 0; i < _sidewalkNodes.Count; i++)
            {
                if ((_sidewalkNodes[i] - pos).sqrMagnitude < sqrMin) return true;
            }
            return false;
        }

        private void GenerateCrosswalksAcrossStreets()
        {
            crosswalks.Clear();
            // Connect sidewalk nodes separated by 8m-18m across potential roadway gap
            for (int i = 0; i < _sidewalkNodes.Count; i++)
            {
                Vector3 pA = _sidewalkNodes[i];
                for (int j = i + 1; j < _sidewalkNodes.Count; j++)
                {
                    Vector3 pB = _sidewalkNodes[j];
                    float dist = Vector3.Distance(pA, pB);
                    float yDiff = Mathf.Abs(pA.y - pB.y);

                    if (dist >= 9.0f && dist <= 16.0f && yDiff < 1.0f)
                    {
                        // Check if direct midpoint is also on NavMesh (the road connecting them)
                        Vector3 mid = (pA + pB) * 0.5f;
                        if (NavMesh.SamplePosition(mid, out NavMeshHit midHit, 2.0f, NavMesh.AllAreas))
                        {
                            // Avoid duplicate near crosswalks
                            bool exists = false;
                            foreach (var cw in crosswalks)
                            {
                                if (Vector3.Distance(cw.curbPointA, pA) < 10.0f) { exists = true; break; }
                            }

                            if (!exists && crosswalks.Count < 12)
                            {
                                crosswalks.Add(new CrosswalkZone
                                {
                                    name = $"Crosswalk_{crosswalks.Count + 1}",
                                    curbPointA = pA,
                                    curbPointB = pB,
                                    width = 3.5f
                                });
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Selects a smart sidewalk destination within the wander radius.
        /// Prioritizes staying along pedestrian sidewalks and plaza zones.
        /// </summary>
        public bool TryGetSidewalkDestination(Vector3 currentPos, float wanderRadius, out Vector3 destination)
        {
            destination = currentPos;
            if (_sidewalkNodes.Count == 0) return false;

            List<Vector3> candidates = new List<Vector3>();
            for (int i = 0; i < _sidewalkNodes.Count; i++)
            {
                float d = Vector3.Distance(currentPos, _sidewalkNodes[i]);
                if (d >= 5.0f && d <= wanderRadius)
                {
                    candidates.Add(_sidewalkNodes[i]);
                }
            }

            if (candidates.Count > 0)
            {
                destination = candidates[Random.Range(0, candidates.Count)];
                // Small jitter within sidewalk corridor width
                destination += new Vector3(Random.Range(-0.8f, 0.8f), 0f, Random.Range(-0.8f, 0.8f));
                if (NavMesh.SamplePosition(destination, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
                {
                    destination = hit.position;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Finds the closest crosswalk connecting towards the destination if across a roadway.
        /// </summary>
        public bool TryGetNearestCrosswalk(Vector3 currentPos, Vector3 targetPos, out Vector3 crosswalkWaypoint)
        {
            crosswalkWaypoint = currentPos;
            if (crosswalks.Count == 0) return false;

            CrosswalkZone bestCw = default;
            float bestDist = float.MaxValue;
            bool found = false;

            for (int i = 0; i < crosswalks.Count; i++)
            {
                var cw = crosswalks[i];
                float dA = Vector3.Distance(currentPos, cw.curbPointA);
                float dB = Vector3.Distance(currentPos, cw.curbPointB);
                float curMin = Mathf.Min(dA, dB);

                // If crosswalk is along general direction of target
                Vector3 toTarget = (targetPos - currentPos).normalized;
                Vector3 toCw = ((dA < dB ? cw.curbPointA : cw.curbPointB) - currentPos).normalized;

                if (Vector3.Dot(toTarget, toCw) > 0.4f && curMin < bestDist && curMin < 25.0f)
                {
                    bestDist = curMin;
                    bestCw = cw;
                    found = true;
                }
            }

            if (found)
            {
                float dA = Vector3.Distance(currentPos, bestCw.curbPointA);
                float dB = Vector3.Distance(currentPos, bestCw.curbPointB);
                crosswalkWaypoint = (dA < dB) ? bestCw.curbPointA : bestCw.curbPointB;
                return true;
            }

            return false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.6f);
            foreach (var node in _sidewalkNodes)
            {
                Gizmos.DrawSphere(node + Vector3.up * 0.15f, 0.35f);
            }

            Gizmos.color = new Color(1f, 0.8f, 0.1f, 0.9f);
            foreach (var cw in crosswalks)
            {
                Gizmos.DrawLine(cw.curbPointA + Vector3.up * 0.2f, cw.curbPointB + Vector3.up * 0.2f);
                Gizmos.DrawWireCube((cw.curbPointA + cw.curbPointB) * 0.5f + Vector3.up * 0.1f, new Vector3(cw.width, 0.2f, Vector3.Distance(cw.curbPointA, cw.curbPointB)));
            }
        }
    }
}
