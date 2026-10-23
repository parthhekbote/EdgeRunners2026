#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace PolygonCyberCity.NPC.Editor
{
    public static class NPCAnimatorControllerBuilder
    {
        private const string ControllerPath = "Assets/PolygonCyberCity/Animations/AC_CyberNPC.controller";

        [MenuItem("CyberCity/NPC Animation/Build 6-Layer NPC Animator Controller", false, 10)]
        [MenuItem("Tools/CyberCity/NPC Animation/Build 6-Layer NPC Animator Controller", false, 10)]
        public static void BuildController()
        {
            if (!AssetDatabase.IsValidFolder("Assets/PolygonCyberCity/Animations"))
            {
                AssetDatabase.CreateFolder("Assets/PolygonCyberCity", "Animations");
            }

            // Find Synty clip data for motions
            SyntyCombined.SyntyAnimClipData clipData = null;
            string[] guids = AssetDatabase.FindAssets("t:SyntyAnimClipData");
            if (guids.Length > 0)
            {
                string clipDataPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                clipData = AssetDatabase.LoadAssetAtPath<SyntyCombined.SyntyAnimClipData>(clipDataPath);
            }

            // Find rich idle clips from Synty AnimationIdles
            // Find rich idle clips from Synty AnimationIdles (29 specialized animation states)
            AnimationClip idleBase = LoadClipByName("A_POLY_IDL_Base_Masc") ?? clipData?.idleStanding;
            AnimationClip idlePhoneScroll = LoadClipByName("A_POLY_IDL_UsePhone_OneHand_Scrolling_Masc");
            AnimationClip idlePhoneCall = LoadClipByName("A_POLY_IDL_UsePhone_OneHand_Call_Loop_Masc");
            AnimationClip idleArmsFolded = LoadClipByName("A_POLY_IDL_ArmsFolded_Casual_Loop_Masc");
            AnimationClip idleWeightShift = LoadClipByName("A_POLY_IDL_WeightShift_L_Masc");
            AnimationClip idleCheckWatch = LoadClipByName("A_POLY_IDL_CheckWatch_Masc");
            AnimationClip idleThoughtful = LoadClipByName("A_POLY_IDL_Thoughtful_L_ChinScratch_Masc");
            AnimationClip idleHandsOnHips = LoadClipByName("A_POLY_IDL_HandsOnHips_Base_Loop_Masc");
            AnimationClip idleBoredTap = LoadClipByName("A_POLY_IDL_Bored_FootTap_Masc");
            AnimationClip idlePhoneSelfie = LoadClipByName("A_POLY_IDL_UsePhone_OneHand_Selfie_Loop_Masc");
            AnimationClip idlePhoneVideo = LoadClipByName("A_POLY_IDL_UsePhone_OneHand_Video_Loop_Masc");
            AnimationClip idlePhoneType = LoadClipByName("A_POLY_IDL_UsePhone_TwoHands_Typing_Masc");
            AnimationClip idleArmsFoldedGrumpy = LoadClipByName("A_POLY_IDL_ArmsFolded_Grumpy_Loop_Masc");
            AnimationClip idleLeanBack = LoadClipByName("A_POLY_IDL_Lean_B_Base_Loop_Masc");
            AnimationClip idleLeanArmsFolded = LoadClipByName("A_POLY_IDL_Lean_B_ArmsFolded_Loop_Masc");
            AnimationClip idleLeanLegUp = LoadClipByName("A_POLY_IDL_Lean_B_ArmsFoldedLegUp_Loop_Masc");
            AnimationClip idleBoredKick = LoadClipByName("A_POLY_IDL_Bored_KickGround_Masc");
            AnimationClip idleBoredSlump = LoadClipByName("A_POLY_IDL_Bored_SlumpBack_Masc");
            AnimationClip idleStretchArms = LoadClipByName("A_POLY_IDL_Stretch_Arms_Masc");
            AnimationClip idleStretchShoulders = LoadClipByName("A_POLY_IDL_Stretch_Shoulders_Masc");
            AnimationClip idleYawn = LoadClipByName("A_POLY_IDL_Yawn_Masc");
            AnimationClip idleLookSky = LoadClipByName("A_POLY_IDL_Look_Up_Masc");
            AnimationClip idleLookBehind = LoadClipByName("A_POLY_IDL_Look_L_Behind_Masc");
            AnimationClip idleWave = LoadClipByName("A_POLY_IDL_Wave_Small_Loop_Masc");
            AnimationClip idleHairComb = LoadClipByName("A_POLY_IDL_PlayWithHair_CombBack_Masc");
            AnimationClip idleHeadNod = LoadClipByName("A_POLY_IDL_HeadNod_Greet_Masc");
            AnimationClip idleDrinkHold = LoadClipByName("A_POLY_IDL_Drink_Hold_Loop_Masc");
            AnimationClip idleEatHold = LoadClipByName("A_POLY_IDL_Eat_Hold_Loop_Masc");
            AnimationClip idlePostureAggressive = LoadClipByName("A_POLY_IDL_Posture_Aggressive_Loop_Masc");
            AnimationClip idlePostureSlumped = LoadClipByName("A_POLY_IDL_Posture_Slumped_Loop_Masc");

            // Locomotion clips
            AnimationClip walkClip = LoadClipByName("A_Walk_F_Masc") ?? clipData?.walkForward;
            AnimationClip runClip = clipData?.runForward;
            AnimationClip sprintClip = clipData?.sprintForward;

            // Gesture / Activity clips
            AnimationClip gestureClip = LoadClipByName("A_POLY_IDL_PointHand_Index_F_Masc");
            AnimationClip activityDrinkClip = LoadClipByName("A_POLY_IDL_Drink_Hold_Loop_Masc");

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            // Add Parameters
            AddFloatParam(controller, "MoveSpeed");
            AddFloatParam(controller, "Speed");
            AddIntParam(controller, "CurrentGait");
            AddFloatParam(controller, "StrafeDirectionX");
            AddFloatParam(controller, "StrafeDirectionZ");
            AddFloatParam(controller, "TurnAmount");
            AddBoolParam(controller, "IsWalking");
            AddBoolParam(controller, "IsRunning");
            AddBoolParam(controller, "IsStopped");
            AddBoolParam(controller, "IsCrouching");
            AddBoolParam(controller, "IsGrounded");
            AddBoolParam(controller, "IsStarting");
            AddBoolParam(controller, "MovementInputHeld");

            AddBoolParam(controller, "SwordDrawn");
            AddFloatParam(controller, "SwordDrawnWeight");
            AddIntParam(controller, "AttackID");
            AddTriggerParam(controller, "Attack");
            AddIntParam(controller, "CombatState");
            AddBoolParam(controller, "IsStrafing");
            AddTriggerParam(controller, "BlockBegin");
            AddTriggerParam(controller, "BlockLoop");
            AddTriggerParam(controller, "BlockEnd");

            AddIntParam(controller, "HitDirection");
            AddIntParam(controller, "HitType");
            AddTriggerParam(controller, "Hit");
            AddTriggerParam(controller, "Die");
            AddIntParam(controller, "DodgeDirection");
            AddTriggerParam(controller, "Dodge");
            AddTriggerParam(controller, "Knockdown");

            AddIntParam(controller, "PerceptionType");
            AddTriggerParam(controller, "PerceptionReact");
            AddBoolParam(controller, "IsPanicking");
            AddTriggerParam(controller, "PanicLookBack");

            AddIntParam(controller, "ActivityID");
            AddTriggerParam(controller, "Activity");
            AddIntParam(controller, "GestureID");
            AddTriggerParam(controller, "Gesture");
            AddIntParam(controller, "RandomIdle");

            // ─────────────────────────────────────────────────────────────
            // Configure Layer 0: Base Locomotion & Complete Idle Architecture
            // ─────────────────────────────────────────────────────────────
            AnimatorControllerLayer baseLayer = controller.layers[0];
            baseLayer.name = "Base Locomotion";
            baseLayer.defaultWeight = 1.0f;
            AnimatorStateMachine baseSM = baseLayer.stateMachine;

            // 1. Locomotion Blend Tree (MoveSpeed: 0=Idle, 1.35=Walk, 3.5=Run, 6.0=Sprint)
            BlendTree locomotionBT;
            AnimatorState locomotionState = controller.CreateBlendTreeInController("Locomotion", out locomotionBT, 0);
            locomotionBT.blendType = BlendTreeType.Simple1D;
            locomotionBT.blendParameter = "MoveSpeed";
            locomotionBT.useAutomaticThresholds = false;
            locomotionState.writeDefaultValues = true;

            var btChildren = new System.Collections.Generic.List<ChildMotion>();
            if (idleBase != null) btChildren.Add(new ChildMotion { motion = idleBase, threshold = 0f, timeScale = 1f });
            if (walkClip != null) btChildren.Add(new ChildMotion { motion = walkClip, threshold = 1.35f, timeScale = 1f });
            if (runClip != null) btChildren.Add(new ChildMotion { motion = runClip, threshold = 3.5f, timeScale = 1f });
            if (sprintClip != null) btChildren.Add(new ChildMotion { motion = sprintClip, threshold = 6.0f, timeScale = 1f });
            locomotionBT.children = btChildren.ToArray();
            locomotionBT.useAutomaticThresholds = false;
            var children = locomotionBT.children;
            if (children.Length > 0) children[0].threshold = 0f;
            if (children.Length > 1) children[1].threshold = 1.35f;
            if (children.Length > 2) children[2].threshold = 3.5f;
            if (children.Length > 3) children[3].threshold = 6.0f;
            locomotionBT.children = children;

            // 2. Idle State Machine with Dynamic Behavioral Variants
            AnimatorState idleBaseState = baseSM.AddState("Idle_Base", new Vector3(300, -100, 0));
            idleBaseState.motion = idleBase;
            baseSM.defaultState = idleBaseState;

            // Transition: Idle_Base <-> Locomotion
            var toLocomotion = idleBaseState.AddTransition(locomotionState);
            toLocomotion.AddCondition(AnimatorConditionMode.If, 0, "IsWalking");
            toLocomotion.duration = 0.20f;
            toLocomotion.hasExitTime = false;

            var fromLocomotion = locomotionState.AddTransition(idleBaseState);
            fromLocomotion.AddCondition(AnimatorConditionMode.IfNot, 0, "IsWalking");
            fromLocomotion.duration = 0.25f;
            fromLocomotion.hasExitTime = false;

            // Full Suite of 29 Dynamic Behavioral Idle Variants
            CreateIdleVariant(baseSM, locomotionState, idleBaseState, "Idle_Phone_Scroll", idlePhoneScroll, 1, new Vector3(550, -400, 0), false);
            CreateIdleVariant(baseSM, locomotionState, idleBaseState, "Idle_Phone_Call", idlePhoneCall, 2, new Vector3(550, -370, 0), false);
            CreateIdleVariant(baseSM, locomotionState, idleBaseState, "Idle_ArmsFolded", idleArmsFolded, 3, new Vector3(550, -340, 0), false);
            CreateIdleVariant(baseSM, locomotionState, idleBaseState, "Idle_WeightShift", idleWeightShift, 4, new Vector3(550, -310, 0), true);
            CreateIdleVariant(baseSM, locomotionState, idleBaseState, "Idle_CheckWatch", idleCheckWatch, 5, new Vector3(550, -280, 0), true);
            CreateIdleVariant(baseSM, locomotionState, idleBaseState, "Idle_Thoughtful", idleThoughtful, 6, new Vector3(550, -250, 0), true);
            CreateIdleVariant(baseSM, locomotionState, idleBaseState, "Idle_HandsOnHips", idleHandsOnHips, 7, new Vector3(550, -220, 0), false);
            CreateIdleVariant(baseSM, locomotionState, idleBaseState, "Idle_Bored_Tap", idleBoredTap, 8, new Vector3(550, -190, 0), true);
            CreateIdleVariant(baseSM, locomotionState, idleBaseState, "Idle_Phone_Selfie", idlePhoneSelfie, 9, new Vector3(550, -160, 0), false);
            CreateIdleVariant(baseSM, locomotionState, idleBaseState, "Idle_Phone_Video", idlePhoneVideo, 10, new Vector3(550, -130, 0), false);
            CreateIdleVariant(baseSM, locomotionState, idleBaseState, "Idle_Phone_Type", idlePhoneType, 11, new Vector3(550, -100, 0), true);
            CreateIdleVariant(baseSM, locomotionState, idleBaseState, "Idle_ArmsFolded_Grumpy", idleArmsFoldedGrumpy, 12, new Vector3(550, -70, 0), false);
            CreateIdleVariant(baseSM, locomotionState, idleBaseState, "Idle_Lean_Back", idleLeanBack, 13, new Vector3(550, -40, 0), false);
            CreateIdleVariant(baseSM, locomotionState, idleBaseState, "Idle_Lean_ArmsFolded", idleLeanArmsFolded, 14, new Vector3(550, -10, 0), false);
            CreateIdleVariant(baseSM, locomotionState, idleBaseState, "Idle_Lean_LegUp", idleLeanLegUp, 15, new Vector3(550, 20, 0), false);
            CreateIdleVariant(baseSM, locomotionState, idleBaseState, "Idle_Bored_Kick", idleBoredKick, 16, new Vector3(550, 50, 0), true);
            CreateIdleVariant(baseSM, locomotionState, idleBaseState, "Idle_Bored_Slump", idleBoredSlump, 17, new Vector3(550, 80, 0), true);
            CreateIdleVariant(baseSM, locomotionState, idleBaseState, "Idle_Stretch_Arms", idleStretchArms, 18, new Vector3(550, 110, 0), true);
            CreateIdleVariant(baseSM, locomotionState, idleBaseState, "Idle_Stretch_Shoulders", idleStretchShoulders, 19, new Vector3(550, 140, 0), true);
            CreateIdleVariant(baseSM, locomotionState, idleBaseState, "Idle_Yawn", idleYawn, 20, new Vector3(550, 170, 0), true);
            CreateIdleVariant(baseSM, locomotionState, idleBaseState, "Idle_Look_Sky", idleLookSky, 21, new Vector3(550, 200, 0), true);
            CreateIdleVariant(baseSM, locomotionState, idleBaseState, "Idle_Look_Behind", idleLookBehind, 22, new Vector3(550, 230, 0), true);
            CreateIdleVariant(baseSM, locomotionState, idleBaseState, "Idle_Wave", idleWave, 23, new Vector3(550, 260, 0), true);
            CreateIdleVariant(baseSM, locomotionState, idleBaseState, "Idle_Hair_Comb", idleHairComb, 24, new Vector3(550, 290, 0), true);
            CreateIdleVariant(baseSM, locomotionState, idleBaseState, "Idle_HeadNod", idleHeadNod, 25, new Vector3(550, 320, 0), true);
            CreateIdleVariant(baseSM, locomotionState, idleBaseState, "Idle_Drink_Hold", idleDrinkHold, 26, new Vector3(550, 350, 0), false);
            CreateIdleVariant(baseSM, locomotionState, idleBaseState, "Idle_Eat_Hold", idleEatHold, 27, new Vector3(550, 380, 0), false);
            CreateIdleVariant(baseSM, locomotionState, idleBaseState, "Idle_Posture_Aggressive", idlePostureAggressive, 28, new Vector3(550, 410, 0), false);
            CreateIdleVariant(baseSM, locomotionState, idleBaseState, "Idle_Posture_Slumped", idlePostureSlumped, 29, new Vector3(550, 440, 0), false);

            // Cower State (Crouching idle when cornered/panicking)
            AnimatorState cowerState = baseSM.AddState("Cower", new Vector3(300, 250, 0));
            if (clipData != null && clipData.idleCrouching != null) cowerState.motion = clipData.idleCrouching;

            var toCower = locomotionState.AddTransition(cowerState);
            toCower.AddCondition(AnimatorConditionMode.If, 0, "IsCrouching");
            toCower.duration = 0.15f;
            toCower.hasExitTime = false;

            var exitCower = cowerState.AddTransition(locomotionState);
            exitCower.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrouching");
            exitCower.duration = 0.15f;
            exitCower.hasExitTime = false;

            var idleToCower = idleBaseState.AddTransition(cowerState);
            idleToCower.AddCondition(AnimatorConditionMode.If, 0, "IsCrouching");
            idleToCower.duration = 0.15f;
            idleToCower.hasExitTime = false;

            // Hit React State on Layer 0
            AnimatorState hitState = baseSM.AddState("HitReact", new Vector3(500, 320, 0));
            if (clipData != null && clipData.hitReactBack.clip != null) hitState.motion = clipData.hitReactBack.clip;

            var anyHit = baseSM.AddAnyStateTransition(hitState);
            anyHit.AddCondition(AnimatorConditionMode.If, 0, "Hit");
            anyHit.duration = 0.08f;
            anyHit.canTransitionToSelf = false;

            var exitHit = hitState.AddTransition(locomotionState);
            exitHit.hasExitTime = true;
            exitHit.exitTime = 0.85f;
            exitHit.duration = 0.15f;

            // Knockdown State on Layer 0
            AnimatorState kdState = baseSM.AddState("Knockdown", new Vector3(500, 390, 0));
            if (clipData != null && clipData.knockDownBegin != null) kdState.motion = clipData.knockDownBegin;

            var anyKd = baseSM.AddAnyStateTransition(kdState);
            anyKd.AddCondition(AnimatorConditionMode.If, 0, "Knockdown");
            anyKd.duration = 0.1f;
            anyKd.canTransitionToSelf = false;

            var exitKd = kdState.AddTransition(locomotionState);
            exitKd.hasExitTime = true;
            exitKd.exitTime = 0.9f;
            exitKd.duration = 0.15f;

            // Death State on Layer 0
            AnimatorState deathState = baseSM.AddState("Death", new Vector3(500, 460, 0));
            if (clipData != null && clipData.deathBack != null) deathState.motion = clipData.deathBack;

            var anyDeath = baseSM.AddAnyStateTransition(deathState);
            anyDeath.AddCondition(AnimatorConditionMode.If, 0, "Die");
            anyDeath.duration = 0.08f;
            anyDeath.canTransitionToSelf = false;

            // Directional Death States for direct PlayDeathClipViaOverride / TryPlayState lookup
            AddNamedDeathState(baseSM, "A_Death_F_01_Sword", clipData?.deathForward, new Vector3(750, 320, 0));
            AddNamedDeathState(baseSM, "A_Death_B_01_Sword", clipData?.deathBack, new Vector3(750, 370, 0));
            AddNamedDeathState(baseSM, "A_Death_L_01_Sword", clipData?.deathLeft, new Vector3(750, 420, 0));
            AddNamedDeathState(baseSM, "A_Death_R_01_Sword", clipData?.deathRight, new Vector3(750, 470, 0));

            // ─────────────────────────────────────────────────────────────
            // Configure Layer 1: Upper Body (Gestures & Activities)
            // ─────────────────────────────────────────────────────────────
            controller.AddLayer("Upper Body");
            AnimatorControllerLayer upperLayer = controller.layers[1];
            upperLayer.defaultWeight = 0.0f;
            AnimatorStateMachine upperSM = upperLayer.stateMachine;
            AnimatorState emptyUpper = upperSM.AddState("Empty", new Vector3(250, 0, 0));
            upperSM.defaultState = emptyUpper;

            AnimatorState gestureState = upperSM.AddState("Gesture", new Vector3(250, 100, 0));
            if (gestureClip != null) gestureState.motion = gestureClip;
            var anyGesture = upperSM.AddAnyStateTransition(gestureState);
            anyGesture.AddCondition(AnimatorConditionMode.If, 0, "Gesture");
            anyGesture.duration = 0.15f;
            var exitGesture = gestureState.AddTransition(emptyUpper);
            exitGesture.hasExitTime = true;
            exitGesture.exitTime = 0.85f;
            exitGesture.duration = 0.15f;

            AnimatorState activityState = upperSM.AddState("Activity", new Vector3(250, 200, 0));
            if (activityDrinkClip != null) activityState.motion = activityDrinkClip;
            var anyActivity = upperSM.AddAnyStateTransition(activityState);
            anyActivity.AddCondition(AnimatorConditionMode.If, 0, "Activity");
            anyActivity.duration = 0.15f;
            var exitActivity = activityState.AddTransition(emptyUpper);
            exitActivity.hasExitTime = true;
            exitActivity.exitTime = 0.9f;
            exitActivity.duration = 0.15f;

            // ─────────────────────────────────────────────────────────────
            // Configure Layer 2: Combat
            // ─────────────────────────────────────────────────────────────
            controller.AddLayer("Combat");
            AnimatorControllerLayer combatLayer = controller.layers[2];
            combatLayer.defaultWeight = 0.0f;
            AnimatorStateMachine combatSM = combatLayer.stateMachine;
            AnimatorState combatIdle = combatSM.AddState("CombatIdle", new Vector3(250, 0, 0));
            if (clipData != null && clipData.idleStanding != null) combatIdle.motion = clipData.idleStanding;
            combatSM.defaultState = combatIdle;

            AnimatorState attackState = combatSM.AddState("Attack", new Vector3(250, 100, 0));
            var anyAttack = combatSM.AddAnyStateTransition(attackState);
            anyAttack.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            anyAttack.duration = 0.08f;
            var exitAttack = attackState.AddTransition(combatIdle);
            exitAttack.hasExitTime = true;
            exitAttack.exitTime = 0.85f;
            exitAttack.duration = 0.12f;

            AnimatorState blockState = combatSM.AddState("Block", new Vector3(250, 200, 0));
            if (clipData != null && clipData.parryReturnToBlock != null) blockState.motion = clipData.parryReturnToBlock;
            var anyBlock = combatSM.AddAnyStateTransition(blockState);
            anyBlock.AddCondition(AnimatorConditionMode.If, 0, "BlockBegin");
            anyBlock.duration = 0.1f;
            var exitBlock = blockState.AddTransition(combatIdle);
            exitBlock.AddCondition(AnimatorConditionMode.If, 0, "BlockEnd");
            exitBlock.duration = 0.15f;

            // ─────────────────────────────────────────────────────────────
            // Configure Layer 3: Reaction
            // ─────────────────────────────────────────────────────────────
            controller.AddLayer("Reaction");
            AnimatorControllerLayer reactLayer = controller.layers[3];
            reactLayer.defaultWeight = 0.0f;
            AnimatorStateMachine reactSM = reactLayer.stateMachine;
            AnimatorState reactEmpty = reactSM.AddState("Empty", new Vector3(250, 0, 0));
            reactSM.defaultState = reactEmpty;

            AnimatorState hitReactLayer3 = reactSM.AddState("HitReact", new Vector3(250, 100, 0));
            if (clipData != null && clipData.hitReactBack.clip != null) hitReactLayer3.motion = clipData.hitReactBack.clip;
            var anyHitL3 = reactSM.AddAnyStateTransition(hitReactLayer3);
            anyHitL3.AddCondition(AnimatorConditionMode.If, 0, "Hit");
            anyHitL3.duration = 0.08f;
            var exitHitL3 = hitReactLayer3.AddTransition(reactEmpty);
            exitHitL3.hasExitTime = true;
            exitHitL3.exitTime = 0.85f;
            exitHitL3.duration = 0.15f;

            AnimatorState perceptionState = reactSM.AddState("PerceptionReact", new Vector3(250, 200, 0));
            var anyPerception = reactSM.AddAnyStateTransition(perceptionState);
            anyPerception.AddCondition(AnimatorConditionMode.If, 0, "PerceptionReact");
            anyPerception.duration = 0.12f;
            var exitPerception = perceptionState.AddTransition(reactEmpty);
            exitPerception.hasExitTime = true;
            exitPerception.exitTime = 0.85f;
            exitPerception.duration = 0.15f;

            // ─────────────────────────────────────────────────────────────
            // Configure Layer 4: Full Body Override
            // ─────────────────────────────────────────────────────────────
            controller.AddLayer("Full Body Override");
            AnimatorControllerLayer overrideLayer = controller.layers[4];
            overrideLayer.defaultWeight = 0.0f;
            AnimatorStateMachine overrideSM = overrideLayer.stateMachine;
            AnimatorState overrideEmpty = overrideSM.AddState("Empty", new Vector3(250, 0, 0));
            overrideSM.defaultState = overrideEmpty;

            AnimatorState dodgeState = overrideSM.AddState("Dodge", new Vector3(250, 100, 0));
            if (clipData != null && clipData.dodgeRollBack.clip != null) dodgeState.motion = clipData.dodgeRollBack.clip;
            var anyDodge = overrideSM.AddAnyStateTransition(dodgeState);
            anyDodge.AddCondition(AnimatorConditionMode.If, 0, "Dodge");
            anyDodge.duration = 0.1f;
            var exitDodge = dodgeState.AddTransition(overrideEmpty);
            exitDodge.hasExitTime = true;
            exitDodge.exitTime = 0.85f;
            exitDodge.duration = 0.15f;

            AnimatorState knockdownL4 = overrideSM.AddState("Knockdown", new Vector3(250, 200, 0));
            if (clipData != null && clipData.knockDownBegin != null) knockdownL4.motion = clipData.knockDownBegin;
            var anyKdL4 = overrideSM.AddAnyStateTransition(knockdownL4);
            anyKdL4.AddCondition(AnimatorConditionMode.If, 0, "Knockdown");
            anyKdL4.duration = 0.1f;
            var exitKdL4 = knockdownL4.AddTransition(overrideEmpty);
            exitKdL4.hasExitTime = true;
            exitKdL4.exitTime = 0.9f;
            exitKdL4.duration = 0.15f;

            AnimatorState deathL4 = overrideSM.AddState("Death", new Vector3(250, 300, 0));
            if (clipData != null && clipData.deathBack != null) deathL4.motion = clipData.deathBack;
            var deathTransition = overrideSM.AddAnyStateTransition(deathL4);
            deathTransition.AddCondition(AnimatorConditionMode.If, 0, "Die");
            deathTransition.duration = 0.08f;
            deathTransition.canTransitionToSelf = false;

            // ─────────────────────────────────────────────────────────────
            // Configure Layer 5: IK
            // ─────────────────────────────────────────────────────────────
            controller.AddLayer("IK");
            AnimatorControllerLayer ikLayer = controller.layers[5];
            ikLayer.defaultWeight = 1.0f;
            ikLayer.iKPass = true;
            AnimatorStateMachine ikSM = ikLayer.stateMachine;
            ikSM.AddState("IK_Pass", new Vector3(250, 0, 0));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[NPCAnimatorControllerBuilder] Successfully built AAA 6-layer NPC Animator Controller with full dynamic idle suite at: '{ControllerPath}'");
        }

        private static void CreateIdleVariant(
            AnimatorStateMachine sm, 
            AnimatorState locomotionState, 
            AnimatorState idleBaseState, 
            string stateName, 
            AnimationClip clip, 
            int variantId, 
            Vector3 position,
            bool isOneShotAction)
        {
            if (clip == null) return;

            AnimatorState varState = sm.AddState(stateName, position);
            varState.motion = clip;

            // Transition: Idle_Base -> Variant State when RandomIdle == variantId
            var toVar = idleBaseState.AddTransition(varState);
            toVar.AddCondition(AnimatorConditionMode.Equals, variantId, "RandomIdle");
            toVar.duration = 0.35f;
            toVar.hasExitTime = false;

            // Transition: Variant State -> Idle_Base when RandomIdle changes away from this variant
            var toBase = varState.AddTransition(idleBaseState);
            toBase.AddCondition(AnimatorConditionMode.NotEqual, variantId, "RandomIdle");
            toBase.duration = 0.35f;
            toBase.hasExitTime = false;

            // Interrupt to Locomotion immediately if walking starts!
            var toLoco = varState.AddTransition(locomotionState);
            toLoco.AddCondition(AnimatorConditionMode.If, 0, "IsWalking");
            toLoco.duration = 0.20f;
            toLoco.hasExitTime = false;
        }

        private static AnimationClip LoadClipByName(string clipName)
        {
            string[] guids = AssetDatabase.FindAssets(clipName);
            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                if (path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                {
                    var all = AssetDatabase.LoadAllAssetsAtPath(path);
                    foreach (var a in all)
                    {
                        if (a is AnimationClip c && !c.name.StartsWith("__preview__"))
                        {
                            return c;
                        }
                    }
                }
            }
            return null;
        }

        private static void AddNamedDeathState(AnimatorStateMachine sm, string stateName, Motion motion, Vector3 pos)
        {
            var st = sm.AddState(stateName, pos);
            if (motion != null) st.motion = motion;
            var trans = sm.AddAnyStateTransition(st);
            trans.AddCondition(AnimatorConditionMode.If, 0, "Die");
            trans.duration = 0.08f;
            trans.canTransitionToSelf = false;
        }

        private static void AddFloatParam(AnimatorController ac, string name) => ac.AddParameter(name, AnimatorControllerParameterType.Float);
        private static void AddIntParam(AnimatorController ac, string name) => ac.AddParameter(name, AnimatorControllerParameterType.Int);
        private static void AddBoolParam(AnimatorController ac, string name) => ac.AddParameter(name, AnimatorControllerParameterType.Bool);
        private static void AddTriggerParam(AnimatorController ac, string name) => ac.AddParameter(name, AnimatorControllerParameterType.Trigger);
    }
}
#endif
