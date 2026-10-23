using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using InsaneGun.Combat;
using SyntyCombined;
using SyntyCombined.Cutscene;
using Alita.UI;
using Cyberpunk.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Cyberpunk.Missions
{
    /// <summary>
    /// Master Cinematic Director for Dr. Dyson Ido's tragic execution on the Sector 7 Skybridge in Demo_City.
    /// Features:
    /// 1. Vulcan Robot Boss (from Mission 4) drops from high altitude with ground slam crater shockwave.
    /// 2. Guardians of the Galaxy style title card: "VULCAN // FORGE-LORD & FACTORY EXECUTIONER".
    /// 3. Dramatic face-to-face confrontation between Dr. Ido, Alita, and the towering Robot Boss.
    /// 4. Robot Boss performs the iconic Heavy Stab animation from Mission 4 (A_Attack_HeavyStab01_Sword).
    /// 5. Visceral impalement through Dr. Ido's torso with hitstop time-dilation, FX_SwordStab_01, blood splat, and cybernetic sparks.
    /// 6. Dr. Ido collapses onto the bridge deck (A_Death_B_01_Sword) delivering his heartbreaking final words.
    /// 7. Alita draws her blade in furious resolve, vowing vengeance as the robot boss dispatches his recovery squad.
    /// 8. Seamless blend back to gameplay and Mission 1 combat, leaving Ido's fallen body on the bridge with an interactive memorial prompt.
    /// </summary>
    public class IdoBridgeExecutionDirector : MonoBehaviour
    {
        public static IdoBridgeExecutionDirector Instance { get; private set; }

        public const string PREF_IDO_KILLED_IN_SESSION = "ido_bridge_killed_in_session";
        public static bool HasIdoBeenExecuted { get; set; } = false;

        [Header("State")]
        public bool isCutsceneRunning = false;
        public bool hasFallenIdoSpawned = false;

        [Header("Key Positions on Sector 7 Skybridge")]
        public Vector3 bridgeCenterPos = new Vector3(0.00f, 0.08f, -9.33f);
        public Vector3 idoStandoffPos = new Vector3(-1.00f, 0.08f, -9.33f);
        public Vector3 alitaStandoffPos = new Vector3(-3.60f, 0.08f, -9.33f);
        public Vector3 bossDropSpawnPos = new Vector3(3.80f, 18.0f, -9.33f);
        public Vector3 bossLandPos = new Vector3(2.60f, 0.08f, -9.33f);

        [Header("Actors")]
        public GameObject playerObject;
        public GameObject doctorActor;
        public GameObject bossActor;
        public Animator bossAnimator;
        public Animator doctorAnimator;

        [Header("Audio Clips")]
        public AudioClip voiceVulcanIntro;
        public AudioClip voiceAlitaRage;
        public AudioClip sfxThunder;
        public AudioClip sfxGroundSlam;
        public AudioClip sfxSwordThrust;
        public AudioClip sfxSwordPull;
        public AudioClip musicOminous;

        [Header("Animation Clips (Mission 4 Stabbing & Death)")]
        public AnimationClip clipHeavyStab;
        public AnimationClip clipIdoDeath;
        public AnimationClip clipIdoStabDeath;
        public AnimationClip clipIdoHitFlinch;
        public AnimationClip clipBossIdleMenacing;

        [Header("VFX Prefabs")]
        public GameObject vfxSwordStabPrefab;
        public GameObject vfxBloodSplatPrefab;
        public GameObject vfxElectricityPrefab;

        // Private Cutscene State
        private Camera _cutsceneCam;
        private SmoothOrbitCamera _cachedOrbitCam;
        private Coroutine _cameraTrackRoutine;
        private Coroutine _shakeRoutine;
        private Vector3 _currentCamBasePos;
        private Vector3 _shakeOffset = Vector3.zero;

        private AudioSource _musicSource;
        private AudioSource _sfxSource;
        private AudioSource _voiceSource;

        private GameObject _fallenIdoMemorialObj;
        private bool _isKneelingBesideIdo = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrapInDemoCity()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.name.IndexOf("Demo_City", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                EnsureInCity();
            }
        }

        public static IdoBridgeExecutionDirector EnsureInCity()
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<IdoBridgeExecutionDirector>();
            if (existing != null)
            {
                Instance = existing;
                return Instance;
            }

            GameObject go = new GameObject("[Ido_Bridge_Execution_Director]");
            Instance = go.AddComponent<IdoBridgeExecutionDirector>();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            SetupAudioSources();
            LoadAssets();
        }

        private void SetupAudioSources()
        {
            if (_musicSource == null)
            {
                _musicSource = gameObject.AddComponent<AudioSource>();
                _musicSource.spatialBlend = 0f;
                _musicSource.loop = true;
                _musicSource.playOnAwake = false;
                _musicSource.volume = 0.85f;
            }
            if (_voiceSource == null)
            {
                _voiceSource = gameObject.AddComponent<AudioSource>();
                _voiceSource.spatialBlend = 0f;
                _voiceSource.loop = false;
                _voiceSource.playOnAwake = false;
                _voiceSource.priority = 0;
                _voiceSource.volume = 1.0f;
            }
            if (_sfxSource == null)
            {
                _sfxSource = gameObject.AddComponent<AudioSource>();
                _sfxSource.spatialBlend = 0f;
                _sfxSource.loop = false;
                _sfxSource.playOnAwake = false;
                _sfxSource.volume = 1.0f;
            }
        }

        public void LoadAssets()
        {
#if UNITY_EDITOR
            if (voiceVulcanIntro == null) voiceVulcanIntro = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Voice/Vulcan/vulcan_01_intro.wav");
            if (voiceAlitaRage == null) voiceAlitaRage = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Voice/Blackwall/alita_01_attack.wav");
            if (sfxThunder == null) sfxThunder = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Weather/dragon-studio-dry-thunder-364468.mp3");
            if (sfxGroundSlam == null) sfxGroundSlam = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Combat/Sword_Slash_Heavy_Cleave_01.wav");
            if (sfxSwordThrust == null) sfxSwordThrust = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Combat/Sword_Slash_Piercing_Thrust_01.wav");
            if (sfxSwordPull == null) sfxSwordPull = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Combat/Sword_Slash_Light_02.wav");
            if (musicOminous == null) musicOminous = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/vulcan_battle/viramiller-ominous-284554.mp3");

            if (clipHeavyStab == null)
            {
                clipHeavyStab = LoadClipFromFBX("Assets/Synty/AnimationSwordCombat/Animations/Polygon/Attack/HeavyStab01/A_Attack_HeavyStab01_Sword.fbx", "A_Attack_HeavyStab01_Sword")
                             ?? LoadClipFromFBX("Assets/Nephilite Studios/Straight Sword Animation Set/Animations/Straight Sword Animations/Nephilim Animations (Remastered)/Straight Sword Critical Strike/core_main_riposte_01.fbx", "core_main_riposte_01")
                             ?? LoadClipFromFBX("Assets/InsaneGun_Sword_Set/Animation/Humanoid/atk_sword_05.fbx", "atk_sword_05");
            }

            if (clipIdoStabDeath == null)
            {
                // The authentic stabbing/impaled front death reaction: victim pierced in torso, clutches blade in shock, drops to knees, and collapses onto deck
                clipIdoStabDeath = LoadClipFromFBX("Assets/Nephilite Studios/Straight Sword Animation Set/Animations/Straight Sword Animations/Nephilim Animations (Remastered)/Straight Sword Critical Strike/core_main_riposte_victim_01.fbx", "core_main_riposte_victim_01")
                                ?? LoadClipFromFBX("Assets/Nephilite Studios/Straight Sword Animation Set/Animations/Straight Sword Animations/Nephilim Animations (Remastered)/Straight Sword Critical Strike/core_riposte_death_01.fbx", "core_riposte_death_01")
                                ?? LoadClipFromFBX("Assets/InsaneGun_Sword_Set/Animation/Humanoid/dead_04.fbx", "dead_04")
                                ?? LoadClipFromFBX("Assets/Synty/AnimationSwordCombat/Animations/Polygon/Death/A_Death_F_01_Sword.fbx", "A_Death_F_01_Sword");
            }
            if (clipIdoDeath == null) clipIdoDeath = clipIdoStabDeath;

            if (clipIdoHitFlinch == null)
            {
                clipIdoHitFlinch = LoadClipFromFBX("Assets/Synty/AnimationSwordCombat/Animations/Polygon/Hit/HitReact/A_Hit_F_React_Sword.fbx", "A_Hit_F_React_Sword")
                                ?? LoadClipFromFBX("Assets/InsaneGun_Sword_Set/Animation/Humanoid/hit_body_front.fbx", "hit_body_front");
            }

            if (clipBossIdleMenacing == null)
            {
                clipBossIdleMenacing = LoadClipFromFBX("Assets/Synty/AnimationSwordCombat/Animations/Polygon/Idle/Menacing01/A_Idle_Menacing01_Sword.fbx", "A_Idle_Menacing01_Sword")
                                    ?? LoadClipFromFBX("Assets/InsaneGun_Sword_Set/Animation/Humanoid/idle.fbx", "idle");
            }

            if (vfxSwordStabPrefab == null) vfxSwordStabPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonParticles/Prefabs/FX_SwordStab_01.prefab");
            if (vfxBloodSplatPrefab == null) vfxBloodSplatPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonParticles/Prefabs/FX_BloodSplat_01.prefab");
            if (vfxElectricityPrefab == null) vfxElectricityPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonParticles/Prefabs/FX_Electricity_01.prefab");
#endif
        }

        private static AnimationClip LoadClipFromFBX(string fbxPath, string clipName = null)
        {
#if UNITY_EDITOR
            var assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            if (assets != null)
            {
                AnimationClip firstValid = null;
                foreach (var asset in assets)
                {
                    if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    {
                        if (firstValid == null) firstValid = clip;
                        if (!string.IsNullOrEmpty(clipName) && string.Equals(clip.name, clipName, StringComparison.OrdinalIgnoreCase))
                            return clip;
                    }
                }
                if (!string.IsNullOrEmpty(clipName) && firstValid != null)
                {
                    Debug.Log($"[IdoBridgeExecution] Using clip '{firstValid.name}' from '{fbxPath}'");
                }
                return firstValid;
            }
#endif
            return null;
        }

        private RuntimeAnimatorController BuildBossCutsceneController()
        {
#if UNITY_EDITOR
            var baseController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/InsaneGun_Sword_Set/AC_InsaneGun_CombatMaster.controller");
            if (baseController == null) return null;

            var aoc = new AnimatorOverrideController(baseController);
            aoc.name = "AOC_IdoBridge_Vulcan_Cutscene";

            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            aoc.GetOverrides(overrides);

            for (int i = 0; i < overrides.Count; i++)
            {
                var original = overrides[i].Key;
                if (original == null) continue;
                string origName = original.name;

                if (origName == "atk_sword_05" && clipHeavyStab != null)
                {
                    overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(original, clipHeavyStab);
                }
                else if (origName == "N_idle" && clipBossIdleMenacing != null)
                {
                    overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(original, clipBossIdleMenacing);
                }
            }

            aoc.ApplyOverrides(overrides);
            return aoc;
#else
            return null;
#endif
        }

        private RuntimeAnimatorController BuildDoctorCutsceneController()
        {
#if UNITY_EDITOR
            var baseController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/InsaneGun_Sword_Set/AC_InsaneGun_CombatMaster.controller");
            if (baseController == null) return null;

            var aoc = new AnimatorOverrideController(baseController);
            aoc.name = "AOC_IdoBridge_Doctor_Cutscene";

            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            aoc.GetOverrides(overrides);

            for (int i = 0; i < overrides.Count; i++)
            {
                var original = overrides[i].Key;
                if (original == null) continue;
                string origName = original.name;

                // Override Death_04 with authentic impalement/stabbing death reaction
                if (origName == "dead_04" && clipIdoStabDeath != null)
                {
                    overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(original, clipIdoStabDeath);
                }
                else if (origName == "dead_01" && clipIdoStabDeath != null)
                {
                    overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(original, clipIdoStabDeath);
                }
                else if (origName == "hit_body_front" && clipIdoHitFlinch != null)
                {
                    overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(original, clipIdoHitFlinch);
                }
            }

            aoc.ApplyOverrides(overrides);
            return aoc;
#else
            return null;
#endif
        }


        [ContextMenu("Debug Trigger Ido Death Cutscene")]
        public void DebugTriggerCutscene()
        {
            TriggerExecutionCutscene(null, force: true);
        }

        [ContextMenu("Reset Cutscene Progress")]
        public void ResetProgress()
        {
            HasIdoBeenExecuted = false;
            PlayerPrefs.DeleteKey(PREF_IDO_KILLED_IN_SESSION);
            PlayerPrefs.Save();
            Debug.Log("[IdoBridgeExecution] Progress reset.");
        }

        /// <summary>
        /// Main entry point to play the full cinematic murder of Dr. Ido on the bridge.
        /// </summary>
        public void TriggerExecutionCutscene(Action onComplete = null, bool force = false)
        {
            if (!force && HasIdoBeenExecuted)
            {
                Debug.Log("[IdoBridgeExecution] Ido has already been executed in this session.");
                onComplete?.Invoke();
                return;
            }

            if (isCutsceneRunning)
            {
                Debug.LogWarning("[IdoBridgeExecution] Cutscene is already running!");
                return;
            }

            StartCoroutine(ExecutionCutsceneRoutine(onComplete));
        }

        private IEnumerator ExecutionCutsceneRoutine(Action onComplete)
        {
            isCutsceneRunning = true;
            HasIdoBeenExecuted = true;
            PlayerPrefs.SetInt(PREF_IDO_KILLED_IN_SESSION, 1);
            PlayerPrefs.Save();

            Debug.Log("[IdoBridgeExecution] Initiating cinematic execution of Dr. Ido by Mission 4 Robot Boss on Sector 7 Skybridge!");

            LoadAssets();

            // 1. Prepare Camera
            _cutsceneCam = Camera.main ?? FindAnyObjectByType<Camera>();
            if (_cutsceneCam != null)
            {
                _cachedOrbitCam = _cutsceneCam.GetComponent<SmoothOrbitCamera>();
                if (_cachedOrbitCam != null) _cachedOrbitCam.enabled = false;
            }

            // 2. Prepare Alita
            PrepareAlita();

            // 3. Prepare Dr. Ido
            PrepareDoctorIdo();

            // 4. Slide in Cinematic 2.39:1 Letterbox Bars
            GotGTitleCardUI.GetOrCreate().SetLetterboxOnly(true, 0.45f);

            // [Shot 1: 0.0s - 2.5s] Establishing Skybridge Tension & Distant Thunder
            Vector3 shot1Pos = new Vector3(-6.80f, 2.40f, -14.50f);
            Quaternion shot1Rot = Quaternion.LookRotation((bridgeCenterPos + Vector3.up * 1.2f) - shot1Pos);
            SetCameraTransform(shot1Pos, shot1Rot, 58f);

            if (_musicSource != null && musicOminous != null)
            {
                _musicSource.clip = musicOminous;
                _musicSource.loop = true;
                _musicSource.volume = 0.85f;
                _musicSource.Play();
            }

            // Dr. Ido turns looking out into the misty sky
            if (doctorActor != null)
            {
                doctorActor.transform.position = idoStandoffPos;
                doctorActor.transform.rotation = Quaternion.Euler(0f, 90f, 0f); // Facing forward along bridge (+X)
            }
            if (playerObject != null)
            {
                playerObject.transform.position = alitaStandoffPos;
                playerObject.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            }

            CyberpunkDialogueHUD.GetOrCreate().ShowDialogue(
                "DR. DYSON IDO",
                "Alita, wait... look at the sky! Energy telemetry spiking right above the bridge!",
                null,
                "CYBER-SURGEON & HUNTER-WARRIOR",
                null
            );

            yield return new WaitForSecondsRealtime(2.8f);

            // [Shot 2: 2.8s - 6.5s] Skyward Thunder Drop & GroundSlam of the Robot Boss
            Vector3 shot2Pos = new Vector3(0.50f, 0.95f, -6.00f);
            Quaternion shot2Rot = Quaternion.Euler(-55f, 40f, 0f); // Looking high up at industrial bridge arches
            SetCameraTransform(shot2Pos, shot2Rot, 65f);

            if (_sfxSource != null && sfxThunder != null)
            {
                _sfxSource.PlayOneShot(sfxThunder, 1.0f);
            }
            TriggerScreenShake(0.6f, 0.5f);

            // Spawn Robot Boss (Vulcan from Mission 4)
            SpawnRobotBoss(bossDropSpawnPos);

            // Plunge Robot Boss down onto bridge deck over 0.70s
            float dropDuration = 0.70f;
            float dropElapsed = 0f;
            Vector3 startPos = bossDropSpawnPos;
            Vector3 endPos = bossLandPos;

            while (dropElapsed < dropDuration)
            {
                dropElapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(dropElapsed / dropDuration);
                float easeInT = t * t * t;
                if (bossActor != null)
                {
                    bossActor.transform.position = Vector3.Lerp(startPos, endPos, easeInT);
                }

                float camPitch = Mathf.Lerp(-55f, -12f, easeInT);
                if (_cutsceneCam != null)
                {
                    _cutsceneCam.transform.rotation = Quaternion.Euler(camPitch, 40f, 0f);
                }
                yield return null;
            }

            if (bossActor != null) bossActor.transform.position = bossLandPos;

            // GroundSlam impact shockwave & heavy screen shake
            if (_sfxSource != null && sfxGroundSlam != null)
            {
                _sfxSource.PlayOneShot(sfxGroundSlam, 1.0f);
            }
            TriggerScreenShake(1.9f, 0.95f);
            SpawnImpactVFX(bossLandPos);

            // Visceral slow-mo impact freeze (0.16x time scale)
            Time.timeScale = 0.16f;
            yield return new WaitForSecondsRealtime(0.55f);
            Time.timeScale = 1.0f;

            // [Shot 3: 6.5s - 10.5s] Low-Angle Robot Boss Reveal & Title Card Slam
            Vector3 shot3Pos = new Vector3(4.80f, 0.85f, -7.20f);
            Quaternion shot3Rot = Quaternion.LookRotation((bossLandPos + Vector3.up * 1.4f) - shot3Pos);
            StartCameraTransition(shot3Pos, shot3Rot, 46f, 2.0f);

            if (bossAnimator != null)
            {
                bossAnimator.CrossFadeInFixedTime("Locomotion", 0.4f, 0);
            }

            GotGTitleCardUI.GetOrCreate().ShowTitleCard(
                title: "VULCAN",
                subtitle: "FORGE-LORD & FACTORY EXECUTIONER",
                details: "SECTOR 7 SKYBRIDGE  •  TARGET: URM-004  •  HARBORING PENALTY: DEATH",
                duration: 5.0f
            );

            yield return new WaitForSecondsRealtime(4.2f);

            // [Shot 4: 10.5s - 15.0s] Ido's Heroic Stand & Robot Boss Verdict
            Vector3 shot4Pos = new Vector3(-2.20f, 1.40f, -6.80f);
            Quaternion shot4Rot = Quaternion.LookRotation((idoStandoffPos + Vector3.up * 1.3f) - shot4Pos);
            StartCameraTransition(shot4Pos, shot4Rot, 44f, 1.8f);

            // Dr. Ido steps forward defensively to shield Alita
            if (doctorActor != null)
            {
                doctorActor.transform.position = idoStandoffPos;
                doctorActor.transform.rotation = Quaternion.Euler(0f, 90f, 0f); // Facing boss (+X)
            }
            if (doctorAnimator != null)
            {
                doctorAnimator.CrossFadeInFixedTime("Locomotion", 0.2f, 0);
            }

            CyberpunkDialogueHUD.GetOrCreate().ShowDialogue(
                "DR. DYSON IDO",
                "Vulcan! Stand down! She's under my medical care—she belongs to no corporation!",
                null,
                "CYBER-SURGEON & HUNTER-WARRIOR",
                null
            );

            yield return new WaitForSecondsRealtime(4.5f);

            // Boss verdict
            CyberpunkDialogueHUD.GetOrCreate().ShowDialogue(
                "VULCAN",
                "NEGATIVE, DOCTOR. CONTRABAND CONFIRMED: MARTIAN QUANTUM CORE URM-004. HARBORING PENALTY: IMMEDIATE TERMINATION.",
                null,
                "FACTORY EXECUTIONER",
                null
            );

            if (_voiceSource != null && voiceVulcanIntro != null)
            {
                _voiceSource.PlayOneShot(voiceVulcanIntro, 1.0f);
            }

            yield return new WaitForSecondsRealtime(4.8f);

            // [Shot 5: 15.0s - 19.5s] The Stabbing Execution (HeavyStab01 from Mission 4)
            // Dynamic side-profile 45-degree angle framing the impalement
            Vector3 shot5Pos = new Vector3(-0.35f, 1.25f, -12.60f);
            Vector3 lookAtTarget = new Vector3(-0.35f, 1.15f, -9.33f);
            Quaternion shot5Rot = Quaternion.LookRotation(lookAtTarget - shot5Pos);
            SetCameraTransform(shot5Pos, shot5Rot, 42f);

            // Boss advances 1.35m closer into lethal stab range
            Vector3 stabLungePos = idoStandoffPos + new Vector3(1.35f, 0f, 0f);
            if (bossActor != null)
            {
                bossActor.transform.position = stabLungePos;
                bossActor.transform.rotation = Quaternion.Euler(0f, -90f, 0f); // Facing -X toward Ido
            }

            if (doctorActor != null)
            {
                doctorActor.transform.position = idoStandoffPos;
                doctorActor.transform.rotation = Quaternion.Euler(0f, 90f, 0f); // Facing +X toward Boss
            }

            // Play Mission 4 Heavy Stab animation on the Robot Boss!
            if (bossAnimator != null)
            {
                bossAnimator.CrossFadeInFixedTime("Attack_05", 0.08f, 0);
            }

            if (_sfxSource != null && sfxSwordThrust != null)
            {
                _sfxSource.PlayOneShot(sfxSwordThrust, 1.0f);
            }

            // Wait until the blade reaches forward apex (~0.35s into clip)
            yield return new WaitForSecondsRealtime(0.35f);

            // ─── LETHAL IMPALEMENT HITSTOP & IDO STABBING DEATH ───────────────
            Vector3 chestImpalePoint = idoStandoffPos + Vector3.up * 1.22f;
            SpawnStabImpactEffects(chestImpalePoint);
            TriggerScreenShake(1.8f, 0.65f);

            // Trigger the authentic stabbing death animation on Dr. Ido!
            // core_main_riposte_victim_01 begins: clutches the blade penetrating his chest in shock and agony!
            if (doctorAnimator != null)
            {
                doctorAnimator.CrossFadeInFixedTime("Death_04", 0.05f, 0);
            }

            // Visceral slow motion hitstop time freeze (0.18x time scale)
            Time.timeScale = 0.18f;
            yield return new WaitForSecondsRealtime(0.65f);
            Time.timeScale = 1.0f;

            // [Shot 6: 19.5s - 25.5s] The Fall of Dr. Ido
            if (_sfxSource != null && sfxSwordPull != null)
            {
                _sfxSource.PlayOneShot(sfxSwordPull, 1.0f);
            }

            // Boss pulls blade back and returns to imposing posture
            if (bossAnimator != null)
            {
                bossAnimator.CrossFadeInFixedTime("Locomotion", 0.45f, 0);
            }

            // Camera moves in for intimate low-angle tragedy shot of falling Ido
            Vector3 shot6Pos = idoStandoffPos + new Vector3(-1.40f, 0.55f, 1.60f);
            Quaternion shot6Rot = Quaternion.LookRotation((idoStandoffPos + Vector3.up * 0.35f) - shot6Pos);
            StartCameraTransition(shot6Pos, shot6Rot, 36f, 2.5f);

            // Wait for Dr. Ido to sink to knees and collapse onto bridge deck
            yield return new WaitForSecondsRealtime(2.4f);

            // Dr. Ido's heartbreaking final words
            CyberpunkDialogueHUD.GetOrCreate().ShowDialogue(
                "DR. DYSON IDO",
                "Alita... your core... they can never erase who you are... Avenge... us...",
                null,
                "CYBER-SURGEON & FATHER",
                null
            );

            yield return new WaitForSecondsRealtime(5.2f);

            // [Shot 7: 25.5s - 30.5s] Alita's Agony & Vow of Vengeance
            Vector3 shot7Pos = alitaStandoffPos + new Vector3(1.10f, 0.75f, 1.30f);
            Quaternion shot7Rot = Quaternion.LookRotation((alitaStandoffPos + Vector3.up * 1.15f) - shot7Pos);
            SetCameraTransform(shot7Pos, shot7Rot, 38f);

            // Alita draws blade, locking into fierce combat stance
            if (playerObject != null)
            {
                var pAnim = playerObject.GetComponent<Animator>();
                if (pAnim != null)
                {
                    pAnim.SetBool("IsSwordDrawn", true);
                    pAnim.Play("Locomotion", 0, 0f);
                }
            }

            if (_voiceSource != null && voiceAlitaRage != null)
            {
                _voiceSource.PlayOneShot(voiceAlitaRage, 1.0f);
            }

            CyberpunkDialogueHUD.GetOrCreate().ShowDialogue(
                "ALITA",
                "IDO! NO! ... You murdered him! I swear on my Martian core... I WILL TEAR EVERY ONE OF YOU APART!",
                null,
                "MARTIAN CYBORG",
                null
            );

            TriggerScreenShake(0.7f, 1.2f);
            yield return new WaitForSecondsRealtime(5.0f);

            // Boss command to recovery squad & departure
            CyberpunkDialogueHUD.GetOrCreate().ShowDialogue(
                "VULCAN",
                "Harborer neutralized. Recovery squad: eliminate the prototype and secure the quantum core.",
                null,
                "FACTORY EXECUTIONER",
                null
            );

            yield return new WaitForSecondsRealtime(4.0f);

            // Robot Boss jumps / disengages up to catwalks
            StartCoroutine(BossDisengageRoutine());
            yield return new WaitForSecondsRealtime(1.2f);

            // [Shot 8: Return to Gameplay]
            GotGTitleCardUI.GetOrCreate().SetLetterboxOnly(false, 0.6f);
            CyberpunkDialogueHUD.GetOrCreate().CloseDialogue();

            // Settle Fallen Ido into memorial pose on the bridge
            SettleFallenIdoMemorial();

            // Restore Main Camera
            RestoreGameplayCamera();

            // Unlock player movement & engage Mission 1 combat
            UnlockPlayer();

            var mission1 = FindAnyObjectByType<Mission1_StreetTrialManager>();
            if (mission1 != null)
            {
                mission1.BeginBridgeFight(fromPrologueEscort: true);
            }

            isCutsceneRunning = false;
            onComplete?.Invoke();
            Debug.Log("[IdoBridgeExecution] Cutscene finished! Player engaged in combat to avenge Dr. Ido.");
        }

        private IEnumerator BossDisengageRoutine()
        {
            if (bossActor == null) yield break;

            Vector3 startP = bossActor.transform.position;
            Vector3 leapDest = startP + new Vector3(8.0f, 26.0f, 0f);

            float leapDur = 1.4f;
            float elapsed = 0f;

            if (_sfxSource != null && sfxGroundSlam != null)
            {
                _sfxSource.PlayOneShot(sfxGroundSlam, 0.7f);
            }

            while (elapsed < leapDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / leapDur);
                float easeOutT = Mathf.Sin(t * Mathf.PI * 0.5f);
                if (bossActor != null)
                {
                    bossActor.transform.position = Vector3.Lerp(startP, leapDest, easeOutT);
                }
                yield return null;
            }

            if (bossActor != null)
            {
                Destroy(bossActor);
                bossActor = null;
            }
        }

        private void PrepareAlita()
        {
            if (playerObject == null) playerObject = GameObject.FindWithTag("Player");
            if (playerObject == null)
            {
                var loco = FindAnyObjectByType<SyntySwordLocomotionController>();
                if (loco != null) playerObject = loco.gameObject;
            }

            if (playerObject != null)
            {
                var synty = playerObject.GetComponent<SyntySwordLocomotionController>();
                if (synty != null) synty.inputLocked = true;

                var insane = playerObject.GetComponent<InsaneGunSwordController>();
                if (insane != null) insane.inputLocked = true;

                var cc = playerObject.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                playerObject.transform.position = alitaStandoffPos;
                playerObject.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                if (cc != null) cc.enabled = true;
            }
        }

        private void UnlockPlayer()
        {
            if (playerObject != null)
            {
                var synty = playerObject.GetComponent<SyntySwordLocomotionController>();
                if (synty != null) synty.inputLocked = false;

                var insane = playerObject.GetComponent<InsaneGunSwordController>();
                if (insane != null) insane.inputLocked = false;
            }
        }

        private static Transform FindChildRecursive(Transform parent, string childName)
        {
            if (parent == null) return null;
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (string.Equals(child.name, childName, StringComparison.OrdinalIgnoreCase))
                    return child;
                var found = FindChildRecursive(child, childName);
                if (found != null) return found;
            }
            return null;
        }

        private void PrepareDoctorIdo()
        {
            // Acquire doctor actor from AlitaCityPrologueDirector or scene
            if (doctorActor == null)
            {
                var prologue = Alita.AlitaCityPrologueDirector.Instance ?? FindAnyObjectByType<Alita.AlitaCityPrologueDirector>();
                if (prologue != null && prologue.doctorActor != null)
                {
                    doctorActor = prologue.doctorActor;
                }
            }

            if (doctorActor == null)
            {
                doctorActor = GameObject.Find("Doctor_Actor_Runtime") ?? GameObject.Find("Sidekick_Doctor_Ido");
            }

            if (doctorActor == null)
            {
                // Fallback spawn Doctor if not in scene
                GameObject prefab = null;
#if UNITY_EDITOR
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/Doctor/Doctor.prefab")
                      ?? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonCyberCity/Prefabs/Characters/SM_Chr_Rich_Male_01.prefab");
#endif
                doctorActor = new GameObject("Doctor_Actor_Runtime");
                if (prefab != null) Instantiate(prefab, doctorActor.transform);
                else GameObject.CreatePrimitive(PrimitiveType.Capsule).transform.SetParent(doctorActor.transform, false);
            }

            doctorActor.transform.position = idoStandoffPos;
            doctorActor.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            doctorAnimator = doctorActor.GetComponentInChildren<Animator>();
            if (doctorAnimator != null)
            {
                doctorAnimator.applyRootMotion = false;
#if UNITY_EDITOR
                var docCtrl = BuildDoctorCutsceneController();
                if (docCtrl != null) doctorAnimator.runtimeAnimatorController = docCtrl;
#endif
                doctorAnimator.speed = 1f;
                doctorAnimator.Play("Locomotion", 0, 0f);
            }
        }

        private void SpawnRobotBoss(Vector3 spawnPos)
        {
            if (bossActor != null) DestroyImmediate(bossActor);

            GameObject robotPrefab = null;
#if UNITY_EDITOR
            robotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/SidekickCharacters/Characters/ScifiRobots/ScifiRobots_01/ScifiRobots_01.prefab")
                       ?? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/SidekickCharacters/Characters/ScifiRobots/ScifiRobots_08/ScifiRobots_08.prefab");
#endif

            if (robotPrefab != null)
            {
                bossActor = Instantiate(robotPrefab, spawnPos, Quaternion.Euler(0f, -90f, 0f));
            }
            else
            {
                bossActor = new GameObject("Vulcan_Bridge_Boss");
                bossActor.transform.position = spawnPos;
                bossActor.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
            }

            bossActor.name = "Vulcan_Bridge_Boss";
            bossActor.tag = "Enemy";

            // Towering 1.35x scale (Forge-Lord stature)
            bossActor.transform.localScale = Vector3.one * 1.35f;

            bossAnimator = bossActor.GetComponentInChildren<Animator>();
            if (bossAnimator != null)
            {
                bossAnimator.applyRootMotion = false;
#if UNITY_EDITOR
                var bossCtrl = BuildBossCutsceneController();
                if (bossCtrl != null) bossAnimator.runtimeAnimatorController = bossCtrl;
#endif
                bossAnimator.speed = 1f;
                bossAnimator.Play("Locomotion", 0, 0f);
            }

            // Equip Forge Greatsword
            EquipForgeBlade(bossActor);

            // Attach Back Smoke Vents
            AttachExhaustSmokeVents(bossActor);
        }

        private void EquipForgeBlade(GameObject bossGo)
        {
            // Search recursively for Synty's dedicated right-hand weapon prop socket "prop_r"
            Transform weaponSocket = FindChildRecursive(bossGo.transform, "prop_r")
                                  ?? FindChildRecursive(bossGo.transform, "Prop_R")
                                  ?? FindChildRecursive(bossGo.transform, "weapon_r")
                                  ?? FindChildRecursive(bossGo.transform, "Weapon_R");

            bool isDedicatedPropSocket = (weaponSocket != null);

            if (weaponSocket == null)
            {
                var anim = bossGo.GetComponentInChildren<Animator>();
                if (anim != null && anim.isHuman)
                {
                    weaponSocket = anim.GetBoneTransform(HumanBodyBones.RightHand);
                }
            }

            if (weaponSocket == null) weaponSocket = bossGo.transform;

            GameObject bladePrefab = null;
#if UNITY_EDITOR
            bladePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonSciFiWorlds/Prefabs/Weapons/Melee/SM_Wep_Sword_05.prefab")
                       ?? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/AnimationSwordCombat/Samples/Prefabs/Wep_Sword_01.prefab");
#endif

            GameObject bladeGo;
            if (bladePrefab != null)
            {
                bladeGo = Instantiate(bladePrefab, weaponSocket);
            }
            else
            {
                bladeGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bladeGo.transform.SetParent(weaponSocket);
                bladeGo.transform.localScale = new Vector3(0.08f, 1.4f, 0.15f);
                Destroy(bladeGo.GetComponent<Collider>());
            }

            bladeGo.name = "Vulcan_Heavy_Forge_Blade";

            // Strip/disable any physics colliders on the weapon so it never displaces actors
            var cols = bladeGo.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null) cols[i].enabled = false;
            }

            if (isDedicatedPropSocket)
            {
                // When parented to Synty's prop_r bone, zero position and identity rotation aligns the grip naturally inside the fingers!
                bladeGo.transform.localPosition = Vector3.zero;
                bladeGo.transform.localRotation = Quaternion.identity;
                bladeGo.transform.localScale = Vector3.one * 1.25f;
            }
            else
            {
                // Fallback for standard Humanoid RightHand bone
                bladeGo.transform.localPosition = new Vector3(0.11780f, 0.03410f, -0.00920f);
                bladeGo.transform.localRotation = Quaternion.Euler(341.3956f, 99.6629f, 276.9453f);
                bladeGo.transform.localScale = Vector3.one * 1.25f;
            }

            // Glowing Molten Blade Material
            var mr = bladeGo.GetComponentInChildren<MeshRenderer>();
            if (mr != null)
            {
                Material mat = mr.material;
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_BaseColor", new Color(0.12f, 0.08f, 0.05f));
                mat.SetColor("_EmissionColor", new Color(4.5f, 0.95f, 0.08f));
            }

            // Fiery Blade Light
            var lightGo = new GameObject("Blade_GlowLight");
            lightGo.transform.SetParent(bladeGo.transform, false);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.45f, 0.05f);
            l.intensity = 3.5f;
            l.range = 4.0f;
        }

        private void AttachExhaustSmokeVents(GameObject bossGo)
        {
            var anim = bossGo.GetComponentInChildren<Animator>();
            Transform chest = bossGo.transform;
            if (anim != null && anim.isHuman)
            {
                var spine = anim.GetBoneTransform(HumanBodyBones.Chest) ?? anim.GetBoneTransform(HumanBodyBones.Spine);
                if (spine != null) chest = spine;
            }

            CreateSmokeVent("Vent_Smoke_L", chest, new Vector3(-0.18f, 0.15f, -0.15f));
            CreateSmokeVent("Vent_Smoke_R", chest, new Vector3(0.18f, 0.15f, -0.15f));
        }

        private void CreateSmokeVent(string name, Transform parent, Vector3 localPos)
        {
            GameObject smokeGo = new GameObject(name);
            smokeGo.transform.SetParent(parent, false);
            smokeGo.transform.localPosition = localPos;
            smokeGo.transform.localRotation = Quaternion.Euler(45f, 180f, 0f);

            var ps = smokeGo.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startSpeed = 1.8f;
            main.startSize = 0.35f;
            main.startLifetime = 1.2f;
            main.startColor = new Color(0.18f, 0.15f, 0.12f, 0.45f);
            main.maxParticles = 50;
            var emission = ps.emission;
            emission.rateOverTime = 22f;
        }

        private void PlayClipDirect(Animator anim, AnimationClip clip)
        {
            if (anim == null || clip == null) return;

            try
            {
                anim.CrossFadeInFixedTime(clip.name, 0.1f, 0);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[IdoBridgeExecution] PlayClipDirect fallback: {ex.Message}");
            }
        }

        private void SpawnImpactVFX(Vector3 pos)
        {
            if (vfxSwordStabPrefab != null)
            {
                var go = Instantiate(vfxSwordStabPrefab, pos + Vector3.up * 0.1f, Quaternion.identity);
                Destroy(go, 4.0f);
            }
        }

        private void SpawnStabImpactEffects(Vector3 pos)
        {
            // 1. Sword stab particle flash
            if (vfxSwordStabPrefab != null)
            {
                var stabVfx = Instantiate(vfxSwordStabPrefab, pos, Quaternion.LookRotation(Vector3.left));
                Destroy(stabVfx, 3.5f);
            }

            // 2. Blood splat / spray
            if (vfxBloodSplatPrefab != null)
            {
                var bloodVfx = Instantiate(vfxBloodSplatPrefab, pos, Quaternion.identity);
                Destroy(bloodVfx, 4.0f);
            }

            // 3. Cybernetic discharge electricity
            if (vfxElectricityPrefab != null)
            {
                var sparkVfx = Instantiate(vfxElectricityPrefab, pos, Quaternion.identity);
                Destroy(sparkVfx, 3.5f);
            }

            // 4. Point light flash
            GameObject flashGo = new GameObject("Stab_LightFlash");
            flashGo.transform.position = pos;
            var l = flashGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1.0f, 0.15f, 0.05f);
            l.intensity = 8.0f;
            l.range = 6.0f;
            Destroy(flashGo, 0.35f);
        }

        private void SettleFallenIdoMemorial()
        {
            if (doctorActor != null)
            {
                // Ensure doctor is posed dead on ground and permanently frozen in collapsed prone pose
                doctorActor.name = "Fallen_Doctor_Ido_Memorial";
                var colliders = doctorActor.GetComponentsInChildren<Collider>();
                foreach (var c in colliders) c.enabled = false;

                if (doctorAnimator != null)
                {
                    doctorAnimator.speed = 0f; // Permanently locks the final frame of the stabbing death collapse!
                }

                // Add small cyber smoke plume rising from chest
                var chestBone = doctorAnimator != null && doctorAnimator.isHuman
                    ? doctorAnimator.GetBoneTransform(HumanBodyBones.Chest)
                    : doctorActor.transform;

                if (chestBone != null)
                {
                    GameObject smokePlume = new GameObject("Ido_Chest_Smoke");
                    smokePlume.transform.SetParent(chestBone, false);
                    smokePlume.transform.localPosition = Vector3.zero;
                    var ps = smokePlume.AddComponent<ParticleSystem>();
                    var m = ps.main;
                    m.startLifetime = 1.8f;
                    m.startSpeed = 0.45f;
                    m.startSize = 0.25f;
                    m.startColor = new Color(0.2f, 0.2f, 0.25f, 0.35f);
                    var em = ps.emission;
                    em.rateOverTime = 8f;
                }

                _fallenIdoMemorialObj = doctorActor;
                hasFallenIdoSpawned = true;
            }
        }

        private void Update()
        {
            // Interactive Homage Prompt when player approaches Dr. Ido's fallen body
            if (hasFallenIdoSpawned && _fallenIdoMemorialObj != null && playerObject != null && !isCutsceneRunning)
            {
                float dist = Vector3.Distance(playerObject.transform.position, _fallenIdoMemorialObj.transform.position);
                if (dist <= 3.2f && !_isKneelingBesideIdo)
                {
                    CyberpunkInteractionPromptHUD.ShowPrompt("E", "KNEEL BESIDE IDO // VOW VENGEANCE");

                    if (Input.GetKeyDown(KeyCode.E))
                    {
                        StartCoroutine(KneelBesideIdoRoutine());
                    }
                }
            }
        }

        private IEnumerator KneelBesideIdoRoutine()
        {
            _isKneelingBesideIdo = true;
            Debug.Log("[IdoBridgeExecution] Alita kneels beside Dr. Ido.");

            CyberpunkDialogueHUD.GetOrCreate().ShowDialogue(
                "ALITA",
                "Rest now, father. Every corporate tower will burn for what they did to you. I swear it on my heart.",
                null,
                "MARTIAN CYBORG",
                () =>
                {
                    _isKneelingBesideIdo = false;
                }
            );

            yield return new WaitForSeconds(4.5f);
            _isKneelingBesideIdo = false;
        }

        private void SetCameraTransform(Vector3 pos, Quaternion rot, float fov)
        {
            if (_cameraTrackRoutine != null) StopCoroutine(_cameraTrackRoutine);
            _currentCamBasePos = pos;
            if (_cutsceneCam != null)
            {
                _cutsceneCam.transform.position = pos + _shakeOffset;
                _cutsceneCam.transform.rotation = rot;
                _cutsceneCam.fieldOfView = fov;
            }
        }

        private void StartCameraTransition(Vector3 targetPos, Quaternion targetRot, float targetFov, float duration)
        {
            if (_cameraTrackRoutine != null) StopCoroutine(_cameraTrackRoutine);
            _cameraTrackRoutine = StartCoroutine(CameraTransitionRoutine(targetPos, targetRot, targetFov, duration));
        }

        private IEnumerator CameraTransitionRoutine(Vector3 endPos, Quaternion endRot, float endFov, float duration)
        {
            if (_cutsceneCam == null) yield break;

            Vector3 startPos = _currentCamBasePos != Vector3.zero ? _currentCamBasePos : _cutsceneCam.transform.position;
            Quaternion startRot = _cutsceneCam.transform.rotation;
            float startFov = _cutsceneCam.fieldOfView;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                _currentCamBasePos = Vector3.Lerp(startPos, endPos, t);
                _cutsceneCam.transform.position = _currentCamBasePos + _shakeOffset;
                _cutsceneCam.transform.rotation = Quaternion.Slerp(startRot, endRot, t);
                _cutsceneCam.fieldOfView = Mathf.Lerp(startFov, endFov, t);
                yield return null;
            }

            _currentCamBasePos = endPos;
            _cutsceneCam.transform.position = endPos + _shakeOffset;
            _cutsceneCam.transform.rotation = endRot;
            _cutsceneCam.fieldOfView = endFov;
        }

        private void TriggerScreenShake(float intensity, float duration)
        {
            if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);
            _shakeRoutine = StartCoroutine(DoScreenShake(intensity, duration));
        }

        private IEnumerator DoScreenShake(float intensity, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float currentPower = intensity * Mathf.Pow(1f - progress, 2f);
                _shakeOffset = new Vector3(
                    (Mathf.PerlinNoise(Time.unscaledTime * 45f, 0f) - 0.5f) * 2f * currentPower,
                    (Mathf.PerlinNoise(0f, Time.unscaledTime * 45f) - 0.5f) * 2f * currentPower,
                    (Mathf.PerlinNoise(Time.unscaledTime * 30f, Time.unscaledTime * 30f) - 0.5f) * currentPower
                );
                yield return null;
            }
            _shakeOffset = Vector3.zero;
        }

        private void RestoreGameplayCamera()
        {
            if (_cutsceneCam != null && _cachedOrbitCam != null)
            {
                _cachedOrbitCam.enabled = true;
                _cachedOrbitCam.SnapToTarget();
            }
        }
    }
}
