using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace PolygonCyberCity.NPC.Spawner
{
    /// <summary>
    /// AAA-grade Dynamic NPC Spawner & Population Streaming System for CyberCity.
    /// Strictly guarantees NPCs spawn ONLY in explorable street & sidewalk areas connected to the player:
    /// 1. Strict vertical elevation filtering (rejects rooftops, bridges, and subway pits).
    /// 2. Mandatory NavMesh PathComplete verification (rejects isolated courtyards, blocked alleys, and unreachable zones).
    /// 3. Dynamic Explorable Node Cache pre-sampled across walkable streets.
    /// 4. Physics capsule clearance (no spawning inside walls, dumpsters, or vehicles).
    /// 5. Immediate pre-seeding on start for an active, living cyberpunk city.
    /// </summary>
    public class CyberNPCSpawner : MonoBehaviour
    {
        [Header("Player & Target Tracking")]
        [Tooltip("Player Transform. If unassigned, automatically located by tag or type.")]
        public Transform playerTransform;
        public Camera playerCamera;

        [Header("Population Density")]
        [Range(5, 60)] public int maxActiveNPCs = 30;
        public float spawnInterval = 1.0f;
        [Tooltip("Initial number of NPCs pre-seeded across explorable streets on start.")]
        [Range(0, 30)] public int initialSpawnCount = 18;

        [Header("Explorable Area & Spatial Streaming")]
        [Tooltip("Minimum distance from player to spawn dynamically (prevents popping in player view).")]
        public float minSpawnRadius = 12f;
        [Tooltip("Maximum distance from player to spawn (keeps population dense on current street).")]
        public float maxSpawnRadius = 36f;
        [Tooltip("Distance at which distant NPCs are recycled back to pool.")]
        public float despawnRadius = 48f;
        [Tooltip("Maximum vertical delta from player ground elevation. Rejects roofs, elevated monorails, and subterranean pits.")]
        public float maxVerticalElevation = 2.5f;

        [Header("Archetype Weights")]
        [Range(0, 100)] public int civilianWeight = 50;
        [Range(0, 100)] public int cyborgWeight = 20;
        [Range(0, 100)] public int richCitizenWeight = 10;
        [Range(0, 100)] public int mercenaryWeight = 15;
        [Range(0, 100)] public int helperBotWeight = 5;

        [Header("Layer Masks")]
        public LayerMask groundLayerMask = ~0;
        public LayerMask obstacleLayerMask = ~0;

        private readonly List<CyberNPCController> _activeNPCs = new List<CyberNPCController>();
        private readonly List<Vector3> _explorableStreetNodes = new List<Vector3>();
        private NavMeshPath _pathBuffer;
        private Vector3 _lastCacheCenter;
        private float _spawnTimer = 0f;
        private int _totalWeight = 100;

        public int ActiveNPCCount => _activeNPCs.Count;

        private void Awake()
        {
            _pathBuffer = new NavMeshPath();
        }

        private void Start()
        {
            FindPlayerReferences();
            CalculateTotalWeights();

            if (playerTransform != null)
            {
                RefreshExplorableNodeCache(playerTransform.position);
                PreSeedInitialPopulation();
            }
        }

        private void FindPlayerReferences()
        {
            if (playerTransform == null)
            {
                var playerGo = GameObject.FindWithTag("Player");
                if (playerGo != null)
                {
                    playerTransform = playerGo.transform;
                }
            }

            if (playerCamera == null)
            {
                playerCamera = Camera.main;
            }
        }

        private void CalculateTotalWeights()
        {
            _totalWeight = civilianWeight + cyborgWeight + richCitizenWeight + mercenaryWeight + helperBotWeight;
            if (_totalWeight <= 0) _totalWeight = 100;
        }

        /// <summary>
        /// Scans a grid around the player on the street elevation and collects verified explorable NavMesh nodes.
        /// </summary>
        public void RefreshExplorableNodeCache(Vector3 center)
        {
            _lastCacheCenter = center;
            _explorableStreetNodes.Clear();

            float gridStep = 4.0f;
            float scanExtent = maxSpawnRadius + 10f;

            for (float x = -scanExtent; x <= scanExtent; x += gridStep)
            {
                for (float z = -scanExtent; z <= scanExtent; z += gridStep)
                {
                    Vector3 samplePoint = new Vector3(center.x + x, center.y, center.z + z);

                    if (NavMesh.SamplePosition(samplePoint, out NavMeshHit hit, 2.5f, NavMesh.AllAreas))
                    {
                        // 1. Strict vertical elevation window (only street level)
                        if (Mathf.Abs(hit.position.y - center.y) <= maxVerticalElevation)
                        {
                            // 2. Strict continuous reachability to player
                            if (NavMesh.CalculatePath(center, hit.position, NavMesh.AllAreas, _pathBuffer) &&
                                _pathBuffer.status == NavMeshPathStatus.PathComplete)
                            {
                                // 3. Clearance check (not inside buildings/walls)
                                if (!Physics.CheckCapsule(hit.position + Vector3.up * 0.4f, hit.position + Vector3.up * 1.5f, 0.30f, obstacleLayerMask, QueryTriggerInteraction.Ignore))
                                {
                                    // Avoid clustering too close
                                    bool isDuplicate = false;
                                    for (int i = 0; i < _explorableStreetNodes.Count; i++)
                                    {
                                        if (Vector3.Distance(_explorableStreetNodes[i], hit.position) < 2.5f)
                                        {
                                            isDuplicate = true;
                                            break;
                                        }
                                    }

                                    if (!isDuplicate)
                                    {
                                        _explorableStreetNodes.Add(hit.position);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Incorporate verified sidewalk nodes from CyberCitySidewalkManager
            if (CyberCitySidewalkManager.Instance != null)
            {
                var swNodes = CyberCitySidewalkManager.Instance.SidewalkNodes;
                for (int i = 0; i < swNodes.Count; i++)
                {
                    Vector3 pt = swNodes[i];
                    if (Mathf.Abs(pt.y - center.y) <= maxVerticalElevation && Vector3.Distance(center, pt) <= scanExtent)
                    {
                        if (!_explorableStreetNodes.Contains(pt))
                        {
                            _explorableStreetNodes.Add(pt);
                        }
                    }
                }
            }
        }

        private void PreSeedInitialPopulation()
        {
            if (CyberNPCPool.Instance == null || _explorableStreetNodes.Count == 0) return;

            int targetCount = Mathf.Min(initialSpawnCount, maxActiveNPCs);
            List<Vector3> availableNodes = new List<Vector3>(_explorableStreetNodes);

            // Shuffle available nodes
            for (int i = availableNodes.Count - 1; i > 0; i--)
            {
                int r = Random.Range(0, i + 1);
                var temp = availableNodes[i];
                availableNodes[i] = availableNodes[r];
                availableNodes[r] = temp;
            }

            int spawned = 0;
            for (int i = 0; i < availableNodes.Count && spawned < targetCount; i++)
            {
                Vector3 pos = availableNodes[i];
                float dist = Vector3.Distance(playerTransform.position, pos);

                // Pre-seed can spawn closer to player (e.g. 6m - 35m) so the immediate street has life
                if (dist >= 6f && dist <= maxSpawnRadius)
                {
                    NPCType typeToSpawn = SelectRandomArchetype();
                    Quaternion rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                    var npc = CyberNPCPool.Instance.GetFromPool(typeToSpawn, pos, rot, playerTransform);
                    if (npc != null)
                    {
                        _activeNPCs.Add(npc);
                        spawned++;
                    }
                }
            }
        }

        private void Update()
        {
            if (playerTransform == null)
            {
                FindPlayerReferences();
                return;
            }

            // If player moved significantly, refresh the local explorable street cache
            if (Vector3.Distance(playerTransform.position, _lastCacheCenter) > 16f)
            {
                RefreshExplorableNodeCache(playerTransform.position);
            }

            // Despawn distant or non-explorable NPCs
            EvaluateDespawns();

            // Dynamic Spawn check
            _spawnTimer += Time.deltaTime;
            if (_spawnTimer >= spawnInterval)
            {
                _spawnTimer = 0f;
                if (_activeNPCs.Count < maxActiveNPCs)
                {
                    TrySpawnNPC();
                }
            }
        }

        private void EvaluateDespawns()
        {
            for (int i = _activeNPCs.Count - 1; i >= 0; i--)
            {
                var npc = _activeNPCs[i];
                if (npc == null || !npc.gameObject.activeInHierarchy || npc.IsDead)
                {
                    _activeNPCs.RemoveAt(i);
                    continue;
                }

                float dist = Vector3.Distance(playerTransform.position, npc.transform.position);
                float yDiff = Mathf.Abs(playerTransform.position.y - npc.transform.position.y);

                // Despawn if beyond radius, or if NPC fell into an unreachable elevation/pit
                if (dist > despawnRadius || yDiff > maxVerticalElevation * 2.2f)
                {
                    _activeNPCs.RemoveAt(i);
                    CyberNPCPool.Instance.ReturnToPool(npc);
                }
            }
        }

        private void TrySpawnNPC()
        {
            if (CyberNPCPool.Instance == null) return;

            if (FindValidSpawnPosition(out Vector3 spawnPos, out Quaternion spawnRot))
            {
                NPCType typeToSpawn = SelectRandomArchetype();
                var npc = CyberNPCPool.Instance.GetFromPool(typeToSpawn, spawnPos, spawnRot, playerTransform);
                if (npc != null)
                {
                    _activeNPCs.Add(npc);
                }
            }
        }

        private bool FindValidSpawnPosition(out Vector3 validPosition, out Quaternion validRotation)
        {
            validPosition = Vector3.zero;
            validRotation = Quaternion.identity;

            // Strategy 1: Pick from verified Explorable Street Nodes within the spawn donut
            if (_explorableStreetNodes.Count > 0)
            {
                List<Vector3> eligibleNodes = new List<Vector3>();
                for (int i = 0; i < _explorableStreetNodes.Count; i++)
                {
                    float d = Vector3.Distance(playerTransform.position, _explorableStreetNodes[i]);
                    if (d >= minSpawnRadius && d <= maxSpawnRadius)
                    {
                        eligibleNodes.Add(_explorableStreetNodes[i]);
                    }
                }

                if (eligibleNodes.Count > 0)
                {
                    // Shuffle candidate check
                    for (int attempt = 0; attempt < 8 && eligibleNodes.Count > 0; attempt++)
                    {
                        int idx = Random.Range(0, eligibleNodes.Count);
                        Vector3 candidatePos = eligibleNodes[idx];
                        eligibleNodes.RemoveAt(idx);

                        // Anti-popping frustum check
                        if (!IsOccludedOrOutsideCenterFocalView(candidatePos))
                        {
                            continue;
                        }

                        validPosition = candidatePos;
                        validRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                        return true;
                    }
                }
            }

            // Strategy 2: Dynamic Raycast Sampling strictly at street level with full path validation
            for (int attempt = 0; attempt < 12; attempt++)
            {
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float distance = Random.Range(minSpawnRadius, maxSpawnRadius);

                Vector3 candidate = playerTransform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
                
                // Scan downward starting at eye level (1.8m above player ground) to avoid rooftops/overpasses
                Vector3 rayStart = new Vector3(candidate.x, playerTransform.position.y + 1.8f, candidate.z);

                if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 4.5f, groundLayerMask))
                {
                    // Validate on NavMesh
                    if (NavMesh.SamplePosition(hit.point, out NavMeshHit navHit, 2.0f, NavMesh.AllAreas))
                    {
                        Vector3 finalPoint = navHit.position;

                        // 1. Strict vertical elevation check
                        if (Mathf.Abs(finalPoint.y - playerTransform.position.y) > maxVerticalElevation)
                        {
                            continue;
                        }

                        // 2. Strict NavMesh PathComplete reachability
                        if (!NavMesh.CalculatePath(playerTransform.position, finalPoint, NavMesh.AllAreas, _pathBuffer) ||
                            _pathBuffer.status != NavMeshPathStatus.PathComplete)
                        {
                            continue;
                        }

                        // 3. Physical capsule clearance
                        if (Physics.CheckCapsule(finalPoint + Vector3.up * 0.4f, finalPoint + Vector3.up * 1.5f, 0.30f, obstacleLayerMask, QueryTriggerInteraction.Ignore))
                        {
                            continue;
                        }

                        // 4. Anti-popping camera check
                        if (!IsOccludedOrOutsideCenterFocalView(finalPoint))
                        {
                            continue;
                        }

                        validPosition = finalPoint;
                        validRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                        return true;
                    }
                }
            }

            return false;
        }

        private bool IsOccludedOrOutsideCenterFocalView(Vector3 worldPoint)
        {
            if (playerCamera == null) return true;

            Vector3 viewPos = playerCamera.WorldToViewportPoint(worldPoint);
            bool inCenterFrustum = viewPos.z > 0 && viewPos.x >= 0.2f && viewPos.x <= 0.8f && viewPos.y >= 0.2f && viewPos.y <= 0.8f;
            float distToCam = Vector3.Distance(playerCamera.transform.position, worldPoint);

            if (inCenterFrustum && distToCam < 24f)
            {
                // Check if occluded by buildings/environment
                Vector3 camToPoint = worldPoint - playerCamera.transform.position;
                int envMask = LayerMask.GetMask("Default", "Static", "Environment", "Building");
                if (envMask == 0) envMask = groundLayerMask;
                if (!Physics.Raycast(playerCamera.transform.position, camToPoint.normalized, camToPoint.magnitude - 0.5f, envMask))
                {
                    // Direct line of sight in center viewport: discard to prevent visible popping
                    return false;
                }
            }

            return true;
        }

        private NPCType SelectRandomArchetype()
        {
            int roll = Random.Range(0, _totalWeight);
            int cumulative = civilianWeight;
            if (roll < cumulative) return NPCType.Civilian;

            cumulative += cyborgWeight;
            if (roll < cumulative) return NPCType.Cyborg;

            cumulative += richCitizenWeight;
            if (roll < cumulative) return NPCType.RichCitizen;

            cumulative += mercenaryWeight;
            if (roll < cumulative) return NPCType.Mercenary;

            return NPCType.HelperBot;
        }

        private void OnDrawGizmosSelected()
        {
            if (playerTransform == null) return;

            Gizmos.color = new Color(0f, 1f, 0.4f, 0.25f);
            Gizmos.DrawWireSphere(playerTransform.position, minSpawnRadius);

            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.35f);
            Gizmos.DrawWireSphere(playerTransform.position, maxSpawnRadius);

            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.25f);
            Gizmos.DrawWireSphere(playerTransform.position, despawnRadius);

            // Draw explorable street nodes
            Gizmos.color = new Color(0f, 1f, 0.8f, 0.7f);
            foreach (var node in _explorableStreetNodes)
            {
                Gizmos.DrawSphere(node + Vector3.up * 0.2f, 0.35f);
            }
        }
    }
}
