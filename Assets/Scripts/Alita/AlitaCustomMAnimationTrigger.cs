using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using InsaneGun.Combat;

namespace Alita
{
    /// <summary>
    /// Executes the custom animation from "X Bot.fbx" ("X_Bot_M_Action") when the player presses the M key.
    /// Can be attached directly to Alita or any player character.
    /// Coordinates seamlessly with InsaneGunSwordController or directly triggers via Animator.
    /// </summary>
    public class AlitaCustomMAnimationTrigger : MonoBehaviour
    {
        [Header("Key Configuration")]
        [Tooltip("The keyboard key that triggers the animation.")]
        public KeyCode triggerKey = KeyCode.M;

        [Header("Animation State Settings")]
        [Tooltip("State name in the Animator Controller.")]
        public string stateName = "X_Bot_M_Action";

        [Tooltip("Parameter name in Animator Controller.")]
        public string triggerParamName = "Trigger_XBot_M";

        [Tooltip("Duration in seconds the action pose or motion plays before returning to locomotion.")]
        public float actionDuration = 15.2f;

        [Header("State")]
        public bool isPlayingAction = false;

        private Animator _animator;
        private InsaneGunSwordController _swordController;
        private Coroutine _actionRoutine;

        private void Awake()
        {
            _swordController = GetComponent<InsaneGunSwordController>();
            _animator = GetComponent<Animator>();
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }
        }

        private void Update()
        {
            bool mPressed = false;

            // New Input System
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.mKey.wasPressedThisFrame)
            {
                mPressed = true;
            }

            // Legacy Input Manager fallback
            try
            {
                if (Input.GetKeyDown(triggerKey))
                {
                    mPressed = true;
                }
            }
            catch { }

            if (mPressed)
            {
                PlayCustomAction();
            }
        }

        /// <summary>
        /// Plays the custom action from X Bot.fbx on Alita.
        /// </summary>
        public void PlayCustomAction()
        {
            // If InsaneGunSwordController is attached, let it drive the combat-integrated action
            if (_swordController != null)
            {
                if (_swordController.IsDead) return;
                _swordController.ExecuteCustomAnimationAction();
                return;
            }

            if (_animator == null)
            {
                _animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
            }

            if (_animator != null)
            {
                if (_actionRoutine != null) StopCoroutine(_actionRoutine);
                _actionRoutine = StartCoroutine(DirectAnimationRoutine());
            }
        }

        private IEnumerator DirectAnimationRoutine()
        {
            isPlayingAction = true;

            // Try CrossFade to state on base layer first
            if (_animator.HasState(0, Animator.StringToHash(stateName)))
            {
                _animator.CrossFadeInFixedTime(stateName, 0.15f, 0, 0f);
            }
            // Or combat actions layer if multi-layered (e.g. SyntyCombined Layer 3)
            else if (_animator.layerCount > 3 && _animator.HasState(3, Animator.StringToHash(stateName)))
            {
                _animator.SetLayerWeight(3, 1f);
                _animator.CrossFadeInFixedTime(stateName, 0.15f, 3, 0f);
            }
            else
            {
                // Fallback to trigger parameter
                _animator.SetTrigger(triggerParamName);
            }

            yield return new WaitForSeconds(actionDuration);

            if (_animator.HasState(0, Animator.StringToHash("Locomotion")))
            {
                _animator.CrossFadeInFixedTime("Locomotion", 0.2f, 0, 0f);
            }

            isPlayingAction = false;
            _actionRoutine = null;
        }
    }
}
