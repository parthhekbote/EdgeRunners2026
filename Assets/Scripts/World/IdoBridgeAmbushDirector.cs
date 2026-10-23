using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using InsaneGun.Combat;
using SyntyCombined;
using SyntyCombined.Cutscene;
using Cyberpunk.Missions;
using Alita.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Cyberpunk.World
{
    /// <summary>
    /// Marks the bridge used for the Ido ambush. Place one on the bridge deck (centre of the walkable span),
    /// with its blue (forward) axis running along the bridge. Cyberpunk > Ido Bridge > Create Marker From Selection
    /// builds one from the selected bridge object.
    /// </summary>
    public class IdoBridgeMarker : MonoBehaviour
    {
        public float halfLength = 12f;
        public float halfWidth = 2f;

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.2f, 0.3f, 0.9f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.up * 0.1f, new Vector3(halfWidth * 2f, 0.2f, halfLength * 2f));
        }
    }

    /// <summary>
    /// After Alita beats Vulcan and walks out of the Pit, Dr. Ido calls for help from a bridge.
    /// She runs to him, but as she closes in Vulcan appears behind Ido, stabs him and hurls him off the bridge.
    /// Plays once per session in Demo_City; started by PitArenaWarpRunner once Alita is back on the sky terrace.
    /// </summary>
    public class IdoBridgeAmbushDirector : MonoBehaviour
    {
        public static bool HasPlayed { get; private set; }

        /// <summary>True once Ido has fallen: Mission 5 opens the server room door.</summary>
        public static bool Mission5Unlocked { get; private set; }

        private const float TriggerDistance = 13f;
        private const float FallDepth = 70f;

        private Transform _player;
        private InsaneGunSwordController _hero;
        private SyntySwordLocomotionController _loco;
        private Camera _cam;
        private SmoothOrbitCamera _orbit;

        private Vector3 _center, _axis, _side;
        private float _halfLength;
        private GameObject _ido;
        private Animator _idoAnim;
        private GameObject _vulcan;
        private Animator _vulcanAnim;
        private Light _beacon;
        private bool _forceImmediateAmbush = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            HasPlayed = false;
            Mission5Unlocked = false;
        }

        public static void TryBegin()
        {
            if (HasPlayed) return;
            if (SceneManager.GetActiveScene().name != PitArenaDoorTrigger.CitySceneName) return;
            if (!Mission4_VulcanShowdownManager.IsVulcanDefeated) return;
            if (FindAnyObjectByType<IdoBridgeAmbushDirector>() != null) return;

            new GameObject("[Ido_Bridge_Ambush]").AddComponent<IdoBridgeAmbushDirector>();
        }

        public static void ForceBegin(bool immediateAmbush = false)
        {
            HasPlayed = false;
            Mission4_VulcanShowdownManager.IsVulcanDefeated = true;
            var existing = Object.FindObjectsByType<IdoBridgeAmbushDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var e in existing) Destroy(e.gameObject);

            var go = new GameObject("[Ido_Bridge_Ambush]");
            var dir = go.AddComponent<IdoBridgeAmbushDirector>();
            dir._forceImmediateAmbush = immediateAmbush;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnSceneLoadedRuntime()
        {
            SceneManager.sceneLoaded -= OnSceneLoadedCallback;
            SceneManager.sceneLoaded += OnSceneLoadedCallback;

            EnsureHotkeyRunner();
            CheckAutoPlayCutscene();
        }

        private static void OnSceneLoadedCallback(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == PitArenaDoorTrigger.CitySceneName)
            {
                EnsureHotkeyRunner();
                CheckAutoPlayCutscene();
            }
        }

        private static void EnsureHotkeyRunner()
        {
            if (SceneManager.GetActiveScene().name == PitArenaDoorTrigger.CitySceneName)
            {
                if (Object.FindAnyObjectByType<IdoBridgeHotkeyRunner>() == null)
                {
                    var go = new GameObject("[Ido_Bridge_Hotkey_Runner]");
                    go.AddComponent<IdoBridgeHotkeyRunner>();
                    DontDestroyOnLoad(go);
                }
            }
        }

        private static void CheckAutoPlayCutscene()
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorPrefs.GetBool("IdoBridge_AutoPlayCutscene", false))
            {
                bool immediate = UnityEditor.EditorPrefs.GetBool("IdoBridge_ImmediateAmbush", true);
                UnityEditor.EditorPrefs.DeleteKey("IdoBridge_AutoPlayCutscene");
                UnityEditor.EditorPrefs.DeleteKey("IdoBridge_ImmediateAmbush");
                ForceBegin(immediate);
            }
#endif
        }

        private void Start() => StartCoroutine(Run());

        // ── Main sequence ────────────────────────────────────────────────────────

        private IEnumerator Run()
        {
            if (!_forceImmediateAmbush)
            {
                yield return new WaitForSeconds(2.5f); // let the arrival settle after the loading screen
            }
            else
            {
                yield return new WaitForSeconds(0.2f);
            }

            if (!FindPlayer() || !ResolveBridge())
            {
                Debug.LogWarning("[IdoBridge] Aborted: no player or no IdoBridgeMarker in the scene " +
                                 "(use Cyberpunk > Ido Bridge > Create Marker From Selection).");
                Destroy(gameObject);
                yield break;
            }
            HasPlayed = true;

            SpawnIdo();

            if (_forceImmediateAmbush)
            {
                // Teleport Alita to within trigger distance facing Ido for instant cutscene test
                Vector3 idoP = _ido.transform.position;
                float sgn = Vector3.Dot(_player.position - _center, _axis) >= 0f ? -1f : 1f;
                Vector3 toIdoDir = -_axis * sgn;
                Vector3 alitaPos = GroundAt(idoP - toIdoDir * 11.5f);
                var cc = _player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                _player.position = alitaPos;
                Face(_player, toIdoDir);
                Physics.SyncTransforms();
                if (cc != null) cc.enabled = true;

                if (_orbit != null)
                {
                    _orbit.target = _player;
                    _orbit.SnapToTarget();
                }

                yield return new WaitForSeconds(0.25f);
            }
            else
            {
                // Ido's distress call as soon as Alita is out of the arena
                yield return PlayConversation(new List<CyberpunkDialogueHUD.DialogueLineData>
                {
                    Line("DR. DYSON IDO", "CYBER-SURGEON // COMMS", "Alita! Alita, can you hear me?! Thank God, the signal's still holding."),
                    Line("DR. DYSON IDO", "CYBER-SURGEON // COMMS", "I'm on the bridge. Vulcan's people have me pinned and I can't get past them. Please, I need you here!"),
                    Line("DR. DYSON IDO", "CYBER-SURGEON // COMMS", "Hurry, Alita. I don't think I have long."),
                });

                CyberpunkHUDController.Instance?.SetQuestObjective("DR. IDO // DISTRESS CALL", "Reach Dr. Ido on the bridge before it's too late");
                CyberpunkHUDController.Instance?.ShowToast("DR. IDO NEEDS HELP: RUN TO THE BRIDGE", 5f);
            }

            // Wait for Alita to reach him
            while (_player != null && Flat(_player.position - _ido.transform.position).magnitude > TriggerDistance)
                yield return null;

            yield return AmbushCutscene();
            Destroy(gameObject, 1f);
        }

        private IEnumerator AmbushCutscene()
        {
            LockPlayer(true);
            BeginCamera();
            GotGTitleCardUI.GetOrCreate().SetLetterboxOnly(true, 0.45f);

            Vector3 idoPos = _ido.transform.position;
            Vector3 toIdo = Flat(idoPos - _player.position).normalized;
            Face(_ido.transform, -toIdo);
            SetAlitaIdle();

            // Shot 1: behind Alita, Ido ahead on the bridge
            SetCam(_player.position - toIdo * 3.2f + Vector3.up * 2.0f + _side * 1.2f,
                   idoPos + Vector3.up * 1.5f, 50f);
            StartCoroutine(MoveCam(_player.position - toIdo * 1.6f + Vector3.up * 1.75f + _side * 0.8f,
                                   idoPos + Vector3.up * 1.5f, 44f, 2.8f));

            yield return PlayConversation(new List<CyberpunkDialogueHUD.DialogueLineData>
            {
                Line("DR. DYSON IDO", "CYBER-SURGEON // COMMS", "Alita... you came. Stay back, it's a trap! He's—"),
            });

            // Shot 2: Vulcan crashes in behind Ido
            Vector3 behindDir = Flat(idoPos - _player.position).normalized;      // away from Alita
            Vector3 vulcanStart = idoPos + behindDir * 10f + Vector3.up * 0.1f;
            Vector3 vulcanStrike = idoPos + behindDir * 1.35f;
            SpawnVulcan(vulcanStart, -behindDir);

            SetCam(idoPos - behindDir * 4.5f + _side * 3.2f + Vector3.up * 1.4f, idoPos + behindDir * 2.5f + Vector3.up * 1.4f, 52f);
            SpawnFx("FX_Explosion_Large_Dark_01", vulcanStart, 0.5f);
            yield return new WaitForSeconds(0.5f);

            // Vulcan sprints to Ido
            SetVulcanMove(1f);
            float t = 0f;
            const float rush = 0.75f;
            while (t < rush)
            {
                t += Time.deltaTime;
                _vulcan.transform.position = Vector3.Lerp(vulcanStart, vulcanStrike, Mathf.SmoothStep(0f, 1f, t / rush));
                yield return null;
            }
            SetVulcanMove(0f);
            Face(_vulcan.transform, -behindDir);

            // The stab
            if (_vulcanAnim != null)
            {
                if (HasParam(_vulcanAnim, "AttackIndex")) _vulcanAnim.SetInteger("AttackIndex", 4);
                if (HasParam(_vulcanAnim, "Attack")) _vulcanAnim.SetTrigger("Attack");
                _vulcanAnim.CrossFadeInFixedTime("Attack_04", 0.08f);
            }
            Face(_ido.transform, behindDir);
            SetCam(idoPos - behindDir * 3.0f + _side * 2.4f + Vector3.up * 1.2f, idoPos + Vector3.up * 1.3f, 38f);
            yield return new WaitForSeconds(0.28f);

            SpawnFx("FX_Explosion_Large_Dark_01", idoPos + Vector3.up * 1.2f, 0.35f);
            StartCoroutine(ShakeCam(0.35f, 0.5f));
            yield return HitStop(0.18f);
            PlayIdoStaggerPose();

            // Close on Vulcan holding him up
            SetCam(vulcanStrike + _side * 2.2f - behindDir * 1.2f + Vector3.up * 1.7f, vulcanStrike + Vector3.up * 1.9f, 34f);
            yield return PlayConversation(new List<CyberpunkDialogueHUD.DialogueLineData>
            {
                Line("FORGE-LORD VULCAN", "ARENA MASTER", "Your doctor made you. Now watch me unmake him."),
            });

            // Alita's reaction shot
            Vector3 aPos = _player.position;
            SetCam(aPos + toIdo * 1.4f + _side * 0.7f + Vector3.up * 1.55f, aPos + Vector3.up * 1.5f, 36f);
            yield return new WaitForSeconds(0.6f);

            // Wide shot of the throw: Vulcan hurls Ido sideways off the bridge
            Vector3 throwDir = _side * (Vector3.Dot(_side, _player.position - _center) >= 0f ? -1f : 1f);
            SetCam(idoPos - behindDir * 7.5f + Vector3.up * 3.2f, idoPos + Vector3.up * 1.0f, 55f);
            if (_vulcanAnim != null)
            {
                if (HasParam(_vulcanAnim, "AttackIndex")) _vulcanAnim.SetInteger("AttackIndex", 2);
                if (HasParam(_vulcanAnim, "Attack")) _vulcanAnim.SetTrigger("Attack");
                _vulcanAnim.CrossFadeInFixedTime("Attack_02", 0.08f);
            }
            yield return new WaitForSeconds(0.25f);
            yield return ThrowIdo(throwDir);

            // Vulcan dissolves into the forge-fire and leaves
            StartCoroutine(VulcanLeaves(behindDir));

            yield return PlayConversation(new List<CyberpunkDialogueHUD.DialogueLineData>
            {
                Line("ALITA", "BERSERKER UNIT 99", "IDO!!"),
            });

            // Hand control back
            GotGTitleCardUI.GetOrCreate().SetLetterboxOnly(false, 0.5f);
            EndCamera();
            LockPlayer(false);
            if (_beacon != null) Destroy(_beacon.gameObject);

            CyberpunkHUDController.Instance?.ShowToast("DR. IDO HAS FALLEN FROM THE BRIDGE", 6f);
            Mission5Unlocked = true;
            CyberpunkHUDController.Instance?.SetQuestObjective("MISSION 5 // THE SERVER ROOM",
                "Ido's last signal traces to the server room. Reach the door and enter [E]");
        }

        // ── Beats ────────────────────────────────────────────────────────────────

        private IEnumerator ThrowIdo(Vector3 dir)
        {
            Vector3 start = _ido.transform.position;
            Vector3 end = start + dir * 6f + Vector3.down * FallDepth;
            const float dur = 2.4f;
            float t = 0f;
            Vector3 spin = new Vector3(Random.Range(240f, 420f), Random.Range(-90f, 90f), Random.Range(160f, 320f));

            _idoAnim?.SetBool("IsStopped", false);
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = t / dur;
                Vector3 p = Vector3.Lerp(start, end, k * k);          // accelerating fall
                p += dir * (Mathf.Sin(Mathf.Clamp01(k * 3f) * Mathf.PI * 0.5f) * 1.5f);
                p.y += Mathf.Sin(Mathf.Clamp01(k * 4f) * Mathf.PI) * 1.6f; // initial toss
                _ido.transform.position = p;
                _ido.transform.Rotate(spin * Time.deltaTime, Space.Self);

                // Camera tracks the fall
                if (_cam != null)
                {
                    Vector3 look = _ido.transform.position;
                    Quaternion want = Quaternion.LookRotation((look - _cam.transform.position).normalized);
                    _cam.transform.rotation = Quaternion.Slerp(_cam.transform.rotation, want, 6f * Time.deltaTime);
                }
                yield return null;
            }
            Destroy(_ido);
        }

        private IEnumerator VulcanLeaves(Vector3 away)
        {
            if (_vulcan == null) yield break;
            SpawnFx("FX_Explosion_Large_Dark_01", _vulcan.transform.position + Vector3.up, 0.5f);
            SetVulcanMove(1f);
            Face(_vulcan.transform, away);
            float t = 0f;
            while (t < 1.4f && _vulcan != null)
            {
                t += Time.deltaTime;
                _vulcan.transform.position += away * (9f * Time.deltaTime);
                yield return null;
            }
            if (_vulcan != null) Destroy(_vulcan);
        }

        private void PlayIdoStaggerPose()
        {
            if (_ido == null) return;
            if (_idoAnim != null) _idoAnim.speed = 0f;                 // freeze mid-impact
            _ido.transform.rotation = Quaternion.Euler(-14f, _ido.transform.eulerAngles.y, 0f);
        }

        // ── Setup helpers ────────────────────────────────────────────────────────

        private bool FindPlayer()
        {
            var go = GameObject.FindWithTag("Player");
            if (go != null) _player = go.transform;
            if (_player == null)
            {
                _hero = FindAnyObjectByType<InsaneGunSwordController>();
                if (_hero != null) _player = _hero.transform;
            }
            if (_player == null) return false;
            _hero = _player.GetComponent<InsaneGunSwordController>();
            _loco = _player.GetComponent<SyntySwordLocomotionController>();
            return true;
        }

        private bool ResolveBridge()
        {
            var marker = FindAnyObjectByType<IdoBridgeMarker>();
            if (marker == null) return false;

            _axis = Flat(marker.transform.forward).normalized;
            if (_axis.sqrMagnitude < 0.01f) _axis = Vector3.forward;
            _side = Vector3.Cross(Vector3.up, _axis).normalized;
            _halfLength = Mathf.Max(6f, marker.halfLength);

            _center = marker.transform.position;
            return true;
        }

        private void SpawnIdo()
        {
            // Ido waits towards the far end of the bridge from where Alita arrives
            float sign = Vector3.Dot(_player.position - _center, _axis) >= 0f ? -1f : 1f;
            Vector3 pos = GroundAt(_center + _axis * (sign * _halfLength * 0.35f));

            _ido = new GameObject("Doctor_Ido_Bridge");
            _ido.transform.position = pos;
            _ido.transform.rotation = Quaternion.LookRotation(-_axis * sign);

            GameObject prefab = null;
#if UNITY_EDITOR
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/Doctor/Doctor.prefab")
                  ?? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonCyberCity/Prefabs/Characters/SM_Chr_Rich_Male_01.prefab");
#endif
            GameObject model = prefab != null ? Instantiate(prefab, _ido.transform) : GameObject.CreatePrimitive(PrimitiveType.Capsule);
            model.name = "Doctor_Model";
            model.transform.SetParent(_ido.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            foreach (var c in _ido.GetComponentsInChildren<Collider>(true)) c.isTrigger = true;
            var agent = _ido.GetComponentInChildren<UnityEngine.AI.NavMeshAgent>();
            if (agent != null) Destroy(agent);

            _idoAnim = model.GetComponent<Animator>();
#if UNITY_EDITOR
            if (_idoAnim != null && _idoAnim.runtimeAnimatorController == null)
            {
                var ac = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Alita/AC_CyberNPC.controller")
                      ?? AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Alita/AC_Ido_Discovery.controller");
                if (ac != null) _idoAnim.runtimeAnimatorController = ac;
            }
#endif
            if (_idoAnim != null) _idoAnim.applyRootMotion = false;

            // Cyan beacon so the player can find him from the terrace
            var beaconGo = new GameObject("Ido_Beacon");
            beaconGo.transform.SetParent(_ido.transform, false);
            beaconGo.transform.localPosition = Vector3.up * 3.5f;
            _beacon = beaconGo.AddComponent<Light>();
            _beacon.type = LightType.Point;
            _beacon.color = new Color(0.1f, 0.9f, 1f);
            _beacon.intensity = 6f;
            _beacon.range = 14f;
        }

        private void SpawnVulcan(Vector3 pos, Vector3 facing)
        {
            GameObject prefab = null;
#if UNITY_EDITOR
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/SidekickCharacters/Characters/ScifiRobots/ScifiRobots_01/ScifiRobots_01.prefab")
                  ?? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/SidekickCharacters/Characters/ScifiRobots/ScifiRobots_08/ScifiRobots_08.prefab");
#endif
            _vulcan = prefab != null ? Instantiate(prefab, pos, Quaternion.LookRotation(facing)) : GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _vulcan.name = "Vulcan_Bridge_Ambusher";
            _vulcan.transform.position = pos;
            _vulcan.transform.rotation = Quaternion.LookRotation(facing);
            _vulcan.transform.localScale = Vector3.one * 1.35f;

            foreach (var c in _vulcan.GetComponentsInChildren<Collider>(true)) c.enabled = false;

            _vulcanAnim = _vulcan.GetComponent<Animator>();
            if (_vulcanAnim != null)
            {
                _vulcanAnim.applyRootMotion = false;
#if UNITY_EDITOR
                var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/InsaneGun_Sword_Set/AC_InsaneGun_CombatMaster.controller");
                if (ctrl != null) _vulcanAnim.runtimeAnimatorController = ctrl;
#endif
                if (HasParam(_vulcanAnim, "IsSwordDrawn")) _vulcanAnim.SetBool("IsSwordDrawn", true);
            }

            EquipForgeBlade(_vulcan);
            GlowArmor(_vulcan);
        }

        private static void EquipForgeBlade(GameObject boss)
        {
            var anim = boss.GetComponent<Animator>();
            Transform hand = boss.transform;
            if (anim != null && anim.isHuman)
            {
                var r = anim.GetBoneTransform(HumanBodyBones.RightHand);
                if (r != null) hand = r;
            }

            GameObject bladePrefab = null;
#if UNITY_EDITOR
            bladePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonSciFiWorlds/Prefabs/Weapons/Melee/SM_Wep_Sword_05.prefab")
                       ?? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/AnimationSwordCombat/Samples/Prefabs/Wep_Sword_01.prefab");
#endif
            GameObject blade;
            if (bladePrefab != null) blade = Instantiate(bladePrefab, hand);
            else
            {
                blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
                blade.transform.SetParent(hand);
                blade.transform.localScale = new Vector3(0.08f, 1.4f, 0.15f);
                Destroy(blade.GetComponent<Collider>());
            }
            blade.name = "Vulcan_Heavy_Forge_Blade";
            blade.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            blade.transform.localRotation = Quaternion.Euler(0f, 90f, 90f);
            blade.transform.localScale = Vector3.one * 1.28f;

            var mr = blade.GetComponentInChildren<MeshRenderer>();
            if (mr != null)
            {
                var m = mr.material;
                m.EnableKeyword("_EMISSION");
                m.SetColor("_BaseColor", new Color(0.12f, 0.08f, 0.05f));
                m.SetColor("_EmissionColor", new Color(6.5f, 1.4f, 0.1f));
            }

            var l = new GameObject("ForgeBlade_HeatGlow").AddComponent<Light>();
            l.transform.SetParent(blade.transform, false);
            l.type = LightType.Point;
            l.color = new Color(1f, 0.45f, 0.05f);
            l.intensity = 3.5f;
            l.range = 4f;
        }

        private static void GlowArmor(GameObject boss)
        {
            foreach (var r in boss.GetComponentsInChildren<Renderer>())
            {
                if (r == null || r.gameObject.name.Contains("Blade") || r is TrailRenderer) continue;
                foreach (var m in r.materials)
                {
                    if (m == null) continue;
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", new Color(2.8f, 0.65f, 0.05f));
                }
            }
        }

        // ── Camera & control ─────────────────────────────────────────────────────

        private void BeginCamera()
        {
            _cam = Camera.main ?? FindAnyObjectByType<Camera>();
            if (_cam == null) return;
            _orbit = _cam.GetComponent<SmoothOrbitCamera>();
            if (_orbit != null) _orbit.enabled = false;
        }

        private void EndCamera()
        {
            if (_orbit != null)
            {
                _orbit.enabled = true;
                _orbit.target = _player;
                _orbit.tightPositionFollow = true;
                _orbit.SnapToTarget();
            }
        }

        private void SetCam(Vector3 pos, Vector3 lookAt, float fov)
        {
            if (_cam == null) return;
            _cam.transform.position = pos;
            _cam.transform.rotation = Quaternion.LookRotation((lookAt - pos).normalized);
            _cam.fieldOfView = fov;
        }

        private IEnumerator MoveCam(Vector3 toPos, Vector3 lookAt, float toFov, float duration)
        {
            if (_cam == null) yield break;
            Vector3 p0 = _cam.transform.position;
            float f0 = _cam.fieldOfView;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / duration);
                _cam.transform.position = Vector3.Lerp(p0, toPos, k);
                _cam.transform.rotation = Quaternion.LookRotation((lookAt - _cam.transform.position).normalized);
                _cam.fieldOfView = Mathf.Lerp(f0, toFov, k);
                yield return null;
            }
        }

        private IEnumerator ShakeCam(float magnitude, float duration)
        {
            if (_cam == null) yield break;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                _cam.transform.position += Random.insideUnitSphere * (magnitude * (1f - t / duration) * 0.15f);
                yield return null;
            }
        }

        private IEnumerator HitStop(float seconds)
        {
            Time.timeScale = 0.05f;
            yield return new WaitForSecondsRealtime(seconds);
            Time.timeScale = 1f;
        }

        private void LockPlayer(bool locked)
        {
            if (_hero != null) _hero.inputLocked = locked;
            if (_loco != null) _loco.inputLocked = locked;
        }

        private void SetAlitaIdle()
        {
            var a = _player != null ? _player.GetComponent<Animator>() : null;
            if (a != null && HasParam(a, "MoveSpeed")) a.SetFloat("MoveSpeed", 0f);
        }

        private void SetVulcanMove(float v)
        {
            if (_vulcanAnim != null && HasParam(_vulcanAnim, "MoveSpeed")) _vulcanAnim.SetFloat("MoveSpeed", v * 6f);
        }

        // ── Utility ──────────────────────────────────────────────────────────────

        private IEnumerator PlayConversation(List<CyberpunkDialogueHUD.DialogueLineData> lines)
        {
            bool done = false;
            float budget = 4f;
            foreach (var l in lines) budget += l.autoAdvanceTime + 2f;
            CyberpunkDialogueHUD.GetOrCreate().StartConversation(lines, () => done = true);
            float t = 0f;
            while (!done && t < budget)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            yield return new WaitForSeconds(0.25f);
        }

        private static CyberpunkDialogueHUD.DialogueLineData Line(string speaker, string title, string text)
        {
            float auto = Mathf.Clamp(text.Length * 0.055f + 1.6f, 3f, 9f);
            return new CyberpunkDialogueHUD.DialogueLineData(speaker, text, null, title, auto);
        }

        private static bool HasParam(Animator a, string name)
        {
            foreach (var p in a.parameters) if (p.name == name) return true;
            return false;
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        private static void Face(Transform t, Vector3 dir)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f) t.rotation = Quaternion.LookRotation(dir.normalized);
        }

        private Vector3 GroundAt(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 4f, Vector3.down, out var hit, 12f, ~0, QueryTriggerInteraction.Ignore))
                p.y = hit.point.y + 0.02f;
            return p;
        }

        private static void SpawnFx(string prefabName, Vector3 pos, float scale)
        {
#if UNITY_EDITOR
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Synty/PolygonParticles/Prefabs/{prefabName}.prefab");
            if (prefab == null) return;
            var fx = Instantiate(prefab, pos, Quaternion.identity);
            fx.transform.localScale = Vector3.one * scale;
            Destroy(fx, 4f);
#endif
        }
    }

    /// <summary>
    /// Runtime hotkey to trigger the Ido Bridge Ambush cutscene on demand.
    /// Press F9 for Immediate Ambush cutscene, Shift+F9 for Full Distress Call sequence.
    /// </summary>
    public class IdoBridgeHotkeyRunner : MonoBehaviour
    {
        private void Update()
        {
            if (SceneManager.GetActiveScene().name != PitArenaDoorTrigger.CitySceneName) return;

            // F9: Trigger cutscene immediately
            if (Input.GetKeyDown(KeyCode.F9))
            {
                bool immediate = !Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift);
                IdoBridgeAmbushDirector.ForceBegin(immediate);
            }
        }
    }
}
