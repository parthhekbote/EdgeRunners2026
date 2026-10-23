#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Alita;

namespace PolygonCyberCity.NPC.Editor
{
    /// <summary>
    /// Builds the 3 movie-authentic evolution states of Alita from 'Alita: Battle Angel'
    /// using Synty Sidekick Characters, materials, and animations:
    /// 1. Broken Scrapyard State (Severed chassis, exposed chrome spine, beating URM core, sparks)
    /// 2. Normal Doll Body State (Ido's porcelain civilian streetwear, gentle expressive eyes)
    /// 3. Final Nano Body Armour State (URM Berserker nanotech chassis, blood eye stripes, Damascus Blade)
    /// </summary>
    public static class AlitaSidekickEvolutionBuilder
    {
        public const string PrefabsFolder = "Assets/Prefabs/Alita_Sidekick";
        public const string MaterialsFolder = "Assets/Prefabs/Alita_Sidekick/Materials";
        public const string ScenePath = "Assets/Scenes/Scene_Alita_3States_Showcase.unity";

        private const string SidekickHuman02 = "Assets/Synty/SidekickCharacters/Characters/HumanSpecies/HumanSpecies_02/HumanSpecies_02.prefab";
        private const string SidekickStarter01 = "Assets/Synty/SidekickCharacters/Characters/Starter/Starter_01/Starter_01.prefab";
        private const string SidekickStarter02 = "Assets/Synty/SidekickCharacters/Characters/Starter/Starter_02/Starter_02.prefab";

        private const string DamascusBladePrefab = "Assets/PolygonCyberCity/Prefabs/Weapons/SM_Wep_Sword_02.prefab";
        private const string FeminineIdleAnimPath = "Assets/Synty/AnimationBaseLocomotion/Animations/Polygon/Feminine/Idle/A_Idle_Standing_Femn.fbx";
        private const string SwordIdleAnimPath = "Assets/Synty/AnimationSwordCombat/Animations/Polygon/Idle/Base/A_Idle_Base_Sword.fbx";

        [MenuItem("Tools/Alita Battle Angel/Generate 3 Sidekick Alita States & Showcase Scene")]
        public static void GenerateAll()
        {
            EnsureFolders();

            Debug.Log("[AlitaSidekick] Starting generation of 3 Alita evolution states...");

            // 1. Build Materials
            EnsureMaterials();

            // 2. Build State 1: Broken Scrapyard State
            BuildState1_BrokenScrapyard();

            // 3. Build State 2: Normal Doll Body State
            BuildState2_NormalDollBody();

            // 4. Build State 3: Final Nano Body Armour Berserker State
            BuildState3_NanoBerserker();

            // 5. Build Showcase Scene featuring all 3 pedestals
            BuildShowcaseScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=#00F0FF><b>[AlitaSidekick]</b> Successfully created all 3 Alita evolution states and Showcase Scene!</color>");
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Alita_Sidekick"))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                {
                    AssetDatabase.CreateFolder("Assets", "Prefabs");
                }
                AssetDatabase.CreateFolder("Assets/Prefabs", "Alita_Sidekick");
            }
            if (!AssetDatabase.IsValidFolder(MaterialsFolder))
            {
                AssetDatabase.CreateFolder(PrefabsFolder, "Materials");
            }
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }
        }

        private static void EnsureMaterials()
        {
            // 1. Scrapyard Material
            string p1 = MaterialsFolder + "/M_Alita_Scrapyard.mat";
            Material m1 = AssetDatabase.LoadAssetAtPath<Material>(p1);
            if (m1 == null)
            {
                Material baseHumanMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Synty/SidekickCharacters/Characters/HumanSpecies/HumanSpecies_02/Materials/HumanSpecies_02.mat");
                if (baseHumanMat != null)
                {
                    m1 = new Material(baseHumanMat);
                    m1.name = "M_Alita_Scrapyard";
                    if (m1.HasProperty("_DirtAmount")) m1.SetFloat("_DirtAmount", 0.78f);
                    if (m1.HasProperty("_DarkAmount")) m1.SetFloat("_DarkAmount", 0.35f);
                    if (m1.HasProperty("_DirtColor")) m1.SetColor("_DirtColor", new Color(0.42f, 0.28f, 0.16f));
                    AssetDatabase.CreateAsset(m1, p1);
                }
            }

            // 2. Doll Body Material
            string p2 = MaterialsFolder + "/M_Alita_DollBody.mat";
            Material m2 = AssetDatabase.LoadAssetAtPath<Material>(p2);
            if (m2 == null)
            {
                Material baseStarter02Mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Synty/SidekickCharacters/Characters/Starter/Starter_02/Materials/Starter_02.mat");
                if (baseStarter02Mat != null)
                {
                    m2 = new Material(baseStarter02Mat);
                    m2.name = "M_Alita_DollBody";
                    if (m2.HasProperty("_DirtAmount")) m2.SetFloat("_DirtAmount", 0.0f);
                    if (m2.HasProperty("_DarkAmount")) m2.SetFloat("_DarkAmount", 0.0f);
                    if (m2.HasProperty("_SkinColor")) m2.SetColor("_SkinColor", new Color(1.0f, 0.90f, 0.84f));
                    if (m2.HasProperty("_SkinColorAmount")) m2.SetFloat("_SkinColorAmount", 0.65f);
                    if (m2.HasProperty("_EyelinerAmount")) m2.SetFloat("_EyelinerAmount", 0.60f);
                    AssetDatabase.CreateAsset(m2, p2);
                }
            }

            // 3. Berserker Material
            string p3 = MaterialsFolder + "/M_Alita_Berserker.mat";
            Material m3 = AssetDatabase.LoadAssetAtPath<Material>(p3);
            if (m3 == null)
            {
                Material baseStarter01Mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Synty/SidekickCharacters/Characters/Starter/Starter_01/Materials/Starter_01.mat");
                if (baseStarter01Mat != null)
                {
                    m3 = new Material(baseStarter01Mat);
                    m3.name = "M_Alita_Berserker";
                    if (m3.HasProperty("_DirtAmount")) m3.SetFloat("_DirtAmount", 0.0f);
                    if (m3.HasProperty("_DarkAmount")) m3.SetFloat("_DarkAmount", 0.95f);
                    if (m3.HasProperty("_EyelinerAmount")) m3.SetFloat("_EyelinerAmount", 0.95f);
                    AssetDatabase.CreateAsset(m3, p3);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // 1. STATE 1: BROKEN SCRAPYARD STATE
        // ─────────────────────────────────────────────────────────────────────
        public static GameObject BuildState1_BrokenScrapyard()
        {
            GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SidekickHuman02);
            if (basePrefab == null)
            {
                Debug.LogError("[AlitaSidekick] Missing Sidekick base: " + SidekickHuman02);
                return null;
            }

            GameObject instance = Object.Instantiate(basePrefab);
            instance.name = "Alita_01_Scrapyard_Broken";

            // Enlarge eyes for iconic Alita anime/manga look
            TuneAlitaEyes(instance, 35f);

            // Assign permanent Scrapyard material
            Material scrapMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/M_Alita_Scrapyard.mat");
            if (scrapMat != null)
            {
                var smr = instance.GetComponentInChildren<SkinnedMeshRenderer>();
                if (smr != null) smr.sharedMaterial = scrapMat;
            }

            // Hide/collapse lower body and left arm for severed scrapyard look
            Transform hips = FindBone(instance.transform, "Hips");
            Transform leftArm = FindBone(instance.transform, "UpperArm_L");
            Transform rightArm = FindBone(instance.transform, "UpperArm_R");
            Transform rightForearm = FindBone(instance.transform, "LowerArm_R");
            Transform head = FindBone(instance.transform, "Head");

            // Sever legs below hips using exact Sidekick bone names
            Transform thighL = FindBone(instance.transform, "thigh_l") ?? FindBone(hips, "Leg_L");
            Transform thighR = FindBone(instance.transform, "thigh_r") ?? FindBone(hips, "Leg_R");
            Transform thighTwistL = FindBone(instance.transform, "thigh_twist_01_l");
            Transform thighTwistR = FindBone(instance.transform, "thigh_twist_01_r");
            if (thighL != null) thighL.localScale = Vector3.zero;
            if (thighR != null) thighR.localScale = Vector3.zero;
            if (thighTwistL != null) thighTwistL.localScale = Vector3.zero;
            if (thighTwistR != null) thighTwistR.localScale = Vector3.zero;

            // Sever left arm at shoulder socket
            if (leftArm != null)
            {
                leftArm.localScale = Vector3.zero;
            }

            // Gently tilt head and pose right arm limp in scrap
            if (head != null)
            {
                head.localRotation = Quaternion.Euler(8f, 18f, -12f);
            }
            if (rightArm != null)
            {
                rightArm.localRotation = Quaternion.Euler(25f, -15f, -30f);
            }
            if (rightForearm != null)
            {
                rightForearm.localRotation = Quaternion.Euler(15f, 20f, -10f);
            }

            // Attach signature dark bob hair (SM_Chr_Attach_Hair_03)
            Transform headAttach = FindBone(instance.transform, "headAttach") ?? head;
            if (headAttach != null)
            {
                string hairPath = "Assets/PolygonCyberCity/Prefabs/Characters/Attachments/SM_Chr_Attach_Hair_03.prefab";
                GameObject hairPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(hairPath);
                if (hairPrefab != null)
                {
                    GameObject hairGo = Object.Instantiate(hairPrefab, headAttach);
                    hairGo.name = "Alita_Hair_Bob";
                    hairGo.transform.localPosition = new Vector3(0f, -0.015f, -0.01f);
                    hairGo.transform.localRotation = Quaternion.identity;
                    hairGo.transform.localScale = Vector3.one;

                    var hairRend = hairGo.GetComponent<MeshRenderer>();
                    Material hairMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/PolygonCyberCity/Materials/Characters/PolygonCyberCity_01_A.mat");
                    if (hairMat != null && hairRend != null) hairRend.sharedMaterial = hairMat;
                }
            }

            // Attach Exposed Chrome Spine extending downwards from severed waist
            Transform chest = FindBone(instance.transform, "Spine_02") ?? instance.transform;
            BuildExposedCyberneticSpine(hips != null ? hips : chest);

            // Attach Trailing hydraulic/electrical cables
            AttachTrailingWires(hips != null ? hips : chest);

            // Attach Beating URM Antimatter Heart Core inside chest cavity
            GameObject coreGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            coreGo.name = "URM_Antimatter_Core";
            coreGo.transform.SetParent(chest, false);
            coreGo.transform.localPosition = new Vector3(0f, 0.08f, 0.07f);
            coreGo.transform.localScale = new Vector3(0.075f, 0.075f, 0.075f);
            Object.DestroyImmediate(coreGo.GetComponent<Collider>());

            // Outer Magnetic Housing Collar
            GameObject cageGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cageGo.name = "Core_Magnetic_Collar";
            cageGo.transform.SetParent(coreGo.transform, false);
            cageGo.transform.localPosition = Vector3.zero;
            cageGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            cageGo.transform.localScale = new Vector3(1.4f, 0.12f, 1.4f);
            Object.DestroyImmediate(cageGo.GetComponent<Collider>());

            var cageMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/M_Cybernetic_Spine_Chrome.mat");
            if (cageMat == null)
            {
                cageMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                cageMat.name = "M_Cybernetic_Spine_Chrome";
                cageMat.color = new Color(0.85f, 0.88f, 0.92f);
                cageMat.SetFloat("_Metallic", 0.95f);
                cageMat.SetFloat("_Smoothness", 0.90f);
                AssetDatabase.CreateAsset(cageMat, MaterialsFolder + "/M_Cybernetic_Spine_Chrome.mat");
            }
            cageGo.GetComponent<MeshRenderer>().sharedMaterial = cageMat;

            // Glowing Pulsating Cyan Core
            Color cyanGlow = new Color(0.0f, 0.95f, 1.0f);
            var coreMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/M_URM_Core_Glow.mat");
            if (coreMat == null)
            {
                coreMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                coreMat.name = "M_URM_Core_Glow";
                coreMat.color = cyanGlow;
                coreMat.EnableKeyword("_EMISSION");
                coreMat.SetColor("_EmissionColor", cyanGlow * 4.5f);
                AssetDatabase.CreateAsset(coreMat, MaterialsFolder + "/M_URM_Core_Glow.mat");
            }
            var coreRend = coreGo.GetComponent<MeshRenderer>();
            coreRend.sharedMaterial = coreMat;

            var coreLight = coreGo.AddComponent<Light>();
            coreLight.type = LightType.Point;
            coreLight.color = cyanGlow;
            coreLight.range = 3.5f;
            coreLight.intensity = 2.8f;

            // Electric sparks at severed left shoulder socket
            Transform leftShoulder = FindBone(instance.transform, "Shoulder_L") ?? FindBone(instance.transform, "Clavicle_L") ?? chest;
            ParticleSystem sparkPs = CreateElectricSparkEmitter(leftShoulder, Vector3.zero);

            // AlitaBrokenCyborg controller for 55 BPM heartbeat audio and pulse
            var controller = instance.AddComponent<AlitaBrokenCyborg>();
            controller.coreRenderer = coreRend;
            controller.coreLight = coreLight;
            controller.coreColor = cyanGlow;
            controller.baseCoreIntensity = 2.5f;
            if (sparkPs != null) controller.severedCableSparks = new ParticleSystem[] { sparkPs };

            // Save clean independent Prefabs
            string savePath = $"{PrefabsFolder}/Alita_01_Scrapyard_Broken.prefab";
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(instance, savePath);
            PrefabUtility.SaveAsPrefabAsset(instance, "Assets/Prefabs/Alita_Broken_Cyborg.prefab");
            Object.DestroyImmediate(instance);

            Debug.Log("[AlitaSidekick] Saved State 1 to " + savePath + " and Assets/Prefabs/Alita_Broken_Cyborg.prefab");
            return savedPrefab;
        }

        // ─────────────────────────────────────────────────────────────────────
        // 2. STATE 2: NORMAL CIVILIAN DOLL BODY
        // ─────────────────────────────────────────────────────────────────────
        public static GameObject BuildState2_NormalDollBody()
        {
            GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SidekickStarter02);
            if (basePrefab == null) basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SidekickHuman02);

            GameObject instance = Object.Instantiate(basePrefab);
            instance.name = "Alita_02_Normal_DollBody";

            // Enlarge eyes for curious, innocent doll expression
            TuneAlitaEyes(instance, 55f);

            // Assign permanent Doll Body material
            Material dollMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/M_Alita_DollBody.mat");
            if (dollMat != null)
            {
                var smr = instance.GetComponentInChildren<SkinnedMeshRenderer>();
                if (smr != null) smr.sharedMaterial = dollMat;
            }

            // Sample gentle standing idle animation pose
            SamplePose(instance, FeminineIdleAnimPath, 0.4f);

            // Ensure natural breathing idle animation controller is attached
            AttachLocomotionAnimator(instance);

            // Save Prefab
            string savePath = $"{PrefabsFolder}/Alita_02_Normal_DollBody.prefab";
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(instance, savePath);
            Object.DestroyImmediate(instance);

            Debug.Log("[AlitaSidekick] Saved State 2: " + savePath);
            return savedPrefab;
        }

        // ─────────────────────────────────────────────────────────────────────
        // 3. STATE 3: FINAL NANO BODY ARMOUR (URM BERSERKER)
        // ─────────────────────────────────────────────────────────────────────
        public static GameObject BuildState3_NanoBerserker()
        {
            GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SidekickStarter01);
            if (basePrefab == null) basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SidekickHuman02);

            GameObject instance = Object.Instantiate(basePrefab);
            instance.name = "Alita_03_Final_NanoBerserker";

            // Sharp, focused warrior gaze
            TuneAlitaEyes(instance, 45f);

            // Assign permanent Berserker material
            Material berserkerMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/M_Alita_Berserker.mat");
            if (berserkerMat != null)
            {
                var smr = instance.GetComponentInChildren<SkinnedMeshRenderer>();
                if (smr != null) smr.sharedMaterial = berserkerMat;
            }

            // Add Alita's iconic blood-red warrior stripes under eyes
            AddWarriorBloodEyeStripes(instance);

            // Add Damascus Blade to Right Hand
            Transform rightHand = FindBone(instance.transform, "Hand_R");
            if (rightHand != null)
            {
                AttachDamascusBlade(rightHand);
            }

            // Sample combat sword guard pose
            SamplePose(instance, SwordIdleAnimPath, 0.2f);

            // Attach combat animator
            AttachLocomotionAnimator(instance);

            // Save Prefab
            string savePath = $"{PrefabsFolder}/Alita_03_Final_NanoBerserker.prefab";
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(instance, savePath);
            Object.DestroyImmediate(instance);

            Debug.Log("[AlitaSidekick] Saved State 3: " + savePath);
            return savedPrefab;
        }

        // ─────────────────────────────────────────────────────────────────────
        // SHOWCASE SCENE BUILDER
        // ─────────────────────────────────────────────────────────────────────
        public static void BuildShowcaseScene()
        {
            var newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Ambient & Sky
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.08f, 0.10f, 0.16f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.04f, 0.05f, 0.09f);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.008f;

            // Key Directional Light (Zalem sunbeam)
            GameObject lightGo = new GameObject("Key_Light_ZalemSunbeam");
            Light dirLight = lightGo.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.color = new Color(0.85f, 0.92f, 1.0f);
            dirLight.intensity = 1.35f;
            dirLight.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(45f, -35f, 0f);

            // Neon Rim Light (Warm amber/magenta rim)
            GameObject rimGo = new GameObject("Rim_Light_IronCity");
            Light rimLight = rimGo.AddComponent<Light>();
            rimLight.type = LightType.Directional;
            rimLight.color = new Color(1.0f, 0.35f, 0.55f);
            rimLight.intensity = 0.55f;
            rimLight.shadows = LightShadows.None;
            rimGo.transform.rotation = Quaternion.Euler(20f, 145f, 0f);

            // Post-Processing
            GameObject volumeGo = new GameObject("Showcase_PostProcess_Volume");
            Volume volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            var volProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/PolygonCyberCity/CyberCity_VolumeProfile.asset")
                          ?? AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Timelines/Alita_Cutscene_PostProcess_Profile.asset");
            if (volProfile != null) volume.profile = volProfile;

            // Environment Root
            GameObject envRoot = new GameObject("Showcase_Environment");

            // Main Exhibition Floor
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Exhibition_Floor";
            floor.transform.SetParent(envRoot.transform);
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(36f, 1f, 24f);

            Material floorMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/PolygonCyberCity/Materials/Floor_01.mat")
                             ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/PolygonCyberCity/Materials/Floor_02.mat");
            if (floorMat != null) floor.GetComponent<MeshRenderer>().sharedMaterial = floorMat;

            // Add background skyline buildings
            string[] blds = new string[] { "SM_Bld_Background_Building_01", "SM_Bld_Background_Building_02", "SM_Bld_Background_Building_03" };
            for (int b = 0; b < 3; b++)
            {
                var bp = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/PolygonCyberCity/Prefabs/Buildings/{blds[b]}.prefab");
                if (bp != null)
                {
                    var bg = (GameObject)PrefabUtility.InstantiatePrefab(bp, envRoot.transform);
                    bg.transform.position = new Vector3((b - 1) * 22f, 0f, 25f);
                    bg.transform.localScale = Vector3.one * 1.2f;
                }
            }

            // 3 Themed Pedestals
            // Pedestal 1: Scrapyard Broken Alita (Left: X = -5.0f)
            BuildScrapyardPedestal(envRoot.transform, new Vector3(-5.0f, 0f, 0f));

            // Pedestal 2: Normal Civilian Doll Body (Center: X = 0f)
            BuildCivilianPedestal(envRoot.transform, new Vector3(0f, 0f, 0f));

            // Pedestal 3: Final Berserker Nano Armour (Right: X = 5.0f)
            BuildBerserkerPedestal(envRoot.transform, new Vector3(5.0f, 0f, 0f));

            // Main Camera setup
            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            Camera cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 45f;
            cam.nearClipPlane = 0.2f;
            cam.farClipPlane = 150f;
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = new Vector3(0f, 2.0f, -9.0f);
            camGo.transform.rotation = Quaternion.Euler(7f, 0f, 0f);
            camGo.AddComponent<CyberSpectatorCamera>();

            // Save Scene
            EditorSceneManager.SaveScene(newScene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[AlitaSidekick] Showcase Scene saved at: {ScenePath}");
        }

        private static void BuildScrapyardPedestal(Transform parent, Vector3 center)
        {
            GameObject pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.name = "Pedestal_01_Scrapyard";
            pedestal.transform.SetParent(parent);
            pedestal.transform.position = center + new Vector3(0f, 0.25f, 0f);
            pedestal.transform.localScale = new Vector3(3.8f, 0.25f, 3.8f);

            var pedMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            pedMat.color = new Color(0.18f, 0.16f, 0.14f);
            pedestal.GetComponent<MeshRenderer>().sharedMaterial = pedMat;

            // Scrap Debris pile on pedestal
            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Props/SM_Prop_Robot_Posed_Pile_01.prefab",
                center + new Vector3(0f, 0.5f, 0.15f), Quaternion.Euler(0f, 45f, 0f), new Vector3(0.70f, 0.70f, 0.70f), parent);

            // Spawn Alita 01
            GameObject prefab1 = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsFolder}/Alita_01_Scrapyard_Broken.prefab");
            if (prefab1 != null)
            {
                GameObject alita1 = (GameObject)PrefabUtility.InstantiatePrefab(prefab1, parent);
                alita1.transform.position = center + new Vector3(0f, 0.88f, -0.05f);
                alita1.transform.rotation = Quaternion.Euler(22f, -22f, -8f); // Reclined resting on scrap
            }

            // Holographic Pedestal Label
            CreateHologramLabel(parent, center + new Vector3(0f, 0.65f, -1.8f), "I. SCRAPYARD CORE (URM HEART)");

            // Pedestal Point Light (Cool Cyan / Scrapyard Glow)
            GameObject lightGo = new GameObject("Light_Scrapyard_Glow");
            lightGo.transform.SetParent(parent);
            lightGo.transform.position = center + new Vector3(0f, 1.5f, -1.2f);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(0f, 0.88f, 1f);
            l.range = 3.5f;
            l.intensity = 1.8f;
        }

        private static void BuildCivilianPedestal(Transform parent, Vector3 center)
        {
            GameObject pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.name = "Pedestal_02_DollBody";
            pedestal.transform.SetParent(parent);
            pedestal.transform.position = center + new Vector3(0f, 0.25f, 0f);
            pedestal.transform.localScale = new Vector3(3.8f, 0.25f, 3.8f);

            var pedMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            pedMat.color = new Color(0.85f, 0.85f, 0.88f); // Porcelain white
            pedMat.SetFloat("_Smoothness", 0.75f);
            pedestal.GetComponent<MeshRenderer>().sharedMaterial = pedMat;

            // Spawn Alita 02
            GameObject prefab2 = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsFolder}/Alita_02_Normal_DollBody.prefab");
            if (prefab2 != null)
            {
                GameObject alita2 = (GameObject)PrefabUtility.InstantiatePrefab(prefab2, parent);
                alita2.transform.position = center + new Vector3(0f, 0.5f, 0f);
                alita2.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // Face camera
            }

            // Holographic Pedestal Label
            CreateHologramLabel(parent, center + new Vector3(0f, 0.65f, -1.8f), "II. CIVILIAN DOLL BODY (IDO)");

            // Warm golden ambient fill light for civilian/human feel
            GameObject lightGo = new GameObject("Light_DollBody_Warmth");
            lightGo.transform.SetParent(parent);
            lightGo.transform.position = center + new Vector3(0f, 1.8f, -1.2f);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1.0f, 0.88f, 0.65f);
            l.range = 3.5f;
            l.intensity = 1.6f;
        }

        private static void BuildBerserkerPedestal(Transform parent, Vector3 center)
        {
            GameObject pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.name = "Pedestal_03_Berserker";
            pedestal.transform.SetParent(parent);
            pedestal.transform.position = center + new Vector3(0f, 0.25f, 0f);
            pedestal.transform.localScale = new Vector3(3.8f, 0.25f, 3.8f);

            var pedMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            pedMat.color = new Color(0.10f, 0.12f, 0.16f); // Obsidian nanotech
            pedMat.SetFloat("_Metallic", 0.95f);
            pedMat.SetFloat("_Smoothness", 0.90f);
            pedMat.EnableKeyword("_EMISSION");
            pedMat.SetColor("_EmissionColor", new Color(0f, 0.75f, 1f) * 0.4f);
            pedestal.GetComponent<MeshRenderer>().sharedMaterial = pedMat;

            // Spawn Alita 03
            GameObject prefab3 = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsFolder}/Alita_03_Final_NanoBerserker.prefab");
            if (prefab3 != null)
            {
                GameObject alita3 = (GameObject)PrefabUtility.InstantiatePrefab(prefab3, parent);
                alita3.transform.position = center + new Vector3(0f, 0.5f, 0f);
                alita3.transform.rotation = Quaternion.Euler(0f, 168f, 0f); // Hero combat angle
            }

            // Holographic Pedestal Label
            CreateHologramLabel(parent, center + new Vector3(0f, 0.65f, -1.8f), "III. BERSERKER (NANO ARMOUR)");

            // Intense Cyan Plasma spot/point light
            GameObject lightGo = new GameObject("Light_Berserker_Plasma");
            lightGo.transform.SetParent(parent);
            lightGo.transform.position = center + new Vector3(0f, 2.0f, -1.2f);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(0f, 1.0f, 0.92f);
            l.range = 4.0f;
            l.intensity = 2.4f;
        }

        private static void CreateHologramLabel(Transform parent, Vector3 pos, string text)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            quad.name = "Holo_Plate_" + text.Split(' ')[0];
            quad.transform.SetParent(parent);
            quad.transform.position = pos;
            quad.transform.localScale = new Vector3(2.4f, 0.12f, 0.04f);
            quad.transform.rotation = Quaternion.Euler(25f, 0f, 0f);

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.color = new Color(0f, 0.85f, 1f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(0f, 0.85f, 1f) * 1.8f);
            quad.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Object.DestroyImmediate(quad.GetComponent<Collider>());
        }

        // ─────────────────────────────────────────────────────────────────────
        // UTILITIES & HELPER METHODS
        // ─────────────────────────────────────────────────────────────────────
        private static void TuneAlitaEyes(GameObject go, float wideAmount)
        {
            var smr = go.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr == null || smr.sharedMesh == null) return;
            var mesh = smr.sharedMesh;

            int leftIdx = mesh.GetBlendShapeIndex("MESHBlends.eyeWideUpperLeft");
            int rightIdx = mesh.GetBlendShapeIndex("MESHBlends.eyeWideUpperRight");
            if (leftIdx >= 0) smr.SetBlendShapeWeight(leftIdx, wideAmount);
            if (rightIdx >= 0) smr.SetBlendShapeWeight(rightIdx, wideAmount);
        }

        private static void SamplePose(GameObject go, string animFbxPath, float time)
        {
            var objs = AssetDatabase.LoadAllAssetsAtPath(animFbxPath);
            AnimationClip targetClip = null;
            foreach (var o in objs)
            {
                if (o is AnimationClip c && !c.name.Contains("__preview__"))
                {
                    targetClip = c;
                    break;
                }
            }

            if (targetClip != null)
            {
                targetClip.SampleAnimation(go, time);
            }
        }

        private static void AddWarriorBloodEyeStripes(GameObject go)
        {
            // Blood-red warrior warpaint stripes under Alita's eyes (her signature Damascus combat mark)
            Transform head = FindBone(go.transform, "Head");
            if (head == null) return;

            GameObject warpaint = new GameObject("Alita_Blood_EyeStripes");
            warpaint.transform.SetParent(head, false);
            warpaint.transform.localPosition = new Vector3(0f, 0.055f, 0.082f);
            warpaint.transform.localRotation = Quaternion.identity;

            // Left stripe
            GameObject stripeL = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stripeL.name = "Stripe_L";
            stripeL.transform.SetParent(warpaint.transform, false);
            stripeL.transform.localPosition = new Vector3(-0.035f, -0.015f, 0.01f);
            stripeL.transform.localScale = new Vector3(0.010f, 0.035f, 0.005f);
            stripeL.transform.localRotation = Quaternion.Euler(0f, -12f, 15f);
            Object.DestroyImmediate(stripeL.GetComponent<Collider>());

            // Right stripe
            GameObject stripeR = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stripeR.name = "Stripe_R";
            stripeR.transform.SetParent(warpaint.transform, false);
            stripeR.transform.localPosition = new Vector3(0.035f, -0.015f, 0.01f);
            stripeR.transform.localScale = new Vector3(0.010f, 0.035f, 0.005f);
            stripeR.transform.localRotation = Quaternion.Euler(0f, 12f, -15f);
            Object.DestroyImmediate(stripeR.GetComponent<Collider>());

            var bloodMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            bloodMat.color = new Color(0.68f, 0.04f, 0.04f); // Rich crimson dried blood
            bloodMat.SetFloat("_Smoothness", 0.45f);

            stripeL.GetComponent<MeshRenderer>().sharedMaterial = bloodMat;
            stripeR.GetComponent<MeshRenderer>().sharedMaterial = bloodMat;
        }

        private static void AttachDamascusBlade(Transform handBone)
        {
            GameObject bladePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DamascusBladePrefab);
            if (bladePrefab == null) return;

            GameObject bladeInstance = (GameObject)PrefabUtility.InstantiatePrefab(bladePrefab, handBone);
            bladeInstance.name = "Damascus_Blade";
            bladeInstance.transform.localPosition = new Vector3(0.02f, 0.05f, 0.02f);
            bladeInstance.transform.localRotation = Quaternion.Euler(15f, -85f, -90f);
            bladeInstance.transform.localScale = Vector3.one * 0.95f;

            // Emissive cyan plasma cutting edge
            Material plasmaEdge = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            Color cyan = new Color(0f, 0.92f, 1.0f);
            plasmaEdge.color = Color.white;
            plasmaEdge.EnableKeyword("_EMISSION");
            plasmaEdge.SetColor("_EmissionColor", cyan * 2.8f);

            foreach (var mr in bladeInstance.GetComponentsInChildren<MeshRenderer>())
            {
                var mats = mr.sharedMaterials;
                if (mats.Length > 1) mats[1] = plasmaEdge;
                mr.sharedMaterials = mats;
            }
        }

        private static void AttachLocomotionAnimator(GameObject go)
        {
            var anim = go.GetComponentInChildren<Animator>();
            if (anim == null) anim = go.AddComponent<Animator>();

            var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/PolygonCyberCity/Animations/AC_CyberNPC.controller")
                    ?? AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/SyntyCombined/AC_SyntyCombined.controller");
            if (ctrl != null) anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
        }

        private static void BuildExposedCyberneticSpine(Transform parentBone)
        {
            GameObject spineContainer = new GameObject("Exposed_Mechanical_Spine");
            spineContainer.transform.SetParent(parentBone, false);
            spineContainer.transform.localPosition = new Vector3(0f, -0.05f, -0.04f);

            var chromeMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_Cybernetic_Spine_Chrome.mat");
            if (chromeMat == null)
            {
                chromeMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                chromeMat.color = new Color(0.72f, 0.75f, 0.80f);
                chromeMat.SetFloat("_Metallic", 0.96f);
                chromeMat.SetFloat("_Smoothness", 0.92f);
            }

            int vertebraeCount = 6;
            for (int i = 0; i < vertebraeCount; i++)
            {
                GameObject v = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                v.name = $"Vertebra_{i + 1}";
                v.transform.SetParent(spineContainer.transform, false);
                v.transform.localPosition = new Vector3(0f, -i * 0.055f, 0f);
                v.transform.localScale = new Vector3(0.042f, 0.022f, 0.042f);
                v.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                Object.DestroyImmediate(v.GetComponent<Collider>());
                v.GetComponent<MeshRenderer>().sharedMaterial = chromeMat;
            }
        }

        private static void AttachTrailingWires(Transform parentBone)
        {
            GameObject cableRoot = new GameObject("Trailing_Cable_Bundles");
            cableRoot.transform.SetParent(parentBone, false);
            cableRoot.transform.localPosition = new Vector3(0f, -0.15f, -0.04f);

            var rubberMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            rubberMat.color = new Color(0.08f, 0.09f, 0.11f);
            rubberMat.SetFloat("_Smoothness", 0.35f);

            for (int i = 0; i < 4; i++)
            {
                GameObject cable = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cable.name = $"Cable_Strand_{i + 1}";
                cable.transform.SetParent(cableRoot.transform, false);
                cable.transform.localPosition = new Vector3((i - 1.5f) * 0.035f, -i * 0.04f, 0f);
                cable.transform.localScale = new Vector3(0.015f, 0.14f, 0.015f);
                cable.transform.localRotation = Quaternion.Euler(20f + i * 5f, 0f, (i - 1.5f) * 15f);
                Object.DestroyImmediate(cable.GetComponent<Collider>());
                cable.GetComponent<MeshRenderer>().sharedMaterial = rubberMat;
            }
        }

        private static ParticleSystem CreateElectricSparkEmitter(Transform parent, Vector3 localPos)
        {
            GameObject psGo = new GameObject("Severed_Cable_Sparks");
            psGo.transform.SetParent(parent, false);
            psGo.transform.localPosition = localPos;

            var ps = psGo.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 0.25f;
            main.startSpeed = 1.8f;
            main.startSize = 0.04f;
            main.startColor = new Color(0.1f, 0.9f, 1.0f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 25;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 2, 5, 4, 0.75f) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.04f;

            var renderer = psGo.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.15f;
            renderer.lengthScale = 1.4f;

            var sparkMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/PolygonCyberCity/Materials/FX/FX_Streaks.mat")
                        ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_Cybernetic_Spine_Chrome.mat");
            if (sparkMat != null) renderer.sharedMaterial = sparkMat;

            return ps;
        }

        private static GameObject SpawnPropSafe(string path, Vector3 pos, Quaternion rot, Vector3 scale, Transform parent)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            go.transform.position = pos;
            go.transform.rotation = rot;
            go.transform.localScale = scale;
            return go;
        }

        private static Transform FindBone(Transform root, string boneName)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.Equals(boneName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return t;
                }
            }
            return null;
        }
    }
}
#endif
