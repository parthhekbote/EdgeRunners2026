using StudyMode.Polity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StudyMode.Polity.Editor
{
    public static class AdvancedPolityQuizSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Advanced_Polity_Quiz.unity";

        [MenuItem("Tools/Study Mode/Build Advanced Polity Quiz")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camera.tag = "MainCamera";
            camera.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
            camera.GetComponent<Camera>().backgroundColor = new Color(.012f, .02f, .045f);
            new GameObject("Advanced Polity Quiz", typeof(AdvancedPolityQuizController));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();
            Debug.Log("Advanced Polity Quiz scene created at " + ScenePath);
        }

        private static void AddToBuildSettings()
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
