using UnityEngine;
using UnityEngine.AI;
using PolygonCyberCity.NPC.Spawner;

namespace PolygonCyberCity.NPC.BT.Tasks
{
    /// <summary>
    /// When cornered or under heavy fire, NPC drops into a defensive cowering posture.
    /// </summary>
    public class TaskCower : BTNode
    {
        private readonly NavMeshAgent _agent;
        private readonly Animator _animator;
        private readonly CyberNPCController _controller;
        private float _cowerDuration = 5.0f;
        private float _timer = 0f;

        public TaskCower(NavMeshAgent agent, Animator animator, CyberNPCController controller)
        {
            _agent = agent;
            _animator = animator;
            _controller = controller;
        }

        public override NodeState Evaluate()
        {
            if (blackboard == null) return NodeState.Failure;

            // Do not write any animator state on a dying/dead NPC —
            // this prevents the cower pose from being latched in on the lethal-hit frame.
            if (_controller != null && !_controller.IsAlive)
            {
                LastState = NodeState.Failure;
                return NodeState.Failure;
            }

            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                _agent.velocity = Vector3.zero;
            }

            if (_animator != null)
            {
                _animator.SetFloat("MoveSpeed", 0f);
                _animator.SetBool("IsWalking", false);
                _animator.SetBool("IsCrouching", true);
            }

            _timer += Time.deltaTime;
            blackboard.CurrentState = NPCMacroState.Cowering;

            // Look away from threat or keep trembling
            if (blackboard.PlayerTransform != null)
            {
                Vector3 lookAway = (owner.transform.position - blackboard.PlayerTransform.position).normalized;
                lookAway.y = 0;
                if (lookAway != Vector3.zero)
                {
                    owner.transform.rotation = Quaternion.Slerp(owner.transform.rotation, Quaternion.LookRotation(lookAway), Time.deltaTime * 3f);
                }
            }

            if (_timer >= _cowerDuration)
            {
                _timer = 0f;
                blackboard.IsCornered = false;
                if (_animator != null) _animator.SetBool("IsCrouching", false);
                LastState = NodeState.Success;
                return NodeState.Success;
            }

            LastState = NodeState.Running;
            return NodeState.Running;
        }

        public override void ResetNode()
        {
            base.ResetNode();
            _timer = 0f;
            if (_animator != null) _animator.SetBool("IsCrouching", false);
        }
    }
}
