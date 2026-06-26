# Slime RPG — Game Design Document (v0.1)

> **Status:** Living draft. Created 2026-06-26.
> **Engine:** Unity (URP 2D). **Platforms:** Android / iOS (mobile-first, portrait).
> **Genre:** Idle Gacha RPG / Incremental — "roll, build a team, push, prestige."
> **Inspiration:** *Slime RPG* (Roblox 3D) reimagined as a 2D mobile game.

---

## 1. High Concept

You tap a **dice** to roll for **heroes** of escalating rarity. Your best 5 heroes form an
**auto-battling team** that pushes through **worlds made of stages**, killing mobs and bosses
for **gold**. Gold and progress feed a **hexagonal upgrade tree** (unlock a node → its neighbors
open up) that boosts luck, damage, gold, respawn speed and more. Deep in that tree you can unlock
a skill that makes mobs rarely drop **Ascension Shards** — collect enough and you **ascend**:
reset your run but keep your skills, start at **×2 luck**, and unlock even rarer hero tiers.

**The fantasy:** "One more roll." Each tap might be the 1-in-250,000 pull.

---

## 2. Core Loop

```
        ┌──────────────────────────────────────────────┐
        │                                              │
        ▼                                              │
   TAP DICE  ──►  GET HERO (rarity gated by luck)  ──► BUILD TEAM (top 5)
        ▲                                              │
        │                                              ▼
   spend gold/                                   TEAM AUTO-FIGHTS
   shards on                                     stages & bosses
   UPGRADE TREE                                        │
        ▲                                              ▼
        │                                          EARN GOLD
        └──────────────  +  COLLECTION BONUSES  ◄──────┘

   ...after enough tree investment → unlock Ascension Shard drop skill →
   collect shards → ASCEND (keep skills, ×2 luck, rarer heroes) → loop deepens
```

**Minute-to-minute:** tap dice, watch pulls, drag good heroes into team, spend gold in tree.
**Session-to-session:** push a new world, complete collection entries, save toward ascension.
**Meta:** ascend, climb faster, chase top-tier heroes that only appear at high luck.

---

## 3. The Dice (Core Mechanic)

- A large, satisfying tappable dice in the center/bottom of the main screen.
- **Cost:** Free, on a short cooldown (start ~3s). Cooldown is reducible via the upgrade tree
  and an **Auto-Roll** unlock.
- Each tap = one roll = one hero pulled, with a **rarity reveal animation** (color burst, screen
  shake scaling with rarity, rare pulls get a full-screen flash).
- **Bulk roll (×10):** unlocked later; instant, optionally an ad/IAP convenience or gold cost.
- **Dice skins:** cosmetic (IAP / event rewards). Some skins are purely visual; no pay-to-win odds.

### Luck & Odds

Each rarity has a **base probability**. Your **Luck multiplier** improves the effective chance of
rarer tiers. Recommended implementation: roll from rarest → most common; for each tier,
`hitChance = baseChance × luck`; first tier that "hits" is the result (fallback = Common).

**Proposed rarity table (8 tiers, base odds at Luck ×1):**

| Tier        | Base Odds   | Color        | Notes                                    |
|-------------|-------------|--------------|------------------------------------------|
| Common      | 1 / 2       | Gray         | Filler, fuel for Sell Dupes              |
| Uncommon    | 1 / 6       | Green        |                                          |
| Rare        | 1 / 25      | Blue         |                                          |
| Epic        | 1 / 100     | Purple       |                                          |
| Legendary   | 1 / 500     | Gold         | First "big pull" moment                  |
| Mythic      | 1 / 2,500   | Red          |                                          |
| Secret      | 1 / 25,000  | Iridescent   | Mostly reachable only with high luck     |
| Godly       | 1 / 250,000 | Rainbow/VFX  | Ascension-gated; the lifetime chase      |

> Tiers can be **unlocked progressively**: Secret/Godly only start appearing after enough luck /
> after first ascension, so early game stays readable.

- **Luck Roll Streaks (unlockable skills, NOT hidden pity):** instead of a hidden pity counter,
  the player *buys* deterministic milestone-roll luck spikes from the tree. The roll counter is
  visible, so the player feels the build-up to a big roll:

  | Skill         | Trigger              | Bonus on that roll |
  |---------------|----------------------|--------------------|
  | **Gold Roll** | every **10th** roll  | ×2 luck            |
  | **Platinum Roll** | every **50th** roll | ×5 luck         |
  | **Diamond Roll** | every **100th** roll | ×10 luck       |

  Each is a separate unlockable (and further upgradable) skill. On a roll that hits multiple
  milestones (e.g. roll #100 is also a multiple of 50 and 10), bonuses **stack multiplicatively**
  → a massive ×100 "jackpot roll" every 100th tap. Tunable; could cap or use highest-tier if ×100
  proves too swingy. This replaces a classic pity system and turns anti-frustration into a
  *purchasable, anticipated* feature.
- **Luck Surge (optional juice):** short timed buff (from ads/skill) that multiplies luck — great
  rewarded-ad hook.

---

## 4. Heroes

- Heroes are a **collection**; you own every copy you pull (no forced deletion).
- **Stats:** primarily **DPS**, plus a secondary flavor (HP/ability) for synergy later.
- **Class/Element tags** for team synergy (e.g. Fire/Water/Earth, or Tank/DPS/Support).

### Duplicates & Sinks
- **Sell Dupes button:** one tap converts surplus copies into gold (keeps at least 1 of each, or a
  configurable "keep N"). Also supports selling a chosen quantity.
- **Collection Book bonuses:** owning each **unique** hero grants a **permanent passive** (small
  luck/damage/gold bonus). Completing a **set** (a world's roster, a full rarity) grants a bigger
  passive **plus a generous chunk of the premium currency (Gems)**. This is deliberate: it teaches
  free players to *use* Gems and lets them experience premium features, so the f2p experience feels
  close to paid — paying players just get there faster and stack permanent multipliers on top.
- **Merge / Upgrade (unlocked later):** spend dupes + currency to **level/evolve** heroes, craft
  **missing** heroes, or forge **special** exclusive heroes. This turns "useless" dupes into a
  long-term resource and lets unlucky players still complete the book.

### Team
- **Up to 5 heroes** fielded at once; they auto-attack. Team DPS = sum (+ synergy bonuses).
- Simple **auto-fill "best team"** button + manual drag for min-maxers.
- Later: **synergy bonuses** for matching classes/elements to reward thoughtful comps.

> **Art direction TBD:** heroes could be slimes (on-theme, cheap to produce many variants by
> recoloring/reshaping a base slime) or full characters. **Recommendation:** slime-based heroes —
> fits the title, makes 100s of cheap collectible variants feasible, and reads great in 2D.

---

## 5. World & Combat (Idle Auto-Battler)

- Structure: **Worlds → Stages.** Each World (Forest, Cave, Volcano, …) contains numbered stages.
- Per stage: a few **mob waves**, periodic **mini-bosses**, and a **World Boss** gate at the end.
- **Bosses have a timer** (DPS check). The boss sits at the **last stage of a chunk** (e.g. stage
  10). **Failing the timer drops you back one stage** (stage 9), where you **farm normal mobs** for
  gold and skill upgrades until you're strong enough to clear the boss. This is the natural **soft
  wall** — no hard gate, just "grind the stage before the boss." Players will spam stage 9, roll
  more, and buy tree upgrades until they out-DPS the boss timer.
- **Combat is automatic** — heroes fire on their own; player optionally taps for a manual
  attack/ability for a small bonus (keeps hands busy without requiring it).
- **Respawn:** when a hero/team wipes (or for boss retries), a short respawn timer ticks; respawn
  speed is an upgrade-tree stat.
- **Offline / idle earnings:** timestamp-based; team keeps farming the current stage while away
  (capped duration, extendable via ad/IAP). Essential for the genre.

---

## 6. Hexagonal Upgrade Tree

- A **honeycomb** of upgrade nodes. **Unlocking a node activates its adjacent neighbors**, letting
  the player choose a path. Each node has levels (gold cost scales per level).
- **Branches (suggested):**
  - **Luck** — base luck multiplier, surge duration, and the **Luck Roll Streak** skills
    (Gold/Platinum/Diamond — see §3) that spike luck on every 10th/50th/100th roll.
  - **Damage** — team DPS %, crit chance/damage, boss damage, per-class buffs.
  - **Economy** — gold per kill, gold multiplier, dupe sell value, offline cap.
  - **Utility** — dice cooldown, auto-roll, respawn speed, offline duration.
  - **Ascension (gated deep)** — the special node: **"Mobs have X% chance to drop an Ascension
    Shard"** (starts ~0.1%, upgradable). Placed far enough in that players must invest broadly
    before prestige even becomes possible — this is the natural gate to the meta layer.

**Extra node ideas (you asked for more):**
- **Double Pull** — % chance a single roll grants **2 heroes** at once.
- **Tier Bump (Crit Roll)** — small % chance a roll's result is **upgraded one rarity tier**.
- **Auto-Advance** — once your DPS can **one-shot** a stage, auto-progress to the next (idle QoL).
- **Stage Skip** — beating a boss instantly fast-clears the next few weak stages.
- **Gold Rush** — bosses & mini-bosses drop **bonus gold** / chance for a gold chest.
- **Offline Rolls** — the dice **auto-rolls while you're away** (capped), so you return to new pulls.
- **Shard Hunter** — upgrades the Ascension Shard drop % and adds a chance for **double shards**.
- **Team Synergy** — bonus DPS when team shares a class/element; unlocks comp-building depth.
- **Boss Grace** — small **extra time** on boss DPS-check timers.
- **Magnet** — auto-collect gold/drops without tapping.
- **Bulk Sell+** — Sell Dupes returns more gold and can auto-trigger at a threshold.

> The tree is the main **gold sink** and the sense-of-progress engine between rolls.

---

## 7. Ascension (Prestige)

- Once the **Ascension Shard drop** skill is unlocked, mobs rarely drop **Ascension Shards**.
- Collect enough Shards → **Ascend**:
  - **Reset (per-run):** only **stage/world progress** and **gold** reset to the start. The *run*
    is what resets, not your account.
  - **Keep (permanent):** your **entire hero collection** (you NEVER lose pulls — that 1/250k Godly
    stays forever), your **team**, and your **upgrade-tree skills**. The whole point is you don't
    re-grind the tree or re-roll your heroes.
  - **Net feel:** because you keep team + skills but the stages reset, you now **one-shot** the
    early stages and blast back up to your old wall ~2× faster, then push past it. Re-clearing
    early content should feel powerful and fast, never tedious.
  - **Reward:** permanent **luck multiplier ×2** per ascension (stacking/curve TBD), and **higher
    rarity tiers** (Secret/Godly) become reachable.
- **Ascension currency / shop (optional later):** spend banked Shards on permanent meta perks
  (extra team slot, starting gold, exclusive ascension-only heroes).
- **Long-term:** consider a **second prestige layer** much later for whales/veterans (e.g.
  "Transcend") — design hook only, not MVP.

---

## 8. Monetization (Ads + IAP)

**Rewarded ads (player-positive, opt-in):**
- 2× / temporary luck surge, instant dice cooldown, double offline earnings, free ×10 roll,
  revive boss timer, double a reward chest.

**Premium currency — Gems (earnable AND buyable):**
- Free players earn **generous Gems from collection-set completion** (see §4), so they learn to use
  Gems and taste premium features. Paying buys Gems faster + unlocks the permanent boosts below.

**IAP / Gem sinks:**
- **Permanent multipliers** (the headline paid power): **×2 Gold (permanent)**, **×2 Luck
  (permanent)**, etc. Stack on top of the free curve to climb noticeably faster.
- **Game speed:** **free players run at 2× speed; paid unlocks 4× speed.** Big QoL, very common
  paid lever in idle games.
- **Extra skills / skill nodes** unlocked or boosted for payers.
- **Remove ads / auto-collect** convenience pack; **team slot** expansion.
- **Cosmetics:** dice skins, hero skins, UI themes.
- **Starter / value bundles**, limited event bundles.

**Principle:** sell **time, speed, permanent multipliers, and cosmetics** — never break the f2p
curve so badly that free players can't enjoy the full loop. The collection→Gems faucet keeps free
players engaged; paid just compounds. Ads always optional and rewarding.

---

## 9. Retention & Live-Ops (post-MVP)

- **Daily login** rewards, **daily quests** (roll X times, beat Y boss).
- **Limited-time events** with **exclusive event heroes** (drive return visits + collection).
- **Achievements** tied to collection %, ascensions, stage depth.
- **Leaderboards** (highest stage / most ascensions).
- Cloud save.

---

## 10. UX / Screens

1. **Main / Battle** — combat view (team vs mobs/boss, stage counter), the **dice** + roll button,
   quick team bar, currency header. The home screen.
2. **Collection / Index** — grid of all heroes by rarity, owned/locked, dupe counts, Sell Dupes,
   collection bonuses, Merge/Upgrade tab.
3. **Upgrade Tree** — the hex honeycomb, pan/zoom, node info + buy.
4. **Ascension** — Shard count, projected luck gain, Ascend button + confirmation.
5. **Shop** — gems, bundles, remove-ads, cosmetics, ad-reward chest.

**Style:** clean, colorful, juicy (squash/stretch, particles, number pop-ups). Portrait, one-thumb.

**Main screen composition (decided):** the **battlefield IS the home screen** — your team of slimes
fights on the left, a **cluster of enemies** you whittle down sits on the right, and the **dice is a
floating ROLL button** (bottom-right) layered over the battle. Team is shown **both** as live slimes
on the field **and** as a 5-slot **portrait bar** along the bottom (level/HP per slot). A **compact
roll-log feed** (bottom-left) toasts recent pulls. Progress path stays pinned at the top, currencies
above it. So: watch combat, tap to roll, drag heroes into the team bar — all on one screen.

---

## 11. Technical Architecture (Unity, URP 2D)

**Data-driven via ScriptableObjects:**
- `RarityTierSO` (name, base odds, color, VFX, unlock condition)
- `HeroSO` (id, name, rarity, baseDPS, class/element, art, collection bonus)
- `UpgradeNodeSO` (id, branch, neighbors[], cost curve, effect, level cap)
- `WorldSO` / `StageSO` (mobs, boss, timers, reward curves)

**Managers (singletons / services):**
- `GameManager` — boot, scene/state flow.
- `SaveManager` — JSON save (local + cloud later); timestamp for offline.
- `RollManager` — dice cooldown, luck calc, pity, rarity resolution → hero grant.
- `CollectionManager` — ownership, dupes, sell, collection bonuses, merge.
- `TeamManager` — active 5, auto-best, synergy.
- `CombatManager` — wave/boss loop, DPS application, respawn, offline catch-up.
- `EconomyManager` — gold, shards, gems, transactions.
- `UpgradeTreeManager` — node graph, unlock adjacency, apply effects to stats.
- `AscensionManager` — shard tracking, reset rules, luck scaling.
- `AdsManager` / `IAPManager` — Unity Ads/LevelPlay + Unity IAP.

**Stat pipeline:** a central `PlayerStats` aggregates base + tree + collection + ascension +
temporary buffs so any system can query final Luck/DPS/GoldMult/etc.

**Save model:** owned heroes (id→count), unlocked/leveled nodes, gold/shards/gems, world+stage,
ascension count, lastSeen timestamp, settings.

---

## 12. MVP Scope (first playable)

**In:** dice + roll w/ luck & rarities (use 5 tiers to start), hero grant + collection grid,
top-5 team, 1 world with ~10 stages + 1 boss, basic combat & gold, a small hex tree (Luck/Damage/
Economy/Utility + the Ascension-drop node), ascension reset+×2 luck, local JSON save, offline
earnings. Placeholder slime art (recolored base).

**Deferred:** merge/upgrade, multiple worlds, events, leaderboards, cosmetics shop, cloud save,
full 8-tier rarity, synergies, second prestige.

### Suggested build milestones
1. **M1 – Roll core:** dice, cooldown, luck math, rarity table, hero SOs, pull reveal, collection list.
2. **M2 – Combat & economy:** team, auto-battle vs stages/boss, gold, offline earnings, save/load.
3. **M3 – Upgrade tree:** hex graph, adjacency unlock, effects wired to PlayerStats.
4. **M4 – Ascension:** shard drop node, shard collection, ascend reset + luck scaling.
5. **M5 – Polish & monetize:** juice/VFX, ads + IAP hooks, balance pass, MVP build to device.

---

## 13. Open Questions / To Decide Later

- Final art direction: slimes vs characters (recommendation: slimes).
- Exact luck-scaling curve per ascension (flat ×2 each, or diminishing?).
- Luck Roll Streak: do milestone bonuses truly multiply to ×100, or cap/use-highest?
- Gold/DPS/cost balance curves (needs a spreadsheet pass).
- Combat respawn during a run (team wipe handling) — separate from ascension reset, still TBD.
- Merge/upgrade recipe economy.
- Whether ×10 rolls cost gold, gems, or are ad-gated.
- Gem economy tuning: how much Gems each collection set awards vs IAP pricing.

---

*Next step after sign-off: stand up M1 in the Unity scene (dice + roll + rarity reveal) using
Gerty, starting from the current empty URP 2D scene.*
