using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SyntyCombined;
using SyntyCombined.BlackMarket;
using SyntyCombined.World;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SyntyCombined.World
{
    public enum BetrayalPhase
    {
        NotStarted,
        WaitingForCoreReturn,
        ConfrontingCollector,
        CoordinatesSpawned,
        CoordinatesSecured,
        ExitedBuilding_TankAttacking,
        EscapedToStarship
    }

    /// <summary>
    /// Manages the narrative climax on Sector 04:
    /// 1. Alita returns to The Collector's Sanctuary Pod after collecting/restoring the core(s).
    /// 2. The Collector betrays her: reveals Omni-Corp's 100,000 credit bounty on her intact URM chassis.
    /// 3. Alita snatches the decrypted Martian hyperspace coordinates datapad from the console desk.
    /// 4. As soon as Alita flees the building through the front door, the Heavy Hover Tank in the garage powers up and attacks her!
    /// 5. Alita survives the assault and escapes to her starship on Landing Pad B-04 to warp to Mars.
    /// </summary>
    public class CollectorBetrayalManager : MonoBehaviour
    {
        private static CollectorBetrayalManager _instance;
        public static CollectorBetrayalManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<CollectorBetrayalManager>() ?? FindObjectOfType<CollectorBetrayalManager>();
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("Current Phase")]
        public BetrayalPhase currentPhase = BetrayalPhase.NotStarted;

        [Header("Scene References")]
        public BlackMarketNPC collectorNPC;
        public SyndicateHoverTankAI hoverTankAI;
        public GameObject coordinatesDatapadProp;

        [Header("Coordinates Datapad Settings")]
        public Vector3 datapadDeskPos = new Vector3(5.20f, 0.98f, -9.10f);

        [Header("Exit Trigger Settings")]
        public Vector3 exitTriggerCenter = new Vector3(-1.20f, 0.80f, -10.25f);
        public Vector3 exitTriggerSize = new Vector3(4.0f, 3.5f, 4.0f);

        [Header("Landing Pad & Mars Warp Escape")]
        public Vector3 landingPadPos = new Vector3(27.48f, 1.86f, 34.00f);
        public string marsSceneName = "Scene_Mars_FinalBattle";
        public SpaceshipFlightController starshipFlightController;

        [Header("Alarm & FX")]
        public Color emergencyRedColor = new Color(1.0f, 0.05f, 0.05f);
        public AudioClip sirenAlarmSFX;
        public AudioClip datapadPickupSFX;

        // Internal State
        private Transform _playerTransform;
        private Light _alarmStrobe;
        private bool _isStrobeActive = false;
        private float _strobeTimer = 0f;
        private bool _isPlayerNearDatapad = false;
        private GameObject _extractionBeacon;
        private bool _isPlayerNearStarship = false;
        private float _distanceToStarship = 999f;
        private bool _isTransitioningToMars = false;
        private float _warpTransitionAlpha = 0f;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            SetupComponents();
        }

        private void Start()
        {
            FindSceneReferences();
            SetupDatapadProp();
            SetupAlarmLighting();
            SetupExtractionBeacon();
        }

        private void SetupComponents()
        {
            // Create trigger collider for building exit
            var boxCol = GetComponent<BoxCollider>();
            if (boxCol == null)
            {
                boxCol = gameObject.AddComponent<BoxCollider>();
            }
            boxCol.isTrigger = true;
            boxCol.center = exitTriggerCenter;
            boxCol.size = exitTriggerSize;
        }

        public void FindSceneReferences()
        {
            if (_playerTransform == null)
            {
                var p = GameObject.Find("Alita_BattleAngel_Hero") ?? GameObject.FindWithTag("Player");
                if (p != null) _playerTransform = p.transform;
            }

            if (collectorNPC == null)
            {
                var colObj = GameObject.Find("NPC_The_Collector_Kaelen_Voss");
                if (colObj != null) collectorNPC = colObj.GetComponent<BlackMarketNPC>();
            }

            if (hoverTankAI == null)
            {
                var tankObj = GameObject.Find("SM_Veh_HoverTank_03");
                if (tankObj != null)
                {
                    hoverTankAI = tankObj.GetComponent<SyndicateHoverTankAI>();
                    if (hoverTankAI == null) hoverTankAI = tankObj.AddComponent<SyndicateHoverTankAI>();
                }
            }

            if (starshipFlightController == null)
            {
                var shipObj = GameObject.Find("Alita_Lost_Martian_Starship");
                if (shipObj != null) starshipFlightController = shipObj.GetComponent<SpaceshipFlightController>();
            }
        }

        private void SetupExtractionBeacon()
        {
            if (_extractionBeacon != null) return;
            var existing = GameObject.Find("Extract_Beacon_Mars_LandingPad");
            if (existing != null)
            {
                _extractionBeacon = existing;
                _extractionBeacon.SetActive(false);
                return;
            }

            _extractionBeacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _extractionBeacon.name = "Extract_Beacon_Mars_LandingPad";
            _extractionBeacon.transform.position = landingPadPos + Vector3.up * 0.1f;
            _extractionBeacon.transform.localScale = new Vector3(4.5f, 0.05f, 4.5f);

            var col = _extractionBeacon.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }

            var mr = _extractionBeacon.GetComponent<MeshRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader);
            mat.color = new Color(0.1f, 0.85f, 1.0f, 0.5f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(0.1f, 1.2f, 1.6f));
            mr.material = mat;

            var light = _extractionBeacon.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.1f, 0.9f, 1.0f);
            light.range = 14f;
            light.intensity = 2.5f;

            _extractionBeacon.SetActive(false);
        }

        private void SetupDatapadProp()
        {
            if (coordinatesDatapadProp != null) return;

            // Look for existing or create new glowing Mars coordinates datapad
            var existing = GameObject.Find("Mars_Hyperspace_Coordinates_Datapad");
            if (existing != null)
            {
                coordinatesDatapadProp = existing;
                coordinatesDatapadProp.SetActive(false);
                return;
            }

            // Create sleek glowing datapad
            coordinatesDatapadProp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            coordinatesDatapadProp.name = "Mars_Hyperspace_Coordinates_Datapad";
            coordinatesDatapadProp.transform.position = datapadDeskPos;
            coordinatesDatapadProp.transform.rotation = Quaternion.Euler(-15f, 40f, 0f);
            coordinatesDatapadProp.transform.localScale = new Vector3(0.35f, 0.04f, 0.25f);

            var mr = coordinatesDatapadProp.GetComponent<MeshRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader);
            mat.color = new Color(0.1f, 0.9f, 1.0f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(0.1f, 1.2f, 1.8f));
            mr.material = mat;

            // Add intense cyan point light
            var l = coordinatesDatapadProp.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(0.1f, 0.9f, 1.0f);
            l.range = 3.5f;
            l.intensity = 2.5f;

            // Trigger collider
            var col = coordinatesDatapadProp.GetComponent<BoxCollider>();
            if (col != null) col.isTrigger = true;

            coordinatesDatapadProp.SetActive(false);
        }

        private void SetupAlarmLighting()
        {
            if (_alarmStrobe != null) return;
            var strobeObj = new GameObject("Sanctuary_Emergency_Alarm_Strobe");
            strobeObj.transform.position = new Vector3(0.0f, 3.5f, -10.25f);
            _alarmStrobe = strobeObj.AddComponent<Light>();
            _alarmStrobe.type = LightType.Point;
            _alarmStrobe.color = emergencyRedColor;
            _alarmStrobe.range = 28f;
            _alarmStrobe.intensity = 0f;
            _alarmStrobe.enabled = false;
        }

        private void Update()
        {
            // Developer cheat / testing shortcuts:
            // [F9] instantly triggers Betrayal Sequence inside Pod!
            if (Input.GetKeyDown(KeyCode.F9))
            {
                TestBetrayalSequence();
            }

            // [F10] instantly triggers Mars Hyperspace Jump!
            if (Input.GetKeyDown(KeyCode.F10))
            {
                TestMarsJumpShortcut();
            }

            // Animate alarm strobe if active
            if (_isStrobeActive && _alarmStrobe != null)
            {
                _strobeTimer += Time.deltaTime * 6.5f;
                _alarmStrobe.intensity = Mathf.PingPong(_strobeTimer, 1.0f) * 6.0f;
            }

            // Animate hovering datapad bobbing & rotation when spawned
            if (currentPhase == BetrayalPhase.CoordinatesSpawned && coordinatesDatapadProp != null && coordinatesDatapadProp.activeSelf)
            {
                coordinatesDatapadProp.transform.Rotate(Vector3.up, 45f * Time.deltaTime, Space.World);

                if (_playerTransform != null)
                {
                    float dist = Vector3.Distance(_playerTransform.position, coordinatesDatapadProp.transform.position);
                    _isPlayerNearDatapad = dist <= 3.2f;

                    // Interact with [E] or walk close into it to snatch coordinates
                    if (_isPlayerNearDatapad && (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F) || dist < 1.4f))
                    {
                        SnatchCoordinates();
                    }
                }
            }

            // Proximity check for escaping to starship on Landing Pad B-04
            if (currentPhase == BetrayalPhase.CoordinatesSecured)
            {
                if (_playerTransform == null) FindSceneReferences();
                if (_playerTransform != null && _playerTransform.position.x < -0.4f)
                {
                    OnPlayerExitedBuilding();
                }
            }

            if (currentPhase == BetrayalPhase.CoordinatesSecured || currentPhase == BetrayalPhase.ExitedBuilding_TankAttacking)
            {
                if (_playerTransform == null) FindSceneReferences();

                if (_playerTransform != null)
                {
                    Vector3 shipPos = (starshipFlightController != null) ? starshipFlightController.transform.position : landingPadPos;
                    _distanceToStarship = Vector3.Distance(_playerTransform.position, shipPos);
                    _isPlayerNearStarship = _distanceToStarship <= 8.5f;

                    // When Alita reaches the starship, press [E] or [F] or [R] to launch and jump to Mars!
                    if (_isPlayerNearStarship && !_isTransitioningToMars)
                    {
                        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.R))
                        {
                            TriggerEscapeToMars();
                        }
                    }
                }

                // Animate extraction beacon pulse if active
                if (_extractionBeacon != null && _extractionBeacon.activeSelf)
                {
                    var light = _extractionBeacon.GetComponent<Light>();
                    if (light != null)
                    {
                        light.intensity = 2.0f + Mathf.PingPong(Time.time * 4f, 2.5f);
                    }
                }
            }
        }

        /// <summary>
        /// Called when the player finishes the betrayal conversation with The Collector.
        /// </summary>
        public void OnCollectorBetrayalDialogueFinished()
        {
            currentPhase = BetrayalPhase.CoordinatesSpawned;

            // Spawn & Activate Glowing Coordinates Datapad on the table
            if (coordinatesDatapadProp == null)
            {
                SetupDatapadProp();
            }
            if (coordinatesDatapadProp != null)
            {
                coordinatesDatapadProp.SetActive(true);
            }

            // Start alarm strobe
            _isStrobeActive = true;
            if (_alarmStrobe != null) _alarmStrobe.enabled = true;

            // Collector enters defensive / alerted posture
            if (collectorNPC != null && collectorNPC.animator != null)
            {
                collectorNPC.animator.SetInteger("RandomIdle", 28); // Aggressive posture
            }

            if (BlackMarketNPCDialogueHUD.Instance != null)
            {
                BlackMarketNPCDialogueHUD.Instance.ShowMissionBanner("🚨 VOSS'S BETRAYAL! GRAB THE MARS JUMP COORDINATES FROM THE CONSOLE [E]!");
                BlackMarketNPCDialogueHUD.Instance.ShowTransmission(
                    "KAELEN VOSS",
                    "SYNDICATE TRAITOR",
                    "Did you really think I'd let you walk away with the coordinates, Alita? Omni-Corp's 100,000 credit bounty on your intact chassis is mine! Kill her!",
                    new Color(1f, 0.25f, 0.2f),
                    6.5f
                );
            }

            if (CyberpunkHUDController.Instance != null)
            {
                CyberpunkHUDController.Instance.SetQuestObjective(
                    "VOSS'S BETRAYAL // AMBUSH",
                    "► Voss betrayed you to Omni-Corp! Snatch the Mars Coordinates from the desk [E] and escape!"
                );
                CyberpunkHUDController.Instance.ShowToast("CRITICAL: KAELEN VOSS SPRUNG AN AMBUSH FOR THE BOUNTY!", 4.5f);
            }
        }

        /// <summary>
        /// Alita grabs the decrypted Mars hyperspace coordinates datapad.
        /// </summary>
        public void SnatchCoordinates()
        {
            if (currentPhase != BetrayalPhase.CoordinatesSpawned) return;

            currentPhase = BetrayalPhase.CoordinatesSecured;
            BlackMarketNPC.MarsCoordinatesUnlocked = true;
            PlayerPrefs.SetInt("Mars_Coordinates_Decrypted", 1);
            PlayerPrefs.Save();

            if (coordinatesDatapadProp != null)
            {
                coordinatesDatapadProp.SetActive(false);
            }

            if (_extractionBeacon == null) SetupExtractionBeacon();
            if (_extractionBeacon != null)
            {
                _extractionBeacon.SetActive(true);
            }

            if (Application.isPlaying) PlayPickupSound();

            if (BlackMarketNPCDialogueHUD.Instance != null)
            {
                BlackMarketNPCDialogueHUD.Instance.ShowMissionBanner("🚨 MARS COORDINATES SECURED! SURVIVE VOSS'S AMBUSH & REACH PAD B-04!");
                BlackMarketNPCDialogueHUD.Instance.ShowTransmission(
                    "KAELEN VOSS",
                    "SYNDICATE TRAITOR",
                    "All Syndicate units, open fire! The Berserker has the coordinates! Garage Heavy Hover Tank, destroy her before she reaches the flight deck!",
                    new Color(1f, 0.2f, 0.2f),
                    6.5f
                );
            }

            if (CyberpunkHUDController.Instance != null)
            {
                CyberpunkHUDController.Instance.SetQuestObjective(
                    "SURVIVE VOSS'S AMBUSH",
                    "► Escape the sanctuary building and survive the Syndicate Heavy Hover Tank attack!"
                );
                CyberpunkHUDController.Instance.ShowToast("VOSS'S FORCES ENGAGED: REACH STARSHIP ON PAD B-04", 4.5f);
            }
        }

        /// <summary>
        /// Sensed when Alita runs through the front door and exits the sanctuary building.
        /// </summary>
        private void OnTriggerEnter(Collider other)
        {
            if (currentPhase == BetrayalPhase.CoordinatesSecured)
            {
                if (other.CompareTag("Player") || other.name.Contains("Alita"))
                {
                    OnPlayerExitedBuilding();
                }
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (currentPhase == BetrayalPhase.CoordinatesSecured)
            {
                if (other.CompareTag("Player") || other.name.Contains("Alita"))
                {
                    OnPlayerExitedBuilding();
                }
            }
        }

        /// <summary>
        /// As soon as Alita leaves the building, the hover tank in the garage starts attacking her!
        /// </summary>
        public void OnPlayerExitedBuilding()
        {
            if (currentPhase != BetrayalPhase.CoordinatesSecured) return;

            currentPhase = BetrayalPhase.ExitedBuilding_TankAttacking;

            FindSceneReferences();

            // Activate the Tank in the garage!
            if (hoverTankAI != null)
            {
                hoverTankAI.ActivateTank();
            }

            // Bodyguards outside switch to combat alert
            var g1 = GameObject.Find("NPC_Alien_Bodyguard_Vorgash");
            var g2 = GameObject.Find("NPC_Alien_Bodyguard_Krogan");
            if (g1 != null) g1.GetComponent<Animator>()?.SetInteger("RandomIdle", 28);
            if (g2 != null) g2.GetComponent<Animator>()?.SetInteger("RandomIdle", 28);
        }

        private void PlayPickupSound()
        {
            AudioSource audio = GetComponent<AudioSource>();
            if (audio == null) audio = gameObject.AddComponent<AudioSource>();

            // Synthesize high-tech data acquisition chime
            int rate = 44100;
            int len = (int)(rate * 0.45f);
            float[] data = new float[len];
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / rate;
                float f1 = 880f;
                float f2 = 1320f;
                float f3 = 1760f;
                float env = Mathf.Exp(-7f * t);
                data[i] = (Mathf.Sin(2f * Mathf.PI * f1 * t) * 0.35f +
                           Mathf.Sin(2f * Mathf.PI * f2 * t) * 0.35f +
                           Mathf.Sin(2f * Mathf.PI * f3 * t) * 0.30f) * env;
            }
            var clip = AudioClip.Create("Data_Pickup", len, 1, rate, false);
            clip.SetData(data, 0);
            audio.PlayOneShot(clip, 0.9f);
        }

        /// <summary>
        /// Triggered when Alita reaches her starship on Landing Pad B-04 and initiates launch to Mars.
        /// </summary>
        public void TriggerEscapeToMars()
        {
            if (_isTransitioningToMars) return;
            _isTransitioningToMars = true;
            currentPhase = BetrayalPhase.EscapedToStarship;

            // Disarm tank combat so it stops firing during takeoff
            if (hoverTankAI != null)
            {
                if (hoverTankAI.targetingLaser != null) hoverTankAI.targetingLaser.enabled = false;
            }

            if (BlackMarketNPCDialogueHUD.Instance != null)
            {
                BlackMarketNPCDialogueHUD.Instance.ShowMissionBanner("🚀 STARSHIP LAUNCH CONFIRMED // WARPING TO MARS CYDONIA BASIN!");
                BlackMarketNPCDialogueHUD.Instance.ShowTransmission(
                    "ALITA",
                    "URM // FLIGHT SYSTEM",
                    "Coordinates locked. Spooling warp drive to factor 9. Next stop: Mars!",
                    new Color(0.3f, 0.95f, 1.0f),
                    6.0f
                );
            }

            if (CyberpunkHUDController.Instance != null)
            {
                CyberpunkHUDController.Instance.SetQuestObjective(
                    "OPERATION RED HORIZON",
                    "► HYPERSPACE WARP ENGAGED: In transit to Mars Cydonia War-Vault..."
                );
                CyberpunkHUDController.Instance.ShowToast("HYPERDRIVE ENGAGED // TRAJECTORY: MARS", 5.0f);
            }

            StartCoroutine(CinematicWarpToMarsRoutine());
        }

        private IEnumerator CinematicWarpToMarsRoutine()
        {
            FindSceneReferences();

            // If starship controller is present, initiate its boarding and hyperspace routine
            if (starshipFlightController != null)
            {
                starshipFlightController.targetTransitScene = marsSceneName;
                starshipFlightController.BoardShip();
                starshipFlightController.EngageHyperSpaceWarp();
            }
            else
            {
                // Standalone cinematic warp routine
                PlayWarpDriveSound();
                float duration = 2.4f;
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    _warpTransitionAlpha = Mathf.Clamp01(elapsed / (duration * 0.85f));
                    yield return null;
                }
                _warpTransitionAlpha = 1.0f;
                UnityEngine.SceneManagement.SceneManager.LoadScene(marsSceneName);
            }
        }

        private void PlayWarpDriveSound()
        {
            AudioSource audio = GetComponent<AudioSource>();
            if (audio == null) audio = gameObject.AddComponent<AudioSource>();

            // Synthesize hyperspace warp spool and sonic boom
            int rate = 44100;
            int len = (int)(rate * 2.2f);
            float[] data = new float[len];
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / rate;
                float freq = Mathf.Lerp(120f, 950f, t / 2.2f);
                float env = Mathf.Clamp01(t / 0.5f) * Mathf.Clamp01((2.2f - t) / 0.3f);
                data[i] = (Mathf.Sin(2f * Mathf.PI * freq * t) * 0.4f +
                           (Random.value * 2f - 1f) * 0.15f) * env;
            }
            var clip = AudioClip.Create("Hyperspace_Warp", len, 1, rate, false);
            clip.SetData(data, 0);
            audio.PlayOneShot(clip, 1.0f);
        }

        /// <summary>
        /// Developer shortcut: Teleports Alita into the Collector's room and triggers betrayal flow.
        /// </summary>
        [ContextMenu("Test Betrayal Sequence (F9)")]
        public void TestBetrayalSequence()
        {
            FindSceneReferences();

            if (_playerTransform != null)
            {
                _playerTransform.position = new Vector3(4.5f, 0.45f, -8.8f);
                _playerTransform.rotation = Quaternion.Euler(0f, 45f, 0f);
                Physics.SyncTransforms();
            }

            // Set mission stage to Return
            if (BlackMarketCoreRetrievalMission.Instance != null)
            {
                BlackMarketCoreRetrievalMission.Instance.currentStage = CoreRetrievalStage.ReturnToCoreEngine;
            }

            currentPhase = BetrayalPhase.ConfrontingCollector;

            // Start dialogue
            if (collectorNPC != null)
            {
                collectorNPC.StartConversation();
            }
            else
            {
                OnCollectorBetrayalDialogueFinished();
            }
        }

        /// <summary>
        /// Developer shortcut: Teleports Alita to Landing Pad B-04 and triggers Mars hyperspace jump.
        /// </summary>
        [ContextMenu("Test Mars Jump (F10)")]
        public void TestMarsJumpShortcut()
        {
            FindSceneReferences();

            if (_playerTransform != null)
            {
                _playerTransform.position = landingPadPos - Vector3.forward * 3.5f;
                Physics.SyncTransforms();
            }

            TriggerEscapeToMars();
        }

        #region Prompt GUI
        private void OnGUI()
        {
            float sw = Screen.width;
            float sh = Screen.height;

            // 1. Snatch Coordinates Prompt inside Pod
            if (currentPhase == BetrayalPhase.CoordinatesSpawned && _isPlayerNearDatapad)
            {
                float w = 480f;
                float h = 48f;
                float x = (sw - w) * 0.5f;
                float y = sh * 0.65f;

                DrawCyberpunkBox(x, y, w, h, new Color(0.1f, 0.95f, 1.0f, 0.98f));

                var style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                style.normal.textColor = Color.white;
                GUI.Label(new Rect(x, y, w, h), "PRESS [E] TO SNATCH MARS HYPERSPACE COORDINATES", style);
            }

            // 2. Extraction Waypoint & Boarding Prompt on Landing Pad B-04
            if (currentPhase == BetrayalPhase.CoordinatesSecured || currentPhase == BetrayalPhase.ExitedBuilding_TankAttacking)
            {
                // Top-Center Extraction HUD Indicator
                float indW = 480f;
                float indH = 32f;
                float indX = (sw - indW) * 0.5f;
                float indY = 68f;

                DrawCyberpunkBox(indX, indY, indW, indH, new Color(0.2f, 0.85f, 1.0f, 0.85f));
                var indStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                indStyle.normal.textColor = new Color(0.4f, 0.95f, 1.0f);
                GUI.Label(new Rect(indX, indY, indW, indH), $"► EXTRACTION STARSHIP (PAD B-04) // DISTANCE: {_distanceToStarship:F0}M // STATUS: READY", indStyle);

                // Near Starship Boarding & Warp Prompt
                if (_isPlayerNearStarship && !_isTransitioningToMars)
                {
                    float pw = 520f;
                    float ph = 52f;
                    float px = (sw - pw) * 0.5f;
                    float py = sh * 0.65f;

                    DrawCyberpunkBox(px, py, pw, ph, new Color(0f, 1.0f, 0.75f, 0.95f));
                    var pStyle = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 14,
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter
                    };
                    pStyle.normal.textColor = Color.white;
                    GUI.Label(new Rect(px, py, pw, ph), "PRESS [E] OR [F] TO LAUNCH STARSHIP // WARP TO MARS", pStyle);
                }
            }

            // 3. Fullscreen Hyperspace Warp Whiteout Flash
            if (_isTransitioningToMars && _warpTransitionAlpha > 0.01f)
            {
                Color prevColor = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, _warpTransitionAlpha);
                GUI.DrawTexture(new Rect(0, 0, sw, sh), Texture2D.whiteTexture);

                var warpStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 24,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                warpStyle.normal.textColor = Color.black;
                GUI.Label(new Rect(0, sh * 0.42f, sw, 40), "HYPERSPACE WARP IN PROGRESS", warpStyle);

                var subStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                subStyle.normal.textColor = new Color(0.2f, 0.2f, 0.2f);
                GUI.Label(new Rect(0, sh * 0.50f, sw, 30), "TRAJECTORY: MARS // CYDONIA BASIN // SECTOR 00", subStyle);

                GUI.color = prevColor;
            }

            GUI.color = Color.white;
        }

        private void DrawCyberpunkBox(float x, float y, float w, float h, Color borderColor)
        {
            GUI.color = new Color(0.02f, 0.05f, 0.09f, 0.92f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);

            GUI.color = borderColor;
            GUI.DrawTexture(new Rect(x, y, w, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x, y + h - 2, w, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x, y, 2, h), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x + w - 2, y, 2, h), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
        #endregion
    }
}
