using UnityEngine;

namespace PolygonCyberCity.NPC.BT.Tasks
{
    /// <summary>
    /// Smoothly rotates the NPC / head toward the player when they walk by casually in LOD 0/1.
    /// </summary>
    public class TaskCasualLookAt : BTNode
    {
        private readonly float _maxLookDistance;

        public TaskCasualLookAt(float maxDistance = 4.5f)
        {
            _maxLookDistance = maxDistance;
        }

        public override NodeState Evaluate()
        {
            if (blackboard == null || blackboard.PlayerTransform == null) return NodeState.Failure;

            // Only perform IK / look rotations in LOD 0 (close range) to save CPU
            if (blackboard.CurrentLOD > 0) return NodeState.Failure;

            float dist = blackboard.DistanceToPlayer;
            if (dist > _maxLookDistance || dist < 0.5f) return NodeState.Failure;

            Vector3 toPlayer = (blackboard.PlayerTransform.position - owner.transform.position).normalized;
            toPlayer.y = 0;

            if (toPlayer != Vector3.zero)
            {
                // Smoothly glance toward player
                Quaternion targetRot = Quaternion.LookRotation(toPlayer);
                owner.transform.rotation = Quaternion.Slerp(owner.transform.rotation, targetRot, Time.deltaTime * 2.5f);
            }

            LastState = NodeState.Success;
            return NodeState.Success;
        }
    }
}
