#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.AI;
using Unity.AI.Navigation;
using PolygonCyberCity.NPC.Spawner;

namespace PolygonCyberCity.NPC.Editor
{
    public static class Cyber100SceneBuilder
    {
        public const string ScenePath = "Assets/PolygonCyberCity/Scenes/Cybernetic_100_NPC_Showcase.unity";
        private const string PrefabsFolder = "Assets/PolygonCyberCity/Prefabs/Generated_100_CyberNPCs";

        [MenuItem("Tools/CyberCity/Create Dedicated 100 NPC Showcase Scene")]
        public static void BuildShowcaseScene()
        {
            // 1. Create a brand new clean scene
            var newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 2. Setup Lighting & Sky
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.06f, 0.08f, 0.12f); // Deep cyberpunk blue/navy
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.04f, 0.05f, 0.09f);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.006f;

            // Directional Moon / Cyber City Key Light
            GameObject lightGo = new GameObject("Directional_Cyber_Light");
            Light dirLight = lightGo.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.color = new Color(0.75f, 0.88f, 1.0f); // Cool cyber moonlight
            dirLight.intensity = 1.35f;
            dirLight.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(45f, -35f, 0f);

            // Neon Plaza Fill Light (warm magenta/amber rim)
            GameObject rimLightGo = new GameObject("Directional_Neon_RimLight");
            Light rimLight = rimLightGo.AddComponent<Light>();
            rimLight.type = LightType.Directional;
            rimLight.color = new Color(1.0f, 0.25f, 0.65f); // Neon magenta rim
            rimLight.intensity = 0.45f;
            rimLight.shadows = LightShadows.None;
            rimLightGo.transform.rotation = Quaternion.Euler(20f, 145f, 0f);

            // 3. Post Processing Volume
            GameObject volumeGo = new GameObject("PostProcessing_Volume");
            Volume volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            var volProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/PolygonCyberCity/CyberCity_VolumeProfile.asset");
            if (volProfile != null)
            {
                volume.profile = volProfile;
            }

            // 4. Ground Plaza Floor
            GameObject environmentRoot = new GameObject("Environment_Showcase_Plaza");

            // Large polished dark plaza base
            GameObject plazaFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plazaFloor.name = "Plaza_MainFloor";
            plazaFloor.transform.SetParent(environmentRoot.transform);
            plazaFloor.transform.position = new Vector3(0f, -0.5f, 0f);
            plazaFloor.transform.localScale = new Vector3(80f, 1f, 80f);

            Material floorMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/PolygonCyberCity/Materials/Floor_01.mat")
                             ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/PolygonCyberCity/Materials/Floor_02.mat");
            if (floorMat != null)
            {
                plazaFloor.GetComponent<MeshRenderer>().sharedMaterial = floorMat;
            }

            // Neon Perimeter Edges / Walkway Borders
            CreatePlazaBorders(environmentRoot.transform);

            // Background Cyber City Skyscrapers for epic skyline
            CreateSkylineBackdrop(environmentRoot.transform);

            // 5. Main Camera with Orbit / Spectator Controller
            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            Camera cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.2f;
            cam.farClipPlane = 300f;
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = new Vector3(0f, 6.5f, -24f);
            camGo.transform.rotation = Quaternion.Euler(14f, 0f, 0f);

            // Add simple mouse-look / WASD spectator script for easy browsing in Play Mode
            camGo.AddComponent<CyberSpectatorCamera>();

            // 6. Spawn all 100 Cybernetic NPCs in Formation
            GameObject npcRoot = new GameObject("Cybernetic_100_NPCs");
            Spawn100Formations(npcRoot.transform);

            // 7. Add NavMesh Surface & Bake
            var navSurface = environmentRoot.AddComponent<NavMeshSurface>();
            navSurface.collectObjects = CollectObjects.All;
            navSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            try
            {
                navSurface.BuildNavMesh();
                Debug.Log("[Cyber100Scene] NavMesh built successfully for 100 NPCs!");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Cyber100Scene] NavMesh build note: " + e.Message);
            }

            // 8. Save the Scene
            EditorSceneManager.SaveScene(newScene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"<color=#00F0FF><b>[Cyber100Scene]</b> Dedicated 100 NPC Showcase Scene successfully created and saved at: {ScenePath}!</color>");
        }

        private static void CreatePlazaBorders(Transform parent)
        {
            Material borderNeonMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/PolygonCyberCity/Materials/Laser.mat")
                                  ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/PolygonCyberCity/Materials/Alts/PolygonCyberCity_01_A.mat");

            // 4 Neon boundary light rails around the plaza
            Vector3[] borderPositions = new Vector3[]
            {
                new Vector3(0f, 0.15f, 38f),
                new Vector3(0f, 0.15f, -38f),
                new Vector3(38f, 0.15f, 0f),
                new Vector3(-38f, 0.15f, 0f)
            };
            Vector3[] borderScales = new Vector3[]
            {
                new Vector3(76f, 0.3f, 0.6f),
                new Vector3(76f, 0.3f, 0.6f),
                new Vector3(0.6f, 0.3f, 76f),
                new Vector3(0.6f, 0.3f, 76f)
            };

            for (int i = 0; i < 4; i++)
            {
                GameObject rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rail.name = "Plaza_NeonRail_" + i;
                rail.transform.SetParent(parent);
                rail.transform.position = borderPositions[i];
                rail.transform.localScale = borderScales[i];
                if (borderNeonMat != null) rail.GetComponent<MeshRenderer>().sharedMaterial = borderNeonMat;
            }
        }

        private static void CreateSkylineBackdrop(Transform parent)
        {
            string[] bldPrefabs = new string[]
            {
                "SM_Bld_Background_Building_01",
                "SM_Bld_Background_Building_02",
                "SM_Bld_Background_Building_03",
                "SM_Bld_Background_Building_04"
            };

            Vector3[] bldPositions = new Vector3[]
            {
                new Vector3(-45f, 0f, 50f),
                new Vector3(-18f, 0f, 55f),
                new Vector3(18f, 0f, 55f),
                new Vector3(45f, 0f, 50f),
                new Vector3(-55f, 0f, 0f),
                new Vector3(55f, 0f, 0f)
            };

            for (int i = 0; i < bldPositions.Length; i++)
            {
                string bldName = bldPrefabs[i % bldPrefabs.Length];
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/PolygonCyberCity/Prefabs/Buildings/{bldName}.prefab");
                if (prefab != null)
                {
                    GameObject bld = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                    bld.transform.position = bldPositions[i];
                    bld.transform.rotation = Quaternion.Euler(0f, Random.Range(0, 4) * 90f, 0f);
                    bld.transform.localScale = Vector3.one * 1.4f;
                }
            }
        }

        private static void Spawn100Formations(Transform root)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new string[] { PrefabsFolder });
            if (guids.Length == 0)
            {
                Debug.LogError("[Cyber100Scene] No prefabs found in " + PrefabsFolder);
                return;
            }

            // Arrange into 5 Distinct Faction Squads (20 NPCs per squad)
            // 10 columns x 10 rows formation with clean aisle walkways
            int cols = 10;
            float spacingX = 2.8f;
            float spacingZ = 3.2f;

            for (int i = 0; i < guids.Length && i < 100; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                int row = i / cols; // 0 to 9
                int col = i % cols; // 0 to 9

                // Center aisle gap between col 4 and 5, and row 4 and 5
                float xOffset = (col - (cols - 1) / 2f) * spacingX + (col >= 5 ? 1.5f : -1.5f);
                float zOffset = (row - (cols - 1) / 2f) * spacingZ + (row >= 5 ? 2.0f : -2.0f);

                Vector3 pos = new Vector3(xOffset, 0f, zOffset);

                GameObject npc = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                npc.transform.position = pos;
                // Face slightly towards center aisle / camera
                npc.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            }
        }
    }

    /// <summary>
    /// Smooth spectator fly/orbit camera for freely navigating and inspecting all 100 NPCs.
    /// WASD/Arrows to fly, Right-Click + Mouse to look around, Shift to boost speed.
    /// </summary>
    public class CyberSpectatorCamera : MonoBehaviour
    {
        public float moveSpeed = 12f;
        public float boostMultiplier = 2.5f;
        public float lookSensitivity = 2.5f;

        private float _yaw = 0f;
        private float _pitch = 14f;

        private void Start()
        {
            Vector3 angles = transform.eulerAngles;
            _yaw = angles.y;
            _pitch = angles.x;
        }

        private void Update()
        {
            if (Input.GetMouseButton(1))
            {
                _yaw += Input.GetAxis("Mouse X") * lookSensitivity * 40f * Time.deltaTime;
                _pitch -= Input.GetAxis("Mouse Y") * lookSensitivity * 40f * Time.deltaTime;
                _pitch = Mathf.Clamp(_pitch, -80f, 80f);
                transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            }

            float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? boostMultiplier : 1f);
            Vector3 dir = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) dir += transform.forward;
            if (Input.GetKey(KeyCode.S)) dir -= transform.forward;
            if (Input.GetKey(KeyCode.A)) dir -= transform.right;
            if (Input.GetKey(KeyCode.D)) dir += transform.right;
            if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Space)) dir += Vector3.up;
            if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.LeftControl)) dir -= Vector3.up;

            if (dir.sqrMagnitude > 0.01f)
            {
                transform.position += dir.normalized * speed * Time.deltaTime;
            }
        }
    }
}
#endif
