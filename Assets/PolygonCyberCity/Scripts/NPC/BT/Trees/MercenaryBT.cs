using UnityEngine;
using UnityEngine.AI;
using PolygonCyberCity.NPC.BT.Tasks;
using PolygonCyberCity.NPC.Spawner;

namespace PolygonCyberCity.NPC.BT.Trees
{
    /// <summary>
    /// Behavior Tree Builder for Hostile Mercenaries, Cyber-Gangers, and Security Guards.
    /// Priority 1: Morale Break / Tactical Retreat if Critical HP (<25%).
    /// Priority 2: Aggressive Combat (Tactical Strafe, Combo Attack, Weapon Engage).
    /// Priority 3: Suspicious Warning / Security Challenge.
    /// Priority 4: Ambient Patrol & Guard Loitering.
    /// </summary>
    public static class MercenaryBT
    {
        public static BTNode Build(GameObject owner, NavMeshAgent agent, Animator animator, CyberNPCController controller)
        {
            // --- BRANCH 1: MORALE BREAK / RETREAT ON CRITICAL HEALTH ---
            var retreatBranch = new ConditionNode(
                bb => (bb.Health < 25f || AlitaReputation.IsKnownHero) && bb.WasAttacked,
                new TaskPanicFlee(agent, animator, controller, fleeDist: 25f, sprintSpeed: 5.5f)
            );

            // --- BRANCH 2: AGGRESSIVE COMBAT ENGAGEMENT ---
            var combatBranch = new ConditionNode(
                bb => !AlitaReputation.IsKnownHero && (bb.CurrentState == NPCMacroState.Aggressive || bb.WasAttacked),
                new Selector(
                    // 2A: Strike player if in close range with cooldown
                    new Sequence(
                        new ConditionNode(bb => bb.DistanceToPlayer <= 2.8f),
                        new Cooldown(1.5f, new TaskExecuteAttack(agent, animator, controller, range: 2.5f, duration: 1.1f, damage: 18f))
                    ),
                    // 2B: Tactical strafe and close distance
                    new TaskTacticalReposition(agent, animator, preferredDist: 2.2f)
                )
            );

            // --- BRANCH 3: SUSPICIOUS WARNING & CHALLENGE ---
            var warningBranch = new ConditionNode(
                bb => (bb.CurrentState == NPCMacroState.Suspicious || (bb.IsPlayerNear && bb.DistanceToPlayer < 7.0f && bb.WeaponDrawn)),
                new TaskIssueWarning(agent, animator, controller)
            );

            // --- BRANCH 4: AMBIENT PATROL & LOITERING ---
            var ambientBranch = new Selector(
                new TaskLoiterSocialize(agent, animator, 5.0f, 10.0f),
                new TaskWanderStreet(agent, animator, wanderRadius: 35f)
            );

            // --- ROOT SELECTOR ---
            var root = new Selector(
                retreatBranch,
                combatBranch,
                warningBranch,
                ambientBranch
            );

            return root;
        }
    }
}
