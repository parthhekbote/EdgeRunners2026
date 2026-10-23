using UnityEngine;

namespace Alita
{
    /// <summary>
    /// Master Visual Effect & Environment System for the TRON: Ares Orbital Horizon,
    /// Colossal Cybernetic Eye, and Body Reconstruction Sequence (32.0s - 64.0s).
    /// 
    /// Beat-for-beat matching the IMAX prologue reference:
    /// - 32.0s - 34.5s: Corridor glide over circular abyss pit with vertical illuminated circuit pillars (JARED LETO).
    /// - 34.5s - 39.5s: Ascension into planetary orbit showing the curved cybernetic world with
    ///                  multi-layer incandescent solar horizon (cyan rim -> white solar rim -> fiery crimson)
    ///                  and a luminous circular caldera igniting on the dark surface (GRETA LEE).
    /// - 39.5s - 43.5s: Steep orbital dive toward the radiant caldera with hundreds of streaming laser filaments
    ///                  revealing the concentric undulating cybernetic iris blossom (EVAN PETERS).
    /// - 43.5s - 48.5s: Frontal framing of the Colossal Wireframe Cybernetic Human Eye with almond eyelids,
    ///                  spherical grid, and floral rosette iris blossom (JODIE TURNER-SMITH & HASAN MINHAJ).
    /// - 48.5s - 50.0s: BRIGHT RED EXPLOSION IN HER EYE -> Searing flash detonates inside the pupil void
    ///                  with wireframe circuit lines flying violently OUTWARD into the lens!
    /// - 50.0s - 54.0s: Lateral tracking along expanding neon conduits tracing the Cranial Armor Shell (ARTURO CASTRO).
    /// - 54.0s - 58.5s: Elevated symmetrical framing of the reconstructing Cybernetic Helmet, Head, and Shoulders (WITH GILLIAN ANDERSON).
    /// - 58.5s - 64.0s: Molten nanite crystallization spreading across the cervical spine and shoulders (AND JEFF BRIDGES).
    /// </summary>
    public class TronOrbitalEyeSequenceVFX : MonoBehaviour
    {
        [Header("Root Transforms")]
        public Transform abyssPitRoot;
        public Transform orbitalEnvironmentRoot;
        public Transform planetCurvatureRoot;
        public Transform atmosphericHorizonRoot;
        public Transform radiantCalderaRoot;
        public Transform radialFilamentsRoot;
        public Transform concentricIrisStromaRoot;
        public Transform colossalCyberEyeRoot;
        public Transform eyeLidsRoot;
        public Transform faceWireframeTopologyRoot;

        [Header("Reconstruction Transforms (48.5s - 64.0s)")]
        public Transform eyeExplosionLinesRoot;
        public Transform reconstructingChassisRoot;
        public Transform naniteCrystallizationRoot;

        [Header("Cinematic Lighting")]
        public Light atmosphericRimLight;
        public Light calderaCoreLight;
        public Light cyberEyeKeyLight;
        public Light eyeExplosionFlashLight;
        public Light chassisKeyLight;

        [Header("Camera Transform Anchors")]
        public Transform pitCamStart;
        public Transform pitCamEnd;
        public Transform orbitalCamStart;
        public Transform orbitalCamEnd;
        public Transform diveCamStart;
        public Transform diveCamEnd;
        public Transform eyeCamStart;
        public Transform eyeCamEnd;
        public Transform eyeCamClose;
        public Transform cranialSweepStart;
        public Transform cranialSweepEnd;
        public Transform helmetElevatedStart;
        public Transform helmetElevatedEnd;
        public Transform spineStart;
        public Transform spineEnd;

        [Header("Animation Speeds")]
        public float filamentSpinSpeed = 4.0f;
        public float irisCounterSpinSpeed = -8.0f;
        public float horizonGlowPulseSpeed = 2.5f;

        private void Awake()
        {
            if (orbitalEnvironmentRoot != null)
                orbitalEnvironmentRoot.gameObject.SetActive(false);
            if (abyssPitRoot != null)
                abyssPitRoot.gameObject.SetActive(false);
            if (reconstructingChassisRoot != null)
                reconstructingChassisRoot.gameObject.SetActive(false);
            if (eyeExplosionLinesRoot != null)
                eyeExplosionLinesRoot.gameObject.SetActive(false);
        }

        /// <summary>
        /// Evaluates all orbital elements, lights, and sub-components at the cutscene time.
        /// </summary>
        public void EvaluateAtTime(float time)
        {
            bool isActive = (time >= 31.8f && time < 64.5f);

            if (!isActive)
            {
                if (orbitalEnvironmentRoot != null && orbitalEnvironmentRoot.gameObject.activeSelf)
                    orbitalEnvironmentRoot.gameObject.SetActive(false);
                if (abyssPitRoot != null && abyssPitRoot.gameObject.activeSelf)
                    abyssPitRoot.gameObject.SetActive(false);
                if (reconstructingChassisRoot != null && reconstructingChassisRoot.gameObject.activeSelf)
                    reconstructingChassisRoot.gameObject.SetActive(false);
                if (eyeExplosionLinesRoot != null && eyeExplosionLinesRoot.gameObject.activeSelf)
                    eyeExplosionLinesRoot.gameObject.SetActive(false);
                return;
            }

            // 1. Abyss Pit (32.0s - 34.8s)
            bool isPitActive = (time >= 31.8f && time < 34.8f);
            if (abyssPitRoot != null)
            {
                abyssPitRoot.gameObject.SetActive(isPitActive);
            }

            // 2. Orbital Space Environment (34.0s - 64.5f)
            bool isOrbitalActive = (time >= 34.0f && time < 64.5f);
            if (orbitalEnvironmentRoot != null)
            {
                orbitalEnvironmentRoot.gameObject.SetActive(isOrbitalActive);
            }

            if (!isOrbitalActive) return;

            // Shot 4+ active conditions
            bool isEyeShot = (time >= 43.5f && time < 50.2f);
            bool isReconstructShot = (time >= 49.5f && time < 64.5f);

            // Hide planet curvature, horizon, and caldera during the colossal Cyber Eye shot and body reconstruction
            if (planetCurvatureRoot != null)
            {
                planetCurvatureRoot.gameObject.SetActive(!isEyeShot && !isReconstructShot);
            }
            if (atmosphericHorizonRoot != null)
            {
                atmosphericHorizonRoot.gameObject.SetActive(!isEyeShot && !isReconstructShot);
                if (!isEyeShot && !isReconstructShot)
                {
                    float horizonGlow = 1.0f + Mathf.Sin(time * horizonGlowPulseSpeed) * 0.15f;
                    if (atmosphericRimLight != null)
                    {
                        atmosphericRimLight.intensity = 26f * horizonGlow;
                    }
                }
            }

            // Animate Radiant Caldera & Laser Filaments (37.0s - 43.5s)
            if (radiantCalderaRoot != null)
            {
                radiantCalderaRoot.gameObject.SetActive(!isEyeShot && !isReconstructShot);
                if (!isEyeShot && !isReconstructShot)
                {
                    float calderaIgnition = Mathf.Clamp01((time - 35.0f) / 2.0f);
                    radiantCalderaRoot.localScale = Vector3.one * Mathf.Lerp(0.01f, 1.0f, calderaIgnition);

                    if (calderaCoreLight != null)
                    {
                        float flare = (time >= 37.5f && time < 41.5f) ? 2.5f : 1.2f;
                        calderaCoreLight.intensity = calderaIgnition * (flare * 5.5f + Mathf.Sin(time * 8f) * 0.5f);
                    }
                }
            }

            // Spin & waviness of radial filaments
            if (radialFilamentsRoot != null && !isEyeShot && !isReconstructShot)
            {
                radialFilamentsRoot.Rotate(Vector3.up, filamentSpinSpeed * Time.deltaTime, Space.Self);
            }

            // Counter-rotation of concentric iris stroma
            if (concentricIrisStromaRoot != null && !isEyeShot && !isReconstructShot)
            {
                concentricIrisStromaRoot.Rotate(Vector3.up, irisCounterSpinSpeed * Time.deltaTime, Space.Self);
                float irisPulse = 1.0f + Mathf.Sin(time * 3.5f) * 0.04f;
                concentricIrisStromaRoot.localScale = new Vector3(irisPulse, irisPulse, 1.0f);
            }

            // Colossal Cybernetic Wireframe Human Eye (43.5s - 50.2s)
            if (colossalCyberEyeRoot != null)
            {
                colossalCyberEyeRoot.gameObject.SetActive(isEyeShot);

                if (isEyeShot)
                {
                    float eyeProgress = Mathf.Clamp01((time - 43.5f) / 1.5f);
                    float eyeScale = Mathf.Lerp(0.92f, 1.0f, eyeProgress);
                    colossalCyberEyeRoot.localScale = Vector3.one * eyeScale;

                    if (cyberEyeKeyLight != null)
                    {
                        cyberEyeKeyLight.intensity = Mathf.Lerp(0f, 3.8f, eyeProgress);
                    }
                }
            }

            // BRIGHT RED EXPLOSION IN HER EYE & OUTWARD MOVING LINES (48.5s - 50.5s)
            if (eyeExplosionLinesRoot != null)
            {
                bool isExploding = (time >= 48.5f && time < 50.5f);
                eyeExplosionLinesRoot.gameObject.SetActive(isExploding);

                if (isExploding)
                {
                    float expT = Mathf.Clamp01((time - 48.5f) / 1.8f);
                    float scaleExpand = Mathf.Lerp(0.1f, 5.5f, Mathf.Pow(expT, 0.65f));
                    eyeExplosionLinesRoot.localScale = Vector3.one * scaleExpand;

                    if (eyeExplosionFlashLight != null)
                    {
                        float flashDecay = (1.0f - expT);
                        eyeExplosionFlashLight.intensity = flashDecay * 35.0f;
                    }
                }
            }

            // RECONSTRUCTING CYBERNETIC CHASSIS (49.5s - 64.5s)
            if (reconstructingChassisRoot != null)
            {
                reconstructingChassisRoot.gameObject.SetActive(isReconstructShot);

                if (isReconstructShot)
                {
                    float reconT = Mathf.Clamp01((time - 49.5f) / 14.5f);
                    reconstructingChassisRoot.localScale = Vector3.one * Mathf.Lerp(0.90f, 1.0f, reconT);

                    if (chassisKeyLight != null)
                    {
                        chassisKeyLight.intensity = 14.0f + Mathf.Sin(time * 6.0f) * 2.0f;
                    }
                }
            }

            // MOLTEN NANITE CRYSTALLIZATION (56.0s - 64.5s)
            if (naniteCrystallizationRoot != null)
            {
                bool isNaniteActive = (time >= 56.0f && time < 64.5f);
                naniteCrystallizationRoot.gameObject.SetActive(isNaniteActive);

                if (isNaniteActive)
                {
                    float nanoT = Mathf.Clamp01((time - 56.0f) / 8.0f);
                    float shimmer = 1.0f + Mathf.Sin(time * 16.0f) * 0.22f;
                    naniteCrystallizationRoot.localScale = Vector3.one * (Mathf.Lerp(0.25f, 1.0f, nanoT) * shimmer);
                }
            }
        }

        /// <summary>
        /// Frame-accurate cinematic camera solver for the sequence (32.0s - 64.0s).
        /// </summary>
        public bool GetCinematicCameraTransform(float time, out Vector3 camPos, out Quaternion camRot, out float fov)
        {
            camPos = Vector3.zero;
            camRot = Quaternion.identity;
            fov = 42f;

            if (time < 32.0f || time >= 64.0f) return false;

            if (time < 34.5f)
            {
                // 1. SUBTERRANEAN CORRIDOR TO ABYSS PIT (32.0s - 34.5s) - JARED LETO
                // Camera glides along the monolithic wall on the right, looking up at the distant dawn flare with a -20 deg roll
                float t = Mathf.Clamp01((time - 32.0f) / 2.5f);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                Vector3 pStart = pitCamStart != null ? pitCamStart.position : (abyssPitRoot != null ? abyssPitRoot.TransformPoint(new Vector3(10.5f, 8.2f, -18.0f)) : new Vector3(10.5f, 8.2f, -18.0f));
                Vector3 pEnd = pitCamEnd != null ? pitCamEnd.position : (abyssPitRoot != null ? abyssPitRoot.TransformPoint(new Vector3(12.5f, 7.6f, -6.0f)) : new Vector3(12.5f, 7.6f, -6.0f));
                camPos = Vector3.Lerp(pStart, pEnd, smoothT);

                Vector3 target = abyssPitRoot != null 
                    ? abyssPitRoot.TransformPoint(new Vector3(3.0f, 15.0f, 25.0f))
                    : new Vector3(3.0f, 15.0f, 25.0f);
                camRot = Quaternion.LookRotation((target - camPos).normalized, Vector3.up);
                camRot *= Quaternion.Euler(6.0f, 0f, Mathf.Lerp(-20.0f, -18.5f, smoothT));
                fov = Mathf.Lerp(45f, 41f, smoothT);
                return true;
            }
            else if (time < 39.5f)
            {
                // 2. ORBITAL HORIZON & CYBER PLANET CURVATURE (34.5s - 39.5s) - GRETA LEE
                // Camera glides above the curved cybernetic world with signature -11.5 deg IMAX roll
                float t = Mathf.Clamp01((time - 34.5f) / 5.0f);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                Vector3 oStart = orbitalCamStart != null ? orbitalCamStart.position : (orbitalEnvironmentRoot != null ? orbitalEnvironmentRoot.TransformPoint(new Vector3(0f, 95f, -145f)) : new Vector3(0f, 295f, -565f));
                Vector3 oEnd = orbitalCamEnd != null ? orbitalCamEnd.position : (orbitalEnvironmentRoot != null ? orbitalEnvironmentRoot.TransformPoint(new Vector3(0f, 82f, -120f)) : new Vector3(0f, 282f, -540f));
                camPos = Vector3.Lerp(oStart, oEnd, smoothT);

                Vector3 lookTarget = orbitalEnvironmentRoot != null
                    ? orbitalEnvironmentRoot.TransformPoint(new Vector3(0f, 40f, 50f))
                    : new Vector3(0f, 240f, -370f);
                camRot = Quaternion.LookRotation((lookTarget - camPos).normalized, Vector3.up);
                camRot *= Quaternion.Euler(0f, 0f, -11.5f + smoothT * 1.5f); // Signature diagonal IMAX planetary tilt
                fov = Mathf.Lerp(40f, 36f, smoothT);
                return true;
            }
            else if (time < 43.5f)
            {
                // 3. ORBITAL PLUNGE & RADIANT ENERGY APERTURE (39.5s - 43.5s) - EVAN PETERS
                // Steep orbital dive plunging directly into the radiating caldera aperture
                float t = Mathf.Clamp01((time - 39.5f) / 4.0f);
                float diveProgress = Mathf.Pow(t, 0.75f);
                float diveT = Mathf.SmoothStep(0f, 1f, diveProgress);

                Vector3 dStart = diveCamStart != null ? diveCamStart.position : (orbitalEnvironmentRoot != null ? orbitalEnvironmentRoot.TransformPoint(new Vector3(0f, 85f, -30f)) : new Vector3(0f, 285f, -450f));
                Vector3 dEnd = diveCamEnd != null ? diveCamEnd.position : (orbitalEnvironmentRoot != null ? orbitalEnvironmentRoot.TransformPoint(new Vector3(0f, 46f, 34f)) : new Vector3(0f, 246f, -386f));
                camPos = Vector3.Lerp(dStart, dEnd, diveT);

                Vector3 target = radiantCalderaRoot != null 
                    ? radiantCalderaRoot.position 
                    : (orbitalEnvironmentRoot != null ? orbitalEnvironmentRoot.TransformPoint(new Vector3(0f, 38f, 50f)) : Vector3.zero);
                camRot = Quaternion.LookRotation((target - camPos).normalized, Vector3.up);
                camRot *= Quaternion.Euler(Mathf.Lerp(-4.0f, -1.0f, diveT), 0f, Mathf.Lerp(-6.0f, 0.0f, diveT));
                fov = Mathf.Lerp(42f, 34f, diveT);
                return true;
            }
            else if (time < 47.0f)
            {
                // 4A. COLOSSAL CYBERNETIC HUMAN EYE REVEAL (43.5s - 47.0s) - JODIE TURNER-SMITH
                // Full frontal almond eye framing with -10.5 deg Dutch angle roll, matching frame 0092
                float t = Mathf.Clamp01((time - 43.5f) / 3.5f);
                float eyeT = Mathf.SmoothStep(0f, 1f, t);

                Vector3 eStart = eyeCamStart != null ? eyeCamStart.position : (colossalCyberEyeRoot != null ? colossalCyberEyeRoot.TransformPoint(new Vector3(0f, 0.20f, -15.5f)) : new Vector3(0f, 50.20f, -55.5f));
                Vector3 eEnd = eyeCamEnd != null ? eyeCamEnd.position : (colossalCyberEyeRoot != null ? colossalCyberEyeRoot.TransformPoint(new Vector3(0f, 0.15f, -13.5f)) : new Vector3(0f, 50.15f, -53.5f));
                camPos = Vector3.Lerp(eStart, eEnd, eyeT);

                Vector3 lookTarget = colossalCyberEyeRoot != null 
                    ? colossalCyberEyeRoot.TransformPoint(new Vector3(0f, 0f, -1.50f))
                    : (orbitalEnvironmentRoot != null ? orbitalEnvironmentRoot.TransformPoint(new Vector3(0f, 50f, -41.5f)) : Vector3.zero);
                camRot = Quaternion.LookRotation((lookTarget - camPos).normalized, Vector3.up);
                camRot *= Quaternion.Euler(0f, 0f, Mathf.Lerp(-10.5f, -8.0f, eyeT));
                fov = Mathf.Lerp(36f, 33f, eyeT);
                return true;
            }
            else if (time < 48.5f)
            {
                // 4B. DYNAMIC TILTED EYE PUSH-IN (47.0s - 48.5s) - HASAN MINHAJ
                // Camera tilts diagonally and pushes close to the iris right before the detonation
                float t = Mathf.Clamp01((time - 47.0f) / 1.5f);
                float tiltT = Mathf.SmoothStep(0f, 1f, t);

                Vector3 eStart = eyeCamEnd != null ? eyeCamEnd.position : (colossalCyberEyeRoot != null ? colossalCyberEyeRoot.TransformPoint(new Vector3(0f, 0.15f, -13.5f)) : new Vector3(0f, 50.15f, -53.5f));
                Vector3 eClose = eyeCamClose != null ? eyeCamClose.position : (colossalCyberEyeRoot != null ? colossalCyberEyeRoot.TransformPoint(new Vector3(0.35f, -0.10f, -8.2f)) : new Vector3(0.35f, 49.90f, -48.2f));
                camPos = Vector3.Lerp(eStart, eClose, tiltT);

                Vector3 lookTarget = colossalCyberEyeRoot != null 
                    ? colossalCyberEyeRoot.TransformPoint(new Vector3(0f, 0f, -1.50f))
                    : (orbitalEnvironmentRoot != null ? orbitalEnvironmentRoot.TransformPoint(new Vector3(0f, 50f, -41.5f)) : Vector3.zero);
                camRot = Quaternion.LookRotation((lookTarget - camPos).normalized, Vector3.up);
                camRot *= Quaternion.Euler(0f, 0f, Mathf.Lerp(-8.0f, -22.5f, tiltT));
                fov = Mathf.Lerp(33f, 29f, tiltT);
                return true;
            }
            else if (time < 50.0f)
            {
                // 4C. BRIGHT RED EXPLOSION IN EYE & SURGE INTO BURSTING VOID (48.5s - 50.0s)
                float t = Mathf.Clamp01((time - 48.5f) / 1.5f);
                float surgeT = Mathf.Pow(t, 0.72f);

                Vector3 eClose = eyeCamClose != null ? eyeCamClose.position : (colossalCyberEyeRoot != null ? colossalCyberEyeRoot.TransformPoint(new Vector3(0.35f, -0.10f, -8.2f)) : new Vector3(0.35f, 49.90f, -48.2f));
                Vector3 eThrough = colossalCyberEyeRoot != null ? colossalCyberEyeRoot.TransformPoint(new Vector3(0.05f, -0.02f, -4.5f)) : new Vector3(0.05f, 49.98f, -44.5f);
                camPos = Vector3.Lerp(eClose, eThrough, surgeT);

                Vector3 target = colossalCyberEyeRoot != null ? colossalCyberEyeRoot.TransformPoint(new Vector3(0f, 0f, -1.50f)) : new Vector3(0f, 50f, -41.5f);
                camRot = Quaternion.LookRotation((target - camPos).normalized, Vector3.up);
                camRot *= Quaternion.Euler(0f, 0f, Mathf.Lerp(-22.5f, -6.0f, surgeT));
                fov = Mathf.Lerp(29f, 45f, surgeT);
                return true;
            }
            else if (time < 54.0f)
            {
                // 5. CRANIAL ARMOR SHELL & LATERAL CONDUIT TRACKING (50.0s - 54.0s) - ARTURO CASTRO
                // Matches Frame 0104: Close lateral tracking shot along the dual horizontal temple conduits
                float t = Mathf.Clamp01((time - 50.0f) / 4.0f);
                float sweepT = Mathf.SmoothStep(0f, 1f, t);

                Vector3 cBase = reconstructingChassisRoot != null ? reconstructingChassisRoot.position : new Vector3(0f, 32f, -40f);
                Vector3 cStart = cBase + new Vector3(-1.85f, 0.20f, -0.6f);
                Vector3 cEnd = cBase + new Vector3(-1.45f, 0.22f, 0.8f);
                camPos = Vector3.Lerp(cStart, cEnd, sweepT);

                Vector3 target = cBase + new Vector3(0.3f, 0.18f, 1.2f);
                camRot = Quaternion.LookRotation((target - camPos).normalized, Vector3.up);
                camRot *= Quaternion.Euler(0f, 0f, -6.5f); // Signature diagonal tilt
                fov = Mathf.Lerp(34f, 30f, sweepT);
                return true;
            }
            else if (time < 58.5f)
            {
                // 6. ELEVATED SYMMETRICAL RECONSTRUCTION OF HELMET & SHOULDERS (54.0s - 58.5s) - WITH GILLIAN ANDERSON
                // Matches Frame 0112: Symmetrical rear perspective showing the helmet crest, rear skull contours, and outward diagonal shoulder beams
                float t = Mathf.Clamp01((time - 54.0f) / 4.5f);
                float elevT = Mathf.SmoothStep(0f, 1f, t);

                Vector3 cBase = reconstructingChassisRoot != null ? reconstructingChassisRoot.position : new Vector3(0f, 32f, -40f);
                Vector3 hStart = cBase + new Vector3(0f, 0.55f, -4.6f);
                Vector3 hEnd = cBase + new Vector3(0f, 0.40f, -3.8f);
                camPos = Vector3.Lerp(hStart, hEnd, elevT);

                Vector3 target = cBase + new Vector3(0f, -0.1f, 0.2f);
                camRot = Quaternion.LookRotation((target - camPos).normalized, Vector3.up);
                fov = Mathf.Lerp(38f, 34f, elevT);
                return true;
            }
            else
            {
                // 7. SPINE & SHOULDER MOLTEN NANITE CRYSTALLIZATION (58.5s - 64.0s) - AND JEFF BRIDGES
                // Matches Frame 0120: Push-in closer along the cervical spine, base of the skull, and molten nanites crystallizing across shoulders
                float t = Mathf.Clamp01((time - 58.5f) / 5.5f);
                float spineT = Mathf.SmoothStep(0f, 1f, t);

                Vector3 cBase = reconstructingChassisRoot != null ? reconstructingChassisRoot.position : new Vector3(0f, 32f, -40f);
                Vector3 sStart = cBase + new Vector3(0f, 0.40f, -3.8f);
                Vector3 sEnd = cBase + new Vector3(0.12f, 0.28f, -2.8f);
                camPos = Vector3.Lerp(sStart, sEnd, spineT);

                Vector3 target = cBase + new Vector3(0f, -0.2f, 0.6f);
                camRot = Quaternion.LookRotation((target - camPos).normalized, Vector3.up);
                camRot *= Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, 4.0f, spineT));
                fov = Mathf.Lerp(34f, 28f, spineT);
                return true;
            }
        }
    }
}
