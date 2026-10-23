using UnityEngine;
using UnityEngine.AI;

namespace PolygonCyberCity.NPC.BT.Tasks
{
    /// <summary>
    /// Pauses at neon shopfronts, vending machines, or street vendors to inspect wares.
    /// </summary>
    public class TaskInspectShopfront : BTNode
    {
        private readonly NavMeshAgent _agent;
        private readonly Animator _animator;
        private float _inspectDuration;
        private float _timer = 0f;
        private bool _isBrowsing = false;

        public TaskInspectShopfront(NavMeshAgent agent, Animator animator)
        {
            _agent = agent;
            _animator = animator;
        }

        public override NodeState Evaluate()
        {
            if (blackboard == null) return NodeState.Failure;

            // 25% chance to perform shop browsing when this branch triggers
            if (!_isBrowsing)
            {
                if (Random.value > 0.35f)
                {
                    LastState = NodeState.Failure;
                    return NodeState.Failure;
                }

                _isBrowsing = true;
                _timer = 0f;
                _inspectDuration = Random.Range(3.0f, 6.0f);

                if (_agent != null && _agent.isOnNavMesh)
                {
                    _agent.isStopped = true;
                    _agent.velocity = Vector3.zero;
                }

                if (_animator != null)
                {
                    _animator.SetFloat("MoveSpeed", 0f);
                    _animator.SetBool("IsWalking", false);
                }
            }

            _timer += Time.deltaTime;

            if (_timer >= _inspectDuration)
            {
                _isBrowsing = false;
                _timer = 0f;
                if (_agent != null && _agent.isOnNavMesh)
                {
                    _agent.isStopped = false;
                }
                LastState = NodeState.Success;
                return NodeState.Success;
            }

            LastState = NodeState.Running;
            return NodeState.Running;
        }

        public override void ResetNode()
        {
            base.ResetNode();
            _isBrowsing = false;
            _timer = 0f;
            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = false;
            }
        }
    }
}
