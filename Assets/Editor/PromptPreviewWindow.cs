using UnityEngine;
using UnityEditor;

namespace Cyberpunk.Editor
{
    public class PromptPreviewWindow : EditorWindow
    {
        [MenuItem("Cyberpunk/Show Interaction Prompts Preview")]
        public static void Open()
        {
            var win = GetWindow<PromptPreviewWindow>("Interaction Prompts");
            win.minSize = new Vector2(440, 310);
            win.maxSize = new Vector2(440, 310);
            win.Show();
        }

        private void OnGUI()
        {
            // Dark futuristic backdrop
            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), new Color(0.04f, 0.07f, 0.10f, 1f));

            // Grid pattern
            Color gridCol = new Color(0.08f, 0.14f, 0.18f, 0.5f);
            for (float x = 0; x < position.width; x += 24)
            {
                EditorGUI.DrawRect(new Rect(x, 0, 1, position.height), gridCol);
            }
            for (float y = 0; y < position.height; y += 24)
            {
                EditorGUI.DrawRect(new Rect(0, y, position.width, 1), gridCol);
            }

            // Title
            GUIStyle header = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter
            };
            header.normal.textColor = new Color(0.15f, 0.94f, 0.98f, 0.9f);
            GUI.Label(new Rect(0, 14, position.width, 22), "AAA CYBERPUNK INTERACTION PROMPTS", header);

            // 1. Embark [Hold] (Spaceship Boarding - exact match to reference screenshot)
            Cyberpunk.UI.CyberpunkInteractionPromptHUD.DrawPromptImmediate("E", "Embark [Hold]", 0.50f, new Vector2(position.width * 0.5f, 65f));

            // 2. Enter Clinic Lab
            Cyberpunk.UI.CyberpunkInteractionPromptHUD.DrawPromptImmediate("E", "Enter Clinic Lab", 0f, new Vector2(position.width * 0.5f, 125f));

            // 3. Exit to Sector 4 Plaza
            Cyberpunk.UI.CyberpunkInteractionPromptHUD.DrawPromptImmediate("E", "Exit to Sector 4 Plaza", 0f, new Vector2(position.width * 0.5f, 185f));

            // 4. Ride Sky Lift
            Cyberpunk.UI.CyberpunkInteractionPromptHUD.DrawPromptImmediate("E", "Ride Sky Lift to Upper Plaza", 0f, new Vector2(position.width * 0.5f, 245f));
        }
    }
}
