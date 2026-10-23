using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Cyberpunk.Mars
{
    /// <summary>
    /// AAA-Grade Standalone Cinematic Director: Mars Orbital Fleet Arrival.
    /// Frame-by-frame recreation of Star Wars Fleet Arrival (12s to 25s, DINZHI UE5/Blender animation) adapted for Mars:
    /// - Beat 1 (0:12 - 0:16): Deep space starfield void -> Warp rupture shockwave -> Flagship Super Dreadnought (SM_Ship_Colossal_01, 765m) breaches bow-first with vanguard escorts.
    /// - Beat 2 (0:17 - 0:19): Dorsal spine tracking shot skimming past communications spires -> Starboard & port warp detonations -> Fleet Battlecarrier (SM_Ship_Galactic_Carrier_01, 540m) & escort cruisers drop in.
    /// - Beat 3 (0:20 - 0:25): Grand crane pullback reveal -> Colossal Red Planet Mars globe fills the horizon -> Sublight ion engines ignite across the entire armada -> High-orbital blockade established!
    /// Fully isolated from the Mars ground mission for independent playback, testing, and cinematic quality.
    /// </summary>
    [ExecuteAlways]
    public class MarsOrbitalFleetArrivalDirector : MonoBehaviour
    {
        public static MarsOrbitalFleetArrivalDirector Instance { get; private set; }

        [System.Serializable]
        public class EscortShipEntry
        {
            public string name;
            public GameObject shipObject;
            public Vector3 formationOffset;
            public Quaternion formationRotation;
            public float scale = 1.0f;
            public Color thrusterColor = new Color(0f, 0.85f, 1f);
            public Vector3 thrusterLocalOffset = new Vector3(0, 0, -20f);
            public Vector3 thrusterScale = new Vector3(6f, 10f, 6f);
            public GameObject thrusterGlow;
            public Light thrusterLight;
        }

        [Header("Cinematic State")]
        public bool autoPlayOnStart = true;
        public bool isPlaying = false;
        public float playbackTime = 0f;
        public int currentBeat = 1;

        [Header("Scene References")]
        public Camera cinematicCamera;
        public GameObject marsGlobe;
        public GameObject flagshipColossal;
        public GameObject carrierGalactic;
        public GameObject armadaRoot;

        [Header("Primary Capital Prefabs (PolygonSciFiSpace)")]
        public GameObject colossalPrefab;
        public GameObject carrierPrefab;

        [Header("Escort Flotilla Prefabs")]
        public GameObject cruiser1Prefab;
        public GameObject cruiser2Prefab;
        public GameObject cruiser3Prefab;
        public GameObject stealthPrefab;
        public GameObject fighterPrefab;

        [Header("Environment")]
        public Material spaceSkyboxMaterial;

        [Header("Lighting")]
        public Light keySunLight;
        public Light marsBounceLight;

        [Header("Audio")]
        public AudioClip warpBoomAudio;
        public AudioClip engineRumbleAudio;
        public AudioClip ominousThemeAudio;

        [Header("UI & Next Scene")]
        public string nextSceneName = "Scene_Mars_FinalBattle";
        public bool showHUD = true;

        [Header("Escort Ships Data")]
        public List<EscortShipEntry> escortShips = new List<EscortShipEntry>();

        // Private audio sources
        private AudioSource _musicSource;
        private AudioSource _sfxSource;
        private AudioSource _rumbleSource;

        // FX & Lights
        private List<Light> _flagshipThrusterLights = new List<Light>();
        private List<GameObject> _flagshipThrusterGlows = new List<GameObject>();
        private List<Light> _carrierThrusterLights = new List<Light>();
        private List<GameObject> _carrierThrusterGlows = new List<GameObject>();
        private GameObject _warpShockwave;
        private Light _warpFlashLight;

        // Camera Shake / Trauma
        private float _cameraTrauma = 0f;

        // GUI Styles
        private GUIStyle _titleStyle;
        private GUIStyle _subTitleStyle;
        private GUIStyle _hudTelemetryStyle;
        private GUIStyle _promptStyle;
        private Texture2D _whiteTex;
        private bool _guiInitialized = false;

        private Coroutine _cutsceneRoutine;

        private void Awake()
        {
            Instance = this;
            SetupAudioSources();
            LoadDefaultAssets();
        }

        private void Start()
        {
            if (Application.isPlaying)
            {
                EnsureStageBuilt();
                if (autoPlayOnStart)
                {
                    PlayCutscene();
                }
            }
        }

        public void LoadDefaultAssets()
        {
#if UNITY_EDITOR
            if (colossalPrefab == null)
                colossalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonSciFiSpace/Prefabs/Vehicles/SM_Ship_Colossal_01.prefab");
            if (carrierPrefab == null)
                carrierPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonSciFiSpace/Prefabs/Vehicles/SM_Ship_Galactic_Carrier_01.prefab");

            if (cruiser1Prefab == null)
                cruiser1Prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonSciFiSpace/Prefabs/Vehicles/SM_Ship_Cruiser_01.prefab");
            if (cruiser2Prefab == null)
                cruiser2Prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonSciFiSpace/Prefabs/Vehicles/SM_Ship_Cruiser_02.prefab");
            if (cruiser3Prefab == null)
                cruiser3Prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonSciFiSpace/Prefabs/Vehicles/SM_Ship_Cruiser_03.prefab");
            if (stealthPrefab == null)
                stealthPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonSciFiSpace/Prefabs/Vehicles/SM_Ship_Stealth_01.prefab");
            if (fighterPrefab == null)
                fighterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonSciFiSpace/Prefabs/Vehicles/SM_Ship_Fighter_Heavy_01.prefab");

            if (spaceSkyboxMaterial == null)
                spaceSkyboxMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/PolygonSciFiSpace/Materials/Misc/PolygonScifiSpace_Skybox_01.mat");

            if (warpBoomAudio == null)
                warpBoomAudio = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Weather/dragon-studio-dry-thunder-364468.mp3");
            if (engineRumbleAudio == null)
                engineRumbleAudio = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Mechs/Mech_Chassis_Reactor_Loop.wav");
            if (ominousThemeAudio == null)
                ominousThemeAudio = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/vulcan_battle/echoes_of_lumen-ominous-music-595730.mp3");
#endif
        }

        private void SetupAudioSources()
        {
            if (_musicSource == null)
            {
                _musicSource = gameObject.AddComponent<AudioSource>();
                _musicSource.playOnAwake = false;
                _musicSource.loop = true;
                _musicSource.volume = 0.65f;
            }

            if (_sfxSource == null)
            {
                _sfxSource = gameObject.AddComponent<AudioSource>();
                _sfxSource.playOnAwake = false;
                _sfxSource.loop = false;
                _sfxSource.volume = 0.95f;
            }

            if (_rumbleSource == null)
            {
                _rumbleSource = gameObject.AddComponent<AudioSource>();
                _rumbleSource.playOnAwake = false;
                _rumbleSource.loop = true;
                _rumbleSource.volume = 0.50f;
            }
        }

        [ContextMenu("Rebuild High-Orbit Mars Stage")]
        public void EnsureStageBuilt()
        {
            LoadDefaultAssets();

            // 1. Skybox
            if (spaceSkyboxMaterial != null)
            {
                RenderSettings.skybox = spaceSkyboxMaterial;
            }

            // 2. Camera
            if (cinematicCamera == null)
            {
                var camGo = GameObject.Find("Orbital_Cinematic_Camera");
                if (camGo == null)
                {
                    camGo = new GameObject("Orbital_Cinematic_Camera");
                    camGo.transform.SetParent(transform, false);
                }
                cinematicCamera = camGo.GetComponent<Camera>();
                if (cinematicCamera == null) cinematicCamera = camGo.AddComponent<Camera>();
                cinematicCamera.clearFlags = spaceSkyboxMaterial != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
                cinematicCamera.backgroundColor = new Color(0.002f, 0.002f, 0.005f);
                cinematicCamera.nearClipPlane = 0.5f;
                cinematicCamera.farClipPlane = 35000f;
                cinematicCamera.fieldOfView = 56f;
                cinematicCamera.tag = "MainCamera";
            }

            // 3. Directional Sun Key Light (Warm Grazing Sunlight)
            if (keySunLight == null)
            {
                var sunGo = GameObject.Find("Orbital_KeySun");
                if (sunGo == null)
                {
                    sunGo = new GameObject("Orbital_KeySun");
                    sunGo.transform.SetParent(transform, false);
                }
                keySunLight = sunGo.GetComponent<Light>();
                if (keySunLight == null) keySunLight = sunGo.AddComponent<Light>();
                keySunLight.type = LightType.Directional;
            }
            keySunLight.transform.localRotation = Quaternion.Euler(18f, 125f, 0f);
            keySunLight.color = new Color(1.0f, 0.96f, 0.90f);
            keySunLight.intensity = 3.2f;
            keySunLight.shadows = LightShadows.Soft;

            // 4. Mars Atmospheric Orange Rim Fill Light
            if (marsBounceLight == null)
            {
                var bGo = GameObject.Find("Orbital_MarsBounce");
                if (bGo == null)
                {
                    bGo = new GameObject("Orbital_MarsBounce");
                    bGo.transform.SetParent(transform, false);
                }
                marsBounceLight = bGo.GetComponent<Light>();
                if (marsBounceLight == null) marsBounceLight = bGo.AddComponent<Light>();
                marsBounceLight.type = LightType.Directional;
            }
            marsBounceLight.transform.localRotation = Quaternion.Euler(-15f, 320f, 0f);
            marsBounceLight.color = new Color(1.0f, 0.35f, 0.12f);
            marsBounceLight.intensity = 1.2f;

            // 5. Flagship Super Dreadnought (SM_Ship_Colossal_01)
            if (flagshipColossal == null)
            {
                flagshipColossal = GameObject.Find("Dreadnought_Flagship_Colossal");
                if (flagshipColossal == null && colossalPrefab != null)
                {
#if UNITY_EDITOR
                    flagshipColossal = (GameObject)PrefabUtility.InstantiatePrefab(colossalPrefab, transform);
#else
                    flagshipColossal = Instantiate(colossalPrefab, transform);
#endif
                    flagshipColossal.name = "Dreadnought_Flagship_Colossal";
                }
            }

            if (flagshipColossal != null)
            {
                flagshipColossal.transform.localScale = Vector3.one * 1.25f; // ~765m length
                flagshipColossal.transform.localPosition = new Vector3(-120f, 0f, 500f);
                flagshipColossal.transform.localRotation = Quaternion.Euler(0f, 20f, 0f);
                foreach (var c in flagshipColossal.GetComponentsInChildren<Collider>()) DestroyImmediate(c);
                SetupCapitalShipThrusters(flagshipColossal, true);
            }

            // 6. Fleet Battlecarrier (SM_Ship_Galactic_Carrier_01)
            if (carrierGalactic == null)
            {
                carrierGalactic = GameObject.Find("Carrier_Escort_Galactic");
                if (carrierGalactic == null && carrierPrefab != null)
                {
#if UNITY_EDITOR
                    carrierGalactic = (GameObject)PrefabUtility.InstantiatePrefab(carrierPrefab, transform);
#else
                    carrierGalactic = Instantiate(carrierPrefab, transform);
#endif
                    carrierGalactic.name = "Carrier_Escort_Galactic";
                }
            }

            if (carrierGalactic != null)
            {
                carrierGalactic.transform.localScale = Vector3.one * 1.85f; // ~540m length
                carrierGalactic.transform.localPosition = flagshipColossal.transform.position + flagshipColossal.transform.TransformDirection(new Vector3(420f, -40f, 320f));
                carrierGalactic.transform.localRotation = flagshipColossal.transform.rotation * Quaternion.Euler(0f, -6f, 0f);
                foreach (var c in carrierGalactic.GetComponentsInChildren<Collider>()) DestroyImmediate(c);
                SetupCapitalShipThrusters(carrierGalactic, false);
            }

            // 7. Mars Colossal Globe (Photorealistic High-Def Smooth Sphere)
            if (marsGlobe == null)
            {
                marsGlobe = GameObject.Find("Mars_Colossal_Globe");
                if (marsGlobe == null)
                {
                    marsGlobe = new GameObject("Mars_Colossal_Globe");
                    marsGlobe.transform.SetParent(transform, false);
                }
            }

            var sphereMesh = AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>("Assets/Models/Environments/Mesh_HighDef_Planet_Sphere.asset");
            var mf = marsGlobe.GetComponent<MeshFilter>() ?? marsGlobe.AddComponent<MeshFilter>();
            if (sphereMesh != null) mf.sharedMesh = sphereMesh;

            // Position Mars to fill 80% of upper & right background curvature
            marsGlobe.transform.position = flagshipColossal.transform.position + flagshipColossal.transform.TransformDirection(new Vector3(1500f, 150f, 4800f));
            marsGlobe.transform.localScale = Vector3.one * 6400f;
            marsGlobe.transform.rotation = Quaternion.Euler(22f, 115f, 0f);

            var marsRen = marsGlobe.GetComponent<MeshRenderer>() ?? marsGlobe.AddComponent<MeshRenderer>();
            if (marsRen != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                var mat = new Material(shader) { name = "Mat_Mars_Realistic_Surface_2K" };
#if UNITY_EDITOR
                var albedoTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Planets/Mars_Realistic_Albedo_2K.jpg");
                var normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Planets/Mars_Realistic_Normal_2K.png");
                if (albedoTex != null) mat.SetTexture("_BaseMap", albedoTex);
                if (normalTex != null)
                {
                    mat.EnableKeyword("_NORMALMAP");
                    mat.SetTexture("_BumpMap", normalTex);
                    mat.SetFloat("_BumpScale", 3.0f);
                }
#endif
                mat.SetColor("_BaseColor", new Color(0.92f, 0.88f, 0.85f));
                mat.SetFloat("_Smoothness", 0.18f);
                mat.SetFloat("_Metallic", 0.0f);
                marsRen.sharedMaterial = mat;
            }

            // 8. Escort Flotilla (Cruisers, Corvettes, Fighters)
            BuildEscortFlotilla();

            // 9. Warp Shockwave & Flash Rig
            if (_warpShockwave == null)
            {
                _warpShockwave = GameObject.Find("Warp_Shockwave_FX");
                if (_warpShockwave == null)
                {
                    _warpShockwave = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    _warpShockwave.name = "Warp_Shockwave_FX";
                    _warpShockwave.transform.SetParent(transform, false);
                    var c = _warpShockwave.GetComponent<Collider>();
                    if (c != null) DestroyImmediate(c);

                    var ren = _warpShockwave.GetComponent<MeshRenderer>();
                    var s = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                    var m = new Material(s);
                    m.color = new Color(0.4f, 0.85f, 1f, 0.85f);
                    ren.sharedMaterial = m;
                }

                _warpFlashLight = _warpShockwave.GetComponent<Light>();
                if (_warpFlashLight == null) _warpFlashLight = _warpShockwave.AddComponent<Light>();
                _warpFlashLight.type = LightType.Point;
                _warpFlashLight.color = new Color(0.35f, 0.85f, 1.0f);
                _warpFlashLight.range = 600f;
                _warpFlashLight.intensity = 0f;

                _warpShockwave.SetActive(false);
            }

            // Place camera in default Beat 3 pose for scene view
            if (cinematicCamera != null && flagshipColossal != null)
            {
                Vector3 camLocalToFlagship = new Vector3(-35f, 150f, -220f);
                cinematicCamera.transform.position = flagshipColossal.transform.TransformPoint(camLocalToFlagship);
                cinematicCamera.transform.rotation = flagshipColossal.transform.rotation * Quaternion.Euler(11f, 14f, 0f);
                cinematicCamera.fieldOfView = 56f;
            }

            Debug.Log("<color=#00FFCC><b>[Mars Orbital Fleet]</b></color> High-orbit stage with full escort armada successfully built!");
        }

        private void BuildEscortFlotilla()
        {
            if (armadaRoot == null)
            {
                armadaRoot = GameObject.Find("Escort_Armada_Flotilla");
                if (armadaRoot == null)
                {
                    armadaRoot = new GameObject("Escort_Armada_Flotilla");
                    armadaRoot.transform.SetParent(transform, false);
                }
            }

            // Clear previous entries
            escortShips.Clear();

            var unlitShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var amberMat = new Material(unlitShader) { color = new Color(1.0f, 0.65f, 0.15f) };
            var cyanMat = new Material(unlitShader) { color = new Color(0.1f, 0.95f, 1.0f) };

            // Escort Ship Specifications matching Star Wars reference frames 21-25
            var specs = new[] {
                // Port Upper Cruiser (Ref: Upper-Left ship with twin yellow thrusters)
                new { Name = "Escort_Cruiser_Port_Upper", Prefab = cruiser1Prefab, Offset = new Vector3(-340f, 180f, 480f), Rot = Quaternion.Euler(0f, 22f, 0f), Scale = 2.0f, Mat = amberMat, Color = new Color(1.0f, 0.65f, 0.15f), GlowOffset = new Vector3(0, 0, -26f), GlowScale = new Vector3(6f, 10f, 6f) },
                // Port Lower Cruiser (Ref: Lower-Left ship)
                new { Name = "Escort_Cruiser_Port_Lower", Prefab = cruiser2Prefab, Offset = new Vector3(-280f, -80f, 280f), Rot = Quaternion.Euler(0f, 18f, 0f), Scale = 1.8f, Mat = amberMat, Color = new Color(1.0f, 0.65f, 0.15f), GlowOffset = new Vector3(0, 0, -25f), GlowScale = new Vector3(5f, 8f, 5f) },
                // Far Starboard Cruiser (Ref: Far-Right ship)
                new { Name = "Escort_Cruiser_Starboard", Prefab = cruiser3Prefab, Offset = new Vector3(680f, 90f, 620f), Rot = Quaternion.Euler(0f, 16f, 0f), Scale = 1.9f, Mat = cyanMat, Color = new Color(0.15f, 0.9f, 1.0f), GlowOffset = new Vector3(0, 0, -25f), GlowScale = new Vector3(6f, 10f, 6f) },
                // Vanguard Lead Cruiser (Ref: Distant upper-center leading ship)
                new { Name = "Vanguard_Cruiser_Lead", Prefab = cruiser1Prefab, Offset = new Vector3(140f, 240f, 1100f), Rot = Quaternion.Euler(0f, 20f, 0f), Scale = 2.2f, Mat = amberMat, Color = new Color(1.0f, 0.65f, 0.15f), GlowOffset = new Vector3(0, 0, -26f), GlowScale = new Vector3(6f, 10f, 6f) },
                // Vanguard Wing Left (Ref: Upper distant escort)
                new { Name = "Vanguard_Escort_Left", Prefab = stealthPrefab, Offset = new Vector3(480f, 320f, 1350f), Rot = Quaternion.Euler(0f, 20f, 0f), Scale = 2.5f, Mat = cyanMat, Color = new Color(0.15f, 0.9f, 1.0f), GlowOffset = new Vector3(0, 0, -10f), GlowScale = new Vector3(4f, 6f, 4f) },
                // Vanguard Wing Right
                new { Name = "Vanguard_Escort_Right", Prefab = stealthPrefab, Offset = new Vector3(-120f, 290f, 1450f), Rot = Quaternion.Euler(0f, 20f, 0f), Scale = 2.5f, Mat = amberMat, Color = new Color(1.0f, 0.65f, 0.15f), GlowOffset = new Vector3(0, 0, -10f), GlowScale = new Vector3(4f, 6f, 4f) },
                // Starboard Fighter Escort Pair
                new { Name = "Fighter_Escort_1", Prefab = fighterPrefab, Offset = new Vector3(260f, 40f, 260f), Rot = Quaternion.Euler(0f, 20f, 0f), Scale = 2.0f, Mat = cyanMat, Color = new Color(0.15f, 0.9f, 1.0f), GlowOffset = new Vector3(0, 0, -6f), GlowScale = new Vector3(2.5f, 4f, 2.5f) },
                new { Name = "Fighter_Escort_2", Prefab = fighterPrefab, Offset = new Vector3(290f, 65f, 240f), Rot = Quaternion.Euler(0f, 20f, 0f), Scale = 2.0f, Mat = cyanMat, Color = new Color(0.15f, 0.9f, 1.0f), GlowOffset = new Vector3(0, 0, -6f), GlowScale = new Vector3(2.5f, 4f, 2.5f) }
            };

            foreach (var cfg in specs)
            {
                if (cfg.Prefab == null) continue;

                Transform existing = armadaRoot.transform.Find(cfg.Name);
                GameObject sGo;
                if (existing == null)
                {
#if UNITY_EDITOR
                    sGo = (GameObject)PrefabUtility.InstantiatePrefab(cfg.Prefab, armadaRoot.transform);
#else
                    sGo = Instantiate(cfg.Prefab, armadaRoot.transform);
#endif
                    sGo.name = cfg.Name;
                }
                else sGo = existing.gameObject;

                sGo.transform.position = flagshipColossal.transform.position + flagshipColossal.transform.TransformDirection(cfg.Offset);
                sGo.transform.rotation = cfg.Rot;
                sGo.transform.localScale = Vector3.one * cfg.Scale;
                foreach (var col in sGo.GetComponentsInChildren<Collider>()) DestroyImmediate(col);

                // Setup Thruster Glow Cylinder
                string glowName = "Thruster_Glow";
                Transform gTrans = sGo.transform.Find(glowName);
                GameObject glowGo;
                if (gTrans == null)
                {
                    glowGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    glowGo.name = glowName;
                    glowGo.transform.SetParent(sGo.transform, false);
                    var gCol = glowGo.GetComponent<Collider>();
                    if (gCol != null) DestroyImmediate(gCol);
                }
                else glowGo = gTrans.gameObject;

                glowGo.transform.localPosition = cfg.GlowOffset;
                glowGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                glowGo.transform.localScale = cfg.GlowScale;
                glowGo.GetComponent<MeshRenderer>().sharedMaterial = cfg.Mat;

                // Thruster Light
                var tLight = glowGo.GetComponent<Light>();
                if (tLight == null) tLight = glowGo.AddComponent<Light>();
                tLight.type = LightType.Point;
                tLight.color = cfg.Color;
                tLight.range = 80f;
                tLight.intensity = 0f;

                var entry = new EscortShipEntry
                {
                    name = cfg.Name,
                    shipObject = sGo,
                    formationOffset = cfg.Offset,
                    formationRotation = cfg.Rot,
                    scale = cfg.Scale,
                    thrusterColor = cfg.Color,
                    thrusterLocalOffset = cfg.GlowOffset,
                    thrusterScale = cfg.GlowScale,
                    thrusterGlow = glowGo,
                    thrusterLight = tLight
                };
                escortShips.Add(entry);
            }
        }

        private void SetupCapitalShipThrusters(GameObject ship, bool isFlagship)
        {
            var lightList = isFlagship ? _flagshipThrusterLights : _carrierThrusterLights;
            var glowList = isFlagship ? _flagshipThrusterGlows : _carrierThrusterGlows;
            lightList.Clear();
            glowList.Clear();

            Vector3[] localOffsets = isFlagship ?
                new Vector3[] { new Vector3(-25f, 8f, -280f), new Vector3(25f, 8f, -280f), new Vector3(0f, 24f, -280f) } :
                new Vector3[] { new Vector3(-18f, 5f, -145f), new Vector3(18f, 5f, -145f), new Vector3(0f, 0f, -148f) };

            Vector3 glowScale = isFlagship ? new Vector3(14f, 22f, 14f) : new Vector3(12f, 18f, 12f);
            Color thColor = isFlagship ? new Color(1.0f, 0.65f, 0.15f) : new Color(0.1f, 0.95f, 1.0f);

            var unlitShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var glowMat = new Material(unlitShader) { color = thColor };

            for (int i = 0; i < localOffsets.Length; i++)
            {
                string tName = $"Thruster_Light_{i + 1}";
                Transform existing = ship.transform.Find(tName);
                GameObject tGo;
                if (existing == null)
                {
                    tGo = new GameObject(tName);
                    tGo.transform.SetParent(ship.transform, false);
                }
                else tGo = existing.gameObject;

                tGo.transform.localPosition = localOffsets[i];
                var l = tGo.GetComponent<Light>();
                if (l == null) l = tGo.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = thColor;
                l.range = 140f;
                l.intensity = 0f;
                lightList.Add(l);

                // Add physical glow cylinder
                string gName = $"Thruster_Glow_Mesh_{i + 1}";
                Transform gExisting = ship.transform.Find(gName);
                GameObject gGo;
                if (gExisting == null)
                {
                    gGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    gGo.name = gName;
                    gGo.transform.SetParent(ship.transform, false);
                    var col = gGo.GetComponent<Collider>();
                    if (col != null) DestroyImmediate(col);
                }
                else gGo = gExisting.gameObject;

                gGo.transform.localPosition = localOffsets[i];
                gGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                gGo.transform.localScale = glowScale;
                gGo.GetComponent<MeshRenderer>().sharedMaterial = glowMat;
                glowList.Add(gGo);
            }
        }

        private void Update()
        {
            if (isPlaying && Application.isPlaying)
            {
                playbackTime += Time.deltaTime;

                // Handle Skip on Spacebar / Left Click / Escape
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Escape))
                {
                    SkipCutscene();
                }

                // Handle Replay on [R]
                if (Input.GetKeyDown(KeyCode.R))
                {
                    PlayCutscene();
                }

                // Handle Jump to Mars on [Enter]
                if (Input.GetKeyDown(KeyCode.Return))
                {
                    TransitionToMarsSurface();
                }
            }
        }

        public void PlayCutscene()
        {
            EnsureStageBuilt();

            if (_cutsceneRoutine != null) StopCoroutine(_cutsceneRoutine);
            _cutsceneRoutine = StartCoroutine(ExecuteOrbitalFleetArrivalSequence());
        }

        public void SkipCutscene()
        {
            if (_cutsceneRoutine != null) StopCoroutine(_cutsceneRoutine);

            currentBeat = 3;
            playbackTime = 13.0f;
            SetThrustersIntensity(1.0f);

            if (flagshipColossal != null)
            {
                flagshipColossal.SetActive(true);
                flagshipColossal.transform.localPosition = new Vector3(-120f, 0f, 500f);
                flagshipColossal.transform.localRotation = Quaternion.Euler(0f, 20f, 0f);
            }

            if (carrierGalactic != null)
            {
                carrierGalactic.SetActive(true);
                carrierGalactic.transform.position = flagshipColossal.transform.position + flagshipColossal.transform.TransformDirection(new Vector3(420f, -40f, 320f));
                carrierGalactic.transform.localRotation = flagshipColossal.transform.rotation * Quaternion.Euler(0f, -6f, 0f);
            }

            foreach (var escort in escortShips)
            {
                if (escort.shipObject != null)
                {
                    escort.shipObject.SetActive(true);
                    escort.shipObject.transform.position = flagshipColossal.transform.position + flagshipColossal.transform.TransformDirection(escort.formationOffset);
                    escort.shipObject.transform.rotation = escort.formationRotation;
                }
            }

            if (cinematicCamera != null && flagshipColossal != null)
            {
                Vector3 camLocalToFlagship = new Vector3(-35f, 150f, -220f);
                cinematicCamera.transform.position = flagshipColossal.transform.TransformPoint(camLocalToFlagship);
                cinematicCamera.transform.rotation = flagshipColossal.transform.rotation * Quaternion.Euler(11f, 14f, 0f);
                cinematicCamera.fieldOfView = 56f;
            }

            if (_warpShockwave != null) _warpShockwave.SetActive(false);
            _cameraTrauma = 0f;

            Debug.Log("<color=#00FFCC><b>[Mars Orbital Fleet]</b></color> Skipped to High-Orbital Armada Blockade.");
        }

        public void TransitionToMarsSurface()
        {
            if (!string.IsNullOrEmpty(nextSceneName))
            {
                Debug.Log($"<color=#FF5500><b>[Mars Orbital Fleet]</b></color> Transitioning down to {nextSceneName}...");
                SceneManager.LoadScene(nextSceneName);
            }
        }

        private IEnumerator ExecuteOrbitalFleetArrivalSequence()
        {
            isPlaying = true;
            playbackTime = 0f;
            currentBeat = 1;
            SetThrustersIntensity(0f);

            // Start ambient audio
            if (_musicSource != null && ominousThemeAudio != null)
            {
                _musicSource.clip = ominousThemeAudio;
                _musicSource.Play();
            }

            if (_rumbleSource != null && engineRumbleAudio != null)
            {
                _rumbleSource.clip = engineRumbleAudio;
                _rumbleSource.Play();
            }

            // Hide ships initially
            if (flagshipColossal != null) flagshipColossal.SetActive(false);
            if (carrierGalactic != null) carrierGalactic.SetActive(false);
            foreach (var escort in escortShips)
            {
                if (escort.shipObject != null) escort.shipObject.SetActive(false);
            }

            // ════════════════════════════════════════════════════════════════
            // BEAT 1 (0:12 – 0:16): WARP BREACH & PROW EMERGENCE (~3.4s)
            // ════════════════════════════════════════════════════════════════
            currentBeat = 1;

            // Camera looking up into starfield void (Ref: Sec 12-14)
            Vector3 b1CamStart = transform.position + new Vector3(80f, -40f, 650f);
            Vector3 b1CamEnd = transform.position + new Vector3(40f, -20f, 580f);
            Quaternion b1CamRot = Quaternion.Euler(-18f, -22f, 0f);

            if (cinematicCamera != null)
            {
                cinematicCamera.transform.position = b1CamStart;
                cinematicCamera.transform.rotation = b1CamRot;
                cinematicCamera.fieldOfView = 52f;
            }

            yield return new WaitForSeconds(0.4f);

            // Detonate Hyperspace Warp Shockwave at Prow Emergence Point (Ref: Sec 14)
            Vector3 warpPoint1 = transform.position + new Vector3(-80f, 50f, 1100f);
            StartCoroutine(TriggerWarpShockwave(warpPoint1, new Color(0.35f, 0.85f, 1f), 1.8f));
            AddCameraTrauma(0.9f);

            if (_sfxSource != null && warpBoomAudio != null)
            {
                _sfxSource.PlayOneShot(warpBoomAudio, 1.0f);
            }

            yield return new WaitForSeconds(0.2f);

            // Flagship Dreadnought emerges bow-first from warp (Ref: Sec 15-16)
            if (flagshipColossal != null)
            {
                flagshipColossal.SetActive(true);
                flagshipColossal.transform.localRotation = Quaternion.Euler(0f, 20f, 0f);
            }

            // Vanguard ships streak in ahead
            foreach (var escort in escortShips)
            {
                if (escort.name.Contains("Vanguard") && escort.shipObject != null)
                {
                    escort.shipObject.SetActive(true);
                }
            }

            Vector3 ship1Start = new Vector3(-60f, 15f, 1350f);
            Vector3 ship1Mid = new Vector3(-100f, 5f, 650f);

            float b1Timer = 0f;
            float b1Dur = 2.8f;

            while (b1Timer < b1Dur)
            {
                b1Timer += Time.deltaTime;
                float norm = Mathf.Clamp01(b1Timer / b1Dur);
                float smooth = Mathf.SmoothStep(0f, 1f, norm);

                if (flagshipColossal != null)
                {
                    flagshipColossal.transform.localPosition = Vector3.Lerp(ship1Start, ship1Mid, smooth);
                }

                if (cinematicCamera != null)
                {
                    Vector3 p = Vector3.Lerp(b1CamStart, b1CamEnd, smooth);
                    cinematicCamera.transform.position = ApplyCameraShake(p);
                    cinematicCamera.transform.rotation = Quaternion.Euler(-18f + smooth * 6f, -22f + smooth * 4f, 0f);
                }

                yield return null;
            }

            // ════════════════════════════════════════════════════════════════
            // BEAT 2 (0:17 – 0:19): DORSAL SPINE TRACKING & ESCORT WARP-IN (~2.6s)
            // ════════════════════════════════════════════════════════════════
            currentBeat = 2;

            // Camera cuts to low dorsal spine skimming past communication masts (Ref: Sec 17-19)
            Vector3 b2CamStart = flagshipColossal.transform.TransformPoint(new Vector3(-15f, 95f, -80f));
            Vector3 b2CamEnd = flagshipColossal.transform.TransformPoint(new Vector3(-25f, 115f, -140f));
            Quaternion b2CamStartRot = flagshipColossal.transform.rotation * Quaternion.Euler(6f, 18f, 0f);
            Quaternion b2CamEndRot = flagshipColossal.transform.rotation * Quaternion.Euler(8f, 26f, 0f);

            if (cinematicCamera != null)
            {
                cinematicCamera.fieldOfView = 54f;
            }

            // Starboard warp flash for Carrier emergence (Ref: Sec 17)
            Vector3 warpPoint2 = flagshipColossal.transform.position + flagshipColossal.transform.TransformDirection(new Vector3(420f, 60f, 1000f));
            StartCoroutine(TriggerWarpShockwave(warpPoint2, new Color(0.2f, 0.95f, 0.85f), 1.5f));
            AddCameraTrauma(0.7f);

            if (_sfxSource != null && warpBoomAudio != null)
            {
                _sfxSource.PlayOneShot(warpBoomAudio, 0.85f);
            }

            if (carrierGalactic != null)
            {
                carrierGalactic.SetActive(true);
            }

            // Port and flank escort cruisers drop out of warp
            foreach (var escort in escortShips)
            {
                if (escort.shipObject != null)
                {
                    escort.shipObject.SetActive(true);
                }
            }

            Vector3 carrierStart = flagshipColossal.transform.position + flagshipColossal.transform.TransformDirection(new Vector3(420f, -20f, 900f));
            Vector3 carrierMid = flagshipColossal.transform.position + flagshipColossal.transform.TransformDirection(new Vector3(420f, -35f, 450f));

            float b2Timer = 0f;
            float b2Dur = 2.6f;

            while (b2Timer < b2Dur)
            {
                b2Timer += Time.deltaTime;
                float norm = Mathf.Clamp01(b2Timer / b2Dur);
                float smooth = Mathf.SmoothStep(0f, 1f, norm);

                if (flagshipColossal != null)
                {
                    flagshipColossal.transform.localPosition = Vector3.Lerp(ship1Mid, new Vector3(-115f, 2f, 540f), smooth);
                }

                if (carrierGalactic != null)
                {
                    carrierGalactic.transform.position = Vector3.Lerp(carrierStart, carrierMid, smooth);
                }

                if (cinematicCamera != null)
                {
                    Vector3 p = Vector3.Lerp(b2CamStart, b2CamEnd, smooth);
                    cinematicCamera.transform.position = ApplyCameraShake(p);
                    cinematicCamera.transform.rotation = Quaternion.Slerp(b2CamStartRot, b2CamEndRot, smooth);
                }

                yield return null;
            }

            // ════════════════════════════════════════════════════════════════
            // BEAT 3 (0:20 – 0:25): GRAND MARS REVEAL & FULL ARMADA BLOCKADE (~4.6s)
            // ════════════════════════════════════════════════════════════════
            currentBeat = 3;

            // Grand Crane Pullback revealing Mars (Ref: Sec 20-25)
            Vector3 b3CamStart = flagshipColossal.transform.TransformPoint(new Vector3(-25f, 125f, -160f));
            Vector3 b3CamEnd = flagshipColossal.transform.TransformPoint(new Vector3(-35f, 150f, -220f));
            Quaternion b3CamStartRot = flagshipColossal.transform.rotation * Quaternion.Euler(8f, 10f, 0f);
            Quaternion b3CamEndRot = flagshipColossal.transform.rotation * Quaternion.Euler(11f, 14f, 0f);

            if (cinematicCamera != null)
            {
                cinematicCamera.fieldOfView = 56f;
            }

            // Sublight Ion Thrusters ignite across the entire armada! (Ref: Sec 23-24)
            StartCoroutine(IgniteThrustersSmooth(1.2f));

            Vector3 finalShip1Pos = new Vector3(-120f, 0f, 500f);
            Vector3 finalCarrierPos = flagshipColossal.transform.position + flagshipColossal.transform.TransformDirection(new Vector3(420f, -40f, 320f));

            float b3Timer = 0f;
            float b3Dur = 4.6f;

            while (b3Timer < b3Dur)
            {
                b3Timer += Time.deltaTime;
                float norm = Mathf.Clamp01(b3Timer / b3Dur);
                float smooth = Mathf.SmoothStep(0f, 1f, norm);

                if (flagshipColossal != null)
                {
                    flagshipColossal.transform.localPosition = Vector3.Lerp(new Vector3(-115f, 2f, 540f), finalShip1Pos, smooth);
                }

                if (carrierGalactic != null)
                {
                    carrierGalactic.transform.position = Vector3.Lerp(carrierMid, finalCarrierPos, smooth);
                }

                // Escort ships glide smoothly in formation
                foreach (var escort in escortShips)
                {
                    if (escort.shipObject != null)
                    {
                        escort.shipObject.transform.position = flagshipColossal.transform.position + flagshipColossal.transform.TransformDirection(escort.formationOffset);
                    }
                }

                if (cinematicCamera != null)
                {
                    Vector3 p = Vector3.Lerp(b3CamStart, b3CamEnd, smooth);
                    cinematicCamera.transform.position = ApplyCameraShake(p);
                    cinematicCamera.transform.rotation = Quaternion.Slerp(b3CamStartRot, b3CamEndRot, smooth);
                }

                yield return null;
            }

            Debug.Log("<color=#00FFCC><b>[Mars Orbital Fleet]</b></color> High-Orbital Fleet Arrival sequence completed successfully!");
        }

        private IEnumerator TriggerWarpShockwave(Vector3 position, Color flashColor, float duration)
        {
            if (_warpShockwave == null) yield break;

            _warpShockwave.SetActive(true);
            _warpShockwave.transform.position = position;
            _warpShockwave.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            _warpShockwave.transform.localScale = Vector3.one * 10f;

            if (_warpFlashLight != null)
            {
                _warpFlashLight.color = flashColor;
                _warpFlashLight.intensity = 35f;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Expanding ring shockwave
                float scale = Mathf.Lerp(15f, 850f, Mathf.Sqrt(t));
                _warpShockwave.transform.localScale = new Vector3(scale, 1.5f, scale);

                if (_warpFlashLight != null)
                {
                    _warpFlashLight.intensity = Mathf.Lerp(35f, 0f, t * t);
                }

                yield return null;
            }

            _warpShockwave.SetActive(false);
        }

        private IEnumerator IgniteThrustersSmooth(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                SetThrustersIntensity(Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            SetThrustersIntensity(1.0f);
        }

        private void SetThrustersIntensity(float factor)
        {
            foreach (var l in _flagshipThrusterLights)
            {
                if (l != null) l.intensity = 18f * factor;
            }
            foreach (var g in _flagshipThrusterGlows)
            {
                if (g != null) g.SetActive(factor > 0.05f);
            }

            foreach (var l in _carrierThrusterLights)
            {
                if (l != null) l.intensity = 15f * factor;
            }
            foreach (var g in _carrierThrusterGlows)
            {
                if (g != null) g.SetActive(factor > 0.05f);
            }

            foreach (var escort in escortShips)
            {
                if (escort.thrusterLight != null) escort.thrusterLight.intensity = 12f * factor;
                if (escort.thrusterGlow != null) escort.thrusterGlow.SetActive(factor > 0.05f);
            }
        }

        public void AddCameraTrauma(float amount)
        {
            _cameraTrauma = Mathf.Clamp01(_cameraTrauma + amount);
        }

        private Vector3 ApplyCameraShake(Vector3 basePos)
        {
            if (_cameraTrauma <= 0.001f) return basePos;

            float shake = _cameraTrauma * _cameraTrauma;
            float offsetX = (Mathf.PerlinNoise(Time.time * 26f, 0f) - 0.5f) * 2f * shake * 0.85f;
            float offsetY = (Mathf.PerlinNoise(0f, Time.time * 26f) - 0.5f) * 2f * shake * 0.85f;
            float offsetZ = (Mathf.PerlinNoise(Time.time * 26f, Time.time * 26f) - 0.5f) * 2f * shake * 0.5f;

            _cameraTrauma = Mathf.Max(0f, _cameraTrauma - Time.deltaTime * 1.8f);

            return basePos + new Vector3(offsetX, offsetY, offsetZ);
        }

        #region GUI & Title Cards
        private void InitGUIStyles()
        {
            if (_guiInitialized && _whiteTex != null) return;

            _whiteTex = Texture2D.whiteTexture;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _titleStyle.normal.textColor = new Color(0.95f, 0.98f, 1.0f);

            _subTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _subTitleStyle.normal.textColor = new Color(1.0f, 0.45f, 0.15f);

            _hudTelemetryStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft
            };
            _hudTelemetryStyle.normal.textColor = new Color(0.85f, 0.92f, 1.0f);

            _promptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight
            };
            _promptStyle.normal.textColor = new Color(0.3f, 0.95f, 0.85f);

            _guiInitialized = true;
        }

        private void OnGUI()
        {
            if (!showHUD || !Application.isPlaying) return;

            InitGUIStyles();

            float sw = Screen.width;
            float sh = Screen.height;

            // 1. Cinematic 21:9 Letterbox Bars
            float barHeight = sh * 0.10f;
            GUI.color = new Color(0.01f, 0.01f, 0.02f, 0.95f);
            GUI.DrawTexture(new Rect(0, 0, sw, barHeight), _whiteTex);
            GUI.DrawTexture(new Rect(0, sh - barHeight, sw, barHeight), _whiteTex);

            // 2. Cyan Tech Trim Line
            GUI.color = new Color(0f, 0.85f, 1f, 0.65f);
            GUI.DrawTexture(new Rect(0, barHeight - 2f, sw, 2f), _whiteTex);
            GUI.DrawTexture(new Rect(0, sh - barHeight, sw, 2f), _whiteTex);

            // 3. GotG-Style Title Card Banner during Beat 1 & 2
            if (playbackTime < 6.5f)
            {
                float titleAlpha = Mathf.Clamp01(playbackTime / 1.0f);
                if (playbackTime > 5.0f) titleAlpha = Mathf.Clamp01((6.5f - playbackTime) / 1.5f);

                GUI.color = new Color(1f, 1f, 1f, titleAlpha);
                GUI.Label(new Rect(0, barHeight + 25f, sw, 32f), "MARS // SECTOR-00 HIGH ORBIT", _titleStyle);

                GUI.color = new Color(1f, 0.45f, 0.15f, titleAlpha);
                GUI.Label(new Rect(0, barHeight + 58f, sw, 22f), "SEPARATIST BATTLECRUISER ARMADA DETECTED", _subTitleStyle);
            }

            // 4. Tactical Radar Alert Banner during Beat 3 (Ref: 20s - 25s)
            if (currentBeat == 3)
            {
                float cardW = 540f;
                float cardH = 34f;
                float cardX = (sw - cardW) * 0.5f;
                float cardY = sh - barHeight - 48f;

                GUI.color = new Color(0.04f, 0.06f, 0.10f, 0.85f);
                GUI.DrawTexture(new Rect(cardX, cardY, cardW, cardH), _whiteTex);
                GUI.color = new Color(1.0f, 0.35f, 0.15f, 0.9f);
                DrawBorder(new Rect(cardX, cardY, cardW, cardH), 1);

                GUI.color = Color.white;
                var alertStyle = new GUIStyle(_titleStyle) { fontSize = 12, fontStyle = FontStyle.Bold };
                alertStyle.normal.textColor = new Color(1.0f, 0.45f, 0.15f);
                GUI.Label(new Rect(cardX, cardY + 6f, cardW, 20f), "[!] HIGH-ORBITAL SIEGE BLOCKADE ESTABLISHED // MARS CURVATURE CONFIRMED", alertStyle);
            }

            // 5. Telemetry & Interactive Prompts (Top/Bottom corners)
            GUI.color = Color.white;
            GUI.Label(new Rect(32f, 12f, 400f, 22f), $"ORBITAL TELEMETRY: ALTITUDE 42,000 KM // BEAT {currentBeat}/3 // ARMADA COUNT: {2 + escortShips.Count}", _hudTelemetryStyle);

            GUI.color = Color.white;
            GUI.Label(new Rect(sw - 420f, sh - 28f, 390f, 20f), "[SPACE / LMB] SKIP  |  [R] REPLAY  |  [ENTER] MARS SURFACE", _promptStyle);
        }

        private void DrawBorder(Rect rect, int thickness)
        {
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), _whiteTex);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), _whiteTex);
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), _whiteTex);
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), _whiteTex);
        }
        #endregion
    }
}
