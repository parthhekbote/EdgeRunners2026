using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cyberpunk.Editor
{
    /// <summary>
    /// Ensures that the Tron Chamber inside Dr. Ido's Clinic (Demo_Interior) has persistent
    /// BoxColliders on its floor and perimeter walls so Alita never falls through into the void.
    /// </summary>
    [InitializeOnLoad]
    public static class TronChamberColliderBaker
    {
        public const string InteriorScenePath = "Assets/PolygonCyberCity/Scenes/Demo_Interior.unity";

        static TronChamberColliderBaker()
        {
            EditorSceneManager.sceneOpened += OnSceneOpened;
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (scene.name == "Demo_Interior" && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                BakeColliders(false);
            }
        }

        [MenuItem("Tools/Alita/Bake Tron Chamber Colliders (Demo_Interior)")]
        public static void BakeCollidersManual()
        {
            BakeColliders(true);
        }

        public static void BakeColliders(bool saveScene)
        {
            var chamber = GameObject.Find("Clinic_Attached_Tron_Chamber");
            if (chamber == null) return;

            bool modified = false;
            // Standard Unity Plane mesh extends -5 to +5 on X and Z (10x10 units)
            modified |= EnsureBoxCollider(chamber.transform.Find("Tron_Floor_Obsidian"), 0.5f);
            modified |= EnsureBoxCollider(chamber.transform.Find("Tron_Wall_North_Back"), 0.5f);
            modified |= EnsureBoxCollider(chamber.transform.Find("Tron_Wall_West_Back"), 0.5f);
            modified |= EnsureBoxCollider(chamber.transform.Find("Tron_Wall_East_Back"), 0.5f);
            modified |= EnsureBoxCollider(chamber.transform.Find("Tron_Wall_South_Left_Back"), 0.5f);
            modified |= EnsureBoxCollider(chamber.transform.Find("Tron_Wall_South_Right_Back"), 0.5f);
            modified |= EnsureBoxCollider(chamber.transform.Find("Tron_Ceiling_Obsidian"), 0.5f);

            if (modified)
            {
                var scene = chamber.scene;
                EditorSceneManager.MarkSceneDirty(scene);
                if (saveScene)
                {
                    EditorSceneManager.SaveScene(scene);
                    Debug.Log("[TronChamberColliderBaker] Successfully baked Tron Chamber colliders into " + scene.path);
                }
            }
        }

        private static bool EnsureBoxCollider(Transform target, float thickness)
        {
            if (target == null) return false;
            var box = target.GetComponent<BoxCollider>();
            if (box == null)
            {
                box = Undo.AddComponent<BoxCollider>(target.gameObject);
                box.size = new Vector3(10f, thickness, 10f);
                box.center = new Vector3(0f, -thickness * 0.5f, 0f);
                return true;
            }
            return false;
        }
    }
}
