using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SyntyCombined.BlackMarket
{
    /// <summary>
    /// Autonomous walking, patrolling, and roaming controller for Black Market NPCs.
    /// Drives smooth waypoint traversal across market streets, shop stalls, and promenades,
    /// synchronizes walking/idling animations with zero-lag transitions, and guarantees
    /// 100% continuous floor collider snapping. Automatically pauses when Alita engages
    /// in dialogue or approaches within interaction range.
    /// </summary>
    public class BlackMarketNPCWanderer : MonoBehaviour
    {
        [Header("Patrol Waypoints")]
        [Tooltip("Series of local or world positions to patrol between")]
        public List<Vector3> waypoints = new List<Vector3>();
        public bool loopPatrol = true;

        [Header("Locomotion Tuning")]
        public float walkSpeed = 1.45f;
        public float turnSpeed = 5.5f;
        public float waypointRadius = 1.0f;

        [Header("Waypoint Pause (Browsing & Looking Around)")]
        public float waitTimeMin = 0.4f;
        public float waitTimeMax = 1.2f;

        [Header("Animation")]
        public Animator animator;
        public RuntimeAnimatorController walkController;
        public RuntimeAnimatorController idleController;

        [Header("Obstacle Avoidance")]
        [Tooltip("How far ahead to probe for walls/props")]
        public float lookAhead = 1.1f;
        public float probeRadius = 0.3f;
        public float probeHeight = 0.9f;

        [Header("Floor Raycast Layer Mask")]
        public LayerMask floorLayerMask = ~0;

        // Internal State
        private int _currentWaypointIndex = 0;
        private bool _isWaiting = false;
        private float _waitTimer = 0f;
        private bool _isPaused = false;
        private bool _isReversing = false;
        private BlackMarketNPC _npc;
        private CharacterController _characterController;
        private int _isWalkingParamHash;
        private Vector3 _lastStuckCheckPos;
        private float _stuckCheckTimer = 0f;
        private float _avoidSign = 1f;
        private float _blockedTime = 0f;

        public bool IsMoving => !_isWaiting && !_isPaused && waypoints != null && waypoints.Count > 1;

        private void Awake()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                if (animator.runtimeAnimatorController == null)
                {
#if UNITY_EDITOR
                    animator.runtimeAnimatorController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/PolygonCyberCity/Animations/AC_CyberNPC.controller");
#endif
                }
            }

            CleanupConflictingComponents();

            _npc = GetComponent<BlackMarketNPC>();
            _characterController = GetComponent<CharacterController>();
            _isWalkingParamHash = Animator.StringToHash("IsWalking");
        }

        private void CleanupConflictingComponents()
        {
            var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null) agent.enabled = false;

            var cyberCtrl = GetComponent("PolygonCyberCity.NPC.Spawner.CyberNPCController") as MonoBehaviour;
            if (cyberCtrl != null) cyberCtrl.enabled = false;

            var bt = GetComponent("PolygonCyberCity.NPC.BT.BehaviorTreeRunner") as MonoBehaviour;
            if (bt != null) bt.enabled = false;

            var veh = GetComponent("PolygonCyberCity.NPC.Spawner.CyberNPCVehicleAvoidance") as MonoBehaviour;
            if (veh != null) veh.enabled = false;
        }

        private void Start()
        {
            // 1. Ignore collision with all other NPCs in the ecosystem so crowd never jams
            var myCols = GetComponentsInChildren<Collider>();
            var rootParent = transform.root;
            if (rootParent != null)
            {
                var allCols = rootParent.GetComponentsInChildren<Collider>();
                foreach (var mc in myCols)
                {
                    if (mc == null) continue;
                    foreach (var oc in allCols)
                    {
                        if (oc != null && mc != oc)
                        {
                            Physics.IgnoreCollision(mc, oc, true);
                        }
                    }
                }
            }

            // 1b. Discard waypoints buried inside walls or props so nobody tries to walk into them
            if (waypoints.Count > 2)
            {
                for (int i = waypoints.Count - 1; i >= 0 && waypoints.Count > 2; i--)
                    if (IsInsideSolid(waypoints[i])) waypoints.RemoveAt(i);
            }

            // 2. Initialize floor height for all waypoints
            for (int i = 0; i < waypoints.Count; i++)
            {
                Vector3 wp = waypoints[i];
                wp.y = SnapToFloor(wp);
                waypoints[i] = wp;
            }

            // 3. Stagger starting position along waypoint route so NPCs don't cluster together
            if (waypoints != null && waypoints.Count > 1)
            {
                _currentWaypointIndex = Random.Range(0, waypoints.Count);
                Vector3 startPos = waypoints[_currentWaypointIndex];
                startPos.y = SnapToFloor(startPos);
                transform.position = startPos;

                _currentWaypointIndex = (_currentWaypointIndex + 1) % waypoints.Count;
                _lastStuckCheckPos = transform.position;
                _waitTimer = Random.Range(0f, 0.6f);
            }

            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            SetWalkingAnimation(true);
        }

        private void Update()
        {
            if (waypoints == null || waypoints.Count < 2) return;

            // Check if story moved to combat/ambush
            if (BlackMarketStoryCombatDirector.Instance != null)
            {
                var storyState = BlackMarketStoryCombatDirector.Instance.currentState;
                if (storyState == BlackMarketStoryState.BetrayalAmbush ||
                    storyState == BlackMarketStoryState.FleeAcrossCatwalk ||
                    storyState == BlackMarketStoryState.CatwalkCollapse_Fall ||
                    storyState == BlackMarketStoryState.LowerTrenchCombat)
                {
                    SetWalkingAnimation(false);
                    return;
                }
            }

            // If paused for dialogue or player interaction, halt
            if (_isPaused)
            {
                SetWalkingAnimation(false);
                return;
            }

            if (_isWaiting)
            {
                _waitTimer -= Time.deltaTime;
                SetWalkingAnimation(false);

                if (_waitTimer <= 0f)
                {
                    _isWaiting = false;
                    if (HasParameter(Animator.StringToHash("RandomIdle")))
                    {
                        animator.SetInteger("RandomIdle", 0);
                    }
                    AdvanceWaypoint();
                    SetWalkingAnimation(true);
                }
                return;
            }

            // Walk towards current waypoint
            Vector3 target = waypoints[_currentWaypointIndex];
            Vector3 diff = target - transform.position;
            diff.y = 0f;

            float dist = diff.magnitude;

            if (dist <= waypointRadius)
            {
                // Reached waypoint: pause to browse wares or observe the market
                _isWaiting = true;
                _waitTimer = Random.Range(waitTimeMin, waitTimeMax);
                SetWalkingAnimation(false);
                return;
            }

            SetWalkingAnimation(true);

            // Steer around walls and props instead of walking straight into them
            Vector3 moveDir = diff.normalized;
            bool pathFree = TrySteer(ref moveDir);
            if (!pathFree)
            {
                _blockedTime += Time.deltaTime;
                if (_blockedTime > 0.8f)
                {
                    // Boxed in: give up on this waypoint, browse for a moment, then try the next one
                    _blockedTime = 0f;
                    _isWaiting = true;
                    _waitTimer = Random.Range(waitTimeMin, waitTimeMax);
                    AdvanceWaypoint();
                    SetWalkingAnimation(false);
                }
                else
                {
                    SetWalkingAnimation(false);
                }
                return;
            }
            _blockedTime = 0f;

            if (moveDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
            }

            // While still turning away from a wall, hold position rather than clipping into it
            if (IsBlocked(transform.forward, Mathf.Min(lookAhead, 0.6f)))
                return;

            // Anti-stuck watchdog: if moving but barely progressing over 1.6s, advance waypoint
            _stuckCheckTimer += Time.deltaTime;
            if (_stuckCheckTimer >= 1.6f)
            {
                float moved = Vector3.Distance(transform.position, _lastStuckCheckPos);
                if (moved < 0.25f && !_isWaiting && !_isPaused)
                {
                    AdvanceWaypoint();
                }
                _lastStuckCheckPos = transform.position;
                _stuckCheckTimer = 0f;
            }

            // Move forward using CharacterController or fallback transform positioning
            if (_characterController != null && _characterController.enabled)
            {
                Vector3 motion = transform.forward * walkSpeed;
                motion.y = -9.81f; // Gravity to hug ramps, steps, and slopes
                _characterController.Move(motion * Time.deltaTime);

                // Ensure height consistency on elevated platforms and steps
                Vector3 p = transform.position;
                float groundY = SnapToFloor(p);
                if (p.y < groundY - 0.25f || p.y > groundY + 0.6f)
                {
                    p.y = groundY;
                    transform.position = p;
                }
            }
            else
            {
                Vector3 nextPos = transform.position + transform.forward * (walkSpeed * Time.deltaTime);
                nextPos.y = SnapToFloor(nextPos);
                transform.position = nextPos;
            }
        }

        private bool IsInsideSolid(Vector3 pos)
        {
            var cols = Physics.OverlapSphere(new Vector3(pos.x, pos.y + 1.0f, pos.z), 0.3f, ~0, QueryTriggerInteraction.Ignore);
            foreach (var c in cols)
            {
                if (c == null || c.transform.IsChildOf(transform)) continue;
                if (c.GetComponentInParent<CharacterController>() != null) continue;
                if (c.GetComponentInParent<BlackMarketNPCWanderer>() != null) continue;
                string n = c.name.ToLower();
                if (n.Contains("alita") || n.Contains("starship") || n.StartsWith("npc_") || n.Contains("trigger")) continue;
                return true;
            }
            return false;
        }

        // Tries the desired heading first, then widening offsets on the side that worked last time.
        private bool TrySteer(ref Vector3 desired)
        {
            if (!IsBlocked(desired, lookAhead)) return true;

            float[] angles = { 35f, 70f, 110f, 150f };
            foreach (float a in angles)
            {
                Vector3 first = Quaternion.Euler(0f, a * _avoidSign, 0f) * desired;
                if (!IsBlocked(first, lookAhead)) { desired = first; return true; }

                Vector3 second = Quaternion.Euler(0f, -a * _avoidSign, 0f) * desired;
                if (!IsBlocked(second, lookAhead)) { desired = second; _avoidSign = -_avoidSign; return true; }
            }
            return false;
        }

        // True when a wall or solid prop (not floor, steps, other NPCs or the player) is within dist along dir.
        private bool IsBlocked(Vector3 dir, float dist)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return false;
            dir.Normalize();

            // Probe at chest and knee height so low walls and railings count too; all layers, whatever floorLayerMask says
            for (int h = 0; h < 2; h++)
            {
                Vector3 origin = transform.position + Vector3.up * (h == 0 ? probeHeight : 0.45f);
                if (ProbeHits(origin, dir, dist)) return true;
            }
            return false;
        }

        private bool ProbeHits(Vector3 origin, Vector3 dir, float dist)
        {
            var hits = Physics.SphereCastAll(origin, probeRadius, dir, dist, ~0, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                var col = hit.collider;
                if (col == null || col.transform.IsChildOf(transform)) continue;
                if (hit.normal.y > 0.55f) continue;                       // walkable floor / ramp
                if (col.GetComponentInParent<CharacterController>() != null) continue; // other NPCs, player
                if (col.GetComponentInParent<BlackMarketNPCWanderer>() != null) continue;

                string n = col.name.ToLower();
                if (n.Contains("alita") || n.Contains("starship") || n.StartsWith("npc_") ||
                    n.Contains("sign") || n.Contains("trigger") || n.Contains("roof")) continue;

                // Low lips (curbs, cables) are stepped over
                if (col.bounds.max.y < transform.position.y + 0.25f) continue;

                return true;
            }
            return false;
        }

        private void AdvanceWaypoint()
        {
            if (waypoints.Count == 0) return;

            if (loopPatrol)
            {
                _currentWaypointIndex = (_currentWaypointIndex + 1) % waypoints.Count;
            }
            else
            {
                if (!_isReversing)
                {
                    _currentWaypointIndex++;
                    if (_currentWaypointIndex >= waypoints.Count)
                    {
                        _currentWaypointIndex = waypoints.Count - 2;
                        _isReversing = true;
                    }
                }
                else
                {
                    _currentWaypointIndex--;
                    if (_currentWaypointIndex < 0)
                    {
                        _currentWaypointIndex = 1;
                        _isReversing = false;
                    }
                }
            }
        }

        private float SnapToFloor(Vector3 pos)
        {
            // Cast ray from 1.5m above the current position down 4.0m
            Ray ray = new Ray(new Vector3(pos.x, pos.y + 1.5f, pos.z), Vector3.down);
            RaycastHit[] hits = Physics.RaycastAll(ray, 4.0f, floorLayerMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => b.point.y.CompareTo(a.point.y));

            foreach (var hit in hits)
            {
                string hitName = hit.collider.name.ToLower();
                if (hitName.Contains("alita") || hitName.Contains("starship") || hitName.Contains("roof") ||
                    hitName.Contains("sign") || hitName.StartsWith("npc_") || hitName.Contains("trigger"))
                    continue;

                return hit.point.y;
            }

            return pos.y;
        }

        public void SetPaused(bool paused)
        {
            _isPaused = paused;
            SetWalkingAnimation(!paused && !_isWaiting);
        }

        private void SetWalkingAnimation(bool walking)
        {
            if (animator == null) return;

            if (walking)
            {
                if (HasParameter(Animator.StringToHash("RandomIdle")))
                {
                    animator.SetInteger("RandomIdle", 0);
                }

                if (HasParameter(_isWalkingParamHash))
                {
                    animator.SetBool(_isWalkingParamHash, true);
                }

                if (HasParameter(Animator.StringToHash("IsStopped")))
                {
                    animator.SetBool("IsStopped", false);
                }

                float animSpeed = Mathf.Max(walkSpeed, 1.45f);
                if (HasParameter(Animator.StringToHash("Speed")))
                {
                    animator.SetFloat("Speed", animSpeed);
                }

                if (HasParameter(Animator.StringToHash("MoveSpeed")))
                {
                    animator.SetFloat("MoveSpeed", animSpeed);
                }

                // If not already in Locomotion or actively transitioning into it, smoothly crossfade
                var state = animator.GetCurrentAnimatorStateInfo(0);
                if (!state.IsName("Locomotion") && !animator.IsInTransition(0))
                {
                    animator.CrossFadeInFixedTime("Locomotion", 0.12f, 0);
                }
            }
            else
            {
                if (HasParameter(_isWalkingParamHash))
                {
                    animator.SetBool(_isWalkingParamHash, false);
                }

                if (HasParameter(Animator.StringToHash("IsStopped")))
                {
                    animator.SetBool("IsStopped", true);
                }

                if (HasParameter(Animator.StringToHash("Speed")))
                {
                    animator.SetFloat("Speed", 0f);
                }

                if (HasParameter(Animator.StringToHash("MoveSpeed")))
                {
                    animator.SetFloat("MoveSpeed", 0f);
                }

                if (HasParameter(Animator.StringToHash("RandomIdle")) && _isWaiting)
                {
                    int currentIdle = animator.GetInteger("RandomIdle");
                    if (currentIdle == 0)
                    {
                        int[] wanderIdlePool = { 1, 4, 5, 6, 21, 22 };
                        animator.SetInteger("RandomIdle", wanderIdlePool[Random.Range(0, wanderIdlePool.Length)]);
                    }
                }
            }

            // Controller swapping fallback if idle/walk are separate standalone controllers
            if (walkController != null && idleController != null)
            {
                var targetCtrl = walking ? walkController : idleController;
                if (animator.runtimeAnimatorController != targetCtrl)
                {
                    animator.runtimeAnimatorController = targetCtrl;
                }
            }
        }

        private bool HasParameter(int paramHash)
        {
            if (animator == null) return false;
            foreach (var p in animator.parameters)
            {
                if (p.nameHash == paramHash) return true;
            }
            return false;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (waypoints == null || waypoints.Count < 2) return;

            Gizmos.color = Color.cyan;
            for (int i = 0; i < waypoints.Count; i++)
            {
                Gizmos.DrawSphere(waypoints[i], 0.25f);
                int next = (i + 1) % waypoints.Count;
                if (loopPatrol || next != 0)
                {
                    Gizmos.DrawLine(waypoints[i], waypoints[next]);
                }
            }
        }
#endif
    }
}
