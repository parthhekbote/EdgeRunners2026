using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using Unity.Cinemachine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Alita
{
    /// <summary>
    /// Narrative States for the God of War style continuous single-take sequence.
    /// </summary>
    public enum CutscenePhase
    {
        MainMenu,               // Real-time menu: camera focuses on Alita resting in the scrap in the rain & thunder
        TransitionToIdo,        // Seamless sweeping crane pan from Alita across scrap dunes and BEHIND Dr. Ido
        IdoSearching,           // Dr. Ido walks into the scrapyard through rain with camera trailing behind him
        IdoApproaching,         // Dr. Ido notices the faint URM glow and approaches the scrap dune
        IdoExamining,           // Dr. Ido kneels, clears debris, checks vitals & warm quantum core (stays in stasis)
        IdoLiftingAlita,        // Custom procedural lift: Ido reaches down and gathers Alita into his arms
        IdoWalkingCarrying,     // Dr. Ido walks carrying Alita through the rain out of the scrapyard towards his clinic
        Finished                // Cinematic fade to black into Chapter I: Ido's Clinic
    }

    /// <summary>
    /// Master Cinematic Cutscene Controller for the Scrapyard Discovery.
    /// Features:
    /// - God of War seamless real-time Title Menu directly in the 300m junkyard
    /// - Heavy moody thunderstorm with dynamic lightning flashes and delayed thunder rumbles
    /// - Continuous single-take camera sweeping arc from Alita to BEHIND Dr. Ido with ZERO cuts or loading screens
    /// - Voice-acted dialogue for Dr. Dyson Ido
    /// - Custom procedural animation for Dr. Ido lifting and carrying dormant Alita in his arms
    /// </summary>
    public class AlitaCutsceneController : MonoBehaviour
    {
        public static AlitaCutsceneController Instance { get; private set; }

        [Header("Cutscene Phase")]
        public CutscenePhase currentPhase = CutscenePhase.MainMenu;

        [Header("Key Cutscene Actors")]
        public GameObject doctorActor;
        public Animator doctorAnimator;
        public AlitaBrokenCyborg alitaActor;
        public PlayableDirector timelineDirector;
        public IdoCarryAlitaRig carryRig;

        [Header("Weather & Storm System")]
        public ScrapyardStormSystem stormSystem;
        public ParticleSystem rainFX;
        public ParticleSystem rainSplashFX;
        public bool enableRain = true;

        [Header("Lighting & Atmosphere")]
        public Light zalemSunbeamLight;
        public Light doctorKeyLight;
        public Light alitaFocusLight;
        public Light diagnosticProbeLight;
        public ParticleSystem environmentalDustFX;

        [Header("Cinematic Camera")]
        public Camera cutsceneCamera;
        public CinemachineBrain cinemachineBrain;
        public bool directCameraControl = true;
        public float cameraFieldOfView = 44f;

        [Header("Actor Movement Waypoints")]
        public Vector3 doctorEntrancePos = new Vector3(32.75f, 2.65f, -64.29f);
        public Vector3 doctorNoticePos = new Vector3(33.65f, 2.20f, -54.50f);
        public Vector3 doctorAlitaPos = new Vector3(33.30f, 2.10f, -50.10f);
        public Vector3 doctorExitPos = new Vector3(32.75f, 2.20f, -72.0f);

        [Header("Cinematic Soundtrack")]
        public AudioSource musicSource;
        public AudioClip cutsceneMusic;
        [Range(0f, 1f)] public float menuMusicVolume = 0.38f;
        [Range(0f, 1f)] public float cinematicMusicVolume = 0.80f;
        public bool autoPlayMusic = true;

        [Header("Dr. Ido Voice Acting")]
        public AudioSource doctorVoiceSource;
        public AudioClip voiceIdoSearch;
        public AudioClip voiceIdoNotice;
        public AudioClip voiceIdoExamine;
        public AudioClip voiceIdoCoreWarm;
        public AudioClip voiceIdoStasis;
        public AudioClip voiceIdoCarryHome;

        [Header("UI & Presentation")]
        public CanvasGroup scifiMenuCanvasGroup;
        public bool showCinematicBars = false;
        public bool showSubtitles = true;
        private string _currentSubtitle = "";
        private float _subtitleTimer = 0f;
        private float _fadeAlpha = 0f;

        [Header("Scene Stitching & Transition to Cutscene 2")]
        public string nextCutsceneSceneName = "Alita_Tron_Awakening";
        public float transitionCardDuration = 4.2f;
        public bool autoAdvanceToNextScene = true;
        public bool allowSkipCutscene = true;
        private bool _isLoadingNextScene = false;
        private float _loadProgress = 0f;

        // Transition Timing & Camera Tracking
        private float _phaseTime = 0f;
        private float _menuFadeAlpha = 1.0f;
        private Vector3 _transitionStartCamPos;
        private Quaternion _transitionStartCamRot;
        private float _transitionStartFov = 38f;

        // Audio
        private AudioSource _audioSource;
        private AudioClip _relayClickClip;
        private AudioClip _buttonClickClip;

        private void Awake()
        {
            Instance = this;
            SetupAudio();
            LoadVoiceClips();
        }

        private void SetupAudio()
        {
            _audioSource = gameObject.GetComponent<AudioSource>();
            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;

            // Dedicated 2D Music AudioSource
            if (musicSource == null)
            {
                Transform musicChild = transform.Find("Cutscene_Music_Source");
                if (musicChild != null)
                {
                    musicSource = musicChild.GetComponent<AudioSource>();
                }
                else
                {
                    GameObject mGo = new GameObject("Cutscene_Music_Source");
                    mGo.transform.SetParent(transform);
                    musicSource = mGo.AddComponent<AudioSource>();
                }
            }

            if (musicSource != null)
            {
                musicSource.spatialBlend = 0f; // Pure 2D cinematic soundtrack
                musicSource.loop = true;
                musicSource.playOnAwake = false;
                musicSource.volume = menuMusicVolume;
            }

            // UI button click clip
            int clickSamples = 1600;
            _buttonClickClip = AudioClip.Create("UI_Click", clickSamples, 1, 44100, false);
            float[] clickData = new float[clickSamples];
            for (int i = 0; i < clickSamples; i++)
            {
                float t = (float)i / clickSamples;
                clickData[i] = Mathf.Sin(t * 2200f * Mathf.PI * 2f) * Mathf.Exp(-t * 26f) * 0.6f;
            }
            _buttonClickClip.SetData(clickData, 0);

            // Diagnostic probe relay click
            int relaySamples = 1200;
            _relayClickClip = AudioClip.Create("Relay_Click", relaySamples, 1, 44100, false);
            float[] relayData = new float[relaySamples];
            for (int i = 0; i < relaySamples; i++)
            {
                float t = (float)i / relaySamples;
                relayData[i] = ((Random.value * 2f - 1f) * 0.5f + Mathf.Sin(t * 1800f * Mathf.PI * 2f) * 0.5f) * Mathf.Exp(-t * 20f) * 0.7f;
            }
            _relayClickClip.SetData(relayData, 0);

            // Doctor Voice Source
            if (doctorVoiceSource == null && doctorActor != null)
            {
                doctorVoiceSource = doctorActor.GetComponent<AudioSource>();
                if (doctorVoiceSource == null) doctorVoiceSource = doctorActor.AddComponent<AudioSource>();
                doctorVoiceSource.spatialBlend = 0.25f; // Mostly clear dialogue with subtle spatial presence
                doctorVoiceSource.playOnAwake = false;
                doctorVoiceSource.volume = 1.0f;
            }
        }

        private void LoadVoiceClips()
        {
#if UNITY_EDITOR
            if (cutsceneMusic == null) cutsceneMusic = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/Tears_in_the_Rain.mp3");
            if (voiceIdoSearch == null) voiceIdoSearch = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Voice/Ido/ido_01_search.wav");
            if (voiceIdoNotice == null) voiceIdoNotice = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Voice/Ido/ido_02_notice.wav");
            if (voiceIdoExamine == null) voiceIdoExamine = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Voice/Ido/ido_03_examine.wav");
            if (voiceIdoCoreWarm == null) voiceIdoCoreWarm = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Voice/Ido/ido_04_core_warm.wav");
            if (voiceIdoStasis == null) voiceIdoStasis = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Voice/Ido/ido_05_stasis.wav");
            if (voiceIdoCarryHome == null) voiceIdoCarryHome = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Voice/Ido/ido_06_carry_home.wav");
#endif
        }

        private void Start()
        {
            // Auto-discover components if missing
            if (cutsceneCamera == null) cutsceneCamera = Camera.main;
            if (cinemachineBrain == null && cutsceneCamera != null) cinemachineBrain = cutsceneCamera.GetComponent<CinemachineBrain>();
            if (cinemachineBrain != null && directCameraControl) cinemachineBrain.enabled = false;

            if (stormSystem == null)
            {
                stormSystem = FindAnyObjectByType<ScrapyardStormSystem>();
                if (stormSystem == null)
                {
                    var stormGo = new GameObject("Scrapyard_Storm_System");
                    stormSystem = stormGo.AddComponent<ScrapyardStormSystem>();
                }
            }
            if (stormSystem != null)
            {
                stormSystem.targetCamera = cutsceneCamera;
                stormSystem.EnsureRainParticlesInstantiated();
            }

            if (alitaActor == null)
            {
                var alitaGo = GameObject.Find("Alita_Cyborg_Character") ?? GameObject.Find("Alita_Broken_Cyborg");
                if (alitaGo != null)
                {
                    alitaActor = alitaGo.GetComponent<AlitaBrokenCyborg>();
                    if (alitaActor == null) alitaActor = alitaGo.AddComponent<AlitaBrokenCyborg>();
                }
            }

            if (carryRig == null && doctorActor != null)
            {
                carryRig = doctorActor.GetComponent<IdoCarryAlitaRig>();
                if (carryRig == null) carryRig = doctorActor.AddComponent<IdoCarryAlitaRig>();
                carryRig.doctorActor = doctorActor;
                if (alitaActor != null) carryRig.alitaCyborg = alitaActor.gameObject;
                carryRig.InitializeRig();
            }

            // Position Doctor at entrance
            if (doctorActor != null)
            {
                doctorActor.transform.position = doctorEntrancePos;
                Vector3 moveDir = (doctorNoticePos - doctorEntrancePos).normalized;
                if (moveDir != Vector3.zero) doctorActor.transform.rotation = Quaternion.LookRotation(moveDir);
            }

            // Create diagnostic probe light if needed
            if (diagnosticProbeLight == null)
            {
                GameObject probeGo = new GameObject("Diagnostic_Probe_Light");
                probeGo.transform.SetParent(transform);
                diagnosticProbeLight = probeGo.AddComponent<Light>();
                diagnosticProbeLight.type = LightType.Point;
                diagnosticProbeLight.color = new Color(0.1f, 0.95f, 1.0f);
                diagnosticProbeLight.range = 2.8f;
                diagnosticProbeLight.intensity = 0f;
                diagnosticProbeLight.enabled = false;
            }

            // Start soundtrack playback
            if (autoPlayMusic && musicSource != null && cutsceneMusic != null)
            {
                if (musicSource.clip != cutsceneMusic) musicSource.clip = cutsceneMusic;
                musicSource.volume = menuMusicVolume;
                if (!musicSource.isPlaying) musicSource.Play();
            }

            if (doctorAnimator == null && doctorActor != null)
            {
                doctorAnimator = doctorActor.GetComponentInChildren<Animator>();
            }
            SetDoctorAnimation(0f, false, false, false);

            // Start in MainMenu phase
            currentPhase = CutscenePhase.MainMenu;
            _phaseTime = 0f;
            _menuFadeAlpha = 1.0f;
        }

        private void Update()
        {
            _phaseTime += Time.deltaTime;
            UpdateMusicVolume();

            if (_subtitleTimer > 0f)
            {
                _subtitleTimer -= Time.deltaTime;
                if (_subtitleTimer <= 0f)
                {
                    _currentSubtitle = "";
                    Alita.UI.CyberpunkDialogueHUD.Instance?.CloseDialogue();
                }
            }

            // Quick Skip to Finish for development & player convenience
            if (allowSkipCutscene && currentPhase != CutscenePhase.MainMenu && currentPhase != CutscenePhase.Finished)
            {
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Tab))
                {
                    SkipToFinish();
                }
            }

            // State-specific logic
            switch (currentPhase)
            {
                case CutscenePhase.MainMenu:
                    UpdateMainMenuInput();
                    break;

                case CutscenePhase.TransitionToIdo:
                    UpdateTransition();
                    break;

                case CutscenePhase.IdoSearching:
                    UpdateIdoSearching();
                    break;

                case CutscenePhase.IdoApproaching:
                    UpdateIdoApproaching();
                    break;

                case CutscenePhase.IdoExamining:
                    UpdateIdoExamining();
                    break;

                case CutscenePhase.IdoLiftingAlita:
                    UpdateIdoLifting();
                    break;

                case CutscenePhase.IdoWalkingCarrying:
                    UpdateIdoCarryingWalk();
                    break;

                case CutscenePhase.Finished:
                    UpdateFinished();
                    break;
            }
        }

        private void LateUpdate()
        {
            if (directCameraControl && cutsceneCamera != null)
            {
                UpdateCinematicCamera();
            }
        }

        #region Soundtrack & Audio Volume Control

        private void UpdateMusicVolume()
        {
            if (musicSource == null) return;

            float targetVol = menuMusicVolume;

            switch (currentPhase)
            {
                case CutscenePhase.MainMenu:
                    targetVol = menuMusicVolume;
                    break;

                case CutscenePhase.TransitionToIdo:
                case CutscenePhase.IdoSearching:
                case CutscenePhase.IdoApproaching:
                case CutscenePhase.IdoExamining:
                case CutscenePhase.IdoLiftingAlita:
                case CutscenePhase.IdoWalkingCarrying:
                    targetVol = cinematicMusicVolume;
                    break;

                case CutscenePhase.Finished:
                    targetVol = 0f;
                    break;
            }

            musicSource.volume = Mathf.MoveTowards(musicSource.volume, targetVol, Time.deltaTime * 0.35f);
        }

        #endregion

        #region Phase Handlers

        private void UpdateMainMenuInput()
        {
            // Allow player to press SPACE, ENTER, or click START to begin the journey
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                StartJourney();
            }
        }

        public void StartJourney()
        {
            if (currentPhase != CutscenePhase.MainMenu) return;

            if (_audioSource != null && _buttonClickClip != null)
            {
                _audioSource.PlayOneShot(_buttonClickClip, 0.9f);
            }

            // Ensure soundtrack is playing and ready to swell
            if (musicSource != null && cutsceneMusic != null)
            {
                if (musicSource.clip != cutsceneMusic) musicSource.clip = cutsceneMusic;
                if (!musicSource.isPlaying) musicSource.Play();
            }

            // Record starting camera pose for seamless interpolation
            if (cutsceneCamera != null)
            {
                _transitionStartCamPos = cutsceneCamera.transform.position;
                _transitionStartCamRot = cutsceneCamera.transform.rotation;
                _transitionStartFov = cutsceneCamera.fieldOfView;
            }

            currentPhase = CutscenePhase.TransitionToIdo;
            _phaseTime = 0f;
        }

        public void LoadClinicAwakeningScene()
        {
            if (_audioSource != null && _buttonClickClip != null)
            {
                _audioSource.PlayOneShot(_buttonClickClip, 0.9f);
            }
            UnityEngine.SceneManagement.SceneManager.LoadScene(nextCutsceneSceneName);
        }

        public void QuitGame()
        {
            if (_audioSource != null && _buttonClickClip != null)
            {
                _audioSource.PlayOneShot(_buttonClickClip, 0.9f);
            }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void UpdateTransition()
        {
            // Smoothly dissolve the Title Menu UI
            _menuFadeAlpha = Mathf.MoveTowards(_menuFadeAlpha, 0f, Time.deltaTime * 1.25f);
            if (scifiMenuCanvasGroup != null)
            {
                scifiMenuCanvasGroup.alpha = Mathf.MoveTowards(scifiMenuCanvasGroup.alpha, 0f, Time.deltaTime * 1.5f);
                if (scifiMenuCanvasGroup.alpha <= 0.01f) scifiMenuCanvasGroup.gameObject.SetActive(false);
            }
            SetDoctorAnimation(0f, false, false, false);

            // Transition duration: 5.2 seconds for slow, majestic sweeping arc behind Ido
            if (_phaseTime >= 5.2f)
            {
                currentPhase = CutscenePhase.IdoSearching;
                _phaseTime = 0f;
                SetSubtitleAndVoice("Dr. Dyson Ido: \"More salvage dumped from Zalem... nothing intact in this storm.\"", 5.5f, voiceIdoSearch);
            }
        }

        private void UpdateIdoSearching()
        {
            SetDoctorAnimation(1.0f, false, false, false);

            // Doctor walks from entrance (0s to 12s) towards notice point
            if (doctorActor != null)
            {
                float t = Mathf.Clamp01(_phaseTime / 12.0f);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                doctorActor.transform.position = Vector3.Lerp(doctorEntrancePos, doctorNoticePos, smoothT);
                Vector3 moveDir = (doctorNoticePos - doctorEntrancePos).normalized;
                if (moveDir != Vector3.zero)
                {
                    doctorActor.transform.rotation = Quaternion.Slerp(doctorActor.transform.rotation, Quaternion.LookRotation(moveDir), Time.deltaTime * 5f);
                }
            }

            if (_phaseTime >= 12.0f)
            {
                currentPhase = CutscenePhase.IdoApproaching;
                _phaseTime = 0f;
                SetSubtitleAndVoice("Dr. Dyson Ido: \"Wait... what is that faint glow beneath the wreckage?\"", 4.5f, voiceIdoNotice);
                if (_audioSource != null && _relayClickClip != null) _audioSource.PlayOneShot(_relayClickClip, 0.7f);
            }
        }

        private void UpdateIdoApproaching()
        {
            Vector3 alitaPos = alitaActor != null ? alitaActor.transform.position : doctorAlitaPos;
            float t = Mathf.Clamp01(_phaseTime / 8.5f);

            // Slower, cautious pace as he gets closer
            float approachSpeed = Mathf.Lerp(1.0f, 0.45f, t);
            SetDoctorAnimation(approachSpeed, false, false, false);

            // Doctor walks from notice point to kneeling position beside Alita (0s to 8.5s)
            if (doctorActor != null)
            {
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                doctorActor.transform.position = Vector3.Lerp(doctorNoticePos, doctorAlitaPos, smoothT);
                Vector3 moveDir = (doctorAlitaPos - doctorNoticePos).normalized;
                if (moveDir != Vector3.zero)
                {
                    doctorActor.transform.rotation = Quaternion.Slerp(doctorActor.transform.rotation, Quaternion.LookRotation(moveDir), Time.deltaTime * 5f);
                }
            }

            if (_phaseTime >= 8.5f)
            {
                currentPhase = CutscenePhase.IdoExamining;
                _phaseTime = 0f;
                SetDoctorAnimation(0f, true, false, false);
                SetSubtitleAndVoice("Dr. Dyson Ido: \"A Martian Berserker chassis... discarded centuries ago.\"", 5.0f, voiceIdoExamine);
                StartCoroutine(DiagnosticExaminationRoutine());
            }
        }

        private IEnumerator DiagnosticExaminationRoutine()
        {
            yield return new WaitForSeconds(3.8f);
            SetSubtitleAndVoice("Dr. Dyson Ido: \"Her brain is intact... and her antimatter core is still warm.\"", 5.5f, voiceIdoCoreWarm);

            if (diagnosticProbeLight != null)
            {
                diagnosticProbeLight.enabled = true;
                Vector3 probeCenter = GetAlitaFocusCenter();
                diagnosticProbeLight.transform.position = probeCenter + new Vector3(-0.15f, 0.25f, -0.20f);
            }

            float diagTimer = 0f;
            while (diagTimer < 5.0f)
            {
                diagTimer += Time.deltaTime;
                if (diagnosticProbeLight != null)
                {
                    diagnosticProbeLight.intensity = Mathf.PingPong(diagTimer * 3.5f, 1.6f) + 1.1f;
                }
                yield return null;
            }

            if (diagnosticProbeLight != null) diagnosticProbeLight.enabled = false;

            // Doctor recognizes she cannot wake up here in the storm:
            SetSubtitleAndVoice("Dr. Dyson Ido: \"She's in deep stasis. I need to take her back to the clinic.\"", 5.0f, voiceIdoStasis);
            yield return new WaitForSeconds(4.2f);

            // Proceed to lifting Alita
            currentPhase = CutscenePhase.IdoLiftingAlita;
            _phaseTime = 0f;
            SetDoctorAnimation(0f, false, true, false);

            if (carryRig != null)
            {
                carryRig.StartPickupSequence(() =>
                {
                    currentPhase = CutscenePhase.IdoWalkingCarrying;
                    _phaseTime = 0f;
                    SetDoctorAnimation(1.0f, false, false, true);
                    SetSubtitleAndVoice("Dr. Dyson Ido: \"Hang on, little angel. Let's get you home.\"", 5.5f, voiceIdoCarryHome);
                });
            }
            else
            {
                currentPhase = CutscenePhase.IdoWalkingCarrying;
                _phaseTime = 0f;
                SetDoctorAnimation(1.0f, false, false, true);
            }
        }

        private void UpdateIdoExamining()
        {
            SetDoctorAnimation(0f, true, false, false);
            if (doctorActor != null)
            {
                Vector3 alitaPos = alitaActor != null ? alitaActor.transform.position : doctorAlitaPos;
                Vector3 dir = (alitaPos - doctorActor.transform.position).normalized;
                dir.y = 0;
                if (dir != Vector3.zero)
                {
                    doctorActor.transform.rotation = Quaternion.Slerp(doctorActor.transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 3f);
                }
            }
        }

        private void UpdateIdoLifting()
        {
            SetDoctorAnimation(0f, false, true, false);
            if (doctorActor != null)
            {
                Vector3 alitaPos = alitaActor != null ? alitaActor.transform.position : doctorAlitaPos;
                Vector3 dir = (alitaPos - doctorActor.transform.position).normalized;
                dir.y = 0;
                if (dir != Vector3.zero)
                {
                    doctorActor.transform.rotation = Quaternion.Slerp(doctorActor.transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 3f);
                }
            }
        }

        private void UpdateIdoCarryingWalk()
        {
            if (carryRig != null) carryRig.isWalkingWithAlita = true;
            SetDoctorAnimation(1.0f, false, false, true);

            // Doctor turns and walks carrying Alita toward the exit / clinic path (0s to 14s)
            if (doctorActor != null)
            {
                float t = Mathf.Clamp01(_phaseTime / 14.0f);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                doctorActor.transform.position = Vector3.Lerp(doctorAlitaPos, doctorExitPos, smoothT);

                Vector3 moveDir = (doctorExitPos - doctorAlitaPos).normalized;
                if (moveDir != Vector3.zero)
                {
                    doctorActor.transform.rotation = Quaternion.Slerp(doctorActor.transform.rotation, Quaternion.LookRotation(moveDir), Time.deltaTime * 4f);
                }
            }

            if (_phaseTime >= 14.0f)
            {
                currentPhase = CutscenePhase.Finished;
                _phaseTime = 0f;
                SetDoctorAnimation(0f, false, false, true);
            }
        }

        private void UpdateFinished()
        {
            _fadeAlpha = Mathf.MoveTowards(_fadeAlpha, 1.0f, Time.deltaTime * 1.5f);

            if (_fadeAlpha >= 0.95f && !_isLoadingNextScene)
            {
                StartCoroutine(TransitionToNextSceneRoutine());
            }
        }

        public void SkipToFinish()
        {
            if (_isLoadingNextScene) return;
            currentPhase = CutscenePhase.Finished;
            _phaseTime = 0f;
            SetDoctorAnimation(0f, false, false, true);
            _currentSubtitle = "";
            _fadeAlpha = 1.0f;
            StartCoroutine(TransitionToNextSceneRoutine());
        }

        private IEnumerator TransitionToNextSceneRoutine()
        {
            if (_isLoadingNextScene) yield break;
            _isLoadingNextScene = true;
            _fadeAlpha = 1.0f;

            if (musicSource != null) musicSource.volume = 0f;
            if (stormSystem != null)
            {
                stormSystem.isStormActive = false;
                stormSystem.StopAllCoroutines();
                if (stormSystem.rainAudioSource != null) stormSystem.rainAudioSource.Stop();
                if (stormSystem.thunderAudioSource != null) stormSystem.thunderAudioSource.Stop();
                if (stormSystem.lightningLight != null) stormSystem.lightningLight.intensity = 0f;
            }

            var op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(nextCutsceneSceneName);
            if (op != null)
            {
                while (!op.isDone)
                {
                    _loadProgress = Mathf.Clamp01(op.progress / 0.9f);
                    yield return null;
                }
            }
        }

        /// <summary>
        /// Synchronizes Doctor's Humanoid Mecanim animator with cutscene narrative states.
        /// </summary>
        public void SetDoctorAnimation(float speed, bool isExamining, bool isLifting, bool isCarrying)
        {
            if (doctorAnimator == null)
            {
                if (doctorActor != null) doctorAnimator = doctorActor.GetComponentInChildren<Animator>();
            }

            if (doctorAnimator != null)
            {
                doctorAnimator.SetFloat("Speed", speed);
                doctorAnimator.SetBool("IsExamining", isExamining);
                doctorAnimator.SetBool("IsLifting", isLifting);
                doctorAnimator.SetBool("IsCarrying", isCarrying);
            }
        }

        #endregion

        public Vector3 GetAlitaFocusCenter()
        {
            if (alitaActor != null)
            {
                var anim = alitaActor.GetComponentInChildren<Animator>();
                if (anim != null)
                {
                    Transform head = anim.GetBoneTransform(HumanBodyBones.Head);
                    Transform chest = anim.GetBoneTransform(HumanBodyBones.Chest);
                    if (head != null && chest != null) return (head.position + chest.position) * 0.5f;
                }
                return alitaActor.transform.position + Vector3.up * 1.25f;
            }

            var alitaGo = GameObject.Find("Alita_Cyborg_Character") ?? GameObject.Find("Alita_Broken_Cyborg");
            if (alitaGo != null)
            {
                var anim = alitaGo.GetComponentInChildren<Animator>();
                if (anim != null)
                {
                    Transform head = anim.GetBoneTransform(HumanBodyBones.Head);
                    Transform chest = anim.GetBoneTransform(HumanBodyBones.Chest);
                    if (head != null && chest != null) return (head.position + chest.position) * 0.5f;
                }
                return alitaGo.transform.position + Vector3.up * 1.25f;
            }

            return doctorAlitaPos + Vector3.up * 0.45f;
        }

        #region Camera System

        private void UpdateCinematicCamera()
        {
            if (cutsceneCamera == null) return;

            Vector3 alitaPos = doctorAlitaPos;
            if (alitaActor != null) alitaPos = alitaActor.transform.position;
            else
            {
                var alitaGo = GameObject.Find("Alita_Cyborg_Character") ?? GameObject.Find("Alita_Broken_Cyborg");
                if (alitaGo != null) alitaPos = alitaGo.transform.position;
            }

            Vector3 alitaCoreCenter = GetAlitaFocusCenter();
            Vector3 targetCamPos = cutsceneCamera.transform.position;
            Vector3 targetLookAt = alitaCoreCenter;
            float targetFov = 38f;
            float smoothSpeed = 5.0f;

            // Doctor's forward and right vectors
            Vector3 docFwd = doctorActor != null ? doctorActor.transform.forward : (doctorNoticePos - doctorEntrancePos).normalized;
            Vector3 docRight = doctorActor != null ? doctorActor.transform.right : Vector3.right;

            switch (currentPhase)
            {
                case CutscenePhase.MainMenu:
                    // Rule-of-Thirds 16:9 cinematic framing with Alita on right and menu space on left
                    Vector3 menuDrift = new Vector3(
                        Mathf.Sin(Time.time * 0.7f) * 0.02f,
                        Mathf.Cos(Time.time * 0.5f) * 0.015f,
                        Mathf.Sin(Time.time * 0.4f) * 0.012f
                    );
                    targetCamPos = alitaCoreCenter + new Vector3(-0.55f, 0.12f, -1.85f) + menuDrift;
                    targetLookAt = alitaCoreCenter + new Vector3(-0.35f, -0.06f, 0f);
                    targetFov = 34f;
                    smoothSpeed = 4.0f;
                    break;

                case CutscenePhase.TransitionToIdo:
                    // God of War continuous single-take crane sweep over scrapyard canyon
                    float t = Mathf.Clamp01(_phaseTime / 5.2f);
                    float smoothT = Mathf.SmoothStep(0f, 1f, t);

                    // Destination camera pose: Leading frontal 3/4 angle in front of Dr. Ido
                    Vector3 idoEntryCamPos = doctorEntrancePos + docFwd * 3.0f + Vector3.up * 1.35f - docRight * 0.9f;
                    Vector3 idoEntryLook = doctorEntrancePos + Vector3.up * 1.25f;

                    // Arcing midpoint high above scrap mounds
                    Vector3 midPoint = Vector3.Lerp(_transitionStartCamPos, idoEntryCamPos, 0.5f) + new Vector3(-1.5f, 4.0f, 0f);

                    // Quadratic Bezier curve
                    Vector3 p0 = _transitionStartCamPos;
                    Vector3 p1 = midPoint;
                    Vector3 p2 = idoEntryCamPos;
                    targetCamPos = (1f - smoothT) * (1f - smoothT) * p0 + 2f * (1f - smoothT) * smoothT * p1 + smoothT * smoothT * p2;

                    targetLookAt = Vector3.Lerp(alitaCoreCenter, idoEntryLook, smoothT);
                    targetFov = Mathf.Lerp(_transitionStartFov, 38f, smoothT);
                    smoothSpeed = 12.0f;
                    break;

                case CutscenePhase.IdoSearching:
                    // Atmospheric leading 3/4 tracking shot gliding ahead of Dr. Ido through the storm
                    if (doctorActor != null)
                    {
                        Vector3 docPos = doctorActor.transform.position;
                        targetCamPos = docPos + docFwd * 2.85f + Vector3.up * 1.35f - docRight * 0.85f;
                        targetLookAt = docPos + Vector3.up * 1.25f;
                    }
                    targetFov = 38f;
                    smoothSpeed = 6.0f;
                    break;

                case CutscenePhase.IdoApproaching:
                    // Cinematic profile two-shot establishing Dr. Ido approaching Alita's glowing debris mound
                    if (doctorActor != null)
                    {
                        Vector3 docPos = doctorActor.transform.position;
                        targetCamPos = Vector3.Lerp(docPos, alitaPos, 0.45f) + new Vector3(-2.6f, 1.4f, -1.2f);
                        targetLookAt = Vector3.Lerp(docPos + Vector3.up * 1.15f, alitaCoreCenter, 0.5f);
                    }
                    targetFov = 36f;
                    smoothSpeed = 5.0f;
                    break;

                case CutscenePhase.IdoExamining:
                    // Diagnostic close-up framing Dr. Ido kneeling beside Alita's illuminated face and beating core
                    targetCamPos = doctorAlitaPos + new Vector3(-0.95f, 0.85f, -0.95f);
                    targetLookAt = alitaCoreCenter;
                    targetFov = 26f;
                    smoothSpeed = 4.0f;
                    break;

                case CutscenePhase.IdoLiftingAlita:
                    // Low-angle heroic tilt: Camera tilts up dynamically as Dr. Ido gathers and stands with Alita
                    float liftT = Mathf.Clamp01(_phaseTime / 4.2f);
                    Vector3 currentDocPos = doctorActor != null ? doctorActor.transform.position : alitaPos;
                    targetCamPos = doctorAlitaPos + new Vector3(-1.35f, Mathf.Lerp(0.75f, 1.35f, liftT), -1.35f);
                    targetLookAt = currentDocPos + Vector3.up * Mathf.Lerp(0.45f, 1.20f, liftT);
                    targetFov = 32f;
                    smoothSpeed = 4.0f;
                    break;

                case CutscenePhase.IdoWalkingCarrying:
                    if (doctorActor != null)
                    {
                        Vector3 docPos = doctorActor.transform.position;
                        Vector3 walkDir = (doctorExitPos - doctorAlitaPos).normalized;
                        float walkProgress = Mathf.Clamp01(_phaseTime / 14.0f);
                        if (walkProgress < 0.70f)
                        {
                            targetCamPos = docPos + walkDir * 3.2f + Vector3.up * 1.35f - docRight * 0.4f;
                            targetLookAt = docPos + Vector3.up * 1.20f;
                            targetFov = 36f;
                        }
                        else
                        {
                            // Cinematic crane sweep up into rain clouds as Ido walks away into the storm
                            float craneT = (walkProgress - 0.70f) / 0.30f;
                            float smoothCrane = Mathf.SmoothStep(0f, 1f, craneT);
                            targetCamPos = docPos + walkDir * 4.2f + Vector3.up * Mathf.Lerp(1.35f, 4.6f, smoothCrane) - docRight * 0.4f;
                            targetLookAt = docPos + Vector3.up * Mathf.Lerp(1.20f, 4.8f, smoothCrane) + walkDir * Mathf.Lerp(0f, 5.0f, smoothCrane);
                            targetFov = Mathf.Lerp(36f, 44f, smoothCrane);
                        }
                    }
                    smoothSpeed = 5.0f;
                    break;

                case CutscenePhase.Finished:
                    break;
            }

            // Apply camera transform smoothly
            cutsceneCamera.transform.position = Vector3.Lerp(cutsceneCamera.transform.position, targetCamPos, Time.deltaTime * smoothSpeed);
            Vector3 lookDir = (targetLookAt - cutsceneCamera.transform.position).normalized;
            if (lookDir != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDir);
                cutsceneCamera.transform.rotation = Quaternion.Slerp(cutsceneCamera.transform.rotation, targetRot, Time.deltaTime * smoothSpeed);
            }
            cutsceneCamera.fieldOfView = Mathf.Lerp(cutsceneCamera.fieldOfView, targetFov, Time.deltaTime * 3.5f);
        }

        #endregion

        #region UI & Presentation

        public void SetSubtitleAndVoice(string text, float duration, AudioClip voiceClip = null)
        {
            _currentSubtitle = text;
            _subtitleTimer = duration;

            if (voiceClip != null && doctorVoiceSource != null)
            {
                doctorVoiceSource.Stop();
                doctorVoiceSource.clip = voiceClip;
                doctorVoiceSource.Play();
            }

            if (!string.IsNullOrEmpty(text))
            {
                Alita.UI.CyberpunkDialogueHUD.GetOrCreate().ShowDialogue(
                    "DR. DYSON IDO",
                    text,
                    null,
                    "CYBER-SURGEON & HUNTER-WARRIOR",
                    null,
                    duration
                );
            }
        }

        private void OnGUI()
        {
            int sw = Screen.width;
            int sh = Screen.height;

            // 1. Seamless Main Menu UI (only if scifiMenuCanvasGroup is not assigned)
            if (scifiMenuCanvasGroup == null && _menuFadeAlpha > 0.01f)
            {
                DrawSeamlessMainMenuUI(sw, sh);
            }

            // 2. Cinematic Letterbox Bars (only during cinematic action if enabled, never in menu)
            if (showCinematicBars && currentPhase != CutscenePhase.MainMenu)
            {
                int barHeight = Mathf.Max(40, (int)(sh * 0.088f));
                GUI.color = Color.black;
                GUI.DrawTexture(new Rect(0, 0, sw, barHeight), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(0, sh - barHeight, sw, barHeight), Texture2D.whiteTexture);
            }

            // 3. Cinematic Subtitles rendered via AAA CyberpunkDialogueHUD (Canvas uGUI)

            // 4. Seamless Cinematic Fade to Black (No title card or button prompt)
            if (_fadeAlpha > 0.01f)
            {
                GUI.color = new Color(0f, 0f, 0f, _fadeAlpha);
                GUI.DrawTexture(new Rect(0, 0, sw, sh), Texture2D.whiteTexture);
            }

            // Skip prompt during playback
            if (allowSkipCutscene && currentPhase != CutscenePhase.MainMenu && currentPhase != CutscenePhase.Finished)
            {
                GUIStyle skipStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    alignment = TextAnchor.MiddleRight
                };
                skipStyle.normal.textColor = new Color(0.7f, 0.8f, 0.9f, 0.5f);
                GUI.Label(new Rect(sw - 230, 12, 210, 22), "Press [ESC] to Skip", skipStyle);
            }
        }

        private void DrawSeamlessMainMenuUI(int sw, int sh)
        {
            GUI.color = new Color(1f, 1f, 1f, _menuFadeAlpha);

            int marginX = (int)(sw * 0.08f);
            int startY = (int)(sh * 0.22f);

            // Title: ALITA: BATTLE ANGEL
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Clamp(sw / 34, 26, 48),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            titleStyle.normal.textColor = new Color(1f, 1f, 1f, _menuFadeAlpha);
            GUI.Label(new Rect(marginX, startY, 600, 56), "ALITA: BATTLE ANGEL", titleStyle);

            // Subtitle: THE SCRAPYARD DISCOVERY
            GUIStyle subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Clamp(sw / 74, 12, 18),
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft
            };
            subStyle.normal.textColor = new Color(0.1f, 0.92f, 1f, _menuFadeAlpha * 0.9f);
            GUI.Label(new Rect(marginX + 2, startY + 54, 500, 26), "◈ THE SCRAPYARD DISCOVERY // SEAMLESS PROLOGUE", subStyle);

            // Interactive Start Button
            int btnY = (int)(sh * 0.62f);
            int btnW = 260;
            int btnH = 50;

            // Pulsing highlight glow on button
            float pulse = Mathf.PingPong(Time.time * 1.8f, 0.35f) + 0.65f;
            Color btnColor = new Color(0f, 0.85f, 1.0f, _menuFadeAlpha * pulse);

            GUI.color = btnColor;
            GUIStyle btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            btnStyle.normal.textColor = Color.white;

            if (GUI.Button(new Rect(marginX, btnY, btnW, btnH), "[ START JOURNEY ]", btnStyle))
            {
                StartJourney();
            }

            // Keyboard prompt
            GUIStyle keyPromptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Normal
            };
            keyPromptStyle.normal.textColor = new Color(0.7f, 0.8f, 0.9f, _menuFadeAlpha * 0.75f);
            GUI.Label(new Rect(marginX, btnY + 58, 400, 22), "Press [SPACE] or [ENTER] to Begin", keyPromptStyle);

            // Atmospheric weather telemetry
            int statusY = sh - (int)(sh * 0.14f);
            GUIStyle statusStyle = new GUIStyle(GUI.skin.label) { fontSize = 10 };
            statusStyle.normal.textColor = new Color(0.5f, 0.7f, 0.85f, _menuFadeAlpha * 0.65f);
            GUI.Label(new Rect(marginX, statusY, 500, 20), "ATMOSPHERE: PRECIPITATION OVERCAST // REAL-TIME RAIN & THUNDER AUDIO", statusStyle);
        }

        #endregion
    }
}
