using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Alita;
using InsaneGun.Combat;
using SyntyCombined;

namespace Cyberpunk.World
{
    /// <summary>
    /// The server room door in Demo_City for Mission 5. Locked until Ido has been thrown off the bridge,
    /// then [E] takes Alita to Scene_Blackwall_Cyberspace. Place one on the door (Cyberpunk > Server Room >
    /// Make Selected Door The Server Room Door builds it from the selected door object).
    /// </summary>
    public class ServerRoomMissionDoor : MonoBehaviour
    {
        [Header("Transition")]
        public string targetSceneName = "Scene_Blackwall_Cyberspace";
        public Vector3 targetSpawnPosition = new Vector3(0f, 0.08f, -92.0f);
        public float targetSpawnYaw = 0f;
        public string loadingTitle = "BLACKWALL DATACENTER // SERVER ARCHIVE 09";
        public string loadingStatus = "BYPASSING SUB-NET FIREWALL... LINKING TO SERVER ROOM...";

        [Header("Interaction")]
        public float interactionRadius = 3.5f;
        public KeyCode interactKey = KeyCode.E;

        private static readonly Color LockedColor = new Color(1f, 0.15f, 0.2f);
        private static readonly Color OpenColor = new Color(0.1f, 0.95f, 1f);

        private Transform _player;
        private Light _light;
        private bool _inRange;
        private bool _wasUnlocked;
        private float _deniedUntil;

        public static bool IsUnlocked => IdoBridgeAmbushDirector.Mission5Unlocked;

        private void Start()
        {
            var lightGo = new GameObject("ServerRoomDoor_Light");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 2.4f, 0.4f);
            _light = lightGo.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.range = 6f;
            _light.intensity = 2.5f;
            _light.color = LockedColor;
        }

        private void Update()
        {
            if (_player == null)
            {
                var p = GameObject.FindWithTag("Player");
                if (p != null) _player = p.transform;
                if (_player == null) return;
            }

            bool unlocked = IsUnlocked;
            if (_light != null)
            {
                // Red while locked, pulsing cyan once Mission 5 opens it
                _light.color = unlocked ? OpenColor : LockedColor;
                _light.intensity = unlocked ? 2.5f + Mathf.Sin(Time.time * 4f) * 1.0f : 1.8f;
            }

            if (unlocked && !_wasUnlocked)
            {
                _wasUnlocked = true;
                CyberpunkHUDController.Instance?.SetQuestObjective("MISSION 5 // THE SERVER ROOM",
                    "Ido's last signal traces to the server room. Reach the door and enter [E]");
            }

            if (ServerRoomDoorTrigger.IsInTransition) { _inRange = false; return; }

            _inRange = Vector3.Distance(transform.position, _player.position) <= interactionRadius;
            if (_inRange && (Input.GetKeyDown(interactKey) || Input.GetKeyDown(KeyCode.F)))
            {
                if (unlocked) StartCoroutine(Transition());
                else
                {
                    _deniedUntil = Time.time + 2.5f;
                    CyberpunkHUDController.Instance?.ShowToast("ACCESS DENIED // SERVER ROOM SEALED", 2.5f);
                }
            }
        }

        private void OnGUI()
        {
            if (!_inRange || ServerRoomDoorTrigger.IsInTransition) return;
            Cyberpunk.UI.CyberpunkInteractionPromptHUD.DrawPromptImmediate("E", IsUnlocked ? "Enter Server Room" : "Server Room (Sealed)");
        }

        private IEnumerator Transition()
        {
            ServerRoomDoorTrigger.IsInTransition = true;
            SetInputLocked(true);

            CyberpunkLoadingScreen.Show(loadingTitle);
            CyberpunkLoadingScreen.SetStatus(loadingStatus);

            ServerRoomDoorTrigger.PendingSpawnPos = targetSpawnPosition;
            ServerRoomDoorTrigger.PendingSpawnYaw = targetSpawnYaw;

            float elapsed = 0f;
            while (elapsed < 0.85f)
            {
                elapsed += Time.unscaledDeltaTime;
                CyberpunkLoadingScreen.SetProgress(Mathf.Clamp01(elapsed / 0.85f) * 0.5f);
                yield return null;
            }

            var op = SceneManager.LoadSceneAsync(targetSceneName);
            if (op != null)
            {
                op.allowSceneActivation = true;
                while (!op.isDone)
                {
                    CyberpunkLoadingScreen.SetProgress(Mathf.Lerp(0.5f, 0.95f, op.progress / 0.9f));
                    yield return null;
                }
            }
        }

        private void SetInputLocked(bool locked)
        {
            if (_player == null) return;
            var hero = _player.GetComponent<InsaneGunSwordController>();
            if (hero != null) hero.inputLocked = locked;
            var loco = _player.GetComponent<SyntySwordLocomotionController>();
            if (loco != null) loco.inputLocked = locked;
        }
    }
}
