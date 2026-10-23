using System.Collections;
using UnityEngine;

namespace Alita
{
    /// <summary>
    /// AAA-quality procedural animation, bone posing, and attachment rig
    /// for Dr. Dyson Ido cradling and carrying Alita in the rain cutscene.
    /// Handles:
    /// 1. Smoothly lifting Alita from the scrap dune into his arms.
    /// 2. Locking Dr. Ido's arms in a steady, protective cradle hold without walk-cycle arm swing jitter.
    /// 3. Parenting Alita's movement to Dr. Ido's chest bone for 100% natural organic body sway.
    /// 4. Posing Alita's limbs in a relaxed, serene sleeping posture while being carried.
    /// </summary>
    public class IdoCarryAlitaRig : MonoBehaviour
    {
        [Header("Actors")]
        public GameObject doctorActor;
        public GameObject alitaCyborg;
        public Animator doctorAnimator;
        public Animator alitaAnimator;

        [Header("Carry Rig State")]
        public bool isCarrying = false;
        [Range(0f, 1f)] public float carryPoseWeight = 0f;
        [Range(0f, 1f)] public float pickupProgress = 0f;

        [Header("Carry Socket (Chest Bone Anchor)")]
        public Transform chestSocket;
        public Vector3 localCarryOffset = new Vector3(0.06f, -0.14f, 0.36f);
        public Vector3 localCarryEuler = new Vector3(-123f, -6f, 117f);

        [Header("Locomotion")]
        public bool isWalkingWithAlita = false;
        public float carryWalkSpeed = 1.0f;

        // Bone Transforms for Doctor Ido
        private Transform _docSpine;
        private Transform _docChest;
        private Transform _docLeftUpperArm;
        private Transform _docLeftLowerArm;
        private Transform _docLeftHand;
        private Transform _docRightUpperArm;
        private Transform _docRightLowerArm;
        private Transform _docRightHand;

        // Bone Transforms for Alita
        private Transform _alitaHead;
        private Transform _alitaTorso;
        private Transform _alitaLeftUpperArm;
        private Transform _alitaLeftLowerArm;
        private Transform _alitaLeftHand;
        private Transform _alitaRightUpperArm;
        private Transform _alitaRightLowerArm;
        private Transform _alitaRightHand;
        private Transform _alitaLeftUpperLeg;
        private Transform _alitaLeftLowerLeg;
        private Transform _alitaLeftFoot;
        private Transform _alitaRightUpperLeg;
        private Transform _alitaRightLowerLeg;
        private Transform _alitaRightFoot;

        private Vector3 _originalAlitaWorldPos;
        private Quaternion _originalAlitaWorldRot;
        private Rigidbody[] _alitaRigidbodies;
        private Collider[] _alitaColliders;
        private SkinnedMeshRenderer _alitaSMR;

        // Doctor Target Cradle Rotations (Polygon Humanoid Rig)
        // Left arm cradles Alita's head and upper torso from the left side
        private static readonly Quaternion DocLeftUpperArmCradle = Quaternion.Euler(-25f, 45f, 40f);
        private static readonly Quaternion DocLeftLowerArmCradle = Quaternion.Euler(10f, 75f, 15f);
        private static readonly Quaternion DocLeftHandCradle     = Quaternion.Euler(-10f, 15f, 0f);

        // Right arm cradles Alita's lower back and waist from the right side, cupped upward with elbow bent naturally
        private static readonly Quaternion DocRightUpperArmCradle = Quaternion.Euler(21f, 81f, 39f);
        private static readonly Quaternion DocRightLowerArmCradle = Quaternion.Euler(-21f, 93f, -35f);
        private static readonly Quaternion DocRightHandCradle     = Quaternion.Euler(-20f, 0f, 0f);

        // Alita Target Carried Posture Rotations
        private static readonly Quaternion AlitaHeadCradle = Quaternion.Euler(-8f, 25f, 10f);
        private static readonly Quaternion AlitaLeftUpperArmCradle = Quaternion.Euler(30f, 20f, 65f);
        private static readonly Quaternion AlitaLeftLowerArmCradle = Quaternion.Euler(-15f, 35f, 10f);
        private static readonly Quaternion AlitaLeftHandCradle = Quaternion.Euler(10f, 5f, 0f);

        private static readonly Quaternion AlitaRightUpperArmCradle = Quaternion.Euler(35f, -25f, 65f);
        private static readonly Quaternion AlitaRightLowerArmCradle = Quaternion.Euler(20f, -35f, -10f);
        private static readonly Quaternion AlitaRightHandCradle = Quaternion.Euler(10f, -5f, 0f);

        private static readonly Quaternion AlitaLeftUpperLegCradle = Quaternion.Euler(25f, 8f, 0f);
        private static readonly Quaternion AlitaLeftLowerLegCradle = Quaternion.Euler(-20f, 0f, 0f);
        private static readonly Quaternion AlitaLeftFootCradle = Quaternion.Euler(15f, 0f, 0f);

        private static readonly Quaternion AlitaRightUpperLegCradle = Quaternion.Euler(22f, -8f, 0f);
        private static readonly Quaternion AlitaRightLowerLegCradle = Quaternion.Euler(-18f, 0f, 0f);
        private static readonly Quaternion AlitaRightFootCradle = Quaternion.Euler(15f, 0f, 0f);

        private void Start()
        {
            InitializeRig();
        }

        public void InitializeRig()
        {
            if (doctorActor == null) doctorActor = gameObject;
            if (doctorAnimator == null) doctorAnimator = doctorActor.GetComponentInChildren<Animator>();

            if (doctorAnimator != null)
            {
                _docSpine = doctorAnimator.GetBoneTransform(HumanBodyBones.Spine);
                _docChest = doctorAnimator.GetBoneTransform(HumanBodyBones.Chest) ?? _docSpine;

                _docLeftUpperArm = doctorAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                _docLeftLowerArm = doctorAnimator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                _docLeftHand = doctorAnimator.GetBoneTransform(HumanBodyBones.LeftHand);

                _docRightUpperArm = doctorAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                _docRightLowerArm = doctorAnimator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                _docRightHand = doctorAnimator.GetBoneTransform(HumanBodyBones.RightHand);

                // Ensure attachment socket on Doctor chest
                if (_docChest != null && chestSocket == null)
                {
                    Transform existing = _docChest.Find("Alita_Carry_Socket");
                    if (existing != null)
                    {
                        chestSocket = existing;
                    }
                    else
                    {
                        var socketGo = new GameObject("Alita_Carry_Socket");
                        chestSocket = socketGo.transform;
                        chestSocket.SetParent(_docChest, false);
                    }
                    chestSocket.localPosition = localCarryOffset;
                    chestSocket.localEulerAngles = localCarryEuler;
                }
            }

            if (alitaCyborg == null)
            {
                alitaCyborg = GameObject.Find("Alita_Cyborg_Character") ?? GameObject.Find("Alita_Broken_Cyborg");
            }

            if (alitaCyborg != null)
            {
                _originalAlitaWorldPos = alitaCyborg.transform.position;
                _originalAlitaWorldRot = alitaCyborg.transform.rotation;
                _alitaRigidbodies = alitaCyborg.GetComponentsInChildren<Rigidbody>();
                _alitaColliders = alitaCyborg.GetComponentsInChildren<Collider>();
                _alitaSMR = alitaCyborg.GetComponentInChildren<SkinnedMeshRenderer>();

                alitaAnimator = alitaCyborg.GetComponent<Animator>();
                if (alitaAnimator != null)
                {
                    _alitaHead = alitaAnimator.GetBoneTransform(HumanBodyBones.Head);
                    _alitaTorso = alitaAnimator.GetBoneTransform(HumanBodyBones.Spine)
                               ?? alitaAnimator.GetBoneTransform(HumanBodyBones.Chest)
                               ?? alitaAnimator.GetBoneTransform(HumanBodyBones.Hips);
                    _alitaLeftUpperArm = alitaAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                    _alitaLeftLowerArm = alitaAnimator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                    _alitaLeftHand = alitaAnimator.GetBoneTransform(HumanBodyBones.LeftHand);
                    _alitaRightUpperArm = alitaAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                    _alitaRightLowerArm = alitaAnimator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                    _alitaRightHand = alitaAnimator.GetBoneTransform(HumanBodyBones.RightHand);

                    _alitaLeftUpperLeg = alitaAnimator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                    _alitaLeftLowerLeg = alitaAnimator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                    _alitaLeftFoot = alitaAnimator.GetBoneTransform(HumanBodyBones.LeftFoot);
                    _alitaRightUpperLeg = alitaAnimator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                    _alitaRightLowerLeg = alitaAnimator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
                    _alitaRightFoot = alitaAnimator.GetBoneTransform(HumanBodyBones.RightFoot);
                }

                // Robust name fallbacks for Alita bones if avatar mapping is partial
                if (_alitaTorso == null)
                {
                    _alitaTorso = FindChildRecursive(alitaCyborg.transform, "spine_02")
                               ?? FindChildRecursive(alitaCyborg.transform, "spine_01")
                               ?? FindChildRecursive(alitaCyborg.transform, "Spine_02")
                               ?? FindChildRecursive(alitaCyborg.transform, "Spine_01")
                               ?? FindChildRecursive(alitaCyborg.transform, "pelvis")
                               ?? FindChildRecursive(alitaCyborg.transform, "Hips");
                }
                if (_alitaHead == null) _alitaHead = FindChildRecursive(alitaCyborg.transform, "head") ?? FindChildRecursive(alitaCyborg.transform, "Head");
                if (_alitaRightUpperArm == null) _alitaRightUpperArm = FindChildRecursive(alitaCyborg.transform, "upperarm_r") ?? FindChildRecursive(alitaCyborg.transform, "UpperArm_R");
                if (_alitaRightLowerArm == null) _alitaRightLowerArm = FindChildRecursive(alitaCyborg.transform, "lowerarm_r") ?? FindChildRecursive(alitaCyborg.transform, "LowerArm_R");
                if (_alitaRightHand == null) _alitaRightHand = FindChildRecursive(alitaCyborg.transform, "hand_r") ?? FindChildRecursive(alitaCyborg.transform, "Hand_R");
                if (_alitaLeftUpperArm == null) _alitaLeftUpperArm = FindChildRecursive(alitaCyborg.transform, "upperarm_l") ?? FindChildRecursive(alitaCyborg.transform, "UpperArm_L");
                if (_alitaLeftLowerArm == null) _alitaLeftLowerArm = FindChildRecursive(alitaCyborg.transform, "lowerarm_l") ?? FindChildRecursive(alitaCyborg.transform, "LowerArm_L");
                if (_alitaLeftHand == null) _alitaLeftHand = FindChildRecursive(alitaCyborg.transform, "hand_l") ?? FindChildRecursive(alitaCyborg.transform, "Hand_L");
            }
        }

        public void StartPickupSequence(System.Action onComplete = null)
        {
            InitializeRig();
            StartCoroutine(PickupRoutine(onComplete));
        }

        private IEnumerator PickupRoutine(System.Action onComplete)
        {
            isCarrying = true;
            pickupProgress = 0f;

            if (alitaCyborg != null)
            {
                _originalAlitaWorldPos = alitaCyborg.transform.position;
                _originalAlitaWorldRot = alitaCyborg.transform.rotation;
                SetAlitaRagdollKinematic(true);
            }

            float duration = 4.2f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                pickupProgress = Mathf.Clamp01(elapsed / duration);

                // Smooth S-curve for lifting weight
                carryPoseWeight = Mathf.SmoothStep(0f, 1f, pickupProgress);

                yield return null;
            }

            carryPoseWeight = 1.0f;
            pickupProgress = 1.0f;
            isWalkingWithAlita = true;
            onComplete?.Invoke();
        }

        public void SetAlitaRagdollKinematic(bool kinematic)
        {
            if (alitaCyborg == null) return;
            if (_alitaRigidbodies == null || _alitaRigidbodies.Length == 0)
            {
                _alitaRigidbodies = alitaCyborg.GetComponentsInChildren<Rigidbody>();
                _alitaColliders = alitaCyborg.GetComponentsInChildren<Collider>();
            }

            foreach (var rb in _alitaRigidbodies)
            {
                if (rb != null) rb.isKinematic = kinematic;
            }
            foreach (var col in _alitaColliders)
            {
                if (col != null) col.enabled = !kinematic;
            }
        }

        private void LateUpdate()
        {
            if (!isCarrying || carryPoseWeight <= 0.001f) return;

            // 1. Lock Doctor's arms in solid cradle hold
            ApplyDoctorCradleArms();

            // 2. Attach Alita to Doctor's chest socket
            UpdateAlitaAttachedPosition();

            // 3. Pose Alita's limbs into serene sleeping cradle pose
            ApplyAlitaCarriedPose();
        }

        private void ApplyDoctorCradleArms()
        {
            if (doctorActor == null) return;
            if (_docRightUpperArm == null || _docLeftUpperArm == null) InitializeRig();

            // If fully carrying, lock directly so Mecanim walk arm swing cannot cause jitter
            if (carryPoseWeight >= 0.98f)
            {
                if (_docLeftUpperArm != null) _docLeftUpperArm.localRotation = DocLeftUpperArmCradle;
                if (_docLeftLowerArm != null) _docLeftLowerArm.localRotation = DocLeftLowerArmCradle;
                if (_docLeftHand != null) _docLeftHand.localRotation = DocLeftHandCradle;

                if (_docRightUpperArm != null) _docRightUpperArm.localRotation = DocRightUpperArmCradle;
                if (_docRightLowerArm != null) _docRightLowerArm.localRotation = DocRightLowerArmCradle;
                if (_docRightHand != null) _docRightHand.localRotation = DocRightHandCradle;
            }
            else
            {
                // Smooth blend during pickup
                if (_docLeftUpperArm != null) _docLeftUpperArm.localRotation = Quaternion.Slerp(_docLeftUpperArm.localRotation, DocLeftUpperArmCradle, carryPoseWeight);
                if (_docLeftLowerArm != null) _docLeftLowerArm.localRotation = Quaternion.Slerp(_docLeftLowerArm.localRotation, DocLeftLowerArmCradle, carryPoseWeight);
                if (_docLeftHand != null) _docLeftHand.localRotation = Quaternion.Slerp(_docLeftHand.localRotation, DocLeftHandCradle, carryPoseWeight);

                if (_docRightUpperArm != null) _docRightUpperArm.localRotation = Quaternion.Slerp(_docRightUpperArm.localRotation, DocRightUpperArmCradle, carryPoseWeight);
                if (_docRightLowerArm != null) _docRightLowerArm.localRotation = Quaternion.Slerp(_docRightLowerArm.localRotation, DocRightLowerArmCradle, carryPoseWeight);
                if (_docRightHand != null) _docRightHand.localRotation = Quaternion.Slerp(_docRightHand.localRotation, DocRightHandCradle, carryPoseWeight);
            }

            // Slight protective spine lean
            if (_docSpine != null)
            {
                Quaternion protectiveLean = Quaternion.Euler(-4f * carryPoseWeight, 0f, 0f);
                _docSpine.localRotation = protectiveLean * _docSpine.localRotation;
            }
        }

        private void UpdateAlitaAttachedPosition()
        {
            if (alitaCyborg == null || doctorActor == null) return;
            if (_docChest == null) InitializeRig();

            // 1. Target world rotation for Alita cradled horizontally
            Quaternion targetWorldRot;
            if (_docChest != null)
            {
                targetWorldRot = _docChest.rotation * Quaternion.Euler(localCarryEuler);
            }
            else
            {
                targetWorldRot = doctorActor.transform.rotation * Quaternion.Euler(localCarryEuler);
            }

            // 2. Calculate dynamic forearm cradle shelf position directly between Dr. Ido's arms
            Vector3 armsCradlePos;
            if (_docLeftLowerArm != null && _docRightLowerArm != null && _docChest != null)
            {
                Vector3 leftArmMid = (_docLeftHand != null) ? (_docLeftLowerArm.position + _docLeftHand.position) * 0.5f : _docLeftLowerArm.position;
                Vector3 rightArmMid = (_docRightHand != null) ? (_docRightLowerArm.position + _docRightHand.position) * 0.5f : _docRightLowerArm.position;
                Vector3 forearmCenter = (leftArmMid + rightArmMid) * 0.5f;

                // Shelf position: resting directly on forearms, tucked securely against chest
                armsCradlePos = forearmCenter
                    + _docChest.up * 0.04f
                    + _docChest.forward * 0.05f;
            }
            else if (_docChest != null)
            {
                armsCradlePos = _docChest.position + _docChest.forward * 0.36f - _docChest.up * 0.14f;
            }
            else
            {
                armsCradlePos = doctorActor.transform.position + doctorActor.transform.forward * 0.36f + doctorActor.transform.up * 1.22f;
            }

            // 3. Compensate for the distance between Alita's root pivot (feet origin) and her actual torso,
            // so her actual body (spine/chest/pelvis) rests DIRECTLY on Dr. Ido's forearms and between his hands!
            float torsoHeight = 1.05f;
            if (_alitaTorso != null)
            {
                float measured = alitaCyborg.transform.InverseTransformPoint(_alitaTorso.position).y;
                if (measured > 0.4f && measured < 1.6f) torsoHeight = measured;
            }
            Vector3 worldTorsoOffset = targetWorldRot * new Vector3(0f, torsoHeight, 0f);
            Vector3 targetWorldPos = armsCradlePos - worldTorsoOffset;

            // Keep chestSocket synchronized for inspector/scene references
            if (chestSocket != null)
            {
                chestSocket.position = armsCradlePos;
                chestSocket.rotation = targetWorldRot;
            }

            // Smooth liftoff from scrap ground into Dr. Ido's arms
            if (pickupProgress < 1.0f)
            {
                float liftArc = Mathf.SmoothStep(0f, 1f, pickupProgress);
                // Gentle parabolic arc during the lift
                float arcY = Mathf.Sin(pickupProgress * Mathf.PI) * 0.12f;
                alitaCyborg.transform.position = Vector3.Lerp(_originalAlitaWorldPos, targetWorldPos, liftArc) + Vector3.up * arcY;
                alitaCyborg.transform.rotation = Quaternion.Slerp(_originalAlitaWorldRot, targetWorldRot, liftArc);
            }
            else
            {
                alitaCyborg.transform.position = targetWorldPos;
                alitaCyborg.transform.rotation = targetWorldRot;
            }
        }

        private static Transform FindChildRecursive(Transform parent, string name)
        {
            if (parent == null) return null;
            if (parent.name.Equals(name, System.StringComparison.OrdinalIgnoreCase)) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindChildRecursive(parent.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        private void ApplyAlitaCarriedPose()
        {
            float w = carryPoseWeight;
            if (w <= 0.01f) return;

            if (_alitaHead != null) _alitaHead.localRotation = Quaternion.Slerp(_alitaHead.localRotation, AlitaHeadCradle, w);

            if (_alitaLeftUpperArm != null)
            {
                _alitaLeftUpperArm.localScale = Vector3.zero;
                _alitaLeftUpperArm.localRotation = Quaternion.Slerp(_alitaLeftUpperArm.localRotation, AlitaLeftUpperArmCradle, w);
            }
            if (_alitaLeftLowerArm != null) _alitaLeftLowerArm.localRotation = Quaternion.Slerp(_alitaLeftLowerArm.localRotation, AlitaLeftLowerArmCradle, w);
            if (_alitaLeftHand != null) _alitaLeftHand.localRotation = Quaternion.Slerp(_alitaLeftHand.localRotation, AlitaLeftHandCradle, w);

            if (_alitaRightUpperArm != null) _alitaRightUpperArm.localRotation = Quaternion.Slerp(_alitaRightUpperArm.localRotation, AlitaRightUpperArmCradle, w);
            if (_alitaRightLowerArm != null) _alitaRightLowerArm.localRotation = Quaternion.Slerp(_alitaRightLowerArm.localRotation, AlitaRightLowerArmCradle, w);
            if (_alitaRightHand != null) _alitaRightHand.localRotation = Quaternion.Slerp(_alitaRightHand.localRotation, AlitaRightHandCradle, w);

            if (_alitaLeftUpperLeg != null)
            {
                _alitaLeftUpperLeg.localScale = Vector3.zero;
                _alitaLeftUpperLeg.localRotation = Quaternion.Slerp(_alitaLeftUpperLeg.localRotation, AlitaLeftUpperLegCradle, w);
            }
            if (_alitaLeftLowerLeg != null) _alitaLeftLowerLeg.localRotation = Quaternion.Slerp(_alitaLeftLowerLeg.localRotation, AlitaLeftLowerLegCradle, w);
            if (_alitaLeftFoot != null) _alitaLeftFoot.localRotation = Quaternion.Slerp(_alitaLeftFoot.localRotation, AlitaLeftFootCradle, w);

            if (_alitaRightUpperLeg != null)
            {
                _alitaRightUpperLeg.localScale = Vector3.zero;
                _alitaRightUpperLeg.localRotation = Quaternion.Slerp(_alitaRightUpperLeg.localRotation, AlitaRightUpperLegCradle, w);
            }
            if (_alitaRightLowerLeg != null) _alitaRightLowerLeg.localRotation = Quaternion.Slerp(_alitaRightLowerLeg.localRotation, AlitaRightLowerLegCradle, w);
            if (_alitaRightFoot != null) _alitaRightFoot.localRotation = Quaternion.Slerp(_alitaRightFoot.localRotation, AlitaRightFootCradle, w);
        }
    }
}
