using UnityEngine;
using UnityEngine.AI;
using PolygonCyberCity.NPC.Spawner;

namespace PolygonCyberCity.NPC.BT.Tasks
{
    /// <summary>
    /// AAA NavMesh sidewalk and street wandering task for pedestrians and ambient NPCs.
    /// Features sidewalk corridor preference, designated crosswalk navigation, wall avoidance rerouting,
    /// anti-stuck speed progress tracking, and vehicle avoidance synchronization.
    /// </summary>
    public class TaskWanderStreet : BTNode
    {
        private readonly NavMeshAgent _agent;
        private readonly Animator _animator;
        private readonly float _wanderRadius;
        private Vector3 _destination;
        private bool _hasTarget = false;
        private float _stuckTimer = 0f;
        private float _lastCheckTime = 0f;
        private Vector3 _lastCheckPosition;
        private NavMeshPath _pathBuffer;
        private CyberNPCVehicleAvoidance _vehicleAvoidance;

        // Crosswalk state
        private bool _isApproachingCrosswalk = false;
        private Vector3 _finalSidewalkDestination;
        private float _crosswalkWaitTimer = 0f;
        private bool _isWaitingAtCrosswalk = false;

        public TaskWanderStreet(NavMeshAgent agent, Animator animator, float wanderRadius = 28f)
        {
            _agent = agent;
            _animator = animator;
            _wanderRadius = wanderRadius;
            _pathBuffer = new NavMeshPath();
        }

        public override NodeState Evaluate()
        {
            if (_agent == null || blackboard == null) return NodeState.Failure;

            if (_vehicleAvoidance == null && owner != null)
            {
                _vehicleAvoidance = owner.GetComponent<CyberNPCVehicleAvoidance>();
            }

            // Yield control if currently executing emergency vehicle dodge
            if (_vehicleAvoidance != null && _vehicleAvoidance.IsEvading)
            {
                LastState = NodeState.Running;
                return NodeState.Running;
            }

            if (!_agent.isOnNavMesh)
            {
                if (NavMesh.SamplePosition(owner.transform.position, out NavMeshHit snapHit, 5.0f, NavMesh.AllAreas))
                {
                    _agent.Warp(snapHit.position);
                }
                else
                {
                    return NodeState.Failure;
                }
            }

            // Crosswalk curb wait logic (look both ways before crossing)
            if (_isWaitingAtCrosswalk)
            {
                _crosswalkWaitTimer += Time.deltaTime;
                if (_animator != null && _animator.isActiveAndEnabled)
                {
                    _animator.SetBool("IsWalking", false);
                    _animator.SetBool("IsStopped", true);
                }

                // Head turn / look left & right
                owner.transform.Rotate(Vector3.up, Mathf.Sin(_crosswalkWaitTimer * 5f) * 25f * Time.deltaTime);

                if (_crosswalkWaitTimer >= 0.85f)
                {
                    _isWaitingAtCrosswalk = false;
                    _isApproachingCrosswalk = false;
                    _destination = _finalSidewalkDestination;
                    _agent.isStopped = false;
                    _agent.SetDestination(_destination);
                }

                LastState = NodeState.Running;
                return NodeState.Running;
            }

            if (!_hasTarget)
            {
                if (PickValidSidewalkOrNavMeshDestination(out Vector3 target, out bool viaCrosswalk, out Vector3 crosswalkNode))
                {
                    if (viaCrosswalk)
                    {
                        _isApproachingCrosswalk = true;
                        _finalSidewalkDestination = target;
                        _destination = crosswalkNode;
                    }
                    else
                    {
                        _isApproachingCrosswalk = false;
                        _destination = target;
                    }

                    _hasTarget = true;
                    _stuckTimer = 0f;
                    _lastCheckPosition = owner.transform.position;
                    _lastCheckTime = Time.time;
                    _agent.isStopped = false;
                    _agent.speed = blackboard.MoveSpeed;
                    _agent.SetDestination(_destination);
                    blackboard.CurrentDestination = _destination;
                }
                else
                {
                    LastState = NodeState.Failure;
                    return NodeState.Failure;
                }
            }

            // Sync locomotion animation state
            if (_animator != null && _animator.isActiveAndEnabled)
            {
                _animator.SetBool("IsWalking", true);
                _animator.SetBool("IsStopped", false);
                _animator.SetInteger("RandomIdle", 0);
            }

            // Whisker & SphereCast Obstacle Detection (Wall / Building / Barrier avoidance)
            if (DetectObstacleAhead(out Vector3 avoidHitNormal, out Vector3 avoidDir))
            {
                if (RerouteAroundObstacle(avoidHitNormal, avoidDir, out Vector3 newTarget))
                {
                    _destination = newTarget;
                    _agent.SetDestination(_destination);
                    blackboard.CurrentDestination = _destination;
                }
            }

            // Anti-Stuck Speed Progress Tracking
            if (Time.time - _lastCheckTime >= 0.20f)
            {
                float distMoved = Vector3.Distance(owner.transform.position, _lastCheckPosition);
                _lastCheckPosition = owner.transform.position;
                _lastCheckTime = Time.time;

                float expectedDist = _agent.speed * 0.20f;
                if (!_agent.isStopped && distMoved < 0.30f * expectedDist)
                {
                    _stuckTimer += 0.20f;
                    if (_stuckTimer >= 0.50f)
                    {
                        _agent.ResetPath();
                        _hasTarget = false;
                        _stuckTimer = 0f;

                        if (PickValidSidewalkOrNavMeshDestination(out Vector3 rerouteTarget, out _, out _))
                        {
                            _destination = rerouteTarget;
                            _hasTarget = true;
                            _agent.SetDestination(_destination);
                        }
                    }
                }
                else
                {
                    _stuckTimer = Mathf.Max(0f, _stuckTimer - 0.20f);
                }
            }

            // Check arrival or partial unreachability
            if (!_agent.pathPending && (_agent.remainingDistance <= _agent.stoppingDistance + 0.6f || _agent.pathStatus == NavMeshPathStatus.PathPartial))
            {
                // If arrived at crosswalk curb: pause to look before crossing to final sidewalk
                if (_isApproachingCrosswalk)
                {
                    _isWaitingAtCrosswalk = true;
                    _crosswalkWaitTimer = 0f;
                    _agent.isStopped = true;
                    _agent.velocity = Vector3.zero;
                    LastState = NodeState.Running;
                    return NodeState.Running;
                }

                _hasTarget = false;
                if (_animator != null && _animator.isActiveAndEnabled)
                {
                    _animator.SetBool("IsWalking", false);
                    _animator.SetBool("IsStopped", true);
                }
                LastState = NodeState.Success;
                return NodeState.Success;
            }

            LastState = NodeState.Running;
            return NodeState.Running;
        }

        private bool PickValidSidewalkOrNavMeshDestination(out Vector3 target, out bool viaCrosswalk, out Vector3 crosswalkNode)
        {
            target = owner.transform.position;
            viaCrosswalk = false;
            crosswalkNode = Vector3.zero;

            // Strategy 1: Smart Sidewalk Corridor via CyberCitySidewalkManager
            if (CyberCitySidewalkManager.Instance != null)
            {
                if (CyberCitySidewalkManager.Instance.TryGetSidewalkDestination(owner.transform.position, _wanderRadius, out Vector3 sidewalkTarget))
                {
                    if (_agent.CalculatePath(sidewalkTarget, _pathBuffer) && _pathBuffer.status == NavMeshPathStatus.PathComplete)
                    {
                        target = sidewalkTarget;

                        // Check if route crosses a street crosswalk zone
                        if (CyberCitySidewalkManager.Instance.TryGetNearestCrosswalk(owner.transform.position, target, out crosswalkNode))
                        {
                            viaCrosswalk = true;
                        }
                        return true;
                    }
                }
            }

            // Strategy 2: Fallback NavMesh sampling with 100% complete path validation
            for (int attempt = 0; attempt < 8; attempt++)
            {
                Vector3 randomDirection = Random.insideUnitSphere * _wanderRadius;
                randomDirection.y = 0;
                Vector3 candidatePos = owner.transform.position + randomDirection;

                if (NavMesh.SamplePosition(candidatePos, out NavMeshHit hit, 8f, NavMesh.AllAreas))
                {
                    if (_agent.CalculatePath(hit.position, _pathBuffer) && _pathBuffer.status == NavMeshPathStatus.PathComplete)
                    {
                        target = hit.position;
                        return true;
                    }
                }
            }

            return false;
        }

        private bool DetectObstacleAhead(out Vector3 hitNormal, out Vector3 avoidDirection)
        {
            hitNormal = Vector3.zero;
            avoidDirection = Vector3.zero;
            Vector3 origin = owner.transform.position + Vector3.up * 0.8f;
            Vector3 fwd = owner.transform.forward;
            float rayDist = 1.8f;

            Vector3 leftFwd = Quaternion.Euler(0, -35, 0) * fwd;
            Vector3 rightFwd = Quaternion.Euler(0, 35, 0) * fwd;

            bool hit = false;

            if (IsSolidWall(origin, fwd, rayDist, out hitNormal))
            {
                hit = true;
            }
            else if (IsSolidWall(origin, leftFwd, rayDist * 0.85f, out hitNormal))
            {
                hit = true;
            }
            else if (IsSolidWall(origin, rightFwd, rayDist * 0.85f, out hitNormal))
            {
                hit = true;
            }
            else if (Physics.SphereCast(origin, 0.35f, fwd, out RaycastHit sphereHit, rayDist, ~0, QueryTriggerInteraction.Ignore))
            {
                if (sphereHit.collider != null && !sphereHit.collider.isTrigger && !sphereHit.transform.IsChildOf(owner.transform) && !sphereHit.transform.CompareTag("Player"))
                {
                    hitNormal = sphereHit.normal;
                    hit = true;
                }
            }

            if (hit)
            {
                avoidDirection = Vector3.Reflect(fwd, hitNormal);
                avoidDirection.y = 0f;
                if (avoidDirection.sqrMagnitude < 0.01f) avoidDirection = -fwd;
                avoidDirection.Normalize();
                return true;
            }

            return false;
        }

        private bool IsSolidWall(Vector3 origin, Vector3 dir, float dist, out Vector3 normal)
        {
            normal = Vector3.zero;
            if (Physics.Raycast(origin, dir, out RaycastHit hit, dist, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!hit.collider.isTrigger && !hit.transform.IsChildOf(owner.transform) && !hit.transform.CompareTag("Player"))
                {
                    normal = hit.normal;
                    return true;
                }
            }
            return false;
        }

        private bool RerouteAroundObstacle(Vector3 hitNormal, Vector3 avoidDir, out Vector3 newDestination)
        {
            newDestination = owner.transform.position;

            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f;
                Vector3 testDir = Quaternion.Euler(0, angle, 0) * avoidDir;
                Vector3 candidatePos = owner.transform.position + testDir * Random.Range(10f, 22f);

                if (NavMesh.SamplePosition(candidatePos, out NavMeshHit hit, 6f, NavMesh.AllAreas))
                {
                    if (_agent.CalculatePath(hit.position, _pathBuffer) && _pathBuffer.status == NavMeshPathStatus.PathComplete)
                    {
                        newDestination = hit.position;
                        return true;
                    }
                }
            }

            return false;
        }

        public override void ResetNode()
        {
            base.ResetNode();
            _hasTarget = false;
            _stuckTimer = 0f;
            _isApproachingCrosswalk = false;
            _isWaitingAtCrosswalk = false;
        }
    }
}
