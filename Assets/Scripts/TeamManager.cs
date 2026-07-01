using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SlimeRPG
{
    /// <summary>
    /// Owns the equipped team roster (now 7 slots; only slot 1 unlocked at start, the rest unlock via
    /// the per-row Unlock button or the Hero Slot skill). The first roll equips a slime; later rolls
    /// auto-equip into an empty slot or replace the weakest if the new one is rarer. Drives the
    /// scrollable roster UI (icon + lock + per-slot button) and spawns the hero units on the field.
    /// </summary>
    public class TeamManager : MonoBehaviour
    {
        public const int SlotCount = 7;

        public SlimeRoller roller;
        public CombatManager combat;
        public Transform heroContainer;
        public Image[] slotMini;          // SlotCount
        public GameObject[] slotLock;     // SlotCount
        public Button[] slotButtons;      // SlotCount — per-row Unlock/Upgrade
        public Text[] slotButtonLabels;   // SlotCount
        public GameObject[] slotCoinIcons; // SlotCount — gold coin shown in the Upgrade state
        public Text[] slotCostLabels;      // SlotCount — upgrade/unlock gold cost
        public Text[] slotLevelLabels;     // SlotCount — "Lv N" per-slot upgrade level
        public Button[] slotEquipButtons;  // SlotCount — "Equip" shown on an unlocked-but-empty slot -> opens inventory
        public Button unlockSlotButton;   // optional legacy button (Skill Tree); may be null
        public Text unlockSlotLabel;

        [Header("Slot unlock routes to the Skill Tree")]
        public GameObject skillsPanel;
        public GameObject inventoryPanel;
        public SkillNode[] heroSlotNodes; // the Hero Slot chain (1->2->3->4); highlight the next buyable one
        public SkillNode skillStartNode;  // tree centre — highlighted if no Hero Slot hex is revealed yet

        public int unlockedSlots = 1;
        public int[] equipped = { -1, -1, -1, -1, -1, -1, -1 };

        readonly Unit[] _slotHeroes = new Unit[SlotCount];
        readonly int[] _slotRarity = { -1, -1, -1, -1, -1, -1, -1 };

        public System.Action OnTeamChanged;

        Color SlimeColor(int idx) => (roller != null && idx >= 0 && idx < roller.rarities.Count) ? roller.rarities[idx].color : Color.gray;
        int SlimeTier(int idx) => (roller != null && idx >= 0 && idx < roller.rarities.Count) ? roller.rarities[idx].tier : 0;

        // Gold cost to UNLOCK the slot at this index (index 0 = slot 1, free). Steep ~5x ramp so getting all
        // 7 is a real grind (ascension should unlock ~around affording slot 6). Resets on ascension later.
        static readonly int[] SlotCosts = { 0, 100, 500, 2500, 12000, 60000, 300000 };

        // Per-slot UPGRADE (the always-something-to-click gold sink): each level boosts the slot's hero every
        // stat a little; cheap and slow (2g at Lv0 -> ~10g by Lv10 via 1.175^level, climbing after). Slot-attached.
        public const float UpgradePerLevel = 0.06f;                         // +6% DPS & HP per level
        readonly int[] slotUpgradeLevel = new int[SlotCount];
        public int SlotUpgradeCost(int level) => Mathf.RoundToInt(2f * Mathf.Pow(1.175f, level));
        float SlotMult(int slot) => 1f + UpgradePerLevel * slotUpgradeLevel[slot];

        void Start()
        {
            // Auto-equip ONLY the first slime (when the team is empty); after that the player equips manually.
            if (roller != null) roller.OnRolled += OnFirstRollEquip;
            if (unlockSlotButton != null) unlockSlotButton.onClick.AddListener(() => UnlockSlot());
            if (slotButtons != null)
                for (int i = 0; i < slotButtons.Length; i++)
                {
                    int idx = i;
                    if (slotButtons[i] != null) slotButtons[i].onClick.AddListener(() => OnSlotButton(idx));
                }
            if (slotEquipButtons != null)
                foreach (var b in slotEquipButtons)
                    if (b != null) b.onClick.AddListener(OpenInventory);
            RefreshSlots();
            RebuildHeroes();
        }

        void OnDestroy() { if (roller != null) roller.OnRolled -= OnFirstRollEquip; }

        bool _autoEquippedFirst;
        void OnFirstRollEquip(int idx)
        {
            if (_autoEquippedFirst) return;                 // only ever auto-equips the very first slime
            _autoEquippedFirst = true;
            for (int i = 0; i < unlockedSlots; i++) if (equipped[i] >= 0) return; // already has a slime
            int empty = -1;
            for (int i = 0; i < unlockedSlots; i++) if (equipped[i] < 0) { empty = i; break; }
            if (empty < 0) return;
            equipped[empty] = idx;
            RefreshSlots();
            RebuildHeroes();
        }

        /// <summary>Equip one copy of a rarity into the first empty unlocked slot (respects owned count). Returns success.</summary>
        public bool Equip(int rarity)
        {
            if (roller == null || roller.owned == null || rarity < 0 || rarity >= roller.owned.Length) return false;
            int already = 0;
            for (int i = 0; i < unlockedSlots; i++) if (equipped[i] == rarity) already++;
            if (already >= roller.owned[rarity]) return false; // no spare copies to equip
            int target = -1;
            for (int i = 0; i < unlockedSlots; i++) if (equipped[i] < 0) { target = i; break; } // prefer an empty slot
            if (target < 0)
            {
                // team full -> replace the weakest equipped slot (lets you swap with a full/1-slot team)
                int weak = int.MaxValue;
                for (int i = 0; i < unlockedSlots; i++) if (equipped[i] < weak) { weak = equipped[i]; target = i; }
            }
            if (target < 0) return false;
            equipped[target] = rarity;
            RefreshSlots();
            RebuildHeroes();
            return true;
        }

        /// <summary>Clear an equipped slot.</summary>
        public void Unequip(int slot)
        {
            if (slot < 0 || slot >= unlockedSlots || equipped[slot] < 0) return;
            equipped[slot] = -1;
            RefreshSlots();
            RebuildHeroes();
        }

        public void AutoEquipBest()
        {
            var pool = new List<int>();
            if (roller != null && roller.owned != null)
                for (int r = 0; r < roller.owned.Length; r++)
                    for (int c = 0; c < roller.owned[r]; c++) pool.Add(r);
            pool.Sort(); pool.Reverse();

            for (int i = 0; i < equipped.Length; i++) equipped[i] = -1;
            for (int i = 0; i < unlockedSlots && i < pool.Count; i++) equipped[i] = pool[i];
            RefreshSlots();
            RebuildHeroes();
        }

        public int NextSlotCost() => unlockedSlots >= SlotCount ? -1 : SlotCosts[unlockedSlots];

        /// <summary>Per-row button: an unlocked slot buys a per-slot upgrade; the next locked slot buys the unlock (gold).</summary>
        void OnSlotButton(int i)
        {
            if (i < unlockedSlots) TryUpgradeSlot(i);
            else if (i == unlockedSlots) UnlockSlot();
        }

        /// <summary>Buy one per-slot upgrade level with gold and retune the equipped hero in place (no heal).</summary>
        void TryUpgradeSlot(int i)
        {
            if (roller == null || i < 0 || i >= unlockedSlots) return;
            int cost = SlotUpgradeCost(slotUpgradeLevel[i]);
            if (roller.gold < cost) return;
            roller.gold -= cost;
            roller.UpdateGoldUI();
            slotUpgradeLevel[i]++;
            if (_slotHeroes[i] != null && equipped[i] >= 0)
            {
                int tier = SlimeTier(equipped[i]);
                _slotHeroes[i].SetStats(SlimeCatalog.TierHp[tier] * SlotMult(i), SlimeCatalog.TierDps[tier] * SlotMult(i));
            }
            RefreshSlots();
            roller.OnInventoryChanged?.Invoke();
        }

        /// <summary>Slots are only unlocked via the Skill Tree. Open it and pulse the NEXT buyable Hero Slot hex.</summary>
        void OpenSlotUnlock()
        {
            if (inventoryPanel != null) inventoryPanel.SetActive(false);
            if (skillsPanel != null) skillsPanel.SetActive(true);
            SkillNode target = skillStartNode;
            if (heroSlotNodes != null)
                foreach (var n in heroSlotNodes)
                    if (n != null && n.gameObject.activeInHierarchy && n.state == SkillNode.State.Available) { target = n; break; }
            if (target != null) target.Highlight();
        }

        /// <summary>Open the inventory (from an empty slot's Equip button).</summary>
        void OpenInventory()
        {
            if (skillsPanel != null) skillsPanel.SetActive(false);
            if (inventoryPanel != null) inventoryPanel.SetActive(true);
        }

        /// <summary>Unlocks a team slot for free (used by the Hero Slot skill, which already paid).</summary>
        public void AddSlot()
        {
            if (unlockedSlots >= SlotCount) return;
            unlockedSlots++;
            RefreshSlots();
            RebuildHeroes();
        }

        public bool UnlockSlot()
        {
            int cost = NextSlotCost();
            if (cost < 0 || roller == null || roller.gold < cost) return false;
            roller.gold -= cost;
            roller.UpdateGoldUI();
            unlockedSlots++;
            RefreshSlots();
            RebuildHeroes();
            roller.OnInventoryChanged?.Invoke();
            return true;
        }

        void RefreshSlots()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                bool unlocked = i < unlockedSlots;
                bool isNext = i == unlockedSlots;
                if (slotLock != null && i < slotLock.Length && slotLock[i] != null) slotLock[i].SetActive(!unlocked);
                if (slotMini != null && i < slotMini.Length && slotMini[i] != null)
                {
                    bool has = unlocked && equipped[i] >= 0;
                    slotMini[i].gameObject.SetActive(has);
                    if (has) slotMini[i].color = SlimeColor(equipped[i]);
                }
                // "Equip" button on an unlocked-but-empty slot (where the slime icon would be)
                if (slotEquipButtons != null && i < slotEquipButtons.Length && slotEquipButtons[i] != null)
                    slotEquipButtons[i].gameObject.SetActive(unlocked && equipped[i] < 0);
                if (slotButtonLabels != null && i < slotButtonLabels.Length && slotButtonLabels[i] != null)
                    slotButtonLabels[i].text = unlocked ? "Upgrade" : (isNext ? "Unlock" : "Locked");
                // both the Upgrade (unlocked) and Unlock (next) states show a gold coin + cost
                bool showCost = unlocked || isNext;
                if (slotCoinIcons != null && i < slotCoinIcons.Length && slotCoinIcons[i] != null) slotCoinIcons[i].SetActive(showCost);
                if (slotCostLabels != null && i < slotCostLabels.Length && slotCostLabels[i] != null)
                {
                    slotCostLabels[i].gameObject.SetActive(showCost);
                    if (unlocked) slotCostLabels[i].text = NumberFormat.Short(SlotUpgradeCost(slotUpgradeLevel[i]));
                    else if (isNext) slotCostLabels[i].text = NumberFormat.Short(SlotCosts[i]);
                }
                if (slotLevelLabels != null && i < slotLevelLabels.Length && slotLevelLabels[i] != null)
                {
                    slotLevelLabels[i].gameObject.SetActive(unlocked);
                    if (unlocked) slotLevelLabels[i].text = "Lv " + slotUpgradeLevel[i];
                }
                if (slotButtons != null && i < slotButtons.Length && slotButtons[i] != null)
                    slotButtons[i].interactable = unlocked || isNext;
            }
            if (unlockSlotLabel != null)
            {
                int c = NextSlotCost();
                unlockSlotLabel.text = c < 0 ? "Team Full (" + SlotCount + "/" + SlotCount + ")" : "Unlock Slot  (" + NumberFormat.Short(c) + "g)";
            }
            OnTeamChanged?.Invoke();
        }

        /// <summary>
        /// Rebuilds only the slots whose equipped slime changed, so unchanged heroes KEEP their HP
        /// (rolling/equipping must not heal the team). New/changed heroes start at full HP.
        /// </summary>
        void RebuildHeroes()
        {
            if (heroContainer == null || combat == null) return;

            for (int i = 0; i < SlotCount; i++)
            {
                int desired = (i < unlockedSlots) ? equipped[i] : -1;
                if (desired < 0)
                {
                    if (_slotHeroes[i] != null) { Destroy(_slotHeroes[i].gameObject); _slotHeroes[i] = null; }
                    _slotRarity[i] = -1;
                }
                else if (_slotHeroes[i] == null || _slotRarity[i] != desired)
                {
                    if (_slotHeroes[i] != null) Destroy(_slotHeroes[i].gameObject);
                    _slotHeroes[i] = combat.CreateUnit(heroContainer, Vector2.zero, 122f, SlimeColor(desired), true, SlimeCatalog.TierHp[SlimeTier(desired)] * SlotMult(i), SlimeCatalog.TierDps[SlimeTier(desired)] * SlotMult(i));
                    _slotRarity[i] = desired;
                }
                // else: same rarity in this slot -> keep the existing hero (and its current HP)
            }

            var heroes = new List<Unit>();
            for (int i = 0; i < SlotCount; i++) if (_slotHeroes[i] != null) heroes.Add(_slotHeroes[i]);
            var pts = LeftFormation(heroes.Count);
            for (int i = 0; i < heroes.Count; i++)
                heroes[i].GetComponent<RectTransform>().anchoredPosition = pts[i];

            combat.SetHeroes(heroes);
        }

        static Vector2[] LeftFormation(int count)
        {
            if (count <= 5)
            {
                var p = CombatManager.EnemyFormation(count);
                for (int i = 0; i < p.Length; i++) p[i].x = -p[i].x;
                return p;
            }
            // 6-7 heroes: staggered left columns
            var pts = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                int col = i % 3, row = i / 3;
                pts[i] = new Vector2(-(250f + col * 95f), -30f - row * 120f);
            }
            return pts;
        }
    }
}
