#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using InsaneGun.Combat;
using Cyberpunk.Missions;

namespace Cyberpunk.Missions.Editor
{
    public static class Mission1SceneSetup
    {
        private const string ScenePath = "Assets/PolygonCyberCity/Scenes/Demo_City.unity";
        private const string ThugPrefabPath = "Assets/Prefabs/Mission1_Thug.prefab";
        private const string ControllerPath = "Assets/InsaneGun_Sword_Set/AC_InsaneGun_CombatMaster.controller";
        private const string AvatarPath = "Assets/PolygonCyberCity/Models/Characters.fbx";
        private const string BaseRobotModelPath = "Assets/PolygonCyberCity/Prefabs/Characters/SM_Chr_Robot_01.prefab";
        private const string RazorPunkMatPath = "Assets/Materials/M_RogueBot_RazorPunk.mat";

        private static readonly Vector3 DefaultStreetSpawnPos = new Vector3(19.00f, -17.98f, 49.50f);
        private static readonly Quaternion DefaultStreetSpawnRot = Quaternion.Euler(0f, 0f, 0f);

        [MenuItem("Tools/Missions/1. Build Mission1_Thug Prefab")]
        public static GameObject BuildThugPrefab()
        {
            Directory.CreateDirectory("Assets/Prefabs");

            GameObject baseAsset = AssetDatabase.LoadAssetAtPath<GameObject>(BaseRobotModelPath);
            if (baseAsset == null)
            {
                baseAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonCyberCity/Prefabs/Characters/SM_Chr_Mercenary_Male_01.prefab");
            }

            GameObject instance = baseAsset != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(baseAsset)
                : new GameObject("Mission1_Thug");

            instance.name = "Mission1_Thug";
            instance.tag = "Enemy";
            instance.layer = 9; // Enemy Layer

            // Scale 1.06x for menacing silhouette
            instance.transform.localScale = Vector3.one * 1.06f;

            foreach (Transform t in instance.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = 9;
            }

            // Material Override: Dark carbon body + emissive red visor/stripes
            Material punkMat = AssetDatabase.LoadAssetAtPath<Material>(RazorPunkMatPath);
            if (punkMat == null)
            {
                var baseMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/PolygonCyberCity/Materials/Alts/PolygonCyberCity_01_A.mat");
                punkMat = new Material(baseMat.shader);
                if (baseMat.HasProperty("_BaseMap")) punkMat.SetTexture("_BaseMap", baseMat.GetTexture("_BaseMap"));
                if (baseMat.HasProperty("_EmissionMap")) punkMat.SetTexture("_EmissionMap", baseMat.GetTexture("_EmissionMap"));
                Color darkBody = new Color(0.04f, 0.05f, 0.07f, 1.0f);
                punkMat.color = darkBody;
                if (punkMat.HasProperty("_BaseColor")) punkMat.SetColor("_BaseColor", darkBody);
                punkMat.EnableKeyword("_EMISSION");
                punkMat.SetColor("_EmissionColor", new Color(1.0f, 0.02f, 0.12f, 1.0f) * 2.5f);
                AssetDatabase.CreateAsset(punkMat, RazorPunkMatPath);
            }

            // Ensure SM_Chr_Robot_01 is active and wears punkMat
            var smrs = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var smr in smrs)
            {
                if (smr.name == "SM_Chr_Robot_01" || smr.name.Contains("Robot"))
                {
                    smr.gameObject.SetActive(true);
                    smr.sharedMaterial = punkMat;
                }
                else
                {
                    smr.gameObject.SetActive(false);
                }
            }

            // Character Controller (scaled)
            var cc = instance.GetComponent<CharacterController>();
            if (cc == null) cc = instance.AddComponent<CharacterController>();
            cc.center = new Vector3(0f, 0.93f * 1.06f, 0f);
            cc.height = 1.80f * 1.06f;
            cc.radius = 0.35f * 1.06f;
            cc.skinWidth = 0.08f;
            cc.stepOffset = 0.35f;

            // Animator
            var anim = instance.GetComponent<Animator>() ?? instance.GetComponentInChildren<Animator>();
            if (anim == null) anim = instance.AddComponent<Animator>();
            var rac = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (rac != null) anim.runtimeAnimatorController = rac;
            var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(AvatarPath);
            if (avatar != null) anim.avatar = avatar;
            anim.applyRootMotion = false;

            // InsaneGunSwordController - BALANCED COMBAT STATS: ~6 hits to kill (210 HP), 15 dmg, responsive pacing & flanking speed
            var ctrl = instance.GetComponent<InsaneGunSwordController>();
            if (ctrl == null) ctrl = instance.AddComponent<InsaneGunSwordController>();
            ctrl.controlMode = InsaneGunSwordController.ControlMode.AICirclingEnemy;
            ctrl.maxHealth = 210f;
            ctrl.currentHealth = 210f;
            ctrl.attackDamage = 15.0f;
            ctrl.heavyAttackDamage = 22.0f;
            ctrl.runSpeed = 4.6f;
            ctrl.walkSpeed = 2.8f;
            ctrl.minAttackInterval = 1.30f;
            ctrl.comboAcceptWindowStart = 0.65f;
            ctrl.autoReviveOnDeath = false;
            ctrl.enemyLayers = (1 << 8) | (1 << 3) | (1 << 0);

            // Red Rim PointLight on chest (Spine_02)
            Transform chestBone = anim.GetBoneTransform(HumanBodyBones.Chest) 
                               ?? anim.GetBoneTransform(HumanBodyBones.Spine)
                               ?? instance.transform;
            var existingLight = chestBone.Find("Red_Rim_PointLight");
            if (existingLight == null)
            {
                var lightGo = new GameObject("Red_Rim_PointLight");
                lightGo.transform.SetParent(chestBone, false);
                lightGo.transform.localPosition = new Vector3(0f, 0.1f, 0.15f);
                var pl = lightGo.AddComponent<Light>();
                pl.type = LightType.Point;
                pl.color = new Color(1.0f, 0.02f, 0.15f);
                pl.intensity = 2.2f;
                pl.range = 2.5f;
            }

            // Euphoria Ragdoll
            var ragdoll = instance.GetComponent<EuphoriaRagdollController>();
            if (ragdoll == null) ragdoll = instance.AddComponent<EuphoriaRagdollController>();
            ragdoll.autoRecover = false;
            ragdoll.SetupRagdollBones();

            // Katana Weapon
            Transform rHand = anim.GetBoneTransform(HumanBodyBones.RightHand);
            if (rHand == null) rHand = instance.transform.Find("hand_r") ?? instance.transform;

            string swordPrefabPath = "Assets/PolygonCyberCity/Prefabs/Weapons/SM_Wep_Sword_01.prefab";
            GameObject swordAsset = AssetDatabase.LoadAssetAtPath<GameObject>(swordPrefabPath);
            GameObject katana;

            if (swordAsset != null)
            {
                katana = (GameObject)PrefabUtility.InstantiatePrefab(swordAsset, rHand);
                katana.name = "Thug_Cyber_Katana";
                katana.transform.localPosition = new Vector3(0.02f, 0.05f, 0.01f);
                katana.transform.localRotation = Quaternion.Euler(0f, 90f, -90f);
            }
            else
            {
                katana = new GameObject("Thug_Energy_Katana");
                katana.transform.SetParent(rHand, false);
                katana.transform.localPosition = new Vector3(0.04f, 0.08f, 0.02f);
                katana.transform.localRotation = Quaternion.Euler(0f, 90f, -90f);

                var blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
                blade.name = "Blade";
                blade.transform.SetParent(katana.transform, false);
                blade.transform.localPosition = new Vector3(0f, 0.55f, 0f);
                blade.transform.localScale = new Vector3(0.025f, 1.1f, 0.05f);
                Object.DestroyImmediate(blade.GetComponent<Collider>());
            }

            // Blade tip for TrailRenderer
            Transform tip = katana.transform.Find("Blade_Tip");
            if (tip == null)
            {
                var tipGo = new GameObject("Blade_Tip");
                tipGo.transform.SetParent(katana.transform, false);
                tipGo.transform.localPosition = new Vector3(0f, 0.75f, 0f);
                tip = tipGo.transform;
            }

            var trailShader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                           ?? Shader.Find("Universal Render Pipeline/Lit")
                           ?? Shader.Find("Standard");
            var bladeMat = new Material(trailShader);
            Color glowColor = new Color(1.0f, 0.05f, 0.20f);
            bladeMat.color = glowColor;
            bladeMat.EnableKeyword("_EMISSION");
            bladeMat.SetColor("_EmissionColor", glowColor * 3.5f);

            var trail = tip.GetComponent<TrailRenderer>();
            if (trail == null) trail = tip.gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.22f;
            trail.startWidth = 0.70f;
            trail.endWidth = 0.02f;
            trail.sharedMaterial = bladeMat;
            trail.emitting = false;
            trail.minVertexDistance = 0.04f;
            ctrl.swordTrail = trail;

            // Mission Tracker
            var tracker = instance.GetComponent<Mission1_EnemyTracker>();
            if (tracker == null) tracker = instance.AddComponent<Mission1_EnemyTracker>();
            tracker.aggroDistance = 12.0f;
            tracker.displayName = "RAZOR PUNK";

            // Save as Prefab
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, ThugPrefabPath);
            Object.DestroyImmediate(instance);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Mission1] Successfully created {ThugPrefabPath} as RAZOR PUNK combat bot!");
            return prefab;
        }

        [MenuItem("Tools/Missions/2. Setup Mission 1 in Active Scene")]
        public static void SetupMission1InActiveScene()
        {
            var existingMgr = Object.FindAnyObjectByType<Mission1_StreetTrialManager>();
            if (existingMgr != null)
            {
                existingMgr.squadEnemyPrefabs = new GameObject[]
                {
                    AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Mission1_Enemy_Ninja.prefab"),
                    AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Mission1_Enemy_Enforcer.prefab"),
                    AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Mission1_Enemy_ProtoMech.prefab")
                };
                existingMgr.enableRivalInfighting = true;
                if (existingMgr.uiManager != null)
                {
                    existingMgr.uiManager.idoPortraitSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Characters/Doctor/Textures/T_Doctor_Ido_HoloPortrait.png");
                }
                var existingAudio = existingMgr.GetComponent<Mission1_AudioManager>();
                if (existingAudio != null)
                {
                    existingAudio.bgmClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/paulyudin-intense-intense-music-574004.mp3");
                }
                EditorSceneManager.MarkSceneDirty(existingMgr.gameObject.scene);
                EditorSceneManager.SaveScene(existingMgr.gameObject.scene);
                Debug.Log("[Mission1] Updated existing Mission1_StreetTrialManager in active scene with Diverse Squad & Cutscene Dr. Ido portrait & Combat Music!");
                Selection.activeGameObject = existingMgr.gameObject;
                return;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ThugPrefabPath);
            if (prefab == null)
            {
                prefab = BuildThugPrefab();
            }

            // Check or spawn Player
            var player = Object.FindAnyObjectByType<InsaneGunSwordController>();
            Vector3 spawnPos = player != null ? player.transform.position : DefaultStreetSpawnPos;
            Quaternion spawnRot = player != null ? player.transform.rotation : DefaultStreetSpawnRot;

            GameObject mgrObj = new GameObject("Mission1_StreetTrialManager");
            Undo.RegisterCreatedObjectUndo(mgrObj, "Create Mission1 Manager");

            var mgr = mgrObj.AddComponent<Mission1_StreetTrialManager>();
            mgr.mission1SpawnPosition = spawnPos;
            mgr.mission1SpawnRotation = spawnRot;
            mgr.enemyPrefab = prefab;
            mgr.squadEnemyPrefabs = new GameObject[]
            {
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Mission1_Enemy_Ninja.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Mission1_Enemy_Enforcer.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Mission1_Enemy_ProtoMech.prefab")
            };
            mgr.enableRivalInfighting = true;

            // Audio Manager
            var audioMgr = mgrObj.GetComponent<Mission1_AudioManager>();
            if (audioMgr == null) audioMgr = mgrObj.AddComponent<Mission1_AudioManager>();
            audioMgr.bgmClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/paulyudin-intense-intense-music-574004.mp3");
            if (audioMgr.bgmClip == null)
            {
                audioMgr.bgmClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/Nine Inch Nails - Init.mp3");
            }
            audioMgr.bgmFallbackClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/NIN_Init_Full_0_to_120.wav");

            // UI Manager
            GameObject uiObj = new GameObject("Mission1_UIManager");
            uiObj.transform.SetParent(mgrObj.transform);
            var uiMgr = uiObj.AddComponent<Mission1_UIManager>();
            mgr.uiManager = uiMgr;

            // Doctor Ido Portrait (Cutscene Doctor)
            uiMgr.idoPortraitSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Characters/Doctor/Textures/T_Doctor_Ido_HoloPortrait.png");
            if (uiMgr.idoPortraitSprite == null)
            {
                uiMgr.idoPortraitSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Characters/Doctor/Textures/T_Doctor_Ido_HoloPortrait.jpg");
            }
            if (uiMgr.idoPortraitSprite == null)
            {
                uiMgr.idoPortraitSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceSciFiSoldierHUD/Samples/Sprites/SPR_HUD_SciFi_Example_Comms_Portrait.png");
            }

            Selection.activeGameObject = mgrObj;
            Debug.Log("[Mission1] Successfully configured Mission1_StreetTrialManager in active scene!");
        }

        [MenuItem("Tools/Missions/3. Complete Mission 1 Setup for Demo_City")]
        public static void SetupDemoCityMission1()
        {
            if (!File.Exists(ScenePath))
            {
                Debug.LogError($"[Mission1] Scene not found at {ScenePath}");
                return;
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError("[Mission1] Failed to open Demo_City.unity");
                return;
            }

            // 1. Build Prefab
            GameObject prefab = BuildThugPrefab();

            // 2. Setup Player in Scene if missing
            EnsurePlayerInDemoCity();

            // 3. Setup Mission 1 Manager
            SetupMission1InActiveScene();

            // 4. Mark dirty and save scene
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[Mission1] Demo_City.unity successfully updated with MISSION 1 - STREET TRIAL!");
        }

        private static void EnsurePlayerInDemoCity()
        {
            var syntyPlayer = Object.FindAnyObjectByType<SyntyCombined.SyntySwordLocomotionController>();
            if (syntyPlayer != null)
            {
                Debug.Log($"[Mission1] Found existing playable character: '{syntyPlayer.name}' (SyntySwordLocomotionController)");
                return;
            }

            var player = Object.FindAnyObjectByType<InsaneGunSwordController>();
            if (player != null && player.controlMode == InsaneGunSwordController.ControlMode.PlayerControlled)
            {
                Debug.Log($"[Mission1] Found existing playable character: '{player.name}'");
                return;
            }

            // Instantiate player character
            string playerPrefabPath = "Assets/PolygonCyberCity/Prefabs/Characters/SM_Chr_Cyborg_Female_01.prefab";
            GameObject baseAsset = AssetDatabase.LoadAssetAtPath<GameObject>(playerPrefabPath);

            GameObject hero = baseAsset != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(baseAsset)
                : new GameObject("InsaneGun_Sword_Hero");

            hero.name = "InsaneGun_Sword_Hero";
            hero.tag = "Player";
            hero.layer = 8; // Layer 8 = Player
            hero.transform.position = DefaultStreetSpawnPos;
            hero.transform.rotation = DefaultStreetSpawnRot;

            var cc = hero.GetComponent<CharacterController>();
            if (cc == null) cc = hero.AddComponent<CharacterController>();
            cc.center = new Vector3(0f, 0.93f, 0f);
            cc.height = 1.80f;
            cc.radius = 0.35f;
            cc.skinWidth = 0.08f;
            cc.stepOffset = 0.35f;

            var anim = hero.GetComponent<Animator>() ?? hero.GetComponentInChildren<Animator>();
            if (anim == null) anim = hero.AddComponent<Animator>();
            var rac = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (rac != null) anim.runtimeAnimatorController = rac;
            var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(AvatarPath);
            if (avatar != null) anim.avatar = avatar;
            anim.applyRootMotion = false;

            var swordCtrl = hero.GetComponent<InsaneGunSwordController>();
            if (swordCtrl == null) swordCtrl = hero.AddComponent<InsaneGunSwordController>();
            swordCtrl.controlMode = InsaneGunSwordController.ControlMode.PlayerControlled;
            swordCtrl.maxHealth = 250f;
            swordCtrl.currentHealth = 250f;
            swordCtrl.attackDamage = 35f;
            swordCtrl.heavyAttackDamage = 75f;
            swordCtrl.enemyLayers = 1 << 9; // Layer 9 = Enemy
            swordCtrl.walkSpeed = 2.4f;
            swordCtrl.runSpeed = 6.2f;

            // Camera Setup
            var cam = Camera.main;
            if (cam == null)
            {
                GameObject camObj = new GameObject("MainCamera");
                camObj.tag = "MainCamera";
                cam = camObj.AddComponent<Camera>();
                camObj.AddComponent<AudioListener>();
            }

            var orbitCam = cam.GetComponent<SmoothOrbitCamera>();
            if (orbitCam == null) orbitCam = cam.gameObject.AddComponent<SmoothOrbitCamera>();
            orbitCam.target = hero.transform;

            Debug.Log("[Mission1] Ensured InsaneGun_Sword_Hero with SmoothOrbitCamera in Demo_City.");
        }
    }
}
#endif
