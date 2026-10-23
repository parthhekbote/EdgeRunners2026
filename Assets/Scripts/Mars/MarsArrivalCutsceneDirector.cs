using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using PolygonMech;
using InsaneGun.Combat;
using SyntyCombined.Cutscene;
using SyntyCombined.VFX;
using SyntyCombined;
using SyntyCombined.BlackMarket;
using Alita.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Cyberpunk.Mars
{
    /// <summary>
    /// AAA-Grade Cinematic Blockbuster Arrival Cutscene Director for Mars (Scene_Mars_FinalBattle).
    /// Orchestrates the full 8-shot cinematic sequence:
    /// - Shot 1: Orbital Hyperspace Breach & Atmospheric Entry over Cydonia Basin with celestial Phobos
    ///   and retro GotG-style title card splash ("MARS // CYDONIA BASIN // SECTOR 00").
    /// - Shot 2: Low-altitude excavation canyon & ancient monolith radar dish flyby with camera shake,
    ///   roaring starship pass, and Alita cockpit comms radio transmission.
    /// - Shot 3: Retro-thruster vertical touchdown on the Martian regolith; Alita disembarks,
    ///   draws her Damascus blade, and discovers the legendary Ancient URM Antimatter Core!
    /// - Shot 4: Core Retrieval & Supercharge Surge: The ancient core glides into Alita's chest,
    ///   fusing with her Berserker heart in a blinding flash of plasma energy, lightning arcs, and blade ignition!
    /// - Shot 5: Star Wars Imperial Star Destroyer Arrival: Two massive capital dreadnoughts breach the clouds,
    ///   casting colossal shadows and sweeping high-intensity searchlights across the crater!
    /// - Shot 6: Ventral Drop & Pilot Army Surround: The Colossus Titan Mech and 12 Mech Pilots land in a perimeter,
    ///   aiming Arc Rifles with crossing laser sights and opening fire!
    /// - Shot 7: The One-Shot Obliteration: Alita gathers her supercharged power, unleashes a 360° supersonic
    ///   Antimatter Berserker Cataclysm shockwave—ONE-SHOTTING the entire pilot army and launching them flying everywhere
    ///   across the Martian dunes in high-arc Euphoria physics ragdolls!
    /// - Shot 8: Mech Boss Solo Challenge: Mech roars in mechanical rage, hydraulic steam vents blast, red visors flare,
    ///   and it hoists its rocket war hammer with a seismic ground impact.
    /// - Seamless transition into gameplay third-person camera (SmoothOrbitCamera), unlocking combat controls.
    /// - Skip functionality on [Space] or [Escape].
    /// </summary>
    public class MarsArrivalCutsceneDirector : MonoBehaviour
    {
        public static MarsArrivalCutsceneDirector Instance { get; private set; }

        public static bool ForcePlayCutsceneOnSceneLoad = true;

        [Header("Director Controls")]
        public bool playOnStart = true;
        public bool isPlaying { get; private set; } = false;

        public enum CutscenePhase
        {
            None,
            ArrivalLanding,
            PlayingMission,
            InvasionAndObliteration,
            BossBattle
        }
        public CutscenePhase currentPhase = CutscenePhase.None;

        [Header("Actor References")]
        public GameObject alitaHero;
        public GameObject bossMech;
        public GameObject starship;

        [Header("Invasion & Core Systems")]
        public MarsAntimatterCorePedestal corePedestal;
        public MarsStarDestroyerFleet starDestroyerFleet;
        public MarsInvasionArmyManager armyManager;
        public AlitaSuperchargeAura superchargeAura;

        [Header("Cameras")]
        public Camera mainCamera;
        private SmoothOrbitCamera _orbitCamera;

        [Header("Audio Tracks & SFX")]
        public AudioClip celestialDroneAudio;
        public AudioClip starshipFlybyAudio;
        public AudioClip bladeUnsheathAudio;
        public AudioClip combatMusic;
        public AudioClip commsChirpAudio;

        private AudioSource _musicSource;
        private AudioSource _ambientSource;
        private AudioSource _sfxSource;
        private AudioSource _voiceSource;

        [Header("Visual Effects & Lights")]
        public Light starshipThrusterLight;
        public Light bossEyeVisorLight;
        public ParticleSystem redDustTouchdownFX;
        public ParticleSystem hydraulicSteamFX;

        [Header("Martian Dust Storm")]
        public MartianDustStormSystem dustStormSystem;

        // Internal State & Animations
        private Coroutine _cutsceneCoroutine;
        private InsaneGunSwordController _alitaController;
        private CharacterController _alitaCharController;
        private Animator _alitaAnimator;
        private HammerMechAI _bossAI;
        private Animator _bossAnimator;
        private BossHealthBar _bossHealthBar;
        private MarsFinalBattleDirector _marsDirector;

        // Camera Shake State
        private float _shakeTrauma = 0f;
        private const float ShakeDecay = 1.6f;
        private const float MaxShakeRoll = 4.2f;
        private const float MaxShakePitch = 3.0f;
        private const float MaxShakePosOffset = 0.35f;

        // Comms HUD Transmission State
        private bool _isShowingComms = false;
        private string _commsSpeaker = "";
        private string _commsRole = "";
        private string _commsText = "";
        private float _commsTimer = 0f;
        private Texture2D _commsBackdropTex;
        private GUIStyle _commsHeaderStyle;
        private GUIStyle _commsBodyStyle;
        private GUIStyle _tacticalAlertStyle;
        private bool _stylesInitialized = false;

        // Tactical Boss Alert State
        private string _tacticalAlertText = "";
        private float _tacticalAlertTimer = 0f;

        // Scene Ground Positions
        public Vector3 ShipTouchdownPos = new Vector3(0f, 2.20f, 58.00f);
        public Quaternion ShipTouchdownRot = Quaternion.Euler(0f, 0f, 0f);

        public Vector3 CorePedestalPos = new Vector3(0f, 0.40f, 103.00f);

        public Vector3 AlitaCombatStartPos = new Vector3(0f, 1.00f, 75.00f);
        public Quaternion AlitaCombatStartRot = Quaternion.Euler(0f, 0f, 0f);

        public Vector3 BossCombatStartPos = new Vector3(0f, 1.00f, 96.00f);
        public Quaternion BossCombatStartRot = Quaternion.Euler(0f, 180f, 0f);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnsureComponentsAndActors();
            SetupAudioSources();
        }

        private void Start()
        {
            if (playOnStart || ForcePlayCutsceneOnSceneLoad)
            {
                ForcePlayCutsceneOnSceneLoad = false;
                StartArrivalCutscene();
            }
        }

        private void Update()
        {
            if (isPlaying)
            {
                // Skip on Space or Escape
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape))
                {
                    SkipCutscene();
                }

                // Trauma dissipation
                if (_shakeTrauma > 0f)
                {
                    _shakeTrauma = Mathf.Max(0f, _shakeTrauma - Time.deltaTime * ShakeDecay);
                }

                // Comms timer
                if (_commsTimer > 0f)
                {
                    _commsTimer -= Time.deltaTime;
                    if (_commsTimer <= 0f) _isShowingComms = false;
                }

                // Tactical alert timer
                if (_tacticalAlertTimer > 0f)
                {
                    _tacticalAlertTimer -= Time.deltaTime;
                    if (_tacticalAlertTimer <= 0f) _tacticalAlertText = "";
                }
            }
        }

        public void EnsureComponentsAndActors()
        {
            if (alitaHero == null) alitaHero = GameObject.Find("Alita") ?? GameObject.FindGameObjectWithTag("Player");
            if (bossMech == null) bossMech = GameObject.Find("PF_HammerMechBoss");
            if (starship == null) starship = GameObject.Find("Alita_Martian_Starship") ?? GameObject.Find("Starship");
            if (mainCamera == null)
            {
                var camObj = GameObject.FindGameObjectWithTag("MainCamera");
                if (camObj != null) mainCamera = camObj.GetComponent<Camera>();
            }

            if (starship != null)
            {
                ShipTouchdownPos = starship.transform.position;
                ShipTouchdownRot = starship.transform.rotation;
            }

            if (alitaHero != null)
            {
                AlitaCombatStartPos = alitaHero.transform.position;
                AlitaCombatStartRot = alitaHero.transform.rotation;
            }

            if (bossMech != null)
            {
                BossCombatStartPos = bossMech.transform.position;
                BossCombatStartRot = bossMech.transform.rotation;
            }

            if (mainCamera != null)
            {
                _orbitCamera = mainCamera.GetComponent<SmoothOrbitCamera>();
            }

            if (alitaHero != null)
            {
                _alitaController = alitaHero.GetComponent<InsaneGunSwordController>();
                _alitaCharController = alitaHero.GetComponent<CharacterController>();
                _alitaAnimator = alitaHero.GetComponent<Animator>();
                if (superchargeAura == null) superchargeAura = alitaHero.GetComponent<AlitaSuperchargeAura>() ?? alitaHero.AddComponent<AlitaSuperchargeAura>();
            }

            if (bossMech != null)
            {
                _bossAI = bossMech.GetComponent<HammerMechAI>();
                _bossAnimator = bossMech.GetComponent<Animator>();
                _bossHealthBar = bossMech.GetComponent<BossHealthBar>();
            }

            if (_marsDirector == null)
            {
                _marsDirector = GetComponent<MarsFinalBattleDirector>() ?? FindFirstObjectByType<MarsFinalBattleDirector>();
            }

            if (corePedestal == null)
            {
                corePedestal = GetComponentInChildren<MarsAntimatterCorePedestal>() ?? FindFirstObjectByType<MarsAntimatterCorePedestal>();
                if (corePedestal == null)
                {
                    GameObject pGO = new GameObject("Mars_Antimatter_Core_Pedestal");
                    pGO.transform.position = CorePedestalPos;
                    corePedestal = pGO.AddComponent<MarsAntimatterCorePedestal>();
                }
            }

            if (corePedestal != null)
            {
                CorePedestalPos = corePedestal.transform.position;
            }

            if (starDestroyerFleet == null)
            {
                starDestroyerFleet = GetComponentInChildren<MarsStarDestroyerFleet>() ?? FindFirstObjectByType<MarsStarDestroyerFleet>();
                if (starDestroyerFleet == null)
                {
                    GameObject fGO = new GameObject("Mars_StarDestroyer_Fleet");
                    starDestroyerFleet = fGO.AddComponent<MarsStarDestroyerFleet>();
                }
            }

            if (armyManager == null)
            {
                armyManager = GetComponentInChildren<MarsInvasionArmyManager>() ?? FindFirstObjectByType<MarsInvasionArmyManager>();
                if (armyManager == null)
                {
                    GameObject aGO = new GameObject("Mars_Invasion_Army_Manager");
                    armyManager = aGO.AddComponent<MarsInvasionArmyManager>();
                }
            }

            // Ensure Starship exists in scene
            EnsureStarship();

            // Setup Mech Visor Light
            SetupBossVisorLight();

            // Ensure Martian Dust Storm System
            if (dustStormSystem == null)
            {
                dustStormSystem = GetComponent<MartianDustStormSystem>() ?? FindFirstObjectByType<MartianDustStormSystem>();
                if (dustStormSystem == null) dustStormSystem = gameObject.AddComponent<MartianDustStormSystem>();
            }

            // Load default audio clips if missing
            LoadDefaultAudioClips();
        }

        private void EnsureStarship()
        {
            if (starship == null)
            {
                starship = GameObject.Find("Alita_Martian_Starship") ?? GameObject.Find("Alita_Lost_Martian_Starship");
            }

            if (starship == null)
            {
#if UNITY_EDITOR
                var shipPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonSciFiSpace/Prefabs/Vehicles/SM_Ship_Stealth_01.prefab");
                if (shipPrefab != null)
                {
                    starship = (GameObject)PrefabUtility.InstantiatePrefab(shipPrefab);
                    starship.name = "Alita_Martian_Starship";
                }
#endif
            }

            if (starship != null)
            {
                var existingLight = starship.GetComponentInChildren<Light>();
                if (existingLight == null)
                {
                    GameObject lightGO = new GameObject("Thruster_Point_Light");
                    lightGO.transform.SetParent(starship.transform, false);
                    lightGO.transform.localPosition = new Vector3(0f, 0.4f, -7.5f);
                    starshipThrusterLight = lightGO.AddComponent<Light>();
                    starshipThrusterLight.type = LightType.Point;
                    starshipThrusterLight.color = new Color(0.1f, 0.85f, 1.0f);
                    starshipThrusterLight.range = 25f;
                    starshipThrusterLight.intensity = 3.5f;
                }
                else
                {
                    starshipThrusterLight = existingLight;
                }
            }
        }

        private void SetupBossVisorLight()
        {
            if (bossMech != null && bossEyeVisorLight == null)
            {
                var existing = bossMech.transform.Find("Boss_Visor_Light");
                if (existing != null)
                {
                    bossEyeVisorLight = existing.GetComponent<Light>();
                }
                else
                {
                    GameObject visorGO = new GameObject("Boss_Visor_Light");
                    visorGO.transform.SetParent(bossMech.transform, false);
                    visorGO.transform.localPosition = new Vector3(0f, 4.8f, 0.9f);
                    bossEyeVisorLight = visorGO.AddComponent<Light>();
                    bossEyeVisorLight.type = LightType.Point;
                    bossEyeVisorLight.color = new Color(1.0f, 0.12f, 0.05f);
                    bossEyeVisorLight.range = 10f;
                    bossEyeVisorLight.intensity = 0f;
                }
            }
        }

        private void LoadDefaultAudioClips()
        {
#if UNITY_EDITOR
            if (celestialDroneAudio == null)
                celestialDroneAudio = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Cutscenes/Astropulse_Celestial_Drone.mp3");
            if (celestialDroneAudio == null)
                celestialDroneAudio = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/Audio/Drones/sci-fi-drone-sound-effect.mp3");

            if (starshipFlybyAudio == null)
                starshipFlybyAudio = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Combat/SciFi_Starship_Flyby_Pass.mp3");

            if (bladeUnsheathAudio == null)
                bladeUnsheathAudio = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Combat/Sword_Slash_Light_01.wav");

            if (combatMusic == null)
                combatMusic = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Crowd/tatamusic-battle-warrior-fighting-drums-478210.mp3");

            if (commsChirpAudio == null)
                commsChirpAudio = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/NPC_Chatter/npc_helperbot_chirp_01.wav");
#endif
        }

        private void SetupAudioSources()
        {
            _musicSource = gameObject.AddComponent<AudioSource>();
            _musicSource.loop = true;
            _musicSource.playOnAwake = false;
            _musicSource.volume = 0.65f;

            _ambientSource = gameObject.AddComponent<AudioSource>();
            _ambientSource.loop = true;
            _ambientSource.playOnAwake = false;
            _ambientSource.volume = 0.50f;

            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.loop = false;
            _sfxSource.playOnAwake = false;

            _voiceSource = gameObject.AddComponent<AudioSource>();
            _voiceSource.loop = false;
            _voiceSource.playOnAwake = false;
            _voiceSource.volume = 0.90f;
        }

        public void StartArrivalCutscene()
        {
            if (isPlaying) return;
            isPlaying = true;

            EnsureComponentsAndActors();

            // 1. Suspend Alita gameplay controls
            if (_alitaController != null) _alitaController.enabled = false;
            if (_alitaCharController != null) _alitaCharController.enabled = false;

            // 2. Suspend Gameplay Orbit Camera
            if (_orbitCamera != null) _orbitCamera.enabled = false;

            // 3. Suspend Boss AI & Health Bar
            if (_bossAI != null) _bossAI.enabled = false;
            if (_bossHealthBar != null) _bossHealthBar.enabled = false;

            // 4. Boss Mech starts hidden / suspended in orbital drop bay
            if (bossMech != null)
            {
                bossMech.transform.position = BossCombatStartPos + Vector3.up * 80f;
                bossMech.transform.rotation = BossCombatStartRot;
                SetActorRenderersActive(bossMech, false);
            }
            if (bossEyeVisorLight != null) bossEyeVisorLight.intensity = 0f;

            // 5. Hide Alita initially (she is inside the landing starship)
            if (alitaHero != null)
            {
                alitaHero.transform.position = ShipTouchdownPos + Vector3.forward * 4f;
                alitaHero.transform.rotation = AlitaCombatStartRot;
                SetActorRenderersActive(alitaHero, false);
            }

            // 6. Stop any other music and play atmospheric celestial drone
            if (_ambientSource != null && celestialDroneAudio != null)
            {
                _ambientSource.clip = celestialDroneAudio;
                _ambientSource.Play();
            }

            // 7. Initialize Dust Storm
            if (dustStormSystem != null)
            {
                dustStormSystem.SetStormIntensity(0.05f, 2.5f);
            }

            if (_cutsceneCoroutine != null) StopCoroutine(_cutsceneCoroutine);
            _cutsceneCoroutine = StartCoroutine(ArrivalCutsceneSequence());
        }

        private IEnumerator ArrivalCutsceneSequence()
        {
            // ════════════════════════════════════════════════════════════════════════
            // SHOT 1: ORBITAL HYPERSPACE BREACH & ATMOSPHERIC RE-ENTRY (5.0s)
            // ════════════════════════════════════════════════════════════════════════
            if (_sfxSource != null)
            {
                _sfxSource.PlayOneShot(MechAudioSynth.GetThrusterIgnition(), 0.95f);
            }

            var titleCard = GotGTitleCardUI.GetOrCreate();
            if (titleCard != null)
            {
                titleCard.titleColor = new Color(1.0f, 0.45f, 0.15f);
                titleCard.titleShadowColor = new Color(0.85f, 0.12f, 0.25f, 0.85f);
                titleCard.subtitleColor = new Color(0.1f, 0.92f, 1.0f);
                titleCard.ShowTitleCard("MARS", "CYDONIA BASIN // SECTOR 00", "ANCIENT URM EXCAVATION SITE • HIGH THREAT PROTOCOL • COORD: 40.75°N, 9.46°W", 4.5f);
            }

            Vector3 shipStart1 = new Vector3(130f, 150f, -160f);
            Vector3 shipEnd1 = new Vector3(25f, 45f, -25f);
            Quaternion shipRot1 = Quaternion.Euler(28f, -28f, 14f);

            Vector3 camStart1 = new Vector3(-65f, 58f, -70f);
            Vector3 camEnd1 = new Vector3(-42f, 48f, -40f);

            float t1 = 0f;
            float dur1 = 5.0f;
            bool atmosphericEntryTriggered = false;

            if (starship != null)
            {
                starship.transform.position = shipStart1;
                starship.transform.rotation = shipRot1;
                if (starshipThrusterLight != null) starshipThrusterLight.intensity = 5.0f;
            }

            while (t1 < dur1)
            {
                t1 += Time.deltaTime;
                float norm = Mathf.Clamp01(t1 / dur1);
                float smooth = Mathf.SmoothStep(0f, 1f, norm);

                if (!atmosphericEntryTriggered && t1 >= 2.4f)
                {
                    atmosphericEntryTriggered = true;
                    if (dustStormSystem != null) dustStormSystem.SetStormIntensity(1.0f, 0.45f);
                    if (_sfxSource != null) _sfxSource.PlayOneShot(MechAudioSynth.GetThrusterIgnition(), 0.90f);
                    AddCameraTrauma(0.70f);
                }

                if (starship != null)
                {
                    starship.transform.position = Vector3.Lerp(shipStart1, shipEnd1, Mathf.Pow(norm, 1.25f));
                    starship.transform.rotation = Quaternion.Slerp(shipRot1, Quaternion.Euler(22f, -18f, 8f), norm);
                }

                if (mainCamera != null)
                {
                    Vector3 camPos = Vector3.Lerp(camStart1, camEnd1, smooth);
                    mainCamera.transform.position = ApplyCameraShake(camPos);
                    if (starship != null)
                    {
                        Vector3 lookTarget = Vector3.Lerp(starship.transform.position, new Vector3(20f, 25f, 30f), norm * 0.4f);
                        mainCamera.transform.rotation = Quaternion.LookRotation(lookTarget - camPos);
                    }
                    mainCamera.fieldOfView = Mathf.Lerp(66f, 56f, norm);
                }

                yield return null;
            }

            // ════════════════════════════════════════════════════════════════════════
            // SHOT 2: LOW-ALTITUDE CANYON FLYBY & COMMS (4.5s)
            // ════════════════════════════════════════════════════════════════════════
            Vector3 shipStart2 = new Vector3(-35f, 32f, -40f);
            Vector3 shipEnd2 = new Vector3(0f, 14f, 42f);
            Vector3 camPos2 = new Vector3(22f, 6.2f, 12f);

            if (mainCamera != null)
            {
                mainCamera.transform.position = camPos2;
                mainCamera.fieldOfView = 58f;
            }

            TriggerCockpitTransmission(
                "ALITA",
                "CYDONIA SATELLITE COMMS",
                "URM distress beacon verified... Breaching heavy atmospheric dust storm over Cydonia Basin! Detecting massive ancient energy signature!",
                4.2f
            );

            float t2 = 0f;
            float dur2 = 4.5f;
            bool flybyAudioPlayed = false;

            while (t2 < dur2)
            {
                t2 += Time.deltaTime;
                float norm = Mathf.Clamp01(t2 / dur2);

                if (starship != null)
                {
                    Vector3 curvePos = Vector3.Lerp(shipStart2, shipEnd2, norm);
                    curvePos.x += Mathf.Sin(norm * Mathf.PI) * 12f;
                    starship.transform.position = curvePos;

                    float roll = Mathf.Sin(norm * Mathf.PI) * 38f;
                    starship.transform.rotation = Quaternion.Euler(14f - norm * 20f, 15f - norm * 15f, roll);

                    if (mainCamera != null)
                    {
                        mainCamera.transform.position = ApplyCameraShake(camPos2);
                        mainCamera.transform.rotation = Quaternion.LookRotation(starship.transform.position - mainCamera.transform.position);
                    }

                    if (!flybyAudioPlayed && norm > 0.35f)
                    {
                        flybyAudioPlayed = true;
                        if (_sfxSource != null && starshipFlybyAudio != null)
                        {
                            _sfxSource.PlayOneShot(starshipFlybyAudio, 1.0f);
                        }
                        AddCameraTrauma(0.95f);
                    }
                }

                yield return null;
            }

            // ════════════════════════════════════════════════════════════════════════
            // SHOT 3: STARSHIP TOUCHDOWN & CORE DISCOVERY (5.0s)
            // ════════════════════════════════════════════════════════════════════════
            Vector3 shipStart3 = ShipTouchdownPos + new Vector3(0f, 12f, 0f);
            Vector3 shipEnd3 = ShipTouchdownPos;

            Vector3 camStart3 = ShipTouchdownPos + new Vector3(-5.5f, 0.5f, 6f);
            Vector3 camEnd3 = ShipTouchdownPos + new Vector3(-1.8f, 0.65f, 12.5f);

            if (_sfxSource != null)
            {
                _sfxSource.PlayOneShot(MechAudioSynth.GetThrusterLoop(), 0.65f);
            }

            float t3 = 0f;
            float dur3 = 5.0f;
            bool touchedDown = false;
            bool swordDrawn = false;

            // Make Alita visible at starship ramp walking forward towards core
            Vector3 alitaWalkStart = ShipTouchdownPos + new Vector3(0f, 0.1f, 3f);
            Vector3 alitaWalkTarget = (corePedestal != null) ? (corePedestal.transform.position - Vector3.forward * 2f) : (ShipTouchdownPos + Vector3.forward * 6f);

            if (alitaHero != null)
            {
                alitaHero.transform.position = alitaWalkStart;
                alitaHero.transform.rotation = AlitaCombatStartRot;
                SetActorRenderersActive(alitaHero, true);
                if (_alitaAnimator != null) _alitaAnimator.Play("Locomotion", 0, 0f);
            }

            while (t3 < dur3)
            {
                t3 += Time.deltaTime;
                float norm = Mathf.Clamp01(t3 / dur3);

                if (!touchedDown && starship != null)
                {
                    float descentNorm = Mathf.Clamp01(t3 / 2.2f);
                    float smoothDescent = Mathf.SmoothStep(0f, 1f, descentNorm);
                    starship.transform.position = Vector3.Lerp(shipStart3, shipEnd3, smoothDescent);
                    starship.transform.rotation = Quaternion.Euler(Mathf.Lerp(-12f, 0f, smoothDescent), 0f, 0f);

                    if (descentNorm >= 1.0f)
                    {
                        touchedDown = true;
                        starship.transform.position = shipEnd3;
                        starship.transform.rotation = ShipTouchdownRot;
                        if (starshipThrusterLight != null) starshipThrusterLight.intensity = 0.5f;
                        if (_sfxSource != null) _sfxSource.PlayOneShot(MechAudioSynth.GetPneumaticRelease(), 0.85f);
                        AddCameraTrauma(0.45f);
                    }
                }

                // Alita walks towards the glowing Antimatter Core pedestal
                if (alitaHero != null && t3 > 1.5f)
                {
                    float walkNorm = Mathf.Clamp01((t3 - 1.5f) / 2.2f);
                    alitaHero.transform.position = Vector3.Lerp(alitaWalkStart, alitaWalkTarget, walkNorm);

                    if (walkNorm >= 1.0f && !swordDrawn)
                    {
                        swordDrawn = true;
                        if (_alitaAnimator != null) _alitaAnimator.CrossFadeInFixedTime("Draw_Sword", 0.15f);
                        if (_sfxSource != null && bladeUnsheathAudio != null) _sfxSource.PlayOneShot(bladeUnsheathAudio, 0.9f);
                    }
                }

                if (mainCamera != null)
                {
                    Vector3 camPos = Vector3.Lerp(camStart3, camEnd3, norm);
                    mainCamera.transform.position = ApplyCameraShake(camPos);
                    if (corePedestal != null)
                    {
                        mainCamera.transform.rotation = Quaternion.LookRotation((corePedestal.transform.position + Vector3.up * 1.2f) - camPos);
                    }
                    mainCamera.fieldOfView = 50f;
                }

                yield return null;
            }

            // ════════════════════════════════════════════════════════════════════════
            // TRANSITION: SEAMLESS HAND-OFF TO PLAYABLE CRASHED WARSHIP MISSION
            // ════════════════════════════════════════════════════════════════════════
            yield return StartCoroutine(TransitionToCoreMissionRoutine());
        }

        private IEnumerator TransitionToCoreMissionRoutine()
        {
            Vector3 handoffCamPos = (alitaHero != null ? alitaHero.transform.position : new Vector3(0f, 2.35f, 66f)) + new Vector3(0.48f, 1.48f, -3.2f);
            Quaternion handoffCamRot = Quaternion.Euler(12f, 0f, 0f);

            Vector3 startPos = mainCamera != null ? mainCamera.transform.position : handoffCamPos;
            Quaternion startRot = mainCamera != null ? mainCamera.transform.rotation : handoffCamRot;

            float blendTimer = 0f;
            float blendDuration = 1.2f;

            var titleCard = GotGTitleCardUI.GetOrCreate();
            if (titleCard != null)
            {
                titleCard.HideTitleCard(false);
            }

            while (blendTimer < blendDuration)
            {
                blendTimer += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, blendTimer / blendDuration);

                if (mainCamera != null)
                {
                    mainCamera.transform.position = Vector3.Lerp(startPos, handoffCamPos, t);
                    mainCamera.transform.rotation = Quaternion.Slerp(startRot, handoffCamRot, t);
                    mainCamera.fieldOfView = Mathf.Lerp(50f, 60f, t);
                }

                yield return null;
            }

            EndArrivalAndStartCoreMission();
        }

        private void EndArrivalAndStartCoreMission()
        {
            isPlaying = false;
            currentPhase = CutscenePhase.PlayingMission;

            if (starship != null)
            {
                starship.transform.position = ShipTouchdownPos;
                starship.transform.rotation = ShipTouchdownRot;
                if (starshipThrusterLight != null) starshipThrusterLight.intensity = 0.5f;
            }

            if (alitaHero != null)
            {
                SetActorRenderersActive(alitaHero, true);
                if (_alitaCharController != null) _alitaCharController.enabled = true;
                if (_alitaController != null)
                {
                    _alitaController.enabled = true;
                    _alitaController.controlMode = InsaneGunSwordController.ControlMode.PlayerControlled;
                    _alitaController.isInvincible = false;
                }
            }

            if (mainCamera != null)
            {
                if (_orbitCamera != null)
                {
                    _orbitCamera.enabled = true;
                    if (alitaHero != null) _orbitCamera.target = alitaHero.transform;
                }
                mainCamera.fieldOfView = 60f;
            }

            var titleCard = GotGTitleCardUI.GetOrCreate();
            if (titleCard != null) titleCard.HideTitleCard(true);

            if (MarsCoreRetrievalMission.Instance != null)
            {
                MarsCoreRetrievalMission.Instance.StartCoreRetrievalMission();
            }
        }

        /// <summary>
        /// Triggered when Alita exits the crashed warship after absorbing the Berserker Core.
        /// Plays Shots 5, 6, 7, 8:
        /// - 2 Imperial Dreadnoughts breach clouds (Star Wars arrival)
        /// - Colossus Titan Mech drops down & 12 Mech Pilots surround Alita
        /// - Alita 360° Cleave ONE-SHOTS the army into Euphoria ragdolls
        /// - Titan Mech solo challenge roar & seismic hammer slam
        /// - Handoff to 1-on-1 gameplay boss duel!
        /// </summary>
        public void PlayStarWarsInvasionSequence()
        {
            if (currentPhase == CutscenePhase.InvasionAndObliteration || currentPhase == CutscenePhase.BossBattle) return;

            isPlaying = true;
            currentPhase = CutscenePhase.InvasionAndObliteration;

            // 1. Suspend gameplay controls
            if (_alitaController != null) _alitaController.enabled = false;
            if (_alitaCharController != null) _alitaCharController.enabled = false;
            if (_orbitCamera != null) _orbitCamera.enabled = false;

            // 2. Position Alita in the combat arena facing forward
            if (alitaHero != null)
            {
                alitaHero.transform.position = AlitaCombatStartPos;
                alitaHero.transform.rotation = AlitaCombatStartRot;
                SetActorRenderersActive(alitaHero, true);
                if (_alitaAnimator != null) _alitaAnimator.Play("Idle", 0, 0f);
            }

            if (_cutsceneCoroutine != null) StopCoroutine(_cutsceneCoroutine);
            _cutsceneCoroutine = StartCoroutine(StarWarsInvasionSequenceRoutine());
        }

        private IEnumerator StarWarsInvasionSequenceRoutine()
        {
            // ════════════════════════════════════════════════════════════════════════
            // SHOT 5: 2 DREADNOUGHT CAPITAL SHIPS PULL UP LIKE STAR WARS (5.5s)
            // ════════════════════════════════════════════════════════════════════════
            AddCameraTrauma(0.85f);

            if (starDestroyerFleet != null)
            {
                starDestroyerFleet.StartFleetDescentSequence(6.5f);
            }

            Vector3 camStart5 = new Vector3(0f, 0.9f, 68f);
            Vector3 camEnd5 = new Vector3(0f, 1.4f, 69.5f);

            TriggerCockpitTransmission(
                "TACTICAL SENSORS",
                "ORBITAL THREAT RADAR",
                "SEISMIC AIR QUAKE! Two imperial-class dreadnought warships breaching cloud shelf! Ventral hangars powering up!",
                4.5f
            );

            float t5 = 0f;
            float dur5 = 5.5f;

            while (t5 < dur5)
            {
                t5 += Time.deltaTime;
                float norm = Mathf.Clamp01(t5 / dur5);

                // Worm's-eye camera looking up into the storm clouds at the descending behemoths
                if (mainCamera != null)
                {
                    Vector3 camPos = Vector3.Lerp(camStart5, camEnd5, norm);
                    mainCamera.transform.position = ApplyCameraShake(camPos);
                    mainCamera.transform.rotation = Quaternion.Euler(-34f + norm * 8f, 0f, 0f);
                    mainCamera.fieldOfView = Mathf.Lerp(65f, 55f, norm);
                }

                yield return null;
            }

            // ════════════════════════════════════════════════════════════════════════
            // SHOT 6: MECH BOSS & PILOT ARMY LAND AND OPEN FIRE (5.0s)
            // ════════════════════════════════════════════════════════════════════════
            // 1. Reveal and land Colossus Titan Boss Mech with seismic ground impact
            if (bossMech != null)
            {
                bossMech.transform.position = BossCombatStartPos;
                bossMech.transform.rotation = BossCombatStartRot;
                SetActorRenderersActive(bossMech, true);
                if (_bossAnimator != null) _bossAnimator.Play("Idle", 0, 0f);
            }
            if (_sfxSource != null)
            {
                _sfxSource.PlayOneShot(MechAudioSynth.GetSeismicSlam(), 1.0f);
            }
            AddCameraTrauma(1.0f);

            // 2. Spawn 12 Mech Pilot NPCs encircling Alita
            if (armyManager != null)
            {
                armyManager.SpawnPilotArmy(AlitaCombatStartPos);
                armyManager.StartArmyFiringSequence(alitaHero != null ? alitaHero.transform : null);
            }

            TriggerCockpitTransmission(
                "PILOT COMMANDER",
                "EXPEDITIONARY FORCE",
                "INTRUDER LOCATED! Surround her! Lock arc rifles and fire at will!",
                4.0f
            );

            Vector3 camStart6 = new Vector3(8.5f, 2.2f, 75f);
            Vector3 camEnd6 = new Vector3(4.8f, 1.8f, 75f);

            float t6 = 0f;
            float dur6 = 5.0f;

            while (t6 < dur6)
            {
                t6 += Time.deltaTime;
                float norm = Mathf.Clamp01(t6 / dur6);

                if (mainCamera != null)
                {
                    Vector3 camPos = Vector3.Lerp(camStart6, camEnd6, norm);
                    mainCamera.transform.position = ApplyCameraShake(camPos);
                    mainCamera.transform.rotation = Quaternion.LookRotation(AlitaCombatStartPos + Vector3.up * 1.2f - camPos);
                    mainCamera.fieldOfView = 58f;
                }

                yield return null;
            }

            // ════════════════════════════════════════════════════════════════════════
            // SHOT 7: ALITA ONE-SHOTS THE ENTIRE ARMY — RAGDOLLS FLY EVERYWHERE! (4.5s)
            // ════════════════════════════════════════════════════════════════════════
            Vector3 camStart7 = new Vector3(-2.2f, 1.4f, 71.5f);
            Vector3 camEnd7 = new Vector3(-3.8f, 1.9f, 70.0f);

            // Trigger Alita's 360° Cleave with obliteration callback
            if (superchargeAura != null)
            {
                StartCoroutine(superchargeAura.PerformCataclysmCleaveRoutine(() => {
                    if (armyManager != null)
                    {
                        armyManager.TriggerArmyOneShotObliteration(AlitaCombatStartPos, 32f, 36f);
                    }
                    AddCameraTrauma(1.0f);
                }));
            }
            else if (armyManager != null)
            {
                armyManager.TriggerArmyOneShotObliteration(AlitaCombatStartPos, 32f, 36f);
                AddCameraTrauma(1.0f);
            }

            float t7 = 0f;
            float dur7 = 4.5f;

            while (t7 < dur7)
            {
                t7 += Time.deltaTime;
                float norm = Mathf.Clamp01(t7 / dur7);

                if (mainCamera != null)
                {
                    Vector3 camPos = Vector3.Lerp(camStart7, camEnd7, norm);
                    mainCamera.transform.position = ApplyCameraShake(camPos);
                    mainCamera.transform.rotation = Quaternion.LookRotation(AlitaCombatStartPos + Vector3.up * 1.2f - camPos);
                    mainCamera.fieldOfView = Mathf.Lerp(52f, 62f, norm);
                }

                yield return null;
            }

            // ════════════════════════════════════════════════════════════════════════
            // SHOT 8: MECH BOSS STANDS ALONE — FURY ROAR & HAMMER SLAM (5.0s)
            // ════════════════════════════════════════════════════════════════════════
            Vector3 camStart8 = new Vector3(3.2f, 1.1f, 89.5f);
            Vector3 camEnd8 = new Vector3(1.6f, 1.5f, 90.8f);

            if (bossEyeVisorLight != null) bossEyeVisorLight.intensity = 5.0f;
            if (_sfxSource != null)
            {
                _sfxSource.PlayOneShot(MechAudioSynth.GetOverdriveAlarm(), 0.85f);
                _sfxSource.PlayOneShot(MechAudioSynth.GetServoWhine(), 0.95f);
            }

            if (_bossAnimator != null)
            {
                _bossAnimator.CrossFadeInFixedTime("ReactorPowerUp", 0.2f);
            }

            TriggerTacticalAlert("[!] ALL ESCORT PILOTS ELIMINATED // ENGAGE COLOSSUS-06 TITAN WAR MECH [!]", 4.5f);

            float t8 = 0f;
            float dur8 = 5.0f;
            bool hammerSlammed = false;

            while (t8 < dur8)
            {
                t8 += Time.deltaTime;
                float norm = Mathf.Clamp01(t8 / dur8);

                if (!hammerSlammed && t8 >= 2.6f)
                {
                    hammerSlammed = true;
                    if (_bossAnimator != null) _bossAnimator.CrossFadeInFixedTime("GroundSlam", 0.15f);
                    if (_sfxSource != null) _sfxSource.PlayOneShot(MechAudioSynth.GetSeismicSlam(), 1.0f);
                    AddCameraTrauma(1.0f);
                }

                if (mainCamera != null)
                {
                    Vector3 camPos = Vector3.Lerp(camStart8, camEnd8, norm);
                    mainCamera.transform.position = ApplyCameraShake(camPos);
                    if (bossMech != null)
                    {
                        Vector3 lookTarget = bossMech.transform.position + Vector3.up * 5.2f;
                        mainCamera.transform.rotation = Quaternion.LookRotation(lookTarget - camPos);
                    }
                    mainCamera.fieldOfView = Mathf.Lerp(52f, 45f, norm);
                }

                yield return null;
            }

            // ════════════════════════════════════════════════════════════════════════
            // TRANSITION: SEAMLESS HAND-OFF TO GAMEPLAY COMBAT
            // ════════════════════════════════════════════════════════════════════════
            yield return StartCoroutine(TransitionToGameplayRoutine());
        }

        private IEnumerator TransitionToGameplayRoutine()
        {
            Vector3 handoffCamPos = AlitaCombatStartPos + new Vector3(0.48f, 1.48f, -3.2f);
            Quaternion handoffCamRot = Quaternion.Euler(12f, 0f, 0f);

            Vector3 startPos = mainCamera != null ? mainCamera.transform.position : handoffCamPos;
            Quaternion startRot = mainCamera != null ? mainCamera.transform.rotation : handoffCamRot;

            float blendTimer = 0f;
            float blendDuration = 1.2f;

            var titleCard = GotGTitleCardUI.GetOrCreate();
            if (titleCard != null)
            {
                titleCard.HideTitleCard(false);
            }

            while (blendTimer < blendDuration)
            {
                blendTimer += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, blendTimer / blendDuration);

                if (mainCamera != null)
                {
                    mainCamera.transform.position = Vector3.Lerp(startPos, handoffCamPos, t);
                    mainCamera.transform.rotation = Quaternion.Slerp(startRot, handoffCamRot, t);
                    mainCamera.fieldOfView = Mathf.Lerp(48f, 60f, t);
                }

                yield return null;
            }

            CompleteCutsceneAndStartCombat();
        }

        public void SkipCutscene()
        {
            if (!isPlaying) return;

            if (_cutsceneCoroutine != null)
            {
                StopCoroutine(_cutsceneCoroutine);
                _cutsceneCoroutine = null;
            }

            var titleCard = GotGTitleCardUI.GetOrCreate();
            if (titleCard != null)
            {
                titleCard.HideTitleCard(true);
            }

            _isShowingComms = false;
            _tacticalAlertText = "";
            _shakeTrauma = 0f;

            if (currentPhase == CutscenePhase.ArrivalLanding)
            {
                EndArrivalAndStartCoreMission();
                return;
            }

            // If in Invasion phase or general fallback:
            // Ensure Core is absorbed and Alita is supercharged
            if (corePedestal != null)
            {
                corePedestal.FastForwardAbsorption(alitaHero != null ? alitaHero.transform : null);
            }
            if (superchargeAura != null)
            {
                superchargeAura.ActivateSupercharge();
            }

            // Ensure Star Destroyer Fleet is in siege position
            if (starDestroyerFleet != null)
            {
                starDestroyerFleet.FastForwardFleet();
            }

            // Ensure army is spawned and one-shot obliterated into ragdolls
            if (armyManager != null)
            {
                if (!armyManager.isArmySpawned) armyManager.SpawnPilotArmy(AlitaCombatStartPos);
                armyManager.TriggerArmyOneShotObliteration(AlitaCombatStartPos, 32f, 36f);
            }

            CompleteCutsceneAndStartCombat();
        }

        private void CompleteCutsceneAndStartCombat()
        {
            isPlaying = false;
            currentPhase = CutscenePhase.BossBattle;

            // 1. Lock Final Positions & Rotations
            if (starship != null)
            {
                starship.transform.position = ShipTouchdownPos;
                starship.transform.rotation = ShipTouchdownRot;
                if (starshipThrusterLight != null) starshipThrusterLight.intensity = 0.5f;
            }

            if (alitaHero != null)
            {
                alitaHero.transform.position = AlitaCombatStartPos;
                alitaHero.transform.rotation = AlitaCombatStartRot;
                SetActorRenderersActive(alitaHero, true);

                if (_alitaAnimator != null)
                {
                    _alitaAnimator.Play("Locomotion", 0, 0f);
                }

                if (_alitaCharController != null) _alitaCharController.enabled = true;
                if (_alitaController != null)
                {
                    _alitaController.enabled = true;
                    _alitaController.controlMode = InsaneGunSwordController.ControlMode.PlayerControlled;
                    if (bossMech != null) _alitaController.targetEnemy = bossMech.transform;
                }
            }

            if (bossMech != null)
            {
                bossMech.transform.position = BossCombatStartPos;
                bossMech.transform.rotation = BossCombatStartRot;
                SetActorRenderersActive(bossMech, true);

                if (bossEyeVisorLight != null) bossEyeVisorLight.intensity = 4.0f;

                if (_bossAnimator != null)
                {
                    _bossAnimator.Play("Idle", 0, 0f);
                }

                if (_bossAI != null)
                {
                    _bossAI.enabled = true;
                    if (alitaHero != null) _bossAI.target = alitaHero.transform;
                }

                if (_bossHealthBar != null)
                {
                    _bossHealthBar.enabled = true;
                    _bossHealthBar.bossName = "COLOSSUS-06 // TITAN WAR MECH";
                    _bossHealthBar.bossSub = "HEAVY KINETIC SIEGE UNIT // URM EXCAVATION GUARDIAN";
                }
            }

            // 2. Re-enable Gameplay SmoothOrbitCamera
            if (mainCamera != null)
            {
                mainCamera.transform.position = AlitaCombatStartPos + new Vector3(0.48f, 1.48f, -3.2f);
                mainCamera.transform.rotation = Quaternion.Euler(12f, 0f, 0f);
                mainCamera.fieldOfView = 60f;

                if (_orbitCamera != null)
                {
                    _orbitCamera.enabled = true;
                    if (alitaHero != null) _orbitCamera.target = alitaHero.transform;
                }
            }

            // 3. Stop drone audio and trigger Combat Music
            if (_ambientSource != null) _ambientSource.Stop();

            if (_musicSource != null && combatMusic != null)
            {
                _musicSource.clip = combatMusic;
                _musicSource.volume = 0.75f;
                _musicSource.Play();
            }

            // 4. Alert Mars Final Battle Director if active
            if (_marsDirector != null)
            {
                _marsDirector.enabled = true;
                _marsDirector.StartCombatEncounter();
                _marsDirector.TriggerAlert("[ENGAGEMENT ACTIVE] DESTROY THE TITAN WAR MECH // SECURE THE CYDONIA CRATER", 5.0f, true);
            }

            // 5. Ensure Dust Storm continues actively in gameplay combat
            if (dustStormSystem != null)
            {
                dustStormSystem.SetStormIntensity(1.0f, 10.0f);
            }

            Alita.UI.CyberpunkDialogueHUD.Instance?.CloseDialogue();
            Debug.Log("[MarsArrivalCutsceneDirector] Mars Invasion Cutscene Completed. Army obliterated. 1-on-1 Combat engaged!");
        }

        private Vector3 ApplyCameraShake(Vector3 basePos)
        {
            if (_shakeTrauma <= 0f) return basePos;

            float shakeMag = _shakeTrauma * _shakeTrauma;
            float offsetX = (Mathf.PerlinNoise(Time.time * 28f, 0f) * 2f - 1f) * MaxShakePosOffset * shakeMag;
            float offsetY = (Mathf.PerlinNoise(0f, Time.time * 28f) * 2f - 1f) * MaxShakePosOffset * shakeMag;

            if (mainCamera != null)
            {
                float roll = (Mathf.PerlinNoise(Time.time * 22f, 12f) * 2f - 1f) * MaxShakeRoll * shakeMag;
                float pitch = (Mathf.PerlinNoise(15f, Time.time * 22f) * 2f - 1f) * MaxShakePitch * shakeMag;
                mainCamera.transform.Rotate(pitch, 0f, roll, Space.Self);
            }

            return basePos + new Vector3(offsetX, offsetY, 0f);
        }

        public void AddCameraTrauma(float amount)
        {
            _shakeTrauma = Mathf.Clamp01(_shakeTrauma + amount);
        }

        public void TriggerCockpitTransmission(string speaker, string role, string text, float duration)
        {
            _commsSpeaker = speaker;
            _commsRole = role;
            _commsText = text;
            _commsTimer = duration;
            _isShowingComms = true;

            if (_voiceSource != null && commsChirpAudio != null)
            {
                _voiceSource.PlayOneShot(commsChirpAudio, 0.75f);
            }

            CyberpunkDialogueHUD.GetOrCreate().ShowDialogue(
                speaker,
                text,
                null,
                role,
                null,
                duration
            );
        }

        public void TriggerTacticalAlert(string text, float duration)
        {
            _tacticalAlertText = text;
            _tacticalAlertTimer = duration;
            if (_sfxSource != null)
            {
                _sfxSource.PlayOneShot(MechAudioSynth.GetOverdriveAlarm(), 0.7f);
            }
        }

        private void SetActorRenderersActive(GameObject actor, bool active)
        {
            if (actor == null) return;
            foreach (var r in actor.GetComponentsInChildren<Renderer>(true))
            {
                r.enabled = active;
            }
        }

        private void InitGUIStyles()
        {
            if (_stylesInitialized) return;

            if (_commsBackdropTex == null)
            {
                _commsBackdropTex = new Texture2D(2, 2);
                Color[] pix = new Color[4];
                for (int i = 0; i < 4; i++) pix[i] = new Color(0.04f, 0.08f, 0.14f, 0.92f);
                _commsBackdropTex.SetPixels(pix);
                _commsBackdropTex.Apply();
            }

            _commsHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            _commsHeaderStyle.normal.textColor = new Color(0f, 0.92f, 1f);

            _commsBodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true
            };
            _commsBodyStyle.normal.textColor = new Color(0.92f, 0.95f, 1f);

            _tacticalAlertStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _tacticalAlertStyle.normal.textColor = new Color(1f, 0.22f, 0.12f);

            _stylesInitialized = true;
        }

        private void OnGUI()
        {
            if (!isPlaying && string.IsNullOrEmpty(_tacticalAlertText)) return;

            InitGUIStyles();

            // 1. Cockpit Radio Comms Transmission Card rendered via AAA CyberpunkDialogueHUD

            // 2. Tactical Warning Banner
            if (!string.IsNullOrEmpty(_tacticalAlertText))
            {
                float bannerW = Mathf.Min(820f, Screen.width * 0.88f);
                float bannerH = 40f;
                float bannerX = (Screen.width - bannerW) * 0.5f;
                float bannerY = 85f;
                Rect bannerRect = new Rect(bannerX, bannerY, bannerW, bannerH);

                Color prevColor = GUI.color;
                GUI.color = Color.white;
                GUI.DrawTexture(bannerRect, _commsBackdropTex);

                GUI.color = new Color(1f, 0.22f, 0.12f, 0.9f);
                GUI.Box(new Rect(bannerRect.x, bannerRect.y, bannerRect.width, 2.5f), GUIContent.none);
                GUI.Box(new Rect(bannerRect.x, bannerRect.y + bannerRect.height - 2.5f, bannerRect.width, 2.5f), GUIContent.none);

                GUI.color = Color.white;
                GUI.Label(bannerRect, _tacticalAlertText, _tacticalAlertStyle);
                GUI.color = prevColor;
            }
        }
    }

    public enum MarsMissionStage
    {
        None,
        InfiltrateCrashedWarship,
        CoreFound_AwaitingExtraction,
        CoreAbsorbed_ExitWarship,
        StarWarsInvasionEngaged,
        BossBattle
    }

    /// <summary>
    /// Playable mission on Mars: Finding the URM Antimatter Core inside the Crashed Spaceship.
    /// Manages:
    /// 1. Alita disembarks her starship and is in full 3rd-person player control.
    /// 2. HUD quest objectives and 3D screen-space reticle pointing to the Crashed URM Warship.
    /// 3. Infiltrating the crashed spaceship's breached hull and discovering the pulsating Antimatter Core.
    /// 4. Interactive [E] extraction and power absorption (500% Berserker overcharge, crackling lightning aura, glowing blade).
    /// 5. Exiting the crashed spaceship triggers the Star Wars Dreadnought arrival, Pilot Army surround, 360° ragdoll obliteration, and Titan Mech duel!
    /// </summary>
    public class MarsCoreRetrievalMission : MonoBehaviour
    {
        private static MarsCoreRetrievalMission _instance;
        public static MarsCoreRetrievalMission Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<MarsCoreRetrievalMission>() ?? FindObjectOfType<MarsCoreRetrievalMission>();
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("Mission Stage")]
        public MarsMissionStage currentStage = MarsMissionStage.None;

        [Header("Scene References")]
        public GameObject alitaHero;
        public GameObject crashedWarship;
        public MarsAntimatterCorePedestal corePedestal;
        public MarsArrivalCutsceneDirector cutsceneDirector;
        public AlitaSuperchargeAura superchargeAura;

        [Header("Positions & Thresholds")]
        public Vector3 crashedShipPos = new Vector3(0f, 0f, 115f);
        public Vector3 coreBayPos = new Vector3(0f, 0.40f, 103f);
        public Vector3 warshipExitPos = new Vector3(0f, 1.0f, 85f);
        public float coreInteractRadius = 3.8f;
        public float warshipExitZ = 86.0f;

        [Header("Visual & FX")]
        public GameObject coreExtractionVFXPrefab;
        public AudioClip extractionChimeAudio;

        // Internal State
        private bool _isPlayerNearCore = false;
        private bool _hasExitedWarship = false;
        private Camera _mainCam;
        private AudioSource _audioSource;

        private void Awake()
        {
            Instance = this;
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
        }

        private void Start()
        {
            FindSceneReferences();
            EnsureCrashedSpaceship();
        }

        public void FindSceneReferences()
        {
            if (alitaHero == null)
            {
                alitaHero = GameObject.Find("Alita") ?? GameObject.FindWithTag("Player");
            }

            if (corePedestal == null)
            {
                corePedestal = FindFirstObjectByType<MarsAntimatterCorePedestal>() ?? FindObjectOfType<MarsAntimatterCorePedestal>();
            }

            if (cutsceneDirector == null)
            {
                cutsceneDirector = FindFirstObjectByType<MarsArrivalCutsceneDirector>() ?? FindObjectOfType<MarsArrivalCutsceneDirector>();
            }

            if (superchargeAura == null && alitaHero != null)
            {
                superchargeAura = alitaHero.GetComponent<AlitaSuperchargeAura>() ?? alitaHero.AddComponent<AlitaSuperchargeAura>();
            }

            if (_mainCam == null)
            {
                _mainCam = Camera.main;
            }

            if (corePedestal != null)
            {
                coreBayPos = corePedestal.transform.position;
                crashedShipPos = corePedestal.transform.position + Vector3.forward * 8f;
                warshipExitPos = corePedestal.transform.position - Vector3.forward * 12f;
                warshipExitZ = warshipExitPos.z;
            }
        }

        /// <summary>
        /// Ensures the Crashed URM Warship is in the scene with interior core bay,
        /// breached hull entrance, and emergency lighting.
        /// </summary>
        public void EnsureCrashedSpaceship()
        {
            if (crashedWarship != null) return;

            var existing = GameObject.Find("Crashed_URM_Warship_Derelict");
            if (existing != null)
            {
                crashedWarship = existing;
                return;
            }

#if UNITY_EDITOR
            var shipPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonSciFiSpace/Prefabs/Vehicles/SM_Ship_Cruiser_01.prefab")
                          ?? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonSciFiWorlds/Prefabs/Buildings/SM_Bld_Scav_Wreckage_01.prefab");

            if (shipPrefab != null)
            {
                crashedWarship = (GameObject)PrefabUtility.InstantiatePrefab(shipPrefab);
                crashedWarship.name = "Crashed_URM_Warship_Derelict";
                crashedWarship.transform.position = crashedShipPos;
                crashedWarship.transform.rotation = Quaternion.Euler(7f, 180f, -4f);

                var lightObj = new GameObject("CoreBay_Emergency_Strobe");
                lightObj.transform.SetParent(crashedWarship.transform, false);
                lightObj.transform.localPosition = new Vector3(0f, 2.5f, 0f);
                var l = lightObj.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(0.1f, 0.9f, 1.0f);
                l.range = 18f;
                l.intensity = 2.8f;

                if (corePedestal != null)
                {
                    corePedestal.transform.position = coreBayPos;
                }
            }
#endif
        }

        /// <summary>
        /// Starts the playable Core Retrieval Mission on foot on Mars.
        /// </summary>
        public void StartCoreRetrievalMission()
        {
            FindSceneReferences();
            currentStage = MarsMissionStage.InfiltrateCrashedWarship;

            // Unlock Alita's third-person player controls
            if (alitaHero != null)
            {
                var swordCtrl = alitaHero.GetComponent<InsaneGun.Combat.InsaneGunSwordController>();
                if (swordCtrl != null)
                {
                    swordCtrl.controlMode = InsaneGun.Combat.InsaneGunSwordController.ControlMode.PlayerControlled;
                    swordCtrl.isInvincible = false;
                }

                var cc = alitaHero.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = true;
            }

            // Restore third-person camera
            if (_mainCam != null)
            {
                var orbit = _mainCam.GetComponent<SmoothOrbitCamera>();
                if (orbit != null)
                {
                    orbit.enabled = true;
                    if (alitaHero != null) orbit.target = alitaHero.transform;
                }
            }

            // Show HUD Quest Objective
            if (CyberpunkHUDController.Instance != null)
            {
                CyberpunkHUDController.Instance.SetQuestObjective(
                    "OPERATION RED HORIZON // CYDONIA BASIN",
                    "► Infiltrate the crashed URM Warship & recover the Antimatter Berserker Core"
                );
                CyberpunkHUDController.Instance.ShowToast("TOUCHDOWN CONFIRMED: EXPLORE CRASHED DERELICT", 5.0f);
            }

            if (BlackMarketNPCDialogueHUD.Instance != null)
            {
                BlackMarketNPCDialogueHUD.Instance.ShowMissionBanner("MISSION ACTIVE: INFILTRATE CRASHED URM WARSHIP & RECOVER CORE");
                BlackMarketNPCDialogueHUD.Instance.ShowTransmission(
                    "ALITA",
                    "URM TACTICAL SENSORS",
                    "Touchdown confirmed in Cydonia Basin. Sensors detect an ancient URM battlecruiser half-buried in the dunes ahead. The Berserker Heart is inside the reactor bay!",
                    new Color(0.2f, 0.92f, 1.0f),
                    6.5f
                );
            }
        }

        private void Update()
        {
            // Developer test shortcuts
            if (Input.GetKeyDown(KeyCode.F5))
            {
                TestTeleportToCore();
            }
            if (Input.GetKeyDown(KeyCode.F6))
            {
                TestTriggerStarWarsInvasion();
            }
            if (Input.GetKeyDown(KeyCode.F7))
            {
                TestTriggerPilotObliteration();
            }

            if (currentStage == MarsMissionStage.None) return;

            if (alitaHero == null) FindSceneReferences();
            if (alitaHero == null) return;

            Vector3 playerPos = alitaHero.transform.position;

            // 1. Check distance to Core Pedestal
            if (currentStage == MarsMissionStage.InfiltrateCrashedWarship || currentStage == MarsMissionStage.CoreFound_AwaitingExtraction)
            {
                Vector3 corePos = corePedestal != null ? corePedestal.transform.position : coreBayPos;
                float dist = Vector3.Distance(playerPos, corePos);
                _isPlayerNearCore = dist <= coreInteractRadius;

                if (_isPlayerNearCore)
                {
                    currentStage = MarsMissionStage.CoreFound_AwaitingExtraction;

                    // Press [E] or [F] to extract and absorb the core
                    if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F) || dist < 1.4f)
                    {
                        ExtractAndAbsorbCore();
                    }
                }
                else
                {
                    currentStage = MarsMissionStage.InfiltrateCrashedWarship;
                }
            }

            // 2. Check if player has exited the crashed warship after absorbing the core
            if (currentStage == MarsMissionStage.CoreAbsorbed_ExitWarship && !_hasExitedWarship)
            {
                // Exiting toward the central basin (z decreases toward combat arena)
                if (playerPos.z <= warshipExitZ || Vector3.Distance(playerPos, warshipExitPos) < 4.0f)
                {
                    _hasExitedWarship = true;
                    OnPlayerExitedWarship();
                }
            }
        }

        /// <summary>
        /// Alita extracts the ancient URM Antimatter Berserker Core from the reactor bay.
        /// </summary>
        public void ExtractAndAbsorbCore()
        {
            if (currentStage != MarsMissionStage.CoreFound_AwaitingExtraction) return;

            currentStage = MarsMissionStage.CoreAbsorbed_ExitWarship;

            // Play absorption sequence on pedestal
            if (corePedestal != null && alitaHero != null)
            {
                corePedestal.StartAbsorptionSequence(alitaHero.transform, OnCoreAbsorptionComplete);
            }
            else
            {
                OnCoreAbsorptionComplete();
            }

            // Play audio surge
            PlayExtractionSound();
        }

        private void OnCoreAbsorptionComplete()
        {
            // Activate Alita's crackling plasma supercharge aura and blade glow
            if (superchargeAura != null)
            {
                superchargeAura.ActivateSupercharge();
            }

            if (CyberpunkHUDController.Instance != null)
            {
                CyberpunkHUDController.Instance.SetQuestObjective(
                    "POWER OVERCHARGE",
                    "► Berserker Heart Synchronized (500% Output)! Exit the warship into the Cydonia Basin"
                );
                CyberpunkHUDController.Instance.ShowToast("CRITICAL: URM COMBAT LIMITERS DISENGAGED", 5.0f);
            }

            if (BlackMarketNPCDialogueHUD.Instance != null)
            {
                BlackMarketNPCDialogueHUD.Instance.ShowMissionBanner("⚡ URM ANTIMATTER BERSERKER HEART SYNCHRONIZED — COMBAT POWER AT 500%!");
                BlackMarketNPCDialogueHUD.Instance.ShowTransmission(
                    "ALITA",
                    "URM BERSERKER // OVERLOAD",
                    "Berserker core integrated... Systems operating at 500% capacity! All combat limiters disengaged!",
                    new Color(0.95f, 0.95f, 1.0f),
                    6.0f
                );
            }
        }

        /// <summary>
        /// Triggered when Alita exits the crashed warship into the Cydonia Basin.
        /// Initiates the epic Star Wars Dreadnought arrival, Pilot Army surround, and boss duel!
        /// </summary>
        public void OnPlayerExitedWarship()
        {
            currentStage = MarsMissionStage.StarWarsInvasionEngaged;

            if (CyberpunkHUDController.Instance != null)
            {
                CyberpunkHUDController.Instance.SetQuestObjective(
                    "IMPERIAL SIEGE",
                    "► SURVIVE THE DREADNOUGHT INVASION & TITAN WAR MECH ASSAULT!"
                );
                CyberpunkHUDController.Instance.ShowToast("SEISMIC ANOMALY: DREADNOUGHTS BREACHING CLOUDS", 4.5f);
            }

            // Trigger the cinematic Star Wars Dreadnought arrival & Mech drop
            if (cutsceneDirector != null)
            {
                cutsceneDirector.PlayStarWarsInvasionSequence();
            }
        }

        private void PlayExtractionSound()
        {
            if (_audioSource == null) return;
            // High-voltage synthetic electrical warp surge
            int rate = 44100;
            int len = (int)(rate * 1.8f);
            float[] data = new float[len];
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / rate;
                float freq = Mathf.Lerp(110f, 1200f, t / 1.8f);
                float env = Mathf.Clamp01(t / 0.3f) * Mathf.Exp(-2.2f * t);
                data[i] = (Mathf.Sin(2f * Mathf.PI * freq * t) * 0.5f + (UnityEngine.Random.value * 2f - 1f) * 0.25f) * env;
            }
            var clip = AudioClip.Create("Core_Extraction_Surge", len, 1, rate, false);
            clip.SetData(data, 0);
            _audioSource.PlayOneShot(clip, 1.0f);
        }

        #region HUD & 3D Waypoint Reticle
        private void OnGUI()
        {
            if (currentStage == MarsMissionStage.None || currentStage == MarsMissionStage.BossBattle) return;
            if (SyntyCombined.Cutscene.GlobalCutsceneState.IsCutsceneActive) return;

            float sw = Screen.width;
            float sh = Screen.height;

            // 1. Mission Card (Top-Left)
            float cardW = 420f;
            float cardH = 68f;
            float cardX = 24f;
            float cardY = 22f;

            GUI.color = new Color(0.04f, 0.05f, 0.08f, 0.92f);
            GUI.DrawTexture(new Rect(cardX, cardY, cardW, cardH), Texture2D.whiteTexture);
            GUI.color = new Color(1.0f, 0.45f, 0.15f, 0.95f);
            DrawBorder(new Rect(cardX, cardY, cardW, cardH), 1);

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };
            titleStyle.normal.textColor = new Color(1.0f, 0.5f, 0.15f);

            var subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
            subStyle.normal.textColor = Color.white;

            switch (currentStage)
            {
                case MarsMissionStage.InfiltrateCrashedWarship:
                    GUI.Label(new Rect(cardX + 12f, cardY + 8f, cardW - 24f, 18f), "MISSION: THE BERSERKER HEART // CYDONIA BASIN", titleStyle);
                    GUI.Label(new Rect(cardX + 12f, cardY + 28f, cardW - 24f, 32f), ">> INFILTRATE CRASHED URM WARSHIP <<\nLOCATE THE ANTIMATTER BERSERKER CORE", subStyle);
                    break;

                case MarsMissionStage.CoreFound_AwaitingExtraction:
                    GUI.Label(new Rect(cardX + 12f, cardY + 8f, cardW - 24f, 18f), "MISSION: THE BERSERKER HEART // CYDONIA BASIN", titleStyle);
                    subStyle.normal.textColor = new Color(0.1f, 0.95f, 1.0f);
                    GUI.Label(new Rect(cardX + 12f, cardY + 28f, cardW - 24f, 32f), ">> ANTIMATTER CORE LOCATED <<\nPRESS [E] TO EXTRACT & ABSORB POWER", subStyle);
                    break;

                case MarsMissionStage.CoreAbsorbed_ExitWarship:
                    GUI.Label(new Rect(cardX + 12f, cardY + 8f, cardW - 24f, 18f), "URM CORE ABSORPTION COMPLETE // COMBAT POWER 500%", titleStyle);
                    subStyle.normal.textColor = new Color(0.2f, 1.0f, 0.4f);
                    GUI.Label(new Rect(cardX + 12f, cardY + 28f, cardW - 24f, 32f), ">> CRITICAL ENERGY OVERCHARGE ACTIVE <<\nEXIT WARSHIP DERELICT INTO CYDONIA BASIN", subStyle);
                    break;
            }

            // 2. Interaction Prompt when near Core
            if (_isPlayerNearCore && currentStage == MarsMissionStage.CoreFound_AwaitingExtraction)
            {
                float promptW = 380f;
                float promptH = 36f;
                float promptX = (sw - promptW) * 0.5f;
                float promptY = sh * 0.72f;

                GUI.color = new Color(0.02f, 0.05f, 0.10f, 0.92f);
                GUI.DrawTexture(new Rect(promptX, promptY, promptW, promptH), Texture2D.whiteTexture);
                GUI.color = new Color(0.1f, 0.95f, 1.0f, 0.95f);
                DrawBorder(new Rect(promptX, promptY, promptW, promptH), 1);

                var promptStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 13,
                    fontStyle = FontStyle.Bold
                };
                promptStyle.normal.textColor = new Color(0.2f, 1.0f, 1.0f);
                GUI.Label(new Rect(promptX, promptY, promptW, promptH), "[E] EXTRACT & ABSORB URM ANTIMATTER CORE", promptStyle);
            }

            // 3. 3D Waypoint Reticle in screen space
            if (_mainCam != null)
            {
                Vector3 targetWp = (currentStage == MarsMissionStage.CoreAbsorbed_ExitWarship) ? warshipExitPos : (corePedestal != null ? corePedestal.transform.position : coreBayPos);
                Vector3 screenPos = _mainCam.WorldToScreenPoint(targetWp);

                if (screenPos.z > 0.5f)
                {
                    float cx = screenPos.x;
                    float cy = sh - screenPos.y;
                    float dist = screenPos.z;

                    GUI.color = (currentStage == MarsMissionStage.CoreAbsorbed_ExitWarship) ? new Color(0.2f, 1f, 0.4f, 0.95f) : new Color(0.1f, 0.95f, 1f, 0.95f);
                    DrawCircleReticle(cx, cy, 26f);

                    var wpStyle = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 11,
                        fontStyle = FontStyle.Bold
                    };
                    wpStyle.normal.textColor = GUI.color;

                    string wpText = (currentStage == MarsMissionStage.CoreAbsorbed_ExitWarship)
                        ? $"WARSHIP HULL EXIT\n[{dist:F0}m]"
                        : $"URM CRASHED WARSHIP // CORE BAY\n[{dist:F0}m]";

                    GUI.Label(new Rect(cx - 130f, cy + 28f, 260f, 30f), wpText, wpStyle);
                }
            }

            GUI.color = Color.white;
        }

        private void DrawBorder(Rect rect, int thickness)
        {
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);
        }

        private void DrawCircleReticle(float cx, float cy, float radius)
        {
            int segments = 16;
            for (int i = 0; i < segments; i++)
            {
                float a1 = i * (Mathf.PI * 2f / segments);
                float a2 = (i + 1) * (Mathf.PI * 2f / segments);
                Vector2 p1 = new Vector2(cx + Mathf.Cos(a1) * radius, cy + Mathf.Sin(a1) * radius);
                Vector2 p2 = new Vector2(cx + Mathf.Cos(a2) * radius, cy + Mathf.Sin(a2) * radius);
                DrawLine(p1, p2, 2f);
            }
        }

        private void DrawLine(Vector2 p1, Vector2 p2, float width)
        {
            Vector2 d = p2 - p1;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            GUIUtility.RotateAroundPivot(angle, p1);
            GUI.DrawTexture(new Rect(p1.x, p1.y - width * 0.5f, d.magnitude, width), Texture2D.whiteTexture);
            GUIUtility.RotateAroundPivot(-angle, p1);
        }
        #endregion

        #region Developer Test Shortcuts
        [ContextMenu("Test Teleport To Core (F5)")]
        public void TestTeleportToCore()
        {
            FindSceneReferences();
            if (alitaHero != null)
            {
                alitaHero.transform.position = coreBayPos + Vector3.back * 2.5f;
                alitaHero.transform.rotation = Quaternion.identity;
                Physics.SyncTransforms();
            }
            currentStage = MarsMissionStage.CoreFound_AwaitingExtraction;
        }

        [ContextMenu("Test Trigger Star Wars Invasion (F6)")]
        public void TestTriggerStarWarsInvasion()
        {
            FindSceneReferences();
            if (alitaHero != null)
            {
                alitaHero.transform.position = warshipExitPos + Vector3.back * 4f;
                Physics.SyncTransforms();
            }
            if (superchargeAura != null)
            {
                superchargeAura.ActivateSupercharge();
            }
            OnPlayerExitedWarship();
        }

        [ContextMenu("Test Trigger Pilot Obliteration (F7)")]
        public void TestTriggerPilotObliteration()
        {
            FindSceneReferences();
            if (cutsceneDirector != null && cutsceneDirector.armyManager != null && alitaHero != null)
            {
                cutsceneDirector.armyManager.TriggerArmyOneShotObliteration(alitaHero.transform.position, 32f, 36f);
            }
        }
        #endregion
    }
}
