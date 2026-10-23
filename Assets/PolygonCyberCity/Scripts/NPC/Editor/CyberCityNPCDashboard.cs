#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using PolygonCyberCity.NPC.Spawner;

namespace PolygonCyberCity.NPC.Editor
{
    /// <summary>
    /// Unified 1-Click Dashboard for Cyber City NPC Generation, Dynamic Spawner,
    /// Enemy Placement, and Population Management.
    /// </summary>
    public class CyberCityNPCDashboard : EditorWindow
    {
        private const string CyberCityScenePath = "Assets/PolygonCyberCity/Scenes/Demo_City.unity";

        [MenuItem("Tools/Cyber City/NPC Population Dashboard", false, 0)]
        public static void ShowWindow()
        {
            var window = GetWindow<CyberCityNPCDashboard>("Cyber City NPCs");
            window.minSize = new Vector2(420, 520);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            
            // Header Banner
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter
            };
            EditorGUILayout.LabelField(new GUIContent("🏙️ CYBER CITY AAA NPC DASHBOARD"), headerStyle);
            EditorGUILayout.LabelField("GTA V / Vice City Inspired Pedestrians & Behavior Trees", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.Space(15);

            // Status Box
            DrawStatusBox();
            EditorGUILayout.Space(15);

            // Actions Box
            EditorGUILayout.LabelField("POPULATION ACTIONS", EditorStyles.boldLabel);

            GUI.backgroundColor = new Color(0.3f, 0.8f, 1f);
            if (GUILayout.Button("🚀 1. Setup AAA Crowd Spawner & Behavior Trees", GUILayout.Height(38)))
            {
                CyberNPCSpawnerSetupWindow.SetupNPCSystemInCyberCity();
                Repaint();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.HelpBox("Configures the zero-allocation object pool, bakes NavMesh, and initializes GTA-style AI behavior trees.", MessageType.None);

            EditorGUILayout.Space(8);

            GUI.backgroundColor = new Color(1f, 0.7f, 0.2f);
            if (GUILayout.Button("⚔️ 2. Setup InsaneGun Sword Showcase Scene", GUILayout.Height(38)))
            {
                InsaneGun.Editor.InsaneGunShowcaseSetup.SetupShowcase();
                Repaint();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.HelpBox("Creates a dedicated combat showcase scene with InsaneGun 7-hit sword combos, gun attacks, and combat dummies!", MessageType.None);

            EditorGUILayout.Space(15);

            // Utilities & Cleanup
            EditorGUILayout.LabelField("UTILITIES & CLEANUP", EditorStyles.boldLabel);

            GUILayout.BeginHorizontal();
            
            if (GUILayout.Button("🔍 Focus Player & Scene View", GUILayout.Height(30)))
            {
                FocusPlayer();
            }

            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("🧹 Clear All Scene NPCs", GUILayout.Height(30)))
            {
                ClearAllNPCs();
                Repaint();
            }
            GUI.backgroundColor = Color.white;

            GUILayout.EndHorizontal();

            EditorGUILayout.Space(20);
            EditorGUILayout.HelpBox("All NPCs feature obstacle wall rerouting, complete path verification, and multi-idle animation transitions.", MessageType.Info);
        }

        private void DrawStatusBox()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            var scene = EditorSceneManager.GetActiveScene();
            bool isDemoCity = scene.path == CyberCityScenePath;

            EditorGUILayout.LabelField($"Active Scene: {scene.name} {(isDemoCity ? "✅ (Demo_City)" : "⚠️")}", EditorStyles.boldLabel);

            var player = GameObject.Find("PF_PolygonPlayer") ?? GameObject.FindWithTag("Player");
            EditorGUILayout.LabelField($"Player Status: {(player != null ? $"Found at {player.transform.position}" : "Not Found")}");

            var manager = GameObject.Find("CyberNPCManager");
            EditorGUILayout.LabelField($"Dynamic Crowd Manager: {(manager != null ? "Active ✅" : "Not Active (Click Setup)")}");

            int controllerCount = Object.FindObjectsByType<CyberNPCController>(FindObjectsInactive.Include).Length;
            EditorGUILayout.LabelField($"NPC Controllers in Scene: {controllerCount}");

            EditorGUILayout.EndVertical();
        }

        private static void FocusPlayer()
        {
            var player = GameObject.Find("PF_PolygonPlayer") ?? GameObject.FindWithTag("Player");
            if (player != null)
            {
                Selection.activeGameObject = player;
                EditorGUIUtility.PingObject(player);
                if (SceneView.lastActiveSceneView != null)
                {
                    SceneView.lastActiveSceneView.FrameSelected();
                }
            }
            else
            {
                Debug.LogWarning("[CyberCityDashboard] Player object not found in current scene.");
            }
        }

        [MenuItem("Tools/Cyber City/3. Clean All Scene NPCs", false, 40)]
        public static void ClearAllNPCs()
        {
            var container1 = GameObject.Find("--- CYBER CITY NPCS ---");
            if (container1 != null) Undo.DestroyObjectImmediate(container1);

            var container2 = GameObject.Find("--- NPCs & ENEMIES ---");
            if (container2 != null) Undo.DestroyObjectImmediate(container2);

            var manager = GameObject.Find("CyberNPCManager");
            if (manager != null) Undo.DestroyObjectImmediate(manager);

            Debug.Log("[CyberCityDashboard] Cleaned up all NPCs and spawners from scene.");
        }
    }
}
#endif
