using UnityEditor;
using UnityEngine;

namespace Cyberpunk.Missions.EditorTools
{
    /// <summary>Menu shortcuts to preview the Ido assassination cutscene without replaying the story.</summary>
    public static class IdoAssassinationMenu
    {
        [MenuItem("Tools/Missions/🎬 Play Ido Assassination Cutscene", priority = 21)]
        public static void PlayCutscene()
        {
            if (!Application.isPlaying)
            {
                Debug.Log("[IdoAssassination] Starting Play Mode...");
                EditorApplication.isPlaying = true;
                return;
            }
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.IndexOf("Demo_City", System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                Cyberpunk.World.ClinicDoorTrigger.IsInTransit = true;
                UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoadedForCutscene;
                UnityEngine.SceneManagement.SceneManager.LoadScene("Demo_City");
                return;
            }
            IdoAssassinationDirector.DebugPlayNow();
        }

        private static void OnSceneLoadedForCutscene(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (scene.name.IndexOf("Demo_City", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoadedForCutscene;
                EditorApplication.delayCall += () =>
                {
                    IdoAssassinationDirector.DebugPlayNow();
                };
            }
        }

        [MenuItem("Tools/Missions/🎬 Start Ido Bridge Approach (waypoint)", priority = 22)]
        public static void StartApproach()
        {
            if (!Application.isPlaying)
            {
                Debug.Log("[IdoAssassination] Starting Play Mode...");
                EditorApplication.isPlaying = true;
                return;
            }
            IdoAssassinationDirector.Begin();
        }
    }
}
