using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Alita.UI;

namespace Cyberpunk.Audio
{
    /// <summary>
    /// Layered orbital-station ambience for the Black Market (Demo_BlackMarket_01):
    /// 1. Crowd murmur (3D emitters spread around the market)
    /// 2. Random hydraulic one-shots
    /// 3. Low, muffled background music
    /// No drones, hums or ship whooshes: any leftover drone/hum loops in the scene are muted.
    /// Everything ducks while CyberpunkDialogueHUD has a conversation open.
    /// Clips load from Resources/Audio/BlackMarket.
    /// </summary>
    [DisallowMultipleComponent]
    public class BlackMarketAmbienceManager : MonoBehaviour
    {
        public static BlackMarketAmbienceManager Instance { get; private set; }

        private const string ResPath = "Audio/BlackMarket/";

        [Header("Volumes")]
        [Range(0f, 1f)] public float crowdVolume = 0.35f;
        [Range(0f, 1f)] public float musicVolume = 0.22f;
        [Range(0f, 1f)] public float oneShotVolume = 0.45f;

        [Header("Ducking")]
        [Range(0f, 1f)] public float duckMultiplier = 0.45f;
        public float duckSpeed = 3f;

        [Header("One-shot timing (seconds)")]
        public Vector2 hydraulicInterval = new Vector2(8f, 20f);

        private AudioSource _music;
        private AudioSource[] _crowd;
        private AudioClip[] _crowdClips, _hydraulicClips;
        private float _duck = 1f;
        private float _musicFade;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            if (IsBlackMarket(SceneManager.GetActiveScene())) Ensure();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (IsBlackMarket(scene)) Ensure();
        }

        private static bool IsBlackMarket(Scene scene)
        {
            return scene.name.IndexOf("BlackMarket", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static BlackMarketAmbienceManager Ensure()
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<BlackMarketAmbienceManager>();
            if (existing != null) { Instance = existing; return existing; }

            var go = new GameObject("[BlackMarket_Ambience]");
            Instance = go.AddComponent<BlackMarketAmbienceManager>();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (FindAnyObjectByType<AudioListener>() == null && Camera.main != null)
                Camera.main.gameObject.AddComponent<AudioListener>();

            _music = MakeBed("Music_Low", Load("BM_Music"));
            if (_music != null) _music.volume = 0f;

            _crowdClips = new[] { Load("npc_civilian_murmur_01"), Load("npc_civilian_murmur_02"), Load("npc_civilian_murmur_03") };
            _hydraulicClips = new[] { Load("Mech_Hydraulic_Actuator_01"), Load("Mech_Hydraulic_Actuator_02"), Load("Mech_Hydraulic_Actuator_03") };

            BuildCrowdEmitters();
        }

        private void Start()
        {
            StartCoroutine(MuteDroneLoopsRoutine());
            StartCoroutine(OneShotLoop(hydraulicInterval, PlayHydraulic));
        }

        private static AudioClip Load(string name) => Resources.Load<AudioClip>(ResPath + name);

        private AudioSource MakeBed(string name, AudioClip clip)
        {
            if (clip == null) return null;
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = true;
            src.spatialBlend = 0f;
            src.playOnAwake = false;
            src.volume = 0f;
            src.Play();
            return src;
        }

        // Murmur emitters scattered around the player's start area so the crowd feels placed in the hall
        private void BuildCrowdEmitters()
        {
            Vector3[] offsets =
            {
                new Vector3(14f, 2f, 10f), new Vector3(-16f, 2f, 8f),
                new Vector3(10f, 2f, -14f), new Vector3(-12f, 2f, -12f)
            };
            _crowd = new AudioSource[offsets.Length];
            for (int i = 0; i < offsets.Length; i++)
            {
                var clip = _crowdClips[i % _crowdClips.Length];
                if (clip == null) continue;
                var go = new GameObject("Crowd_" + i);
                go.transform.SetParent(transform, false);
                go.transform.position = offsets[i];
                var src = go.AddComponent<AudioSource>();
                src.clip = clip;
                src.loop = true;
                src.spatialBlend = 1f;
                src.minDistance = 6f;
                src.maxDistance = 45f;
                src.rolloffMode = AudioRolloffMode.Linear;
                src.volume = 0f;
                src.time = Random.Range(0f, clip.length);
                src.pitch = Random.Range(0.9f, 1.05f);
                src.Play();
                _crowd[i] = src;
            }
        }

        private void Update()
        {
            bool inDialogue = CyberpunkDialogueHUD.Instance != null && CyberpunkDialogueHUD.Instance.isDialogueActive;
            _duck = Mathf.MoveTowards(_duck, inDialogue ? duckMultiplier : 1f, duckSpeed * Time.unscaledDeltaTime);

            _musicFade = Mathf.MoveTowards(_musicFade, 1f, Time.unscaledDeltaTime / 6f);

            if (_music != null) _music.volume = musicVolume * _musicFade * _duck;
            if (_crowd != null)
                foreach (var c in _crowd)
                    if (c != null) c.volume = crowdVolume * _duck;
        }

        private IEnumerator OneShotLoop(Vector2 interval, System.Action action)
        {
            yield return new WaitForSeconds(Random.Range(interval.x * 0.3f, interval.x));
            while (true)
            {
                action();
                yield return new WaitForSeconds(Random.Range(interval.x, interval.y));
            }
        }

        private Vector3 RandomPointAroundListener(float minR, float maxR, float height)
        {
            Vector3 center = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            Vector2 dir = Random.insideUnitCircle.normalized * Random.Range(minR, maxR);
            return center + new Vector3(dir.x, height, dir.y);
        }

        // Silences drone/hum loops and whoosh sounds (flybys, pass-bys, dashes) that belong to the scene itself.
        // Sword slashes and other combat sounds are left alone.
        private IEnumerator MuteDroneLoopsRoutine()
        {
            var wait = new WaitForSeconds(0.15f);
            while (true)
            {
                foreach (var src in FindObjectsByType<AudioSource>(FindObjectsInactive.Include))
                {
                    if (src == null || src.transform.IsChildOf(transform)) continue;
                    if (src.GetComponent<SyntyCombined.Cutscene.BlackMarketArrivalCutsceneDirector>() != null) continue; // cutscene music and ship pass-by stay
                    string clipName = src.clip != null ? src.clip.name.ToLowerInvariant() : "";
                    string objName = src.gameObject.name.ToLowerInvariant();

                    bool droneLike = objName.Contains("hover_hum") || objName.Contains("drone") ||
                                     clipName.Contains("drone") || clipName.Contains("_hum") || clipName.Contains("hum_");
                    bool whooshLike = clipName.Contains("passby") || clipName.Contains("flyby") || clipName.Contains("swoosh") ||
                                      clipName.Contains("dash_cyber") || clipName.Contains("jump_takeoff") ||
                                      (clipName.Contains("whoosh") && !clipName.Contains("slash"));
                    if (!droneLike && !whooshLike) continue;

                    src.mute = true;
                    if (src.isPlaying) src.Stop();
                }
                yield return wait;
            }
        }

        private void PlayHydraulic()
        {
            var clip = _hydraulicClips[Random.Range(0, _hydraulicClips.Length)];
            if (clip == null) return;
            PlayAt(clip, RandomPointAroundListener(12f, 35f, 3f), oneShotVolume * 0.7f, Random.Range(0.8f, 1.1f), 50f);
        }

        private void PlayAt(AudioClip clip, Vector3 pos, float volume, float pitch, float maxDist)
        {
            var go = new GameObject("OneShot_" + clip.name);
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.volume = volume * _duck;
            src.pitch = pitch;
            src.spatialBlend = 1f;
            src.minDistance = 8f;
            src.maxDistance = maxDist;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.Play();
            Destroy(go, clip.length / Mathf.Max(0.1f, pitch) + 0.2f);
        }
    }
}
