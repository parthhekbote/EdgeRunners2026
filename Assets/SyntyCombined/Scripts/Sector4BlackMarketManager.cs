using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using InsaneGun.Combat;
using SyntyCombined.Cutscene;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SyntyCombined
{
    /// <summary>
    /// Manages the Sector 4 Black Market gameplay scene when Alita arrives via hyperspace warp transit
    /// from the Blackwall Cyberspace communion.
    /// 
    /// Ensures:
    /// - Alita (Alita_BattleAngel_Hero) spawns or positions cleanly onto the landing plaza.
    /// - The parked Martian Starship is spawned with active boarding controller.
    /// - Smooth Orbit Camera targets Alita for full third-person locomotion and sword combat.
    /// - Cyberpunk HUD displays Sector 4 district banner and quest objective to find the Antimatter Core.
    /// </summary>
    public class Sector4BlackMarketManager : MonoBehaviour
    {
        public static Sector4BlackMarketManager Instance { get; private set; }

        public const string BLACK_MARKET_SCENE_NAME = "Demo_BlackMarket_01";

        [Header("Spawn Settings")]
        public Vector3 landingPlazaCenter = new Vector3(27.48f, 1.86f, 34.00f);

        [Header("Prefabs")]
        public GameObject alitaHeroPrefab;
        public GameObject starshipPrefab;

        private GameObject _spawnedAlita;
        private GameObject _parkedShip;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnSceneLoadedRuntime()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;

            CheckAndInitializeInScene(SceneManager.GetActiveScene());
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            CheckAndInitializeInScene(scene);
        }

        private static void CheckAndInitializeInScene(Scene scene)
        {
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.name)) return;
            if (!scene.name.Equals(BLACK_MARKET_SCENE_NAME, System.StringComparison.OrdinalIgnoreCase)) return;

            if (Instance != null || Object.FindAnyObjectByType<Sector4BlackMarketManager>() != null) return;

            GameObject mgrGO = new GameObject("[Sector4_BlackMarket_Manager]");
            mgrGO.AddComponent<Sector4BlackMarketManager>();
            Debug.Log("<color=#00F0FF><b>[Sector 4 Black Market]</b></color> Initialized Sector4BlackMarketManager in " + scene.name);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            StartCoroutine(SetupBlackMarketSequenceRoutine());
        }

        private IEnumerator SetupBlackMarketSequenceRoutine()
        {
            // Give Unity physics 1 frame to initialize scene colliders
            yield return null;

            // 1. Determine ground position in Black Market plaza
            Vector3 groundPos = FindPlazaGroundPosition();

            // 2. Setup Alita Player Character
            SetupAlitaPlayer(groundPos);

            // 3. Setup Parked Martian Starship
            SetupParkedStarship(groundPos);

            // 4. Setup Camera Rig
            SetupCameraRig();

            // 5. Setup Cyberpunk HUD & Quest Tracker
            SetupCyberpunkHUD();

            // 6. Guardians of the Galaxy Themed Arrival Cutscene in DRIFT
            SetupArrivalCutscene();

            // 7. Arrival atmospheric VFX
            StartCoroutine(ArrivalShockwaveRoutine(groundPos));

            Debug.Log("<color=#00FFAA><b>[Sector 4 Black Market]</b></color> Alita successfully arrived in DRIFT. Ready to locate Antimatter Core.");
        }

        private Vector3 FindPlazaGroundPosition()
        {
            Vector3 rayStart = landingPlazaCenter + Vector3.up * 35.0f;
            RaycastHit hit;
            if (Physics.Raycast(rayStart, Vector3.down, out hit, 60.0f))
            {
                return hit.point;
            }

            // Fallback ground height
            return new Vector3(0f, 0.05f, 0f);
        }

        private void SetupAlitaPlayer(Vector3 groundPos)
        {
            _spawnedAlita = GameObject.Find("Alita_BattleAngel_Hero");
            if (_spawnedAlita == null) _spawnedAlita = GameObject.FindWithTag("Player");

            Vector3 safeAlitaPos = new Vector3(22.80f, 1.88f, 33.50f);
            if (_spawnedAlita == null)
            {
#if UNITY_EDITOR
                if (alitaHeroPrefab == null)
                {
                    alitaHeroPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Alita_BattleAngel_Hero.prefab");
                }
#endif
                if (alitaHeroPrefab != null)
                {
                    _spawnedAlita = Instantiate(alitaHeroPrefab, safeAlitaPos, Quaternion.Euler(0f, 180f, 0f));
                    _spawnedAlita.name = "Alita_BattleAngel_Hero";
                    _spawnedAlita.tag = "Player";
                }
            }
            else
            {
                // Reposition existing Alita safely beside starship cockpit on the pad
                var cc = _spawnedAlita.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                _spawnedAlita.transform.position = safeAlitaPos;
                _spawnedAlita.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                if (cc != null) cc.enabled = true;
            }

            if (_spawnedAlita != null)
            {
                var swordCtrl = _spawnedAlita.GetComponent<InsaneGunSwordController>();
                if (swordCtrl != null)
                {
                    swordCtrl.enabled = true;
                    swordCtrl.currentHealth = swordCtrl.maxHealth;
                }

                // Front fill light
                var fillLight = _spawnedAlita.GetComponentInChildren<Light>();
                if (fillLight == null)
                {
                    GameObject flGO = new GameObject("Alita_FrontFill");
                    flGO.transform.SetParent(_spawnedAlita.transform, false);
                    flGO.transform.localPosition = new Vector3(0f, 1.4f, -1.0f);
                    var l = flGO.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.color = new Color(0.85f, 0.4f, 0.5f, 1.0f);
                    l.intensity = 1.2f;
                    l.range = 4.5f;
                }
            }
        }

        private void SetupParkedStarship(Vector3 groundPos)
        {
            _parkedShip = GameObject.Find("Alita_Lost_Martian_Starship");
            if (_parkedShip == null)
            {
                var existingHover = GameObject.Find("SM_Veh_HoverCraft_01");
                if (existingHover != null)
                {
                    _parkedShip = existingHover;
                    _parkedShip.name = "Alita_Lost_Martian_Starship";
                }
            }

            if (_parkedShip == null)
            {
#if UNITY_EDITOR
                if (starshipPrefab == null)
                {
                    starshipPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonSciFiWorlds/Prefabs/Props/Vehicles/SM_Veh_HoverCraft_01.prefab");
                }
#endif
                Vector3 shipPos = new Vector3(27.48f, 1.86f, 34.00f);
                Quaternion shipRot = Quaternion.Euler(0f, 180f, 0f);

                if (starshipPrefab != null)
                {
                    _parkedShip = Instantiate(starshipPrefab, shipPos, shipRot);
                    _parkedShip.name = "Alita_Lost_Martian_Starship";
                }
            }

            if (_parkedShip != null)
            {
                var flightCtrl = _parkedShip.GetComponent<SpaceshipFlightController>();
                if (flightCtrl == null) flightCtrl = _parkedShip.AddComponent<SpaceshipFlightController>();
                flightCtrl.boardKey = KeyCode.F;
                flightCtrl.targetTransitScene = "Scene_Blackwall_Cyberspace";
            }
        }

        private void SetupCameraRig()
        {
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                var camObj = GameObject.Find("Main Camera");
                if (camObj != null) mainCam = camObj.GetComponent<Camera>();
            }

            if (mainCam != null && _spawnedAlita != null)
            {
                var orbitCam = mainCam.GetComponent<SmoothOrbitCamera>();
                if (orbitCam == null) orbitCam = mainCam.gameObject.AddComponent<SmoothOrbitCamera>();
                orbitCam.target = _spawnedAlita.transform;
                orbitCam.targetOffset = new Vector3(0f, 1.45f, 0f);
                orbitCam.cameraOffset = new Vector3(0.48f, 0.08f, -2.7f);
                orbitCam.tightPositionFollow = true;
                orbitCam.minDistance = 1.8f;
                orbitCam.maxDistance = 6.5f;
            }
        }

        private void SetupCyberpunkHUD()
        {
            // Ensure CyberpunkHUDController instance
            if (CyberpunkHUDController.Instance == null)
            {
                var hudObj = new GameObject("CyberpunkHUD_Controller");
                hudObj.AddComponent<CyberpunkHUDController>();
            }

            if (CyberpunkHUDController.Instance != null)
            {
                var distTxt = CyberpunkHUDController.Instance.transform.Find("Text_District/Txt_Dist");
                if (distTxt != null)
                {
                    var tmp = distTxt.GetComponent<TMPro.TextMeshProUGUI>();
                    if (tmp != null) tmp.text = "DRIFT // SECTOR 04";
                }

                CyberpunkHUDController.Instance.SetQuestObjective(
                    "DRIFT: UNREGULATED MARKET",
                    "► Locate The Collector inside the skull-marked trophy sanctum to track your core"
                );

                CyberpunkHUDController.Instance.ShowToast(
                    "WARP JUMP COMPLETE // ARRIVED AT DRIFT OUTLAW MARKET",
                    6.5f
                );
            }
        }

        private void SetupArrivalCutscene()
        {
            var director = Object.FindAnyObjectByType<BlackMarketArrivalCutsceneDirector>();
            if (director == null)
            {
                var dirObj = new GameObject("[BlackMarket_Arrival_Cutscene_Director]");
                director = dirObj.AddComponent<BlackMarketArrivalCutsceneDirector>();
            }

            director.starship = _parkedShip;
            director.alitaHero = _spawnedAlita;
            director.playOnStart = true;
            director.StartCutscene();
        }

        private IEnumerator ArrivalShockwaveRoutine(Vector3 center)
        {
            var beacon = new GameObject("Arrival_Beacon");
            beacon.transform.position = center + Vector3.up * 1.5f;
            var l = beacon.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(0f, 0.94f, 1f);
            l.intensity = 8f;
            l.range = 35f;

            float elapsed = 0f;
            while (elapsed < 1.8f)
            {
                elapsed += Time.deltaTime;
                l.intensity = Mathf.Lerp(8f, 0f, elapsed / 1.8f);
                yield return null;
            }

            Destroy(beacon);
        }
    }
}
