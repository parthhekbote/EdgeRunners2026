using UnityEngine;
using UnityEngine.AI;

namespace PolygonCyberCity.NPC.BT.Tasks
{
    /// <summary>
    /// Tactical strafe and repositioning task for combat mercenaries and street enforcers.
    /// </summary>
    public class TaskTacticalReposition : BTNode
    {
        private readonly NavMeshAgent _agent;
        private readonly Animator _animator;
        private readonly float _preferredDistance;
        private float _strafeDirection = 1f;
        private float _directionChangeTimer = 0f;

        public TaskTacticalReposition(NavMeshAgent agent, Animator animator, float preferredDist = 3.5f)
        {
            _agent = agent;
            _animator = animator;
            _preferredDistance = preferredDist;
        }

        public override NodeState Evaluate()
        {
            if (blackboard == null || blackboard.PlayerTransform == null || _agent == null) return NodeState.Failure;

            if (!_agent.isOnNavMesh) return NodeState.Failure;

            float dist = blackboard.DistanceToPlayer;

            // In close melee range, ready to attack
            if (dist <= _preferredDistance + 0.5f && dist >= _preferredDistance - 0.5f)
            {
                LastState = NodeState.Success;
                return NodeState.Success;
            }

            _directionChangeTimer += Time.deltaTime;
            if (_directionChangeTimer > 2.5f)
            {
                _directionChangeTimer = 0f;
                _strafeDirection = Random.value > 0.5f ? 1f : -1f;
            }

            Vector3 toPlayer = (blackboard.PlayerTransform.position - owner.transform.position).normalized;
            Vector3 lateralDir = Vector3.Cross(Vector3.up, toPlayer) * _strafeDirection;

            Vector3 desiredPosition;
            if (dist > _preferredDistance + 1f)
            {
                // Advance toward player with slight lateral offset
                desiredPosition = blackboard.PlayerTransform.position - toPlayer * _preferredDistance + lateralDir * 1.5f;
            }
            else
            {
                // Back up slightly or circle
                desiredPosition = owner.transform.position - toPlayer * 2.0f + lateralDir * 2.0f;
            }

            if (NavMesh.SamplePosition(desiredPosition, out NavMeshHit hit, 4f, NavMesh.AllAreas))
            {
                _agent.isStopped = false;
                _agent.speed = blackboard.MoveSpeed * 1.4f;
                _agent.SetDestination(hit.position);
            }

            // Always face the player during combat repositioning
            Vector3 faceDir = (blackboard.PlayerTransform.position - owner.transform.position).normalized;
            faceDir.y = 0;
            if (faceDir != Vector3.zero)
            {
                owner.transform.rotation = Quaternion.Slerp(owner.transform.rotation, Quaternion.LookRotation(faceDir), Time.deltaTime * 6f);
            }

            if (_animator != null)
            {
                _animator.SetBool("IsStrafing", true);
            }

            LastState = NodeState.Running;
            return NodeState.Running;
        }

        public override void ResetNode()
        {
            base.ResetNode();
            if (_animator != null) _animator.SetBool("IsStrafing", false);
        }
    }
}
