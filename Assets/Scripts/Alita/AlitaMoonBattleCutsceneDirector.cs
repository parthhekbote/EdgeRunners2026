using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Alita
{
    /// <summary>
    /// Exact 1-13s cinematic phases matching the reference video (https://youtu.be/wEvlaJFcir0)
    /// Sampled and choreographed at 4 frames per second across 12 seconds.
    /// </summary>
    public enum MoonBattleCutscenePhase
    {
        BootSprintDust = 0,        // 0.0s - 1.6s: Low-angle boots sprinting in lunar dust (Ref Sec 1.0 - 2.6)
        LowGravitySpaceFleet = 1,  // 1.6s - 3.4s: Leap into zero-g, spaceships & Earth backdrop (Ref Sec 2.6 - 4.4)
        BattlefieldChaos = 2,      // 3.4s - 5.6s: Wide army crossfire & giant ridge explosions (Ref Sec 4.4 - 6.6)
        AlitaHeroSlide = 3,        // 5.6s - 7.4s: Alita 3-point slide with blast behind her (Ref Sec 6.6 - 8.4)
        CommanderAssault = 4,      // 7.4s - 9.4s: Gelda leading the charge, firing rifle bursts (Ref Sec 8.4 - 10.4)
        AlitaChargeSpring = 5,     // 9.4s - 10.6s: Alita bursting up from the ground (Ref Sec 10.4 - 11.6)
        MeleeZeroGClash = 6        // 10.6s - 12.0s: Dynamic hand-to-hand tackle and sparks (Ref Sec 11.6 - 13.0)
    }

    [System.Serializable]
    public class CinematicCameraShot
    {
        public string shotName;
        public Vector3 startPos;
        public Vector3 endPos;
        public Vector3 startRotEuler;
        public Vector3 endRotEuler;
        public float startFov = 50f;
        public float endFov = 45f;
        public float startRoll = 0f;
        public float endRoll = 0f;
        public float shakeBase = 0.02f;
    }

    /// <summary>
    /// Master 12-second AAA Cinematic Cutscene Director for the Alita Moon Flashback Battle.
    /// Perfectly mirrors the authentic movie sequence (1-13s from https://youtu.be/wEvlaJFcir0)
    /// analyzed at 4 frames per second with identical audio design:
    /// - Audio: Assets/Audio/MoonBattle/Alita_MoonBattle_1_13.wav
    /// - Zero HUD / No HUD: Pure cinematic widescreen presentation, all UI elements removed.
    /// - Shot 1: Extreme low-angle lunar boots sprint & dust impact shockwave.
    /// - Shot 2: Low-gravity leap following soldiers into space sky, glowing Earth & starfighters.
    /// - Shot 3: Wide crater battlefield crossfire with massive concussion detonation.
    /// - Shot 4: Alita sliding in iconic 3-point hero stance, massive explosion bursting behind her.
    /// - Shot 5: Commander Gelda advancing and firing rifle bursts with squad.
    /// - Shot 6: Alita springing from slide to explosive sprint forward.
    /// - Shot 7: Dynamic zero-g melee clash, mid-air tackle & energy sparks.
    /// </summary>
    [ExecuteAlways]
    public class AlitaMoonBattleCutsceneDirector : MonoBehaviour
    {
        public static AlitaMoonBattleCutsceneDirector Instance { get; private set; }

        [Header("Playback State")]
        public MoonBattleCutscenePhase currentPhase = MoonBattleCutscenePhase.BootSprintDust;
        public float currentTime = 0f;
        public float totalDuration = 13.0f;
        public bool isPlaying = true;
        public bool autoPlayOnStart = true;
        public bool loopAfterFinish = false;

        [Header("Cinematic Display")]
        [Tooltip("When false, removes all HUD, timecode, debug texts and overlays for pure cinema presentation.")]
        public bool showHUD = false;

        [Header("Cinematic Camera Rig")]
        public Camera cinematicCamera;
        public float cameraShakeIntensity = 0f;

        [Header("Actors & Armies")]
        public GameObject alitaHero;
        public GameObject commanderGelda;
        public List<GameObject> squadTroops = new List<GameObject>();
        public List<GameObject> enemyTroops = new List<GameObject>();

        [Header("Star Wars Space Fleet & Dogfights")]
        public List<GameObject> starfighters = new List<GameObject>();
        public List<GameObject> enemyInterceptors = new List<GameObject>();
        public GameObject assaultGunship;
        public GameObject planetaryCannon;
        public GameObject horizonTitanMech;
        public List<LineRenderer> dogfightLaserBeams = new List<LineRenderer>();

        [Header("Environment & Celestial")]
        public Transform earthTransform;
        public Light lunarSunLight;

        [Header("VFX & Particles")]
        public GameObject tracerVFXRoot;
        public GameObject muzzleFlashVFXRoot;
        public GameObject explosionVFXRoot;

        [Header("Audio")]
        public AudioSource musicSource;
        public AudioSource combatSFXSource;
        public AudioClip combatSoundtrack;

        // Letterbox Styling
        private Texture2D _blackPixel;
        private Texture2D _boxBgPixel;

        // Predefined Camera Shots
        private Dictionary<MoonBattleCutscenePhase, CinematicCameraShot> _shots = new Dictionary<MoonBattleCutscenePhase, CinematicCameraShot>();

        // Battle Army Formation Caches (16 squad members, 14 enemy defenders)
        private Vector3[] _squadBaseOffsets;
        private Vector3[] _enemyBaseOffsets;
        private bool _armiesReinforced = false;

        private void OnEnable()
        {
            Instance = this;
            SetupStyles();
            SetupShots();
            SetupFormations();
        }

        private void Awake()
        {
            Instance = this;
            SetupStyles();
            SetupShots();
            SetupFormations();
            EnsureBattleArmies();
            LoadAudioTrack();
            HideSceneGameplayHUD();
        }

        private void Start()
        {
            if (cinematicCamera == null)
            {
                var cineCamGo = GameObject.Find("Moon_Battle_Cinematic_Camera");
                if (cineCamGo != null)
                {
                    cinematicCamera = cineCamGo.GetComponent<Camera>();
                }
                if (cinematicCamera == null)
                {
                    cinematicCamera = Camera.main;
                }
            }

            if (cinematicCamera != null)
            {
                cinematicCamera.depth = 1;
            }

            // Deactivate redundant or conflicting scene cameras so cinematic camera has exclusive control
            var extraCameras = GameObject.Find("[Cinematic_Cutscene_Cameras]");
            if (extraCameras != null)
            {
                extraCameras.SetActive(false);
            }

            var mainCam = GameObject.Find("Main Camera");
            if (mainCam != null && cinematicCamera != null && mainCam != cinematicCamera.gameObject)
            {
                mainCam.SetActive(false);
            }

            EnsureBattleArmies();
            LoadAudioTrack();
            HideSceneGameplayHUD();

            if (autoPlayOnStart && Application.isPlaying)
            {
                StartCutscene();
            }
        }

        public void LoadAudioTrack()
        {
            if (combatSoundtrack == null)
            {
#if UNITY_EDITOR
                combatSoundtrack = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/MoonBattle/Alita_MoonBattle_1_13.wav");
#endif
            }

            if (musicSource == null)
            {
                musicSource = GetComponent<AudioSource>();
                if (musicSource == null)
                {
                    musicSource = gameObject.AddComponent<AudioSource>();
                }
            }

            if (musicSource != null && combatSoundtrack != null)
            {
                musicSource.clip = combatSoundtrack;
                musicSource.playOnAwake = false;
                musicSource.loop = loopAfterFinish;
                musicSource.volume = 1.0f;
            }
        }

        /// <summary>
        /// Automatically disables any gameplay UI Canvas / HUD in the scene during the cutscene.
        /// Addresses user request: "remove hub in cutscenes".
        /// </summary>
        public void HideSceneGameplayHUD()
        {
            var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var c in canvases)
            {
                if (c.renderMode == RenderMode.ScreenSpaceOverlay || 
                    c.gameObject.name.IndexOf("HUD", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    c.gameObject.name.IndexOf("UI", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    c.gameObject.SetActive(false);
                }
            }
        }

        private void SetupStyles()
        {
            if (_blackPixel == null)
            {
                _blackPixel = new Texture2D(1, 1);
                _blackPixel.SetPixel(0, 0, Color.black);
                _blackPixel.Apply();
            }

            if (_boxBgPixel == null)
            {
                _boxBgPixel = new Texture2D(1, 1);
                _boxBgPixel.SetPixel(0, 0, new Color(0.04f, 0.06f, 0.10f, 0.90f));
                _boxBgPixel.Apply();
            }
        }

        private void SetupFormations()
        {
            // 16 Squad Formation Positions across the lunar front (30m tactical front)
            _squadBaseOffsets = new Vector3[]
            {
                new Vector3(-13.5f, 0.15f, -5.5f), // Far left vanguard wing
                new Vector3(-10.2f, 0.12f, -8.5f), // Left flank rifleman
                new Vector3(-12.0f, 0.20f, -11.5f), // Left rear anchor
                new Vector3(-7.5f, 0.15f, -6.0f),  // Left-mid skirmisher
                new Vector3(-5.2f, 0.10f, -9.5f),  // Mid-left support
                new Vector3(-2.8f, 0.12f, -7.0f),  // Center-left point
                new Vector3(-0.8f, 0.10f, -11.0f), // Center rearguard
                new Vector3(2.2f, 0.10f, -6.5f),   // Center-right point
                new Vector3(4.5f, 0.15f, -10.0f),  // Mid-right support
                new Vector3(6.8f, 0.18f, -5.5f),   // Right-mid skirmisher
                new Vector3(9.5f, 0.20f, -8.0f),   // Right flank rifleman
                new Vector3(12.2f, 0.22f, -6.2f),  // Far right vanguard wing
                new Vector3(14.5f, 0.25f, -9.5f),  // Far right perimeter
                new Vector3(11.0f, 0.20f, -12.5f), // Right rear anchor
                new Vector3(-8.8f, 0.18f, -13.0f), // Deep left reserve
                new Vector3(7.5f, 0.20f, -13.5f)   // Deep right reserve
            };

            // 14 Enemy Defense Positions along the crater ridge
            _enemyBaseOffsets = new Vector3[]
            {
                new Vector3(-10.5f, 0.25f, 1.0f),  // Far left ridge bunker
                new Vector3(-7.2f, 0.20f, 2.5f),   // Left trench gunner
                new Vector3(-4.5f, 0.15f, 0.0f),   // Left pillbox defender
                new Vector3(-1.8f, 0.15f, -0.6f),  // Center-left barricade
                new Vector3(1.5f, 0.18f, 0.8f),    // Center-right rifleman
                new Vector3(4.5f, 0.22f, 2.2f),    // Right elevated redoubt
                new Vector3(7.8f, 0.30f, 3.5f),    // Right heavy outpost
                new Vector3(11.2f, 0.35f, 4.8f),   // Far right sniper nest
                new Vector3(-12.8f, 0.30f, 3.0f),  // Far left ridge outpost
                new Vector3(-2.5f, 0.12f, 3.8f),   // Center backline support
                new Vector3(2.8f, 0.25f, 3.2f),    // Center-right pillbox
                new Vector3(6.2f, 0.18f, -0.8f),   // Forward trench picket
                new Vector3(9.2f, 0.25f, 1.5f),    // Right perimeter line
                new Vector3(-5.5f, 0.20f, 4.2f)    // Left reserve defender
            };
        }

        public void EnsureBattleArmies()
        {
            if (_armiesReinforced) return;

            if (squadTroops == null) squadTroops = new List<GameObject>();
            if (enemyTroops == null) enemyTroops = new List<GameObject>();

            squadTroops.RemoveAll(go => go == null);
            enemyTroops.RemoveAll(go => go == null);

            var squadParent = GameObject.Find("Vanguard_Assault_Squad")?.transform;
            if (squadParent != null)
            {
                foreach (Transform child in squadParent)
                {
                    if (child.name.Contains("Soldier") && !squadTroops.Contains(child.gameObject))
                    {
                        squadTroops.Add(child.gameObject);
                    }
                }
            }

            var enemyParent = GameObject.Find("Hostile_Perimeter_Defense")?.transform;
            if (enemyParent != null)
            {
                foreach (Transform child in enemyParent)
                {
                    if (child.name.Contains("Enemy") && !enemyTroops.Contains(child.gameObject))
                    {
                        enemyTroops.Add(child.gameObject);
                    }
                }
            }

            if (earthTransform == null)
            {
                earthTransform = GameObject.Find("Earth_In_Lunar_Sky")?.transform;
            }
            if (earthTransform != null)
            {
                earthTransform.position = new Vector3(351.7f, 129.2f, 174.6f);
                earthTransform.localScale = new Vector3(130f, 130f, 1f);
            }

            RuntimeAnimatorController shootCtrl = null;
#if UNITY_EDITOR
            shootCtrl = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/ShootRifle_Controller.controller");
#endif

            if (squadTroops.Count > 0 && squadTroops.Count < 16)
            {
                int baseCount = squadTroops.Count;
                Transform parentT = squadParent != null ? squadParent : squadTroops[0].transform.parent;
                for (int i = baseCount; i < 16; i++)
                {
                    var template = squadTroops[i % baseCount];
                    if (template == null) continue;
                    var clone = Instantiate(template, parentT);
                    clone.name = $"URM_Vanguard_Soldier_{i + 1}";
                    clone.hideFlags = HideFlags.DontSave;
                    if (shootCtrl != null)
                    {
                        var anim = clone.GetComponent<Animator>();
                        if (anim != null) anim.runtimeAnimatorController = shootCtrl;
                    }
                    squadTroops.Add(clone);
                }
            }

            if (enemyTroops.Count > 0 && enemyTroops.Count < 14)
            {
                int baseEnemy = enemyTroops.Count;
                Transform parentT = enemyParent != null ? enemyParent : enemyTroops[0].transform.parent;
                for (int e = baseEnemy; e < 14; e++)
                {
                    var template = enemyTroops[e % baseEnemy];
                    if (template == null) continue;
                    var clone = Instantiate(template, parentT);
                    clone.name = $"Enemy_Defender_{e + 1}";
                    clone.hideFlags = HideFlags.DontSave;
                    if (shootCtrl != null)
                    {
                        var anim = clone.GetComponent<Animator>();
                        if (anim != null) anim.runtimeAnimatorController = shootCtrl;
                    }
                    enemyTroops.Add(clone);
                }
            }

            _armiesReinforced = true;
        }

        public void SetupShots()
        {
            _shots.Clear();
            Vector3 center = new Vector3(172f, 13f, 10f);

            // Shot 0: BootSprintDust (0.0s - 2.5s, Ref F1 - F10)
            // Extreme low-angle boots running in lunar sand, heavy impact kicking dust into lens
            _shots[MoonBattleCutscenePhase.BootSprintDust] = new CinematicCameraShot
            {
                shotName = "LowAngle_Boot_Sprint_Impact",
                startPos = center + new Vector3(-0.95f, 0.12f, -6.6f),
                endPos = center + new Vector3(-0.35f, 0.10f, -5.0f),
                startRotEuler = new Vector3(14f, 24f, -2.0f),
                endRotEuler = new Vector3(18f, 28f, -1.0f),
                startFov = 38f,
                endFov = 34f,
                startRoll = -2.0f,
                endRoll = -1.0f,
                shakeBase = 0.02f
            };

            // Shot 1: LowGravitySpaceFleet (2.5s - 3.5s, Ref F11 - F14)
            // Camera tilts rapidly up into black cosmos, tracking soldiers leaping into low-g under glowing Earth
            _shots[MoonBattleCutscenePhase.LowGravitySpaceFleet] = new CinematicCameraShot
            {
                shotName = "ZeroG_Leap_SpaceFleet_Ascent",
                startPos = center + new Vector3(-1.8f, 0.40f, -5.2f),
                endPos = center + new Vector3(0.8f, 1.60f, -3.2f),
                startRotEuler = new Vector3(8f, 36f, 3.0f),
                endRotEuler = new Vector3(-16f, 44f, 5.0f),
                startFov = 48f,
                endFov = 54f,
                startRoll = 3.0f,
                endRoll = 5.0f,
                shakeBase = 0.015f
            };

            // Shot 2: BattlefieldChaos (3.5s - 5.8s, Ref F15 - F24)
            // Wide tracking shot right across crater ridges, army firing, massive concussion explosion
            _shots[MoonBattleCutscenePhase.BattlefieldChaos] = new CinematicCameraShot
            {
                shotName = "Wide_Crater_Battlefield_Concussion",
                startPos = center + new Vector3(-12.0f, 2.0f, -8.0f),
                endPos = center + new Vector3(-5.0f, 2.3f, -4.0f),
                startRotEuler = new Vector3(11f, 42f, -1.5f),
                endRotEuler = new Vector3(8f, 36f, -0.5f),
                startFov = 62f,
                endFov = 55f,
                startRoll = -1.5f,
                endRoll = -0.5f,
                shakeBase = 0.035f
            };

            // Shot 3: AlitaHeroSlide (5.8s - 7.4s, Ref F25 - F30)
            // Low 3/4 tracking camera facing Alita in 3-point hero stance, massive explosion bursting behind her
            _shots[MoonBattleCutscenePhase.AlitaHeroSlide] = new CinematicCameraShot
            {
                shotName = "Alita_3Point_Hero_Slide_Blast",
                startPos = center + new Vector3(-0.65f, 0.45f, 3.3f),
                endPos = center + new Vector3(-0.25f, 0.42f, 2.6f),
                startRotEuler = new Vector3(-6f, 155f, 1.5f),
                endRotEuler = new Vector3(-4f, 148f, 0.5f),
                startFov = 40f,
                endFov = 35f,
                startRoll = 1.5f,
                endRoll = 0.5f,
                shakeBase = 0.025f
            };

            // Shot 4: CommanderAssault (7.4s - 9.4s, Ref F31 - F38)
            // Close profile tracking shot on Commander Gelda firing rifle bursts and advancing
            _shots[MoonBattleCutscenePhase.CommanderAssault] = new CinematicCameraShot
            {
                shotName = "Commander_Gelda_Rifle_Burst",
                startPos = center + new Vector3(-2.8f, 1.25f, 0.6f),
                endPos = center + new Vector3(-2.3f, 1.28f, 1.6f),
                startRotEuler = new Vector3(2f, 44f, -1.0f),
                endRotEuler = new Vector3(4f, 48f, -0.5f),
                startFov = 34f,
                endFov = 30f,
                startRoll = -1.0f,
                endRoll = -0.5f,
                shakeBase = 0.015f
            };

            // Shot 5: AlitaChargeSpring (9.4s - 10.4s, Ref F39 - F42)
            // Low dynamic snap behind Alita springing off the ground and rocketing forward
            _shots[MoonBattleCutscenePhase.AlitaChargeSpring] = new CinematicCameraShot
            {
                shotName = "Alita_Spring_Acceleration",
                startPos = center + new Vector3(0.2f, 0.45f, 0.6f),
                endPos = center + new Vector3(0.6f, 0.70f, 2.2f),
                startRotEuler = new Vector3(10f, 26f, 1.5f),
                endRotEuler = new Vector3(14f, 30f, 0.8f),
                startFov = 46f,
                endFov = 40f,
                startRoll = 1.5f,
                endRoll = 0.8f,
                shakeBase = 0.02f
            };

            // Shot 6: MeleeZeroGClash (10.4s - 13.0s, Ref F43 - F52)
            // Dynamic melee combat collision, tackle into regolith and soaring zero-g slice
            _shots[MoonBattleCutscenePhase.MeleeZeroGClash] = new CinematicCameraShot
            {
                shotName = "ZeroG_Melee_Tackle_Clash",
                startPos = center + new Vector3(-2.8f, 1.75f, -0.6f),
                endPos = center + new Vector3(-1.8f, 1.65f, 0.8f),
                startRotEuler = new Vector3(-4f, 42f, -3.5f),
                endRotEuler = new Vector3(-2f, 48f, -1.5f),
                startFov = 42f,
                endFov = 38f,
                startRoll = -3.5f,
                endRoll = -1.5f,
                shakeBase = 0.035f
            };
        }

        public void StartCutscene()
        {
            currentTime = 0f;
            isPlaying = true;
            Time.timeScale = 1.0f;

            LoadAudioTrack();
            HideSceneGameplayHUD();

            if (musicSource != null && combatSoundtrack != null)
            {
                musicSource.clip = combatSoundtrack;
                musicSource.time = 0f;
                musicSource.loop = loopAfterFinish;
                musicSource.Play();
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space)) isPlaying = !isPlaying;
            if (Input.GetKeyDown(KeyCode.R)) StartCutscene();
            if (Input.GetKeyDown(KeyCode.Alpha1)) ScrubTo(0.0f);   // BootSprintDust
            if (Input.GetKeyDown(KeyCode.Alpha2)) ScrubTo(1.6f);   // LowGravitySpaceFleet
            if (Input.GetKeyDown(KeyCode.Alpha3)) ScrubTo(3.4f);   // BattlefieldChaos
            if (Input.GetKeyDown(KeyCode.Alpha4)) ScrubTo(5.6f);   // AlitaHeroSlide
            if (Input.GetKeyDown(KeyCode.Alpha5)) ScrubTo(7.4f);   // CommanderAssault
            if (Input.GetKeyDown(KeyCode.Alpha6)) ScrubTo(9.4f);   // AlitaChargeSpring
            if (Input.GetKeyDown(KeyCode.Alpha7)) ScrubTo(10.6f);  // MeleeZeroGClash

            if (!isPlaying) return;

            currentTime += Time.deltaTime;
            if (currentTime >= totalDuration)
            {
                if (loopAfterFinish)
                {
                    StartCutscene();
                }
                else
                {
                    isPlaying = false;
                    Time.timeScale = 1.0f;
                }
                return;
            }

            UpdateCutscenePhase();
            UpdateActorPacing();
            UpdateSpaceDogfights();
            UpdateCameraChoreography();
            UpdateVFXAndAudio();
        }

        public void ScrubTo(float time)
        {
            if (_shots == null || _shots.Count == 0) SetupShots();
            currentTime = Mathf.Clamp(time, 0f, totalDuration);
            UpdateCutscenePhase();
            UpdateActorPacing();
            UpdateSpaceDogfights();
            UpdateCameraChoreography();
            UpdateVFXAndAudio();

            if (musicSource != null && combatSoundtrack != null && Application.isPlaying)
            {
                musicSource.time = Mathf.Clamp(currentTime, 0f, combatSoundtrack.length - 0.05f);
            }
        }

        private void UpdateCutscenePhase()
        {
            if (currentTime < 2.5f)
            {
                currentPhase = MoonBattleCutscenePhase.BootSprintDust;
            }
            else if (currentTime < 3.5f)
            {
                currentPhase = MoonBattleCutscenePhase.LowGravitySpaceFleet;
            }
            else if (currentTime < 5.8f)
            {
                currentPhase = MoonBattleCutscenePhase.BattlefieldChaos;
            }
            else if (currentTime < 7.4f)
            {
                currentPhase = MoonBattleCutscenePhase.AlitaHeroSlide;
            }
            else if (currentTime < 9.4f)
            {
                currentPhase = MoonBattleCutscenePhase.CommanderAssault;
            }
            else if (currentTime < 10.4f)
            {
                currentPhase = MoonBattleCutscenePhase.AlitaChargeSpring;
            }
            else
            {
                currentPhase = MoonBattleCutscenePhase.MeleeZeroGClash;
            }
        }

        private void UpdateCameraChoreography()
        {
            if (cinematicCamera == null) return;
            if (!_shots.ContainsKey(currentPhase)) return;

            CinematicCameraShot shot = _shots[currentPhase];
            float phaseStart = GetPhaseStartTime(currentPhase);
            float phaseDuration = GetPhaseDuration(currentPhase);
            float t = Mathf.Clamp01((currentTime - phaseStart) / phaseDuration);

            float easeT = Mathf.SmoothStep(0f, 1f, t);

            Vector3 center = new Vector3(172f, 13f, 10f);
            Vector3 targetPos = Vector3.Lerp(shot.startPos, shot.endPos, easeT);
            Quaternion targetRot;

            float currentRoll = Mathf.Lerp(shot.startRoll, shot.endRoll, easeT);

            // Dynamic camera orientation for each authentic phase
            switch (currentPhase)
            {
                case MoonBattleCutscenePhase.BootSprintDust:
                {
                    // Track tightly on sprinting boots at regolith level
                    Vector3 lookBoots = center + new Vector3(-0.6f, 0.08f, -4.8f);
                    targetRot = Quaternion.LookRotation((lookBoots - targetPos).normalized, Vector3.up);
                    break;
                }
                case MoonBattleCutscenePhase.LowGravitySpaceFleet:
                {
                    // Tilt upward following low-g flight, skyward into starfighters and Earth
                    Vector3 lookSky = center + new Vector3(1.5f, 5.5f, 5.5f);
                    targetRot = Quaternion.LookRotation((lookSky - targetPos).normalized, Vector3.up);
                    break;
                }
                case MoonBattleCutscenePhase.BattlefieldChaos:
                {
                    // Wide sweep across crater battlefield and exploding ridge
                    Vector3 lookRidge = center + new Vector3(6.5f, 1.8f, 5.5f);
                    targetRot = Quaternion.LookRotation((lookRidge - targetPos).normalized, Vector3.up);
                    break;
                }
                case MoonBattleCutscenePhase.AlitaHeroSlide:
                {
                    // Framing Alita in 3-point landing with explosion behind her
                    Vector3 heroPos = (alitaHero != null) ? alitaHero.transform.position + Vector3.up * 0.35f : (center + Vector3.up * 0.35f);
                    targetRot = Quaternion.LookRotation((heroPos - targetPos).normalized, Vector3.up);
                    break;
                }
                case MoonBattleCutscenePhase.CommanderAssault:
                {
                    // Profile tracking on Gelda advancing
                    Vector3 geldaPos = (commanderGelda != null) ? commanderGelda.transform.position + Vector3.up * 1.15f : (center + Vector3.up * 1.15f);
                    targetRot = Quaternion.LookRotation((geldaPos - targetPos).normalized, Vector3.up);
                    break;
                }
                case MoonBattleCutscenePhase.AlitaChargeSpring:
                {
                    // Snap behind Alita rocketing forward
                    Vector3 heroAhead = (alitaHero != null) ? alitaHero.transform.position + alitaHero.transform.forward * 2.0f + Vector3.up * 0.8f : (center + Vector3.up * 0.8f);
                    targetRot = Quaternion.LookRotation((heroAhead - targetPos).normalized, Vector3.up);
                    break;
                }
                case MoonBattleCutscenePhase.MeleeZeroGClash:
                {
                    // Close-quarters melee fight collision and soaring kick
                    Vector3 fightCenter = center + new Vector3(1.4f, 1.1f, 3.2f);
                    targetRot = Quaternion.LookRotation((fightCenter - targetPos).normalized, Vector3.up);
                    break;
                }
                default:
                    targetRot = Quaternion.Slerp(Quaternion.Euler(shot.startRotEuler), Quaternion.Euler(shot.endRotEuler), easeT);
                    break;
            }

            // Apply Dutch tilt (roll)
            targetRot = targetRot * Quaternion.Euler(0f, 0f, currentRoll);

            float targetFov = Mathf.Lerp(shot.startFov, shot.endFov, easeT);

            // Smooth Steadicam breathing and micro-tremor
            if (cameraShakeIntensity > 0.0005f)
            {
                float timeFactor = Time.time;
                Vector3 handheldBreathing = new Vector3(
                    (Mathf.PerlinNoise(timeFactor * 1.4f, 0.5f) - 0.5f) * 0.05f,
                    (Mathf.PerlinNoise(0.5f, timeFactor * 1.2f) - 0.5f) * 0.03f,
                    (Mathf.PerlinNoise(timeFactor * 1.0f, timeFactor * 1.0f) - 0.5f) * 0.02f
                ) * cameraShakeIntensity;

                Vector3 combatTremor = new Vector3(
                    (Mathf.PerlinNoise(timeFactor * 4.5f, 1.2f) - 0.5f) * 0.02f,
                    (Mathf.PerlinNoise(1.2f, timeFactor * 4.5f) - 0.5f) * 0.02f,
                    0f
                ) * cameraShakeIntensity;

                targetPos += (handheldBreathing + combatTremor);

                float rotPitchDrift = (Mathf.PerlinNoise(timeFactor * 1.6f, 2.5f) - 0.5f) * cameraShakeIntensity * 1.0f;
                float rotYawDrift = (Mathf.PerlinNoise(2.5f, timeFactor * 1.4f) - 0.5f) * cameraShakeIntensity * 1.0f;
                targetRot *= Quaternion.Euler(rotPitchDrift, rotYawDrift, 0f);
            }

            cinematicCamera.transform.position = targetPos;
            cinematicCamera.transform.rotation = targetRot;
            cinematicCamera.fieldOfView = targetFov;

            if (earthTransform != null)
            {
                earthTransform.rotation = Quaternion.LookRotation((cinematicCamera.transform.position - earthTransform.position).normalized, Vector3.up);
            }
        }

        private void UpdateActorPacing()
        {
            Vector3 center = new Vector3(172f, 13f, 10f);
            Vector3 enemyRidgeCenter = center + new Vector3(16.5f, 0.35f, 10.5f);

            if (_squadBaseOffsets == null || _squadBaseOffsets.Length == 0) SetupFormations();

            // ── Phase 0: Low-Angle Boot Sprint & Impact (0.0s - 2.5s) ──────────────────
            if (currentPhase == MoonBattleCutscenePhase.BootSprintDust)
            {
                if (alitaHero != null)
                {
                    float sprintCycle = currentTime * 8.5f;
                    float legBob = Mathf.Abs(Mathf.Sin(sprintCycle)) * 0.22f;
                    alitaHero.transform.position = center + new Vector3(-0.6f, legBob, -5.2f + currentTime * 2.2f);
                    alitaHero.transform.rotation = Quaternion.Euler(-8f, 28f, 0f);
                }

                for (int s = 0; s < squadTroops.Count && s < _squadBaseOffsets.Length; s++)
                {
                    if (squadTroops[s] == null) continue;
                    float runPhase = currentTime * 8.0f + s * 0.7f;
                    float lowGBob = Mathf.Abs(Mathf.Sin(runPhase)) * 0.25f;
                    Vector3 squadPos = center + _squadBaseOffsets[s] + new Vector3(0f, lowGBob, currentTime * 2.0f);
                    squadTroops[s].transform.position = squadPos;
                    squadTroops[s].transform.rotation = Quaternion.Euler(-8f, 25f + (s % 4) * 3f, 0f);
                }
            }

            // ── Phase 1: Low-Gravity Leap into the Sky (2.5s - 3.5s) ───────────────────
            if (currentPhase == MoonBattleCutscenePhase.LowGravitySpaceFleet)
            {
                float leapProgress = (currentTime - 2.5f) / 1.0f;
                float arcY = Mathf.Sin(leapProgress * Mathf.PI) * 3.2f;

                if (alitaHero != null)
                {
                    alitaHero.transform.position = center + new Vector3(-0.2f + leapProgress * 1.5f, arcY, -1.5f + leapProgress * 2.5f);
                    alitaHero.transform.rotation = Quaternion.Euler(-14f, 38f, 4f);
                }

                for (int s = 0; s < squadTroops.Count && s < _squadBaseOffsets.Length; s++)
                {
                    if (squadTroops[s] == null) continue;
                    float sProgress = Mathf.Clamp01(leapProgress + (s % 3) * 0.1f);
                    float sArc = Mathf.Sin(sProgress * Mathf.PI) * (2.2f + (s % 4) * 0.3f);
                    Vector3 sPos = center + _squadBaseOffsets[s] + new Vector3(sProgress * 1.8f, sArc, sProgress * 2.8f);
                    squadTroops[s].transform.position = sPos;
                    squadTroops[s].transform.rotation = Quaternion.Euler(-12f, 32f + (s % 5) * 3f, 0f);
                }
            }

            // ── Phase 2: Wide Battlefield Chaos & Concussion (3.5s - 5.8s) ─────────────
            if (currentPhase == MoonBattleCutscenePhase.BattlefieldChaos)
            {
                float chaosT = (currentTime - 3.5f) / 2.3f;
                Vector3 advanceVector = new Vector3(0.4f, 0f, 0.8f) * (chaosT * 3.5f);

                if (alitaHero != null)
                {
                    float alitaBob = Mathf.Abs(Mathf.Sin(currentTime * 6.5f)) * 0.25f;
                    alitaHero.transform.position = center + advanceVector + new Vector3(0f, alitaBob, 0.5f);
                    alitaHero.transform.rotation = Quaternion.Euler(-6f, 32f, 0f);
                }

                if (commanderGelda != null)
                {
                    float geldaBob = Mathf.Abs(Mathf.Sin(currentTime * 6.0f + 1.2f)) * 0.22f;
                    commanderGelda.transform.position = center + advanceVector + new Vector3(-2.0f, geldaBob + 0.1f, -0.2f);
                    commanderGelda.transform.rotation = Quaternion.Euler(-5f, 34f, 0f);
                }

                for (int s = 0; s < squadTroops.Count && s < _squadBaseOffsets.Length; s++)
                {
                    if (squadTroops[s] == null) continue;
                    float runPhase = currentTime * 5.8f + s * 0.85f;
                    float lowGBob = Mathf.Abs(Mathf.Sin(runPhase)) * 0.30f;
                    Vector3 squadOffset = _squadBaseOffsets[s] + advanceVector;
                    squadOffset.y += lowGBob;

                    squadTroops[s].transform.position = center + squadOffset;
                    float recoil = (Mathf.Sin(currentTime * 20f + s * 2.2f) > 0.7f) ? -5f : 0f;
                    squadTroops[s].transform.rotation = Quaternion.Euler(recoil, 28f + (s % 5) * 3f, 0f);
                }
            }

            // ── Phase 3: Alita 3-Point Hero Slide with Blast Behind (5.8s - 7.4s) ──────
            if (currentPhase == MoonBattleCutscenePhase.AlitaHeroSlide)
            {
                float slideT = Mathf.Clamp01((currentTime - 5.8f) / 1.6f);
                float slideEase = 1.0f - Mathf.Pow(1.0f - slideT, 2.5f);

                if (alitaHero != null)
                {
                    // Slides to a stop facing 3/4 towards camera, turning head back over shoulder
                    Vector3 slidePos = center + new Vector3(0.5f, 0.05f, 0.8f + slideEase * 1.2f);
                    alitaHero.transform.position = slidePos;
                    alitaHero.transform.rotation = Quaternion.Euler(0f, 60f, 0f);
                }

                if (commanderGelda != null)
                {
                    commanderGelda.transform.position = center + new Vector3(-2.5f, 0.12f, 0.8f);
                    commanderGelda.transform.rotation = Quaternion.Euler(-4f, 35f, 0f);
                }

                for (int s = 0; s < squadTroops.Count && s < _squadBaseOffsets.Length; s++)
                {
                    if (squadTroops[s] == null) continue;
                    Vector3 coverPos = (s % 2 == 0) ? 
                        new Vector3(-8.5f - (s / 2) * 2.2f, 0.1f, -3.0f - (s / 2) * 1.2f) : 
                        new Vector3(7.5f + (s / 2) * 2.2f, 0.1f, -2.5f - (s / 2) * 1.2f);
                    squadTroops[s].transform.position = center + coverPos;
                    float burst = (Mathf.Sin(currentTime * 18f + s * 2.0f) > 0.72f) ? -4f : 0f;
                    squadTroops[s].transform.rotation = Quaternion.Euler(burst, 28f + (s % 4) * 3f, 0f);
                }
            }

            // ── Phase 4: Commander Gelda Assault Burst (7.4s - 9.4s) ───────────────────
            if (currentPhase == MoonBattleCutscenePhase.CommanderAssault)
            {
                float geldaT = (currentTime - 7.4f) / 2.0f;
                if (commanderGelda != null)
                {
                    float geldaRecoil = (Mathf.Sin(currentTime * 24f) > 0.7f) ? -6f : 0f;
                    commanderGelda.transform.position = center + new Vector3(-1.8f, 0.12f, 1.2f + geldaT * 1.6f);
                    commanderGelda.transform.rotation = Quaternion.Euler(geldaRecoil, 45f, 0f);
                }

                if (alitaHero != null)
                {
                    alitaHero.transform.position = center + new Vector3(0.5f, 0.05f, 2.0f);
                    alitaHero.transform.rotation = Quaternion.Euler(0f, 35f, 0f);
                }

                for (int s = 0; s < squadTroops.Count && s < _squadBaseOffsets.Length; s++)
                {
                    if (squadTroops[s] == null) continue;
                    Vector3 linePos = center + _squadBaseOffsets[s] + new Vector3(0f, 0f, 2.5f + geldaT * 1.5f);
                    squadTroops[s].transform.position = linePos;
                    float weaponBurst = (Mathf.Sin(currentTime * 20f + s * 2.5f) > 0.7f) ? -5f : 0f;
                    squadTroops[s].transform.rotation = Quaternion.Euler(weaponBurst, 32f + (s % 4) * 3f, 0f);
                }
            }

            // ── Phase 5: Alita Charge Spring from 3-Point Stance (9.4s - 10.4s) ─────────
            if (currentPhase == MoonBattleCutscenePhase.AlitaChargeSpring)
            {
                float springT = (currentTime - 9.4f) / 1.0f;
                float springY = Mathf.Sin(springT * Mathf.PI * 0.5f) * 0.45f;

                if (alitaHero != null)
                {
                    alitaHero.transform.position = center + new Vector3(0.5f + springT * 0.5f, springY, 2.0f + springT * 3.5f);
                    alitaHero.transform.rotation = Quaternion.Euler(-15f, 28f, 0f);
                }

                if (commanderGelda != null)
                {
                    commanderGelda.transform.position = center + new Vector3(-1.8f, 0.12f, 2.8f + springT * 1.2f);
                    commanderGelda.transform.rotation = Quaternion.Euler(-5f, 35f, 0f);
                }
            }

            // ── Phase 6: Melee Zero-G Clash, Tackle & Soaring Kick (10.4s - 13.0s) ─────
            if (currentPhase == MoonBattleCutscenePhase.MeleeZeroGClash)
            {
                float meleeT = (currentTime - 10.4f) / 2.6f;

                // Alita soaring across the background in zero-g slice/flying kick
                if (alitaHero != null)
                {
                    float flyT = Mathf.Clamp01((currentTime - 11.2f) / 1.8f);
                    float flyArc = Mathf.Sin(flyT * Mathf.PI) * 1.8f + 0.6f;
                    alitaHero.transform.position = center + new Vector3(0.2f + flyT * 2.8f, flyArc, 1.6f + flyT * 3.2f);
                    alitaHero.transform.rotation = Quaternion.Euler(-24f, 48f, 32f);
                }

                // Soldier tackling enemy into lunar regolith in foreground
                float tackleT = Mathf.Clamp01(meleeT * 1.6f);
                float tackleHeight = Mathf.Max(0.08f, Mathf.Sin(tackleT * Mathf.PI * 0.7f) * 1.4f * (1f - tackleT * 0.8f));

                if (squadTroops.Count > 0 && squadTroops[0] != null)
                {
                    squadTroops[0].transform.position = center + new Vector3(1.0f + tackleT * 0.5f, tackleHeight + 0.08f, 2.4f + tackleT * 0.7f);
                    squadTroops[0].transform.rotation = Quaternion.Euler(-20f + tackleT * 32f, 45f, tackleT * 22f);
                }

                if (enemyTroops.Count > 0 && enemyTroops[0] != null)
                {
                    enemyTroops[0].transform.position = center + new Vector3(1.3f + tackleT * 0.4f, tackleHeight * 0.8f + 0.05f, 2.7f + tackleT * 0.6f);
                    enemyTroops[0].transform.rotation = Quaternion.Euler(18f - tackleT * 38f, 225f, -tackleT * 28f);
                }
            }

            // ── Hostile Defense Battalion ────────────────────────────────────────────
            for (int e = 0; e < enemyTroops.Count && e < _enemyBaseOffsets.Length; e++)
            {
                if (enemyTroops[e] == null) continue;
                if (currentPhase == MoonBattleCutscenePhase.MeleeZeroGClash && e == 0) continue; // hand-to-hand combatant

                Vector3 ePos = enemyRidgeCenter + _enemyBaseOffsets[e];
                enemyTroops[e].transform.position = ePos;
                float enemyBurst = (Mathf.Sin(currentTime * 18f + e * 2.8f) > 0.68f) ? -5f : 0f;
                enemyTroops[e].transform.rotation = Quaternion.Euler(enemyBurst, 185f + (e * 4f), 0f);
            }
        }

        private void UpdateSpaceDogfights()
        {
            Vector3 center = new Vector3(172f, 13f, 10f);

            // 1. Starfighter Squadron Alpha
            for (int i = 0; i < starfighters.Count; i++)
            {
                if (starfighters[i] == null) continue;
                float shipCycle = (currentTime * 0.10f) % 1.0f;
                Vector3 wingOffset = (i == 0) ? Vector3.zero :
                    ((i == 1) ? new Vector3(-12f, -2.0f, -14f) : new Vector3(12f, 2.0f, -16f));
                float x = Mathf.Lerp(185f, 290f, shipCycle);
                float y = 52f + Mathf.Sin(shipCycle * Mathf.PI * 2f) * 4f;
                float z = Mathf.Lerp(45f, 125f, shipCycle);

                starfighters[i].transform.position = new Vector3(x, y, z) + wingOffset;
                Vector3 forwardDir = new Vector3(105f, 4f, 80f).normalized;
                starfighters[i].transform.rotation = Quaternion.LookRotation(forwardDir, Vector3.up) * Quaternion.Euler(0f, 0f, Mathf.Sin(currentTime * 3f + i) * 16f);
            }

            // 2. Enemy Interceptors
            for (int e = 0; e < enemyInterceptors.Count; e++)
            {
                if (enemyInterceptors[e] == null) continue;
                float chaseCycle = ((currentTime - 0.3f) * 0.10f) % 1.0f;
                Vector3 chaseOffset = (e == 0) ? new Vector3(-8f, -1.5f, -28f) : new Vector3(8f, 1.5f, -32f);
                float x = Mathf.Lerp(185f, 290f, chaseCycle);
                float y = 51f + Mathf.Sin(chaseCycle * Mathf.PI * 2f) * 4f;
                float z = Mathf.Lerp(45f, 125f, chaseCycle);

                enemyInterceptors[e].transform.position = new Vector3(x, y, z) + chaseOffset;
                Vector3 forwardDir = new Vector3(105f, 4f, 80f).normalized;
                enemyInterceptors[e].transform.rotation = Quaternion.LookRotation(forwardDir, Vector3.up) * Quaternion.Euler(0f, 0f, -20f + Mathf.Sin(currentTime * 3.5f + e) * 18f);
            }

            // 3. Assault Dropship in upper lunar sky
            if (assaultGunship != null)
            {
                float runT = currentTime / totalDuration;
                Vector3 startRun = center + new Vector3(-45f, 42f, -15f);
                Vector3 endRun = center + new Vector3(55f, 38f, 50f);
                assaultGunship.transform.position = Vector3.Lerp(startRun, endRun, runT);
                Vector3 heading = (endRun - startRun).normalized;
                assaultGunship.transform.rotation = Quaternion.LookRotation(heading, Vector3.up) * Quaternion.Euler(8f, 0f, 25f);
            }

            // 4. Space Dogfight Laser Beams
            for (int l = 0; l < dogfightLaserBeams.Count; l++)
            {
                var lr = dogfightLaserBeams[l];
                if (lr == null) continue;
                bool isCyan = (l % 2 == 0);
                float laserCycle = (currentTime * 6.5f + l * 0.35f) % 1.0f;
                Vector3 origin, target;
                if (isCyan && starfighters.Count > 0 && starfighters[0] != null)
                {
                    origin = starfighters[0].transform.position + new Vector3(0f, -0.8f, 3.2f);
                    target = origin + starfighters[0].transform.forward * 65f;
                }
                else if (enemyInterceptors.Count > 0 && enemyInterceptors[0] != null)
                {
                    origin = enemyInterceptors[0].transform.position + new Vector3(0f, 0.4f, 3.0f);
                    target = origin + enemyInterceptors[0].transform.forward * 58f;
                }
                else
                {
                    origin = new Vector3(205f, 55f, 60f) + new Vector3(l * 8f, l * 2.5f, l * 6f);
                    target = origin + new Vector3(48f, 4f, 30f);
                }

                Vector3 curStart = Vector3.Lerp(origin, target, Mathf.Max(0f, laserCycle - 0.28f));
                Vector3 curEnd = Vector3.Lerp(origin, target, laserCycle);
                lr.SetPosition(0, curStart);
                lr.SetPosition(1, curEnd);
            }

            // 5. Planetary Cannon
            if (planetaryCannon != null)
            {
                float kick = Mathf.Pow(Mathf.Sin(currentTime * 1.8f), 12f);
                planetaryCannon.transform.localRotation = Quaternion.Euler(-35f - kick * 8f, 28f, 0f);
            }
        }

        private void UpdateVFXAndAudio()
        {
            switch (currentPhase)
            {
                case MoonBattleCutscenePhase.BootSprintDust:
                    // Heavy ground stomp vibration at 1.4s
                    cameraShakeIntensity = (currentTime >= 1.35f && currentTime <= 1.55f) ? 0.04f : 0.015f;
                    break;
                case MoonBattleCutscenePhase.LowGravitySpaceFleet:
                    cameraShakeIntensity = 0.01f;
                    break;
                case MoonBattleCutscenePhase.BattlefieldChaos:
                    // Massive explosion detonation at 4.6s - 5.0s
                    cameraShakeIntensity = (currentTime >= 4.5f && currentTime <= 5.1f) ? 0.055f : 0.03f;
                    break;
                case MoonBattleCutscenePhase.AlitaHeroSlide:
                    // Concussion blast directly behind her at 5.8s - 6.3s
                    cameraShakeIntensity = (currentTime >= 5.8f && currentTime <= 6.4f) ? 0.045f : 0.02f;
                    break;
                case MoonBattleCutscenePhase.CommanderAssault:
                    cameraShakeIntensity = 0.015f;
                    break;
                case MoonBattleCutscenePhase.AlitaChargeSpring:
                    cameraShakeIntensity = 0.02f;
                    break;
                case MoonBattleCutscenePhase.MeleeZeroGClash:
                    // Hand-to-hand impact rumble
                    cameraShakeIntensity = 0.035f;
                    break;
                default:
                    cameraShakeIntensity = 0.015f;
                    break;
            }
        }

        private float GetPhaseStartTime(MoonBattleCutscenePhase phase)
        {
            switch (phase)
            {
                case MoonBattleCutscenePhase.BootSprintDust: return 0.0f;
                case MoonBattleCutscenePhase.LowGravitySpaceFleet: return 2.5f;
                case MoonBattleCutscenePhase.BattlefieldChaos: return 3.5f;
                case MoonBattleCutscenePhase.AlitaHeroSlide: return 5.8f;
                case MoonBattleCutscenePhase.CommanderAssault: return 7.4f;
                case MoonBattleCutscenePhase.AlitaChargeSpring: return 9.4f;
                case MoonBattleCutscenePhase.MeleeZeroGClash: return 10.4f;
                default: return 0f;
            }
        }

        private float GetPhaseDuration(MoonBattleCutscenePhase phase)
        {
            switch (phase)
            {
                case MoonBattleCutscenePhase.BootSprintDust: return 2.5f;
                case MoonBattleCutscenePhase.LowGravitySpaceFleet: return 1.0f;
                case MoonBattleCutscenePhase.BattlefieldChaos: return 2.3f;
                case MoonBattleCutscenePhase.AlitaHeroSlide: return 1.6f;
                case MoonBattleCutscenePhase.CommanderAssault: return 2.0f;
                case MoonBattleCutscenePhase.AlitaChargeSpring: return 1.0f;
                case MoonBattleCutscenePhase.MeleeZeroGClash: return 2.6f;
                default: return 1.5f;
            }
        }

        private void OnGUI()
        {
            float screenW = Screen.width;
            float screenH = Screen.height;

            // Pure cinematic 2.39:1 anamorphic letterbox bars (no HUD overlays)
            float targetAspect = 2.39f;
            float currentAspect = screenW / screenH;
            float barH = 0f;

            if (currentAspect <= targetAspect)
            {
                float cinemaHeight = screenW / targetAspect;
                barH = (screenH - cinemaHeight) * 0.5f;
            }

            if (barH > 0f)
            {
                if (_blackPixel == null) SetupStyles();
                GUI.DrawTexture(new Rect(0, 0, screenW, barH), _blackPixel);
                GUI.DrawTexture(new Rect(0, screenH - barH, screenW, barH), _blackPixel);
            }

            // HUD is completely hidden during cutscenes per user request ("remove hub in cutscenes")
            // No timecode, no labels, no speaker/subtitles box by default!
            if (!showHUD) return;
        }
    }
}
