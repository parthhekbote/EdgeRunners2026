using UnityEngine;

namespace CyberpunkDev.Traffic
{
    /// <summary>
    /// Pure data/state container for an individual flying hover car.
    /// STRICT PERFORMANCE RULE: Contains NO Update() loop or Rigidbody physics.
    /// All movement, banking, and culling are executed in FlyingCarManager's master loop.
    /// </summary>
    [DisallowMultipleComponent]
    public class HoverCar : MonoBehaviour
    {
        [Header("Car Identity")]
        public string carId;
        public int laneIndex;

        [Header("Movement State (Managed by FlyingCarManager)")]
        [System.NonSerialized] public AirLane currentLane;
        [System.NonSerialized] public float distanceAlongLane;
        [System.NonSerialized] public float baseSpeed;
        [System.NonSerialized] public float currentSpeed;
        [System.NonSerialized] public float targetSpeed;
        [System.NonSerialized] public float bobPhaseOffset;
        [System.NonSerialized] public float currentBankRoll;
        [System.NonSerialized] public float lateralOffset;
        [System.NonSerialized] public float wanderSeed;
        [System.NonSerialized] public float altitudeJitter;
        [System.NonSerialized] public float lastDistToListener = 999f;
        [System.NonSerialized] public float whooshCooldown = 0f;
        [System.NonSerialized] public bool isApproachingListener = false;
        [System.NonSerialized] public bool isCarActive = false;
        [System.NonSerialized] public Vector3 originalScale = Vector3.one;

        [Header("Cached Components for Zero-Allocation Culling")]
        [SerializeField] private Renderer[] m_Renderers;
        [SerializeField] private TrailRenderer[] m_Trails;
        [SerializeField] private Light[] m_Lights;

        // Runtime state flags
        private bool m_IsMeshVisible = true;
        private bool m_AreTrailsActive = true;
        private AudioSource m_AttachedAudioSource;

        public AudioSource AttachedAudioSource
        {
            get => m_AttachedAudioSource;
            set => m_AttachedAudioSource = value;
        }

        public bool IsMeshVisible => m_IsMeshVisible;
        public bool AreTrailsActive => m_AreTrailsActive;
        public float LastDistToListener => lastDistToListener;

        /// <summary>
        /// Assigns a random Synty colorway palette material to the vehicle's body mesh.
        /// </summary>
        public void ApplyMaterialPalette(Material paletteMat)
        {
            if (paletteMat == null) return;
            CacheComponents();

            if (m_Renderers != null)
            {
                for (int i = 0; i < m_Renderers.Length; i++)
                {
                    Renderer rend = m_Renderers[i];
                    if (rend == null) continue;
                    string rName = rend.name.ToLower();
                    // Don't replace underglow, glass, or light materials
                    if (rName.Contains("underglow") || rName.Contains("glass") || rName.Contains("trail")) continue;

                    Material[] mats = rend.sharedMaterials;
                    bool modified = false;
                    for (int m = 0; m < mats.Length; m++)
                    {
                        if (mats[m] != null && mats[m].name.StartsWith("PolygonCyberCity"))
                        {
                            mats[m] = paletteMat;
                            modified = true;
                        }
                    }
                    if (modified)
                    {
                        rend.sharedMaterials = mats;
                    }
                }
            }
        }

        /// <summary>
        /// Applies custom neon color tint to underglow quads, trails, and lights.
        /// </summary>
        public void ApplyCustomNeonColor(Color neonColor)
        {
            CacheComponents();

            // Tint underglow and materials
            MaterialPropertyBlock propBlock = new MaterialPropertyBlock();
            propBlock.SetColor("_BaseColor", neonColor);
            propBlock.SetColor("_EmissionColor", neonColor * 3.5f);

            if (m_Renderers != null)
            {
                for (int i = 0; i < m_Renderers.Length; i++)
                {
                    if (m_Renderers[i] != null && m_Renderers[i].name.Contains("Underglow"))
                    {
                        m_Renderers[i].SetPropertyBlock(propBlock);
                    }
                }
            }

            // Tint trails
            if (m_Trails != null)
            {
                for (int i = 0; i < m_Trails.Length; i++)
                {
                    if (m_Trails[i] != null)
                    {
                        Gradient grad = new Gradient();
                        grad.SetKeys(
                            new GradientColorKey[] {
                                new GradientColorKey(neonColor, 0f),
                                new GradientColorKey(neonColor * 0.5f, 0.7f),
                                new GradientColorKey(Color.black, 1f)
                            },
                            new GradientAlphaKey[] {
                                new GradientAlphaKey(0.85f, 0f),
                                new GradientAlphaKey(0.4f, 0.5f),
                                new GradientAlphaKey(0f, 1f)
                            }
                        );
                        m_Trails[i].colorGradient = grad;
                    }
                }
            }

            // Tint lights
            if (m_Lights != null)
            {
                for (int i = 0; i < m_Lights.Length; i++)
                {
                    if (m_Lights[i] != null)
                    {
                        m_Lights[i].color = neonColor;
                    }
                }
            }
        }

        private void Awake()
        {
            CacheComponents();
            if (transform.localScale.sqrMagnitude > 0.001f)
            {
                originalScale = transform.localScale;
            }
        }

        public void CacheComponents()
        {
            if (m_Renderers == null || m_Renderers.Length == 0)
                m_Renderers = GetComponentsInChildren<Renderer>(true);

            if (m_Trails == null || m_Trails.Length == 0)
                m_Trails = GetComponentsInChildren<TrailRenderer>(true);

            if (m_Lights == null || m_Lights.Length == 0)
                m_Lights = GetComponentsInChildren<Light>(true);

            if (originalScale == Vector3.zero && transform.localScale.sqrMagnitude > 0.001f)
                originalScale = transform.localScale;
        }

        /// <summary>
        /// Spawns this car at the start of the specified linear lane.
        /// </summary>
        public void SpawnAtStart(AirLane lane, int laneIdx, float startDistance = 0f)
        {
            CacheComponents();
            currentLane = lane;
            laneIndex = laneIdx;
            distanceAlongLane = startDistance;
            isCarActive = true;
            whooshCooldown = 0f;
            isApproachingListener = false;

            // Clear and enable trail renderers
            if (m_Trails != null)
            {
                for (int i = 0; i < m_Trails.Length; i++)
                {
                    if (m_Trails[i] != null)
                    {
                        m_Trails[i].Clear();
                        m_Trails[i].emitting = true;
                    }
                }
            }

            SetMeshVisibility(true);
            SetTrailsActive(true);

            // If starting at distance 0, scale begins small for seamless entrance
            if (startDistance <= 0.01f)
            {
                transform.localScale = originalScale * 0.05f;
            }
            else
            {
                transform.localScale = originalScale;
            }
        }

        /// <summary>
        /// Cleanly despawns vehicle when reaching the end of the line.
        /// Detaches audio, stops trails, hides mesh, and returns to idle pool.
        /// </summary>
        public void Despawn()
        {
            isCarActive = false;
            currentLane = null;
            distanceAlongLane = 0f;

            if (m_AttachedAudioSource != null)
            {
                m_AttachedAudioSource.Stop();
                m_AttachedAudioSource = null;
            }

            if (m_Trails != null)
            {
                for (int i = 0; i < m_Trails.Length; i++)
                {
                    if (m_Trails[i] != null)
                    {
                        m_Trails[i].emitting = false;
                        m_Trails[i].Clear();
                    }
                }
            }

            SetMeshVisibility(false);
            SetTrailsActive(false);
            transform.localScale = originalScale;
        }

        /// <summary>
        /// Smoothly eases vehicle scale in at start of line and out at end of line.
        /// Zero GC allocation per-frame.
        /// </summary>
        public void UpdateSpawnDespawnVisuals(float spawnDist, float despawnDist, float totalLength)
        {
            if (!isCarActive) return;

            float t = 1.0f;
            if (spawnDist > 0.01f && distanceAlongLane < spawnDist)
            {
                t = Mathf.Clamp01(distanceAlongLane / spawnDist);
            }
            else if (despawnDist > 0.01f && distanceAlongLane > (totalLength - despawnDist))
            {
                t = Mathf.Clamp01((totalLength - distanceAlongLane) / despawnDist);
            }

            float scaleFactor = Mathf.SmoothStep(0.0f, 1.0f, t);
            if (scaleFactor < 0.005f) scaleFactor = 0.001f;
            transform.localScale = originalScale * scaleFactor;
        }

        /// <summary>
        /// Toggles visual renderers without allocating memory or destroying state.
        /// </summary>
        public void SetMeshVisibility(bool visible)
        {
            if (m_IsMeshVisible == visible) return;
            m_IsMeshVisible = visible;

            if (m_Renderers != null)
            {
                for (int i = 0; i < m_Renderers.Length; i++)
                {
                    if (m_Renderers[i] != null)
                        m_Renderers[i].enabled = visible;
                }
            }

            if (m_Lights != null)
            {
                for (int i = 0; i < m_Lights.Length; i++)
                {
                    if (m_Lights[i] != null)
                        m_Lights[i].enabled = visible;
                }
            }
        }

        /// <summary>
        /// Enables/disables engine trail emitters (culled when far from camera).
        /// </summary>
        public void SetTrailsActive(bool active)
        {
            if (m_AreTrailsActive == active) return;
            m_AreTrailsActive = active;

            if (m_Trails != null)
            {
                for (int i = 0; i < m_Trails.Length; i++)
                {
                    if (m_Trails[i] != null)
                    {
                        m_Trails[i].emitting = active;
                    }
                }
            }
        }

        /// <summary>
        /// Direct kinematic Transform placement.
        /// </summary>
        public void SetPose(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
        }
    }
}
