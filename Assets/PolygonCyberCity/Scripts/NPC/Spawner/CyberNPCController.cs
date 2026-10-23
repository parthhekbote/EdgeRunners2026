using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using PolygonCyberCity.NPC.BT;
using PolygonCyberCity.NPC.BT.Trees;
using PolygonCyberCity.NPC.Animation;

namespace PolygonCyberCity.NPC.Spawner
{
    public enum NPCState
    {
        Alive,
        Dying,
        Dead
    }

    /// <summary>
    /// Core NPC Actor controller managing NavMesh navigation, animation states,
    /// Behavior Tree execution, damage handling, and cyberpunk scanner metadata.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(BehaviorTreeRunner))]
    public class CyberNPCController : MonoBehaviour, SyntyCombined.ISyntyDamageable
    {
        [Header("Archetype Configuration")]
        public NPCType npcType = NPCType.Civilian;
        public NPCFaction faction = NPCFaction.Civilians;
        public ThreatRating threatRating = ThreatRating.Low;
        public float maxHealth = 100f;
        [Tooltip("Damage dealt to Alita by a civilian-class NPC punch (kept low on purpose).")]
        public float civilianAttackDamage = 6f;
        [Tooltip("Distance at which a hostile NPC notices Alita and starts the fight.")]
        public float hostileAggroRange = 14f;
        public float walkSpeed = 1.35f;
        public float runSpeed = 4.5f;

        [Header("Animation Configuration")]
        public RuntimeAnimatorController npcAnimatorController;
        public NPCAnimationProfile animationProfile;

        [Header("Lifecycle & Death System")]
        public float despawnDelay = 10.0f;
        public float settleDuration = 3.0f;
        public float sinkDuration = 2.0f;
        public bool enableRagdollOnDeath = true;

        [Header("Cyberpunk Scanner Info")]
        public ScannerProfile scannerProfile;

        [Header("Components & References")]
        public NavMeshAgent agent;
        public Animator animator;
        public BehaviorTreeRunner btRunner;
        public Collider hitCollider;
        public AudioSource audioSource;
        public CyberNPCAudioChatter audioChatter;
        public CyberNPCVehicleAvoidance vehicleAvoidance;

        [Header("VFX & Audio")]
        public GameObject hitSparksVFX;
        public GameObject deathVFX;
        public AudioClip[] hurtSounds;
        public AudioClip[] warningSounds;
        public AudioClip[] panicSounds;

        private BTBlackboard _blackboard;
        private Transform _playerTransform;
        private float _currentHealth;
        private NPCState _lifeState = NPCState.Alive;
        private int _originalLayer = 0;
        private CapsuleCollider _capsuleCollider;
        private Rigidbody[] _ragdollRigidbodies;
        private Collider[] _ragdollColliders;
        private AnimatorOverrideController _overrideController;

        private float _alertRadius = 12f;
        private static readonly Collider[] AlertBuffer = new Collider[16];

        private float _combatLayerWeight = 0f;
        private float _upperBodyWeight = 0f;
        private bool _wasMovingLastFrame = false;
        private float _startedTimer = 0f;
        private float _idleFidgetTimer = 0f;
        private float _nextFidgetTime = 3.5f;
        private float _currentFidgetDuration = 0f;
        private bool _isPlayingFidget = false;
        private float _currentLookWeight = 0f;
        private Vector3 _smoothLookTarget;

        private bool _isInConversation = false;
        private ConversationRole _conversationRole = ConversationRole.None;
        private CyberNPCController _conversationPartner;
        private Vector3 _conversationTargetPosition;
        private NPCConversationSession _currentConversationSession;

        public BTBlackboard Blackboard => _blackboard;
        public NPCState LifeState => _lifeState;
        public bool IsAlive => _lifeState == NPCState.Alive;
        public bool IsDead => _lifeState != NPCState.Alive;
        public float HealthPercent => Mathf.Clamp01(_currentHealth / maxHealth);
        public bool IsInConversation => _isInConversation;
        public ConversationRole ConversationRole => _conversationRole;
        public CyberNPCController ConversationPartner => _conversationPartner;
        public Vector3 ConversationTargetPosition => _conversationTargetPosition;

        public void StartConversation(CyberNPCController partner, ConversationRole role, Vector3 targetPos, NPCConversationSession session)
        {
            if (_lifeState != NPCState.Alive || _blackboard == null || _blackboard.CurrentState != NPCMacroState.Normal) return;

            _isInConversation = true;
            _conversationPartner = partner;
            _conversationRole = role;
            _conversationTargetPosition = targetPos;
            _currentConversationSession = session;
            _blackboard.Set("IsInConversation", true);

            var facial = GetComponent<Synty.Sidekick.Facial.SyntySidekickFacialAnimator>();
            if (facial != null)
            {
                facial.SetTalkingState(role == ConversationRole.Speaker);
            }
        }

        public void SetConversationRole(ConversationRole role)
        {
            _conversationRole = role;
            var facial = GetComponent<Synty.Sidekick.Facial.SyntySidekickFacialAnimator>();
            if (facial != null)
            {
                facial.SetTalkingState(role == ConversationRole.Speaker);
            }
        }

        public void EndConversation()
        {
            if (!_isInConversation) return;

            _isInConversation = false;
            _conversationRole = ConversationRole.None;
            _conversationPartner = null;
            _currentConversationSession = null;
            if (_blackboard != null)
            {
                _blackboard.Set("IsInConversation", false);
            }

            var facial = GetComponent<Synty.Sidekick.Facial.SyntySidekickFacialAnimator>();
            if (facial != null)
            {
                facial.SetTalkingState(false);
            }
        }

        private readonly HashSet<int> _cachedParamHashes = new HashSet<int>();

        private void CacheAnimatorParameters()
        {
            _cachedParamHashes.Clear();
            if (animator == null || animator.runtimeAnimatorController == null) return;
            var paramsArray = animator.parameters;
            for (int i = 0; i < paramsArray.Length; i++)
            {
                _cachedParamHashes.Add(paramsArray[i].nameHash);
            }
        }

        private void SetFloatSafe(int hash, float value)
        {
            if (animator != null && _cachedParamHashes.Contains(hash)) animator.SetFloat(hash, value);
        }

        private void SetFloatDampedSafe(int hash, float value, float dampTime, float deltaTime)
        {
            if (animator != null && _cachedParamHashes.Contains(hash)) animator.SetFloat(hash, value, dampTime, deltaTime);
        }

        private void SetBoolSafe(int hash, bool value)
        {
            if (animator != null && _cachedParamHashes.Contains(hash)) animator.SetBool(hash, value);
        }

        private void SetIntSafe(int hash, int value)
        {
            if (animator != null && _cachedParamHashes.Contains(hash)) animator.SetInteger(hash, value);
        }

        private void SetTriggerSafe(int hash)
        {
            if (animator != null && _cachedParamHashes.Contains(hash)) animator.SetTrigger(hash);
        }

        private void ResetTriggerSafe(int hash)
        {
            if (animator != null && _cachedParamHashes.Contains(hash)) animator.ResetTrigger(hash);
        }

        private void Awake()
        {
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            if (btRunner == null) btRunner = GetComponent<BehaviorTreeRunner>() ?? gameObject.AddComponent<BehaviorTreeRunner>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (hitCollider == null) hitCollider = GetComponentInChildren<Collider>();
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (audioChatter == null) audioChatter = GetComponent<CyberNPCAudioChatter>() ?? gameObject.AddComponent<CyberNPCAudioChatter>();
            if (vehicleAvoidance == null) vehicleAvoidance = GetComponent<CyberNPCVehicleAvoidance>() ?? gameObject.AddComponent<CyberNPCVehicleAvoidance>();

            _originalLayer = gameObject.layer;
            _capsuleCollider = GetComponent<CapsuleCollider>();
            if (_capsuleCollider == null) _capsuleCollider = GetComponentInChildren<CapsuleCollider>();
            _ragdollRigidbodies = GetComponentsInChildren<Rigidbody>();
            _ragdollColliders = GetComponentsInChildren<Collider>();
            CacheInitialBoneTransforms();
            DisableRagdoll();

            if (animator != null && animator.runtimeAnimatorController == null)
            {
                if (npcAnimatorController != null)
                {
                    animator.runtimeAnimatorController = npcAnimatorController;
                }
#if UNITY_EDITOR
                else
                {
                    animator.runtimeAnimatorController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/PolygonCyberCity/Animations/AC_CyberNPC.controller");
                }
#endif
            }

            // Build an override controller so we can hot-swap death clips at runtime
            // without modifying the shared Animator Controller asset.
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                _overrideController = new AnimatorOverrideController(animator.runtimeAnimatorController);
                animator.runtimeAnimatorController = _overrideController;
                // Re-cache parameters because swapping the controller clears them
                CacheAnimatorParameters();
            }

            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 1.0f;
                audioSource.minDistance = 2f;
                audioSource.maxDistance = 25f;
                audioSource.playOnAwake = false;
            }

            ConfigureAgent();
            GenerateRandomScannerProfile();
        }

        private void Start()
        {
            // Auto-initialize if placed statically in the scene without the pool
            if (_blackboard == null)
            {
                Transform targetPlayer = null;
                var playerGo = GameObject.FindWithTag("Player");
                if (playerGo != null)
                {
                    targetPlayer = playerGo.transform;
                }
                else if (Camera.main != null)
                {
                    targetPlayer = Camera.main.transform;
                }

                if (animationProfile == null && CyberNPCPool.Instance != null)
                {
                    animationProfile = CyberNPCPool.Instance.GetProfileForType(npcType);
                }

                InitializeNPC(targetPlayer);
            }
        }

        private void ConfigureAgent()
        {
            if (agent != null)
            {
                agent.speed = walkSpeed;
                agent.acceleration = 14f;
                agent.angularSpeed = 420f;
                agent.stoppingDistance = 0.5f;
                agent.autoBraking = true;
                agent.autoRepath = true;
                agent.radius = 0.35f;
                agent.height = 1.8f;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
                agent.avoidancePriority = Random.Range(30, 70);
            }
        }

        public void InitializeNPC(Transform player)
        {
            _playerTransform = player;

            if (animationProfile == null && CyberNPCPool.Instance != null)
            {
                animationProfile = CyberNPCPool.Instance.GetProfileForType(npcType);
            }

            if (animationProfile != null)
            {
                walkSpeed = animationProfile.walkSpeed;
                runSpeed = animationProfile.runSpeed;
            }

            _currentHealth = maxHealth;
            _lifeState = NPCState.Alive;

            if (animator != null)
            {
                // Layer weights default to 0 in the controller; the hit-reaction layer must be live.
                int reactionLayer = animator.GetLayerIndex("Reaction");
                if (reactionLayer >= 0) animator.SetLayerWeight(reactionLayer, 1f);
            }

            if (animator != null && animationProfile != null)
            {
                animator.speed = Random.Range(animationProfile.animSpeedVariationMin, animationProfile.animSpeedVariationMax);
            }

            if (_blackboard == null)
            {
                _blackboard = new BTBlackboard();
            }
            else
            {
                _blackboard.Clear();
            }

            _blackboard.Health = _currentHealth;
            _blackboard.MaxHealth = maxHealth;
            _blackboard.MoveSpeed = walkSpeed;
            _blackboard.PlayerTransform = _playerTransform;
            _blackboard.CurrentState = NPCMacroState.Normal;
            _blackboard.CurrentLOD = 0;

            EnsureNavMeshBinding();

            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.speed = walkSpeed;
                agent.ResetPath();
            }

            if (hitCollider != null)
            {
                hitCollider.enabled = true;
            }

            RollReputationBehaviour();
            BuildBehaviorTree();
        }

        private bool _hostileToAlita;
        private bool _provoked;

        private bool CanFightAlita =>
            npcType == NPCType.Civilian || npcType == NPCType.RichCitizen ||
            npcType == NPCType.Cyborg || npcType == NPCType.StreetThug;

        /// <summary>Decides, once per spawn, whether this NPC distrusts Alita (see AlitaReputation).</summary>
        private void RollReputationBehaviour()
        {
            _hostileToAlita = CanFightAlita && AlitaReputation.RollHostile();
            _provoked = false;
            _blackboard.Set("FightPlayer", false);
        }

        /// <summary>Makes this NPC attack Alita right now (used for the post-warning street scuffle).</summary>
        public void ForceAttackAlita()
        {
            if (_lifeState != NPCState.Alive || _blackboard == null || !CanFightAlita) return;
            _hostileToAlita = true;
            _blackboard.Set("FightPlayer", true);
            _blackboard.WasAttacked = false;
            _blackboard.CurrentState = NPCMacroState.Aggressive;
            PlayRandomAudio(warningSounds, 0.9f);
            if (btRunner != null)
            {
                btRunner.ResetTree();
                btRunner.ForceTick();
            }
        }

        private void UpdateReputationBehaviour()
        {
            if (!CanFightAlita) return;

            if (AlitaReputation.IsKnownHero)
            {
                // Vulcan is down: the city respects Alita. Nobody attacks her, even when hit;
                // anyone still fighting backs off and runs.
                _hostileToAlita = false;
                _provoked = false;
                if (_blackboard.Get<bool>("FightPlayer"))
                {
                    _blackboard.Set("FightPlayer", false);
                    _blackboard.WasAttacked = true;
                    _blackboard.CurrentState = NPCMacroState.Fleeing;
                    if (btRunner != null) btRunner.ResetTree();
                }
                return;
            }

            if (!_hostileToAlita) return;

            if (!AlitaReputation.CityWarningGiven)
            {
                // Ido hasn't warned Alita yet: hostile locals stay calm until he does.
                return;
            }

            bool fighting = _blackboard.Get<bool>("FightPlayer");
            if (!fighting && _blackboard.CurrentState == NPCMacroState.Normal && _blackboard.DistanceToPlayer < hostileAggroRange)
            {
                // Unprovoked: this one jumps the newcomer before she touches anyone.
                _blackboard.Set("FightPlayer", true);
                _blackboard.CurrentState = NPCMacroState.Aggressive;
                PlayRandomAudio(warningSounds, 0.9f);
            }
            else if (fighting && _blackboard.DistanceToPlayer > 35f)
            {
                _blackboard.Set("FightPlayer", false);
                _blackboard.CurrentState = NPCMacroState.Normal;
            }
        }

        private void BuildBehaviorTree()
        {
            BTNode rootTree;

            switch (npcType)
            {
                case NPCType.Mercenary:
                case NPCType.StreetThug:
                    rootTree = MercenaryBT.Build(gameObject, agent, animator, this);
                    break;
                case NPCType.HelperBot:
                    rootTree = DroneBotBT.Build(gameObject, agent, animator, this);
                    break;
                case NPCType.Civilian:
                case NPCType.RichCitizen:
                case NPCType.Cyborg:
                default:
                    rootTree = CivilianBT.Build(gameObject, agent, animator, this);
                    break;
            }

            if (btRunner == null) btRunner = GetComponent<BehaviorTreeRunner>() ?? gameObject.AddComponent<BehaviorTreeRunner>();
            btRunner.Initialize(rootTree, _blackboard);
        }

        private void EnsureNavMeshBinding()
        {
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            if (agent != null && !agent.isOnNavMesh)
            {
                if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 8.0f, NavMesh.AllAreas))
                {
                    agent.Warp(hit.position);
                }
            }
        }

        private void Update()
        {
            if (_lifeState != NPCState.Alive || _blackboard == null) return;

            // Ensure agent stays active on NavMesh
            EnsureNavMeshBinding();

            // Player distance update
            if (_playerTransform != null)
            {
                float dist = Vector3.Distance(transform.position, _playerTransform.position);
                _blackboard.DistanceToPlayer = dist;
                _blackboard.IsPlayerNear = dist < 12f;

                // Update LOD tier
                if (dist < 18f) _blackboard.CurrentLOD = 0;
                else if (dist < 40f) _blackboard.CurrentLOD = 1;
                else if (dist < 65f) _blackboard.CurrentLOD = 2;
                else _blackboard.CurrentLOD = 3;
            }

            // Animation LOD layer management
            if (animator != null && animator.isActiveAndEnabled)
            {
                if (_isInConversation && _blackboard.CurrentLOD <= 1)
                {
                    _upperBodyWeight = Mathf.MoveTowards(_upperBodyWeight, 1.0f, Time.deltaTime * 3.0f);
                }

                switch (_blackboard.CurrentLOD)
                {
                    case 0: // Full fidelity
                        animator.enabled = true;
                        if (animator.layerCount > 1) animator.SetLayerWeight(1, _upperBodyWeight);
                        break;
                    case 1: // Reduced IK, upper body active only if in conversation
                        animator.enabled = true;
                        if (animator.layerCount > 1) animator.SetLayerWeight(1, _isInConversation ? _upperBodyWeight : 0f);
                        break;
                    case 2: // Base locomotion only (static facing pose, no upper body gesture layer)
                        animator.enabled = true;
                        if (animator.layerCount > 1) animator.SetLayerWeight(1, 0f);
                        if (animator.layerCount > 2) animator.SetLayerWeight(2, 0f);
                        break;
                    case 3: // Disable animator entirely at far LOD
                        animator.enabled = false;
                        break;
                }
            }

            // Idle fidget variation timer
            if (_lifeState == NPCState.Alive && animator != null && animator.isActiveAndEnabled && _blackboard != null && _blackboard.CurrentLOD <= 2)
            {
                // Autonomous idle fidgets only trigger during Normal roaming/standing state
                // (Dedicated tasks like PerformingActivity or Loitering manage their own contextual poses)
                if (_blackboard.CurrentState == NPCMacroState.Normal)
                {
                    float curSpeed = (agent != null && agent.isOnNavMesh) ? agent.velocity.magnitude : 0f;
                    if (curSpeed < 0.12f)
                    {
                        _idleFidgetTimer += Time.deltaTime;
                        if (!_isPlayingFidget)
                        {
                            if (_idleFidgetTimer >= _nextFidgetTime)
                            {
                                _isPlayingFidget = true;
                                _idleFidgetTimer = 0f;
                                _currentFidgetDuration = Random.Range(3.5f, 6.5f);
                                // Pick among 29 rich urban idle variations based on archetype
                                int nextIdle = GetRandomUrbanIdle();
                                if (nextIdle == 0) nextIdle = Random.Range(1, 30);
                                SetIntSafe(NPCAnimatorHashes.RandomIdle, nextIdle);
                            }
                        }
                        else
                        {
                            if (_idleFidgetTimer >= _currentFidgetDuration)
                            {
                                // Return smoothly to Base Idle
                                _isPlayingFidget = false;
                                _idleFidgetTimer = 0f;
                                _nextFidgetTime = Random.Range(
                                    animationProfile != null ? animationProfile.idleFidgetIntervalMin : 3.0f,
                                    animationProfile != null ? animationProfile.idleFidgetIntervalMax : 6.5f
                                );
                                SetIntSafe(NPCAnimatorHashes.RandomIdle, 0);
                            }
                        }
                    }
                    else
                    {
                        if (_isPlayingFidget || _idleFidgetTimer > 0f)
                        {
                            _isPlayingFidget = false;
                            _idleFidgetTimer = 0f;
                            SetIntSafe(NPCAnimatorHashes.RandomIdle, 0);
                        }
                    }
                }
            }

            UpdateReputationBehaviour();

            // Smooth Locomotion Animation Synchronization
            UpdateSmoothLocomotion();
        }

        private void UpdateSmoothLocomotion()
        {
            // Never write locomotion parameters while dying or dead —
            // they would fight the death animation the same frame Die() plays it.
            if (_lifeState != NPCState.Alive) return;
            if (animator == null || !animator.isActiveAndEnabled) return;

            float currentSpeed = (agent != null && agent.enabled && agent.isOnNavMesh) ? agent.velocity.magnitude : 0f;
            float maxSpeed = (agent != null) ? Mathf.Max(agent.speed, 0.1f) : walkSpeed;

            float targetSpeedParam = 0f;
            int gait = 0;
            bool isMoving = currentSpeed > 0.08f;

            if (_blackboard != null && _blackboard.CurrentState == NPCMacroState.Fleeing)
            {
                targetSpeedParam = Mathf.Lerp(3.5f, 6.0f, Mathf.Clamp01(currentSpeed / Mathf.Max(runSpeed, 3.5f)));
                gait = 3; // Sprint
            }
            else if (isMoving)
            {
                // Walking: directly aligns with physical velocity in m/s (1.35 m/s = pure walk)
                targetSpeedParam = currentSpeed;
                gait = currentSpeed > 2.5f ? 2 : 1; // 1 = Walk, 2 = Run
            }
            else
            {
                targetSpeedParam = 0f;
                gait = 0;
            }

            // Damped parameter updates
            SetFloatDampedSafe(NPCAnimatorHashes.MoveSpeed, targetSpeedParam, 0.12f, Time.deltaTime);
            SetFloatDampedSafe(NPCAnimatorHashes.Speed, currentSpeed, 0.12f, Time.deltaTime);
            SetIntSafe(NPCAnimatorHashes.CurrentGait, gait);
            SetBoolSafe(NPCAnimatorHashes.IsWalking, isMoving);
            SetBoolSafe(NPCAnimatorHashes.IsRunning, gait >= 2);
            SetBoolSafe(NPCAnimatorHashes.IsStopped, !isMoving);
            SetBoolSafe(NPCAnimatorHashes.MovementInputHeld, isMoving);
            SetBoolSafe(NPCAnimatorHashes.IsGrounded, true);
            SetBoolSafe(NPCAnimatorHashes.IsCrouching, _blackboard != null && _blackboard.CurrentState == NPCMacroState.Cowering);
            SetBoolSafe(NPCAnimatorHashes.SwordDrawn, _blackboard != null && _blackboard.WeaponDrawn);

            // Compute local-space velocity for 2D strafe blend tree
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                Vector3 localVelocity = transform.InverseTransformDirection(agent.velocity);
                float strafeX = Mathf.Clamp(localVelocity.x / maxSpeed, -1f, 1f);
                float strafeZ = Mathf.Clamp(localVelocity.z / maxSpeed, -1f, 1f);
                SetFloatDampedSafe(NPCAnimatorHashes.StrafeX, strafeX, 0.12f, Time.deltaTime);
                SetFloatDampedSafe(NPCAnimatorHashes.StrafeZ, strafeZ, 0.12f, Time.deltaTime);

                // Turn amount
                float turnAngle = Vector3.SignedAngle(transform.forward, agent.desiredVelocity.normalized, Vector3.up);
                float turnAmount = Mathf.Clamp(turnAngle / 180f, -1f, 1f);
                SetFloatDampedSafe(NPCAnimatorHashes.TurnAmount, turnAmount, 0.1f, Time.deltaTime);
            }

            // Start/Stop state detection
            bool justStarted = isMoving && !_wasMovingLastFrame;
            SetBoolSafe(NPCAnimatorHashes.IsStarting, justStarted || _startedTimer > 0f);
            if (justStarted) _startedTimer = 0.3f;
            if (_startedTimer > 0f) _startedTimer -= Time.deltaTime;
            _wasMovingLastFrame = isMoving;

            // Combat layer weight blending (Layer 2)
            if (animator.layerCount > 2)
            {
                float targetCombatWeight = (_blackboard != null && _blackboard.WeaponDrawn) ? 1.0f : 0.0f;
                _combatLayerWeight = Mathf.MoveTowards(_combatLayerWeight, targetCombatWeight, Time.deltaTime * 5f);
                animator.SetLayerWeight(2, _combatLayerWeight);
            }
        }

        private int GetRandomUrbanIdle()
        {
            switch (npcType)
            {
                case NPCType.StreetThug:
                case NPCType.Mercenary:
                    // Combat/gritty idles: Posture Aggressive (28), Arms Folded Grumpy (12), Lean Arms Folded (14), Look Behind (22), Bored Kick (16), Hands on Hips (7), Lean Leg Up (15)
                    int[] combatIdles = { 0, 3, 7, 12, 13, 14, 15, 16, 22, 28 };
                    return combatIdles[Random.Range(0, combatIdles.Length)];

                case NPCType.RichCitizen:
                    // Elite idles: Phone Call (2), Check Watch (5), Hair Comb (24), Arms Folded (3), Head Nod (25), Selfie (9), Lean Back (13)
                    int[] richIdles = { 0, 1, 2, 5, 6, 9, 13, 14, 19, 24, 25 };
                    return richIdles[Random.Range(0, richIdles.Length)];

                case NPCType.HelperBot:
                    // Robotic idles: Thoughtful scanning (6), Look at Sky (21), Weight Shift (4), Look Behind (22), Head Nod (25)
                    int[] botIdles = { 0, 4, 6, 21, 22, 25 };
                    return botIdles[Random.Range(0, botIdles.Length)];

                case NPCType.Civilian:
                case NPCType.Cyborg:
                default:
                    // Full diverse pedestrian idles (1..29)
                    int[] civIdles = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 13, 14, 15, 17, 18, 19, 20, 21, 22, 23, 26, 27, 29 };
                    return civIdles[Random.Range(0, civIdles.Length)];
            }
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (animator == null || !animator.isActiveAndEnabled || _lifeState != NPCState.Alive || _blackboard == null) return;

            // LookAt IK: Conversation Partner Look (LOD 0/1) or Player Look (LOD 0)
            if (_isInConversation && _conversationPartner != null && _conversationPartner.IsAlive && _blackboard.CurrentLOD <= 1)
            {
                Vector3 partnerHead = _conversationPartner.transform.position + Vector3.up * 1.6f;
                _smoothLookTarget = Vector3.Lerp(_smoothLookTarget == Vector3.zero ? partnerHead : _smoothLookTarget, partnerHead, Time.deltaTime * 4.5f);

                _currentLookWeight = Mathf.MoveTowards(_currentLookWeight, 0.85f, Time.deltaTime * 2.5f);
                animator.SetLookAtPosition(_smoothLookTarget);
                animator.SetLookAtWeight(_currentLookWeight, 0.2f, 0.8f, 0.9f, 0.5f);

                if (_blackboard.CurrentLOD == 0)
                {
                    animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 1f);
                    animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, 0.5f);
                    animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 1f);
                    animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, 0.5f);

                    AdjustFootIK(AvatarIKGoal.LeftFoot);
                    AdjustFootIK(AvatarIKGoal.RightFoot);
                }
            }
            else if (_blackboard.CurrentLOD == 0 && _playerTransform != null && _blackboard.IsPlayerNear && _blackboard.CurrentState != NPCMacroState.Fleeing)
            {
                Vector3 playerHead = _playerTransform.position + Vector3.up * 1.6f;
                _smoothLookTarget = Vector3.Lerp(_smoothLookTarget == Vector3.zero ? playerHead : _smoothLookTarget, playerHead, Time.deltaTime * 3.5f);

                _currentLookWeight = Mathf.MoveTowards(_currentLookWeight, 0.7f, Time.deltaTime * 2.0f);
                animator.SetLookAtPosition(_smoothLookTarget);
                animator.SetLookAtWeight(_currentLookWeight, 0.15f, 0.85f, 0.9f, 0.5f);

                // Foot IK (LOD 0 only)
                animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 1f);
                animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, 0.5f);
                animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 1f);
                animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, 0.5f);

                AdjustFootIK(AvatarIKGoal.LeftFoot);
                AdjustFootIK(AvatarIKGoal.RightFoot);
            }
            else
            {
                if (_currentLookWeight > 0.01f)
                {
                    _currentLookWeight = Mathf.MoveTowards(_currentLookWeight, 0f, Time.deltaTime * 3.0f);
                    animator.SetLookAtWeight(_currentLookWeight, 0.1f, 0.5f, 0.5f, 0.5f);
                }
            }
        }

        private void AdjustFootIK(AvatarIKGoal foot)
        {
            Vector3 footPos = animator.GetIKPosition(foot);
            if (Physics.Raycast(footPos + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 1.0f, LayerMask.GetMask("Default")))
            {
                Vector3 adjustedPos = hit.point + Vector3.up * 0.05f;
                animator.SetIKPosition(foot, adjustedPos);
                Quaternion footRot = Quaternion.FromToRotation(Vector3.up, hit.normal) * transform.rotation;
                animator.SetIKRotation(foot, footRot);
            }
        }

        public void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitDirection, bool isHeavy)
        {
            if (_lifeState != NPCState.Alive) return;

            if (_isInConversation)
            {
                if (_currentConversationSession != null && _currentConversationSession.IsActive)
                    _currentConversationSession.EndSession();
                else
                    EndConversation();
            }

            _currentHealth -= damage;
            if (_blackboard != null)
            {
                _blackboard.Health = _currentHealth;
                _blackboard.WasAttacked = true;
                _blackboard.LastThreatPosition = _playerTransform != null ? _playerTransform.position : hitPoint;
            }

            PlayRandomAudio(hurtSounds, 0.9f);

            // Resolve physical reaction type via NPCAnimationReactionResolver
            // Skip Hit trigger entirely for lethal shots — Die() will handle the animation directly
            // so the hit-reaction / cower animation never starts before death plays.
            if (animator != null && animator.isActiveAndEnabled && _currentHealth > 0f)
            {
                int dirIdx = CalculateHitDirectionIndex(hitDirection);
                bool isBlocking = _blackboard != null && _blackboard.Get<bool>("IsBlocking");
                var reaction = NPCAnimationReactionResolver.Resolve(damage, _currentHealth, maxHealth, isHeavy, isBlocking, damage * 0.5f, 100f);

                SetIntSafe(NPCAnimatorHashes.HitDirection, dirIdx);
                SetIntSafe(NPCAnimatorHashes.HitType, (int)reaction);
                SetTriggerSafe(NPCAnimatorHashes.Hit);
            }

            AlertNearbyCrowd(hitPoint);

            if (_currentHealth <= 0f)
            {
                Die(hitDirection, hitPoint);
            }
            else
            {
                // Once hit, each civilian-class NPC either fights back or runs (about half and half).
                // Before Ido's warning, and after Vulcan's defeat, nobody fights back.
                bool alreadyFighting = _blackboard != null && _blackboard.Get<bool>("FightPlayer");
                bool fightBack = CanFightAlita && AlitaReputation.CityWarningGiven && !AlitaReputation.IsKnownHero &&
                                 (alreadyFighting || Random.value < AlitaReputation.FightBackFraction);
                if (fightBack)
                {
                    // Fights back instead of running.
                    _provoked = true;
                    if (_blackboard != null)
                    {
                        _blackboard.Set("FightPlayer", true);
                        _blackboard.CurrentState = NPCMacroState.Aggressive;
                    }
                }
                else if (npcType == NPCType.Civilian || npcType == NPCType.RichCitizen || npcType == NPCType.Cyborg || npcType == NPCType.HelperBot)
                {
                    if (_blackboard != null)
                    {
                        _blackboard.Set("FightPlayer", false);
                        _blackboard.CurrentState = NPCMacroState.Fleeing;
                        _blackboard.WasAttacked = true;
                    }
                    if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = false;
                }
                else if (AlitaReputation.IsKnownHero)
                {
                    // Gang members respect the Pit champion too: they run instead of hitting back.
                    if (_blackboard != null)
                    {
                        _blackboard.CurrentState = NPCMacroState.Fleeing;
                        _blackboard.WasAttacked = true;
                    }
                    if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = false;
                }
                else
                {
                    if (_blackboard != null) _blackboard.CurrentState = NPCMacroState.Aggressive;
                }

                if (btRunner != null)
                {
                    btRunner.ResetTree();
                    btRunner.ForceTick();
                }
            }
        }

        private int CalculateHitDirectionIndex(Vector3 hitDir)
        {
            if (hitDir == Vector3.zero) return 1;
            Vector3 localDir = transform.InverseTransformDirection(hitDir);
            float angle = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;

            if (Mathf.Abs(angle) > 135f) return 1; // Front hit
            if (Mathf.Abs(angle) < 45f)  return 2; // Back hit
            if (angle < 0f)              return 3; // Left hit
            return 4;                              // Right hit
        }

        private void AlertNearbyCrowd(Vector3 threatOrigin)
        {
            int hits = Physics.OverlapSphereNonAlloc(transform.position, _alertRadius, AlertBuffer);
            for (int i = 0; i < hits; i++)
            {
                var otherNpc = AlertBuffer[i].GetComponentInParent<CyberNPCController>();
                if (otherNpc != null && otherNpc != this && otherNpc.IsAlive)
                {
                    otherNpc.ReceiveThreatAlert(threatOrigin, npcType);
                }
            }
        }

        public void ReceiveThreatAlert(Vector3 threatPos, NPCType alertSourceType)
        {
            if (_lifeState != NPCState.Alive || _blackboard == null) return;

            if (_isInConversation)
            {
                if (_currentConversationSession != null && _currentConversationSession.IsActive)
                    _currentConversationSession.EndSession();
                else
                    EndConversation();
            }

            _blackboard.LastThreatPosition = threatPos;

            // Set perception type for TaskPerceptionReact
            int perceptionType = (alertSourceType == NPCType.Mercenary) ? 1 : 0; // 0=Suspicious, 1=Gunshot/Combat
            _blackboard.Set("PerceptionType", perceptionType);

            if (npcType == NPCType.Civilian || npcType == NPCType.RichCitizen || npcType == NPCType.HelperBot)
            {
                _blackboard.CurrentState = NPCMacroState.Fleeing;
                _blackboard.WasAttacked = true;
            }
            else if ((npcType == NPCType.Mercenary || npcType == NPCType.StreetThug) && !AlitaReputation.IsKnownHero)
            {
                _blackboard.CurrentState = NPCMacroState.Aggressive;
            }

            if (btRunner != null) btRunner.ForceTick();
        }

        public void TriggerPanicAlert()
        {
            if (audioSource != null && !audioSource.isPlaying)
            {
                PlayRandomAudio(panicSounds, 0.7f);
            }
        }

        public void TriggerWarningCallout()
        {
            if (audioSource != null && !audioSource.isPlaying)
            {
                PlayRandomAudio(warningSounds, 0.85f);
            }
        }

        public void TriggerAttackAudio()
        {
        }

        private void PlayRandomAudio(AudioClip[] clips, float volume)
        {
            if (clips != null && clips.Length > 0 && audioSource != null)
            {
                var clip = clips[Random.Range(0, clips.Length)];
                if (clip != null)
                {
                    audioSource.pitch = Random.Range(0.92f, 1.08f);
                    audioSource.PlayOneShot(clip, volume);
                }
            }
        }

        private void Die(Vector3 deathImpulse, Vector3 hitPoint)
        {
            if (_lifeState != NPCState.Alive) return;

            // 1. State Machine & Lifecycle Transition
            _lifeState = NPCState.Dying;
            if (_blackboard != null) _blackboard.CurrentState = NPCMacroState.Dying;

            // Stop Behavior Tree Execution immediately to stop AI ticks
            if (btRunner != null)
            {
                btRunner.ResetTree();
                btRunner.enabled = false;
            }

            // Stop NavMeshAgent pathfinding immediately
            if (agent != null)
            {
                if (agent.isOnNavMesh)
                {
                    agent.isStopped = true;
                    agent.velocity = Vector3.zero;
                }
                agent.enabled = false;
            }

            // 2. Collision Layer Switching
            int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
            int targetLayer = ignoreRaycastLayer >= 0 ? ignoreRaycastLayer : gameObject.layer;
            SetLayerRecursively(gameObject, targetLayer);

            // 3. Grounded check
            bool isGrounded = CheckIsGrounded();

            // 4. Cinematic Hit-Pause
            if (animationProfile != null && animationProfile.enableHitPause)
                StartCoroutine(HitPauseRoutine(animationProfile.hitPauseTimeScale, animationProfile.hitPauseDuration));

            // 5. Impact VFX at hit point
            GameObject impactPrefab = (animationProfile != null && animationProfile.deathImpactVFX != null)
                ? animationProfile.deathImpactVFX : deathVFX;
            if (impactPrefab != null)
                Instantiate(impactPrefab, hitPoint != Vector3.zero ? hitPoint : transform.position + Vector3.up * 0.8f, Quaternion.identity);

            // 6. Death audio
            AudioClip deathAudio = (animationProfile != null) ? animationProfile.GetRandomDeathSound() : null;
            if (deathAudio != null && audioSource != null)
            { audioSource.pitch = Random.Range(0.88f, 1.12f); audioSource.PlayOneShot(deathAudio, 1.0f); }
            else
                PlayRandomAudio(hurtSounds, 0.6f);

            // 7. Directional death info
            Vector3 localDir = deathImpulse.sqrMagnitude > 0.01f
                ? transform.InverseTransformDirection(deathImpulse) : -Vector3.forward;
            var hitDir = SyntyCombined.SyntyCombatAnimationResolver.GetDominantDirection(localDir);
            bool isHeavyHit = deathImpulse.magnitude > 3.0f;

            // 8. Euphoria Ragdoll Death
            bool usedRagdoll = false;
            if (!isGrounded || enableRagdollOnDeath)
            {
                var euphoria = GetComponent<InsaneGun.Combat.EuphoriaRagdollController>();
                if (euphoria == null) euphoria = gameObject.AddComponent<InsaneGun.Combat.EuphoriaRagdollController>();
                Vector3 fatalPoint = hitPoint != Vector3.zero ? hitPoint : transform.position + Vector3.up * 1.1f;
                Vector3 fatalDir = deathImpulse != Vector3.zero ? deathImpulse : transform.forward;
                euphoria.TriggerEuphoriaRagdoll(fatalPoint, fatalDir, 18f, isFatal: true);
                usedRagdoll = true;
            }

            if (!usedRagdoll && animator != null && animator.isActiveAndEnabled)
            {
                animator.speed = 1.0f;
                animator.Rebind();
                animator.Update(0f);

                // Set grounded parameters explicitly to prevent InAir transitions
                SetBoolSafe(NPCAnimatorHashes.IsGrounded, true);
                SetBoolSafe(NPCAnimatorHashes.IsCrouching, false);
                SetBoolSafe(NPCAnimatorHashes.IsJumping, false);
                SetBoolSafe(NPCAnimatorHashes.IsPanicking, false);
                SetFloatSafe(NPCAnimatorHashes.MoveSpeed, 0f);
                SetFloatSafe(NPCAnimatorHashes.Speed, 0f);
                ResetTriggerSafe(NPCAnimatorHashes.Attack);
                ResetTriggerSafe(NPCAnimatorHashes.Hit);
                ResetTriggerSafe(NPCAnimatorHashes.Dodge);
                ResetTriggerSafe(NPCAnimatorHashes.Knockdown);
                ResetTriggerSafe(NPCAnimatorHashes.PerceptionReact);
                ResetTriggerSafe(NPCAnimatorHashes.Gesture);
                ResetTriggerSafe(NPCAnimatorHashes.Activity);

                // Zero out upper body and secondary layer weights so no crouching/gestures bleed into death pose
                for (int l = 1; l < animator.layerCount; l++)
                {
                    animator.SetLayerWeight(l, 0f);
                }

                SetIntSafe(NPCAnimatorHashes.HitDirection, CalculateHitDirectionIndex(deathImpulse));

                // Heavy hit: stagger kneel -> fall
                bool usedKD = false;
                if (isHeavyHit && animationProfile != null && animationProfile.useKnockdownSequenceOnHeavyHit)
                {
                    string kd = animationProfile.knockdownToDeathStateName;
                    if (!string.IsNullOrEmpty(kd) && TryPlayState(kd, out _))
                    {
                        StartCoroutine(KnockdownToDeathRoutine(hitDir, animationProfile.knockdownToDeathDelay));
                        usedKD = true;
                    }
                }

                if (!usedKD)
                {
                    bool started = false;

                    // P1: Profile directional clip pool (random variant) — override into Death state
                    if (!started && animationProfile != null)
                    {
                        AnimationClip profileClip = animationProfile.GetRandomDeathClip(hitDir);
                        if (profileClip != null)
                        {
                            started = PlayDeathClipViaOverride(profileClip, hitDir);
                            Debug.Log($"[CyberNPC] Die P1 Profile clip: {profileClip.name} -> started={started}", this);
                        }
                    }

                    // P2: SyntyAnimClipData directional clips (combat NPCs)
                    if (!started)
                    {
                        SyntyCombined.SyntyAnimClipData cd = animationProfile?.combatClipData;
                        if (cd != null)
                        {
                            AnimationClip syntyClip = SyntyCombined.SyntyCombatAnimationResolver.GetDeath(cd, hitDir);
                            if (syntyClip != null)
                            {
                                started = PlayDeathClipViaOverride(syntyClip, hitDir);
                                Debug.Log($"[CyberNPC] Die P2 Synty clip: {syntyClip.name} -> started={started}", this);
                            }
                        }
                    }

                    // P3: Try playing directional state directly by name across all layers
                    if (!started)
                    {
                        started = TryPlayDirectionalDeathState(hitDir, out string statePlayed, out int layerPlayed);
                        if (started)
                        {
                            Debug.Log($"[CyberNPC] Die P3 named state: '{statePlayed}' on layer {layerPlayed}", this);
                        }
                    }

                    // P4: Procedural directional collapse (fallback)
                    if (!started)
                    {
                        Debug.Log($"[CyberNPC] Die P4 procedural collapse dir={hitDir}", this);
                        StartCoroutine(ProceduralCollapseRoutine(hitDir));
                    }
                }

                StartCoroutine(DirectionalRecoilImpulseRoutine(deathImpulse));

                if (_capsuleCollider != null) { _capsuleCollider.height = 0.35f; _capsuleCollider.center = new Vector3(0f, 0.18f, 0f); }
            }

            StartCoroutine(ExecuteDeathLifecycleRoutine());
        }

        /// <summary>
        /// Applies physical momentum shift (GTA Vice City-style kinetic slide)
        /// upon lethal impact, smoothly moving the body along the impact vector.
        /// </summary>
        private IEnumerator DirectionalRecoilImpulseRoutine(Vector3 impulse)
        {
            float duration = 0.35f;
            float elapsed = 0f;
            Vector3 pushDir = impulse.sqrMagnitude > 0.01f ? impulse.normalized : -transform.forward;
            pushDir.y = 0f;
            if (pushDir.sqrMagnitude < 0.01f) pushDir = -transform.forward;

            float pushForce = Mathf.Clamp(impulse.magnitude * 0.45f, 0.9f, 3.4f);

            while (elapsed < duration && _lifeState == NPCState.Dying)
            {
                elapsed += Time.deltaTime;
                float speed = Mathf.Lerp(pushForce, 0f, elapsed / duration);
                Vector3 moveDelta = pushDir * (speed * Time.deltaTime);

                if (!Physics.Raycast(transform.position + Vector3.up * 0.5f, pushDir, moveDelta.magnitude + 0.25f, LayerMask.GetMask("Default", "Terrain", "Ground", "Walls")))
                {
                    transform.position += moveDelta;
                }
                yield return null;
            }
        }

        private bool CheckIsGrounded()
        {
            Vector3 origin = transform.position + Vector3.up * 0.4f;
            return Physics.Raycast(origin, Vector3.down, 0.8f, LayerMask.GetMask("Default", "Terrain", "Ground"));
        }

        private struct BoneTransform
        {
            public Transform transform;
            public Vector3 localPosition;
            public Quaternion localRotation;
        }
        private List<BoneTransform> _initialBoneTransforms;

        private void CacheInitialBoneTransforms()
        {
            _initialBoneTransforms = new List<BoneTransform>();
            if (_ragdollRigidbodies == null) return;
            foreach (var rb in _ragdollRigidbodies)
            {
                if (rb != null)
                {
                    _initialBoneTransforms.Add(new BoneTransform
                    {
                        transform = rb.transform,
                        localPosition = rb.transform.localPosition,
                        localRotation = rb.transform.localRotation
                    });
                }
            }
        }

        private void RestoreBoneTransforms()
        {
            if (_initialBoneTransforms == null) return;
            foreach (var bt in _initialBoneTransforms)
            {
                if (bt.transform != null)
                {
                    bt.transform.localPosition = bt.localPosition;
                    bt.transform.localRotation = bt.localRotation;
                }
            }
        }

        private bool EnableRagdoll(Vector3 impulse, Vector3 hitPoint)
        {
            if (_ragdollRigidbodies == null || _ragdollRigidbodies.Length == 0) return false;

            if (animator != null) animator.enabled = false;

            foreach (var rb in _ragdollRigidbodies)
            {
                if (rb == null) continue;
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.WakeUp();
            }

            foreach (var col in _ragdollColliders)
            {
                if (col == null || col == hitCollider) continue;
                col.enabled = true;
            }

            Rigidbody targetRb = GetClosestRigidbody(hitPoint);
            if (targetRb != null && impulse.sqrMagnitude > 0.01f)
            {
                targetRb.AddForceAtPosition(impulse * 12f, hitPoint, ForceMode.Impulse);
            }

            return true;
        }

        private Rigidbody GetClosestRigidbody(Vector3 point)
        {
            if (_ragdollRigidbodies == null || _ragdollRigidbodies.Length == 0) return null;
            Rigidbody best = _ragdollRigidbodies[0];
            float minDist = float.MaxValue;
            foreach (var rb in _ragdollRigidbodies)
            {
                if (rb == null) continue;
                float d = Vector3.SqrMagnitude(rb.position - point);
                if (d < minDist) { minDist = d; best = rb; }
            }
            return best;
        }

        private void DisableRagdoll()
        {
            if (_ragdollRigidbodies == null) return;
            foreach (var rb in _ragdollRigidbodies)
            {
                if (rb == null) continue;
                rb.isKinematic = true;
                rb.useGravity = false;
            }
            RestoreBoneTransforms();
        }

        /// <summary>
        /// Absolute fallback death visual: smoothly tilts the NPC in the direction of the hit
        /// to simulate a directional collapse when no animator death states are available.
        /// </summary>
        private IEnumerator ProceduralCollapseRoutine(SyntyCombined.CardinalDirection dir = SyntyCombined.CardinalDirection.Back)
        {
            if (animator != null) animator.enabled = false;

            float elapsed = 0f;
            float duration = 0.55f;
            Quaternion startRot = transform.rotation;

            // Tilt direction based on which side was hit
            Vector3 tiltEuler = dir switch
            {
                SyntyCombined.CardinalDirection.Front => new Vector3(82f,  0f,   0f),  // Falls forward
                SyntyCombined.CardinalDirection.Left  => new Vector3(0f,   0f,  82f),  // Falls left
                SyntyCombined.CardinalDirection.Right => new Vector3(0f,   0f, -82f),  // Falls right
                _                                     => new Vector3(-82f, 15f, 20f),  // Falls backward
            };
            Quaternion collapseRot = startRot * Quaternion.Euler(tiltEuler);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                transform.rotation = Quaternion.Slerp(startRot, collapseRot, t);
                yield return null;
            }
            transform.rotation = collapseRot;
        }

        /// <summary>
        /// Cinematic time-scale freeze on the lethal-hit frame.
        /// Uses unscaled time so the freeze duration is consistent regardless of existing slow-mo.
        /// </summary>
        private IEnumerator HitPauseRoutine(float targetTimeScale, float realDuration)
        {
            float prev = Time.timeScale;
            Time.timeScale = targetTimeScale;
            yield return new WaitForSecondsRealtime(realDuration);
            Time.timeScale = prev;
        }

        /// <summary>
        /// Plays a knockdown/stagger clip then crossfades into the directional death clip
        /// after a configurable delay, creating a cinematic "stagger → collapse" sequence.
        /// Also triggers ground-impact audio after the settle phase.
        /// </summary>
        private IEnumerator KnockdownToDeathRoutine(SyntyCombined.CardinalDirection dir, float delay)
        {
            yield return new WaitForSeconds(delay);

            if (animator == null || !animator.isActiveAndEnabled) yield break;

            bool played = false;
            if (animationProfile != null)
            { var c = animationProfile.GetRandomDeathClip(dir); if (c != null) { played = PlayDeathClipViaOverride(c, dir); } }
            if (!played) { var cd = animationProfile?.combatClipData; if (cd != null) { var c = SyntyCombined.SyntyCombatAnimationResolver.GetDeath(cd, dir); if (c != null) { played = PlayDeathClipViaOverride(c, dir); } } }
            if (!played) { played = TryPlayDirectionalDeathState(dir, out _, out _); }
            if (!played) StartCoroutine(ProceduralCollapseRoutine(dir));

            // Ground impact audio — timed to when body hits the floor
            yield return new WaitForSeconds(settleDuration * 0.65f);
            AudioClip impact = animationProfile != null ? animationProfile.GetRandomGroundImpact() : null;
            if (impact != null && audioSource != null)
            { audioSource.pitch = Random.Range(0.85f, 1.05f); audioSource.PlayOneShot(impact, 0.9f); }
        }

        private string[] GetStateNamesForDirection(SyntyCombined.CardinalDirection dir)
        {
            switch (dir)
            {
                case SyntyCombined.CardinalDirection.Front:
                    return new string[] { "A_Death_F_01_Sword", "A_Death_F_01_Pose_Sword", "A_Death_Forward", "Death_Front", "Death_F", "Death", "Die", "Dead" };
                case SyntyCombined.CardinalDirection.Back:
                    return new string[] { "A_Death_B_01_Sword", "A_Death_B_01_Pose_Sword", "A_Death_Back", "Death_Back", "Death_B", "Death", "Die", "Dead" };
                case SyntyCombined.CardinalDirection.Left:
                    return new string[] { "A_Death_L_01_Sword", "A_Death_L_01_Pose_Sword", "A_Death_Left", "Death_Left", "Death_L", "Death", "Die", "Dead" };
                case SyntyCombined.CardinalDirection.Right:
                    return new string[] { "A_Death_R_01_Sword", "A_Death_R_01_Pose_Sword", "A_Death_Right", "Death_Right", "Death_R", "Death", "Die", "Dead" };
                default:
                    return new string[] { "A_Death_F_01_Sword", "A_Death_B_01_Sword", "A_Death_L_01_Sword", "A_Death_R_01_Sword", "Death", "Die", "Dead" };
            }
        }

        private bool TryPlayState(string stateName, out int layerPlayed)
        {
            layerPlayed = -1;
            if (animator == null || !animator.isActiveAndEnabled || string.IsNullOrEmpty(stateName)) return false;

            int hash = Animator.StringToHash(stateName);
            for (int l = animator.layerCount - 1; l >= 0; l--)
            {
                if (animator.HasState(l, hash))
                {
                    animator.SetLayerWeight(l, 1.0f);
                    animator.Play(hash, l, 0f);
                    layerPlayed = l;
                    return true;
                }
            }
            return false;
        }

        private bool TryPlayDirectionalDeathState(SyntyCombined.CardinalDirection hitDir, out string statePlayed, out int layerPlayed)
        {
            statePlayed = null;
            layerPlayed = -1;
            string[] candidates = GetStateNamesForDirection(hitDir);
            foreach (var name in candidates)
            {
                if (TryPlayState(name, out layerPlayed))
                {
                    statePlayed = name;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Swaps <paramref name="clip"/> into a matching death state slot of the
        /// <see cref="AnimatorOverrideController"/>, then plays that state immediately.
        /// Returns true if the state was found and played.
        /// </summary>
        private bool PlayDeathClipViaOverride(AnimationClip clip, SyntyCombined.CardinalDirection hitDir)
        {
            if (clip == null || animator == null || !animator.isActiveAndEnabled) return false;

            string[] candidates = GetStateNamesForDirection(hitDir);

            if (_overrideController != null)
            {
                var overrides = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<AnimationClip, AnimationClip>>(_overrideController.overridesCount);
                _overrideController.GetOverrides(overrides);

                bool slotFound = false;
                for (int i = 0; i < overrides.Count; i++)
                {
                    if (overrides[i].Key == null) continue;
                    string keyName = overrides[i].Key.name;
                    foreach (string s in candidates)
                    {
                        if (keyName.IndexOf(s, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            overrides[i] = new System.Collections.Generic.KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, clip);
                            slotFound = true;
                            break;
                        }
                    }
                    if (slotFound) break;
                }

                if (!slotFound)
                {
                    for (int i = 0; i < overrides.Count; i++)
                    {
                        if (overrides[i].Key == null) continue;
                        string keyName = overrides[i].Key.name;
                        if (keyName.IndexOf("Death", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                            keyName.IndexOf("Die", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            overrides[i] = new System.Collections.Generic.KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, clip);
                            slotFound = true;
                            break;
                        }
                    }
                }

                if (slotFound)
                {
                    _overrideController.ApplyOverrides(overrides);
                    if (TryPlayDirectionalDeathState(hitDir, out string sPlayed, out int lPlayed))
                    {
                        Debug.Log($"[CyberNPC] PlayDeathClipViaOverride: overrode state '{sPlayed}' on layer {lPlayed} -> clip '{clip.name}'", this);
                        return true;
                    }
                }
            }

            if (TryPlayDirectionalDeathState(hitDir, out string stateP, out int layerP))
            {
                Debug.Log($"[CyberNPC] PlayDeathClipViaOverride: direct fallback to state '{stateP}' on layer {layerP}", this);
                return true;
            }

            Debug.LogWarning($"[CyberNPC] PlayDeathClipViaOverride: no death state found for dir={hitDir}. Clip='{clip.name}'", this);
            return false;
        }

        private IEnumerator ExecuteDeathLifecycleRoutine()
        {
            // Phase 1: Settle — monitor death clip progression and freeze animator on final frame
            float elapsed = 0f;
            bool frozen = false;
            while (elapsed < settleDuration)
            {
                elapsed += Time.deltaTime;
                if (!frozen && animator != null && animator.isActiveAndEnabled)
                {
                    bool reachedEnd = false;
                    for (int l = 0; l < animator.layerCount; l++)
                    {
                        if (animator.GetLayerWeight(l) > 0.01f)
                        {
                            var info = animator.GetCurrentAnimatorStateInfo(l);
                            if (info.normalizedTime >= 0.90f || elapsed >= 1.15f)
                            {
                                reachedEnd = true;
                                break;
                            }
                        }
                    }

                    if (reachedEnd)
                    {
                        animator.speed = 0f; // Freeze animation on exact ground death pose
                        frozen = true;
                    }
                }
                yield return null;
            }

            // Ground impact audio (fires when the death animation reaches the floor)
            AudioClip impactAudio = animationProfile != null ? animationProfile.GetRandomGroundImpact() : null;
            if (impactAudio != null && audioSource != null)
            { audioSource.pitch = Random.Range(0.85f, 1.05f); audioSource.PlayOneShot(impactAudio, 0.85f); }

            // Phase 2: State transition to DEAD & disable Animator
            _lifeState = NPCState.Dead;
            if (_blackboard != null) _blackboard.CurrentState = NPCMacroState.Dead;
            if (animator != null)
            {
                animator.speed = 1f;
                animator.enabled = false;
            }

            // Phase 3: Remain in world for despawn delay
            float remainingDelay = Mathf.Max(0f, despawnDelay - settleDuration - sinkDuration);
            if (remainingDelay > 0f)
                yield return new WaitForSeconds(remainingDelay);

            // Phase 4: Dissolve VFX then smooth sink into ground before pool return
            if (animationProfile != null && animationProfile.dissolveSinkVFX != null)
                Instantiate(animationProfile.dissolveSinkVFX, transform.position, Quaternion.identity);

            Vector3 startPos = transform.position;
            Vector3 targetPos = startPos - Vector3.up * 1.4f;
            elapsed = 0f;

            while (elapsed < sinkDuration)
            {
                elapsed += Time.deltaTime;
                transform.position = Vector3.Lerp(startPos, targetPos, elapsed / sinkDuration);
                yield return null;
            }

            if (CyberNPCPool.Instance != null)
            {
                CyberNPCPool.Instance.ReturnToPool(this);
            }
        }

        private void SetLayerRecursively(GameObject obj, int layer)
        {
            if (obj == null) return;
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        public void ResetForReuse()
        {
            StopAllCoroutines();
            _lifeState = NPCState.Alive;
            _currentHealth = maxHealth;

            SetLayerRecursively(gameObject, _originalLayer);
            DisableRagdoll();

            var euphoria = GetComponent<InsaneGun.Combat.EuphoriaRagdollController>();
            if (euphoria != null)
            {
                euphoria.ResetRagdollImmediate();
            }

            if (agent != null)
            {
                agent.enabled = true;
            }

            if (_capsuleCollider != null)
            {
                _capsuleCollider.height = 1.8f;
                _capsuleCollider.center = new Vector3(0f, 0.9f, 0f);
            }

            if (hitCollider != null)
            {
                hitCollider.enabled = true;
            }

            if (animator != null)
            {
                animator.speed = 1.0f;
                animator.enabled = true;
                animator.Rebind();
                animator.Update(0f);

                // Reset layer weights
                if (animator.layerCount > 1) animator.SetLayerWeight(1, 0f);
                if (animator.layerCount > 2) animator.SetLayerWeight(2, 0f);
                if (animator.layerCount > 3) animator.SetLayerWeight(3, 0f);
                if (animator.layerCount > 4) animator.SetLayerWeight(4, 0f);
            }

            if (btRunner != null)
            {
                btRunner.enabled = true;
            }

            if (_blackboard != null)
            {
                _blackboard.Clear();
                _blackboard.CurrentState = NPCMacroState.Normal;
            }
        }

        private void GenerateRandomScannerProfile()
        {
            string[] firstNames = { "Kaelen", "Vex", "Nyx", "Jax", "Riker", "Kira", "Tatsuya", "Zero", "Echo", "Sora", "Devon", "Rin" };
            string[] cyberwares = { "Sandevistan Mk.3", "Mantis Blades", "Kiroshi Optics v4", "Subdermal Armor", "Gorilla Arms", "Reflex Tuner", "Optical Camo", "Synaptic Accelerator" };

            scannerProfile = new ScannerProfile
            {
                CharacterName = firstNames[Random.Range(0, firstNames.Length)] + " #" + Random.Range(100, 999),
                Cyberware = cyberwares[Random.Range(0, cyberwares.Length)],
                Faction = faction,
                Threat = threatRating,
                Bounty = threatRating == ThreatRating.Lethal ? Random.Range(5000, 15000) : (threatRating == ThreatRating.High ? Random.Range(2000, 5000) : 0)
            };
        }
    }
}
