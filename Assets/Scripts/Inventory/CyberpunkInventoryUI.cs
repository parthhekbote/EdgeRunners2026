using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Cyberpunk.Inventory
{
    /// <summary>
    /// Cyberpunk-2077-style backpack docked bottom-left, replacing the static key
    /// chips. I toggles, Esc closes. Own canvas; hides (never deletes) the chips zone.
    /// Player locomotion locks while open; cursor frees. World keeps running.
    /// </summary>
    public class CyberpunkInventoryUI : MonoBehaviour
    {
        public static CyberpunkInventoryUI Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance._open;

        private static readonly Color PanelBg  = new Color(0.035f, 0.035f, 0.040f, 0.92f);
        private static readonly Color ChipBg   = new Color(0.04f, 0.04f, 0.045f, 0.85f);
        private static readonly Color Yellow   = new Color(0.988f, 0.933f, 0.039f);
        private static readonly Color Cyan     = new Color(0.000f, 0.847f, 1.000f);
        private static readonly Color White    = new Color(0.960f, 0.970f, 0.960f);
        private static readonly Color Muted    = new Color(0.620f, 0.660f, 0.680f);

        private Canvas _canvas;
        private GameObject _barObj;
        private TextMeshProUGUI _barText;
        private GameObject _panelObj;
        private TextMeshProUGUI _headText;
        private TextMeshProUGUI _detailTitle;
        private TextMeshProUGUI _detailBody;
        private TextMeshProUGUI _detailStats;
        private Button _btnPrimary;
        private TextMeshProUGUI _btnPrimaryText;
        private Button _btnSell;
        private TextMeshProUGUI _btnSellText;
        private GameObject _tabRow;
        private GameObject _listRoot;
        private readonly List<Button> _tabButtons = new List<Button>();
        private readonly List<GameObject> _rowObjs = new List<GameObject>();

        private TMP_FontAsset _headerFont;
        private TMP_FontAsset _boldFont;
        private TMP_FontAsset _bodyFont;

        private bool _open;
        private string _category = "ALL";
        private int _selIndex;
        private bool _wasCursorOverridden;
        private bool _prevLockedState;
        private float _chipsSweepTime;

        private static readonly string[] Tabs = { "ALL", "WEAPONS", "CONSUMABLES", "CYBERWARE", "JUNK" };

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _headerFont = Resources.Load<TMP_FontAsset>("SyntyUI/Fonts/Font_SciFi_Header");
            _boldFont = Resources.Load<TMP_FontAsset>("SyntyUI/Fonts/Font_SciFi_Bold");
            _bodyFont = Resources.Load<TMP_FontAsset>("SyntyUI/Fonts/Font_SciFi_Body");
            BuildUI();
            HideChipsZone();
            if (CyberpunkInventory.Instance != null)
                CyberpunkInventory.Instance.OnChanged += RefreshAll;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (CyberpunkInventory.Instance != null)
                CyberpunkInventory.Instance.OnChanged -= RefreshAll;
            RestoreCursor();
        }

        public static void EnsureInScene()
        {
            CyberpunkInventory.EnsureInScene();
            if (Instance != null) return;
            GameObject go = new GameObject("Cyberpunk_InventoryUI");
            go.AddComponent<CyberpunkInventoryUI>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootInventoryRuntime()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.name.IndexOf("Demo_City", System.StringComparison.OrdinalIgnoreCase) < 0) return;
            EnsureInScene();
        }

        private void HideChipsZone()
        {
            GameObject chips = GameObject.Find("Zone_BottomLeft_ActionChips");
            if (chips != null && chips.activeSelf)
            {
                chips.SetActive(false);
                Debug.Log("[InventoryUI] Bottom-left action chips hidden; backpack docked in their place (reversible).");
            }
        }

        // ── build ─────────────────────────────────────────────────────────────
        private void BuildUI()
        {
            GameObject canvasObj = new GameObject("Canvas_Hub_Inventory", typeof(RectTransform));
            canvasObj.transform.SetParent(transform, false);
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 45;
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();

            // Closed slim bar (fits the old chips strip)
            _barObj = Panel(canvasObj, "Bar_Backpack", new Vector2(0f, 0f), new Vector2(28f, 26f), new Vector2(560f, 30f));
            _barText = Text(_barObj, "Text_Bar", "", 13f, TextAlignmentOptions.Left, new Vector2(12f, 0f), new Vector2(536f, 28f), White, _boldFont);

            // Open panel (grows upward from the same dock)
            _panelObj = Panel(canvasObj, "Panel_Backpack", new Vector2(0f, 0f), new Vector2(28f, 26f), new Vector2(560f, 372f));
            _headText = Text(_panelObj, "Text_Head", "", 15f, TextAlignmentOptions.Left, new Vector2(12f, 160f), new Vector2(536f, 26f), White, _headerFont);

            _tabRow = new GameObject("Row_Tabs", typeof(RectTransform));
            _tabRow.transform.SetParent(_panelObj.transform, false);
            RectTransform tabRt = _tabRow.GetComponent<RectTransform>();
            tabRt.anchoredPosition = new Vector2(0f, 122f);
            tabRt.sizeDelta = new Vector2(560f, 28f);
            for (int i = 0; i < Tabs.Length; i++)
            {
                int idx = i;
                GameObject tab = new GameObject("Tab_" + Tabs[i], typeof(RectTransform));
                tab.transform.SetParent(_tabRow.transform, false);
                RectTransform trt = tab.GetComponent<RectTransform>();
                trt.anchoredPosition = new Vector2(-224f + idx * 112f, 0f);
                trt.sizeDelta = new Vector2(106f, 26f);
                Image bg = tab.AddComponent<Image>();
                bg.color = ChipBg;
                bg.raycastTarget = true;
                Button b = tab.AddComponent<Button>();
                TextMeshProUGUI tt = Text(tab, "T", Tabs[i], 11f, TextAlignmentOptions.Center, Vector2.zero, new Vector2(106f, 26f), Muted, _boldFont);
                tt.raycastTarget = false;
                b.onClick.AddListener(() => { _category = Tabs[idx]; _selIndex = 0; RefreshAll(); });
                _tabButtons.Add(b);
            }

            _listRoot = new GameObject("List_Items", typeof(RectTransform));
            _listRoot.transform.SetParent(_panelObj.transform, false);
            RectTransform lrt = _listRoot.GetComponent<RectTransform>();
            lrt.anchoredPosition = new Vector2(0f, -30f);
            lrt.sizeDelta = new Vector2(548f, 190f);

            _detailTitle = Text(_panelObj, "Text_DetailTitle", "", 14f, TextAlignmentOptions.Left, new Vector2(12f, -140f), new Vector2(536f, 24f), White, _boldFont);
            _detailStats = Text(_panelObj, "Text_DetailStats", "", 11.5f, TextAlignmentOptions.Left, new Vector2(12f, -166f), new Vector2(300f, 44f), Cyan, _bodyFont);
            _detailBody = Text(_panelObj, "Text_DetailBody", "", 11f, TextAlignmentOptions.Left, new Vector2(320f, -166f), new Vector2(228f, 44f), Muted, _bodyFont);

            _btnPrimary = MakeButton(_panelObj, "Btn_Primary", new Vector2(-140f, -158f), new Vector2(120f, 30f));
            _btnPrimaryText = _btnPrimary.GetComponentInChildren<TextMeshProUGUI>();
            _btnPrimary.onClick.AddListener(OnPrimaryPressed);
            _btnSell = MakeButton(_panelObj, "Btn_Sell", new Vector2(-8f, -158f), new Vector2(120f, 30f));
            _btnSellText = _btnSell.GetComponentInChildren<TextMeshProUGUI>();
            _btnSell.onClick.AddListener(OnSellPressed);

            _panelObj.SetActive(false);
            RefreshAll();
        }

        private GameObject Panel(GameObject root, string name, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            GameObject o = new GameObject(name, typeof(RectTransform));
            o.transform.SetParent(root.transform, false);
            RectTransform rt = o.GetComponent<RectTransform>();
            rt.anchorMin = anchor; rt.anchorMax = anchor;
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            Image bg = o.AddComponent<Image>();
            bg.color = PanelBg;
            bg.raycastTarget = false;
            Border(o, Yellow, 1f);
            return o;
        }

        private void Border(GameObject parent, Color c, float w)
        {
            Edge(parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, w), c);
            Edge(parent, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, w), c);
            Edge(parent, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(w, 0f), c);
            Edge(parent, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(w, 0f), c);
        }

        private void Edge(GameObject parent, Vector2 amin, Vector2 amax, Vector2 piv, Vector2 size, Color c)
        {
            GameObject o = new GameObject("Edge", typeof(RectTransform));
            o.transform.SetParent(parent.transform, false);
            RectTransform rt = o.GetComponent<RectTransform>();
            rt.anchorMin = amin; rt.anchorMax = amax; rt.pivot = piv;
            rt.anchoredPosition = Vector2.zero; rt.sizeDelta = size;
            Image img = o.AddComponent<Image>();
            img.color = c; img.raycastTarget = false;
        }

        private TextMeshProUGUI Text(GameObject parent, string name, string text, float size,
            TextAlignmentOptions align, Vector2 pos, Vector2 dim, Color color, TMP_FontAsset font)
        {
            GameObject o = new GameObject(name, typeof(RectTransform));
            o.transform.SetParent(parent.transform, false);
            RectTransform rt = o.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = dim;
            TextMeshProUGUI tmp = o.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = align;
            tmp.color = color;
            tmp.raycastTarget = false;
            return tmp;
        }

        private Button MakeButton(GameObject parent, string name, Vector2 pos, Vector2 size)
        {
            GameObject o = new GameObject(name, typeof(RectTransform));
            o.transform.SetParent(parent.transform, false);
            RectTransform rt = o.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            Image bg = o.AddComponent<Image>();
            bg.color = ChipBg;
            Border(o, Cyan, 1f);
            Button b = o.AddComponent<Button>();
            Text(b.gameObject, "T", "", 12f, TextAlignmentOptions.Center, Vector2.zero, size, Yellow, _boldFont).raycastTarget = false;
            return b;
        }

        // ── open/close + input ────────────────────────────────────────────────
        private void Update()
        {
            // The HUD can (re)build its chips after us — keep them docked away.
            _chipsSweepTime -= Time.unscaledDeltaTime;
            if (_chipsSweepTime <= 0f)
            {
                _chipsSweepTime = 2.0f;
                HideChipsZone();
            }
            if (Input.GetKeyDown(KeyCode.I)) Toggle();
            if (!_open) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { SetOpen(false); return; }
            if (Input.GetKeyDown(KeyCode.UpArrow)) { MoveSel(-1); return; }
            if (Input.GetKeyDown(KeyCode.DownArrow)) { MoveSel(1); return; }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) { OnPrimaryPressed(); return; }
            if (Input.GetKeyDown(KeyCode.S)) { OnSellPressed(); return; }
        }

        public void Toggle()
        {
            SetOpen(!_open);
        }

        public void SetOpen(bool open)
        {
            if (open == _open) return;
            if (open)
            {
                if (SyntyCombined.InGamePauseManager.IsPaused) return;
                var ui = Mission1Hook();
                if (ui != null && ui.IsPopupOpen()) return;
                CyberpunkInventory.EnsureInScene();
                if (!ResolvePlayer()) return;
            }
            _open = open;
            _panelObj.SetActive(open);
            _barObj.SetActive(!open);
            if (open)
            {
                LockPlayer(true);
                InsaneGun.Combat.SmoothOrbitCamera.cursorLockOverride = true;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                _selIndex = 0;
                RefreshAll();
                Debug.Log("[InventoryUI] Backpack open.");
            }
            else
            {
                RestoreCursor();
                LockPlayer(false);
                RefreshAll();
            }
        }

        private void RestoreCursor()
        {
            InsaneGun.Combat.SmoothOrbitCamera.cursorLockOverride = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private Cyberpunk.Missions.Mission1_UIManager Mission1Hook()
        {
            return Cyberpunk.Missions.Mission1_UIManager.Instance ??
                Object.FindAnyObjectByType<Cyberpunk.Missions.Mission1_UIManager>();
        }

        private bool ResolvePlayer()
        {
            var synty = Object.FindAnyObjectByType<SyntyCombined.SyntySwordLocomotionController>();
            if (synty != null) return true;
            if (GameObject.FindWithTag("Player") != null) return true;
            return false;
        }

        private void LockPlayer(bool locked)
        {
            var synty = Object.FindAnyObjectByType<SyntyCombined.SyntySwordLocomotionController>();
            if (synty != null) synty.inputLocked = locked;
            var hero = synty != null ? synty.gameObject : GameObject.FindWithTag("Player");
            if (hero != null)
            {
                var insane = hero.GetComponent<InsaneGun.Combat.InsaneGunSwordController>();
                if (insane != null) insane.inputLocked = locked;
            }
        }

        // ── data → widgets ────────────────────────────────────────────────────
        private List<ItemStack> VisibleStacks()
        {
            var inv = CyberpunkInventory.Instance;
            var out_ = new List<ItemStack>();
            if (inv == null) return out_;
            foreach (var s in inv.items)
            {
                var d = inv.GetDef(s.defId);
                if (d == null) continue;
                if (_category == "ALL") out_.Add(s);
                else if (_category == "WEAPONS" && d.category == ItemCategory.Weapon) out_.Add(s);
                else if (_category == "CONSUMABLES" && d.category == ItemCategory.Consumable) out_.Add(s);
                else if (_category == "CYBERWARE" && d.category == ItemCategory.Cyberware) out_.Add(s);
                else if (_category == "JUNK" && (d.category == ItemCategory.Junk || d.category == ItemCategory.Quest)) out_.Add(s);
            }
            return out_;
        }

        private void MoveSel(int dir)
        {
            var list = VisibleStacks();
            if (list.Count == 0) return;
            _selIndex = Mathf.Clamp(_selIndex + dir, 0, list.Count - 1);
            RefreshAll();
        }

        private void RefreshAll()
        {
            var inv = CyberpunkInventory.Instance;
            if (inv == null || _barText == null) return;
            string bar = $"<color=#FCEE0A>BACKPACK [I]</color>  €${inv.eddies}   {inv.TotalWeight():F1}/{inv.capacity:F0}kg";
            if (inv.equippedWeaponId != null)
            {
                var w = inv.GetDef(inv.equippedWeaponId);
                if (w != null) bar += $"   <color=#00D8FF>{w.displayName}</color>";
            }
            _barText.text = bar;
            if (!_open) return;

            _headText.text = $"<color=#FCEE0A>BACKPACK</color>   €${inv.eddies}    {inv.TotalWeight():F1} / {inv.capacity:F0} kg";
            for (int i = 0; i < _tabButtons.Count && i < Tabs.Length; i++)
            {
                var txt = _tabButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                if (txt != null) txt.color = (Tabs[i] == _category) ? Yellow : Muted;
            }

            foreach (var r in _rowObjs) Destroy(r);
            _rowObjs.Clear();
            var list = VisibleStacks();
            _selIndex = Mathf.Clamp(_selIndex, 0, Mathf.Max(0, list.Count - 1));
            for (int i = 0; i < list.Count; i++)
            {
                int idx = i;
                var stack = list[i];
                var def = inv.GetDef(stack.defId);
                GameObject row = new GameObject("Row_" + idx, typeof(RectTransform));
                row.transform.SetParent(_listRoot.transform, false);
                RectTransform rrt = row.GetComponent<RectTransform>();
                rrt.anchoredPosition = new Vector2(0f, 82f - idx * 24f);
                rrt.sizeDelta = new Vector2(548f, 22f);
                Image bg = row.AddComponent<Image>();
                bg.color = (idx == _selIndex) ? new Color(0.10f, 0.28f, 0.33f, 0.95f) : new Color(0.03f, 0.03f, 0.035f, 0.0f);
                Button b = row.AddComponent<Button>();
                b.onClick.AddListener(() => { _selIndex = idx; RefreshAll(); });

                GameObject sw = new GameObject("Sw", typeof(RectTransform));
                sw.transform.SetParent(row.transform, false);
                RectTransform srt = sw.GetComponent<RectTransform>();
                srt.anchoredPosition = new Vector2(-262f, 0f);
                srt.sizeDelta = new Vector2(12f, 12f);
                Image swi = sw.AddComponent<Image>();
                swi.color = def.tint;
                swi.raycastTarget = false;

                Color rc;
                if (!CyberpunkInventory.RarityColors.TryGetValue(def.rarity, out rc)) rc = White;
                string label = $"<color=#{ColorUtility.ToHtmlStringRGB(rc)}>{def.displayName}</color>";
                if (stack.count > 1) label += $"  <color=#9EA9ADFF>x{stack.count}</color>";
                if (inv.equippedWeaponId == def.id) label += "  <color=#FCEE0A>[EQUIPPED]</color>";
                else if (inv.equippedCyberware.Contains(def.id)) label += "  <color=#FCEE0A>[IMPLANTED]</color>";
                Text(row, "T", label, 12f, TextAlignmentOptions.Left, new Vector2(30f, 0f), new Vector2(400f, 22f), White, _bodyFont).raycastTarget = false;
                string right = def.category == ItemCategory.Consumable ? $"{def.healPct * 100f:F0}%"
                    : def.category == ItemCategory.Weapon ? $"+{def.damageBonus:F0}" : $"€${def.price}";
                Text(row, "V", right, 12f, TextAlignmentOptions.Right, new Vector2(120f, 0f), new Vector2(100f, 22f), Muted, _boldFont).raycastTarget = false;
                _rowObjs.Add(row);
            }

            UpdateDetails(list);
        }

        private void UpdateDetails(List<ItemStack> list)
        {
            var inv = CyberpunkInventory.Instance;
            _detailTitle.text = "";
            _detailStats.text = "";
            _detailBody.text = "Select an item. [Up/Down] browse — [Enter] use/equip — [S] sell — [I] close.";
            _btnPrimaryText.text = "";
            _btnSellText.text = "";
            _btnPrimary.gameObject.SetActive(false);
            _btnSell.gameObject.SetActive(false);
            if (inv == null || list.Count == 0 || _selIndex < 0 || _selIndex >= list.Count) return;

            var stack = list[_selIndex];
            var def = inv.GetDef(stack.defId);
            if (def == null) return;
            Color rc;
            if (!CyberpunkInventory.RarityColors.TryGetValue(def.rarity, out rc)) rc = White;
            _detailTitle.text = $"<color=#{ColorUtility.ToHtmlStringRGB(rc)}>{def.displayName}</color>" +
                (stack.count > 1 ? $"  x{stack.count}" : "");
            string stats = $"{def.rarity} {def.category}\nWeight {def.weight:F1} kg";
            if (def.damageBonus > 0f) stats += $"\nDamage +{def.damageBonus:F0} / Heavy +{def.heavyBonus:F0}";
            if (def.healPct > 0f) stats += $"\nHeals {def.healPct * 100f:F0}% HP";
            if (def.maxHpBonus > 0f) stats += $"\nMax HP +{def.maxHpBonus:F0}";
            if (def.speedBonus > 0f) stats += $"\nMove speed +{def.speedBonus:F1}";
            _detailStats.text = stats;
            _detailBody.text = def.desc;

            bool equipped = inv.equippedWeaponId == def.id || inv.equippedCyberware.Contains(def.id);
            if (def.category == ItemCategory.Weapon)
            {
                _btnPrimary.gameObject.SetActive(true);
                _btnPrimaryText.text = equipped ? "SHEATHE" : "EQUIP";
            }
            else if (def.category == ItemCategory.Consumable)
            {
                _btnPrimary.gameObject.SetActive(true);
                _btnPrimaryText.text = "USE";
            }
            else if (def.category == ItemCategory.Cyberware)
            {
                _btnPrimary.gameObject.SetActive(true);
                _btnPrimaryText.text = equipped ? "REMOVE" : "IMPLANT";
            }
            if (!inv.IsQuestItem(def.id) && !equipped)
            {
                _btnSell.gameObject.SetActive(true);
                _btnSellText.text = $"SELL +€${def.price}";
            }
        }

        private ItemStack SelectedStack()
        {
            var list = VisibleStacks();
            if (_selIndex < 0 || _selIndex >= list.Count) return null;
            return list[_selIndex];
        }

        private void OnPrimaryPressed()
        {
            var inv = CyberpunkInventory.Instance;
            var s = SelectedStack();
            if (inv == null || s == null) return;
            var def = inv.GetDef(s.defId);
            if (def == null) return;
            bool equipped = inv.equippedWeaponId == def.id || inv.equippedCyberware.Contains(def.id);
            if (def.category == ItemCategory.Weapon)
            {
                if (equipped) inv.UnequipWeapon();
                else if (inv.EquipWeapon(def.id)) Toast($"{def.displayName} equipped.");
            }
            else if (def.category == ItemCategory.Consumable)
            {
                if (inv.UseConsumable(def.id)) Toast($"{def.displayName} used.");
                else Toast("Already at full health.");
            }
            else if (def.category == ItemCategory.Cyberware)
            {
                if (inv.ToggleCyberware(def.id)) Toast($"{def.displayName} updated.");
            }
        }

        private void OnSellPressed()
        {
            var inv = CyberpunkInventory.Instance;
            var s = SelectedStack();
            if (inv == null || s == null) return;
            inv.SellOne(s.defId);
        }

        private void Toast(string msg)
        {
            var hud = SyntyCombined.CyberpunkHUDController.Instance;
            if (hud != null) hud.ShowToast(msg, 2.0f);
        }
    }
}
