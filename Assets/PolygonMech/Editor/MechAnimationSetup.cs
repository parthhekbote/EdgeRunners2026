using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace PolygonMech.Editor
{
    public static class MechAnimationSetup
    {
        // ── GreatSwordPack contains animations retargeted ON SM_Veh_Mech_01 ──
        private const string GSPPath        = "Assets/PolygonMech/Animations/GreatSwordPack";
        private const string ControllerPath = "Assets/PolygonMech/Animations/AC_BossMech.controller";
        private const string SamuraiControllerPath = "Assets/PolygonMech/Animations/AC_SamuraiMech.controller";

        [MenuItem("Polygon Mech/1. Setup Animations and Controller", false, 1)]
        public static void SetupAll()
        {
            Debug.Log("[MechSetup] Building AC_BossMech Animator Controller (GreatSwordPack)...");
            BuildBossController();

            Debug.Log("[MechSetup] Building AC_SamuraiMech Animator Controller (GreatSwordPack)...");
            BuildSamuraiController();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MechSetup] SUCCESS! Boss Animator Controllers built from GreatSwordPack mech rig.");
        }

        // ── Loads the clip from an FBX that is retargeted to SM_Veh_Mech_01 ──
        private static AnimationClip LoadClip(string fbxPath)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            foreach (var a in assets)
            {
                if (a is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    return clip;
            }
            Debug.LogWarning("[MechSetup] Clip not found: " + fbxPath);
            return null;
        }

        public static AnimatorController BuildBossController()
        {
            AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (ctrl == null)
                ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            // ── Load 32 Curated Mechanical Animations ──
            const string CPPath = "Assets/PolygonMech/Animations/CreaturePack";

            // 1. Idles
            AnimationClip clipIdle      = LoadClip($"{GSPPath}/great sword idle (3).fbx");
            AnimationClip clipIdleVent  = LoadClip($"{CPPath}/mutant breathing idle.fbx") ?? clipIdle;

            // 2. Locomotion
            AnimationClip clipWalk      = LoadClip($"{GSPPath}/great sword walk.fbx");
            AnimationClip clipWalkPatrol= LoadClip($"{GSPPath}/great sword walk (2).fbx") ?? clipWalk;
            AnimationClip clipRun       = LoadClip($"{GSPPath}/great sword run.fbx");
            AnimationClip clipRunDash   = LoadClip($"{GSPPath}/great sword run (2).fbx") ?? clipRun;
            AnimationClip clipStrafeR   = LoadClip($"{GSPPath}/great sword strafe.fbx");
            AnimationClip clipStrafeL   = LoadClip($"{GSPPath}/great sword strafe (2).fbx");
            AnimationClip clipDiagR     = LoadClip($"{GSPPath}/great sword strafe (3).fbx");
            AnimationClip clipDiagL     = LoadClip($"{GSPPath}/great sword strafe (4).fbx");

            // 3. Turns & Pivots
            AnimationClip clipTurn180   = LoadClip($"{GSPPath}/great sword 180 turn.fbx");
            AnimationClip clipTurn90R   = LoadClip($"{CPPath}/mutant right turn 90.fbx") ?? clipTurn180;
            AnimationClip clipTurn45L   = LoadClip($"{CPPath}/mutant left turn 45.fbx") ?? clipTurn180;

            // 4. Heavy Weapon Slashes
            AnimationClip clipSlashCleave    = LoadClip($"{GSPPath}/great sword slash.fbx");
            AnimationClip clipSlashOverhead  = LoadClip($"{GSPPath}/great sword slash (2).fbx");
            AnimationClip clipSlashLaunch    = LoadClip($"{GSPPath}/great sword slash (3).fbx");
            AnimationClip clipSlashCrossCut  = LoadClip($"{GSPPath}/great sword slash (4).fbx");
            AnimationClip clipSlashDecapitate= LoadClip($"{GSPPath}/great sword slash (5).fbx");

            // 5. Heavy Mechanical Attacks
            AnimationClip clipPistonThrust   = LoadClip($"{GSPPath}/great sword attack.fbx");
            AnimationClip clipCycloneSpin    = LoadClip($"{GSPPath}/great sword high spin attack.fbx");
            AnimationClip clipRocketDash     = LoadClip($"{GSPPath}/great sword slide attack.fbx");
            AnimationClip clipJumpCleave     = LoadClip($"{GSPPath}/great sword jump attack.fbx");
            AnimationClip clipPneumaticPunch = LoadClip($"{CPPath}/mutant punch.fbx");
            AnimationClip clipClawSwipe      = LoadClip($"{CPPath}/mutant swiping.fbx");
            AnimationClip clipHydraulicKick  = LoadClip($"{GSPPath}/great sword kick.fbx");
            AnimationClip clipSeismicHeel    = LoadClip($"{GSPPath}/great sword kick (2).fbx");

            // 6. Defensive Guards
            AnimationClip clipBladeShield    = LoadClip($"{GSPPath}/great sword blocking.fbx");
            AnimationClip clipHeavyBrace     = LoadClip($"{GSPPath}/great sword blocking (2).fbx");
            AnimationClip clipRiposteParry   = LoadClip($"{GSPPath}/great sword blocking (3).fbx");

            // 7. Power, Stagger & Death
            AnimationClip clipRoar           = LoadClip($"{CPPath}/mutant roaring.fbx");
            AnimationClip clipPowerUp        = LoadClip($"{GSPPath}/great sword power up.fbx");
            AnimationClip clipImpactLight    = LoadClip($"{GSPPath}/great sword impact.fbx");
            AnimationClip clipImpactHeavy    = LoadClip($"{GSPPath}/great sword impact (3).fbx");
            AnimationClip clipDeathExplode   = LoadClip($"{GSPPath}/two handed sword death (2).fbx");
            AnimationClip clipDeathFall      = LoadClip($"{GSPPath}/two handed sword death.fbx");

            // ── Clear and rebuild parameters ──
            while (ctrl.parameters.Length > 0) ctrl.RemoveParameter(0);

            // Floats
            ctrl.AddParameter("MoveX",          AnimatorControllerParameterType.Float);
            ctrl.AddParameter("MoveZ",          AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Speed",          AnimatorControllerParameterType.Float);

            // Backward-compatible attack triggers
            ctrl.AddParameter("MeleeSwipe",     AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("GroundSlam",     AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("SpinAttack",     AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("SlideAttack",    AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("SeismicStomp",   AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("MissileBarrage", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("GatlingBurst",   AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Roar",           AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Stagger",        AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Die",            AnimatorControllerParameterType.Trigger);

            // 32-Move Curated Triggers
            ctrl.AddParameter("SlashCleave",       AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("SlashOverhead",     AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("SlashLaunch",       AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("SlashCrossCut",     AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("SlashDecapitate",   AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("PistonThrust",      AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("CycloneSpin",       AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("RocketDash",        AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("JumpCleave",        AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("PneumaticPunch",    AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("ClawSwipe",         AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("HydraulicKick",     AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("SeismicHeel",       AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("BladeShield",       AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("HeavyBrace",        AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("RiposteParry",      AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Turn180",           AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Turn90R",           AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Turn45L",           AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("OverdriveRoar",     AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("ReactorPowerUp",    AnimatorControllerParameterType.Trigger);

            var sm = ctrl.layers[0].stateMachine;
            foreach (var cs in sm.states) sm.RemoveState(cs.state);
            foreach (var at in sm.anyStateTransitions) sm.RemoveAnyStateTransition(at);

            // 1. 2D Freeform Directional Locomotion BlendTree
            var blendTree = new BlendTree
            {
                name = "Mech2DLocomotion",
                blendType = BlendTreeType.FreeformDirectional2D,
                blendParameter = "MoveX",
                blendParameterY = "MoveZ",
                useAutomaticThresholds = false
            };

            if (clipIdle       != null) blendTree.AddChild(clipIdle,       new Vector2(0f, 0f));
            if (clipWalk       != null) blendTree.AddChild(clipWalk,       new Vector2(0f, 1f));
            if (clipRunDash    != null) blendTree.AddChild(clipRunDash,    new Vector2(0f, 2f));
            if (clipWalkPatrol != null) blendTree.AddChild(clipWalkPatrol, new Vector2(0f, -1f));
            if (clipStrafeL    != null) blendTree.AddChild(clipStrafeL,    new Vector2(-1f, 0f));
            if (clipStrafeR    != null) blendTree.AddChild(clipStrafeR,    new Vector2(1f, 0f));
            if (clipDiagL      != null) blendTree.AddChild(clipDiagL,      new Vector2(-0.7f, 0.7f));
            if (clipDiagR      != null) blendTree.AddChild(clipDiagR,      new Vector2(0.7f, 0.7f));

            AssetDatabase.AddObjectToAsset(blendTree, ctrl);

            var stateLoco = sm.AddState("Locomotion", new Vector3(200, 100, 0));
            stateLoco.motion = blendTree;
            sm.defaultState = stateLoco;

            // 2. Idle Combat Stance
            var stateIdle = sm.AddState("Idle", new Vector3(200, 200, 0));
            stateIdle.motion = clipIdle;

            var tLocoIdle = stateLoco.AddTransition(stateIdle);
            tLocoIdle.AddCondition(AnimatorConditionMode.Less, 0.08f, "Speed");
            tLocoIdle.hasExitTime = false; tLocoIdle.duration = 0.2f;

            var tIdleLoco = stateIdle.AddTransition(stateLoco);
            tIdleLoco.AddCondition(AnimatorConditionMode.Greater, 0.08f, "Speed");
            tIdleLoco.hasExitTime = false; tIdleLoco.duration = 0.2f;


            var stateDict = new System.Collections.Generic.Dictionary<string, AnimatorState>();

            AnimatorState AddCombatState(string name, AnimationClip clip, Vector3 pos, string primaryTrigger, string altTrigger = null, float exitTime = 0.95f, float blendDur = 0.12f)
            {
                var st = sm.AddState(name, pos);
                st.motion = clip;
                stateDict[name] = st;

                void BindTrigger(string trig)
                {
                    if (string.IsNullOrEmpty(trig)) return;
                    var tI = stateIdle.AddTransition(st);
                    tI.AddCondition(AnimatorConditionMode.If, 0, trig);
                    tI.hasExitTime = false; tI.duration = blendDur;

                    var tL = stateLoco.AddTransition(st);
                    tL.AddCondition(AnimatorConditionMode.If, 0, trig);
                    tL.hasExitTime = false; tL.duration = blendDur;
                }

                BindTrigger(primaryTrigger);
                if (!string.IsNullOrEmpty(altTrigger)) BindTrigger(altTrigger);

                var tBack = st.AddTransition(stateIdle);
                tBack.hasExitTime = true;
                tBack.exitTime = exitTime;
                tBack.duration = 0.15f;

                var tBackLoco = st.AddTransition(stateLoco);
                tBackLoco.hasExitTime = true;
                tBackLoco.exitTime = exitTime;
                tBackLoco.duration = 0.15f;
                tBackLoco.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

                return st;
            }

            void AddComboLink(string fromName, string toName, string trigger, float exitTime = 0.65f, float blendDur = 0.10f)
            {
                if (stateDict.TryGetValue(fromName, out var fromSt) && stateDict.TryGetValue(toName, out var toSt))
                {
                    var t = fromSt.AddTransition(toSt);
                    t.AddCondition(AnimatorConditionMode.If, 0, trigger);
                    t.hasExitTime = true;
                    t.exitTime = exitTime;
                    t.duration = blendDur;
                }
            }

            void AddDefensiveInterrupt(string fromName, string toName, string trigger)
            {
                if (stateDict.TryGetValue(fromName, out var fromSt) && stateDict.TryGetValue(toName, out var toSt))
                {
                    var t = fromSt.AddTransition(toSt);
                    t.AddCondition(AnimatorConditionMode.If, 0, trigger);
                    t.hasExitTime = false;
                    t.duration = 0.08f;
                }
            }

            int posX = 550;
            int posY = -100;
            int stepY = 65;

            // ── Slashes ──
            AddCombatState("MeleeSwipe",      clipSlashCleave,     new Vector3(posX, posY += stepY), "MeleeSwipe", "SlashCleave", 0.88f);
            AddCombatState("SlashOverhead",   clipSlashOverhead,   new Vector3(posX, posY += stepY), "SlashOverhead", null, 0.90f);
            AddCombatState("SlashLaunch",     clipSlashLaunch,     new Vector3(posX, posY += stepY), "SlashLaunch", null, 0.88f);
            AddCombatState("SlashCrossCut",   clipSlashCrossCut,   new Vector3(posX, posY += stepY), "SlashCrossCut", null, 0.85f);
            AddCombatState("SlashDecapitate", clipSlashDecapitate, new Vector3(posX, posY += stepY), "SlashDecapitate", null, 0.90f);

            // ── Heavy Mechanical Attacks ──
            AddCombatState("PistonThrust",    clipPistonThrust,    new Vector3(posX, posY += stepY), "PistonThrust", null, 0.85f);
            AddCombatState("SpinAttack",      clipCycloneSpin,     new Vector3(posX, posY += stepY), "SpinAttack", "CycloneSpin", 0.90f);
            AddCombatState("SlideAttack",     clipRocketDash,      new Vector3(posX, posY += stepY), "SlideAttack", "RocketDash", 0.85f);
            AddCombatState("GroundSlam",      clipJumpCleave,      new Vector3(posX, posY += stepY), "GroundSlam", "JumpCleave", 0.92f);
            AddCombatState("PneumaticPunch",  clipPneumaticPunch,  new Vector3(posX, posY += stepY), "PneumaticPunch", null, 0.82f);
            AddCombatState("ClawSwipe",       clipClawSwipe,       new Vector3(posX, posY += stepY), "ClawSwipe", null, 0.85f);
            AddCombatState("HydraulicKick",   clipHydraulicKick,   new Vector3(posX, posY += stepY), "HydraulicKick", null, 0.85f);
            AddCombatState("SeismicStomp",    clipSeismicHeel,     new Vector3(posX, posY += stepY), "SeismicStomp", "SeismicHeel", 0.88f);

            // ── Defensive Guards ──
            AddCombatState("BladeShield",     clipBladeShield,     new Vector3(posX, posY += stepY), "BladeShield", null, 0.95f, 0.08f);
            AddCombatState("HeavyBrace",      clipHeavyBrace,      new Vector3(posX, posY += stepY), "HeavyBrace", null, 0.95f, 0.08f);
            AddCombatState("RiposteParry",    clipRiposteParry,    new Vector3(posX, posY += stepY), "RiposteParry", null, 0.85f, 0.08f);

            // ── Turns & Pivots ──
            AddCombatState("Turn180",         clipTurn180,         new Vector3(posX, posY += stepY), "Turn180", null, 0.85f, 0.1f);
            AddCombatState("Turn90R",         clipTurn90R,         new Vector3(posX, posY += stepY), "Turn90R", null, 0.85f, 0.1f);
            AddCombatState("Turn45L",         clipTurn45L,         new Vector3(posX, posY += stepY), "Turn45L", null, 0.85f, 0.1f);

            // ── Ordnance & Flares ──
            AddCombatState("MissileBarrage",  clipPowerUp,         new Vector3(posX, posY += stepY), "MissileBarrage", null, 0.92f);
            AddCombatState("GatlingBurst",    clipHeavyBrace,      new Vector3(posX, posY += stepY), "GatlingBurst", null, 0.92f);
            AddCombatState("Roar",            clipRoar,            new Vector3(posX, posY += stepY), "Roar", "OverdriveRoar", 0.95f);
            AddCombatState("ReactorPowerUp",  clipPowerUp,         new Vector3(posX, posY += stepY), "ReactorPowerUp", null, 0.95f);

            // ── Direct Combo Chaining ──
            // Combo 1: Cleave -> CrossCut -> Decapitate
            AddComboLink("MeleeSwipe", "SlashCrossCut", "SlashCrossCut", 0.60f);
            AddComboLink("SlashCrossCut", "SlashDecapitate", "SlashDecapitate", 0.62f);

            // Combo 2: Overhead -> Upward Launch -> Jump Ground Slam
            AddComboLink("SlashOverhead", "SlashLaunch", "SlashLaunch", 0.65f);
            AddComboLink("SlashLaunch", "GroundSlam", "GroundSlam", 0.68f);

            // Combo 3: Piston Thrust -> Cyclone Spin -> Rocket Dash
            AddComboLink("PistonThrust", "SpinAttack", "SpinAttack", 0.58f);
            AddComboLink("SpinAttack", "SlideAttack", "SlideAttack", 0.70f);

            // Combo 4: Pneumatic Punch -> Claw Backhand -> Hydraulic Kick -> Seismic Heel
            AddComboLink("PneumaticPunch", "ClawSwipe", "ClawSwipe", 0.55f);
            AddComboLink("ClawSwipe", "HydraulicKick", "HydraulicKick", 0.62f);
            AddComboLink("HydraulicKick", "SeismicStomp", "SeismicStomp", 0.65f);

            // Defensive Parry Ripostes: BladeShield / RiposteParry directly into MeleeSwipe or SlashCleave
            AddComboLink("BladeShield", "RiposteParry", "RiposteParry", 0.30f);
            AddComboLink("RiposteParry", "MeleeSwipe", "MeleeSwipe", 0.40f);

            // Any-State Parry Interrupts for agile counters
            string[] interruptableAttacks = { "MeleeSwipe", "SlashCrossCut", "PistonThrust", "PneumaticPunch" };
            foreach (var atk in interruptableAttacks)
            {
                AddDefensiveInterrupt(atk, "BladeShield", "BladeShield");
                AddDefensiveInterrupt(atk, "RiposteParry", "RiposteParry");
            }

            // ── Hit Reactions & Death ──
            var stStagger = sm.AddState("Stagger", new Vector3(200, -80));
            stStagger.motion = clipImpactHeavy;
            var tAnyStagger = sm.AddAnyStateTransition(stStagger);
            tAnyStagger.AddCondition(AnimatorConditionMode.If, 0, "Stagger");
            tAnyStagger.hasExitTime = false; tAnyStagger.duration = 0.1f;
            var tStaggerBack = stStagger.AddTransition(stateIdle);
            tStaggerBack.hasExitTime = true; tStaggerBack.exitTime = 0.95f; tStaggerBack.duration = 0.15f;

            var stDeath = sm.AddState("Death", new Vector3(200, 520));
            stDeath.motion = clipDeathExplode;
            var tAnyDeath = sm.AddAnyStateTransition(stDeath);
            tAnyDeath.AddCondition(AnimatorConditionMode.If, 0, "Die");
            tAnyDeath.hasExitTime = false; tAnyDeath.duration = 0.1f;

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            Debug.Log("[MechSetup] AC_BossMech controller successfully built with all 32 curated mechanical animations.");
            return ctrl;
        }

        public static AnimatorController BuildSamuraiController()
        {
            AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(SamuraiControllerPath);
            if (ctrl == null)
                ctrl = AnimatorController.CreateAnimatorControllerAtPath(SamuraiControllerPath);

            AnimationClip clipIdle     = LoadClip($"{GSPPath}/great sword idle (3).fbx");
            AnimationClip clipIdle2    = LoadClip($"{GSPPath}/great sword idle (4).fbx");
            AnimationClip clipWalk     = LoadClip($"{GSPPath}/great sword walk.fbx");
            AnimationClip clipRun      = LoadClip($"{GSPPath}/great sword run (2).fbx");
            AnimationClip clipSlash    = LoadClip($"{GSPPath}/great sword slash (3).fbx");
            AnimationClip clipSlash2   = LoadClip($"{GSPPath}/great sword slash (4).fbx");
            AnimationClip clipDash     = LoadClip($"{GSPPath}/great sword slide attack.fbx");
            AnimationClip clipWhirl    = LoadClip($"{GSPPath}/great sword high spin attack.fbx");
            AnimationClip clipBlock    = LoadClip($"{GSPPath}/great sword blocking.fbx");
            AnimationClip clipPunish   = LoadClip($"{GSPPath}/great sword kick.fbx");
            AnimationClip clipBerserk  = LoadClip($"{GSPPath}/great sword power up.fbx");
            AnimationClip clipImpact   = LoadClip($"{GSPPath}/great sword impact (2).fbx");
            AnimationClip clipDeath    = LoadClip($"{GSPPath}/two handed sword death (2).fbx");

            while (ctrl.parameters.Length > 0) ctrl.RemoveParameter(0);
            ctrl.AddParameter("Speed",          AnimatorControllerParameterType.Float);
            ctrl.AddParameter("KatanaSlash",    AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("IaidoDash",      AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("BladeWhirlwind", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("CounterStance",  AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("CounterPunish",  AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Berserk",        AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Stagger",        AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Die",            AnimatorControllerParameterType.Trigger);

            var sm = ctrl.layers[0].stateMachine;
            foreach (var cs in sm.states) sm.RemoveState(cs.state);
            foreach (var at in sm.anyStateTransitions) sm.RemoveAnyStateTransition(at);

            var blendTree = new BlendTree
            {
                name = "LocomotionTree",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = false
            };
            if (clipIdle != null) blendTree.AddChild(clipIdle, 0f);
            if (clipWalk != null) blendTree.AddChild(clipWalk, 1f);
            if (clipRun  != null) blendTree.AddChild(clipRun,  2f);
            AssetDatabase.AddObjectToAsset(blendTree, ctrl);

            var stateLoco = sm.AddState("Locomotion", new Vector3(200, 100));
            stateLoco.motion = blendTree;
            sm.defaultState = stateLoco;

            var stateIdle = sm.AddState("Idle", new Vector3(200, 200));
            stateIdle.motion = clipIdle2 ?? clipIdle;

            var tLocoIdle = stateLoco.AddTransition(stateIdle);
            tLocoIdle.AddCondition(AnimatorConditionMode.Less, 0.08f, "Speed");
            tLocoIdle.hasExitTime = false; tLocoIdle.duration = 0.2f;

            var tIdleLoco = stateIdle.AddTransition(stateLoco);
            tIdleLoco.AddCondition(AnimatorConditionMode.Greater, 0.08f, "Speed");
            tIdleLoco.hasExitTime = false; tIdleLoco.duration = 0.2f;

            void ConnectAttack(AnimatorState atk, string trigger, float exitTime = 1.0f, float blendDur = 0.12f)
            {
                var tI = stateIdle.AddTransition(atk);
                tI.AddCondition(AnimatorConditionMode.If, 0, trigger);
                tI.hasExitTime = false; tI.duration = blendDur;

                var tL = stateLoco.AddTransition(atk);
                tL.AddCondition(AnimatorConditionMode.If, 0, trigger);
                tL.hasExitTime = false; tL.duration = blendDur;

                var tBack = atk.AddTransition(stateIdle);
                tBack.hasExitTime = true;
                tBack.exitTime = exitTime;
                tBack.duration = 0.12f;
            }

            var stSlash   = sm.AddState("KatanaSlash",   new Vector3(550, 0));
            stSlash.motion = clipSlash; ConnectAttack(stSlash, "KatanaSlash");

            var stDash    = sm.AddState("IaidoDash",     new Vector3(550, 80));
            stDash.motion = clipDash; ConnectAttack(stDash, "IaidoDash");

            var stWhirl   = sm.AddState("BladeWhirlwind", new Vector3(550, 160));
            stWhirl.motion = clipWhirl; ConnectAttack(stWhirl, "BladeWhirlwind");

            var stCounter = sm.AddState("CounterStance", new Vector3(550, 240));
            stCounter.motion = clipBlock; ConnectAttack(stCounter, "CounterStance");

            var stPunish  = sm.AddState("CounterPunish", new Vector3(550, 320));
            stPunish.motion = clipPunish; ConnectAttack(stPunish, "CounterPunish");

            var stBerserk = sm.AddState("Berserk",       new Vector3(550, 400));
            stBerserk.motion = clipBerserk; ConnectAttack(stBerserk, "Berserk");

            var stStagger = sm.AddState("Stagger",       new Vector3(200, -80));
            stStagger.motion = clipImpact;
            var tAnyS = sm.AddAnyStateTransition(stStagger);
            tAnyS.AddCondition(AnimatorConditionMode.If, 0, "Stagger");
            tAnyS.hasExitTime = false; tAnyS.duration = 0.1f;
            var tSBack = stStagger.AddTransition(stateIdle);
            tSBack.hasExitTime = true; tSBack.exitTime = 1.0f; tSBack.duration = 0.15f;

            var stDeath = sm.AddState("Death",           new Vector3(200, 500));
            stDeath.motion = clipDeath;
            var tAnyD = sm.AddAnyStateTransition(stDeath);
            tAnyD.AddCondition(AnimatorConditionMode.If, 0, "Die");
            tAnyD.hasExitTime = false; tAnyD.duration = 0.1f;

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            Debug.Log("[MechSetup] AC_SamuraiMech controller built with GreatSwordPack mech animations.");
            return ctrl;
        }
    }
}
