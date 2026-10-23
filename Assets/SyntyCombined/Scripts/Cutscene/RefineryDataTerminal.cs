using System.Collections;
using UnityEngine;

namespace SyntyCombined.BlackMarket
{
    /// <summary>
    /// Interactive Omni-Corp secure terminal in the refinery sub-station.
    /// Alita must infiltrate this terminal and extract the encrypted Data Core for Voss.
    /// </summary>
    public class RefineryDataTerminal : MonoBehaviour
    {
        public static RefineryDataTerminal Instance { get; private set; }

        [Header("Terminal Settings")]
        public float interactionRange = 2.8f;
        public Vector3 promptOffset = new Vector3(0f, 1.8f, 0f);
        public Color terminalColor = new Color(0.2f, 0.85f, 1.0f); // Bright Cyan
        public bool isHacked = false;

        [Header("Audio & FX")]
        public AudioClip hackSuccessSFX;
        public GameObject sparkVFX;

        private Transform _playerTransform;
        private Light _terminalBeaconLight;
        private float _pulseTimer = 0f;
        private bool _isPlayerInRange = false;

        private void Awake()
        {
            if (Instance == null) Instance = this;
        }

        private void Start()
        {
            FindPlayer();
            SetupBeaconLight();
        }

        private void FindPlayer()
        {
            var p = GameObject.FindGameObjectWithTag("Player") ?? GameObject.Find("Alita") ?? GameObject.Find("Alita_Hero") ?? GameObject.Find("Alita_BattleAngel_Hero");
            if (p != null) _playerTransform = p.transform;
        }

        private void SetupBeaconLight()
        {
            var lightGo = new GameObject("Terminal_Holo_Light");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 1.5f, 0f);

            _terminalBeaconLight = lightGo.AddComponent<Light>();
            _terminalBeaconLight.type = LightType.Point;
            _terminalBeaconLight.range = 8.0f;
            _terminalBeaconLight.color = terminalColor;
            _terminalBeaconLight.intensity = 2.5f;
        }

        private void Update()
        {
            if (_playerTransform == null)
            {
                FindPlayer();
                if (_playerTransform == null) return;
            }

            // Pulse beacon light when mission is active
            if (!isHacked && BlackMarketStoryCombatDirector.Instance != null &&
                BlackMarketStoryCombatDirector.Instance.currentState == BlackMarketStoryState.InfiltrateRefineryTask)
            {
                _pulseTimer += Time.deltaTime * 4.0f;
                if (_terminalBeaconLight != null)
                {
                    _terminalBeaconLight.intensity = 2.0f + Mathf.PingPong(_pulseTimer, 1.5f) * 3.0f;
                }

                float dist = Vector3.Distance(transform.position, _playerTransform.position);
                _isPlayerInRange = dist <= interactionRange;

                if (_isPlayerInRange && (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F)))
                {
                    HackTerminal();
                }
            }
            else
            {
                _isPlayerInRange = false;
                if (_terminalBeaconLight != null)
                {
                    _terminalBeaconLight.intensity = isHacked ? 0.8f : 1.5f;
                    _terminalBeaconLight.color = isHacked ? Color.green : terminalColor;
                }
            }
        }

        public void HackTerminal()
        {
            if (isHacked) return;
            isHacked = true;

            StartCoroutine(ExecuteHackRoutine());
        }

        private IEnumerator ExecuteHackRoutine()
        {
            // Spawn sparks / hack feedback
            GameObject sparkPrefab = Resources.Load<GameObject>("PolygonParticles/Prefabs/FX_Explosion_01");
            if (sparkPrefab != null)
            {
                var fx = Instantiate(sparkPrefab, transform.position + Vector3.up * 1.2f, Quaternion.identity);
                fx.transform.localScale = Vector3.one * 0.4f;
                Destroy(fx, 2.5f);
            }

            // Play hack audio
            if (hackSuccessSFX != null)
            {
                AudioSource.PlayClipAtPoint(hackSuccessSFX, transform.position, 1.0f);
            }

            // Notify HUD & Director
            var hud = BlackMarketNPCDialogueHUD.GetOrCreate();

            // The ledger reveals what Voss was really shipping — and that the core is still alive
            hud.ShowTransmission(
                "REFINERY MAINFRAME",
                "OMNI-CORP // ASSET TRANSFER 09-C",
                "Asset URM-004, Antimatter Core. Destination: Cydonia Sub-Level 09. Status: active. Signature: Berserker-class. Heartbeat: sixty-one beats per minute.",
                new Color(0.20f, 0.85f, 1.0f),
                7f
            );

            hud.ShowMissionBanner("✅ DATA CORE EXTRACTED // RETURN TO KAELEN VOSS IN THE SECTOR 4 ALLEY");

            if (CyberpunkHUDController.Instance != null)
            {
                CyberpunkHUDController.Instance.ShowToast("DATA CORE RETRIEVED // RETURN TO VOSS", 4.0f);
            }

            yield return new WaitForSeconds(0.5f);

            if (BlackMarketStoryCombatDirector.Instance != null)
            {
                BlackMarketStoryCombatDirector.Instance.OnDataCoreRetrieved();
            }
        }

        private void OnGUI()
        {
            if (isHacked) return;
            if (BlackMarketStoryCombatDirector.Instance == null) return;
            if (BlackMarketStoryCombatDirector.Instance.currentState != BlackMarketStoryState.InfiltrateRefineryTask) return;
            if (!_isPlayerInRange) return;

            Cyberpunk.UI.CyberpunkInteractionPromptHUD.DrawPromptImmediate("E", "Hack Mainframe Terminal");
        }
    }
}
