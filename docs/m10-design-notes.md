# M10 design notes: bigger vaults, defence upgrades, gem uses, trophy decorations

Ideas from device testing on 21 October 2026, to fold into M10 (Full content) and M12 (Monetisation).
Nothing here is built yet. Adjust anything before M10 starts.

The fairness rule from M8 still holds: money buys looks, time and convenience, never raid power.
Every item below keeps to it.

## 1. Vaults grow with Vault Level

Today every vault has the same grid. The grid and build budget should grow with Vault Level, so
higher levels give room for more creative layouts, Clash of Clans style.

| Vault Level | Grid (tiles) | Build points | Rooms (guide) |
| --- | --- | --- | --- |
| 1 | 12 x 16 | today's budget | 2 to 3 |
| 3 | 14 x 18 | +25% | 3 to 4 |
| 5 | 16 x 22 | +50% | 4 to 5 |
| 7 | 18 x 24 | +75% | 5 to 6 |
| 10 | 20 x 28 | +100% | 6 to 8 |

- The raid clock (60 s) may need to scale with size. The balance simulator decides, so a perfect
  run of a full Level 10 vault stays possible.
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

1. M9 finishes: UI polish (buttons, the results screen fitting, the Heist Pass ladder, the editor in the new style).
2. The release-signed AAB for internal testing.
3. M10: bigger vaults and defence upgrades (with the balance simulator), then decorations and trophies.
4. M12: the gem shop for themes, decorations and skins.
