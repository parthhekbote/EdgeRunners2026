using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using InsaneGun.Combat;
using PolygonMech;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Cyberpunk.Mars
{
    /// <summary>
    /// Manages the invasion Pilot Army deployed from the Imperial-style Dreadnoughts on Mars.
    /// Spawns 10-14 Mech Pilot NPCs (SM_Chr_MechPilot_Male_01 & Female_01) equipped with Arc/Power Rifles.
    /// Manages:
    /// - Tactical perimeter formation encircling Alita with converging laser sights.
    /// - Coordinated rifle fire with muzzle flashes, projectile tracers, and barrier deflection sparks.
    /// - Euphoria Ragdoll integration (SetupRagdollBones on all humanoid pilot bones).
    /// - One-Shot Obliteration: Alita's 360° Antimatter Berserker Cataclysm wipes out the entire army simultaneously,
    ///   launching all pilots flying 25-40 meters across the Martian sand with independent rifle physics!
    /// </summary>
    public class MarsInvasionArmyManager : MonoBehaviour
    {
        public static MarsInvasionArmyManager Instance { get; private set; }

        [Header("Pilot Character Prefabs")]
        public GameObject malePilotPrefab;
        public GameObject femalePilotPrefab;

        [Header("Weapon Prefabs")]
        public GameObject arcRiflePrefab;
        public GameObject powerRiflePrefab;

        [Header("StarCraft Drop Pod Armada (Cinematic Reference 1:34-1:45)")]
        public GameObject dropPodPrefab;
        public GameObject whiteSmokeFX;
        public GameObject thrusterFlameFX;
        public GameObject landingDustFX;
        [Range(2, 6)] public int podCount = 4;
        public float podDescentAltitude = 95f;
        public float podDescentDuration = 1.8f; // Hyper-kinetic re-entry speed (was 5.2s, now matching StarCraft 1:34-1:45)
        public bool arePodsLanded = false;

        [Header("Army Formation Tuning")]
        [Range(6, 16)] public int pilotCount = 12;
        public float perimeterRadius = 11.5f;
        public Vector3 armyCenterPos = new Vector3(0f, 2.50f, 75.00f);

        [Header("Audio SFX")]
        public AudioClip rifleFireAudio;
        public AudioClip mercGruntAudio;
        public AudioClip shockwaveObliterationAudio;
        public AudioClip laserDeflectAudio;

        [Header("Runtime State")]
        public bool isArmySpawned = false;
        public bool isArmyFiring = false;
        public bool isArmyObliterated = false;

        private readonly List<PilotUnit> _pilots = new List<PilotUnit>();
        private readonly List<DropPodInstance> _dropPods = new List<DropPodInstance>();
        private AudioSource _audioSource;

        public class DropPodInstance
        {
            public GameObject rootObject;
            public Vector3 startPos;
            public Vector3 targetPos;
            public Quaternion descentRot;
            public Quaternion landedRot;
            public ParticleSystem flameFX;
            public Light retroLight;
            public bool hasRetroBurned;
            public bool hasLanded;
        }

        public class PilotUnit
        {
            public GameObject rootObject;
            public Animator animator;
            public EuphoriaRagdollController ragdoll;
            public GameObject rifleObject;
            public LineRenderer laserSight;
            public Light muzzleLight;
            public ParticleSystem muzzleFX;
            public bool isDead = false;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0.5f;

            LoadDefaultAssets();
        }

        public void LoadDefaultAssets()
        {
#if UNITY_EDITOR
            if (malePilotPrefab == null)
                malePilotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonMech/Prefabs/Characters/SM_Chr_MechPilot_Male_01.prefab");
            if (femalePilotPrefab == null)
                femalePilotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonMech/Prefabs/Characters/SM_Chr_MechPilot_Female_01.prefab");

            if (arcRiflePrefab == null)
                arcRiflePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonMech/Prefabs/Weapons/SM_Wep_ArcRifle_01.prefab");
            if (powerRiflePrefab == null)
                powerRiflePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonMech/Prefabs/Weapons/SM_Wep_PowerRifle_01.prefab");

            if (dropPodPrefab == null)
                dropPodPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonSciFiSpace/Prefabs/Vehicles/SM_Veh_EscapePod_Large_01.prefab");
            if (dropPodPrefab == null)
                dropPodPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonSciFiWorlds/Prefabs/Props/Vehicles/SM_Veh_DropShip_01.prefab");

            if (whiteSmokeFX == null)
                whiteSmokeFX = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonMech/Prefabs/FX/FX_Smoke_White_01.prefab");
            if (thrusterFlameFX == null)
                thrusterFlameFX = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonMech/Prefabs/FX/FX_Flame_Exhaust_01.prefab");
            if (landingDustFX == null)
                landingDustFX = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonSciFiWorlds/Prefabs/FX/FX_Ship_Hover_Dust_01.prefab");

            if (mercGruntAudio == null)
                mercGruntAudio = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/NPC_Chatter/npc_merc_grunt_01.wav");
            if (mercGruntAudio == null)
                mercGruntAudio = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Voice/alita_01_attack.wav");

            if (shockwaveObliterationAudio == null)
                shockwaveObliterationAudio = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Combat/Sword_Slash_Heavy_Cleave_01.wav");

            if (laserDeflectAudio == null)
                laserDeflectAudio = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/NPC_Chatter/npc_reaction_startle_01.wav");
#endif
        }

        /// <summary>
        /// STAGE 1: StarCraft Drop Pod Armada Sequence (StarCraft Cinematic Reference 1:34-1:45).
        /// Drops 4 heavy pods from orbit/Dreadnoughts, retro-burns at Y=14m, slams onto ground,
        /// vents steam & blows doors, then deploys 12 pilots into an 11.5m firing circle.
        /// </summary>
        public IEnumerator PerformStarCraftDropPodArmadaRoutine(Vector3 center, Action onPodsLanded = null, Action onArmyFormed = null)
        {
            if (isArmySpawned) yield break;
            isArmySpawned = true;
            isArmyObliterated = false;
            armyCenterPos = center;

            CleanupArmy();

            // 1. Establish 4 tactical Drop Pod landing zones around Alita
            Vector3[] podOffsets = new Vector3[]
            {
                new Vector3(-9.2f, 0f, 6.2f),
                new Vector3(9.2f, 0f, 6.2f),
                new Vector3(-7.5f, 0f, -8.2f),
                new Vector3(7.5f, 0f, -8.2f)
            };

            _dropPods.Clear();

            for (int i = 0; i < podCount && i < podOffsets.Length; i++)
            {
                Vector3 targetPos = center + podOffsets[i];
                targetPos.y = center.y;

                // Approaching diagonally from Dreadnought hangar shelf
                Vector3 startPos = targetPos + new Vector3(-26f, podDescentAltitude, 42f);
                Vector3 descentDir = (targetPos - startPos).normalized;
                Quaternion descentRot = Quaternion.LookRotation(descentDir) * Quaternion.Euler(90f, 0f, 0f);
                Quaternion landedRot = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);

                GameObject podGO = null;
                if (dropPodPrefab != null)
                {
                    podGO = Instantiate(dropPodPrefab, startPos, descentRot, transform);
                }
                else
                {
                    podGO = CreateProceduralDropPod(startPos, descentRot);
                }
                podGO.name = $"StarCraft_DropPod_{i + 1:D2}";
                podGO.transform.localScale = Vector3.one * 1.35f;

                // Add bottom retro thruster light
                GameObject lightGO = new GameObject("Retro_Thruster_Light");
                lightGO.transform.SetParent(podGO.transform, false);
                lightGO.transform.localPosition = new Vector3(0f, -1.2f, 0f);
                var retroLight = lightGO.AddComponent<Light>();
                retroLight.type = LightType.Point;
                retroLight.color = new Color(1.0f, 0.45f, 0.10f); // Hot orange flame
                retroLight.range = 22f;
                retroLight.intensity = 3.5f;

                // Add thruster exhaust particle system
                ParticleSystem flamePS = null;
                if (thrusterFlameFX != null)
                {
                    var fx = Instantiate(thrusterFlameFX, lightGO.transform);
                    fx.transform.localPosition = Vector3.zero;
                    fx.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    flamePS = fx.GetComponent<ParticleSystem>();
                }

                _dropPods.Add(new DropPodInstance
                {
                    rootObject = podGO,
                    startPos = startPos,
                    targetPos = targetPos,
                    descentRot = descentRot,
                    landedRot = landedRot,
                    flameFX = flamePS,
                    retroLight = retroLight,
                    hasRetroBurned = false,
                    hasLanded = false
                });
            }

            // Play distant atmospheric descent roar
            if (_audioSource != null)
            {
                MarsSfx.Play(MechAudioSynth.GetThrusterIgnition(), 0.4f, 0.7f);
            }

            float timer = 0f;
            bool retroFired = false;

            while (timer < podDescentDuration)
            {
                timer += Time.deltaTime;
                float norm = Mathf.Clamp01(timer / podDescentDuration);

                // High-velocity deceleration curve (terminal entry -> retro burn)
                float curve = norm < 0.68f ? (norm / 0.68f) * 0.72f : 0.72f + Mathf.SmoothStep(0f, 0.28f, (norm - 0.68f) / 0.32f);

                for (int i = 0; i < _dropPods.Count; i++)
                {
                    var pod = _dropPods[i];
                    if (pod.hasLanded) continue;

                    Vector3 curPos = Vector3.Lerp(pod.startPos, pod.targetPos, curve);
                    pod.rootObject.transform.position = curPos;

                    // Transition rotation upright as it brakes
                    float rotT = Mathf.Clamp01((curve - 0.5f) / 0.5f);
                    pod.rootObject.transform.rotation = Quaternion.Slerp(pod.descentRot, pod.landedRot, rotT);

                    // Trigger Retro-Burn Cluster (StarCraft 1:42) at Y <= 14m
                    if (!pod.hasRetroBurned && curPos.y <= center.y + 14f)
                    {
                        pod.hasRetroBurned = true;
                        if (pod.retroLight != null) pod.retroLight.intensity = 14f;
                        if (pod.flameFX != null && !pod.flameFX.isPlaying) pod.flameFX.Play();
                    }
                }

                // Audio cue for retro-rockets
                if (!retroFired && norm >= 0.68f)
                {
                    retroFired = true;
                    if (_audioSource != null)
                    {
                        MarsSfx.Play(MechAudioSynth.GetThrusterIgnition(), 0.7f);
                    }
                    if (MarsArrivalCutsceneDirector.Instance != null)
                    {
                        MarsArrivalCutsceneDirector.Instance.AddCameraTrauma(0.45f);
                    }
                }

                yield return null;
            }

            // ════════════════════════════════════════════════════════════════════════
            // GROUND SLAMDOWN & RETRO-CUTOFF (StarCraft 1:45)
            // ════════════════════════════════════════════════════════════════════════
            for (int i = 0; i < _dropPods.Count; i++)
            {
                var pod = _dropPods[i];
                pod.hasLanded = true;
                pod.rootObject.transform.position = pod.targetPos;
                pod.rootObject.transform.rotation = pod.landedRot;

                if (pod.retroLight != null) pod.retroLight.intensity = 0f;
                if (pod.flameFX != null) pod.flameFX.Stop();

                // Spawn 360° red dust shockwave ring at pod base
                SpawnPodLandingDustRing(pod.targetPos);

                // Spawn pneumatic steam venting from pod hatches
                SpawnPodSteamVents(pod.targetPos);
            }

            arePodsLanded = true;

            // Colossal kinetic slamdown sound
            if (_audioSource != null)
            {
                MarsSfx.Play(MechAudioSynth.GetSeismicSlam(), 0.9f, 0.9f);
            }

            if (MarsArrivalCutsceneDirector.Instance != null)
            {
                MarsArrivalCutsceneDirector.Instance.AddCameraTrauma(0.95f);
            }

            onPodsLanded?.Invoke();

            // Wait 0.15s for doors to blow open & steam to vent (fast action deploy)
            yield return new WaitForSeconds(0.15f);

            // ════════════════════════════════════════════════════════════════════════
            // DEPLOY 12 PILOTS FROM PODS INTO COMBAT PERIMETER
            // ════════════════════════════════════════════════════════════════════════
            SpawnPilotsFromDropPods(center);

            onArmyFormed?.Invoke();
        }

        private void SpawnPilotsFromDropPods(Vector3 center)
        {
            float angleStep = 360f / pilotCount;

            for (int i = 0; i < pilotCount; i++)
            {
                float angle = i * angleStep;
                float rad = angle * Mathf.Deg2Rad;

                float radius = perimeterRadius + UnityEngine.Random.Range(-0.8f, 0.8f);
                Vector3 finalPos = center + new Vector3(Mathf.Sin(rad) * radius, 0f, Mathf.Cos(rad) * radius);
                finalPos.y = center.y;

                // Pick the nearest pod as spawn origin
                int podIdx = i % Mathf.Max(1, _dropPods.Count);
                Vector3 spawnOrigin = _dropPods.Count > 0 ? _dropPods[podIdx].targetPos : finalPos;
                spawnOrigin.y = center.y;

                Vector3 lookDir = (center - finalPos);
                lookDir.y = 0f;
                Quaternion spawnRot = lookDir != Vector3.zero ? Quaternion.LookRotation(lookDir) : Quaternion.identity;

                GameObject chosenPrefab = (i % 2 == 0) ? malePilotPrefab : femalePilotPrefab;
                if (chosenPrefab == null) chosenPrefab = malePilotPrefab;
                if (chosenPrefab == null) continue;

                // Spawn at final position facing Alita
                GameObject pilotGO = Instantiate(chosenPrefab, finalPos, spawnRot, transform);
                pilotGO.name = $"MechPilot_Soldier_{i + 1:D2}";
                pilotGO.tag = "Untagged";

                var anim = pilotGO.GetComponent<Animator>();
                if (anim != null)
                {
#if UNITY_EDITOR
                    var animCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/InsaneGun_Sword_Set/Ect/AnimatorContrl/atk_gun_01.controller");
                    if (animCtrl == null)
                        animCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/ShootRifle_Controller.controller");
                    if (animCtrl != null) anim.runtimeAnimatorController = animCtrl;
#endif
                    anim.applyRootMotion = false;
                }

                GameObject rifleGO = AttachWeaponToPilot(pilotGO, (i % 2 == 0) ? arcRiflePrefab : powerRiflePrefab);

                var ragdoll = pilotGO.GetComponent<EuphoriaRagdollController>();
                if (ragdoll == null) ragdoll = pilotGO.AddComponent<EuphoriaRagdollController>();
                ragdoll.autoRecover = false;
                ragdoll.autoFreezeFatalRagdolls = true;
                ragdoll.ragdollForceMultiplier = 1.65f;
                ragdoll.SetupRagdollBones();

                LineRenderer laser = SetupLaserSight(pilotGO, rifleGO);
                Light muzzleLight = SetupMuzzleLight(rifleGO);

                _pilots.Add(new PilotUnit
                {
                    rootObject = pilotGO,
                    animator = anim,
                    ragdoll = ragdoll,
                    rifleObject = rifleGO,
                    laserSight = laser,
                    muzzleLight = muzzleLight,
                    isDead = false
                });
            }

            Debug.Log($"[MarsInvasionArmyManager] StarCraft Drop Pod armada deployed {pilotCount} Mech Pilots into tactical perimeter!");
        }

        public void SpawnPodLandingDustRing(Vector3 pos)
        {
            if (landingDustFX != null)
            {
                var dust = Instantiate(landingDustFX, pos + Vector3.up * 0.1f, Quaternion.identity);
                Destroy(dust, 4.0f);
            }
            else
            {
                // Procedural expanding Martian red dust ring
                GameObject dustRingGO = new GameObject("FX_Pod_Dust_Shockwave");
                dustRingGO.transform.position = pos + Vector3.up * 0.1f;
                var mf = dustRingGO.AddComponent<MeshFilter>();
                var mr = dustRingGO.AddComponent<MeshRenderer>();
                mf.sharedMesh = CreateRingMesh(24, 0.75f);

                var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                mat.color = new Color(0.85f, 0.42f, 0.22f, 0.75f); // Martian rust-orange dust
                mr.sharedMaterial = mat;

                StartCoroutine(ExpandDustRingRoutine(dustRingGO, mat, 14f, 1.2f));
            }
        }

        private IEnumerator ExpandDustRingRoutine(GameObject ringGO, Material mat, float maxRadius, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float norm = Mathf.Clamp01(t / duration);
                float radius = Mathf.Lerp(1.2f, maxRadius, Mathf.Sin(norm * Mathf.PI * 0.5f));
                ringGO.transform.localScale = new Vector3(radius, 1f, radius);
                mat.color = new Color(0.85f, 0.42f, 0.22f, Mathf.Lerp(0.75f, 0f, norm));
                yield return null;
            }
            Destroy(ringGO);
        }

        private void SpawnPodSteamVents(Vector3 pos)
        {
            if (whiteSmokeFX != null)
            {
                var smoke1 = Instantiate(whiteSmokeFX, pos + new Vector3(1.1f, 0.8f, 0f), Quaternion.Euler(-30f, 90f, 0f));
                var smoke2 = Instantiate(whiteSmokeFX, pos + new Vector3(-1.1f, 0.8f, 0f), Quaternion.Euler(-30f, -90f, 0f));
                Destroy(smoke1, 3.0f);
                Destroy(smoke2, 3.0f);
            }
        }

        private GameObject CreateProceduralDropPod(Vector3 pos, Quaternion rot)
        {
            GameObject pod = new GameObject("Procedural_Drop_Pod");
            pod.transform.position = pos;
            pod.transform.rotation = rot;

            // Main armored capsule body
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Pod_Hull";
            body.transform.SetParent(pod.transform, false);
            body.transform.localScale = new Vector3(2.4f, 2.8f, 2.4f);

            var mr = body.GetComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.28f, 0.12f, 0.10f); // Dark Terran/Dominion maroon-red
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.75f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.45f);
            mr.sharedMaterial = mat;

            // Strip collider so it doesn't obstruct player/ragdolls
            var col = body.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // Nose cone / heat shield
            GameObject nose = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            nose.name = "Heat_Shield";
            nose.transform.SetParent(pod.transform, false);
            nose.transform.localPosition = new Vector3(0f, 2.7f, 0f);
            nose.transform.localScale = new Vector3(2.35f, 1.2f, 2.35f);
            var noseCol = nose.GetComponent<Collider>();
            if (noseCol != null) Destroy(noseCol);

            return pod;
        }

        public void FastForwardDropPods(Vector3 center)
        {
            if (isArmySpawned) return;
            CleanupArmy();
            SpawnPilotArmy(center);
            arePodsLanded = true;
        }

        /// <summary>
        /// Spawns the 10-14 Mech Pilots in an imposing tactical circle around the target coordinate.
        /// </summary>
        public void SpawnPilotArmy(Vector3 center)
        {
            if (isArmySpawned) return;
            isArmySpawned = true;
            isArmyObliterated = false;
            armyCenterPos = center;

            // Clear any lingering units
            CleanupArmy();

            float angleStep = 360f / pilotCount;

            for (int i = 0; i < pilotCount; i++)
            {
                float angle = i * angleStep;
                float rad = angle * Mathf.Deg2Rad;

                // Slightly randomize radius for organic military formation
                float radius = perimeterRadius + UnityEngine.Random.Range(-1.2f, 1.2f);
                Vector3 spawnPos = center + new Vector3(Mathf.Sin(rad) * radius, 0f, Mathf.Cos(rad) * radius);

                // Sample ground height or use center Y
                spawnPos.y = center.y;

                // Rotate to face inward toward Alita at center
                Vector3 lookDir = (center - spawnPos);
                lookDir.y = 0f;
                Quaternion spawnRot = lookDir != Vector3.zero ? Quaternion.LookRotation(lookDir) : Quaternion.identity;

                // Alternate between male and female pilots
                GameObject chosenPrefab = (i % 2 == 0) ? malePilotPrefab : femalePilotPrefab;
                if (chosenPrefab == null) chosenPrefab = malePilotPrefab;
                if (chosenPrefab == null) continue;

                GameObject pilotGO = Instantiate(chosenPrefab, spawnPos, spawnRot, transform);
                pilotGO.name = $"MechPilot_Soldier_{i + 1:D2}";
                pilotGO.tag = "Untagged"; // Will be Enemy once combat starts

                // Configure Animator with Gun Aiming Controller
                var anim = pilotGO.GetComponent<Animator>();
                if (anim != null)
                {
#if UNITY_EDITOR
                    var animCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/InsaneGun_Sword_Set/Ect/AnimatorContrl/atk_gun_01.controller");
                    if (animCtrl == null)
                        animCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/ShootRifle_Controller.controller");
                    if (animCtrl != null) anim.runtimeAnimatorController = animCtrl;
#endif
                    anim.applyRootMotion = false;
                }

                // Attach Arc / Power Rifle to Right Hand
                GameObject rifleGO = AttachWeaponToPilot(pilotGO, (i % 2 == 0) ? arcRiflePrefab : powerRiflePrefab);

                // Setup Euphoria Ragdoll Controller
                var ragdoll = pilotGO.GetComponent<EuphoriaRagdollController>();
                if (ragdoll == null) ragdoll = pilotGO.AddComponent<EuphoriaRagdollController>();
                ragdoll.autoRecover = false; // Fatal ragdolls settle and stay down!
                ragdoll.autoFreezeFatalRagdolls = true;
                ragdoll.ragdollForceMultiplier = 1.65f;
                ragdoll.SetupRagdollBones();

                // Setup Laser Sight targeting Alita
                LineRenderer laser = SetupLaserSight(pilotGO, rifleGO);

                // Setup Muzzle Flash Light
                Light muzzleLight = SetupMuzzleLight(rifleGO);

                _pilots.Add(new PilotUnit
                {
                    rootObject = pilotGO,
                    animator = anim,
                    ragdoll = ragdoll,
                    rifleObject = rifleGO,
                    laserSight = laser,
                    muzzleLight = muzzleLight,
                    isDead = false
                });
            }

            Debug.Log($"[MarsInvasionArmyManager] Spawned {pilotCount} Mech Pilots in tactical perimeter around {center}.");
        }

        private GameObject AttachWeaponToPilot(GameObject pilotGO, GameObject weaponPrefab)
        {
            if (weaponPrefab == null) return null;

            // Search for Right Hand bone
            Transform rightHand = FindBoneDeep(pilotGO.transform, "Hand_R");
            if (rightHand == null) rightHand = FindBoneDeep(pilotGO.transform, "Hand.R");
            if (rightHand == null) rightHand = FindBoneDeep(pilotGO.transform, "RightHand");
            if (rightHand == null) rightHand = pilotGO.transform;

            GameObject rifle = Instantiate(weaponPrefab, rightHand);
            rifle.name = "Pilot_Assault_Rifle";

            // Natural tactical grip alignment in Synty hand
            rifle.transform.localPosition = new Vector3(-0.04f, 0.07f, 0.02f);
            rifle.transform.localRotation = Quaternion.Euler(12f, 85f, -90f);
            rifle.transform.localScale = Vector3.one * 0.95f;

            // Strip existing colliders/rigidbodies so they don't mess with ragdoll until detached
            var cols = rifle.GetComponentsInChildren<Collider>();
            foreach (var c in cols) c.enabled = false;

            return rifle;
        }

        private Transform FindBoneDeep(Transform parent, string boneName)
        {
            if (parent.name.Equals(boneName, StringComparison.OrdinalIgnoreCase)) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                var result = FindBoneDeep(parent.GetChild(i), boneName);
                if (result != null) return result;
            }
            return null;
        }

        private LineRenderer SetupLaserSight(GameObject pilotGO, GameObject rifleGO)
        {
            Transform muzzleOrigin = rifleGO != null ? rifleGO.transform : pilotGO.transform;
            GameObject laserGO = new GameObject("Laser_Sight_Beam");
            laserGO.transform.SetParent(muzzleOrigin, false);
            laserGO.transform.localPosition = new Vector3(0f, 0.08f, 0.45f);

            var line = laserGO.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.startWidth = 0.025f;
            line.endWidth = 0.045f;
            line.positionCount = 2;

            // URP Additive / Unlit Red-Cyan Laser Material
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.color = new Color(1.0f, 0.15f, 0.15f, 0.85f);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(4.0f, 0.2f, 0.2f));
            }
            line.sharedMaterial = mat;
            line.enabled = true;

            return line;
        }

        private Light SetupMuzzleLight(GameObject rifleGO)
        {
            if (rifleGO == null) return null;

            GameObject lightGO = new GameObject("Muzzle_Flash_Light");
            lightGO.transform.SetParent(rifleGO.transform, false);
            lightGO.transform.localPosition = new Vector3(0f, 0.08f, 0.65f);

            var l = lightGO.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1.0f, 0.55f, 0.20f);
            l.range = 5f;
            l.intensity = 0f;
            return l;
        }

        private void Update()
        {
            if (!isArmySpawned || isArmyObliterated) return;

            // Update Laser Sight endpoints targeting Alita's core
            Vector3 targetCenter = armyCenterPos + Vector3.up * 1.25f;

            foreach (var pilot in _pilots)
            {
                if (pilot.isDead || pilot.laserSight == null) continue;

                Vector3 startPos = pilot.laserSight.transform.position;
                pilot.laserSight.SetPosition(0, startPos);

                // Add slight weapon jitter for realistic military aiming
                Vector3 jitter = UnityEngine.Random.insideUnitSphere * 0.12f;
                pilot.laserSight.SetPosition(1, targetCenter + jitter);
            }
        }

        /// <summary>
        /// Starts the coordinated firing sequence where the Pilot Army opens fire on Alita!
        /// </summary>
        public void StartArmyFiringSequence(Transform targetTransform)
        {
            if (isArmyFiring || isArmyObliterated) return;
            isArmyFiring = true;
            StartCoroutine(ArmyFiringRoutine(targetTransform));
        }

        private IEnumerator ArmyFiringRoutine(Transform targetTransform)
        {
            float fireDuration = 1.2f;
            float timer = 0f;

            while (timer < fireDuration && !isArmyObliterated)
            {
                timer += Time.deltaTime;

                // Random pilot bursts
                for (int i = 0; i < _pilots.Count; i++)
                {
                    var pilot = _pilots[i];
                    if (pilot.isDead) continue;

                    if (UnityEngine.Random.value < 0.18f)
                    {
                        StartCoroutine(TriggerPilotMuzzleFlash(pilot));
                    }
                }

                yield return new WaitForSeconds(0.08f);
            }

            isArmyFiring = false;
        }

        private IEnumerator TriggerPilotMuzzleFlash(PilotUnit pilot)
        {
            if (pilot.muzzleLight != null) pilot.muzzleLight.intensity = UnityEngine.Random.Range(3.5f, 6.0f);

            yield return new WaitForSeconds(0.05f);

            if (pilot.muzzleLight != null) pilot.muzzleLight.intensity = 0f;
        }

        /// <summary>
        /// THE ONE-SHOT OBLITERATION:
        /// Alita unleashes her supercharged 360° Antimatter Berserker Cataclysm!
        /// Slow motion hit-stop, blinding optical shockwave, and all 10-14 pilots
        /// get launched FLYING across the Martian dunes in high-arc physics ragdolls!
        /// </summary>
        public void TriggerArmyOneShotObliteration(Vector3 blastOrigin, float blastRadius = 25f, float blastForce = 34f)
        {
            if (isArmyObliterated) return;
            isArmyObliterated = true;
            isArmyFiring = false;

            // 1. Play seismic implosion and heavy cleave audio
            if (_audioSource != null)
            {
                MarsSfx.Play(MechAudioSynth.GetSeismicSlam(), 0.8f, 0.85f);
                MarsSfx.Play(shockwaveObliterationAudio, 0.8f);
            }

            // 2. Spawn Expanding Cataclysm Shockwave Dome & Flash
            SpawnCataclysmShockwaveVFX(blastOrigin, blastRadius);

            // 3. Trigger Slow-Mo Hit Stop (0.16s frame freeze)
            StartCoroutine(ObliterationHitStopRoutine(0.16f, 0.12f));

            // 4. Cataclysmic Physics Ragdoll on every pilot!
            int pilotsLaunched = 0;
            foreach (var pilot in _pilots)
            {
                if (pilot.isDead || pilot.rootObject == null) continue;
                pilot.isDead = true;

                // Disable laser sight and animator
                if (pilot.laserSight != null) pilot.laserSight.enabled = false;
                if (pilot.animator != null) pilot.animator.enabled = false;
                if (pilot.muzzleLight != null) pilot.muzzleLight.intensity = 0f;

                // Detach rifle with explosive tumble
                DetachRifleWithPhysics(pilot.rifleObject, blastOrigin, blastForce);

                // Compute radial outward launch trajectory
                Vector3 pilotPos = pilot.rootObject.transform.position;
                Vector3 outwardDir = (pilotPos - blastOrigin);
                outwardDir.y = 0f;

                if (outwardDir.sqrMagnitude < 0.1f)
                {
                    outwardDir = UnityEngine.Random.insideUnitSphere;
                    outwardDir.y = 0f;
                }
                outwardDir.Normalize();

                // High-arc launch vector: 70% outward + 45% upward lift
                Vector3 launchVector = (outwardDir * 1.15f + Vector3.up * 0.55f).normalized * blastForce;

                // Trigger Euphoria Procedural Ragdoll!
                if (pilot.ragdoll != null)
                {
                    Vector3 impactPoint = pilotPos + Vector3.up * 1.1f;
                    pilot.ragdoll.TriggerEuphoriaRagdoll(impactPoint, launchVector, blastForce, isFatal: true);
                }

                // Spawn spark burst and dust plume at pilot launch position
                SpawnImpactSparksAndDust(pilotPos);

                pilotsLaunched++;
            }

            // Play Merc scream / grunt
            if (_audioSource != null && mercGruntAudio != null)
            {
                MarsSfx.Play(mercGruntAudio, 0.7f, UnityEngine.Random.Range(0.85f, 1.05f));
            }

            Debug.Log($"[MarsInvasionArmyManager] ANTIMATTER CATACLYSM! {pilotsLaunched} pilots one-shot obliterated and sent flying across Mars!");
        }

        private void DetachRifleWithPhysics(GameObject rifleGO, Vector3 blastOrigin, float blastForce)
        {
            if (rifleGO == null) return;

            rifleGO.transform.SetParent(null, true);

            var rb = rifleGO.GetComponent<Rigidbody>();
            if (rb == null) rb = rifleGO.AddComponent<Rigidbody>();
            rb.mass = 4.2f;
            rb.linearDamping = 0.35f;
            rb.angularDamping = 0.55f;
            rb.isKinematic = false;
            rb.useGravity = true;

            var col = rifleGO.GetComponent<BoxCollider>();
            if (col == null) col = rifleGO.AddComponent<BoxCollider>();
            col.size = new Vector3(0.12f, 0.25f, 0.85f);
            col.enabled = true;

            Vector3 flyDir = (rifleGO.transform.position - blastOrigin).normalized + Vector3.up * 0.65f;
            rb.linearVelocity = flyDir.normalized * (blastForce * UnityEngine.Random.Range(0.65f, 0.95f));
            rb.angularVelocity = UnityEngine.Random.insideUnitSphere * 24f;

            // Auto-clean rifle after 15s to keep scene tidy
            Destroy(rifleGO, 15f);
        }

        private void SpawnCataclysmShockwaveVFX(Vector3 origin, float maxRadius)
        {
            // Planar Expanding Shockwave Ring
            GameObject waveGO = new GameObject("FX_Antimatter_Cataclysm_Shockwave");
            waveGO.transform.position = origin + Vector3.up * 0.15f;

            var shockwaveLight = waveGO.AddComponent<Light>();
            shockwaveLight.type = LightType.Point;
            shockwaveLight.color = new Color(1.0f, 0.22f, 0.12f);
            shockwaveLight.range = 45f;
            shockwaveLight.intensity = 9.0f;

            StartCoroutine(ExpandShockwaveRoutine(waveGO, shockwaveLight, maxRadius));
        }

        private IEnumerator ExpandShockwaveRoutine(GameObject waveGO, Light waveLight, float maxRadius)
        {
            float duration = 0.45f;
            float timer = 0f;

            // Create flat circular ring mesh for the shockwave
            var mf = waveGO.AddComponent<MeshFilter>();
            var mr = waveGO.AddComponent<MeshRenderer>();
            mf.sharedMesh = CreateRingMesh(32, 0.88f);

            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.color = new Color(1.0f, 0.20f, 0.12f, 0.90f);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(5.5f, 0.70f, 0.20f));
            }
            mr.sharedMaterial = mat;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                float norm = Mathf.Clamp01(timer / duration);
                float easeOut = Mathf.Sin(norm * Mathf.PI * 0.5f);

                float currentRadius = Mathf.Lerp(1.5f, maxRadius, easeOut);
                waveGO.transform.localScale = new Vector3(currentRadius, 1f, currentRadius);

                if (waveLight != null)
                {
                    waveLight.intensity = Mathf.Lerp(9.0f, 0f, norm);
                    waveLight.range = Mathf.Lerp(25f, 55f, norm);
                }

                // Fade alpha
                mat.color = new Color(1.0f, 0.20f, 0.12f, Mathf.Lerp(0.95f, 0f, norm));

                yield return null;
            }

            Destroy(waveGO);
        }

        private Mesh CreateRingMesh(int segments, float innerRatio)
        {
            Mesh mesh = new Mesh { name = "ShockwaveRingMesh" };
            Vector3[] verts = new Vector3[(segments + 1) * 2];
            int[] tris = new int[segments * 6];

            float step = 360f / segments;
            for (int i = 0; i <= segments; i++)
            {
                float rad = i * step * Mathf.Deg2Rad;
                float cos = Mathf.Cos(rad);
                float sin = Mathf.Sin(rad);

                verts[i * 2] = new Vector3(cos * innerRatio, 0f, sin * innerRatio);
                verts[i * 2 + 1] = new Vector3(cos, 0f, sin);

                if (i < segments)
                {
                    int root = i * 2;
                    tris[i * 6 + 0] = root;
                    tris[i * 6 + 1] = root + 1;
                    tris[i * 6 + 2] = root + 2;

                    tris[i * 6 + 3] = root + 1;
                    tris[i * 6 + 4] = root + 3;
                    tris[i * 6 + 5] = root + 2;
                }
            }

            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            return mesh;
        }

        private void SpawnImpactSparksAndDust(Vector3 pos)
        {
            GameObject sparkGO = new GameObject("FX_Pilot_Obliteration_Sparks");
            sparkGO.transform.position = pos + Vector3.up * 0.5f;

            var ps = sparkGO.AddComponent<ParticleSystem>();
            var psr = sparkGO.GetComponent<ParticleSystemRenderer>();

            var main = ps.main;
            main.startLifetime = 0.35f;
            main.startSpeed = 16f;
            main.startSize = 0.22f;
            main.startColor = new Color(1.0f, 0.28f, 0.14f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.loop = false;

            var emission = ps.emission;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 22) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.4f;

            var pMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Universal Render Pipeline/Unlit"));
            pMat.mainTexture = AlitaSuperchargeAura.GetOrCreateSoftCircleTexture();
            pMat.color = new Color(1.0f, 0.22f, 0.12f);
            if (pMat.HasProperty("_EmissionColor"))
            {
                pMat.EnableKeyword("_EMISSION");
                pMat.SetColor("_EmissionColor", new Color(5.0f, 0.6f, 0.2f));
            }
            psr.material = pMat;

            ps.Play();
            Destroy(sparkGO, 0.6f);
        }

        private IEnumerator ObliterationHitStopRoutine(float freezeDuration, float targetScale)
        {
            // Overlapping kill freezes share one remembered time scale (see HitStopTimeScale)
            Cyberpunk.Core.HitStopTimeScale.Begin(targetScale);
            try
            {
                yield return new WaitForSecondsRealtime(freezeDuration);
            }
            finally
            {
                Cyberpunk.Core.HitStopTimeScale.End();
            }
        }

        public void CleanupArmy()
        {
            foreach (var pilot in _pilots)
            {
                if (pilot.rootObject != null)
                {
                    Destroy(pilot.rootObject);
                }
            }
            _pilots.Clear();

            foreach (var pod in _dropPods)
            {
                if (pod.rootObject != null)
                {
                    Destroy(pod.rootObject);
                }
            }
            _dropPods.Clear();

            isArmySpawned = false;
            isArmyFiring = false;
            arePodsLanded = false;
        }

        private void OnDestroy()
        {
            CleanupArmy();
        }
    }
}
