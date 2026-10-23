using System.Collections.Generic;
using UnityEngine;

namespace CyberpunkDev.Traffic
{
    /// <summary>
    /// Master Air Traffic Coordinator.
    /// Strictly complies with the Zero-Lag Architecture:
    /// - SINGLE Update() loop executing all car kinematics and culling.
    /// - ZERO per-frame memory allocations (GC-free).
    /// - Distance and frustum culling for meshes and trails.
    /// - Automatic FPS degradation fallback (reduces to 8 cars if < 45 FPS).
    /// </summary>
    public class FlyingCarManager : MonoBehaviour
    {
        public static FlyingCarManager Instance { get; private set; }

        [Header("Configuration")]
        [SerializeField] private TrafficSettings settings;

        [Header("Air Lanes (Low, Mid, High)")]
        [SerializeField] private AirLane[] lanes;

        [Header("Hover Vehicle Prefabs")]
        [SerializeField] private GameObject[] carPrefabs;

        [Header("Audio Integration")]
        [SerializeField] private FlyingCarAudioManager audioManager;

        // Pre-allocated object pool and active lists
        private readonly List<HoverCar> m_AllCarsPool = new List<HoverCar>(24);
        private readonly List<HoverCar> m_ActiveCars = new List<HoverCar>(24);
        private float[] m_LaneSpawnTimers;

        // Performance monitoring & auto-fallback
        private float m_FpsAccumulator;
        private int m_FpsFrameCount;
        private float m_FpsEvaluationTimer;
        private float m_LowFpsDuration;
        private bool m_IsLowFpsFallbackActive;

        // Cached camera references
        private Camera m_MainCamera;
        private Transform m_CameraTransform;

        public TrafficSettings Settings => settings;
        public IReadOnlyList<HoverCar> ActiveCars => m_ActiveCars;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            ResolveCamera();
            InitializeAirTraffic();
        }

        private void Start()
        {
            if (audioManager != null)
            {
                audioManager.Initialize(settings, m_CameraTransform);
            }
        }

        private void ResolveCamera()
        {
            m_MainCamera = Camera.main;
            if (m_MainCamera != null)
            {
                m_CameraTransform = m_MainCamera.transform;
            }
        }

        /// <summary>
        /// Instantiates and pre-allocates vehicles into the idle pool.
        /// Configures linear lanes with zero loops and initializes per-lane spawn timers.
        /// </summary>
        public void InitializeAirTraffic()
        {
            if (settings == null)
            {
                Debug.LogError("[FlyingCarManager] TrafficSettings is missing!");
                return;
            }

            EnsureObstacleFreeLanes();

            // Ensure splines are initialized
            for (int i = 0; i < lanes.Length; i++)
            {
                if (lanes[i] != null) lanes[i].RebuildSpline();
            }

            // Auto-discover vehicle prefabs if missing or incomplete
            if (carPrefabs == null || carPrefabs.Length < 7)
            {
                var resourcePrefabs = Resources.LoadAll<GameObject>("Vehicles_Hover");
                if (resourcePrefabs != null && resourcePrefabs.Length > 0)
                {
                    carPrefabs = resourcePrefabs;
                }
#if UNITY_EDITOR
                if (carPrefabs == null || carPrefabs.Length < 7)
                {
                    string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Vehicles_Hover", "Assets/Resources/Vehicles_Hover" });
                    if (guids != null && guids.Length > 0)
                    {
                        var loadedList = new List<GameObject>();
                        for (int g = 0; g < guids.Length; g++)
                        {
                            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[g]);
                            var p = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                            if (p != null && (p.GetComponent<HoverCar>() != null || p.name.StartsWith("Hover_")))
                            {
                                loadedList.Add(p);
                            }
                        }
                        if (loadedList.Count > 0)
                        {
                            carPrefabs = loadedList.ToArray();
                        }
                    }
                }
#endif
            }

            if (carPrefabs == null || carPrefabs.Length == 0)
            {
                Debug.LogError("[FlyingCarManager] No vehicle prefabs found in scene or Resources/Vehicles_Hover.");
                return;
            }

            // Load Synty colorway palettes for visual diversity
            Material[] palettes = null;
#if UNITY_EDITOR
            string[] matGuids = UnityEditor.AssetDatabase.FindAssets("t:Material", new[] { "Assets/PolygonCyberCity/Materials/Alts" });
            if (matGuids != null && matGuids.Length > 0)
            {
                var matList = new List<Material>();
                for (int m = 0; m < matGuids.Length; m++)
                {
                    string p = UnityEditor.AssetDatabase.GUIDToAssetPath(matGuids[m]);
                    var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(p);
                    if (mat != null && mat.name.StartsWith("PolygonCyberCity_")) matList.Add(mat);
                }
                if (matList.Count > 0) palettes = matList.ToArray();
            }
#endif

            // Clean up any existing instances in pool and scene hierarchy
            for (int i = 0; i < m_AllCarsPool.Count; i++)
            {
                if (m_AllCarsPool[i] != null)
                {
                    if (Application.isPlaying) Destroy(m_AllCarsPool[i].gameObject);
                    else DestroyImmediate(m_AllCarsPool[i].gameObject);
                }
            }
            m_AllCarsPool.Clear();
            m_ActiveCars.Clear();

            Transform existingRoot = transform.Find("Traffic_Spawned_Cars");
            if (existingRoot != null)
            {
                if (Application.isPlaying) Destroy(existingRoot.gameObject);
                else DestroyImmediate(existingRoot.gameObject);
            }

            int targetTotal = Mathf.Max(settings.targetCarCount, 20);
            int carsPerLane = Mathf.CeilToInt((float)targetTotal / lanes.Length);

            Transform carsRoot = new GameObject("Traffic_Spawned_Cars").transform;
            carsRoot.SetParent(transform);

            // 8 vibrant cyberpunk neon colorways
            Color[] neonPalette = new Color[] {
                new Color(0.05f, 0.95f, 1.0f),  // Cyber Cyan
                new Color(1.0f, 0.15f, 0.85f),  // Hot Magenta
                new Color(0.1f, 1.0f, 0.45f),   // Acid Lime
                new Color(1.0f, 0.75f, 0.05f),  // Amber Gold
                new Color(0.55f, 0.15f, 1.0f),  // Deep Violet
                new Color(0.15f, 0.65f, 1.0f),  // Electric Blue
                new Color(1.0f, 0.35f, 0.05f),  // Neon Orange
                new Color(1.0f, 0.95f, 0.2f)   // Solar Yellow
            };

            int carCounter = 0;
            for (int laneIdx = 0; laneIdx < lanes.Length; laneIdx++)
            {
                AirLane lane = lanes[laneIdx];
                if (lane == null) continue;

                for (int c = 0; c < carsPerLane; c++)
                {
                    if (carCounter >= targetTotal) break;

                    GameObject prefab = carPrefabs[(carCounter * 3 + laneIdx * 2 + c) % carPrefabs.Length];
                    if (prefab == null) continue;

                    GameObject carGO = Instantiate(prefab, carsRoot);
                    carGO.name = $"Car_{lane.laneName}_{c:D2}_{prefab.name}";

                    HoverCar hoverCar = carGO.GetComponent<HoverCar>();
                    if (hoverCar == null)
                    {
                        hoverCar = carGO.AddComponent<HoverCar>();
                    }

                    hoverCar.carId = carGO.name;
                    hoverCar.laneIndex = laneIdx;
                    hoverCar.currentLane = lane;

                    // Speed profile
                    float speedProfile = Random.Range(0.85f, 1.25f);
                    if (prefab.name.Contains("Truck") || prefab.name.Contains("Van")) speedProfile = Random.Range(0.80f, 0.95f);
                    else if (prefab.name.Contains("Police") || prefab.name.Contains("Luxury")) speedProfile = Random.Range(1.10f, 1.30f);

                    hoverCar.baseSpeed = Random.Range(settings.minCarSpeed, settings.maxCarSpeed) * speedProfile;
                    hoverCar.currentSpeed = hoverCar.baseSpeed;
                    hoverCar.targetSpeed = hoverCar.baseSpeed;
                    hoverCar.bobPhaseOffset = Random.Range(0f, 15f);

                    float maxOffset = (lane.altitude > 10f ? 0.40f : 0.15f);
                    hoverCar.lateralOffset = Random.Range(-maxOffset, maxOffset);
                    hoverCar.wanderSeed = Random.Range(0f, 200f);
                    hoverCar.altitudeJitter = Random.Range(-0.10f, 0.10f);
                    hoverCar.CacheComponents();

                    if (palettes != null && palettes.Length > 0)
                    {
                        Material randomPalette = palettes[Random.Range(0, palettes.Length)];
                        hoverCar.ApplyMaterialPalette(randomPalette);
                    }

                    Color neonColor = neonPalette[carCounter % neonPalette.Length];
                    hoverCar.ApplyCustomNeonColor(neonColor);

                    // Deactivate and pool car initially
                    hoverCar.Despawn();
                    m_AllCarsPool.Add(hoverCar);
                    carCounter++;
                }
            }

            // Initialize per-lane spawn timers
            m_LaneSpawnTimers = new float[lanes.Length];
            for (int l = 0; l < lanes.Length; l++)
            {
                // Stagger initial launches so cars start appearing at the start of each line sequentially
                m_LaneSpawnTimers[l] = l * 0.75f;
            }

            // If prewarm is enabled, stage initial cars along the lines
            if (settings.prewarmTraffic)
            {
                int activeSlots = Mathf.Min(lanes.Length, m_AllCarsPool.Count);
                for (int l = 0; l < activeSlots; l++)
                {
                    AirLane lane = lanes[l];
                    if (lane == null) continue;
                    HoverCar car = m_AllCarsPool[l];
                    float startDist = Random.Range(10f, Mathf.Max(lane.TotalLength * 0.5f, 15f));
                    car.SpawnAtStart(lane, l, startDist);
                    Vector3 initPos = lane.GetPointAtDistance(startDist);
                    initPos.y += settings.globalAltitudeOffset + car.altitudeJitter;
                    Vector3 initTan = lane.GetTangentAtDistance(startDist);
                    car.SetPose(initPos, Quaternion.LookRotation(initTan, Vector3.up));
                    m_ActiveCars.Add(car);
                }
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0.0001f) return;

            MonitorPerformance(dt);

            if (m_CameraTransform == null)
            {
                ResolveCamera();
                if (m_CameraTransform == null) return;
            }

            Vector3 camPos = m_CameraTransform.position;
            Vector3 camForward = m_CameraTransform.forward;
            float time = Time.time;
            float speedMult = settings.globalSpeedMultiplier;
            float bobAmp = settings.verticalBobAmplitude;
            float bobFreq = settings.verticalBobFrequency * Mathf.PI * 2f;
            float maxBank = settings.maxBankAngle;
            float bankDamping = settings.bankDamping;
            float cullDistSqr = settings.distanceCullThreshold * settings.distanceCullThreshold;
            float trailDistSqr = settings.trailCullDistance * settings.trailCullDistance;
            bool enableCulling = settings.enableCulling;
            bool enableWander = settings.enableOrganicWander;
            float wanderAmp = settings.wanderAmplitude;
            float wanderFreq = settings.wanderFrequency;
            float altitudeOffset = settings.globalAltitudeOffset;

            int targetCount = m_IsLowFpsFallbackActive ? settings.lowFpsCarCount : settings.targetCarCount;

            // 1. Per-Lane Spawner Loop: Spawns cars strictly at the START of the line
            if (lanes != null && m_LaneSpawnTimers != null)
            {
                for (int l = 0; l < lanes.Length; l++)
                {
                    AirLane lane = lanes[l];
                    if (lane == null) continue;

                    m_LaneSpawnTimers[l] -= dt;
                    if (m_LaneSpawnTimers[l] <= 0f)
                    {
                        // Check overall active vehicle capacity
                        if (m_ActiveCars.Count >= targetCount)
                        {
                            m_LaneSpawnTimers[l] = 1.5f; // Wait and retry when a car despawns
                            continue;
                        }

                        // Headway clearance check at lane start: ensure no car is blocking the entrance
                        bool startBlocked = false;
                        for (int a = 0; a < m_ActiveCars.Count; a++)
                        {
                            HoverCar existing = m_ActiveCars[a];
                            if (existing != null && existing.isCarActive && existing.laneIndex == l)
                            {
                                if (existing.distanceAlongLane < settings.minSpawnHeadway)
                                {
                                    startBlocked = true;
                                    break;
                                }
                            }
                        }

                        if (startBlocked)
                        {
                            m_LaneSpawnTimers[l] = 1.0f; // Postpone launch until entrance clears
                            continue;
                        }

                        // Find an idle car from the pool
                        HoverCar freeCar = null;
                        for (int p = 0; p < m_AllCarsPool.Count; p++)
                        {
                            if (!m_AllCarsPool[p].isCarActive)
                            {
                                freeCar = m_AllCarsPool[p];
                                break;
                            }
                        }

                        if (freeCar != null)
                        {
                            freeCar.baseSpeed = Random.Range(settings.minCarSpeed, settings.maxCarSpeed);
                            freeCar.currentSpeed = freeCar.baseSpeed;
                            freeCar.targetSpeed = freeCar.baseSpeed;
                            freeCar.wanderSeed = Random.Range(0f, 200f);
                            freeCar.bobPhaseOffset = Random.Range(0f, 15f);

                            // Launch strictly at the START of the line (distance 0)
                            freeCar.SpawnAtStart(lane, l, 0f);

                            Vector3 initPos = lane.GetPointAtDistance(0f);
                            initPos.y += altitudeOffset + freeCar.altitudeJitter;
                            Vector3 initTan = lane.GetTangentAtDistance(0f);
                            freeCar.SetPose(initPos, Quaternion.LookRotation(initTan, Vector3.up));

                            m_ActiveCars.Add(freeCar);

                            // Set interval for next car spawn on this lane
                            m_LaneSpawnTimers[l] = Random.Range(settings.minSpawnInterval, settings.maxSpawnInterval);
                        }
                        else
                        {
                            m_LaneSpawnTimers[l] = 2.0f;
                        }
                    }
                }
            }

            // 2. Active Vehicles Kinematic & Despawn Loop
            for (int i = m_ActiveCars.Count - 1; i >= 0; i--)
            {
                HoverCar car = m_ActiveCars[i];
                if (car == null || !car.isCarActive)
                {
                    m_ActiveCars.RemoveAt(i);
                    continue;
                }

                AirLane lane = car.currentLane;
                if (lane == null)
                {
                    car.Despawn();
                    m_ActiveCars.RemoveAt(i);
                    continue;
                }

                // Dynamic Headway Control: smooth deceleration when catching up to a car ahead
                float desiredSpeed = car.baseSpeed;
                for (int j = 0; j < m_ActiveCars.Count; j++)
                {
                    if (i == j) continue;
                    HoverCar other = m_ActiveCars[j];
                    if (other != null && other.isCarActive && other.laneIndex == car.laneIndex)
                    {
                        float diff = other.distanceAlongLane - car.distanceAlongLane;
                        if (diff > 0f && diff < 16.0f) // Car ahead within 16 meters
                        {
                            desiredSpeed = Mathf.Min(car.baseSpeed * 0.65f, other.currentSpeed * 0.90f);
                            break;
                        }
                    }
                }
                car.currentSpeed = Mathf.Lerp(car.currentSpeed, desiredSpeed, dt * 2.0f);

                // Advance distance along spline
                float microVar = 1.0f + 0.08f * Mathf.Sin(time * 0.5f + car.wanderSeed);
                car.distanceAlongLane += car.currentSpeed * microVar * speedMult * dt;

                // REACHED END OF LINE -> DESPAWN AT END!
                if (car.distanceAlongLane >= lane.TotalLength)
                {
                    car.Despawn();
                    m_ActiveCars.RemoveAt(i);
                    continue;
                }

                // Smooth visual ease-in at start (spawn) and ease-out at end (despawn)
                car.UpdateSpawnDespawnVisuals(settings.spawnTransitionDistance, settings.despawnTransitionDistance, lane.TotalLength);

                float currentDist = car.distanceAlongLane;

                // Evaluate spline position and forward tangent
                Vector3 splinePos = lane.GetPointAtDistance(currentDist);
                Vector3 tangent = lane.GetTangentAtDistance(currentDist);

                // Dynamic banking calculation (lookahead tangent delta)
                Vector3 aheadTangent = lane.GetTangentAtDistance(Mathf.Min(currentDist + 4.5f, lane.TotalLength));
                Vector3 turnAxis = Vector3.Cross(tangent, aheadTangent);
                float turnAngle = turnAxis.y * Mathf.Rad2Deg;
                float targetBankRoll = Mathf.Clamp(-turnAngle * 2.8f, -maxBank, maxBank);
                car.currentBankRoll = Mathf.Lerp(car.currentBankRoll, targetBankRoll, dt * bankDamping);

                // Hover bob, organic lateral wander, and altitude calibration
                float bob = Mathf.Sin((time + car.bobPhaseOffset) * bobFreq) * bobAmp;
                float wander = 0f;
                if (enableWander)
                {
                    float wProg = time * wanderFreq + car.wanderSeed;
                    wander = (Mathf.Sin(wProg) * 0.7f + Mathf.Sin(wProg * 2.2f) * 0.3f) * wanderAmp;
                    if (lane.altitude < 10f) wander *= 0.35f;
                }

                Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
                Vector3 finalPos = splinePos + (Vector3.up * (bob + altitudeOffset + car.altitudeJitter)) + (right * (car.lateralOffset + wander));

                // Compute rotation with banking roll
                Quaternion forwardRot = Quaternion.LookRotation(tangent, Vector3.up);
                Quaternion finalRot = forwardRot * Quaternion.Euler(0f, 0f, car.currentBankRoll);

                car.SetPose(finalPos, finalRot);

                // Zero-Lag Culling (Frustum & Distance)
                if (enableCulling)
                {
                    Vector3 toCar = finalPos - camPos;
                    float distSqr = toCar.sqrMagnitude;

                    if (distSqr > cullDistSqr)
                    {
                        car.SetMeshVisibility(false);
                        car.SetTrailsActive(false);
                    }
                    else
                    {
                        bool isBehindCam = distSqr > (30f * 30f) && (Vector3.Dot(camForward, toCar.normalized) < -0.2f);
                        if (isBehindCam)
                        {
                            car.SetMeshVisibility(false);
                            car.SetTrailsActive(false);
                        }
                        else
                        {
                            car.SetMeshVisibility(true);
                            car.SetTrailsActive(distSqr <= trailDistSqr);
                        }
                    }
                }
            }

            // 3. Update 3D Audio assignment to nearest cars (Proximity-Only)
            if (audioManager != null)
            {
                audioManager.UpdateAudioAssignments(m_ActiveCars);
            }
        }

        /// <summary>
        /// Ensures obstacle-free transit corridors spanning the entire city.
        /// Preserves all user-created, moved, and duplicated lanes.
        /// </summary>
        private void EnsureObstacleFreeLanes()
        {
            // First, discover any existing AirLane components under this manager or in the scene
            var existingLanes = GetComponentsInChildren<AirLane>(true);
            if (existingLanes != null && existingLanes.Length > 0)
            {
                var activeLaneList = new List<AirLane>();
                for (int i = 0; i < existingLanes.Length; i++)
                {
                    if (existingLanes[i] != null && existingLanes[i].gameObject.activeSelf)
                    {
                        existingLanes[i].RebuildSpline();
                        activeLaneList.Add(existingLanes[i]);
                    }
                }

                if (activeLaneList.Count > 0)
                {
                    lanes = activeLaneList.ToArray();
                    return;
                }
            }

            // Only generate default 8 corridors if no lanes exist in the scene:
            Transform root = transform;

            // 1. Low Canyon Northbound: Lower street traffic (Y = -13.5m, X = -2.0m, Z in [-250m, 250m])
            AirLane laneCanyonN = CreateOrGetLane(root, "Lane_Straight_Canyon_North", -13.5f, false, true, new Color(0.05f, 0.95f, 1.0f), new Vector3[] {
                new Vector3(-2.0f, -13.5f, -250.0f),
                new Vector3(-2.0f, -13.5f, -125.0f),
                new Vector3(-2.0f, -13.5f, 0.0f),
                new Vector3(-2.0f, -13.5f, 125.0f),
                new Vector3(-2.0f, -13.5f, 250.0f)
            });

            // 2. Low Canyon Southbound: Lower street traffic (Y = -13.5m, X = +2.5m, Z in [250m, -250m])
            AirLane laneCanyonS = CreateOrGetLane(root, "Lane_Straight_Canyon_South", -13.5f, false, true, new Color(1.0f, 0.75f, 0.05f), new Vector3[] {
                new Vector3(2.5f, -13.5f, 250.0f),
                new Vector3(2.5f, -13.5f, 125.0f),
                new Vector3(2.5f, -13.5f, 0.0f),
                new Vector3(2.5f, -13.5f, -125.0f),
                new Vector3(2.5f, -13.5f, -250.0f)
            });

            // 3. Mid Canyon Northbound: Mid-altitude transit (Y = 8.0m, X = -2.5m, Z in [-220m, 220m])
            AirLane laneMidN = CreateOrGetLane(root, "Lane_Straight_Mid_North", 8.0f, false, true, new Color(0.15f, 0.65f, 1.0f), new Vector3[] {
                new Vector3(-2.5f, 8.0f, -220.0f),
                new Vector3(-2.5f, 8.0f, -110.0f),
                new Vector3(-2.5f, 8.0f, 0.0f),
                new Vector3(-2.5f, 8.0f, 110.0f),
                new Vector3(-2.5f, 8.0f, 220.0f)
            });

            // 4. Mid Canyon Southbound: Mid-altitude transit (Y = 8.0m, X = +2.5m, Z in [220m, -220m])
            AirLane laneMidS = CreateOrGetLane(root, "Lane_Straight_Mid_South", 8.0f, false, true, new Color(1.0f, 0.15f, 0.85f), new Vector3[] {
                new Vector3(2.5f, 8.0f, 220.0f),
                new Vector3(2.5f, 8.0f, 110.0f),
                new Vector3(2.5f, 8.0f, 0.0f),
                new Vector3(2.5f, 8.0f, -110.0f),
                new Vector3(2.5f, 8.0f, -220.0f)
            });

            // 5. Elevated Skyway Northbound: High bridge altitude (Y = 20.0m, X = -2.0m, Z in [-220m, 220m])
            AirLane laneSkywayN = CreateOrGetLane(root, "Lane_Straight_Skyway_North", 20.0f, false, true, new Color(0.95f, 0.45f, 0.1f), new Vector3[] {
                new Vector3(-2.0f, 20.0f, -220.0f),
                new Vector3(-2.0f, 20.0f, -110.0f),
                new Vector3(-2.0f, 20.0f, 0.0f),
                new Vector3(-2.0f, 20.0f, 110.0f),
                new Vector3(-2.0f, 20.0f, 220.0f)
            });

            // 6. Elevated Skyway Southbound: High bridge altitude (Y = 20.0f, X = +2.5m, Z in [220m, -220m])
            AirLane laneSkywayS = CreateOrGetLane(root, "Lane_Straight_Skyway_South", 20.0f, false, true, new Color(0.65f, 0.15f, 1.0f), new Vector3[] {
                new Vector3(2.5f, 20.0f, 220.0f),
                new Vector3(2.5f, 20.0f, 110.0f),
                new Vector3(2.5f, 20.0f, 0.0f),
                new Vector3(2.5f, 20.0f, -110.0f),
                new Vector3(2.5f, 20.0f, -220.0f)
            });

            // 7. High Skyline Express: Trans-city diagonal express (Y = 22.0m, from (-10, 22, -220) to (10, 22, 220))
            AirLane laneSkylineExpress = CreateOrGetLane(root, "Lane_High_Skyline_Express", 22.0f, false, true, new Color(1.0f, 0.95f, 0.2f), new Vector3[] {
                new Vector3(-10.0f, 22.0f, -220.0f),
                new Vector3(-5.0f, 22.0f, -110.0f),
                new Vector3(0.0f, 22.0f, 0.0f),
                new Vector3(5.0f, 22.0f, 110.0f),
                new Vector3(10.0f, 22.0f, 220.0f)
            });

            // 8. High Skyline Return: High rooftop trans-city cruiser (Y = 32.0m, X = 0.0m, Z in [250m, -250m])
            AirLane laneSkylineReturn = CreateOrGetLane(root, "Lane_High_Skyline_Return", 32.0f, false, true, new Color(0.2f, 1.0f, 0.4f), new Vector3[] {
                new Vector3(0.0f, 32.0f, 250.0f),
                new Vector3(0.0f, 32.0f, 125.0f),
                new Vector3(0.0f, 32.0f, 0.0f),
                new Vector3(0.0f, 32.0f, -125.0f),
                new Vector3(0.0f, 32.0f, -250.0f)
            });

            lanes = new AirLane[] { laneCanyonN, laneCanyonS, laneMidN, laneMidS, laneSkywayN, laneSkywayS, laneSkylineExpress, laneSkylineReturn };
        }

        private AirLane CreateOrGetLane(Transform parent, string laneName, float altitude, bool isLoop, bool isClockwise, Color color, Vector3[] waypointCoords)
        {
            Transform existing = parent.Find(laneName);
            if (existing != null)
            {
                AirLane existingLane = existing.GetComponent<AirLane>();
                if (existingLane != null && existingLane.waypoints != null && existingLane.waypoints.Count >= 2)
                {
                    // Lane already exists with custom waypoints - PRESERVE THEM!
                    return existingLane;
                }
            }

            GameObject laneGO = existing != null ? existing.gameObject : new GameObject(laneName);
            laneGO.transform.SetParent(parent);
            laneGO.transform.localPosition = Vector3.zero;

            AirLane lane = laneGO.GetComponent<AirLane>();
            if (lane == null) lane = laneGO.AddComponent<AirLane>();

            lane.laneName = laneName;
            lane.altitude = altitude;
            lane.altitudeOffset = 0f;
            lane.isLoop = isLoop;
            lane.isClockwise = isClockwise;
            lane.pathColor = color;

            // Only populate default waypoints if none exist
            if (laneGO.transform.childCount == 0)
            {
                lane.waypoints = new List<Transform>();
                for (int i = 0; i < waypointCoords.Length; i++)
                {
                    GameObject wp = new GameObject($"WP_{i + 1:D2}");
                    wp.transform.SetParent(laneGO.transform);
                    wp.transform.position = waypointCoords[i];
                    lane.waypoints.Add(wp.transform);
                }
            }

            lane.RebuildSpline();
            return lane;
        }

        /// <summary>
        /// Monitors real-time FPS and automatically reduces car count to lowFpsCarCount if FPS drops below threshold.
        /// </summary>
        private void MonitorPerformance(float dt)
        {
            m_FpsAccumulator += 1f / Mathf.Max(dt, 0.0001f);
            m_FpsFrameCount++;
            m_FpsEvaluationTimer += dt;

            if (m_FpsEvaluationTimer >= 0.5f)
            {
                float avgFps = m_FpsAccumulator / m_FpsFrameCount;
                m_FpsAccumulator = 0f;
                m_FpsFrameCount = 0;
                m_FpsEvaluationTimer = 0f;

                if (avgFps < settings.lowFpsThreshold)
                {
                    m_LowFpsDuration += 0.5f;
                    if (m_LowFpsDuration >= settings.lowFpsEvaluationDuration && !m_IsLowFpsFallbackActive)
                    {
                        ActivateLowFpsFallback(true);
                    }
                }
                else if (avgFps > 55f)
                {
                    m_LowFpsDuration = 0f;
                    if (m_IsLowFpsFallbackActive)
                    {
                        ActivateLowFpsFallback(false);
                    }
                }
            }
        }

        private void ActivateLowFpsFallback(bool activate)
        {
            m_IsLowFpsFallbackActive = activate;
            int desiredCount = activate ? settings.lowFpsCarCount : settings.targetCarCount;
            desiredCount = Mathf.Clamp(desiredCount, 4, m_AllCarsPool.Count);

            while (m_ActiveCars.Count > desiredCount && m_ActiveCars.Count > 0)
            {
                int lastIdx = m_ActiveCars.Count - 1;
                m_ActiveCars[lastIdx].Despawn();
                m_ActiveCars.RemoveAt(lastIdx);
            }

            Debug.Log($"[FlyingCarManager] Performance Auto-Fallback: Target active cars adjusted to {desiredCount} (Triggered: {activate})");
        }
    }
}

