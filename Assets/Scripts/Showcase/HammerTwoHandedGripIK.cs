using UnityEngine;

namespace PolygonMech
{
    /// <summary>
    /// Enforces two-handed grip on the war hammer weapon shaft via Mecanim humanoid inverse kinematics.
    /// Locks the left hand to an attach point on the hammer during two-handed stances and swings,
    /// while smoothly fading weight to 0 during sheathed or death animations.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class HammerTwoHandedGripIK : MonoBehaviour
    {
        [Header("IK Targets")]
        [Tooltip("Transform socket on the hammer shaft where the left hand should grip.")]
        public Transform leftHandGripTarget;

        [Tooltip("Optional elbow pole hint for natural arm bending.")]
        public Transform leftElbowHintTarget;

        [Header("IK Weights")]
        [Range(0f, 1f)] public float leftHandPositionWeight = 1f;
        [Range(0f, 1f)] public float leftHandRotationWeight = 1f;
        [Range(0f, 1f)] public float leftElbowHintWeight = 0f;

        [Header("Automatic State Blending")]
        public bool autoBlendForSheath = true;
        public float blendSpeed = 8f;

        [Header("Reach Protection")]
        [Tooltip("Maximum allowed reach distance before soft-fading IK to prevent mesh stretching.")]
        public float maxReachDistance = 2.2f;

        private Animator animator;
        private Transform leftShoulder;
        private float currentWeight = 1f;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            FindLeftShoulder();
        }

        private void Start()
        {
            if (leftShoulder == null) FindLeftShoulder();
        }

        private void FindLeftShoulder()
        {
            if (animator != null && animator.isHuman)
            {
                leftShoulder = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                if (leftShoulder == null)
                {
                    leftShoulder = animator.GetBoneTransform(HumanBodyBones.LeftShoulder);
                }
            }
        }

        private void Update()
        {
            if (!autoBlendForSheath || animator == null) return;

            var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            bool isSheathedOrDying = stateInfo.IsName("Idle-Sheathed") ||
                                    stateInfo.IsName("Walk-Sheathed") ||
                                    stateInfo.IsName("Run-Sheathed") ||
                                    stateInfo.IsName("WalkSlow-Sheathed") ||
                                    stateInfo.IsName("WeaponSheath") ||
                                    stateInfo.IsName("WeaponUnsheath") ||
                                    stateInfo.IsName("Death") ||
                                    stateInfo.IsName("Revive");

            float targetWeight = isSheathedOrDying ? 0f : 1f;

            // Check reach distance to avoid hyperextension
            if (targetWeight > 0f && leftShoulder != null && leftHandGripTarget != null)
            {
                float dist = Vector3.Distance(leftShoulder.position, leftHandGripTarget.position);
                if (dist > maxReachDistance)
                {
                    // Soft falloff beyond maxReachDistance
                    float overflow = dist - maxReachDistance;
                    float reachFactor = Mathf.Clamp01(1f - overflow * 2.5f);
                    targetWeight *= reachFactor;
                }
            }

            currentWeight = Mathf.MoveTowards(currentWeight, targetWeight, Time.deltaTime * blendSpeed);
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (animator == null || leftHandGripTarget == null) return;

            float posWeight = leftHandPositionWeight * currentWeight;
            float rotWeight = leftHandRotationWeight * currentWeight;

            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, posWeight);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, rotWeight);

            if (posWeight > 0.001f)
            {
                animator.SetIKPosition(AvatarIKGoal.LeftHand, leftHandGripTarget.position);
            }

            if (rotWeight > 0.001f)
            {
                animator.SetIKRotation(AvatarIKGoal.LeftHand, leftHandGripTarget.rotation);
            }

            if (leftElbowHintTarget != null && leftElbowHintWeight > 0.001f)
            {
                animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, leftElbowHintWeight * currentWeight);
                animator.SetIKHintPosition(AvatarIKHint.LeftElbow, leftElbowHintTarget.position);
            }
        }

        public void SetGripTarget(Transform target)
        {
            leftHandGripTarget = target;
        }

        public void SetWeight(float weight)
        {
            leftHandPositionWeight = weight;
            leftHandRotationWeight = weight;
        }
    }
}
