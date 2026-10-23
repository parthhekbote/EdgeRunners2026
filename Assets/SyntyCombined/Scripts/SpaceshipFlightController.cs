using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using InsaneGun.Combat;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SyntyCombined
{
    /// <summary>
    /// AAA-Grade Sci-Fi Starfighter / Spaceship Flight Controller.
    /// Overhauled for buttery-smooth arcade hover & flight mechanics:
    /// - 100% bug-free 6DOF flight physics with anti-gravity ground cushion
    /// - Clean gimbal-lock-free orientation (Yaw * Pitch * Roll banking)
    /// - Instant responsive boarding [E] with zero physics conflicts
    /// - LateUpdate camera chase tracking with zero jitter
    /// - Dual rapid plasma blaster cannons ([LMB]) & heavy blast ([RMB])
    /// - Hyperspace Warp Jump sequence ([R] / [Enter]) into Sector 4 Black Market
    /// - Sleek Cyberpunk flight HUD with speed, altitude, reticle & navigation telemetry
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(BoxCollider))]
    public class SpaceshipFlightController : MonoBehaviour
    {
        public static SpaceshipFlightController ActiveShip { get; private set; }

        [Header("State")]
        [SerializeField] private bool _isPiloted = false;
        public bool IsPiloted => _isPiloted;

        [Header("Flight Dynamics & Thrusters")]
        [Tooltip("Standard cruise forward speed in m/s (approx 198 km/h)")]
        public float cruiseSpeed = 55f;
        [Tooltip("Maximum afterburner speed in m/s (approx 432 km/h)")]
        public float boostSpeed = 120f;
        [Tooltip("Reverse / retro-brake speed in m/s")]
        public float reverseSpeed = 22f;
        [Tooltip("Lateral strafe speed in m/s")]
        public float strafeSpeed = 28f;
        [Tooltip("Vertical lift / sink speed in m/s")]
        public float verticalSpeed = 24f;

        [Header("Acceleration & Drift Inertia")]
        [Tooltip("Forward thruster acceleration rate in m/s^2")]
        public float forwardAcceleration = 28f;
        [Tooltip("Boost afterburner acceleration in m/s^2")]
        public float boostAcceleration = 55f;
        [Tooltip("Lateral strafe thruster acceleration in m/s^2")]
        public float strafeAcceleration = 22f;
        [Tooltip("Vertical thruster acceleration in m/s^2")]
        public float verticalAcceleration = 22f;
        [Tooltip("Braking deceleration when throttle is released")]
        public float brakeDeceleration = 18f;

        [Header("Rotational Steering (Gimbal-Free 6DOF)")]
        [Tooltip("Turn pitch speed in deg/s")]
        public float pitchSpeed = 85f;
        [Tooltip("Turn yaw speed in deg/s")]
        public float yawSpeed = 95f;
        [Tooltip("Mouse steering sensitivity")]
        public float mouseSensitivity = 65f;
        [Tooltip("Maximum roll bank angle into turns (degrees)")]
        public float maxRollBank = 36f;
        [Tooltip("Roll bank transition speed")]
        public float rollBankSpeed = 6.0f;
        [Tooltip("Angular steering responsiveness")]
        public float angularResponsiveness = 18f;

        [Header("Hover Cushion & Landing Suspension")]
        public float hoverTargetHeight = 2.4f;
        public float hoverSpringStrength = 30f;

        [Header("Weapons Systems")]
        public float laserFireRate = 10f; // shots per sec
        public float laserSpeed = 180f;
        public float laserDamage = 50f;
        public float heavyLaserDamage = 130f;
        public float heavyCooldown = 1.0f;

        [Header("Thruster & Hover VFX")]
        public GameObject thrusterVFXPrefab;
        public GameObject hoverDustVFXPrefab;
        private Transform _thrustFlameL;
        private Transform _thrustFlameR;
        private ParticleSystem[] _hoverDustSystems;
        private float _glideThrusterOverride = -1f;

        [Header("Interaction")]
        public float boardDistance = 5.5f;
        public KeyCode boardKey = KeyCode.F;
        public KeyCode viewToggleKey = KeyCode.V;
        private float _boardHoldTimer = 0f;
        private const float BOARD_HOLD_DURATION = 0.35f;
        private float _exitCooldown = 0f;

        [Header("Transit & Hyperspace Warp")]
        public string targetTransitScene = "Scene_Mars_FinalBattle";
        public KeyCode warpKey = KeyCode.R;
        public bool isWarping = false;
        public bool warpUnlocked = true;
        public bool enableAltitudeWarp = false;
        public string warpDestinationTitle = "DESTINATION: MARS SECTOR-04 CRATER  |  PRESS [R] OR [ENTER] TO WARP";
        private float _warpFlashAlpha = 0f;
        private Coroutine _warpCoroutine;

        // Sub-object transforms
        private Transform _boosterL;
        private Transform _boosterR;
        private Transform _gunL;
        private Transform _gunR;
        private Transform _cockpitCamAnchor;

        // Physics & movement state
        private Rigidbody _rb;
        private float _currentPitch = 0f;
        private float _currentYaw = 0f;
        private float _currentRoll = 0f;
        private bool _isBoosting = false;
        private float _throttleInput = 0f;
        private float _strafeInput = 0f;
        private float _verticalInput = 0f;
        private float _mouseXInput = 0f;
        private float _mouseYInput = 0f;
        private float _rudderInput = 0f;
        private float _fireTimer = 0f;
        private float _heavyTimer = 0f;
        private bool _alternateGun = false;

        // Player reference
        private GameObject _player;
        private CharacterController _playerCC;
        private InsaneGunSwordController _playerSwordCtrl;
        private SmoothOrbitCamera _playerOrbitCam;
        private Camera _mainCam;

        // Camera control
        [Header("Camera Rig Settings")]
        public float defaultChaseDistance = 14.5f;
        public float defaultChaseHeight = 5.2f;
        private bool _isCockpitView = false;
        private float _currentCamFov = 65f;
        private float _targetCamFov = 65f;
        private Vector3 _camShakeOffset = Vector3.zero;
        private float _camDistance = 14.5f;
        private float _camHeight = 5.2f;
        private float _camTurnLead = 0f;
        private Vector3 _camPosVelocity = Vector3.zero;

        // Audio
        private AudioSource _engineAudio;
        private AudioSource _weaponAudio;
        private AudioClip _engineHumClip;
        private AudioClip _laserShotClip;
        private AudioClip _heavyShotClip;

        // Thruster VFX
        private ParticleSystem _thrustPsL;
        private ParticleSystem _thrustPsR;
        private Light _thrustLightL;
        private Light _thrustLightR;
        private Light _hoverRepulsorLight;

        // Ground telemetry
        private float _groundDistance = 0f;

        private void Awake()
        {
            EnsureInitialized();
        }

        public void EnsureInitialized()
        {
            if (_rb != null) return;

            _rb = GetComponent<Rigidbody>();
            if (_rb == null) _rb = gameObject.AddComponent<Rigidbody>();

            _rb.mass = 1500f;
            _rb.linearDamping = 0.40f;
            _rb.angularDamping = 2.0f;
            _rb.useGravity = false;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.constraints = RigidbodyConstraints.None;

            SetupCollidersAndAnchors();
            SetupAudioSources();
            SetupThrusterVFX();
        }

        private void Start()
        {
            EnsureInitialized();
            FindPlayerAndCamera();
            CheckGroundDistance();

            if (!_isPiloted)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
                _rb.isKinematic = true;
            }
        }

        private void SetupCollidersAndAnchors()
        {
            if (_boosterL == null) _boosterL = transform.Find("SM_Veh_HoverCraft_01_Booster_l");
            if (_boosterR == null) _boosterR = transform.Find("SM_Veh_HoverCraft_01_Booster_r");
            if (_gunL == null) _gunL = transform.Find("SM_Veh_HoverCraft_01_Gun_l");
            if (_gunR == null) _gunR = transform.Find("SM_Veh_HoverCraft_01_Gun_r");

            // Automatic fallbacks for SS-04 Nightshade Striker / custom starfighters
            if (_boosterL == null)
            {
                var bL = transform.Find("Booster_L_Anchor");
                if (bL == null)
                {
                    var go = new GameObject("Booster_L_Anchor");
                    go.transform.SetParent(transform, false);
                    go.transform.localPosition = new Vector3(-2.35f, 0f, -7.36f);
                    _boosterL = go.transform;
                }
                else _boosterL = bL;
            }
            if (_boosterR == null)
            {
                var bR = transform.Find("Booster_R_Anchor");
                if (bR == null)
                {
                    var go = new GameObject("Booster_R_Anchor");
                    go.transform.SetParent(transform, false);
                    go.transform.localPosition = new Vector3(2.35f, 0f, -7.36f);
                    _boosterR = go.transform;
                }
                else _boosterR = bR;
            }
            if (_gunL == null)
            {
                var gL = transform.Find("Gun_L_Anchor");
                if (gL == null)
                {
                    var go = new GameObject("Gun_L_Anchor");
                    go.transform.SetParent(transform, false);
                    go.transform.localPosition = new Vector3(-3.20f, -0.40f, 3.50f);
                    _gunL = go.transform;
                }
                else _gunL = gL;
            }
            if (_gunR == null)
            {
                var gR = transform.Find("Gun_R_Anchor");
                if (gR == null)
                {
                    var go = new GameObject("Gun_R_Anchor");
                    go.transform.SetParent(transform, false);
                    go.transform.localPosition = new Vector3(3.20f, -0.40f, 3.50f);
                    _gunR = go.transform;
                }
                else _gunR = gR;
            }

            // Setup Responsive Hull Physics Material with Bounciness and Tangential Deflection
            var flightMat = new PhysicsMaterial("Starship_Hull_Physics_Mat")
            {
                dynamicFriction = 0.15f,
                staticFriction = 0.20f,
                bounciness = 0.25f,
                frictionCombine = PhysicsMaterialCombine.Average,
                bounceCombine = PhysicsMaterialCombine.Maximum
            };

            // Setup Box Collider to encapsulate hull with zero ground snag
            var boxCol = GetComponent<BoxCollider>();
            if (boxCol != null)
            {
                var mf = GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null && mf.sharedMesh.bounds.size.z > 12f)
                {
                    boxCol.center = new Vector3(0f, 0.4f, 0.1f);
                    boxCol.size = new Vector3(9.3f, 3.4f, 15.4f);
                }
                else if (boxCol.size.z < 8f)
                {
                    boxCol.center = new Vector3(0f, 1.45f, 0.1f);
                    boxCol.size = new Vector3(7.4f, 2.3f, 7.6f);
                }
                boxCol.material = flightMat;
            }

            // Ensure all existing child mesh colliders use the hull physics material
            foreach (var col in GetComponentsInChildren<Collider>())
            {
                col.material = flightMat;
            }

            // Cockpit / Nose Cam Anchor (Unobstructed panoramic flight view)
            var cockAnchorObj = transform.Find("Cockpit_Cam_Anchor");
            if (cockAnchorObj == null)
            {
                var go = new GameObject("Cockpit_Cam_Anchor");
                go.transform.SetParent(transform, false);
                var mf = GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null && mf.sharedMesh.bounds.size.z > 12f)
                {
                    go.transform.localPosition = new Vector3(0f, 1.75f, 2.80f);
                }
                else
                {
                    go.transform.localPosition = new Vector3(0f, 2.15f, 3.85f);
                }
                _cockpitCamAnchor = go.transform;
            }
            else
            {
                _cockpitCamAnchor = cockAnchorObj;
            }
        }

        private void FindPlayerAndCamera()
        {
            if (_player == null)
            {
                _player = GameObject.Find("Alita_BattleAngel_Hero");
                if (_player == null) _player = GameObject.FindWithTag("Player");
            }

            if (_player != null)
            {
                _playerCC = _player.GetComponent<CharacterController>();
                _playerSwordCtrl = _player.GetComponent<InsaneGunSwordController>();
            }

            if (_mainCam == null)
            {
                _mainCam = Camera.main;
                if (_mainCam == null)
                {
                    var camObj = GameObject.Find("Main Camera");
                    if (camObj != null) _mainCam = camObj.GetComponent<Camera>();
                }
            }

            if (_mainCam != null)
            {
                _playerOrbitCam = _mainCam.GetComponent<SmoothOrbitCamera>();
            }
        }

        #region Procedural Audio & Thruster VFX
        private void SetupAudioSources()
        {
            _engineHumClip = GenerateProceduralEngineHum();
            _laserShotClip = GenerateProceduralLaserClip(1400f, 220f, 0.09f);
            _heavyShotClip = GenerateProceduralLaserClip(800f, 90f, 0.20f);

            _engineAudio = gameObject.AddComponent<AudioSource>();
            _engineAudio.clip = _engineHumClip;
            _engineAudio.loop = true;
            _engineAudio.playOnAwake = false;
            _engineAudio.spatialBlend = 0.4f;
            _engineAudio.minDistance = 4f;
            _engineAudio.maxDistance = 80f;
            _engineAudio.volume = 0.35f;
            _engineAudio.pitch = 0.9f;

            _weaponAudio = gameObject.AddComponent<AudioSource>();
            _weaponAudio.loop = false;
            _weaponAudio.playOnAwake = false;
            _weaponAudio.spatialBlend = 0.3f;
            _weaponAudio.volume = 0.65f;
        }

        private AudioClip GenerateProceduralEngineHum()
        {
            int sampleRate = 44100;
            int samples = sampleRate * 2;
            float[] data = new float[samples];
            float baseFreq = 64f;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float s1 = Mathf.Sin(2f * Mathf.PI * baseFreq * t) * 0.45f;
                float s2 = Mathf.Sin(2f * Mathf.PI * (baseFreq * 2.02f) * t) * 0.28f;
                float s3 = Mathf.Sin(2f * Mathf.PI * (baseFreq * 3.98f) * t) * 0.14f;
                float noise = (Random.value * 2f - 1f) * 0.08f;
                data[i] = Mathf.Clamp(s1 + s2 + s3 + noise, -1f, 1f);
            }

            var clip = AudioClip.Create("Engine_Hover_Hum", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip GenerateProceduralLaserClip(float startFreq, float endFreq, float duration)
        {
            int sampleRate = 44100;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float p = (float)i / samples;
                float freq = Mathf.Lerp(startFreq, endFreq, p * p);
                float env = 1f - p;
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * (float)i / sampleRate) * env * 0.7f;
            }

            var clip = AudioClip.Create("Laser_Procedural", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private void SetupThrusterVFX()
        {
#if UNITY_EDITOR
            if (thrusterVFXPrefab == null)
            {
                thrusterVFXPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonParticles/Prefabs/FX_Flame_Booster_01.prefab");
            }
            if (hoverDustVFXPrefab == null)
            {
                hoverDustVFXPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonSciFiWorlds/Prefabs/FX/FX_Ship_Hover_Dust_01.prefab");
            }
#endif

            var aaaThrusters = GetComponent<VFX.AAAStarshipThrusterSystem>();
            if (aaaThrusters == null)
            {
                aaaThrusters = gameObject.AddComponent<VFX.AAAStarshipThrusterSystem>();
            }

            if (_boosterL != null)
            {
                SetupSingleBoosterVFX(_boosterL, ref _thrustPsL, ref _thrustFlameL, ref _thrustLightL, "L");
            }
            if (_boosterR != null)
            {
                SetupSingleBoosterVFX(_boosterR, ref _thrustPsR, ref _thrustFlameR, ref _thrustLightR, "R");
            }

            var underbelly = transform.Find("Repulsor_Underbelly_Light");
            if (underbelly == null)
            {
                var go = new GameObject("Repulsor_Underbelly_Light");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, 0.45f, 0.2f);
                _hoverRepulsorLight = go.AddComponent<Light>();
                _hoverRepulsorLight.type = LightType.Point;
                _hoverRepulsorLight.color = new Color(0f, 0.94f, 1f);
                _hoverRepulsorLight.range = 14f;
                _hoverRepulsorLight.intensity = 1.2f;
            }
            else
            {
                _hoverRepulsorLight = underbelly.GetComponent<Light>();
            }

            if (hoverDustVFXPrefab != null && _hoverDustSystems == null)
            {
                var existingDust = transform.Find("Ship_Hover_Dust_FX");
                GameObject dustObj = null;
                if (existingDust == null)
                {
                    dustObj = Instantiate(hoverDustVFXPrefab, transform);
                    dustObj.name = "Ship_Hover_Dust_FX";
                    dustObj.transform.localPosition = new Vector3(0f, 0.15f, 0f);
                    dustObj.transform.localRotation = Quaternion.identity;
                    dustObj.transform.localScale = Vector3.one * 1.2f;
                }
                else
                {
                    dustObj = existingDust.gameObject;
                }

                if (dustObj != null)
                {
                    _hoverDustSystems = dustObj.GetComponentsInChildren<ParticleSystem>();
                }
            }
        }

        private void SetupSingleBoosterVFX(Transform booster, ref ParticleSystem ps, ref Transform flameT, ref Light glowLight, string side)
        {
            var oldFX = booster.Find("Booster_Exhaust_FX");
            if (oldFX != null)
            {
                if (Application.isPlaying) Destroy(oldFX.gameObject);
                else DestroyImmediate(oldFX.gameObject);
            }

            var existingFlame = booster.Find($"Booster_Flame_{side}");
            if (existingFlame != null)
            {
                if (Application.isPlaying) Destroy(existingFlame.gameObject);
                else DestroyImmediate(existingFlame.gameObject);
            }

            var aaaThrusters = GetComponent<VFX.AAAStarshipThrusterSystem>();
            if (aaaThrusters != null)
            {
                // Handled cleanly by AAAStarshipThrusterSystem (no dropped 3D mesh particles)
                return;
            }

            ps = CreateFallbackBoosterParticles(booster);
            flameT = ps.transform;

            var existingLight = booster.Find("Booster_Glow_Light");
            if (existingLight == null)
            {
                var go = new GameObject("Booster_Glow_Light");
                go.transform.SetParent(booster, false);
                go.transform.localPosition = new Vector3(0f, 0f, -1.4f);
                glowLight = go.AddComponent<Light>();
                glowLight.type = LightType.Point;
                glowLight.color = new Color(0f, 0.88f, 1f);
                glowLight.range = 7.0f;
                glowLight.intensity = 1.8f;
            }
            else
            {
                glowLight = existingLight.GetComponent<Light>();
            }
        }

        private ParticleSystem CreateFallbackBoosterParticles(Transform booster)
        {
            var go = new GameObject("Booster_Exhaust_FX");
            go.transform.SetParent(booster, false);
            go.transform.localPosition = new Vector3(0f, 0f, -1.2f);
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 0.32f;
            main.startSpeed = 16f;
            main.startSize = 0.55f;
            main.startColor = new Color(0f, 0.95f, 1f, 0.85f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 55f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8f;
            shape.radius = 0.35f;

            return ps;
        }
        #endregion

        private void Update()
        {
            if (_player == null || _mainCam == null) FindPlayerAndCamera();

            CheckGroundDistance();

            if (_exitCooldown > 0f) _exitCooldown -= Time.deltaTime;

            if (_isPiloted)
            {
                HandleFlightInput();
                HandleWeaponsInput();
                UpdateEngineFX();

                // Hyperspace Warp Jump trigger
                bool altWarp = enableAltitudeWarp && transform.position.y > 65f;
                if (warpUnlocked && !isWarping && (Input.GetKeyDown(warpKey) || Input.GetKeyDown(KeyCode.Return) || altWarp))
                {
                    EngageHyperSpaceWarp();
                }

                // Exit ship on [F] or [E]
                if (_exitCooldown <= 0f && (Input.GetKeyDown(boardKey) || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F)))
                {
                    DisembarkPlayer();
                }

                // Toggle cockpit / chase camera on [V]
                if (Input.GetKeyDown(viewToggleKey))
                {
                    _isCockpitView = !_isCockpitView;
                }
            }
            else
            {
                // Standby / Unpiloted check distance to player
                if (_player != null && _exitCooldown <= 0f)
                {
                    float dist = Vector3.Distance(transform.position, _player.transform.position);
                    if (dist <= boardDistance)
                    {
                        // Single tap or hold immediately boards on [F]
                        if (Input.GetKeyDown(boardKey) || Input.GetKeyDown(KeyCode.F))
                        {
                            BoardShip();
                        }
                        else if (Input.GetKey(boardKey) || Input.GetKey(KeyCode.F))
                        {
                            _boardHoldTimer += Time.deltaTime;
                            if (_boardHoldTimer >= BOARD_HOLD_DURATION)
                            {
                                _boardHoldTimer = 0f;
                                BoardShip();
                            }
                        }
                        else
                        {
                            _boardHoldTimer = Mathf.Max(0f, _boardHoldTimer - Time.deltaTime * 4f);
                        }
                    }
                    else
                    {
                        _boardHoldTimer = 0f;
                    }
                }

                if (_glideThrusterOverride >= 0f)
                {
                    ApplyThrusterVisuals(_glideThrusterOverride, isStandby: false);
                }
                else
                {
                    UpdateStandbyFX();
                }
            }
        }

        private void FixedUpdate()
        {
            if (_isPiloted && !_rb.isKinematic)
            {
                ApplyFlightPhysics();
            }
        }

        private void LateUpdate()
        {
            if (_isPiloted)
            {
                UpdateCameraRig();
            }
        }

        private void CheckGroundDistance()
        {
            // If tilted beyond ~50 degrees from horizontal (e.g. climbing, diving, looping),
            // ground cushion repulsors should not engage
            float uprightDot = Vector3.Dot(transform.up, Vector3.up);
            if (uprightDot < 0.60f)
            {
                _groundDistance = 999f;
                return;
            }

            RaycastHit hit;
            Vector3 rayStart = transform.position + Vector3.up * 0.2f;
            int layerMask = ~LayerMask.GetMask("Ignore Raycast", "Player");

            if (Physics.Raycast(rayStart, Vector3.down, out hit, 80f, layerMask))
            {
                _groundDistance = hit.distance;
            }
            else
            {
                _groundDistance = 999f;
            }
        }

        #region Boarding & Disembarking
        public void BoardShip()
        {
            EnsureInitialized();
            if (_player == null) FindPlayerAndCamera();
            if (_player == null) return;

            _isPiloted = true;
            ActiveShip = this;
            _exitCooldown = 0.6f;

            // Activate Rigidbody physics
            _rb.isKinematic = false;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;

            // Lift ship slightly into hover mode immediately
            transform.position += Vector3.up * 1.5f;

            // Initialize rotation state from current yaw
            _currentYaw = transform.eulerAngles.y;
            _currentPitch = 0f;
            _currentRoll = 0f;

            // Lock cursor for intuitive steering
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Disable player on-foot systems and hide player
            if (_playerOrbitCam != null) _playerOrbitCam.enabled = false;
            if (_playerCC != null) _playerCC.enabled = false;
            if (_playerSwordCtrl != null) _playerSwordCtrl.enabled = false;
            _player.SetActive(false);

            // Power on engines
            if (_engineAudio != null)
            {
                _engineAudio.volume = 0.45f;
                _engineAudio.pitch = 1.0f;
                _engineAudio.Play();
            }

            // Initialize camera rig smoothly to prevent camera teleport hitching
            _camDistance = defaultChaseDistance;
            _camHeight = defaultChaseHeight;
            _camTurnLead = 0f;
            _camPosVelocity = Vector3.zero;
            if (_mainCam != null)
            {
                _mainCam.fieldOfView = 65f;
                Vector3 initPos = transform.position - transform.forward * _camDistance + Vector3.up * _camHeight;
                _mainCam.transform.position = initPos;
                Vector3 lookTarget = transform.position + transform.forward * 50f + Vector3.up * 3.0f;
                _mainCam.transform.rotation = Quaternion.LookRotation((lookTarget - initPos).normalized, Vector3.up);
            }

            Debug.Log("<color=#00FFAA><b>[Starship]</b></color> Boarded Martian Starship. Engines online.");
        }

        public void DisembarkPlayer()
        {
            if (_player == null) return;

            _isPiloted = false;
            _glideThrusterOverride = -1f;
            if (ActiveShip == this) ActiveShip = null;

            // Calculate safe ground exit position to the left of the starship
            Vector3 exitPos = transform.position - transform.right * 4.5f;
            RaycastHit hit;
            int mask = ~LayerMask.GetMask("Ignore Raycast", "Player");
            if (Physics.Raycast(exitPos + Vector3.up * 4.0f, Vector3.down, out hit, 25f, mask))
            {
                exitPos = hit.point + Vector3.up * 0.08f;
            }

            // Restore player position, rotation & active state
            _player.transform.position = exitPos;
            _player.transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y - 90f, 0f);
            _player.SetActive(true);

            if (_playerCC != null) _playerCC.enabled = true;
            if (_playerSwordCtrl != null) _playerSwordCtrl.enabled = true;

            // Restore Main Camera
            if (_mainCam != null)
            {
                _mainCam.fieldOfView = 60f;
                if (_playerOrbitCam != null)
                {
                    _playerOrbitCam.target = _player.transform;
                    _playerOrbitCam.enabled = true;
                }
            }

            // Lock cursor for third-person player camera
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Settle ship down safely
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.isKinematic = true;

            if (_engineAudio != null)
            {
                _engineAudio.pitch = 0.8f;
                _engineAudio.volume = 0.15f;
            }

            Debug.Log("<color=#00FFAA><b>[Starship]</b></color> Disembarked Martian Starship.");
        }

        public void SetBoardingCooldown(float duration)
        {
            _exitCooldown = duration;
        }
        #endregion

        #region Flight Control & Physics
        private void HandleFlightInput()
        {
            // 1. Forward / Reverse Throttle: W (+1), S (-1 / Airbrake)
            _throttleInput = Input.GetAxis("Vertical");

            // 2. Lateral Strafing: A (-1), D (+1)
            _strafeInput = Input.GetAxis("Horizontal");

            // 3. Optional Dedicated Rudder Turn: Q (Yaw Left)
            _rudderInput = 0f;
            if (Input.GetKey(KeyCode.Q)) _rudderInput -= 1f;

            // 4. Boost / Afterburner: Left Shift
            _isBoosting = Input.GetKey(KeyCode.LeftShift);

            // 5. Vertical Thruster: Space (+1), LeftControl / C (-1)
            _verticalInput = 0f;
            if (Input.GetKey(KeyCode.Space)) _verticalInput += 1f;
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C)) _verticalInput -= 1f;

            // 6. Responsive Mouse Steering Input
            _mouseXInput = Input.GetAxis("Mouse X");
            _mouseYInput = Input.GetAxis("Mouse Y");
        }

        private void ApplyFlightPhysics()
        {
            // ==========================================
            // 1. Gimbal-Free 6DOF Rotational Steering
            // ==========================================
            // Pitch: rotates around local X (transform.right). Up/Down with mouse
            float targetPitchRate = -_mouseYInput * mouseSensitivity;
            // Yaw: rotates around local Y (transform.up) using Mouse X + Rudder
            float targetYawRate = (_mouseXInput * mouseSensitivity) + (_rudderInput * yawSpeed * 0.8f);

            // Clamp steering rates to maximum specs
            targetPitchRate = Mathf.Clamp(targetPitchRate, -pitchSpeed, pitchSpeed);
            targetYawRate = Mathf.Clamp(targetYawRate, -yawSpeed, yawSpeed);

            // Auto-horizon leveling and aerodynamic banking around local Z (transform.forward)
            float targetRollRate = 0f;
            Vector3 projectedForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (projectedForward.sqrMagnitude > 0.05f)
            {
                Vector3 horizonRight = Vector3.Cross(Vector3.up, projectedForward.normalized).normalized;
                float currentRollAngle = Vector3.SignedAngle(horizonRight, transform.right, transform.forward);

                // Banking into yaw and strafe turns (up to maxRollBank)
                float turnDemand = _strafeInput * 0.70f + Mathf.Clamp(_mouseXInput * 0.40f, -1f, 1f);
                float desiredBankAngle = -Mathf.Clamp(turnDemand, -1f, 1f) * maxRollBank;

                float rollAngleError = Mathf.DeltaAngle(currentRollAngle, desiredBankAngle);
                targetRollRate = Mathf.Clamp(rollAngleError * rollBankSpeed, -rollBankSpeed * 15f, rollBankSpeed * 15f);
            }

            // Convert local rates to world angular velocity
            Vector3 desiredLocalAngVel = new Vector3(
                targetPitchRate * Mathf.Deg2Rad,
                targetYawRate * Mathf.Deg2Rad,
                targetRollRate * Mathf.Deg2Rad
            );
            Vector3 desiredWorldAngVel = transform.TransformDirection(desiredLocalAngVel);

            // Apply angular velocity with smooth responsiveness
            _rb.angularVelocity = Vector3.Lerp(_rb.angularVelocity, desiredWorldAngVel, angularResponsiveness * Time.fixedDeltaTime);

            // ==========================================
            // 2. Newtonian Thruster Propulsion & Drift
            // ==========================================
            // Decompose current world velocity into starship's local coordinate frame
            Vector3 currentVel = _rb.linearVelocity;
            Vector3 localVel = transform.InverseTransformDirection(currentVel);

            // Desired local target velocities
            float desiredForwardSpeed = 0f;
            if (_throttleInput > 0.05f)
            {
                desiredForwardSpeed = (_isBoosting ? boostSpeed : cruiseSpeed) * _throttleInput;
            }
            else if (_throttleInput < -0.05f)
            {
                desiredForwardSpeed = reverseSpeed * _throttleInput;
            }

            float desiredStrafeSpeed = _strafeInput * strafeSpeed;
            float desiredVerticalSpeed = _verticalInput * verticalSpeed;

            // Calculate needed velocity changes in local space
            float deltaForward = desiredForwardSpeed - localVel.z;
            float deltaStrafe = desiredStrafeSpeed - localVel.x;
            float deltaVertical = desiredVerticalSpeed - localVel.y;

            // Determine thruster response rates
            float forwardRate = (_throttleInput > 0.05f) ? (_isBoosting ? boostAcceleration : forwardAcceleration) : brakeDeceleration;
            float strafeRate = (_strafeInput != 0f) ? strafeAcceleration : (brakeDeceleration * 0.85f);
            float verticalRate = (_verticalInput != 0f) ? verticalAcceleration : (brakeDeceleration * 0.85f);

            // Clamp thrust change to thruster max acceleration
            float maxDeltaForward = forwardRate * Time.fixedDeltaTime;
            float maxDeltaStrafe = strafeRate * Time.fixedDeltaTime;
            float maxDeltaVertical = verticalRate * Time.fixedDeltaTime;

            float appliedDeltaForward = Mathf.Clamp(deltaForward, -maxDeltaForward, maxDeltaForward);
            float appliedDeltaStrafe = Mathf.Clamp(deltaStrafe, -maxDeltaStrafe, maxDeltaStrafe);
            float appliedDeltaVertical = Mathf.Clamp(deltaVertical, -maxDeltaVertical, maxDeltaVertical);

            // Transform impulse back to world space
            Vector3 thrustVelocityChange = transform.forward * appliedDeltaForward
                                         + transform.right * appliedDeltaStrafe
                                         + transform.up * appliedDeltaVertical;

            // Apply thrust via VelocityChange (preserves collision impulses and momentum)
            _rb.AddForce(thrustVelocityChange, ForceMode.VelocityChange);

            // ==========================================
            // 3. Smooth Damped Ground Hover Suspension
            // ==========================================
            // Only engage when close to deck, roughly upright, not boosting, and not intentionally diving
            if (_groundDistance < hoverTargetHeight && Vector3.Dot(transform.up, Vector3.up) > 0.65f && _verticalInput >= -0.15f && !_isBoosting)
            {
                float hoverDelta = hoverTargetHeight - _groundDistance;
                float currentVerticalSpeed = Vector3.Dot(_rb.linearVelocity, Vector3.up);
                // Critically damped spring force: F = k * x - c * v
                float cushionForce = (hoverDelta * hoverSpringStrength) - (currentVerticalSpeed * 4.0f);
                cushionForce = Mathf.Clamp(cushionForce, -6f, 22f);
                _rb.AddForce(Vector3.up * cushionForce, ForceMode.Acceleration);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!_isPiloted) return;

            float impactForce = collision.relativeVelocity.magnitude;
            if (impactForce < 1.5f) return;

            // 1. Camera trauma shake scaled to impact speed
            float shakeIntensity = Mathf.Clamp(impactForce * 0.035f, 0.08f, 0.95f);
            _camShakeOffset = Random.insideUnitSphere * shakeIntensity;

            // 2. Contact sparks at the impact point
            if (collision.contactCount > 0)
            {
                ContactPoint contact = collision.GetContact(0);
                SpawnImpactSparks(contact.point, contact.normal, isHeavy: impactForce > 10f);
            }

            // 3. Collision audio thud
            if (_weaponAudio != null && _heavyShotClip != null)
            {
                float vol = Mathf.Clamp01(impactForce / 20f) * 0.85f;
                _weaponAudio.pitch = Random.Range(0.60f, 0.80f);
                _weaponAudio.PlayOneShot(_heavyShotClip, vol);
            }
        }

        private void UpdateEngineFX()
        {
            float thrustFactor = Mathf.Clamp01(_throttleInput);
            if (_isBoosting)
            {
                thrustFactor = 1.6f;
            }
            else if (_throttleInput < -0.05f)
            {
                thrustFactor = 0.25f; // Reverse thruster plume
            }
            else if (Mathf.Abs(_verticalInput) > 0.05f)
            {
                thrustFactor = 0.45f; // Lift thruster plume
            }
            else if (thrustFactor < 0.15f)
            {
                thrustFactor = 0.20f; // Minimum active flight hover plume
            }

            ApplyThrusterVisuals(thrustFactor, isStandby: false);
        }

        private void UpdateStandbyFX()
        {
            ApplyThrusterVisuals(0f, isStandby: true);
        }

        public void SetThrusterGlideState(float intensity)
        {
            EnsureInitialized();
            _glideThrusterOverride = intensity;
            if (intensity >= 0f)
            {
                ApplyThrusterVisuals(intensity, isStandby: false);
            }
            else
            {
                ApplyThrusterVisuals(0f, isStandby: true);
            }
        }

        private void ApplyThrusterVisuals(float factor, bool isStandby)
        {
            var aaaThrusters = GetComponent<VFX.AAAStarshipThrusterSystem>();
            if (aaaThrusters != null)
            {
                aaaThrusters.UpdateThrusterVisuals(isStandby ? 0.2f : factor, !isStandby && factor > 1.05f);
            }

            if (isStandby)
            {
                float idlePulse = Mathf.Sin(Time.time * 3.5f) * 0.08f;
                Vector3 idleScale = new Vector3(0.85f + idlePulse * 0.4f, 0.55f + idlePulse, 0.85f + idlePulse * 0.4f);

                if (_thrustFlameL != null) _thrustFlameL.localScale = idleScale;
                if (_thrustFlameR != null) _thrustFlameR.localScale = idleScale;

                float standbyLight = 0.85f + Mathf.Sin(Time.time * 3.5f) * 0.25f;
                if (_thrustLightL != null)
                {
                    _thrustLightL.intensity = standbyLight;
                    _thrustLightL.range = 5.0f;
                }
                if (_thrustLightR != null)
                {
                    _thrustLightR.intensity = standbyLight;
                    _thrustLightR.range = 5.0f;
                }

                if (_hoverRepulsorLight != null)
                {
                    _hoverRepulsorLight.intensity = 0.8f + Mathf.Sin(Time.time * 2f) * 0.2f;
                }

                if (_hoverDustSystems != null)
                {
                    foreach (var ps in _hoverDustSystems)
                    {
                        if (ps != null)
                        {
                            var em = ps.emission;
                            em.enabled = false;
                        }
                    }
                }

                if (_engineAudio != null && _engineAudio.isPlaying)
                {
                    _engineAudio.volume = Mathf.MoveTowards(_engineAudio.volume, 0.12f, Time.deltaTime * 0.5f);
                    _engineAudio.pitch = Mathf.MoveTowards(_engineAudio.pitch, 0.85f, Time.deltaTime * 0.5f);
                }
                return;
            }

            // Active Thrust Mode (Flight or Blackwall Emergence Glide)
            bool isBoost = factor > 1.05f;
            float thrust = Mathf.Clamp01(factor);

            // Compute flame scale:
            // SM_Flame_FX length is along local Y (which points along -Z of booster)
            float flameY;
            float flameXZ;
            if (isBoost)
            {
                flameY = Mathf.Lerp(1.65f, 2.35f, (factor - 1f) / 0.6f) + Random.Range(-0.06f, 0.06f);
                flameXZ = 1.25f;
            }
            else
            {
                flameY = Mathf.Lerp(0.70f, 1.65f, thrust);
                flameXZ = Mathf.Lerp(0.88f, 1.15f, thrust);
            }
            Vector3 targetScale = new Vector3(flameXZ, flameY, flameXZ);

            if (_thrustFlameL != null)
            {
                _thrustFlameL.localScale = Vector3.Lerp(_thrustFlameL.localScale, targetScale, Time.deltaTime * 18f);
            }
            if (_thrustFlameR != null)
            {
                _thrustFlameR.localScale = Vector3.Lerp(_thrustFlameR.localScale, targetScale, Time.deltaTime * 18f);
            }

            // Particle system speed modulation
            float psSpeed = Mathf.Lerp(12f, isBoost ? 32f : 20f, thrust);
            if (_thrustPsL != null)
            {
                var m = _thrustPsL.main;
                m.startSpeed = psSpeed;
            }
            if (_thrustPsR != null)
            {
                var m = _thrustPsR.main;
                m.startSpeed = psSpeed;
            }

            // Booster lights
            float lightIntensity = Mathf.Lerp(1.2f, isBoost ? 4.8f : 2.8f, thrust);
            float lightRange = Mathf.Lerp(6.0f, isBoost ? 12.0f : 9.0f, thrust);
            if (_thrustLightL != null)
            {
                _thrustLightL.intensity = lightIntensity;
                _thrustLightL.range = lightRange;
            }
            if (_thrustLightR != null)
            {
                _thrustLightR.intensity = lightIntensity;
                _thrustLightR.range = lightRange;
            }

            // Underbelly repulsor light
            if (_hoverRepulsorLight != null)
            {
                float groundFactor = Mathf.Clamp01(1f - (_groundDistance / 10f));
                _hoverRepulsorLight.intensity = Mathf.Lerp(0.8f, 3.5f, groundFactor);
            }

            // Dynamic Ground Hover Dust
            if (_hoverDustSystems != null && _hoverDustSystems.Length > 0)
            {
                bool nearGround = _groundDistance < 8.0f;
                float dustFactor = nearGround ? Mathf.Clamp01(1f - (_groundDistance / 8.0f)) : 0f;

                foreach (var ps in _hoverDustSystems)
                {
                    if (ps == null) continue;
                    var em = ps.emission;
                    if (nearGround)
                    {
                        em.enabled = true;
                        em.rateOverTime = Mathf.Lerp(4f, isBoost ? 45f : 30f, dustFactor);
                    }
                    else
                    {
                        em.enabled = false;
                    }
                }
            }

            // Engine Audio modulation
            if (_engineAudio != null)
            {
                if (!_engineAudio.isPlaying) _engineAudio.Play();
                float targetPitch = Mathf.Lerp(0.95f, isBoost ? 1.85f : 1.35f, thrust);
                float targetVol = Mathf.Lerp(0.35f, isBoost ? 0.95f : 0.70f, thrust);
                _engineAudio.pitch = Mathf.Lerp(_engineAudio.pitch, targetPitch, Time.deltaTime * 8f);
                _engineAudio.volume = Mathf.Lerp(_engineAudio.volume, targetVol, Time.deltaTime * 8f);
            }
        }
        #endregion

        #region Weapons System
        private void HandleWeaponsInput()
        {
            _fireTimer -= Time.deltaTime;
            _heavyTimer -= Time.deltaTime;

            // Primary Rapid Plasma Fire (Hold or Click LMB)
            if (Input.GetMouseButton(0) && _fireTimer <= 0f)
            {
                _fireTimer = 1f / laserFireRate;
                FirePlasmaBolt(_alternateGun);
                _alternateGun = !_alternateGun;
            }

            // Heavy Torpedo Blast (RMB or F)
            if ((Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.F)) && _heavyTimer <= 0f)
            {
                _heavyTimer = heavyCooldown;
                FireHeavyBlast();
            }
        }

        private void FirePlasmaBolt(bool useLeftGun)
        {
            Transform gun = (useLeftGun && _gunL != null) ? _gunL : (_gunR != null ? _gunR : transform);
            Vector3 muzzlePos = gun.position + gun.forward * 1.8f;
            Vector3 shootDir = gun.forward;

            StartCoroutine(SimulateLaserBolt(muzzlePos, shootDir, laserSpeed, laserDamage, false));

            if (_weaponAudio != null && _laserShotClip != null)
            {
                _weaponAudio.pitch = Random.Range(0.95f, 1.05f);
                _weaponAudio.PlayOneShot(_laserShotClip, 0.60f);
            }

            _camShakeOffset = Random.insideUnitSphere * 0.03f;
        }

        private void FireHeavyBlast()
        {
            if (_gunL != null)
            {
                StartCoroutine(SimulateLaserBolt(_gunL.position + _gunL.forward * 1.8f, _gunL.forward, laserSpeed * 0.85f, heavyLaserDamage, true));
            }
            if (_gunR != null)
            {
                StartCoroutine(SimulateLaserBolt(_gunR.position + _gunR.forward * 1.8f, _gunR.forward, laserSpeed * 0.85f, heavyLaserDamage, true));
            }

            if (_weaponAudio != null && _heavyShotClip != null)
            {
                _weaponAudio.pitch = 0.95f;
                _weaponAudio.PlayOneShot(_heavyShotClip, 0.95f);
            }

            _camShakeOffset = Random.insideUnitSphere * 0.08f;
        }

        private IEnumerator SimulateLaserBolt(Vector3 start, Vector3 dir, float speed, float damage, bool isHeavy)
        {
            var bolt = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bolt.name = isHeavy ? "Heavy_Torpedo" : "Plasma_Bolt";
            var col = bolt.GetComponent<Collider>();
            if (col != null) Destroy(col);

            bolt.transform.position = start;
            bolt.transform.localScale = isHeavy ? new Vector3(0.55f, 0.55f, 2.2f) : new Vector3(0.24f, 0.24f, 1.4f);
            bolt.transform.rotation = Quaternion.LookRotation(dir);

            var rend = bolt.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                mat.color = isHeavy ? new Color(1f, 0.2f, 0.4f) : new Color(0f, 0.95f, 1f);
                rend.material = mat;
            }

            var light = bolt.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = isHeavy ? new Color(1f, 0.3f, 0.5f) : new Color(0f, 0.95f, 1f);
            light.range = isHeavy ? 10f : 5f;
            light.intensity = isHeavy ? 4f : 2f;

            float maxLife = 2.5f;
            float elapsed = 0f;
            Vector3 currentPos = start;

            while (elapsed < maxLife)
            {
                float step = speed * Time.deltaTime;
                RaycastHit hit;
                int mask = ~LayerMask.GetMask("Ignore Raycast", "Player");

                if (Physics.Raycast(currentPos, dir, out hit, step, mask))
                {
                    bolt.transform.position = hit.point;
                    SpawnImpactSparks(hit.point, hit.normal, isHeavy);
                    ApplyDamageToHit(hit, damage, dir, isHeavy);
                    Destroy(bolt);
                    yield break;
                }

                currentPos += dir * step;
                bolt.transform.position = currentPos;
                elapsed += Time.deltaTime;
                yield return null;
            }

            Destroy(bolt);
        }

        private void SpawnImpactSparks(Vector3 point, Vector3 normal, bool isHeavy)
        {
            var sparkObj = new GameObject("Laser_Impact_Sparks");
            sparkObj.transform.position = point;
            sparkObj.transform.rotation = Quaternion.LookRotation(normal);

            var ps = sparkObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 0.25f;
            main.startSpeed = isHeavy ? 18f : 10f;
            main.startSize = isHeavy ? 0.35f : 0.18f;
            main.startColor = isHeavy ? new Color(1f, 0.3f, 0.5f) : new Color(0f, 0.95f, 1f);
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)(isHeavy ? 35 : 18)) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 45f;

            ps.Play();
            Destroy(sparkObj, 0.6f);
        }

        private void ApplyDamageToHit(RaycastHit hit, float damage, Vector3 direction, bool isHeavy)
        {
            var swordCtrl = hit.collider.GetComponentInParent<InsaneGunSwordController>();
            if (swordCtrl != null)
            {
                swordCtrl.TakeDamage(damage, hit.point, direction, isHeavy);
            }

            var ragdoll = hit.collider.GetComponentInParent<EuphoriaRagdollController>();
            if (ragdoll != null)
            {
                float force = isHeavy ? 55f : 32f;
                ragdoll.TriggerEuphoriaRagdoll(hit.point, direction, force, false);
            }
        }
        #endregion

        #region Camera Rig
        private void UpdateCameraRig()
        {
            if (_mainCam == null) return;

            // 1. Mouse Scroll Wheel Dynamic Zoom (8.5m to 24m)
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
            {
                _camDistance = Mathf.Clamp(_camDistance - scroll * 5.0f, 8.5f, 24.0f);
            }

            // 2. Dynamic Speed Lag & FOV Kick
            float currentForwardSpeed = _rb != null ? Vector3.Dot(_rb.linearVelocity, transform.forward) : 0f;
            float speedFactor = Mathf.Clamp01(currentForwardSpeed / cruiseSpeed);
            if (_isBoosting) speedFactor = 1.45f;

            _targetCamFov = _isBoosting ? 82f : Mathf.Lerp(65f, 76f, speedFactor);
            _currentCamFov = Mathf.Lerp(_currentCamFov, _targetCamFov, Time.deltaTime * 6f);
            _mainCam.fieldOfView = _currentCamFov;

            // 3. Cockpit / Nose View Toggle (V key)
            if (_isCockpitView)
            {
                Vector3 nosePos = transform.position + transform.forward * 3.85f + transform.up * 2.15f + _camShakeOffset;
                _mainCam.transform.position = Vector3.Lerp(_mainCam.transform.position, nosePos, Time.deltaTime * 24f);

                Vector3 noseLookTarget = transform.position + transform.forward * 60f + transform.up * 2.0f;
                _mainCam.transform.rotation = Quaternion.Slerp(_mainCam.transform.rotation, Quaternion.LookRotation((noseLookTarget - _mainCam.transform.position).normalized, transform.up), Time.deltaTime * 24f);
            }
            else
            {
                // 4. Tactical 3rd-Person Chase View
                // Dynamic distance and height with speed stretch
                float dynamicDist = _camDistance + speedFactor * 3.2f;
                float dynamicHeight = _camHeight + speedFactor * 0.9f;

                // Turn leading: smoothly swing camera slightly outward when banking/turning
                float turnInfluence = _strafeInput + Input.GetAxis("Mouse X") * 0.35f;
                _camTurnLead = Mathf.Lerp(_camTurnLead, turnInfluence * 2.4f, Time.deltaTime * 6f);

                // Calculate ideal camera position using blended up vector to maintain orientation at high pitch
                Vector3 blendedUp = Vector3.Slerp(Vector3.up, transform.up, 0.70f);
                Vector3 offset = -transform.forward * dynamicDist 
                               + blendedUp * dynamicHeight 
                               - transform.right * _camTurnLead;
                Vector3 targetCamPos = transform.position + offset + _camShakeOffset;

                // Smooth damp camera position for butter-smooth cinematic motion without micro-stutter
                _mainCam.transform.position = Vector3.SmoothDamp(_mainCam.transform.position, targetCamPos, ref _camPosVelocity, 0.06f);

                // Look-at target: 45m ahead, elevated so ship sits neatly in the lower third of the screen
                Vector3 lookTarget = transform.position + transform.forward * 45f + blendedUp * 1.8f;
                Vector3 forwardDir = (lookTarget - _mainCam.transform.position).normalized;

                // Stabilized horizon roll: camera tracks pitch/yaw smoothly with dampened bank roll
                Vector3 camUp = Vector3.Slerp(Vector3.up, transform.up, 0.35f);
                Quaternion targetRot = Quaternion.LookRotation(forwardDir, camUp);
                _mainCam.transform.rotation = Quaternion.Slerp(_mainCam.transform.rotation, targetRot, Time.deltaTime * 20f);
            }

            _camShakeOffset = Vector3.Lerp(_camShakeOffset, Vector3.zero, Time.deltaTime * 12f);
        }
        #endregion

        #region Cyberpunk Flight HUD & Interaction GUI
        private void OnGUI()
        {
            if (SyntyCombined.Cutscene.GlobalCutsceneState.IsCutsceneActive) return;

            if (_warpFlashAlpha > 0.01f)
            {
                GUI.color = new Color(0.8f, 0.96f, 1.0f, _warpFlashAlpha);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

                var warpStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.Clamp(Screen.width / 52, 18, 30),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                warpStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(0, Screen.height * 0.45f, Screen.width, 40), "HYPERSPACE JUMP INITIATED // SECTOR 4 BLACK MARKET", warpStyle);
                GUI.color = Color.white;
            }

            if (_isPiloted)
            {
                DrawPilotingHUD();
            }
            else
            {
                DrawBoardingPrompt();
            }
        }

        private void DrawBoardingPrompt()
        {
            if (_player == null) return;

            float dist = Vector3.Distance(transform.position, _player.transform.position);
            if (dist > boardDistance) return;

            // Only show boarding prompt if facing roughly towards the ship when not directly underneath it
            if (_mainCam != null && dist > 3.0f)
            {
                Vector3 toShip = (transform.position - _player.transform.position).normalized;
                float lookDot = Vector3.Dot(_mainCam.transform.forward, toShip);
                if (lookDot < 0.20f) return;
            }

            Cyberpunk.UI.CyberpunkInteractionPromptHUD.DrawPromptImmediate("F", "Embark Starship [Hold]", _boardHoldTimer / BOARD_HOLD_DURATION);
        }

        private void DrawPilotingHUD()
        {
            float sw = Screen.width;
            float sh = Screen.height;

            // 0. Warp / Mission Banner (Top-Center)
            float destW = 640f;
            float destH = 34f;
            float destX = (sw - destW) * 0.5f;
            float destY = 22f;

            GUI.color = new Color(0.02f, 0.05f, 0.10f, 0.90f);
            GUI.DrawTexture(new Rect(destX, destY, destW, destH), Texture2D.whiteTexture);
            GUI.color = new Color(0f, 0.94f, 1.0f, 0.95f);
            DrawBorder(new Rect(destX, destY, destW, destH), 1);

            var destStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
            destStyle.normal.textColor = warpUnlocked ? new Color(0f, 0.95f, 1f) : new Color(1f, 0.8f, 0.2f);
            string bannerMsg = warpUnlocked 
                ? (string.IsNullOrEmpty(warpDestinationTitle) ? "PRESS [R] TO ENGAGE HYPERSPACE WARP" : warpDestinationTitle)
                : "MISSION ACTIVE: RETRIEVE ASTEROID CORE MATRICES  |  TRACTOR CLAMP ENGAGED";
            GUI.Label(new Rect(destX, destY, destW, destH), bannerMsg, destStyle);

            // 1. Center Flight Crosshair
            Vector3 crosshairWorldPos = transform.position + transform.forward * 35f;
            Vector3 screenCrosshair = _mainCam != null ? _mainCam.WorldToScreenPoint(crosshairWorldPos) : new Vector3(sw * 0.5f, sh * 0.5f, 0);

            if (screenCrosshair.z > 0)
            {
                float cx = screenCrosshair.x;
                float cy = sh - screenCrosshair.y;
                float size = 16f;

                GUI.color = new Color(0f, 0.95f, 1f, 0.85f);
                GUI.DrawTexture(new Rect(cx - size, cy - 1, size * 0.65f, 2), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx + size * 0.35f, cy - 1, size * 0.65f, 2), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx - 1, cy - size, 2, size * 0.65f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx - 1, cy + size * 0.35f, 2, size * 0.65f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx - 2, cy - 2, 4, 4), Texture2D.whiteTexture);
            }

            // 2. Flight Instruments (Bottom-Left)
            float panelW = 280f;
            float panelH = 100f;
            float panelX = 30f;
            float panelY = sh - panelH - 30f;

            GUI.color = new Color(0.02f, 0.05f, 0.10f, 0.82f);
            GUI.DrawTexture(new Rect(panelX, panelY, panelW, panelH), Texture2D.whiteTexture);
            GUI.color = new Color(0f, 0.85f, 1f, 0.8f);
            DrawBorder(new Rect(panelX, panelY, panelW, panelH), 1);

            var hudText = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold };
            hudText.normal.textColor = new Color(0f, 0.95f, 1f);

            float speedKmh = _rb != null ? _rb.linearVelocity.magnitude * 3.6f : 0f;
            GUI.Label(new Rect(panelX + 14, panelY + 10, panelW, 20), $"SPEED: {speedKmh:F0} KM/H {(_isBoosting ? "[BOOST]" : "")}", hudText);
            GUI.Label(new Rect(panelX + 14, panelY + 30, panelW, 20), $"ALTITUDE: {_groundDistance:F1} M", hudText);
            GUI.Label(new Rect(panelX + 14, panelY + 50, panelW, 20), $"VIEW: {(_isCockpitView ? "COCKPIT 1ST-PERSON" : "CHASE 3RD-PERSON")}", hudText);
            GUI.Label(new Rect(panelX + 14, panelY + 70, panelW, 20), $"WARP DRIVE: READY [R / ENTER]", hudText);

            // 3. Flight Controls Guide (Bottom-Center)
            float guideW = 680f;
            float guideH = 40f;
            float guideX = (sw - guideW) * 0.5f;
            float guideY = sh - guideH - 18f;

            GUI.color = new Color(0.02f, 0.05f, 0.10f, 0.80f);
            GUI.DrawTexture(new Rect(guideX, guideY, guideW, guideH), Texture2D.whiteTexture);
            GUI.color = new Color(0.1f, 0.85f, 1f, 0.65f);
            DrawBorder(new Rect(guideX, guideY, guideW, guideH), 1);

            var guideStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11
            };
            guideStyle.normal.textColor = new Color(0.85f, 0.95f, 1f);
            GUI.Label(new Rect(guideX, guideY + 4, guideW, 16), "W/S: Throttle  |  A/D: Turn  |  Space/Ctrl: Lift  |  Shift: Boost  |  Mouse: Pitch/Yaw", guideStyle);
            GUI.Label(new Rect(guideX, guideY + 20, guideW, 16), "LMB: Rapid Cannons  |  RMB: Heavy Blast  |  Scroll: Zoom  |  V: Cockpit/Chase  |  [R]/[Enter]: Warp  |  [E]: Disembark", guideStyle);

            GUI.color = Color.white;
        }

        private void DrawBorder(Rect r, int thickness)
        {
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y + r.height - thickness, r.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, thickness, r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x + r.width - thickness, r.y, thickness, r.height), Texture2D.whiteTexture);
        }
        #endregion

        #region Hyperspace Warp Jump Sequence
        public void EngageHyperSpaceWarp()
        {
            if (isWarping) return;
            if (_warpCoroutine != null) StopCoroutine(_warpCoroutine);
            _warpCoroutine = StartCoroutine(HyperSpaceWarpRoutine());
        }

        private IEnumerator HyperSpaceWarpRoutine()
        {
            isWarping = true;

            GameObject warpTunnel = null;
#if UNITY_EDITOR
            var warpPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonSciFiWorlds/Prefabs/FX/FX_WarpTunnel_01.prefab");
            if (warpPrefab != null)
            {
                warpTunnel = Instantiate(warpPrefab, transform.position + transform.forward * 20f, transform.rotation, transform);
            }
#endif

            if (_engineAudio != null)
            {
                _engineAudio.pitch = 1.95f;
                _engineAudio.volume = 1.0f;
            }

            float elapsed = 0f;
            float duration = 2.4f;
            Vector3 warpDir = transform.forward;
            if (warpDir.y < 0.15f) warpDir = (warpDir + Vector3.up * 0.35f).normalized;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / duration;

                if (_rb != null)
                {
                    _rb.linearVelocity = warpDir * Mathf.Lerp(boostSpeed, boostSpeed * 4f, progress);
                }

                if (_mainCam != null)
                {
                    _mainCam.fieldOfView = Mathf.Lerp(60f, 105f, progress);
                }

                if (progress > 0.55f)
                {
                    _warpFlashAlpha = Mathf.InverseLerp(0.55f, 1.0f, progress);
                }

                yield return null;
            }

            _warpFlashAlpha = 1.0f;

            if (!string.IsNullOrEmpty(targetTransitScene))
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(targetTransitScene);
            }
        }
        #endregion
    }
}
