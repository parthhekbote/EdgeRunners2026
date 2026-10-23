using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using PolygonMech;
using Cyberpunk.Mars;
using InsaneGun.Combat;

namespace Cyberpunk.Mars.Editor
{
    /// <summary>
    /// Builds Scene_Mars_FinalBattle from the Synty Scavenger demo map:
    /// - Removes all buildings (refineries, silos, towers, catwalks, elevators, furniture)
    /// - Leaves all scraps, junk mounds, vehicle wrecks, and crates as-is
    /// - Strips alien vegetation/lake slime to create an authentic barren Martian wasteland
    /// - Creates a warm reddish Martian atmosphere, golden directional sun, procedural sky dome,
    ///   celestial Phobos in high orbit, swirling red dust storms, and cinematic ACES post-processing
    /// - Populates Alita, Colossus Titan War Mech boss, Martian starship, core pedestal, cutscene & battle directors
    /// </summary>
    public static class MarsScavengerSceneBuilder
    {
        private const string SourceScenePath = "Assets/Synty/PolygonSciFiWorlds/Scenes/Demo_Scavenger_01.unity";
        private const string TargetScenePath = "Assets/Scenes/Scene_Mars_FinalBattle.unity";
        private const string BackupScenePath = "Assets/Scenes/Scene_Mars_FinalBattle_ProceduralBackup.unity";

        // Verified open combat coordinates along the central corridor
        private static readonly Vector3 AlitaBasePos = new Vector3(5f, 0.1f, -15f);
        private static readonly Vector3 BossBasePos = new Vector3(5f, 0.05f, 12f);
        private static readonly Vector3 StarshipBasePos = new Vector3(5f, 1.8f, 48f);
        private static readonly Vector3 PedestalBasePos = new Vector3(5f, 0.20f, 54f);

        [MenuItem("Cyberpunk/Build Mars Final Battle (Scavenger Map)")]
        public static void Build()
        {
            if (File.Exists(TargetScenePath) && !File.Exists(BackupScenePath))
            {
                AssetDatabase.CopyAsset(TargetScenePath, BackupScenePath);
                Debug.Log($"[MarsScavengerSceneBuilder] Backed up original scene to {BackupScenePath}");
            }

            var scene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);

            // 1. Preserve wreckage prefabs in Building by reparenting to Props
            PreserveWreckages(scene);

            // 2. Strip buildings, building furniture, alien vegetation, and toxic slime
            int removedCount = StripNonMartianElements(scene);

            // 3. Remove old camera, lights, and global volumes
            RemoveSceneCamerasLightsAndVolumes(scene);

            // 4. Apply Martian Lighting, SkyDome, Phobos, and Fog
            var sunLight = ApplyMarsAtmosphere(scene);

            // 5. Add swirling red dust storm particle emitters
            AddDustEmitters();

            // 6. Setup ACES Post-Processing Volume
            SetupMarsVolumeProfile();

            // 7. Populate full gameplay (Alita, Titan Boss, Starship, Pedestal, Fleet, Directors)
            PopulateGameplay(sunLight);

            // 8. Save the generated scene
            EditorSceneManager.SaveScene(scene, TargetScenePath);
            Debug.Log($"[MarsScavengerSceneBuilder] Successfully generated and saved {TargetScenePath} ({removedCount} objects stripped; scraps preserved)");
        }

        // ── Preservation & Stripping ─────────────────────────────────────────────

        private static void PreserveWreckages(Scene scene)
        {
            var buildingRoot = GameObject.Find("Building");
            var propsRoot = GameObject.Find("Props") ?? new GameObject("Props");

            if (buildingRoot != null)
            {
                var wreckages = new List<Transform>();
                foreach (Transform child in buildingRoot.transform)
                {
                    if (child.name.StartsWith("SM_Bld_Scav_Wreckage"))
                    {
                        wreckages.Add(child);
                    }
                }

                foreach (var w in wreckages)
                {
                    w.SetParent(propsRoot.transform, true);
                    w.name = "Preserved_" + w.name;
                }
            }
        }

        private static int StripNonMartianElements(Scene scene)
        {
            var doomed = new List<GameObject>();

            // Delete Building root if present
            var buildingRoot = GameObject.Find("Building");
            if (buildingRoot != null) doomed.Add(buildingRoot);

            foreach (var go in AllObjects(scene))
            {
                if (go == null || go == buildingRoot) continue;
                string key = SourceName(go);
                if (ShouldRemove(key, go.name))
                {
                    doomed.Add(go);
                }
            }

            int count = 0;
            foreach (var go in doomed)
            {
                if (go == null) continue;
                UnityEngine.Object.DestroyImmediate(go);
                count++;
            }
            return count;
        }

        private static bool ShouldRemove(string sourceName, string goName)
        {
            if (string.IsNullOrEmpty(sourceName)) sourceName = goName ?? "";

            // PRESERVE: Scrap piles, vehicle wrecks, rocks, cliffs, dirt roads, ground, skeleton fossils, craters
            if (sourceName.StartsWith("SM_Bld_Scav_Wreckage") || goName.StartsWith("Preserved_")) return false;
            if (sourceName.StartsWith("SM_Prop_Scav_Scrap")) return false;
            if (sourceName.StartsWith("SM_Veh_")) return false;
            if (sourceName.StartsWith("SM_Env_Ground")) return false;
            if (sourceName.StartsWith("SM_Env_DirtRoad")) return false;
            if (sourceName.StartsWith("SM_Env_Cliff")) return false;
            if (sourceName.StartsWith("SM_Env_Rock")) return false;
            if (sourceName.StartsWith("SM_Env_Skeleton")) return false;
            if (sourceName.StartsWith("SM_Env_Detail")) return false;
            if (sourceName.StartsWith("SM_Env_Cracked")) return false;
            if (sourceName.StartsWith("SM_Env_Core")) return false;
            if (sourceName.StartsWith("SM_Prop_Crate")) return false;
            if (sourceName.StartsWith("SM_Prop_Tether")) return false;
            if (sourceName.StartsWith("SM_Prop_Drill")) return false;
            if (sourceName.StartsWith("SM_Prop_Cable")) return false;
            if (sourceName.StartsWith("SM_Prop_PowerCell")) return false;

            // REMOVE: Buildings, refineries, silos, pipes, railings, elevators, doors, windmills, graffiti
            if (sourceName.StartsWith("SM_Bld_")) return true;
            if (sourceName.StartsWith("SM_Decal_Graffiti")) return true;

            // REMOVE: Alien lush green vegetation and grass (Mars is a barren red desert)
            if (sourceName.StartsWith("SM_Env_Plant_")) return true;
            if (sourceName.StartsWith("SM_Env_Grass_")) return true;
            if (sourceName.StartsWith("SM_Env_LakeEdge")) return true;
            if (sourceName.StartsWith("SM_Env_Blob")) return true;

            // REMOVE: Alien green radioactive bubbles and insects
            if (sourceName.StartsWith("FX_Radioactive")) return true;
            if (sourceName.StartsWith("FX_Alien_Insects")) return true;

            // REMOVE: Building interior / vendor furniture
            if (sourceName.StartsWith("SM_Prop_Vendor") || sourceName.StartsWith("SM_Prop_Table") ||
                sourceName.StartsWith("SM_Prop_Chair") || sourceName.StartsWith("SM_Prop_Monitor") ||
                sourceName.StartsWith("SM_Prop_Locker") || sourceName.StartsWith("SM_Prop_Vending") ||
                sourceName.StartsWith("SM_Prop_Bed") || sourceName.StartsWith("SM_Prop_Sofa") ||
                sourceName.StartsWith("SM_Prop_Canopy") || sourceName.StartsWith("SM_Prop_Wall_Unit") ||
                sourceName.StartsWith("SM_Prop_Cardboard") || sourceName.StartsWith("SM_Prop_TrashBin") ||
                sourceName.StartsWith("SM_Prop_Junky"))
            {
                return true;
            }

            return false;
        }

        private static string SourceName(GameObject go)
        {
            if (PrefabUtility.IsOutermostPrefabInstanceRoot(go))
            {
                var src = PrefabUtility.GetCorrespondingObjectFromSource(go);
                if (src != null) return Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(src));
            }
            return go.name;
        }

        private static void RemoveSceneCamerasLightsAndVolumes(Scene scene)
        {
            foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (c != null && c.gameObject.scene == scene) UnityEngine.Object.DestroyImmediate(c.gameObject);
            }

            foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (l != null && l.gameObject.scene == scene && l.type == LightType.Directional)
                {
                    UnityEngine.Object.DestroyImmediate(l.gameObject);
                }
            }

            foreach (var v in UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (v != null && v.gameObject.scene == scene) UnityEngine.Object.DestroyImmediate(v.gameObject);
            }
        }

        // ── Authentic Martian Atmosphere ─────────────────────────────────────────

        private static Light ApplyMarsAtmosphere(Scene scene)
        {
            // Red dust fog
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0032f;
            RenderSettings.fogColor = new Color(0.84f, 0.44f, 0.24f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.74f, 0.40f, 0.25f);

            // Bright golden-amber Martian directional sun
            var sunGO = new GameObject("Martian_Sun");
            var sun = sunGO.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1.0f, 0.85f, 0.70f);
            sun.intensity = 2.5f;
            sun.shadows = LightShadows.Soft;
            sun.shadowBias = 0.05f;
            sunGO.transform.rotation = Quaternion.Euler(38f, -35f, 0f);
            RenderSettings.sun = sun;

            // Warm amber-red atmospheric bounce fill
            var fillGO = new GameObject("Martian_Atmospheric_Fill");
            var fill = fillGO.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.92f, 0.44f, 0.22f);
            fill.intensity = 0.70f;
            fill.shadows = LightShadows.None;
            fillGO.transform.rotation = Quaternion.Euler(-35f, 145f, 0f);

            // Procedural SkyDome wrapping the Martian horizon
            var skyDomePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonSciFiWorlds/Prefabs/FX/SkyDome.prefab");
            if (skyDomePrefab != null)
            {
                var skyDome = (GameObject)PrefabUtility.InstantiatePrefab(skyDomePrefab);
                skyDome.name = "Martian_SkyDome";
                skyDome.transform.position = new Vector3(0f, -50f, 0f);
                skyDome.transform.localScale = Vector3.one * 1400f;
                var skyMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Synty/PolygonSciFiWorlds/Materials/Sky/Skybox_Scav_01.mat");
                if (skyMat != null)
                {
                    foreach (var rend in skyDome.GetComponentsInChildren<Renderer>())
                    {
                        rend.sharedMaterial = skyMat;
                        rend.shadowCastingMode = ShadowCastingMode.Off;
                    }
                }
            }

            // Celestial Moon Phobos in high Martian orbit
            var planetPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonSciFiSpace/Prefabs/Environment/SM_Env_Planet_01.prefab");
            if (planetPrefab != null)
            {
                var phobos = (GameObject)PrefabUtility.InstantiatePrefab(planetPrefab);
                phobos.name = "Celestial_Phobos";
                phobos.transform.position = new Vector3(180f, 220f, 380f);
                phobos.transform.localScale = Vector3.one * 65f;
                phobos.transform.rotation = Quaternion.Euler(22f, 195f, 15f);
                var rend = phobos.GetComponentInChildren<Renderer>();
                if (rend != null) rend.shadowCastingMode = ShadowCastingMode.Off;
            }

            return sun;
        }

        private static void AddDustEmitters()
        {
            var dustPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonSciFiWorlds/Prefabs/FX/FX_Dust_Blowing_Scav_01.prefab");
            if (dustPrefab == null) return;

            var root = new GameObject("Atmospheric_Dust_Emitters");
            Vector3[] positions =
            {
                new Vector3(5f, 0.2f, -15f),
                new Vector3(5f, 0.2f, 12f),
                new Vector3(-20f, 0.2f, -10f),
                new Vector3(25f, 0.2f, -10f),
                new Vector3(-15f, 0.2f, 25f),
                new Vector3(20f, 0.2f, 25f),
                new Vector3(5f, 0.2f, 48f)
            };

            for (int i = 0; i < positions.Length; i++)
            {
                var d = (GameObject)PrefabUtility.InstantiatePrefab(dustPrefab, root.transform);
                d.name = $"Dust_Emitter_{i + 1}";
                d.transform.position = SnapToGround(positions[i]);
            }
        }

        private static void SetupMarsVolumeProfile()
        {
            var volumeGO = new GameObject("Global Volume");
            var volume = volumeGO.AddComponent<Volume>();
            volume.isGlobal = true;

            var marsProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            marsProfile.name = "Mars_Scavenger_Battle_Profile";

            var bloom = marsProfile.Add<Bloom>(true);
            bloom.threshold.Override(0.85f);
            bloom.intensity.Override(1.3f);
            bloom.scatter.Override(0.7f);

            var colorAdj = marsProfile.Add<ColorAdjustments>(true);
            colorAdj.postExposure.Override(0.75f);
            colorAdj.contrast.Override(18f);
            colorAdj.saturation.Override(14f);
            colorAdj.colorFilter.Override(new Color(1.0f, 0.96f, 0.92f));

            var whiteBalance = marsProfile.Add<WhiteBalance>(true);
            whiteBalance.temperature.Override(16f); // Warm sandstone warmth

            var tonemapping = marsProfile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.ACES);

            var vignette = marsProfile.Add<Vignette>(true);
            vignette.intensity.Override(0.24f);
            vignette.smoothness.Override(0.35f);

            volume.sharedProfile = marsProfile;
            AssetDatabase.CreateAsset(marsProfile, "Assets/Materials/Mars_Scavenger_Battle_VolumeProfile.asset");
        }

        // ── Gameplay Population ──────────────────────────────────────────────────

        private static void PopulateGameplay(Light sunLight)
        {
            Vector3 alitaPos = SnapToGround(AlitaBasePos);
            Vector3 bossPos = SnapToGround(BossBasePos);
            Vector3 shipPos = SnapToGround(StarshipBasePos);
            Vector3 pedestalPos = SnapToGround(PedestalBasePos);

            // 1. Player Setup (ALITA - Battle Angel Hero)
            GameObject playerInstance = null;
            var alitaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Alita_BattleAngel_Hero.prefab");
            InsaneGunSwordController swordCtrl = null;

            if (alitaPrefab != null)
            {
                playerInstance = (GameObject)PrefabUtility.InstantiatePrefab(alitaPrefab);
                playerInstance.name = "Alita";
                playerInstance.transform.position = alitaPos;
                playerInstance.transform.rotation = Quaternion.identity;
                playerInstance.tag = "Player";
                playerInstance.layer = LayerMask.NameToLayer("Player");

                swordCtrl = playerInstance.GetComponent<InsaneGunSwordController>();
                if (swordCtrl != null)
                {
                    swordCtrl.controlMode = InsaneGunSwordController.ControlMode.PlayerControlled;
                    swordCtrl.groundLayers = ~0;
                    swordCtrl.enemyLayers = LayerMask.GetMask("Enemy");
                }
            }

            // 2. Boss Setup (PF_HammerMechBoss)
            var bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonMech/Prefabs/Vehicles/PF_HammerMechBoss.prefab");
            GameObject bossInstance = null;
            if (bossPrefab != null)
            {
                bossInstance = (GameObject)PrefabUtility.InstantiatePrefab(bossPrefab);
                bossInstance.name = "PF_HammerMechBoss";
                bossInstance.transform.position = bossPos;
                bossInstance.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

                int enemyLayer = LayerMask.NameToLayer("Enemy");
                bossInstance.layer = enemyLayer;
                foreach (Transform child in bossInstance.GetComponentsInChildren<Transform>(true))
                {
                    child.gameObject.layer = enemyLayer;
                }

                var ai = bossInstance.GetComponent<HammerMechAI>();
                if (ai != null && playerInstance != null)
                {
                    var serializedAI = new SerializedObject(ai);
                    serializedAI.FindProperty("target").objectReferenceValue = playerInstance.transform;
                    serializedAI.ApplyModifiedProperties();
                }

                if (swordCtrl != null)
                {
                    swordCtrl.targetEnemy = bossInstance.transform;
                }

                var bossBar = bossInstance.GetComponent<BossHealthBar>() ?? bossInstance.AddComponent<BossHealthBar>();
                bossBar.bossName = "COLOSSUS-06 // TITAN WAR MECH";
                bossBar.bossSub = "HEAVY KINETIC SIEGE UNIT // MARS EXPEDITIONARY FORCE";
            }

            // 3. Main Camera & SmoothOrbitCamera (Alita's 3rd Person Combat Camera)
            GameObject camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            camGO.transform.position = alitaPos + new Vector3(0.48f, 2.1f, -3.5f);
            camGO.transform.rotation = Quaternion.Euler(12f, 0f, 0f);

            var cam = camGO.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.2f;
            cam.farClipPlane = 1600f;
            camGO.AddComponent<AudioListener>();

            var camData = camGO.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;

            var orbitCam = camGO.AddComponent<SmoothOrbitCamera>();
            if (orbitCam != null && playerInstance != null)
            {
                orbitCam.target = playerInstance.transform;
                orbitCam.targetOffset = new Vector3(0f, 1.4f, 0f);
                orbitCam.cameraOffset = new Vector3(0.48f, 0.08f, -3.2f);
                orbitCam.tightPositionFollow = true;
            }

            // 4. Starship (Alita's Martian Starship)
            GameObject shipInstance = null;
            var shipPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonSciFiSpace/Prefabs/Vehicles/SM_Ship_Stealth_01.prefab");
            if (shipPrefab != null)
            {
                shipInstance = (GameObject)PrefabUtility.InstantiatePrefab(shipPrefab);
                shipInstance.name = "Alita_Martian_Starship";
                shipInstance.transform.position = shipPos;
                shipInstance.transform.rotation = Quaternion.identity;

                GameObject lightGO = new GameObject("Thruster_Point_Light");
                lightGO.transform.SetParent(shipInstance.transform, false);
                lightGO.transform.localPosition = new Vector3(0f, 0.4f, -7.5f);
                var tLight = lightGO.AddComponent<Light>();
                tLight.type = LightType.Point;
                tLight.color = new Color(0.1f, 0.85f, 1.0f);
                tLight.range = 25f;
                tLight.intensity = 3.5f;
            }

            // 5. Ancient URM Antimatter Core Pedestal
            GameObject corePedestalGO = new GameObject("Mars_Antimatter_Core_Pedestal");
            corePedestalGO.transform.position = pedestalPos;
            var corePedestal = corePedestalGO.AddComponent<MarsAntimatterCorePedestal>();

            // 6. Imperial Star Destroyer Fleet
            GameObject fleetGO = new GameObject("Mars_StarDestroyer_Fleet");
            var fleet = fleetGO.AddComponent<MarsStarDestroyerFleet>();

            // 7. Invasion Pilot Army Manager
            GameObject armyGO = new GameObject("Mars_Invasion_Army_Manager");
            var armyManager = armyGO.AddComponent<MarsInvasionArmyManager>();

            // 8. Alita Supercharge Aura
            AlitaSuperchargeAura superchargeAura = null;
            if (playerInstance != null)
            {
                superchargeAura = playerInstance.GetComponent<AlitaSuperchargeAura>() ?? playerInstance.AddComponent<AlitaSuperchargeAura>();
            }

            // 9. Directors & Missions
            GameObject directorGO = new GameObject("Mars_Final_Battle_Director");
            var director = directorGO.AddComponent<MarsFinalBattleDirector>();
            if (bossInstance != null) director.bossAI = bossInstance.GetComponent<HammerMechAI>();
            if (playerInstance != null) director.playerTransform = playerInstance.transform;

            director.combatMusic = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/tatamusic-battle-warrior-fighting-drums-478210.mp3");
            director.dustStormAmbient = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/sci-fi-drone-sound-effect.mp3");
            director.martianSunLight = sunLight;

            var stormSystem = directorGO.AddComponent<MartianDustStormSystem>();
            stormSystem.martianSun = sunLight;
            stormSystem.starship = shipInstance;

            var cutsceneDirector = directorGO.AddComponent<MarsArrivalCutsceneDirector>();
            cutsceneDirector.alitaHero = playerInstance;
            cutsceneDirector.bossMech = bossInstance;
            cutsceneDirector.starship = shipInstance;
            cutsceneDirector.mainCamera = cam;
            cutsceneDirector.dustStormSystem = stormSystem;
            cutsceneDirector.corePedestal = corePedestal;
            cutsceneDirector.starDestroyerFleet = fleet;
            cutsceneDirector.armyManager = armyManager;
            cutsceneDirector.superchargeAura = superchargeAura;

            var coreMission = directorGO.AddComponent<MarsCoreRetrievalMission>();
            coreMission.alitaHero = playerInstance;
            coreMission.corePedestal = corePedestal;
            coreMission.cutsceneDirector = cutsceneDirector;
            coreMission.superchargeAura = superchargeAura;
        }

        public static Vector3 SnapToGround(Vector3 p)
        {
            Physics.SyncTransforms();
            if (Physics.Raycast(new Vector3(p.x, 300f, p.z), Vector3.down, out var hit, 600f, ~0, QueryTriggerInteraction.Ignore))
            {
                return new Vector3(p.x, hit.point.y + p.y, p.z);
            }
            return p;
        }

        private static IEnumerable<GameObject> AllObjects(Scene scene)
        {
            var stack = new Stack<Transform>(scene.GetRootGameObjects().Select(g => g.transform));
            var list = new List<GameObject>();
            while (stack.Count > 0)
            {
                var t = stack.Pop();
                list.Add(t.gameObject);
                foreach (Transform c in t) stack.Push(c);
            }
            return list;
        }
    }
}
