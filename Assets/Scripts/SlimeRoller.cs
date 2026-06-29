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
        public GameObject rollLabel;   // "TAP TO ROLL" — hidden after the first roll
        public float popupSeconds = 2.2f;
        CanvasGroup _popupCg;
        Coroutine _hideCo;

        [Header("Dice Animation")]
        /// <summary>Plays the 2s tumble on each roll (manual tap or Auto Roll).</summary>
        public DiceSpinner diceSpinner;
        public RectTransform plusOneAnchor; // the dice rect — floating "+1" spawns here on a duplicate
        public Font font;                   // for the floating "+1" text

        [Header("Collection alert (badge on the Collection nav button)")]
        public GameObject collectionBadge;
        public Text collectionBadgeText;
        bool[] _seen;

        [Header("Roll Cooldown")]
        public float rollCooldown = 2.5f; // 2s spin + 0.5s gap so the result is readable before the next roll
        public Image cooldownOverlay;   // Filled image over the dice; full -> empty as it cools
        bool _cooling;

        [Header("Auto Roll")]
        /// <summary>When on (unlocked via the Auto Roll skill), the dice re-rolls itself every cooldown.</summary>
        public bool autoRoll = false;

        [Header("Streak Cubes")]
        /// <summary>Gold/Platinum/Diamond mini-cubes on the dice ring; revealed as roll-streak skills unlock (future).</summary>
        public GameObject[] streakCubes;

        /// <summary>Raised whenever owned counts or gold change (inventory UI listens).</summary>
        public System.Action OnInventoryChanged;
        /// <summary>Raised after a successful roll with the rolled rarity index (team listens).</summary>
        public System.Action<int> OnRolled;

        void Reset() { SetupDefaultPool(); }

        public void SetupDefaultPool()
        {
            // starter slimes — all "Common" rarity, distinguished by number + colour (stats/odds kept)
            rarities = new List<SlimeRarity>
            {
                new SlimeRarity { name = "Common Slime 1", color = new Color(0.62f, 0.65f, 0.70f), baseWeight = 1f / 2f },
                new SlimeRarity { name = "Common Slime 2", color = new Color(0.35f, 0.82f, 0.40f), baseWeight = 1f / 4f },
                new SlimeRarity { name = "Common Slime 3", color = new Color(0.28f, 0.55f, 1.00f), baseWeight = 1f / 8f },
                new SlimeRarity { name = "Common Slime 4", color = new Color(0.70f, 0.35f, 1.00f), baseWeight = 1f / 16f },
                new SlimeRarity { name = "Common Slime 5", color = new Color(1.00f, 0.80f, 0.16f), baseWeight = 1f / 32f },
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
            UpdateCollectionBadge();
        }

        void Update()
        {
            // Auto Roll: fire on cooldown with no tap once the skill is unlocked.
            if (autoRoll && !_cooling && Application.isPlaying) Roll();
        }

        public SlimeRarity Roll()
        {
            if (rarities == null || rarities.Count == 0) return null;
            if (_cooling) return null; // on cooldown — wait for the dice to be ready
            EnsureOwned();
            rollCount++;
            if (rollLabel != null && rollCount == 1) rollLabel.SetActive(false); // only show "TAP TO ROLL" before the first roll

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
            bool isNew = owned[picked] == 0; // first time we've ever pulled this one
            owned[picked]++;

            string hex = ColorUtility.ToHtmlStringRGB(got.color);
            Debug.Log($"<color=#{hex}>● [Roll #{rollCount}] {got.name}{(isNew ? " (NEW!)" : "")}  (base 1/{got.Denominator})  — now own x{owned[picked]}</color>");

            OnInventoryChanged?.Invoke();
            if (diceSpinner != null) diceSpinner.Spin(got.color);
            // equip + reveal happen once the reel LANDS (not at roll start), so auto-equip waits for the animation
            if (diceSpinner != null && Application.isPlaying) StartCoroutine(RevealAfter(diceSpinner.spinDuration, got, isNew, picked));
            else { OnRolled?.Invoke(picked); SpawnReveal(got.color, got.name, isNew); UpdateCollectionBadge(); }
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
            if (popupNameText != null) { popupNameText.text = got.name; popupNameText.color = got.color; }
            if (popupChanceText != null) { popupChanceText.text = "1/" + got.Denominator; }
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

        /// <summary>When the reel lands: auto-equip (if applicable), show the floater, bump the collection alert.</summary>
        IEnumerator RevealAfter(float delay, SlimeRarity got, bool isNew, int picked)
        {
            yield return new WaitForSeconds(delay);
            OnRolled?.Invoke(picked); // auto-equip the first slime here, AFTER the animation
            SpawnReveal(got.color, got.name, isNew);
            UpdateCollectionBadge();
        }

        void EnsureSeen() { if (_seen == null || _seen.Length != (rarities?.Count ?? 0)) _seen = new bool[rarities?.Count ?? 0]; }

        /// <summary>Shows "(N)" on the Collection nav button = owned slimes not yet viewed in the Collection.</summary>
        public void UpdateCollectionBadge()
        {
            EnsureOwned(); EnsureSeen();
            int n = 0;
            for (int i = 0; i < owned.Length && i < _seen.Length; i++) if (owned[i] > 0 && !_seen[i]) n++;
            if (collectionBadge != null) collectionBadge.SetActive(n > 0);
            if (collectionBadgeText != null) collectionBadgeText.text = n.ToString();
        }

        /// <summary>Mark every owned slime as viewed (call when the Collection opens) and clear the badge.</summary>
        public void MarkCollectionSeen()
        {
            EnsureOwned(); EnsureSeen();
            for (int i = 0; i < owned.Length && i < _seen.Length; i++) if (owned[i] > 0) _seen[i] = true;
            UpdateCollectionBadge();
        }

        /// <summary>Small floater out of the dice: "+1" (or "NEW!") + the slime's name, in its colour.</summary>
        public void SpawnReveal(Color c, string slimeName, bool isNew)
        {
            if (!Application.isPlaying || plusOneAnchor == null || font == null) return;
            var go = new GameObject("RollReveal", typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(plusOneAnchor.parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = plusOneAnchor.anchorMin; rt.anchorMax = plusOneAnchor.anchorMax; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(460, 120);
            rt.anchoredPosition = plusOneAnchor.anchoredPosition + new Vector2(0, 40f);
            var cg = go.GetComponent<CanvasGroup>();
            MakeFloatText(go.transform, isNew ? "NEW!" : "+1", isNew ? new Color(1f, 0.86f, 0.3f) : c, 50, new Vector2(0, 30));
            MakeFloatText(go.transform, slimeName, c, 34, new Vector2(0, -28));
            StartCoroutine(FloatReveal(rt, cg));
        }

        void MakeFloatText(Transform parent, string s, Color c, int size, Vector2 pos)
        {
            var go = new GameObject("T", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(460, 56); rt.anchoredPosition = pos;
            var t = go.AddComponent<Text>();
            t.font = font; t.text = s; t.fontSize = size; t.fontStyle = FontStyle.Bold; t.alignment = TextAnchor.MiddleCenter; t.color = c;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            var o = go.AddComponent<UnityEngine.UI.Outline>(); o.effectColor = new Color(0f, 0f, 0f, 0.9f); o.effectDistance = new Vector2(2.5f, -2.5f);
        }

        IEnumerator FloatReveal(RectTransform rt, CanvasGroup cg)
        {
            Vector2 start = rt.anchoredPosition;
            float dur = 1.2f, tm = 0f;
            while (tm < dur && rt != null)
            {
                tm += Time.deltaTime; float u = tm / dur;
                rt.anchoredPosition = start + new Vector2(0, 80f * u);
                if (cg != null) cg.alpha = u < 0.65f ? 1f : Mathf.Clamp01((1f - u) / 0.35f);
                yield return null;
            }
            if (rt != null) Destroy(rt.gameObject);
        }

        /// <summary>
        /// Colours flashing on the dice while it spins — a CURATED list of "relevant" slimes.
        /// TODO (manual tuning later): drop tiers whose effective chance (baseWeight*luck^i) is below
        /// ~0.0001% (too rare to tease), AND drop the lowest commons once luck is very high (e.g. 100x)
        /// since they're no longer relevant. For now returns every rarity colour.
        /// </summary>
        public Color[] GetReelColors()
        {
            if (rarities == null || rarities.Count == 0) return null;
            var list = new List<Color>(rarities.Count);
            for (int i = 0; i < rarities.Count; i++) list.Add(rarities[i].color);
            return list.ToArray();
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

        public void UpdateGoldUI() { if (goldText != null) goldText.text = NumberFormat.Short(gold); }
        // Luck shown as a multiplier: 100% -> 1x, 150% -> 1.5x, 200% -> 2x.
        public void UpdateLuckUI() { if (luckText != null) luckText.text = "Luck " + luckMultiplier.ToString("0.##") + "x"; }
    }
}
