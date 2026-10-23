#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cyberpunk.Arena.Editor
{
    public static class PitArenaShortcutMenu
    {
        private const string PitScenePath = "Assets/Scenes/Cyberpunk_Indoor_Combat_Arena.unity";
        private const string PitSceneName = "Cyberpunk_Indoor_Combat_Arena";

        [MenuItem("Tools/The Pit Arena/🏟️ Open Pit Arena Scene (Cyberpunk_Indoor_Combat_Arena)", priority = 1)]
        public static void OpenArenaScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(PitScenePath);
                Debug.Log($"<color=#00FFAA><b>[Pit Arena]</b></color> Loaded scene: {PitScenePath}");
            }
        }

        [MenuItem("Tools/The Pit Arena/⚡ Play Pit Arena Directly (Play Current Scene)", priority = 2)]
        public static void PlayArenaDirectly()
        {
            // Clear any forced clinic lab boot scene so current scene plays
            Alita.Editor.AlitaPlayModeBootConfig.ClearStartScene();

            if (EditorSceneManager.GetActiveScene().path != PitScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(PitScenePath);
            }

            EditorApplication.isPlaying = true;
        }

        [MenuItem("Tools/The Pit Arena/🚪 Teleport Directly to The Pit (During Play Mode)", priority = 3)]
        public static void TeleportToPitPlayMode()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Play Mode Required",
                    "This action is for when you are playing in Demo_City.\n\nTo open the scene in Edit Mode, use 'Tools > The Pit Arena > 🏟️ Open Pit Arena Scene'.", "OK");
                return;
            }

            // Ensure arena pass is granted
            Cyberpunk.Inventory.CyberpunkInventory.EnsureInScene();
            if (Cyberpunk.Inventory.CyberpunkInventory.Instance != null && Cyberpunk.Inventory.CyberpunkInventory.Instance.CountOf("arena_pass") == 0)
            {
                Cyberpunk.Inventory.CyberpunkInventory.Instance.AddItem("arena_pass", 1);
            }

            World.PitArenaDoorTrigger.PendingSpawnPos = new Vector3(0f, 0.854f, -7.5f);
            World.PitArenaDoorTrigger.PendingSpawnYaw = 0f;
            World.PitArenaDoorTrigger.IsInTransit = true;
            World.PitArenaDoorTrigger.HasVisitedPit = true;

            SceneManager.LoadScene(PitSceneName);
            Debug.Log("<color=#00FFAA><b>[Pit Arena]</b></color> Teleporting Alita directly to The Pit Combat Ring!");
        }
    }
}
#endif
