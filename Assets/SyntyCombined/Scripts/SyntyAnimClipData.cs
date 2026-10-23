using System;
using UnityEngine;

namespace SyntyCombined
{
    // ─────────────────────────────────────────────────────────────────────────────
    // SyntyAnimClipData.cs
    //
    // ScriptableObject that holds every AnimationClip reference for the combined
    // Synty Locomotion + Sword Combat controller.
    //
    // No string names, no hardcoded durations: everything is read directly from the
    // assigned AnimationClip assets (clip.length) at runtime.
    //
    // Create via:  Assets → Create → SyntyCombined → Anim Clip Data
    // ─────────────────────────────────────────────────────────────────────────────

    [CreateAssetMenu(
        fileName = "SyntyAnimClipData",
        menuName  = "SyntyCombined/Anim Clip Data",
        order     = 1)]
    public class SyntyAnimClipData : ScriptableObject
    {
        // ── Nested data types ────────────────────────────────────────────────────

        /// <summary>
        /// A single animation clip together with its authored timing.
        /// Duration is auto-read from clip.length; the two window offsets let the
        /// controller know when to allow combo chaining and when the player can
        /// cancel into movement.
        /// </summary>
        [Serializable]
        public struct ClipEntry
        {
            [Tooltip("The AnimationClip FBX sub-asset.")]
            public AnimationClip clip;

            [Tooltip("Normalised time [0..1] at which the NEXT combo hit may be chained in.")]
            [Range(0f, 1f)]
            public float comboWindowNorm;

            [Tooltip("Normalised time [0..1] at which the player may cancel into movement.")]
            [Range(0f, 1f)]
            public float exitWindowNorm;

            /// <summary>Clip duration in seconds (read from clip asset; never hardcoded).</summary>
            public float Duration => clip != null ? clip.length : 1f;

            /// <summary>Absolute time (s) at which combo input is accepted.</summary>
            public float ComboWindowSec => Duration * comboWindowNorm;

            /// <summary>Absolute time (s) at which movement cancel is allowed.</summary>
            public float ExitWindowSec  => Duration * exitWindowNorm;
        }

        /// <summary>
        /// An ordered chain of ClipEntries that form one combo sequence.
        /// Combos loop: after the last hit the index resets to 0.
        /// </summary>
        [Serializable]
        public struct ComboChain
        {
            public string chainName;
            public ClipEntry[] hits;
        }

        // ── Animator Parameter Names ─────────────────────────────────────────────
        // Stored as strings so you can match them to whatever controller you use.
        // The controller caches them as int hashes at Awake time.
        [Header("Animator Parameter Names (must match your Animator Controller)")]
        public string paramMoveSpeed           = "MoveSpeed";
        public string paramCurrentGait         = "CurrentGait";   // Int: 0=Walk 1=Run 2=Sprint
        public string paramIsWalking           = "IsWalking";
        public string paramIsCrouching         = "IsCrouching";
        public string paramIsGrounded          = "IsGrounded";
        public string paramIsJumping           = "IsJumping";
        public string paramStrafeX             = "StrafeDirectionX";
        public string paramStrafeZ             = "StrafeDirectionZ";
        public string paramIsStrafing          = "IsStrafing";
        public string paramLeanValue           = "LeanValue";
        public string paramFallingDuration     = "FallingDuration";
        public string paramMovementInputHeld   = "MovementInputHeld";
        public string paramMovementInputTapped = "MovementInputTapped";
        public string paramIsStopped           = "IsStopped";
        public string paramIsStarting          = "IsStarting";
        public string paramSwordDrawn          = "SwordDrawn";
        public string paramSwordDrawnWeight    = "SwordDrawnWeight";
        public string paramIsSliding           = "IsSliding";
        public string paramCombatState         = "CombatState";
        public string paramAttackID            = "AttackID";
        public string paramDodgeDirection      = "DodgeDirection";
        public string paramHitDirection        = "HitDirection";
        // Combat layer weight is driven via Animator.SetLayerWeight, not a parameter.

        // ── Locomotion Clips (Polygon) ────────────────────────────────────────────
        [Header("Locomotion – Idle")]
        public AnimationClip idleStanding;
        public AnimationClip idleCrouching;

        [Header("Locomotion – Walk (forward & strafes)")]
        public AnimationClip walkForward;
        public AnimationClip walkStrafeL;
        public AnimationClip walkStrafeR;
        public AnimationClip walkStrafeFL;
        public AnimationClip walkStrafeFR;
        public AnimationClip walkStrafeBL;
        public AnimationClip walkStrafeBR;
        public AnimationClip walkBackward;

        [Header("Locomotion – Run (forward & strafes)")]
        public AnimationClip runForward;
        public AnimationClip runStrafeL;
        public AnimationClip runStrafeR;
        public AnimationClip runStrafeFL;
        public AnimationClip runStrafeFR;
        public AnimationClip runStrafeBL;
        public AnimationClip runStrafeBR;
        public AnimationClip runBackward;

        [Header("Locomotion – Sprint & Slide")]
        public AnimationClip sprintForward;
        public AnimationClip sprintUp25;
        public AnimationClip sprintDown25;
        public AnimationClip sprintToCrouch;

        [Header("Locomotion – Crouch Walk (forward & strafes)")]
        public AnimationClip crouchWalkForward;
        public AnimationClip crouchWalkStrafeL;
        public AnimationClip crouchWalkStrafeR;
        public AnimationClip crouchWalkBackward;

        [Header("Locomotion – In-Air")]
        public AnimationClip jumpIdle;
        public AnimationClip jumpWalking;
        public AnimationClip jumpRunning;
        public AnimationClip jumpSprinting;
        public AnimationClip fallShort;
        public AnimationClip fallLarge;
        public AnimationClip landSoft;
        public AnimationClip landMedium;
        public AnimationClip landHard;

        [Header("Locomotion – Transitions")]
        public AnimationClip idleToRunForward;
        public AnimationClip idleToRun90L;
        public AnimationClip idleToRun90R;
        public AnimationClip idleToRun180L;
        public AnimationClip idleToRun180R;
        public AnimationClip idleToWalkForward;
        public AnimationClip runToIdleLFoot;
        public AnimationClip runToIdleRFoot;
        public AnimationClip walkToIdleLFoot;
        public AnimationClip walkToIdleRFoot;
        public AnimationClip standToCrouch;
        public AnimationClip crouchToStand;

        // ── Armed Locomotion Clips (Synty MOD_BL) ──────────────────────────────────
        [Header("Armed Locomotion – Idle (MOD_BL)")]
        public AnimationClip armedIdleStanding;

        [Header("Armed Locomotion – Walk (MOD_BL forward & strafes)")]
        public AnimationClip armedWalkForward;
        public AnimationClip armedWalkStrafeL;
        public AnimationClip armedWalkStrafeR;
        public AnimationClip armedWalkStrafeFL;
        public AnimationClip armedWalkStrafeFR;
        public AnimationClip armedWalkStrafeBL;
        public AnimationClip armedWalkStrafeBR;
        public AnimationClip armedWalkBackward;

        [Header("Armed Locomotion – Run (MOD_BL forward & strafes)")]
        public AnimationClip armedRunForward;
        public AnimationClip armedRunStrafeL;
        public AnimationClip armedRunStrafeR;
        public AnimationClip armedRunStrafeFL;
        public AnimationClip armedRunStrafeFR;
        public AnimationClip armedRunStrafeBL;
        public AnimationClip armedRunStrafeBR;
        public AnimationClip armedRunBackward;

        [Header("Armed Locomotion – Sprint & Crouch (MOD_BL)")]
        public AnimationClip armedSprintForward;
        public AnimationClip armedCrouchIdle;
        public AnimationClip armedCrouchWalkForward;
        public AnimationClip armedCrouchWalkStrafeL;
        public AnimationClip armedCrouchWalkStrafeR;
        public AnimationClip armedCrouchWalkBackward;

        // ── Sword Idle / Sheathed ─────────────────────────────────────────────────
        [Header("Sword – Idle")]
        public AnimationClip swordIdleBase;
        public AnimationClip swordIdleSheathed;
        public AnimationClip swordIdleEnergetic;
        public AnimationClip swordIdleFlourish;
        public AnimationClip swordIdleMenacingBegin;
        public AnimationClip swordIdleMenacingLoop;
        public AnimationClip swordIdleMenacingEnd;

        [Header("Sword – Draw & Sheathe (Feminine)")]
        [Tooltip("A_Draw_Sword_Femn.fbx")]
        public AnimationClip drawSword;
        [Tooltip("A_Sheathe_Sword_Femn.fbx")]
        public AnimationClip sheatheSword;

        [Tooltip("Normalised time at which the sword moves from hip to hand during Draw.")]
        [Range(0f, 1f)]
        public float drawSwapNormTime   = 0.35f;

        [Tooltip("Normalised time at which the sword moves from hand to hip during Sheathe.")]
        [Range(0f, 1f)]
        public float sheatheSwapNormTime = 0.50f;

        // ── Light Attack Combos ───────────────────────────────────────────────────
        [Header("Sword – Light Combo (LightCombo01 A/B/C)")]
        public ClipEntry lightComboA;   // A_Attack_LightCombo01A_Sword
        public ClipEntry lightComboB;   // A_Attack_LightCombo01B_Sword
        public ClipEntry lightComboC;   // A_Attack_LightCombo01C_Sword (finisher)

        // ── Heavy Attack Combos ───────────────────────────────────────────────────
        [Header("Sword – Heavy Combo (HeavyCombo01 A/B/C)")]
        public ClipEntry heavyComboA;   // A_Attack_HeavyCombo01A_Sword
        public ClipEntry heavyComboB;   // A_Attack_HeavyCombo01B_Sword
        public ClipEntry heavyComboC;   // A_Attack_HeavyCombo01C_Sword (finisher)

        // ── Special Attacks ───────────────────────────────────────────────────────
        [Header("Sword – Special Attacks")]
        public ClipEntry heavyFlourish;    // A_Attack_HeavyFlourish01_Sword
        public ClipEntry heavyStab;        // A_Attack_HeavyStab01_Sword
        public ClipEntry lightFencing;     // A_Attack_LightFencing01_Sword
        public ClipEntry lightLeaping;     // A_Attack_LightLeaping01_Sword

        [Header("Sword – Cyborg Special Moves")]
        [Tooltip("Spinning cyclone 3-hit arc — assign A_Attack_LightCombo01C_Sword or a custom clip.")]
        public ClipEntry spinningCyclone;  // Fallback: A_Attack_LightCombo01C_Sword
        [Tooltip("Slow overhead slam with huge posture damage — assign A_Attack_HeavyCombo01C_Sword or a custom clip.")]
        public ClipEntry overheadSlam;     // Fallback: A_Attack_HeavyCombo01C_Sword
        [Tooltip("Shoulder charge dash-through — assign A_Attack_LightLeaping01_Sword or a custom clip.")]
        public ClipEntry shoulderCharge;   // Fallback: A_Attack_LightLeaping01_Sword

        // ── Block / Parry ─────────────────────────────────────────────────────────
        [Header("Sword – Block")]
        public AnimationClip blockBegin;
        public AnimationClip blockLoop;
        public AnimationClip blockEnd;

        [Header("Sword – Parry")]
        public ClipEntry parryForward;
        public ClipEntry parryLeft;
        public ClipEntry parryRight;
        public ClipEntry parryBreak;
        public ClipEntry counterShove;
        public ClipEntry pommelStrike;
        public AnimationClip parryReturnToBlock;

        // ── Dodge / Roll ──────────────────────────────────────────────────────────
        [Header("Sword – Dodge (step)")]
        public ClipEntry dodgeForward;
        public ClipEntry dodgeBack;
        public ClipEntry dodgeLeft;
        public ClipEntry dodgeRight;

        [Header("Sword – Dodge Roll")]
        public ClipEntry dodgeRollForward;
        public ClipEntry dodgeRollBack;
        public ClipEntry dodgeRollLeft;
        public ClipEntry dodgeRollRight;

        // ── Hit Reactions ─────────────────────────────────────────────────────────
        [Header("Sword – Hit React (short flinch)")]
        public ClipEntry hitReactForward;
        public ClipEntry hitReactBack;
        public ClipEntry hitReactLeft;
        public ClipEntry hitReactRight;

        [Header("Sword – Hit Stagger (larger reaction)")]
        public ClipEntry hitStaggerForward;
        public ClipEntry hitStaggerBack;
        public ClipEntry hitStaggerLeft;
        public ClipEntry hitStaggerRight;

        // ── Knockdown ─────────────────────────────────────────────────────────────
        [Header("Sword – Knockdown")]
        public AnimationClip knockDownBegin;
        public AnimationClip knockDownLoop;
        public AnimationClip knockDownEnd;

        // ── Stun ──────────────────────────────────────────────────────────────────
        [Header("Sword – Stun")]
        public AnimationClip stunBegin;
        public AnimationClip stunLoop;
        public AnimationClip stunEnd;

        // ── Death ─────────────────────────────────────────────────────────────────
        [Header("Sword – Death")]
        public AnimationClip deathForward;
        public AnimationClip deathBack;
        public AnimationClip deathLeft;
        public AnimationClip deathRight;

        // ── Clip Speed Metadata ───────────────────────────────────────────────────
        // These are the ROOT-MOTION speeds the clips were authored at (m/s).
        // The controller divides actual CC speed by these to get animator.speed.
        [Header("Clip Root Speeds (m/s) – must match authored animation")]
        public float walkClipSpeed   = 1.40f;
        public float runClipSpeed    = 2.50f;
        public float sprintClipSpeed = 7.00f;
        public float crouchClipSpeed = 1.10f;

        // ── Convenience accessors ─────────────────────────────────────────────────

        /// <summary>Returns the ClipEntry for a light combo hit (1-indexed, loops).</summary>
        public ClipEntry GetLightCombo(int step)
        {
            step = ((step - 1) % 3) + 1;
            return step == 1 ? lightComboA : step == 2 ? lightComboB : lightComboC;
        }

        /// <summary>Returns the ClipEntry for a heavy combo hit (1-indexed, loops).</summary>
        public ClipEntry GetHeavyCombo(int step)
        {
            step = ((step - 1) % 3) + 1;
            return step == 1 ? heavyComboA : step == 2 ? heavyComboB : heavyComboC;
        }

        /// <summary>Returns directional dodge based on normalised input direction.</summary>
        public ClipEntry GetDodge(Vector2 dir, bool roll)
        {
            if (roll)
            {
                if (dir.y >=  0.5f) return dodgeRollForward;
                if (dir.y <= -0.5f) return dodgeRollBack;
                return dir.x < 0f   ? dodgeRollLeft : dodgeRollRight;
            }
            else
            {
                if (dir.y >=  0.5f) return dodgeForward;
                if (dir.y <= -0.5f) return dodgeBack;
                return dir.x < 0f   ? dodgeLeft : dodgeRight;
            }
        }

        /// <summary>Returns a random death clip (direction optional: 0=any).</summary>
        public AnimationClip GetDeath(Vector3 hitDir)
        {
            // Rotate hit direction to character-local space
            float dot = hitDir.z; // forward = F, backward = B
            float side = hitDir.x;

            if (Mathf.Abs(dot) >= Mathf.Abs(side))
                return dot >= 0f ? deathBack : deathForward;
            else
                return side >= 0f ? deathRight : deathLeft;
        }
    }
}
