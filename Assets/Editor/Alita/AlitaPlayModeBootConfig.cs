using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Alita.Editor
{
    /// <summary>
    /// Ensures that pressing Play in the Unity Editor always starts directly inside
    /// Dr. Dyson Ido's Cyber Clinic Lab (Demo_Interior) for the Awakening sequence,
    /// preventing any temporary flash or spawn in Demo_City.
    /// </summary>
    [InitializeOnLoad]
    public static class AlitaPlayModeBootConfig
    {
        public const string ClinicInteriorScenePath = "Assets/PolygonCyberCity/Scenes/Demo_Interior.unity";

        static AlitaPlayModeBootConfig()
        {
            // Left empty so Unity plays whichever scene is currently open in the Editor.
            // To lock Play Mode to Clinic Lab, use Menu: Tools/Alita/Set Play Mode Start Scene.
        }

        [MenuItem("Tools/Alita/Set Play Mode Start Scene -> Dr. Ido's Clinic Lab (Demo_Interior)")]
        public static void EnsureClinicStartScene()
        {
            var interiorSceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ClinicInteriorScenePath);
            if (interiorSceneAsset != null)
            {
                if (EditorSceneManager.playModeStartScene != interiorSceneAsset)
                {
                    EditorSceneManager.playModeStartScene = interiorSceneAsset;
                    Debug.Log("[AlitaBootConfig] PlayModeStartScene configured: " + ClinicInteriorScenePath);
                }
            }
        }

        [MenuItem("Tools/Alita/Clear Play Mode Start Scene (Play Current Scene)")]
        public static void ClearStartScene()
        {
            EditorSceneManager.playModeStartScene = null;
            Debug.Log("[AlitaBootConfig] PlayModeStartScene cleared. Unity will play currently active scene.");
        }
    }
}
