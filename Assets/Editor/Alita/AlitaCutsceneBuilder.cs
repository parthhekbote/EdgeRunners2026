#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;
using Alita;
using Synty.SidekickCharacters.Editor;

namespace Alita.Editor
{
    public static class AlitaCutsceneBuilder
    {
        public const string CutsceneScenePath = "Assets/Scenes/Alita_Discovery_Cutscene.unity";
        public const string TimelineDir = "Assets/Timelines";
        public const string TimelinePath = "Assets/Timelines/Alita_Discovery_Timeline.playable";

        public const string DoctorPrefabPath = "Assets/Characters/Doctor/Doctor.prefab";
        public const string DoctorFallbackPrefabPath = "Assets/Prefabs/Doctor.prefab";
        public const string AlitaPrefabPath = "Assets/Prefabs/Alita_Broken_Cyborg.prefab";

        public static readonly Vector3 DefaultAlitaPos = new Vector3(33.95f, 1.63f, -50.29f);
        public static readonly Quaternion DefaultAlitaRot = Quaternion.Euler(332.04f, 166.88f, 17.33f);
        public static readonly Vector3 DefaultDoctorEntrancePos = new Vector3(32.75f, 2.20f, -64.29f);
        public static readonly Vector3 DefaultDoctorNoticePos = new Vector3(33.15f, 2.20f, -55.79f);
        public static readonly Vector3 DefaultDoctorAlitaPos = new Vector3(33.30f, 2.20f, -51.04f);

        [MenuItem("Tools/Alita Battle Angel/Generate Complete Discovery Cutscene", false, 1)]
        [MenuItem("Tools/Cutscenes/Generate Alita Discovery Cutscene", false, 2)]
        public static void GenerateCutsceneScene()
        {
            Debug.Log("[AlitaCutscene] Starting complete generation of Alita Discovery Cutscene...");

            Directory.CreateDirectory("Assets/Scenes");
            Directory.CreateDirectory(TimelineDir);

            // 1. Create a clean scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 2. Build the Scrapyard Environment
            GameObject scrapRoot = BuildScrapyardEnvironment();

            // 3. Spawn Alita deeper in the scrapyard
            GameObject alitaGo = SpawnAlitaCyborg();

            // 4. Spawn Doctor at the scrapyard entrance
            GameObject doctorGo = SpawnDoctor();

            // 5. Build Cinemachine Cameras & Main Camera
            GameObject mainCamGo;
            List<CinemachineCamera> cmCameras = BuildCinemachineRig(out mainCamGo, doctorGo.transform, alitaGo.transform);

            // 6. Build Cinematic Lighting, Volumetric Haze & FX
            Light sunLight;
            Light moundLight;
            ParticleSystem dustFX;
            BuildLightingAndAtmosphere(alitaGo.transform.position, out sunLight, out moundLight, out dustFX);

            // 7. Create Timeline Asset with Cinemachine, Doctor, and Marker Tracks
            TimelineAsset timeline = BuildTimelineAsset(cmCameras, doctorGo, alitaGo);

            // 8. Setup PlayableDirector and Cutscene Controller
            GameObject cutsceneManager = new GameObject("Cutscene_Manager");
            PlayableDirector director = cutsceneManager.AddComponent<PlayableDirector>();
            director.playableAsset = timeline;
            director.playOnAwake = true;
            director.timeUpdateMode = DirectorUpdateMode.GameTime;

            var controller = cutsceneManager.AddComponent<AlitaCutsceneController>();
            controller.doctorActor = doctorGo;
            controller.alitaActor = alitaGo.GetComponent<AlitaBrokenCyborg>();
            controller.timelineDirector = director;
            controller.cutsceneCamera = mainCamGo.GetComponent<Camera>();
            controller.cinemachineBrain = mainCamGo.GetComponent<CinemachineBrain>();
            controller.directCameraControl = true;
            controller.showSubtitles = true;
            controller.showCinematicBars = true;
            controller.doctorEntrancePos = DefaultDoctorEntrancePos;
            controller.doctorNoticePos = DefaultDoctorNoticePos;
            controller.doctorAlitaPos = DefaultDoctorAlitaPos;
            controller.zalemSunbeamLight = sunLight;
            controller.doctorKeyLight = doctorGo.GetComponentInChildren<Light>();
            controller.alitaFocusLight = moundLight;
            controller.environmentalDustFX = dustFX;

            // Wire Carry Rig on Doctor
            var carryRig = doctorGo.GetComponent<IdoCarryAlitaRig>() ?? doctorGo.AddComponent<IdoCarryAlitaRig>();
            carryRig.doctorActor = doctorGo;
            carryRig.alitaCyborg = alitaGo;
            carryRig.localCarryOffset = new Vector3(0.06f, -0.14f, 0.36f);
            carryRig.localCarryEuler = new Vector3(-123f, -6f, 117f);
            carryRig.InitializeRig();
            controller.carryRig = carryRig;

            // Wire Weather Storm System
            var storm = UnityEngine.Object.FindAnyObjectByType<ScrapyardStormSystem>();
            if (storm == null)
            {
                var stormGo = new GameObject("Scrapyard_Storm_System");
                storm = stormGo.AddComponent<ScrapyardStormSystem>();
                storm.targetCamera = mainCamGo.GetComponent<Camera>();
            }
            controller.stormSystem = storm;

            // Bind Timeline Tracks
            BindTimelineTracks(director, timeline, mainCamGo.GetComponent<CinemachineBrain>(), doctorGo, cmCameras);

            // 9. Save Scene and Assets
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, CutsceneScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[AlitaCutscene] SUCCESS! Complete Discovery Cutscene generated at: {CutsceneScenePath}");
            Debug.Log($"[AlitaCutscene] Timeline asset generated at: {TimelinePath}");
            Debug.Log("[AlitaCutscene] Cutscene 'Alita_Discovery_Cutscene' is ready! Press PLAY to experience the cutscene!");
        }

        [MenuItem("Tools/Alita Battle Angel/Setup God of War Menu and Storm System", false, 0)]
        public static void SetupGodOfWarMenuAndStormSystem()
        {
            var alita = GameObject.Find("Alita_Broken_Cyborg");
            if (alita == null)
            {
                Debug.LogError("[AlitaCutscene] Alita_Broken_Cyborg not found in active scene!");
                return;
            }

            Vector3 aPos = alita.transform.position;
            Vector3 entrance = new Vector3(aPos.x - 1.20f, aPos.y + 0.57f, aPos.z - 14.00f);
            Vector3 notice   = new Vector3(aPos.x - 0.80f, aPos.y + 0.57f, aPos.z - 5.50f);
            Vector3 kneel    = new Vector3(aPos.x - 0.65f, aPos.y + 0.57f, aPos.z - 0.75f);
            Vector3 exitPos  = new Vector3(aPos.x - 1.20f, aPos.y + 0.57f, aPos.z - 20.00f);

            var doctor = GameObject.Find("Doctor_Actor");
            if (doctor != null)
            {
                Undo.RecordObject(doctor.transform, "Align Doctor");
                doctor.transform.position = entrance;
                doctor.transform.rotation = Quaternion.Euler(0f, 8f, 0f);

                var rig = doctor.GetComponent<IdoCarryAlitaRig>() ?? doctor.AddComponent<IdoCarryAlitaRig>();
                rig.doctorActor = doctor;
                rig.alitaCyborg = alita;
                rig.localCarryOffset = new Vector3(0.06f, -0.14f, 0.36f);
                rig.localCarryEuler = new Vector3(-123f, -6f, 117f);
                rig.InitializeRig();
            }

            // Setup Storm System
            var storm = UnityEngine.Object.FindAnyObjectByType<ScrapyardStormSystem>();
            if (storm == null)
            {
                var stormGo = new GameObject("Scrapyard_Storm_System");
                storm = stormGo.AddComponent<ScrapyardStormSystem>();
            }

            var rainGo = GameObject.Find("FX_CutsceneRain");
            if (rainGo != null) storm.rainParticleSystem = rainGo.GetComponentInChildren<ParticleSystem>();

            var splashGo = GameObject.Find("FX_CutsceneRain_Splashes");
            if (splashGo != null) storm.rainSplashSystem = splashGo.GetComponentInChildren<ParticleSystem>();

            var mainCam = GameObject.Find("MainCamera");
            if (mainCam != null)
            {
                storm.targetCamera = mainCam.GetComponent<Camera>();
            }

            // Cutscene Controller
            var cutsceneManager = GameObject.Find("Cutscene_Manager");
            if (cutsceneManager != null)
            {
                var controller = cutsceneManager.GetComponent<AlitaCutsceneController>();
                if (controller != null)
                {
                    Undo.RecordObject(controller, "Configure Controller");
                    controller.doctorActor = doctor;
                    controller.alitaActor = alita.GetComponent<AlitaBrokenCyborg>();
                    controller.doctorEntrancePos = entrance;
                    controller.doctorNoticePos = notice;
                    controller.doctorAlitaPos = kneel;
                    controller.doctorExitPos = exitPos;
                    controller.stormSystem = storm;
                    if (doctor != null) controller.carryRig = doctor.GetComponent<IdoCarryAlitaRig>();
                    if (mainCam != null) controller.cutsceneCamera = mainCam.GetComponent<Camera>();
                    controller.currentPhase = CutscenePhase.MainMenu;
                }
            }

            EditorSceneManager.MarkSceneDirty(alita.scene);
            EditorSceneManager.SaveScene(alita.scene);
            Debug.Log("[AlitaCutscene] SUCCESS: God of War Seamless Menu & Atmospheric Thunderstorm configured!");
        }

        [MenuItem("Tools/Alita Battle Angel/Stitch Cutscenes 1 and 2 (Setup Seamless Bridge)", false, 5)]
        public static void StitchCutscenes1And2()
        {
            Debug.Log("[AlitaStitch] Starting automated verification & scene stitching...");

            // 1. Ensure scenes in EditorBuildSettings
            string[] requiredScenes = new string[]
            {
                "Assets/Scenes/Alita_Discovery_Cutscene.unity",
                "Assets/Scenes/Alita_Tron_Awakening.unity",
                "Assets/PolygonCyberCity/Scenes/Demo_City.unity"
            };

            var currentBuildScenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool buildSettingsDirty = false;

            foreach (var scPath in requiredScenes)
            {
                if (!File.Exists(scPath))
                {
                    Debug.LogWarning($"[AlitaStitch] Scene file not found at {scPath}!");
                    continue;
                }

                bool alreadyAdded = currentBuildScenes.Any(s => s.path == scPath);
                if (!alreadyAdded)
                {
                    currentBuildScenes.Add(new EditorBuildSettingsScene(scPath, true));
                    buildSettingsDirty = true;
                    Debug.Log($"[AlitaStitch] Added {scPath} to EditorBuildSettings!");
                }
            }

            if (buildSettingsDirty)
            {
                EditorBuildSettings.scenes = currentBuildScenes.ToArray();
            }

            // 2. Configure Alita_Discovery_Cutscene.unity (Cutscene 1)
            string cutscene1Path = "Assets/Scenes/Alita_Discovery_Cutscene.unity";
            if (File.Exists(cutscene1Path))
            {
                var s1 = EditorSceneManager.OpenScene(cutscene1Path, OpenSceneMode.Single);
                var ctrl1 = UnityEngine.Object.FindAnyObjectByType<AlitaCutsceneController>();
                if (ctrl1 != null)
                {
                    Undo.RecordObject(ctrl1, "Stitch Cutscene 1");
                    ctrl1.nextCutsceneSceneName = "Alita_Tron_Awakening";
                    ctrl1.transitionCardDuration = 4.2f;
                    ctrl1.autoAdvanceToNextScene = true;
                    ctrl1.allowSkipCutscene = true;
                    EditorUtility.SetDirty(ctrl1);
                    EditorSceneManager.MarkSceneDirty(s1);
                    EditorSceneManager.SaveScene(s1);
                    Debug.Log("[AlitaStitch] Configured AlitaCutsceneController in Alita_Discovery_Cutscene.unity with seamless bridge to Alita_Tron_Awakening!");
                }
            }

            // 3. Configure Alita_Tron_Awakening.unity (Cutscene 2)
            string cutscene2Path = "Assets/Scenes/Alita_Tron_Awakening.unity";
            if (File.Exists(cutscene2Path))
            {
                var s2 = EditorSceneManager.OpenScene(cutscene2Path, OpenSceneMode.Single);
                var dir2 = UnityEngine.Object.FindAnyObjectByType<AlitaTronAwakeningDirector>();
                if (dir2 != null)
                {
                    Undo.RecordObject(dir2, "Stitch Cutscene 2");
                    dir2.nextGameplaySceneName = "Demo_Interior";
                    dir2.introFadeDuration = 1.2f;
                    dir2.endTransitionCardDuration = 4.5f;
                    dir2.autoAdvanceToGameplay = true;
                    dir2.allowSkipToGameplay = true;
                    EditorUtility.SetDirty(dir2);
                    EditorSceneManager.MarkSceneDirty(s2);
                    EditorSceneManager.SaveScene(s2);
                    Debug.Log("[AlitaStitch] Configured AlitaTronAwakeningDirector in Alita_Tron_Awakening.unity with seamless hand-off to Demo_City!");
                }
            }

            // Return to Cutscene 1 so user can press Play
            if (File.Exists(cutscene1Path))
            {
                EditorSceneManager.OpenScene(cutscene1Path, OpenSceneMode.Single);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[AlitaStitch] SUCCESS! Cutscene 1 (Scrapyard Discovery) and Cutscene 2 (Awakening & Reconstruction) are now seamlessly stitched together!");
        }

        [MenuItem("Tools/Alita Battle Angel/Align Cutscene to Current Alita Position", false, 3)]
        public static void AlignCutsceneToCurrentAlita()
        {
            var alita = GameObject.Find("Alita_Broken_Cyborg");
            if (alita == null)
            {
                Debug.LogError("[AlitaCutscene] Alita_Broken_Cyborg not found in active scene!");
                return;
            }

            Vector3 aPos = alita.transform.position;
            Vector3 entrance = new Vector3(aPos.x - 1.20f, aPos.y + 0.57f, aPos.z - 14.00f);
            Vector3 notice   = new Vector3(aPos.x - 0.80f, aPos.y + 0.57f, aPos.z - 5.50f);
            Vector3 kneel    = new Vector3(aPos.x - 0.65f, aPos.y + 0.57f, aPos.z - 0.75f);

            var doctor = GameObject.Find("Doctor_Actor");
            if (doctor != null)
            {
                Undo.RecordObject(doctor.transform, "Align Doctor");
                doctor.transform.position = entrance;
                doctor.transform.rotation = Quaternion.Euler(0f, 8f, 0f);

                var rig = doctor.GetComponent<IdoCarryAlitaRig>() ?? doctor.AddComponent<IdoCarryAlitaRig>();
                rig.doctorActor = doctor;
                rig.alitaCyborg = alita;
                rig.localCarryOffset = new Vector3(0.06f, -0.14f, 0.36f);
                rig.localCarryEuler = new Vector3(-123f, -6f, 117f);
                rig.InitializeRig();
            }

            var cutsceneManager = GameObject.Find("Cutscene_Manager");
            if (cutsceneManager != null)
            {
                var controller = cutsceneManager.GetComponent<AlitaCutsceneController>();
                if (controller != null)
                {
                    Undo.RecordObject(controller, "Align Cutscene Controller");
                    controller.doctorEntrancePos = entrance;
                    controller.doctorNoticePos = notice;
                    controller.doctorAlitaPos = kneel;
                    if (doctor != null) controller.carryRig = doctor.GetComponent<IdoCarryAlitaRig>();
                }
            }

            var mainCam = GameObject.Find("MainCamera");
            if (mainCam != null)
            {
                Undo.RecordObject(mainCam.transform, "Align MainCamera");
                mainCam.transform.position = entrance + new Vector3(0.8f, 1.6f, -4.0f);
                mainCam.transform.rotation = Quaternion.Euler(10f, -10f, 0f);
            }

            // Align all Cinemachine Cameras
            AlignCMCamera("CM_Opening", entrance + new Vector3(0.8f, 1.6f, -4.0f), Quaternion.Euler(10f, -10f, 0f), 50f);
            AlignCMCamera("CM_Doctor_Walk", entrance + new Vector3(-1.0f, 1.55f, -1.0f), Quaternion.Euler(10f, 18f, 0f), 45f);
            AlignCMCamera("CM_Doctor_Reaction", notice + new Vector3(0.55f, 1.45f, 1.4f), Quaternion.Euler(6f, 195f, 0f), 34f);
            AlignCMCamera("CM_Alita_Reveal", aPos + new Vector3(0.75f, 0.72f, -1.3f), Quaternion.Euler(18f, -42f, 0f), 38f);
            AlignCMCamera("CM_Approach", aPos + new Vector3(-2.2f, 1.27f, -2.5f), Quaternion.Euler(14f, 52f, 0f), 42f);
            AlignCMCamera("CM_Interaction", aPos + new Vector3(-1.15f, 1.02f, -1.6f), Quaternion.Euler(12f, 56f, 0f), 36f);
            AlignCMCamera("CM_Alita_Closeup", aPos + new Vector3(-0.15f, 0.52f, -0.75f), Quaternion.Euler(16f, 18f, 0f), 30f);

            // Align Lights
            AlignLight("Light_Alita_Face_Key", aPos + new Vector3(0.5f, 2.2f, -1.2f), Quaternion.Euler(46f, -22f, 0f));
            AlignLight("Light_Discovery_Focus", aPos + new Vector3(0f, 1.8f, -0.2f), Quaternion.identity);
            AlignLight("Zalem_Sunbeam_Spotlight", aPos + new Vector3(-1.8f, 6.5f, -2.2f), Quaternion.LookRotation((aPos + Vector3.up * 0.3f) - (aPos + new Vector3(-1.8f, 6.5f, -2.2f))));
            AlignLight("Light_Scrapyard_Path_Fill", notice + new Vector3(0f, 3.3f, 0f), Quaternion.identity);

            var dust = GameObject.Find("FX_Scrap_Floating_Dust");
            if (dust != null)
            {
                Undo.RecordObject(dust.transform, "Align Dust FX");
                dust.transform.position = aPos + new Vector3(-1f, 1.5f, -2f);
            }

            EditorSceneManager.MarkSceneDirty(alita.scene);
            EditorSceneManager.SaveScene(alita.scene);
            Debug.Log($"[AlitaCutscene] SUCCESS: Cutscene completely aligned to Alita at {aPos}!");
        }

        private static void AlignCMCamera(string name, Vector3 pos, Quaternion rot, float fov)
        {
            var go = GameObject.Find(name);
            if (go != null)
            {
                Undo.RecordObject(go.transform, "Align CM Cam Transform");
                go.transform.position = pos;
                go.transform.rotation = rot;
                var cm = go.GetComponent<CinemachineCamera>();
                if (cm != null)
                {
                    Undo.RecordObject(cm, "Align CM Cam Lens");
                    cm.Lens.FieldOfView = fov;
                    cm.Lens.FarClipPlane = 450f;
                }
            }
        }

        private static void AlignLight(string name, Vector3 pos, Quaternion rot)
        {
            var go = GameObject.Find(name);
            if (go != null)
            {
                Undo.RecordObject(go.transform, "Align Light");
                go.transform.position = pos;
                if (rot != Quaternion.identity) go.transform.rotation = rot;
            }
        }

        private static GameObject BuildScrapyardEnvironment()
        {
            GameObject scrapRoot = new GameObject("Environment_Scrapyard");

            // -------------------------------------------------------------
            // 1. TERRAIN & FOUNDATION (Expansive 300m x 300m ground + plateaus)
            // -------------------------------------------------------------
            GameObject terrainRoot = new GameObject("Scrapyard_Terrain_Foundation");
            terrainRoot.transform.SetParent(scrapRoot.transform);

            var groundMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/PolygonCyberCity/Materials/Building/Floor_01.mat");

            // Colossal main ground floor
            GameObject mainGround = GameObject.CreatePrimitive(PrimitiveType.Plane);
            mainGround.name = "Scrapyard_Ground_Main";
            mainGround.transform.SetParent(terrainRoot.transform);
            mainGround.transform.position = new Vector3(0f, -0.05f, 0f);
            mainGround.transform.localScale = new Vector3(30f, 1f, 30f); // 300m x 300m vast scrapyard desert
            if (groundMat != null) mainGround.GetComponent<MeshRenderer>().sharedMaterial = groundMat;

            // Tiered stepped scrap plateaus to break up flatness and create crater depth
            CreateTerrainPlateau("Plateau_East", new Vector3(45f, 1.8f, 5f), Quaternion.Euler(0f, 15f, 0f), new Vector3(14f, 1f, 16f), groundMat, terrainRoot.transform);
            CreateTerrainPlateau("Plateau_West", new Vector3(-48f, 2.2f, -10f), Quaternion.Euler(0f, -20f, 0f), new Vector3(14f, 1f, 16f), groundMat, terrainRoot.transform);
            CreateTerrainPlateau("Plateau_North_Backdrop", new Vector3(0f, 2.8f, 48f), Quaternion.identity, new Vector3(18f, 1f, 12f), groundMat, terrainRoot.transform);
            CreateTerrainPlateau("Plateau_South_Approach", new Vector3(0f, 1.2f, -50f), Quaternion.Euler(0f, 5f, 0f), new Vector3(16f, 1f, 14f), groundMat, terrainRoot.transform);

            // -------------------------------------------------------------
            // 2. ZONE 1: DISCOVERY CLEARING & APPROACH PATH (Radius 0m - 12m)
            // -------------------------------------------------------------
            GameObject zone1Root = new GameObject("Zone1_Discovery_Clearing");
            zone1Root.transform.SetParent(scrapRoot.transform);

            // Alita's central discovery mound robot debris (behind & framing, keeping Alita 100% visible)
            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Props/SM_Prop_Robot_Posed_Pile_01.prefab",
                new Vector3(0.2f, 0f, 4.3f), Quaternion.Euler(0f, 40f, 0f), new Vector3(1.25f, 1.25f, 1.25f), zone1Root.transform);

            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Props/SM_Prop_Robot_Posed_Pile_02.prefab",
                new Vector3(-2.2f, 0f, 4.8f), Quaternion.Euler(0f, -45f, 0f), Vector3.one, zone1Root.transform);

            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Props/SM_Prop_Robot_Posed_01.prefab",
                new Vector3(1.6f, 0.05f, 3.2f), Quaternion.Euler(-15f, 70f, 20f), Vector3.one, zone1Root.transform);

            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Props/SM_Prop_Robot_Posed_03.prefab",
                new Vector3(-1.9f, 0.05f, 2.8f), Quaternion.Euler(10f, -50f, -15f), Vector3.one, zone1Root.transform);

            // Scattered robot components around Alita's resting place
            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Props/SM_Prop_Robot_Part_01.prefab",
                new Vector3(0.85f, 0f, 3.9f), Quaternion.Euler(15f, 30f, 45f), Vector3.one, zone1Root.transform);
            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Props/SM_Prop_Robot_Part_02.prefab",
                new Vector3(-1.1f, 0f, 4.1f), Quaternion.Euler(-25f, 65f, 10f), Vector3.one, zone1Root.transform);
            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Props/SM_Prop_Robotic_Needle_Arm_01.prefab",
                new Vector3(1.3f, 0.1f, 2.6f), Quaternion.Euler(30f, -40f, 10f), Vector3.one, zone1Root.transform);

            // Cable bundles trailing into the scrap
            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Props/SM_Prop_Wires_02.prefab",
                new Vector3(0.4f, 0f, 3.8f), Quaternion.Euler(0f, 25f, 0f), Vector3.one, zone1Root.transform);
            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Props/SM_Prop_Wires_12.prefab",
                new Vector3(-0.7f, 0f, 4.0f), Quaternion.Euler(0f, -60f, 0f), Vector3.one, zone1Root.transform);

            // Flanking scrap mounds lining the Doctor's approach path (keeping central walkway open)
            SpawnPropSafe("Assets/Synty/PolygonSciFiWorlds/Prefabs/Props/Scrap/SM_Prop_Scav_Scrap_Pile_01.prefab",
                new Vector3(-4.0f, 0f, -6.5f), Quaternion.Euler(0f, 60f, 0f), Vector3.one, zone1Root.transform);
            SpawnPropSafe("Assets/Synty/PolygonSciFiWorlds/Prefabs/Props/Scrap/SM_Prop_Scav_Scrap_Pile_02.prefab",
                new Vector3(3.5f, 0f, -8.0f), Quaternion.Euler(0f, -30f, 0f), Vector3.one, zone1Root.transform);
            SpawnPropSafe("Assets/Synty/PolygonSciFiWorlds/Prefabs/Props/Scrap/SM_Prop_Scav_Scrap_Pile_03.prefab",
                new Vector3(-3.8f, 0f, 0.2f), Quaternion.Euler(0f, 15f, 0f), new Vector3(1.2f, 1.1f, 1.2f), zone1Root.transform);
            SpawnPropSafe("Assets/Synty/PolygonSciFiWorlds/Prefabs/Props/Scrap/SM_Prop_Scav_Scrap_Pile_04.prefab",
                new Vector3(3.6f, 0f, 1.2f), Quaternion.Euler(0f, -85f, 0f), Vector3.one, zone1Root.transform);

            SpawnPropSafe("Assets/Synty/PolygonSciFiWorlds/Prefabs/Props/Scrap/SM_Prop_Scav_Scrap_21.prefab",
                new Vector3(1.9f, 0f, 3.8f), Quaternion.Euler(15f, -35f, -10f), new Vector3(1.1f, 1.1f, 1.1f), zone1Root.transform);
            SpawnPropSafe("Assets/Synty/PolygonSciFiWorlds/Prefabs/Props/Scrap/SM_Prop_Scav_Scrap_40.prefab",
                new Vector3(-1.9f, 0f, 3.8f), Quaternion.Euler(-10f, 45f, 25f), Vector3.one, zone1Root.transform);

            // -------------------------------------------------------------
            // 3. ZONE 2: MID CANYON WALLS & CRASHED VEHICLES (Radius 14m - 48m)
            // -------------------------------------------------------------
            GameObject zone2Root = new GameObject("Zone2_Canyon_Walls_And_Wrecks");
            zone2Root.transform.SetParent(scrapRoot.transform);

            // Towering mid-range junk piles creating valley walls
            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Env/SM_Env_Junk_Pile_01.prefab",
                new Vector3(-14f, 0f, -12f), Quaternion.Euler(0f, 35f, 0f), new Vector3(2.4f, 2.2f, 2.4f), zone2Root.transform);
            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Env/SM_Env_Junk_Pile_02.prefab",
                new Vector3(15f, 0f, -10f), Quaternion.Euler(0f, -45f, 0f), new Vector3(2.3f, 2.1f, 2.3f), zone2Root.transform);
            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Env/SM_Env_Junk_Pile_03.prefab",
                new Vector3(-16f, 0f, 12f), Quaternion.Euler(0f, 80f, 0f), new Vector3(2.6f, 2.4f, 2.6f), zone2Root.transform);
            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Env/SM_Env_Junk_Pile_04.prefab",
                new Vector3(16f, 0f, 14f), Quaternion.Euler(0f, -115f, 0f), new Vector3(2.5f, 2.3f, 2.5f), zone2Root.transform);
            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Env/SM_Env_Junk_Pile_05.prefab",
                new Vector3(0f, 0f, 18f), Quaternion.Euler(0f, 180f, 0f), new Vector3(3.0f, 2.5f, 3.0f), zone2Root.transform);
            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Env/SM_Env_Junk_Pile_01.prefab",
                new Vector3(-24f, 0f, 2f), Quaternion.Euler(0f, 15f, 0f), new Vector3(2.8f, 2.6f, 2.8f), zone2Root.transform);
            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Env/SM_Env_Junk_Pile_02.prefab",
                new Vector3(25f, 0f, 0f), Quaternion.Euler(0f, -85f, 0f), new Vector3(2.7f, 2.5f, 2.7f), zone2Root.transform);
            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Env/SM_Env_Junk_Pile_03.prefab",
                new Vector3(-18f, 0f, -25f), Quaternion.Euler(0f, 120f, 0f), new Vector3(2.8f, 2.4f, 2.8f), zone2Root.transform);
            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Env/SM_Env_Junk_Pile_04.prefab",
                new Vector3(20f, 0f, -24f), Quaternion.Euler(0f, -60f, 0f), new Vector3(2.7f, 2.3f, 2.7f), zone2Root.transform);

            // Additional junk pile inserts on elevated terraces
            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Env/SM_Env_Junk_Pile_Insert_01.prefab",
                new Vector3(-10f, 1.8f, 15f), Quaternion.Euler(15f, 45f, 0f), new Vector3(2.0f, 2.0f, 2.0f), zone2Root.transform);
            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Env/SM_Env_Junk_Pile_Insert_01.prefab",
                new Vector3(12f, 2.0f, 16f), Quaternion.Euler(-10f, -75f, 0f), new Vector3(2.0f, 2.0f, 2.0f), zone2Root.transform);

            // Crashed and half-buried vehicle carcasses embedded in scrap dunes
            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Vehicles/SM_Veh_Truck_01.prefab",
                new Vector3(-22f, 1.0f, -16f), Quaternion.Euler(22f, 40f, -15f), new Vector3(1.3f, 1.3f, 1.3f), zone2Root.transform);
            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Vehicles/SM_Veh_Food_Truck_01.prefab",
                new Vector3(23f, 0.6f, -12f), Quaternion.Euler(-18f, -110f, 22f), new Vector3(1.2f, 1.2f, 1.2f), zone2Root.transform);
            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Vehicles/SM_Veh_Train_Middle_01.prefab",
                new Vector3(28f, 2.8f, 18f), Quaternion.Euler(12f, -30f, 18f), new Vector3(1.3f, 1.3f, 1.3f), zone2Root.transform);
            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Vehicles/SM_Veh_SUV_01.prefab",
                new Vector3(-25f, 0.8f, 16f), Quaternion.Euler(28f, 125f, -10f), new Vector3(1.2f, 1.2f, 1.2f), zone2Root.transform);
            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Vehicles/SM_Veh_Luxury_Car_01.prefab",
                new Vector3(-15f, 0.4f, -28f), Quaternion.Euler(-15f, 65f, 10f), new Vector3(1.1f, 1.1f, 1.1f), zone2Root.transform);

            // Industrial scrap pipes & hanging droids
            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Props/SM_Prop_Junk_Pipe_01.prefab",
                new Vector3(-14f, 3.5f, 10f), Quaternion.Euler(-35f, 45f, 0f), new Vector3(2.0f, 2.0f, 2.0f), zone2Root.transform);
            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Props/SM_Prop_Junk_Pipe_02.prefab",
                new Vector3(16f, 4.0f, 8f), Quaternion.Euler(30f, -60f, 15f), new Vector3(2.2f, 2.2f, 2.2f), zone2Root.transform);
            SpawnPropSafe("Assets/Synty/PolygonSciFiWorlds/Prefabs/Buildings/SM_Bld_Scav_Refinery_Pipe_01.prefab",
                new Vector3(-22f, 6.0f, -6f), Quaternion.Euler(20f, 85f, -15f), new Vector3(1.8f, 1.8f, 1.8f), zone2Root.transform);
            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Props/SM_Prop_Robot_Posed_Hanging_01.prefab",
                new Vector3(-14.2f, 2.5f, 9.5f), Quaternion.Euler(0f, 65f, 0f), new Vector3(1.2f, 1.2f, 1.2f), zone2Root.transform);

            // -------------------------------------------------------------
            // 4. ZONE 3: COLOSSAL MOUNTAINS & SKYWARD ZALEM SILHOUETTES (Radius 50m - 140m)
            // -------------------------------------------------------------
            GameObject zone3Root = new GameObject("Zone3_Colossal_Mountain_Perimeter");
            zone3Root.transform.SetParent(scrapRoot.transform);

            // 360-degree colossal junk mountain horizons
            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Env/SM_Env_Junk_Background_01.prefab",
                new Vector3(0f, 0f, 75f), Quaternion.identity, new Vector3(4.2f, 3.5f, 4.2f), zone3Root.transform);
            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Env/SM_Env_Junk_Background_01.prefab",
                new Vector3(70f, 0f, 35f), Quaternion.Euler(0f, -60f, 0f), new Vector3(4.0f, 3.6f, 4.0f), zone3Root.transform);
            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Env/SM_Env_Junk_Background_01.prefab",
                new Vector3(-70f, 0f, 35f), Quaternion.Euler(0f, 60f, 0f), new Vector3(4.0f, 3.6f, 4.0f), zone3Root.transform);
            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Env/SM_Env_Junk_Background_01.prefab",
                new Vector3(65f, 0f, -50f), Quaternion.Euler(0f, -135f, 0f), new Vector3(4.0f, 3.4f, 4.0f), zone3Root.transform);
            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Env/SM_Env_Junk_Background_01.prefab",
                new Vector3(-65f, 0f, -50f), Quaternion.Euler(0f, 135f, 0f), new Vector3(4.0f, 3.4f, 4.0f), zone3Root.transform);
            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Env/SM_Env_Junk_Background_01.prefab",
                new Vector3(0f, 0f, -85f), Quaternion.Euler(0f, 180f, 0f), new Vector3(4.2f, 3.5f, 4.2f), zone3Root.transform);

            // Giant scrap mountain peaks (scaled 5.5x - 7.0x, rising 35-50m into the sky)
            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Env/SM_Env_Junk_Pile_05.prefab",
                new Vector3(0f, 0f, 60f), Quaternion.Euler(0f, 180f, 0f), new Vector3(7.0f, 5.0f, 7.0f), zone3Root.transform);
            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Env/SM_Env_Junk_Pile_01.prefab",
                new Vector3(-55f, 0f, 48f), Quaternion.Euler(0f, 30f, 0f), new Vector3(6.5f, 4.8f, 6.5f), zone3Root.transform);
            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Env/SM_Env_Junk_Pile_02.prefab",
                new Vector3(58f, 0f, 48f), Quaternion.Euler(0f, -40f, 0f), new Vector3(6.5f, 4.8f, 6.5f), zone3Root.transform);
            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Env/SM_Env_Junk_Pile_03.prefab",
                new Vector3(-60f, 0f, -30f), Quaternion.Euler(0f, 75f, 0f), new Vector3(6.0f, 4.5f, 6.0f), zone3Root.transform);
            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Env/SM_Env_Junk_Pile_04.prefab",
                new Vector3(62f, 0f, -28f), Quaternion.Euler(0f, -100f, 0f), new Vector3(6.0f, 4.5f, 6.0f), zone3Root.transform);
            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Env/SM_Env_Junk_Pile_05.prefab",
                new Vector3(-35f, 0f, -70f), Quaternion.Euler(0f, 150f, 0f), new Vector3(6.5f, 4.5f, 6.5f), zone3Root.transform);
            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Env/SM_Env_Junk_Pile_01.prefab",
                new Vector3(38f, 0f, -68f), Quaternion.Euler(0f, -145f, 0f), new Vector3(6.5f, 4.5f, 6.5f), zone3Root.transform);

            // Skyward Zalem megastructures and disposal towers (rising 60m - 100m into overcast sky)
            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Buildings/SM_Bld_Background_Building_01.prefab",
                new Vector3(15f, 0f, 120f), Quaternion.Euler(0f, 15f, 0f), new Vector3(2.0f, 2.4f, 2.0f), zone3Root.transform);
            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Buildings/SM_Bld_Background_Building_02.prefab",
                new Vector3(-90f, 0f, 95f), Quaternion.Euler(0f, 45f, 0f), new Vector3(1.8f, 2.2f, 1.8f), zone3Root.transform);
            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Buildings/SM_Bld_Background_Building_03.prefab",
                new Vector3(95f, 0f, 90f), Quaternion.Euler(0f, -40f, 0f), new Vector3(1.8f, 2.2f, 1.8f), zone3Root.transform);
            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Buildings/SM_Bld_Background_Building_04.prefab",
                new Vector3(-100f, 0f, -60f), Quaternion.Euler(0f, 110f, 0f), new Vector3(1.7f, 2.0f, 1.7f), zone3Root.transform);
            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Buildings/SM_Bld_Background_Building_01.prefab",
                new Vector3(100f, 0f, -65f), Quaternion.Euler(0f, -120f, 0f), new Vector3(1.7f, 2.0f, 1.7f), zone3Root.transform);

            SpawnPropSafe("Assets/Synty/PolygonSciFiWorlds/Prefabs/Buildings/SM_Bld_Corp_Comms_Tower_01.prefab",
                new Vector3(-48f, 0f, 90f), Quaternion.Euler(0f, 25f, 0f), new Vector3(2.8f, 3.2f, 2.8f), zone3Root.transform);
            SpawnPropSafe("Assets/Synty/PolygonSciFiWorlds/Prefabs/Buildings/SM_Bld_Corp_Comms_Tower_01.prefab",
                new Vector3(52f, 0f, 85f), Quaternion.Euler(0f, -35f, 0f), new Vector3(2.6f, 3.0f, 2.6f), zone3Root.transform);

            // -------------------------------------------------------------
            // 5. PROCEDURAL DENSE SCRAP DUNES & BURIED WRECKS CARPET
            // -------------------------------------------------------------
            GameObject dunesRoot = new GameObject("Zone_Dense_Scrap_Dunes");
            dunesRoot.transform.SetParent(scrapRoot.transform);
            PopulateDenseScrapyardDunes(dunesRoot.transform, Vector3.zero);

            return scrapRoot;
        }

        private static void PopulateDenseScrapyardDunes(Transform parent, Vector3 centerOffset)
        {
            string[] junkPrefabs = new string[]
            {
                "Assets/PolygonCyberCity/Prefabs/Env/SM_Env_Junk_Pile_01.prefab",
                "Assets/PolygonCyberCity/Prefabs/Env/SM_Env_Junk_Pile_02.prefab",
                "Assets/PolygonCyberCity/Prefabs/Env/SM_Env_Junk_Pile_03.prefab",
                "Assets/PolygonCyberCity/Prefabs/Env/SM_Env_Junk_Pile_04.prefab",
                "Assets/PolygonCyberCity/Prefabs/Env/SM_Env_Junk_Pile_05.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Props/Scrap/SM_Prop_Scav_Scrap_Pile_01.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Props/Scrap/SM_Prop_Scav_Scrap_Pile_02.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Props/Scrap/SM_Prop_Scav_Scrap_Pile_03.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Props/Scrap/SM_Prop_Scav_Scrap_Pile_04.prefab"
            };

            string[] vehicleWrecks = new string[]
            {
                "Assets/PolygonCyberCity/Prefabs/Vehicles/SM_Veh_Truck_01.prefab",
                "Assets/PolygonCyberCity/Prefabs/Vehicles/SM_Veh_Food_Truck_01.prefab",
                "Assets/PolygonCyberCity/Prefabs/Vehicles/SM_Veh_Food_Trailer_01.prefab",
                "Assets/PolygonCyberCity/Prefabs/Vehicles/SM_Veh_Train_Middle_01.prefab",
                "Assets/PolygonCyberCity/Prefabs/Vehicles/SM_Veh_Train_Cockpit_01.prefab",
                "Assets/PolygonCyberCity/Prefabs/Vehicles/SM_Veh_SUV_01.prefab",
                "Assets/PolygonCyberCity/Prefabs/Vehicles/SM_Veh_Luxury_Car_01.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Props/SM_Prop_Container_01.prefab"
            };

            // 1. Concentric undulating scrap dune rings (Radius 16m to 68m)
            float[] rings = new float[] { 18f, 28f, 40f, 55f, 68f };
            int[] counts = new int[] { 8, 12, 14, 16, 18 };

            for (int r = 0; r < rings.Length; r++)
            {
                float radius = rings[r];
                int count = counts[r];
                float angleStep = 360f / count;

                for (int i = 0; i < count; i++)
                {
                    float angleDeg = i * angleStep + (r * 19.5f); // Stagger successive rings
                    float rad = angleDeg * Mathf.Deg2Rad;
                    float x = Mathf.Sin(rad) * radius;
                    float z = Mathf.Cos(rad) * radius;

                    // Protect central discovery clearing and doctor walking corridor
                    if (Mathf.Abs(x) < 4.8f && z < 6.0f && z > -22.0f) continue;

                    Vector3 worldPos = centerOffset + new Vector3(x, 0f, z);
                    Quaternion rot = Quaternion.Euler(0f, (i * 47f) % 360f, 0f);
                    float scaleMul = Mathf.Lerp(2.2f, 4.8f, (radius - 16f) / 52f);
                    Vector3 scale = new Vector3(scaleMul, scaleMul * 0.85f, scaleMul);

                    string chosenPrefab = junkPrefabs[(r * 3 + i) % junkPrefabs.Length];
                    SpawnPropSafe(chosenPrefab, worldPos, rot, scale, parent);

                    // Sporadically embed half-buried vehicle wrecks and containers into every ~3rd mound
                    if ((i + r) % 3 == 0)
                    {
                        string wreckPrefab = vehicleWrecks[(i + r * 2) % vehicleWrecks.Length];
                        Vector3 wreckPos = worldPos + new Vector3(Mathf.Sin(rad + 0.3f) * 2.5f, 0.6f, Mathf.Cos(rad + 0.3f) * 2.5f);
                        Quaternion wreckRot = Quaternion.Euler(
                            Mathf.Sin(i * 1.5f) * 28f,
                            (angleDeg + 90f) % 360f,
                            Mathf.Cos(i * 1.2f) * 22f
                        );
                        Vector3 wreckScale = wreckPrefab.Contains("Container") ? new Vector3(1.3f, 1.3f, 1.3f) : new Vector3(1.15f, 1.15f, 1.15f);
                        SpawnPropSafe(wreckPrefab, wreckPos, wreckRot, wreckScale, parent);
                    }
                }
            }

            // 2. Closed 360-degree colossal mountain perimeter (Radius 85m - 95m)
            int mountainCount = 12;
            float mountainAngleStep = 360f / mountainCount;
            for (int m = 0; m < mountainCount; m++)
            {
                float angleDeg = m * mountainAngleStep;
                float rad = angleDeg * Mathf.Deg2Rad;
                float dist = 88f + (m % 2 == 0 ? 6f : -4f);
                Vector3 mPos = centerOffset + new Vector3(Mathf.Sin(rad) * dist, 0f, Mathf.Cos(rad) * dist);
                Quaternion mRot = Quaternion.Euler(0f, angleDeg + 180f, 0f); // Face inward
                Vector3 mScale = new Vector3(4.8f, 3.8f + (m % 3) * 0.4f, 4.8f);

                SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Env/SM_Env_Junk_Background_01.prefab", mPos, mRot, mScale, parent);
            }
        }

        private static GameObject CreateTerrainPlateau(string name, Vector3 pos, Quaternion rot, Vector3 scale, Material mat, Transform parent)
        {
            GameObject plateau = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plateau.name = name;
            plateau.transform.SetParent(parent);
            plateau.transform.position = pos;
            plateau.transform.rotation = rot;
            plateau.transform.localScale = scale;
            if (mat != null) plateau.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return plateau;
        }

        private static GameObject SpawnAlitaCyborg()
        {
            GameObject alitaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AlitaPrefabPath);
            GameObject alitaInstance;

            Vector3 alitaPos = DefaultAlitaPos;
            Quaternion alitaRot = DefaultAlitaRot;

            if (alitaPrefab != null)
            {
                alitaInstance = (GameObject)PrefabUtility.InstantiatePrefab(alitaPrefab);
                alitaInstance.transform.position = alitaPos;
                alitaInstance.transform.rotation = alitaRot;
            }
            else
            {
                alitaInstance = AlitaScrapyardSceneSetup.BuildBrokenAlitaCyborg();
                alitaInstance.transform.position = alitaPos;
                alitaInstance.transform.rotation = alitaRot;
            }

            alitaInstance.name = "Alita_Broken_Cyborg";

            var controller = alitaInstance.GetComponent<AlitaBrokenCyborg>();
            if (controller == null) controller = alitaInstance.AddComponent<AlitaBrokenCyborg>();
            controller.showLegacyGUI = false; // Using modern cinematic cutscene subtitles & letterbox

            return alitaInstance;
        }

        private static GameObject SpawnDoctor()
        {
            GameObject doctorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DoctorPrefabPath)
                                      ?? AssetDatabase.LoadAssetAtPath<GameObject>(DoctorFallbackPrefabPath);

            Vector3 startPos = DefaultDoctorEntrancePos;
            Quaternion startRot = Quaternion.Euler(0f, 8f, 0f);

            // 1. Root Actor container (holds world position, navigation, and persistent transform)
            GameObject doctorActor = new GameObject("Doctor_Actor");
            doctorActor.transform.position = startPos;
            doctorActor.transform.rotation = startRot;

            // 2. Child model instance (contains SkinnedMeshRenderers and Animator)
            GameObject doctorModel;
            if (doctorPrefab != null)
            {
                doctorModel = (GameObject)PrefabUtility.InstantiatePrefab(doctorPrefab, doctorActor.transform);
            }
            else
            {
                doctorModel = DoctorCreator.CreateDoctor();
                doctorModel.transform.SetParent(doctorActor.transform, false);
            }
            doctorModel.name = "Doctor_Model";
            doctorModel.transform.localPosition = Vector3.zero;
            doctorModel.transform.localRotation = Quaternion.identity;

            // Clean up any NavMeshAgent on cutscene actor to allow pure cinematic transform control
            var agent = doctorModel.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null) agent.enabled = false;

            // 3. Dedicated Cinematic Key Light on Doctor (Warm practical light)
            GameObject keyLightGo = new GameObject("Doctor_Key_Light");
            keyLightGo.transform.SetParent(doctorActor.transform);
            keyLightGo.transform.localPosition = new Vector3(0.4f, 2.3f, 1.0f);
            keyLightGo.transform.localRotation = Quaternion.Euler(50f, -25f, 0f);
            Light keyLight = keyLightGo.AddComponent<Light>();
            keyLight.type = LightType.Spot;
            keyLight.spotAngle = 70f;
            keyLight.innerSpotAngle = 45f;
            keyLight.range = 8.5f;
            keyLight.color = new Color(1.0f, 0.94f, 0.82f);
            keyLight.intensity = 2.4f;
            keyLight.shadows = LightShadows.Soft;

            // 4. Dedicated Cyan Rim Light on Doctor (Cyberpunk edge contrast)
            GameObject rimLightGo = new GameObject("Doctor_Rim_Light");
            rimLightGo.transform.SetParent(doctorActor.transform);
            rimLightGo.transform.localPosition = new Vector3(-0.6f, 1.8f, -1.2f);
            rimLightGo.transform.localRotation = Quaternion.Euler(25f, 155f, 0f);
            Light rimLight = rimLightGo.AddComponent<Light>();
            rimLight.type = LightType.Spot;
            rimLight.spotAngle = 75f;
            rimLight.innerSpotAngle = 50f;
            rimLight.range = 7.0f;
            rimLight.color = new Color(0.1f, 0.85f, 1.0f);
            rimLight.intensity = 1.6f;
            rimLight.shadows = LightShadows.None;

            // 5. Framing Targets
            GameObject doctorHeadTarget = new GameObject("Target_Doctor_Head");
            doctorHeadTarget.transform.SetParent(doctorActor.transform);
            doctorHeadTarget.transform.localPosition = new Vector3(0f, 1.62f, 0f);

            GameObject doctorChestTarget = new GameObject("Target_Doctor_Chest");
            doctorChestTarget.transform.SetParent(doctorActor.transform);
            doctorChestTarget.transform.localPosition = new Vector3(0f, 1.25f, 0f);

            return doctorActor;
        }

        private static List<CinemachineCamera> BuildCinemachineRig(out GameObject mainCamGo, Transform doctorT, Transform alitaT)
        {
            var cameras = new List<CinemachineCamera>();
            Vector3 aPos = alitaT.position;
            Vector3 entrance = DefaultDoctorEntrancePos;
            Vector3 notice   = DefaultDoctorNoticePos;

            // 1. Main Camera with CinemachineBrain and URP Additional Camera Data
            mainCamGo = new GameObject("MainCamera");
            mainCamGo.tag = "MainCamera";
            Camera cam = mainCamGo.AddComponent<Camera>();
            cam.fieldOfView = 48f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 450f;
            mainCamGo.transform.position = entrance + new Vector3(0.8f, 1.6f, -4.0f);
            mainCamGo.transform.rotation = Quaternion.Euler(10f, -10f, 0f);

            var brain = mainCamGo.AddComponent<CinemachineBrain>();
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 1.8f);

            var uac = mainCamGo.AddComponent<UniversalAdditionalCameraData>();
            uac.renderPostProcessing = true;
            uac.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            uac.antialiasingQuality = AntialiasingQuality.High;

            mainCamGo.AddComponent<AudioListener>();

            GameObject rigRoot = new GameObject("Cinemachine_Cameras");

            Transform doctorHeadTarget = doctorT.Find("Target_Doctor_Head") ?? doctorT;
            Transform doctorChestTarget = doctorT.Find("Target_Doctor_Chest") ?? doctorT;

            GameObject alitaCoreTarget = new GameObject("Target_Alita_Core");
            alitaCoreTarget.transform.SetParent(alitaT);
            alitaCoreTarget.transform.localPosition = new Vector3(0f, 0.25f, 0f);

            // Shot 1: CM_Opening (0s - 5s) — Wide atmospheric establishing shot directly framing Doctor entering
            var cmOpening = CreateCMCamera("CM_Opening", rigRoot.transform,
                entrance + new Vector3(0.8f, 1.6f, -4.0f), Quaternion.Euler(10f, -10f, 0f), 50f);
            cmOpening.Target.LookAtTarget = doctorChestTarget;
            cameras.Add(cmOpening);

            // Shot 2: CM_Doctor_Walk (4.2s - 18s) — Low-angle 3rd person tracking camera trailing Doctor
            var cmWalk = CreateCMCamera("CM_Doctor_Walk", rigRoot.transform,
                entrance + new Vector3(-1.0f, 1.55f, -1.0f), Quaternion.Euler(10f, 18f, 0f), 45f);
            cmWalk.Target.TrackingTarget = doctorT;
            cmWalk.Target.LookAtTarget = doctorChestTarget;
            cameras.Add(cmWalk);

            // Shot 3: CM_Doctor_Reaction (18s - 23s) — Medium portrait of Doctor stopping, visor glowing
            var cmReaction = CreateCMCamera("CM_Doctor_Reaction", rigRoot.transform,
                notice + new Vector3(0.55f, 1.45f, 1.4f), Quaternion.Euler(6f, 195f, 0f), 34f);
            cmReaction.Target.LookAtTarget = doctorHeadTarget;
            cameras.Add(cmReaction);

            // Shot 4: CM_Alita_Reveal (23s - 30s) — Heroic unobstructed camera framing Alita lying in the scrap
            var cmReveal = CreateCMCamera("CM_Alita_Reveal", rigRoot.transform,
                aPos + new Vector3(0.75f, 0.72f, -1.3f), Quaternion.Euler(18f, -42f, 0f), 38f);
            cmReveal.Target.LookAtTarget = alitaCoreTarget.transform;
            cameras.Add(cmReveal);

            // Shot 5: CM_Approach (30s - 39s) — Two-shot showing Doctor stepping toward Alita
            var cmApproach = CreateCMCamera("CM_Approach", rigRoot.transform,
                aPos + new Vector3(-2.2f, 1.27f, -2.5f), Quaternion.Euler(14f, 52f, 0f), 42f);
            cmApproach.Target.LookAtTarget = alitaCoreTarget.transform;
            cameras.Add(cmApproach);

            // Shot 6 & 7: CM_Interaction (39s - 56s) — Intimate two-shot as Doctor kneels, examines, and touches core
            var cmInteraction = CreateCMCamera("CM_Interaction", rigRoot.transform,
                aPos + new Vector3(-1.15f, 1.02f, -1.6f), Quaternion.Euler(12f, 56f, 0f), 36f);
            cmInteraction.Target.LookAtTarget = alitaCoreTarget.transform;
            cameras.Add(cmInteraction);

            // Shot 8, 9, 10: CM_Alita_Closeup (56s - 71s) — Macro close-up on Alita's face and surging URM core
            var cmCloseup = CreateCMCamera("CM_Alita_Closeup", rigRoot.transform,
                aPos + new Vector3(-0.15f, 0.52f, -0.75f), Quaternion.Euler(16f, 18f, 0f), 30f);
            cmCloseup.Target.LookAtTarget = alitaCoreTarget.transform;
            cameras.Add(cmCloseup);

            return cameras;
        }

        private static CinemachineCamera CreateCMCamera(string name, Transform parent, Vector3 pos, Quaternion rot, float fov)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = pos;
            go.transform.rotation = rot;

            var cm = go.AddComponent<CinemachineCamera>();
            cm.Lens.FieldOfView = fov;
            cm.Lens.NearClipPlane = 0.05f;
            cm.Lens.FarClipPlane = 450f;
            return cm;
        }

        private static void BuildLightingAndAtmosphere(Vector3 alitaPos, out Light sunLight, out Light moundLight, out ParticleSystem dustFX)
        {
            GameObject lightRoot = new GameObject("Lighting_Atmosphere");

            // 1. Zalem Sunlight piercing the smog (Bright, warm directional key light)
            GameObject sunGo = new GameObject("Sun_Zalem_Pierce");
            sunGo.transform.SetParent(lightRoot.transform);
            sunGo.transform.rotation = Quaternion.Euler(44f, -36f, 0f);
            Light sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1.0f, 0.93f, 0.84f);
            sun.intensity = 2.4f;
            sun.shadows = LightShadows.Soft;
            sunLight = sun;

            // 2. Cyan Rim Fill Light for cinematic cyberpunk contrast
            GameObject rimGo = new GameObject("Rim_CyberCyan");
            rimGo.transform.SetParent(lightRoot.transform);
            rimGo.transform.rotation = Quaternion.Euler(-20f, 140f, 0f);
            Light rim = rimGo.AddComponent<Light>();
            rim.type = LightType.Directional;
            rim.color = new Color(0.12f, 0.78f, 1.0f);
            rim.intensity = 1.35f;
            rim.shadows = LightShadows.None;

            // 3. Scrapyard Path Fill Light (ensures corridor is clearly visible and not murky)
            GameObject pathLightGo = new GameObject("Light_Scrapyard_Path_Fill");
            pathLightGo.transform.SetParent(lightRoot.transform);
            pathLightGo.transform.position = alitaPos + new Vector3(-0.8f, 3.8f, -5.5f);
            Light pathLight = pathLightGo.AddComponent<Light>();
            pathLight.type = LightType.Point;
            pathLight.color = new Color(0.85f, 0.92f, 1.0f);
            pathLight.range = 18f;
            pathLight.intensity = 1.3f;
            pathLight.shadows = LightShadows.None;

            // 4. Dedicated Cyan Glow over Alita's discovery spot
            GameObject moundLightGo = new GameObject("Light_Discovery_Focus");
            moundLightGo.transform.SetParent(lightRoot.transform);
            moundLightGo.transform.position = alitaPos + new Vector3(0f, 1.8f, -0.2f);
            Light mound = moundLightGo.AddComponent<Light>();
            mound.type = LightType.Point;
            mound.color = new Color(0.15f, 0.90f, 1.0f);
            mound.range = 7.5f;
            mound.intensity = 3.2f;
            moundLight = mound;

            // 5. Dedicated Key Spotlight on Alita's Face & Torso
            GameObject alitaKeyGo = new GameObject("Light_Alita_Face_Key");
            alitaKeyGo.transform.SetParent(lightRoot.transform);
            alitaKeyGo.transform.position = alitaPos + new Vector3(0.5f, 2.2f, -1.2f);
            alitaKeyGo.transform.rotation = Quaternion.Euler(46f, -22f, 0f);
            Light alitaKey = alitaKeyGo.AddComponent<Light>();
            alitaKey.type = LightType.Spot;
            alitaKey.spotAngle = 65f;
            alitaKey.innerSpotAngle = 40f;
            alitaKey.range = 7.5f;
            alitaKey.color = new Color(1.0f, 0.96f, 0.90f);
            alitaKey.intensity = 3.4f;
            alitaKey.shadows = LightShadows.Soft;

            // 6. Ambient Trilight (Vibrant, clear ambient light so details pop)
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.62f, 0.72f);
            RenderSettings.ambientEquatorColor = new Color(0.44f, 0.42f, 0.50f);
            RenderSettings.ambientGroundColor = new Color(0.30f, 0.28f, 0.35f);

            // 7. Atmospheric Moody Distance Fog for 300m wasteland depth
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.0075f;
            RenderSettings.fogColor = new Color(0.11f, 0.13f, 0.19f);

            // 8. Distant Rim / Horizon Accent Lights on Colossal Scrap Mountain Ridges
            GameObject ridgeCyanGo = new GameObject("Light_Ridge_Cyan_Glow");
            ridgeCyanGo.transform.SetParent(lightRoot.transform);
            ridgeCyanGo.transform.position = new Vector3(-45f, 25f, 40f);
            var ridgeCyan = ridgeCyanGo.AddComponent<Light>();
            ridgeCyan.type = LightType.Point;
            ridgeCyan.color = new Color(0.15f, 0.85f, 1.0f);
            ridgeCyan.range = 80f;
            ridgeCyan.intensity = 1.2f;

            GameObject ridgeAmberGo = new GameObject("Light_Ridge_Amber_Glow");
            ridgeAmberGo.transform.SetParent(lightRoot.transform);
            ridgeAmberGo.transform.position = new Vector3(45f, 25f, 35f);
            var ridgeAmber = ridgeAmberGo.AddComponent<Light>();
            ridgeAmber.type = LightType.Point;
            ridgeAmber.color = new Color(1.0f, 0.65f, 0.25f);
            ridgeAmber.range = 80f;
            ridgeAmber.intensity = 1.1f;

            // 9. Environmental floating dust particles
            dustFX = null;
            var dustPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonSciFiWorlds/Prefabs/FX/FX_Scrap_Floating_Mesh_Full_01.prefab");
            if (dustPrefab != null)
            {
                var dust = UnityEngine.Object.Instantiate(dustPrefab, lightRoot.transform);
                dust.name = "FX_Scrap_Floating_Dust";
                dust.transform.position = alitaPos + new Vector3(-1f, 1.5f, -2f);
                dustFX = dust.GetComponentInChildren<ParticleSystem>();
            }

            // 10. Global Post-Processing Volume
            GameObject volGo = new GameObject("PostProcessing_GlobalVolume");
            volGo.transform.SetParent(lightRoot.transform);
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Alita_Cutscene_PostProcess_Profile";
            AssetDatabase.CreateAsset(profile, "Assets/Timelines/Alita_Cutscene_PostProcess_Profile.asset");

            // Bloom
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.80f);
            bloom.intensity.Override(1.4f);
            bloom.scatter.Override(0.70f);
            bloom.tint.Override(new Color(0.92f, 0.98f, 1f));

            // Vignette (Subtle cinematic falloff)
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.22f);
            vignette.smoothness.Override(0.42f);

            // Tonemapping
            var tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.ACES);

            // Color Adjustments (Crisp, high clarity, vivid cybernetic tones)
            var colorAdj = profile.Add<ColorAdjustments>(true);
            colorAdj.postExposure.Override(0.25f);
            colorAdj.contrast.Override(8f);
            colorAdj.saturation.Override(10f);

            vol.profile = profile;
        }

        private static TimelineAsset BuildTimelineAsset(List<CinemachineCamera> cmCameras, GameObject doctorGo, GameObject alitaGo)
        {
            // Create or replace Timeline Asset
            TimelineAsset timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);
            if (timeline != null)
            {
                AssetDatabase.DeleteAsset(TimelinePath);
            }

            timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            timeline.name = "Alita_Discovery_Timeline";
            AssetDatabase.CreateAsset(timeline, TimelinePath);

            // 1. Cinemachine Track
            var cmTrack = timeline.CreateTrack<CinemachineTrack>(null, "Cinemachine Track");

            // Shots setup: (CM, start, duration, name, easeIn, easeOut)
            // Total = 71 seconds
            AddCinemachineShot(cmTrack, cmCameras[0], 0.0, 5.0, "Shot 1 — Establishing Scrapyard", 0.0, 1.2);
            AddCinemachineShot(cmTrack, cmCameras[1], 4.2, 14.5, "Shot 2 — Doctor Walks Through Scrap", 1.2, 1.5);
            AddCinemachineShot(cmTrack, cmCameras[2], 18.0, 5.5, "Shot 3 — Doctor Notices Glow", 1.0, 1.2);
            AddCinemachineShot(cmTrack, cmCameras[3], 23.0, 7.5, "Shot 4 — Alita Revealed in Debris", 1.2, 1.5);
            AddCinemachineShot(cmTrack, cmCameras[4], 30.0, 9.5, "Shot 5 — Doctor Approaches Mound", 1.2, 1.5);
            AddCinemachineShot(cmTrack, cmCameras[5], 39.0, 17.5, "Shots 6 & 7 — Kneel & Examine Interface", 1.5, 1.8);
            AddCinemachineShot(cmTrack, cmCameras[6], 56.0, 15.0, "Shots 8..10 — Alita Activates & Awakening", 1.5, 0.0);

            // 2. Doctor Animation Track
            var doctorTrack = timeline.CreateTrack<AnimationTrack>(null, "Doctor Animation Track");

            AnimationClip walkAnim = LoadClipFromFbx("Assets/Synty/AnimationBaseLocomotion/Animations/Sidekick/Masculine/Locomotion/Walk/A_MOD_BL_Walk_F_Masc.fbx");
            AnimationClip idleAnim = LoadClipFromFbx("Assets/Synty/AnimationBaseLocomotion/Animations/Sidekick/Masculine/Idles/A_MOD_BL_Idle_Standing_Masc.fbx");
            AnimationClip crouchTransAnim = LoadClipFromFbx("Assets/Synty/AnimationBaseLocomotion/Animations/Sidekick/Masculine/Transitions/Stand_ToCrouch/A_MOD_BL_Stand_ToCrouch_Masc.fbx");
            AnimationClip crouchIdleAnim = LoadClipFromFbx("Assets/Synty/AnimationBaseLocomotion/Animations/Sidekick/Masculine/Idles/A_MOD_BL_Idle_Crouching_Masc.fbx");
            AnimationClip reachInteractAnim = LoadClipFromFbx("Assets/CombatGirlsCharacterPack/CombatGirls_DualKatana/Animations/Normal/DK_Take.fbx");

            // Clip 1: Walk from entrance (0s - 18s)
            AddAnimClip(doctorTrack, walkAnim, 0.0, 18.0, "Walk Entrance", 0.0, 0.8);

            // Clip 2: Idle & Notice (18s - 30s)
            AddAnimClip(doctorTrack, idleAnim, 17.5, 12.5, "Stop & Notice", 0.8, 0.8);

            // Clip 3: Walk approach (30s - 39s)
            AddAnimClip(doctorTrack, walkAnim, 29.5, 9.5, "Approach Alita", 0.8, 0.8);

            // Clip 4: Stand to Crouch (39s - 42.5s)
            AddAnimClip(doctorTrack, crouchTransAnim, 38.8, 3.7, "Kneel Down", 0.6, 0.5);

            // Clip 5: Crouch Idle & Inspect (42s - 48s)
            AddAnimClip(doctorTrack, crouchIdleAnim, 42.0, 6.5, "Examine Body", 0.5, 0.6);

            // Clip 6: Reach / Touch Alita (48s - 56s)
            AddAnimClip(doctorTrack, reachInteractAnim ?? crouchIdleAnim, 48.0, 8.0, "Touch Interface", 0.6, 0.8);

            // Clip 7: Surprised reaction & Close observation (56s - 71s)
            AddAnimClip(doctorTrack, crouchIdleAnim, 55.5, 15.5, "Observe Awakening", 0.8, 0.0);

            // 3. Timeline Markers for Narrative Signals
            if (timeline.markerTrack == null)
            {
                timeline.CreateMarkerTrack();
            }
            var markerTrack = timeline.markerTrack;

            AddSignalMarker(markerTrack, 18.0, "Doctor_Notices_Alita");
            AddSignalMarker(markerTrack, 48.0, "Doctor_Touches_Alita");
            AddSignalMarker(markerTrack, 56.0, "Alita_Activates");
            AddSignalMarker(markerTrack, 71.0, "Cutscene_End");

            EditorUtility.SetDirty(timeline);
            return timeline;
        }

        private static void AddCinemachineShot(CinemachineTrack track, CinemachineCamera cam, double start, double duration, string name, double easeIn, double easeOut)
        {
            TimelineClip clip = track.CreateClip<CinemachineShot>();
            clip.start = start;
            clip.duration = duration;
            clip.displayName = name;

            if (easeIn > 0) clip.easeInDuration = easeIn;
            if (easeOut > 0) clip.easeOutDuration = easeOut;

            var shotAsset = clip.asset as CinemachineShot;
            if (shotAsset != null)
            {
                shotAsset.VirtualCamera.defaultValue = cam;
                shotAsset.VirtualCamera.exposedName = new PropertyName($"Ref_{name}_{Guid.NewGuid().ToString().Substring(0, 8)}");
            }
        }

        private static void AddAnimClip(AnimationTrack track, AnimationClip clip, double start, double duration, string name, double easeIn, double easeOut)
        {
            if (clip == null) return;
            TimelineClip tc = track.CreateClip(clip);
            tc.start = start;
            tc.duration = duration;
            tc.displayName = name;
            if (easeIn > 0) tc.easeInDuration = easeIn;
            if (easeOut > 0) tc.easeOutDuration = easeOut;
        }

        private static void AddSignalMarker(TrackAsset track, double time, string markerName)
        {
            var emitter = track.CreateMarker<SignalEmitter>(time);
            emitter.name = markerName;
        }

        private static void BindTimelineTracks(PlayableDirector director, TimelineAsset timeline, CinemachineBrain brain, GameObject doctorGo, List<CinemachineCamera> cmCameras)
        {
            var anim = doctorGo.GetComponentInChildren<Animator>();

            foreach (var track in timeline.GetOutputTracks())
            {
                if (track is CinemachineTrack cmTrack)
                {
                    director.SetGenericBinding(cmTrack, brain);

                    // Set camera exposed references for all shot clips
                    int camIndex = 0;
                    foreach (var clip in cmTrack.GetClips())
                    {
                        var shotAsset = clip.asset as CinemachineShot;
                        if (shotAsset != null && camIndex < cmCameras.Count)
                        {
                            director.SetReferenceValue(shotAsset.VirtualCamera.exposedName, cmCameras[camIndex]);
                            camIndex++;
                        }
                    }
                }
                else if (track is AnimationTrack animTrack)
                {
                    if (animTrack.name.Contains("Doctor") && anim != null)
                    {
                        director.SetGenericBinding(animTrack, anim);
                    }
                }
            }
        }

        private static AnimationClip LoadClipFromFbx(string path)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var a in assets)
            {
                if (a is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    return clip;
                }
            }
            return null;
        }

        private static GameObject SpawnPropSafe(string path, Vector3 pos, Quaternion rot, Vector3 scale, Transform parent)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) return null;
            var go = UnityEngine.Object.Instantiate(asset, pos, rot, parent);
            go.transform.localScale = scale;
            return go;
        }
    }
}
#endif
