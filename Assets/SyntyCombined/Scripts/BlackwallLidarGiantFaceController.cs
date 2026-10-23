using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Synty.Sidekick.Facial;

namespace SyntyCombined
{
    /// <summary>
    /// Controls the Colossal Female LiDAR Face projected from the Blackwall in Cyberspace.
    /// Features:
    /// - 16-meter-tall female face/bust materializing directly out of the 200m Blackwall LiDAR plane.
    /// - Authentic Cyberpunk 2077 Blackwall LiDAR dot matrix with audio-reactive Z-billow and scanline glitches.
    /// - Dynamic procedural and blendshape speech lip sync (jaw drop, lip phonemes, brow expression, eye blinks).
    /// - Piercing cyan eye sensor point lights casting dynamic lighting onto Alita and the reflective salt-lake floor.
    /// - Intelligent Look-At tracking towards Alita on the cyberspace platform below.
    /// - Smooth volumetric emergence and dissolution into the Blackwall.
    /// </summary>
    public class BlackwallLidarGiantFaceController : MonoBehaviour
    {
        public static BlackwallLidarGiantFaceController Instance { get; private set; }

        [Header("Target & Transform Settings")]
        [Tooltip("Transform of Alita/Player on the mirror floor below.")]
        public Transform playerTarget;
        [Tooltip("Root Transform of the giant female head.")]
        public Transform headRoot;
        [Tooltip("Resting forward position when fully emerged out of the Blackwall (Z ~ 69.5m).")]
        public Vector3 emergedLocalPosition = new Vector3(0f, 13.0f, 69.5f);
        [Tooltip("Dormant position embedded flat within the 200m Blackwall barrier plane (Z ~ 75.2m).")]
        public Vector3 dormantLocalPosition = new Vector3(0f, 13.0f, 75.2f);
        [Tooltip("Target scale of the giant female face (12 to 14 meters tall, framed inside 2.39:1 letterbox).")]
        public Vector3 emergedScale = new Vector3(35.0f, 35.0f, 35.0f);

        [Header("Renderers & Materials")]
        public Renderer[] faceRenderers;
        public Material lidarFaceMaterial;

        [Header("Facial Blendshapes & Animation")]
        public SkinnedMeshRenderer skinnedHeadRenderer;
        public SyntySidekickFacialAnimator sidekickFacialAnimator;
        public bool isSpeaking = false;
        [Range(0f, 1f)] public float speechAmplitude = 0f;

        [Header("Dynamic Cyberspace Lighting")]
        public Light eyeLightLeft;
        public Light eyeLightRight;
        public Light auraLight;
        [ColorUsage(true, true)] public Color eyeCyanColor = new Color(0.0f, 0.95f, 1.0f);
        [ColorUsage(true, true)] public Color auraCrimsonColor = new Color(1.0f, 0.03f, 0.15f);

        [Header("Audio & Voice Resonance")]
        public AudioSource entityVoiceAudioSource;
        public AudioClip emergenceDroneClip;
        public AudioClip whisperEchoClip;

        [Header("State")]
        public bool isEmerged = false;
        public bool isTransitioning = false;
        [Range(0f, 1f)] public float dissolveAmount = 0f;

        // Blendshape cache
        private int _jawOpenIndex = -1;
        private int _mouthFunnelIndex = -1;
        private int _mouthSmileIndex = -1;
        private int _browInnerUpIndex = -1;
        private int _eyeBlinkLeftIndex = -1;
        private int _eyeBlinkRightIndex = -1;

        // Tracking & Look-At
        private Quaternion _baseRotation;
        private float _blinkTimer = 3.0f;
        private float _blinkWeight = 0f;

        // Shader Property IDs
        private static readonly int DissolveAmountID = Shader.PropertyToID("_DissolveAmount");
        private static readonly int VoiceAudioAmpID = Shader.PropertyToID("_VoiceAudioAmp");
        private static readonly int JawDropID = Shader.PropertyToID("_JawDrop");
        private static readonly int GlitchIntensityID = Shader.PropertyToID("_GlitchIntensity");

        private void Awake()
        {
            Instance = this;
            if (headRoot == null) headRoot = transform;
            _baseRotation = headRoot.localRotation;

            EnsureComponentsAndBlendshapes();
            SetDissolveImmediate(0f);
        }

        private void Start()
        {
            FindPlayer();
            EnsureComponentsAndBlendshapes();
        }

        public void FindPlayer()
        {
            if (playerTarget != null) return;
            var p = GameObject.FindWithTag("Player");
            if (p != null) { playerTarget = p.transform; return; }
            var hero = GameObject.Find("Alita_BattleAngel_Hero");
            if (hero != null) { playerTarget = hero.transform; return; }
            var allCtrl = Object.FindObjectsByType<InsaneGun.Combat.InsaneGunSwordController>(FindObjectsInactive.Exclude);
            foreach (var c in allCtrl)
            {
                if (c.controlMode == InsaneGun.Combat.InsaneGunSwordController.ControlMode.PlayerControlled)
                {
                    playerTarget = c.transform;
                    break;
                }
            }
        }

        public void EnsureComponentsAndBlendshapes()
        {
            if (faceRenderers == null || faceRenderers.Length == 0)
            {
                faceRenderers = GetComponentsInChildren<Renderer>(true);
            }

            if (skinnedHeadRenderer == null)
            {
                skinnedHeadRenderer = GetComponentInChildren<SkinnedMeshRenderer>(true);
            }

            if (skinnedHeadRenderer != null && skinnedHeadRenderer.sharedMesh != null)
            {
                Mesh m = skinnedHeadRenderer.sharedMesh;
                for (int i = 0; i < m.blendShapeCount; i++)
                {
                    string bName = m.GetBlendShapeName(i).ToLowerInvariant();
                    if (_jawOpenIndex < 0 && (bName.Contains("jawopen") || bName.Contains("mouthopen"))) _jawOpenIndex = i;
                    if (_mouthFunnelIndex < 0 && (bName.Contains("mouthfunnel") || bName.Contains("moutho"))) _mouthFunnelIndex = i;
                    if (_mouthSmileIndex < 0 && (bName.Contains("mouthsmile") || bName.Contains("smile"))) _mouthSmileIndex = i;
                    if (_browInnerUpIndex < 0 && (bName.Contains("browinnerup") || bName.Contains("browup"))) _browInnerUpIndex = i;
                    if (_eyeBlinkLeftIndex < 0 && (bName.Contains("eyeblinkleft") || bName.Contains("blink_l"))) _eyeBlinkLeftIndex = i;
                    if (_eyeBlinkRightIndex < 0 && (bName.Contains("eyeblinkright") || bName.Contains("blink_r"))) _eyeBlinkRightIndex = i;
                }
            }

            if (sidekickFacialAnimator == null)
            {
                sidekickFacialAnimator = GetComponentInChildren<SyntySidekickFacialAnimator>(true);
            }

            // Ensure AudioSource & Cybernetic Filters
            if (entityVoiceAudioSource == null)
            {
                entityVoiceAudioSource = GetComponent<AudioSource>();
                if (entityVoiceAudioSource == null) entityVoiceAudioSource = gameObject.AddComponent<AudioSource>();
            }
            entityVoiceAudioSource.spatialBlend = 0.25f;
            entityVoiceAudioSource.minDistance = 15.0f;
            entityVoiceAudioSource.maxDistance = 150.0f;
            entityVoiceAudioSource.playOnAwake = false;

            // Cybernetic Old Net acoustic resonance filters
            var echo = entityVoiceAudioSource.GetComponent<AudioEchoFilter>();
            if (echo == null) echo = entityVoiceAudioSource.gameObject.AddComponent<AudioEchoFilter>();
            echo.delay = 75f;
            echo.decayRatio = 0.32f;
            echo.wetMix = 0.38f;
            echo.dryMix = 0.88f;

            var distortion = entityVoiceAudioSource.GetComponent<AudioDistortionFilter>();
            if (distortion == null) distortion = entityVoiceAudioSource.gameObject.AddComponent<AudioDistortionFilter>();
            distortion.distortionLevel = 0.05f;

            // Ensure Eye Lights
            EnsureEyeLights();
        }

        private void EnsureEyeLights()
        {
            if (eyeLightLeft == null)
            {
                var lGO = new GameObject("Light_Eye_Left");
                lGO.transform.SetParent(headRoot, false);
                lGO.transform.localPosition = new Vector3(-0.0410f, 0.0397f, 0.035f);
                eyeLightLeft = lGO.AddComponent<Light>();
                eyeLightLeft.type = LightType.Point;
                eyeLightLeft.color = eyeCyanColor;
                eyeLightLeft.intensity = 0f;
                eyeLightLeft.range = 35.0f;
                eyeLightLeft.shadows = LightShadows.None;
            }

            if (eyeLightRight == null)
            {
                var rGO = new GameObject("Light_Eye_Right");
                rGO.transform.SetParent(headRoot, false);
                rGO.transform.localPosition = new Vector3(0.0410f, 0.0397f, 0.035f);
                eyeLightRight = rGO.AddComponent<Light>();
                eyeLightRight.type = LightType.Point;
                eyeLightRight.color = eyeCyanColor;
                eyeLightRight.intensity = 0f;
                eyeLightRight.range = 35.0f;
                eyeLightRight.shadows = LightShadows.None;
            }

            if (auraLight == null)
            {
                var aGO = new GameObject("Light_Blackwall_Aura");
                aGO.transform.SetParent(headRoot, false);
                aGO.transform.localPosition = new Vector3(0f, 0f, 0.15f);
                auraLight = aGO.AddComponent<Light>();
                auraLight.type = LightType.Point;
                auraLight.color = auraCrimsonColor;
                auraLight.intensity = 0f;
                auraLight.range = 45.0f;
                auraLight.shadows = LightShadows.None;
            }
        }

        private void Update()
        {
            if (playerTarget == null) FindPlayer();

            // 1. Head Look-At tracking towards Alita on the cyberspace platform
            if (isEmerged && playerTarget != null)
            {
                Vector3 toPlayer = (playerTarget.position - headRoot.position).normalized;
                if (toPlayer.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(toPlayer, Vector3.up);
                    headRoot.rotation = Quaternion.Slerp(headRoot.rotation, targetRot, Time.deltaTime * 1.8f);
                }
            }

            // 2. Speech & Lip Sync Animation
            UpdateSpeechLipSync();

            // 3. Eye Blinking & Micro-gestures
            UpdateEyeBlinking();
        }

        private void UpdateSpeechLipSync()
        {
            // Sample live audio output data from playing voice clip for perfect syllable lip sync
            if (entityVoiceAudioSource != null && entityVoiceAudioSource.isPlaying)
            {
                float[] samples = new float[64];
                entityVoiceAudioSource.GetOutputData(samples, 0);
                float sum = 0f;
                for (int i = 0; i < samples.Length; i++) sum += samples[i] * samples[i];
                float rms = Mathf.Sqrt(sum / samples.Length);
                if (rms > 0.003f)
                {
                    speechAmplitude = Mathf.Clamp01(rms * 5.8f);
                    isSpeaking = true;
                }
                else
                {
                    speechAmplitude = Mathf.MoveTowards(speechAmplitude, 0f, Time.deltaTime * 6f);
                }
            }
            else
            {
                isSpeaking = false;
                speechAmplitude = Mathf.MoveTowards(speechAmplitude, 0f, Time.deltaTime * 10f);
            }

            float targetAmp = isSpeaking ? speechAmplitude : 0f;
            float currentAmp = Mathf.MoveTowards(GetMaterialFloat(VoiceAudioAmpID), targetAmp, Time.deltaTime * 14f);
            SetMaterialFloat(VoiceAudioAmpID, currentAmp);

            // Procedural Jaw Drop for Shader
            float jawWeight = currentAmp;
            SetMaterialFloat(JawDropID, jawWeight);

            // Blendshapes (if SkinnedMeshRenderer is available)
            if (skinnedHeadRenderer != null)
            {
                float blendJaw = jawWeight * 85.0f;
                if (_jawOpenIndex >= 0) skinnedHeadRenderer.SetBlendShapeWeight(_jawOpenIndex, blendJaw);
                if (_mouthFunnelIndex >= 0) skinnedHeadRenderer.SetBlendShapeWeight(_mouthFunnelIndex, blendJaw * 0.45f);
                if (_mouthSmileIndex >= 0) skinnedHeadRenderer.SetBlendShapeWeight(_mouthSmileIndex, 15.0f + Mathf.Sin(Time.time * 2f) * 8f);
                if (_browInnerUpIndex >= 0) skinnedHeadRenderer.SetBlendShapeWeight(_browInnerUpIndex, 30.0f + currentAmp * 25.0f);
            }

            // Subtle eye light pulse during speech
            if (isEmerged)
            {
                float eyeIntensity = Mathf.Lerp(4.0f, 8.5f, currentAmp);
                if (eyeLightLeft != null) eyeLightLeft.intensity = eyeIntensity;
                if (eyeLightRight != null) eyeLightRight.intensity = eyeIntensity;
            }
        }

        private void UpdateEyeBlinking()
        {
            if (!isEmerged) return;

            _blinkTimer -= Time.deltaTime;
            if (_blinkTimer <= 0f)
            {
                _blinkTimer = Random.Range(3.5f, 7.5f);
                StartCoroutine(BlinkRoutine());
            }

            if (skinnedHeadRenderer != null)
            {
                if (_eyeBlinkLeftIndex >= 0) skinnedHeadRenderer.SetBlendShapeWeight(_eyeBlinkLeftIndex, _blinkWeight);
                if (_eyeBlinkRightIndex >= 0) skinnedHeadRenderer.SetBlendShapeWeight(_eyeBlinkRightIndex, _blinkWeight);
            }
        }

        private IEnumerator BlinkRoutine()
        {
            float duration = 0.16f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                _blinkWeight = Mathf.Sin((elapsed / duration) * Mathf.PI) * 95f;
                yield return null;
            }
            _blinkWeight = 0f;
        }

        /// <summary>
        /// Materializes the giant female LiDAR face out of the 200m Blackwall plane.
        /// </summary>
        public void Emerge(float duration = 3.5f)
        {
            if (isEmerged || isTransitioning) return;
            StartCoroutine(EmergeRoutine(duration));
        }

        private IEnumerator EmergeRoutine(float duration)
        {
            isTransitioning = true;
            gameObject.SetActive(true);

            headRoot.localPosition = dormantLocalPosition;
            headRoot.localScale = Vector3.zero;
            SetDissolveImmediate(0f);

            if (entityVoiceAudioSource != null && emergenceDroneClip != null)
            {
                entityVoiceAudioSource.PlayOneShot(emergenceDroneClip, 0.85f);
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                headRoot.localPosition = Vector3.Lerp(dormantLocalPosition, emergedLocalPosition, smoothT);
                headRoot.localScale = Vector3.Lerp(Vector3.zero, emergedScale, smoothT);
                SetDissolveImmediate(smoothT);

                // Ramp up lights
                if (eyeLightLeft != null) eyeLightLeft.intensity = Mathf.Lerp(0f, 5.0f, smoothT);
                if (eyeLightRight != null) eyeLightRight.intensity = Mathf.Lerp(0f, 5.0f, smoothT);
                if (auraLight != null) auraLight.intensity = Mathf.Lerp(0f, 8.0f, smoothT);

                yield return null;
            }

            headRoot.localPosition = emergedLocalPosition;
            headRoot.localScale = emergedScale;
            SetDissolveImmediate(1.0f);

            isEmerged = true;
            isTransitioning = false;
        }

        /// <summary>
        /// Dissolves the giant face back into the 200m Blackwall LiDAR grid.
        /// </summary>
        public void DissolveBack(float duration = 3.0f)
        {
            if (!isEmerged || isTransitioning) return;
            StartCoroutine(DissolveRoutine(duration));
        }

        private IEnumerator DissolveRoutine(float duration)
        {
            isTransitioning = true;
            isSpeaking = false;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float smoothT = 1.0f - Mathf.SmoothStep(0f, 1f, t);

                headRoot.localPosition = Vector3.Lerp(dormantLocalPosition, emergedLocalPosition, smoothT);
                headRoot.localScale = Vector3.Lerp(Vector3.zero, emergedScale, smoothT);
                SetDissolveImmediate(smoothT);

                if (eyeLightLeft != null) eyeLightLeft.intensity = Mathf.Lerp(0f, 5.0f, smoothT);
                if (eyeLightRight != null) eyeLightRight.intensity = Mathf.Lerp(0f, 5.0f, smoothT);
                if (auraLight != null) auraLight.intensity = Mathf.Lerp(0f, 8.0f, smoothT);

                yield return null;
            }

            SetDissolveImmediate(0f);
            headRoot.localScale = Vector3.zero;
            isEmerged = false;
            isTransitioning = false;
            gameObject.SetActive(false);
        }

        public void SetSpeakingState(bool speaking, float amplitude)
        {
            isSpeaking = speaking;
            speechAmplitude = amplitude;
        }

        public void SetDissolveImmediate(float amount)
        {
            dissolveAmount = amount;
            SetMaterialFloat(DissolveAmountID, amount);
        }

        private void SetMaterialFloat(int propID, float val)
        {
            if (faceRenderers == null) return;
            for (int i = 0; i < faceRenderers.Length; i++)
            {
                if (faceRenderers[i] != null)
                {
                    faceRenderers[i].material.SetFloat(propID, val);
                }
            }
        }

        private float GetMaterialFloat(int propID)
        {
            if (faceRenderers != null && faceRenderers.Length > 0 && faceRenderers[0] != null)
            {
                return faceRenderers[0].material.GetFloat(propID);
            }
            return 0f;
        }
    }
}
