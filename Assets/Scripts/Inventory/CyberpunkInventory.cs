using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cyberpunk.Inventory
{
    public enum ItemCategory { Weapon, Consumable, Junk, Cyberware, Quest }
    public enum ItemRarity { Common, Uncommon, Rare, Epic, Legendary }

    [Serializable]
    public class ItemDef
    {
        public string id;
        public string displayName;
        public ItemCategory category;
        public ItemRarity rarity;
        public float damageBonus;
        public float heavyBonus;
        public float healPct;
        public float maxHpBonus;
        public float speedBonus;
        public int price;
        public float weight;
        public string desc;
        public Color tint;

        public ItemDef(string id, string name, ItemCategory cat, ItemRarity rar,
            float dmg, float heavy, float heal, float hp, float spd, int price, float weight,
            string desc, Color tint)
        {
            this.id = id; displayName = name; category = cat; rarity = rar;
            damageBonus = dmg; heavyBonus = heavy; healPct = heal; maxHpBonus = hp; speedBonus = spd;
            this.price = price; this.weight = weight; this.desc = desc; this.tint = tint;
        }
    }

    [Serializable]
    public class ItemStack
    {
        public string defId;
        public int count;
        public ItemStack(string defId, int count) { this.defId = defId; this.count = count; }
    }

    /// <summary>
    /// Player backpack state (session-scoped). Pure data + rules; UI lives in
    /// CyberpunkInventoryUI. Never touches other systems except through their own
    /// public health/damage fields (same pattern as the mission managers).
    /// </summary>
    public class CyberpunkInventory : MonoBehaviour
    {
        public static CyberpunkInventory Instance { get; private set; }

        public static readonly Dictionary<ItemRarity, Color> RarityColors = new Dictionary<ItemRarity, Color>
        {
            { ItemRarity.Common,    new Color(0.78f, 0.78f, 0.78f) },
            { ItemRarity.Uncommon,  new Color(0.20f, 0.80f, 0.30f) },
            { ItemRarity.Rare,      new Color(0.25f, 0.60f, 1.00f) },
            { ItemRarity.Epic,      new Color(0.70f, 0.40f, 0.90f) },
            { ItemRarity.Legendary, new Color(1.00f, 0.55f, 0.10f) },
        };

        public static readonly Dictionary<string, ItemDef> Catalog = BuildCatalog();

        private static Dictionary<string, ItemDef> BuildCatalog()
        {
            var c = new Dictionary<string, ItemDef>();
            Color steel = new Color(0.55f, 0.75f, 0.85f);
            Color amber = new Color(1.00f, 0.75f, 0.25f);
            Color red = new Color(1.00f, 0.30f, 0.30f);
            Color green = new Color(0.30f, 0.95f, 0.55f);
            Color violet = new Color(0.70f, 0.50f, 1.00f);
            Color bone = new Color(0.80f, 0.80f, 0.75f);

            c["thermal_katana"] = new ItemDef("thermal_katana", "Thermal Katana", ItemCategory.Weapon, ItemRarity.Common,
                4f, 6f, 0f, 0f, 0f, 120, 3.0f, "Ido's spare street blade. Hot edge, honest weight.", steel);
            c["rogue_sidearm"] = new ItemDef("rogue_sidearm", "Rogue Sidearm", ItemCategory.Weapon, ItemRarity.Uncommon,
                6f, 9f, 0f, 0f, 0f, 220, 2.0f, "Corp-security pistol, serials filed off. Crack shot.", amber);
            c["damascus_edge"] = new ItemDef("damascus_edge", "Damascus Edge", ItemCategory.Weapon, ItemRarity.Rare,
                9f, 14f, 0f, 0f, 0f, 450, 2.6f, "Folded Martian steel. Sings when it cuts.", violet);

            c["maxdoc"] = new ItemDef("maxdoc", "MaxDoc Mk.1", ItemCategory.Consumable, ItemRarity.Common,
                0f, 0f, 0.40f, 0f, 0f, 60, 0.2f, "Trauma Team in a vial. Restores 40% health.", green);
            c["bounceback"] = new ItemDef("bounceback", "Bounce Back", ItemCategory.Consumable, ItemRarity.Uncommon,
                0f, 0f, 0.25f, 0f, 0f, 35, 0.2f, "Cheap stim. Restores 25% health. Tastes like battery.", green);

            c["bot_core"] = new ItemDef("bot_core", "Fried Bot Core", ItemCategory.Junk, ItemRarity.Uncommon,
                0f, 0f, 0f, 0f, 0f, 45, 1.5f, "Still warm. Somebody pays for these.", amber);
            c["razor_shard"] = new ItemDef("razor_shard", "Razor Shard", ItemCategory.Junk, ItemRarity.Common,
                0f, 0f, 0f, 0f, 0f, 25, 0.8f, "Splinter off a Corp blade.", bone);
            c["scrap_plate"] = new ItemDef("scrap_plate", "Scrap Plate", ItemCategory.Junk, ItemRarity.Common,
                0f, 0f, 0f, 0f, 0f, 12, 2.0f, "Gutter titanium. Heavy for what it pays.", bone);

            c["subdermal_weave"] = new ItemDef("subdermal_weave", "Subdermal Weave", ItemCategory.Cyberware, ItemRarity.Rare,
                0f, 0f, 0f, 50f, 0f, 300, 0f, "Ballistic mesh under the skin. +50 max health.", violet);
            c["synaptic_accel"] = new ItemDef("synaptic_accel", "Synaptic Accelerator", ItemCategory.Cyberware, ItemRarity.Uncommon,
                0f, 0f, 0f, 0f, 0.4f, 350, 0f, "Overclocked reflexes. +0.4 move speed.", amber);
            c["reflex_tuner"] = new ItemDef("reflex_tuner", "Reflex Tuner", ItemCategory.Cyberware, ItemRarity.Rare,
                0f, 8f, 0f, 0f, 0f, 280, 0f, "Heavy-attack servo tuning. +8 heavy damage (gun-kata rigs).", violet);

            c["arena_pass"] = new ItemDef("arena_pass", "ARENA PASS", ItemCategory.Quest, ItemRarity.Legendary,
                0f, 0f, 0f, 0f, 0f, 0, 0.1f, "Stolen Pit entry chip. Tomorrow night you walk in legal.", amber);
            return c;
        }

        [Header("Wallet & Carry")]
        public int eddies = 150;
        public float capacity = 100f;

        public List<ItemStack> items = new List<ItemStack>();
        public string equippedWeaponId = null;
        public List<string> equippedCyberware = new List<string>();

        public event Action OnChanged;

        public void AddEddies(int amount)
        {
            eddies += amount;
            OnChanged?.Invoke();
        }

        // Base stats captured lazily (after mission binds) so bonuses never drift.
        private bool _basesCaptured;
        private float _baseInsaneAtk, _baseInsaneHeavy, _baseInsaneRun, _baseInsaneMax;
        private float _baseSyntyRun, _baseSyntyMax;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SeedStartingKit();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void SeedStartingKit()
        {
            if (items.Count > 0) return;
            items.Add(new ItemStack("thermal_katana", 1));
            items.Add(new ItemStack("maxdoc", 2));
            items.Add(new ItemStack("scrap_plate", 3));
            equippedWeaponId = "thermal_katana";
            Debug.Log("[Inventory] Starting kit issued: Thermal Katana (equipped), 2x MaxDoc, 3x Scrap. €$150.");
        }

        public static bool EnsureInScene()
        {
            if (Instance != null) return true;
            GameObject go = new GameObject("Cyberpunk_Inventory");
            go.AddComponent<CyberpunkInventory>();
            return Instance != null;
        }

        public ItemDef GetDef(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            Catalog.TryGetValue(id, out var def);
            return def;
        }

        public int CountOf(string id)
        {
            var s = items.Find(x => x.defId == id);
            return s != null ? s.count : 0;
        }

        public float TotalWeight()
        {
            float w = 0f;
            foreach (var s in items)
            {
                var d = GetDef(s.defId);
                if (d != null) w += d.weight * s.count;
            }
            return w;
        }

        public bool AddItem(string id, int count = 1)
        {
            var def = GetDef(id);
            if (def == null || count <= 0) return false;
            if (def.category != ItemCategory.Quest && TotalWeight() + def.weight * count > capacity)
            {
                Debug.Log($"[Inventory] OVER CAPACITY: cannot take {def.displayName}.");
                return false;
            }
            var s = items.Find(x => x.defId == id);
            if (s != null) s.count += count;
            else items.Add(new ItemStack(id, count));
            Debug.Log($"[Inventory] +{count} {def.displayName}.");
            OnChanged?.Invoke();
            return true;
        }

        public bool RemoveItem(string id, int count = 1)
        {
            var s = items.Find(x => x.defId == id);
            if (s == null || s.count < count) return false;
            s.count -= count;
            if (s.count <= 0) items.Remove(s);
            if (equippedWeaponId == id && CountOf(id) == 0) equippedWeaponId = null;
            equippedCyberware.RemoveAll(x => CountOf(x) == 0);
            OnChanged?.Invoke();
            return true;
        }

        public bool IsQuestItem(string id)
        {
            var d = GetDef(id);
            return d != null && d.category == ItemCategory.Quest;
        }

        public bool EquipWeapon(string id)
        {
            var d = GetDef(id);
            if (d == null || d.category != ItemCategory.Weapon || CountOf(id) == 0) return false;
            equippedWeaponId = id;
            ApplyLoadout();
            Debug.Log($"[Inventory] Equipped {d.displayName} (+{d.damageBonus} dmg / +{d.heavyBonus} heavy).");
            OnChanged?.Invoke();
            return true;
        }

        public void UnequipWeapon()
        {
            equippedWeaponId = null;
            ApplyLoadout();
            OnChanged?.Invoke();
        }

        public bool ToggleCyberware(string id)
        {
            var d = GetDef(id);
            if (d == null || d.category != ItemCategory.Cyberware || CountOf(id) == 0) return false;
            if (equippedCyberware.Contains(id)) equippedCyberware.Remove(id);
            else if (equippedCyberware.Count < 3) equippedCyberware.Add(id);
            else return false;
            ApplyLoadout();
            Debug.Log($"[Inventory] Cyberware {(equippedCyberware.Contains(id) ? "implanted" : "removed")}: {d.displayName}.");
            OnChanged?.Invoke();
            return true;
        }

        public bool UseConsumable(string id)
        {
            var d = GetDef(id);
            if (d == null || d.category != ItemCategory.Consumable || CountOf(id) == 0) return false;
            ResolvePlayer(out var synty, out var insane);
            if (synty == null && insane == null) return false;
            bool healed = false;
            if (synty != null && synty.currentHealth < synty.maxHealth && synty.currentHealth > 0f)
            {
                synty.currentHealth = Mathf.Min(synty.maxHealth, synty.currentHealth + synty.maxHealth * d.healPct);
                healed = true;
            }
            if (insane != null && insane.currentHealth < insane.maxHealth && !insane.IsDead)
            {
                insane.currentHealth = Mathf.Min(insane.maxHealth, insane.currentHealth + insane.maxHealth * d.healPct);
                healed = true;
            }
            if (!healed) return false;
            RemoveItem(id, 1);
            Debug.Log($"[Inventory] Used {d.displayName} (+{d.healPct * 100f:F0}% HP).");
            return true;
        }

        public bool SellOne(string id)
        {
            var d = GetDef(id);
            if (d == null || IsQuestItem(id) || CountOf(id) == 0) return false;
            if (equippedWeaponId == id) return false;
            if (equippedCyberware.Contains(id)) return false;
            if (!RemoveItem(id, 1)) return false;
            eddies += d.price;
            Debug.Log($"[Inventory] Sold {d.displayName} +€${d.price} (now €${eddies}).");
            return true;
        }

        /// <summary>M1 bot kill drop: 40% core, 15% shard. Called from mission code.</summary>
        public void RollBotDrop()
        {
            float r = UnityEngine.Random.value;
            if (r < 0.40f) AddItem("bot_core", 1);
            else if (r < 0.55f) AddItem("razor_shard", 1);
        }

        public void ApplyLoadout()
        {
            ResolvePlayer(out var synty, out var insane);
            if (synty == null && insane == null) return;
            if (!_basesCaptured)
            {
                if (insane != null)
                {
                    _baseInsaneAtk = insane.attackDamage;
                    _baseInsaneHeavy = insane.heavyAttackDamage;
                    _baseInsaneRun = insane.runSpeed;
                    _baseInsaneMax = insane.maxHealth;
                }
                if (synty != null)
                {
                    _baseSyntyRun = synty.runSpeed;
                    _baseSyntyMax = synty.maxHealth;
                }
                _basesCaptured = true;
            }

            float wDmg = 0f, wHeavy = 0f;
            var w = GetDef(equippedWeaponId);
            if (w != null && w.category == ItemCategory.Weapon) { wDmg = w.damageBonus; wHeavy = w.heavyBonus; }

            float hpBonus = 0f, spdBonus = 0f, tunerHeavy = 0f;
            foreach (var cid in equippedCyberware)
            {
                var c = GetDef(cid);
                if (c == null || c.category != ItemCategory.Cyberware) continue;
                hpBonus += c.maxHpBonus;
                spdBonus += c.speedBonus;
                tunerHeavy += (cid == "reflex_tuner") ? c.heavyBonus : 0f;
            }

            if (insane != null)
            {
                insane.attackDamage = _baseInsaneAtk + wDmg;
                insane.heavyAttackDamage = _baseInsaneHeavy + wHeavy + tunerHeavy;
                insane.runSpeed = _baseInsaneRun + spdBonus;
                ApplyMaxHp(insane, _baseInsaneMax + hpBonus);
            }
            if (synty != null)
            {
                synty.runSpeed = _baseSyntyRun + spdBonus;
                ApplyMaxHp(synty, _baseSyntyMax + hpBonus);
            }
        }

        private void ApplyMaxHp(InsaneGun.Combat.InsaneGunSwordController c, float newMax)
        {
            float diff = newMax - c.maxHealth;
            c.maxHealth = newMax;
            if (diff != 0f) c.currentHealth = Mathf.Clamp(c.currentHealth + diff, 0f, newMax);
        }

        private void ApplyMaxHp(SyntyCombined.SyntySwordLocomotionController c, float newMax)
        {
            float diff = newMax - c.maxHealth;
            c.maxHealth = newMax;
            if (diff != 0f) c.currentHealth = Mathf.Clamp(c.currentHealth + diff, 0f, newMax);
        }

        private void ResolvePlayer(out SyntyCombined.SyntySwordLocomotionController synty,
            out InsaneGun.Combat.InsaneGunSwordController insane)
        {
            synty = UnityEngine.Object.FindAnyObjectByType<SyntyCombined.SyntySwordLocomotionController>();
            insane = null;
            GameObject hero = synty != null ? synty.gameObject : GameObject.FindWithTag("Player");
            if (hero == null)
            {
                var ctrls = UnityEngine.Object.FindObjectsByType<InsaneGun.Combat.InsaneGunSwordController>(FindObjectsSortMode.None);
                foreach (var c in ctrls)
                {
                    if (c != null && c.controlMode == InsaneGun.Combat.InsaneGunSwordController.ControlMode.PlayerControlled)
                    {
                        hero = c.gameObject;
                        break;
                    }
                }
            }
            if (hero != null)
            {
                if (synty == null) synty = hero.GetComponent<SyntyCombined.SyntySwordLocomotionController>();
                insane = hero.GetComponent<InsaneGun.Combat.InsaneGunSwordController>();
            }
        }
    }
}
