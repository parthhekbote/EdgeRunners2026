using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace SyntyCombined
{
    /// <summary>
    /// AAA Cyberpunk Micro-Interaction Controller for UI Elements.
    /// Handles smooth hover scaling, glow intensity shifts, pointer audio feedback,
    /// and tactile click depressions.
    /// </summary>
    public class CyberUIInteractiveEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        [Header("Scale Micro-Animation")]
        public bool enableScale = true;
        public float hoverScale = 1.03f;
        public float clickScale = 0.97f;
        public float scaleSpeed = 16f;

        [Header("Glow & Color Shift")]
        public bool enableColorShift = true;
        public Graphic targetGraphic;
        public Outline targetOutline;
        public Color normalColor = Color.white;
        public Color hoverColor = new Color(0.2f, 0.95f, 1f, 1f);
        public Color clickColor = new Color(0f, 0.7f, 0.95f, 1f);

        [Header("Audio Feedback")]
        public bool playHoverAudio = true;
        public bool playClickAudio = true;

        private Vector3 _initialScale;
        private Vector3 _targetScale;
        private Color _targetColor;
        private bool _isHovered = false;
        private bool _isPressed = false;
        private bool _isSelected = false;

        private void Awake()
        {
            _initialScale = transform.localScale;
            _targetScale = _initialScale;

            if (targetGraphic == null) targetGraphic = GetComponent<Graphic>();
            if (targetOutline == null) targetOutline = GetComponent<Outline>();

            if (targetGraphic != null)
            {
                normalColor = targetGraphic.color;
            }
            _targetColor = normalColor;
        }

        private void OnEnable()
        {
            transform.localScale = _initialScale;
            _targetScale = _initialScale;
            _isHovered = false;
            _isPressed = false;
            _isSelected = false;
            UpdateVisualState();
        }

        private void Update()
        {
            // Smooth unscaled interpolation (works while paused with Time.timeScale = 0)
            if (enableScale && transform.localScale != _targetScale)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, _targetScale, Time.unscaledDeltaTime * scaleSpeed);
            }

            if (enableColorShift && targetGraphic != null && targetGraphic.color != _targetColor)
            {
                targetGraphic.color = Color.Lerp(targetGraphic.color, _targetColor, Time.unscaledDeltaTime * scaleSpeed);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHovered = true;
            UpdateVisualState();

            if (playHoverAudio && InGamePauseManager.Instance != null)
            {
                InGamePauseManager.Instance.PlayHoverAudio();
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovered = false;
            _isPressed = false;
            UpdateVisualState();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _isPressed = true;
            UpdateVisualState();

            if (playClickAudio && InGamePauseManager.Instance != null)
            {
                InGamePauseManager.Instance.PlayClickAudio();
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _isPressed = false;
            UpdateVisualState();
        }

        public void OnSelect(BaseEventData eventData)
        {
            _isSelected = true;
            UpdateVisualState();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            _isSelected = false;
            UpdateVisualState();
        }

        private void UpdateVisualState()
        {
            if (_isPressed)
            {
                _targetScale = _initialScale * clickScale;
                _targetColor = clickColor;
                if (targetOutline != null) targetOutline.effectColor = new Color(0f, 0.9f, 1f, 0.9f);
            }
            else if (_isHovered || _isSelected)
            {
                _targetScale = _initialScale * hoverScale;
                _targetColor = hoverColor;
                if (targetOutline != null) targetOutline.effectColor = new Color(0f, 0.95f, 1f, 0.85f);
            }
            else
            {
                _targetScale = _initialScale;
                _targetColor = normalColor;
                if (targetOutline != null) targetOutline.effectColor = new Color(0f, 0.85f, 1f, 0.35f);
            }
        }
    }
}
