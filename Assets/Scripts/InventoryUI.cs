using UnityEngine;
using UnityEngine.UI;

namespace SlimeRPG
{
    /// <summary>
    /// Drives the inventory panel: one row per rarity showing the owned count (xN). Selecting a
    /// row highlights it; the Sell button sells that rarity's duplicates (keeping 1) for gold.
    /// Row buttons / texts are assigned by the scene builder; this self-wires at runtime.
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        public SlimeRoller roller;
        public TeamManager team;
        public Button[] rowButtons;
        public Image[] rowBackgrounds;
        public Text[] countTexts;
        public Button sellButton;
        public Text sellLabel;
        public Button autoEquipButton;
        public Button unlockSlotButton;
        public Text unlockSlotLabel;
        public int selected = -1;

        static readonly Color RowNormal   = new Color(0.13f, 0.15f, 0.18f, 1f);
        static readonly Color RowSelected = new Color(0.22f, 0.30f, 0.40f, 1f);

        void Start()
        {
            if (rowButtons != null)
                for (int i = 0; i < rowButtons.Length; i++)
                {
                    int idx = i;
                    if (rowButtons[i] != null) rowButtons[i].onClick.AddListener(() => Select(idx));
                }
            if (sellButton != null) sellButton.onClick.AddListener(SellSelected);
            if (autoEquipButton != null) autoEquipButton.onClick.AddListener(() => { if (team != null) team.AutoEquipBest(); });
            if (unlockSlotButton != null) unlockSlotButton.onClick.AddListener(() => { if (team != null) { team.UnlockSlot(); Refresh(); } });
            if (roller != null) roller.OnInventoryChanged += Refresh;
            Refresh();
        }

        void OnEnable() { Refresh(); }
        void OnDestroy() { if (roller != null) roller.OnInventoryChanged -= Refresh; }

        public void Select(int i) { selected = i; Refresh(); }

        public void SellSelected()
        {
            if (roller == null || selected < 0) return;
            roller.SellDupes(selected);
            Refresh();
        }

        public void Refresh()
        {
            if (roller == null) return;
            roller.EnsureOwned();

            if (countTexts != null)
                for (int i = 0; i < countTexts.Length; i++)
                {
                    int c = (roller.owned != null && i < roller.owned.Length) ? roller.owned[i] : 0;
                    if (countTexts[i] != null) countTexts[i].text = "x" + c;
                    if (rowBackgrounds != null && i < rowBackgrounds.Length && rowBackgrounds[i] != null)
                        rowBackgrounds[i].color = (i == selected) ? RowSelected : RowNormal;
                }

            if (sellLabel != null)
            {
                if (selected < 0) sellLabel.text = "Select a slime";
                else
                {
                    int dupes = (roller.owned != null && selected < roller.owned.Length) ? Mathf.Max(0, roller.owned[selected] - 1) : 0;
                    int unit = (roller.sellValues != null && selected < roller.sellValues.Length) ? roller.sellValues[selected] : 1;
                    sellLabel.text = dupes > 0 ? $"Sell {dupes} dupes  (+{dupes * unit}g)" : "No dupes to sell";
                }
            }

            if (unlockSlotLabel != null && team != null)
            {
                int cost = team.NextSlotCost();
                unlockSlotLabel.text = cost < 0 ? "Team Full (5/5)" : $"Unlock Slot ({cost}g)";
            }
        }
    }
}
