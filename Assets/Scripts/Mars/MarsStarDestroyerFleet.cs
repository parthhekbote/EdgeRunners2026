using System.Collections;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Cyberpunk.Mars
{
    /// <summary>
    /// Manages the two massive Imperial Star Destroyer-style Dreadnought Capital Ships on Mars.
    /// Orchestrates:
    /// - Two colossal battlecruisers (SM_Ship_Colossal_01 & SM_Ship_Galactic_Carrier_01) scaled to 250m+ length.
    /// - Ominous atmospheric cloud-shelf breach with volcanic thruster plumes and sweeping searchlights.
    /// - Massive shadows sliding across the Martian dunes.
    /// - Ventral drop-bay illumination signaling the deployment of the Colossus Titan Mech and Pilot Army.
    /// </summary>
    public class MarsStarDestroyerFleet : MonoBehaviour
    {
        public static MarsStarDestroyerFleet Instance { get; private set; }

        [Header("Capital Ship Prefabs")]
        public GameObject colossalShipPrefab;
        public GameObject carrierShipPrefab;

        [Header("Fleet Ships")]
        public GameObject dreadnoughtAlpha; // Left Flank
        public GameObject dreadnoughtBeta;  // Right Flank

        [Header("Searchlights & Thrusters")]
        public Light searchlightAlpha;
        public Light searchlightBeta;
        public Light thrusterLightAlpha;
        public Light thrusterLightBeta;

        [Header("Audio")]
        public AudioClip ominousWarshipAudio;
        public AudioClip engineRumbleAudio;

        private AudioSource _rumbleSource;
        private AudioSource _hornSource;
        private Coroutine _descentCoroutine;

        // Waypoints (Descent from cloud shelf to low-altitude siege position)
        // The ships are huge: they hang far behind the arena so they frame the sky instead of covering it
        private const float AlphaScale = 0.5f;
        private const float BetaScale = 0.65f;

        private readonly Vector3 StartPosAlpha = new Vector3(-150f, 260f, 640f);
        private readonly Vector3 EndPosAlpha   = new Vector3(-130f, 140f, 440f);
        private readonly Quaternion RotAlpha   = Quaternion.Euler(14f, 18f, -4f);

        private readonly Vector3 StartPosBeta  = new Vector3(170f, 280f, 660f);
        private readonly Vector3 EndPosBeta    = new Vector3(150f, 150f, 460f);
        private readonly Quaternion RotBeta    = Quaternion.Euler(12f, -16f, 3f);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _rumbleSource = gameObject.AddComponent<AudioSource>();
            _rumbleSource.loop = true;
            _rumbleSource.playOnAwake = false;
            _rumbleSource.volume = 0.55f;
            _rumbleSource.spatialBlend = 0.4f;

            _hornSource = gameObject.AddComponent<AudioSource>();
            _hornSource.loop = false;
            _hornSource.playOnAwake = false;
            _hornSource.volume = 0.85f;

            LoadDefaultAssets();
            EnsureFleetInstantiated();
            SetFleetVisible(false); // the dreadnoughts only exist once the invasion starts
        }

        // ── Engines: the player-ship plasma flame (AAAStarshipThrusterSystem) placed in the real bell nozzles ──

        private CapitalShipThrusters.Rig _alphaRig;
        private CapitalShipThrusters.Rig _betaRig;

        private void BuildEngines()
        {
            if (_alphaRig == null && dreadnoughtAlpha != null) _alphaRig = CapitalShipThrusters.BuildColossal(dreadnoughtAlpha);
            if (_betaRig == null && dreadnoughtBeta != null) _betaRig = CapitalShipThrusters.BuildCarrier(dreadnoughtBeta);
        }

        /// <summary>0 = engines dark, 1 = full burn.</summary>
        public void SetEnginePower(float factor)
        {
            BuildEngines();
            CapitalShipThrusters.SetPower(_alphaRig, factor);
            CapitalShipThrusters.SetPower(_betaRig, factor);
        }

        private void SetFleetVisible(bool visible)
        {
            if (dreadnoughtAlpha != null)
            {
                dreadnoughtAlpha.transform.localScale = Vector3.one * AlphaScale;
                dreadnoughtAlpha.SetActive(visible);
            }
            if (dreadnoughtBeta != null)
            {
                dreadnoughtBeta.transform.localScale = Vector3.one * BetaScale;
                dreadnoughtBeta.SetActive(visible);
            }
        }

        public void LoadDefaultAssets()
        {
#if UNITY_EDITOR
            if (colossalShipPrefab == null)
                colossalShipPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonSciFiSpace/Prefabs/Vehicles/SM_Ship_Colossal_01.prefab");
            if (carrierShipPrefab == null)
                carrierShipPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonSciFiSpace/Prefabs/Vehicles/SM_Ship_Galactic_Carrier_01.prefab");

            if (ominousWarshipAudio == null)
                ominousWarshipAudio = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/echoes_of_lumen-ominous-music-595730.mp3");
            if (engineRumbleAudio == null)
                engineRumbleAudio = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Mechs/Mech_Chassis_Reactor_Loop.wav");
#endif
        }

        public void EnsureFleetInstantiated()
        {
            // 1. Dreadnought Alpha (Colossal Dreadnought)
            if (dreadnoughtAlpha == null)
            {
                var existing = GameObject.Find("Dreadnought_Alpha_Colossal");
                if (existing != null) dreadnoughtAlpha = existing;
            }

            if (dreadnoughtAlpha == null && colossalShipPrefab != null)
            {
                dreadnoughtAlpha = Instantiate(colossalShipPrefab, StartPosAlpha, RotAlpha, transform);
                dreadnoughtAlpha.name = "Dreadnought_Alpha_Colossal";
                dreadnoughtAlpha.transform.localScale = Vector3.one * AlphaScale;

                // Strip internal colliders for pure cinematic performance
                foreach (var c in dreadnoughtAlpha.GetComponentsInChildren<Collider>()) c.enabled = false;

                // Add searchlight
                GameObject sl = new GameObject("Searchlight_Alpha");
                sl.transform.SetParent(dreadnoughtAlpha.transform, false);
                sl.transform.localPosition = new Vector3(0f, -0.8f, 6.0f);
                sl.transform.localRotation = Quaternion.Euler(60f, 15f, 0f);
                searchlightAlpha = sl.AddComponent<Light>();
                searchlightAlpha.type = LightType.Spot;
                searchlightAlpha.color = new Color(0.95f, 0.95f, 1.0f);
                searchlightAlpha.range = 220f;
                searchlightAlpha.spotAngle = 42f;
                searchlightAlpha.intensity = 18f;

                // Add thruster light
                GameObject tl = new GameObject("Thruster_Light_Alpha");
                tl.transform.SetParent(dreadnoughtAlpha.transform, false);
                tl.transform.localPosition = new Vector3(0f, 0.5f, -9.0f);
                thrusterLightAlpha = tl.AddComponent<Light>();
                thrusterLightAlpha.type = LightType.Point;
                thrusterLightAlpha.color = new Color(0.15f, 0.65f, 1.0f);
                thrusterLightAlpha.range = 90f;
                thrusterLightAlpha.intensity = 12f;
            }

            // 2. Dreadnought Beta (Galactic Carrier)
            if (dreadnoughtBeta == null)
            {
                var existing = GameObject.Find("Dreadnought_Beta_Carrier");
                if (existing != null) dreadnoughtBeta = existing;
            }

            if (dreadnoughtBeta == null && carrierShipPrefab != null)
            {
                dreadnoughtBeta = Instantiate(carrierShipPrefab, StartPosBeta, RotBeta, transform);
                dreadnoughtBeta.name = "Dreadnought_Beta_Carrier";
                dreadnoughtBeta.transform.localScale = Vector3.one * BetaScale;

                foreach (var c in dreadnoughtBeta.GetComponentsInChildren<Collider>()) c.enabled = false;

                // Add searchlight
                GameObject sl = new GameObject("Searchlight_Beta");
                sl.transform.SetParent(dreadnoughtBeta.transform, false);
                sl.transform.localPosition = new Vector3(0f, -0.8f, 5.5f);
                sl.transform.localRotation = Quaternion.Euler(60f, -15f, 0f);
                searchlightBeta = sl.AddComponent<Light>();
                searchlightBeta.type = LightType.Spot;
                searchlightBeta.color = new Color(0.95f, 0.95f, 1.0f);
                searchlightBeta.range = 220f;
                searchlightBeta.spotAngle = 42f;
                searchlightBeta.intensity = 18f;

                // Add thruster light
                GameObject tl = new GameObject("Thruster_Light_Beta");
                tl.transform.SetParent(dreadnoughtBeta.transform, false);
                tl.transform.localPosition = new Vector3(0f, 0.5f, -8.5f);
                thrusterLightBeta = tl.AddComponent<Light>();
                thrusterLightBeta.type = LightType.Point;
                thrusterLightBeta.color = new Color(1.0f, 0.45f, 0.1f);
                thrusterLightBeta.range = 90f;
                thrusterLightBeta.intensity = 12f;
            }

        }

        /// <summary>
        /// Initiates the Star Wars Imperial Star Destroyer-style descent sequence.
        /// The two colossal dreadnoughts breach the cloud ceiling side by side,
        /// roaring with deep sub-bass and casting massive searchlights across Mars.
        /// </summary>
        public void StartFleetDescentSequence(float duration = 6.0f)
        {
            EnsureFleetInstantiated();
            if (dreadnoughtAlpha != null) dreadnoughtAlpha.transform.position = StartPosAlpha;
            if (dreadnoughtBeta != null) dreadnoughtBeta.transform.position = StartPosBeta;
            SetFleetVisible(true);

            if (_hornSource != null && ominousWarshipAudio != null)
            {
                _hornSource.clip = ominousWarshipAudio;
                _hornSource.Play();
            }

            if (_rumbleSource != null && engineRumbleAudio != null)
            {
                _rumbleSource.clip = engineRumbleAudio;
                _rumbleSource.volume = 0f; // swells in with the engines (see FleetDescentRoutine)
                _rumbleSource.Play();
            }

            if (_descentCoroutine != null) StopCoroutine(_descentCoroutine);
            _descentCoroutine = StartCoroutine(FleetDescentRoutine(duration));
        }

        private IEnumerator FleetDescentRoutine(float duration)
        {
            float timer = 0f;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                float norm = Mathf.Clamp01(timer / duration);
                float smooth = Mathf.SmoothStep(0f, 1f, norm);
                SetEnginePower(Mathf.Clamp01(norm * 2.5f)); // engines spool up as the ships breach the clouds
                if (_rumbleSource != null) _rumbleSource.volume = 0.55f * smooth;

                if (dreadnoughtAlpha != null)
                {
                    dreadnoughtAlpha.transform.position = Vector3.Lerp(StartPosAlpha, EndPosAlpha, smooth);
                    dreadnoughtAlpha.transform.rotation = RotAlpha;
                }

                if (dreadnoughtBeta != null)
                {
                    dreadnoughtBeta.transform.position = Vector3.Lerp(StartPosBeta, EndPosBeta, smooth);
                    dreadnoughtBeta.transform.rotation = RotBeta;
                }

                // Oscillate searchlights to sweep the crater floor
                float sweep = Mathf.Sin(timer * 1.5f) * 12f;
                if (searchlightAlpha != null)
                {
                    searchlightAlpha.transform.localRotation = Quaternion.Euler(58f + sweep * 0.4f, 15f + sweep, 0f);
                }
                if (searchlightBeta != null)
                {
                    searchlightBeta.transform.localRotation = Quaternion.Euler(58f - sweep * 0.4f, -15f - sweep, 0f);
                }

                yield return null;
            }

            SetEnginePower(1f);
            if (dreadnoughtAlpha != null) dreadnoughtAlpha.transform.position = EndPosAlpha;
            if (dreadnoughtBeta != null) dreadnoughtBeta.transform.position = EndPosBeta;
        }

        /// <summary>Fades the horn and engine rumble out so they end without a click.</summary>
        public void FadeOutAudio(float duration)
        {
            if (_rumbleSource != null) StartCoroutine(MarsSfx.FadeOutAndStop(_rumbleSource, duration));
            if (_hornSource != null) StartCoroutine(MarsSfx.FadeOutAndStop(_hornSource, duration));
        }

        public void FastForwardFleet()
        {
            EnsureFleetInstantiated();
            SetFleetVisible(true);
            SetEnginePower(1f);
            if (dreadnoughtAlpha != null) dreadnoughtAlpha.transform.position = EndPosAlpha;
            if (dreadnoughtBeta != null) dreadnoughtBeta.transform.position = EndPosBeta;
        }
    }
}
