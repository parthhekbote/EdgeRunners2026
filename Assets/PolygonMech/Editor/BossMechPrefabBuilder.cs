using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PolygonMech.Editor
{
    [InitializeOnLoad]
    public static class BossMechPrefabBuilder
    {
        // ── Source prefab (Mech 02 is the armed humanoid-skeleton mech) ──────────
        private const string SourcePrefabPath    = "Assets/PolygonMech/Prefabs/Vehicles/SM_Veh_Mech_02.prefab";
        private const string OutputPrefabPath    = "Assets/PolygonMech/Prefabs/Vehicles/PF_BossMech_AI.prefab";
        private const string ControllerPath      = "Assets/PolygonMech/Animations/AC_BossMech.controller";
        private const string SwordPrefabPath     = "Assets/PolygonMech/Prefabs/Weapons/SM_Wep_Sword_01.prefab";

        // ── Avatar source: SM_Veh_Mech_01 inside GreatSwordPack (has valid Generic rig) ──
        private const string AvatarFBXPath       = "Assets/PolygonMech/Animations/GreatSwordPack/SM_Veh_Mech_01.fbx";

        // ── Samurai Boss ─────────────────────────────────────────────────────────
        private const string SamuraiOutputPath   = "Assets/PolygonMech/Prefabs/Vehicles/PF_SamuraiMechBoss.prefab";
        private const string KatanaPrefabPath    = "Assets/PolygonMech/Prefabs/Weapons/SM_Wep_Katana_01.prefab";
        private const string SamuraiControllerPath = "Assets/PolygonMech/Animations/AC_SamuraiMech.controller";

        // ── FX — all real assets in Assets/PolygonMech/Prefabs/FX/ ──────────────
        private const string FXBase              = "Assets/PolygonMech/Prefabs/FX";

        static BossMechPrefabBuilder()
        {
            EditorApplication.delayCall += AutoRun;
        }

        private static void AutoRun()
        {
            if (!SessionState.GetBool("BossMechAutoBuilt_v3", false))
            {
                SessionState.SetBool("BossMechAutoBuilt_v3", true);
                BuildAndSpawn();
            }
        }

        // ── Helper: load avatar from GreatSwordPack FBX (Generic rig, same skeleton as mechs) ──
        private static Avatar LoadMechAvatar()
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(AvatarFBXPath);
            foreach (var a in assets)
            {
                if (a is Avatar av && av.isValid)
                    return av;
            }
            Debug.LogWarning("[BossBuilder] No valid Avatar in " + AvatarFBXPath + " — leaving avatar null (Generic rig OK).");
            return null;
        }

        private static GameObject LoadFX(string name) =>
            AssetDatabase.LoadAssetAtPath<GameObject>($"{FXBase}/{name}");

        [MenuItem("Polygon Mech/2. Build Boss Prefab & Spawn in Scene", false, 2)]
        public static void BuildAndSpawn()
        {
            // Step 1: Ensure controllers are built from GreatSwordPack
            MechAnimationSetup.BuildBossController();

            // Step 2: Load source prefab
            GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath);
            if (sourcePrefab == null)
            {
                Debug.LogError("[BossBuilder] Source prefab not found: " + SourcePrefabPath);
                return;
            }

            GameObject bossGO = (GameObject)PrefabUtility.InstantiatePrefab(sourcePrefab);
            PrefabUtility.UnpackPrefabInstance(bossGO, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            bossGO.name = "Boss_GoliathMech";

            // ── CharacterController ──────────────────────────────────────────────
            CharacterController cc = bossGO.GetComponent<CharacterController>();
            if (cc == null) cc = bossGO.AddComponent<CharacterController>();
            cc.height = 3.8f; cc.radius = 1.3f; cc.center = new Vector3(0, 1.9f, 0);
            cc.stepOffset = 0.5f; cc.slopeLimit = 45f; cc.skinWidth = 0.08f;

            // ── Animator — Generic rig (mech skeleton) ───────────────────────────
            Animator anim = bossGO.GetComponent<Animator>();
            if (anim == null) anim = bossGO.AddComponent<Animator>();
            Avatar mechAvatar = LoadMechAvatar();
            if (mechAvatar != null) anim.avatar = mechAvatar;
            anim.applyRootMotion = false;
            anim.updateMode = AnimatorUpdateMode.Normal;

            RuntimeAnimatorController ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (ctrl != null) anim.runtimeAnimatorController = ctrl;

            // ── Attach Giant Sword ───────────────────────────────────────────────
            Transform rHand = (anim != null && anim.isHuman) ? anim.GetBoneTransform(HumanBodyBones.RightHand) : null;
            if (rHand == null) rHand = FindInHierarchy(bossGO.transform, "Hand_R");
            if (rHand == null) rHand = FindInHierarchy(bossGO.transform, "mixamorig:RightHand");

            if (rHand != null)
            {
                GameObject swordPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SwordPrefabPath);
                if (swordPrefab != null)
                {
                    Transform socket = GetOrCreateSocket(rHand);
                    for (int i = rHand.childCount - 1; i >= 0; i--)
                        if (rHand.GetChild(i).name.StartsWith("SM_Wep_"))
                            Object.DestroyImmediate(rHand.GetChild(i).gameObject);

                    GameObject sword = Object.Instantiate(swordPrefab, socket);
                    sword.name = "SM_Wep_Sword_01";
                    sword.transform.localPosition = Vector3.zero;
                    sword.transform.localRotation = Quaternion.identity;
                    sword.transform.localScale    = Vector3.one;

                    if (sword.transform.Find("SwordTip") == null)
                    {
                        var tip = new GameObject("SwordTip");
                        tip.transform.SetParent(sword.transform, false);
                        tip.transform.localPosition = new Vector3(0, 3.5f, 0);
                    }
                    Debug.Log("[BossBuilder] Sword attached.");
                }
            }
            else
            {
                Debug.LogWarning("[BossBuilder] Hand_R bone not found — sword not attached. Check the mech rig.");
            }

            // ── BossMechAI ───────────────────────────────────────────────────────
            BossMechAI ai = bossGO.GetComponent<BossMechAI>();
            if (ai == null) ai = bossGO.AddComponent<BossMechAI>();

            // ── Wire FX using REAL existing prefab paths ─────────────────────────
            SerializedObject soAI = new SerializedObject(ai);
            soAI.Update();

            SetFXProp(soAI, "groundSlamFXPrefab",    LoadFX("FX_Jump_Impact_01.prefab"));
            SetFXProp(soAI, "missileBarrageFXPrefab", LoadFX("FX_Missile_Barrage_01.prefab"));
            SetFXProp(soAI, "exhaustFlameFXPrefab",   LoadFX("FX_Flame_Exhaust_01.prefab"));
            SetFXProp(soAI, "sparksDeathFXPrefab",    LoadFX("FX_Sparks_01.prefab"));

            // ── Spawn points ─────────────────────────────────────────────────────
            Transform slamPt = FindOrAddChild(bossGO.transform, "SlamPoint",     new Vector3(0, 0, 2.5f));
            Transform podL   = FindOrAddChild(bossGO.transform, "ShoulderPod_L", new Vector3(-1.2f, 2.8f, 0.4f));
            Transform podR   = FindOrAddChild(bossGO.transform, "ShoulderPod_R", new Vector3(1.2f,  2.8f, 0.4f));
            Transform ventL  = FindOrAddChild(bossGO.transform, "ExhaustVent_L", new Vector3(-0.8f, 2.5f, -1.0f));
            Transform ventR  = FindOrAddChild(bossGO.transform, "ExhaustVent_R", new Vector3(0.8f,  2.5f, -1.0f));

            soAI.FindProperty("groundSlamPoint").objectReferenceValue  = slamPt;
            soAI.FindProperty("shoulderPodLeft").objectReferenceValue  = podL;
            soAI.FindProperty("shoulderPodRight").objectReferenceValue = podR;
            soAI.FindProperty("exhaustVentLeft").objectReferenceValue  = ventL;
            soAI.FindProperty("exhaustVentRight").objectReferenceValue = ventR;
            soAI.ApplyModifiedProperties();

            // ── Save prefab ──────────────────────────────────────────────────────
            PrefabUtility.SaveAsPrefabAsset(bossGO, OutputPrefabPath);
            Object.DestroyImmediate(bossGO);
            Debug.Log("[BossBuilder] PF_BossMech_AI saved.");

            SpawnInScene("Boss_GoliathMech", OutputPrefabPath, new Vector3(-6f, 0f, 9f), 160f);
        }

        [MenuItem("Polygon Mech/3. Build Samurai Mech Boss (Kensei-01)", false, 3)]
        public static void BuildSamuraiMechBoss()
        {
            MechAnimationSetup.BuildSamuraiController();

            GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath);
            if (sourcePrefab == null) { Debug.LogError("[SamuraiBuilder] Source prefab not found."); return; }

            GameObject bossGO = (GameObject)PrefabUtility.InstantiatePrefab(sourcePrefab);
            PrefabUtility.UnpackPrefabInstance(bossGO, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            bossGO.name = "Boss_KenseiSamuraiMech";

            CharacterController cc = bossGO.GetComponent<CharacterController>();
            if (cc == null) cc = bossGO.AddComponent<CharacterController>();
            cc.height = 3.8f; cc.radius = 1.3f; cc.center = new Vector3(0, 1.9f, 0);
            cc.stepOffset = 0.5f; cc.slopeLimit = 45f; cc.skinWidth = 0.08f;

            Animator anim = bossGO.GetComponent<Animator>();
            if (anim == null) anim = bossGO.AddComponent<Animator>();
            Avatar mechAvatar = LoadMechAvatar();
            if (mechAvatar != null) anim.avatar = mechAvatar;
            anim.applyRootMotion = false;
            anim.updateMode = AnimatorUpdateMode.Normal;

            RuntimeAnimatorController ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(SamuraiControllerPath);
            if (ctrl == null) ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (ctrl != null) anim.runtimeAnimatorController = ctrl;

            Transform rHand = (anim != null && anim.isHuman) ? anim.GetBoneTransform(HumanBodyBones.RightHand) : null;
            if (rHand == null) rHand = FindInHierarchy(bossGO.transform, "Hand_R");
            if (rHand != null)
            {
                GameObject katanaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(KatanaPrefabPath);
                if (katanaPrefab != null)
                {
                    Transform socket = GetOrCreateSocket(rHand);
                    for (int i = rHand.childCount - 1; i >= 0; i--)
                        if (rHand.GetChild(i).name.StartsWith("SM_Wep_"))
                            Object.DestroyImmediate(rHand.GetChild(i).gameObject);

                    GameObject katana = Object.Instantiate(katanaPrefab, socket);
                    katana.name = "SM_Wep_Katana_01";
                    katana.transform.localPosition = Vector3.zero;
                    katana.transform.localRotation = Quaternion.identity;
                    katana.transform.localScale    = Vector3.one;

                    if (katana.transform.Find("KatanaTip") == null)
                    {
                        var tip = new GameObject("KatanaTip");
                        tip.transform.SetParent(katana.transform, false);
                        tip.transform.localPosition = new Vector3(0, 4.48f, 0);
                    }
                    Debug.Log("[SamuraiBuilder] Katana attached.");
                }
            }

            SamuraiMechAI ai = bossGO.GetComponent<SamuraiMechAI>();
            if (ai == null) ai = bossGO.AddComponent<SamuraiMechAI>();
            Transform katanaTip = FindInHierarchy(bossGO.transform, "KatanaTip");
            Transform slamPt    = FindOrAddChild(bossGO.transform, "SlamPoint_S", new Vector3(0, 0, 2.5f));

            SerializedObject soAI = new SerializedObject(ai);
            soAI.Update();
            if (katanaTip != null) soAI.FindProperty("katanaTip").objectReferenceValue = katanaTip;
            soAI.FindProperty("slamPoint").objectReferenceValue = slamPt;
            soAI.ApplyModifiedProperties();

            PrefabUtility.SaveAsPrefabAsset(bossGO, SamuraiOutputPath);
            Object.DestroyImmediate(bossGO);
            Debug.Log("[SamuraiBuilder] PF_SamuraiMechBoss saved.");

            SpawnInScene("Boss_KenseiSamuraiMech", SamuraiOutputPath, new Vector3(6f, 0f, 9f), 200f);
        }

        private static void SpawnInScene(string objName, string prefabPath, Vector3 pos, float yRot)
        {
            string scenePath = "Assets/Scenes/SyntyCombatDemo.unity";
            if (!File.Exists(scenePath)) return;

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            GameObject existing = GameObject.Find(objName);
            if (existing != null) Object.DestroyImmediate(existing);

            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);
            inst.name = objName;
            inst.transform.position = pos;
            inst.transform.rotation = Quaternion.Euler(0, yRot, 0);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[BossBuilder] {objName} spawned at {pos}.");
        }

        // ── Utility ──────────────────────────────────────────────────────────────
        private static void SetFXProp(SerializedObject so, string propName, GameObject fx)
        {
            var prop = so.FindProperty(propName);
            if (prop != null && fx != null) prop.objectReferenceValue = fx;
            else if (fx == null) Debug.LogWarning($"[BossBuilder] FX prefab not found for '{propName}'.");
        }

        private static Transform FindOrAddChild(Transform parent, string name, Vector3 localPos)
        {
            Transform t = parent.Find(name);
            if (t == null) t = FindInHierarchy(parent, name);
            if (t != null) return t;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go.transform;
        }

        public static Transform FindInHierarchy(Transform root, string target)
        {
            if (root.name == target) return root;
            foreach (Transform c in root)
            {
                Transform found = FindInHierarchy(c, target);
                if (found != null) return found;
            }
            return null;
        }

        private static Transform GetOrCreateSocket(Transform rHand)
        {
            Transform propR = rHand.Find("Prop_R");
            if (propR == null)
            {
                var go = new GameObject("Prop_R");
                go.transform.SetParent(rHand, false);
                propR = go.transform;
            }
            Transform socket = propR.Find("Prop_R_Socket");
            if (socket == null)
            {
                var go = new GameObject("Prop_R_Socket");
                go.transform.SetParent(propR, false);
                go.transform.localPosition    = new Vector3(-0.03f, -0.32f, -0.05f);
                go.transform.localEulerAngles = new Vector3(0, 0, 180f);
                socket = go.transform;
            }
            return socket;
        }
    }
}
