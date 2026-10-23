using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using InsaneGun.Combat;

namespace PolygonMech.Editor
{
    public static class MechActionVFXPreviewer
    {
        [MenuItem("Polygon Mech/Generate Action VFX Previews", false, 10)]
        public static void GenerateActionVFXPreviews()
        {
            string scenePath = "Assets/Scenes/SyntyCombatDemo.unity";
            if (EditorSceneManager.GetActiveScene().path != scenePath)
            {
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            var boss = GameObject.Find("Boss_GoliathMech");
            if (boss == null)
            {
                Debug.LogError("[ActionVFX] Boss_GoliathMech not found in scene!");
                return;
            }

            string outDir = "Assets/PolygonMech/VerifiedAnimationFrames";
            Directory.CreateDirectory(outDir);

            // Camera setup with dynamic cinematic offset and URP Post Processing
            GameObject camGO = new GameObject("VFX_ActionCam");
            Camera cam = camGO.AddComponent<Camera>();
            cam.fieldOfView = 48f;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;

            var uac = camGO.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            uac.renderPostProcessing = true;

            Vector3 bossCenter = boss.transform.position + Vector3.up * 2.5f;
            // Angled front-quarter dramatic combat camera with generous wide framing
            Vector3 camPos = bossCenter + boss.transform.forward * 6.5f + boss.transform.right * 4.6f + Vector3.up * 1.2f;
            camGO.transform.position = camPos;
            camGO.transform.LookAt(bossCenter + Vector3.up * 0.35f);

            // Directional key combat light
            GameObject lightGO = new GameObject("VFX_ActionLight");
            Light light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.95f, 0.9f);
            light.intensity = 1.3f;
            lightGO.transform.rotation = Quaternion.Euler(38f, 135f, 0f);

            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);

            var bladeVFX = boss.GetComponent<MechBladeCombatVFX>();

            void CapturePose(string id, string clipPath, float normTime, int slashType, bool isPhase2, string desc)
            {
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (clip == null)
                {
                    Debug.LogWarning($"[ActionVFX] Could not load clip: {clipPath}");
                    return;
                }

                if (bladeVFX != null)
                {
                    bladeVFX.SetPhase2(isPhase2);
                    bladeVFX.StartSwingTrail();
                }

                AnimationMode.StartAnimationMode();
                AnimationMode.SampleAnimationClip(boss, clip, clip.length * normTime);

                Transform hips = boss.transform.Find("Root/Hips");
                Vector3 currentMechPos = (hips != null) ? hips.position : boss.transform.position;
                Vector3 center = new Vector3(currentMechPos.x, boss.transform.position.y + 2.5f, currentMechPos.z);

                Vector3 targetCamPos = center + boss.transform.forward * 6.0f + boss.transform.right * 4.4f + Vector3.up * 0.9f;
                Vector3 targetLookAt = center + Vector3.up * 0.2f;

                camGO.transform.position = targetCamPos;
                camGO.transform.LookAt(targetLookAt);

                GameObject anchorObj = new GameObject("VFX_Attacker_Anchor");
                anchorObj.transform.position = new Vector3(currentMechPos.x, boss.transform.position.y, currentMechPos.z);
                anchorObj.transform.rotation = boss.transform.rotation;

                // Spawn the 3D Tron Melee Slash Arc with Titan Scaling (2.4x radius, 1.8m height offset)
                if (slashType > 0)
                {
                    TronSlashArcGenerator.SpawnTronSlash(anchorObj.transform, slashType, isPhase2, scaleMultiplier: 2.4f, heightOffset: 1.8f);
                }
                else
                {
                    // Defensive shield spark burst
                    if (bladeVFX != null)
                        bladeVFX.EmitDefensiveParrySparks();
                }

                cam.targetTexture = rt;
                cam.Render();

                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                tex.Apply();

                cam.targetTexture = null;
                RenderTexture.active = null;

                AnimationMode.StopAnimationMode();
                Object.DestroyImmediate(anchorObj);

                if (bladeVFX != null)
                {
                    bladeVFX.StopSwingTrail();
                }

                // Clean up spawned VFX objects
                var spawnedArcs = GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var g in spawnedArcs)
                {
                    if (g.name.StartsWith("Tron_") || g.name.StartsWith("Needle_") || g.name.StartsWith("FloorGrid_") || g.name.StartsWith("DeRez_"))
                    {
                        Object.DestroyImmediate(g);
                    }
                }

                string filePath = Path.Combine(outDir, $"ActionVFX_{id}.png");
                File.WriteAllBytes(filePath, tex.EncodeToPNG());
                Debug.Log($"[ActionVFX] Captured {desc} -> {filePath}");
            }

            try
            {
                // 1. Cleave Horizontal (Cyber Gold Tron Arc)
                CapturePose("01_Cleave_Horizontal", 
                    "Assets/PolygonMech/Animations/GreatSwordPack/great sword slash.fbx", 
                    0.44f, 1, false, "Horizontal Cleave with 3D Cyber Arc");

                // 2. Overhead Execution Split (Seismic Floor Grid Ring + Vertical Arc)
                CapturePose("02_Overhead_Split", 
                    "Assets/PolygonMech/Animations/GreatSwordPack/great sword slash (2).fbx", 
                    0.50f, 5, false, "Overhead Split with Seismic Ring");

                // 3. Cyclone Whirlwind (360-degree Light Wall)
                CapturePose("03_Cyclone_Whirlwind", 
                    "Assets/PolygonMech/Animations/GreatSwordPack/great sword high spin attack.fbx", 
                    0.45f, 6, false, "360-Degree Cyclone Whirlwind Wall");

                // 4. Overdrive Decapitation Finisher (Phase 2 Plasma Crimson Rupture)
                CapturePose("04_Overdrive_Finisher", 
                    "Assets/PolygonMech/Animations/GreatSwordPack/great sword slash (5).fbx", 
                    0.42f, 7, true, "Phase 2 Overdrive Decapitation Rupture");

                // 5. Piercing Piston Thrust (Focused Forward Laser Tunnel)
                CapturePose("05_Piston_Thrust", 
                    "Assets/PolygonMech/Animations/GreatSwordPack/great sword attack.fbx", 
                    0.38f, 4, false, "Piercing Piston Thrust Laser Tunnel");

                // 6. Reactive Blade Shield (Defensive Sparks & Kinetic Flash)
                CapturePose("06_Blade_Shield", 
                    "Assets/PolygonMech/Animations/GreatSwordPack/great sword blocking (2).fbx", 
                    0.35f, 0, false, "Reactive Blade Shield Spark Burst");
            }
            finally
            {
                Object.DestroyImmediate(camGO);
                Object.DestroyImmediate(lightGO);
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(tex);
                AssetDatabase.Refresh();
            }
        }
    }
}
