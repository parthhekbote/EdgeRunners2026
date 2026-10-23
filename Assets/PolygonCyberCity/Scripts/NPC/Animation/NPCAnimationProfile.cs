using UnityEngine;
using PolygonCyberCity.NPC.Spawner;

namespace PolygonCyberCity.NPC.Animation
{
    public enum NPCActivityType
    {
        None, Phone, Eat, Drink, Smoke, Talk, Sit, Lean, Shop, Inspect,
        Work, UseTerminal, VendingMachine, TakePhoto, WatchAdvertisement, Wait, Shelter
    }

    [CreateAssetMenu(fileName = "NPCAnimationProfile", menuName = "CyberCity/NPC Animation Profile")]
    public class NPCAnimationProfile : ScriptableObject
    {
        [Header("Archetype Identification")]
        public NPCType targetArchetype = NPCType.Civilian;

        [Header("Combat Clip Set")]
        public SyntyCombined.SyntyAnimClipData combatClipData;

        [Header("Locomotion Tuning")]
        public float walkSpeed = 1.8f;
        public float runSpeed = 5.2f;
        public float sprintSpeed = 7.0f;
        public bool hasCombatLocomotion = false;

        [Header("Defensive Capabilities")]
        public bool canBlock = false;
        public bool canParry = false;
        public bool canDodge = false;
        [Range(0f, 1f)] public float blockChance = 0f;
        [Range(0f, 1f)] public float parryChance = 0f;
        [Range(0f, 1f)] public float dodgeChance = 0f;

        [Header("Activity and Fidget")]
        public NPCActivityType[] allowedActivities;
        public float idleFidgetIntervalMin = 4f;
        public float idleFidgetIntervalMax = 10f;
        public int idleVariantCount = 3;

        [Header("Speed Variation")]
        [Range(0.8f, 1.2f)] public float animSpeedVariationMin = 0.95f;
        [Range(0.8f, 1.2f)] public float animSpeedVariationMax = 1.05f;

        // ── ENHANCED DEATH SYSTEM ────────────────────────────────────────────

        [Header("Death - Directional Animation Variants")]
        [Tooltip("Pool of death clips for a front hit (NPC falls forward). One is randomly chosen.")]
        public AnimationClip[] deathClipsFront;
        [Tooltip("Pool of death clips for a back hit (NPC falls backward). One is randomly chosen.")]
        public AnimationClip[] deathClipsBack;
        [Tooltip("Pool of death clips for a left-side hit. One is randomly chosen.")]
        public AnimationClip[] deathClipsLeft;
        [Tooltip("Pool of death clips for a right-side hit. One is randomly chosen.")]
        public AnimationClip[] deathClipsRight;

        [Header("Death - Knockdown-to-Death Sequence")]
        [Tooltip("Heavy lethal hits play a stagger clip first, then collapse into death for cinematic weight.")]
        public bool useKnockdownSequenceOnHeavyHit = true;
        [Tooltip("Animator state name for the stagger/kneel clip played before the death clip on heavy hits.")]
        public string knockdownToDeathStateName = "Knockdown";
        [Tooltip("Seconds to wait after the knockdown clip starts before crossfading into the death clip.")]
        [Range(0.1f, 2.0f)] public float knockdownToDeathDelay = 0.65f;

        [Header("Death - Cinematic Hit-Pause")]
        [Tooltip("Briefly drops TimeScale on the lethal-hit frame for cinematic impact weight.")]
        public bool enableHitPause = true;
        [Tooltip("TimeScale during the freeze (0 = full stop, 0.05 = near-freeze, 0.2 = slow-mo).")]
        [Range(0f, 0.2f)] public float hitPauseTimeScale = 0.04f;
        [Tooltip("Real-time duration of the freeze in seconds (unscaled).")]
        [Range(0.02f, 0.18f)] public float hitPauseDuration = 0.07f;

        [Header("Death - Audio")]
        [Tooltip("Death grunt / last-breath audio. One is randomly chosen on death.")]
        public AudioClip[] deathSounds;
        [Tooltip("Ground thud / impact sound played when the body settles.")]
        public AudioClip[] groundImpactSounds;

        [Header("Death - VFX")]
        [Tooltip("Particle burst at the hit point on a lethal impact (blood, sparks, etc).")]
        public GameObject deathImpactVFX;
        [Tooltip("Cyberpunk dissolve / glitch VFX that plays when the NPC sinks into the ground.")]
        public GameObject dissolveSinkVFX;

        // ── Runtime helpers ──────────────────────────────────────────────────

        /// <summary>
        /// Returns a random AnimationClip from the directional pool for the given hit direction.
        /// Falls back through other direction pools if the matching one is empty.
        /// </summary>
        public AnimationClip GetRandomDeathClip(SyntyCombined.CardinalDirection dir)
        {
            AnimationClip[] pool = dir switch
            {
                SyntyCombined.CardinalDirection.Front => deathClipsFront,
                SyntyCombined.CardinalDirection.Left  => deathClipsLeft,
                SyntyCombined.CardinalDirection.Right => deathClipsRight,
                _                                     => deathClipsBack
            };
            AnimationClip c = PickRandom(pool);
            if (c != null) return c;
            AnimationClip[][] fallbacks = { deathClipsBack, deathClipsFront, deathClipsLeft, deathClipsRight };
            foreach (var p in fallbacks) { c = PickRandom(p); if (c != null) return c; }
            return null;
        }

        /// <summary>Returns a random death grunt / last-breath sound, or null.</summary>
        public AudioClip GetRandomDeathSound()    => PickRandom(deathSounds);

        /// <summary>Returns a random ground impact thud sound, or null.</summary>
        public AudioClip GetRandomGroundImpact() => PickRandom(groundImpactSounds);

        // ── Private generic pickers ──────────────────────────────────────────

        private static AnimationClip PickRandom(AnimationClip[] pool)
        {
            if (pool == null || pool.Length == 0) return null;
            int count = 0;
            for (int i = 0; i < pool.Length; i++) if (pool[i] != null) count++;
            if (count == 0) return null;
            int target = UnityEngine.Random.Range(0, count), seen = 0;
            for (int i = 0; i < pool.Length; i++)
            {
                if (pool[i] == null) continue;
                if (seen == target) return pool[i];
                seen++;
            }
            return null;
        }

        private static AudioClip PickRandom(AudioClip[] pool)
        {
            if (pool == null || pool.Length == 0) return null;
            int count = 0;
            for (int i = 0; i < pool.Length; i++) if (pool[i] != null) count++;
            if (count == 0) return null;
            int target = UnityEngine.Random.Range(0, count), seen = 0;
            for (int i = 0; i < pool.Length; i++)
            {
                if (pool[i] == null) continue;
                if (seen == target) return pool[i];
                seen++;
            }
            return null;
        }
    }
}
