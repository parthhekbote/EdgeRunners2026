#if UNITY_EDITOR
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Alita;

namespace Alita.Editor
{
    public static class AlitaScrapyardSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/Scene_Alita_Scrapyard_Discovery.unity";
        private const string PrefabSavePath = "Assets/Prefabs/Alita_Broken_Cyborg.prefab";
        private const string FemaleCyborgPrefab = "Assets/PolygonCyberCity/Prefabs/Characters/SM_Chr_Cyborg_Female_01.prefab";

        [MenuItem("Tools/Alita Battle Angel/Setup Scrapyard Discovery Scene")]
        public static void SetupScrapyardScene() => SetupScrapyardSceneInternal(true);

        public static void SetupScrapyardSceneBatch() => SetupScrapyardSceneInternal(false);

        public static void SetupScrapyardSceneInternal(bool showDialog)
        {
            Directory.CreateDirectory("Assets/Scenes");
            Directory.CreateDirectory("Assets/Prefabs");

            // 1. Create Clean Scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // 2. Build Scrapyard Environment
            BuildScrapyardEnvironment();

            // 3. Build & Pose Broken Alita Cyborg
            GameObject alitaGo = BuildBrokenAlitaCyborg();

            // 4. Setup Cinematic Camera
            SetupCinematicCamera(alitaGo.transform);

            // 5. Setup Atmospheric Lighting & Volumetric Zalem Sunbeam
            SetupScrapyardLighting(alitaGo.transform.position);

            // 6. Save Scene & Reusable Prefab
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log($"[Alita] Successfully created Scrapyard Discovery Scene at {ScenePath}!");
            Debug.Log($"[Alita] Reusable Broken Alita prefab saved to {PrefabSavePath}!");

            if (showDialog && !Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("Alita: Battle Angel",
                    "Scrapyard Discovery Scene is READY!\n\n" +
                    "• Character: Broken Alita Cyborg (severed limbs, mechanical spine, trailing cables)\n" +
                    "• Core: Beating 300-year-old URM Antimatter Heart (audio & light pulse)\n" +
                    "• Scrapyard: Massive piles of discarded robotic debris from Zalem\n" +
                    "• Camera: Cinematic orbit & examine scan (Press [E] or [Space] in Play mode)\n\n" +
                    "Press PLAY to experience the discovery!", "Witness The Awakening!");
            }
        }

        public static void BuildScrapyardEnvironment()
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
            // 2. ZONE 1: DISCOVERY CLEARING (Radius 0m - 12m)
            // -------------------------------------------------------------
            GameObject zone1Root = new GameObject("Zone1_Discovery_Clearing");
            zone1Root.transform.SetParent(scrapRoot.transform);

            // Alita's central discovery mound robot debris (behind & framing Alita at 0,0,0)
            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Props/SM_Prop_Robot_Posed_Pile_01.prefab",
                new Vector3(0.2f, 0f, 0.8f), Quaternion.Euler(0f, 40f, 0f), new Vector3(1.25f, 1.25f, 1.25f), zone1Root.transform);

            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Props/SM_Prop_Robot_Posed_Pile_02.prefab",
                new Vector3(-2.2f, 0f, 1.3f), Quaternion.Euler(0f, -45f, 0f), Vector3.one, zone1Root.transform);

            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Props/SM_Prop_Robot_Posed_01.prefab",
                new Vector3(1.6f, 0.05f, -0.3f), Quaternion.Euler(-15f, 70f, 20f), Vector3.one, zone1Root.transform);

            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Props/SM_Prop_Robot_Posed_03.prefab",
                new Vector3(-1.9f, 0.05f, -0.7f), Quaternion.Euler(10f, -50f, -15f), Vector3.one, zone1Root.transform);

            // Scattered robot components around Alita's resting place
            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Props/SM_Prop_Robot_Part_01.prefab",
                new Vector3(0.85f, 0f, 0.4f), Quaternion.Euler(15f, 30f, 45f), Vector3.one, zone1Root.transform);
            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Props/SM_Prop_Robot_Part_02.prefab",
                new Vector3(-1.1f, 0f, 0.6f), Quaternion.Euler(-25f, 65f, 10f), Vector3.one, zone1Root.transform);
            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Props/SM_Prop_Robotic_Needle_Arm_01.prefab",
                new Vector3(1.3f, 0.1f, -0.9f), Quaternion.Euler(30f, -40f, 10f), Vector3.one, zone1Root.transform);

            // Cable bundles trailing into the scrap
            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Props/SM_Prop_Wires_02.prefab",
                new Vector3(0.4f, 0f, 0.3f), Quaternion.Euler(0f, 25f, 0f), Vector3.one, zone1Root.transform);
            SpawnPropSafe("Assets/PolygonCyberCity/Prefabs/Props/SM_Prop_Wires_12.prefab",
                new Vector3(-0.7f, 0f, 0.5f), Quaternion.Euler(0f, -60f, 0f), Vector3.one, zone1Root.transform);

            // Flanking scrap mounds
            SpawnPropSafe("Assets/Synty/PolygonSciFiWorlds/Prefabs/Props/Scrap/SM_Prop_Scav_Scrap_Pile_01.prefab",
                new Vector3(-4.0f, 0f, -6.5f), Quaternion.Euler(0f, 60f, 0f), Vector3.one, zone1Root.transform);
            SpawnPropSafe("Assets/Synty/PolygonSciFiWorlds/Prefabs/Props/Scrap/SM_Prop_Scav_Scrap_Pile_02.prefab",
                new Vector3(3.5f, 0f, -8.0f), Quaternion.Euler(0f, -30f, 0f), Vector3.one, zone1Root.transform);
            SpawnPropSafe("Assets/Synty/PolygonSciFiWorlds/Prefabs/Props/Scrap/SM_Prop_Scav_Scrap_Pile_03.prefab",
                new Vector3(-3.8f, 0f, 0.2f), Quaternion.Euler(0f, 15f, 0f), new Vector3(1.2f, 1.1f, 1.2f), zone1Root.transform);
            SpawnPropSafe("Assets/Synty/PolygonSciFiWorlds/Prefabs/Props/Scrap/SM_Prop_Scav_Scrap_Pile_04.prefab",
                new Vector3(3.6f, 0f, 1.2f), Quaternion.Euler(0f, -85f, 0f), Vector3.one, zone1Root.transform);

            SpawnPropSafe("Assets/Synty/PolygonSciFiWorlds/Prefabs/Props/Scrap/SM_Prop_Scav_Scrap_21.prefab",
                new Vector3(1.9f, 0f, 0.3f), Quaternion.Euler(15f, -35f, -10f), new Vector3(1.1f, 1.1f, 1.1f), zone1Root.transform);
            SpawnPropSafe("Assets/Synty/PolygonSciFiWorlds/Prefabs/Props/Scrap/SM_Prop_Scav_Scrap_40.prefab",
                new Vector3(-1.9f, 0f, 0.3f), Quaternion.Euler(-10f, 45f, 25f), Vector3.one, zone1Root.transform);

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
                    float angleDeg = i * angleStep + (r * 19.5f);
                    float rad = angleDeg * Mathf.Deg2Rad;
                    float x = Mathf.Sin(rad) * radius;
                    float z = Mathf.Cos(rad) * radius;

                    // Protect central discovery clearing
                    if (Mathf.Abs(x) < 4.5f && Mathf.Abs(z) < 4.5f) continue;

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
                Quaternion mRot = Quaternion.Euler(0f, angleDeg + 180f, 0f);
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

        public static GameObject SpawnPropSafe(string path, Vector3 pos, Quaternion rot, Vector3 scale, Transform parent = null)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) return null;
            var go = parent != null ? Object.Instantiate(asset, pos, rot, parent) : Object.Instantiate(asset, pos, rot);
            go.transform.localScale = scale;
            return go;
        }

        private const string AnimeAlitaPrefab = "Assets/CombatGirlsCharacterPack/CombatGirls_DualKatana/Prefab/Dual_Katana_Girl.prefab";
        private const string DarkHairMatPath = "Assets/CombatGirlsCharacterPack/CombatGirls_DualKatana/Materials/Hair/Dual_Katana_Hair_02.mat";

        public static GameObject BuildBrokenAlitaCyborg()
        {
            // Prefer high-detail Anime female character model for Alita's delicate face & hair; fallback to Synty Cyborg
            GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AnimeAlitaPrefab)
                                    ?? AssetDatabase.LoadAssetAtPath<GameObject>(FemaleCyborgPrefab);

            GameObject alita;
            Vector3 spawnPos = new Vector3(0f, 0.38f, 0f);
            Quaternion spawnRot = Quaternion.Euler(22f, -32f, -6f);

            if (basePrefab != null)
            {
                alita = Object.Instantiate(basePrefab, spawnPos, spawnRot);
                alita.name = "Alita_Broken_Cyborg";
            }
            else
            {
                alita = new GameObject("Alita_Broken_Cyborg");
                alita.transform.position = spawnPos;
                alita.transform.rotation = spawnRot;
            }

            bool isAnimeModel = basePrefab != null && basePrefab.name.Contains("Dual_Katana");

            if (isAnimeModel)
            {
                ConfigureAnimeAlitaModel(alita);
            }
            else
            {
                ConfigureSyntyAlitaModel(alita);
            }

            // Find key anatomical joint transforms for attachments
            Transform chest = isAnimeModel
                ? (FindChildRecursive(alita.transform, "spine_02") ?? FindChildRecursive(alita.transform, "spine_03") ?? alita.transform)
                : (FindChildRecursive(alita.transform, "Spine_02") ?? alita.transform);

            Transform pelvis = isAnimeModel
                ? (FindChildRecursive(alita.transform, "pelvis") ?? FindChildRecursive(alita.transform, "spine_01") ?? alita.transform)
                : (FindChildRecursive(alita.transform, "Hips") ?? alita.transform);

            Transform leftStump = isAnimeModel
                ? (FindChildRecursive(alita.transform, "clavicle_l") ?? FindChildRecursive(alita.transform, "upperarm_l") ?? alita.transform)
                : (FindChildRecursive(alita.transform, "Elbow_L") ?? FindChildRecursive(alita.transform, "UpperArm_L") ?? alita.transform);

            // 1. Build Exposed Cybernetic Spine & Vertebrae at severed waist
            BuildExposedCyberneticSpine(pelvis);

            // 2. Attach Trailing Hydraulic & Electrical Cable Bundles at severed pelvis
            AttachTrailingWires(pelvis, new Vector3(0f, -0.06f, -0.08f), Quaternion.Euler(45f, 180f, 0f));

            // 3. Attach Beating URM Antimatter Heart Core inside chest cavity
            GameObject coreGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            coreGo.name = "URM_Antimatter_Core";
            coreGo.transform.SetParent(chest, false);
            coreGo.transform.localPosition = isAnimeModel ? new Vector3(0f, 0.06f, 0.09f) : new Vector3(0f, 0.12f, 0.08f);
            coreGo.transform.localScale = new Vector3(0.085f, 0.085f, 0.085f);
            Object.DestroyImmediate(coreGo.GetComponent<Collider>());

            // Outer Magnetic Containment Collar framing the core
            GameObject cageGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cageGo.name = "Core_Containment_Housing";
            cageGo.transform.SetParent(coreGo.transform, false);
            cageGo.transform.localPosition = Vector3.zero;
            cageGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            cageGo.transform.localScale = new Vector3(1.4f, 0.12f, 1.4f);
            Object.DestroyImmediate(cageGo.GetComponent<Collider>());
            var cageMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            cageMat.color = new Color(0.18f, 0.22f, 0.26f);
            cageMat.SetFloat("_Metallic", 0.92f);
            cageMat.SetFloat("_Smoothness", 0.85f);
            cageGo.GetComponent<MeshRenderer>().sharedMaterial = cageMat;

            // Glowing Cyan URM Core Material
            var coreMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            Color cyanGlow = new Color(0.0f, 0.90f, 1.0f);
            coreMat.color = cyanGlow;
            coreMat.EnableKeyword("_EMISSION");
            coreMat.SetColor("_EmissionColor", cyanGlow * 4.2f);
            var coreRend = coreGo.GetComponent<MeshRenderer>();
            coreRend.sharedMaterial = coreMat;

            // Core Pulsating Point Light
            var coreLight = coreGo.AddComponent<Light>();
            coreLight.type = LightType.Point;
            coreLight.color = cyanGlow;
            coreLight.range = 3.8f;
            coreLight.intensity = 2.6f;

            // 4. Electric Micro-Sparks at Severed Left Arm Stump
            ParticleSystem sparkPs = CreateElectricSparkEmitter(leftStump, Vector3.zero);

            // 5. Attach AlitaBrokenCyborg Controller Component
            var controller = alita.GetComponent<AlitaBrokenCyborg>() ?? alita.AddComponent<AlitaBrokenCyborg>();
            controller.coreRenderer = coreRend;
            controller.coreLight = coreLight;
            controller.coreColor = cyanGlow;
            controller.baseCoreIntensity = 2.4f;
            // Clean up any missing scripts from imported asset
            foreach (Transform t in alita.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            }

            // Save Prefab safely
            try
            {
                PrefabUtility.SaveAsPrefabAsset(alita, PrefabSavePath);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[Alita] Prefab save warning: " + ex.Message);
            }

            Selection.activeGameObject = alita;
            return alita;
        }

        private static void ConfigureAnimeAlitaModel(GameObject alita)
        {
            // 1. Remove extraneous weapons and combat accessories
            string[] toRemove = new string[]
            {
                "Weapon_Katana L", "Weapon_Katana R",
                "Bone_Acc_Shuriken_Root", "DualKatana_Cloth", "Shinobi_Cloth"
            };
            foreach (var name in toRemove)
            {
                Transform t = FindChildRecursive(alita.transform, name);
                if (t != null) Object.DestroyImmediate(t.gameObject);
            }

            // 2. Setup Serene Sleeping / Dormant Face
            // Deactivate alternate expressions, activate Face_04 (peaceful serene face)
            string[] facesToDeactivate = new string[] { "Face_01Re", "Face_02", "Face_03", "Face_05" };
            foreach (var faceName in facesToDeactivate)
            {
                Transform t = FindChildRecursive(alita.transform, faceName);
                if (t != null) t.gameObject.SetActive(false);
            }
            Transform sereneFace = FindChildRecursive(alita.transform, "Face_04") ?? FindChildRecursive(alita.transform, "Face_01Re");
            if (sereneFace != null) sereneFace.gameObject.SetActive(true);

            // Deactivate combat masks
            string[] masks = new string[] { "Mask01", "Mask02", "Mask03" };
            foreach (var maskName in masks)
            {
                Transform t = FindChildRecursive(alita.transform, maskName);
                if (t != null) t.gameObject.SetActive(false);
            }

            // 3. Set Alita Movie Dark Brunette/Black Hair Material
            var darkHairMat = AssetDatabase.LoadAssetAtPath<Material>(DarkHairMatPath);
            if (darkHairMat != null)
            {
                Transform hairT = FindChildRecursive(alita.transform, "DualKatana_Hair");
                if (hairT != null)
                {
                    var rend = hairT.GetComponent<Renderer>();
                    if (rend != null) rend.sharedMaterial = darkHairMat;
                }
            }

            // 4. Disable any external physics scripts (e.g. MagicaCloth) to avoid runtime errors
            var allComponents = alita.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var mb in allComponents)
            {
                if (mb != null && mb.GetType().Namespace != null && mb.GetType().Namespace.Contains("Magica"))
                {
                    mb.enabled = false;
                }
            }

            // 5. Sever Lower Legs (Legs torn off at the hips, leaving severed pelvis)
            SeverLimbByName(alita.transform, "thigh_l");
            SeverLimbByName(alita.transform, "thigh_r");
            SeverLimbByName(alita.transform, "thigh_twist_01_l");
            SeverLimbByName(alita.transform, "thigh_twist_01_r");

            // 6. Sever Left Arm (Severed at shoulder socket)
            SeverLimbByName(alita.transform, "upperarm_l");
            SeverLimbByName(alita.transform, "lowerarm_l");
            SeverLimbByName(alita.transform, "upperarm_twist_01_l");
            SeverLimbByName(alita.transform, "lowerarm_twist_01_l");

            // 7. Pose Dormant Head resting gently back in scrap
            Transform neck = FindChildRecursive(alita.transform, "neck_01");
            if (neck != null) neck.localRotation = Quaternion.Euler(-10f, 12f, -4f);

            Transform head = FindChildRecursive(alita.transform, "head");
            if (head != null) head.localRotation = Quaternion.Euler(-8f, 14f, 3f);

            // 8. Right Arm resting limp and lifeless beside her
            Transform upperArmR = FindChildRecursive(alita.transform, "upperarm_r");
            if (upperArmR != null) upperArmR.localRotation = Quaternion.Euler(22f, 18f, -24f);

            Transform lowerArmR = FindChildRecursive(alita.transform, "lowerarm_r");
            if (lowerArmR != null) lowerArmR.localRotation = Quaternion.Euler(14f, 28f, 2f);
        }

        private static void ConfigureSyntyAlitaModel(GameObject alita)
        {
            // Sever lower legs at hip joints
            SeverLimbByName(alita.transform, "UpperLeg_L");
            SeverLimbByName(alita.transform, "UpperLeg_R");

            // Sever left arm at elbow/bicep
            SeverLimbByName(alita.transform, "LowerArm_L");

            // Pose head resting back gently
            Transform neck = FindChildRecursive(alita.transform, "Neck");
            if (neck != null) neck.localRotation = Quaternion.Euler(-12f, 10f, -6f);

            Transform head = FindChildRecursive(alita.transform, "Head");
            if (head != null) head.localRotation = Quaternion.Euler(-8f, 12f, 4f);

            // Right arm resting limp beside her
            Transform upperArmR = FindChildRecursive(alita.transform, "UpperArm_R");
            if (upperArmR != null) upperArmR.localRotation = Quaternion.Euler(15f, 25f, -30f);
        }

        private static void BuildExposedCyberneticSpine(Transform pelvis)
        {
            if (pelvis == null) return;

            GameObject spineAssembly = new GameObject("Exposed_Cybernetic_Spine");
            spineAssembly.transform.SetParent(pelvis, false);
            spineAssembly.transform.localPosition = new Vector3(0f, -0.02f, -0.04f);
            spineAssembly.transform.localRotation = Quaternion.identity;

            var spineMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            spineMat.color = new Color(0.22f, 0.24f, 0.27f);
            spineMat.SetFloat("_Metallic", 0.95f);
            spineMat.SetFloat("_Smoothness", 0.88f);

            // Create 3 segmented metallic vertebrae segments extending from severed waist
            for (int i = 0; i < 3; i++)
            {
                GameObject vertebra = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                vertebra.name = $"Vertebra_0{i + 1}";
                vertebra.transform.SetParent(spineAssembly.transform, false);
                vertebra.transform.localPosition = new Vector3(0f, -i * 0.042f, 0f);
                vertebra.transform.localScale = new Vector3(0.065f, 0.016f, 0.065f);
                Object.DestroyImmediate(vertebra.GetComponent<Collider>());
                vertebra.GetComponent<MeshRenderer>().sharedMaterial = spineMat;
            }
        }

        private static void SeverLimbByName(Transform root, string boneName)
        {
            Transform bone = FindChildRecursive(root, boneName);
            if (bone != null)
            {
                bone.localScale = Vector3.zero;
            }
        }

        private static void AttachTrailingWires(Transform parent, Vector3 localPos, Quaternion localRot)
        {
            string[] wirePaths = new string[]
            {
                "Assets/PolygonCyberCity/Prefabs/Props/SM_Prop_Wires_02.prefab",
                "Assets/PolygonCyberCity/Prefabs/Props/SM_Prop_Wires_12.prefab",
                "Assets/PolygonCyberCity/Prefabs/Props/SM_Prop_Wires_16.prefab"
            };

            foreach (var path in wirePaths)
            {
                var wireAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (wireAsset != null)
                {
                    var wire = Object.Instantiate(wireAsset, parent, false);
                    wire.name = "Severed_Cables";
                    wire.transform.localPosition = localPos;
                    wire.transform.localRotation = localRot;
                    wire.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
                    break;
                }
            }
        }

        private static ParticleSystem CreateElectricSparkEmitter(Transform parent, Vector3 localPos)
        {
            GameObject sparkGo = new GameObject("Cable_Sparks");
            sparkGo.transform.SetParent(parent, false);
            sparkGo.transform.localPosition = localPos;

            var ps = sparkGo.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 0.25f;
            main.startSpeed = 2.0f;
            main.startSize = 0.04f;
            main.startColor = new Color(0.2f, 0.9f, 1f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 12, 18) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;

            var rend = sparkGo.GetComponent<ParticleSystemRenderer>();
            rend.material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit"));

            return ps;
        }

        private static void SetupCinematicCamera(Transform target)
        {
            var camGo = Camera.main != null ? Camera.main.gameObject : new GameObject("Main Camera");
            camGo.tag = "MainCamera";

            var cam = camGo.GetComponent<Camera>() ?? camGo.AddComponent<Camera>();
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 450f;

            if (camGo.GetComponent<AudioListener>() == null)
            {
                camGo.AddComponent<AudioListener>();
            }

            var cineCam = camGo.GetComponent<AlitaCinematicCamera>() ?? camGo.AddComponent<AlitaCinematicCamera>();
            cineCam.target = target;
            cineCam.lookOffset = new Vector3(0f, 0.35f, 0f);
            cineCam.distance = 1.8f;
            cineCam.examineDistance = 0.95f;

            camGo.transform.position = target.position + new Vector3(0.6f, 0.9f, -1.5f);
            camGo.transform.LookAt(target.position + Vector3.up * 0.35f);
        }

        public static void SetupScrapyardLighting(Vector3 targetPos)
        {
            // 1. Ambient Moody Junkyard Fog for 300m wasteland depth
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.11f, 0.13f, 0.19f);
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.0075f;
            RenderSettings.ambientSkyColor = new Color(0.18f, 0.20f, 0.28f);
            RenderSettings.ambientGroundColor = new Color(0.06f, 0.05f, 0.08f);

            // 2. Low-intensity Moody Directional Fill Light
            var lightGo = GameObject.Find("Directional Light");
            if (lightGo == null) lightGo = new GameObject("Directional Light");
            var dirLight = lightGo.GetComponent<Light>();
            if (dirLight == null) dirLight = lightGo.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.color = new Color(0.35f, 0.45f, 0.65f);
            dirLight.intensity = 0.45f;
            lightGo.transform.rotation = Quaternion.Euler(40f, 130f, 0f);

            // 3. Dramatic Zalem Sunbeam Spotlight (Shining down through garbage chute onto Alita)
            GameObject beamGo = new GameObject("Zalem_Sunbeam_Spotlight");
            beamGo.transform.position = targetPos + new Vector3(-1.8f, 6.5f, -2.2f);
            beamGo.transform.LookAt(targetPos + Vector3.up * 0.3f);

            var spot = beamGo.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.color = new Color(1.0f, 0.92f, 0.80f); // Warm cinematic golden shaft of light
            spot.intensity = 6.5f;
            spot.range = 14f;
            spot.spotAngle = 32f;
            spot.innerSpotAngle = 18f;

            // 4. Ambient Dust Motes / Embers drifting in the sunbeam
            GameObject dustGo = new GameObject("Scrapyard_Dust_Motes");
            dustGo.transform.position = targetPos + Vector3.up * 1.5f;
            var ps = dustGo.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 5.0f;
            main.startSpeed = 0.15f;
            main.startSize = 0.02f;
            main.startColor = new Color(1.0f, 0.9f, 0.7f, 0.6f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 80;

            var emission = ps.emission;
            emission.rateOverTime = 15;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(3f, 2.5f, 3f);

            // 5. Distant Rim / Horizon Accent Lights on Colossal Mountain Ridges
            GameObject ridgeCyanGo = new GameObject("Light_Ridge_Cyan_Glow");
            ridgeCyanGo.transform.position = targetPos + new Vector3(-45f, 25f, 40f);
            var ridgeCyan = ridgeCyanGo.AddComponent<Light>();
            ridgeCyan.type = LightType.Point;
            ridgeCyan.color = new Color(0.15f, 0.85f, 1.0f);
            ridgeCyan.range = 80f;
            ridgeCyan.intensity = 1.2f;

            GameObject ridgeAmberGo = new GameObject("Light_Ridge_Amber_Glow");
            ridgeAmberGo.transform.position = targetPos + new Vector3(45f, 25f, 35f);
            var ridgeAmber = ridgeAmberGo.AddComponent<Light>();
            ridgeAmber.type = LightType.Point;
            ridgeAmber.color = new Color(1.0f, 0.65f, 0.25f);
            ridgeAmber.range = 80f;
            ridgeAmber.intensity = 1.1f;
        }

        private static Transform FindChildRecursive(Transform parent, string name)
        {
            if (parent == null) return null;
            if (parent.name == name) return parent;

            for (int i = 0; i < parent.childCount; i++)
            {
                var found = FindChildRecursive(parent.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
#endif
