using UnityEngine;
using UnityEngine.AI;
using PolygonCyberCity.NPC.BT.Tasks;
using PolygonCyberCity.NPC.Spawner;

namespace PolygonCyberCity.NPC.BT.Trees
{
    /// <summary>
    /// Behavior Tree Builder for Civilians, Pedestrians, and Rich Citizens.
    /// Priority 1: Danger & Panic (Flee when attacked or near combat, Cower if cornered).
    /// Priority 2: Perception Reaction (Suspicious stop, gunshot duck, explosion flinch).
    /// Priority 3: Casual Player Awareness (Glance/Look at player when passing).
    /// Priority 4: Ambient Urban Routine (Contextual SmartObject activity, shopfront browsing, loitering, sidewalk wandering).
    /// </summary>
    public static class CivilianBT
    {
        public static BTNode Build(GameObject owner, NavMeshAgent agent, Animator animator, CyberNPCController controller)
        {
            // --- BRANCH 0: FIGHT ALITA (hostile locals / civilians who fight back) ---
            var fightBranch = new ConditionNode(
                bb => bb.Get<bool>("FightPlayer") && bb.CurrentState == NPCMacroState.Aggressive && bb.Health >= 35f,
                new Selector(
                    new Sequence(
                        new ConditionNode(bb => bb.DistanceToPlayer <= 2.6f),
                        new Cooldown(1.8f, new TaskExecuteAttack(agent, animator, controller, range: 2.2f, duration: 1.2f, damage: controller.civilianAttackDamage))
                    ),
                    new TaskTacticalReposition(agent, animator, preferredDist: 1.8f)
                )
            );

            // --- BRANCH 1: PANIC & THREAT RESPONSE ---
            var panicBranch = new Selector(
                // Sub-branch A: Cower if cornered or low HP
                new ConditionNode(
                    bb => bb.IsCornered || (bb.Health < 35f && bb.WasAttacked),
                    new TaskCower(agent, animator, controller)
                ),
                // Sub-branch B: Panic sprint away from threat
                new ConditionNode(
                    bb => bb.WasAttacked || bb.CurrentState == NPCMacroState.Fleeing,
                    new TaskPanicFlee(agent, animator, controller, fleeDist: 32f, sprintSpeed: 5.2f)
                )
            );

            // --- BRANCH 2: PERCEPTION REACTION ---
            var perceptionBranch = new ConditionNode(
                bb => bb.CurrentState == NPCMacroState.Suspicious,
                new TaskPerceptionReact(agent, animator, controller)
            );

            // --- BRANCH 3: SOCIAL CONVERSATION ---
            var conversationBranch = new ConditionNode(
                bb => bb.Get<bool>("IsInConversation"),
                new TaskConversation(agent, animator, controller)
            );

            // --- BRANCH 4: PLAYER PROXIMITY AWARENESS ---
            var proximityBranch = new ConditionNode(
                bb => bb.IsPlayerNear && bb.DistanceToPlayer < 4.0f && bb.CurrentState == NPCMacroState.Normal,
                new TaskCasualLookAt(maxDistance: 4.0f)
            );

            // --- BRANCH 5: AMBIENT URBAN ROUTINE ---
            var ambientBranch = new Selector(
                new TaskPerformActivity(agent, animator, controller),
                new Sequence(
                    new TaskWanderStreet(agent, animator, wanderRadius: 28f),
                    new TaskLoiterSocialize(agent, animator, minDuration: 2.0f, maxDuration: 4.5f)
                )
            );

            // --- ROOT SELECTOR ---
            var root = new Selector(
                fightBranch,
                panicBranch,
                perceptionBranch,
                conversationBranch,
                proximityBranch,
                ambientBranch
            );

            return root;
        }
    }
}
