using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using InsaneGun.Combat;
using InsaneGun.Testing;
using InsaneGun.Arena;

namespace InsaneGun.EditorTools
{
    /// <summary>
    /// Builds a world-class, authentic Cyberpunk Championship Fight Stadium / Colosseum.
    /// 
    /// Architecture:
    /// - 125m x 125m x 36m colossal industrial brutalist stadium colosseum enclosure.
    /// - Heavy overhead space-frame steel roof trusses and catwalks with industrial maintenance rails.
    /// - Suspended Center-Hung Mega Jumbotron Scoreboard Rig (Y = 13.5m) with 4 widescreen LED match
    ///   displays, 360-degree fascia ticker, line-array audio speaker clusters, and rotating holographic dragon.
    /// - Central 22m x 22m Raised Championship Ring (0.85m high) with heavy matte-black steel corner posts,
    ///   authentic padded turnbuckles (Red Corner, Blue Corner, Neutral Corners), 3-tier tension cables,
    ///   and multi-ring neon octagon combat markings on a dark carbon-fiber canvas.
    /// - Ringside Production & Broadcast Zone: Heavy steel crowd barricades, TV broadcast commentators desk
    ///   with live monitors, ringside camera operators on tripods, corner hydration crates, and VIP floor seats.
    /// - Multi-Tier Stadium Grandstand Bowl wrapping around the colosseum:
    ///   * Lower Bowl (Tiers 1-10, 16m to 38m): Raked seating with installed stadium chairs and dual-tone LED edge strips.
    ///   * VIP Mezzanine Suites (Tiers 11-12, Y = 13m-16.5m): Suspended luxury executive boxes with smoked glass,
    ///     warm lounge illumination, and continuous 360-degree LED digital ribbon ticker.
    ///   * Upper Deck Sky Bowl (Tiers 13-18, Y = 17.5m-27m): Soaring upper grandstands packed with fans.
    /// - Grand Fighter Entrance Stage & Runway Ramp at South Concourse with colossal video entrance arch and lasers.
    /// - Volumetric searchlight light cones (SM_LightRayRound_01) cutting through the stadium atmospheric haze.
    /// - Hovering TV broadcast camera drones (FX_Drone_01) in 3D space around the ring with spotlight downlights.
    /// - 550+ Diverse Cyberpunk Spectators placed across all tiers in pre-sampled edit-mode cheering poses.
    /// - Full URP Global Post-Processing Volume with Bloom (1.35) and ACES Tonemapping.
    /// </summary>
    public static class CyberpunkIndoorArenaBuilder
    {
        private const string ScenePath = "Assets/Scenes/Cyberpunk_Indoor_Combat_Arena.unity";
        private const string TestArenaPath = "Assets/Scenes/InsaneGun_Combat_TestArena.unity";
        private const string ControllerDir = "Assets/Animations/Spectators";

        [MenuItem("Tools/Cyberpunk/Build Indoor Fight Arena (Closed Building + Cheering Crowd)")]
        public static void BuildIndoorArenaMenu()
        {
            BuildAndSaveScene(ScenePath);
            BuildAndSaveScene(TestArenaPath);
        }

        public static void BuildAndSaveScene(string targetScenePath)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = Path.GetFileNameWithoutExtension(targetScenePath);

            EnsureDirectories();
            var spectatorControllers = EnsureSpectatorControllers();

            // 1. Building Enclosure & Overhead Space-Frame Trusses (150m x 150m x 38m)
            GameObject stadiumRoot = BuildStadiumEnclosure();

            // 2. Global Post-Processing Volume (Bloom 1.35, ACES Tonemapping, Color Grading)
            BuildPostProcessingVolume(stadiumRoot.transform);

            // 3. Central Championship Combat Ring (22m x 22m raised platform with turnbuckles & decals)
            GameObject ringRoot = BuildChampionshipCombatRing(stadiumRoot.transform);

            // 4. Ringside TV Production & Broadcast Zone (Commentators desk, cameras, corner gear, VIP seats)
            BuildRingsideProductionZone(stadiumRoot.transform);

            // 5. Multi-Tier Stadium Grandstand Bowl (Lower Bowl + VIP Suites + Upper Deck)
            GameObject grandstandsRoot = BuildStadiumGrandstandBowl(stadiumRoot.transform);

            // 6. South Concourse Fighter Entrance Stage & Grand Runway Ramp
            BuildFighterEntranceStageAndRunway(stadiumRoot.transform);

            // 7. Center-Hung Overhead Mega Jumbotron Scoreboard Rig (Y = 13.5m) & Speakers
            BuildCenterHungJumbotronAndTrusses(stadiumRoot.transform);

            // 8. Overhead Space-Frame Catwalks & Roof Truss Girders
            BuildOverheadTrussesAndCatwalks(stadiumRoot.transform);

            // 9. Volumetric Searchlight Beams & TV Broadcast Drones
            BuildVolumetricBeamsAndBroadcastDrones(stadiumRoot.transform);

            // 10. Professional Stadium Broadcast Lighting Rig
            BuildStadiumLighting(stadiumRoot.transform);

            // 11. Spectators are dynamically managed by ArenaSpectatorCrowdSpawner (<1.5MB scene YAML!)
            // GameObject crowdRoot = BuildSpectatorCrowd(stadiumRoot.transform, spectatorControllers);

            // 12. Spawn Combatants (Alita Hero + Rival Duelist inside the ring)
            var (player, rival) = SpawnCombatants(ringRoot.transform);

            // 13. Connect Spectators to Combatants
            // ConnectSpectatorsToCombat(crowdRoot, rival != null ? rival.transform : null);

            // 14. Setup Smooth Orbit Camera
            SetupIndoorCamera(player.transform, stadiumRoot.transform);

            // 15. Add Arena Combat Manager & BGM Audio
            SetupArenaManager(player, rival, stadiumRoot.transform);

            // 15.5 Purge any spectator crowd objects before saving so scene file YAML remains under 2.5MB
            var allGo = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var go in allGo)
            {
                if (go != null && (go.name.Contains("Spectator_Crowd") || go.name.StartsWith("Spectator_T")))
                {
                    Object.DestroyImmediate(go);
                }
            }

            // 16. Mark and Save Scene
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, targetScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"<color=#00FFFF>[CyberpunkArena] Authentic World-Class Championship Fight Stadium successfully built at: {targetScenePath}</color>");
        }

        private static void EnsureDirectories()
        {
            if (!Directory.Exists("Assets/Scenes")) Directory.CreateDirectory("Assets/Scenes");
            if (!Directory.Exists(ControllerDir)) Directory.CreateDirectory(ControllerDir);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 1. STADIUM ENCLOSURE (125m x 125m x 36m)
        // ─────────────────────────────────────────────────────────────────────
        private static GameObject BuildStadiumEnclosure()
        {
            GameObject root = new GameObject("Championship_Stadium_Complex");

            var floorMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/PolygonCyberCity/Materials/Building/Floor_02.mat")
                        ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/PolygonCyberCity/Materials/Building/Floor_01.mat");
            var wallMat  = AssetDatabase.LoadAssetAtPath<Material>("Assets/PolygonCyberCity/Materials/Building/Wall_01_Triplanar.mat")
                        ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/PolygonCyberCity/Materials/Building/Wall_01.mat")
                        ?? floorMat;
            var ceilingMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/PolygonCyberCity/Materials/Building/PolygonCyberCity_Triplanar_01_C.mat")
                          ?? wallMat;

            float stadiumW = 150f;
            float stadiumH = 38f;
            float halfDist = stadiumW * 0.5f;

            // --- Floor Slab (125m x 125m) ---
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Stadium_Foundation_Floor";
            floor.transform.SetParent(root.transform);
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(stadiumW, 1.0f, stadiumW);
            if (floorMat != null) floor.GetComponent<Renderer>().sharedMaterial = floorMat;

            // --- Roof / Ceiling (Shadow Casting OFF so interior receives directional fill!) ---
            GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceiling.name = "Stadium_Dome_Ceiling";
            ceiling.transform.SetParent(root.transform);
            ceiling.transform.position = new Vector3(0f, stadiumH, 0f);
            ceiling.transform.localScale = new Vector3(stadiumW, 1.2f, stadiumW);
            if (ceilingMat != null) ceiling.GetComponent<Renderer>().sharedMaterial = ceilingMat;
            var cRend = ceiling.GetComponent<Renderer>();
            if (cRend != null) cRend.shadowCastingMode = ShadowCastingMode.Off;

            // --- 4 Perimeter Stadium Walls ---
            float wallThick = 2.5f;
            float wallCenterY = stadiumH * 0.5f;

            CreateWall(root.transform, "Wall_North", new Vector3(0f, wallCenterY, halfDist), new Vector3(stadiumW, stadiumH, wallThick), wallMat);
            CreateWall(root.transform, "Wall_South", new Vector3(0f, wallCenterY, -halfDist), new Vector3(stadiumW, stadiumH, wallThick), wallMat);
            CreateWall(root.transform, "Wall_East",  new Vector3(halfDist, wallCenterY, 0f), new Vector3(wallThick, stadiumH, stadiumW), wallMat);
            CreateWall(root.transform, "Wall_West",  new Vector3(-halfDist, wallCenterY, 0f), new Vector3(wallThick, stadiumH, stadiumW), wallMat);

            // --- High Wall Holographic Billboards & Sponsor Signs ---
            string[] holoPrefabs = new[]
            {
                "Assets/PolygonCyberCity/Prefabs/Props/SM_Prop_Holo_Sign_01.prefab",
                "Assets/PolygonCyberCity/Prefabs/Props/SM_Prop_Holo_Sign_04.prefab",
                "Assets/PolygonCyberCity/Prefabs/Props/SM_Prop_Holo_Sign_07.prefab",
                "Assets/PolygonCyberCity/Prefabs/Props/SM_Prop_Holo_Sign_10.prefab",
                "Assets/PolygonCyberCity/Prefabs/Props/SM_Prop_Holo_Sign_14.prefab"
            };

            for (int s = 0; s < 4; s++)
            {
                float rotY = s * 90f;
                float dist = stadiumW * 0.5f - 3.0f;

                for (int h = -1; h <= 1; h++)
                {
                    string hPath = holoPrefabs[Mathf.Abs(s + h + 5) % holoPrefabs.Length];
                    var pref = AssetDatabase.LoadAssetAtPath<GameObject>(hPath);
                    if (pref != null)
                    {
                        Vector3 hp;
                        if (s == 0) hp = new Vector3(h * 32f, 26.0f, dist);
                        else if (s == 1) hp = new Vector3(dist, 26.0f, h * 32f);
                        else if (s == 2) hp = new Vector3(h * 32f, 26.0f, -dist);
                        else hp = new Vector3(-dist, 26.0f, h * 32f);

                        var go = Object.Instantiate(pref, hp, Quaternion.Euler(0f, rotY + 180f, 0f), root.transform);
                        go.name = $"HighWall_Holo_Billboard_{s}_{h}";
                        go.transform.localScale = Vector3.one * 5.5f;
                    }
                }
            }

            return root;
        }

        private static GameObject CreateWall(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(parent);
            wall.transform.position = pos;
            wall.transform.localScale = scale;
            if (mat != null) wall.GetComponent<Renderer>().sharedMaterial = mat;
            var r = wall.GetComponent<Renderer>();
            if (r != null) r.shadowCastingMode = ShadowCastingMode.Off;
            return wall;
        }

        // ─────────────────────────────────────────────────────────────────────
        // 2. GLOBAL POST-PROCESSING VOLUME (BLOOM & ACES TONEMAPPING)
        // ─────────────────────────────────────────────────────────────────────
        private static void BuildPostProcessingVolume(Transform parent)
        {
            var volGo = new GameObject("Stadium_Global_PostProcess_Volume");
            volGo.transform.SetParent(parent);

            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 100;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Stadium_Championship_VolumeProfile";

            // Bloom: subtle, cinematic neon bloom without blinding glare
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.value = 0.65f;
            bloom.threshold.value = 1.10f;
            bloom.scatter.value = 0.65f;

            // Rich contrast & natural exposure
            var ca = profile.Add<ColorAdjustments>(true);
            ca.contrast.value = 25f;
            ca.saturation.value = 18f;
            ca.postExposure.value = -0.10f;

            // ACES Cinematic Tonemapping
            var tm = profile.Add<Tonemapping>(true);
            tm.mode.value = TonemappingMode.ACES;

            vol.profile = profile;
        }

        // ─────────────────────────────────────────────────────────────────────
        // 3. CENTRAL CHAMPIONSHIP COMBAT RING (22m x 22m RAISED PLATFORM)
        // ─────────────────────────────────────────────────────────────────────
        private static GameObject BuildChampionshipCombatRing(Transform parent)
        {
            GameObject ringRoot = new GameObject("Championship_Combat_Ring");
            ringRoot.transform.SetParent(parent);

            var litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var floorMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/PolygonCyberCity/Materials/Building/Floor_02.mat");
            var steelMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/PolygonCyberCity/Materials/Props/Rusty_Metal.mat");

            float ringSize = 56.0f;
            float stageHeight = 0.85f;

            // --- 1. Raised Fighting Ring Platform (56m x 56m, 0.85m high) ---
            GameObject ringStage = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ringStage.name = "Raised_Ring_Stage_Platform";
            ringStage.transform.SetParent(ringRoot.transform);
            ringStage.transform.position = new Vector3(0f, stageHeight * 0.5f, 0f);
            ringStage.transform.localScale = new Vector3(ringSize, stageHeight, ringSize);

            var canvasMat = new Material(litShader);
            canvasMat.color = new Color(0.07f, 0.09f, 0.12f);
            canvasMat.SetFloat("_Smoothness", 0.45f);
            ringStage.GetComponent<Renderer>().sharedMaterial = canvasMat;

            // Ring Apron Border (4 narrow perimeter edge strips around the stage)
            var apronMat = new Material(litShader);
            apronMat.color = new Color(0.12f, 0.12f, 0.14f);
            apronMat.EnableKeyword("_EMISSION");
            apronMat.SetColor("_EmissionColor", new Color(1f, 0.45f, 0f) * 2.5f);

            float halfR = ringSize * 0.5f - 0.4f;
            float edgeW = ringSize;
            float edgeThick = 0.22f;
            float edgeY = stageHeight + 0.015f;

            CreateEdgeBar(ringRoot.transform, "Apron_Edge_N", new Vector3(0f, edgeY, halfR + 0.30f), new Vector3(edgeW, 0.03f, edgeThick), apronMat);
            CreateEdgeBar(ringRoot.transform, "Apron_Edge_S", new Vector3(0f, edgeY, -halfR - 0.30f), new Vector3(edgeW, 0.03f, edgeThick), apronMat);
            CreateEdgeBar(ringRoot.transform, "Apron_Edge_E", new Vector3(halfR + 0.30f, edgeY, 0f), new Vector3(edgeThick, 0.03f, edgeW), apronMat);
            CreateEdgeBar(ringRoot.transform, "Apron_Edge_W", new Vector3(-halfR - 0.30f, edgeY, 0f), new Vector3(edgeThick, 0.03f, edgeW), apronMat);

            // --- 2. In-Ring Glowing Cyber Octagon Markings on Canvas ---
            float ringY = stageHeight + 0.02f;
            CreateWireframeCircle(ringRoot.transform, "Octagon_Canvas_Outer_Cyan", 48.0f, ringY, new Color(0f, 0.95f, 1f), 0.22f);
            CreateWireframeCircle(ringRoot.transform, "Octagon_Canvas_Mid_Magenta", 30.0f, ringY, new Color(1f, 0.15f, 0.75f), 0.18f);
            CreateWireframeCircle(ringRoot.transform, "Octagon_Canvas_Center_Amber", 14.0f, ringY, new Color(1f, 0.60f, 0.05f), 0.15f);

            // Red Corner and Blue Corner In-Ring Circles
            CreateWireframeCircle(ringRoot.transform, "Combat_BlueCorner_Circle", 5.0f, ringY, new Color(0.15f, 0.65f, 1.0f), 0.12f, new Vector3(halfR - 4.5f, 0f, halfR - 4.5f));
            CreateWireframeCircle(ringRoot.transform, "Combat_RedCorner_Circle", 5.0f, ringY, new Color(1.0f, 0.15f, 0.18f), 0.12f, new Vector3(-halfR + 4.5f, 0f, -halfR + 4.5f));

            // --- 3. Authentic Championship Turnbuckle Posts & Ropes ---
            // Real corner posts are heavy matte-black steel cylinders with team turnbuckle pads!
            Vector3[] postCorners = new[]
            {
                new Vector3(halfR, stageHeight, halfR),    // North-East: Blue Corner
                new Vector3(-halfR, stageHeight, halfR),   // North-West: Neutral Corner
                new Vector3(-halfR, stageHeight, -halfR),  // South-West: Red Corner
                new Vector3(halfR, stageHeight, -halfR)    // South-East: Neutral Corner
            };

            string[] cornerNames = new[] { "BlueCorner", "NeutralWest", "RedCorner", "NeutralEast" };
            Color[] teamColors = new[]
            {
                new Color(0.15f, 0.65f, 1.0f),  // Blue Corner
                new Color(0.85f, 0.85f, 0.90f),  // White / Neutral
                new Color(1.0f, 0.15f, 0.20f),  // Red Corner
                new Color(0.85f, 0.85f, 0.90f)   // White / Neutral
            };

            var postSteelMat = new Material(litShader);
            postSteelMat.color = new Color(0.10f, 0.11f, 0.13f);
            postSteelMat.SetFloat("_Metallic", 0.85f);
            postSteelMat.SetFloat("_Smoothness", 0.60f);

            for (int i = 0; i < 4; i++)
            {
                Vector3 pPos = postCorners[i];
                Color cCol = teamColors[i];

                // Turnbuckle Steel Post (2.4m high from canvas, 0.40m diameter)
                GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                post.name = $"Ring_Post_{cornerNames[i]}";
                post.transform.SetParent(ringRoot.transform);
                post.transform.position = pPos + Vector3.up * 1.2f;
                post.transform.localScale = new Vector3(0.40f, 1.2f, 0.40f);
                post.GetComponent<Renderer>().sharedMaterial = postSteelMat;

                // Padded Corner Turnbuckle Pad (protecting fighters at the corner)
                GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pad.name = $"Turnbuckle_Pad_{cornerNames[i]}";
                pad.transform.SetParent(post.transform);
                pad.transform.position = pPos + Vector3.up * 1.35f;
                pad.transform.localScale = new Vector3(0.65f, 1.8f, 0.65f);

                var padMat = new Material(litShader);
                padMat.color = cCol;
                padMat.EnableKeyword("_EMISSION");
                padMat.SetColor("_EmissionColor", cCol * 1.6f);
                pad.GetComponent<Renderer>().sharedMaterial = padMat;

                // Corner Top Beacon Light
                var beaconGo = new GameObject($"BeaconLight_{cornerNames[i]}");
                beaconGo.transform.SetParent(post.transform);
                beaconGo.transform.position = pPos + Vector3.up * 2.55f;
                var bLight = beaconGo.AddComponent<Light>();
                bLight.type = LightType.Point;
                bLight.color = cCol;
                bLight.intensity = 3.0f;
                bLight.range = 8.0f;

                // 3 Tiers of Realistic Championship Tension Cables (0.05m thick with tension sleeves)
                Vector3 nextPos = postCorners[(i + 1) % 4];
                for (int r = 1; r <= 3; r++)
                {
                    float ropeH = stageHeight + r * 0.68f;
                    Vector3 rStart = new Vector3(pPos.x, ropeH, pPos.z);
                    Vector3 rEnd   = new Vector3(nextPos.x, ropeH, nextPos.z);
                    Vector3 rMid   = (rStart + rEnd) * 0.5f;
                    float rLen     = Vector3.Distance(rStart, rEnd);

                    GameObject rope = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    rope.name = $"Ring_Rope_Side_{i}_Tier_{r}";
                    rope.transform.SetParent(ringRoot.transform);
                    rope.transform.position = rMid;
                    rope.transform.LookAt(rEnd);
                    rope.transform.Rotate(90f, 0f, 0f);
                    rope.transform.localScale = new Vector3(0.045f, rLen * 0.5f, 0.045f);

                    var ropeMat = new Material(litShader);
                    ropeMat.color = new Color(0.12f, 0.12f, 0.15f);
                    ropeMat.EnableKeyword("_EMISSION");
                    // Sleek cyber tracer glow on the middle and top ropes
                    Color ropeGlow = (r == 3) ? new Color(0f, 0.95f, 1f) : (r == 2 ? new Color(1f, 0.20f, 0.75f) : new Color(0.9f, 0.5f, 0.1f));
                    ropeMat.SetColor("_EmissionColor", ropeGlow * 1.8f);
                    rope.GetComponent<Renderer>().sharedMaterial = ropeMat;
                    Object.DestroyImmediate(rope.GetComponent<Collider>());
                }
            }

            // --- 4. Metal Access Stairs leading up to the ring (North and South) ---
            BuildRingStairs(ringRoot.transform, new Vector3(0f, 0f, halfR + 0.85f), Quaternion.identity, stageHeight);
            BuildRingStairs(ringRoot.transform, new Vector3(0f, 0f, -halfR - 0.85f), Quaternion.Euler(0f, 180f, 0f), stageHeight);

            return ringRoot;
        }

        private static void BuildRingStairs(Transform parent, Vector3 pos, Quaternion rot, float targetHeight)
        {
            var litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(litShader);
            mat.color = new Color(0.15f, 0.16f, 0.20f);
            mat.SetFloat("_Metallic", 0.7f);

            GameObject stairsRoot = new GameObject("Ring_Access_Stairs");
            stairsRoot.transform.SetParent(parent);
            stairsRoot.transform.position = pos;
            stairsRoot.transform.rotation = rot;

            int steps = 3;
            float stepRise = targetHeight / steps;
            float stepRun = 0.45f;
            float width = 3.2f;

            for (int s = 0; s < steps; s++)
            {
                GameObject step = GameObject.CreatePrimitive(PrimitiveType.Cube);
                step.name = $"Step_{s + 1}";
                step.transform.SetParent(stairsRoot.transform);
                step.transform.localPosition = new Vector3(0f, (s + 0.5f) * stepRise, (steps - 1 - s) * stepRun);
                step.transform.localScale = new Vector3(width, stepRise, stepRun);
                step.GetComponent<Renderer>().sharedMaterial = mat;
            }
        }

        private static void CreateEdgeBar(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = name;
            bar.transform.SetParent(parent);
            bar.transform.position = pos;
            bar.transform.localScale = scale;
            if (mat != null) bar.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(bar.GetComponent<Collider>());
        }

        private static GameObject CreateWireframeCircle(Transform parent, string name, float diameter, float yPos, Color emissiveColor, float thickness, Vector3 centerOffset = default)
        {
            var litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(litShader);
            mat.color = new Color(0.02f, 0.02f, 0.03f, 0.9f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emissiveColor * 3.5f);

            GameObject circle = new GameObject(name);
            circle.transform.SetParent(parent);
            circle.transform.position = centerOffset;

            int segments = 24;
            float radius = diameter * 0.5f;
            float angleStep = 360f / segments;

            for (int i = 0; i < segments; i++)
            {
                float a1 = i * angleStep * Mathf.Deg2Rad;
                float a2 = (i + 1) * angleStep * Mathf.Deg2Rad;
                Vector3 p1 = centerOffset + new Vector3(Mathf.Sin(a1) * radius, yPos + 0.015f, Mathf.Cos(a1) * radius);
                Vector3 p2 = centerOffset + new Vector3(Mathf.Sin(a2) * radius, yPos + 0.015f, Mathf.Cos(a2) * radius);

                Vector3 mid = (p1 + p2) * 0.5f;
                float segLen = Vector3.Distance(p1, p2);

                GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bar.name = $"Seg_{i}";
                bar.transform.SetParent(circle.transform);
                bar.transform.position = mid;
                bar.transform.LookAt(p2);
                bar.transform.localScale = new Vector3(thickness, 0.025f, segLen);
                bar.GetComponent<Renderer>().sharedMaterial = mat;
                Object.DestroyImmediate(bar.GetComponent<Collider>());
            }

            return circle;
        }

        // ─────────────────────────────────────────────────────────────────────
        // 4. RINGSIDE TV PRODUCTION & BROADCAST ZONE (13.5m TO 16m)
        // ─────────────────────────────────────────────────────────────────────
        private static void BuildRingsideProductionZone(Transform parent)
        {
            GameObject prodRoot = new GameObject("Ringside_Production_And_Broadcast_Zone");
            prodRoot.transform.SetParent(parent);

            var litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var barMat = new Material(litShader);
            barMat.color = new Color(0.12f, 0.12f, 0.15f);
            barMat.EnableKeyword("_EMISSION");
            barMat.SetColor("_EmissionColor", new Color(0f, 0.85f, 1f) * 1.5f);

            float barDist = 31.0f;
            float barLen = 62.0f;
            float barH = 1.15f;
            float barThick = 0.25f;

            // 1. Heavy Security Barricades surrounding the ring apron
            CreateBarricade(prodRoot.transform, "Barricade_North", new Vector3(0f, barH * 0.5f, barDist), new Vector3(barLen, barH, barThick), barMat);
            CreateBarricade(prodRoot.transform, "Barricade_South", new Vector3(0f, barH * 0.5f, -barDist), new Vector3(barLen, barH, barThick), barMat);
            CreateBarricade(prodRoot.transform, "Barricade_East",  new Vector3(barDist, barH * 0.5f, 0f), new Vector3(barThick, barH, barLen), barMat);
            CreateBarricade(prodRoot.transform, "Barricade_West",  new Vector3(-barDist, barH * 0.5f, 0f), new Vector3(barThick, barH, barLen), barMat);

            // 2. Ringside TV Commentary / Broadcast Desk on West Ringside
            string deskPrefabPath = "Assets/PolygonCyberCity/Prefabs/Props/SM_Prop_Desk_01.prefab";
            var deskPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(deskPrefabPath);
            if (deskPrefab != null)
            {
                var desk1 = Object.Instantiate(deskPrefab, new Vector3(-29.5f, 0f, 1.8f), Quaternion.Euler(0f, 90f, 0f), prodRoot.transform);
                desk1.name = "Broadcast_Desk_Main";
                desk1.transform.localScale = Vector3.one * 1.3f;

                var desk2 = Object.Instantiate(deskPrefab, new Vector3(-29.5f, 0f, -1.8f), Quaternion.Euler(0f, 90f, 0f), prodRoot.transform);
                desk2.name = "Broadcast_Desk_Secondary";
                desk2.transform.localScale = Vector3.one * 1.3f;
            }

            // 3. Ringside Broadcast TV Cameras on Tripods
            string camPrefabPath = "Assets/PolygonCyberCity/Prefabs/Props/SM_Prop_Camera_01.prefab";
            var camPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(camPrefabPath);
            if (camPrefab != null)
            {
                Vector3[] camPositions = new[]
                {
                    new Vector3(0f, 0f, 29.8f),      // North Camera
                    new Vector3(29.8f, 0f, 0f),      // East Camera
                    new Vector3(-29.8f, 0f, -6.5f),  // West Ringside Camera
                    new Vector3(-12.5f, 0f, -29.8f)  // South Camera
                };

                for (int c = 0; c < camPositions.Length; c++)
                {
                    Vector3 cp = camPositions[c];
                    var camGo = Object.Instantiate(camPrefab, cp, Quaternion.identity, prodRoot.transform);
                    camGo.name = $"Ringside_TV_Camera_{c + 1}";
                    camGo.transform.LookAt(Vector3.up * 1.2f);
                    camGo.transform.localScale = Vector3.one * 1.4f;
                }
            }

            // 4. Ringside VIP Floor Seating (Front row chairs just inside the barricades)
            string seatPrefabPath = "Assets/PolygonCyberCity/Prefabs/Props/SM_Prop_Bench_Seat_01.prefab";
            var seatPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(seatPrefabPath);
            if (seatPrefab != null)
            {
                // North and East ringside VIP benches
                for (int b = -6; b <= 6; b++)
                {
                    if (Mathf.Abs(b) < 2) continue;
                    var seatN = Object.Instantiate(seatPrefab, new Vector3(b * 3.8f, 0f, 29.5f), Quaternion.Euler(0f, 180f, 0f), prodRoot.transform);
                    seatN.name = $"VIP_Floor_Seat_North_{b}";
                    seatN.transform.localScale = Vector3.one * 1.2f;

                    var seatE = Object.Instantiate(seatPrefab, new Vector3(29.5f, 0f, b * 3.8f), Quaternion.Euler(0f, 270f, 0f), prodRoot.transform);
                    seatE.name = $"VIP_Floor_Seat_East_{b}";
                    seatE.transform.localScale = Vector3.one * 1.2f;
                }
            }
        }

        private static void CreateBarricade(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            b.name = name;
            b.transform.SetParent(parent);
            b.transform.position = pos;
            b.transform.localScale = scale;
            b.GetComponent<Renderer>().sharedMaterial = mat;
        }

        // ─────────────────────────────────────────────────────────────────────
        // 5. MULTI-TIER STADIUM GRANDSTAND BOWL (LOWER BOWL + VIP + UPPER DECK)
        // ─────────────────────────────────────────────────────────────────────
        public struct GrandstandTierInfo
        {
            public int tierIndex;
            public Vector3 center;
            public Vector3 forwardDir;
            public float width;
            public float height;
            public bool isVipBalcony;
        }

        private static List<GrandstandTierInfo> _allBleacherTiers = new List<GrandstandTierInfo>();

        private static GameObject BuildStadiumGrandstandBowl(Transform parent)
        {
            _allBleacherTiers.Clear();

            GameObject grandstandRoot = new GameObject("Stadium_Grandstands_MultiTier_Bowl");
            grandstandRoot.transform.SetParent(parent);

            var stepMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/PolygonCyberCity/Materials/Building/Floor_01.mat");
            var wallMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/PolygonCyberCity/Materials/Building/Wall_01_Triplanar.mat");
            var glassMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/PolygonCyberCity/Materials/Props/Glass_Transparent_Shiny.mat");
            string railPrefabPath = "Assets/PolygonCyberCity/Prefabs/Buildings/SM_Bld_Railing_Straight_01.prefab";
            var railPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(railPrefabPath);

            var litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            // Subtle, sleek Glowing LED Edge Strips for every riser (no blinding glare)
            var ledCyanMat = new Material(litShader);
            ledCyanMat.color = new Color(0.04f, 0.08f, 0.12f);
            ledCyanMat.EnableKeyword("_EMISSION");
            ledCyanMat.SetColor("_EmissionColor", new Color(0f, 0.95f, 1f) * 1.5f);

            var ledAmberMat = new Material(litShader);
            ledAmberMat.color = new Color(0.12f, 0.06f, 0.02f);
            ledAmberMat.EnableKeyword("_EMISSION");
            ledAmberMat.SetColor("_EmissionColor", new Color(1f, 0.50f, 0.05f) * 1.5f);

            // 360-Degree Continuous Fascia Ribbon Board Display
            var ribbonMat = new Material(litShader);
            ribbonMat.color = new Color(0.05f, 0.02f, 0.10f);
            ribbonMat.EnableKeyword("_EMISSION");
            ribbonMat.SetColor("_EmissionColor", new Color(1f, 0.15f, 0.85f) * 1.8f);

            // VIP Lounge Warm Golden Glow Material
            var vipGlowMat = new Material(litShader);
            vipGlowMat.color = new Color(0.20f, 0.15f, 0.05f);
            vipGlowMat.EnableKeyword("_EMISSION");
            vipGlowMat.SetColor("_EmissionColor", new Color(1.0f, 0.70f, 0.20f) * 0.9f);

            float pitHalf = 33.0f; // Regulation distance from center ring to Tier 1

            // 4 Sides of the Grandstand Colosseum
            BuildGrandstandSide(grandstandRoot.transform, "Grandstand_North", 0f, pitHalf, Vector3.forward, Vector3.back, stepMat, wallMat, glassMat, railPrefab, ledCyanMat, ledAmberMat, ribbonMat, vipGlowMat);
            BuildGrandstandSide(grandstandRoot.transform, "Grandstand_South", 0f, -pitHalf, Vector3.back, Vector3.forward, stepMat, wallMat, glassMat, railPrefab, ledCyanMat, ledAmberMat, ribbonMat, vipGlowMat);
            BuildGrandstandSide(grandstandRoot.transform, "Grandstand_East",  pitHalf, 0f, Vector3.right, Vector3.left, stepMat, wallMat, glassMat, railPrefab, ledCyanMat, ledAmberMat, ribbonMat, vipGlowMat);
            BuildGrandstandSide(grandstandRoot.transform, "Grandstand_West",  -pitHalf, 0f, Vector3.left, Vector3.right, stepMat, wallMat, glassMat, railPrefab, ledCyanMat, ledAmberMat, ribbonMat, vipGlowMat);

            return grandstandRoot;
        }

        private static void BuildGrandstandSide(
            Transform parent, string sideName,
            float startX, float startZ,
            Vector3 outwardsDir, Vector3 lookInwardDir,
            Material stepMat, Material wallMat, Material glassMat,
            GameObject railPrefab,
            Material ledCyanMat, Material ledAmberMat,
            Material ribbonMat, Material vipGlowMat)
        {
            GameObject sideRoot = new GameObject(sideName);
            sideRoot.transform.SetParent(parent);

            bool isZAxis = Mathf.Abs(outwardsDir.z) > 0.5f;
            float baseWidth = 70.0f;

            // ─────────────────────────────────────────────────────────────────
            // A. LOWER BOWL (Tiers 1 to 10: Y = 1.1m to 11.0m, dist = 16.5m to 38.5m)
            // ─────────────────────────────────────────────────────────────────
            int lowerTiers = 10;
            float lowerDepth = 2.2f;
            float lowerRise = 1.05f;

            for (int t = 0; t < lowerTiers; t++)
            {
                float tierDist = (t + 0.5f) * lowerDepth;
                float tierHeight = (t + 1) * lowerRise;
                float tierW = baseWidth + t * 4.2f;

                Vector3 tierPos = isZAxis ? new Vector3(startX, tierHeight * 0.5f, startZ + outwardsDir.z * tierDist)
                                          : new Vector3(startX + outwardsDir.x * tierDist, tierHeight * 0.5f, startZ);
                Vector3 tierScale = isZAxis ? new Vector3(tierW, tierHeight, lowerDepth)
                                            : new Vector3(lowerDepth, tierHeight, tierW);

                GameObject step = GameObject.CreatePrimitive(PrimitiveType.Cube);
                step.name = $"LowerBowl_Tier_{t + 1}";
                step.transform.SetParent(sideRoot.transform);
                step.transform.position = tierPos;
                step.transform.localScale = tierScale;
                if (stepMat != null) step.GetComponent<Renderer>().sharedMaterial = stepMat;

                // Glowing LED Edge Strip
                float lipY = tierHeight + 0.03f;
                Vector3 ledPos = isZAxis ? new Vector3(startX, lipY, startZ + outwardsDir.z * (t * lowerDepth))
                                         : new Vector3(startX + outwardsDir.x * (t * lowerDepth), lipY, startZ);
                Vector3 ledScale = isZAxis ? new Vector3(tierW, 0.06f, 0.10f)
                                           : new Vector3(0.10f, 0.06f, tierW);

                GameObject ledStrip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ledStrip.name = $"LowerBowl_Tier_{t + 1}_LED";
                ledStrip.transform.SetParent(sideRoot.transform);
                ledStrip.transform.position = ledPos;
                ledStrip.transform.localScale = ledScale;
                ledStrip.GetComponent<Renderer>().sharedMaterial = (t % 2 == 0) ? ledCyanMat : ledAmberMat;
                Object.DestroyImmediate(ledStrip.GetComponent<Collider>());

                // Record standing position for crowd
                Vector3 standingCenter = tierPos + Vector3.up * (tierHeight * 0.5f);
                _allBleacherTiers.Add(new GrandstandTierInfo
                {
                    tierIndex = t,
                    center = standingCenter,
                    forwardDir = lookInwardDir,
                    width = tierW,
                    height = tierHeight,
                    isVipBalcony = false
                });

                // Front safety railing on Tier 1
                if (t == 0 && railPrefab != null)
                {
                    float yRot = (lookInwardDir == Vector3.back) ? 180f :
                                 (lookInwardDir == Vector3.forward) ? 0f :
                                 (lookInwardDir == Vector3.left) ? 270f : 90f;
                    int railCount = Mathf.RoundToInt(tierW / 2.5f);
                    for (int ri = 0; ri < railCount; ri++)
                    {
                        float offset = -tierW * 0.5f + (ri + 0.5f) * 2.5f;
                        Vector3 rp = isZAxis ? new Vector3(startX + offset, tierHeight, startZ)
                                            : new Vector3(startX, tierHeight, startZ + offset);
                        var rail = Object.Instantiate(railPrefab, rp, Quaternion.Euler(0f, yRot, 0f), sideRoot.transform);
                        rail.name = "Front_Safety_Rail";
                    }
                }
            }

            // ─────────────────────────────────────────────────────────────────
            // B. VIP LUXURY SUITES MEZZANINE (Y = 12.5m to 16.5m, dist = 39m to 45m)
            // ─────────────────────────────────────────────────────────────────
            float vipDistStart = lowerTiers * lowerDepth;
            float vipDepth = 6.0f;
            float vipHeight = 4.2f;
            float vipY = lowerTiers * lowerRise + vipHeight * 0.5f;
            float vipW = baseWidth + lowerTiers * 4.2f + 4.0f;

            Vector3 vipPos = isZAxis ? new Vector3(startX, vipY, startZ + outwardsDir.z * (vipDistStart + vipDepth * 0.5f))
                                     : new Vector3(startX + outwardsDir.x * (vipDistStart + vipDepth * 0.5f), vipY, startZ);
            Vector3 vipScale = isZAxis ? new Vector3(vipW, vipHeight, vipDepth)
                                       : new Vector3(vipDepth, vipHeight, vipW);

            GameObject vipBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
            vipBox.name = "VIP_Executive_Suites_Box";
            vipBox.transform.SetParent(sideRoot.transform);
            vipBox.transform.position = vipPos;
            vipBox.transform.localScale = vipScale;
            if (vipGlowMat != null) vipBox.GetComponent<Renderer>().sharedMaterial = vipGlowMat;

            // VIP Viewing Glass Balcony Panels
            float glassY = lowerTiers * lowerRise + 2.1f;
            Vector3 glassPos = isZAxis ? new Vector3(startX, glassY, startZ + outwardsDir.z * (vipDistStart + 0.2f))
                                       : new Vector3(startX + outwardsDir.x * (vipDistStart + 0.2f), glassY, startZ);
            Vector3 glassScale = isZAxis ? new Vector3(vipW - 2.0f, 3.2f, 0.15f)
                                         : new Vector3(0.15f, 3.2f, vipW - 2.0f);

            GameObject glass = GameObject.CreatePrimitive(PrimitiveType.Cube);
            glass.name = "VIP_Smoked_Glass_Windows";
            glass.transform.SetParent(sideRoot.transform);
            glass.transform.position = glassPos;
            glass.transform.localScale = glassScale;
            if (glassMat != null) glass.GetComponent<Renderer>().sharedMaterial = glassMat;

            // 360-Degree Continuous Fascia Ribbon Board running along the VIP front apron!
            float ribbonY = lowerTiers * lowerRise + 0.9f;
            Vector3 ribbonPos = isZAxis ? new Vector3(startX, ribbonY, startZ + outwardsDir.z * (vipDistStart - 0.1f))
                                        : new Vector3(startX + outwardsDir.x * (vipDistStart - 0.1f), ribbonY, startZ);
            Vector3 ribbonScale = isZAxis ? new Vector3(vipW + 4.0f, 1.8f, 0.35f)
                                          : new Vector3(0.35f, 1.8f, vipW + 4.0f);

            GameObject ribbon = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ribbon.name = "Stadium_LED_Fascia_Ribbon_Ticker";
            ribbon.transform.SetParent(sideRoot.transform);
            ribbon.transform.position = ribbonPos;
            ribbon.transform.localScale = ribbonScale;
            ribbon.GetComponent<Renderer>().sharedMaterial = ribbonMat;

            // Record VIP Balcony tier for high-roller spectators
            _allBleacherTiers.Add(new GrandstandTierInfo
            {
                tierIndex = lowerTiers,
                center = isZAxis ? new Vector3(startX, lowerTiers * lowerRise + 0.05f, startZ + outwardsDir.z * (vipDistStart + 1.2f))
                                 : new Vector3(startX + outwardsDir.x * (vipDistStart + 1.2f), lowerTiers * lowerRise + 0.05f, startZ),
                forwardDir = lookInwardDir,
                width = vipW * 0.9f,
                height = lowerTiers * lowerRise,
                isVipBalcony = true
            });

            // ─────────────────────────────────────────────────────────────────
            // C. UPPER DECK SKY BOWL (Tiers 12 to 18: Y = 17m to 26m, dist = 46m to 60m)
            // ─────────────────────────────────────────────────────────────────
            int upperTiers = 7;
            float upperDepth = 2.0f;
            float upperRise = 1.35f; // Steeper angle for upper deck!
            float upperBaseDist = vipDistStart + vipDepth;
            float upperBaseH = lowerTiers * lowerRise + vipHeight;

            for (int u = 0; u < upperTiers; u++)
            {
                float uDist = upperBaseDist + (u + 0.5f) * upperDepth;
                float uH = upperBaseH + (u + 1) * upperRise;
                float uW = vipW + u * 3.5f;

                Vector3 uPos = isZAxis ? new Vector3(startX, uH * 0.5f, startZ + outwardsDir.z * uDist)
                                       : new Vector3(startX + outwardsDir.x * uDist, uH * 0.5f, startZ);
                Vector3 uScale = isZAxis ? new Vector3(uW, uH, upperDepth)
                                         : new Vector3(upperDepth, uH, uW);

                GameObject uStep = GameObject.CreatePrimitive(PrimitiveType.Cube);
                uStep.name = $"UpperDeck_Tier_{u + 1}";
                uStep.transform.SetParent(sideRoot.transform);
                uStep.transform.position = uPos;
                uStep.transform.localScale = uScale;
                if (stepMat != null) uStep.GetComponent<Renderer>().sharedMaterial = stepMat;

                // Upper LED Strip
                float uLipY = uH + 0.03f;
                Vector3 uLedPos = isZAxis ? new Vector3(startX, uLipY, startZ + outwardsDir.z * (upperBaseDist + u * upperDepth))
                                          : new Vector3(startX + outwardsDir.x * (upperBaseDist + u * upperDepth), uLipY, startZ);
                Vector3 uLedScale = isZAxis ? new Vector3(uW, 0.06f, 0.10f)
                                            : new Vector3(0.10f, 0.06f, uW);

                GameObject uLed = GameObject.CreatePrimitive(PrimitiveType.Cube);
                uLed.name = $"UpperDeck_Tier_{u + 1}_LED";
                uLed.transform.SetParent(sideRoot.transform);
                uLed.transform.position = uLedPos;
                uLed.transform.localScale = uLedScale;
                uLed.GetComponent<Renderer>().sharedMaterial = (u % 2 == 0) ? ledCyanMat : ledAmberMat;
                Object.DestroyImmediate(uLed.GetComponent<Collider>());

                _allBleacherTiers.Add(new GrandstandTierInfo
                {
                    tierIndex = lowerTiers + 1 + u,
                    center = uPos + Vector3.up * (uH * 0.5f),
                    forwardDir = lookInwardDir,
                    width = uW,
                    height = uH,
                    isVipBalcony = false
                });
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // 6. SOUTH CONCOURSE FIGHTER ENTRANCE STAGE & GRAND RUNWAY RAMP
        // ─────────────────────────────────────────────────────────────────────
        private static void BuildFighterEntranceStageAndRunway(Transform parent)
        {
            GameObject entranceRoot = new GameObject("Fighter_Entrance_Stage_And_Runway");
            entranceRoot.transform.SetParent(parent);

            var litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            // Elevated Entrance Stage at South (-Z = 58m)
            float stageZ = -58.0f;
            float stageY = 2.4f;
            float stageW = 20.0f;
            float stageD = 10.0f;

            GameObject stage = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stage.name = "Fighter_Main_Entrance_Stage";
            stage.transform.SetParent(entranceRoot.transform);
            stage.transform.position = new Vector3(0f, stageY * 0.5f, stageZ);
            stage.transform.localScale = new Vector3(stageW, stageY, stageD);

            var stageMat = new Material(litShader);
            stageMat.color = new Color(0.08f, 0.09f, 0.12f);
            stage.GetComponent<Renderer>().sharedMaterial = stageMat;

            // Colossal Entrance Video Screen Arch (16m wide x 10m high)
            var screenMat = new Material(litShader);
            screenMat.color = new Color(0.02f, 0.04f, 0.10f);
            screenMat.EnableKeyword("_EMISSION");
            screenMat.SetColor("_EmissionColor", new Color(0.10f, 0.85f, 1.0f) * 4.5f);

            GameObject screen = GameObject.CreatePrimitive(PrimitiveType.Cube);
            screen.name = "Fighter_Entrance_Video_Wall";
            screen.transform.SetParent(entranceRoot.transform);
            screen.transform.position = new Vector3(0f, stageY + 5.5f, stageZ - 2.5f);
            screen.transform.localScale = new Vector3(16.0f, 9.0f, 0.35f);
            screen.GetComponent<Renderer>().sharedMaterial = screenMat;

            // Runway Ramp connecting Entrance Stage (Z = -53m) down to Ringside Barricades (Z = -31.0f)
            float rampStart = stageZ + stageD * 0.5f; // -53m
            float rampEnd = -31.0f;
            float rampLen = Mathf.Abs(rampEnd - rampStart);
            float rampMidZ = (rampStart + rampEnd) * 0.5f;
            float rampSlopeY = stageY * 0.5f;

            GameObject ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = "Fighter_Entrance_Runway_Ramp";
            ramp.transform.SetParent(entranceRoot.transform);
            ramp.transform.position = new Vector3(0f, rampSlopeY, rampMidZ);
            ramp.transform.localScale = new Vector3(4.2f, 0.25f, rampLen);
            ramp.transform.rotation = Quaternion.Euler(-Mathf.Atan2(stageY, rampLen) * Mathf.Rad2Deg, 0f, 0f);

            var rampMat = new Material(litShader);
            rampMat.color = new Color(0.10f, 0.11f, 0.14f);
            ramp.GetComponent<Renderer>().sharedMaterial = rampMat;

            // Runway Flanking Glowing Neon Guidance Strips
            var runwayNeonMat = new Material(litShader);
            runwayNeonMat.color = new Color(0.02f, 0.08f, 0.15f);
            runwayNeonMat.EnableKeyword("_EMISSION");
            runwayNeonMat.SetColor("_EmissionColor", new Color(0f, 0.95f, 1f) * 4.0f);

            GameObject stripL = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stripL.name = "Runway_Neon_Strip_Left";
            stripL.transform.SetParent(entranceRoot.transform);
            stripL.transform.position = new Vector3(-2.2f, rampSlopeY + 0.15f, rampMidZ);
            stripL.transform.localScale = new Vector3(0.15f, 0.15f, rampLen);
            stripL.transform.rotation = ramp.transform.rotation;
            stripL.GetComponent<Renderer>().sharedMaterial = runwayNeonMat;

            GameObject stripR = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stripR.name = "Runway_Neon_Strip_Right";
            stripR.transform.SetParent(entranceRoot.transform);
            stripR.transform.position = new Vector3(2.2f, rampSlopeY + 0.15f, rampMidZ);
            stripR.transform.localScale = new Vector3(0.15f, 0.15f, rampLen);
            stripR.transform.rotation = ramp.transform.rotation;
            stripR.GetComponent<Renderer>().sharedMaterial = runwayNeonMat;
        }

        // ─────────────────────────────────────────────────────────────────────
        // 7. CENTER-HUNG OVERHEAD MEGA JUMBOTRON SCOREBOARD RIG (Y = 13.5m)
        // ─────────────────────────────────────────────────────────────────────
        private static void BuildCenterHungJumbotronAndTrusses(Transform parent)
        {
            GameObject rigRoot = new GameObject("Arena_CenterHung_Jumbotron_Rig");
            rigRoot.transform.SetParent(parent);

            var litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var wallMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/PolygonCyberCity/Materials/Building/Wall_01_Triplanar.mat");

            // --- Hanging Jumbotron (18m wide x 7.5m high, suspended at Y = 18.0m) ---
            GameObject jumbotron = new GameObject("Center_Hanging_Jumbotron_Cube");
            jumbotron.transform.SetParent(rigRoot.transform);
            jumbotron.transform.position = new Vector3(0f, 18.0f, 0f);

            float jSize = 18.0f;
            float jHeight = 7.5f;

            // Octagonal Housing Core
            GameObject jCore = GameObject.CreatePrimitive(PrimitiveType.Cube);
            jCore.name = "Jumbotron_Housing_Core";
            jCore.transform.SetParent(jumbotron.transform);
            jCore.transform.position = new Vector3(0f, 18.0f, 0f);
            jCore.transform.localScale = new Vector3(jSize, jHeight, jSize);
            if (wallMat != null) jCore.GetComponent<Renderer>().sharedMaterial = wallMat;

            // 4 Digital Widescreen Match Displays (North, South, East, West)
            var screenMat = new Material(litShader);
            screenMat.color = new Color(0.02f, 0.05f, 0.12f);
            screenMat.EnableKeyword("_EMISSION");
            screenMat.SetColor("_EmissionColor", new Color(0f, 0.70f, 0.95f) * 1.8f);

            float sThick = 0.15f;
            float sOffset = jSize * 0.5f + 0.12f;

            CreateJumbotronScreen(jumbotron.transform, "Screen_North", new Vector3(0f, 18.0f, sOffset), new Vector3(jSize - 1.2f, jHeight - 1.0f, sThick), screenMat);
            CreateJumbotronScreen(jumbotron.transform, "Screen_South", new Vector3(0f, 18.0f, -sOffset), new Vector3(jSize - 1.2f, jHeight - 1.0f, sThick), screenMat);
            CreateJumbotronScreen(jumbotron.transform, "Screen_East",  new Vector3(sOffset, 18.0f, 0f), new Vector3(sThick, jHeight - 1.0f, jSize - 1.2f), screenMat);
            CreateJumbotronScreen(jumbotron.transform, "Screen_West",  new Vector3(-sOffset, 18.0f, 0f), new Vector3(sThick, jHeight - 1.0f, jSize - 1.2f), screenMat);

            // Jumbotron Upper & Lower 360 Ribbon Tickers
            var tickerMat = new Material(litShader);
            tickerMat.color = new Color(0.05f, 0.02f, 0.08f);
            tickerMat.EnableKeyword("_EMISSION");
            tickerMat.SetColor("_EmissionColor", new Color(1f, 0.18f, 0.80f) * 1.8f);

            GameObject topTicker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            topTicker.name = "Jumbotron_Top_Ribbon_Ticker";
            topTicker.transform.SetParent(jumbotron.transform);
            topTicker.transform.position = new Vector3(0f, 21.6f, 0f);
            topTicker.transform.localScale = new Vector3(jSize + 0.8f, 0.65f, jSize + 0.8f);
            topTicker.GetComponent<Renderer>().sharedMaterial = tickerMat;

            GameObject botTicker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            botTicker.name = "Jumbotron_Bottom_Ribbon_Ticker";
            botTicker.transform.SetParent(jumbotron.transform);
            botTicker.transform.position = new Vector3(0f, 14.4f, 0f);
            botTicker.transform.localScale = new Vector3(jSize + 0.8f, 0.65f, jSize + 0.8f);
            botTicker.GetComponent<Renderer>().sharedMaterial = tickerMat;

            // Soft Center Downlight Spotlight
            var centerPointGo = new GameObject("Jumbotron_Downlight_Center");
            centerPointGo.transform.SetParent(jumbotron.transform);
            centerPointGo.transform.position = new Vector3(0f, 14.0f, 0f);
            var cpLight = centerPointGo.AddComponent<Light>();
            cpLight.type = LightType.Spot;
            cpLight.color = new Color(0.98f, 0.99f, 1.0f);
            cpLight.intensity = 6.0f;
            cpLight.range = 25.0f;
            cpLight.spotAngle = 80.0f;
            cpLight.innerSpotAngle = 45.0f;
            cpLight.shadows = LightShadows.Soft;

            // Rotating Holographic Dragon suspended inside/below the Jumbotron
            string dragonPath = "Assets/PolygonCyberCity/Prefabs/FX/Hologram_Dragon_01.prefab";
            var dragonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(dragonPath);
            if (dragonPrefab != null)
            {
                var dragon = Object.Instantiate(dragonPrefab, new Vector3(0f, 12.5f, 0f), Quaternion.Euler(0f, 45f, 0f), jumbotron.transform);
                dragon.name = "Jumbotron_Center_Holo_Dragon";
                dragon.transform.localScale = new Vector3(2.8f, 2.8f, 2.8f);
            }

            // Suspended Line-Array Acoustic Speaker Horns at 4 Jumbotron corners
            for (int k = 0; k < 4; k++)
            {
                float rotAngle = k * 90f + 45f;
                float dist = jSize * 0.55f;
                float rad = rotAngle * Mathf.Deg2Rad;
                Vector3 spPos = new Vector3(Mathf.Sin(rad) * dist, 15.0f, Mathf.Cos(rad) * dist);

                GameObject speaker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                speaker.name = $"Acoustic_LineArray_Cluster_{k + 1}";
                speaker.transform.SetParent(jumbotron.transform);
                speaker.transform.position = spPos;
                speaker.transform.rotation = Quaternion.Euler(20f, rotAngle, 0f);
                speaker.transform.localScale = new Vector3(1.4f, 2.6f, 0.9f);

                var spMat = new Material(litShader);
                spMat.color = new Color(0.08f, 0.08f, 0.10f);
                speaker.GetComponent<Renderer>().sharedMaterial = spMat;
            }
        }

        private static void CreateJumbotronScreen(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject screen = GameObject.CreatePrimitive(PrimitiveType.Cube);
            screen.name = name;
            screen.transform.SetParent(parent);
            screen.transform.position = pos;
            screen.transform.localScale = scale;
            screen.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(screen.GetComponent<Collider>());
        }

        // ─────────────────────────────────────────────────────────────────────
        // 8. OVERHEAD SPACE-FRAME CATWALKS & ROOF TRUSS GIRDERS
        // ─────────────────────────────────────────────────────────────────────
        private static void BuildOverheadTrussesAndCatwalks(Transform parent)
        {
            GameObject trussRoot = new GameObject("Overhead_Roof_Trusses_And_Catwalks");
            trussRoot.transform.SetParent(parent);

            var litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var trussMat = new Material(litShader);
            trussMat.color = new Color(0.12f, 0.14f, 0.18f);
            trussMat.SetFloat("_Metallic", 0.85f);
            trussMat.SetFloat("_Smoothness", 0.55f);

            float roofH = 36.0f;
            float trussSpan = 145.0f;

            // 4 Longitudinal Heavy Box Truss Beams (East-West)
            for (int i = -1; i <= 1; i += 2)
            {
                GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
                beam.name = $"Roof_Longitudinal_Truss_{i}";
                beam.transform.SetParent(trussRoot.transform);
                beam.transform.position = new Vector3(0f, roofH, i * 22f);
                beam.transform.localScale = new Vector3(trussSpan, 1.8f, 1.8f);
                beam.GetComponent<Renderer>().sharedMaterial = trussMat;

                // Aviation Hazard Beacon at center
                var beacon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                beacon.name = "Aviation_Warning_Beacon";
                beacon.transform.SetParent(beam.transform);
                beacon.transform.position = new Vector3(0f, roofH + 1.2f, i * 22f);
                beacon.transform.localScale = Vector3.one * 0.6f;
                var bMat = new Material(litShader);
                bMat.color = Color.red;
                bMat.EnableKeyword("_EMISSION");
                bMat.SetColor("_EmissionColor", Color.red * 4.0f);
                beacon.GetComponent<Renderer>().sharedMaterial = bMat;
            }

            // 4 Transverse Heavy Box Truss Beams (North-South)
            for (int i = -1; i <= 1; i += 2)
            {
                GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
                beam.name = $"Roof_Transverse_Truss_{i}";
                beam.transform.SetParent(trussRoot.transform);
                beam.transform.position = new Vector3(i * 22f, roofH, 0f);
                beam.transform.localScale = new Vector3(1.8f, 1.8f, trussSpan);
                beam.GetComponent<Renderer>().sharedMaterial = trussMat;
            }

            // Center Truss Catwalk Ring surrounding the Jumbotron (Y = 22m)
            float catwalkSize = 26.0f;
            CreateCatwalkSection(trussRoot.transform, "Catwalk_N", new Vector3(0f, 22.0f, catwalkSize * 0.5f), new Vector3(catwalkSize, 0.35f, 1.8f), trussMat);
            CreateCatwalkSection(trussRoot.transform, "Catwalk_S", new Vector3(0f, 22.0f, -catwalkSize * 0.5f), new Vector3(catwalkSize, 0.35f, 1.8f), trussMat);
            CreateCatwalkSection(trussRoot.transform, "Catwalk_E", new Vector3(catwalkSize * 0.5f, 22.0f, 0f), new Vector3(1.8f, 0.35f, catwalkSize), trussMat);
            CreateCatwalkSection(trussRoot.transform, "Catwalk_W", new Vector3(-catwalkSize * 0.5f, 22.0f, 0f), new Vector3(1.8f, 0.35f, catwalkSize), trussMat);
        }

        private static void CreateCatwalkSection(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject cw = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cw.name = name;
            cw.transform.SetParent(parent);
            cw.transform.position = pos;
            cw.transform.localScale = scale;
            cw.GetComponent<Renderer>().sharedMaterial = mat;
        }

        // ─────────────────────────────────────────────────────────────────────
        // 9. VOLUMETRIC SEARCHLIGHT BEAMS & TV BROADCAST DRONES
        // ─────────────────────────────────────────────────────────────────────
        private static void BuildVolumetricBeamsAndBroadcastDrones(Transform parent)
        {
            GameObject fxRoot = new GameObject("Arena_Volumetric_Lighting_And_Broadcast_Drones");
            fxRoot.transform.SetParent(parent);

            // 1. Volumetric Light Cones / God Rays (SM_LightRayRound_01.prefab)
            string lightRayPath = "Assets/PolygonCyberCity/Prefabs/FX/SM_LightRayRound_01.prefab";
            var rayPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(rayRayPath());
            if (rayPrefab != null)
            {
                // 4 Diagonal Searchlights from high catwalks down into the ring
                Vector3[] beamPositions = new[]
                {
                    new Vector3(18f, 24.0f, 18f),
                    new Vector3(-18f, 24.0f, 18f),
                    new Vector3(-18f, 24.0f, -18f),
                    new Vector3(18f, 24.0f, -18f)
                };

                for (int b = 0; b < beamPositions.Length; b++)
                {
                    Vector3 bp = beamPositions[b];
                    var rayGo = Object.Instantiate(rayPrefab, bp, Quaternion.identity, fxRoot.transform);
                    rayGo.name = $"Volumetric_Searchlight_Shaft_{b + 1}";
                    rayGo.transform.LookAt(Vector3.up * 0.85f);
                    rayGo.transform.localScale = new Vector3(2.5f, 2.5f, 4.5f);
                }
            }

            // 2. Hovering TV Broadcast Camera Drones (FX_Drone_01.prefab)
            string dronePrefabPath = "Assets/PolygonCyberCity/Prefabs/FX/FX_Drone_01.prefab";
            var dronePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(dronePrefabPath);
            if (dronePrefab != null)
            {
                Vector3[] dronePositions = new[]
                {
                    new Vector3(14.0f, 5.5f, 9.0f),
                    new Vector3(-14.0f, 6.5f, 8.0f),
                    new Vector3(11.0f, 7.5f, -12.0f),
                    new Vector3(-12.0f, 6.0f, -10.0f)
                };

                for (int d = 0; d < dronePositions.Length; d++)
                {
                    Vector3 dp = dronePositions[d];
                    var droneGo = Object.Instantiate(dronePrefab, dp, Quaternion.identity, fxRoot.transform);
                    droneGo.name = $"Broadcast_Camera_Drone_{d + 1}";
                    droneGo.transform.LookAt(Vector3.up * 1.2f);
                    droneGo.transform.localScale = Vector3.one * 1.5f;

                    // Dedicated downlight spotlight attached to the drone
                    var spotGo = new GameObject($"Drone_Spotlight_{d + 1}");
                    spotGo.transform.SetParent(droneGo.transform);
                    spotGo.transform.localPosition = Vector3.down * 0.2f;
                    spotGo.transform.LookAt(Vector3.up * 0.9f);

                    var spot = spotGo.AddComponent<Light>();
                    spot.type = LightType.Spot;
                    spot.color = new Color(0.85f, 0.95f, 1.0f);
                    spot.intensity = 18.0f;
                    spot.range = 22.0f;
                    spot.spotAngle = 40.0f;
                    spot.innerSpotAngle = 20.0f;
                }
            }
        }

        private static string rayRayPath() => "Assets/PolygonCyberCity/Prefabs/FX/SM_LightRayRound_01.prefab";

        // ─────────────────────────────────────────────────────────────────────
        // 10. PROFESSIONAL STADIUM BROADCAST LIGHTING RIG
        // ─────────────────────────────────────────────────────────────────────
        private static void BuildStadiumLighting(Transform parent)
        {
            GameObject lightRoot = new GameObject("Arena_Stadium_Lighting_Rig");
            lightRoot.transform.SetParent(parent);

            // Rich Stadium Ambient Fill (Moody deep twilight navy)
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.10f, 0.13f, 0.20f);

            // --- Primary Directional Stadium Sky Key (Broadcast Lighting, shadowCasting on roof disabled) ---
            var dirLightGo = new GameObject("Stadium_Overhead_Broadcast_KeyLight");
            dirLightGo.transform.SetParent(lightRoot.transform);
            dirLightGo.transform.rotation = Quaternion.Euler(58f, 32f, 0f);
            var dirLight = dirLightGo.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.color = new Color(0.96f, 0.98f, 1.0f);
            dirLight.intensity = 0.55f;
            dirLight.shadows = LightShadows.Soft;

            // --- Secondary Directional Stadium Rim Light (Cyan/Violet contrast) ---
            var dirRimGo = new GameObject("Stadium_Rim_Contrast_KeyLight");
            dirRimGo.transform.SetParent(lightRoot.transform);
            dirRimGo.transform.rotation = Quaternion.Euler(45f, 210f, 0f);
            var dirRim = dirRimGo.AddComponent<Light>();
            dirRim.type = LightType.Directional;
            dirRim.color = new Color(0.20f, 0.80f, 1.0f);
            dirRim.intensity = 0.20f;
            dirRim.shadows = LightShadows.None;

            // --- 12 Ring Canvas Downlights mounted on an overhead circular truss at Y = 18m ---
            float ringRadius = 24.0f;
            float ringH = 18.0f;

            for (int i = 0; i < 12; i++)
            {
                float angle = i * (360f / 12f) * Mathf.Deg2Rad;
                Vector3 spotPos = new Vector3(Mathf.Sin(angle) * ringRadius, ringH, Mathf.Cos(angle) * ringRadius);

                var spotGo = new GameObject($"Ring_Canvas_Spotlight_{i + 1}");
                spotGo.transform.SetParent(lightRoot.transform);
                spotGo.transform.position = spotPos;
                spotGo.transform.LookAt(Vector3.zero + Vector3.up * 0.85f);

                var spot = spotGo.AddComponent<Light>();
                spot.type = LightType.Spot;
                spot.color = (i % 2 == 0) ? new Color(0.98f, 0.99f, 1.0f) : new Color(0.40f, 0.95f, 1.0f);
                spot.intensity = 5.0f;
                spot.range = 35.0f;
                spot.spotAngle = 60.0f;
                spot.innerSpotAngle = 32.0f;
                spot.shadows = LightShadows.Soft;
            }

            // --- 8 Dedicated Grandstand Crowd Wash Lights ---
            (string sideName, Vector3[] lightPositions, Vector3 lookTarget)[] sideLights = new[]
            {
                ("NorthCrowd", new[] { new Vector3(-14f, 18f, 12f), new Vector3(14f, 18f, 12f) }, new Vector3(0f, 6.0f, 32f)),
                ("SouthCrowd", new[] { new Vector3(-14f, 18f, -12f), new Vector3(14f, 18f, -12f) }, new Vector3(0f, 6.0f, -32f)),
                ("EastCrowd",  new[] { new Vector3(12f, 18f, -14f), new Vector3(12f, 18f, 14f) }, new Vector3(32f, 6.0f, 0f)),
                ("WestCrowd",  new[] { new Vector3(-12f, 18f, -14f), new Vector3(-12f, 18f, 14f) }, new Vector3(-32f, 6.0f, 0f))
            };

            foreach (var side in sideLights)
            {
                for (int li = 0; li < side.lightPositions.Length; li++)
                {
                    var washGo = new GameObject($"Crowd_WashLight_{side.sideName}_{li + 1}");
                    washGo.transform.SetParent(lightRoot.transform);
                    washGo.transform.position = side.lightPositions[li];
                    washGo.transform.LookAt(side.lookTarget);

                    var spot = washGo.AddComponent<Light>();
                    spot.type = LightType.Spot;
                    spot.color = new Color(1.0f, 0.95f, 0.90f);
                    spot.intensity = 12.0f;
                    spot.range = 45.0f;
                    spot.spotAngle = 80.0f;
                    spot.innerSpotAngle = 45.0f;
                    spot.shadows = LightShadows.None;
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // 11. 550+ DIVERSE SPECTATORS ARRANGED ACROSS ALL GRANDSTAND TIERS
        // ─────────────────────────────────────────────────────────────────────
        private static Dictionary<ArenaSpectatorNPC.SpectatorCheerType, (RuntimeAnimatorController ctrl, AnimationClip clip)[]> EnsureSpectatorControllers()
        {
            var dict = new Dictionary<ArenaSpectatorNPC.SpectatorCheerType, (RuntimeAnimatorController, AnimationClip)[]>();

            var cheerDefs = new (ArenaSpectatorNPC.SpectatorCheerType type, string femnFbx, string mascFbx)[]
            {
                (
                    ArenaSpectatorNPC.SpectatorCheerType.DoubleWaveCheer,
                    "Assets/Synty/AnimationIdles/Animations/Polygon/Feminine/Wave/Stances/A_POLY_IDL_Wave_Double_Loop_Femn.fbx",
                    "Assets/Synty/AnimationIdles/Animations/Polygon/Masculine/Wave/Stances/A_POLY_IDL_Wave_Double_Loop_Masc.fbx"
                ),
                (
                    ArenaSpectatorNPC.SpectatorCheerType.FistPumpCheer,
                    "Assets/Synty/AnimationIdles/Animations/Polygon/Feminine/Wave/Stances/A_POLY_IDL_Wave_Large_Loop_Femn.fbx",
                    "Assets/Synty/AnimationIdles/Animations/Polygon/Masculine/Wave/Stances/A_POLY_IDL_Wave_Large_Loop_Masc.fbx"
                ),
                (
                    ArenaSpectatorNPC.SpectatorCheerType.SmallWaveCheer,
                    "Assets/Synty/AnimationIdles/Animations/Polygon/Feminine/Wave/Stances/A_POLY_IDL_Wave_Small_Loop_Femn.fbx",
                    "Assets/Synty/AnimationIdles/Animations/Polygon/Masculine/Wave/Stances/A_POLY_IDL_Wave_Small_Loop_Masc.fbx"
                ),
                (
                    ArenaSpectatorNPC.SpectatorCheerType.AggressiveHype,
                    "Assets/Synty/AnimationIdles/Animations/Polygon/Feminine/Posture/Stances/A_POLY_IDL_Posture_Aggressive_Loop_Femn.fbx",
                    "Assets/Synty/AnimationIdles/Animations/Polygon/Masculine/Posture/Stances/A_POLY_IDL_Posture_Aggressive_Loop_Masc.fbx"
                ),
                (
                    ArenaSpectatorNPC.SpectatorCheerType.DrinkWatching,
                    "Assets/Synty/AnimationIdles/Animations/Polygon/Feminine/Drink/Stances/A_POLY_IDL_Drink_Hold_Loop_Femn.fbx",
                    "Assets/Synty/AnimationIdles/Animations/Polygon/Masculine/Drink/Stances/A_POLY_IDL_Drink_Hold_Loop_Masc.fbx"
                ),
                (
                    ArenaSpectatorNPC.SpectatorCheerType.RailingLean,
                    "Assets/Synty/AnimationIdles/Animations/Polygon/Feminine/Lean/Stances/ArmsFolded/A_POLY_IDL_Lean_B_ArmsFolded_Loop_Femn.fbx",
                    "Assets/Synty/AnimationIdles/Animations/Polygon/Masculine/Lean/Stances/ArmsFolded/A_POLY_IDL_Lean_B_ArmsFolded_Loop_Masc.fbx"
                ),
                (
                    ArenaSpectatorNPC.SpectatorCheerType.VideoRecording,
                    "Assets/Synty/AnimationIdles/Animations/Polygon/Feminine/UsePhone/Stances/Video/A_POLY_IDL_UsePhone_OneHand_Video_Loop_Femn.fbx",
                    "Assets/Synty/AnimationIdles/Animations/Polygon/Masculine/UsePhone/Stances/Video/A_POLY_IDL_UsePhone_OneHand_Video_Loop_Masc.fbx"
                )
            };

            foreach (var def in cheerDefs)
            {
                string femnPath = $"{ControllerDir}/AC_Spectator_{def.type}_Femn.controller";
                string mascPath = $"{ControllerDir}/AC_Spectator_{def.type}_Masc.controller";

                var femnClip = LoadClipFromFbx(def.femnFbx);
                var mascClip = LoadClipFromFbx(def.mascFbx);

                var femnCtrl = BuildSingleClipController(femnPath, femnClip);
                var mascCtrl = BuildSingleClipController(mascPath, mascClip);

                dict[def.type] = new (RuntimeAnimatorController, AnimationClip)[]
                {
                    (femnCtrl, femnClip),
                    (mascCtrl, mascClip)
                };
            }

            return dict;
        }

        private static RuntimeAnimatorController BuildSingleClipController(string controllerPath, AnimationClip clip)
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (existing != null) return existing;

            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            if (clip != null)
            {
                var state = ctrl.layers[0].stateMachine.AddState("CheeringLoop");
                state.motion = clip;
                ctrl.layers[0].stateMachine.defaultState = state;
            }
            return ctrl;
        }

        private static AnimationClip LoadClipFromFbx(string fbxPath)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            if (assets == null) return null;
            foreach (var a in assets)
            {
                if (a is AnimationClip c && !c.name.StartsWith("__preview__")) return c;
            }
            return null;
        }

        public static void SpawnCrowdForSpawner(Transform parent)
        {
            if (!Application.isPlaying) return; // Absolutely prevent serializing spectators into scene file YAML!
            var controllers = EnsureSpectatorControllers();
            BuildSpectatorCrowd(parent, controllers);
        }

        private static GameObject BuildSpectatorCrowd(
            Transform parent,
            Dictionary<ArenaSpectatorNPC.SpectatorCheerType, (RuntimeAnimatorController ctrl, AnimationClip clip)[]> controllers)
        {
            GameObject crowdRoot = new GameObject("Spectator_Crowd_Stadium_Bleachers");
            crowdRoot.transform.SetParent(parent);
            crowdRoot.hideFlags = HideFlags.DontSave;

            string[] characterPrefabs = new string[]
            {
                "Assets/PolygonCyberCity/Prefabs/Characters/SM_Chr_Mercenary_Female_01.prefab",
                "Assets/PolygonCyberCity/Prefabs/Characters/SM_Chr_Mercenary_Male_01.prefab",
                "Assets/PolygonCyberCity/Prefabs/Characters/SM_Chr_Cyborg_Female_01.prefab",
                "Assets/PolygonCyberCity/Prefabs/Characters/SM_Chr_Cyborg_Male_01.prefab",
                "Assets/PolygonCyberCity/Prefabs/Characters/SM_Chr_Female_Soldier_01.prefab",
                "Assets/PolygonCyberCity/Prefabs/Characters/SM_Chr_Male_Soldier_01.prefab",
                "Assets/PolygonCyberCity/Prefabs/Characters/SM_Chr_Rich_Female_01.prefab",
                "Assets/PolygonCyberCity/Prefabs/Characters/SM_Chr_Rich_Male_01.prefab",
                "Assets/PolygonCyberCity/Prefabs/Characters/SM_Chr_Street_Male_01.prefab",
                "Assets/PolygonCyberCity/Prefabs/Characters/SM_Chr_Robot_Female_01.prefab",
                "Assets/PolygonCyberCity/Prefabs/Characters/SM_Chr_Robot_Mercenary_01.prefab",
                "Assets/PolygonCyberCity/Prefabs/Characters/SM_Chr_Fancy_Bot_01.prefab"
            };

            AudioClip[] chatterClips = new AudioClip[]
            {
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/NPC_Chatter/npc_civilian_murmur_01.wav"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/NPC_Chatter/npc_civilian_murmur_02.wav"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/NPC_Chatter/npc_civilian_murmur_03.wav"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/NPC_Chatter/npc_merc_grunt_01.wav"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/NPC_Chatter/npc_merc_grunt_02.wav")
            };

            int globalNpcCount = 0;
            int maxCrowd = 88;

            // Populate front tiers overlooking the 56m combat ring (Tiers 0 and 1)
            foreach (var tier in _allBleacherTiers)
            {
                if (tier.tierIndex > 1) continue; // Front 2 tiers for unobstructed view and zero CPU lag!
                if (globalNpcCount >= maxCrowd) break;

                bool isZAxis = Mathf.Abs(tier.forwardDir.z) > 0.5f;
                float halfW = tier.width * 0.44f;
                float spacing = 2.4f;

                for (float offset = -halfW; offset <= halfW; offset += spacing)
                {
                    if (globalNpcCount >= maxCrowd) break;
                    // Leave aisle openings
                    if (Mathf.Abs(offset) < 1.4f || Mathf.Abs(Mathf.Abs(offset) - halfW * 0.5f) < 1.0f) continue;

                    float stepOffsetTowardRing = (tier.tierIndex == 0) ? 0.55f : 0f;
                    float jitterX = isZAxis ? offset + Random.Range(-0.08f, 0.08f) : (tier.center.x + tier.forwardDir.x * stepOffsetTowardRing);
                    float jitterZ = isZAxis ? (tier.center.z + tier.forwardDir.z * stepOffsetTowardRing) : offset + Random.Range(-0.08f, 0.08f);
                    Vector3 npcPos = new Vector3(jitterX, tier.center.y, jitterZ);

                    string prefabPath = characterPrefabs[globalNpcCount % characterPrefabs.Length];
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    if (prefab == null) continue;

                    // Face inward toward the center arena ring with slight natural variation
                    Vector3 lookTarget = Vector3.zero + new Vector3(Random.Range(-2.5f, 2.5f), 0.85f, Random.Range(-2.5f, 2.5f));
                    Vector3 faceDir = (lookTarget - npcPos).normalized;
                    faceDir.y = 0f;
                    Quaternion rot = Quaternion.LookRotation(faceDir);

                    var npc = Object.Instantiate(prefab, npcPos, rot, crowdRoot.transform);
                    npc.name = $"Spectator_T{tier.tierIndex + 1}_{globalNpcCount + 1:D3}_{prefab.name}";

                    // Remove combat components
                    var insaneCtrl = npc.GetComponent<InsaneGunSwordController>();
                    if (insaneCtrl != null) Object.DestroyImmediate(insaneCtrl);
                    var charCtrl = npc.GetComponent<CharacterController>();
                    if (charCtrl != null) charCtrl.enabled = false;

                    // Assign Cheer Persona & Animation Controller
                    ArenaSpectatorNPC.SpectatorCheerType cheer;
                    if (tier.tierIndex == 0)
                    {
                        var frontCheers = new[] {
                            ArenaSpectatorNPC.SpectatorCheerType.RailingLean,
                            ArenaSpectatorNPC.SpectatorCheerType.FistPumpCheer,
                            ArenaSpectatorNPC.SpectatorCheerType.VideoRecording,
                            ArenaSpectatorNPC.SpectatorCheerType.AggressiveHype
                        };
                        cheer = frontCheers[globalNpcCount % frontCheers.Length];
                    }
                    else
                    {
                        var generalCheers = new[] {
                            ArenaSpectatorNPC.SpectatorCheerType.DoubleWaveCheer,
                            ArenaSpectatorNPC.SpectatorCheerType.FistPumpCheer,
                            ArenaSpectatorNPC.SpectatorCheerType.AggressiveHype,
                            ArenaSpectatorNPC.SpectatorCheerType.DrinkWatching,
                            ArenaSpectatorNPC.SpectatorCheerType.SmallWaveCheer,
                            ArenaSpectatorNPC.SpectatorCheerType.VideoRecording
                        };
                        cheer = generalCheers[globalNpcCount % generalCheers.Length];
                    }

                    bool isFemn = prefab.name.ToLower().Contains("female");

                    var anim = npc.GetComponent<Animator>();
                    if (anim == null) anim = npc.AddComponent<Animator>();
                    anim.applyRootMotion = false;
                    anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms; // ZERO CPU waste when offscreen or occluded!

                    if (controllers.TryGetValue(cheer, out var pair) && pair.Length >= 2)
                    {
                        var selected = isFemn ? pair[0] : pair[1];
                        anim.runtimeAnimatorController = selected.ctrl;

                        // EDIT-MODE POSE SAMPLING: Pose skeleton immediately in the editor so NO T-POSES!
                        if (selected.clip != null)
                        {
                            Vector3 targetPos = npcPos;
                            Quaternion targetRot = rot;

                            float sampleT = Random.Range(0.2f, Mathf.Max(0.5f, selected.clip.length - 0.2f));
                            selected.clip.SampleAnimation(npc, sampleT);

                            npc.transform.position = targetPos;
                            npc.transform.rotation = targetRot;
                        }
                    }

                    // Add Spectator NPC Behaviour Script
                    var specComp = npc.AddComponent<ArenaSpectatorNPC>();
                    specComp.cheerType = cheer;
                    specComp.chatterClips = chatterClips;
                    specComp.lookAtArenaCenter = true;

                    globalNpcCount++;
                }
            }

            SetDontSaveRecursive(crowdRoot);
            return crowdRoot;
        }

        private static void SetDontSaveRecursive(GameObject go)
        {
            if (go == null) return;
            go.hideFlags = HideFlags.DontSave;
            var comps = go.GetComponents<Component>();
            foreach (var c in comps)
            {
                if (c != null) c.hideFlags = HideFlags.DontSave;
            }
            for (int i = 0; i < go.transform.childCount; i++)
            {
                SetDontSaveRecursive(go.transform.GetChild(i).gameObject);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // 12. SPAWN COMBATANTS (ALITA + ENEMY DUELIST)
        // ─────────────────────────────────────────────────────────────────────
        private static (InsaneGunSwordController player, InsaneGunSwordController rival) SpawnCombatants(Transform parent)
        {
            float canvasY = 0.86f;

            // 1. Alita Battle Angel Player Combatant
            string playerPrefabPath = "Assets/Prefabs/Alita_BattleAngel_Hero.prefab";
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(playerPrefabPath);
            if (playerPrefab == null)
            {
                Debug.LogError("[CyberpunkArena] Could not find Alita player prefab!");
                return (null, null);
            }

            var playerGo = Object.Instantiate(playerPrefab, new Vector3(0f, canvasY, -10.0f), Quaternion.identity, parent);
            playerGo.name = "Alita_BattleAngel_Hero";
            playerGo.tag = "Player";

            var playerCtrl = playerGo.GetComponent<InsaneGunSwordController>();
            if (playerCtrl == null) playerCtrl = playerGo.AddComponent<InsaneGunSwordController>();
            playerCtrl.controlMode = InsaneGunSwordController.ControlMode.PlayerControlled;
            playerCtrl.maxHealth = 400f;
            playerCtrl.currentHealth = 400f;

            string playerAnimCtrlPath = "Assets/InsaneGun_Sword_Set/AC_InsaneGun_CombatMaster.controller";
            var pAnim = playerGo.GetComponent<Animator>();
            if (pAnim != null) pAnim.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(playerAnimCtrlPath);

            var idleClip = LoadClipFromFbx("Assets/InsaneGun_Sword_Set/Animation/Humanoid/idle.fbx");
            if (idleClip != null)
            {
                Vector3 pP = playerGo.transform.position;
                Quaternion pR = playerGo.transform.rotation;
                idleClip.SampleAnimation(playerGo, 0.15f);
                playerGo.transform.position = pP;
                playerGo.transform.rotation = pR;
            }

            // Damascus Blade Grip Transform Setup
            string swordPrefabPath = "Assets/CombatGirlsCharacterPack/CombatGirls_DualKatana/Prefab/Weapon_Katana_R.prefab";
            var swordPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(swordPrefabPath);
            if (swordPrefab != null)
            {
                var anim = playerGo.GetComponent<Animator>();
                Transform rightHand = (anim != null) ? anim.GetBoneTransform(HumanBodyBones.RightHand) : null;
                if (rightHand != null)
                {
                    var blade = Object.Instantiate(swordPrefab, rightHand);
                    blade.name = "Alita_Damascus_Blade";
                    blade.transform.localPosition = new Vector3(0.11780f, 0.03410f, -0.00920f);
                    blade.transform.localEulerAngles = new Vector3(341.3956f, 99.6629f, 276.9453f);
                    blade.transform.localScale = Vector3.one * 1.05f;

                    // Sleek Tron Katana Blade Material (Balanced Cyan Glow)
                    var mr = blade.GetComponentInChildren<MeshRenderer>();
                    if (mr != null)
                    {
                        var bladeMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                        bladeMat.name = "M_Tron_Katana_Alita";
                        bladeMat.SetColor("_BaseColor", new Color(0.04f, 0.08f, 0.12f, 1f));
                        bladeMat.SetFloat("_Metallic", 0.95f);
                        bladeMat.SetFloat("_Smoothness", 0.88f);
                        bladeMat.EnableKeyword("_EMISSION");
                        bladeMat.SetColor("_EmissionColor", new Color(0f, 1.6f, 2.2f, 1f)); // Balanced Tron Cyan Glow
                        mr.material = bladeMat;
                    }

                    // Subtle Point Light casting soft cyan ambiance around Alita's weapon
                    var bladeLight = new GameObject("Tron_Blade_Neon_Light");
                    bladeLight.transform.SetParent(blade.transform, false);
                    bladeLight.transform.localPosition = new Vector3(0f, 0f, -0.55f);
                    var light = bladeLight.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.color = new Color(0f, 0.95f, 1.0f);
                    light.range = 1.8f;
                    light.intensity = 0.65f;

                    // Clean, single Tron Blade Edge Trail (avoids overlapping double-brightness)
                    var trailGo = new GameObject("Tron_Blade_Edge_Trail");
                    trailGo.transform.SetParent(blade.transform, false);
                    trailGo.transform.localPosition = new Vector3(0f, 0f, -0.85f);
                    var trail = trailGo.AddComponent<TrailRenderer>();
                    AAACyberCombatVFX.ConfigureBladeTrail(trail, true);
                    trail.emitting = false;

                    playerCtrl.swordTrail = trail;
                    playerCtrl.swordInstance = blade.transform;
                }
            }

            // 2. Rival Cyber Duelist
            string rivalPrefabPath = "Assets/PolygonCyberCity/Prefabs/Characters/SM_Chr_Mercenary_Male_01.prefab";
            var rivalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(rivalPrefabPath);
            if (rivalPrefab != null)
            {
                var rivalGo = Object.Instantiate(rivalPrefab, new Vector3(0f, canvasY, 10.0f), Quaternion.Euler(0f, 180f, 0f), parent);
                rivalGo.name = "Rival_Cyber_Duelist";

                var rivalCtrl = rivalGo.GetComponent<InsaneGunSwordController>();
                if (rivalCtrl == null) rivalCtrl = rivalGo.AddComponent<InsaneGunSwordController>();
                rivalCtrl.controlMode = InsaneGunSwordController.ControlMode.AICirclingEnemy;
                rivalCtrl.maxHealth = 300f;
                rivalCtrl.currentHealth = 300f;

                string animCtrlPath = "Assets/InsaneGun_Sword_Set/AC_InsaneGun_Sidekick_Boy.controller";
                var anim = rivalGo.GetComponent<Animator>();
                if (anim != null) anim.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(animCtrlPath);

                // Equip Rival with Cyber Katana & Tron Amber Blade Trail
                Transform rivalRightHand = (anim != null) ? anim.GetBoneTransform(HumanBodyBones.RightHand) : null;
                if (rivalRightHand != null && swordPrefab != null)
                {
                    var rBlade = Object.Instantiate(swordPrefab, rivalRightHand);
                    rBlade.name = "Rival_Cyber_Katana";
                    rBlade.transform.localPosition = new Vector3(0.11780f, 0.03410f, -0.00920f);
                    rBlade.transform.localEulerAngles = new Vector3(341.3956f, 99.6629f, 276.9453f);
                    rBlade.transform.localScale = Vector3.one * 1.05f;

                    // Sleek Tron Katana Blade Material (Balanced Amber Glow)
                    var rMr = rBlade.GetComponentInChildren<MeshRenderer>();
                    if (rMr != null)
                    {
                        var rBladeMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                        rBladeMat.name = "M_Tron_Katana_Rival";
                        rBladeMat.SetColor("_BaseColor", new Color(0.12f, 0.06f, 0.02f, 1f));
                        rBladeMat.SetFloat("_Metallic", 0.95f);
                        rBladeMat.SetFloat("_Smoothness", 0.88f);
                        rBladeMat.EnableKeyword("_EMISSION");
                        rBladeMat.SetColor("_EmissionColor", new Color(2.4f, 0.9f, 0.05f, 1f)); // Balanced Tron Amber Glow
                        rMr.material = rBladeMat;
                    }

                    // Dynamic Point Light casting subtle amber illumination
                    var rBladeLight = new GameObject("Tron_Blade_Neon_Light");
                    rBladeLight.transform.SetParent(rBlade.transform, false);
                    rBladeLight.transform.localPosition = new Vector3(0f, 0f, -0.55f);
                    var rLight = rBladeLight.AddComponent<Light>();
                    rLight.type = LightType.Point;
                    rLight.color = new Color(1.0f, 0.45f, 0.05f);
                    rLight.range = 1.8f;
                    rLight.intensity = 0.65f;

                    // Clean, single Tron Blade Edge Trail
                    var rTrailGo = new GameObject("Tron_Blade_Edge_Trail");
                    rTrailGo.transform.SetParent(rBlade.transform, false);
                    rTrailGo.transform.localPosition = new Vector3(0f, 0f, -0.85f);
                    var rTrail = rTrailGo.AddComponent<TrailRenderer>();
                    AAACyberCombatVFX.ConfigureBladeTrail(rTrail, false);
                    rTrail.emitting = false;

                    rivalCtrl.swordTrail = rTrail;
                    rivalCtrl.swordInstance = rBlade.transform;
                }

                if (idleClip != null)
                {
                    Vector3 rP = rivalGo.transform.position;
                    Quaternion rR = rivalGo.transform.rotation;
                    idleClip.SampleAnimation(rivalGo, 0.25f);
                    rivalGo.transform.position = rP;
                    rivalGo.transform.rotation = rR;
                }

                return (playerCtrl, rivalCtrl);
            }

            return (playerCtrl, null);
        }

        private static void ConnectSpectatorsToCombat(GameObject crowdRoot, Transform rival)
        {
            if (crowdRoot == null) return;
            var spectators = crowdRoot.GetComponentsInChildren<ArenaSpectatorNPC>();
            foreach (var s in spectators)
            {
                s.SetArenaFocus(Vector3.zero, rival);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // 13. INDOOR ORBIT CAMERA SETUP
        // ─────────────────────────────────────────────────────────────────────
        private static void SetupIndoorCamera(Transform playerTarget, Transform parent)
        {
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                var camGo = new GameObject("MainCamera");
                mainCam = camGo.AddComponent<Camera>();
                camGo.tag = "MainCamera";
                camGo.transform.SetParent(parent);
            }

            mainCam.fieldOfView = 65f;
            mainCam.transform.position = new Vector3(0f, 4.0f, -17.5f);
            mainCam.transform.LookAt(playerTarget.position + Vector3.up * 1.45f);

            var orbit = mainCam.GetComponent<SmoothOrbitCamera>();
            if (orbit == null) orbit = mainCam.gameObject.AddComponent<SmoothOrbitCamera>();
            orbit.target = playerTarget;
            orbit.tightPositionFollow = true;
            orbit.targetOffset = new Vector3(0f, 1.45f, 0f);
            orbit.cameraOffset = new Vector3(0f, 0.40f, -6.5f);
            orbit.minDistance = 2.5f;
            orbit.maxDistance = 20.0f;

            var uad = mainCam.GetComponent<UniversalAdditionalCameraData>();
            if (uad == null) uad = mainCam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            uad.renderPostProcessing = true;
            uad.antialiasing = AntialiasingMode.None;
        }

        // ─────────────────────────────────────────────────────────────────────
        // 14. ARENA COMBAT MANAGER & BGM AUDIO
        // ─────────────────────────────────────────────────────────────────────
        private static void SetupArenaManager(InsaneGunSwordController player, InsaneGunSwordController rival, Transform parent)
        {
            var mgrGo = new GameObject("Arena_Combat_Manager");
            mgrGo.transform.SetParent(parent);

            var testMgr = mgrGo.AddComponent<InsaneGunCombatTestManager>();
            testMgr.player = player;
            testMgr.enemy = rival;
            testMgr.enemySpawnPos = new Vector3(0f, 0.86f, 10.0f);
            testMgr.enemySpawnRot = Quaternion.Euler(0f, 180f, 0f);

            var bgmClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/Across_The_Spider_Verse_Intro.mp3")
                       ?? AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/Cyberpunk_Combat_Metal_Heart.mp3")
                       ?? AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/paulyudin-intense-intense-music-574004.mp3");
            if (bgmClip != null)
            {
                var audio = mgrGo.AddComponent<AudioSource>();
                audio.clip = bgmClip;
                audio.volume = 0.55f;
                audio.loop = true;
                audio.playOnAwake = true;
                audio.Play();
            }
        }
    }
}
