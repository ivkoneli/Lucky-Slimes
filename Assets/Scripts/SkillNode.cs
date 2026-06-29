using System.Collections;
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
        public enum Effect { None, HeroSlot, Luck, Damage, Gold, Crit, Speed, AutoRoll, Spin }

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
        public DiceSpinner spinner;

        static readonly Color AvailableCol = new Color(0.20f, 0.34f, 0.26f, 1f); // darker (not bought)
        static readonly Color PurchasedCol = new Color(0.34f, 0.66f, 0.40f, 1f); // brighter (bought)

        bool _wired;
        Coroutine _pulse;

        public int CurrentCost => Mathf.RoundToInt(baseCost * Mathf.Pow(10f, level));

        /// <summary>Flash this node white a few times to draw attention (e.g. when sent here to unlock a slot). Stops on click.</summary>
        public void Highlight()
        {
            if (!isActiveAndEnabled || background == null) return;
            if (_pulse != null) StopCoroutine(_pulse);
            _pulse = StartCoroutine(PulseRoutine());
        }

        IEnumerator PulseRoutine()
        {
            Color baseCol = state == State.Purchased ? PurchasedCol : AvailableCol;
            for (int n = 0; n < 6; n++)
            {
                float t = 0f;
                while (t < 0.6f) { t += Time.deltaTime; background.color = Color.Lerp(Color.white, baseCol, t / 0.6f); yield return null; }
                background.color = baseCol;
                yield return new WaitForSeconds(0.15f);
            }
            background.color = baseCol;
            _pulse = null;
        }

        void Start() { Wire(); RefreshVisual(); }

        void Wire()
        {
            if (_wired) return;
            if (button != null) { button.onClick.AddListener(OnClick); _wired = true; }
        }

        public void OnClick()
        {
            if (state == State.Hidden) return;
            if (effect == Effect.AutoRoll && state == State.Purchased) return; // one-time unlock
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
            if (_pulse != null) { StopCoroutine(_pulse); _pulse = null; } // clicked — stop the attention pulse
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
                case Effect.AutoRoll: if (roller != null) roller.autoRoll = true; break;
                case Effect.Spin: if (spinner != null) spinner.SpeedUp(0.8f); break; // dice spins 20% faster
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
                if (effect == Effect.AutoRoll) costLabel.text = state == State.Purchased ? "ON" : (CurrentCost > 0 ? NumberFormat.Short(CurrentCost) + "g" : "Free");
                else if (effect == Effect.None) costLabel.text = state == State.Purchased ? "" : (CurrentCost > 0 ? NumberFormat.Short(CurrentCost) + "g" : "Open");
                else costLabel.text = (level > 0 ? "Lv" + level + "  " : "") + NumberFormat.Short(CurrentCost) + "g";
            }
        }
    }
}
