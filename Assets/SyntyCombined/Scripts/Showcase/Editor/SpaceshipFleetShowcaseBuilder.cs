#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SyntyCombined.Showcase.Editor
{
    public static class SpaceshipFleetShowcaseBuilder
    {
        private const string SCENE_PATH = "Assets/Scenes/Showcase_AllSpaceships_FleetLineup.unity";
        private const string VEH_PATH = "Assets/PolygonSciFiSpace/Prefabs/Vehicles/";
        private const string BLD_PATH = "Assets/PolygonSciFiSpace/Prefabs/Buildings/";
        private const string ENV_PATH = "Assets/PolygonSciFiSpace/Prefabs/Environment/";
        private const string MAT_PATH = "Assets/PolygonSciFiSpace/Materials/Misc/";

        [MenuItem("Game/Build Spaceship Fleet Showcase Scene")]
        public static void BuildShowcaseScene()
        {
            // 1. Create or Open the Scene
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 2. Setup Space Skybox & Ambient Lighting
            Material skyboxMat = AssetDatabase.LoadAssetAtPath<Material>(MAT_PATH + "PolygonScifiSpace_Skybox_01.mat");
            if (skyboxMat != null)
            {
                RenderSettings.skybox = skyboxMat;
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.08f, 0.12f, 0.22f);

            // 3. Setup Directional Sunlight & Rim Lights
            GameObject sunGo = new GameObject("Cosmic_Sun_KeyLight");
            Light sunLight = sunGo.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.color = new Color(0.88f, 0.95f, 1.0f);
            sunLight.intensity = 1.35f;
            sunGo.transform.rotation = Quaternion.Euler(38f, -32f, 0f);

            GameObject rimGo = new GameObject("Nebula_Rim_Light");
            Light rimLight = rimGo.AddComponent<Light>();
            rimLight.type = LightType.Directional;
            rimLight.color = new Color(0.25f, 0.55f, 0.95f);
            rimLight.intensity = 0.65f;
            rimGo.transform.rotation = Quaternion.Euler(-25f, 150f, 0f);

            // 4. Create Root Containers
            GameObject rootEnvironment = new GameObject("[Environment]");
            GameObject rootRunway = new GameObject("[Runway_FlightDeck]");
            GameObject rootFleet = new GameObject("[Spaceship_Fleet]");
            GameObject rootDirector = new GameObject("[Showcase_Director]");

            // 5. Build Distant Cosmic Environment (Planets, Asteroids)
            BuildCosmicBackdrop(rootEnvironment.transform);

            // 6. Build Modular Flight Deck Runway
            BuildFlightDeckRunway(rootRunway.transform);

            // 7. Instantiate and Line Up All Spaceships
            var controller = rootDirector.AddComponent<SpaceshipFleetShowcaseController>();
            BuildFleetLineup(rootFleet.transform, controller);

            // 8. Setup Showcase Camera Rig
            GameObject camGo = new GameObject("Showcase_MainCamera");
            Camera cam = camGo.AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.fieldOfView = 56f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 2500f;
            cam.clearFlags = CameraClearFlags.Skybox;
            camGo.AddComponent<AudioListener>();

            controller.showcaseCamera = cam;
            GameObject rigGo = new GameObject("Showcase_CameraRig");
            rigGo.transform.SetParent(rootDirector.transform);
            controller.cameraRig = rigGo.transform;
            camGo.transform.SetParent(rigGo.transform, false);

            controller.overviewCamPos = new Vector3(85f, 75f, 210f);
            controller.overviewCamTarget = new Vector3(0f, 15f, 260f);
            controller.flybyStartPos = new Vector3(-24f, 14f, -40f);
            controller.flybyEndPos = new Vector3(-24f, 28f, 680f);
            controller.flybySpeed = 65f;

            // Focus on first ship initially
            controller.FocusOnShip(0, true);

            // 9. Save Scene
            EditorSceneManager.SaveScene(scene, SCENE_PATH);
            Debug.Log("<color=#00F0FF><b>[Spaceship Fleet Showcase]</b></color> Scene built successfully with all 32 spaceships and 6 orbital defense stations at " + SCENE_PATH);
        }

        private static void BuildCosmicBackdrop(Transform parent)
        {
            // Giant Ringed Planet (Aegis Prime)
            GameObject planetPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ENV_PATH + "SM_Env_Planet_01.prefab");
            GameObject ringsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ENV_PATH + "SM_Env_PlanetRings_01.prefab");

            if (planetPrefab != null)
            {
                GameObject planet = PrefabUtility.InstantiatePrefab(planetPrefab) as GameObject;
                planet.name = "Planet_Aegis_Prime";
                planet.transform.SetParent(parent);
                planet.transform.position = new Vector3(380f, 210f, 900f);
                planet.transform.localScale = Vector3.one * 150f;
                planet.transform.rotation = Quaternion.Euler(15f, -45f, 25f);

                if (ringsPrefab != null)
                {
                    GameObject rings = PrefabUtility.InstantiatePrefab(ringsPrefab) as GameObject;
                    rings.name = "Planet_Rings";
                    rings.transform.SetParent(planet.transform, false);
                    rings.transform.localPosition = Vector3.zero;
                    rings.transform.localScale = Vector3.one * 1.8f;
                    rings.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
                }
            }

            // Secondary Red Moon (Cydonia)
            GameObject moonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ENV_PATH + "SM_Env_Planet_03.prefab");
            if (moonPrefab != null)
            {
                GameObject moon = PrefabUtility.InstantiatePrefab(moonPrefab) as GameObject;
                moon.name = "Moon_Cydonia";
                moon.transform.SetParent(parent);
                moon.transform.position = new Vector3(-320f, 260f, 820f);
                moon.transform.localScale = Vector3.one * 55f;
            }

            // Distant Asteroid Fields
            GameObject astLarge = AssetDatabase.LoadAssetAtPath<GameObject>(ENV_PATH + "SM_Env_Asteroid_Large_01.prefab");
            if (astLarge != null)
            {
                GameObject ast1 = PrefabUtility.InstantiatePrefab(astLarge) as GameObject;
                ast1.name = "Asteroid_Cluster_West";
                ast1.transform.SetParent(parent);
                ast1.transform.position = new Vector3(-260f, 90f, 540f);
                ast1.transform.localScale = Vector3.one * 2.5f;

                GameObject ast2 = PrefabUtility.InstantiatePrefab(astLarge) as GameObject;
                ast2.name = "Asteroid_Cluster_East";
                ast2.transform.SetParent(parent);
                ast2.transform.position = new Vector3(320f, 130f, 620f);
                ast2.transform.localScale = Vector3.one * 3.0f;
                ast2.transform.rotation = Quaternion.Euler(40f, 85f, 15f);
            }
        }

        private static void BuildFlightDeckRunway(Transform parent)
        {
            GameObject floorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BLD_PATH + "SM_Bld_Floor_01.prefab");
            GameObject beaconPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(VEH_PATH + "SM_Veh_Beacon_01.prefab");
            GameObject padPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BLD_PATH + "SM_Bld_Landing_Platform_01.prefab");

            // Build Sleek Modular Runway Deck (X from -55m to +55m, Z from -60m to 760m)
            // Using 5m x 5m clean floor tiles in grid steps of 15m x 20m
            if (floorPrefab != null)
            {
                for (float z = -60f; z <= 780f; z += 20f)
                {
                    for (float x = -50f; x <= 50f; x += 25f)
                    {
                        GameObject floor = PrefabUtility.InstantiatePrefab(floorPrefab) as GameObject;
                        floor.name = string.Format("Runway_Deck_X{0:F0}_Z{1:F0}", x, z);
                        floor.transform.SetParent(parent);
                        floor.transform.position = new Vector3(x, -0.05f, z);
                        floor.transform.localScale = new Vector3(5f, 1f, 4f);
                    }
                }
            }

            // Runway Boundary Guidance Beacons (along both edges every 30m)
            if (beaconPrefab != null)
            {
                for (float z = -50f; z <= 760f; z += 30f)
                {
                    GameObject bW = PrefabUtility.InstantiatePrefab(beaconPrefab) as GameObject;
                    bW.name = string.Format("Runway_Beacon_West_Z{0:F0}", z);
                    bW.transform.SetParent(parent);
                    bW.transform.position = new Vector3(-55f, 0.1f, z);

                    GameObject bE = PrefabUtility.InstantiatePrefab(beaconPrefab) as GameObject;
                    bE.name = string.Format("Runway_Beacon_East_Z{0:F0}", z);
                    bE.transform.SetParent(parent);
                    bE.transform.position = new Vector3(55f, 0.1f, z);
                }
            }

            // Giant Hyperspace Stargate at the Rear of the Fleet (framing the Colossal Dreadnought)
            GameObject gatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(VEH_PATH + "SM_Veh_WarpGate_Outer_01.prefab");
            if (gatePrefab != null)
            {
                GameObject gate = PrefabUtility.InstantiatePrefab(gatePrefab) as GameObject;
                gate.name = "Fleet_Hyperspace_Stargate_Gateway";
                gate.transform.SetParent(parent);
                gate.transform.position = new Vector3(0f, 130f, 960f);
                gate.transform.localScale = Vector3.one * 1.6f;
                gate.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            }
        }

        private static void BuildFleetLineup(Transform parent, SpaceshipFleetShowcaseController controller)
        {
            // Full Registry of Spaceships to instantiate
            var shipSpecs = new (string path, string name, string shipClass, string role, Vector3 dimensions, Vector3 lineupPos, float camDist, Vector3 armadaPos)[]
            {
                // =========================================================================
                // WING 1: LIGHT STRIKE & RECON FIGHTERS (West Flight Line, X = -28m)
                // =========================================================================
                (VEH_PATH + "SM_Ship_Fighter_01.prefab", "SF-01 Ghost Interceptor", "LIGHT FIGHTER", "High-Speed Atmospheric & Void Interceptor", new Vector3(13.3f, 3.7f, 12.9f), new Vector3(-28f, 1.8f, 0f), 22f, new Vector3(-90f, 15f, 40f)),
                (VEH_PATH + "SM_Ship_Fighter_02.prefab", "SF-02 Viper Dogfighter", "LIGHT FIGHTER", "Air-Superiority Kinetic Dogfighter", new Vector3(15.0f, 4.5f, 13.7f), new Vector3(-28f, 2.2f, 28f), 24f, new Vector3(-70f, 18f, 70f)),
                (VEH_PATH + "SM_Ship_Fighter_03.prefab", "SF-03 Vanguard Strike Craft", "LIGHT FIGHTER", "Multi-Role Tactical Plasma Striker", new Vector3(19.7f, 4.6f, 12.5f), new Vector3(-28f, 2.3f, 56f), 26f, new Vector3(-50f, 20f, 100f)),
                (VEH_PATH + "SM_Ship_Fighter_04.prefab", "SF-04 Razor Interceptor", "LIGHT FIGHTER", "Low-Profile High-Atmosphere Skimmer", new Vector3(19.3f, 2.8f, 15.2f), new Vector3(-28f, 1.4f, 84f), 26f, new Vector3(-110f, 12f, 50f)),
                (VEH_PATH + "SM_Ship_Fighter_05.prefab", "SF-05 Scythe Scout", "LIGHT FIGHTER", "Reconnaissance & Electronic Warfare", new Vector3(17.8f, 2.7f, 14.8f), new Vector3(-28f, 1.4f, 112f), 25f, new Vector3(-130f, 10f, 60f)),

                // =========================================================================
                // WING 2: STEALTH INFILTRATORS & RECON (West Flight Line Extension, Z = 140m+)
                // =========================================================================
                (VEH_PATH + "SM_Ship_Stealth_01.prefab", "SS-01 Shadow Infiltrator", "STEALTH FIGHTER", "Deep Radar Absorption Stealth Recon", new Vector3(18.5f, 2.2f, 19.7f), new Vector3(-28f, 1.2f, 140f), 28f, new Vector3(-95f, 28f, 140f)),
                (VEH_PATH + "SM_Ship_Stealth_02.prefab", "SS-02 Phantom Stalker", "STEALTH FIGHTER", "Thermal-Cloaked Black Ops Interceptor", new Vector3(12.2f, 4.9f, 18.0f), new Vector3(-28f, 2.5f, 168f), 24f, new Vector3(-75f, 32f, 170f)),
                (VEH_PATH + "SM_Ship_Stealth_03.prefab", "SS-03 Spectre Penetrator", "STEALTH FIGHTER", "High-Atmosphere Shield Bypasser", new Vector3(9.3f, 7.4f, 15.7f), new Vector3(-28f, 3.7f, 196f), 22f, new Vector3(-55f, 35f, 200f)),
                (VEH_PATH + "SM_Ship_Stealth_04.prefab", "SS-04 Nightshade Striker", "STEALTH FIGHTER", "Precision Surgical Plasma Striker", new Vector3(9.3f, 5.3f, 15.7f), new Vector3(-28f, 2.7f, 224f), 22f, new Vector3(-115f, 25f, 150f)),
                (VEH_PATH + "SM_Ship_Stealth_05.prefab", "SS-05 Void Stalker Wing", "STEALTH FIGHTER", "Ultra-Wide Stealth Delta Interceptor", new Vector3(24.5f, 3.2f, 12.1f), new Vector3(-28f, 1.6f, 252f), 30f, new Vector3(-135f, 22f, 160f)),

                // =========================================================================
                // WING 3: HEAVY ASSAULT FIGHTERS & GUNSHIPS (East Flight Line, X = +28m)
                // =========================================================================
                (VEH_PATH + "SM_Ship_Fighter_Heavy_01.prefab", "HF-01 Titan Heavy Gunship", "HEAVY FIGHTER", "Twin-Engine Heavy Orbital Gunship", new Vector3(16.1f, 4.4f, 12.0f), new Vector3(28f, 2.2f, 0f), 25f, new Vector3(90f, 15f, 40f)),
                (VEH_PATH + "SM_Ship_Fighter_Heavy_02.prefab", "HF-02 Devastator Striker", "HEAVY FIGHTER", "Heavy Armored Kinetic Shock Fighter", new Vector3(16.6f, 5.6f, 13.4f), new Vector3(28f, 2.8f, 28f), 26f, new Vector3(70f, 18f, 70f)),
                (VEH_PATH + "SM_Ship_Fighter_Heavy_03.prefab", "HF-03 Juggernaut Raider", "HEAVY FIGHTER", "Armored Heavy Assault Raider", new Vector3(22.5f, 4.9f, 13.8f), new Vector3(28f, 2.5f, 56f), 28f, new Vector3(50f, 20f, 100f)),
                (VEH_PATH + "SM_Ship_Fighter_Heavy_04.prefab", "HF-04 Command Gunship", "HEAVY FIGHTER", "Squadron Command & Heavy Fire Support", new Vector3(15.5f, 3.9f, 16.2f), new Vector3(28f, 2.0f, 84f), 26f, new Vector3(110f, 12f, 50f)),

                // =========================================================================
                // WING 4: STRATEGIC BOMBERS & SIEGE CRAFT (East Flight Line Extension, Z = 112m+)
                // =========================================================================
                (VEH_PATH + "SM_Ship_Bomber_01.prefab", "SB-01 Torpedo Bomber", "STRATEGIC BOMBER", "Anti-Capital Proton Torpedo Bomber", new Vector3(30.1f, 5.3f, 21.9f), new Vector3(28f, 2.7f, 112f), 35f, new Vector3(95f, 28f, 140f)),
                (VEH_PATH + "SM_Ship_Bomber_02.prefab", "SB-02 Siege Fortress", "STRATEGIC BOMBER", "Heavy Planetary Bombardment Craft", new Vector3(26.4f, 6.3f, 25.5f), new Vector3(28f, 3.2f, 147f), 35f, new Vector3(75f, 32f, 170f)),
                (VEH_PATH + "SM_Ship_Bomber_03.prefab", "SB-03 Plasma Fortress", "STRATEGIC BOMBER", "High-Yield Thermonuclear Siege Craft", new Vector3(30.3f, 5.5f, 20.7f), new Vector3(28f, 2.8f, 182f), 36f, new Vector3(55f, 35f, 200f)),
                (VEH_PATH + "SM_Ship_Bomber_04.prefab", "SB-04 Void Breaker", "STRATEGIC BOMBER", "Sub-Orbital Armor Cracker Bomber", new Vector3(25.3f, 4.9f, 21.0f), new Vector3(28f, 2.5f, 217f), 34f, new Vector3(115f, 25f, 150f)),

                // =========================================================================
                // LINE 5: BATTLE CRUISERS & TACTICAL TRANSPORTS (Center-Anchored, Z = 280m to 440m)
                // =========================================================================
                (VEH_PATH + "SM_Ship_Cruiser_01.prefab", "BC-01 Sovereign Battlecruiser", "BATTLECRUISER", "Long-Range Beam Weapon Capital Cruiser", new Vector3(29.2f, 10.9f, 52.2f), new Vector3(-20f, 6.0f, 290f), 55f, new Vector3(-80f, 45f, 270f)),
                (VEH_PATH + "SM_Ship_Cruiser_02.prefab", "BC-02 Vanguard Battlecruiser", "BATTLECRUISER", "Fleet Command & Point Defense Cruiser", new Vector3(23.0f, 14.8f, 49.8f), new Vector3(20f, 8.0f, 290f), 55f, new Vector3(80f, 45f, 270f)),
                (VEH_PATH + "SM_Ship_Cruiser_03.prefab", "BC-03 Dread-Cruiser", "BATTLECRUISER", "Heavy Armored Siege Assault Cruiser", new Vector3(36.8f, 12.3f, 50.5f), new Vector3(0f, 7.0f, 350f), 60f, new Vector3(0f, 50f, 300f)),
                (VEH_PATH + "SM_Ship_Transport_01.prefab", "TR-01 Heavy Void Transport", "HEAVY TRANSPORT", "Armored Planetary Troop & Armor Hauler", new Vector3(48.9f, 45.3f, 137.3f), new Vector3(-35f, 23.0f, 420f), 90f, new Vector3(-130f, 60f, 340f)),
                (VEH_PATH + "SM_Ship_Galactic_Carrier_Armor_01.prefab", "GC-Armor Strike Carrier", "ESCORT CARRIER", "Armored Rapid-Deploy Carrier Section", new Vector3(31.9f, 14.5f, 169.1f), new Vector3(35f, 12.0f, 420f), 95f, new Vector3(130f, 60f, 340f)),

                // =========================================================================
                // LINE 6: SUPERCARRIERS & CAPITAL DREADNOUGHTS (High Docking Bay, Z = 500m+)
                // =========================================================================
                (VEH_PATH + "SM_Ship_Massive_Transport_01.prefab", "MT-01 Titan Bulk Carrier", "MASSIVE FREIGHTER", "Gargantuan Interstellar Resource Hauler (300m)", new Vector3(83.6f, 121.7f, 306.1f), new Vector3(-65f, 55.0f, 530f), 160f, new Vector3(-180f, 85f, 460f)),
                (VEH_PATH + "SM_Ship_Galactic_Carrier_01.prefab", "GC-01 Sovereign Supercarrier", "SUPERCARRIER", "Flagship Multi-Wing Galactic Fleet Carrier (294m)", new Vector3(101.9f, 75.3f, 293.7f), new Vector3(65f, 45.0f, 530f), 160f, new Vector3(180f, 85f, 460f)),
                (VEH_PATH + "SM_Ship_Colossal_01.prefab", "DREADNOUGHT // URM-SOVEREIGN", "COLOSSAL DREADNOUGHT", "Supreme Armada Flagship & Planetary Bombardment (612m)", new Vector3(128.2f, 174.6f, 611.7f), new Vector3(0f, 95.0f, 720f), 240f, new Vector3(0f, 120f, 550f))
            };

            GameObject padPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BLD_PATH + "SM_Bld_Landing_Platform_01.prefab");
            GameObject padsRoot = new GameObject("[Landing_Pads]");
            padsRoot.transform.SetParent(parent);

            for (int i = 0; i < shipSpecs.Length; i++)
            {
                var spec = shipSpecs[i];
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.path);
                if (prefab == null) continue;

                GameObject ship = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                ship.name = string.Format("Ship_{0:D2}_{1}", i + 1, prefab.name);
                ship.transform.SetParent(parent);
                ship.transform.position = spec.lineupPos;
                ship.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // Facing toward entrance runway

                // Individual Landing Platform under ship
                if (padPrefab != null)
                {
                    GameObject pad = PrefabUtility.InstantiatePrefab(padPrefab) as GameObject;
                    pad.name = "Pad_" + ship.name;
                    pad.transform.SetParent(padsRoot.transform);
                    pad.transform.position = new Vector3(spec.lineupPos.x, 0.02f, spec.lineupPos.z);
                    float padScaleX = Mathf.Clamp(spec.dimensions.x * 0.12f, 1.2f, 3.5f);
                    float padScaleZ = Mathf.Clamp(spec.dimensions.z * 0.12f, 1.2f, 3.5f);
                    pad.transform.localScale = new Vector3(padScaleX, 1f, padScaleZ);
                }

                // Create Holographic 3D Specs Badge
                GameObject badgeGo = new GameObject("HoloBadge_" + ship.name);
                badgeGo.transform.SetParent(ship.transform);
                badgeGo.transform.localPosition = new Vector3(0f, spec.dimensions.y * 0.75f + 3f, spec.dimensions.z * 0.4f);

                TextMesh tm = badgeGo.AddComponent<TextMesh>();
                tm.text = string.Format("<color=#00F0FF><b>{0}</b></color>\n<color=#FBBF24>{1}</color>\n<color=#94A3B8>{2:F1}m x {3:F1}m</color>", spec.name.ToUpper(), spec.shipClass, spec.dimensions.x, spec.dimensions.z);
                tm.fontSize = 24;
                tm.characterSize = 0.12f;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;

                // Add entry to controller
                var entry = new SpaceshipEntry
                {
                    displayName = spec.name,
                    shipClass = spec.shipClass,
                    tacticalRole = spec.role,
                    dimensions = spec.dimensions,
                    shipInstance = ship,
                    lineupPosition = spec.lineupPos,
                    lineupRotation = Quaternion.Euler(0f, 180f, 0f),
                    armadaPosition = spec.armadaPos,
                    armadaRotation = Quaternion.Euler(0f, 180f, 0f),
                    cameraDistance = spec.camDist,
                    cameraOffset = new Vector3(0f, spec.dimensions.y * 0.35f, 0f),
                    badgeTextMesh = tm
                };

                controller.fleet.Add(entry);
            }

            // =========================================================================
            // LINE 7: ORBITAL DEFENSE MEGASTATIONS (High Cosmic Geostationary Orbit Arc)
            // =========================================================================
            var stationSpecs = new (string path, string name, string role, Vector3 pos, float scale, Quaternion rot)[]
            {
                (VEH_PATH + "SM_Ship_Station_01.prefab", "Station Apex (Defense Hub)", "Orbital Defense Bastion", new Vector3(-350f, 220f, 850f), 0.6f, Quaternion.Euler(15f, 45f, 0f)),
                (VEH_PATH + "SM_Ship_Station_02.prefab", "Station Solaris (Research Ring)", "Planetary Research Ring", new Vector3(-180f, 260f, 920f), 0.7f, Quaternion.Euler(25f, -30f, 10f)),
                (VEH_PATH + "SM_Ship_Station_03.prefab", "Station Aegis (Solar Array)", "High-Yield Solar Harvester", new Vector3(0f, 280f, 980f), 0.8f, Quaternion.Euler(0f, 180f, 0f)),
                (VEH_PATH + "SM_Ship_Station_04.prefab", "Station Horizon (Spire Bastion)", "Refinery Deep Space Spire", new Vector3(180f, 250f, 920f), 0.7f, Quaternion.Euler(-15f, -60f, 10f)),
                (VEH_PATH + "SM_Ship_Station_05.prefab", "Station Citadel (Heavy Fortress)", "Heavy Armored Citadel Spire", new Vector3(350f, 210f, 850f), 0.6f, Quaternion.Euler(20f, 110f, 0f)),
                (VEH_PATH + "SM_Ship_Station_06.prefab", "Station Outpost (Observation Array)", "Tactical Sensor Outpost", new Vector3(480f, 190f, 760f), 0.8f, Quaternion.Euler(10f, -45f, 0f))
            };

            GameObject rootStations = new GameObject("[Orbital_Megastations]");
            rootStations.transform.SetParent(parent);

            foreach (var st in stationSpecs)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(st.path);
                if (prefab == null) continue;

                GameObject station = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                station.name = st.name;
                station.transform.SetParent(rootStations.transform);
                station.transform.position = st.pos;
                station.transform.localScale = Vector3.one * st.scale;
                station.transform.rotation = st.rot;
            }
        }
    }
}
#endif
