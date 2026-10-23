using UnityEngine;

namespace Alita
{
    /// <summary>
    /// Master Visual Effect for the TRON: Ares Prologue Highway (9.8s - 32.0s).
    /// Re-creates the iconic film sequence beat-for-beat:
    /// - 10.2s - 23.5s: Radial Starburst, expanding concentric retinal mandala, and solar flare beams (DISNEY PRESENTS)
    /// - 23.5s - 27.0s: Vertical Cylindrical Conduit Shaft dive lined with neon circuit pillars (SEAN BAILEY PRODUCTION)
    /// - 27.0s - 32.0s: High-speed Illuminated Chevron Portal Corridor fly-through with triangular gates (A FILM BY JOACHIM RØNNING)
    /// </summary>
    public class TronEyePupilRadialBurstVFX : MonoBehaviour
    {
        [Header("Hierarchy Roots")]
        public Transform starburstRoot;
        public Transform verticalShaftRoot;
        public Transform chevronCorridorRoot;

        [Header("Starburst Components (10.2s - 23.5s)")]
        public Transform coreNucleus;
        public Transform aresCoreIdentityRoot;
        public Transform dataCubesRoot;
        public Transform radialRaysRoot;
        public Transform retinalRingsRoot;
        public Transform eyeApertureFrameRoot;
        public ParticleSystem emberBurstPS;
        public Light coreVolumetricLight;

        [Header("Vertical Shaft Components (23.5s - 27.0s)")]
        public Transform shaftPillarsRoot;
        public Light shaftLight;

        [Header("Chevron Corridor Components (27.0s - 32.0s)")]
        public Transform chevronArchesRoot;
        public Light corridorLight;

        [Header("Animation Speeds")]
        public float raySpinSpeed = 12f;
        public float ringCounterSpinSpeed = -18f;
        public float pupilBreathingRate = 2.5f;

        private Vector3 _baseScale = Vector3.one;

        private void Awake()
        {
            _baseScale = transform.localScale;
        }

        public void EvaluateAtTime(float time)
        {
            // Active during the eye pupil zoom-out sequence (10.2s - 45.0s)
            if (time < 10.2f || time >= 45.0f)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            // Keep starburst always active at the eye; disable separate shafts/corridors
            if (starburstRoot != null) starburstRoot.gameObject.SetActive(true);
            if (verticalShaftRoot != null) verticalShaftRoot.gameObject.SetActive(false);
            if (chevronCorridorRoot != null) chevronCorridorRoot.gameObject.SetActive(false);

            float impulse = Mathf.Clamp01((time - 10.2f) / 0.22f);
            float scaleMult = Mathf.Lerp(0.02f, 1.45f, impulse);
            if ((time - 10.2f) > 0.22f)
            {
                float tSustain = Mathf.Clamp01((time - 10.42f) / 13.08f);
                scaleMult = Mathf.Lerp(1.45f, 1.0f, tSustain);
            }

            // As camera pulls back from eye revealing face and cranium (24s - 45s), gently scale and fade into the ocular aperture
            if (time >= 24.0f)
            {
                float tFade = Mathf.Clamp01((time - 24.0f) / 20.0f);
                scaleMult *= Mathf.Lerp(1.0f, 0.15f, tFade);
            }

            if (starburstRoot != null) starburstRoot.localScale = _baseScale * scaleMult;

            // Core radiance & pulse for DISNEY PRESENTS (21.0s - 23.5s)
            if (coreVolumetricLight != null)
            {
                float baseInt = 1.4f;
                if (time >= 21.0f && time < 23.5f)
                {
                    float pulse = Mathf.Sin((time - 21.0f) * 12f) * 1.5f;
                    coreVolumetricLight.intensity = baseInt + 2.5f + pulse;
                    coreVolumetricLight.range = 14f;
                }
                else
                {
                    float spike = (1f - Mathf.Clamp01((time - 10.2f) / 0.45f)) * 4.0f;
                    float breath = Mathf.Sin(time * pupilBreathingRate) * 0.35f;
                    float fadeLight = time >= 24.0f ? (1f - Mathf.Clamp01((time - 24.0f) / 18.0f)) : 1.0f;
                    coreVolumetricLight.intensity = (baseInt + spike + breath) * fadeLight;
                    coreVolumetricLight.range = 9.0f * fadeLight;
                }
            }

            // Spin rays and concentric rings
            if (radialRaysRoot != null)
            {
                radialRaysRoot.localRotation = Quaternion.Euler(0f, 0f, (time - 10.2f) * raySpinSpeed);
            }
            if (retinalRingsRoot != null)
            {
                retinalRingsRoot.localRotation = Quaternion.Euler(0f, 0f, (time - 10.2f) * ringCounterSpinSpeed);
            }
            if (aresCoreIdentityRoot != null)
            {
                aresCoreIdentityRoot.localRotation = Quaternion.Euler((time - 10.2f) * 18f, (time - 10.2f) * 26f, (time - 10.2f) * 12f);
            }
        }

        /// <summary>
        /// Frame-accurate cinematic camera solver for the prologue highway sequence (10.2s - 32.0s).
        /// </summary>
        public bool GetCinematicCameraTransform(float time, out Vector3 camPos, out Quaternion camRot, out float fov)
        {
            if (time < 10.2f || time >= 32.0f)
            {
                camPos = Vector3.zero;
                camRot = Quaternion.identity;
                fov = 40f;
                return false;
            }

            if (time < 23.5f)
            {
                // 1. STARBURST & RETINAL MANDALA (10.2s - 23.5s) - DISNEY PRESENTS
                float t = Mathf.Clamp01((time - 10.2f) / 13.3f);
                Vector3 sStart = starburstRoot != null ? starburstRoot.TransformPoint(new Vector3(0f, 0f, 1.40f)) : new Vector3(0f, 0.23f, 2.05f);
                Vector3 sEnd = starburstRoot != null ? starburstRoot.TransformPoint(new Vector3(0f, 0f, 0.68f)) : new Vector3(0f, 0.23f, 1.33f);
                camPos = Vector3.Lerp(sStart, sEnd, Mathf.SmoothStep(0f, 1f, t));

                Vector3 target = starburstRoot != null ? starburstRoot.position : new Vector3(0f, 0.23f, 0.65f);
                camRot = Quaternion.LookRotation((target - camPos).normalized, Vector3.up);
                camRot *= Quaternion.Euler(0f, 0f, (time - 10.2f) * 3.5f);
                fov = Mathf.Lerp(54f, 40f, t);
                return true;
            }
            else if (time < 27.0f)
            {
                // 2. VERTICAL SHAFT PLUNGE (23.5s - 27.0s) - SEAN BAILEY PRODUCTION
                // Position camera near the curved wall of the cylindrical shaft looking down and across the curving circuit tracks
                float t = Mathf.Clamp01((time - 23.5f) / 3.5f);
                float plungeT = Mathf.Pow(t, 0.82f);

                Vector3 sBase = verticalShaftRoot != null ? verticalShaftRoot.position : new Vector3(0f, -8f, -40f);
                camPos = sBase + new Vector3(0f, -3.5f - plungeT * 26f, -4.2f);
                Vector3 lookTarget = sBase + new Vector3(0f, -3.5f - plungeT * 26f - 14f, 1.5f);
                camRot = Quaternion.LookRotation((lookTarget - camPos).normalized, Vector3.up);
                camRot *= Quaternion.Euler(0f, 0f, (time - 23.5f) * 16f); // Spiral spin along the cylinder wall
                fov = Mathf.Lerp(52f, 42f, t);
                return true;
            }
            else
            {
                // 3. CHEVRON PORTAL CORRIDOR FLY-THROUGH (27.0s - 32.0s) - A FILM BY JOACHIM RØNNING
                // Camera flies forward inside the illuminated corridor past the glowing triangular chevron arches
                float t = Mathf.Clamp01((time - 27.0f) / 5.0f);
                float corrT = Mathf.SmoothStep(0f, 1f, t);

                Vector3 cBase = chevronCorridorRoot != null ? chevronCorridorRoot.position : new Vector3(0f, -46f, -10f);
                camPos = cBase + new Vector3(1.2f, 1.8f, -32f + corrT * 64f);
                Vector3 lookTarget = camPos + new Vector3(-0.35f, 0.15f, 9.5f);
                camRot = Quaternion.LookRotation((lookTarget - camPos).normalized, Vector3.up);
                camRot *= Quaternion.Euler(0f, 0f, -15.5f); // Signature diagonal corridor tilt
                fov = Mathf.Lerp(48f, 42f, t);
                return true;
            }
        }
    }
}
