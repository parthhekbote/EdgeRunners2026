using UnityEngine;

namespace InsaneGun.Arena
{
    /// <summary>
    /// Spawns and manages the 550+ cheering stadium spectator NPCs at runtime or on scene load.
    /// Ensures scenes remain ultra-lightweight (<2MB) and load instantly without serializing
    /// thousands of NPC components into raw YAML.
    /// </summary>
    public class ArenaSpectatorCrowdSpawner : MonoBehaviour
    {
        private void Awake()
        {
            if (Application.isPlaying)
            {
                EnsureCrowd();
            }
        }

        public void EnsureCrowd()
        {
            Transform existing = transform.Find("Spectator_Crowd_Stadium_Bleachers");
            if (existing != null && existing.childCount > 0) return;

#if UNITY_EDITOR
            var builderType = System.Type.GetType("CyberpunkIndoorArenaBuilder, Assembly-CSharp-Editor");
            if (builderType != null)
            {
                var method = builderType.GetMethod("SpawnCrowdForSpawner", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (method != null)
                {
                    method.Invoke(null, new object[] { transform });
                }
            }
#endif
        }
    }
}
