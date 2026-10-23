using UnityEngine;

namespace Alita
{
    /// <summary>
    /// Procedurally animates the blazing TRON Identity Disc / Quantum Radial Sphere
    /// during the IMAX Init Sequence. Detonates into a massive radial sphere of
    /// circuit light beams at 10.2s following Dr. Ido's terminal cursor flash!
    /// </summary>
    public class TronIdentityDisc : MonoBehaviour
    {
        [Header("Rotation")]
        public float spinSpeed = 45f;
        public float counterSpinSpeed = -28f;
        public float raysSpinSpeed = 12f;
        public Transform innerRing;
        public Transform midRing;
        public Transform outerRing;
        public Transform radialRaysRoot;

        [Header("Pulsing Core")]
        public Light coreLight;
        public float baseIntensity = 5.0f;
        public float pulseAmount = 1.5f;

        [Header("Detonation Explosion")]
        public bool hasDetonated = false;

        private Vector3 _originalScale = Vector3.one;

        private void Awake()
        {
            _originalScale = transform.localScale;
        }

        public void TriggerDetonation(float progress)
        {
            // progress: 0 to 1 over the 10.2s - 25s phase
            hasDetonated = true;

            // Shockwave expansion curve: sudden outward burst then steady breathing
            float burst = Mathf.Clamp01(progress / 0.12f);
            float scaleMult = Mathf.Lerp(0.05f, 1.25f, burst);
            if (progress > 0.12f)
            {
                float tRest = (progress - 0.12f) / 0.88f;
                scaleMult = Mathf.Lerp(1.25f, 1.0f, tRest);
            }
            transform.localScale = _originalScale * scaleMult;

            if (coreLight != null)
            {
                // Blinding spike at moment of detonation
                float spike = (1f - Mathf.Clamp01(progress / 0.08f)) * 16f;
                coreLight.intensity = baseIntensity + spike + Mathf.Sin(Time.time * 6f) * pulseAmount;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (outerRing != null)
            {
                outerRing.Rotate(Vector3.forward, spinSpeed * dt, Space.Self);
            }

            if (midRing != null)
            {
                midRing.Rotate(Vector3.forward, (spinSpeed * 0.7f) * dt, Space.Self);
            }

            if (innerRing != null)
            {
                innerRing.Rotate(Vector3.forward, counterSpinSpeed * dt, Space.Self);
            }

            if (radialRaysRoot != null)
            {
                radialRaysRoot.Rotate(Vector3.forward, raysSpinSpeed * dt, Space.Self);
            }

            if (coreLight != null && !hasDetonated)
            {
                float pulse = Mathf.Sin(Time.time * 6f) * pulseAmount;
                coreLight.intensity = baseIntensity + pulse;
            }
        }
    }
}
