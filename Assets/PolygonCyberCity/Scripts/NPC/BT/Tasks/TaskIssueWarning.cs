using UnityEngine;
using UnityEngine.AI;
using PolygonCyberCity.NPC.Spawner;

namespace PolygonCyberCity.NPC.BT.Tasks
{
    /// <summary>
    /// Guard/Mercenary warning task: Stiffens posture, faces player, issues warning.
    /// Escalates to Aggressive if player stays or attacks; returns to Normal if player backs off.
    /// </summary>
    public class TaskIssueWarning : BTNode
    {
        private readonly NavMeshAgent _agent;
        private readonly Animator _animator;
        private readonly CyberNPCController _controller;
        private float _warningTimer = 0f;
        private const float EscalationThreshold = 3.5f;

        public TaskIssueWarning(NavMeshAgent agent, Animator animator, CyberNPCController controller)
        {
            _agent = agent;
            _animator = animator;
            _controller = controller;
        }

        public override NodeState Evaluate()
        {
            if (blackboard == null || blackboard.PlayerTransform == null) return NodeState.Failure;

            float dist = blackboard.DistanceToPlayer;

            // If player left warning radius (e.g. > 8m), de-escalate back to normal
            if (dist > 8.0f)
            {
                _warningTimer = 0f;
                blackboard.CurrentState = NPCMacroState.Normal;
                blackboard.WeaponDrawn = false;
                if (_animator != null) _animator.SetBool("SwordDrawn", false);
                LastState = NodeState.Success;
                return NodeState.Success;
            }

            // Face player
            Vector3 toPlayer = (blackboard.PlayerTransform.position - owner.transform.position).normalized;
            toPlayer.y = 0;
            if (toPlayer != Vector3.zero)
            {
                owner.transform.rotation = Quaternion.Slerp(owner.transform.rotation, Quaternion.LookRotation(toPlayer), Time.deltaTime * 5f);
            }

            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                _agent.velocity = Vector3.zero;
            }

            blackboard.CurrentState = NPCMacroState.Suspicious;
            blackboard.WeaponDrawn = true;

            if (_animator != null)
            {
                _animator.SetFloat("MoveSpeed", 0f);
                _animator.SetBool("IsWalking", false);
                _animator.SetBool("SwordDrawn", true);
            }

            _warningTimer += Time.deltaTime;

            if (_controller != null)
            {
                _controller.TriggerWarningCallout();
            }

            // Escalation check
            if (_warningTimer >= EscalationThreshold || blackboard.WasAttacked)
            {
                _warningTimer = 0f;
                blackboard.CurrentState = NPCMacroState.Aggressive;
                LastState = NodeState.Success;
                return NodeState.Success;
            }

            LastState = NodeState.Running;
            return NodeState.Running;
        }

        public override void ResetNode()
        {
            base.ResetNode();
            _warningTimer = 0f;
        }
    }
}
