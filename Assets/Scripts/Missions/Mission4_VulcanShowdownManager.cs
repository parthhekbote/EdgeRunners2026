using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using InsaneGun.Combat;
using CyberCombat.Audio;
using Cyberpunk.Audio;
using Cyberpunk.Inventory;
using Cyberpunk.World;
using SyntyCombined;
using SyntyCombined.Cutscene;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Cyberpunk.Missions
{
    /// <summary>
    /// Master Encounter Director for Mission 4: Vulcan Showdown (Forge-Lord Boss Duel).
    /// Features:
    /// - AAA Cinematic Multi-Shot Cutscene Hijack after Round 3:
    ///   [Shot 1: 0.0s - 1.2s] Arena Wide Establishing Shot (all floodlights cut, drums stop, ambient drone)
    ///   [Shot 2: 1.2s - 2.8s] Jumbotron Glitch Dutch Tilt (chromatic glitch audio, SIGNAL OVERRIDE -> VULCAN FOUNDRY)
    ///   [Shot 3: 2.8s - 4.4s] Skyward Thunder Drop & GroundSlam (thunder boom, high-speed fireball drop, shockwave impact, slow-mo hitstop)
    ///   [Shot 4: 4.4s - 7.0s] Low-Angle Villain Reveal & GotG Title Card Slam (VULCAN / FORGE-LORD OF SECTOR 04, weapon ribbon ignite, steam vents)
    ///   [Shot 5: 7.0s - 10.5s] Cinematic Villain Orbit Monologue (Voice 01 plays with synchronized subtitle via BlackwallStoryDialogueHUD)
    ///   [Shot 6: 10.5s - 12.0s] Reverse Over-the-Shoulder Standoff (Alita vs Vulcan standoff across the ring)
    ///   [Shot 7: 12.0s] Seamless Camera Blend to Gameplay & War Drums start!
    /// - Multi-stage boss escalation:
    ///   Phase 1 (100%->66% HP): The Duelist. Heavy Forge Greatsword with immense poise.
    ///   Phase 2 (66%->33% HP): Meltdown Surge. Red alarm strobes, voice line 02, warning flash.
    ///   Phase 3 (33%->0% HP): Judgement of the Forge. Climax music shift, floor fire embers, voice line 03.
    ///   Defeat (0% HP): Kneeling boss, voice line 04, interactive [E] FINISH prompt & championship purse.
    /// </summary>
    public class Mission4_VulcanShowdownManager : MonoBehaviour
    {
        public static Mission4_VulcanShowdownManager Instance { get; private set; }

        public const string PREF_VULCAN_DEFEATED = "vulcan_boss_defeated";
        private static bool _isVulcanDefeatedInSession = false;

        /// <summary>
        /// True if Vulcan has been defeated in the current play session.
        /// Once defeated in this session, the Pit Arena operates in standard championship mode without repeated Vulcan ambushes.
        /// Each new game session or play mode launch starts fresh (false) so the epic cutscene and boss duel trigger after Round 3!
        /// </summary>
        public static bool IsVulcanDefeated
        {
            get => _isVulcanDefeatedInSession;
            set
            {
                _isVulcanDefeatedInSession = value;
                if (!value)
                {
                    PlayerPrefs.DeleteKey(PREF_VULCAN_DEFEATED);
                    PlayerPrefs.Save();
                }
            }
        }

        [ContextMenu("Reset Vulcan Progress")]
        public static void ResetVulcanProgress()
        {
            _isVulcanDefeatedInSession = false;
            PlayerPrefs.DeleteKey(PREF_VULCAN_DEFEATED);
            PlayerPrefs.Save();
            Debug.Log("[Mission4] Vulcan progress reset. Vulcan will spawn naturally upon clearing Round 3.");
        }

        [ContextMenu("Force Trigger Vulcan Showdown")]
        public void DebugForceVulcanShowdown()
        {
            TriggerVulcanArrival(force: true);
        }

        public enum ShowdownPhase
        {
            Inactive,
            CinematicHijack,
            Phase1_TheDuelist,
            Phase2_Meltdown,
            Phase3_Judgement,
            KneelingDefeat,
            VictoryCompleted
        }

        [Header("State")]
        public ShowdownPhase currentPhase = ShowdownPhase.Inactive;
        public float vulcanMaxHealth = 850f;
        public float vulcanCurrentHealth = 850f;

        [Header("Combatants")]
        public InsaneGunSwordController player;
        public InsaneGunSwordController vulcanBoss;

        [Header("Positions")]
        public Vector3 ringCenterPos = new Vector3(0f, 0.85f, 0f);
        public Vector3 alitaStandoffPos = new Vector3(0f, 0.85f, -4.8f);
        public Vector3 dropSpawnPos = new Vector3(0f, 22f, 0f);

        [Header("Voice Clips (Assets/Audio/Voice/Vulcan)")]
        public AudioClip voiceIntro;
        public AudioClip voicePhase2;
        public AudioClip voicePhase3;
        public AudioClip voiceDeath;

        [Header("Music Tracks (Assets/Audio/vulcan_battle)")]
        public AudioClip musicOminousIntro;    // viramiller-ominous-284554.mp3
        public AudioClip musicWarDrums;        // stereo_color-war-drum-484498.mp3
        public AudioClip musicMeltdownClimax;  // echoes_of_lumen-ominous-music-595730.mp3

        [Header("SFX & Audio Sources")]
        public AudioClip sfxThunder;
        public AudioClip sfxGlitch;
        public AudioClip sfxGroundSlam;
        public AudioSource musicSource;
        public AudioSource voiceSource;
        public AudioSource sfxSource;

        [Header("Jumbotron In-World Display")]
        public string currentJumbotronText = "";
        public Color currentJumbotronColor = new Color(0f, 0.95f, 1f, 1f);
        public bool isJumbotronGlitching = false;

        // Cinematic Camera State
        private Camera _cutsceneCam;
        private SmoothOrbitCamera _cachedOrbitCam;
        private Coroutine _cameraTrackRoutine;
        private Vector3 _currentCamBasePos;
        private Vector3 _shakeOffset = Vector3.zero;
        private Coroutine _shakeRoutine;

        // Visual References
        private readonly List<Light> _cachedArenaLights = new List<Light>();
        private readonly List<float> _originalLightIntensities = new List<float>();
        private readonly List<Color> _originalLightColors = new List<Color>();
        private Light _spotlightVulcan;
        private Coroutine _strobeLightsRoutine;
        private Coroutine _hijackTimelineRoutine;
        private Coroutine _musicDuckRoutine;
        private Material _vulcanArmorMat;
        private Material _vulcanBladeMat;
        private Light _vulcanBladeLight;
        private TrailRenderer _vulcanBladeTrail;
        private ParticleSystem _exhaustSmokeL;
        private ParticleSystem _exhaustSmokeR;

        // 3D In-World Text components for Jumbotron screens
        private readonly List<TextMesh> _jumbotronTextMeshes = new List<TextMesh>();

        // GUI Styles
        private GUIStyle _jumbotronTickerStyle;
        private GUIStyle _finishPromptStyle;
        private GUIStyle _subtitleStyle;
        private GUIStyle _speakerStyle;
        private Texture2D _jumbotronBgTex;

        // Subtitle State
        private string _activeSubtitle = "";
        private string _activeSpeaker = "";
        private Color _speakerColor = new Color(1.0f, 0.45f, 0.05f, 1.0f);
        private float _subtitleTimer = 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrapInArena()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;

            var activeScene = SceneManager.GetActiveScene();
            if (IsArenaScene(activeScene.name))
            {
                EnsureInArena();
            }
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (IsArenaScene(scene.name))
            {
                EnsureInArena();
            }
        }

        private static bool IsArenaScene(string name)
        {
            return name.IndexOf("Combat_Arena", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Indoor_Arena", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static Mission4_VulcanShowdownManager EnsureInArena()
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<Mission4_VulcanShowdownManager>();
            if (existing != null)
            {
                Instance = existing;
                return Instance;
            }

            GameObject go = new GameObject("[Mission4_Vulcan_Showdown_Manager]");
            Instance = go.AddComponent<Mission4_VulcanShowdownManager>();
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
            LoadAudioAssets();
            CacheArenaLighting();
            SetupJumbotronWorldScreens();
            BuildGUIStyles();
        }

        private void Start()
        {
            if (_cachedArenaLights.Count == 0) CacheArenaLighting();
            if (_jumbotronTextMeshes.Count == 0) SetupJumbotronWorldScreens();

            if (IsVulcanDefeated)
            {
                RestoreArenaLighting(forgeTint: false);
                if (musicSource != null && musicSource.isPlaying) musicSource.Stop();
                if (voiceSource != null && voiceSource.isPlaying) voiceSource.Stop();
                if (sfxSource != null && sfxSource.isPlaying) sfxSource.Stop();
            }
        }

        private void SetupAudioSources()
        {
            if (musicSource == null)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
                musicSource.spatialBlend = 0f;
                musicSource.loop = true;
                musicSource.playOnAwake = false;
                musicSource.volume = 0.85f;
            }

            if (voiceSource == null)
            {
                voiceSource = gameObject.AddComponent<AudioSource>();
                voiceSource.spatialBlend = 0f;
                voiceSource.loop = false;
                voiceSource.playOnAwake = false;
                voiceSource.priority = 0; // Highest priority for crystal-clear dialogue
                voiceSource.volume = 1.0f;
            }

            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
                sfxSource.spatialBlend = 0f;
                sfxSource.loop = false;
                sfxSource.playOnAwake = false;
                sfxSource.volume = 1.0f;
            }
        }

        public void LoadAudioAssets()
        {
#if UNITY_EDITOR
            if (voiceIntro == null) voiceIntro = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Voice/Vulcan/vulcan_01_intro.wav");
            if (voicePhase2 == null) voicePhase2 = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Voice/Vulcan/vulcan_02_phase2.wav");
            if (voicePhase3 == null) voicePhase3 = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Voice/Vulcan/vulcan_03_phase3.wav");
            if (voiceDeath == null) voiceDeath = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Voice/Vulcan/vulcan_04_death.wav");

            if (musicOminousIntro == null) musicOminousIntro = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/vulcan_battle/viramiller-ominous-284554.mp3");
            var spiderVerseClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/Across_The_Spider_Verse_Intro.mp3");
            if (spiderVerseClip != null)
            {
                musicWarDrums = spiderVerseClip;
            }
            else if (musicWarDrums == null)
            {
                musicWarDrums = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/vulcan_battle/stereo_color-war-drum-484498.mp3");
            }
            if (musicMeltdownClimax == null) musicMeltdownClimax = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/vulcan_battle/echoes_of_lumen-ominous-music-595730.mp3");

            if (sfxThunder == null) sfxThunder = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Weather/dragon-studio-dry-thunder-364468.mp3");
            if (sfxGlitch == null) sfxGlitch = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Weather/dragon-studio-dry-thunder-364468.mp3");
            if (sfxGroundSlam == null) sfxGroundSlam = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Combat/Sword_Slash_Heavy_Cleave_01.wav");
#endif
        }

        private void CacheArenaLighting()
        {
            _cachedArenaLights.Clear();
            _originalLightIntensities.Clear();
            _originalLightColors.Clear();

            var allLights = FindObjectsByType<Light>();
            foreach (var l in allLights)
            {
                if (l != null && l.enabled)
                {
                    _cachedArenaLights.Add(l);
                    _originalLightIntensities.Add(l.intensity);
                    _originalLightColors.Add(l.color);
                }
            }
        }

        private void SetupJumbotronWorldScreens()
        {
            _jumbotronTextMeshes.Clear();

            string[] screenNames = new string[] { "Screen_North", "Screen_South", "Screen_East", "Screen_West" };

            foreach (var sName in screenNames)
            {
                var sGo = GameObject.Find(sName);
                if (sGo == null) continue;

                var existingTm = sGo.GetComponentInChildren<TextMesh>();
                if (existingTm != null)
                {
                    _jumbotronTextMeshes.Add(existingTm);
                    continue;
                }

                GameObject textGo = new GameObject($"{sName}_Label3D");
                textGo.transform.SetParent(sGo.transform, false);

                textGo.transform.localPosition = new Vector3(0f, 0f, -0.6f);
                textGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

                var tm = textGo.AddComponent<TextMesh>();
                tm.text = "// VULCAN PIT ARENA //";
                tm.fontSize = 42;
                tm.characterSize = 0.12f;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;
                tm.color = new Color(0f, 0.95f, 1f, 1f);

                _jumbotronTextMeshes.Add(tm);
            }
        }

        public void SetJumbotronDisplay(string text, Color color, bool glitchEffect = false)
        {
            currentJumbotronText = text;
            currentJumbotronColor = color;
            isJumbotronGlitching = glitchEffect;

            foreach (var tm in _jumbotronTextMeshes)
            {
                if (tm != null)
                {
                    tm.text = text;
                    tm.color = color;
                }
            }
        }

        /// <summary>
        /// Initiates the full cinematic AAA Vulcan Showdown takeover!
        /// </summary>
        public void TriggerVulcanArrival(bool force = false)
        {
            if (!force && IsVulcanDefeated)
            {
                Debug.Log("[Mission4] Vulcan is already defeated in this session. Pit arena runs in standard championship mode.");
                return;
            }

            if (currentPhase != ShowdownPhase.Inactive)
            {
                Debug.LogWarning($"[Mission4] TriggerVulcanArrival called but currentPhase is {currentPhase}.");
                return;
            }

            if (_cachedArenaLights.Count == 0) CacheArenaLighting();
            if (_jumbotronTextMeshes.Count == 0) SetupJumbotronWorldScreens();

            if (_hijackTimelineRoutine != null) StopCoroutine(_hijackTimelineRoutine);
            _hijackTimelineRoutine = StartCoroutine(VulcanHijackTimelineRoutine());
        }

        private static Alita.UI.CyberpunkDialogueHUD.DialogueLineData CutsceneLine(string speaker, string title, string text)
        {
            float auto = Mathf.Clamp(text.Length * 0.055f + 1.6f, 3.0f, 9.0f);
            return new Alita.UI.CyberpunkDialogueHUD.DialogueLineData(speaker, text, null, title, auto);
        }

        /// <summary>Runs a dialogue conversation and waits for it to finish (with a safety timeout).</summary>
        private IEnumerator PlayCutsceneConversation(List<Alita.UI.CyberpunkDialogueHUD.DialogueLineData> lines)
        {
            bool done = false;
            float budget = 4f;
            foreach (var l in lines) budget += l.autoAdvanceTime + 2f;
            Alita.UI.CyberpunkDialogueHUD.GetOrCreate().StartConversation(lines, () => done = true);
            float t = 0f;
            while (!done && t < budget)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            yield return new WaitForSeconds(0.3f);
        }

        private IEnumerator VulcanHijackTimelineRoutine()
        {
            currentPhase = ShowdownPhase.CinematicHijack;

            // 1. Prepare Camera
            _cutsceneCam = Camera.main ?? FindAnyObjectByType<Camera>();
            if (_cutsceneCam != null)
            {
                _cachedOrbitCam = _cutsceneCam.GetComponent<SmoothOrbitCamera>();
                if (_cachedOrbitCam != null) _cachedOrbitCam.enabled = false;
            }

            // 2. Prepare Alita (Never dead or ragdolled during cutscene!)
            PrepareAlitaForCutscene();

            // 3. Slide in Cinematic 2.39:1 Widescreen Letterbox Bars
            GotGTitleCardUI.GetOrCreate().SetLetterboxOnly(true, 0.45f);

            // [Shot 1: 0.0s - 2.8s] Arena Wide Establishing Shot & Sudden Blackout
            Vector3 shot1Pos = new Vector3(-10.5f, 3.8f, -14.2f);
            Quaternion shot1Rot = Quaternion.LookRotation((new Vector3(0f, 3.5f, 0f) - shot1Pos).normalized);
            SetCameraTransform(shot1Pos, shot1Rot, 64f);

            SetJumbotronDisplay("// ROUND 3 CLEARED // CHALLENGER RECOGNIZED", new Color(0f, 0.95f, 1f, 1f));
            yield return new WaitForSeconds(0.4f);

            // Blackout: lights die, drums stop, ominous ambient drone
            PitArenaExitTrigger.IsExitLocked = true;
            PitCrowdAudioManager.Instance?.CutDrumsAndAmbience();

            if (musicSource != null && musicOminousIntro != null)
            {
                musicSource.clip = musicOminousIntro;
                musicSource.loop = true;
                musicSource.volume = 0.75f; // Amplified menacing ominous music throughout boss takeover
                musicSource.Play();
            }

            // Dim arena floodlights to near pitch darkness
            for (int i = 0; i < _cachedArenaLights.Count; i++)
            {
                if (_cachedArenaLights[i] != null)
                {
                    _cachedArenaLights[i].intensity = _originalLightIntensities[i] * 0.04f;
                    _cachedArenaLights[i].color = new Color(0.06f, 0.10f, 0.20f);
                }
            }

            // Let the tension linger in the dark arena
            yield return new WaitForSeconds(2.4f);

            // [Shot 2: 2.8s - 6.2s] Jumbotron Glitch Dutch Tilt Shot
            if (sfxSource != null && sfxGlitch != null)
            {
                sfxSource.PlayOneShot(sfxGlitch, 0.95f);
            }

            Vector3 shot2Pos = new Vector3(3.2f, 4.8f, -6.2f);
            Quaternion shot2Rot = Quaternion.Euler(-42f, 160f, 14f);
            StartCameraTransition(shot2Pos, shot2Rot, 52f, 1.8f);

            SetJumbotronDisplay("// EMERGENCY OVERRIDE //", new Color(1f, 0.15f, 0.2f, 1f), glitchEffect: true);
            yield return new WaitForSeconds(1.6f);
            SetJumbotronDisplay("VULCAN FOUNDRY // PRIORITY ALPHA", new Color(1.0f, 0.45f, 0.05f, 1f));
            yield return new WaitForSeconds(1.8f);

            // [Shot 3: 6.2s - 10.0s] Skyward Thunder Drop & GroundSlam Crater Impact
            Vector3 shot3Pos = new Vector3(0.35f, 0.65f, -4.5f);
            Quaternion shot3Rot = Quaternion.Euler(-68f, 0f, 0f); // Looking high up at industrial rafters
            SetCameraTransform(shot3Pos, shot3Rot, 68f);

            if (sfxSource != null && sfxThunder != null)
            {
                sfxSource.PlayOneShot(sfxThunder, 1.0f);
            }
            TriggerScreenShake(0.60f, 0.50f);

            // Spawn Vulcan high above arena rafters
            SpawnVulcanBoss(dropSpawnPos);
            var bossAnim = vulcanBoss != null ? vulcanBoss.GetComponent<Animator>() : null;
            if (bossAnim != null)
            {
                bossAnim.Play("Attack_03", 0, 0.15f);
            }

            // Plunge Vulcan down to ground over 0.65s while camera tracks down
            float dropDuration = 0.65f;
            float dropElapsed = 0f;
            Vector3 startPos = dropSpawnPos;
            Vector3 endPos = ringCenterPos;

            while (dropElapsed < dropDuration)
            {
                dropElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(dropElapsed / dropDuration);
                float easeInT = t * t; // High-velocity gravity acceleration
                if (vulcanBoss != null)
                {
                    vulcanBoss.transform.position = Vector3.Lerp(startPos, endPos, easeInT);
                }

                // Tilt camera down tracking Vulcan
                float camPitch = Mathf.Lerp(-68f, -14f, easeInT);
                if (_cutsceneCam != null)
                {
                    _cutsceneCam.transform.rotation = Quaternion.Euler(camPitch, 0f, 0f);
                }
                yield return null;
            }

            // Firmly anchor Vulcan to the floor surface (no hovering!)
            if (vulcanBoss != null) vulcanBoss.transform.position = ringCenterPos;

            // GroundSlam impact shockwave & dramatic time-dilation freeze
            TriggerGroundSlamImpact();
            TriggerScreenShake(1.8f, 0.90f);
            SpawnCraterImpactLighting();

            // Grounded slam impact pose: both feet planted firmly on platform!
            if (bossAnim != null)
            {
                bossAnim.Play("Attack_03", 0, 0.42f);
            }

            // Visceral slow-motion impact freeze (0.16x time scale for immense weight)
            Time.timeScale = 0.16f;
            yield return new WaitForSecondsRealtime(0.55f);
            Time.timeScale = 1.0f;

            // [Shot 4: 10.0s - 15.0s] Low-Angle Villain Reveal & GotG Title Card Slam
            Vector3 shot4Pos = new Vector3(1.85f, 1.25f, -3.8f);
            Quaternion shot4Rot = Quaternion.LookRotation((ringCenterPos + Vector3.up * 1.30f) - shot4Pos);
            StartCameraTransition(shot4Pos, shot4Rot, 48f, 2.8f);

            // Vulcan stands up tall into menacing warlord stance firmly on the ground
            if (bossAnim != null)
            {
                bossAnim.CrossFadeInFixedTime("Locomotion", 0.9f);
            }

            // Swell ominous music during title card reveal
            DuckMusicVolume(0.85f, 1.0f);

            // Title card slam splash
            GotGTitleCardUI.GetOrCreate().ShowTitleCard(
                title: "VULCAN",
                subtitle: "FORGE-LORD OF SECTOR 04",
                details: "CHALLENGER: ALITA  vs  OWNER: VULCAN  •  STATUS: NO CASH OUT",
                duration: 6.5f
            );

            // Ignite Forge Greatsword & smoke exhaust vents
            IgniteVulcanVisuals();

            yield return new WaitForSeconds(5.0f);

            // [Shot 5: 15.0s - 23.5s] 360-Degree Villain Monologue Orbit & Amplified Ominous Music
            Vector3 shot5EndPos = new Vector3(-2.4f, 1.35f, -3.5f);
            Quaternion shot5EndRot = Quaternion.LookRotation((ringCenterPos + Vector3.up * 1.30f) - shot5EndPos);
            StartCameraTransition(shot5EndPos, shot5EndRot, 46f, 8.2f);

            // Vulcan's full monologue plays as a line-by-line conversation (voice audio runs underneath).
            Cyberpunk.Audio.PitCrowdAudioManager.Instance?.CutDrumsAndAmbience();
            if (voiceIntro != null && voiceSource != null)
            {
                voiceSource.clip = voiceIntro;
                voiceSource.priority = 0;
                voiceSource.volume = 1.0f;
                voiceSource.Play();
            }
            yield return PlayCutsceneConversation(new List<Alita.UI.CyberpunkDialogueHUD.DialogueLineData>
            {
                CutsceneLine("FORGE-LORD VULCAN", "ARENA MASTER", "You broke my toys on the bridge. Stole my pass. Shamed my Pit before five hundred witnesses."),
                CutsceneLine("FORGE-LORD VULCAN", "ARENA MASTER", "I sent troops. You sent back scrap. So now... the Forge comes himself."),
                CutsceneLine("FORGE-LORD VULCAN", "ARENA MASTER", "Alita... I will unmake you."),
            });

            // [Shot 5B: 23.5s - 27.2s] Alita's Low-Angle Defiance Profile Shot (3.7s)
            Vector3 shot5bPos = alitaStandoffPos + new Vector3(-0.95f, 0.85f, 1.45f);
            Quaternion shot5bRot = Quaternion.LookRotation((alitaStandoffPos + Vector3.up * 1.25f) - shot5bPos);
            SetCameraTransform(shot5bPos, shot5bRot, 42f);

            // Alita locks into battle stance with glowing Damascus blade
            var pAnim = player != null ? player.GetComponent<Animator>() : null;
            if (pAnim != null)
            {
                pAnim.SetBool("IsSwordDrawn", true);
                pAnim.Play("Locomotion", 0, 0f);
            }
            if (sfxSource != null && sfxGlitch != null)
            {
                sfxSource.PlayOneShot(sfxGlitch, 0.45f);
            }

            yield return new WaitForSeconds(3.7f);

            // [Shot 5C: 27.2s - 31.7s] Vulcan's Blade-Scrape Floor Drag & Intimidation Step (4.5s)
            Vector3 shot5cPos = ringCenterPos + new Vector3(1.25f, 0.40f, -1.85f);
            Quaternion shot5cRot = Quaternion.Euler(-18f, -40f, 12f);
            StartCameraTransition(shot5cPos, shot5cRot, 38f, 1.8f);

            // Vulcan steps forward heavily and drags greatsword across arena floor
            if (bossAnim != null)
            {
                bossAnim.CrossFadeInFixedTime("Attack_01", 0.4f);
            }
            if (sfxSource != null && sfxGroundSlam != null)
            {
                sfxSource.PlayOneShot(sfxGroundSlam, 0.75f);
            }
            TriggerScreenShake(0.55f, 0.7f);
            SpawnCraterImpactLighting();

            SetJumbotronDisplay("// COMBAT LOCKDOWN // SURVIVAL RATE: 0.0% //", new Color(1.0f, 0.15f, 0.15f, 1f), glitchEffect: true);

            yield return new WaitForSeconds(4.5f);

            // Return Vulcan to imposing ready stance
            if (bossAnim != null)
            {
                bossAnim.CrossFadeInFixedTime("Locomotion", 0.6f);
            }

            // [Shot 5D: 31.7s - 35.2s] High Catwalk Sparks & Emergency Deathmatch Sirens (3.5s)
            Vector3 shot5dPos = new Vector3(-6.5f, 8.5f, -8.0f);
            Quaternion shot5dRot = Quaternion.LookRotation((ringCenterPos + Vector3.up * 0.8f) - shot5dPos);
            SetCameraTransform(shot5dPos, shot5dRot, 56f);

            if (sfxSource != null && sfxThunder != null)
            {
                sfxSource.PlayOneShot(sfxThunder, 0.85f);
            }
            TriggerScreenShake(0.70f, 0.8f);

            // Flash arena lighting into molten forge blood-orange emergency tint
            for (int i = 0; i < _cachedArenaLights.Count; i++)
            {
                if (_cachedArenaLights[i] != null)
                {
                    _cachedArenaLights[i].color = new Color(1.0f, 0.28f, 0.05f);
                    _cachedArenaLights[i].intensity = _originalLightIntensities[i] * 0.75f;
                }
            }

            SetJumbotronDisplay("ALITA vs VULCAN // DEATHMATCH PROTOCOL ACTIVE", new Color(1.0f, 0.75f, 0.0f, 1f));

            yield return new WaitForSeconds(3.5f);

            // Maintain amplified ominous BGM through standoff
            DuckMusicVolume(0.75f, 0.8f);

            // [Shot 6: 35.2s - 38.7s] Reverse Over-the-Shoulder Duelist Standoff (3.5s)
            SetJumbotronDisplay("CHALLENGER: ALITA vs OWNER: VULCAN // DEATHMATCH", new Color(1.0f, 0.85f, 0.1f, 1f));

            Vector3 shot6Pos = alitaStandoffPos + new Vector3(0.55f, 1.45f, -1.15f);
            Quaternion shot6Rot = Quaternion.LookRotation((ringCenterPos + Vector3.up * 1.30f) - shot6Pos);
            StartCameraTransition(shot6Pos, shot6Rot, 44f, 2.2f);

            // Alita answers Vulcan; the duel begins only after her last line.
            yield return PlayCutsceneConversation(new List<Alita.UI.CyberpunkDialogueHUD.DialogueLineData>
            {
                CutsceneLine("ALITA", "MARTIAN CYBORG", "Your toys came for me first. I only fought back. The pass was my way to you, nothing more."),
                CutsceneLine("ALITA", "MARTIAN CYBORG", "I don't remember who I was before, Vulcan. But I know I won't be anyone's trophy."),
                CutsceneLine("ALITA", "MARTIAN CYBORG", "No more speeches. Let's finish this."),
            });

            // [Shot 7: 38.7s] Fight Start & Seamless Camera Handoff
            currentPhase = ShowdownPhase.Phase1_TheDuelist;

            GotGTitleCardUI.GetOrCreate().SetLetterboxOnly(false, 0.8f);

            CyberpunkHUDController.Instance?.ShowToast("// M4 // VULCAN SHOWDOWN - PHASE 1: THE DUELIST", 4.0f);
            CyberpunkHUDController.Instance?.SetQuestObjective("MISSION 4 // FORGE-LORD SHOWDOWN", "DEFEAT VULCAN IN THE PIT");
            CyberpunkHUDController.Instance?.SetBossActive("VULCAN // FORGE-LORD");
            CyberpunkHUDController.Instance?.SetBossHealth(1.0f, "VULCAN // FORGE-LORD (PHASE 1)");

            // Shift music to heavy war drums at full battle volume!
            if (musicSource != null && musicWarDrums != null)
            {
                musicSource.clip = musicWarDrums;
                musicSource.loop = true;
                musicSource.volume = 0.90f;
                musicSource.Play();
            }

            // Restore arena spotlight with dramatic center downlight
            RestoreArenaLighting(forgeTint: true);

            // Re-enable SmoothOrbitCamera seamlessly
            if (_cachedOrbitCam != null)
            {
                _cachedOrbitCam.enabled = true;
                _cachedOrbitCam.target = player != null ? player.transform : null;
                _cachedOrbitCam.SyncToCurrentTransform();
            }

            // Unlock Alita & Vulcan
            if (player != null)
            {
                player.inputLocked = false;
            }

            if (vulcanBoss != null)
            {
                var cc = vulcanBoss.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = true;
                vulcanBoss.inputLocked = false;
                vulcanBoss.controlMode = InsaneGunSwordController.ControlMode.AICirclingEnemy;
                if (player != null) vulcanBoss.targetEnemy = player.transform;
            }

            Debug.Log("<color=#FF6600><b>[Mission 4]</b></color> Vulcan Showdown Phase 1 Duelist engaged!");
        }

        private void PrepareAlitaForCutscene()
        {
            if (player == null)
            {
                var p = GameObject.FindWithTag("Player");
                if (p != null) player = p.GetComponent<InsaneGunSwordController>();
            }

            // Ensure Mission 3 state does not display death modal or interfere during Vulcan Showdown
            var pitMgr = FindAnyObjectByType<Cyberpunk.Missions.Mission3_PitFightManager>();
            if (pitMgr != null)
            {
                pitMgr.currentState = Cyberpunk.Missions.Mission3_PitFightManager.PitState.VulcanShowdown;
            }

            var hud = FindAnyObjectByType<SyntyCombined.CyberpunkHUDController>();
            if (hud != null) hud.HideDeathOverlay();

            // Guarantee Blackwall AI is completely decoupled and never interferes with Mission 4
            var blackwallHud = FindAnyObjectByType<SyntyCombined.BlackwallStoryDialogueHUD>();
            if (blackwallHud != null)
            {
                blackwallHud.isDialogueActive = false;
                blackwallHud.gameObject.SetActive(false);
                Destroy(blackwallHud.gameObject);
            }

            if (player != null)
            {
                // Guarantee Alita is standing tall and fully healed
                player.transform.position = alitaStandoffPos;
                player.transform.rotation = Quaternion.Euler(0f, 0f, 0f); // Facing Vulcan at center
                player.Revive(alitaStandoffPos);
                player.maxHealth = 400f;
                player.currentHealth = 400f;
                player.inputLocked = true;

                var pAnim = player.GetComponent<Animator>();
                if (pAnim != null)
                {
                    pAnim.applyRootMotion = false;
                    pAnim.SetBool("IsSwordDrawn", true);
                    pAnim.SetFloat("MoveSpeed", 0f);
                    pAnim.Play("Locomotion", 0, 0f);
                }

                // Ensure Damascus Blade is visually drawn and emissive
                var renderers = player.GetComponentsInChildren<Renderer>(true);
                foreach (var r in renderers)
                {
                    if (r.gameObject.name.Contains("Sword") || r.gameObject.name.Contains("Blade"))
                    {
                        r.gameObject.SetActive(true);
                        foreach (var mat in r.materials)
                        {
                            if (mat != null)
                            {
                                mat.EnableKeyword("_EMISSION");
                                mat.SetColor("_EmissionColor", new Color(0.1f, 1.8f, 2.8f));
                            }
                        }
                    }
                }
            }
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
                elapsed += Time.deltaTime;
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

        public void TriggerScreenShake(float intensity, float duration)
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

        private static AnimationClip LoadClipFromFBX(string fbxPath, string clipName)
        {
#if UNITY_EDITOR
            var assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            if (assets != null)
            {
                foreach (var asset in assets)
                {
                    if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    {
                        if (string.IsNullOrEmpty(clipName) || clip.name == clipName)
                            return clip;
                    }
                }
            }
#endif
            return null;
        }

        private RuntimeAnimatorController BuildVulcanAnimatorController()
        {
#if UNITY_EDITOR
            var baseController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/InsaneGun_Sword_Set/AC_InsaneGun_CombatMaster.controller");
            if (baseController == null) return null;

            var overrideController = new AnimatorOverrideController(baseController);
            overrideController.name = "AOC_Vulcan_Warlord_Combat";

            var heavyA = LoadClipFromFBX("Assets/Synty/AnimationSwordCombat/Animations/Polygon/Attack/HeavyCombo01/A_Attack_HeavyCombo01A_Sword.fbx", "A_Attack_HeavyCombo01A_Sword");
            var heavyB = LoadClipFromFBX("Assets/Synty/AnimationSwordCombat/Animations/Polygon/Attack/HeavyCombo01/A_Attack_HeavyCombo01B_Sword.fbx", "A_Attack_HeavyCombo01B_Sword");
            var heavyC = LoadClipFromFBX("Assets/Synty/AnimationSwordCombat/Animations/Polygon/Attack/HeavyCombo01/A_Attack_HeavyCombo01C_Sword.fbx", "A_Attack_HeavyCombo01C_Sword");
            var heavySpin = LoadClipFromFBX("Assets/Synty/AnimationSwordCombat/Animations/Polygon/Attack/HeavyFlourish01/A_Attack_HeavyFlourish01_Sword.fbx", "A_Attack_HeavyFlourish_Sword");
            var heavyStab = LoadClipFromFBX("Assets/Synty/AnimationSwordCombat/Animations/Polygon/Attack/HeavyStab01/A_Attack_HeavyStab01_Sword.fbx", "A_Attack_HeavyStab01_Sword");
            var menacingIdle = LoadClipFromFBX("Assets/Synty/AnimationSwordCombat/Animations/Polygon/Idle/Menacing01/A_Idle_Menacing01_Sword.fbx", "A_Idle_Menacing01_Sword");

            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            overrideController.GetOverrides(overrides);

            for (int i = 0; i < overrides.Count; i++)
            {
                var original = overrides[i].Key;
                if (original == null) continue;
                string origName = original.name;

                if (origName == "atk_sword_01" && heavyA != null) overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(original, heavyA);
                else if (origName == "atk_sword_02" && heavyB != null) overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(original, heavyB);
                else if (origName == "atk_sword_03" && heavyC != null) overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(original, heavyC);
                else if (origName == "atk_sword_04" && heavySpin != null) overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(original, heavySpin);
                else if (origName == "atk_sword_05" && heavyStab != null) overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(original, heavyStab);
                else if (origName == "atk_sword_06" && heavyA != null) overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(original, heavyA);
                else if (origName == "atk_sword_07" && heavyC != null) overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(original, heavyC);
                else if (origName == "N_idle" && menacingIdle != null) overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(original, menacingIdle);
            }

            overrideController.ApplyOverrides(overrides);
            return overrideController;
#else
            return null;
#endif
        }

        private void SpawnVulcanBoss(Vector3 spawnPos)
        {
            // Find player
            if (player == null)
            {
                var p = GameObject.FindWithTag("Player");
                if (p != null) player = p.GetComponent<InsaneGunSwordController>();
            }

            // Clean up old rival
            var rivalGo = GameObject.Find("Rival_Cyber_Duelist");
            if (rivalGo != null) rivalGo.SetActive(false);

            // Clean up existing Vulcan if any
            var existingVulcan = GameObject.Find("Vulcan_Forge_Lord");
            if (existingVulcan != null) DestroyImmediate(existingVulcan);

            // Load Synty Sidekick Robot Prefab
            GameObject robotPrefab = null;
#if UNITY_EDITOR
            robotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/SidekickCharacters/Characters/ScifiRobots/ScifiRobots_01/ScifiRobots_01.prefab");
            if (robotPrefab == null)
            {
                robotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/SidekickCharacters/Characters/ScifiRobots/ScifiRobots_08/ScifiRobots_08.prefab");
            }
#endif

            GameObject vGo;
            if (robotPrefab != null)
            {
                vGo = Instantiate(robotPrefab, spawnPos, Quaternion.Euler(0f, 180f, 0f));
            }
            else
            {
                vGo = new GameObject("Vulcan_Forge_Lord");
                vGo.transform.position = spawnPos;
            }

            vGo.name = "Vulcan_Forge_Lord";
            vGo.tag = "Enemy";
            vGo.layer = LayerMask.NameToLayer("Enemy") != -1 ? LayerMask.NameToLayer("Enemy") : 9;

            // Scale to 1.35x (Imposing Heavy Forge Warlord stature towering over Alita)
            vGo.transform.localScale = Vector3.one * 1.35f;

            // Setup CharacterController with humanoid offset (bottom of capsule at Y=0.0)
            var cc = vGo.GetComponent<CharacterController>();
            if (cc == null) cc = vGo.AddComponent<CharacterController>();
            cc.center = new Vector3(0f, 1.10f, 0f);
            cc.height = 2.2f;
            cc.radius = 0.52f;
            cc.enabled = false; // Kept disabled during cutscene drop/standoff so physics collision does not lift or displace him

            // Setup InsaneGunSwordController
            vulcanBoss = vGo.GetComponent<InsaneGunSwordController>();
            if (vulcanBoss == null) vulcanBoss = vGo.AddComponent<InsaneGunSwordController>();

            vulcanBoss.controlMode = InsaneGunSwordController.ControlMode.AICirclingEnemy;
            vulcanBoss.aiMaxHealth = vulcanMaxHealth;
            vulcanBoss.maxHealth = vulcanMaxHealth;
            vulcanBoss.currentHealth = vulcanMaxHealth;
            vulcanCurrentHealth = vulcanMaxHealth;

            // Assign Custom Heavy Warlord Animator Controller
            var anim = vGo.GetComponent<Animator>();
            if (anim != null)
            {
                anim.applyRootMotion = false;
#if UNITY_EDITOR
                var vulcanAoc = BuildVulcanAnimatorController();
                if (vulcanAoc != null)
                {
                    anim.runtimeAnimatorController = vulcanAoc;
                }
                else
                {
                    var combatMaster = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/InsaneGun_Sword_Set/AC_InsaneGun_CombatMaster.controller");
                    if (combatMaster != null) anim.runtimeAnimatorController = combatMaster;
                }
#endif
            }

            // Combat stats: High damage, heavy stagger poise
            vulcanBoss.attackDamage = 46f;
            vulcanBoss.heavyAttackDamage = 98f;
            vulcanBoss.runSpeed = 6.2f;
            vulcanBoss.maxPoise = 130f;
            vulcanBoss.currentPoise = 130f;
            vulcanBoss.hasHyperArmor = false;
            vulcanBoss.aiBlockChance = 0.22f;
            vulcanBoss.aiParryChance = 0.12f;
            vulcanBoss.aiDodgeReactionChance = 0.18f;
            vulcanBoss.activeAICombo = InsaneGunSwordController.AIGunSwordComboType.ExecutionerShred;

            if (player != null)
            {
                vulcanBoss.targetEnemy = player.transform;
                player.targetEnemy = vulcanBoss.transform;
            }

            // Equip Heavy 2-Handed Forge Greatsword
            EquipForgeBlade(vGo);

            // Apply Forge-Lord Iron & Molten Vents Materials
            ApplyForgeLordMaterials(vGo);

            // Attach Back Smoke Vents
            AttachExhaustSmokeVents(vGo);

            // Hook events
            vulcanBoss.OnDamaged -= OnVulcanDamaged;
            vulcanBoss.OnDamaged += OnVulcanDamaged;
            vulcanBoss.OnDeath -= OnVulcanKilled;
            vulcanBoss.OnDeath += OnVulcanKilled;

            // Lock input during intro
            vulcanBoss.inputLocked = true;
        }

        private void EquipForgeBlade(GameObject bossGo)
        {
            var anim = bossGo.GetComponent<Animator>();
            Transform handTransform = bossGo.transform;
            if (anim != null && anim.isHuman)
            {
                var rHand = anim.GetBoneTransform(HumanBodyBones.RightHand);
                if (rHand != null) handTransform = rHand;
            }

            GameObject bladePrefab = null;
#if UNITY_EDITOR
            bladePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonSciFiWorlds/Prefabs/Weapons/Melee/SM_Wep_Sword_05.prefab");
            if (bladePrefab == null)
            {
                bladePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/AnimationSwordCombat/Samples/Prefabs/Wep_Sword_01.prefab");
            }
#endif

            GameObject bladeGo;
            if (bladePrefab != null)
            {
                bladeGo = Instantiate(bladePrefab, handTransform);
            }
            else
            {
                bladeGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bladeGo.transform.SetParent(handTransform);
                bladeGo.transform.localScale = new Vector3(0.08f, 1.4f, 0.15f);
                Destroy(bladeGo.GetComponent<Collider>());
            }

            bladeGo.name = "Vulcan_Heavy_Forge_Blade";
            bladeGo.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            bladeGo.transform.localRotation = Quaternion.Euler(0f, 90f, 90f);
            bladeGo.transform.localScale = Vector3.one * 1.28f;

            // Forge Glowing Edge Material
            var mr = bladeGo.GetComponentInChildren<MeshRenderer>();
            if (mr != null)
            {
                _vulcanBladeMat = mr.material;
                _vulcanBladeMat.EnableKeyword("_EMISSION");
                _vulcanBladeMat.SetColor("_BaseColor", new Color(0.12f, 0.08f, 0.05f));
                _vulcanBladeMat.SetColor("_EmissionColor", new Color(4.5f, 0.95f, 0.08f));
            }

            // Weapon Ribbon Trail
            _vulcanBladeTrail = bladeGo.AddComponent<TrailRenderer>();
            _vulcanBladeTrail.time = 0.28f;
            _vulcanBladeTrail.startWidth = 0.45f;
            _vulcanBladeTrail.endWidth = 0.05f;
            _vulcanBladeTrail.minVertexDistance = 0.05f;
#if UNITY_EDITOR
            var trailMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/SyntyCombined/Materials/Mat_EnemyTrail_Alpha.mat");
            if (trailMat != null) _vulcanBladeTrail.material = trailMat;
#endif
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(1f, 0.65f, 0.1f), 0.0f), new GradientColorKey(new Color(0.95f, 0.15f, 0.05f), 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.95f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            _vulcanBladeTrail.colorGradient = grad;

            // Point light on blade
            var lightGo = new GameObject("ForgeBlade_HeatGlow");
            lightGo.transform.SetParent(bladeGo.transform, false);
            _vulcanBladeLight = lightGo.AddComponent<Light>();
            _vulcanBladeLight.type = LightType.Point;
            _vulcanBladeLight.color = new Color(1f, 0.45f, 0.05f);
            _vulcanBladeLight.intensity = 3.5f;
            _vulcanBladeLight.range = 4.0f;
        }

        private void AttachExhaustSmokeVents(GameObject bossGo)
        {
            var anim = bossGo.GetComponent<Animator>();
            Transform chest = bossGo.transform;
            if (anim != null && anim.isHuman)
            {
                var spine = anim.GetBoneTransform(HumanBodyBones.Chest) ?? anim.GetBoneTransform(HumanBodyBones.Spine);
                if (spine != null) chest = spine;
            }

            _exhaustSmokeL = CreateSmokeVent("Vent_Smoke_L", chest, new Vector3(-0.18f, 0.15f, -0.15f));
            _exhaustSmokeR = CreateSmokeVent("Vent_Smoke_R", chest, new Vector3(0.18f, 0.15f, -0.15f));
        }

        private ParticleSystem CreateSmokeVent(string name, Transform parent, Vector3 localPos)
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
            emission.rateOverTime = 18f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 15f;
            shape.radius = 0.05f;

            var psr = smokeGo.GetComponent<ParticleSystemRenderer>();
            if (psr != null)
            {
#if UNITY_EDITOR
                var smokeMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Synty/PolygonParticles/Materials/PolygonParticleFX_Smoke_Mat_03.mat");
                if (smokeMat != null) psr.material = smokeMat;
#endif
            }

            return ps;
        }

        private void ApplyForgeLordMaterials(GameObject bossGo)
        {
            var renderers = bossGo.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                if (r == null || r.gameObject.name.Contains("Blade") || r is TrailRenderer) continue;
                foreach (var mat in r.materials)
                {
                    if (mat == null) continue;
                    _vulcanArmorMat = mat;
                    _vulcanArmorMat.EnableKeyword("_EMISSION");
                    _vulcanArmorMat.SetColor("_EmissionColor", new Color(2.8f, 0.65f, 0.05f));
                }
            }
        }

        private void IgniteVulcanVisuals()
        {
            if (_vulcanBladeMat != null)
            {
                _vulcanBladeMat.SetColor("_EmissionColor", new Color(6.5f, 1.4f, 0.1f));
            }
            if (_vulcanBladeLight != null)
            {
                _vulcanBladeLight.intensity = 5.0f;
            }
        }

        private void TriggerGroundSlamImpact()
        {
            // Spawn Impact Shockwave VFX
            GameObject shockPrefab = null;
#if UNITY_EDITOR
            shockPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonParticles/Prefabs/FX_Fire_Explosion_01.prefab");
            if (shockPrefab == null)
            {
                shockPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonParticles/Prefabs/FX_Impact_Large_01.prefab");
            }
#endif
            if (shockPrefab != null)
            {
                var fx = Instantiate(shockPrefab, ringCenterPos, Quaternion.identity);
                Destroy(fx, 4.0f);
            }

            if (sfxSource != null && sfxGroundSlam != null)
            {
                sfxSource.PlayOneShot(sfxGroundSlam, 1.0f);
            }

            // Camera trauma shake
            SmoothOrbitCamera.AddShake(0.85f);

            // Dramatic Spotlight on Vulcan
            if (_spotlightVulcan == null)
            {
                GameObject spotGo = new GameObject("Spotlight_Vulcan_Arrival");
                spotGo.transform.position = new Vector3(0f, 15f, 0f);
                _spotlightVulcan = spotGo.AddComponent<Light>();
                _spotlightVulcan.type = LightType.Spot;
                _spotlightVulcan.spotAngle = 65f;
                _spotlightVulcan.innerSpotAngle = 35f;
                _spotlightVulcan.range = 28f;
                _spotlightVulcan.color = new Color(1.0f, 0.55f, 0.15f);
                _spotlightVulcan.intensity = 16f;
                _spotlightVulcan.shadows = LightShadows.Soft;
            }
            else
            {
                _spotlightVulcan.intensity = 16f;
            }
        }

        private void SpawnCraterImpactLighting()
        {
            GameObject lightGo = new GameObject("Crater_Explosion_FlashLight");
            lightGo.transform.position = ringCenterPos + Vector3.up * 0.6f;
            var flash = lightGo.AddComponent<Light>();
            flash.type = LightType.Point;
            flash.color = new Color(1.0f, 0.5f, 0.08f);
            flash.intensity = 18f;
            flash.range = 14f;
            StartCoroutine(FadeOutLight(flash, 1.1f));
        }

        private IEnumerator FadeOutLight(Light l, float duration)
        {
            if (l == null) yield break;
            float startInt = l.intensity;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                if (l != null) l.intensity = Mathf.Lerp(startInt, 0f, elapsed / duration);
                yield return null;
            }
            if (l != null) Destroy(l.gameObject);
        }

        private void RestoreArenaLighting(bool forgeTint = false)
        {
            for (int i = 0; i < _cachedArenaLights.Count; i++)
            {
                if (_cachedArenaLights[i] != null)
                {
                    _cachedArenaLights[i].intensity = Mathf.Max(_originalLightIntensities[i], 1.2f);
                    _cachedArenaLights[i].color = forgeTint
                        ? Color.Lerp(_originalLightColors[i], new Color(1.0f, 0.35f, 0.1f), 0.55f)
                        : _originalLightColors[i];
                    _cachedArenaLights[i].enabled = true;
                }
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.28f, 0.32f, 0.42f, 1f);

            var keyLight = GameObject.Find("Stadium_Overhead_Broadcast_KeyLight")?.GetComponent<Light>();
            if (keyLight != null) { keyLight.intensity = 1.4f; keyLight.enabled = true; }

            var rimLight = GameObject.Find("Stadium_Rim_Contrast_KeyLight")?.GetComponent<Light>();
            if (rimLight != null) { rimLight.intensity = 0.75f; rimLight.enabled = true; }
        }

        public void DuckMusicVolume(float targetVolume, float duration)
        {
            if (_musicDuckRoutine != null) StopCoroutine(_musicDuckRoutine);
            _musicDuckRoutine = StartCoroutine(DuckMusicRoutine(targetVolume, duration));
        }

        private IEnumerator DuckMusicRoutine(float targetVolume, float duration)
        {
            if (musicSource == null) yield break;
            float startVol = musicSource.volume;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                musicSource.volume = Mathf.Lerp(startVol, targetVolume, elapsed / duration);
                yield return null;
            }
            musicSource.volume = targetVolume;
        }

        public void PlayVulcanVoiceLine(AudioClip clip, string text, float displayDuration = 5f, bool duckBgm = true, float duckVolume = 0.45f)
        {
            if (duckBgm)
            {
                DuckMusicVolume(duckVolume, 0.35f);
            }

            // Silence any ambient crowd murmur from interrupting
            Cyberpunk.Audio.PitCrowdAudioManager.Instance?.CutDrumsAndAmbience();

            // Native Mission 4 Subtitles via AAA CyberpunkDialogueHUD
            _activeSubtitle = text;
            _activeSpeaker = "// VULCAN // FORGE-LORD //";
            _speakerColor = new Color(1.0f, 0.45f, 0.05f, 1.0f);
            _subtitleTimer = displayDuration;

            Alita.UI.CyberpunkDialogueHUD.GetOrCreate().ShowDialogue(
                "FORGE-LORD VULCAN",
                text,
                null,
                "ARENA MASTER",
                null,
                displayDuration
            );

            if (clip != null && voiceSource != null)
            {
                voiceSource.clip = clip;
                voiceSource.priority = 0;
                voiceSource.volume = 1.0f;
                voiceSource.Play();
            }
        }

        private void Update()
        {
            if (_subtitleTimer > 0f)
            {
                _subtitleTimer -= Time.deltaTime;
                if (_subtitleTimer <= 0f)
                {
                    _activeSubtitle = "";
                    _activeSpeaker = "";
                    Alita.UI.CyberpunkDialogueHUD.Instance?.CloseDialogue();
                }
            }

            if (currentPhase == ShowdownPhase.Phase1_TheDuelist ||
                currentPhase == ShowdownPhase.Phase2_Meltdown ||
                currentPhase == ShowdownPhase.Phase3_Judgement)
            {
                UpdateBossFightState();
            }
            else if (currentPhase == ShowdownPhase.KneelingDefeat)
            {
                UpdateKneelingDefeatPrompt();
            }

            // Debug hotkey for instant test: [F4] force triggers / [Shift+F4] resets defeat save flag
            if (Input.GetKeyDown(KeyCode.F4))
            {
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                {
                    PlayerPrefs.DeleteKey(PREF_VULCAN_DEFEATED);
                    PlayerPrefs.Save();
                    Debug.Log("[Mission4] Reset vulcan_boss_defeated save flag. Vulcan will spawn naturally after Round 3.");
                    CyberpunkHUDController.Instance?.ShowToast("// DEBUG: VULCAN BOSS PROGRESSION RESET //", 2.5f);
                }
                else
                {
                    Debug.Log("[Mission4] Debug trigger F4 pressed - initiating Vulcan Showdown!");
                    TriggerVulcanArrival(force: true);
                }
            }
        }

        private void LateUpdate()
        {
            if (currentPhase == ShowdownPhase.CinematicHijack && _cutsceneCam != null)
            {
                _cutsceneCam.transform.position = _currentCamBasePos + _shakeOffset;
            }
        }

        private void UpdateBossFightState()
        {
            if (vulcanBoss == null) return;

            vulcanCurrentHealth = vulcanBoss.currentHealth;
            float hpPct = Mathf.Clamp01(vulcanCurrentHealth / vulcanMaxHealth);

            // Update Boss Bar
            string phaseName = currentPhase == ShowdownPhase.Phase1_TheDuelist ? "PHASE 1: THE DUELIST" :
                               currentPhase == ShowdownPhase.Phase2_Meltdown ? "PHASE 2: MELTDOWN" : "PHASE 3: FORGE JUDGEMENT";
            CyberpunkHUDController.Instance?.SetBossHealth(hpPct, $"VULCAN // FORGE-LORD ({phaseName})");

            // Transition to Phase 2 (<= 66% HP)
            if (currentPhase == ShowdownPhase.Phase1_TheDuelist && hpPct <= 0.66f)
            {
                TriggerPhase2();
            }
            // Transition to Phase 3 (<= 33% HP)
            else if (currentPhase == ShowdownPhase.Phase2_Meltdown && hpPct <= 0.33f)
            {
                TriggerPhase3();
            }
        }

        private void TriggerPhase2()
        {
            currentPhase = ShowdownPhase.Phase2_Meltdown;
            Debug.Log("<color=#FF3300><b>[Mission 4]</b></color> Entering Phase 2: Reactor Meltdown!");

            // Jumbotron Flash: WARNING: REACTOR MELTDOWN
            SetJumbotronDisplay("WARNING: REACTOR MELTDOWN", new Color(1f, 0.1f, 0.1f, 1f), glitchEffect: true);
            CyberpunkHUDController.Instance?.ShowToast("⚠ WARNING: VULCAN REACTOR MELTDOWN INITIATED ⚠", 3.5f);

            // Voice Line 02 (Audio only, no dialogue popup)
            if (voiceSource != null && voicePhase2 != null)
            {
                voiceSource.PlayOneShot(voicePhase2);
            }

            // Red Warning Lights Pulse
            StartRedStrobeLights();

            // Double emissive on armor & blade
            if (_vulcanArmorMat != null) _vulcanArmorMat.SetColor("_EmissionColor", new Color(5.6f, 1.3f, 0.1f));
            if (_vulcanBladeMat != null) _vulcanBladeMat.SetColor("_EmissionColor", new Color(7.0f, 1.7f, 0.1f));
            if (_vulcanBladeLight != null) _vulcanBladeLight.intensity = 4.5f;

            // Combat acceleration
            if (vulcanBoss != null)
            {
                vulcanBoss.runSpeed = 6.8f;
                vulcanBoss.attackDamage = 55f;
                vulcanBoss.activeAICombo = InsaneGunSwordController.AIGunSwordComboType.StalkerLunge;
            }
        }

        private void TriggerPhase3()
        {
            currentPhase = ShowdownPhase.Phase3_Judgement;
            Debug.Log("<color=#FF0000><b>[Mission 4]</b></color> Entering Phase 3: Judgement of the Forge!");

            // Jumbotron Flash: JUDGEMENT OF THE FORGE
            SetJumbotronDisplay("JUDGEMENT OF THE FORGE", new Color(1f, 0.25f, 0.05f, 1f));
            CyberpunkHUDController.Instance?.ShowToast("⚔ JUDGEMENT OF THE FORGE // MAXIMUM HAZARD ⚔", 3.5f);

            // Crossfade Music to Climax Track (echoes_of_lumen)
            if (musicSource != null && musicMeltdownClimax != null)
            {
                musicSource.clip = musicMeltdownClimax;
                musicSource.volume = 1.0f;
                musicSource.Play();
            }

            // Voice Line 03 (Audio only, no dialogue popup)
            if (voiceSource != null && voicePhase3 != null)
            {
                voiceSource.PlayOneShot(voicePhase3);
            }

            // Emissive x3.5 blazing magma
            if (_vulcanArmorMat != null) _vulcanArmorMat.SetColor("_EmissionColor", new Color(9.5f, 2.4f, 0.2f));
            if (_vulcanBladeMat != null) _vulcanBladeMat.SetColor("_EmissionColor", new Color(12.0f, 3.2f, 0.25f));
            if (_vulcanBladeLight != null)
            {
                _vulcanBladeLight.intensity = 7.0f;
                _vulcanBladeLight.range = 5.5f;
            }

            // Permanent HyperArmor & Berserk combo strings
            if (vulcanBoss != null)
            {
                vulcanBoss.hasHyperArmor = true;
                vulcanBoss.runSpeed = 7.4f;
                vulcanBoss.attackDamage = 64f;
                vulcanBoss.activeAICombo = InsaneGunSwordController.AIGunSwordComboType.BlitzkriegOverdrive;
            }
        }

        private void StartRedStrobeLights()
        {
            if (_strobeLightsRoutine != null) StopCoroutine(_strobeLightsRoutine);
            _strobeLightsRoutine = StartCoroutine(RedStrobeRoutine());
        }

        private IEnumerator RedStrobeRoutine()
        {
            while (currentPhase == ShowdownPhase.Phase2_Meltdown || currentPhase == ShowdownPhase.Phase3_Judgement)
            {
                float pulse = Mathf.PingPong(Time.time * 3.5f, 1.0f);
                Color redAlarm = Color.Lerp(new Color(0.2f, 0.02f, 0.02f), new Color(1.0f, 0.1f, 0.1f), pulse);

                for (int i = 0; i < _cachedArenaLights.Count; i++)
                {
                    if (_cachedArenaLights[i] != null)
                    {
                        _cachedArenaLights[i].color = redAlarm;
                        _cachedArenaLights[i].intensity = _originalLightIntensities[i] * (0.6f + pulse * 0.6f);
                    }
                }
                yield return null;
            }
        }

        private void OnVulcanDamaged(InsaneGunSwordController v, float dmg)
        {
            PitCrowdAudioManager.Instance?.PlayHitReaction(dmg);
        }

        private void OnVulcanKilled(InsaneGunSwordController v)
        {
            if (currentPhase == ShowdownPhase.KneelingDefeat || currentPhase == ShowdownPhase.VictoryCompleted) return;

            currentPhase = ShowdownPhase.KneelingDefeat;
            IsVulcanDefeated = true;

            if (_strobeLightsRoutine != null) StopCoroutine(_strobeLightsRoutine);
            RestoreArenaLighting(forgeTint: false);

            // Jumbotron: CHAMPION: ALITA // NEW LEGEND OF THE PIT
            SetJumbotronDisplay("CHAMPION: ALITA // NEW LEGEND OF THE PIT", new Color(0f, 0.95f, 1f, 1f));
            CyberpunkHUDController.Instance?.HideBossBar();

            // Vulcan Kneeling state
            if (vulcanBoss != null)
            {
                vulcanBoss.inputLocked = true;
                vulcanBoss.isInvincible = true;
                var anim = vulcanBoss.GetComponent<Animator>();
                if (anim != null)
                {
                    anim.SetTrigger("Die");
                }
            }

            // Voice Line 04 (Audio only, no dialogue popup)
            if (voiceSource != null && voiceDeath != null)
            {
                voiceSource.PlayOneShot(voiceDeath);
            }
            Alita.UI.CyberpunkDialogueHUD.Instance?.CloseDialogue();

            PendingIdoReputationMessage = true;

            Debug.Log("<color=#00FFAA><b>[Mission 4]</b></color> Vulcan defeated! Press [E] to execute finisher.");
        }

        private void UpdateKneelingDefeatPrompt()
        {
            if (vulcanBoss == null) return;

            float dist = Vector3.Distance(player != null ? player.transform.position : Vector3.zero, vulcanBoss.transform.position);

            // E also advances dialogue; don't let it fire the finisher while Ido is talking.
            bool dialogueOpen = Alita.UI.CyberpunkDialogueHUD.Instance != null &&
                                Alita.UI.CyberpunkDialogueHUD.Instance.isDialogueActive;
            if (!dialogueOpen && dist <= 3.8f && Input.GetKeyDown(KeyCode.E))
            {
                ExecuteFinisher();
            }
        }

        private void ExecuteFinisher()
        {
            currentPhase = ShowdownPhase.VictoryCompleted;
            IsVulcanDefeated = true;

            // Player Finisher Strike
            if (player != null)
            {
                player.transform.LookAt(vulcanBoss.transform.position);
                var pAnim = player.GetComponent<Animator>();
                if (pAnim != null) pAnim.SetTrigger("AttackHeavy");
            }

            // Impact Spark & Disassembly Explosion
            GameObject expPrefab = null;
#if UNITY_EDITOR
            expPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonParticles/Prefabs/FX_Explosion_Large_Dark_01.prefab");
#endif
            if (expPrefab != null && vulcanBoss != null)
            {
                var fx = Instantiate(expPrefab, vulcanBoss.transform.position + Vector3.up * 1.0f, Quaternion.identity);
                Destroy(fx, 4.0f);
            }

            // Remove Boss GameObject
            if (vulcanBoss != null)
            {
                Destroy(vulcanBoss.gameObject, 0.5f);
            }

            // Smoothly fade out boss battle music
            if (musicSource != null && musicSource.isPlaying)
            {
                StartCoroutine(FadeOutMusic(musicSource, 2.5f));
            }

            // Deafening Crowd Ovation
            PitCrowdAudioManager.Instance?.PlayCheer(isEntrance: true);
            PitCrowdAudioManager.Instance?.StartClap();

            // Massive Purse: +€$ 10,000!
            CyberpunkInventory.EnsureInScene();
            if (CyberpunkInventory.Instance != null)
            {
                CyberpunkInventory.Instance.AddEddies(10000);
            }

            // Victory Toast
            CyberpunkHUDController.Instance?.ShowToast(
                "// VULCAN EXECUTED // THE PIT BELONGS TO ALITA! // +€$10,000 // RETURN TO SECTOR 04", 6.0f);
            CyberpunkHUDController.Instance?.SetQuestObjective("MISSION 4 // COMPLETE", "LEGEND OF THE PIT SECURED");

            // Unlock South exit with green light
            PitArenaExitTrigger.IsExitLocked = false;

            // Clear Jumbotron boss display
            SetJumbotronDisplay("// CHAMPION: ALITA // PIT LEGEND //", new Color(0f, 0.95f, 1f, 1f));

            // Restore arena lighting
            RestoreArenaLighting(forgeTint: false);

            // Re-arm Pit Arena so it seamlessly transitions back into normal championship mode
            currentPhase = ShowdownPhase.Inactive;
            Mission3_PitFightManager.Instance?.OnVulcanShowdownCompleted();
        }

        /// <summary>Set when Vulcan falls; Ido's reputation message plays once Alita is back in the city.</summary>
        public static bool PendingIdoReputationMessage;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void HookIdoReputationMessage()
        {
            PendingIdoReputationMessage = false;
            SceneManager.sceneLoaded -= OnSceneLoadedForIdoMessage;
            SceneManager.sceneLoaded += OnSceneLoadedForIdoMessage;
        }

        private static void OnSceneLoadedForIdoMessage(Scene scene, LoadSceneMode mode)
        {
            if (!PendingIdoReputationMessage || scene.name != "Demo_City") return;
            PendingIdoReputationMessage = false;
            new GameObject("IdoReputationMessenger").AddComponent<IdoReputationMessenger>();
        }

        private class IdoReputationMessenger : MonoBehaviour
        {
            private IEnumerator Start()
            {
                // Let the loading screen finish and the player settle outside the arena doors.
                yield return new WaitForSecondsRealtime(4.0f);

                var lines = new List<Alita.UI.CyberpunkDialogueHUD.DialogueLineData>
                {
                    CutsceneLine("DR. DYSON IDO", "CYBER-SURGEON // COMMS", "Alita... you did it. The whole district just watched you take down Vulcan."),
                    CutsceneLine("DR. DYSON IDO", "CYBER-SURGEON // COMMS", "You've earned a reputation in this city now. People respect you for what you did in the Pit, so no one will attack you anymore, even if you attack them."),
                    CutsceneLine("DR. DYSON IDO", "CYBER-SURGEON // COMMS", "They'll only run from you now. Use that respect wisely."),
                };
                bool done = false;
                Alita.UI.CyberpunkDialogueHUD.GetOrCreate().StartConversation(lines, () => done = true);
                float t = 0f;
                while (!done && t < 40f)
                {
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
                Destroy(gameObject);
            }
        }

        private IEnumerator FadeOutMusic(AudioSource src, float duration)
        {
            if (src == null) yield break;
            float startVol = src.volume;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                if (src != null) src.volume = Mathf.Lerp(startVol, 0f, elapsed / duration);
                yield return null;
            }
            if (src != null)
            {
                src.Stop();
                src.volume = startVol;
            }
        }

        private void BuildGUIStyles()
        {
            _jumbotronBgTex = new Texture2D(1, 1);
            _jumbotronBgTex.SetPixel(0, 0, new Color(0.01f, 0.02f, 0.04f, 0.92f));
            _jumbotronBgTex.Apply();

            _jumbotronTickerStyle = new GUIStyle
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0f, 0.95f, 1f, 1f) }
            };

            _finishPromptStyle = new GUIStyle
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1.0f, 0.92f, 0.05f, 1f) }
            };

            _speakerStyle = new GUIStyle
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1.0f, 0.45f, 0.05f, 1f) }
            };

            _subtitleStyle = new GUIStyle
            {
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = Color.white }
            };
        }

        private void OnGUI()
        {
            // 1. Cinematic Jumbotron Top Banner (Visible during hijack and boss fight)
            if (currentPhase != ShowdownPhase.Inactive && !string.IsNullOrEmpty(currentJumbotronText))
            {
                DrawTopJumbotronBanner();
            }

            // 2. Kneeling [E] FINISH Prompt
            if (currentPhase == ShowdownPhase.KneelingDefeat)
            {
                DrawFinishPrompt();
            }

            // 3. Subtitles are rendered via CyberpunkDialogueHUD (Canvas uGUI)
        }

        private void DrawTopJumbotronBanner()
        {
            float sw = Screen.width;
            float w = Mathf.Clamp(sw * 0.72f, 620f, 960f);
            float h = 48f;
            float x = (sw - w) * 0.5f;
            float y = 14f;

            Rect r = new Rect(x, y, w, h);
            GUI.DrawTexture(r, _jumbotronBgTex);

            Color borderColor = isJumbotronGlitching ? (UnityEngine.Random.value > 0.5f ? Color.red : Color.cyan) : currentJumbotronColor;
            DrawBorder(r, borderColor, 2f);

            _jumbotronTickerStyle.normal.textColor = borderColor;
            GUI.Label(new Rect(x, y + 10f, w, 28f), currentJumbotronText, _jumbotronTickerStyle);
        }

        private void DrawFinishPrompt()
        {
            float sw = Screen.width;
            float sh = Screen.height;
            float w = 460f;
            float h = 54f;
            float x = (sw - w) * 0.5f;
            float y = sh * 0.65f;

            Rect r = new Rect(x, y, w, h);
            GUI.DrawTexture(r, _jumbotronBgTex);

            float pulse = Mathf.Sin(Time.time * 8f) * 0.15f + 0.85f;
            DrawBorder(r, new Color(1f, 0.9f, 0.1f, pulse), 2.5f);

            GUI.Label(new Rect(x, y + 12f, w, 30f), "PRESS [ E ] TO FINISH FORGE-LORD", _finishPromptStyle);
        }

        private void DrawSubtitles()
        {
            float sw = Screen.width;
            float sh = Screen.height;
            float subHeight = 52f;
            float subY = sh - subHeight - 38f;
            float spkY = subY - 26f;

            // 4-way drop shadow for speaker badge
            _speakerStyle.normal.textColor = new Color(0f, 0f, 0f, 0.95f);
            GUI.Label(new Rect(1, spkY + 1, sw, 24), _activeSpeaker, _speakerStyle);
            GUI.Label(new Rect(-1, spkY - 1, sw, 24), _activeSpeaker, _speakerStyle);
            GUI.Label(new Rect(-1, spkY + 1, sw, 24), _activeSpeaker, _speakerStyle);
            GUI.Label(new Rect(1, spkY - 1, sw, 24), _activeSpeaker, _speakerStyle);

            _speakerStyle.normal.textColor = _speakerColor;
            GUI.Label(new Rect(0, spkY, sw, 24), _activeSpeaker, _speakerStyle);

            // 4-way drop shadow for subtitle body
            _subtitleStyle.normal.textColor = new Color(0f, 0f, 0f, 0.95f);
            GUI.Label(new Rect(1, subY + 1, sw, subHeight), _activeSubtitle, _subtitleStyle);
            GUI.Label(new Rect(-1, subY - 1, sw, subHeight), _activeSubtitle, _subtitleStyle);
            GUI.Label(new Rect(-1, subY + 1, sw, subHeight), _activeSubtitle, _subtitleStyle);
            GUI.Label(new Rect(1, subY - 1, sw, subHeight), _activeSubtitle, _subtitleStyle);

            _subtitleStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(0, subY, sw, subHeight), _activeSubtitle, _subtitleStyle);
        }

        private void DrawBorder(Rect r, Color color, float width)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, width), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - width, r.width, width), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, width, r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - width, r.y, width, r.height), Texture2D.whiteTexture);
            GUI.color = old;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            if (vulcanBoss != null)
            {
                vulcanBoss.OnDamaged -= OnVulcanDamaged;
                vulcanBoss.OnDeath -= OnVulcanKilled;
            }

            if (_jumbotronBgTex != null) Destroy(_jumbotronBgTex);
        }
    }
}
