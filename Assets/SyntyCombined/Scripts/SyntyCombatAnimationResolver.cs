using UnityEngine;

namespace SyntyCombined
{
    public enum CardinalDirection
    {
        Front = 0,
        Back  = 1,
        Left  = 2,
        Right = 3
    }

    public enum KnockdownPhase
    {
        Begin = 0,
        Loop  = 1,
        End   = 2
    }

    public enum StunPhase
    {
        Begin = 0,
        Loop  = 1,
        End   = 2
    }

    public enum IdlePersonalityType
    {
        Base           = 0,
        Sheathed       = 1,
        Energetic      = 2,
        Flourish       = 3,
        MenacingBegin  = 4,
        MenacingLoop   = 5,
        MenacingEnd    = 6
    }

    public enum SpecialAttackType
    {
        HeavyFlourish,
        HeavyStab,
        LightFencing,
        LightLeaping,
        SpinningCyclone,
        OverheadSlam,
        ShoulderCharge
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // SyntyCombatAnimationResolver.cs
    //
    // Centralized single source of truth for all combat animation queries across both
    // Player and Enemy AI entities.
    //
    // Deterministic selection:
    //   - Dominant axis selection for directional hit flinches, staggers, and deaths.
    //   - Clean phase lookup for Knockdown and Stun states.
    //   - Standardized attack and defense mappings.
    // ─────────────────────────────────────────────────────────────────────────────
    public static class SyntyCombatAnimationResolver
    {
        /// <summary>
        /// Converts a local space 3D vector (e.g. hit normal / attacker relative direction)
        /// into the dominant CardinalDirection (Front, Back, Left, Right).
        /// </summary>
        public static CardinalDirection GetDominantDirection(Vector3 localDir)
        {
            float absX = Mathf.Abs(localDir.x);
            float absZ = Mathf.Abs(localDir.z);

            if (absZ >= absX)
                return localDir.z >= 0f ? CardinalDirection.Front : CardinalDirection.Back;
            else
                return localDir.x >= 0f ? CardinalDirection.Right : CardinalDirection.Left;
        }

        /// <summary>
        /// Converts a 2D movement input vector (X=Horizontal, Y=Vertical) into CardinalDirection.
        /// </summary>
        public static CardinalDirection GetMoveDirection(Vector2 input)
        {
            if (input.sqrMagnitude < 0.001f)
                return CardinalDirection.Back;

            float absX = Mathf.Abs(input.x);
            float absY = Mathf.Abs(input.y);

            if (absY >= absX)
                return input.y >= 0f ? CardinalDirection.Front : CardinalDirection.Back;
            else
                return input.x >= 0f ? CardinalDirection.Right : CardinalDirection.Left;
        }

        /// <summary>
        /// Returns the dodge step or dodge roll clip entry for the given cardinal direction.
        /// </summary>
        public static SyntyAnimClipData.ClipEntry GetDodge(SyntyAnimClipData clips, CardinalDirection dir, bool isRoll)
        {
            if (clips == null) return default;

            if (isRoll)
            {
                switch (dir)
                {
                    case CardinalDirection.Front: return clips.dodgeRollForward;
                    case CardinalDirection.Back:  return clips.dodgeRollBack;
                    case CardinalDirection.Left:  return clips.dodgeRollLeft;
                    case CardinalDirection.Right: return clips.dodgeRollRight;
                    default: return clips.dodgeRollBack;
                }
            }
            else
            {
                switch (dir)
                {
                    case CardinalDirection.Front: return clips.dodgeForward;
                    case CardinalDirection.Back:  return clips.dodgeBack;
                    case CardinalDirection.Left:  return clips.dodgeLeft;
                    case CardinalDirection.Right: return clips.dodgeRight;
                    default: return clips.dodgeBack;
                }
            }
        }

        /// <summary>
        /// Returns the short hit reaction flinch clip for the given cardinal direction.
        /// </summary>
        public static SyntyAnimClipData.ClipEntry GetHitReaction(SyntyAnimClipData clips, CardinalDirection dir)
        {
            if (clips == null) return default;
            switch (dir)
            {
                case CardinalDirection.Front: return clips.hitReactForward;
                case CardinalDirection.Back:  return clips.hitReactBack;
                case CardinalDirection.Left:  return clips.hitReactLeft;
                case CardinalDirection.Right: return clips.hitReactRight;
                default: return clips.hitReactBack;
            }
        }

        /// <summary>
        /// Returns the heavy hit stagger clip for the given cardinal direction.
        /// </summary>
        public static SyntyAnimClipData.ClipEntry GetHitStagger(SyntyAnimClipData clips, CardinalDirection dir)
        {
            if (clips == null) return default;
            switch (dir)
            {
                case CardinalDirection.Front: return clips.hitStaggerForward;
                case CardinalDirection.Back:  return clips.hitStaggerBack;
                case CardinalDirection.Left:  return clips.hitStaggerLeft;
                case CardinalDirection.Right: return clips.hitStaggerRight;
                default: return clips.hitStaggerBack;
            }
        }

        /// <summary>
        /// Returns the death animation clip for the given cardinal direction.
        /// </summary>
        public static AnimationClip GetDeath(SyntyAnimClipData clips, CardinalDirection dir)
        {
            if (clips == null) return null;
            switch (dir)
            {
                case CardinalDirection.Front: return clips.deathForward != null ? clips.deathForward : clips.deathBack;
                case CardinalDirection.Back:  return clips.deathBack;
                case CardinalDirection.Left:  return clips.deathLeft != null ? clips.deathLeft : clips.deathBack;
                case CardinalDirection.Right: return clips.deathRight != null ? clips.deathRight : clips.deathBack;
                default: return clips.deathBack;
            }
        }

        /// <summary>
        /// Returns the knockdown clip for Begin, Loop, or End phases.
        /// </summary>
        public static AnimationClip GetKnockdown(SyntyAnimClipData clips, KnockdownPhase phase)
        {
            if (clips == null) return null;
            switch (phase)
            {
                case KnockdownPhase.Begin: return clips.knockDownBegin;
                case KnockdownPhase.Loop:  return clips.knockDownLoop;
                case KnockdownPhase.End:   return clips.knockDownEnd;
                default: return clips.knockDownLoop;
            }
        }

        /// <summary>
        /// Returns the stun clip for Begin, Loop, or End phases.
        /// </summary>
        public static AnimationClip GetStun(SyntyAnimClipData clips, StunPhase phase)
        {
            if (clips == null) return null;
            switch (phase)
            {
                case StunPhase.Begin: return clips.stunBegin;
                case StunPhase.Loop:  return clips.stunLoop;
                case StunPhase.End:   return clips.stunEnd;
                default: return clips.stunLoop;
            }
        }

        /// <summary>
        /// Returns the idle or personality flourish clip.
        /// </summary>
        public static AnimationClip GetIdle(SyntyAnimClipData clips, IdlePersonalityType type)
        {
            if (clips == null) return null;
            switch (type)
            {
                case IdlePersonalityType.Base:          return clips.swordIdleBase != null ? clips.swordIdleBase : clips.idleStanding;
                case IdlePersonalityType.Sheathed:      return clips.swordIdleSheathed != null ? clips.swordIdleSheathed : clips.idleStanding;
                case IdlePersonalityType.Energetic:     return clips.swordIdleEnergetic;
                case IdlePersonalityType.Flourish:      return clips.swordIdleFlourish;
                case IdlePersonalityType.MenacingBegin: return clips.swordIdleMenacingBegin != null ? clips.swordIdleMenacingBegin : clips.swordIdleFlourish;
                case IdlePersonalityType.MenacingLoop:  return clips.swordIdleMenacingLoop != null ? clips.swordIdleMenacingLoop : clips.swordIdleBase;
                case IdlePersonalityType.MenacingEnd:   return clips.swordIdleMenacingEnd != null ? clips.swordIdleMenacingEnd : clips.swordIdleBase;
                default: return clips.swordIdleBase;
            }
        }

        /// <summary>
        /// Returns a light combo hit (step 1, 2, or 3).
        /// </summary>
        public static SyntyAnimClipData.ClipEntry GetLightAttack(SyntyAnimClipData clips, int step)
        {
            if (clips == null) return default;
            step = ((step - 1) % 3) + 1;
            return step == 1 ? clips.lightComboA : step == 2 ? clips.lightComboB : clips.lightComboC;
        }

        /// <summary>
        /// Returns a heavy combo hit (step 1, 2, or 3).
        /// </summary>
        public static SyntyAnimClipData.ClipEntry GetHeavyAttack(SyntyAnimClipData clips, int step)
        {
            if (clips == null) return default;
            step = ((step - 1) % 3) + 1;
            return step == 1 ? clips.heavyComboA : step == 2 ? clips.heavyComboB : clips.heavyComboC;
        }

        /// <summary>
        /// Returns special attack clip entries.
        /// </summary>
        public static SyntyAnimClipData.ClipEntry GetSpecialAttack(SyntyAnimClipData clips, SpecialAttackType type)
        {
            if (clips == null) return default;
            switch (type)
            {
                case SpecialAttackType.HeavyFlourish:   return clips.heavyFlourish;
                case SpecialAttackType.HeavyStab:       return clips.heavyStab;
                case SpecialAttackType.LightFencing:    return clips.lightFencing;
                case SpecialAttackType.LightLeaping:    return clips.lightLeaping;
                case SpecialAttackType.SpinningCyclone: return clips.spinningCyclone.clip != null ? clips.spinningCyclone : clips.lightComboC;
                case SpecialAttackType.OverheadSlam:    return clips.overheadSlam.clip != null ? clips.overheadSlam : clips.heavyComboC;
                case SpecialAttackType.ShoulderCharge:  return clips.shoulderCharge.clip != null ? clips.shoulderCharge : clips.lightLeaping;
                default: return clips.heavyFlourish;
            }
        }

        /// <summary>
        /// Returns directional parry clip entries.
        /// </summary>
        public static SyntyAnimClipData.ClipEntry GetParry(SyntyAnimClipData clips, CardinalDirection dir)
        {
            if (clips == null) return default;
            switch (dir)
            {
                case CardinalDirection.Left:  return clips.parryLeft.clip != null ? clips.parryLeft : clips.parryForward;
                case CardinalDirection.Right: return clips.parryRight.clip != null ? clips.parryRight : clips.parryForward;
                default: return clips.parryForward;
            }
        }
    }
}
