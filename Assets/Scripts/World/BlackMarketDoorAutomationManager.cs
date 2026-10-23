using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Cyberpunk.World
{
    /// <summary>
    /// Master automation manager for the Black Market district (Demo_BlackMarket_01).
    /// Discovers all doors, airlocks, vendor shutters, blast gates, and cargo bays across the entire scene,
    /// binding them with autonomous CyberpunkAutomaticDoor controllers.
    /// </summary>
    public static class BlackMarketDoorAutomationManager
    {
        public const string BlackMarketSceneName = "Demo_BlackMarket_01";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnSceneLoadedRuntime()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.name == BlackMarketSceneName)
            {
                int count = SetupAllDoorsInScene();
                Debug.Log($"[BlackMarketDoorAutomationManager] Initialized {count} automatic proximity doors in '{scene.name}'.");
            }
        }

        /// <summary>
        /// Finds and configures every single door assembly in the active scene.
        /// Can be called both in Editor Mode and at Runtime.
        /// </summary>
        public static int SetupAllDoorsInScene()
        {
            var allTransforms = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            var configuredDoors = new List<CyberpunkAutomaticDoor>();

            foreach (var t in allTransforms)
            {
                if (t == null) continue;
                string n = t.name;

                // Check if this transform is a primary door assembly
                bool isTargetAssembly = false;

                // 1. Building doors (frames)
                if (n.StartsWith("SM_Bld_Door_") && !n.Contains("_Door_0"))
                {
                    isTargetAssembly = true;
                }
                // 2. Vendor stalls
                else if (n.StartsWith("SM_Bld_Vendor_") && !n.Contains("_Door_0") && HasDoorChild(t))
                {
                    isTargetAssembly = true;
                }
                // 3. Loading bay
                else if (n.StartsWith("SM_Bld_Corp_Loading_Bay_01") && HasDoorChild(t))
                {
                    isTargetAssembly = true;
                }
                // 4. Shipping Container
                else if (n.StartsWith("SM_Prop_Container_01") && HasDoorChild(t))
                {
                    isTargetAssembly = true;
                }
                // 5. Starship Boarding Door
                else if (n == "Alita_Lost_Martian_Starship" || (n.StartsWith("SM_Veh_HoverCraft_01") && HasDoorChild(t)))
                {
                    isTargetAssembly = true;
                }
                // 6. Vendor Hover Tugs with accessible cab doors
                else if (n.StartsWith("SM_Veh_Vendor_Tug_01") && !n.Contains("_Door_0") && HasDoorChild(t))
                {
                    isTargetAssembly = true;
                }
                // 7. Wall Units / Cabinets
                else if (n.StartsWith("SM_Prop_Wall_Unit_01") && !n.Contains("_Door_0") && HasDoorChild(t))
                {
                    isTargetAssembly = true;
                }
                // 8. Generic standalone door assembly with a movable leaf
                else if (HasDoorChild(t) && !n.ToLower().Contains("barrier") && !n.ToLower().Contains("pillar") && !n.ToLower().Contains("waypoint"))
                {
                    isTargetAssembly = true;
                }

                // Never attach to child panel if an ancestor already has CyberpunkAutomaticDoor or if this is a door leaf
                if (n.Contains("_Door_0") || n.Contains("Cargo_Door") || n.Contains("Shutter_0"))
                {
                    isTargetAssembly = false;
                }

                if (isTargetAssembly && t.parent != null && t.parent.GetComponentInParent<CyberpunkAutomaticDoor>() != null)
                {
                    isTargetAssembly = false;
                }

                if (isTargetAssembly)
                {
                    var door = t.GetComponent<CyberpunkAutomaticDoor>();
                    if (door == null)
                    {
                        door = t.gameObject.AddComponent<CyberpunkAutomaticDoor>();
                        door.AutoConfigurePanels();
                    }
                    else if (door.panels == null || door.panels.Count == 0)
                    {
                        door.AutoConfigurePanels();
                    }

                    // Ensure door settings are tuned for seamless, immediate response
                    door.openDistance = Mathf.Max(door.openDistance, 6.5f);
                    door.closeDistance = Mathf.Max(door.closeDistance, 9.5f);
                    door.openSpeed = Mathf.Max(door.openSpeed, 5.5f);
                    door.disableRootFrameColliderAlways = true;

                    // Disable any blocking root collider on this door assembly
                    var rootCol = t.GetComponent<Collider>();
                    if (rootCol != null)
                    {
                        rootCol.enabled = false;
                        door.rootFrameCollider = rootCol;
                    }

                    // Special tuning for the Collector's Sanctum front door
                    if (t.name.Contains("Single_04") && Mathf.Abs(t.position.z - (-10.25f)) < 2.0f)
                    {
                        door.openDistance = 7.5f;
                        door.closeDistance = 10.5f;
                        door.openSpeed = 6.0f;
                    }

                    // Only track if panels were successfully configured
                    if (door.panels != null && door.panels.Count > 0)
                    {
                        configuredDoors.Add(door);
#if UNITY_EDITOR
                        EditorUtility.SetDirty(door);
#endif
                    }
                    else
                    {
                        // Remove empty component
                        Object.DestroyImmediate(door);
                    }
                }
            }

            // Post-pass cleanup of any accidental nested components
            for (int i = configuredDoors.Count - 1; i >= 0; i--)
            {
                var d = configuredDoors[i];
                if (d != null && d.transform.parent != null && d.transform.parent.GetComponentInParent<CyberpunkAutomaticDoor>() != null)
                {
                    configuredDoors.RemoveAt(i);
                    Object.DestroyImmediate(d);
                }
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }
#endif

            return configuredDoors.Count;
        }

        private static bool HasDoorChild(Transform parent)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var ch = parent.GetChild(i);
                string chName = ch.name.ToLower();
                if (chName.Contains("door_0") || chName.Contains("cargo_door") || chName.Contains("shutter_0"))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
