#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using PolygonCyberCity.NPC.Spawner;
using PolygonCyberCity.NPC.Animation;

namespace PolygonCyberCity.NPC.Editor
{
    public static class NPCConversationTestTools
    {
        [MenuItem("CyberCity/NPC Animation/Test Pair Nearby NPCs for Conversation", false, 30)]
        [MenuItem("Tools/CyberCity/NPC Animation/Test Pair Nearby NPCs for Conversation", false, 30)]
        public static void TestPairNearbyNPCs()
        {
            var npcs = Object.FindObjectsByType<CyberNPCController>();
            if (npcs == null || npcs.Length < 2)
            {
                Debug.LogWarning("[NPCConversationTestTools] Found less than 2 CyberNPCController instances in active scene. Place at least 2 NPCs in scene to test.");
                return;
            }

            CyberNPCController npcA = null;
            CyberNPCController npcB = null;

            // Find closest pair of active NPCs
            float minDistance = float.MaxValue;
            for (int i = 0; i < npcs.Length; i++)
            {
                for (int j = i + 1; j < npcs.Length; j++)
                {
                    if (npcs[i].IsDead || npcs[j].IsDead) continue;
                    float dist = Vector3.Distance(npcs[i].transform.position, npcs[j].transform.position);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        npcA = npcs[i];
                        npcB = npcs[j];
                    }
                }
            }

            if (npcA == null || npcB == null)
            {
                Debug.LogWarning("[NPCConversationTestTools] Could not find 2 living candidate NPCs.");
                return;
            }

            bool success = NPCConversationManager.TryStartManualConversation(npcA, npcB);
            if (success)
            {
                Debug.Log($"<color=green>[NPCConversationTestTools] Successfully paired '{npcA.name}' ({npcA.ConversationRole}) and '{npcB.name}' ({npcB.ConversationRole}) for conversation! Distance: {minDistance:F2}m.</color>");
            }
            else
            {
                Debug.LogError($"[NPCConversationTestTools] Failed to initiate conversation session between '{npcA.name}' and '{npcB.name}'. Ensure NPCMacroState is Normal.");
            }
        }

        [MenuItem("CyberCity/NPC Animation/Simulate Gunshot Threat (Interrupt Conversation)", false, 31)]
        [MenuItem("Tools/CyberCity/NPC Animation/Simulate Gunshot Threat (Interrupt Conversation)", false, 31)]
        public static void SimulateGunshotThreatInterrupt()
        {
            var npcs = Object.FindObjectsByType<CyberNPCController>();
            int interruptedCount = 0;

            foreach (var npc in npcs)
            {
                if (npc != null && !npc.IsDead && npc.IsInConversation)
                {
                    interruptedCount++;
                    npc.ReceiveThreatAlert(npc.transform.position + Vector3.forward * 2f, NPCType.Mercenary);
                }
            }

            if (interruptedCount > 0)
            {
                Debug.Log($"<color=yellow>[NPCConversationTestTools] Simulated gunshot alert successfully interrupted {interruptedCount} NPCs in active conversations!</color>");
            }
            else
            {
                Debug.LogWarning("[NPCConversationTestTools] No active conversations found to interrupt.");
            }
        }
    }
}
#endif
