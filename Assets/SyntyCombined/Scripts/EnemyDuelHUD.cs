using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SyntyCombined
{
    /// <summary>
    /// Connects the 1v1 Sword Combat Enemy to the project's official Synty Interface Sci-Fi HUD.
    /// Uses existing project prefabs (e.g. HUD_SciFi_WorldSpace_EnemyInfo or HUD_SciFi_Event_BossBar_Techy01)
    /// instead of creating a custom OnGUI HUD.
    /// </summary>
    public class EnemyDuelHUD : MonoBehaviour
    {
        public enum HUDMode
        {
            WorldSpaceOverhead,
            ScreenSpaceBossBar
        }

        [Header("HUD Prefab (From Project)")]
        [Tooltip("The project's official Synty HUD prefab to use (e.g. HUD_SciFi_WorldSpace_EnemyInfo or HUD_SciFi_Event_BossBar_Techy01).")]
        public GameObject hudPrefab;
        public HUDMode displayMode = HUDMode.WorldSpaceOverhead;

        [Header("Duelist Info")]
        public string duelistName = "CYBORG MERCENARY // 01";
        public string duelistTitle = "CYBERCITY ELITE BLADEMASTER";

        [Header("World Space Settings")]
        public Vector3 headOffset = new Vector3(0f, 2.2f, 0f);
        public float canvasScale = 0.007f;
        public float maxDisplayDistance = 30f;

        [Header("References")]
        public SwordEnemyAI enemyAI;

        private GameObject _spawnedHud;
        private Canvas _canvas;
        private Transform _canvasTransform;
        private Slider _healthSlider;
        private TMP_Text _nameText;
        private TMP_Text _levelOrStatusText;

        private float _health = 200f;
        private float _maxHealth = 200f;

        private void Awake()
        {
            if (enemyAI == null) enemyAI = GetComponent<SwordEnemyAI>();
            if (enemyAI != null)
            {
                _health = enemyAI.currentHealth;
                _maxHealth = enemyAI.maxHealth;
            }
        }

        private void Start()
        {
            // Re-read health in case SwordEnemyAI.Awake ran after ours
            if (enemyAI != null)
            {
                _health = enemyAI.currentHealth;
                _maxHealth = enemyAI.maxHealth;
            }

            // Create the HUD at runtime only — keeps scene view clean (no floating T gizmo)
            InitializeProjectHUD();

            if (_healthSlider != null)
                _healthSlider.value = Mathf.Clamp01(_health / _maxHealth);
        }

        private void OnEnable()
        {
            if (enemyAI != null)
            {
                enemyAI.OnHealthChanged += HandleHealthChanged;
                enemyAI.OnStateChanged += HandleStateChanged;
            }
        }

        private void OnDisable()
        {
            if (enemyAI != null)
            {
                enemyAI.OnHealthChanged -= HandleHealthChanged;
                enemyAI.OnStateChanged -= HandleStateChanged;
            }
        }

        private void InitializeProjectHUD()
        {
            if (hudPrefab == null)
            {
                #if UNITY_EDITOR
                hudPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Synty/InterfaceSciFiSoldierHUD/Prefabs/NPC_HealthBars_EnemyData/HUD_SciFi_WorldSpace_EnemyInfo.prefab"
                );
                #endif
                if (hudPrefab == null) return;
            }

            if (displayMode == HUDMode.WorldSpaceOverhead)
            {
                // Create World-Space Canvas above head
                GameObject canvasGO = new GameObject("EnemyHUD_WorldCanvas");
                canvasGO.transform.SetParent(transform, false);
                canvasGO.transform.localPosition = headOffset;
                canvasGO.transform.localScale = Vector3.one * canvasScale;

                _canvas = canvasGO.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.WorldSpace;
                canvasGO.AddComponent<CanvasScaler>();
                _canvasTransform = canvasGO.transform;

                // Instantiate in-project Synty HUD
                _spawnedHud = Instantiate(hudPrefab, _canvasTransform);
                _spawnedHud.transform.localPosition = Vector3.zero;
                _spawnedHud.transform.localRotation = Quaternion.identity;
                _spawnedHud.transform.localScale = Vector3.one;
            }
            else
            {
                // Screen Space Boss Bar
                Canvas screenCanvas = FindOrCreateScreenCanvas();
                _spawnedHud = Instantiate(hudPrefab, screenCanvas.transform);
                var rt = _spawnedHud.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0.5f, 1f);
                    rt.anchorMax = new Vector2(0.5f, 1f);
                    rt.pivot = new Vector2(0.5f, 1f);
                    rt.anchoredPosition = new Vector2(0f, -25f);
                }
            }

            // Hook up Slider and Text components from the Synty prefab
            _healthSlider = _spawnedHud.GetComponentInChildren<Slider>();
            if (_healthSlider != null)
            {
                _healthSlider.minValue = 0f;
                _healthSlider.maxValue = 1f;
                _healthSlider.value = Mathf.Clamp01(_health / _maxHealth);
            }

            // Find TextMeshPro components in the Synty prefab
            var tmpTexts = _spawnedHud.GetComponentsInChildren<TMP_Text>();
            foreach (var t in tmpTexts)
            {
                string lowerName = t.name.ToLower();
                string lowerText = t.text.ToLower();

                if (lowerName.Contains("name") || lowerText.Contains("drone") || lowerText.Contains("enemy") || lowerName.Contains("title"))
                {
                    _nameText = t;
                    t.text = duelistName;
                }
                else if (lowerName.Contains("level") || lowerName.Contains("sub") || lowerName.Contains("class") || lowerText.Contains("lvl"))
                {
                    _levelOrStatusText = t;
                    t.text = duelistTitle;
                }
            }
        }

        private Canvas FindOrCreateScreenCanvas()
        {
            Canvas existing = FindAnyObjectByType<Canvas>();
            if (existing != null && existing.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return existing;
            }

            GameObject go = new GameObject("ScreenCanvas_DuelHUD");
            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            go.AddComponent<CanvasScaler>();
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private void LateUpdate()
        {
            if (displayMode == HUDMode.WorldSpaceOverhead && _canvasTransform != null)
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    float dist = Vector3.Distance(cam.transform.position, _canvasTransform.position);
                    if (dist > maxDisplayDistance)
                    {
                        if (_canvas.enabled) _canvas.enabled = false;
                        return;
                    }

                    if (!_canvas.enabled && _health > 0f) _canvas.enabled = true;

                    // Billboard towards camera
                    _canvasTransform.rotation = cam.transform.rotation;
                }
            }
        }

        private void HandleHealthChanged(float cur, float max)
        {
            _health = cur;
            _maxHealth = max;

            if (_healthSlider != null)
            {
                _healthSlider.value = Mathf.Clamp01(_health / _maxHealth);
            }

            if (_health <= 0f)
            {
                if (_canvas != null) _canvas.enabled = false;
                if (_spawnedHud != null) _spawnedHud.SetActive(false);
            }
        }

        private void HandleStateChanged(SwordEnemyAI.AIState newState)
        {
            if (_levelOrStatusText == null) return;

            switch (newState)
            {
                case SwordEnemyAI.AIState.Intro:
                    _levelOrStatusText.text = "[DRAWING BLADE...]";
                    break;
                // ── Defensive ──────────────────────────────────────────────────
                case SwordEnemyAI.AIState.Block:
                    _levelOrStatusText.text = "[GUARDING]";
                    break;
                case SwordEnemyAI.AIState.ParryCounter:
                    _levelOrStatusText.text = "[PARRY / COUNTER]";
                    break;
                case SwordEnemyAI.AIState.Dodge:
                    _levelOrStatusText.text = "[DODGING]";
                    break;
                case SwordEnemyAI.AIState.Staggered:
                    _levelOrStatusText.text = "[STAGGERED — VULNERABLE]";
                    break;
                // ── Special Attacks ────────────────────────────────────────────
                case SwordEnemyAI.AIState.BlinkSlash:
                    _levelOrStatusText.text = "[CYBER BLINK]";
                    break;
                case SwordEnemyAI.AIState.SpinningCyclone:
                    _levelOrStatusText.text = "[CYCLONE SPIN]";
                    break;
                case SwordEnemyAI.AIState.OverheadSlam:
                    _levelOrStatusText.text = "[OVERHEAD SLAM]";
                    break;
                case SwordEnemyAI.AIState.ShoulderCharge:
                    _levelOrStatusText.text = "[CHARGING!]";
                    break;
                case SwordEnemyAI.AIState.PommelCross:
                    _levelOrStatusText.text = "[POMMEL STRIKE]";
                    break;
                case SwordEnemyAI.AIState.GrenadeToss:
                    _levelOrStatusText.text = "[GRENADE!]";
                    break;
                // ── Phase Transition ───────────────────────────────────────────
                case SwordEnemyAI.AIState.OverdriveTransition:
                    _levelOrStatusText.text = "⚡ OVERDRIVE ENGAGED ⚡";
                    break;
                // ── Terminal ───────────────────────────────────────────────────
                case SwordEnemyAI.AIState.Dead:
                    _levelOrStatusText.text = "[TERMINATED]";
                    break;
                default:
                    _levelOrStatusText.text = duelistTitle;
                    break;
            }
        }
    }
}
