#if UNITY_EDITOR
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace SyntyCombined.Editor
{
    /// <summary>
    /// Editor setup utility for 1v1 Sword Combat Enemy AI and NPC Sword Locomotion.
    /// Features:
    ///   • Creates PF_SwordEnemy_CyberCity prefab equipped with CyberCity swords (SM_Wep_Sword_01 to 08)
    ///   • Spawns the 1v1 Duelist in SyntyCombatDemo scene facing the player
    ///   • "Convert Selected Character" tool: Equip any NPC/character with locomotion, sword combat, and AI
    /// </summary>
    [InitializeOnLoad]
    public static class SwordEnemySetup
    {
        private const string OutputPrefabPath  = "Assets/SyntyCombined/Prefabs/PF_SwordEnemy_CyberCity.prefab";
        private const string ControllerPath    = "Assets/SyntyCombined/AC_SyntyCombined.controller";
        private const string ClipDataPath      = "Assets/SyntyCombined/SyntyAnimClipData.asset";
        private const string CyberSwordPath    = "Assets/PolygonCyberCity/Prefabs/Weapons/SM_Wep_Sword_01.prefab";
        private const string CyberSheathPath   = "Assets/PolygonCyberCity/Prefabs/Weapons/SM_Wep_Sword_01_Sheath_01.prefab";
        private const string CyberCharPath     = "Assets/PolygonCyberCity/Prefabs/Characters/SM_Chr_Robot_Mercenary_01.prefab";
        private const string FallbackCharPath  = "Assets/Synty/AnimationSwordCombat/Meshes/PolygonSyntyCharacter.fbx";
        private const string DemoScenePath     = "Assets/Scenes/SyntyCombatDemo.unity";

        // ── VFX Prefab Paths ──────────────────────────────────────────────────────
        private const string VfxSparksPath      = "Assets/PolygonCyberCity/Prefabs/FX/FX_Sparks_01.prefab";
        private const string VfxSmokeDarkPath   = "Assets/PolygonMech/Prefabs/FX/FX_Smoke_Dark_01.prefab";
        private const string VfxFootstepPath    = "Assets/PolygonMech/Prefabs/FX/FX_Footstep_01.prefab";
        private const string VfxMechSparksPath  = "Assets/PolygonMech/Prefabs/FX/FX_Sparks_01.prefab";

        static SwordEnemySetup()
        {
            EditorApplication.delayCall += AutoRun;
        }

        private static void AutoRun()
        {
            if (!SessionState.GetBool("SwordEnemyAutoBuilt_v1", false))
            {
                SessionState.SetBool("SwordEnemyAutoBuilt_v1", true);
                Setup1v1Enemy();
            }
        }

        [MenuItem("Tools/Synty/Setup 1v1 Sword Combat Enemy")]
        public static void Setup1v1Enemy()
        {
            var prefab = CreateOrUpdateEnemyPrefab();
            if (prefab != null)
            {
                SpawnEnemyInScene(prefab);
            }
        }

        [MenuItem("Tools/Synty/Convert Selected Character to Sword Combat NPC")]
        public static void ConvertSelected()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("Selection Required", "Please select a humanoid character GameObject in the Hierarchy or Project view.", "OK");
                return;
            }

            ConfigureGameObjectAsSwordEnemy(selected);
            EditorUtility.DisplayDialog("Success", $"Successfully configured '{selected.name}' with Sword Combat Locomotion and 1v1 Enemy AI!", "OK");
        }

        public static GameObject CreateOrUpdateEnemyPrefab()
        {
            string dir = Path.GetDirectoryName(OutputPrefabPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            // 1. Create root GameObject
            GameObject rootGO = new GameObject("PF_SwordEnemy_CyberCity");

            // 2. Instantiate character visual model
            GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CyberCharPath);
            if (modelPrefab == null)
            {
                modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FallbackCharPath);
            }

            GameObject modelInstance = null;
            if (modelPrefab != null)
            {
                modelInstance = Object.Instantiate(modelPrefab, rootGO.transform);
                modelInstance.name = "CharacterModel";
                modelInstance.transform.localPosition = Vector3.zero;
                modelInstance.transform.localRotation = Quaternion.identity;
                modelInstance.transform.localScale = Vector3.one;
            }

            // 3. Configure all components on root
            ConfigureGameObjectAsSwordEnemy(rootGO, modelInstance);

            // 4. Save as Prefab
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAssetAndConnect(rootGO, OutputPrefabPath, InteractionMode.AutomatedAction);
            Object.DestroyImmediate(rootGO);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SwordEnemySetup] Successfully created 1v1 Sword Enemy Prefab at: {OutputPrefabPath}");
            return savedPrefab;
        }

        public static void ConfigureGameObjectAsSwordEnemy(GameObject rootGO, GameObject modelInstance = null)
        {
            // CharacterController
            var cc = rootGO.GetComponent<CharacterController>();
            if (cc == null) cc = rootGO.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.minMoveDistance = 0.001f;
            cc.stepOffset = 0.3f;

            // Animator
            var anim = rootGO.GetComponentInChildren<Animator>();
            if (anim == null && modelInstance != null) anim = modelInstance.GetComponent<Animator>();
            if (anim == null) anim = rootGO.AddComponent<Animator>();

            var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (ctrl != null) anim.runtimeAnimatorController = ctrl;

            // WeaponSocketController (CyberCity Swords)
            var socketCtrl = rootGO.GetComponent<WeaponSocketController>();
            if (socketCtrl == null) socketCtrl = rootGO.AddComponent<WeaponSocketController>();

            socketCtrl.swordPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CyberSwordPath);
            socketCtrl.sheathPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CyberSheathPath);
            // Calibrated for CyberCity humanoid RightHand bone (no Prop_R)
            // X = blade forward, Y = up along handle, Z = side
            socketCtrl.handOffset   = new Vector3(0f,    0.1f,  0.01f);
            socketCtrl.handRotation = new Vector3(0f,  180f,   90f);
            socketCtrl.hipOffset    = new Vector3(0.18f, -0.05f, -0.08f);
            socketCtrl.hipRotation  = new Vector3(-10f, 15f, 110f);

            // SwordEnemyAI
            var ai = rootGO.GetComponent<SwordEnemyAI>();
            if (ai == null) ai = rootGO.AddComponent<SwordEnemyAI>();

            ai.animator = anim;
            ai.weaponSocket = socketCtrl;
            ai.clips = AssetDatabase.LoadAssetAtPath<SyntyAnimClipData>(ClipDataPath);
            ai.maxHealth = 200f;
            ai.currentHealth = 200f;
            ai.maxPosture = 100f;
            ai.idealOrbitDistance = 3.2f;
            ai.attackRange = 2.0f;
            ai.walkSpeed = 2.2f;
            ai.runSpeed = 4.8f;
            ai.strafeSpeed = 2.0f;
            ai.blockChance = 0.65f;
            ai.parryChance = 0.25f;
            ai.dodgeChance = 0.25f;
            ai.postureBreakDuration        = 3.2f;
            // Special Move Config
            ai.blinkSlashTeleportDistance   = 3.0f;
            ai.shoulderChargeDashDistance   = 6.0f;
            ai.shoulderChargeSpeed          = 14f;
            ai.cycloneRadius                = 2.2f;
            ai.cycloneHitCount              = 3;
            ai.grenadeExplosionRadius       = 3.5f;
            ai.cameraShakeMagnitude         = 0.25f;
            ai.cameraShakeDuration          = 0.35f;

            // VFX prefabs — wire from existing project FX libraries
            ai.vfxSwordHit     = AssetDatabase.LoadAssetAtPath<GameObject>(VfxSparksPath);
            ai.vfxBlockImpact  = AssetDatabase.LoadAssetAtPath<GameObject>(VfxSparksPath);
            ai.vfxBlinkSmoke   = AssetDatabase.LoadAssetAtPath<GameObject>(VfxSmokeDarkPath);
            ai.vfxChargeImpact = AssetDatabase.LoadAssetAtPath<GameObject>(VfxFootstepPath);
            ai.vfxOverdriveBurst = AssetDatabase.LoadAssetAtPath<GameObject>(VfxMechSparksPath);
            ai.vfxGrenadeBurst = AssetDatabase.LoadAssetAtPath<GameObject>(VfxSparksPath);
            ai.vfxDeath        = AssetDatabase.LoadAssetAtPath<GameObject>(VfxSmokeDarkPath);

            // EnemyDuelHUD (Using existing project Synty HUD)
            var hud = rootGO.GetComponent<EnemyDuelHUD>();
            if (hud == null) hud = rootGO.AddComponent<EnemyDuelHUD>();
            hud.enemyAI = ai;
            hud.hudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/InterfaceSciFiSoldierHUD/Prefabs/NPC_HealthBars_EnemyData/HUD_SciFi_WorldSpace_EnemyInfo.prefab");
            hud.displayMode = EnemyDuelHUD.HUDMode.WorldSpaceOverhead;
            hud.duelistName = "CYBORG MERCENARY // 01";
            hud.duelistTitle = "CYBERCITY ELITE BLADEMASTER";
            hud.headOffset = new Vector3(0f, 2.2f, 0f);
        }

        public static void SpawnEnemyInScene(GameObject enemyPrefab)
        {
            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.path != DemoScenePath)
            {
                if (File.Exists(DemoScenePath))
                {
                    EditorSceneManager.OpenScene(DemoScenePath);
                }
            }

            // Check if an enemy already exists in the scene
            var existingEnemy = GameObject.Find("PF_SwordEnemy_CyberCity") ?? GameObject.Find("SwordEnemy_1v1");
            if (existingEnemy != null)
            {
                Object.DestroyImmediate(existingEnemy);
            }

            // Spawn facing player at (0, 0, 4.5)
            GameObject spawned = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab);
            spawned.name = "SwordEnemy_1v1";
            spawned.transform.position = new Vector3(0f, 0f, 4.5f);
            spawned.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            // Connect player target
            var player = Object.FindAnyObjectByType<SyntySwordLocomotionController>();
            var ai = spawned.GetComponent<SwordEnemyAI>();
            if (player != null && ai != null)
            {
                ai.playerTarget = player.transform;
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

            Selection.activeGameObject = spawned;
            Debug.Log("[SwordEnemySetup] 1v1 Sword Combat Enemy spawned successfully in the arena facing the player!");
        }
    }
}
#endif
