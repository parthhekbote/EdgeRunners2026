using System.Collections.Generic;
using UnityEngine;

namespace InsaneGun.Combat
{
    /// <summary>
    /// Global coordinator and performance optimizer for Euphoria ragdolls in CyberCity.
    /// Manages simulation budgets, auto-freezes distant settled ragdolls,
    /// throttles ground impact audio, and handles radial physics wake-ups.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class EuphoriaRagdollManager : MonoBehaviour
    {
        private static EuphoriaRagdollManager _instance;
        public static EuphoriaRagdollManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var existing = FindAnyObjectByType<EuphoriaRagdollManager>();
                    if (existing != null)
                    {
                        _instance = existing;
                    }
                    else
                    {
                        var go = new GameObject("EuphoriaRagdollManager");
                        _instance = go.AddComponent<EuphoriaRagdollManager>();
                    }
                }
                return _instance;
            }
        }

        [Header("Global Ragdoll Budget")]
        [Tooltip("Maximum simultaneous actively-simulating ragdolls. Furthest ragdolls from player are settled first.")]
        [Range(2, 24)] public int maxActiveRagdolls = 8;

        [Tooltip("Target player or camera transform to calculate distance-based ragdoll priority.")]
        public Transform priorityTarget;

        [Header("Audio & Impact Throttling")]
        [Tooltip("Minimum time between global bone impact sounds to prevent audio channel clipping.")]
        public float globalImpactSoundCooldown = 0.12f;

        private static readonly List<EuphoriaRagdollController> _activeRagdolls = new List<EuphoriaRagdollController>(32);
        private static readonly List<EuphoriaRagdollController> _allRagdolls = new List<EuphoriaRagdollController>(64);
        private float _lastGlobalImpactSoundTime = -1f;

        public static int ActiveRagdollCount => _activeRagdolls.Count;
        public static int TotalRagdollCount => _allRagdolls.Count;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            FindPriorityTarget();
        }

        private void Start()
        {
            if (priorityTarget == null)
                FindPriorityTarget();
        }

        private void Update()
        {
            if (priorityTarget == null)
                FindPriorityTarget();

            EnforceSimulationBudget();
        }

        private void FindPriorityTarget()
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                priorityTarget = player.transform;
                return;
            }
            if (Camera.main != null)
            {
                priorityTarget = Camera.main.transform;
            }
        }

        public static void Register(EuphoriaRagdollController ragdoll)
        {
            if (ragdoll != null && !_allRagdolls.Contains(ragdoll))
            {
                _allRagdolls.Add(ragdoll);
            }
        }

        public static void Unregister(EuphoriaRagdollController ragdoll)
        {
            _allRagdolls.Remove(ragdoll);
            _activeRagdolls.Remove(ragdoll);
        }

        public static void NotifyRagdollActivated(EuphoriaRagdollController ragdoll)
        {
            if (ragdoll != null && !_activeRagdolls.Contains(ragdoll))
            {
                _activeRagdolls.Add(ragdoll);
                Instance?.EnforceSimulationBudget();
            }
        }

        public static void NotifyRagdollDeactivated(EuphoriaRagdollController ragdoll)
        {
            _activeRagdolls.Remove(ragdoll);
        }

        /// <summary>
        /// Enforces max active ragdoll limit. If active count exceeds budget,
        /// sorts by distance from player and forces the most distant ones to settle.
        /// </summary>
        public void EnforceSimulationBudget()
        {
            // Clean up nulls
            for (int i = _activeRagdolls.Count - 1; i >= 0; i--)
            {
                if (_activeRagdolls[i] == null || !_activeRagdolls[i].IsRagdoll)
                    _activeRagdolls.RemoveAt(i);
            }

            if (_activeRagdolls.Count <= maxActiveRagdolls) return;

            Vector3 refPos = priorityTarget != null ? priorityTarget.position : Vector3.zero;

            // Sort so furthest is at the beginning
            _activeRagdolls.Sort((a, b) =>
            {
                if (a == null || b == null) return 0;
                float distA = Vector3.SqrMagnitude(a.transform.position - refPos);
                float distB = Vector3.SqrMagnitude(b.transform.position - refPos);
                return distB.CompareTo(distA); // Descending (furthest first)
            });

            // Force freeze distant ones down to budget
            while (_activeRagdolls.Count > maxActiveRagdolls)
            {
                var candidate = _activeRagdolls[0];
                _activeRagdolls.RemoveAt(0);
                if (candidate != null && candidate.IsRagdoll && !candidate.IsSettled)
                {
                    candidate.ForceSettleAndFreeze();
                }
            }
        }

        /// <summary>
        /// Checks if an impact sound can play without audio clipping.
        /// </summary>
        public bool CanPlayImpactSound()
        {
            if (Time.time - _lastGlobalImpactSoundTime < globalImpactSoundCooldown)
                return false;

            _lastGlobalImpactSoundTime = Time.time;
            return true;
        }

        /// <summary>
        /// Wakes up any settled/frozen ragdolls within a blast radius and imparts physics impulse.
        /// </summary>
        public static void WakeRagdollsInRadius(Vector3 center, float radius, float force)
        {
            float sqrRadius = radius * radius;
            for (int i = 0; i < _allRagdolls.Count; i++)
            {
                var r = _allRagdolls[i];
                if (r == null || !r.IsRagdoll) continue;

                Vector3 bodyPos = r.RagdollPelvisPosition;
                float sqrDist = Vector3.SqrMagnitude(bodyPos - center);
                if (sqrDist <= sqrRadius)
                {
                    float dist = Mathf.Sqrt(sqrDist);
                    float falloff = 1f - Mathf.Clamp01(dist / radius);
                    Vector3 blastDir = dist > 0.01f ? (bodyPos - center).normalized : Vector3.up;
                    Vector3 impulse = (blastDir + Vector3.up * 0.4f).normalized * (force * falloff);
                    r.AddImpulse(bodyPos, impulse);
                }
            }
        }
    }
}
