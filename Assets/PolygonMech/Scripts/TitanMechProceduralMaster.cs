using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PolygonMech
{
    /// <summary>
    /// AAA Titan Mech Procedural Kinematics & Audio/VFX Master.
    /// Provides:
    /// 1. Independent Turret Chassis Aiming (torso slews to track target like a tank turret).
    /// 2. Heavy Hydraulic Piston Suspension (pelvis compresses down on footfalls with spring recoil).
    /// 3. Seismic Footstep Impacts (sub-bass audio, pneumatic hiss, concrete dust particles, camera shake).
    /// 4. Dynamic Thruster Afterburners & Flap Articulation (idle glow to roaring rocket boost).
    /// 5. Arm & Weapon Socket Alignment (prevents armor clipping into cockpit).
    /// 6. Heavy Weapon Emitters: Seismic Ground Cleave, Skater Dash Sparks, Missile Barrages, Gatling Bursts.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class TitanMechProceduralMaster : MonoBehaviour
    {
        [Header("Target & Chassis Tracking")]
        public Transform target;
        [SerializeField] private float maxChassisYaw   = 65f;
        [SerializeField] private float maxChassisPitch = 35f; // Expanded to allow deep downward glare at small targets like Alita
        [SerializeField] private float chassisTurnSpeed = 8f;

        [Header("Hydraulic Suspension Settings")]
        [SerializeField] private float suspensionStiffness = 38f;
        [SerializeField] private float suspensionDamping   = 8f;
        [SerializeField] private float footstepCompression = 0.12f;

        [Header("Camera Shake Settings")]
        [SerializeField] private float maxShakeDistance = 35f;
        [SerializeField] private float footstepShakeIntensity = 0.25f;
        [SerializeField] private float slamShakeIntensity     = 1.0f;

        [Header("Attached FX Prefabs (Auto-loaded if null)")]
        [SerializeField] private GameObject footstepFXPrefab;
        [SerializeField] private GameObject jumpImpactFXPrefab;
        [SerializeField] private GameObject exhaustFlameFXPrefab;
        [SerializeField] private GameObject sparksFXPrefab;
        [SerializeField] private GameObject missileBarrageFXPrefab;
        [SerializeField] private GameObject machineGunFXPrefab;

        // Bone Transforms
        private Transform hipsBone;
        private Transform spine01;
        private Transform spine02;
        private Transform spine03;
        private Transform headBone;
        private Transform clavicleL;
        private Transform clavicleR;
        private Transform ankleL;
        private Transform ankleR;
        private Transform exhaustL;
        private Transform exhaustR;
        private Transform handRSocket;

        // Exhaust Flaps
        private List<Transform> exhaustFlaps = new List<Transform>();

        // Thruster Particle Systems & Lights
        private ParticleSystem flameLeftPS;
        private ParticleSystem flameRightPS;
        private Light flameLeftLight;
        private Light flameRightLight;

        [Header("Authentic Heavy Titan Mech Audio")]
        public AudioClip[] sfxTitanFootsteps;
        public AudioClip sfxChassisReactorLoop;
        public AudioClip[] sfxHydraulicActuators;

        // Audio Sources
        private AudioSource footstepAudioSource;
        private AudioSource servoAudioSource;
        private AudioSource movementServoAudioSource;
        private AudioSource thrusterAudioSource;
        private AudioSource weaponAudioSource;
        private Vector3 lastMechPosition;
        private float lastMechRotationY;
        private float currentMovementServoVol = 0f;
        private float nextMoveServoWhineTime = 0f;

        // Runtime Suspension State
        private Vector3 hipsOriginalLocalPos;
        private float hipsCurrentCompression = 0f;
        private float hipsVelocity = 0f;

        // Foot Tracking
        private float lastLeftFootY = 0f;
        private float lastRightFootY = 0f;
        private bool leftFootGrounded = true;
        private bool rightFootGrounded = true;
        private float nextFootstepTime = 0f;

        // Torso Tracking State
        private float currentChassisYaw = 0f;
        private float currentChassisPitch = 0f;

        // Dash / Boost State
        private bool isBoosting = false;
        private float boostIntensity = 0.3f; // 0.3 = idle, 1.0 = walking, 2.0 = dash

        private Animator anim;

        private void Awake()
        {
            anim = GetComponent<Animator>();
            FindAndCacheBones();
            SetupAudioSources();
            LoadFXPrefabs();
            SetupExhaustThrusters();
        }

        private void Start()
        {
            if (target == null) FindTarget();
        }

        public void FindTarget()
        {
            GameObject p = GameObject.Find("Alita_03_Final_NanoBerserker")
                        ?? GameObject.Find("Alita_BattleAngel_Hero")
                        ?? GameObject.Find("Alita")
                        ?? GameObject.Find("Alitha")
                        ?? GameObject.Find("Player_LucyCombat")
                        ?? GameObject.Find("PF_DualKatanaPlayer")
                        ?? GameObject.Find("PF_SyntySwordPlayer")
                        ?? GameObject.FindGameObjectWithTag("Player");
            if (p != null) target = p.transform;
        }

        private void FindAndCacheBones()
        {
            hipsBone = FindInHierarchy("Hips");
            if (hipsBone != null) hipsOriginalLocalPos = hipsBone.localPosition;

            spine01   = FindInHierarchy("Spine_01");
            spine02   = FindInHierarchy("Spine_02");
            spine03   = FindInHierarchy("Spine_03");
            headBone  = FindInHierarchy("Head");
            clavicleL = FindInHierarchy("Clavicle_L");
            clavicleR = FindInHierarchy("Clavicle_R");
            ankleL    = FindInHierarchy("Ankle_L");
            ankleR    = FindInHierarchy("Ankle_R");
            exhaustL  = FindInHierarchy("Exhaust_l_01");
            exhaustR  = FindInHierarchy("Exhaust_r_01");
            handRSocket = FindInHierarchy("Prop_R_Socket") ?? FindInHierarchy("Prop_R");

            // Cache all exhaust flaps
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("ExhaustFlap_"))
                    exhaustFlaps.Add(t);
            }
        }

        private Transform FindInHierarchy(string boneName)
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == boneName) return t;
            }
            return null;
        }

        private void SetupAudioSources()
        {
            footstepAudioSource      = CreateSubAudioSource("Audio_MechFootsteps",      25f, 1.0f);
            servoAudioSource         = CreateSubAudioSource("Audio_MechServos",         20f, 0.45f);
            movementServoAudioSource = CreateSubAudioSource("Audio_MechMovementServos", 30f, 0.60f);
            thrusterAudioSource      = CreateSubAudioSource("Audio_MechThrusters",      35f, 0.85f);
            weaponAudioSource        = CreateSubAudioSource("Audio_MechWeapons",        40f, 1.0f);

            // Setup looping movement servo audio
#if UNITY_EDITOR
            if (sfxTitanFootsteps == null || sfxTitanFootsteps.Length == 0)
            {
                sfxTitanFootsteps = new AudioClip[] {
                    UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Mechs/Mech_Footstep_Titan_Heavy_01.wav"),
                    UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Mechs/Mech_Footstep_Titan_Heavy_02.wav"),
                    UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Mechs/Mech_Footstep_Titan_Heavy_03.wav")
                };
            }
            if (sfxChassisReactorLoop == null)
                sfxChassisReactorLoop = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Mechs/Mech_Chassis_Reactor_Loop.wav");
            if (sfxHydraulicActuators == null || sfxHydraulicActuators.Length == 0)
            {
                sfxHydraulicActuators = new AudioClip[] {
                    UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Mechs/Mech_Hydraulic_Actuator_01.wav"),
                    UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Mechs/Mech_Hydraulic_Actuator_02.wav"),
                    UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Mechs/Mech_Hydraulic_Actuator_03.wav")
                };
            }

            movementServoAudioSource.clip = sfxChassisReactorLoop != null ? sfxChassisReactorLoop : MechAudioSynth.GetMovementServoLoop();
#else
            movementServoAudioSource.clip = sfxChassisReactorLoop != null ? sfxChassisReactorLoop : MechAudioSynth.GetMovementServoLoop();
#endif
            movementServoAudioSource.loop = true;
            movementServoAudioSource.volume = 0f;
            movementServoAudioSource.pitch = 1.0f;
            movementServoAudioSource.Play();

            lastMechPosition = transform.position;
            lastMechRotationY = transform.eulerAngles.y;

            // Setup looping thruster audio
            thrusterAudioSource.clip = MechAudioSynth.GetThrusterLoop();
            thrusterAudioSource.loop = true;
            thrusterAudioSource.volume = 0.15f;
            thrusterAudioSource.Play();
        }

        private AudioSource CreateSubAudioSource(string sourceName, float maxDist, float baseVol)
        {
            GameObject obj = new GameObject(sourceName);
            obj.transform.SetParent(transform, false);
            AudioSource src = obj.AddComponent<AudioSource>();
            src.spatialBlend = 1.0f; // 3D
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = 3f;
            src.maxDistance = maxDist;
            src.volume = baseVol;
            src.playOnAwake = false;
            return src;
        }

        private void LoadFXPrefabs()
        {
#if UNITY_EDITOR
            string fxBase = "Assets/PolygonMech/Prefabs/FX";
            if (footstepFXPrefab == null)
                footstepFXPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"{fxBase}/FX_Footstep_01.prefab");
            if (jumpImpactFXPrefab == null)
                jumpImpactFXPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"{fxBase}/FX_Jump_Impact_01.prefab");
            if (exhaustFlameFXPrefab == null)
                exhaustFlameFXPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"{fxBase}/FX_Flame_Exhaust_01.prefab");
            if (sparksFXPrefab == null)
                sparksFXPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"{fxBase}/FX_Sparks_01.prefab");
            if (missileBarrageFXPrefab == null)
                missileBarrageFXPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"{fxBase}/FX_Missile_Barrage_01.prefab");
            if (machineGunFXPrefab == null)
                machineGunFXPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"{fxBase}/FX_MachineGun_Repeating_01.prefab");
#endif
        }

        private void SetupExhaustThrusters()
        {
            if (exhaustFlameFXPrefab == null) return;

            if (exhaustL != null)
            {
                GameObject fLeft = Instantiate(exhaustFlameFXPrefab, exhaustL);
                fLeft.transform.localPosition = Vector3.zero;
                fLeft.transform.localRotation = Quaternion.Euler(0, 180, 0);
                flameLeftPS = fLeft.GetComponentInChildren<ParticleSystem>();
                flameLeftLight = fLeft.GetComponentInChildren<Light>();
                if (flameLeftLight != null) flameLeftLight.color = new Color(0.2f, 0.7f, 1f);
            }

            if (exhaustR != null)
            {
                GameObject fRight = Instantiate(exhaustFlameFXPrefab, exhaustR);
                fRight.transform.localPosition = Vector3.zero;
                fRight.transform.localRotation = Quaternion.Euler(0, 180, 0);
                flameRightPS = fRight.GetComponentInChildren<ParticleSystem>();
                flameRightLight = fRight.GetComponentInChildren<Light>();
                if (flameRightLight != null) flameRightLight.color = new Color(0.2f, 0.7f, 1f);
            }
        }

        private void Update()
        {
            UpdateHydraulicSuspension();
            DetectFootsteps();
            UpdateThrusterEffects();
            UpdateMechanicalMovementAudio();
        }

        private void UpdateMechanicalMovementAudio()
        {
            if (movementServoAudioSource == null) return;

            float dt = Mathf.Max(0.0001f, Time.deltaTime);
            Vector3 posDelta = transform.position - lastMechPosition;
            posDelta.y = 0f;
            float linearSpeed = posDelta.magnitude / dt;

            float currentRotY = transform.eulerAngles.y;
            float angularSpeed = Mathf.Abs(Mathf.DeltaAngle(lastMechRotationY, currentRotY)) / dt;

            lastMechPosition = transform.position;
            lastMechRotationY = currentRotY;

            bool isMoving = (linearSpeed > 0.15f || angularSpeed > 10f);
            float targetVol = 0f;
            float targetPitch = 1.0f;

            if (isMoving)
            {
                float speedNorm = Mathf.Clamp01(linearSpeed / 6.0f);
                float turnNorm = Mathf.Clamp01(angularSpeed / 75f);
                float activity = Mathf.Max(speedNorm, turnNorm);

                // Authentic Heavy Industrial Diesel-Electric / Reactor Chug
                targetVol = Mathf.Lerp(0.12f, 0.38f, activity);
                targetPitch = Mathf.Lerp(0.92f, 1.10f, speedNorm);

                if (angularSpeed > 28f && Time.time >= nextMoveServoWhineTime)
                {
                    nextMoveServoWhineTime = Time.time + Random.Range(1.1f, 1.7f);
                    PlayHydraulicActuator(0.40f);
                }
            }

            currentMovementServoVol = Mathf.MoveTowards(currentMovementServoVol, targetVol, dt * 3.0f);
            movementServoAudioSource.volume = currentMovementServoVol;
            movementServoAudioSource.pitch = Mathf.Lerp(movementServoAudioSource.pitch, targetPitch, dt * 4.0f);
        }

        private void LateUpdate()
        {
            ApplyChassisTurretTracking();
            CorrectArmAndWeaponAlignment();
        }

        // ══════════════════════════════════════════════════════════════════════
        // 1. CHASSIS TURRET TRACKING (Decoupled Upper Torso Aiming)
        // ══════════════════════════════════════════════════════════════════════

        private void ApplyChassisTurretTracking()
        {
            if (target == null) return;

            // Compute line of sight from spine/cockpit height down to Alita's core height (Y ~ 1.0m)
            Vector3 spineOrigin = spine01 != null ? spine01.position : (transform.position + Vector3.up * 3.2f);
            Vector3 targetCenter = target.position + Vector3.up * 1.0f;
            Vector3 toTarget = targetCenter - spineOrigin;
            Vector3 localDir = transform.InverseTransformDirection(toTarget);

            float targetYaw = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
            targetYaw = Mathf.Clamp(targetYaw, -maxChassisYaw, maxChassisYaw);

            // In Unity local Euler, positive X tilts forward/downward toward floor.
            float horizDist = Mathf.Sqrt(localDir.x * localDir.x + localDir.z * localDir.z);
            float targetPitch = -Mathf.Atan2(localDir.y, Mathf.Max(0.1f, horizDist)) * Mathf.Rad2Deg;
            targetPitch = Mathf.Clamp(targetPitch, -15f, maxChassisPitch);

            float prevYaw = currentChassisYaw;
            currentChassisYaw   = Mathf.Lerp(currentChassisYaw, targetYaw, Time.deltaTime * chassisTurnSpeed);
            currentChassisPitch = Mathf.Lerp(currentChassisPitch, targetPitch, Time.deltaTime * chassisTurnSpeed);

            // Servo sound when turret is slewing
            float angularDelta = Mathf.Abs(currentChassisYaw - prevYaw) / Time.deltaTime;
            if (angularDelta > 15f && !servoAudioSource.isPlaying)
            {
                servoAudioSource.clip = MechAudioSynth.GetServoWhine();
                servoAudioSource.volume = Mathf.Clamp01(angularDelta / 60f) * 0.45f;
                servoAudioSource.Play();
            }

            // Distribute rotation smoothly across the 3 spine joints
            Quaternion spineRot = Quaternion.Euler(currentChassisPitch * 0.33f, currentChassisYaw * 0.33f, 0f);

            if (spine01 != null) spine01.localRotation *= spineRot;
            if (spine02 != null) spine02.localRotation *= spineRot;
            if (spine03 != null) spine03.localRotation *= spineRot;
            if (headBone != null) headBone.localRotation *= Quaternion.Euler(currentChassisPitch * 0.2f, currentChassisYaw * 0.2f, 0f);
        }

        // ══════════════════════════════════════════════════════════════════════
        // 2. HYDRAULIC PISTON SUSPENSION (Spring-Damper Pelvis Shock)
        // ══════════════════════════════════════════════════════════════════════

        private void UpdateHydraulicSuspension()
        {
            if (hipsBone == null) return;

            // Critically damped spring simulation: F = -k*x - d*v
            float force = -suspensionStiffness * hipsCurrentCompression - suspensionDamping * hipsVelocity;
            hipsVelocity += force * Time.deltaTime;
            hipsCurrentCompression += hipsVelocity * Time.deltaTime;

            hipsBone.localPosition = hipsOriginalLocalPos + new Vector3(0, hipsCurrentCompression, 0);
        }

        public void TriggerPelvisCompression(float intensity = 0.12f)
        {
            hipsCurrentCompression = -intensity;
            hipsVelocity = 0f;
        }

        // ══════════════════════════════════════════════════════════════════════
        // 3. SEISMIC FOOTSTEPS & CAMERA SHAKE
        // ══════════════════════════════════════════════════════════════════════

        private void DetectFootsteps()
        {
            if (ankleL == null || ankleR == null) return;

            float curLeftY  = ankleL.position.y;
            float curRightY = ankleR.position.y;

            float groundThreshold = transform.position.y + 0.35f;

            // Left foot contact detection
            if (curLeftY <= groundThreshold && !leftFootGrounded && Time.time >= nextFootstepTime)
            {
                leftFootGrounded = true;
                OnFootStrike(ankleL.position, true);
                nextFootstepTime = Time.time + 0.22f;
            }
            else if (curLeftY > groundThreshold + 0.12f)
            {
                leftFootGrounded = false;
            }

            // Right foot contact detection
            if (curRightY <= groundThreshold && !rightFootGrounded && Time.time >= nextFootstepTime)
            {
                rightFootGrounded = true;
                OnFootStrike(ankleR.position, false);
                nextFootstepTime = Time.time + 0.22f;
            }
            else if (curRightY > groundThreshold + 0.12f)
            {
                rightFootGrounded = false;
            }

            lastLeftFootY  = curLeftY;
            lastRightFootY = curRightY;
        }

        private void OnFootStrike(Vector3 footPos, bool isLeft)
        {
            // 1. Hydraulic suspension bounce
            TriggerPelvisCompression(footstepCompression);

            // 2. Play Hollywood Titan Stomp (Sub-bass seismic tremor + steel plate slam + concrete crush + air purge)
            if (footstepAudioSource != null)
            {
                AudioClip stepClip = null;
                if (sfxTitanFootsteps != null && sfxTitanFootsteps.Length > 0)
                {
                    stepClip = sfxTitanFootsteps[Random.Range(0, sfxTitanFootsteps.Length)];
                }
                if (stepClip == null) stepClip = MechAudioSynth.GetFootstepStomp();

                footstepAudioSource.pitch = Random.Range(0.95f, 1.05f);
                footstepAudioSource.PlayOneShot(stepClip, 1.0f);

                PlayHydraulicActuator(0.40f);
            }

            // 3. Concrete dust blast
            if (footstepFXPrefab != null)
            {
                Vector3 spawnPos = new Vector3(footPos.x, transform.position.y + 0.05f, footPos.z);
                GameObject dust = Instantiate(footstepFXPrefab, spawnPos, Quaternion.identity);
                Destroy(dust, 1.8f);
            }

            // 4. Camera Shake
            TriggerProximityCameraShake(footstepShakeIntensity);
        }

        public void PlayHydraulicActuator(float volume = 0.45f)
        {
            if (servoAudioSource == null || sfxHydraulicActuators == null || sfxHydraulicActuators.Length == 0) return;
            AudioClip actClip = sfxHydraulicActuators[Random.Range(0, sfxHydraulicActuators.Length)];
            if (actClip != null)
            {
                servoAudioSource.pitch = Random.Range(0.94f, 1.06f);
                servoAudioSource.PlayOneShot(actClip, volume);
            }
        }

        public void TriggerProximityCameraShake(float intensity)
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            float dist = Vector3.Distance(cam.transform.position, transform.position);
            if (dist > maxShakeDistance) return;

            float factor = 1.0f - Mathf.Clamp01(dist / maxShakeDistance);
            float shakeAmount = intensity * factor;

            // Direct procedural camera impulse
            StartCoroutine(PerformCameraShake(cam.transform, shakeAmount, 0.25f));
        }

        private IEnumerator PerformCameraShake(Transform camTransform, float amount, float duration)
        {
            Vector3 originalPos = camTransform.localPosition;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float damp = 1.0f - (elapsed / duration);
                float x = (Random.value * 2f - 1f) * amount * damp;
                float y = (Random.value * 2f - 1f) * amount * damp;

                camTransform.localPosition = originalPos + new Vector3(x, y, 0);
                yield return null;
            }

            camTransform.localPosition = originalPos;
        }

        // ══════════════════════════════════════════════════════════════════════
        // 4. THRUSTER EXHAUST & FLAP ARTICULATION
        // ══════════════════════════════════════════════════════════════════════

        public void SetThrusterBoost(float intensity)
        {
            boostIntensity = intensity;
            isBoosting = (intensity > 1.2f);

            if (isBoosting && thrusterAudioSource != null)
            {
                thrusterAudioSource.PlayOneShot(MechAudioSynth.GetThrusterIgnition(), 0.9f);
            }
        }

        private void UpdateThrusterEffects()
        {
            // Modulate looping rumble volume & pitch
            if (thrusterAudioSource != null)
            {
                float targetVol = Mathf.Lerp(0.18f, 0.95f, Mathf.Clamp01((boostIntensity - 0.3f) / 1.7f));
                float targetPitch = Mathf.Lerp(0.85f, 1.35f, Mathf.Clamp01((boostIntensity - 0.3f) / 1.7f));
                thrusterAudioSource.volume = Mathf.Lerp(thrusterAudioSource.volume, targetVol, Time.deltaTime * 6f);
                thrusterAudioSource.pitch  = Mathf.Lerp(thrusterAudioSource.pitch, targetPitch, Time.deltaTime * 6f);
            }

            // Adjust particle systems scale/rate
            float flameScale = Mathf.Lerp(0.4f, 1.8f, Mathf.Clamp01(boostIntensity / 2f));

            if (flameLeftPS != null)
            {
                var main = flameLeftPS.main;
                main.startSize = flameScale;
            }
            if (flameRightPS != null)
            {
                var main = flameRightPS.main;
                main.startSize = flameScale;
            }

            if (flameLeftLight != null) flameLeftLight.intensity = flameScale * 3.5f;
            if (flameRightLight != null) flameRightLight.intensity = flameScale * 3.5f;

            // Procedurally open exhaust flaps when afterburners fire
            float flapAngle = Mathf.Lerp(0f, 32f, Mathf.Clamp01((boostIntensity - 0.8f) / 1.2f));
            for (int i = 0; i < exhaustFlaps.Count; i++)
            {
                if (exhaustFlaps[i] != null)
                {
                    exhaustFlaps[i].localRotation = Quaternion.Euler(flapAngle, 0, 0);
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // 5. ARM & WEAPON SOCKET CALIBRATION (No Armor Clipping)
        // ══════════════════════════════════════════════════════════════════════

        private void CorrectArmAndWeaponAlignment()
        {
            // Rest Clavicles to prevent armor plates from clipping through the cockpit frame
            if (clavicleL != null)
            {
                clavicleL.localRotation *= Quaternion.Euler(0, -6f, 0);
            }
            if (clavicleR != null)
            {
                clavicleR.localRotation *= Quaternion.Euler(0, 6f, 0);
            }

            // Calibrate sword socket forward orientation so the blade points naturally down/forward
            if (handRSocket != null)
            {
                Transform sword = handRSocket.Find("SM_Wep_Sword_01") ?? handRSocket.Find("SM_Wep_Katana_01");
                if (sword != null)
                {
                    sword.localPosition = Vector3.zero;
                    sword.localRotation = Quaternion.identity;
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // 6. HEAVY WEAPON & COMBAT EMITTERS
        // ══════════════════════════════════════════════════════════════════════

        public void EmitSeismicGroundSlam(Vector3 slamPoint, float radius = 7.5f, float damage = 45f)
        {
            // 1. Particle shockwave
            if (jumpImpactFXPrefab != null)
            {
                GameObject fx = Instantiate(jumpImpactFXPrefab, slamPoint, Quaternion.identity);
                Destroy(fx, 3.0f);
            }

            // 2. Heavy seismic audio
            if (weaponAudioSource != null)
            {
                weaponAudioSource.pitch = Random.Range(0.9f, 1.05f);
                weaponAudioSource.PlayOneShot(MechAudioSynth.GetSeismicSlam(), 1.0f);
            }

            // 3. Screen shudder
            TriggerProximityCameraShake(slamShakeIntensity);

            // 4. Damage check
            Collider[] colliders = Physics.OverlapSphere(slamPoint, radius);
            foreach (var col in colliders)
            {
                if (col.transform.root == transform.root) continue;
                SyntyCombined.ISyntyDamageable dmg = col.GetComponentInParent<SyntyCombined.ISyntyDamageable>();
                if (dmg != null)
                {
                    Vector3 knockDir = (col.transform.position - slamPoint).normalized;
                    knockDir.y = 0.4f;
                    dmg.TakeDamage(damage, col.transform.position, knockDir, true);
                }
            }
        }

        public void EmitSkaterDashSparks()
        {
            if (sparksFXPrefab != null && ankleL != null && ankleR != null)
            {
                GameObject spL = Instantiate(sparksFXPrefab, ankleL.position, Quaternion.identity);
                GameObject spR = Instantiate(sparksFXPrefab, ankleR.position, Quaternion.identity);
                Destroy(spL, 1.5f);
                Destroy(spR, 1.5f);
            }
        }

        public void FireMissileBarrage(Transform aimTarget, int missileCount = 6)
        {
            StartCoroutine(MissileSalvoCoroutine(aimTarget, missileCount));
        }

        private IEnumerator MissileSalvoCoroutine(Transform aimTarget, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 launchPos = transform.position + Vector3.up * 3.2f + (i % 2 == 0 ? transform.right : -transform.right) * 1.4f;

                if (missileBarrageFXPrefab != null)
                {
                    Quaternion rot = Quaternion.LookRotation(transform.forward + Vector3.up * 0.3f);
                    GameObject missile = Instantiate(missileBarrageFXPrefab, launchPos, rot);
                    Destroy(missile, 2.5f);
                }

                if (weaponAudioSource != null)
                {
                    weaponAudioSource.pitch = Random.Range(0.95f, 1.15f);
                    weaponAudioSource.PlayOneShot(MechAudioSynth.GetMissileLaunch(), 0.85f);
                }

                yield return new WaitForSeconds(0.12f);
            }
        }

        public void FireGatlingBurst(Transform aimTarget, float duration = 1.8f)
        {
            StartCoroutine(GatlingBurstCoroutine(aimTarget, duration));
        }

        private IEnumerator GatlingBurstCoroutine(Transform aimTarget, float duration)
        {
            float elapsed = 0f;
            Vector3 armPos = transform.position + Vector3.up * 2.2f - transform.right * 1.5f;

            GameObject mGun = null;
            if (machineGunFXPrefab != null)
            {
                mGun = Instantiate(machineGunFXPrefab, armPos, transform.rotation, transform);
            }

            while (elapsed < duration)
            {
                elapsed += 0.08f;
                if (weaponAudioSource != null)
                {
                    weaponAudioSource.pitch = Random.Range(0.9f, 1.1f);
                    weaponAudioSource.PlayOneShot(MechAudioSynth.GetGatlingShot(), 0.75f);
                }
                yield return new WaitForSeconds(0.08f);
            }

            if (mGun != null) Destroy(mGun);
        }
    }
}
