using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using InsaneGun.Combat;
using SyntyCombined;
using SyntyCombined.Cutscene;
using Alita.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Cyberpunk.Missions
{
    /// <summary>
    /// Post-Mission-4 finale on the Sector 7 Skybridge: SwitchBlade (ScifiCivilians_03), the cyborg who runs
    /// the organisation, assassinates Dr. Ido from behind while Alita is caught off guard.
    ///
    /// Flow: Ido's reputation call ends -> Begin() spawns Ido on the bridge and shows a waypoint ->
    /// when Alita walks up, the multi-shot cutscene plays -> gameplay resumes with Ido's body left on the bridge.
    ///
    /// Paired animations (Nephilite Straight Sword "Critical Strike"): core_main_backstab_01 (attacker) and
    /// core_main_backstab_victim_01 (victim). The attacker stands 0.6m behind the victim, both facing the same way;
    /// the victim clip carries ~2.2m of root motion so Ido staggers forward into the fall.
    /// </summary>
    public class IdoAssassinationDirector : MonoBehaviour
    {
        public static IdoAssassinationDirector Instance { get; private set; }

        /// <summary>True once the scene has been played in this session.</summary>
        public static bool HasPlayed { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic()
        {
            Instance = null;
            HasPlayed = false;
        }

        // ── Bridge layout (bridge runs along world X at Z = -8.75, width 2.5m) ──
        private const float BridgeZ = -8.75f;
        private const float BridgeY = 0.05f;
        private static readonly Vector3 IdoPos = new Vector3(1.5f, BridgeY, BridgeZ);        // faces -X, toward Alita
        private static readonly Vector3 AlitaPos = new Vector3(-2.6f, BridgeY, BridgeZ);     // faces +X, toward Ido
        private static readonly Vector3 BladeHiddenPos = new Vector3(4.5f, BridgeY, BridgeZ); // far behind Ido, faces -X
        private static readonly Vector3 BladeStabPos = new Vector3(IdoPos.x + 0.6f, BridgeY, BridgeZ);
        private const float TriggerRadius = 9f;

        private const string ControllerPath = "Assets/Animations/AC_IdoAssassination.controller";

        // ── Actors / state ───────────────────────────────────────────────────
        private GameObject _player;
        private GameObject _ido;
        private Animator _idoAnim;
        private GameObject _blade;
        private Animator _bladeAnim;
        private Animator _playerAnim;
        private RuntimeAnimatorController _playerOriginalController;
        private bool _approachActive;
        private bool _cutsceneRunning;

        private Camera _cam;
        private SmoothOrbitCamera _orbitCam;
        private Coroutine _camRoutine;
        private Coroutine _shakeRoutine;
        private Vector3 _camBase;
        private Vector3 _shakeOffset;

        private AudioSource _music;
        private AudioSource _sfx;
        private AudioClip _musicClip, _thrustClip, _thunderClip, _dashClip;
        private GameObject _vfxStab, _vfxBlood, _vfxElectric;

        private GameObject _ambientIdoBody;

        /// <summary>Starts the post-Mission-4 sequence: Ido waits on the bridge and a waypoint leads Alita to him.</summary>
        public static void Begin()
        {
            if (HasPlayed) return;
            if (Instance == null)
            {
                var go = new GameObject("[Ido_Assassination_Director]");
                Instance = go.AddComponent<IdoAssassinationDirector>();
            }
            SuppressMission1AndNearbyEnemies();
            SuppressPrologueHUD();
            Instance.StartApproach();
        }

        /// <summary>Editor/debug entry: plays the cutscene immediately, skipping the approach, and allows replays.</summary>
        public static void DebugPlayNow()
        {
            if (Instance == null)
            {
                var go = new GameObject("[Ido_Assassination_Director]");
                Instance = go.AddComponent<IdoAssassinationDirector>();
            }
            SuppressMission1AndNearbyEnemies();
            SuppressPrologueHUD();
            Instance.ResetForReplay();
            Instance._player = GameObject.FindWithTag("Player");
            Instance.StartCoroutine(Instance.AssassinationCutscene());
        }

        private static void SuppressMission1AndNearbyEnemies()
        {
            Mission1_StreetTrialManager.IsMission1Completed = true;
            var m1 = Mission1_StreetTrialManager.Instance != null ? Mission1_StreetTrialManager.Instance : FindAnyObjectByType<Mission1_StreetTrialManager>();
            if (m1 != null)
            {
                m1.StopAllCoroutines();
                if (m1.mission1Zone != null) Destroy(m1.mission1Zone);
                if (m1.missionMarkerObj != null) Destroy(m1.missionMarkerObj);
                if (m1.activeEnemies != null)
                {
                    foreach (var enemy in m1.activeEnemies)
                    {
                        if (enemy != null && enemy.gameObject != null) Destroy(enemy.gameObject);
                    }
                    m1.activeEnemies.Clear();
                }
                m1.enabled = false;
                Destroy(m1.gameObject);
            }

            var zone = GameObject.Find("Mission1_Zone");
            if (zone != null) Destroy(zone);
            var marker = GameObject.Find("Mission1_Marker");
            if (marker != null) Destroy(marker);
            var m1Runtime = GameObject.Find("Mission1_StreetTrial_Runtime");
            if (m1Runtime != null) Destroy(m1Runtime);

            // Destroy all Mission1_EnemyTrackers and their GameObjects
            var trackers = FindObjectsByType<Mission1_EnemyTracker>(FindObjectsSortMode.None);
            foreach (var t in trackers)
            {
                if (t != null && t.gameObject != null) Destroy(t.gameObject);
            }

            // Destroy all enemies near the skybridge corridor or with combat controllers
            var allControllers = FindObjectsByType<InsaneGun.Combat.InsaneGunSwordController>(FindObjectsSortMode.None);
            foreach (var ctrl in allControllers)
            {
                if (ctrl == null || ctrl.gameObject == null) continue;
                if (ctrl.CompareTag("Player") || ctrl.gameObject.name.Contains("Alita")) continue;
                if (ctrl.CompareTag("Enemy") || ctrl.GetComponent<Mission1_EnemyTracker>() != null || ctrl.name.StartsWith("⛧") || ctrl.name.Contains("Thug"))
                {
                    Destroy(ctrl.gameObject);
                }
            }

            var enemies = GameObject.FindGameObjectsWithTag("Enemy");
            foreach (var e in enemies)
            {
                if (e != null && Vector3.Distance(e.transform.position, new Vector3(0f, 0.5f, BridgeZ)) < 60f)
                {
                    Destroy(e);
                }
            }

            // Clear Mission 1 UI
            var ui = Mission1_UIManager.Instance != null ? Mission1_UIManager.Instance : FindAnyObjectByType<Mission1_UIManager>();
            if (ui != null)
            {
                ui.HideKillTracker();
                ui.UpdateWaypointHUD(Vector3.zero, false);
                ui.ShowFollowPopup(false);
                ui.DismissPopup();
            }
            var blips = GameObject.Find("Container_EnemyBlips");
            if (blips != null) blips.SetActive(false);
        }

        private static void SuppressPrologueHUD()
        {
            var prologue = Alita.AlitaCityPrologueDirector.Instance != null ? Alita.AlitaCityPrologueDirector.Instance : FindAnyObjectByType<Alita.AlitaCityPrologueDirector>();
            if (prologue != null)
            {
                prologue.StopAllCoroutines();
                prologue.enabled = false;
                if (prologue.doctorActor != null) Destroy(prologue.doctorActor);
                Destroy(prologue.gameObject);
            }
            var docActor = GameObject.Find("Doctor_Actor_Runtime");
            if (docActor != null) Destroy(docActor);
            var sidekickIdo = GameObject.Find("Sidekick_Doctor_Ido");
            if (sidekickIdo != null) Destroy(sidekickIdo);
        }

        private void ResetForReplay()
        {
            StopAllCoroutines();
            _cutsceneRunning = false;
            _approachActive = false;
            if (_ido != null) Destroy(_ido);
            if (_blade != null) Destroy(_blade);
            _ido = null; _blade = null; _idoAnim = null; _bladeAnim = null;
            Time.timeScale = 1f;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _music = gameObject.AddComponent<AudioSource>();
            _music.spatialBlend = 0f; _music.loop = true; _music.playOnAwake = false; _music.volume = 0.8f;
            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.spatialBlend = 0f; _sfx.playOnAwake = false;
            LoadAssets();
        }

        private void OnDisable()
        {
            Time.timeScale = 1f;
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
        }

        private void LoadAssets()
        {
#if UNITY_EDITOR
            _musicClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/vulcan_battle/viramiller-ominous-284554.mp3");
            _thrustClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Combat/Sword_Slash_Piercing_Thrust_01.wav");
            _thunderClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Weather/dragon-studio-dry-thunder-364468.mp3");
            _dashClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Combat/Dash_Cyber_01.wav");
            _vfxStab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonParticles/Prefabs/FX_SwordStab_01.prefab");
            _vfxBlood = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonParticles/Prefabs/FX_BloodSplat_01.prefab");
            _vfxElectric = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonParticles/Prefabs/FX_Electricity_01.prefab");
#endif
        }

        // ─────────────────────────────────────────────────────────────────────
        // APPROACH: Ido waits on the bridge, waypoint guides Alita to him
        // ─────────────────────────────────────────────────────────────────────
        private void StartApproach()
        {
            if (_approachActive || _cutsceneRunning) return;
            _player = GameObject.FindWithTag("Player");
            _approachActive = true;

            SpawnIdo();
            var hud = CyberpunkHUDController.Instance;
            if (hud != null)
            {
                hud.SetQuestObjective("// MEET DR. IDO ON THE SKYBRIDGE", "> Dr. Ido is waiting at Sector 7 Skybridge\n> He has something to tell you");
            }
        }

        private void Update()
        {
            if (!_approachActive || _cutsceneRunning) return;
            if (_player == null) { _player = GameObject.FindWithTag("Player"); if (_player == null) return; }

            var ui = Mission1_UIManager.Instance != null ? Mission1_UIManager.Instance : FindAnyObjectByType<Mission1_UIManager>();
            Vector3 target = IdoPos;
            float dist = Vector3.Distance(_player.transform.position, target);
            if (ui != null) ui.UpdateWaypointHUD(target, true, "DR. IDO");

            if (dist <= TriggerRadius)
            {
                _approachActive = false;
                if (ui != null) ui.UpdateWaypointHUD(target, false, null);
                StartCoroutine(AssassinationCutscene());
            }
        }

        private void SpawnIdo()
        {
            if (_ido != null) return;
            GameObject prefab = null;
#if UNITY_EDITOR
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/Doctor/Doctor.prefab")
                  ?? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonCyberCity/Prefabs/Characters/SM_Chr_Rich_Male_01.prefab");
#endif
            if (prefab == null) { Debug.LogError("[IdoAssassination] Doctor prefab not found."); return; }
            _ido = Instantiate(prefab, IdoPos, Quaternion.Euler(0f, -90f, 0f));
            _ido.name = "Ido_Bridge_Actor";
            StripGameplayComponents(_ido);
            _idoAnim = _ido.GetComponentInChildren<Animator>();
            ApplyCutsceneController(_idoAnim);
            if (_idoAnim != null) _idoAnim.Play("Idle", 0, 0f);
        }

        private void SpawnSwitchBlade()
        {
            if (_blade != null) return;
            GameObject prefab = null;
#if UNITY_EDITOR
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/SidekickCharacters/Characters/ScifiCivilians/ScifiCivilians_03/ScifiCivilians_03.prefab");
#endif
            if (prefab == null) { Debug.LogError("[IdoAssassination] ScifiCivilians_03 prefab not found."); return; }
            _blade = Instantiate(prefab, BladeHiddenPos, Quaternion.Euler(0f, -90f, 0f));
            _blade.name = "SwitchBlade";
            StripGameplayComponents(_blade);
            _bladeAnim = _blade.GetComponentInChildren<Animator>();
            ApplyCutsceneController(_bladeAnim);
            if (_bladeAnim != null) _bladeAnim.Play("Menace", 0, 0f);
            EquipBlade(_blade);
        }

        private static void StripGameplayComponents(GameObject go)
        {
            foreach (var cc in go.GetComponentsInChildren<CharacterController>(true)) cc.enabled = false;
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true)) rb.isKinematic = true;
            foreach (var col in go.GetComponentsInChildren<Collider>(true)) col.enabled = false;
            foreach (var na in go.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true)) na.enabled = false;
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                string ns = mb.GetType().Namespace ?? "";
                if (ns.StartsWith("Unity") || ns.StartsWith("TMPro")) continue;
                mb.enabled = false;
            }
            go.layer = 2; // Ignore Raycast
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = 2;
            }
        }

        private static void ApplyCutsceneController(Animator anim)
        {
            if (anim == null) return;
#if UNITY_EDITOR
            var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (ctrl != null) anim.runtimeAnimatorController = ctrl;
#endif
            anim.applyRootMotion = false;
            anim.speed = 1f;
        }

        private static Transform FindChildRecursive(Transform parent, string childName)
        {
            if (parent == null) return null;
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (string.Equals(child.name, childName, StringComparison.OrdinalIgnoreCase)) return child;
                var found = FindChildRecursive(child, childName);
                if (found != null) return found;
            }
            return null;
        }

        private void EquipBlade(GameObject owner)
        {
            Transform socket = FindChildRecursive(owner.transform, "prop_r") ?? FindChildRecursive(owner.transform, "Prop_R");
            bool dedicated = socket != null;
            if (socket == null)
            {
                var anim = owner.GetComponentInChildren<Animator>();
                if (anim != null && anim.isHuman) socket = anim.GetBoneTransform(HumanBodyBones.RightHand);
            }
            if (socket == null) return;

            GameObject bladePrefab = null;
#if UNITY_EDITOR
            bladePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonSciFiWorlds/Prefabs/Weapons/Melee/SM_Wep_Sword_05.prefab")
                       ?? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/AnimationSwordCombat/Samples/Prefabs/Wep_Sword_01.prefab");
#endif
            if (bladePrefab == null) return;
            var sword = Instantiate(bladePrefab, socket);
            sword.name = "SwitchBlade_Blade";
            foreach (var c in sword.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            if (dedicated)
            {
                sword.transform.localPosition = Vector3.zero;
                sword.transform.localRotation = Quaternion.identity;
            }
            else
            {
                sword.transform.localPosition = new Vector3(0.1178f, 0.0341f, -0.0092f);
                sword.transform.localRotation = Quaternion.Euler(341.3956f, 99.6629f, 276.9453f);
            }
            sword.transform.localScale = Vector3.one * 1.1f;

            var mr = sword.GetComponentInChildren<MeshRenderer>();
            if (mr != null)
            {
                var mat = mr.material;
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.1f, 2.6f, 3.2f));
            }
            var lightGo = new GameObject("Blade_GlowLight");
            lightGo.transform.SetParent(sword.transform, false);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(0.1f, 0.9f, 1f); l.intensity = 2.5f; l.range = 3f;
        }

        // ─────────────────────────────────────────────────────────────────────
        // CUTSCENE
        // ─────────────────────────────────────────────────────────────────────
        private IEnumerator AssassinationCutscene()
        {
            _cutsceneRunning = true;
            HasPlayed = true;

            _cam = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
            if (_cam != null)
            {
                _orbitCam = _cam.GetComponent<SmoothOrbitCamera>();
                if (_orbitCam != null) _orbitCam.enabled = false;
            }

            Cyberpunk.Audio.CyberBackgroundMusic.Instance?.SuspendForMission(1.2f);
            SuppressMission1AndNearbyEnemies();
            SuppressPrologueHUD();

            // Hide gameplay HUD during cinematic cutscene
            SetGameplayHudVisible(false);

            PrepareAlita();
            SpawnIdo();
            SpawnSwitchBlade();
            CreateKeyLight();
            GotGTitleCardUI.GetOrCreate().SetLetterboxOnly(true, 0.45f);

            if (_musicClip != null) { _music.clip = _musicClip; _music.volume = 0.0f; _music.Play(); StartCoroutine(FadeMusic(0.7f, 3f)); }

            Quaternion faceWest = Quaternion.Euler(0f, -90f, 0f);
            Quaternion faceEast = Quaternion.Euler(0f, 90f, 0f);
            _ido.transform.SetPositionAndRotation(IdoPos, faceWest);
            _blade.transform.SetPositionAndRotation(BladeHiddenPos, faceWest);

            // Re-sweep to eliminate any bot instantiated during the first 2 frames of scene boot
            SuppressMission1AndNearbyEnemies();
            SuppressPrologueHUD();

            // [Shot 1] High wide establishing shot of the skybridge, slow descent
            SetCam(new Vector3(-6.5f, 4.2f, -12.5f), LookAt(new Vector3(-6.5f, 4.2f, -12.5f), new Vector3(-0.5f, 0.8f, BridgeZ)), 50f);
            MoveCam(new Vector3(-4.5f, 2.5f, -11.5f), LookAt(new Vector3(-4.5f, 2.5f, -11.5f), new Vector3(-0.5f, 0.9f, BridgeZ)), 44f, 6.5f);
            yield return Say("DR. DYSON IDO", "CYBER-SURGEON & HUNTER-WARRIOR",
                "Alita. Come here. All of Sector 4 is saying your name now. You gave this city something it hasn't had in years.", 5.4f);

            // [Shot 2] Medium side two-shot, framed above railing with neon skyline behind
            // Raised above the railing: the deck is only 2.7m wide, so a low side camera is blocked by the wall.
            Vector3 s2 = new Vector3(-0.55f, 3.0f, -11.8f);
            SetCam(s2, LookAt(s2, new Vector3(-0.55f, 1.1f, BridgeZ)), 50f);
            MoveCam(new Vector3(-0.55f, 3.0f, -11.9f), LookAt(new Vector3(-0.55f, 3.0f, -11.9f), new Vector3(-0.55f, 1.15f, BridgeZ)), 52f, 7f);
            yield return Say("ALITA", "MARTIAN CYBORG", "I only fought. You are the one who rebuilt me, Ido.", 3.6f);
            yield return Say("DR. DYSON IDO", "CYBER-SURGEON & HUNTER-WARRIOR",
                "Listen. The people who made your core are not gone. They answer to one man, a cyborg called SwitchBlade. He runs everything in this city's shadow.", 6.6f);

            // [Shot 3] Over Alita's right shoulder on the bridge deck to Ido; SwitchBlade silhouette deep in background
            Vector3 s3 = new Vector3(-4.3f, 1.6f, -8.15f);
            SetCam(s3, LookAt(s3, new Vector3(2.5f, 1.3f, BridgeZ)), 34f);
            yield return Say("DR. DYSON IDO", "CYBER-SURGEON & HUNTER-WARRIOR",
                "I should have told you sooner. He will come for you now that the whole district knows what you are. Stay close and...", 4.8f);
            if (_idoAnim != null) { /* Ido's idle holds */ }
            if (_sfx != null && _thunderClip != null) _sfx.PlayOneShot(_thunderClip, 0.7f);

            // [Shot 4] Low-angle reveal of SwitchBlade on the bridge deck + title card
            Vector3 s4 = new Vector3(0.3f, 0.75f, -8.1f);
            SetCam(s4, LookAt(s4, new Vector3(4.5f, 1.45f, BridgeZ)), 36f);
            MoveCam(new Vector3(1.0f, 0.9f, -8.1f), LookAt(new Vector3(1.0f, 0.9f, -8.1f), new Vector3(4.5f, 1.45f, BridgeZ)), 32f, 3.4f);
            GotGTitleCardUI.GetOrCreate().ShowTitleCard(
                title: "SWITCHBLADE",
                subtitle: "CYBORG OVERLORD OF THE SYNDICATE",
                details: "SECTOR 7 SKYBRIDGE  //  TARGET: DR. DYSON IDO",
                duration: 2.6f);
            TriggerShake(0.25f, 0.6f);
            yield return new WaitForSecondsRealtime(3.0f);

            // [Shot 5] The dash: SwitchBlade crosses the bridge in a blink with cyber electricity trail
            Vector3 s5 = new Vector3(0.0f, 1.6f, -8.2f);
            SetCam(s5, LookAt(s5, new Vector3(2.2f, 1.3f, BridgeZ)), 34f);
            if (_sfx != null && _dashClip != null) _sfx.PlayOneShot(_dashClip, 1f);
            yield return MoveActor(_blade.transform, BladeHiddenPos, BladeStabPos, 0.28f, true);
            _blade.transform.rotation = faceWest;

            // [Shot 6] The stab: tight side profile on the paired backstab animations
            Vector3 s6 = new Vector3(0.8f, 2.6f, -11.8f);
            SetCam(s6, LookAt(s6, new Vector3(1.75f, 1.0f, BridgeZ)), 34f);
            MoveCam(new Vector3(0.2f, 2.6f, -11.8f), LookAt(new Vector3(0.2f, 2.6f, -11.8f), new Vector3(0.3f, 0.9f, BridgeZ)), 38f, 3.0f);
            if (_bladeAnim != null) _bladeAnim.CrossFadeInFixedTime("Backstab", 0.05f, 0);
            if (_idoAnim != null)
            {
                _idoAnim.applyRootMotion = true; // the victim clip carries Ido's stagger forward
                _idoAnim.CrossFadeInFixedTime("Victim", 0.05f, 0);
            }
            if (_sfx != null && _thrustClip != null) _sfx.PlayOneShot(_thrustClip, 1f);
            yield return new WaitForSecondsRealtime(0.85f);

            // Impact: hit-stop, VFX, shockwave that blindsides Alita
            Vector3 chest = new Vector3(IdoPos.x, BridgeY + 1.25f, BridgeZ);
            SpawnStabEffects(chest);
            TriggerShake(1.7f, 0.7f);
            if (_player != null && _playerAnim != null) _playerAnim.CrossFadeInFixedTime("KnockDown", 0.06f, 0);
            Time.timeScale = 0.18f;
            yield return new WaitForSecondsRealtime(0.6f);
            Time.timeScale = 1f;

            // [Shot 7] Alita's face on the bridge deck as the shockwave floors her
            Vector3 s7 = new Vector3(-1.4f, 2.2f, -11.0f);
            SetCam(s7, LookAt(s7, new Vector3(-2.6f, 0.6f, BridgeZ)), 34f);
            if (_playerAnim != null) _playerAnim.CrossFadeInFixedTime("KnockLoop", 0.15f, 0);
            yield return new WaitForSecondsRealtime(1.5f);

            // [Shot 8] Floor-level on the bridge deck: Ido staggers forward and collapses toward Alita
            Vector3 s8 = new Vector3(-1.0f, 2.7f, -11.6f);
            SetCam(s8, LookAt(s8, new Vector3(-0.7f, 0.3f, BridgeZ)), 38f);
            yield return new WaitForSecondsRealtime(1.2f);
            if (_idoAnim != null) _idoAnim.CrossFadeInFixedTime("Dead", 0.1f, 0);
            yield return Say("DR. DYSON IDO", "CYBER-SURGEON & FATHER",
                "Alita... your core... they can never erase who you are. Run... live... and finish what I could not.", 6.2f);
            if (_idoAnim != null) _idoAnim.speed = 0f; // hold the final collapsed pose
            if (_idoAnim != null) _idoAnim.applyRootMotion = false;

            // [Shot 9] SwitchBlade on the bridge deck: the villain's line, then he vanishes with dash burst
            Vector3 s9 = new Vector3(-0.4f, 1.4f, -8.1f);
            SetCam(s9, LookAt(s9, new Vector3(BladeStabPos.x, 1.5f, BridgeZ)), 32f);
            MoveCam(new Vector3(0.2f, 1.45f, -8.1f), LookAt(new Vector3(0.2f, 1.45f, -8.1f), new Vector3(BladeStabPos.x, 1.5f, BridgeZ)), 28f, 5f);
            if (_bladeAnim != null) _bladeAnim.CrossFadeInFixedTime("Menace", 0.4f, 0);
            yield return Say("SWITCHBLADE", "CYBORG OVERLORD", "The Pit's new champion. A shame about the doctor. Rest, little ghost. Your core is mine, and so is this city.", 6.0f);
            if (_sfx != null && _dashClip != null) _sfx.PlayOneShot(_dashClip, 1f);
            yield return MoveActor(_blade.transform, _blade.transform.position, _blade.transform.position + new Vector3(9f, 0f, 0f), 0.35f, true);
            SpawnElectricBurst(_blade.transform.position + Vector3.up);
            Destroy(_blade);
            _blade = null;

            // [Shot 10] Alita kneels beside Ido, dramatic crane pull-back
            if (_playerAnim != null) _playerAnim.CrossFadeInFixedTime("KnockLoop", 0.2f, 0);
            Vector3 s10 = new Vector3(-1.6f, 2.4f, -10.8f);
            SetCam(s10, LookAt(s10, new Vector3(-1.5f, 0.4f, BridgeZ)), 34f);
            MoveCam(new Vector3(-4.5f, 5.0f, -13.5f), LookAt(new Vector3(-4.5f, 5.0f, -13.5f), new Vector3(-1.5f, 0.3f, BridgeZ)), 48f, 7.0f);
            yield return Say("ALITA", "MARTIAN CYBORG", "Ido... SwitchBlade. I will find you, and I will end you.", 4.2f);
            yield return new WaitForSecondsRealtime(2.0f);

            // Hand back control
            GotGTitleCardUI.GetOrCreate().SetLetterboxOnly(false, 0.7f);
            LinkedIdoBodyCleanup();
            RestorePlayer();
            if (_orbitCam != null) { _orbitCam.enabled = true; _orbitCam.SnapToTarget(); }
            StartCoroutine(FadeMusic(0f, 3f));
            var hud = CyberpunkHUDController.Instance;
            if (hud != null)
            {
                hud.ShowToast("// DR. IDO IS DEAD // FIND SWITCHBLADE", 5f);
                hud.SetQuestObjective("// AVENGE DR. IDO", "> SwitchBlade runs the syndicate that hunts you\n> Track him through the city");
            }
            SetGameplayHudVisible(true);
            if (_keyLight != null) Destroy(_keyLight);
            Cyberpunk.Audio.CyberBackgroundMusic.Instance?.ResumeAfterMission();
            _cutsceneRunning = false;
        }

        // ─────────────────────────────────────────────────────────────────────
        // HELPERS
        // ─────────────────────────────────────────────────────────────────────
        /// <summary>The in-game HUD canvas keeps resetting its CanvasGroup alpha, so toggle the root Canvas itself.</summary>
        private static void SetGameplayHudVisible(bool visible)
        {
            var hud = GameObject.Find("Canvas_InGameHUD");
            if (hud == null) return;
            var canvas = hud.GetComponent<Canvas>();
            if (canvas != null) canvas.enabled = visible;
        }

        private GameObject _keyLight;

        /// <summary>The bridge is dark at street level; a cool fill light keeps both actors readable in every shot.</summary>
        private void CreateKeyLight()
        {
            if (_keyLight != null) Destroy(_keyLight);
            _keyLight = new GameObject("Cutscene_KeyLight");
            _keyLight.transform.position = new Vector3(0.2f, 3.2f, BridgeZ - 1.2f);
            var l = _keyLight.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(0.72f, 0.82f, 1f);
            l.intensity = 6f;
            l.range = 11f;
        }

        private IEnumerator Say(string speaker, string title, string text, float seconds)
        {
            CyberpunkDialogueHUD.GetOrCreate().ShowDialogue(speaker, text, null, title, null, seconds);
            yield return new WaitForSecondsRealtime(seconds + 0.2f);
        }

        private IEnumerator MoveActor(Transform t, Vector3 from, Vector3 to, float duration, bool addTrail = false)
        {
            GameObject trailGo = null;
            if (addTrail && _vfxElectric != null && t != null)
            {
                trailGo = Instantiate(_vfxElectric, t.position + Vector3.up * 0.8f, Quaternion.identity, t);
            }
            float e = 0f;
            while (e < duration && t != null)
            {
                e += Time.unscaledDeltaTime;
                t.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, e / duration));
                yield return null;
            }
            if (t != null) t.position = to;
            if (trailGo != null) Destroy(trailGo, 1.2f);
        }

        private IEnumerator FadeMusic(float target, float duration)
        {
            float start = _music != null ? _music.volume : 0f;
            float e = 0f;
            while (e < duration && _music != null)
            {
                e += Time.unscaledDeltaTime;
                _music.volume = Mathf.Lerp(start, target, e / duration);
                yield return null;
            }
            if (_music != null && target <= 0.001f) _music.Stop();
        }

        private void PrepareAlita()
        {
            if (_player == null) _player = GameObject.FindWithTag("Player");
            if (_player == null) return;

            var synty = _player.GetComponent<SyntySwordLocomotionController>();
            if (synty != null) { synty.inputLocked = true; synty.enabled = false; }
            var insane = _player.GetComponent<InsaneGunSwordController>();
            if (insane != null) { insane.inputLocked = true; insane.enabled = false; }

            var cc = _player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            _player.transform.SetPositionAndRotation(AlitaPos, Quaternion.Euler(0f, 90f, 0f));
            if (cc != null) cc.enabled = true;

            _playerAnim = _player.GetComponentInChildren<Animator>();
            if (_playerAnim != null)
            {
                _playerOriginalController = _playerAnim.runtimeAnimatorController;
                ApplyCutsceneController(_playerAnim);
                _playerAnim.Play("Idle", 0, 0f);
            }
        }

        private void RestorePlayer()
        {
            if (_player == null) return;
            if (_playerAnim != null && _playerOriginalController != null)
            {
                _playerAnim.runtimeAnimatorController = _playerOriginalController;
            }
            var synty = _player.GetComponent<SyntySwordLocomotionController>();
            if (synty != null) { synty.enabled = true; synty.inputLocked = false; }
            var insane = _player.GetComponent<InsaneGunSwordController>();
            if (insane != null) { insane.enabled = true; insane.inputLocked = false; }
        }

        private void LinkedIdoBodyCleanup()
        {
            // Ido's body stays on the bridge, frozen in its final pose, with a thin wisp of smoke.
            if (_ido == null) return;
            _ido.name = "Fallen_Doctor_Ido_Memorial";
            var chest = _idoAnim != null && _idoAnim.isHuman ? _idoAnim.GetBoneTransform(HumanBodyBones.Chest) : _ido.transform;
            if (chest != null)
            {
                var smoke = new GameObject("Ido_Chest_Smoke");
                smoke.transform.SetParent(chest, false);
                var ps = smoke.AddComponent<ParticleSystem>();
                var m = ps.main;
                m.startLifetime = 1.8f; m.startSpeed = 0.4f; m.startSize = 0.22f;
                m.startColor = new Color(0.2f, 0.2f, 0.25f, 0.3f);
                var em = ps.emission; em.rateOverTime = 7f;

                // A ParticleSystem with no material renders as magenta squares; give it a soft unlit URP material.
                var psr = smoke.GetComponent<ParticleSystemRenderer>();
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
                if (psr != null && shader != null)
                {
                    var mat = new Material(shader);
                    mat.SetColor("_BaseColor", new Color(0.25f, 0.25f, 0.3f, 0.35f));
                    psr.sharedMaterial = mat;
                }
            }
        }

        private void SpawnStabEffects(Vector3 pos)
        {
            if (_vfxStab != null) Destroy(Instantiate(_vfxStab, pos, Quaternion.LookRotation(Vector3.left)), 3.5f);
            if (_vfxBlood != null) Destroy(Instantiate(_vfxBlood, pos, Quaternion.identity), 4f);
            SpawnElectricBurst(pos);
            var flash = new GameObject("Stab_LightFlash");
            flash.transform.position = pos;
            var l = flash.AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(1f, 0.15f, 0.1f); l.intensity = 8f; l.range = 6f;
            Destroy(flash, 0.35f);
        }

        private void SpawnElectricBurst(Vector3 pos)
        {
            if (_vfxElectric != null) Destroy(Instantiate(_vfxElectric, pos, Quaternion.identity), 3.5f);
        }

        private static Quaternion LookAt(Vector3 from, Vector3 target)
        {
            Vector3 d = target - from;
            return d.sqrMagnitude < 0.0001f ? Quaternion.identity : Quaternion.LookRotation(d);
        }

        private void SetCam(Vector3 pos, Quaternion rot, float fov)
        {
            if (_camRoutine != null) { StopCoroutine(_camRoutine); _camRoutine = null; }
            _camBase = pos;
            if (_cam == null) return;
            _cam.transform.SetPositionAndRotation(pos + _shakeOffset, rot);
            _cam.fieldOfView = fov;
        }

        private void MoveCam(Vector3 pos, Quaternion rot, float fov, float duration)
        {
            if (_camRoutine != null) StopCoroutine(_camRoutine);
            _camRoutine = StartCoroutine(CamRoutine(pos, rot, fov, duration));
        }

        private IEnumerator CamRoutine(Vector3 endPos, Quaternion endRot, float endFov, float duration)
        {
            if (_cam == null) yield break;
            Vector3 startPos = _camBase;
            Quaternion startRot = _cam.transform.rotation;
            float startFov = _cam.fieldOfView;
            float e = 0f;
            while (e < duration)
            {
                e += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, e / duration);
                _camBase = Vector3.Lerp(startPos, endPos, t);
                _cam.transform.SetPositionAndRotation(_camBase + _shakeOffset, Quaternion.Slerp(startRot, endRot, t));
                _cam.fieldOfView = Mathf.Lerp(startFov, endFov, t);
                yield return null;
            }
            _camBase = endPos;
            _camRoutine = null;
        }

        private void LateUpdate()
        {
            // Keep shake applied even when the camera is held still.
            if (_cutsceneRunning && _cam != null && _camRoutine == null)
            {
                _cam.transform.position = _camBase + _shakeOffset;
            }
        }

        private void TriggerShake(float intensity, float duration)
        {
            if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);
            _shakeRoutine = StartCoroutine(ShakeRoutine(intensity, duration));
        }

        private IEnumerator ShakeRoutine(float intensity, float duration)
        {
            float e = 0f;
            while (e < duration)
            {
                e += Time.unscaledDeltaTime;
                float power = intensity * Mathf.Pow(1f - Mathf.Clamp01(e / duration), 2f);
                _shakeOffset = new Vector3(
                    (Mathf.PerlinNoise(Time.unscaledTime * 45f, 0f) - 0.5f) * 2f * power,
                    (Mathf.PerlinNoise(0f, Time.unscaledTime * 45f) - 0.5f) * 2f * power,
                    0f);
                yield return null;
            }
            _shakeOffset = Vector3.zero;
        }
    }
}
