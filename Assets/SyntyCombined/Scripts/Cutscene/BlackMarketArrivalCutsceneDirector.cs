using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using InsaneGun.Combat;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SyntyCombined.Cutscene
{
    /// <summary>
    /// Guardians of the Galaxy Themed Arrival Cutscene Director for DRIFT (Sector 4 Black Market).
    /// Orchestrates:
    /// 1. High-altitude hyperspace breach & atmospheric entry
    /// 2. Multi-camera dynamic choreography (4 cinematic camera angles)
    /// 3. Curvy flight path evaluation along BlackMarketFlightPathSO
    /// 4. Retro GotG Title Card splash ("DRIFT") with Guns N' Roses soundtrack
    /// 5. Reverse flare cushion touchdown onto central landing pad
    /// 6. Seamless transition into third-person gameplay with Alita
    /// </summary>
    public class BlackMarketArrivalCutsceneDirector : MonoBehaviour
    {
        public static BlackMarketArrivalCutsceneDirector Instance { get; private set; }

        public static bool ForcePlayCutsceneOnSceneLoad = false;

        [Header("Flight Path Configuration (SO)")]
        [Tooltip("The tweakable ScriptableObject defining the curvy arrival path, banking, speed, and handles")]
        public BlackMarketFlightPathSO flightPath;

        [Header("Timing")]
        [Range(10f, 60f)] public float cutsceneDuration = 30.0f;
        public bool playOnStart = true;
        public bool isPlaying { get; private set; }

        [Header("Scene Waypoints (Interactive Dragging)")]
        [Tooltip("Parent transform holding draggable FlightWaypointNode GameObjects in the scene hierarchy")]
        public Transform waypointsParent;

        [Header("Soundtrack Integration")]
        [Tooltip("Guns N' Roses - Sweet Child O' Mine (or user assigned rock track)")]
        public AudioClip sweetChildOMineClip;
        [Range(0f, 1f)] public float musicVolume = 0.85f;
        [Tooltip("Exact second in the cutscene when the soundtrack begins fading away (28.0s)")]
        public float musicFadeStartTime = 28.0f;
        [Tooltip("Total duration of the fade-away curve in seconds until completely silent")]
        public float musicFadeOutDuration = 3.5f;
        private AudioSource _musicSource;
        private Coroutine _musicFadeRoutine;

        [Header("Ship & Actor References")]
        public GameObject starship;
        public GameObject alitaHero;

        [Header("Cinematic Camera")]
        public Camera cinematicCamera;
        private Camera _gameplayCamera;
        private float _currentProgress = 0f;
        private Coroutine _cutsceneRoutine;
        private int _currentShotIndex = -1;

        [Header("Flyby Camera Shake")]
        [Tooltip("Maximum camera roll angle jitter in degrees during flyby")]
        public float maxShakeRoll = 3.2f;
        [Tooltip("Maximum camera pitch angle kick in degrees during flyby")]
        public float maxShakePitch = 2.4f;
        [Tooltip("Maximum lateral position offset in meters during flyby")]
        public float maxShakePosOffset = 0.25f;
        [Tooltip("How fast the camera shake trauma dissipates")]
        public float shakeDecay = 1.35f;
        private float _shakeTrauma = 0f;

        [Header("Flyby Sound FX")]
        public AudioClip flybyWhooshClip;
        private AudioSource _sfxSource;
        private bool _flybyAudioTriggered = false;

        // Ground landing pad target (SM_Bld_Corp_LandingPad_02)
        [Header("Landing Pad Target")]
        public Vector3 landingPadPos = new Vector3(27.48f, 1.86f, 34.00f);
        public Quaternion landingPadRot = Quaternion.Euler(0f, 180f, 0f);

        [Header("3D Trajectory Visualization")]
        [Tooltip("If true, a LineRenderer draws the path. Default is false so no lines are visible in game.")]
        public bool showTrajectoryLine = false;
        public LineRenderer trajectoryLineRenderer;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnsureFlightPathLoaded();
            SetupAudio();
            SetupCinematicCamera();
            SetupTrajectoryLineRenderer();
        }

        private void Start()
        {
            // If in Black Market scene and either playOnStart or ForcePlayCutsceneOnSceneLoad is true
            string currentScene = SceneManager.GetActiveScene().name;
            bool isBlackMarket = currentScene.Equals("Demo_BlackMarket_01", System.StringComparison.OrdinalIgnoreCase);

            if (isBlackMarket && (playOnStart || ForcePlayCutsceneOnSceneLoad))
            {
                ForcePlayCutsceneOnSceneLoad = false;
                StartCutscene();
            }
        }

        // While the arrival cutscene plays, Sweet Child O' Mine is the only sound: every other
        // AudioSource (ambience, engines, drones, music) is muted and restored when it ends.
        private readonly System.Collections.Generic.List<AudioSource> _mutedForCutscene = new System.Collections.Generic.List<AudioSource>();
        private float _nextMuteSweep;

        private void MuteAllOtherAudio()
        {
            foreach (var src in FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (src == null || src == _musicSource || src == _sfxSource || src.mute) continue;
                src.mute = true;
                _mutedForCutscene.Add(src);
            }
        }

        // The ship's engine and thruster loops are the loudest thing in the flyby: keep them silent every frame.
        private void MuteStarshipAudio()
        {
            if (starship == null) return;
            foreach (var src in starship.GetComponentsInChildren<AudioSource>(true))
            {
                if (src == null || src == _musicSource || src == _sfxSource) continue;
                if (!src.mute) { src.mute = true; _mutedForCutscene.Add(src); }
                if (src.isPlaying) src.Stop();
            }
        }

        private void RestoreOtherAudio()
        {
            foreach (var src in _mutedForCutscene)
                if (src != null) src.mute = false;
            _mutedForCutscene.Clear();
        }

        private void Update()
        {
            if (isPlaying)
            {
                if (Time.unscaledTime >= _nextMuteSweep)
                {
                    _nextMuteSweep = Time.unscaledTime + 0.05f;
                    MuteAllOtherAudio();
                }
                MuteStarshipAudio();

                // Allow skipping with Space or Escape
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape))
                {
                    SkipCutscene();
                }
            }
        }

        public void EnsureFlightPathLoaded()
        {
            if (flightPath == null)
            {
#if UNITY_EDITOR
                flightPath = AssetDatabase.LoadAssetAtPath<BlackMarketFlightPathSO>("Assets/SyntyCombined/Data/BlackMarket_Arrival_FlightPath.asset");
#endif
                if (flightPath == null)
                {
                    flightPath = Resources.Load<BlackMarketFlightPathSO>("BlackMarket_Arrival_FlightPath");
                }

                if (flightPath == null)
                {
                    flightPath = ScriptableObject.CreateInstance<BlackMarketFlightPathSO>();
                    flightPath.ResetToDefaultCurvyPath();
                }
            }

            // Auto-sanitize handles if any waypoint has corrupted or extreme values
            if (flightPath != null && flightPath.waypoints != null)
            {
                foreach (var wp in flightPath.waypoints)
                {
                    if (wp.handleIn.sqrMagnitude > 120f * 120f)
                    {
                        wp.handleIn = Vector3.ClampMagnitude(wp.handleIn, 60f);
                    }
                    if (wp.handleOut.sqrMagnitude > 120f * 120f)
                    {
                        wp.handleOut = Vector3.ClampMagnitude(wp.handleOut, 60f);
                    }
                }
            }
        }

        private void SetupAudio()
        {
            _musicSource = GetComponent<AudioSource>();
            if (_musicSource == null) _musicSource = gameObject.AddComponent<AudioSource>();

            _musicSource.playOnAwake = false;
            _musicSource.spatialBlend = 0f; // 2D Stereo full blast for GotG feel
            _musicSource.volume = musicVolume;

            // If clip unassigned, search for Sweet Child O' Mine Gundam Cut or fallback
            if (sweetChildOMineClip == null)
            {
#if UNITY_EDITOR
                sweetChildOMineClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/Sweet_Child_O_Mine_Gundam_Cut.mp3");
                if (sweetChildOMineClip == null)
                {
                    sweetChildOMineClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/Sweet_Child_O_Mine.mp3");
                }
                if (sweetChildOMineClip == null)
                {
                    sweetChildOMineClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/Cyberpunk_Combat_Metal_Heart.mp3");
                }
#endif
            }

            if (sweetChildOMineClip != null)
            {
                _musicSource.clip = sweetChildOMineClip;
            }

            // Setup dedicated SFX AudioSource for acoustic flyby blast
            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.playOnAwake = false;
            _sfxSource.spatialBlend = 0f;
            _sfxSource.volume = 1.0f;

            if (flybyWhooshClip == null)
            {
#if UNITY_EDITOR
                flybyWhooshClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Combat/SciFi_Starship_Flyby_Pass.mp3");
                if (flybyWhooshClip == null)
                {
                    flybyWhooshClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Combat/Dash_Cyber_01.wav");
                }
#endif
            }
        }

        private void SetupCinematicCamera()
        {
            if (cinematicCamera == null)
            {
                var camObj = transform.Find("CinematicCutsceneCamera");
                if (camObj != null)
                {
                    cinematicCamera = camObj.GetComponent<Camera>();
                }
                else
                {
                    GameObject go = new GameObject("CinematicCutsceneCamera");
                    go.transform.SetParent(transform, false);
                    cinematicCamera = go.AddComponent<Camera>();
                    cinematicCamera.nearClipPlane = 0.3f;
                    cinematicCamera.farClipPlane = 3500f;
                    cinematicCamera.fieldOfView = 60f;
                }
            }

            cinematicCamera.farClipPlane = 3500f;

            // Ensure Cinematic camera has AudioListener
            var listener = cinematicCamera.GetComponent<AudioListener>();
            if (listener == null)
            {
                listener = cinematicCamera.gameObject.AddComponent<AudioListener>();
            }
            listener.enabled = false;

            cinematicCamera.gameObject.SetActive(false);
        }

        [ContextMenu("Play Cutscene")]
        public void StartCutscene()
        {
            if (isPlaying) return;

            _currentShotIndex = -1;
            EnsureFlightPathLoaded();
            FindOrSpawnShipAndAlita();

            if (_cutsceneRoutine != null) StopCoroutine(_cutsceneRoutine);
            _cutsceneRoutine = StartCoroutine(PlayCutsceneSequenceRoutine());
        }

        public void SkipCutscene()
        {
            if (!isPlaying) return;

            if (_cutsceneRoutine != null) StopCoroutine(_cutsceneRoutine);
            FadeOutMusic(1.0f);
            FinishCutscene(skipped: true);
        }

        private void FindOrSpawnShipAndAlita()
        {
            if (starship == null)
            {
                starship = GameObject.Find("Alita_Lost_Martian_Starship");
                if (starship == null) starship = GameObject.Find("SM_Veh_HoverCraft_01");
                if (starship == null)
                {
                    var vehiclesParent = GameObject.Find("Vehicles");
                    if (vehiclesParent != null && vehiclesParent.transform.childCount > 0)
                    {
                        starship = vehiclesParent.transform.GetChild(0).gameObject;
                    }
                }
            }

            if (alitaHero == null)
            {
                alitaHero = GameObject.Find("Alita_BattleAngel_Hero");
                if (alitaHero == null)
                {
                    var players = GameObject.FindGameObjectsWithTag("Player");
                    if (players != null && players.Length > 0) alitaHero = players[0];
                }
            }

            // Disable player control during cutscene and hide Alita until landing
            if (alitaHero != null)
            {
                var cc = alitaHero.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                var sword = alitaHero.GetComponent<InsaneGunSwordController>();
                if (sword != null) sword.enabled = false;

                // Hide Alita so she does not stand awkwardly on the empty landing pad before arrival
                alitaHero.gameObject.SetActive(false);
            }
        }

        private IEnumerator PlayCutsceneSequenceRoutine()
        {
            isPlaying = true;
            _currentProgress = 0f;
            _shakeTrauma = 0f;
            _flybyAudioTriggered = false;

            // 0. Ensure Global Audio is active and unmuted
            AudioListener.volume = 1f;
            AudioListener.pause = false;

            // Ensure trajectory path lines are completely invisible in game
            if (trajectoryLineRenderer != null)
            {
                trajectoryLineRenderer.enabled = false;
                trajectoryLineRenderer.gameObject.SetActive(false);
            }
            var lineChild = transform.Find("Trajectory_Line_Renderer");
            if (lineChild != null)
            {
                var lr = lineChild.GetComponent<LineRenderer>();
                if (lr != null) lr.enabled = false;
                lineChild.gameObject.SetActive(false);
            }

            // 1. Switch to cinematic camera and activate its AudioListener
            _gameplayCamera = Camera.main;
            if (_gameplayCamera != null && _gameplayCamera != cinematicCamera)
            {
                var gpListener = _gameplayCamera.GetComponent<AudioListener>();
                if (gpListener != null) gpListener.enabled = false;
                _gameplayCamera.gameObject.SetActive(false);
            }

            cinematicCamera.gameObject.SetActive(true);
            var cinListener = cinematicCamera.GetComponent<AudioListener>();
            if (cinListener == null) cinListener = cinematicCamera.gameObject.AddComponent<AudioListener>();
            cinListener.enabled = true;

            // 2. Play Guns N' Roses rock soundtrack
            if (_musicFadeRoutine != null)
            {
                StopCoroutine(_musicFadeRoutine);
                _musicFadeRoutine = null;
            }

            if (_musicSource != null && _musicSource.clip != null)
            {
                _musicSource.time = 0f;
                _musicSource.volume = musicVolume;
                _musicSource.Play();
            }

            // 3. Setup Title Card UI
            var titleUI = GotGTitleCardUI.GetOrCreate();

            // Initial ship placement at start of path
            if (flightPath != null && flightPath.waypoints.Count > 0 && starship != null)
            {
                flightPath.Evaluate(0f, out Vector3 startPos, out Quaternion startRot, out _, out _, out _);
                starship.transform.position = startPos;
                starship.transform.rotation = startRot;

                var rb = starship.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }

                // Temporarily disable starship colliders during the cutscene flight
                // to prevent any physics depenetration jitter or collision fighting with the landing pad
                var cols = starship.GetComponentsInChildren<Collider>();
                foreach (var c in cols)
                {
                    if (c != null) c.enabled = false;
                }

                var flightCtrl = starship.GetComponent<SpaceshipFlightController>();
                if (flightCtrl != null)
                {
                    flightCtrl.enabled = false; // Director drives transform directly
                }
            }

            // High altitude hypersonic warp breach flash
            SpawnWarpBreachVFX(starship != null ? starship.transform.position : Vector3.zero);

            bool titleCardTriggered = false;
            bool musicFadeInitiated = false;
            float elapsed = 0f;

            while (elapsed < cutsceneDuration)
            {
                elapsed += Time.deltaTime;
                _currentProgress = Mathf.Clamp01(elapsed / cutsceneDuration);

                // Update Starship along Bézier curve with butter-smooth rotation filtering
                if (flightPath != null && starship != null)
                {
                    flightPath.Evaluate(_currentProgress, out Vector3 p, out Quaternion r, out float spd, out float thruster, out float fov);
                    starship.transform.position = p;
                    // Low-pass rotation filtering to eliminate any frame-time micro-jitter
                    starship.transform.rotation = Quaternion.Slerp(starship.transform.rotation, r, Time.deltaTime * 24f);

                    cinematicCamera.fieldOfView = Mathf.Lerp(cinematicCamera.fieldOfView, fov, Time.deltaTime * 3.5f);
                }

                // Camera Choreography (4 distinct dynamic shots)
                UpdateCameraChoreography(_currentProgress);

                // Shot 2 Trigger: GotG retro Title Drop "DRIFT"
                if (!titleCardTriggered && _currentProgress >= 0.24f)
                {
                    titleCardTriggered = true;
                    if (titleUI != null)
                    {
                        titleUI.ShowTitleCard("DRIFT", "SECTOR 04 // UNREGULATED OUTLAW MARKET", "CYGNUS-9 EXPEDITION  •  LOC: 39.402, -112.84  •  STATUS: UNGOVERNED", 5.2f);
                    }
                }

                // Shot 4: Smooth musical fade-away into silence starting at the exact 28.0s mark as ship flares and touches down
                if (!musicFadeInitiated && elapsed >= musicFadeStartTime)
                {
                    musicFadeInitiated = true;
                    FadeOutMusic(musicFadeOutDuration);
                }

                yield return null;
            }

            FinishCutscene(skipped: false);
        }

        // User's exact SceneView vista camera angle
        private readonly Vector3 SCENE_VIEW_VISTA_POS = new Vector3(-129.46f, 198.08f, 951.76f);
        private readonly Quaternion SCENE_VIEW_VISTA_ROT = Quaternion.Euler(9.77f, 185.11f, 0.00f);

        private void UpdateCameraChoreography(float t)
        {
            if (cinematicCamera == null || starship == null) return;

            Vector3 shipPos = starship.transform.position;
            Quaternion shipRot = starship.transform.rotation;

            // --- SHOT 1: Deep Space Establishing Shot (User's Exact SceneView Angle) (0.00 to 0.28) ---
            if (t < 0.28f)
            {
                if (_currentShotIndex != 0)
                {
                    _currentShotIndex = 0;
                    cinematicCamera.transform.position = SCENE_VIEW_VISTA_POS;
                    cinematicCamera.transform.rotation = SCENE_VIEW_VISTA_ROT;
                    cinematicCamera.fieldOfView = 60f;
                }

                float shotT = t / 0.28f;
                // Subtle epic forward drift keeping the fractured sphere & starry nebula perfectly framed
                Vector3 driftPos = Vector3.Lerp(SCENE_VIEW_VISTA_POS, SCENE_VIEW_VISTA_POS + SCENE_VIEW_VISTA_ROT * Vector3.forward * 45f, shotT);
                Quaternion driftRot = SCENE_VIEW_VISTA_ROT;

                // Detect when ship roars past from behind overhead
                float distToCam = Vector3.Distance(shipPos, driftPos);
                if (distToCam < 24f)
                {
                    float proximity = Mathf.Clamp01(1f - (distToCam / 24f));
                    _shakeTrauma = Mathf.Max(_shakeTrauma, proximity);

                    // Trigger flyby sonic thruster blast SFX
                    if (proximity > 0.55f && !_flybyAudioTriggered)
                    {
                        _flybyAudioTriggered = true;
                        // The ship pass-by sound is the one effect allowed alongside the music
                        if (_sfxSource != null && flybyWhooshClip != null)
                        {
                            _sfxSource.PlayOneShot(flybyWhooshClip, 1.0f);
                        }
                    }
                }

                // Apply procedural flyby acoustic shake
                if (_shakeTrauma > 0.001f)
                {
                    _shakeTrauma = Mathf.MoveTowards(_shakeTrauma, 0f, Time.deltaTime * shakeDecay);
                    float intensity = _shakeTrauma * _shakeTrauma; // non-linear response

                    float time = Time.time * 38f;
                    float offsetX = (Mathf.PerlinNoise(time, 0.2f) * 2f - 1f) * maxShakePosOffset * intensity;
                    float offsetY = (Mathf.PerlinNoise(0.7f, time) * 2f - 1f) * maxShakePosOffset * intensity;
                    float shakeRoll = (Mathf.PerlinNoise(3.4f, time * 1.15f) * 2f - 1f) * maxShakeRoll * intensity;
                    float shakePitch = (Mathf.PerlinNoise(time * 0.95f, 1.8f) * 2f - 1f) * maxShakePitch * intensity;

                    driftPos += driftRot * new Vector3(offsetX, offsetY, 0f);
                    driftRot *= Quaternion.Euler(shakePitch, 0f, shakeRoll);
                }

                cinematicCamera.transform.position = driftPos;
                cinematicCamera.transform.rotation = driftRot;
                cinematicCamera.fieldOfView = 60f;
            }
            // --- SHOT 2: Threading the Fractured Shell & GotG Title Drop "DRIFT" (0.28 to 0.55) ---
            else if (t < 0.55f)
            {
                Vector3 shot2Pos = new Vector3(-142f, 122f, 385f);
                if (_currentShotIndex != 1)
                {
                    _currentShotIndex = 1;
                    cinematicCamera.transform.position = shot2Pos;
                    cinematicCamera.transform.rotation = Quaternion.LookRotation(shipPos - shot2Pos);
                    cinematicCamera.fieldOfView = 62f;
                }

                cinematicCamera.transform.position = Vector3.Lerp(cinematicCamera.transform.position, shot2Pos, Time.deltaTime * 6f);
                cinematicCamera.transform.rotation = Quaternion.Slerp(cinematicCamera.transform.rotation, Quaternion.LookRotation(shipPos - cinematicCamera.transform.position), Time.deltaTime * 10f);
            }
            // --- SHOT 3: Dynamic Station Core Weave Past Cranes & Refinery (0.55 to 0.80) ---
            else if (t < 0.80f)
            {
                Vector3 followOffset = shipRot * new Vector3(-4.8f, 2.2f, -11.5f);
                Vector3 targetCamPos = shipPos + followOffset;
                Vector3 lookPoint = shipPos + shipRot * new Vector3(0.5f, 0.8f, 8.0f);

                if (_currentShotIndex != 2)
                {
                    _currentShotIndex = 2;
                    cinematicCamera.transform.position = targetCamPos;
                    cinematicCamera.transform.rotation = Quaternion.LookRotation(lookPoint - targetCamPos);
                    cinematicCamera.fieldOfView = 65f;
                }

                cinematicCamera.transform.position = Vector3.Lerp(cinematicCamera.transform.position, targetCamPos, Time.deltaTime * 7.5f);
                cinematicCamera.transform.rotation = Quaternion.Slerp(cinematicCamera.transform.rotation, Quaternion.LookRotation(lookPoint - cinematicCamera.transform.position), Time.deltaTime * 8.5f);
            }
            // --- SHOT 4: Elevated Tarmac Apron Landing & Touchdown (0.80 to 1.00) ---
            else
            {
                // Elevated tarmac apron camera: perfectly frames the entire landing pad with 100% clear sightline
                Vector3 padCamPos = landingPadPos + new Vector3(5.5f, 3.2f, -12.0f);

                if (_currentShotIndex != 3)
                {
                    _currentShotIndex = 3;
                    cinematicCamera.transform.position = padCamPos;
                    cinematicCamera.transform.rotation = Quaternion.LookRotation(shipPos + Vector3.up * 1.0f - padCamPos);
                    cinematicCamera.fieldOfView = 58f;
                }

                // Smooth cinematic tracking as ship flares repulsors, levels flat, and settles onto pad
                cinematicCamera.transform.position = padCamPos;
                cinematicCamera.transform.rotation = Quaternion.Slerp(cinematicCamera.transform.rotation, Quaternion.LookRotation(shipPos + Vector3.up * 1.0f - cinematicCamera.transform.position), Time.deltaTime * 8f);
                cinematicCamera.fieldOfView = Mathf.Lerp(cinematicCamera.fieldOfView, 54f, Time.deltaTime * 2.5f);
            }
        }

        private void FinishCutscene(bool skipped)
        {
            isPlaying = false;
            RestoreOtherAudio();
            _currentShotIndex = -1;

            // 1. Settle starship firmly on landing pad
            if (starship != null)
            {
                starship.transform.position = landingPadPos;
                starship.transform.rotation = landingPadRot;

                var rb = starship.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }

                // Re-enable starship colliders once firmly settled on the pad
                var cols = starship.GetComponentsInChildren<Collider>();
                foreach (var c in cols)
                {
                    if (c != null) c.enabled = true;
                }

                var flightCtrl = starship.GetComponent<SpaceshipFlightController>();
                if (flightCtrl != null)
                {
                    flightCtrl.enabled = true;
                    flightCtrl.SetBoardingCooldown(3.5f); // Prevent immediate accidental re-boarding
                }
            }

            // 2. Hide Title Card UI & Letterbox
            var titleUI = GotGTitleCardUI.GetOrCreate();
            if (titleUI != null)
            {
                titleUI.HideTitleCard(instant: skipped);
                titleUI.SetLetterboxOnly(false, 0.8f);
            }

            // 3. Immediately re-enable Alita Hero and position beside cockpit on flat tarmac
            if (alitaHero != null)
            {
                alitaHero.gameObject.SetActive(true);
                var cc = alitaHero.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;

                // Safe touchdown exit position beside starship cockpit on the pad (no wing collision)
                Vector3 exitPos = new Vector3(22.80f, 1.88f, 33.50f);
                alitaHero.transform.position = exitPos;
                alitaHero.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                if (cc != null) cc.enabled = true;

                var sword = alitaHero.GetComponent<InsaneGunSwordController>();
                if (sword != null)
                {
                    sword.enabled = true;
                    sword.currentHealth = sword.maxHealth;
                }

                var anim = alitaHero.GetComponentInChildren<Animator>();
                if (anim != null) anim.enabled = true;
            }

            // 4. Switch from cinematic camera to 3rd-person gameplay camera behind Alita
            if (cinematicCamera != null)
            {
                var cinListener = cinematicCamera.GetComponent<AudioListener>();
                if (cinListener != null) cinListener.enabled = false;
                cinematicCamera.gameObject.SetActive(false);
            }

            Camera mainCam = _gameplayCamera != null ? _gameplayCamera : Camera.main;
            if (mainCam == null)
            {
                var go = GameObject.Find("Main Camera");
                if (go != null) mainCam = go.GetComponent<Camera>();
            }

            if (mainCam != null)
            {
                mainCam.gameObject.SetActive(true);
                var gpListener = mainCam.GetComponent<AudioListener>();
                if (gpListener == null) gpListener = mainCam.gameObject.AddComponent<AudioListener>();
                gpListener.enabled = true;

                if (alitaHero != null)
                {
                    var orbit = mainCam.GetComponent<InsaneGun.Combat.SmoothOrbitCamera>();
                    if (orbit == null) orbit = mainCam.gameObject.AddComponent<InsaneGun.Combat.SmoothOrbitCamera>();
                    orbit.targetOffset = new Vector3(0f, 1.45f, 0f);
                    orbit.cameraOffset = new Vector3(0.48f, 0.08f, -2.7f);
                    orbit.enabled = true;
                    orbit.SetTarget(alitaHero.transform);
                }

                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            // 5. Music graceful fade-away into complete silence
            if (!skipped)
            {
                if (_musicFadeRoutine == null && _musicSource != null && _musicSource.isPlaying)
                {
                    FadeOutMusic(musicFadeOutDuration);
                }
            }

            // 6. Update Cyberpunk HUD Objective
            if (CyberpunkHUDController.Instance != null)
            {
                CyberpunkHUDController.Instance.SetQuestObjective(
                    "DRIFT: UNREGULATED MARKET",
                    "► Locate The Collector inside the skull-marked trophy sanctum to track your core"
                );
                CyberpunkHUDController.Instance.ShowToast(
                    "TOUCHDOWN COMPLETE // WELCOME TO DRIFT OUTLAW MARKET",
                    5.0f
                );
            }

            // Ensure Black Market NPCs and Shopkeepers are spawned and ready
            SyntyCombined.BlackMarket.BlackMarketRuntimeSpawner.SpawnEcosystem();
            var bmHud = SyntyCombined.BlackMarket.BlackMarketNPCDialogueHUD.GetOrCreate();
            if (bmHud != null)
            {
                bmHud.ShowMissionBanner("OBJECTIVE: FIND THE COLLECTOR IN THE SKULL-MARKED POD");
            }

            Debug.Log("<color=#FFD700><b>[DRIFT Cutscene]</b></color> Arrival cutscene finished successfully. Player in command.");
        }

        public void FadeOutMusic(float duration)
        {
            if (_musicSource == null || !_musicSource.isPlaying) return;

            if (_musicFadeRoutine != null) StopCoroutine(_musicFadeRoutine);
            _musicFadeRoutine = StartCoroutine(FadeOutMusicRoutine(duration));
        }

        private IEnumerator FadeOutMusicRoutine(float duration)
        {
            if (_musicSource == null) yield break;

            float startVol = _musicSource.volume;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Cinematic S-curve falloff (SmoothStep)
                float curve = Mathf.SmoothStep(1f, 0f, t);
                if (_musicSource != null)
                {
                    _musicSource.volume = startVol * curve;
                }
                yield return null;
            }

            if (_musicSource != null)
            {
                _musicSource.volume = 0f;
                _musicSource.Stop();
            }
            _musicFadeRoutine = null;
        }

        private void SpawnWarpBreachVFX(Vector3 pos)
        {
            var breachObj = new GameObject("Hyperspace_Breach_Flash");
            breachObj.transform.position = pos;
            var l = breachObj.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(0.2f, 0.85f, 1.0f);
            l.intensity = 18f;
            l.range = 80f;

            StartCoroutine(FlashDecayRoutine(breachObj, l, 1.2f));
        }

        private IEnumerator FlashDecayRoutine(GameObject go, Light l, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                if (l != null) l.intensity = Mathf.Lerp(18f, 0f, elapsed / duration);
                yield return null;
            }
            if (go != null) Destroy(go);
        }

        public void SetupTrajectoryLineRenderer()
        {
            if (!showTrajectoryLine || Application.isPlaying)
            {
                if (trajectoryLineRenderer != null)
                {
                    trajectoryLineRenderer.enabled = false;
                    trajectoryLineRenderer.gameObject.SetActive(false);
                }
                var existingLine = transform.Find("Trajectory_Line_Renderer");
                if (existingLine != null)
                {
                    var lr = existingLine.GetComponent<LineRenderer>();
                    if (lr != null) lr.enabled = false;
                    existingLine.gameObject.SetActive(false);
                }
                return;
            }

            EnsureFlightPathLoaded();
            if (flightPath == null) return;

            if (trajectoryLineRenderer == null)
            {
                var lineObj = transform.Find("Trajectory_Line_Renderer");
                if (lineObj != null)
                {
                    trajectoryLineRenderer = lineObj.GetComponent<LineRenderer>();
                }
                else
                {
                    var go = new GameObject("Trajectory_Line_Renderer");
                    go.transform.SetParent(transform, false);
                    trajectoryLineRenderer = go.AddComponent<LineRenderer>();
                }
            }

            if (trajectoryLineRenderer != null)
            {
                trajectoryLineRenderer.enabled = true;
                trajectoryLineRenderer.useWorldSpace = true;
                trajectoryLineRenderer.startWidth = 0.08f;
                trajectoryLineRenderer.endWidth = 0.04f;
                trajectoryLineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trajectoryLineRenderer.receiveShadows = false;

                // Simple additive/unlit material
                var shader = Shader.Find("Sprites/Default");
                if (shader != null) trajectoryLineRenderer.material = new Material(shader);

                // Cyan to gold gradient
                Gradient grad = new Gradient();
                grad.SetKeys(
                    new GradientColorKey[] {
                        new GradientColorKey(new Color(0f, 0.95f, 1f), 0.0f),
                        new GradientColorKey(new Color(0.9f, 0.2f, 1f), 0.45f),
                        new GradientColorKey(new Color(1f, 0.85f, 0.05f), 1.0f)
                    },
                    new GradientAlphaKey[] {
                        new GradientAlphaKey(0.85f, 0.0f),
                        new GradientAlphaKey(0.95f, 0.5f),
                        new GradientAlphaKey(0.85f, 1.0f)
                    }
                );
                trajectoryLineRenderer.colorGradient = grad;

                int count = 90;
                trajectoryLineRenderer.positionCount = count;
                for (int i = 0; i < count; i++)
                {
                    float t = (float)i / (count - 1);
                    flightPath.Evaluate(t, out Vector3 p, out _, out _, out _, out _);
                    trajectoryLineRenderer.SetPosition(i, p);
                }
            }
        }

        [ContextMenu("Spawn Draggable Waypoint Objects")]
        public void SpawnSceneWaypointObjects()
        {
            EnsureFlightPathLoaded();
            if (flightPath == null || flightPath.waypoints == null) return;

            ClearSceneWaypointObjects();

            GameObject parentGO = new GameObject("[Flight_Path_Waypoints]");
            parentGO.transform.SetParent(transform, false);
            waypointsParent = parentGO.transform;

            for (int i = 0; i < flightPath.waypoints.Count; i++)
            {
                var wp = flightPath.waypoints[i];
                GameObject nodeGO = new GameObject($"Waypoint_{i + 1:D2}_{wp.label}");
                nodeGO.transform.SetParent(waypointsParent, false);
                nodeGO.transform.position = wp.position;

                var node = nodeGO.AddComponent<FlightWaypointNode>();
                node.InitializeFromWaypoint(i, wp, flightPath, this);
            }

            SetupTrajectoryLineRenderer();
            Debug.Log($"<color=#00F0FF><b>[Flight Path]</b></color> Spawned {flightPath.waypoints.Count} draggable waypoint objects in scene! Click any waypoint in Scene View or Hierarchy to drag and shape the trajectory.");
        }

        [ContextMenu("Clear Scene Waypoint Objects")]
        public void ClearSceneWaypointObjects()
        {
            if (waypointsParent != null)
            {
#if UNITY_EDITOR
                if (Application.isPlaying)
                    Destroy(waypointsParent.gameObject);
                else
                    DestroyImmediate(waypointsParent.gameObject);
#else
                Destroy(waypointsParent.gameObject);
#endif
                waypointsParent = null;
            }
            else
            {
                var existing = transform.Find("[Flight_Path_Waypoints]");
                if (existing != null)
                {
#if UNITY_EDITOR
                    if (Application.isPlaying)
                        Destroy(existing.gameObject);
                    else
                        DestroyImmediate(existing.gameObject);
#else
                    Destroy(existing.gameObject);
#endif
                }
            }
        }

        [ContextMenu("Bake Scene Waypoints to Asset")]
        public void BakeSceneWaypointsToSO()
        {
            if (flightPath == null) EnsureFlightPathLoaded();
            if (flightPath == null) return;

            Transform container = waypointsParent;
            if (container == null) container = transform.Find("[Flight_Path_Waypoints]");
            if (container == null)
            {
                Debug.LogWarning("[Flight Path] No [Flight_Path_Waypoints] container found in scene to bake!");
                return;
            }

            var nodes = container.GetComponentsInChildren<FlightWaypointNode>();
            if (nodes == null || nodes.Length == 0) return;

            for (int i = 0; i < nodes.Length; i++)
            {
                nodes[i].SyncToScriptableObject();
            }

#if UNITY_EDITOR
            EditorUtility.SetDirty(flightPath);
            AssetDatabase.SaveAssets();
#endif
            SetupTrajectoryLineRenderer();
            Debug.Log($"<color=#FFD700><b>[Flight Path]</b></color> Baked {nodes.Length} scene waypoints to {flightPath.name}.asset!");
        }

        private void OnDrawGizmos()
        {
            if (flightPath == null) EnsureFlightPathLoaded();
            if (flightPath == null || flightPath.waypoints == null || flightPath.waypoints.Count == 0) return;

            int count = 80;
            Vector3 prev = Vector3.zero;
            for (int i = 0; i <= count; i++)
            {
                float t = (float)i / count;
                flightPath.Evaluate(t, out Vector3 curr, out _, out _, out _, out _);
                if (i > 0)
                {
                    Gizmos.color = Color.Lerp(new Color(0f, 0.95f, 1f, 0.9f), new Color(1f, 0.85f, 0.1f, 0.9f), t);
                    Gizmos.DrawLine(prev, curr);
                }
                prev = curr;
            }

            // Draw waypoints
            for (int i = 0; i < flightPath.waypoints.Count; i++)
            {
                var wp = flightPath.waypoints[i];
                Gizmos.color = (i == flightPath.waypoints.Count - 1) ? Color.green : (i == 0 ? Color.cyan : Color.yellow);
                Gizmos.DrawWireSphere(wp.position, 1.5f);
            }

            // Draw Landing Pad outline
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.85f);
            Gizmos.DrawWireCube(landingPadPos, new Vector3(8f, 0.3f, 8f));
        }
    }
}
