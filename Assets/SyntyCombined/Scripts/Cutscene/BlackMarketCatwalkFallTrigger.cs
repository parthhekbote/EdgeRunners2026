using System.Collections;
using UnityEngine;

namespace SyntyCombined.BlackMarket
{
    /// <summary>
    /// Governs the elevated Gantry Catwalk collapse and Alita's dramatic fall away
    /// into the lower cargo alleyway during the syndicate bounty ambush.
    /// </summary>
    public class BlackMarketCatwalkFallTrigger : MonoBehaviour
    {
        public static BlackMarketCatwalkFallTrigger Instance { get; private set; }

        [Header("Catwalk & Fall Settings")]
        public Transform catwalkSectionToCollapse;
        public Vector3 triggerCenter = new Vector3(15.0f, 1.88f, 27.5f);
        public Vector3 triggerSize = new Vector3(4.0f, 3.5f, 4.0f);
        public Vector3 fallVelocityImpulse = new Vector3(-4.5f, 2.0f, -4.5f); // Blows her off the railing
        public float lowerTrenchYThreshold = -0.5f;

        [Header("Audio & FX")]
        public AudioClip explosionSFX;
        public AudioClip fallLandingSFX;
        public GameObject explosionVFXPrefab;

        // Runtime state
        private bool _isTriggered = false;
        private bool _hasLanded = false;
        private Transform _playerTransform;
        private CharacterController _playerCC;
        private Rigidbody _playerRb;

        private void Awake()
        {
            if (Instance == null) Instance = this;
        }

        private void Start()
        {
            FindPlayer();
            EnsureTriggerCollider();
        }

        private void FindPlayer()
        {
            var p = GameObject.FindGameObjectWithTag("Player") ?? GameObject.Find("Alita") ?? GameObject.Find("Alita_Hero") ?? GameObject.Find("Alita_BattleAngel_Hero");
            if (p != null)
            {
                _playerTransform = p.transform;
                _playerCC = p.GetComponent<CharacterController>();
                _playerRb = p.GetComponent<Rigidbody>();
            }
        }

        private void EnsureTriggerCollider()
        {
            var col = GetComponent<BoxCollider>();
            if (col == null)
            {
                col = gameObject.AddComponent<BoxCollider>();
            }
            col.isTrigger = true;
            col.size = triggerSize;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_isTriggered) return;

            // Only trigger during the escape sprint
            if (BlackMarketStoryCombatDirector.Instance == null ||
                BlackMarketStoryCombatDirector.Instance.currentState != BlackMarketStoryState.FleeAcrossCatwalk)
            {
                return;
            }

            if (other.CompareTag("Player") || other.name.Contains("Alita"))
            {
                TriggerCatwalkCollapse();
            }
        }

        public void TriggerCatwalkCollapse()
        {
            if (_isTriggered) return;
            _isTriggered = true;

            StartCoroutine(ExecuteCollapseSequence());
        }

        private IEnumerator ExecuteCollapseSequence()
        {
            FindPlayer();

            // 1. Play massive explosion audio
            if (explosionSFX != null)
            {
                AudioSource.PlayClipAtPoint(explosionSFX, transform.position, 1.2f);
            }

            // 2. Spawn explosion particle effect
            if (explosionVFXPrefab != null)
            {
                var vfx = Instantiate(explosionVFXPrefab, transform.position + Vector3.up * 0.5f, Quaternion.identity);
                Destroy(vfx, 4.0f);
            }

            // 3. Shake camera violently
            StartCoroutine(DoScreenShake(1.2f, 0.45f));

            // 4. Break / drop the catwalk section if assigned
            if (catwalkSectionToCollapse != null)
            {
                var rb = catwalkSectionToCollapse.GetComponent<Rigidbody>();
                if (rb == null) rb = catwalkSectionToCollapse.gameObject.AddComponent<Rigidbody>();
                rb.mass = 800f;
                rb.isKinematic = false;
                rb.AddForce(Vector3.down * 15f + Vector3.back * 10f, ForceMode.Impulse);
                rb.AddTorque(new Vector3(15f, 5f, 25f), ForceMode.Impulse);
            }

            // 5. Kinetic blast impulse: Blow Alita off the catwalk railing down into the trench!
            if (_playerTransform != null)
            {
                if (_playerCC != null)
                {
                    // Move character controller off the edge
                    StartCoroutine(ApplyCCVelocity(_playerCC, fallVelocityImpulse, 0.4f));
                }
                else if (_playerRb != null)
                {
                    _playerRb.AddForce(fallVelocityImpulse * 50f, ForceMode.VelocityChange);
                }
            }

            // Notify Director
            if (BlackMarketStoryCombatDirector.Instance != null)
            {
                BlackMarketStoryCombatDirector.Instance.OnCatwalkExploded();
            }

            // 6. Monitor fall until Alita hits the lower trench ground
            float fallTimer = 0f;
            while (!_hasLanded && fallTimer < 4.0f)
            {
                fallTimer += Time.deltaTime;
                if (_playerTransform != null && _playerTransform.position.y <= lowerTrenchYThreshold)
                {
                    OnAlitaLandedInLowerTrench();
                    break;
                }
                yield return null;
            }

            if (!_hasLanded)
            {
                OnAlitaLandedInLowerTrench();
            }
        }

        private IEnumerator ApplyCCVelocity(CharacterController cc, Vector3 impulse, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration && cc != null)
            {
                cc.Move(impulse * Time.deltaTime * 6.0f);
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private void OnAlitaLandedInLowerTrench()
        {
            if (_hasLanded) return;
            _hasLanded = true;

            // Landing Impact SFX
            if (fallLandingSFX != null && _playerTransform != null)
            {
                AudioSource.PlayClipAtPoint(fallLandingSFX, _playerTransform.position, 1.0f);
            }

            // Heavy landing impact camera shake
            StartCoroutine(DoScreenShake(0.6f, 0.35f));

            // Dramatic dialogue / toast
            var hud = BlackMarketNPCDialogueHUD.GetOrCreate();
            hud.ShowMissionBanner("⚠️ CATWALK DESTROYED! TRAPPED IN LOWER MAINTENANCE TRENCH! PURSUIT INCOMING!");

            if (CyberpunkHUDController.Instance != null)
            {
                CyberpunkHUDController.Instance.ShowToast("[ALITA] Catwalk blew! Need to fight through the trench to Pad B-04!", 4.5f);
            }

            if (BlackMarketStoryCombatDirector.Instance != null)
            {
                BlackMarketStoryCombatDirector.Instance.OnCatwalkFallen();
            }
        }

        private IEnumerator DoScreenShake(float duration, float magnitude)
        {
            var cam = Camera.main;
            if (cam == null) yield break;

            Vector3 origPos = cam.transform.localPosition;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float x = Random.Range(-1f, 1f) * magnitude;
                float y = Random.Range(-1f, 1f) * magnitude;
                cam.transform.localPosition = origPos + new Vector3(x, y, 0f);

                elapsed += Time.deltaTime;
                magnitude = Mathf.Lerp(magnitude, 0f, elapsed / duration);
                yield return null;
            }

            cam.transform.localPosition = origPos;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.4f);
            Gizmos.DrawCube(transform.position, triggerSize);
        }
    }
}
