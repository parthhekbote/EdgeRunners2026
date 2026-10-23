using UnityEditor;
using MCPForUnity.Editor.Constants;
using MCPForUnity.Editor.Services.Transport.Transports;

namespace MCPForUnity.Editor
{
    [InitializeOnLoad]
    public static class AutoConnectMCP
    {
        static AutoConnectMCP()
        {
            // Configure Stdio as the primary transport matching the IDE MCP configuration
            EditorPrefs.SetBool(EditorPrefKeys.UseHttpTransport, false);

            EditorApplication.delayCall += () =>
            {
                try
                {
                    if (!StdioBridgeHost.IsRunning)
                    {
                        StdioBridgeHost.Start();
                        UnityEngine.Debug.Log("[MCPForUnity] StdioBridgeHost started on port: " + StdioBridgeHost.GetCurrentPort());
                    }
                }
                catch (System.Exception ex)
                {
                    UnityEngine.Debug.LogWarning("[MCPForUnity] AutoConnect warning: " + ex.Message);
                }
            };
        }

        [MenuItem("Tools/MCP For Unity/Start Bridge (Stdio)")]
        public static void ForceStartStdioBridge()
        {
            EditorPrefs.SetBool(EditorPrefKeys.UseHttpTransport, false);
            StdioBridgeHost.Start();
            UnityEngine.Debug.Log("[MCPForUnity] Manually started StdioBridgeHost on port: " + StdioBridgeHost.GetCurrentPort());
        }
    }
}
