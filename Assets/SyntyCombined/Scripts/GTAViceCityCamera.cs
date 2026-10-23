using UnityEngine;

namespace SyntyCombined
{
    /// <summary>
    /// GTA Vice City-Style Third-Person Follow & Orbit Camera Controller.
    /// 
    /// FEATURES & COLLISION RESOLUTION:
    /// 1. Pivot-based SphereCast: Casts from player head/chest pivot toward desired position using Physics.SphereCast.
    /// 2. Asymmetric Distance Smoothing: Fast pull-in (0.02s) when occluded to prevent geometry clipping, 
    ///    smooth push-out (0.25s) when clear to prevent camera popping.
    /// 3. Tight-Space Height & Pitch Bump: Automatically raises camera height and pitches downward in tight corners/walls.
    /// 4. Player Mesh Fading: Automatically hides/fades player mesh when distance falls below threshold.
    /// 5. Auto-recenter behind movement: Drifts back behind player velocity after idle input delay.
    /// 6. Hysteresis Protection: Prevents rapid jitter when sliding along walls via decoupled distance damping.
    /// </summary>
    [AddComponentMenu("Camera/GTA Vice City Follow Camera")]
    public class GTAViceCityCamera : MonoBehaviour
    {
        [Header("Target & Pivot Settings")]
        [Tooltip("The player transform to follow.")]
        public Transform target;

        [Tooltip("Offset relative to player position where camera aims (head/chest level).")]
        public Vector3 pivotOffset = new Vector3(0f, 1.55f, 0f);

        [Header("Distance & Framing")]
        [Tooltip("Standard follow distance from player pivot in open space.")]
        public float defaultDistance = 4.2f;

        [Tooltip("Minimum allowable distance before hiding player mesh.")]
        public float minDistance = 0.5f;

        [Tooltip("Default height offset of camera above target pivot.")]
        public float defaultHeight = 0.45f;

        [Tooltip("Default downward pitch angle in degrees (positive looks down).")]
        public float defaultPitch = 12f;

        [Tooltip("Minimum and maximum pitch limits (X-axis rotation).")]
        public float minPitch = -25f;
        public float maxPitch = 70f;

        [Header("Orbit & Manual Control")]
        [Tooltip("Mouse/stick sensitivity for yaw (horizontal orbit).")]
        public float yawSensitivity = 120f;

        [Tooltip("Mouse/stick sensitivity for pitch (vertical orbit).")]
        public float pitchSensitivity = 100f;

        [Tooltip("Speed at which camera auto-realigns behind moving player.")]
        public float autoFollowSpeed = 3.5f;

        [Tooltip("Delay in seconds of idle input before auto-realigning behind player.")]
        public float recenterDelay = 1.8f;

        [Tooltip("Minimum player velocity threshold to trigger auto-alignment.")]
        public float autoFollowVelThreshold = 0.4f;

        [Header("Collision System")]
        [Tooltip("Layer mask containing walls, terrain, obstacles, and environment geometry.")]
        public LayerMask collisionLayers = ~0;

        [Tooltip("Radius of the sphere cast used for detecting geometry collisions.")]
        public float sphereCastRadius = 0.25f;

        [Tooltip("Minimum buffer distance kept between camera sphere and hit wall surfaces.")]
        public float surfaceOffset = 0.15f;

        [Header("Asymmetric Distance Smoothing & Hysteresis")]
        [Tooltip("Smooth time when pulling IN (rapid reaction to avoid occlusion).")]
        public float pullInSmoothTime = 0.02f;

        [Tooltip("Smooth time when pushing OUT (gradual return to prevent popping).")]
        public float pushOutSmoothTime = 0.25f;

        [Tooltip("Smooth time for camera position damping.")]
        public float positionDampTime = 0.03f;

        [Header("Tight-Space Lift & Pitch Compensation")]
        [Tooltip("Extra height added as camera gets forced closer to player in tight spaces.")]
        public float tightSpaceMaxHeightBump = 0.75f;

        [Tooltip("Extra downward pitch added in tight spaces to keep player visible from above.")]
        public float tightSpaceExtraPitch = 14f;

        [Header("Player Fade / Occlusion Protection")]
        [Tooltip("Distance threshold below which player renderers fade out.")]
        public float fadeDistanceThreshold = 1.1f;

        [Tooltip("List of player Renderers to fade out when camera is ultra close.")]
        public Renderer[] playerRenderers;

        [Header("Debug Visualization")]
        public bool showDebugGizmos = true;
        public Color gizmoTargetColor = Color.green;
        public Color gizmoHitColor = Color.red;
        public Color gizmoRayColor = Color.cyan;

        // Internal State
        private float _currentYaw;
        private float _currentPitch;
        private float _currentDistance;
        private float _distanceVelocity;
        private Vector3 _currentPosition;
        private Vector3 _positionVelocity;
        private float _lastInputTime;

        private void Start()
        {
            if (target == null)
            {
                var player = GameObject.FindWithTag("Player");
                if (player != null) target = player.transform;
            }

            if (target != null)
            {
                _currentYaw = target.eulerAngles.y;
            }

            _currentPitch = defaultPitch;
            _currentDistance = defaultDistance;
            _currentPosition = transform.position;

            // Auto-gather player renderers if not explicitly assigned
            if ((playerRenderers == null || playerRenderers.Length == 0) && target != null)
            {
                playerRenderers = target.GetComponentsInChildren<Renderer>();
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;

            ProcessInput();
            UpdateCameraTransform();
            UpdatePlayerFade();
        }

        private void ProcessInput()
        {
            float inputX = Input.GetAxis("Mouse X");
            float inputY = Input.GetAxis("Mouse Y");

            float moveYaw = inputX * yawSensitivity * Time.deltaTime;
            float movePitch = inputY * pitchSensitivity * Time.deltaTime;

            bool hasManualInput = Mathf.Abs(inputX) > 0.001f || Mathf.Abs(inputY) > 0.001f;

            if (hasManualInput)
            {
                _currentYaw += moveYaw;
                _currentPitch -= movePitch;
                _currentPitch = Mathf.Clamp(_currentPitch, minPitch, maxPitch);
                _lastInputTime = Time.time;
            }
            else
            {
                // Auto-realign behind player velocity direction after idle delay
                if (Time.time - _lastInputTime > recenterDelay)
                {
                    Vector3 moveDir = target.forward;
                    var agent = target.GetComponent<UnityEngine.AI.NavMeshAgent>();
                    if (agent != null && agent.enabled && agent.velocity.sqrMagnitude > autoFollowVelThreshold * autoFollowVelThreshold)
                    {
                        moveDir = agent.velocity.normalized;
                    }
                    else if (target.GetComponent<Rigidbody>() != null && target.GetComponent<Rigidbody>().linearVelocity.sqrMagnitude > autoFollowVelThreshold * autoFollowVelThreshold)
                    {
                        moveDir = target.GetComponent<Rigidbody>().linearVelocity.normalized;
                    }

                    moveDir.y = 0f;
                    if (moveDir.sqrMagnitude > 0.01f)
                    {
                        float targetYaw = Quaternion.LookRotation(moveDir).eulerAngles.y;
                        _currentYaw = Mathf.LerpAngle(_currentYaw, targetYaw, Time.deltaTime * autoFollowSpeed);
                        _currentPitch = Mathf.Lerp(_currentPitch, defaultPitch, Time.deltaTime * autoFollowSpeed * 0.5f);
                    }
                }
            }
        }

        private void UpdateCameraTransform()
        {
            // 1. Target pivot position in world space
            Vector3 pivotPosition = target.position + target.rotation * pivotOffset;

            // 2. Unoccluded desired rotation and direction
            Quaternion targetRotation = Quaternion.Euler(_currentPitch, _currentYaw, 0f);
            Vector3 desiredDirection = targetRotation * -Vector3.forward;
            Vector3 desiredPosition = pivotPosition + desiredDirection * defaultDistance + Vector3.up * defaultHeight;

            // 3. Collision resolution via SphereCast from pivot to desired position
            Vector3 rayDirection = (desiredPosition - pivotPosition).normalized;
            float maxRayDistance = Vector3.Distance(pivotPosition, desiredPosition);

            float targetDistance = defaultDistance;
            float heightBump = 0f;
            float pitchBump = 0f;

            if (Physics.SphereCast(pivotPosition, sphereCastRadius, rayDirection, out RaycastHit hit, maxRayDistance, collisionLayers, QueryTriggerInteraction.Ignore))
            {
                // Calculate hit distance with surface buffer offset
                float hitDistance = Mathf.Max(hit.distance - surfaceOffset, minDistance);
                targetDistance = Mathf.Min(hitDistance, defaultDistance);

                // Tight space height and pitch compensation
                float tightFactor = 1f - Mathf.Clamp01(targetDistance / defaultDistance);
                heightBump = tightFactor * tightSpaceMaxHeightBump;
                pitchBump = tightFactor * tightSpaceExtraPitch;
            }

            // 4. Asymmetric Distance Dampening:
            // Rapid pull-in (0.02s) when occluded, gradual push-out (0.25s) when clear
            float smoothTime = targetDistance < _currentDistance ? pullInSmoothTime : pushOutSmoothTime;
            _currentDistance = Mathf.SmoothDamp(_currentDistance, targetDistance, ref _distanceVelocity, smoothTime);

            // 5. Compute final camera position & rotation with tight-space compensation
            Quaternion finalRotation = Quaternion.Euler(_currentPitch + pitchBump, _currentYaw, 0f);
            Vector3 finalDirection = finalRotation * -Vector3.forward;
            Vector3 finalTargetPosition = pivotPosition + finalDirection * _currentDistance + Vector3.up * (defaultHeight + heightBump);

            // 6. Frame-rate independent position smoothing
            _currentPosition = Vector3.SmoothDamp(_currentPosition, finalTargetPosition, ref _positionVelocity, positionDampTime);

            transform.position = _currentPosition;
            transform.rotation = Quaternion.LookRotation((pivotPosition + Vector3.up * heightBump) - transform.position);
        }

        private void UpdatePlayerFade()
        {
            if (playerRenderers == null || playerRenderers.Length == 0) return;

            float alpha = Mathf.Clamp01((_currentDistance - minDistance) / (fadeDistanceThreshold - minDistance));

            foreach (var r in playerRenderers)
            {
                if (r == null) continue;
                r.enabled = alpha > 0.05f;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!showDebugGizmos || target == null) return;

            Vector3 pivotPosition = target.position + target.rotation * pivotOffset;
            Quaternion targetRotation = Quaternion.Euler(_currentPitch, _currentYaw, 0f);
            Vector3 desiredPosition = pivotPosition + (targetRotation * -Vector3.forward) * defaultDistance + Vector3.up * defaultHeight;

            // Pivot Point
            Gizmos.color = gizmoTargetColor;
            Gizmos.DrawWireSphere(pivotPosition, 0.15f);

            // Cast Ray & Desired Position
            Gizmos.color = gizmoRayColor;
            Gizmos.DrawLine(pivotPosition, desiredPosition);
            Gizmos.DrawWireSphere(desiredPosition, sphereCastRadius);

            // Resolved Camera Position
            Gizmos.color = gizmoHitColor;
            Gizmos.DrawWireSphere(transform.position, sphereCastRadius + 0.05f);
        }
    }
}
