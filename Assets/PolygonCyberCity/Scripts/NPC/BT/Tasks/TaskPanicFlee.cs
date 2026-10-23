using UnityEngine;
using UnityEngine.AI;
using PolygonCyberCity.NPC.Spawner;

namespace PolygonCyberCity.NPC.BT.Tasks
{
    /// <summary>
    /// Calculates an escape vector away from the threat and flees at sprint speed.
    /// If cornered or unable to find a path, updates Blackboard.IsCornered to true.
    /// </summary>
    public class TaskPanicFlee : BTNode
    {
        private readonly NavMeshAgent _agent;
        private readonly Animator _animator;
        private readonly CyberNPCController _controller;
        private readonly float _fleeDistance;
        private readonly float _sprintSpeed;
        private Vector3 _fleeTarget;
        private float _repathTimer;

        public TaskPanicFlee(NavMeshAgent agent, Animator animator, CyberNPCController controller, float fleeDist = 28f, float sprintSpeed = 5.2f)
        {
            _agent = agent;
            _animator = animator;
            _controller = controller;
            _fleeDistance = fleeDist;
            _sprintSpeed = sprintSpeed;
        }

        public override NodeState Evaluate()
        {
            if (blackboard == null || _agent == null) return NodeState.Failure;

            // Determine threat position (last threat pos or player pos)
            Vector3 threatPos = blackboard.LastThreatPosition;
            if (threatPos == Vector3.zero && blackboard.PlayerTransform != null)
            {
                threatPos = blackboard.PlayerTransform.position;
            }

            if (threatPos == Vector3.zero) return NodeState.Failure;

            float distToThreat = Vector3.Distance(owner.transform.position, threatPos);

            // If safely away, flee succeeded
            if (distToThreat > _fleeDistance && !blackboard.WasAttacked)
            {
                blackboard.CurrentState = NPCMacroState.Normal;
                _agent.speed = blackboard.MoveSpeed;
                LastState = NodeState.Success;
                return NodeState.Success;
            }

            _repathTimer -= Time.deltaTime;
            if (_repathTimer <= 0f || !_agent.hasPath)
            {
                _repathTimer = 1.0f;

                // Calculate escape vector in opposite direction of threat
                Vector3 awayDir = (owner.transform.position - threatPos).normalized;
                // Add slight random deviation to prevent linear predictable paths
                awayDir += new Vector3(Random.Range(-0.35f, 0.35f), 0f, Random.Range(-0.35f, 0.35f));
                awayDir.Normalize();

                Vector3 targetCandidate = owner.transform.position + awayDir * _fleeDistance;

                if (NavMesh.SamplePosition(targetCandidate, out NavMeshHit hit, 15f, NavMesh.AllAreas))
                {
                    _fleeTarget = hit.position;
                    _agent.isStopped = false;
                    _agent.speed = _sprintSpeed;
                    _agent.SetDestination(_fleeTarget);
                    blackboard.IsCornered = false;
                }
                else
                {
                    // Check if trapped against wall
                    if (_agent.velocity.sqrMagnitude < 0.1f && distToThreat < 6f)
                    {
                        blackboard.IsCornered = true;
                        LastState = NodeState.Failure;
                        return NodeState.Failure;
                    }
                }
            }

            if (_controller != null)
            {
                _controller.TriggerPanicAlert();
            }

            LastState = NodeState.Running;
            return NodeState.Running;
        }

        public override void ResetNode()
        {
            base.ResetNode();
            _repathTimer = 0f;
            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.speed = blackboard != null ? blackboard.MoveSpeed : 1.8f;
            }
        }
    }
}
