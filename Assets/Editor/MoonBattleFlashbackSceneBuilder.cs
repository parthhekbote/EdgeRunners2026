using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;
using Alita;

namespace InsaneGun.Editor
{
    public static class MoonBattleFlashbackSceneBuilder
    {
        [MenuItem("Cyberpunk/Build Moon Battle Flashback Scene")]
        public static void BuildMoonBattleScene()
        {
            Debug.Log("[MoonBattle] Assembling 52-second Moon Battle Flashback Cutscene...");

            // ── 1. Configure Lunar Atmosphere & Lighting ─────────────────────────────
            ConfigureLunarAtmosphere();

            // ── 2. Configure Colossal Earth in the Lunar Sky ─────────────────────────
            Transform earth = SetupEarthInSky();

            // ── 3. Clean Alien Flora (Preserve Desolate Lunar Surface) ───────────────
            CleanAlienFlora();

            // ── 4. Setup Root Containers ─────────────────────────────────────────────
            Transform battleRoot = GetOrCreateRoot("[Moon_Combat_Encounter]");

            Vector3 center = new Vector3(172f, 13.0f, 10f); // Lunar crater ridge by rover

            // ── 5. Setup Alita, Commander Gelda & URM Vanguard Squad ────────────────
            SetupHeroAndSquad(battleRoot, center, out GameObject alitaGo, out GameObject geldaGo, out List<GameObject> squadList);

            // ── 6. Setup Hostile Defense Soldiers & Fortified Barricades ────────────
            SetupEnemyDefenseSquad(battleRoot, center + new Vector3(16.5f, 0.35f, 10.5f), out List<GameObject> enemyList);

            // ── 7. Setup Star Wars Space Fleet, Assault Gunships & Dogfights ─────────
            SetupStarWarsFleetAndHorizonCombat(battleRoot, center, out List<GameObject> starfighters, out List<GameObject> interceptors, out GameObject gunship, out GameObject cannon, out GameObject titan, out List<LineRenderer> dogfightLasers);

            // ── 8. Setup Combat VFX ──────────────────────────────────────────────────
            SetupCombatVFX(battleRoot, center, out GameObject tracerRoot, out GameObject muzzleRoot);

            // ── 9. Setup Director & Audio ────────────────────────────────────────────
            SetupDirector(center, alitaGo, geldaGo, squadList, enemyList, earth, tracerRoot, muzzleRoot, starfighters, interceptors, gunship, cannon, titan, dogfightLasers);

            AssetDatabase.SaveAssets();
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            Debug.Log("[MoonBattle] Successfully assembled Moon Battle Flashback Cutscene with Star Wars Fleet & Visceral Combat!");
        }

        private static Transform GetOrCreateRoot(string name)
        {
            var go = GameObject.Find(name);
            if (go != null)
            {
                Object.DestroyImmediate(go);
            }
            go = new GameObject(name);
            go.transform.position = Vector3.zero;
            return go.transform;
        }

        private static void ConfigureLunarAtmosphere()
        {
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.18f, 0.20f, 0.25f, 1.0f);

            // Dark Charcoal Lunar Regolith Materials
            var matExp = AssetDatabase.LoadAssetAtPath<Material>("Assets/Synty/PolygonSciFiWorlds/Materials/Environment/PolygonSciFiWorlds_Env_Triplanar_Exp_01.mat");
            if (matExp != null)
            {
                matExp.SetColor("_Ground_Color", new Color(0.48f, 0.48f, 0.52f, 1.0f));
                matExp.SetColor("_Rock_Color", new Color(0.38f, 0.38f, 0.42f, 1.0f));
                matExp.SetColor("_Road_Color", new Color(0.30f, 0.30f, 0.34f, 1.0f));
            }

            var matGrass = AssetDatabase.LoadAssetAtPath<Material>("Assets/Synty/PolygonSciFiWorlds/Materials/Environment/PolygonSciFiWorlds_Env_Triplanar_Grass_01.mat");
            if (matGrass != null)
            {
                matGrass.SetColor("_Ground_Color", new Color(0.46f, 0.46f, 0.50f, 1.0f));
                matGrass.SetColor("_Rock_Color", new Color(0.36f, 0.36f, 0.40f, 1.0f));
                matGrass.SetColor("_Road_Color", new Color(0.28f, 0.28f, 0.32f, 1.0f));
            }

            // Directional Light: Stark Lunar Sun
            var lightsRoot = GameObject.Find("Lights - Enable One");
            if (lightsRoot != null)
            {
                var urpObj = lightsRoot.transform.Find("URP");
                if (urpObj != null)
                {
                    urpObj.gameObject.SetActive(true);
                    var dirLightGO = urpObj.Find("Directional Light");
                    if (dirLightGO != null)
                    {
                        var dirLight = dirLightGO.GetComponent<Light>();
                        if (dirLight != null)
                        {
                            dirLight.enabled = true;
                            dirLight.color = new Color(1.0f, 0.98f, 0.95f, 1.0f);
                            dirLight.intensity = 2.85f;
                            dirLight.shadows = LightShadows.Hard;
                            dirLight.shadowStrength = 0.96f;
                            dirLight.transform.rotation = Quaternion.Euler(32f, -62f, 0f);
                        }
                    }
                }
            }

            // Earthshine directional fill light (blue ambient bounce from Earth)
            var earthshineGO = GameObject.Find("Earthshine_Fill_Light");
            if (earthshineGO != null) Object.DestroyImmediate(earthshineGO);
            earthshineGO = new GameObject("Earthshine_Fill_Light");
            var esLight = earthshineGO.AddComponent<Light>();
            esLight.type = LightType.Directional;
            esLight.color = new Color(0.22f, 0.48f, 0.95f, 1.0f);
            esLight.intensity = 1.35f;
            esLight.shadows = LightShadows.Soft;
            Vector3 earthPos = new Vector3(245f, 62f, 85f);
            Vector3 battleCenter = new Vector3(172f, 13f, 10f);
            earthshineGO.transform.rotation = Quaternion.LookRotation((battleCenter - earthPos).normalized);

            // Hide atmosphere dome
            var skydome = GameObject.Find("SkyDome");
            if (skydome != null)
            {
                var domeMesh = skydome.transform.Find("SM_FX_Skydome_01");
                if (domeMesh != null) domeMesh.gameObject.SetActive(false);
            }

            // Post-Processing Bloom & Tonemapping
            var vol = Object.FindFirstObjectByType<Volume>();
            if (vol != null && vol.profile != null)
            {
                if (vol.profile.TryGet<ColorAdjustments>(out var ca))
                {
                    ca.contrast.value = 42f;
                    ca.saturation.value = -6f;
                }
                if (vol.profile.TryGet<Bloom>(out var bloom))
                {
                    bloom.threshold.value = 1.05f;
                    bloom.intensity.value = 2.4f;
                }
            }
        }

        private static Transform SetupEarthInSky()
        {
            var earthObj = GameObject.Find("Earth_In_Lunar_Sky");
            if (earthObj != null) Object.DestroyImmediate(earthObj);

            earthObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            earthObj.name = "Earth_In_Lunar_Sky";
            // Majestic photorealistic Earth rising high in the celestial lunar sky
            Vector3 center = new Vector3(172f, 13f, 10f);
            Vector3 earthPos = new Vector3(351.7f, 129.2f, 174.6f);
            earthObj.transform.position = earthPos;

            // Orient quad to face camera center (Quad normal is (0,0,-1))
            Vector3 lookDir = (center + new Vector3(0f, 2.5f, 0f) - earthPos).normalized;
            earthObj.transform.rotation = Quaternion.LookRotation(-lookDir, Vector3.up);
            earthObj.transform.localScale = new Vector3(130f, 130f, 1f);

            var earthMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            earthMat.name = "M_Earth_Photorealistic_Sky";

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Environment/T_Earth_Photorealistic.png");
            if (tex != null) earthMat.mainTexture = tex;
            earthMat.color = Color.white;

            // Transparent blending with double-sided rendering
            earthMat.SetFloat("_Surface", 1);
            earthMat.SetFloat("_Blend", 0);
            earthMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            earthMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            earthMat.SetInt("_ZWrite", 0);
            earthMat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            earthMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            earthMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

            earthObj.GetComponent<MeshRenderer>().sharedMaterial = earthMat;
            Object.DestroyImmediate(earthObj.GetComponent<Collider>());
            return earthObj.transform;
        }

        private static void CleanAlienFlora()
        {
            var envObj = GameObject.Find("Environment");
            if (envObj != null)
            {
                foreach (Transform t in envObj.GetComponentsInChildren<Transform>(true))
                {
                    string n = t.gameObject.name.ToLower();
                    if (n.Contains("plant") || n.Contains("coral") || n.Contains("flower") || n.Contains("cactus") || 
                        n.Contains("tree") || n.Contains("spike") || n.Contains("kelp") || n.Contains("bulb") || n.Contains("pinecone") ||
                        n.Contains("floating_rock") || n.Contains("crystal") || n.Contains("fence") || n.Contains("grass"))
                    {
                        t.gameObject.SetActive(false);
                    }
                    if (n.Contains("cliff_curved_large_01"))
                    {
                        if (Vector3.Distance(t.position, new Vector3(192.7f, -2.4f, 51.9f)) < 20f ||
                            Vector3.Distance(t.position, new Vector3(355.1f, 31.7f, 211.0f)) < 20f)
                        {
                            t.gameObject.SetActive(false);
                        }
                    }
                }
            }

            var allObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            Vector3 center = new Vector3(172f, 13f, 10f);
            foreach (var go in allObjects)
            {
                string n = go.name.ToLower();
                if (n.Contains("crystal") || n.Contains("fence") || n.Contains("grass"))
                {
                    if (Vector3.Distance(go.transform.position, center) < 40f)
                    {
                        go.SetActive(false);
                    }
                }
            }
        }

        private static void SetupHeroAndSquad(Transform parent, Vector3 center, out GameObject alitaGo, out GameObject geldaGo, out List<GameObject> squadList)
        {
            Transform heroSquad = new GameObject("Hero_Squad_Alita_And_URM_Crew").transform;
            heroSquad.SetParent(parent, false);

            squadList = new List<GameObject>();

            // Gun Anim Controllers
            var ctrlShootRifle = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/ShootRifle_Controller.controller");
            var ctrlGunWalk = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/InsaneGun_Sword_Set/Ect/AnimatorContrl/atk_gun_walking.controller");
            var ctrlGun01 = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/InsaneGun_Sword_Set/Ect/AnimatorContrl/atk_gun_01.controller");
            var ctrlGun02 = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/InsaneGun_Sword_Set/Ect/AnimatorContrl/atk_gun_02.controller");
            var ctrlGun03 = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/InsaneGun_Sword_Set/Ect/AnimatorContrl/atk_gun_03.controller");
            var ctrlGun04 = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/InsaneGun_Sword_Set/Ect/AnimatorContrl/atk_gun_04.controller");

            // 1. Alita (Nano-Berserker body)
            var alitaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Alita_Sidekick/Alita_03_Final_NanoBerserker.prefab");
            alitaGo = (GameObject)PrefabUtility.InstantiatePrefab(alitaPrefab);
            alitaGo.name = "Alita_Combat_Hero";
            alitaGo.transform.SetParent(heroSquad, false);
            alitaGo.transform.position = center + new Vector3(0f, 0f, 0f);
            alitaGo.transform.rotation = Quaternion.Euler(0f, 35f, 0f);
            var alitaAnim = alitaGo.GetComponent<Animator>();
            if (alitaAnim != null && ctrlGunWalk != null) alitaAnim.runtimeAnimatorController = ctrlGunWalk;
            
            // Deactivate swords/blades and trail renderers - gun combat only per user request!
            foreach (Transform t in alitaGo.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.ToLower().Contains("blade") || t.name.ToLower().Contains("sword") || t.name.ToLower().Contains("katana"))
                {
                    t.gameObject.SetActive(false);
                }
            }
            foreach (var tr in alitaGo.GetComponentsInChildren<TrailRenderer>(true))
            {
                tr.enabled = false;
                tr.gameObject.SetActive(false);
            }

            // Cinematic soft facial fill light for close-up remembrance
            var faceLightGO = new GameObject("Alita_Remembrance_FaceLight");
            faceLightGO.transform.SetParent(alitaGo.transform, false);
            faceLightGO.transform.localPosition = new Vector3(0.0f, 1.45f, 0.65f);
            var faceLight = faceLightGO.AddComponent<Light>();
            faceLight.type = LightType.Point;
            faceLight.color = new Color(0.85f, 0.92f, 1.0f);
            faceLight.intensity = 1.8f;
            faceLight.range = 2.5f;

            AttachWeapon(alitaGo, "Assets/Synty/PolygonSciFiWorlds/Prefabs/Weapons/SM_Wep_Assault_01.prefab");

            // 2. Commander Gelda (URM Squad Commander in Officer EVA Suit)
            var suitFem1 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_SpaceSuit_Female_01.prefab");
            geldaGo = (GameObject)PrefabUtility.InstantiatePrefab(suitFem1);
            geldaGo.name = "Commander_Gelda";
            geldaGo.transform.SetParent(heroSquad, false);
            geldaGo.transform.position = center + new Vector3(-1.1f, 0f, -0.4f);
            geldaGo.transform.rotation = Quaternion.Euler(0f, 38f, 0f);
            var geldaAnim = geldaGo.GetComponent<Animator>();
            if (geldaAnim != null) geldaAnim.runtimeAnimatorController = (ctrlShootRifle != null) ? ctrlShootRifle : ctrlGun02;
            AttachWeapon(geldaGo, "Assets/Synty/PolygonSciFiWorlds/Prefabs/Weapons/SM_Wep_Sniper_01.prefab");

            // 3. Move buggy to distant atmospheric background position (no foreground occlusion)
            var buggy = GameObject.Find("SM_Veh_Buggy_01");
            if (buggy != null)
            {
                buggy.transform.position = center + new Vector3(20f, 0.1f, -10f);
                buggy.transform.rotation = Quaternion.Euler(0f, -65f, 0f);
            }

            // 3. Charging Vanguard Troops (Spread across wide lunar front)
            var suitMale1 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_SpaceSuit_Male_01.prefab");
            var suitMale2 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_SpaceSuit_Male_02.prefab");

            RuntimeAnimatorController[] squadControllers = new RuntimeAnimatorController[]
            {
                (ctrlShootRifle != null) ? ctrlShootRifle : ctrlGunWalk,
                (ctrlShootRifle != null) ? ctrlShootRifle : ctrlGun01,
                (ctrlShootRifle != null) ? ctrlShootRifle : ctrlGun02,
                (ctrlShootRifle != null) ? ctrlShootRifle : ctrlGun03,
                (ctrlShootRifle != null) ? ctrlShootRifle : ctrlGun04
            };

            Vector3[] squadOffsets = new Vector3[]
            {
                new Vector3(-13.5f, 0.15f, -5.5f), // Far left vanguard wing
                new Vector3(-10.2f, 0.12f, -8.5f), // Left flank rifleman
                new Vector3(-7.5f, 0.15f, -6.0f),  // Left-mid skirmisher
                new Vector3(2.2f, 0.10f, -6.5f),   // Center-right point
                new Vector3(6.8f, 0.18f, -5.5f),   // Right-mid skirmisher
                new Vector3(12.2f, 0.22f, -6.2f)   // Far right vanguard wing
            };

            for (int i = 0; i < squadOffsets.Length; i++)
            {
                var prefab = (i % 2 == 0) ? suitMale1 : suitMale2;
                if (prefab == null) continue;
                var soldier = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                soldier.name = $"URM_Soldier_Vanguard_{i + 1}";
                soldier.transform.SetParent(heroSquad, false);
                soldier.transform.position = center + squadOffsets[i];
                soldier.transform.rotation = Quaternion.Euler(0f, Random.Range(10f, 25f), 0f);

                var sAnim = soldier.GetComponent<Animator>();
                if (sAnim != null && squadControllers[i % squadControllers.Length] != null)
                {
                    sAnim.runtimeAnimatorController = squadControllers[i % squadControllers.Length];
                }

                string wep = (i % 3 == 0) ? "SM_Wep_Heavy_01" : "SM_Wep_Assault_02";
                AttachWeapon(soldier, $"Assets/Synty/PolygonSciFiWorlds/Prefabs/Weapons/{wep}.prefab");
                squadList.Add(soldier);
            }
        }

        private static void SetupEnemyDefenseSquad(Transform parent, Vector3 enemyCenter, out List<GameObject> enemyList)
        {
            Transform enemySquad = new GameObject("Hostile_Perimeter_Defense").transform;
            enemySquad.SetParent(parent, false);

            enemyList = new List<GameObject>();

            var ctrlShootRifle = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/ShootRifle_Controller.controller");
            var ctrlGun01 = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/InsaneGun_Sword_Set/Ect/AnimatorContrl/atk_gun_01.controller");
            var ctrlGun03 = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/InsaneGun_Sword_Set/Ect/AnimatorContrl/atk_gun_03.controller");

            var soldierPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_Soldier_Male_01.prefab");
            var heavySoldierPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonSciFiWorlds/Prefabs/Characters/SM_Chr_ScifiWorlds_Soldier_Male_02.prefab");

            Vector3[] enemyOffsets = new Vector3[]
            {
                new Vector3(-10.5f, 0.25f, 1.0f),
                new Vector3(-4.5f, 0.15f, 0.0f),
                new Vector3(4.5f, 0.22f, 2.2f),
                new Vector3(11.2f, 0.35f, 4.8f)
            };

            for (int i = 0; i < enemyOffsets.Length; i++)
            {
                var prefab = (i == 1) ? heavySoldierPrefab : soldierPrefab;
                if (prefab == null) continue;
                var enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                enemy.name = $"Enemy_Defender_{i + 1}";
                enemy.transform.SetParent(enemySquad, false);
                enemy.transform.position = enemyCenter + enemyOffsets[i];
                enemy.transform.rotation = Quaternion.Euler(0f, Random.Range(175f, 195f), 0f);

                var eAnim = enemy.GetComponent<Animator>();
                if (eAnim != null)
                {
                    eAnim.runtimeAnimatorController = (ctrlShootRifle != null) ? ctrlShootRifle : ((i % 2 == 0) ? ctrlGun01 : ctrlGun03);
                }

                AttachWeapon(enemy, "Assets/Synty/PolygonSciFiWorlds/Prefabs/Weapons/SM_Wep_Assault_01.prefab");
                enemyList.Add(enemy);
            }

            // Fortified defense barricades and military cover on the opposing ridge
            var barrierMesh = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonSciFiWorlds/Models/SM_Bld_Platform_Barrier_Long_01.fbx");
            var crateMesh = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonCyberCity/Models/SM_Prop_Crate_01.fbx");

            for (int b = 0; b < 3; b++)
            {
                if (barrierMesh != null)
                {
                    var bar = (GameObject)PrefabUtility.InstantiatePrefab(barrierMesh);
                    bar.name = $"Enemy_Barrier_{b + 1}";
                    bar.transform.SetParent(enemySquad, false);
                    bar.transform.position = enemyCenter + new Vector3(-3.5f + b * 3.5f, 0f, -0.8f);
                    bar.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                    bar.transform.localScale = Vector3.one * 1.2f;
                }
                if (crateMesh != null)
                {
                    var cr = (GameObject)PrefabUtility.InstantiatePrefab(crateMesh);
                    cr.name = $"Enemy_AmmoCrate_{b + 1}";
                    cr.transform.SetParent(enemySquad, false);
                    cr.transform.position = enemyCenter + new Vector3(-2.0f + b * 3.0f, 0f, -0.4f);
                    cr.transform.rotation = Quaternion.Euler(0f, 25f * b, 0f);
                }
            }
        }

        private static void AttachWeapon(GameObject character, string weaponPrefabPath)
        {
            var weaponPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(weaponPrefabPath);
            if (weaponPrefab == null) return;

            Transform rightHand = FindBone(character.transform, "hand_r", "hand.r", "r_hand", "weapon_r", "right_hand");
            if (rightHand == null) rightHand = character.transform;

            var weapon = (GameObject)PrefabUtility.InstantiatePrefab(weaponPrefab);
            weapon.name = "Equipped_Gun";
            weapon.transform.SetParent(rightHand, false);
            weapon.transform.localPosition = new Vector3(0.04f, 0.05f, -0.02f);
            weapon.transform.localRotation = Quaternion.Euler(-10f, -90f, 0f);
            weapon.transform.localScale = Vector3.one * 1.05f;
        }

        private static Transform FindBone(Transform current, params string[] boneNames)
        {
            // First pass: exact matches (excluding ik_ bones)
            foreach (var t in current.GetComponentsInChildren<Transform>(true))
            {
                string lower = t.name.ToLower();
                if (lower.StartsWith("ik_") || lower.Contains("ikhand") || lower.Contains("ik_hand")) continue;
                foreach (var b in boneNames)
                {
                    if (lower == b) return t;
                }
            }

            // Second pass: contains matches (excluding ik_ bones)
            foreach (var t in current.GetComponentsInChildren<Transform>(true))
            {
                string lower = t.name.ToLower();
                if (lower.StartsWith("ik_") || lower.Contains("ikhand") || lower.Contains("ik_hand")) continue;
                foreach (var b in boneNames)
                {
                    if (lower.Contains(b)) return t;
                }
            }

            return null;
        }

        private static void SetupStarWarsFleetAndHorizonCombat(
            Transform parent,
            Vector3 center,
            out List<GameObject> starfighters,
            out List<GameObject> interceptors,
            out GameObject gunship,
            out GameObject cannon,
            out GameObject titan,
            out List<LineRenderer> dogfightLasers)
        {
            Transform fleetRoot = new GameObject("StarWars_Space_Battle_Fleet").transform;
            fleetRoot.SetParent(parent, false);

            starfighters = new List<GameObject>();
            interceptors = new List<GameObject>();
            dogfightLasers = new List<LineRenderer>();

            // 1. Squadron Alpha (3 URM Starfighters)
            var fighterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Vehicles_Hover/Hover_Police_Enforcer.prefab");
            if (fighterPrefab == null) fighterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Vehicles_Hover/Hover_Luxury_Car.prefab");

            for (int i = 0; i < 3; i++)
            {
                var ship = (GameObject)PrefabUtility.InstantiatePrefab(fighterPrefab);
                ship.name = $"URM_Starfighter_{i + 1}";
                ship.transform.SetParent(fleetRoot, false);
                ship.transform.localScale = Vector3.one * 3.5f;

                // Glowing blue ion thruster
                var thrusterGO = new GameObject("Ion_Thruster_Light");
                thrusterGO.transform.SetParent(ship.transform, false);
                thrusterGO.transform.localPosition = new Vector3(0f, 0.4f, -2.6f);
                var tLight = thrusterGO.AddComponent<Light>();
                tLight.type = LightType.Point;
                tLight.color = new Color(0.12f, 0.78f, 1.0f); // Brilliant Cyan
                tLight.intensity = 5.0f;
                tLight.range = 16.0f;

                starfighters.Add(ship);
            }

            // 2. Squadron Beta (2 Enemy Interceptors)
            var interceptorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonCyberCity/Prefabs/FX/FX_Drone_01.prefab");
            if (interceptorPrefab == null) interceptorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Vehicles_Hover/Hover_Compact_Cart.prefab");

            for (int e = 0; e < 2; e++)
            {
                var enemyShip = (GameObject)PrefabUtility.InstantiatePrefab(interceptorPrefab);
                enemyShip.name = $"Enemy_Interceptor_{e + 1}";
                enemyShip.transform.SetParent(fleetRoot, false);
                enemyShip.transform.localScale = Vector3.one * 3.2f;

                // Glowing crimson engine thruster
                var thrusterGO = new GameObject("Crimson_Thruster_Light");
                thrusterGO.transform.SetParent(enemyShip.transform, false);
                thrusterGO.transform.localPosition = new Vector3(0f, 0.2f, -1.8f);
                var tLight = thrusterGO.AddComponent<Light>();
                tLight.type = LightType.Point;
                tLight.color = new Color(1.0f, 0.15f, 0.25f); // Lethal Crimson
                tLight.intensity = 5.0f;
                tLight.range = 16.0f;

                interceptors.Add(enemyShip);
            }

            // 3. Low-Altitude Assault Gunship
            var gunshipPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Vehicles_Hover/Hover_SUV.prefab");
            gunship = (GameObject)PrefabUtility.InstantiatePrefab(gunshipPrefab);
            gunship.name = "URM_LowAltitude_Assault_Gunship";
            gunship.transform.SetParent(fleetRoot, false);
            gunship.transform.localScale = Vector3.one * 3.0f;

            var gThruster = new GameObject("Gunship_Heavy_Afterburner");
            gThruster.transform.SetParent(gunship.transform, false);
            gThruster.transform.localPosition = new Vector3(0f, 0.6f, -3.2f);
            var gLight = gThruster.AddComponent<Light>();
            gLight.type = LightType.Point;
            gLight.color = new Color(0.2f, 0.85f, 1.0f);
            gLight.intensity = 6.5f;
            gLight.range = 20.0f;

            // 4. Planetary Defense Cannon on Distant Ridge
            var cannonMesh = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonSciFiWorlds/Models/SM_Bld_Planetary_Cannon_01.fbx");
            if (cannonMesh != null)
            {
                cannon = (GameObject)PrefabUtility.InstantiatePrefab(cannonMesh);
                cannon.name = "Planetary_Defense_Cannon";
                cannon.transform.SetParent(fleetRoot, false);
                cannon.transform.position = center + new Vector3(380f, -20f, 450f);
                cannon.transform.rotation = Quaternion.Euler(-35f, 28f, 0f);
                cannon.transform.localScale = Vector3.one * 1.5f;
            }
            else cannon = null;

            // 5. Horizon War Titan Mech
            var mechPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonMech/Prefabs/Vehicles/SM_Veh_Mech_01.prefab");
            if (mechPrefab != null)
            {
                titan = (GameObject)PrefabUtility.InstantiatePrefab(mechPrefab);
                titan.name = "Horizon_War_Titan_Mech";
                titan.transform.SetParent(fleetRoot, false);
                titan.transform.position = center + new Vector3(55f, 0.5f, -15f);
                titan.transform.rotation = Quaternion.Euler(0f, -45f, 0f);
                titan.transform.localScale = Vector3.one * 1.8f;
            }
            else titan = null;

            // 6. Space Dogfight Laser Beam LineRenderers
            var laserShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (laserShader == null) laserShader = Shader.Find("Sprites/Default");

            for (int l = 0; l < 6; l++)
            {
                var laserGO = new GameObject($"Space_Dogfight_Laser_{l + 1}");
                laserGO.transform.SetParent(fleetRoot, false);
                var lr = laserGO.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.startWidth = 0.95f;
                lr.endWidth = 0.95f;

                var lMat = new Material(laserShader);
                bool isCyan = (l % 2 == 0);
                Color laserCol = isCyan ? new Color(0.15f, 0.90f, 1.0f) : new Color(1.0f, 0.12f, 0.25f);
                lMat.color = laserCol;
                lMat.SetColor("_EmissionColor", laserCol * 4.2f);
                lMat.EnableKeyword("_EMISSION");
                lr.material = lMat;
                lr.shadowCastingMode = ShadowCastingMode.Off;
                lr.receiveShadows = false;

                dogfightLasers.Add(lr);
            }
        }

        private static void SetupCombatVFX(Transform parent, Vector3 center, out GameObject tracerRoot, out GameObject muzzleRoot)
        {
            Transform vfxRoot = new GameObject("Combat_Battle_VFX").transform;
            vfxRoot.SetParent(parent, false);

            tracerRoot = new GameObject("Tracers_Root");
            tracerRoot.transform.SetParent(vfxRoot, false);

            muzzleRoot = new GameObject("Muzzle_Root");
            muzzleRoot.transform.SetParent(vfxRoot, false);

            var tracerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonParticles/Prefabs/FX_Gunshot_Heavy_Repeating_Tracers_01.prefab");
            var gunshotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonParticles/Prefabs/FX_Gunshot_01.prefab");

            // Muzzle flash on Alita
            if (gunshotPrefab != null)
            {
                var mf = (GameObject)PrefabUtility.InstantiatePrefab(gunshotPrefab);
                mf.name = "VFX_Alita_MuzzleFlash";
                mf.transform.SetParent(muzzleRoot.transform, false);
                mf.transform.position = center + new Vector3(0.2f, 1.25f, 1.1f);
                mf.transform.rotation = Quaternion.Euler(0f, 15f, 0f);
            }

            // Tracers towards enemy ridge
            if (tracerPrefab != null)
            {
                var tracer = (GameObject)PrefabUtility.InstantiatePrefab(tracerPrefab);
                tracer.name = "VFX_Hero_Tracers";
                tracer.transform.SetParent(tracerRoot.transform, false);
                tracer.transform.position = center + new Vector3(0f, 1.35f, 1.8f);
                tracer.transform.rotation = Quaternion.Euler(-1f, 15f, 0f);
            }

            // Enemy return tracers
            if (tracerPrefab != null)
            {
                var enemyTracer = (GameObject)PrefabUtility.InstantiatePrefab(tracerPrefab);
                enemyTracer.name = "VFX_Enemy_Return_Tracers";
                enemyTracer.transform.SetParent(tracerRoot.transform, false);
                enemyTracer.transform.position = center + new Vector3(16.5f, 1.4f, 10.5f);
                enemyTracer.transform.rotation = Quaternion.Euler(1f, 195f, 0f);
            }
        }

        private static void SetupDirector(
            Vector3 center,
            GameObject alita,
            GameObject gelda,
            List<GameObject> squad,
            List<GameObject> enemies,
            Transform earth,
            GameObject tracers,
            GameObject muzzles,
            List<GameObject> starfighters,
            List<GameObject> interceptors,
            GameObject gunship,
            GameObject cannon,
            GameObject titan,
            List<LineRenderer> dogfightLasers)
        {
            var directorGO = GameObject.Find("[Moon_Battle_Director]");
            if (directorGO != null) Object.DestroyImmediate(directorGO);

            directorGO = new GameObject("[Moon_Battle_Director]");
            directorGO.transform.position = center;

            var director = directorGO.AddComponent<AlitaMoonBattleCutsceneDirector>();

            // Setup Camera
            var camGO = GameObject.Find("Moon_Battle_Cinematic_Camera");
            if (camGO != null) Object.DestroyImmediate(camGO);

            camGO = new GameObject("Moon_Battle_Cinematic_Camera");
            var cam = camGO.AddComponent<Camera>();
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 2000f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.01f, 0.01f, 0.02f, 1f);

            var urpCam = camGO.AddComponent<UniversalAdditionalCameraData>();
            urpCam.renderPostProcessing = true;

            director.cinematicCamera = cam;
            director.alitaHero = alita;
            director.commanderGelda = gelda;
            director.squadTroops = squad;
            director.enemyTroops = enemies;
            director.earthTransform = earth;
            director.tracerVFXRoot = tracers;
            director.muzzleFlashVFXRoot = muzzles;

            director.starfighters = starfighters;
            director.enemyInterceptors = interceptors;
            director.assaultGunship = gunship;
            director.planetaryCannon = cannon;
            director.horizonTitanMech = titan;
            director.dogfightLaserBeams = dogfightLasers;

            // Audio Sources
            var musicSource = directorGO.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.volume = 0.85f;
            musicSource.loop = true;
            director.musicSource = musicSource;

            director.combatSoundtrack = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/MoonBattle/Alita_MoonBattle_1_13.wav")
                ?? AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/Cyberpunk_Combat_Regime.mp3");
            director.totalDuration = 13.0f;
            director.showHUD = false;

            // Directional Light
            var lightsRoot = GameObject.Find("Lights - Enable One");
            if (lightsRoot != null)
            {
                var dirLight = lightsRoot.GetComponentInChildren<Light>();
                director.lunarSunLight = dirLight;
            }
        }
    }
}
