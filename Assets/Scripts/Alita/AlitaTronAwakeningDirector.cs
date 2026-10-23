using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Random = UnityEngine.Random;

namespace Alita
{
    public enum AlitaAwakeningPhase
    {
        IdoTerminalTyping,      // 0.0s - 9.8s: Dr. Ido at physical console typing on keyboard, live text ON CONSOLE ASSET (no 2D HUD)
        CursorFlashDetonation, // 9.8s - 10.2s: White cursor on terminal screen explodes into blinding flash!
        EyePupilRadialBurst,    // 10.2s - 32.0s: INSIDE ALITA/ARES EYE PUPIL: massive radial sphere of millions of orange/red circuit light beams radiating 360° into infinity!
        EyeZoomOut,             // 32.0s - 48.0s: Slowly zooming out from pupil, revealing the cybernetic iris and head
        NanoBodyConstruction,   // 48.0s - 72.5s: Nano-construction of cybernetic body: molten voxels compiling spine & flexing cyber hand
        DigitizationComplete,   // 72.5s - 76.5s: 98% -> DIGITIZATION COMPLETE & overhead slab glide
        WideSlabSilhouette,     // 76.5s - 80.0s (1:20): The iconic wide profile silhouette sit-up on floating slab in 6-sided Red Grid Chamber!
        Touchdown,              // 80.0s - 86.0s: Boots touchdown on obsidian mirror floor rippling reflections
        Confrontation           // 86.0s - 120.0s: Hero combat stance framing Alita & Dr. Ido
    }

    /// <summary>
    /// Master 120-second Cinematic Cutscene Director for Alita's TRON: Ares Awakening Scene.
    /// Beat-for-beat matching the full IMAX opening sequence:
    /// - 0:00 - 0:10: Dr. Dyson Ido typing boot commands live on the physical terminal console asset (NOT as a 2D HUD)
    /// - 0:10 - 0:10.2: Dr. Ido slams Enter -> white cursor flashes blindingly and detonates
    /// - 0:10.2 - 0:32: INSIDE THE EYE PUPIL OF ALITA / ARES: massive radial sphere of millions of bright orange/red circuit light beams radiating 360 degrees outward into infinity!
    /// - 0:32 - 0:48: Slowly zooming out from pupil, revealing the cybernetic iris and head
    /// - 0:48 - 0:72.5: Nano-construction of Alita's cybernetic body on the slab: molten voxels compiling spine, shoulders & cyber hand flex
    /// - 0:72.5 - 0:76.5: Overhead slab glide & 98% -> DIGITIZATION COMPLETE
    /// - 0:76.5 - 0:80 (1:20): Sub-bass drop HARD CUT to the Iconic Wide Profile Silhouette Sit-Up in the 6-sided Red Grid Chamber!
    /// - 0:80 - 0:86: Obsidian mirror floor touchdown with rippling neon reflections
    /// - 0:86 - 1:20+: Hero 3/4 combat ready stance framing Alita & Dr. Ido
    /// </summary>
    public class AlitaTronAwakeningDirector : MonoBehaviour
    {
        public static AlitaTronAwakeningDirector Instance { get; private set; }

        [Header("Playback State")]
        public AlitaAwakeningPhase currentPhase = AlitaAwakeningPhase.IdoTerminalTyping;
        public float currentTime = 0f;
        public float totalDuration = 84.0f;
        public bool isPlaying = true;
        public bool loopAfterFinish = false;

        [Header("Scene Stitching & Gameplay Hand-off")]
        public float introFadeDuration = 1.2f;
        public string nextGameplaySceneName = "Demo_Interior";
        public float endTransitionCardDuration = 4.5f;
        public bool autoAdvanceToGameplay = true;
        public bool allowSkipToGameplay = true;

        [Header("Cinematic Title Card Settings (80.5s - 84.0s)")]
        [Tooltip("The year text displayed on the cinematic title card")]
        public string titleCardYear = "2563";
        [Tooltip("Subtitle text displayed underneath the year and accent line")]
        public string titleCardSubtitle = "THREE CENTURIES AFTER THE GREAT WAR";
        [Tooltip("Location/Clinic text displayed under the subtitle")]
        public string titleCardLocation = "DR. IDO'S CYBER-REPAIR CLINIC // SECTOR 7";
        [Tooltip("Font scale multiplier for the year (default 1.7x for commanding cinematic presence)")]
        [Range(1.0f, 3.0f)]
        public float titleCardYearScale = 1.7f;

        private float _introFadeAlpha = 1.0f;
        private float _endFadeAlpha = 0.0f;
        private bool _isEnding = false;
        private bool _isLoadingGameplay = false;
        private float _gameplayLoadProgress = 0f;

        [Header("Debug & Diagnostics")]
        [Tooltip("Displays live film timecode, elapsed seconds, frame count, and phase name")]
        public bool showTimestampHUD = true;

        [Header("Actors & Props")]
        public GameObject alitaActor;
        public GameObject chamberGridRoot;
        public Transform slabTransform;
        public Transform eyePupilVFXTransform;
        public TronEyePupilRadialBurstVFX eyePupilVFX;
        public IdoTerminalTypingController idoController;
        public Transform terminalTransform;
        public TronTerminalScreenAsset terminalScreen;

        [Header("TRON Orbital Eye Sequence (32s - 48s)")]
        public TronOrbitalEyeSequenceVFX orbitalEyeVFX;

        [Header("Master Character Reconstruction VFX System (10.2s - 64.0s)")]
        public AlitaTronAwakeningReconstructionVFXSystem reconstructionVFX;

        [Header("Lighting & VFX")]
        public Light slabEdgeLight;
        public Light opticLeftLight;
        public Light opticRightLight;
        public Light faceKeyLight;
        public Light overheadScanLight;

        [Header("TRON Identity Disc")]
        public TronIdentityDisc identityDisc;

        [Header("Audio")]
        public AudioSource musicSource;
        public AudioSource fxSource;

        [Header("Camera Rig")]
        public Camera mainCamera;

        [Header("Facial & Eyelid Awakening Animation")]
        [Tooltip("Procedural blendshape control for slow eyelid opening during EyeZoomOut")]
        public bool enableFacialAnimation = true;
        [Tooltip("Live evaluated eyelid blink weight (0 = fully open, 100 = fully closed)")]
        [Range(0f, 100f)]
        public float currentBlinkWeight = 100f;

        // Cached Facial BlendShape Indices
        private int _bsEyeBlinkUpperL = -1;
        private int _bsEyeBlinkUpperR = -1;
        private int _bsEyeBlinkLowerL = -1;
        private int _bsEyeBlinkLowerR = -1;
        private int _bsBrowInnerUpL = -1;
        private int _bsBrowInnerUpR = -1;
        private int _bsEyeSquintL = -1;
        private int _bsEyeSquintR = -1;

        // Compilation Shader Material instances
        private List<Material> _alitaMaterials = new List<Material>();
        private Animator _alitaAnim;
        private SkinnedMeshRenderer _alitaSMR;

        // Procedural Bone Transforms for Alita
        private Transform _hips;
        private Transform _spine;
        private Transform _chest;
        private Transform _neck;
        private Transform _head;
        private Transform _armL;
        private Transform _forearmL;
        private Transform _handL;
        private Transform _armR;
        private Transform _forearmR;
        private Transform _handR;
        private Transform _thighL;
        private Transform _calfL;
        private Transform _footL;
        private Transform _thighR;
        private Transform _calfR;
        private Transform _footR;

        // Resting bone local rotations
        private Transform _spine01;
        private Transform _spine02;
        private Transform _spine03;
        private Quaternion _hipsRest;
        private Quaternion _s1Rest;
        private Quaternion _s2Rest;
        private Quaternion _s3Rest;
        private Quaternion _spineRest;
        private Quaternion _chestRest;
        private Quaternion _neckRest;
        private Quaternion _headRest;
        private Quaternion _armLPrefabRest;
        private Quaternion _armRPrefabRest;
        private Quaternion _armLRest;
        private Quaternion _forearmLRest;
        private Quaternion _handLRest;
        private Quaternion _armRRest;
        private Quaternion _forearmRRest;
        private Quaternion _handRRest;
        private Quaternion _thighLRest;
        private Quaternion _calfLRest;
        private Quaternion _thighRRest;
        private Quaternion _calfRRest;
        private Quaternion _footLRest;
        private Quaternion _footRRest;

        // Initial actor placement
        private Vector3 _alitaLiePos;
        private Quaternion _alitaLieRot;
        private Vector3 _alitaSitPos;
        private Vector3 _alitaStandPos;

        // Cinematic UI Styles (ONLY for Title Cards and Final HUD)
        private GUIStyle _timecodeStyle;
        private GUIStyle _hudHeaderStyle;
        private GUIStyle _creditStyle;
        private Texture2D _blackPixel;
        private Texture2D _whitePixel;

        private float _lastKeystrokeSfxTime;
        private bool _hasPlayedEnterTone;
        private bool _hasPlayedDetonationTone;
        private bool _hasPlayedDropTone;

        private void Awake()
        {
            Instance = this;
            CacheBones();
            CacheMaterials();
            SetupRestPoses();
        }

        private void Start()
        {
            if (mainCamera == null) mainCamera = Camera.main;

            currentTime = 0f;
            currentPhase = AlitaAwakeningPhase.IdoTerminalTyping;
            isPlaying = true;
            _introFadeAlpha = 1.0f;
            _endFadeAlpha = 0.0f;
            _isEnding = false;
            _isLoadingGameplay = false;
            _hasPlayedEnterTone = false;
            _hasPlayedDetonationTone = false;
            _hasPlayedDropTone = false;
            _lastKeystrokeSfxTime = 0f;

            EvaluateAtTime(0f);

            if (musicSource != null)
            {
                var clip = Resources.Load<AudioClip>("NIN_Init_Full_0_to_120");
#if UNITY_EDITOR
                if (clip == null)
                {
                    clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/NIN_Init_Full_0_to_120.wav");
                }
#endif
                if (clip != null)
                {
                    musicSource.clip = clip;
                }
                musicSource.loop = false;
                musicSource.time = 0f;
                musicSource.Play();
            }

            if (fxSource != null)
            {
                fxSource.loop = false;
            }

            PlaySound(75f, 0.8f, 0.35f);
        }

        [ContextMenu("Restart Cutscene From First")]
        public void RestartCutscene()
        {
            currentTime = 0f;
            currentPhase = AlitaAwakeningPhase.IdoTerminalTyping;
            isPlaying = true;
            _introFadeAlpha = 1.0f;
            _endFadeAlpha = 0.0f;
            _isEnding = false;
            _isLoadingGameplay = false;
            _hasPlayedEnterTone = false;
            _hasPlayedDetonationTone = false;
            _hasPlayedDropTone = false;
            _lastKeystrokeSfxTime = 0f;

            EvaluateAtTime(0f);

            if (musicSource != null && musicSource.clip != null)
            {
                musicSource.loop = false;
                musicSource.time = 0f;
                musicSource.Play();
            }
        }

        [ContextMenu("Evaluate At 0s")]
        public void EvaluateAtFirstFrame()
        {
            currentTime = 0f;
            currentPhase = AlitaAwakeningPhase.IdoTerminalTyping;
            EvaluateAtTime(0f);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (this != null && !Application.isPlaying)
                    {
                        EvaluateAtTime(currentTime);
                    }
                };
            }
        }
#endif

        public void CacheBones()
        {
            if (alitaActor == null) return;

            _alitaAnim = alitaActor.GetComponent<Animator>();
            _alitaSMR = alitaActor.GetComponentInChildren<SkinnedMeshRenderer>();
            if (_alitaSMR != null)
            {
                _alitaSMR.forceMatrixRecalculationPerRender = true;
            }
            CacheBlendShapeIndices();

            if (_alitaAnim != null && _alitaAnim.isHuman)
            {
                _hips = _alitaAnim.GetBoneTransform(HumanBodyBones.Hips);
                _spine01 = _hips != null ? _hips.Find("spine_01") : null;
                _spine02 = _spine01 != null ? _spine01.Find("spine_02") : null;
                _spine03 = _spine02 != null ? _spine02.Find("spine_03") : null;
                _spine = _alitaAnim.GetBoneTransform(HumanBodyBones.Spine);
                _chest = _alitaAnim.GetBoneTransform(HumanBodyBones.Chest) ?? _spine;
                _neck = _alitaAnim.GetBoneTransform(HumanBodyBones.Neck);
                _head = _alitaAnim.GetBoneTransform(HumanBodyBones.Head);
                _armL = _alitaAnim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                _forearmL = _alitaAnim.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                _handL = _alitaAnim.GetBoneTransform(HumanBodyBones.LeftHand);
                _armR = _alitaAnim.GetBoneTransform(HumanBodyBones.RightUpperArm);
                _forearmR = _alitaAnim.GetBoneTransform(HumanBodyBones.RightLowerArm);
                _handR = _alitaAnim.GetBoneTransform(HumanBodyBones.RightHand);
                _thighL = _alitaAnim.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                _calfL = _alitaAnim.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                _footL = _alitaAnim.GetBoneTransform(HumanBodyBones.LeftFoot);
                _thighR = _alitaAnim.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                _calfR = _alitaAnim.GetBoneTransform(HumanBodyBones.RightLowerLeg);
                _footR = _alitaAnim.GetBoneTransform(HumanBodyBones.RightFoot);

                _alitaAnim.enabled = false;
            }

            if (identityDisc == null && _chest != null)
            {
                identityDisc = _chest.GetComponentInChildren<TronIdentityDisc>();
            }

            // Load authentic immutable bind pose from prefab to prevent any twisted rest poses
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Alita_Sidekick/Alita_HumanCustom_DollBody.prefab");
            var pAnim = prefab != null ? prefab.GetComponent<Animator>() : null;
            if (pAnim != null)
            {
                Transform pHips = pAnim.GetBoneTransform(HumanBodyBones.Hips);
                Transform pS1 = pHips != null ? pHips.Find("spine_01") : null;
                Transform pS2 = pS1 != null ? pS1.Find("spine_02") : null;
                Transform pS3 = pS2 != null ? pS2.Find("spine_03") : null;
                Transform pNeck = pS3 != null ? pS3.Find("neck_01") : null;
                Transform pHead = pNeck != null ? pNeck.Find("head") : null;

                _hipsRest = pHips != null ? pHips.localRotation : Quaternion.identity;
                _s1Rest = pS1 != null ? pS1.localRotation : Quaternion.identity;
                _s2Rest = pS2 != null ? pS2.localRotation : Quaternion.identity;
                _s3Rest = pS3 != null ? pS3.localRotation : Quaternion.identity;
                _spineRest = _s1Rest;
                _chestRest = _s2Rest;
                _neckRest = pNeck != null ? pNeck.localRotation : Quaternion.identity;
                _headRest = pHead != null ? pHead.localRotation : Quaternion.identity;

                _armLPrefabRest = pAnim.GetBoneTransform(HumanBodyBones.LeftUpperArm).localRotation;
                _forearmLRest = pAnim.GetBoneTransform(HumanBodyBones.LeftLowerArm).localRotation;
                _handLRest = pAnim.GetBoneTransform(HumanBodyBones.LeftHand).localRotation;

                _armRPrefabRest = pAnim.GetBoneTransform(HumanBodyBones.RightUpperArm).localRotation;
                _forearmRRest = pAnim.GetBoneTransform(HumanBodyBones.RightLowerArm).localRotation;
                _handRRest = pAnim.GetBoneTransform(HumanBodyBones.RightHand).localRotation;

                // Natural arms alongside hips on slab
                _armLRest = _armLPrefabRest * Quaternion.Euler(0f, -80f, 0f);
                _armRRest = _armRPrefabRest * Quaternion.Euler(0f, 80f, 0f);

                _thighLRest = pAnim.GetBoneTransform(HumanBodyBones.LeftUpperLeg).localRotation;
                _calfLRest = pAnim.GetBoneTransform(HumanBodyBones.LeftLowerLeg).localRotation;
                _footLRest = pAnim.GetBoneTransform(HumanBodyBones.LeftFoot).localRotation;
                _thighRRest = pAnim.GetBoneTransform(HumanBodyBones.RightUpperLeg).localRotation;
                _calfRRest = pAnim.GetBoneTransform(HumanBodyBones.RightLowerLeg).localRotation;
                _footRRest = pAnim.GetBoneTransform(HumanBodyBones.RightFoot).localRotation;
                return;
            }
#endif
            if (_hips != null) _hipsRest = _hips.localRotation;
            if (_spine01 != null) _s1Rest = _spine01.localRotation;
            if (_spine02 != null) _s2Rest = _spine02.localRotation;
            if (_spine03 != null) _s3Rest = _spine03.localRotation;
            _spineRest = _s1Rest;
            _chestRest = _s2Rest;
            if (_neck != null) _neckRest = _neck.localRotation;
            if (_head != null) _headRest = _head.localRotation;
            if (_armL != null) _armLPrefabRest = _armL.localRotation;
            if (_forearmL != null) _forearmLRest = _forearmL.localRotation;
            if (_handL != null) _handLRest = _handL.localRotation;
            if (_armR != null) _armRPrefabRest = _armR.localRotation;
            if (_forearmR != null) _forearmRRest = _forearmR.localRotation;
            if (_handR != null) _handRRest = _handR.localRotation;
            _armLRest = _armLPrefabRest * Quaternion.Euler(0f, -80f, 0f);
            _armRRest = _armRPrefabRest * Quaternion.Euler(0f, 80f, 0f);
            if (_thighL != null) _thighLRest = _thighL.localRotation;
            if (_calfL != null) _calfLRest = _calfL.localRotation;
            if (_footL != null) _footLRest = _footL.localRotation;
            if (_thighR != null) _thighRRest = _thighR.localRotation;
            if (_calfR != null) _calfRRest = _calfR.localRotation;
            if (_footR != null) _footRRest = _footR.localRotation;
        }

        public void CacheMaterials()
        {
            if (alitaActor == null) return;
            var renderers = alitaActor.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                foreach (var m in (Application.isPlaying ? r.materials : r.sharedMaterials))
                {
                    if (m != null && m.HasProperty("_CompileProgress") && !_alitaMaterials.Contains(m))
                    {
                        _alitaMaterials.Add(m);
                    }
                }
            }
        }

        public void SetupRestPoses()
        {
            Vector3 slabPos = slabTransform != null ? slabTransform.position : new Vector3(0f, 0.65f, 0f);
            _alitaLiePos = new Vector3(0f, 0.74f, -0.82f);
            _alitaSitPos = new Vector3(0f, 0.74f, -0.82f);
            _alitaStandPos = new Vector3(0f, 0.74f, -0.82f);

            _alitaLieRot = Quaternion.Euler(-90f, 180f, 0f);

            if (alitaActor != null)
            {
                alitaActor.transform.position = _alitaLiePos;
                alitaActor.transform.rotation = _alitaLieRot;
            }
        }

        public void EnsureReconstructionVFX()
        {
            if (reconstructionVFX == null)
            {
                reconstructionVFX = GetComponentInChildren<AlitaTronAwakeningReconstructionVFXSystem>();
            }
            if (reconstructionVFX == null)
            {
                GameObject vfxGO = new GameObject("Alita_Reconstruction_VFX_System");
                vfxGO.transform.SetParent(this.transform, false);
                reconstructionVFX = vfxGO.AddComponent<AlitaTronAwakeningReconstructionVFXSystem>();

                var whiteMat = LoadMat("Assets/Materials/Tron/Mat_Tron_White_Core.mat");
                var ambMat = LoadMat("Assets/Materials/Tron/Mat_Tron_Amber_Highlight.mat");
                var redOrgMat = LoadMat("Assets/Materials/Tron/Mat_Tron_Red_Orange_Neon.mat");
                var deepRMat = LoadMat("Assets/Materials/Tron/Mat_Tron_Deep_Red_Neon.mat");
                var blkMat = LoadMat("Assets/Materials/Tron/Mat_Tron_Black_Slab.mat");
                var mirMat = LoadMat("Assets/Materials/Tron/Mat_Tron_Obsidian_Mirror.mat");

                reconstructionVFX.InitializeVFX(alitaActor != null ? alitaActor.transform : null, whiteMat, ambMat, redOrgMat, deepRMat, blkMat, mirMat);
            }
        }

#if UNITY_EDITOR
        private Material LoadMat(string path)
        {
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
        }
#else
        private Material LoadMat(string path) { return null; }
#endif

        public void EvaluateAtTime(float time)
        {
            currentTime = time;
            if (_hips == null) CacheBones();
            if (_alitaMaterials == null || _alitaMaterials.Count == 0) CacheMaterials();
            EnsureReconstructionVFX();
            SetupRestPoses();
            DeterminePhase();
            UpdateShaderCompilation();

            if (reconstructionVFX != null)
            {
                reconstructionVFX.EvaluateAtTime(time);
            }

            if (eyePupilVFX != null)
            {
                eyePupilVFX.gameObject.SetActive(false);
            }

            if (orbitalEyeVFX != null)
            {
                orbitalEyeVFX.gameObject.SetActive(false);
            }

            if (identityDisc == null)
            {
                identityDisc = GetComponentInChildren<TronIdentityDisc>(true);
            }
            if (identityDisc != null)
            {
                bool isDiscActive = (time >= 60.0f);
                identityDisc.gameObject.SetActive(isDiscActive);
                if (isDiscActive)
                {
                    identityDisc.transform.localPosition = new Vector3(0.04f, 0.0f, 0.0f);
                    identityDisc.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                }
            }

            UpdateCinematicCamera();
            UpdateLighting();
            UpdateProceduralKinematics();

            if (terminalScreen != null)
            {
                terminalScreen.UpdateScreenAtTime(time);
            }
        }

        private void HandleGameplayTransitionInput()
        {
            if (_isLoadingGameplay) return;

            // Quick skip to mission gameplay for developer & player convenience
            if (allowSkipToGameplay && currentTime < 80.5f)
            {
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Tab))
                {
                    TriggerTransitionToGameplay();
                    return;
                }
            }

            // Advance into Clinic Lab during the 2563 title card
            if (currentTime >= 80.5f)
            {
                if (Input.GetKeyDown(KeyCode.Space) || 
                    Input.GetKeyDown(KeyCode.Return) || 
                    Input.GetKeyDown(KeyCode.KeypadEnter) || 
                    Input.GetMouseButtonDown(0) ||
                    Input.anyKeyDown)
                {
                    TriggerTransitionToGameplay();
                }
            }
        }

        private void Update()
        {
            // 1. Always process input and scene transitions even if cutscene playback reached the end
            HandleGameplayTransitionInput();

            if (_isEnding && !_isLoadingGameplay && _endFadeAlpha >= 0.95f)
            {
                TriggerTransitionToGameplay();
            }

            if (!isPlaying) return;

            // 2. Intro fade from black (smooth handoff from Cutscene 1)
            if (_introFadeAlpha > 0f)
            {
                _introFadeAlpha = Mathf.MoveTowards(_introFadeAlpha, 0f, Time.deltaTime / Mathf.Max(0.1f, introFadeDuration));
            }

            // 3. 1:20 Music Fade-Out (User Request: "and tron cutscene stop the music at 1:20 like fade away")
            if (currentTime >= 80.0f && musicSource != null && musicSource.isPlaying && musicSource.volume > 0f)
            {
                musicSource.volume = Mathf.MoveTowards(musicSource.volume, 0f, Time.deltaTime * 0.45f);
            }

            // 4. Time progression
            if (!_isLoadingGameplay)
            {
                currentTime += Time.deltaTime;
            }

            if (currentTime >= totalDuration)
            {
                if (loopAfterFinish)
                {
                    currentTime = 0f;
                    Start();
                }
                else
                {
                    currentTime = totalDuration;
                    // Do NOT set isPlaying = false to prevent freezing input!
                    if (autoAdvanceToGameplay && !_isEnding && !_isLoadingGameplay)
                    {
                        TriggerTransitionToGameplay();
                    }
                }
            }

            DeterminePhase();
            UpdateShaderCompilation();
            EnsureReconstructionVFX();

            if (reconstructionVFX != null)
            {
                reconstructionVFX.EvaluateAtTime(currentTime);
            }

            if (eyePupilVFX != null)
            {
                eyePupilVFX.gameObject.SetActive(false);
            }

            if (orbitalEyeVFX != null)
            {
                orbitalEyeVFX.gameObject.SetActive(false);
            }

            if (identityDisc == null)
            {
                identityDisc = GetComponentInChildren<TronIdentityDisc>(true);
            }
            if (identityDisc != null)
            {
                bool isDiscActive = (currentTime >= 60.0f);
                identityDisc.gameObject.SetActive(isDiscActive);
                if (isDiscActive)
                {
                    identityDisc.transform.localPosition = new Vector3(0.04f, 0.0f, 0.0f);
                    identityDisc.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                }
            }

            UpdateCinematicCamera();
            UpdateLighting();
            UpdateEyelidAndFacialAnimation(currentTime);

            if (terminalScreen != null)
            {
                terminalScreen.UpdateScreenAtTime(currentTime);
            }

            // Keystroke audio feedback while Dr. Ido types at terminal
            if (currentPhase == AlitaAwakeningPhase.IdoTerminalTyping && currentTime >= 3.2f && currentTime < 9.5f)
            {
                if (Time.time - _lastKeystrokeSfxTime > 0.11f)
                {
                    _lastKeystrokeSfxTime = Time.time;
                    float freq = Random.Range(1800f, 2600f);
                    PlaySound(freq, 0.015f, 0.05f);
                }
            }

            // Dr. Ido Enter slam tone
            if (currentTime >= 9.5f && currentTime < 9.8f && !_hasPlayedEnterTone)
            {
                _hasPlayedEnterTone = true;
                PlaySound(1760f, 0.12f, 0.4f);
            }
            else if (currentTime < 9.0f)
            {
                _hasPlayedEnterTone = false;
            }

            // Blinding cursor flash detonation tone
            if (currentTime >= 9.8f && currentTime < 10.3f && !_hasPlayedDetonationTone)
            {
                _hasPlayedDetonationTone = true;
                PlaySound(2800f, 0.4f, 0.9f);
                PlaySound(70f, 0.9f, 0.75f);
            }
            else if (currentTime < 9.5f)
            {
                _hasPlayedDetonationTone = false;
            }

            // 1:20 Sub-bass drop impact tone
            if (currentTime >= 76.5f && currentTime < 77.5f && !_hasPlayedDropTone)
            {
                _hasPlayedDropTone = true;
                PlaySound(60f, 0.9f, 0.85f);
            }
            else if (currentTime < 75.0f)
            {
                _hasPlayedDropTone = false;
            }
        }

        private void LateUpdate()
        {
            if (!isPlaying) return;
            UpdateProceduralKinematics();
        }

        public void ScrubToTime(float time)
        {
            currentTime = Mathf.Clamp(time, 0f, totalDuration);
            DeterminePhase();
            UpdateShaderCompilation();
            EnsureReconstructionVFX();

            if (reconstructionVFX != null)
            {
                reconstructionVFX.EvaluateAtTime(currentTime);
            }

            if (eyePupilVFX != null) eyePupilVFX.gameObject.SetActive(false);
            if (orbitalEyeVFX != null) orbitalEyeVFX.gameObject.SetActive(false);

            if (identityDisc == null)
            {
                identityDisc = GetComponentInChildren<TronIdentityDisc>(true);
            }
            if (identityDisc != null)
            {
                bool isDiscActive = (currentTime >= 60.0f);
                identityDisc.gameObject.SetActive(isDiscActive);
                if (isDiscActive)
                {
                    identityDisc.transform.localPosition = new Vector3(0.04f, 0.0f, 0.0f);
                    identityDisc.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                }
            }

            UpdateCinematicCamera();
            UpdateLighting();

            if (terminalScreen != null)
            {
                terminalScreen.UpdateScreenAtTime(currentTime);
            }

            UpdateProceduralKinematics();
        }

        public void DeterminePhase()
        {
            if (currentTime < 9.9f) currentPhase = AlitaAwakeningPhase.IdoTerminalTyping;
            else if (currentTime < 10.2f) currentPhase = AlitaAwakeningPhase.CursorFlashDetonation;
            else if (currentTime < 32.0f) currentPhase = AlitaAwakeningPhase.EyePupilRadialBurst;
            else if (currentTime < 48.0f) currentPhase = AlitaAwakeningPhase.EyeZoomOut;
            else if (currentTime < 72.5f) currentPhase = AlitaAwakeningPhase.NanoBodyConstruction;
            else if (currentTime < 76.5f) currentPhase = AlitaAwakeningPhase.DigitizationComplete;
            else currentPhase = AlitaAwakeningPhase.WideSlabSilhouette; // Exactly 1:20 drop & upright sit-up to finale!
        }

        private struct AwakeningCameraKeyframe
        {
            public float time;
            public Vector3 pos;
            public Vector3 target;
            public float fov;
            public float roll;
            public float upWeight; // 0 = forward (top of head), 1 = Vector3.up

            public AwakeningCameraKeyframe(float t, Vector3 p, Vector3 tg, float f, float r, float u)
            {
                time = t; pos = p; target = tg; fov = f; roll = r; upWeight = u;
            }
        }

        private void SolveAwakeningGrandZoomOutCamera(float time, out Vector3 camPos, out Quaternion camRot, out float fov)
        {
            Vector3 slabP = slabTransform != null ? slabTransform.position : new Vector3(0f, 0.65f, 0f);
            Vector3 slabCenter = slabP + new Vector3(0f, 0.25f, 0.05f);
            Vector3 faceCenter = new Vector3(0.000f, 0.810f, 0.780f);
            Vector3 eyeFocus = new Vector3(-0.040f, 0.815f, 0.800f);

            AwakeningCameraKeyframe[] keys = new AwakeningCameraKeyframe[]
            {
                // 0: 10.2s - Inside right eye pupil macro extreme close-up (looking straight down along -Y into eye)
                new AwakeningCameraKeyframe(10.2f, eyeFocus + new Vector3(0f, 0.055f, 0.005f), eyeFocus, 34f, 0f, 0.0f),
                
                // 1: 24.0s - Retinal iris mandala & concentric code aperture radiating 360 degrees
                new AwakeningCameraKeyframe(24.0f, eyeFocus + new Vector3(0.005f, 0.30f, 0.010f), eyeFocus, 38f, 10f, 0.15f),
                
                // 2: 36.0s - Eyelids, bridge of nose, facial contours & wireframe cranium compiling with neural red lines
                new AwakeningCameraKeyframe(36.0f, faceCenter + new Vector3(0.08f, 0.58f, -0.06f), faceCenter, 36f, 4f, 0.60f),
                
                // 3: 48.0s - High 3/4 cinematic angle showing cranium, neck, and shoulders compiling with molten voxels
                new AwakeningCameraKeyframe(48.0f, slabP + new Vector3(0.55f, 1.15f, 0.65f), slabP + new Vector3(0f, 0.20f, 0.40f), 34f, 0f, 0.85f),
                
                // 4: 62.0s - Upper body compiling, glowing TRON Identity Disc, and right cyber hand flexing & calibrating
                new AwakeningCameraKeyframe(62.0f, slabP + new Vector3(1.45f, 1.25f, 0.35f), slabP + new Vector3(0f, 0.15f, 0.15f), 32f, 0f, 1.0f),
                
                // 5: 72.5s - Full cybernetic body compiling on floating slab in the compact TRON Red Grid Chamber
                new AwakeningCameraKeyframe(72.5f, slabP + new Vector3(3.4f, 0.90f, 0.15f), slabCenter, 34f, 0f, 1.0f),
                
                // 6: 76.5s - Iconic Wide Profile Silhouette Shot across the chamber at the 1:20 drop!
                new AwakeningCameraKeyframe(76.5f, new Vector3(3.9f, 1.05f, 0.05f), slabCenter, 40f, 0f, 1.0f),
                
                // 7: 80.5s - Medium cinematic angle framing Alita as she sits upright on the slab
                new AwakeningCameraKeyframe(80.5f, new Vector3(1.45f, 0.95f, 0.40f), new Vector3(0f, 0.88f, 0.15f), 35f, 0f, 1.0f),

                // 8: 87.0s - Two-shot framing Dr. Ido standing beside the slab speaking to Alita
                new AwakeningCameraKeyframe(87.0f, new Vector3(-1.85f, 1.15f, 0.95f), new Vector3(-0.25f, 0.85f, 0.20f), 38f, 0f, 1.0f),

                // 9: 95.0s - Close-up on Alita
                new AwakeningCameraKeyframe(95.0f, new Vector3(1.15f, 0.95f, 0.35f), new Vector3(0f, 0.90f, 0.15f), 32f, 0f, 1.0f),

                // 10: 101.0s - Over-the-shoulder perspective from Alita towards Dr. Ido
                new AwakeningCameraKeyframe(101.0f, new Vector3(1.20f, 1.10f, -0.35f), new Vector3(-0.65f, 1.05f, 0.30f), 35f, 0f, 1.0f),

                // 11: 115.0s - Wide two-shot showing both characters in the Red Grid Chamber as Ido gestures toward the clinic exit
                new AwakeningCameraKeyframe(115.0f, new Vector3(2.9f, 1.15f, 0.25f), new Vector3(-0.40f, 0.85f, 0.25f), 42f, 0f, 1.0f),

                // 12: 122.0s - Wide establishing shot leading to seamless black fade
                new AwakeningCameraKeyframe(122.0f, new Vector3(2.9f, 1.15f, 0.25f), new Vector3(-0.40f, 0.85f, 0.25f), 42f, 0f, 1.0f)
            };

            if (time <= keys[0].time)
            {
                camPos = keys[0].pos;
                fov = keys[0].fov;
                Vector3 up = Vector3.Lerp(Vector3.forward, Vector3.up, keys[0].upWeight).normalized;
                camRot = Quaternion.LookRotation((keys[0].target - camPos).normalized, up) * Quaternion.Euler(0f, 0f, keys[0].roll);
                return;
            }
            if (time >= keys[keys.Length - 1].time)
            {
                int last = keys.Length - 1;
                camPos = keys[last].pos;
                fov = keys[last].fov;
                Vector3 up = Vector3.Lerp(Vector3.forward, Vector3.up, keys[last].upWeight).normalized;
                camRot = Quaternion.LookRotation((keys[last].target - camPos).normalized, up) * Quaternion.Euler(0f, 0f, keys[last].roll);
                return;
            }

            int idx = 0;
            for (int i = 0; i < keys.Length - 1; i++)
            {
                if (time >= keys[i].time && time <= keys[i + 1].time)
                {
                    idx = i;
                    break;
                }
            }

            AwakeningCameraKeyframe kA = keys[idx];
            AwakeningCameraKeyframe kB = keys[idx + 1];
            float t = Mathf.Clamp01((time - kA.time) / (kB.time - kA.time));
            float s = Mathf.SmoothStep(0f, 1f, t);

            camPos = Vector3.Lerp(kA.pos, kB.pos, s);
            Vector3 target = Vector3.Lerp(kA.target, kB.target, s);
            fov = Mathf.Lerp(kA.fov, kB.fov, s);
            float roll = Mathf.Lerp(kA.roll, kB.roll, s);
            float upW = Mathf.Lerp(kA.upWeight, kB.upWeight, s);

            Vector3 upDir = Vector3.Lerp(Vector3.forward, Vector3.up, upW).normalized;
            Vector3 lookDir = (target - camPos).normalized;
            camRot = Quaternion.LookRotation(lookDir, upDir) * Quaternion.Euler(0f, 0f, roll);
        }

        private void UpdateShaderCompilation()
        {
            float progress;
            if (currentTime < 10.2f)
            {
                progress = 0.0f;
            }
            else if (currentTime < 72.5f)
            {
                // Smooth body compilation sweeping down as camera zooms out from eyes to whole body
                progress = Mathf.Clamp01((currentTime - 10.2f) / 62.3f);
            }
            else
            {
                progress = 1.0f;
            }

            foreach (var m in _alitaMaterials)
            {
                if (m != null && m.HasProperty("_CompileProgress"))
                {
                    m.SetFloat("_CompileProgress", progress);
                }
            }
        }

        private void UpdateCinematicCamera()
        {
            if (mainCamera == null) return;

            // Actor / prop contextual visibility
            bool isIdoActive = (currentPhase == AlitaAwakeningPhase.IdoTerminalTyping ||
                                currentPhase == AlitaAwakeningPhase.CursorFlashDetonation ||
                                currentPhase == AlitaAwakeningPhase.Confrontation ||
                                currentTime >= 80.0f);

            if (idoController != null)
            {
                idoController.gameObject.SetActive(isIdoActive);
                if (currentTime >= 80.0f)
                {
                    idoController.transform.position = new Vector3(-0.95f, 0f, 0.40f);
                    Vector3 lookDir = (new Vector3(0f, 0.85f, 0.15f) - idoController.transform.position);
                    lookDir.y = 0f;
                    if (lookDir != Vector3.zero) idoController.transform.rotation = Quaternion.LookRotation(lookDir);
                }
            }
            if (terminalTransform != null) terminalTransform.gameObject.SetActive(currentPhase == AlitaAwakeningPhase.IdoTerminalTyping || currentPhase == AlitaAwakeningPhase.CursorFlashDetonation);

            // Alita, floating slab, and red grid chamber are active throughout the entire awakening and construction sequence!
            bool isAlitaVisible = (currentTime >= 10.2f);
            if (alitaActor != null) alitaActor.SetActive(isAlitaVisible);
            if (chamberGridRoot != null) chamberGridRoot.SetActive(true);
            if (slabTransform != null) slabTransform.gameObject.SetActive(true);
            if (orbitalEyeVFX != null) orbitalEyeVFX.gameObject.SetActive(false);

            // Keep eye burst centered right on Alita's eye
            if (eyePupilVFXTransform != null)
            {
                eyePupilVFXTransform.position = new Vector3(0.00f, 0.76f, 0.68f);
                eyePupilVFXTransform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            }

            Vector3 docP = idoController != null ? idoController.transform.position : new Vector3(-3.0f, 0f, 1.2f);
            Vector3 docFwd = idoController != null ? idoController.transform.forward : new Vector3(0.9f, 0f, -0.4f).normalized;
            Vector3 docRgt = idoController != null ? idoController.transform.right : new Vector3(-0.4f, 0f, -0.9f).normalized;
            Vector3 termPos = terminalTransform != null ? terminalTransform.position : docP + docFwd * 0.48f;

            if (currentPhase == AlitaAwakeningPhase.IdoTerminalTyping)
            {
                // 0.0s - 9.8s: Framing physical terminal screen asset on console
                Vector3 quadPos = terminalScreen != null && terminalScreen.screenQuadGO != null ? terminalScreen.screenQuadGO.transform.position : termPos + Vector3.up * 0.95f;
                Vector3 quadFwd = terminalScreen != null && terminalScreen.screenQuadGO != null ? terminalScreen.screenQuadGO.transform.forward : -docFwd;
                Vector3 quadRgt = terminalScreen != null && terminalScreen.screenQuadGO != null ? terminalScreen.screenQuadGO.transform.right : -docRgt;
                Vector3 quadUp  = terminalScreen != null && terminalScreen.screenQuadGO != null ? terminalScreen.screenQuadGO.transform.up : Vector3.up;

                if (currentTime < 4.5f)
                {
                    // Direct close-up on physical terminal screen showing centered # ./start typing (filling entire frame cleanly)
                    mainCamera.transform.position = quadPos + quadFwd * 0.28f;
                    mainCamera.transform.rotation = Quaternion.LookRotation(-quadFwd, quadUp);
                    mainCamera.fieldOfView = 30f;
                }
                else if (currentTime < 7.5f)
                {
                    // Dynamic monitor perspective: close over typing position looking across screen matching frame_0013.png
                    Vector3 camPos = quadPos + quadFwd * 0.24f - quadRgt * 0.13f + quadUp * 0.015f;
                    mainCamera.transform.position = camPos;
                    Vector3 lookTarget = quadPos + quadRgt * 0.04f + quadUp * 0.045f;
                    mainCamera.transform.rotation = Quaternion.LookRotation((lookTarget - camPos).normalized, quadUp);
                    mainCamera.transform.Rotate(0f, 0f, 8f, Space.Self);
                    mainCamera.fieldOfView = 32f;
                }
                else if (currentTime < 9.5f)
                {
                    // Direct close-up on terminal screen with scrolling red/white boot logs matching frame_0018.png
                    mainCamera.transform.position = quadPos + quadFwd * 0.38f + quadRgt * 0.085f;
                    mainCamera.transform.rotation = Quaternion.LookRotation(-quadFwd, quadUp);
                    mainCamera.fieldOfView = 32f;
                }
                else
                {
                    // 9.5s - 9.9s: Tight macro close-up on terminal cursor block █ as Dr. Ido slams Enter matching frame_0020.png
                    mainCamera.transform.position = quadPos + quadFwd * 0.32f;
                    mainCamera.transform.rotation = Quaternion.LookRotation(-quadFwd, quadUp);
                    mainCamera.fieldOfView = 30f;
                }
            }
            else if (currentPhase == AlitaAwakeningPhase.CursorFlashDetonation)
            {
                // 9.8s - 10.2s: Zoom into the cursor block █ on screen as incandescent flash detonates!
                Vector3 cQuadPos = terminalScreen != null && terminalScreen.screenQuadGO != null ? terminalScreen.screenQuadGO.transform.position : termPos + Vector3.up * 0.95f;
                Vector3 cQuadFwd = terminalScreen != null && terminalScreen.screenQuadGO != null ? terminalScreen.screenQuadGO.transform.forward : -docFwd;
                Vector3 cQuadUp  = terminalScreen != null && terminalScreen.screenQuadGO != null ? terminalScreen.screenQuadGO.transform.up : Vector3.up;
                mainCamera.transform.position = cQuadPos + cQuadFwd * 0.18f;
                mainCamera.transform.rotation = Quaternion.LookRotation(-cQuadFwd, cQuadUp);
                mainCamera.fieldOfView = 20f;
            }
            else
            {
                // 10.2s - 120.0s: EVERYTHING THAT HAPPENED AFTER TERMINAL SCENE IS DONE BY ONE CAMERA ZOOMING OUT OF HIS EYES WHICH IS BEING CONSTRUCTED!
                SolveAwakeningGrandZoomOutCamera(currentTime, out Vector3 camPos, out Quaternion camRot, out float camFov);
                mainCamera.transform.position = camPos;
                mainCamera.transform.rotation = camRot;
                mainCamera.fieldOfView = camFov;
            }
        }

        private void UpdateProceduralKinematics()
        {
            if (alitaActor == null) return;

            // Update Dr. Ido typing state
            if (idoController != null)
            {
                idoController.isTyping = (currentPhase == AlitaAwakeningPhase.IdoTerminalTyping);
                idoController.typingIntensity = 1.2f;
                idoController.isSlammingEnter = (currentTime >= 9.5f && currentTime < 9.8f);
                idoController.UpdateTypingKinematics(currentTime);
            }

            // Always keep root placed flat on the floating slab (supine: nose & chest pointing straight UP +Y)
            alitaActor.transform.position = _alitaLiePos;
            alitaActor.transform.rotation = _alitaLieRot;

            // Legs: calves and feet always rest flat on slab
            if (_calfL != null) _calfL.localRotation = _calfLRest;
            if (_calfR != null) _calfR.localRotation = _calfRRest;
            if (_footL != null) _footL.localRotation = _footLRest;
            if (_footR != null) _footR.localRotation = _footRRest;

            switch (currentPhase)
            {
                case AlitaAwakeningPhase.IdoTerminalTyping:
                case AlitaAwakeningPhase.CursorFlashDetonation:
                case AlitaAwakeningPhase.EyePupilRadialBurst:
                case AlitaAwakeningPhase.EyeZoomOut:
                    // 0.0s - 48.0s: Lying motionless flat on slab with arms naturally alongside hips on slab
                    if (_hips != null) _hips.localRotation = _hipsRest;
                    if (_thighL != null) _thighL.localRotation = _thighLRest;
                    if (_thighR != null) _thighR.localRotation = _thighRRest;
                    if (_spine01 != null) _spine01.localRotation = _s1Rest;
                    if (_spine02 != null) _spine02.localRotation = _s2Rest;
                    if (_spine03 != null) _spine03.localRotation = _s3Rest;
                    if (_neck != null) _neck.localRotation = _neckRest;
                    if (_head != null) _head.localRotation = _headRest;

                    if (_armL != null) _armL.localRotation = _armLRest;
                    if (_forearmL != null) _forearmL.localRotation = _forearmLRest;
                    if (_handL != null) _handL.localRotation = _handLRest;
                    if (_armR != null) _armR.localRotation = _armRRest;
                    if (_forearmR != null) _forearmR.localRotation = _forearmRRest;
                    if (_handR != null) _handR.localRotation = _handRRest;
                    break;

                case AlitaAwakeningPhase.NanoBodyConstruction:
                    // 48.0s - 72.5s: Body supine on slab, foreground cyber arm elevates and flexes fingers
                    if (_hips != null) _hips.localRotation = _hipsRest;
                    if (_thighL != null) _thighL.localRotation = _thighLRest;
                    if (_thighR != null) _thighR.localRotation = _thighRRest;
                    if (_spine01 != null) _spine01.localRotation = _s1Rest;
                    if (_spine02 != null) _spine02.localRotation = _s2Rest;
                    if (_spine03 != null) _spine03.localRotation = _s3Rest;
                    if (_neck != null) _neck.localRotation = _neckRest;
                    if (_head != null) _head.localRotation = _headRest;

                    // Smooth raise (48s-56s), calibration hold, and smooth lower (68s-72.5s)
                    float tArmRaise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((currentTime - 48.0f) / 8.0f));
                    float tArmLower = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((currentTime - 68.0f) / 4.0f));
                    float armBlend = tArmRaise * (1.0f - tArmLower);

                    if (_armL != null)
                    {
                        _armL.localRotation = _armLRest * Quaternion.Euler(0f, 0f, 35f * armBlend);
                    }
                    if (_forearmL != null)
                    {
                        _forearmL.localRotation = _forearmLRest * Quaternion.Euler(0f, 0f, 40f * armBlend);
                    }
                    if (_handL != null)
                    {
                        float fingerWiggle = Mathf.Sin(currentTime * 8f) * 8f * armBlend;
                        _handL.localRotation = _handLRest * Quaternion.Euler(0f, 0f, fingerWiggle);
                    }

                    if (_armR != null) _armR.localRotation = _armRRest;
                    if (_forearmR != null) _forearmR.localRotation = _forearmRRest;
                    if (_handR != null) _handR.localRotation = _handRRest;
                    break;

                case AlitaAwakeningPhase.DigitizationComplete:
                    // 72.5s - 76.5s: Rest arms right before 1:20 sit-up
                    if (_hips != null) _hips.localRotation = _hipsRest;
                    if (_thighL != null) _thighL.localRotation = _thighLRest;
                    if (_thighR != null) _thighR.localRotation = _thighRRest;
                    if (_spine01 != null) _spine01.localRotation = _s1Rest;
                    if (_spine02 != null) _spine02.localRotation = _s2Rest;
                    if (_spine03 != null) _spine03.localRotation = _s3Rest;
                    if (_neck != null) _neck.localRotation = _neckRest;
                    if (_head != null) _head.localRotation = _headRest;

                    if (_armL != null) _armL.localRotation = _armLRest;
                    if (_forearmL != null) _forearmL.localRotation = _forearmLRest;
                    if (_handL != null) _handL.localRotation = _handLRest;
                    if (_armR != null) _armR.localRotation = _armRRest;
                    if (_forearmR != null) _forearmR.localRotation = _forearmRRest;
                    if (_handR != null) _handR.localRotation = _handRRest;
                    break;

                case AlitaAwakeningPhase.WideSlabSilhouette:
                default:
                    // 76.5s - 120.0s: The Iconic 1:20 Sit-Up!
                    // Majestic erect seated posture on slab with hands resting beside hips on slab surface
                    float tSit = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((currentTime - 76.5f) / 3.5f));
                    float hipAngle = 68f * tSit;

                    if (_hips != null) _hips.localRotation = _hipsRest * Quaternion.Euler(0f, 0f, -hipAngle);
                    if (_thighL != null) _thighL.localRotation = _thighLRest * Quaternion.Euler(0f, 0f, -hipAngle);
                    if (_thighR != null) _thighR.localRotation = _thighRRest * Quaternion.Euler(0f, 0f, -hipAngle);

                    // Natural noble upright posture
                    if (_spine01 != null) _spine01.localRotation = _s1Rest * Quaternion.Euler(0f, 0f, 6f * tSit);
                    if (_spine02 != null) _spine02.localRotation = _s2Rest * Quaternion.Euler(0f, 0f, 6f * tSit);
                    if (_spine03 != null) _spine03.localRotation = _s3Rest * Quaternion.Euler(0f, 0f, 4f * tSit);
                    if (_neck != null) _neck.localRotation = _neckRest * Quaternion.Euler(0f, 0f, -6f * tSit);
                    if (_head != null) _head.localRotation = _headRest * Quaternion.Euler(0f, 0f, -4f * tSit);

                    // Arms supporting seated posture flat on top of the black glass slab (Y = 0.72m)
                    Quaternion sitArmL = _armLPrefabRest * Quaternion.Euler(0f, -80f, 15f);
                    Quaternion sitArmR = _armRPrefabRest * Quaternion.Euler(0f, 80f, -15f);
                    if (_armL != null) _armL.localRotation = Quaternion.Slerp(_armLRest, sitArmL, tSit);
                    if (_armR != null) _armR.localRotation = Quaternion.Slerp(_armRRest, sitArmR, tSit);
                    if (_forearmL != null) _forearmL.localRotation = Quaternion.Slerp(_forearmLRest, _forearmLRest * Quaternion.Euler(0f, 0f, 15f), tSit);
                    if (_forearmR != null) _forearmR.localRotation = Quaternion.Slerp(_forearmRRest, _forearmRRest * Quaternion.Euler(0f, 0f, -15f), tSit);
                    if (_handL != null) _handL.localRotation = _handLRest;
                    if (_handR != null) _handR.localRotation = _handRRest;
                    break;
            }

            if (_alitaSMR != null)
            {
                _alitaSMR.forceMatrixRecalculationPerRender = true;
            }

            UpdateEyelidAndFacialAnimation(currentTime);
        }

        public void CacheBlendShapeIndices()
        {
            if (_alitaSMR == null && alitaActor != null)
            {
                _alitaSMR = alitaActor.GetComponentInChildren<SkinnedMeshRenderer>();
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
                _bsEyeSquintL = FindBlendShapeIndex(m, "eyeSquintLeft");
                _bsEyeSquintR = FindBlendShapeIndex(m, "eyeSquintRight");
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
                if (n.EndsWith(targetName, StringComparison.OrdinalIgnoreCase)) return i;
            }
            return -1;
        }

        public void UpdateEyelidAndFacialAnimation(float time)
        {
            if (!enableFacialAnimation) return;
            if (_alitaSMR == null && alitaActor != null)
            {
                _alitaSMR = alitaActor.GetComponentInChildren<SkinnedMeshRenderer>();
                CacheBlendShapeIndices();
            }
            if (_alitaSMR == null) return;

            if (_bsEyeBlinkUpperL < 0)
            {
                CacheBlendShapeIndices();
            }

            float blinkUpper = 100f;
            float blinkLower = 100f;
            float browInnerUp = 0f;
            float squint = 0f;

            if (time < 30.0f)
            {
                // Pre-awakening: completely shut
                blinkUpper = 100f;
                blinkLower = 85f;
                browInnerUp = 0f;
                squint = 0f;
            }
            else if (time < 31.8f)
            {
                // Synaptic stirring: subtle neural flutter in eyelids as radial circuits complete
                float tF = (time - 30.0f) / 1.8f;
                float ripple = Mathf.Sin(tF * Mathf.PI * 4f) * 6f;
                blinkUpper = Mathf.Clamp(100f - Mathf.Abs(ripple), 92f, 100f);
                blinkLower = blinkUpper * 0.85f;
                browInnerUp = Mathf.Lerp(0f, 3f, tF);
            }
            else if (time < 40.0f)
            {
                // Core EyeZoomOut Awakening:
                // As the camera zooms out from the pupil and reveals the iris, eyelids, and head,
                // Alita slowly and smoothly opens her eyelids!
                float tOpen = (time - 31.8f) / 8.2f; // 8.2 seconds of slow, graceful opening
                float smoothOpen = Mathf.SmoothStep(0f, 1f, tOpen);
                blinkUpper = Mathf.Lerp(100f, 0f, smoothOpen);
                blinkLower = blinkUpper * 0.75f;

                // Subtle organic squint as eyes adjust to the neon chamber lights
                float squintT = Mathf.Clamp01((time - 33.0f) / 5.5f);
                squint = Mathf.Sin(squintT * Mathf.PI) * 22f;

                // Inner brows slowly elevate in wonder and sentience
                float browT = Mathf.Clamp01((time - 32.5f) / 7.5f);
                browInnerUp = Mathf.SmoothStep(0f, 25f, browT);
            }
            else if (time < 41.6f)
            {
                // Receptor adjustment micro-blink (adjusting to luminescence)
                float tBlink = (time - 40.0f) / 1.6f;
                float blinkPulse = Mathf.Sin(tBlink * Mathf.PI) * 68f;
                blinkUpper = blinkPulse;
                blinkLower = blinkPulse * 0.65f;
                browInnerUp = 25f;
                squint = 0f;
            }
            else
            {
                // Fully awake & conscious!
                // Subtle organic blinks every ~6.2 seconds and gentle living breath
                float cycle = (time - 42.0f) % 6.2f;
                if (cycle < 0.22f)
                {
                    float bT = cycle / 0.22f;
                    float bCurve = Mathf.Sin(bT * Mathf.PI) * 75f;
                    blinkUpper = bCurve;
                    blinkLower = bCurve * 0.65f;
                }
                else
                {
                    blinkUpper = 0f;
                    blinkLower = 0f;
                }

                browInnerUp = 22f + Mathf.Sin(time * 0.75f) * 3f;
                squint = 0f;
            }

            currentBlinkWeight = blinkUpper;

            if (_bsEyeBlinkUpperL >= 0) _alitaSMR.SetBlendShapeWeight(_bsEyeBlinkUpperL, blinkUpper);
            if (_bsEyeBlinkUpperR >= 0) _alitaSMR.SetBlendShapeWeight(_bsEyeBlinkUpperR, blinkUpper);
            if (_bsEyeBlinkLowerL >= 0) _alitaSMR.SetBlendShapeWeight(_bsEyeBlinkLowerL, blinkLower);
            if (_bsEyeBlinkLowerR >= 0) _alitaSMR.SetBlendShapeWeight(_bsEyeBlinkLowerR, blinkLower);
            if (_bsBrowInnerUpL >= 0) _alitaSMR.SetBlendShapeWeight(_bsBrowInnerUpL, browInnerUp);
            if (_bsBrowInnerUpR >= 0) _alitaSMR.SetBlendShapeWeight(_bsBrowInnerUpR, browInnerUp);
            if (_bsEyeSquintL >= 0) _alitaSMR.SetBlendShapeWeight(_bsEyeSquintL, squint);
            if (_bsEyeSquintR >= 0) _alitaSMR.SetBlendShapeWeight(_bsEyeSquintR, squint);
        }

        private void UpdateLighting()
        {
            if (slabEdgeLight != null)
            {
                slabEdgeLight.intensity = 2.2f;
            }

            if (opticLeftLight != null && opticRightLight != null)
            {
                float opticIntensity = (currentTime >= 10.2f && currentTime < 76.5f) ? 1.0f : 0f;
                opticLeftLight.intensity = opticIntensity;
                opticRightLight.intensity = opticIntensity;
            }

            if (faceKeyLight != null)
            {
                faceKeyLight.color = new Color(1.0f, 0.90f, 0.80f);
                faceKeyLight.range = 2.2f;
                if (currentPhase == AlitaAwakeningPhase.WideSlabSilhouette)
                {
                    faceKeyLight.intensity = 0f; // Pure wide silhouette against red grid
                }
                else if (currentTime >= 10.2f && currentTime < 76.5f)
                {
                    faceKeyLight.intensity = 1.0f; // Key illumination on constructing eye, face, cranium, neck, and torso
                }
                else
                {
                    faceKeyLight.intensity = 0.4f;
                }
            }

            if (overheadScanLight != null)
            {
                if (currentPhase == AlitaAwakeningPhase.WideSlabSilhouette)
                {
                    overheadScanLight.intensity = 0f; // Pure silhouette against red grid!
                }
                else if (currentTime >= 48.0f && currentTime < 72.5f)
                {
                    overheadScanLight.intensity = 2.5f + Mathf.Sin(currentTime * 10f) * 0.8f;
                }
                else
                {
                    overheadScanLight.intensity = 0.6f;
                }
            }
        }

        private void PlaySound(float freq, float duration, float volume)
        {
            if (fxSource == null) return;
            AudioClip clip = CreateToneClip(freq, duration);
            fxSource.PlayOneShot(clip, volume);
        }

        private AudioClip CreateToneClip(float frequency, float duration)
        {
            int sampleRate = 44100;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Sin(Mathf.PI * (t / duration));
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope;
            }
            AudioClip clip = AudioClip.Create("Tone_" + frequency, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void OnGUI()
        {
            InitStyles();

            // Soft warm breakout flare overlay (ONLY at 9.9s - 10.15s)
            if (currentTime >= 9.9f && currentTime < 10.15f)
            {
                float tFlash = (currentTime < 10.02f) ? (currentTime - 9.9f) / 0.12f : 1.0f - (currentTime - 10.02f) / 0.13f;
                float alpha = Mathf.Clamp01(tFlash) * 0.25f;
                GUI.color = new Color(1f, 0.90f, 0.80f, alpha);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _whitePixel);
                GUI.color = Color.white;
            }

            // 72.5s - 76.5s: Red Telemetry HUD Box on slab (98% -> DIGITIZATION COMPLETE)
            if (currentPhase == AlitaAwakeningPhase.DigitizationComplete)
            {
                float tHud = currentTime - 72.5f;
                string percentText = "96%";
                if (tHud >= 1.0f && tHud < 2.0f) percentText = "98%";
                else if (tHud >= 2.0f && tHud < 3.0f) percentText = "99%";
                else if (tHud >= 3.0f) percentText = "DIGITIZATION COMPLETE";

                float boxW = 520f;
                float boxH = 140f;
                Rect boxRect = new Rect((Screen.width - boxW) * 0.5f, (Screen.height - boxH) * 0.5f, boxW, boxH);

                GUI.color = new Color(0.04f, 0.02f, 0.02f, 0.85f);
                GUI.DrawTexture(boxRect, _blackPixel);

                GUI.color = new Color(1.0f, 0.15f, 0.18f, 1f);
                GUI.DrawTexture(new Rect(boxRect.x, boxRect.y, boxRect.width, 3), _whitePixel);
                GUI.DrawTexture(new Rect(boxRect.x, boxRect.yMax - 3, boxRect.width, 3), _whitePixel);
                GUI.DrawTexture(new Rect(boxRect.x, boxRect.y, 4, boxRect.height), _whitePixel);
                GUI.DrawTexture(new Rect(boxRect.xMax - 4, boxRect.y, 4, boxRect.height), _whitePixel);

                GUI.color = Color.white;
                GUI.Label(new Rect(boxRect.x, boxRect.y + 35, boxRect.width, 60), percentText, _creditStyle);
                GUI.color = Color.white;
            }

            // 76.5s - 76.8s: Sub-bass blackout cut right at the 1:20 drop
            if (currentTime >= 76.5f && currentTime < 76.8f)
            {
                float tSub = (currentTime - 76.5f) / 0.3f;
                GUI.color = new Color(1f, 1f, 1f, 1f - tSub);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _whitePixel);
                GUI.color = Color.white;
            }

            // Cinematic Film Title Cards & Cast Billing
            string creditText = "";
            TextAnchor creditAlign = TextAnchor.MiddleCenter;
            Rect creditRect = new Rect(0, Screen.height * 0.78f, Screen.width, 60);
            float creditAlpha = 0f;

            if (currentTime >= 11.2f && currentTime < 16.5f)
            {
                creditText = "> INIT SEQUENCE // QUANTUM RECONSTRUCTION";
                creditRect = new Rect(0, Screen.height * 0.78f, Screen.width, 60);
                creditAlign = TextAnchor.MiddleCenter;
                creditAlpha = Mathf.Sin(Mathf.Clamp01((currentTime - 11.2f) / 5.3f) * Mathf.PI);
            }
            else if (currentTime >= 23.0f && currentTime < 28.5f)
            {
                creditText = "EDGERUNNERS\nPRESENTS";
                creditRect = new Rect(0, Screen.height * 0.44f, Screen.width, 100);
                creditAlign = TextAnchor.MiddleCenter;
                creditAlpha = Mathf.Sin(Mathf.Clamp01((currentTime - 23.0f) / 5.5f) * Mathf.PI);
            }
            else if (currentTime >= 29.5f && currentTime < 36.5f)
            {
                creditText = "GAME DIRECTOR\nYASHWANTH";
                creditRect = new Rect(Screen.width * 0.12f, Screen.height * 0.76f, Screen.width * 0.50f, 90);
                creditAlign = TextAnchor.MiddleLeft;
                creditAlpha = Mathf.Sin(Mathf.Clamp01((currentTime - 29.5f) / 7.0f) * Mathf.PI);
            }
            else if (currentTime >= 37.5f && currentTime < 44.5f)
            {
                creditText = "LEAD TECHNICAL ARTIST\nLAKSHAYA";
                creditRect = new Rect(Screen.width * 0.38f, Screen.height * 0.76f, Screen.width * 0.50f, 90);
                creditAlign = TextAnchor.MiddleRight;
                creditAlpha = Mathf.Sin(Mathf.Clamp01((currentTime - 37.5f) / 7.0f) * Mathf.PI);
            }
            else if (currentTime >= 45.5f && currentTime < 52.5f)
            {
                creditText = "LEAD COMBAT & SYSTEMS DESIGNER\nPUNITH";
                creditRect = new Rect(Screen.width * 0.12f, Screen.height * 0.76f, Screen.width * 0.50f, 90);
                creditAlign = TextAnchor.MiddleLeft;
                creditAlpha = Mathf.Sin(Mathf.Clamp01((currentTime - 45.5f) / 7.0f) * Mathf.PI);
            }
            else if (currentTime >= 53.5f && currentTime < 61.5f)
            {
                creditText = "EXECUTIVE GAME DEVELOPMENT & DIRECTION\nYASHWANTH  •  LAKSHAYA  •  PUNITH";
                creditRect = new Rect(0, Screen.height * 0.75f, Screen.width, 90);
                creditAlign = TextAnchor.MiddleCenter;
                creditAlpha = Mathf.Sin(Mathf.Clamp01((currentTime - 53.5f) / 8.0f) * Mathf.PI);
            }
            else if (currentTime >= 62.5f && currentTime < 72.0f)
            {
                creditText = "SUBJECT: ALITA // URM BERSERKER RECONSTRUCTION";
                creditRect = new Rect(0, Screen.height * 0.78f, Screen.width, 60);
                creditAlign = TextAnchor.MiddleCenter;
                creditAlpha = Mathf.Sin(Mathf.Clamp01((currentTime - 62.5f) / 9.5f) * Mathf.PI);
            }
            else if (currentTime >= 76.5f && currentTime < 86.0f)
            {
                creditText = ">> REAWAKENING SEQUENCE: COMPLETE <<";
                creditRect = new Rect(0, Screen.height * 0.78f, Screen.width, 60);
                creditAlign = TextAnchor.MiddleCenter;
                creditAlpha = Mathf.Sin(Mathf.Clamp01((currentTime - 76.5f) / 9.5f) * Mathf.PI);
            }

            if (!string.IsNullOrEmpty(creditText) && creditAlpha > 0.01f)
            {
                _creditStyle.alignment = creditAlign;
                GUI.color = new Color(1f, 0.52f, 0.22f, Mathf.Clamp01(creditAlpha * 1.35f));
                GUI.Label(creditRect, creditText, _creditStyle);
                GUI.color = Color.white;
            }

            // Real-Time On-Screen Film Timecode & Diagnostics HUD
            if (showTimestampHUD)
            {
                int minutes = Mathf.FloorToInt(currentTime / 60f);
                float seconds = currentTime % 60f;
                int frame = Mathf.FloorToInt(currentTime * 30f);

                string tcTime = string.Format("{0:00}:{1:00.00}", minutes, seconds);
                string phaseName = currentPhase.ToString();
                string phaseRange = GetPhaseRangeString(currentPhase);

                float hudW = 460f;
                float hudH = 68f;
                Rect hudRect = new Rect(24f, 20f, hudW, hudH);

                // Sleek Semi-Transparent Dark Glass Backdrop
                GUI.color = new Color(0.015f, 0.02f, 0.035f, 0.88f);
                GUI.DrawTexture(hudRect, _blackPixel);

                // Neon TRON Crimson / Amber Border
                GUI.color = new Color(1.0f, 0.35f, 0.12f, 0.95f);
                GUI.DrawTexture(new Rect(hudRect.x, hudRect.y, hudRect.width, 2f), _whitePixel);
                GUI.DrawTexture(new Rect(hudRect.x, hudRect.yMax - 2f, hudRect.width, 2f), _whitePixel);
                GUI.DrawTexture(new Rect(hudRect.x, hudRect.y, 3f, hudRect.height), _whitePixel);
                GUI.DrawTexture(new Rect(hudRect.xMax - 3f, hudRect.y, 3f, hudRect.height), _whitePixel);

                // Row 1: Timecode, Elapsed Seconds, Frame Counter
                GUI.color = Color.white;
                GUI.Label(new Rect(hudRect.x + 14f, hudRect.y + 8f, hudRect.width - 28f, 26f), 
                    $"<color=#00F5FF>[TC]</color> <color=#FFFFFF><b>{tcTime}</b></color>  <color=#FF7733>({currentTime:00.00}s)</color>  <color=#999999>Frame: {frame} @ 30fps</color>", _hudHeaderStyle);

                // Row 2: Active Cutscene Phase & Sequence Range
                GUI.Label(new Rect(hudRect.x + 14f, hudRect.y + 35f, hudRect.width - 28f, 24f), 
                    $"<color=#FF3322><b>PHASE:</b></color> <color=#FFDD66>{phaseName}</color>  <color=#AAAAAA>[{phaseRange}]</color>", _timecodeStyle);
                GUI.color = Color.white;
            }

            // Intro Fade-in from Black (Seamless hand-off from Cutscene 1)
            if (_introFadeAlpha > 0.005f)
            {
                GUI.color = new Color(0f, 0f, 0f, _introFadeAlpha);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            // End Fade-out (Seamless pure black cinematic fade into City Gameplay)
            if (_endFadeAlpha > 0.005f)
            {
                GUI.color = new Color(0f, 0f, 0f, _endFadeAlpha);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            // Year 2563 Cinematic Black Title Card (80.5s - 84.0s)
            if (currentTime >= 80.5f)
            {
                DrawYearTitleCard();
            }

            // Skip Hint
            if (allowSkipToGameplay && !_isEnding && currentTime < 80.5f)
            {
                GUIStyle skipStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    alignment = TextAnchor.MiddleRight
                };
                skipStyle.normal.textColor = new Color(0.7f, 0.8f, 0.9f, 0.5f);
                GUI.Label(new Rect(Screen.width - 240, 12, 220, 22), "Press [ESC] to Skip to Clinic Lab", skipStyle);
            }
        }

        private void DrawYearTitleCard()
        {
            int sw = Screen.width;
            int sh = Screen.height;

            // Full screen solid black backdrop
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, sw, sh), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Massive Year 2563 Text (High impact AAA Cyberpunk Typography)
            int baseSize = Mathf.Clamp(sw / 11, 85, 125);
            int yearFontSize = Mathf.RoundToInt(baseSize * titleCardYearScale);
            yearFontSize = Mathf.Clamp(yearFontSize, 110, 185);

            GUIStyle yearStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = yearFontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            float centerY = sh * 0.28f;
            float yearBoxH = yearFontSize + 32f;

            // Multi-pass Electric Cyan glow & chromatic aberration for neon punch
            yearStyle.normal.textColor = new Color(0f, 0.90f, 1f, 0.40f);
            GUI.Label(new Rect(-4, centerY - 2, sw, yearBoxH), titleCardYear, yearStyle);
            GUI.Label(new Rect(4, centerY + 2, sw, yearBoxH), titleCardYear, yearStyle);
            GUI.Label(new Rect(-2, centerY + 3, sw, yearBoxH), titleCardYear, yearStyle);
            GUI.Label(new Rect(2, centerY - 3, sw, yearBoxH), titleCardYear, yearStyle);

            // Crisp white foreground
            yearStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(0, centerY, sw, yearBoxH), titleCardYear, yearStyle);

            // Amber Accent Line (widened to balance the massive year)
            float lineWidth = Mathf.Clamp(sw * 0.38f, 520f, 720f);
            float lineX = (sw - lineWidth) * 0.5f;
            float lineY = centerY + yearBoxH + 4f;

            // Subtle line shadow
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(lineX, lineY + 2, lineWidth, 3.5f), Texture2D.whiteTexture);

            // Golden amber accent line
            GUI.color = new Color(1f, 0.78f, 0.22f, 0.95f);
            GUI.DrawTexture(new Rect(lineX, lineY, lineWidth, 3.5f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Subtitle: "THREE CENTURIES AFTER THE GREAT WAR"
            GUIStyle subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Clamp(sw / 58, 15, 23),
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter
            };
            subStyle.normal.textColor = new Color(0.96f, 0.86f, 0.40f, 0.95f);
            GUI.Label(new Rect(0, lineY + 18, sw, 34), titleCardSubtitle, subStyle);

            // Clinic Location
            GUIStyle clinicStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Clamp(sw / 78, 12, 17),
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter
            };
            clinicStyle.normal.textColor = new Color(0.68f, 0.82f, 0.92f, 0.78f);
            GUI.Label(new Rect(0, lineY + 54, sw, 28), titleCardLocation, clinicStyle);

            // Bottom Prompt
            GUIStyle promptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter
            };

            if (_isLoadingGameplay)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
                promptStyle.normal.textColor = Color.Lerp(new Color(0f, 0.85f, 1f, 0.8f), new Color(1f, 0.85f, 0.2f, 1f), pulse);
                GUI.Label(new Rect(0, sh - 55, sw, 28), "◈ INITIALIZING DR. IDO'S CYBER CLINIC LAB... [LOADING]", promptStyle);
            }
            else
            {
                float pulse = 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 4f);
                promptStyle.normal.textColor = new Color(0.7f, 0.85f, 0.95f, pulse);
                GUI.Label(new Rect(0, sh - 55, sw, 28), "[SPACE / CLICK] Awaken in Clinic Lab", promptStyle);

                // Invisible full-screen button so clicking anywhere advances immediately
                if (GUI.Button(new Rect(0, 0, sw, sh), GUIContent.none, GUIStyle.none))
                {
                    TriggerTransitionToGameplay();
                }

                // Interactive Event / Click detection inside OnGUI
                if (Event.current != null)
                {
                    if (Event.current.type == EventType.MouseDown)
                    {
                        TriggerTransitionToGameplay();
                        Event.current.Use();
                    }
                    else if (Event.current.type == EventType.KeyDown && 
                            (Event.current.keyCode == KeyCode.Space || Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
                    {
                        TriggerTransitionToGameplay();
                        Event.current.Use();
                    }
                }
            }
        }

        public void FinishCutsceneAndPromptGameplay()
        {
            TriggerTransitionToGameplay();
        }

        public void TriggerTransitionToGameplay()
        {
            if (_isLoadingGameplay) return;
            _isLoadingGameplay = true;
            _isEnding = true;
            _endFadeAlpha = 1.0f;

            if (musicSource != null && musicSource.isPlaying)
            {
                StartCoroutine(FadeOutAudioRoutine(musicSource, 0.5f));
            }

            StartCoroutine(TransitionToGameplayRoutine());
        }

        private IEnumerator FadeOutAudioRoutine(AudioSource src, float duration)
        {
            if (src == null) yield break;
            float startVol = src.volume;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                if (src != null) src.volume = Mathf.Lerp(startVol, 0f, t / duration);
                yield return null;
            }
            if (src != null) src.volume = 0f;
        }

        private IEnumerator TransitionToGameplayRoutine()
        {
            _isLoadingGameplay = true;
            _endFadeAlpha = 1.0f;

            string targetScene = string.IsNullOrEmpty(nextGameplaySceneName) ? "Demo_Interior" : nextGameplaySceneName;

            // Normalize and verify target scene in Build Settings
            bool sceneExists = false;
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings; i++)
            {
                string sPath = UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i);
                string sName = System.IO.Path.GetFileNameWithoutExtension(sPath);
                if (sName.Equals(targetScene, System.StringComparison.OrdinalIgnoreCase))
                {
                    sceneExists = true;
                    targetScene = sName;
                    break;
                }
            }

            if (!sceneExists)
            {
                Debug.LogWarning($"[AlitaTronAwakening] Target scene '{targetScene}' not found in Build Settings! Falling back to Demo_Interior.");
                targetScene = "Demo_Interior";
            }

            Debug.Log($"[AlitaTronAwakening] Loading gameplay scene: '{targetScene}'...");

            AsyncOperation op = null;
            try
            {
                op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(targetScene);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[AlitaTronAwakening] LoadSceneAsync failed: {ex.Message}. Loading synchronously...");
                UnityEngine.SceneManagement.SceneManager.LoadScene(targetScene);
                yield break;
            }

            if (op != null)
            {
                while (!op.isDone)
                {
                    _gameplayLoadProgress = Mathf.Clamp01(op.progress / 0.9f);
                    yield return null;
                }
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(targetScene);
            }
        }

        private string GetPhaseRangeString(AlitaAwakeningPhase phase)
        {
            switch (phase)
            {
                case AlitaAwakeningPhase.IdoTerminalTyping: return "0.0s - 9.8s";
                case AlitaAwakeningPhase.CursorFlashDetonation: return "9.8s - 10.2s";
                case AlitaAwakeningPhase.EyePupilRadialBurst: return "10.2s - 32.0s";
                case AlitaAwakeningPhase.EyeZoomOut: return "32.0s - 48.0s";
                case AlitaAwakeningPhase.NanoBodyConstruction: return "48.0s - 72.5s";
                case AlitaAwakeningPhase.DigitizationComplete: return "72.5s - 76.5s";
                case AlitaAwakeningPhase.WideSlabSilhouette: return "76.5s - 80.0s (1:20)";
                case AlitaAwakeningPhase.Touchdown: return "80.0s - 86.0s";
                case AlitaAwakeningPhase.Confrontation: return "86.0s - 120.0s";
                default: return "";
            }
        }

        private void InitStyles()
        {
            if (_blackPixel == null)
            {
                _blackPixel = new Texture2D(1, 1);
                _blackPixel.SetPixel(0, 0, Color.black);
                _blackPixel.Apply();
            }
            if (_whitePixel == null)
            {
                _whitePixel = new Texture2D(1, 1);
                _whitePixel.SetPixel(0, 0, Color.white);
                _whitePixel.Apply();
            }
            if (_timecodeStyle == null)
            {
                _timecodeStyle = new GUIStyle();
                _timecodeStyle.fontSize = 13;
                _timecodeStyle.richText = true;
                _timecodeStyle.normal.textColor = Color.white;
                _timecodeStyle.fontStyle = FontStyle.Normal;
            }
            if (_hudHeaderStyle == null)
            {
                _hudHeaderStyle = new GUIStyle();
                _hudHeaderStyle.fontSize = 15;
                _hudHeaderStyle.richText = true;
                _hudHeaderStyle.normal.textColor = Color.white;
                _hudHeaderStyle.fontStyle = FontStyle.Bold;
            }
            if (_creditStyle == null)
            {
                _creditStyle = new GUIStyle();
                _creditStyle.fontSize = 24;
                _creditStyle.alignment = TextAnchor.MiddleCenter;
                _creditStyle.normal.textColor = new Color(1.0f, 0.45f, 0.15f, 1.0f);
                _creditStyle.fontStyle = FontStyle.Bold;
            }
        }
    }
}
