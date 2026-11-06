// TEMPORARY: frame-exact clean capture rig for the cinematic edit. Safe to delete (Assets/_EditCapture).
using System.Collections;
using System.IO;
using UnityEngine;

public class CleanCapture : MonoBehaviour
{
    public static CleanCapture I;
    public string outRoot = "/Users/slaygen/Desktop/edit/cinematic/capture";
    public int quality = 92;
    public bool hideUI = true;
    public bool IsBusy;
    public int FramesWritten;
    Camera rig;

    public class ShotSpec
    {
        public string name = "shot";
        public float duration = 4f;
        public Vector3 p0, p1, e0, e1;
        public float fov0 = 50f, fov1 = 50f;
        public float roll0, roll1;
        public Transform follow;      // p0/p1 are offsets from this (world axes unless local)
        public bool local;
        public Transform lookAt;      // if set camera aims here instead of e0/e1
        public Vector3 lookOffset;
        public bool record = true;
        public float lead = 0f;       // seconds of unrecorded run-up (lets animation/physics settle)
    }

    void Awake() { I = this; Time.captureFramerate = 60; }
    void OnDestroy() { Time.captureFramerate = 0; if (I == this) I = null; }

    void LateUpdate()
    {
        if (hideUI && Time.frameCount % 4 == 0) HideUI();
    }

    public static void HideUI()
    {
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
            if (c.enabled) c.enabled = false;
    }

    public Camera EnsureRig()
    {
        if (rig != null) return rig;
        var main = Camera.main;
        GameObject go;
        if (main != null)
        {
            go = Instantiate(main.gameObject);
            foreach (var mb in go.GetComponents<MonoBehaviour>())
                if (!(mb is UnityEngine.Rendering.Universal.UniversalAdditionalCameraData)) Destroy(mb);
            var al = go.GetComponent<AudioListener>(); if (al) Destroy(al);
        }
        else { go = new GameObject(); go.AddComponent<Camera>(); }
        go.name = "CaptureRig"; go.tag = "Untagged";
        rig = go.GetComponent<Camera>();
        rig.depth = 1000; rig.enabled = true; rig.targetTexture = null;
        return rig;
    }

    static float Smooth(float u) { return u * u * (3f - 2f * u); }

    public void Play(ShotSpec s) { StartCoroutine(Run(s)); }

    // One still per (position, target) pair -> outRoot/_scout/<name>/s_XX.jpg  (for choosing camera setups)
    public void Scout(string name, Vector3[] positions, Vector3[] targets, float fov)
    {
        StartCoroutine(ScoutRun(name, positions, targets, fov));
    }

    IEnumerator ScoutRun(string name, Vector3[] positions, Vector3[] targets, float fov)
    {
        IsBusy = true; FramesWritten = 0;
        var cam = EnsureRig();
        string dir = Path.Combine(outRoot, "_scout", name);
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);
        for (int i = 0; i < positions.Length; i++)
        {
            cam.transform.position = positions[i];
            cam.transform.rotation = Quaternion.LookRotation(targets[i] - positions[i], Vector3.up);
            cam.fieldOfView = fov;
            yield return null; yield return null;
            yield return new WaitForEndOfFrame();
            var t = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(dir, "s_" + i.ToString("D2") + ".jpg"), t.EncodeToJPG(85));
            Destroy(t); FramesWritten++;
        }
        IsBusy = false;
    }

    IEnumerator Run(ShotSpec s)
    {
        IsBusy = true; FramesWritten = 0;
        var cam = EnsureRig();
        string dir = Path.Combine(outRoot, s.name);
        if (s.record) { if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir); }
        int lead = Mathf.RoundToInt(s.lead * 60f);
        int n = Mathf.Max(2, Mathf.RoundToInt(s.duration * 60f));
        for (int i = -lead; i < n; i++)
        {
            float u = Mathf.Clamp01(i / (float)(n - 1));
            float k = Smooth(u);
            Vector3 off = Vector3.Lerp(s.p0, s.p1, k);
            Vector3 pos = s.follow ? (s.local ? s.follow.TransformPoint(off) : s.follow.position + off) : off;
            cam.transform.position = pos;
            Quaternion rot = s.lookAt
                ? Quaternion.LookRotation((s.lookAt.position + s.lookOffset) - pos, Vector3.up)
                : Quaternion.Euler(Vector3.Lerp(s.e0, s.e1, k));
            rot *= Quaternion.Euler(0, 0, Mathf.Lerp(s.roll0, s.roll1, k));
            cam.transform.rotation = rot;
            cam.fieldOfView = Mathf.Lerp(s.fov0, s.fov1, k);
            yield return new WaitForEndOfFrame();
            if (s.record && i >= 0)
            {
                var t = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(dir, "f_" + i.ToString("D5") + ".jpg"), t.EncodeToJPG(quality));
                Destroy(t);
                FramesWritten++;
            }
        }
        IsBusy = false;
    }
}
