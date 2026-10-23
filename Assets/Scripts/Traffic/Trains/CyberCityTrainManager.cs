using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cyberpunk.Traffic.Trains
{
    [Serializable]
    public class TrainConfiguration
    {
        public string lineName = "Sector 7 Express";
        public Transform cockpit;
        public Transform middleCar1;
        public Transform middleCar2;
        public Transform seedRail;
        public float verticalOffset = -4.39f;
        public float cruiseSpeed = 22.0f;
        public float dwellDuration = 5.0f;
        public Color headlightColor = new Color(0.3f, 0.85f, 1.0f);
        [HideInInspector] public CyberTrainConsist consist;
        [HideInInspector] public CyberTrainPath path;
    }

    /// <summary>
    /// Master coordinator for all monorail transit trains in Cyberpunk Demo City.
    /// Manages track spline construction, consist initialization, and runtime scheduling.
    /// </summary>
    [DisallowMultipleComponent]
    public class CyberCityTrainManager : MonoBehaviour
    {
        private static CyberCityTrainManager _instance;
        public static CyberCityTrainManager Instance => _instance;

        [Header("Scene References")]
        [Tooltip("The root 'Trains' GameObject containing all rail pieces.")]
        public Transform railsRoot;

        [Header("Global Audio")]
        [Tooltip("Audio clip for monorail electric motor hum.")]
        public AudioClip trainAudioClip;

        [Header("Train Fleets")]
        public List<TrainConfiguration> trainFleet = new List<TrainConfiguration>();

        [Header("Simulation Settings")]
        [Tooltip("Auto-discover and initialize all trains on scene Start.")]
        public bool autoInitialize = true;

        [Tooltip("Master speed multiplier for all trains in the city.")]
        [Range(0.2f, 2.5f)]
        public float speedMultiplier = 1.0f;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        private void Start()
        {
            if (autoInitialize)
            {
                InitializeAllTrains();
            }
        }

        /// <summary>
        /// Auto-locates the 3 pre-existing trains in Demo_City if not explicitly configured in Inspector.
        /// </summary>
        public void AutoDiscoverTrains()
        {
            if (railsRoot == null)
            {
                GameObject trObj = GameObject.Find("Trains");
                if (trObj != null) railsRoot = trObj.transform;
            }

            if (trainFleet.Count == 0)
            {
                // Train 1: High Eastern Skybridge Line (Underslung, Y = 25.38m)
                Transform c1 = FindTransform("SM_Veh_Train_Cockpit_01 (7)");
                Transform m1_1 = FindTransform("SM_Veh_Train_Middle_01 (9)");
                Transform m1_2 = FindTransform("SM_Veh_Train_Middle_01 (10)");
                Transform seed1 = FindTransform("SM_Bld_Train_Rail_Straight_01 (11)");

                if (c1 != null && seed1 != null)
                {
                    trainFleet.Add(new TrainConfiguration
                    {
                        lineName = "Skyline Line (Upper Skybridge)",
                        cockpit = c1,
                        middleCar1 = m1_1,
                        middleCar2 = m1_2,
                        seedRail = seed1,
                        verticalOffset = -4.39f,
                        cruiseSpeed = 24.0f,
                        dwellDuration = 5.0f,
                        headlightColor = new Color(0.2f, 0.9f, 1.0f) // Cyan
                    });
                }

                // Train 2: Mid Boulevard Central Line (Overslung, Y = 16.50m)
                Transform c2 = FindTransform("SM_Veh_Train_Cockpit_01 (1)");
                Transform m2_1 = FindTransform("SM_Veh_Train_Middle_01 (2)");
                Transform m2_2 = FindTransform("SM_Veh_Train_Middle_01 (3)");
                Transform seed2 = FindTransform("SM_Bld_Train_Rail_Straight_01");

                if (c2 != null && seed2 != null)
                {
                    trainFleet.Add(new TrainConfiguration
                    {
                        lineName = "Boulevard Metro (Mid Level)",
                        cockpit = c2,
                        middleCar1 = m2_1,
                        middleCar2 = m2_2,
                        seedRail = seed2,
                        verticalOffset = 1.38f, // Overslung on rail
                        cruiseSpeed = 20.0f,
                        dwellDuration = 6.0f,
                        headlightColor = new Color(1.0f, 0.75f, 0.2f) // Amber Gold
                    });
                }

                // Train 3: Lower Deck Industrial Line (Underslung, Y = -10.25m)
                Transform c3 = FindTransform("SM_Veh_Train_Cockpit_01");
                Transform m3_1 = FindTransform("SM_Veh_Train_Middle_01");
                Transform m3_2 = FindTransform("SM_Veh_Train_Middle_01 (1)");
                Transform seed3 = FindTransform("SM_Bld_Train_Rail_Straight_01 (3)");

                if (c3 != null && seed3 != null)
                {
                    trainFleet.Add(new TrainConfiguration
                    {
                        lineName = "Lower Canal Express (Lower Deck)",
                        cockpit = c3,
                        middleCar1 = m3_1,
                        middleCar2 = m3_2,
                        seedRail = seed3,
                        verticalOffset = -4.58f,
                        cruiseSpeed = 22.0f,
                        dwellDuration = 5.0f,
                        headlightColor = new Color(0.4f, 1.0f, 0.5f) // Neon Emerald
                    });
                }
            }
        }

        public void InitializeAllTrains()
        {
            if (railsRoot == null)
            {
                AutoDiscoverTrains();
            }

            if (trainFleet.Count == 0)
            {
                AutoDiscoverTrains();
            }

            if (railsRoot == null)
            {
                Debug.LogWarning("[CyberCityTrainManager] Rails root not found in scene!");
                return;
            }

            foreach (var train in trainFleet)
            {
                InitializeTrain(train);
            }

            Debug.Log($"[CyberCityTrainManager] Successfully initialized {trainFleet.Count} monorail trains across the city!");
        }

        private void InitializeTrain(TrainConfiguration config)
        {
            if (config.cockpit == null || config.seedRail == null)
            {
                Debug.LogWarning($"[CyberCityTrainManager] Skipping train '{config.lineName}': missing cockpit or seed rail.");
                return;
            }

            // Build parametric track spline
            config.path = CyberTrackBuilder.BuildPathFromSeed(config.seedRail, railsRoot, 14);
            if (config.path == null || !config.path.IsValid)
            {
                Debug.LogError($"[CyberCityTrainManager] Failed to construct track path for '{config.lineName}'");
                return;
            }

            // Find closest distance on the path to current cockpit position
            float initialDistance = config.path.FindClosestDistance(config.cockpit.position);

            // Attach or get CyberTrainConsist
            CyberTrainConsist consist = config.cockpit.GetComponent<CyberTrainConsist>();
            if (consist == null)
            {
                consist = config.cockpit.gameObject.AddComponent<CyberTrainConsist>();
            }

            consist.cockpit = config.cockpit;
            consist.middleCar1 = config.middleCar1;
            consist.middleCar2 = config.middleCar2;
            consist.cruiseSpeed = config.cruiseSpeed * speedMultiplier;
            consist.stationDwellDuration = config.dwellDuration;
            consist.headlightColor = config.headlightColor;
            consist.engineHumClip = trainAudioClip;
            consist.terminusBuffer = 26.0f;

            // Initialize consist on path with designated vertical offset
            consist.Initialize(config.path, initialDistance, config.verticalOffset);
            config.consist = consist;
        }

        private Transform FindTransform(string name)
        {
            GameObject obj = GameObject.Find(name);
            return obj != null ? obj.transform : null;
        }

        private void OnDrawGizmos()
        {
            if (trainFleet == null) return;
            foreach (var t in trainFleet)
            {
                if (t.path != null)
                {
                    t.path.DrawGizmos(t.headlightColor);
                }
            }
        }
    }
}
