using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SlimeRPG
{
    [System.Serializable]
    public class SlimeRarity
    {
        public string name;
        public Color color = Color.white;
        public float baseWeight = 1f;
        /// <summary>Base odds as the 1/x denominator (1/2 -> 2, 1/8 -> 8).</summary>
        public int Denominator => Mathf.Max(1, Mathf.RoundToInt(1f / Mathf.Max(0.0001f, baseWeight)));
    }

    /// <summary>
    /// Core game state + dice roll. Tapping the dice rolls a weighted slime, banks it in the
    /// owned-count inventory, shows a center pop-up (name + base 1/x), and prints a colour-coded
    /// line to the Console. Luck scales rarer tiers up. Sell-dupes keeps 1 copy per rarity.
    /// </summary>
    public class SlimeRoller : MonoBehaviour
    {
        [Header("Rarity Pool (index 0 = most common)")]
        public List<SlimeRarity> rarities = new List<SlimeRarity>();

        [Header("Luck")]
        public float luckMultiplier = 1f;

        [Header("Economy")]
        public int[] sellValues = { 5, 15, 60, 250, 1200 };
        public int[] owned;
        public int gold = 0;
        public int rollCount = 0;

        [Header("UI References")]
        public Button diceButton;
        public Image diceImage;
        public Text goldText;
        public Text luckText;
        public Text popupNameText;
        public Text popupChanceText;
        public GameObject popupRoot;
        public float popupSeconds = 2.2f;
        CanvasGroup _popupCg;
        Coroutine _hideCo;

        [Header("Roll Cooldown")]
        public float rollCooldown = 1.0f;
        public Image cooldownOverlay;   // Filled image over the dice; full -> empty as it cools
        bool _cooling;

        /// <summary>Raised whenever owned counts or gold change (inventory UI listens).</summary>
        public System.Action OnInventoryChanged;
        /// <summary>Raised after a successful roll with the rolled rarity index (team listens).</summary>
        public System.Action<int> OnRolled;

        void Reset() { SetupDefaultPool(); }

        public void SetupDefaultPool()
        {
            rarities = new List<SlimeRarity>
            {
                new SlimeRarity { name = "Common",    color = new Color(0.62f, 0.65f, 0.70f), baseWeight = 1f / 2f },
                new SlimeRarity { name = "Uncommon",  color = new Color(0.35f, 0.82f, 0.40f), baseWeight = 1f / 4f },
                new SlimeRarity { name = "Rare",      color = new Color(0.28f, 0.55f, 1.00f), baseWeight = 1f / 8f },
                new SlimeRarity { name = "Epic",      color = new Color(0.70f, 0.35f, 1.00f), baseWeight = 1f / 16f },
                new SlimeRarity { name = "Legendary", color = new Color(1.00f, 0.80f, 0.16f), baseWeight = 1f / 32f },
            };
            EnsureOwned();
        }

        public void EnsureOwned()
        {
            if (owned == null || owned.Length != rarities.Count) owned = new int[rarities.Count];
        }

        void Start()
        {
            if (rarities == null || rarities.Count == 0) SetupDefaultPool();
            EnsureOwned();
            if (diceButton != null) diceButton.onClick.AddListener(() => Roll());
            if (popupRoot != null)
            {
                _popupCg = popupRoot.GetComponent<CanvasGroup>();
                popupRoot.SetActive(false); // hidden until the first roll
            }
            if (cooldownOverlay != null) cooldownOverlay.fillAmount = 0f; // ready
            UpdateGoldUI();
            UpdateLuckUI();
        }

        public SlimeRarity Roll()
        {
            if (rarities == null || rarities.Count == 0) return null;
            if (_cooling) return null; // on cooldown — wait for the dice to be ready
            EnsureOwned();
            rollCount++;

            float total = 0f;
            var w = new float[rarities.Count];
            for (int i = 0; i < rarities.Count; i++)
            {
                w[i] = rarities[i].baseWeight * Mathf.Pow(luckMultiplier, i);
                total += w[i];
            }
            float r = Random.value * total;
            int picked = rarities.Count - 1;
            float acc = 0f;
            for (int i = 0; i < w.Length; i++)
            {
                acc += w[i];
                if (r <= acc) { picked = i; break; }
            }

            SlimeRarity got = rarities[picked];
            owned[picked]++;
            if (diceImage != null) diceImage.color = got.color;
            ShowPopup(got);

            string hex = ColorUtility.ToHtmlStringRGB(got.color);
            Debug.Log($"<color=#{hex}>● [Roll #{rollCount}] {got.name} Slime!  (base 1/{got.Denominator})  — now own x{owned[picked]}</color>");

            OnInventoryChanged?.Invoke();
            OnRolled?.Invoke(picked);
            if (Application.isPlaying) StartCoroutine(CooldownRoutine());
            return got;
        }

        IEnumerator CooldownRoutine()
        {
            _cooling = true;
            if (cooldownOverlay != null) cooldownOverlay.fillAmount = 1f;
            float t = 0f;
            while (t < rollCooldown)
            {
                t += Time.deltaTime;
                if (cooldownOverlay != null) cooldownOverlay.fillAmount = 1f - (t / rollCooldown);
                yield return null;
            }
            if (cooldownOverlay != null) cooldownOverlay.fillAmount = 0f; // ready again
            _cooling = false;
        }

        void ShowPopup(SlimeRarity got)
        {
            if (popupNameText != null) { popupNameText.text = got.name + " Slime!"; popupNameText.color = got.color; }
            if (popupChanceText != null) { popupChanceText.text = "Base chance  1/" + got.Denominator; }
            if (popupRoot != null)
            {
                popupRoot.SetActive(true);
                if (_popupCg != null) _popupCg.alpha = 1f;
                if (Application.isPlaying)
                {
                    if (_hideCo != null) StopCoroutine(_hideCo);
                    _hideCo = StartCoroutine(HidePopupAfterDelay());
                }
            }
        }

        IEnumerator HidePopupAfterDelay()
        {
            yield return new WaitForSeconds(popupSeconds);
            float t = 0f;
            while (t < 0.4f)
            {
                t += Time.deltaTime;
                if (_popupCg != null) _popupCg.alpha = 1f - (t / 0.4f);
                yield return null;
            }
            if (popupRoot != null) popupRoot.SetActive(false);
            if (_popupCg != null) _popupCg.alpha = 1f;
            _hideCo = null;
        }

        /// <summary>Sells all duplicate copies of a rarity (keeps 1). Returns gold gained.</summary>
        public int SellDupes(int idx)
        {
            EnsureOwned();
            if (idx < 0 || idx >= owned.Length) return 0;
            int dupes = owned[idx] - 1;
            if (dupes <= 0) return 0;
            int val = (idx < sellValues.Length ? sellValues[idx] : 1) * dupes;
            owned[idx] = 1;
            gold += val;
            UpdateGoldUI();
            OnInventoryChanged?.Invoke();
            return val;
        }

        public void UpdateGoldUI() { if (goldText != null) goldText.text = gold.ToString("N0"); }
        public void UpdateLuckUI() { if (luckText != null) luckText.text = "Luck " + Mathf.RoundToInt(luckMultiplier * 100f) + "%"; }
    }
}
