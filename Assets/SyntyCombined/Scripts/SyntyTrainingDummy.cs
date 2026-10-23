using System.Collections;
using UnityEngine;

namespace SyntyCombined
{
    /// <summary>
    /// Training dummy target that reacts to player sword attacks with hit reactions,
    /// staggers, floating combat text, and automatic reset.
    /// </summary>
    public class SyntyTrainingDummy : MonoBehaviour, ISyntyDamageable
    {
        [Header("Stats")]
        public float maxHealth = 150f;
        public float currentHealth;
        public string dummyName = "Combat Dummy";

        [Header("Components")]
        public Animator animator;
        public Renderer[] meshRenderers;

        [Header("Layer Index")]
        public int combatLayerIndex = 3;

        private bool _isDead = false;
        private Coroutine _flashRoutine;
        private string _lastHitText = "";
        private float _hitTextTimer = 0f;
        private Color _hitTextColor = Color.yellow;

        private void Awake()
        {
            currentHealth = maxHealth;
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
            if (meshRenderers == null || meshRenderers.Length == 0)
                meshRenderers = GetComponentsInChildren<Renderer>();
        }

        public void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitDirection, bool isHeavy)
        {
            if (_isDead) return;

            currentHealth -= damage;
            _lastHitText = $"-{damage:F0} {(isHeavy ? "CRIT!" : "HIT")}";
            _hitTextColor = isHeavy ? new Color(1f, 0.4f, 0f) : new Color(1f, 0.9f, 0.2f);
            _hitTextTimer = 1.2f;

            if (meshRenderers != null && meshRenderers.Length > 0)
            {
                if (_flashRoutine != null) StopCoroutine(_flashRoutine);
                _flashRoutine = StartCoroutine(HitFlashRoutine());
            }

            if (currentHealth <= 0f)
            {
                Die(hitDirection);
                return;
            }

            // Play hit animation
            if (animator != null && animator.isActiveAndEnabled)
            {
                string animState = isHeavy ? "A_Hit_F_Stagger_Sword" : "A_Hit_F_React_Sword";
                animator.SetLayerWeight(combatLayerIndex, 1f);
                animator.CrossFadeInFixedTime(animState, 0.08f, combatLayerIndex);
            }
        }

        private void Die(Vector3 hitDirection)
        {
            _isDead = true;
            _lastHitText = "DEFEATED!";
            _hitTextColor = Color.red;
            _hitTextTimer = 2.5f;

            var ragdoll = GetComponent<InsaneGun.Combat.EuphoriaRagdollController>();
            if (ragdoll == null) ragdoll = gameObject.AddComponent<InsaneGun.Combat.EuphoriaRagdollController>();
            Vector3 fatalPoint = transform.position + Vector3.up * 1.1f;
            Vector3 fatalDir = hitDirection != Vector3.zero ? hitDirection : transform.forward;
            ragdoll.TriggerEuphoriaRagdoll(fatalPoint, fatalDir, 18f, isFatal: true);

            StartCoroutine(RespawnRoutine());
        }

        private IEnumerator HitFlashRoutine()
        {
            // Simple visual hit feedback
            yield return new WaitForSeconds(0.12f);
        }

        private IEnumerator RespawnRoutine()
        {
            yield return new WaitForSeconds(4f);
            currentHealth = maxHealth;
            _isDead = false;

            var ragdoll = GetComponent<InsaneGun.Combat.EuphoriaRagdollController>();
            if (ragdoll != null && ragdoll.IsRagdoll)
            {
                ragdoll.ResetRagdollImmediate();
            }

            if (animator != null && animator.isActiveAndEnabled)
            {
                animator.CrossFadeInFixedTime("Combat_Empty", 0.25f, combatLayerIndex);
            }
        }

        private void Update()
        {
            if (_hitTextTimer > 0f)
                _hitTextTimer -= Time.deltaTime;
        }

        private void OnGUI()
        {
            if (Camera.main == null) return;

            var ragdoll = GetComponent<InsaneGun.Combat.EuphoriaRagdollController>();
            Vector3 basePos = (ragdoll != null && ragdoll.IsRagdoll) ? ragdoll.RagdollPelvisPosition : transform.position;
            Vector3 worldPos = basePos + Vector3.up * (_isDead ? 0.8f : 2.1f);
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);

            // Behind camera
            if (screenPos.z <= 0.5f || screenPos.z > 25f) return;

            float x = screenPos.x;
            float y = Screen.height - screenPos.y;

            // Health bar
            float barWidth = 100f;
            float barHeight = 10f;
            float healthPct = Mathf.Clamp01(currentHealth / maxHealth);

            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(new Rect(x - barWidth * 0.5f - 1, y - 1, barWidth + 2, barHeight + 2), Texture2D.whiteTexture);

            GUI.color = _isDead ? Color.gray : Color.Lerp(Color.red, Color.green, healthPct);
            GUI.DrawTexture(new Rect(x - barWidth * 0.5f, y, barWidth * healthPct, barHeight), Texture2D.whiteTexture);

            // Dummy label
            GUI.color = Color.white;
            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };
            GUI.Label(new Rect(x - 80, y - 20, 160, 18), dummyName, style);

            // Floating hit text
            if (_hitTextTimer > 0f)
            {
                float alpha = Mathf.Clamp01(_hitTextTimer);
                Color c = _hitTextColor;
                c.a = alpha;
                GUI.color = c;
                var hitStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 13,
                    fontStyle = FontStyle.Bold
                };
                float offsetY = (1.2f - _hitTextTimer) * 20f;
                GUI.Label(new Rect(x - 80, y - 40 - offsetY, 160, 22), _lastHitText, hitStyle);
            }
        }
    }
}
