using System.Collections;
using UnityEngine;
using UnityEngine.Events;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SyntyCombined.Cutscene
{
    /// <summary>
    /// Frame-accurate recreation of the Astropulse: Reincarnation (3:23 - 3:28) Cinematic Reveal.
    /// Orchestrates:
    /// 1. Initial 0.25s blackout reset transition
    /// 2. Low-angle OTS silhouette framing against the cosmic nebula
    /// 3. Smooth parallax arc orbital sweep (counter-clockwise around hooded cowl)
    /// 4. Golden cybernetic mask emergence from deep cowl shadow
    /// 5. Intensely glowing violet oculus eye with breathing pulse & anamorphic flare
    /// 6. Synchronized low-frequency celestial drone soundscape
    /// 7. 2.39:1 widescreen letterbox presentation
    /// </summary>
    [ExecuteAlways]
    public class CelestialVisionCutsceneDirector : MonoBehaviour
    {
        public static CelestialVisionCutsceneDirector Instance { get; private set; }

        [Header("Playback Timing")]
        [Tooltip("Exact duration matching the reference clip (3:23 - 3:28)")]
        [Range(2.0f, 10.0f)] public float duration = 4.8f;
        [Tooltip("Retinal reset blackout duration at the very start")]
        [Range(0f, 1.0f)] public float blackoutDuration = 0.25f;
        public bool playOnStart = true;
        public bool loopInPreview = false;

        [Header("Actor & Focal Point References")]
        [Tooltip("Root transform of the Celestial Overseer (Hooded entity)")]
        public Transform overseerEntity;
        [Tooltip("Look target (Golden mask / bridge of the brow)")]
        public Transform lookTarget;
        [Tooltip("Point light embedded inside the eye socket")]
        public Light oculusPointLight;
        [Tooltip("Horizontal anamorphic streak sprite / flare transform")]
        public Transform anamorphicFlareTransform;

        [Header("Camera Rig")]
        public Camera cinematicCamera;
        [Tooltip("Starting Field of View (Wide OTS)")]
        public float startFOV = 42.0f;
        [Tooltip("Ending Field of View (Close-up Telephoto compression)")]
        public float endFOV = 32.0f;
        [Tooltip("Subtle handheld camera breathing vibration")]
        public float cameraJitterAmount = 0.008f;

        [Header("Orbit Keyframes (Relative to Overseer)")]
        [Tooltip("Start position: low-angle OTS behind right shoulder")]
        public Vector3 localStartOffset = new Vector3(0.55f, 1.42f, -1.25f);
        [Tooltip("Mid position: side profile arc clearing the cowl rim")]
        public Vector3 localMidOffset = new Vector3(0.95f, 1.52f, -0.20f);
        [Tooltip("End position: frontal 3/4 close-up")]
        public Vector3 localEndOffset = new Vector3(0.32f, 1.58f, 0.82f);

        [Header("Oculus Eye Glow Settings")]
        public Color oculusColor = new Color(0.66f, 0.33f, 0.98f, 1.0f); // Vibrant violet
        public float baseOculusIntensity = 4.0f;
        public float peakOculusIntensity = 14.0f;
        public float oculusPulseSpeed = 8.5f;

        [Header("Audio")]
        public AudioClip celestialDroneClip;
        [Range(0f, 1f)] public float audioVolume = 0.9f;
        private AudioSource _audioSource;

        [Header("Events")]
        public UnityEvent onCutsceneStart;
        public UnityEvent onCutsceneComplete;

        [Header("Live State")]
        [Range(0f, 1f)] public float normalizedTime = 0f;
        public bool isPlaying = false;

        // Internal presentation state
        private Texture2D _blackTexture;
        private float _currentBlackoutAlpha = 0f;
        private Coroutine _playbackRoutine;

        private void Awake()
        {
            if (Application.isPlaying)
            {
                Instance = this;
            }
            InitBlackTexture();
        }

        private void Start()
        {
            if (Application.isPlaying && playOnStart)
            {
                Play();
            }
        }

        private void InitBlackTexture()
        {
            if (_blackTexture == null)
            {
                _blackTexture = new Texture2D(1, 1);
                _blackTexture.SetPixel(0, 0, Color.black);
                _blackTexture.Apply();
            }
        }

        public void Play()
        {
            if (_playbackRoutine != null)
            {
                StopCoroutine(_playbackRoutine);
            }

            if (Application.isPlaying)
            {
                _playbackRoutine = StartCoroutine(PlaybackCoroutine());
            }
            else
            {
                normalizedTime = 0f;
                isPlaying = true;
            }
        }

        public void Stop()
        {
            if (_playbackRoutine != null)
            {
                StopCoroutine(_playbackRoutine);
                _playbackRoutine = null;
            }
            isPlaying = false;
        }

        private IEnumerator PlaybackCoroutine()
        {
            isPlaying = true;
            normalizedTime = 0f;

            // Setup audio
            if (celestialDroneClip != null)
            {
                if (_audioSource == null)
                {
                    _audioSource = gameObject.AddComponent<AudioSource>();
                    _audioSource.playOnAwake = false;
                    _audioSource.spatialBlend = 0f; // 2D Full stereo immersion
                }
                _audioSource.clip = celestialDroneClip;
                _audioSource.volume = audioVolume;
                _audioSource.Play();
            }

            onCutsceneStart?.Invoke();

            // Retinal Blackout Beat
            float elapsed = 0f;
            _currentBlackoutAlpha = 1f;

            while (elapsed < blackoutDuration)
            {
                elapsed += Time.deltaTime;
                _currentBlackoutAlpha = 1f - Mathf.Clamp01(elapsed / blackoutDuration);
                Evaluate(0f);
                yield return null;
            }
            _currentBlackoutAlpha = 0f;

            // Main Cinematic Arc Orbit
            float cutsceneTimer = 0f;
            while (cutsceneTimer < duration)
            {
                cutsceneTimer += Time.deltaTime;
                normalizedTime = Mathf.Clamp01(cutsceneTimer / duration);
                Evaluate(normalizedTime);
                yield return null;
            }

            normalizedTime = 1f;
            Evaluate(1f);
            isPlaying = false;
            onCutsceneComplete?.Invoke();

            if (loopInPreview && Application.isPlaying)
            {
                yield return new WaitForSeconds(1.0f);
                Play();
            }
        }

        /// <summary>
        /// Evaluates camera position, orientation, FOV, and oculus lighting at normalized time t [0..1]
        /// </summary>
        public void Evaluate(float t)
        {
            Transform root = overseerEntity != null ? overseerEntity : transform;
            Vector3 targetFocus = lookTarget != null ? lookTarget.position : root.position + Vector3.up * 1.55f;

            // 1. Orbital Arc Position (Quadratic Bézier Curve in Local Space)
            float curvedT = Mathf.SmoothStep(0f, 1f, t);
            Vector3 localPos = EvaluateBezier(localStartOffset, localMidOffset, localEndOffset, curvedT);
            Vector3 worldPos = root.TransformPoint(localPos);

            // 2. Camera Handheld Micro-Breathing Vibration
            if (cameraJitterAmount > 0f)
            {
                float timeSeed = (Application.isPlaying ? Time.time : (float)EditorApplication.timeSinceStartup) * 1.5f;
                float jitterX = (Mathf.PerlinNoise(timeSeed, 0f) - 0.5f) * cameraJitterAmount;
                float jitterY = (Mathf.PerlinNoise(0f, timeSeed) - 0.5f) * cameraJitterAmount;
                worldPos += root.right * jitterX + root.up * jitterY;
            }

            // 3. Apply Camera Transform
            if (cinematicCamera != null)
            {
                cinematicCamera.transform.position = worldPos;

                // Dynamic look target: early frames look slightly forward past the shoulder;
                // as camera swings around, focus locks tightly onto the golden mask
                Vector3 forwardPastShoulder = root.position + root.forward * 4.0f + Vector3.up * 1.5f;
                Vector3 currentFocus = Vector3.Lerp(forwardPastShoulder, targetFocus, Mathf.SmoothStep(0.2f, 0.75f, t));

                // Calculate look rotation
                Quaternion targetRot = Quaternion.LookRotation((currentFocus - worldPos).normalized, Vector3.up);

                // Subtle Dutch tilt: slight roll during the OTS that levels out at frontal reveal
                float dutchRoll = Mathf.Lerp(2.5f, 0.0f, Mathf.Clamp01(t * 1.5f));
                targetRot *= Quaternion.Euler(0f, 0f, dutchRoll);

                cinematicCamera.transform.rotation = targetRot;
                cinematicCamera.fieldOfView = Mathf.Lerp(startFOV, endFOV, curvedT);
            }

            // 4. Oculus Eye & Anamorphic Flare Modulation
            float oculusFactor = Mathf.Clamp01((t - 0.35f) / 0.65f);
            float pulse = 0.85f + 0.15f * Mathf.Sin((Application.isPlaying ? Time.time : (float)EditorApplication.timeSinceStartup) * oculusPulseSpeed);
            float currentIntensity = Mathf.Lerp(baseOculusIntensity, peakOculusIntensity, oculusFactor) * pulse;

            if (oculusPointLight != null)
            {
                oculusPointLight.color = oculusColor;
                oculusPointLight.intensity = currentIntensity;
            }

            if (anamorphicFlareTransform != null)
            {
                float flareScale = Mathf.Lerp(0.2f, 1.2f, oculusFactor) * pulse;
                anamorphicFlareTransform.localScale = new Vector3(flareScale * 2.8f, flareScale * 0.15f, flareScale);
            }
        }

        private Vector3 EvaluateBezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
        {
            float u = 1f - t;
            return u * u * p0 + 2f * u * t * p1 + t * t * p2;
        }

        private void OnGUI()
        {
            InitBlackTexture();

            // 1. Fullscreen Retinal Blackout Fade
            if (_currentBlackoutAlpha > 0.001f)
            {
                Color prevColor = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, _currentBlackoutAlpha);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _blackTexture);
                GUI.color = prevColor;
            }

            // 2. Cinematic 2.39:1 Anamorphic Widescreen Letterbox Bars
            float targetAspect = 2.39f;
            float currentAspect = (float)Screen.width / Screen.height;

            if (currentAspect < targetAspect)
            {
                float targetHeight = Screen.width / targetAspect;
                float barHeight = (Screen.height - targetHeight) * 0.5f;

                Color prevColor = GUI.color;
                GUI.color = Color.black;
                GUI.DrawTexture(new Rect(0, 0, Screen.width, barHeight), _blackTexture);
                GUI.DrawTexture(new Rect(0, Screen.height - barHeight, Screen.width, barHeight), _blackTexture);
                GUI.color = prevColor;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Transform root = overseerEntity != null ? overseerEntity : transform;
            Vector3 p0 = root.TransformPoint(localStartOffset);
            Vector3 p1 = root.TransformPoint(localMidOffset);
            Vector3 p2 = root.TransformPoint(localEndOffset);

            // Draw Parallax Arc Curve
            Gizmos.color = new Color(0.66f, 0.33f, 0.98f, 0.8f);
            int segments = 24;
            Vector3 prev = p0;
            for (int i = 1; i <= segments; i++)
            {
                float t = (float)i / segments;
                Vector3 curr = root.TransformPoint(EvaluateBezier(localStartOffset, localMidOffset, localEndOffset, t));
                Gizmos.DrawLine(prev, curr);
                prev = curr;
            }

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(p0, 0.06f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(p1, 0.06f);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(p2, 0.06f);

            // Draw line to look target
            if (lookTarget != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(lookTarget.position, 0.08f);
            }
        }
#endif
    }
}
