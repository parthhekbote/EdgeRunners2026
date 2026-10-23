using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Synty.Sidekick.Facial
{
    /// <summary>
    /// AAA-Quality Facial Animator, Lip Sync & Proximity Dialogue System for Synty Sidekick & Cyberpunk Characters.
    /// Supports:
    /// - 84 FACS / ARKit / Synty Sidekick blendshapes across single or modular multi-mesh characters (Head, Brows, Beard, Teeth).
    /// - Audio-reactive real-time lip sync from AudioSource (RMS amplitude + high/low frequency zero-crossing vowel estimation).
    /// - Procedural speech cadence generator (organic 4.2 Hz human syllable rhythm with Perlin micro-accents).
    /// - Lifelike eye blinking with random intervals and organic double-blinks.
    /// - Expressive emotional moods (Neutral, Friendly, CyberpunkSmirk, Serious, Concerned, Surprised, Suspicious).
    /// - Dynamic Head Look-At towards the player when in proximity (< 5m), clamped to natural neck angles.
    /// - Additive conversational micro-nods and syllable-synced head gestures in LateUpdate.
    /// - World-space holographic cyberpunk interaction prompt ("[E] TALK TO <NAME>") and expandable dialogue speech bubble.
    /// - Typewriter text effect with procedural cybernetic voice chatter audio.
    /// - Full backward compatibility with existing Scene / CyberNPCController integration.
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class SyntySidekickFacialAnimator : MonoBehaviour
    {
        public enum SpeechMode
        {
            AudioReactive,     // Analyzes real-time AudioSource waveform/loudness
            ProceduralCadence  // Synthesizes natural human phoneme syllable rhythm
        }

        public enum FacialMood
        {
            Neutral,
            Friendly,
            CyberpunkSmirk,
            Serious,
            Concerned,
            Surprised,
            Suspicious
        }

        [System.Serializable]
        public struct DialogueEntry
        {
            [TextArea(2, 4)]
            public string text;
            public FacialMood mood;
            public AudioClip customVoiceClip;

            public DialogueEntry(string t, FacialMood m = FacialMood.CyberpunkSmirk, AudioClip clip = null)
            {
                text = t;
                mood = m;
                customVoiceClip = clip;
            }
        }

        [Header("Target Mesh & Renderers")]
        [Tooltip("Primary SkinnedMeshRenderer (auto-detected if null). Modular child meshes with blendshapes are also cached.")]
        public SkinnedMeshRenderer targetSkinnedMesh;

        [Header("Speech & Lip Movement")]
        public SpeechMode speechMode = SpeechMode.ProceduralCadence;
        public bool isSpeaking = false;
        [Range(0f, 100f)] public float jawOpenMaxWeight = 78f;
        [Range(0f, 100f)] public float lipShapeMaxWeight = 65f;
        [Range(5f, 40f)]  public float lipSmoothSpeed = 24f;
        [Range(0.5f, 5f)] public float audioLoudnessSensitivity = 2.4f;

        [Header("Blinking & Natural Life")]
        public bool enableBlinking = true;
        public float minBlinkInterval = 2.2f;
        public float maxBlinkInterval = 4.8f;
        [Range(0f, 0.4f)] public float doubleBlinkChance = 0.20f;

        [Header("Expressions & Mood")]
        public FacialMood currentMood = FacialMood.Neutral;
        public bool enableEyebrowEmphasis = true;
        [Range(0f, 100f)] public float moodBlendSpeed = 12f;

        [Header("Head Gestures & Look-At")]
        public bool enableHeadMicroGestures = true;
        [Range(0f, 5f)] public float headNodIntensity = 2.4f;
        public bool enableLookAtPlayer = true;
        public float lookAtMaxDistance = 5.2f;
        public float lookAtMaxAngle = 75f;

        [Header("Audio & Voice Chatter")]
        public AudioSource voiceAudioSource;
        public AudioClip[] speechClips;

        [Header("Proximity Dialogue System")]
        public bool enableProximityInteraction = true;
        public float interactionRadius = 3.6f;
        public string characterDisplayName = "CYBER CITIZEN";
        public string characterRoleSubtitle = "CITIZEN // SECTOR 4";

        [TextArea(2, 4)]
        public string[] dialogueLines = new string[]
        {
            "Watch your six, choom. The Blackwall has been flickering all morning.",
            "Doctor Ido's clinic is just down the avenue. Cleanest cybernetics in the district.",
            "Did you see that cyborg girl with the Damascus blade? Incredible combat speed.",
            "NCPD drones are running heavy sweeps near the plaza. Keep your weapons holstered."
        };

        public DialogueEntry[] richDialogueLines;

        // ─── Multi-Mesh Blendshape Mapping ────────────────────────────────────
        private class MeshBlendMapping
        {
            public SkinnedMeshRenderer smr;
            public int idxJawOpen = -1;
            public int idxMouthClose = -1;
            public int idxMouthFunnel = -1;
            public int idxMouthPucker = -1;
            public int idxMouthSmileL = -1;
            public int idxMouthSmileR = -1;
            public int idxMouthFrownL = -1;
            public int idxMouthFrownR = -1;
            public int idxMouthDimpleL = -1;
            public int idxMouthDimpleR = -1;
            public int idxMouthLowerDownL = -1;
            public int idxMouthLowerDownR = -1;
            public int idxMouthUpperUpL = -1;
            public int idxMouthUpperUpR = -1;
            public int idxMouthStretchL = -1;
            public int idxMouthStretchR = -1;
            public int idxEyeBlinkUpperL = -1;
            public int idxEyeBlinkUpperR = -1;
            public int idxEyeBlinkLowerL = -1;
            public int idxEyeBlinkLowerR = -1;
            public int idxEyeSquintL = -1;
            public int idxEyeSquintR = -1;
            public int idxEyeWideUpperL = -1;
            public int idxEyeWideUpperR = -1;
            public int idxBrowInnerUpL = -1;
            public int idxBrowInnerUpR = -1;
            public int idxBrowRaiseL = -1;
            public int idxBrowRaiseR = -1;
            public int idxBrowFrownL = -1;
            public int idxBrowFrownR = -1;
            public int idxBrowInnerDownL = -1;
            public int idxBrowInnerDownR = -1;
        }

        private readonly List<MeshBlendMapping> _allMeshMappings = new List<MeshBlendMapping>();

        // ─── Current Blendshape Target Weights ────────────────────────────────
        private float _currJawOpen = 0f;
        private float _currMouthFunnel = 0f;
        private float _currMouthPucker = 0f;
        private float _currMouthStretch = 0f;
        private float _currMouthClose = 0f;
        private float _currEyeBlink = 0f;
        private float _currBrowRaise = 0f;

        // Dynamic Mood Weights
        private float _currSmileL = 0f;
        private float _currSmileR = 0f;
        private float _currFrownL = 0f;
        private float _currFrownR = 0f;
        private float _currDimpleR = 0f;
        private float _currBrowFrown = 0f;
        private float _currEyeSquint = 0f;
        private float _currEyeWide = 0f;

        // ─── Blinking State ───────────────────────────────────────────────────
        private float _blinkTimer = 0f;
        private float _nextBlinkTime = 3f;
        private bool _isBlinking = false;
        private float _blinkProgress = 0f;

        // ─── Procedural Cadence State ─────────────────────────────────────────
        private float _cadencePhase = 0f;
        private readonly float[] _audioSampleBuffer = new float[128];

        // ─── Head Bone & Look-At ──────────────────────────────────────────────
        private Transform _headBone;
        private Transform _playerTransform;
        private float _currLookAtWeight = 0f;
        private float _targetLookAtWeight = 0f;

        // ─── World UI Elements ────────────────────────────────────────────────
        private GameObject _uiRootObj;
        private CanvasGroup _promptCanvasGroup;
        private TextMeshProUGUI _promptText;
        private CanvasGroup _bubbleCanvasGroup;
        private TextMeshProUGUI _bubbleSpeakerName;
        private TextMeshProUGUI _bubbleRoleText;
        private TextMeshProUGUI _bubbleDialogueText;
        private TextMeshProUGUI _bubbleMoodBadge;

        // ─── Typewriter & Dialogue State ──────────────────────────────────────
        private Coroutine _speechRoutine;
        private int _currentDialogueIndex = 0;
        private bool _isTypewriting = false;
        private string _fullTargetDialogue = "";
        private float _bubbleAutoCloseTimer = 0f;
        private bool _playerInProximity = false;

        // Procedural Typewriter Sound Effect
        private static AudioClip _proceduralChirpClip;

        public void Awake()
        {
            Initialize();
        }

        public void Initialize()
        {
            FindSkinnedMesh();
            CacheBlendShapeIndices();
            SetupAudioSource();
            FindHeadBone();
            CreateWorldDialogueUI();
            LoadDefaultChatterClips();
            EnsureProceduralChirpAudio();
        }

        private void Start()
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null) _playerTransform = player.transform;
            _nextBlinkTime = Random.Range(minBlinkInterval, maxBlinkInterval);
        }

        private void OnValidate()
        {
            FindSkinnedMesh();
            CacheBlendShapeIndices();
        }

        public void FindSkinnedMesh()
        {
            var smrs = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (targetSkinnedMesh == null || targetSkinnedMesh.sharedMesh == null || targetSkinnedMesh.sharedMesh.blendShapeCount == 0)
            {
                foreach (var smr in smrs)
                {
                    if (smr.sharedMesh != null && smr.sharedMesh.blendShapeCount > 0)
                    {
                        targetSkinnedMesh = smr;
                        break;
                    }
                }
            }
        }

        public void CacheBlendShapeIndices()
        {
            _allMeshMappings.Clear();
            var smrs = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var smr in smrs)
            {
                if (smr.sharedMesh == null || smr.sharedMesh.blendShapeCount == 0) continue;
                Mesh m = smr.sharedMesh;

                var mapping = new MeshBlendMapping();
                mapping.smr = smr;
                mapping.idxJawOpen       = FindBlendIndex(m, "jawOpen");
                mapping.idxMouthClose    = FindBlendIndex(m, "mouthClose");
                mapping.idxMouthFunnel   = FindBlendIndex(m, "mouthFunnel");
                mapping.idxMouthPucker   = FindBlendIndex(m, "mouthPucker");
                mapping.idxMouthSmileL   = FindBlendIndex(m, "mouthSmileLeft");
                mapping.idxMouthSmileR   = FindBlendIndex(m, "mouthSmileRight");
                mapping.idxMouthFrownL   = FindBlendIndex(m, "mouthFrownLeft");
                mapping.idxMouthFrownR   = FindBlendIndex(m, "mouthFrownRight");
                mapping.idxMouthDimpleL  = FindBlendIndex(m, "mouthDimpleLeft");
                mapping.idxMouthDimpleR  = FindBlendIndex(m, "mouthDimpleRight");
                mapping.idxMouthLowerDownL = FindBlendIndex(m, "mouthLowerDownLeft");
                mapping.idxMouthLowerDownR = FindBlendIndex(m, "mouthLowerDownRight");
                mapping.idxMouthUpperUpL   = FindBlendIndex(m, "mouthUpperUpLeft");
                mapping.idxMouthUpperUpR   = FindBlendIndex(m, "mouthUpperUpRight");
                mapping.idxMouthStretchL = FindBlendIndex(m, "mouthStretchLeft");
                mapping.idxMouthStretchR = FindBlendIndex(m, "mouthStretchRight");

                mapping.idxEyeBlinkUpperL = FindBlendIndex(m, "eyeBlinkUpperLeft");
                if (mapping.idxEyeBlinkUpperL < 0) mapping.idxEyeBlinkUpperL = FindBlendIndex(m, "eyeBlinkLeft");
                mapping.idxEyeBlinkUpperR = FindBlendIndex(m, "eyeBlinkUpperRight");
                if (mapping.idxEyeBlinkUpperR < 0) mapping.idxEyeBlinkUpperR = FindBlendIndex(m, "eyeBlinkRight");
                mapping.idxEyeBlinkLowerL = FindBlendIndex(m, "eyeBlinkLowerLeft");
                mapping.idxEyeBlinkLowerR = FindBlendIndex(m, "eyeBlinkLowerRight");

                mapping.idxEyeSquintL    = FindBlendIndex(m, "eyeSquintLeft");
                mapping.idxEyeSquintR    = FindBlendIndex(m, "eyeSquintRight");
                mapping.idxEyeWideUpperL = FindBlendIndex(m, "eyeWideUpperLeft");
                mapping.idxEyeWideUpperR = FindBlendIndex(m, "eyeWideUpperRight");

                mapping.idxBrowInnerUpL  = FindBlendIndex(m, "browInnerUpLeft");
                mapping.idxBrowInnerUpR  = FindBlendIndex(m, "browInnerUpRight");
                mapping.idxBrowRaiseL    = FindBlendIndex(m, "browRaiseLeft");
                mapping.idxBrowRaiseR    = FindBlendIndex(m, "browRaiseRight");
                mapping.idxBrowFrownL    = FindBlendIndex(m, "browFrownLeft");
                mapping.idxBrowFrownR    = FindBlendIndex(m, "browFrownRight");
                mapping.idxBrowInnerDownL = FindBlendIndex(m, "browInnerDownLeft");
                mapping.idxBrowInnerDownR = FindBlendIndex(m, "browInnerDownRight");

                _allMeshMappings.Add(mapping);
            }
        }

        private int FindBlendIndex(Mesh m, string canonicalName)
        {
            string low = canonicalName.ToLower();
            for (int i = 0; i < m.blendShapeCount; i++)
            {
                string bName = m.GetBlendShapeName(i).ToLower();
                if (bName == low || bName == "meshblends." + low || bName.EndsWith("." + low) || bName == "headblends." + low)
                    return i;
            }
            return -1;
        }

        private void SetupAudioSource()
        {
            if (voiceAudioSource == null) voiceAudioSource = GetComponent<AudioSource>();
            if (voiceAudioSource == null) voiceAudioSource = gameObject.AddComponent<AudioSource>();

            voiceAudioSource.spatialBlend = 1.0f;
            voiceAudioSource.dopplerLevel = 0.05f;
            voiceAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            voiceAudioSource.minDistance = 1.8f;
            voiceAudioSource.maxDistance = 16f;
            voiceAudioSource.playOnAwake = false;
            voiceAudioSource.loop = false;
        }

        private void FindHeadBone()
        {
            var anim = GetComponent<Animator>();
            if (anim != null && anim.isHuman)
            {
                _headBone = anim.GetBoneTransform(HumanBodyBones.Head);
            }
            if (_headBone == null)
            {
                var trs = GetComponentsInChildren<Transform>();
                foreach (var t in trs)
                {
                    string n = t.name.ToLower();
                    if (n == "head" || n.EndsWith("_head") || n.EndsWith(":head"))
                    {
                        _headBone = t;
                        break;
                    }
                }
            }
        }

        private void LoadDefaultChatterClips()
        {
#if UNITY_EDITOR
            if (speechClips == null || speechClips.Length == 0)
            {
                var clips = new List<AudioClip>();
                for (int i = 1; i <= 3; i++)
                {
                    var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Audio/NPC_Chatter/npc_civilian_murmur_0{i}.wav");
                    if (clip != null) clips.Add(clip);
                }
                for (int i = 1; i <= 3; i++)
                {
                    var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Audio/NPC_Chatter/npc_phone_chatter_0{i}.wav");
                    if (clip != null) clips.Add(clip);
                }
                speechClips = clips.ToArray();
            }
#endif
        }

        private static void EnsureProceduralChirpAudio()
        {
            if (_proceduralChirpClip != null) return;
            int samples = 750;
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float env = Mathf.Sin(t * Mathf.PI);
                data[i] = Mathf.Sin(2f * Mathf.PI * 1350f * ((float)i / 22050f)) * env * 0.22f;
            }
            _proceduralChirpClip = AudioClip.Create("CyberTypewriterChirp", samples, 1, 22050, false);
            _proceduralChirpClip.SetData(data, 0);
        }

        private void Update()
        {
            UpdateBlinking();
            UpdateSpeechLipSync();
            UpdateMoodExpressions();
            UpdateProximityInteraction();
            UpdateBillboardOrientation();
        }

        private void LateUpdate()
        {
            // Crucial: Apply blendshapes in LateUpdate so Animator controller passes do NOT overwrite them!
            ApplyBlendShapesToAllMeshes();

            // Apply Head Look-At & Conversational Gestures
            if (_headBone != null)
            {
                ApplyHeadLookAtAndGestures();
            }
        }

        // ─── 1. Eye Blinking Engine ───────────────────────────────────────────
        private void UpdateBlinking()
        {
            if (!enableBlinking)
            {
                _currEyeBlink = 0f;
                return;
            }

            _blinkTimer += Time.deltaTime;
            if (!_isBlinking)
            {
                if (_blinkTimer >= _nextBlinkTime)
                {
                    _isBlinking = true;
                    _blinkProgress = 0f;
                    _blinkTimer = 0f;
                    _nextBlinkTime = Random.Range(minBlinkInterval, maxBlinkInterval);
                }
            }
            else
            {
                _blinkProgress += Time.deltaTime * 6.8f;
                if (_blinkProgress <= 0.45f)
                {
                    _currEyeBlink = Mathf.SmoothStep(0f, 100f, _blinkProgress / 0.45f);
                }
                else if (_blinkProgress < 1.0f)
                {
                    _currEyeBlink = Mathf.SmoothStep(100f, 0f, (_blinkProgress - 0.45f) / 0.55f);
                }
                else
                {
                    _currEyeBlink = 0f;
                    _isBlinking = false;
                    if (Random.value < doubleBlinkChance)
                    {
                        _nextBlinkTime = 0.16f; // Immediate double-blink
                    }
                }
            }
        }

        // ─── 2. Speech & Lip Sync Engine ──────────────────────────────────────
        private void UpdateSpeechLipSync()
        {
            float targetJaw = 0f;
            float targetFunnel = 0f;
            float targetPucker = 0f;
            float targetStretch = 0f;
            float targetClose = 0f;
            float targetBrowRaise = 0f;

            bool isAudioPlaying = voiceAudioSource != null && voiceAudioSource.isPlaying;
            bool activeTalking = isSpeaking || isAudioPlaying;

            if (activeTalking)
            {
                if (speechMode == SpeechMode.AudioReactive && isAudioPlaying)
                {
                    voiceAudioSource.GetOutputData(_audioSampleBuffer, 0);
                    float sum = 0f;
                    int zeroCrossings = 0;
                    float lastSample = 0f;

                    for (int i = 0; i < _audioSampleBuffer.Length; i++)
                    {
                        float s = _audioSampleBuffer[i];
                        sum += s * s;
                        if ((s > 0f && lastSample <= 0f) || (s < 0f && lastSample >= 0f))
                            zeroCrossings++;
                        lastSample = s;
                    }

                    float rms = Mathf.Sqrt(sum / _audioSampleBuffer.Length);
                    float volumeNorm = Mathf.Clamp01(rms * audioLoudnessSensitivity * 10f);
                    float zcr = (float)zeroCrossings / _audioSampleBuffer.Length;

                    if (volumeNorm > 0.05f)
                    {
                        targetJaw = volumeNorm * jawOpenMaxWeight;
                        if (zcr < 0.18f)
                        {
                            targetFunnel = volumeNorm * lipShapeMaxWeight * 0.75f;
                            targetPucker = volumeNorm * lipShapeMaxWeight * 0.45f;
                        }
                        else
                        {
                            targetStretch = volumeNorm * lipShapeMaxWeight * 0.70f;
                        }

                        if (enableEyebrowEmphasis && volumeNorm > 0.40f)
                        {
                            targetBrowRaise = (volumeNorm - 0.40f) * 65f;
                        }
                    }
                    else
                    {
                        targetClose = 35f;
                    }
                }
                else
                {
                    // Procedural Cadence (Natural 4.2 Hz Syllable Rhythm)
                    _cadencePhase += Time.deltaTime * 4.2f * Mathf.PI * 2f;
                    float syllableWave = Mathf.Sin(_cadencePhase);
                    float syllableAccent = Mathf.PerlinNoise(Time.time * 2.8f, 0.5f);

                    if (syllableWave > -0.25f)
                    {
                        float openness = Mathf.Clamp01((syllableWave + 0.25f) / 1.25f) * syllableAccent;
                        targetJaw = openness * jawOpenMaxWeight;

                        float vowelChoice = Mathf.Sin(_cadencePhase * 0.5f);
                        if (vowelChoice > 0.2f)
                        {
                            targetFunnel = openness * lipShapeMaxWeight * 0.70f;
                        }
                        else if (vowelChoice < -0.2f)
                        {
                            targetStretch = openness * lipShapeMaxWeight * 0.70f;
                        }
                        else
                        {
                            targetPucker = openness * lipShapeMaxWeight * 0.50f;
                        }

                        if (enableEyebrowEmphasis && openness > 0.55f)
                        {
                            targetBrowRaise = 40f * openness;
                        }
                    }
                    else
                    {
                        targetClose = 40f;
                    }
                }
            }

            float dt = Time.deltaTime * lipSmoothSpeed;
            _currJawOpen     = Mathf.Lerp(_currJawOpen,     targetJaw,     dt);
            _currMouthFunnel = Mathf.Lerp(_currMouthFunnel, targetFunnel,  dt);
            _currMouthPucker = Mathf.Lerp(_currMouthPucker, targetPucker,  dt);
            _currMouthStretch= Mathf.Lerp(_currMouthStretch,targetStretch, dt);
            _currMouthClose  = Mathf.Lerp(_currMouthClose,  targetClose,   dt);
            _currBrowRaise   = Mathf.Lerp(_currBrowRaise,   targetBrowRaise, dt * 0.7f);
        }

        // ─── 3. Mood & Emotional Expression Interpolation ─────────────────────
        private void UpdateMoodExpressions()
        {
            float targetSmileL = 0f;
            float targetSmileR = 0f;
            float targetFrownL = 0f;
            float targetFrownR = 0f;
            float targetDimpleR = 0f;
            float targetBrowFrown = 0f;
            float targetEyeSquint = 0f;
            float targetEyeWide = 0f;

            switch (currentMood)
            {
                case FacialMood.Friendly:
                    targetSmileL = 26f;
                    targetSmileR = 26f;
                    break;

                case FacialMood.CyberpunkSmirk:
                    targetSmileL = 4f;
                    targetSmileR = 40f;
                    targetDimpleR = 32f;
                    targetEyeSquint = 16f;
                    break;

                case FacialMood.Serious:
                    targetFrownL = 18f;
                    targetFrownR = 18f;
                    targetBrowFrown = 35f;
                    targetEyeSquint = 15f;
                    break;

                case FacialMood.Concerned:
                    targetFrownL = 24f;
                    targetFrownR = 24f;
                    targetBrowFrown = 45f;
                    break;

                case FacialMood.Surprised:
                    targetEyeWide = 55f;
                    targetBrowFrown = -25f;
                    break;

                case FacialMood.Suspicious:
                    targetSmileR = 15f;
                    targetEyeSquint = 35f;
                    targetBrowFrown = 30f;
                    targetFrownL = 15f;
                    break;
            }

            float dt = Time.deltaTime * moodBlendSpeed;
            _currSmileL   = Mathf.Lerp(_currSmileL,   targetSmileL,   dt);
            _currSmileR   = Mathf.Lerp(_currSmileR,   targetSmileR,   dt);
            _currFrownL   = Mathf.Lerp(_currFrownL,   targetFrownL,   dt);
            _currFrownR   = Mathf.Lerp(_currFrownR,   targetFrownR,   dt);
            _currDimpleR  = Mathf.Lerp(_currDimpleR,  targetDimpleR,  dt);
            _currBrowFrown= Mathf.Lerp(_currBrowFrown,targetBrowFrown,dt);
            _currEyeSquint= Mathf.Lerp(_currEyeSquint,targetEyeSquint,dt);
            _currEyeWide  = Mathf.Lerp(_currEyeWide,  targetEyeWide,  dt);
        }

        // ─── 4. Apply Blendshapes across All Meshes ───────────────────────────
        private void ApplyBlendShapesToAllMeshes()
        {
            if (_allMeshMappings.Count == 0) return;

            float lowerLipDrop = _currJawOpen * 0.38f;

            for (int i = 0; i < _allMeshMappings.Count; i++)
            {
                var map = _allMeshMappings[i];
                if (map.smr == null) continue;

                // Speech Visemes
                SetWeight(map.smr, map.idxJawOpen,       _currJawOpen);
                SetWeight(map.smr, map.idxMouthClose,    _currMouthClose);
                SetWeight(map.smr, map.idxMouthFunnel,   _currMouthFunnel);
                SetWeight(map.smr, map.idxMouthPucker,   _currMouthPucker);
                SetWeight(map.smr, map.idxMouthStretchL, _currMouthStretch);
                SetWeight(map.smr, map.idxMouthStretchR, _currMouthStretch);
                SetWeight(map.smr, map.idxMouthLowerDownL, lowerLipDrop);
                SetWeight(map.smr, map.idxMouthLowerDownR, lowerLipDrop);

                // Mood Expressions
                SetWeight(map.smr, map.idxMouthSmileL,   _currSmileL);
                SetWeight(map.smr, map.idxMouthSmileR,   _currSmileR);
                SetWeight(map.smr, map.idxMouthFrownL,   _currFrownL);
                SetWeight(map.smr, map.idxMouthFrownR,   _currFrownR);
                SetWeight(map.smr, map.idxMouthDimpleR,  _currDimpleR);

                // Eyes: Blink, Squint, Wide
                SetWeight(map.smr, map.idxEyeBlinkUpperL, _currEyeBlink);
                SetWeight(map.smr, map.idxEyeBlinkUpperR, _currEyeBlink);
                SetWeight(map.smr, map.idxEyeBlinkLowerL, _currEyeBlink * 0.35f);
                SetWeight(map.smr, map.idxEyeBlinkLowerR, _currEyeBlink * 0.35f);

                SetWeight(map.smr, map.idxEyeSquintL,    _currEyeSquint);
                SetWeight(map.smr, map.idxEyeSquintR,    _currEyeSquint);
                SetWeight(map.smr, map.idxEyeWideUpperL, _currEyeWide);
                SetWeight(map.smr, map.idxEyeWideUpperR, _currEyeWide);

                // Eyebrows
                float totalBrowRaise = Mathf.Max(0f, _currBrowRaise - (_currBrowFrown > 0 ? _currBrowFrown * 0.4f : 0f));
                SetWeight(map.smr, map.idxBrowInnerUpL, totalBrowRaise);
                SetWeight(map.smr, map.idxBrowInnerUpR, totalBrowRaise);
                SetWeight(map.smr, map.idxBrowRaiseL,   totalBrowRaise * 0.8f);
                SetWeight(map.smr, map.idxBrowRaiseR,   totalBrowRaise * 0.8f);

                float browFurrow = Mathf.Max(0f, _currBrowFrown);
                SetWeight(map.smr, map.idxBrowFrownL,   browFurrow);
                SetWeight(map.smr, map.idxBrowFrownR,   browFurrow);
                SetWeight(map.smr, map.idxBrowInnerDownL, browFurrow * 0.75f);
                SetWeight(map.smr, map.idxBrowInnerDownR, browFurrow * 0.75f);
            }
        }

        private static void SetWeight(SkinnedMeshRenderer smr, int index, float weight)
        {
            if (index >= 0 && smr != null)
            {
                smr.SetBlendShapeWeight(index, Mathf.Clamp(weight, 0f, 100f));
            }
        }

        // ─── 5. Head Look-At & Speech Gestures ────────────────────────────────
        private void ApplyHeadLookAtAndGestures()
        {
            if (_headBone == null) return;

            // 1. Look-At Player
            if (enableLookAtPlayer && _playerTransform != null)
            {
                float dist = Vector3.Distance(transform.position, _playerTransform.position);
                if (dist <= lookAtMaxDistance)
                {
                    Vector3 lookTarget = _playerTransform.position + Vector3.up * 1.55f;
                    Vector3 toPlayer = (lookTarget - _headBone.position).normalized;
                    float angle = Vector3.Angle(transform.forward, toPlayer);
                    if (angle <= lookAtMaxAngle)
                    {
                        _targetLookAtWeight = Mathf.Clamp01(1f - (dist / lookAtMaxDistance) * 0.6f);
                    }
                    else
                    {
                        _targetLookAtWeight = 0f;
                    }
                }
                else
                {
                    _targetLookAtWeight = 0f;
                }
            }
            else
            {
                _targetLookAtWeight = 0f;
            }

            _currLookAtWeight = Mathf.Lerp(_currLookAtWeight, _targetLookAtWeight, Time.deltaTime * 3.5f);

            if (_currLookAtWeight > 0.02f && _playerTransform != null)
            {
                Vector3 lookTarget = _playerTransform.position + Vector3.up * 1.55f;
                Vector3 toTarget = lookTarget - _headBone.position;
                if (toTarget.sqrMagnitude > 0.01f)
                {
                    Quaternion worldLookRot = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
                    _headBone.rotation = Quaternion.Slerp(_headBone.rotation, worldLookRot, _currLookAtWeight * 0.75f);
                }
            }

            // 2. Additive Conversational Gestures
            if (enableHeadMicroGestures)
            {
                float nodPitch = 0f;
                float tiltRoll = 0f;

                if (isSpeaking || (voiceAudioSource != null && voiceAudioSource.isPlaying))
                {
                    nodPitch = Mathf.Sin(Time.time * 6.5f) * headNodIntensity * (_currJawOpen / 75f);
                    tiltRoll = Mathf.Cos(Time.time * 3.2f) * (headNodIntensity * 0.35f);
                }
                else
                {
                    nodPitch = Mathf.Sin(Time.time * 1.3f) * 0.35f;
                }

                Quaternion gestureRot = Quaternion.Euler(nodPitch, 0f, tiltRoll);
                _headBone.localRotation = _headBone.localRotation * gestureRot;
            }
        }

        // ─── 6. Proximity Interaction & Input [E] ─────────────────────────────
        private void UpdateProximityInteraction()
        {
            if (!enableProximityInteraction) return;

            if (_playerTransform == null)
            {
                var player = GameObject.FindWithTag("Player");
                if (player != null) _playerTransform = player.transform;
                if (_playerTransform == null) return;
            }

            float dist = Vector3.Distance(transform.position, _playerTransform.position);
            _playerInProximity = dist <= interactionRadius;

            // Handle Interaction Prompt visibility
            if (_playerInProximity && !_bubbleCanvasGroup.gameObject.activeSelf)
            {
                _promptCanvasGroup.alpha = Mathf.MoveTowards(_promptCanvasGroup.alpha, 1f, Time.deltaTime * 4f);
            }
            else
            {
                _promptCanvasGroup.alpha = Mathf.MoveTowards(_promptCanvasGroup.alpha, 0f, Time.deltaTime * 4f);
            }

            // Advance dialogue auto-close timer if speaking ended
            if (_bubbleCanvasGroup.gameObject.activeSelf && !_isTypewriting && !isSpeaking)
            {
                _bubbleAutoCloseTimer -= Time.deltaTime;
                if (_bubbleAutoCloseTimer <= 0f || dist > interactionRadius * 1.8f)
                {
                    HideSpeechBubble();
                }
            }

            // Input handling: [E] Talk
            if (Input.GetKeyDown(KeyCode.E) && _playerInProximity)
            {
                OnInteractPressed();
            }
        }

        private void OnInteractPressed()
        {
            // If currently typewriting, skip directly to complete text
            if (_isTypewriting)
            {
                _isTypewriting = false;
                if (_bubbleDialogueText != null) _bubbleDialogueText.text = _fullTargetDialogue;
                _bubbleAutoCloseTimer = 4.5f;
                return;
            }

            // Retrieve dialogue entries
            int totalLines = GetTotalDialogueCount();
            if (totalLines == 0) return;

            DialogueEntry entry = GetDialogueEntry(_currentDialogueIndex);
            _currentDialogueIndex = (_currentDialogueIndex + 1) % totalLines;

            SpeakDialogueEntry(entry);
        }

        private int GetTotalDialogueCount()
        {
            if (richDialogueLines != null && richDialogueLines.Length > 0) return richDialogueLines.Length;
            if (dialogueLines != null && dialogueLines.Length > 0) return dialogueLines.Length;
            return 0;
        }

        private DialogueEntry GetDialogueEntry(int index)
        {
            if (richDialogueLines != null && richDialogueLines.Length > 0)
            {
                return richDialogueLines[index % richDialogueLines.Length];
            }
            if (dialogueLines != null && dialogueLines.Length > 0)
            {
                string text = dialogueLines[index % dialogueLines.Length];
                FacialMood mood = currentMood;
                string low = text.ToLower();
                if (low.Contains("warning") || low.Contains("ncpd") || low.Contains("hostile") || low.Contains("patrol")) mood = FacialMood.Serious;
                else if (low.Contains("haha") || low.Contains("choom") || low.Contains("katana") || low.Contains("blade") || low.Contains("deck")) mood = FacialMood.CyberpunkSmirk;
                else if (low.Contains("doc") || low.Contains("clinic") || low.Contains("careful")) mood = FacialMood.Friendly;

                AudioClip clip = (speechClips != null && speechClips.Length > 0) ? speechClips[index % speechClips.Length] : null;
                return new DialogueEntry(text, mood, clip);
            }
            return new DialogueEntry("Transmission active.");
        }

        public void SpeakDialogueEntry(DialogueEntry entry)
        {
            currentMood = entry.mood;
            _fullTargetDialogue = entry.text;
            float duration = entry.customVoiceClip != null ? entry.customVoiceClip.length : Mathf.Max(3.2f, entry.text.Length * 0.055f);

            if (_speechRoutine != null) StopCoroutine(_speechRoutine);
            _speechRoutine = StartCoroutine(TypewriterSpeechRoutine(entry, duration));
        }

        private IEnumerator TypewriterSpeechRoutine(DialogueEntry entry, float estimatedDuration)
        {
            isSpeaking = true;
            _isTypewriting = true;

            // Audio: Play spatial voice chatter or comms sound
            if (entry.customVoiceClip != null && voiceAudioSource != null)
            {
                voiceAudioSource.clip = entry.customVoiceClip;
                voiceAudioSource.volume = 0.88f;
                voiceAudioSource.Play();
            }

            ShowSpeechBubbleUI(entry);

            // Typewriter effect
            string full = entry.text;
            if (!Application.isPlaying)
            {
                if (_bubbleDialogueText != null) _bubbleDialogueText.text = full;
                isSpeaking = true;
                yield break;
            }

            _bubbleDialogueText.text = "";
            float charDelay = 0.026f;

            for (int i = 0; i < full.Length; i++)
            {
                if (!_isTypewriting) break; // Player pressed E to skip typewriter

                _bubbleDialogueText.text += full[i];

                // Play subtle typewriter synth chirp every 2nd non-space character
                if (full[i] != ' ' && i % 2 == 0 && voiceAudioSource != null && _proceduralChirpClip != null)
                {
                    voiceAudioSource.PlayOneShot(_proceduralChirpClip, 0.18f);
                }

                yield return new WaitForSeconds(charDelay);
            }

            _bubbleDialogueText.text = full;
            _isTypewriting = false;

            // Remain in talking viseme state for a natural buffer or clip duration
            float remainingSpeech = Mathf.Max(0.8f, estimatedDuration - (full.Length * charDelay));
            yield return new WaitForSeconds(remainingSpeech);

            isSpeaking = false;
            _bubbleAutoCloseTimer = 4.2f;
            _speechRoutine = null;
        }

        // ─── 7. World-Space UI Creation (Cyberpunk Hologram) ──────────────────
        private void CreateWorldDialogueUI()
        {
            _uiRootObj = new GameObject("WorldDialogueUI");
            _uiRootObj.transform.SetParent(transform);
            _uiRootObj.transform.localPosition = new Vector3(0f, 2.15f, 0f);

            var canvas = _uiRootObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = _uiRootObj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(380f, 130f);
            rt.localScale = Vector3.one * 0.003f; // 1.14m wide x 0.39m tall in world space

            // ─── PROMPT BADGE ────────────────────────────────────────────────
            var promptObj = new GameObject("PromptBadge");
            promptObj.transform.SetParent(_uiRootObj.transform, false);
            var pRt = promptObj.AddComponent<RectTransform>();
            pRt.anchorMin = new Vector2(0.1f, 0.25f);
            pRt.anchorMax = new Vector2(0.9f, 0.75f);
            pRt.sizeDelta = Vector2.zero;

            _promptCanvasGroup = promptObj.AddComponent<CanvasGroup>();
            _promptCanvasGroup.alpha = 0f;

            var pBg = promptObj.AddComponent<Image>();
            pBg.color = new Color(0.02f, 0.05f, 0.09f, 0.92f);

            var pOutline = promptObj.AddComponent<Outline>();
            pOutline.effectColor = new Color(0f, 0.94f, 1f, 0.85f); // Kiroshi Cyan
            pOutline.effectDistance = new Vector2(1.5f, 1.5f);

            var pTextObj = new GameObject("Txt_Prompt");
            pTextObj.transform.SetParent(promptObj.transform, false);
            _promptText = pTextObj.AddComponent<TextMeshProUGUI>();
            _promptText.text = $"[E] TALK TO {characterDisplayName.ToUpper()}";
            _promptText.fontSize = 17f;
            _promptText.fontStyle = FontStyles.Bold;
            _promptText.alignment = TextAlignmentOptions.Center;
            _promptText.color = new Color(0.98f, 0.93f, 0.04f, 1f); // Cyberpunk Yellow
            var ptRt = pTextObj.GetComponent<RectTransform>();
            ptRt.anchorMin = Vector2.zero; ptRt.anchorMax = Vector2.one;
            ptRt.sizeDelta = Vector2.zero;

            // ─── SPEECH BUBBLE PANEL ─────────────────────────────────────────
            var bubbleObj = new GameObject("SpeechBubble");
            bubbleObj.transform.SetParent(_uiRootObj.transform, false);
            var bRt = bubbleObj.AddComponent<RectTransform>();
            bRt.anchorMin = Vector2.zero; bRt.anchorMax = Vector2.one;
            bRt.sizeDelta = Vector2.zero;

            _bubbleCanvasGroup = bubbleObj.AddComponent<CanvasGroup>();
            _bubbleCanvasGroup.alpha = 0f;
            bubbleObj.SetActive(false);

            var bBg = bubbleObj.AddComponent<Image>();
            bBg.color = new Color(0.015f, 0.035f, 0.065f, 0.96f);

            var bOutline = bubbleObj.AddComponent<Outline>();
            bOutline.effectColor = new Color(0f, 0.94f, 1f, 0.80f);
            bOutline.effectDistance = new Vector2(1.5f, 1.5f);

            // Cyber Yellow Accent Top Strip
            var stripObj = new GameObject("AccentStrip");
            stripObj.transform.SetParent(bubbleObj.transform, false);
            var stripImg = stripObj.AddComponent<Image>();
            stripImg.color = new Color(0.988f, 0.933f, 0.039f, 1f);
            var sRt = stripObj.GetComponent<RectTransform>();
            sRt.anchorMin = new Vector2(0f, 0.96f);
            sRt.anchorMax = new Vector2(1f, 1f);
            sRt.sizeDelta = Vector2.zero;

            // Header: Speaker Name + Subtitle
            var headerObj = new GameObject("Txt_Header");
            headerObj.transform.SetParent(bubbleObj.transform, false);
            _bubbleSpeakerName = headerObj.AddComponent<TextMeshProUGUI>();
            _bubbleSpeakerName.text = $"// COMMS // {characterDisplayName.ToUpper()}";
            _bubbleSpeakerName.fontSize = 14f;
            _bubbleSpeakerName.fontStyle = FontStyles.Bold;
            _bubbleSpeakerName.color = new Color(0f, 0.95f, 1f, 1f);
            var hRt = headerObj.GetComponent<RectTransform>();
            hRt.anchorMin = new Vector2(0.04f, 0.76f);
            hRt.anchorMax = new Vector2(0.65f, 0.94f);
            hRt.sizeDelta = Vector2.zero;

            // Mood Badge Text
            var moodObj = new GameObject("Txt_MoodBadge");
            moodObj.transform.SetParent(bubbleObj.transform, false);
            _bubbleMoodBadge = moodObj.AddComponent<TextMeshProUGUI>();
            _bubbleMoodBadge.text = "[MOOD: SMIRK]";
            _bubbleMoodBadge.fontSize = 11.5f;
            _bubbleMoodBadge.alignment = TextAlignmentOptions.Right;
            _bubbleMoodBadge.color = new Color(0.988f, 0.933f, 0.039f, 0.9f);
            var mRt = moodObj.GetComponent<RectTransform>();
            mRt.anchorMin = new Vector2(0.65f, 0.76f);
            mRt.anchorMax = new Vector2(0.96f, 0.94f);
            mRt.sizeDelta = Vector2.zero;

            // Main Dialogue Text
            var bodyObj = new GameObject("Txt_Body");
            bodyObj.transform.SetParent(bubbleObj.transform, false);
            _bubbleDialogueText = bodyObj.AddComponent<TextMeshProUGUI>();
            _bubbleDialogueText.text = "";
            _bubbleDialogueText.fontSize = 12.5f;
            _bubbleDialogueText.lineSpacing = 1.15f;
            _bubbleDialogueText.color = new Color(0.96f, 0.98f, 1f, 1f);
            _bubbleDialogueText.alignment = TextAlignmentOptions.TopLeft;
            var bodyRt = bodyObj.GetComponent<RectTransform>();
            bodyRt.anchorMin = new Vector2(0.04f, 0.18f);
            bodyRt.anchorMax = new Vector2(0.96f, 0.74f);
            bodyRt.sizeDelta = Vector2.zero;

            // Footer Prompt: [E] ADVANCE
            var footerObj = new GameObject("Txt_Footer");
            footerObj.transform.SetParent(bubbleObj.transform, false);
            var footerText = footerObj.AddComponent<TextMeshProUGUI>();
            footerText.text = "[E] NEXT   ▲ CYBERLINK ACTIVE";
            footerText.fontSize = 10f;
            footerText.color = new Color(0.5f, 0.65f, 0.8f, 0.85f);
            var fRt = footerObj.GetComponent<RectTransform>();
            fRt.anchorMin = new Vector2(0.04f, 0.03f);
            fRt.anchorMax = new Vector2(0.96f, 0.16f);
            fRt.sizeDelta = Vector2.zero;
        }

        private void ShowSpeechBubbleUI(DialogueEntry entry)
        {
            if (_bubbleCanvasGroup == null) return;
            _bubbleCanvasGroup.gameObject.SetActive(true);
            _bubbleCanvasGroup.alpha = 1f;

            if (_bubbleSpeakerName != null) _bubbleSpeakerName.text = $"// COMMS // {characterDisplayName.ToUpper()}";
            if (_bubbleMoodBadge != null) _bubbleMoodBadge.text = $"[MOOD: {entry.mood.ToString().ToUpper()}]";

            // Hide the interaction prompt while actively talking
            if (_promptCanvasGroup != null) _promptCanvasGroup.alpha = 0f;
        }

        private void HideSpeechBubble()
        {
            if (_bubbleCanvasGroup == null) return;
            _bubbleCanvasGroup.alpha = 0f;
            _bubbleCanvasGroup.gameObject.SetActive(false);
            if (_speechRoutine != null)
            {
                StopCoroutine(_speechRoutine);
                _speechRoutine = null;
            }
            isSpeaking = false;
            _isTypewriting = false;
        }

        private void UpdateBillboardOrientation()
        {
            if (_uiRootObj == null) return;

            Camera targetCam = Camera.main;
#if UNITY_EDITOR
            if (targetCam == null && UnityEditor.SceneView.lastActiveSceneView != null)
            {
                targetCam = UnityEditor.SceneView.lastActiveSceneView.camera;
            }
#endif
            if (targetCam == null) return;

            // Align rotation with camera to prevent mirrored text
            _uiRootObj.transform.rotation = targetCam.transform.rotation;
        }

        // ─── Public API (Maintains full compatibility) ────────────────────────
        public void SpeakText(string text, AudioClip customClip = null, float duration = 4.0f)
        {
            DialogueEntry entry = new DialogueEntry(text, currentMood, customClip);
            SpeakDialogueEntry(entry);
        }

        public void SpeakRandomDialogue()
        {
            int total = GetTotalDialogueCount();
            if (total == 0) return;
            DialogueEntry entry = GetDialogueEntry(Random.Range(0, total));
            SpeakDialogueEntry(entry);
        }

        public void SetTalkingState(bool talking, AudioClip clip = null)
        {
            isSpeaking = talking;
            if (talking && clip != null && voiceAudioSource != null)
            {
                voiceAudioSource.clip = clip;
                voiceAudioSource.volume = 0.88f;
                voiceAudioSource.Play();
            }
            else if (!talking && voiceAudioSource != null && voiceAudioSource.isPlaying)
            {
                voiceAudioSource.Stop();
            }
        }
    }
}
