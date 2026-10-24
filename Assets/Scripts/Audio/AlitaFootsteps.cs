using SyntyCombined.Cutscene;
using UnityEngine;

namespace Cyberpunk.Audio
{
    /// <summary>
    /// Footsteps for the player's Alita. One step sound fires each time a foot plants, read from the humanoid foot bones,
    /// so it stays in sync with whatever walk or run animation is playing. Without a humanoid rig it falls back to stride distance.
    /// Silent when she is airborne, dead, input-locked, driven by a cutscene (no movement key held), or the game is paused.
    /// </summary>
    public class AlitaFootsteps : MonoBehaviour
    {
        private const string ClipFolder = "Audio/Footsteps";
        private const float Volume = 0.9f;

        private AudioSource _src;
        private AudioClip[] _clips;
        private int _lastClip = -1;

        private Transform _player;
        private CharacterController _cc;
        private SyntyCombined.SyntySwordLocomotionController _synty;
        private InsaneGun.Combat.InsaneGunSwordController _insane;
        private Animator _anim;
        private Transform _footL, _footR;
        private Foot _fl, _fr;
        private Vector3 _lastPos;
        private float _speed, _stride, _scanTimer;

        private class Foot { public float min = 9f, max = -9f; public bool down; public float lastStep; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<AlitaFootsteps>() != null) return;
            var go = new GameObject("[AlitaFootsteps]");
            DontDestroyOnLoad(go);
            go.AddComponent<AlitaFootsteps>();
        }

        private void Awake()
        {
            _clips = Resources.LoadAll<AudioClip>(ClipFolder);
            _src = gameObject.AddComponent<AudioSource>();
            _src.playOnAwake = false;
            _src.spatialBlend = 0f;
            _src.bypassReverbZones = true;
            if (_clips == null || _clips.Length == 0) enabled = false;
        }

        private void Update()
        {
            if (!FindPlayer()) return;

            Vector3 d = _player.position - _lastPos;
            d.y = 0f;
            _lastPos = _player.position;
            float inst = Time.deltaTime > 0f ? d.magnitude / Time.deltaTime : 0f;
            if (inst > 14f) { _stride = 0f; return; } // teleport
            _speed = Mathf.Lerp(_speed, inst, 0.25f);

            if (!Walking()) { _stride = 0f; _fl.down = _fr.down = true; return; }

            if (_footL != null && _footR != null)
            {
                float baseY = _player.position.y;
                TrackFoot(_fl, _footL.position.y - baseY);
                TrackFoot(_fr, _footR.position.y - baseY);
            }
            else
            {
                _stride += d.magnitude;
                if (_stride > 0.85f) { _stride = 0f; Step(1f); }
            }
        }

        // A step is the foot dropping into the low part of its own height range, once per swing
        private void TrackFoot(Foot f, float h)
        {
            f.min = Mathf.Min(h, Mathf.Lerp(f.min, h, 0.002f));
            f.max = Mathf.Max(h, Mathf.Lerp(f.max, h, 0.002f));
            float range = f.max - f.min;
            if (range < 0.05f) return;

            if (!f.down && h < f.min + range * 0.22f && Time.time - f.lastStep > 0.18f)
            {
                f.down = true;
                f.lastStep = Time.time;
                Step(Mathf.Clamp(0.75f + _speed / 6f * 0.35f, 0.75f, 1.1f));
            }
            else if (f.down && h > f.min + range * 0.5f) f.down = false;
        }

        private void Step(float loudness)
        {
            int i = Random.Range(0, _clips.Length);
            if (i == _lastClip) i = (i + 1) % _clips.Length;
            _lastClip = i;
            _src.pitch = Random.Range(0.93f, 1.07f);
            _src.PlayOneShot(_clips[i], Volume * loudness);
        }

        private bool Walking()
        {
            if (_speed < 0.6f || Time.timeScale < 0.3f) return false;
            if (GlobalCutsceneState.IsCutsceneActive) return false;
            if (_synty != null && (!_synty.enabled || _synty.inputLocked)) return false;
            if (_insane != null && (!_insane.enabled || _insane.inputLocked || _insane.IsDead || _insane.IsDodging || _insane.IsJumping)) return false;
            return HoldingMoveKey() && Grounded();
        }

        // Scripted walks (cutscenes, spawns) move her with no key held, so only real input counts
        private static bool HoldingMoveKey()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && (kb.wKey.isPressed || kb.aKey.isPressed || kb.sKey.isPressed || kb.dKey.isPressed ||
                               kb.upArrowKey.isPressed || kb.downArrowKey.isPressed || kb.leftArrowKey.isPressed || kb.rightArrowKey.isPressed))
                return true;
            var pad = UnityEngine.InputSystem.Gamepad.current;
            if (pad != null && pad.leftStick.ReadValue().sqrMagnitude > 0.04f) return true;
            try { if (Mathf.Abs(Input.GetAxisRaw("Horizontal")) + Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.1f) return true; } catch { }
            return false;
        }

        private bool Grounded()
        {
            if (_cc != null && _cc.enabled && _cc.isGrounded) return true;
            float h = _cc != null ? _cc.height * 0.5f : 1f;
            return Physics.Raycast(_player.position + Vector3.up * h, Vector3.down, h + 0.35f, ~0, QueryTriggerInteraction.Ignore);
        }

        private bool FindPlayer()
        {
            if (_player != null && _player.gameObject.activeInHierarchy) return true;
            _scanTimer -= Time.unscaledDeltaTime;
            if (_scanTimer > 0f) return false;
            _scanTimer = 1f;

            var go = GameObject.FindWithTag("Player");
            if (go == null) return false;
            _player = go.transform;
            _cc = go.GetComponent<CharacterController>();
            _synty = go.GetComponent<SyntyCombined.SyntySwordLocomotionController>();
            _insane = go.GetComponent<InsaneGun.Combat.InsaneGunSwordController>();
            _anim = go.GetComponent<Animator>() ?? go.GetComponentInChildren<Animator>();
            _footL = _footR = null;
            if (_anim != null && _anim.isHuman)
            {
                _footL = _anim.GetBoneTransform(HumanBodyBones.LeftFoot);
                _footR = _anim.GetBoneTransform(HumanBodyBones.RightFoot);
            }
            _fl = new Foot(); _fr = new Foot();
            _lastPos = _player.position;
            _speed = 0f;
            return true;
        }
    }
}
