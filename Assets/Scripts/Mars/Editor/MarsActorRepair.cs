using InsaneGun.Combat;
using PolygonMech;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Cyberpunk.Mars.Editor
{
    /// <summary>
    /// Puts Alita and the Hammer Mech boss back into Scene_Mars_FinalBattle when the scene was built without them
    /// (the invasion / Titanfall sequence needs both) and wires them to the camera, the directors and each other.
    /// </summary>
    public static class MarsActorRepair
    {
        private const string ScenePath = "Assets/Scenes/Scene_Mars_FinalBattle.unity";
        private const string AlitaPrefab = "Assets/Prefabs/Alita_BattleAngel_Hero.prefab";
        private const string BossPrefab = "Assets/PolygonMech/Prefabs/Vehicles/PF_HammerMechBoss.prefab";

        private static readonly Vector3 AlitaBasePos = new Vector3(5f, 0.1f, -15f);
        private static readonly Vector3 BossBasePos = new Vector3(5f, 0.05f, 12f);

        [MenuItem("Cyberpunk/Repair Mars Scene Actors")]
        public static void Repair()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var alita = FindIncludingInactive("Alita");
            if (alita == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AlitaPrefab);
                alita = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                alita.name = "Alita";
                alita.transform.position = MarsScavengerSceneBuilder.SnapToGround(AlitaBasePos);
                alita.transform.rotation = Quaternion.identity;
                alita.tag = "Player";
                alita.layer = LayerMask.NameToLayer("Player");
                var sword = alita.GetComponent<InsaneGunSwordController>();
                if (sword != null)
                {
                    sword.controlMode = InsaneGunSwordController.ControlMode.PlayerControlled;
                    sword.groundLayers = ~0;
                    sword.enemyLayers = LayerMask.GetMask("Enemy");
                }
            }

            var boss = FindIncludingInactive("PF_HammerMechBoss");
            if (boss == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefab);
                boss = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                boss.name = "PF_HammerMechBoss";
                boss.transform.position = MarsScavengerSceneBuilder.SnapToGround(BossBasePos);
                boss.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                int enemy = LayerMask.NameToLayer("Enemy");
                foreach (var t in boss.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = enemy;
                var bar = boss.GetComponent<BossHealthBar>() ?? boss.AddComponent<BossHealthBar>();
                bar.bossName = "COLOSSUS-06 // TITAN WAR MECH";
                bar.bossSub = "HEAVY KINETIC SIEGE UNIT // MARS EXPEDITIONARY FORCE";
            }

            var ai = boss.GetComponent<HammerMechAI>();
            if (ai != null)
            {
                var so = new SerializedObject(ai);
                so.FindProperty("target").objectReferenceValue = alita.transform;
                so.ApplyModifiedProperties();
            }

            var swordCtrl = alita.GetComponent<InsaneGunSwordController>();
            if (swordCtrl != null) swordCtrl.targetEnemy = boss.transform;

            var aura = alita.GetComponent<AlitaSuperchargeAura>() ?? alita.AddComponent<AlitaSuperchargeAura>();

            var cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
            if (cam != null)
            {
                var orbit = cam.GetComponent<SmoothOrbitCamera>() ?? cam.gameObject.AddComponent<SmoothOrbitCamera>();
                orbit.target = alita.transform;
                orbit.targetOffset = new Vector3(0f, 1.4f, 0f);
                orbit.cameraOffset = new Vector3(0.48f, 0.08f, -3.2f);
                orbit.tightPositionFollow = true;
            }

            var directorGO = GameObject.Find("Mars_Final_Battle_Director");
            if (directorGO != null)
            {
                var battle = directorGO.GetComponent<MarsFinalBattleDirector>();
                if (battle != null)
                {
                    battle.bossAI = ai;
                    battle.playerTransform = alita.transform;
                }

                var cut = directorGO.GetComponent<MarsArrivalCutsceneDirector>();
                if (cut != null)
                {
                    cut.alitaHero = alita;
                    cut.bossMech = boss;
                    cut.superchargeAura = aura;
                    if (cam != null) cut.mainCamera = cam;
                }

                var mission = directorGO.GetComponent<MarsCoreRetrievalMission>();
                if (mission != null)
                {
                    mission.alitaHero = alita;
                    mission.superchargeAura = aura;
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[MarsActorRepair] Alita and the Hammer Mech boss are in Scene_Mars_FinalBattle and wired up.");
        }

        private static GameObject FindIncludingInactive(string name)
        {
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (t.hideFlags != HideFlags.None || !t.gameObject.scene.IsValid()) continue;
                if (t.name == name) return t.gameObject;
            }
            return null;
        }
    }
}
