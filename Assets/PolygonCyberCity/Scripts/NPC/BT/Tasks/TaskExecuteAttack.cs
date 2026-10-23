using UnityEngine;
using UnityEngine.AI;
using PolygonCyberCity.NPC.Spawner;

namespace PolygonCyberCity.NPC.BT.Tasks
{
    /// <summary>
    /// Executes melee or blaster attack against the player when within strike range.
    /// </summary>
    public class TaskExecuteAttack : BTNode
    {
        private readonly NavMeshAgent _agent;
        private readonly Animator _animator;
        private readonly CyberNPCController _controller;
        private readonly float _attackRange;
        private readonly float _attackDuration;
        private readonly float _damage;
        private float _attackTimer = 0f;
        private bool _damageApplied = false;

        public TaskExecuteAttack(NavMeshAgent agent, Animator animator, CyberNPCController controller, float range = 2.2f, float duration = 1.2f, float damage = 15f)
        {
            _agent = agent;
            _animator = animator;
            _controller = controller;
            _attackRange = range;
            _attackDuration = duration;
            _damage = damage;
        }

        public override NodeState Evaluate()
        {
            if (blackboard == null || blackboard.PlayerTransform == null) return NodeState.Failure;

            float dist = blackboard.DistanceToPlayer;
            if (dist > _attackRange + 0.8f && _attackTimer == 0f)
            {
                LastState = NodeState.Failure;
                return NodeState.Failure;
            }

            if (_attackTimer == 0f)
            {
                // Start attack
                _damageApplied = false;
                if (_agent != null && _agent.isOnNavMesh)
                {
                    _agent.isStopped = true;
                    _agent.velocity = Vector3.zero;
                }

                // Face player
                Vector3 toPlayer = (blackboard.PlayerTransform.position - owner.transform.position).normalized;
                toPlayer.y = 0;
                if (toPlayer != Vector3.zero)
                {
                    owner.transform.rotation = Quaternion.LookRotation(toPlayer);
                }

                if (_animator != null)
                {
                    int attackId = Random.Range(1, 8); // 1-7 (Light Combos, Heavy Cleaves, Stabs, Leaping, Flourish)
                    _animator.SetInteger("AttackID", attackId);
                    _animator.SetTrigger("Attack");
                }

                if (_controller != null)
                {
                    _controller.TriggerAttackAudio();
                }
            }

            _attackTimer += Time.deltaTime;

            // Lunge forward into the strike during wind-up
            if (_attackTimer >= _attackDuration * 0.15f && _attackTimer <= _attackDuration * 0.45f)
            {
                owner.transform.position += owner.transform.forward * 1.5f * Time.deltaTime;
            }

            // Apply damage midway through animation
            if (!_damageApplied && _attackTimer >= _attackDuration * 0.45f)
            {
                _damageApplied = true;
                if (Vector3.Distance(owner.transform.position, blackboard.PlayerTransform.position) <= _attackRange + 0.5f)
                {
                    var damageable = blackboard.PlayerTransform.GetComponent<SyntyCombined.ISyntyDamageable>();
                    if (damageable != null)
                    {
                        Vector3 hitDir = (blackboard.PlayerTransform.position - owner.transform.position).normalized;
                        damageable.TakeDamage(_damage, blackboard.PlayerTransform.position + Vector3.up, hitDir, false);

                        // Notify the enemy AI that it landed a hit — escalates threat meter
                        if (_controller != null)
                        {
                            var sword = owner.GetComponent<InsaneGun.Combat.InsaneGunSwordController>();
                            if (sword != null) sword.NotifyHitLanded();
                        }
                    }
                }
            }

            if (_attackTimer >= _attackDuration)
            {
                _attackTimer = 0f;
                _damageApplied = false;
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
            _attackTimer = 0f;
            _damageApplied = false;
            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = false;
            }
        }
    }
}
