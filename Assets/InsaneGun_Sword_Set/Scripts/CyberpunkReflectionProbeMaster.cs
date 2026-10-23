using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace InsaneGun.Combat
{
    /// <summary>
    /// Real-time Box-Projected Reflection Probe Engine for Cyberpunk Environments.
    /// Provides mirror-like reflections of neon billboards, laser barricades,
    /// and combatants on wet asphalt ground and metallic surfaces.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("InsaneGun/Cyberpunk Reflection Probe Master")]
    public class CyberpunkReflectionProbeMaster : MonoBehaviour
    {
        public static CyberpunkReflectionProbeMaster Instance { get; private set; }

        [Header("Probe Configuration")]
        public bool enableRealtimeReflections = true;
        public Vector3 probeSize = new Vector3(52f, 24f, 52f);
        public Vector3 probeCenterOffset = new Vector3(0f, 3.5f, 0f);
        [Range(128, 1024)] public int probeResolution = 512;
        public ReflectionProbeTimeSlicingMode timeSlicing = ReflectionProbeTimeSlicingMode.IndividualFaces;

        [Header("Probe Settings")]
        public LayerMask cullingMask = ~0; // Everything except UI
        public float shadowDistance = 45f;

        private ReflectionProbe _probe;

        private void Awake()
        {
            Instance = this;
            SetupReflectionProbe();
        }

        private void Start()
        {
            SetupReflectionProbe();
        }

        private void OnEnable()
        {
            SetupReflectionProbe();
        }

        [ContextMenu("Refresh Reflection Probe")]
        public void SetupReflectionProbe()
        {
            if (!enableRealtimeReflections) return;

            Transform child = transform.Find("Realtime_Cyberpunk_ReflectionProbe");
            GameObject probeGo = child != null ? child.gameObject : null;

            if (probeGo == null)
            {
                probeGo = new GameObject("Realtime_Cyberpunk_ReflectionProbe");
                probeGo.transform.SetParent(transform, false);
                probeGo.transform.localPosition = Vector3.up * 1.5f;
            }

            _probe = probeGo.GetComponent<ReflectionProbe>();
            if (_probe == null) _probe = probeGo.AddComponent<ReflectionProbe>();

            // Configure Box Projection for accurate rectangular arena reflections
            _probe.mode = ReflectionProbeMode.Realtime;
            _probe.refreshMode = ReflectionProbeRefreshMode.EveryFrame;
            _probe.timeSlicingMode = timeSlicing;
            _probe.boxProjection = true;
            _probe.size = probeSize;
            _probe.center = probeCenterOffset;
            _probe.resolution = probeResolution;
            _probe.shadowDistance = shadowDistance;
            _probe.importance = 1;
            _probe.blendDistance = 2.0f;
            _probe.clearFlags = ReflectionProbeClearFlags.Skybox;
            _probe.intensity = 1.15f;
            _probe.hdr = true;

            // Cull UI and Particle effects from probe render pass for performance
            int uiLayer = LayerMask.NameToLayer("UI");
            LayerMask mask = ~0;
            if (uiLayer >= 0) mask &= ~(1 << uiLayer);
            _probe.cullingMask = mask;

            _probe.RenderProbe();
        }

        public void RequestImmediateUpdate()
        {
            if (_probe != null) _probe.RenderProbe();
        }
    }
}
