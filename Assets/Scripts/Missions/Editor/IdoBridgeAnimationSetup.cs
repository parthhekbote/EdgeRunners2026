using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Cyberpunk.Missions.Editor
{
    public static class IdoBridgeAnimationSetup
    {
        public const string VulcanControllerPath = "Assets/Scripts/Missions/AC_IdoBridge_Vulcan.controller";
        public const string DoctorControllerPath = "Assets/Scripts/Missions/AC_IdoBridge_Doctor.controller";

        public const string HeavyStabFbx = "Assets/Synty/AnimationSwordCombat/Animations/Polygon/Attack/HeavyStab01/A_Attack_HeavyStab01_Sword.fbx";
        public const string MenacingIdleFbx = "Assets/Synty/AnimationSwordCombat/Animations/Polygon/Idle/Menacing01/A_Idle_Menacing01_Sword.fbx";
        public const string RiposteVictimFbx = "Assets/Nephilite Studios/Straight Sword Animation Set/Animations/Straight Sword Animations/Nephilim Animations (Remastered)/Straight Sword Critical Strike/core_main_riposte_victim_01.fbx";
        public const string RiposteDeathFbx = "Assets/Nephilite Studios/Straight Sword Animation Set/Animations/Straight Sword Animations/Nephilim Animations (Remastered)/Straight Sword Critical Strike/core_riposte_death_01.fbx";
        public const string RiposteAttackerFbx = "Assets/Nephilite Studios/Straight Sword Animation Set/Animations/Straight Sword Animations/Nephilim Animations (Remastered)/Straight Sword Critical Strike/core_main_riposte_01.fbx";
        public const string DoctorIdleFbx = "Assets/InsaneGun_Sword_Set/Animation/Humanoid/idle.fbx";
        public const string HitFlinchFbx = "Assets/Synty/AnimationSwordCombat/Animations/Polygon/Hit/HitReact/A_Hit_F_React_Sword.fbx";

        [MenuItem("Tools/Missions/Verify & Build Ido Bridge Cutscene Controllers")]
        public static void BuildControllers()
        {
            Debug.Log("[IdoBridgeAnimationSetup] Verifying and generating cutscene AnimatorControllers...");

            var clipHeavyStab = LoadClip(HeavyStabFbx, "A_Attack_HeavyStab01_Sword");
            var clipMenacingIdle = LoadClip(MenacingIdleFbx, "A_Idle_Menacing01_Sword");
            var clipRiposteVictim = LoadClip(RiposteVictimFbx, "core_main_riposte_victim_01");
            var clipRiposteDeath = LoadClip(RiposteDeathFbx, "core_riposte_death_01");
            var clipRiposteAttacker = LoadClip(RiposteAttackerFbx, "core_main_riposte_01");
            var clipDoctorIdle = LoadClip(DoctorIdleFbx, "idle") ?? LoadClip(MenacingIdleFbx, "A_Idle_Menacing01_Sword");
            var clipHitFlinch = LoadClip(HitFlinchFbx, "A_Hit_F_React_Sword");

            Debug.Log($"[IdoBridgeAnimationSetup] Clips Loaded:\n" +
                      $"HeavyStab: {clipHeavyStab != null} ({clipHeavyStab?.length:F2}s)\n" +
                      $"MenacingIdle: {clipMenacingIdle != null} ({clipMenacingIdle?.length:F2}s)\n" +
                      $"RiposteVictim (Stabbing Death): {clipRiposteVictim != null} ({clipRiposteVictim?.length:F2}s)\n" +
                      $"RiposteDeath: {clipRiposteDeath != null} ({clipRiposteDeath?.length:F2}s)\n" +
                      $"HitFlinch: {clipHitFlinch != null} ({clipHitFlinch?.length:F2}s)");

            // 1. Build Vulcan Controller
            var vulcanCtrl = AnimatorController.CreateAnimatorControllerAtPath(VulcanControllerPath);
            var vRootSm = vulcanCtrl.layers[0].stateMachine;

            var vIdleState = vRootSm.AddState("Idle");
            vIdleState.motion = clipMenacingIdle;
            vRootSm.defaultState = vIdleState;

            var vStabState = vRootSm.AddState("HeavyStab");
            vStabState.motion = clipHeavyStab;

            if (clipRiposteAttacker != null)
            {
                var vRiposteState = vRootSm.AddState("RiposteStab");
                vRiposteState.motion = clipRiposteAttacker;
            }

            // 2. Build Doctor Ido Controller
            var docCtrl = AnimatorController.CreateAnimatorControllerAtPath(DoctorControllerPath);
            var docRootSm = docCtrl.layers[0].stateMachine;

            var docIdleState = docRootSm.AddState("Idle");
            docIdleState.motion = clipDoctorIdle;
            docRootSm.defaultState = docIdleState;

            var docFlinchState = docRootSm.AddState("HitFlinch");
            docFlinchState.motion = clipHitFlinch;

            var docStabDeathState = docRootSm.AddState("StabDeath");
            docStabDeathState.motion = clipRiposteVictim ?? clipRiposteDeath;

            if (clipRiposteDeath != null)
            {
                var docRiposteDeathState = docRootSm.AddState("RiposteDeath");
                docRiposteDeathState.motion = clipRiposteDeath;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[IdoBridgeAnimationSetup] Successfully generated AC_IdoBridge_Vulcan and AC_IdoBridge_Doctor!");
        }

        public static AnimationClip LoadClip(string fbxPath, string clipName)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            if (assets != null)
            {
                foreach (var asset in assets)
                {
                    if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    {
                        if (string.IsNullOrEmpty(clipName) || clip.name == clipName)
                            return clip;
                    }
                }
            }
            return null;
        }
    }
}
