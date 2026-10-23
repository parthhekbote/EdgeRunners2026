#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Alita;

namespace Alita.Editor
{
    /// <summary>
    /// Editor Builder for the Tron: Ares / Tron: Legacy styled
    /// Awakening Cutscene in Dr. Dyson Ido's cybernetic clinic.
    /// Scene path: Assets/Scenes/Alita_Clinic_Awakening_Cutscene.unity
    /// </summary>
    public static class AlitaAwakeningSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Alita_Clinic_Awakening_Cutscene.unity";

        public const string PodPrefabPath = "Assets/PolygonCyberCity/Prefabs/Props/SM_Prop_Hibernation_Pod_01.prefab";
        public const string PodConsolePrefabPath = "Assets/PolygonCyberCity/Prefabs/Props/SM_Prop_Hibernation_Pod_Console_01.prefab";
        public const string HologramTablePrefabPath = "Assets/PolygonCyberCity/Prefabs/Props/SM_Prop_Hologram_Table_01.prefab";
        public const string ServerPrefabPath = "Assets/PolygonCyberCity/Prefabs/Props/SM_Prop_Server_01.prefab";
        public const string MedicalChairPrefabPath = "Assets/PolygonCyberCity/Prefabs/Props/SM_Prop_Medical_Chair_01.prefab";
        public const string ConsolePrefabPath = "Assets/PolygonCyberCity/Prefabs/Props/SM_Prop_Console_01.prefab";

        public const string AlitaDollPrefabPath = "Assets/Prefabs/Alita_Sidekick/Alita_02_Normal_DollBody.prefab";
        public const string DoctorCyberCityPrefabPath = "Assets/PolygonCyberCity/Prefabs/Characters/SM_Chr_Rich_Male_01.prefab";
        public const string DoctorHairPrefabPath = "Assets/PolygonCyberCity/Prefabs/Characters/SM_Chr_Attach_Hair_03.prefab";
        public const string DoctorHairMatPath = "Assets/Materials/M_Ido_Hair_SilverWhite.mat";
        public const string DoctorPrefabPath = "Assets/Prefabs/Doctor.prefab";
        public const string DoctorFallbackPath = "Assets/Characters/Doctor/Doctor.prefab";
        public const string HairPrefabPath = "Assets/PolygonCyberCity/Prefabs/Characters/SM_Chr_Attach_Hair_03.prefab";

        public const string LaserGridMatPath = "Assets/PolygonCyberCity/Materials/Misc/Laser_Grid.mat";
        public const string DarkFloorMatPath = "Assets/PolygonCyberCity/Materials/Building/Floor_01.mat";
        public const string WallMatPath = "Assets/PolygonCyberCity/Materials/Building/Wall_01.mat";

        [MenuItem("Tools/Alita Battle Angel/Generate Clinic Awakening Cutscene (Tron Style)", false, 20)]
        public static void GenerateAwakeningScene()
        {
            Debug.Log("[AlitaAwakening] Starting Tron-style Clinic Awakening Cutscene Generation...");

            Directory.CreateDirectory("Assets/Scenes");

            // 1. Create clean empty scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 2. Build Chamber Architecture
            GameObject chamberRoot = BuildTronChamber();

            // 3. Build Hibernation Pod & Platform
            Transform podCanopy;
            GameObject podGo = SpawnHibernationPod(out podCanopy);

            // 4. Spawn Alita in Doll Body inside the pod
            Light coreLight, leftEyeLight, rightEyeLight;
            GameObject alitaGo = SpawnAlitaInPod(podGo.transform, out coreLight, out leftEyeLight, out rightEyeLight);

            // 5. Spawn Dr. Dyson Ido at diagnostic console
            GameObject doctorGo = SpawnDoctor();

            // 6. Spawn High-Tech Medical & Holographic Hardware
            SpawnMedicalHardware(chamberRoot.transform);

            // 7. Lighting & Post-Processing
            Light overheadSurgLight;
            BuildLightingAndPostProcessing(out overheadSurgLight);

            // 8. Visual Effects (Cryo Steam & Laser Scanner)
            ParticleSystem cryoSteamFX;
            LineRenderer scanLaserLine;
            BuildVFX(podGo.transform, out cryoSteamFX, out scanLaserLine);

            // 9. Main Camera
            GameObject camGo = BuildCinematicCamera();

            // 10. Master Controller Setup
            GameObject controllerGo = new GameObject("Alita_Awakening_Cutscene_Controller");
            var controller = controllerGo.AddComponent<AlitaAwakeningCutsceneController>();

            controller.alitaDollBody = alitaGo;
            controller.alitaAnimator = alitaGo.GetComponent<Animator>();
            if (controller.alitaAnimator != null) controller.alitaAnimator.enabled = false;

            controller.doctorActor = doctorGo;
            controller.doctorAnimator = doctorGo.GetComponentInChildren<Animator>();

            controller.podBase = podGo.transform;
            controller.podCanopy = podCanopy;

            controller.overheadSurgicalLight = overheadSurgLight;
            controller.quantumCoreLight = coreLight;
            controller.leftEyeOpticLight = leftEyeLight;
            controller.rightEyeOpticLight = rightEyeLight;

            controller.cryoSteamFX = cryoSteamFX;
            controller.scanLaserLine = scanLaserLine;
            controller.cutsceneCamera = camGo.GetComponent<Camera>();

            // Assign Laser Grid Material for pulsing
            var gridMat = AssetDatabase.LoadAssetAtPath<Material>(LaserGridMatPath);
            if (gridMat != null) controller.tronGridMaterial = gridMat;

            // 11. Save Scene and Add to Build Settings
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);

            Debug.Log($"[AlitaAwakening] SUCCESS: Clinic Awakening Cutscene generated and saved to {ScenePath}!");
        }

        private static GameObject BuildTronChamber()
        {
            GameObject root = new GameObject("Environment_Tron_Clinic");

            // Reflective dark floor
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor_Reflective";
            floor.transform.SetParent(root.transform);
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(2.0f, 1f, 2.0f); // 20m x 20m

            Material floorMat = AssetDatabase.LoadAssetAtPath<Material>(DarkFloorMatPath);
            if (floorMat != null) floor.GetComponent<MeshRenderer>().sharedMaterial = floorMat;

            // Tron Glowing Grid Overlay directly on floor
            GameObject gridQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            gridQuad.name = "Tron_Floor_Laser_Grid";
            gridQuad.transform.SetParent(root.transform);
            gridQuad.transform.position = new Vector3(0f, 0.015f, 0f);
            gridQuad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            gridQuad.transform.localScale = new Vector3(16f, 16f, 1f);

            Material gridMat = AssetDatabase.LoadAssetAtPath<Material>(LaserGridMatPath);
            if (gridMat != null)
            {
                gridQuad.GetComponent<MeshRenderer>().sharedMaterial = gridMat;
            }

            // Central Elevated Circular Pedestal (Dais)
            GameObject dais = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            dais.name = "Pedestal_Hibernation_Dais";
            dais.transform.SetParent(root.transform);
            dais.transform.position = new Vector3(0f, 0.08f, 0f);
            dais.transform.localScale = new Vector3(4.2f, 0.08f, 4.2f);
            if (floorMat != null) dais.GetComponent<MeshRenderer>().sharedMaterial = floorMat;
            UnityEngine.Object.DestroyImmediate(dais.GetComponent<Collider>());

            // Cyan emissive neon trim ring around pedestal
            GameObject ringGlow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ringGlow.name = "Pedestal_Neon_Trim";
            ringGlow.transform.SetParent(dais.transform);
            ringGlow.transform.localPosition = new Vector3(0f, -0.05f, 0f);
            ringGlow.transform.localScale = new Vector3(1.04f, 0.04f, 1.04f);
            UnityEngine.Object.DestroyImmediate(ringGlow.GetComponent<Collider>());

            Material cyanNeon = CreateEmissiveNeonMaterial("Mat_Tron_Cyan_Neon", new Color(0f, 0.95f, 1f), 3.5f);
            ringGlow.GetComponent<MeshRenderer>().sharedMaterial = cyanNeon;

            // Sleek walls enclosing the clinic
            Material wallMat = AssetDatabase.LoadAssetAtPath<Material>(WallMatPath) ?? floorMat;
            BuildWall(root.transform, "Wall_North", new Vector3(0f, 3.5f, 9.5f), new Vector3(19f, 7f, 0.5f), wallMat);
            BuildWall(root.transform, "Wall_South", new Vector3(0f, 3.5f, -9.5f), new Vector3(19f, 7f, 0.5f), wallMat);
            BuildWall(root.transform, "Wall_East", new Vector3(9.5f, 3.5f, 0f), new Vector3(0.5f, 7f, 19f), wallMat);
            BuildWall(root.transform, "Wall_West", new Vector3(-9.5f, 3.5f, 0f), new Vector3(0.5f, 7f, 19f), wallMat);

            // Ceiling with dark finish
            GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ceiling.name = "Ceiling";
            ceiling.transform.SetParent(root.transform);
            ceiling.transform.position = new Vector3(0f, 7.0f, 0f);
            ceiling.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            ceiling.transform.localScale = new Vector3(19f, 19f, 1f);
            if (wallMat != null) ceiling.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

            // Recessed Tron Neon Light Strips along walls
            BuildNeonStrip(root.transform, new Vector3(-9.35f, 3.5f, -4f), new Vector3(0.08f, 6.0f, 0.08f), cyanNeon);
            BuildNeonStrip(root.transform, new Vector3(-9.35f, 3.5f, 4f), new Vector3(0.08f, 6.0f, 0.08f), cyanNeon);
            BuildNeonStrip(root.transform, new Vector3(9.35f, 3.5f, -4f), new Vector3(0.08f, 6.0f, 0.08f), cyanNeon);
            BuildNeonStrip(root.transform, new Vector3(9.35f, 3.5f, 4f), new Vector3(0.08f, 6.0f, 0.08f), cyanNeon);

            return root;
        }

        private static void BuildWall(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(parent);
            wall.transform.position = pos;
            wall.transform.localScale = scale;
            if (mat != null) wall.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        private static void BuildNeonStrip(Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            strip.name = "Neon_Strip";
            strip.transform.SetParent(parent);
            strip.transform.position = pos;
            strip.transform.localScale = scale;
            strip.GetComponent<MeshRenderer>().sharedMaterial = mat;

            // Accent point light
            GameObject plGo = new GameObject("Light_Neon_Accent");
            plGo.transform.SetParent(strip.transform);
            plGo.transform.localPosition = Vector3.zero;
            Light pl = plGo.AddComponent<Light>();
            pl.type = LightType.Point;
            pl.color = new Color(0f, 0.85f, 1f);
            pl.range = 5.5f;
            pl.intensity = 1.2f;
        }

        private static GameObject SpawnHibernationPod(out Transform podCanopy)
        {
            var podPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PodPrefabPath);
            GameObject podGo;
            if (podPrefab != null)
            {
                podGo = (GameObject)PrefabUtility.InstantiatePrefab(podPrefab);
            }
            else
            {
                podGo = new GameObject("SM_Prop_Hibernation_Pod_01");
            }

            podGo.name = "Hibernation_Pod";
            podGo.transform.position = new Vector3(0f, 0.16f, 0f);
            podGo.transform.rotation = Quaternion.identity;

            podCanopy = podGo.transform.Find("SM_Prop_Hibernation_Pod_Top_01");
            if (podCanopy == null && podGo.transform.childCount > 0)
            {
                podCanopy = podGo.transform.GetChild(0);
            }

            return podGo;
        }

        private static GameObject SpawnAlitaInPod(Transform podT, out Light coreLight, out Light leftEyeLight, out Light rightEyeLight)
        {
            var alitaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AlitaDollPrefabPath);
            GameObject alitaGo;
            if (alitaPrefab != null)
            {
                alitaGo = (GameObject)PrefabUtility.InstantiatePrefab(alitaPrefab);
            }
            else
            {
                alitaGo = new GameObject("Alita_DollBody_Fallback");
            }

            alitaGo.name = "Alita_02_Normal_DollBody";
            alitaGo.transform.position = new Vector3(-0.02f, 1.55f, 0.15f);
            alitaGo.transform.rotation = Quaternion.Euler(-90f, 180f, 0f);

            // Assign pure movie-authentic white porcelain doll body material to robotic chassis parts
            var porcelainMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_Alita_White_Porcelain.mat");
            var faceMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Synty/SidekickCharacters/Characters/HumanSpecies/HumanSpecies_02/Materials/HumanSpecies_02.mat");
            
            var allSmrs = alitaGo.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var smr in allSmrs)
            {
                smr.updateWhenOffscreen = true;
                if (smr.name.Contains("ROBO") || smr.name.Contains("TORS") || smr.name.Contains("AUPL") || smr.name.Contains("AUPR") 
                    || smr.name.Contains("ALWL") || smr.name.Contains("ALWR") || smr.name.Contains("HND") || smr.name.Contains("HIPS") 
                    || smr.name.Contains("LEG") || smr.name.Contains("FOT") || smr.name == "mesh")
                {
                    if (porcelainMat != null) smr.sharedMaterial = porcelainMat;
                }
                else
                {
                    if (faceMat != null) smr.sharedMaterial = faceMat;
                }
            }

            // Attach hair if not already present in sidekick hierarchy
            AttachAlitaHair(alitaGo);

            // Quantum Core Point Light in chest
            GameObject coreGo = new GameObject("Light_Quantum_Core");
            coreGo.transform.SetParent(alitaGo.transform);
            coreGo.transform.localPosition = new Vector3(0f, 1.22f, -0.06f);
            coreLight = coreGo.AddComponent<Light>();
            coreLight.type = LightType.Point;
            coreLight.color = new Color(0f, 0.95f, 1.0f);
            coreLight.range = 2.2f;
            coreLight.intensity = 0.6f;
            coreLight.shadows = LightShadows.None;

            // Left Eye Optic Light
            GameObject eyeLGo = new GameObject("Light_Optic_Eye_L");
            eyeLGo.transform.SetParent(alitaGo.transform);
            eyeLGo.transform.localPosition = new Vector3(-0.045f, 1.58f, -0.10f);
            leftEyeLight = eyeLGo.AddComponent<Light>();
            leftEyeLight.type = LightType.Point;
            leftEyeLight.color = new Color(0.1f, 1.0f, 1.0f);
            leftEyeLight.range = 0.5f;
            leftEyeLight.intensity = 0f; // Ignites at optic boot

            // Right Eye Optic Light
            GameObject eyeRGo = new GameObject("Light_Optic_Eye_R");
            eyeRGo.transform.SetParent(alitaGo.transform);
            eyeRGo.transform.localPosition = new Vector3(0.045f, 1.58f, -0.10f);
            rightEyeLight = eyeRGo.AddComponent<Light>();
            rightEyeLight.type = LightType.Point;
            rightEyeLight.color = new Color(0.1f, 1.0f, 1.0f);
            rightEyeLight.range = 0.5f;
            rightEyeLight.intensity = 0f;

            return alitaGo;
        }

        private static void AttachAlitaHair(GameObject alita)
        {
            var anim = alita.GetComponent<Animator>();
            Transform headT = anim != null ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
            if (headT == null)
            {
                var h = alita.transform.Find("head") ?? alita.transform.Find("Head");
                if (h != null) headT = h;
            }

            // If sidekick head already has hair skinned mesh, skip attaching external hair
            if (alita.transform.Find("SK_HUMN_BASE_09_02HAIR_HU01") != null) return;

            if (headT != null && headT.Find("Alita_Hair") == null)
            {
                var hairPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HairPrefabPath)
                    ?? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonCyberCity/Prefabs/Characters/SM_Chr_Attach_Hair_01.prefab");

                if (hairPrefab != null)
                {
                    GameObject hair = (GameObject)PrefabUtility.InstantiatePrefab(hairPrefab, headT);
                    hair.name = "Alita_Hair";
                    hair.transform.localPosition = Vector3.zero;
                    hair.transform.localRotation = Quaternion.identity;
                }
            }
        }

        private static GameObject SpawnDoctor()
        {
            var docPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DoctorCyberCityPrefabPath)
                ?? AssetDatabase.LoadAssetAtPath<GameObject>(DoctorPrefabPath)
                ?? AssetDatabase.LoadAssetAtPath<GameObject>(DoctorFallbackPath);

            GameObject doctorActor = new GameObject("Doctor_Actor");
            doctorActor.transform.position = new Vector3(-2.2f, 0f, 1.8f);
            doctorActor.transform.rotation = Quaternion.Euler(0f, 135f, 0f);

            if (docPrefab != null)
            {
                GameObject docModel = (GameObject)PrefabUtility.InstantiatePrefab(docPrefab, doctorActor.transform);
                docModel.name = "Doctor_Model";
                docModel.transform.localPosition = Vector3.zero;
                docModel.transform.localRotation = Quaternion.identity;

                // Deactivate any unwanted meshes if composite, keep Rich_Male_01 visible
                var allSmrs = docModel.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                foreach (var s in allSmrs)
                {
                    if (s.name.Contains("Rich_Male_01") || s.name == "mesh")
                    {
                        s.gameObject.SetActive(true);
                    }
                    else if (s.name.Contains("SM_Chr_"))
                    {
                        s.gameObject.SetActive(false);
                    }
                }

                // Attach Doctor Ido's signature silver hair
                var anim = docModel.GetComponent<Animator>();
                Transform headBone = anim != null ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
                if (headBone == null) headBone = FindChildRecursive(docModel.transform, "Head");

                if (headBone != null && headBone.Find("Doctor_Hair") == null)
                {
                    var hairPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DoctorHairPrefabPath);
                    if (hairPrefab != null)
                    {
                        GameObject hair = (GameObject)PrefabUtility.InstantiatePrefab(hairPrefab, headBone);
                        hair.name = "Doctor_Hair";
                        hair.transform.localPosition = Vector3.zero;
                        hair.transform.localRotation = Quaternion.identity;

                        var silverMat = AssetDatabase.LoadAssetAtPath<Material>(DoctorHairMatPath);
                        if (silverMat != null)
                        {
                            var r = hair.GetComponent<Renderer>();
                            if (r != null) r.sharedMaterial = silverMat;
                        }
                    }
                }

                // Ensure Animator has AC_CyberNPC
                if (anim != null)
                {
                    var ac = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/PolygonCyberCity/Animations/AC_CyberNPC.controller");
                    if (ac != null) anim.runtimeAnimatorController = ac;
                    anim.applyRootMotion = false;
                }
            }

            // Key light on doctor
            GameObject docLight = new GameObject("Light_Doctor_Key");
            docLight.transform.SetParent(doctorActor.transform);
            docLight.transform.localPosition = new Vector3(0.3f, 2.2f, 0.8f);
            Light l = docLight.AddComponent<Light>();
            l.type = LightType.Spot;
            l.spotAngle = 65f;
            l.range = 6f;
            l.color = new Color(1.0f, 0.95f, 0.85f);
            l.intensity = 1.8f;

            return doctorActor;
        }

        private static void SpawnMedicalHardware(Transform parent)
        {
            GameObject hwRoot = new GameObject("Hardware_Medical_Consoles");
            hwRoot.transform.SetParent(parent);

            // 1. Pod Master Console
            GameObject podConsole = SpawnPropSafe(PodConsolePrefabPath, new Vector3(-1.6f, 0.16f, 1.2f), Quaternion.Euler(0f, 120f, 0f), hwRoot.transform);
            if (podConsole != null)
            {
                // High-tech 3D CLI terminal display box overlay on the monitor face
                GameObject screenBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
                screenBox.name = "Screen_Tron_CLI_Display";
                screenBox.transform.SetParent(podConsole.transform);
                screenBox.transform.localPosition = new Vector3(0f, 1.09f, -0.64f);
                screenBox.transform.localRotation = Quaternion.Euler(38.5f, 0f, 0f);
                screenBox.transform.localScale = new Vector3(0.92f, 1.20f, 0.02f);
                GameObject.DestroyImmediate(screenBox.GetComponent<Collider>());

                var screenMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                screenMat.color = new Color(0.01f, 0.05f, 0.08f);
                screenMat.EnableKeyword("_EMISSION");
                screenMat.SetColor("_EmissionColor", new Color(0f, 0.85f, 1.0f) * 1.5f);
                screenBox.GetComponent<MeshRenderer>().sharedMaterial = screenMat;
            }

            // 2. Hologram Table
            SpawnPropSafe(HologramTablePrefabPath, new Vector3(2.8f, 0f, 1.0f), Quaternion.Euler(0f, -45f, 0f), hwRoot.transform);

            // 3. Server Racks against back wall
            SpawnPropSafe(ServerPrefabPath, new Vector3(-4.5f, 0f, 8.8f), Quaternion.Euler(0f, 180f, 0f), hwRoot.transform);
            SpawnPropSafe(ServerPrefabPath, new Vector3(-2.8f, 0f, 8.8f), Quaternion.Euler(0f, 180f, 0f), hwRoot.transform);
            SpawnPropSafe(ServerPrefabPath, new Vector3(2.8f, 0f, 8.8f), Quaternion.Euler(0f, 180f, 0f), hwRoot.transform);
            SpawnPropSafe(ServerPrefabPath, new Vector3(4.5f, 0f, 8.8f), Quaternion.Euler(0f, 180f, 0f), hwRoot.transform);

            // 4. Diagnostic Chair
            SpawnPropSafe(MedicalChairPrefabPath, new Vector3(-5.2f, 0f, -2.5f), Quaternion.Euler(0f, 45f, 0f), hwRoot.transform);

            // 5. Secondary Terminal
            SpawnPropSafe(ConsolePrefabPath, new Vector3(-5.2f, 0f, -1.0f), Quaternion.Euler(0f, 90f, 0f), hwRoot.transform);
        }

        private static GameObject SpawnPropSafe(string path, Vector3 pos, Quaternion rot, Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                go.transform.position = pos;
                go.transform.rotation = rot;
                return go;
            }
            return null;
        }

        private static Transform FindChildRecursive(Transform parent, string childName)
        {
            if (parent == null) return null;
            if (parent.name.Equals(childName, StringComparison.OrdinalIgnoreCase) || parent.name.Contains(childName)) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindChildRecursive(parent.GetChild(i), childName);
                if (found != null) return found;
            }
            return null;
        }

        private static void BuildLightingAndPostProcessing(out Light overheadSurgicalLight)
        {
            GameObject lightRoot = new GameObject("Lighting_And_Atmosphere");

            // 1. Overhead Volumetric Surgical Light
            GameObject surgGo = new GameObject("Light_Overhead_Surgical_Spot");
            surgGo.transform.SetParent(lightRoot.transform);
            surgGo.transform.position = new Vector3(0f, 5.2f, 0.2f);
            surgGo.transform.rotation = Quaternion.Euler(88f, 0f, 0f);

            overheadSurgicalLight = surgGo.AddComponent<Light>();
            overheadSurgicalLight.type = LightType.Spot;
            overheadSurgicalLight.spotAngle = 60f;
            overheadSurgicalLight.innerSpotAngle = 35f;
            overheadSurgicalLight.range = 8.5f;
            overheadSurgicalLight.color = new Color(0.85f, 0.96f, 1.0f); // Clean icy surgical white
            overheadSurgicalLight.intensity = 3.8f;
            overheadSurgicalLight.shadows = LightShadows.Soft;

            // 2. Cyan Ambient Rim Fill Light
            GameObject ambGo = new GameObject("Light_Chamber_Cyan_Ambient");
            ambGo.transform.SetParent(lightRoot.transform);
            ambGo.transform.position = new Vector3(0f, 3.5f, -3.5f);
            Light amb = ambGo.AddComponent<Light>();
            amb.type = LightType.Point;
            amb.range = 16f;
            amb.color = new Color(0f, 0.65f, 1.0f);
            amb.intensity = 1.1f;
            amb.shadows = LightShadows.None;

            // 3. Post-Processing Volume
            GameObject volGo = new GameObject("Global_PostProcessing_Volume");
            volGo.transform.SetParent(lightRoot.transform);
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Tron_Clinic_Awakening_Profile";

            // Bloom
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.value = 1.6f;
            bloom.threshold.value = 0.85f;
            bloom.tint.value = new Color(0.75f, 0.95f, 1.0f);

            // Tonemapping
            var tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.value = TonemappingMode.ACES;

            // Vignette
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.value = 0.35f;
            vignette.smoothness.value = 0.45f;

            // Color Adjustments
            var colorAdj = profile.Add<ColorAdjustments>(true);
            colorAdj.contrast.value = 16f;
            colorAdj.postExposure.value = 0.2f;

            vol.sharedProfile = profile;
            AssetDatabase.CreateAsset(profile, "Assets/Settings/Tron_Clinic_Awakening_Profile.asset");
        }

        private static void BuildVFX(Transform podT, out ParticleSystem cryoSteamFX, out LineRenderer scanLaserLine)
        {
            GameObject vfxRoot = new GameObject("VFX_Chamber_Effects");
            vfxRoot.transform.SetParent(podT);

            // 1. Cryo Steam FX
            GameObject steamGo = new GameObject("FX_Cryo_Steam");
            steamGo.transform.SetParent(vfxRoot.transform);
            steamGo.transform.localPosition = new Vector3(0f, 0.95f, 0f);

            cryoSteamFX = steamGo.AddComponent<ParticleSystem>();
            var psMain = cryoSteamFX.main;
            psMain.playOnAwake = false;
            psMain.loop = false;
            psMain.duration = 3.5f;
            psMain.startLifetime = 2.4f;
            psMain.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.6f);
            psMain.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.95f);
            psMain.startColor = new Color(0.85f, 0.96f, 1.0f, 0.45f);
            psMain.simulationSpace = ParticleSystemSimulationSpace.World;

            var psEmission = cryoSteamFX.emission;
            psEmission.rateOverTime = 80f;

            var psShape = cryoSteamFX.shape;
            psShape.shapeType = ParticleSystemShapeType.Box;
            psShape.scale = new Vector3(1.2f, 0.1f, 2.5f);

            // 2. Scan Laser Line
            GameObject laserGo = new GameObject("FX_Scan_Laser_Line");
            laserGo.transform.SetParent(vfxRoot.transform);
            scanLaserLine = laserGo.AddComponent<LineRenderer>();
            scanLaserLine.positionCount = 2;
            scanLaserLine.startWidth = 0.025f;
            scanLaserLine.endWidth = 0.025f;
            scanLaserLine.useWorldSpace = true;

            Material laserMat = CreateEmissiveNeonMaterial("Mat_Scan_Laser_RedNeon", new Color(0f, 0.95f, 1f), 4.5f);
            scanLaserLine.sharedMaterial = laserMat;
            scanLaserLine.startColor = new Color(0f, 0.95f, 1f, 0.9f);
            scanLaserLine.endColor = new Color(0f, 0.95f, 1f, 0.9f);
        }

        private static GameObject BuildCinematicCamera()
        {
            GameObject camGo = new GameObject("MainCamera");
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(-2.6f, 2.2f, -1.4f);
            Vector3 target = new Vector3(0f, 1.45f, 0.35f);
            camGo.transform.rotation = Quaternion.LookRotation((target - camGo.transform.position).normalized);

            Camera cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 34f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 100f;

            var uac = camGo.AddComponent<UniversalAdditionalCameraData>();
            uac.renderPostProcessing = true;
            uac.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            camGo.AddComponent<AudioListener>();

            return camGo;
        }

        private static Material CreateEmissiveNeonMaterial(string matName, Color emissionColor, float intensity)
        {
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) litShader = Shader.Find("Standard");

            Material mat = new Material(litShader);
            mat.name = matName;
            mat.SetColor("_BaseColor", Color.black);
            mat.SetColor("_EmissionColor", emissionColor * intensity);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            string dir = "Assets/Materials/Tron";
            Directory.CreateDirectory(dir);
            string assetPath = $"{dir}/{matName}.mat";
            AssetDatabase.CreateAsset(mat, assetPath);
            return mat;
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == scenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(scenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }
    }
}
#endif
