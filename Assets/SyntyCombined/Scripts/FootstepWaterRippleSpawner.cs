using UnityEngine;

namespace SyntyCombined
{
    /// <summary>
    /// Spawns subtle concentric water ripples beneath combatants' feet on the Cyberspace salt-flat mirror floor.
    /// </summary>
    public class FootstepWaterRippleSpawner : MonoBehaviour
    {
        [Header("Ripple Settings")]
        public GameObject ripplePrefab;
        public float rippleInterval = 0.28f;
        public float minMoveSpeed = 0.5f;
        public float groundHeight = 0.02f;

        private Vector3 _lastPos;
        private float _lastRippleTime;

        private void Start()
        {
            _lastPos = transform.position;
            _lastRippleTime = Time.time;
        }

        private void Update()
        {
            Vector3 delta = transform.position - _lastPos;
            delta.y = 0;
            float speed = delta.magnitude / Mathf.Max(Time.deltaTime, 0.001f);
            _lastPos = transform.position;

            if (speed >= minMoveSpeed && Time.time - _lastRippleTime >= rippleInterval)
            {
                SpawnRipple();
                _lastRippleTime = Time.time;
            }
        }

        private void SpawnRipple()
        {
            if (ripplePrefab == null) return;

            Vector3 spawnPos = new Vector3(transform.position.x, groundHeight, transform.position.z);
            GameObject ripple = Instantiate(ripplePrefab, spawnPos, Quaternion.Euler(90f, 0f, 0f));
            ripple.transform.localScale = Vector3.one * Random.Range(0.8f, 1.25f);
            Destroy(ripple, 2.5f);
        }
    }
}
