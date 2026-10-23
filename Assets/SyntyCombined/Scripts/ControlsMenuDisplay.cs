using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SyntyCombined
{
    /// <summary>
    /// Manages the Controls & Keybindings UI tab in the Start Menu Hub.
    /// Toggles between Keyboard/Mouse, Xbox, and PlayStation controller diagrams and details.
    /// </summary>
    public class ControlsMenuDisplay : MonoBehaviour
    {
        public enum InputDeviceType
        {
            KeyboardMouse,
            XboxController,
            PlayStationController
        }

        [Header("Device Tabs")]
        public Button keyboardTabButton;
        public Button xboxTabButton;
        public Button playstationTabButton;

        [Header("Diagram Display Panels")]
        public GameObject keyboardDiagramPanel;
        public GameObject xboxDiagramPanel;
        public GameObject playstationDiagramPanel;

        [Header("Key Binding Legend (Optional Details)")]
        public TMP_Text moveBindingText;
        public TMP_Text cameraBindingText;
        public TMP_Text attackBindingText;
        public TMP_Text parryBindingText;
        public TMP_Text dodgeBindingText;
        public TMP_Text lockOnBindingText;
        public TMP_Text pauseBindingText;

        private void Start()
        {
            if (keyboardTabButton != null) keyboardTabButton.onClick.AddListener(() => SwitchDevice(InputDeviceType.KeyboardMouse));
            if (xboxTabButton != null) xboxTabButton.onClick.AddListener(() => SwitchDevice(InputDeviceType.XboxController));
            if (playstationTabButton != null) playstationTabButton.onClick.AddListener(() => SwitchDevice(InputDeviceType.PlayStationController));

            // Default view
            SwitchDevice(InputDeviceType.KeyboardMouse);
        }

        public void SwitchDevice(InputDeviceType device)
        {
            if (keyboardDiagramPanel) keyboardDiagramPanel.SetActive(device == InputDeviceType.KeyboardMouse);
            if (xboxDiagramPanel) xboxDiagramPanel.SetActive(device == InputDeviceType.XboxController);
            if (playstationDiagramPanel) playstationDiagramPanel.SetActive(device == InputDeviceType.PlayStationController);

            UpdateLegendText(device);
        }

        private void UpdateLegendText(InputDeviceType device)
        {
            switch (device)
            {
                case InputDeviceType.KeyboardMouse:
                    if (moveBindingText) moveBindingText.text = "W / A / S / D";
                    if (cameraBindingText) cameraBindingText.text = "Mouse Movement";
                    if (attackBindingText) attackBindingText.text = "Left Mouse Click";
                    if (parryBindingText) parryBindingText.text = "Right Mouse Click";
                    if (dodgeBindingText) dodgeBindingText.text = "Spacebar";
                    if (lockOnBindingText) lockOnBindingText.text = "Middle Click / Shift";
                    if (pauseBindingText) pauseBindingText.text = "ESC Key";
                    break;

                case InputDeviceType.XboxController:
                    if (moveBindingText) moveBindingText.text = "Left Stick";
                    if (cameraBindingText) cameraBindingText.text = "Right Stick";
                    if (attackBindingText) attackBindingText.text = "X / RB";
                    if (parryBindingText) parryBindingText.text = "LB / LT";
                    if (dodgeBindingText) dodgeBindingText.text = "A Button";
                    if (lockOnBindingText) lockOnBindingText.text = "Right Stick Click (RS)";
                    if (pauseBindingText) pauseBindingText.text = "Menu Button";
                    break;

                case InputDeviceType.PlayStationController:
                    if (moveBindingText) moveBindingText.text = "Left Analog Stick";
                    if (cameraBindingText) cameraBindingText.text = "Right Analog Stick";
                    if (attackBindingText) attackBindingText.text = "Square / R1";
                    if (parryBindingText) parryBindingText.text = "L1 / L2";
                    if (dodgeBindingText) dodgeBindingText.text = "Cross (X)";
                    if (lockOnBindingText) lockOnBindingText.text = "R3 Click";
                    if (pauseBindingText) pauseBindingText.text = "Options Button";
                    break;
            }
        }
    }
}
