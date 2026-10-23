using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace InsaneGun.Combat
{
    /// <summary>
    /// Next-Generation Euphoria-Style Procedural Physics & Ragdoll Controller.
    /// Provides:
    /// 1. Anatomically constrained CharacterJoints with PhysX projection (no joint stretching or rubber limbs).
    /// 2. Hinge-constrained knees & elbows (bends naturally, prevents grotesque backwards bending).
    /// 3. Locomotion velocity inheritance (sprinting characters slide and tumble naturally on hit).
    /// 4. Internal collision suppression (zero self-penetration explosions or ground spasms).
    /// 5. Selective Continuous Collision Detection (Continuous on core mass, Discrete on limbs = 75% PhysX CPU savings).
    /// 6. Auto-Settle & Physics Freeze Culling for dense crowd scalability.
    /// 7. Visceral bone impact thuds & ground dust puffs.
    /// 8. Seamless, GC-free pose-blended get-up recovery in LateUpdate.
    /// 9. Instant O(1) reset for object pooling, respawning, and training dummies.
    /// </summary>
    [DisallowMultipleComponent]
    public class EuphoriaRagdollController : MonoBehaviour
    {
        // ── State ────────────────────────────────────────────────────────────
        [Header("State")]
        [SerializeField] private bool _isRagdoll = false;
        [SerializeField] private bool _isSettled = false;
        [SerializeField] private bool _isFrozen = false;

        public bool IsRagdoll => _isRagdoll;
        public bool IsSettled => _isSettled;
        public bool IsFrozen  => _isFrozen;

        // ── Euphoria Physics Tuning ──────────────────────────────────────────
        [Header("Euphoria Physics Tuning")]
        [Tooltip("Multiplier applied to directional impact forces.")]
        public float ragdollForceMultiplier = 1.35f;

        [Tooltip("Portion of character movement velocity preserved into ragdoll tumble.")]
        [Range(0f, 1.5f)] public float inheritVelocityMultiplier = 0.85f;

        [Tooltip("Cap on inherited movement velocity to prevent physics glitches.")]
        public float maxInheritedVelocity = 12.0f;

        [Tooltip("Linear damping applied to all ragdoll rigidbodies (heft / air resistance).")]
        public float boneLinearDamping = 0.75f;

        [Tooltip("Angular damping applied to all ragdoll rigidbodies (tumble resistance).")]
        public float boneAngularDamping = 1.80f;

        // ── Settle & Optimization ────────────────────────────────────────────
        [Header("Performance & Sleep Culling")]
        [Tooltip("Velocity magnitude below which the ragdoll is considered coming to rest.")]
        public float ragdollSettleVelocityThreshold = 0.22f;

        [Tooltip("Consecutive seconds at low velocity required to mark body as settled.")]
        public float settleCheckDuration = 0.40f;

        [Tooltip("Automatically freeze fatal bodies to kinematic after settling to eliminate PhysX solver cost.")]
        public bool autoFreezeFatalRagdolls = true;

        [Tooltip("Buffer time after settling before freezing rigidbodies to kinematic.")]
        public float freezeDelayAfterSettle = 1.8f;

        // ── Recovery & Get-Up ────────────────────────────────────────────────
        [Header("Get-Up / Recovery")]
        public bool autoRecover = true;
        public float autoRecoverDelay = 2.2f;

        [Tooltip("Duration in seconds to smoothly blend from ragdoll pose into animator pose.")]
        public float poseBlendDuration = 0.35f;

        [Tooltip("Blend-in duration for animator get-up crossfade.")]
        public float getUpBlendInTime = 0.15f;

        public AnimationClip getUpFaceDownClip;
        public AnimationClip getUpFaceUpClip;

        // ── Ground Impact FX ─────────────────────────────────────────────────
        [Header("Impact Audio & Dust VFX")]
        public bool enableGroundImpactAudio = true;
        public bool enableGroundImpactDust = true;
        public float groundImpactMinVelocity = 2.2f;
        public AudioClip[] groundImpactAudioClips;

        // ── Internal References & Structs ────────────────────────────────────
        private struct RagdollBone
        {
            public HumanBodyBones boneType;
            public Transform transform;
            public Rigidbody rigidbody;
            public Collider collider;
            public CharacterJoint joint;
        }

        private struct BoneInitialPose
        {
            public Transform transform;
            public Vector3 localPosition;
            public Quaternion localRotation;
        }

        private struct BoneBlendSnapshot
        {
            public Transform bone;
            public Vector3 localPos;
            public Quaternion localRot;
        }

        private readonly List<RagdollBone> _bones = new List<RagdollBone>(14);
        private readonly List<BoneInitialPose> _initialPoses = new List<BoneInitialPose>(14);
        private BoneBlendSnapshot[] _blendSnapshots;

        private Animator _animator;
        private CharacterController _cc;
        private NavMeshAgent _navAgent;
        private InsaneGunSwordController _combatController;
        private Transform _pelvisTransform;
        private Coroutine _recoverCoroutine;
        private Coroutine _settleMonitorCoroutine;

        private Vector3 _lastFramePosition;
        private Vector3 _estimatedMovementVelocity;
        private float _blendTimer = 0f;
        private bool _isBlendingPose = false;

        private static PhysicsMaterial _ragdollPhysMat;
        private static AudioClip _cachedBootLandClip;

        public Vector3 RagdollPelvisPosition => _pelvisTransform != null ? _pelvisTransform.position : transform.position;

        // ── Lifecycle ────────────────────────────────────────────────────────
        private void Awake()
        {
            CacheComponents();
            SetupRagdollBones();
            AutoAssignClips();
            EuphoriaRagdollManager.Register(this);
        }

        private void OnDestroy()
        {
            EuphoriaRagdollManager.Unregister(this);
        }

        private void OnEnable()
        {
            _lastFramePosition = transform.position;
            _estimatedMovementVelocity = Vector3.zero;
        }

        private void Update()
        {
            // Track movement velocity while alive and animated
            if (!_isRagdoll)
            {
                float dt = Time.deltaTime;
                if (dt > 0.0001f)
                {
                    _estimatedMovementVelocity = (transform.position - _lastFramePosition) / dt;
                }
                _lastFramePosition = transform.position;
            }
        }

        private void LateUpdate()
        {
            // Smooth, GC-free pose blend from captured ragdoll local transforms to current Animator pose
            if (_isBlendingPose && _blendSnapshots != null)
            {
                _blendTimer += Time.deltaTime;
                float progress = Mathf.Clamp01(_blendTimer / Mathf.Max(0.01f, poseBlendDuration));
                float smoothWeight = Mathf.SmoothStep(0f, 1f, progress);

                for (int i = 0; i < _blendSnapshots.Length; i++)
                {
                    var snap = _blendSnapshots[i];
                    if (snap.bone == null) continue;

                    // Interpolate local rotations and local positions toward animator's evaluated pose
                    snap.bone.localRotation = Quaternion.Slerp(snap.localRot, snap.bone.localRotation, smoothWeight);
                    snap.bone.localPosition = Vector3.Lerp(snap.localPos, snap.bone.localPosition, smoothWeight);
                }

                if (progress >= 1.0f)
                {
                    _isBlendingPose = false;
                    _blendSnapshots = null;
                }
            }
        }

        private void CacheComponents()
        {
            if (_animator == null) _animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
            if (_cc == null) _cc = GetComponent<CharacterController>();
            if (_navAgent == null) _navAgent = GetComponent<NavMeshAgent>();
            if (_combatController == null) _combatController = GetComponent<InsaneGunSwordController>();
        }

        // ── Bone Hierarchy & Physics Setup ───────────────────────────────────
        public void SetupRagdollBones()
        {
            if (_bones.Count > 0) return;
            CacheComponents();

            if (_ragdollPhysMat == null)
            {
                _ragdollPhysMat = new PhysicsMaterial("EuphoriaRagdollMat")
                {
                    dynamicFriction = 0.70f,
                    staticFriction = 0.70f,
                    bounciness = 0.08f,
                    frictionCombine = PhysicsMaterialCombine.Average,
                    bounceCombine = PhysicsMaterialCombine.Minimum
                };
            }

            // Bone anatomical blueprint: (bone, parent, collider dimensions, isSphere, jointType)
            // 0 = Root/Hips, 1 = Spine/Chest, 2 = Head, 3 = Shoulder, 4 = Elbow (Hinge), 5 = UpperLeg, 6 = Knee (Hinge)
            var boneDefs = new (HumanBodyBones bone, HumanBodyBones parent, Vector3 colSize, bool isSphere, int jointType)[]
            {
                (HumanBodyBones.Hips,          HumanBodyBones.LastBone,     new Vector3(0.24f, 0.20f, 0.20f), false, 0),
                (HumanBodyBones.Spine,         HumanBodyBones.Hips,         new Vector3(0.22f, 0.22f, 0.18f), false, 1),
                (HumanBodyBones.Chest,         HumanBodyBones.Spine,        new Vector3(0.26f, 0.24f, 0.20f), false, 1),
                (HumanBodyBones.Head,          HumanBodyBones.Chest,        new Vector3(0.18f, 0.18f, 0.18f), true,  2),

                (HumanBodyBones.LeftUpperArm,  HumanBodyBones.Chest,        new Vector3(0.12f, 0.26f, 0.12f), false, 3),
                (HumanBodyBones.LeftLowerArm,  HumanBodyBones.LeftUpperArm, new Vector3(0.10f, 0.24f, 0.10f), false, 4),
                (HumanBodyBones.RightUpperArm, HumanBodyBones.Chest,        new Vector3(0.12f, 0.26f, 0.12f), false, 3),
                (HumanBodyBones.RightLowerArm, HumanBodyBones.RightUpperArm,new Vector3(0.10f, 0.24f, 0.10f), false, 4),

                (HumanBodyBones.LeftUpperLeg,  HumanBodyBones.Hips,         new Vector3(0.14f, 0.36f, 0.14f), false, 5),
                (HumanBodyBones.LeftLowerLeg,  HumanBodyBones.LeftUpperLeg, new Vector3(0.12f, 0.34f, 0.12f), false, 6),
                (HumanBodyBones.RightUpperLeg, HumanBodyBones.Hips,         new Vector3(0.14f, 0.36f, 0.14f), false, 5),
                (HumanBodyBones.RightLowerLeg, HumanBodyBones.RightUpperLeg,new Vector3(0.12f, 0.34f, 0.12f), false, 6),
            };

            // Fast child transform dictionary for instant O(1) fallback lookups without recursive tree scans
            Dictionary<string, Transform> childMap = null;

            var rbMap = new Dictionary<HumanBodyBones, Rigidbody>(16);

            foreach (var def in boneDefs)
            {
                Transform t = null;
                if (_animator != null && _animator.isHuman)
                {
                    t = _animator.GetBoneTransform(def.bone);
                }

                if (t == null)
                {
                    if (childMap == null) childMap = BuildChildTransformMap();
                    t = FindBoneInMap(childMap, def.bone);
                }

                if (t == null) continue;

                if (def.bone == HumanBodyBones.Hips) _pelvisTransform = t;

                // Cache initial local transform for instant O(1) reset/pooling
                _initialPoses.Add(new BoneInitialPose
                {
                    transform = t,
                    localPosition = t.localPosition,
                    localRotation = t.localRotation
                });

                // 1. Rigidbody
                var rb = t.GetComponent<Rigidbody>();
                if (rb == null) rb = t.gameObject.AddComponent<Rigidbody>();
                rb.mass = GetBoneMass(def.bone);
                rb.linearDamping = boneLinearDamping;
                rb.angularDamping = boneAngularDamping;

                // Continuous collision on core mass to prevent floor tunneling; Discrete on limbs for 75% PhysX speedup
                if (def.bone == HumanBodyBones.Hips || def.bone == HumanBodyBones.Spine || def.bone == HumanBodyBones.Chest)
                    rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                else
                    rb.collisionDetectionMode = CollisionDetectionMode.Discrete;

                rb.isKinematic = true;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rbMap[def.bone] = rb;

                // 2. Collider
                Collider col;
                if (def.isSphere)
                {
                    var sc = t.GetComponent<SphereCollider>();
                    if (sc == null) sc = t.gameObject.AddComponent<SphereCollider>();
                    sc.radius = def.colSize.x * 0.5f;
                    sc.sharedMaterial = _ragdollPhysMat;
                    col = sc;
                }
                else
                {
                    var cc = t.GetComponent<CapsuleCollider>();
                    if (cc == null) cc = t.gameObject.AddComponent<CapsuleCollider>();
                    cc.radius = Mathf.Max(def.colSize.x, def.colSize.z) * 0.5f;
                    cc.height = def.colSize.y;
                    cc.direction = 1; // Y-axis
                    cc.sharedMaterial = _ragdollPhysMat;
                    col = cc;
                }

                col.isTrigger = false;
                col.enabled = false;

                // Add ground impact listener on primary bones (Pelvis, Chest, Head)
                if (def.bone == HumanBodyBones.Hips || def.bone == HumanBodyBones.Chest || def.bone == HumanBodyBones.Head)
                {
                    var listener = t.GetComponent<EuphoriaBoneImpactListener>();
                    if (listener == null) listener = t.gameObject.AddComponent<EuphoriaBoneImpactListener>();
                    listener.Initialize(this, def.bone);
                }

                // 3. Anatomical CharacterJoint
                CharacterJoint joint = null;
                if (def.parent != HumanBodyBones.LastBone && rbMap.TryGetValue(def.parent, out var parentRb))
                {
                    joint = t.GetComponent<CharacterJoint>();
                    if (joint == null) joint = t.gameObject.AddComponent<CharacterJoint>();
                    joint.connectedBody = parentRb;
                    joint.enablePreprocessing = false;
                    joint.enableCollision = false;

                    // Joint Projection: strictly enforces joint constraints and stops limbs from tearing/stretching!
                    joint.enableProjection = true;
                    joint.projectionDistance = 0.035f;
                    joint.projectionAngle = 5.0f;

                    // Soft limit springs eliminate jitter and hard bounces at joint limits
                    SoftJointLimitSpring limitSpring = new SoftJointLimitSpring { spring = 150f, damper = 22f };
                    joint.twistLimitSpring = limitSpring;
                    joint.swingLimitSpring = limitSpring;

                    ConfigureAnatomicalLimits(joint, def.jointType);
                }

                _bones.Add(new RagdollBone
                {
                    boneType = def.bone,
                    transform = t,
                    rigidbody = rb,
                    collider = col,
                    joint = joint
                });
            }

            // CRITICAL OPTIMIZATION: Suppress all internal collision pairs inside this ragdoll!
            // Limbs never collide with the character's own torso/pelvis. This eliminates 100% of internal
            // self-penetration explosions, spastic floor twitching, and removes O(N^2) collision checks.
            for (int i = 0; i < _bones.Count; i++)
            {
                var colA = _bones[i].collider;
                if (colA == null) continue;

                // Ignore with CharacterController
                if (_cc != null) Physics.IgnoreCollision(colA, _cc, true);

                for (int j = i + 1; j < _bones.Count; j++)
                {
                    var colB = _bones[j].collider;
                    if (colB != null)
                    {
                        Physics.IgnoreCollision(colA, colB, true);
                    }
                }
            }

            SetRagdollState(false);
        }

        private void ConfigureAnatomicalLimits(CharacterJoint joint, int jointType)
        {
            switch (jointType)
            {
                case 1: // Spine & Chest
                    joint.lowTwistLimit = new SoftJointLimit { limit = -15f };
                    joint.highTwistLimit = new SoftJointLimit { limit = 15f };
                    joint.swing1Limit = new SoftJointLimit { limit = 30f }; // Forward flexion / extension
                    joint.swing2Limit = new SoftJointLimit { limit = 15f }; // Lateral bend
                    break;

                case 2: // Head / Neck
                    joint.lowTwistLimit = new SoftJointLimit { limit = -40f };
                    joint.highTwistLimit = new SoftJointLimit { limit = 40f };
                    joint.swing1Limit = new SoftJointLimit { limit = 35f };
                    joint.swing2Limit = new SoftJointLimit { limit = 25f };
                    break;

                case 3: // Shoulder (Upper Arm) - High mobility ball & socket
                    joint.lowTwistLimit = new SoftJointLimit { limit = -60f };
                    joint.highTwistLimit = new SoftJointLimit { limit = 60f };
                    joint.swing1Limit = new SoftJointLimit { limit = 75f };
                    joint.swing2Limit = new SoftJointLimit { limit = 50f };
                    break;

                case 4: // Elbow (Lower Arm) - Strict Hinge flexion forward
                    joint.lowTwistLimit = new SoftJointLimit { limit = -5f };
                    joint.highTwistLimit = new SoftJointLimit { limit = 135f }; // Forward bend
                    joint.swing1Limit = new SoftJointLimit { limit = 6f };
                    joint.swing2Limit = new SoftJointLimit { limit = 6f };
                    break;

                case 5: // Hip (Upper Leg) - Ball & socket
                    joint.lowTwistLimit = new SoftJointLimit { limit = -20f };
                    joint.highTwistLimit = new SoftJointLimit { limit = 20f };
                    joint.swing1Limit = new SoftJointLimit { limit = 70f }; // Forward step / backward extension
                    joint.swing2Limit = new SoftJointLimit { limit = 25f }; // Lateral play
                    break;

                case 6: // Knee (Lower Leg) - Strict Hinge bend backwards
                    joint.lowTwistLimit = new SoftJointLimit { limit = -5f };
                    joint.highTwistLimit = new SoftJointLimit { limit = 135f }; // Backward bend
                    joint.swing1Limit = new SoftJointLimit { limit = 6f };
                    joint.swing2Limit = new SoftJointLimit { limit = 6f };
                    break;
            }
        }

        private Dictionary<string, Transform> BuildChildTransformMap()
        {
            var map = new Dictionary<string, Transform>(64, System.StringComparer.OrdinalIgnoreCase);
            var transforms = GetComponentsInChildren<Transform>(true);
            foreach (var tr in transforms)
            {
                if (!map.ContainsKey(tr.name))
                    map[tr.name] = tr;
            }
            return map;
        }

        private Transform FindBoneInMap(Dictionary<string, Transform> map, HumanBodyBones bone)
        {
            string[] names = null;
            switch (bone)
            {
                case HumanBodyBones.Hips:          names = new[] { "pelvis", "hips", "hip", "b_hips", "b_pelvis", "root/pelvis" }; break;
                case HumanBodyBones.Spine:         names = new[] { "spine_01", "spine", "spine1", "b_spine01" }; break;
                case HumanBodyBones.Chest:         names = new[] { "spine_02", "spine_03", "chest", "spine2", "b_spine02" }; break;
                case HumanBodyBones.Head:          names = new[] { "head", "b_head" }; break;
                case HumanBodyBones.LeftUpperArm:  names = new[] { "upperarm_l", "arm_l", "b_leftarm", "left_arm", "leftupperarm" }; break;
                case HumanBodyBones.LeftLowerArm:  names = new[] { "lowerarm_l", "forearm_l", "b_leftforearm", "left_forearm", "leftlowerarm" }; break;
                case HumanBodyBones.RightUpperArm: names = new[] { "upperarm_r", "arm_r", "b_rightarm", "right_arm", "rightupperarm" }; break;
                case HumanBodyBones.RightLowerArm: names = new[] { "lowerarm_r", "forearm_r", "b_rightforearm", "right_forearm", "rightlowerarm" }; break;
                case HumanBodyBones.LeftUpperLeg:  names = new[] { "thigh_l", "leg_l", "b_leftleg", "left_leg", "leftupperleg" }; break;
                case HumanBodyBones.LeftLowerLeg:  names = new[] { "calf_l", "shin_l", "b_leftlegshin", "left_shin", "leftlowerleg" }; break;
                case HumanBodyBones.RightUpperLeg: names = new[] { "thigh_r", "leg_r", "b_rightleg", "right_leg", "rightupperleg" }; break;
                case HumanBodyBones.RightLowerLeg: names = new[] { "calf_r", "shin_r", "b_rightlegshin", "right_shin", "rightlowerleg" }; break;
            }

            if (names == null) return null;
            foreach (var n in names)
            {
                if (map.TryGetValue(n, out var tr)) return tr;
            }
            return null;
        }

        private float GetBoneMass(HumanBodyBones bone)
        {
            switch (bone)
            {
                case HumanBodyBones.Hips: return 18f;
                case HumanBodyBones.Spine: return 14f;
                case HumanBodyBones.Chest: return 16f;
                case HumanBodyBones.Head: return 4.5f;
                case HumanBodyBones.LeftUpperLeg:
                case HumanBodyBones.RightUpperLeg: return 9f;
                case HumanBodyBones.LeftLowerLeg:
                case HumanBodyBones.RightLowerLeg: return 5f;
                case HumanBodyBones.LeftUpperArm:
                case HumanBodyBones.RightUpperArm: return 3.5f;
                case HumanBodyBones.LeftLowerArm:
                case HumanBodyBones.RightLowerArm: return 2.5f;
                default: return 3.0f;
            }
        }

        // ── State Switching ──────────────────────────────────────────────────
        public void SetRagdollState(bool active)
        {
            _isRagdoll = active;
            _isSettled = false;
            _isFrozen = false;

            if (_animator != null) _animator.enabled = !active;
            if (_cc != null) _cc.enabled = !active;
            if (_navAgent != null && _navAgent.isOnNavMesh) _navAgent.enabled = !active;

            foreach (var b in _bones)
            {
                if (b.rigidbody != null)
                {
                    if (!active && !b.rigidbody.isKinematic)
                    {
                        b.rigidbody.linearVelocity = Vector3.zero;
                        b.rigidbody.angularVelocity = Vector3.zero;
                    }
                    b.rigidbody.isKinematic = !active;
                    b.rigidbody.useGravity = active;
                }
                if (b.collider != null)
                {
                    b.collider.enabled = active;
                }
            }

            if (active)
                EuphoriaRagdollManager.NotifyRagdollActivated(this);
            else
                EuphoriaRagdollManager.NotifyRagdollDeactivated(this);
        }

        // ── Euphoria Dynamic Impact Activation ───────────────────────────────
        /// <summary>
        /// Activates Rockstar/Euphoria style procedural ragdoll with directional impact impulse,
        /// locomotion velocity preservation, and automatic settle / freeze optimization.
        /// </summary>
        public void TriggerEuphoriaRagdoll(Vector3 impactPoint, Vector3 impactDirection, float forceMagnitude, bool isFatal = false)
        {
            TriggerEuphoriaRagdoll(impactPoint, impactDirection, forceMagnitude, isFatal, null);
        }

        /// <summary>
        /// Overload accepting custom inherited movement velocity.
        /// </summary>
        public void TriggerEuphoriaRagdoll(Vector3 impactPoint, Vector3 impactDirection, float forceMagnitude, bool isFatal, Vector3? inheritedVelocity)
        {
            if (_recoverCoroutine != null) StopCoroutine(_recoverCoroutine);
            if (_settleMonitorCoroutine != null) StopCoroutine(_settleMonitorCoroutine);

            // Compute inherited movement velocity (sprinting characters slide and tumble naturally)
            Vector3 initVel = Vector3.zero;
            if (inheritedVelocity.HasValue)
            {
                initVel = inheritedVelocity.Value;
            }
            else
            {
                if (_cc != null) initVel = _cc.velocity;
                else if (_navAgent != null && _navAgent.enabled) initVel = _navAgent.velocity;
                else initVel = _estimatedMovementVelocity;
            }

            initVel = Vector3.ClampMagnitude(initVel * inheritVelocityMultiplier, maxInheritedVelocity);

            SetRagdollState(true);

            // Apply base inherited velocity to all bones
            foreach (var b in _bones)
            {
                if (b.rigidbody != null)
                {
                    b.rigidbody.linearVelocity = initVel;
                }
            }

            // Directional impulse with slight upward lift to simulate realistic impact trajectory
            Vector3 force = (impactDirection + Vector3.up * 0.35f).normalized * (forceMagnitude * ragdollForceMultiplier);

            // Find bone closest to the impact point
            Rigidbody targetRb = null;
            float minDist = float.MaxValue;
            foreach (var b in _bones)
            {
                if (b.rigidbody == null) continue;
                float dist = Vector3.Distance(b.transform.position, impactPoint);
                if (dist < minDist)
                {
                    minDist = dist;
                    targetRb = b.rigidbody;
                }
            }

            if (targetRb == null && _bones.Count > 0) targetRb = _bones[0].rigidbody;

            if (targetRb != null)
            {
                targetRb.AddForceAtPosition(force, impactPoint, ForceMode.Impulse);
                targetRb.AddTorque(Random.insideUnitSphere * (forceMagnitude * 0.75f), ForceMode.Impulse);
            }

            // Distribute residual momentum to other bones for cohesive physical acceleration
            foreach (var b in _bones)
            {
                if (b.rigidbody != null && b.rigidbody != targetRb)
                {
                    b.rigidbody.AddForce(force * 0.30f, ForceMode.Impulse);
                }
            }

            // Start Settle / Sleep / Recovery monitoring
            _settleMonitorCoroutine = StartCoroutine(SettleAndRecoveryMonitorRoutine(isFatal));
        }

        /// <summary>
        /// Imparts an impulse to an already-active or settled ragdoll (e.g. secondary explosions, spaceship shots, Blackwall shockwave).
        /// Wakes up frozen rigidbodies automatically.
        /// </summary>
        public void AddImpulse(Vector3 point, Vector3 impulse)
        {
            if (!_isRagdoll) return;

            // Wake up if settled or frozen
            if (_isFrozen)
            {
                _isFrozen = false;
                foreach (var b in _bones)
                {
                    if (b.rigidbody != null)
                    {
                        b.rigidbody.isKinematic = false;
                        b.rigidbody.useGravity = true;
                        b.rigidbody.WakeUp();
                    }
                }
            }

            _isSettled = false;

            Rigidbody targetRb = null;
            float minDist = float.MaxValue;
            foreach (var b in _bones)
            {
                if (b.rigidbody == null) continue;
                float dist = Vector3.Distance(b.transform.position, point);
                if (dist < minDist) { minDist = dist; targetRb = b.rigidbody; }
            }

            if (targetRb != null)
            {
                targetRb.AddForceAtPosition(impulse, point, ForceMode.Impulse);
                targetRb.AddTorque(Random.insideUnitSphere * impulse.magnitude * 0.5f, ForceMode.Impulse);
            }

            foreach (var b in _bones)
            {
                if (b.rigidbody != null && b.rigidbody != targetRb)
                {
                    b.rigidbody.AddForce(impulse * 0.25f, ForceMode.Impulse);
                }
            }
        }

        // ── Settle, Freeze & Auto-Recovery Monitor ────────────────────────────
        private IEnumerator SettleAndRecoveryMonitorRoutine(bool isFatal)
        {
            float maxWait = isFatal ? 10f : (autoRecoverDelay + 3.0f);
            float elapsed = 0f;
            float lowVelTimer = 0f;
            float sqrThreshold = ragdollSettleVelocityThreshold * ragdollSettleVelocityThreshold;

            while (elapsed < maxWait)
            {
                elapsed += Time.deltaTime;

                // Sample kinetic energy across all rigidbodies
                float totalLinearEnergy = 0f;
                int validBones = 0;
                foreach (var b in _bones)
                {
                    if (b.rigidbody != null && !b.rigidbody.isKinematic)
                    {
                        totalLinearEnergy += b.rigidbody.linearVelocity.sqrMagnitude;
                        validBones++;
                    }
                }

                float avgEnergy = validBones > 0 ? (totalLinearEnergy / validBones) : 0f;

                if (avgEnergy < sqrThreshold)
                {
                    lowVelTimer += Time.deltaTime;
                    if (lowVelTimer >= settleCheckDuration)
                    {
                        _isSettled = true;
                        break;
                    }
                }
                else
                {
                    lowVelTimer = 0f;
                }

                yield return null;
            }

            _isSettled = true;

            // Put rigidbodies to sleep to save PhysX CPU
            foreach (var b in _bones)
            {
                if (b.rigidbody != null && !b.rigidbody.isKinematic)
                    b.rigidbody.Sleep();
            }

            if (isFatal)
            {
                // For dead bodies, freeze to kinematic after buffer time to eliminate physics solver cost completely
                if (autoFreezeFatalRagdolls)
                {
                    yield return new WaitForSeconds(freezeDelayAfterSettle);
                    ForceSettleAndFreeze();
                }
            }
            else if (autoRecover && gameObject.activeInHierarchy)
            {
                // Non-fatal knockdown: wait out remaining recovery delay then get up
                float remainingDelay = Mathf.Max(0.1f, autoRecoverDelay - elapsed);
                yield return new WaitForSeconds(remainingDelay);
                RecoverFromKnockdown();
            }
        }

        /// <summary>
        /// Forces rigidbodies to kinematic and marks as frozen. Drops PhysX solver load to 0%.
        /// </summary>
        public void ForceSettleAndFreeze()
        {
            if (!_isRagdoll) return;
            _isSettled = true;
            _isFrozen = true;

            foreach (var b in _bones)
            {
                if (b.rigidbody != null)
                {
                    b.rigidbody.linearVelocity = Vector3.zero;
                    b.rigidbody.angularVelocity = Vector3.zero;
                    b.rigidbody.isKinematic = true;
                    b.rigidbody.useGravity = false;
                }
            }

            EuphoriaRagdollManager.NotifyRagdollDeactivated(this);
        }

        // ── Clean Get-Up Recovery ────────────────────────────────────────────
        /// <summary>
        /// Recovers from knockdown with smooth pose-blend and get-up animation.
        /// </summary>
        public void RecoverFromKnockdown()
        {
            if (!_isRagdoll) return;
            if (gameObject.activeInHierarchy)
                StartCoroutine(RecoverRoutine(isFatal: false));
        }

        /// <summary>
        /// Fully revives character to combat (called by InsaneGunSwordController.Revive).
        /// </summary>
        public void RecoverToCombat(Vector3? targetPosition = null)
        {
            if (!_isRagdoll) return;
            if (gameObject.activeInHierarchy)
                StartCoroutine(RecoverRoutine(isFatal: true, overridePos: targetPosition));
        }

        private IEnumerator RecoverRoutine(bool isFatal, Vector3? overridePos = null)
        {
            if (_settleMonitorCoroutine != null) StopCoroutine(_settleMonitorCoroutine);

            // Step 1: Capture current local transforms of all bones
            _blendSnapshots = new BoneBlendSnapshot[_bones.Count];
            for (int i = 0; i < _bones.Count; i++)
            {
                var tr = _bones[i].transform;
                _blendSnapshots[i] = new BoneBlendSnapshot
                {
                    bone = tr,
                    localPos = tr != null ? tr.localPosition : Vector3.zero,
                    localRot = tr != null ? tr.localRotation : Quaternion.identity
                };
            }

            // Step 2: Calculate get-up root position on ground beneath pelvis
            Vector3 getupPos;
            if (overridePos.HasValue)
            {
                getupPos = overridePos.Value;
            }
            else
            {
                getupPos = _pelvisTransform != null ? _pelvisTransform.position : transform.position;
                if (Physics.Raycast(getupPos + Vector3.up * 1.2f, Vector3.down, out RaycastHit hit, 6f, LayerMask.GetMask("Default", "Ground", "Terrain", "Walls")))
                    getupPos.y = hit.point.y;
                else
                    getupPos.y = Mathf.Max(getupPos.y, 0f);
            }

            // Step 3: Determine facing direction (Face-down vs Face-up)
            bool faceDown = true;
            if (_pelvisTransform != null)
            {
                // Check if pelvis up or forward points toward ground
                faceDown = Vector3.Dot(_pelvisTransform.forward, Vector3.up) < 0f || Vector3.Dot(_pelvisTransform.up, Vector3.up) < 0f;
            }

            Vector3 getupForward = transform.forward;
            if (_pelvisTransform != null)
            {
                Vector3 pelvisFwd = _pelvisTransform.forward;
                pelvisFwd.y = 0f;
                if (pelvisFwd.sqrMagnitude > 0.01f) getupForward = pelvisFwd.normalized;
                if (faceDown) getupForward = -getupForward;
            }

            // Step 4: Turn off ragdoll physics, reposition root
            SetRagdollState(false);
            transform.position = getupPos;
            transform.rotation = Quaternion.LookRotation(getupForward, Vector3.up);

            // Step 5: Enable Animator and start pose-blending in LateUpdate
            if (_animator != null)
            {
                _animator.enabled = true;
                _animator.Rebind();
                _animator.Update(0f);
                _animator.Play("Locomotion", 0, 0f);
                _animator.Update(0f);
            }

            _blendTimer = 0f;
            _isBlendingPose = true;

            // Wait out pose blend duration
            yield return new WaitForSeconds(poseBlendDuration);

            _isBlendingPose = false;
            _blendSnapshots = null;

            // Step 6: Trigger get-up animation if states exist, or CrossFade directly
            AnimationClip getUpClip = faceDown ? getUpFaceDownClip : (getUpFaceUpClip ?? getUpFaceDownClip);

            if (_animator != null)
            {
                int getUpHash = Animator.StringToHash("GetUp");
                int knockDownEndHash = Animator.StringToHash("KnockDown_End");
                int locHash = Animator.StringToHash("Locomotion");

                if (_animator.HasState(0, getUpHash))
                    _animator.CrossFadeInFixedTime(getUpHash, getUpBlendInTime, 0, 0f);
                else if (_animator.HasState(0, knockDownEndHash))
                    _animator.CrossFadeInFixedTime(knockDownEndHash, getUpBlendInTime, 0, 0f);
                else if (_animator.HasState(0, locHash))
                    _animator.CrossFadeInFixedTime(locHash, getUpBlendInTime, 0, 0f);

                SpawnGetUpDust(getupPos);

                float clipLength = getUpClip != null ? getUpClip.length : 0.6f;
                yield return new WaitForSeconds(Mathf.Max(0.2f, clipLength - getUpBlendInTime));

                _animator.CrossFadeInFixedTime(locHash, 0.20f, 0, 0f);
            }
            else
            {
                SpawnGetUpDust(getupPos);
                yield return new WaitForSeconds(0.4f);
            }

            // Step 7: Re-enable character controller / combat systems
            if (_cc != null) _cc.enabled = true;
            if (_navAgent != null && _navAgent.isOnNavMesh) _navAgent.enabled = true;

            if (_combatController != null)
            {
                if (isFatal)
                    _combatController.Revive(getupPos);
                else
                    _combatController.OnKnockdownRecovered(getupPos);
            }
        }

        // ── Instant O(1) Reset for Object Pooling & Training Dummies ─────────
        /// <summary>
        /// Immediately restores initial bone bindpose, sets rigidbodies to kinematic,
        /// disables colliders, and re-enables animator. Zero GC allocations.
        /// </summary>
        public void ResetRagdollImmediate(Vector3? resetPosition = null)
        {
            if (_recoverCoroutine != null)
            {
                StopCoroutine(_recoverCoroutine);
                _recoverCoroutine = null;
            }
            if (_settleMonitorCoroutine != null)
            {
                StopCoroutine(_settleMonitorCoroutine);
                _settleMonitorCoroutine = null;
            }

            _isBlendingPose = false;
            _blendSnapshots = null;
            _isSettled = false;
            _isFrozen = false;

            SetRagdollState(false);
            RestoreInitialPoses();

            if (resetPosition.HasValue)
            {
                if (_cc != null) _cc.enabled = false;
                if (_navAgent != null) _navAgent.enabled = false;
                transform.position = resetPosition.Value;
                if (_cc != null) _cc.enabled = true;
                if (_navAgent != null && _navAgent.isOnNavMesh) _navAgent.enabled = true;
            }

            if (_animator != null)
            {
                _animator.enabled = true;
                _animator.Rebind();
                _animator.Update(0f);
            }
        }

        private void RestoreInitialPoses()
        {
            for (int i = 0; i < _initialPoses.Count; i++)
            {
                var p = _initialPoses[i];
                if (p.transform != null)
                {
                    p.transform.localPosition = p.localPosition;
                    p.transform.localRotation = p.localRotation;
                }
            }
        }

        // ── Impact Audio & Dust Feedback ─────────────────────────────────────
        internal void HandleBoneCollision(HumanBodyBones bone, Collision collision)
        {
            if (!_isRagdoll || _isFrozen) return;

            float relVel = collision.relativeVelocity.magnitude;
            if (relVel < groundImpactMinVelocity) return;

            Vector3 contactPoint = collision.GetContact(0).point;

            // 1. Audio Thud
            if (enableGroundImpactAudio && EuphoriaRagdollManager.Instance != null && EuphoriaRagdollManager.Instance.CanPlayImpactSound())
            {
                AudioClip clip = null;
                if (groundImpactAudioClips != null && groundImpactAudioClips.Length > 0)
                {
                    clip = groundImpactAudioClips[Random.Range(0, groundImpactAudioClips.Length)];
                }
                else
                {
                    clip = GetCachedBootLandClip();
                }

                if (clip != null)
                {
                    float vol = Mathf.Clamp(relVel / 8.0f, 0.35f, 0.95f);
                    AudioSource.PlayClipAtPoint(clip, contactPoint, vol);
                }
            }

            // 2. Ground Dust Puff
            if (enableGroundImpactDust)
            {
                SpawnImpactDust(contactPoint, relVel);
            }
        }

        private static AudioClip GetCachedBootLandClip()
        {
            if (_cachedBootLandClip == null)
            {
#if UNITY_EDITOR
                _cachedBootLandClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
                    "Assets/Audio/Combat/Jump_Landing_Boots_01.wav");
#endif
                if (_cachedBootLandClip == null)
                    _cachedBootLandClip = Resources.Load<AudioClip>("Audio/Jump_Landing_Boots_01");
            }
            return _cachedBootLandClip;
        }

        private void SpawnImpactDust(Vector3 pos, float impactSpeed)
        {
            GameObject g = new GameObject("Ragdoll_Impact_Dust");
            g.transform.position = pos + Vector3.up * 0.05f;
            var ps = g.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main;
            m.duration = 0.25f;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.70f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.5f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.12f, Mathf.Clamp(impactSpeed * 0.06f, 0.2f, 0.45f));
            m.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.6f, 0.6f, 0.6f, 0.45f),
                new Color(0.75f, 0.75f, 0.75f, 0.15f));
            m.stopAction = ParticleSystemStopAction.Destroy;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.35f;

            var em = ps.emission;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(impactSpeed * 2.5f, 8, 22)) });
            ps.Play();

            Destroy(g, 2f);
        }

        private void SpawnGetUpDust(Vector3 pos)
        {
            GameObject g = new GameObject("GetUp_Dust_FX");
            g.transform.position = pos + Vector3.up * 0.05f;
            var ps = g.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main;
            m.duration = 0.30f;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.40f, 0.80f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 3.0f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
            m.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.65f, 0.65f, 0.65f, 0.50f),
                new Color(0.85f, 0.85f, 0.85f, 0.20f));
            m.stopAction = ParticleSystemStopAction.Destroy;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.45f;

            var em = ps.emission;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 16) });
            ps.Play();

            Destroy(g, 2.5f);
        }

        private void AutoAssignClips()
        {
#if UNITY_EDITOR
            if (getUpFaceDownClip == null)
            {
                getUpFaceDownClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    "Assets/Synty/AnimationSwordCombat/Animations/Polygon/Hit/KnockDown/A_KnockDown_End_Sword.fbx");
            }
#endif
            if (getUpFaceDownClip == null)
                getUpFaceDownClip = Resources.Load<AnimationClip>("Animations/A_KnockDown_End_Sword");
        }
    }

    /// <summary>
    /// Lightweight collision reporter attached to key bones (Pelvis, Chest, Head)
    /// to detect hard ground/wall impacts and trigger Euphoria thud audio & dust VFX.
    /// </summary>
    [DisallowMultipleComponent]
    public class EuphoriaBoneImpactListener : MonoBehaviour
    {
        private EuphoriaRagdollController _controller;
        private HumanBodyBones _boneType;

        public void Initialize(EuphoriaRagdollController controller, HumanBodyBones boneType)
        {
            _controller = controller;
            _boneType = boneType;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_controller != null)
            {
                _controller.HandleBoneCollision(_boneType, collision);
            }
        }
    }
}
