using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Alita.Editor
{
    /// <summary>
    /// Master Scene Builder for Alita's 120-Second TRON: Ares Awakening Cutscene.
    /// Perfectly mirrors the full IMAX 'Init' opening sequence from 0:00 to 1:20:
    /// 1. Glowing Spinning TRON Identity Disc / Quantum Portal in Obsidian Void (0:00 - 0:18)
    /// 2. Towering Circuit Monolith Data Highway Corridor Flythrough & Title Cards (0:18 - 0:45)
    /// 3. Dr. Dyson Ido at Cybernetic Terminal furiously typing Neural Reboot commands (0:45 - 0:56)
    /// 4. Macro Cybernetic Body Assembly of Alita (from Human-Custom.sk) & Optic Flares (0:56 - 1:16)
    /// 5. Dr. Ido slams final execute key -> Sub-bass flash drop & blackout (1:16 - 1:20)
    /// 6. HARD CUT on 1:20 drop to Iconic Wide Profile Silhouette Sit-Up in 6-Sided Red Grid Chamber (1:20 - 1:36)
    /// 7. Obsidian floor touchdown & ready combat stance (1:36 - 2:00)
    /// </summary>
    public static class AlitaTronAwakeningSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Alita_Tron_Awakening.unity";
        public const string AlitaPrefabPath = "Assets/Prefabs/Alita_Sidekick/Alita_02_Normal_DollBody.prefab";
        public const string DoctorPrefabPath = "Assets/PolygonCyberCity/Prefabs/Characters/SM_Chr_Rich_Male_01.prefab";
        public const string ConsolePrefabPath = "Assets/PolygonCyberCity/Prefabs/Props/SM_Prop_Console_01.prefab";
        public const string MusicAudioPath = "Assets/Audio/NIN_Init_Full_0_to_120.wav";
        public const string PostProcessProfilePath = "Assets/Timelines/Alita_Cutscene_PostProcess_Profile.asset";
        public const string RingMeshPath = "Assets/Meshes/SM_Tron_Disc_Ring.asset";

        [MenuItem("Tools/Alita Battle Angel/Build TRON Ares Awakening Cutscene (0:00 to 1:20 Full)")]
        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Build Materials
            var redGridShader = Shader.Find("Cyberpunk/Tron_Red_Cube_Grid") ?? Shader.Find("Cyberpunk/Tron_Infinite_Floor_Grid");
            var redGridMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Tron/Mat_Tron_Red_Cube_Grid.mat");
            if (redGridMat == null)
            {
                redGridMat = new Material(redGridShader);
                AssetDatabase.CreateAsset(redGridMat, "Assets/Materials/Tron/Mat_Tron_Red_Cube_Grid.mat");
            }
            else
            {
                redGridMat.shader = redGridShader;
            }
            redGridMat.SetColor("_GridColor", new Color(5.5f, 0.08f, 0.16f, 1f));
            redGridMat.SetFloat("_GridSpacing", 1.0f);
            redGridMat.SetFloat("_LineWidth", 0.025f);
            redGridMat.SetFloat("_FadeStart", 40f);
            redGridMat.SetFloat("_FadeEnd", 200f);
            redGridMat.SetFloat("_BaseAlpha", 1.0f);
            EditorUtility.SetDirty(redGridMat);

            // Semi-transparent Floor Grid Material for Obsidian Mirror reflection
            var floorGridMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Tron/Mat_Tron_Floor_Red_Grid.mat");
            if (floorGridMat == null)
            {
                floorGridMat = new Material(redGridShader);
                AssetDatabase.CreateAsset(floorGridMat, "Assets/Materials/Tron/Mat_Tron_Floor_Red_Grid.mat");
            }
            else
            {
                floorGridMat.shader = redGridShader;
            }
            floorGridMat.SetColor("_GridColor", new Color(4.5f, 0.08f, 0.16f, 1f));
            floorGridMat.SetFloat("_GridSpacing", 4.5f);
            floorGridMat.SetFloat("_LineWidth", 0.010f);
            floorGridMat.SetFloat("_FadeStart", 60f);
            floorGridMat.SetFloat("_FadeEnd", 300f);
            floorGridMat.SetFloat("_BaseAlpha", 0.88f);
            EditorUtility.SetDirty(floorGridMat);

            // Pure Obsidian Mirror Floor Material (Cyberspace Planar Mirror)
            var mirrorShader = Shader.Find("Cyberpunk/Cyberspace_Mirror_Floor");
            var mirrorMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Tron/Mat_Tron_Obsidian_Mirror.mat");
            if (mirrorMat == null)
            {
                mirrorMat = new Material(mirrorShader != null ? mirrorShader : Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mirrorMat, "Assets/Materials/Tron/Mat_Tron_Obsidian_Mirror.mat");
            }
            else if (mirrorShader != null)
            {
                mirrorMat.shader = mirrorShader;
            }
            mirrorMat.SetColor("_BaseColor", new Color(0.001f, 0.001f, 0.002f, 1f));
            mirrorMat.SetFloat("_MirrorReflectivity", 0.98f);
            mirrorMat.SetFloat("_DistortionStrength", 0.0008f);
            mirrorMat.SetFloat("_FresnelPower", 1.8f);
            mirrorMat.SetFloat("_SpecularGloss", 0.98f);
            mirrorMat.SetFloat("_SpecularIntensity", 2.0f);
            EditorUtility.SetDirty(mirrorMat);

            // Searing White Neon Edge Tube Material
            var whiteNeonMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Tron/Mat_Tron_White_Neon_Tube.mat");
            if (whiteNeonMat == null)
            {
                whiteNeonMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(whiteNeonMat, "Assets/Materials/Tron/Mat_Tron_White_Neon_Tube.mat");
            }
            whiteNeonMat.SetColor("_BaseColor", new Color(4.5f, 4.5f, 4.8f, 1f));
            EditorUtility.SetDirty(whiteNeonMat);

            // Electric Orange / Amber Neon Tube Material (For Disc & Circuit Pillars)
            var orangeNeonMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Tron/Mat_Tron_Orange_Neon.mat");
            if (orangeNeonMat == null)
            {
                orangeNeonMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(orangeNeonMat, "Assets/Materials/Tron/Mat_Tron_Orange_Neon.mat");
            }
            orangeNeonMat.SetColor("_BaseColor", new Color(5.0f, 1.8f, 0.2f, 1f));
            EditorUtility.SetDirty(orangeNeonMat);

            // Cyan Holographic Terminal Screen Material
            var cyanCliMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Tron/Mat_Tron_Cyan_CLI.mat");
            if (cyanCliMat == null)
            {
                cyanCliMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(cyanCliMat, "Assets/Materials/Tron/Mat_Tron_Cyan_CLI.mat");
            }
            cyanCliMat.SetColor("_BaseColor", new Color(0.2f, 2.2f, 3.0f, 1f));
            EditorUtility.SetDirty(cyanCliMat);

            // Matte Black Slab Material
            var blackSlabMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Tron/Mat_Tron_Black_Slab.mat");
            if (blackSlabMat == null)
            {
                blackSlabMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(blackSlabMat, "Assets/Materials/Tron/Mat_Tron_Black_Slab.mat");
            }
            blackSlabMat.SetColor("_BaseColor", new Color(0.015f, 0.015f, 0.02f, 1f));
            blackSlabMat.SetFloat("_Smoothness", 0.35f);
            blackSlabMat.SetFloat("_Metallic", 0.1f);
            EditorUtility.SetDirty(blackSlabMat);

            // Searing White Core Material (Controlled incandescent solar core)
            var whiteCoreMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Tron/Mat_Tron_White_Core.mat");
            if (whiteCoreMat == null)
            {
                whiteCoreMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(whiteCoreMat, "Assets/Materials/Tron/Mat_Tron_White_Core.mat");
            }
            whiteCoreMat.SetColor("_BaseColor", new Color(2.4f, 2.0f, 1.8f, 1f));
            EditorUtility.SetDirty(whiteCoreMat);

            // Red-Orange Circuit Neon Material (Prologue Primary - Fiery Searing Red-Orange)
            var redOrangeMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Tron/Mat_Tron_Red_Orange_Neon.mat");
            if (redOrangeMat == null)
            {
                redOrangeMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(redOrangeMat, "Assets/Materials/Tron/Mat_Tron_Red_Orange_Neon.mat");
            }
            redOrangeMat.SetColor("_BaseColor", new Color(3.5f, 0.28f, 0.04f, 1f));
            EditorUtility.SetDirty(redOrangeMat);

            // Deep Searing Red Neon Material (Pure Crimson Laser Red)
            var deepRedMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Tron/Mat_Tron_Deep_Red_Neon.mat");
            if (deepRedMat == null)
            {
                deepRedMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(deepRedMat, "Assets/Materials/Tron/Mat_Tron_Deep_Red_Neon.mat");
            }
            deepRedMat.SetColor("_BaseColor", new Color(2.8f, 0.06f, 0.02f, 1f));
            EditorUtility.SetDirty(deepRedMat);

            // Amber Circuit Highlight Material
            var amberMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Tron/Mat_Tron_Amber_Highlight.mat");
            if (amberMat == null)
            {
                amberMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(amberMat, "Assets/Materials/Tron/Mat_Tron_Amber_Highlight.mat");
            }
            amberMat.SetColor("_BaseColor", new Color(3.2f, 0.55f, 0.04f, 1f));
            EditorUtility.SetDirty(amberMat);

            // Holographic Cyan Material
            var holoCyanMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Tron/Mat_Tron_Holo_Cyan.mat");
            if (holoCyanMat == null)
            {
                holoCyanMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(holoCyanMat, "Assets/Materials/Tron/Mat_Tron_Holo_Cyan.mat");
            }
            holoCyanMat.SetColor("_BaseColor", new Color(0.2f, 2.5f, 3.2f, 1f));
            EditorUtility.SetDirty(holoCyanMat);

            // Outer Atmospheric Rim Blue Material (Signature Disney TRON Orbital Sunrise Fringe)
            var blueAtmoMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Tron/Mat_Tron_Atmosphere_Blue.mat");
            if (blueAtmoMat == null)
            {
                blueAtmoMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(blueAtmoMat, "Assets/Materials/Tron/Mat_Tron_Atmosphere_Blue.mat");
            }
            blueAtmoMat.SetColor("_BaseColor", new Color(0.2f, 1.4f, 3.8f, 1f));
            EditorUtility.SetDirty(blueAtmoMat);

            // Monolith Pillar Material
            var pillarMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Tron/Mat_Tron_Monolith_Pillar.mat");
            if (pillarMat == null)
            {
                pillarMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(pillarMat, "Assets/Materials/Tron/Mat_Tron_Monolith_Pillar.mat");
            }
            pillarMat.SetColor("_BaseColor", new Color(0.02f, 0.02f, 0.03f, 1f));
            pillarMat.SetFloat("_Smoothness", 0.85f);
            pillarMat.SetFloat("_Metallic", 0.75f);
            EditorUtility.SetDirty(pillarMat);

            AssetDatabase.SaveAssets();

            // 2. Setup Camera & Cyberspace Environment
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;

            GameObject camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            camGO.transform.position = new Vector3(0f, 3.2f, -138f);
            camGO.transform.rotation = Quaternion.Euler(0f, 0f, 0f);

            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.fieldOfView = 52f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 1400f;
            cam.allowHDR = true;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;
            RenderSettings.reflectionIntensity = 0f;

            var uad = camGO.AddComponent<UniversalAdditionalCameraData>();
            uad.renderPostProcessing = true;
            uad.volumeLayerMask = ~0;
            uad.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            uad.antialiasingQuality = AntialiasingQuality.High;

            camGO.AddComponent<AudioListener>();

            // 3. Setup 6-Sided Red Laser Grid Chamber (Scaled to authentic TRON: Ares film dimensions)
            GameObject chamberRoot = new GameObject("TRON_Red_Grid_Chamber");
            GameObject chamberGridRoot = new GameObject("Chamber_Grid_Enclosure");
            chamberGridRoot.transform.SetParent(chamberRoot.transform, false);

            // Floor Base (Liquid Obsidian Mirror Floor with CyberspacePlanarMirror)
            GameObject floorGO = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floorGO.name = "Floor_Obsidian_Base";
            floorGO.transform.SetParent(chamberGridRoot.transform);
            floorGO.transform.position = Vector3.zero;
            floorGO.transform.localScale = Vector3.one; // Standard 10m x 10m Plane
            var floorMR = floorGO.GetComponent<MeshRenderer>();
            floorMR.sharedMaterial = mirrorMat;
            Object.DestroyImmediate(floorGO.GetComponent<Collider>());

            var planarMirror = floorGO.AddComponent<SyntyCombined.CyberspacePlanarMirror>();
            planarMirror.planeHeight = 0.0f;
            planarMirror.clipPlaneOffset = 0.02f;
            planarMirror.floorRenderer = floorMR;
            planarMirror.mirrorMaterial = mirrorMat;
            planarMirror.renderInEditor = true;
            planarMirror.textureResolution = 1024;
            planarMirror.renderScale = 1.0f;
            planarMirror.reflectionLayers = ~0;

            // Ceiling (Red Grid)
            GameObject ceilingGO = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ceilingGO.name = "Ceiling_Red_Grid";
            ceilingGO.transform.SetParent(chamberGridRoot.transform);
            ceilingGO.transform.position = new Vector3(0f, 4.0f, 0f);
            ceilingGO.transform.rotation = Quaternion.Euler(180f, 0f, 0f);
            ceilingGO.transform.localScale = Vector3.one; // 10m x 10m Plane
            ceilingGO.GetComponent<MeshRenderer>().sharedMaterial = redGridMat;
            Object.DestroyImmediate(ceilingGO.GetComponent<Collider>());

            // Closed 6-Sided Chamber Walls (Scaled directly around slab for authentic intimate perspective - 10m x 10m x 4m)
            CreateWall(chamberGridRoot.transform, "Wall_North", new Vector3(0f, 2.0f, 5.0f), Quaternion.Euler(90f, 180f, 0f), new Vector3(1.0f, 1.0f, 0.4f), redGridMat);
            CreateWall(chamberGridRoot.transform, "Wall_South", new Vector3(0f, 2.0f, -5.0f), Quaternion.Euler(-90f, 0f, 0f), new Vector3(1.0f, 1.0f, 0.4f), redGridMat);
            CreateWall(chamberGridRoot.transform, "Wall_East", new Vector3(5.0f, 2.0f, 0f), Quaternion.Euler(0f, 0f, 90f), new Vector3(0.4f, 1.0f, 1.0f), redGridMat);
            CreateWall(chamberGridRoot.transform, "Wall_West", new Vector3(-5.0f, 2.0f, 0f), Quaternion.Euler(0f, 0f, -90f), new Vector3(0.4f, 1.0f, 1.0f), redGridMat);

            // Iconic Horizon Laser Beam running across the West Wall at slab height (y = 0.65m)
            GameObject horizonBeam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            horizonBeam.name = "Horizon_Laser_Beam";
            horizonBeam.transform.SetParent(chamberGridRoot.transform, false);
            horizonBeam.transform.position = new Vector3(-4.95f, 0.65f, 0f);
            horizonBeam.transform.localScale = new Vector3(0.05f, 0.04f, 10.0f);
            horizonBeam.GetComponent<MeshRenderer>().sharedMaterial = deepRedMat;
            Object.DestroyImmediate(horizonBeam.GetComponent<Collider>());

            // 5. Setup Master Eye Pupil Radial Burst (Inside Alita's Eye Pupil on Slab - Prologue Inspired)
            GameObject prologueVFXRoot = new GameObject("TRON_Eye_Pupil_VFX_Master");
            GameObject pupilBurstGO = CreateEyePupilRadialBurst(chamberRoot.transform, whiteCoreMat, redOrangeMat, deepRedMat, amberMat, holoCyanMat, pillarMat, blackSlabMat);
            pupilBurstGO.transform.position = new Vector3(0f, 0.76f, 0.68f);
            pupilBurstGO.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

            // 6. Setup Towering Circuit Monolith Corridor (0:18 - 0:45)
            CreateCircuitMonolithCorridor(chamberRoot.transform, pillarMat, redOrangeMat);

            // 4. Setup Floating Awakening Slab (Z = 0)
            GameObject slabRoot = new GameObject("Ares_Awakening_Slab");
            slabRoot.transform.position = new Vector3(0f, 0.65f, 0f);

            float tubeThick = 0.035f;
            float slabW = 1.12f;
            float slabL = 2.32f;

            // Main Slab Plinth (Floating cantilever platform)
            GameObject slabPlinth = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slabPlinth.name = "Slab_Plinth";
            slabPlinth.transform.SetParent(slabRoot.transform, false);
            slabPlinth.transform.localScale = new Vector3(1.1f, 0.14f, 2.3f);
            slabPlinth.GetComponent<MeshRenderer>().sharedMaterial = blackSlabMat;
            Object.DestroyImmediate(slabPlinth.GetComponent<Collider>());

            // Glowing Red Neon Perimeter Border Tube (TRON: Ares Red Rim)
            CreateNeonTube(slabRoot.transform, "Rim_North", new Vector3(0f, 0.06f, slabL * 0.5f), new Vector3(slabW, tubeThick, tubeThick), deepRedMat);
            CreateNeonTube(slabRoot.transform, "Rim_South", new Vector3(0f, 0.06f, -slabL * 0.5f), new Vector3(slabW, tubeThick, tubeThick), deepRedMat);
            CreateNeonTube(slabRoot.transform, "Rim_East", new Vector3(slabW * 0.5f, 0.06f, 0f), new Vector3(tubeThick, tubeThick, slabL), deepRedMat);
            CreateNeonTube(slabRoot.transform, "Rim_West", new Vector3(-slabW * 0.5f, 0.06f, 0f), new Vector3(tubeThick, tubeThick, slabL), deepRedMat);

            // Slab Perimeter Rim Light (Searing TRON Red)
            GameObject slabLightGO = new GameObject("Light_Slab_Edge");
            slabLightGO.transform.SetParent(slabRoot.transform, false);
            slabLightGO.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            var slabLgt = slabLightGO.AddComponent<Light>();
            slabLgt.type = LightType.Point;
            slabLgt.color = new Color(1.0f, 0.18f, 0.05f);
            slabLgt.intensity = 2.2f;
            slabLgt.range = 3.5f;

            // Subtle Red Underglow Light
            GameObject underGlowGO = new GameObject("Light_Red_Underglow");
            underGlowGO.transform.SetParent(slabRoot.transform, false);
            underGlowGO.transform.localPosition = new Vector3(0f, -0.45f, 0f);
            var underLgt = underGlowGO.AddComponent<Light>();
            underLgt.type = LightType.Point;
            underLgt.color = new Color(1f, 0.06f, 0.08f);
            underLgt.intensity = 1.8f;
            underLgt.range = 2.5f;

            // Dedicated Soft Face Key Light for Macro Head Shot
            GameObject faceKeyLightGO = new GameObject("Light_Face_Key");
            faceKeyLightGO.transform.SetParent(slabRoot.transform, false);
            faceKeyLightGO.transform.localPosition = new Vector3(-0.35f, 0.55f, 0.65f);
            var fKeyLgt = faceKeyLightGO.AddComponent<Light>();
            fKeyLgt.type = LightType.Point;
            fKeyLgt.color = new Color(1.0f, 0.45f, 0.18f); // Warm TRON amber
            fKeyLgt.intensity = 1.2f;
            fKeyLgt.range = 1.8f;

            // 7. Spawn Customized Alita Doll Body on Slab with Authentic TRON Ares Program Suit
            GameObject alitaGO = null;
            GameObject discGO = null;
            var alitaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AlitaPrefabPath);
            if (alitaPrefab != null)
            {
                alitaGO = Object.Instantiate(alitaPrefab);
                alitaGO.name = "Alita_DollBody";
                alitaGO.transform.position = slabRoot.transform.position + new Vector3(0f, 0.12f, -0.91f);
                alitaGO.transform.rotation = Quaternion.Euler(-90f, 180f, 0f);

                // Setup Authentic TRON Ares Program Cyber-Suit Material
                var tronSuitMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Tron/Mat_Alita_Tron_Ares.mat");
                if (tronSuitMat == null)
                {
                    var berserkerMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Prefabs/Alita_Sidekick/Materials/M_Alita_Berserker.mat");
                    if (berserkerMat != null)
                    {
                        tronSuitMat = new Material(berserkerMat);
                    }
                    else
                    {
                        tronSuitMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    }
                    AssetDatabase.CreateAsset(tronSuitMat, "Assets/Materials/Tron/Mat_Alita_Tron_Ares.mat");
                }
                tronSuitMat.SetColor("_Color", new Color(0.04f, 0.04f, 0.05f, 1f));
                tronSuitMat.SetColor("_BaseColor", new Color(0.04f, 0.04f, 0.05f, 1f));
                tronSuitMat.SetColor("_EmissionColor", new Color(16.0f, 0.65f, 0.12f, 1f));
                tronSuitMat.SetFloat("_Metallic", 0.75f);
                tronSuitMat.SetFloat("_Smoothness", 0.85f);
                tronSuitMat.EnableKeyword("_EMISSION");
                EditorUtility.SetDirty(tronSuitMat);

                var smr = alitaGO.GetComponentInChildren<SkinnedMeshRenderer>();
                if (smr != null)
                {
                    smr.sharedMaterial = tronSuitMat;
                }

                // Configure arms along ribs & mount glowing TRON Identity Disc on back
                var anim = alitaGO.GetComponent<Animator>();
                if (anim != null)
                {
                    anim.enabled = false;
                    var chestBone = anim.GetBoneTransform(HumanBodyBones.Chest) ?? anim.GetBoneTransform(HumanBodyBones.Spine);
                    discGO = CreateTronIdentityDisc(chestBone, whiteCoreMat, redOrangeMat, deepRedMat, amberMat);

                    var armL = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                    var armR = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
                    if (armL != null) armL.localEulerAngles = new Vector3(0f, 0f, 75f);
                    if (armR != null) armR.localEulerAngles = new Vector3(0f, 0f, 105f);
                }
            }

            // 8. Spawn Dr. Dyson Ido at Diagnostic Terminal Workstation
            GameObject terminalGO = null;
            IdoTerminalTypingController idoCtrl = null;

            Vector3 docPos = new Vector3(-3.0f, 0f, 1.2f);
            Vector3 slabPos = new Vector3(0f, 0.65f, 0f);
            Vector3 dirToSlab = (slabPos - docPos).normalized;
            float docRotY = Mathf.Atan2(dirToSlab.x, dirToSlab.z) * Mathf.Rad2Deg;

            var docPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DoctorPrefabPath);
            var consolePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ConsolePrefabPath);

            if (docPrefab != null && consolePrefab != null)
            {
                var docGO = Object.Instantiate(docPrefab, docPos, Quaternion.Euler(0f, docRotY, 0f));
                docGO.name = "Dr_Dyson_Ido";
                docGO.transform.SetParent(chamberRoot.transform);

                Vector3 consolePos = docPos + docGO.transform.forward * 0.48f;
                terminalGO = Object.Instantiate(consolePrefab, consolePos, Quaternion.Euler(0f, docRotY + 180f, 0f));
                terminalGO.name = "Ido_Diagnostic_Console";
                terminalGO.transform.SetParent(chamberRoot.transform);

                // Mount In-World 3D Terminal Screen Asset (No 2D HUD!)
                var termScreen = terminalGO.AddComponent<TronTerminalScreenAsset>();
                termScreen.SetupScreenCanvas();

                // Screen Glow Light illuminating Dr. Ido's hands and face
                GameObject screenLightGO = new GameObject("Light_Terminal_Screen");
                screenLightGO.transform.SetParent(terminalGO.transform, false);
                screenLightGO.transform.localPosition = new Vector3(0f, 1.15f, 0.05f);
                var scrLgt = screenLightGO.AddComponent<Light>();
                scrLgt.type = LightType.Point;
                scrLgt.color = new Color(0.2f, 0.85f, 1.0f);
                scrLgt.intensity = 2.2f;
                scrLgt.range = 2.5f;

                // Cinematic Rim Light on Dr. Ido from chamber
                GameObject docRimLightGO = new GameObject("Light_Doctor_Rim");
                docRimLightGO.transform.SetParent(docGO.transform, false);
                docRimLightGO.transform.localPosition = new Vector3(-1.2f, 2.0f, -0.6f);
                var rimLgt = docRimLightGO.AddComponent<Light>();
                rimLgt.type = LightType.Point;
                rimLgt.color = new Color(1.0f, 0.45f, 0.18f); // amber rim
                rimLgt.intensity = 2.2f;
                rimLgt.range = 4.0f;

                idoCtrl = docGO.AddComponent<IdoTerminalTypingController>();
                idoCtrl.terminalConsole = terminalGO.transform;
                idoCtrl.terminalScreenLight = scrLgt;
                idoCtrl.doctorRoot = docGO.transform;
                idoCtrl.CacheBones();
            }

            // 9. Overhead Spotlight
            GameObject scanLightGO = new GameObject("Light_Overhead_Scan");
            scanLightGO.transform.position = new Vector3(0f, 2.8f, 0.2f);
            scanLightGO.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var scanLight = scanLightGO.AddComponent<Light>();
            scanLight.type = LightType.Spot;
            scanLight.color = new Color(1f, 0.92f, 0.75f);
            scanLight.intensity = 1.0f;
            scanLight.range = 6f;
            scanLight.spotAngle = 65f;

            // 10. Setup Audio Sources
            GameObject audioMgrGO = new GameObject("Awakening_Audio_Manager");
            var musicSrc = audioMgrGO.AddComponent<AudioSource>();
            musicSrc.playOnAwake = false;
            musicSrc.loop = true;
            musicSrc.volume = 0.85f;
            var musicClip = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicAudioPath);
            if (musicClip != null) musicSrc.clip = musicClip;

            var fxSrc = audioMgrGO.AddComponent<AudioSource>();
            fxSrc.playOnAwake = false;
            fxSrc.volume = 1f;

            // 11. Setup Post-Processing (Calibrated ACES Tonemapping and Clamped Bloom)
            GameObject ppGO = new GameObject("Awakening_PostProcess_Volume");
            var vol = ppGO.AddComponent<Volume>();
            vol.isGlobal = true;
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostProcessProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, PostProcessProfilePath);
            }
            while (profile.components.Count > 0) profile.Remove(profile.components[0].GetType());
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.value = 1.05f;
            bloom.intensity.value = 1.35f;
            bloom.scatter.value = 0.62f;
            bloom.clamp.value = 10.0f;
            bloom.tint.value = new Color(1f, 0.96f, 0.90f);
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.value = TonemappingMode.ACES;
            var colorAdj = profile.Add<ColorAdjustments>(true);
            colorAdj.contrast.value = 12f;
            EditorUtility.SetDirty(profile);
            vol.sharedProfile = profile;

            // 12. Master Director Component
            GameObject directorGO = new GameObject("Alita_TRON_Awakening_Director");
            var director = directorGO.AddComponent<AlitaTronAwakeningDirector>();
            director.mainCamera = cam;
            director.alitaActor = alitaGO;
            director.chamberGridRoot = chamberGridRoot;
            director.identityDisc = discGO != null ? discGO.GetComponent<TronIdentityDisc>() : null;
            director.slabTransform = slabRoot.transform;
            director.eyePupilVFXTransform = pupilBurstGO.transform;
            director.eyePupilVFX = pupilBurstGO.GetComponent<TronEyePupilRadialBurstVFX>();
            director.terminalTransform = terminalGO != null ? terminalGO.transform : null;
            director.terminalScreen = terminalGO != null ? terminalGO.GetComponent<TronTerminalScreenAsset>() : null;
            director.idoController = idoCtrl;
            director.slabEdgeLight = slabLgt;
            director.faceKeyLight = fKeyLgt;
            director.overheadScanLight = scanLight;
            director.musicSource = musicSrc;
            director.fxSource = fxSrc;

            // Setup Master TRON Orbital Eye Sequence (32s - 64s)
            GameObject orbitalMasterRoot = new GameObject("TRON_Orbital_Master");
            GameObject orbitalEyeVFXGO = CreateOrbitalEyeSequence(orbitalMasterRoot.transform, redGridMat, mirrorMat, blackSlabMat, whiteCoreMat, redOrangeMat, deepRedMat, amberMat, blueAtmoMat, pillarMat);
            director.orbitalEyeVFX = orbitalEyeVFXGO.GetComponent<TronOrbitalEyeSequenceVFX>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[AlitaTronAwakening] Successfully generated full 120s scene {ScenePath}!");
        }

        private static GameObject CreateEyePupilRadialBurst(Transform parent, Material whiteCoreMat, Material redOrangeMat, Material deepRedMat, Material amberMat, Material holoCyanMat, Material pillarMat, Material blackSlabMat)
        {
            GameObject burstRoot = new GameObject("Tron_Eye_Pupil_Radial_Burst");
            burstRoot.transform.SetParent(parent, false);
            burstRoot.transform.localPosition = new Vector3(0f, 0.23f, 0.65f);

            var vfx = burstRoot.AddComponent<TronEyePupilRadialBurstVFX>();

            // =========================================================================
            // 1. STARBURST & RETINAL MANDALA ROOT (10.2s - 23.5s) - DISNEY PRESENTS
            // =========================================================================
            GameObject starburstRoot = new GameObject("Radial_Starburst_Mandala");
            starburstRoot.transform.SetParent(burstRoot.transform, false);
            starburstRoot.transform.localPosition = Vector3.zero;
            vfx.starburstRoot = starburstRoot.transform;

            // 1A. Incandescent Core Nucleus Sphere
            GameObject nucleus = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            nucleus.name = "Core_Nucleus_Sphere";
            nucleus.transform.SetParent(starburstRoot.transform, false);
            nucleus.transform.localPosition = Vector3.zero;
            nucleus.transform.localScale = new Vector3(0.035f, 0.035f, 0.035f);
            nucleus.GetComponent<MeshRenderer>().sharedMaterial = whiteCoreMat;
            Object.DestroyImmediate(nucleus.GetComponent<Collider>());
            vfx.coreNucleus = nucleus.transform;

            // 1B. Prologue Ares Core Identity Nested Rings (Interlocking gimbal aperture rings)
            GameObject aresCoreRoot = new GameObject("Ares_Core_Identity_Nested_Rings");
            aresCoreRoot.transform.SetParent(starburstRoot.transform, false);
            vfx.aresCoreIdentityRoot = aresCoreRoot.transform;

            float[] aresRingRadii = new float[] { 0.22f, 0.32f, 0.44f };
            for (int r = 0; r < aresRingRadii.Length; r++)
            {
                GameObject gRing = new GameObject($"Gimbal_Ring_{r}");
                gRing.transform.SetParent(aresCoreRoot.transform, false);
                gRing.transform.localRotation = Quaternion.Euler(r * 35f, r * 55f, r * 20f);

                int ticks = 24 + r * 8;
                float rRadius = aresRingRadii[r];
                for (int t = 0; t < ticks; t++)
                {
                    float angle = (t / (float)ticks) * Mathf.PI * 2f;
                    Vector3 tickPos = new Vector3(Mathf.Cos(angle) * rRadius, 0f, Mathf.Sin(angle) * rRadius);

                    GameObject tick = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    tick.name = $"AresCoreSegment_{t}";
                    tick.transform.SetParent(gRing.transform, false);
                    tick.transform.localPosition = tickPos;
                    tick.transform.localRotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f);
                    tick.transform.localScale = new Vector3(0.015f, 0.0015f, 0.0015f);
                    tick.GetComponent<MeshRenderer>().sharedMaterial = (r == 0) ? whiteCoreMat : (r == 1 ? amberMat : redOrangeMat);
                    Object.DestroyImmediate(tick.GetComponent<Collider>());
                }
            }

            // 1C. Far-field background data cubes
            GameObject dataCubesRoot = new GameObject("Prologue_Modular_Data_Cubes");
            dataCubesRoot.transform.SetParent(starburstRoot.transform, false);
            vfx.dataCubesRoot = dataCubesRoot.transform;

            int cubeCount = 64;
            for (int c = 0; c < cubeCount; c++)
            {
                float phi = Mathf.PI * (3.0f - Mathf.Sqrt(5.0f));
                float y = 1f - (c / (float)(cubeCount - 1)) * 2f;
                float radAtY = Mathf.Sqrt(1f - y * y);
                float theta = phi * c;
                Vector3 cDir = new Vector3(Mathf.Cos(theta) * radAtY, y, Mathf.Sin(theta) * radAtY).normalized;
                float dist = Random.Range(4.5f, 14.0f);
                float cubeScale = Random.Range(0.006f, 0.016f);

                GameObject dataCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                dataCube.name = $"DataCube_{c}";
                dataCube.transform.SetParent(dataCubesRoot.transform, false);
                dataCube.transform.localPosition = cDir * dist;
                dataCube.transform.localRotation = Random.rotation;
                dataCube.transform.localScale = Vector3.one * cubeScale;
                dataCube.GetComponent<MeshRenderer>().sharedMaterial = (c % 4 == 0) ? holoCyanMat : ((c % 2 == 0) ? amberMat : redOrangeMat);
                Object.DestroyImmediate(dataCube.GetComponent<Collider>());
            }

            // 1D. 360 Fibonacci Spherical Laser Rays
            GameObject radialRaysRoot = new GameObject("Radial_360_Circuit_Rays");
            radialRaysRoot.transform.SetParent(starburstRoot.transform, false);
            vfx.radialRaysRoot = radialRaysRoot.transform;

            int rayCount = 360;
            float phiRay = Mathf.PI * (3.0f - Mathf.Sqrt(5.0f));
            for (int i = 0; i < rayCount; i++)
            {
                float y = 1f - (i / (float)(rayCount - 1)) * 2f;
                float radiusAtY = Mathf.Sqrt(1f - y * y);
                float theta = phiRay * i;
                float x = Mathf.Cos(theta) * radiusAtY;
                float z = Mathf.Sin(theta) * radiusAtY;
                Vector3 dir = new Vector3(x, y, z).normalized;

                float rayLen = Random.Range(35.0f, 95.0f);
                float thickness = Random.Range(0.0005f, 0.0010f);

                GameObject ray = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ray.name = $"RadialRay_{i}";
                ray.transform.SetParent(radialRaysRoot.transform, false);
                ray.transform.localPosition = dir * (0.05f + rayLen * 0.5f);
                ray.transform.localRotation = Quaternion.LookRotation(dir);
                ray.transform.localScale = new Vector3(thickness, thickness, rayLen);
                ray.GetComponent<MeshRenderer>().sharedMaterial = (i % 3 == 0) ? amberMat : ((i % 2 == 0) ? deepRedMat : redOrangeMat);
                Object.DestroyImmediate(ray.GetComponent<Collider>());
            }

            // 1E. Retinal Iris Aperture Rings (Concentric ripple mandala)
            GameObject retinalRingsRoot = new GameObject("Retinal_Iris_Rings");
            retinalRingsRoot.transform.SetParent(starburstRoot.transform, false);
            vfx.retinalRingsRoot = retinalRingsRoot.transform;

            float[] ringRadii = new float[] { 0.26f, 0.45f, 0.70f, 1.05f, 1.45f };
            int[] ringSegments = new int[] { 32, 44, 56, 68, 80 };

            for (int r = 0; r < ringRadii.Length; r++)
            {
                float ringR = ringRadii[r];
                int segs = ringSegments[r];
                GameObject ringGO = new GameObject($"RetinalRing_{r}");
                ringGO.transform.SetParent(retinalRingsRoot.transform, false);

                for (int s = 0; s < segs; s++)
                {
                    float angle = (s / (float)segs) * Mathf.PI * 2f;
                    Vector3 pos = new Vector3(Mathf.Cos(angle) * ringR, 0f, Mathf.Sin(angle) * ringR);

                    GameObject tick = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    tick.name = $"Tick_{s}";
                    tick.transform.SetParent(ringGO.transform, false);
                    tick.transform.localPosition = pos;
                    tick.transform.localRotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f);
                    tick.transform.localScale = new Vector3(0.015f, 0.0015f, 0.0015f);
                    tick.GetComponent<MeshRenderer>().sharedMaterial = (s % 4 == 0) ? amberMat : redOrangeMat;
                    Object.DestroyImmediate(tick.GetComponent<Collider>());
                }
            }

            // 1F. Volumetric Core Light
            GameObject lightGO = new GameObject("Volumetric_Pupil_Core_Light");
            lightGO.transform.SetParent(starburstRoot.transform, false);
            var l = lightGO.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1.0f, 0.38f, 0.10f);
            l.intensity = 1.6f;
            l.range = 10.0f;
            vfx.coreVolumetricLight = l;

            // =========================================================================
            // 2. VERTICAL CONDUIT SHAFT ROOT (23.5s - 27.0s) - SEAN BAILEY PRODUCTION
            // =========================================================================
            GameObject verticalShaftRoot = new GameObject("Vertical_Conduit_Shaft");
            verticalShaftRoot.transform.SetParent(burstRoot.transform, false);
            verticalShaftRoot.transform.localPosition = new Vector3(0f, -8f, -40f);
            vfx.verticalShaftRoot = verticalShaftRoot.transform;

            GameObject shaftPillarsRoot = new GameObject("Shaft_Pillars");
            shaftPillarsRoot.transform.SetParent(verticalShaftRoot.transform, false);
            vfx.shaftPillarsRoot = shaftPillarsRoot.transform;

            int sPillars = 48;
            float sRadius = 6.2f;
            float sDepth = 42f;

            for (int sp = 0; sp < sPillars; sp++)
            {
                float ang = (sp / (float)sPillars) * Mathf.PI * 2f;
                Vector3 pPos = new Vector3(Mathf.Cos(ang) * sRadius, -sDepth * 0.5f, Mathf.Sin(ang) * sRadius);

                var sPillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                sPillar.name = $"ShaftPillar_{sp}";
                sPillar.transform.SetParent(shaftPillarsRoot.transform, false);
                sPillar.transform.localPosition = pPos;
                sPillar.transform.localRotation = Quaternion.Euler(0f, -ang * Mathf.Rad2Deg, 0f);
                sPillar.transform.localScale = new Vector3(0.72f, sDepth, 0.85f);
                sPillar.GetComponent<MeshRenderer>().sharedMaterial = pillarMat;
                Object.DestroyImmediate(sPillar.GetComponent<Collider>());

                var sTrace = GameObject.CreatePrimitive(PrimitiveType.Cube);
                sTrace.name = "Neon_Trace";
                sTrace.transform.SetParent(sPillar.transform, false);
                sTrace.transform.localPosition = new Vector3(0f, 0f, -0.44f);
                sTrace.transform.localScale = new Vector3(0.035f, 1f, 0.035f);
                sTrace.GetComponent<MeshRenderer>().sharedMaterial = (sp % 2 == 0) ? redOrangeMat : amberMat;
                Object.DestroyImmediate(sTrace.GetComponent<Collider>());
            }

            float[] sRingsY = new float[] { -4f, -12f, -20f, -28f, -35f, -40f };
            for (int sr = 0; sr < sRingsY.Length; sr++)
            {
                int rSegs = 36;
                for (int rs = 0; rs < rSegs; rs++)
                {
                    float ang = (rs / (float)rSegs) * Mathf.PI * 2f;
                    Vector3 rPos = new Vector3(Mathf.Cos(ang) * (sRadius - 0.38f), sRingsY[sr], Mathf.Sin(ang) * (sRadius - 0.38f));
                    var rSeg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    rSeg.name = $"ShaftRingSeg_{sr}_{rs}";
                    rSeg.transform.SetParent(shaftPillarsRoot.transform, false);
                    rSeg.transform.localPosition = rPos;
                    rSeg.transform.localRotation = Quaternion.Euler(0f, -ang * Mathf.Rad2Deg, 0f);
                    rSeg.transform.localScale = new Vector3(1.1f, 0.05f, 0.06f);
                    rSeg.GetComponent<MeshRenderer>().sharedMaterial = amberMat;
                    Object.DestroyImmediate(rSeg.GetComponent<Collider>());
                }
            }

            var shaftLightGO = new GameObject("Light_Shaft_Plunge");
            shaftLightGO.transform.SetParent(verticalShaftRoot.transform, false);
            shaftLightGO.transform.localPosition = new Vector3(0f, -20f, 0f);
            var sLight = shaftLightGO.AddComponent<Light>();
            sLight.type = LightType.Point;
            sLight.color = new Color(1.0f, 0.28f, 0.08f);
            sLight.intensity = 7.0f;
            sLight.range = 45f;
            vfx.shaftLight = sLight;
            verticalShaftRoot.SetActive(false);

            // =========================================================================
            // 3. CHEVRON PORTAL CORRIDOR ROOT (27.0s - 32.0s) - A FILM BY JOACHIM RØNNING
            // =========================================================================
            GameObject chevronCorridorRoot = new GameObject("Chevron_Portal_Corridor");
            chevronCorridorRoot.transform.SetParent(burstRoot.transform, false);
            chevronCorridorRoot.transform.localPosition = new Vector3(0f, -46f, -10f);
            vfx.chevronCorridorRoot = chevronCorridorRoot.transform;

            GameObject chevronArchesRoot = new GameObject("Chevron_Arches");
            chevronArchesRoot.transform.SetParent(chevronCorridorRoot.transform, false);
            vfx.chevronArchesRoot = chevronArchesRoot.transform;

            float cLen = 90f;
            var cFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cFloor.name = "Corridor_Floor";
            cFloor.transform.SetParent(chevronCorridorRoot.transform, false);
            cFloor.transform.localPosition = new Vector3(0f, 0f, 0f);
            cFloor.transform.localScale = new Vector3(9.5f, 0.2f, cLen);
            cFloor.GetComponent<MeshRenderer>().sharedMaterial = blackSlabMat;
            Object.DestroyImmediate(cFloor.GetComponent<Collider>());

            float[] floorTracksX = new float[] { -3.6f, -1.2f, 1.2f, 3.6f };
            for (int tx = 0; tx < floorTracksX.Length; tx++)
            {
                var track = GameObject.CreatePrimitive(PrimitiveType.Cube);
                track.name = $"FloorTrack_{tx}";
                track.transform.SetParent(chevronCorridorRoot.transform, false);
                track.transform.localPosition = new Vector3(floorTracksX[tx], 0.12f, 0f);
                track.transform.localScale = new Vector3(0.06f, 0.04f, cLen);
                track.GetComponent<MeshRenderer>().sharedMaterial = (tx == 1 || tx == 2) ? amberMat : redOrangeMat;
                Object.DestroyImmediate(track.GetComponent<Collider>());
            }

            int chevronGates = 16;
            float zStartGate = -40f;
            float zStepGate = 5.2f;

            for (int g = 0; g < chevronGates; g++)
            {
                float z = zStartGate + g * zStepGate;
                GameObject gateGO = new GameObject($"ChevronGate_{g}");
                gateGO.transform.SetParent(chevronArchesRoot.transform, false);
                gateGO.transform.localPosition = new Vector3(0f, 0f, z);

                Vector3 leftStart = new Vector3(-4.4f, 0.2f, 0f);
                Vector3 leftPeak = new Vector3(0f, 4.8f, 0f);
                Vector3 leftMid = (leftStart + leftPeak) * 0.5f;
                float leftLen = Vector3.Distance(leftStart, leftPeak);

                var leftBeam = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leftBeam.name = "Chevron_Left_Beam";
                leftBeam.transform.SetParent(gateGO.transform, false);
                leftBeam.transform.localPosition = leftMid;
                leftBeam.transform.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Atan2(4.6f, 4.4f) * Mathf.Rad2Deg);
                leftBeam.transform.localScale = new Vector3(0.18f, leftLen, 0.35f);
                leftBeam.GetComponent<MeshRenderer>().sharedMaterial = (g % 2 == 0) ? amberMat : redOrangeMat;
                Object.DestroyImmediate(leftBeam.GetComponent<Collider>());

                Vector3 rightStart = new Vector3(4.4f, 0.2f, 0f);
                Vector3 rightPeak = new Vector3(0f, 4.8f, 0f);
                Vector3 rightMid = (rightStart + rightPeak) * 0.5f;
                float rightLen = Vector3.Distance(rightStart, rightPeak);

                var rightBeam = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rightBeam.name = "Chevron_Right_Beam";
                rightBeam.transform.SetParent(gateGO.transform, false);
                rightBeam.transform.localPosition = rightMid;
                rightBeam.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(4.6f, 4.4f) * Mathf.Rad2Deg);
                rightBeam.transform.localScale = new Vector3(0.18f, rightLen, 0.35f);
                rightBeam.GetComponent<MeshRenderer>().sharedMaterial = (g % 2 == 0) ? amberMat : redOrangeMat;
                Object.DestroyImmediate(rightBeam.GetComponent<Collider>());

                var lPillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lPillar.name = "Wall_Pillar_L";
                lPillar.transform.SetParent(gateGO.transform, false);
                lPillar.transform.localPosition = new Vector3(-4.6f, 2.5f, 0f);
                lPillar.transform.localScale = new Vector3(0.6f, 5.0f, 0.6f);
                lPillar.GetComponent<MeshRenderer>().sharedMaterial = pillarMat;
                Object.DestroyImmediate(lPillar.GetComponent<Collider>());

                var rPillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rPillar.name = "Wall_Pillar_R";
                rPillar.transform.SetParent(gateGO.transform, false);
                rPillar.transform.localPosition = new Vector3(4.6f, 2.5f, 0f);
                rPillar.transform.localScale = new Vector3(0.6f, 5.0f, 0.6f);
                rPillar.GetComponent<MeshRenderer>().sharedMaterial = pillarMat;
                Object.DestroyImmediate(rPillar.GetComponent<Collider>());
            }

            var corrLightGO = new GameObject("Light_Chevron_Corridor");
            corrLightGO.transform.SetParent(chevronCorridorRoot.transform, false);
            corrLightGO.transform.localPosition = new Vector3(0f, 3.2f, 0f);
            var cLight = corrLightGO.AddComponent<Light>();
            cLight.type = LightType.Point;
            cLight.color = new Color(1.0f, 0.42f, 0.12f);
            cLight.intensity = 8.5f;
            cLight.range = 55f;
            vfx.corridorLight = cLight;
            chevronCorridorRoot.SetActive(false);

            burstRoot.SetActive(false);
            return burstRoot;
        }

        private static void CreateCircuitMonolithCorridor(Transform parent, Material pillarMat, Material orangeNeonMat)
        {
            GameObject corridorRoot = new GameObject("TRON_Circuit_Monolith_Corridor");
            corridorRoot.transform.SetParent(parent);

            int pillarCount = 16;
            float zStart = -112f;
            float zEnd = -18f;
            float zStep = (zEnd - zStart) / (pillarCount - 1);

            for (int i = 0; i < pillarCount; i++)
            {
                float z = zStart + i * zStep;

                // Left Pillar
                CreatePillar(corridorRoot.transform, $"Pillar_L_{i}", new Vector3(-4.5f, 7f, z), pillarMat, orangeNeonMat, true);

                // Right Pillar
                CreatePillar(corridorRoot.transform, $"Pillar_R_{i}", new Vector3(4.5f, 7f, z), pillarMat, orangeNeonMat, false);

                // Overhead Data Conduit Bridge
                if (i % 2 == 0)
                {
                    GameObject bridge = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    bridge.name = $"Conduit_Bridge_{i}";
                    bridge.transform.SetParent(corridorRoot.transform);
                    bridge.transform.position = new Vector3(0f, 11.2f, z);
                    bridge.transform.localScale = new Vector3(9.2f, 0.18f, 0.45f);
                    bridge.GetComponent<MeshRenderer>().sharedMaterial = pillarMat;
                    Object.DestroyImmediate(bridge.GetComponent<Collider>());

                    // Neon stripe on bridge
                    GameObject stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    stripe.name = "Neon_Stripe";
                    stripe.transform.SetParent(bridge.transform, false);
                    stripe.transform.localPosition = new Vector3(0f, -0.6f, 0f);
                    stripe.transform.localScale = new Vector3(1.0f, 0.2f, 0.3f);
                    stripe.GetComponent<MeshRenderer>().sharedMaterial = orangeNeonMat;
                    Object.DestroyImmediate(stripe.GetComponent<Collider>());
                }
            }
        }

        private static void CreatePillar(Transform parent, string name, Vector3 pos, Material pillarMat, Material neonMat, bool isLeft)
        {
            GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pillar.name = name;
            pillar.transform.SetParent(parent);
            pillar.transform.position = pos;
            pillar.transform.localScale = new Vector3(1.4f, 15f, 1.8f);
            pillar.GetComponent<MeshRenderer>().sharedMaterial = pillarMat;
            Object.DestroyImmediate(pillar.GetComponent<Collider>());

            // Inward-facing glowing neon data conduit
            GameObject conduit = GameObject.CreatePrimitive(PrimitiveType.Cube);
            conduit.name = "Neon_Conduit";
            conduit.transform.SetParent(pillar.transform, false);
            conduit.transform.localPosition = new Vector3(isLeft ? 0.52f : -0.52f, 0f, 0f);
            conduit.transform.localScale = new Vector3(0.06f, 1.0f, 0.08f);
            conduit.GetComponent<MeshRenderer>().sharedMaterial = neonMat;
            Object.DestroyImmediate(conduit.GetComponent<Collider>());
        }

        private static void CreateWall(Transform parent, string name, Vector3 pos, Quaternion rot, Vector3 scale, Material mat)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Plane);
            wall.name = name;
            wall.transform.SetParent(parent);
            wall.transform.position = pos;
            wall.transform.rotation = rot;
            wall.transform.localScale = scale;
            wall.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Object.DestroyImmediate(wall.GetComponent<Collider>());
        }

        private static void CreateNeonTube(Transform parent, string name, Vector3 localPos, Vector3 localScale, Material mat)
        {
            GameObject tube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tube.name = name;
            tube.transform.SetParent(parent, false);
            tube.transform.localPosition = localPos;
            tube.transform.localScale = localScale;
            tube.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Object.DestroyImmediate(tube.GetComponent<Collider>());
        }

        private static GameObject CreateTronIdentityDisc(Transform chestBone, Material whiteCoreMat, Material redOrangeMat, Material deepRedMat, Material amberMat)
        {
            if (chestBone == null) return null;

            GameObject discGO = new GameObject("TRON_Identity_Disc");
            discGO.transform.SetParent(chestBone, false);
            discGO.transform.localPosition = new Vector3(0f, 0.04f, -0.12f);
            discGO.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            discGO.transform.localScale = Vector3.one * 0.45f;

            // Outer Ring
            GameObject outer = new GameObject("Outer_Ring");
            outer.transform.SetParent(discGO.transform, false);
            int segs = 32;
            float rOuter = 0.32f;
            for (int i = 0; i < segs; i++)
            {
                float ang = (i / (float)segs) * Mathf.PI * 2f;
                var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                seg.name = $"OuterSeg_{i}";
                seg.transform.SetParent(outer.transform, false);
                seg.transform.localPosition = new Vector3(Mathf.Cos(ang) * rOuter, Mathf.Sin(ang) * rOuter, 0f);
                seg.transform.localRotation = Quaternion.Euler(0f, 0f, ang * Mathf.Rad2Deg);
                seg.transform.localScale = new Vector3(0.04f, 0.015f, 0.012f);
                seg.GetComponent<MeshRenderer>().sharedMaterial = redOrangeMat;
                Object.DestroyImmediate(seg.GetComponent<Collider>());
            }

            // Mid Ring
            GameObject mid = new GameObject("Mid_Ring");
            mid.transform.SetParent(discGO.transform, false);
            float rMid = 0.22f;
            for (int i = 0; i < 24; i++)
            {
                float ang = (i / 24f) * Mathf.PI * 2f;
                var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                seg.name = $"MidSeg_{i}";
                seg.transform.SetParent(mid.transform, false);
                seg.transform.localPosition = new Vector3(Mathf.Cos(ang) * rMid, Mathf.Sin(ang) * rMid, 0f);
                seg.transform.localRotation = Quaternion.Euler(0f, 0f, ang * Mathf.Rad2Deg);
                seg.transform.localScale = new Vector3(0.035f, 0.012f, 0.012f);
                seg.GetComponent<MeshRenderer>().sharedMaterial = deepRedMat;
                Object.DestroyImmediate(seg.GetComponent<Collider>());
            }

            // Inner Core Ring
            GameObject inner = new GameObject("Inner_Ring");
            inner.transform.SetParent(discGO.transform, false);
            float rIn = 0.12f;
            for (int i = 0; i < 16; i++)
            {
                float ang = (i / 16f) * Mathf.PI * 2f;
                var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                seg.name = $"InnerSeg_{i}";
                seg.transform.SetParent(inner.transform, false);
                seg.transform.localPosition = new Vector3(Mathf.Cos(ang) * rIn, Mathf.Sin(ang) * rIn, 0f);
                seg.transform.localRotation = Quaternion.Euler(0f, 0f, ang * Mathf.Rad2Deg);
                seg.transform.localScale = new Vector3(0.03f, 0.010f, 0.010f);
                seg.GetComponent<MeshRenderer>().sharedMaterial = whiteCoreMat;
                Object.DestroyImmediate(seg.GetComponent<Collider>());
            }

            // Core Light
            var lightGO = new GameObject("Disc_Light");
            lightGO.transform.SetParent(discGO.transform, false);
            var l = lightGO.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.2f, 0.05f);
            l.intensity = 2.5f;
            l.range = 1.2f;

            var tronDisc = discGO.AddComponent<TronIdentityDisc>();
            tronDisc.outerRing = outer.transform;
            tronDisc.midRing = mid.transform;
            tronDisc.innerRing = inner.transform;
            tronDisc.coreLight = l;

            return discGO;
        }

        private static GameObject CreateOrbitalEyeSequence(Transform parent, Material redGridMat, Material mirrorMat, Material blackSlabMat, Material whiteCoreMat, Material redOrangeMat, Material deepRedMat, Material amberMat, Material blueAtmoMat, Material pillarMat)
        {
            GameObject vfxGO = new GameObject("TRON_Orbital_Cyber_Eye_Sequence_VFX");
            vfxGO.transform.SetParent(parent, false);
            var vfx = vfxGO.AddComponent<TronOrbitalEyeSequenceVFX>();

            // =========================================================================
            // 1. Subterranean Abyss Pit (32.0s - 34.5s) - JARED LETO
            // Vast cylindrical canyon chasm lined with dense monolithic circuit walls and distant dawn flare
            // =========================================================================
            GameObject pitRoot = new GameObject("Subterranean_Abyss_Pit");
            pitRoot.transform.SetParent(vfxGO.transform, false);
            pitRoot.transform.position = new Vector3(0f, 0f, 0f);

            float pitRadius = 27f;
            float pitDepth = 65f;
            int pitRimSegs = 64;

            // Circular rim at floor level
            for (int i = 0; i < pitRimSegs; i++)
            {
                float ang = (i / (float)pitRimSegs) * Mathf.PI * 2f;
                Vector3 pos = new Vector3(Mathf.Cos(ang) * pitRadius, 0.65f, Mathf.Sin(ang) * pitRadius);
                var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                seg.name = $"PitRim_{i}";
                seg.transform.SetParent(pitRoot.transform, false);
                seg.transform.localPosition = pos;
                seg.transform.localRotation = Quaternion.Euler(0f, -ang * Mathf.Rad2Deg, 0f);
                seg.transform.localScale = new Vector3(2.8f, 0.12f, 0.35f);
                seg.GetComponent<MeshRenderer>().sharedMaterial = (i % 2 == 0) ? redOrangeMat : amberMat;
                Object.DestroyImmediate(seg.GetComponent<Collider>());
            }

            // Continuous ring of 96 monolithic curved wall pillars forming a solid, dense canyon wall
            int pitPillars = 96;
            for (int p = 0; p < pitPillars; p++)
            {
                float ang = (p / (float)pitPillars) * Mathf.PI * 2f;
                Vector3 pPos = new Vector3(Mathf.Cos(ang) * pitRadius, 0.65f - pitDepth * 0.5f, Mathf.Sin(ang) * pitRadius);
                var col = GameObject.CreatePrimitive(PrimitiveType.Cube);
                col.name = $"PitPillar_{p}";
                col.transform.SetParent(pitRoot.transform, false);
                col.transform.localPosition = pPos;
                col.transform.localRotation = Quaternion.Euler(0f, -ang * Mathf.Rad2Deg, 0f);
                col.transform.localScale = new Vector3(2.0f, pitDepth, 2.2f);
                col.GetComponent<MeshRenderer>().sharedMaterial = pillarMat;
                Object.DestroyImmediate(col.GetComponent<Collider>());

                // 4 Fine Neon Data Traces per pillar (delicate vertical circuit tracks)
                float[] vOffsets = new float[] { -0.65f, -0.22f, 0.22f, 0.65f };
                for (int vo = 0; vo < 4; vo++)
                {
                    var neonTrace = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    neonTrace.name = $"Neon_Trace_{vo}";
                    neonTrace.transform.SetParent(col.transform, false);
                    neonTrace.transform.localPosition = new Vector3(vOffsets[vo], 0f, -1.12f);
                    neonTrace.transform.localScale = new Vector3(0.025f, 1f, 0.02f);
                    neonTrace.GetComponent<MeshRenderer>().sharedMaterial = (vo == 1 || vo == 2) ? amberMat : ((p % 2 == 0) ? redOrangeMat : deepRedMat);
                    Object.DestroyImmediate(neonTrace.GetComponent<Collider>());
                }

                // 5 Horizontal cross-circuit bands at depth tiers
                float[] hTiers = new float[] { -5f, -15f, -28f, -42f, -55f };
                for (int ht = 0; ht < hTiers.Length; ht++)
                {
                    var hBand = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    hBand.name = $"HBand_{ht}";
                    hBand.transform.SetParent(col.transform, false);
                    hBand.transform.localPosition = new Vector3(0f, hTiers[ht] / pitDepth, -1.11f);
                    hBand.transform.localScale = new Vector3(1.95f, 0.006f, 0.02f);
                    hBand.GetComponent<MeshRenderer>().sharedMaterial = (ht % 2 == 0) ? redOrangeMat : amberMat;
                    Object.DestroyImmediate(hBand.GetComponent<Collider>());
                }
            }

            // Radiating red grid circuit lines on the floor surrounding the pit lip
            int floorTraces = 48;
            for (int ft = 0; ft < floorTraces; ft++)
            {
                float ang = (ft / (float)floorTraces) * Mathf.PI * 2f;
                Vector3 fDir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                var fTrace = GameObject.CreatePrimitive(PrimitiveType.Cube);
                fTrace.name = $"FloorTrace_{ft}";
                fTrace.transform.SetParent(pitRoot.transform, false);
                fTrace.transform.localPosition = fDir * (pitRadius + 18f) + new Vector3(0f, 0.64f, 0f);
                fTrace.transform.localRotation = Quaternion.Euler(0f, -ang * Mathf.Rad2Deg, 0f);
                fTrace.transform.localScale = new Vector3(0.12f, 0.015f, 36f);
                fTrace.GetComponent<MeshRenderer>().sharedMaterial = (ft % 2 == 0) ? redOrangeMat : deepRedMat;
                Object.DestroyImmediate(fTrace.GetComponent<Collider>());
            }

            // Blazing Dawn Solar Flare on the canyon rim crest (matching frame 0066-0068: distant anamorphic streak across upper rim)
            var dawnFlareRoot = new GameObject("Pit_Dawn_Solar_Flare_Crest");
            dawnFlareRoot.transform.SetParent(pitRoot.transform, false);
            dawnFlareRoot.transform.localPosition = new Vector3(0.0f, 17.5f, 31.0f);

            var dawnFlareBeam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dawnFlareBeam.name = "Dawn_Anamorphic_Flare_Beam";
            dawnFlareBeam.transform.SetParent(dawnFlareRoot.transform, false);
            dawnFlareBeam.transform.localPosition = new Vector3(0f, 0f, 0f);
            dawnFlareBeam.transform.localScale = new Vector3(110.0f, 0.45f, 0.05f);
            dawnFlareBeam.GetComponent<MeshRenderer>().sharedMaterial = redOrangeMat;
            Object.DestroyImmediate(dawnFlareBeam.GetComponent<Collider>());

            var dawnRimRibbon = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dawnRimRibbon.name = "Dawn_Rim_Glow_Ribbon";
            dawnRimRibbon.transform.SetParent(dawnFlareRoot.transform, false);
            dawnRimRibbon.transform.localPosition = new Vector3(0f, -0.2f, -0.1f);
            dawnRimRibbon.transform.localScale = new Vector3(45.0f, 0.25f, 0.05f);
            dawnRimRibbon.GetComponent<MeshRenderer>().sharedMaterial = amberMat;
            Object.DestroyImmediate(dawnRimRibbon.GetComponent<Collider>());

            var dawnLightGO = new GameObject("Light_Pit_Dawn");
            dawnLightGO.transform.SetParent(dawnFlareRoot.transform, false);
            dawnLightGO.transform.localPosition = new Vector3(0f, -1.0f, -4.0f);
            var dawnL = dawnLightGO.AddComponent<Light>();
            dawnL.type = LightType.Point;
            dawnL.color = new Color(1.0f, 0.42f, 0.08f);
            dawnL.intensity = 10.0f;
            dawnL.range = 80f;

            // Abyss bottom floor disc and deep crimson volumetric light
            var pitBottom = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pitBottom.name = "Pit_Abyss_Floor";
            pitBottom.transform.SetParent(pitRoot.transform, false);
            pitBottom.transform.localPosition = new Vector3(0f, 0.65f - pitDepth, 0f);
            pitBottom.transform.localScale = new Vector3(pitRadius * 2.2f, 0.1f, pitRadius * 2.2f);
            pitBottom.GetComponent<MeshRenderer>().sharedMaterial = blackSlabMat;
            Object.DestroyImmediate(pitBottom.GetComponent<Collider>());

            var pitAbyssLight = new GameObject("Light_Pit_Abyss");
            pitAbyssLight.transform.SetParent(pitRoot.transform, false);
            pitAbyssLight.transform.localPosition = new Vector3(0f, 0.65f - pitDepth * 0.7f, 0f);
            var pLight = pitAbyssLight.AddComponent<Light>();
            pLight.type = LightType.Point;
            pLight.color = new Color(1.0f, 0.12f, 0.02f);
            pLight.intensity = 8.0f;
            pLight.range = 65f;

            vfx.abyssPitRoot = pitRoot.transform;

            // Camera anchors for Subterranean Pit Shot (diagonal canyon rim glide)
            var pCamStart = new GameObject("CamAnchor_PitStart");
            pCamStart.transform.SetParent(pitRoot.transform, false);
            pCamStart.transform.localPosition = new Vector3(-1.0f, 8.5f, -18.0f);
            vfx.pitCamStart = pCamStart.transform;

            var pCamEnd = new GameObject("CamAnchor_PitEnd");
            pCamEnd.transform.SetParent(pitRoot.transform, false);
            pCamEnd.transform.localPosition = new Vector3(0.0f, 7.5f, -8.0f);
            vfx.pitCamEnd = pCamEnd.transform;

            // =========================================================================
            // 2. High Cyberspace Orbital Environment (34.5s - 48.0s)
            // =========================================================================
            GameObject orbEnv = new GameObject("Orbital_Space_Environment");
            orbEnv.transform.SetParent(vfxGO.transform, false);
            orbEnv.transform.position = new Vector3(0f, 200f, -420f);
            vfx.orbitalEnvironmentRoot = orbEnv.transform;

            // =========================================================================
            // 3. Cyber Planet Curvature Hemisphere (Ultra-Wide 560m sphere)
            // =========================================================================
            GameObject planetRoot = new GameObject("Cyber_Planet_Curvature");
            planetRoot.transform.SetParent(orbEnv.transform, false);
            vfx.planetCurvatureRoot = planetRoot.transform;

            float sphereRadius = 280f;
            Vector3 sphereCenterLocal = new Vector3(0f, -220f, 110f);

            var planetSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            planetSphere.name = "Planet_Sphere_Mesh";
            planetSphere.transform.SetParent(planetRoot.transform, false);
            planetSphere.transform.localPosition = sphereCenterLocal;
            planetSphere.transform.localScale = Vector3.one * (sphereRadius * 2f);
            planetSphere.GetComponent<MeshRenderer>().sharedMaterial = redGridMat;
            Object.DestroyImmediate(planetSphere.GetComponent<Collider>());

            // Micro-circuit voxel clusters on planet surface
            GameObject cityRoot = new GameObject("Planet_City_Voxel_Grid");
            cityRoot.transform.SetParent(planetRoot.transform, false);
            cityRoot.transform.localPosition = sphereCenterLocal;

            int cityBlocks = 240;
            float goldenRatio = (1f + Mathf.Sqrt(5f)) * 0.5f;
            for (int b = 0; b < cityBlocks; b++)
            {
                float theta = 2f * Mathf.PI * b / goldenRatio;
                float phi = Mathf.Acos(1f - (b / (float)cityBlocks) * 0.38f);
                Vector3 bDir = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta)).normalized;

                var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.name = $"CityBlock_{b}";
                block.transform.SetParent(cityRoot.transform, false);
                block.transform.localPosition = bDir * (sphereRadius + 0.25f);
                block.transform.localRotation = Quaternion.LookRotation(bDir);
                block.transform.localScale = new Vector3(Random.Range(1.2f, 3.8f), Random.Range(1.2f, 3.8f), Random.Range(0.3f, 1.2f));
                block.GetComponent<MeshRenderer>().sharedMaterial = (b % 4 == 0) ? amberMat : ((b % 2 == 0) ? redOrangeMat : blackSlabMat);
                Object.DestroyImmediate(block.GetComponent<Collider>());
            }

            // =========================================================================
            // 4. Ultra-Wide 650m Multi-Layer Atmospheric Sunrise Horizon Arc (GRETA LEE)
            // =========================================================================
            GameObject atmoRoot = new GameObject("Atmospheric_Sunrise_Horizon_Arc");
            atmoRoot.transform.SetParent(orbEnv.transform, false);
            atmoRoot.transform.localPosition = new Vector3(0f, 68f, 105f);
            vfx.atmosphericHorizonRoot = atmoRoot.transform;

            int arcSegs = 96;
            float arcSpan = 650f;
            float atmoCurveDrop = 38f;

            CreateCurvedHorizonRibbon(atmoRoot.transform, "Horizon_Layer1_Blue_Limb", arcSpan, 2.5f, 1.0f, 1.0f, atmoCurveDrop, arcSegs, blueAtmoMat);
            CreateCurvedHorizonRibbon(atmoRoot.transform, "Horizon_Layer2_White_Solar", arcSpan, 1.2f, 1.2f, 1.2f, atmoCurveDrop, arcSegs, whiteCoreMat);
            CreateCurvedHorizonRibbon(atmoRoot.transform, "Horizon_Layer3_Amber_Corona", arcSpan, 0.0f, 1.8f, 1.5f, atmoCurveDrop, arcSegs, amberMat);
            CreateCurvedHorizonRibbon(atmoRoot.transform, "Horizon_Layer4_RedOrange_Flare", arcSpan, -1.5f, 2.5f, 1.8f, atmoCurveDrop, arcSegs, redOrangeMat);
            CreateCurvedHorizonRibbon(atmoRoot.transform, "Horizon_Layer5_Crimson_Haze", arcSpan, -3.5f, 3.5f, 2.0f, atmoCurveDrop, arcSegs, deepRedMat);

            var sunDisk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            sunDisk.name = "Solar_Sunrise_Core_Disc";
            sunDisk.transform.SetParent(atmoRoot.transform, false);
            sunDisk.transform.localPosition = new Vector3(0f, 1.2f, 3.5f);
            sunDisk.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            sunDisk.transform.localScale = new Vector3(25f, 0.15f, 2.5f);
            sunDisk.GetComponent<MeshRenderer>().sharedMaterial = whiteCoreMat;
            Object.DestroyImmediate(sunDisk.GetComponent<Collider>());

            var sunFlareBeam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sunFlareBeam.name = "Solar_Anamorphic_Flare_Streak";
            sunFlareBeam.transform.SetParent(atmoRoot.transform, false);
            sunFlareBeam.transform.localPosition = new Vector3(0f, 1.2f, 3.2f);
            sunFlareBeam.transform.localScale = new Vector3(320f, 0.65f, 0.15f);
            sunFlareBeam.GetComponent<MeshRenderer>().sharedMaterial = redOrangeMat;
            Object.DestroyImmediate(sunFlareBeam.GetComponent<Collider>());

            var rimLightGO = new GameObject("Light_Atmospheric_Solar_Rim");
            rimLightGO.transform.SetParent(atmoRoot.transform, false);
            rimLightGO.transform.localPosition = new Vector3(0f, 4f, -12f);
            var rimL = rimLightGO.AddComponent<Light>();
            rimL.type = LightType.Point;
            rimL.color = new Color(1.0f, 0.58f, 0.18f);
            rimL.intensity = 26.0f;
            rimL.range = 550f;
            vfx.atmosphericRimLight = rimL;

            // Camera anchors for Orbital Shot (GRETA LEE)
            var oCamStart = new GameObject("CamAnchor_OrbitalStart");
            oCamStart.transform.SetParent(orbEnv.transform, false);
            oCamStart.transform.localPosition = new Vector3(0f, 122f, -155f);
            vfx.orbitalCamStart = oCamStart.transform;

            var oCamEnd = new GameObject("CamAnchor_OrbitalEnd");
            oCamEnd.transform.SetParent(orbEnv.transform, false);
            oCamEnd.transform.localPosition = new Vector3(0f, 108f, -128f);
            vfx.orbitalCamEnd = oCamEnd.transform;

            // =========================================================================
            // 5. Radiant Caldera & Laser Filaments (EVAN PETERS, 37s - 43s)
            // Colossal planetary energy aperture on forward curvature, tilted 28 deg toward camera
            // =========================================================================
            GameObject calderaRoot = new GameObject("Radiant_Caldera_Energy_Aperture");
            calderaRoot.transform.SetParent(orbEnv.transform, false);
            calderaRoot.transform.localPosition = new Vector3(0f, 40f, 42f);
            calderaRoot.transform.localRotation = Quaternion.Euler(28f, 0f, 0f);
            vfx.radiantCalderaRoot = calderaRoot.transform;

            int cRimSegs = 64;
            float cRimRadius = 24.0f;
            for (int r = 0; r < cRimSegs; r++)
            {
                float ang = (r / (float)cRimSegs) * Mathf.PI * 2f;
                Vector3 rPos = new Vector3(Mathf.Cos(ang) * cRimRadius, 0.20f, Mathf.Sin(ang) * cRimRadius);
                var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                seg.name = $"CalderaRim_{r}";
                seg.transform.SetParent(calderaRoot.transform, false);
                seg.transform.localPosition = rPos;
                seg.transform.localRotation = Quaternion.Euler(0f, -ang * Mathf.Rad2Deg, 0f);
                seg.transform.localScale = new Vector3(2.5f, 0.45f, 0.40f);
                seg.GetComponent<MeshRenderer>().sharedMaterial = (r % 2 == 0) ? redOrangeMat : amberMat;
                Object.DestroyImmediate(seg.GetComponent<Collider>());
            }

            // Outer Undulating Prominence Waves
            GameObject promRoot = new GameObject("Caldera_Prominence_Waves");
            promRoot.transform.SetParent(calderaRoot.transform, false);
            int promCount = 56;
            for (int pr = 0; pr < promCount; pr++)
            {
                float ang = (pr / (float)promCount) * Mathf.PI * 2f;
                float waveR = cRimRadius + 6.5f + Mathf.Sin(pr * 3.5f) * 2.5f;
                Vector3 pDir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                var promArch = GameObject.CreatePrimitive(PrimitiveType.Cube);
                promArch.name = $"PromArch_{pr}";
                promArch.transform.SetParent(promRoot.transform, false);
                promArch.transform.localPosition = pDir * waveR + new Vector3(0f, 0.35f + Mathf.Sin(pr * 2f) * 0.25f, 0f);
                promArch.transform.localRotation = Quaternion.Euler(Mathf.Sin(pr * 4f) * 12f, -ang * Mathf.Rad2Deg, 0f);
                promArch.transform.localScale = new Vector3(3.2f, 0.45f, 1.2f);
                promArch.GetComponent<MeshRenderer>().sharedMaterial = (pr % 2 == 0) ? amberMat : redOrangeMat;
                Object.DestroyImmediate(promArch.GetComponent<Collider>());
            }

            // 240 Radial Laser Filaments (radiating flat across the surface plane)
            GameObject filamentsRoot = new GameObject("Radial_Laser_Filaments");
            filamentsRoot.transform.SetParent(calderaRoot.transform, false);
            vfx.radialFilamentsRoot = filamentsRoot.transform;

            int filCount = 240;
            for (int f = 0; f < filCount; f++)
            {
                float ang = (f / (float)filCount) * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)).normalized;
                float filLen = Random.Range(35f, 95f);
                float filThick = 0.045f;

                var fil = GameObject.CreatePrimitive(PrimitiveType.Cube);
                fil.name = $"Filament_{f}";
                fil.transform.SetParent(filamentsRoot.transform, false);
                fil.transform.localPosition = dir * (cRimRadius + filLen * 0.5f) + new Vector3(0f, 0.05f, 0f);
                fil.transform.localRotation = Quaternion.Euler(0f, -ang * Mathf.Rad2Deg, 0f);
                fil.transform.localScale = new Vector3(filThick, 0.025f, filLen);
                fil.GetComponent<MeshRenderer>().sharedMaterial = (f % 3 == 0) ? amberMat : ((f % 2 == 0) ? deepRedMat : redOrangeMat);
                Object.DestroyImmediate(fil.GetComponent<Collider>());
            }

            // Concentric Iris Stroma
            GameObject stromaRoot = new GameObject("Concentric_Iris_Stroma_Petals");
            stromaRoot.transform.SetParent(calderaRoot.transform, false);
            vfx.concentricIrisStromaRoot = stromaRoot.transform;

            float[] stromaRadii = new float[] { 5.5f, 11.0f, 17.5f, 23.5f };
            int[] stromaSegs = new int[] { 24, 36, 48, 60 };

            for (int sr = 0; sr < stromaRadii.Length; sr++)
            {
                float sRadius = stromaRadii[sr];
                int sCount = stromaSegs[sr];
                var sRing = new GameObject($"StromaRing_{sr}");
                sRing.transform.SetParent(stromaRoot.transform, false);

                for (int sp = 0; sp < sCount; sp++)
                {
                    float ang = (sp / (float)sCount) * Mathf.PI * 2f;
                    Vector3 sPos = new Vector3(Mathf.Cos(ang) * sRadius, 0.12f + sr * 0.06f, Mathf.Sin(ang) * sRadius);
                    var pet = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    pet.name = $"Petal_{sp}";
                    pet.transform.SetParent(sRing.transform, false);
                    pet.transform.localPosition = sPos;
                    pet.transform.localRotation = Quaternion.Euler(sr * 6f, -ang * Mathf.Rad2Deg + Mathf.Sin(sp) * 12f, 0f);
                    pet.transform.localScale = new Vector3(0.65f + sr * 0.18f, 0.05f, 0.45f + sr * 0.12f);
                    pet.GetComponent<MeshRenderer>().sharedMaterial = (sr == 0) ? whiteCoreMat : (sr == 1 ? amberMat : (sr == 2 ? redOrangeMat : deepRedMat));
                    Object.DestroyImmediate(pet.GetComponent<Collider>());
                }
            }

            // Caldera Dark Sunken Aperture (Pupil Void)
            var calAperture = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            calAperture.name = "Caldera_Aperture_Pupil_Void";
            calAperture.transform.SetParent(calderaRoot.transform, false);
            calAperture.transform.localPosition = new Vector3(0f, -0.05f, 0f);
            calAperture.transform.localScale = new Vector3(4.8f, 0.12f, 4.8f);
            calAperture.GetComponent<MeshRenderer>().sharedMaterial = blackSlabMat;
            Object.DestroyImmediate(calAperture.GetComponent<Collider>());

            var calLightGO = new GameObject("Light_Caldera_Core");
            calLightGO.transform.SetParent(calderaRoot.transform, false);
            calLightGO.transform.localPosition = new Vector3(0f, 2.5f, 0f);
            var calL = calLightGO.AddComponent<Light>();
            calL.type = LightType.Point;
            calL.color = new Color(1.0f, 0.42f, 0.10f);
            calL.intensity = 12.0f;
            calL.range = 95f;
            vfx.calderaCoreLight = calL;

            // Camera anchors for Orbital Dive (EVAN PETERS) - Steep plunge right into the colossal caldera
            var dCamStart = new GameObject("CamAnchor_DiveStart");
            dCamStart.transform.SetParent(orbEnv.transform, false);
            dCamStart.transform.localPosition = new Vector3(0f, 95f, -25f);
            vfx.diveCamStart = dCamStart.transform;

            var dCamEnd = new GameObject("CamAnchor_DiveEnd");
            dCamEnd.transform.SetParent(orbEnv.transform, false);
            dCamEnd.transform.localPosition = new Vector3(0f, 52f, 30f);
            vfx.diveCamEnd = dCamEnd.transform;

            // =========================================================================
            // 6. Colossal Wireframe Cybernetic Human Eye (43.5s - 48.0s)
            // Independent root, frontally facing camera, isolated against obsidian void
            // High-density cybernetic wireframe lattice matching frame 0090-0096
            // =========================================================================
            GameObject cyberEyeRoot = new GameObject("Colossal_Cybernetic_Human_Eye");
            cyberEyeRoot.transform.SetParent(orbEnv.transform, false);
            cyberEyeRoot.transform.localPosition = new Vector3(0f, 50f, -40f);
            cyberEyeRoot.transform.localRotation = Quaternion.identity;
            vfx.colossalCyberEyeRoot = cyberEyeRoot.transform;

            // 6A. 3D Curved Sclera Eyeball Sphere (Smooth sphere with TRON Red Grid & Wireframe Lat/Long Rings)
            // Radius 7.4m, center at z = 4.2m -> front pole at z = 4.2 - 7.4 = -3.20m
            float scleraR = 7.4f;
            var scleraSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            scleraSphere.name = "Eyeball_Sclera_Grid_Sphere";
            scleraSphere.transform.SetParent(cyberEyeRoot.transform, false);
            scleraSphere.transform.localPosition = new Vector3(0f, 0f, 4.2f);
            scleraSphere.transform.localScale = Vector3.one * (scleraR * 2f);
            scleraSphere.GetComponent<MeshRenderer>().sharedMaterial = redGridMat;
            Object.DestroyImmediate(scleraSphere.GetComponent<Collider>());

            // Spherical Wireframe Latitude & Longitude Grid on front hemisphere
            GameObject scleraGridRoot = new GameObject("Sclera_Wireframe_Grid_Rings");
            scleraGridRoot.transform.SetParent(cyberEyeRoot.transform, false);
            scleraGridRoot.transform.localPosition = new Vector3(0f, 0f, 4.2f); // Centered at eyeball sphere center

            // 9 Horizontal Latitude Rings curving across front hemisphere
            float[] latAngles = new float[] { -36f, -27f, -18f, -9f, 0f, 9f, 18f, 27f, 36f };
            for (int la = 0; la < latAngles.Length; la++)
            {
                float latRad = latAngles[la] * Mathf.Deg2Rad;
                float ringY = scleraR * Mathf.Sin(latRad);
                float ringR = scleraR * Mathf.Cos(latRad);

                int ringSegs = 48;
                for (int rs = 0; rs < ringSegs; rs++)
                {
                    float a = Mathf.Lerp(-75f, 75f, rs / (float)(ringSegs - 1)) * Mathf.Deg2Rad;
                    Vector3 rPos = new Vector3(ringR * Mathf.Sin(a), ringY, -ringR * Mathf.Cos(a));

                    var latSeg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    latSeg.name = $"Lat_{la}_{rs}";
                    latSeg.transform.SetParent(scleraGridRoot.transform, false);
                    latSeg.transform.localPosition = rPos;
                    latSeg.transform.localRotation = Quaternion.Euler(-latAngles[la], a * Mathf.Rad2Deg, 0f);
                    latSeg.transform.localScale = new Vector3(ringR * 2.6f * Mathf.PI / ringSegs * 0.75f, 0.022f, 0.022f);
                    latSeg.GetComponent<MeshRenderer>().sharedMaterial = (la % 2 == 0) ? redOrangeMat : deepRedMat;
                    Object.DestroyImmediate(latSeg.GetComponent<Collider>());
                }
            }

            // 11 Vertical Longitude Meridian Arches across front hemisphere
            float[] merAngles = new float[] { -50f, -40f, -30f, -20f, -10f, 0f, 10f, 20f, 30f, 40f, 50f };
            for (int ma = 0; ma < merAngles.Length; ma++)
            {
                float merRad = merAngles[ma] * Mathf.Deg2Rad;
                int merSegs = 36;
                for (int ms = 0; ms < merSegs; ms++)
                {
                    float v = Mathf.Lerp(-45f, 45f, ms / (float)(merSegs - 1)) * Mathf.Deg2Rad;
                    float y = scleraR * Mathf.Sin(v);
                    float rXZ = scleraR * Mathf.Cos(v);
                    Vector3 mPos = new Vector3(rXZ * Mathf.Sin(merRad), y, -rXZ * Mathf.Cos(merRad));

                    var merSeg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    merSeg.name = $"Mer_{ma}_{ms}";
                    merSeg.transform.SetParent(scleraGridRoot.transform, false);
                    merSeg.transform.localPosition = mPos;
                    merSeg.transform.localRotation = Quaternion.Euler(-v * Mathf.Rad2Deg, merAngles[ma], 0f);
                    merSeg.transform.localScale = new Vector3(0.02f, scleraR * 1.6f / merSegs * 1.15f, 0.02f);
                    merSeg.GetComponent<MeshRenderer>().sharedMaterial = deepRedMat;
                    Object.DestroyImmediate(merSeg.GetComponent<Collider>());
                }
            }

            // 6B. Almond Cybernetic Eyelids (Multi-strand glowing wireframe contours with cross-hatch struts)
            GameObject lidsRoot = new GameObject("Almond_Eyelid_Arcs");
            lidsRoot.transform.SetParent(cyberEyeRoot.transform, false);
            vfx.eyeLidsRoot = lidsRoot.transform;

            int lidSegs = 64;
            float eyeHalfW = 10.5f;
            float upperArchH = 4.2f;
            float lowerArchH = -2.9f;

            // Upper Eyelid: 4 parallel contoured strands (Inner edge -> outer socket rim)
            float[] upperOffsets = new float[] { 0.0f, 0.22f, 0.48f, 0.78f };
            float[] upperThick = new float[] { 0.055f, 0.045f, 0.04f, 0.04f };
            Material[] upperMats = new Material[] { amberMat, redOrangeMat, deepRedMat, deepRedMat };

            for (int sIdx = 0; sIdx < 4; sIdx++)
            {
                float uOff = upperOffsets[sIdx];
                float uThk = upperThick[sIdx];
                Material uMat = upperMats[sIdx];

                for (int ls = 0; ls < lidSegs; ls++)
                {
                    float t = ls / (float)(lidSegs - 1);
                    float x = Mathf.Lerp(-eyeHalfW, eyeHalfW, t);
                    float curve = Mathf.Sin(t * Mathf.PI);

                    Vector3 upPos = new Vector3(x, curve * (upperArchH + uOff), -3.20f - curve * 1.10f);
                    var upSeg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    upSeg.name = $"UpperStrand_{sIdx}_{ls}";
                    upSeg.transform.SetParent(lidsRoot.transform, false);
                    upSeg.transform.localPosition = upPos;
                    upSeg.transform.localScale = new Vector3(eyeHalfW / (float)lidSegs * 1.25f, uThk, uThk);
                    upSeg.GetComponent<MeshRenderer>().sharedMaterial = uMat;
                    Object.DestroyImmediate(upSeg.GetComponent<Collider>());
                }
            }

            // Lower Eyelid: 3 parallel contoured strands
            float[] lowerOffsets = new float[] { 0.0f, -0.22f, -0.48f };
            float[] lowerThick = new float[] { 0.050f, 0.040f, 0.035f };
            Material[] lowerMats = new Material[] { redOrangeMat, deepRedMat, deepRedMat };

            for (int sIdx = 0; sIdx < 3; sIdx++)
            {
                float lOff = lowerOffsets[sIdx];
                float lThk = lowerThick[sIdx];
                Material lMat = lowerMats[sIdx];

                for (int ls = 0; ls < lidSegs; ls++)
                {
                    float t = ls / (float)(lidSegs - 1);
                    float x = Mathf.Lerp(-eyeHalfW, eyeHalfW, t);
                    float curve = Mathf.Sin(t * Mathf.PI);

                    Vector3 loPos = new Vector3(x, curve * (lowerArchH + lOff), -3.20f - curve * 1.10f);
                    var loSeg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    loSeg.name = $"LowerStrand_{sIdx}_{ls}";
                    loSeg.transform.SetParent(lidsRoot.transform, false);
                    loSeg.transform.localPosition = loPos;
                    loSeg.transform.localScale = new Vector3(eyeHalfW / (float)lidSegs * 1.25f, lThk, lThk);
                    loSeg.GetComponent<MeshRenderer>().sharedMaterial = lMat;
                    Object.DestroyImmediate(loSeg.GetComponent<Collider>());
                }
            }

            // 48 Cross-Hatch Cyber Struts linking the eyelid strands into an authentic wireframe lattice
            int strutCount = 48;
            for (int st = 0; st < strutCount; st++)
            {
                float stT = st / (float)(strutCount - 1);
                float stX = Mathf.Lerp(-eyeHalfW * 0.92f, eyeHalfW * 0.92f, stT);
                float stCurve = Mathf.Sin(stT * Mathf.PI);

                var uStrut = GameObject.CreatePrimitive(PrimitiveType.Cube);
                uStrut.name = $"UpperStrut_{st}";
                uStrut.transform.SetParent(lidsRoot.transform, false);
                uStrut.transform.localPosition = new Vector3(stX, stCurve * (upperArchH + 0.38f), -3.20f - stCurve * 1.10f);
                uStrut.transform.localScale = new Vector3(0.025f, 0.72f, 0.025f);
                uStrut.GetComponent<MeshRenderer>().sharedMaterial = (st % 2 == 0) ? amberMat : redOrangeMat;
                Object.DestroyImmediate(uStrut.GetComponent<Collider>());

                var lStrut = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lStrut.name = $"LowerStrut_{st}";
                lStrut.transform.SetParent(lidsRoot.transform, false);
                lStrut.transform.localPosition = new Vector3(stX, stCurve * (lowerArchH - 0.24f), -3.20f - stCurve * 1.10f);
                lStrut.transform.localScale = new Vector3(0.025f, 0.48f, 0.025f);
                lStrut.GetComponent<MeshRenderer>().sharedMaterial = deepRedMat;
                Object.DestroyImmediate(lStrut.GetComponent<Collider>());
            }

            // 6C. Bio-Cybernetic Iris Rosette Blossom (Intricate floral mandala in center)
            // Sits right at the front of the eyeball at z = -3.24f
            GameObject eyeIrisRoot = new GameObject("Cyber_Eye_Iris_Blossom");
            eyeIrisRoot.transform.SetParent(cyberEyeRoot.transform, false);
            eyeIrisRoot.transform.localPosition = new Vector3(0f, 0f, -3.24f);

            // Central pupil aperture void
            var eyePupilVoid = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            eyePupilVoid.name = "Cyber_Eye_Pupil_Void";
            eyePupilVoid.transform.SetParent(eyeIrisRoot.transform, false);
            eyePupilVoid.transform.localPosition = new Vector3(0f, 0f, -0.02f);
            eyePupilVoid.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            eyePupilVoid.transform.localScale = new Vector3(1.15f, 0.04f, 1.15f);
            eyePupilVoid.GetComponent<MeshRenderer>().sharedMaterial = blackSlabMat;
            Object.DestroyImmediate(eyePupilVoid.GetComponent<Collider>());

            // Tier 1: Inner white-hot floral core petals (radius 1.55m, 24 petals)
            int t1Count = 24;
            for (int t1 = 0; t1 < t1Count; t1++)
            {
                float a = (t1 / (float)t1Count) * Mathf.PI * 2f;
                Vector3 tPos = new Vector3(Mathf.Cos(a) * 1.55f, Mathf.Sin(a) * 1.55f, -0.04f);
                var pet1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pet1.name = $"T1Petal_{t1}";
                pet1.transform.SetParent(eyeIrisRoot.transform, false);
                pet1.transform.localPosition = tPos;
                pet1.transform.localRotation = Quaternion.Euler(0f, 0f, -a * Mathf.Rad2Deg);
                pet1.transform.localScale = new Vector3(0.08f, 0.24f, 0.02f);
                pet1.GetComponent<MeshRenderer>().sharedMaterial = whiteCoreMat;
                Object.DestroyImmediate(pet1.GetComponent<Collider>());
            }

            // Tier 2: Middle amber floral petals (radius 2.25m, 32 petals)
            int t2Count = 32;
            for (int t2 = 0; t2 < t2Count; t2++)
            {
                float a = (t2 / (float)t2Count) * Mathf.PI * 2f;
                Vector3 tPos = new Vector3(Mathf.Cos(a) * 2.25f, Mathf.Sin(a) * 2.25f, -0.02f);
                var pet2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pet2.name = $"T2Petal_{t2}";
                pet2.transform.SetParent(eyeIrisRoot.transform, false);
                pet2.transform.localPosition = tPos;
                pet2.transform.localRotation = Quaternion.Euler(0f, 0f, -a * Mathf.Rad2Deg + Mathf.Sin(t2 * 2f) * 10f);
                pet2.transform.localScale = new Vector3(0.12f, 0.42f, 0.02f);
                pet2.GetComponent<MeshRenderer>().sharedMaterial = amberMat;
                Object.DestroyImmediate(pet2.GetComponent<Collider>());
            }

            // Tier 3: Outer red-orange coronal petals (radius 3.05m, 40 petals)
            int t3Count = 40;
            for (int t3 = 0; t3 < t3Count; t3++)
            {
                float a = (t3 / (float)t3Count) * Mathf.PI * 2f;
                Vector3 tPos = new Vector3(Mathf.Cos(a) * 3.05f, Mathf.Sin(a) * 3.05f, 0.01f);
                var pet3 = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pet3.name = $"T3Petal_{t3}";
                pet3.transform.SetParent(eyeIrisRoot.transform, false);
                pet3.transform.localPosition = tPos;
                pet3.transform.localRotation = Quaternion.Euler(0f, 0f, -a * Mathf.Rad2Deg);
                pet3.transform.localScale = new Vector3(0.15f, 0.58f, 0.02f);
                pet3.GetComponent<MeshRenderer>().sharedMaterial = redOrangeMat;
                Object.DestroyImmediate(pet3.GetComponent<Collider>());
            }

            // Tier 4: Coronal perimeter ring (radius 3.65m, 48 segments)
            int t4Count = 48;
            for (int t4 = 0; t4 < t4Count; t4++)
            {
                float a = (t4 / (float)t4Count) * Mathf.PI * 2f;
                Vector3 tPos = new Vector3(Mathf.Cos(a) * 3.65f, Mathf.Sin(a) * 3.65f, 0.02f);
                var pet4 = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pet4.name = $"T4Ring_{t4}";
                pet4.transform.SetParent(eyeIrisRoot.transform, false);
                pet4.transform.localPosition = tPos;
                pet4.transform.localRotation = Quaternion.Euler(0f, 0f, -a * Mathf.Rad2Deg);
                pet4.transform.localScale = new Vector3(0.20f, 0.035f, 0.02f);
                pet4.GetComponent<MeshRenderer>().sharedMaterial = amberMat;
                Object.DestroyImmediate(pet4.GetComponent<Collider>());
            }

            // 80 Radial Iris Laser Filaments radiating between petals
            int filIrisCount = 80;
            for (int fi = 0; fi < filIrisCount; fi++)
            {
                float a = (fi / (float)filIrisCount) * Mathf.PI * 2f;
                Vector3 fPos = new Vector3(Mathf.Cos(a) * 2.45f, Mathf.Sin(a) * 2.45f, 0.0f);
                var filI = GameObject.CreatePrimitive(PrimitiveType.Cube);
                filI.name = $"IrisFil_{fi}";
                filI.transform.SetParent(eyeIrisRoot.transform, false);
                filI.transform.localPosition = fPos;
                filI.transform.localRotation = Quaternion.Euler(0f, 0f, -a * Mathf.Rad2Deg);
                filI.transform.localScale = new Vector3(0.012f, 2.3f, 0.012f);
                filI.GetComponent<MeshRenderer>().sharedMaterial = (fi % 3 == 0) ? whiteCoreMat : ((fi % 2 == 0) ? amberMat : redOrangeMat);
                Object.DestroyImmediate(filI.GetComponent<Collider>());
            }

            // 6D. Facial Wireframe Topology & Voxel Clusters
            GameObject topoRoot = new GameObject("Facial_Wireframe_Topology_Voxels");
            topoRoot.transform.SetParent(cyberEyeRoot.transform, false);
            vfx.faceWireframeTopologyRoot = topoRoot.transform;

            // Brow Ridge Arch (36 segments)
            int browSegs = 36;
            for (int br = 0; br < browSegs; br++)
            {
                float bt = br / (float)(browSegs - 1);
                float bx = Mathf.Lerp(-eyeHalfW * 1.25f, eyeHalfW * 1.25f, bt);
                float by = Mathf.Sin(bt * Mathf.PI) * (upperArchH + 1.8f) + 0.8f;
                var browCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                browCube.name = $"BrowVoxel_{br}";
                browCube.transform.SetParent(topoRoot.transform, false);
                browCube.transform.localPosition = new Vector3(bx, by, -2.8f);
                browCube.transform.localScale = new Vector3(0.55f, 0.05f, 0.04f);
                browCube.GetComponent<MeshRenderer>().sharedMaterial = (br % 3 == 0) ? amberMat : redOrangeMat;
                Object.DestroyImmediate(browCube.GetComponent<Collider>());
            }

            // Lower Orbital Rim / Zygomatic Cheek Arch (32 segments)
            int cheekSegs = 32;
            for (int ck = 0; ck < cheekSegs; ck++)
            {
                float ct = ck / (float)(cheekSegs - 1);
                float cx = Mathf.Lerp(-eyeHalfW * 1.20f, eyeHalfW * 1.20f, ct);
                float cy = -Mathf.Sin(ct * Mathf.PI) * (Mathf.Abs(lowerArchH) + 1.6f) - 0.4f;
                var cheekCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cheekCube.name = $"CheekVoxel_{ck}";
                cheekCube.transform.SetParent(topoRoot.transform, false);
                cheekCube.transform.localPosition = new Vector3(cx, cy, -2.7f);
                cheekCube.transform.localScale = new Vector3(0.50f, 0.045f, 0.04f);
                cheekCube.GetComponent<MeshRenderer>().sharedMaterial = (ck % 2 == 0) ? redOrangeMat : deepRedMat;
                Object.DestroyImmediate(cheekCube.GetComponent<Collider>());
            }

            // Temple & Cheekbone 3D Voxel Clusters (64 nodes)
            int voxelCount = 64;
            for (int v = 0; v < voxelCount; v++)
            {
                float vSide = (v % 2 == 0) ? 1f : -1f;
                float vx = vSide * Random.Range(eyeHalfW * 0.95f, eyeHalfW * 1.65f);
                float vy = Random.Range(-5.0f, 6.0f);
                float vz = Random.Range(-3.5f, 1.5f);

                var vox = GameObject.CreatePrimitive(PrimitiveType.Cube);
                vox.name = $"FaceVoxel_{v}";
                vox.transform.SetParent(topoRoot.transform, false);
                vox.transform.localPosition = new Vector3(vx, vy, vz);
                vox.transform.localScale = Vector3.one * Random.Range(0.12f, 0.35f);
                vox.GetComponent<MeshRenderer>().sharedMaterial = (v % 4 == 0) ? whiteCoreMat : ((v % 2 == 0) ? redOrangeMat : deepRedMat);
                Object.DestroyImmediate(vox.GetComponent<Collider>());
            }

            // Key Light for Wireframe Eye
            var eyeKeyLightGO = new GameObject("Light_Cyber_Eye_Key");
            eyeKeyLightGO.transform.SetParent(cyberEyeRoot.transform, false);
            eyeKeyLightGO.transform.localPosition = new Vector3(0f, 1.2f, -14.0f);
            var eyeKeyL = eyeKeyLightGO.AddComponent<Light>();
            eyeKeyL.type = LightType.Point;
            eyeKeyL.color = new Color(1.0f, 0.48f, 0.14f);
            eyeKeyL.intensity = 3.2f;
            eyeKeyL.range = 32f;
            vfx.cyberEyeKeyLight = eyeKeyL;

            // Camera anchors for Cyber Eye (JODIE TURNER-SMITH & HASAN MINHAJ)
            var eCamStart = new GameObject("CamAnchor_EyeStart");
            eCamStart.transform.SetParent(cyberEyeRoot.transform, false);
            eCamStart.transform.localPosition = new Vector3(0f, 0.25f, -14.8f);
            vfx.eyeCamStart = eCamStart.transform;

            var eCamEnd = new GameObject("CamAnchor_EyeEnd");
            eCamEnd.transform.SetParent(cyberEyeRoot.transform, false);
            eCamEnd.transform.localPosition = new Vector3(0f, 0.15f, -12.8f);
            vfx.eyeCamEnd = eCamEnd.transform;

            // 6E. Eye Explosion Outward Flying Lines (48.5s - 50.5s)
            GameObject expLinesRoot = new GameObject("Eye_Explosion_Outward_Lines");
            expLinesRoot.transform.SetParent(cyberEyeRoot.transform, false);
            expLinesRoot.transform.localPosition = new Vector3(0f, 0f, -3.2f);
            vfx.eyeExplosionLinesRoot = expLinesRoot.transform;

            int expLinesCount = 64;
            for (int el = 0; el < expLinesCount; el++)
            {
                float ang = (el / (float)expLinesCount) * Mathf.PI * 2f;
                float rDist = Random.Range(0.8f, 3.5f);
                Vector3 eDir = new Vector3(Mathf.Cos(ang) * rDist, Mathf.Sin(ang) * rDist, -Random.Range(0.8f, 3.2f)).normalized;
                float eLen = Random.Range(6.0f, 20.0f);

                var eLine = GameObject.CreatePrimitive(PrimitiveType.Cube);
                eLine.name = $"ExpLine_{el}";
                eLine.transform.SetParent(expLinesRoot.transform, false);
                eLine.transform.localPosition = eDir * (eLen * 0.5f);
                eLine.transform.localRotation = Quaternion.LookRotation(eDir);
                eLine.transform.localScale = new Vector3(0.025f, 0.025f, eLen);
                eLine.GetComponent<MeshRenderer>().sharedMaterial = (el % 3 == 0) ? whiteCoreMat : ((el % 2 == 0) ? amberMat : redOrangeMat);
                Object.DestroyImmediate(eLine.GetComponent<Collider>());
            }
            expLinesRoot.SetActive(false);

            // 6F. Reconstructing Cybernetic Chassis (Cranial Helmet, Neck, & Shoulders - 50.0s - 64.0s)
            GameObject reconChassisRoot = new GameObject("Reconstructing_Cybernetic_Chassis");
            reconChassisRoot.transform.SetParent(vfxGO.transform, false);
            reconChassisRoot.transform.localPosition = new Vector3(0f, 32f, -40f);
            vfx.reconstructingChassisRoot = reconChassisRoot.transform;

            // Smooth Dark Reflective Cranial Skull Dome (Mirror material matching frame 0104)
            var skullDome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            skullDome.name = "Cranial_Skull_Dome";
            skullDome.transform.SetParent(reconChassisRoot.transform, false);
            skullDome.transform.localPosition = new Vector3(0f, 0f, 0f);
            skullDome.transform.localScale = new Vector3(2.6f, 3.2f, 3.4f);
            skullDome.GetComponent<MeshRenderer>().sharedMaterial = mirrorMat;
            Object.DestroyImmediate(skullDome.GetComponent<Collider>());

            // Occipital Rear Plate
            var occPlate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            occPlate.name = "Occipital_Armor_Plate";
            occPlate.transform.SetParent(reconChassisRoot.transform, false);
            occPlate.transform.localPosition = new Vector3(0f, -0.4f, -1.5f);
            occPlate.transform.localScale = new Vector3(1.8f, 1.6f, 0.4f);
            occPlate.GetComponent<MeshRenderer>().sharedMaterial = blackSlabMat;
            Object.DestroyImmediate(occPlate.GetComponent<Collider>());

            // Dual Horizontal Temple Conduits (The defining signature of Frame 0104 ARTURO CASTRO)
            for (int side = -1; side <= 1; side += 2)
            {
                var upConduit = GameObject.CreatePrimitive(PrimitiveType.Cube);
                upConduit.name = $"TempleConduit_Upper_{side}";
                upConduit.transform.SetParent(reconChassisRoot.transform, false);
                upConduit.transform.localPosition = new Vector3(side * 1.34f, 0.18f, 0.1f);
                upConduit.transform.localScale = new Vector3(0.045f, 0.045f, 4.4f);
                upConduit.GetComponent<MeshRenderer>().sharedMaterial = amberMat;
                Object.DestroyImmediate(upConduit.GetComponent<Collider>());

                var lowConduit = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lowConduit.name = $"TempleConduit_Lower_{side}";
                lowConduit.transform.SetParent(reconChassisRoot.transform, false);
                lowConduit.transform.localPosition = new Vector3(side * 1.34f, -0.18f, 0.1f);
                lowConduit.transform.localScale = new Vector3(0.045f, 0.045f, 4.4f);
                lowConduit.GetComponent<MeshRenderer>().sharedMaterial = redOrangeMat;
                Object.DestroyImmediate(lowConduit.GetComponent<Collider>());

                for (int b = 0; b < 10; b++)
                {
                    float bt = b / 9f;
                    float bz = -1.2f - bt * 0.9f;
                    float by = Mathf.Lerp(0.18f, 0.65f, bt);
                    var bPiece = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    bPiece.name = $"ConduitBranch_{side}_{b}";
                    bPiece.transform.SetParent(reconChassisRoot.transform, false);
                    bPiece.transform.localPosition = new Vector3(side * (1.30f - bt * 0.35f), by, bz);
                    bPiece.transform.localScale = new Vector3(0.04f, 0.04f, 0.14f);
                    bPiece.GetComponent<MeshRenderer>().sharedMaterial = (b % 2 == 0) ? amberMat : redOrangeMat;
                    Object.DestroyImmediate(bPiece.GetComponent<Collider>());
                }
            }

            // Sagittal Crest Arch (Forehead over crown to occiput - Frame 0112 GILLIAN ANDERSON)
            int sagCount = 36;
            for (int sc = 0; sc < sagCount; sc++)
            {
                float a = (sc / (float)(sagCount - 1)) * Mathf.PI;
                Vector3 scPos = new Vector3(0f, Mathf.Sin(a) * 1.75f + 0.1f, -Mathf.Cos(a) * 1.85f);
                var scBlock = GameObject.CreatePrimitive(PrimitiveType.Cube);
                scBlock.name = $"SagittalCrest_{sc}";
                scBlock.transform.SetParent(reconChassisRoot.transform, false);
                scBlock.transform.localPosition = scPos;
                scBlock.transform.localScale = new Vector3(0.08f, 0.04f, 0.16f);
                scBlock.GetComponent<MeshRenderer>().sharedMaterial = (sc % 2 == 0) ? whiteCoreMat : amberMat;
                Object.DestroyImmediate(scBlock.GetComponent<Collider>());
            }

            // 12 Bilateral Cranial Contour Ribs
            for (int side = -1; side <= 1; side += 2)
            {
                for (int rib = 0; rib < 6; rib++)
                {
                    float rz = -1.6f + rib * 0.6f;
                    int ribSegs = 14;
                    for (int rs = 0; rs < ribSegs; rs++)
                    {
                        float ang = (rs / (float)ribSegs) * (Mathf.PI * 0.5f);
                        Vector3 rPos = new Vector3(side * (Mathf.Cos(ang) * 1.38f), Mathf.Sin(ang) * 1.65f + 0.05f, rz);
                        var rSeg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        rSeg.name = $"CranialRib_{side}_{rib}_{rs}";
                        rSeg.transform.SetParent(reconChassisRoot.transform, false);
                        rSeg.transform.localPosition = rPos;
                        rSeg.transform.localScale = new Vector3(0.04f, 0.04f, 0.12f);
                        rSeg.GetComponent<MeshRenderer>().sharedMaterial = redOrangeMat;
                        Object.DestroyImmediate(rSeg.GetComponent<Collider>());
                    }
                }
            }

            // Cervical Spine (6 segmented vertebrae rings)
            for (int v = 0; v < 6; v++)
            {
                float vy = -1.6f - v * 0.32f;
                float vz = -0.4f + v * 0.12f;
                var vert = GameObject.CreatePrimitive(PrimitiveType.Cube);
                vert.name = $"CervicalVert_{v}";
                vert.transform.SetParent(reconChassisRoot.transform, false);
                vert.transform.localPosition = new Vector3(0f, vy, vz);
                vert.transform.localScale = new Vector3(0.65f, 0.14f, 0.55f);
                vert.GetComponent<MeshRenderer>().sharedMaterial = amberMat;
                Object.DestroyImmediate(vert.GetComponent<Collider>());
            }

            // Diagonal Outward Shoulder Beams (Frame 0112 GILLIAN ANDERSON & 0120 JEFF BRIDGES)
            for (int side = -1; side <= 1; side += 2)
            {
                var sBeam = GameObject.CreatePrimitive(PrimitiveType.Cube);
                sBeam.name = $"ShoulderBeam_{side}";
                sBeam.transform.SetParent(reconChassisRoot.transform, false);
                sBeam.transform.localPosition = new Vector3(side * 6.5f, -2.8f, 2.5f);
                sBeam.transform.localRotation = Quaternion.Euler(0f, 0f, side * 35f);
                sBeam.transform.localScale = new Vector3(0.09f, 0.09f, 22.0f);
                sBeam.GetComponent<MeshRenderer>().sharedMaterial = (side == -1) ? whiteCoreMat : redOrangeMat;
                Object.DestroyImmediate(sBeam.GetComponent<Collider>());

                var clav = GameObject.CreatePrimitive(PrimitiveType.Cube);
                clav.name = $"Clavicle_{side}";
                clav.transform.SetParent(reconChassisRoot.transform, false);
                clav.transform.localPosition = new Vector3(side * 1.8f, -2.4f, 0.6f);
                clav.transform.localScale = new Vector3(2.6f, 0.12f, 0.45f);
                clav.GetComponent<MeshRenderer>().sharedMaterial = redOrangeMat;
                Object.DestroyImmediate(clav.GetComponent<Collider>());
            }

            // Dedicated Incandescent Key Light for Chassis Reconstruction
            var chKeyLightGO = new GameObject("Light_Chassis_Key");
            chKeyLightGO.transform.SetParent(reconChassisRoot.transform, false);
            chKeyLightGO.transform.localPosition = new Vector3(0f, 1.2f, -2.5f);
            var chKeyL = chKeyLightGO.AddComponent<Light>();
            chKeyL.type = LightType.Point;
            chKeyL.color = new Color(1.0f, 0.38f, 0.12f);
            chKeyL.intensity = 15.0f;
            chKeyL.range = 35f;
            vfx.chassisKeyLight = chKeyL;

            // 6G. Molten Nanite Crystallization (58.0s - 64.0s - JEFF BRIDGES)
            GameObject naniteRoot = new GameObject("Molten_Nanite_Crystallization");
            naniteRoot.transform.SetParent(reconChassisRoot.transform, false);
            naniteRoot.transform.localPosition = new Vector3(0f, -2.2f, 0.2f);
            vfx.naniteCrystallizationRoot = naniteRoot.transform;

            int nVoxels = 160;
            for (int nv = 0; nv < nVoxels; nv++)
            {
                float nx = Random.Range(-2.4f, 2.4f);
                float ny = Random.Range(-1.2f, 0.8f);
                float nz = Random.Range(-0.8f, 1.4f);

                var nVox = GameObject.CreatePrimitive(PrimitiveType.Cube);
                nVox.name = $"NaniteVox_{nv}";
                nVox.transform.SetParent(naniteRoot.transform, false);
                nVox.transform.localPosition = new Vector3(nx, ny, nz);
                nVox.transform.localScale = Vector3.one * Random.Range(0.04f, 0.15f);
                nVox.GetComponent<MeshRenderer>().sharedMaterial = (nv % 3 == 0) ? whiteCoreMat : ((nv % 2 == 0) ? amberMat : redOrangeMat);
                Object.DestroyImmediate(nVox.GetComponent<Collider>());
            }
            naniteRoot.SetActive(false);
            reconChassisRoot.SetActive(false);

            // 6H. Eye Explosion Flash Light
            var expLightGO = new GameObject("Light_Eye_Explosion_Flash");
            expLightGO.transform.SetParent(cyberEyeRoot.transform, false);
            expLightGO.transform.localPosition = new Vector3(0f, 0f, -3.2f);
            var expL = expLightGO.AddComponent<Light>();
            expL.type = LightType.Point;
            expL.color = new Color(1.0f, 0.32f, 0.08f);
            expL.intensity = 0f;
            expL.range = 45f;
            vfx.eyeExplosionFlashLight = expL;

            // Camera anchors for Eye Close & Reconstruction
            var eCloseAnchor = new GameObject("CamAnchor_EyeClose");
            eCloseAnchor.transform.SetParent(cyberEyeRoot.transform, false);
            eCloseAnchor.transform.localPosition = new Vector3(0.55f, -0.15f, -9.2f);
            vfx.eyeCamClose = eCloseAnchor.transform;

            var cSweepStart = new GameObject("CamAnchor_CranialSweepStart");
            cSweepStart.transform.SetParent(cyberEyeRoot.transform, false);
            cSweepStart.transform.localPosition = new Vector3(-1.8f, 0.45f, -3.8f);
            vfx.cranialSweepStart = cSweepStart.transform;

            var cSweepEnd = new GameObject("CamAnchor_CranialSweepEnd");
            cSweepEnd.transform.SetParent(cyberEyeRoot.transform, false);
            cSweepEnd.transform.localPosition = new Vector3(-0.65f, 0.85f, -1.8f);
            vfx.cranialSweepEnd = cSweepEnd.transform;

            var hElevStart = new GameObject("CamAnchor_HelmetElevatedStart");
            hElevStart.transform.SetParent(cyberEyeRoot.transform, false);
            hElevStart.transform.localPosition = new Vector3(0f, 1.85f, -3.5f);
            vfx.helmetElevatedStart = hElevStart.transform;

            var hElevEnd = new GameObject("CamAnchor_HelmetElevatedEnd");
            hElevEnd.transform.SetParent(cyberEyeRoot.transform, false);
            hElevEnd.transform.localPosition = new Vector3(0f, 1.35f, -2.6f);
            vfx.helmetElevatedEnd = hElevEnd.transform;

            var spStart = new GameObject("CamAnchor_SpineStart");
            spStart.transform.SetParent(cyberEyeRoot.transform, false);
            spStart.transform.localPosition = new Vector3(0f, 1.25f, -2.4f);
            vfx.spineStart = spStart.transform;

            var spEnd = new GameObject("CamAnchor_SpineEnd");
            spEnd.transform.SetParent(cyberEyeRoot.transform, false);
            spEnd.transform.localPosition = new Vector3(0.15f, 0.85f, -1.6f);
            vfx.spineEnd = spEnd.transform;

            cyberEyeRoot.SetActive(false);
            return vfxGO;
        }

        private static void CreateCurvedHorizonRibbon(Transform parent, string name, float totalWidth, float yOffset, float height, float depth, float curveDrop, int count, Material mat)
        {
            GameObject ribbonRoot = new GameObject(name);
            ribbonRoot.transform.SetParent(parent, false);

            float halfW = totalWidth * 0.5f;

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)(count - 1); // 0 to 1
                float x = Mathf.Lerp(-halfW, halfW, t);
                float normX = x / halfW;
                float y = yOffset - (normX * normX) * curveDrop;

                var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                seg.name = $"Seg_{i}";
                seg.transform.SetParent(ribbonRoot.transform, false);
                seg.transform.localPosition = new Vector3(x, y, 0f);

                // Tangent slope tilt
                float slope = -2f * normX * (curveDrop / halfW);
                float angle = Mathf.Atan(slope) * Mathf.Rad2Deg;
                seg.transform.localRotation = Quaternion.Euler(0f, 0f, angle);

                float segWidth = (totalWidth / (float)count) * 1.15f;
                seg.transform.localScale = new Vector3(segWidth, height, depth);
                seg.GetComponent<MeshRenderer>().sharedMaterial = mat;
                Object.DestroyImmediate(seg.GetComponent<Collider>());
            }
        }
    }
}
