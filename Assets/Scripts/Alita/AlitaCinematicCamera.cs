using System.Collections;
using UnityEngine;

namespace Alita
{
    /// <summary>
    /// Aesthetic cinematic camera for Alita in the Scrapyard.
    /// Features:
    /// - Continuous smooth low-angle orbit around broken Alita
    /// - Raycast / SphereCast obstruction avoidance ensuring no debris or scrap piles block the view
    /// - Subtle vertical bobbing (organic cinematic motion)
    /// - Reactive mouse parallax for interactive UI depth
    /// - Smooth zoom transition for the Awakening / Cutscene sequence
    /// </summary>
    public class AlitaCinematicCamera : MonoBehaviour
    {
        [Header("Target & Framing")]
        public Transform target;
        public Vector3 lookOffset = new Vector3(0.0f, 0.35f, 0.0f);
        public Vector3 screenFramingOffset = new Vector3(0.12f, 0f, 0f); // Subtle framing offset so UI sits beside Alita

        [Header("Orbit Dynamics")]
        public float baseDistance = 1.9f;
        public float distance { get => baseDistance; set => baseDistance = value; }
        public float examineDistance = 1.0f;
        public float minAllowedDistance = 0.85f;
        public float orbitSpeed = 5.5f;
        public float basePitch = 14f;
        public float pitchAmplitude = 4f;
        public float pitchFrequency = 0.25f;

        [Header("Organic Vertical Bobbing")]
        public float verticalBobAmplitude = 0.06f;
        public float verticalBobFrequency = 0.35f;

        [Header("Obstruction & Collision Avoidance")]
        public bool enableObstructionAvoidance = true;
        public float obstacleSphereRadius = 0.18f;
        public float collisionSafetyBuffer = 0.22f;
        public LayerMask obstacleLayers = ~0; // Test against all solid scrap / environment

        [Header("Reactive Mouse Parallax")]
        public bool enableMouseParallax = true;
        public float mouseParallaxStrength = 3.5f;

        [Header("Cinematic State")]
        public bool isAwakening = false;
        public float awakeningTargetDistance = 0.9f;

        private float _currentYaw = -25f;
        private float _currentPitch;
        private float _currentDistance;
        private Vector3 _currentVelocity;
        private Vector3 _currentLookVelocity;
        private Vector3 _lastFocusPoint;
        private AlitaBrokenCyborg _alita;

        private void Start()
        {
            _currentDistance = baseDistance;
            _currentPitch = basePitch;

            if (target != null)
            {
                _alita = target.GetComponent<AlitaBrokenCyborg>();
                _lastFocusPoint = target.position + lookOffset;
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;

            // 1. Orbit Yaw progression
            _currentYaw += Time.deltaTime * orbitSpeed;

            // 2. Subtle organic breathing pitch & height modulation
            float pitchWave = Mathf.Sin(Time.time * pitchFrequency * Mathf.PI * 2f) * pitchAmplitude;
            float targetPitch = basePitch + pitchWave;

            // 3. Interactive Mouse Parallax (adds reactive depth behind start menu)
            float mouseOffsetX = 0f;
            float mouseOffsetY = 0f;
            if (enableMouseParallax && Screen.width > 0 && Screen.height > 0)
            {
                float normX = (Input.mousePosition.x / Screen.width) - 0.5f;
                float normY = (Input.mousePosition.y / Screen.height) - 0.5f;
                mouseOffsetX = normX * mouseParallaxStrength;
                mouseOffsetY = normY * (mouseParallaxStrength * 0.6f);
            }

            float finalYaw = _currentYaw + mouseOffsetX;
            _currentPitch = Mathf.Lerp(_currentPitch, targetPitch - mouseOffsetY, Time.deltaTime * 3.0f);

            // 4. Focus target position with vertical bob
            Vector3 focus = target.position + lookOffset;
            float verticalBob = Mathf.Sin(Time.time * verticalBobFrequency * Mathf.PI * 2f) * verticalBobAmplitude;
            focus.y += verticalBob;

            // 5. Calculate ideal unconstrained position
            float targetDist = isAwakening ? awakeningTargetDistance : baseDistance;
            Quaternion orbitRotation = Quaternion.Euler(_currentPitch, finalYaw, 0f);
            Vector3 direction = orbitRotation * Vector3.back;
            Vector3 idealPos = focus + (direction * targetDist);

            // 6. Raycast / SphereCast Obstruction Avoidance
            // Checks if any scrap piles or environment colliders block the line of sight to Alita
            float safeDistance = targetDist;
            if (enableObstructionAvoidance)
            {
                Vector3 rayOrigin = focus;
                Vector3 rayDir = (idealPos - focus).normalized;
                float checkDistance = targetDist;

                RaycastHit hit;
                // Exclude target and its children from obstruction hits
                if (Physics.SphereCast(rayOrigin, obstacleSphereRadius, rayDir, out hit, checkDistance, obstacleLayers, QueryTriggerInteraction.Ignore))
                {
                    if (hit.transform != target && !hit.transform.IsChildOf(target))
                    {
                        // Bring camera in front of the occluding scrap
                        safeDistance = Mathf.Clamp(hit.distance - collisionSafetyBuffer, minAllowedDistance, targetDist);
                    }
                }
            }

            _currentDistance = Mathf.Lerp(_currentDistance, safeDistance, Time.deltaTime * 8.0f);
            Vector3 finalPos = focus + (direction * _currentDistance);

            // Apply subtle lateral framing offset (keeps Alita visible when menu is on screen)
            Vector3 rightOffset = orbitRotation * Vector3.right * screenFramingOffset.x;
            Vector3 upOffset = Vector3.up * screenFramingOffset.y;
            finalPos += rightOffset + upOffset;

            // 7. Smoothly damp camera transform
            transform.position = Vector3.SmoothDamp(transform.position, finalPos, ref _currentVelocity, 0.08f);

            // Smooth LookAt focus point
            _lastFocusPoint = Vector3.SmoothDamp(_lastFocusPoint, focus, ref _currentLookVelocity, 0.06f);
            transform.LookAt(_lastFocusPoint);
        }

        /// <summary>
        /// Smoothly swoops the camera closer to Alita when the player clicks Start / Initialize Awakening.
        /// </summary>
        public void TriggerAwakeningCinematic(float targetDist = 0.82f, float duration = 2.0f)
        {
            isAwakening = true;
            awakeningTargetDistance = targetDist;
            StartCoroutine(AwakeningFocusRoutine(duration));
        }

        private IEnumerator AwakeningFocusRoutine(float duration)
        {
            float elapsed = 0f;
            float startPitch = basePitch;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                basePitch = Mathf.Lerp(startPitch, 6.0f, elapsed / duration); // Lower angle to dramatic heroic look
                yield return null;
            }
        }
    }
}

