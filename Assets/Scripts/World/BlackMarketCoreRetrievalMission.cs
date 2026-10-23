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
        Briefing,
        CollectingMatrices,
        ReturnToCoreEngine,
        InsertingCores,
        ReactorIgnited,
        HyperspaceWarpUnlocked
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
            SpawnCoreMatricesIfNeeded();
            SetupReactorBay();

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

            // Check if player is near insertion bay when all 4 cores are secured
            if (currentStage == CoreRetrievalStage.ReturnToCoreEngine)
            {
                CheckInsertionBayProximity();
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
                currentStage = CoreRetrievalStage.ReturnToCoreEngine;
                if (CollectorBetrayalManager.Instance != null)
                {
                    CollectorBetrayalManager.Instance.currentPhase = BetrayalPhase.WaitingForCoreReturn;
                }

                if (BlackMarketNPCDialogueHUD.Instance != null)
                {
                    BlackMarketNPCDialogueHUD.Instance.ShowMissionBanner("ALL 4 MATRICES SECURED // RETURN TO THE COLLECTOR'S SANCTUARY POD");
                    BlackMarketNPCDialogueHUD.Instance.ShowTransmission("VOSS", "DATA FIXER", 
                        "Outstanding flying, Alita! All 4 matrices are locked in your cargo clamp. Land your starship on Landing Pad B-04 / Flight Deck, and bring them directly to my Sanctuary Pod [E] to claim your Mars coordinates!", 
                        new Color(0.2f, 0.95f, 1f), 8.0f);
                }

                if (CyberpunkHUDController.Instance != null)
                {
                    CyberpunkHUDController.Instance.SetQuestObjective(
                        "MISSION PAYOFF",
                        "► Land starship on Landing Pad B-04 & deliver cores to The Collector in his Sanctuary Pod [E]"
                    );
                    CyberpunkHUDController.Instance.ShowToast("MATRICES SECURED: RETURN TO COLLECTOR'S POD [E]", 5.0f);
                }
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

            // Mission Objective Card (Top Left)
            float objW = 420f;
            float objH = 68f;
            float objX = 24f;
            float objY = 22f;

            GUI.color = new Color(0.02f, 0.05f, 0.10f, 0.90f);
            GUI.DrawTexture(new Rect(objX, objY, objW, objH), Texture2D.whiteTexture);
            GUI.color = new Color(0f, 0.94f, 1.0f, 0.95f);
            DrawBorder(new Rect(objX, objY, objW, objH), 1);

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };
            titleStyle.normal.textColor = new Color(0f, 0.95f, 1f);

            var subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
            subStyle.normal.textColor = Color.white;

            int held = tractorBeam != null ? tractorBeam.HeldCoreCount : 0;
            bool isPiloting = nightshadeShip != null && nightshadeShip.IsPiloted;

            Vector3 targetWpPos = Vector3.zero;
            string wpLabel = "";

            switch (currentStage)
            {
                case CoreRetrievalStage.Briefing:
                    GUI.Label(new Rect(objX + 12f, objY + 8f, objW - 24f, 18f), "MISSION: THE COLLECTOR // DRIFT SECTOR 04", titleStyle);
                    GUI.Label(new Rect(objX + 12f, objY + 28f, objW - 24f, 32f), ">> LOCATE KAELEN VOSS <<\nENTER SANCTUARY POD ON CONCOURSE [E]", subStyle);
                    targetWpPos = new Vector3(4.80f, 0.45f, -7.80f);
                    wpLabel = "THE COLLECTOR // KAELEN VOSS";
                    break;

                case CoreRetrievalStage.CollectingMatrices:
                    if (!isPiloting)
                    {
                        GUI.Label(new Rect(objX + 12f, objY + 8f, objW - 24f, 18f), "MISSION: ASTEROID CORE RETRIEVAL", titleStyle);
                        GUI.Label(new Rect(objX + 12f, objY + 28f, objW - 24f, 32f), ">> BOARD NIGHTSHADE STRIKER <<\nHEAD TO WESTERN FLIGHT DECK [E]", subStyle);
                        if (nightshadeShip != null) targetWpPos = nightshadeShip.transform.position;
                        wpLabel = "SS-04 NIGHTSHADE STRIKER // FLIGHT DECK";
                    }
                    else
                    {
                        GUI.Label(new Rect(objX + 12f, objY + 8f, objW - 24f, 18f), "MISSION: ASTEROID CORE RETRIEVAL", titleStyle);
                        GUI.Label(new Rect(objX + 12f, objY + 28f, objW - 24f, 32f), $">> RETRIEVE SCATTERED MATRICES [{held} / 4] <<\nFLY OUT BROKEN DOME // TRACTOR BEAM [T]", subStyle);
                    }
                    break;

                case CoreRetrievalStage.ReturnToCoreEngine:
                    GUI.Label(new Rect(objX + 12f, objY + 8f, objW - 24f, 18f), "MISSION: DELIVER THE 4 CORES", titleStyle);
                    subStyle.normal.textColor = new Color(0.2f, 1f, 0.5f);
                    GUI.Label(new Rect(objX + 12f, objY + 28f, objW - 24f, 32f), $">> ALL 4 MATRICES SECURED <<\nLAND ON PAD B-04 & TALK TO THE COLLECTOR [E]", subStyle);
                    targetWpPos = (CollectorBetrayalManager.Instance != null) ? CollectorBetrayalManager.Instance.landingPadPos : new Vector3(22.0f, 1.88f, 33.5f);
                    wpLabel = "LANDING PAD B-04 // THE COLLECTOR";
                    break;

                case CoreRetrievalStage.InsertingCores:
                case CoreRetrievalStage.ReactorIgnited:
                case CoreRetrievalStage.HyperspaceWarpUnlocked:
                    GUI.Label(new Rect(objX + 12f, objY + 8f, objW - 24f, 18f), "MISSION COMPLETED: MARS COORDINATES DECRYPTED", titleStyle);
                    subStyle.normal.textColor = new Color(0.1f, 1f, 0.4f);
                    GUI.Label(new Rect(objX + 12f, objY + 28f, objW - 24f, 32f), "WARP VECTOR LOCKED // PRESS [R] TO JUMP TO MARS!", subStyle);
                    break;
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
        #endregion
    }
}
