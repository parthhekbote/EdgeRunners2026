using UnityEngine;

namespace CyberpunkDev.Traffic
{
    [CreateAssetMenu(fileName = "TrafficSettings", menuName = "Cyberpunk/Traffic/Traffic Settings", order = 1)]
    public class TrafficSettings : ScriptableObject
    {
        [Header("Traffic Density")]
        [Tooltip("Target number of flying cars active in the scene (typically 12-25, supports up to 50).")]
        [Range(6, 50)] public int targetCarCount = 25;

        [Tooltip("Reduced car count triggered when system detects performance drops.")]
        [Range(4, 12)] public int lowFpsCarCount = 8;

        [Tooltip("FPS threshold below which low-performance mode activates.")]
        public float lowFpsThreshold = 45.0f;

        [Tooltip("Seconds below threshold before reducing active car count.")]
        public float lowFpsEvaluationDuration = 3.0f;


        [Header("Global Altitude Calibration")]
        [Tooltip("Global vertical offset applied to all flying car paths. Set to 0 since verified lanes are positioned at exact clear street altitudes.")]
        [Range(-40f, 20f)] public float globalAltitudeOffset = 0.0f;

        [Header("Linear Corridor Spawning & Despawning")]
        [Tooltip("Minimum seconds between vehicle spawns per lane.")]
        [Range(1.5f, 15f)] public float minSpawnInterval = 3.0f;

        [Tooltip("Maximum seconds between vehicle spawns per lane.")]
        [Range(2.0f, 25f)] public float maxSpawnInterval = 6.5f;

        [Tooltip("Minimum distance in meters from lane start before next vehicle can spawn.")]
        [Range(10f, 40f)] public float minSpawnHeadway = 18.0f;

        [Tooltip("Distance near start over which car smoothly scales in upon spawning at start.")]
        [Range(0f, 25f)] public float spawnTransitionDistance = 8.0f;

        [Tooltip("Distance before lane end over which car smoothly scales out before despawning at end.")]
        [Range(0f, 25f)] public float despawnTransitionDistance = 8.0f;

        [Tooltip("If false (default), cars strictly spawn at the START of lines. If true, initial wave is staged along lines on scene start.")]
        public bool prewarmTraffic = false;

        [Header("Speed & Organic Patterns")]
        [Tooltip("Global multiplier applied to all air traffic movement.")]
        [Range(0.1f, 3.0f)] public float globalSpeedMultiplier = 1.0f;

        [Tooltip("Minimum cruise speed in meters per second.")]
        [Range(5f, 25f)] public float minCarSpeed = 8.0f;

        [Tooltip("Maximum cruise speed in meters per second.")]
        [Range(5f, 30f)] public float maxCarSpeed = 15.0f;

        [Tooltip("Vertical floating bob amplitude in meters.")]
        [Range(0.05f, 1.0f)] public float verticalBobAmplitude = 0.25f;

        [Tooltip("Vertical floating bob frequency (cycles per second).")]
        [Range(0.2f, 3.0f)] public float verticalBobFrequency = 1.0f;

        [Tooltip("Enable organic lateral lane wandering for natural non-rail traffic.")]
        public bool enableOrganicWander = true;

        [Tooltip("Amplitude of random lateral lane weaving in meters. Kept small so cars never clip buildings.")]
        [Range(0.05f, 1.0f)] public float wanderAmplitude = 0.25f;

        [Tooltip("Frequency of random lateral lane weaving.")]
        [Range(0.05f, 1.0f)] public float wanderFrequency = 0.20f;

        [Tooltip("Maximum bank angle (Z roll) when turning into sharp curves.")]
        [Range(5f, 40f)] public float maxBankAngle = 22.0f;

        [Tooltip("Interpolation damping speed for banking.")]
        [Range(1f, 15f)] public float bankDamping = 4.5f;

        [Header("Zero-Lag Performance & Culling")]
        [Tooltip("Maximum distance from main camera before mesh renderers are culled.")]
        public float distanceCullThreshold = 200.0f;

        [Tooltip("Distance from camera beyond which engine trails are disabled.")]
        public float trailCullDistance = 75.0f;

        [Tooltip("Angle off camera view vector beyond which objects behind camera are culled.")]
        public float behindCameraCullAngle = 105.0f;

        [Tooltip("Enable distance and frustum culling to eliminate off-screen render work.")]
        public bool enableCulling = true;

        [Header("Proximity-Only 3D Audio")]
        [Tooltip("Enable constant 2D background city drone. Disabled so cars make sound ONLY when nearby.")]
        public bool enableAmbientDrone = false;

        [Tooltip("Maximum concurrent pooled 3D AudioSources for nearby hover cars.")]
        [Range(2, 6)] public int maxPooledAudioSources = 4;

        [Tooltip("Master volume for traffic audio.")]
        [Range(0f, 1f)] public float masterTrafficVolume = 0.55f;

        [Tooltip("Volume of engine thruster hum loop.")]
        [Range(0f, 1f)] public float engineHumVolume = 0.35f;

        [Tooltip("Volume of pass-by aerodynamic whoosh one-shot.")]
        [Range(0f, 1f)] public float passByWhooshVolume = 0.50f;

        [Tooltip("Volume of distant city background traffic drone bed (if enabled).")]
        [Range(0f, 1f)] public float ambientCityTrafficVolume = 0.08f;

        [Tooltip("Min distance for 3D sound rolloff.")]
        public float audioMinDistance = 3.0f;

        [Tooltip("Max distance for 3D sound rolloff. Sound drops to ZERO beyond this distance.")]
        public float audioMaxDistance = 22.0f;

        [Tooltip("Distance within which a pass-by whoosh can be triggered.")]
        public float passByTriggerDistance = 12.0f;

        [Tooltip("Interval in seconds between nearest-car audio source reassignments.")]
        public float audioReassignInterval = 0.35f;
    }
}
