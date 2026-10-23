#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using InsaneGun.Combat;

namespace InsaneGun.Editor
{
    public static class InsaneGunShowcaseSetup
    {
        private const string ScenePath = "Assets/Scenes/InsaneGun_Sword_Showcase.unity";
        private const string ControllerPath = "Assets/InsaneGun_Sword_Set/AC_InsaneGun_CombatMaster.controller";
        private const string AnimFolderPath = "Assets/InsaneGun_Sword_Set/Animation/Humanoid";
        private const string CityMapScenePath = "Assets/PolygonCyberCity/Scenes/Demo_City.unity";

        // Central street plaza coordinates in Demo_City
        private static readonly Vector3 StreetSpawnPos = new Vector3(12.55f, -15.5f, 57.12f);
        private static readonly Quaternion StreetSpawnRot = Quaternion.Euler(0f, -130f, 0f);

        [MenuItem("Tools/InsaneGun Sword/Setup Showcase Scene & Playable Character")]
        public static void SetupShowcase()
        {
            // 1. Create/Ensure Master Animator Controller with all InsaneGun animations
            var animController = BuildInsaneGunAnimatorController();

            // 2. Load the Cyber City map as our showcase scene
            Directory.CreateDirectory("Assets/Scenes");
            UnityEngine.SceneManagement.Scene scene;

            if (File.Exists(CityMapScenePath))
            {
                Debug.Log($"[InsaneGun] Duplicating Cyber City map from {CityMapScenePath} to {ScenePath}...");
                File.Copy(CityMapScenePath, ScenePath, true);
                AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceUpdate);
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Debug.Log("[InsaneGun] Loaded full Cyber City map environment!");
            }
            else
            {
                Debug.LogWarning($"[InsaneGun] {CityMapScenePath} not found! Falling back to empty scene.");
                scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            }

            // 3. Clean up conflicting demo objects & old instances from previous runs
            CleanConflictingObjects();

            // 4. Setup street plaza safety collider so characters never fall through pavement gaps
            SetupStreetSafetyCollider();

            // 5. Spawn Playable Character on the city street
            GameObject hero = SpawnHeroCharacter(animController);

            // 6. Setup Follow Camera
            SetupFollowCamera(hero.transform);

            // 7. Spawn Combat Target Dummies on the street
            SpawnCombatDummies();

            // 8. Mark dirty and save Scene
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[InsaneGun] Successfully created Showcase Scene on the Cyber City map at {ScenePath}!");

            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("InsaneGun Sword Setup",
                    "InsaneGun Sword Showcase Scene is READY on the CYBER CITY MAP!\n\n" +
                    "• Map: Synty Polygon Cyber City (boulevard & neon skyscrapers)\n" +
                    "• Character: Cyborg Hero with InsaneGun Sword Set\n" +
                    "• Combos: 7-hit sword attacks, heavy combo chains, gun-kata, 4-way dodges\n" +
                    "• Enemies: 3 combat dummies placed on the city street\n\n" +
                    "Press PLAY to battle!", "Let's Battle!");
            }
        }

        private static void CleanConflictingObjects()
        {
            // Destroy any previous hero or dummy instances
            var oldHero = GameObject.Find("InsaneGun_Sword_Hero");
            if (oldHero != null) Object.DestroyImmediate(oldHero);

            for (int i = 1; i <= 10; i++)
            {
                var oldDummy = GameObject.Find($"Target_Dummy_0{i}");
                if (oldDummy != null) Object.DestroyImmediate(oldDummy);
            }

            // Destroy old flat arena floor or safety colliders from previous runs
            var oldFloor = GameObject.Find("Arena_Floor");
            if (oldFloor != null) Object.DestroyImmediate(oldFloor);

            var oldCol = GameObject.Find("Ground_Collider");
            if (oldCol != null) Object.DestroyImmediate(oldCol);

            var oldSafety = GameObject.Find("Street_Plaza_Safety_Collider");
            if (oldSafety != null) Object.DestroyImmediate(oldSafety);

            // Destroy old Synty locomotion demo player if present in Demo_City
            var oldSyntyPlayer = GameObject.Find("PF_PolygonPlayer");
            if (oldSyntyPlayer != null) Object.DestroyImmediate(oldSyntyPlayer);

            var oldSyntyPlayerClone = GameObject.Find("PF_PolygonPlayer(Clone)");
            if (oldSyntyPlayerClone != null) Object.DestroyImmediate(oldSyntyPlayerClone);

            // Clean up any extraneous demo overview cameras that would steal focus
            var strayCam = GameObject.Find("Camera");
            if (strayCam != null && strayCam.GetComponent<Camera>() != null)
            {
                strayCam.SetActive(false);
            }
        }

        private static void SetupStreetSafetyCollider()
        {
            // Solid invisible collision slab placed 20cm below the street surface.
            // This guarantees that CharacterControllers will NEVER fall through mesh seams
            // during combat dodges or fast sword attack movements.
            GameObject safety = new GameObject("Street_Plaza_Safety_Collider");
            safety.transform.position = new Vector3(StreetSpawnPos.x, -16.45f, StreetSpawnPos.z);
            var box = safety.AddComponent<BoxCollider>();
            box.size = new Vector3(80f, 0.2f, 80f);
            box.center = Vector3.zero;
        }

        [MenuItem("Tools/InsaneGun Sword/Rebuild Combat Animator Controller")]
        public static void RebuildControllerMenu()
        {
            BuildInsaneGunAnimatorController();
            Debug.Log("[InsaneGun] Combat Animator Controller rebuilt cleanly!");
        }

        public static AnimatorController BuildInsaneGunAnimatorController()
        {
            if (System.IO.File.Exists(ControllerPath))
            {
                AssetDatabase.DeleteAsset(ControllerPath);
                AssetDatabase.Refresh();
            }

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            // ── Parameters ──────────────────────────────────────────────────────────────
            controller.AddParameter("MoveSpeed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Attack",      AnimatorControllerParameterType.Trigger);
            controller.AddParameter("AttackIndex", AnimatorControllerParameterType.Int);
            controller.AddParameter("HeavyCombo",  AnimatorControllerParameterType.Trigger);
            controller.AddParameter("HeavyIndex",  AnimatorControllerParameterType.Int);
            controller.AddParameter("GunAttack",   AnimatorControllerParameterType.Trigger);
            controller.AddParameter("GunIndex",    AnimatorControllerParameterType.Int);
            controller.AddParameter("Dodge",       AnimatorControllerParameterType.Trigger);
            controller.AddParameter("DodgeDir",    AnimatorControllerParameterType.Int); // 0=front,1=back,2=left,3=right
            controller.AddParameter("Hit",         AnimatorControllerParameterType.Trigger);
            controller.AddParameter("HitDir",      AnimatorControllerParameterType.Int); // 0=front,1=back,2=left,3=right
            controller.AddParameter("HitType",     AnimatorControllerParameterType.Int); // 0=body_light,1=body_heavy,2-4=combo1-3_hit
            controller.AddParameter("DeadIndex",   AnimatorControllerParameterType.Int); // 1-5 random death
            controller.AddParameter("Die",         AnimatorControllerParameterType.Trigger);

            var rootSM = controller.layers[0].stateMachine;

            // ── 1. Locomotion BlendTree (idle → walk → run) ─────────────────────────────
            var locoState = rootSM.AddState("Locomotion", new Vector3(300, 100, 0));
            BlendTree blendTree = new BlendTree();
            blendTree.name = "Locomotion_Blend";
            blendTree.blendType = BlendTreeType.Simple1D;
            blendTree.blendParameter = "MoveSpeed";
            blendTree.minThreshold = 0f;
            blendTree.maxThreshold = 2f;

            var idleClip = LoadClip("N_idle") ?? LoadClip("idle");
            var walkClip = LoadClip("walk_strafe_front");
            var runClip  = LoadClip("move_run_front") ?? LoadClip("run_strafe_front");
            if (idleClip != null) blendTree.AddChild(idleClip, 0f);
            if (walkClip != null) blendTree.AddChild(walkClip, 1f);
            if (runClip  != null) blendTree.AddChild(runClip,  2f);

            AssetDatabase.AddObjectToAsset(blendTree, controller);
            locoState.motion = blendTree;
            rootSM.defaultState = locoState;

            // ── 2. Light Sword Attacks (atk_sword_01–07) ────────────────────────────────
            for (int i = 1; i <= 7; i++)
            {
                var clip = LoadClip($"atk_sword_0{i}");
                if (clip == null) continue;
                var state = rootSM.AddState($"Attack_0{i}", new Vector3(600, 40 * i, 0));
                state.motion = clip;
                state.tag = "Attack";
                var t = rootSM.AddAnyStateTransition(state);
                t.AddCondition(AnimatorConditionMode.If, 0, "Attack");
                t.AddCondition(AnimatorConditionMode.Equals, i, "AttackIndex");
                t.duration = 0.05f;
                t.hasExitTime = false;
                t.canTransitionToSelf = false;
                var ex = state.AddTransition(locoState);
                ex.hasExitTime = true;
                ex.exitTime = 0.85f;
                ex.duration = 0.15f;
            }

            // ── 3. Heavy Combos (combo_01–03) with sub-phase chains ─────────────────────
            int[] comboPhases = { 3, 6, 5 };
            for (int c = 1; c <= 3; c++)
            {
                int phases = comboPhases[c - 1];
                var phaseStates = new AnimatorState[phases];

                for (int p = 1; p <= phases; p++)
                {
                    var clip = LoadClip($"combo_0{c}_{p}") ?? LoadClip($"combo_0{c}");
                    if (clip == null) continue;

                    var state = rootSM.AddState($"Combo_0{c}_Phase{p}", new Vector3(850 + (c - 1) * 220, 60 * p, 0));
                    state.motion = clip;
                    state.tag = "Attack";
                    phaseStates[p - 1] = state;

                    if (p == 1)
                    {
                        var t = rootSM.AddAnyStateTransition(state);
                        t.AddCondition(AnimatorConditionMode.If, 0, "HeavyCombo");
                        t.AddCondition(AnimatorConditionMode.Equals, c, "HeavyIndex");
                        t.duration = 0.05f;
                        t.hasExitTime = false;
                        t.canTransitionToSelf = false;
                    }
                }

                // Phase N → Phase N+1 (auto-chain on exit time)
                for (int p = 0; p < phases - 1; p++)
                {
                    if (phaseStates[p] == null || phaseStates[p + 1] == null) continue;
                    var chain = phaseStates[p].AddTransition(phaseStates[p + 1]);
                    chain.hasExitTime = true; chain.exitTime = 0.85f; chain.duration = 0.08f;
                }

                // Last phase → Locomotion
                int last = phases - 1;
                while (last >= 0 && phaseStates[last] == null) last--;
                if (last >= 0)
                {
                    var ret = phaseStates[last].AddTransition(locoState);
                    ret.hasExitTime = true; ret.exitTime = 0.85f; ret.duration = 0.15f;
                }
            }

            // ── 4. Gun Attacks ───────────────────────────────────────────────────────────
            // 4a. Walking gun attack — checked first (MoveSpeed > 0.3)
            var walkGunClip = LoadClip("atk_gun_walking");
            if (walkGunClip != null)
            {
                var wg = rootSM.AddState("Gun_Walking", new Vector3(600, 650, 0));
                wg.motion = walkGunClip;
                wg.tag = "Attack";
                var wgt = rootSM.AddAnyStateTransition(wg);
                wgt.AddCondition(AnimatorConditionMode.If, 0, "GunAttack");
                wgt.AddCondition(AnimatorConditionMode.Greater, 0.3f, "MoveSpeed");
                wgt.duration = 0.08f; wgt.hasExitTime = false; wgt.canTransitionToSelf = false;
                var wgex = wg.AddTransition(locoState);
                wgex.hasExitTime = true; wgex.exitTime = 0.85f; wgex.duration = 0.12f;
            }

            // 4b. Stationary gun attacks (atk_gun_01–04)
            for (int g = 1; g <= 4; g++)
            {
                var clip = LoadClip($"atk_gun_0{g}");
                if (clip == null) continue;
                var state = rootSM.AddState($"Gun_Attack_0{g}", new Vector3(600, 430 + 50 * g, 0));
                state.motion = clip;
                state.tag = "Attack";
                var t = rootSM.AddAnyStateTransition(state);
                t.AddCondition(AnimatorConditionMode.If, 0, "GunAttack");
                t.AddCondition(AnimatorConditionMode.Equals, g, "GunIndex");
                t.duration = 0.08f; t.hasExitTime = false; t.canTransitionToSelf = false;
                var ex = state.AddTransition(locoState);
                ex.hasExitTime = true; ex.exitTime = 0.85f; ex.duration = 0.12f;
            }

            // ── 5. Directional Dodge (avoid_front/back/left/right) ──────────────────────
            // DodgeDir: 0=front, 1=back, 2=left, 3=right
            string[] dodgeClips  = { "avoid_front", "avoid_back",  "avoid_left",  "avoid_right"  };
            string[] rollFallbk  = { "roll_front",  "roll_back",   "roll_left",   "roll_right"   };
            string[] dodgeNames  = { "Dodge_Front", "Dodge_Back",  "Dodge_Left",  "Dodge_Right"  };
            for (int d = 0; d < 4; d++)
            {
                var clip = LoadClip(dodgeClips[d]) ?? LoadClip(rollFallbk[d]);
                if (clip == null) continue;
                var state = rootSM.AddState(dodgeNames[d], new Vector3(300, 330 + 90 * d, 0));
                state.motion = clip;
                state.tag = "Dodge";
                var t = rootSM.AddAnyStateTransition(state);
                t.AddCondition(AnimatorConditionMode.If, 0, "Dodge");
                t.AddCondition(AnimatorConditionMode.Equals, d, "DodgeDir");
                t.duration = 0.05f; t.hasExitTime = false; t.canTransitionToSelf = false;
                var ex = state.AddTransition(locoState);
                ex.hasExitTime = true; ex.exitTime = 0.85f; ex.duration = 0.10f;
            }

            // ── 6a. Light Body Hit Reactions — HitType=0 (directional) ─────────────────
            string[] bodyClips  = { "hit_body_front", "hit_body_back", "hit_body_left", "hit_body_right" };
            string[] bodyNames  = { "HitBody_Front",  "HitBody_Back",  "HitBody_Left",  "HitBody_Right"  };
            for (int d = 0; d < 4; d++)
            {
                var clip = LoadClip(bodyClips[d]);
                if (clip == null) continue;
                var state = rootSM.AddState(bodyNames[d], new Vector3(100, 330 + 90 * d, 0));
                state.motion = clip;
                state.tag = "Hit";
                var t = rootSM.AddAnyStateTransition(state);
                t.AddCondition(AnimatorConditionMode.If, 0, "Hit");
                t.AddCondition(AnimatorConditionMode.Equals, 0, "HitType");
                t.AddCondition(AnimatorConditionMode.Equals, d, "HitDir");
                t.duration = 0.03f; t.hasExitTime = false; t.canTransitionToSelf = true;
                var ex = state.AddTransition(locoState);
                ex.hasExitTime = true; ex.exitTime = 0.70f; ex.duration = 0.12f;
            }

            // ── 6a2. Light Head Hit Reactions — HitType=5 (alternating combo hits) ─────
            string[] headClips  = { "hit_head_front", "hit_head_back", "hit_head_left", "hit_head_right" };
            string[] headNames  = { "HitHead_Front",  "HitHead_Back",  "HitHead_Left",  "HitHead_Right"  };
            for (int d = 0; d < 4; d++)
            {
                var clip = LoadClip(headClips[d]) ?? LoadClip(bodyClips[d]);
                if (clip == null) continue;
                var state = rootSM.AddState(headNames[d], new Vector3(100, 700 + 90 * d, 0));
                state.motion = clip;
                state.tag = "Hit";
                var t = rootSM.AddAnyStateTransition(state);
                t.AddCondition(AnimatorConditionMode.If, 0, "Hit");
                t.AddCondition(AnimatorConditionMode.Equals, 5, "HitType");
                t.AddCondition(AnimatorConditionMode.Equals, d, "HitDir");
                t.duration = 0.03f; t.hasExitTime = false; t.canTransitionToSelf = true;
                var ex = state.AddTransition(locoState);
                ex.hasExitTime = true; ex.exitTime = 0.70f; ex.duration = 0.12f;
            }

            // ── 6b. Heavy Hit Reactions — HitType=1 (player stagger, directional) ───────
            string[] heavyClips = { "hit_heavy_front", "hit_heavy_back", "hit_heavy_left", "hit_heavy_right" };
            string[] heavyNames = { "HitHeavy_Front",  "HitHeavy_Back",  "HitHeavy_Left",  "HitHeavy_Right"  };
            for (int d = 0; d < 4; d++)
            {
                var clip = LoadClip(heavyClips[d]);
                if (clip == null) continue;
                var state = rootSM.AddState(heavyNames[d], new Vector3(-200, 330 + 90 * d, 0));
                state.motion = clip;
                state.tag = "Hit";
                var t = rootSM.AddAnyStateTransition(state);
                t.AddCondition(AnimatorConditionMode.If, 0, "Hit");
                t.AddCondition(AnimatorConditionMode.Equals, 1, "HitType");
                t.AddCondition(AnimatorConditionMode.Equals, d, "HitDir");
                t.duration = 0.03f; t.hasExitTime = false; t.canTransitionToSelf = true;
                var ex = state.AddTransition(locoState);
                ex.hasExitTime = true; ex.exitTime = 0.80f; ex.duration = 0.15f;
            }

            // ── 6c. Combo Finisher Hit Reactions — HitType=2/3/4 (enemy knockback) ──────
            // HitType = comboIndex + 1 → 2=combo_01_hit, 3=combo_02_hit, 4=combo_03_hit
            for (int c = 1; c <= 3; c++)
            {
                var clip = LoadClip($"combo_0{c}_hit");
                if (clip == null) continue;
                var state = rootSM.AddState($"ComboHit_0{c}", new Vector3(400, 330 + 110 * c, 0));
                state.motion = clip;
                state.tag = "Hit";
                var t = rootSM.AddAnyStateTransition(state);
                t.AddCondition(AnimatorConditionMode.If, 0, "Hit");
                t.AddCondition(AnimatorConditionMode.Equals, c + 1, "HitType");
                t.duration = 0.03f; t.hasExitTime = false; t.canTransitionToSelf = true;
                var ex = state.AddTransition(locoState);
                ex.hasExitTime = true; ex.exitTime = 0.85f; ex.duration = 0.15f;
            }

            // ── 7. Death (dead_01–05, random DeadIndex 1-5) ────────────────────────────
            for (int di = 1; di <= 5; di++)
            {
                var clip = LoadClip($"dead_0{di}");
                if (clip == null) continue;
                var state = rootSM.AddState($"Death_0{di}", new Vector3(-100, 680 + 90 * di, 0));
                state.motion = clip;
                state.tag = "Death";
                var t = rootSM.AddAnyStateTransition(state);
                t.AddCondition(AnimatorConditionMode.If, 0, "Die");
                t.AddCondition(AnimatorConditionMode.Equals, di, "DeadIndex");
                t.duration = 0.08f; t.hasExitTime = false; t.canTransitionToSelf = false;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static AnimationClip LoadClip(string fbxName)
        {
            string[] candidatePaths = new string[]
            {
                $"{AnimFolderPath}/{fbxName}.fbx",
                $"{AnimFolderPath}/{fbxName}_inplace.fbx"
            };

            foreach (var path in candidatePaths)
            {
                if (File.Exists(path))
                {
                    var assets = AssetDatabase.LoadAllAssetsAtPath(path);
                    if (assets != null)
                    {
                        foreach (var asset in assets)
                        {
                            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                            {
                                return clip;
                            }
                        }
                    }
                }
            }
            return null;
        }

        private static GameObject SpawnHeroCharacter(AnimatorController controller)
        {
            // Pick high quality cyborg female character prefab from PolygonCyberCity
            string prefabPath = "Assets/PolygonCyberCity/Prefabs/Characters/SM_Chr_Cyborg_Female_01.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            GameObject hero;

            if (prefab != null)
            {
                hero = Object.Instantiate(prefab, StreetSpawnPos, StreetSpawnRot);
                hero.name = "InsaneGun_Sword_Hero";
            }
            else
            {
                hero = new GameObject("InsaneGun_Sword_Hero");
                hero.transform.position = StreetSpawnPos;
                hero.transform.rotation = StreetSpawnRot;
                var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                capsule.transform.SetParent(hero.transform);
                capsule.transform.localPosition = new Vector3(0, 1, 0);
            }

            hero.tag = "Player";

            // Character Controller — properly sized for a humanoid character.
            var cc = hero.GetComponent<CharacterController>();
            if (cc == null) cc = hero.AddComponent<CharacterController>();
            cc.center = new Vector3(0f, 0.93f, 0f);
            cc.height = 1.80f;
            cc.radius = 0.35f;
            cc.skinWidth = 0.08f;
            cc.stepOffset = 0.35f;
            cc.minMoveDistance = 0.001f;

            // Animator & Avatar
            var anim = hero.GetComponent<Animator>();
            if (anim == null) anim = hero.GetComponentInChildren<Animator>();
            if (anim == null) anim = hero.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;

            var avatar = AssetDatabase.LoadAssetAtPath<Avatar>("Assets/PolygonCyberCity/Models/Characters.fbx");
            if (avatar != null) anim.avatar = avatar;

            // InsaneGun Combat Script
            var swordCtrl = hero.GetComponent<InsaneGunSwordController>();
            if (swordCtrl == null) swordCtrl = hero.AddComponent<InsaneGunSwordController>();
            swordCtrl.controlMode = InsaneGunSwordController.ControlMode.PlayerControlled;
            swordCtrl.walkSpeed = 3.0f;
            swordCtrl.runSpeed = 7.0f;
            swordCtrl.dodgeDistance = 6.5f;

            // Holographic Ghost Trail Emitter
            var ghostEmitter = hero.GetComponent<HolographicGhostEmitter>();
            if (ghostEmitter == null) ghostEmitter = hero.AddComponent<HolographicGhostEmitter>();

            // Weapons
            EquipHeroWeapons(hero.transform, swordCtrl, anim);

            Selection.activeGameObject = hero;
            return hero;
        }

        private static void EquipHeroWeapons(Transform hero, InsaneGunSwordController swordCtrl, Animator anim)
        {
            Transform rHand = anim != null ? anim.GetBoneTransform(HumanBodyBones.RightHand) : hero;
            Transform lHand = anim != null ? anim.GetBoneTransform(HumanBodyBones.LeftHand) : hero;

            // 1. Right Hand: Cyber Katana Weapon
            string swordPrefabPath = "Assets/PolygonCyberCity/Prefabs/Weapons/SM_Wep_Sword_01.prefab";
            GameObject swordAsset = AssetDatabase.LoadAssetAtPath<GameObject>(swordPrefabPath);
            GameObject katana;

            if (swordAsset != null)
            {
                katana = Object.Instantiate(swordAsset, rHand, false);
                katana.name = "Cyber_Sword";
                katana.transform.localPosition = new Vector3(0.02f, 0.06f, 0.01f);
                katana.transform.localRotation = Quaternion.Euler(0f, 90f, -90f);
            }
            else
            {
                katana = new GameObject("Energy_Katana");
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

            // Setup blade tip transform for wide arc ribbon trail
            Transform tip = katana.transform.Find("Blade_Tip");
            if (tip == null)
            {
                var tipGo = new GameObject("Blade_Tip");
                tipGo.transform.SetParent(katana.transform, false);
                tipGo.transform.localPosition = new Vector3(0f, 0.75f, 0f);
                tip = tipGo.transform;
            }

            // Glowing Additive / Transparent Cyber Material for Weapon Trail
            var trailShader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                           ?? Shader.Find("Universal Render Pipeline/Lit")
                           ?? Shader.Find("Standard");
            var bladeMat = new Material(trailShader);
            bladeMat.color = new Color(0.1f, 0.95f, 1f, 0.85f);
            bladeMat.EnableKeyword("_EMISSION");
            bladeMat.SetColor("_EmissionColor", new Color(0f, 0.9f, 1f) * 3.5f);

            var trail = tip.GetComponent<TrailRenderer>();
            if (trail == null) trail = tip.gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.20f;
            trail.startWidth = 0.65f;
            trail.endWidth = 0.02f;
            trail.sharedMaterial = bladeMat;
            trail.emitting = false;
            trail.minVertexDistance = 0.05f;

            // Gradient: glowing cyan fading into transparent magenta
            var grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(0.1f, 0.95f, 1f), 0.0f),
                    new GradientColorKey(new Color(0.6f, 0.15f, 1f), 0.65f),
                    new GradientColorKey(new Color(0.9f, 0.05f, 0.6f), 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.9f, 0.0f),
                    new GradientAlphaKey(0.6f, 0.6f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            trail.colorGradient = grad;

            swordCtrl.swordTrail = trail;

            // 2. Left Hand: Sidearm Blaster for Gun Attacks
            string pistolPath = "Assets/PolygonCyberCity/Prefabs/Weapons/SM_Wep_Pistol_01.prefab";
            GameObject pistolAsset = AssetDatabase.LoadAssetAtPath<GameObject>(pistolPath);
            if (pistolAsset != null && lHand != null)
            {
                var pistol = Object.Instantiate(pistolAsset, lHand, false);
                pistol.name = "Sidearm_Blaster";
                pistol.transform.localPosition = new Vector3(-0.02f, 0.06f, 0.01f);
                pistol.transform.localRotation = Quaternion.Euler(0f, -90f, 90f);
            }
        }

        private static void SetupFollowCamera(Transform target)
        {
            var camGo = Camera.main != null ? Camera.main.gameObject : GameObject.FindWithTag("MainCamera");
            if (camGo == null)
            {
                camGo = new GameObject("Main Camera");
                camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }

            camGo.tag = "MainCamera";
            camGo.SetActive(true);

            var follow = camGo.GetComponent<SmoothOrbitCamera>();
            if (follow == null) follow = camGo.AddComponent<SmoothOrbitCamera>();
            follow.target = target;
            follow.targetOffset = new Vector3(0f, 1.4f, 0f);
            follow.cameraOffset = new Vector3(0f, 0.4f, -3.8f);

            // Position camera initially behind hero facing down the street
            camGo.transform.position = target.position - (target.forward * 3.8f) + Vector3.up * 1.8f;
            camGo.transform.rotation = Quaternion.Euler(15f, -130f, 0f);
        }

        private static void SpawnCombatDummies()
        {
            // Position dummies on the street plaza facing the hero
            Vector3 forward = StreetSpawnRot * Vector3.forward;
            Vector3 right   = StreetSpawnRot * Vector3.right;
            Quaternion facePlayer = Quaternion.LookRotation(-forward);

            Vector3[] dummyPositions = new Vector3[]
            {
                StreetSpawnPos + forward * 5.0f,               // Center
                StreetSpawnPos + forward * 5.5f - right * 2.8f, // Left
                StreetSpawnPos + forward * 5.5f + right * 2.8f  // Right
            };

            string[] dummyPrefabs = new string[]
            {
                "Assets/PolygonCyberCity/Prefabs/Characters/SM_Chr_Robot_01.prefab",
                "Assets/PolygonCyberCity/Prefabs/Characters/SM_Chr_Male_Soldier_01.prefab",
                "Assets/PolygonCyberCity/Prefabs/Characters/SM_Chr_Fancy_Bot_01.prefab"
            };

            for (int i = 0; i < dummyPositions.Length; i++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(dummyPrefabs[i]);
                GameObject dummy = prefab != null
                    ? Object.Instantiate(prefab, dummyPositions[i], facePlayer)
                    : GameObject.CreatePrimitive(PrimitiveType.Capsule);
                dummy.name = $"Target_Dummy_0{i + 1}";
                dummy.transform.position = dummyPositions[i];
                dummy.transform.rotation = facePlayer;

                // CharacterController for gravity and movement
                var dummyCC = dummy.GetComponent<CharacterController>();
                if (dummyCC == null) dummyCC = dummy.AddComponent<CharacterController>();
                dummyCC.center = new Vector3(0f, 0.93f, 0f);
                dummyCC.height = 1.80f;
                dummyCC.radius = 0.35f;
                dummyCC.skinWidth = 0.08f;
                dummyCC.stepOffset = 0.35f;

                var ctrl = dummy.GetComponent<InsaneGunSwordController>();
                if (ctrl == null) ctrl = dummy.AddComponent<InsaneGunSwordController>();
                ctrl.controlMode = InsaneGunSwordController.ControlMode.AICirclingEnemy;
                ctrl.maxHealth = 300f;
                ctrl.currentHealth = 300f;

                var anim = dummy.GetComponent<Animator>();
                if (anim == null) anim = dummy.GetComponentInChildren<Animator>();
                if (anim == null) anim = dummy.AddComponent<Animator>();
                anim.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
                anim.applyRootMotion = false;
                anim.avatar = AssetDatabase.LoadAssetAtPath<Avatar>("Assets/PolygonCyberCity/Models/Characters.fbx");

                // Holographic Ghost Trail Emitter
                var dummyGhost = dummy.GetComponent<HolographicGhostEmitter>();
                if (dummyGhost == null) dummyGhost = dummy.AddComponent<HolographicGhostEmitter>();
            }
        }
    }
}
#endif
