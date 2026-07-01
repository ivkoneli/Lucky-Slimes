using UnityEngine;
using UnityEngine.UI;

namespace SlimeRPG
{
    /// <summary>
    /// Collection screen: a cell per slime type showing its base drop chance (1/x). Un-obtained =
    /// dark silhouette + "???" (no eyes); obtained = the coloured slime (with eyes) + name.
    /// Clicking a newly-collected card claims a one-time 10-gem reward (red dot until claimed).
    /// A top counter tracks unique slimes collected out of the total, and a bottom reward bar
    /// fills as you collect uniques — reaching the milestone lets you Collect gems (5 → 10 → 15 …).
    /// </summary>
    public class CollectionUI : MonoBehaviour
    {
        public SlimeRoller roller;
        public Image[] icons;       // body per rarity
        public GameObject[] eyes;   // face — only shown once obtained
        public Text[] names;
        public Text[] chances;
        public GameObject[] newDots; // red "claim 10 gems" dot per cell
        public Button[] cells;       // whole card is clickable to claim the slime's gem

        [Header("Top counter (unique collected / total)")]
        public Text topCountText;
        public RectTransform topFill;

        [Header("Reward track (milestone bar + collect)")]
        public Text rewardLabel;    // "3/5"
        public RectTransform rewardFill;
        public Button claimButton;
        public GameObject claimDot;  // red dot on the collect button when a reward is ready

        /// <summary>Total slimes to collect (placeholder until the full 100-slime pool exists).</summary>
        public const int TotalSlimes = 100;
        /// <summary>Gems granted per collection milestone.</summary>
        public const int RewardGems = 100;

        static readonly Color Silhouette = new Color(0.20f, 0.22f, 0.26f, 1f);
        static readonly Color QName = new Color(0.50f, 0.52f, 0.56f, 1f);
        static readonly Color ClaimReady = new Color(0.46f, 0.74f, 0.42f, 1f);   // green
        static readonly Color ClaimIdle = new Color(0.24f, 0.27f, 0.31f, 1f);    // dim gray
        static readonly Color[] RarityCols = {
            new Color(0.62f, 0.65f, 0.70f), new Color(0.35f, 0.82f, 0.40f), new Color(0.28f, 0.55f, 1f),
            new Color(0.70f, 0.35f, 1f), new Color(1f, 0.80f, 0.16f)
        };

        // Wire button listeners at runtime — listeners added from the editor build script aren't serialized.
        void Awake()
        {
            if (claimButton != null) claimButton.onClick.AddListener(OnClaim);
            if (cells != null)
                for (int i = 0; i < cells.Length; i++)
                {
                    if (cells[i] == null) continue;
                    int idx = i;
                    cells[i].onClick.AddListener(() => OnCellClicked(idx));
                }
        }

        void OnEnable() { Refresh(); }

        /// <summary>Click a card to claim its one-time 10-gem "new slime" reward (does nothing once claimed).</summary>
        void OnCellClicked(int i)
        {
            if (roller != null && roller.ClaimSlimeGem(i)) Refresh();
        }

        /// <summary>Collect the ready milestone reward: grant gems, advance the ladder, refresh.</summary>
        public void OnClaim()
        {
            if (roller == null || !roller.CollectionMilestoneReady()) return;
            roller.AddGems(RewardGems);
            roller.collectionClaimed++;
            roller.UpdateCollectionBadge();
            Refresh();
        }

        public void Refresh()
        {
            if (roller == null || icons == null) return;
            roller.EnsureOwned();
            for (int i = 0; i < icons.Length; i++)
            {
                bool owned = roller.owned != null && i < roller.owned.Length && roller.owned[i] > 0;
                if (icons[i] != null) icons[i].color = owned ? RarityCols[i] : Silhouette;
                if (eyes != null && i < eyes.Length && eyes[i] != null) eyes[i].SetActive(owned);
                if (names != null && i < names.Length && names[i] != null)
                {
                    names[i].text = owned ? roller.rarities[i].name : "???";
                    names[i].color = owned ? RarityCols[i] : QName;
                }
                if (chances != null && i < chances.Length && chances[i] != null)
                    chances[i].text = "1/" + roller.rarities[i].Denominator;
                if (newDots != null && i < newDots.Length && newDots[i] != null)
                    newDots[i].SetActive(roller.HasUnclaimedSlimeGem(i));
            }

            // top counter + fill — counts slimes whose reward has been collected, not merely owned
            int collected = roller.ClaimedSlimeCount();
            if (topCountText != null) topCountText.text = collected + "/" + TotalSlimes;
            if (topFill != null) SetFill(topFill, (float)collected / TotalSlimes);

            // reward track
            int target = roller.CollectionTarget;
            int prog = Mathf.Clamp(roller.CollectionProgress, 0, target);
            bool ready = roller.CollectionMilestoneReady();
            if (rewardLabel != null) rewardLabel.text = prog + "/" + target;
            if (rewardFill != null) SetFill(rewardFill, target > 0 ? (float)prog / target : 0f);
            if (claimDot != null) claimDot.SetActive(ready);
            if (claimButton != null && claimButton.image != null) claimButton.image.color = ready ? ClaimReady : ClaimIdle;
        }

        // A fill bar is a left-anchored child whose right edge (anchorMax.x) tracks the fraction.
        static void SetFill(RectTransform fill, float pct)
        {
            pct = Mathf.Clamp01(pct);
            var max = fill.anchorMax; max.x = pct; fill.anchorMax = max;
            fill.offsetMin = Vector2.zero; fill.offsetMax = Vector2.zero;
        }
    }
}
