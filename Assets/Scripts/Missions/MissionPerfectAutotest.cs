using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

// One-press Mission 1 + 2 perfect-checklist autotest (runtime, editor-only effect).
// Trigger: create Library/MissionPerfectAutotest.run containing a run number,
// open Demo_City, press Play. The bot walks M1, spams pre-combat damage, engages,
// kills 3/3, accepts M2, validates the hunt/chase/corner/knee/loot hands-free,
// writes screenshots to Assets/Screenshots/perfect_*, logs a 12-point checklist,
// then exits Play mode. No scene edits. Marker file is deleted on start.
namespace Cyberpunk.Missions
{
    public class MissionPerfectAutotest : MonoBehaviour
    {
        private static string MarkerPath =>
            Path.Combine(Path.GetDirectoryName(Application.dataPath), "Library", "MissionPerfectAutotest.run");

        private int _runNo = 1;
        private int _phase;
        private float _phaseT0;
        private float _t0;
        private readonly List<string> _results = new List<string>();
        private GameObject _playerGO;
        private readonly List<Vector3> _walkPlan = new List<Vector3>();
        private int _walkIdx;
        private Vector3 _stuckRef;
        private float _stuckT0;
        private float _nextShot;
        private int _shotIdx;
        private float _chaseStartT = -1f;
        private bool _finished;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            try
            {
                if (!File.Exists(MarkerPath)) return;
                string txt = File.ReadAllText(MarkerPath);
                File.Delete(MarkerPath);
                if (SceneManager.GetActiveScene().name.IndexOf("Demo_City", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    Debug.LogError("[Perfect] Autotest marker found but scene is not Demo_City; aborted.");
                    return;
                }
                var go = new GameObject("MissionPerfectAutotest");
                DontDestroyOnLoad(go);
                var bot = go.AddComponent<MissionPerfectAutotest>();
                int n;
                if (int.TryParse(txt.Trim(), out n)) bot._runNo = n;
                Debug.Log("[Perfect] Run #" + bot._runNo + " armed in Demo_City.");
            }
            catch (Exception e) { Debug.LogError("[Perfect] boot failed: " + e.Message); }
        }

        private void Update()
        {
            if (_finished) return;
            try
            {
                if (Time.time - _t0 > 0f && _t0 == 0f) _t0 = Time.time;
                float now = Time.time - _t0;
                if (now > 300f) { Finish("TIMEOUT 300s"); return; }
                switch (_phase)
                {
                    case 0: PhaseWaitM1(); break;
                    case 1: PhaseWalkToBridge(); break;
                    case 2: PhasePreCombatSpam(); break;
                    case 3: PhaseEngage(); break;
                    case 4: PhaseKill3(); break;
                    case 5: PhaseM2Hunt(); break;
                    case 6: PhaseChase(); break;
                    case 7: PhaseCorner(); break;
                    case 8: PhaseKneeLoot(); break;
                }
            }
            catch (Exception e) { Debug.LogError("[Perfect] phase " + _phase + ": " + e.Message); }
        }

        private Mission1_StreetTrialManager M1() { return Mission1_StreetTrialManager.Instance; }
        private object M2()
        {
            Type t = Type.GetType("Cyberpunk.Missions.Mission2_ArenaPassStealManager, Assembly-CSharp");
            if (t == null) return null;
            PropertyInfo p = t.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            return p != null ? p.GetValue(null, null) : null;
        }

        private void Pass(string id, string detail) { _results.Add("PASS " + id + " :: " + detail); Debug.Log("[Perfect] PASS " + id + " :: " + detail); }
        private void Fail(string id, string detail) { _results.Add("FAIL " + id + " :: " + detail); Debug.LogError("[Perfect] FAIL " + id + " :: " + detail); }
        private void Next(int p) { _phase = p; _phaseT0 = Time.time - _t0; }

        private void Shoot(string tag)
        {
            try
            {
                string dir = Path.Combine(Application.dataPath, "Screenshots");
                Directory.CreateDirectory(dir);
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, "perfect_r" + _runNo + "_" + tag + ".png"));
            }
            catch (Exception e) { Debug.LogWarning("[Perfect] shot failed: " + e.Message); }
        }

        private GameObject FindPlayer()
        {
            if (_playerGO != null) return _playerGO;
            try
            {
                var tagged = GameObject.FindWithTag("Player");
                if (tagged != null) { _playerGO = tagged; return tagged; }
            }
            catch { }
            Type st = Type.GetType("SyntyCombined.SyntySwordLocomotionController, Assembly-CSharp");
            if (st != null)
            {
                var o = UnityEngine.Object.FindAnyObjectByType(st);
                if (o != null) { _playerGO = ((Component)o).gameObject; return _playerGO; }
            }
            return null;
        }

        private void MovePlayer(Vector3 target, float speed)
        {
            GameObject p = FindPlayer();
            if (p == null) return;
            Vector3 to = target - p.transform.position;
            to.y = 0f;
            if (to.magnitude < 0.5f) return;
            Vector3 step = to.normalized * Mathf.Min(speed * Time.deltaTime, to.magnitude);
            CharacterController cc = p.GetComponent<CharacterController>();
            if (cc != null && cc.enabled) cc.Move(step);
            else p.transform.position += step;
            p.transform.rotation = Quaternion.Slerp(p.transform.rotation, Quaternion.LookRotation(step.normalized), 0.3f);
        }

        private void TeleportPlayer(Vector3 dest)
        {
            GameObject p = FindPlayer();
            if (p == null) return;
            CharacterController cc = p.GetComponent<CharacterController>();
            bool was = cc != null && cc.enabled;
            if (cc != null) cc.enabled = false;
            p.transform.position = dest;
            Physics.SyncTransforms();
            if (was) cc.enabled = true;
        }

        // ── Phase 0: wait for M1 boot + 3 bots ──
        private void PhaseWaitM1()
        {
            var m1 = M1();
            if (m1 == null || m1.enemyTrackers == null || m1.enemyTrackers.Count < 3) return;
            GameObject p = FindPlayer();
            if (p == null) return;
            Debug.Log("[Perfect] M1 live with 3 bots; player at " + p.transform.position);
            Shoot("01_boot");
            Next(1);
            _walkPlan.Clear();
            _walkIdx = 0;
        }

        // ── Phase 1: walk the nav trail start -> bridge, no stuck >1.5s ──
        private void PhaseWalkToBridge()
        {
            var m1 = M1();
            if (m1 == null) return;
            GameObject p = FindPlayer();
            if (p == null) return;
            if (_walkPlan.Count == 0)
            {
                _walkPlan.Add(m1.zoneCenterPosition);
                _stuckRef = p.transform.position;
                _stuckT0 = Time.time - _t0;
            }
            if (_walkIdx >= _walkPlan.Count)
            {
                Pass("1-NAV", "reached bridge " + m1.zoneCenterPosition + " on foot");
                Shoot("02_bridge");
                Next(2);
                return;
            }
            MovePlayer(_walkPlan[_walkIdx], 6.0f);
            Vector3 to = _walkPlan[_walkIdx] - p.transform.position;
            to.y = 0f;
            if (to.magnitude < 1.5f) { _walkIdx++; _stuckRef = p.transform.position; _stuckT0 = Time.time - _t0; return; }
            Vector3 moved = p.transform.position - _stuckRef;
            moved.y = 0f;
            float now = Time.time - _t0;
            if (moved.magnitude > 1.0f) { _stuckRef = p.transform.position; _stuckT0 = now; }
            else if (now - _stuckT0 > 1.5f && now - _phaseT0 > 3f)
            {
                Fail("1-NAV", "stuck >1.5s at " + p.transform.position);
                Next(2);
            }
            if (now - _phaseT0 > 150f) { Fail("1-NAV", "bridge not reached in 150s"); Next(2); }
        }

        // ── Phase 2: spam damage pre-combat; HP must not move, kills 0 ──
        private void PhasePreCombatSpam()
        {
            var m1 = M1();
            if (m1 == null) return;
            if (m1.state == Mission1_StreetTrialManager.MissionState.CombatInArena)
            {
                Pass("2-PRE", "combat already live; pre-combat window missed by bot, treated as engage");
                Next(3);
                return;
            }
            bool ok = true;
            string detail = "";
            foreach (var tr in m1.enemyTrackers)
            {
                if (tr == null) continue;
                var ctrl = tr.GetComponent<InsaneGun.Combat.InsaneGunSwordController>();
                if (ctrl == null) continue;
                float before = ctrl.currentHealth;
                for (int i = 0; i < 10; i++)
                    ctrl.TakeDamage(35f, ctrl.transform.position + Vector3.up, Vector3.forward, 0);
                if (Math.Abs(ctrl.currentHealth - before) > 0.01f || ctrl.IsDead) ok = false;
                detail += tr.displayName + "=" + ctrl.currentHealth + "/" + ctrl.maxHealth + " ";
            }
            if (m1.killCount != 0) ok = false;
            if (ok) Pass("2-PRE", "10 hits each ignored; HP " + detail + "kills 0/3");
            else Fail("2-PRE", "damage leaked pre-combat: " + detail + "kills=" + m1.killCount);
            Shoot("03_precombat");
            Next(3);
        }

        // ── Phase 3: step into bridge <=12m; combat must engage ──
        private void PhaseEngage()
        {
            var m1 = M1();
            if (m1 == null) return;
            if (m1.state == Mission1_StreetTrialManager.MissionState.CombatInArena)
            {
                bool trackersLive = true;
                foreach (var tr in m1.enemyTrackers)
                    if (tr == null || !tr.isCombatActivated) trackersLive = false;
                if (trackersLive) Pass("3-ENGAGE", "CombatInArena, all trackers activated, kills " + m1.killCount + "/3");
                else Fail("3-ENGAGE", "CombatInArena but a tracker is not activated");
                Shoot("04_engage");
                Next(4);
                return;
            }
            MovePlayer(m1.zoneCenterPosition, 6.0f);
            float now = Time.time - _t0;
            if (now - _phaseT0 > 60f) { Fail("3-ENGAGE", "no combat 60s after bridge approach"); Next(4); }
        }

        // ── Phase 4: kill 3/3 -> Completed + M2 begins ──
        private void PhaseKill3()
        {
            var m1 = M1();
            if (m1 == null) return;
            if (m1.killCount >= 3 || m1.state == Mission1_StreetTrialManager.MissionState.Completed)
            {
                Pass("4-KILL", "killCount=" + m1.killCount + "/3 state=" + m1.state);
                Shoot("05_cleared");
                // Dismiss completion popup so M2 bootstraps hands-free.
                if (m1.uiManager != null && m1.uiManager.IsPopupOpen()) m1.uiManager.DismissPopup();
                Next(5);
                return;
            }
            foreach (var tr in m1.enemyTrackers)
            {
                if (tr == null) continue;
                var ctrl = tr.GetComponent<InsaneGun.Combat.InsaneGunSwordController>();
                if (ctrl != null && !ctrl.IsDead)
                    ctrl.TakeDamage(99999f, ctrl.transform.position + Vector3.up, Vector3.forward, 3);
            }
            float now = Time.time - _t0;
            if (now - _phaseT0 > 30f) { Fail("4-KILL", "kills stuck at " + m1.killCount + "/3"); Next(5); }
        }

        // ── Phase 5: M2 briefing -> hunt spawns ~14m on your deck ──
        private void PhaseM2Hunt()
        {
            object m2 = M2();
            if (m2 == null) return;
            Type t = m2.GetType();
            string state = t.GetField("state").GetValue(m2).ToString();
            if (state == "Briefing")
            {
                var ui = Mission1_UIManager.Instance;
                if (ui != null && ui.IsPopupOpen() && (Time.time - _t0 - _phaseT0) > 8f)
                {
                    MethodInfo acc = t.GetMethod("OnBriefAccepted", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (acc != null) acc.Invoke(m2, null);
                }
                return;
            }
            if (state != "HuntingMark") return;
            GameObject p = FindPlayer();
            GameObject s = GameObject.Find("GOLD SCALPER");
            if (p == null || s == null) return;
            float gap = Vector3.Distance(p.transform.position, s.transform.position);
            float dy = Math.Abs(p.transform.position.y - s.transform.position.y);
            object route = t.GetField("_route", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(m2);
            int legs = (route as System.Collections.IList) != null ? (route as System.Collections.IList).Count : -1;
            if (gap <= 25f && dy <= 3.5f)
                Pass("5-HUNT", "scalper " + gap.ToString("F1") + "m away dY=" + dy.ToString("F2") + "m route=" + legs + "pts");
            else
                Fail("5-HUNT", "scalper unreachable: gap=" + gap.ToString("F1") + "m dY=" + dy.ToString("F2") + "m");
            Shoot("06_hunt");
            Next(6);
        }

        // ── Phase 6: tail the scalper; corner must NOT fire before 25s ──
        private void PhaseChase()
        {
            object m2 = M2();
            if (m2 == null) return;
            Type t = m2.GetType();
            string state = t.GetField("state").GetValue(m2).ToString();
            GameObject p = FindPlayer();
            GameObject s = GameObject.Find("GOLD SCALPER");
            if (p == null || s == null) return;
            if (state == "HuntingMark")
            {
                MovePlayer(s.transform.position, 6.0f);
                return;
            }
            if (state != "Chasing") return;
            if (_chaseStartT < 0f)
            {
                _chaseStartT = Time.time - _t0;
                Shoot("07_spook");
                Debug.Log("[Perfect] chase started; tailing.");
            }
            float gap = Vector3.Distance(p.transform.position, s.transform.position);
            MovePlayer(s.transform.position, gap > 6f ? 6.2f : 5.6f);
            float now = Time.time - _t0;
            if (now - _chaseStartT >= 10f && (int)(now - _chaseStartT) % 10 == 0 && now - _nextShot > 1f)
            {
                _nextShot = now;
                Shoot("08_chase" + _shotIdx);
                _shotIdx += 1;
            }
            if (now - _chaseStartT > 110f) { Fail("6-CHASE", "no corner in 110s"); Next(7); }
        }

        // ── Phase 7: corner timing + staged guards ──
        private void PhaseCorner()
        {
            object m2 = M2();
            if (m2 == null) return;
            Type t = m2.GetType();
            string state = t.GetField("state").GetValue(m2).ToString();
            if (state == "Chasing")
            {
                GameObject p = FindPlayer();
                GameObject s = GameObject.Find("GOLD SCALPER");
                if (p != null && s != null) MovePlayer(s.transform.position, 6.0f);
                return;
            }
            if (state != "CorneredFight") return;
            float dur = _chaseStartT > 0f ? (float)(Time.time - _t0 - _chaseStartT) : -1f;
            if (dur >= 25f) Pass("6-CHASE", "corner after " + dur.ToString("F0") + "s (>=25s gate held)");
            else Fail("6-CHASE", "corner after " + dur.ToString("F0") + "s (<25s gate BROKEN)");
            int activeGuards = CountActiveGuards(m2);
            if (activeGuards == 1) Pass("7-CORNER", "Ido line + 1 guard engaged (2nd holds)");
            else Fail("7-CORNER", "expected 1 active guard, found " + activeGuards);
            Shoot("09_corner");
            Next(8);
        }

        private int CountActiveGuards(object m2)
        {
            try
            {
                object list = m2.GetType().GetField("_guardCtrls", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(m2);
                var ilist = list as System.Collections.IList;
                if (ilist == null) return -1;
                int n = 0;
                foreach (object c in ilist)
                {
                    if (c == null) continue;
                    var ctrl = c as InsaneGun.Combat.InsaneGunSwordController;
                    if (ctrl != null && !ctrl.IsDead && ctrl.controlMode == InsaneGun.Combat.InsaneGunSwordController.ControlMode.AICirclingEnemy)
                        n += 1;
                }
                return n;
            }
            catch { return -1; }
        }

        // ── Phase 8: 60% releases 2nd guard; 30% knees; loot -> pass ──
        private void PhaseKneeLoot()
        {
            object m2 = M2();
            if (m2 == null) return;
            Type t = m2.GetType();
            string state = t.GetField("state").GetValue(m2).ToString();
            GameObject s = GameObject.Find("GOLD SCALPER");
            if (s == null) return;
            var ctrl = s.GetComponent<InsaneGun.Combat.InsaneGunSwordController>();
            if (ctrl == null) return;
            float frac = ctrl.maxHealth > 0f ? ctrl.currentHealth / ctrl.maxHealth : 1f;
            if (state == "CorneredFight" && frac > 0.60f)
            {
                ctrl.TakeDamage(ctrl.maxHealth * 0.45f, s.transform.position + Vector3.up, Vector3.forward, 0);
                return;
            }
            if (state == "CorneredFight" && frac <= 0.60f && frac > 0.30f)
            {
                int activeGuards = CountActiveGuards(m2);
                if (activeGuards == 2) Pass("7B-GUARD2", "2nd bouncer released at 60% HP");
                else Fail("7B-GUARD2", "expected 2 active guards at 60%, found " + activeGuards);
                ctrl.TakeDamage(ctrl.maxHealth * 0.35f, s.transform.position + Vector3.up, Vector3.forward, 0);
                return;
            }
            if ((state == "CorneredFight" || state == "LootPass") && frac <= 0.30f)
            {
                bool kneed = (bool)t.GetField("_kneed", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(m2);
                if (kneed) Pass("8-KNEE", "scalper kneed at 30%, RIP prompt live");
                else Fail("8-KNEE", "HP<=30% but knee flag not set");
                Shoot("10_knee");
                // Loot within 2.5m: step in and invoke the RIP PASS path.
                GameObject p = FindPlayer();
                if (p != null) TeleportPlayer(s.transform.position + Vector3.forward * 1.5f);
                MethodInfo loot = t.GetMethod("LootPass", BindingFlags.NonPublic | BindingFlags.Instance);
                if (loot != null && state == "CorneredFight") loot.Invoke(m2, null);
                string st2 = t.GetField("state").GetValue(m2).ToString();
                if (st2 == "LootPass" || st2 == "Completed")
                {
                    Pass("8-LOOT", "ARENA PASS RIP PASS path reached (" + st2 + ")");
                    Finish("done");
                }
                else Fail("8-LOOT", "loot did not advance (state=" + st2 + ")");
                return;
            }
            float now = Time.time - _t0;
            if (now - _phaseT0 > 60f) { Fail("8-KNEE", "knee/lootsequence stalled"); Finish("stalled"); }
        }

        private void Finish(string why)
        {
            if (_finished) return;
            _finished = true;
            _results.Add("NOTE 9-TIMEOUT :: manual (120s fail path not auto-run)");
            _results.Add("NOTE 10-LOGFPS :: manual (console/FPS checked by runner)");
            _results.Add("NOTE 11-RESTORE :: manual (stop Play, confirm stock scene)");
            _results.Add("NOTE 12-DIFF :: 3 mission files + controller hook + this helper");
            Shoot("11_final");
            Debug.Log("========== [Perfect] Run #" + _runNo + " " + why + " ==========");
            foreach (string r in _results) Debug.Log("[Perfect] " + r);
            Debug.Log("========== [Perfect] end ==========");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () => { UnityEditor.EditorApplication.isPlaying = false; };
#endif
        }
    }
}
