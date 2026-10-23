using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cyberpunk.World.Editor
{
    public class IdoBridgeAmbushToolWindow : EditorWindow
    {
        private const string MarkerName = "Ido_Bridge_Marker";
        private const string CityScenePath = "Assets/PolygonCyberCity/Scenes/Demo_City.unity";

        [MenuItem("Tools/Cyberpunk/Ido Bridge Ambush Director", false, 10)]
        [MenuItem("Cyberpunk/Ido Bridge/Ambush Director Tool Window", false, 0)]
        public static void OpenWindow()
        {
            var win = GetWindow<IdoBridgeAmbushToolWindow>("Ido Ambush Tool");
            win.minSize = new Vector2(400f, 520f);
            win.Show();
        }

        [MenuItem("Tools/Cyberpunk/▶ Play Cutscene (Immediate Ambush)", false, 11)]
        [MenuItem("Cyberpunk/Ido Bridge/▶ Play Cutscene (Immediate Ambush)", false, 10)]
        public static void MenuPlayImmediate()
        {
            PlayCutscene(true);
        }

        [MenuItem("Tools/Cyberpunk/▶ Play Full Sequence (Distress Call)", false, 12)]
        [MenuItem("Cyberpunk/Ido Bridge/▶ Play Full Sequence (Distress Call)", false, 11)]
        public static void MenuPlayFullSequence()
        {
            PlayCutscene(false);
        }

        [MenuItem("Cyberpunk/Ido Bridge/Auto-Setup Bridge Marker in Demo_City", false, 20)]
        public static void MenuAutoSetupMarker()
        {
            AutoSetupBridgeMarkerInDemoCity();
        }

        [MenuItem("Cyberpunk/Ido Bridge/Create Marker From Selection", false, 21)]
        public static void CreateFromSelection()
        {
            var sel = Selection.activeGameObject;
            if (sel == null)
            {
                EditorUtility.DisplayDialog("Ido Bridge", "Select the bridge object in the Hierarchy first.", "OK");
                return;
            }

            var renderers = sel.GetComponentsInChildren<Renderer>();
            Bounds b;
            if (renderers.Length > 0)
            {
                b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
            }
            else
            {
                b = new Bounds(sel.transform.position, new Vector3(2f, 0.5f, 24f));
            }

            bool alongX = b.size.x > b.size.z;
            Vector3 axis = alongX ? Vector3.right : Vector3.forward;
            float length = alongX ? b.size.x : b.size.z;
            float width = alongX ? b.size.z : b.size.x;

            var existing = Object.FindAnyObjectByType<IdoBridgeMarker>();
            GameObject go = existing != null ? existing.gameObject : new GameObject(MarkerName);
            go.name = MarkerName;
            go.transform.position = new Vector3(b.center.x, b.max.y, b.center.z);
            go.transform.rotation = Quaternion.LookRotation(axis);

            var m = go.GetComponent<IdoBridgeMarker>() ?? go.AddComponent<IdoBridgeMarker>();
            m.halfLength = Mathf.Max(6f, length * 0.5f);
            m.halfWidth = Mathf.Max(1f, width * 0.5f);

            Selection.activeGameObject = go;
            EditorUtility.SetDirty(go);
            EditorSceneManager.MarkSceneDirty(go.scene);
            Debug.Log($"<color=#38EFD7>[IdoBridge]</color> Marker placed at {go.transform.position} (length {length:F1}m, width {width:F1}m). Save the scene.");
        }

        public static void AutoSetupBridgeMarkerInDemoCity()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Ido Bridge", "Please stop play mode before setting up scene markers.", "OK");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != "Demo_City")
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(CityScenePath);
                    scene = EditorSceneManager.GetActiveScene();
                }
                else return;
            }

            // Bridge 2 calculated 11-piece continuous bounds at y=-18, z=56.25:
            Vector3 center = new Vector3(1.25f, -17.97f, 56.25f);
            Vector3 axis = Vector3.right;
            float halfLength = 13.75f;
            float halfWidth = 1.4f;

            var existing = Object.FindAnyObjectByType<IdoBridgeMarker>();
            GameObject go = existing != null ? existing.gameObject : new GameObject(MarkerName);
            go.name = MarkerName;
            go.transform.position = center;
            go.transform.rotation = Quaternion.LookRotation(axis);

            var m = go.GetComponent<IdoBridgeMarker>() ?? go.AddComponent<IdoBridgeMarker>();
            m.halfLength = halfLength;
            m.halfWidth = halfWidth;

            Selection.activeGameObject = go;
            EditorUtility.SetDirty(go);
            EditorUtility.SetDirty(m);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log($"<color=#38EFD7>[IdoBridge]</color> Successfully configured and saved {MarkerName} at {center} in Demo_City!");
        }

        public static void PlayCutscene(bool immediateAmbush)
        {
            if (!EditorApplication.isPlaying)
            {
                var curScene = EditorSceneManager.GetActiveScene();
                if (curScene.name != "Demo_City")
                {
                    if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    {
                        EditorSceneManager.OpenScene(CityScenePath);
                    }
                    else return;
                }

                // Clear any forced playModeStartScene so Unity always plays the currently active scene
                EditorSceneManager.playModeStartScene = null;

                // Ensure marker exists in scene
                var marker = Object.FindAnyObjectByType<IdoBridgeMarker>();
                if (marker == null)
                {
                    AutoSetupBridgeMarkerInDemoCity();
                }

                // Set narrative story flags (Alita returned after defeating Vulcan in the Pit)
                PitArenaDoorTrigger.IsReturningFromPit = true;
                PitArenaDoorTrigger.HasVisitedPit = true;
                Cyberpunk.Missions.Mission4_VulcanShowdownManager.IsVulcanDefeated = true;

                // Queue auto-play for when Play Mode initializes
                EditorPrefs.SetBool("IdoBridge_AutoPlayCutscene", true);
                EditorPrefs.SetBool("IdoBridge_ImmediateAmbush", immediateAmbush);
                EditorApplication.isPlaying = true;
            }
            else
            {
                // Already in Play Mode:
                if (SceneManager.GetActiveScene().name != "Demo_City")
                {
                    // If in a different scene, switch to Demo_City and auto-play
                    EditorPrefs.SetBool("IdoBridge_AutoPlayCutscene", true);
                    EditorPrefs.SetBool("IdoBridge_ImmediateAmbush", immediateAmbush);
                    SceneManager.LoadScene("Demo_City");
                    return;
                }

                IdoBridgeAmbushDirector.ForceBegin(immediateAmbush);
            }
        }

        public static void TeleportAlitaToBridge()
        {
            var marker = Object.FindAnyObjectByType<IdoBridgeMarker>();
            if (marker == null)
            {
                EditorUtility.DisplayDialog("Teleport", "Bridge marker not found in scene. Setup the marker first.", "OK");
                return;
            }

            var player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                var hero = Object.FindAnyObjectByType<InsaneGun.Combat.InsaneGunSwordController>();
                if (hero != null) player = hero.gameObject;
            }

            if (player == null)
            {
                EditorUtility.DisplayDialog("Teleport", "Player GameObject not found.", "OK");
                return;
            }

            Vector3 targetPos = marker.transform.position + Vector3.up * 0.1f;
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.transform.position = targetPos;
            Physics.SyncTransforms();
            if (cc != null) cc.enabled = true;

            var cam = Camera.main ?? Object.FindAnyObjectByType<Camera>();
            if (cam != null)
            {
                var orbit = cam.GetComponent<InsaneGun.Combat.SmoothOrbitCamera>();
                if (orbit != null)
                {
                    orbit.target = player.transform;
                    orbit.SnapToTarget();
                }
            }

            Selection.activeGameObject = player;
            if (!EditorApplication.isPlaying)
            {
                EditorUtility.SetDirty(player);
                SceneView.FrameLastActiveSceneView();
            }

            Debug.Log($"<color=#38EFD7>[IdoBridge]</color> Teleported Alita to bridge at {targetPos}.");
        }

        // ── GUI ───────────────────────────────────────────────────────────────
        private void OnGUI()
        {
            var activeScene = SceneManager.GetActiveScene();
            var marker = Object.FindAnyObjectByType<IdoBridgeMarker>();

            GUILayout.Space(12f);
            GUILayout.Label("CYBERPUNK // IDO BRIDGE AMBUSH", EditorStyles.boldLabel);
            GUILayout.Label("Cutscene Testing & Director Control Deck", EditorStyles.miniLabel);
            GUILayout.Space(8f);

            // ── Status Box ────────────────────────────────────────────────────
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("SCENE STATUS", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Active Scene:", activeScene.name);
            EditorGUILayout.LabelField("Play Mode:", EditorApplication.isPlaying ? "PLAYING" : "EDIT MODE");
            EditorGUILayout.LabelField("Bridge Marker:", marker != null ? $"FOUND ({marker.transform.position})" : "MISSING");
            EditorGUILayout.LabelField("Vulcan Defeated:", Cyberpunk.Missions.Mission4_VulcanShowdownManager.IsVulcanDefeated ? "YES" : "NO");
            EditorGUILayout.EndVertical();

            GUILayout.Space(10f);

            // ── Main Action Buttons ───────────────────────────────────────────
            GUI.backgroundColor = new Color(0.2f, 0.95f, 0.85f);
            if (GUILayout.Button("▶ PLAY CUTSCENE (Immediate Ambush)", GUILayout.Height(40f)))
            {
                PlayCutscene(true);
            }
            GUI.backgroundColor = Color.white;

            GUILayout.Space(4f);

            GUI.backgroundColor = new Color(0.3f, 0.7f, 1.0f);
            if (GUILayout.Button("▶ PLAY FULL SEQUENCE (Distress Comms + Run)", GUILayout.Height(32f)))
            {
                PlayCutscene(false);
            }
            GUI.backgroundColor = Color.white;

            GUILayout.Space(6f);

            if (GUILayout.Button("⚡ Teleport Alita to Bridge", GUILayout.Height(28f)))
            {
                TeleportAlitaToBridge();
            }

            GUILayout.Space(12f);

            // ── Setup & Marker Utilities ──────────────────────────────────────
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("BRIDGE MARKER SETUP", EditorStyles.boldLabel);

            if (GUILayout.Button("Auto-Setup Bridge Marker in Demo_City", GUILayout.Height(26f)))
            {
                AutoSetupBridgeMarkerInDemoCity();
            }

            if (GUILayout.Button("Create Marker From Selection", GUILayout.Height(24f)))
            {
                CreateFromSelection();
            }

            if (marker != null)
            {
                if (GUILayout.Button("Ping / Select Bridge Marker", GUILayout.Height(22f)))
                {
                    Selection.activeGameObject = marker.gameObject;
                    EditorGUIUtility.PingObject(marker.gameObject);
                }
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(10f);

            // ── Runtime Controls & Hotkeys ────────────────────────────────────
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("KEYBOARD SHORTCUTS IN GAME", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("• F9", "Play Ambush Cutscene immediately");
            EditorGUILayout.LabelField("• Shift + F9", "Play Full Sequence (Comms + Run)");
            EditorGUILayout.LabelField("• F8", "Toggle on-screen test button in Game view");
            EditorGUILayout.EndVertical();

            GUILayout.Space(8f);

            if (EditorApplication.isPlaying)
            {
                GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
                if (GUILayout.Button("■ Stop Play Mode", GUILayout.Height(30f)))
                {
                    EditorApplication.isPlaying = false;
                }
                GUI.backgroundColor = Color.white;
            }
        }
    }
}
