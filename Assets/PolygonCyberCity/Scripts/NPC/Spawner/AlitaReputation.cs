using UnityEngine;
using Cyberpunk.Missions;

namespace PolygonCyberCity.NPC.Spawner
{
    /// <summary>
    /// City-wide reputation of Alita. She is a stranger with no registry tag, so a
    /// share of the population is hostile to her until she defeats Vulcan in the
    /// Pit Arena (Mission 4); after that nobody starts a fight with her.
    /// </summary>
    public static class AlitaReputation
    {
        /// <summary>Fraction of eligible NPCs that attack Alita on sight. Zero: nobody starts a fight, NPCs only react once she attacks.</summary>
        public const float HostileFraction = 0f;

        /// <summary>Chance that an NPC fights back (instead of fleeing) once Alita hits it.</summary>
        public const float FightBackFraction = 0.50f;

        /// <summary>
        /// Set once Ido has warned Alita about the city. Until then nobody attacks her.
        /// </summary>
        public static bool CityWarningGiven { get; set; }

        /// <summary>True while NPCs are allowed to attack Alita.</summary>
        public static bool NpcsMayAttack => CityWarningGiven && !IsKnownHero;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => CityWarningGiven = false;

        public static bool IsKnownHero => Mission4_VulcanShowdownManager.IsVulcanDefeated;

        public static bool RollHostile() => !IsKnownHero && Random.value < HostileFraction;

    }
}
