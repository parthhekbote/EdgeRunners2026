using UnityEngine;
using UnityEngine.AI;
using PolygonCyberCity.NPC.Animation;

namespace PolygonCyberCity.NPC.Spawner
{
    /// <summary>
    /// Dynamic Vehicle & Traffic Avoidance Sensor for Pedestrians.
    /// Detects approaching hover cars, ground vehicles, and high-speed hazards:
    /// 1. Forward cone & spherecast sensor (6m lookahead).
    /// 2. Immediate emergency yield: halts agent locomotion to prevent clipping.
    /// 3. Lateral sidestep / curb evasion: steps away from vehicle vector.
    /// 4. Startled vocal reaction & evasive animation dispatch.
    /// 5. Smooth resumption once vehicle clears the hazard zone.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(CyberNPCController))]
    public class CyberNPCVehicleAvoidance : MonoBehaviour
    {
        [Header("Detection Parameters")]
        public float lookAheadDistance = 6.5f;
        public float detectionRadius = 1.6f;
        public float minEvadeCooldown = 3.0f;
        public LayerMask vehicleLayerMask = ~0;

        private NavMeshAgent _agent;
        private CyberNPCController _controller;
        private Animator _animator;
        private CyberNPCAudioChatter _audioChatter;

        private bool _isEvading = false;
        private float _evadeTimer = 0f;
        private float _lastEvadeTime = -10f;
        private Vector3 _evadeOriginDestination;

        public bool IsEvading => _isEvading;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _controller = GetComponent<CyberNPCController>();
            _animator = GetComponentInChildren<Animator>();
            _audioChatter = GetComponent<CyberNPCAudioChatter>();
        }

        private void Update()
        {
            if (_controller == null || _controller.IsDead || _agent == null || !_agent.isOnNavMesh)
                return;

            if (_isEvading)
            {
                HandleActiveEvasion();
            }
            else
            {
                ScanForApproachingVehicles();
            }
        }

        private void ScanForApproachingVehicles()
        {
            if (Time.time - _lastEvadeTime < minEvadeCooldown) return;

            Vector3 origin = transform.position + Vector3.up * 0.8f;
            Vector3 forward = transform.forward;

            // 1. Forward SphereCast check
            Ray ray = new Ray(origin, forward);
            if (Physics.SphereCast(ray, detectionRadius, out RaycastHit hit, lookAheadDistance, vehicleLayerMask, QueryTriggerInteraction.Ignore))
            {
                if (IsVehicleOrHazard(hit.collider.gameObject, out Vector3 vehicleVel))
                {
                    TriggerVehicleEvasion(hit.point, vehicleVel);
                    return;
                }
            }

            // 2. Wide OverlapSphere proximity check for cross-lane traffic
            Collider[] overlaps = Physics.OverlapSphere(origin, 4.0f, vehicleLayerMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < overlaps.Length; i++)
            {
                if (overlaps[i].gameObject == gameObject) continue;
                if (IsVehicleOrHazard(overlaps[i].gameObject, out Vector3 vehicleVel))
                {
                    // Check if vehicle is moving toward the NPC
                    Vector3 toNpc = (transform.position - overlaps[i].transform.position).normalized;
                    if (Vector3.Dot(vehicleVel.normalized, toNpc) > 0.35f)
                    {
                        TriggerVehicleEvasion(overlaps[i].transform.position, vehicleVel);
                        break;
                    }
                }
            }
        }

        private bool IsVehicleOrHazard(GameObject go, out Vector3 velocity)
        {
            velocity = Vector3.zero;

            // Check HoverCar (AirTrafficSystem)
            var hoverCar = go.GetComponentInParent<CyberpunkDev.Traffic.HoverCar>();
            if (hoverCar != null)
            {
                velocity = hoverCar.transform.forward * Mathf.Max(hoverCar.currentSpeed, 10f);
                return true;
            }

            // Check Rigidbody with high speed
            var rb = go.GetComponentInParent<Rigidbody>();
            if (rb != null && rb.linearVelocity.magnitude > 2.5f)
            {
                velocity = rb.linearVelocity;
                return true;
            }

            // Check name convention for Vehicles
            string n = go.name.ToLower();
            if (n.Contains("car") || n.Contains("vehicle") || n.Contains("taxi") || n.Contains("truck") || n.Contains("van"))
            {
                velocity = go.transform.forward * 8f;
                return true;
            }

            return false;
        }

        public void TriggerVehicleEvasion(Vector3 vehiclePos, Vector3 vehicleVel)
        {
            if (_agent == null) _agent = GetComponent<NavMeshAgent>();
            if (_controller == null) _controller = GetComponent<CyberNPCController>();
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            if (_audioChatter == null) _audioChatter = GetComponent<CyberNPCAudioChatter>();

            _isEvading = true;
            _evadeTimer = 0f;
            _lastEvadeTime = Time.time;
            _evadeOriginDestination = (_agent != null && _agent.isOnNavMesh && _agent.hasPath) ? _agent.destination : transform.position;

            // 1. Immediately halt forward march if on navmesh
            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                _agent.velocity = Vector3.zero;
            }

            // 2. Compute lateral dodge vector away from vehicle velocity path
            Vector3 vehicleHeading = (vehicleVel != Vector3.zero) ? vehicleVel.normalized : (transform.position - vehiclePos).normalized;
            Vector3 lateralDir = Vector3.Cross(vehicleHeading, Vector3.up).normalized;

            // Pick direction away from vehicle center
            if (Vector3.Dot(lateralDir, transform.position - vehiclePos) < 0f)
            {
                lateralDir = -lateralDir;
            }

            Vector3 evadeTarget = transform.position + lateralDir * 1.6f;
            if (_agent != null && _agent.isOnNavMesh && NavMesh.SamplePosition(evadeTarget, out NavMeshHit navHit, 2.0f, NavMesh.AllAreas))
            {
                _agent.isStopped = false;
                _agent.speed = _controller != null ? _controller.runSpeed : 4.0f;
                _agent.SetDestination(navHit.position);
            }



            if (_animator != null && _animator.isActiveAndEnabled)
            {
                _animator.SetTrigger(NPCAnimatorHashes.PanicLookBack);
            }
        }

        private void HandleActiveEvasion()
        {
            _evadeTimer += Time.deltaTime;

            // Wait 1.4s for vehicle to clear before resuming normal routine
            if (_evadeTimer >= 1.4f)
            {
                _isEvading = false;
                _evadeTimer = 0f;

                if (_agent != null && _agent.isOnNavMesh)
                {
                    _agent.isStopped = false;
                    _agent.speed = _controller != null ? _controller.walkSpeed : 2.0f;
                    if (_evadeOriginDestination != Vector3.zero)
                    {
                        _agent.SetDestination(_evadeOriginDestination);
                    }
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = _isEvading ? Color.red : Color.cyan;
            Vector3 origin = transform.position + Vector3.up * 0.8f;
            Gizmos.DrawWireSphere(origin + transform.forward * lookAheadDistance * 0.5f, detectionRadius);
            Gizmos.DrawLine(origin, origin + transform.forward * lookAheadDistance);
        }
    }
}
