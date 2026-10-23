using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.SceneManagement;
using SyntyCombined;
using InsaneGun.Combat;

namespace InsaneGun.Editor
{
    /// <summary>
    /// Dedicated scene builder for the complete Blackwall Cyberspace & Datacenter experience:
    /// - Massive, expanded Datacenter Server Room (22m wide x 9m tall x 90m long, Z = -100 to Z = -10):
    ///   * Multi-tiered towering server arrays (3 tiers high) lining both outer and inner aisles
    ///   * An extruded central server monolith core at Z = -55 radiating crimson red laser light
    ///   * Overhead cable gantries, structural trusses, diagnostic consoles, and ambient cyberpunk lighting
    ///   * Player spawns at Z = -92, looking down the grand server corridor
    /// - AT THE END OF THE SERVER ROOM (Z = -10):
    ///   * ONLY THE DOOR WHICH OPENS (SM_Bld_Door_Double_04), embedded cleanly in the end wall!
    ///   * Automated pneumatic double-sliding blast door (BlackwallGatewayDoor) with indicator light
    /// - IN BLACKWALL CYBERSPACE (Z = -10 to Z = +120):
    ///   * ONLY THE DOOR WHICH OPENS INTO THE BLACKWALL — pure, surreal cyberspace void!
    ///   * An infinite reflective salt-lake mirror water floor with real-time planar reflections
    ///   * At Z = 45: The monumental 200m authentic Blackwall LiDAR barrier towering into cyberspace!
    /// </summary>
    public static class BlackwallCyberspaceSceneBuilder
    {
        public const string SCENE_PATH = "Assets/Scenes/Scene_Blackwall_Cyberspace.unity";

        // Spatial Layout Constants (Expanded mega-datacenter scale)
        public const float ROOM_Z_START     = -100.0f;
        public const float DOOR_Z           =  -10.0f;
        public const float BLACKWALL_Z      =   75.0f;
        public const float PLAYER_SPAWN_Z   =  -92.0f;

        // Asset paths
        private const string BLACKWALL_MESH_PATH     = "Assets/BlackwallMesh/Blackwall_UltraHD_200m_Mesh.asset";
        private const string BLACKWALL_MAT_PATH      = "Assets/SyntyCombined/Materials/M_Blackwall_Authentic_Lidar.mat";
        private const string MIRROR_FLOOR_MAT_PATH   = "Assets/InsaneGun_Sword_Set/Materials/M_Cyberspace_Water_Mirror.mat";
        private const string GHOST_ATTACK_AUDIO_PATH  = "Assets/Audio/Blackwall/cyberpunk-2077-blackwall-ghost-attack.mp3";
        private const string AMBIENT_MUSIC_PATH      = "Assets/Audio/Blackwall/51-beyond-the-blackwall.mp3";
        private const string SPACE_SKYBOX_PATH       = "Assets/Synty/PolygonSciFiWorlds/Materials/Misc/SkyBoxSpace_01.mat";
        private const string PLAYER_PREFAB_PATH      = "Assets/Prefabs/Alita_Sidekick/Alita_03_Final_NanoBerserker.prefab";
        private const string WATER_RIPPLE_PATH       = "Assets/Synty/PolygonParticles/Prefabs/FX_WaterRipple_01.prefab";
        private const string SPARKS_FX_PATH          = "Assets/Synty/PolygonParticles/Prefabs/FX_Sparks_01.prefab";
        private const string ELECTRICITY_FX_PATH     = "Assets/Synty/PolygonParticles/Prefabs/FX_Electricity_01.prefab";

        // Datacenter & Door Assets
        private const string SERVER_RACK_PATH        = "Assets/PolygonCyberCity/Prefabs/Props/SM_Prop_Server_01.prefab";
        private const string CONSOLE_PATH            = "Assets/PolygonCyberCity/Prefabs/Props/SM_Prop_Console_01.prefab";
        private const string DOOR_DOUBLE_04_PATH     = "Assets/Synty/PolygonSciFiWorlds/Prefabs/Buildings/SM_Bld_Door_Double_04.prefab";
        private const string PORTAL_THIN_FX_PATH     = "Assets/Synty/PolygonParticles/Prefabs/FX_Portal_Thin_01.prefab";
        private const string NEON_SIGN_01_PATH       = "Assets/PolygonCyberCity/Prefabs/Props/SM_Prop_NeonSign_01.prefab";
        private const string NEON_SIGN_02_PATH       = "Assets/PolygonCyberCity/Prefabs/Props/SM_Prop_NeonSign_02.prefab";
        private const string HOLO_SIGN_01_PATH       = "Assets/PolygonCyberCity/Prefabs/Props/SM_Prop_Holo_Sign_01.prefab";
        private const string GIRDERS_PATH            = "Assets/Synty/PolygonSciFiWorlds/Prefabs/Buildings/SM_Bld_Bridge_Rail_Pillar_01.prefab";

        [MenuItem("Tools/Cyberpunk Blackwall Arena/Build Dedicated Blackwall Scene", priority = 0)]
        public static void BuildStandaloneBlackwallScene()
        {
            Debug.Log("<color=#FF0055><b>[Blackwall Scene Builder]</b></color> Building expanded Server Room with Door at the end leading to Blackwall...");

            // 1. Create clean scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 2. Load Core Assets
            var blackwallMesh = AssetDatabase.LoadAssetAtPath<Mesh>(BLACKWALL_MESH_PATH);
            var blackwallMat = AssetDatabase.LoadAssetAtPath<Material>(BLACKWALL_MAT_PATH);
            var mirrorFloorMat = AssetDatabase.LoadAssetAtPath<Material>(MIRROR_FLOOR_MAT_PATH);
            var ghostAttackClip = AssetDatabase.LoadAssetAtPath<AudioClip>(GHOST_ATTACK_AUDIO_PATH);
            var ambientMusicClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AMBIENT_MUSIC_PATH);
            var spaceSkybox = AssetDatabase.LoadAssetAtPath<Material>(SPACE_SKYBOX_PATH);
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_PREFAB_PATH);
            var ripplePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WATER_RIPPLE_PATH);
            var sparksPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SPARKS_FX_PATH);
            var electricityPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ELECTRICITY_FX_PATH);
            var heroTrailMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/SyntyCombined/Materials/Mat_HeroTrail_Alpha.mat");

            // 3. Environment Root
            GameObject envRoot = new GameObject("[Blackwall_Cyberspace_Environment]");

            // 4. Cyberspace Atmosphere & Skybox
            if (spaceSkybox != null) RenderSettings.skybox = spaceSkybox;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.008f, 0.008f, 0.015f, 1.0f);
            RenderSettings.fogDensity = 0.012f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.035f, 0.02f, 0.035f, 1.0f);

            // 5. Directional Cyber Fill Light
            GameObject dirLightGO = new GameObject("Directional_Cyberspace_Light");
            dirLightGO.transform.SetParent(envRoot.transform, false);
            dirLightGO.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            var dirLight = dirLightGO.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.color = new Color(0.3f, 0.35f, 0.5f, 1.0f);
            dirLight.intensity = 0.4f;
            dirLight.shadows = LightShadows.Soft;

            // 6. Datacenter Server Room Soundtrack (Plays inside Server Room, muted near Blackwall in Cyberspace)
            AudioSource musicSource = null;
            if (ambientMusicClip != null)
            {
                GameObject musicGO = new GameObject("Server_Room_Music_AudioSource");
                musicGO.transform.SetParent(envRoot.transform, false);
                musicSource = musicGO.AddComponent<AudioSource>();
                musicSource.clip = ambientMusicClip;
                musicSource.loop = true;
                musicSource.volume = 0.50f; // Plays inside Server Room!
                musicSource.spatialBlend = 0.0f;
                musicSource.playOnAwake = true;
            }

            // 7. Planar Mirror Water Floor (covers the Cyberspace expanse beyond the door: Z = -55 to +165)
            GameObject floorGO = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floorGO.name = "Cyberspace_SaltLake_MirrorFloor";
            floorGO.transform.SetParent(envRoot.transform, false);
            floorGO.transform.position = new Vector3(0f, 0.0f, 55.0f);
            floorGO.transform.localScale = new Vector3(25.0f, 1.0f, 22.0f);

            var oldCol = floorGO.GetComponent<Collider>();
            if (oldCol != null) Object.DestroyImmediate(oldCol);
            var floorCol = floorGO.AddComponent<BoxCollider>();
            floorCol.center = new Vector3(0f, -0.6f, 0f);
            floorCol.size = new Vector3(10f, 1.2f, 10f);

            var floorRenderer = floorGO.GetComponent<MeshRenderer>();
            if (floorRenderer != null && mirrorFloorMat != null)
            {
                floorRenderer.sharedMaterial = mirrorFloorMat;
                floorRenderer.shadowCastingMode = ShadowCastingMode.Off;
                floorRenderer.receiveShadows = true;
            }

            var planarMirror = floorGO.AddComponent<CyberspacePlanarMirror>();
            planarMirror.planeHeight = 0.0f;
            planarMirror.clipPlaneOffset = 0.05f;
            planarMirror.floorRenderer = floorRenderer;
            planarMirror.mirrorMaterial = mirrorFloorMat;
            planarMirror.textureResolution = 1024;
            planarMirror.renderScale = 1.0f;
            planarMirror.reflectionLayers = ~0;
            planarMirror.renderInEditor = false;

            // 8. 200m Towering Authentic Blackwall LiDAR Matrix Barrier at Z = BLACKWALL_Z (75.0m)
            GameObject blackwallGO = new GameObject("Blackwall_Canonical_Lidar_Wall");
            blackwallGO.transform.SetParent(envRoot.transform, false);
            blackwallGO.transform.position = new Vector3(0f, 0f, BLACKWALL_Z);
            blackwallGO.transform.rotation = Quaternion.identity;
            blackwallGO.transform.localScale = new Vector3(3.0f, 2.0f, 1.0f);

            var wallFilter = blackwallGO.AddComponent<MeshFilter>();
            if (blackwallMesh != null) wallFilter.sharedMesh = blackwallMesh;

            var wallRenderer = blackwallGO.AddComponent<MeshRenderer>();
            if (blackwallMat != null)
            {
                wallRenderer.sharedMaterial = blackwallMat;
                wallRenderer.shadowCastingMode = ShadowCastingMode.Off;
                wallRenderer.receiveShadows = true;
            }

            var wallCol = blackwallGO.AddComponent<BoxCollider>();
            wallCol.center = new Vector3(0f, 18f, 0.6f);
            wallCol.size = new Vector3(85f, 36f, 1.2f);
            wallCol.isTrigger = false;

            AudioSource wallAudio = null;
            if (ghostAttackClip != null)
            {
                wallAudio = blackwallGO.AddComponent<AudioSource>();
                wallAudio.clip = ghostAttackClip;
                wallAudio.loop = true;
                wallAudio.volume = 0.85f; // Full authentic volume for FFT reactivity
                wallAudio.spatialBlend = 0.55f;
                wallAudio.minDistance = 20.0f;
                wallAudio.maxDistance = 120.0f;
                wallAudio.playOnAwake = true;
            }

            var barrier = blackwallGO.AddComponent<BlackwallRogueAIBarrier>();
            barrier.blackwallSoundLoop = ghostAttackClip;
            barrier.audioReactive = true;
            barrier.enableZDisplacement = true;
            barrier.bassSensitivity = 12.0f;
            barrier.screechSensitivity = 22.0f;
            barrier.masterZScale = 1.8f;
            barrier.bassZScale = 6.0f;
            barrier.voiceZScale = 3.0f;
            barrier.screechZScale = 8.0f;
            barrier.shockDamage = 45.0f;
            barrier.contactCooldown = 0.45f;

            for (int li = -4; li <= 4; li++)
            {
                float xPos = li * 9.5f;
                GameObject beaconGO = new GameObject($"Blackwall_Beacon_{li}");
                beaconGO.transform.SetParent(blackwallGO.transform, false);
                beaconGO.transform.position = new Vector3(xPos, 2.8f, BLACKWALL_Z - 1.2f);

                var bLight = beaconGO.AddComponent<Light>();
                bLight.type = LightType.Point;
                bLight.color = new Color(1.0f, 0.04f, 0.15f, 1.0f);
                bLight.intensity = 6.0f;
                bLight.range = 20.0f;
                bLight.shadows = LightShadows.None;
            }

            BlackwallRogueAIEncounter encounter = null;
            if (sparksPrefab != null)
            {
                GameObject sparkGO = (GameObject)PrefabUtility.InstantiatePrefab(sparksPrefab);
                if (sparkGO != null)
                {
                    sparkGO.name = "RogueAI_Sparks_FX";
                    sparkGO.transform.SetParent(blackwallGO.transform, false);
                    sparkGO.transform.position = new Vector3(0f, 1.2f, BLACKWALL_Z - 1.2f);
                    barrier.rogueAISparksVFX = sparkGO.GetComponent<ParticleSystem>();

                    // Breach Point Point Light
                    var sparkLight = sparkGO.AddComponent<Light>();
                    sparkLight.type = LightType.Point;
                    sparkLight.color = new Color(1.0f, 0.15f, 0.35f, 1.0f);
                    sparkLight.intensity = 4.0f;
                    sparkLight.range = 8.5f;

                    // Rogue AI Encounter Controller ([E] interaction, Nine Inch Nails music switch, Holographic Robot Boss)
                    encounter = sparkGO.AddComponent<BlackwallRogueAIEncounter>();
                    encounter.interactionRadius = 5.5f;
                    encounter.interactKey = KeyCode.E;
                    encounter.sparksFX = barrier.rogueAISparksVFX;
                    encounter.wallAmbientAudioSource = wallAudio;
                    encounter.serverRoomMusicSource = musicSource;
                    encounter.battleMusicClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/Nine Inch Nails - Forked Reality.mp3");
                    encounter.robotEnemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Mission1_Enemy_Ninja.prefab");
                    encounter.enforcerBossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Mission1_Enemy_Enforcer.prefab");
                    encounter.holographicShader = Shader.Find("Cyberpunk/Holographic_RogueAI");
                }
            }

            if (electricityPrefab != null)
            {
                GameObject elecGO = (GameObject)PrefabUtility.InstantiatePrefab(electricityPrefab);
                if (elecGO != null)
                {
                    elecGO.name = "RogueAI_Electricity_FX";
                    elecGO.transform.SetParent(blackwallGO.transform, false);
                    elecGO.transform.position = new Vector3(0f, 3.5f, BLACKWALL_Z - 1.0f);
                    barrier.rogueAILightningVFX = elecGO.GetComponent<ParticleSystem>();

                    if (encounter != null)
                    {
                        encounter.lightningFX = barrier.rogueAILightningVFX;
                    }
                }
            }

            barrier.CollectRenderersAndLights();

            // 8.5 Colossal Female LiDAR Face projected from the Blackwall
            BuildGiantFemaleLidarFace(envRoot);

            // 9. Global Post-Processing Volume
            GameObject volumeGO = new GameObject("AAA_Cyberpunk_PostProcess_Volume");
            volumeGO.transform.SetParent(envRoot.transform, false);
            var volume = volumeGO.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Blackwall_Cyberspace_Profile";

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.85f);
            bloom.intensity.Override(2.2f);
            bloom.scatter.Override(0.70f);
            bloom.tint.Override(new Color(0.95f, 0.95f, 1.0f));

            var colorAdjust = profile.Add<ColorAdjustments>(true);
            colorAdjust.postExposure.Override(0.35f);
            colorAdjust.contrast.Override(30f);
            colorAdjust.saturation.Override(20f);

            var tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.ACES);

            var chromaticAberration = profile.Add<ChromaticAberration>(true);
            chromaticAberration.intensity.Override(0.35f);

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.35f);
            vignette.smoothness.Override(0.45f);

            volume.profile = profile;

            // Properly serialize sub-assets so they are never {fileID: 0}
            AssetDatabase.CreateAsset(profile, "Assets/InsaneGun_Sword_Set/Blackwall_PostProcess_Profile.asset");
            AssetDatabase.AddObjectToAsset(bloom, profile);
            AssetDatabase.AddObjectToAsset(colorAdjust, profile);
            AssetDatabase.AddObjectToAsset(tonemapping, profile);
            AssetDatabase.AddObjectToAsset(chromaticAberration, profile);
            AssetDatabase.AddObjectToAsset(vignette, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            // 10. Instantiate Player Character (Alita Hero) INSIDE the Big Server Room at Z = PLAYER_SPAWN_Z (-92.0f)
            GameObject playerGO = null;
            if (playerPrefab != null)
            {
                playerGO = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                playerGO.name = "Alita_BattleAngel_Hero";
                playerGO.tag = "Player";
                playerGO.transform.position = new Vector3(0f, 0.08f, PLAYER_SPAWN_Z);
                playerGO.transform.rotation = Quaternion.identity; // Facing forward down the aisle toward the door!

                if (ripplePrefab != null)
                {
                    var rippleSpawner = playerGO.GetComponent<FootstepWaterRippleSpawner>();
                    if (rippleSpawner == null) rippleSpawner = playerGO.AddComponent<FootstepWaterRippleSpawner>();
                    rippleSpawner.ripplePrefab = ripplePrefab;
                    rippleSpawner.groundHeight = 0.02f;
                }

                if (heroTrailMat != null)
                {
                    foreach (var tr in playerGO.GetComponentsInChildren<TrailRenderer>(true))
                    {
                        tr.sharedMaterial = heroTrailMat;
                    }
                }

                GameObject fillLightGO = new GameObject("Player_Blackwall_FrontFill");
                fillLightGO.transform.SetParent(playerGO.transform, false);
                fillLightGO.transform.localPosition = new Vector3(0f, 1.4f, -1.0f);
                var fillLight = fillLightGO.AddComponent<Light>();
                fillLight.type = LightType.Point;
                fillLight.color = new Color(0.85f, 0.4f, 0.5f, 1.0f);
                fillLight.intensity = 1.2f;
                fillLight.range = 4.5f;
                fillLight.shadows = LightShadows.None;
            }

            // 11. Main Camera & Smooth Orbit System behind the Player
            GameObject camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            camGO.transform.position = new Vector3(0f, 2.0f, PLAYER_SPAWN_Z - 4.5f);
            camGO.transform.rotation = Quaternion.Euler(8f, 0f, 0f);

            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.cullingMask = ~0;
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1000f;

            var camData = camGO.AddComponent<UniversalAdditionalCameraData>();
            camData.antialiasing = AntialiasingMode.None;
            camData.renderPostProcessing = true;

            camGO.AddComponent<AudioListener>();

            var orbitCam = camGO.AddComponent<SmoothOrbitCamera>();
            if (playerGO != null) orbitCam.target = playerGO.transform;
            orbitCam.targetOffset = new Vector3(0f, 1.45f, 0f);
            orbitCam.cameraOffset = new Vector3(0f, 0.40f, -4.8f);
            orbitCam.tightPositionFollow = true;
            orbitCam.minDistance = 2.2f;
            orbitCam.maxDistance = 8.5f;

            // 12. Build the Expanded Datacenter Server Room with the Door at the end leading to Blackwall!
            BuildExpandedDatacenterRoom(envRoot, musicSource, wallAudio);

            // 13. Safety Void Catch Foundation (beneath the entire world to catch characters if ever displaced)
            GameObject safetyCatchGO = new GameObject("[Safety_Void_Catch_Floor]");
            safetyCatchGO.transform.SetParent(envRoot.transform, false);
            safetyCatchGO.transform.position = new Vector3(0f, -25.0f, 0f);
            var safetyCol = safetyCatchGO.AddComponent<BoxCollider>();
            safetyCol.size = new Vector3(600f, 4.0f, 600f);
            safetyCol.isTrigger = false;

            // 14. Save Scene & Build Settings
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, SCENE_PATH);
            AddSceneToBuildSettings(SCENE_PATH);
            AddSceneToBuildSettings("Assets/Synty/PolygonSciFiWorlds/Scenes/Demo_BlackMarket_01.unity");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"<color=#00FFAA><b>[Blackwall Scene Builder]</b></color> Successfully created and saved scene at <b>{SCENE_PATH}</b>!");
        }

        /// <summary>
        /// Builds the expanded Datacenter Server Room (22m wide x 9m high x 90m long)
        /// with multi-tier server banks, pulsing red laser core, and ONLY the door at the end leading to Blackwall.
        /// </summary>
        private static void BuildExpandedDatacenterRoom(GameObject envRoot, AudioSource musicSource, AudioSource wallAudio)
        {
            GameObject roomRoot = new GameObject("[Datacenter_Server_Room]");
            roomRoot.transform.SetParent(envRoot.transform, false);

            var serverPrefab     = AssetDatabase.LoadAssetAtPath<GameObject>(SERVER_RACK_PATH);
            var consolePrefab    = AssetDatabase.LoadAssetAtPath<GameObject>(CONSOLE_PATH);
            var door04Prefab     = AssetDatabase.LoadAssetAtPath<GameObject>(DOOR_DOUBLE_04_PATH);
            var portalThinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PORTAL_THIN_FX_PATH);
            var neon1Prefab      = AssetDatabase.LoadAssetAtPath<GameObject>(NEON_SIGN_01_PATH);
            var neon2Prefab      = AssetDatabase.LoadAssetAtPath<GameObject>(NEON_SIGN_02_PATH);
            var holoSignPrefab   = AssetDatabase.LoadAssetAtPath<GameObject>(HOLO_SIGN_01_PATH);
            var girdersPrefab    = AssetDatabase.LoadAssetAtPath<GameObject>(GIRDERS_PATH);

            // Materials
            var wallMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/SyntyCombined/Materials/M_Datacenter_Room_Wall.mat");
            if (wallMat == null)
            {
                wallMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                wallMat.name = "M_Datacenter_Room_Wall";
                wallMat.SetColor("_BaseColor", new Color(0.045f, 0.045f, 0.055f, 1f));
                wallMat.SetFloat("_Metallic", 0.85f);
                wallMat.SetFloat("_Smoothness", 0.45f);
                AssetDatabase.CreateAsset(wallMat, "Assets/SyntyCombined/Materials/M_Datacenter_Room_Wall.mat");
            }

            var roomFloorMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/SyntyCombined/Materials/M_Datacenter_Room_Floor.mat");
            if (roomFloorMat == null)
            {
                roomFloorMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                roomFloorMat.name = "M_Datacenter_Room_Floor";
                roomFloorMat.SetColor("_BaseColor", new Color(0.06f, 0.065f, 0.075f, 1f));
                roomFloorMat.SetFloat("_Metallic", 0.75f);
                roomFloorMat.SetFloat("_Smoothness", 0.55f);
                AssetDatabase.CreateAsset(roomFloorMat, "Assets/SyntyCombined/Materials/M_Datacenter_Room_Floor.mat");
            }

            // Expanded Room Dimensions
            float roomWidth = 22.0f; // from X = -11.0 to +11.0
            float roomHalfWidth = roomWidth * 0.5f;
            float roomHeight = 9.0f;
            float zStart = ROOM_Z_START; // -100.0f
            float zEnd = DOOR_Z;         // -10.0f
            float roomLength = zEnd - zStart; // 90.0m
            float zCenter = (zStart + zEnd) * 0.5f; // -55.0m

            // ── 1. Server Room Interior Root (Dynamic visibility: Hidden from Cyberspace, fully enclosed inside room) ──
            GameObject interiorRoot = new GameObject("Server_Room_Interior");
            interiorRoot.transform.SetParent(roomRoot.transform, false);

            // Floor (Solid 1.2m thick foundation with top surface flush at Y = 0.0f)
            GameObject roomFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roomFloor.name = "Datacenter_Floor";
            roomFloor.transform.SetParent(interiorRoot.transform, false);
            roomFloor.transform.position = new Vector3(0f, -0.6f, zCenter);
            roomFloor.transform.localScale = new Vector3(roomWidth, 1.2f, roomLength);
            roomFloor.GetComponent<MeshRenderer>().sharedMaterial = roomFloorMat;

            // Back Wall (enclosing rear behind spawn at Z = -100.0)
            GameObject bWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bWall.name = "Datacenter_BackWall";
            bWall.transform.SetParent(interiorRoot.transform, false);
            bWall.transform.position = new Vector3(0f, roomHeight * 0.5f, zStart);
            bWall.transform.localScale = new Vector3(roomWidth + 0.5f, roomHeight, 0.5f);
            bWall.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

            // Left Side Wall (X = -11.0m)
            GameObject lWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lWall.name = "Datacenter_LeftWall";
            lWall.transform.SetParent(interiorRoot.transform, false);
            lWall.transform.position = new Vector3(-roomHalfWidth - 0.25f, roomHeight * 0.5f, zCenter);
            lWall.transform.localScale = new Vector3(0.5f, roomHeight, roomLength);
            lWall.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

            // Right Side Wall (X = +11.0m)
            GameObject rWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rWall.name = "Datacenter_RightWall";
            rWall.transform.SetParent(interiorRoot.transform, false);
            rWall.transform.position = new Vector3(roomHalfWidth + 0.25f, roomHeight * 0.5f, zCenter);
            rWall.transform.localScale = new Vector3(0.5f, roomHeight, roomLength);
            rWall.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

            // Enclosed Ceiling / Roof (Y = 9.0m)
            GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceiling.name = "Datacenter_Roof_Ceiling";
            ceiling.transform.SetParent(interiorRoot.transform, false);
            ceiling.transform.position = new Vector3(0f, roomHeight + 0.25f, zCenter);
            ceiling.transform.localScale = new Vector3(roomWidth + 1.0f, 0.5f, roomLength);
            ceiling.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

            // Front Wall Enclosing the Doorway (inside Server Room at Z = zEnd - 0.25f)
            // Left of Door (X = -11.0m to -2.04m, width 8.96m, center X = -6.52m)
            GameObject fWallL = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fWallL.name = "Datacenter_FrontWall_Left";
            fWallL.transform.SetParent(interiorRoot.transform, false);
            fWallL.transform.position = new Vector3(-6.52f, roomHeight * 0.5f, zEnd - 0.25f);
            fWallL.transform.localScale = new Vector3(8.96f, roomHeight, 0.5f);
            fWallL.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

            // Right of Door (X = +2.04m to +11.0m, width 8.96m, center X = +6.52m)
            GameObject fWallR = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fWallR.name = "Datacenter_FrontWall_Right";
            fWallR.transform.SetParent(interiorRoot.transform, false);
            fWallR.transform.position = new Vector3(6.52f, roomHeight * 0.5f, zEnd - 0.25f);
            fWallR.transform.localScale = new Vector3(8.96f, roomHeight, 0.5f);
            fWallR.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

            // Lintel above Door (X = 0, width 4.08m, Y = 3.65m to 9.0m, height 5.35m, center Y = 6.325m)
            GameObject fWallLintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fWallLintel.name = "Datacenter_FrontWall_Lintel";
            fWallLintel.transform.SetParent(interiorRoot.transform, false);
            fWallLintel.transform.position = new Vector3(0f, 6.325f, zEnd - 0.25f);
            fWallLintel.transform.localScale = new Vector3(4.08f, 5.35f, 0.5f);
            fWallLintel.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

            // ── 2. THE DOOR AT THE END OF THE SERVER ROOM (Z = -10.0f) ───────────
            // In Blackwall Cyberspace, ONLY this door is visible standing freely in cyberspace!
            // No exterior building, no outer walls, no server racks in cyberspace!
            if (door04Prefab != null)
            {
                GameObject doorGO = (GameObject)PrefabUtility.InstantiatePrefab(door04Prefab);
                doorGO.name = "ServerRoom_Exit_Door_To_Blackwall";
                doorGO.transform.SetParent(roomRoot.transform, false);
                doorGO.transform.position = new Vector3(0f, 0f, zEnd);
                doorGO.transform.rotation = Quaternion.identity;
                doorGO.transform.localScale = new Vector3(1.3f, 1.3f, 1.3f);

                // Add Kinematic Rigidbodies to sliding panels so PhysX handles moving colliders cleanly
                var p1 = doorGO.transform.Find("SM_Bld_Door_Double_04_Door_01");
                var p2 = doorGO.transform.Find("SM_Bld_Door_Double_04_Door_02");
                if (p1 != null) { var rb = p1.gameObject.AddComponent<Rigidbody>(); rb.isKinematic = true; }
                if (p2 != null) { var rb = p2.gameObject.AddComponent<Rigidbody>(); rb.isKinematic = true; }

                // High-tech biometric / status indicator light on door frame
                GameObject dLightGO = new GameObject("Door_Security_IndicatorLight");
                dLightGO.transform.SetParent(doorGO.transform, false);
                dLightGO.transform.localPosition = new Vector3(0f, 3.2f, -0.2f);
                var dLight = dLightGO.AddComponent<Light>();
                dLight.type = LightType.Point;
                dLight.color = new Color(1.0f, 0.05f, 0.12f, 1.0f);
                dLight.intensity = 3.5f;
                dLight.range = 6.0f;

                // Threshold Particle FX inside the door frame (Cyberspace reality distortion)
                ParticleSystem portalPS = null;
                if (portalThinPrefab != null)
                {
                    GameObject pfx = (GameObject)PrefabUtility.InstantiatePrefab(portalThinPrefab);
                    pfx.name = "Doorway_Blackwall_Threshold_FX";
                    pfx.transform.SetParent(doorGO.transform, false);
                    pfx.transform.localPosition = new Vector3(0f, 1.6f, 0.05f);
                    pfx.transform.localScale = new Vector3(0.65f, 0.65f, 0.65f);
                    portalPS = pfx.GetComponent<ParticleSystem>();
                }

                // Attach BlackwallGatewayDoor controller
                var gatewayDoor = doorGO.AddComponent<BlackwallGatewayDoor>();
                gatewayDoor.leftDoorPanel = p1;
                gatewayDoor.rightDoorPanel = p2;
                gatewayDoor.indicatorLight = dLight;
                gatewayDoor.thresholdFX = portalPS;
                gatewayDoor.triggerDistance = 7.0f;
                gatewayDoor.openSlideDistance = 1.85f;
                gatewayDoor.openSpeed = 3.8f;
                gatewayDoor.lockedColor = new Color(1.0f, 0.05f, 0.12f, 1f);
                gatewayDoor.openColor = new Color(0.1f, 0.95f, 1.0f, 1f);

                // Attach BlackwallCyberspaceGatewayPortal for dynamic door-only cyberspace visibility and audio muting
                var portalVis = doorGO.AddComponent<BlackwallCyberspaceGatewayPortal>();
                portalVis.serverRoomInterior = interiorRoot;
                portalVis.thresholdZ = -9.8f;
                portalVis.serverRoomMusicAudio = musicSource;
                portalVis.blackwallWallAudio = wallAudio;

                // Add trigger volume to the doorway
                var doorCol = doorGO.AddComponent<BoxCollider>();
                doorCol.isTrigger = true;
                doorCol.center = new Vector3(0f, 1.8f, 0f);
                doorCol.size = new Vector3(4.0f, 3.6f, 3.0f);
            }

            // Warning Hologram directly over the Door leading to Blackwall (inside server room)
            if (holoSignPrefab != null)
            {
                GameObject hs = (GameObject)PrefabUtility.InstantiatePrefab(holoSignPrefab);
                hs.name = "Holo_Sign_Blackwall_Terminal";
                hs.transform.SetParent(interiorRoot.transform, false);
                hs.transform.position = new Vector3(0f, 4.5f, zEnd - 0.35f);
                hs.transform.localScale = new Vector3(1.25f, 1.25f, 1.25f);
            }

            // ── 3. Structural Ceiling Girders & Overhead Trusses with Downlights ───
            if (girdersPrefab != null)
            {
                float[] zGirders = new float[] { -90f, -75f, -60f, -45f, -30f, -18f };
                foreach (float zg in zGirders)
                {
                    GameObject gantry = (GameObject)PrefabUtility.InstantiatePrefab(girdersPrefab);
                    gantry.name = $"Overhead_Truss_Z{zg:F0}";
                    gantry.transform.SetParent(interiorRoot.transform, false);
                    gantry.transform.position = new Vector3(0f, roomHeight - 1.2f, zg);
                    gantry.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                    gantry.transform.localScale = new Vector3(2.3f, 1.2f, 1.4f);

                    // Downlight under truss
                    GameObject tLightGO = new GameObject($"Truss_Downlight_Z{zg:F0}");
                    tLightGO.transform.SetParent(gantry.transform, false);
                    tLightGO.transform.localPosition = new Vector3(0f, -0.4f, 0f);
                    var tLight = tLightGO.AddComponent<Light>();
                    tLight.type = LightType.Point;
                    tLight.color = new Color(0.2f, 0.5f, 0.8f, 1.0f);
                    tLight.intensity = 2.5f;
                    tLight.range = 8.0f;
                    tLight.shadows = LightShadows.None;
                }
            }

            // ── 4. Towering Multi-Tier Server Banks (3 Tiers High!) ───────────────
            // Outer Wall Server Banks: Stacked 3 tiers high (Y = 0, Y = 2.28, Y = 4.56)
            // Inner Aisle Server Banks: Stacked 2 tiers high (Y = 0, Y = 2.28)
            // Central corridor is 7m wide and 100% unblocked all the way to the door!
            if (serverPrefab != null)
            {
                for (float z = zStart + 4f; z <= zEnd - 3.5f; z += 1.25f)
                {
                    bool isCoreZone = (z >= -58.0f && z <= -52.0f);

                    // Outer Left Wall Racks (X = -9.8m, 3 tiers high)
                    for (int tier = 0; tier < 3; tier++)
                    {
                        float yPos = tier * 2.28f;
                        GameObject lOuter = (GameObject)PrefabUtility.InstantiatePrefab(serverPrefab);
                        lOuter.name = $"Server_L_Outer_Z{z:F0}_T{tier}";
                        lOuter.transform.SetParent(interiorRoot.transform, false);
                        lOuter.transform.position = new Vector3(-roomHalfWidth + 1.2f, yPos, z);
                        lOuter.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

                        GameObject rOuter = (GameObject)PrefabUtility.InstantiatePrefab(serverPrefab);
                        rOuter.name = $"Server_R_Outer_Z{z:F0}_T{tier}";
                        rOuter.transform.SetParent(interiorRoot.transform, false);
                        rOuter.transform.position = new Vector3(roomHalfWidth - 1.2f, yPos, z);
                        rOuter.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
                    }

                    // Inner Aisle Racks (X = -4.8m and +4.8m, 2 tiers high)
                    if (!isCoreZone)
                    {
                        for (int tier = 0; tier < 2; tier++)
                        {
                            float yPos = tier * 2.28f;
                            GameObject lInner = (GameObject)PrefabUtility.InstantiatePrefab(serverPrefab);
                            lInner.name = $"Server_L_Inner_Z{z:F0}_T{tier}";
                            lInner.transform.SetParent(interiorRoot.transform, false);
                            lInner.transform.position = new Vector3(-4.8f, yPos, z);
                            lInner.transform.rotation = Quaternion.Euler(0f, -90f, 0f);

                            GameObject rInner = (GameObject)PrefabUtility.InstantiatePrefab(serverPrefab);
                            rInner.name = $"Server_R_Inner_Z{z:F0}_T{tier}";
                            rInner.transform.SetParent(interiorRoot.transform, false);
                            rInner.transform.position = new Vector3(4.8f, yPos, z);
                            rInner.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                        }
                    }
                }
            }

            // Consoles placed along the server aisles
            if (consolePrefab != null)
            {
                float[] conZ = new float[] { -85f, -70f, -38f, -22f };
                foreach (float cz in conZ)
                {
                    GameObject conL = (GameObject)PrefabUtility.InstantiatePrefab(consolePrefab);
                    conL.name = $"Console_L_Z{cz:F0}";
                    conL.transform.SetParent(interiorRoot.transform, false);
                    conL.transform.position = new Vector3(-roomHalfWidth + 1.6f, 0f, cz);
                    conL.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

                    GameObject conR = (GameObject)PrefabUtility.InstantiatePrefab(consolePrefab);
                    conR.name = $"Console_R_Z{cz:F0}";
                    conR.transform.SetParent(interiorRoot.transform, false);
                    conR.transform.position = new Vector3(roomHalfWidth - 1.6f, 0f, cz);
                    conR.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
                }
            }

            // Red & Cyan Ambient Aisle Lights down the Grand Runway
            float[] zAisleLights = new float[] { -92f, -80f, -68f, -56f, -44f, -32f, -20f };
            foreach (float zl in zAisleLights)
            {
                GameObject lLightGO = new GameObject($"Aisle_Light_L_{zl:F0}");
                lLightGO.transform.SetParent(interiorRoot.transform, false);
                lLightGO.transform.position = new Vector3(-3.2f, 3.2f, zl);
                var lLight = lLightGO.AddComponent<Light>();
                lLight.type = LightType.Point;
                lLight.color = new Color(1.0f, 0.05f, 0.12f, 1f);
                lLight.intensity = 4.5f;
                lLight.range = 10.0f;
                lLight.shadows = LightShadows.None;

                GameObject rLightGO = new GameObject($"Aisle_Light_R_{zl:F0}");
                rLightGO.transform.SetParent(interiorRoot.transform, false);
                rLightGO.transform.position = new Vector3(3.2f, 3.2f, zl);
                var rLight = rLightGO.AddComponent<Light>();
                rLight.type = LightType.Point;
                rLight.color = new Color(0.1f, 0.85f, 1.0f, 1f);
                rLight.intensity = 4.0f;
                rLight.range = 10.0f;
                rLight.shadows = LightShadows.None;
            }

            // ── 5. Flanking Power Core Towers at Z = -55 (Leaving Central Runway Completely Clear) ──
            float coreZ = -55.0f;
            var beamMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/SyntyCombined/Materials/M_Datacenter_CentralCore_Beam.mat");
            if (beamMat == null)
            {
                beamMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                beamMat.name = "M_Datacenter_CentralCore_Beam";
                beamMat.SetColor("_BaseColor", new Color(20.0f, 0.15f, 0.28f, 1.0f));
                AssetDatabase.CreateAsset(beamMat, "Assets/SyntyCombined/Materials/M_Datacenter_CentralCore_Beam.mat");
            }

            // Left Flank Reactor Core Tower (X = -4.8m)
            GameObject coreRootL = new GameObject("Reactor_Core_Tower_Left");
            coreRootL.transform.SetParent(interiorRoot.transform, false);
            coreRootL.transform.position = new Vector3(-4.8f, 0f, coreZ);

            // Right Flank Reactor Core Tower (X = +4.8m)
            GameObject coreRootR = new GameObject("Reactor_Core_Tower_Right");
            coreRootR.transform.SetParent(interiorRoot.transform, false);
            coreRootR.transform.position = new Vector3(4.8f, 0f, coreZ);

            if (serverPrefab != null)
            {
                for (int ty = 0; ty < 3; ty++)
                {
                    float yOffset = ty * 2.28f;
                    GameObject sL = (GameObject)PrefabUtility.InstantiatePrefab(serverPrefab);
                    sL.transform.SetParent(coreRootL.transform, false);
                    sL.transform.position = new Vector3(-4.8f, yOffset, coreZ);
                    sL.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

                    GameObject sR = (GameObject)PrefabUtility.InstantiatePrefab(serverPrefab);
                    sR.transform.SetParent(coreRootR.transform, false);
                    sR.transform.position = new Vector3(4.8f, yOffset, coreZ);
                    sR.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
                }
            }

            // Left Crimson Energy Pylon
            GameObject beamL = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beamL.name = "Reactor_Beam_Left";
            beamL.transform.SetParent(coreRootL.transform, false);
            beamL.transform.position = new Vector3(-4.8f, 4.2f, coreZ);
            beamL.transform.localScale = new Vector3(0.35f, 8.2f, 0.35f);
            var colL = beamL.GetComponent<Collider>();
            if (colL != null) Object.DestroyImmediate(colL);
            var rendL = beamL.GetComponent<MeshRenderer>();
            rendL.sharedMaterial = beamMat;
            rendL.shadowCastingMode = ShadowCastingMode.Off;

            // Right Crimson Energy Pylon
            GameObject beamR = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beamR.name = "Reactor_Beam_Right";
            beamR.transform.SetParent(coreRootR.transform, false);
            beamR.transform.position = new Vector3(4.8f, 4.2f, coreZ);
            beamR.transform.localScale = new Vector3(0.35f, 8.2f, 0.35f);
            var colR = beamR.GetComponent<Collider>();
            if (colR != null) Object.DestroyImmediate(colR);
            var rendR = beamR.GetComponent<MeshRenderer>();
            rendR.sharedMaterial = beamMat;
            rendR.shadowCastingMode = ShadowCastingMode.Off;

            // Flanking Reactor Lights
            List<Light> coreLights = new List<Light>();
            float[] lightHeights = new float[] { 1.8f, 5.0f };
            foreach (float lh in lightHeights)
            {
                GameObject clL = new GameObject($"Core_Light_L_Y{lh}");
                clL.transform.SetParent(coreRootL.transform, false);
                clL.transform.position = new Vector3(-4.8f, lh, coreZ);
                var lComp = clL.AddComponent<Light>();
                lComp.type = LightType.Point;
                lComp.color = new Color(1.0f, 0.04f, 0.12f, 1.0f);
                lComp.intensity = 8.0f;
                lComp.range = 14.0f;
                lComp.shadows = LightShadows.None;
                coreLights.Add(lComp);

                GameObject clR = new GameObject($"Core_Light_R_Y{lh}");
                clR.transform.SetParent(coreRootR.transform, false);
                clR.transform.position = new Vector3(4.8f, lh, coreZ);
                var rComp = clR.AddComponent<Light>();
                rComp.type = LightType.Point;
                rComp.color = new Color(0.1f, 0.85f, 1.0f, 1.0f);
                rComp.intensity = 8.0f;
                rComp.range = 14.0f;
                rComp.shadows = LightShadows.None;
                coreLights.Add(rComp);
            }

            if (neon2Prefab != null)
            {
                GameObject s2 = (GameObject)PrefabUtility.InstantiatePrefab(neon2Prefab);
                s2.name = "Neon_Alert_Overhead";
                s2.transform.SetParent(interiorRoot.transform, false);
                s2.transform.position = new Vector3(0f, 7.2f, coreZ);
                s2.transform.localScale = new Vector3(1.3f, 1.3f, 1.3f);
            }

            // Set interior inactive by default so opening the scene from Cyberspace only shows the standalone Door!
            // BlackwallCyberspaceGatewayPortal dynamically activates interior when camera/player is inside server room (Z < -9.8m).
            interiorRoot.SetActive(false);
            roomRoot.SetActive(true);
        }

        /// <summary>
        /// Instantiates the 16m-tall Colossal Female LiDAR Face projected from the Blackwall barrier.
        /// Generated easily from Synty Sidekick (Starter_02.prefab) and styled with the Cyberpunk Blackwall LiDAR shader.
        /// </summary>
        public static void BuildGiantFemaleLidarFace(GameObject envRoot)
        {
            const string FACE_MAT_PATH = "Assets/SyntyCombined/Materials/M_Blackwall_GiantLidar_Face.mat";
            const string FEMALE_FACE_MESH_PATH = "Assets/BlackwallMesh/Blackwall_Giant_Female_Face_Mesh.asset";

            // 1. Ensure LiDAR Material
            var faceMat = AssetDatabase.LoadAssetAtPath<Material>(FACE_MAT_PATH);
            if (faceMat == null)
            {
                var shader = Shader.Find("Cyberpunk/Blackwall_Lidar_Face");
                if (shader != null)
                {
                    faceMat = new Material(shader);
                    faceMat.name = "M_Blackwall_GiantLidar_Face";
                    faceMat.SetColor("_CrimsonColor", new Color(6.0f, 0.02f, 0.08f, 1.0f));
                    faceMat.SetColor("_MagentaColor", new Color(8.0f, 0.08f, 5.0f, 1.0f));
                    faceMat.SetColor("_PeakColor", new Color(12.0f, 12.0f, 14.0f, 1.0f));
                    faceMat.SetColor("_CyanColor", new Color(0.05f, 5.0f, 9.5f, 1.0f));
                    faceMat.SetFloat("_LidarDensity", 480.0f);
                    faceMat.SetFloat("_DotRadius", 0.35f);
                    faceMat.SetFloat("_DotSharpness", 50.0f);
                    faceMat.SetFloat("_DotStretch", 1.6f);
                    faceMat.SetFloat("_ScanlineSpeed", 3.5f);
                    faceMat.SetFloat("_GlitchIntensity", 0.18f);
                    faceMat.SetFloat("_EyeGlowIntensity", 3.5f);
                    faceMat.SetFloat("_DissolveAmount", 1.0f);
                    faceMat.SetFloat("_EdgeFadeStart", 0.88f);
                    AssetDatabase.CreateAsset(faceMat, FACE_MAT_PATH);
                }
            }

            // 2. Instantiate Clean Colossal Female Face (NO body, NO arms - pure facial contour)
            var faceMesh = AssetDatabase.LoadAssetAtPath<Mesh>(FEMALE_FACE_MESH_PATH);
            if (faceMesh != null)
            {
                GameObject faceGO = new GameObject("Giant_Female_Blackwall_Face");
                faceGO.transform.SetParent(envRoot.transform, false);
                faceGO.transform.position = new Vector3(0f, 14.5f, BLACKWALL_Z + 0.2f);
                faceGO.transform.rotation = Quaternion.Euler(14.0f, 180.0f, 0f); // Facing Alita, 14 deg downward gaze
                faceGO.transform.localScale = Vector3.zero; // Starts dormant until encounter completion or communion

                // Add SkinnedMeshRenderer for real-time 84-blendshape facial speech & pure LiDAR dot rasterization
                var smr = faceGO.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMesh = faceMesh;
                smr.sharedMaterial = faceMat;
                smr.shadowCastingMode = ShadowCastingMode.Off;
                smr.receiveShadows = false;

                // Attach Controller
                var faceCtrl = faceGO.AddComponent<BlackwallLidarGiantFaceController>();
                faceCtrl.lidarFaceMaterial = faceMat;
                faceCtrl.skinnedHeadRenderer = smr;
                faceCtrl.headRoot = faceGO.transform;
                faceCtrl.dormantLocalPosition = new Vector3(0f, 13.0f, BLACKWALL_Z + 0.2f);
                faceCtrl.emergedLocalPosition = new Vector3(0f, 13.0f, BLACKWALL_Z - 5.5f);
                faceCtrl.emergedScale = new Vector3(35.0f, 35.0f, 35.0f);
                faceCtrl.emergenceDroneClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Blackwall/cyberpunk-2077-blackwall-ghost-attack.mp3");
                faceCtrl.EnsureComponentsAndBlendshapes();

                // Attach Cinematic Dialogue HUD
                GameObject canvasGO = new GameObject("Canvas_BlackwallStoryDialogue");
                canvasGO.transform.SetParent(envRoot.transform, false);
                var hud = canvasGO.AddComponent<BlackwallStoryDialogueHUD>();
                hud.giantFace = faceCtrl;
                hud.starshipPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonSciFiWorlds/Prefabs/Props/Vehicles/SM_Veh_HoverCraft_01.prefab");
                hud.EnsureCanvasAndComponents();
                hud.LoadVoiceClips();

                Debug.Log("<color=#00FFAA><b>[Blackwall Scene Builder]</b></color> Successfully created and attached Pure LiDAR Giant Female Face & Dialogue HUD with Voice Acting!");
            }
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path == scenePath)
                {
                    scenes[i].enabled = true;
                    EditorBuildSettings.scenes = scenes.ToArray();
                    return;
                }
            }

            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
