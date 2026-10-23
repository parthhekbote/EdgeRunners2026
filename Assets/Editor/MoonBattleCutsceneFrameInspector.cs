using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using Alita;

namespace InsaneGun.Editor
{
    public static class MoonBattleCutsceneFrameInspector
    {
        [MenuItem("Cyberpunk/Dump All Cutscene Frames And Build Matrix")]
        public static void DumpFramesAndBuildMatrix()
        {
            var prevFilter = Debug.unityLogger.filterLogType;
            try
            {
                Debug.unityLogger.filterLogType = LogType.Assert;

                string scenePath = "Assets/Scenes/Scene_Moon_Battle_Flashback.unity";
                var activeScene = EditorSceneManager.GetActiveScene();
                if (activeScene.path != scenePath)
                {
                    EditorSceneManager.SaveOpenScenes();
                    EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                }

                var director = Object.FindFirstObjectByType<AlitaMoonBattleCutsceneDirector>();
                if (director == null || director.cinematicCamera == null)
                {
                    Debug.LogError("[FrameInspector] Director or Cinematic Camera not found!");
                    return;
                }

                director.EnsureBattleArmies();
                director.LoadAudioTrack();
                director.HideSceneGameplayHUD();

                string outDir = Path.Combine(Application.dataPath, "../VerifiedCutsceneFrames/MoonBattle");
                if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

                // Load authentic humanoid combat clips
                var clipShootRifle = GetClipFromFBX("Assets/Animations/Shoot Rifle.fbx");
                var clipGunWalk = GetClipFromFBX("Assets/InsaneGun_Sword_Set/Animation/Humanoid/Inplace/atk_gun_walking_inplace.fbx");
                var clipGun01 = GetClipFromFBX("Assets/InsaneGun_Sword_Set/Animation/Humanoid/Inplace/atk_gun_01_inplace.fbx");
                var clipGun02 = GetClipFromFBX("Assets/InsaneGun_Sword_Set/Animation/Humanoid/Inplace/atk_gun_02_inplace.fbx");
                var clipGun03 = GetClipFromFBX("Assets/InsaneGun_Sword_Set/Animation/Humanoid/Inplace/atk_gun_03_inplace.fbx");
                var clipGun04 = GetClipFromFBX("Assets/InsaneGun_Sword_Set/Animation/Humanoid/Inplace/atk_gun_04_inplace.fbx");
                var clipRun = GetClipFromFBX("Assets/InsaneGun_Sword_Set/Animation/Humanoid/Inplace/move_run_front_inplace.fbx");
                var clipStrafe = GetClipFromFBX("Assets/InsaneGun_Sword_Set/Animation/Humanoid/Inplace/run_strafe_front_inplace.fbx");
                var clipJump = GetClipFromFBX("Assets/InsaneGun_Sword_Set/Animation/Humanoid/Inplace/jump_inplace.fbx");
                var clipRoll = GetClipFromFBX("Assets/InsaneGun_Sword_Set/Animation/Humanoid/Inplace/roll_front_inplace.fbx");
                var clipReload = GetClipFromFBX("Assets/InsaneGun_Sword_Set/Animation/Humanoid/Inplace/reload_inplace.fbx");
                var clipHit = GetClipFromFBX("Assets/InsaneGun_Sword_Set/Animation/Humanoid/Inplace/hit_heavy_front_inplace.fbx");
                var clipIdle = GetClipFromFBX("Assets/InsaneGun_Sword_Set/Animation/Humanoid/Inplace/N_idle_inplace.fbx");

                var cam = director.cinematicCamera;
                var earthObj = GameObject.Find("Earth_In_Lunar_Sky");

                // ── 1. Render all 52 Frames at 4 FPS (13 seconds x 4 fps = 52 frames) ───
                int totalFrames = 52;
                int cols = 4;
                int rows = 13; // 13 seconds x 4 frames per second = 52 frames
                int cellW = 320;
                int cellH = 180;

                Texture2D[] frameThumbnails = new Texture2D[totalFrames];
                var renderRT = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32);

                for (int f = 0; f < totalFrames; f++)
                {
                    float t = f * 0.25f; // 4 frames per second (T = 0.00s to 12.75s)
                    director.ScrubTo(t);

                    if (earthObj != null)
                    {
                        earthObj.transform.rotation = Quaternion.LookRotation((cam.transform.position - earthObj.transform.position).normalized, Vector3.up);
                    }

                    var frameTex = new Texture2D(960, 540, TextureFormat.RGB24, false);

                    AnimationMode.StartAnimationMode();

                    SampleCombatChoreography(
                        director, t,
                        clipShootRifle,
                        clipGunWalk, clipGun01, clipGun02, clipGun03, clipGun04,
                        clipRun, clipStrafe, clipJump, clipRoll, clipReload, clipHit, clipIdle);

                    cam.targetTexture = renderRT;
                    cam.Render();

                    RenderTexture.active = renderRT;
                    frameTex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
                    frameTex.Apply();

                    cam.targetTexture = null;
                    RenderTexture.active = null;

                    AnimationMode.StopAnimationMode();

                    // Memory transition white flash fade matching movie reference F01-F02 and ending transition
                    float flashAlpha = 0f;
                    if (t < 0.45f)
                    {
                        flashAlpha = Mathf.Lerp(0.92f, 0.0f, t / 0.45f);
                    }
                    else if (t >= 12.75f)
                    {
                        flashAlpha = 0.25f;
                    }

                    if (flashAlpha > 0.01f)
                    {
                        Color[] pixels = frameTex.GetPixels();
                        Color flashCol = Color.white;
                        for (int p = 0; p < pixels.Length; p++)
                        {
                            pixels[p] = Color.Lerp(pixels[p], flashCol, flashAlpha);
                        }
                        frameTex.SetPixels(pixels);
                        frameTex.Apply();
                    }

                    // Downscale to 320x180 for matrix cell
                    var thumb = ScaleTexture(frameTex, cellW, cellH);
                    frameThumbnails[f] = thumb;

                    // Burn timestamp label onto thumbnail (bottom-left)
                    DrawTimestamp(thumb, 10, 12, t, f + 1);
                    thumb.Apply();

                    // Save individual frame
                    byte[] pngBytes = frameTex.EncodeToPNG();
                    string framePath = Path.Combine(outDir, $"frame_4fps_{f + 1:00}_sec_{t:0.00}s.png");
                    File.WriteAllBytes(framePath, pngBytes);

                    Object.DestroyImmediate(frameTex);
                }

                Object.DestroyImmediate(renderRT);

                // ── 2. Assemble Master 52-Frame Contact Sheet Matrix (1280 x 2340) ─────────
                int matrixW = cols * cellW;
                int matrixH = rows * cellH;
                var matrixTex = new Texture2D(matrixW, matrixH, TextureFormat.RGB24, false);

                Color bgCol = new Color(0.03f, 0.05f, 0.08f);
                Color[] bgPixels = new Color[matrixW * matrixH];
                for (int p = 0; p < bgPixels.Length; p++) bgPixels[p] = bgCol;
                matrixTex.SetPixels(bgPixels);

                for (int row = 0; row < rows; row++)
                {
                    for (int col = 0; col < cols; col++)
                    {
                        int index = row * cols + col;
                        int startX = col * cellW;
                        int startY = (rows - 1 - row) * cellH;

                        if (index < totalFrames && frameThumbnails[index] != null)
                        {
                            var thumb = frameThumbnails[index];
                            Color[] thumbPixels = thumb.GetPixels();
                            matrixTex.SetPixels(startX, startY, cellW, cellH, thumbPixels);
                        }
                    }
                }

                for (int i = 0; i < frameThumbnails.Length; i++)
                {
                    if (frameThumbnails[i] != null) Object.DestroyImmediate(frameThumbnails[i]);
                }

                matrixTex.Apply();
                byte[] matrixBytes = matrixTex.EncodeToPNG();
                Object.DestroyImmediate(matrixTex);

                string matrixFilePath = Path.Combine(outDir, "moon_battle_cutscene_frame_matrix.png");
                File.WriteAllBytes(matrixFilePath, matrixBytes);
                Debug.Log($"[FrameInspector] Successfully built 4-FPS Cutscene Matrix: {matrixFilePath} ({matrixBytes.Length} bytes)");

                string docDir = Path.Combine(Application.dataPath, "../Documentation/Images");
                if (!Directory.Exists(docDir)) Directory.CreateDirectory(docDir);
                File.WriteAllBytes(Path.Combine(docDir, "moon_battle_cutscene_frame_matrix.png"), matrixBytes);

                // ── 3. Render 7 Hero 1080p Cinematic Frames ──────────────────────────────
                DumpAllFrames();

                AssetDatabase.Refresh();
                Debug.Log("[FrameInspector] Completed 4-FPS Frame Analysis and Matrix Generation!");
            }
            finally
            {
                Debug.unityLogger.filterLogType = prevFilter;
            }
        }

        [MenuItem("Cyberpunk/Dump All 6 Moon Battle Frames")]
        public static void DumpAllFrames()
        {
            var prevFilter = Debug.unityLogger.filterLogType;
            try
            {
                Debug.unityLogger.filterLogType = LogType.Assert;

                string scenePath = "Assets/Scenes/Scene_Moon_Battle_Flashback.unity";
                var activeScene = EditorSceneManager.GetActiveScene();
                if (activeScene.path != scenePath)
                {
                    EditorSceneManager.SaveOpenScenes();
                    EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                }

                var director = Object.FindFirstObjectByType<AlitaMoonBattleCutsceneDirector>();
                if (director == null || director.cinematicCamera == null)
                {
                    Debug.LogError("[FrameInspector] Director or Cinematic Camera not found!");
                    return;
                }

                director.EnsureBattleArmies();
                director.LoadAudioTrack();
                director.HideSceneGameplayHUD();

                string outDir = Path.Combine(Application.dataPath, "../VerifiedCutsceneFrames/MoonBattle");
                if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

                var clipShootRifle = GetClipFromFBX("Assets/Animations/Shoot Rifle.fbx");
                var clipGunWalk = GetClipFromFBX("Assets/InsaneGun_Sword_Set/Animation/Humanoid/Inplace/atk_gun_walking_inplace.fbx");
                var clipGun01 = GetClipFromFBX("Assets/InsaneGun_Sword_Set/Animation/Humanoid/Inplace/atk_gun_01_inplace.fbx");
                var clipGun02 = GetClipFromFBX("Assets/InsaneGun_Sword_Set/Animation/Humanoid/Inplace/atk_gun_02_inplace.fbx");
                var clipGun03 = GetClipFromFBX("Assets/InsaneGun_Sword_Set/Animation/Humanoid/Inplace/atk_gun_03_inplace.fbx");
                var clipGun04 = GetClipFromFBX("Assets/InsaneGun_Sword_Set/Animation/Humanoid/Inplace/atk_gun_04_inplace.fbx");
                var clipRun = GetClipFromFBX("Assets/InsaneGun_Sword_Set/Animation/Humanoid/Inplace/move_run_front_inplace.fbx");
                var clipStrafe = GetClipFromFBX("Assets/InsaneGun_Sword_Set/Animation/Humanoid/Inplace/run_strafe_front_inplace.fbx");
                var clipJump = GetClipFromFBX("Assets/InsaneGun_Sword_Set/Animation/Humanoid/Inplace/jump_inplace.fbx");
                var clipRoll = GetClipFromFBX("Assets/InsaneGun_Sword_Set/Animation/Humanoid/Inplace/roll_front_inplace.fbx");
                var clipReload = GetClipFromFBX("Assets/InsaneGun_Sword_Set/Animation/Humanoid/Inplace/reload_inplace.fbx");
                var clipHit = GetClipFromFBX("Assets/InsaneGun_Sword_Set/Animation/Humanoid/Inplace/hit_heavy_front_inplace.fbx");
                var clipIdle = GetClipFromFBX("Assets/InsaneGun_Sword_Set/Animation/Humanoid/Inplace/N_idle_inplace.fbx");

                // 7 Key Hero Timestamps matching the 7 authentic movie shots
                float[] testTimes = new float[] { 1.2f, 2.9f, 4.6f, 6.4f, 8.4f, 9.9f, 11.8f };
                string[] frameNames = new string[]
                {
                    "frame_01_boot_sprint_impact_1.2s.png",
                    "frame_02_zerog_spacefleet_ascent_2.9s.png",
                    "frame_03_wide_crater_concussion_4.6s.png",
                    "frame_04_alita_3point_hero_slide_6.4s.png",
                    "frame_05_commander_gelda_burst_8.4s.png",
                    "frame_06_alita_spring_acceleration_9.9s.png",
                    "frame_07_zerog_melee_tackle_11.8s.png"
                };

                var cam = director.cinematicCamera;
                var earthObj = GameObject.Find("Earth_In_Lunar_Sky");

                for (int i = 0; i < testTimes.Length; i++)
                {
                    float t = testTimes[i];
                    director.ScrubTo(t);

                    if (earthObj != null)
                    {
                        earthObj.transform.rotation = Quaternion.LookRotation((cam.transform.position - earthObj.transform.position).normalized, Vector3.up);
                    }

                    var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);

                    AnimationMode.StartAnimationMode();

                    SampleCombatChoreography(
                        director, t,
                        clipShootRifle,
                        clipGunWalk, clipGun01, clipGun02, clipGun03, clipGun04,
                        clipRun, clipStrafe, clipJump, clipRoll, clipReload, clipHit, clipIdle);

                    var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
                    cam.targetTexture = rt;
                    cam.Render();

                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                    tex.Apply();

                    cam.targetTexture = null;
                    RenderTexture.active = null;
                    Object.DestroyImmediate(rt);

                    AnimationMode.StopAnimationMode();

                    byte[] bytes = tex.EncodeToPNG();
                    Object.DestroyImmediate(tex);

                    string filePath = Path.Combine(outDir, frameNames[i]);
                    File.WriteAllBytes(filePath, bytes);
                    Debug.Log($"[FrameInspector] Rendered Hero Frame {i + 1}/{testTimes.Length}: {frameNames[i]} ({bytes.Length} bytes)");

                    if (i == 1 || i == 3)
                    {
                        string showcaseDest = Path.Combine(Application.dataPath, "../Documentation/Images/inengine_moon_battle_flashback.png");
                        File.WriteAllBytes(showcaseDest, bytes);
                    }
                }

                AssetDatabase.Refresh();
            }
            finally
            {
                Debug.unityLogger.filterLogType = prevFilter;
            }
        }

        private static void SampleCombatChoreography(
            AlitaMoonBattleCutsceneDirector director,
            float t,
            AnimationClip clipShootRifle,
            AnimationClip clipGunWalk,
            AnimationClip clipGun01,
            AnimationClip clipGun02,
            AnimationClip clipGun03,
            AnimationClip clipGun04,
            AnimationClip clipRun,
            AnimationClip clipStrafe,
            AnimationClip clipJump,
            AnimationClip clipRoll,
            AnimationClip clipReload,
            AnimationClip clipHit,
            AnimationClip clipIdle)
        {
            // 1. Alita Motions
            if (director.alitaHero != null)
            {
                if (t < 2.5f)
                {
                    // Boot sprint (Ref F1-F10)
                    var c = (clipRun != null) ? clipRun : clipGunWalk;
                    if (c != null) SafeSample(director.alitaHero, c, (t * 2.2f) % c.length);
                }
                else if (t < 3.5f)
                {
                    // Zero-g leap (Ref F11-F14)
                    var c = (clipJump != null) ? clipJump : clipGun02;
                    float leapT = (t - 2.5f) / 1.0f;
                    if (c != null) SafeSample(director.alitaHero, c, Mathf.Clamp(leapT * c.length * 0.9f, 0f, c.length - 0.05f));
                }
                else if (t < 5.8f)
                {
                    // Charging across crater (Ref F15-F24)
                    var c = (clipRun != null) ? clipRun : clipGun01;
                    if (c != null) SafeSample(director.alitaHero, c, ((t - 3.5f) * 1.5f) % c.length);
                }
                else if (t < 7.4f)
                {
                    // 3-point hero slide (Ref F25-F30)
                    var c = (clipRoll != null) ? clipRoll : clipHit;
                    float slideT = (t - 5.8f) / 1.6f;
                    if (c != null) SafeSample(director.alitaHero, c, Mathf.Clamp(slideT * c.length * 0.8f, 0f, c.length - 0.05f));
                }
                else if (t < 9.4f)
                {
                    // Crouched hero stance (Ref F31-F38)
                    if (clipIdle != null) SafeSample(director.alitaHero, clipIdle, (t - 7.4f) % clipIdle.length);
                }
                else if (t < 10.4f)
                {
                    // Spring and charge (Ref F39-F42)
                    var c = (clipRun != null) ? clipRun : clipGunWalk;
                    if (c != null) SafeSample(director.alitaHero, c, ((t - 9.4f) * 2.5f) % c.length);
                }
                else
                {
                    // Mid-air soaring zero-g kick / slice (Ref F43-F52)
                    var c = (clipJump != null) ? clipJump : (clipGun04 != null ? clipGun04 : clipGun01);
                    float soarT = (t - 10.4f) / 2.6f;
                    if (c != null) SafeSample(director.alitaHero, c, Mathf.Clamp(soarT * c.length * 0.85f, 0f, c.length - 0.05f));
                }
            }

            // 2. Commander Gelda Motions
            if (director.commanderGelda != null)
            {
                if (t < 7.4f)
                {
                    var c = (clipGunWalk != null) ? clipGunWalk : clipRun;
                    if (c != null) SafeSample(director.commanderGelda, c, (t * 1.3f) % c.length);
                }
                else if (t < 9.4f)
                {
                    // Firing battle rifle in bursts (Ref F31-F38)
                    var c = (clipShootRifle != null) ? clipShootRifle : clipGun01;
                    if (c != null) SafeSample(director.commanderGelda, c, ((t - 7.4f) * 1.6f) % c.length);
                }
                else
                {
                    var c = (clipGunWalk != null) ? clipGunWalk : clipShootRifle;
                    if (c != null) SafeSample(director.commanderGelda, c, (t * 1.2f) % c.length);
                }
            }

            // 3. Squad Platoon Motions
            for (int s = 0; s < director.squadTroops.Count; s++)
            {
                var sq = director.squadTroops[s];
                if (sq == null) continue;

                if (t < 2.5f)
                {
                    var c = (clipRun != null) ? clipRun : clipGunWalk;
                    if (c != null) SafeSample(sq, c, (t * 2.0f + s * 0.35f) % c.length);
                }
                else if (t < 3.5f)
                {
                    var c = (s % 2 == 0) ? clipJump : (clipShootRifle != null ? clipShootRifle : clipGun02);
                    float leapProg = (t - 2.5f) / 1.0f;
                    if (c != null) SafeSample(sq, c, Mathf.Clamp(leapProg * c.length * 0.85f, 0f, c.length - 0.05f));
                }
                else if (t >= 10.4f && s == 0)
                {
                    // Soldier tackling in melee combat
                    var c = (clipRoll != null) ? clipRoll : clipGun04;
                    float tackleT = (t - 10.4f) / 2.6f;
                    if (c != null) SafeSample(sq, c, Mathf.Clamp(tackleT * c.length * 0.9f, 0f, c.length - 0.05f));
                }
                else
                {
                    var c = (s % 3 == 0 && clipShootRifle != null) ? clipShootRifle :
                            ((s % 3 == 1 && clipGun01 != null) ? clipGun01 : clipGunWalk);
                    if (c != null) SafeSample(sq, c, (t * 1.5f + s * 0.4f) % c.length);
                }
            }

            // 4. Enemy Hostiles
            for (int e = 0; e < director.enemyTroops.Count; e++)
            {
                var en = director.enemyTroops[e];
                if (en == null) continue;

                if (t >= 10.4f && e == 0)
                {
                    // Enemy taking melee tackle hit
                    var c = (clipHit != null) ? clipHit : clipGun03;
                    float clashT = (t - 10.4f) / 2.6f;
                    if (c != null) SafeSample(en, c, Mathf.Clamp(clashT * c.length, 0f, c.length - 0.05f));
                }
                else
                {
                    var c = (e % 2 == 0 && clipShootRifle != null) ? clipShootRifle :
                            ((clipGun03 != null) ? clipGun03 : clipIdle);
                    if (c != null) SafeSample(en, c, (t * 1.4f + e * 0.5f) % c.length);
                }
            }
        }

        private static void SafeSample(GameObject target, AnimationClip clip, float time)
        {
            if (target == null || clip == null) return;
            try
            {
                clip.SampleAnimation(target, time);
            }
            catch {}
        }

        private static AnimationClip GetClipFromFBX(string assetPath)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            foreach (var a in assets)
            {
                if (a is AnimationClip c && !c.name.StartsWith("__preview__"))
                {
                    return c;
                }
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

        private static void DrawTimestamp(Texture2D tex, int x, int y, float t, int frameNum)
        {
            int blockW = 105;
            int blockH = 18;
            Color bg = new Color(0.04f, 0.07f, 0.12f, 0.85f);
            Color cyan = new Color(0.15f, 0.85f, 1.0f);

            for (int bx = 0; bx < blockW && (x + bx) < tex.width; bx++)
            {
                for (int by = 0; by < blockH && (y + by) < tex.height; by++)
                {
                    tex.SetPixel(x + bx, y + by, bg);
                }
            }

            int sec = Mathf.FloorToInt(t);
            int ms = Mathf.FloorToInt((t - sec) * 100f);
            DrawNumber(tex, x + 4, y + 4, sec, cyan);
            DrawChar(tex, x + 16, y + 4, '.', cyan);
            DrawNumber(tex, x + 24, y + 4, ms / 10, cyan);
            DrawNumber(tex, x + 34, y + 4, ms % 10, cyan);
            DrawChar(tex, x + 44, y + 4, 's', cyan);
            DrawChar(tex, x + 58, y + 4, 'F', cyan);
            DrawNumber(tex, x + 70, y + 4, frameNum / 10, cyan);
            DrawNumber(tex, x + 80, y + 4, frameNum % 10, cyan);
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
            if (ch == '.')
            {
                Set2x2(tex, x, y, col);
            }
            else if (ch == 's')
            {
                Set2x2(tex, x + 2, y + 6, col);
                Set2x2(tex, x, y + 4, col);
                Set2x2(tex, x + 2, y + 2, col);
                Set2x2(tex, x + 4, y, col);
                Set2x2(tex, x, y, col);
            }
            else if (ch == 'F')
            {
                Set2x2(tex, x, y, col);
                Set2x2(tex, x, y + 2, col);
                Set2x2(tex, x, y + 4, col);
                Set2x2(tex, x, y + 6, col);
                Set2x2(tex, x, y + 8, col);
                Set2x2(tex, x + 2, y + 8, col);
                Set2x2(tex, x + 4, y + 8, col);
                Set2x2(tex, x + 2, y + 4, col);
            }
        }

        private static void Set2x2(Texture2D tex, int x, int y, Color col)
        {
            for (int dx = 0; dx < 2 && (x + dx) < tex.width; dx++)
            {
                for (int dy = 0; dy < 2 && (y + dy) < tex.height; dy++)
                {
                    tex.SetPixel(x + dx, y + dy, col);
                }
            }
        }
    }
}
