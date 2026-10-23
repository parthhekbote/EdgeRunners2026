using System.Collections;
using UnityEngine;

namespace SyntyCombined
{
    /// <summary>
    /// Monitors the abyss / Fade Plane below Demo_City.
    /// If the player falls off sidewalks, roofs, or bridges and hits the Fade Plane or drops
    /// below the altitude threshold, safely respawns them at a designated spawn point or safe checkpoint.
    /// </summary>
    [DisallowMultipleComponent]
    public class FadePlaneRespawn : MonoBehaviour
    {
        public static FadePlaneRespawn Instance { get; private set; }

        [Header("Spawn Location")]
        [Tooltip("Transform marker for where the player should respawn. If null, uses fallbackSpawnPosition.")]
        public Transform respawnPoint;

        [Tooltip("Default respawn coordinates if no respawnPoint Transform is assigned.")]
        public Vector3 fallbackSpawnPosition = new Vector3(12.55f, -17.98f, 57.12f);

        [Tooltip("Default respawn facing angle (yaw).")]
        public float fallbackSpawnYaw = 130f;

        [Header("Checkpoint System")]
        [Tooltip("If enabled, automatically records the player's last safe grounded position every 2 seconds.")]
        public bool useLastSafeGroundedPosition = false;

        [Tooltip("Minimum Y height to consider a grounded position safe (prevents saving while in the abyss).")]
        public float minSafeGroundedY = -25.0f;

        [Header("Altitude Failsafe")]
        [Tooltip("Failsafe trigger: If player Y drops below this value, respawn immediately even if collider misses.")]
        public float fallAltitudeThreshold = -52.0f;

        [Header("Feedback")]
        [Tooltip("Restore player health to maximum on respawn.")]
        public bool restoreHealthOnRespawn = true;

        [Tooltip("Optional audio clip played upon respawning.")]
        public AudioClip respawnAudioClip;

        private Transform _playerTransform;
        private SyntySwordLocomotionController _playerLocomotion;
        private InsaneGun.Combat.InsaneGunSwordController _insanePlayer;
        private Vector3 _lastSafePosition;
        private Quaternion _lastSafeRotation;
        private float _lastGroundedCheckTimer = 0f;
        private bool _isRespawning = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _lastSafePosition = fallbackSpawnPosition;
            _lastSafeRotation = Quaternion.Euler(0f, fallbackSpawnYaw, 0f);

            // Clean up any rogue BoxColliders added by prior scripts
            var rogueBoxes = GetComponents<BoxCollider>();
            foreach (var b in rogueBoxes)
            {
                if (Application.isPlaying) Destroy(b);
                else DestroyImmediate(b);
            }
        }

        private void Start()
        {
            LocatePlayer();
        }

        private void LocatePlayer()
        {
            if (_playerTransform == null || _insanePlayer == null && _playerLocomotion == null)
            {
                GameObject playerObj = GameObject.FindWithTag("Player");
                if (playerObj == null)
                {
                    var insane = Object.FindAnyObjectByType<InsaneGun.Combat.InsaneGunSwordController>();
                    if (insane != null) playerObj = insane.gameObject;
                }
                if (playerObj == null)
                {
                    SyntySwordLocomotionController loc = Object.FindAnyObjectByType<SyntySwordLocomotionController>();
                    if (loc != null) playerObj = loc.gameObject;
                }

                if (playerObj != null)
                {
                    _playerTransform = playerObj.transform;
                    _insanePlayer = playerObj.GetComponent<InsaneGun.Combat.InsaneGunSwordController>();
                    _playerLocomotion = playerObj.GetComponent<SyntySwordLocomotionController>();
                }
            }
        }

        private void Update()
        {
            if (_playerTransform == null)
            {
                LocatePlayer();
                if (_playerTransform == null) return;
            }

            // 1. Checkpoint tracker
            if (useLastSafeGroundedPosition)
            {
                _lastGroundedCheckTimer += Time.deltaTime;
                if (_lastGroundedCheckTimer >= 2.0f)
                {
                    _lastGroundedCheckTimer = 0f;
                    if (_playerLocomotion != null && _playerLocomotion.IsGroundedState && _playerTransform.position.y > minSafeGroundedY)
                    {
                        _lastSafePosition = _playerTransform.position;
                        _lastSafeRotation = _playerTransform.rotation;
                    }
                }
            }

            // 2. High-speed altitude watchdog failsafe
            // Sub-level interiors live at Y ≈ -200m; only trigger abyss respawn if falling past street abyss (-52m to -150m) or sub-level abyss (<= -260m)
            bool inInteriorSublevel = _playerTransform.position.y < -150f;
            float activeThreshold = inInteriorSublevel ? -260f : fallAltitudeThreshold;

            if (!_isRespawning && _playerTransform.position.y <= activeThreshold)
            {
                RespawnPlayer("Altitude watchdog threshold exceeded (fell into abyss)");
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // Only trigger if player is falling from the street into the abyss above the interior sublevel
            if (IsPlayerCollider(other) && _playerTransform != null && _playerTransform.position.y > -150f && _playerTransform.position.y <= fallAltitudeThreshold)
            {
                RespawnPlayer("Entered Fade Plane trigger in abyss");
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            // Only trigger if player is falling from the street into the abyss above the interior sublevel
            if (IsPlayerCollider(collision.collider) && _playerTransform != null && _playerTransform.position.y > -150f && _playerTransform.position.y <= fallAltitudeThreshold)
            {
                RespawnPlayer("Collided with Fade Plane mesh in abyss");
            }
        }

        private bool IsPlayerCollider(Collider col)
        {
            if (col == null) return false;
            if (col.CompareTag("Player")) return true;
            if (_playerTransform != null && (col.transform == _playerTransform || col.transform.IsChildOf(_playerTransform))) return true;
            return false;
        }

        /// <summary>
        /// Resets the player position, momentum, and state.
        /// </summary>
        public void RespawnPlayer(string reason = "")
        {
            if (_isRespawning) return;
            StartCoroutine(RespawnRoutine(reason));
        }

        private IEnumerator RespawnRoutine(string reason)
        {
            _isRespawning = true;
            Debug.Log($"<color=#00FFFF>[FadePlaneRespawn] Respawning player! Reason: {reason}</color>");

            Vector3 targetPos = (respawnPoint != null)
                ? respawnPoint.position
                : (useLastSafeGroundedPosition ? _lastSafePosition : fallbackSpawnPosition);

            Quaternion targetRot = (respawnPoint != null)
                ? respawnPoint.rotation
                : (useLastSafeGroundedPosition ? _lastSafeRotation : Quaternion.Euler(0f, fallbackSpawnYaw, 0f));

            // Ensure player reference is valid
            LocatePlayer();

            if (_insanePlayer != null)
            {
                _insanePlayer.Revive(targetPos);
                _insanePlayer.transform.rotation = targetRot;
                if (restoreHealthOnRespawn)
                {
                    _insanePlayer.currentHealth = _insanePlayer.maxHealth;
                }
            }
            else if (_playerLocomotion != null)
            {
                // Cleanly teleport character controller and cancel falling velocity
                _playerLocomotion.Teleport(targetPos, targetRot);

                if (restoreHealthOnRespawn)
                {
                    _playerLocomotion.currentHealth = _playerLocomotion.maxHealth;
                    _playerLocomotion.OnPlayerHealthChanged?.Invoke(_playerLocomotion.currentHealth, _playerLocomotion.maxHealth);
                }
            }
            else if (_playerTransform != null)
            {
                CharacterController cc = _playerTransform.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                _playerTransform.position = targetPos;
                _playerTransform.rotation = targetRot;
                if (cc != null) cc.enabled = true;
            }

            // Play respawn sound if provided
            if (respawnAudioClip != null)
            {
                AudioSource.PlayClipAtPoint(respawnAudioClip, targetPos, 0.75f);
            }

            yield return new WaitForSeconds(0.2f);
            _isRespawning = false;
        }

        [ContextMenu("Test Respawn Player")]
        public void TestRespawn()
        {
            RespawnPlayer("Manual test via context menu");
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 spawn = respawnPoint != null ? respawnPoint.position : fallbackSpawnPosition;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(spawn, 0.75f);
            Gizmos.color = Color.blue;
            Vector3 fwd = respawnPoint != null ? respawnPoint.forward : Quaternion.Euler(0f, fallbackSpawnYaw, 0f) * Vector3.forward;
            Gizmos.DrawRay(spawn, fwd * 2.0f);

            // Draw kill plane line
            Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.4f);
            Gizmos.DrawCube(new Vector3(transform.position.x, fallAltitudeThreshold, transform.position.z), new Vector3(300f, 0.5f, 300f));
        }
    }
}
