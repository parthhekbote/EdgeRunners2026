using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using InsaneGun.Combat;
using SyntyCombined;
using SyntyCombined.Cutscene;
using Alita.UI;
using UnityEngine.AI;

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
        private static readonly Vector3 BladeHiddenPos = new Vector3(9.0f, BridgeY, BridgeZ); // beam landing spot at the far end of the deck, faces -X
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
        private AudioSource _impactSrc; // dedicated source so impact is always audible, even in slow-mo
        private AudioClip _musicClip, _thrustClip, _thunderClip, _dashClip;
        // Bridge-battle audio (Assets/Audio/switchblade_bridge_battle/)
        private AudioClip _bridgeBgmClip, _stabSoundClip, _impactSoundClip;
        private GameObject _vfxStab, _vfxBlood, _vfxElectric, _vfxLightning, _vfxSmoke, _vfxFog;
        private readonly List<GameObject> _entryFx = new List<GameObject>();
        private readonly List<GameObject> _hiddenBystanders = new List<GameObject>();
        private readonly Dictionary<Light, float> _lightOriginals = new Dictionary<Light, float>();
        private float _ambientOriginal = -1f;

        private GameObject _ambientIdoBody;

        private static readonly Vector3 HighLiftStationPos = new Vector3(14.28f, 27.08f, 2.50f);

        // ── Approach Pathway & Visual Markers ──────────────────────────────
        private GameObject _tracerObj;
        private LineRenderer _tracerLine;
        private LineRenderer _tracerCoreLine;
        private Material _tracerMaterial;
        private Material _tracerCoreMat;
        private NavMeshPath _tracerNavPath;
        private GameObject _idoMarkerObj;
        private float _lastTracerUpdateTime;
        private Vector3 _lastPlayerTracerPos;
        private Sprite _tracerArrowSprite;

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
            ClearApproachTracer();
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

            // Dedicated source for impact SFX: bypasses listener pause so it cuts through slow-mo
            _impactSrc = gameObject.AddComponent<AudioSource>();
            _impactSrc.spatialBlend = 0f;
            _impactSrc.playOnAwake = false;
            _impactSrc.ignoreListenerPause = true;
            _impactSrc.volume = 1f;

            LoadAssets();
        }

        private void OnDisable()
        {
            Time.timeScale = 1f;
        }

        private void OnDestroy()
        {
            ClearApproachTracer();
            Time.timeScale = 1f;
        }

        private void LoadAssets()
        {
#if UNITY_EDITOR
            _musicClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/vulcan_battle/viramiller-ominous-284554.mp3");
            _thrustClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Combat/Sword_Slash_Piercing_Thrust_01.wav");
            _thunderClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Weather/dragon-studio-dry-thunder-364468.mp3");
            _dashClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Combat/Dash_Cyber_01.wav");

            // Bridge-battle audio – copied from the user-added .mpeg files to proper .mp3 names
            const string bb = "Assets/Audio/switchblade_bridge_battle/";
            _bridgeBgmClip  = AssetDatabase.LoadAssetAtPath<AudioClip>(bb + "bridge_bgm.mp3");
            _stabSoundClip  = AssetDatabase.LoadAssetAtPath<AudioClip>(bb + "stab_sound.mp3");
            _impactSoundClip = AssetDatabase.LoadAssetAtPath<AudioClip>(bb + "impact_sound.mp3");

            if (_bridgeBgmClip  == null) Debug.LogWarning("[IdoAssassination] bridge_bgm.mp3 not found.");
            if (_stabSoundClip  == null) Debug.LogWarning("[IdoAssassination] stab_sound.mp3 not found.");
            if (_impactSoundClip == null) Debug.LogWarning("[IdoAssassination] impact_sound.mp3 not found.");

            _vfxStab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonParticles/Prefabs/FX_SwordStab_01.prefab");
            _vfxBlood = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonParticles/Prefabs/FX_BloodSplat_01.prefab");
            _vfxElectric = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonParticles/Prefabs/FX_Electricity_01.prefab");
            _vfxLightning = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonParticles/Prefabs/FX_LightningStrike_01.prefab");
            _vfxSmoke = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonParticles/Prefabs/FX_Smoke_Black_Large_01.prefab");
            _vfxFog = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonParticles/Prefabs/FX_Fog_Big_01.prefab");
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

            Cyberpunk.World.PitSkyLiftSystem.DeactivateMission3();
            SpawnIdo();
            BuildTracerLine();
            BuildIdoMarker();

            var hud = CyberpunkHUDController.Instance;
            bool isUpper = _player != null && _player.transform.position.y > 15f;
            Vector3 legTarget = isUpper ? HighLiftStationPos : IdoPos;
            if (hud != null)
            {
                if (isUpper)
                {
                    hud.SetQuestObjective("// MEET DR. IDO ON THE SKYBRIDGE", "> Take the Sky Lift down to Sector 7\n> Dr. Ido is waiting on the skybridge");
                }
                else
                {
                    hud.SetQuestObjective("// MEET DR. IDO ON THE SKYBRIDGE", "> Dr. Ido is waiting at Sector 7 Skybridge\n> He has something to tell you");
                }
            }

            UpdateTracerGeometry(legTarget);
        }

        private void Update()
        {
            if (!_approachActive || _cutsceneRunning) return;
            if (_player == null) { _player = GameObject.FindWithTag("Player"); if (_player == null) return; }

            // Animate holographic diamond and marker
            if (_idoMarkerObj != null)
            {
                var diamond = _idoMarkerObj.transform.Find("HoloDiamond");
                if (diamond != null)
                {
                    diamond.Rotate(0f, 0f, 90f * Time.deltaTime, Space.Self);
                    float bob = Mathf.Sin(Time.time * 3.5f) * 0.12f;
                    diamond.localPosition = new Vector3(0f, 2.8f + bob, 0f);
                }
            }

            // Animate line texture flow & breathing core pulse
            if (_tracerMaterial != null)
            {
                _tracerMaterial.mainTextureOffset = new Vector2(-Time.time * 2.2f, 0f);
            }
            if (_tracerCoreMat != null)
            {
                float pulse = 1f + 0.35f * Mathf.Sin(Time.time * 4f);
                _tracerCoreMat.SetColor("_EmissionColor", new Color(0.75f, 1f, 1f) * (4.8f * pulse));
            }

            bool isUpper = _player.transform.position.y > 15f;
            Vector3 legTarget = isUpper ? HighLiftStationPos : IdoPos;
            string legLabel = isUpper ? "TAKE SKY LIFT : TO DR. IDO" : "DR. IDO : SECTOR 7 BRIDGE";

            var ui = Mission1_UIManager.Instance != null ? Mission1_UIManager.Instance : FindAnyObjectByType<Mission1_UIManager>();
            if (ui != null) ui.UpdateWaypointHUD(legTarget, true, legLabel);

            // Recalculate path throttled every 0.15s or on movement > 0.4m
            if (Time.time - _lastTracerUpdateTime > 0.15f || Vector3.Distance(_player.transform.position, _lastPlayerTracerPos) > 0.4f)
            {
                _lastTracerUpdateTime = Time.time;
                _lastPlayerTracerPos = _player.transform.position;
                UpdateTracerGeometry(legTarget);
            }

            float distToIdo = Vector3.Distance(_player.transform.position, IdoPos);
            if (distToIdo <= TriggerRadius)
            {
                _approachActive = false;
                ClearApproachTracer();
                if (ui != null) ui.UpdateWaypointHUD(Vector3.zero, false, null);
                StartCoroutine(AssassinationCutscene());
            }
        }

        private void BuildTracerLine()
        {
            if (_tracerObj != null) Destroy(_tracerObj);
            var existingTracer = GameObject.Find("[Ido_Bridge_NavTracer]");
            if (existingTracer != null) Destroy(existingTracer);

            _tracerObj = new GameObject("[Ido_Bridge_NavTracer]");

            Shader unlit = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                        ?? Shader.Find("Universal Render Pipeline/Unlit")
                        ?? Shader.Find("Sprites/Default");

            // 1. Layer 1: Sleek Outer Directional Cyber Halo Aura
            var ribbonObj = new GameObject("Tracer_Ribbon");
            ribbonObj.transform.SetParent(_tracerObj.transform, false);
            _tracerLine = ribbonObj.AddComponent<LineRenderer>();
            _tracerLine.useWorldSpace = true;
            _tracerLine.startWidth = 0.085f; // Sleek delicate aura
            _tracerLine.endWidth = 0.085f;
            _tracerLine.numCornerVertices = 4;
            _tracerLine.numCapVertices = 4;
            _tracerLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _tracerLine.receiveShadows = false;
            _tracerLine.textureMode = LineTextureMode.Tile;
            _tracerLine.alignment = LineAlignment.View;

            _tracerMaterial = new Material(unlit);
            Color cyanOuter = new Color(0.0f, 0.86f, 1.0f, 0.38f);
            _tracerMaterial.color = cyanOuter;
            if (_tracerMaterial.HasProperty("_BaseColor")) _tracerMaterial.SetColor("_BaseColor", cyanOuter);
            if (_tracerMaterial.HasProperty("_EmissionColor"))
            {
                _tracerMaterial.EnableKeyword("_EMISSION");
                _tracerMaterial.SetColor("_EmissionColor", cyanOuter * 1.8f);
            }

#if UNITY_EDITOR
            _tracerArrowSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/SyntyUI/Sprites/HUD/SPR_HUD_SciFi_Arrow_Clean.png");
#endif
            if (_tracerArrowSprite == null)
            {
                _tracerArrowSprite = Resources.Load<Sprite>("SyntyUI/Sprites/HUD/SPR_HUD_SciFi_Arrow_Clean");
            }
            if (_tracerArrowSprite != null)
            {
                _tracerMaterial.mainTexture = _tracerArrowSprite.texture;
            }
            _tracerLine.sharedMaterial = _tracerMaterial;

            // 2. Layer 2: Ultra-Crisp Razor-Thin Laser Filament Core
            var coreObj = new GameObject("Tracer_Core");
            coreObj.transform.SetParent(_tracerObj.transform, false);
            _tracerCoreLine = coreObj.AddComponent<LineRenderer>();
            _tracerCoreLine.useWorldSpace = true;
            _tracerCoreLine.startWidth = 0.022f; // Razor-thin 2.2cm cyber thread
            _tracerCoreLine.endWidth = 0.022f;
            _tracerCoreLine.numCornerVertices = 4;
            _tracerCoreLine.numCapVertices = 4;
            _tracerCoreLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _tracerCoreLine.receiveShadows = false;
            _tracerCoreLine.alignment = LineAlignment.View;

            _tracerCoreMat = new Material(unlit);
            Color coreCyan = new Color(0.90f, 1.0f, 1.0f, 0.98f);
            _tracerCoreMat.color = coreCyan;
            if (_tracerCoreMat.HasProperty("_BaseColor")) _tracerCoreMat.SetColor("_BaseColor", coreCyan);
            if (_tracerCoreMat.HasProperty("_EmissionColor"))
            {
                _tracerCoreMat.EnableKeyword("_EMISSION");
                _tracerCoreMat.SetColor("_EmissionColor", coreCyan * 3.8f);
            }
            _tracerCoreLine.sharedMaterial = _tracerCoreMat;

            _tracerNavPath = new NavMeshPath();
        }

        private void UpdateTracerGeometry(Vector3 target)
        {
            if (_tracerLine == null || _tracerCoreLine == null || _player == null) return;

            Vector3 startPos = _player.transform.position;
            if (NavMesh.SamplePosition(startPos, out NavMeshHit sHit, 4.0f, NavMesh.AllAreas))
            {
                startPos = sHit.position;
            }

            Vector3 endPos = target;
            if (NavMesh.SamplePosition(endPos, out NavMeshHit eHit, 4.0f, NavMesh.AllAreas))
            {
                endPos = eHit.position;
            }

            if (_tracerNavPath == null) _tracerNavPath = new NavMeshPath();

            bool hasPath = false;
            try
            {
                hasPath = NavMesh.CalculatePath(startPos, endPos, NavMesh.AllAreas, _tracerNavPath);
            }
            catch
            {
                _tracerNavPath = new NavMeshPath();
                try { hasPath = NavMesh.CalculatePath(startPos, endPos, NavMesh.AllAreas, _tracerNavPath); } catch { }
            }

            if (hasPath && _tracerNavPath.status != NavMeshPathStatus.PathInvalid && _tracerNavPath.corners.Length >= 2)
            {
                Vector3[] corners = _tracerNavPath.corners;
                int count = corners.Length;
                _tracerLine.positionCount = count;
                _tracerCoreLine.positionCount = count;

                float totalLength = 0f;
                for (int i = 0; i < count; i++)
                {
                    Vector3 pt = corners[i];
                    pt.y += 0.045f; // Sleek floor elevation
                    _tracerLine.SetPosition(i, pt);
                    _tracerCoreLine.SetPosition(i, pt + Vector3.up * 0.005f);
                    if (i > 0) totalLength += Vector3.Distance(corners[i - 1], corners[i]);
                }
                if (_tracerMaterial != null)
                {
                    _tracerMaterial.mainTextureScale = new Vector2(Mathf.Max(1f, totalLength * 4.0f), 1f);
                }
            }
            else
            {
                _tracerLine.positionCount = 2;
                _tracerCoreLine.positionCount = 2;
                Vector3 p0 = startPos + Vector3.up * 0.045f;
                Vector3 p1 = endPos + Vector3.up * 0.045f;
                _tracerLine.SetPosition(0, p0);
                _tracerLine.SetPosition(1, p1);
                _tracerCoreLine.SetPosition(0, p0 + Vector3.up * 0.005f);
                _tracerCoreLine.SetPosition(1, p1 + Vector3.up * 0.005f);

                if (_tracerMaterial != null)
                {
                    _tracerMaterial.mainTextureScale = new Vector2(Mathf.Max(1f, Vector3.Distance(startPos, endPos) * 4.0f), 1f);
                }
            }
        }

        private void BuildIdoMarker()
        {
            if (_idoMarkerObj != null) Destroy(_idoMarkerObj);
            var existingMarker = GameObject.Find("[Ido_Bridge_WaypointMarker]");
            if (existingMarker != null) Destroy(existingMarker);

            _idoMarkerObj = new GameObject("[Ido_Bridge_WaypointMarker]");
            _idoMarkerObj.transform.position = IdoPos;

            // 1. Floating Holographic Diamond Billboard
            var diamond = GameObject.CreatePrimitive(PrimitiveType.Quad);
            diamond.name = "HoloDiamond";
            diamond.transform.SetParent(_idoMarkerObj.transform, false);
            diamond.transform.localPosition = new Vector3(0f, 2.6f, 0f);
            diamond.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            diamond.transform.localScale = new Vector3(0.42f, 0.42f, 0.42f);
            Destroy(diamond.GetComponent<Collider>());

            Shader unlit = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                        ?? Shader.Find("Universal Render Pipeline/Unlit")
                        ?? Shader.Find("Sprites/Default");
            Material mat = new Material(unlit);
            Color cyan = new Color(0f, 0.95f, 1f, 0.95f);
            mat.color = cyan;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", cyan);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", cyan * 3.0f);
            }
            diamond.GetComponent<Renderer>().sharedMaterial = mat;

            // 2. Cyan Floor Circle Ring
            var ringObj = new GameObject("FloorRing");
            ringObj.transform.SetParent(_idoMarkerObj.transform, false);
            ringObj.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            var ringLine = ringObj.AddComponent<LineRenderer>();
            ringLine.useWorldSpace = false;
            ringLine.startWidth = 0.025f; // Slim aesthetic floor circle
            ringLine.endWidth = 0.025f;
            ringLine.numCornerVertices = 4;
            ringLine.numCapVertices = 4;
            ringLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ringLine.receiveShadows = false;
            int segs = 36;
            ringLine.positionCount = segs;
            float r = 1.4f;
            for (int i = 0; i < segs; i++)
            {
                float a = i * Mathf.PI * 2f / segs;
                ringLine.SetPosition(i, new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
            }
            ringLine.sharedMaterial = mat;

            // 3. Point Light
            var lightObj = new GameObject("MarkerLight");
            lightObj.transform.SetParent(_idoMarkerObj.transform, false);
            lightObj.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            var l = lightObj.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = cyan;
            l.range = 6.0f;
            l.intensity = 2.5f;
        }

        private void ClearApproachTracer()
        {
            if (_tracerObj != null) Destroy(_tracerObj);
            if (_idoMarkerObj != null) Destroy(_idoMarkerObj);
            _tracerObj = null;
            _tracerLine = null;
            _tracerCoreLine = null;
            _idoMarkerObj = null;
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
                // Identity left the blade sticking out along the fingers. This rotation (measured in the live pose)
                // runs the hilt through the fist so the blade leaves from the thumb side and thrusts forward in the backstab.
                sword.transform.localPosition = Vector3.zero;
                sword.transform.localRotation = Quaternion.Euler(358.7f, 267.4f, 173.3f);
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
            ClearApproachTracer();

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
            HideBystanders();
            StartCoroutine(BystanderSweepRoutine());
            GotGTitleCardUI.GetOrCreate().SetLetterboxOnly(true, 0.45f);

            // Pre-load BGM silently so there's no seek delay when it slams in at SwitchBlade's landing.
            // Volume stays at 0 until Shot 4 (entrance impact).
            AudioClip openingMusic = _bridgeBgmClip != null ? _bridgeBgmClip : _musicClip;
            if (openingMusic != null)
            {
                _music.clip = openingMusic;
                _music.volume = 0f;
                _music.Play(); // buffered silently – will fade in at the landing
            }

            Quaternion faceWest = Quaternion.Euler(0f, -90f, 0f);
            Quaternion faceEast = Quaternion.Euler(0f, 90f, 0f);
            _ido.transform.SetPositionAndRotation(IdoPos, faceWest);
            _blade.transform.SetPositionAndRotation(BladeHiddenPos, faceWest);
            _blade.SetActive(false); // SwitchBlade only appears in the thunder entrance

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

            // [Shot 4] THE ENTRANCE: a rumble of thunder, then a beam of light spears down from the clouds; the city
            // goes dark only while the beam is up. SwitchBlade drops down it, lands with a shockwave and walks slowly
            // toward Ido. Cameras stay on the far side of the bridge or in front of him so Ido never blocks the frame.
            Vector3 beamBase = BladeHiddenPos;
            Vector3 c4 = new Vector3(10.5f, 2.0f, -6.4f);
            SetCam(c4, LookAt(c4, new Vector3(beamBase.x, 9.0f, BridgeZ)), 58f);
            if (_sfx != null && _thunderClip != null) _sfx.PlayOneShot(_thunderClip, 0.45f);
            TriggerShake(0.12f, 1.2f);
            yield return new WaitForSecondsRealtime(1.4f);

            // Beam + darkness together
            StartCoroutine(FadeSceneLighting(0.14f, 1.4f));
            CreateDarkness();
            StartCoroutine(FadeDarkness(0.8f, 1.4f));
            var beam = CreateSkyBeam(beamBase);
            StartCoroutine(GrowBeam(beam, 1.0f, 1.4f));
            StartCoroutine(LightningFlash(0.4f, false));
            if (_sfx != null && _thunderClip != null) _sfx.PlayOneShot(_thunderClip, 0.7f);
            // ── Music starts exactly here, as the spotlight pierces down and darkness creeps in ──
            StartCoroutine(FadeMusic(0.75f, 1.6f));
            MoveCam(c4, LookAt(c4, new Vector3(beamBase.x, 1.4f, BridgeZ)), 42f, 3.6f);
            TriggerShake(0.3f, 1.4f);
            yield return new WaitForSecondsRealtime(2.4f);

            // SwitchBlade descends the beam and lands
            _blade.transform.position = beamBase + Vector3.up * 16f;
            _blade.SetActive(true);
            if (_bladeAnim != null) _bladeAnim.Play("Menace", 0, 0f);
            yield return MoveActor(_blade.transform, beamBase + Vector3.up * 16f, beamBase, 0.5f, false, true);

            // Impact: shockwave ring, flash, smoke, screen slam + aftershocks, hit-stop
            SpawnShockwave(beamBase);
            StartCoroutine(LightningFlash(1.0f, true));
            if (_sfx != null && _thunderClip != null) _sfx.PlayOneShot(_thunderClip, 1.0f);
            SpawnEntrySmoke(beamBase);
            SpawnElectricBurst(beamBase + Vector3.up * 0.2f);
            StartCoroutine(ImpactShakeRoutine());
            StartCoroutine(DarknessPulse());
            // Swell music hard on landing to punctuate the hit
            StartCoroutine(FadeMusic(1.0f, 0.2f));
            Time.timeScale = 0.15f;
            yield return new WaitForSecondsRealtime(0.28f);
            Time.timeScale = 1f;
            StartCoroutine(FadeBeam(beam, 1.4f));
            yield return new WaitForSecondsRealtime(1.0f);

            // The walk: slow, heavy, deliberate (7.5s). Animation slowed to match the pace so his feet don't slide.
            const float walkTime = 7.5f;
            Vector3 walkEnd = new Vector3(4.2f, BridgeY, BridgeZ);
            if (_bladeAnim != null) { _bladeAnim.CrossFadeInFixedTime("Walk", 0.3f, 0); _bladeAnim.speed = 0.5f; }
            StartCoroutine(MoveActor(_blade.transform, beamBase, walkEnd, walkTime, false, true));

            // 4a: three-quarter front tracking shot from beyond the railing, so his face reads as he leaves the smoke (4s)
            Vector3 trackA = new Vector3(beamBase.x - 2.2f, 2.0f, -6.5f);
            Vector3 trackB = new Vector3(4.6f, 2.0f, -6.5f);
            SetCam(trackA, LookAt(trackA, new Vector3(beamBase.x, 1.45f, BridgeZ)), 40f);
            MoveCam(trackB, LookAt(trackB, new Vector3(6.4f, 1.45f, BridgeZ)), 36f, 4.0f);
            yield return new WaitForSecondsRealtime(4.0f);

            // 4b: low hero angle straight ahead of him as he closes in (3.5s)
            Vector3 hero = new Vector3(2.5f, 0.55f, -8.35f);
            SetCam(hero, LookAt(hero, new Vector3(6.4f, 1.7f, BridgeZ)), 34f);
            MoveCam(new Vector3(2.7f, 0.6f, -8.35f), LookAt(new Vector3(2.7f, 0.6f, -8.35f), new Vector3(4.6f, 1.6f, BridgeZ)), 30f, 3.5f);
            yield return new WaitForSecondsRealtime(walkTime - 4.0f);
            if (_bladeAnim != null) { _bladeAnim.speed = 1f; _bladeAnim.CrossFadeInFixedTime("Menace", 0.4f, 0); }

            // 4c: TITLE DROP (Vulcan-style): a held shot on his face, the blade ignites, thunder cracks, music swells,
            // and the card slams in for a full beat.
            Vector3 tc = new Vector3(2.4f, 1.3f, -8.55f);
            SetCam(tc, LookAt(tc, new Vector3(walkEnd.x, 1.55f, BridgeZ)), 40f);
            MoveCam(new Vector3(2.8f, 1.35f, -8.6f), LookAt(new Vector3(2.8f, 1.35f, -8.6f), new Vector3(walkEnd.x, 1.6f, BridgeZ)), 32f, 5.5f);
            StartCoroutine(IgniteBlade());
            StartCoroutine(LightningFlash(0.7f, false));
            if (_sfx != null && _thunderClip != null) _sfx.PlayOneShot(_thunderClip, 0.9f);
            TriggerShake(0.5f, 0.6f);
            // Duck BGM under the title card so music breathes; restore after
            StartCoroutine(FadeMusic(1.0f, 1.0f));
            GotGTitleCardUI.GetOrCreate().ShowTitleCard(
                title: "SWITCHBLADE",
                subtitle: "CYBORG OVERLORD OF THE SYNDICATE",
                details: "SECTOR 7 SKYBRIDGE  •  TARGET: DR. DYSON IDO  •  NO WITNESSES",
                duration: 6.0f);
            yield return new WaitForSecondsRealtime(5.0f);

            // Spotlight over: music ducks under dialogue
            StartCoroutine(FadeDarkness(0f, 1.5f));
            StartCoroutine(RestoreSceneLighting(1.5f));
            StartCoroutine(FadeMusic(0.45f, 1.5f)); // duck under dialogue
            yield return Say("SWITCHBLADE", "CYBORG OVERLORD", "Doctor Ido. You hid her well. Not well enough.", 4.2f);

            // [Shot 5] The dash: SwitchBlade crosses the bridge in a blink with cyber electricity trail
            Vector3 s5 = new Vector3(2.4f, 2.4f, -6.4f);
            SetCam(s5, LookAt(s5, new Vector3(2.8f, 1.1f, BridgeZ)), 42f);
            if (_sfx != null && _dashClip != null) _sfx.PlayOneShot(_dashClip, 1f);
            yield return MoveActor(_blade.transform, _blade.transform.position, BladeStabPos, 0.28f, true);
            _blade.transform.rotation = faceWest;

            // [Shot 6] The kill in slow motion: wind-up at full speed, then time drops to 22% through the thrust,
            // the camera creeps in, and the impact lands mid-slow-mo before time snaps back.
            Vector3 s6 = new Vector3(0.8f, 2.6f, -11.8f);
            SetCam(s6, LookAt(s6, new Vector3(1.75f, 1.0f, BridgeZ)), 34f);
            MoveCam(new Vector3(1.2f, 2.0f, -10.9f), LookAt(new Vector3(1.2f, 2.0f, -10.9f), new Vector3(1.7f, 1.1f, BridgeZ)), 26f, 3.2f);
            if (_bladeAnim != null) _bladeAnim.CrossFadeInFixedTime("Backstab", 0.05f, 0);
            if (_idoAnim != null)
            {
                _idoAnim.applyRootMotion = true; // the victim clip carries Ido's stagger forward
                _idoAnim.CrossFadeInFixedTime("Victim", 0.05f, 0);
            }
            // Stab sound (bridge-battle) has priority; fall back to generic thrust SFX
            if (_sfx != null)
            {
                if (_stabSoundClip != null) _sfx.PlayOneShot(_stabSoundClip, 1f);
                else if (_thrustClip != null) _sfx.PlayOneShot(_thrustClip, 1f);
            }
            // BGM sync: swell the music back up as the blade goes in
            StartCoroutine(FadeMusic(0.8f, 0.3f));
            yield return new WaitForSecondsRealtime(0.45f);

            const float slowScale = 0.22f;
            StartCoroutine(TimeScaleRamp(1f, slowScale, 0.15f));
            if (_music != null) _music.pitch = 0.8f;
            yield return new WaitForSecondsRealtime(0.4f / slowScale); // remaining 0.4s of wind-up, played slow

            // Impact, mid slow-mo: blood, sparks, and a shockwave rolling down the bridge toward Alita
            Vector3 chest = new Vector3(IdoPos.x, BridgeY + 1.25f, BridgeZ);
            SpawnStabEffects(chest);
            SpawnShockwave(new Vector3(IdoPos.x, BridgeY, BridgeZ));
            TriggerShake(1.2f, 1.2f);
            // Fire-and-forget temporary AudioSource: bypasses timeScale, shared-source state, and AudioListener
            PlayImpactOnce(_impactSoundClip != null ? _impactSoundClip : _thunderClip);
            yield return new WaitForSecondsRealtime(0.7f);

            // [Shot 7] The shockwave hits Alita: she is thrown to the deck, still in slow motion, time recovering as she falls
            Vector3 s7 = new Vector3(-1.9f, 2.4f, -6.5f);
            SetCam(s7, LookAt(s7, new Vector3(-2.6f, 0.9f, BridgeZ)), 36f);
            MoveCam(new Vector3(-2.1f, 2.1f, -6.7f), LookAt(new Vector3(-2.1f, 2.1f, -6.7f), new Vector3(-2.6f, 0.5f, BridgeZ)), 32f, 2.2f);
            if (_playerAnim != null) _playerAnim.CrossFadeInFixedTime("KnockDown", 0.05f, 0);
            SpawnElectricBurst(AlitaPos + Vector3.up * 1.0f);
            // Music ducks low during Ido's dying words
            StartCoroutine(FadeMusic(0.25f, 0.6f));
            TriggerShake(0.7f, 0.7f);
            StartCoroutine(TimeScaleRamp(slowScale, 1f, 1.4f));
            yield return new WaitForSecondsRealtime(1.6f);
            if (_music != null) _music.pitch = 1f;
            if (_playerAnim != null) _playerAnim.CrossFadeInFixedTime("KnockLoop", 0.2f, 0);
            yield return new WaitForSecondsRealtime(0.6f);

            // [Shot 8] Floor-level on the bridge deck: Ido staggers forward and collapses toward Alita
            Vector3 s8 = new Vector3(-1.0f, 2.7f, -11.6f);
            SetCam(s8, LookAt(s8, new Vector3(-0.7f, 0.3f, BridgeZ)), 38f);
            yield return new WaitForSecondsRealtime(1.2f);
            if (_idoAnim != null) _idoAnim.CrossFadeInFixedTime("Dead", 0.1f, 0);
            yield return Say("DR. DYSON IDO", "CYBER-SURGEON & FATHER",
                "Alita... your core... they can never erase who you are. Run... live... and finish what I could not.", 6.2f);
            if (_idoAnim != null) _idoAnim.speed = 0f; // hold the final collapsed pose
            if (_idoAnim != null) _idoAnim.applyRootMotion = false;

            // [Shot 9] SwitchBlade's line, framed from the front so his face is on screen
            Vector3 s9 = new Vector3(0.6f, 1.45f, -8.45f);
            SetCam(s9, LookAt(s9, new Vector3(BladeStabPos.x, 1.55f, BridgeZ)), 34f);
            MoveCam(new Vector3(0.9f, 1.45f, -8.5f), LookAt(new Vector3(0.9f, 1.45f, -8.5f), new Vector3(BladeStabPos.x, 1.6f, BridgeZ)), 30f, 6f);
            if (_bladeAnim != null) _bladeAnim.CrossFadeInFixedTime("Menace", 0.4f, 0);
            yield return Say("SWITCHBLADE", "CYBORG OVERLORD", "The Pit's new champion. A shame about the doctor. Rest, little ghost. Your core is mine, and so is this city.", 6.0f);

            // Exit: the spotlight returns, the city goes dark again, and he rises up the beam and is gone
            Vector3 exitBase = new Vector3(_blade.transform.position.x, BridgeY, BridgeZ);
            CreateDarkness();
            StartCoroutine(FadeDarkness(0.75f, 1.0f));
            StartCoroutine(FadeSceneLighting(0.14f, 1.0f));
            var exitBeam = CreateSkyBeam(exitBase);
            StartCoroutine(GrowBeam(exitBeam, 0.8f, 1.0f));
            if (_sfx != null && _thunderClip != null) _sfx.PlayOneShot(_thunderClip, 0.8f);
            TriggerShake(0.3f, 1.0f);
            Vector3 s9b = new Vector3(-0.2f, 0.7f, -8.4f);
            SetCam(s9b, LookAt(s9b, new Vector3(exitBase.x, 1.6f, BridgeZ)), 46f);
            MoveCam(s9b, LookAt(s9b, new Vector3(exitBase.x, 9.0f, BridgeZ)), 56f, 2.6f);
            yield return new WaitForSecondsRealtime(1.2f);

            SpawnShockwave(exitBase);
            StartCoroutine(LightningFlash(0.9f, true));
            if (_sfx != null && _thunderClip != null) _sfx.PlayOneShot(_thunderClip, 1.0f);
            StartCoroutine(ImpactShakeRoutine());
            yield return MoveActor(_blade.transform, _blade.transform.position, exitBase + Vector3.up * 18f, 1.2f);
            SpawnElectricBurst(exitBase + Vector3.up * 1.0f);
            Destroy(_blade);
            _blade = null;
            StartCoroutine(FadeBeam(exitBeam, 1.2f));
            StartCoroutine(FadeDarkness(0f, 1.4f));
            StartCoroutine(RestoreSceneLighting(1.4f));
            yield return new WaitForSecondsRealtime(1.2f);

            // [Shot 10] Alita kneels beside Ido, dramatic crane pull-back
            if (_playerAnim != null) _playerAnim.CrossFadeInFixedTime("KnockLoop", 0.2f, 0);
            Vector3 s10 = new Vector3(-1.6f, 2.4f, -10.8f);
            SetCam(s10, LookAt(s10, new Vector3(-1.5f, 0.4f, BridgeZ)), 34f);
            MoveCam(new Vector3(-4.5f, 5.0f, -13.5f), LookAt(new Vector3(-4.5f, 5.0f, -13.5f), new Vector3(-1.5f, 0.3f, BridgeZ)), 48f, 7.0f);
            yield return Say("ALITA", "MARTIAN CYBORG", "Ido... SwitchBlade. I will find you, and I will end you.", 4.2f);
            yield return new WaitForSecondsRealtime(2.0f);

            // Hand back control
            RestoreBystanders();
            StartCoroutine(FadeDarkness(0f, 1.0f, true));
            StartCoroutine(RestoreSceneLighting(1.0f));
            foreach (var fx in _entryFx) if (fx != null) Destroy(fx);
            _entryFx.Clear();
            GotGTitleCardUI.GetOrCreate().SetLetterboxOnly(false, 0.7f);
            LinkedIdoBodyCleanup();
            RestorePlayer();
            if (_orbitCam != null) { _orbitCam.enabled = true; _orbitCam.SnapToTarget(); }
            StartCoroutine(FadeMusic(0f, 3f));
            if (_impactSrc != null) _impactSrc.Stop();
            var hud = CyberpunkHUDController.Instance;
            if (hud != null)
            {
                hud.ShowToast("// ACCESS GRANTED // SERVER ROOM UNLOCKED", 6f);
                hud.SetQuestObjective("// MISSION 5: THE SERVER ROOM", "> Dr. Ido's terminal is online\n> Follow the cyber trail to the Server Room");
            }
            SetGameplayHudVisible(true);
            Cyberpunk.World.IdoBridgeAmbushDirector.UnlockMission5();

            // Aesthetic holographic popup informing player that Server Room is unlocked
            ServerRoomUnlockNotificationUI.Show();

            // Aesthetic dual-layer cyber trial pathway navigation leading to the Server Room
            ServerRoomNavTracer.Begin();

            if (_keyLight != null) Destroy(_keyLight);
            Cyberpunk.Audio.CyberBackgroundMusic.Instance?.ResumeAfterMission();
            _cutsceneRunning = false;
        }

        // ─────────────────────────────────────────────────────────────────────
        // HELPERS
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Spawns a completely isolated AudioSource for the impact clip.
        /// Using a fresh GameObject guarantees the clip is heard regardless of
        /// Time.timeScale, AudioListener.pause, or the state of any shared source.
        /// The object self-destructs after the clip length (+ 0.3s headroom).
        /// </summary>
        private void PlayImpactOnce(AudioClip clip)
        {
            if (clip == null) return;
            var go = new GameObject("_ImpactOneShot");
            DontDestroyOnLoad(go); // survive scene reloads mid-cutscene
            var src = go.AddComponent<AudioSource>();
            src.spatialBlend = 0f;
            src.volume = 1f;
            src.priority = 0;               // highest priority in Unity's mixer
            src.ignoreListenerPause = true;  // not silenced by AudioListener.pause
            src.ignoreListenerVolume = false;
            src.velocityUpdateMode = AudioVelocityUpdateMode.Fixed; // immune to timeScale
            src.clip = clip;
            src.Play();
            Destroy(go, clip.length + 0.3f);
        }
        /// <summary>The in-game HUD canvas keeps resetting its CanvasGroup alpha, so toggle the root Canvas itself.</summary>
        private static void SetGameplayHudVisible(bool visible)
        {
            // Gameplay HUD plus the always-on BACKPACK bar from the inventory UI.
            foreach (var name in new[] { "Canvas_InGameHUD", "Canvas_Hub_Inventory" })
            {
                var go = GameObject.Find(name);
                var canvas = go != null ? go.GetComponent<Canvas>() : null;
                if (canvas != null) canvas.enabled = visible;
            }
        }

        // ── Entrance atmosphere ──────────────────────────────────────────────

        private void SpawnEntrySmoke(Vector3 pos)
        {
            foreach (var prefab in new[] { _vfxSmoke }) // the big fog prefab washed out the whole frame
            {
                if (prefab == null) continue;
                var go = Instantiate(prefab, pos, Quaternion.identity);
                go.transform.localScale = Vector3.one * 1.2f;
                _entryFx.Add(go);
            }
        }

        // ── Villain-entrance darkness: vignette overlay + (when available) URP post-processing ──

        private GameObject _darkRoot;
        private CanvasGroup _darkGroup;
        private Volume _darkVolume;
        private float _darkLevel;
        private bool _camPostWasOn;

        private void CreateDarkness()
        {
            if (_darkRoot != null) return;

            // 1) Screen-space vignette + tint (always works, sits under the letterbox bars and dialogue)
            _darkRoot = new GameObject("Cutscene_DarkOverlay");
            var canvas = _darkRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;
            _darkGroup = _darkRoot.AddComponent<CanvasGroup>();
            _darkGroup.alpha = 0f;
            _darkGroup.blocksRaycasts = false;
            _darkGroup.interactable = false;

            var tint = new GameObject("Tint", typeof(RectTransform)).AddComponent<Image>();
            tint.transform.SetParent(_darkRoot.transform, false);
            tint.color = new Color(0.01f, 0.02f, 0.05f, 0.32f);
            tint.raycastTarget = false;
            Stretch(tint.rectTransform);

            var vig = new GameObject("Vignette", typeof(RectTransform)).AddComponent<RawImage>();
            vig.transform.SetParent(_darkRoot.transform, false);
            vig.texture = BuildVignetteTexture();
            vig.raycastTarget = false;
            Stretch(vig.rectTransform);

            // 2) URP post-processing: crushed exposure, desaturation, cold tint, vignette, aberration
            try
            {
                var go = new GameObject("Cutscene_DarkVolume");
                _darkVolume = go.AddComponent<Volume>();
                _darkVolume.isGlobal = true;
                _darkVolume.priority = 100f;
                _darkVolume.weight = 0f;
                var profile = ScriptableObject.CreateInstance<VolumeProfile>();
                var v = profile.Add<Vignette>(true);
                v.intensity.Override(0.5f); v.smoothness.Override(0.7f); v.color.Override(Color.black);
                var ca = profile.Add<ColorAdjustments>(true);
                ca.postExposure.Override(-1.1f); ca.saturation.Override(-30f); ca.contrast.Override(25f);
                ca.colorFilter.Override(new Color(0.72f, 0.84f, 1f));
                var ch = profile.Add<ChromaticAberration>(true);
                ch.intensity.Override(0.3f);
                _darkVolume.sharedProfile = profile;

                if (_cam != null)
                {
                    var data = _cam.GetUniversalAdditionalCameraData();
                    if (data != null) { _camPostWasOn = data.renderPostProcessing; data.renderPostProcessing = true; }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[IdoAssassination] Post-processing darkness unavailable, using overlay only: " + ex.Message);
            }
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private static Texture2D BuildVignetteTexture()
        {
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = (x / (n - 1f)) * 2f - 1f;
                    float dy = (y / (n - 1f)) * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.2f;
                    float a = Mathf.SmoothStep(0.25f, 1.0f, d) * 0.92f;
                    tex.SetPixel(x, y, new Color(0f, 0f, 0f, a));
                }
            }
            tex.Apply();
            return tex;
        }

        private void SetDarkness(float k)
        {
            _darkLevel = k;
            if (_darkGroup != null) _darkGroup.alpha = k;
            if (_darkVolume != null) _darkVolume.weight = Mathf.Clamp01(k);
        }

        private IEnumerator FadeDarkness(float target, float duration, bool destroyAfter = false)
        {
            float start = _darkLevel;
            float e = 0f;
            while (e < duration)
            {
                e += Time.unscaledDeltaTime;
                SetDarkness(Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, e / duration)));
                yield return null;
            }
            SetDarkness(target);
            if (destroyAfter) DestroyDarkness();
        }

        /// <summary>On impact the screen slams to near-black for a beat, then settles back to the villain mood.</summary>
        private IEnumerator DarknessPulse()
        {
            float hold = _darkLevel;
            SetDarkness(1f);
            yield return new WaitForSecondsRealtime(0.18f);
            float e = 0f;
            while (e < 0.9f)
            {
                e += Time.unscaledDeltaTime;
                SetDarkness(Mathf.Lerp(1f, Mathf.Max(hold, 0.8f), Mathf.SmoothStep(0f, 1f, e / 0.9f)));
                yield return null;
            }
        }

        private void DestroyDarkness()
        {
            if (_darkRoot != null) Destroy(_darkRoot);
            if (_darkVolume != null) Destroy(_darkVolume.gameObject);
            _darkRoot = null; _darkGroup = null; _darkVolume = null;
            if (_cam != null)
            {
                var data = _cam.GetUniversalAdditionalCameraData();
                if (data != null) data.renderPostProcessing = _camPostWasOn;
            }
        }

        /// <summary>Landing impact: a hard slam followed by decaying aftershocks.</summary>
        private IEnumerator ImpactShakeRoutine()
        {
            TriggerShake(1.4f, 0.55f);
            yield return new WaitForSecondsRealtime(0.5f);
            TriggerShake(0.8f, 0.7f);
            yield return new WaitForSecondsRealtime(0.65f);
            TriggerShake(0.4f, 0.9f);
        }

        /// <summary>Title-drop beat: the blade flares bright with an electric burst, then settles to a hotter glow.</summary>
        private IEnumerator IgniteBlade()
        {
            if (_blade == null) yield break;
            var sword = FindChildRecursive(_blade.transform, "SwitchBlade_Blade");
            if (sword == null) yield break;
            var mr = sword.GetComponentInChildren<MeshRenderer>();
            var l = sword.GetComponentInChildren<Light>();
            SpawnElectricBurst(sword.position);
            Color baseEmit = new Color(0.1f, 2.6f, 3.2f);
            float e = 0f;
            while (e < 1.2f && sword != null)
            {
                e += Time.unscaledDeltaTime;
                float k = e < 0.25f ? e / 0.25f : Mathf.Lerp(1f, 0.55f, (e - 0.25f) / 0.95f);
                if (mr != null) mr.material.SetColor("_EmissionColor", baseEmit * (1f + 3f * k));
                if (l != null) { l.intensity = 2.5f + 10f * k; l.range = 3f + 3f * k; }
                yield return null;
            }
        }

        private IEnumerator TimeScaleRamp(float from, float to, float duration)
        {
            float e = 0f;
            while (e < duration)
            {
                e += Time.unscaledDeltaTime;
                Time.timeScale = Mathf.Lerp(from, to, Mathf.Clamp01(e / duration));
                yield return null;
            }
            Time.timeScale = to;
        }

        private static Material TransparentGlow(Color color)
        {
            var shader = Shader.Find("Sprites/Default");
            var m = new Material(shader);
            m.color = color;
            return m;
        }

        private GameObject CreateSkyBeam(Vector3 basePos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Cutscene_SkyBeam";
            Destroy(go.GetComponent<Collider>());
            go.transform.position = basePos + Vector3.up * 30f;
            go.transform.localScale = new Vector3(0.05f, 30f, 0.05f);
            go.GetComponent<Renderer>().material = TransparentGlow(new Color(0.35f, 0.95f, 1f, 0.55f));
            var lightGo = new GameObject("Cutscene_BeamLight");
            lightGo.transform.SetParent(go.transform, false);
            lightGo.transform.position = basePos + Vector3.up * 1.5f;
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(0.35f, 0.95f, 1f); l.range = 16f; l.intensity = 0f;
            _entryFx.Add(go);
            return go;
        }

        private IEnumerator GrowBeam(GameObject beam, float radius, float duration)
        {
            if (beam == null) yield break;
            var r = beam.GetComponent<Renderer>();
            var l = beam.GetComponentInChildren<Light>();
            float e = 0f;
            while (e < duration && beam != null)
            {
                e += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(e / duration);
                float d = Mathf.Lerp(0.05f, radius * 2f, k * k);
                beam.transform.localScale = new Vector3(d, 30f, d);
                float flicker = 0.8f + 0.2f * Mathf.PerlinNoise(Time.unscaledTime * 30f, 0f);
                if (r != null) r.material.color = new Color(0.35f, 0.95f, 1f, 0.55f * flicker);
                if (l != null) l.intensity = 14f * k * flicker;
                yield return null;
            }
        }

        private IEnumerator FadeBeam(GameObject beam, float duration)
        {
            if (beam == null) yield break;
            var r = beam.GetComponent<Renderer>();
            var l = beam.GetComponentInChildren<Light>();
            float e = 0f;
            while (e < duration && beam != null)
            {
                e += Time.unscaledDeltaTime;
                float k = 1f - Mathf.Clamp01(e / duration);
                if (r != null) r.material.color = new Color(0.35f, 0.95f, 1f, 0.55f * k);
                if (l != null) l.intensity = 14f * k;
                yield return null;
            }
            if (beam != null) Destroy(beam);
        }

        private void SpawnShockwave(Vector3 pos)
        {
            StartCoroutine(ShockwaveRoutine(pos));
        }

        private IEnumerator ShockwaveRoutine(Vector3 pos)
        {
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Cutscene_Shockwave";
            Destroy(ring.GetComponent<Collider>());
            ring.transform.position = pos + Vector3.up * 0.12f;
            var r = ring.GetComponent<Renderer>();
            r.material = TransparentGlow(new Color(0.5f, 1f, 1f, 0.7f));
            var flash = new GameObject("Cutscene_ImpactFlash");
            flash.transform.position = pos + Vector3.up * 0.6f;
            var l = flash.AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(0.5f, 0.95f, 1f); l.range = 14f; l.intensity = 20f;
            float dur = 0.7f, e = 0f;
            while (e < dur)
            {
                e += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(e / dur);
                float d = Mathf.Lerp(0.6f, 13f, 1f - (1f - k) * (1f - k));
                ring.transform.localScale = new Vector3(d, 0.04f, d);
                r.material.color = new Color(0.5f, 1f, 1f, 0.7f * (1f - k));
                l.intensity = 20f * (1f - k);
                yield return null;
            }
            Destroy(ring);
            Destroy(flash);
        }

        private IEnumerator LightningFlash(float peak, bool strike)
        {
            Vector3 pos = BladeHiddenPos + new Vector3(-1f, 7f, 0f);
            var go = new GameObject("Cutscene_Lightning");
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(0.72f, 0.84f, 1f);
            l.range = 70f;
            l.intensity = 0f;
            if (strike && _vfxLightning != null)
            {
                var bolt = Instantiate(_vfxLightning, BladeHiddenPos, Quaternion.identity);
                Destroy(bolt, 4f);
            }
            float[] pattern = { 1f, 0.15f, 0.85f, 0.25f, 0.6f, 0.05f, 0f };
            foreach (float f in pattern)
            {
                l.intensity = f * peak * 60f;
                yield return new WaitForSecondsRealtime(0.06f);
            }
            Destroy(go);
        }

        private IEnumerator FadeSceneLighting(float factor, float duration)
        {
            if (_lightOriginals.Count == 0)
            {
                foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                {
                    if (l == null || l.gameObject == _keyLight) continue;
                    if (l.name.StartsWith("Cutscene_") || l.name.StartsWith("Stab_") || l.name.StartsWith("Blade_")) continue;
                    if (_ido != null && l.transform.IsChildOf(_ido.transform)) continue;
                    if (_blade != null && l.transform.IsChildOf(_blade.transform)) continue;
                    if (_player != null && l.transform.IsChildOf(_player.transform)) continue;
                    _lightOriginals[l] = l.intensity;
                }
                _ambientOriginal = RenderSettings.ambientIntensity;
            }
            float e = 0f;
            while (e < duration)
            {
                e += Time.unscaledDeltaTime;
                ApplyLightingFactor(Mathf.Lerp(1f, factor, Mathf.SmoothStep(0f, 1f, e / duration)));
                yield return null;
            }
            ApplyLightingFactor(factor);
        }

        private IEnumerator RestoreSceneLighting(float duration)
        {
            float e = 0f;
            float start = _lightOriginals.Count > 0 ? GetCurrentFactor() : 1f;
            while (e < duration)
            {
                e += Time.unscaledDeltaTime;
                ApplyLightingFactor(Mathf.Lerp(start, 1f, e / duration));
                yield return null;
            }
            ApplyLightingFactor(1f);
            _lightOriginals.Clear();
        }

        private float _currentLightFactor = 1f;
        private float GetCurrentFactor() => _currentLightFactor;

        private void ApplyLightingFactor(float f)
        {
            _currentLightFactor = f;
            foreach (var kv in _lightOriginals)
            {
                if (kv.Key != null) kv.Key.intensity = kv.Value * f;
            }
            if (_ambientOriginal >= 0f) RenderSettings.ambientIntensity = _ambientOriginal * Mathf.Max(f, 0.25f);
        }

        // ── Keep passers-by out of the shot ─────────────────────────────────

        private void HideBystanders()
        {
            foreach (var npc in FindObjectsByType<PolygonCyberCity.NPC.Spawner.CyberNPCController>(FindObjectsSortMode.None))
            {
                if (npc == null || !npc.gameObject.activeInHierarchy) continue;
                if (Vector3.Distance(npc.transform.position, IdoPos) > 90f) continue;
                _hiddenBystanders.Add(npc.gameObject);
                npc.gameObject.SetActive(false);
            }
        }

        private IEnumerator BystanderSweepRoutine()
        {
            while (_cutsceneRunning)
            {
                HideBystanders();
                yield return new WaitForSecondsRealtime(0.5f);
            }
        }

        private void RestoreBystanders()
        {
            foreach (var go in _hiddenBystanders) if (go != null) go.SetActive(true);
            _hiddenBystanders.Clear();
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

        private IEnumerator MoveActor(Transform t, Vector3 from, Vector3 to, float duration, bool addTrail = false, bool linear = false)
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
                float k = Mathf.Clamp01(e / duration);
                t.position = Vector3.Lerp(from, to, linear ? k : Mathf.SmoothStep(0f, 1f, k));
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
