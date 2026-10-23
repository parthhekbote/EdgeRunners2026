using System.Collections;
using UnityEngine;
using SyntyCombined;
using WarriorAnims;

namespace PolygonMech
{
    /// <summary>
    /// Bridges ISyntyDamageable damage system into ExplosiveLLC WarriorController.
    /// Allows the Hammer Warrior player to take hits, stagger, and flash on hammer impacts.
    /// </summary>
    public class WarriorDamageBridge : MonoBehaviour, ISyntyDamageable
    {
        public float maxHealth = 500f;
        public float currentHealth { get; private set; }

        private WarriorController warriorController;
        private AudioSource audioSource;

        private void Awake()
        {
            currentHealth = maxHealth;
            warriorController = GetComponent<WarriorController>();
            audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();

            var scc = GetComponent<SuperCharacterController>();
            if (scc != null)
            {
                scc.Walkable = LayerMask.GetMask("Default", "Enemy", "SmallProps", "MediumProps");
            }
        }

        public void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitDirection, bool isHeavy)
        {
            currentHealth = Mathf.Max(0f, currentHealth - damage);

            if (warriorController != null)
            {
                warriorController.GetHit();
            }

            StartCoroutine(FlashRenderer());
        }

        private IEnumerator FlashRenderer()
        {
            var rends = GetComponentsInChildren<Renderer>();
            foreach (var r in rends)
            {
                if (r != null && r.material.HasProperty("_BaseColor"))
                {
                    Color prev = r.material.GetColor("_BaseColor");
                    r.material.SetColor("_BaseColor", Color.red);
                    yield return new WaitForSeconds(0.1f);
                    if (r != null) r.material.SetColor("_BaseColor", prev);
                }
            }
        }
    }
}
