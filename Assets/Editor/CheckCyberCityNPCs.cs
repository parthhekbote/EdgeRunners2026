#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.AI;
using PolygonCyberCity.NPC.Spawner;
using PolygonCyberCity.NPC.Editor;

namespace PolygonCyberCity.Editor
{
    public static class CheckCyberCityNPCs
    {
        public static void InspectScene()
        {
            const string scenePath = "Assets/PolygonCyberCity/Scenes/Demo_City.unity";
            Debug.Log($"[NPC_Audit] Opening scene: {scenePath}");

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError($"[NPC_Audit] FAILED to open scene at {scenePath}!");
                return;
            }

            Debug.Log($"[NPC_Audit] Scene opened successfully! Root count: {scene.rootCount}");

            // 1. Check Player
            var player = GameObject.FindWithTag("Player");
            Debug.Log($"[NPC_Audit] GameObject with tag 'Player': {(player != null ? player.name : "NULL - NO OBJECT TAGGED 'Player' FOUND!")}");
            if (player != null)
            {
                Debug.Log($"[NPC_Audit] Player position: {player.transform.position}, active: {player.activeInHierarchy}");
            }

            // 2. Check NavMesh
            NavMeshTriangulation navData = NavMesh.CalculateTriangulation();
            Debug.Log($"[NPC_Audit] NavMesh vertices count: {navData.vertices.Length}, indices: {navData.indices.Length}");

            // 3. Check All Scene Roots
            var roots = scene.GetRootGameObjects();
            Debug.Log($"[NPC_Audit] Root objects count: {roots.Length}");
            foreach (var r in roots)
            {
                Debug.Log($"[NPC_Audit] Root: '{r.name}', active={r.activeSelf}");
            }

            // 4. Check CyberNPCManager / CyberNPCSpawner / CyberNPCPool
            var manager = GameObject.Find("CyberNPCManager");
            Debug.Log($"[NPC_Audit] CyberNPCManager: {(manager != null ? "Found" : "NOT FOUND IN SCENE")}");

            if (manager != null)
            {
                var spawner = manager.GetComponent<CyberNPCSpawner>();
                Debug.Log($"[NPC_Audit] CyberNPCSpawner: {(spawner != null ? "Found" : "NULL")}");
                if (spawner != null)
                {
                    Debug.Log($"[NPC_Audit] Spawner playerTransform: {(spawner.playerTransform != null ? spawner.playerTransform.name : "NULL")}");
                    Debug.Log($"[NPC_Audit] Spawner maxActiveNPCs: {spawner.maxActiveNPCs}");
                    Debug.Log($"[NPC_Audit] Spawner groundLayerMask: {spawner.groundLayerMask.value}");
                }

                var pool = manager.GetComponent<CyberNPCPool>();
                Debug.Log($"[NPC_Audit] CyberNPCPool: {(pool != null ? "Found" : "NULL")}");
                if (pool != null)
                {
                    Debug.Log($"[NPC_Audit] Pool defaultAnimatorController: {(pool.defaultAnimatorController != null ? pool.defaultAnimatorController.name : "NULL")}");
                    Debug.Log($"[NPC_Audit] Pool defaultHumanoidAvatar: {(pool.defaultHumanoidAvatar != null ? pool.defaultHumanoidAvatar.name : "NULL")}");
                    Debug.Log($"[NPC_Audit] Pool poolConfigs count: {pool.poolConfigs.Count}");
                    for (int i = 0; i < pool.poolConfigs.Count; i++)
                    {
                        var cfg = pool.poolConfigs[i];
                        Debug.Log($"[NPC_Audit] PoolConfig[{i}]: Type={cfg.npcType}, Prefab={(cfg.prefab != null ? cfg.prefab.name : "NULL")}, InitialPoolSize={cfg.initialPoolSize}");
                    }

                    // Test instantiate 1 NPC from pool config
                    if (pool.poolConfigs.Count > 0 && pool.poolConfigs[0].prefab != null)
                    {
                        var samplePrefab = pool.poolConfigs[0].prefab;
                        Debug.Log($"[NPC_Audit] Testing Sample Prefab: '{samplePrefab.name}'");
                        var anim = samplePrefab.GetComponentInChildren<Animator>();
                        Debug.Log($"[NPC_Audit] Sample Prefab Animator: {(anim != null ? anim.name : "NULL")}, avatar={(anim != null && anim.avatar != null ? anim.avatar.name : "NULL")}, controller={(anim != null && anim.runtimeAnimatorController != null ? anim.runtimeAnimatorController.name : "NULL")}");
                        var renderers = samplePrefab.GetComponentsInChildren<SkinnedMeshRenderer>();
                        Debug.Log($"[NPC_Audit] Sample Prefab SkinnedMeshRenderers: {renderers.Length}");
                        foreach (var smr in renderers)
                        {
                            Debug.Log($"[NPC_Audit]   SMR: {smr.name}, sharedMesh={(smr.sharedMesh != null ? smr.sharedMesh.name : "NULL")}, materials={smr.sharedMaterials.Length}");
                        }
                    }
                }
            }

            // 5. Check for any static CyberNPCController instances in the scene
            var controllers = Object.FindObjectsByType<CyberNPCController>(FindObjectsInactive.Include);
            Debug.Log($"[NPC_Audit] Total CyberNPCController instances in scene: {controllers.Length}");
            for (int i = 0; i < Mathf.Min(controllers.Length, 10); i++)
            {
                var c = controllers[i];
                Debug.Log($"[NPC_Audit] NPC[{i}]: name='{c.name}', active={c.gameObject.activeInHierarchy}, lifeState={c.LifeState}, anim={(c.animator != null ? c.animator.name : "NULL")}, agent={(c.agent != null ? (c.agent.isOnNavMesh ? "OnNavMesh" : "NOT on NavMesh") : "NULL agent")}");
            }
        }

        public static void FixAndSetupScene()
        {
            Debug.Log("[NPC_Fix] Starting automated fix and setup for CyberCity NPCs...");

            // 1. Build Controller
            NPCAnimatorControllerBuilder.BuildController();

            // 2. Create Default Animation Profiles
            NPCAnimationProfileBuilder.CreateDefaultProfiles();

            // 3. Setup NPC System in Demo_City
            CyberNPCSpawnerSetupWindow.SetupNPCSystemInCyberCity();

            // 4. Save Scene and Assets
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[NPC_Fix] CyberCity NPC system successfully built, configured, and saved!");
        }
    }
}
#endif

// Trigger recompilation 2026-10-04
