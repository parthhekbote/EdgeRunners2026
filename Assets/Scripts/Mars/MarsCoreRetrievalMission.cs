using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using PolygonMech;
using InsaneGun.Combat;
using SyntyCombined.Cutscene;
using SyntyCombined.VFX;
using SyntyCombined;
using SyntyCombined.BlackMarket;
using Alita.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Cyberpunk.Mars
{
    public enum MarsMissionStage
    {
        None,
        InfiltrateCrashedWarship,
        CoreFound_AwaitingExtraction,
        CoreAbsorbed_ExitWarship,
        StarWarsInvasionEngaged,
        BossBattle
    }

    /// <summary>
    /// Playable mission on Mars: Finding the URM Antimatter Core inside the Crashed Spaceship.
    /// Manages:
    /// 1. Alita disembarks her starship and is in full 3rd-person player control.
    /// 2. HUD quest objectives and 3D screen-space reticle pointing to the Crashed URM Warship.
    /// 3. Infiltrating the crashed spaceship's breached hull and discovering the pulsating Antimatter Core.
    /// 4. Interactive [E] extraction and power absorption (500% Berserker overcharge, crackling lightning aura, glowing blade).
    /// 5. Exiting the crashed spaceship triggers the Star Wars Dreadnought arrival, Pilot Army surround, 360° ragdoll obliteration, and Titan Mech duel!
    /// </summary>
    public class MarsCoreRetrievalMission : MonoBehaviour
    {
        private static MarsCoreRetrievalMission _instance;
        public static MarsCoreRetrievalMission Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<MarsCoreRetrievalMission>() ?? FindObjectOfType<MarsCoreRetrievalMission>();
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("Mission Stage")]
        public MarsMissionStage currentStage = MarsMissionStage.None;

        [Header("Scene References")]
        public GameObject alitaHero;
        public GameObject crashedWarship;
        public MarsAntimatterCorePedestal corePedestal;
        public MarsArrivalCutsceneDirector cutsceneDirector;
        public AlitaSuperchargeAura superchargeAura;

        [Header("Positions & Thresholds")]
        public Vector3 crashedShipPos = new Vector3(0f, 0f, 115f);
        public Vector3 coreBayPos = new Vector3(0f, 0.40f, 103f);
        public Vector3 warshipExitPos = new Vector3(0f, 1.0f, 85f);
        public float coreInteractRadius = 3.8f;
        public float warshipExitZ = 86.0f;

        [Header("Visual & FX")]
        public GameObject coreExtractionVFXPrefab;
        public AudioClip extractionChimeAudio;

        // Internal State
        private bool _isPlayerNearCore = false;
        private bool _hasExitedWarship = false;
        private Camera _mainCam;
        private AudioSource _audioSource;

        private void Awake()
        {
            Instance = this;
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
        }

        private void Start()
        {
            FindSceneReferences();
            EnsureCrashedSpaceship();
        }

        public void FindSceneReferences()
        {
            if (alitaHero == null)
            {
                alitaHero = GameObject.Find("Alita") ?? GameObject.FindWithTag("Player");
            }

            if (corePedestal == null)
            {
                corePedestal = FindFirstObjectByType<MarsAntimatterCorePedestal>() ?? FindObjectOfType<MarsAntimatterCorePedestal>();
            }

            if (cutsceneDirector == null)
            {
                cutsceneDirector = FindFirstObjectByType<MarsArrivalCutsceneDirector>() ?? FindObjectOfType<MarsArrivalCutsceneDirector>();
            }

            if (superchargeAura == null && alitaHero != null)
            {
                superchargeAura = alitaHero.GetComponent<AlitaSuperchargeAura>() ?? alitaHero.AddComponent<AlitaSuperchargeAura>();
            }

            if (_mainCam == null)
            {
                _mainCam = Camera.main;
            }

            if (corePedestal != null)
            {
                coreBayPos = corePedestal.transform.position;
                crashedShipPos = corePedestal.transform.position + Vector3.forward * 8f;
                warshipExitPos = corePedestal.transform.position - Vector3.forward * 12f;
                warshipExitZ = warshipExitPos.z;
            }
        }

        /// <summary>
        /// Ensures the Crashed URM Warship is in the scene with interior core bay,
        /// breached hull entrance, and emergency lighting.
        /// </summary>
        public void EnsureCrashedSpaceship()
        {
            if (crashedWarship != null) return;

            var existing = GameObject.Find("Crashed_URM_Warship_Derelict");
            if (existing != null)
            {
                crashedWarship = existing;
                return;
            }

#if UNITY_EDITOR
            var shipPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonSciFiSpace/Prefabs/Vehicles/SM_Ship_Cruiser_01.prefab")
                          ?? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonSciFiWorlds/Prefabs/Buildings/SM_Bld_Scav_Wreckage_01.prefab");

            if (shipPrefab != null)
            {
                crashedWarship = (GameObject)PrefabUtility.InstantiatePrefab(shipPrefab);
                crashedWarship.name = "Crashed_URM_Warship_Derelict";
                crashedWarship.transform.position = crashedShipPos;
                crashedWarship.transform.rotation = Quaternion.Euler(7f, 180f, -4f);

                var lightObj = new GameObject("CoreBay_Emergency_Strobe");
                lightObj.transform.SetParent(crashedWarship.transform, false);
                lightObj.transform.localPosition = new Vector3(0f, 2.5f, 0f);
                var l = lightObj.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(0.1f, 0.9f, 1.0f);
                l.range = 18f;
                l.intensity = 2.8f;

                if (corePedestal != null)
                {
                    corePedestal.transform.position = coreBayPos;
                }
            }
#endif
        }

        /// <summary>
        /// Starts the playable Core Retrieval Mission on foot on Mars.
        /// </summary>
        public void StartCoreRetrievalMission()
        {
            FindSceneReferences();
            currentStage = MarsMissionStage.InfiltrateCrashedWarship;

            // Unlock Alita's third-person player controls
            if (alitaHero != null)
            {
                var swordCtrl = alitaHero.GetComponent<InsaneGun.Combat.InsaneGunSwordController>();
                if (swordCtrl != null)
                {
                    swordCtrl.controlMode = InsaneGun.Combat.InsaneGunSwordController.ControlMode.PlayerControlled;
                    swordCtrl.isInvincible = false;
                }

                var cc = alitaHero.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = true;
            }

            // Restore third-person camera
            if (_mainCam != null)
            {
                var orbit = _mainCam.GetComponent<SmoothOrbitCamera>();
                if (orbit != null)
                {
                    orbit.enabled = true;
                    if (alitaHero != null) orbit.target = alitaHero.transform;
                }
            }

            if (cutsceneDirector != null)
            {
                cutsceneDirector.PlayPostLandingAudio();
            }

            // Show HUD Quest Objective
            if (CyberpunkHUDController.Instance != null)
            {
                CyberpunkHUDController.Instance.SetQuestObjective(
                    "OPERATION RED HORIZON // CYDONIA BASIN",
                    "► Infiltrate the crashed URM Warship & recover the Antimatter Berserker Core"
                );
                CyberpunkHUDController.Instance.ShowToast("TOUCHDOWN CONFIRMED: EXPLORE CRASHED DERELICT", 5.0f);
            }

            if (BlackMarketNPCDialogueHUD.Instance != null)
            {
                BlackMarketNPCDialogueHUD.Instance.ShowMissionBanner("MISSION ACTIVE: INFILTRATE CRASHED URM WARSHIP & RECOVER CORE");
                BlackMarketNPCDialogueHUD.Instance.ShowTransmission(
                    "ALITA",
                    "URM TACTICAL SENSORS",
                    "Touchdown confirmed in Cydonia Basin. Sensors detect an ancient URM battlecruiser half-buried in the dunes ahead. The Berserker Heart is inside the reactor bay!",
                    new Color(0.2f, 0.92f, 1.0f),
                    6.5f
                );
            }
        }

        private void Update()
        {
            // Developer test shortcuts
            if (Input.GetKeyDown(KeyCode.F5))
            {
                TestTeleportToCore();
            }
            if (Input.GetKeyDown(KeyCode.F6))
            {
                TestTriggerStarWarsInvasion();
            }
            if (Input.GetKeyDown(KeyCode.F7))
            {
                TestTriggerPilotObliteration();
            }

            if (currentStage == MarsMissionStage.None) return;

            if (alitaHero == null) FindSceneReferences();
            if (alitaHero == null) return;

            Vector3 playerPos = alitaHero.transform.position;

            // 1. Check distance to Core Pedestal
            if (currentStage == MarsMissionStage.InfiltrateCrashedWarship || currentStage == MarsMissionStage.CoreFound_AwaitingExtraction)
            {
                Vector3 corePos = corePedestal != null ? corePedestal.transform.position : coreBayPos;
                float dist = Vector3.Distance(playerPos, corePos);
                _isPlayerNearCore = dist <= coreInteractRadius;

                if (_isPlayerNearCore)
                {
                    currentStage = MarsMissionStage.CoreFound_AwaitingExtraction;

                    // Press [E] or [F] to extract and absorb the core
                    if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F) || dist < 1.4f)
                    {
                        ExtractAndAbsorbCore();
                    }
                }
                else
                {
                    currentStage = MarsMissionStage.InfiltrateCrashedWarship;
                }
            }

            // 2. Check if player has exited the crashed warship after absorbing the core
            if (currentStage == MarsMissionStage.CoreAbsorbed_ExitWarship && !_hasExitedWarship)
            {
                // Exiting toward the central basin (z decreases toward combat arena)
                if (playerPos.z <= warshipExitZ || Vector3.Distance(playerPos, warshipExitPos) < 4.0f)
                {
                    _hasExitedWarship = true;
                    OnPlayerExitedWarship();
                }
            }
        }

        /// <summary>
        /// Alita extracts the ancient URM Antimatter Berserker Core from the reactor bay.
        /// </summary>
        public void ExtractAndAbsorbCore()
        {
            if (currentStage != MarsMissionStage.CoreFound_AwaitingExtraction) return;

            currentStage = MarsMissionStage.CoreAbsorbed_ExitWarship;

            // Play absorption sequence on pedestal
            if (corePedestal != null && alitaHero != null)
            {
                corePedestal.StartAbsorptionSequence(alitaHero.transform, OnCoreAbsorptionComplete);
            }
            else
            {
                OnCoreAbsorptionComplete();
            }

            // Play audio surge
            PlayExtractionSound();
        }

        private void OnCoreAbsorptionComplete()
        {
            // Activate Alita's crackling plasma supercharge aura and blade glow
            if (superchargeAura != null)
            {
                superchargeAura.ActivateSupercharge();
            }

            if (CyberpunkHUDController.Instance != null)
            {
                CyberpunkHUDController.Instance.SetQuestObjective(
                    "POWER OVERCHARGE",
                    "► Berserker Heart Synchronized (500% Output)! Exit the warship into the Cydonia Basin"
                );
                CyberpunkHUDController.Instance.ShowToast("CRITICAL: URM COMBAT LIMITERS DISENGAGED", 5.0f);
            }

            if (BlackMarketNPCDialogueHUD.Instance != null)
            {
                BlackMarketNPCDialogueHUD.Instance.ShowMissionBanner("⚡ URM ANTIMATTER BERSERKER HEART SYNCHRONIZED — COMBAT POWER AT 500%!");
                BlackMarketNPCDialogueHUD.Instance.ShowTransmission(
                    "ALITA",
                    "URM BERSERKER // OVERLOAD",
                    "Berserker core integrated... Systems operating at 500% capacity! All combat limiters disengaged!",
                    new Color(0.95f, 0.95f, 1.0f),
                    6.0f
                );
            }
        }

        /// <summary>
        /// Triggered when Alita exits the crashed warship into the Cydonia Basin.
        /// Initiates the epic Star Wars Dreadnought arrival, Pilot Army surround, and boss duel!
        /// </summary>
        public void OnPlayerExitedWarship()
        {
            currentStage = MarsMissionStage.StarWarsInvasionEngaged;

            if (CyberpunkHUDController.Instance != null)
            {
                CyberpunkHUDController.Instance.SetQuestObjective(
                    "IMPERIAL SIEGE",
                    "► SURVIVE THE DREADNOUGHT INVASION & TITAN WAR MECH ASSAULT!"
                );
                CyberpunkHUDController.Instance.ShowToast("SEISMIC ANOMALY: DREADNOUGHTS BREACHING CLOUDS", 4.5f);
            }

            // Trigger the cinematic Star Wars Dreadnought arrival & Mech drop
            if (cutsceneDirector != null)
            {
                cutsceneDirector.PlayStarWarsInvasionSequence();
            }
        }

        private void PlayExtractionSound()
        {
            if (_audioSource == null) return;
            // High-voltage synthetic electrical warp surge
            int rate = 44100;
            int len = (int)(rate * 1.8f);
            float[] data = new float[len];
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / rate;
                float freq = Mathf.Lerp(110f, 1200f, t / 1.8f);
                float env = Mathf.Clamp01(t / 0.3f) * Mathf.Exp(-2.2f * t);
                data[i] = (Mathf.Sin(2f * Mathf.PI * freq * t) * 0.5f + (UnityEngine.Random.value * 2f - 1f) * 0.25f) * env;
            }
            var clip = AudioClip.Create("Core_Extraction_Surge", len, 1, rate, false);
            clip.SetData(data, 0);
            _audioSource.PlayOneShot(clip, 1.0f);
        }

        #region HUD & 3D Waypoint Reticle
        private void OnGUI()
        {
            if (currentStage == MarsMissionStage.None || currentStage == MarsMissionStage.BossBattle) return;
            if (SyntyCombined.Cutscene.GlobalCutsceneState.IsCutsceneActive) return;

            float sw = Screen.width;
            float sh = Screen.height;

            // 1. Mission Card (Top-Left)
            float cardW = 420f;
            float cardH = 68f;
            float cardX = 24f;
            float cardY = 22f;

            GUI.color = new Color(0.04f, 0.05f, 0.08f, 0.92f);
            GUI.DrawTexture(new Rect(cardX, cardY, cardW, cardH), Texture2D.whiteTexture);
            GUI.color = new Color(1.0f, 0.45f, 0.15f, 0.95f);
            DrawBorder(new Rect(cardX, cardY, cardW, cardH), 1);

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };
            titleStyle.normal.textColor = new Color(1.0f, 0.5f, 0.15f);

            var subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
            subStyle.normal.textColor = Color.white;

            switch (currentStage)
            {
                case MarsMissionStage.InfiltrateCrashedWarship:
                    GUI.Label(new Rect(cardX + 12f, cardY + 8f, cardW - 24f, 18f), "MISSION: THE BERSERKER HEART // CYDONIA BASIN", titleStyle);
                    GUI.Label(new Rect(cardX + 12f, cardY + 28f, cardW - 24f, 32f), ">> INFILTRATE CRASHED URM WARSHIP <<\nLOCATE THE ANTIMATTER BERSERKER CORE", subStyle);
                    break;

                case MarsMissionStage.CoreFound_AwaitingExtraction:
                    GUI.Label(new Rect(cardX + 12f, cardY + 8f, cardW - 24f, 18f), "MISSION: THE BERSERKER HEART // CYDONIA BASIN", titleStyle);
                    subStyle.normal.textColor = new Color(0.1f, 0.95f, 1.0f);
                    GUI.Label(new Rect(cardX + 12f, cardY + 28f, cardW - 24f, 32f), ">> ANTIMATTER CORE LOCATED <<\nPRESS [E] TO EXTRACT & ABSORB POWER", subStyle);
                    break;

                case MarsMissionStage.CoreAbsorbed_ExitWarship:
                    GUI.Label(new Rect(cardX + 12f, cardY + 8f, cardW - 24f, 18f), "URM CORE ABSORPTION COMPLETE // COMBAT POWER 500%", titleStyle);
                    subStyle.normal.textColor = new Color(0.2f, 1.0f, 0.4f);
                    GUI.Label(new Rect(cardX + 12f, cardY + 28f, cardW - 24f, 32f), ">> CRITICAL ENERGY OVERCHARGE ACTIVE <<\nEXIT WARSHIP DERELICT INTO CYDONIA BASIN", subStyle);
                    break;
            }

            // 2. Interaction Prompt when near Core
            if (_isPlayerNearCore && currentStage == MarsMissionStage.CoreFound_AwaitingExtraction)
            {
                float promptW = 380f;
                float promptH = 36f;
                float promptX = (sw - promptW) * 0.5f;
                float promptY = sh * 0.72f;

                GUI.color = new Color(0.02f, 0.05f, 0.10f, 0.92f);
                GUI.DrawTexture(new Rect(promptX, promptY, promptW, promptH), Texture2D.whiteTexture);
                GUI.color = new Color(0.1f, 0.95f, 1.0f, 0.95f);
                DrawBorder(new Rect(promptX, promptY, promptW, promptH), 1);

                var promptStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 13,
                    fontStyle = FontStyle.Bold
                };
                promptStyle.normal.textColor = new Color(0.2f, 1.0f, 1.0f);
                GUI.Label(new Rect(promptX, promptY, promptW, promptH), "[E] EXTRACT & ABSORB URM ANTIMATTER CORE", promptStyle);
            }

            // 3. 3D Waypoint Reticle in screen space
            if (_mainCam != null)
            {
                Vector3 targetWp = (currentStage == MarsMissionStage.CoreAbsorbed_ExitWarship) ? warshipExitPos : (corePedestal != null ? corePedestal.transform.position : coreBayPos);
                Vector3 screenPos = _mainCam.WorldToScreenPoint(targetWp);

                if (screenPos.z > 0.5f)
                {
                    float cx = screenPos.x;
                    float cy = sh - screenPos.y;
                    float dist = screenPos.z;

                    GUI.color = (currentStage == MarsMissionStage.CoreAbsorbed_ExitWarship) ? new Color(0.2f, 1f, 0.4f, 0.95f) : new Color(0.1f, 0.95f, 1f, 0.95f);
                    DrawCircleReticle(cx, cy, 26f);

                    var wpStyle = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 11,
                        fontStyle = FontStyle.Bold
                    };
                    wpStyle.normal.textColor = GUI.color;

                    string wpText = (currentStage == MarsMissionStage.CoreAbsorbed_ExitWarship)
                        ? $"WARSHIP HULL EXIT\n[{dist:F0}m]"
                        : $"URM CRASHED WARSHIP // CORE BAY\n[{dist:F0}m]";

                    GUI.Label(new Rect(cx - 130f, cy + 28f, 260f, 30f), wpText, wpStyle);
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
                float a1 = i * (Mathf.PI * 2f / segments);
                float a2 = (i + 1) * (Mathf.PI * 2f / segments);
                Vector2 p1 = new Vector2(cx + Mathf.Cos(a1) * radius, cy + Mathf.Sin(a1) * radius);
                Vector2 p2 = new Vector2(cx + Mathf.Cos(a2) * radius, cy + Mathf.Sin(a2) * radius);
                DrawLine(p1, p2, 2f);
            }
        }

        private void DrawLine(Vector2 p1, Vector2 p2, float width)
        {
            Vector2 d = p2 - p1;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            GUIUtility.RotateAroundPivot(angle, p1);
            GUI.DrawTexture(new Rect(p1.x, p1.y - width * 0.5f, d.magnitude, width), Texture2D.whiteTexture);
            GUIUtility.RotateAroundPivot(-angle, p1);
        }
        #endregion

        #region Developer Test Shortcuts
        [ContextMenu("Test Teleport To Core (F5)")]
        public void TestTeleportToCore()
        {
            FindSceneReferences();
            if (alitaHero != null)
            {
                alitaHero.transform.position = coreBayPos + Vector3.back * 2.5f;
                alitaHero.transform.rotation = Quaternion.identity;
                Physics.SyncTransforms();
            }
            currentStage = MarsMissionStage.CoreFound_AwaitingExtraction;
        }

        [ContextMenu("Test Trigger Star Wars Invasion (F6)")]
        public void TestTriggerStarWarsInvasion()
        {
            FindSceneReferences();
            if (alitaHero != null)
            {
                alitaHero.transform.position = warshipExitPos + Vector3.back * 4f;
                Physics.SyncTransforms();
            }
            if (superchargeAura != null)
            {
                superchargeAura.ActivateSupercharge();
            }
            OnPlayerExitedWarship();
        }

        [ContextMenu("Test Trigger Pilot Obliteration (F7)")]
        public void TestTriggerPilotObliteration()
        {
            FindSceneReferences();
            if (cutsceneDirector != null && cutsceneDirector.armyManager != null && alitaHero != null)
            {
                cutsceneDirector.armyManager.TriggerArmyOneShotObliteration(alitaHero.transform.position, 32f, 36f);
            }
        }
        #endregion
    }
}
