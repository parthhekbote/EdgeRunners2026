using UnityEngine;

namespace Tron.Terminal
{
    /// <summary>
    /// Subtle cinematic camera sway and breathing motion to give the Tron terminal
    /// an immersive, alive cyberspace feel.
    /// </summary>
    public class TronCameraDrift : MonoBehaviour
    {
        public float swayAmount = 0.04f;
        public float swaySpeed = 0.6f;
        public float rotSwayAmount = 0.4f;

        private Vector3 _initialPos;
        private Quaternion _initialRot;

        private void Start()
        {
            _initialPos = transform.position;
            _initialRot = transform.rotation;
        }

        private void Update()
        {
            float t = Time.time * swaySpeed;
            float offsetX = Mathf.Sin(t * 0.9f) * swayAmount;
            float offsetY = Mathf.Cos(t * 1.2f) * (swayAmount * 0.6f);
            float offsetZ = Mathf.Sin(t * 0.7f) * (swayAmount * 0.4f);

            transform.position = _initialPos + new Vector3(offsetX, offsetY, offsetZ);

            float rotX = Mathf.Sin(t * 0.8f) * (rotSwayAmount * 0.5f);
            float rotY = Mathf.Cos(t * 1.1f) * rotSwayAmount;
            transform.rotation = _initialRot * Quaternion.Euler(rotX, rotY, 0f);
        }
    }
}
