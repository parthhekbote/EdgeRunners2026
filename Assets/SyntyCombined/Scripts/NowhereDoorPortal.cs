using UnityEngine;

namespace SyntyCombined
{
    /// <summary>
    /// Surreal "Door in the Middle of Nowhere" that blends into the misty cyberspace expanse.
    /// It stands completely isolated in the open void far back from the Blackwall.
    /// When the player approaches:
    /// - The door slides open with an electronic airlock hiss and red indicator light.
    /// When the player crosses through the door frame:
    /// - Seamlessly activates the enclosed Datacenter Server Room with
    ///   banks of servers, the central pulsing red laser extrusion core, combat enemies,
    ///   and the monumental 200m Blackwall towering at the far end!
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class NowhereDoorPortal : MonoBehaviour
    {
        [Header("Connected Interior")]
        public GameObject serverRoomRoot;

        [Header("Door Visuals")]
        public Transform leftDoorPanel;
        public Transform rightDoorPanel;
        public Light doorIndicatorLight;

        [Header("Audio")]
        public AudioSource audioSource;
        public AudioClip doorOpenSound;
        public AudioClip transitionGlitchSound;

        [Header("Settings")]
        public float triggerDistance = 6.0f;
        public float openSlideDistance = 0.95f;
        public float openSpeed = 4.5f;

        private Transform _playerTransform;
        private bool _isOpen = false;
        private bool _isInside = false;
        private Vector3 _leftClosedLocalPos;
        private Vector3 _rightClosedLocalPos;

        private void Awake()
        {
            var col = GetComponent<BoxCollider>();
            if (col != null)
            {
                col.isTrigger = true;
                col.center = new Vector3(0f, 1.5f, 0f);
                col.size = new Vector3(3.0f, 3.5f, 2.5f);
            }
        }

        private void Start()
        {
            FindPlayer();

            if (leftDoorPanel != null) _leftClosedLocalPos = leftDoorPanel.localPosition;
            if (rightDoorPanel != null) _rightClosedLocalPos = rightDoorPanel.localPosition;

            float doorZ = transform.position.z;
            if (_playerTransform != null && _playerTransform.position.z > doorZ + 0.5f)
            {
                _isInside = true;
                if (serverRoomRoot != null) serverRoomRoot.SetActive(true);
            }
            else
            {
                _isInside = false;
                if (serverRoomRoot != null) serverRoomRoot.SetActive(false);
            }
        }

        private void FindPlayer()
        {
            if (_playerTransform == null)
            {
                var p = GameObject.FindWithTag("Player");
                if (p != null) _playerTransform = p.transform;
            }
        }

        private void Update()
        {
            FindPlayer();
            if (_playerTransform == null) return;

            float dist = Vector3.Distance(transform.position, _playerTransform.position);
            bool shouldOpen = dist <= triggerDistance;

            if (shouldOpen != _isOpen)
            {
                _isOpen = shouldOpen;
                if (_isOpen && audioSource != null && doorOpenSound != null)
                {
                    audioSource.PlayOneShot(doorOpenSound, 0.7f);
                }
            }

            // Animate sliding panels
            if (leftDoorPanel != null)
            {
                Vector3 targetLeft = _leftClosedLocalPos + (_isOpen ? Vector3.left * openSlideDistance : Vector3.zero);
                leftDoorPanel.localPosition = Vector3.Lerp(leftDoorPanel.localPosition, targetLeft, Time.deltaTime * openSpeed);
            }

            if (rightDoorPanel != null)
            {
                Vector3 targetRight = _rightClosedLocalPos + (_isOpen ? Vector3.right * openSlideDistance : Vector3.zero);
                rightDoorPanel.localPosition = Vector3.Lerp(rightDoorPanel.localPosition, targetRight, Time.deltaTime * openSpeed);
            }

            // Pulse indicator light
            if (doorIndicatorLight != null)
            {
                float pulse = Mathf.Sin(Time.time * 3.5f) * 0.35f + 0.65f;
                doorIndicatorLight.intensity = _isOpen ? 4.5f : (2.2f * pulse);
            }

            // Coordinate threshold fallback
            float doorZ = transform.position.z;
            float playerZ = _playerTransform.position.z;

            if (!_isInside && playerZ >= (doorZ + 0.1f))
            {
                EnterServerRoom();
            }
            else if (_isInside && playerZ < (doorZ - 1.5f))
            {
                ExitToCyberspaceVoid();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                EnterServerRoom();
            }
        }

        public void EnterServerRoom()
        {
            if (_isInside) return;
            _isInside = true;
            if (serverRoomRoot != null) serverRoomRoot.SetActive(true);

            if (audioSource != null && transitionGlitchSound != null)
            {
                audioSource.PlayOneShot(transitionGlitchSound, 0.85f);
            }
            Debug.Log("<color=#00FFAA>[Nowhere Door]</color> Player stepped through into the Server Room!");
        }

        public void ExitToCyberspaceVoid()
        {
            if (!_isInside) return;
            _isInside = false;
            if (serverRoomRoot != null) serverRoomRoot.SetActive(false);
            Debug.Log("<color=#FF0055>[Nowhere Door]</color> Player stepped back out into the open Cyberspace void!");
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, triggerDistance);
        }
    }
}
