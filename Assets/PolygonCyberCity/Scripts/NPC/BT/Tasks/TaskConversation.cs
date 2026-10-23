using UnityEngine;
using UnityEngine.AI;
using PolygonCyberCity.NPC.Animation;
using PolygonCyberCity.NPC.Spawner;

namespace PolygonCyberCity.NPC.BT.Tasks
{
    /// <summary>
    /// BT task handling NPC conversation state: paths to facing stance position, stops, turns to face partner,
    /// and triggers randomized upper-body speaker gestures and listener reactions based on LOD tier.
    /// Emits DialogueGesture animation events.
    /// </summary>
    public class TaskConversation : BTNode
    {
        private readonly NavMeshAgent _agent;
        private readonly Animator _animator;
        private readonly CyberNPCController _controller;

        private float _gestureTimer = 0f;
        private float _nextGestureInterval = 3.0f;
        private float _phaseOffset = 0f;

        public TaskConversation(NavMeshAgent agent, Animator animator, CyberNPCController controller)
        {
            _agent = agent;
            _animator = animator;
            _controller = controller;
            _phaseOffset = Random.Range(0f, 1.5f);
            _nextGestureInterval = Random.Range(2.5f, 4.5f) + _phaseOffset;
        }

        public override NodeState Evaluate()
        {
            if (blackboard == null || _controller == null || !_controller.IsInConversation)
            {
                LastState = NodeState.Failure;
                return NodeState.Failure;
            }

            var partner = _controller.ConversationPartner;
            if (partner == null || partner.IsDead || !_controller.gameObject.activeInHierarchy)
            {
                _controller.EndConversation();
                LastState = NodeState.Failure;
                return NodeState.Failure;
            }

            // If NPC was alerted or entered threat state, abort conversation
            if (blackboard.CurrentState != NPCMacroState.Normal)
            {
                _controller.EndConversation();
                LastState = NodeState.Failure;
                return NodeState.Failure;
            }

            // LOD check
            int lod = blackboard.CurrentLOD;

            // LOD 3: Skip visual rendering / locomotion entirely (state exists logically)
            if (lod >= 3)
            {
                if (_agent != null && _agent.isActiveAndEnabled && _agent.isOnNavMesh)
                {
                    _agent.isStopped = true;
                }
                LastState = NodeState.Running;
                return NodeState.Running;
            }

            // Locomotion handoff: path to target facing stance position on NavMesh
            Vector3 targetPos = _controller.ConversationTargetPosition;
            float distToTarget = Vector3.Distance(_controller.transform.position, targetPos);

            if (distToTarget > 0.6f && _agent != null && _agent.isActiveAndEnabled && _agent.isOnNavMesh)
            {
                _agent.isStopped = false;
                _agent.SetDestination(targetPos);
                _agent.speed = _controller.walkSpeed;

                if (_animator != null && _animator.isActiveAndEnabled)
                {
                    float speedNorm = Mathf.Clamp01(_agent.velocity.magnitude / Mathf.Max(_controller.walkSpeed, 1f));
                    _animator.SetFloat(NPCAnimatorHashes.MoveSpeed, speedNorm, 0.1f, Time.deltaTime);
                    _animator.SetBool(NPCAnimatorHashes.IsWalking, true);
                    _animator.SetBool(NPCAnimatorHashes.IsStopped, false);
                }
            }
            else
            {
                // Reached stance position: stop agent and face partner
                if (_agent != null && _agent.isActiveAndEnabled && _agent.isOnNavMesh)
                {
                    _agent.isStopped = true;
                    _agent.velocity = Vector3.zero;
                }

                if (_animator != null && _animator.isActiveAndEnabled)
                {
                    _animator.SetFloat(NPCAnimatorHashes.MoveSpeed, 0f, 0.12f, Time.deltaTime);
                    _animator.SetBool(NPCAnimatorHashes.IsWalking, false);
                    _animator.SetBool(NPCAnimatorHashes.IsStopped, true);
                    _animator.SetBool(NPCAnimatorHashes.MovementInputHeld, false);
                }

                // Mutual facing rotation towards partner
                Vector3 dirToPartner = (partner.transform.position - _controller.transform.position).normalized;
                dirToPartner.y = 0f;
                if (dirToPartner != Vector3.zero)
                {
                    Quaternion targetRot = Quaternion.LookRotation(dirToPartner);
                    _controller.transform.rotation = Quaternion.Slerp(_controller.transform.rotation, targetRot, Time.deltaTime * 6.0f);
                }

                // Upper-Body Gesture & Reaction handling (LOD 0 and LOD 1 only)
                if (lod <= 1)
                {
                    UpdateConversationGestures();
                }
            }

            LastState = NodeState.Running;
            return NodeState.Running;
        }

        private void UpdateConversationGestures()
        {
            if (_animator == null || !_animator.isActiveAndEnabled) return;

            _gestureTimer += Time.deltaTime;
            if (_gestureTimer >= _nextGestureInterval)
            {
                _gestureTimer = 0f;

                var role = _controller.ConversationRole;
                int gestureId;
                string gestureName;

                if (role == ConversationRole.Speaker)
                {
                    // Speaker gestures (Phase 18 randomized selection)
                    gestureId = Random.Range(0, 4); // 0=Nod, 1=Point, 2=Explain, 3=Shrug
                    gestureName = GetGestureName(gestureId);
                    _nextGestureInterval = Random.Range(2.5f, 4.8f) + _phaseOffset;
                }
                else
                {
                    // Listener reactions (Phase 18 randomized selection)
                    gestureId = Random.Range(4, 6); // 4=Agree/Nod/Laugh, 5=Argue/Disagree
                    gestureName = GetGestureName(gestureId);
                    _nextGestureInterval = Random.Range(3.5f, 6.5f) + _phaseOffset;
                }

                _animator.SetInteger(NPCAnimatorHashes.GestureID, gestureId);
                _animator.SetTrigger(NPCAnimatorHashes.Gesture);

                // Dispatch DialogueGesture event
                DispatchDialogueGestureEvent(gestureName);
            }
        }

        private string GetGestureName(int gestureId)
        {
            switch (gestureId)
            {
                case 0: return "TalkNod";
                case 1: return "Point";
                case 2: return "Explain";
                case 3: return "Shrug";
                case 4: return "AgreeReact";
                case 5: return "ArgueReact";
                default: return "TalkNod";
            }
        }

        private void DispatchDialogueGestureEvent(string gestureName)
        {
            if (_controller != null && _controller.animator != null)
            {
                var receiver = _controller.animator.GetComponent<NPCAnimationEventReceiver>();
                if (receiver != null)
                {
                    receiver.DialogueGesture(gestureName);
                }
            }
        }

        public override void ResetNode()
        {
            base.ResetNode();
            _gestureTimer = 0f;
        }
    }
}
