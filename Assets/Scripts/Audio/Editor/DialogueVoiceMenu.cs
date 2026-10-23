using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Cyberpunk.Audio.Editor
{
    /// <summary>
    /// Cyberpunk > Voice: refreshes the generated dialogue voice clips (needs Python with edge-tts and internet).
    /// </summary>
    public static class DialogueVoiceMenu
    {
        private static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);

        [MenuItem("Cyberpunk/Voice/Generate Dialogue Voices (new lines only)")]
        public static void Generate()
        {
            string tools = Path.Combine(ProjectRoot, "Tools", "VoiceGen");
            string cmd = $"/c cd /d \"{tools}\" && python extract_dialogue.py && python generate_voices.py";
            var psi = new ProcessStartInfo("cmd.exe", cmd)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var proc = Process.Start(psi);
            if (proc == null) { Debug.LogError("[Voice] Could not start Python."); return; }

            proc.OutputDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) Debug.Log("[Voice] " + e.Data); };
            proc.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) Debug.LogWarning("[Voice] " + e.Data); };
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            proc.EnableRaisingEvents = true;
            proc.Exited += (_, __) => EditorApplication.delayCall += () =>
            {
                AssetDatabase.Refresh();
                Debug.Log("[Voice] Finished. New clips are imported.");
            };
            Debug.Log("[Voice] Generating voices for new dialogue lines (this can take a few minutes)...");
        }

        [MenuItem("Cyberpunk/Voice/Open Lines Missing A Voice")]
        public static void OpenMissing()
        {
            string path = Path.Combine(ProjectRoot, "VoiceLines", "missing.tsv");
            if (File.Exists(path)) EditorUtility.RevealInFinder(path);
            else EditorUtility.DisplayDialog("Voice", "No missing lines recorded yet. Play the game; lines without a voice clip are listed here.", "OK");
        }
    }
}
