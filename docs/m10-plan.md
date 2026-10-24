# M10 plan: Full content

Status: agreed 24 October 2026. Design inputs: [m10-design-notes.md](m10-design-notes.md)
(Vault Level table, build costs, upgrade costs and shape) and [game-spec.md](game-spec.md)
(trap table, Sir Whiskers).

## Goal

Vaults grow from 12 x 16 to 20 x 28 with Vault Level. The six remaining traps and Sir Whiskers
are playable. Every defence can be upgraded to level 5. Decorations and trophies can be placed.
A balance simulator shows that every vault the rules allow stays beatable, and its results set
the numbers and the raid timer.

All new defences use stand-in models. Phase 2 art replaces them later, as it did for Wick.

**Out of scope:** the gem shop (M12), the CDN, the release AAB and the Play Console (all after
M10), and Sir Whiskers' own upgrade path (he stays at level 1 in M10).

## Rules that apply throughout

- **One SimVersion bump for the milestone (3 to 4)**, made in the first sim commit. Every sim
  commit after that updates the goldens deliberately, with expected outcomes worked out by hand
  as today. Cloud Code is deployed to development only once v4 is complete enough to play.
- **Fairness:** money buys time, never levels. Gems can finish a defence upgrade early, but the
  gold, the wax and the Vault Level cap still apply.
- **Tells never shrink below a readable floor.** Upgrades make a defence faster and stronger,
  but a warning (the Spike Floor rumble, the Mimic's breathing, Sir Whiskers' wiggle) always
  lasts at least 10 ticks (a third of a second).
- **Timings are whole ticks** (30 a second).

## Build order

Each step is one or more small commits with tests.

### 1. Balance simulator, on today's rules

A .NET tool in `tools/balance`, built first so every later step is measured, not judged by feel.

- Move the solver out of `tools/seedgen` into a small shared project that both tools reference.
- For each Vault Level, generate a few hundred vaults filled to the full build budget with the
  defences that level unlocks, at the highest defence level that Vault Level allows.
- Run the solver on each and report: how many it can escape with 1 star and with 3 stars, and
  the fastest, median and slowest escape.
- Flag any level where some vaults are unbeatable, or where the fastest escape is under 15 s
  (today's 9 x 13 vaults can be cleared in under 10 s, which is what M10 fixes).
- Writes a Markdown report to `tools/balance/report.md` so results can be compared run to run.
- Runs in CI with a small sample, and fails if a beatability check fails.

The solver will grow as each new defence lands: it needs to wait out timers, tiptoe past the
Troll, stand still for Sir Whiskers, pull levers and plan around ice and knockback.

### 2. Vault sizes (first sim change: SimVersion 4)

- `VaultRules` gets the table: grid, build points, braziers and treasure piles per Vault Level,
  plus the build costs for the new defences.
- The validator accepts the grid sizes up to the vault's level (see decision B).
- The editor works on the larger grids; room templates exist for each size.
- Expanding a vault in the editor, with the builder's bellows blast (decision B).
- Seed vaults, dev vaults and templates are regenerated for 12 x 16 and up.
- Goldens move to the new grid where they need to.
- Development Cloud Save is wiped of 9 x 13 vaults and v3 replays (see decision A).

### 3. Defence levels

- The vault JSON gains `defenceLevels` (a level per defence type). The sim reads it, so a replay
  always uses the levels it was raided at.
- The client never sets levels. Cloud Code stamps them on publish from the player's upgrades.
- Upgrades menu: one entry per unlocked defence, using the existing workshops, costs and
  speed-ups. Costs and caps come from `make_economy.py`, so Remote Config can tune them.
- Levels apply to the six current defences in this step, using the tables below.
- When an upgrade finishes, the live vault is checked again (see decision C).

### 4. Lantern in raids

M8 sells Lantern levels, but they don't change raids yet. The raid ticket carries the raider's
Lantern level, the sim adds its light bonus, and the raid record stores it so replays match.

### 5. Knockback

Knockback is shared by Bellows level 5 and Sir Whiskers, so it lands once, with its own tests:

- Wick is pushed N tiles and stops on the last free tile before a wall, crate or object.
- A knockback is followed by the usual invulnerability window.
- A knockback never sets off an instant-snuff trap such as the Snuffer Bell.

### 6 to 11. The six new traps, one at a time

Ice Tiles, Swing Axe, Mimic Chest, Sleepy Troll, Fan Maze, Lever Doors. Each comes with:

- sim rules and tests
- a golden replay
- a stand-in view
- an editor tray entry and validator rules
- its level 2 to 5 table and level 5 perk
- solver support and a balance simulator run

Lever Doors also needs a link from each lever to its door in the vault data.

### 12. Sir Whiskers

He is built to the rules in his game-spec section: wake within 3 tiles of a lit flame, stalk
while Wick moves, give up after 1.5 s of stillness, a 1-second wiggle tell before every pounce,
a swat that costs 1 flicker with a 2-tile knockback, always chase a decoy, and one per vault.

He gets a stand-in model that uses the art pipeline's Sir Whiskers clip names, so phase 2 art
drops straight in.

### 13. Fairness test and tuning

Run the balance simulator on the full rule set and tune build points, costs and defence levels
from the results. **The raid timer is decided here, with you, from the report.**

### 14. Decorations and trophies

- The vault data gains a list of decorations: cosmetic props with a position and rotation.
  The sim never reads them. They don't block anything, and they aren't hashed.
- The editor tray gets a decorations tab. Every achievement tier unlocks a trophy prop
  (`prop_trophy_*`). Trophies are placed in the hoard room.
- Raiders see decorations, and see trophies when they reach the hoard.
- Stand-in props until phase 2 art.

### 15. Wrap-up

Regenerate seed vaults for every Vault Level. Deploy Cloud Code and Remote Config to
development. Update the memory notes and docs, then run the device check.

## Defence levels

Level 1 is today's behaviour (or the new trap's base rules). Levels 2 to 4 are one small step
each. Level 5 is a bigger step plus a perk. Authored cycles stay as the builder set them; a
level takes ticks off that cycle, never below the type's minimum. First-pass numbers, tuned in
step 13.

| Defence | Level 1 (base) | Levels 2 to 4, each | Level 5 | Level 5 perk |
| --- | --- | --- | --- | --- |
| Drip Pipe | Cycle as built (default 45 ticks, 1.5 s) | -2 ticks | -3 ticks | Puddle: the hit tile slows Wick to tiptoe speed for 1 s |
| Bellows | Cycle as built (default 75 ticks), gust reaches 6 tiles | -2 ticks | -3 ticks | 1-tile knockback |
| Spike Floor | Cycle as built (default 60 ticks), 15-tick rumble | -2 ticks | -3 ticks | Rumble shortens to 10 ticks (the floor) |
| Snuffer Bell | Drops once when Wick steps on its plate, then is spent | Level 2 re-arms 10 s after dropping; levels 3 and 4 re-arm 2 s sooner each | Re-arms after 4 s | Plate grows to 2 tiles |
| Night Owl | Chase speed 100, sight 5 tiles | +5 chase speed | +10 | Sight range 6 tiles |
| Wax Moths | Speed 40, drain every 60 ticks | +3 speed | +5 | Drain every 45 ticks |
| Ice Tiles | Wick slides until he hits something | +10% slide speed | +15% | Sliding dims the flame by half a tile |
| Swing Axe | Swings on its cycle | -2 ticks | -3 ticks | The arc covers one more tile |
| Mimic Chest | Bites for 2 flickers when Wick is adjacent | Lunges 2 ticks sooner | 3 ticks sooner | A bite steals one carried treasure pile |
| Sleepy Troll | Wakes in bright light and stomps | Wakes at a slightly dimmer light | Dimmer again | The stomp also hits the 4 tiles around him |
| Fan Maze | Turns on its cycle, pushes 1 tile | -2 ticks | -3 ticks | Pushes 2 tiles |
| Lever Doors | A door stays open for its timer after its lever is pulled | -0.5 s open time | -1 s | One lever can work two doors |
| Sir Whiskers | His spec section | none in M10 | none | none |

Upgrades are vault-wide: upgrading Drip Pipe upgrades every drip pipe in your vault. Today the
Snuffer Bell is one-shot, so its upgrades add re-arming (a new rule, with a visible lift-back-up
animation as its tell).

## Decisions (24 October 2026)

**A. Development data is wiped at the SimVersion bump.** Every published vault in development
is 9 x 13, and v3 replays can't play back on the v4 sim. Development vaults and replays are
wiped and reseeded with the new seed vaults. There are no real players yet.

**B. Growing the grid.** Your live vault keeps its size and stays valid. The editor offers to
expand it: the layout keeps its place, anchored at the entrance edge, and the new space is
added as wall. A little builder character walks in with a hand-held bellows and blasts a
doorway through the old outer wall into the new space; you dig out the rest as usual. You
republish when ready, with the usual clear-check.

- The builder is presentation only (client side, never in the sim), with a stand-in model
  until phase 2 art. He needs an entry in asset-production.md (`char_builder`, with clips such
  as `walk_in`, `blast`, `cheer`, `walk_out`).
- He can skip with a tap, and plays once per expansion.

**C. Live vault after a defence upgrade.** When an upgrade finishes, Cloud Code re-runs your
stored clear-check with the new levels. If you still escape with 1 star, the live vault takes
the new level straight away. If not, it keeps the old level and the game asks you to re-test.

## Checks by hand at the end

On a phone, against development:

- Raise a test account through each grid size and check the editor, camera, scout flyover and
  Home framing at 12 x 16 and 20 x 28.
- Expand a vault after a level-up: the builder blasts a doorway, the old layout keeps its
  place, the old vault stays live until you republish, and a tap skips the builder.
- Place, test and publish a vault with each new defence; raid it from a second account.
- Upgrade a defence and check the live vault picks up the level (decision C) and that the
  change is visible in a raid.
- Get swatted by Sir Whiskers into a Snuffer Bell plate and check the bell doesn't drop.
- Place a decoration and a trophy, then raid that vault and check both show and change nothing.
- Read the balance report together and decide the raid timer.
