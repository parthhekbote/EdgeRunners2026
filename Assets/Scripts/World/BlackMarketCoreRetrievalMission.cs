using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SyntyCombined.BlackMarket;
using SyntyCombined;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SyntyCombined.World
{
    public enum CoreRetrievalStage
    {
        Briefing = 0,
        CollectingMatrices = 1,
        ReturnToLandingPad = 2,
        ReturnToCoreEngine = 2,
        LandedReturnToCollector = 3,
        InsertingCores = 4,
        ReactorIgnited = 5,
        HyperspaceWarpUnlocked = 6
    }

    /// <summary>
    /// Black Market Story Mission: Asteroid Core Engine-01 Retrieval.
    /// Manages:
    /// - Spawning 4 high-energy scattered Core Matrices in space around the broken dome
    /// - Tracking collection telemetry (0/4 -> 4/4) with voice/comms transmissions
    /// - Receptacle docking & core insertion at SM_Env_Core_01_Engine_01
    /// - Reactor ignition sequence (volumetric plasma beams, shockwave VFX & rumble SFX)
    /// - Decrypting Mars navigation coordinates & unlocking Hyperspace Warp Jump [R]
    /// </summary>
    public class BlackMarketCoreRetrievalMission : MonoBehaviour
    {
        private static BlackMarketCoreRetrievalMission _instance;
        public static BlackMarketCoreRetrievalMission Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<BlackMarketCoreRetrievalMission>() ?? FindObjectOfType<BlackMarketCoreRetrievalMission>();
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("Mission Stage")]
        public CoreRetrievalStage currentStage = CoreRetrievalStage.Briefing;

        [Header("Target Asteroid Core Engine")]
        public Transform coreEngineTransform;
        public Vector3 engineInsertionBayPos = new Vector3(-3.18f, 12.0f, -3.39f);
        public float insertionDockRadius = 26f;

        [Header("Landing Pad B-04 Target")]
        public Vector3 landingPadPos = new Vector3(27.48f, 1.86f, 34.00f);
        public Quaternion landingPadRot = Quaternion.Euler(0f, 180f, 0f);
        public float landingApproachRadius = 32f;
        public float landingPadDockRadius = 6.5f;

        private GameObject _landingPadBeacon;
        private AudioSource _landingAudioSource;
        private AudioClip _landingClampClip;
        private bool _isLandingSequenceRunning = false;
        private bool _isInLandingZone = false;

        [Header("Spaceship Reference")]
        public SpaceshipFlightController nightshadeShip;
        public NightshadeTractorBeam tractorBeam;

        [Header("Scattered Core Matrices")]
        public List<AsteroidCoreMatrix> spawnedMatrices = new List<AsteroidCoreMatrix>();
        public Vector3[] spaceSpawnCoordinates = new Vector3[]
        {
            new Vector3(-75f, 95f, 40f),    // Alpha: Western asteroid breach
            new Vector3( 15f, 160f, 110f),  // Beta: High orbit above fractured dome
            new Vector3( 110f, 85f, -30f),  // Gamma: Eastern orbital gantry debris
            new Vector3(-35f, 130f, 175f)   // Delta: Northern solar array breach
        };

        [Header("Reactor VFX & Audio")]
        public GameObject reactorIgnitionVFX;
        public Light reactorShaftLight;
        private AudioSource _reactorAudio;
        private AudioClip _reactorIgniteClip;

        // UI & Telemetry
        private Camera _mainCam;
        private bool _hasShownIntroComms = false;

        private void Awake()
        {
            Instance = this;
            SetupMissionComponents();
        }

        private void Start()
        {
            FindSceneReferences();
            ConfigureLandingOnPodPad();
            SpawnCoreMatricesIfNeeded();
            SetupReactorBay();
            SetupLandingPadVisuals();
            SetupLandingAudio();

            if (nightshadeShip != null)
            {
                // Lock warp until cores are collected and engine is restored
                nightshadeShip.warpUnlocked = false;
                nightshadeShip.enableAltitudeWarp = false;
                nightshadeShip.targetTransitScene = "Scene_Mars_FinalBattle";
            }

            if (currentStage == CoreRetrievalStage.Briefing)
            {
                if (CyberpunkHUDController.Instance != null)
                {
                    CyberpunkHUDController.Instance.SetQuestObjective(
                        "DRIFT // SECTOR 04",
                        "► Enter The Collector's Sanctuary Pod and speak with Kaelen Voss [E]"
                    );
                }
            }
        }

        private void FindSceneReferences()
        {
            if (_mainCam == null)
            {
                _mainCam = Camera.main;
                if (_mainCam == null)
                {
                    var camObj = GameObject.Find("Main Camera");
                    if (camObj != null) _mainCam = camObj.GetComponent<Camera>();
                }
            }

            if (nightshadeShip == null)
            {
                nightshadeShip = FindFirstObjectByType<SpaceshipFlightController>();
            }

            if (tractorBeam == null && nightshadeShip != null)
            {
                tractorBeam = nightshadeShip.GetComponent<NightshadeTractorBeam>();
            }

            if (coreEngineTransform == null)
            {
                var engineObj = GameObject.Find("SM_Env_Core_01_Engine_01");
                if (engineObj != null) coreEngineTransform = engineObj.transform;
            }

            if (tractorBeam != null)
            {
                tractorBeam.OnCoreSecured -= HandleCoreSecured;
                tractorBeam.OnCoreSecured += HandleCoreSecured;
            }
        }

        private void SetupMissionComponents()
        {
            _reactorIgniteClip = GenerateReactorIgniteClip();
            _reactorAudio = gameObject.AddComponent<AudioSource>();
            _reactorAudio.clip = _reactorIgniteClip;
            _reactorAudio.loop = true;
            _reactorAudio.playOnAwake = false;
            _reactorAudio.spatialBlend = 0.5f;
            _reactorAudio.minDistance = 15f;
            _reactorAudio.maxDistance = 250f;
            _reactorAudio.volume = 0.8f;
        }

        private AudioClip GenerateReactorIgniteClip()
        {
            int sampleRate = 44100;
            int samples = sampleRate * 3;
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float bass = Mathf.Sin(2f * Mathf.PI * 42f * t) * 0.55f;
                float sub = Mathf.Sin(2f * Mathf.PI * 84f * t) * 0.30f;
                float hum = Mathf.Sin(2f * Mathf.PI * 168f * t) * 0.15f;
                float noise = (Random.value * 2f - 1f) * 0.08f;
                data[i] = Mathf.Clamp(bass + sub + hum + noise, -1f, 1f);
            }
            var clip = AudioClip.Create("Reactor_Ignite_Loop", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private void SpawnCoreMatricesIfNeeded()
        {
            // Check if already exist
            var existing = FindObjectsByType<AsteroidCoreMatrix>(FindObjectsSortMode.None);
            if (existing.Length >= 4)
            {
                spawnedMatrices = new List<AsteroidCoreMatrix>(existing);
                return;
            }

            string[] ids = new string[] { "ALPHA", "BETA", "GAMMA", "DELTA" };
            Color[] colors = new Color[]
            {
                new Color(0f, 0.95f, 1f),      // Cyan
                new Color(0.2f, 0.6f, 1f),     // Deep Blue
                new Color(0f, 1f, 0.85f),      // Mint Plasma
                new Color(0.35f, 0.90f, 1f)    // Electric Azure
            };

#if UNITY_EDITOR
            var cellPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonSciFiWorlds/Prefabs/Props/SM_Prop_PowerCell_03.prefab");
            if (cellPrefab == null)
            {
                cellPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonSciFiWorlds/Prefabs/Props/SM_Prop_Fuel_Cell_01.prefab");
            }
#else
            GameObject cellPrefab = null;
#endif

            var matrixGroup = new GameObject("Scattered_Asteroid_Cores_Group");

            for (int i = 0; i < spaceSpawnCoordinates.Length; i++)
            {
                Vector3 spawnPos = spaceSpawnCoordinates[i];
                GameObject coreObj = null;

                if (cellPrefab != null)
                {
                    coreObj = Instantiate(cellPrefab, spawnPos, Random.rotation, matrixGroup.transform);
                }
                else
                {
                    coreObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    coreObj.transform.position = spawnPos;
                    coreObj.transform.rotation = Random.rotation;
                    coreObj.transform.SetParent(matrixGroup.transform);
                }

                coreObj.name = $"Asteroid_Core_Matrix_{ids[i]}";
                coreObj.transform.localScale = Vector3.one * 2.2f;

                var matrixComp = coreObj.GetComponent<AsteroidCoreMatrix>();
                if (matrixComp == null) matrixComp = coreObj.AddComponent<AsteroidCoreMatrix>();

                matrixComp.matrixId = ids[i];
                matrixComp.matrixName = $"CORE MATRIX // {ids[i]}";
                matrixComp.energyColor = colors[i];
                matrixComp.tumbleSpeed = Random.Range(14f, 26f);
                matrixComp.tumbleAxis = Random.onUnitSphere;

                // Sphere collider for tractor detection
                var sphereCol = coreObj.GetComponent<SphereCollider>();
                if (sphereCol == null)
                {
                    sphereCol = coreObj.AddComponent<SphereCollider>();
                    sphereCol.radius = 1.4f;
                    sphereCol.isTrigger = true;
                }

                spawnedMatrices.Add(matrixComp);
            }
        }

        private void SetupReactorBay()
        {
            if (coreEngineTransform != null)
            {
                engineInsertionBayPos = new Vector3(coreEngineTransform.position.x, coreEngineTransform.position.y + 55f, coreEngineTransform.position.z);
            }

            // Reactor shaft light
            var lightObj = GameObject.Find("Reactor_Shaft_Glow_Light");
            if (lightObj == null)
            {
                lightObj = new GameObject("Reactor_Shaft_Glow_Light");
                lightObj.transform.position = engineInsertionBayPos + Vector3.down * 4f;
                reactorShaftLight = lightObj.AddComponent<Light>();
                reactorShaftLight.type = LightType.Point;
                reactorShaftLight.color = new Color(0f, 0.95f, 1f);
                reactorShaftLight.range = 75f;
                reactorShaftLight.intensity = 1.5f;
            }
            else
            {
                reactorShaftLight = lightObj.GetComponent<Light>();
            }

            // Setup Reactor ignition pillar FX
#if UNITY_EDITOR
            if (reactorIgnitionVFX == null)
            {
                var fxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonSciFiWorlds/Prefabs/FX/FX_Core_01.prefab");
                if (fxPrefab != null)
                {
                    reactorIgnitionVFX = Instantiate(fxPrefab, engineInsertionBayPos + Vector3.down * 8f, Quaternion.identity);
                    reactorIgnitionVFX.name = "Reactor_Ignition_FX_Active";
                    reactorIgnitionVFX.SetActive(false);
                }
            }
#endif
        }

        private void Update()
        {
            if (nightshadeShip == null || tractorBeam == null)
            {
                FindSceneReferences();
            }

            // Trigger intro comms when player first boards the ship
            if (!_hasShownIntroComms && nightshadeShip != null && nightshadeShip.IsPiloted)
            {
                _hasShownIntroComms = true;
                currentStage = CoreRetrievalStage.CollectingMatrices;
                ShowIntroBriefing();
            }

            // Check if player is returning to landing pad with all 4 cores secured
            if (currentStage == CoreRetrievalStage.ReturnToLandingPad)
            {
                CheckLandingPadProximity();
            }

            // Animate landing beacon light pulse
            if (_landingPadBeacon != null)
            {
                bool showBeacon = (currentStage == CoreRetrievalStage.ReturnToLandingPad);
                if (_landingPadBeacon.activeSelf != showBeacon)
                {
                    _landingPadBeacon.SetActive(showBeacon);
                }

                if (showBeacon)
                {
                    var light = _landingPadBeacon.GetComponent<Light>();
                    if (light != null)
                    {
                        light.intensity = 2.5f + Mathf.PingPong(Time.time * 4f, 3.0f);
                    }
                }
            }
        }

        private void ShowIntroBriefing()
        {
            if (BlackMarketNPCDialogueHUD.Instance != null)
            {
                BlackMarketNPCDialogueHUD.Instance.ShowMissionBanner("MISSION ACTIVE: RETRIEVE 4 ASTEROID CORE MATRICES OUTSIDE BROKEN DOME");
                BlackMarketNPCDialogueHUD.Instance.ShowTransmission("VOSS", "DATA FIXER", 
                    "Alita, fly up through the fractured dome into orbit. Use your ventral tractor beam [T] to lock onto the 4 drifting core matrices and secure them into your cargo clamp!", 
                    new Color(0.2f, 0.85f, 1f), 7.5f);
            }
        }

        private void HandleCoreSecured(AsteroidCoreMatrix core)
        {
            int held = tractorBeam != null ? tractorBeam.HeldCoreCount : 0;

            if (held < 4)
            {
                if (BlackMarketNPCDialogueHUD.Instance != null)
                {
                    BlackMarketNPCDialogueHUD.Instance.ShowTransmission("ROOK", "CHIEF SHIPWRIGHT", 
                        $"Matrix {core.matrixId} locked in ventral clamp! [{held}/4] matrices secured. Keep scanning the perimeter!", 
                        new Color(1f, 0.75f, 0.2f), 4.5f);
                }
            }
            else
            {
                // All 4 cores secured!
                currentStage = CoreRetrievalStage.ReturnToLandingPad;
                if (CollectorBetrayalManager.Instance != null)
                {
                    CollectorBetrayalManager.Instance.currentPhase = BetrayalPhase.WaitingForCoreReturn;
                }

                if (BlackMarketNPCDialogueHUD.Instance != null)
                {
                    BlackMarketNPCDialogueHUD.Instance.ShowMissionBanner("ALL 4 MATRICES SECURED // RETURN TO FLIGHT DECK PAD B-04");
                    BlackMarketNPCDialogueHUD.Instance.ShowTransmission("VOSS", "DATA FIXER", 
                        "Outstanding flying, Alita! All 4 matrices are locked in your cargo clamp. Return through the dome breach and land your starship back on Landing Pad B-04 on the flight deck! Bring them directly to my Sanctuary Pod [E] to claim your Mars coordinates!", 
                        new Color(0.2f, 0.95f, 1f), 8.0f);
                }

                if (CyberpunkHUDController.Instance != null)
                {
                    CyberpunkHUDController.Instance.SetQuestObjective(
                        "MISSION PAYOFF",
                        "► Return through dome breach and land on Landing Pad B-04 [E]"
                    );
                    CyberpunkHUDController.Instance.ShowToast("MATRICES SECURED: RETURN TO LANDING PAD B-04", 5.0f);
                }
            }
        }

        private void SetupLandingAudio()
        {
            if (_landingAudioSource != null) return;
            _landingClampClip = GenerateLandingClampClip();
            _landingAudioSource = gameObject.AddComponent<AudioSource>();
            _landingAudioSource.clip = _landingClampClip;
            _landingAudioSource.playOnAwake = false;
            _landingAudioSource.spatialBlend = 0.5f;
            _landingAudioSource.volume = 0.95f;
        }

        private AudioClip GenerateLandingClampClip()
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * 0.95f);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float clunk = Mathf.Sin(2f * Mathf.PI * 65f * t) * Mathf.Exp(-t * 12f) * 0.7f;
                float latch = Mathf.Sin(2f * Mathf.PI * 340f * t) * Mathf.Exp(-t * 22f) * 0.35f;
                float hiss = (Random.value * 2f - 1f) * Mathf.Clamp01(1f - t / 0.85f) * Mathf.Clamp01(t * 8f) * 0.25f;
                data[i] = Mathf.Clamp(clunk + latch + hiss, -1f, 1f);
            }
            var clip = AudioClip.Create("Landing_Clamp_Lock", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private void PlayLandingAudio()
        {
            if (_landingAudioSource == null) SetupLandingAudio();
            if (_landingAudioSource != null && _landingClampClip != null)
            {
                _landingAudioSource.PlayOneShot(_landingClampClip);
            }
        }

        // After collecting the cores the Nightshade lands on the western Pod Pad (SM_Bld_Pod_Pad_01),
        // where it was parked at the start, not on the eastern corporate pad the old coordinates pointed at.
        private Vector3 _alitaDisembarkPos;
        private bool _hasAlitaDisembarkPos;

        private void ConfigureLandingOnPodPad()
        {
            var pad = GameObject.Find("SM_Bld_Pod_Pad_01");
            if (pad == null)
            {
                Debug.LogWarning("[CoreRetrieval] SM_Bld_Pod_Pad_01 not found; keeping the default landing pad position.");
                return;
            }

            var rends = pad.GetComponentsInChildren<Renderer>();
            Bounds b = rends.Length > 0 ? rends[0].bounds : new Bounds(pad.transform.position, Vector3.one);
            foreach (var rd in rends) b.Encapsulate(rd.bounds);

            float groundY = GroundYAt(b.center.x, b.center.z, b.max.y + 25f, b.max.y);

            // Keep the ship's own pivot-to-ground offset so it sits on the deck exactly as it did when parked
            float rest = 1.2f;
            Quaternion rot = landingPadRot;
            if (nightshadeShip != null)
            {
                Vector3 sp = nightshadeShip.transform.position;
                float shipGround = GroundYAt(sp.x, sp.z, sp.y + 10f, sp.y);
                rest = Mathf.Clamp(sp.y - shipGround, 0.3f, 6f);
                rot = nightshadeShip.transform.rotation;
            }

            landingPadPos = new Vector3(b.center.x, groundY + rest, b.center.z);
            landingPadRot = rot;

            // Alita steps off beside the cockpit, on the deck
            Vector3 side = (rot * Vector3.right).normalized * 3.6f;
            _alitaDisembarkPos = new Vector3(landingPadPos.x + side.x, GroundYAt(landingPadPos.x + side.x, landingPadPos.z + side.z, landingPadPos.y + 6f, landingPadPos.y) + 0.05f, landingPadPos.z + side.z);
            _hasAlitaDisembarkPos = true;

            if (CollectorBetrayalManager.Instance != null) CollectorBetrayalManager.Instance.landingPadPos = landingPadPos;
            Debug.Log($"[CoreRetrieval] Landing pad set to SM_Bld_Pod_Pad_01 at {landingPadPos}");
        }

        private float GroundYAt(float x, float z, float fromY, float fallback)
        {
            var hits = Physics.RaycastAll(new Vector3(x, fromY, z), Vector3.down, 80f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity;
            foreach (var h in hits)
            {
                string n = h.collider.name.ToLowerInvariant();
                if (n.Contains("alita") || n.Contains("nightshade") || n.Contains("starship") || n.StartsWith("npc_")) continue;
                if (h.collider.GetComponentInParent<CharacterController>() != null) continue;
                if (h.point.y > best) best = h.point.y;
            }
            return float.IsNegativeInfinity(best) ? fallback : best;
        }

        private void SetupLandingPadVisuals()
        {
            if (_landingPadBeacon != null) return;
            var existing = GameObject.Find("Landing_Pad_B04_MissionBeacon");
            if (existing != null)
            {
                _landingPadBeacon = existing;
                _landingPadBeacon.transform.position = landingPadPos + Vector3.up * 0.08f;
                _landingPadBeacon.SetActive(false);
                return;
            }

            _landingPadBeacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _landingPadBeacon.name = "Landing_Pad_B04_MissionBeacon";
            _landingPadBeacon.transform.position = landingPadPos + Vector3.up * 0.08f;
            _landingPadBeacon.transform.localScale = new Vector3(8.5f, 0.04f, 8.5f);

            var col = _landingPadBeacon.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }

            var mr = _landingPadBeacon.GetComponent<MeshRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader);
            mat.color = new Color(0f, 1f, 0.65f, 0.55f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(0f, 1.8f, 1.1f) * 1.5f);
            mr.material = mat;

            var light = _landingPadBeacon.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0f, 1f, 0.65f);
            light.range = 22f;
            light.intensity = 3.5f;

            _landingPadBeacon.SetActive(false);
        }

        private void CheckLandingPadProximity()
        {
            if (nightshadeShip == null || _isLandingSequenceRunning) return;

            // If player already disembarked on/near pad
            if (!nightshadeShip.IsPiloted)
            {
                float pDist = Vector3.Distance(nightshadeShip.transform.position, landingPadPos);
                if (pDist < 14f)
                {
                    OnStarshipLandedOnPad();
                }
                return;
            }

            Vector3 shipPos = nightshadeShip.transform.position;
            float horizDist = Vector2.Distance(new Vector2(shipPos.x, shipPos.z), new Vector2(landingPadPos.x, landingPadPos.z));
            float vertDist = Mathf.Abs(shipPos.y - landingPadPos.y);

            // Approach zone check
            if (horizDist <= landingApproachRadius && vertDist <= 35f)
            {
                _isInLandingZone = true;

                // Auto-land condition:
                // 1. Ship touches down or hovers low directly over pad:
                bool isTouchdown = (horizDist <= landingPadDockRadius && shipPos.y <= (landingPadPos.y + 3.2f));
                // 2. Player presses [E] or [F] while aligned in the approach zone:
                bool isLandingKey = (horizDist <= 18f && vertDist <= 22f && (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F)));

                if (isTouchdown || isLandingKey)
                {
                    ExecuteLandingOnPad();
                }
            }
            else
            {
                _isInLandingZone = false;
            }
        }

        public void ExecuteLandingOnPad()
        {
            if (_isLandingSequenceRunning) return;
            StartCoroutine(ExecuteLandingPadTouchdownSequence());
        }

        private IEnumerator ExecuteLandingPadTouchdownSequence()
        {
            _isLandingSequenceRunning = true;

            if (CyberpunkHUDController.Instance != null)
            {
                CyberpunkHUDController.Instance.ShowToast("TOUCHDOWN SEQUENCE ENGAGED // PAD B-04", 3.0f);
            }

            if (nightshadeShip != null)
            {
                nightshadeShip.SetBoardingCooldown(2.5f);

                var rb = nightshadeShip.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    if (!rb.isKinematic)
                    {
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                    }
                    rb.isKinematic = true;
                }

                Vector3 startPos = nightshadeShip.transform.position;
                Quaternion startRot = nightshadeShip.transform.rotation;
                Vector3 targetPos = landingPadPos;
                Quaternion targetRot = landingPadRot;

                float duration = 1.3f;
                float elapsed = 0f;

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                    nightshadeShip.transform.position = Vector3.Lerp(startPos, targetPos, t);
                    nightshadeShip.transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
                    yield return null;
                }

                nightshadeShip.transform.position = targetPos;
                nightshadeShip.transform.rotation = targetRot;

                // Play clamp & landing audio
                PlayLandingAudio();

                // Disembark Alita safely onto the flight deck landing pad
                nightshadeShip.DisembarkPlayer();

                // Place Alita cleanly beside cockpit on the pad surface
                var player = GameObject.FindWithTag("Player");
                if (player != null)
                {
                    Vector3 safeAlitaPos = _hasAlitaDisembarkPos ? _alitaDisembarkPos : new Vector3(22.88f, 1.88f, 34.00f);
                    var cc = player.GetComponent<CharacterController>();
                    if (cc != null) cc.enabled = false;
                    player.transform.position = safeAlitaPos;
                    player.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
                    if (cc != null) cc.enabled = true;
                    Physics.SyncTransforms();
                }
            }

            OnStarshipLandedOnPad();
            _isLandingSequenceRunning = false;
        }

        private void OnStarshipLandedOnPad()
        {
            if (currentStage == CoreRetrievalStage.LandedReturnToCollector) return;

            currentStage = CoreRetrievalStage.LandedReturnToCollector;

            if (CollectorBetrayalManager.Instance != null)
            {
                CollectorBetrayalManager.Instance.currentPhase = BetrayalPhase.WaitingForCoreReturn;
                CollectorBetrayalManager.Instance.landingPadPos = landingPadPos;
            }

            if (BlackMarketNPCDialogueHUD.Instance != null)
            {
                BlackMarketNPCDialogueHUD.Instance.ShowMissionBanner("TOUCHDOWN CONFIRMED // DELIVER CORES TO VOSS");
                BlackMarketNPCDialogueHUD.Instance.ShowTransmission("VOSS", "DATA FIXER", 
                    "Starship touchdown confirmed on Pad B-04. Bring all 4 core matrices into my Sanctuary Pod [E] immediately, Alita!", 
                    new Color(0.2f, 0.95f, 1f), 7.5f);
            }

            if (CyberpunkHUDController.Instance != null)
            {
                CyberpunkHUDController.Instance.SetQuestObjective(
                    "DELIVER CORES // DRIFT SECTOR 04",
                    "► Enter The Collector's Sanctuary Pod and speak with Kaelen Voss [E]"
                );
                CyberpunkHUDController.Instance.ShowToast("STARSHIP SECURED: PROCEED TO SANCTUARY POD [E]", 5.0f);
            }
        }

        private void CheckInsertionBayProximity()
        {
            if (nightshadeShip == null) return;

            float dist = Vector3.Distance(nightshadeShip.transform.position, engineInsertionBayPos);
            if (dist <= insertionDockRadius)
            {
                // Initiate Core Insertion Sequence
                InitiateCoreInsertionSequence();
            }
        }

        public void InitiateCoreInsertionSequence()
        {
            currentStage = CoreRetrievalStage.InsertingCores;

            if (BlackMarketNPCDialogueHUD.Instance != null)
            {
                BlackMarketNPCDialogueHUD.Instance.ShowMissionBanner("VENTRAL DOCKING ENGAGED // INSERTING CORE MATRICES INTO ENGINE-01");
            }

            if (tractorBeam != null)
            {
                tractorBeam.ReleaseAllCoresToReceptor(engineInsertionBayPos, OnCoresInserted);
            }
            else
            {
                OnCoresInserted();
            }
        }

        private void OnCoresInserted()
        {
            StartCoroutine(ReactorStartupSequence());
        }

        private IEnumerator ReactorStartupSequence()
        {
            currentStage = CoreRetrievalStage.ReactorIgnited;

            // 1. Play massive reactor hum & ignition sound
            if (_reactorAudio != null)
            {
                _reactorAudio.Play();
            }

            // 2. Activate Reactor Ignition VFX Pillar
            if (reactorIgnitionVFX != null)
            {
                reactorIgnitionVFX.SetActive(true);
            }

            // 3. Flare reactor light
            float flareTimer = 0f;
            while (flareTimer < 2.5f)
            {
                flareTimer += Time.deltaTime;
                if (reactorShaftLight != null)
                {
                    reactorShaftLight.intensity = Mathf.Lerp(1.5f, 8.5f, flareTimer / 2.5f);
                    reactorShaftLight.range = Mathf.Lerp(75f, 150f, flareTimer / 2.5f);
                }
                yield return null;
            }

            // 4. Reactor Ignited -> Return to The Collector's Sanctuary Pod to claim coordinates
            currentStage = CoreRetrievalStage.ReactorIgnited;
            if (CollectorBetrayalManager.Instance != null)
            {
                CollectorBetrayalManager.Instance.currentPhase = BetrayalPhase.WaitingForCoreReturn;
            }

            if (BlackMarketNPCDialogueHUD.Instance != null)
            {
                BlackMarketNPCDialogueHUD.Instance.ShowMissionBanner("STATION REACTOR RESTORED // RETURN TO THE COLLECTOR'S SANCTUARY POD");
                BlackMarketNPCDialogueHUD.Instance.ShowTransmission("THE COLLECTOR", "SYNDICATE OVERLORD", 
                    "Reactor output is at 100%! Superb flying, Alita! Return to my trophy sanctuary pod on the Sector 4 concourse to claim your decrypted Mars coordinates.", 
                    new Color(0.95f, 0.75f, 0.15f), 9.0f);
            }

            if (CyberpunkHUDController.Instance != null)
            {
                CyberpunkHUDController.Instance.SetQuestObjective(
                    "CLAIM MARS COORDINATES",
                    "► Return to The Collector in his Trophy Sanctuary Pod to receive the coordinates"
                );
            }
        }

        #region HUD & Objective Overlay
        private void OnGUI()
        {
            if (SyntyCombined.Cutscene.GlobalCutsceneState.IsCutsceneActive) return;
            if (CollectorBetrayalManager.Instance != null && CollectorBetrayalManager.Instance.currentPhase >= BetrayalPhase.CoordinatesSpawned) return;

            float sw = Screen.width;
            float sh = Screen.height;

            int held = tractorBeam != null ? tractorBeam.HeldCoreCount : 0;
            bool isPiloting = nightshadeShip != null && nightshadeShip.IsPiloted;

            Vector3 targetWpPos = Vector3.zero;
            string wpLabel = "";
            string objTitle = null;
            string objBody = null;

            switch (currentStage)
            {
                case CoreRetrievalStage.Briefing:
                    objTitle = "MISSION: THE COLLECTOR // DRIFT SECTOR 04";
                    objBody = ">> LOCATE KAELEN VOSS <<\nENTER SANCTUARY POD ON CONCOURSE [E]";
                    targetWpPos = new Vector3(4.80f, 0.45f, -7.80f);
                    wpLabel = "THE COLLECTOR // KAELEN VOSS";
                    break;

                case CoreRetrievalStage.CollectingMatrices:
                    if (!isPiloting)
                    {
                        objTitle = "MISSION: ASTEROID CORE RETRIEVAL";
                        objBody = ">> BOARD NIGHTSHADE STRIKER <<\nHEAD TO WESTERN FLIGHT DECK [E]";
                        if (nightshadeShip != null) targetWpPos = nightshadeShip.transform.position;
                        wpLabel = "SS-04 NIGHTSHADE STRIKER // FLIGHT DECK";
                    }
                    else
                    {
                        objTitle = "MISSION: ASTEROID CORE RETRIEVAL";
                        objBody = $">> RETRIEVE SCATTERED MATRICES [{held} / 4] <<\nFLY OUT BROKEN DOME // TRACTOR BEAM [T]";
                    }
                    break;

                case CoreRetrievalStage.ReturnToLandingPad:
                    objTitle = "MISSION: RETURN TO FLIGHT DECK";
                    objBody = $">> ALL 4 MATRICES SECURED [{held}/4] <<\nRETURN THROUGH DOME & LAND ON PAD B-04 [E]";
                    targetWpPos = landingPadPos;
                    wpLabel = "LANDING PAD B-04 // WESTERN FLIGHT DECK";
                    break;

                case CoreRetrievalStage.LandedReturnToCollector:
                    objTitle = "MISSION: DELIVER THE 4 CORES";
                    objBody = ">> TOUCHDOWN CONFIRMED <<\nENTER SANCTUARY POD & TALK TO KAELEN VOSS [E]";
                    targetWpPos = new Vector3(4.80f, 0.45f, -7.80f);
                    wpLabel = "THE COLLECTOR // KAELEN VOSS";
                    break;

                case CoreRetrievalStage.InsertingCores:
                case CoreRetrievalStage.ReactorIgnited:
                case CoreRetrievalStage.HyperspaceWarpUnlocked:
                    objTitle = "MISSION COMPLETED: MARS COORDINATES DECRYPTED";
                    objBody = "WARP VECTOR LOCKED // PRESS [R] TO JUMP TO MARS!";
                    break;
            }

            // Objective text lives in the HUD quest tracker below the minimap
            PushObjectiveToHud(objTitle, objBody);

            // Draw landing alignment guidance HUD when approaching Landing Pad B-04
            if (currentStage == CoreRetrievalStage.ReturnToLandingPad && isPiloting && nightshadeShip != null)
            {
                Vector3 shipPos = nightshadeShip.transform.position;
                float horizDist = Vector2.Distance(new Vector2(shipPos.x, shipPos.z), new Vector2(landingPadPos.x, landingPadPos.z));
                float alt = shipPos.y - landingPadPos.y;

                if (horizDist < 45f)
                {
                    DrawLandingAlignmentHUD(sw, sh, horizDist, alt);
                }
            }

            // Draw 3D Waypoint Reticle when target is active
            if (targetWpPos != Vector3.zero && _mainCam != null)
            {
                Vector3 screenPos = _mainCam.WorldToScreenPoint(targetWpPos);
                if (screenPos.z > 0.5f)
                {
                    float cx = screenPos.x;
                    float cy = sh - screenPos.y;
                    float dist = screenPos.z;

                    GUI.color = new Color(0.1f, 1f, 0.4f, 0.95f);
                    DrawCircleReticle(cx, cy, 26f);

                    var wpStyle = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 11,
                        fontStyle = FontStyle.Bold
                    };
                    wpStyle.normal.textColor = new Color(0.1f, 1f, 0.4f);
                    GUI.Label(new Rect(cx - 140f, cy + 28f, 280f, 30f), $"{wpLabel}\n[{dist:F0}m]", wpStyle);
                }
            }

            GUI.color = Color.white;
        }

        private string _lastHudTitle, _lastHudBody;

        private void PushObjectiveToHud(string title, string body)
        {
            if (title == null || body == null) return;
            if (title == _lastHudTitle && body == _lastHudBody) return;
            _lastHudTitle = title;
            _lastHudBody = body;
            if (CyberpunkHUDController.Instance != null)
                CyberpunkHUDController.Instance.SetQuestObjective(title, body.Replace(">>", "").Replace("<<", "").Replace("\n", " ").Trim());
        }

        private void DrawBorder(Rect rect, int thickness)
        {
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);
        }

        private void DrawCircleReticle(float cx, float cy, float radius)
        {
            int segments = 16;
            for (int i = 0; i < segments; i++)
            {
                float a1 = (i / (float)segments) * Mathf.PI * 2f;
                float a2 = ((i + 1) / (float)segments) * Mathf.PI * 2f;
                Vector2 p1 = new Vector2(cx + Mathf.Cos(a1) * radius, cy + Mathf.Sin(a1) * radius);
                Vector2 p2 = new Vector2(cx + Mathf.Cos(a2) * radius, cy + Mathf.Sin(a2) * radius);
                DrawGuiLine(p1, p2, 2);
            }
        }

        private void DrawGuiLine(Vector2 p1, Vector2 p2, int thickness)
        {
            var matrix = GUI.matrix;
            float angle = Mathf.Atan2(p2.y - p1.y, p2.x - p1.x) * Mathf.Rad2Deg;
            float length = Vector2.Distance(p1, p2);

            GUIUtility.RotateAroundPivot(angle, p1);
            GUI.DrawTexture(new Rect(p1.x, p1.y - thickness * 0.5f, length, thickness), Texture2D.whiteTexture);
            GUI.matrix = matrix;
        }
        private void DrawLandingAlignmentHUD(float sw, float sh, float horizDist, float alt)
        {
            float boxW = 420f;
            float boxH = 78f;
            float boxX = (sw - boxW) * 0.5f;
            float boxY = sh - 165f;

            // Background banner
            GUI.color = new Color(0.02f, 0.05f, 0.10f, 0.88f);
            GUI.DrawTexture(new Rect(boxX, boxY, boxW, boxH), Texture2D.whiteTexture);

            bool isAligned = (horizDist <= 18f && alt <= 25f);
            Color accentCol = isAligned ? new Color(0f, 1f, 0.65f) : new Color(0f, 0.85f, 1f);
            GUI.color = accentCol;
            DrawBorder(new Rect(boxX, boxY, boxW, boxH), 2);

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
            titleStyle.normal.textColor = accentCol;

            var subStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                fontStyle = FontStyle.Normal
            };
            subStyle.normal.textColor = Color.white;

            string statusText = isAligned
                ? "▼ TOUCHDOWN ZONE LOCKED  |  PRESS [E] TO AUTO-LAND ▼"
                : "▲ FLIGHT DECK ALIGNMENT  |  DESCEND TO PAD B-04 ▲";

            GUI.Label(new Rect(boxX, boxY + 6f, boxW, 20f), statusText, titleStyle);
            GUI.Label(new Rect(boxX, boxY + 28f, boxW, 20f), $"RANGE: {horizDist:F1}m   |   ALTITUDE: {alt:F1}m   |   DECK: B-04", subStyle);

            if (isAligned)
            {
                var promptStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 11,
                    fontStyle = FontStyle.Bold
                };
                promptStyle.normal.textColor = new Color(1f, 0.9f, 0.2f);
                GUI.Label(new Rect(boxX, boxY + 50f, boxW, 20f), ">> TOUCH DOWN OR HIT [E] / [F] TO DOCK <<", promptStyle);
            }

            GUI.color = Color.white;
        }
        #endregion
    }
}
