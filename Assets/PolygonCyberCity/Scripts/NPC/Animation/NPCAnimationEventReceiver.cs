using UnityEngine;
using PolygonCyberCity.NPC.Spawner;

namespace PolygonCyberCity.NPC.Animation
{
    /// <summary>
    /// Receives AnimationEvents dispatched from Animation Clips / Animator States.
    /// Attach this component to the GameObject containing the Animator component.
    /// </summary>
    public class NPCAnimationEventReceiver : MonoBehaviour
    {
        private CyberNPCController _controller;

        private void Awake()
        {
            _controller = GetComponentInParent<CyberNPCController>();
        }

        public void OnAttackHitStart()
        {
            // Triggered when attack enters active damage frames
            if (_controller != null)
            {
                // Can be wired to weapon collision / damage trigger
            }
        }

        public void OnAttackHitEnd()
        {
            // Triggered when attack exits active damage frames
            if (_controller != null)
            {
                // Can disable weapon collision
            }
        }

        public void OnFootstep()
        {
            // Triggered on foot contact events
            if (_controller != null && _controller.audioSource != null)
            {
                // Play step sound if audio source present
            }
        }

        public static event System.Action<string, GameObject> OnDialogueGestureEvent;

        public void DialogueGesture(string gestureName)
        {
            OnDialogueGestureEvent?.Invoke(gestureName, gameObject);
            Debug.Log($"[DialogueGesture Event] NPC '{gameObject.name}' triggered gesture '{gestureName}'");
        }

        public void OnDialogueGesture(string gestureName)
        {
            DialogueGesture(gestureName);
        }

        public void OnDeathComplete()
        {
            // Triggered when death animation completes
            if (_controller != null)
            {
                // Can notify pool or disable object
            }
        }
    }
}
