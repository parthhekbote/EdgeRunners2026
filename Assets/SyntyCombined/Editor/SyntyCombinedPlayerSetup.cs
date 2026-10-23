#if UNITY_EDITOR
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace SyntyCombined.Editor
{
    // ─────────────────────────────────────────────────────────────────────────────
    // SyntyCombinedPlayerSetup.cs  (Editor-only)
    //
    // Automatically creates:
    //   1. The PF_SyntySwordPlayer prefab based strictly on Synty's User Guide:
    //      - PolygonSyntyCharacter (SwordCombat FBX with dedicated Prop_R bone)
    //      - AC_SyntyCombined (4-layer Mecanim controller)
    //      - SyntyAnimClipData
    //      - Wep_Sword_01
    //      - SyntySwordLocomotionController
    //   2. A complete playable demo scene: Assets/Scenes/SyntyCombatDemo.unity
    //      - Arena ground, lighting, camera
    //      - Player character
    //      - Interactive training dummies with hit reactions & health HUD
    //
    // Run via: Tools → SyntyCombined → Setup Player Prefab and Demo Scene
    // ─────────────────────────────────────────────────────────────────────────────

    public static class SyntyCombinedPlayerSetup
    {
        private const string PrefabOutputDir = "Assets/SyntyCombined/Prefabs";
        private const string PlayerPrefabPath = "Assets/SyntyCombined/Prefabs/PF_SyntySwordPlayer.prefab";
        private const string DemoScenePath   = "Assets/Scenes/SyntyCombatDemo.unity";

        private const string CharacterFbxPath = "Assets/Synty/AnimationSwordCombat/Meshes/PolygonSyntyCharacter.fbx";
        private const string ControllerPath   = "Assets/SyntyCombined/AC_SyntyCombined.controller";
        private const string ClipDataPath     = "Assets/SyntyCombined/SyntyAnimClipData.asset";
        private const string SwordPrefabPath  = "Assets/Synty/AnimationSwordCombat/Samples/Prefabs/Wep_Sword_01.prefab";

        [MenuItem("Tools/SyntyCombined/Setup Player Prefab and Demo Scene")]
        public static void SetupAll()
        {
            CreatePlayerPrefab();
            CreateDemoScene();
            UpdateBaseLocomotionPlayer();
            Debug.Log("[SyntyCombined] Setup completed successfully! Open 'Assets/Scenes/SyntyCombatDemo.unity' and hit Play.");
        }

        [MenuItem("Tools/SyntyCombined/Update BaseLocomotion PF_PolygonPlayer")]
        public static void UpdateBaseLocomotionPlayer()
        {
            string path = "Assets/Synty/AnimationBaseLocomotion/Samples/Prefabs/PF_PolygonPlayer.prefab";
            if (!File.Exists(path)) return;

            var root = PrefabUtility.LoadPrefabContents(path);
            if (root == null) return;

            // Remove any legacy DualKatana controller
            var oldCtrl = root.GetComponent("DualKatanaPlayerController");
            if (oldCtrl != null) Object.DestroyImmediate(oldCtrl);

            var existingCtrl = root.GetComponent<SyntySwordLocomotionController>();
            if (existingCtrl == null) existingCtrl = root.AddComponent<SyntySwordLocomotionController>();

            var model = root.transform.Find("PolygonSyntyCharacter");
            if (model != null)
            {
                existingCtrl.characterMesh = model;
                existingCtrl.animator = model.GetComponent<Animator>();
                if (existingCtrl.animator != null)
                {
                    var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
                    if (ctrl != null) existingCtrl.animator.runtimeAnimatorController = ctrl;
                }
            }
            var camTarget = root.transform.Find("CameraTarget");
            if (camTarget != null) existingCtrl.cameraTarget = camTarget;

            existingCtrl.clips = AssetDatabase.LoadAssetAtPath<SyntyAnimClipData>(ClipDataPath);
            existingCtrl.swordPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SwordPrefabPath);
            existingCtrl.locomotionLayerIndex = 0;
            existingCtrl.swordArmLayerIndex = 1;
            existingCtrl.swordHandLayerIndex = 2;
            existingCtrl.combatLayerIndex = 3;

            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.SaveAssets();
            Debug.Log($"[SyntyCombined] Successfully updated {path} with SyntySwordLocomotionController!");
        }

        [MenuItem("Tools/SyntyCombined/Create Player Prefab Only")]
        public static GameObject CreatePlayerPrefab()
        {
            if (!Directory.Exists(PrefabOutputDir))
                Directory.CreateDirectory(PrefabOutputDir);

            var charFbx = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterFbxPath);
            if (charFbx == null)
            {
                Debug.LogError($"[SyntyCombined] Character FBX not found at {CharacterFbxPath}!");
                return null;
            }

            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            var clipData   = AssetDatabase.LoadAssetAtPath<SyntyAnimClipData>(ClipDataPath);
            var swordPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SwordPrefabPath);

            // 1. Root GameObject
            var root = new GameObject("PF_SyntySwordPlayer");
            root.tag = "Player";

            // 2. CharacterController
            var cc = root.AddComponent<CharacterController>();
            cc.height          = 1.80f;
            cc.radius          = 0.28f;
            cc.center          = new Vector3(0f, 0.90f, 0f);
            cc.slopeLimit      = 45f;
            cc.stepOffset      = 0.25f;
            cc.skinWidth       = 0.02f;
            cc.minMoveDistance = 0.001f;

            // 3. Child model
            var model = (GameObject)PrefabUtility.InstantiatePrefab(charFbx);
            model.name = "PolygonSyntyCharacter";
            model.transform.SetParent(root.transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale    = Vector3.one;

            // 4. Animator setup
            var animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            // 5. Camera target
            var camTarget = new GameObject("CameraTarget");
            camTarget.transform.SetParent(root.transform);
            camTarget.transform.localPosition = new Vector3(0f, 1.45f, 0f);

            // 6. SyntySwordLocomotionController
            var ctrl = root.AddComponent<SyntySwordLocomotionController>();
            ctrl.characterMesh           = model.transform;
            ctrl.animator                = animator;
            ctrl.cameraTarget            = camTarget.transform;
            ctrl.clips                   = clipData;
            ctrl.swordPrefab             = swordPrefab;
            ctrl.locomotionLayerIndex    = 0;
            ctrl.swordArmLayerIndex      = 1;
            ctrl.swordHandLayerIndex     = 2;
            ctrl.combatLayerIndex        = 3;
            ctrl.showCombatHUD           = true;

            // 7. Save as prefab
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SyntyCombined] Player prefab created at {PlayerPrefabPath}");
            return prefab;
        }

        [MenuItem("Tools/SyntyCombined/Create Demo Scene Only")]
        public static void CreateDemoScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Lighting & Environment
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.90f);
            light.intensity = 1.2f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.6f, 0.7f, 0.85f);
            RenderSettings.ambientEquatorColor = new Color(0.4f, 0.45f, 0.5f);
            RenderSettings.ambientGroundColor = new Color(0.2f, 0.2f, 0.25f);

            // 2. Arena Floor
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Arena_Floor";
            floor.transform.position   = new Vector3(0f, -0.25f, 0f);
            floor.transform.localScale = new Vector3(40f, 0.5f, 40f);

            // Dark sci-fi arena floor material
            var floorRenderer = floor.GetComponent<Renderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.color = new Color(0.12f, 0.14f, 0.18f);
            mat.SetFloat("_Smoothness", 0.35f);
            floorRenderer.sharedMaterial = mat;

            // 3. Perimeter Pillars / Obstacles
            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90f * Mathf.Deg2Rad;
                var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pillar.name = $"Pillar_{i+1}";
                pillar.transform.position   = new Vector3(Mathf.Cos(angle) * 12f, 2f, Mathf.Sin(angle) * 12f);
                pillar.transform.localScale = new Vector3(1.2f, 2f, 1.2f);
                var pMat = new Material(mat);
                pMat.color = new Color(0.18f, 0.22f, 0.28f);
                pillar.GetComponent<Renderer>().sharedMaterial = pMat;
            }

            // 4. Player Instance
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerPrefab == null)
            {
                playerPrefab = CreatePlayerPrefab();
            }

            GameObject playerInstance = null;
            if (playerPrefab != null)
            {
                playerInstance = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                playerInstance.transform.position = Vector3.zero;
                playerInstance.transform.rotation = Quaternion.identity;
            }

            // 5. Main Camera
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane  = 1000f;
            cam.fieldOfView   = 60f;
            camGo.AddComponent<AudioListener>();

            if (playerInstance != null)
            {
                var ctrl = playerInstance.GetComponent<SyntySwordLocomotionController>();
                if (ctrl != null) ctrl.playerCamera = cam;
            }

            // 6. Training Dummies
            CreateTrainingDummy(new Vector3(0f, 0f, 5f),   180f, "Target Alpha (Center)");
            CreateTrainingDummy(new Vector3(-4f, 0f, 4f),  150f, "Target Beta (Left)");
            CreateTrainingDummy(new Vector3(4f, 0f, 4f),  -150f, "Target Gamma (Right)");

            // 7. Save Scene
            string sceneDir = Path.GetDirectoryName(DemoScenePath);
            if (!Directory.Exists(sceneDir)) Directory.CreateDirectory(sceneDir);

            EditorSceneManager.SaveScene(scene, DemoScenePath);
            Debug.Log($"[SyntyCombined] Demo scene created and saved to {DemoScenePath}");
        }

        private static void CreateTrainingDummy(Vector3 pos, float yRot, string name)
        {
            var charFbx    = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterFbxPath);
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);

            var dummyGo = new GameObject(name);
            dummyGo.transform.position = pos;
            dummyGo.transform.rotation = Quaternion.Euler(0f, yRot, 0f);

            var col = dummyGo.AddComponent<CapsuleCollider>();
            col.height = 1.8f;
            col.radius = 0.32f;
            col.center = new Vector3(0f, 0.9f, 0f);

            if (charFbx != null)
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(charFbx);
                model.name = "Model";
                model.transform.SetParent(dummyGo.transform);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;

                var anim = model.GetComponent<Animator>();
                if (anim == null) anim = model.AddComponent<Animator>();
                anim.runtimeAnimatorController = controller;
                anim.applyRootMotion = false;

                var dummy = dummyGo.AddComponent<SyntyTrainingDummy>();
                dummy.animator = anim;
                dummy.dummyName = name;
                dummy.combatLayerIndex = 3;
            }
            else
            {
                var dummy = dummyGo.AddComponent<SyntyTrainingDummy>();
                dummy.dummyName = name;
            }
        }
    }
}
#endif
