using UnityEngine;
using UnityEngine.AI;
using PolygonCyberCity.NPC.BT.Tasks;
using PolygonCyberCity.NPC.Spawner;

namespace PolygonCyberCity.NPC.BT.Trees
{
    /// <summary>
    /// Behavior Tree Builder for Helper Bots, Maintenance Drones, and Service Robots.
    /// Priority 1: Evasive Scramble when attacked/damaged.
    /// Priority 2: Service scan / maintenance pause.
    /// Priority 3: City patrol path navigation.
    /// </summary>
    public static class DroneBotBT
    {
        public static BTNode Build(GameObject owner, NavMeshAgent agent, Animator animator, CyberNPCController controller)
        {
            // --- BRANCH 1: EVASIVE SCRAMBLE ---
            var scrambleBranch = new ConditionNode(
                bb => bb.WasAttacked || bb.CurrentState == NPCMacroState.Fleeing,
                new TaskPanicFlee(agent, animator, controller, fleeDist: 28f, sprintSpeed: 4.8f)
            );

            // --- BRANCH 2: SERVICE SCAN / MAINTENANCE ---
            var serviceBranch = new Selector(
                new TaskInspectShopfront(agent, animator),
                new TaskLoiterSocialize(agent, animator, 3.0f, 7.0f),
                new TaskWanderStreet(agent, animator, wanderRadius: 25f)
            );

            var root = new Selector(
                scrambleBranch,
                serviceBranch
            );

            return root;
        }
    }
}
