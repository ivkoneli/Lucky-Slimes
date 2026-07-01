using System.Collections.Generic;
using UnityEngine;

namespace SlimeRPG
{
    /// <summary>
    /// Single source of truth for the 7 rarity tiers and the 50-slime placeholder pool. Both the
    /// runtime (SlimeRoller/TeamManager/Inventory/Collection) and the editor scene builder read from
    /// here, so tuning tiers = one edit. Each tier has a border colour (shown as the card border once
    /// obtained), per-slime body colours (slight variations of that colour), stats (~2.3x per tier),
    /// a sell value, and a base drop weight. Placeholder names are "{Tier} Slime {n}".
    /// </summary>
    public static class SlimeCatalog
    {
        public const int TierCount = 7;

        public static readonly string[] TierNames = { "Common", "Uncommon", "Rare", "Epic", "Legendary", "Mythic", "Divine" };

        // Border colour per tier (gray / green / blue / purple / gold / red / snow-white for Divine
        // until we add a prismarine shader). Also the base body colour before per-slime variation.
        public static readonly Color[] TierBorder = {
            new Color(0.62f, 0.65f, 0.70f), // Common  – gray
            new Color(0.38f, 0.82f, 0.44f), // Uncommon – green
            new Color(0.28f, 0.55f, 1.00f), // Rare    – blue
            new Color(0.72f, 0.38f, 1.00f), // Epic    – purple
            new Color(1.00f, 0.80f, 0.16f), // Legendary – golden yellow
            new Color(0.92f, 0.28f, 0.34f), // Mythic  – red
            new Color(0.94f, 0.97f, 1.00f), // Divine  – snow white (prismarine shader later)
        };

        public static readonly int[]   PerTier   = { 8, 8, 8, 7, 7, 6, 6 };                 // sums to 50
        public static readonly float[] TierDps    = { 6f, 14f, 32f, 74f, 170f, 390f, 900f };  // ~2.3x/tier
        public static readonly float[] TierHp     = { 80f, 180f, 420f, 970f, 2200f, 5000f, 11500f };
        public static readonly int[]   TierSell   = { 5, 15, 45, 140, 430, 1300, 4000 };
        // Per-slime base drop weight (the "1/x" shown on the card is 1/weight; real odds are normalised).
        public static readonly float[] TierWeight = { 0.5f, 0.1f, 0.025f, 0.006f, 0.0012f, 0.0002f, 0.00002f };

        public static int Total { get { int t = 0; for (int i = 0; i < PerTier.Length; i++) t += PerTier[i]; return t; } }

        public static Color Border(int tier) => TierBorder[Mathf.Clamp(tier, 0, TierCount - 1)];

        /// <summary>Body colour = a slight per-slime variation of the tier's border colour (brightness + a touch of hue).</summary>
        public static Color BodyColor(int tier, int idxInTier, int countInTier)
        {
            Color baseC = Border(tier);
            float t = countInTier <= 1 ? 0.5f : (float)idxInTier / (countInTier - 1); // 0..1 across the tier
            Color.RGBToHSV(baseC, out float h, out float s, out float v);
            h = Mathf.Repeat(h + (t - 0.5f) * 0.05f, 1f);      // ±0.025 hue drift
            s = Mathf.Clamp01(s * (0.85f + 0.30f * t));        // a bit less/more saturated
            v = Mathf.Clamp01(v * (0.80f + 0.40f * t));        // darker -> lighter
            return Color.HSVToRGB(h, s, v);
        }

        /// <summary>Build the full 50-slime pool (deterministic order, so owned[]/collection/inventory indices align).</summary>
        public static List<SlimeRarity> BuildPool()
        {
            var list = new List<SlimeRarity>(Total);
            for (int tier = 0; tier < TierCount; tier++)
                for (int j = 0; j < PerTier[tier]; j++)
                    list.Add(new SlimeRarity
                    {
                        name = TierNames[tier] + " Slime " + (j + 1),
                        color = BodyColor(tier, j, PerTier[tier]),
                        baseWeight = TierWeight[tier],
                        tier = tier,
                    });
            return list;
        }
    }
}
