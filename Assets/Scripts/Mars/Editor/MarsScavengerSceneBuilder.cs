using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Cyberpunk.Mars.Editor
{
    /// <summary>
    /// Builds Scene_Mars_FinalBattle from the Synty Scavenger demo map: strips the buildings (and the
    /// lush vegetation that would break the Mars look), keeps the scrap and wreckage, re-lights the map
    /// as a dusty Martian basin, then adds Alita, the Titan boss, camera, directors and the starship.
    /// </summary>
    public static class MarsScavengerSceneBuilder
    {
        private const string SourceScenePath = "Assets/Synty/PolygonSciFiWorlds/Scenes/Demo_Scavenger_01.unity";
        private const string TargetScenePath = "Assets/Scenes/Scene_Mars_FinalBattle.unity";
        private const string BackupScenePath = "Assets/Scenes/Scene_Mars_FinalBattle_ProceduralBackup.unity";

        // Combat ring: loose props inside this radius are cleared so the Titan has room to fight
        private const float ArenaClearRadius = 24f;

        [MenuItem("Cyberpunk/Build Mars Final Battle (Scavenger Map)")]
        public static void Build()
        {
            if (File.Exists(TargetScenePath) && !File.Exists(BackupScenePath))
                AssetDatabase.CopyAsset(TargetScenePath, BackupScenePath);

            var scene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);

            int removedBuildings = StripScene(scene);
            RemoveSceneCamerasLightsAndVolumes(scene);
            ClearArena(scene);
            var sun = ApplyMarsAtmosphere(scene);
            AddDustEmitters();

            MarsSceneBuilder.PopulateGameplay(sun, true);

            EditorSceneManager.SaveScene(scene, TargetScenePath);
            Debug.Log($"[MarsScavengerSceneBuilder] Saved {TargetScenePath} ({removedBuildings} building/vegetation objects removed)");
        }

        [MenuItem("Cyberpunk/Inspect Scavenger Map")]
        public static void Inspect()
        {
            var scene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
            var sb = new StringBuilder();
            sb.AppendLine("== ROOTS ==");
            foreach (var r in scene.GetRootGameObjects())
                sb.AppendLine($"{r.name} pos={r.transform.position} children={r.transform.childCount} active={r.activeSelf}");

            sb.AppendLine("== LIGHTS ==");
            foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                sb.AppendLine($"{l.name} {l.type} int={l.intensity}");
            sb.AppendLine("== CAMERAS ==");
            foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                sb.AppendLine($"{c.name} pos={c.transform.position}");
            sb.AppendLine("== VOLUMES ==");
            foreach (var v in UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                sb.AppendLine($"{v.name} global={v.isGlobal}");

            sb.AppendLine("== NON-PREFAB / NAMED (water, lake, building) ==");
            foreach (var go in AllObjects(scene))
            {
                string n = go.name.ToLowerInvariant();
                if (n.Contains("water") || n.Contains("lake") || n.Contains("building") || n.Contains("sky") || n.Contains("timeline") || n.Contains("director"))
                    sb.AppendLine($"{GetPath(go)} pos={go.transform.position}");
            }

            sb.AppendLine("== RENDER SETTINGS ==");
            sb.AppendLine($"skybox={(RenderSettings.skybox != null ? RenderSettings.skybox.name : "null")} fog={RenderSettings.fog} ambient={RenderSettings.ambientMode}");

            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool first = true;
            foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (first) { bounds = r.bounds; first = false; } else bounds.Encapsulate(r.bounds);
            }
            sb.AppendLine($"render bounds center={bounds.center} size={bounds.size}");

            Physics.SyncTransforms();
            foreach (var p in new[] { new Vector3(0, 0, -12), new Vector3(0, 0, 10), new Vector3(0, 0, 58), new Vector3(0, 0, 67.5f), new Vector3(0, 0, 0) })
            {
                string res = Physics.Raycast(new Vector3(p.x, 300f, p.z), Vector3.down, out var hit, 600f, ~0, QueryTriggerInteraction.Ignore)
                    ? $"{hit.point.y:F2} on {hit.collider.name}" : "NO HIT";
                sb.AppendLine($"ground at {p.x},{p.z}: {res}");
            }

            Debug.Log(sb.ToString());
        }

        // ── Stripping ────────────────────────────────────────────────────────────

        private static int StripScene(Scene scene)
        {
            var doomed = new List<GameObject>();
            foreach (var go in AllObjects(scene))
            {
                if (go == null) continue;
                string key = SourceName(go);
                if (ShouldRemove(key)) doomed.Add(go);
            }

            int count = 0;
            foreach (var go in doomed)
            {
                if (go == null) continue;
                UnityEngine.Object.DestroyImmediate(go);
                count++;
            }
            return count;
        }

        // Buildings and lush plant life go; scrap, wreckage, crates, vehicles, rocks, cliffs and ground stay.
        private static bool ShouldRemove(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;

            if (name.StartsWith("SM_Bld_Scav_Wreckage")) return false; // wrecks are scrap
            if (name.StartsWith("SM_Bld_")) return true;               // refinery, railings, pipes, silos, greebles, lights
            if (name.StartsWith("SM_Env_Plant_")) return true;
            if (name.StartsWith("SM_Env_Grass_")) return true;
            if (name.StartsWith("SM_Env_LakeEdge")) return true;
            if (name.StartsWith("FX_Radioactive")) return true;
            if (name.StartsWith("SM_Prop_Vendor") || name.StartsWith("SM_Prop_Table") ||
                name.StartsWith("SM_Prop_Chair") || name.StartsWith("SM_Prop_Monitor") ||
                name.StartsWith("SM_Prop_Locker")) return true;        // furniture belonging to the buildings
            return false;
        }

        private static string SourceName(GameObject go)
        {
            if (PrefabUtility.IsOutermostPrefabInstanceRoot(go))
            {
                var src = PrefabUtility.GetCorrespondingObjectFromSource(go);
                if (src != null) return Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(src));
            }
            return go.name;
        }

        private static void RemoveSceneCamerasLightsAndVolumes(Scene scene)
        {
            foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (c != null && c.gameObject.scene == scene) UnityEngine.Object.DestroyImmediate(c.gameObject);

            foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (l != null && l.gameObject.scene == scene && l.type == LightType.Directional)
                    UnityEngine.Object.DestroyImmediate(l.gameObject);

            foreach (var v in UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (v != null && v.gameObject.scene == scene) UnityEngine.Object.DestroyImmediate(v.gameObject);
        }

        // Clears loose props from the combat ring; ground tiles, cliffs and craters stay.
        private static void ClearArena(Scene scene)
        {
            var doomed = new List<GameObject>();
            foreach (var go in AllObjects(scene))
            {
                if (go == null || !PrefabUtility.IsOutermostPrefabInstanceRoot(go)) continue;
                string n = SourceName(go);
                bool loose = n.StartsWith("SM_Prop_") || n.StartsWith("SM_Env_Rock") ||
                             n.StartsWith("SM_Env_Ground_Junk") || n.StartsWith("SM_Veh_") ||
                             n.StartsWith("SM_Bld_Scav_Wreckage");
                if (!loose) continue;

                Vector3 p = go.transform.position;
                if (new Vector2(p.x, p.z).magnitude < ArenaClearRadius) doomed.Add(go);
            }
            foreach (var go in doomed) if (go != null) UnityEngine.Object.DestroyImmediate(go);
        }

        // ── Mars look ────────────────────────────────────────────────────────────

        private static Light ApplyMarsAtmosphere(Scene scene)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0042f;
            RenderSettings.fogColor = new Color(0.84f, 0.44f, 0.24f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.74f, 0.40f, 0.25f);

            var sunGO = new GameObject("Martian_Sun");
            var sun = sunGO.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1.0f, 0.85f, 0.70f);
            sun.intensity = 2.4f;
            sun.shadows = LightShadows.Soft;
            sunGO.transform.rotation = Quaternion.Euler(36f, -32f, 0f);
            RenderSettings.sun = sun;

            var fillGO = new GameObject("Martian_Atmospheric_Fill");
            var fill = fillGO.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.90f, 0.42f, 0.24f);
            fill.intensity = 0.65f;
            fill.shadows = LightShadows.None;
            fillGO.transform.rotation = Quaternion.Euler(-35f, 148f, 0f);

            // Sky dome using the scavenger sky, same as the procedural Mars build
            var skyDomePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonSciFiWorlds/Prefabs/FX/SkyDome.prefab");
            if (skyDomePrefab != null)
            {
                var skyDome = (GameObject)PrefabUtility.InstantiatePrefab(skyDomePrefab);
                skyDome.name = "Martian_SkyDome";
                skyDome.transform.position = new Vector3(0f, -60f, 0f);
                skyDome.transform.localScale = Vector3.one * 1200f;
                var skyMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Synty/PolygonSciFiWorlds/Materials/Sky/Skybox_Scav_01.mat");
                if (skyMat != null)
                    foreach (var rend in skyDome.GetComponentsInChildren<Renderer>())
                    {
                        rend.sharedMaterial = skyMat;
                        rend.shadowCastingMode = ShadowCastingMode.Off;
                    }
            }

            var planetPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonSciFiSpace/Prefabs/Environment/SM_Env_Planet_01.prefab");
            if (planetPrefab != null)
            {
                var phobos = (GameObject)PrefabUtility.InstantiatePrefab(planetPrefab);
                phobos.name = "Celestial_Phobos";
                phobos.transform.position = new Vector3(180f, 220f, 380f);
                phobos.transform.localScale = Vector3.one * 65f;
                phobos.transform.rotation = Quaternion.Euler(22f, 195f, 15f);
                var rend = phobos.GetComponentInChildren<Renderer>();
                if (rend != null) rend.shadowCastingMode = ShadowCastingMode.Off;
            }

            return sun;
        }

        private static void AddDustEmitters()
        {
            var dustPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonSciFiWorlds/Prefabs/FX/FX_Dust_Blowing_Scav_01.prefab");
            if (dustPrefab == null) return;

            var root = new GameObject("Atmospheric_Dust_Emitters");
            Vector3[] pos =
            {
                new Vector3(0f, 0.2f, 0f), new Vector3(-25f, 0.2f, 20f), new Vector3(25f, 0.2f, 20f),
                new Vector3(0f, 0.2f, -25f), new Vector3(-50f, 0.2f, -40f), new Vector3(50f, 0.2f, 45f)
            };
            for (int i = 0; i < pos.Length; i++)
            {
                var d = (GameObject)PrefabUtility.InstantiatePrefab(dustPrefab, root.transform);
                d.name = $"Dust_Emitter_{i + 1}";
                d.transform.position = MarsSceneBuilder.SnapToGround(pos[i]);
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static IEnumerable<GameObject> AllObjects(Scene scene)
        {
            var stack = new Stack<Transform>(scene.GetRootGameObjects().Select(g => g.transform));
            var list = new List<GameObject>();
            while (stack.Count > 0)
            {
                var t = stack.Pop();
                list.Add(t.gameObject);
                foreach (Transform c in t) stack.Push(c);
            }
            return list;
        }

        private static string GetPath(GameObject go)
        {
            string path = go.name;
            var t = go.transform.parent;
            while (t != null) { path = t.name + "/" + path; t = t.parent; }
            return path;
        }
    }
}
