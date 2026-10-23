using System.Collections.Generic;
using UnityEngine;
using SyntyCombined;
using InsaneGun.Combat;

namespace InsaneGun.Combat
{
    /// <summary>
    /// Master Spawner and Wave Controller for Holographic Rogue AI enemies emerging
    /// directly out of the 200m Cyberpunk Blackwall barrier plane into the arena.
    /// Controls single emergence, squad horde emergence, auto-wave spawning, and GUI controls.
    /// </summary>
    public class BlackwallHolographicSpawner : MonoBehaviour
    {
        public static BlackwallHolographicSpawner Instance { get; private set; }

        [Header("Enemy Prefabs")]
        [Tooltip("Primary Synty Sword Enemy prefab to spawn from the Blackwall.")]
        public GameObject enemyPrefab;

        [Header("Blackwall Spawn Plane Parameters")]
        [Tooltip("Z coordinate of the Blackwall barrier plane.")]
        public float blackwallZ = 28.5f;
        [Tooltip("X spawn width range along the 200m Blackwall.")]
        public float spawnWidthX = 18.0f;
        [Tooltip("Floor Y spawn coordinate.")]
        public float spawnY = 0.0f;

        [Header("Controls & Auto Waves")]
        public KeyCode spawnSingleKey = KeyCode.B;
        public KeyCode spawnSquadKey = KeyCode.N;
        public KeyCode clearEnemiesKey = KeyCode.V;
        public bool showOnScreenGUI = true;
        public bool autoRespawnWaves = false;
        public int maxActiveEnemies = 6;

        private readonly List<GameObject> _activeEnemies = new List<GameObject>();

        private void Awake()
        {
            Instance = this;
            EnsureEnemyPrefab();
        }

        private void Start()
        {
            EnsureEnemyPrefab();
        }

        private void EnsureEnemyPrefab()
        {
            if (enemyPrefab == null)
            {

#if UNITY_EDITOR
                string prefabPath = "Assets/SyntyCombined/Prefabs/PF_SwordEnemy_CyberCity.prefab";
                enemyPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
#endif
            }
        }

        private void Update()
        {
            // Remove null / dead enemies from active list
            _activeEnemies.RemoveAll(e => e == null);

            // Hotkey triggers
            if (Input.GetKeyDown(spawnSingleKey))
            {
                SpawnEnemyFromBlackwall();
            }

            if (Input.GetKeyDown(spawnSquadKey))
            {
                SpawnSquadFromBlackwall(3);
            }

            if (Input.GetKeyDown(clearEnemiesKey))
            {
                ClearAllEnemies();
            }

            // Auto-respawn wave logic
            if (autoRespawnWaves && _activeEnemies.Count == 0)
            {
                SpawnSquadFromBlackwall(2);
            }
        }

        [ContextMenu("Spawn Holographic Enemy from Blackwall")]
        public GameObject SpawnEnemyFromBlackwall()
        {
            if (_activeEnemies.Count >= maxActiveEnemies)
            {
                Debug.LogWarning($"[Blackwall Spawner] Max active enemies reached ({maxActiveEnemies}).");
                return null;
            }

            EnsureEnemyPrefab();

            // Random X position along the Blackwall Z=28.5m plane
            float randomX = Random.Range(-spawnWidthX, spawnWidthX);
            Vector3 spawnPos = new Vector3(randomX, spawnY, blackwallZ);
            Quaternion spawnRot = Quaternion.Euler(0f, 180f, 0f); // Facing forward towards player (-Z)

            GameObject newEnemy = null;
            if (enemyPrefab != null)
            {
                newEnemy = Instantiate(enemyPrefab, spawnPos, spawnRot);
            }
            else
            {
                // Fallback: Clone existing scene enemy if prefab is unassigned
                var existingEnemy = GameObject.FindWithTag("Enemy");
                if (existingEnemy != null)
                {
                    newEnemy = Instantiate(existingEnemy, spawnPos, spawnRot);
                }
            }

            if (newEnemy != null)
            {
                newEnemy.name = $"Holographic_Rogue_AI_{_activeEnemies.Count + 1}";
                newEnemy.tag = "Enemy";

                // Attach emergence controller
                var emergence = newEnemy.GetComponent<HolographicBlackwallEmergence>();
                if (emergence == null) emergence = newEnemy.AddComponent<HolographicBlackwallEmergence>();

                emergence.BeginEmergence(spawnPos, spawnRot);
                _activeEnemies.Add(newEnemy);

                Debug.Log($"<color=#FF0055><b>[Blackwall Spawner]</b></color> Emerging Rogue AI Enemy from Blackwall at X={randomX:F1}m, Z={blackwallZ:F1}m!");
            }

            return newEnemy;
        }

        [ContextMenu("Spawn Holographic Squad from Blackwall")]
        public void SpawnSquadFromBlackwall(int count = 3)
        {
            float stepX = (spawnWidthX * 2.0f) / Mathf.Max(1, count + 1);
            float startX = -spawnWidthX + stepX;

            for (int i = 0; i < count; i++)
            {
                if (_activeEnemies.Count >= maxActiveEnemies) break;

                EnsureEnemyPrefab();
                float spawnX = startX + i * stepX + Random.Range(-1.5f, 1.5f);
                Vector3 spawnPos = new Vector3(spawnX, spawnY, blackwallZ);
                Quaternion spawnRot = Quaternion.Euler(0f, 180f, 0f);

                GameObject newEnemy = enemyPrefab != null ? Instantiate(enemyPrefab, spawnPos, spawnRot) : null;
                if (newEnemy != null)
                {
                    newEnemy.name = $"Holographic_Rogue_AI_Squad_{i + 1}";
                    newEnemy.tag = "Enemy";

                    var emergence = newEnemy.GetComponent<HolographicBlackwallEmergence>();
                    if (emergence == null) emergence = newEnemy.AddComponent<HolographicBlackwallEmergence>();

                    emergence.BeginEmergence(spawnPos, spawnRot);
                    _activeEnemies.Add(newEnemy);
                }
            }
        }

        public void ClearAllEnemies()
        {
            foreach (var e in _activeEnemies)
            {
                if (e != null) Destroy(e);
            }
            _activeEnemies.Clear();
            Debug.Log("<color=#FF0055><b>[Blackwall Spawner]</b></color> Cleared all active holographic enemies.");
        }

        private void OnGUI()
        {
            if (!showOnScreenGUI) return;

            // Cyberpunk HUD overlay in lower-left corner
            int activeCount = _activeEnemies.Count;

            GUIStyle cardStyle = new GUIStyle(GUI.skin.box);
            cardStyle.normal.background = Texture2D.blackTexture;

            float width = 340f;
            float height = 110f;
            float margin = 20f;
            Rect hudRect = new Rect(margin, Screen.height - height - margin - 80f, width, height);

            GUI.Box(hudRect, "");

            GUILayout.BeginArea(new Rect(hudRect.x + 10, hudRect.y + 8, width - 20, height - 16));
            
            GUILayout.Label("<color=#FF0055><b>🔴 BLACKWALL ROGUE AI EMERGENCE</b></color>", new GUIStyle(GUI.skin.label) { richText = true, fontSize = 13 });
            GUILayout.Label($"Active Rogue AI Units: <color=#00E5FF><b>{activeCount} / {maxActiveEnemies}</b></color>", new GUIStyle(GUI.skin.label) { richText = true });
            
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("[B] Spawn 1 AI Unit", GUILayout.Height(24)))
            {
                SpawnEnemyFromBlackwall();
            }
            if (GUILayout.Button("[N] Spawn AI Squad", GUILayout.Height(24)))
            {
                SpawnSquadFromBlackwall(3);
            }
            if (GUILayout.Button("[V] Clear", GUILayout.Height(24)))
            {
                ClearAllEnemies();
            }
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }
    }
}
