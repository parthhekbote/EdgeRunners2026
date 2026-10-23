using UnityEngine;
using UnityEngine.AI;
using PolygonCyberCity.NPC.Spawner;
using PolygonCyberCity.NPC.Animation;
using SyntyCombined;

namespace PolygonCyberCity.NPC.BT.Tasks
{
    /// <summary>
    /// Defensive tactical reaction (Block, Parry, Dodge) for combat-capable NPCs.
    /// </summary>
    public class TaskDefend : BTNode
    {
        private readonly NavMeshAgent _agent;
        private readonly Animator _animator;
        private readonly CyberNPCController _controller;

        private float _defenseTimer = 0f;
        private float _defenseDuration = 1.2f;
        private bool _isDefending = false;

        public TaskDefend(NavMeshAgent agent, Animator animator, CyberNPCController controller)
        {
            _agent = agent;
            _animator = animator;
            _controller = controller;
        }

        public override NodeState Evaluate()
        {
            if (blackboard == null || _controller == null || _controller.animationProfile == null)
            {
                LastState = NodeState.Failure;
                return NodeState.Failure;
            }

            var profile = _controller.animationProfile;
            if (!profile.canBlock && !profile.canParry && !profile.canDodge)
            {
                LastState = NodeState.Failure;
                return NodeState.Failure;
            }

            if (!_isDefending)
            {
                _isDefending = true;
                _defenseTimer = 0f;

                bool rolledBlock = profile.canBlock && (Random.value <= profile.blockChance);
                bool rolledParry = profile.canParry && (Random.value <= profile.parryChance);
                bool rolledDodge = profile.canDodge && (Random.value <= profile.dodgeChance);

                if (rolledDodge)
                {
                    PerformDodge(profile);
                    _defenseDuration = 0.8f;
                }
                else if (rolledParry)
                {
                    PerformParry(profile);
                    _defenseDuration = 1.0f;
                }
                else if (rolledBlock)
                {
                    PerformBlock();
                    _defenseDuration = Random.Range(0.8f, 1.8f);
                }
                else
                {
                    _isDefending = false;
                    LastState = NodeState.Failure;
                    return NodeState.Failure;
                }
            }

            // Face threat while defending
            if (blackboard.PlayerTransform != null)
            {
                Vector3 lookDir = (blackboard.PlayerTransform.position - owner.transform.position).normalized;
                lookDir.y = 0;
                if (lookDir != Vector3.zero)
                {
                    owner.transform.rotation = Quaternion.Slerp(owner.transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 8f);
                }
            }

            _defenseTimer += Time.deltaTime;

            if (_defenseTimer >= _defenseDuration)
            {
                EndDefense();
                LastState = NodeState.Success;
                return NodeState.Success;
            }

            LastState = NodeState.Running;
            return NodeState.Running;
        }

        private void PerformBlock()
        {
            blackboard.Set("IsBlocking", true);
            if (_animator != null && _animator.isActiveAndEnabled)
            {
                _animator.SetTrigger(NPCAnimatorHashes.BlockBegin);
            }
        }

        private void PerformParry(NPCAnimationProfile profile)
        {
            blackboard.Set("IsBlocking", true);
            if (_animator != null && _animator.isActiveAndEnabled && profile.combatClipData != null)
            {
                var parryEntry = SyntyCombatAnimationResolver.GetParry(profile.combatClipData, CardinalDirection.Front);
                if (parryEntry.clip != null)
                {
                    _animator.CrossFadeInFixedTime(parryEntry.clip.name, 0.05f);
                }
                else
                {
                    _animator.SetTrigger(NPCAnimatorHashes.BlockBegin);
                }
            }
        }

        private void PerformDodge(NPCAnimationProfile profile)
        {
            if (_animator != null && _animator.isActiveAndEnabled && profile.combatClipData != null)
            {
                var dodgeEntry = SyntyCombatAnimationResolver.GetDodge(profile.combatClipData, CardinalDirection.Back, false);
                if (dodgeEntry.clip != null)
                {
                    _animator.CrossFadeInFixedTime(dodgeEntry.clip.name, 0.05f);
                }
                else
                {
                    _animator.SetInteger(NPCAnimatorHashes.DodgeDirection, 1);
                    _animator.SetTrigger(NPCAnimatorHashes.Dodge);
                }
            }
        }

        private void EndDefense()
        {
            _isDefending = false;
            _defenseTimer = 0f;
            blackboard.Set("IsBlocking", false);
            if (_animator != null && _animator.isActiveAndEnabled)
            {
                _animator.SetTrigger(NPCAnimatorHashes.BlockEnd);
            }
        }

        public override void ResetNode()
        {
            base.ResetNode();
            EndDefense();
        }
    }
}
