using UnityEngine;

namespace SyntyCombined
{
    /// <summary>
    /// Interface for any combat target, enemy, or training dummy that can receive sword damage and hit reactions.
    /// </summary>
    public interface ISyntyDamageable
    {
        void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitDirection, bool isHeavy);
    }
}
