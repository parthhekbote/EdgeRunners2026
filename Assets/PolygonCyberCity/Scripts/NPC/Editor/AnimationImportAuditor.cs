#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PolygonCyberCity.NPC.Editor
{
    public static class AnimationImportAuditor
    {
        [MenuItem("CyberCity/NPC Animation/Audit Synty Import Settings", false, 12)]
        [MenuItem("Tools/Cyber City/NPC Animation/Audit Synty Import Settings", false, 12)]
        public static void AuditSyntyAnimations()
        {
            string searchPath = "Assets/Synty/AnimationSwordCombat/Animations";
            if (!Directory.Exists(searchPath))
            {
                Debug.LogWarning($"[AnimationImportAuditor] Directory not found: {searchPath}");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { searchPath });
            int checkedCount = 0;
            int warningCount = 0;

            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!assetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) continue;

                ModelImporter modelImporter = AssetImporter.GetAtPath(assetPath) as ModelImporter;
                if (modelImporter == null) continue;

                checkedCount++;

                if (modelImporter.animationType != ModelImporterAnimationType.Human)
                {
                    Debug.LogWarning($"[AnimationImportAuditor] Non-Humanoid animation detected at '{assetPath}' (Type: {modelImporter.animationType})", AssetDatabase.LoadAssetAtPath<Object>(assetPath));
                    warningCount++;
                }

                if (modelImporter.sourceAvatar == null && modelImporter.animationType == ModelImporterAnimationType.Human)
                {
                    Debug.LogWarning($"[AnimationImportAuditor] Humanoid animation missing Avatar reference at '{assetPath}'", AssetDatabase.LoadAssetAtPath<Object>(assetPath));
                    warningCount++;
                }
            }

            Debug.Log($"[AnimationImportAuditor] Audit complete: {checkedCount} FBX files inspected, {warningCount} warnings logged.");
        }
    }
}
#endif
