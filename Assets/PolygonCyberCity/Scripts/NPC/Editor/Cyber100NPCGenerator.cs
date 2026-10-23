#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.AI;
using PolygonCyberCity.NPC.Spawner;
using PolygonCyberCity.NPC.Animation;
using PolygonCyberCity.NPC.BT;

namespace PolygonCyberCity.NPC.Editor
{
    /// <summary>
    /// Automated AAA Cybernetic NPC Prefab Generator & Batch Spawner.
    /// Creates 100 uniquely customized Cyberpunk NPCs using Polygon CyberCity characters,
    /// cybernetic head/body attachments, colorway materials, and scanner profiles.
    /// </summary>
    public static class Cyber100NPCGenerator
    {
        public const string OutputFolder = "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs";
        private const string CharPath = "Assets/PolygonCyberCity/Prefabs/Characters/";
        private const string AttachPath = "Assets/PolygonCyberCity/Prefabs/Characters/Attachments/";
        private const string MatPath = "Assets/PolygonCyberCity/Materials/Alts/";

        // Base model pools
        private static readonly string[] MaleBaseModels = new string[]
        {
            "SM_Chr_Cyborg_Male_01",
            "SM_Chr_Male_Soldier_01",
            "SM_Chr_Mercenary_Male_01",
            "SM_Chr_Street_Male_01",
            "SM_Chr_Rich_Male_01",
            "SM_Chr_Doctor_01",
            "SM_Chr_PleasureBot_Male_01"
        };

        private static readonly string[] FemaleBaseModels = new string[]
        {
            "SM_Chr_Cyborg_Female_01",
            "SM_Chr_Female_Soldier_01",
            "SM_Chr_Mercenary_Female_01",
            "SM_Chr_Racer_Female_01",
            "SM_Chr_Rich_Female_01"
        };

        private static readonly string[] DroidBaseModels = new string[]
        {
            "SM_Chr_Robot_01",
            "SM_Chr_Robot_Female_01",
            "SM_Chr_Robot_Mercenary_01",
            "SM_Chr_Robot_Damaged_01",
            "SM_Chr_Fancy_Bot_01",
            "SM_Chr_Helper_Bot_01"
        };

        // Cybernetic Attachments
        private static readonly string[] MaleCybernetics = new string[]
        {
            "SM_Chr_Attach_Cybernetics_Male_01",
            "SM_Chr_Attach_Cybernetics_Male_02",
            "SM_Chr_Attach_Cybernetics_Male_03",
            "SM_Chr_Attach_Cybernetics_Male_04",
            "SM_Chr_Attach_Cybernetics_Male_05",
            "SM_Chr_Attach_Cybernetics_Male_06"
        };

        private static readonly string[] FemaleCybernetics = new string[]
        {
            "SM_Chr_Attach_Cybernetics_Female_01",
            "SM_Chr_Attach_Cybernetics_Female_02",
            "SM_Chr_Attach_Cybernetics_Female_03",
            "SM_Chr_Attach_Cybernetics_Female_04",
            "SM_Chr_Attach_Cybernetics_Female_05"
        };

        // Extra Tactical & Cyber gear
        private static readonly string[] UniversalTacticalGear = new string[]
        {
            "SM_Chr_Attach_Headset_01",
            "SM_Chr_Attach_Headset_02",
            "SM_Chr_Attach_Helmet_Soldier_01",
            "SM_Chr_Attach_Helmet_Soldier_02",
            "SM_Chr_Attach_Helmet_Soldier_03",
            "SM_Chr_Attach_Mask_Male_01",
            "SM_Chr_Attach_Backpack_01",
            "SM_Chr_Attach_Piercings_Male_01",
            "SM_Chr_Attach_Piercings_Female_01",
            "SM_Chr_Attach_Piercings_Female_02"
        };

        // Material Alts (Neon Blue, Cyber Gold, Neon Magenta, Carbon Black)
        private static readonly string[] MaterialAlts = new string[]
        {
            "PolygonCyberCity_01_A", "PolygonCyberCity_01_B", "PolygonCyberCity_01_C",
            "PolygonCyberCity_02_A", "PolygonCyberCity_02_B", "PolygonCyberCity_02_C",
            "PolygonCyberCity_03_A", "PolygonCyberCity_03_B", "PolygonCyberCity_03_C",
            "PolygonCyberCity_04_A", "PolygonCyberCity_04_B", "PolygonCyberCity_04_C"
        };

        // Cyberpunk First Names
        private static readonly string[] FirstNames = new string[]
        {
            "Jax", "Nyx", "Riku", "Vektor", "Aria", "Cipher", "Gideon", "Echo", "Kira", "Zane",
            "Sora", "Dante", "Nova", "Bex", "Talon", "Raven", "Silas", "Lexa", "Kai", "Jett",
            "Valerie", "Cortex", "Rook", "Blaze", "Vesper", "Orion", "Lyra", "Kestrel", "Reeve", "Syn",
            "Malik", "Nadia", "Cross", "Zero", "Mirei", "Flint", "Trace", "Vaughn", "Shade", "Quinn"
        };

        // Cyberpunk Surnames / Aliases
        private static readonly string[] Surnames = new string[]
        {
            "Wire", "Chromium", "0x7F", "Cruz", "Overclock", "Chen", "9", "Stryker", "Glitch", "Mercer",
            "Pulse", "Nexus", "Arasaka-Ex", "Ghost", "Byte", "Ward", "Neon", "Kowalski", "Steel", "Vance",
            "Voltage", "Holloway", "Blackwood", "Frost", "Rezvani", "Drake", "Sterling", "Cross", "Synapse", "Vortex",
            "Shadow", "Zero", "Chrome", "Spire", "Titan", "Specter", "Razor", "Drift", "Static", "Ignite"
        };

        // Cyberware Augments
        private static readonly string[] CyberwareList = new string[]
        {
            "Mantis Blades Mk.4 & Kiroshi Optics v3.2",
            "Sandevistan Reflex Booster & Subdermal Armor",
            "Neural Coprocessor & Monowire Port",
            "Synaptic Accelerator & Titanium Bone Lacing",
            "Thermal Optics & Smart-Link Cyberware",
            "Overclock Module & Kerenzikov Booster",
            "Subdermal Ballistic Mesh & Cybernetic Heart",
            "Gorilla Arms Actuator & Dermal Plating",
            "Cranial ICE-Breaker & Interface Jacks",
            "Biomonitor V4 & Adrenaline Booster",
            "Second Heart Auxiliary & Blood Pump",
            "Microgenerator Pulse & Optical Camo Subdermal",
            "Pain Editor Implant & Cyber-Spine Mk.II",
            "Targeting Telemetry & Cybernetic Rebreather",
            "Cyber-Arm High-Torque Servo & Ocular Visor",
            "Subdermal Neuro-Weave & Reflex Tuner",
            "Bio-Conductor & Cyberware Compression Grid",
            "Memory Boost Chip & Synaptic Signal Optimizer",
            "Tactical Threat Assessor & Cyber-Audio Rig",
            "Reinforced Tendons & Subdermal Shock Absorbers"
        };

        [MenuItem("Tools/CyberCity/Generate 100 Cybernetic NPC Prefabs")]
        public static void Generate100Prefabs()
        {
            if (!AssetDatabase.IsValidFolder("Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs"))
            {
                AssetDatabase.CreateFolder("Assets/PolygonCyberCity/Prefabs", "Generated_100_CyberNPCs");
            }

            // Load shared assets
            var animController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/PolygonCyberCity/Animations/AC_CyberNPC.controller")
                              ?? AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/SyntyCombined/AC_SyntyCombined.controller");
            var defaultAvatar = AssetDatabase.LoadAssetAtPath<Avatar>("Assets/PolygonCyberCity/Models/Characters.fbx");

            var civilianProfile = AssetDatabase.LoadAssetAtPath<NPCAnimationProfile>("Assets/PolygonCyberCity/Data/AnimProfiles/CivilianAnimProfile.asset");
            var mercenaryProfile = AssetDatabase.LoadAssetAtPath<NPCAnimationProfile>("Assets/PolygonCyberCity/Data/AnimProfiles/MercenaryAnimProfile.asset");
            var securityProfile = AssetDatabase.LoadAssetAtPath<NPCAnimationProfile>("Assets/PolygonCyberCity/Data/AnimProfiles/SecurityAnimProfile.asset");
            var helperBotProfile = AssetDatabase.LoadAssetAtPath<NPCAnimationProfile>("Assets/PolygonCyberCity/Data/AnimProfiles/HelperBotAnimProfile.asset");
            var richProfile = AssetDatabase.LoadAssetAtPath<NPCAnimationProfile>("Assets/PolygonCyberCity/Data/AnimProfiles/RichCitizenAnimProfile.asset");

            int generatedCount = 0;
            Random.InitState(20261005); // deterministic seed for reproducible high quality

            for (int i = 1; i <= 100; i++)
            {
                int categoryChoice = i % 10;
                bool isFemale = (i % 3 == 0);
                bool isDroid = (categoryChoice == 0 || categoryChoice == 9);

                string baseName;
                if (isDroid)
                {
                    baseName = DroidBaseModels[i % DroidBaseModels.Length];
                }
                else if (isFemale)
                {
                    baseName = FemaleBaseModels[i % FemaleBaseModels.Length];
                }
                else
                {
                    baseName = MaleBaseModels[i % MaleBaseModels.Length];
                }

                GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharPath + baseName + ".prefab");
                if (basePrefab == null)
                {
                    Debug.LogWarning($"[Cyber100NPC] Base prefab missing: {baseName}");
                    continue;
                }

                GameObject npcInstance = Object.Instantiate(basePrefab);
                
                string charFirstName = FirstNames[(i * 3 + 7) % FirstNames.Length];
                string charSurname = Surnames[(i * 7 + 13) % Surnames.Length];
                string fullCharName = $"{charFirstName} {charSurname}";
                string safeFileName = $"CyberNPC_{i:D3}_{charFirstName}_{charSurname}".Replace(" ", "_").Replace("'", "");
                npcInstance.name = safeFileName;

                // Pick and apply material alt
                string matName = MaterialAlts[(i + 2) % MaterialAlts.Length];
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath + matName + ".mat");
                if (mat != null)
                {
                    foreach (var smr in npcInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        var mats = smr.sharedMaterials;
                        for (int m = 0; m < mats.Length; m++) mats[m] = mat;
                        smr.sharedMaterials = mats;
                    }
                }

                // Locate Head and Spine bones for attachments
                Transform headBone = FindBone(npcInstance.transform, "Head");
                Transform spineBone = FindBone(npcInstance.transform, "Spine_02") ?? FindBone(npcInstance.transform, "Spine_01");

                // Pick 1 Primary Cybernetic Attachment
                string cyberAttachName;
                if (isFemale && !isDroid)
                {
                    cyberAttachName = FemaleCybernetics[(i * 2) % FemaleCybernetics.Length];
                }
                else
                {
                    cyberAttachName = MaleCybernetics[(i * 2) % MaleCybernetics.Length];
                }

                AttachPrefab(npcInstance, cyberAttachName, headBone, mat);

                // 40% chance of a secondary tactical attachment (Headset, Mask, or Backpack)
                if (i % 2 == 0)
                {
                    string secAttach = UniversalTacticalGear[(i * 5) % UniversalTacticalGear.Length];
                    Transform targetBone = secAttach.Contains("Backpack") ? spineBone : headBone;
                    AttachPrefab(npcInstance, secAttach, targetBone, mat);
                }

                // Configure Animator
                var anim = npcInstance.GetComponentInChildren<Animator>();
                if (anim == null) anim = npcInstance.AddComponent<Animator>();
                if (animController != null) anim.runtimeAnimatorController = animController;
                if (anim.avatar == null && defaultAvatar != null) anim.avatar = defaultAvatar;
                anim.applyRootMotion = false;

                // Configure NavMeshAgent
                var agent = npcInstance.GetComponent<NavMeshAgent>();
                if (agent == null) agent = npcInstance.AddComponent<NavMeshAgent>();
                agent.speed = Random.Range(1.8f, 3.2f);
                agent.angularSpeed = 240f;
                agent.acceleration = 12f;
                agent.stoppingDistance = 0.5f;
                agent.radius = 0.35f;
                agent.height = 1.8f;
                agent.avoidancePriority = Random.Range(30, 70);

                // Configure Collider
                var col = npcInstance.GetComponent<CapsuleCollider>();
                if (col == null) col = npcInstance.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0, 0.9f, 0);
                col.radius = 0.35f;
                col.height = 1.8f;

                // Configure BehaviorTreeRunner
                var btRunner = npcInstance.GetComponent<BehaviorTreeRunner>();
                if (btRunner == null) btRunner = npcInstance.AddComponent<BehaviorTreeRunner>();

                // Configure CyberNPCController
                var ctrl = npcInstance.GetComponent<CyberNPCController>();
                if (ctrl == null) ctrl = npcInstance.AddComponent<CyberNPCController>();

                // Archetype & Faction selection
                NPCType type;
                NPCFaction faction;
                ThreatRating threat;
                NPCAnimationProfile profile;

                if (isDroid)
                {
                    type = NPCType.HelperBot;
                    faction = NPCFaction.ServiceRobotics;
                    threat = ThreatRating.Low;
                    profile = helperBotProfile;
                }
                else if (baseName.Contains("Soldier"))
                {
                    type = NPCType.StreetThug;
                    faction = (i % 4 == 0) ? NPCFaction.ArasakaSecurity : NPCFaction.TygerClaws;
                    threat = ThreatRating.Moderate;
                    profile = securityProfile;
                }
                else if (baseName.Contains("Mercenary"))
                {
                    type = NPCType.Mercenary;
                    faction = (i % 3 == 0) ? NPCFaction.Maelstrom : NPCFaction.TraumaTeam;
                    threat = (i % 5 == 0) ? ThreatRating.Lethal : ThreatRating.High;
                    profile = mercenaryProfile;
                }
                else if (baseName.Contains("Rich"))
                {
                    type = NPCType.RichCitizen;
                    faction = NPCFaction.Civilians;
                    threat = ThreatRating.Low;
                    profile = richProfile;
                }
                else if (baseName.Contains("Cyborg"))
                {
                    type = NPCType.Cyborg;
                    faction = (i % 2 == 0) ? NPCFaction.Moxes : NPCFaction.Maelstrom;
                    threat = ThreatRating.Moderate;
                    profile = civilianProfile;
                }
                else
                {
                    type = NPCType.Civilian;
                    faction = NPCFaction.Civilians;
                    threat = ThreatRating.Low;
                    profile = civilianProfile;
                }

                ctrl.npcType = type;
                ctrl.faction = faction;
                ctrl.threatRating = threat;
                ctrl.npcAnimatorController = animController;
                ctrl.animationProfile = profile;
                ctrl.walkSpeed = agent.speed;
                ctrl.runSpeed = agent.speed * 2.2f;
                ctrl.maxHealth = threat == ThreatRating.Lethal ? 250f : (threat == ThreatRating.High ? 180f : 100f);

                // Configure Scanner Profile
                ctrl.scannerProfile = new ScannerProfile
                {
                    CharacterName = fullCharName,
                    Cyberware = CyberwareList[(i * 4) % CyberwareList.Length],
                    Faction = faction,
                    Threat = threat,
                    Bounty = threat == ThreatRating.Low ? Random.Range(350f, 1500f) :
                            (threat == ThreatRating.Moderate ? Random.Range(2000f, 8500f) :
                            (threat == ThreatRating.High ? Random.Range(12000f, 32000f) : Random.Range(45000f, 95000f)))
                };

                // Save Prefab
                string savePath = $"{OutputFolder}/{safeFileName}.prefab";
                PrefabUtility.SaveAsPrefabAsset(npcInstance, savePath);
                Object.DestroyImmediate(npcInstance);

                generatedCount++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"<color=#00F0FF><b>[Cyber100NPC]</b> Successfully generated and saved {generatedCount} unique Cybernetic NPC Prefabs into {OutputFolder}!</color>");
        }

        private static void AttachPrefab(GameObject parentCharacter, string attachPrefabName, Transform targetBone, Material tintMat)
        {
            if (targetBone == null) return;
            string fullPath = AttachPath + attachPrefabName + ".prefab";
            GameObject attachPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(fullPath);
            if (attachPrefab == null) return;

            GameObject attachInstance = Object.Instantiate(attachPrefab, targetBone);
            attachInstance.name = attachPrefabName;
            attachInstance.transform.localPosition = Vector3.zero;
            attachInstance.transform.localRotation = Quaternion.identity;
            attachInstance.transform.localScale = Vector3.one;

            if (tintMat != null)
            {
                foreach (var mr in attachInstance.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var mats = mr.sharedMaterials;
                    for (int m = 0; m < mats.Length; m++) mats[m] = tintMat;
                    mr.sharedMaterials = mats;
                }
            }
        }

        private static Transform FindBone(Transform root, string boneName)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.Equals(boneName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return t;
                }
            }
            return null;
        }

        [MenuItem("Tools/CyberCity/Spawn 100 Cybernetic NPCs in Scene")]
        public static void Spawn100InActiveScene()
        {
            if (!AssetDatabase.IsValidFolder(OutputFolder))
            {
                Debug.LogError("[Cyber100NPC] Output folder does not exist. Please generate prefabs first!");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new string[] { OutputFolder });
            if (guids.Length == 0)
            {
                Debug.LogError("[Cyber100NPC] No prefabs found in " + OutputFolder);
                return;
            }

            GameObject container = GameObject.Find("Spawned_100_CyberNPCs");
            if (container != null)
            {
                Undo.DestroyObjectImmediate(container);
            }
            container = new GameObject("Spawned_100_CyberNPCs");
            Undo.RegisterCreatedObjectUndo(container, "Spawn 100 Cyber NPCs");

            Transform player = GameObject.FindWithTag("Player")?.transform;
            Vector3 center = player != null ? player.position : Vector3.zero;

            int spawned = 0;
            // Spawn in a grid / perimeter pattern around the scene center
            int cols = 10;
            float spacing = 2.5f;

            for (int i = 0; i < guids.Length && i < 100; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                int row = i / cols;
                int col = i % cols;
                Vector3 spawnPos = center + new Vector3((col - cols / 2f) * spacing, 0f, (row - cols / 2f) * spacing);

                // Sample NavMesh to place accurately on ground
                if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 5.0f, NavMesh.AllAreas))
                {
                    spawnPos = hit.position;
                }

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, container.transform);
                instance.transform.position = spawnPos;
                instance.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
                spawned++;
            }

            Debug.Log($"<color=#00FF88><b>[Cyber100NPC]</b> Successfully spawned {spawned} Cybernetic NPCs into active scene under '{container.name}'!</color>");
        }

        [MenuItem("Tools/CyberCity/Clear Spawned NPCs in Scene")]
        public static void ClearSpawnedNPCs()
        {
            GameObject container = GameObject.Find("Spawned_100_CyberNPCs");
            if (container != null)
            {
                Undo.DestroyObjectImmediate(container);
                Debug.Log("[Cyber100NPC] Cleared spawned NPCs container from scene.");
            }
        }
    }
}
#endif
