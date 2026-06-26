using UnityEngine;
using UnityEngine.UI;

namespace SlimeRPG
{
    /// <summary>
    /// One hexagon in the skill tree. Buying it spends gold, applies its effect, and reveals its
    /// hidden neighbours (first purchase). Each repeat purchase (level) costs 10x the previous.
    /// "Hero Slot" unlocks a team slot. Hidden nodes are invisible until a neighbour is bought.
    /// </summary>
    public class SkillNode : MonoBehaviour
    {
        public enum State { Hidden, Available, Purchased }
        public enum Effect { None, HeroSlot, Luck, Damage, Gold, Crit, Speed }

        public State state = State.Hidden;
        public Effect effect = Effect.None;
        public SkillNode[] neighbors;

        public Button button;
        public Image background;
        public Text nameLabel;
        public Text costLabel;

        public int baseCost = 60;
        public int level = 0;

        public SlimeRoller roller;
        public CombatManager combat;
        public TeamManager team;

        static readonly Color AvailableCol = new Color(0.20f, 0.34f, 0.26f, 1f); // darker (not bought)
        static readonly Color PurchasedCol = new Color(0.34f, 0.66f, 0.40f, 1f); // brighter (bought)

        bool _wired;

        public int CurrentCost => Mathf.RoundToInt(baseCost * Mathf.Pow(10f, level));

        void Start() { Wire(); RefreshVisual(); }

        void Wire()
        {
            if (_wired) return;
            if (button != null) { button.onClick.AddListener(OnClick); _wired = true; }
        }

        public void OnClick()
        {
            if (state == State.Hidden) return;
            int cost = CurrentCost;
            if (roller != null && cost > 0)
            {
                if (roller.gold < cost) return; // can't afford
                roller.gold -= cost;
                roller.UpdateGoldUI();
                roller.OnInventoryChanged?.Invoke();
            }
            level++;
            ApplyEffect();
            if (state == State.Available)
            {
                state = State.Purchased;
                if (neighbors != null) foreach (var n in neighbors) if (n != null) n.Reveal();
            }
            RefreshVisual();
        }

        void ApplyEffect()
        {
            switch (effect)
            {
                case Effect.HeroSlot: if (team != null) team.AddSlot(); break;
                case Effect.Luck: if (roller != null) { roller.luckMultiplier += 0.5f; roller.UpdateLuckUI(); } break;
                case Effect.Damage: if (combat != null) combat.damageMult += 0.5f; break;
                case Effect.Gold: if (combat != null) combat.goldMult += 0.5f; break;
                case Effect.Crit: if (combat != null) combat.critChance = Mathf.Min(0.9f, combat.critChance + 0.04f); break;
                case Effect.Speed: if (combat != null) combat.tickInterval = Mathf.Max(0.15f, combat.tickInterval * 0.9f); break;
            }
        }

        public void Reveal()
        {
            if (state != State.Hidden) return;
            state = State.Available;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            Wire();
            RefreshVisual();
        }

        public void SetState(State s)
        {
            state = s;
            gameObject.SetActive(s != State.Hidden);
            RefreshVisual();
        }

        public void RefreshVisual()
        {
            if (state == State.Hidden) return;
            if (background != null) background.color = state == State.Purchased ? PurchasedCol : AvailableCol;
            if (costLabel != null)
            {
                if (effect == Effect.None) costLabel.text = state == State.Purchased ? "" : (CurrentCost > 0 ? CurrentCost + "g" : "Open");
                else costLabel.text = (level > 0 ? "Lv" + level + "  " : "") + CurrentCost + "g";
            }
        }
    }
}
