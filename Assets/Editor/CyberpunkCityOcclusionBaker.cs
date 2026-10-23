using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace CyberpunkDev.EditorTools
{
    /// <summary>
    /// Automated Occlusion Culling Baker & Static Flag Configurator for Demo_City.
    /// Sets up Umbra Occluder and Occludee static flags on modular city assets
    /// and triggers StaticOcclusionCulling.Compute().
    /// </summary>
    public static class CyberpunkCityOcclusionBaker
    {
        private const string CityScenePath = "Assets/PolygonCyberCity/Scenes/Demo_City.unity";

        [MenuItem("Tools/Cyberpunk/Bake Demo_City Occlusion Culling")]
        public static void BakeDemoCityOcclusion()
        {
            Debug.Log("[OcclusionBaker] Starting Demo_City Occlusion Setup and Baking...");

            var currentScene = EditorSceneManager.GetActiveScene();
            if (currentScene.path != CityScenePath)
            {
                Debug.Log($"[OcclusionBaker] Opening {CityScenePath}...");
                currentScene = EditorSceneManager.OpenScene(CityScenePath, OpenSceneMode.Single);
            }

            // 1. Tag Renderers with Occluder & Occludee Static Flags
            var allRenderers = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include);
            int occludersCount = 0;
            int occludeesCount = 0;

            Undo.RecordObjects(allRenderers, "Configure City Occlusion Flags");

            int smallPropLayer = LayerMask.NameToLayer("SmallProps");
            int medPropLayer = LayerMask.NameToLayer("MediumProps");
            int taggedLayersCount = 0;

            foreach (var r in allRenderers)
            {
                GameObject go = r.gameObject;
                string n = go.name.ToLower();

                // CRITICAL: NEVER mark player, weapons, characters, or enemies as static occludees!
                if (go.CompareTag("Player") || go.GetComponentInParent<CharacterController>() != null ||
                    go.GetComponentInParent<Animator>() != null ||
                    n.Contains("blade") || n.Contains("sword") || n.Contains("katana") ||
                    n.Contains("alita") || n.Contains("hero") || n.Contains("ido") || n.Contains("doctor") || n.Contains("enemy"))
                {
                    GameObjectUtility.SetStaticEditorFlags(go, 0);
                    r.allowOcclusionWhenDynamic = false;
                    continue;
                }

                // Assign distance-culled layers to props
                if (smallPropLayer >= 0 && (n.Contains("junk") || n.Contains("can_0") || n.Contains("bottle") ||
                    n.Contains("debris") || n.Contains("paper") || n.Contains("greebles") || n.Contains("trash")))
                {
                    go.layer = smallPropLayer;
                    taggedLayersCount++;
                }
                else if (medPropLayer >= 0 && (n.Contains("bench") || n.Contains("barrier") || n.Contains("planter") ||
                    n.Contains("chair") || n.Contains("table") || n.Contains("vending") || n.Contains("sign_small")))
                {
                    go.layer = medPropLayer;
                    taggedLayersCount++;
                }

                // Major structural pieces (Buildings, Walls, Roads, Foundations) act as occluders and occludees
                bool isOccluder = go.name.StartsWith("SM_Bld_") || go.name.StartsWith("SM_Env_Road") ||
                                  go.name.StartsWith("SM_Env_Pavement") || go.name.Contains("Wall") ||
                                  go.name.Contains("Foundation") || go.name.Contains("Pillar_Large") ||
                                  go.name.Contains("Building");

                StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(go);

                if (isOccluder)
                {
                    flags |= StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic;
                    occludersCount++;
                }
                else
                {
                    // Props, furniture, street items are occludees (hidden when behind buildings)
                    flags &= ~StaticEditorFlags.OccluderStatic;
                    flags |= StaticEditorFlags.OccludeeStatic;
                    occludeesCount++;
                }

                GameObjectUtility.SetStaticEditorFlags(go, flags);
            }

            Debug.Log($"[OcclusionBaker] Configured {occludersCount} Occluders, {occludeesCount} Occludees, and tagged {taggedLayersCount} props with distance culling layers.");

            // 2. Setup OcclusionArea over the city bounds
            var occlusionArea = UnityEngine.Object.FindAnyObjectByType<OcclusionArea>();
            if (occlusionArea == null)
            {
                var areaObj = new GameObject("City_OcclusionArea");
                occlusionArea = areaObj.AddComponent<OcclusionArea>();
            }

            occlusionArea.center = new Vector3(0f, 15f, 20f);
            occlusionArea.size = new Vector3(450f, 100f, 450f);

            // 3. Ensure CyberpunkCityCullingManager is baked into the scene
            var cullingManager = UnityEngine.Object.FindAnyObjectByType<Cyberpunk.World.CyberpunkCityCullingManager>();
            if (cullingManager == null)
            {
                var cullingObj = new GameObject("[CyberpunkCity_CullingManager]");
                cullingObj.AddComponent<Cyberpunk.World.CyberpunkCityCullingManager>();
                Debug.Log("[OcclusionBaker] Added [CyberpunkCity_CullingManager] to Demo_City.");
            }

            // 4. Mark Scene Dirty & Save
            EditorSceneManager.MarkSceneDirty(currentScene);
            EditorSceneManager.SaveScene(currentScene);

            // 5. Compute Occlusion Culling Bake
            Debug.Log("[OcclusionBaker] Starting StaticOcclusionCulling.Compute()...");
            bool success = StaticOcclusionCulling.Compute();
            if (success)
            {
                Debug.Log("[OcclusionBaker] StaticOcclusionCulling.Compute() completed successfully!");
            }
            else
            {
                Debug.LogWarning("[OcclusionBaker] StaticOcclusionCulling.Compute() returned false or was cancelled.");
            }

            EditorSceneManager.SaveScene(currentScene);
            AssetDatabase.SaveAssets();
        }

        public static void BakeDemoCityOcclusionBatch()
        {
            BakeDemoCityOcclusion();
            EditorApplication.Exit(0);
        }
    }
}
