using System.Collections.Generic;
using UnityEngine;
using PolygonCyberCity.NPC.Animation;

namespace PolygonCyberCity.NPC.Spawner
{
    /// <summary>
    /// High-performance zero-allocation Object Pool for CyberCity NPC archetypes.
    /// Supports multi-prefab model diversity per archetype with randomized model selection.
    /// </summary>
    public class CyberNPCPool : MonoBehaviour
    {
        public static CyberNPCPool Instance { get; private set; }

        [System.Serializable]
        public struct PoolConfig
        {
            public NPCType npcType;
            public GameObject prefab;
            public int initialPoolSize;
        }

        [Header("Default Assets")]
        [Tooltip("Default animator controller assigned to humanoid NPCs if none is attached.")]
        public RuntimeAnimatorController defaultAnimatorController;
        [Tooltip("Default Humanoid Avatar assigned if prefab animator has no avatar attached.")]
        public Avatar defaultHumanoidAvatar;

        [Header("Archetype Animation Profiles")]
        public NPCAnimationProfile civilianProfile;
        public NPCAnimationProfile richCitizenProfile;
        public NPCAnimationProfile helperBotProfile;
        public NPCAnimationProfile mercenaryProfile;
        public NPCAnimationProfile securityProfile;

        [Header("Pool Configurations")]
        public List<PoolConfig> poolConfigs = new List<PoolConfig>();

        private readonly Dictionary<string, Queue<CyberNPCController>> _prefabPools = new Dictionary<string, Queue<CyberNPCController>>();
        private readonly Dictionary<NPCType, List<GameObject>> _archetypePrefabs = new Dictionary<NPCType, List<GameObject>>();
        private Transform _poolRoot;

        public NPCAnimationProfile GetProfileForType(NPCType type)
        {
            switch (type)
            {
                case NPCType.Civilian:
                case NPCType.Cyborg:
                    return civilianProfile;
                case NPCType.RichCitizen:
                    return richCitizenProfile != null ? richCitizenProfile : civilianProfile;
                case NPCType.HelperBot:
                    return helperBotProfile != null ? helperBotProfile : civilianProfile;
                case NPCType.Mercenary:
                    return mercenaryProfile;
                case NPCType.StreetThug:
                    return securityProfile != null ? securityProfile : mercenaryProfile;
                default:
                    return civilianProfile;
            }
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _poolRoot = new GameObject("NPC_Pool_Container").transform;
            _poolRoot.SetParent(transform);

            PrewarmPools();
        }

        private void PrewarmPools()
        {
            _prefabPools.Clear();
            _archetypePrefabs.Clear();

            foreach (var cfg in poolConfigs)
            {
                if (cfg.prefab == null) continue;

                if (!_archetypePrefabs.ContainsKey(cfg.npcType))
                {
                    _archetypePrefabs[cfg.npcType] = new List<GameObject>();
                }
                if (!_archetypePrefabs[cfg.npcType].Contains(cfg.prefab))
                {
                    _archetypePrefabs[cfg.npcType].Add(cfg.prefab);
                }

                string key = cfg.prefab.name;
                if (!_prefabPools.ContainsKey(key))
                {
                    _prefabPools[key] = new Queue<CyberNPCController>();
                }

                int countToPrewarm = Mathf.Max(cfg.initialPoolSize, 2);
                for (int i = 0; i < countToPrewarm; i++)
                {
                    CreatePooledInstance(cfg.npcType, cfg.prefab);
                }
            }
        }

        private CyberNPCController CreatePooledInstance(NPCType type, GameObject prefab)
        {
            GameObject instance = Instantiate(prefab, _poolRoot);
            string key = prefab.name;
            instance.name = $"{key}_Pooled";

            // Ensure Capsule Collider for hit detection & raycasting
            var col = instance.GetComponent<CapsuleCollider>();
            if (col == null)
            {
                col = instance.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0f, 0.9f, 0f);
                col.radius = 0.35f;
                col.height = 1.8f;
            }

            // Ensure NavMeshAgent
            var agent = instance.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent == null)
            {
                agent = instance.AddComponent<UnityEngine.AI.NavMeshAgent>();
                agent.speed = 1.35f;
                agent.angularSpeed = 360f;
                agent.acceleration = 12f;
                agent.stoppingDistance = 0.5f;
                agent.radius = 0.35f;
                agent.height = 1.8f;
            }

            // Ensure Animator & Avatar
            var anim = instance.GetComponent<Animator>();
            if (anim == null) anim = instance.GetComponentInChildren<Animator>();
            if (anim == null) anim = instance.AddComponent<Animator>();

            if (defaultAnimatorController == null)
            {
#if UNITY_EDITOR
                defaultAnimatorController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/PolygonCyberCity/Animations/AC_CyberNPC.controller");
#endif
            }

            if (anim.runtimeAnimatorController == null && defaultAnimatorController != null)
            {
                anim.runtimeAnimatorController = defaultAnimatorController;
            }
            if (anim.avatar == null && defaultHumanoidAvatar != null)
            {
                anim.avatar = defaultHumanoidAvatar;
            }
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            anim.enabled = true;

            var controller = instance.GetComponent<CyberNPCController>();
            if (controller == null)
            {
                controller = instance.AddComponent<CyberNPCController>();
            }

            var bt = instance.GetComponent<PolygonCyberCity.NPC.BT.BehaviorTreeRunner>() ?? instance.AddComponent<PolygonCyberCity.NPC.BT.BehaviorTreeRunner>();
            controller.btRunner = bt;
            controller.npcType = type;
            controller.hitCollider = col;
            controller.agent = agent;
            controller.animator = anim;
            controller.animationProfile = GetProfileForType(type);

            // Ensure AudioSource & AudioChatter
            var aSrc = instance.GetComponent<AudioSource>();
            if (aSrc == null)
            {
                aSrc = instance.AddComponent<AudioSource>();
                aSrc.spatialBlend = 1.0f;
                aSrc.minDistance = 1.5f;
                aSrc.maxDistance = 14.0f;
                aSrc.playOnAwake = false;
            }
            controller.audioSource = aSrc;

            var chatter = instance.GetComponent<CyberNPCAudioChatter>() ?? instance.AddComponent<CyberNPCAudioChatter>();
            chatter.enableNPCVocalNoises = false;
            chatter.chatterVolume = 0f;
            controller.audioChatter = chatter;

            var avoid = instance.GetComponent<CyberNPCVehicleAvoidance>() ?? instance.AddComponent<CyberNPCVehicleAvoidance>();
            controller.vehicleAvoidance = avoid;

            // Now deactivate and add to pool
            instance.SetActive(false);

            if (!_prefabPools.ContainsKey(key))
            {
                _prefabPools[key] = new Queue<CyberNPCController>();
            }

            _prefabPools[key].Enqueue(controller);
            return controller;
        }

        public CyberNPCController GetFromPool(NPCType type, Vector3 position, Quaternion rotation, Transform player)
        {
            // Pick a random visual prefab variant registered under this archetype
            GameObject selectedPrefab = null;
            if (_archetypePrefabs.TryGetValue(type, out List<GameObject> prefabs) && prefabs != null && prefabs.Count > 0)
            {
                selectedPrefab = prefabs[Random.Range(0, prefabs.Count)];
            }
            else
            {
                // Fallback: search configs for matching prefab
                var cfg = poolConfigs.Find(c => c.npcType == type);
                if (cfg.prefab != null) selectedPrefab = cfg.prefab;
            }

            if (selectedPrefab == null) return null;

            string key = selectedPrefab.name;
            if (!_prefabPools.TryGetValue(key, out Queue<CyberNPCController> pool) || pool == null || pool.Count == 0)
            {
                CreatePooledInstance(type, selectedPrefab);
                _prefabPools.TryGetValue(key, out pool);
            }

            if (pool == null || pool.Count == 0) return null;

            var npc = pool.Dequeue();
            npc.transform.position = position;
            npc.transform.rotation = rotation;
            npc.gameObject.SetActive(true);

            // Re-verify Animator & Avatar on active spawn to prevent T-pose glitches
            if (npc.animator == null)
            {
                npc.animator = npc.GetComponent<Animator>();
                if (npc.animator == null) npc.animator = npc.GetComponentInChildren<Animator>();
                if (npc.animator == null) npc.animator = npc.gameObject.AddComponent<Animator>();
            }

            if (npc.animator != null)
            {
                if (npc.animator.runtimeAnimatorController == null && defaultAnimatorController != null)
                {
                    npc.animator.runtimeAnimatorController = defaultAnimatorController;
                }
                if (npc.animator.avatar == null && defaultHumanoidAvatar != null)
                {
                    npc.animator.avatar = defaultHumanoidAvatar;
                }
                npc.animator.applyRootMotion = false;
                npc.animator.enabled = true;
                npc.animator.Rebind();
                npc.animator.Update(0f);
            }

            if (npc.animationProfile == null)
            {
                npc.animationProfile = GetProfileForType(type);
            }

            if (npc.agent != null && UnityEngine.AI.NavMesh.SamplePosition(position, out UnityEngine.AI.NavMeshHit navHit, 3.0f, UnityEngine.AI.NavMesh.AllAreas))
            {
                if (!npc.agent.enabled) npc.agent.enabled = true;
                npc.agent.Warp(navHit.position);
            }

            npc.ResetForReuse();
            npc.InitializeNPC(player);

            return npc;
        }

        public void ReturnToPool(CyberNPCController npc)
        {
            if (npc == null) return;

            npc.gameObject.SetActive(false);
            npc.transform.SetParent(_poolRoot);

            // Derive key from object name
            string key = npc.gameObject.name.Replace("_Pooled", "").Replace("(Clone)", "").Trim();

            if (!_prefabPools.ContainsKey(key))
            {
                _prefabPools[key] = new Queue<CyberNPCController>();
            }

            _prefabPools[key].Enqueue(npc);
        }
    }
}
