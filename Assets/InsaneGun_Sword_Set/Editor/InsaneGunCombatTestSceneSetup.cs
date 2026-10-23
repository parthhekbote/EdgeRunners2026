#if UNITY_EDITOR
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using InsaneGun.Combat;
using InsaneGun.Testing;

namespace InsaneGun.Editor
{
    public static class InsaneGunCombatTestSceneSetup
    {
        public const string ScenePath = "Assets/Scenes/InsaneGun_Combat_TestArena.unity";
        public const string GirlControllerPath = "Assets/InsaneGun_Sword_Set/AC_InsaneGun_Sidekick_Girl.controller";
        public const string BoyControllerPath  = "Assets/InsaneGun_Sword_Set/AC_InsaneGun_Sidekick_Boy.controller";
        private const string AnimFolderPath = "Assets/InsaneGun_Sword_Set/Animation/Humanoid";

        [MenuItem("Tools/InsaneGun Sword/Open Combat Test Arena Scene", priority = 1)]
        public static void OpenCombatTestArena()
        {
            if (File.Exists(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Debug.Log($"[InsaneGun] Opened Combat Test Arena: {ScenePath}");
            }
            else
            {
                BuildTestArenaScene();
            }
        }

        [MenuItem("Tools/InsaneGun Sword/Spawn Alita Combat Hero (In Current Scene)", priority = 1)]
        public static void SpawnAlitaHeroInCurrentScene()
        {
            EnsureTagsAndLayers();
            var girlController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(GirlControllerPath);
            if (girlController == null)
            {
                girlController = BuildSidekickCombatController(GirlControllerPath, isFemale: false);
            }

            Vector3 spawnPos = Vector3.zero;
            SceneView view = SceneView.lastActiveSceneView;
            if (view != null && view.camera != null)
            {
                spawnPos = view.camera.transform.position + view.camera.transform.forward * 4f;
                RaycastHit hit;
                if (Physics.Raycast(spawnPos + Vector3.up * 5f, Vector3.down, out hit, 50f))
                {
                    spawnPos = hit.point;
                }
            }

            GameObject hero = SpawnAlitaPlayer(girlController);
            hero.transform.position = spawnPos;
            SetupCamera(hero.transform);

            Selection.activeGameObject = hero;
            Debug.Log($"[InsaneGun] Alita Combat Hero (Battle Angel / Girl Combat Set) spawned in current scene at: {spawnPos}");
        }

        [MenuItem("Tools/InsaneGun Sword/Spawn Masculine Combat Hero (In Current Scene)", priority = 2)]
        public static void SpawnMasculineHeroInCurrentScene()
        {
            EnsureTagsAndLayers();
            var boyController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(BoyControllerPath);
            if (boyController == null)
            {
                boyController = BuildSidekickCombatController(BoyControllerPath, isFemale: false);
            }

            Vector3 spawnPos = Vector3.zero;
            SceneView view = SceneView.lastActiveSceneView;
            if (view != null && view.camera != null)
            {
                spawnPos = view.camera.transform.position + view.camera.transform.forward * 4f;
                RaycastHit hit;
                if (Physics.Raycast(spawnPos + Vector3.up * 5f, Vector3.down, out hit, 50f))
                {
                    spawnPos = hit.point;
                }
            }

            GameObject hero = SpawnBoyPlayer(boyController);
            hero.transform.position = spawnPos;
            SetupCamera(hero.transform);

            Selection.activeGameObject = hero;
            Debug.Log($"[InsaneGun] Masculine Combat Hero (Sidekick Boy) spawned in current scene at: {spawnPos}");
        }

        [MenuItem("Tools/InsaneGun Sword/Rebuild Combat Test Arena Scene", priority = 3)]
        public static void BuildTestArenaScene()
        {
            // 1. Build Girl & Boy master combat animator controllers (Alita configured with Masculine Locomotion as requested)
            var girlController = BuildSidekickCombatController(GirlControllerPath, isFemale: false);
            var boyController  = BuildSidekickCombatController(BoyControllerPath,  isFemale: false);

            // 2. Ensure layers and tags exist
            EnsureTagsAndLayers();

            // 3. Create fresh scene
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 4. Build cyber combat arena environment
            BuildArenaEnvironment();

            // 5. Setup lighting & atmosphere
            SetupLightingAndAtmosphere();

            // 6. Spawn Alita as Playable Hero (Synty Feminine Locomotion & Girl Combat Animations)
            GameObject player = SpawnAlitaPlayer(girlController);

            // 7. Spawn Boy as Target Enemy Rival (Synty Masculine Locomotion & Boy Combat Animations)
            GameObject enemy = SpawnBoyEnemy(boyController, player.transform);

            // 8. Setup Camera
            SetupCamera(player.transform);

            // 9. Add Test Arena Manager
            SetupTestManager(player, enemy);

            // 10. Save Scene
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[InsaneGun] Combat Test Arena (Alita Battle Angel Hero vs Sidekick Boy Rival) created successfully at: {ScenePath}");
        }

        public static AnimatorController BuildSidekickCombatController(string outputPath, bool isFemale)
        {
            if (File.Exists(outputPath))
            {
                AssetDatabase.DeleteAsset(outputPath);
                AssetDatabase.Refresh();
            }

            var controller = AnimatorController.CreateAnimatorControllerAtPath(outputPath);

            // Parameters
            controller.AddParameter("MoveSpeed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Attack",      AnimatorControllerParameterType.Trigger);
            controller.AddParameter("AttackIndex", AnimatorControllerParameterType.Int);
            controller.AddParameter("HeavyCombo",  AnimatorControllerParameterType.Trigger);
            controller.AddParameter("HeavyIndex",  AnimatorControllerParameterType.Int);
            controller.AddParameter("GunAttack",   AnimatorControllerParameterType.Trigger);
            controller.AddParameter("GunIndex",    AnimatorControllerParameterType.Int);
            controller.AddParameter("Dodge",       AnimatorControllerParameterType.Trigger);
            controller.AddParameter("DodgeDir",    AnimatorControllerParameterType.Int);
            controller.AddParameter("Hit",         AnimatorControllerParameterType.Trigger);
            controller.AddParameter("HitDir",      AnimatorControllerParameterType.Int);
            controller.AddParameter("HitType",     AnimatorControllerParameterType.Int);
            controller.AddParameter("DeadIndex",   AnimatorControllerParameterType.Int);
            controller.AddParameter("Die",         AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Draw",        AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Sheathe",     AnimatorControllerParameterType.Trigger);
            controller.AddParameter("IsSwordDrawn",AnimatorControllerParameterType.Bool);
            controller.AddParameter("Jump",        AnimatorControllerParameterType.Trigger);
            controller.AddParameter("IsJumping",    AnimatorControllerParameterType.Bool);

            var rootSM = controller.layers[0].stateMachine;

            // 1. Synty Locomotion BlendTree (Idle -> Walk -> Run)
            var locoState = rootSM.AddState("Locomotion", new Vector3(300, 100, 0));
            BlendTree blendTree = new BlendTree();
            blendTree.name = isFemale ? "Synty_Locomotion_Feminine" : "Synty_Locomotion_Masculine";
            blendTree.blendType = BlendTreeType.Simple1D;
            blendTree.blendParameter = "MoveSpeed";
            blendTree.minThreshold = 0f;
            blendTree.maxThreshold = 2f;

            string genderFolder = isFemale ? "Feminine" : "Masculine";
            string suffix = isFemale ? "Femn" : "Masc";

            AnimationClip syntyIdle = LoadSyntyClip($"Assets/Synty/AnimationBaseLocomotion/Animations/Sidekick/{genderFolder}/Idles/A_MOD_BL_Idle_Standing_{suffix}.fbx");
            AnimationClip syntyWalk = LoadSyntyClip($"Assets/Synty/AnimationBaseLocomotion/Animations/Sidekick/{genderFolder}/Locomotion/Walk/A_MOD_BL_Walk_F_{suffix}.fbx");
            AnimationClip syntyRun  = LoadSyntyClip($"Assets/Synty/AnimationBaseLocomotion/Animations/Sidekick/{genderFolder}/Locomotion/Run/A_MOD_BL_Run_F_{suffix}.fbx");

            if (syntyIdle != null) blendTree.AddChild(syntyIdle, 0f);
            if (syntyWalk != null) blendTree.AddChild(syntyWalk, 1f);
            if (syntyRun  != null) blendTree.AddChild(syntyRun,  2f);

            AssetDatabase.AddObjectToAsset(blendTree, controller);
            locoState.motion = blendTree;
            rootSM.defaultState = locoState;

            // 2. Light Sword Attacks (atk_sword_01–07)
            for (int i = 1; i <= 7; i++)
            {
                var clip = LoadCombatClip($"atk_sword_0{i}");
                if (clip == null) continue;
                var state = rootSM.AddState($"Attack_0{i}", new Vector3(600, 40 * i, 0));
                state.motion = clip;
                state.tag = "Attack";
                var t = rootSM.AddAnyStateTransition(state);
                t.AddCondition(AnimatorConditionMode.If, 0, "Attack");
                t.AddCondition(AnimatorConditionMode.Equals, i, "AttackIndex");
                t.duration = 0.04f;
                t.hasExitTime = false;
                t.canTransitionToSelf = false;
                var ex = state.AddTransition(locoState);
                ex.hasExitTime = true;
                ex.exitTime = 0.82f;
                ex.duration = 0.12f;
            }

            // 3. Single-Click Heavy Attacks (Heavy_01, Heavy_02, Heavy_03 Finisher)
            // Each RMB click plays ONE distinct heavy strike. Chaining occurs only on subsequent RMB clicks!
            string[] heavyClipNames = { "combo_01_1", "combo_01_2", "combo_01_3" };
            string[] heavyStateNames = { "Heavy_01_Slash", "Heavy_02_Cleave", "Heavy_03_Finisher" };

            for (int h = 1; h <= 3; h++)
            {
                var clip = LoadCombatClip(heavyClipNames[h - 1])
                        ?? LoadCombatClip($"combo_0{h}_1")
                        ?? LoadCombatClip($"atk_sword_0{h + 4}");
                if (clip == null) continue;

                var state = rootSM.AddState(heavyStateNames[h - 1], new Vector3(880, 80 * h, 0));
                state.motion = clip;
                state.tag = "Attack";

                var t = rootSM.AddAnyStateTransition(state);
                t.AddCondition(AnimatorConditionMode.If, 0, "HeavyCombo");
                t.AddCondition(AnimatorConditionMode.Equals, h, "HeavyIndex");
                t.duration = 0.04f;
                t.hasExitTime = false;
                t.canTransitionToSelf = false;

                // Return to Locomotion on single click (NO automatic chain to next phase!)
                var ex = state.AddTransition(locoState);
                ex.hasExitTime = true;
                ex.exitTime = (h == 3) ? 0.88f : 0.82f;
                ex.duration = 0.12f;
            }

            // 4. Gun Attacks
            var walkGunClip = LoadCombatClip("atk_gun_walking");
            if (walkGunClip != null)
            {
                var wg = rootSM.AddState("Gun_Walking", new Vector3(600, 650, 0));
                wg.motion = walkGunClip;
                wg.tag = "Attack";
                var wgt = rootSM.AddAnyStateTransition(wg);
                wgt.AddCondition(AnimatorConditionMode.If, 0, "GunAttack");
                wgt.AddCondition(AnimatorConditionMode.Greater, 0.3f, "MoveSpeed");
                wgt.duration = 0.06f; wgt.hasExitTime = false; wgt.canTransitionToSelf = false;
                var wgex = wg.AddTransition(locoState);
                wgex.hasExitTime = true; wgex.exitTime = 0.85f; wgex.duration = 0.12f;
            }

            for (int g = 1; g <= 4; g++)
            {
                var clip = LoadCombatClip($"atk_gun_0{g}");
                if (clip == null) continue;
                var state = rootSM.AddState($"Gun_Attack_0{g}", new Vector3(600, 430 + 50 * g, 0));
                state.motion = clip;
                state.tag = "Attack";
                var t = rootSM.AddAnyStateTransition(state);
                t.AddCondition(AnimatorConditionMode.If, 0, "GunAttack");
                t.AddCondition(AnimatorConditionMode.Equals, g, "GunIndex");
                t.duration = 0.06f; t.hasExitTime = false; t.canTransitionToSelf = false;
                var ex = state.AddTransition(locoState);
                ex.hasExitTime = true; ex.exitTime = 0.85f; ex.duration = 0.12f;
            }

            // 5. Directional Dodge
            string[] dodgeClips = { "avoid_front", "avoid_back", "avoid_left", "avoid_right" };
            string[] rollFallbk = { "roll_front",  "roll_back",  "roll_left",  "roll_right"  };
            string[] dodgeNames = { "Dodge_Front", "Dodge_Back", "Dodge_Left", "Dodge_Right" };
            for (int d = 0; d < 4; d++)
            {
                var clip = LoadCombatClip(dodgeClips[d]) ?? LoadCombatClip(rollFallbk[d]);
                if (clip == null) continue;
                var state = rootSM.AddState(dodgeNames[d], new Vector3(300, 330 + 90 * d, 0));
                state.motion = clip;
                state.tag = "Dodge";
                var t = rootSM.AddAnyStateTransition(state);
                t.AddCondition(AnimatorConditionMode.If, 0, "Dodge");
                t.AddCondition(AnimatorConditionMode.Equals, d, "DodgeDir");
                t.duration = 0.04f; t.hasExitTime = false; t.canTransitionToSelf = false;
                var ex = state.AddTransition(locoState);
                ex.hasExitTime = true; ex.exitTime = 0.85f; ex.duration = 0.10f;
            }

            // 6a. Light Body Hit Reactions (HitType=0) — canTransitionToSelf=true for combo sync!
            string[] bodyClips = { "hit_body_front", "hit_body_back", "hit_body_left", "hit_body_right" };
            string[] bodyNames = { "HitBody_Front",  "HitBody_Back",  "HitBody_Left",  "HitBody_Right"  };
            for (int d = 0; d < 4; d++)
            {
                var clip = LoadCombatClip(bodyClips[d]);
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

            // 6a2. Light Head Hit Reactions (HitType=5)
            string[] headClips = { "hit_head_front", "hit_head_back", "hit_head_left", "hit_head_right" };
            string[] headNames = { "HitHead_Front",  "HitHead_Back",  "HitHead_Left",  "HitHead_Right"  };
            for (int d = 0; d < 4; d++)
            {
                var clip = LoadCombatClip(headClips[d]) ?? LoadCombatClip(bodyClips[d]);
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

            // 6b. Heavy Hit Reactions (HitType=1)
            string[] heavyClips = { "hit_heavy_front", "hit_heavy_back", "hit_heavy_left", "hit_heavy_right" };
            string[] heavyNames = { "HitHeavy_Front",  "HitHeavy_Back",  "HitHeavy_Left",  "HitHeavy_Right"  };
            for (int d = 0; d < 4; d++)
            {
                var clip = LoadCombatClip(heavyClips[d]);
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

            // 6c. Combo Finisher Hit Reactions (HitType=2/3/4)
            for (int c = 1; c <= 3; c++)
            {
                var clip = LoadCombatClip($"combo_0{c}_hit");
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

            // 7. Death (dead_01–05)
            for (int di = 1; di <= 5; di++)
            {
                var clip = LoadCombatClip($"dead_0{di}");
                if (clip == null) continue;
                var state = rootSM.AddState($"Death_0{di}", new Vector3(-100, 680 + 90 * di, 0));
                state.motion = clip;
                state.tag = "Death";
                var t = rootSM.AddAnyStateTransition(state);
                t.AddCondition(AnimatorConditionMode.If, 0, "Die");
                t.AddCondition(AnimatorConditionMode.Equals, di, "DeadIndex");
                t.duration = 0.08f; t.hasExitTime = false; t.canTransitionToSelf = false;
            }

            // 8. Sword Draw & Sheathe States
            AnimationClip drawClip = LoadSyntyClip($"Assets/Synty/AnimationSwordCombat/Animations/Polygon/Idle/Sheathed/A_Draw_Sword_{suffix}.fbx")
                                  ?? LoadCombatClip("equip")
                                  ?? LoadCombatClip("atk_sword_01");
            if (drawClip != null)
            {
                var state = rootSM.AddState("Draw_Sword", new Vector3(850, 100, 0));
                state.motion = drawClip;
                state.tag = "Draw";
                var t = rootSM.AddAnyStateTransition(state);
                t.AddCondition(AnimatorConditionMode.If, 0, "Draw");
                t.duration = 0.05f; t.hasExitTime = false; t.canTransitionToSelf = false;
                var ex = state.AddTransition(locoState);
                ex.hasExitTime = true; ex.exitTime = 0.85f; ex.duration = 0.15f;
            }

            AnimationClip sheatheClip = LoadSyntyClip($"Assets/Synty/AnimationSwordCombat/Animations/Polygon/Idle/Sheathed/A_Sheathe_Sword_{suffix}.fbx")
                                     ?? LoadCombatClip("unequip")
                                     ?? LoadCombatClip("atk_sword_01");
            if (sheatheClip != null)
            {
                var state = rootSM.AddState("Sheathe_Sword", new Vector3(850, 180, 0));
                state.motion = sheatheClip;
                state.tag = "Sheathe";
                var t = rootSM.AddAnyStateTransition(state);
                t.AddCondition(AnimatorConditionMode.If, 0, "Sheathe");
                t.duration = 0.05f; t.hasExitTime = false; t.canTransitionToSelf = false;
                var ex = state.AddTransition(locoState);
                ex.hasExitTime = true; ex.exitTime = 0.85f; ex.duration = 0.15f;
            }

            // 9. Jump & In-Air States (Synty Base Locomotion Package)
            AnimationClip jumpIdleClip = LoadSyntyClip($"Assets/Synty/AnimationBaseLocomotion/Animations/Sidekick/{genderFolder}/InAir/A_MOD_BL_Jump_Idle_{suffix}.fbx")
                                      ?? LoadSyntyClip($"Assets/Synty/AnimationBaseLocomotion/Animations/Polygon/{genderFolder}/InAir/A_Jump_Idle_{suffix}.fbx");

            AnimationClip jumpRunClip  = LoadSyntyClip($"Assets/Synty/AnimationBaseLocomotion/Animations/Sidekick/{genderFolder}/InAir/A_MOD_BL_Jump_Running_{suffix}.fbx")
                                      ?? LoadSyntyClip($"Assets/Synty/AnimationBaseLocomotion/Animations/Polygon/{genderFolder}/InAir/A_Jump_Running_{suffix}.fbx");

            AnimationClip landClip     = LoadSyntyClip($"Assets/Synty/AnimationBaseLocomotion/Animations/Sidekick/{genderFolder}/InAir/A_MOD_BL_Land_IdleSoft_{suffix}.fbx")
                                      ?? LoadSyntyClip($"Assets/Synty/AnimationBaseLocomotion/Animations/Polygon/{genderFolder}/InAir/A_Land_IdleSoft_{suffix}.fbx");

            if (jumpIdleClip != null)
            {
                var jumpState = rootSM.AddState("Jump", new Vector3(850, 260, 0));

                BlendTree jumpBlendTree = new BlendTree();
                jumpBlendTree.name = isFemale ? "Synty_Locomotion_Jump_Femn" : "Synty_Locomotion_Jump_Masc";
                jumpBlendTree.blendType = BlendTreeType.Simple1D;
                jumpBlendTree.blendParameter = "MoveSpeed";
                jumpBlendTree.minThreshold = 0f;
                jumpBlendTree.maxThreshold = 2f;

                jumpBlendTree.AddChild(jumpIdleClip, 0f);
                if (jumpRunClip != null)
                {
                    jumpBlendTree.AddChild(jumpRunClip, 1f);
                    jumpBlendTree.AddChild(jumpRunClip, 2f);
                }
                else
                {
                    jumpBlendTree.AddChild(jumpIdleClip, 2f);
                }

                AssetDatabase.AddObjectToAsset(jumpBlendTree, controller);
                jumpState.motion = jumpBlendTree;
                jumpState.tag = "Jump";

                var t = rootSM.AddAnyStateTransition(jumpState);
                t.AddCondition(AnimatorConditionMode.If, 0, "Jump");
                t.duration = 0.04f; t.hasExitTime = false; t.canTransitionToSelf = false;

                if (landClip != null)
                {
                    var landState = rootSM.AddState("Land", new Vector3(850, 340, 0));
                    landState.motion = landClip;
                    landState.tag = "Land";

                    var toLand = jumpState.AddTransition(landState);
                    toLand.AddCondition(AnimatorConditionMode.IfNot, 0, "IsJumping");
                    toLand.duration = 0.08f; toLand.hasExitTime = false;

                    var landToLoco = landState.AddTransition(locoState);
                    landToLoco.hasExitTime = true; landToLoco.exitTime = 0.65f; landToLoco.duration = 0.12f;

                    var landCancelRun = landState.AddTransition(locoState);
                    landCancelRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "MoveSpeed");
                    landCancelRun.duration = 0.08f; landCancelRun.hasExitTime = false;
                }

                var ex = jumpState.AddTransition(locoState);
                ex.hasExitTime = true; ex.exitTime = 0.85f; ex.duration = 0.15f;

                var exAir = jumpState.AddTransition(locoState);
                exAir.AddCondition(AnimatorConditionMode.IfNot, 0, "IsJumping");
                exAir.duration = 0.12f; exAir.hasExitTime = false;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static AnimationClip LoadSyntyClip(string fullPath)
        {
            string norm = fullPath.Replace('\\', '/');
            var directClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(norm);
            if (directClip != null && !directClip.name.StartsWith("__preview__")) return directClip;

            if (File.Exists(norm) || File.Exists(Path.GetFullPath(norm)))
            {
                var assets = AssetDatabase.LoadAllAssetsAtPath(norm);
                if (assets != null)
                {
                    foreach (var a in assets)
                    {
                        if (a is AnimationClip c && !c.name.StartsWith("__preview__")) return c;
                    }
                }
            }
            return null;
        }

        private static AnimationClip LoadCombatClip(string fbxName)
        {
            string[] candidatePaths = new string[]
            {
                $"{AnimFolderPath}/{fbxName}.fbx",
                $"{AnimFolderPath}/{fbxName}_inplace.fbx",
                $"{AnimFolderPath}/Inplace/{fbxName}_inplace.fbx"
            };

            foreach (var path in candidatePaths)
            {
                string norm = path.Replace('\\', '/');
                if (File.Exists(norm) || File.Exists(Path.GetFullPath(norm)))
                {
                    var assets = AssetDatabase.LoadAllAssetsAtPath(norm);
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

        private static void EnsureTagsAndLayers()
        {
            var tagManagerObj = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (tagManagerObj == null || tagManagerObj.Length == 0) return;
            SerializedObject tagManager = new SerializedObject(tagManagerObj[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            if (layers != null)
            {
                var p8 = layers.GetArrayElementAtIndex(8);
                if (p8 != null && string.IsNullOrEmpty(p8.stringValue)) p8.stringValue = "Player";
                var p9 = layers.GetArrayElementAtIndex(9);
                if (p9 != null && string.IsNullOrEmpty(p9.stringValue)) p9.stringValue = "Enemy";
            }

            SerializedProperty tags = tagManager.FindProperty("tags");
            if (tags != null)
            {
                bool hasEnemy = false;
                for (int i = 0; i < tags.arraySize; i++)
                {
                    if (tags.GetArrayElementAtIndex(i).stringValue == "Enemy") { hasEnemy = true; break; }
                }
                if (!hasEnemy)
                {
                    tags.InsertArrayElementAtIndex(tags.arraySize);
                    tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = "Enemy";
                }
            }
            tagManager.ApplyModifiedProperties();
        }

        private static void BuildArenaEnvironment()
        {
            GameObject arenaRoot = new GameObject("Combat_Arena_Environment");

            // 1. Cyber Combat Arena Floor Slab (40m x 40m)
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Cyber_Floor_Slab";
            floor.transform.SetParent(arenaRoot.transform);
            floor.transform.position = new Vector3(0f, -0.25f, 0f);
            floor.transform.localScale = new Vector3(40f, 0.5f, 40f);

            var floorMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/PolygonCyberCity/Materials/Building/Floor_02.mat")
                        ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/PolygonCyberCity/Materials/Building/Floor_01.mat");
            if (floorMat != null)
            {
                floor.GetComponent<Renderer>().sharedMaterial = floorMat;
            }

            // 2. Central Dual-Ring Duel Marker (Emissive Neon Combat Decal)
            var ringShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Duel_Ring_Marker";
            ring.transform.SetParent(arenaRoot.transform);
            ring.transform.position = new Vector3(0f, 0.005f, 0f);
            ring.transform.localScale = new Vector3(15f, 0.01f, 15f);
            Object.DestroyImmediate(ring.GetComponent<Collider>());

            var ringMat = new Material(ringShader);
            ringMat.color = new Color(0.04f, 0.12f, 0.22f, 0.75f);
            ringMat.EnableKeyword("_EMISSION");
            ringMat.SetColor("_EmissionColor", new Color(0f, 0.85f, 1f) * 1.8f);
            ring.GetComponent<Renderer>().sharedMaterial = ringMat;

            GameObject innerRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            innerRing.name = "Duel_Inner_Circle";
            innerRing.transform.SetParent(arenaRoot.transform);
            innerRing.transform.position = new Vector3(0f, 0.008f, 0f);
            innerRing.transform.localScale = new Vector3(7.5f, 0.01f, 7.5f);
            Object.DestroyImmediate(innerRing.GetComponent<Collider>());

            var innerMat = new Material(ringShader);
            innerMat.color = new Color(0.18f, 0.03f, 0.14f, 0.75f);
            innerMat.EnableKeyword("_EMISSION");
            innerMat.SetColor("_EmissionColor", new Color(1f, 0.15f, 0.75f) * 1.8f);
            innerRing.GetComponent<Renderer>().sharedMaterial = innerMat;

            // 3. Overhead Giant Holographic Centerpieces (Dragon + Swimming Koi)
            string dragonPath = "Assets/PolygonCyberCity/Prefabs/FX/Hologram_Dragon_01.prefab";
            var dragonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(dragonPath);
            if (dragonPrefab != null)
            {
                var dragon = Object.Instantiate(dragonPrefab, new Vector3(0f, 10.5f, 0f), Quaternion.Euler(0f, 35f, 0f), arenaRoot.transform);
                dragon.name = "Overhead_Hologram_Dragon";
                dragon.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
            }

            string fishPath = "Assets/PolygonCyberCity/Prefabs/FX/FX_Fish_Hologram_01.prefab";
            var fishPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(fishPath);
            if (fishPrefab != null)
            {
                var fish1 = Object.Instantiate(fishPrefab, new Vector3(-5.5f, 6.5f, 4f), Quaternion.Euler(15f, 110f, 0f), arenaRoot.transform);
                fish1.name = "Hologram_Koi_01";
                fish1.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);

                var fish2 = Object.Instantiate(fishPrefab, new Vector3(5.5f, 7.2f, -4f), Quaternion.Euler(-10f, -70f, 0f), arenaRoot.transform);
                fish2.name = "Hologram_Koi_02";
                fish2.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
            }

            // 4. Four Corner Holographic Cherry Trees (Illuminated)
            string treePath = "Assets/PolygonCyberCity/Prefabs/Env/SM_Env_Hologram_Cherry_Tree_01.prefab";
            var treePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(treePath);
            Vector3[] cornerPositions = new Vector3[]
            {
                new Vector3(16f, 0f, 16f),
                new Vector3(-16f, 0f, 16f),
                new Vector3(16f, 0f, -16f),
                new Vector3(-16f, 0f, -16f)
            };

            for (int i = 0; i < cornerPositions.Length; i++)
            {
                if (treePrefab != null)
                {
                    var tree = Object.Instantiate(treePrefab, cornerPositions[i], Quaternion.Euler(0f, i * 90f + 45f, 0f), arenaRoot.transform);
                    tree.name = $"Hologram_Cherry_Tree_0{i + 1}";
                }

                // Hot pink / cyan under-canopy glow
                var treeLightGo = new GameObject($"Tree_Glow_Light_0{i + 1}");
                treeLightGo.transform.SetParent(arenaRoot.transform);
                treeLightGo.transform.position = cornerPositions[i] + Vector3.up * 2.8f;
                var tLight = treeLightGo.AddComponent<Light>();
                tLight.type = LightType.Point;
                tLight.color = (i % 2 == 0) ? new Color(1f, 0.15f, 0.75f) : new Color(0f, 0.85f, 1f);
                tLight.intensity = 3.5f;
                tLight.range = 14f;
            }

            // 5. Perimeter Laser Grid Walls & Military Barricades
            string laserWallPath = "Assets/PolygonCyberCity/Prefabs/Buildings/SM_Bld_Wall_Laser_01.prefab";
            string barricadePath = "Assets/PolygonCyberCity/Prefabs/Props/SM_Prop_Barricade_Military_01.prefab";
            var laserPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(laserWallPath);
            var barricadePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(barricadePath);

            // Spawn laser walls along 4 perimeter sides
            float halfEdge = 19.5f;
            for (int edge = 0; edge < 4; edge++)
            {
                float rotAngle = edge * 90f;
                Quaternion rot = Quaternion.Euler(0f, rotAngle, 0f);

                for (int slot = -3; slot <= 3; slot++)
                {
                    if (slot == 0) continue; // Leave central gateway gaps
                    Vector3 localPos = new Vector3(slot * 4.8f, 0f, halfEdge);
                    Vector3 worldPos = rot * localPos;

                    if (laserPrefab != null && Mathf.Abs(slot) >= 2)
                    {
                        var laser = Object.Instantiate(laserPrefab, worldPos, rot, arenaRoot.transform);
                        laser.name = $"Laser_Wall_E{edge}_S{slot}";
                    }
                    else if (barricadePrefab != null)
                    {
                        var bar = Object.Instantiate(barricadePrefab, worldPos, rot, arenaRoot.transform);
                        bar.name = $"Barricade_E{edge}_S{slot}";
                    }
                }
            }

            // 6. Grand Street Lanterns (With Functional Downward Spotlights)
            string streetLightPath = "Assets/PolygonCyberCity/Prefabs/Props/SM_Prop_Street_Light_Large_01.prefab";
            var streetLightPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(streetLightPath);
            Vector3[] lanternPositions = new Vector3[]
            {
                new Vector3(0f, 0f, 18.5f),
                new Vector3(0f, 0f, -18.5f),
                new Vector3(18.5f, 0f, 0f),
                new Vector3(-18.5f, 0f, 0f)
            };
            float[] lanternRotations = new float[] { 180f, 0f, 270f, 90f };

            for (int i = 0; i < lanternPositions.Length; i++)
            {
                if (streetLightPrefab != null)
                {
                    var sl = Object.Instantiate(streetLightPrefab, lanternPositions[i], Quaternion.Euler(0f, lanternRotations[i], 0f), arenaRoot.transform);
                    sl.name = $"Street_Light_Large_0{i + 1}";
                }

                // Spotlight casting pool of light onto the combatants
                var spotGo = new GameObject($"Arena_Perimeter_Spotlight_0{i + 1}");
                spotGo.transform.SetParent(arenaRoot.transform);
                spotGo.transform.position = lanternPositions[i] + Vector3.up * 4.2f;
                spotGo.transform.rotation = Quaternion.Euler(55f, lanternRotations[i], 0f);

                var spot = spotGo.AddComponent<Light>();
                spot.type = LightType.Spot;
                spot.color = (i % 2 == 0) ? new Color(0.2f, 0.85f, 1f) : new Color(1f, 0.45f, 0.1f);
                spot.intensity = 6.5f;
                spot.range = 25f;
                spot.spotAngle = 65f;
                spot.shadows = LightShadows.Soft;
            }

            // 7. Surrounding Cyberpunk Megacity Skyline (8 Towering Skyscrapers)
            string[] bldPrefabs = new string[]
            {
                "Assets/PolygonCyberCity/Prefabs/Buildings/SM_Bld_Background_Building_01.prefab",
                "Assets/PolygonCyberCity/Prefabs/Buildings/SM_Bld_Background_Building_02.prefab",
                "Assets/PolygonCyberCity/Prefabs/Buildings/SM_Bld_Background_Building_03.prefab",
                "Assets/PolygonCyberCity/Prefabs/Buildings/SM_Bld_Background_Building_04.prefab"
            };

            Vector3[] bldPositions = new Vector3[]
            {
                new Vector3(0f, -2f, 52f),    // North
                new Vector3(45f, -2f, 45f),   // North-East
                new Vector3(52f, -2f, 0f),    // East
                new Vector3(45f, -2f, -45f),  // South-East
                new Vector3(0f, -2f, -52f),   // South
                new Vector3(-45f, -2f, -45f), // South-West
                new Vector3(-52f, -2f, 0f),   // West
                new Vector3(-45f, -2f, 45f)   // North-West
            };
            float[] bldAngles = new float[] { 180f, 225f, 270f, 315f, 0f, 45f, 90f, 135f };

            for (int i = 0; i < bldPositions.Length; i++)
            {
                string bldPath = bldPrefabs[i % bldPrefabs.Length];
                var bldAsset = AssetDatabase.LoadAssetAtPath<GameObject>(bldPath);
                if (bldAsset != null)
                {
                    var bld = Object.Instantiate(bldAsset, bldPositions[i], Quaternion.Euler(0f, bldAngles[i], 0f), arenaRoot.transform);
                    bld.name = $"Skyscraper_Skyline_0{i + 1}";
                }
            }

            // 8. Holographic Ads & Cyber Billboards
            string billboardPath = "Assets/PolygonCyberCity/Prefabs/Props/SM_Prop_Billboard_Clean_Sign_01.prefab";
            var bbPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(billboardPath);
            if (bbPrefab != null)
            {
                var bb1 = Object.Instantiate(bbPrefab, new Vector3(0f, 3.5f, 19.2f), Quaternion.Euler(0f, 180f, 0f), arenaRoot.transform);
                bb1.name = "Cyber_Billboard_North";
                var bb2 = Object.Instantiate(bbPrefab, new Vector3(0f, 3.5f, -19.2f), Quaternion.Euler(0f, 0f, 0f), arenaRoot.transform);
                bb2.name = "Cyber_Billboard_South";
            }
        }

        private static void SetupLightingAndAtmosphere()
        {
            // 1. Cool Cyberpunk Moonlight Key Light
            var lightGo = new GameObject("Directional_Cyber_Moonlight");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.35f, 0.55f, 0.85f);
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(52f, -38f, 0f);

            // 2. High-Tech Overhead Combat Ring Floodlight
            var floodGo = new GameObject("Arena_Overhead_Floodlight");
            var flood = floodGo.AddComponent<Light>();
            flood.type = LightType.Spot;
            flood.color = new Color(0.7f, 0.95f, 1.0f);
            flood.intensity = 5.5f;
            flood.range = 28f;
            flood.spotAngle = 78f;
            flood.shadows = LightShadows.Soft;
            floodGo.transform.position = new Vector3(0f, 14f, 0f);
            floodGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            // 3. Neon Cyber Rim Lights (Cyan / Hot Magenta / Electric Purple)
            var cyanLightGo = new GameObject("Neon_Rim_Light_Cyan");
            var cyanLight = cyanLightGo.AddComponent<Light>();
            cyanLight.type = LightType.Point;
            cyanLight.color = new Color(0f, 0.95f, 1f);
            cyanLight.intensity = 4.8f;
            cyanLight.range = 32f;
            cyanLightGo.transform.position = new Vector3(-10f, 3.8f, 7f);

            var magLightGo = new GameObject("Neon_Rim_Light_Magenta");
            var magLight = magLightGo.AddComponent<Light>();
            magLight.type = LightType.Point;
            magLight.color = new Color(1f, 0.10f, 0.75f);
            magLight.intensity = 4.8f;
            magLight.range = 32f;
            magLightGo.transform.position = new Vector3(10f, 3.8f, -7f);

            var purpleLightGo = new GameObject("Neon_Rim_Light_Purple");
            var purpleLight = purpleLightGo.AddComponent<Light>();
            purpleLight.type = LightType.Point;
            purpleLight.color = new Color(0.6f, 0.15f, 1f);
            purpleLight.intensity = 3.8f;
            purpleLight.range = 28f;
            purpleLightGo.transform.position = new Vector3(-10f, 3.8f, -7f);

            // 4. Midnight Cyberpunk Atmosphere & Volumetric Fog
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.06f, 0.10f, 0.18f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.010f;
            RenderSettings.fogColor = new Color(0.02f, 0.05f, 0.12f);

            // 5. Next-Gen AAA Realistic Graphics Master (ACES, HDR Bloom, Wet Roads, SSAO)
            var gfxGo = new GameObject("AAA_Cyberpunk_Graphics_Master");
            var gfxMaster = gfxGo.AddComponent<AAACyberpunkGraphicsMaster>();
            gfxMaster.SetupPipeline();
        }

        private static GameObject SpawnBoyPlayer(RuntimeAnimatorController controller)
        {
            Vector3 spawnPos = new Vector3(0f, 0f, -3.0f);
            Quaternion spawnRot = Quaternion.identity;

            // Synty Sidekick HumanSpecies_02 (Boy)
            string prefabPath = "Assets/Synty/SidekickCharacters/Characters/HumanSpecies/HumanSpecies_02/HumanSpecies_02.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            GameObject hero = prefab != null
                ? Object.Instantiate(prefab, spawnPos, spawnRot)
                : new GameObject("Sidekick_Boy_Hero");

            hero.name = "Sidekick_Boy_Hero";
            hero.tag = "Player";
            hero.layer = 8; // Layer 8 = Player

            foreach (Transform child in hero.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = 8;
            }

            var cc = hero.GetComponent<CharacterController>();
            if (cc == null) cc = hero.AddComponent<CharacterController>();
            cc.center = new Vector3(0f, 0.95f, 0f);
            cc.height = 1.88f;
            cc.radius = 0.35f;
            cc.skinWidth = 0.08f;
            cc.stepOffset = 0.35f;

            var anim = hero.GetComponent<Animator>();
            if (anim == null) anim = hero.GetComponentInChildren<Animator>();
            if (anim == null) anim = hero.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;

            var swordCtrl = hero.GetComponent<InsaneGunSwordController>();
            if (swordCtrl == null) swordCtrl = hero.AddComponent<InsaneGunSwordController>();
            swordCtrl.controlMode = InsaneGunSwordController.ControlMode.PlayerControlled;
            swordCtrl.enemyLayers = 1 << 9; // Target Enemy layer
            swordCtrl.maxHealth = 300f;
            swordCtrl.currentHealth = 300f;
            swordCtrl.walkSpeed = 3.0f;
            swordCtrl.runSpeed = 6.8f;
            swordCtrl.dodgeDistance = 6.5f;
            swordCtrl.enableSlowMotion = true;

            var ghost = hero.GetComponent<HolographicGhostEmitter>();
            if (ghost == null) ghost = hero.AddComponent<HolographicGhostEmitter>();

            var ragdoll = hero.GetComponent<EuphoriaRagdollController>();
            if (ragdoll == null) ragdoll = hero.AddComponent<EuphoriaRagdollController>();
            ragdoll.ragdollForceMultiplier = 1.35f;
            ragdoll.autoRecoverDelay = 2.2f;
            ragdoll.autoRecover = true;
            ragdoll.SetupRagdollBones();

            EquipSidekickWeapons(hero.transform, swordCtrl, anim, isPlayer: true);

            return hero;
        }

        private static GameObject SpawnGirlEnemy(RuntimeAnimatorController controller, Transform playerTarget)
        {
            Vector3 spawnPos = new Vector3(0f, 0f, 2.5f);
            Quaternion spawnRot = Quaternion.Euler(0f, 180f, 0f);

            // Synty Sidekick HumanSpecies_01 (Girl)
            string prefabPath = "Assets/Synty/SidekickCharacters/Characters/HumanSpecies/HumanSpecies_01/HumanSpecies_01.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            GameObject enemy = prefab != null
                ? Object.Instantiate(prefab, spawnPos, spawnRot)
                : new GameObject("Sidekick_Girl_Rival");

            enemy.name = "Sidekick_Girl_Rival";
            enemy.tag = "Enemy";
            enemy.layer = 9; // Layer 9 = Enemy

            foreach (Transform child in enemy.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = 9;
            }

            var cc = enemy.GetComponent<CharacterController>();
            if (cc == null) cc = enemy.AddComponent<CharacterController>();
            cc.center = new Vector3(0f, 0.93f, 0f);
            cc.height = 1.80f;
            cc.radius = 0.35f;
            cc.skinWidth = 0.08f;
            cc.stepOffset = 0.35f;

            var anim = enemy.GetComponent<Animator>();
            if (anim == null) anim = enemy.GetComponentInChildren<Animator>();
            if (anim == null) anim = enemy.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;

            var swordCtrl = enemy.GetComponent<InsaneGunSwordController>();
            if (swordCtrl == null) swordCtrl = enemy.AddComponent<InsaneGunSwordController>();
            swordCtrl.controlMode = InsaneGunSwordController.ControlMode.TrainingDummy;
            swordCtrl.targetEnemy = playerTarget;
            swordCtrl.enemyLayers = 1 << 8; // Target Player layer
            swordCtrl.maxHealth = 200f;
            swordCtrl.currentHealth = 200f;
            swordCtrl.walkSpeed = 2.2f;
            swordCtrl.attackDamage = 18f;
            swordCtrl.autoReviveOnDeath = true;
            swordCtrl.autoReviveDelay = 2.5f;

            var ghost = enemy.GetComponent<HolographicGhostEmitter>();
            if (ghost == null) ghost = enemy.AddComponent<HolographicGhostEmitter>();

            var ragdoll = enemy.GetComponent<EuphoriaRagdollController>();
            if (ragdoll == null) ragdoll = enemy.AddComponent<EuphoriaRagdollController>();
            ragdoll.ragdollForceMultiplier = 1.45f;
            ragdoll.autoRecoverDelay = 2.2f;
            ragdoll.autoRecover = true;
            ragdoll.SetupRagdollBones();

            EquipSidekickWeapons(enemy.transform, swordCtrl, anim, isPlayer: false);

            return enemy;
        }

        private static GameObject SpawnAlitaPlayer(RuntimeAnimatorController controller)
        {
            Vector3 spawnPos = new Vector3(0f, 0f, -3.0f);
            Quaternion spawnRot = Quaternion.identity;

            // Movie Authentic Alita: URM Berserker Nano Armor with Damascus Blade & War Stripes
            string alitaBerserkerPath = "Assets/Prefabs/Alita_Sidekick/Alita_03_Final_NanoBerserker.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(alitaBerserkerPath);
            if (prefab == null)
            {
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Alita_Sidekick/Alita_02_Normal_DollBody.prefab");
            }
            if (prefab == null)
            {
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/SidekickCharacters/Characters/HumanSpecies/HumanSpecies_01/HumanSpecies_01.prefab");
            }

            GameObject hero = prefab != null
                ? Object.Instantiate(prefab, spawnPos, spawnRot)
                : new GameObject("Alita_BattleAngel_Hero");

            hero.name = "Alita_BattleAngel_Hero";
            hero.tag = "Player";
            hero.layer = 8; // Layer 8 = Player

            foreach (Transform child in hero.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = 8;
            }

            var cc = hero.GetComponent<CharacterController>();
            if (cc == null) cc = hero.AddComponent<CharacterController>();
            cc.center = new Vector3(0f, 0.93f, 0f);
            cc.height = 1.80f;
            cc.radius = 0.35f;
            cc.skinWidth = 0.08f;
            cc.stepOffset = 0.35f;

            var anim = hero.GetComponent<Animator>();
            if (anim == null) anim = hero.GetComponentInChildren<Animator>();
            if (anim == null) anim = hero.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;

            var swordCtrl = hero.GetComponent<InsaneGunSwordController>();
            if (swordCtrl == null) swordCtrl = hero.AddComponent<InsaneGunSwordController>();
            swordCtrl.controlMode = InsaneGunSwordController.ControlMode.PlayerControlled;
            swordCtrl.enemyLayers = 1 << 9; // Target Enemy layer
            swordCtrl.maxHealth = 350f;
            swordCtrl.currentHealth = 350f;
            swordCtrl.attackDamage = 45f;
            swordCtrl.heavyAttackDamage = 95f;
            swordCtrl.walkSpeed = 3.0f;
            swordCtrl.runSpeed = 7.0f;
            swordCtrl.dodgeDistance = 3.2f;
            swordCtrl.enableSlowMotion = true;

            var ghost = hero.GetComponent<HolographicGhostEmitter>();
            if (ghost == null) ghost = hero.AddComponent<HolographicGhostEmitter>();

            var ragdoll = hero.GetComponent<EuphoriaRagdollController>();
            if (ragdoll == null) ragdoll = hero.AddComponent<EuphoriaRagdollController>();
            ragdoll.ragdollForceMultiplier = 1.35f;
            ragdoll.autoRecoverDelay = 2.2f;
            ragdoll.autoRecover = true;
            ragdoll.SetupRagdollBones();

            EquipSidekickWeapons(hero.transform, swordCtrl, anim, isPlayer: true);

            return hero;
        }

        private static GameObject SpawnBoyEnemy(RuntimeAnimatorController controller, Transform playerTarget)
        {
            Vector3 spawnPos = new Vector3(0f, 0f, 2.5f);
            Quaternion spawnRot = Quaternion.Euler(0f, 180f, 0f);

            // Synty Sidekick HumanSpecies_02 (Boy)
            string prefabPath = "Assets/Synty/SidekickCharacters/Characters/HumanSpecies/HumanSpecies_02/HumanSpecies_02.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            GameObject enemy = prefab != null
                ? Object.Instantiate(prefab, spawnPos, spawnRot)
                : new GameObject("Sidekick_Boy_Rival");

            enemy.name = "Sidekick_Boy_Rival";
            enemy.tag = "Enemy";
            enemy.layer = 9; // Layer 9 = Enemy

            foreach (Transform child in enemy.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = 9;
            }

            var cc = enemy.GetComponent<CharacterController>();
            if (cc == null) cc = enemy.AddComponent<CharacterController>();
            cc.center = new Vector3(0f, 0.95f, 0f);
            cc.height = 1.88f;
            cc.radius = 0.35f;
            cc.skinWidth = 0.08f;
            cc.stepOffset = 0.35f;

            var anim = enemy.GetComponent<Animator>();
            if (anim == null) anim = enemy.GetComponentInChildren<Animator>();
            if (anim == null) anim = enemy.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;

            var swordCtrl = enemy.GetComponent<InsaneGunSwordController>();
            if (swordCtrl == null) swordCtrl = enemy.AddComponent<InsaneGunSwordController>();
            // TrainingDummy mode by default so user can test combos cleanly! Toggleable to AI with [2]
            swordCtrl.controlMode = InsaneGunSwordController.ControlMode.TrainingDummy;
            swordCtrl.targetEnemy = playerTarget;
            swordCtrl.enemyLayers = 1 << 8; // Target Player layer
            swordCtrl.maxHealth = 1000f;
            swordCtrl.currentHealth = 1000f;
            swordCtrl.walkSpeed = 2.2f;
            swordCtrl.attackDamage = 18f;
            swordCtrl.autoReviveOnDeath = true;
            swordCtrl.autoReviveDelay = 2.5f;

            var ghost = enemy.GetComponent<HolographicGhostEmitter>();
            if (ghost == null) ghost = enemy.AddComponent<HolographicGhostEmitter>();

            var ragdoll = enemy.GetComponent<EuphoriaRagdollController>();
            if (ragdoll == null) ragdoll = enemy.AddComponent<EuphoriaRagdollController>();
            ragdoll.ragdollForceMultiplier = 1.45f;
            ragdoll.autoRecoverDelay = 2.2f;
            ragdoll.autoRecover = true;
            ragdoll.SetupRagdollBones();

            EquipSidekickWeapons(enemy.transform, swordCtrl, anim, isPlayer: false);

            return enemy;
        }

        private static void EquipSidekickWeapons(Transform charRoot, InsaneGunSwordController swordCtrl, Animator anim, bool isPlayer)
        {
            Transform rHand = anim != null ? anim.GetBoneTransform(HumanBodyBones.RightHand) : null;
            Transform lHand = anim != null ? anim.GetBoneTransform(HumanBodyBones.LeftHand)  : null;

            if (rHand == null) rHand = FindChildRecursive(charRoot, "hand_r") ?? FindChildRecursive(charRoot, "Hand_R") ?? charRoot;
            if (lHand == null) lHand = FindChildRecursive(charRoot, "hand_l") ?? FindChildRecursive(charRoot, "Hand_L") ?? charRoot;

            if (swordCtrl != null)
            {
                swordCtrl.rightHandSocket = rHand;
                swordCtrl.leftHandSocket = lHand;
            }

            // Sword in right hand (Alita Damascus Blade or Enemy Cyber Katana)
            GameObject katana = null;
            Transform existingBlade = rHand.Find("Damascus_Blade") ?? rHand.Find("Alita_Damascus_Blade") ?? rHand.Find("Player_Cyber_Katana");
            if (existingBlade != null)
            {
                katana = existingBlade.gameObject;
                katana.name = "Alita_Damascus_Blade";
            }
            else
            {
                string swordPrefabPath = isPlayer
                    ? "Assets/PolygonCyberCity/Prefabs/Weapons/SM_Wep_Sword_02.prefab" // Alita's Damascus Blade
                    : "Assets/PolygonCyberCity/Prefabs/Weapons/SM_Wep_Sword_01.prefab";
                GameObject swordAsset = AssetDatabase.LoadAssetAtPath<GameObject>(swordPrefabPath)
                                     ?? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonCyberCity/Prefabs/Weapons/SM_Wep_Sword_01.prefab");

                if (swordAsset != null)
                {
                    katana = Object.Instantiate(swordAsset, rHand, false);
                    katana.name = isPlayer ? "Alita_Damascus_Blade" : "Enemy_Cyber_Katana";
                    katana.transform.localPosition = isPlayer
                        ? new Vector3(0.11780f, 0.03410f, -0.00920f)
                        : new Vector3(0.04f, 0.05f, 0.01f);
                    katana.transform.localRotation = isPlayer
                        ? Quaternion.Euler(341.39560f, 99.66290f, 276.94530f)
                        : Quaternion.Euler(0f, 180f, 90f);
                    if (isPlayer) katana.transform.localScale = new Vector3(0.95f, 0.95f, 0.95f);
                }
                else
                {
                    katana = new GameObject(isPlayer ? "Alita_Damascus_Blade" : "Enemy_Cyber_Katana");
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
            Color glowColor = isPlayer ? new Color(0f, 0.95f, 1f) : new Color(1f, 0.2f, 0.7f);
            bladeMat.color = glowColor * 0.9f;
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

            var grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(glowColor, 0.0f),
                    new GradientColorKey(isPlayer ? new Color(0.6f, 0.15f, 1f) : new Color(1f, 0.6f, 0.1f), 0.65f),
                    new GradientColorKey(Color.white, 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.95f, 0.0f),
                    new GradientAlphaKey(0.65f, 0.6f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            trail.colorGradient = grad;
            if (swordCtrl != null) swordCtrl.swordTrail = trail;

            // Hip Sheath Socket on character pelvis/hips
            Transform hip = anim != null ? anim.GetBoneTransform(HumanBodyBones.Hips) : null;
            if (hip == null) hip = FindChildRecursive(charRoot, "pelvis") ?? FindChildRecursive(charRoot, "Pelvis") ?? FindChildRecursive(charRoot, "Hips");

            Transform hipSocket = null;
            if (hip != null)
            {
                hipSocket = hip.Find("Hip_Sheath_Socket");
                if (hipSocket == null)
                {
                    var hsGo = new GameObject("Hip_Sheath_Socket");
                    hsGo.transform.SetParent(hip, false);
                    hsGo.transform.localPosition = new Vector3(-0.16f, -0.05f, 0.05f);
                    hsGo.transform.localRotation = Quaternion.Euler(20f, 10f, 115f);
                    hipSocket = hsGo.transform;
                }
            }

            if (swordCtrl != null)
            {
                swordCtrl.hipSocket = hipSocket;
                swordCtrl.swordInstance = katana != null ? katana.transform : null;
                swordCtrl.isSwordDrawn = true;
                if (katana != null)
                {
                    swordCtrl.swordHandLocalPos = katana.transform.localPosition;
                    swordCtrl.swordHandLocalRot = katana.transform.localEulerAngles;
                }
            }

            // Gun in left hand (for Alita / player)
            if (isPlayer && lHand != null)
            {
                Transform existingGun = lHand.Find("Sidearm_Blaster");
                if (existingGun == null)
                {
                    string pistolPath = "Assets/PolygonCyberCity/Prefabs/Weapons/SM_Wep_Pistol_01.prefab";
                    GameObject pistolAsset = AssetDatabase.LoadAssetAtPath<GameObject>(pistolPath);
                    if (pistolAsset != null)
                    {
                        var pistol = Object.Instantiate(pistolAsset, lHand, false);
                        pistol.name = "Sidearm_Blaster";
                        pistol.transform.localPosition = new Vector3(-0.02f, 0.06f, 0.01f);
                        pistol.transform.localRotation = Quaternion.Euler(0f, -90f, 90f);
                    }
                }
            }
        }

        private static Transform FindChildRecursive(Transform parent, string childName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == childName) return child;
                var found = FindChildRecursive(child, childName);
                if (found != null) return found;
            }
            return null;
        }

        [MenuItem("Tools/Alita Battle Angel/Equip Alita on City Player (Demo_City)", priority = 10)]
        public static void EquipAlitaOnCityPlayer()
        {
            var player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                Debug.LogError("[Alita] Player GameObject not found in current scene!");
                return;
            }

            var dummySmr = player.transform.Find("PolygonSyntyCharacter/SK_DUMMY_POLYGON_01")
                        ?? player.transform.Find("PolygonSyntyCharacter/Alita_Character_Mesh");
            if (dummySmr != null)
            {
                var alitaMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Meshes/SM_Alita_HumanCustom.asset");
                var alitaMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Prefabs/Alita_Sidekick/Materials/M_Alita_Berserker.mat")
                            ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/Prefabs/Alita_Sidekick/Materials/M_Alita_HumanCustom.mat");
                var smr = dummySmr.GetComponent<SkinnedMeshRenderer>();
                if (smr != null)
                {
                    if (alitaMesh != null) smr.sharedMesh = alitaMesh;
                    if (alitaMat != null) smr.sharedMaterial = alitaMat;
                    dummySmr.name = "Alita_Character_Mesh";
                    Debug.Log("<color=#00F0FF>[Alita] Successfully equipped Alita character model on City Player!</color>");
                }
            }
        }

        private static void SetupCamera(Transform target)
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
            follow.targetOffset = new Vector3(0f, 1.35f, 0f);
            follow.cameraOffset = new Vector3(0f, 0.35f, -3.6f);

            camGo.transform.position = target.position - (target.forward * 3.6f) + Vector3.up * 1.7f;
            camGo.transform.rotation = Quaternion.Euler(14f, 0f, 0f);
        }

        private static void SetupTestManager(GameObject player, GameObject enemy)
        {
            var mgrGo = new GameObject("Combat_Test_Manager");
            var mgr = mgrGo.AddComponent<InsaneGunCombatTestManager>();
            mgr.player = player.GetComponent<InsaneGunSwordController>();
            mgr.enemy = enemy.GetComponent<InsaneGunSwordController>();
            mgr.enemySpawnPos = enemy.transform.position;
            mgr.enemySpawnRot = enemy.transform.rotation;
        }
    }
}
#endif
