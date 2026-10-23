using UnityEngine;

namespace PolygonCyberCity.NPC.Animation
{
    /// <summary>
    /// Interactive Smart Object in the cyberpunk metropolis (Benches, Vending Machines,
    /// Street Terminals / ATMs, Shopfront Displays, Food Trailers).
    /// Pedestrians path to this object, align with the interaction point, and play contextual urban activities.
    /// </summary>
    public class SmartObject : MonoBehaviour
    {
        [Header("Smart Object Settings")]
        public NPCActivityType activityType = NPCActivityType.Sit;
        public Transform interactionPoint;
        public float interactionDuration = 6.0f;
        public float cooldownAfterUse = 12.0f;
        public bool isOccupied = false;

        [System.NonSerialized]
        private float _lastOccupiedTime = -999f;
        [System.NonSerialized]
        private GameObject _occupant;

        public bool IsAvailable => !isOccupied && (_lastOccupiedTime < 0f || Time.time >= _lastOccupiedTime + cooldownAfterUse);
        public GameObject CurrentOccupant => _occupant;

        private void Awake()
        {
            _lastOccupiedTime = -999f;
            if (interactionPoint == null)
            {
                interactionPoint = transform;
            }
        }

        private void OnEnable()
        {
            if (_lastOccupiedTime == 0f)
            {
                _lastOccupiedTime = -999f;
            }
        }

        public bool TryOccupy(GameObject npc)
        {
            if (!IsAvailable) return false;

            isOccupied = true;
            _occupant = npc;
            return true;
        }

        public void Release(GameObject npc)
        {
            if (_occupant == npc || npc == null)
            {
                isOccupied = false;
                _occupant = null;
                _lastOccupiedTime = Time.time;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = isOccupied ? Color.red : (IsAvailable ? Color.green : Color.yellow);
            Transform t = interactionPoint != null ? interactionPoint : transform;
            Gizmos.DrawWireSphere(t.position, 0.4f);
            Gizmos.DrawRay(t.position, t.forward * 0.75f);
        }
    }
}
