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

        [MenuItem("Tools/Missions/🎬 Test Arena Exit -> Ido Comms -> Sector 7 Pathway", priority = 23)]
        public static void TestArenaExitFlow()
        {
            if (!Application.isPlaying)
            {
                Debug.Log("[IdoAssassination] Starting Play Mode...");
                EditorApplication.isPlaying = true;
                return;
            }

            Mission4_VulcanShowdownManager.IsVulcanDefeated = true;
            World.PitArenaDoorTrigger.IsReturningFromPit = true;
            World.PitArenaDoorTrigger.HasVisitedPit = true;
            World.PitSkyLiftSystem.DeactivateMission3();

            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                var cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                player.transform.position = new Vector3(16.50f, 27.05f, -1.25f);
                player.transform.rotation = Quaternion.Euler(0f, 270f, 0f);
                if (cc != null) cc.enabled = true;
            }

            Mission4_VulcanShowdownManager.TriggerIdoReputationMessage();
        }

        [MenuItem("Tools/Missions/🎬 Test Server Room Unlock (Popup & Pathway)", priority = 24)]
        public static void TestServerRoomUnlock()
        {
            if (!Application.isPlaying)
            {
                Debug.Log("[IdoAssassination] Starting Play Mode...");
                EditorApplication.isPlaying = true;
                return;
            }

            // Unlock Mission 5
            World.IdoBridgeAmbushDirector.UnlockMission5();

            // Show aesthetic popup
            Alita.UI.ServerRoomUnlockNotificationUI.Show();

            // Start aesthetic dual-layer navigation tracer
            ServerRoomNavTracer.Begin();
        }
    }
}
