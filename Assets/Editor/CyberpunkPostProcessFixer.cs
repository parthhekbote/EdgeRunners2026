using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CyberpunkDev.EditorTools
{
    [InitializeOnLoad]
    public static class CyberpunkPostProcessFixer
    {
        private const string LogFilePath = "Logs/postprocess_fix_result.log";
        private const string TargetScenePath = "Assets/Scenes/InsaneGun_Combat_TestArena.unity";
        private const string ScreenshotPath = "Assets/game_tab_with_postprocessing.png";
        private static bool s_HasRunOnce = false;

        static CyberpunkPostProcessFixer()
        {
            // Manual trigger via MenuItem only to avoid unintended scene switches on assembly reload
            // EditorApplication.delayCall += AutoExecuteOnLoad;
        }

        private static void AutoExecuteOnLoad()
        {
            if (s_HasRunOnce || Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
            s_HasRunOnce = true;
            RunBothCommands();
        }

        [MenuItem("Tools/Cyberpunk/Run Post-Processing Inspection & Fix")]
        public static void RunBothCommands()
        {
            var sb = new StringBuilder();
            void LogMsg(string msg, params object[] args)
            {
                string formatted = (args != null && args.Length > 0) ? string.Format(msg, args) : msg;
                Debug.Log($"[PostProcessFixer] {formatted}");
                sb.AppendLine($"[INFO] {formatted}");
            }
            void LogWarn(string msg, params object[] args)
            {
                string formatted = (args != null && args.Length > 0) ? string.Format(msg, args) : msg;
                Debug.LogWarning($"[PostProcessFixer] {formatted}");
                sb.AppendLine($"[WARN] {formatted}");
            }
            void LogErr(string msg, params object[] args)
            {
                string formatted = (args != null && args.Length > 0) ? string.Format(msg, args) : msg;
                Debug.LogError($"[PostProcessFixer] {formatted}");
                sb.AppendLine($"[ERROR] {formatted}");
            }

            LogMsg("================================================================================");
            LogMsg("EXECUTING POST-PROCESSING PIPELINE INSPECTION & FIX ({0})", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            LogMsg("================================================================================");

            // Ensure target scene is loaded if volume not found in active scene
            GameObject volGO = FindPostProcessVolume();
            if (volGO == null && File.Exists(TargetScenePath))
            {
                LogMsg("Volume not in current scene. Opening target scene: {0}", TargetScenePath);
                var activeScene = EditorSceneManager.GetActiveScene();
                if (activeScene.isDirty)
                {
                    EditorSceneManager.SaveOpenScenes();
                }
                EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);
                volGO = FindPostProcessVolume();
            }

            // -------------------------------------------------------------
            // COMMAND 1: Inspect Post-Processing Pipeline Setup
            // -------------------------------------------------------------
            LogMsg("--- [COMMAND 1: Inspect Post-Processing Pipeline Setup] ---");
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                var uad = mainCam.GetComponent<UniversalAdditionalCameraData>();
                LogMsg("Main Camera found: '{0}', cullingMask={1}, volumeLayerMask={2}, renderPostProcessing={3}",
                    mainCam.name,
                    mainCam.cullingMask,
                    uad != null ? uad.volumeLayerMask.value.ToString() : "null",
                    uad != null ? uad.renderPostProcessing.ToString() : "null");
            }
            else
            {
                LogWarn("Camera.main is null! Searching all cameras...");
                Camera[] allCams = Resources.FindObjectsOfTypeAll<Camera>();
                foreach (var cam in allCams)
                {
                    if (cam.gameObject.scene.isLoaded)
                    {
                        mainCam = cam;
                        LogMsg("Found scene camera: {0}", cam.name);
                        break;
                    }
                }
            }

            if (volGO != null)
            {
                Volume vol = volGO.GetComponent<Volume>();
                LogMsg("Volume GameObject: name={0}, active={1}, layer={2} ({3}), isGlobal={4}, weight={5}",
                    volGO.name, volGO.activeSelf, volGO.layer, LayerMask.LayerToName(volGO.layer), vol.isGlobal, vol.weight);
                if (vol.profile != null)
                {
                    LogMsg("Profile: '{0}', component count={1}", vol.profile.name, vol.profile.components.Count);
                    foreach (var c in vol.profile.components)
                    {
                        LogMsg("  Component: {0}, active={1}", c.name, c.active);
                    }
                }
            }
            else
            {
                LogWarn("AAA_Cyberpunk_PostProcess_Volume not found in active scene!");
            }

            // -------------------------------------------------------------
            // COMMAND 2: Enable Post-Processing on Main Camera and Capture View
            // -------------------------------------------------------------
            LogMsg("--- [COMMAND 2: Enable Post-Processing on Main Camera and Capture View] ---");
            if (mainCam == null)
            {
                LogErr("No Main Camera found! Cannot apply fix.");
                WriteLog(sb.ToString());
                return;
            }

            // 1. Enable post-processing on Main Camera
            var uadComponent = mainCam.GetComponent<UniversalAdditionalCameraData>();
            if (uadComponent == null)
            {
                uadComponent = mainCam.gameObject.AddComponent<UniversalAdditionalCameraData>();
                LogMsg("UniversalAdditionalCameraData added to Main Camera.");
            }

            if (uadComponent != null)
            {
                Undo.RecordObject(uadComponent, "Enable Post Processing on Game Camera");
                uadComponent.renderPostProcessing = true;
                uadComponent.antialiasing = AntialiasingMode.None;
                EditorUtility.SetDirty(uadComponent);
                LogMsg("Main Camera renderPostProcessing set to TRUE!");
            }

            // 2. Configure AAA_Cyberpunk_PostProcess_Volume
            if (volGO != null)
            {
                volGO.SetActive(true);
                Volume vol = volGO.GetComponent<Volume>();
                vol.weight = 1.0f;
                vol.isGlobal = true;

                if (vol.profile != null)
                {
                    Undo.RecordObject(vol.profile, "Configure Cyberpunk Bloom and Post-Processing");

                    // Enable Bloom with radiant glow
                    if (vol.profile.TryGet<Bloom>(out var bloom))
                    {
                        bloom.active = true;
                        bloom.intensity.value = 2.2f;
                        bloom.threshold.value = 0.85f;
                        bloom.scatter.value = 0.70f;
                        bloom.tint.value = new Color(0.95f, 0.95f, 1.0f, 1.0f);
                        LogMsg("Bloom enabled: intensity=2.2, threshold=0.85, scatter=0.70, tint=(0.95, 0.95, 1.0, 1.0)");
                    }

                    // Disable Depth of Field so the wall is crisp and sharp
                    if (vol.profile.TryGet<DepthOfField>(out var dof))
                    {
                        dof.active = false;
                        LogMsg("DepthOfField disabled (prevents out-of-focus blur)");
                    }

                    if (vol.profile.TryGet<Tonemapping>(out var tm))
                    {
                        tm.active = true;
                        tm.mode.value = TonemappingMode.ACES;
                        LogMsg("Tonemapping enabled: ACES mode");
                    }

                    if (vol.profile.TryGet<ColorAdjustments>(out var ca))
                    {
                        ca.active = true;
                        ca.postExposure.value = 0.35f;
                        LogMsg("ColorAdjustments enabled: postExposure=0.35");
                    }

                    EditorUtility.SetDirty(vol.profile);
                }
                EditorUtility.SetDirty(volGO);
                LogMsg("Volume and profile modified and marked dirty.");
            }

            // 3. Save scene
            if (!Application.isPlaying)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveOpenScenes();
                AssetDatabase.SaveAssets();
                LogMsg("Scene and assets saved successfully.");
            }

            // 4. Render 1080p verification screenshot
            int width = 1920;
            int height = 1080;
            RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture prevRT = mainCam.targetTexture;
            mainCam.targetTexture = rt;
            mainCam.Render();
            mainCam.targetTexture = prevRT;

            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;

            File.WriteAllBytes(ScreenshotPath, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.Refresh();

            LogMsg("Saved 1080p screenshot with post-processing enabled to {0}", ScreenshotPath);
            LogMsg("================================================================================");
            LogMsg("POST-PROCESSING PIPELINE SETUP & CAPTURE COMPLETE!");
            LogMsg("================================================================================");

            WriteLog(sb.ToString());
        }

        private static GameObject FindPostProcessVolume()
        {
            GameObject volGO = GameObject.Find("AAA_Cyberpunk_PostProcess_Volume");
            if (volGO == null)
            {
                Volume[] vols = Resources.FindObjectsOfTypeAll<Volume>();
                foreach (var v in vols)
                {
                    if (v.name == "AAA_Cyberpunk_PostProcess_Volume")
                    {
                        volGO = v.gameObject;
                        break;
                    }
                }
            }
            return volGO;
        }

        private static void WriteLog(string content)
        {
            try
            {
                string dir = Path.GetDirectoryName(LogFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(LogFilePath, content);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PostProcessFixer] Could not write log file: {ex.Message}");
            }
        }
    }
}
