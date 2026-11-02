using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using PolygonMech;
using InsaneGun.Combat;
using Alita.UI;
using SyntyCombined.Cutscene;
using SyntyCombined;
using Cyberpunk.World;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Cyberpunk.Mars
{
    /// <summary>
    /// Master Encounter Director for the Final Martian Boss Battle.
    /// Manages:
    /// - Cinematic Title Card Intro: "MARS // CYDONIA BASIN // TITAN SIEGE"
    /// - Boss state HUD warnings (Cyclone Spin, Seismic Quake, Hydraulic Punish Window)
    /// - Dynamic Combat BGM and Martian dust storm audio
    /// - Camera Shake on heavy hammer slams and shockwaves
    /// - Victory condition and cinematic outro
    /// - Boss Defeat Slow-Motion Impact
    /// - Alita Victory Flourish & Blade Sheathe Animation with SFX
    /// - Emotional 3-Shot Cinematic Cutscene: Alita avenging Dr. Ido (with Ido memory portrait & voice)
    /// - Interactive Cyberpunk Option to Travel Back to Cyber City (Demo_City)
    /// - Hyperspace Warp Whiteout Transition landing Alita safely in Cyber City plaza
    /// </summary>
    public class MarsFinalBattleDirector : MonoBehaviour
    {
        public static MarsFinalBattleDirector Instance { get; private set; }

        [Header("Combatants")]
        public HammerMechAI bossAI;
        public Transform playerTransform;

        [Header("Audio & Ambience")]
        public AudioClip combatMusic;
        public AudioClip dustStormAmbient;
        public AudioClip sfxAlarmWarning;
        private AudioSource musicSource;
        private AudioSource ambientSource;
        private AudioSource sfxSource;

        [Header("Atmospheric Controls")]
        public ParticleSystem redDustStormFX;
        public Light martianSunLight;

        [Header("Victory & Outro Sequence")]
        public Transform starshipTransform;
        public AnimationClip victoryFlourishClip;
        public AudioClip victoryMusic;
        public AudioClip warpSFX;
        public AudioClip bladeSheatheSFX;
        public AudioClip idoMemoryVoiceClip;
        public Sprite idoPortrait;
        public Sprite alitaPortrait;

        // UI State
        private float introTimer = 0f;
        private const float IntroDuration = 5.0f;
        private bool isIntroActive = true;
        private string tacticalAlertText = "";
        private float tacticalAlertTimer = 0f;
        private MechCombatState lastLoggedBossState = MechCombatState.Spawning;

        // Outro & Travel State
        private bool _isVictoryTriggered = false;
        private bool _isCutscenePlaying = false;
        private bool _isTravelPromptActive = false;
        private bool _isPersistentPromptActive = false;
        private bool _isTransitioningToCity = false;
        private float _warpFlashAlpha = 0f;
        private bool _isNearStarship = false;

        // Runtime Components
        private InsaneGunSwordController _alitaController;
        private CharacterController _alitaCharController;
        private Animator _alitaAnimator;
        private Camera _mainCamera;
        private SmoothOrbitCamera _orbitCamera;

        // GUI Styles
        private GUIStyle titleHeaderStyle;
        private GUIStyle subHeaderStyle;
        private GUIStyle missionBriefStyle;
        private GUIStyle tacticalAlertStyle;
        private GUIStyle boxBackdropStyle;
        private GUIStyle modalTitleStyle;
        private GUIStyle modalBodyStyle;
        private GUIStyle btnTravelStyle;
        private GUIStyle btnStayStyle;
        private GUIStyle persistentPromptStyle;
        private bool stylesInitialized = false;

        private void Awake()
        {
            Instance = this;

            // Audio Sources
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.volume = 0.55f;

            ambientSource = gameObject.AddComponent<AudioSource>();
            ambientSource.loop = true;
            ambientSource.playOnAwake = false;
            ambientSource.volume = 0.35f;

            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
        }

        private void Start()
        {
            if (bossAI == null) bossAI = FindAnyObjectByType<HammerMechAI>();
            if (playerTransform == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) playerTransform = player.transform;
            }

#if UNITY_EDITOR
            if (combatMusic == null)
            {
                combatMusic = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/Cyberpunk_Combat_Metal_Heart.mp3");
            }
#endif
            CachePlayerAndCameraComponents();
            FindStarshipReference();
            LoadEditorAssets();

            if (sfxAlarmWarning == null)
            {
                sfxAlarmWarning = MechAudioSynth.GetOverdriveAlarm();
            }

            // Check if Mars Arrival Cutscene Director is active
            var cutscene = MarsArrivalCutsceneDirector.Instance ?? FindFirstObjectByType<MarsArrivalCutsceneDirector>();
            if (cutscene != null && (cutscene.playOnStart || MarsArrivalCutsceneDirector.ForcePlayCutsceneOnSceneLoad))
            {
                isIntroActive = false;
                introTimer = 0f;
                return;
            }

            StartCombatEncounter();
        }

        private void CachePlayerAndCameraComponents()
        {
            if (playerTransform != null)
            {
                _alitaController = playerTransform.GetComponent<InsaneGunSwordController>();
                _alitaCharController = playerTransform.GetComponent<CharacterController>();
                _alitaAnimator = playerTransform.GetComponent<Animator>();
            }

            if (_mainCamera == null)
            {
                var camObj = GameObject.FindGameObjectWithTag("MainCamera");
                if (camObj != null) _mainCamera = camObj.GetComponent<Camera>();
                if (_mainCamera == null) _mainCamera = Camera.main;
            }

            if (_mainCamera != null)
            {
                _orbitCamera = _mainCamera.GetComponent<SmoothOrbitCamera>();
            }
        }

        private void FindStarshipReference()
        {
            if (starshipTransform == null)
            {
                var shipObj = GameObject.Find("Alita_Martian_Starship") ??
                              GameObject.Find("Alita_Lost_Martian_Starship") ??
                              GameObject.Find("Starship") ??
                              GameObject.Find("SS-04_Nightshade_Striker");
                if (shipObj != null) starshipTransform = shipObj.transform;
                else if (MarsArrivalCutsceneDirector.Instance != null && MarsArrivalCutsceneDirector.Instance.starship != null)
                {
                    starshipTransform = MarsArrivalCutsceneDirector.Instance.starship.transform;
                }
            }
        }

        private void LoadEditorAssets()
        {
#if UNITY_EDITOR
            if (combatMusic == null)
            {
                combatMusic = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/Cyberpunk_Combat_Metal_Heart.mp3");
            }
            if (victoryFlourishClip == null)
            {
                var assets = AssetDatabase.LoadAllAssetsAtPath("Assets/Synty/AnimationSwordCombat/Animations/Polygon/Idle/Fidgets/A_Idle_Flourish01_Sword.fbx");
                if (assets != null)
                {
                    foreach (var obj in assets)
                    {
                        if (obj is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                        {
                            victoryFlourishClip = clip;
                            break;
                        }
                    }
                }
            }
            if (bladeSheatheSFX == null)
            {
                bladeSheatheSFX = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Combat/Sword_Slash_Light_01.wav");
            }
            if (warpSFX == null)
            {
                warpSFX = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Combat/SciFi_Starship_Flyby_Pass.mp3");
            }
            if (victoryMusic == null)
            {
                victoryMusic = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/Mars_PostLanding_Theme.mp3");
                if (victoryMusic == null)
                {
                    victoryMusic = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/Tears_in_the_Rain.mp3");
                }
            }
            if (idoMemoryVoiceClip == null)
            {
                idoMemoryVoiceClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Voice/Ido/ido_mission1_complete.mp3");
            }
            if (idoPortrait == null)
            {
                idoPortrait = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/Dialogue/T_DoctorIdo_Dialogue_Portrait.png");
            }
            if (alitaPortrait == null)
            {
                alitaPortrait = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/Dialogue/T_Alita_Dialogue_Portrait.png");
            }
#endif
        }

        public void StartCombatEncounter()
        {
            // The arrival/invasion director owns the music (crossfaded); this track is only for scenes without it
            bool cutsceneOwnsMusic = MarsArrivalCutsceneDirector.Instance != null;
            if (!cutsceneOwnsMusic && combatMusic != null && !musicSource.isPlaying)
            {
                musicSource.clip = combatMusic;
                musicSource.Play();
            }

            if (dustStormAmbient != null && !ambientSource.isPlaying)
            {
                ambientSource.clip = dustStormAmbient;
                ambientSource.Play();
            }

            introTimer = IntroDuration;
            isIntroActive = true;
        }

        private void Update()
        {
            if (isIntroActive)
            {
                introTimer -= Time.deltaTime;
                if (introTimer <= 0f) isIntroActive = false;
            }

            if (tacticalAlertTimer > 0f)
            {
                tacticalAlertTimer -= Time.deltaTime;
                if (tacticalAlertTimer <= 0f) tacticalAlertText = "";
            }

            // Monitor Boss State for Tactical HUD Alerts
            if (bossAI != null && bossAI.currentState != lastLoggedBossState)
            // Monitor Boss State for Tactical HUD Alerts & Victory Condition
            if (bossAI != null)
            {
                lastLoggedBossState = bossAI.currentState;
                OnBossStateChanged(lastLoggedBossState);
                if (bossAI.currentState != lastLoggedBossState)
                {
                    lastLoggedBossState = bossAI.currentState;
                    OnBossStateChanged(lastLoggedBossState);
                }

                // Check for Boss Defeat condition
                if (!_isVictoryTriggered && (bossAI.currentState == MechCombatState.Dead || bossAI.currentHealth <= 0.01f))
                {
                    StartVictorySequence();
                }
            }

            // Developer Quick-Test Key (F8): Immediately trigger the Avenged Ido sequence
            if (!_isVictoryTriggered && Input.GetKeyDown(KeyCode.F8))
            {
                Debug.Log("[MarsFinalBattleDirector] F8 Pressed: Triggering Boss Defeat & Avenged Dr. Ido Sequence!");
                if (bossAI != null) bossAI.TakeDamage(99999f, bossAI.transform.position, Vector3.up, true);
                else StartVictorySequence();
            }

            // Starship Proximity Monitoring
            if (starshipTransform != null && playerTransform != null)
            {
                float dist = Vector3.Distance(playerTransform.position, starshipTransform.position);
                _isNearStarship = (dist < 14f);
            }

            // Keyboard Shortcuts for Travel Option
            if (_isTravelPromptActive && !_isTransitioningToCity)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
                    Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E))
                {
                    TravelBackToCyberCity();
                }
                else if (Input.GetKeyDown(KeyCode.Escape))
                {
                    _isTravelPromptActive = false;
                    _isPersistentPromptActive = true;
                }
            }
            else if (_isPersistentPromptActive && !_isTransitioningToCity)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    TravelBackToCyberCity();
                }
                else if (_isNearStarship && Input.GetKeyDown(KeyCode.E))
                {
                    TravelBackToCyberCity();
                }
            }
        }

        private void OnBossStateChanged(MechCombatState newState)
        {
            switch (newState)
            {
                case MechCombatState.SeismicQuake:
                    TriggerAlert("[!] SEISMIC WARNING: GROUND SHOCKWAVE PROPAGATING [!]", 2.2f, true);
                    break;
                case MechCombatState.LeapGroundSlam:
                    TriggerAlert("[!] BALLISTIC ALERT: ROCKET-ASSISTED LEAP SLAM [!]", 2.0f, true);
                    break;
                case MechCombatState.HydraulicVenting:
                    TriggerAlert("[VULNERABLE] HYDRAULIC OVERHEAT: VENTING STEAM - PUNISH NOW!", 1.5f, false);
                    break;
                case MechCombatState.DefensiveGuard:
                    TriggerAlert("[DEFENSE] HAMMER GUARD BRACED - PREPARE FOR COUNTER", 1.2f, false);
                    break;
                case MechCombatState.Staggered:
                    TriggerAlert("[SYSTEM FAULT] POISE BROKEN // MECH MALFUNCTIONING", 1.8f, false);
                    break;
                case MechCombatState.Dead:
                    TriggerAlert("TARGET DESTROYED // MARS CYDONIA EXCAVATION SECURED", 6.0f, false);
                    TriggerAlert("TARGET DESTROYED // COLOSSUS TITAN COLLAPSED", 4.0f, false);
                    break;
            }
        }

        public void TriggerAlert(string alert, float duration, bool playBeep)
        {
            tacticalAlertText = alert;
            tacticalAlertTimer = duration;
            if (playBeep && sfxAlarmWarning != null)
            {
                sfxSource.PlayOneShot(sfxAlarmWarning, 0.7f);
            }
        }

        public void StartVictorySequence()
        {
            if (_isVictoryTriggered) return;
            _isVictoryTriggered = true;
            StartCoroutine(ExecuteVictoryAndAvengedIdoSequence());
        }

        private IEnumerator ExecuteVictoryAndAvengedIdoSequence()
        {
            _isCutscenePlaying = true;
            tacticalAlertText = "";
            tacticalAlertTimer = 0f;

            // Re-ensure references
            CachePlayerAndCameraComponents();
            FindStarshipReference();

            // 1. Slow-Motion Heavy Impact Moment
            Time.timeScale = 0.35f;
            if (musicSource != null && musicSource.isPlaying)
            {
                StartCoroutine(FadeAudioSource(musicSource, 0f, 1.2f));
            }
            yield return new WaitForSecondsRealtime(1.4f);
            Time.timeScale = 1.0f;

            // 2. Atmospheric Transition: Quiet Martian dust storm & crossfade in Victory Theme
            var dustStorm = MartianDustStormSystem.Instance ?? FindFirstObjectByType<MartianDustStormSystem>();
            if (dustStorm != null)
            {
                dustStorm.SetStormIntensity(0.06f, 3.0f);
            }

            if (victoryMusic != null && musicSource != null)
            {
                musicSource.clip = victoryMusic;
                musicSource.volume = 0f;
                musicSource.loop = true;
                musicSource.Play();
                StartCoroutine(FadeAudioSource(musicSource, 0.55f, 2.0f));
            }

            // 3. Suspend Player Movement & Align for Victory Presentation
            if (_alitaController != null) _alitaController.enabled = false;
            if (_alitaCharController != null) _alitaCharController.SimpleMove(Vector3.zero);
            if (_orbitCamera != null) _orbitCamera.enabled = false;

            // Turn Alita forward/away from the fallen boss for a heroic composition
            if (playerTransform != null && bossAI != null)
            {
                Vector3 awayFromBoss = (playerTransform.position - bossAI.transform.position).normalized;
                awayFromBoss.y = 0f;
                if (awayFromBoss.sqrMagnitude > 0.01f)
                {
                    playerTransform.rotation = Quaternion.LookRotation(awayFromBoss);
                }
            }

            // 4. Play Alita Victory Flourish & Blade Sheathe Animation
            if (_alitaAnimator != null)
            {
                if (victoryFlourishClip != null)
                {
                    try
                    {
                        var overrideController = new AnimatorOverrideController(_alitaAnimator.runtimeAnimatorController);
                        overrideController["Attack_07"] = victoryFlourishClip;
                        _alitaAnimator.runtimeAnimatorController = overrideController;
                        _alitaAnimator.CrossFadeInFixedTime("Attack_07", 0.15f);
                    }
                    catch
                    {
                        _alitaAnimator.CrossFadeInFixedTime("Combo_03_Phase5", 0.15f);
                    }
                }
                else
                {
                    _alitaAnimator.CrossFadeInFixedTime("Combo_03_Phase5", 0.15f);
                }
            }

            // Play Metallic Blade Flourish / Sheathe SFX
            if (bladeSheatheSFX != null)
            {
                MarsSfx.Play(bladeSheatheSFX, 0.9f, 1.15f);
            }

            yield return new WaitForSecondsRealtime(2.0f);

            // 5. Cinematic Cutscene: Avenged Dr. Ido
            var titleCardUI = GotGTitleCardUI.GetOrCreate();
            if (titleCardUI != null)
            {
                titleCardUI.SetLetterboxOnly(true, 0.8f);
            }

            Camera cam = _mainCamera != null ? _mainCamera : Camera.main;
            Vector3 alitaPos = playerTransform != null ? playerTransform.position : Vector3.zero;
            Vector3 bossPos = bossAI != null ? bossAI.transform.position : (alitaPos + Vector3.forward * 8f);

            // --- SHOT 1: Low-Angle Hero Stance (Alita with smoking Boss Titan behind her) ---
            if (cam != null)
            {
                Vector3 shot1Dir = (bossPos - alitaPos).normalized;
                shot1Dir.y = 0f;
                if (shot1Dir.sqrMagnitude < 0.01f) shot1Dir = Vector3.forward;
                cam.transform.position = alitaPos - shot1Dir * 3.4f + Vector3.up * 1.05f;
                cam.transform.LookAt(alitaPos + Vector3.up * 1.35f);
                cam.fieldOfView = 50f;
            }

            bool shot1Complete = false;
            var dialogueHUD = CyberpunkDialogueHUD.GetOrCreate();
            dialogueHUD.ShowDialogue(
                "ALITA",
                "It's over, Ido. The titan that broke into your clinic... that took your life... is nothing but cold scrap iron now.",
                alitaPortrait,
                "URM BERSERKER",
                () => shot1Complete = true,
                5.2f
            );

            float waitTimer = 0f;
            while (!shot1Complete && waitTimer < 6.5f)
            {
                waitTimer += Time.unscaledDeltaTime;
                yield return null;
            }

            // --- SHOT 2: Close-up of Alita looking towards the celestial stars & Dr. Ido's memory ---
            if (cam != null)
            {
                Vector3 sideOffset = playerTransform != null ? (playerTransform.right * 1.15f - playerTransform.forward * 1.25f) : Vector3.right;
                cam.transform.position = alitaPos + sideOffset + Vector3.up * 1.45f;
                cam.transform.LookAt(alitaPos + Vector3.up * 1.42f);
                cam.fieldOfView = 42f;
            }

            bool shot2Complete = false;
            dialogueHUD.ShowDialogue(
                "DR. DYSON IDO",
                "You were never just a weapon, Alita. You have a human heart... Never let this world take that away from you.",
                idoPortrait,
                "CYBERNETICS SURGEON // MEMORY MATRIX",
                () => shot2Complete = true,
                5.8f,
                idoMemoryVoiceClip
            );

            waitTimer = 0f;
            while (!shot2Complete && waitTimer < 7.0f)
            {
                waitTimer += Time.unscaledDeltaTime;
                yield return null;
            }

            // --- SHOT 3: Wide Panoramic Martian Ridge & Waiting Starship ---
            Vector3 shipPos = starshipTransform != null ? starshipTransform.position : (alitaPos + Vector3.left * 22f);
            if (cam != null)
            {
                Vector3 midPoint = (alitaPos + shipPos) * 0.5f;
                cam.transform.position = midPoint + Vector3.up * 8.5f + Vector3.back * 18f;
                cam.transform.LookAt(alitaPos + Vector3.up * 1.2f);
                cam.fieldOfView = 65f;
            }

            if (titleCardUI != null)
            {
                titleCardUI.ShowTitleCard(
                    "★ MISSION COMPLETE // DR. IDO AVENGED ★",
                    "COLOSSUS-06 TITAN MECH NEUTRALIZED • ANTIMATTER CORE SECURED",
                    "MARS CYDONIA EXCAVATION LIBERATED • HYPERSPACE WARP READY",
                    6.5f
                );
            }

            bool shot3Complete = false;
            dialogueHUD.ShowDialogue(
                "ALITA",
                "Your legacy is safe, Doctor. I'm bringing your core home. Cyber City is waiting for me.",
                alitaPortrait,
                "URM BERSERKER",
                () => shot3Complete = true,
                4.8f
            );

            waitTimer = 0f;
            while (!shot3Complete && waitTimer < 5.8f)
            {
                waitTimer += Time.unscaledDeltaTime;
                yield return null;
            }

            // 6. Release Cutscene & Restore Player Gameplay
            dialogueHUD.CloseDialogue(0.3f);
            if (titleCardUI != null)
            {
                titleCardUI.SetLetterboxOnly(false, 0.6f);
            }

            if (cam != null) cam.fieldOfView = 60f;
            if (_orbitCamera != null)
            {
                _orbitCamera.enabled = true;
                if (playerTransform != null) _orbitCamera.target = playerTransform;
            }
            if (_alitaController != null) _alitaController.enabled = true;
            if (_alitaCharController != null) _alitaCharController.enabled = true;

            _isCutscenePlaying = false;

            // 7. Open Interactive Travel Option Prompt
            _isTravelPromptActive = true;
        }

        private IEnumerator FadeAudioSource(AudioSource src, float targetVol, float duration)
        {
            if (src == null) yield break;
            float startVol = src.volume;
            float t = 0f;
            while (t < duration && src != null)
            {
                t += Time.unscaledDeltaTime;
                src.volume = Mathf.Lerp(startVol, targetVol, t / duration);
                yield return null;
            }
            if (src != null)
            {
                src.volume = targetVol;
                if (targetVol <= 0.001f) src.Stop();
            }
        }

        public void TravelBackToCyberCity()
        {
            if (_isTransitioningToCity) return;
            StartCoroutine(ExecuteTravelRoutine());
        }

        private IEnumerator ExecuteTravelRoutine()
        {
            _isTransitioningToCity = true;
            _isTravelPromptActive = false;
            _isPersistentPromptActive = false;

            // Lock player input during warp
            if (_alitaController != null) _alitaController.enabled = false;
            if (_alitaCharController != null) _alitaCharController.SimpleMove(Vector3.zero);

            // Play starship engine warp pass SFX
            if (warpSFX != null)
            {
                MarsSfx.Play(warpSFX, 1.0f, 1.0f);
            }

            // Fullscreen Hyperspace Warp Whiteout Flash
            float flashDuration = 1.1f;
            float t = 0f;
            while (t < flashDuration)
            {
                t += Time.unscaledDeltaTime;
                _warpFlashAlpha = Mathf.Clamp01(t / 0.85f);
                yield return null;
            }
            _warpFlashAlpha = 1.0f;

            // Inform Cyber City clinic door trigger of incoming arrival (Plaza spawn outside Dr. Ido's Clinic)
            ClinicDoorTrigger.IsInTransit = true;
            ClinicDoorTrigger.PendingSpawnPos = new Vector3(19.00f, -17.98f, 49.50f);
            ClinicDoorTrigger.PendingSpawnYaw = 180f;
            PlayerPrefs.SetInt(MainMenuController.PREF_SELECTED_SECTOR, 0);
            PlayerPrefs.SetInt("MarsTitan_Defeated", 1);
            PlayerPrefs.Save();

            yield return new WaitForSecondsRealtime(0.25f);
            Time.timeScale = 1.0f;

            SceneManager.LoadScene("Demo_City");
        }

        private void InitStyles()
        {
            if (stylesInitialized) return;

            titleHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            titleHeaderStyle.normal.textColor = new Color(1f, 0.42f, 0.2f); // Mars Burnt Orange

            subHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            subHeaderStyle.normal.textColor = new Color(0f, 0.88f, 1f); // Neon Cyan

            missionBriefStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter
            };
            missionBriefStyle.normal.textColor = new Color(0.85f, 0.88f, 0.95f, 0.9f);

            tacticalAlertStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            boxBackdropStyle = new GUIStyle(GUI.skin.box);
            boxBackdropStyle.normal.background = MakeTex(2, 2, new Color(0.08f, 0.03f, 0.02f, 0.88f));
            boxBackdropStyle.normal.background = MakeTex(2, 2, new Color(0.06f, 0.03f, 0.03f, 0.92f));

            modalTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            modalTitleStyle.normal.textColor = new Color(0f, 0.94f, 1f); // Neon Cyan

            modalBodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            modalBodyStyle.normal.textColor = new Color(0.9f, 0.93f, 0.98f, 0.95f);

            btnTravelStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            btnTravelStyle.normal.textColor = new Color(0.04f, 0.05f, 0.08f);
            btnTravelStyle.normal.background = MakeTex(2, 2, new Color(0f, 0.94f, 1f, 0.95f)); // Cyan Glow
            btnTravelStyle.hover.textColor = Color.white;
            btnTravelStyle.hover.background = MakeTex(2, 2, new Color(0.2f, 1f, 0.8f, 1f));

            btnStayStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            btnStayStyle.normal.textColor = new Color(0.85f, 0.88f, 0.92f);
            btnStayStyle.normal.background = MakeTex(2, 2, new Color(0.18f, 0.14f, 0.22f, 0.9f));
            btnStayStyle.hover.textColor = Color.white;
            btnStayStyle.hover.background = MakeTex(2, 2, new Color(0.35f, 0.25f, 0.45f, 1f));

            persistentPromptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            persistentPromptStyle.normal.textColor = new Color(0.1f, 1f, 0.7f); // Neon Emerald/Cyan

            stylesInitialized = true;
        }

        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        private void DrawCyberBorder(Rect rect, Color color, float thickness)
        {
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height - thickness, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x + rect.width - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            GUI.color = prev;
        }

        private void OnGUI()
        {
            InitStyles();

            // 0. Fullscreen Warp Whiteout Flash
            if (_warpFlashAlpha > 0.001f)
            {
                Color prevC = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, _warpFlashAlpha);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture, ScaleMode.StretchToFill);
                GUI.color = prevC;
            }

            // 1. Mission Intro Cinematic Card (First 5 seconds)
            if (isIntroActive)
            {
                float alpha = Mathf.Clamp01(introTimer / 1.0f);
                float cardW = 680f;
                float cardH = 100f;
                float cardX = (Screen.width - cardW) * 0.5f;
                float cardY = 85f;

                Color prevColor = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, alpha);

                GUI.Box(new Rect(cardX, cardY, cardW, cardH), GUIContent.none, boxBackdropStyle);
                DrawCyberBorder(new Rect(cardX, cardY, cardW, cardH), new Color(1f, 0.42f, 0.2f, 0.7f), 1.5f);
                GUI.Label(new Rect(cardX, cardY + 8, cardW, 28), "OPERATION RED HORIZON // MARS CYDONIA BASIN", titleHeaderStyle);
                GUI.Label(new Rect(cardX, cardY + 38, cardW, 20), "URM ANCIENT VAULT EXCAVATION SITE - SECTOR 00", subHeaderStyle);
                GUI.Label(new Rect(cardX, cardY + 62, cardW, 22), "DIRECTIVE: NEUTRALIZE COLOSSUS-06 TITAN WAR MECH // RECOVER ANTIMATTER CORE", missionBriefStyle);

                GUI.color = prevColor;
            }

            // 2. Real-time Tactical Move Alert Banner
            if (!string.IsNullOrEmpty(tacticalAlertText))
            {
                bool isDanger = tacticalAlertText.Contains("THREAT") || tacticalAlertText.Contains("WARNING") || tacticalAlertText.Contains("ALERT");
                tacticalAlertStyle.normal.textColor = isDanger ? new Color(1f, 0.25f, 0.15f) : new Color(0.2f, 1f, 0.45f);

                float bannerW = 600f;
                float bannerH = 34f;
                float bannerX = (Screen.width - bannerW) * 0.5f;
                float bannerY = Screen.height - 85f;

                GUI.Box(new Rect(bannerX, bannerY, bannerW, bannerH), GUIContent.none, boxBackdropStyle);
                GUI.Label(new Rect(bannerX, bannerY + 6, bannerW, 24), tacticalAlertText, tacticalAlertStyle);
            }

            // 3. Victory Travel Modal Dialog
            if (_isTravelPromptActive && !_isTransitioningToCity)
            {
                float boxW = 660f;
                float boxH = 250f;
                float boxX = (Screen.width - boxW) * 0.5f;
                float boxY = (Screen.height - boxH) * 0.5f;

                GUI.Box(new Rect(boxX, boxY, boxW, boxH), GUIContent.none, boxBackdropStyle);
                DrawCyberBorder(new Rect(boxX, boxY, boxW, boxH), new Color(0f, 0.94f, 1f, 0.85f), 2f);

                GUI.Label(new Rect(boxX, boxY + 16f, boxW, 28f), "★ MISSION COMPLETE // DR. IDO AVENGED ★", modalTitleStyle);
                GUI.Label(new Rect(boxX, boxY + 46f, boxW, 20f), "COLOSSUS-06 TITAN NEUTRALIZED • ANTIMATTER CORE SECURED", subHeaderStyle);

                string desc = "Dr. Ido's death has been avenged. The Martian URM vault is liberated.\nYour starship is fully fueled and calibrated for the hyperspace jump back to Cyber City.";
                GUI.Label(new Rect(boxX + 24f, boxY + 74f, boxW - 48f, 50f), desc, modalBodyStyle);

                float btnW = 280f;
                float btnH = 46f;
                float btnY = boxY + 172f;

                // Travel Back button
                if (GUI.Button(new Rect(boxX + 35f, btnY, btnW, btnH), "▶ TRAVEL TO CYBER CITY\n[ENTER / SPACE / E]", btnTravelStyle))
                {
                    TravelBackToCyberCity();
                }

                // Explore Mars button
                if (GUI.Button(new Rect(boxX + boxW - btnW - 35f, btnY, btnW, btnH), "EXPLORE MARS SURFACE\n[ESC]", btnStayStyle))
                {
                    _isTravelPromptActive = false;
                    _isPersistentPromptActive = true;
                }
            }

            // 4. Persistent Return Prompt when exploring Mars
            if (_isPersistentPromptActive && !_isTransitioningToCity && !_isCutscenePlaying)
            {
                float barW = 560f;
                float barH = 38f;
                float barX = (Screen.width - barW) * 0.5f;
                float barY = 24f;

                GUI.Box(new Rect(barX, barY, barW, barH), GUIContent.none, boxBackdropStyle);
                DrawCyberBorder(new Rect(barX, barY, barW, barH), new Color(0.2f, 0.9f, 1f, 0.65f), 1.5f);

                string promptText = _isNearStarship
                    ? "★ NEAR STARSHIP: PRESS [E] OR [ENTER] TO RETURN TO CYBER CITY ★"
                    : "★ DR. IDO AVENGED — PRESS [ENTER] TO RETURN TO CYBER CITY ★";

                GUI.Label(new Rect(barX, barY + 7f, barW, 24f), promptText, persistentPromptStyle);
            }
        }
    }
}
