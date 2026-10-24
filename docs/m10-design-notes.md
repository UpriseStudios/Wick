# M10 design notes: bigger vaults, defence upgrades, gem uses, trophy decorations

Ideas from device testing on 21 October 2026, to fold into M10 (Full content) and M12 (Monetisation).
Nothing here is built yet. Adjust anything before M10 starts.

The fairness rule from M8 still holds: money buys looks, time and convenience, never raid power.
Every item below keeps to it.

## 1. Vaults grow with Vault Level

Today every vault has the same grid. The grid and build budget should grow with Vault Level, so
higher levels give room for more creative layouts, Clash of Clans style.

Decided on 24 October 2026: Level 1 is 12 x 16. Today's 9 x 13 is too small, and raids can be
finished in under 10 seconds.

### Vault Level table (draft, 24 October 2026)

First-pass numbers for review. The balance simulator tunes build points and costs; the grid
sizes, unlocks and caps are the shape to agree now.

| Vault Level | Grid (tiles) | Build points | Rooms (guide) | Unlocks | Defence level cap | Braziers | Treasure piles |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | 12 x 16 | 80 | 2 to 3 | Drip Pipe, Bellows, Spike Floor, Crate | 1 | 1 | 3 |
| 2 | 12 x 16 | 110 | 2 to 3 | Snuffer Bell, Night Owl | 1 | 1 | 4 |
| 3 | 14 x 18 | 150 | 3 to 4 | Wax Moths, Ice Tiles | 2 | 2 | 4 |
| 4 | 14 x 18 | 180 | 3 to 4 | Swing Axe | 2 | 2 | 5 |
| 5 | 16 x 22 | 230 | 4 to 5 | Mimic Chest | 3 | 2 | 5 |
| 6 | 16 x 22 | 270 | 4 to 5 | Sleepy Troll | 3 | 3 | 6 |
| 7 | 18 x 24 | 330 | 5 to 6 | Fan Maze | 4 | 3 | 6 |
| 8 | 18 x 24 | 380 | 5 to 6 | Lever Doors | 4 | 3 | 7 |
| 9 | 18 x 24 | 430 | 5 to 6 | (none: room to use the full kit) | 5 | 3 | 7 |
| 10 | 20 x 28 | 520 | 6 to 8 | Sir Whiskers | 5 | 4 | 8 |

- **Grid** grows every other level, as in the original sketch. Level 9 shares Level 7's grid
  so Level 10 is a real jump.
- **Build points** grow faster than the grid (about 0.4 points per tile at Level 1, 0.9 at
  Level 10), so higher vaults are denser as well as bigger. Today's budget is 60 to 330.
- **Rooms** stay a guide for the editor's templates, not a rule.
- **Raising Vault Level** keeps M8's gold, time and star gate unchanged (400 gold and 5 stars
  for Level 2, up to 50,000 gold and 365 stars for Level 10). The Lantern table is unchanged.
- **Treasure value** stays 100 x Vault Level per pile, and the loot cap per level is unchanged.

### Build point costs

| Defence | Cost | Notes |
| --- | --- | --- |
| Crate | 4 | Unchanged |
| Drip Pipe | 10 | Unchanged |
| Spike Floor | 12 | Unchanged |
| Bellows | 15 | Unchanged |
| Snuffer Bell | 20 | Unchanged |
| Wax Moths | 25 | Unchanged |
| Night Owl | 30 | Unchanged |
| Ice Tiles | 3 per tile | Placed as a patch, so priced per tile |
| Swing Axe | 22 | |
| Lever Doors | 20 per door and lever pair | |
| Mimic Chest | 28 | |
| Fan Maze | 30 | |
| Sleepy Troll | 35 | |
| Sir Whiskers | 60 | One per vault |

### Defence upgrade costs (per level, same for every defence)

| To level | Gold | Wax | Build time |
| --- | --- | --- | --- |
| 2 | 300 | 10 | 20 min |
| 3 | 1,200 | 40 | 2 h |
| 4 | 4,000 | 120 | 6 h |
| 5 | 10,000 | 300 | 12 h |

- Every defence except Sir Whiskers gets 5 levels. He has one level in M10, because he grows
  as his own feature later.
- The cap is half the Vault Level, rounded up, so a defence unlocked late can catch up.
- Upgrading all 12 upgradable defences to Level 5 costs about 186,000 gold and 5,600 wax,
  a long tail beyond Vault Level 10 (145,000 gold in total).

### Still open

- The raid clock (60 s now) is decided after the fairness test with the balance simulator.
  A perfect run of a full Level 10 vault must stay possible.
- What each level improves for the six new traps (the six originals are in section 2).

- This is a sim rule change: it bumps SimVersion and re-records the golden replays deliberately.
- The camera, the scout flyover and Home's framing already adapt to vault size.

## 2. Defence upgrades

Each trap and guardian type gets levels, bought with gold and wax in the Upgrades menu (one
workshop each, like Vault Level), and capped by Vault Level.

| Defence | What a level improves (examples) |
| --- | --- |
| Drip Pipe | Shorter gap between drips |
| Bellows | Longer gust reach (5, 6, then 7 tiles) |
| Spike Floor | Shorter warning |
| Snuffer Bell | Wider trigger plate |
| Night Owl | Wider sight cone, quicker to notice |
| Wax Moths | Faster drift |

**Upgrade shape (decided 24 October 2026):** upgrades make a defence faster and stronger, and
each one must feel worth buying.

- Levels 2 to 4: a small step each, such as a shorter cycle or quicker reaction.
- Level 5: a bigger step plus a signature perk, a new effect the defence didn't have before.
- Example: Bellows fire every 1.5 s at level 1. Levels 2 to 4 each take 2 ticks (about 0.07 s)
  off the cycle. Level 5 takes off 3 ticks (0.1 s) more and adds a 1-tile knockback, which
  reuses Sir Whiskers' knockback rules.
- Timings are whole ticks (30 a second), so steps are multiples of 1/30 s. A step of 0.05 s
  can't exist in the sim.
- Each defence gets a level 2 to 5 table and a level 5 perk in the M10 plan; numbers are then
  tuned from balance simulator results, not by feel.

- The balance simulator must keep every upgraded vault beatable: a perfect line always exists.
- Upgrade levels are stored with the published vault, so a replay always uses the levels it was raided at.
- This is a sim rule change (SimVersion bump, new goldens).
- Gems can finish an upgrade early, as today, but never buy a level.

## 3. Gems become worth having

Gems are the premium currency. Today they mostly speed things up. M12 gives them things to buy:

- **Vault themes** (Crypt, Sunken Ship, Dragon Hoard, Frozen Crypt, Haunted Bakery): gems, or
  seasonal events.
- **Decorations** for your vault: banners, torches, gold piles, statues. Cosmetic props that
  raiders see but that never affect the raid.
- **Skins**: some sold for gems, some still cash-only, some as Heist Pass rewards.
- **Convenience**, as today: finishing an upgrade early and an extra workshop.

Gem prices sit beside the existing cash shop, and the economy config (Remote Config) holds them
so they can be tuned without an app update.

## 4. Trophies become decorations

Every achievement tier unlocks a trophy prop: a gold candle statue, a crown on a cushion, a
snuffed-out bell. You place it in your treasure room in the vault editor. Raiders see it when
they reach the hoard, so trophies show off what you've done.

- Decorations live in the vault data as cosmetic objects. The sim skips them: they're not walls,
  they don't block, and they're never hashed. Adding them doesn't change any raid.
- They follow the same art pipeline as other props (`prop_trophy_*`) and import checks.

## Order

1. M9 finishes: UI polish (buttons, the results screen fitting, the Heist Pass ladder, the editor in the new style). Done.
2. M10: bigger vaults, the six new traps and Sir Whiskers, and defence upgrades (with the balance
   simulator), then decorations and trophies. Traps are built against stand-ins; phase 2 art
   replaces them, as it did for Wick.
3. The release-signed AAB for internal testing, the Play Console and the CDN, once M10's assets are in.
4. M12: the gem shop for themes, decorations and skins.
