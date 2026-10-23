#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

namespace SyntyCombined.Editor
{
    // ─────────────────────────────────────────────────────────────────────────────
    // SyntyCombinedControllerBuilder.cs  (Editor-only)
    //
    // Automatically builds the complete 4-layer Animator Controller based directly on
    // Synty's official "Sword Combat User Guide.pdf" (pages 18–20):
    //
    //   Layer 0  –  Locomotion     (Base full-body blend trees, crouch, in-air, idle blend)
    //   Layer 1  –  SwordArm       (Mask_Arm_R, Override, weight ~0.70 while moving)
    //   Layer 2  –  SwordHand      (Mask_Hand_R, Override, weight 1.00 for locked grip)
    //   Layer 3  –  Combat_Actions (Full-body override for attacks, dodges, parries, reactions)
    //
    // Run via:  Tools → SyntyCombined → Build Animator Controller
    // ─────────────────────────────────────────────────────────────────────────────

    public static class SyntyCombinedControllerBuilder
    {
        private const string OutputPath   = "Assets/SyntyCombined/AC_SyntyCombined.controller";
        private const string DataPath     = "Assets/SyntyCombined/SyntyAnimClipData.asset";
        private const string ArmMaskPath  = "Assets/Synty/AnimationSwordCombat/Meshes/Mask_Arm_R.mask";
        private const string HandMaskPath = "Assets/Synty/AnimationSwordCombat/Meshes/Mask_Hand_R.mask";

        [MenuItem("Tools/SyntyCombined/Build Animator Controller")]
        public static void Build()
        {
            // ── Load Clip Data ────────────────────────────────────────────────────
            var clips = AssetDatabase.LoadAssetAtPath<SyntyAnimClipData>(DataPath);
            if (clips == null)
            {
                Debug.LogError($"[SyntyCombined] Could not find SyntyAnimClipData at {DataPath}!");
                return;
            }

            var armMask = AssetDatabase.LoadAssetAtPath<AvatarMask>(ArmMaskPath);
            if (armMask == null)
                Debug.LogWarning($"[SyntyCombined] Mask_Arm_R not found at {ArmMaskPath}!");

            var handMask = AssetDatabase.LoadAssetAtPath<AvatarMask>(HandMaskPath);
            if (handMask == null)
                Debug.LogWarning($"[SyntyCombined] Mask_Hand_R not found at {HandMaskPath}!");

            // ── Ensure output directory exists ────────────────────────────────────
            string dir = Path.GetDirectoryName(OutputPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            // ── Create or overwrite the controller ────────────────────────────────
            var controller = AnimatorController.CreateAnimatorControllerAtPath(OutputPath);
            controller.name = "AC_SyntyCombined";

            // ── Add all parameters ────────────────────────────────────────────────
            AddFloat(controller, clips.paramMoveSpeed);
            AddInt  (controller, clips.paramCurrentGait);
            AddBool (controller, clips.paramIsWalking);
            AddBool (controller, clips.paramIsCrouching);
            AddBool (controller, clips.paramIsGrounded);
            AddBool (controller, clips.paramIsJumping);
            AddFloat(controller, clips.paramStrafeX);
            AddFloat(controller, clips.paramStrafeZ);
            AddFloat(controller, clips.paramIsStrafing);
            AddFloat(controller, clips.paramLeanValue);
            AddFloat(controller, clips.paramFallingDuration);
            AddBool (controller, clips.paramMovementInputHeld);
            AddBool (controller, clips.paramMovementInputTapped);
            AddBool (controller, clips.paramIsStopped);
            AddBool (controller, clips.paramIsStarting);
            AddBool (controller, clips.paramSwordDrawn);
            AddFloat(controller, clips.paramSwordDrawnWeight);
            AddBool (controller, clips.paramIsSliding);
            AddInt  (controller, clips.paramCombatState);
            AddInt  (controller, clips.paramAttackID);
            AddInt  (controller, clips.paramDodgeDirection);
            AddInt  (controller, clips.paramHitDirection);

            // ══════════════════════════════════════════════════════════════════════
            // LAYER 0 – Locomotion (Base Layer, Full Body)
            // ══════════════════════════════════════════════════════════════════════
            var rootSM = controller.layers[0].stateMachine;
            rootSM.name = "Locomotion";

            // ── 1. Unarmed Locomotion State ───────────────────────────────────────
            AnimatorState unarmedLocoState = rootSM.AddState("Locomotion_Unarmed");
            unarmedLocoState.speed = 1f;

            var unarmedMainTree = new BlendTree();
            AssetDatabase.AddObjectToAsset(unarmedMainTree, controller);
            unarmedMainTree.name           = "Unarmed_Locomotion_Main";
            unarmedMainTree.blendType      = BlendTreeType.Simple1D;
            unarmedMainTree.blendParameter = clips.paramMoveSpeed;
            unarmedMainTree.useAutomaticThresholds = false;

            // Idle (unarmed)
            if (clips.idleStanding != null)
                unarmedMainTree.AddChild(clips.idleStanding, 0f);

            // Walk at 1.40 m/s – 2D Cartesian Strafe Tree (unarmed)
            var walkTree = new BlendTree();
            AssetDatabase.AddObjectToAsset(walkTree, controller);
            walkTree.name            = "Unarmed_Walk_Strafe";
            walkTree.blendType       = BlendTreeType.FreeformCartesian2D;
            walkTree.blendParameter  = clips.paramStrafeX;
            walkTree.blendParameterY = clips.paramStrafeZ;
            walkTree.useAutomaticThresholds = false;

            AddStrafe2DClips(walkTree,
                clips.walkForward,  clips.walkBackward,
                clips.walkStrafeL,  clips.walkStrafeR,
                clips.walkStrafeFL, clips.walkStrafeFR,
                clips.walkStrafeBL, clips.walkStrafeBR);
            unarmedMainTree.AddChild(walkTree, clips.walkClipSpeed);

            // Run at 2.50 m/s – 2D Cartesian Strafe Tree (unarmed)
            var runTree = new BlendTree();
            AssetDatabase.AddObjectToAsset(runTree, controller);
            runTree.name            = "Unarmed_Run_Strafe";
            runTree.blendType       = BlendTreeType.FreeformCartesian2D;
            runTree.blendParameter  = clips.paramStrafeX;
            runTree.blendParameterY = clips.paramStrafeZ;
            runTree.useAutomaticThresholds = false;

            AddStrafe2DClips(runTree,
                clips.runForward,  clips.runBackward,
                clips.runStrafeL,  clips.runStrafeR,
                clips.runStrafeFL, clips.runStrafeFR,
                clips.runStrafeBL, clips.runStrafeBR);
            unarmedMainTree.AddChild(runTree, clips.runClipSpeed);

            // Sprint at 7.00 m/s (unarmed)
            if (clips.sprintForward != null)
                unarmedMainTree.AddChild(clips.sprintForward, clips.sprintClipSpeed);

            unarmedLocoState.motion = unarmedMainTree;

            // ── 2. Armed Locomotion State (MOD_BL sword guard locomotion) ─────────
            AnimatorState armedLocoState = rootSM.AddState("Locomotion_Armed");
            armedLocoState.speed = 1f;

            var armedMainTree = new BlendTree();
            AssetDatabase.AddObjectToAsset(armedMainTree, controller);
            armedMainTree.name           = "Armed_Locomotion_Main";
            armedMainTree.blendType      = BlendTreeType.Simple1D;
            armedMainTree.blendParameter = clips.paramMoveSpeed;
            armedMainTree.useAutomaticThresholds = false;

            // Idle (armed sword guard)
            var swordIdle = clips.armedIdleStanding != null ? clips.armedIdleStanding :
                           (clips.swordIdleBase != null ? clips.swordIdleBase : clips.idleStanding);
            if (swordIdle != null)
                armedMainTree.AddChild(swordIdle, 0f);

            // Armed Walk at 1.40 m/s – 2D Cartesian Strafe Tree (MOD_BL)
            var armedWalkTree = new BlendTree();
            AssetDatabase.AddObjectToAsset(armedWalkTree, controller);
            armedWalkTree.name            = "Armed_Walk_Strafe";
            armedWalkTree.blendType       = BlendTreeType.FreeformCartesian2D;
            armedWalkTree.blendParameter  = clips.paramStrafeX;
            armedWalkTree.blendParameterY = clips.paramStrafeZ;
            armedWalkTree.useAutomaticThresholds = false;

            AddStrafe2DClips(armedWalkTree,
                clips.armedWalkForward   != null ? clips.armedWalkForward   : clips.walkForward,
                clips.armedWalkBackward  != null ? clips.armedWalkBackward  : clips.walkBackward,
                clips.armedWalkStrafeL   != null ? clips.armedWalkStrafeL   : clips.walkStrafeL,
                clips.armedWalkStrafeR   != null ? clips.armedWalkStrafeR   : clips.walkStrafeR,
                clips.armedWalkStrafeFL  != null ? clips.armedWalkStrafeFL  : clips.walkStrafeFL,
                clips.armedWalkStrafeFR  != null ? clips.armedWalkStrafeFR  : clips.walkStrafeFR,
                clips.armedWalkStrafeBL  != null ? clips.armedWalkStrafeBL  : clips.walkStrafeBL,
                clips.armedWalkStrafeBR  != null ? clips.armedWalkStrafeBR  : clips.walkStrafeBR);
            armedMainTree.AddChild(armedWalkTree, clips.walkClipSpeed);

            // Armed Run at 2.50 m/s – 2D Cartesian Strafe Tree (MOD_BL)
            var armedRunTree = new BlendTree();
            AssetDatabase.AddObjectToAsset(armedRunTree, controller);
            armedRunTree.name            = "Armed_Run_Strafe";
            armedRunTree.blendType       = BlendTreeType.FreeformCartesian2D;
            armedRunTree.blendParameter  = clips.paramStrafeX;
            armedRunTree.blendParameterY = clips.paramStrafeZ;
            armedRunTree.useAutomaticThresholds = false;

            AddStrafe2DClips(armedRunTree,
                clips.armedRunForward   != null ? clips.armedRunForward   : clips.runForward,
                clips.armedRunBackward  != null ? clips.armedRunBackward  : clips.runBackward,
                clips.armedRunStrafeL   != null ? clips.armedRunStrafeL   : clips.runStrafeL,
                clips.armedRunStrafeR   != null ? clips.armedRunStrafeR   : clips.runStrafeR,
                clips.armedRunStrafeFL  != null ? clips.armedRunStrafeFL  : clips.runStrafeFL,
                clips.armedRunStrafeFR  != null ? clips.armedRunStrafeFR  : clips.runStrafeFR,
                clips.armedRunStrafeBL  != null ? clips.armedRunStrafeBL  : clips.runStrafeBL,
                clips.armedRunStrafeBR  != null ? clips.armedRunStrafeBR  : clips.runStrafeBR);
            armedMainTree.AddChild(armedRunTree, clips.runClipSpeed);

            // Armed Sprint at 7.00 m/s (MOD_BL)
            var armedSprint = clips.armedSprintForward != null ? clips.armedSprintForward : clips.sprintForward;
            if (armedSprint != null)
                armedMainTree.AddChild(armedSprint, clips.sprintClipSpeed);

            armedLocoState.motion = armedMainTree;

            // ── Crossfades between Unarmed and Armed Locomotion ───────────────────
            var unarmedToArmed = unarmedLocoState.AddTransition(armedLocoState);
            unarmedToArmed.AddCondition(AnimatorConditionMode.If, 0f, clips.paramSwordDrawn);
            unarmedToArmed.duration = 0.20f;
            unarmedToArmed.hasExitTime = false;

            var armedToUnarmed = armedLocoState.AddTransition(unarmedLocoState);
            armedToUnarmed.AddCondition(AnimatorConditionMode.IfNot, 0f, clips.paramSwordDrawn);
            armedToUnarmed.duration = 0.20f;
            armedToUnarmed.hasExitTime = false;

            // ── Crouch State ──────────────────────────────────────────────────────
            AnimatorState crouchState = rootSM.AddState("Crouch");
            crouchState.speed = 1f;

            var crouchMainTree = new BlendTree();
            AssetDatabase.AddObjectToAsset(crouchMainTree, controller);
            crouchMainTree.name           = "Crouch_Main";
            crouchMainTree.blendType      = BlendTreeType.Simple1D;
            crouchMainTree.blendParameter = clips.paramMoveSpeed;
            crouchMainTree.useAutomaticThresholds = false;

            if (clips.idleCrouching != null)
                crouchMainTree.AddChild(clips.idleCrouching, 0f);

            var crouchWalkTree = new BlendTree();
            AssetDatabase.AddObjectToAsset(crouchWalkTree, controller);
            crouchWalkTree.name            = "CrouchWalk_Strafe";
            crouchWalkTree.blendType       = BlendTreeType.FreeformCartesian2D;
            crouchWalkTree.blendParameter  = clips.paramStrafeX;
            crouchWalkTree.blendParameterY = clips.paramStrafeZ;
            crouchWalkTree.useAutomaticThresholds = false;

            if (clips.crouchWalkForward  != null) crouchWalkTree.AddChild(clips.crouchWalkForward,  new Vector2(0f,  1f));
            if (clips.crouchWalkBackward != null) crouchWalkTree.AddChild(clips.crouchWalkBackward, new Vector2(0f, -1f));
            if (clips.crouchWalkStrafeL  != null) crouchWalkTree.AddChild(clips.crouchWalkStrafeL,  new Vector2(-1f, 0f));
            if (clips.crouchWalkStrafeR  != null) crouchWalkTree.AddChild(clips.crouchWalkStrafeR,  new Vector2( 1f, 0f));

            crouchMainTree.AddChild(crouchWalkTree, clips.crouchClipSpeed);
            crouchState.motion = crouchMainTree;

            // ── Slide State (Sprint To Crouch) ────────────────────────────────────
            AnimatorState slideState = rootSM.AddState("Sprint_ToCrouch");
            slideState.speed = 1f;
            if (clips.sprintToCrouch != null)
                slideState.motion = clips.sprintToCrouch;

            // ── InAir State ───────────────────────────────────────────────────────
            AnimatorState inAirState = rootSM.AddState("InAir");
            inAirState.motion = clips.fallShort != null ? (Motion)clips.fallShort : (Motion)clips.jumpIdle;

            // ── Locomotion Transitions (Unarmed) ──────────────────────────────────
            var unarmedToCrouch = unarmedLocoState.AddTransition(crouchState);
            unarmedToCrouch.AddCondition(AnimatorConditionMode.If, 0f, clips.paramIsCrouching);
            unarmedToCrouch.duration = 0.15f;
            unarmedToCrouch.hasExitTime = false;

            var crouchToUnarmed = crouchState.AddTransition(unarmedLocoState);
            crouchToUnarmed.AddCondition(AnimatorConditionMode.IfNot, 0f, clips.paramIsCrouching);
            crouchToUnarmed.AddCondition(AnimatorConditionMode.IfNot, 0f, clips.paramSwordDrawn);
            crouchToUnarmed.duration = 0.15f;
            crouchToUnarmed.hasExitTime = false;

            var crouchToArmed = crouchState.AddTransition(armedLocoState);
            crouchToArmed.AddCondition(AnimatorConditionMode.IfNot, 0f, clips.paramIsCrouching);
            crouchToArmed.AddCondition(AnimatorConditionMode.If, 0f, clips.paramSwordDrawn);
            crouchToArmed.duration = 0.15f;
            crouchToArmed.hasExitTime = false;

            // ── Locomotion Transitions (Armed) ────────────────────────────────────
            var armedToCrouch = armedLocoState.AddTransition(crouchState);
            armedToCrouch.AddCondition(AnimatorConditionMode.If, 0f, clips.paramIsCrouching);
            armedToCrouch.duration = 0.15f;
            armedToCrouch.hasExitTime = false;

            // Slide Transitions
            var unarmedToSlide = unarmedLocoState.AddTransition(slideState);
            unarmedToSlide.AddCondition(AnimatorConditionMode.If, 0f, clips.paramIsSliding);
            unarmedToSlide.duration = 0.10f;
            unarmedToSlide.hasExitTime = false;

            var armedToSlide = armedLocoState.AddTransition(slideState);
            armedToSlide.AddCondition(AnimatorConditionMode.If, 0f, clips.paramIsSliding);
            armedToSlide.duration = 0.10f;
            armedToSlide.hasExitTime = false;

            var slideToCrouch = slideState.AddTransition(crouchState);
            slideToCrouch.AddCondition(AnimatorConditionMode.IfNot, 0f, clips.paramIsSliding);
            slideToCrouch.AddCondition(AnimatorConditionMode.If, 0f, clips.paramIsCrouching);
            slideToCrouch.duration = 0.20f;
            slideToCrouch.hasExitTime = false;

            var slideToUnarmed = slideState.AddTransition(unarmedLocoState);
            slideToUnarmed.AddCondition(AnimatorConditionMode.IfNot, 0f, clips.paramIsSliding);
            slideToUnarmed.AddCondition(AnimatorConditionMode.IfNot, 0f, clips.paramIsCrouching);
            slideToUnarmed.AddCondition(AnimatorConditionMode.IfNot, 0f, clips.paramSwordDrawn);
            slideToUnarmed.duration = 0.20f;
            slideToUnarmed.hasExitTime = false;

            var slideToArmed = slideState.AddTransition(armedLocoState);
            slideToArmed.AddCondition(AnimatorConditionMode.IfNot, 0f, clips.paramIsSliding);
            slideToArmed.AddCondition(AnimatorConditionMode.IfNot, 0f, clips.paramIsCrouching);
            slideToArmed.AddCondition(AnimatorConditionMode.If, 0f, clips.paramSwordDrawn);
            slideToArmed.duration = 0.20f;
            slideToArmed.hasExitTime = false;

            var slideToAir = slideState.AddTransition(inAirState);
            slideToAir.AddCondition(AnimatorConditionMode.If, 0f, clips.paramIsJumping);
            slideToAir.duration = 0.10f;
            slideToAir.hasExitTime = false;

            // InAir Transitions
            var unarmedToAirJump = unarmedLocoState.AddTransition(inAirState);
            unarmedToAirJump.AddCondition(AnimatorConditionMode.If, 0f, clips.paramIsJumping);
            unarmedToAirJump.duration = 0.10f;
            unarmedToAirJump.hasExitTime = false;

            var unarmedToAirFall = unarmedLocoState.AddTransition(inAirState);
            unarmedToAirFall.AddCondition(AnimatorConditionMode.IfNot, 0f, clips.paramIsGrounded);
            unarmedToAirFall.duration = 0.15f;
            unarmedToAirFall.hasExitTime = false;

            var armedToAirJump = armedLocoState.AddTransition(inAirState);
            armedToAirJump.AddCondition(AnimatorConditionMode.If, 0f, clips.paramIsJumping);
            armedToAirJump.duration = 0.10f;
            armedToAirJump.hasExitTime = false;

            var armedToAirFall = armedLocoState.AddTransition(inAirState);
            armedToAirFall.AddCondition(AnimatorConditionMode.IfNot, 0f, clips.paramIsGrounded);
            armedToAirFall.duration = 0.15f;
            armedToAirFall.hasExitTime = false;

            var crouchToAir = crouchState.AddTransition(inAirState);
            crouchToAir.AddCondition(AnimatorConditionMode.If, 0f, clips.paramIsJumping);
            crouchToAir.duration = 0.10f;
            crouchToAir.hasExitTime = false;

            var airToUnarmed = inAirState.AddTransition(unarmedLocoState);
            airToUnarmed.AddCondition(AnimatorConditionMode.If, 0f, clips.paramIsGrounded);
            airToUnarmed.AddCondition(AnimatorConditionMode.IfNot, 0f, clips.paramIsJumping);
            airToUnarmed.AddCondition(AnimatorConditionMode.IfNot, 0f, clips.paramSwordDrawn);
            airToUnarmed.duration = 0.15f;
            airToUnarmed.hasExitTime = false;

            var airToArmed = inAirState.AddTransition(armedLocoState);
            airToArmed.AddCondition(AnimatorConditionMode.If, 0f, clips.paramIsGrounded);
            airToArmed.AddCondition(AnimatorConditionMode.IfNot, 0f, clips.paramIsJumping);
            airToArmed.AddCondition(AnimatorConditionMode.If, 0f, clips.paramSwordDrawn);
            airToArmed.duration = 0.15f;
            airToArmed.hasExitTime = false;

            rootSM.defaultState = unarmedLocoState;

            // ══════════════════════════════════════════════════════════════════════
            // LAYER 1 – SwordArm (Mask_Arm_R, Override, weight ~0.70 while moving)
            // (Official Synty User Guide page 18, step 2–8)
            // ══════════════════════════════════════════════════════════════════════
            var swordArmLayer           = new AnimatorControllerLayer();
            swordArmLayer.name          = "SwordArm";
            swordArmLayer.defaultWeight = 0f;
            swordArmLayer.blendingMode  = AnimatorLayerBlendingMode.Override;
            swordArmLayer.avatarMask    = armMask;

            var swordArmSM = new AnimatorStateMachine();
            swordArmSM.name = "SwordArm";
            AssetDatabase.AddObjectToAsset(swordArmSM, controller);
            swordArmLayer.stateMachine = swordArmSM;

            AnimatorState armHold = swordArmSM.AddState("Sword_Hold_Arm");
            armHold.motion = swordIdle;
            armHold.speed  = 1f;
            swordArmSM.defaultState = armHold;

            // ══════════════════════════════════════════════════════════════════════
            // LAYER 2 – SwordHand (Mask_Hand_R, Override, weight 1.00 for locked grip)
            // (Official Synty User Guide page 18, step 2–8)
            // ══════════════════════════════════════════════════════════════════════
            var swordHandLayer           = new AnimatorControllerLayer();
            swordHandLayer.name          = "SwordHand";
            swordHandLayer.defaultWeight = 0f;
            swordHandLayer.blendingMode  = AnimatorLayerBlendingMode.Override;
            swordHandLayer.avatarMask    = handMask;

            var swordHandSM = new AnimatorStateMachine();
            swordHandSM.name = "SwordHand";
            AssetDatabase.AddObjectToAsset(swordHandSM, controller);
            swordHandLayer.stateMachine = swordHandSM;

            AnimatorState handHold = swordHandSM.AddState("Sword_Hold_Hand");
            handHold.motion = swordIdle;
            handHold.speed  = 1f;
            swordHandSM.defaultState = handHold;

            // ══════════════════════════════════════════════════════════════════════
            // LAYER 3 – Combat_Actions (Full Body Override)
            // (Official Synty User Guide page 20, step 11)
            // ══════════════════════════════════════════════════════════════════════
            var combatLayer           = new AnimatorControllerLayer();
            combatLayer.name          = "Combat_Actions";
            combatLayer.defaultWeight = 0f;
            combatLayer.blendingMode  = AnimatorLayerBlendingMode.Override;
            combatLayer.avatarMask    = null; // Full Body Override

            var combatSM = new AnimatorStateMachine();
            combatSM.name = "Combat_Actions";
            AssetDatabase.AddObjectToAsset(combatSM, controller);
            combatLayer.stateMachine = combatSM;

            // Default state: empty (no motion). Layer stays here when no combat action is playing.
            AnimatorState combatEmpty = combatSM.AddState("Combat_Empty");
            combatEmpty.motion = null;
            combatSM.defaultState = combatEmpty;

            // Register all combat animations as states in this layer
            var addedClips = new HashSet<string>();

            void AddCombatState(AnimationClip clip)
            {
                if (clip == null || addedClips.Contains(clip.name)) return;
                addedClips.Add(clip.name);

                var state = combatSM.AddState(clip.name);
                state.motion = clip;
                state.speed  = 1f;

                // Failsafe exit transition to Combat_Empty at end of clip
                var exitTrans = state.AddTransition(combatEmpty);
                exitTrans.hasExitTime = true;
                exitTrans.exitTime    = 0.98f;
                exitTrans.duration    = 0.15f;
                exitTrans.hasFixedDuration = true;
            }

            // Draw & Sheathe
            AddCombatState(clips.drawSword);
            AddCombatState(clips.sheatheSword);

            // Sword Idles
            AddCombatState(clips.swordIdleBase);
            AddCombatState(clips.swordIdleSheathed);
            AddCombatState(clips.swordIdleEnergetic);
            AddCombatState(clips.swordIdleFlourish);
            AddCombatState(clips.swordIdleMenacingBegin);
            AddCombatState(clips.swordIdleMenacingLoop);
            AddCombatState(clips.swordIdleMenacingEnd);

            // Light Attacks
            AddCombatState(clips.lightComboA.clip);
            AddCombatState(clips.lightComboB.clip);
            AddCombatState(clips.lightComboC.clip);
            AddCombatState(clips.lightFencing.clip);
            AddCombatState(clips.lightLeaping.clip);

            // Heavy Attacks
            AddCombatState(clips.heavyComboA.clip);
            AddCombatState(clips.heavyComboB.clip);
            AddCombatState(clips.heavyComboC.clip);
            AddCombatState(clips.heavyFlourish.clip);
            AddCombatState(clips.heavyStab.clip);

            // Block & Parry
            AddCombatState(clips.blockBegin);
            AddCombatState(clips.blockLoop);
            AddCombatState(clips.blockEnd);
            AddCombatState(clips.parryForward.clip);
            AddCombatState(clips.parryLeft.clip);
            AddCombatState(clips.parryRight.clip);
            AddCombatState(clips.parryBreak.clip);
            AddCombatState(clips.counterShove.clip);
            AddCombatState(clips.pommelStrike.clip);
            AddCombatState(clips.parryReturnToBlock);

            // Dodges & Rolls
            AddCombatState(clips.dodgeForward.clip);
            AddCombatState(clips.dodgeBack.clip);
            AddCombatState(clips.dodgeLeft.clip);
            AddCombatState(clips.dodgeRight.clip);
            AddCombatState(clips.dodgeRollForward.clip);
            AddCombatState(clips.dodgeRollBack.clip);
            AddCombatState(clips.dodgeRollLeft.clip);
            AddCombatState(clips.dodgeRollRight.clip);

            // Reactions & Impact
            AddCombatState(clips.hitReactForward.clip);
            AddCombatState(clips.hitReactBack.clip);
            AddCombatState(clips.hitReactLeft.clip);
            AddCombatState(clips.hitReactRight.clip);
            AddCombatState(clips.hitStaggerForward.clip);
            AddCombatState(clips.hitStaggerBack.clip);
            AddCombatState(clips.hitStaggerLeft.clip);
            AddCombatState(clips.hitStaggerRight.clip);
            AddCombatState(clips.knockDownBegin);
            AddCombatState(clips.knockDownLoop);
            AddCombatState(clips.knockDownEnd);
            AddCombatState(clips.stunBegin);
            AddCombatState(clips.stunLoop);
            AddCombatState(clips.stunEnd);

            // Deaths
            AddCombatState(clips.deathForward);
            AddCombatState(clips.deathBack);
            AddCombatState(clips.deathLeft);
            AddCombatState(clips.deathRight);

            // ── Serialise Layer Array ─────────────────────────────────────────────
            var layers = new AnimatorControllerLayer[4];
            layers[0] = controller.layers[0];
            layers[0].name = "Locomotion";
            layers[1] = swordArmLayer;
            layers[2] = swordHandLayer;
            layers[3] = combatLayer;
            controller.layers = layers;

            // ── Save ──────────────────────────────────────────────────────────────
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SyntyCombined] Animator Controller built successfully at: {OutputPath} with {addedClips.Count} combat states across 4 layers (Locomotion, SwordArm, SwordHand, Combat_Actions)!");

            if (!Application.isBatchMode)
            {
                Selection.activeObject = controller;
                EditorGUIUtility.PingObject(controller);
            }
        }

        private static void AddStrafe2DClips(BlendTree tree,
            AnimationClip fwd, AnimationClip bwd,
            AnimationClip left, AnimationClip right,
            AnimationClip fl, AnimationClip fr,
            AnimationClip bl, AnimationClip br)
        {
            const float d = 0.7071f;
            if (fwd   != null) tree.AddChild(fwd,   new Vector2( 0f,  1f));
            if (bwd   != null) tree.AddChild(bwd,   new Vector2( 0f, -1f));
            if (left  != null) tree.AddChild(left,  new Vector2(-1f,  0f));
            if (right != null) tree.AddChild(right, new Vector2( 1f,  0f));
            if (fl    != null) tree.AddChild(fl,    new Vector2(-d,   d));
            if (fr    != null) tree.AddChild(fr,    new Vector2( d,   d));
            if (bl    != null) tree.AddChild(bl,    new Vector2(-d,  -d));
            if (br    != null) tree.AddChild(br,    new Vector2( d,  -d));
        }

        private static void AddFloat(AnimatorController c, string name)
            => c.AddParameter(name, AnimatorControllerParameterType.Float);
        private static void AddInt(AnimatorController c, string name)
            => c.AddParameter(name, AnimatorControllerParameterType.Int);
        private static void AddBool(AnimatorController c, string name)
            => c.AddParameter(name, AnimatorControllerParameterType.Bool);
    }
}
#endif
