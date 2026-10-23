using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Synty.SidekickCharacters.API;
using Synty.SidekickCharacters.Database;
using Synty.SidekickCharacters.Database.DTO;
using Synty.SidekickCharacters.Enums;
using Synty.SidekickCharacters.Serialization;
using Synty.SidekickCharacters.Utils;
using Unity.VisualScripting.YamlDotNet.Serialization;
using PolygonCyberCity.NPC.Spawner;

namespace Synty.SidekickCharacters.Editor
{
    [InitializeOnLoad]
    public static class DoctorCreator
    {
        // Central Characters folder locations
        public const string CharacterDoctorDir = "Assets/Characters/Doctor";
        public const string CharacterDoctorPrefab = "Assets/Characters/Doctor/Doctor.prefab";
        public const string CharacterDoctorFbx = "Assets/Characters/Doctor/Doctor.fbx";
        public const string CharactersRootPrefab = "Assets/Characters/Doctor.prefab";

        // Polygon CyberCity Character prefab location
        public const string CyberCityDoctorPrefab = "Assets/PolygonCyberCity/Prefabs/Characters/SM_Chr_Doctor_01.prefab";

        // Legacy / Demo fallback paths
        public const string PrefabSavePathAssets = "Assets/Doctor.prefab";
        public const string PrefabSavePathPrefabs = "Assets/Prefabs/Doctor.prefab";
        public const string PrefabSavePathSaved = "Assets/Synty/SidekickCharacters/_Demos/SavedCharacters/Doctor.prefab";

        static DoctorCreator()
        {
            EditorApplication.delayCall += AutoGenerateIfNotExists;
        }

        private static void AutoGenerateIfNotExists()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorPrefs.GetBool("DoctorCharacterInCharacters_v2", false))
            {
                EditorPrefs.SetBool("DoctorCharacterInCharacters_v2", true);
                CreateDoctor();
            }
        }

        [MenuItem("Tools/Sidekick/Generate Doctor Character", false, 1)]
        [MenuItem("Tools/Characters/Add Doctor Model in Characters", false, 2)]
        public static GameObject CreateDoctor()
        {
            Debug.Log("[DoctorCreator] Generating authentic Cutscene Cyber-Doctor (Ido) in Characters...");

            // 1. Ensure all destination directories exist
            Directory.CreateDirectory("Assets/Characters");
            Directory.CreateDirectory(CharacterDoctorDir);
            Directory.CreateDirectory($"{CharacterDoctorDir}/Materials");
            Directory.CreateDirectory($"{CharacterDoctorDir}/Textures");
            Directory.CreateDirectory("Assets/PolygonCyberCity/Prefabs/Characters");
            Directory.CreateDirectory("Assets/Prefabs");
            Directory.CreateDirectory("Assets/Synty/SidekickCharacters/_Demos/SavedCharacters");
            Directory.CreateDirectory("Assets/Materials");

            // Cutscene Doctor (SM_Chr_Rich_Male_01)
            string cutsceneDocPath = "Assets/PolygonCyberCity/Prefabs/Characters/SM_Chr_Rich_Male_01.prefab";
            GameObject cutscenePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(cutsceneDocPath);
            if (cutscenePrefab != null)
            {
                GameObject docObj = UnityEngine.Object.Instantiate(cutscenePrefab);
                docObj.name = "Doctor";
                docObj.transform.position = Vector3.zero;
                docObj.transform.rotation = Quaternion.identity;

                Animator docAnim = docObj.GetComponent<Animator>();
                if (docAnim != null)
                {
                    var cutsceneAc = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/PolygonCyberCity/Animations/AC_CyberNPC.controller");
                    if (cutsceneAc != null) docAnim.runtimeAnimatorController = cutsceneAc;
                    docAnim.applyRootMotion = false;
                }

                var docCol = docObj.GetComponent<CapsuleCollider>() ?? docObj.AddComponent<CapsuleCollider>();
                docCol.center = new Vector3(0f, 0.9f, 0f);
                docCol.height = 1.8f;
                docCol.radius = 0.35f;

                var docAgent = docObj.GetComponent<UnityEngine.AI.NavMeshAgent>() ?? docObj.AddComponent<UnityEngine.AI.NavMeshAgent>();
                docAgent.speed = 1.8f;
                docAgent.stoppingDistance = 0.5f;

                var docCtrl = docObj.GetComponent<CyberNPCController>() ?? docObj.AddComponent<CyberNPCController>();
                docCtrl.npcType = NPCType.Civilian;
                docCtrl.hitCollider = docCol;
                docCtrl.agent = docAgent;
                docCtrl.animator = docAnim;

                SavePrefabSafe(docObj, CharacterDoctorPrefab);
                SavePrefabSafe(docObj, CharactersRootPrefab);
                SavePrefabSafe(docObj, CyberCityDoctorPrefab);
                SavePrefabSafe(docObj, PrefabSavePathPrefabs);
                SavePrefabSafe(docObj, PrefabSavePathAssets);
                SavePrefabSafe(docObj, PrefabSavePathSaved);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"[DoctorCreator] SUCCESS! Cutscene Doctor model fully saved to: '{CharacterDoctorPrefab}'!");
                Selection.activeGameObject = docObj;
                return docObj;
            }

            // 2. Load Base Model and Base Material
            var dbManager = new DatabaseManager();
            GameObject baseModel = Resources.Load<GameObject>("Meshes/SK_BaseModel");
            Material baseMat = Resources.Load<Material>("Materials/M_BaseMaterial");

            if (baseModel == null)
            {
                baseModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/SidekickCharacters/Resources/Meshes/SK_BaseModel.fbx");
            }
            if (baseMat == null)
            {
                baseMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Synty/SidekickCharacters/Resources/Materials/M_BaseMaterial.mat");
            }

            if (baseModel == null || baseMat == null)
            {
                Debug.LogError("[DoctorCreator] Required SK_BaseModel or M_BaseMaterial not found.");
                return null;
            }

            // 3. Find Humanoid Avatar from SK_BaseModel
            Avatar humanoidAvatar = null;
            var subAssets = AssetDatabase.LoadAllAssetsAtPath("Assets/Synty/SidekickCharacters/Resources/Meshes/SK_BaseModel.fbx");
            foreach (var sub in subAssets)
            {
                if (sub is Avatar av && av.isHuman)
                {
                    humanoidAvatar = av;
                    break;
                }
            }
            if (humanoidAvatar == null)
            {
                var baseAnimator = baseModel.GetComponentInChildren<Animator>();
                if (baseAnimator != null) humanoidAvatar = baseAnimator.avatar;
            }

            // 4. Create / Load persistent Doctor Material
            string doctorMatPath = $"{CharacterDoctorDir}/Materials/M_Doctor.mat";
            Material doctorMat = AssetDatabase.LoadAssetAtPath<Material>(doctorMatPath);
            if (doctorMat == null)
            {
                doctorMat = new Material(baseMat);
                doctorMat.name = "M_Doctor";
                AssetDatabase.CreateAsset(doctorMat, doctorMatPath);
            }

            string legacyMatPath = "Assets/Materials/M_Doctor.mat";
            if (!File.Exists(legacyMatPath))
            {
                AssetDatabase.CopyAsset(doctorMatPath, legacyMatPath);
            }

            var runtime = new SidekickRuntime(baseModel, baseMat, null, dbManager);

            // Populate all mapped parts
            try
            {
                SidekickRuntime.PopulateToolData(runtime).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DoctorCreator] Exception populating tool data: {ex}");
            }

            var partLibrary = runtime.MappedPartDictionary;
            if (partLibrary == null || partLibrary.Count == 0)
            {
                Debug.LogError("[DoctorCreator] Part library is empty or unpopulated.");
                return null;
            }

            // 5. Curated Doctor Parts: Sci-Fi lab coat, ocular diagnostic scanner, human features
            var desiredParts = new Dictionary<CharacterPartType, string>()
            {
                { CharacterPartType.Head, "SK_HUMN_BASE_01_01HEAD_HU01" },
                { CharacterPartType.Hair, "SK_HUMN_BASE_02_02HAIR_HU01" },
                { CharacterPartType.EyebrowLeft, "SK_HUMN_BASE_01_03EBRL_HU01" },
                { CharacterPartType.EyebrowRight, "SK_HUMN_BASE_01_04EBRR_HU01" },
                { CharacterPartType.EyeLeft, "SK_HUMN_BASE_01_05EYEL_HU01" },
                { CharacterPartType.EyeRight, "SK_HUMN_BASE_01_06EYER_HU01" },
                { CharacterPartType.EarLeft, "SK_HUMN_BASE_01_07EARL_HU01" },
                { CharacterPartType.EarRight, "SK_HUMN_BASE_01_08EARR_HU01" },
                { CharacterPartType.FacialHair, "SK_HUMN_BASE_01_09FCHR_HU01" },
                { CharacterPartType.Nose, "SK_HUMN_BASE_01_35NOSE_HU01" },
                { CharacterPartType.Teeth, "SK_HUMN_BASE_01_36TETH_HU01" },
                { CharacterPartType.Tongue, "SK_HUMN_BASE_01_37TONG_HU01" },
                { CharacterPartType.Torso, "SK_SCFI_CIVL_09_10TORS_HU01" },          // Medical / Lab Coat
                { CharacterPartType.ArmUpperLeft, "SK_SCFI_CIVL_09_11AUPL_HU01" },
                { CharacterPartType.ArmUpperRight, "SK_SCFI_CIVL_09_12AUPR_HU01" },
                { CharacterPartType.ArmLowerLeft, "SK_SCFI_CIVL_09_13ALWL_HU01" },
                { CharacterPartType.ArmLowerRight, "SK_SCFI_CIVL_09_14ALWR_HU01" },
                { CharacterPartType.HandLeft, "SK_SCFI_CIVL_09_15HNDL_HU01" },
                { CharacterPartType.HandRight, "SK_SCFI_CIVL_09_16HNDR_HU01" },
                { CharacterPartType.Hips, "SK_SCFI_CIVL_09_17HIPS_HU01" },
                { CharacterPartType.LegLeft, "SK_SCFI_CIVL_09_18LEGL_HU01" },
                { CharacterPartType.LegRight, "SK_SCFI_CIVL_09_19LEGR_HU01" },
                { CharacterPartType.FootLeft, "SK_SCFI_CIVL_09_20FOTL_HU01" },
                { CharacterPartType.FootRight, "SK_SCFI_CIVL_09_21FOTR_HU01" },
                { CharacterPartType.AttachmentFace, "SK_SCFI_CIVL_09_23AFAC_HU01" }, // Ocular Scanner Visor
                { CharacterPartType.Wrap, "SK_HUMN_BASE_01_38WRAP_HU01" }
            };

            List<SkinnedMeshRenderer> partsToUse = new List<SkinnedMeshRenderer>();
            Dictionary<string, Mesh> sourceMeshes = new Dictionary<string, Mesh>();

            foreach (var kvp in desiredParts)
            {
                CharacterPartType partType = kvp.Key;
                string partName = kvp.Value;

                if (!partLibrary.TryGetValue(partType, out var dict) || dict == null || dict.Count == 0)
                {
                    continue;
                }

                SidekickPart part = null;
                if (!string.IsNullOrEmpty(partName) && dict.TryGetValue(partName, out part))
                {
                    // Exact match found
                }
                else
                {
                    foreach (var p in dict.Values)
                    {
                        part = p;
                        break;
                    }
                }

                if (part != null)
                {
                    GameObject partModel = part.GetPartModel();
                    if (partModel != null)
                    {
                        SkinnedMeshRenderer[] smrs = partModel.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                        foreach (var smr in smrs)
                        {
                            if (smr != null)
                            {
                                partsToUse.Add(smr);
                                if (smr.sharedMesh != null && !sourceMeshes.ContainsKey(smr.name))
                                {
                                    sourceMeshes[smr.name] = smr.sharedMesh;
                                }
                            }
                        }
                    }
                }
            }

            if (partsToUse.Count == 0)
            {
                Debug.LogError("[DoctorCreator] No valid parts could be loaded for Doctor character.");
                return null;
            }

            // 6. Assemble Doctor character hierarchy with Sidekick runtime
            GameObject doctorObj = runtime.CreateCharacter("Doctor", partsToUse, false, true);
            if (doctorObj == null)
            {
                Debug.LogError("[DoctorCreator] runtime.CreateCharacter failed.");
                return null;
            }

            doctorObj.transform.position = Vector3.zero;
            doctorObj.transform.rotation = Quaternion.identity;

            // 7. CRITICAL FIX: Re-bind real FBX Asset Meshes so Prefab has non-zero mesh assets
            foreach (var smr in doctorObj.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr != null)
                {
                    if (sourceMeshes.TryGetValue(smr.name, out Mesh assetMesh) && assetMesh != null)
                    {
                        smr.sharedMesh = assetMesh;
                    }
                    smr.sharedMaterial = doctorMat;
                    smr.updateWhenOffscreen = true;
                }
            }

            // 8. Attach authentic Doctor Medic Badges & Pouches
            AttachDoctorAccessories(doctorObj);

            // 9. Configure Humanoid Animator & CyberNPC Components
            Animator anim = doctorObj.GetComponent<Animator>();
            if (anim == null) anim = doctorObj.AddComponent<Animator>();

            var ac = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/PolygonCyberCity/Animations/AC_CyberNPC.controller");
            if (ac != null) anim.runtimeAnimatorController = ac;
            if (humanoidAvatar != null) anim.avatar = humanoidAvatar;
            anim.applyRootMotion = false;
            anim.enabled = true;

            // CapsuleCollider
            var col = doctorObj.GetComponent<CapsuleCollider>();
            if (col == null) col = doctorObj.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.9f, 0f);
            col.radius = 0.35f;
            col.height = 1.8f;

            // NavMeshAgent
            var agent = doctorObj.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent == null) agent = doctorObj.AddComponent<UnityEngine.AI.NavMeshAgent>();
            agent.speed = 1.8f;
            agent.angularSpeed = 360f;
            agent.acceleration = 12f;
            agent.stoppingDistance = 0.5f;
            agent.radius = 0.35f;
            agent.height = 1.8f;

            // CyberNPCController
            var controller = doctorObj.GetComponent<CyberNPCController>();
            if (controller == null) controller = doctorObj.AddComponent<CyberNPCController>();
            controller.npcType = NPCType.Civilian;
            controller.hitCollider = col;
            controller.agent = agent;
            controller.animator = anim;

            // 10. Attempt FBX Export to Assets/Characters/Doctor/Doctor.fbx
            ExportDoctorFbx(doctorObj);

            // 11. Save Doctor Prefabs in all key Characters folders
            SavePrefabSafe(doctorObj, CharacterDoctorPrefab);
            SavePrefabSafe(doctorObj, CharactersRootPrefab);
            SavePrefabSafe(doctorObj, CyberCityDoctorPrefab);
            SavePrefabSafe(doctorObj, PrefabSavePathPrefabs);
            SavePrefabSafe(doctorObj, PrefabSavePathAssets);
            SavePrefabSafe(doctorObj, PrefabSavePathSaved);

            // 12. Save Sidekick .sk serialized definitions and character textures
            SaveDoctorSkDefinition("Doctor_Ido", desiredParts);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeGameObject = doctorObj;
            Debug.Log($"[DoctorCreator] SUCCESS! Doctor model fully added to Characters: '{CharacterDoctorPrefab}' and '{CyberCityDoctorPrefab}'!");
            return doctorObj;
        }

        private static void AttachDoctorAccessories(GameObject doctorObj)
        {
            // Medic Patch on Chest
            var patchPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/Attachments/SM_Chr_Attach_Patch_Medic_01.prefab");
            if (patchPrefab != null)
            {
                Transform spine02 = FindChildRecursive(doctorObj.transform, "spine_02") ?? FindChildRecursive(doctorObj.transform, "spine_01");
                if (spine02 != null)
                {
                    GameObject patch = UnityEngine.Object.Instantiate(patchPrefab, spine02);
                    patch.name = "Attach_Medic_Patch";
                    patch.transform.localPosition = new Vector3(0.08f, 0.12f, 0.12f);
                    patch.transform.localRotation = Quaternion.Euler(0f, 15f, 0f);
                    patch.transform.localScale = Vector3.one;
                }
            }

            // Medic Pouch on Hip Belt
            var pouchPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/Attachments/SM_Chr_Attach_Pouch_Medic_01.prefab");
            if (pouchPrefab != null)
            {
                Transform pelvis = FindChildRecursive(doctorObj.transform, "pelvis") ?? doctorObj.transform;
                if (pelvis != null)
                {
                    GameObject pouch = UnityEngine.Object.Instantiate(pouchPrefab, pelvis);
                    pouch.name = "Attach_Medic_Pouch";
                    pouch.transform.localPosition = new Vector3(0.18f, -0.06f, 0.02f);
                    pouch.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                    pouch.transform.localScale = Vector3.one * 0.9f;
                }
            }
        }

        private static void ExportDoctorFbx(GameObject rootObj)
        {
            try
            {
                Type modelExporterType = Type.GetType("UnityEditor.Formats.Fbx.Exporter.ModelExporter, Unity.Formats.Fbx.Editor");
                if (modelExporterType != null)
                {
                    var exportMethod = modelExporterType.GetMethod("ExportObject", new[] { typeof(string), typeof(UnityEngine.Object) });
                    if (exportMethod != null)
                    {
                        string exportedPath = (string)exportMethod.Invoke(null, new object[] { CharacterDoctorFbx, rootObj });
                        if (!string.IsNullOrEmpty(exportedPath))
                        {
                            AssetDatabase.ImportAsset(CharacterDoctorFbx, ImportAssetOptions.ForceUpdate);
                            ModelImporter importer = AssetImporter.GetAtPath(CharacterDoctorFbx) as ModelImporter;
                            if (importer != null)
                            {
                                importer.animationType = ModelImporterAnimationType.Human;
                                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                                importer.SaveAndReimport();
                            }
                            Debug.Log($"[DoctorCreator] Exported standalone Humanoid FBX: {CharacterDoctorFbx}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DoctorCreator] FBX export note (using prefab mesh links directly): {ex.Message}");
            }
        }

        private static void SavePrefabSafe(GameObject obj, string path)
        {
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                PrefabUtility.SaveAsPrefabAsset(obj, path);
                Debug.Log($"[DoctorCreator] Saved prefab to {path}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DoctorCreator] Failed saving prefab to {path}: {ex}");
            }
        }

        private static void SaveDoctorSkDefinition(string characterName, Dictionary<CharacterPartType, string> partsDict)
        {
            try
            {
                var skCharacter = new SerializedCharacter
                {
                    Name = characterName,
                    Species = 1 // Human
                };

                var partsList = new List<SerializedPart>();
                foreach (var kvp in partsDict)
                {
                    partsList.Add(new SerializedPart(kvp.Value, kvp.Key, "1"));
                }
                skCharacter.Parts = partsList;

                var dbManager = new DatabaseManager();
                var colorSet = SidekickColorSet.GetDefault(dbManager);
                var species = SidekickSpecies.GetByID(dbManager, 1);
                var savedSet = new SerializedColorSet();
                if (colorSet != null && species != null)
                {
                    savedSet.PopulateFromSidekickColorSet(colorSet, species);
                    skCharacter.ColorSet = savedSet;
                }

                skCharacter.BlendShapes = new SerializedBlendShapeValues
                {
                    BodyTypeValue = 0f,
                    BodySizeValue = 0f,
                    MuscleValue = 20f
                };

                var serializer = new Serializer();
                string yaml = serializer.Serialize(skCharacter);

                string[] paths = new[]
                {
                    $"{CharacterDoctorDir}/Doctor.sk",
                    $"{CharacterDoctorDir}/Doctor_Ido.sk",
                    "Assets/Synty/SidekickCharacters/_Demos/SavedCharacters/Doctor_Ido.sk",
                    "Assets/Synty/SidekickCharacters/Characters/Doctor_Ido/Doctor_Ido.sk"
                };

                foreach (var path in paths)
                {
                    string dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    File.WriteAllText(path, yaml);
                    AssetDatabase.ImportAsset(path);
                }

                // Copy color maps into Doctor/Textures
                string srcColorMap = "Assets/Synty/SidekickCharacters/Characters/HumanSpecies/HumanSpecies_01/Textures/T_HumanSpecies_01ColorMap.png";
                if (File.Exists(srcColorMap))
                {
                    string dst1 = $"{CharacterDoctorDir}/Textures/T_Doctor_ColorMap.png";
                    string dst2 = $"{CharacterDoctorDir}/Textures/T_HumanSpecies_01ColorMap.png";
                    if (!File.Exists(dst1)) AssetDatabase.CopyAsset(srcColorMap, dst1);
                    if (!File.Exists(dst2)) AssetDatabase.CopyAsset(srcColorMap, dst2);
                }

                Debug.Log($"[DoctorCreator] Successfully saved Sidekick .sk character definitions for '{characterName}'.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DoctorCreator] Note during .sk generation: {ex.Message}");
            }
        }

        private static Transform FindChildRecursive(Transform parent, string name)
        {
            if (parent == null) return null;
            if (string.Equals(parent.name, name, StringComparison.OrdinalIgnoreCase)) return parent;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindChildRecursive(parent.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}