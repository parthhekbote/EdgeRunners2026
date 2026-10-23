#if UNITY_EDITOR
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using PolygonMech;

namespace PolygonMech.Editor
{
    public static class MechShowcaseSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Mech_Animations_Showcase.unity";

        // User's exact configured sword transform values — MUST BE PRESERVED ACCORDING TO USER INSTRUCTION
        public static readonly Vector3 UserSwordLocalPos = new Vector3(-0.2180f, 0.2370f, 0.0770f);
        public static readonly Vector3 UserSwordLocalRot = new Vector3(286.13f, 352.86f, 352.36f);
        public static readonly Vector3 UserSwordLocalScale = Vector3.one;

        private const string SwordBonePath = "Root/Hips/Spine_01/Spine_02/Spine_03/Clavicle_R/Shoulder_R/Elbow_R/Hand_R/Prop_R/Prop_R_Socket";

        [MenuItem("Polygon Mech/3. Build Mech Animations Showcase Scene", false, 3)]
        public static void BuildShowcaseScene()
        {
            Debug.Log("<color=#00FFFF>[MechShowcase] Building Grand Exhibition Scene with All 32 Mech Animations...</color>");

            // 1. Create or Open Scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 2. Setup Lighting & Atmosphere
            SetupLightingAndPostProcessing();

            // 3. Setup Colosseum Arena Environment
            GameObject colosseum = BuildColosseumEnvironment();

            // 4. Setup Central Hero Stage Turntable
            GameObject heroMech = BuildHeroTurntable();

            // 5. Setup the 4 Terraced Tiers & 32 Mech Exhibits
            List<MechShowcaseExhibit> exhibits = Build32ExhibitGallery();

            // 6. Setup Director & Main Camera
            SetupDirectorAndCamera(heroMech, exhibits);

            // 7. Save Scene
            string sceneDir = Path.GetDirectoryName(ScenePath);
            if (!Directory.Exists(sceneDir)) Directory.CreateDirectory(sceneDir);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"<color=#00FF66>[MechShowcase] Grand Exhibition Scene successfully created and saved at: {ScenePath}</color>");
        }

        private static void SetupLightingAndPostProcessing()
        {
            // Key Directional Light (Deep Space Cyber Moon)
            GameObject sunObj = new GameObject("Directional_CyberKeyLight");
            Light sun = sunObj.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(0.85f, 0.92f, 1.0f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            sun.shadowNormalBias = 0.4f;
            sunObj.transform.rotation = Quaternion.Euler(46f, -32f, 0f);

            var uld = sunObj.AddComponent<UniversalAdditionalLightData>();
            uld.usePipelineSettings = true;

            // Fill Light
            GameObject fillObj = new GameObject("Directional_CyanFillLight");
            Light fill = fillObj.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.15f, 0.55f, 0.85f);
            fill.intensity = 0.45f;
            fill.shadows = LightShadows.None;
            fillObj.transform.rotation = Quaternion.Euler(30f, 140f, 0f);

            // Ambient settings
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.08f, 0.12f, 0.18f);
            RenderSettings.ambientEquatorColor = new Color(0.04f, 0.06f, 0.10f);
            RenderSettings.ambientGroundColor = new Color(0.02f, 0.03f, 0.05f);

            // Global Volume (Bloom, ACES Tonemapping, Chromatic Aberration)
            GameObject volObj = new GameObject("GlobalVolume_PostProcessing");
            Volume volume = volObj.AddComponent<Volume>();
            volume.isGlobal = true;

            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Showcase_URP_Profile";

            // Bloom
            Bloom bloom = profile.Add<Bloom>(true);
            bloom.threshold.value = 0.90f;
            bloom.intensity.value = 1.95f;
            bloom.scatter.value = 0.70f;
            bloom.tint.value = new Color(0.9f, 0.95f, 1.0f);

            // Tonemapping (ACES)
            Tonemapping tone = profile.Add<Tonemapping>(true);
            tone.mode.value = TonemappingMode.ACES;

            // Chromatic Aberration
            ChromaticAberration chroma = profile.Add<ChromaticAberration>(true);
            chroma.intensity.value = 0.18f;

            // Vignette
            Vignette vig = profile.Add<Vignette>(true);
            vig.intensity.value = 0.28f;
            vig.smoothness.value = 0.45f;

            volume.profile = profile;

            string profDir = "Assets/PolygonMech/Materials";
            if (Directory.Exists(profDir))
            {
                AssetDatabase.CreateAsset(profile, Path.Combine(profDir, "Showcase_VolumeProfile.asset"));
            }
        }

        private static GameObject BuildColosseumEnvironment()
        {
            GameObject root = new GameObject("Colosseum_Exhibition_Arena");

            Material floorMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/PolygonCyberCity/Materials/Building/Floor_02.mat")
                             ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/PolygonCyberCity/Materials/Building/Floor_01.mat");
            if (floorMat == null)
            {
                Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                floorMat = new Material(urpLit) { name = "M_CyberArena_Floor" };
                floorMat.SetColor("_BaseColor", new Color(0.08f, 0.10f, 0.14f));
                floorMat.SetFloat("_Smoothness", 0.65f);
                floorMat.SetFloat("_Metallic", 0.70f);
            }

            // 1. Massive Arena Floor Slab (140m x 140m)
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Colosseum_Floor_Slab";
            floor.transform.parent = root.transform;
            floor.transform.position = new Vector3(0f, -0.5f, 10f);
            floor.transform.localScale = new Vector3(140f, 1f, 140f);
            floor.GetComponent<Renderer>().sharedMaterial = floorMat;

            // 2. Cyber Perimeter Boundary Wall Ring (Curved Stadium Backdrop)
            Material wallMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/PolygonCyberCity/Materials/Building/Wall_01_Triplanar.mat")
                            ?? floorMat;

            int wallSegments = 24;
            float wallRadius = 60f;
            for (int i = 0; i < wallSegments; i++)
            {
                float angle = Mathf.Lerp(-100f, 100f, (float)i / (wallSegments - 1)) * Mathf.Deg2Rad;
                Vector3 pos = new Vector3(Mathf.Sin(angle) * wallRadius, 6f, Mathf.Cos(angle) * wallRadius + 6f);
                Quaternion rot = Quaternion.LookRotation(new Vector3(0f, 0f, 10f) - pos);

                GameObject wallPillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wallPillar.name = $"Perimeter_Pylon_{i:D2}";
                wallPillar.transform.parent = root.transform;
                wallPillar.transform.position = pos;
                wallPillar.transform.rotation = rot;
                wallPillar.transform.localScale = new Vector3(6.5f, 14f, 2.5f);
                wallPillar.GetComponent<Renderer>().sharedMaterial = wallMat;

                // Emissive neon accent bar on wall
                GameObject neonBar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                neonBar.name = "Neon_Stripe";
                neonBar.transform.parent = wallPillar.transform;
                neonBar.transform.localPosition = new Vector3(0f, 0f, -0.55f);
                neonBar.transform.localRotation = Quaternion.identity;
                neonBar.transform.localScale = new Vector3(0.9f, 0.08f, 0.1f);

                var mr = neonBar.GetComponent<Renderer>();
                mr.sharedMaterial = GetOrCreateEmissiveMaterial(new Color(0.0f, 1.8f, 2.5f), "M_WallNeon_Cyan");
            }

            return root;
        }

        private static GameObject BuildHeroTurntable()
        {
            GameObject turntableRoot = new GameObject("Hero_Center_Turntable");
            turntableRoot.transform.position = new Vector3(0f, 0f, -6f);

            // Elevated circular platform
            GameObject baseCylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            baseCylinder.name = "Turntable_Platform";
            baseCylinder.transform.parent = turntableRoot.transform;
            baseCylinder.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            baseCylinder.transform.localScale = new Vector3(8.5f, 0.30f, 8.5f);

            Material baseMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/PolygonCyberCity/Materials/Building/Floor_02.mat")
                            ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/PolygonCyberCity/Materials/Building/Floor_01.mat");
            if (baseMat != null) baseCylinder.GetComponent<Renderer>().sharedMaterial = baseMat;

            // Outer Glowing Tron Rim
            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Turntable_NeonRim";
            ring.transform.parent = turntableRoot.transform;
            ring.transform.localPosition = new Vector3(0f, 0.32f, 0f);
            ring.transform.localScale = new Vector3(8.7f, 0.05f, 8.7f);
            ring.GetComponent<Renderer>().sharedMaterial = GetOrCreateEmissiveMaterial(new Color(4.8f, 1.5f, 0.2f), "M_Neon_CyberGold");

            // Overhead Hero Spotlight
            GameObject spotGO = new GameObject("Hero_Key_Spotlight");
            spotGO.transform.parent = turntableRoot.transform;
            spotGO.transform.localPosition = new Vector3(0f, 11f, 0f);
            spotGO.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            Light spot = spotGO.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.range = 18f;
            spot.spotAngle = 55f;
            spot.color = new Color(1.0f, 0.95f, 0.85f);
            spot.intensity = 4.8f;
            spot.shadows = LightShadows.Soft;

            var uld = spotGO.AddComponent<UniversalAdditionalLightData>();
            uld.usePipelineSettings = true;

            // Instantiate Hero Goliath Mech
            string prefabPath = "Assets/PolygonMech/Prefabs/Vehicles/PF_BossMech_AI.prefab";
            GameObject mechPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (mechPrefab == null)
            {
                Debug.LogError("[MechShowcase] PF_BossMech_AI.prefab not found!");
                return null;
            }

            GameObject hero = (GameObject)PrefabUtility.InstantiatePrefab(mechPrefab);
            hero.name = "Hero_GoliathMech";
            hero.transform.parent = turntableRoot.transform;
            hero.transform.localPosition = new Vector3(0f, 0.30f, 0f);
            hero.transform.localRotation = Quaternion.identity;

            // Disable AI / character controller so it acts cleanly as the interactive exhibition hero
            var ai = hero.GetComponent<BossMechAI>();
            if (ai != null) ai.enabled = false;
            var cc = hero.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            var bar = hero.GetComponent<BossHealthBar>();
            if (bar != null) bar.enabled = false;

            // Configure blade VFX
            var bladeVFX = hero.GetComponent<MechBladeCombatVFX>();
            if (bladeVFX == null) bladeVFX = hero.AddComponent<MechBladeCombatVFX>();

            // Strictly preserve user configured sword transform!
            PreserveSwordTransform(hero);

            // Pre-sample hero idle pose in Editor
            AnimationClip heroIdle = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/PolygonMech/Animations/GreatSwordPack/great sword idle (3).fbx");
            if (heroIdle != null)
            {
                AnimationMode.StartAnimationMode();
                AnimationMode.SampleAnimationClip(hero, heroIdle, heroIdle.length * 0.35f);
                AnimationMode.StopAnimationMode();
            }

            return hero;
        }

        private static List<MechShowcaseExhibit> Build32ExhibitGallery()
        {
            var exhibits = new List<MechShowcaseExhibit>();

            string prefabPath = "Assets/PolygonMech/Prefabs/Vehicles/PF_BossMech_AI.prefab";
            GameObject mechPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (mechPrefab == null) return exhibits;

            GameObject galleryRoot = new GameObject("Mech_32_Gallery_Colosseum");

            // Curated 32 animations from inspector
            var animDefs = MechBossAnimationMatrixInspector.Curated32Animations;

            // 4 Semicircular Tiers (8 mechs per tier)
            // Tier 1: Heavy Slashes & Execution Strikes (Weapons Core)
            // Tier 2: Mechanical Brawling, Stomps & Aerial Strikes
            // Tier 3: Tactical Locomotion, Sprints & Flank Evasion
            // Tier 4: Core Stances, Defensive Parries, Roar & Core Death
            float[] tierRadii = new float[] { 17f, 27f, 37f, 47f };
            float[] tierElevations = new float[] { 0.20f, 1.80f, 3.40f, 5.00f };
            float[] tierAngles = new float[] { 54f, 60f, 66f, 72f };
            float[] tierAngleOffsets = new float[] { 0f, 3.8f, 0f, 3.8f }; // Staggered lanes

            // Material for pedestals
            Material pedMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/PolygonCyberCity/Materials/Building/Floor_02.mat")
                           ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/PolygonCyberCity/Materials/Building/Floor_01.mat");

            for (int i = 0; i < animDefs.Length; i++)
            {
                var def = animDefs[i];
                int tierIndex = i / 8;
                int posInTier = i % 8;

                float r = tierRadii[tierIndex];
                float elev = tierElevations[tierIndex];
                float maxA = tierAngles[tierIndex];
                float offsetA = tierAngleOffsets[tierIndex];

                // Spread evenly from -maxA to +maxA with staggered lane offset
                float t = (float)posInTier / 7f;
                float angleDeg = Mathf.Lerp(-maxA, maxA, t) + offsetA;
                float angleRad = angleDeg * Mathf.Deg2Rad;

                Vector3 pedestalPos = new Vector3(Mathf.Sin(angleRad) * r, elev, Mathf.Cos(angleRad) * r + 2f);
                Quaternion lookRot = Quaternion.LookRotation(new Vector3(0f, 0.5f, -6f) - pedestalPos);

                // 1. Build Pedestal
                GameObject pedestalGO = new GameObject($"Pedestal_{def.id}");
                pedestalGO.transform.parent = galleryRoot.transform;
                pedestalGO.transform.position = pedestalPos;
                pedestalGO.transform.rotation = lookRot;

                // Base Cylinder
                GameObject cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cyl.name = "Pedestal_Base";
                cyl.transform.parent = pedestalGO.transform;
                cyl.transform.localPosition = new Vector3(0f, -elev * 0.5f, 0f);
                cyl.transform.localRotation = Quaternion.identity;
                cyl.transform.localScale = new Vector3(4.5f, elev * 0.5f + 0.15f, 4.5f);
                if (pedMat != null) cyl.GetComponent<Renderer>().sharedMaterial = pedMat;

                // Neon Ring on Pedestal Top
                Color catColor = GetCategoryColor(def.category);
                GameObject neonRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                neonRing.name = "Neon_Border_Ring";
                neonRing.transform.parent = pedestalGO.transform;
                neonRing.transform.localPosition = new Vector3(0f, 0.16f, 0f);
                neonRing.transform.localRotation = Quaternion.identity;
                neonRing.transform.localScale = new Vector3(4.65f, 0.04f, 4.65f);

                Material neonMat = GetOrCreateEmissiveMaterial(catColor, $"M_Neon_{def.category}");
                var ringRenderer = neonRing.GetComponent<Renderer>();
                ringRenderer.sharedMaterial = neonMat;

                // Overhead Spotlight for this Exhibit
                GameObject spotGO = new GameObject("Exhibit_Spotlight");
                spotGO.transform.parent = pedestalGO.transform;
                spotGO.transform.localPosition = new Vector3(0f, 8.5f, 0f);
                spotGO.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

                Light spot = spotGO.AddComponent<Light>();
                spot.type = LightType.Spot;
                spot.range = 14f;
                spot.spotAngle = 50f;
                spot.color = catColor;
                spot.intensity = 2.6f;
                spot.shadows = LightShadows.Soft;

                var uld = spotGO.AddComponent<UniversalAdditionalLightData>();
                uld.usePipelineSettings = true;

                // 2. Instantiate Mech
                GameObject mech = (GameObject)PrefabUtility.InstantiatePrefab(mechPrefab);
                mech.name = $"Mech_{def.id}";
                mech.transform.parent = pedestalGO.transform;
                mech.transform.localPosition = new Vector3(0f, 0.18f, 0f);
                mech.transform.localRotation = Quaternion.identity;

                // Remove unwanted AI combat loops on static gallery mechs
                var ai = mech.GetComponent<BossMechAI>();
                if (ai != null) Object.DestroyImmediate(ai);
                var cc = mech.GetComponent<CharacterController>();
                if (cc != null) Object.DestroyImmediate(cc);
                var bar = mech.GetComponent<BossHealthBar>();
                if (bar != null) Object.DestroyImmediate(bar);

                // STRICTLY PRESERVE USER CONFIGURED SWORD TRANSFORM!
                PreserveSwordTransform(mech);

                // 3. Attach & Configure MechShowcaseExhibit
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(def.relPath);
                var exhibit = mech.AddComponent<MechShowcaseExhibit>();
                exhibit.animationIndex = i + 1;
                exhibit.animationId = def.id;
                exhibit.category = def.category;
                exhibit.displayName = def.displayName;
                exhibit.clip = clip;
                exhibit.baseDuration = clip != null ? clip.length : 1.0f;
                exhibit.themeColor = catColor;
                exhibit.spotlight = spot;
                exhibit.pedestalRingRenderer = ringRenderer;

                // Pre-sample action pose in Editor mode so mechs are not in T-pose
                if (clip != null)
                {
                    AnimationMode.StartAnimationMode();
                    AnimationMode.SampleAnimationClip(mech, clip, clip.length * 0.40f);
                    AnimationMode.StopAnimationMode();
                }

                // 4. Floating Holographic Billboard Label
                BuildHologramPlate(pedestalGO.transform, exhibit, catColor);

                exhibits.Add(exhibit);
            }

            return exhibits;
        }

        private static void BuildHologramPlate(Transform parent, MechShowcaseExhibit exhibit, Color color)
        {
            GameObject plate = new GameObject("Holo_TitlePlate");
            plate.transform.parent = parent;
            plate.transform.localPosition = new Vector3(0f, 0.40f, -2.4f);
            plate.transform.localRotation = Quaternion.Euler(-18f, 0f, 0f);

            // Backing plate
            GameObject back = GameObject.CreatePrimitive(PrimitiveType.Cube);
            back.name = "Plate_Backing";
            back.transform.parent = plate.transform;
            back.transform.localPosition = Vector3.zero;
            back.transform.localRotation = Quaternion.identity;
            back.transform.localScale = new Vector3(2.6f, 0.65f, 0.05f);

            Material plateMat = GetOrCreateEmissiveMaterial(new Color(0.04f, 0.06f, 0.08f), "M_HoloPlate_Dark");
            back.GetComponent<Renderer>().sharedMaterial = plateMat;

            // 3D Text Mesh (Native Unity component - zero package dependencies)
            GameObject textObj = new GameObject("Label_Text");
            textObj.transform.parent = plate.transform;
            textObj.transform.localPosition = new Vector3(0f, 0.05f, -0.04f);
            textObj.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // Text faces forward
            textObj.transform.localScale = new Vector3(0.08f, 0.08f, 0.08f);

            TextMesh tm = textObj.AddComponent<TextMesh>();
            tm.text = $"[{exhibit.animationIndex:D2}] {exhibit.displayName.ToUpper()}\n<color=#{ColorUtility.ToHtmlStringRGB(color)}>{exhibit.category.ToUpper()}</color> • {exhibit.baseDuration:F1}s";
            tm.fontSize = 28;
            tm.alignment = TextAlignment.Center;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = Color.white;
            tm.fontStyle = FontStyle.Bold;
        }

        private static void SetupDirectorAndCamera(GameObject heroMech, List<MechShowcaseExhibit> exhibits)
        {
            // Setup Camera
            GameObject camGO = new GameObject("Main_Showcase_Camera");
            camGO.transform.position = new Vector3(0f, 16f, -22f);
            camGO.transform.rotation = Quaternion.Euler(22f, 0f, 0f);

            Camera cam = camGO.AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.fieldOfView = 52f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 300f;
            cam.clearFlags = CameraClearFlags.Skybox;

            var uac = camGO.AddComponent<UniversalAdditionalCameraData>();
            uac.renderPostProcessing = true;

            camGO.AddComponent<AudioListener>();

            // Setup Director Manager
            GameObject mgrGO = new GameObject("_ShowcaseDirector");
            var mgr = mgrGO.AddComponent<MechShowcaseManager>();
            mgr.mainCamera = cam;
            mgr.heroMech = heroMech;
            mgr.exhibits = exhibits;
            mgr.currentCameraMode = MechShowcaseManager.CameraMode.ColosseumOverview;
        }

        /// <summary>
        /// Ensures the sword is attached with the EXACT user-configured transform.
        /// NEVER resets or overwrites the user's manual adjustments!
        /// </summary>
        private static void PreserveSwordTransform(GameObject mech)
        {
            Transform socket = mech.transform.Find(SwordBonePath);
            if (socket == null) return;

            Transform sword = socket.Find("SM_Wep_Sword_01");
            if (sword == null)
            {
                // Instantiate weapon if missing
                string swordPrefabPath = "Assets/PolygonMech/Prefabs/Weapons/SM_Wep_Sword_01.prefab";
                GameObject swordPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(swordPrefabPath);
                if (swordPrefab != null)
                {
                    sword = ((GameObject)PrefabUtility.InstantiatePrefab(swordPrefab, socket)).transform;
                    sword.name = "SM_Wep_Sword_01";
                }
            }

            if (sword != null)
            {
                // Apply the user's EXACT position and rotation
                sword.localPosition = UserSwordLocalPos;
                sword.localEulerAngles = UserSwordLocalRot;
                sword.localScale = UserSwordLocalScale;

                // Ensure SwordTip is present for trail and light
                Transform tip = sword.Find("SwordTip");
                if (tip == null)
                {
                    GameObject tipObj = new GameObject("SwordTip");
                    tipObj.transform.parent = sword;
                    tipObj.transform.localPosition = new Vector3(0f, 3.5f, 0f);
                    tipObj.transform.localRotation = Quaternion.identity;
                    tipObj.transform.localScale = Vector3.one;
                }
            }
        }

        private static Color GetCategoryColor(string cat)
        {
            switch (cat.ToLower())
            {
                case "slash":      return new Color(1.0f, 0.72f, 0.15f); // Cyber Gold
                case "attack":     return new Color(1.0f, 0.44f, 0.20f); // Fiery Amber
                case "locomotion": return new Color(0.0f, 0.90f, 1.0f);  // Electric Cyan
                case "turn":       return new Color(0.70f, 1.0f, 0.0f);  // Neon Lime
                case "defense":    return new Color(0.80f, 0.40f, 1.0f); // Royal Violet
                case "idle":       return new Color(0.30f, 0.65f, 1.0f); // Cobalt Blue
                case "power":      return new Color(1.0f, 0.10f, 0.35f); // Overdrive Crimson
                case "reaction":   return new Color(0.95f, 0.25f, 0.85f);// Magenta
                case "death":      return new Color(0.85f, 0.10f, 0.15f);// Deep Crimson
                default:           return Color.cyan;
            }
        }

        private static Material GetOrCreateEmissiveMaterial(Color color, string name)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material mat = new Material(shader) { name = name };
            mat.SetColor("_BaseColor", color);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * 2.2f);
            return mat;
        }
    }
}
#endif
