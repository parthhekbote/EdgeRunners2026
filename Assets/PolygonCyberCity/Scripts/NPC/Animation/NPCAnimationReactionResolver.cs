using UnityEngine;

namespace PolygonCyberCity.NPC.Animation
{
    public enum ReactionType
    {
        Flinch = 0,
        Stagger = 1,
        Knockdown = 2,
        GuardBreak = 3,
        Death = 4
    }

    public static class NPCAnimationReactionResolver
    {
        /// <summary>
        /// Resolves the appropriate physical reaction type based on damage, health, posture, and attack impact.
        /// </summary>
        public static ReactionType Resolve(float damage, float currentHealth, float maxHealth, bool isHeavy, bool isBlocking, float postureDamage, float maxPosture)
        {
            if (currentHealth - damage <= 0f)
            {
                return ReactionType.Death;
            }

            if (isBlocking && postureDamage >= maxPosture)
            {
                return ReactionType.GuardBreak;
            }

            float damageRatio = maxHealth > 0f ? (damage / maxHealth) : 0f;

            if (isHeavy && damageRatio > 0.25f)
            {
                return ReactionType.Knockdown;
            }

            if (isHeavy || damageRatio > 0.15f)
            {
                return ReactionType.Stagger;
            }

            return ReactionType.Flinch;
        }
    }
}
