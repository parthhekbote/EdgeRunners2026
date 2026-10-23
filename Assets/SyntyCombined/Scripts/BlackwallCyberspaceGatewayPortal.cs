using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SyntyCombined
{
    /// <summary>
    /// Controls gateway portal visibility between the Datacenter Server Room and Blackwall Cyberspace.
    /// In Blackwall Cyberspace (Z >= thresholdZ), ONLY THE DOOR is visible — no building,
    /// no outer walls, and no server racks are rendered in cyberspace.
    /// When on the Server Room side (Z < thresholdZ), the server room interior is fully rendered.
    /// Operates seamlessly in both the Unity Editor Scene View and Play Mode.
    /// </summary>
    [ExecuteAlways]
    public class BlackwallCyberspaceGatewayPortal : MonoBehaviour
    {
        [Header("Portal References")]
        [Tooltip("The root GameObject containing all server room interior contents (racks, towers, consoles, floor)")]
        public GameObject serverRoomInterior;

        [Tooltip("Z coordinate threshold dividing Server Room and Blackwall Cyberspace")]
        public float thresholdZ = -9.8f;

        [Header("Editor Debug")]
        [Tooltip("Check this in the Inspector to force the interior to remain visible in the Editor for editing.")]
        public bool forceShowInteriorInEditor = false;

        [Header("Audio Sources")]
        [Tooltip("Server room music soundtrack — plays inside server room, muted near Blackwall in Cyberspace")]
        public AudioSource serverRoomMusicAudio;

        [Tooltip("3D Blackwall barrier audio (ghost attack sound) — muted in server room, plays near Blackwall")]
        public AudioSource blackwallWallAudio;

        [Range(0f, 1f)]
        public float maxMusicVolume = 0.50f;

        [Range(0f, 1f)]
        public float maxWallVolume = 0.65f;

        [Tooltip("Fade speed for audio when transitioning between room and cyberspace")]
        public float audioFadeSpeed = 2.0f;

        // Backwards compatibility alias
        public AudioSource blackwallAtmosphereAudio
        {
            get => serverRoomMusicAudio;
            set => serverRoomMusicAudio = value;
        }

        private void Awake()
        {
            AutoFindAudioSources();
            if (Application.isPlaying)
            {
                CheckVisibility(Camera.main);
            }
        }

        private void Start()
        {
            AutoFindAudioSources();
            if (Application.isPlaying)
            {
                CheckVisibility(Camera.main);
            }
        }

        public void AutoFindAudioSources()
        {
            if (serverRoomMusicAudio == null)
            {
                var musicGO = GameObject.Find("Server_Room_Music_AudioSource");
                if (musicGO == null) musicGO = GameObject.Find("Blackwall_Atmosphere_AudioSource");
                if (musicGO != null) serverRoomMusicAudio = musicGO.GetComponent<AudioSource>();
            }

            if (blackwallWallAudio == null)
            {
                var wallGO = GameObject.Find("Blackwall_Canonical_Lidar_Wall");
                if (wallGO != null) blackwallWallAudio = wallGO.GetComponent<AudioSource>();
            }
        }

        private void OnEnable()
        {
#if UNITY_EDITOR
            EditorApplication.update += EditorUpdate;
#endif
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            EditorApplication.update -= EditorUpdate;
#endif
        }

        private void Update()
        {
            if (Application.isPlaying)
            {
                CheckVisibility(Camera.main);
            }
        }

#if UNITY_EDITOR
        private void EditorUpdate()
        {
            if (Application.isPlaying) return;

            if (forceShowInteriorInEditor)
            {
                if (serverRoomInterior != null && !serverRoomInterior.activeSelf)
                    serverRoomInterior.SetActive(true);
                return;
            }

            Camera cam = null;
            if (SceneView.lastActiveSceneView != null && SceneView.lastActiveSceneView.camera != null)
            {
                cam = SceneView.lastActiveSceneView.camera;
            }
            else if (SceneView.sceneViews != null && SceneView.sceneViews.Count > 0)
            {
                var sv = SceneView.sceneViews[0] as SceneView;
                if (sv != null && sv.camera != null) cam = sv.camera;
            }

            if (cam != null)
            {
                CheckVisibility(cam);
            }
        }
#endif

        public void CheckVisibility(Camera cam)
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            // In Blackwall Cyberspace (Z >= thresholdZ), ONLY the door is visible.
            // The server room interior (racks, towers, floor, walls, roof) is hidden.
            bool inCyberspace = cam.transform.position.z >= thresholdZ;
            bool shouldShow = !inCyberspace;

            if (serverRoomInterior != null && serverRoomInterior.activeSelf != shouldShow)
            {
                serverRoomInterior.SetActive(shouldShow);
            }

            UpdateAudio(inCyberspace);
        }

        private void UpdateAudio(bool inCyberspace)
        {
            // Inside server room (!inCyberspace): Music PLAYS, Blackwall barrier sound is MUTED
            // In Cyberspace / near Blackwall (inCyberspace): Music is MUTED, Blackwall barrier sound PLAYS
            float targetMusic = inCyberspace ? 0.0f : maxMusicVolume;
            float targetWall = inCyberspace ? maxWallVolume : 0.0f;

            if (Application.isPlaying)
            {
                if (serverRoomMusicAudio != null)
                {
                    serverRoomMusicAudio.volume = Mathf.MoveTowards(
                        serverRoomMusicAudio.volume,
                        targetMusic,
                        Time.deltaTime * audioFadeSpeed
                    );
                }

                if (blackwallWallAudio != null)
                {
                    blackwallWallAudio.volume = Mathf.MoveTowards(
                        blackwallWallAudio.volume,
                        targetWall,
                        Time.deltaTime * audioFadeSpeed
                    );
                }
            }
            else
            {
                if (serverRoomMusicAudio != null)
                    serverRoomMusicAudio.volume = targetMusic;

                if (blackwallWallAudio != null)
                    blackwallWallAudio.volume = targetWall;
            }
        }
    }
}
