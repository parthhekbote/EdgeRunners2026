using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;
using Alita.UI;

namespace Alita
{
    /// <summary>
    /// Master Opening Controller for BLACKOUT — ACT I OPENING.
    /// Manages the seamless progression:
    /// 1. SCENE 01: TRON: Ares-inspired Terminal Boot & System Reconstruction
    /// 2. SCENE 02: Eye Activation & Aperture Focus (Aperture click, blur -> focus)
    /// 3. SCENE 03: The Scrapper (The Doctor) examining her chassis & diagnostic monitor
    /// 4. SCENE 04: Body Discovery (Hand elevation, finger flexing, distorted vocal glitch)
    /// 5. SCENE 05: Scrapyard Discovery & Unknown Component Scan
    /// 6. SCENE 06: Mirror Reflection & Technological Ambiguity
    /// 7. SCENE 07: System Diagnostic Overlay HUD
    /// 8. SCENE 08: First Movement & Reflex Catch
    /// 9. SCENE 09: Combat Response against Training Drone & Seamless Gameplay Transition
    /// 10. SCENE 10: The Blackout Grid Interruption & Autonomous Terminal Callback
    /// 11. SCENE 11: Lower District Exploration & Act I Gameplay Start
    /// </summary>
    public class BlackoutAct1CutsceneController : MonoBehaviour
    {
        public static BlackoutAct1CutsceneController Instance { get; private set; }

        public enum OpeningPhase
        {
            Scene01_TerminalBoot,
            Scene02_EyeActivation,
            Scene03_TheScrapper,
            Scene04_BodyDiscovery,
            Scene05_ScrapyardMontage,
            Scene06_MirrorScene,
            Scene07_SystemOverlay,
            Scene08_FirstMovement,
            Scene09_CombatTransition,
            Scene10_TheBlackout,
            Scene11_LowerDistrictPlayable
        }

        [Header("Opening State")]
        public OpeningPhase currentPhase = OpeningPhase.Scene01_TerminalBoot;
        public float sequenceTimer = 0f;
        public bool isSkipping = false;

        [Header("Key Actors")]
        public GameObject doctorActor;
        public Transform doctorVisualRoot;
        public AlitaBrokenCyborg alitaActor;
        public PlayableDirector timelineDirector;
        public GameObject playerCombatCharacter;
        public GameObject trainingDroneBot;

        [Header("Cameras & Cinemachine")]
        public CinemachineBrain cinemachineBrain;
        public Camera mainCamera;
        public GameObject cinematicRig;
        public GameObject gameplayCameraRig;

        [Header("Lighting & Atmosphere")]
        public Light workshopMainLight;
        public Light doctorKeyLight;
        public Light alitaBedLight;
        public Light emergencyRedLight;
        public Volume postProcessingVolume;

        [Header("Terminal Boot Settings")]
        public float terminalBootDuration = 18.0f;
        public bool terminalBootFinished = false;

        [Header("Actor Movement Waypoints")]
        public Vector3 doctorEntrancePos = new Vector3(-2.0f, 0f, -12.0f);
        public Vector3 doctorNoticePos = new Vector3(-1.4f, 0f, -2.5f);
        public Vector3 doctorAlitaPos = new Vector3(-0.65f, 0f, 2.7f);

        // Terminal Boot Text Script
        private readonly string[] _terminalBootLines = new string[]
        {
            "> SYSTEM RECOVERY",
            "> SCANNING...",
            "NEURAL SYSTEM ............ FOUND",
            "MOTOR SYSTEM ............. FOUND",
            "OPTICAL SYSTEM ........... FOUND",
            "POWER CORE ............... UNSTABLE",
            "MEMORY ................... CORRUPTED",
            "> RECOVERY PROTOCOL",
            "> AUTHORIZATION REQUIRED",
            "> AUTHORIZATION........... ACCEPTED"
        };

        private readonly string[] _systemCalibrationLines = new string[]
        {
            "POWER CORE .............. 12%",
            "NEURAL LINK ............. 04%",
            "MOTOR CONTROL ........... 09%",
            "OPTICAL SYSTEM .......... 16%",
            "POWER CORE .............. 37%",
            "NEURAL LINK ............. 21%",
            "MOTOR CONTROL ........... 34%",
            "OPTICAL SYSTEM .......... 48%",
            "> SYNCHRONIZATION",
            "> 61%",
            "> 82%",
            "> ERROR"
        };

        private readonly string[] _memoryArchiveLines = new string[]
        {
            "MEMORY ARCHIVE",
            "SECTOR 01 ........ CORRUPTED",
            "SECTOR 02 ........ CORRUPTED",
            "SECTOR 03 ........ CORRUPTED",
            "SECTOR 04 ........ LOCKED",
            "SECTOR 05 ........ LOCKED",
            "SUBJECT DESIGNATION:",
            "████████",
            "> MEMORY RESTORATION ABORTED",
            "> INITIALIZE",
            "> START"
        };

        // Procedural Audio Synthesizer
        private AudioSource _audioSource;
        private AudioClip _relayClickClip;
        private AudioClip _electricalHumClip;
        private AudioClip _glitchToneClip;
        private AudioClip _apertureClickClip;

        // Visual & UI runtime state
        private List<string> _visibleTerminalLines = new List<string>();
        private string _activeTypingLine = "";
        private float _cursorBlinkTimer = 0f;
        private bool _cursorVisible = true;
        private float _terminalAlpha = 1.0f;
        private float _eyeBlurAmount = 1.0f;
        private float _blackFadeAlpha = 1.0f;
        private string _activeSubtitle = "";
        private float _subtitleTimer = 0f;
        private bool _showDiagnosticOverlay = false;
        private bool _isBlackoutActive = false;
        public bool IsBlackoutActive => _isBlackoutActive;
        private bool _playerHasControl = false;

        private void Awake()
        {
            Instance = this;
            SetupProceduralAudio();
        }

        private void Start()
        {
            if (mainCamera == null) mainCamera = Camera.main;

            // Ensure Doctor actor is positioned correctly at entrance
            if (doctorActor != null)
            {
                doctorActor.transform.position = doctorEntrancePos;
                Vector3 lookDir = (doctorNoticePos - doctorEntrancePos).normalized;
                if (lookDir != Vector3.zero) doctorActor.transform.rotation = Quaternion.LookRotation(lookDir);
            }

            // Ensure Doctor Key Light is active and vibrant
            if (doctorKeyLight != null)
            {
                doctorKeyLight.enabled = true;
                doctorKeyLight.intensity = 1.8f;
                doctorKeyLight.color = new Color(1.0f, 0.94f, 0.85f);
            }

            // Start with the TRON: Ares Terminal Boot sequence
            StartCoroutine(TerminalBootRoutine());
        }

        private void SetupProceduralAudio()
        {
            _audioSource = gameObject.GetComponent<AudioSource>();
            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;

            // 1. Relay Click (Short physical mechanical impulse)
            int clickSamples = 1200;
            _relayClickClip = AudioClip.Create("Relay_Click", clickSamples, 1, 44100, false);
            float[] clickData = new float[clickSamples];
            for (int i = 0; i < clickSamples; i++)
            {
                float t = (float)i / clickSamples;
                float decay = Mathf.Exp(-t * 22f);
                float noise = (UnityEngine.Random.value * 2f - 1f) * 0.4f;
                float tone = Mathf.Sin(t * 1800f * Mathf.PI * 2f) * 0.6f;
                clickData[i] = (noise + tone) * decay * 0.8f;
            }
            _relayClickClip.SetData(clickData, 0);

            // 2. Electrical Hum (Continuous 60Hz hum with subtle harmonics)
            int humSamples = 44100;
            _electricalHumClip = AudioClip.Create("Electrical_Hum", humSamples, 1, 44100, false);
            float[] humData = new float[humSamples];
            for (int i = 0; i < humSamples; i++)
            {
                float t = (float)i / 44100;
                float h1 = Mathf.Sin(t * 60f * Mathf.PI * 2f) * 0.5f;
                float h2 = Mathf.Sin(t * 120f * Mathf.PI * 2f) * 0.25f;
                float h3 = Mathf.Sin(t * 180f * Mathf.PI * 2f) * 0.12f;
                humData[i] = (h1 + h2 + h3) * 0.25f;
            }
            _electricalHumClip.SetData(humData, 0);

            // 3. Aperture Mechanical Click
            int apSamples = 3000;
            _apertureClickClip = AudioClip.Create("Aperture_Click", apSamples, 1, 44100, false);
            float[] apData = new float[apSamples];
            for (int i = 0; i < apSamples; i++)
            {
                float t = (float)i / apSamples;
                float decay = Mathf.Exp(-t * 16f);
                float tone = Mathf.Sin(t * 3200f * Mathf.PI * 2f);
                apData[i] = tone * decay * 0.7f;
            }
            _apertureClickClip.SetData(apData, 0);
        }

        private void PlayRelaySound(float vol = 0.6f)
        {
            if (_audioSource != null && _relayClickClip != null)
            {
                _audioSource.PlayOneShot(_relayClickClip, vol);
            }
        }

        private void PlayApertureSound()
        {
            if (_audioSource != null && _apertureClickClip != null)
            {
                _audioSource.PlayOneShot(_apertureClickClip, 0.9f);
            }
        }

        private void Update()
        {
            sequenceTimer += Time.deltaTime;

            // Cursor blink
            _cursorBlinkTimer += Time.deltaTime;
            if (_cursorBlinkTimer >= 0.45f)
            {
                _cursorBlinkTimer = 0f;
                _cursorVisible = !_cursorVisible;
            }

            // Subtitle timer
            if (_subtitleTimer > 0f)
            {
                _subtitleTimer -= Time.deltaTime;
                if (_subtitleTimer <= 0f) _activeSubtitle = "";
            }

            // Skip handling via Space / Escape
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape))
            {
                if (!terminalBootFinished)
                {
                    terminalBootFinished = true;
                    StopAllCoroutines();
                    StartCoroutine(TransitionToScene02());
                }
            }

            // If player has control in Scene 09, handle gameplay input
            if (_playerHasControl)
            {
                HandleGameplayInput();
            }
        }

        private void LateUpdate()
        {
            // CRITICAL: Update Doctor world transform in LateUpdate()
            // This prevents Unity Timeline's PlayableGraph / AnimationTrack from resetting the Doctor to (0,0,0)!
            if (terminalBootFinished && doctorActor != null)
            {
                UpdateDoctorTransformInScene();
            }
        }

        private void UpdateDoctorTransformInScene()
        {
            // Calculate progress based on timeline time or cutscene timer
            float sceneTime = sequenceTimer;
            if (timelineDirector != null && timelineDirector.state == PlayState.Playing)
            {
                sceneTime = (float)timelineDirector.time;
            }

            // Shot 1 & 2: Entrance -> Notice Point (0s to 18s)
            if (sceneTime < 18.0f)
            {
                float t = Mathf.Clamp01(sceneTime / 17.5f);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                doctorActor.transform.position = Vector3.Lerp(doctorEntrancePos, doctorNoticePos, smoothT);
                Vector3 moveDir = (doctorNoticePos - doctorEntrancePos).normalized;
                if (moveDir != Vector3.zero)
                {
                    doctorActor.transform.rotation = Quaternion.Slerp(doctorActor.transform.rotation, Quaternion.LookRotation(moveDir), Time.deltaTime * 8f);
                }
            }
            // Shot 3 & 4: Stop, Notice glow in scrap (18s to 30s)
            else if (sceneTime < 30.0f)
            {
                doctorActor.transform.position = doctorNoticePos;
                Vector3 lookAtScrap = (new Vector3(0f, 0.42f, 3.5f) - doctorNoticePos).normalized;
                lookAtScrap.y = 0;
                doctorActor.transform.rotation = Quaternion.Slerp(doctorActor.transform.rotation, Quaternion.LookRotation(lookAtScrap), Time.deltaTime * 4f);
            }
            // Shot 5: Approach Alita's mound (30s to 39s)
            else if (sceneTime < 39.0f)
            {
                float t = Mathf.Clamp01((sceneTime - 30.0f) / 8.5f);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                doctorActor.transform.position = Vector3.Lerp(doctorNoticePos, doctorAlitaPos, smoothT);
                Vector3 moveDir = (doctorAlitaPos - doctorNoticePos).normalized;
                if (moveDir != Vector3.zero)
                {
                    doctorActor.transform.rotation = Quaternion.Slerp(doctorActor.transform.rotation, Quaternion.LookRotation(moveDir), Time.deltaTime * 6f);
                }
            }
            // Shot 6+: Beside Alita on repair bed / mound (39s to End)
            else
            {
                doctorActor.transform.position = doctorAlitaPos;
                Vector3 lookAtAlita = (new Vector3(0f, 0.42f, 3.5f) - doctorAlitaPos).normalized;
                lookAtAlita.y = 0;
                doctorActor.transform.rotation = Quaternion.Slerp(doctorActor.transform.rotation, Quaternion.LookRotation(lookAtAlita), Time.deltaTime * 4f);
            }
        }

        #region SCENE 01: TRON: ARES TERMINAL BOOT ROUTINE
        private IEnumerator TerminalBootRoutine()
        {
            currentPhase = OpeningPhase.Scene01_TerminalBoot;
            _terminalAlpha = 1.0f;
            _blackFadeAlpha = 1.0f;
            _visibleTerminalLines.Clear();

            // Initial silent electrical hum
            yield return new WaitForSeconds(1.2f);

            // Phase 1: Header & Initial Diagnostics
            foreach (string line in _terminalBootLines)
            {
                yield return StartCoroutine(TypewriterPrintLine(line, 0.035f));
                PlayRelaySound(0.5f);
                yield return new WaitForSeconds(0.18f);
            }

            yield return new WaitForSeconds(0.6f);

            // Phase 2: Calibration percentages
            foreach (string calib in _systemCalibrationLines)
            {
                yield return StartCoroutine(TypewriterPrintLine(calib, 0.02f));
                PlayRelaySound(0.45f);
                yield return new WaitForSeconds(0.12f);
            }

            yield return new WaitForSeconds(0.5f);

            // Phase 3: Memory Archive Corruption
            foreach (string mem in _memoryArchiveLines)
            {
                yield return StartCoroutine(TypewriterPrintLine(mem, 0.025f));
                PlayRelaySound(0.55f);
                yield return new WaitForSeconds(0.15f);
            }

            yield return new WaitForSeconds(0.8f);

            // Final ENTER -> Instant Black
            PlayRelaySound(0.9f);
            terminalBootFinished = true;

            yield return StartCoroutine(TransitionToScene02());
        }

        private IEnumerator TypewriterPrintLine(string fullLine, float charDelay)
        {
            _activeTypingLine = "";
            for (int i = 0; i < fullLine.Length; i++)
            {
                _activeTypingLine += fullLine[i];
                if (i % 2 == 0) PlayRelaySound(0.25f);
                yield return new WaitForSeconds(charDelay);
            }
            _visibleTerminalLines.Add(_activeTypingLine);
            if (_visibleTerminalLines.Count > 18) _visibleTerminalLines.RemoveAt(0);
            _activeTypingLine = "";
        }

        private IEnumerator TransitionToScene02()
        {
            // Instant black pause
            _terminalAlpha = 0f;
            _blackFadeAlpha = 1.0f;
            yield return new WaitForSeconds(0.8f);

            // SCENE 02: Eye Activation
            currentPhase = OpeningPhase.Scene02_EyeActivation;
            PlayApertureSound();

            // Start Timeline Playback in Scrapyard / Workshop
            if (timelineDirector != null)
            {
                timelineDirector.time = 0;
                timelineDirector.Play();
            }

            // Focus -> Blur -> Focus aperture simulation
            float focusDuration = 2.4f;
            float elapsed = 0f;
            while (elapsed < focusDuration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / focusDuration;
                // Optical focus pulse: 1.0 -> 0.1 -> 0.6 -> 0.0
                _blackFadeAlpha = Mathf.Lerp(1.0f, 0.0f, progress);
                _eyeBlurAmount = Mathf.PingPong(progress * 2.5f, 1f);
                yield return null;
            }
            _blackFadeAlpha = 0f;
            _eyeBlurAmount = 0f;

            // SCENE 03: The Scrapper Doctor
            StartCoroutine(NarrativeSequenceRoutine());
        }
        #endregion

        #region SCENES 03 TO 11: NARRATIVE & GAMEPLAY PROGRESSION
        private IEnumerator NarrativeSequenceRoutine()
        {
            // Scene 03: Doctor enters, approaches, checks diagnostics
            currentPhase = OpeningPhase.Scene03_TheScrapper;
            SetSubtitle("LOWER CITY — INDUSTRIAL SCRAP DISTRICT", 4.5f);

            yield return new WaitForSeconds(14.0f);
            SetSubtitle("Dr. Dyson Ido: \"Power core unstable... but neural pathways are intact.\"", 5.0f);

            yield return new WaitForSeconds(6.0f);
            SetSubtitle("Dr. Dyson Ido: \"You're awake.\"", 4.0f);

            // Scene 04: Body Discovery
            yield return new WaitForSeconds(5.0f);
            currentPhase = OpeningPhase.Scene04_BodyDiscovery;
            SetSubtitle("Dr. Dyson Ido: \"Easy. Don't stress the rebuilt servos.\"", 4.5f);

            yield return new WaitForSeconds(4.5f);
            SetSubtitle("Alita: \"...Where... am I...?\"", 4.0f);

            yield return new WaitForSeconds(4.0f);
            SetSubtitle("Dr. Dyson Ido: \"You tell me. I pulled you from three meters of industrial scrap.\"", 5.0f);

            // Scene 05: Scrapyard Montage & Unknown Component
            yield return new WaitForSeconds(6.0f);
            currentPhase = OpeningPhase.Scene05_ScrapyardMontage;
            if (alitaActor != null)
            {
                alitaActor.TriggerAwakeningSurge();
            }
            SetSubtitle("◈ [COMPONENT SCAN: UNKNOWN MARTIAN ANTIMATTER PROTOCOL] ◈", 5.0f);

            // Scene 07: System Diagnostic Overlay
            yield return new WaitForSeconds(6.0f);
            currentPhase = OpeningPhase.Scene07_SystemOverlay;
            _showDiagnosticOverlay = true;
            yield return new WaitForSeconds(4.5f);
            _showDiagnosticOverlay = false;

            // Scene 08: First Movement
            yield return new WaitForSeconds(3.0f);
            currentPhase = OpeningPhase.Scene08_FirstMovement;
            SetSubtitle("Dr. Dyson Ido: \"Let's verify motor sync. Focus on the target ahead.\"", 5.0f);

            // Scene 09: Combat Transition (Seamless blend from cutscene to gameplay)
            yield return new WaitForSeconds(5.0f);
            currentPhase = OpeningPhase.Scene09_CombatTransition;
            EnableGameplayTransition();

            // Wait for combat tutorial
            yield return new WaitForSeconds(16.0f);

            // Scene 10: The Blackout Event
            yield return StartCoroutine(TriggerBlackoutEventRoutine());

            // Scene 11: Lower District Playable
            currentPhase = OpeningPhase.Scene11_LowerDistrictPlayable;
            SetSubtitle("OBJECTIVE: EXPLORE THE LOWER DISTRICT", 6.0f);
        }

        private void EnableGameplayTransition()
        {
            _playerHasControl = true;
            SetSubtitle("SYSTEM ONLINE — COMBAT TUTORIAL: [WASD] MOVE | [SPACE] DODGE | [LMB] ATTACK", 7.0f);

            // If player combat character exists, enable control
            if (playerCombatCharacter != null)
            {
                playerCombatCharacter.SetActive(true);
            }

            // Spawn or activate training drone bot
            if (trainingDroneBot != null)
            {
                trainingDroneBot.SetActive(true);
            }
        }

        private void HandleGameplayInput()
        {
            // Simple interactive tutorial feedback
            if (Input.GetMouseButtonDown(0))
            {
                PlayRelaySound(0.8f);
            }
            if (Input.GetKeyDown(KeyCode.Space))
            {
                PlayRelaySound(0.6f);
            }
        }

        private IEnumerator TriggerBlackoutEventRoutine()
        {
            currentPhase = OpeningPhase.Scene10_TheBlackout;
            _isBlackoutActive = true;

            // Lights flicker
            for (int i = 0; i < 4; i++)
            {
                if (workshopMainLight != null) workshopMainLight.enabled = false;
                PlayRelaySound(0.7f);
                yield return new WaitForSeconds(0.12f);
                if (workshopMainLight != null) workshopMainLight.enabled = true;
                yield return new WaitForSeconds(0.18f);
            }

            // Instant Blackout
            if (workshopMainLight != null) workshopMainLight.enabled = false;
            if (doctorKeyLight != null) doctorKeyLight.intensity = 0.35f;
            if (emergencyRedLight != null)
            {
                emergencyRedLight.enabled = true;
                emergencyRedLight.color = new Color(0.95f, 0.15f, 0.15f);
                emergencyRedLight.intensity = 1.6f;
            }

            SetSubtitle("◈ [WARNING: POWER GRID INTERRUPTION — CAUSE: UNKNOWN] ◈", 5.0f);
            yield return new WaitForSeconds(4.5f);

            SetSubtitle("◈ TERMINAL CALLBACK: BLACKOUT PROTOCOL DETECTED — STATUS: ACTIVE ◈", 5.5f);
            yield return new WaitForSeconds(5.5f);
        }
        #endregion

        public void SetSubtitle(string text, float duration)
        {
            _activeSubtitle = text;
            _subtitleTimer = duration;

            if (!string.IsNullOrEmpty(text))
            {
                CyberpunkDialogueHUD.GetOrCreate().ShowSubtitle(text, duration);
            }
        }

        #region ONGUI CINEMATIC HUD & TRON RENDERING
        private void OnGUI()
        {
            int sw = Screen.width;
            int sh = Screen.height;

            // 1. TRON: Ares Terminal Boot Overlay (Scene 01)
            if (currentPhase == OpeningPhase.Scene01_TerminalBoot && _terminalAlpha > 0f)
            {
                GUI.color = Color.black;
                GUI.DrawTexture(new Rect(0, 0, sw, sh), Texture2D.whiteTexture);

                // Cyan Vector Blueprint Aesthetics
                GUI.color = new Color(0f, 0.95f, 1f, 0.85f);
                GUIStyle headerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.Clamp(sw / 65, 14, 20),
                    fontStyle = FontStyle.Bold
                };
                headerStyle.normal.textColor = new Color(0f, 0.95f, 1f);
                GUI.Label(new Rect(40, 35, sw - 80, 26), "BLACKOUT OS — CORE SYSTEM BOOT // ARCHITECTURE: URM-CYBORG-V4", headerStyle);

                // Terminal Lines
                GUIStyle logStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.Clamp(sw / 75, 12, 16),
                    fontStyle = FontStyle.Normal
                };
                logStyle.normal.textColor = new Color(0.85f, 0.98f, 1f, 0.92f);

                int startY = 75;
                int lineH = 22;
                for (int i = 0; i < _visibleTerminalLines.Count; i++)
                {
                    string l = _visibleTerminalLines[i];
                    if (l.Contains("ERROR") || l.Contains("CORRUPTED") || l.Contains("ABORTED"))
                    {
                        logStyle.normal.textColor = new Color(1.0f, 0.28f, 0.28f);
                    }
                    else if (l.Contains("ACCEPTED") || l.Contains("FOUND"))
                    {
                        logStyle.normal.textColor = new Color(0.2f, 1.0f, 0.6f);
                    }
                    else
                    {
                        logStyle.normal.textColor = new Color(0.75f, 0.95f, 1.0f);
                    }
                    GUI.Label(new Rect(40, startY + (i * lineH), sw - 80, lineH), l, logStyle);
                }

                // Currently typing line with blinking cursor
                if (!string.IsNullOrEmpty(_activeTypingLine) || _cursorVisible)
                {
                    string activeDisplay = _activeTypingLine + (_cursorVisible ? "_" : " ");
                    logStyle.normal.textColor = new Color(0f, 0.95f, 1f);
                    GUI.Label(new Rect(40, startY + (_visibleTerminalLines.Count * lineH), sw - 80, lineH), activeDisplay, logStyle);
                }

                // Digital Body Wireframe Blueprint (Right side of screen)
                DrawTronVectorBlueprint(sw, sh);

                // Skip prompt
                GUI.color = new Color(0.5f, 0.6f, 0.7f, 0.6f);
                GUIStyle skipStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleRight };
                GUI.Label(new Rect(sw - 240, 10, 220, 20), "Press [SPACE] to Skip Boot", skipStyle);
                return;
            }

            // 2. Full Black Fade / Eye Aperture Blur
            if (_blackFadeAlpha > 0f)
            {
                GUI.color = new Color(0f, 0f, 0f, _blackFadeAlpha);
                GUI.DrawTexture(new Rect(0, 0, sw, sh), Texture2D.whiteTexture);
            }

            // 3. Cinematic Letterbox Bars (2.39:1 Anamorphic)
            int barHeight = Mathf.Max(42, (int)(sh * 0.09f));
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, sw, barHeight), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, sh - barHeight, sw, barHeight), Texture2D.whiteTexture);

            // 4. Subtle Diagnostic Overlay HUD (Scene 07)
            if (_showDiagnosticOverlay)
            {
                DrawDiagnosticOverlay(sw, sh);
            }

            // 5. Cinematic Subtitles rendered via AAA CyberpunkDialogueHUD (Canvas uGUI)
        }

        private void DrawTronVectorBlueprint(int sw, int sh)
        {
            // Vector schematic drawn on the right half of the screen
            int cx = (int)(sw * 0.72f);
            int cy = (int)(sh * 0.50f);
            int boxSize = Mathf.Min(340, (int)(sh * 0.65f));

            // Outer blueprint box
            GUI.color = new Color(0f, 0.85f, 1f, 0.25f);
            GUI.Box(new Rect(cx - boxSize / 2, cy - boxSize / 2, boxSize, boxSize), "");

            // Header
            GUI.color = new Color(0f, 0.95f, 1f, 0.85f);
            GUIStyle bpStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.UpperLeft };
            GUI.Label(new Rect(cx - boxSize / 2 + 10, cy - boxSize / 2 + 10, boxSize - 20, 20), "◈ CHASSIS DIAGNOSTIC // 3D SCHEMATIC", bpStyle);

            // Schematic Nodes (Power Core, Spine, Motor, Cranial)
            GUI.color = new Color(0.1f, 0.95f, 1f, 0.75f);
            GUI.Label(new Rect(cx - 35, cy - 80, 80, 20), "[CRANIAL]");
            GUI.Label(new Rect(cx - 35, cy - 25, 80, 20), "[URM CORE]");
            GUI.Label(new Rect(cx - 35, cy + 30, 80, 20), "[SPINAL LINK]");
            GUI.Label(new Rect(cx - 75, cy + 90, 80, 20), "[L-SERVO]");
            GUI.Label(new Rect(cx + 15, cy + 90, 80, 20), "[R-SERVO]");
        }

        private void DrawDiagnosticOverlay(int sw, int sh)
        {
            int boxW = 280;
            int boxH = 140;
            int rx = 50;
            int ry = 70;

            GUI.color = new Color(0f, 0.15f, 0.25f, 0.75f);
            GUI.DrawTexture(new Rect(rx, ry, boxW, boxH), Texture2D.whiteTexture);

            GUI.color = new Color(0f, 0.95f, 1.0f);
            GUIStyle diagStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold };
            GUI.Label(new Rect(rx + 15, ry + 12, boxW - 30, 20), "◈ SYSTEM DIAGNOSTIC OVERLAY", diagStyle);

            diagStyle.fontStyle = FontStyle.Normal;
            GUI.Label(new Rect(rx + 15, ry + 36, boxW - 30, 18), "MOTOR CONTROL ........ 78%", diagStyle);
            GUI.Label(new Rect(rx + 15, ry + 56, boxW - 30, 18), "BALANCE .............. 91%", diagStyle);
            GUI.Label(new Rect(rx + 15, ry + 76, boxW - 30, 18), "NEURAL RESPONSE ...... 84%", diagStyle);
            diagStyle.normal.textColor = new Color(1.0f, 0.35f, 0.35f);
            GUI.Label(new Rect(rx + 15, ry + 96, boxW - 30, 18), "COMBAT SYSTEM ........ LOCKED", diagStyle);
            diagStyle.normal.textColor = new Color(0f, 0.95f, 1.0f);
            GUI.Label(new Rect(rx + 15, ry + 116, boxW - 30, 18), "UNKNOWN PROTOCOL", diagStyle);
        }
        #endregion
    }
}
