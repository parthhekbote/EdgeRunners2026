using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using PolygonMech;
using Cyberpunk.Mars;
using InsaneGun.Combat;

namespace Cyberpunk.Mars.Editor
{
    public static class MarsSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Scene_Mars_FinalBattle.unity";

        [MenuItem("Cyberpunk/Build Mars Final Battle Scene")]
        public static void BuildMarsFinalBattleScene()
        {
            // 1. Create a brand new clean scene
            var newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 2. Setup Lighting & Atmospheric Fog Settings
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0028f; // Clear long-distance view across the vast Martian basin
            RenderSettings.fogColor = new Color(0.84f, 0.44f, 0.24f); // Warm Martian Red Horizon Haze
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.74f, 0.40f, 0.25f); // Warm Martian ambient bounce

            // 3. Directional Sun & Atmospheric Fill
            GameObject sunGO = new GameObject("Martian_Sun");
            var sunLight = sunGO.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.color = new Color(1.0f, 0.85f, 0.70f); // Bright golden-amber sun
            sunLight.intensity = 2.4f;
            sunLight.shadows = LightShadows.Soft;
            sunGO.transform.rotation = Quaternion.Euler(36f, -32f, 0f);

            GameObject fillLightGO = new GameObject("Martian_Atmospheric_Fill");
            var fillLight = fillLightGO.AddComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.color = new Color(0.90f, 0.42f, 0.24f);
            fillLight.intensity = 0.65f;
            fillLight.shadows = LightShadows.None;
            fillLightGO.transform.rotation = Quaternion.Euler(-35f, 148f, 0f);

            // 4. SkyDome (wraps entire skybox horizon)
            var skyDomePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonSciFiWorlds/Prefabs/FX/SkyDome.prefab");
            if (skyDomePrefab != null)
            {
                var skyDome = (GameObject)PrefabUtility.InstantiatePrefab(skyDomePrefab);
                skyDome.name = "Martian_SkyDome";
                skyDome.transform.position = new Vector3(0f, -60f, 0f);
                skyDome.transform.localScale = Vector3.one * 1200f; // Encompasses the entire 700m terrain

                var skyMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Synty/PolygonSciFiWorlds/Materials/Sky/Skybox_Scav_01.mat");
                if (skyMat != null)
                {
                    var rends = skyDome.GetComponentsInChildren<Renderer>();
                    foreach (var rend in rends)
                    {
                        rend.sharedMaterial = skyMat;
                        rend.shadowCastingMode = ShadowCastingMode.Off;
                    }
                }
            }

            // 5. Celestial Moon (Phobos) in High Martian Orbit
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

            // 6. Huge Martian Land & Environment Parent
            GameObject envRoot = new GameObject("--- [ENVIRONMENT - MARS VAST EXPANSE] ---");

            var scavMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Synty/PolygonSciFiWorlds/Materials/Environment/PolygonSciFiWorlds_Env_Triplanar_Scav_01.mat");

            // 6A. Procedural Vast 700m x 700m Continuous Martian Terrain Basin
            CreateHugeMartianBasin(envRoot.transform, scavMat);

            // 6B. Impact Craters Embedded on the Martian Dunes
            string[] craterPrefabs = new string[]
            {
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Environments/SM_Env_Detail_Crater_01.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Environments/SM_Env_Detail_Crater_02.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Environments/SM_Env_Detail_Crater_03.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Environments/SM_Env_Detail_Crater_04.prefab"
            };

            Vector3[] craterPositions = new Vector3[]
            {
                new Vector3(45f, 0.2f, 50f),
                new Vector3(-55f, 0.2f, 40f),
                new Vector3(60f, 0.2f, -40f),
                new Vector3(-45f, 0.2f, -55f),
                new Vector3(85f, 0.3f, 15f),
                new Vector3(-88f, 0.3f, -18f)
            };

            GameObject cratersRoot = new GameObject("Martian_Impact_Craters");
            cratersRoot.transform.SetParent(envRoot.transform);

            for (int i = 0; i < craterPositions.Length; i++)
            {
                var cpPath = craterPrefabs[i % craterPrefabs.Length];
                var cp = AssetDatabase.LoadAssetAtPath<GameObject>(cpPath);
                if (cp != null)
                {
                    var c = (GameObject)PrefabUtility.InstantiatePrefab(cp, cratersRoot.transform);
                    c.name = $"Impact_Crater_{i + 1}";
                    c.transform.position = craterPositions[i];
                    c.transform.rotation = Quaternion.Euler(0f, i * 67f, 0f);
                    c.transform.localScale = Vector3.one * UnityEngine.Random.Range(1.3f, 2.2f);
                }
            }

            // 6C. Large Martian Sandstone Boulders
            string[] rockPrefabs = new string[]
            {
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Environments/SM_Env_Rock_Large_01.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Environments/SM_Env_Rock_Large_02.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Environments/SM_Env_Rock_Large_03.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Environments/SM_Env_Rock_Large_04.prefab"
            };

            GameObject rocksRoot = new GameObject("Martian_Sandstone_Boulders");
            rocksRoot.transform.SetParent(envRoot.transform);

            int rockCount = 14;
            for (int i = 0; i < rockCount; i++)
            {
                float angle = i * (360f / rockCount) + 14f;
                float dist = UnityEngine.Random.Range(52f, 110f);
                float rad = angle * Mathf.Deg2Rad;
                Vector3 rPos = new Vector3(Mathf.Sin(rad) * dist, 0.1f, Mathf.Cos(rad) * dist);

                var rp = AssetDatabase.LoadAssetAtPath<GameObject>(rockPrefabs[i % rockPrefabs.Length]);
                if (rp != null)
                {
                    var rObj = (GameObject)PrefabUtility.InstantiatePrefab(rp, rocksRoot.transform);
                    rObj.name = $"Martian_Boulder_{i + 1}";
                    rObj.transform.position = rPos;
                    rObj.transform.rotation = Quaternion.Euler(UnityEngine.Random.Range(-5f, 5f), UnityEngine.Random.Range(0f, 360f), 0f);
                    rObj.transform.localScale = Vector3.one * UnityEngine.Random.Range(1.5f, 2.6f);
                }
            }

            // 6D. Towering Geological Rock Spires
            string[] spikePrefabs = new string[]
            {
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Environments/SM_Env_Rock_Spike_01.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Environments/SM_Env_Rock_Spike_02.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Environments/SM_Env_Rock_Spike_03.prefab",
                "Assets/Synty/PolygonSciFiWorlds/Prefabs/Environments/SM_Env_Rock_Spike_04.prefab"
            };

            GameObject spiresRoot = new GameObject("Martian_Geological_Spires");
            spiresRoot.transform.SetParent(envRoot.transform);

            int spireCount = 10;
            for (int i = 0; i < spireCount; i++)
            {
                float angle = i * (360f / spireCount) + 20f;
                float dist = UnityEngine.Random.Range(90f, 160f);
                float rad = angle * Mathf.Deg2Rad;
                Vector3 sPos = new Vector3(Mathf.Sin(rad) * dist, 0.2f, Mathf.Cos(rad) * dist);

                var sp = AssetDatabase.LoadAssetAtPath<GameObject>(spikePrefabs[i % spikePrefabs.Length]);
                if (sp != null)
                {
                    var sObj = (GameObject)PrefabUtility.InstantiatePrefab(sp, spiresRoot.transform);
                    sObj.name = $"Rock_Spire_{i + 1}";
                    sObj.transform.position = sPos;
                    sObj.transform.rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
                    sObj.transform.localScale = new Vector3(1.8f, UnityEngine.Random.Range(2.4f, 3.8f), 1.8f);
                }
            }

            // 6E. Ancient Alien Ruin Monolith Pillars (Perimeter of the Central Arena)
            var ruinPrefab1 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonSciFiWorlds/Prefabs/Environments/SM_Env_Artifact_AlienRuin_01.prefab");
            var ruinPrefab2 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonSciFiWorlds/Prefabs/Environments/SM_Env_Artifact_AlienRuin_02.prefab");

            if (ruinPrefab1 != null && ruinPrefab2 != null)
            {
                GameObject ruinsRoot = new GameObject("Ancient_Martian_Ruins");
                ruinsRoot.transform.SetParent(envRoot.transform);

                Vector3[] ruinPositions = new Vector3[]
                {
                    new Vector3(0f, 0f, 30f),
                    new Vector3(0f, 0f, -30f),
                    new Vector3(30f, 0f, 0f),
                    new Vector3(-30f, 0f, 0f)
                };

                for (int i = 0; i < ruinPositions.Length; i++)
                {
                    var p = (i % 2 == 0) ? ruinPrefab1 : ruinPrefab2;
                    var r = (GameObject)PrefabUtility.InstantiatePrefab(p, ruinsRoot.transform);
                    r.name = $"Martian_Pillar_{i + 1}";
                    r.transform.position = ruinPositions[i];
                    r.transform.rotation = Quaternion.Euler(0f, i * 90f + 45f, 0f);
                    r.transform.localScale = Vector3.one * 1.5f;
                }
            }

            // 6F. Atmospheric Red Dust Storm Emitters across the Vast Plain
            var dustPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonSciFiWorlds/Prefabs/FX/FX_Dust_Blowing_Scav_01.prefab");
            if (dustPrefab != null)
            {
                GameObject dustRoot = new GameObject("Atmospheric_Dust_Emitters");
                dustRoot.transform.SetParent(envRoot.transform);

                Vector3[] dustPos = new Vector3[]
                {
                    new Vector3(0f, 0.2f, 0f),
                    new Vector3(-25f, 0.2f, 20f),
                    new Vector3(25f, 0.2f, 20f),
                    new Vector3(0f, 0.2f, -25f),
                    new Vector3(-50f, 0.2f, -40f),
                    new Vector3(50f, 0.2f, 45f)
                };

                for (int i = 0; i < dustPos.Length; i++)
                {
                    var dust = (GameObject)PrefabUtility.InstantiatePrefab(dustPrefab, dustRoot.transform);
                    dust.name = $"Dust_Emitter_{i + 1}";
                    dust.transform.position = dustPos[i];
                }
            }

            PopulateGameplay(sunLight, false);

            // 13. Save Scene
            EditorSceneManager.SaveScene(newScene, ScenePath);
            Debug.Log($"[MarsSceneBuilder] Alita vs Titan Mech on Huge Martian Land Scene with Dust Storm & Cutscene built and saved to {ScenePath}");
        }

        /// <summary>
        /// Adds volume, Alita, the Titan boss, camera, starship, directors and the core pedestal to the
        /// active scene. When snapToGround is true, every spawn point is dropped onto the scene's colliders.
        /// </summary>
        public static void PopulateGameplay(Light sunLight, bool snapToGround)
        {
            Vector3 G(Vector3 p) => snapToGround ? SnapToGround(p) : p;

            // 7. Global Volume & Post-Processing (Mars Cinematic Tuning)
            GameObject volumeGO = new GameObject("Global Volume");
            var volume = volumeGO.AddComponent<Volume>();
            volume.isGlobal = true;

            var marsProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            marsProfile.name = "Mars_Battle_Profile";

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
            AssetDatabase.CreateAsset(marsProfile, "Assets/Materials/Mars_Battle_VolumeProfile.asset");

            // 8. Player Setup (ALITA - Battle Angel Hero)
            GameObject playerInstance = null;
            var alitaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Alita_BattleAngel_Hero.prefab");
            InsaneGunSwordController swordCtrl = null;

            if (alitaPrefab != null)
            {
                playerInstance = (GameObject)PrefabUtility.InstantiatePrefab(alitaPrefab);
                playerInstance.name = "Alita";
                playerInstance.transform.position = G(new Vector3(0f, 0.1f, -12f));
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

            // 9. Boss Setup (PF_HammerMechBoss)
            var bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonMech/Prefabs/Vehicles/PF_HammerMechBoss.prefab");
            GameObject bossInstance = null;
            if (bossPrefab != null)
            {
                bossInstance = (GameObject)PrefabUtility.InstantiatePrefab(bossPrefab);
                bossInstance.name = "PF_HammerMechBoss";
                bossInstance.transform.position = G(new Vector3(0f, 0.05f, 10f));
                bossInstance.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

                // Set Boss and all children to Enemy layer so Alita can target and damage it
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

                var bossBar = bossInstance.GetComponent<BossHealthBar>();
                if (bossBar == null) bossBar = bossInstance.AddComponent<BossHealthBar>();
                bossBar.bossName = "COLOSSUS-06 // TITAN WAR MECH";
                bossBar.bossSub = "HEAVY KINETIC SIEGE UNIT // MARS EXPEDITIONARY FORCE";
            }

            // 10. Main Camera & SmoothOrbitCamera (Alita's 3rd Person Combat Camera)
            GameObject camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            camGO.transform.position = new Vector3(0f, 2.2f, -15.5f);
            camGO.transform.rotation = Quaternion.Euler(10f, 0f, 0f);

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

            // 11. Starship (Alita's Martian Starship)
            GameObject shipInstance = null;
            var shipPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonSciFiSpace/Prefabs/Vehicles/SM_Ship_Stealth_01.prefab");
            if (shipPrefab != null)
            {
                shipInstance = (GameObject)PrefabUtility.InstantiatePrefab(shipPrefab);
                shipInstance.name = "Alita_Martian_Starship";
                shipInstance.transform.position = G(new Vector3(0f, 2.2f, 58f));
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

            // 12. Mars Final Battle Director & Cutscene Director
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

            // 12B. Ancient URM Antimatter Core Pedestal
            GameObject corePedestalGO = new GameObject("Mars_Antimatter_Core_Pedestal");
            corePedestalGO.transform.position = G(new Vector3(0f, 0.20f, 67.50f));
            var corePedestal = corePedestalGO.AddComponent<MarsAntimatterCorePedestal>();

            // 12C. Imperial Star Destroyer Fleet (Dual Dreadnoughts)
            GameObject fleetGO = new GameObject("Mars_StarDestroyer_Fleet");
            var fleet = fleetGO.AddComponent<MarsStarDestroyerFleet>();

            // 12D. Invasion Pilot Army Manager
            GameObject armyGO = new GameObject("Mars_Invasion_Army_Manager");
            var armyManager = armyGO.AddComponent<MarsInvasionArmyManager>();

            // 12E. Alita Supercharge Aura
            AlitaSuperchargeAura superchargeAura = null;
            if (playerInstance != null)
            {
                superchargeAura = playerInstance.GetComponent<AlitaSuperchargeAura>() ?? playerInstance.AddComponent<AlitaSuperchargeAura>();
            }

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

        /// <summary>Drops a point onto the highest collider below it, keeping its original height as an offset.</summary>
        public static Vector3 SnapToGround(Vector3 p)
        {
            Physics.SyncTransforms();
            if (Physics.Raycast(new Vector3(p.x, 300f, p.z), Vector3.down, out var hit, 600f, ~0, QueryTriggerInteraction.Ignore))
                return new Vector3(p.x, hit.point.y + p.y, p.z);
            return p;
        }

        /// <summary>
        /// Generates a colossal 700m x 700m procedural Martian terrain basin with rolling sand dunes,
        /// a perfectly level central combat ring, rising canyon mountain ridges on the horizon,
        /// triplanar Martian regolith material, and seamless mesh colliders.
        /// </summary>
        private static GameObject CreateHugeMartianBasin(Transform parent, Material mat)
        {
            GameObject terrainGO = new GameObject("Martian_Terrain_Vast_Expanse");
            terrainGO.transform.SetParent(parent);
            terrainGO.transform.position = Vector3.zero;

            int resolution = 70; // 70x70 quads = 5041 vertices
            float size = 700f;   // 700 meters wide
            float halfSize = size * 0.5f;

            Vector3[] vertices = new Vector3[(resolution + 1) * (resolution + 1)];
            Vector2[] uvs = new Vector2[vertices.Length];
            int[] triangles = new int[resolution * resolution * 6];

            for (int z = 0; z <= resolution; z++)
            {
                for (int x = 0; x <= resolution; x++)
                {
                    int idx = z * (resolution + 1) + x;
                    float worldX = (float)x / resolution * size - halfSize;
                    float worldZ = (float)z / resolution * size - halfSize;
                    float dist = Mathf.Sqrt(worldX * worldX + worldZ * worldZ);
                    float angle = Mathf.Atan2(worldZ, worldX);

                    // Central combat zone (radius < 45m) is perfectly level at y = 0
                    float y = 0f;
                    if (dist > 45f)
                    {
                        float innerBlend = Mathf.Clamp01((dist - 45f) / 25f);
                        // Gentle rolling dunes in the mid-range
                        float dunes = Mathf.Sin(worldX * 0.025f) * Mathf.Cos(worldZ * 0.025f) * 2.2f;

                        // Outer canyon mountain range rising to 44m+ on the horizon
                        float mountainRise = 0f;
                        if (dist > 110f)
                        {
                            float mFactor = Mathf.Clamp01((dist - 110f) / 200f);
                            float ridges = Mathf.Sin(angle * 7f) * 7f + Mathf.Cos(angle * 11f) * 4f;
                            mountainRise = Mathf.Pow(mFactor, 1.7f) * (44f + ridges);
                        }

                        y = (dunes + mountainRise) * innerBlend;
                    }

                    vertices[idx] = new Vector3(worldX, y, worldZ);
                    uvs[idx] = new Vector2((float)x / resolution * 60f, (float)z / resolution * 60f);
                }
            }

            int t = 0;
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int row1 = z * (resolution + 1) + x;
                    int row2 = (z + 1) * (resolution + 1) + x;

                    triangles[t++] = row1;
                    triangles[t++] = row2;
                    triangles[t++] = row1 + 1;

                    triangles[t++] = row1 + 1;
                    triangles[t++] = row2;
                    triangles[t++] = row2 + 1;
                }
            }

            Mesh mesh = new Mesh();
            mesh.name = "Martian_Vast_Terrain_Mesh";
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var mf = terrainGO.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            var mr = terrainGO.AddComponent<MeshRenderer>();
            if (mat != null) mr.sharedMaterial = mat;

            var mc = terrainGO.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;

            // Fail-safe underlying BoxCollider so no raycast can ever miss the ground
            var box = terrainGO.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, -0.5f, 0f);
            box.size = new Vector3(750f, 1f, 750f);

            terrainGO.layer = LayerMask.NameToLayer("Default");

            return terrainGO;
        }
    }
}
