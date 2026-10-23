using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Alita.UI;

namespace Alita
{
    public enum AwakeningPhase
    {
        TerminalBoot,         // 0s - 7.5s: Extreme close-up of diagnostic terminal; authentic Linux shell commands typed
        StasisScanning,       // 7.5s - 12.0s: Laser scan over closed glass pod, deep sub-bass drone
        PowerSurge,           // 12.0s - 16.0s: Grid pulses with energy, core ignites, synth riser
        OpticActivation,      // 16.0s - 19.0s: Crystal chime, optic flares ignite, eyes snap open
        DepressurizeCanopy,   // 19.0s - 23.5s: Canopy lifts, cryo-steam hisses outward
        AlitaSitUp,           // 23.5s - 29.0s: Alita smoothly sits upright inside the pod
        InspectHands,         // 29.0s - 36.5s: Raises porcelain cyber hands, flexes fingers in wonder
        IdoApproach,          // 36.5s - 45.5s: Dr. Ido walks over from console, welcomes her
        Epilogue              // 45.5s+: Cinematic title card & interactive freedom
    }

    /// <summary>
    /// Master Cinematic Cutscene Controller for the Tron: Ares / Tron: Legacy
    /// inspired Awakening of Alita in Dr. Ido's cybernetic clinic.
    /// </summary>
    public class AlitaAwakeningCutsceneController : MonoBehaviour
    {
        public static AlitaAwakeningCutsceneController Instance { get; private set; }

        [Header("State")]
        public AwakeningPhase currentPhase = AwakeningPhase.TerminalBoot;
        public float phaseTimer = 0f;
        public bool isPlaying = true;

        [Header("Key Actors")]
        public GameObject alitaDollBody;
        public Animator alitaAnimator;
        public GameObject doctorActor;
        public Animator doctorAnimator;

        [Header("Hibernation Pod Rig")]
        public Transform podBase;
        public Transform podCanopy;
        public ParticleSystem cryoSteamFX;
        public LineRenderer scanLaserLine;
        public Light overheadSurgicalLight;
        public Light quantumCoreLight;
        public Light leftEyeOpticLight;
        public Light rightEyeOpticLight;

        [Header("Tron Grid Elements")]
        public Material tronGridMaterial;
        public List<Light> gridPillarLights = new List<Light>();

        [Header("Camera Choreography")]
        public Camera cutsceneCamera;
        public float cameraFov = 34f;

        [Header("Waypoints")]
        public Vector3 doctorConsolePos = new Vector3(-2.2f, 0f, 1.8f);
        public Vector3 doctorPodSidePos = new Vector3(-1.15f, 0f, 0.45f);

        [Header("Audio")]
        public AudioSource droneAudioSource;
        public AudioSource fxAudioSource;
        public AudioSource dialogueAudioSource;

        // Subtitles
        private string _currentSubtitle = "";
        private float _subtitleTimer = 0f;
        private float _epilogueAlpha = 0f;

        // Procedural Bone Pointers for Alita
        private Transform _alitaHips;
        private Transform _alitaSpine;
        private Transform _alitaChest;
        private Transform _alitaNeck;
        private Transform _alitaHead;
        private Transform _alitaUpperArmL;
        private Transform _alitaLowerArmL;
        private Transform _alitaHandL;
        private Transform _alitaUpperArmR;
        private Transform _alitaLowerArmR;
        private Transform _alitaHandR;

        // Facial blendshapes for eyelid animation
        private SkinnedMeshRenderer _alitaSMR;
        private int _bsEyeBlinkUpperL = -1;
        private int _bsEyeBlinkUpperR = -1;
        private int _bsEyeBlinkLowerL = -1;
        private int _bsEyeBlinkLowerR = -1;
        private int _bsBrowInnerUpL = -1;
        private int _bsBrowInnerUpR = -1;

        // Finger Bones for Hand Inspection
        private List<Transform> _leftFingers = new List<Transform>();
        private List<Transform> _rightFingers = new List<Transform>();

        // Canopy Animation
        private Vector3 _canopyClosedPos;
        private Quaternion _canopyClosedRot;
        private Vector3 _canopyOpenPos;
        private Quaternion _canopyOpenRot;

        // Terminal Boot Sequence (Tron: Ares Style)
        private bool _typedSound1Played = false;
        private bool _beep1Played = false;
        private bool _typedSound2Played = false;
        private bool _beep2Played = false;
        private bool _subDropPlayed = false;

        private void Awake()
        {
            Instance = this;
            InitializeAudio();
            InitializeBones();
            InitializePodCanopy();
        }

        private void InitializeAudio()
        {
            if (droneAudioSource == null) droneAudioSource = gameObject.AddComponent<AudioSource>();
            droneAudioSource.loop = true;
            droneAudioSource.spatialBlend = 0f;
            droneAudioSource.clip = AlitaAwakeningAudioEngine.CreateTronSubBassDrone(16f);
            droneAudioSource.volume = 0.55f;
            droneAudioSource.Play();

            if (fxAudioSource == null)
            {
                var fxGo = new GameObject("FX_Audio_Source");
                fxGo.transform.SetParent(transform);
                fxAudioSource = fxGo.AddComponent<AudioSource>();
            }
            fxAudioSource.spatialBlend = 0f;

            if (dialogueAudioSource == null)
            {
                var diaGo = new GameObject("Dialogue_Audio_Source");
                diaGo.transform.SetParent(transform);
                dialogueAudioSource = diaGo.AddComponent<AudioSource>();
            }
            dialogueAudioSource.spatialBlend = 0f;
        }

        private void InitializeBones()
        {
            if (alitaDollBody == null) alitaDollBody = GameObject.Find("Alita_02_Normal_DollBody");
            if (alitaDollBody != null)
            {
                _alitaSMR = alitaDollBody.GetComponentInChildren<SkinnedMeshRenderer>();
                CacheBlendShapeIndices();
                alitaAnimator = alitaDollBody.GetComponent<Animator>();
                if (alitaAnimator != null)
                {
                    _alitaHips = alitaAnimator.GetBoneTransform(HumanBodyBones.Hips);
                    _alitaSpine = alitaAnimator.GetBoneTransform(HumanBodyBones.Spine);
                    _alitaChest = alitaAnimator.GetBoneTransform(HumanBodyBones.Chest);
                    _alitaNeck = alitaAnimator.GetBoneTransform(HumanBodyBones.Neck);
                    _alitaHead = alitaAnimator.GetBoneTransform(HumanBodyBones.Head);

                    _alitaUpperArmL = alitaAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                    _alitaLowerArmL = alitaAnimator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                    _alitaHandL = alitaAnimator.GetBoneTransform(HumanBodyBones.LeftHand);

                    _alitaUpperArmR = alitaAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                    _alitaLowerArmR = alitaAnimator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                    _alitaHandR = alitaAnimator.GetBoneTransform(HumanBodyBones.RightHand);

                    // Collect finger joints
                    CollectFingers(alitaAnimator, HumanBodyBones.LeftThumbProximal, _leftFingers);
                    CollectFingers(alitaAnimator, HumanBodyBones.LeftIndexProximal, _leftFingers);
                    CollectFingers(alitaAnimator, HumanBodyBones.LeftMiddleProximal, _leftFingers);
                    CollectFingers(alitaAnimator, HumanBodyBones.LeftRingProximal, _leftFingers);
                    CollectFingers(alitaAnimator, HumanBodyBones.LeftLittleProximal, _leftFingers);

                    CollectFingers(alitaAnimator, HumanBodyBones.RightThumbProximal, _rightFingers);
                    CollectFingers(alitaAnimator, HumanBodyBones.RightIndexProximal, _rightFingers);
                    CollectFingers(alitaAnimator, HumanBodyBones.RightMiddleProximal, _rightFingers);
                    CollectFingers(alitaAnimator, HumanBodyBones.RightRingProximal, _rightFingers);
                    CollectFingers(alitaAnimator, HumanBodyBones.RightLittleProximal, _rightFingers);
                }
            }

            if (doctorActor == null) doctorActor = GameObject.Find("Doctor_Actor");
            if (doctorActor != null) doctorAnimator = doctorActor.GetComponentInChildren<Animator>();
        }

        private void CollectFingers(Animator anim, HumanBodyBones startBone, List<Transform> list)
        {
            Transform t1 = anim.GetBoneTransform(startBone);
            if (t1 != null)
            {
                list.Add(t1);
                if (t1.childCount > 0)
                {
                    Transform t2 = t1.GetChild(0);
                    list.Add(t2);
                    if (t2.childCount > 0) list.Add(t2.GetChild(0));
                }
            }
        }

        private void InitializePodCanopy()
        {
            if (podCanopy != null)
            {
                _canopyClosedPos = podCanopy.localPosition;
                _canopyClosedRot = podCanopy.localRotation;
                _canopyOpenPos = _canopyClosedPos + new Vector3(0f, 1.65f, 0f);
                _canopyOpenRot = _canopyClosedRot;
            }
        }

        private void Update()
        {
            if (!isPlaying) return;

            phaseTimer += Time.deltaTime;
            if (_subtitleTimer > 0f)
            {
                _subtitleTimer -= Time.deltaTime;
                if (_subtitleTimer <= 0f) _currentSubtitle = "";
            }

            // Phase Sequence Transitions
            switch (currentPhase)
            {
                case AwakeningPhase.TerminalBoot:
                    UpdateTerminalBoot();
                    if (phaseTimer >= 7.5f)
                    {
                        TransitionToPhase(AwakeningPhase.StasisScanning);
                    }
                    break;

                case AwakeningPhase.StasisScanning:
                    UpdateStasisScanning();
                    if (phaseTimer >= 4.5f)
                    {
                        TransitionToPhase(AwakeningPhase.PowerSurge);
                        fxAudioSource.PlayOneShot(AlitaAwakeningAudioEngine.CreatePowerSurgeSwell(4.5f), 0.85f);
                    }
                    break;

                case AwakeningPhase.PowerSurge:
                    UpdatePowerSurge();
                    if (phaseTimer >= 4.0f)
                    {
                        TransitionToPhase(AwakeningPhase.OpticActivation);
                        fxAudioSource.PlayOneShot(AlitaAwakeningAudioEngine.CreateOpticBootChime(2.8f), 0.90f);
                    }
                    break;

                case AwakeningPhase.OpticActivation:
                    UpdateOpticActivation();
                    if (phaseTimer >= 3.0f)
                    {
                        TransitionToPhase(AwakeningPhase.DepressurizeCanopy);
                        fxAudioSource.PlayOneShot(AlitaAwakeningAudioEngine.CreatePneumaticSteamHiss(3.5f), 0.80f);
                        if (cryoSteamFX != null) cryoSteamFX.Play();
                    }
                    break;

                case AwakeningPhase.DepressurizeCanopy:
                    UpdateDepressurizeCanopy();
                    if (phaseTimer >= 4.5f)
                    {
                        TransitionToPhase(AwakeningPhase.AlitaSitUp);
                    }
                    break;

                case AwakeningPhase.AlitaSitUp:
                    UpdateAlitaSitUp();
                    if (phaseTimer >= 5.5f)
                    {
                        TransitionToPhase(AwakeningPhase.InspectHands);
                        SetSubtitle("Alita: \"...What is this? Where am I?\"", 6.0f);
                    }
                    break;

                case AwakeningPhase.InspectHands:
                    UpdateInspectHands();
                    if (phaseTimer >= 7.5f)
                    {
                        TransitionToPhase(AwakeningPhase.IdoApproach);
                        StartCoroutine(IdoDialogueSequence());
                    }
                    break;

                case AwakeningPhase.IdoApproach:
                    UpdateIdoApproach();
                    if (phaseTimer >= 9.0f)
                    {
                        TransitionToPhase(AwakeningPhase.Epilogue);
                    }
                    break;

                case AwakeningPhase.Epilogue:
                    _epilogueAlpha = Mathf.MoveTowards(_epilogueAlpha, 1.0f, Time.deltaTime * 0.5f);
                    break;
            }
        }

        private void LateUpdate()
        {
            if (cutsceneCamera != null)
            {
                UpdateCinematicCamera();
            }

            // Apply procedural body posing
            ApplyProceduralPosing();
        }

        private void TransitionToPhase(AwakeningPhase next)
        {
            currentPhase = next;
            phaseTimer = 0f;
            if (next == AwakeningPhase.TerminalBoot)
            {
                _typedSound1Played = false;
                _beep1Played = false;
                _typedSound2Played = false;
                _beep2Played = false;
                _subDropPlayed = false;
            }
        }

        #region Phase Handlers

        private void UpdateTerminalBoot()
        {
            // Audio timeline triggers for tactile typing and system initialization
            if (phaseTimer >= 0.15f && !_typedSound1Played)
            {
                _typedSound1Played = true;
                if (fxAudioSource != null) fxAudioSource.PlayOneShot(AlitaAwakeningAudioEngine.CreateTypingSequence(2.1f, 36), 0.85f);
            }
            if (phaseTimer >= 2.25f && !_beep1Played)
            {
                _beep1Played = true;
                if (fxAudioSource != null) fxAudioSource.PlayOneShot(AlitaAwakeningAudioEngine.CreateTerminalBeep(920f), 0.65f);
            }
            if (phaseTimer >= 2.75f && !_typedSound2Played)
            {
                _typedSound2Played = true;
                if (fxAudioSource != null) fxAudioSource.PlayOneShot(AlitaAwakeningAudioEngine.CreateTypingSequence(2.0f, 32), 0.85f);
            }
            if (phaseTimer >= 4.85f && !_beep2Played)
            {
                _beep2Played = true;
                if (fxAudioSource != null) fxAudioSource.PlayOneShot(AlitaAwakeningAudioEngine.CreateTerminalBeep(1100f), 0.65f);
            }
            if (phaseTimer >= 5.80f && !_subDropPlayed)
            {
                _subDropPlayed = true;
                if (fxAudioSource != null) fxAudioSource.PlayOneShot(AlitaAwakeningAudioEngine.CreateIndustrialSubDrop(4.2f), 1.0f);
            }

            // Laser remains off during terminal boot
            if (scanLaserLine != null) scanLaserLine.enabled = false;

            // Faint idle glow on core
            if (quantumCoreLight != null)
            {
                quantumCoreLight.intensity = (phaseTimer >= 5.8f) ? (0.6f + Mathf.Sin(phaseTimer * 20f) * 0.3f) : 0.15f;
            }
        }

        private void UpdateStasisScanning()
        {
            // Laser sweep back and forth over Alita in pod
            if (scanLaserLine != null)
            {
                scanLaserLine.enabled = true;
                float sweepZ = Mathf.Sin(phaseTimer * 2.2f) * 0.95f;
                Vector3 podCenter = podBase != null ? podBase.position : Vector3.zero;
                scanLaserLine.SetPosition(0, podCenter + new Vector3(-0.65f, 1.25f, sweepZ));
                scanLaserLine.SetPosition(1, podCenter + new Vector3(0.65f, 1.25f, sweepZ));
            }

            // Quantum Core faint stasis glow
            if (quantumCoreLight != null)
            {
                quantumCoreLight.intensity = Mathf.PingPong(phaseTimer * 1.5f, 0.4f) + 0.2f;
            }
        }

        private void UpdatePowerSurge()
        {
            if (scanLaserLine != null) scanLaserLine.enabled = false;

            // Power surge: Core light ramps up dramatically
            float t = Mathf.Clamp01(phaseTimer / 4.0f);
            if (quantumCoreLight != null)
            {
                quantumCoreLight.intensity = Mathf.Lerp(0.5f, 4.2f, t) + Mathf.Sin(phaseTimer * 28f) * 0.35f;
            }

            // Pulse grid emissive
            if (tronGridMaterial != null)
            {
                Color pulseCol = Color.Lerp(new Color(0f, 0.4f, 0.6f), new Color(0f, 0.95f, 1f), t);
                tronGridMaterial.SetColor("_EmissionColor", pulseCol * (1.2f + Mathf.Sin(phaseTimer * 18f) * 0.4f));
            }
        }

        private void UpdateOpticActivation()
        {
            float t = Mathf.Clamp01(phaseTimer / 1.0f);
            if (leftEyeOpticLight != null) leftEyeOpticLight.intensity = Mathf.Lerp(0f, 1.8f, t);
            if (rightEyeOpticLight != null) rightEyeOpticLight.intensity = Mathf.Lerp(0f, 1.8f, t);

            if (quantumCoreLight != null)
            {
                quantumCoreLight.intensity = 3.5f + Mathf.Sin(phaseTimer * 8f) * 0.5f;
            }
        }

        private void UpdateDepressurizeCanopy()
        {
            float t = Mathf.Clamp01(phaseTimer / 3.8f);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            if (podCanopy != null)
            {
                podCanopy.localPosition = Vector3.Lerp(_canopyClosedPos, _canopyOpenPos, smoothT);
                podCanopy.localRotation = Quaternion.Slerp(_canopyClosedRot, _canopyOpenRot, smoothT);
            }
        }

        private void UpdateAlitaSitUp()
        {
            // Fully open canopy
            if (podCanopy != null)
            {
                podCanopy.localPosition = _canopyOpenPos;
                podCanopy.localRotation = _canopyOpenRot;
            }
        }

        private void UpdateInspectHands()
        {
            // Hands held up in front of face
        }

        private IEnumerator IdoDialogueSequence()
        {
            yield return new WaitForSeconds(1.0f);
            SetSubtitle("Dr. Dyson Ido: \"Easy now. Don't push yourself... your neural bridges are still calibrating.\"", 5.0f);
            yield return new WaitForSeconds(5.2f);
            SetSubtitle("Dr. Dyson Ido: \"You've been in stasis for three hundred years. Welcome back, Alita.\"", 5.5f);
        }

        private void UpdateIdoApproach()
        {
            if (doctorActor != null)
            {
                float t = Mathf.Clamp01(phaseTimer / 6.0f);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                doctorActor.transform.position = Vector3.Lerp(doctorConsolePos, doctorPodSidePos, smoothT);

                Vector3 dir = (podBase.position - doctorActor.transform.position).normalized;
                dir.y = 0;
                if (dir != Vector3.zero)
                {
                    doctorActor.transform.rotation = Quaternion.Slerp(doctorActor.transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 4f);
                }

                if (doctorAnimator != null)
                {
                    doctorAnimator.SetFloat("Speed", t < 0.95f ? 1.0f : 0f);
                }
            }
        }

        #endregion

        #region Procedural Posing

        private void ApplyProceduralPosing()
        {
            if (alitaDollBody == null) return;

            UpdateEyelidAndFacialAnimation();

            switch (currentPhase)
            {
                case AwakeningPhase.StasisScanning:
                case AwakeningPhase.PowerSurge:
                case AwakeningPhase.OpticActivation:
                case AwakeningPhase.DepressurizeCanopy:
                    // Lying flat in pod
                    PoseLyingFlat();
                    break;

                case AwakeningPhase.AlitaSitUp:
                    float sitT = Mathf.Clamp01(phaseTimer / 5.0f);
                    float smoothSit = Mathf.SmoothStep(0f, 1f, sitT);
                    PoseSitUp(smoothSit);
                    break;

                case AwakeningPhase.InspectHands:
                case AwakeningPhase.IdoApproach:
                case AwakeningPhase.Epilogue:
                    PoseInspectHands();
                    break;
            }
        }

        private void CacheBlendShapeIndices()
        {
            if (_alitaSMR == null && alitaDollBody != null)
            {
                _alitaSMR = alitaDollBody.GetComponentInChildren<SkinnedMeshRenderer>();
            }

            if (_alitaSMR != null && _alitaSMR.sharedMesh != null)
            {
                Mesh m = _alitaSMR.sharedMesh;
                _bsEyeBlinkUpperL = FindBlendShapeIndex(m, "eyeBlinkUpperLeft");
                _bsEyeBlinkUpperR = FindBlendShapeIndex(m, "eyeBlinkUpperRight");
                _bsEyeBlinkLowerL = FindBlendShapeIndex(m, "eyeBlinkLowerLeft");
                _bsEyeBlinkLowerR = FindBlendShapeIndex(m, "eyeBlinkLowerRight");
                _bsBrowInnerUpL = FindBlendShapeIndex(m, "browInnerUpLeft");
                _bsBrowInnerUpR = FindBlendShapeIndex(m, "browInnerUpRight");
            }
        }

        private int FindBlendShapeIndex(Mesh mesh, string targetName)
        {
            if (mesh == null) return -1;
            int direct = mesh.GetBlendShapeIndex(targetName);
            if (direct >= 0) return direct;
            direct = mesh.GetBlendShapeIndex("MESHBlends." + targetName);
            if (direct >= 0) return direct;
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                string n = mesh.GetBlendShapeName(i);
                if (n.EndsWith(targetName, System.StringComparison.OrdinalIgnoreCase)) return i;
            }
            return -1;
        }

        private void UpdateEyelidAndFacialAnimation()
        {
            if (_alitaSMR == null && alitaDollBody != null)
            {
                _alitaSMR = alitaDollBody.GetComponentInChildren<SkinnedMeshRenderer>();
                CacheBlendShapeIndices();
            }
            if (_alitaSMR == null) return;

            if (_bsEyeBlinkUpperL < 0)
            {
                CacheBlendShapeIndices();
            }

            float blink = 100f;
            float brow = 0f;

            switch (currentPhase)
            {
                case AwakeningPhase.TerminalBoot:
                case AwakeningPhase.StasisScanning:
                case AwakeningPhase.PowerSurge:
                    blink = 100f;
                    brow = 0f;
                    break;

                case AwakeningPhase.OpticActivation:
                    // Slowly open eyelids during the 3-second optic flare
                    float t = Mathf.Clamp01(phaseTimer / 2.2f);
                    float smooth = Mathf.SmoothStep(0f, 1f, t);
                    blink = Mathf.Lerp(100f, 0f, smooth);
                    brow = Mathf.SmoothStep(0f, 25f, t);
                    break;

                default:
                    // Open with gentle periodic blinks
                    float cycle = Time.time % 6.0f;
                    if (cycle < 0.2f)
                    {
                        float bT = cycle / 0.2f;
                        blink = Mathf.Sin(bT * Mathf.PI) * 70f;
                    }
                    else
                    {
                        blink = 0f;
                    }
                    brow = 22f + Mathf.Sin(Time.time * 0.8f) * 3f;
                    break;
            }

            float lowerBlink = blink * 0.75f;
            if (_bsEyeBlinkUpperL >= 0) _alitaSMR.SetBlendShapeWeight(_bsEyeBlinkUpperL, blink);
            if (_bsEyeBlinkUpperR >= 0) _alitaSMR.SetBlendShapeWeight(_bsEyeBlinkUpperR, blink);
            if (_bsEyeBlinkLowerL >= 0) _alitaSMR.SetBlendShapeWeight(_bsEyeBlinkLowerL, lowerBlink);
            if (_bsEyeBlinkLowerR >= 0) _alitaSMR.SetBlendShapeWeight(_bsEyeBlinkLowerR, lowerBlink);
            if (_bsBrowInnerUpL >= 0) _alitaSMR.SetBlendShapeWeight(_bsBrowInnerUpL, brow);
            if (_bsBrowInnerUpR >= 0) _alitaSMR.SetBlendShapeWeight(_bsBrowInnerUpR, brow);
        }

        private void PoseLyingFlat()
        {
            if (_alitaSpine != null) _alitaSpine.localRotation = Quaternion.identity;
            if (_alitaChest != null) _alitaChest.localRotation = Quaternion.identity;
            if (_alitaHead != null) _alitaHead.localRotation = Quaternion.identity;

            // Arms resting by sides
            if (_alitaUpperArmL != null) _alitaUpperArmL.localRotation = Quaternion.Euler(15f, 0f, 12f);
            if (_alitaLowerArmL != null) _alitaLowerArmL.localRotation = Quaternion.Euler(0f, 0f, 10f);
            if (_alitaUpperArmR != null) _alitaUpperArmR.localRotation = Quaternion.Euler(15f, 0f, -12f);
            if (_alitaLowerArmR != null) _alitaLowerArmR.localRotation = Quaternion.Euler(0f, 0f, -10f);
        }

        private void PoseSitUp(float weight)
        {
            // Spine tilts forward from 0 to 65 degrees
            if (_alitaSpine != null) _alitaSpine.localRotation = Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(-32f, 0f, 0f), weight);
            if (_alitaChest != null) _alitaChest.localRotation = Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(-33f, 0f, 0f), weight);
            if (_alitaHead != null) _alitaHead.localRotation = Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(18f, Mathf.Sin(phaseTimer * 1.5f) * 12f, 0f), weight);

            // Arms transition toward lap
            if (_alitaUpperArmL != null) _alitaUpperArmL.localRotation = Quaternion.Slerp(Quaternion.Euler(15f, 0f, 12f), Quaternion.Euler(45f, 15f, 25f), weight);
            if (_alitaLowerArmL != null) _alitaLowerArmL.localRotation = Quaternion.Slerp(Quaternion.Euler(0f, 0f, 10f), Quaternion.Euler(30f, 20f, 0f), weight);

            if (_alitaUpperArmR != null) _alitaUpperArmR.localRotation = Quaternion.Slerp(Quaternion.Euler(15f, 0f, -12f), Quaternion.Euler(45f, -15f, -25f), weight);
            if (_alitaLowerArmR != null) _alitaLowerArmR.localRotation = Quaternion.Slerp(Quaternion.Euler(0f, 0f, -10f), Quaternion.Euler(30f, -20f, 0f), weight);
        }

        private void PoseInspectHands()
        {
            // Upright torso
            if (_alitaSpine != null) _alitaSpine.localRotation = Quaternion.Euler(-32f, 0f, 0f);
            if (_alitaChest != null) _alitaChest.localRotation = Quaternion.Euler(-33f, 0f, 0f);
            if (_alitaHead != null) _alitaHead.localRotation = Quaternion.Euler(22f, Mathf.Sin(phaseTimer * 0.8f) * 8f, 0f);

            // Raise both arms in front of face to admire porcelain filigree fingers
            float wristFlex = Mathf.Sin(phaseTimer * 1.2f) * 18f;
            if (_alitaUpperArmL != null) _alitaUpperArmL.localRotation = Quaternion.Euler(75f, 25f, 35f);
            if (_alitaLowerArmL != null) _alitaLowerArmL.localRotation = Quaternion.Euler(60f, 35f, 20f);
            if (_alitaHandL != null) _alitaHandL.localRotation = Quaternion.Euler(10f, 30f + wristFlex, -15f);

            if (_alitaUpperArmR != null) _alitaUpperArmR.localRotation = Quaternion.Euler(75f, -25f, -35f);
            if (_alitaLowerArmR != null) _alitaLowerArmR.localRotation = Quaternion.Euler(60f, -35f, -20f);
            if (_alitaHandR != null) _alitaHandR.localRotation = Quaternion.Euler(10f, -30f - wristFlex, 15f);

            // Delicate procedural finger curling
            float fingerCurl = Mathf.PingPong(phaseTimer * 1.5f, 35f) + 10f;
            foreach (var f in _leftFingers)
            {
                if (f != null) f.localRotation = Quaternion.Euler(fingerCurl, 0f, 0f);
            }
            foreach (var f in _rightFingers)
            {
                if (f != null) f.localRotation = Quaternion.Euler(fingerCurl, 0f, 0f);
            }
        }

        #endregion

        #region Camera Choreography

        private void UpdateCinematicCamera()
        {
            Vector3 podCenter = podBase != null ? podBase.position : Vector3.zero;
            Vector3 alitaHeadPos = _alitaHead != null ? _alitaHead.position : podCenter + Vector3.up * 0.95f;
            Vector3 targetCamPos = cutsceneCamera.transform.position;
            Vector3 targetLookAt = alitaHeadPos;
            float targetFov = 34f;
            float smoothSpeed = 4.5f;

            switch (currentPhase)
            {
                case AwakeningPhase.TerminalBoot:
                    // 0.0s - 5.5s: OTS framing of Dr. Ido typing at diagnostic console
                    // 5.5s - 7.5s: Sub-bass drop - smooth crane swoop from console to pod establishing shot
                    Vector3 docPos = doctorActor != null ? doctorActor.transform.position : doctorConsolePos;
                    Vector3 screenPos = new Vector3(-1.60f, 1.15f, 1.20f);
                    Vector3 otsCamPos = docPos + new Vector3(-0.35f, 1.48f, -0.45f);

                    if (phaseTimer < 5.5f)
                    {
                        targetCamPos = otsCamPos;
                        targetLookAt = screenPos;
                        targetFov = 30f;
                        smoothSpeed = 5.0f;
                    }
                    else
                    {
                        // Swoop toward pod establishing position
                        float swoopT = Mathf.Clamp01((phaseTimer - 5.5f) / 2.0f);
                        float smoothSwoop = Mathf.SmoothStep(0f, 1f, swoopT);
                        Vector3 podEstablishingPos = podCenter + new Vector3(-2.6f, 2.2f, -1.4f);
                        Vector3 podLookAt = podCenter + new Vector3(0f, 1.45f, 0.35f);

                        targetCamPos = Vector3.Lerp(otsCamPos, podEstablishingPos, smoothSwoop);
                        targetLookAt = Vector3.Lerp(screenPos, podLookAt, smoothSwoop);
                        targetFov = Mathf.Lerp(30f, 34f, smoothSwoop);
                        smoothSpeed = 4.0f;
                    }
                    break;

                case AwakeningPhase.StasisScanning:
                    // Establishing 3/4 tracking shot of glowing pod and stasis bed
                    targetCamPos = podCenter + new Vector3(-2.6f, 2.2f, -1.4f);
                    targetLookAt = podCenter + new Vector3(0f, 1.45f, 0.35f);
                    targetFov = 34f;
                    break;

                case AwakeningPhase.PowerSurge:
                    // High-angle spiral crane shot revealing the glowing Tron floor grid
                    float spiralAngle = (phaseTimer * 16f - 40f) * Mathf.Deg2Rad;
                    targetCamPos = podCenter + new Vector3(Mathf.Sin(spiralAngle) * 3.4f, 3.2f, Mathf.Cos(spiralAngle) * 3.4f);
                    targetLookAt = podCenter + new Vector3(0f, 1.40f, 0.35f);
                    targetFov = 38f;
                    break;

                case AwakeningPhase.OpticActivation:
                    // Dramatic snap push-in with Dutch angle on Alita's glowing eyes
                    targetCamPos = alitaHeadPos + new Vector3(-0.65f, 0.55f, -0.85f);
                    targetLookAt = alitaHeadPos + Vector3.up * 0.05f;
                    targetFov = 26f;
                    smoothSpeed = 7.0f;
                    break;

                case AwakeningPhase.DepressurizeCanopy:
                    // Front tracking dolly as steam parts and canopy ascends
                    targetCamPos = podCenter + new Vector3(-2.2f, 1.95f, -1.2f);
                    targetLookAt = podCenter + new Vector3(0f, 1.55f, 0.35f);
                    targetFov = 34f;
                    break;

                case AwakeningPhase.AlitaSitUp:
                    // Hero eye-level track as Alita sits upright
                    targetCamPos = alitaHeadPos + new Vector3(-1.85f, 0.40f, -1.15f);
                    targetLookAt = alitaHeadPos + Vector3.down * 0.10f;
                    targetFov = 32f;
                    break;

                case AwakeningPhase.InspectHands:
                    // Clean hero 3/4 framing of porcelain filigree hands
                    targetCamPos = alitaHeadPos + new Vector3(-1.45f, 0.30f, -0.85f);
                    targetLookAt = _alitaHandL != null ? _alitaHandL.position : (alitaHeadPos + Vector3.down * 0.15f);
                    targetFov = 28f;
                    break;

                case AwakeningPhase.IdoApproach:
                case AwakeningPhase.Epilogue:
                    // Cinematic two-shot establishing Dr. Ido beside pod talking with Alita
                    Vector3 idoPos = doctorActor != null ? doctorActor.transform.position : doctorPodSidePos;
                    targetCamPos = new Vector3(-3.4f, 2.1f, -0.9f);
                    targetLookAt = Vector3.Lerp(idoPos + Vector3.up * 1.4f, alitaHeadPos, 0.5f);
                    targetFov = 36f;
                    break;
            }

            // Smooth interpolation
            cutsceneCamera.transform.position = Vector3.Lerp(cutsceneCamera.transform.position, targetCamPos, Time.deltaTime * smoothSpeed);
            Vector3 lookDir = (targetLookAt - cutsceneCamera.transform.position).normalized;
            if (lookDir != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDir);
                cutsceneCamera.transform.rotation = Quaternion.Slerp(cutsceneCamera.transform.rotation, targetRot, Time.deltaTime * smoothSpeed);
            }
            cutsceneCamera.fieldOfView = Mathf.Lerp(cutsceneCamera.fieldOfView, targetFov, Time.deltaTime * 3.0f);
        }

        #endregion

        #region UI & Presentation

        public void SetSubtitle(string text, float duration)
        {
            _currentSubtitle = text;
            _subtitleTimer = duration;

            if (!string.IsNullOrEmpty(text))
            {
                CyberpunkDialogueHUD.GetOrCreate().ShowSubtitle(text, duration);
            }
        }

        private void OnGUI()
        {
            int sw = Screen.width;
            int sh = Screen.height;

            // 1. Tron: Ares Terminal Boot UI Overlay
            if (currentPhase == AwakeningPhase.TerminalBoot)
            {
                DrawTerminalBootUI(sw, sh);
            }

            // 2. Subtitles rendered via AAA CyberpunkDialogueHUD (Canvas uGUI)

            // 3. Epilogue Banner
            if (_epilogueAlpha > 0.01f)
            {
                GUI.color = new Color(0f, 0f, 0f, _epilogueAlpha * 0.7f);
                GUI.DrawTexture(new Rect(0, sh * 0.38f, sw, 120), Texture2D.whiteTexture);

                GUIStyle epStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.Clamp(sw / 46, 24, 38),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                epStyle.normal.textColor = new Color(0f, 0.95f, 1f, _epilogueAlpha);
                GUI.Label(new Rect(0, sh * 0.40f, sw, 44), "CHAPTER I // THE REBIRTH", epStyle);

                GUIStyle subEpStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.Clamp(sw / 80, 12, 17),
                    alignment = TextAnchor.MiddleCenter
                };
                subEpStyle.normal.textColor = new Color(1f, 1f, 1f, _epilogueAlpha * 0.9f);
                GUI.Label(new Rect(0, sh * 0.40f + 45, sw, 30), "ALITA IS AWAKENED // DOLL BODY ONLINE", subEpStyle);
            }
        }

        private void DrawTerminalBootUI(int sw, int sh)
        {
            float alpha = 1.0f;
            if (phaseTimer > 5.8f)
            {
                alpha = Mathf.Clamp01(1.0f - ((phaseTimer - 5.8f) / 1.7f));
            }
            if (alpha <= 0.001f) return;

            // High-Tech Tron CLI Console Frame
            int panelW = Mathf.Min(820, (int)(sw * 0.78f));
            int panelH = Mathf.Min(440, (int)(sh * 0.62f));
            int panelX = (sw - panelW) / 2;
            int panelY = (int)(sh * 0.16f);

            // Semi-transparent CRT Glass Background
            GUI.color = new Color(0.01f, 0.04f, 0.07f, 0.88f * alpha);
            GUI.DrawTexture(new Rect(panelX, panelY, panelW, panelH), Texture2D.whiteTexture);

            // Glowing Tron Cyan Border
            GUI.color = new Color(0f, 0.85f, 1.0f, 0.75f * alpha);
            GUI.DrawTexture(new Rect(panelX, panelY, panelW, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(panelX, panelY + panelH - 2, panelW, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(panelX, panelY, 2, panelH), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(panelX + panelW - 2, panelY, 2, panelH), Texture2D.whiteTexture);

            // Terminal Header Bar
            GUI.color = new Color(0.02f, 0.10f, 0.16f, 0.95f * alpha);
            GUI.DrawTexture(new Rect(panelX + 2, panelY + 2, panelW - 4, 28), Texture2D.whiteTexture);

            GUIStyle headStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            headStyle.normal.textColor = new Color(0f, 0.9f, 1f, alpha);
            GUI.Label(new Rect(panelX + 14, panelY + 4, panelW - 30, 24), "◈ DR. DYSON IDO // NEURAL DIAGNOSTIC TERMINAL [SYS_INIT v4.19]", headStyle);

            // Blinking cursor string
            string cursor = (Mathf.FloorToInt(Time.time * 2.8f) % 2 == 0) ? "█" : " ";

            // Monospace CLI Text Setup
            GUIStyle cliStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Clamp(sw / 85, 12, 15),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft
            };

            int lineY = panelY + 40;
            int lineSpacing = 22;

            // Command 1: Typing sudo systemctl start alita-core.service
            string cmd1Target = "sudo systemctl start alita-core.service";
            string prompt = "dyson-ido@clinic:~$ ";

            if (phaseTimer >= 0.15f)
            {
                float t1 = Mathf.Clamp01((phaseTimer - 0.15f) / 2.05f);
                int charCount = Mathf.Clamp((int)(t1 * cmd1Target.Length), 0, cmd1Target.Length);
                string typed1 = cmd1Target.Substring(0, charCount);

                cliStyle.normal.textColor = new Color(0.2f, 0.85f, 0.95f, alpha);
                string fullLine1 = prompt + typed1 + (phaseTimer < 2.25f ? cursor : "");
                GUI.Label(new Rect(panelX + 16, lineY, panelW - 32, 22), fullLine1, cliStyle);
                lineY += lineSpacing;
            }

            // Systemd Service Output
            if (phaseTimer >= 2.25f)
            {
                GUIStyle okStyle = new GUIStyle(cliStyle);
                okStyle.normal.textColor = new Color(0.15f, 1.0f, 0.45f, alpha);
                GUI.Label(new Rect(panelX + 16, lineY, 90, 22), "[  OK  ]", okStyle);
                cliStyle.normal.textColor = new Color(0.85f, 0.90f, 0.95f, alpha);
                GUI.Label(new Rect(panelX + 90, lineY, panelW - 110, 22), "Stopped Stasis Cryo Lock.", cliStyle);
                lineY += lineSpacing;

                okStyle.normal.textColor = new Color(0.15f, 1.0f, 0.45f, alpha);
                GUI.Label(new Rect(panelX + 16, lineY, 90, 22), "[  OK  ]", okStyle);
                GUI.Label(new Rect(panelX + 90, lineY, panelW - 110, 22), "Mounted /dev/cyber/doll_body_v2.", cliStyle);
                lineY += lineSpacing;

                okStyle.normal.textColor = new Color(0.15f, 1.0f, 0.45f, alpha);
                GUI.Label(new Rect(panelX + 16, lineY, 90, 22), "[  OK  ]", okStyle);
                GUI.Label(new Rect(panelX + 90, lineY, panelW - 110, 22), "Initialized Bio-Neural Bridge Driver (v4.19-cyber).", cliStyle);
                lineY += lineSpacing;
            }

            // Command 2: ./cyber_init.sh --protocol=awaken --target=alita_99
            string cmd2Target = "./cyber_init.sh --protocol=awaken --target=alita_99";
            if (phaseTimer >= 2.75f)
            {
                float t2 = Mathf.Clamp01((phaseTimer - 2.75f) / 2.05f);
                int charCount2 = Mathf.Clamp((int)(t2 * cmd2Target.Length), 0, cmd2Target.Length);
                string typed2 = cmd2Target.Substring(0, charCount2);

                cliStyle.normal.textColor = new Color(0.2f, 0.85f, 0.95f, alpha);
                string fullLine2 = prompt + typed2 + (phaseTimer < 4.85f ? cursor : "");
                GUI.Label(new Rect(panelX + 16, lineY, panelW - 32, 22), fullLine2, cliStyle);
                lineY += lineSpacing;
            }

            // Kernel Execution Output
            if (phaseTimer >= 4.85f)
            {
                cliStyle.normal.textColor = new Color(1.0f, 0.82f, 0.25f, alpha);
                GUI.Label(new Rect(panelX + 16, lineY, panelW - 32, 22), "> KERNEL: Connecting to heart core [ANTIMATTER/UCRM-01]...", cliStyle);
                lineY += lineSpacing;

                GUI.Label(new Rect(panelX + 16, lineY, panelW - 32, 22), "> CORE VOLTAGE: 1.21 GW [SYNCHRONIZED]", cliStyle);
                lineY += lineSpacing;

                cliStyle.normal.textColor = new Color(0f, 0.95f, 1.0f, alpha);
                GUI.Label(new Rect(panelX + 16, lineY, panelW - 32, 22), "> INITIATING SYNAPSE RE-ENGAGEMENT SEQUENCE...", cliStyle);
                lineY += lineSpacing;
            }

            // Final Activation Banner (Nine Inch Nails Sub-Bass Drop)
            if (phaseTimer >= 5.80f)
            {
                GUIStyle alertStyle = new GUIStyle(cliStyle);
                alertStyle.normal.textColor = new Color(0.15f, 1.0f, 0.45f, alpha);
                GUI.Label(new Rect(panelX + 16, lineY, 90, 22), "[  OK  ]", alertStyle);
                alertStyle.normal.textColor = new Color(0.1f, 1.0f, 0.95f, alpha);
                GUI.Label(new Rect(panelX + 90, lineY, panelW - 110, 22), "ALITA SYSTEM AWAKENING COMMENCED.", alertStyle);
                lineY += lineSpacing;

                alertStyle.normal.textColor = new Color(1.0f, 0.35f, 0.25f, alpha);
                GUI.Label(new Rect(panelX + 16, lineY, panelW - 32, 22), ">>> CRYO CANOPY UNLATCH SEQUENCE ARMED <<<", alertStyle);
            }
        }

        #endregion
    }
}
