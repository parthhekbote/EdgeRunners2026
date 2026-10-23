using UnityEngine;

namespace PolygonMech
{
    public class BossHealthBar : MonoBehaviour
    {
        [Header("Boss Identity")]
        public string bossName = "GOLIATH-01 // TITAN WAR MECH";
        public string bossSub = "CYBER-CORP HEAVY SIEGE UNIT";

        [Header("Health References")]
        [SerializeField] private float maxHealth = 1000f;
        private float currentHealth;
        private float lagHealth;
        private bool isEnraged = false;
        private bool isDead = false;

        [Header("Posture / Poise System")]
        private float maxPosture = 100f;
        private float currentPosture = 0f;
        private float lagPosture = 0f;
        private bool isStaggered = false;

        // Cached GUI styles — allocated once, not every frame
        private GUIStyle _titleStyle;
        private GUIStyle _subStyle;

        private void Awake()
        {
            currentHealth = maxHealth;
            lagHealth = maxHealth;

            // Build GUIStyle cache once (safe here since OnGUI won't run before first frame)
            _titleStyle = new GUIStyle()
            {
                fontSize  = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _titleStyle.normal.textColor = new Color(0.9f, 0.95f, 1f);

            _subStyle = new GUIStyle()
            {
                fontSize  = 10,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter
            };
            _subStyle.normal.textColor = new Color(0.6f, 0.7f, 0.8f, 0.8f);
        }

        private void Update()
        {
            // Smoothly lerp the damage lag bar
            if (lagHealth > currentHealth)
            {
                lagHealth = Mathf.Lerp(lagHealth, currentHealth, Time.deltaTime * 3f);
            }

            // Smoothly lerp posture
            lagPosture = Mathf.Lerp(lagPosture, currentPosture, Time.deltaTime * 6f);
        }

        public void Initialize(float maxHp)
        {
            maxHealth = maxHp;
            currentHealth = maxHp;
            lagHealth = maxHp;
        }

        public void SetHealth(float hp)
        {
            currentHealth = Mathf.Clamp(hp, 0f, maxHealth);
            if (currentHealth <= 0f) isDead = true;
        }

        public void SetPosture(float current, float max)
        {
            currentPosture = Mathf.Clamp(current, 0f, max);
            maxPosture = Mathf.Max(1f, max);
        }

        public void SetStaggered(bool staggered)
        {
            isStaggered = staggered;
        }

        public void SetEnraged(bool enraged)
        {
            isEnraged = enraged;
        }

        private void OnGUI()
        {
            if (isDead && currentHealth <= 0f) return;

            // Screen space top-center boss health bar
            float screenWidth = Screen.width;
            float barWidth = Mathf.Min(screenWidth * 0.6f, 650f);
            float barHeight = 18f;
            float barX = (screenWidth - barWidth) * 0.5f;
            float barY = 32f;

            // Draw Header Title & Subtitle — use cached styles (no per-frame alloc)
            if (isStaggered)
            {
                float flash = Mathf.PingPong(Time.time * 6f, 1f);
                _titleStyle.normal.textColor = Color.Lerp(new Color(1f, 0.9f, 0.2f), new Color(1f, 0.2f, 0.2f), flash);
            }
            else if (isEnraged)
            {
                _titleStyle.normal.textColor = new Color(1f, 0.3f, 0.2f);
            }
            else
            {
                _titleStyle.normal.textColor = new Color(0.9f, 0.95f, 1f);
            }

            string displayTitle = isStaggered ? $"[!] {bossName} [SYSTEM OVERLOAD // VULNERABLE] [!]" :
                                  isEnraged   ? $"[!] {bossName} [OVERDRIVE ENRAGED]" : bossName;

            GUI.Label(new Rect(barX, barY - 30, barWidth, 20), displayTitle, _titleStyle);
            GUI.Label(new Rect(barX, barY - 15, barWidth, 16), bossSub, _subStyle);

            // 1. Dark Background Frame
            Rect bgRect = new Rect(barX - 2, barY - 2, barWidth + 4, barHeight + 4);
            DrawRect(bgRect, new Color(0.05f, 0.08f, 0.12f, 0.9f));

            // Inner Background
            Rect innerBg = new Rect(barX, barY, barWidth, barHeight);
            DrawRect(innerBg, new Color(0.12f, 0.14f, 0.18f, 0.95f));

            float healthPct = Mathf.Clamp01(currentHealth / maxHealth);
            float lagPct = Mathf.Clamp01(lagHealth / maxHealth);

            // 2. Damage Lag Bar (Yellow/Orange)
            if (lagPct > healthPct)
            {
                Rect lagRect = new Rect(barX, barY, barWidth * lagPct, barHeight);
                DrawRect(lagRect, new Color(1f, 0.6f, 0.1f, 0.85f));
            }

            // 3. Main Health Bar (Cyan / Neon Red if enraged)
            Color barColor = isEnraged ? new Color(0.95f, 0.2f, 0.2f) : new Color(0.1f, 0.8f, 0.95f);
            Rect healthRect = new Rect(barX, barY, barWidth * healthPct, barHeight);
            DrawRect(healthRect, barColor);

            // 4. Subtle Border
            DrawBorder(bgRect, isEnraged ? new Color(1f, 0.3f, 0.2f, 0.8f) : new Color(0.3f, 0.5f, 0.7f, 0.6f), 1);

            // 5. Posture / Poise Gauge (directly below health bar)
            float postureHeight = 6f;
            float postureY = barY + barHeight + 4f;
            Rect postureBg = new Rect(barX, postureY, barWidth, postureHeight);
            DrawRect(postureBg, new Color(0.08f, 0.10f, 0.14f, 0.85f));

            float posturePct = Mathf.Clamp01(lagPosture / maxPosture);
            if (posturePct > 0.01f)
            {
                Color poiseColor = isStaggered ? new Color(1f, 0.2f, 0.2f, 0.9f) :
                                  (posturePct > 0.75f ? new Color(1f, 0.5f, 0.1f, 0.9f) : new Color(0.95f, 0.75f, 0.2f, 0.85f));
                Rect poiseBarRect = new Rect(barX, postureY, barWidth * posturePct, postureHeight);
                DrawRect(poiseBarRect, poiseColor);
            }
            DrawBorder(new Rect(barX - 1, postureY - 1, barWidth + 2, postureHeight + 2), new Color(0.25f, 0.35f, 0.45f, 0.5f), 1);
        }

        private static Texture2D s_WhiteTex;
        private static void DrawRect(Rect rect, Color color)
        {
            if (s_WhiteTex == null)
            {
                s_WhiteTex = new Texture2D(1, 1);
                s_WhiteTex.SetPixel(0, 0, Color.white);
                s_WhiteTex.Apply();
            }
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, s_WhiteTex);
            GUI.color = prev;
        }

        private static void DrawBorder(Rect rect, Color color, int thickness)
        {
            DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            DrawRect(new Rect(rect.x, rect.y + rect.height - thickness, rect.width, thickness), color);
            DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            DrawRect(new Rect(rect.x + rect.width - thickness, rect.y, thickness, rect.height), color);
        }
    }
}
