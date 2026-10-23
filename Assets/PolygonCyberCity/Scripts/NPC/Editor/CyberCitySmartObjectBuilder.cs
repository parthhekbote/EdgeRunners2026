#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using PolygonCyberCity.NPC.Animation;

namespace PolygonCyberCity.NPC.Editor
{
    /// <summary>
    /// Automated Smart Object Builder & Scene Integrator for Cyber City.
    /// Equips benches, vending machines, data terminals, shop windows, and food stalls with
    /// validated NavMesh Smart Objects and aligned interaction points.
    /// </summary>
    public static class CyberCitySmartObjectBuilder
    {
        [MenuItem("CyberCity/NPC Population/Equip All City Smart Objects", false, 15)]
        [MenuItem("Tools/Cyber City/NPC Population/Equip All City Smart Objects", false, 15)]
        public static void EquipAllSmartObjects()
        {
            int benchCount = 0;
            int vendingCount = 0;
            int terminalCount = 0;
            int shopCount = 0;
            int foodCount = 0;

            var allGos = Object.FindObjectsByType<GameObject>();

            foreach (var go in allGos)
            {
                string name = go.name.ToLower();

                // 1. Benches & Seats (SM_Prop_Bench_Seat_01, SM_Prop_Bench_Seat_02)
                if (name.Contains("bench_seat") || name.Contains("prop_bench"))
                {
                    if (SetupBench(go)) benchCount++;
                }
                // 2. Vending Machines (SM_Prop_Vending_Machine_01, SM_Prop_Vending_Machine_02)
                else if (name.Contains("vending_machine"))
                {
                    if (SetupVendingMachine(go)) vendingCount++;
                }
                // 3. Street Data Consoles / ATMs (SM_Prop_Street_Console_02)
                else if (name.Contains("street_console") && !name.Contains("glass"))
                {
                    if (SetupTerminal(go)) terminalCount++;
                }
                // 4. Food Trailers & Stalls
                else if (name.Contains("food_trailer") || name.Contains("noodle") || name.Contains("food_stall"))
                {
                    if (SetupFoodStall(go)) foodCount++;
                }
                // 5. Storefront Windows (SM_Bld_Wall_Shop_01..04)
                else if (name.Contains("wall_shop") && !name.Contains("glass") && !name.Contains("shutter"))
                {
                    if (SetupShopWindow(go)) shopCount++;
                }
            }

            if (!EditorApplication.isPlaying)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
            }
            Debug.Log($"[CyberCitySmartObjectBuilder] Successfully equipped {benchCount + vendingCount + terminalCount + shopCount + foodCount} Smart Objects! (Benches: {benchCount}, Vending: {vendingCount}, Terminals: {terminalCount}, Shops: {shopCount}, Food: {foodCount})");
        }

        private static bool SetupBench(GameObject go)
        {
            // 1. Add NavMeshObstacle to physically CARVE OUT the bench cushion and frame from NavMesh
            // This strictly guarantees pedestrians will NEVER walk or stand on top of the bench!
            var obstacle = go.GetComponent<NavMeshObstacle>();
            if (obstacle == null) obstacle = Undo.AddComponent<NavMeshObstacle>(go);
            obstacle.carving = true;
            obstacle.carveOnlyStationary = false;
            var mf = go.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                obstacle.center = mf.sharedMesh.bounds.center;
                obstacle.size = mf.sharedMesh.bounds.size;
            }
            else
            {
                obstacle.center = new Vector3(0f, 0.45f, 0f);
                obstacle.size = new Vector3(2.2f, 0.9f, 0.8f);
            }

            var smartObj = go.GetComponent<SmartObject>();
            if (smartObj == null) smartObj = Undo.AddComponent<SmartObject>(go);
            smartObj.activityType = NPCActivityType.Sit;
            smartObj.interactionDuration = Random.Range(8.0f, 16.0f);
            smartObj.cooldownAfterUse = 14.0f;

            Transform ipt = GetOrCreateInteractionPoint(go, out _);
            // Position firmly on sidewalk pavement in front of bench (Z = +0.85m), facing street
            ipt.localPosition = new Vector3(0f, 0f, 0.85f);
            ipt.localRotation = Quaternion.identity;

            // Snap interaction point position to ground NavMesh
            if (NavMesh.SamplePosition(ipt.position, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
            {
                ipt.position = hit.position;
            }

            smartObj.interactionPoint = ipt;
            EditorUtility.SetDirty(smartObj);
            return true;
        }

        private static bool SetupVendingMachine(GameObject go)
        {
            var smartObj = go.GetComponent<SmartObject>();
            if (smartObj == null) smartObj = Undo.AddComponent<SmartObject>(go);
            smartObj.activityType = NPCActivityType.VendingMachine;
            smartObj.interactionDuration = Random.Range(4.5f, 8.5f);
            smartObj.cooldownAfterUse = 12.0f;

            Transform ipt = GetOrCreateInteractionPoint(go, out _);
            ipt.localPosition = new Vector3(0f, 0f, 0.85f);
            ipt.localRotation = Quaternion.Euler(0f, 180f, 0f);

            if (NavMesh.SamplePosition(ipt.position, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
            {
                ipt.position = hit.position;
            }

            smartObj.interactionPoint = ipt;
            EditorUtility.SetDirty(smartObj);
            return true;
        }

        private static bool SetupTerminal(GameObject go)
        {
            var smartObj = go.GetComponent<SmartObject>();
            if (smartObj == null) smartObj = Undo.AddComponent<SmartObject>(go);
            smartObj.activityType = NPCActivityType.UseTerminal;
            smartObj.interactionDuration = Random.Range(5.0f, 9.5f);
            smartObj.cooldownAfterUse = 10.0f;

            Transform ipt = GetOrCreateInteractionPoint(go, out _);
            ipt.localPosition = new Vector3(0f, 0f, 0.75f);
            ipt.localRotation = Quaternion.Euler(0f, 180f, 0f);

            if (NavMesh.SamplePosition(ipt.position, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
            {
                ipt.position = hit.position;
            }

            smartObj.interactionPoint = ipt;
            EditorUtility.SetDirty(smartObj);
            return true;
        }

        private static bool SetupFoodStall(GameObject go)
        {
            var smartObj = go.GetComponent<SmartObject>();
            if (smartObj == null) smartObj = Undo.AddComponent<SmartObject>(go);
            smartObj.activityType = NPCActivityType.Eat;
            smartObj.interactionDuration = Random.Range(6.0f, 12.0f);
            smartObj.cooldownAfterUse = 12.0f;

            Transform ipt = GetOrCreateInteractionPoint(go, out _);
            ipt.localPosition = new Vector3(0f, 0f, 1.2f);
            ipt.localRotation = Quaternion.Euler(0f, 180f, 0f);

            if (NavMesh.SamplePosition(ipt.position, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
            {
                ipt.position = hit.position;
            }

            smartObj.interactionPoint = ipt;
            EditorUtility.SetDirty(smartObj);
            return true;
        }

        private static bool SetupShopWindow(GameObject go)
        {
            var smartObj = go.GetComponent<SmartObject>();
            if (smartObj == null) smartObj = Undo.AddComponent<SmartObject>(go);
            smartObj.activityType = NPCActivityType.Inspect;
            smartObj.interactionDuration = Random.Range(5.0f, 9.0f);
            smartObj.cooldownAfterUse = 15.0f;

            Transform ipt = GetOrCreateInteractionPoint(go, out _);
            ipt.localPosition = new Vector3(0f, 0f, 1.4f);
            ipt.localRotation = Quaternion.Euler(0f, 180f, 0f);

            if (NavMesh.SamplePosition(ipt.position, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
            {
                ipt.position = hit.position;
            }

            smartObj.interactionPoint = ipt;
            EditorUtility.SetDirty(smartObj);
            return true;
        }

        private static Transform GetOrCreateInteractionPoint(GameObject parent, out bool isNew)
        {
            isNew = false;
            Transform ipt = parent.transform.Find("InteractionPoint");
            if (ipt == null)
            {
                var iptGo = new GameObject("InteractionPoint");
                Undo.RegisterCreatedObjectUndo(iptGo, "Create Interaction Point");
                ipt = iptGo.transform;
                ipt.SetParent(parent.transform, false);
                isNew = true;
            }
            return ipt;
        }
    }
}
#endif
