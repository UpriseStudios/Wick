# M10 plan: Full content

Status: agreed 24 October 2026, then merged with the M10 milestone prompt the same day. Where the
prompt and the decisions in this plan disagree, the decisions win (see "Prompt conflicts").

Design inputs: [m10-design-notes.md](m10-design-notes.md) (Vault Level table, build costs, upgrade
costs and shape) and [game-spec.md](game-spec.md) (trap table, Sir Whiskers, progression).

## Goal

Vaults grow from 12 x 16 to 20 x 28 with Vault Level. The six remaining traps and Sir Whiskers
are playable. Every defence can be upgraded to level 5, and Wick can be upgraded with wax.
Decorations and trophies can be placed. A balance simulator shows that every vault the rules
allow stays beatable, and that a spender levels at most about twice as fast as a daily free
player. Its results set the numbers and the raid timer.

All new defences and the builder use stand-in models. Phase 2 art replaces them later, as it
did for Wick.

**Out of scope:** the gem shop (M12), the CDN, the release AAB and the Play Console (all after
M10), Sir Whiskers' own upgrade path (he stays at level 1 in M10), and designing the 40 seed
vaults (you design them; M10 builds the tooling).

## Prompt conflicts and how they were settled

| The prompt says | The plan does | Why |
| --- | --- | --- |
| Vault sizes from 9 x 13 | From 12 x 16 | Decided: 9 x 13 raids can be cleared in under 10 s |
| Vault Level 1 to 12 | Vault Level 1 to 10 | The agreed table; Sir Whiskers unlocks at the top level, 10 |
| Larger vaults get a longer timer if Remote Config says so | The timer comes from Remote Config per Vault Level, 60 s everywhere until the fairness test | The timer value is decided after the fairness test |
| The server keeps the previous sim build for in-flight raids | Raid tokens record the sim version; a raid started on v3 and submitted to v4 is voided and its raid-limit slot given back | Keeping a second sim build in Cloud Code would fork sim code (CLAUDE.md). Development data is wiped anyway |
| Balance simulator: a heuristic bot | A heuristic bot **and** a solver | The bot gives snuff rates and loot; the solver proves a vault is beatable |
| Fairness test from M8 stays green | Two of its three tests are unchanged. The "whale raid plays the same" test compares a whale with a free player at the **same progression** | The Lantern now changes raids, so a newcomer and a Lantern 5 whale differ by design |

Added by decisions here, not in the prompt: defence upgrades, decorations and trophies, the
builder, and re-checking a live vault when an upgrade finishes.

## Rules that apply throughout

- **One SimVersion bump for the milestone (3 to 4)**, made in the first sim commit. Every sim
  commit after that updates the goldens deliberately, with expected outcomes worked out by hand
  as today. Cloud Code is deployed to development only once v4 is complete enough to play.
- **Both runtimes:** sim tests live in `Sim.Tests`, which Unity runs and
  `server/Wick.Sim.Server.Tests` links, so every new entity is tested in both.
- **Fairness:** money buys time, never levels. Gems can finish an upgrade early, but the gold,
  the wax and the Vault Level cap still apply.
- **Tells never shrink below a readable floor.** Upgrades make a defence faster and stronger,
  but a warning (the Spike Floor rumble, the Mimic's breathing, Sir Whiskers' wiggle) always
  lasts at least 10 ticks (a third of a second).
- **Timings are whole ticks** (30 a second).

## Part A: sim content

### A1. Shared bot project

Move the seedgen solver into `tools/Wick.Bots`, a small project that links the sim source, so
seedgen, the balance simulator and tests share it. Each new defence in A6 to A12 teaches the
solver how to get past it.

### A2. Vault sizes and the raid timer (SimVersion 4)

- `VaultRules` gets the table: grid, build points, braziers and treasure piles per Vault Level,
  plus build costs for the new defences.
- The validator accepts the table's grid sizes up to the vault's level (decision B).
- The raid length comes from the raid token (Remote Config per Vault Level, 60 s for now) and is
  stored with the raid so replays match.
- Raid tokens record the sim version; mismatches are voided and refunded.
- The editor works on the larger grids; room templates exist for each size.
- Expanding a vault in the editor, with the builder's bellows blast (decision B).
- Seed vaults, dev vaults and goldens move to the new grid where they need to.
- Development Cloud Save is wiped of 9 x 13 vaults and v3 replays (decision A).

### A3. Knockback

Knockback is shared by Bellows level 5 and Sir Whiskers, so it lands once, with its own tests:

- Wick is pushed N tiles and stops on the last free tile before a wall, crate or object.
- A knockback is followed by the usual invulnerability window.
- A knockback never sets off an instant-snuff trap such as the Snuffer Bell.

### A4. Defence levels

- The vault JSON gains `defenceLevels` (a level per defence type). The sim reads it, so a replay
  always uses the levels it was raided at.
- The client never sets levels. Cloud Code stamps them on publish from the player's upgrades.
- Upgrades menu: one entry per unlocked defence, using the existing workshops, costs and
  speed-ups. Costs and caps come from `make_economy.py`, so Remote Config can tune them.
- Levels apply to the six current defences in this step, using the tables below.
- When an upgrade finishes, the live vault is checked again (decision C).

### A5. Decorations and trophies

- The vault data gains a list of decorations: cosmetic props with a position and rotation.
  The sim never reads them. They don't block anything, and they aren't hashed.
- The editor tray gets a decorations tab. Every achievement tier unlocks a trophy prop
  (`prop_trophy_*`). Trophies are placed in the hoard room.
- Raiders see decorations, and see trophies when they reach the hoard.

### A6 to A11. The six new traps, one at a time

Ice Tiles, Swing Axe, Mimic Chest, Sleepy Troll, Fan Maze, Lever Doors. Each comes with:

- sim rules and tests
- a golden replay
- `VaultRules` cost and unlock level, and validator rules
- a stand-in view
- an editor tray entry
- its level 2 to 5 table and level 5 perk
- solver and bot support

Lever Doors also need a link from each lever to its door in the vault data, and an editor mode
to link them: tap a lever, then tap the door it opens.

### A12. Sir Whiskers

He is built to his game-spec section:

- movement-triggered stalking: he wakes when a lit flame comes within 3 tiles, then follows
  while Wick moves
- a freeze-to-evade timer: 1.5 s of stillness makes him lose interest
- a 30-tick pounce tell (the wiggle)
- a swat costing 1 flicker, with a deterministic 2-tile knockback (A3)
- invulnerability after a swat, and knockback never setting off instant-snuff traps
- decoy priority
- one per vault, enforced in `VaultValidator`

Goldens: a knockback into a wall, a knockback into another trap, and a freeze escape. His
stand-in uses the art pipeline's clip names, so phase 2 art drops straight in.

## Part B: progression and balance

### B1. Vault Level table and Wick upgrades

- The Vault Level 1 to 10 table (gold, star gate, build budget, unlocks) is already agreed in
  the design notes; it moves into `make_economy.py` and `VaultRules`.
- Wick upgrades are bought with wax and capped by Vault Level, so the cap is the same for
  everyone at a level:
  - **Lantern** (light radius, already sold since M8) is wired into raids: the raid token
    carries the raider's Lantern level, and the raid record stores it so replays match.
  - **Extra decoy** is new: one decoy per raid today, a second unlocked at a higher Vault Level.
- The M8 fairness test is updated as described in "Prompt conflicts", and gains a test that no
  purchase or grant can raise a Lantern, decoy or defence level.

### B2. Balance simulator

A .NET tool in `tools/balance`:

- **Vault generation:** for each Vault Level, thousands of vaults filled to the full build budget
  with the defences that level unlocks, at the highest defence level it allows.
- **Solver:** proves each vault can be escaped with at least 1 star.
- **Heuristic bot:** raids each vault with simple rules (head for the hoard, wait out timed
  traps, tiptoe near guardians, throw the decoy at the first guardian). Reports snuff rate,
  average loot, and the fastest, median and slowest escape.
- **Economy model:** time to reach each Vault Level for a daily free player and for a spender,
  from the economy config (raids per day, loot, star gates, build times, speed-ups).
- **Report:** a table per Vault Level in `tools/balance/report.md`. It flags any level where:
  - some vaults are unbeatable
  - the fastest escape is under 15 s
  - the spender is more than about 2x faster than the free player
  - time-to-level jumps more than 50% over the previous level
- Runs in CI with a small sample, and fails if a beatability check fails.

### B3. Fairness test and tuning

Run the balance simulator on the full rule set and tune build points, costs and defence levels
from the results. **The raid timer is decided here, with you, from the report.**

### B4. Mean vault protection

- Each vault tracks its snuff rate over its recent raids.
- Matchmaking weighs against vaults above a threshold (first pass: more than 85% of the last 20
  raids snuffed), with the threshold in Remote Config.
- Raiders get a free skip: skipping a served vault before starting costs nothing and doesn't
  count against the raid limit.

### B5. Designer seed vault tooling

You design 40 seed vaults across the Vault Levels. M10 adds:

- a validate command that runs the validator and the solver on each file and records a
  clear-check
- an upload command for the development and production directories

The generated seed vaults stay as the fallback until yours are in.

### B6. Wrap-up

Deploy Cloud Code and Remote Config to development, update the memory notes and docs, then run
the device check.

## Defence levels

Level 1 is today's behaviour (or the new trap's base rules). Levels 2 to 4 are one small step
each. Level 5 is a bigger step plus a perk. Authored cycles stay as the builder set them; a
level takes ticks off that cycle, never below the type's minimum. First-pass numbers, tuned in
B3.

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

## Acceptance

- Every entity is covered by tests and golden replays in both runtimes (Unity and .NET).
- The balance report has a table per Vault Level and flags any level where time-to-level jumps
  more than 50% over the previous one.
- The M8 fairness tests are green (with the same-progression change above).
- All tests pass in Unity and .NET. Golden hashes change only in the deliberate v4 re-records.

## Checks by hand at the end

On a phone, against development:

- Raise a test account through each grid size and check the editor, camera, scout flyover and
  Home framing at 12 x 16 and 20 x 28.
- Expand a vault after a level-up: the builder blasts a doorway, the old layout keeps its
  place, the old vault stays live until you republish, and a tap skips the builder.
- Place, test and publish a vault with each new defence, including linking a lever to a door;
  raid it from a second account.
- Upgrade a defence and check the live vault picks up the level (decision C) and that the
  change is visible in a raid.
- Buy a Lantern level and the extra decoy, and check both show in a raid.
- Get swatted by Sir Whiskers into a wall, into a Snuffer Bell plate (the bell must not drop),
  and stand still to make him give up.
- Place a decoration and a trophy, then raid that vault and check both show and change nothing.
- Skip a served vault and check it costs nothing.
- Read the balance report together and decide the raid timer.
