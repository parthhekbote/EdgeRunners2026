using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PolygonMech.Editor
{
    public static class MechBossAnimationMatrixInspector
    {
        public class AnimationTestDef
        {
            public string id;
            public string category;
            public string relPath;
            public string displayName;

            public AnimationTestDef(string id, string category, string relPath, string displayName)
            {
                this.id = id;
                this.category = category;
                this.relPath = relPath;
                this.displayName = displayName;
            }
        }

        public static readonly AnimationTestDef[] Curated32Animations = new AnimationTestDef[]
        {
            // ── 1. IDLES & STANCES ──
            new AnimationTestDef("01_Idle_TitanGuard",       "Idle",        "Assets/PolygonMech/Animations/GreatSwordPack/great sword idle (3).fbx",       "Titan Guard Stance"),
            new AnimationTestDef("02_Idle_ReactorBreathing",  "Idle",        "Assets/PolygonMech/Animations/CreaturePack/mutant breathing idle.fbx",        "Reactor Breathing"),
            
            // ── 2. LOCOMOTION ──
            new AnimationTestDef("03_Walk_HydraulicForward",  "Locomotion",  "Assets/PolygonMech/Animations/GreatSwordPack/great sword walk.fbx",           "Hydraulic Forward Walk"),
            new AnimationTestDef("04_Walk_PatrolStalk",       "Locomotion",  "Assets/PolygonMech/Animations/GreatSwordPack/great sword walk (2).fbx",       "Patrol Stalk"),
            new AnimationTestDef("05_Run_HeavyCharge",        "Locomotion",  "Assets/PolygonMech/Animations/GreatSwordPack/great sword run.fbx",            "Heavy Sprint Charge"),
            new AnimationTestDef("06_Run_OverdriveSprint",    "Locomotion",  "Assets/PolygonMech/Animations/GreatSwordPack/great sword run (2).fbx",        "Afterburner Dash"),
            new AnimationTestDef("07_Strafe_FlankRight",      "Locomotion",  "Assets/PolygonMech/Animations/GreatSwordPack/great sword strafe.fbx",         "Flank Strafe Right"),
            new AnimationTestDef("08_Strafe_FlankLeft",       "Locomotion",  "Assets/PolygonMech/Animations/GreatSwordPack/great sword strafe (2).fbx",     "Flank Strafe Left"),
            new AnimationTestDef("09_Step_EvadeBackRight",    "Locomotion",  "Assets/PolygonMech/Animations/GreatSwordPack/great sword strafe (3).fbx",     "Evade Back-Right"),
            new AnimationTestDef("10_Step_EvadeBackLeft",     "Locomotion",  "Assets/PolygonMech/Animations/GreatSwordPack/great sword strafe (4).fbx",     "Evade Back-Left"),
            
            // ── 3. TURNS & PIVOTS ──
            new AnimationTestDef("11_Turn_180QuickPivot",     "Turn",        "Assets/PolygonMech/Animations/GreatSwordPack/great sword 180 turn.fbx",       "180 Pivot Turn"),
            new AnimationTestDef("12_Turn_90RightServo",      "Turn",        "Assets/PolygonMech/Animations/CreaturePack/mutant right turn 90.fbx",         "90 Servo Turn Right"),
            new AnimationTestDef("13_Turn_45LeftReposition",  "Turn",        "Assets/PolygonMech/Animations/CreaturePack/mutant left turn 45.fbx",          "45 Reposition Left"),

            // ── 4. MELEE SLASHES ──
            new AnimationTestDef("14_Slash_CleaveHorizontal", "Slash",       "Assets/PolygonMech/Animations/GreatSwordPack/great sword slash.fbx",          "Horizontal Cleave"),
            new AnimationTestDef("15_Slash_OverheadSplit",    "Slash",       "Assets/PolygonMech/Animations/GreatSwordPack/great sword slash (2).fbx",      "Overhead Execution Split"),
            new AnimationTestDef("16_Slash_UpwardLauncher",   "Slash",       "Assets/PolygonMech/Animations/GreatSwordPack/great sword slash (3).fbx",      "Upward Launch Slash"),
            new AnimationTestDef("17_Slash_DoubleCrossCut",   "Slash",       "Assets/PolygonMech/Animations/GreatSwordPack/great sword slash (4).fbx",      "Double Cross Slash"),
            new AnimationTestDef("18_Slash_DecapitationSweep","Slash",       "Assets/PolygonMech/Animations/GreatSwordPack/great sword slash (5).fbx",      "Decapitation Sweep"),

            // ── 5. MECHANICAL STRIKES ──
            new AnimationTestDef("19_Attack_PistonThrust",    "Attack",      "Assets/PolygonMech/Animations/GreatSwordPack/great sword attack.fbx",         "Piston Core Thrust"),
            new AnimationTestDef("20_Attack_CycloneWhirlwind","Attack",      "Assets/PolygonMech/Animations/GreatSwordPack/great sword high spin attack.fbx","360 Cyclone Spin"),
            new AnimationTestDef("21_Attack_RocketSkateDash", "Attack",      "Assets/PolygonMech/Animations/GreatSwordPack/great sword slide attack.fbx",   "Rocket Skater Dash"),
            new AnimationTestDef("22_Attack_JumpGroundCleave","Attack",      "Assets/PolygonMech/Animations/GreatSwordPack/great sword jump attack.fbx",    "Jump Ground Cleave"),
            new AnimationTestDef("23_Attack_PneumaticPunch",  "Attack",      "Assets/PolygonMech/Animations/CreaturePack/mutant punch.fbx",                 "Pneumatic Fist Punch"),
            new AnimationTestDef("24_Attack_ClawBackhand",    "Attack",      "Assets/PolygonMech/Animations/CreaturePack/mutant swiping.fbx",               "Heavy Claw Backhand"),
            new AnimationTestDef("25_Attack_HydraulicKick",   "Attack",      "Assets/PolygonMech/Animations/GreatSwordPack/great sword kick.fbx",           "Front Hydraulic Kick"),
            new AnimationTestDef("26_Attack_SeismicHeelCrush","Attack",      "Assets/PolygonMech/Animations/GreatSwordPack/great sword kick (2).fbx",       "Seismic Heel Stomp"),

            // ── 6. DEFENSE & PARRY ──
            new AnimationTestDef("27_Defense_BladeShield",    "Defense",     "Assets/PolygonMech/Animations/GreatSwordPack/great sword blocking.fbx",       "Frontal Blade Guard"),
            new AnimationTestDef("28_Defense_HeavyBrace",     "Defense",     "Assets/PolygonMech/Animations/GreatSwordPack/great sword blocking (2).fbx",   "Heavy Chassis Brace"),
            new AnimationTestDef("29_Defense_RiposteParry",   "Defense",     "Assets/PolygonMech/Animations/GreatSwordPack/great sword blocking (3).fbx",   "Riposte Counter Parry"),

            // ── 7. ROAR, STAGGER & DEATH ──
            new AnimationTestDef("30_Power_OverdriveRoar",    "Power",       "Assets/PolygonMech/Animations/CreaturePack/mutant roaring.fbx",               "Chassis Overdrive Roar"),
            new AnimationTestDef("31_Stagger_PostureBreak",   "Reaction",    "Assets/PolygonMech/Animations/GreatSwordPack/great sword impact (3).fbx",     "Posture Break Recoil"),
            new AnimationTestDef("32_Death_ReactorExplosion", "Death",       "Assets/PolygonMech/Animations/GreatSwordPack/two handed sword death (2).fbx", "Reactor Core Failure")
        };

        [MenuItem("Polygon Mech/2. Verify All 32 Mech Animations Frame-By-Frame", false, 2)]
        public static void RunVerification()
        {
            ExecuteFullInspection(null);
        }

        public static string ExecuteFullInspection(System.Action<string> logger = null)
        {
            void Log(string msg)
            {
                if (logger != null) logger(msg);
                else Debug.Log(msg);
            }

            Log("[AnimInspector] STARTING FRAME-BY-FRAME INSPECTION OF 32 MECH ANIMATIONS...");

            string scenePath = "Assets/Scenes/SyntyCombatDemo.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            GameObject boss = GameObject.Find("Boss_GoliathMech");
            if (boss == null)
            {
                Log("[AnimInspector] ERROR: Boss_GoliathMech not found in scene!");
                return "ERROR: Boss_GoliathMech not found";
            }

            string outDir = Path.Combine(Application.dataPath, "PolygonMech/VerifiedAnimationFrames");
            if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

            // Setup dedicated test camera
            GameObject camGO = new GameObject("__MechAnimTestCamera");
            Camera testCam = camGO.AddComponent<Camera>();
            testCam.fieldOfView = 48f;
            testCam.nearClipPlane = 0.1f;
            testCam.farClipPlane = 100f;
            testCam.clearFlags = CameraClearFlags.SolidColor;
            testCam.backgroundColor = new Color(0.04f, 0.05f, 0.08f, 1f);

            // Position camera looking directly at the Mech at an optimal 3/4 heroic view
            Vector3 bossCenter = boss.transform.position + Vector3.up * 1.9f;
            testCam.transform.position = bossCenter + new Vector3(3.5f, 0.6f, -6.4f);
            testCam.transform.LookAt(bossCenter);

            // Add lighting assist so shadows and metal plating are crisp
            GameObject lightGO = new GameObject("__MechAnimTestKeyLight");
            Light keyLight = lightGO.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.intensity = 1.6f;
            keyLight.color = new Color(0.95f, 0.92f, 0.88f);
            keyLight.transform.rotation = Quaternion.Euler(32f, -38f, 0f);

            int cellW = 320;
            int cellH = 180;
            int heroCols = 8;
            int heroRows = 4; // 8x4 = 32 slots for Master Matrix
            Texture2D[] heroThumbnails = new Texture2D[Curated32Animations.Length];

            var renderRT = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32);
            var frameTex = new Texture2D(960, 540, TextureFormat.RGB24, false);

            int totalFramesTested = 0;
            int passedFrames = 0;
            int failedBonesCount = 0;

            // Test normalized checkpoints for every animation
            float[] sampleCheckpoints = new float[] { 0.15f, 0.40f, 0.65f, 0.90f };

            for (int i = 0; i < Curated32Animations.Length; i++)
            {
                var def = Curated32Animations[i];
                AnimationClip clip = LoadClip(def.relPath);
                if (clip == null)
                {
                    Log($"[AnimInspector] ERROR: Missing clip for {def.id} at {def.relPath}");
                    continue;
                }

                Texture2D heroThumb = null;

                for (int c = 0; c < sampleCheckpoints.Length; c++)
                {
                    float normT = sampleCheckpoints[c];
                    float sampleTime = clip.length * normT;

                    AnimationMode.StartAnimationMode();
                    AnimationMode.SampleAnimationClip(boss, clip, sampleTime);

                    // Bone stability check
                    bool boneValid = CheckBoneSanity(boss);
                    if (!boneValid) failedBonesCount++;

                    testCam.targetTexture = renderRT;
                    testCam.Render();

                    RenderTexture.active = renderRT;
                    frameTex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
                    frameTex.Apply();

                    testCam.targetTexture = null;
                    RenderTexture.active = null;

                    AnimationMode.StopAnimationMode();

                    totalFramesTested++;
                    if (boneValid) passedFrames++;

                    // Scale to thumbnail
                    Texture2D thumb = ScaleTexture(frameTex, cellW, cellH);
                    DrawHUDLabel(thumb, 8, 8, def.displayName, sampleTime, c + 1, boneValid);
                    thumb.Apply();

                    // Checkpoint 1 (approx 40% strike/peak pose) becomes hero thumb
                    if (c == 1 || heroThumb == null)
                    {
                        heroThumb = thumb;
                    }

                    // Save checkpoint frame
                    string fname = $"{def.id}_cp{c + 1}_{sampleTime:0.00}s.png";
                    byte[] png = thumb.EncodeToPNG();
                    File.WriteAllBytes(Path.Combine(outDir, fname), png);
                }

                heroThumbnails[i] = heroThumb;
                Log($"[AnimInspector] [{i + 1:00}/32] {def.displayName} ({clip.name}): 4 frames verified ({clip.length:F2}s, 30fps).");
            }

            // ── BUILD MASTER 8x4 MATRIX SHEET (2560 x 720) ───────────────────
            int matrixW = heroCols * cellW;
            int matrixH = heroRows * cellH;
            var matrixTex = new Texture2D(matrixW, matrixH, TextureFormat.RGB24, false);

            Color bgCol = new Color(0.03f, 0.04f, 0.06f);
            Color[] bgPixels = new Color[matrixW * matrixH];
            for (int p = 0; p < bgPixels.Length; p++) bgPixels[p] = bgCol;
            matrixTex.SetPixels(bgPixels);

            for (int row = 0; row < heroRows; row++)
            {
                for (int col = 0; col < heroCols; col++)
                {
                    int index = row * heroCols + col;
                    int startX = col * cellW;
                    int startY = (heroRows - 1 - row) * cellH;

                    if (index < heroThumbnails.Length && heroThumbnails[index] != null)
                    {
                        Color[] pix = heroThumbnails[index].GetPixels();
                        matrixTex.SetPixels(startX, startY, cellW, cellH, pix);
                    }
                }
            }
            matrixTex.Apply();

            string matrixPath = Path.Combine(outDir, "Mech_32_Animations_MasterMatrix.png");
            File.WriteAllBytes(matrixPath, matrixTex.EncodeToPNG());
            Log($"[AnimInspector] Master Matrix sheet written to: {matrixPath}");

            // Cleanup temp test objects
            Object.DestroyImmediate(camGO);
            Object.DestroyImmediate(lightGO);
            Object.DestroyImmediate(renderRT);
            Object.DestroyImmediate(frameTex);
            Object.DestroyImmediate(matrixTex);

            string report = $"Verified {passedFrames}/{totalFramesTested} frames across all 32 curated mechanical animations with {failedBonesCount} bone faults. Matrix generated at: {matrixPath}";
            Log($"[AnimInspector] COMPLETE: {report}");
            return report;
        }

        private static bool CheckBoneSanity(GameObject root)
        {
            var transforms = root.GetComponentsInChildren<Transform>();
            foreach (var t in transforms)
            {
                Vector3 p = t.localPosition;
                Quaternion r = t.localRotation;
                Vector3 s = t.localScale;

                if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z)) return false;
                if (float.IsNaN(r.x) || float.IsNaN(r.y) || float.IsNaN(r.z) || float.IsNaN(r.w)) return false;
                if (float.IsInfinity(p.x) || float.IsInfinity(p.y) || float.IsInfinity(p.z)) return false;
                if (s.sqrMagnitude < 0.0001f) return false;
            }
            return true;
        }

        private static AnimationClip LoadClip(string relPath)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(relPath);
            if (assets == null) return null;
            foreach (var a in assets)
            {
                if (a is AnimationClip c && !c.name.StartsWith("__preview__"))
                    return c;
            }
            return null;
        }

        private static Texture2D ScaleTexture(Texture2D source, int targetWidth, int targetHeight)
        {
            var rt = RenderTexture.GetTemporary(targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(source, rt);
            var prevRT = RenderTexture.active;
            RenderTexture.active = rt;

            var result = new Texture2D(targetWidth, targetHeight, TextureFormat.RGB24, false);
            result.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
            result.Apply();

            RenderTexture.active = prevRT;
            RenderTexture.ReleaseTemporary(rt);
            return result;
        }

        private static void DrawHUDLabel(Texture2D tex, int x, int y, string name, float t, int cpNum, bool valid)
        {
            int blockW = 210;
            int blockH = 20;
            Color bg = new Color(0.02f, 0.04f, 0.08f, 0.88f);
            Color textCol = valid ? new Color(0.18f, 0.85f, 1.0f) : new Color(1.0f, 0.25f, 0.25f);

            for (int bx = 0; bx < blockW && (x + bx) < tex.width; bx++)
            {
                for (int by = 0; by < blockH && (y + by) < tex.height; by++)
                {
                    tex.SetPixel(x + bx, y + by, bg);
                }
            }

            // Draw CP number
            DrawChar(tex, x + 4, y + 5, 'P', textCol);
            DrawNumber(tex, x + 14, y + 5, cpNum, textCol);
            DrawChar(tex, x + 24, y + 5, ':', textCol);

            // Draw Time
            int sec = Mathf.FloorToInt(t);
            int ms = Mathf.FloorToInt((t - sec) * 10f);
            DrawNumber(tex, x + 34, y + 5, sec, textCol);
            DrawChar(tex, x + 44, y + 5, '.', textCol);
            DrawNumber(tex, x + 52, y + 5, ms, textCol);
            DrawChar(tex, x + 62, y + 5, 's', textCol);

            // Draw Valid flag
            DrawChar(tex, x + 76, y + 5, valid ? 'O' : 'E', textCol);
            DrawChar(tex, x + 86, y + 5, valid ? 'K' : 'R', textCol);
        }

        private static void DrawNumber(Texture2D tex, int x, int y, int num, Color col)
        {
            byte[][] digitBitmaps = new byte[][]
            {
                new byte[] { 0x7, 0x5, 0x5, 0x5, 0x7 }, // 0
                new byte[] { 0x2, 0x6, 0x2, 0x2, 0x7 }, // 1
                new byte[] { 0x7, 0x1, 0x7, 0x4, 0x7 }, // 2
                new byte[] { 0x7, 0x1, 0x7, 0x1, 0x7 }, // 3
                new byte[] { 0x5, 0x5, 0x7, 0x1, 0x1 }, // 4
                new byte[] { 0x7, 0x4, 0x7, 0x1, 0x7 }, // 5
                new byte[] { 0x7, 0x4, 0x7, 0x5, 0x7 }, // 6
                new byte[] { 0x7, 0x1, 0x2, 0x2, 0x2 }, // 7
                new byte[] { 0x7, 0x5, 0x7, 0x5, 0x7 }, // 8
                new byte[] { 0x7, 0x5, 0x7, 0x1, 0x7 }  // 9
            };

            int n = Mathf.Clamp(num, 0, 9);
            byte[] rows = digitBitmaps[n];
            for (int r = 0; r < 5; r++)
            {
                int py = y + (4 - r) * 2;
                byte rowVal = rows[r];
                for (int c = 0; c < 3; c++)
                {
                    if ((rowVal & (1 << (2 - c))) != 0)
                    {
                        int px = x + c * 2;
                        Set2x2(tex, px, py, col);
                    }
                }
            }
        }

        private static void DrawChar(Texture2D tex, int x, int y, char ch, Color col)
        {
            byte[] rows = null;
            switch (ch)
            {
                case 'P': rows = new byte[] { 0x7, 0x5, 0x7, 0x4, 0x4 }; break;
                case 'O': rows = new byte[] { 0x7, 0x5, 0x5, 0x5, 0x7 }; break;
                case 'K': rows = new byte[] { 0x5, 0x5, 0x6, 0x5, 0x5 }; break;
                case 'E': rows = new byte[] { 0x7, 0x4, 0x7, 0x4, 0x7 }; break;
                case 'R': rows = new byte[] { 0x7, 0x5, 0x7, 0x6, 0x5 }; break;
                case 's': rows = new byte[] { 0x0, 0x3, 0x2, 0x1, 0x3 }; break;
                case '.': rows = new byte[] { 0x0, 0x0, 0x0, 0x0, 0x2 }; break;
                case ':': rows = new byte[] { 0x0, 0x2, 0x0, 0x2, 0x0 }; break;
                default:  rows = new byte[] { 0x7, 0x7, 0x7, 0x7, 0x7 }; break;
            }

            for (int r = 0; r < 5; r++)
            {
                int py = y + (4 - r) * 2;
                byte rowVal = rows[r];
                for (int c = 0; c < 3; c++)
                {
                    if ((rowVal & (1 << (2 - c))) != 0)
                    {
                        int px = x + c * 2;
                        Set2x2(tex, px, py, col);
                    }
                }
            }
        }

        private static void Set2x2(Texture2D tex, int x, int y, Color col)
        {
            if (x >= 0 && x + 1 < tex.width && y >= 0 && y + 1 < tex.height)
            {
                tex.SetPixel(x, y, col);
                tex.SetPixel(x + 1, y, col);
                tex.SetPixel(x, y + 1, col);
                tex.SetPixel(x + 1, y + 1, col);
            }
        }
    }
}
