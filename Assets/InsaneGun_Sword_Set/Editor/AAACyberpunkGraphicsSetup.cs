using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace InsaneGun.Combat.Editor
{
    public static class AAACyberpunkGraphicsSetup
    {
        [MenuItem("Cyberpunk/Visuals/Apply AAA Realistic Graphics (DLSS 5 Preset)", false, 10)]
        public static void ApplyAAARealisticGraphics()
        {
            // 1. Find or create graphics master in current active scene
            var master = Object.FindAnyObjectByType<AAACyberpunkGraphicsMaster>();
            if (master == null)
            {
                var go = new GameObject("AAA_Cyberpunk_Graphics_Master");
                master = go.AddComponent<AAACyberpunkGraphicsMaster>();
                Undo.RegisterCreatedObjectUndo(go, "Create AAA Graphics Master");
            }

            master.enableNextGenGraphics = true;
            master.enableWetGroundReflections = true;
            master.enableAtmosphericMist = true;
            master.enableVolumetricLightBeams = true;
            master.SetupPipeline();

            // 2. Configure Main Camera (Anti-Aliasing, HDR, Depth)
            var cam = Camera.main;
            if (cam != null)
            {
                var camData = cam.GetComponent<UniversalAdditionalCameraData>();
                if (camData == null) camData = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();

                camData.renderPostProcessing = true;
                camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing; // SMAA
                camData.antialiasingQuality = AntialiasingQuality.High;
                camData.dithering = true;
                camData.renderShadows = true;

                EditorUtility.SetDirty(cam);
                EditorUtility.SetDirty(camData);
            }

            // 3. Mark active scene dirty
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            Debug.Log("<color=#00E5FF><b>[AAA Graphics Master]</b></color> Successfully applied <b>DLSS 5 Realistic Graphics Preset</b>: ACES Filmic Tonemapping, High HDR Bloom, SSAO Deep Occlusion, Wet Asphalt Reflections, 4K Soft Cascades & Volumetric Night Mist!");
        }

        [MenuItem("Cyberpunk/Visuals/Enhance Current Arena Scene Immediately", false, 11)]
        public static void EnhanceArenaScene()
        {
            ApplyAAARealisticGraphics();
        }
    }
}
