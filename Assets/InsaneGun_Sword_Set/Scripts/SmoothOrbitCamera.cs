using System.Collections;
using UnityEngine;

namespace InsaneGun.Combat
{
    /// <summary>
    /// Runtime 3rd-person smooth orbit camera with mouse look, pitch/yaw limits,
    /// smooth damping, cursor lock management, trauma-based impact shake,
    /// directional impact kick, and micro hit-stop frame freeze.
    /// </summary>
    [AddComponentMenu("InsaneGun/Smooth Orbit Camera")]
    public class SmoothOrbitCamera : MonoBehaviour
    {
        public static SmoothOrbitCamera Instance { get; private set; }

        [Header("Target Follow")]
        public Transform target;
        public Vector3 targetOffset = new Vector3(0f, 1.35f, 0f);
        public Vector3 cameraOffset = new Vector3(0.48f, 0.08f, -2.7f);

        [Header("Shoulder Framing")]
        [Tooltip("Key to swap camera between right shoulder and left shoulder")]
        public KeyCode shoulderSwapKey = KeyCode.X;
        private float _targetShoulderSide = 1f; // 1 = right shoulder, -1 = left shoulder
        private float _currentShoulderSide = 1f;

        [Header("Orbit Speeds & Limits")]
        [Tooltip("When enabled, camera translation follows character position without exponential lag, keeping the character rock-solid and razor-sharp in viewport while maintaining smooth mouse rotation.")]
        public bool tightPositionFollow = true;
        public float mouseSensitivityX = 3.5f;
        public float mouseSensitivityY = 2.5f;
        public static float traumaMultiplier = 1.0f;
        public static bool invertY = false;
        public float smoothSpeed = 14f;
        public float minPitch = -30f;
        public float maxPitch = 70f;

        [Header("Zoom & Distance")]
        public float minDistance = 1.1f;
        public float minCollisionDistance = 0.55f;
        public float maxDistance = 7.0f;
        public float zoomSpeed = 3.5f;
        public float cameraRadius = 0.20f;
        public LayerMask obstacleLayers;

        [Header("Perspective (P Key Toggle)")]
        public bool isFirstPerson = false;
        public Vector3 firstPersonEyeOffset = new Vector3(0f, 1.62f, 0.18f);
        private float _saved3rdPersonDistance = 2.7f;
        private Renderer[] _playerRenderers;

        [Header("Combat Impact Shake")]
        [Tooltip("Trauma polynomial power (2 = quadratic falloff for punchy snap)")]
        public float traumaExponent = 2.0f;
        [Tooltip("Trauma loss per second")]
        public float traumaDecayRate = 2.6f;
        [Tooltip("Max position offset in meters at 1.0 trauma")]
        public float maxShakeTranslation = 0.35f;
        [Tooltip("Max rotational roll angle in degrees at 1.0 trauma")]
        public float maxShakeRoll = 3.8f;
        [Tooltip("Max rotational pitch angle in degrees at 1.0 trauma")]
        public float maxShakePitch = 2.8f;
        [Tooltip("Perlin noise frequency")]
        public float shakeFrequency = 35f;

        private float _yaw = 0f;
        private float _pitch = 15f;
        private float _currentDistance = 2.7f;
        private float _targetDistance = 2.7f;

        // Shake & Trauma state
        private float _trauma = 0f;
        private Vector3 _directionalKick = Vector3.zero;
        private Coroutine _hitStopCoroutine;

        private void Awake()
        {
            Instance = this;
            cursorLockOverride = false;

            Camera cam = GetComponent<Camera>();
            if (cam != null)
            {
                cam.nearClipPlane = 0.05f; // Ensures zero near-plane clipping on close-ups
                cam.farClipPlane = Mathf.Max(cam.farClipPlane, 1000f);
                if (cam.fieldOfView < 68f) cam.fieldOfView = 68f;
            }

            // Upgrade legacy head-centered offsets to over-the-shoulder framing
            if (Mathf.Abs(cameraOffset.x) < 0.05f || cameraOffset.y > 0.25f || cameraOffset.z < -4.0f || targetOffset.y > 1.40f)
            {
                targetOffset = new Vector3(0f, 1.35f, 0f);
                cameraOffset = new Vector3(0.48f, 0.08f, -2.7f);
            }

            // Ensure safe 3rd person zoom limits
            if (minDistance < 1.1f) minDistance = 1.1f;
            if (minCollisionDistance < 0.55f) minCollisionDistance = 0.55f;

            // Automatically configure obstacle layers (exclude Player, Enemy, TransparentFX, IgnoreRaycast, Water)
            if (obstacleLayers.value == 0)
            {
                int ignoreMask = (1 << 1) | (1 << 2) | (1 << 4); // TransparentFX, Ignore Raycast, Water
                int playerLayer = LayerMask.NameToLayer("Player");
                if (playerLayer >= 0) ignoreMask |= (1 << playerLayer);
                int enemyLayer = LayerMask.NameToLayer("Enemy");
                if (enemyLayer >= 0) ignoreMask |= (1 << enemyLayer);
                obstacleLayers = ~ignoreMask;
            }

            var uad = GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (uad == null) uad = gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            uad.renderPostProcessing = true;
            uad.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            uad.antialiasingQuality = UnityEngine.Rendering.Universal.AntialiasingQuality.High;
        }

        private void OnEnable()
        {
            cursorLockOverride = false;
        }

        private void Start()
        {
            _targetDistance = Mathf.Abs(cameraOffset.z) > 0.5f ? Mathf.Abs(cameraOffset.z) : 2.7f;
            _currentDistance = _targetDistance;

            if (target == null)
            {
                var p = GameObject.FindWithTag("Player");
                if (p != null) target = p.transform;
            }

            if (target != null)
            {
                Vector3 angles = transform.eulerAngles;
                _yaw = angles.y;
                float p = angles.x;
                if (p > 180f) p -= 360f;
                _pitch = Mathf.Clamp(p, minPitch, maxPitch);
                SnapToTarget();
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            HandleCursorLock();

            // [P] Key 1st / 3rd Person Toggle
            bool pPressed = false;
            try
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null && kb.pKey.wasPressedThisFrame) pPressed = true;
            }
            catch { }
            if (!pPressed)
            {
                try { if (Input.GetKeyDown(KeyCode.P)) pPressed = true; } catch { }
            }

            if (pPressed && !SyntyCombined.InGamePauseManager.IsPaused)
            {
                TogglePerspective();
            }

            // [X] Key Shoulder Swap (Left / Right Shoulder)
            bool xPressed = false;
            try
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null && kb.xKey.wasPressedThisFrame) xPressed = true;
            }
            catch { }
            if (!xPressed)
            {
                try { if (Input.GetKeyDown(shoulderSwapKey)) xPressed = true; } catch { }
            }

            if (xPressed && !SyntyCombined.InGamePauseManager.IsPaused)
            {
                _targetShoulderSide = -_targetShoulderSide;
            }
            _currentShoulderSide = Mathf.Lerp(_currentShoulderSide, _targetShoulderSide, Time.deltaTime * 10f);

            // Interactive Mouse Scroll Zoom (Zoom In/Out freely in 3rd person)
            if (!isFirstPerson)
            {
                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (Mathf.Abs(scroll) > 0.0001f)
                {
                    _targetDistance = Mathf.Clamp(_targetDistance - scroll * zoomSpeed * 3.5f, minDistance, maxDistance);
                }
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;

            // Lock-on Camera Tracking or Mouse Orbit Input
            var insaneHero = target != null ? target.GetComponent<InsaneGunSwordController>() : null;
            if (insaneHero != null && insaneHero.lockedTarget != null)
            {
                bool targetValid = false;
                try
                {
                    if (insaneHero.lockedTarget != null && insaneHero.lockedTarget.gameObject != null && insaneHero.lockedTarget.gameObject.activeInHierarchy)
                    {
                        var ins = insaneHero.lockedTarget.GetComponentInParent<InsaneGunSwordController>() ?? insaneHero.lockedTarget.GetComponent<InsaneGunSwordController>();
                        var npc = insaneHero.lockedTarget.GetComponentInParent<PolygonCyberCity.NPC.Spawner.CyberNPCController>() ?? insaneHero.lockedTarget.GetComponent<PolygonCyberCity.NPC.Spawner.CyberNPCController>();
                        bool isDead = (ins != null && ins.IsDead) || (npc != null && npc.IsDead);
                        if (!isDead) targetValid = true;
                    }
                }
                catch
                {
                    targetValid = false;
                }

                if (!targetValid)
                {
                    insaneHero.lockedTarget = null;
                }
                else
                {
                    Vector3 toLock = insaneHero.lockedTarget.position - target.position;
                    toLock.y = 0f;
                    if (toLock.sqrMagnitude > 0.01f)
                    {
                        Quaternion lockRot = Quaternion.LookRotation(toLock);
                        _yaw = Mathf.LerpAngle(_yaw, lockRot.eulerAngles.y, Time.deltaTime * 10f);
                        _pitch = Mathf.Lerp(_pitch, 18f, Time.deltaTime * 6f);
                    }
                }
            }
            else if (Cursor.lockState == CursorLockMode.Locked && !SyntyCombined.InGamePauseManager.IsPaused)
            {
                _yaw += Input.GetAxis("Mouse X") * mouseSensitivityX;
                float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivityY;
                _pitch += invertY ? mouseY : -mouseY;
                _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
            }

            // Desired rotation and position centered on target upper torso + shoulder offset
            Quaternion baseRotation = Quaternion.Euler(_pitch, _yaw, 0f);

            // True First-Person Perspective Mode
            if (isFirstPerson)
            {
                Vector3 eyeCenter = target.position + baseRotation * firstPersonEyeOffset;

                if (_trauma > 0.001f)
                {
                    _trauma = Mathf.Clamp01(_trauma - Time.unscaledDeltaTime * traumaDecayRate);
                    float shakeMagnitude = Mathf.Pow(_trauma, traumaExponent);
                    float t = Time.unscaledTime * shakeFrequency;
                    float rollKick = (Mathf.PerlinNoise(t + 17.1f, 0f) * 2f - 1f) * (maxShakeRoll * 0.6f) * shakeMagnitude;
                    float pitchKick = (Mathf.PerlinNoise(0f, t + 31.4f) * 2f - 1f) * (maxShakePitch * 0.6f) * shakeMagnitude;

                    transform.position = eyeCenter;
                    transform.rotation = baseRotation * Quaternion.Euler(pitchKick, 0f, rollKick);
                }
                else
                {
                    transform.position = eyeCenter;
                    transform.rotation = baseRotation;
                }
                return;
            }

            float shoulderX = (Mathf.Abs(cameraOffset.x) > 0.01f ? Mathf.Abs(cameraOffset.x) : 0.48f) * _currentShoulderSide;
            Vector3 shoulderOffset = baseRotation * new Vector3(shoulderX, cameraOffset.y, 0f);
            Vector3 aimCenter = target.position + targetOffset + shoulderOffset;
            Vector3 desiredDirection = baseRotation * Vector3.back;
            float desiredDistance = _targetDistance;

            // Robust obstacle sphere-cast (ignores player, enemies, triggers, and transparent FX)
            int queryMask = obstacleLayers.value;
            if (queryMask == 0)
            {
                int ignoreMask = (1 << 1) | (1 << 2) | (1 << 4);
                int pl = LayerMask.NameToLayer("Player");
                if (pl >= 0) ignoreMask |= (1 << pl);
                int en = LayerMask.NameToLayer("Enemy");
                if (en >= 0) ignoreMask |= (1 << en);
                queryMask = ~ignoreMask;
                obstacleLayers = queryMask;
            }

            if (target != null)
            {
                queryMask &= ~(1 << target.gameObject.layer);
            }
            int pLayer = LayerMask.NameToLayer("Player");
            if (pLayer >= 0) queryMask &= ~(1 << pLayer);
            int eLayer = LayerMask.NameToLayer("Enemy");
            if (eLayer >= 0) queryMask &= ~(1 << eLayer);
            int tLayer = LayerMask.NameToLayer("TransparentFX");
            if (tLayer >= 0) queryMask &= ~(1 << tLayer);

            if (queryMask != 0)
            {
                float castDistance = _targetDistance;
                Vector3 castOrigin = aimCenter;

                if (Physics.SphereCast(castOrigin, cameraRadius, desiredDirection, out RaycastHit hit, castDistance, queryMask, QueryTriggerInteraction.Ignore))
                {
                    float safeHitDistance = Mathf.Max(minCollisionDistance, hit.distance - (cameraRadius * 0.5f));
                    desiredDistance = Mathf.Min(desiredDistance, safeHitDistance);
                }
                else if (Physics.Raycast(castOrigin, desiredDirection, out RaycastHit rayHit, castDistance + 0.1f, queryMask, QueryTriggerInteraction.Ignore))
                {
                    float safeHitDistance = Mathf.Max(minCollisionDistance, rayHit.distance - 0.15f);
                    desiredDistance = Mathf.Min(desiredDistance, safeHitDistance);
                }
            }

            // Anti-clipping responsiveness:
            // When pushed inward by a wall, apply immediately so the camera NEVER clips into walls or geometry.
            // When stepping back into open space, smoothly interpolate distance out.
            if (desiredDistance < _currentDistance)
            {
                _currentDistance = desiredDistance;
            }
            else
            {
                _currentDistance = Mathf.Lerp(_currentDistance, desiredDistance, Time.deltaTime * 10f);
            }

            Vector3 targetPosition = aimCenter + desiredDirection * _currentDistance;

            // Floor penetration guard: prevent camera from dipping through an actual solid surface directly beneath it
            if (queryMask != 0 && Physics.Raycast(targetPosition + Vector3.up * 0.4f, Vector3.down, out RaycastHit floorHit, 0.8f, queryMask, QueryTriggerInteraction.Ignore))
            {
                float floorMinY = floorHit.point.y + cameraRadius + 0.03f;
                if (targetPosition.y < floorMinY) targetPosition.y = floorMinY;
            }

            // Ceiling penetration guard: prevent camera from rising or clipping through ceiling geometry directly above
            if (queryMask != 0)
            {
                if (Physics.Raycast(targetPosition - Vector3.up * 0.1f, Vector3.up, out RaycastHit ceilHit, 2.0f, queryMask, QueryTriggerInteraction.Ignore))
                {
                    float ceilMaxY = ceilHit.point.y - (cameraRadius + 0.08f);
                    if (targetPosition.y > ceilMaxY) targetPosition.y = ceilMaxY;
                }
                if (Physics.Raycast(aimCenter, Vector3.up, out RaycastHit headCeilHit, 8.0f, queryMask, QueryTriggerInteraction.Ignore))
                {
                    float roomMaxY = headCeilHit.point.y - (cameraRadius + 0.08f);
                    if (targetPosition.y > roomMaxY) targetPosition.y = roomMaxY;
                }
            }

            Vector3 smoothPos = tightPositionFollow 
                ? targetPosition 
                : Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * smoothSpeed);

            // Stable orientation: baseRotation matches pitch/yaw exactly without downward tilt distortion.
            Quaternion smoothRot = baseRotation;

            // Calculate Impact Shake
            if (_trauma > 0.001f)
            {
                _trauma = Mathf.Clamp01(_trauma - Time.unscaledDeltaTime * traumaDecayRate);
                float shakeMagnitude = Mathf.Pow(_trauma, traumaExponent);

                float t = Time.unscaledTime * shakeFrequency;
                float offsetX = (Mathf.PerlinNoise(t, 0.0f) * 2f - 1f) * maxShakeTranslation * shakeMagnitude;
                float offsetY = (Mathf.PerlinNoise(0.0f, t) * 2f - 1f) * maxShakeTranslation * shakeMagnitude;
                float offsetZ = (Mathf.PerlinNoise(t, t) * 2f - 1f) * (maxShakeTranslation * 0.35f) * shakeMagnitude;

                float rollKick = (Mathf.PerlinNoise(t + 17.1f, 0f) * 2f - 1f) * maxShakeRoll * shakeMagnitude;
                float pitchKick = (Mathf.PerlinNoise(0f, t + 31.4f) * 2f - 1f) * maxShakePitch * shakeMagnitude;

                // Decay directional kick impulse smoothly
                _directionalKick = Vector3.Lerp(_directionalKick, Vector3.zero, Time.unscaledDeltaTime * 14f);

                // Apply shake relative to camera orientation
                Vector3 shakePosOffset = smoothRot * new Vector3(offsetX, offsetY, offsetZ) + _directionalKick;
                transform.position = smoothPos + shakePosOffset;
                transform.rotation = smoothRot * Quaternion.Euler(pitchKick, 0f, rollKick);
            }
            else
            {
                transform.position = smoothPos;
                transform.rotation = smoothRot;
            }
        }

        public void TogglePerspective()
        {
            isFirstPerson = !isFirstPerson;
            if (isFirstPerson)
            {
                _saved3rdPersonDistance = Mathf.Max(_targetDistance, 1.35f);
                _targetDistance = 0f;
                _currentDistance = 0f;
                SetPlayerMeshVisibilityFor1stPerson(false);
                SyntyCombined.CyberpunkHUDController.Instance?.ShowToast("PERSPECTIVE // 1ST-PERSON KIROSHI OPTICS", 2.0f);
            }
            else
            {
                _targetDistance = _saved3rdPersonDistance;
                SetPlayerMeshVisibilityFor1stPerson(true);
                SyntyCombined.CyberpunkHUDController.Instance?.ShowToast("PERSPECTIVE // 3RD-PERSON TACTICAL", 2.0f);
            }
            SyntyCombined.CyberpunkHUDController.Instance?.UpdateActionChipsPerspective(isFirstPerson);
        }

        private void SetPlayerMeshVisibilityFor1stPerson(bool visible)
        {
            if (target == null) return;
            if (_playerRenderers == null || _playerRenderers.Length == 0)
            {
                _playerRenderers = target.GetComponentsInChildren<Renderer>(true);
            }
            foreach (var r in _playerRenderers)
            {
                if (r == null) continue;
                if (r is SkinnedMeshRenderer)
                {
                    r.shadowCastingMode = visible ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                }
            }
        }

        public static bool cursorLockOverride = false;

        private void HandleCursorLock()
        {
            if (cursorLockOverride || SyntyCombined.InGamePauseManager.IsPaused)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            if (target != null)
            {
                _yaw = target.eulerAngles.y;
                _pitch = 15f;
                SnapToTarget();
            }
        }

        /// <summary>
        /// Add trauma (0.0 to 1.0) to the camera. Stacks additively up to 1.0.
        /// </summary>
        public static void AddShake(float trauma)
        {
            if (Instance != null)
            {
                Instance._trauma = Mathf.Clamp01(Instance._trauma + trauma * traumaMultiplier);
            }
        }

        /// <summary>
        /// Trigger an impact camera shake with trauma and optional directional kick impulse.
        /// </summary>
        public static void ShakeImpact(float trauma, Vector3 directionalKick = default)
        {
            if (Instance != null)
            {
                Instance._trauma = Mathf.Clamp01(Instance._trauma + trauma * traumaMultiplier);
                if (directionalKick != default)
                {
                    Instance._directionalKick += directionalKick;
                }
            }
        }

        /// <summary>
        /// Trigger a micro-pause frame freeze (hit-stop) to convey devastating weapon impact weight.
        /// </summary>
        public static void TriggerHitStop(float durationSeconds = 0.05f, float slowScale = 0.02f)
        {
            if (Instance != null)
            {
                Instance.StartHitStop(durationSeconds, slowScale);
            }
        }

        private void StartHitStop(float durationSeconds, float slowScale)
        {
            if (_hitStopCoroutine != null) StopCoroutine(_hitStopCoroutine);
            _hitStopCoroutine = StartCoroutine(HitStopRoutine(durationSeconds, slowScale));
        }

        private IEnumerator HitStopRoutine(float duration, float slowScale)
        {
            float prevScale = Time.timeScale;
            Time.timeScale = slowScale;
            yield return new WaitForSecondsRealtime(duration);
            Time.timeScale = prevScale;
            _hitStopCoroutine = null;
        }

        /// <summary>
        /// Immediately syncs pitch and yaw to the transform's current world orientation and snaps target offset.
        /// Perfect for seamless handoff from cutscene cameras to gameplay.
        /// </summary>
        public void SyncToCurrentTransform()
        {
            Vector3 angles = transform.eulerAngles;
            _yaw = angles.y;
            float p = angles.x;
            if (p > 180f) p -= 360f;
            _pitch = Mathf.Clamp(p, minPitch, maxPitch);
            SnapToTarget();
        }

        /// <summary>
        /// Immediately snap camera position and rotation to target without smoothing (e.g. after player respawn/teleport).
        /// </summary>
        public void SnapToTarget()
        {
            if (target == null) return;
            _directionalKick = Vector3.zero;
            _trauma = 0f;
            _currentDistance = _targetDistance;
            Quaternion baseRotation = Quaternion.Euler(_pitch, _yaw, 0f);
            float shoulderX = (Mathf.Abs(cameraOffset.x) > 0.01f ? Mathf.Abs(cameraOffset.x) : 0.48f) * _currentShoulderSide;
            Vector3 shoulderOffset = baseRotation * new Vector3(shoulderX, cameraOffset.y, 0f);
            Vector3 aimCenter = target.position + targetOffset + shoulderOffset;
            Vector3 desiredDirection = baseRotation * Vector3.back;
            transform.position = aimCenter + desiredDirection * _currentDistance;
            transform.rotation = baseRotation;
        }
    }
}
