using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using InsaneGun.Combat;
using CyberCombat.Audio;
using SyntyCombined;
using Cyberpunk.Inventory;
using Cyberpunk.Audio;
using Cyberpunk.World;

namespace Cyberpunk.Missions
{
    /// <summary>
    /// Master Arena Combat Director for Mission 3: The Pit.
    /// Simplified & Pure 1v1 Gladiator Ladder:
    /// - 1 Win = Instant Money Payout (€$)!
    /// - More Wins = Tougher Opponents (Higher HP, Damage, Speed, and Abilities).
    /// - Zero complicated combo or favor penalties.
    /// - Dynamic crowd cheering & battle drums blended seamlessly.
    /// </summary>
    public class Mission3_PitFightManager : MonoBehaviour
    {
        public static Mission3_PitFightManager Instance { get; private set; }

        public enum PitState
        {
            NotStarted,
            RoundActive,
            Breather,
            VulcanShowdown,
            PlayerDead,
            CashedOut
        }

        [Header("Match Progression")]
        public PitState currentState = PitState.NotStarted;
        public int currentRound = 1;
        public int bestStreak = 0;
        public int totalSessionPurse = 0;

        [Header("Breather Settings")]
        public float breatherDuration = 4.0f;
        public float currentBreatherTime = 0f;

        [Header("Positions")]
        public Vector3 playerSpawnPos = new Vector3(0f, 0.854f, -7.5f);
        public Vector3 rivalSpawnPos = new Vector3(0f, 0.854f, 7.5f);
        public Quaternion rivalSpawnRot = Quaternion.Euler(0f, 180f, 0f);

        [Header("Combatants")]
        public InsaneGunSwordController player;
        public InsaneGunSwordController rival;

        // Visuals & Lighting
        private Material _rivalKatanaMat;
        private Light _rivalKatanaLight;

        [Header("Audio & Dramatic Entrance")]
        public AudioSource audioSource;
        private static AudioClip _teleportDropClip;
        private static AudioClip _gongBellClip;
        private Coroutine _entranceRoutine;
        private float _cullingCheckTimer = 0f;

        // GUI Styles
        private GUIStyle _rebootHeaderStyle;
        private GUIStyle _rebootSubStyle;
        private GUIStyle _rebootBodyStyle;
        private GUIStyle _rebootBtnStyle;
        private GUIStyle _breatherTitleStyle;
        private GUIStyle _breatherSubStyle;
        private Texture2D _modalBgTex;
        private Texture2D _btnBgTex;
        private Texture2D _btnHoverTex;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrapInArena()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;

            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.name.IndexOf("Combat_Arena", StringComparison.OrdinalIgnoreCase) >= 0 ||
                activeScene.name.IndexOf("Indoor_Arena", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                EnsureInArena();
            }
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name.IndexOf("Combat_Arena", StringComparison.OrdinalIgnoreCase) >= 0 ||
                scene.name.IndexOf("Indoor_Arena", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                EnsureInArena();
            }
        }

        public static Mission3_PitFightManager EnsureInArena()
        {
            EnsureAudioListener();

            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<Mission3_PitFightManager>();
            if (existing != null)
            {
                Instance = existing;
                return Instance;
            }

            GameObject go = new GameObject("[Mission3_Pit_Fight_Manager]");
            Instance = go.AddComponent<Mission3_PitFightManager>();
            return Instance;
        }

        private static void EnsureAudioListener()
        {
            var listener = FindAnyObjectByType<AudioListener>();
            if (listener == null)
            {
                var cam = Camera.main ?? FindAnyObjectByType<Camera>();
                if (cam != null)
                {
                    cam.gameObject.AddComponent<AudioListener>();
                    Debug.Log("[Mission3Pit] Added missing AudioListener to " + cam.gameObject.name);
                }
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

            if (audioSource == null) audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.35f;

            EnsureArenaFloorSafety();

            bestStreak = PlayerPrefs.GetInt("pit_best_streak", 0);
            BuildGUIStyles();
        }

        private void Start()
        {
            StartCoroutine(InitializeArenaRoutine());
        }

        private IEnumerator InitializeArenaRoutine()
        {
            yield return null;

            EnsureAudioListener();

            // Guarantee pristine championship arena lighting and ACES volume
            EnforceChampionshipLighting();

            // Purge all rogue Blackwall and drone audio loops
            var allSources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
            foreach (var src in allSources)
            {
                if (src == null) continue;
                string clipName = src.clip != null ? src.clip.name.ToLowerInvariant() : "";
                string goName = src.gameObject.name.ToLowerInvariant();
                if (clipName.Contains("blackwall") || clipName.Contains("cynosure") ||
                    clipName.Contains("ghost-attack") || clipName.Contains("drone-sound") ||
                    goName.Contains("drone"))
                {
                    src.Stop();
                    src.clip = null;
                    src.enabled = false;
                    Destroy(src);
                }
            }

            // Clean up rogue sandbox / Blackwall objects
            var blackwall = GameObject.Find("Blackwall_Holographic_Enemy_Spawner");
            if (blackwall != null) Destroy(blackwall);
            var bwComps = FindObjectsByType<BlackwallHolographicSpawner>(FindObjectsSortMode.None);
            foreach (var bw in bwComps) Destroy(bw.gameObject);

            var bwHud = FindAnyObjectByType<SyntyCombined.BlackwallStoryDialogueHUD>();
            if (bwHud != null) Destroy(bwHud.gameObject);
            var bwBarriers = FindObjectsByType<SyntyCombined.BlackwallRogueAIBarrier>(FindObjectsSortMode.None);
            foreach (var b in bwBarriers) Destroy(b.gameObject);
            var bwEncounters = FindObjectsByType<InsaneGun.Combat.BlackwallRogueAIEncounter>(FindObjectsSortMode.None);
            foreach (var e in bwEncounters) Destroy(e.gameObject);

            var oldTestManagers = FindObjectsByType<InsaneGun.Testing.InsaneGunCombatTestManager>(FindObjectsSortMode.None);
            foreach (var otm in oldTestManagers)
            {
                otm.enabled = false;
                Destroy(otm);
            }

            LocateCombatants();
            CacheRivalVisuals();

            CyberpunkInventory.EnsureInScene();

            currentRound = 1;
            totalSessionPurse = 0;
            StartRound(currentRound);
        }

        public void LocateCombatants()
        {
            // 1. Locate Player
            var pGos = GameObject.FindGameObjectsWithTag("Player");
            foreach (var go in pGos)
            {
                var ctrl = go.GetComponent<InsaneGunSwordController>();
                if (ctrl != null && ctrl.controlMode == InsaneGunSwordController.ControlMode.PlayerControlled)
                {
                    player = ctrl;
                    break;
                }
            }

            if (player == null)
            {
                var allControllers = FindObjectsByType<InsaneGunSwordController>(FindObjectsSortMode.None);
                foreach (var c in allControllers)
                {
                    if (c.controlMode == InsaneGunSwordController.ControlMode.PlayerControlled)
                    {
                        player = c;
                        break;
                    }
                }
            }

            // Ensure authentic Alita Battle Angel model
            EnsureAlitaCombatant();

            // 2. Locate Rival
            var rivalGo = GameObject.Find("Rival_Cyber_Duelist");
            if (rivalGo != null)
            {
                rival = rivalGo.GetComponent<InsaneGunSwordController>();
            }

            if (rival == null)
            {
                var allControllers = FindObjectsByType<InsaneGunSwordController>(FindObjectsSortMode.None);
                foreach (var c in allControllers)
                {
                    if (c.controlMode == InsaneGunSwordController.ControlMode.AICirclingEnemy ||
                        c.gameObject.name.IndexOf("Rival", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        rival = c;
                        break;
                    }
                }
            }

            // 3. Enforce Strict Combatant Setup (Layers, EnemyLayers, Tags & Targets)
            int pLayer = LayerMask.NameToLayer("Player");
            int eLayer = LayerMask.NameToLayer("Enemy");
            if (pLayer == -1) pLayer = 8;
            if (eLayer == -1) eLayer = 9;

            if (player != null)
            {
                player.gameObject.layer = pLayer;
                player.gameObject.tag = "Player";
                player.enemyLayers = (1 << eLayer) | (1 << 0);
                if (rival != null) player.targetEnemy = rival.transform;

                var pAnim = player.GetComponent<Animator>();
                if (pAnim != null) pAnim.applyRootMotion = false;

                player.OnDamaged -= HandlePlayerDamaged;
                player.OnDamaged += HandlePlayerDamaged;
                player.OnDeath -= HandlePlayerDeath;
                player.OnDeath += HandlePlayerDeath;
            }

            if (rival != null)
            {
                rival.gameObject.layer = eLayer;
                rival.gameObject.tag = "Enemy";
                rival.enemyLayers = (1 << pLayer) | (1 << 0);
                rival.controlMode = InsaneGunSwordController.ControlMode.AICirclingEnemy;
                if (player != null) rival.targetEnemy = player.transform;

                // Enforce humanoid CharacterController bounds: feet flush at Y=0.0
                var rCc = rival.GetComponent<CharacterController>();
                if (rCc != null)
                {
                    rCc.center = new Vector3(0f, 0.95f, 0f);
                    rCc.height = 1.90f;
                    rCc.radius = 0.42f;
                    rCc.stepOffset = 0.30f;
                }

                // Remove conflicting root CapsuleColliders so character never floats
                var redundantCaps = rival.GetComponents<CapsuleCollider>();
                foreach (var rc in redundantCaps)
                {
                    Destroy(rc);
                }

                var rAnim = rival.GetComponent<Animator>();
                if (rAnim != null) rAnim.applyRootMotion = false;

                rival.OnDamaged -= HandleRivalDamaged;
                rival.OnDamaged += HandleRivalDamaged;
                rival.OnDeath -= HandleRivalDeath;
                rival.OnDeath += HandleRivalDeath;
            }

            // Enforce visibility anti-culling fix on all combatants
            FixCombatantInvisibilityAndCulling(player);
            FixCombatantInvisibilityAndCulling(rival);
        }

        private void EnsureAlitaCombatant()
        {
            bool isRealAlita = false;
            if (player != null)
            {
                // Real Alita prefab has Alita_Blood_EyeStripes, Stripe_L, Stripe_R, or Damascus_Blade
                if (player.transform.Find("Alita_Blood_EyeStripes") != null ||
                    player.transform.Find("Stripe_L") != null ||
                    player.transform.Find("mesh") != null)
                {
                    isRealAlita = true;
                }

                // Check if it has the robot female child from the previous setup
                if (player.transform.Find("SM_Chr_Robot_Female_01") != null)
                {
                    isRealAlita = false;
                }
            }

            if (isRealAlita) return;

            Debug.Log("[Mission3Pit] Current player model is not Alita (found generic or robot character). Replacing with Assets/Prefabs/Alita_BattleAngel_Hero.prefab...");

            Vector3 spawnPos = (player != null) ? player.transform.position : playerSpawnPos;
            Quaternion spawnRot = (player != null) ? player.transform.rotation : Quaternion.identity;

            GameObject alitaPrefab = null;
#if UNITY_EDITOR
            alitaPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Alita_BattleAngel_Hero.prefab");
#endif
            if (alitaPrefab == null)
            {
                alitaPrefab = Resources.Load<GameObject>("Prefabs/Alita_BattleAngel_Hero");
            }

            if (alitaPrefab != null)
            {
                Transform parentT = (player != null) ? player.transform.parent : null;
                if (player != null)
                {
                    Destroy(player.gameObject);
                    player = null;
                }

                GameObject alitaGo = Instantiate(alitaPrefab, spawnPos, spawnRot, parentT);
                alitaGo.name = "Alita_BattleAngel_Hero";
                alitaGo.tag = "Player";

                var ctrl = alitaGo.GetComponent<InsaneGunSwordController>();
                if (ctrl == null) ctrl = alitaGo.AddComponent<InsaneGunSwordController>();
                ctrl.controlMode = InsaneGunSwordController.ControlMode.PlayerControlled;
                ctrl.maxHealth = 400f;
                ctrl.currentHealth = 400f;
                player = ctrl;

                // Wire orbit camera
                if (SmoothOrbitCamera.Instance != null)
                {
                    SmoothOrbitCamera.Instance.target = player.transform;
                    SmoothOrbitCamera.Instance.SnapToTarget();
                }
                else
                {
                    var orbit = FindAnyObjectByType<SmoothOrbitCamera>();
                    if (orbit != null)
                    {
                        orbit.target = player.transform;
                        orbit.SnapToTarget();
                    }
                    else if (Camera.main != null)
                    {
                        var newOrbit = Camera.main.gameObject.AddComponent<SmoothOrbitCamera>();
                        newOrbit.target = player.transform;
                        newOrbit.SnapToTarget();
                    }
                }

                if (rival != null)
                {
                    rival.targetEnemy = player.transform;
                }

                Debug.Log("[Mission3Pit] Successfully deployed authentic Alita Battle Angel model in the Pit Arena!");
            }
            else
            {
                Debug.LogWarning("[Mission3Pit] Could not load Alita_BattleAngel_Hero prefab!");
            }
        }

        private void CacheRivalVisuals()
        {
            if (rival == null) return;
            var katana = rival.transform.Find("Rival_Cyber_Katana");
            if (katana == null)
            {
                var anim = rival.GetComponent<Animator>();
                if (anim != null)
                {
                    var hand = anim.GetBoneTransform(HumanBodyBones.RightHand);
                    if (hand != null)
                    {
                        var t = hand.Find("Rival_Cyber_Katana");
                        if (t != null) katana = t;
                    }
                }
            }

            if (katana != null)
            {
                var mr = katana.GetComponentInChildren<MeshRenderer>();
                if (mr != null) _rivalKatanaMat = mr.material;

                var l = katana.GetComponentInChildren<Light>();
                if (l != null) _rivalKatanaLight = l;
            }
        }

        public void StartRound(int roundNum)
        {
            currentRound = roundNum;
            currentState = PitState.RoundActive;

            // Lock South cage exit during active duel
            PitArenaExitTrigger.IsExitLocked = true;

            // Stop breather applause
            PitCrowdAudioManager.Instance?.StopClap();

            // Setup Combatants
            LocateCombatants();

            if (_entranceRoutine != null) StopCoroutine(_entranceRoutine);
            _entranceRoutine = StartCoroutine(ChallengerEntranceRoutine(roundNum));
        }

        private IEnumerator ChallengerEntranceRoutine(int roundNum)
        {
            EnsureArenaFloorSafety();

            // 1. Lock combatants during dramatic entrance
            if (player != null) player.inputLocked = true;
            if (rival != null)
            {
                rival.inputLocked = true;
                rival.controlMode = InsaneGunSwordController.ControlMode.AICirclingEnemy;
            }

            Vector3 targetSpawn = rivalSpawnPos;
            if (Physics.Raycast(rivalSpawnPos + Vector3.up * 2f, Vector3.down, out RaycastHit rHit, 5f))
            {
                targetSpawn.y = rHit.point.y + 0.02f;
            }

            if (rival != null)
            {
                var cc = rival.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;

                rival.transform.position = targetSpawn;
                rival.transform.rotation = rivalSpawnRot;
                Physics.SyncTransforms();

                if (cc != null) cc.enabled = true;

                rival.Revive(targetSpawn);
                rival.transform.position = targetSpawn;
                rival.transform.rotation = rivalSpawnRot;
                if (player != null) rival.targetEnemy = player.transform;

                // Apply unique round visual model and combat scaling
                ApplyChallengerVisuals(roundNum);
                ApplyScaling(rival, roundNum);

                // Anti-invisibility & bounds inflation
                FixCombatantInvisibilityAndCulling(rival);
            }

            if (player != null)
            {
                FixCombatantInvisibilityAndCulling(player);
            }

            string title = GetTitleForRound(roundNum);

            // 2. Spawn Dramatic Hydraulic Warp Beam & Shockwave VFX
            SpawnChallengerEntranceVFX(targetSpawn, roundNum);

            // 3. Play Hydraulic Bass Drop sound
            PlayAudioClip(GetTeleportDropClip(), 1.0f);

            // 4. Challenger weapon unsheathe
            if (rival != null)
            {
                rival.DrawSword();
                var anim = rival.GetComponent<Animator>();
                if (anim != null) anim.SetTrigger("DrawSword");
            }

            // 5. Crowd Intro Roar
            PitCrowdAudioManager.Instance?.PlayCheer(isEntrance: true);

            // 6. Boss Health Bar & Quest Objective
            CyberpunkHUDController.Instance?.SetBossActive(title);
            CyberpunkHUDController.Instance?.SetBossHealth(1.0f, title);
            UpdateQuestObjective();

            // 7. High-impact Cinematic HUD Challenger Announcement Banner
            string toastMsg = (roundNum == 1)
                ? $"// ROUND 1 // NEW CHALLENGER: {title}"
                : $"// ROUND {roundNum} // INCOMING CHALLENGER: {title}";
            CyberpunkHUDController.Instance?.ShowToast(toastMsg, 3.5f);

            // 8. Dramatic tension pause
            yield return new WaitForSeconds(1.5f);

            // 9. Duel Bell / Gong: FIGHT!
            PlayAudioClip(GetGongBellClip(), 0.95f);
            CyberpunkHUDController.Instance?.ShowToast("// DUEL COMMENCED // REST NO BLADE!", 2.0f);

            // 10. Release controls
            if (player != null) player.inputLocked = false;
            if (rival != null) rival.inputLocked = false;

            _entranceRoutine = null;
        }

        public void ApplyChallengerVisuals(int roundNum)
        {
            if (rival == null) return;

            string targetMeshName = "SM_Chr_Street_Male_01";
            Color rimColor = new Color(0.1f, 0.9f, 1.0f); // Neon Cyan
            Color swordColor = new Color(0.1f, 0.9f, 1.0f);

            if (roundNum == 1)
            {
                targetMeshName = "SM_Chr_Street_Male_01";
                rimColor = new Color(0.1f, 0.9f, 1.0f); // Neon Cyan (Razor Runner)
                swordColor = new Color(0.1f, 0.9f, 1.0f);
            }
            else if (roundNum == 2)
            {
                targetMeshName = "SM_Chr_Cyborg_Male_01";
                rimColor = new Color(1.0f, 0.55f, 0.05f); // Amber / Cybernetic Gold (Cyborg Gladiator)
                swordColor = new Color(1.0f, 0.55f, 0.05f);
            }
            else if (roundNum == 3)
            {
                targetMeshName = "SM_Chr_Mercenary_Male_01";
                rimColor = new Color(0.85f, 0.25f, 1.0f); // Neon Violet / Purple (Shadow Mercenary)
                swordColor = new Color(0.85f, 0.25f, 1.0f);
            }
            else if (roundNum == 4)
            {
                targetMeshName = "SM_Chr_Robot_Mercenary_01";
                rimColor = new Color(1.0f, 0.15f, 0.15f); // Threat Crimson (Warbot Berserker)
                swordColor = new Color(1.0f, 0.15f, 0.15f);
            }
            else // Round 5+
            {
                targetMeshName = "SM_Chr_Robot_Damaged_01";
                rimColor = new Color(1.0f, 0.05f, 0.35f); // Void Magenta-Red (Void Champion)
                swordColor = new Color(1.0f, 0.05f, 0.35f);
            }

            // Switch active child character mesh
            var allTfs = rival.GetComponentsInChildren<Transform>(true);
            foreach (var t in allTfs)
            {
                if (t.name.StartsWith("SM_Chr_") && !t.name.Contains("Attach"))
                {
                    bool match = (t.name == targetMeshName);
                    t.gameObject.SetActive(match);
                }
            }

            ConfigureRivalWeaponAndAura(rimColor, swordColor);
            FixCombatantInvisibilityAndCulling(rival);
        }

        private void ConfigureRivalWeaponAndAura(Color rimColor, Color swordColor)
        {
            if (rival == null) return;

            // 1. Configure lights on rival
            var lights = rival.GetComponentsInChildren<Light>(true);
            foreach (var l in lights)
            {
                l.color = rimColor;
                l.intensity = Mathf.Max(l.intensity, 2.8f);
                l.enabled = true;
            }

            // 2. Configure katana emission & blade light
            if (_rivalKatanaMat != null)
            {
                _rivalKatanaMat.SetColor("_BaseColor", swordColor * 0.25f);
                _rivalKatanaMat.SetColor("_EmissionColor", swordColor * 3.6f);
            }
            if (_rivalKatanaLight != null)
            {
                _rivalKatanaLight.color = swordColor;
                _rivalKatanaLight.intensity = 3.0f;
                _rivalKatanaLight.enabled = true;
            }

            // 3. Configure sword trail
            if (rival.swordTrail != null)
            {
                rival.swordTrail.startColor = swordColor;
                rival.swordTrail.endColor = new Color(swordColor.r, swordColor.g, swordColor.b, 0f);
                rival.swordTrail.time = 0.45f;
            }
        }

        private void SpawnChallengerEntranceVFX(Vector3 spawnPos, int roundNum)
        {
            Color vfxColor = new Color(0.1f, 0.9f, 1.0f);
            if (roundNum == 2) vfxColor = new Color(1.0f, 0.65f, 0.1f);
            else if (roundNum == 3) vfxColor = new Color(0.85f, 0.3f, 1.0f);
            else if (roundNum == 4) vfxColor = new Color(1.0f, 0.15f, 0.15f);
            else if (roundNum >= 5) vfxColor = new Color(1.0f, 0.05f, 0.35f);

            var vfxRoot = new GameObject("Challenger_Entrance_VFX");
            vfxRoot.transform.position = spawnPos;

            Shader unlit = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                        ?? Shader.Find("Universal Render Pipeline/Unlit")
                        ?? Shader.Find("Sprites/Default");

            Material beamMat = new Material(unlit);
            beamMat.color = vfxColor;
            if (beamMat.HasProperty("_BaseColor")) beamMat.SetColor("_BaseColor", vfxColor);
            if (beamMat.HasProperty("_EmissionColor"))
            {
                beamMat.EnableKeyword("_EMISSION");
                beamMat.SetColor("_EmissionColor", vfxColor * 4.0f);
            }

            // 1. Vertical Hydraulic Energy Drop Column
            var beamObj = new GameObject("Vertical_Drop_Beam");
            beamObj.transform.SetParent(vfxRoot.transform, false);
            var beamLine = beamObj.AddComponent<LineRenderer>();
            beamLine.useWorldSpace = true;
            beamLine.startWidth = 0.9f;
            beamLine.endWidth = 1.6f;
            beamLine.positionCount = 2;
            beamLine.SetPosition(0, spawnPos + Vector3.up * 14.0f);
            beamLine.SetPosition(1, spawnPos + Vector3.up * 0.05f);
            beamLine.sharedMaterial = beamMat;
            beamLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // 2. High-Intensity Hydraulic Flash Light
            var flashLightObj = new GameObject("Entrance_FlashLight");
            flashLightObj.transform.SetParent(vfxRoot.transform, false);
            flashLightObj.transform.position = spawnPos + Vector3.up * 1.5f;
            var fl = flashLightObj.AddComponent<Light>();
            fl.type = LightType.Point;
            fl.color = vfxColor;
            fl.range = 14.0f;
            fl.intensity = 5.0f;

            // 3. Ground Expanding Shockwave Ring
            var ringObj = new GameObject("Entrance_ShockwaveRing");
            ringObj.transform.SetParent(vfxRoot.transform, false);
            var ringLine = ringObj.AddComponent<LineRenderer>();
            ringLine.useWorldSpace = false;
            ringLine.loop = true;
            ringLine.startWidth = 0.35f;
            ringLine.endWidth = 0.35f;
            ringLine.sharedMaterial = beamMat;
            ringLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            int segs = 36;
            ringLine.positionCount = segs;
            float r = 2.8f;
            for (int i = 0; i < segs; i++)
            {
                float angle = i * Mathf.PI * 2f / segs;
                ringLine.SetPosition(i, new Vector3(Mathf.Cos(angle) * r, 0.08f, Mathf.Sin(angle) * r));
            }

            // Clean up VFX after 1.8 seconds
            Destroy(vfxRoot, 1.8f);
        }

        public static void FixCombatantInvisibilityAndCulling(InsaneGunSwordController fighter)
        {
            if (fighter == null) return;

            // 1. Force SkinnedMeshRenderers to never cull offscreen or dynamically occlude
            var smrs = fighter.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var smr in smrs)
            {
                smr.updateWhenOffscreen = true;
                smr.allowOcclusionWhenDynamic = false;
                smr.skinnedMotionVectors = false;
                // Inflate local AABB bounds so camera frustum / small-mesh culling never culls them
                smr.localBounds = new Bounds(Vector3.zero, new Vector3(50f, 50f, 50f));
            }

            // 2. Force Animator to AlwaysAnimate so transforms are continually evaluated
            var anim = fighter.GetComponent<Animator>();
            if (anim != null)
            {
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
        }

        private static void EnsureArenaFloorSafety()
        {
            var existing = GameObject.Find("Arena_Ring_SafetyFloorSlab");
            if (existing == null)
            {
                var slab = new GameObject("Arena_Ring_SafetyFloorSlab");
                slab.transform.position = new Vector3(0f, 0.78f, 0f);
                var col = slab.AddComponent<BoxCollider>();
                col.size = new Vector3(32f, 0.2f, 32f);
            }
        }

        private void PlayAudioClip(AudioClip clip, float volume = 1f)
        {
            if (clip == null) return;
            if (audioSource != null)
            {
                audioSource.PlayOneShot(clip, volume);
            }
            else
            {
                AudioSource.PlayClipAtPoint(clip, (rival != null ? rival.transform.position : transform.position), volume);
            }
        }

        private static AudioClip GetTeleportDropClip()
        {
            if (_teleportDropClip != null) return _teleportDropClip;
            int rate = 44100;
            float dur = 0.9f;
            int samples = (int)(rate * dur);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float freq = Mathf.Lerp(220f, 42f, t * t);
                float env = Mathf.Exp(-t * 4.5f);
                float wave = Mathf.Sin(2f * Mathf.PI * freq * (i / (float)rate));
                float sub = Mathf.Sin(2f * Mathf.PI * (freq * 0.5f) * (i / (float)rate)) * 0.5f;
                data[i] = (wave + sub) * env * 0.75f;
            }
            _teleportDropClip = AudioClip.Create("Challenger_DropBass", samples, 1, rate, false);
            _teleportDropClip.SetData(data, 0);
            return _teleportDropClip;
        }

        private static AudioClip GetGongBellClip()
        {
            if (_gongBellClip != null) return _gongBellClip;
            int rate = 44100;
            float dur = 1.4f;
            int samples = (int)(rate * dur);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float env = Mathf.Exp(-t * 3.2f);
                float fundamental = Mathf.Sin(2f * Mathf.PI * 330f * (i / (float)rate));
                float harmonic = Mathf.Sin(2f * Mathf.PI * 660f * (i / (float)rate)) * 0.35f;
                float metal = Mathf.Sin(2f * Mathf.PI * 1320f * (i / (float)rate)) * 0.15f;
                data[i] = (fundamental + harmonic + metal) * env * 0.70f;
            }
            _gongBellClip = AudioClip.Create("Arena_DuelGong", samples, 1, rate, false);
            _gongBellClip.SetData(data, 0);
            return _gongBellClip;
        }

        /// <summary>
        /// Exact scaling per round:
        /// - Health: 180 + (round-1)*75 (R1: 180, R2: 255, R3: 330, R4: 405, R5: 480)
        /// - Damage: 22 + (round-1)*8 (R1: 22, R2: 30, R3: 38, R4: 46)
        /// - Speed: 5.8 + (round-1)*0.35
        /// - Combos: R1 Executioner, R2 Stalker Lunge, R3 Cyclone Crossfire, R4+ Blitzkrieg Overdrive
        /// </summary>
        public void ApplyScaling(InsaneGunSwordController r, int round)
        {
            if (r == null) return;

            // 1. Health
            float scaledHP = 180f + (round - 1) * 75f;
            r.aiMaxHealth = scaledHP;
            r.maxHealth = scaledHP;
            r.currentHealth = scaledHP;

            // 2. Damage
            float atk = 22f + (round - 1) * 8f;
            r.attackDamage = atk;
            r.heavyAttackDamage = atk * 2.2f;
            r.gunDamagePerShot = 16f + (round - 1) * 5f;

            // 3. Movement Speed & Mobility
            r.runSpeed = Mathf.Clamp(5.8f + (round - 1) * 0.35f, 5.8f, 7.8f);

            // 4. AI Tactics & Distinct Combo Styles per Round
            if (round == 1)
            {
                r.activeAICombo = InsaneGunSwordController.AIGunSwordComboType.ExecutionerShred;
                r.hasHyperArmor = false;
                r.aiBlockChance = 0.10f;
                r.aiDodgeReactionChance = 0.14f;
                r.aiParryChance = 0f;
                r.maxPoise = 40f;
                r.currentPoise = 40f;
            }
            else if (round == 2)
            {
                r.activeAICombo = InsaneGunSwordController.AIGunSwordComboType.StalkerLunge;
                r.hasHyperArmor = false;
                r.aiBlockChance = 0.18f;
                r.aiDodgeReactionChance = 0.20f;
                r.aiParryChance = 0.06f;
                r.maxPoise = 55f;
                r.currentPoise = 55f;
            }
            else if (round == 3)
            {
                r.activeAICombo = InsaneGunSwordController.AIGunSwordComboType.CycloneCrossfire;
                r.hasHyperArmor = false;
                r.aiBlockChance = 0.26f;
                r.aiDodgeReactionChance = 0.28f;
                r.aiParryChance = 0.12f;
                r.maxPoise = 70f;
                r.currentPoise = 70f;
            }
            else // Round 4+
            {
                r.activeAICombo = InsaneGunSwordController.AIGunSwordComboType.BlitzkriegOverdrive;
                r.hasHyperArmor = true;
                r.aiBlockChance = 0.32f;
                r.aiDodgeReactionChance = 0.35f;
                r.aiParryChance = 0.18f;
                r.maxPoise = 85f + (round - 4) * 15f;
                r.currentPoise = r.maxPoise;
            }

            // Visual Tint (R5+ Void Crimson Glow)
            ApplyVoidVisuals(round >= 5);
        }

        private void ApplyVoidVisuals(bool isVoid)
        {
            if (_rivalKatanaMat != null)
            {
                if (isVoid)
                {
                    Color voidRed = new Color(3.6f, 0.15f, 0.15f, 1f);
                    _rivalKatanaMat.SetColor("_BaseColor", new Color(0.18f, 0.02f, 0.02f, 1f));
                    _rivalKatanaMat.SetColor("_EmissionColor", voidRed);
                    if (_rivalKatanaLight != null)
                    {
                        _rivalKatanaLight.color = new Color(1f, 0.15f, 0.15f, 1f);
                        _rivalKatanaLight.intensity = 3.2f;
                    }
                }
                else
                {
                    Color amber = new Color(2.4f, 0.9f, 0.05f, 1f);
                    _rivalKatanaMat.SetColor("_BaseColor", new Color(0.12f, 0.06f, 0.02f, 1f));
                    _rivalKatanaMat.SetColor("_EmissionColor", amber);
                    if (_rivalKatanaLight != null)
                    {
                        _rivalKatanaLight.color = new Color(1f, 0.65f, 0.1f, 1f);
                        _rivalKatanaLight.intensity = 2.0f;
                    }
                }
            }
        }

        public string GetTitleForRound(int round)
        {
            if (round == 1) return "RAZOR STREET RUNNER";
            if (round == 2) return "CYBORG GLADIATOR";
            if (round == 3) return "ARASAKA SHADOW MERC";
            if (round == 4) return "CHROME WARBOT BERSERKER";
            if (round < 10) return "VOID CHAMPION // OVERDRIVE";
            return "PIT LEGEND";
        }

        private void Update()
        {
            if (currentState == PitState.RoundActive)
            {
                UpdateActiveRound();
            }
            else if (currentState == PitState.Breather)
            {
                UpdateBreather();
            }
            else if (currentState == PitState.VulcanShowdown)
            {
                // Handled exclusively by Mission4_VulcanShowdownManager
            }
        }

        private void UpdateActiveRound()
        {
            if (player == null || rival == null)
            {
                LocateCombatants();
                return;
            }

            // Periodic anti-invisibility check
            _cullingCheckTimer += Time.deltaTime;
            if (_cullingCheckTimer >= 1.5f)
            {
                _cullingCheckTimer = 0f;
                FixCombatantInvisibilityAndCulling(rival);
            }

            // Safety floor clamp: guarantee fighter never falls or clips below the ring floor
            if (rival.transform.position.y < 0.80f)
            {
                Vector3 corrected = rival.transform.position;
                corrected.y = 0.854f;
                rival.transform.position = corrected;
                Physics.SyncTransforms();
            }

            // Update Boss Health Bar
            CyberpunkHUDController.Instance?.SetBossHealth(rival.HealthPercent, $"{GetTitleForRound(currentRound)} (R{currentRound})");
        }

        private void UpdateBreather()
        {
            if (currentState == PitState.VulcanShowdown) return;

            currentBreatherTime -= Time.deltaTime;

            if (currentBreatherTime <= 0f)
            {
                // Next round starts automatically!
                StartRound(currentRound + 1);
            }
        }

        private void UpdateQuestObjective()
        {
            string gig = "MISSION 3 // THE PIT";
            string obj = $"// ROUND {currentRound} — DEFEAT {GetTitleForRound(currentRound)}";
            CyberpunkHUDController.Instance?.SetQuestObjective(gig, obj);
        }

        private void HandleRivalDamaged(InsaneGunSwordController r, float damage)
        {
            if (currentState != PitState.RoundActive) return;

            // Direct crowd cheering feedback on every hit!
            PitCrowdAudioManager.Instance?.PlayHitReaction(damage);
        }

        private void HandleRivalDeath(InsaneGunSwordController r)
        {
            if (currentState != PitState.RoundActive) return;

            currentState = PitState.Breather;
            currentBreatherTime = breatherDuration;

            // Unlock South Exit so player can cash out and leave anytime
            PitArenaExitTrigger.IsExitLocked = false;

            // Hide Boss Bar
            CyberpunkHUDController.Instance?.HideBossBar();

            // Breather heal: restore 35% health
            if (player != null)
            {
                float healAmt = player.maxHealth * 0.35f;
                player.currentHealth = Mathf.Min(player.maxHealth, player.currentHealth + healAmt);
            }

            // Simple, transparent money formula: 1 Win = Money!
            // R1: 500 €$, R2: 750 €$, R3: 1000 €$, R4: 1250 €$
            int roundPurse = 500 + (currentRound - 1) * 250;
            totalSessionPurse += roundPurse;

            // Award directly to inventory wallet
            CyberpunkInventory.EnsureInScene();
            if (CyberpunkInventory.Instance != null)
            {
                CyberpunkInventory.Instance.AddEddies(roundPurse);
            }

            // Streak save
            if (currentRound > bestStreak)
            {
                bestStreak = currentRound;
                PlayerPrefs.SetInt("pit_best_streak", bestStreak);
                PlayerPrefs.Save();
            }

            // Crowd ovation & clapping
            PitCrowdAudioManager.Instance?.PlayCheer(isHeavyHit: true);
            PitCrowdAudioManager.Instance?.StartClap();

            // Clear reward notification
            int totalWallet = CyberpunkInventory.Instance != null ? CyberpunkInventory.Instance.eddies : totalSessionPurse;
            CyberpunkHUDController.Instance?.ShowToast(
                $"// VICTORY! ROUND {currentRound} DEFEATED // +{roundPurse} €$ (WALLET: {totalWallet} €$)", 3.8f);

            UpdateQuestObjective();

            // M4 BOSS SHOWDOWN: When Round 3 is defeated for the FIRST TIME in this session, trigger Vulcan arrival!
            // If Vulcan has already been defeated in this session, the Pit Arena works normally: continues to Round 4 and beyond!
            if (currentRound == 3 && !Mission4_VulcanShowdownManager.IsVulcanDefeated)
            {
                currentState = PitState.VulcanShowdown;
                currentBreatherTime = 9999f; // Suspend standard breather loop for boss arrival
                PitArenaExitTrigger.IsExitLocked = true;
                Debug.Log("<color=#FF6600><b>[Mission3 Pit]</b></color> Round 3 CLEARED! Initiating Vulcan Showdown hijack!");
                Mission4_VulcanShowdownManager.EnsureInArena().TriggerVulcanArrival();
            }
        }

        [ContextMenu("Skip To Round 3 (Test Vulcan Trigger)")]
        public void DebugSkipToRound3()
        {
            currentRound = 3;
            StartRound(3);
        }

        /// <summary>
        /// Called by Mission4_VulcanShowdownManager when Vulcan is defeated and executed.
        /// Seamlessly restores the Pit Arena into standard championship mode.
        /// </summary>
        public void OnVulcanShowdownCompleted()
        {
            Debug.Log("<color=#00FFAA><b>[Mission3 Pit]</b></color> Vulcan defeated! Resuming standard arena championship loop.");

            // Unlock South Exit so player can leave anytime
            PitArenaExitTrigger.IsExitLocked = false;

            // Re-activate rival game object if it was deactivated for Vulcan duel
            if (rival == null || !rival.gameObject.activeInHierarchy)
            {
                var rivalGo = GameObject.Find("Rival_Cyber_Duelist");
                if (rivalGo == null)
                {
                    var allControllers = Resources.FindObjectsOfTypeAll<InsaneGunSwordController>();
                    foreach (var c in allControllers)
                    {
                        if (c.gameObject.name.IndexOf("Rival", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            c.gameObject.SetActive(true);
                            rival = c;
                            break;
                        }
                    }
                }
                else
                {
                    rivalGo.SetActive(true);
                    rival = rivalGo.GetComponent<InsaneGunSwordController>();
                }
            }

            // Restore Pit crowd audio & ambiance
            PitCrowdAudioManager.Instance?.ResumeDrumsAndAmbience();
            PitCrowdAudioManager.Instance?.StartClap();

            // Set current round to 3 and enter standard Breather countdown towards Round 4
            currentRound = 3;
            currentState = PitState.Breather;
            currentBreatherTime = breatherDuration;
        }

        private void HandlePlayerDamaged(InsaneGunSwordController p, float damage)
        {
            if (currentState != PitState.RoundActive) return;

            if (damage >= 45f)
            {
                PitCrowdAudioManager.Instance?.PlayBoo();
            }
        }

        private void HandlePlayerDeath(InsaneGunSwordController p)
        {
            if (currentState == PitState.PlayerDead || currentState == PitState.CashedOut) return;

            currentState = PitState.PlayerDead;
            PitArenaExitTrigger.IsExitLocked = false;
            PitCrowdAudioManager.Instance?.StopClap();
            PitCrowdAudioManager.Instance?.PlayBoo();
            CyberpunkHUDController.Instance?.HideBossBar();

            int finishedStreak = currentRound - 1;
            if (finishedStreak > bestStreak)
            {
                bestStreak = finishedStreak;
                PlayerPrefs.SetInt("pit_best_streak", bestStreak);
                PlayerPrefs.Save();
            }
        }

        public void CashOutAndExit()
        {
            currentState = PitState.CashedOut;
            PitArenaExitTrigger.IsExitLocked = false;
            PitCrowdAudioManager.Instance?.StopClap();

            CyberpunkHUDController.Instance?.ShowToast(
                $"// PURSE SECURED: €${totalSessionPurse} // RETURNING TO SECTOR 04...", 2.5f);

            if (PitArenaExitTrigger.Instance != null)
            {
                PitArenaExitTrigger.Instance.StartExit();
            }
        }

        public void RetryCurrentRound()
        {
            if (player != null)
            {
                player.transform.position = playerSpawnPos;
                player.Revive(playerSpawnPos);
            }

            StartRound(currentRound);
        }

        public void ResetToRoundOne()
        {
            if (player != null)
            {
                player.transform.position = playerSpawnPos;
                player.Revive(playerSpawnPos);
            }

            StartRound(1);
        }

        private void BuildGUIStyles()
        {
            _modalBgTex = new Texture2D(1, 1);
            _modalBgTex.SetPixel(0, 0, new Color(0.02f, 0.03f, 0.05f, 0.95f));
            _modalBgTex.Apply();

            _btnBgTex = new Texture2D(1, 1);
            _btnBgTex.SetPixel(0, 0, new Color(0.08f, 0.12f, 0.16f, 0.90f));
            _btnBgTex.Apply();

            _btnHoverTex = new Texture2D(1, 1);
            _btnHoverTex.SetPixel(0, 0, new Color(0.18f, 0.28f, 0.38f, 0.95f));
            _btnHoverTex.Apply();

            _rebootHeaderStyle = new GUIStyle
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.22f, 0.22f, 1f) }
            };

            _rebootSubStyle = new GUIStyle
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.85f, 0.2f, 1f) }
            };

            _rebootBodyStyle = new GUIStyle
            {
                fontSize = 12,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.85f, 0.9f, 0.95f, 0.95f) }
            };

            _rebootBtnStyle = new GUIStyle
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white, background = _btnBgTex },
                hover = { textColor = new Color(0f, 0.95f, 1f, 1f), background = _btnHoverTex }
            };

            _breatherTitleStyle = new GUIStyle
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0f, 0.95f, 1f, 1f) }
            };

            _breatherSubStyle = new GUIStyle
            {
                fontSize = 12,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.85f, 0.2f, 0.95f) }
            };
        }

        private void OnGUI()
        {
            if (Mission4_VulcanShowdownManager.Instance != null &&
                Mission4_VulcanShowdownManager.Instance.currentPhase != Mission4_VulcanShowdownManager.ShowdownPhase.Inactive)
            {
                return;
            }

            if (currentState == PitState.Breather)
            {
                DrawBreatherHUD();
            }
            else if (currentState == PitState.PlayerDead)
            {
                DrawPlayerDeadModal();
            }
        }

        private void DrawBreatherHUD()
        {
            float sw = Screen.width;
            float boxW = 540f;
            float boxH = 64f;
            float boxX = (sw - boxW) * 0.5f;
            float boxY = 70f;

            Rect r = new Rect(boxX, boxY, boxW, boxH);
            GUI.DrawTexture(r, _modalBgTex);
            DrawBorder(r, new Color(0f, 0.95f, 1f, 0.85f), 2f);

            int secsLeft = Mathf.CeilToInt(currentBreatherTime);
            string title = $"// ROUND {currentRound} CLEARED // NEXT CHALLENGER IN {secsLeft}s";
            string sub = $"[E] AT SOUTH DOOR TO CASH OUT (€${totalSessionPurse}) OR STAND YOUR GROUND";

            GUI.Label(new Rect(boxX, boxY + 8f, boxW, 24f), title, _breatherTitleStyle);
            GUI.Label(new Rect(boxX, boxY + 34f, boxW, 20f), sub, _breatherSubStyle);
        }

        private void DrawPlayerDeadModal()
        {
            float sw = Screen.width;
            float sh = Screen.height;
            float modalW = 520f;
            float modalH = 310f;
            float modalX = (sw - modalW) * 0.5f;
            float modalY = (sh - modalH) * 0.5f;

            Rect modalRect = new Rect(modalX, modalY, modalW, modalH);
            GUI.DrawTexture(modalRect, _modalBgTex);

            Color redGlow = new Color(1f, 0.22f, 0.22f, Mathf.Sin(Time.time * 6f) * 0.2f + 0.8f);
            DrawBorder(modalRect, redGlow, 3f);

            // Header
            GUI.Label(new Rect(modalX, modalY + 16f, modalW, 30f), "⚠ BIOMONITOR ALERT: FLATLINED ⚠", _rebootHeaderStyle);
            GUI.Label(new Rect(modalX, modalY + 48f, modalW, 22f), $"ROUND {currentRound} — {GetTitleForRound(currentRound)}", _rebootSubStyle);

            // Stats
            string stats = $"CURRENT STREAK: {currentRound - 1}   |   BEST STREAK: {bestStreak}\nTOTAL SESSION PURSE BANKED: €${totalSessionPurse}";
            GUI.Label(new Rect(modalX + 20f, modalY + 80f, modalW - 40f, 40f), stats, _rebootBodyStyle);

            // Buttons
            float btnW = modalW - 60f;
            float btnH = 36f;
            float btnX = modalX + 30f;

            if (GUI.Button(new Rect(btnX, modalY + 135f, btnW, btnH), $"[ RETRY ROUND {currentRound} ]", _rebootBtnStyle))
            {
                RetryCurrentRound();
            }

            if (GUI.Button(new Rect(btnX, modalY + 180f, btnW, btnH), "[ RESET TO ROUND 1 ]", _rebootBtnStyle))
            {
                ResetToRoundOne();
            }

            if (GUI.Button(new Rect(btnX, modalY + 225f, btnW, btnH), "[ CASH OUT & RETURN TO SECTOR 04 ]", _rebootBtnStyle))
            {
                CashOutAndExit();
            }
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

        public void EnforceChampionshipLighting()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.28f, 0.32f, 0.42f, 1f);

            var keyLight = GameObject.Find("Stadium_Overhead_Broadcast_KeyLight")?.GetComponent<Light>();
            if (keyLight != null)
            {
                keyLight.intensity = 1.4f;
                keyLight.color = new Color(0.96f, 0.98f, 1.0f);
                keyLight.enabled = true;
            }

            var rimLight = GameObject.Find("Stadium_Rim_Contrast_KeyLight")?.GetComponent<Light>();
            if (rimLight != null)
            {
                rimLight.intensity = 0.75f;
                rimLight.color = new Color(0.25f, 0.75f, 1.0f);
                rimLight.enabled = true;
            }

            for (int i = 1; i <= 12; i++)
            {
                var spot = GameObject.Find($"Ring_Canvas_Spotlight_{i}")?.GetComponent<Light>();
                if (spot != null)
                {
                    spot.intensity = 7.0f;
                    spot.range = 28f;
                    spot.enabled = true;
                }
            }

            var volObj = GameObject.Find("Stadium_Global_PostProcess_Volume");
            if (volObj != null)
            {
                var vol = volObj.GetComponent<UnityEngine.Rendering.Volume>();
                if (vol != null && vol.sharedProfile == null)
                {
#if UNITY_EDITOR
                    vol.sharedProfile = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>("Assets/Settings/CyberpunkCombat_VolumeProfile.asset");
#endif
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            if (player != null)
            {
                player.OnDamaged -= HandlePlayerDamaged;
                player.OnDeath -= HandlePlayerDeath;
            }

            if (rival != null)
            {
                rival.OnDamaged -= HandleRivalDamaged;
                rival.OnDeath -= HandleRivalDeath;
            }

            if (_modalBgTex != null) Destroy(_modalBgTex);
            if (_btnBgTex != null) Destroy(_btnBgTex);
            if (_btnHoverTex != null) Destroy(_btnHoverTex);
        }
    }
}
