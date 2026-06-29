using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SlimeRPG
{
    /// <summary>
    /// Two-way idle auto-battle. On each tick the team focus-fires the front enemy and the enemies
    /// focus-fire the front hero, spawning floating damage numbers. Clearing the stage advances it;
    /// a full team wipe drops back one stage (no losses). Heroes heal to full each new stage.
    /// Enemies scale per stage; every 10th stage is a single 2x boss slime.
    /// </summary>
    public class CombatManager : MonoBehaviour
    {
        [Header("Wiring")]
        public Transform enemyContainer;
        public Sprite circleSprite;
        public Font font;
        public Text stageNumText;
        public Image[] dots;
        public SlimeRoller roller;

        [Header("Tuning")]
        public float tickInterval = 0.55f;
        public float baseEnemyHp = 40f;
        public float baseEnemyDmg = 4f;
        public int baseGold = 4;
        public float critChance = 0.15f;
        public float damageMult = 1f; // raised by Damage skill (applies to heroes)
        public float goldMult = 1f;   // raised by Gold skill

        public int stage = 1;

        readonly List<Unit> _enemies = new List<Unit>();
        readonly List<Unit> _heroes = new List<Unit>();
        float _timer;

        static readonly Color EnemyCol  = new Color(0.78f, 0.34f, 0.42f);
        static readonly Color EyeDark   = new Color(0.12f, 0.12f, 0.15f);
        static readonly Color Forest    = new Color(0.46f, 0.74f, 0.42f);
        static readonly Color ForestDim = new Color(0.22f, 0.34f, 0.22f);
        static readonly Color DoneCol   = new Color(0.34f, 0.52f, 0.32f);
        static readonly Color HpGreen   = new Color(0.45f, 0.85f, 0.40f);
        static readonly Color HpRed     = new Color(0.88f, 0.36f, 0.34f);

        void Start() { SpawnStage(stage); }

        public void SetHeroes(List<Unit> heroes)
        {
            _heroes.Clear();
            if (heroes != null) _heroes.AddRange(heroes);
            // NOTE: do NOT reset HP here — rolling/equipping must not heal the team.
        }

        void Update()
        {
            if (_enemies.Count == 0) return;
            _timer += Time.deltaTime;
            if (_timer < tickInterval) return;
            _timer = 0f;

            // team attacks the front enemy
            var enemy = FirstAlive(_enemies);
            if (enemy != null)
            {
                foreach (var h in _heroes)
                {
                    if (h == null || !h.Alive) continue;
                    Hit(h, enemy, enemyContainer);
                    if (!enemy.Alive) break;
                }
            }
            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                var e = _enemies[i];
                if (e == null || !e.Alive)
                {
                    if (e != null) { AwardKill(e); Destroy(e.gameObject); }
                    _enemies.RemoveAt(i);
                }
            }
            if (_enemies.Count == 0) { NextStage(); return; }

            // enemies attack the front hero
            var hero = FirstAlive(_heroes);
            if (hero != null)
            {
                foreach (var e in _enemies)
                {
                    if (e == null || !e.Alive) continue;
                    Hit(e, hero, hero.transform.parent);
                    if (!hero.Alive) break;
                }
            }
            // hide downed heroes; wipe if the whole team is down
            foreach (var h in _heroes) if (h != null && !h.Alive && h.gameObject.activeSelf) h.gameObject.SetActive(false);
            if (_heroes.Count > 0 && AllDown(_heroes)) Wipe();
        }

        void Hit(Unit attacker, Unit target, Transform numbersParent)
        {
            float mult = attacker.faceRight ? damageMult : 1f; // Damage skill boosts heroes only
            float dmg = attacker.damage * mult * Random.Range(0.85f, 1.15f);
            bool crit = Random.value < critChance;
            if (crit) dmg *= 2f;
            target.TakeDamage(dmg);
            SpawnDamageNumber(numbersParent, target.GetComponent<RectTransform>(), Mathf.RoundToInt(dmg), crit);
        }

        static Unit FirstAlive(List<Unit> list)
        {
            foreach (var u in list) if (u != null && u.Alive) return u;
            return null;
        }

        static bool AllDown(List<Unit> list)
        {
            foreach (var u in list) if (u != null && u.Alive) return false;
            return true;
        }

        void CullDead(List<Unit> list, bool destroy)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i] == null || !list[i].Alive)
                {
                    if (destroy && list[i] != null) Destroy(list[i].gameObject);
                    list.RemoveAt(i);
                }
            }
        }

        void AwardKill(Unit enemy)
        {
            if (roller == null || enemy.goldReward <= 0) return;
            int g = Mathf.RoundToInt(enemy.goldReward * goldMult);
            roller.gold += g;
            roller.UpdateGoldUI();
            roller.OnInventoryChanged?.Invoke();
            SpawnGoldNumber(enemy.GetComponent<RectTransform>(), g);
        }

        void SpawnGoldNumber(RectTransform at, int amount)
        {
            if (at == null) return;
            var go = new GameObject("Gold", typeof(RectTransform)); go.transform.SetParent(enemyContainer, false);
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(220, 56);
            rt.anchoredPosition = at.anchoredPosition + new Vector2(0, -at.sizeDelta.y * 0.4f);
            var t = go.AddComponent<Text>();
            t.font = font; t.text = "+" + NumberFormat.Short(amount) + "g"; t.alignment = TextAnchor.MiddleCenter; t.fontSize = 36;
            t.color = new Color(1f, 0.84f, 0.25f); t.fontStyle = FontStyle.Bold;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            go.AddComponent<DamageNumber>().Init(t);
        }

        void NextStage() { stage++; SpawnStage(stage); }

        void Wipe() { stage = Mathf.Max(1, stage - 1); SpawnStage(stage); }

        void ResetHeroes() { foreach (var h in _heroes) if (h != null) h.ResetFull(); }

        public void SpawnStage(int s)
        {
            foreach (var e in _enemies) if (e != null) Destroy(e.gameObject);
            _enemies.Clear();

            if (stageNumText != null) stageNumText.text = "Stage " + s;
            UpdateDots(s);
            ResetHeroes();

            bool boss = (s % 10 == 0);
            if (boss)
            {
                float k = Mathf.Max(1f, s / 10f);
                var u = CreateUnit(enemyContainer, new Vector2(330, -90), 240f, EnemyCol, false, baseEnemyHp * 16f * k, baseEnemyDmg * 4f * k);
                u.goldReward = Mathf.RoundToInt((baseGold + s * 2) * 10f);
                _enemies.Add(u);
            }
            else
            {
                int count = Mathf.Clamp(1 + (s - 1) / 2, 1, 5);
                float size = Mathf.Min(118f + (s - 1) * 3f, 150f);
                float hp = baseEnemyHp * (1f + 0.32f * (s - 1));
                float dmg = baseEnemyDmg * (1f + 0.30f * (s - 1));
                int gold = baseGold + s * 2;
                var pts = EnemyFormation(count);
                for (int i = 0; i < count; i++)
                {
                    var u = CreateUnit(enemyContainer, pts[i], size, EnemyCol, false, hp, dmg);
                    u.goldReward = gold;
                    _enemies.Add(u);
                }
            }
        }

        void UpdateDots(int s)
        {
            if (dots == null) return;
            int cur = (s - 1) % 10;
            for (int i = 0; i < dots.Length; i++)
            {
                if (dots[i] == null) continue;
                // The boss dot (index 9) is just bigger — same green palette as the rest.
                if (i == cur) dots[i].color = Forest;
                else if (i < cur) dots[i].color = DoneCol;
                else dots[i].color = ForestDim;
            }
        }

        public static Vector2[] EnemyFormation(int count)
        {
            switch (count)
            {
                case 1: return new[] { new Vector2(330, -90) };
                case 2: return new[] { new Vector2(300, -50), new Vector2(380, -160) };
                case 3: return new[] { new Vector2(280, -40), new Vector2(405, -95), new Vector2(320, -175) };
                case 4: return new[] { new Vector2(270, -25), new Vector2(405, -55), new Vector2(290, -165), new Vector2(425, -185) };
                default: return new[] { new Vector2(255, -90), new Vector2(370, -20), new Vector2(370, -160), new Vector2(470, -55), new Vector2(470, -190) };
            }
        }

        /// <summary>Builds a slime blob with eyes + a thick HP bar that shows cur/max. Returns the Unit.</summary>
        public Unit CreateUnit(Transform parent, Vector2 pos, float size, Color color, bool faceRight, float hp, float dmg)
        {
            var go = new GameObject("Unit", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size); rt.anchoredPosition = pos;
            var img = go.AddComponent<Image>(); img.sprite = circleSprite; img.color = color;

            float face = size * 0.12f * (faceRight ? 1f : -1f);
            AddDot(go.transform, new Vector2(-size * 0.16f + face, size * 0.06f), size * 0.16f);
            AddDot(go.transform, new Vector2(size * 0.16f + face, size * 0.06f), size * 0.16f);

            // thick HP bar above the unit; fill is left-anchored and resized by % HP
            float barW = Mathf.Max(size * 1.05f, 130f);
            var bar = new GameObject("HpBar", typeof(RectTransform)); bar.transform.SetParent(go.transform, false);
            var brt = bar.GetComponent<RectTransform>(); brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f); brt.pivot = new Vector2(0.5f, 0.5f);
            brt.sizeDelta = new Vector2(barW, 30); brt.anchoredPosition = new Vector2(0, size * 0.66f);
            // override sorting so every HP bar draws ABOVE all slime bodies (never hidden under another slime)
            var barCanvas = bar.AddComponent<Canvas>(); barCanvas.overrideSorting = true; barCanvas.sortingOrder = 20;
            var barBg = bar.AddComponent<Image>(); barBg.color = new Color(0, 0, 0, 0.7f);

            float innerW = barW - 6f;
            var fillGo = new GameObject("Fill", typeof(RectTransform)); fillGo.transform.SetParent(bar.transform, false);
            var frt = fillGo.GetComponent<RectTransform>(); frt.anchorMin = frt.anchorMax = new Vector2(0, 0.5f); frt.pivot = new Vector2(0, 0.5f);
            frt.sizeDelta = new Vector2(innerW, 24); frt.anchoredPosition = new Vector2(3, 0);
            var fill = fillGo.AddComponent<Image>(); fill.color = faceRight ? HpGreen : HpRed;

            var hpTextGo = new GameObject("HpText", typeof(RectTransform)); hpTextGo.transform.SetParent(bar.transform, false);
            var htrt = hpTextGo.GetComponent<RectTransform>(); htrt.anchorMin = Vector2.zero; htrt.anchorMax = Vector2.one; htrt.offsetMin = Vector2.zero; htrt.offsetMax = Vector2.zero;
            var hpText = hpTextGo.AddComponent<Text>(); hpText.font = font; hpText.alignment = TextAnchor.MiddleCenter; hpText.fontSize = 18; hpText.color = Color.white;
            hpText.horizontalOverflow = HorizontalWrapMode.Overflow; hpText.verticalOverflow = VerticalWrapMode.Overflow;

            var unit = go.AddComponent<Unit>(); unit.faceRight = faceRight; unit.hpFillRect = frt; unit.hpFillWidth = innerW; unit.hpText = hpText; unit.Init(hp, dmg);
            return unit;
        }

        void AddDot(Transform parent, Vector2 pos, float size)
        {
            var go = new GameObject("Eye", typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size); rt.anchoredPosition = pos;
            var img = go.AddComponent<Image>(); img.sprite = circleSprite; img.color = EyeDark;
        }

        void SpawnDamageNumber(Transform parent, RectTransform at, int amount, bool crit)
        {
            if (at == null || parent == null) return;
            var go = new GameObject("Dmg", typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(220, 60);
            rt.anchoredPosition = at.anchoredPosition + new Vector2(Random.Range(-24f, 24f), at.sizeDelta.y * 0.4f);

            var t = go.AddComponent<Text>();
            t.font = font; t.text = NumberFormat.Short(amount); t.alignment = TextAnchor.MiddleCenter;
            t.fontSize = crit ? 56 : 40;
            t.color = crit ? new Color(1f, 0.85f, 0.2f) : Color.white;
            t.fontStyle = crit ? FontStyle.Bold : FontStyle.Normal;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;

            go.AddComponent<DamageNumber>().Init(t);
        }
    }
}
