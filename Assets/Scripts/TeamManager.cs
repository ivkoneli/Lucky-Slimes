using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SlimeRPG
{
    /// <summary>
    /// Owns the equipped team. You start with 0 slimes and 2 unlocked slots (the other 3 are locked
    /// behind inventory upgrades). The first roll equips a slime; later rolls auto-equip into an
    /// empty slot, or replace the weakest equipped slime if the new one is rarer. Auto-Equip Best
    /// fills the team with your highest-rarity owned slimes. Spawns the hero units on the battlefield.
    /// </summary>
    public class TeamManager : MonoBehaviour
    {
        public SlimeRoller roller;
        public CombatManager combat;
        public Transform heroContainer;
        public Image[] slotMini;       // 5
        public GameObject[] slotLock;  // 5
        public Button unlockSlotButton;   // lives in the Skill Tree panel
        public Text unlockSlotLabel;

        public int unlockedSlots = 2;
        public int[] equipped = { -1, -1, -1, -1, -1 };

        readonly Unit[] _slotHeroes = new Unit[5];
        readonly int[] _slotRarity = { -1, -1, -1, -1, -1 };

        public System.Action OnTeamChanged;

        static readonly Color[] RarityCols = {
            new Color(0.62f, 0.65f, 0.70f), new Color(0.35f, 0.82f, 0.40f), new Color(0.28f, 0.55f, 1f),
            new Color(0.70f, 0.35f, 1f), new Color(1f, 0.80f, 0.16f)
        };
        static readonly float[] HeroDps = { 6f, 12f, 24f, 50f, 110f };
        static readonly float[] HeroHp  = { 80f, 140f, 260f, 520f, 1050f };
        static readonly int[] SlotCosts = { 0, 0, 100, 300, 800 }; // cost to unlock slot at this index

        void Start()
        {
            if (roller != null) roller.OnRolled += OnRolled;
            if (unlockSlotButton != null) unlockSlotButton.onClick.AddListener(() => UnlockSlot());
            RefreshSlots();
            RebuildHeroes();
        }

        void OnDestroy() { if (roller != null) roller.OnRolled -= OnRolled; }

        void OnRolled(int idx)
        {
            int empty = -1;
            for (int i = 0; i < unlockedSlots; i++) if (equipped[i] < 0) { empty = i; break; }
            if (empty >= 0)
            {
                equipped[empty] = idx;
            }
            else
            {
                int weakSlot = -1, weakVal = int.MaxValue;
                for (int i = 0; i < unlockedSlots; i++) if (equipped[i] < weakVal) { weakVal = equipped[i]; weakSlot = i; }
                if (weakSlot >= 0 && idx > weakVal) equipped[weakSlot] = idx;
            }
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

        public int NextSlotCost() => unlockedSlots >= 5 ? -1 : SlotCosts[unlockedSlots];

        /// <summary>Unlocks a team slot for free (used by the Hero Slot skill, which already paid).</summary>
        public void AddSlot()
        {
            if (unlockedSlots >= 5) return;
            unlockedSlots++;
            RefreshSlots();
        }

        public bool UnlockSlot()
        {
            int cost = NextSlotCost();
            if (cost < 0 || roller == null || roller.gold < cost) return false;
            roller.gold -= cost;
            roller.UpdateGoldUI();
            unlockedSlots++;
            RefreshSlots();
            roller.OnInventoryChanged?.Invoke();
            return true;
        }

        void RefreshSlots()
        {
            for (int i = 0; i < 5; i++)
            {
                bool locked = i >= unlockedSlots;
                if (slotLock != null && i < slotLock.Length && slotLock[i] != null) slotLock[i].SetActive(locked);
                if (slotMini != null && i < slotMini.Length && slotMini[i] != null)
                {
                    bool has = !locked && equipped[i] >= 0;
                    slotMini[i].gameObject.SetActive(has);
                    if (has) slotMini[i].color = RarityCols[equipped[i]];
                }
            }
            if (unlockSlotLabel != null)
            {
                int c = NextSlotCost();
                unlockSlotLabel.text = c < 0 ? "Team Full (5/5)" : "Unlock Team Slot  (" + c + "g)";
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

            for (int i = 0; i < 5; i++)
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
            for (int i = 0; i < 5; i++) if (_slotHeroes[i] != null) heroes.Add(_slotHeroes[i]);
            var pts = LeftFormation(heroes.Count);
            for (int i = 0; i < heroes.Count; i++)
                heroes[i].GetComponent<RectTransform>().anchoredPosition = pts[i];

            combat.SetHeroes(heroes);
        }

        static Vector2[] LeftFormation(int count)
        {
            var pts = CombatManager.EnemyFormation(count);
            for (int i = 0; i < pts.Length; i++) pts[i].x = -pts[i].x;
            return pts;
        }
    }
}
