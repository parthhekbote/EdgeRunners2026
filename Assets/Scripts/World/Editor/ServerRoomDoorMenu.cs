using UnityEditor;
using UnityEngine;

namespace Cyberpunk.World.Editor
{
    public static class ServerRoomDoorMenu
    {
        private const string MarkerName = "ServerRoom_Mission5_Door";

        [MenuItem("Cyberpunk/Server Room/Make Selected Door The Server Room Door")]
        public static void FromSelection()
        {
            var sel = Selection.activeGameObject;
            if (sel == null)
            {
                EditorUtility.DisplayDialog("Server Room", "Select the door object in the Hierarchy first.", "OK");
                return;
            }

            var renderers = sel.GetComponentsInChildren<Renderer>();
            Bounds b = renderers.Length > 0 ? renderers[0].bounds : new Bounds(sel.transform.position, Vector3.one);
            foreach (var r in renderers) b.Encapsulate(r.bounds);

            var existing = Object.FindAnyObjectByType<ServerRoomMissionDoor>();
            GameObject go = existing != null ? existing.gameObject : new GameObject(MarkerName);
            go.name = MarkerName;
            go.transform.position = new Vector3(b.center.x, b.min.y, b.center.z);
            go.transform.rotation = sel.transform.rotation;
            if (go.GetComponent<ServerRoomMissionDoor>() == null) go.AddComponent<ServerRoomMissionDoor>();

            Selection.activeGameObject = go;
            EditorUtility.SetDirty(go);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
            Debug.Log($"[ServerRoom] Mission 5 door placed at {go.transform.position}. Save the scene.");
        }
    }
}
