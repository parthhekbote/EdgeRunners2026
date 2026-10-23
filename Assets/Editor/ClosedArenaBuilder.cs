using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using SyntyCombined;
using InsaneGun.Combat;

namespace InsaneGun.Editor
{
    /// <summary>
    /// Cyberpunk 2077 "Blackwall" Cyberspace Arena Builder.
    /// Recreates the authentic Cyberspace aesthetic inspired by Salar de Uyuni and the Blackwall:
    /// - Salar de Uyuni Salt-Flat Mirror Ground: Endless reflective shallow water plane connecting ground & sky seamlessly.
    /// - Real-time Planar Reflections: Characters, weapon VFX, and the Blackwall reflect 1:1 in the mirror floor.
    /// - Towering Holographic Blackwall Monolith: 14m tall semi-transparent digital firewall with cascading red data streams,
    ///   translucent Fresnel rim glow, and erratic Rogue AI glitch tearing.
    /// - Footstep Ripple VFX: Concentric digital ripple rings expanding beneath combatants' feet.
    /// - Rogue AI Repulsion & Euphoria Ragdoll Physics: Violent physical rebound and digital shock on contact.
    /// </summary>
    public class ClosedArenaBuilder
    {
        // Asset Prefab Paths
        private const string ELECTRICITY_FX_PATH = "Assets/Synty/PolygonParticles/Prefabs/FX_Electricity_01.prefab";
        private const string SPARKS_FX_PATH      = "Assets/Synty/PolygonParticles/Prefabs/FX_Sparks_01.prefab";
        private const string WATER_RIPPLE_PATH   = "Assets/Synty/PolygonParticles/Prefabs/FX_WaterRipple_01.prefab";
        private const string BASE_EMITTER_PATH   = "Assets/PolygonCyberCity/Prefabs/Buildings/SM_Bld_Roof_Vent_01.prefab";

        private const string BLACKWALL_MAT_PATH   = "Assets/SyntyCombined/Materials/M_Blackwall_Holographic_Monolith.mat";
        private const string MIRROR_FLOOR_MAT_PATH = "Assets/SyntyCombined/Materials/M_Cyberspace_Mirror_Floor.mat";
        private const string GHOST_ATTACK_AUDIO_PATH = "Assets/Audio/Blackwall/cyberpunk-2077-blackwall-ghost-attack.mp3";
        private const string AMBIENT_MUSIC_PATH     = "Assets/Audio/Blackwall/51-beyond-the-blackwall.mp3";

        public const string ARENA_SCENE_PATH = "Assets/Scenes/InsaneGun_Combat_TestArena.unity";

        // Arena Dimensions (Authentic Towering Cyberpunk Monolith)
        private const float ARENA_HALF_SIZE = 22.0f;
        private const float WALL_HEIGHT    = 26.0f;

        [MenuItem("Tools/Cyberpunk Blackwall Arena/Build Rogue AI Blackwall Arena (Current Scene)", priority = 1)]
        public static void BuildBlackwallArena()
        {
            BuildBlackwallArenaInternal();
        }

        [MenuItem("Tools/Cyberpunk Blackwall Arena/Build in InsaneGun Combat Test Arena Scene", priority = 2)]
        public static void BuildInCombatTestArenaScene()
        {
            if (System.IO.File.Exists(ARENA_SCENE_PATH))
            {
                var scene = EditorSceneManager.OpenScene(ARENA_SCENE_PATH, OpenSceneMode.Single);
                BuildBlackwallArenaInternal();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[Blackwall] Saved into {ARENA_SCENE_PATH}");
            }
        }

        public static void BuildBlackwallArenaInternal()
        {
            // 1. Find or create root environment parent
            GameObject root = GameObject.Find("Combat_Arena_Environment");
            if (root == null)
            {
                root = GameObject.Find("Arena_Root");
                if (root == null)
                {
                    root = new GameObject("Combat_Arena_Environment");
                }
            }

            // Remove previous walls / laser fences if already built
            RemoveBuiltWallsInternal();

            // 2. Create master parent for Blackwall
            GameObject blackwallRoot = new GameObject("Blackwall_RogueAI_Arena");
            blackwallRoot.transform.SetParent(root.transform, false);

            // 3. Setup Shaders and Materials
            Material blackwallMat = GetOrCreateHolographicBlackwallMaterial();
            Material mirrorFloorMat = GetOrCreateMirrorFloorMaterial();

            // 4. Build Salar de Uyuni Salt-Flat Mirror Floor
            BuildMirrorSaltLakeFloor(blackwallRoot, mirrorFloorMat);

            // 5. Setup Atmospheric Fog (Horizonless Sky-Ground Continuum)
            ConfigureCyberspaceAtmosphere();

            // 6. Load Particle & Audio Assets
            var electricityPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ELECTRICITY_FX_PATH);
            var sparksPrefab      = AssetDatabase.LoadAssetAtPath<GameObject>(SPARKS_FX_PATH);
            var ripplePrefab      = AssetDatabase.LoadAssetAtPath<GameObject>(WATER_RIPPLE_PATH);
            var emitterPrefab     = AssetDatabase.LoadAssetAtPath<GameObject>(BASE_EMITTER_PATH);

            var ghostAttackClip   = AssetDatabase.LoadAssetAtPath<AudioClip>(GHOST_ATTACK_AUDIO_PATH);
            var ambientMusicClip   = AssetDatabase.LoadAssetAtPath<AudioClip>(AMBIENT_MUSIC_PATH);

            if (ambientMusicClip != null)
            {
                var ambientSource = blackwallRoot.GetComponent<AudioSource>();
                if (ambientSource == null) ambientSource = blackwallRoot.AddComponent<AudioSource>();
                ambientSource.clip = ambientMusicClip;
                ambientSource.loop = true;
                ambientSource.volume = 0.60f;
                ambientSource.spatialBlend = 0.05f;
                ambientSource.playOnAwake = true;
            }

            float wallDist = ARENA_HALF_SIZE;
            float wallWidth = (ARENA_HALF_SIZE * 2f) + 1.5f;

            // 7. Build 4 Towering Holographic Blackwall Sides (North, South, East, West)
            for (int side = 0; side < 4; side++)
            {
                float angle = side * 90f;
                Quaternion sideRot = Quaternion.Euler(0f, angle, 0f);
                Vector3 forwardDir = sideRot * Vector3.forward;
                Vector3 sideCenter = forwardDir * wallDist;

                GameObject sideParent = new GameObject($"Blackwall_Holographic_Side_{GetSideName(side)}");
                sideParent.transform.SetParent(blackwallRoot.transform, false);
                sideParent.transform.position = sideCenter;
                sideParent.transform.rotation = sideRot;

                // Add Barrier Controller & Repulsion Trigger
                var barrier = sideParent.AddComponent<BlackwallRogueAIBarrier>();
                barrier.rogueAIColor = new Color(5.0f, 0.02f, 0.08f, 1f); // HDR Crimson
                barrier.glitchFlashColor = new Color(6.5f, 0.35f, 0.5f, 1f);
                barrier.repulsionForce = 22f;
                barrier.upwardRepulsion = 4.0f;
                barrier.shockDamage = 45f;
                barrier.blackwallSoundLoop = ghostAttackClip;
                barrier.shockAudio = ghostAttackClip;
                barrier.audioReactive = true;
                barrier.bassSensitivity = 8.5f;
                barrier.screechSensitivity = 16.0f;

                // Trigger BoxCollider for character detection & shock
                var triggerCol = sideParent.AddComponent<BoxCollider>();
                triggerCol.isTrigger = true;
                triggerCol.size = new Vector3(wallWidth, WALL_HEIGHT, 2.5f);
                triggerCol.center = new Vector3(0f, WALL_HEIGHT * 0.5f, 0f);

                // Physical Barrier BoxCollider so characters cannot pass through
                var physicalColGO = new GameObject("Physical_Collision_Wall");
                physicalColGO.transform.SetParent(sideParent.transform, false);
                var physCol = physicalColGO.AddComponent<BoxCollider>();
                physCol.isTrigger = false;
                physCol.size = new Vector3(wallWidth, WALL_HEIGHT + 4f, 1.0f);
                physCol.center = new Vector3(0f, WALL_HEIGHT * 0.5f, 0.5f);

                // Create the Towering Holographic Data Curtain Quad Mesh
                GameObject holoPlane = CreateHolographicCurtainPlane($"Holo_Curtain_{GetSideName(side)}", wallWidth, WALL_HEIGHT, blackwallMat);
                holoPlane.transform.SetParent(sideParent.transform, false);
                holoPlane.transform.localPosition = new Vector3(0f, 0f, 0f);
                holoPlane.transform.localRotation = Quaternion.identity;

                // Low-profile sleek digital base projectors along the ground
                if (emitterPrefab != null)
                {
                    for (int em = -3; em <= 3; em += 2)
                    {
                        GameObject emitter = (GameObject)PrefabUtility.InstantiatePrefab(emitterPrefab);
                        if (emitter == null) emitter = Object.Instantiate(emitterPrefab);
                        emitter.name = $"Base_Emitter_{em}";
                        emitter.transform.SetParent(sideParent.transform, false);
                        emitter.transform.localPosition = new Vector3(em * 5.8f, 0f, 0f);
                        emitter.transform.localScale = new Vector3(0.6f, 0.35f, 0.6f);
                        emitter.transform.localRotation = Quaternion.identity;
                    }
                }

                // Pulsing Red Volumetric Point Lights (illuminates the mirror floor & combatants)
                for (int li = -2; li <= 2; li += 2)
                {
                    var lightGO = new GameObject($"Blackwall_Beacon_{li}");
                    lightGO.transform.SetParent(sideParent.transform, false);
                    lightGO.transform.localPosition = new Vector3(li * 8.0f, 3.5f, -0.8f);

                    var bLight = lightGO.AddComponent<Light>();
                    bLight.type = LightType.Point;
                    bLight.color = new Color(1f, 0.04f, 0.15f); // Deep rogue AI red
                    bLight.intensity = 5.5f;
                    bLight.range = 16f;
                    bLight.shadows = LightShadows.None;
                }

                // Rogue AI Lightning FX
                if (electricityPrefab != null)
                {
                    GameObject elecFX = (GameObject)PrefabUtility.InstantiatePrefab(electricityPrefab);
                    if (elecFX == null) elecFX = Object.Instantiate(electricityPrefab);
                    elecFX.name = "RogueAI_Electricity_FX";
                    elecFX.transform.SetParent(sideParent.transform, false);
                    elecFX.transform.localPosition = new Vector3(0f, 3.5f, 0f);
                    barrier.rogueAILightningVFX = elecFX.GetComponent<ParticleSystem>();
                }

                // Rogue AI Rising Sparks & Digital Embers
                if (sparksPrefab != null)
                {
                    GameObject sparkFX = (GameObject)PrefabUtility.InstantiatePrefab(sparksPrefab);
                    if (sparkFX == null) sparkFX = Object.Instantiate(sparksPrefab);
                    sparkFX.name = "RogueAI_Sparks_FX";
                    sparkFX.transform.SetParent(sideParent.transform, false);
                    sparkFX.transform.localPosition = new Vector3(0f, 1.0f, 0f);
                    barrier.rogueAISparksVFX = sparkFX.GetComponent<ParticleSystem>();
                }

                barrier.CollectRenderersAndLights();
            }

            // 8. Attach Footstep Ripples to combatants in scene
            AttachFootstepRipplesToCharacters(ripplePrefab);

            // 9. Balance Enemy Health
            ConfigureSceneEnemyForCombat();

            // Save Scene
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.Refresh();

            Debug.Log("<color=#FF0044><b>[CYBERPUNK BLACKWALL]</b></color> Holographic Blackwall & Salt-Lake Cyberspace Arena built successfully!");
            Debug.Log("<color=#00E5FF>• Salt Lake Ground: Real-time Planar Mirror reflection connecting sky & earth with zero horizon seam</color>");
            Debug.Log("<color=#00E5FF>• Holographic Monolith: 15m tall semi-transparent digital firewall with cascading red code</color>");
            Debug.Log("<color=#00E5FF>• Footstep Ripples: Water ripple rings expanding beneath character feet</color>");
        }

        private static void BuildMirrorSaltLakeFloor(GameObject parent, Material mirrorMat)
        {
            // Find existing arena floor or create one
            GameObject floorGO = GameObject.Find("Cyberspace_SaltLake_MirrorFloor");
            if (floorGO == null)
            {
                floorGO = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floorGO.name = "Cyberspace_SaltLake_MirrorFloor";
            }
            floorGO.transform.SetParent(parent.transform, false);
            floorGO.transform.position = new Vector3(0f, 0.01f, 0f); // Just above base terrain
            floorGO.transform.localScale = new Vector3(7.0f, 1.0f, 7.0f); // 70m x 70m expansive mirror

            var renderer = floorGO.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = mirrorMat;
            }

            // Attach Planar Mirror Reflection Controller
            var planarMirror = floorGO.GetComponent<CyberspacePlanarMirror>();
            if (planarMirror == null) planarMirror = floorGO.AddComponent<CyberspacePlanarMirror>();
            planarMirror.planeHeight = 0.01f;
            planarMirror.floorRenderer = renderer;
            planarMirror.mirrorMaterial = mirrorMat;
            planarMirror.textureResolution = 1024;
        }

        private const string SPACE_SKYBOX_PATH = "Assets/Synty/PolygonSciFiWorlds/Materials/Misc/SkyBoxSpace_01.mat";

        private static void ConfigureCyberspaceAtmosphere()
        {
            var spaceSky = AssetDatabase.LoadAssetAtPath<Material>(SPACE_SKYBOX_PATH);
            if (spaceSky != null)
            {
                RenderSettings.skybox = spaceSky;
            }

            // Seamlessly dissolves horizon line between ground and sky
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.008f, 0.008f, 0.015f, 1f); // Deep abyss matching mirror
            RenderSettings.fogDensity = 0.022f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.04f, 0.02f, 0.04f, 1f);

            // Dim directional light so the neon Blackwall emission and volumetric lights take over
            var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude);
            foreach (var l in lights)
            {
                if (l.type == LightType.Directional)
                {
                    l.intensity = 0.25f;
                    l.color = new Color(0.25f, 0.35f, 0.55f); // Moody dark cyberspace moonlight
                }
            }
        }

        private static GameObject CreateHolographicCurtainPlane(string name, float width, float height, Material mat)
        {
            GameObject planeGO = new GameObject(name);
            var meshFilter = planeGO.AddComponent<MeshFilter>();
            var meshRenderer = planeGO.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = mat;

            Mesh mesh = new Mesh();
            mesh.name = $"{name}_Mesh";

            float halfW = width * 0.5f;

            // Double-sided vertical plane (facing both inwards and outwards)
            Vector3[] vertices = new Vector3[]
            {
                new Vector3(-halfW, 0f, 0f),
                new Vector3(halfW, 0f, 0f),
                new Vector3(halfW, height, 0f),
                new Vector3(-halfW, height, 0f),

                new Vector3(halfW, 0f, 0f),
                new Vector3(-halfW, 0f, 0f),
                new Vector3(-halfW, height, 0f),
                new Vector3(halfW, height, 0f)
            };

            Vector2[] uvs = new Vector2[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),

                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            };

            Vector3[] normals = new Vector3[]
            {
                Vector3.back, Vector3.back, Vector3.back, Vector3.back,
                Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward
            };

            int[] triangles = new int[]
            {
                0, 2, 1, 0, 3, 2, // Front
                4, 6, 5, 4, 7, 6  // Back
            };

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.normals = normals;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();

            meshFilter.sharedMesh = mesh;
            return planeGO;
        }

        private static string GetSideName(int sideIndex)
        {
            switch (sideIndex)
            {
                case 0: return "North";
                case 1: return "East";
                case 2: return "South";
                case 3: return "West";
                default: return $"Side_{sideIndex}";
            }
        }

        private static Material GetOrCreateHolographicBlackwallMaterial()
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(BLACKWALL_MAT_PATH);
            Shader shader = Shader.Find("Cyberpunk/Holographic_Blackwall");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");

            if (mat == null)
            {
                mat = new Material(shader);
                mat.name = "M_Blackwall_Holographic_Monolith";
                System.IO.Directory.CreateDirectory("Assets/SyntyCombined/Materials");
                AssetDatabase.CreateAsset(mat, BLACKWALL_MAT_PATH);
            }
            else
            {
                mat.shader = shader;
            }

            mat.SetColor("_BaseColor", new Color(0.015f, 0.002f, 0.006f, 0.96f));
            mat.SetColor("_EmissionColor", new Color(5.0f, 0.02f, 0.08f, 1.0f));
            mat.SetColor("_CoreEnergyColor", new Color(5.5f, 0.45f, 0.08f, 1.0f));
            mat.SetColor("_GlitchFlashColor", new Color(6.5f, 0.2f, 0.35f, 1.0f));
            mat.SetVector("_DataTiling", new Vector4(28f, 48f, 0f, 0f));
            mat.SetVector("_MacroBlockTiling", new Vector4(8f, 14f, 0f, 0f));
            mat.SetFloat("_TendrilScale", 5.5f);
            mat.SetFloat("_TendrilSpeed", 0.8f);
            mat.SetFloat("_TendrilIntensity", 2.2f);
            mat.SetFloat("_FresnelPower", 2.8f);
            mat.SetFloat("_FresnelIntensity", 3.5f);
            mat.SetFloat("_ScanlineDensity", 220f);
            mat.SetFloat("_GlitchIntensity", 0.85f);
            mat.SetFloat("_PulseSpeed", 1.4f);

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();

            return mat;
        }

        private static Material GetOrCreateMirrorFloorMaterial()
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(MIRROR_FLOOR_MAT_PATH);
            if (mat != null) return mat;

            Shader shader = Shader.Find("Cyberpunk/Cyberspace_Mirror_Floor");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");

            mat = new Material(shader);
            mat.name = "M_Cyberspace_Mirror_Floor";
            mat.SetColor("_BaseColor", new Color(0.015f, 0.015f, 0.025f, 1.0f));
            mat.SetFloat("_MirrorReflectivity", 0.95f);
            mat.SetFloat("_FresnelPower", 3.5f);
            mat.SetFloat("_DistortionStrength", 0.012f);
            mat.SetFloat("_WaveSpeed", 0.8f);

            System.IO.Directory.CreateDirectory("Assets/SyntyCombined/Materials");
            AssetDatabase.CreateAsset(mat, MIRROR_FLOOR_MAT_PATH);
            AssetDatabase.SaveAssets();

            return mat;
        }

        private static void AttachFootstepRipplesToCharacters(GameObject ripplePrefab)
        {
            if (ripplePrefab == null) return;

            var characters = Object.FindObjectsByType<InsaneGunSwordController>();
            foreach (var c in characters)
            {
                var rippleSpawner = c.GetComponent<FootstepWaterRippleSpawner>();
                if (rippleSpawner == null) rippleSpawner = c.gameObject.AddComponent<FootstepWaterRippleSpawner>();
                rippleSpawner.ripplePrefab = ripplePrefab;
                rippleSpawner.groundHeight = 0.02f;
                EditorUtility.SetDirty(c);
            }
        }

        public static void ConfigureSceneEnemyForCombat()
        {
            var enemies = Object.FindObjectsByType<InsaneGunSwordController>();
            foreach (var enemy in enemies)
            {
                if (enemy.controlMode != InsaneGunSwordController.ControlMode.PlayerControlled)
                {
                    enemy.maxHealth = 200f; // Balanced: ~6-8 hits to defeat
                    enemy.currentHealth = 200f;
                    enemy.autoReviveOnDeath = true;
                    enemy.autoReviveDelay = 3.0f;

                    var ragdoll = enemy.GetComponent<EuphoriaRagdollController>();
                    if (ragdoll == null) ragdoll = enemy.gameObject.AddComponent<EuphoriaRagdollController>();
                    ragdoll.ragdollForceMultiplier = 1.6f;
                    ragdoll.SetupRagdollBones();

                    EditorUtility.SetDirty(enemy);
                }
            }
        }

        [MenuItem("Tools/Cyberpunk Blackwall Arena/Test Ragdoll Defeat (Instant 0 HP)", priority = 3)]
        public static void TestInstantRagdollDefeat()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[Blackwall] Please enter Play Mode to test physics ragdoll defeat!");
                return;
            }

            var enemies = Object.FindObjectsByType<InsaneGunSwordController>();
            bool found = false;
            foreach (var enemy in enemies)
            {
                if (enemy.controlMode != InsaneGunSwordController.ControlMode.PlayerControlled && !enemy.IsDead)
                {
                    Vector3 blastDir = -enemy.transform.forward + Vector3.up * 0.35f;
                    enemy.TakeDamage(9999f, enemy.transform.position + Vector3.up * 1.2f, blastDir.normalized, isHeavy: true);
                    found = true;
                    Debug.Log($"<color=#FF0044>[Blackwall]</color> Defeated enemy '{enemy.name}'! Triggered Euphoria Ragdoll.");
                    break;
                }
            }

            if (!found)
            {
                Debug.Log("[Blackwall] No active enemy found in scene to defeat.");
            }
        }

        [MenuItem("Tools/Cyberpunk Blackwall Arena/Remove Blackwall Arena", priority = 4)]
        public static void RemoveBuiltWalls()
        {
            RemoveBuiltWallsInternal();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[Blackwall] Blackwall Arena removed.");
        }

        private static void RemoveBuiltWallsInternal()
        {
            string[] targetNames = new string[] { "Blackwall_RogueAI_Arena", "ClosedArena_Walls", "ClosedArena_Root", "Cyberspace_SaltLake_MirrorFloor" };
            foreach (var n in targetNames)
            {
                GameObject obj = GameObject.Find(n);
                if (obj != null)
                {
                    Object.DestroyImmediate(obj);
                }
            }

            // Also clean up old legacy laser walls & barricades
            GameObject root = GameObject.Find("Combat_Arena_Environment");
            if (root != null)
            {
                var toRemove = new System.Collections.Generic.List<GameObject>();
                foreach (Transform child in root.transform)
                {
                    if (child.name.StartsWith("Laser_Wall_") || child.name.StartsWith("Barricade_") || child.name.StartsWith("Blackwall_Side_"))
                    {
                        toRemove.Add(child.gameObject);
                    }
                }
                foreach (var go in toRemove)
                {
                    Object.DestroyImmediate(go);
                }
            }
        }
    }
}