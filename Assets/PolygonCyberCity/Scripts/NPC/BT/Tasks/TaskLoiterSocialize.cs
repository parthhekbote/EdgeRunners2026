using UnityEngine;
using UnityEngine.AI;

namespace PolygonCyberCity.NPC.BT.Tasks
{
    /// <summary>
    /// Ambient loitering and socialization task (checking cyberware, smoking, looking around).
    /// </summary>
    public class TaskLoiterSocialize : BTNode
    {
        private readonly NavMeshAgent _agent;
        private readonly Animator _animator;
        private float _loiterDuration;
        private float _timer = 0f;
        private bool _isLoitering = false;
#pragma warning disable CS0414 // assigned but never used — reserved for future fidget expansion
        private bool _fidgetPlayed = false;
#pragma warning restore CS0414

        public TaskLoiterSocialize(NavMeshAgent agent, Animator animator, float minDuration = 3.5f, float maxDuration = 9.0f)
        {
            _agent = agent;
            _animator = animator;
            _loiterDuration = Random.Range(minDuration, maxDuration);
        }

        public override NodeState Evaluate()
        {
            if (blackboard == null) return NodeState.Failure;

            if (!_isLoitering)
            {
                _isLoitering = true;
                _fidgetPlayed = false;
                _timer = 0f;
                _loiterDuration = Random.Range(3.0f, 6.5f);
                if (blackboard != null)
                {
                    blackboard.CurrentState = NPCMacroState.Loitering;
                }

                if (_agent != null && _agent.isOnNavMesh)
                {
                    _agent.isStopped = true;
                    _agent.velocity = Vector3.zero;
                }

                if (_animator != null && _animator.isActiveAndEnabled)
                {
                    _animator.SetFloat("MoveSpeed", 0f);
                    _animator.SetBool("IsWalking", false);
                    _animator.SetBool("IsStopped", true);
                    _animator.SetBool("MovementInputHeld", false);

                    // Pick an engaging urban loitering idle from full 29-clip palette
                    int[] richLoiterVariants = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29 };
                    int loiterVariant = richLoiterVariants[Random.Range(0, richLoiterVariants.Length)];
                    _animator.SetInteger("RandomIdle", loiterVariant);
                }
            }

            _timer += Time.deltaTime;

            // Smooth ambient look-around pivot
            if (Mathf.Sin(_timer * 0.8f) > 0.8f)
            {
                owner.transform.Rotate(Vector3.up, 12f * Time.deltaTime);
            }

            if (_timer >= _loiterDuration)
            {
                _isLoitering = false;
                _timer = 0f;
                if (_animator != null && _animator.isActiveAndEnabled)
                {
                    _animator.SetInteger("RandomIdle", 0);
                }
                if (blackboard != null && blackboard.CurrentState == NPCMacroState.Loitering)
                {
                    blackboard.CurrentState = NPCMacroState.Normal;
                }
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
            _isLoitering = false;
            _fidgetPlayed = false;
            _timer = 0f;
            if (_animator != null && _animator.isActiveAndEnabled)
            {
                _animator.SetInteger("RandomIdle", 0);
            }
            if (blackboard != null && blackboard.CurrentState == NPCMacroState.Loitering)
            {
                blackboard.CurrentState = NPCMacroState.Normal;
            }
            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = false;
            }
        }
    }
}
