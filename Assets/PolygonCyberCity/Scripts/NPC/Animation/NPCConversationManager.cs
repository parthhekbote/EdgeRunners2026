using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using PolygonCyberCity.NPC.Spawner;
using PolygonCyberCity.NPC.BT;

namespace PolygonCyberCity.NPC.Animation
{
    public enum ConversationRole
    {
        None,
        Speaker,
        Listener
    }

    /// <summary>
    /// Session tracking active conversation between two or more NPCs.
    /// Manages role swapping, duration timeout, facing positions, and clean termination.
    /// </summary>
    public class NPCConversationSession
    {
        public CyberNPCController NpcA { get; private set; }
        public CyberNPCController NpcB { get; private set; }
        public ConversationRole RoleA { get; private set; }
        public ConversationRole RoleB { get; private set; }
        public Vector3 TargetPosA { get; private set; }
        public Vector3 TargetPosB { get; private set; }
        public bool IsActive { get; private set; }

        private float _sessionDuration;
        private float _sessionTimer;
        private float _roleSwapInterval;
        private float _roleSwapTimer;

        public NPCConversationSession(CyberNPCController npcA, CyberNPCController npcB)
        {
            NpcA = npcA;
            NpcB = npcB;
            IsActive = false;
        }

        public bool InitializeSession()
        {
            if (NpcA == null || NpcB == null || NpcA.IsDead || NpcB.IsDead)
                return false;

            // Calculate mutual facing positions on NavMesh
            Vector3 posA = NpcA.transform.position;
            Vector3 posB = NpcB.transform.position;
            Vector3 midPoint = (posA + posB) * 0.5f;
            Vector3 dirAB = (posB - posA).normalized;

            if (dirAB == Vector3.zero) dirAB = NpcA.transform.forward;

            // Offset ~0.75m from midpoint along separating line (approx 1.5m stance distance)
            Vector3 targetA = midPoint - dirAB * 0.75f;
            Vector3 targetB = midPoint + dirAB * 0.75f;

            if (NavMesh.SamplePosition(targetA, out NavMeshHit hitA, 3.0f, NavMesh.AllAreas))
            {
                targetA = hitA.position;
            }
            if (NavMesh.SamplePosition(targetB, out NavMeshHit hitB, 3.0f, NavMesh.AllAreas))
            {
                targetB = hitB.position;
            }

            TargetPosA = targetA;
            TargetPosB = targetB;

            // Randomize roles (one Speaker, one Listener)
            if (Random.value > 0.5f)
            {
                RoleA = ConversationRole.Speaker;
                RoleB = ConversationRole.Listener;
            }
            else
            {
                RoleA = ConversationRole.Listener;
                RoleB = ConversationRole.Speaker;
            }

            // Timers setup
            _sessionDuration = Random.Range(10.0f, 25.0f);
            _sessionTimer = 0f;
            _roleSwapInterval = Random.Range(3.5f, 7.5f);
            _roleSwapTimer = 0f;

            IsActive = true;

            // Initialize both controllers
            NpcA.StartConversation(NpcB, RoleA, TargetPosA, this);
            NpcB.StartConversation(NpcA, RoleB, TargetPosB, this);

            return true;
        }

        public void UpdateSession(float deltaTime)
        {
            if (!IsActive) return;

            // Interrupt check: if either NPC died, was attacked, or left Normal macro state
            if (NpcA == null || NpcB == null || NpcA.IsDead || NpcB.IsDead ||
                NpcA.Blackboard == null || NpcB.Blackboard == null ||
                NpcA.Blackboard.CurrentState != NPCMacroState.Normal ||
                NpcB.Blackboard.CurrentState != NPCMacroState.Normal)
            {
                EndSession();
                return;
            }

            _sessionTimer += deltaTime;
            if (_sessionTimer >= _sessionDuration)
            {
                EndSession();
                return;
            }

            // Alternate speaker / listener roles periodically
            _roleSwapTimer += deltaTime;
            if (_roleSwapTimer >= _roleSwapInterval)
            {
                _roleSwapTimer = 0f;
                _roleSwapInterval = Random.Range(3.5f, 7.5f);

                RoleA = (RoleA == ConversationRole.Speaker) ? ConversationRole.Listener : ConversationRole.Speaker;
                RoleB = (RoleB == ConversationRole.Speaker) ? ConversationRole.Listener : ConversationRole.Speaker;

                NpcA.SetConversationRole(RoleA);
                NpcB.SetConversationRole(RoleB);
            }
        }

        public void EndSession()
        {
            if (!IsActive) return;
            IsActive = false;

            if (NpcA != null) NpcA.EndConversation();
            if (NpcB != null) NpcB.EndConversation();
        }
    }

    /// <summary>
    /// Global manager that periodically scans for available nearby NPCs, pairs them into conversation sessions,
    /// and manages active conversation lifecycles.
    /// </summary>
    public class NPCConversationManager : MonoBehaviour
    {
        private static NPCConversationManager _instance;
        public static NPCConversationManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[NPCConversationManager]");
                    _instance = go.AddComponent<NPCConversationManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        [Header("Pairing Configuration")]
        [Tooltip("Interval in seconds between scanning for candidate NPC pairs.")]
        public float scanInterval = 2.0f;

        [Tooltip("Maximum distance between two NPCs to initiate conversation.")]
        public float maxPairDistance = 6.0f;

        [Tooltip("Minimum distance required between two NPCs to initiate conversation.")]
        public float minPairDistance = 1.2f;

        [Tooltip("Cooldown duration before an NPC can enter another conversation after completing one.")]
        public float conversationCooldown = 15.0f;

        [Tooltip("Probability chance (0-1) to form a conversation when two candidate NPCs are nearby.")]
        [Range(0f, 1f)] public float pairChance = 0.65f;

        private readonly List<NPCConversationSession> _activeSessions = new List<NPCConversationSession>();
        private readonly Dictionary<CyberNPCController, float> _cooldowns = new Dictionary<CyberNPCController, float>();
        private float _scanTimer = 0f;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // Update all active sessions
            for (int i = _activeSessions.Count - 1; i >= 0; i--)
            {
                var session = _activeSessions[i];
                if (session != null && session.IsActive)
                {
                    session.UpdateSession(dt);
                }
                else
                {
                    _activeSessions.RemoveAt(i);
                }
            }

            // Periodic candidate pairing scan
            _scanTimer += dt;
            if (_scanTimer >= scanInterval)
            {
                _scanTimer = 0f;
                ScanAndPairNPCs();
            }
        }

        private void ScanAndPairNPCs()
        {
            var allNPCs = FindObjectsByType<CyberNPCController>();
            if (allNPCs == null || allNPCs.Length < 2) return;

            var candidates = new List<CyberNPCController>();

            foreach (var npc in allNPCs)
            {
                if (IsEligibleForConversation(npc))
                {
                    candidates.Add(npc);
                }
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                var npcA = candidates[i];
                if (!IsEligibleForConversation(npcA)) continue;

                for (int j = i + 1; j < candidates.Count; j++)
                {
                    var npcB = candidates[j];
                    if (!IsEligibleForConversation(npcB)) continue;

                    float dist = Vector3.Distance(npcA.transform.position, npcB.transform.position);
                    if (dist >= minPairDistance && dist <= maxPairDistance)
                    {
                        if (Random.value <= pairChance)
                        {
                            StartConversationSession(npcA, npcB);
                            break; // npcA is now paired
                        }
                    }
                }
            }
        }

        public bool IsEligibleForConversation(CyberNPCController npc)
        {
            if (npc == null || npc.IsDead || !npc.gameObject.activeInHierarchy) return false;
            if (npc.IsInConversation) return false;
            if (npc.Blackboard == null || npc.Blackboard.CurrentState != NPCMacroState.Normal) return false;

            // Only social-capable archetypes
            if (npc.npcType == NPCType.Mercenary || npc.npcType == NPCType.HelperBot) return false;

            // Cooldown check
            if (_cooldowns.TryGetValue(npc, out float endCooldownTime))
            {
                if (Time.time < endCooldownTime) return false;
            }

            return true;
        }

        public NPCConversationSession StartConversationSession(CyberNPCController npcA, CyberNPCController npcB)
        {
            if (npcA == null || npcB == null) return null;

            var session = new NPCConversationSession(npcA, npcB);
            if (session.InitializeSession())
            {
                _activeSessions.Add(session);
                _cooldowns[npcA] = Time.time + conversationCooldown;
                _cooldowns[npcB] = Time.time + conversationCooldown;
                Debug.Log($"[NPCConversationManager] Started conversation session between '{npcA.name}' and '{npcB.name}'");
                return session;
            }
            return null;
        }

        public static bool TryStartManualConversation(CyberNPCController npcA, CyberNPCController npcB)
        {
            return Instance.StartConversationSession(npcA, npcB) != null;
        }
    }
}
