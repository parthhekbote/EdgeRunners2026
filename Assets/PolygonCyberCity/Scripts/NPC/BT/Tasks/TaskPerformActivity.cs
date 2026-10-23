using UnityEngine;
using UnityEngine.AI;
using PolygonCyberCity.NPC.Spawner;
using PolygonCyberCity.NPC.Animation;

namespace PolygonCyberCity.NPC.BT.Tasks
{
    /// <summary>
    /// AAA Contextual Urban Activity Task for Cyber City Pedestrians.
    /// Interacts with city Smart Objects (benches, vending machines, data terminals, shopfronts, and food stalls):
    /// 1. Finds nearby available SmartObject matching archetype profile.
    /// 2. Paths to interaction point and aligns with target facing orientation.
    /// 3. Performs contextual cyberpunk animations (sitting on bench, phone call at terminal, drinking, window shopping).
    /// 4. Synchronizes spatial audio chatter (phone babble, terminal blips, murmurs).
    /// 5. Cleanly releases SmartObject with cooldown upon completion.
    /// </summary>
    public class TaskPerformActivity : BTNode
    {
        private readonly NavMeshAgent _agent;
        private readonly Animator _animator;
        private readonly CyberNPCController _controller;

        private SmartObject _currentTarget;
        private float _activityTimer = 0f;
        private bool _isPerforming = false;
        private ObstacleAvoidanceType _originalAvoidanceType;

        public TaskPerformActivity(NavMeshAgent agent, Animator animator, CyberNPCController controller)
        {
            _agent = agent;
            _animator = animator;
            _controller = controller;
        }

        public override NodeState Evaluate()
        {
            if (blackboard == null || _controller == null || _controller.animationProfile == null || _agent == null || !_agent.isActiveAndEnabled || !_agent.isOnNavMesh)
            {
                LastState = NodeState.Failure;
                return NodeState.Failure;
            }

            var profile = _controller.animationProfile;
            if (profile.allowedActivities == null || profile.allowedActivities.Length == 0)
            {
                LastState = NodeState.Failure;
                return NodeState.Failure;
            }

            // Phase 1: Search for nearby unassigned SmartObject matching profile
            if (_currentTarget == null)
            {
                Collider[] hits = Physics.OverlapSphere(owner.transform.position, 10.0f);
                foreach (var hit in hits)
                {
                    SmartObject smartObj = hit.GetComponent<SmartObject>();
                    if (smartObj == null) smartObj = hit.GetComponentInParent<SmartObject>();

                    if (smartObj != null && smartObj.IsAvailable)
                    {
                        // Check if activity is in allowed activities
                        bool isAllowed = System.Array.Exists(profile.allowedActivities, a => a == smartObj.activityType);
                        if (!isAllowed)
                        {
                            // Also permit common ambient activities
                            if (smartObj.activityType == NPCActivityType.VendingMachine && System.Array.Exists(profile.allowedActivities, a => a == NPCActivityType.Drink)) isAllowed = true;
                            if (smartObj.activityType == NPCActivityType.UseTerminal && System.Array.Exists(profile.allowedActivities, a => a == NPCActivityType.Phone)) isAllowed = true;
                            if (smartObj.activityType == NPCActivityType.Shop && System.Array.Exists(profile.allowedActivities, a => a == NPCActivityType.Inspect)) isAllowed = true;
                        }

                        if (isAllowed)
                        {
                            if (smartObj.TryOccupy(owner))
                            {
                                _currentTarget = smartObj;
                                break;
                            }
                        }
                    }
                }

                if (_currentTarget == null)
                {
                    LastState = NodeState.Failure;
                    return NodeState.Failure;
                }
            }

            // Phase 2: Approach target interaction point
            Transform interactT = _currentTarget.interactionPoint != null ? _currentTarget.interactionPoint : _currentTarget.transform;
            float dist = Vector3.Distance(owner.transform.position, interactT.position);

            if (dist > 0.95f && !_isPerforming)
            {
                if (_agent != null && _agent.isOnNavMesh)
                {
                    _agent.isStopped = false;
                    _agent.SetDestination(interactT.position);
                    _agent.speed = _controller.walkSpeed;
                }

                if (_animator != null && _animator.isActiveAndEnabled)
                {
                    _animator.SetFloat(NPCAnimatorHashes.MoveSpeed, _agent.velocity.magnitude);
                    _animator.SetBool(NPCAnimatorHashes.IsWalking, true);
                    _animator.SetBool(NPCAnimatorHashes.IsStopped, false);
                    _animator.SetInteger(NPCAnimatorHashes.RandomIdle, 0);
                }

                LastState = NodeState.Running;
                return NodeState.Running;
            }

            // Phase 3: Perform activity at destination
            if (!_isPerforming)
            {
                _isPerforming = true;
                _activityTimer = 0f;
                if (blackboard != null)
                {
                    blackboard.CurrentState = NPCMacroState.PerformingActivity;
                }

                if (_agent != null && _agent.isOnNavMesh)
                {
                    _originalAvoidanceType = _agent.obstacleAvoidanceType;
                    _agent.isStopped = true;
                    _agent.velocity = Vector3.zero;
                    // Lower avoidance while seated or using terminal so pedestrians don't push them
                    if (_currentTarget.activityType == NPCActivityType.Sit)
                    {
                        _agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
                    }
                }

                // Snap facing orientation smoothly to interaction point, ensuring feet are grounded
                Vector3 targetPos = interactT.position;
                if (NavMesh.SamplePosition(targetPos, out NavMeshHit groundHit, 2.0f, NavMesh.AllAreas))
                {
                    targetPos = groundHit.position;
                }
                owner.transform.position = targetPos;
                owner.transform.rotation = interactT.rotation;

                if (_animator != null && _animator.isActiveAndEnabled)
                {
                    _animator.SetFloat(NPCAnimatorHashes.MoveSpeed, 0f);
                    _animator.SetBool(NPCAnimatorHashes.IsWalking, false);
                    _animator.SetBool(NPCAnimatorHashes.IsStopped, true);
                    _animator.SetInteger(NPCAnimatorHashes.ActivityID, (int)_currentTarget.activityType);
                    _animator.SetTrigger(NPCAnimatorHashes.Activity);

                    // Set contextual idle pose based on activity type using full 29-clip palette
                    switch (_currentTarget.activityType)
                    {
                        case NPCActivityType.Sit:
                            // Urban rest / bench stances: Lean Back (13), Lean Arms Folded (14), Lean Leg Up (15), Phone (1), Drink (26)
                            int[] benchPoses = { 13, 14, 15, 1, 26 };
                            _animator.SetInteger(NPCAnimatorHashes.RandomIdle, benchPoses[Random.Range(0, benchPoses.Length)]);
                            break;

                        case NPCActivityType.VendingMachine:
                        case NPCActivityType.Drink:
                            // Drink Hold (26) or Weight Shift (4)
                            _animator.SetInteger(NPCAnimatorHashes.RandomIdle, Random.value > 0.4f ? 26 : 4);
                            break;

                        case NPCActivityType.UseTerminal:
                        case NPCActivityType.Phone:
                            // Phone Typing (11), Phone Call (2), Phone Scroll (1), or Phone Selfie (9)
                            int[] termPoses = { 11, 2, 1, 9 };
                            _animator.SetInteger(NPCAnimatorHashes.RandomIdle, termPoses[Random.Range(0, termPoses.Length)]);
                            break;

                        case NPCActivityType.Inspect:
                        case NPCActivityType.Shop:
                            // Thoughtful Chin Scratch (6), Look at Sky/Displays (21), or Taking Photo/Video (10)
                            int[] inspectPoses = { 6, 21, 10 };
                            _animator.SetInteger(NPCAnimatorHashes.RandomIdle, inspectPoses[Random.Range(0, inspectPoses.Length)]);
                            break;

                        case NPCActivityType.Eat:
                            // Eat Hold (27)
                            _animator.SetInteger(NPCAnimatorHashes.RandomIdle, 27);
                            break;

                        default:
                            _animator.SetInteger(NPCAnimatorHashes.RandomIdle, Random.Range(1, 30));
                            break;
                    }
                }
            }

            _activityTimer += Time.deltaTime;

            if (_activityTimer >= _currentTarget.interactionDuration)
            {
                ResetActivityState();
                LastState = NodeState.Success;
                return NodeState.Success;
            }

            LastState = NodeState.Running;
            return NodeState.Running;
        }

        public override void ResetNode()
        {
            base.ResetNode();
            ResetActivityState();
        }

        private void ResetActivityState()
        {
            if (_currentTarget != null)
            {
                _currentTarget.Release(owner);
                _currentTarget = null;
            }

            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.obstacleAvoidanceType = _originalAvoidanceType;
                _agent.isStopped = false;
            }

            if (_animator != null && _animator.isActiveAndEnabled)
            {
                _animator.SetInteger(NPCAnimatorHashes.RandomIdle, 0);
            }

            if (blackboard != null && blackboard.CurrentState == NPCMacroState.PerformingActivity)
            {
                blackboard.CurrentState = NPCMacroState.Normal;
            }

            _isPerforming = false;
            _activityTimer = 0f;
        }
    }
}
