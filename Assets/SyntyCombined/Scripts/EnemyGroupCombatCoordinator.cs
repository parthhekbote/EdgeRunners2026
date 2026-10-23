using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace CyberCombat.Coordination
{
    /// <summary>
    /// Interface implemented by enemy AI controllers participating in group combat coordination.
    /// Supports InsaneGunSwordController, SwordEnemyAI, and any future enemy types.
    /// </summary>
    public interface IGroupCombatant
    {
        Transform CombatantTransform { get; }
        Transform CombatTarget { get; }
        bool IsCombatActive { get; }
        bool IsCurrentlyAttacking { get; }
        void OnAttackTokenGranted();
        void OnAttackTokenRevoked();
    }

    /// <summary>
    /// Master Coordinator for Multi-Enemy Combat Encounters.
    ///
    /// Core Behaviors:
    /// 1. SURROUND FIRST: When 2 or more enemies engage a target (e.g. Player), they do NOT
    ///    rush in simultaneously. Instead, they fan out into a surrounding ring formation around
    ///    the target (e.g., 2 enemies = 180° pincer, 3 enemies = 120° triangle, 4 enemies = 90° box).
    ///    Attacks are suppressed until the initial encirclement is established.
    /// 2. ATTACK NOT AT ONCE (COORDINATED CADENCE): Only ONE enemy holds the active Attack Token
    ///    at any given time. Non-token holders maintain their surrounding perimeter, pacing and
    ///    menacing the player while keeping guard.
    /// 3. CADENCE INTERVAL: When the attacking enemy finishes their strike, the token is released
    ///    and a tactical cadence pause occurs before the next surrounding enemy takes their turn.
    /// 4. 1v1 DUEL FALLBACK: When only 1 enemy remains, group coordination automatically disengages
    ///    and the survivor fights aggressively with standard 1v1 duel logic.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class EnemyGroupCombatCoordinator : MonoBehaviour
    {
        private static bool _isShuttingDown = false;
        private static EnemyGroupCombatCoordinator _instance;
        public static EnemyGroupCombatCoordinator Instance
        {
            get
            {
                if (_isShuttingDown) return _instance;
                if (_instance == null)
                {
                    _instance = Object.FindAnyObjectByType<EnemyGroupCombatCoordinator>();
                    if (_instance == null && Application.isPlaying)
                    {
                        GameObject go = new GameObject("[EnemyGroupCombatCoordinator]");
                        _instance = go.AddComponent<EnemyGroupCombatCoordinator>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }

        private void OnApplicationQuit()
        {
            _isShuttingDown = true;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        [Header("Formation Configuration")]
        [Tooltip("Ideal radial distance from target for surrounding enemies (meters).")]
        [Range(3.0f, 15.0f)]
        public float surroundRadius = 6.8f;

        [Tooltip("Distance threshold to consider an enemy arrived in their surrounding slot (meters).")]
        [Range(0.5f, 3.5f)]
        public float surroundArrivalTolerance = 1.4f;

        [Tooltip("Maximum seconds to wait for initial surround formation before allowing first attack.")]
        [Range(1.0f, 6.0f)]
        public float initialSurroundTimeout = 2.4f;

        [Tooltip("Speed at which the surrounding ring smoothly rotates with player movement (deg/sec).")]
        [Range(10f, 180f)]
        public float ringRotationFollowSpeed = 60f;

        [Header("Coordinated Attack Sequencing")]
        [Tooltip("Minimum cadence interval between consecutive enemy attacks (seconds). Gives player reaction window.")]
        [Range(0.4f, 3.0f)]
        public float attackCadenceDelay = 1.10f;

        [Tooltip("Safety timeout after which an attack token is forcibly revoked if attacker hangs.")]
        [Range(2.0f, 8.0f)]
        public float maxAttackDuration = 4.5f;

        [Header("Toggles & Debug")]
        public bool enableSurroundFormation = true;
        public bool enableCoordinatedAttacks = true;
        public bool verboseLogging = false;
        public bool showDebugGizmos = true;

        /// <summary>
        /// Slot tracking for an individual combatant inside a group.
        /// </summary>
        public class CombatantSlot
        {
            public IGroupCombatant combatant;
            public int slotIndex;
            public float slotAngle;
            public Vector3 worldPosition;
            public bool isInPosition;
            public float lastAttackTime;

            public CombatantSlot(IGroupCombatant c)
            {
                combatant = c;
                slotIndex = -1;
                slotAngle = 0f;
                worldPosition = Vector3.zero;
                isInPosition = false;
                lastAttackTime = -100f;
            }
        }

        /// <summary>
        /// Manages the surrounding ring and attack token for all enemies targeting a specific target (Player).
        /// </summary>
        public class TargetCombatGroup
        {
            public Transform target;
            public List<CombatantSlot> slots = new List<CombatantSlot>();
            public IGroupCombatant currentAttacker = null;
            public float tokenGrantTime = -100f;
            public float lastAttackFinishTime = -100f;
            public bool isInitialSurroundComplete = false;
            public float groupFormationTime = -100f;
            public float smoothedRingAngle = 0f;
            public int lastAttackerSlotIndex = -1;

            public TargetCombatGroup(Transform targetTransform)
            {
                target = targetTransform;
                smoothedRingAngle = target != null ? target.eulerAngles.y : 0f;
                groupFormationTime = Time.time;
                lastAttackFinishTime = Time.time;
            }

            public int ActiveCount => slots.Count(s => s.combatant != null && s.combatant.IsCombatActive);
        }

        private readonly Dictionary<Transform, TargetCombatGroup> _groups = new Dictionary<Transform, TargetCombatGroup>();
        private readonly List<Transform> _groupKeysToRemove = new List<Transform>();

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                if (Application.isPlaying && transform.parent == null)
                {
                    DontDestroyOnLoad(gameObject);
                }
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            UpdateAllGroups();
        }

        // ══════════════════════════════════════════════════════════════════════════
        // REGISTRATION & MEMBERSHIP API
        // ══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Registers a combatant to participate in group surrounding and coordinated attacks against a target.
        /// </summary>
        public void RegisterCombatant(IGroupCombatant combatant, Transform target)
        {
            if (combatant == null || target == null) return;

            if (!_groups.TryGetValue(target, out var group))
            {
                group = new TargetCombatGroup(target);
                _groups[target] = group;
            }

            if (!group.slots.Any(s => s.combatant == combatant))
            {
                var newSlot = new CombatantSlot(combatant);
                group.slots.Add(newSlot);

                // If group just reached 2+ combatants, reset initial surround state
                if (group.slots.Count >= 2 && !group.isInitialSurroundComplete)
                {
                    group.groupFormationTime = Time.time;
                    group.isInitialSurroundComplete = false;
                    group.lastAttackFinishTime = Time.time;
                    if (verboseLogging)
                    {
                        Debug.Log($"[EnemyGroupCombatCoordinator] Target {target.name} engaged by {group.slots.Count} enemies! Initiating SURROUND FIRST formation.");
                    }
                }
            }
        }

        /// <summary>
        /// Unregisters a combatant upon death, de-aggro, or disabling.
        /// </summary>
        public void UnregisterCombatant(IGroupCombatant combatant)
        {
            if (combatant == null) return;

            foreach (var kvp in _groups)
            {
                var group = kvp.Value;
                int idx = group.slots.FindIndex(s => s.combatant == combatant);
                if (idx >= 0)
                {
                    if (group.currentAttacker == combatant)
                    {
                        group.currentAttacker = null;
                        group.lastAttackFinishTime = Time.time;
                        combatant.OnAttackTokenRevoked();
                    }
                    group.slots.RemoveAt(idx);

                    // If reduced to 1 combatant, disengage group surround mode
                    if (group.slots.Count < 2)
                    {
                        group.isInitialSurroundComplete = false;
                        if (group.currentAttacker != null)
                        {
                            group.currentAttacker.OnAttackTokenRevoked();
                            group.currentAttacker = null;
                        }
                    }
                    break;
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════════════
        // QUERY API FOR COMBATANTS
        // ══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Returns true if 2 or more active enemies are currently surrounding the given target.
        /// </summary>
        public bool IsGroupSurroundActive(Transform target)
        {
            if (!enableSurroundFormation || target == null) return false;
            if (_groups.TryGetValue(target, out var group))
            {
                return group.ActiveCount >= 2;
            }
            return false;
        }

        /// <summary>
        /// Returns true if the group has finished their initial surrounding maneuver.
        /// </summary>
        public bool IsInitialSurroundComplete(Transform target)
        {
            if (target == null) return true;
            if (_groups.TryGetValue(target, out var group))
            {
                if (group.ActiveCount < 2) return true;
                return group.isInitialSurroundComplete;
            }
            return true;
        }

        /// <summary>
        /// Checks if this combatant is currently authorized to attack the target.
        /// Returns true if 1v1 (solo enemy) or if holding the coordinated attack token.
        /// </summary>
        public bool HasAttackToken(IGroupCombatant combatant)
        {
            if (combatant == null) return false;
            var target = combatant.CombatTarget;
            if (target == null) return true;

            if (!enableCoordinatedAttacks) return true;

            if (_groups.TryGetValue(target, out var group))
            {
                if (group.ActiveCount < 2) return true; // Solo enemy: 1v1 duel mode
                return group.currentAttacker == combatant;
            }

            return true;
        }

        /// <summary>
        /// Requests permission to initiate an attack. Returns true if token is granted.
        /// </summary>
        public bool RequestAttackToken(IGroupCombatant combatant)
        {
            if (combatant == null) return false;
            var target = combatant.CombatTarget;
            if (target == null) return true;

            if (!enableCoordinatedAttacks) return true;

            if (_groups.TryGetValue(target, out var group))
            {
                if (group.ActiveCount < 2) return true;

                // Cannot attack during initial surround formation
                if (!group.isInitialSurroundComplete) return false;

                // If already holding token
                if (group.currentAttacker == combatant) return true;

                // If token is free and cadence delay elapsed, grant it!
                if (group.currentAttacker == null && Time.time >= group.lastAttackFinishTime + attackCadenceDelay)
                {
                    GrantAttackToken(group, combatant);
                    return true;
                }

                return false;
            }

            return true;
        }

        /// <summary>
        /// Notifies the coordinator that this combatant has physically initiated their strike/combo.
        /// </summary>
        public void NotifyAttackStarted(IGroupCombatant combatant)
        {
            if (combatant == null) return;
            var target = combatant.CombatTarget;
            if (target == null) return;

            if (_groups.TryGetValue(target, out var group))
            {
                if (group.currentAttacker == null)
                {
                    group.currentAttacker = combatant;
                    group.tokenGrantTime = Time.time;
                }
            }
        }

        /// <summary>
        /// Notifies the coordinator that this combatant has finished their attack or combo,
        /// or was interrupted/parried/staggered. Releases token and begins cadence delay.
        /// </summary>
        public void NotifyAttackFinished(IGroupCombatant combatant)
        {
            if (combatant == null) return;
            var target = combatant.CombatTarget;
            if (target == null) return;

            if (_groups.TryGetValue(target, out var group))
            {
                if (group.currentAttacker == combatant)
                {
                    if (verboseLogging)
                    {
                        Debug.Log($"[EnemyGroupCombatCoordinator] Attack finished by {combatant.CombatantTransform.name}. Releasing token. Cadence delay: {attackCadenceDelay:F2}s");
                    }
                    group.currentAttacker = null;
                    group.lastAttackFinishTime = Time.time;
                    combatant.OnAttackTokenRevoked();
                }
            }
        }

        /// <summary>
        /// Returns the exact 3D world position where this combatant should stand to maintain the encirclement.
        /// </summary>
        public Vector3 GetSurroundSlotPosition(IGroupCombatant combatant, Transform target, float desiredRadius = -1f)
        {
            if (combatant == null || target == null)
            {
                return combatant != null ? combatant.CombatantTransform.position : Vector3.zero;
            }

            if (_groups.TryGetValue(target, out var group))
            {
                var slot = group.slots.FirstOrDefault(s => s.combatant == combatant);
                if (slot != null && slot.slotIndex >= 0)
                {
                    return slot.worldPosition;
                }
            }

            // Fallback: simple radial offset based on current angle
            float r = desiredRadius > 0f ? desiredRadius : surroundRadius;
            Vector3 diff = combatant.CombatantTransform.position - target.position;
            diff.y = 0f;
            Vector3 dir = diff.sqrMagnitude > 0.01f ? diff.normalized : target.forward;
            return target.position + dir * r;
        }

        /// <summary>
        /// Returns total active enemies targeting this target.
        /// </summary>
        public int GetActiveEnemyCount(Transform target)
        {
            if (target != null && _groups.TryGetValue(target, out var group))
            {
                return group.ActiveCount;
            }
            return 0;
        }

        // ══════════════════════════════════════════════════════════════════════════
        // GROUP UPDATE & SLOTS CALCULATION
        // ══════════════════════════════════════════════════════════════════════════

        private void UpdateAllGroups()
        {
            _groupKeysToRemove.Clear();

            foreach (var kvp in _groups)
            {
                var target = kvp.Key;
                var group = kvp.Value;

                if (target == null || !target.gameObject.activeInHierarchy)
                {
                    _groupKeysToRemove.Add(target);
                    continue;
                }

                // 1. Clean dead/inactive slots
                group.slots.RemoveAll(s => s.combatant == null || !s.combatant.IsCombatActive);

                int activeN = group.slots.Count;
                if (activeN < 2)
                {
                    // 1v1 mode
                    group.isInitialSurroundComplete = false;
                    if (group.currentAttacker != null)
                    {
                        group.currentAttacker.OnAttackTokenRevoked();
                        group.currentAttacker = null;
                    }
                    continue;
                }

                // 2. Smooth ring orientation around target
                Vector3 targetForward = target.forward;
                targetForward.y = 0f;
                if (targetForward.sqrMagnitude < 0.01f) targetForward = Vector3.forward;
                targetForward.Normalize();

                float currentTargetAngle = Mathf.Atan2(targetForward.x, targetForward.z) * Mathf.Rad2Deg;
                group.smoothedRingAngle = Mathf.MoveTowardsAngle(
                    group.smoothedRingAngle,
                    currentTargetAngle,
                    ringRotationFollowSpeed * Time.deltaTime
                );

                // 3. Calculate N equidistant surrounding slot positions
                float stepAngle = 360f / activeN;
                Vector3 targetPos = target.position;

                List<Vector3> slotPositions = new List<Vector3>(activeN);
                List<float> slotAngles = new List<float>(activeN);

                for (int i = 0; i < activeN; i++)
                {
                    float angle = Mathf.Repeat(group.smoothedRingAngle + i * stepAngle, 360f);
                    Vector3 offset = Quaternion.Euler(0f, angle, 0f) * (Vector3.forward * surroundRadius);
                    Vector3 slotPos = targetPos + offset;

                    // LayerMask filtering out Player (8), Enemy (9), and Ignore Raycast (2)
                    int obstacleMask = ~((1 << 8) | (1 << 9) | (1 << 2));

                    // Obstacle check: SphereCast outward starting 1.0m from player to avoid clipping player collider
                    Vector3 rayOrigin = targetPos + Vector3.up * 0.9f + offset.normalized * 1.0f;
                    float castDist = Mathf.Max(0.5f, surroundRadius - 1.0f);
                    if (Physics.SphereCast(rayOrigin, 0.35f, offset.normalized, out RaycastHit hit, castDist, obstacleMask, QueryTriggerInteraction.Ignore))
                    {
                        float safeDist = Mathf.Max(4.2f, 1.0f + hit.distance - 0.5f);
                        slotPos = targetPos + offset.normalized * safeDist;
                    }

                    // Snap slot position to ground
                    if (Physics.Raycast(slotPos + Vector3.up * 1.5f, Vector3.down, out RaycastHit groundHit, 4.0f, obstacleMask, QueryTriggerInteraction.Ignore))
                    {
                        slotPos.y = groundHit.point.y;
                    }

                    // Sample NavMesh if available
                    if (NavMesh.SamplePosition(slotPos, out NavMeshHit navHit, 1.5f, NavMesh.AllAreas))
                    {
                        slotPos = navHit.position;
                    }

                    slotPositions.Add(slotPos);
                    slotAngles.Add(angle);
                }

                // 4. Stable Slot Assignment (Minimizing travel distance and crossing)
                AssignCombatantsToSlots(group, slotPositions, slotAngles);

                // 5. Evaluate Initial Surround Formation
                EvaluateInitialSurround(group);

                // 6. Coordinated Attack Token Scheduling
                UpdateAttackTokenScheduling(group);
            }

            foreach (var key in _groupKeysToRemove)
            {
                _groups.Remove(key);
            }
        }

        private void AssignCombatantsToSlots(TargetCombatGroup group, List<Vector3> slotPositions, List<float> slotAngles)
        {
            int n = group.slots.Count;
            Vector3 targetPos = group.target.position;

            // Calculate current bearing angle from target for each combatant
            for (int i = 0; i < n; i++)
            {
                var s = group.slots[i];
                Vector3 toCombatant = s.combatant.CombatantTransform.position - targetPos;
                toCombatant.y = 0f;
                float bearing = Mathf.Atan2(toCombatant.x, toCombatant.z) * Mathf.Rad2Deg;
                bearing = Mathf.Repeat(bearing, 360f);
                s.slotAngle = bearing;
            }

            // Greedy optimal matching: match each slot to nearest combatant
            bool[] slotClaimed = new bool[n];
            bool[] combatantMatched = new bool[n];

            // First pass: preserve existing slot assignments if combatant is still reasonably close
            for (int i = 0; i < n; i++)
            {
                var s = group.slots[i];
                if (s.slotIndex >= 0 && s.slotIndex < n && !slotClaimed[s.slotIndex])
                {
                    float angleDiff = Mathf.Abs(Mathf.DeltaAngle(s.slotAngle, slotAngles[s.slotIndex]));
                    if (angleDiff < (360f / n) * 0.70f)
                    {
                        slotClaimed[s.slotIndex] = true;
                        combatantMatched[i] = true;
                        s.worldPosition = slotPositions[s.slotIndex];
                    }
                }
            }

            // Second pass: assign remaining combatants to best available slots
            for (int i = 0; i < n; i++)
            {
                if (combatantMatched[i]) continue;
                var s = group.slots[i];

                int bestSlot = -1;
                float bestAngleDiff = 9999f;

                for (int slot = 0; slot < n; slot++)
                {
                    if (slotClaimed[slot]) continue;
                    float diff = Mathf.Abs(Mathf.DeltaAngle(s.slotAngle, slotAngles[slot]));
                    if (diff < bestAngleDiff)
                    {
                        bestAngleDiff = diff;
                        bestSlot = slot;
                    }
                }

                if (bestSlot >= 0)
                {
                    s.slotIndex = bestSlot;
                    s.worldPosition = slotPositions[bestSlot];
                    slotClaimed[bestSlot] = true;
                    combatantMatched[i] = true;
                }
            }

            // Evaluate in-position state for each slot
            for (int i = 0; i < n; i++)
            {
                var s = group.slots[i];
                Vector3 cPos = s.combatant.CombatantTransform.position;
                Vector3 slotPos = s.worldPosition;
                cPos.y = 0f; slotPos.y = 0f;
                s.isInPosition = Vector3.Distance(cPos, slotPos) <= surroundArrivalTolerance;
            }
        }

        private void EvaluateInitialSurround(TargetCombatGroup group)
        {
            if (group.isInitialSurroundComplete) return;

            int n = group.slots.Count;
            int inPosCount = group.slots.Count(s => s.isInPosition);

            // If all (or at least N-1) enemies have arrived at their surrounding slots
            bool formationArrived = (n <= 2 && inPosCount >= 2) || (n > 2 && inPosCount >= n - 1);
            bool timeoutReached = (Time.time - group.groupFormationTime) >= initialSurroundTimeout;

            if (formationArrived || timeoutReached)
            {
                group.isInitialSurroundComplete = true;
                group.lastAttackFinishTime = Time.time; // cadence delay begins before first attack
                if (verboseLogging)
                {
                    Debug.Log($"[EnemyGroupCombatCoordinator] Formation READY! ({inPosCount}/{n} in position, arrived={formationArrived}, timeout={timeoutReached}). Commencing coordinated attacks.");
                }
            }
        }

        private void UpdateAttackTokenScheduling(TargetCombatGroup group)
        {
            if (!enableCoordinatedAttacks || !group.isInitialSurroundComplete)
            {
                // Attacking not permitted yet
                return;
            }

            // 1. Safety check on active attacker
            if (group.currentAttacker != null)
            {
                bool isDeadOrInactive = group.currentAttacker == null || !group.currentAttacker.IsCombatActive;
                bool timeout = Time.time - group.tokenGrantTime >= maxAttackDuration;

                if (isDeadOrInactive || timeout)
                {
                    if (verboseLogging)
                    {
                        Debug.LogWarning($"[EnemyGroupCombatCoordinator] Active attacker revoked (dead/inactive={isDeadOrInactive}, timeout={timeout}). Resetting token.");
                    }
                    if (group.currentAttacker != null)
                    {
                        group.currentAttacker.OnAttackTokenRevoked();
                    }
                    group.currentAttacker = null;
                    group.lastAttackFinishTime = Time.time;
                }
                return; // Attacker is actively executing strike
            }

            // 2. Token is available: check cadence cooldown
            if (Time.time < group.lastAttackFinishTime + attackCadenceDelay)
            {
                // In between attacks: all enemies maintain menacing surround stance
                return;
            }

            // 3. Select next attacker among waiting surrounding combatants
            var availableSlots = group.slots
                .Where(s => s.combatant != null && s.combatant.IsCombatActive)
                .ToList();

            if (availableSlots.Count == 0) return;

            // Strategy: Rotate turns fairly. Prefer an enemy that didn't just attack.
            CombatantSlot chosenSlot = null;

            if (availableSlots.Count >= 2)
            {
                // Filter out the last attacker to alternate
                var otherSlots = availableSlots
                    .Where(s => s.slotIndex != group.lastAttackerSlotIndex)
                    .ToList();

                var pool = otherSlots.Count > 0 ? otherSlots : availableSlots;

                // Pick the enemy who has waited the longest in position
                chosenSlot = pool
                    .OrderByDescending(s => (s.isInPosition ? 1000f : 0f) + (Time.time - s.lastAttackTime))
                    .FirstOrDefault();
            }
            else
            {
                chosenSlot = availableSlots[0];
            }

            if (chosenSlot != null && chosenSlot.combatant != null)
            {
                GrantAttackToken(group, chosenSlot.combatant);
                chosenSlot.lastAttackTime = Time.time;
                group.lastAttackerSlotIndex = chosenSlot.slotIndex;
            }
        }

        private void GrantAttackToken(TargetCombatGroup group, IGroupCombatant combatant)
        {
            group.currentAttacker = combatant;
            group.tokenGrantTime = Time.time;
            combatant.OnAttackTokenGranted();
            if (verboseLogging)
            {
                Debug.Log($"[EnemyGroupCombatCoordinator] >>> GRANTED Attack Token to [{combatant.CombatantTransform.name}] <<<");
            }
        }

        // ══════════════════════════════════════════════════════════════════════════
        // SCENE GIZMOS & VISUALIZATION
        // ══════════════════════════════════════════════════════════════════════════

        private void OnDrawGizmos()
        {
            if (!showDebugGizmos || !Application.isPlaying) return;

            foreach (var kvp in _groups)
            {
                var target = kvp.Key;
                var group = kvp.Value;
                if (target == null) continue;

                Vector3 tPos = target.position;

                // Draw surrounding ring
                Gizmos.color = group.isInitialSurroundComplete
                    ? new Color(0.2f, 0.9f, 0.4f, 0.6f) // Green: combat active
                    : new Color(0.1f, 0.7f, 1.0f, 0.5f); // Cyan: forming surround
                Gizmos.DrawWireSphere(tPos + Vector3.up * 0.1f, surroundRadius);

                // Draw slots
                foreach (var s in group.slots)
                {
                    if (s.combatant == null) continue;

                    bool isAttacker = group.currentAttacker == s.combatant;
                    Vector3 cPos = s.combatant.CombatantTransform.position;

                    if (isAttacker)
                    {
                        Gizmos.color = Color.red;
                        Gizmos.DrawWireSphere(s.worldPosition + Vector3.up * 0.3f, 0.6f);
                        Gizmos.DrawLine(cPos + Vector3.up * 1f, tPos + Vector3.up * 1f);
                    }
                    else if (s.isInPosition)
                    {
                        Gizmos.color = Color.yellow;
                        Gizmos.DrawWireSphere(s.worldPosition + Vector3.up * 0.2f, 0.4f);
                        Gizmos.DrawLine(s.worldPosition, cPos);
                    }
                    else
                    {
                        Gizmos.color = Color.cyan;
                        Gizmos.DrawWireSphere(s.worldPosition + Vector3.up * 0.2f, 0.3f);
                        Gizmos.DrawLine(cPos, s.worldPosition);
                    }
                }
            }
        }
    }
}
