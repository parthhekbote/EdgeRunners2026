using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Alita;
using InsaneGun.Combat;
using SyntyCombined;

namespace Cyberpunk.World
{
    /// <summary>
    /// MISSION 3 PHASE 1 - Far Upper-City Sky Lift System in Demo_City.
    /// Connects the lower district alley (where the M2 Scalper getaway van is parked)
    /// to the far upper-city sky terrace plaza 48 meters above.
    /// 
    /// Stations:
    /// - LOW:  (-14.83, -20.92, 8.17), yaw 90, prompt: [E] SKY LIFT TO UPPER PLAZA
    /// - HIGH: (14.50,  27.05, -1.25), yaw 270, prompt: [E] DESCEND TO LOWER ALLEY
    /// 
    /// Features:
    /// - Luminous neon ground holo-rings and vertical hydraulic energy beams
    /// - 10m golden LineRenderer guide tracer connecting HIGH lift to the Pit Sky Door
    /// - CharacterController-safe instant vertical ascension/descent
    /// - CyberpunkLoadingScreen integration ("SKY LIFT // ASCENDING 48M")
    /// - NavMesh.SamplePosition Y verification and camera auto-alignment
    /// </summary>
    public class PitSkyLiftSystem : MonoBehaviour
    {
        public static PitSkyLiftSystem Instance { get; private set; }

        public const string CitySceneName = "Demo_City";

        [Header("Station Coordinates")]
        public static readonly Vector3 LowStationPos = new Vector3(-14.83f, -20.92f, 8.17f);
        public static readonly float LowStationYaw = 90f;

        public static readonly Vector3 HighStationPos = new Vector3(14.50f, 27.05f, -1.25f);
        public static readonly float HighStationYaw = 90f;

        public static readonly Vector3 SkyDoorPos = new Vector3(18.25f, 27.05f, -1.25f);

        public static bool IsMission3Active = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            IsMission3Active = false;
            Instance = null;
            PlayerPrefs.DeleteKey("Mission3_Active");
        }

        public static void ActivateMission3()
        {
            IsMission3Active = true;
            if (Instance != null && Instance._minimapBlipRect != null)
            {
                Instance._minimapBlipRect.gameObject.SetActive(true);
            }
        }

        public static void DeactivateMission3()
        {
            IsMission3Active = false;
            if (Instance != null)
            {
                if (Instance._gpsFloorLine != null)
                    Instance._gpsFloorLine.positionCount = 0;
                if (Instance._goldTracerLine != null)
                    Instance._goldTracerLine.positionCount = 0;
                if (Instance._minimapBlipRect != null)
                    Instance._minimapBlipRect.gameObject.SetActive(false);
            }
        }

        [Header("Tuning")]
        public float interactionRadius = 2.8f;
        public float warpCooldown = 2.0f;
        public KeyCode interactKey = KeyCode.E;

        // Visual Roots
        private GameObject _lowStationObj;
        private GameObject _highStationObj;
        private LineRenderer _goldTracerLine;

        // Minimap & Waypoint Marker state
        private RectTransform _minimapBlipRect;
        private Image _minimapBlipImage;
        private Image _minimapInnerIcon;
        private LineRenderer _gpsFloorLine;
        private NavMeshPath _gpsNavPath;
        private float _lastGpsUpdate = -10f;

        // Runtime state
        private Transform _playerTransform;
        private bool _isWarpping = false;
        private float _lastWarpTime = -10f;
        private int _activeStationIndex = -1; // 0 = LOW, 1 = HIGH, -1 = None

        // UI Styles
        private GUIStyle _hudBoxStyle;
        private GUIStyle _hudTitleStyle;
        private GUIStyle _hudSubStyle;
        private Texture2D _hudBgTex;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            CheckBootstrap(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            CheckBootstrap(scene);
        }

        private static void CheckBootstrap(Scene scene)
        {
            if (scene.name != CitySceneName) return;

            if (Instance == null && FindAnyObjectByType<PitSkyLiftSystem>() == null)
            {
                var go = new GameObject("PitSkyLiftSystem_Runtime");
                go.AddComponent<PitSkyLiftSystem>();
                Debug.Log("[PitSkyLift] Bootstrapped Far Upper-City Sky Lift in Demo_City.");
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            bool isPostPit = Cyberpunk.World.PitArenaDoorTrigger.IsReturningFromPit ||
                             Cyberpunk.Missions.Mission4_VulcanShowdownManager.IsVulcanDefeated ||
                             Cyberpunk.Missions.Mission4_VulcanShowdownManager.PendingIdoReputationMessage ||
                             Cyberpunk.Missions.IdoAssassinationDirector.Instance != null ||
                             Cyberpunk.Missions.IdoAssassinationDirector.HasPlayed;

            if (!isPostPit &&
                (Cyberpunk.Inventory.CyberpunkInventory.Instance != null && Cyberpunk.Inventory.CyberpunkInventory.Instance.CountOf("arena_pass") > 0))
            {
                IsMission3Active = true;
            }
            else
            {
                IsMission3Active = false;
            }

            BuildGUIStyles();
            SpawnStationsVisuals();
            BuildGoldTracer();
            BuildGpsTracer();
            SetupMinimapBlip();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_hudBgTex != null) Destroy(_hudBgTex);
            if (_minimapBlipRect != null) Destroy(_minimapBlipRect.gameObject);
            if (_gpsFloorLine != null) Destroy(_gpsFloorLine.gameObject);
        }

        private void Start()
        {
            LocatePlayer();
        }

        private void LocatePlayer()
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null) { _playerTransform = p.transform; return; }

            var hero = FindAnyObjectByType<InsaneGunSwordController>();
            if (hero != null) { _playerTransform = hero.transform; return; }

            var loco = FindAnyObjectByType<SyntySwordLocomotionController>();
            if (loco != null) _playerTransform = loco.transform;
        }

        // ─────────────────────────────────────────────────────────────────────
        // RUNTIME VISUALS: HOLO-RINGS, SHAFT BEAMS, LIGHTS & TRACER
        // ─────────────────────────────────────────────────────────────────────
        private void SpawnStationsVisuals()
        {
            // 1. LOW Station at alley (-14.83, -20.92, 8.17)
            _lowStationObj = new GameObject("SkyLift_Station_LOW");
            _lowStationObj.transform.SetParent(transform);
            _lowStationObj.transform.position = LowStationPos;
            CreateStationVisuals(_lowStationObj, new Color(0f, 0.92f, 1f, 0.95f), "SKY LIFT // UPPER PLAZA", true);

            // 2. HIGH Station at terrace (14.50, 27.05, -1.25)
            _highStationObj = new GameObject("SkyLift_Station_HIGH");
            _highStationObj.transform.SetParent(transform);
            _highStationObj.transform.position = HighStationPos;
            CreateStationVisuals(_highStationObj, new Color(1f, 0.78f, 0.15f, 0.95f), "SKY LIFT // LOWER ALLEY", false);

            // 3. Spawn architectural elevator backdrop props if available
            TrySpawnElevatorBackdrop(LowStationPos, LowStationYaw, "LowElevatorBackdrop");
            TrySpawnElevatorBackdrop(HighStationPos, HighStationYaw, "HighElevatorBackdrop");

            // 4. Ensure solid floor under high lift pad and unobstructed walkway onto terrace
            SetupLiftPadAndOpening();
        }

        private void SetupLiftPadAndOpening()
        {
            // A. Spawn solid floor tile under HIGH lift station at (14.50, 27.05, -1.25)
            GameObject floorPrefab = null;
#if UNITY_EDITOR
            floorPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonCyberCity/Prefabs/Buildings/Base/SM_Bld_Base_Floor_Combined_01.prefab");
#endif
            if (floorPrefab != null)
            {
                // Mesh pivot offset: pos = center + (1.25, 0.04, -1.25)
                Vector3 padCenter = new Vector3(13.75f, 27.00f, -1.25f);
                Vector3 padPos = new Vector3(padCenter.x + 1.25f, padCenter.y + 0.04f, padCenter.z - 1.25f);
                var pad = Instantiate(floorPrefab, padPos, Quaternion.identity, _highStationObj.transform);
                pad.name = "HighLift_LandingPad_Floor";
            }
            else
            {
                var pad = new GameObject("HighLift_LandingPad_FloorCollider");
                pad.transform.SetParent(_highStationObj.transform);
                pad.transform.position = new Vector3(13.75f, 27.00f, -1.25f);
                var box = pad.AddComponent<BoxCollider>();
                box.size = new Vector3(3.0f, 0.15f, 3.0f);
            }

            // B. Set railing colliders at X=15.0 to triggers so player can step seamlessly onto the terrace
            var rCols = Physics.OverlapSphere(new Vector3(14.85f, 27.5f, -1.25f), 0.7f);
            foreach (var c in rCols)
            {
                if (c.name.Contains("Railing") || c.name.Contains("Rail"))
                {
                    c.isTrigger = true;
                }
            }
        }

        private void TrySpawnElevatorBackdrop(Vector3 pos, float yaw, string objName)
        {
            GameObject prefab = null;
#if UNITY_EDITOR
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonSciFiWorlds/Prefabs/Buildings/SM_Bld_Elevator_Hover_Single_01.prefab")
                  ?? UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonCyberCity/Prefabs/Buildings/SM_Bld_Elevator_01.prefab");
#endif
            if (prefab == null) return;

            // Offset 1.8m behind the player pad
            Vector3 offset = Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0f, -1.9f);
            var inst = Instantiate(prefab, pos + offset, Quaternion.Euler(0f, yaw, 0f), transform);
            inst.name = objName;

            // Set all colliders to triggers so it never blocks the player
            var cols = inst.GetComponentsInChildren<Collider>(true);
            foreach (var c in cols) c.isTrigger = true;
        }

        private void CreateStationVisuals(GameObject parent, Color themeColor, string label, bool isLow)
        {
            // A. Glowing Ground Ring
            var ringObj = new GameObject("HoloRing_Platform");
            ringObj.transform.SetParent(parent.transform, false);
            ringObj.transform.localPosition = new Vector3(0f, 0.05f, 0f);

            var line = ringObj.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.startWidth = 0.14f;
            line.endWidth = 0.14f;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.numCornerVertices = 4;
            line.numCapVertices = 4;

            int segs = 36;
            line.positionCount = segs;
            float r = 1.45f;
            for (int i = 0; i < segs; i++)
            {
                float angle = i * Mathf.PI * 2f / segs;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r));
            }

            Shader unlit = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                        ?? Shader.Find("Universal Render Pipeline/Unlit")
                        ?? Shader.Find("Sprites/Default");

            Material ringMat = new Material(unlit);
            ringMat.color = themeColor;
            if (ringMat.HasProperty("_BaseColor")) ringMat.SetColor("_BaseColor", themeColor);
            if (ringMat.HasProperty("_EmissionColor"))
            {
                ringMat.EnableKeyword("_EMISSION");
                ringMat.SetColor("_EmissionColor", themeColor * 2.8f);
            }
            line.sharedMaterial = ringMat;

            // B. Vertical Energy Shaft Pillar
            var shaftObj = new GameObject("HoloShaft_EnergyBeam");
            shaftObj.transform.SetParent(parent.transform, false);
            shaftObj.transform.localPosition = Vector3.zero;

            var shaftLine = shaftObj.AddComponent<LineRenderer>();
            shaftLine.useWorldSpace = false;
            shaftLine.startWidth = 0.22f;
            shaftLine.endWidth = 0.22f;
            shaftLine.positionCount = 2;
            shaftLine.SetPosition(0, new Vector3(0f, 0f, 0f));
            shaftLine.SetPosition(1, new Vector3(0f, isLow ? 48.0f : -48.0f, 0f));
            shaftLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            Color beamCol = new Color(themeColor.r, themeColor.g, themeColor.b, 0.45f);
            Material beamMat = new Material(unlit);
            beamMat.color = beamCol;
            if (beamMat.HasProperty("_BaseColor")) beamMat.SetColor("_BaseColor", beamCol);
            if (beamMat.HasProperty("_EmissionColor"))
            {
                beamMat.EnableKeyword("_EMISSION");
                beamMat.SetColor("_EmissionColor", themeColor * 1.8f);
            }
            shaftLine.sharedMaterial = beamMat;

            // C. Dynamic Point Light
            var lightObj = new GameObject("Station_PointLight");
            lightObj.transform.SetParent(parent.transform, false);
            lightObj.transform.localPosition = new Vector3(0f, 1.8f, 0f);
            var pl = lightObj.AddComponent<Light>();
            pl.type = LightType.Point;
            pl.color = themeColor;
            pl.range = 7.0f;
            pl.intensity = 1.4f;

            // D. 3D Floating TextMesh
            var textObj = new GameObject("Station_HoloLabel");
            textObj.transform.SetParent(parent.transform, false);
            textObj.transform.localPosition = new Vector3(isLow ? 0f : -0.85f, isLow ? 2.4f : 3.35f, 0f);
            var tm = textObj.AddComponent<TextMesh>();
            tm.text = label;
            tm.fontSize = 28;
            tm.characterSize = 0.055f;
            tm.alignment = TextAlignment.Center;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = themeColor;
            textObj.AddComponent<StationLabelBillboard>();

            // E. 3D Floating Diamond Beacon in World Space
            var beaconObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            beaconObj.name = "Station_DiamondBeacon";
            beaconObj.transform.SetParent(parent.transform, false);
            beaconObj.transform.localPosition = new Vector3(isLow ? 0f : -0.85f, isLow ? 3.4f : 4.15f, 0f);
            beaconObj.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            beaconObj.transform.localScale = new Vector3(0.7f, 0.7f, 1f);
            Destroy(beaconObj.GetComponent<Collider>());

            Material beaconMat = new Material(unlit);
            beaconMat.color = themeColor;
            if (beaconMat.HasProperty("_BaseColor")) beaconMat.SetColor("_BaseColor", themeColor);
            if (beaconMat.HasProperty("_EmissionColor"))
            {
                beaconMat.EnableKeyword("_EMISSION");
                beaconMat.SetColor("_EmissionColor", themeColor * 3.0f);
            }
            beaconObj.GetComponent<Renderer>().sharedMaterial = beaconMat;
            beaconObj.AddComponent<StationLabelBillboard>();
        }

        private void BuildGoldTracer()
        {
            // 24m Luminous Guide Tracer HIGH Lift (14.50, 27.05, -1.25) -> Sky Door (38.50, 27.05, -1.25)
            var tracerObj = new GameObject("SkyLift_GoldTracerLine");
            tracerObj.transform.SetParent(transform, false);

            _goldTracerLine = tracerObj.AddComponent<LineRenderer>();
            _goldTracerLine.startWidth = 0.20f;
            _goldTracerLine.endWidth = 0.20f;
            _goldTracerLine.useWorldSpace = true;
            _goldTracerLine.numCornerVertices = 4;
            _goldTracerLine.numCapVertices = 4;
            _goldTracerLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            int steps = 25;
            _goldTracerLine.positionCount = steps;
            for (int i = 0; i < steps; i++)
            {
                float t = (float)i / (steps - 1);
                Vector3 pt = Vector3.Lerp(HighStationPos, SkyDoorPos, t);
                pt.y += 0.08f; // Elevated off floor to eliminate z-fighting
                _goldTracerLine.SetPosition(i, pt);
            }

            Shader unlit = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                        ?? Shader.Find("Universal Render Pipeline/Unlit")
                        ?? Shader.Find("Sprites/Default");
            Material goldMat = new Material(unlit);
            Color gold = new Color(1.0f, 0.78f, 0.10f, 0.95f);
            goldMat.color = gold;
            if (goldMat.HasProperty("_BaseColor")) goldMat.SetColor("_BaseColor", gold);
            if (goldMat.HasProperty("_EmissionColor"))
            {
                goldMat.EnableKeyword("_EMISSION");
                goldMat.SetColor("_EmissionColor", gold * 2.8f);
            }
            _goldTracerLine.sharedMaterial = goldMat;
        }

        // ─────────────────────────────────────────────────────────────────────
        // UPDATE & INTERACTION CHECK
        // ─────────────────────────────────────────────────────────────────────
        private void Update()
        {
            if (_isWarpping) return;

            if (_playerTransform == null)
            {
                LocatePlayer();
                if (_playerTransform == null) return;
            }

            bool m3Suppressed = Cyberpunk.World.PitArenaDoorTrigger.IsReturningFromPit ||
                                Cyberpunk.Missions.Mission4_VulcanShowdownManager.IsVulcanDefeated ||
                                Cyberpunk.Missions.Mission4_VulcanShowdownManager.PendingIdoReputationMessage ||
                                Cyberpunk.Missions.IdoAssassinationDirector.Instance != null ||
                                Cyberpunk.Missions.IdoAssassinationDirector.HasPlayed;

            // Dynamically activate Mission 3 navigation if pass is secured, M2 completed, or F3 pressed
            if (!IsMission3Active && !m3Suppressed)
            {
                bool hasPass = Cyberpunk.Inventory.CyberpunkInventory.Instance != null 
                    && Cyberpunk.Inventory.CyberpunkInventory.Instance.CountOf("arena_pass") > 0;
                var m2 = FindAnyObjectByType<Cyberpunk.Missions.Mission2_ArenaPassStealManager>();
                bool m2Done = m2 != null && (m2.state == Cyberpunk.Missions.Mission2_ArenaPassStealManager.M2State.Completed 
                                          || m2.state == Cyberpunk.Missions.Mission2_ArenaPassStealManager.M2State.LootPass);

                if (hasPass || m2Done || Input.GetKeyDown(KeyCode.F3))
                {
                    ActivateMission3();
                }
            }

            if (m3Suppressed)
            {
                IsMission3Active = false;
            }

            if (!IsMission3Active)
            {
                // Before Mission 2 is completed or after Vulcan is defeated, keep navigation trail and blip hidden
                if (_gpsFloorLine != null && _gpsFloorLine.positionCount > 0)
                    _gpsFloorLine.positionCount = 0;
                if (_goldTracerLine != null && _goldTracerLine.positionCount > 0)
                    _goldTracerLine.positionCount = 0;
                if (_minimapBlipRect != null && _minimapBlipRect.gameObject.activeSelf)
                    _minimapBlipRect.gameObject.SetActive(false);
            }
            else
            {
                if (_minimapBlipRect != null && !_minimapBlipRect.gameObject.activeSelf)
                    _minimapBlipRect.gameObject.SetActive(true);

                UpdateMinimapBlip();
                UpdateGpsTracer();
            }

            Vector3 pPos = _playerTransform.position;
            float dLow = Vector3.Distance(pPos, LowStationPos);
            float dHigh = Vector3.Distance(pPos, HighStationPos);

            if (dLow <= interactionRadius)
            {
                _activeStationIndex = 0; // At LOW, go HIGH
            }
            else if (dHigh <= interactionRadius)
            {
                float dDoor = Vector3.Distance(pPos, SkyDoorPos);
                // If player is closer to the Pit Door or in its interaction zone, suppress the lift prompt
                if (dDoor < 2.4f || dDoor < dHigh)
                {
                    _activeStationIndex = -1;
                }
                else
                {
                    _activeStationIndex = 1; // At HIGH, go LOW
                }
            }
            else
            {
                _activeStationIndex = -1;
            }

            if (_activeStationIndex != -1 && (Input.GetKeyDown(interactKey) || Input.GetKeyDown(KeyCode.F)))
            {
                if (Time.time - _lastWarpTime >= warpCooldown)
                {
                    bool ascend = (_activeStationIndex == 0);
                    StartCoroutine(ExecuteSkyLiftWarp(ascend));
                }
            }
        }

        private IEnumerator ExecuteSkyLiftWarp(bool ascend)
        {
            _isWarpping = true;
            _lastWarpTime = Time.time;

            SetPlayerControlsLocked(true);

            string title = ascend ? "SKY LIFT // ASCENDING 48M" : "SKY LIFT // DESCENDING 48M";
            string status = ascend
                ? "PRESSURIZING CABIN... ASCENDING TO FAR UPPER PLAZA..."
                : "ENGAGING HYDRAULIC DESCENT TO LOWER ALLEY...";

            CyberpunkLoadingScreen.Show(title);
            CyberpunkLoadingScreen.SetStatus(status);

            // Animate screen progress smoothly
            float elapsed = 0f;
            float dur = 0.85f;
            bool warped = false;

            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / dur);
                CyberpunkLoadingScreen.SetProgress(progress);

                if (progress >= 0.65f && !warped)
                {
                    WarpPlayerToStation(ascend);
                    warped = true;
                }
                yield return null;
            }

            if (!warped) WarpPlayerToStation(ascend);

            yield return null; // Settle physics & camera

            CyberpunkLoadingScreen.Hide();
            SetPlayerControlsLocked(false);
            _isWarpping = false;

            string destName = ascend ? "FAR UPPER PLAZA" : "LOWER DISTRICT ALLEY";
            CyberpunkHUDController.Instance?.ShowToast($"SKY LIFT // ARRIVED AT {destName}", 3.0f);
        }

        private void WarpPlayerToStation(bool toHigh)
        {
            if (_playerTransform == null) LocatePlayer();
            if (_playerTransform == null) return;

            Vector3 dest = toHigh ? HighStationPos : LowStationPos;
            float targetYaw = toHigh ? HighStationYaw : LowStationYaw;

            // Verify destination on NavMesh
            if (NavMesh.SamplePosition(dest, out NavMeshHit nHit, 3.0f, NavMesh.AllAreas))
            {
                dest.y = nHit.position.y;
            }

            var cc = _playerTransform.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            _playerTransform.position = dest;
            _playerTransform.rotation = Quaternion.Euler(0f, targetYaw, 0f);

            Physics.SyncTransforms();

            if (cc != null) cc.enabled = true;

            // Align camera behind player
            if (SmoothOrbitCamera.Instance != null)
            {
                SmoothOrbitCamera.Instance.tightPositionFollow = true;
                Vector3 offset = Quaternion.Euler(14f, targetYaw, 0f) * new Vector3(0f, 0.40f, -4.8f);
                SmoothOrbitCamera.Instance.transform.position = dest + new Vector3(0f, 1.45f, 0f) + offset;
                SmoothOrbitCamera.Instance.transform.rotation = Quaternion.Euler(14f, targetYaw, 0f);
            }
            else if (Camera.main != null)
            {
                Vector3 offset = Quaternion.Euler(14f, targetYaw, 0f) * new Vector3(0f, 0.40f, -4.8f);
                Camera.main.transform.position = dest + new Vector3(0f, 1.45f, 0f) + offset;
                Camera.main.transform.rotation = Quaternion.Euler(14f, targetYaw, 0f);
            }
        }

        private void SetPlayerControlsLocked(bool locked)
        {
            if (_playerTransform == null) return;
            var hero = _playerTransform.GetComponent<InsaneGunSwordController>();
            if (hero != null) hero.inputLocked = locked;
            var loco = _playerTransform.GetComponent<SyntySwordLocomotionController>();
            if (loco != null) loco.inputLocked = locked;
        }

        // ─────────────────────────────────────────────────────────────────────
        // ONGUI INTERACTIVE PROMPT HUD
        // ─────────────────────────────────────────────────────────────────────
        private void BuildGUIStyles()
        {
            _hudBgTex = new Texture2D(1, 1);
            _hudBgTex.SetPixel(0, 0, new Color(0.015f, 0.04f, 0.07f, 0.92f));
            _hudBgTex.Apply();

            _hudBoxStyle = new GUIStyle
            {
                normal = { background = _hudBgTex },
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(16, 16, 8, 8)
            };

            _hudTitleStyle = new GUIStyle
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.05f, 0.95f, 1f, 1f) }
            };

            _hudSubStyle = new GUIStyle
            {
                fontSize = 11,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.82f, 0.2f, 0.95f) }
            };
        }

        private void OnGUI()
        {
            if (_isWarpping || _activeStationIndex == -1) return;

            string action = (_activeStationIndex == 0) ? "Ride Sky Lift to Upper Plaza" : "Descend to Lower Alley";
            Cyberpunk.UI.CyberpunkInteractionPromptHUD.DrawPromptImmediate("E", action);
        }

        private void SetupMinimapBlip()
        {
            if (MinimapController.Instance == null || MinimapController.Instance.enemyBlipsContainer == null) return;
            if (_minimapBlipRect != null) return;

            var container = MinimapController.Instance.enemyBlipsContainer;
            var blipObj = new GameObject("MinimapBlip_SkyLift");
            blipObj.transform.SetParent(container, false);

            _minimapBlipRect = blipObj.AddComponent<RectTransform>();
            _minimapBlipRect.sizeDelta = new Vector2(16f, 16f);
            _minimapBlipRect.localRotation = Quaternion.Euler(0f, 0f, 45f);

            _minimapBlipImage = blipObj.AddComponent<Image>();
            _minimapBlipImage.color = new Color(0f, 0.95f, 1f, 0.95f);

            var innerObj = new GameObject("InnerIcon");
            innerObj.transform.SetParent(blipObj.transform, false);
            var innerRect = innerObj.AddComponent<RectTransform>();
            innerRect.sizeDelta = new Vector2(8f, 8f);
            _minimapInnerIcon = innerObj.AddComponent<Image>();
            _minimapInnerIcon.color = new Color(0.01f, 0.04f, 0.08f, 0.95f);
        }

        private void UpdateMinimapBlip()
        {
            if (_minimapBlipRect == null) SetupMinimapBlip();
            if (_minimapBlipRect == null || MinimapController.Instance == null || _playerTransform == null) return;

            bool isUpper = _playerTransform.position.y > 15f;
            Vector3 targetPos = isUpper ? SkyDoorPos : LowStationPos;

            float minimapWidth = MinimapController.Instance.minimapRawImage != null 
                ? MinimapController.Instance.minimapRawImage.rectTransform.rect.width 
                : 226f;
            float orthographicSize = MinimapController.Instance.orthographicSize > 0f 
                ? MinimapController.Instance.orthographicSize 
                : 40f;
            float scaleRatio = (minimapWidth / 2f) / orthographicSize;

            Vector3 diff = targetPos - _playerTransform.position;
            Vector2 blipPos = new Vector2(diff.x, diff.z) * scaleRatio;

            float pulse = Mathf.Sin(Time.time * 5f) * 0.15f + 1.0f;
            _minimapBlipRect.localScale = new Vector3(pulse, pulse, 1f);

            float maxOffset = (minimapWidth / 2f) - 10f;
            blipPos.x = Mathf.Clamp(blipPos.x, -maxOffset, maxOffset);
            blipPos.y = Mathf.Clamp(blipPos.y, -maxOffset, maxOffset);

            _minimapBlipRect.anchoredPosition = blipPos;

            Color themeColor = isUpper ? new Color(1f, 0.82f, 0.15f, 0.95f) : new Color(0f, 0.95f, 1f, 0.95f);
            if (_minimapBlipImage != null) _minimapBlipImage.color = themeColor;
        }

        private void BuildGpsTracer()
        {
            if (_gpsFloorLine != null) return;

            var obj = new GameObject("SkyLift_GpsFloorTracer");
            obj.transform.SetParent(transform, false);

            _gpsFloorLine = obj.AddComponent<LineRenderer>();
            _gpsFloorLine.startWidth = 0.22f;
            _gpsFloorLine.endWidth = 0.22f;
            _gpsFloorLine.useWorldSpace = true;
            _gpsFloorLine.alignment = LineAlignment.TransformZ;
            _gpsFloorLine.numCapVertices = 4;
            _gpsFloorLine.numCornerVertices = 4;
            _gpsFloorLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _gpsFloorLine.receiveShadows = false;

            Shader unlit = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                        ?? Shader.Find("Universal Render Pipeline/Unlit")
                        ?? Shader.Find("Sprites/Default");
            Material mat = new Material(unlit);
            Color cyan = new Color(0f, 0.95f, 1f, 0.85f);
            mat.color = cyan;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", cyan);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", cyan * 2.5f);
            }
            _gpsFloorLine.sharedMaterial = mat;
            _gpsNavPath = new NavMeshPath();
        }

        private void UpdateGpsTracer()
        {
            if (_gpsFloorLine == null || _playerTransform == null) return;
            if (Time.time - _lastGpsUpdate < 0.25f) return;
            _lastGpsUpdate = Time.time;

            bool isUpper = _playerTransform.position.y > 15f;
            if (isUpper)
            {
                _gpsFloorLine.positionCount = 0;
                return;
            }

            Vector3 target = LowStationPos;
            float dist = Vector3.Distance(_playerTransform.position, target);
            if (dist < 3.0f)
            {
                _gpsFloorLine.positionCount = 0;
                return;
            }

            if (NavMesh.CalculatePath(_playerTransform.position, target, NavMesh.AllAreas, _gpsNavPath))
            {
                if (_gpsNavPath.status == NavMeshPathStatus.PathComplete || _gpsNavPath.corners.Length > 1)
                {
                    _gpsFloorLine.positionCount = _gpsNavPath.corners.Length;
                    for (int i = 0; i < _gpsNavPath.corners.Length; i++)
                    {
                        Vector3 pt = _gpsNavPath.corners[i];
                        pt.y += 0.08f;
                        _gpsFloorLine.SetPosition(i, pt);
                    }
                    return;
                }
            }

            _gpsFloorLine.positionCount = 2;
            _gpsFloorLine.SetPosition(0, _playerTransform.position + Vector3.up * 0.1f);
            _gpsFloorLine.SetPosition(1, target + Vector3.up * 0.1f);
        }

        private void DrawScreenWaypointMarker()
        {
            if (!IsMission3Active ||
                Cyberpunk.Missions.Mission4_VulcanShowdownManager.IsVulcanDefeated ||
                Cyberpunk.Missions.IdoAssassinationDirector.Instance != null ||
                Cyberpunk.Missions.IdoAssassinationDirector.HasPlayed)
            {
                return;
            }

            Camera cam = Camera.main;
            if (cam == null || _playerTransform == null) return;

            bool isUpper = _playerTransform.position.y > 15f;
            Vector3 targetPos = isUpper ? SkyDoorPos : LowStationPos;
            float dist = Vector3.Distance(_playerTransform.position, targetPos);

            if (dist <= interactionRadius) return;

            Vector3 markerWorldPos = targetPos + Vector3.up * 2.8f;
            Vector3 screenPos = cam.WorldToScreenPoint(markerWorldPos);
            bool isBehind = screenPos.z < 0f;

            float sw = Screen.width;
            float sh = Screen.height;
            Vector2 center = new Vector2(sw * 0.5f, sh * 0.5f);

            Vector2 screenPt = new Vector2(screenPos.x, sh - screenPos.y);

            float minX = 140f;
            float maxX = sw - 140f;
            float minY = 100f;
            float maxY = sh - 140f;

            bool offScreen = isBehind || screenPt.x < minX || screenPt.x > maxX || screenPt.y < minY || screenPt.y > maxY;

            string arrow = "";
            if (offScreen)
            {
                Vector2 fromCenter = screenPt - center;
                if (isBehind) fromCenter = -fromCenter;
                if (fromCenter.sqrMagnitude < 0.001f) fromCenter = Vector2.down;
                fromCenter.Normalize();

                float halfW = (maxX - minX) * 0.5f;
                float halfH = (maxY - minY) * 0.5f;
                Vector2 boxCenter = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);

                float scaleX = Mathf.Abs(fromCenter.x) > 0.001f ? halfW / Mathf.Abs(fromCenter.x) : float.MaxValue;
                float scaleY = Mathf.Abs(fromCenter.y) > 0.001f ? halfH / Mathf.Abs(fromCenter.y) : float.MaxValue;
                float s = Mathf.Min(scaleX, scaleY);
                screenPt = boxCenter + fromCenter * s;

                if (Mathf.Abs(fromCenter.x) > Mathf.Abs(fromCenter.y))
                    arrow = fromCenter.x > 0 ? "►" : "◄";
                else
                    arrow = fromCenter.y > 0 ? "▼" : "▲";
            }

            Color theme = isUpper ? new Color(1f, 0.82f, 0.15f) : new Color(0f, 0.95f, 1f);
            string title = isUpper ? "THE PIT // COMBAT ARENA" : "SKY LIFT // THE PIT";
            string text = string.IsNullOrEmpty(arrow)
                ? $"◈ {title}  [{dist:F0}m]"
                : (arrow == "◄" || arrow == "▲")
                    ? $"{arrow} ◈ {title} [{dist:F0}m]"
                    : $"◈ {title} [{dist:F0}m] {arrow}";

            float bw = 260f;
            float bh = 34f;
            Rect r = new Rect(screenPt.x - bw * 0.5f, screenPt.y - bh * 0.5f, bw, bh);

            GUI.Box(r, GUIContent.none, _hudBoxStyle);
            DrawBorder(r, theme, 1.5f);

            var labelStyle = new GUIStyle(_hudTitleStyle)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = theme }
            };
            GUI.Label(r, text, labelStyle);
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
    }

    /// <summary>
    /// Billboards the 3D text label above the station toward the main camera.
    /// </summary>
    public class StationLabelBillboard : MonoBehaviour
    {
        private void LateUpdate()
        {
            if (Camera.main != null)
            {
                transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position);
            }
        }
    }
}
