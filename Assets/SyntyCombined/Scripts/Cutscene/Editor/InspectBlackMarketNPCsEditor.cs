#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SyntyCombined.BlackMarket;

namespace SyntyCombined.Editor
{
    public static class InspectBlackMarketNPCsEditor
    {
        [MenuItem("Tools/Inspect Black Market NPCs")]
        public static void Inspect()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.name != "Demo_BlackMarket_01")
            {
                scene = EditorSceneManager.OpenScene("Assets/Synty/PolygonSciFiWorlds/Scenes/Demo_BlackMarket_01.unity", OpenSceneMode.Single);
            }

            GameObject root = GameObject.Find("[BlackMarket_NPC_Ecosystem]");
            if (root == null)
            {
                Debug.LogError("[Inspect] [BlackMarket_NPC_Ecosystem] not found in scene!");
                return;
            }

            var npcs = root.GetComponentsInChildren<BlackMarketNPC>(true);
            Debug.Log($"[Inspect] Found {npcs.Length} BlackMarketNPC components under [BlackMarket_NPC_Ecosystem].");

            foreach (var npc in npcs)
            {
                var wanderer = npc.GetComponent<BlackMarketNPCWanderer>();
                var anims = npc.GetComponentsInChildren<Animator>(true);

                string prefabPath = "None";
                var prefabSource = PrefabUtility.GetCorrespondingObjectFromSource(npc.gameObject);
                if (prefabSource != null)
                {
                    prefabPath = AssetDatabase.GetAssetPath(prefabSource);
                }

                bool isWanderer = (wanderer != null);
                if (!isWanderer) continue;

                // Inspect all animators on this wanderer
                string animDetails = $"AnimsCount={anims.Length}: ";
                for (int i = 0; i < anims.Length; i++)
                {
                    var a = anims[i];
                    string loc = a.gameObject == npc.gameObject ? "ROOT" : $"CHILD({a.gameObject.name})";
                    string ctrl = a.runtimeAnimatorController != null ? a.runtimeAnimatorController.name : "NULL";
                    string av = a.avatar != null ? a.avatar.name : "NULL";
                    bool avValid = a.avatar != null && a.avatar.isValid;
                    bool avHuman = a.avatar != null && a.avatar.isHuman;
                    
                    animDetails += $"[{i}: {loc}, Enabled={a.enabled}, Culling={a.cullingMode}, RootMotion={a.applyRootMotion}, Ctrl={ctrl}, Avatar={av}(valid={avValid},human={avHuman})] ";
                }

                // Check wanderer's referenced animator
                string wandAnim = "NULL";
                if (wanderer.animator != null)
                {
                    wandAnim = wanderer.animator.gameObject == npc.gameObject ? "ROOT" : $"CHILD({wanderer.animator.gameObject.name})";
                }

                // Check npc's referenced animator
                string npcAnim = "NULL";
                if (npc.animator != null)
                {
                    npcAnim = npc.animator.gameObject == npc.gameObject ? "ROOT" : $"CHILD({npc.animator.gameObject.name})";
                }

                Debug.Log($"[WANDER_INSPECT] '{npc.characterName}' | GO='{npc.gameObject.name}' | WandAnimRef={wandAnim} | NPCAnimRef={npcAnim} | {animDetails}");
            }
        }
    }
}
#endif
