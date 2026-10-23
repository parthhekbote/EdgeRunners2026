#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using PolygonCyberCity.NPC.Spawner;

namespace PolygonCyberCity.NPC.Editor
{
    public static class CyberNPCSpawnerSetupWindow
    {
        private const string CyberCityScenePath = "Assets/PolygonCyberCity/Scenes/Demo_City.unity";
        private const string CharacterPrefabsFolder = "Assets/PolygonCyberCity/Prefabs/Characters";

        [MenuItem("Tools/CyberCity/Setup AAA NPC Spawner with Behavior Trees")]
        public static void SetupNPCSystemInCyberCity()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Cannot Setup in Play Mode", "Please exit Play Mode before running the NPC setup tool.", "OK");
                return;
            }

            var activeScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (activeScene.path != CyberCityScenePath)
            {
                var openedScene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(CyberCityScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
                if (!openedScene.IsValid())
                {
                    Debug.LogError($"[CyberNPC] Failed to open scene at {CyberCityScenePath}");
                    return;
                }
            }

            // Find or create CyberNPCManager
            GameObject managerGo = GameObject.Find("CyberNPCManager");
            if (managerGo == null)
            {
                managerGo = new GameObject("CyberNPCManager");
                Undo.RegisterCreatedObjectUndo(managerGo, "Create CyberNPCManager");
            }

            // Spawner
            var spawner = managerGo.GetComponent<CyberNPCSpawner>();
            if (spawner == null)
            {
                spawner = Undo.AddComponent<CyberNPCSpawner>(managerGo);
            }

            // Pool
            var pool = managerGo.GetComponent<CyberNPCPool>();
            if (pool == null)
            {
                pool = Undo.AddComponent<CyberNPCPool>(managerGo);
            }

            var defaultAnimCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/PolygonCyberCity/Animations/AC_CyberNPC.controller")
                               ?? AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/SyntyCombined/AC_SyntyCombined.controller");
            if (defaultAnimCtrl != null)
            {
                pool.defaultAnimatorController = defaultAnimCtrl;
            }

            var defaultAvatar = AssetDatabase.LoadAssetAtPath<Avatar>("Assets/PolygonCyberCity/Models/Characters.fbx");
            if (defaultAvatar != null)
            {
                pool.defaultHumanoidAvatar = defaultAvatar;
            }

            // Assign archetype animation profiles
            pool.civilianProfile = AssetDatabase.LoadAssetAtPath<PolygonCyberCity.NPC.Animation.NPCAnimationProfile>("Assets/PolygonCyberCity/Data/AnimProfiles/CivilianAnimProfile.asset");
            pool.richCitizenProfile = AssetDatabase.LoadAssetAtPath<PolygonCyberCity.NPC.Animation.NPCAnimationProfile>("Assets/PolygonCyberCity/Data/AnimProfiles/RichCitizenAnimProfile.asset");
            pool.helperBotProfile = AssetDatabase.LoadAssetAtPath<PolygonCyberCity.NPC.Animation.NPCAnimationProfile>("Assets/PolygonCyberCity/Data/AnimProfiles/HelperBotAnimProfile.asset");
            pool.mercenaryProfile = AssetDatabase.LoadAssetAtPath<PolygonCyberCity.NPC.Animation.NPCAnimationProfile>("Assets/PolygonCyberCity/Data/AnimProfiles/MercenaryAnimProfile.asset");
            pool.securityProfile = AssetDatabase.LoadAssetAtPath<PolygonCyberCity.NPC.Animation.NPCAnimationProfile>("Assets/PolygonCyberCity/Data/AnimProfiles/SecurityAnimProfile.asset");

            // Scanner HUD
            var scanner = managerGo.GetComponent<CyberNPCScannerHUD>();
            if (scanner == null)
            {
                scanner = Undo.AddComponent<CyberNPCScannerHUD>(managerGo);
            }

            // Sidewalk & Crosswalk Manager
            var sidewalkMgr = managerGo.GetComponent<CyberCitySidewalkManager>();
            if (sidewalkMgr == null)
            {
                sidewalkMgr = Undo.AddComponent<CyberCitySidewalkManager>(managerGo);
            }
            sidewalkMgr.BakeSidewalkCorridors();

            // Locate Player
            GameObject playerGo = GameObject.Find("PF_PolygonPlayer");
            if (playerGo == null)
            {
                playerGo = GameObject.FindWithTag("Player");
            }

            if (playerGo != null)
            {
                spawner.playerTransform = playerGo.transform;
            }

            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                spawner.playerCamera = mainCam;
                scanner.playerCamera = mainCam;
            }

            // Configure Pool Prefabs with diverse visual models for each archetype
            pool.poolConfigs.Clear();

            // Civilians & Street Citizens
            AddPrefabToPool(pool, NPCType.Civilian, "SM_Chr_Street_Male_01", 6);
            AddPrefabToPool(pool, NPCType.Civilian, "SM_Chr_Racer_Female_01", 6);
            AddPrefabToPool(pool, NPCType.Civilian, "SM_Chr_Cyborg_Female_01", 5);
            AddPrefabToPool(pool, NPCType.Civilian, "SM_Chr_Cyborg_Male_01", 5);
            AddPrefabToPool(pool, NPCType.Civilian, "SM_Chr_PleasureBot_Male_01", 4);
            AddPrefabToPool(pool, NPCType.Civilian, "SM_Chr_Doctor_01", 5);

            // Rich Citizens
            AddPrefabToPool(pool, NPCType.RichCitizen, "SM_Chr_Rich_Female_01", 6);
            AddPrefabToPool(pool, NPCType.RichCitizen, "SM_Chr_Rich_Male_01", 6);

            // Mercenaries & Armed Thugs
            AddPrefabToPool(pool, NPCType.StreetThug, "SM_Chr_Male_Soldier_01", 5);
            AddPrefabToPool(pool, NPCType.StreetThug, "SM_Chr_Female_Soldier_01", 5);
            AddPrefabToPool(pool, NPCType.Mercenary, "SM_Chr_Mercenary_Male_01", 4);
            AddPrefabToPool(pool, NPCType.Mercenary, "SM_Chr_Mercenary_Female_01", 4);
            AddPrefabToPool(pool, NPCType.Mercenary, "SM_Chr_Robot_Mercenary_01", 4);

            // Helper Bots & Droids
            AddPrefabToPool(pool, NPCType.HelperBot, "SM_Chr_Helper_Bot_01", 5);
            AddPrefabToPool(pool, NPCType.HelperBot, "SM_Chr_Robot_01", 5);
            AddPrefabToPool(pool, NPCType.HelperBot, "SM_Chr_Fancy_Bot_01", 4);
            AddPrefabToPool(pool, NPCType.HelperBot, "SM_Chr_Robot_Female_01", 4);
            AddPrefabToPool(pool, NPCType.HelperBot, "SM_Chr_Robot_Damaged_01", 3);

            // Ensure NavMesh Surface for walkable street & sidewalk navigation
            var navSurface = Object.FindAnyObjectByType<Unity.AI.Navigation.NavMeshSurface>();
            if (navSurface == null)
            {
                GameObject navGo = new GameObject("NavMesh_CyberCity_Surface");
                navSurface = navGo.AddComponent<Unity.AI.Navigation.NavMeshSurface>();
                navSurface.collectObjects = Unity.AI.Navigation.CollectObjects.All;
                navSurface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
                Undo.RegisterCreatedObjectUndo(navGo, "Create NavMeshSurface");
            }

            if (navSurface != null)
            {
                // Temporarily enable Read/Write on all MeshFilter meshes so NavMeshBuilder
                // can sample them. Without this, Synty collision meshes flood the console
                // with "does not allow read access" errors.
                var meshFilters = Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include);
                var meshImporters = new System.Collections.Generic.List<(ModelImporter importer, bool wasReadable)>();
                var processedPaths = new System.Collections.Generic.HashSet<string>();

                foreach (var mf in meshFilters)
                {
                    if (mf.sharedMesh == null) continue;
                    string assetPath = AssetDatabase.GetAssetPath(mf.sharedMesh);
                    if (string.IsNullOrEmpty(assetPath) || processedPaths.Contains(assetPath)) continue;
                    processedPaths.Add(assetPath);

                    var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
                    if (importer == null || importer.isReadable) continue;
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                    meshImporters.Add((importer, false));
                }

                try
                {
                    navSurface.BuildNavMesh();
                    Debug.Log("[CyberNPC] Baked NavMeshSurface for CyberCity street navigation!");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[CyberNPC] NavMesh bake encountered an issue: {e.Message}\nYou can bake manually via Window > AI > Navigation > Bake.");
                }

                // Restore mesh import settings
                foreach (var (importer, _) in meshImporters)
                {
                    importer.isReadable = false;
                    importer.SaveAndReimport();
                }
            }

            EditorUtility.SetDirty(managerGo);
            EditorUtility.SetDirty(pool);
            EditorUtility.SetDirty(spawner);

            EditorSceneManager.SaveOpenScenes();

            Debug.Log($"[CyberNPC] Successfully setup AAA NPC Crowd Spawner & Behavior Tree System in {CyberCityScenePath} with {pool.poolConfigs.Count} pooled archetype configs!");
        }

        private static void AddPrefabToPool(CyberNPCPool pool, NPCType type, string prefabName, int count)
        {
            string path = $"{CharacterPrefabsFolder}/{prefabName}.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                pool.poolConfigs.Add(new CyberNPCPool.PoolConfig
                {
                    npcType = type,
                    prefab = prefab,
                    initialPoolSize = count
                });
            }
            else
            {
                Debug.LogWarning($"[CyberNPC] Prefab not found at {path}");
            }
        }
    }
}
#endif
