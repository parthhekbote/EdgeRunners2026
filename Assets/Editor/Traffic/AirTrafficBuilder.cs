#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using CyberpunkDev.Traffic;

namespace CyberpunkTrafficEditor
{
    public static class AirTrafficBuilder
    {
        private const string HoverPrefabsDir = "Assets/Prefabs/Vehicles_Hover";
        private const string ResourcesHoverDir = "Assets/Resources/Vehicles_Hover";
        private const string MaterialsDir = "Assets/PolygonCyberCity/Materials/FX_Mats";
        private const string SettingsPath = "Assets/Scripts/Traffic/TrafficSettings.asset";

        [MenuItem("Tools/Cyberpunk/Build Air Traffic System", false, 100)]
        public static void BuildSystem()
        {
            EnsureDirectories();
            Material neonMatCyan = CreateOrGetNeonMaterial("M_Traffic_NeonCyan", new Color(0.05f, 0.95f, 1.0f));
            Material neonMatBlue = CreateOrGetNeonMaterial("M_Traffic_NeonBlue", new Color(0.15f, 0.65f, 1.0f));
            Material neonMatAmber = CreateOrGetNeonMaterial("M_Traffic_NeonAmber", new Color(1.0f, 0.75f, 0.05f));
            Material neonMatYellow = CreateOrGetNeonMaterial("M_Traffic_NeonYellow", new Color(1.0f, 0.92f, 0.1f));
            Material neonMatRed = CreateOrGetNeonMaterial("M_Traffic_NeonRed", new Color(1.0f, 0.08f, 0.15f));

            Material trailMatCyan = CreateOrGetTrailMaterial("M_Traffic_TrailCyan", new Color(0.1f, 0.85f, 1.0f));
            Material trailMatBlue = CreateOrGetTrailMaterial("M_Traffic_TrailBlue", new Color(0.2f, 0.65f, 1.0f));
            Material trailMatAmber = CreateOrGetTrailMaterial("M_Traffic_TrailAmber", new Color(1.0f, 0.7f, 0.1f));
            Material trailMatYellow = CreateOrGetTrailMaterial("M_Traffic_TrailYellow", new Color(1.0f, 0.9f, 0.2f));
            Material neonMatOrange = CreateOrGetNeonMaterial("M_Traffic_NeonOrange", new Color(1.0f, 0.45f, 0.05f));
            Material trailMatOrange = CreateOrGetTrailMaterial("M_Traffic_TrailOrange", new Color(1.0f, 0.45f, 0.05f));
            Material neonMatGreen = CreateOrGetNeonMaterial("M_Traffic_NeonGreen", new Color(0.15f, 1.0f, 0.45f));
            Material trailMatGreen = CreateOrGetTrailMaterial("M_Traffic_TrailGreen", new Color(0.15f, 1.0f, 0.45f));

            TrafficSettings settings = CreateOrGetTrafficSettings();

            GameObject luxuryCar = BuildHoverLuxuryCar(neonMatCyan, trailMatCyan);
            GameObject suvCar = BuildHoverSUV(neonMatBlue, trailMatBlue);
            GameObject truckCar = BuildHoverTruck(neonMatAmber, trailMatAmber);
            GameObject foodVan = BuildHoverFoodVan(neonMatOrange, trailMatOrange);
            GameObject policeCar = BuildHoverPoliceEnforcer(neonMatRed, trailMatBlue);
            GameObject taxiCar = BuildHoverCyberTaxi(neonMatYellow, trailMatYellow);
            GameObject compactCart = BuildHoverCompactCart(neonMatGreen, trailMatGreen);

            GameObject[] allPrefabs = new GameObject[] { luxuryCar, suvCar, truckCar, foodVan, policeCar, taxiCar, compactCart };

            // Copy/save to Resources folder as well for instant runtime availability
            CopyPrefabsToResources(allPrefabs);

            SetupSceneSystem(settings, allPrefabs);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=#00FF99>[AirTrafficBuilder] Successfully built and deployed 7-Vehicle Air Traffic System across Entire City!</color>");
        }

        private static void EnsureDirectories()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder(HoverPrefabsDir))
                AssetDatabase.CreateFolder("Assets/Prefabs", "Vehicles_Hover");
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(ResourcesHoverDir))
                AssetDatabase.CreateFolder("Assets/Resources", "Vehicles_Hover");
            if (!AssetDatabase.IsValidFolder(MaterialsDir))
                AssetDatabase.CreateFolder("Assets/PolygonCyberCity/Materials", "FX_Mats");
        }

        public static Material CreateOrGetNeonMaterial(string matName, Color neonColor)
        {
            string path = $"{MaterialsDir}/{matName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
                mat = new Material(shader);
                mat.name = matName;

                mat.SetColor("_BaseColor", neonColor);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", neonColor * 4.2f);
                mat.enableInstancing = true;

                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.enableInstancing = true;
            }
            return mat;
        }

        public static Material CreateOrGetTrailMaterial(string matName, Color trailColor)
        {
            string path = $"{MaterialsDir}/{matName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
                mat = new Material(shader);
                mat.name = matName;

                mat.SetFloat("_Surface", 1f); // Transparent
                mat.SetFloat("_Blend", 1f);   // Additive
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

                mat.SetColor("_BaseColor", trailColor);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", trailColor * 3.5f);
                mat.enableInstancing = true;

                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.enableInstancing = true;
            }
            return mat;
        }

        public static TrafficSettings CreateOrGetTrafficSettings()
        {
            TrafficSettings settings = AssetDatabase.LoadAssetAtPath<TrafficSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<TrafficSettings>();
                settings.targetCarCount = 15;
                settings.lowFpsCarCount = 8;
                settings.lowFpsThreshold = 45.0f;
                settings.lowFpsEvaluationDuration = 3.0f;
                settings.globalAltitudeOffset = 0.0f;
                settings.minCarSpeed = 8.0f;
                settings.maxCarSpeed = 16.0f;
                settings.globalSpeedMultiplier = 1.0f;
                settings.verticalBobAmplitude = 0.30f;
                settings.verticalBobFrequency = 1.0f;
                settings.enableOrganicWander = true;
                settings.wanderAmplitude = 1.0f;
                settings.wanderFrequency = 0.25f;
                settings.maxBankAngle = 22.0f;
                settings.distanceCullThreshold = 200.0f;
                settings.trailCullDistance = 75.0f;
                settings.enableCulling = true;
                settings.enableAmbientDrone = false;
                settings.maxPooledAudioSources = 4;
                settings.masterTrafficVolume = 0.55f;
                settings.engineHumVolume = 0.35f;
                settings.passByWhooshVolume = 0.50f;
                settings.ambientCityTrafficVolume = 0.08f;
                settings.audioMinDistance = 3.0f;
                settings.audioMaxDistance = 22.0f;
                settings.passByTriggerDistance = 12.0f;
                settings.audioReassignInterval = 0.35f;

                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            else
            {
                settings.globalAltitudeOffset = 0.0f;
                settings.enableAmbientDrone = false;
                settings.audioMinDistance = 3.0f;
                settings.audioMaxDistance = 22.0f;
                settings.passByTriggerDistance = 12.0f;
                EditorUtility.SetDirty(settings);
            }
            return settings;
        }

        public static GameObject BuildHoverLuxuryCar(Material neonMat, Material trailMat)
        {
            string srcPath = "Assets/PolygonCyberCity/Prefabs/Vehicles/SM_Veh_Luxury_Car_01.prefab";
            string dstPath = $"{HoverPrefabsDir}/Hover_Luxury_Car.prefab";
            GameObject src = AssetDatabase.LoadAssetAtPath<GameObject>(srcPath);

            GameObject root = Object.Instantiate(src);
            root.name = "Hover_Luxury_Car";

            StripPhysicsAndColliders(root);

            AddUnderglowQuad(root.transform, neonMat, new Vector3(0, -0.18f, 0), new Vector2(1.7f, 3.4f));
            AddTrail(root.transform, trailMat, new Vector3(-0.65f, 0.15f, -2.15f), new Color(0f, 0.95f, 1f));
            AddTrail(root.transform, trailMat, new Vector3(0.65f, 0.15f, -2.15f), new Color(0f, 0.95f, 1f));

            HoverCar hoverCar = root.GetComponent<HoverCar>();
            if (hoverCar == null) hoverCar = root.AddComponent<HoverCar>();
            hoverCar.CacheComponents();

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, dstPath);
            Object.DestroyImmediate(root);
            return savedPrefab;
        }

        public static GameObject BuildHoverSUV(Material neonMat, Material trailMat)
        {
            string srcPath = "Assets/PolygonCyberCity/Prefabs/Vehicles/SM_Veh_SUV_01.prefab";
            string dstPath = $"{HoverPrefabsDir}/Hover_SUV.prefab";
            GameObject src = AssetDatabase.LoadAssetAtPath<GameObject>(srcPath);

            GameObject root = Object.Instantiate(src);
            root.name = "Hover_SUV";

            StripPhysicsAndColliders(root);

            AddUnderglowQuad(root.transform, neonMat, new Vector3(0, -0.18f, 0), new Vector2(1.9f, 3.8f));
            AddTrail(root.transform, trailMat, new Vector3(-0.75f, 0.2f, -2.35f), new Color(0.15f, 0.65f, 1f));
            AddTrail(root.transform, trailMat, new Vector3(0.75f, 0.2f, -2.35f), new Color(0.15f, 0.65f, 1f));

            HoverCar hoverCar = root.GetComponent<HoverCar>();
            if (hoverCar == null) hoverCar = root.AddComponent<HoverCar>();
            hoverCar.CacheComponents();

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, dstPath);
            Object.DestroyImmediate(root);
            return savedPrefab;
        }

        public static GameObject BuildHoverTruck(Material neonMat, Material trailMat)
        {
            string srcPath = "Assets/PolygonCyberCity/Prefabs/Vehicles/SM_Veh_Truck_01.prefab";
            string dstPath = $"{HoverPrefabsDir}/Hover_Truck.prefab";
            GameObject src = AssetDatabase.LoadAssetAtPath<GameObject>(srcPath);
            GameObject padPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonCyberCity/Prefabs/Vehicles/SM_Veh_Truck_01_Hover_Pads_01.prefab");

            GameObject root = Object.Instantiate(src);
            root.name = "Hover_Truck";

            StripPhysicsAndColliders(root);

            for (int i = 0; i < root.transform.childCount; i++)
            {
                var child = root.transform.GetChild(i);
                if (child.name.ToLower().Contains("wheel"))
                {
                    child.gameObject.SetActive(false);
                }
            }

            if (padPrefab != null)
            {
                AttachHoverPad(padPrefab, root.transform, Vector3.zero, Quaternion.identity, Vector3.one);
            }

            AddUnderglowQuad(root.transform, neonMat, new Vector3(0, -0.2f, 0), new Vector2(2.2f, 4.4f));
            AddTrail(root.transform, trailMat, new Vector3(-0.9f, 0.35f, -2.85f), new Color(1.0f, 0.75f, 0.1f));
            AddTrail(root.transform, trailMat, new Vector3(0.9f, 0.35f, -2.85f), new Color(1.0f, 0.75f, 0.1f));

            HoverCar hoverCar = root.GetComponent<HoverCar>();
            if (hoverCar == null) hoverCar = root.AddComponent<HoverCar>();
            hoverCar.CacheComponents();

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, dstPath);
            Object.DestroyImmediate(root);
            return savedPrefab;
        }

        public static GameObject BuildHoverPoliceEnforcer(Material neonMat, Material trailMat)
        {
            string srcPath = "Assets/PolygonCyberCity/Prefabs/Vehicles/SM_Veh_Luxury_Car_01.prefab";
            string dstPath = $"{HoverPrefabsDir}/Hover_Police_Enforcer.prefab";
            GameObject src = AssetDatabase.LoadAssetAtPath<GameObject>(srcPath);

            GameObject root = Object.Instantiate(src);
            root.name = "Hover_Police_Enforcer";

            StripPhysicsAndColliders(root);

            // Activate police lights and bullbar
            for (int i = 0; i < root.transform.childCount; i++)
            {
                var child = root.transform.GetChild(i);
                if (child.name.Contains("PoliceLights") || child.name.Contains("Bullbar") || child.name.Contains("Antenna"))
                {
                    child.gameObject.SetActive(true);
                }
                if (child.name.Contains("TaxiSign") || child.name.Contains("FastFood"))
                {
                    child.gameObject.SetActive(false);
                }
            }

            AddUnderglowQuad(root.transform, neonMat, new Vector3(0, -0.18f, 0), new Vector2(1.7f, 3.4f));
            AddTrail(root.transform, trailMat, new Vector3(-0.65f, 0.15f, -2.15f), new Color(1.0f, 0.1f, 0.2f));
            AddTrail(root.transform, trailMat, new Vector3(0.65f, 0.15f, -2.15f), new Color(0.1f, 0.4f, 1.0f));

            HoverCar hoverCar = root.GetComponent<HoverCar>();
            if (hoverCar == null) hoverCar = root.AddComponent<HoverCar>();
            hoverCar.CacheComponents();

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, dstPath);
            Object.DestroyImmediate(root);
            return savedPrefab;
        }

        public static GameObject BuildHoverCyberTaxi(Material neonMat, Material trailMat)
        {
            string srcPath = "Assets/PolygonCyberCity/Prefabs/Vehicles/SM_Veh_Luxury_Car_01.prefab";
            string dstPath = $"{HoverPrefabsDir}/Hover_Cyber_Taxi.prefab";
            GameObject src = AssetDatabase.LoadAssetAtPath<GameObject>(srcPath);

            GameObject root = Object.Instantiate(src);
            root.name = "Hover_Cyber_Taxi";

            StripPhysicsAndColliders(root);

            // Activate taxi sign
            for (int i = 0; i < root.transform.childCount; i++)
            {
                var child = root.transform.GetChild(i);
                if (child.name.Contains("TaxiSign"))
                {
                    child.gameObject.SetActive(true);
                }
                if (child.name.Contains("PoliceLights") || child.name.Contains("FastFood"))
                {
                    child.gameObject.SetActive(false);
                }
            }

            AddUnderglowQuad(root.transform, neonMat, new Vector3(0, -0.18f, 0), new Vector2(1.7f, 3.4f));
            AddTrail(root.transform, trailMat, new Vector3(-0.65f, 0.15f, -2.15f), new Color(1.0f, 0.85f, 0.1f));
            AddTrail(root.transform, trailMat, new Vector3(0.65f, 0.15f, -2.15f), new Color(1.0f, 0.85f, 0.1f));

            HoverCar hoverCar = root.GetComponent<HoverCar>();
            if (hoverCar == null) hoverCar = root.AddComponent<HoverCar>();
            hoverCar.CacheComponents();

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, dstPath);
            Object.DestroyImmediate(root);
            return savedPrefab;
        }

        public static GameObject BuildHoverFoodVan(Material neonMat, Material trailMat)
        {
            string srcPath = "Assets/PolygonCyberCity/Prefabs/Vehicles/SM_Veh_Food_Truck_01.prefab";
            string dstPath = $"{HoverPrefabsDir}/Hover_Food_Van.prefab";
            GameObject src = AssetDatabase.LoadAssetAtPath<GameObject>(srcPath);

            GameObject root = Object.Instantiate(src);
            root.name = "Hover_Food_Van";

            StripPhysicsAndColliders(root);

            AddUnderglowQuad(root.transform, neonMat, new Vector3(0, -0.25f, 0), new Vector2(2.3f, 4.8f));
            AddTrail(root.transform, trailMat, new Vector3(-0.95f, 0.35f, -2.85f), new Color(1.0f, 0.35f, 0.05f));
            AddTrail(root.transform, trailMat, new Vector3(0.95f, 0.35f, -2.85f), new Color(1.0f, 0.35f, 0.05f));

            HoverCar hoverCar = root.GetComponent<HoverCar>();
            if (hoverCar == null) hoverCar = root.AddComponent<HoverCar>();
            hoverCar.CacheComponents();

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, dstPath);
            Object.DestroyImmediate(root);
            return savedPrefab;
        }

        public static GameObject BuildHoverCompactCart(Material neonMat, Material trailMat)
        {
            string srcPath = "Assets/PolygonCyberCity/Prefabs/Vehicles/SM_Veh_Cart_01.prefab";
            string dstPath = $"{HoverPrefabsDir}/Hover_Compact_Cart.prefab";
            GameObject src = AssetDatabase.LoadAssetAtPath<GameObject>(srcPath);

            GameObject root = Object.Instantiate(src);
            root.name = "Hover_Compact_Cart";

            StripPhysicsAndColliders(root);

            AddUnderglowQuad(root.transform, neonMat, new Vector3(0, -0.15f, 0), new Vector2(1.2f, 2.0f));
            AddTrail(root.transform, trailMat, new Vector3(-0.45f, 0.2f, -1.15f), new Color(0.2f, 1.0f, 0.5f));
            AddTrail(root.transform, trailMat, new Vector3(0.45f, 0.2f, -1.15f), new Color(0.2f, 1.0f, 0.5f));

            HoverCar hoverCar = root.GetComponent<HoverCar>();
            if (hoverCar == null) hoverCar = root.AddComponent<HoverCar>();
            hoverCar.CacheComponents();

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, dstPath);
            Object.DestroyImmediate(root);
            return savedPrefab;
        }

        private static void CopyPrefabsToResources(GameObject[] prefabs)
        {
            for (int i = 0; i < prefabs.Length; i++)
            {
                if (prefabs[i] == null) continue;
                string srcPath = AssetDatabase.GetAssetPath(prefabs[i]);
                string dstPath = $"{ResourcesHoverDir}/{prefabs[i].name}.prefab";
                AssetDatabase.CopyAsset(srcPath, dstPath);
            }
        }

        private static void StripPhysicsAndColliders(GameObject go)
        {
            var rbs = go.GetComponentsInChildren<Rigidbody>(true);
            for (int i = rbs.Length - 1; i >= 0; i--) Object.DestroyImmediate(rbs[i]);

            var colliders = go.GetComponentsInChildren<Collider>(true);
            for (int i = colliders.Length - 1; i >= 0; i--) Object.DestroyImmediate(colliders[i]);
        }

        private static void AttachHoverPad(GameObject padPrefab, Transform parent, Vector3 localPos, Quaternion localRot, Vector3 localScale)
        {
            GameObject pad = Object.Instantiate(padPrefab, parent);
            pad.name = "Hover_Pad";
            pad.transform.localPosition = localPos;
            pad.transform.localRotation = localRot;
            pad.transform.localScale = localScale;
            StripPhysicsAndColliders(pad);
        }

        private static void AddUnderglowQuad(Transform parent, Material mat, Vector3 localPos, Vector2 size)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Neon_Underglow";
            quad.transform.SetParent(parent);
            quad.transform.localPosition = localPos;
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);

            var col = quad.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            var rend = quad.GetComponent<MeshRenderer>();
            if (rend != null)
            {
                rend.sharedMaterial = mat;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rend.receiveShadows = false;
            }
        }

        private static void AddTrail(Transform parent, Material mat, Vector3 localPos, Color color)
        {
            GameObject trailGO = new GameObject("Engine_Trail");
            trailGO.transform.SetParent(parent);
            trailGO.transform.localPosition = localPos;
            trailGO.transform.localRotation = Quaternion.identity;

            TrailRenderer tr = trailGO.AddComponent<TrailRenderer>();
            tr.sharedMaterial = mat;
            tr.time = 0.65f;
            tr.minVertexDistance = 0.4f;

            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0f, 0.35f);
            curve.AddKey(1f, 0.02f);
            tr.widthCurve = curve;

            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(color, 0.0f),
                    new GradientColorKey(new Color(0.05f, 0.2f, 0.8f), 0.7f),
                    new GradientColorKey(new Color(0.01f, 0.05f, 0.3f), 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.85f, 0.0f),
                    new GradientAlphaKey(0.45f, 0.5f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            tr.colorGradient = grad;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.receiveShadows = false;
        }

        public static void SetupSceneSystem(TrafficSettings settings, GameObject[] carPrefabs)
        {
            GameObject systemGO = GameObject.Find("AirTrafficSystem");
            if (systemGO == null)
            {
                systemGO = new GameObject("AirTrafficSystem");
            }

            // 8 Full-City Linear Lanes covering Low Canyon, Mid Transit, Elevated Skyways, and High Skyline Corridors
            AirLane laneCanyonN = CreateOrUpdateLane(systemGO.transform, "Lane_Straight_Canyon_North", -13.5f, false, true, new Color(0.05f, 0.95f, 1.0f), GetCanyonNorthWaypoints());
            AirLane laneCanyonS = CreateOrUpdateLane(systemGO.transform, "Lane_Straight_Canyon_South", -13.5f, false, true, new Color(1.0f, 0.75f, 0.05f), GetCanyonSouthWaypoints());
            AirLane laneMidN = CreateOrUpdateLane(systemGO.transform, "Lane_Straight_Mid_North", 8.0f, false, true, new Color(0.15f, 0.65f, 1.0f), GetMidNorthWaypoints());
            AirLane laneMidS = CreateOrUpdateLane(systemGO.transform, "Lane_Straight_Mid_South", 8.0f, false, true, new Color(1.0f, 0.15f, 0.85f), GetMidSouthWaypoints());
            AirLane laneSkywayN = CreateOrUpdateLane(systemGO.transform, "Lane_Straight_Skyway_North", 20.0f, false, true, new Color(0.95f, 0.45f, 0.1f), GetSkywayNorthWaypoints());
            AirLane laneSkywayS = CreateOrUpdateLane(systemGO.transform, "Lane_Straight_Skyway_South", 20.0f, false, true, new Color(0.65f, 0.15f, 1.0f), GetSkywaySouthWaypoints());
            AirLane laneSkylineExpress = CreateOrUpdateLane(systemGO.transform, "Lane_High_Skyline_Express", 22.0f, false, true, new Color(1.0f, 0.95f, 0.2f), GetSkylineExpressWaypoints());
            AirLane laneSkylineReturn = CreateOrUpdateLane(systemGO.transform, "Lane_High_Skyline_Return", 32.0f, false, true, new Color(0.2f, 1.0f, 0.4f), GetSkylineReturnWaypoints());

            // Clean up any legacy loop lanes or colliding objects
            string[] legacyLanes = new string[] {
                "Lane_Low", "Lane_Mid", "Lane_High", "Lane_Loop_Grand_Metropolis",
                "Lane_Canyon_North", "Lane_Canyon_South", "Lane_Cross_Skyway",
                "Lane_Downtown_Loop", "Lane_High_Skyway", "Lane_Loop_Grand_Boulevard",
                "Lane_Straight_North_Boulevard", "Lane_Straight_South_Boulevard",
                "Lane_Straight_East_Skyway", "Lane_Straight_West_Skyway"
            };
            for (int i = 0; i < legacyLanes.Length; i++)
            {
                Transform oldLane = systemGO.transform.Find(legacyLanes[i]);
                if (oldLane != null) Object.DestroyImmediate(oldLane.gameObject);
            }

            FlyingCarAudioManager audioMgr = systemGO.GetComponent<FlyingCarAudioManager>();
            if (audioMgr == null) audioMgr = systemGO.AddComponent<FlyingCarAudioManager>();

            FlyingCarManager manager = systemGO.GetComponent<FlyingCarManager>();
            if (manager == null) manager = systemGO.AddComponent<FlyingCarManager>();

            SerializedObject so = new SerializedObject(manager);
            so.FindProperty("settings").objectReferenceValue = settings;
            so.FindProperty("audioManager").objectReferenceValue = audioMgr;

            SerializedProperty lanesProp = so.FindProperty("lanes");
            lanesProp.arraySize = 8;
            lanesProp.GetArrayElementAtIndex(0).objectReferenceValue = laneCanyonN;
            lanesProp.GetArrayElementAtIndex(1).objectReferenceValue = laneCanyonS;
            lanesProp.GetArrayElementAtIndex(2).objectReferenceValue = laneMidN;
            lanesProp.GetArrayElementAtIndex(3).objectReferenceValue = laneMidS;
            lanesProp.GetArrayElementAtIndex(4).objectReferenceValue = laneSkywayN;
            lanesProp.GetArrayElementAtIndex(5).objectReferenceValue = laneSkywayS;
            lanesProp.GetArrayElementAtIndex(6).objectReferenceValue = laneSkylineExpress;
            lanesProp.GetArrayElementAtIndex(7).objectReferenceValue = laneSkylineReturn;

            SerializedProperty prefabsProp = so.FindProperty("carPrefabs");
            prefabsProp.arraySize = carPrefabs.Length;
            for (int i = 0; i < carPrefabs.Length; i++)
            {
                prefabsProp.GetArrayElementAtIndex(i).objectReferenceValue = carPrefabs[i];
            }
            so.ApplyModifiedProperties();

            SerializedObject soAudio = new SerializedObject(audioMgr);
            soAudio.FindProperty("settings").objectReferenceValue = settings;
            soAudio.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }

        private static AirLane CreateOrUpdateLane(Transform parent, string laneName, float altitude, bool isLoop, bool isClockwise, Color color, Vector3[] waypointCoords)
        {
            Transform laneT = parent.Find(laneName);
            GameObject laneGO = laneT != null ? laneT.gameObject : new GameObject(laneName);
            laneGO.transform.SetParent(parent);
            laneGO.transform.localPosition = Vector3.zero;

            AirLane lane = laneGO.GetComponent<AirLane>();
            if (lane == null) lane = laneGO.AddComponent<AirLane>();

            lane.laneName = laneName;
            lane.altitude = altitude;
            lane.altitudeOffset = 0f;
            lane.isLoop = isLoop;
            lane.isClockwise = isClockwise;
            lane.pathColor = color;
            lane.waypoints = new List<Transform>();

            for (int i = laneGO.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(laneGO.transform.GetChild(i).gameObject);
            }

            for (int i = 0; i < waypointCoords.Length; i++)
            {
                GameObject wp = new GameObject($"WP_{i + 1:D2}");
                wp.transform.SetParent(laneGO.transform);
                wp.transform.position = waypointCoords[i];
                lane.waypoints.Add(wp.transform);
            }

            lane.RebuildSpline();
            return lane;
        }

        // 1. Lower Street Canyon Northbound (Y = -13.5m, X = -2.0m, Z in [-250m, 250m])
        private static Vector3[] GetCanyonNorthWaypoints()
        {
            return new Vector3[] {
                new Vector3(-2.0f, -13.5f, -250.0f),
                new Vector3(-2.0f, -13.5f, -125.0f),
                new Vector3(-2.0f, -13.5f, 0.0f),
                new Vector3(-2.0f, -13.5f, 125.0f),
                new Vector3(-2.0f, -13.5f, 250.0f)
            };
        }

        // 2. Lower Street Canyon Southbound (Y = -13.5m, X = +2.5m, Z in [250m, -250m])
        private static Vector3[] GetCanyonSouthWaypoints()
        {
            return new Vector3[] {
                new Vector3(2.5f, -13.5f, 250.0f),
                new Vector3(2.5f, -13.5f, 125.0f),
                new Vector3(2.5f, -13.5f, 0.0f),
                new Vector3(2.5f, -13.5f, -125.0f),
                new Vector3(2.5f, -13.5f, -250.0f)
            };
        }

        // 3. Mid Canyon Transit Northbound (Y = 8.0m, X = -2.5m, Z in [-220m, 220m])
        private static Vector3[] GetMidNorthWaypoints()
        {
            return new Vector3[] {
                new Vector3(-2.5f, 8.0f, -220.0f),
                new Vector3(-2.5f, 8.0f, -110.0f),
                new Vector3(-2.5f, 8.0f, 0.0f),
                new Vector3(-2.5f, 8.0f, 110.0f),
                new Vector3(-2.5f, 8.0f, 220.0f)
            };
        }

        // 4. Mid Canyon Transit Southbound (Y = 8.0m, X = +2.5m, Z in [220m, -220m])
        private static Vector3[] GetMidSouthWaypoints()
        {
            return new Vector3[] {
                new Vector3(2.5f, 8.0f, 220.0f),
                new Vector3(2.5f, 8.0f, 110.0f),
                new Vector3(2.5f, 8.0f, 0.0f),
                new Vector3(2.5f, 8.0f, -110.0f),
                new Vector3(2.5f, 8.0f, -220.0f)
            };
        }

        // 5. Elevated Skyway Northbound (Y = 20.0m, X = -2.0m, Z in [-220m, 220m])
        private static Vector3[] GetSkywayNorthWaypoints()
        {
            return new Vector3[] {
                new Vector3(-2.0f, 20.0f, -220.0f),
                new Vector3(-2.0f, 20.0f, -110.0f),
                new Vector3(-2.0f, 20.0f, 0.0f),
                new Vector3(-2.0f, 20.0f, 110.0f),
                new Vector3(-2.0f, 20.0f, 220.0f)
            };
        }

        // 6. Elevated Skyway Southbound (Y = 20.0f, X = +2.5f, Z in [220m, -220m])
        private static Vector3[] GetSkywaySouthWaypoints()
        {
            return new Vector3[] {
                new Vector3(2.5f, 20.0f, 220.0f),
                new Vector3(2.5f, 20.0f, 110.0f),
                new Vector3(2.5f, 20.0f, 0.0f),
                new Vector3(2.5f, 20.0f, -110.0f),
                new Vector3(2.5f, 20.0f, -220.0f)
            };
        }

        // 7. High Skyline Express: Trans-city diagonal express (Y = 22.0m, from (-10, 22, -220) to (10, 22, 220))
        private static Vector3[] GetSkylineExpressWaypoints()
        {
            return new Vector3[] {
                new Vector3(-10.0f, 22.0f, -220.0f),
                new Vector3(-5.0f, 22.0f, -110.0f),
                new Vector3(0.0f, 22.0f, 0.0f),
                new Vector3(5.0f, 22.0f, 110.0f),
                new Vector3(10.0f, 22.0f, 220.0f)
            };
        }

        // 8. High Skyline Return: High rooftop trans-city cruiser (Y = 32.0m, X = 0.0m, Z in [250m, -250m])
        private static Vector3[] GetSkylineReturnWaypoints()
        {
            return new Vector3[] {
                new Vector3(0.0f, 32.0f, 250.0f),
                new Vector3(0.0f, 32.0f, 125.0f),
                new Vector3(0.0f, 32.0f, 0.0f),
                new Vector3(0.0f, 32.0f, -125.0f),
                new Vector3(0.0f, 32.0f, -250.0f)
            };
        }
    }
}
#endif
