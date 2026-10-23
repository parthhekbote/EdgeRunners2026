using UnityEngine;

namespace Alita
{
    /// <summary>
    /// Controls Dr. Dyson Ido at the cybernetic diagnostic terminal console,
    /// procedurally typing keystrokes, monitoring neural reboot metrics,
    /// and executing the final awakening sequence for Alita.
    /// </summary>
    public class IdoTerminalTypingController : MonoBehaviour
    {
        [Header("Terminal & Lighting")]
        public Transform terminalConsole;
        public Light terminalScreenLight;
        public Renderer terminalScreenRenderer;

        [Header("Actor & Bones")]
        public Transform doctorRoot;
        public Transform spineBone;
        public Transform chestBone;
        public Transform headBone;
        public Transform leftUpperArm;
        public Transform leftLowerArm;
        public Transform leftHand;
        public Transform rightUpperArm;
        public Transform rightLowerArm;
        public Transform rightHand;

        [Header("Audio")]
        public AudioSource audioSource;

        [Header("Typing Animation Parameters")]
        public bool isTyping = true;
        public float typingSpeed = 14f;
        public float typingIntensity = 1.0f;
        public bool isSlammingEnter = false;

        private float _lastKeystrokeTime;
        private float _typingNoiseSeed;

        private Quaternion _spineRest = Quaternion.Euler(0f, 180f, 191.44f);
        private Quaternion _chestRest = Quaternion.Euler(0f, 180f, 191.25f);
        private Quaternion _headRest = Quaternion.Euler(80.86f, 270f, 0f);
        private Quaternion _restShL = Quaternion.Euler(0f, 301.97f, 45f);
        private Quaternion _restElL = Quaternion.Euler(0f, 285.70f, 0f);
        private Quaternion _restShR = Quaternion.Euler(0f, 59.68f, 45f);
        private Quaternion _restElR = Quaternion.Euler(0f, 80.57f, 0f);

        private void Awake()
        {
            CacheBones();
            _typingNoiseSeed = Random.Range(0f, 100f);
        }

        public void CacheBones()
        {
            var smrs = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var smr in smrs)
            {
                smr.updateWhenOffscreen = true;
            }

            var anim = GetComponentInChildren<Animator>();
            if (anim != null && anim.isHuman)
            {
                spineBone = anim.GetBoneTransform(HumanBodyBones.Spine);
                chestBone = anim.GetBoneTransform(HumanBodyBones.Chest);
                headBone = anim.GetBoneTransform(HumanBodyBones.Head);
                leftUpperArm = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                leftLowerArm = anim.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                leftHand = anim.GetBoneTransform(HumanBodyBones.LeftHand);
                rightUpperArm = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
                rightLowerArm = anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
                rightHand = anim.GetBoneTransform(HumanBodyBones.RightHand);

                if (spineBone != null) _spineRest = spineBone.localRotation;
                if (chestBone != null) _chestRest = chestBone.localRotation;
                if (headBone != null) _headRest = headBone.localRotation;

                anim.enabled = false;
            }
        }

        private void LateUpdate()
        {
            UpdateTypingKinematics(Time.time);
        }

        public void UpdateTypingKinematics(float time)
        {
            if (!isTyping) return;
            if (spineBone == null) CacheBones();

            // Spine subtle breathing
            float breathing = Mathf.Sin(time * 1.8f) * 1.2f;
            if (spineBone != null)
                spineBone.localRotation = _spineRest * Quaternion.Euler(0f, breathing, 0f);

            if (chestBone != null)
                chestBone.localRotation = _chestRest;

            // Head looks down at console screen, with subtle focus shift
            if (headBone != null)
            {
                float headNod = Mathf.Sin(time * 2.5f) * 1.5f;
                headBone.localRotation = _headRest * Quaternion.Euler(0f, headNod, 0f);
            }

            // Keystroke procedural finger & arm vibrations
            float tapL = Mathf.Sin(time * typingSpeed + _typingNoiseSeed) * 5.5f * typingIntensity;
            float tapR = Mathf.Cos(time * typingSpeed * 1.18f + _typingNoiseSeed) * 5.5f * typingIntensity;
            float swayL = Mathf.Sin(time * 2.2f) * 2.0f;
            float swayR = Mathf.Cos(time * 2.2f) * 2.0f;

            // Left hand typing on console keyboard
            if (leftUpperArm != null)
                leftUpperArm.localRotation = _restShL * Quaternion.Euler(15f, -15f + swayL, 65f);

            if (leftLowerArm != null)
                leftLowerArm.localRotation = _restElL * Quaternion.Euler(0f, -20f + tapL, 55f);

            if (leftHand != null)
                leftHand.localRotation = Quaternion.Euler(-20f + tapL * 1.2f, 0f, 0f);

            // Right hand typing or slamming enter key
            if (isSlammingEnter)
            {
                if (rightUpperArm != null)
                    rightUpperArm.localRotation = _restShR * Quaternion.Euler(35f, -15f, 40f);

                if (rightLowerArm != null)
                    rightLowerArm.localRotation = _restElR * Quaternion.Euler(0f, 45f, 25f);

                if (rightHand != null)
                    rightHand.localRotation = Quaternion.Euler(-40f, 0f, 0f);
            }
            else
            {
                if (rightUpperArm != null)
                    rightUpperArm.localRotation = _restShR * Quaternion.Euler(20f, -10f + swayR, 25f);

                if (rightLowerArm != null)
                    rightLowerArm.localRotation = _restElR * Quaternion.Euler(0f, 35f + tapR, 15f);

                if (rightHand != null)
                    rightHand.localRotation = Quaternion.Euler(-20f + tapR * 1.2f, 0f, 0f);
            }

            // Screen light flicker reflecting active computation
            if (terminalScreenLight != null)
            {
                float flicker = Mathf.PerlinNoise(time * 14f, 0f) * 0.45f;
                terminalScreenLight.intensity = (isSlammingEnter ? 3.6f : 2.2f) + flicker;
            }
        }
    }
}
