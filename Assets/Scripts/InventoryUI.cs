using UnityEngine;
using UnityEngine.UI;

namespace SlimeRPG
{
    /// <summary>
    /// Inventory overlay with two tabs (Slimes / Items). Slimes tab: a horizontal strip of the
    /// equipped team slots up top (locked ones blacked out; click to unequip) + a grid of owned
    /// slime types below. Each owned-slime cell shows icon + name + its own Sell and Equip buttons,
    /// and highlights when selected (hover tint via the Button). Auto-Equip lives bottom-right.
    /// Items tab is an empty placeholder for now.
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        public SlimeRoller roller;
        public TeamManager team;

        [Header("Tabs")]
        public Button slimesTabBtn, itemsTabBtn;
        public Image slimesTabBg, itemsTabBg;
        public GameObject slimesPanel, itemsPanel;

        [Header("Equipped preview (top strip)")]
        public Button[] equipSlotBtns;
        public Image[] equipSlotIcons;
        public GameObject[] equipSlotLocks;

        [Header("Inventory grid (one per slime type)")]
        public Button[] invBtns;        // cell body — selects
        public Image[] invFrames;       // cell outer frame — selection highlight
        public Image[] invIcons;
        public Text[] invCounts;
        public Button[] invSellBtns;
        public Button[] invEquipBtns;

        [Header("Bottom")]
        public Button autoEquipButton;

        public int selected = -1;

        static readonly Color TabOn   = new Color(0.28f, 0.42f, 0.58f, 1f);
        static readonly Color TabOff  = new Color(0.16f, 0.18f, 0.22f, 1f);
        static readonly Color FrameOff = new Color(0.10f, 0.11f, 0.14f, 1f); // unselected (subtle)
        static readonly Color FrameSel = new Color(0.45f, 0.74f, 1f, 1f);    // selected highlight
        static readonly Color[] RarityCols = {
            new Color(0.62f, 0.65f, 0.70f), new Color(0.35f, 0.82f, 0.40f), new Color(0.28f, 0.55f, 1f),
            new Color(0.70f, 0.35f, 1f), new Color(1f, 0.80f, 0.16f)
        };

        bool _wired;

        void Start() { Wire(); ShowTab(true); Refresh(); }
        void OnEnable() { Refresh(); }

        void Wire()
        {
            if (_wired) return; _wired = true;
            if (slimesTabBtn != null) slimesTabBtn.onClick.AddListener(() => ShowTab(true));
            if (itemsTabBtn != null) itemsTabBtn.onClick.AddListener(() => ShowTab(false));
            if (autoEquipButton != null) autoEquipButton.onClick.AddListener(() => { if (team != null) team.AutoEquipBest(); Refresh(); });
            if (equipSlotBtns != null)
                for (int i = 0; i < equipSlotBtns.Length; i++)
                {
                    int s = i;
                    if (equipSlotBtns[i] != null) equipSlotBtns[i].onClick.AddListener(() => { if (team != null) team.Unequip(s); Refresh(); });
                }
            if (invBtns != null)
                for (int i = 0; i < invBtns.Length; i++)
                {
                    int r = i;
                    if (invBtns[i] != null) invBtns[i].onClick.AddListener(() => Select(r));
                    if (invSellBtns != null && invSellBtns[i] != null) invSellBtns[i].onClick.AddListener(() => { if (roller != null) roller.SellDupes(r); Select(r); });
                    if (invEquipBtns != null && invEquipBtns[i] != null) invEquipBtns[i].onClick.AddListener(() => { if (team != null) team.Equip(r); Select(r); });
                }
            if (roller != null) roller.OnInventoryChanged += Refresh;
            if (team != null) team.OnTeamChanged += Refresh;
        }

        void OnDestroy()
        {
            if (roller != null) roller.OnInventoryChanged -= Refresh;
            if (team != null) team.OnTeamChanged -= Refresh;
        }

        public void ShowTab(bool slimes)
        {
            if (slimesPanel != null) slimesPanel.SetActive(slimes);
            if (itemsPanel != null) itemsPanel.SetActive(!slimes);
            if (slimesTabBg != null) slimesTabBg.color = slimes ? TabOn : TabOff;
            if (itemsTabBg != null) itemsTabBg.color = slimes ? TabOff : TabOn;
        }

        public void Select(int i) { selected = i; Refresh(); }

        public void Refresh()
        {
            if (roller == null) return;
            roller.EnsureOwned();

            // equipped preview strip
            if (equipSlotIcons != null)
                for (int i = 0; i < equipSlotIcons.Length; i++)
                {
                    bool unlocked = team != null && i < team.unlockedSlots;
                    if (equipSlotLocks != null && i < equipSlotLocks.Length && equipSlotLocks[i] != null) equipSlotLocks[i].SetActive(!unlocked);
                    int eq = (team != null && unlocked && i < team.equipped.Length) ? team.equipped[i] : -1;
                    if (equipSlotIcons[i] != null)
                    {
                        bool has = unlocked && eq >= 0;
                        equipSlotIcons[i].gameObject.SetActive(has);
                        if (has) equipSlotIcons[i].color = RarityCols[eq];
                    }
                }

            // owned-slime grid (show only types you own)
            if (invBtns != null)
                for (int r = 0; r < invBtns.Length; r++)
                {
                    int count = (r < roller.owned.Length) ? roller.owned[r] : 0;
                    bool show = count > 0;
                    if (invBtns[r] != null) invBtns[r].gameObject.SetActive(show);
                    if (invIcons != null && r < invIcons.Length && invIcons[r] != null) invIcons[r].color = RarityCols[r];
                    if (invCounts != null && r < invCounts.Length && invCounts[r] != null) invCounts[r].text = "x" + count;
                    if (invFrames != null && r < invFrames.Length && invFrames[r] != null) invFrames[r].color = (r == selected) ? FrameSel : FrameOff;
                    // Sell/Equip buttons only appear on the selected slime
                    bool sel = show && r == selected;
                    if (invSellBtns != null && r < invSellBtns.Length && invSellBtns[r] != null) invSellBtns[r].gameObject.SetActive(sel);
                    if (invEquipBtns != null && r < invEquipBtns.Length && invEquipBtns[r] != null) invEquipBtns[r].gameObject.SetActive(sel);
                }
        }
    }
}
