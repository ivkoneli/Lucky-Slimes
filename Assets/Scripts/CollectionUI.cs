using UnityEngine;
using UnityEngine.UI;

namespace SlimeRPG
{
    /// <summary>
    /// Collection screen: a cell per slime type showing its base drop chance (1/x). Un-obtained =
    /// dark silhouette + "???" (no eyes); obtained = the coloured slime (with eyes) + name. Opening
    /// the screen marks everything owned as seen, clearing the "new" badge on the Collection nav button.
    /// </summary>
    public class CollectionUI : MonoBehaviour
    {
        public SlimeRoller roller;
        public Image[] icons;       // body per rarity
        public GameObject[] eyes;   // face — only shown once obtained
        public Text[] names;
        public Text[] chances;

        static readonly Color Silhouette = new Color(0.20f, 0.22f, 0.26f, 1f);
        static readonly Color QName = new Color(0.50f, 0.52f, 0.56f, 1f);
        static readonly Color[] RarityCols = {
            new Color(0.62f, 0.65f, 0.70f), new Color(0.35f, 0.82f, 0.40f), new Color(0.28f, 0.55f, 1f),
            new Color(0.70f, 0.35f, 1f), new Color(1f, 0.80f, 0.16f)
        };

        void OnEnable() { Refresh(); if (roller != null) roller.MarkCollectionSeen(); }

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
            }
        }
    }
}
