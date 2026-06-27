using System;

namespace SlimeRPG
{
    /// <summary>
    /// Idle-game number shortening: 1500 -> "1.5K", 2_300_000 -> "2.3M", and so on through
    /// K, M, B, T, Q (quadrillion) and beyond. Values under 1000 stay as plain integers.
    /// Keeps ~3 significant digits so the text stays compact in pills and labels.
    /// </summary>
    public static class NumberFormat
    {
        static readonly string[] Suffixes =
            { "", "K", "M", "B", "T", "Q", "Qi", "Sx", "Sp", "Oc", "No", "Dc" };

        public static string Short(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return "0";
            if (value < 0) return "-" + Short(-value);
            if (value < 1000) return ((long)Math.Round(value)).ToString();

            int tier = (int)(Math.Log10(value) / 3);
            if (tier >= Suffixes.Length) tier = Suffixes.Length - 1;
            double scaled = value / Math.Pow(1000, tier);

            string num = scaled >= 100 ? scaled.ToString("0")
                       : scaled >= 10  ? scaled.ToString("0.#")
                                       : scaled.ToString("0.##");
            return num + Suffixes[tier];
        }

        public static string Short(long value) => Short((double)value);
        public static string Short(int value) => Short((double)value);
    }
}
