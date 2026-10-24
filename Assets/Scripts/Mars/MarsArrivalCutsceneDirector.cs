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
    public partial class MarsArrivalCutsceneDirector : MonoBehaviour
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
        public AudioClip invasionMusic;
        public AudioClip commsChirpAudio;
        public AudioClip postLandingAudio;

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
        private bool _isShowingInteriorCabin = false;

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
                var shipPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonSciFiWorlds/Prefabs/Props/Vehicles/SM_Veh_HoverCraft_01.prefab");
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
                    lightGO.transform.localPosition = new Vector3(0f, 1.65f, -3.4f);
                    starshipThrusterLight = lightGO.AddComponent<Light>();
                    starshipThrusterLight.type = LightType.Point;
                    starshipThrusterLight.color = new Color(1.0f, 0.40f, 0.12f);
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
                combatMusic = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/Cyberpunk_Combat_Metal_Heart.mp3");
            if (invasionMusic == null)
                invasionMusic = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/paulyudin-intense-intense-music-574004.mp3");

            if (commsChirpAudio == null)
                commsChirpAudio = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/NPC_Chatter/npc_helperbot_chirp_01.wav");

            if (postLandingAudio == null)
            {
                postLandingAudio = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/aaaaa.mp3");
            }
#endif
            if (postLandingAudio == null)
            {
                postLandingAudio = Resources.Load<AudioClip>("MarsSfx/Mars_PostLanding_Theme");
            }
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
            // Orbit -> atmosphere -> flyby -> braking -> touchdown -> Alita steps out (MarsArrivalLandingSequence.cs)
            yield return StartCoroutine(PlayOrbitalLandingSequence());
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
            FinishArrivalStage(true);
            isPlaying = false;
            _isShowingInteriorCabin = false;
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

            // Play post-landing exploration music (aaaaa.mpeg)
            PlayPostLandingAudio();

            if (MarsCoreRetrievalMission.Instance != null)
            {
                MarsCoreRetrievalMission.Instance.StartCoreRetrievalMission();
            }
        }

        /// <summary>
        /// Plays the post-landing Martian exploration music (aaaaa.mp3) on the music source after touchdown.
        /// Smoothly crossfades in over the desert wind.
        /// </summary>
        public void PlayPostLandingAudio()
        {
            if (postLandingAudio == null)
            {
#if UNITY_EDITOR
                postLandingAudio = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/aaaaa.mp3");
#endif
                if (postLandingAudio == null)
                {
                    postLandingAudio = Resources.Load<AudioClip>("MarsSfx/Mars_PostLanding_Theme");
                }
            }

            if (postLandingAudio != null)
            {
                CrossfadeMusic(postLandingAudio, 0.65f, fadeOut: 1.0f, fadeIn: 2.2f);
                Debug.Log("[MarsArrivalCutsceneDirector] Post-landing Martian exploration music started: " + postLandingAudio.name);
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

            // Ensure Boss Mech starts hidden in orbital drop altitude
            if (bossMech != null)
            {
                bossMech.transform.position = BossCombatStartPos + Vector3.up * 80f;
                bossMech.transform.rotation = BossCombatStartRot;
                SetActorRenderersActive(bossMech, false);
            }
            if (bossEyeVisorLight != null) bossEyeVisorLight.intensity = 0f;

            if (_cutsceneCoroutine != null) StopCoroutine(_cutsceneCoroutine);
            _cutsceneCoroutine = StartCoroutine(StarWarsInvasionSequenceRoutine());
        }

        private Coroutine _musicFade;

        /// <summary>Fades the current music out and the next track in on the one music source, so tracks never overlap.</summary>
        private void CrossfadeMusic(AudioClip clip, float volume, float fadeOut = 0.8f, float fadeIn = 1.6f)
        {
            if (_musicSource == null || clip == null) return;
            if (_musicSource.clip == clip && _musicSource.isPlaying) return;
            if (_musicFade != null) StopCoroutine(_musicFade);
            _musicFade = StartCoroutine(CrossfadeMusicRoutine(clip, volume, fadeOut, fadeIn));
        }

        private IEnumerator CrossfadeMusicRoutine(AudioClip clip, float volume, float fadeOut, float fadeIn)
        {
            float start = _musicSource.isPlaying ? _musicSource.volume : 0f;
            for (float t = 0f; t < fadeOut && start > 0f; t += Time.unscaledDeltaTime)
            {
                _musicSource.volume = Mathf.Lerp(start, 0f, t / fadeOut);
                yield return null;
            }
            _musicSource.Stop();
            _musicSource.clip = clip;
            _musicSource.volume = 0f;
            _musicSource.Play();
            for (float t = 0f; t < fadeIn; t += Time.unscaledDeltaTime)
            {
                _musicSource.volume = Mathf.Lerp(0f, volume, t / fadeIn);
                yield return null;
            }
            _musicSource.volume = volume;
            _musicFade = null;
        }

        private IEnumerator StarWarsInvasionSequenceRoutine()
        {
            CrossfadeMusic(invasionMusic != null ? invasionMusic : combatMusic, 0.5f);
            // ════════════════════════════════════════════════════════════════════════
            // SHOT 5: 2 DREADNOUGHT CAPITAL SHIPS PULL UP LIKE STAR WARS (5.5s)
            // ════════════════════════════════════════════════════════════════════════
            AddCameraTrauma(0.85f);

            if (starDestroyerFleet != null)
            {
                starDestroyerFleet.StartFleetDescentSequence(2.5f);
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
            float dur5 = 2.2f; // Hyper-kinetic dreadnought breach

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
            // SHOT 6A: STARCRAFT DROP POD ARMADA LANDING & PILOT PERIMETER (6.5s)
            // (Dominion Drop Pods Reference 1:34 - 1:45: Re-entry, Cabin Red-Alert,
            // Retro-Burn Cluster at Y<=14m, Bedrock Slam, and 12-Pilot Encirclement)
            // ════════════════════════════════════════════════════════════════════════
            // Boss Mech remains in high orbit / hidden until Shot 6C
            if (bossMech != null) SetActorRenderersActive(bossMech, false);

            TriggerCockpitTransmission(
                "TACTICAL SENSORS",
                "ORBITAL THREAT RADAR",
                "ATMOSPHERIC DROP POD ARMADA DETECTED! Retro-rockets firing at low altitude!",
                4.0f
            );

            // Launch drop pod armada descent routine
            if (armyManager != null)
            {
                StartCoroutine(armyManager.PerformStarCraftDropPodArmadaRoutine(AlitaCombatStartPos, onPodsLanded: () => {
                    AddCameraTrauma(1.0f);
                }, onArmyFormed: () => {
                    if (armyManager != null && alitaHero != null)
                    {
                        armyManager.StartArmyFiringSequence(alitaHero.transform);
                    }
                }));
            }

            Vector3 camStart6A = new Vector3(4.5f, 1.8f, 66f);
            Vector3 camMid6A   = new Vector3(8.5f, 2.2f, 75f);
            Vector3 camEnd6A   = new Vector3(5.2f, 1.8f, 74.5f);

            float t6A = 0f;
            float dur6A = 2.4f; // StarCraft drop pod speed (1:34-1:45)
            bool pilotsAlerted = false;

            while (t6A < dur6A)
            {
                t6A += Time.deltaTime;

                // Phase 1 (0.0s - 2.2s): Worm's-eye skyward tracking of blazing drop pods
                if (t6A < 1.0f)
                {
                    _isShowingInteriorCabin = false;
                    if (mainCamera != null)
                    {
                        mainCamera.transform.position = ApplyCameraShake(camStart6A);
                        Vector3 lookSkyward = AlitaCombatStartPos + new Vector3(-8f, 45f, 12f);
                        mainCamera.transform.rotation = Quaternion.LookRotation(lookSkyward - camStart6A);
                        mainCamera.fieldOfView = Mathf.Lerp(65f, 58f, t6A / 1.0f);
                    }
                }
                // Phase 2 (2.2s - 3.8s): Cut to Drop Pod Interior Cabin Red Alert Viewport
                else if (t6A >= 1.0f && t6A < 1.6f)
                {
                    _isShowingInteriorCabin = true;
                    AddCameraTrauma(0.35f);

                    if (mainCamera != null)
                    {
                        mainCamera.transform.position = ApplyCameraShake(camStart6A + Vector3.up * 0.1f);
                        mainCamera.fieldOfView = 50f;
                    }
                }
                // Phase 3 (3.8s - 6.5s): Exterior ground slam, retro cutoff, ramp blowout, pilot perimeter
                else
                {
                    _isShowingInteriorCabin = false;
                    float subNorm = (t6A - 1.6f) / (dur6A - 1.6f);

                    if (!pilotsAlerted && t6A >= 1.7f)
                    {
                        pilotsAlerted = true;
                        TriggerCockpitTransmission(
                            "PILOT COMMANDER",
                            "EXPEDITIONARY FORCE",
                            "INTRUDER LOCATED! Surround her! Lock arc rifles and fire at will!",
                            3.5f
                        );
                    }

                    if (mainCamera != null)
                    {
                        Vector3 camPos = Vector3.Lerp(camMid6A, camEnd6A, subNorm);
                        mainCamera.transform.position = ApplyCameraShake(camPos);
                        mainCamera.transform.rotation = Quaternion.LookRotation(AlitaCombatStartPos + Vector3.up * 1.2f - camPos);
                        mainCamera.fieldOfView = Mathf.Lerp(58f, 54f, subNorm);
                    }
                }

                yield return null;
            }

            _isShowingInteriorCabin = false;

            // ════════════════════════════════════════════════════════════════════════
            // SHOT 6B: ALITA 360° ANTIMATTER CATACLYSM CLEAVE — ONE-SHOT OBLITERATION (4.5s)
            // (Alita vaporizes all 12 pilots into Euphoria physics ragdolls across the red dunes)
            // ════════════════════════════════════════════════════════════════════════
            Vector3 camStart6B = new Vector3(-2.4f, 1.3f, 71.5f);
            Vector3 camEnd6B   = new Vector3(-4.0f, 1.9f, 69.8f);

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

            TriggerTacticalAlert("[!] ALL 12 ESCORT PILOTS OBLITERATED // BERSERKER DISCHARGE 100% [!]", 2.0f);

            float t6B = 0f;
            float dur6B = 1.5f; // Fast-cut hit-stop cleave

            while (t6B < dur6B)
            {
                t6B += Time.deltaTime;
                float norm = Mathf.Clamp01(t6B / dur6B);

                if (mainCamera != null)
                {
                    Vector3 camPos = Vector3.Lerp(camStart6B, camEnd6B, norm);
                    mainCamera.transform.position = ApplyCameraShake(camPos);
                    mainCamera.transform.rotation = Quaternion.LookRotation(AlitaCombatStartPos + Vector3.up * 1.2f - camPos);
                    mainCamera.fieldOfView = Mathf.Lerp(52f, 62f, norm);
                }

                yield return null;
            }

            // ════════════════════════════════════════════════════════════════════════
            // SHOT 6C: TITANFALL MECH BOSS ENTRY (9.5s)
            // (Titanfall Cinematic Reference 0:00 - 0:10:
            // Beat 1: Skyward Camera Tilt & Sonic Boom Re-Entry Streak (0.0s - 2.0s)
            // Beat 2: Mid-Air Decoupling & Pod Shell Radial Ejection (2.0s - 3.8s)
            // Beat 3: 3-Point Superhero Seismic Ground Impact & Billowing Dust (3.8s - 5.2s)
            // Beat 4: Hydraulic Stand-Up & Dorsal Steam Blasts (5.2s - 7.5s)
            // Beat 5: Visor Crimson Flare to 6.5f, Hammer Overdrive & Health Bar (7.5s - 9.5s))
            // ════════════════════════════════════════════════════════════════════════
            TriggerCockpitTransmission(
                "TACTICAL SENSORS",
                "ORBITAL THREAT RADAR",
                "SEISMIC MASS DETECTED! Orbital Titan drop inbound at Mach 4!",
                2.8f
            );
            CrossfadeMusic(combatMusic, 0.62f, 0.6f, 1.2f);

            // Spawn Titan drop capsule high in the Martian stratosphere
            Vector3 bossDropStartPos = BossCombatStartPos + Vector3.up * 180f;
            GameObject bossCapsule = CreateBossDropCapsule(bossDropStartPos);

            if (_sfxSource != null)
            {
                _sfxSource.PlayOneShot(MechAudioSynth.GetThrusterIgnition(), 0.8f);
            }

            Vector3 camSkyPos      = AlitaCombatStartPos + new Vector3(2.5f, 1.2f, -1.5f);
            Vector3 camImpactStart = BossCombatStartPos + new Vector3(3.2f, 1.1f, -9.0f);
            Vector3 camImpactEnd   = BossCombatStartPos + new Vector3(1.6f, 1.6f, -6.5f);

            bool decoupled = false;
            bool groundImpacted = false;
            bool stoodUp = false;
            bool visorIgnited = false;

            float t6C = 0f;
            float dur6C = 5.2f; // Hyper-kinetic Titanfall pacing (0:00-0:10)

            while (t6C < dur6C)
            {
                t6C += Time.deltaTime;

                // ── BEAT 1: Skyward Camera Tilt & Supersonic Streak (0.0s - 2.0s) ──
                if (t6C < 1.0f)
                {
                    float b1Norm = t6C / 1.0f;
                    if (bossCapsule != null)
                    {
                        float curY = Mathf.Lerp(180f, 65f, b1Norm);
                        bossCapsule.transform.position = BossCombatStartPos + Vector3.up * curY;
                    }

                    if (mainCamera != null)
                    {
                        mainCamera.transform.position = ApplyCameraShake(camSkyPos);
                        Vector3 lookSky = (bossCapsule != null ? bossCapsule.transform.position : (BossCombatStartPos + Vector3.up * 100f));
                        mainCamera.transform.rotation = Quaternion.Slerp(
                            Quaternion.LookRotation(AlitaCombatStartPos + Vector3.up * 1.2f - camSkyPos),
                            Quaternion.LookRotation(lookSky - camSkyPos),
                            Mathf.SmoothStep(0f, 1f, b1Norm * 1.5f)
                        );
                        mainCamera.fieldOfView = Mathf.Lerp(60f, 52f, b1Norm);
                    }
                }
                // ── BEAT 2: Mid-Air Decoupling & Free-Fall Dive (2.0s - 3.8s) ──
                else if (t6C >= 1.0f && t6C < 2.0f)
                {
                    float b2Norm = (t6C - 1.0f) / 1.0f;

                    if (!decoupled)
                    {
                        decoupled = true;
                        Vector3 decouplePos = BossCombatStartPos + Vector3.up * 65f;
                        SpawnMidAirPodSeparationFX(decouplePos);

                        if (bossCapsule != null)
                        {
                            Destroy(bossCapsule);
                            bossCapsule = null;
                        }

                        // Reveal Boss Mech in mid-air dive posture
                        if (bossMech != null)
                        {
                            bossMech.transform.position = decouplePos;
                            bossMech.transform.rotation = BossCombatStartRot;
                            SetActorRenderersActive(bossMech, true);
                            if (_bossAnimator != null)
                            {
                                _bossAnimator.Play("Jump-Attack1", 0, 0.25f);
                            }
                        }

                        if (_sfxSource != null)
                        {
                            _sfxSource.PlayOneShot(MechAudioSynth.GetPneumaticRelease(), 0.8f);
                        }
                    }

                    // Boss Mech free-falls rapidly toward Martian bedrock
                    if (bossMech != null)
                    {
                        float diveY = Mathf.Lerp(65f, BossCombatStartPos.y, b2Norm * b2Norm);
                        bossMech.transform.position = new Vector3(BossCombatStartPos.x, diveY, BossCombatStartPos.z);
                    }

                    // Camera tilts downward following the plunging Titan
                    if (mainCamera != null && bossMech != null)
                    {
                        Vector3 camPos = Vector3.Lerp(camSkyPos, camImpactStart, b2Norm);
                        mainCamera.transform.position = ApplyCameraShake(camPos);
                        mainCamera.transform.rotation = Quaternion.LookRotation((bossMech.transform.position + Vector3.up * 2f) - camPos);
                        mainCamera.fieldOfView = Mathf.Lerp(52f, 48f, b2Norm);
                    }
                }
                // ── BEAT 3: 3-Point Superhero Seismic Ground Impact (3.8s - 5.2s) ──
                else if (t6C >= 2.0f && t6C < 3.0f)
                {
                    float b3Norm = (t6C - 2.0f) / 1.0f;

                    if (!groundImpacted)
                    {
                        groundImpacted = true;

                        if (bossMech != null)
                        {
                            bossMech.transform.position = BossCombatStartPos;
                            bossMech.transform.rotation = BossCombatStartRot;
                            if (_bossAnimator != null)
                            {
                                _bossAnimator.CrossFadeInFixedTime("GroundSlam", 0.04f);
                            }
                        }

                        if (_sfxSource != null)
                        {
                            _sfxSource.PlayOneShot(MechAudioSynth.GetSeismicSlam(), 1.0f);
                            _sfxSource.PlayOneShot(MechAudioSynth.GetImpactHeavy(), 0.5f);
                        }

                        // Seismic camera trauma (1.25f)
                        AddCameraTrauma(1.25f);

                        // Billowing 360° Martian dust shockwave ring
                        if (redDustTouchdownFX != null)
                        {
                            redDustTouchdownFX.transform.position = BossCombatStartPos;
                            redDustTouchdownFX.Play();
                        }
                        if (armyManager != null)
                        {
                            armyManager.SpawnPodLandingDustRing(BossCombatStartPos);
                        }
                    }

                    // Hold low-angle ground camera on the 3-point crouch
                    if (mainCamera != null)
                    {
                        mainCamera.transform.position = ApplyCameraShake(camImpactStart);
                        Vector3 lookAtCrouch = BossCombatStartPos + Vector3.up * 2.2f;
                        mainCamera.transform.rotation = Quaternion.LookRotation(lookAtCrouch - camImpactStart);
                        mainCamera.fieldOfView = 48f;
                    }
                }
                // ── BEAT 4: Hydraulic Stand-Up & Dorsal Steam Blasts (5.2s - 7.5s) ──
                else if (t6C >= 3.0f && t6C < 4.1f)
                {
                    float b4Norm = (t6C - 3.0f) / 1.1f;

                    if (!stoodUp)
                    {
                        stoodUp = true;

                        if (_bossAnimator != null)
                        {
                            _bossAnimator.CrossFadeInFixedTime("ReactorPowerUp", 0.15f);
                        }

                        if (hydraulicSteamFX != null)
                        {
                            hydraulicSteamFX.transform.position = BossCombatStartPos + Vector3.up * 5.2f;
                            hydraulicSteamFX.Play();
                        }

                        if (_sfxSource != null)
                        {
                            _sfxSource.PlayOneShot(MechAudioSynth.GetPneumaticRelease(), 0.7f);
                            _sfxSource.PlayOneShot(MechAudioSynth.GetServoWhine(), 0.6f);
                        }
                    }

                    // Camera slowly trucks forward and pans up framing the rising 7m titan
                    if (mainCamera != null)
                    {
                        Vector3 camPos = Vector3.Lerp(camImpactStart, camImpactEnd, b4Norm);
                        mainCamera.transform.position = ApplyCameraShake(camPos);
                        Vector3 lookTarget = BossCombatStartPos + Vector3.up * Mathf.Lerp(2.2f, 5.2f, b4Norm);
                        mainCamera.transform.rotation = Quaternion.LookRotation(lookTarget - camPos);
                        mainCamera.fieldOfView = Mathf.Lerp(48f, 44f, b4Norm);
                    }
                }
                // ── BEAT 5: Crimson Visor Flare, Hammer Thruster Ignition & Boss Health Bar (7.5s - 9.5s) ──
                else
                {
                    float b5Norm = (t6C - 4.1f) / 1.1f;

                    if (!visorIgnited)
                    {
                        visorIgnited = true;

                        if (bossEyeVisorLight != null)
                        {
                            bossEyeVisorLight.color = new Color(1.0f, 0.08f, 0.08f);
                            bossEyeVisorLight.intensity = 8.5f;
                        }

                        if (_sfxSource != null)
                        {
                            _sfxSource.PlayOneShot(MechAudioSynth.GetOverdriveAlarm(), 0.6f);
                        }

                        if (_bossAI != null && _bossAI.hammerBooster != null)
                        {
                            _bossAI.hammerBooster.SetState(BoosterFlameState.Overdrive);
                        }

                        if (_bossAnimator != null)
                        {
                            _bossAnimator.CrossFadeInFixedTime("Idle", 0.35f);
                        }

                        TriggerTacticalAlert("[!] GOLIATH-06 TITAN WAR MECH ENGAGED // THREAT CLASS: EXTINCTION [!]", 3.0f);

                        if (_bossHealthBar != null)
                        {
                            _bossHealthBar.enabled = true;
                            _bossHealthBar.bossName = "COLOSSUS-06 // TITAN WAR MECH";
                            _bossHealthBar.bossSub = "HEAVY KINETIC SIEGE UNIT // URM EXCAVATION GUARDIAN";
                        }
                    }

                    if (mainCamera != null)
                    {
                        mainCamera.transform.position = ApplyCameraShake(camImpactEnd);
                        Vector3 lookTarget = BossCombatStartPos + Vector3.up * 5.2f;
                        mainCamera.transform.rotation = Quaternion.LookRotation(lookTarget - camImpactEnd);
                        mainCamera.fieldOfView = Mathf.Lerp(44f, 46f, b5Norm);
                    }
                }

                yield return null;
            }

            if (bossCapsule != null) Destroy(bossCapsule);
            // ════════════════════════════════════════════════════════════════════════
            // TRANSITION: SEAMLESS HAND-OFF TO GAMEPLAY COMBAT
            // ════════════════════════════════════════════════════════════════════════
            yield return StartCoroutine(SwitchbladeRevealDialogue());
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
            _isShowingInteriorCabin = false;
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
            _isShowingInteriorCabin = false;
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
            // Fade the beds out instead of cutting them, so the hand-off has no click
            if (_ambientSource != null) StartCoroutine(MarsSfx.FadeOutAndStop(_ambientSource, 1.5f));
            if (starDestroyerFleet != null) starDestroyerFleet.FadeOutAudio(2.0f);

            if (_musicSource != null && combatMusic != null)
            {
                if (_musicFade != null) { StopCoroutine(_musicFade); _musicFade = null; }
                if (_musicSource.clip != combatMusic || !_musicSource.isPlaying)
                {
                    _musicSource.clip = combatMusic;
                    _musicSource.Play();
                }
                _musicSource.volume = 0.62f;
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

        private float _lastChirpTime = -10f;

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

            // One soft chirp per transmission, and never two inside the same second
            if (_voiceSource != null && commsChirpAudio != null && Time.unscaledTime - _lastChirpTime > 1.0f)
            {
                _lastChirpTime = Time.unscaledTime;
                _voiceSource.PlayOneShot(commsChirpAudio, 0.3f);
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
            DrawArrivalOverlay();
            if (!isPlaying && string.IsNullOrEmpty(_tacticalAlertText)) return;

            InitGUIStyles();

            // 1. Cockpit Radio Comms Transmission Card rendered via AAA CyberpunkDialogueHUD

            // 3. StarCraft Drop Pod Interior Cockpit Red-Alert Viewport
            if (_isShowingInteriorCabin)
            {
                DrawInteriorDropPodHUD();
            }

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
    
        #region Drop Pod & Titanfall Helpers
        private GameObject CreateBossDropCapsule(Vector3 spawnPos)
        {
            GameObject pod = null;
            if (armyManager != null && armyManager.dropPodPrefab != null)
            {
                pod = Instantiate(armyManager.dropPodPrefab, spawnPos, Quaternion.Euler(180f, 0f, 0f));
                pod.transform.localScale = Vector3.one * 2.2f;
            }
            else
            {
                pod = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pod.transform.position = spawnPos;
                pod.transform.localScale = new Vector3(3.5f, 6.0f, 3.5f);
                var c = pod.GetComponent<Collider>();
                if (c != null) Destroy(c);
                var ren = pod.GetComponent<Renderer>();
                if (ren != null) ren.material.color = new Color(0.18f, 0.2f, 0.24f);
            }
            pod.name = "Titanfall_BossDropCapsule";

            var cols = pod.GetComponentsInChildren<Collider>();
            foreach (var col in cols) Destroy(col);

            var lObj = new GameObject("Reentry_Flame_Light");
            lObj.transform.SetParent(pod.transform, false);
            lObj.transform.localPosition = new Vector3(0f, -2.5f, 0f);
            var light = lObj.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1.0f, 0.5f, 0.1f);
            light.range = 45f;
            light.intensity = 16f;

            if (armyManager != null && armyManager.thrusterFlameFX != null)
            {
                var fx = Instantiate(armyManager.thrusterFlameFX, lObj.transform);
                fx.transform.localPosition = Vector3.zero;
                fx.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                fx.transform.localScale = Vector3.one * 2.5f;
            }

            return pod;
        }

        private void SpawnMidAirPodSeparationFX(Vector3 separationPos)
        {
            GameObject flashGO = new GameObject("Decoupler_Flash");
            flashGO.transform.position = separationPos;
            var l = flashGO.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1.0f, 0.85f, 0.4f);
            l.range = 35f;
            l.intensity = 18f;
            Destroy(flashGO, 0.45f);

            Vector3[] radialDirs = new Vector3[]
            {
                new Vector3(1f, 0.25f, 0.5f).normalized,
                new Vector3(-1f, 0.25f, 0.5f).normalized,
                new Vector3(0.5f, 0.25f, -1f).normalized,
                new Vector3(-0.5f, 0.25f, -1f).normalized
            };

            for (int i = 0; i < radialDirs.Length; i++)
            {
                GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                panel.name = $"BossPod_ShellPanel_{i + 1}";
                panel.transform.position = separationPos + radialDirs[i] * 1.8f;
                panel.transform.localScale = new Vector3(1.4f, 2.8f, 0.35f);

                var col = panel.GetComponent<Collider>();
                if (col != null) Destroy(col);

                var ren = panel.GetComponent<Renderer>();
                if (ren != null)
                {
                    ren.material.color = new Color(0.2f, 0.22f, 0.25f);
                }

                var rb = panel.AddComponent<Rigidbody>();
                rb.mass = 45f;
                rb.linearVelocity = radialDirs[i] * UnityEngine.Random.Range(24f, 34f) + Vector3.up * UnityEngine.Random.Range(4f, 10f);
                rb.angularVelocity = UnityEngine.Random.insideUnitSphere * 18f;

                Destroy(panel, 4.5f);
            }

            if (hydraulicSteamFX != null)
            {
                var ps = Instantiate(hydraulicSteamFX, separationPos, Quaternion.identity);
                ps.transform.localScale = Vector3.one * 2.5f;
                ps.Play();
                Destroy(ps.gameObject, 3.5f);
            }
        }

        private void DrawBorder(Rect rect, int thickness)
        {
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);
        }

        private void DrawInteriorDropPodHUD()
        {
            float sw = Screen.width;
            float sh = Screen.height;

            float pulse = 0.45f + 0.35f * Mathf.Sin(Time.time * 9f);
            Color redVignette = new Color(0.95f, 0.06f, 0.06f, pulse);
            Color prevColor = GUI.color;

            // Letterbox Bars
            GUI.color = new Color(0.02f, 0.02f, 0.04f, 0.88f);
            GUI.DrawTexture(new Rect(0, 0, sw, 55f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, sh - 55f, sw, 55f), Texture2D.whiteTexture);

            // Red border warning strobes
            GUI.color = redVignette;
            GUI.DrawTexture(new Rect(0, 52f, sw, 3f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, sh - 55f, sw, 3f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, 0, 16f, sh), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(sw - 16f, 0, 16f, sh), Texture2D.whiteTexture);

            // Top Warning Header
            var warnStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15,
                fontStyle = FontStyle.Bold
            };
            warnStyle.normal.textColor = new Color(1.0f, 0.2f, 0.2f);
            GUI.color = Color.white;
            GUI.Label(new Rect(0, 12f, sw, 30f), "[!] WARNING: ATMOSPHERIC DROP DESCENT // RE-ENTRY IN PROGRESS [!]", warnStyle);

            // Left Flight Telemetry Card
            float cardW = 340f;
            float cardH = 140f;
            float cardY = sh * 0.32f;

            GUI.color = new Color(0.04f, 0.06f, 0.10f, 0.85f);
            GUI.DrawTexture(new Rect(36f, cardY, cardW, cardH), Texture2D.whiteTexture);
            GUI.color = new Color(1.0f, 0.2f, 0.2f, 0.9f);
            DrawBorder(new Rect(36f, cardY, cardW, cardH), 1);

            var telemetryHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };
            telemetryHeaderStyle.normal.textColor = new Color(1.0f, 0.5f, 0.2f);
            GUI.Label(new Rect(48f, cardY + 8f, cardW - 24f, 18f), "DOMINION DROP POD // FLIGHT TELEMETRY", telemetryHeaderStyle);

            var telemetryBodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Normal
            };
            telemetryBodyStyle.normal.textColor = new Color(0.92f, 0.94f, 0.98f);
            string telemetryText = "CABIN DEPRESSURIZED: 0.10 ATM\n" +
                                  "DESCENT VELOCITY: MACH 2.8\n" +
                                  "AIRFRAME HEAT: 1,840°C [TILES OK]\n" +
                                  "ALTITUDE: 2,850M -> SLAM T-3.0S";
            GUI.Label(new Rect(48f, cardY + 30f, cardW - 24f, 95f), telemetryText, telemetryBodyStyle);

            // Right Squadron Status Card
            float rCardX = sw - cardW - 36f;
            GUI.color = new Color(0.04f, 0.06f, 0.10f, 0.85f);
            GUI.DrawTexture(new Rect(rCardX, cardY, cardW, cardH), Texture2D.whiteTexture);
            GUI.color = new Color(0.1f, 0.9f, 1.0f, 0.9f);
            DrawBorder(new Rect(rCardX, cardY, cardW, cardH), 1);

            telemetryHeaderStyle.normal.textColor = new Color(0.1f, 0.9f, 1.0f);
            GUI.Label(new Rect(rCardX + 12f, cardY + 8f, cardW - 24f, 18f), "SQUADRON STATUS // PILOT DIVISION", telemetryHeaderStyle);

            string squadText = "PILOT LINK: 12/12 SYNCHRONIZED\n" +
                              "HELMET VISORS: OPTICAL LOCK\n" +
                              "RETRO-CLUSTER: ARMED (Y <= 14M)\n" +
                              ">> RETRO IGNITION T-MINUS 1.8S <<";
            GUI.Label(new Rect(rCardX + 12f, cardY + 30f, cardW - 24f, 95f), squadText, telemetryBodyStyle);

            // Bottom Mission Directive
            var botStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };
            botStyle.normal.textColor = new Color(1.0f, 0.85f, 0.2f);
            GUI.Label(new Rect(0, sh - 45f, sw, 30f), "MISSION DIRECTIVE: DEPLOY PERIMETER AROUND BERSERKER HOSTILE // LOCK ARC RIFLES", botStyle);

            GUI.color = prevColor;
        }
        #endregion

    }
}
