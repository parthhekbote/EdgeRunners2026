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
    /// Frame-by-frame exact recreation of Star Wars Fleet Arrival (12s to 25s) adapted for Mars:
    /// - Beat 1 (0:12 - 0:16) [t: 0.0s - 4.5s]: Starfield void -> Warp rupture -> Flagship Dreadnought streaks in from starboard-ventral and SNAP STOPS instantly with fighter escort. Mars is hidden in deep space void.
    /// - Beat 2 (0:17 - 0:19) [t: 4.5s - 7.2s]: Dorsal runway tracking shot past communication masts -> Carrier drops in upper-right starry sky with instant snap deceleration. Mars hidden until 0:19 edge reveal.
    /// - Beat 3 (0:20 - 0:24) [t: 7.2s - 12.3s]: Grand crane pullback behind conning mast -> Colossal Red Planet Mars globe fills 75% of horizon -> Both port cruisers flank on left with amber thrusters -> Carrier drops in lower-right with 3 blazing cyan engine bells -> Flagship 3 amber engine bells ignite in lower-left -> High-orbital siege blockade established!
    /// - Beat 4 (0:25) [t: 12.3s - 13.0s]: Hero flotilla exterior pass against Mars and starfield.
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

            // Authentic deep black space with crisp white pin-point stars (PolygonScifiSpace_Skybox_04)
            if (spaceSkyboxMaterial == null)
                spaceSkyboxMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/PolygonSciFiSpace/Materials/Misc/PolygonScifiSpace_Skybox_04.mat");

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

            // 1. Skybox: Deep black space with pinpoint white stars
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
                cinematicCamera.farClipPlane = 45000f;
                cinematicCamera.fieldOfView = 50f;
                cinematicCamera.tag = "MainCamera";
            }

            // 3. Directional Sun Key Light (Warm Grazing Sunlight on ship prow)
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
            keySunLight.transform.localRotation = Quaternion.Euler(20f, -140f, 0f);
            keySunLight.color = new Color(1.0f, 0.96f, 0.90f);
            keySunLight.intensity = 2.8f;
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
                flagshipColossal.transform.localPosition = Vector3.zero;
                flagshipColossal.transform.localRotation = Quaternion.identity;
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
                carrierGalactic.transform.localPosition = new Vector3(250f, -35f, 110f);
                carrierGalactic.transform.localRotation = Quaternion.Euler(4f, 22f, -2f);
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

#if UNITY_EDITOR
            var sphereMesh = AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>("Assets/Models/Environments/Mesh_HighDef_Planet_Sphere.asset");
            var mf = marsGlobe.GetComponent<MeshFilter>() ?? marsGlobe.AddComponent<MeshFilter>();
            if (sphereMesh != null) mf.sharedMesh = sphereMesh;
#endif

            // Position Mars to fill 75% of upper & right background curvature
            marsGlobe.transform.position = flagshipColossal.transform.position + flagshipColossal.transform.TransformDirection(new Vector3(2600f, 320f, 3000f));
            marsGlobe.transform.localScale = Vector3.one * 5400f;
            marsGlobe.transform.rotation = Quaternion.Euler(20f, 115f, 0f);

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
                cinematicCamera.transform.position = flagshipColossal.transform.TransformPoint(new Vector3(48f, 98f, -280f));
                cinematicCamera.transform.LookAt(flagshipColossal.transform.TransformPoint(new Vector3(10f, 70f, 160f)));
                cinematicCamera.fieldOfView = 52f;
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

            escortShips.Clear();

            var unlitShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var amberMat = new Material(unlitShader) { color = new Color(1.0f, 0.65f, 0.15f) };
            var cyanMat = new Material(unlitShader) { color = new Color(0.1f, 0.95f, 1.0f) };

            var specs = new[] {
                // Port Upper Cruiser (Ref: Upper-Left ship with twin yellow thrusters in black starfield)
                new { Name = "Escort_Cruiser_Port_Upper", Prefab = cruiser1Prefab, Offset = new Vector3(-260f, 145f, 130f), Rot = Quaternion.Euler(4f, -14f, 2f), Scale = 2.0f, Mat = amberMat, Color = new Color(1.0f, 0.65f, 0.15f), GlowOffset = new Vector3(0, 0, -26f), GlowScale = new Vector3(8f, 14f, 8f) },
                // Port Lower Cruiser (Ref: Lower-Left ship with twin yellow thrusters in black starfield)
                new { Name = "Escort_Cruiser_Port_Lower", Prefab = cruiser2Prefab, Offset = new Vector3(-270f, 30f, 70f), Rot = Quaternion.Euler(4f, -16f, 2f), Scale = 1.8f, Mat = amberMat, Color = new Color(1.0f, 0.65f, 0.15f), GlowOffset = new Vector3(0, 0, -25f), GlowScale = new Vector3(7f, 12f, 7f) },
                // Vanguard Lead Cruiser (Ref: Center leading ship diving toward Mars)
                new { Name = "Vanguard_Cruiser_Lead", Prefab = cruiser1Prefab, Offset = new Vector3(140f, 190f, 750f), Rot = Quaternion.Euler(8f, 16f, 0f), Scale = 2.2f, Mat = amberMat, Color = new Color(1.0f, 0.65f, 0.15f), GlowOffset = new Vector3(0, 0, -26f), GlowScale = new Vector3(8f, 14f, 8f) },
                // Vanguard Wing Left (Ref: Upper distant escort over Mars)
                new { Name = "Vanguard_Escort_Left", Prefab = stealthPrefab, Offset = new Vector3(450f, 280f, 1100f), Rot = Quaternion.Euler(8f, 18f, 0f), Scale = 2.5f, Mat = cyanMat, Color = new Color(0.15f, 0.9f, 1.0f), GlowOffset = new Vector3(0, 0, -10f), GlowScale = new Vector3(5f, 8f, 5f) },
                // Vanguard Wing Right
                new { Name = "Vanguard_Escort_Right", Prefab = stealthPrefab, Offset = new Vector3(-120f, 260f, 1200f), Rot = Quaternion.Euler(8f, 18f, 0f), Scale = 2.5f, Mat = amberMat, Color = new Color(1.0f, 0.65f, 0.15f), GlowOffset = new Vector3(0, 0, -10f), GlowScale = new Vector3(5f, 8f, 5f) },
                // Starboard Fighter Escort Pair (Fighter 1 is under the belly in Beat 1)
                new { Name = "Fighter_Escort_1", Prefab = fighterPrefab, Offset = new Vector3(-20f, -140f, -30f), Rot = Quaternion.identity, Scale = 1.5f, Mat = cyanMat, Color = new Color(0.15f, 0.9f, 1.0f), GlowOffset = new Vector3(0, 0, -6f), GlowScale = new Vector3(3f, 5f, 3f) },
                new { Name = "Fighter_Escort_2", Prefab = fighterPrefab, Offset = new Vector3(320f, 50f, 260f), Rot = Quaternion.Euler(6f, 20f, 0f), Scale = 2.0f, Mat = cyanMat, Color = new Color(0.15f, 0.9f, 1.0f), GlowOffset = new Vector3(0, 0, -6f), GlowScale = new Vector3(3f, 5f, 3f) }
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
                tLight.range = 100f;
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

            // Flagship stern is at Z = -305, Carrier stern is at Z = -135
            Vector3[] localOffsets = isFlagship ?
                new Vector3[] { new Vector3(-25f, 6f, -305f), new Vector3(25f, 6f, -305f), new Vector3(0f, 22f, -305f) } :
                new Vector3[] { new Vector3(-18f, 5f, -135f), new Vector3(18f, 5f, -135f), new Vector3(0f, 0f, -138f) };

            Vector3 glowScale = isFlagship ? new Vector3(18f, 26f, 18f) : new Vector3(16f, 22f, 16f);
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
                l.range = 160f;
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

                if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Escape))
                {
                    SkipCutscene();
                }

                if (Input.GetKeyDown(KeyCode.R))
                {
                    PlayCutscene();
                }

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
            playbackTime = 12.0f;
            EvaluateCutsceneAtTime(12.0f);

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

        /// <summary>
        /// Deterministic frame evaluation: Sets camera, ships, lighting, and FX for any time t in [0..13]
        /// corresponding to reference video timestamp 0:12 (t=0) to 0:25 (t=13).
        /// </summary>
        public void EvaluateCutsceneAtTime(float t)
        {
            EnsureStageBuilt();

            // ════════════════════════════════════════════════════════════════
            // BEAT 1 (0:12 – 0:16.5) [t: 0.0s to 4.5s]: STARFIELD VOID & FLAGSHIP WARP SNAP
            // ════════════════════════════════════════════════════════════════
            if (t < 4.5f)
            {
                currentBeat = 1;

                if (marsGlobe != null) marsGlobe.SetActive(false);
                if (carrierGalactic != null) carrierGalactic.SetActive(false);

                // Small belly fighter active at t >= 1.0s
                var f1 = GameObject.Find("Fighter_Escort_1");
                if (f1 != null) f1.SetActive(t >= 1.0f);

                // Other escorts hidden
                foreach (var escort in escortShips)
                {
                    if (escort.shipObject != null && escort.name != "Fighter_Escort_1")
                    {
                        escort.shipObject.SetActive(false);
                    }
                }

                // Warp breach at t = 1.8s
                if (t < 1.8f)
                {
                    if (flagshipColossal != null) flagshipColossal.SetActive(false);
                    SetThrustersIntensity(0f);

                    float b1InitNorm = t / 1.8f;
                    Vector3 camPos = Vector3.Lerp(new Vector3(380f, -60f, 520f), new Vector3(350f, -52f, 490f), b1InitNorm);
                    Vector3 lookTarget = Vector3.Lerp(new Vector3(20f, 30f, 200f), new Vector3(0f, 28f, 100f), b1InitNorm);
                    cinematicCamera.transform.position = camPos;
                    cinematicCamera.transform.LookAt(lookTarget);
                    cinematicCamera.fieldOfView = 46f;
                }
                else
                {
                    // Flagship breaches warp and SNAP STOPS instantly (t: 1.8s to 2.15s, then dead stop)
                    if (flagshipColossal != null) flagshipColossal.SetActive(true);

                    float snapDur = 0.35f;
                    float snapTime = t - 1.8f;

                    Vector3 shipWarpStart = new Vector3(200f, 30f, 950f);
                    Vector3 shipRestPos = Vector3.zero;

                    if (snapTime <= snapDur)
                    {
                        float st = snapTime / snapDur;
                        float snapProgress = 1.0f - Mathf.Pow(1.0f - st, 5f);
                        flagshipColossal.transform.position = Vector3.Lerp(shipWarpStart, shipRestPos, snapProgress);
                    }
                    else
                    {
                        float drift = snapTime - snapDur;
                        flagshipColossal.transform.position = shipRestPos + new Vector3(0f, 0f, -0.1f * drift);
                    }
                    flagshipColossal.transform.rotation = Quaternion.identity;

                    // Camera holding heroic starboard-ventral angle looking up at prow and conning tower
                    float b1HoldNorm = Mathf.Clamp01((t - 1.8f) / 2.7f);
                    Vector3 camPos = Vector3.Lerp(new Vector3(340f, -50f, 480f), new Vector3(310f, -42f, 440f), b1HoldNorm);
                    Vector3 lookTarget = Vector3.Lerp(new Vector3(-10f, 25f, 50f), new Vector3(-15f, 28f, 30f), b1HoldNorm);
                    cinematicCamera.transform.position = camPos;
                    cinematicCamera.transform.LookAt(lookTarget);
                    cinematicCamera.fieldOfView = 44f;

                    SetThrustersIntensity(0f);
                }
            }
            // ════════════════════════════════════════════════════════════════
            // BEAT 2 (0:16.5 – 0:19.2) [t: 4.5s to 7.2s]: DORSAL RUNWAY TRACKING & CARRIER SNAP
            // ════════════════════════════════════════════════════════════════
            else if (t < 7.2f)
            {
                currentBeat = 2;

                if (marsGlobe != null)
                {
                    marsGlobe.SetActive(t >= 6.8f);
                    if (t >= 6.8f)
                    {
                        float revealNorm = Mathf.Clamp01((t - 6.8f) / 0.4f);
                        marsGlobe.transform.position = flagshipColossal.transform.position + flagshipColossal.transform.TransformDirection(Vector3.Lerp(new Vector3(4500f, 400f, 3000f), new Vector3(2600f, 320f, 3000f), revealNorm));
                    }
                }

                if (flagshipColossal != null)
                {
                    flagshipColossal.SetActive(true);
                    flagshipColossal.transform.position = Vector3.zero;
                    flagshipColossal.transform.rotation = Quaternion.identity;
                }

                // Carrier emergence at t = 4.8s
                if (carrierGalactic != null)
                {
                    if (t < 4.8f)
                    {
                        carrierGalactic.SetActive(false);
                    }
                    else
                    {
                        carrierGalactic.SetActive(true);
                        float cSnapDur = 0.35f;
                        float cSnapTime = t - 4.8f;

                        Vector3 carrierWarpStart = flagshipColossal.transform.TransformPoint(new Vector3(-350f, 220f, 200f));
                        Vector3 carrierRestPos = flagshipColossal.transform.TransformPoint(new Vector3(-240f, 130f, -320f));

                        if (cSnapTime <= cSnapDur)
                        {
                            float cst = cSnapTime / cSnapDur;
                            float cSnapProgress = 1.0f - Mathf.Pow(1.0f - cst, 5f);
                            carrierGalactic.transform.position = Vector3.Lerp(carrierWarpStart, carrierRestPos, cSnapProgress);
                        }
                        else
                        {
                            carrierGalactic.transform.position = carrierRestPos;
                        }
                        carrierGalactic.transform.rotation = flagshipColossal.transform.rotation * Quaternion.Euler(0f, 190f, 0f);
                    }
                }

                // Camera skimming dorsal runway looking aft-starboard
                if (t < 6.8f)
                {
                    float trackNorm = (t - 4.5f) / 2.3f;
                    Vector3 camLocal = Vector3.Lerp(new Vector3(-12f, 95f, -30f), new Vector3(-8f, 92f, -80f), trackNorm);
                    Vector3 lookLocal = Vector3.Lerp(new Vector3(-42f, 98f, -240f), new Vector3(-38f, 98f, -250f), trackNorm);
                    cinematicCamera.transform.position = flagshipColossal.transform.TransformPoint(camLocal);
                    cinematicCamera.transform.LookAt(flagshipColossal.transform.TransformPoint(lookLocal));
                    cinematicCamera.fieldOfView = 54f;
                }
                else
                {
                    // Transition to Beat 3: crane up and swing forward
                    float swingNorm = (t - 6.8f) / 0.4f;
                    Vector3 camLocal = Vector3.Lerp(new Vector3(-8f, 92f, -80f), new Vector3(35f, 85f, -200f), swingNorm);
                    Vector3 lookLocal = Vector3.Lerp(new Vector3(-38f, 98f, -250f), new Vector3(0f, 65f, 160f), swingNorm);
                    cinematicCamera.transform.position = flagshipColossal.transform.TransformPoint(camLocal);
                    cinematicCamera.transform.LookAt(flagshipColossal.transform.TransformPoint(lookLocal));
                    cinematicCamera.fieldOfView = Mathf.Lerp(54f, 50f, swingNorm);
                }

                SetThrustersIntensity(0f);
            }
            // ════════════════════════════════════════════════════════════════
            // BEAT 3 (0:19.2 – 0:24.3) [t: 7.2s to 12.3s]: GRAND MARS ORBITAL BLOCKADE
            // ════════════════════════════════════════════════════════════════
            else if (t < 12.7f)
            {
                currentBeat = 3;

                if (marsGlobe != null)
                {
                    marsGlobe.SetActive(true);
                    marsGlobe.transform.position = flagshipColossal.transform.position + flagshipColossal.transform.TransformDirection(new Vector3(2600f, 320f, 3000f));
                    marsGlobe.transform.localScale = Vector3.one * 5400f;
                    marsGlobe.transform.rotation = Quaternion.Euler(20f, 115f, 0f);
                }

                if (flagshipColossal != null)
                {
                    flagshipColossal.SetActive(true);
                    flagshipColossal.transform.position = Vector3.zero;
                    flagshipColossal.transform.rotation = Quaternion.identity;
                }

                // All escort flotilla active in formation
                foreach (var escort in escortShips)
                {
                    if (escort.shipObject != null)
                    {
                        escort.shipObject.SetActive(true);
                        escort.shipObject.transform.position = flagshipColossal.transform.position + flagshipColossal.transform.TransformDirection(escort.formationOffset);
                        escort.shipObject.transform.rotation = escort.formationRotation;
                    }
                }

                // Carrier drops in lower-right foreground at t = 10.4s (sec 23)
                if (carrierGalactic != null)
                {
                    carrierGalactic.SetActive(true);
                    Vector3 carrierRestPos = flagshipColossal.transform.TransformPoint(new Vector3(250f, -35f, 110f));
                    Vector3 carrierWarpIn = flagshipColossal.transform.TransformPoint(new Vector3(250f, -20f, 600f));

                    if (t < 10.4f)
                    {
                        carrierGalactic.transform.position = carrierRestPos;
                    }
                    else
                    {
                        float cSnapDur = 0.35f;
                        float cSnapTime = t - 10.4f;
                        if (cSnapTime <= cSnapDur)
                        {
                            float cst = cSnapTime / cSnapDur;
                            float cSnapProgress = 1.0f - Mathf.Pow(1.0f - cst, 5f);
                            carrierGalactic.transform.position = Vector3.Lerp(carrierWarpIn, carrierRestPos, cSnapProgress);
                        }
                        else
                        {
                            carrierGalactic.transform.position = carrierRestPos;
                        }
                    }
                    carrierGalactic.transform.rotation = flagshipColossal.transform.rotation * Quaternion.Euler(4f, 22f, -2f);
                }

                // Camera smooth crane pullback along the dorsal spine behind the stern:
                // t = 7.2s (sec 19.5): (25, 70, -140) -> Look (0, 65, 160)
                // t = 8.0s (sec 20):   (28, 74, -170) -> Look (3, 66, 160)
                // t = 9.0s (sec 21):   (32, 78, -200) -> Look (6, 68, 160)
                // t = 10.0s (sec 22):  (40, 86, -240) -> Look (8, 70, 160)
                // t = 11.0s (sec 23):  (48, 98, -280) -> Look (10, 70, 160)
                // t = 12.0s (sec 24):  (55, 115, -340) -> Look (15, 65, 150)
                float b3Norm = Mathf.Clamp01((t - 7.2f) / 4.8f);
                float b3Smooth = Mathf.SmoothStep(0f, 1f, b3Norm);

                Vector3 b3CamStart = new Vector3(35f, 85f, -200f);
                Vector3 b3CamEnd = new Vector3(55f, 115f, -340f);
                Vector3 b3LookStart = new Vector3(0f, 65f, 160f);
                Vector3 b3LookEnd = new Vector3(15f, 65f, 150f);

                Vector3 curCamLocal = Vector3.Lerp(b3CamStart, b3CamEnd, b3Smooth);
                Vector3 curLookLocal = Vector3.Lerp(b3LookStart, b3LookEnd, b3Smooth);

                cinematicCamera.transform.position = flagshipColossal.transform.TransformPoint(curCamLocal);
                cinematicCamera.transform.LookAt(flagshipColossal.transform.TransformPoint(curLookLocal));
                cinematicCamera.fieldOfView = Mathf.Lerp(50f, 54f, b3Smooth);

                // Thrusters ignite across armada
                float thIntensity = Mathf.Clamp01((t - 8.0f) / 1.5f);
                SetThrustersIntensity(thIntensity);
            }
            // ════════════════════════════════════════════════════════════════
            // BEAT 4 (0:24.3 – 0:25.0) [t: 12.3s to 13.0s]: HERO FLOTILLA EXTERIOR (SEC 25)
            // ════════════════════════════════════════════════════════════════
            else
            {
                currentBeat = 4;

                // In reference sec 25, cut to wide starfield exterior (deep space, Mars not filling frame)
                if (marsGlobe != null) marsGlobe.SetActive(false);
                if (flagshipColossal != null)
                {
                    flagshipColossal.SetActive(true);
                    flagshipColossal.transform.position = Vector3.zero;
                    flagshipColossal.transform.rotation = Quaternion.identity;
                }
                if (carrierGalactic != null)
                {
                    carrierGalactic.SetActive(true);
                    carrierGalactic.transform.position = flagshipColossal.transform.TransformPoint(new Vector3(320f, 60f, 340f));
                    carrierGalactic.transform.rotation = flagshipColossal.transform.rotation * Quaternion.Euler(0f, -6f, 0f);
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

                // Wide heroic exterior pass matching ref sec 25: conning tower/bridge on left, wedge nose to right, deep starfield
                float b4Norm = Mathf.Clamp01((t - 12.7f) / 0.3f);
                Vector3 camPos = Vector3.Lerp(new Vector3(450f, 110f, -60f), new Vector3(440f, 105f, -50f), b4Norm);
                Vector3 lookTarget = new Vector3(-30f, 20f, 80f);
                cinematicCamera.transform.position = camPos;
                cinematicCamera.transform.LookAt(lookTarget);
                cinematicCamera.fieldOfView = 46f;

                SetThrustersIntensity(1.0f);
            }
        }

        private IEnumerator ExecuteOrbitalFleetArrivalSequence()
        {
            isPlaying = true;
            playbackTime = 0f;
            currentBeat = 1;

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

            bool warp1Triggered = false;
            bool warp2Triggered = false;
            bool warp3Triggered = false;

            float totalDuration = 13.0f;

            while (playbackTime < totalDuration)
            {
                playbackTime += Time.deltaTime;

                // Warp FX triggers
                if (!warp1Triggered && playbackTime >= 1.6f)
                {
                    warp1Triggered = true;
                    Vector3 warpPoint1 = transform.position + new Vector3(200f, 20f, 500f);
                    StartCoroutine(TriggerWarpShockwave(warpPoint1, new Color(0.35f, 0.85f, 1f), 1.6f));
                    AddCameraTrauma(0.85f);
                    if (_sfxSource != null && warpBoomAudio != null) _sfxSource.PlayOneShot(warpBoomAudio, 1.0f);
                }

                if (!warp2Triggered && playbackTime >= 4.6f)
                {
                    warp2Triggered = true;
                    Vector3 warpPoint2 = flagshipColossal.transform.TransformPoint(new Vector3(-350f, 220f, 200f));
                    StartCoroutine(TriggerWarpShockwave(warpPoint2, new Color(0.2f, 0.95f, 0.85f), 1.4f));
                    AddCameraTrauma(0.75f);
                    if (_sfxSource != null && warpBoomAudio != null) _sfxSource.PlayOneShot(warpBoomAudio, 0.85f);
                }

                if (!warp3Triggered && playbackTime >= 10.2f)
                {
                    warp3Triggered = true;
                    Vector3 warpPoint3 = flagshipColossal.transform.TransformPoint(new Vector3(250f, -20f, 500f));
                    StartCoroutine(TriggerWarpShockwave(warpPoint3, new Color(0.15f, 0.9f, 1f), 1.2f));
                    AddCameraTrauma(0.6f);
                    if (_sfxSource != null && warpBoomAudio != null) _sfxSource.PlayOneShot(warpBoomAudio, 0.75f);
                }

                EvaluateCutsceneAtTime(playbackTime);

                if (cinematicCamera != null)
                {
                    cinematicCamera.transform.position = ApplyCameraShake(cinematicCamera.transform.position);
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

        public void SetThrustersIntensity(float factor)
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
                if (l != null) l.intensity = 18f * factor;
            }
            foreach (var g in _carrierThrusterGlows)
            {
                if (g != null) g.SetActive(factor > 0.05f);
            }

            foreach (var escort in escortShips)
            {
                if (escort.thrusterLight != null) escort.thrusterLight.intensity = 14f * factor;
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
            GUI.Label(new Rect(32f, 12f, 400f, 22f), $"ORBITAL TELEMETRY: ALTITUDE 42,000 KM // BEAT {currentBeat}/4 // ARMADA COUNT: {2 + escortShips.Count}", _hudTelemetryStyle);

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
