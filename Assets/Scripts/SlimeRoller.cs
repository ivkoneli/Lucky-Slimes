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
        public int gems = 0;              // premium currency (earned from collection-set rewards)
        public int rollCount = 0;

        [Header("Collection reward track")]
        /// <summary>How many collection reward milestones have been claimed (drives the 5→10→15… ladder).</summary>
        public int collectionClaimed = 0;

        [Header("UI References")]
        public Button diceButton;
        public Image diceImage;
        public Text goldText;
        public Text gemsText;
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

        [Header("Collection alert (red dot on the Collection nav button)")]
        public GameObject collectionBadge;
        /// <summary>Per-slime: has the one-time 10-gem "new slime" reward been claimed (by clicking the card)?</summary>
        public bool[] slimeGemClaimed;
        /// <summary>Gems granted the first time you claim a newly-collected slime's card.</summary>
        public const int UnlockGems = 10;

        [Header("Roll Cooldown")]
        public float rollCooldown = 3.3f; // 2s spin + ~1s result hold + small gap, so the pull is readable before the next roll
        public Image cooldownOverlay;   // Filled image over the dice; full -> empty as it cools
        bool _cooling;

        [Header("Auto Roll")]
        /// <summary>When on (unlocked via the Auto Roll skill), the dice re-rolls itself every cooldown.</summary>
        public bool autoRoll = false;

        [Header("Spin Streaks (Gold / Platinum / Diamond)")]
        /// <summary>Chip roots on the spin frame (0=Gold, 1=Platinum, 2=Diamond); hidden until their skill unlocks.</summary>
        public GameObject[] streakChips;
        /// <summary>Radial fill on each chip (same order), 0..1 progress to its next streak roll.</summary>
        public Image[] streakFills;
        /// <summary>Roll ring around the SPIN button — fills as the dice spins; uses a tube-look sprite
        /// (dark in the middle, lighter at the edges) tinted to the current roll-type colour.</summary>
        public Image spinFill;
        /// <summary>The SPIN button graphic; tinted to the rarest armed streak colour, else its base blue.</summary>
        public Image spinButtonBg;
        /// <summary>Ring border just outside the SPIN button; turns a darker streak colour when armed.</summary>
        public Image spinBorder;
        /// <summary>"x/10" counter under the SPIN button (Gold streak); blank until Gold is unlocked.</summary>
        public Text spinCounterText;

        /// <summary>Rolls per streak: Gold=10, Platinum=50, Diamond=100.</summary>
        public readonly int[] streakThresholds = { 10, 50, 100 };
        /// <summary>Luck multiplier applied on a streak roll: Gold ×2, Platinum ×5, Diamond ×10.</summary>
        public readonly float[] streakMult = { 2f, 5f, 10f };
        public bool[] streakUnlocked = new bool[3];
        /// <summary>rollCount at the moment each streak was unlocked — progress counts from here, so streaks
        /// unlocked at different times don't all line up and fire on the same roll.</summary>
        public int[] streakStart = { 0, 0, 0 };
        // SPIN button colour per roll type (0 Gold, 1 Platinum, 2 Diamond)
        static readonly Color[] StreakArmCols = {
            new Color(1f, 0.84f, 0.25f, 1f), new Color(0.80f, 0.84f, 0.92f, 1f), new Color(0.45f, 0.84f, 0.98f, 1f),
        };
        // roll ring: inner (bright original colour) + outer (a bit darker) per roll type
        // roll-ring tint per roll type (the tube sprite shades the middle darker automatically)
        static readonly Color[] StreakRingInner = {
            new Color(1f, 0.82f, 0.22f, 1f), new Color(0.82f, 0.86f, 0.94f, 1f), new Color(0.50f, 0.86f, 1f, 1f),
        };
        // default (normal, non-streak roll): green button + green ring
        static readonly Color DefaultBtn = new Color(0.30f, 0.62f, 0.34f, 1f);
        static readonly Color DefaultRingInner = new Color(0.42f, 0.78f, 0.42f, 1f);

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
            if (spinFill != null) spinFill.fillAmount = 0f;               // roll ring empty until the first roll
            UpdateGoldUI();
            UpdateGemsUI();
            UpdateLuckUI();
            UpdateCollectionBadge();
            UpdateStreakUI();
        }

        /// <summary>Reveal a streak chip (0=Gold, 1=Platinum, 2=Diamond) — called by its skill node.</summary>
        public void UnlockStreak(int i)
        {
            if (i < 0 || i >= 3) return;
            streakUnlocked[i] = true;
            streakStart[i] = rollCount; // start counting this streak from now, not from the very first roll
            if (streakChips != null && i < streakChips.Length && streakChips[i] != null) streakChips[i].SetActive(true);
            UpdateStreakUI();
        }

        /// <summary>Progress into streak i's current cycle, 0..thr — counts rolls SINCE that streak was unlocked
        /// (1..thr across the cycle, thr on the streak roll itself), so the display never skips and unrelated
        /// streaks don't all fire on the same roll.</summary>
        int StreakProgress(int i)
        {
            int thr = streakThresholds[i];
            int rel = rollCount - streakStart[i];
            return rel <= 0 ? 0 : ((rel - 1) % thr) + 1;
        }

        /// <summary>Refresh the streak chip fills, the SPIN button tint + border, and the Gold counter.</summary>
        public void UpdateStreakUI()
        {
            for (int i = 0; i < 3; i++)
            {
                float fill = (float)StreakProgress(i) / streakThresholds[i];  // fills 0→full across the cycle
                if (streakFills != null && i < streakFills.Length && streakFills[i] != null)
                    streakFills[i].fillAmount = streakUnlocked[i] ? fill : 0f;
            }

            // counter tracks the Gold streak (index 0). The ring around the SPIN button is the roll/cooldown
            // animation (driven in CooldownRoutine), NOT the streak — only the Gold chip's ring shows streak.
            if (spinCounterText != null)
            {
                int gthr = streakThresholds[0];
                spinCounterText.text = streakUnlocked[0] ? (StreakProgress(0) + "/" + gthr) : "";
                var pill = spinCounterText.transform.parent;
                if (pill != null) pill.gameObject.SetActive(streakUnlocked[0]);
            }

            // colour the SPIN button + roll ring by the CURRENT roll type: green by default, or this streak's
            // own colour (inner bright + darker outer) on EXACTLY the streak roll itself (10th/50th/100th). Rarest wins.
            int armed = -1;
            for (int i = 2; i >= 0; i--)
                if (streakUnlocked[i] && StreakProgress(i) == streakThresholds[i]) { armed = i; break; }
            Color btn = armed >= 0 ? StreakArmCols[armed] : DefaultBtn;
            Color ring = armed >= 0 ? StreakRingInner[armed] : DefaultRingInner;
            if (spinButtonBg != null) spinButtonBg.color = btn;
            if (spinFill != null) spinFill.color = ring; // tube sprite shades the middle darker on its own
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

            // streak rolls (every 10th/50th/100th SINCE UNLOCK) spike luck; bonuses stack multiplicatively
            float luckBonus = 1f;
            for (int i = 0; i < 3; i++)
            {
                int rel = rollCount - streakStart[i];
                if (streakUnlocked[i] && rel > 0 && rel % streakThresholds[i] == 0) luckBonus *= streakMult[i];
            }
            float effLuck = luckMultiplier * luckBonus;

            float total = 0f;
            var w = new float[rarities.Count];
            for (int i = 0; i < rarities.Count; i++)
            {
                w[i] = rarities[i].baseWeight * Mathf.Pow(effLuck, i);
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
            UpdateStreakUI();
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
            if (spinFill != null) spinFill.fillAmount = 0f; // roll ring starts empty, fills as the dice spins
            float spin = diceSpinner != null ? diceSpinner.spinDuration : 2f;
            float t = 0f;
            while (t < rollCooldown)
            {
                t += Time.deltaTime;
                if (cooldownOverlay != null) cooldownOverlay.fillAmount = 1f - (t / rollCooldown);
                if (spinFill != null) spinFill.fillAmount = Mathf.Clamp01(t / spin); // full once the spin reveals the slime
                yield return null;
            }
            if (cooldownOverlay != null) cooldownOverlay.fillAmount = 0f; // ready again
            if (spinFill != null) spinFill.fillAmount = 1f; // stays full after the slime is rolled, until the next roll
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

        void EnsureClaimed() { if (slimeGemClaimed == null || slimeGemClaimed.Length != (rarities?.Count ?? 0)) slimeGemClaimed = new bool[rarities?.Count ?? 0]; }

        /// <summary>Show the red dot on the Collection nav button when anything in the Collection is claimable
        /// (an unclaimed new-slime gem, or the milestone reward is ready).</summary>
        public void UpdateCollectionBadge()
        {
            EnsureOwned(); EnsureClaimed();
            bool any = CollectionMilestoneReady();
            for (int i = 0; i < owned.Length && i < slimeGemClaimed.Length && !any; i++) if (owned[i] > 0 && !slimeGemClaimed[i]) any = true;
            if (collectionBadge != null) collectionBadge.SetActive(any);
        }

        /// <summary>True if slime i is owned but its one-time 10-gem card reward hasn't been claimed (drives the per-cell red dot).</summary>
        public bool HasUnclaimedSlimeGem(int i)
        {
            EnsureOwned(); EnsureClaimed();
            return i >= 0 && i < owned.Length && i < slimeGemClaimed.Length && owned[i] > 0 && !slimeGemClaimed[i];
        }

        /// <summary>Claim slime i's one-time 10-gem reward (from clicking its Collection card). Returns true if granted.</summary>
        public bool ClaimSlimeGem(int i)
        {
            if (!HasUnclaimedSlimeGem(i)) return false;
            slimeGemClaimed[i] = true;
            AddGems(UnlockGems);
            UpdateCollectionBadge();
            return true;
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
            float dur = 2.1f, tm = 0f; // linger longer so the player can read what they got
            while (tm < dur && rt != null)
            {
                tm += Time.deltaTime; float u = tm / dur;
                rt.anchoredPosition = start + new Vector2(0, 70f * u);
                if (cg != null) cg.alpha = u < 0.78f ? 1f : Mathf.Clamp01((1f - u) / 0.22f);
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
        public void UpdateGemsUI() { if (gemsText != null) gemsText.text = NumberFormat.Short(gems); }
        public void AddGems(int n) { gems += n; UpdateGemsUI(); }
        // Luck shown as a multiplier: 100% -> 1x, 150% -> 1.5x, 200% -> 2x.
        public void UpdateLuckUI() { if (luckText != null) luckText.text = "Luck " + luckMultiplier.ToString("0.##") + "x"; }

        /// <summary>Number of distinct slimes owned (owned[i] &gt; 0).</summary>
        public int UniqueCollected()
        {
            EnsureOwned();
            int n = 0;
            for (int i = 0; i < owned.Length; i++) if (owned[i] > 0) n++;
            return n;
        }

        /// <summary>Number of slimes whose one-time 10-gem reward has been collected — this (NOT mere ownership)
        /// drives the counter + milestone bars, so a slime only counts once its card reward is picked up.</summary>
        public int ClaimedSlimeCount()
        {
            EnsureClaimed();
            int n = 0;
            for (int i = 0; i < slimeGemClaimed.Length; i++) if (slimeGemClaimed[i]) n++;
            return n;
        }

        // ---- collection milestone track (5 -> 10 -> 15 … uniques, 100 gems each) ----
        /// <summary>Uniques needed within the current milestone segment.</summary>
        public int CollectionTarget => 5 * (collectionClaimed + 1);
        /// <summary>Total uniques already accounted for by claimed milestones (5·(1+2+…+claimed)).</summary>
        public int CollectionSegmentBase => 5 * collectionClaimed * (collectionClaimed + 1) / 2;
        /// <summary>Progress into the current milestone segment, counted in COLLECTED slimes (may exceed target until claimed).</summary>
        public int CollectionProgress => ClaimedSlimeCount() - CollectionSegmentBase;
        /// <summary>Is the current milestone reward ready to collect?</summary>
        public bool CollectionMilestoneReady() => CollectionProgress >= CollectionTarget;
    }
}
