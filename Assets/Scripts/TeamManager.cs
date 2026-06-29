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
        public Text[] slotCostLabels;      // SlotCount — upgrade cost (placeholder)
        public Button unlockSlotButton;   // optional legacy button (Skill Tree); may be null
        public Text unlockSlotLabel;

        [Header("Slot unlock routes to the Skill Tree")]
        public GameObject skillsPanel;
        public GameObject inventoryPanel;
        public SkillNode heroSlotNode;    // the Hero Slot skill (real slot unlock)
        public SkillNode skillStartNode;  // tree centre — highlighted if Hero Slot isn't revealed yet

        public int unlockedSlots = 1;
        public int[] equipped = { -1, -1, -1, -1, -1, -1, -1 };

        readonly Unit[] _slotHeroes = new Unit[SlotCount];
        readonly int[] _slotRarity = { -1, -1, -1, -1, -1, -1, -1 };

        public System.Action OnTeamChanged;

        static readonly Color[] RarityCols = {
            new Color(0.62f, 0.65f, 0.70f), new Color(0.35f, 0.82f, 0.40f), new Color(0.28f, 0.55f, 1f),
            new Color(0.70f, 0.35f, 1f), new Color(1f, 0.80f, 0.16f)
        };
        static readonly float[] HeroDps = { 6f, 12f, 24f, 50f, 110f };
        static readonly float[] HeroHp  = { 80f, 140f, 260f, 520f, 1050f };
        // cost to unlock the slot at this index (index 0 = slot 1, free)
        static readonly int[] SlotCosts = { 0, 100, 300, 800, 2000, 5000, 12000 };

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

        /// <summary>Per-row button: unlocked slot = (placeholder) upgrade; the next locked slot = go to the Skill Tree.</summary>
        void OnSlotButton(int i)
        {
            if (i < unlockedSlots) { /* slot upgrade — placeholder, no effect yet */ }
            else if (i == unlockedSlots) OpenSlotUnlock();
        }

        /// <summary>Slots are only unlocked via the Skill Tree. Open it and pulse the Hero Slot node.</summary>
        void OpenSlotUnlock()
        {
            if (inventoryPanel != null) inventoryPanel.SetActive(false);
            if (skillsPanel != null) skillsPanel.SetActive(true);
            var target = (heroSlotNode != null && heroSlotNode.gameObject.activeInHierarchy) ? heroSlotNode : skillStartNode;
            if (target != null) target.Highlight();
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
                    if (has) slotMini[i].color = RarityCols[equipped[i]];
                }
                if (slotButtonLabels != null && i < slotButtonLabels.Length && slotButtonLabels[i] != null)
                    slotButtonLabels[i].text = unlocked ? "Upgrade" : (isNext ? "Unlock" : "Locked");
                // Upgrade state shows a gold coin + cost (placeholder); Unlock/Locked hide them
                if (slotCoinIcons != null && i < slotCoinIcons.Length && slotCoinIcons[i] != null) slotCoinIcons[i].SetActive(unlocked);
                if (slotCostLabels != null && i < slotCostLabels.Length && slotCostLabels[i] != null)
                {
                    slotCostLabels[i].gameObject.SetActive(unlocked);
                    if (unlocked) slotCostLabels[i].text = "100";
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
                    _slotHeroes[i] = combat.CreateUnit(heroContainer, Vector2.zero, 122f, RarityCols[desired], true, HeroHp[desired], HeroDps[desired]);
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
