using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using InsaneGun.Combat;

namespace PolygonMech
{
    /// <summary>
    /// Heavy industrial Euphoria-style Ragdoll Controller designed specifically for the 30-ton Titan Mech Boss.
    /// Features:
    /// - Physically accurate colliders and joint limits scaled for a 6-meter war mech.
    /// - Continuous collision detection on core torso mass to prevent terrain tunneling.
    /// - Internal self-collision suppression to eliminate spasms and physics explosions.
    /// - Dynamic momentum & velocity inheritance on death.
    /// - Weapon release physics (massive war hammer drops and clatters into the dirt).
    /// - Cataclysmic ground crash audio, rock/dust shockwave, and camera trauma shake.
    /// </summary>
    [DisallowMultipleComponent]
    public class MechTitanRagdollController : MonoBehaviour
    {
        [Header("State")]
        [SerializeField] private bool _isRagdoll = false;
        public bool IsRagdoll => _isRagdoll;

        [Header("Physics & Force Tuning")]
        [Tooltip("Portion of character movement velocity preserved into the ragdoll collapse.")]
        [Range(0f, 2f)] public float inheritVelocityMultiplier = 1.0f;
        [Tooltip("Impulse force magnitude applied to the mech chassis on death.")]
        public float deathImpactForce = 9000f;
        [Tooltip("Linear air resistance damping applied to all mech limbs.")]
        public float boneLinearDamping = 0.50f;
        [Tooltip("Angular tumble resistance damping applied to all mech limbs.")]
        public float boneAngularDamping = 1.40f;

        [Header("Weapon Ejection")]
        [Tooltip("If true, the massive war hammer is dropped and becomes a separate physics object upon death.")]
        public bool dropWeaponOnDeath = true;
        public Transform hammerWeaponTransform;

        [Header("Ground Crash Feedback")]
        public GameObject crashDustPrefab;
        public AudioClip sfxMetalCrash;
        [Range(0f, 1f)] public float crashVolume = 1.0f;

        [Serializable]
        public struct MechBoneDefinition
        {
            public HumanBodyBones boneType;
            public Transform boneTransform;
            public Rigidbody rigidbody;
            public Collider collider;
            public CharacterJoint joint;
            public Vector3 initialLocalPos;
            public Quaternion initialLocalRot;
        }

        [Header("Runtime Bone Cache")]
        [SerializeField] private List<MechBoneDefinition> _bones = new List<MechBoneDefinition>(16);

        // Component References
        private Animator _animator;
        private CharacterController _controller;
        private HammerMechAI _ai;
        private HammerTwoHandedGripIK _gripIK;
        private HammerRocketBooster _booster;
        private AudioSource _audioSource;

        private Transform _hipsTransform;
        private Transform _chestTransform;
        private Vector3 _lastFramePos;
        private Vector3 _estimatedVelocity;
        private bool _hasCrashed = false;
        private static PhysicsMaterial _mechPhysMat;

        private void Awake()
        {
            CacheComponents();
            if (_bones.Count == 0)
            {
                SetupRagdollBones();
            }
        }

        private void OnEnable()
        {
            _lastFramePos = transform.position;
            _estimatedVelocity = Vector3.zero;
        }

        private void Update()
        {
            if (!_isRagdoll)
            {
                if (!Application.isPlaying)
                {
                    _estimatedVelocity = Vector3.zero;
                    _lastFramePos = transform.position;
                    return;
                }

                float dt = Time.deltaTime;
                if (dt > 0.0001f)
                {
                    _estimatedVelocity = (transform.position - _lastFramePos) / dt;
                }
                _lastFramePos = transform.position;
            }
        }

        private void CacheComponents()
        {
            if (_animator == null) _animator = GetComponent<Animator>();
            if (_controller == null) _controller = GetComponent<CharacterController>();
            if (_ai == null) _ai = GetComponent<HammerMechAI>();
            if (_gripIK == null) _gripIK = GetComponent<HammerTwoHandedGripIK>();
            if (_booster == null) _booster = GetComponentInChildren<HammerRocketBooster>(true);
            if (_audioSource == null)
            {
                _audioSource = GetComponent<AudioSource>();
                if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
            }

            if (hammerWeaponTransform == null)
            {
                foreach (var mf in GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.name.Contains("Hammer"))
                    {
                        hammerWeaponTransform = mf.transform;
                        break;
                    }
                }
            }
        }

        [ContextMenu("Setup Mech Ragdoll Bones")]
        public void SetupRagdollBones()
        {
            _bones.Clear();
            CacheComponents();

            if (_mechPhysMat == null)
            {
                _mechPhysMat = new PhysicsMaterial("MechTitanPhysMat")
                {
                    dynamicFriction = 0.75f,
                    staticFriction = 0.80f,
                    bounciness = 0.05f,
                    frictionCombine = PhysicsMaterialCombine.Average,
                    bounceCombine = PhysicsMaterialCombine.Minimum
                };
            }

            if (_animator == null) return;

            Transform hips = _animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform spine1 = _animator.GetBoneTransform(HumanBodyBones.Spine);
            Transform spine3 = hips != null ? hips.Find("Spine_01/Spine_02/Spine_03") : null;
            Transform head = _animator.GetBoneTransform(HumanBodyBones.Head);

            Transform shL = _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            Transform elL = _animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            Transform shR = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Transform elR = _animator.GetBoneTransform(HumanBodyBones.RightLowerArm);

            Transform legL = _animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            Transform kneeL = _animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            Transform footL = _animator.GetBoneTransform(HumanBodyBones.LeftFoot);

            Transform legR = _animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            Transform kneeR = _animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            Transform footR = _animator.GetBoneTransform(HumanBodyBones.RightFoot);

            _hipsTransform = hips;
            _chestTransform = spine3;

            // Definition schema: (bone, transform, parentBone, colType, size/radius, height, mass, jointType)
            // colType: 0 = Capsule, 1 = Box, 2 = Sphere
            // jointType: 0 = Root (no joint), 1 = Spine, 2 = Head, 3 = Shoulder, 4 = Elbow (Hinge), 5 = Hip, 6 = Knee (Hinge), 7 = Ankle
            var configs = new (HumanBodyBones bone, Transform t, HumanBodyBones parent, int colType, Vector3 dim, float height, float mass, int jointType)[]
            {
                // Core
                (HumanBodyBones.Hips,          hips,   HumanBodyBones.LastBone,     0, new Vector3(0.55f, 0, 0), 1.10f, 1500f, 0),
                (HumanBodyBones.Spine,         spine1, HumanBodyBones.Hips,         0, new Vector3(0.65f, 0, 0), 0.95f, 1200f, 1),
                (HumanBodyBones.Chest,         spine3, HumanBodyBones.Spine,        1, new Vector3(1.60f, 1.30f, 1.40f), 0f, 3000f, 1),
                (HumanBodyBones.Head,          head,   HumanBodyBones.Chest,        2, new Vector3(0.48f, 0, 0), 0f, 400f,  2),

                // Arms
                (HumanBodyBones.LeftUpperArm,  shL,    HumanBodyBones.Chest,        0, new Vector3(0.35f, 0, 0), 0.90f, 500f,  3),
                (HumanBodyBones.LeftLowerArm,  elL,    HumanBodyBones.LeftUpperArm, 0, new Vector3(0.32f, 0, 0), 1.20f, 400f,  4),
                (HumanBodyBones.RightUpperArm, shR,    HumanBodyBones.Chest,        0, new Vector3(0.35f, 0, 0), 0.90f, 500f,  3),
                (HumanBodyBones.RightLowerArm, elR,    HumanBodyBones.RightUpperArm,0, new Vector3(0.32f, 0, 0), 1.20f, 400f,  4),

                // Legs
                (HumanBodyBones.LeftUpperLeg,  legL,   HumanBodyBones.Hips,         0, new Vector3(0.42f, 0, 0), 1.25f, 900f,  5),
                (HumanBodyBones.LeftLowerLeg,  kneeL,  HumanBodyBones.LeftUpperLeg, 0, new Vector3(0.38f, 0, 0), 1.45f, 750f,  6),
                (HumanBodyBones.LeftFoot,      footL,  HumanBodyBones.LeftLowerLeg, 1, new Vector3(0.65f, 0.40f, 1.05f), 0f, 400f, 7),

                (HumanBodyBones.RightUpperLeg, legR,   HumanBodyBones.Hips,         0, new Vector3(0.42f, 0, 0), 1.25f, 900f,  5),
                (HumanBodyBones.RightLowerLeg, kneeR,  HumanBodyBones.RightUpperLeg,0, new Vector3(0.38f, 0, 0), 1.45f, 750f,  6),
                (HumanBodyBones.RightFoot,     footR,  HumanBodyBones.RightLowerLeg,1, new Vector3(0.65f, 0.40f, 1.05f), 0f, 400f, 7)
            };

            var rbMap = new Dictionary<HumanBodyBones, Rigidbody>(16);

            foreach (var cfg in configs)
            {
                if (cfg.t == null) continue;

                // 1. Rigidbody
                var rb = cfg.t.GetComponent<Rigidbody>();
                if (rb == null) rb = cfg.t.gameObject.AddComponent<Rigidbody>();
                rb.mass = cfg.mass;
                rb.linearDamping = boneLinearDamping;
                rb.angularDamping = boneAngularDamping;
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.interpolation = RigidbodyInterpolation.Interpolate;

                // Continuous collision on massive torso parts to prevent tunneling through ground
                if (cfg.bone == HumanBodyBones.Hips || cfg.bone == HumanBodyBones.Chest)
                    rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                else
                    rb.collisionDetectionMode = CollisionDetectionMode.Discrete;

                rbMap[cfg.bone] = rb;

                // 2. Collider
                Collider col = null;
                if (cfg.colType == 0) // Capsule
                {
                    var cc = cfg.t.GetComponent<CapsuleCollider>();
                    if (cc == null) cc = cfg.t.gameObject.AddComponent<CapsuleCollider>();
                    cc.radius = cfg.dim.x;
                    cc.height = cfg.height;
                    cc.direction = 1; // Y-axis
                    cc.sharedMaterial = _mechPhysMat;
                    col = cc;
                }
                else if (cfg.colType == 1) // Box
                {
                    var bc = cfg.t.GetComponent<BoxCollider>();
                    if (bc == null) bc = cfg.t.gameObject.AddComponent<BoxCollider>();
                    bc.size = cfg.dim;
                    bc.sharedMaterial = _mechPhysMat;
                    col = bc;
                }
                else // Sphere
                {
                    var sc = cfg.t.GetComponent<SphereCollider>();
                    if (sc == null) sc = cfg.t.gameObject.AddComponent<SphereCollider>();
                    sc.radius = cfg.dim.x;
                    sc.sharedMaterial = _mechPhysMat;
                    col = sc;
                }

                col.isTrigger = false;
                col.enabled = false;

                // 3. CharacterJoint
                CharacterJoint joint = null;
                if (cfg.parent != HumanBodyBones.LastBone && rbMap.TryGetValue(cfg.parent, out var parentRb))
                {
                    joint = cfg.t.GetComponent<CharacterJoint>();
                    if (joint == null) joint = cfg.t.gameObject.AddComponent<CharacterJoint>();
                    joint.connectedBody = parentRb;
                    joint.enablePreprocessing = false;
                    joint.enableCollision = false;

                    // Projection strictly enforces joint constraints and prevents stretching limbs!
                    joint.enableProjection = true;
                    joint.projectionDistance = 0.05f;
                    joint.projectionAngle = 8.0f;

                    // Heavy hydraulic limit springs
                    SoftJointLimitSpring limitSpring = new SoftJointLimitSpring { spring = 4000f, damper = 350f };
                    joint.twistLimitSpring = limitSpring;
                    joint.swingLimitSpring = limitSpring;

                    ConfigureJointLimits(joint, cfg.jointType);
                }

                _bones.Add(new MechBoneDefinition
                {
                    boneType = cfg.bone,
                    boneTransform = cfg.t,
                    rigidbody = rb,
                    collider = col,
                    joint = joint,
                    initialLocalPos = cfg.t.localPosition,
                    initialLocalRot = cfg.t.localRotation
                });
            }

            // CRITICAL: Suppress all internal collision checks between mech bones to prevent self-penetration explosions
            for (int i = 0; i < _bones.Count; i++)
            {
                var colA = _bones[i].collider;
                if (colA == null) continue;

                if (_controller != null) Physics.IgnoreCollision(colA, _controller, true);

                for (int j = i + 1; j < _bones.Count; j++)
                {
                    var colB = _bones[j].collider;
                    if (colB != null)
                    {
                        Physics.IgnoreCollision(colA, colB, true);
                    }
                }
            }

            SetRagdollActive(false);
        }

        private void ConfigureJointLimits(CharacterJoint joint, int jointType)
        {
            switch (jointType)
            {
                case 1: // Spine & Chest
                    joint.lowTwistLimit = new SoftJointLimit { limit = -20f };
                    joint.highTwistLimit = new SoftJointLimit { limit = 20f };
                    joint.swing1Limit = new SoftJointLimit { limit = 35f };
                    joint.swing2Limit = new SoftJointLimit { limit = 20f };
                    break;

                case 2: // Head
                    joint.lowTwistLimit = new SoftJointLimit { limit = -45f };
                    joint.highTwistLimit = new SoftJointLimit { limit = 45f };
                    joint.swing1Limit = new SoftJointLimit { limit = 35f };
                    joint.swing2Limit = new SoftJointLimit { limit = 25f };
                    break;

                case 3: // Shoulder
                    joint.lowTwistLimit = new SoftJointLimit { limit = -65f };
                    joint.highTwistLimit = new SoftJointLimit { limit = 65f };
                    joint.swing1Limit = new SoftJointLimit { limit = 80f };
                    joint.swing2Limit = new SoftJointLimit { limit = 55f };
                    break;

                case 4: // Elbow (Hinge)
                    joint.lowTwistLimit = new SoftJointLimit { limit = -5f };
                    joint.highTwistLimit = new SoftJointLimit { limit = 130f };
                    joint.swing1Limit = new SoftJointLimit { limit = 10f };
                    joint.swing2Limit = new SoftJointLimit { limit = 10f };
                    break;

                case 5: // Hip
                    joint.lowTwistLimit = new SoftJointLimit { limit = -25f };
                    joint.highTwistLimit = new SoftJointLimit { limit = 25f };
                    joint.swing1Limit = new SoftJointLimit { limit = 65f };
                    joint.swing2Limit = new SoftJointLimit { limit = 25f };
                    break;

                case 6: // Knee (Hinge)
                    joint.lowTwistLimit = new SoftJointLimit { limit = -5f };
                    joint.highTwistLimit = new SoftJointLimit { limit = 130f };
                    joint.swing1Limit = new SoftJointLimit { limit = 8f };
                    joint.swing2Limit = new SoftJointLimit { limit = 8f };
                    break;

                case 7: // Ankle
                    joint.lowTwistLimit = new SoftJointLimit { limit = -20f };
                    joint.highTwistLimit = new SoftJointLimit { limit = 20f };
                    joint.swing1Limit = new SoftJointLimit { limit = 30f };
                    joint.swing2Limit = new SoftJointLimit { limit = 20f };
                    break;
            }
        }

        private void SetRagdollActive(bool active)
        {
            _isRagdoll = active;

            if (_animator != null) _animator.enabled = !active;
            if (_controller != null) _controller.enabled = !active;
            if (_gripIK != null) _gripIK.enabled = !active;

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
        }

        /// <summary>
        /// Main entry point called when the Titan Mech Boss is defeated.
        /// Collapses the giant war mech into full physical ragdoll simulation.
        /// </summary>
        public void TriggerRagdollDeath(Vector3 impactDirection, Vector3 hitPoint = default)
        {
            if (_isRagdoll) return;
            CacheComponents();

            // 1. Cut off rocket booster fire and audio
            if (_booster != null)
            {
                _booster.SetState(BoosterFlameState.Off);
            }

            // 2. Compute inherited kinetic movement momentum
            Vector3 inheritedMomentum = Vector3.zero;
            if (Application.isPlaying)
            {
                if (_controller != null && _controller.enabled)
                {
                    inheritedMomentum = _controller.velocity;
                }
                else
                {
                    inheritedMomentum = _estimatedVelocity;
                }
                inheritedMomentum = Vector3.ClampMagnitude(inheritedMomentum * inheritVelocityMultiplier, 15.0f);
            }

            // 3. Activate ragdoll physics on all 14 bones
            SetRagdollActive(true);

            // 4. Eject / drop the massive war hammer into the dirt
            if (dropWeaponOnDeath && hammerWeaponTransform != null)
            {
                DetachHammerWeapon(impactDirection);
            }

            // 5. Transfer velocity and apply fatal impact impulse
            if (impactDirection.sqrMagnitude < 0.01f)
            {
                impactDirection = -transform.forward; // Default backward tumble
            }
            impactDirection.Normalize();

            Vector3 fatalImpulse = (impactDirection * deathImpactForce) + (Vector3.up * (deathImpactForce * 0.25f));

            foreach (var b in _bones)
            {
                if (b.rigidbody != null && !b.rigidbody.isKinematic)
                {
                    b.rigidbody.linearVelocity = inheritedMomentum;
                    b.rigidbody.AddForce(fatalImpulse * (b.rigidbody.mass / 12000f), ForceMode.Impulse);
                }
            }

            // 6. Monitor chassis impact with the ground for cinematic crunch & shake
            _hasCrashed = false;
            StartCoroutine(GroundCrashMonitorRoutine());
        }

        private void DetachHammerWeapon(Vector3 impactDir)
        {
            if (hammerWeaponTransform == null) return;

            hammerWeaponTransform.SetParent(null, true);

            var rb = hammerWeaponTransform.GetComponent<Rigidbody>();
            if (rb == null) rb = hammerWeaponTransform.gameObject.AddComponent<Rigidbody>();
            rb.mass = 850f;
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearDamping = 0.4f;
            rb.angularDamping = 1.0f;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            var col = hammerWeaponTransform.GetComponent<BoxCollider>();
            if (col == null)
            {
                col = hammerWeaponTransform.gameObject.AddComponent<BoxCollider>();
                col.center = new Vector3(0.0f, 1.15f, 0.0f);
                col.size = new Vector3(0.85f, 3.80f, 1.65f);
            }
            col.enabled = true;
            col.sharedMaterial = _mechPhysMat;

            // Throw hammer slightly sideways/backwards
            Vector3 weaponTumble = impactDir * 12f + Vector3.up * 4.5f + Vector3.Cross(impactDir, Vector3.up) * UnityEngine.Random.Range(-5f, 5f);
            rb.linearVelocity = weaponTumble;
            rb.angularVelocity = UnityEngine.Random.insideUnitSphere * 4f;
        }

        private IEnumerator GroundCrashMonitorRoutine()
        {
            Transform coreBone = _chestTransform != null ? _chestTransform : _hipsTransform;
            float timer = 0f;
            float maxWait = 4.0f;

            while (timer < maxWait && !_hasCrashed)
            {
                timer += Time.deltaTime;

                if (coreBone != null && coreBone.position.y <= 1.4f)
                {
                    TriggerGroundCrash(coreBone.position);
                    yield break;
                }

                yield return new WaitForSeconds(0.05f);
            }
        }

        private void TriggerGroundCrash(Vector3 crashPoint)
        {
            if (_hasCrashed) return;
            _hasCrashed = true;

            // 1. Violent camera trauma shake
            SmoothOrbitCamera.ShakeImpact(2.4f, Vector3.down * 1.5f);

            // 2. Heavy metallic chassis crunch audio
            if (_audioSource != null)
            {
                AudioClip clip = sfxMetalCrash != null ? sfxMetalCrash : MechAudioSynth.GetFootstepStomp();
                if (clip != null)
                {
                    _audioSource.pitch = UnityEngine.Random.Range(0.75f, 0.90f);
                    _audioSource.PlayOneShot(clip, crashVolume);
                }
            }

            // 3. Ground impact dust explosion
            if (crashDustPrefab != null)
            {
                crashPoint.y = 0.05f;
                var dust = Instantiate(crashDustPrefab, crashPoint, Quaternion.identity);
                dust.transform.localScale = Vector3.one * 2.5f;
                Destroy(dust, 5.0f);
            }
        }

        public void ResetRagdoll()
        {
            SetRagdollActive(false);

            foreach (var b in _bones)
            {
                if (b.boneTransform != null)
                {
                    b.boneTransform.localPosition = b.initialLocalPos;
                    b.boneTransform.localRotation = b.initialLocalRot;
                }
            }
        }
    }
}
