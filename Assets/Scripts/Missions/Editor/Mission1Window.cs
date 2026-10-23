#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using Cyberpunk.Missions;

namespace Cyberpunk.Missions.Editor
{
    public class Mission1Window : EditorWindow
    {
        private const string DemoCityPath = "Assets/PolygonCyberCity/Scenes/Demo_City.unity";
        private const string ThugPrefabPath = "Assets/Prefabs/Mission1_Thug.prefab";

        private Vector2 _scrollPos;

        [MenuItem("Tools/Missions/Mission 1 Dashboard (Window) %#m", priority = 0)]
        [MenuItem("Window/Mission 1 - Street Trial", priority = 10)]
        public static void OpenWindow()
        {
            var window = GetWindow<Mission1Window>("Mission 1: Street Trial");
            window.minSize = new Vector2(420, 520);
            window.Show();
        }

        [MenuItem("Tools/Missions/🎬 Play Ido Murder Cutscene (Bridge)", priority = 20)]
        public static void PlayIdoMurderCutsceneMenu()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Play Mode Required", "Please enter Play Mode in Demo_City to play the Ido Murder Cutscene.", "OK");
                return;
            }
            IdoBridgeExecutionDirector.EnsureInCity().TriggerExecutionCutscene(null, force: true);
        }

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            // Header Banner
            EditorGUILayout.Space(8);
            var headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter
            };
            GUI.color = new Color(0f, 0.95f, 1f);
            EditorGUILayout.LabelField("⚡ MISSION 1: STREET TRIAL ⚡", headerStyle);
            GUI.color = Color.white;

            EditorGUILayout.LabelField("Cyberpunk 2077 × Alita Combat Awakening Trial", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.Space(10);

            // Status Section
            DrawStatusSection();

            EditorGUILayout.Space(10);

            // Scene Management
            DrawSceneActionsSection();

            EditorGUILayout.Space(10);

            // Mission Configuration
            DrawMissionSetupSection();

            EditorGUILayout.Space(10);

            // Runtime Controls (Play Mode)
            if (Application.isPlaying)
            {
                DrawRuntimeSection();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawStatusSection()
        {
            EditorGUILayout.LabelField("SYSTEM STATUS", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            string currentScene = EditorSceneManager.GetActiveScene().path;
            bool isDemoCity = currentScene.Equals(DemoCityPath, System.StringComparison.OrdinalIgnoreCase);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Active Scene:", GUILayout.Width(120));
            GUI.color = isDemoCity ? Color.green : Color.yellow;
            EditorGUILayout.LabelField(isDemoCity ? "Demo_City (Mission 1 Scene)" : Path.GetFileName(currentScene));
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();

            var mgr = Object.FindAnyObjectByType<Mission1_StreetTrialManager>();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Mission Manager:", GUILayout.Width(120));
            GUI.color = mgr != null ? Color.green : Color.red;
            EditorGUILayout.LabelField(mgr != null ? "Active in Scene" : "Not Found in Scene");
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();

            GameObject thugPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ThugPrefabPath);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Base Punk Prefab:", GUILayout.Width(130));
            GUI.color = thugPrefab != null ? Color.green : Color.red;
            EditorGUILayout.LabelField(thugPrefab != null ? "Ready" : "Missing Prefab");
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();

            // Cutscene Doctor Status
            var docPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/Doctor/Doctor.prefab");
            var docPortrait = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Characters/Doctor/Textures/T_Doctor_Ido_HoloPortrait.png");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Dr. Ido (Cutscene):", GUILayout.Width(130));
            GUI.color = (docPrefab != null && docPortrait != null) ? Color.green : Color.yellow;
            EditorGUILayout.LabelField((docPrefab != null && docPortrait != null) ? "Ready (Cutscene Model & Portrait)" : "Needs Setup");
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();

            // Diverse Squad Status
            var ninjaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Mission1_Enemy_Ninja.prefab");
            var enforcerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Mission1_Enemy_Enforcer.prefab");
            var mechPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Mission1_Enemy_ProtoMech.prefab");
            bool diverseSquadReady = ninjaPrefab != null && enforcerPrefab != null && mechPrefab != null;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Diverse Squad:", GUILayout.Width(130));
            GUI.color = diverseSquadReady ? Color.green : Color.yellow;
            EditorGUILayout.LabelField(diverseSquadReady ? "Ready (Ninja, Enforcer, Proto-Mech)" : "Base Only");
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawSceneActionsSection()
        {
            EditorGUILayout.LabelField("SCENE & TRIAL ACTIONS", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (GUILayout.Button("🌆 Open Demo_City (Mission 1 Scene)", GUILayout.Height(32)))
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(DemoCityPath);
                }
            }

            EditorGUILayout.Space(4);

            if (GUILayout.Button("🛠️ Complete 1-Click Setup for Demo_City", GUILayout.Height(28)))
            {
                Mission1SceneSetup.SetupDemoCityMission1();
            }

            if (GUILayout.Button("⚙️ Setup Mission 1 in Active Scene", GUILayout.Height(26)))
            {
                Mission1SceneSetup.SetupMission1InActiveScene();
            }

            if (GUILayout.Button("🎭 Generate / Sync Cutscene Dr. Ido Model", GUILayout.Height(26)))
            {
                Synty.SidekickCharacters.Editor.DoctorCreator.CreateDoctor();
            }

            if (GUILayout.Button("⚔️ Rebuild Diverse Combat Squad Prefabs", GUILayout.Height(26)))
            {
                Mission1SceneSetup.BuildThugPrefab();
            }

            EditorGUILayout.Space(6);

            GUI.backgroundColor = new Color(0.2f, 0.85f, 0.4f);
            if (GUILayout.Button(Application.isPlaying ? "⏹️ Stop Play Mode" : "▶️ Play Mission 1 (Test Trial)", GUILayout.Height(34)))
            {
                if (!Application.isPlaying)
                {
                    string currentScene = EditorSceneManager.GetActiveScene().path;
                    if (!currentScene.Equals(DemoCityPath, System.StringComparison.OrdinalIgnoreCase))
                    {
                        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                        {
                            EditorSceneManager.OpenScene(DemoCityPath);
                        }
                    }
                    EditorApplication.isPlaying = true;
                }
                else
                {
                    EditorApplication.isPlaying = false;
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndVertical();
        }

        private void DrawMissionSetupSection()
        {
            EditorGUILayout.LabelField("MISSION CONFIGURATION & ASSETS", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var mgr = Object.FindAnyObjectByType<Mission1_StreetTrialManager>();
            if (mgr != null)
            {
                EditorGUI.BeginChangeCheck();
                mgr.mission1SpawnPosition = EditorGUILayout.Vector3Field("Spawn Pos:", mgr.mission1SpawnPosition);
                mgr.enemyPrefab = (GameObject)EditorGUILayout.ObjectField("Enemy Prefab:", mgr.enemyPrefab, typeof(GameObject), false);

                if (EditorGUI.EndChangeCheck())
                {
                    EditorUtility.SetDirty(mgr);
                }

                if (GUILayout.Button("Select Mission1 Manager in Hierarchy"))
                {
                    Selection.activeGameObject = mgr.gameObject;
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Mission1_StreetTrialManager is not present in the active scene. Click 'Setup Mission 1 in Active Scene' to generate it.", MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawRuntimeSection()
        {
            EditorGUILayout.LabelField("⚡ LIVE COMBAT DEBUG CONTROLS", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var mgr = Mission1_StreetTrialManager.Instance;
            if (mgr != null)
            {
                EditorGUILayout.LabelField($"Trial State: {mgr.state}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Razor Punks Eliminated: {mgr.killCount} / {Mission1_StreetTrialManager.RequiredKills}");

                EditorGUILayout.Space(4);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("📞 Trigger Dr. Ido Call"))
                {
                    if (mgr.uiManager != null)
                    {
                        mgr.uiManager.ShowPopup(
                            "Dr. Ido",
                            "ALITA, stay on your guard! Rogue Razor Punks have seized the upper skybridge.",
                            "[ ACKNOWLEDGE ]",
                            "[Enter] Confirm",
                            null
                        );
                    }
                }

                if (GUILayout.Button("🏁 Complete Mission"))
                {
                    mgr.killCount = Mission1_StreetTrialManager.RequiredKills;
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("📍 Warp to Spawn"))
                {
                    if (mgr.playerTransform != null)
                    {
                        var cc = mgr.playerTransform.GetComponent<CharacterController>();
                        if (cc != null) cc.enabled = false;
                        mgr.playerTransform.position = mgr.mission1SpawnPosition;
                        if (cc != null) cc.enabled = true;
                    }
                }

                if (GUILayout.Button("⚔️ Warp to Skybridge"))
                {
                    if (mgr.playerTransform != null)
                    {
                        var cc = mgr.playerTransform.GetComponent<CharacterController>();
                        if (cc != null) cc.enabled = false;
                        mgr.playerTransform.position = mgr.zoneCenterPosition;
                        if (cc != null) cc.enabled = true;
                    }
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(4);
                GUI.backgroundColor = new Color(1f, 0.35f, 0.2f);
                if (GUILayout.Button("🎬 Play Ido Murder Cutscene (Robot Boss Stabbing)", GUILayout.Height(30)))
                {
                    IdoBridgeExecutionDirector.EnsureInCity().TriggerExecutionCutscene(null, force: true);
                }
                GUI.backgroundColor = Color.white;
            }
            else
            {
                EditorGUILayout.HelpBox("Mission1_StreetTrialManager is not running.", MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
        }
    }
}
#endif
