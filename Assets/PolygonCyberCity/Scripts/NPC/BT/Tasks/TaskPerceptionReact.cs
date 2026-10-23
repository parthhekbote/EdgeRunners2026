using UnityEngine;
using UnityEngine.AI;
using PolygonCyberCity.NPC.Spawner;
using PolygonCyberCity.NPC.Animation;

namespace PolygonCyberCity.NPC.BT.Tasks
{
    /// <summary>
    /// NPC plays physical perception reaction (suspicious stop/turn, gunshot duck/snap, explosion flinch)
    /// when threat alert is received.
    /// </summary>
    public class TaskPerceptionReact : BTNode
    {
        private readonly NavMeshAgent _agent;
        private readonly Animator _animator;
        private readonly CyberNPCController _controller;

        private float _reactTimer = 0f;
        private float _reactDuration = 1.8f;
        private bool _hasTriggered = false;

        public TaskPerceptionReact(NavMeshAgent agent, Animator animator, CyberNPCController controller)
        {
            _agent = agent;
            _animator = animator;
            _controller = controller;
        }

        public override NodeState Evaluate()
        {
            if (blackboard == null || _agent == null || !_agent.isActiveAndEnabled || !_agent.isOnNavMesh)
            {
                LastState = NodeState.Failure;
                return NodeState.Failure;
            }

            if (blackboard.CurrentState != NPCMacroState.Suspicious)
            {
                _hasTriggered = false;
                LastState = NodeState.Failure;
                return NodeState.Failure;
            }

            if (!_hasTriggered)
            {
                _hasTriggered = true;
                _reactTimer = 0f;

                if (_agent != null && _agent.isOnNavMesh)
                {
                    _agent.isStopped = true;
                    _agent.velocity = Vector3.zero;
                }

                int perceptionType = blackboard.Get<int>("PerceptionType");

                if (_animator != null)
                {
                    _animator.SetFloat(NPCAnimatorHashes.MoveSpeed, 0f);
                    _animator.SetBool(NPCAnimatorHashes.IsWalking, false);
                    _animator.SetBool(NPCAnimatorHashes.IsStopped, true);
                    _animator.SetInteger(NPCAnimatorHashes.PerceptionType, perceptionType);
                    _animator.SetTrigger(NPCAnimatorHashes.PerceptionReact);
                }
            }

            // Face threat location if available
            if (blackboard.LastThreatPosition != Vector3.zero)
            {
                Vector3 lookDir = (blackboard.LastThreatPosition - owner.transform.position).normalized;
                lookDir.y = 0;
                if (lookDir != Vector3.zero)
                {
                    owner.transform.rotation = Quaternion.Slerp(owner.transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 6f);
                }
            }

            _reactTimer += Time.deltaTime;

            if (_reactTimer >= _reactDuration)
            {
                _hasTriggered = false;
                LastState = NodeState.Success;
                return NodeState.Success;
            }

            LastState = NodeState.Running;
            return NodeState.Running;
        }

        public override void ResetNode()
        {
            base.ResetNode();
            _hasTriggered = false;
            _reactTimer = 0f;
        }
    }
}
