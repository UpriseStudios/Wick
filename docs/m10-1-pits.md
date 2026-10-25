# Wax Bandits — M10.1 Pits Spec & Prompt Pack

Oct 24, 2026 · @Jamie

## Overview

M10.1 turns pits from an empty tile type into a real hazard: falling in costs a flicker and sends Wick back to the entrance, and at max level he also drops his loot. It's a nasty setback, never a raid wipe, and the editor guarantees a pit can never make a vault impassable.

It runs straight after M10 and before M11, using the same session rules: plan mode first, review prompt after. It's split into three prompts that each end green:

1. **M10.1a Simulation:** pit falls, respawn, max-level loot drop, events and golden replays.
2. **M10.1b Validator and editor:** placement rules and the pit-free route check.
3. **M10.1c Presentation, server and analytics:** animation, VFX, sound, server re-sim and tuning.

This replaces the short pit note added to the M2 prompt after M2 was already built; treat this pack as the source of truth for pits.

## Decisions (25 October 2026)

Agreed with Jamie before M10.1a. Where they differ from the rest of this pack, these win.

- **Pit levels:** the standard 5 defence levels (capped by Vault Level like every defence), not 3. Levels 2 to 4 each lengthen the fall a little (more time lost); level 5 adds the loot drop.
- **Exit lock:** kept at 8 seconds (`pitRespawnExitLockTicks` = 240).
- **Knockback into pits:** built as a switch and tested both ways in the balance simulator. If "on" keeps raids fair it becomes a deliberate defence tactic; otherwise it stays off. Fan pushes follow the same switch.
- **Raids in flight at the SimVersion bump:** voided with the raid slot returned, as in M10. The server never keeps a second sim build (CLAUDE.md: never fork sim code).
- **Route length:** VaultRules.MinRouteTiles measures the shortest pit-free walk, so pits can serve as maze walls.
- **Doorways:** door tiles and the entrance (and the tiles in front of and behind them). Plain gaps in walls are not doorways.
- **Pit edge visibility and the pit intro card:** to be picked up in the updated game spec and master asset list (lighting rules); the tutorial card waits for the tutorial milestone.
- **Analytics:** the `pit_fall` event and the `raid_end` fields are sent now; checking them in the dashboard waits for the analytics pass before test publishing.
- **Edge cases added:** guardians (owl, moths, troll, Sir Whiskers) path around pits and owl patrols may not cross one; the solver, balance bot and seed generator avoid pits, and the balance generator places them; pits cost 5 build points a tile to start, tuned from the balance run.

**Order (25 October 2026):** M10.2 lighting lock runs before M10.1c, so the pit visuals are made against the locked lighting. The pit edge glow is built in M10.2a with the other always-readable emissives; M10.1c does the rest of the pit presentation.

## M10.1b as built (25 October 2026)

How the agreed rules were read in code (change any you disagree with):

- **Doorways:** a pit may not be on the entrance or a door tile, or on any tile next to one. A door sits in a wall, so its other neighbours are wall and this is exactly "in front of and behind". Gaps in walls are not doorways, but a pit in a one-tile gap still fails the route rule.
- **Route rule:** the pit-free flood fill treats pits like walls; gates open only if their lever is reachable without crossing a pit. A pile or hoard room reachable only across a pit is `PitBlocksRoute` (with the pile's id and the first pit on the walk, or the pile's tile if the pit isn't on the walk itself, such as when it cuts off a lever). Cut off by walls alone is still `TreasureUnreachable` / `HoardUnreachable`.
- **New rule `ObjectOnPit`:** nothing (trap, guardian, pile, lever) may stand on a pit. An owl patrol point may not be a pit, and patrols must be walkable round pits.
- **Editor:** the Pit tool (Rooms tab) refuses a tile that breaks a rule. The tile goes red and a toast gives the reason. While the tool is selected, a green line shows the pit-free route to the hoard. Pit level isn't chosen per tile: every pit is at the owner's Pit upgrade level, shown on the tool ("Pit Lv N"). There is no icon yet; tools are text buttons until the editor art pass.
- **Bots:** the balance bot and seed solver path round pits and won't step onto ice that slides into one. BalanceGen digs pit lines of 2 to 4 tiles outside the hoard, mostly on or beside the route, keeping the route at or above MinRouteTiles.
- **Migration check:** `dotnet run --project tools/vault-audit` (bundled templates, seeds, designer and dev vaults, this PC's editor drafts: all 26 pass). Add `-- --live development` with a service account key in `WICK_UGS_KEY_ID` / `WICK_UGS_SECRET` to check every live vault. It only reports.

Balance run with pits in the generator (200 vaults a level, knockback into pits off): escapes fell at VL3 (89% to 81%), VL4 (81% to 76%), VL5 (71% to 62%), VL7 (64% to 58%) and VL9 (26% to 20%); free-player time to VL10 is 74.5 days. With knockback into pits on, only VL8 moved (51% to 50%), so the switch barely matters in worst-case vaults. Not yet acted on: pit cost and the switch are for M10.1c tuning.

## Pit rules

| Rule | Detail |
| --- | --- |
| What a pit is | A tile type builders paint into the vault, available from Vault Level 1, upgradable like a trap |
| Fall | Moving onto a pit tile makes Wick fall: he loses 1 flicker, plays a tumble and pops back out at the vault entrance |
| Timer | Keeps running through the fall and respawn |
| Loot, below max level | Wick keeps everything he's carrying |
| Loot, at max level | Wick drops everything he's carrying; each item returns to the treasure pile it came from and can be grabbed again |
| Last flicker | If the fall costs his last flicker, he's snuffed and the raid ends as normal |
| After respawn | A short invulnerability window, the same length as after a hit |
| Visibility | Pit edges glow faintly so pits read in the dark, even outside Wick's light |
| Placement: doorways | Never on a door, the entrance, or the tiles directly in front of and behind a doorway |
| Placement: routes | Refused if it would leave no pit-free route from the entrance to every treasure pile and back |
| Clear-check | Pits are covered by the publish clear-check like every other hazard |
| Levels | Levels are set in VaultRules; only the max level adds the loot drop. Proposed: 3 levels, with levels 1 and 2 differing only in cost and look |

**Design intent**: a pit is a nasty setback that costs a flicker, time and sometimes the loot run, but a skilled raider can always recover.

## Edge cases and decisions

Two questions need your call before M10.1a; both are built as tunable rules, so either answer works without rework.

**Decisions needed**

- [x] **The "pit dive" shortcut** (decided: keep the 8 second lock). Below max level Wick keeps his loot and respawns at the entrance, which is also the escape point. A raider could grab the hoard, jump in a pit on purpose, and escape a moment later for the price of 1 flicker. Recommended fix: after a pit respawn the exit stays shut for 8 seconds (a portcullis drops with a clang), so a dive saves no time. Built as `pitRespawnExitLockTicks` (default 240 ticks = 8 seconds); set it to 0 to allow diving as a deliberate tactic.

**Proposed defaults for other edge cases (change any you disagree with)**

| Situation | Behaviour |
| --- | --- |
| Running or holding a run into a pit | Wick falls on entering the tile; the run stops |
| Sliding on ice into a pit | Wick falls: ice is a planning challenge, so the slide counts |
| Falling while invulnerable | Still falls and respawns, but loses no flicker |
| Tiptoeing over a pit | Still falls; tiptoe only affects light and noise |
| Guardians and moths | Treat pits as blocked tiles and path around them |
| Decoy | Placed on Wick's tile, so it can never be dropped into a pit |
| Mimic Chest or treasure pile next to a pit | Allowed, as long as the pit-free route rule holds |
| Dropped loot at max level | Returns instantly to its original pile; the pile shows its restored amount |
| Several piles' worth of loot | Each item returns to the pile it came from |
| Pit on the tile after the entrance | Refused (the tile in front of the entrance counts as a doorway) |
| Fall during the last second of the timer | Timer runs out as normal; any loot carried at that moment pays nothing because Wick hasn't escaped |

## Technical changes

All gameplay logic lives in Wick.Sim, so the server's re-simulation picks it up automatically once the shared source is redeployed.

| Area | Change |
| --- | --- |
| Sim: tile rules | Pit tiles enter Wick's movement step; moving onto one triggers a fall before hits and pickups resolve |
| Sim: state | Per carried item, the source pile id; an exit-lock countdown; respawn invulnerability reuses the existing hit window |
| Sim: tick order | Fall resolves in step 2 (movement), so a fall on the same tick as a trap hit counts as the fall only |
| Sim: knockback | Knockback paths check pits per `knockbackIntoPits` |
| Sim: guardians | Pathfinding treats pits as blocked |
| Sim: events | `PitFall`, `PitRespawn`, `LootDropped` (with pile ids and amounts), `ExitLocked`, `ExitUnlocked` |
| Sim: tuning | `SimTuning`: `pitRespawnExitLockTicks`, `knockbackIntoPits`; `VaultRules`: pit levels, cost, max level |
| Sim: version | Bump `SimVersion` once for M10.1; golden replays regenerated deliberately, with the diff reviewed |
| Validator | Doorway placement ban plus a pit-free route check (grid flood fill from the entrance to every treasure pile, pits treated as blocked) |
| Editor | Live refusal of pit placements in red with the reason; route preview overlay while painting pits |
| Server | `PublishVault` runs the same validator; `SubmitRaid` verifies with the new SimVersion and keeps the previous build for in-flight raids |
| Analytics | `raid_end` gains `pit_falls` and `loot_dropped`; new `pit_fall` event (vault id, pit level, flickers left, loot dropped) |
| Clips | `PitFall` scores as a highlight event for shared clips |

## Assets needed

| ID | Asset | Notes |
| --- | --- | --- |
| `tile_<theme>_pit` | Pit edge, pit corner and glowing pit floor | Already in each theme's tile kit; check the edge glow reads outside Wick's light |
| `tile_pit_levels` | Pit level looks | Level 1 plain, level 2 with spikes on the rim, max level with a deeper glow and a gold-eating maw at the bottom |
| `anim_wick_pit_fall` | Wick falling in | 18-frame wobble, flail and drop, then gone |
| `anim_wick_pit_respawn` | Wick popping back out at the entrance | 20-frame pop up with a dazed shake |
| `vfx_pit_fall` | Fall puff | Dust and a few sparks from his flame as he drops |
| `vfx_pit_respawn` | Respawn puff | Smoke puff at the entrance |
| `vfx_loot_return` | Loot flying back to its piles | Coins and gems arc back, max level only |
| `prop_entrance_portcullis` | Exit lock | Portcullis drops with a clang and lifts when the lock ends (only if the exit lock is kept) |
| `ui_hud_exit_lock` | Exit lock countdown | Small padlock and seconds over the entrance |
| `ui_ico_pit` | Editor Pit tile icon | Replaces the "Lava/Block" icon from the UI sheet |
| `ui_editor_route_overlay` | Route preview | Green safe-route line, red refused tile |
| `sfx_pit` | Pit sounds | Falling whistle, distant thud, respawn pop, portcullis clang, loot whoosh |

## M10.1a Simulation

Save this pack as `docs/m10-1-pits.md` in the repo first; the prompts refer to it.

```text
Read CLAUDE.md, docs/m10-1-pits.md (all sections) and docs/tech-design.md
(Raid simulation). Plan first and show me the state and event changes before
writing code. Sim work only; no Unity presentation in this session.

1. Pit tiles in Wick.Sim movement (tick step 2): entering a pit tile triggers a
   fall. Running stops; ice slides that end in a pit fall; tiptoe doesn't
   prevent it.
2. Fall resolution: lose 1 flicker unless invulnerable; if that leaves 0
   flickers, Snuffed and the raid ends. Otherwise teleport Wick to the entrance
   tile, facing into the vault, and start the standard invulnerability window.
   A fall on the same tick as a trap hit counts as the fall only.
3. Carried loot tracking: each carried item records its source pile id. At max
   pit level (from VaultRules), drop all carried items and restore each to its
   source pile. Below max level, keep them.
4. Exit lock: after a respawn, block escape for SimTuning.pitRespawnExitLockTicks
   (default 240). 0 disables it. Escape attempts during the lock do nothing.
5. Knockback (Bellows max level, Sir Whiskers swat): if
   SimTuning.knockbackIntoPits is false (default), knockback stops on the last
   tile before a pit; if true, it can push Wick into one.
6. Guardian and moth pathing treat pits as blocked.
7. Events (presentation only, never affecting state): PitFall, PitRespawn,
   LootDropped (pile ids and amounts), ExitLocked, ExitUnlocked.
8. VaultRules: pit levels (proposed 3), cost per level, max level flag.
9. Bump SimVersion once. Regenerate golden replays deliberately and show me the
   list of changed hashes with the reason for each.
10. Tests for every row of the rules and edge-case tables, plus new golden
    replays: fall and recover; fall on the last flicker; max-level loot drop and
    re-grab; knockback stopping at a pit edge; ice slide into a pit; exit lock
    blocking an immediate escape.

Acceptance: all tests green in both runtimes; new golden hashes identical in
Unity and .NET; fuzz run of 2,000 raids on vaults with pits throws nothing.
```

## M10.1b Validator and editor

```text
Read CLAUDE.md and docs/m10-1-pits.md (Pit rules, Technical changes). Plan
first. M10.1a must be merged.

1. VaultValidator (Wick.Sim, so client and server share it):
   - Refuse pits on door tiles, the entrance, and the tiles directly in front of
     and behind any doorway (including the tile in front of the entrance).
   - Pit-free route check: grid flood fill from the entrance treating pits and
     walls as blocked (and locked doors as blocked unless their lever is
     reachable on the same pit-free route). Every treasure pile must be
     reachable. Refuse any vault that fails.
   - Return a reason code per refusal (PitInDoorway, PitBlocksRoute) and the
     tile(s) involved.
2. Editor: when painting a pit, validate live and show refused tiles in red
   with a short reason tooltip; show a green preview of the current safe route
   while the pit tool is selected.
3. Editor tray: the Pit tile with its icon (replacing the old "Lava/Block"
   icon), level selector, cost in build points.
4. Existing saved drafts and live vaults: write a migration check that flags
   any vault that now fails the new rules, and list them for me; don't modify
   any vault automatically.
5. Tests: doorway cases (door, entrance, both sides), a corridor fully blocked
   by a pit line, an open room ringed with pits, a route that only works via a
   lever door, and a valid vault with several pits.

Acceptance: validator tests green in both runtimes; I can't place a blocking
pit in the editor on device; the migration report runs against dev data.
```

## M10.1c Presentation, server and analytics

```text
Read CLAUDE.md and docs/m10-1-pits.md (Assets needed, Technical changes).
Plan first. M10.1a and M10.1b must be merged. Use placeholder art where final
assets in Art/Incoming aren't ready yet, named to the asset IDs in the pack.

1. Views driven only by sim events: PitFall plays anim_wick_pit_fall and
   vfx_pit_fall; PitRespawn plays anim_wick_pit_respawn and vfx_pit_respawn at
   the entrance; LootDropped plays vfx_loot_return arcing to each pile and
   updates pile visuals; ExitLocked and ExitUnlocked drive the portcullis and
   the HUD lock countdown.
2. Pit tile visuals per level, with an edge glow that stays visible outside
   Wick's light (an exception in the visibility mask for pit edges only).
3. Camera: a short pan from the pit to the entrance on respawn (presentation
   only), skippable by any input.
4. Audio: sfx_pit set wired to the same events.
5. Server: redeploy the Cloud Code module with the new shared Sim; keep the
   previous SimVersion available for raids started before the deploy;
   PublishVault uses the new validator. Confirm with a test raid on dev.
6. Analytics: raid_end gains pit_falls and loot_dropped; new pit_fall event
   (vault id, pit level, flickers left, loot dropped).
7. Clip highlights: score PitFall as a highlight event.
8. Tutorial: add a pit intro card (3-second clip of a fall and the escape
   route) shown the first time a player meets a pit.

Acceptance: on device, a fall, a max-level loot drop and an exit lock all play
correctly and match the replay of the same raid; server re-sim agrees with the
client on 20 test raids containing pits; the pit_fall event appears in the
dashboard.
```

## Acceptance checklist

M10.1 is done when every box is ticked. Then run the standard milestone review prompt against the M10 tag.

- [ ] Both decisions recorded (exit lock length, knockback into pits) and set in SimTuning.
- [ ] Falling costs 1 flicker and respawns Wick at the entrance; a fall on the last flicker snuffs him.
- [ ] Below max level loot is kept; at max level it returns to its piles and can be re-grabbed.
- [ ] The exit lock blocks an immediate escape after a respawn (if kept).
- [ ] Knockback behaves as decided at pit edges.
- [ ] The editor refuses pits in doorways and any pit that blocks the route, with the reason shown.
- [ ] The migration report lists any existing vaults that break the new rules.
- [ ] All golden replays match in Unity and .NET; hash changes reviewed.
- [ ] Server re-sim agrees with the client on raids with pits.
- [ ] Fall, respawn, loot return and portcullis read clearly on a low-end phone.
- [ ] Pit intro card added to the tutorial.
- [ ] Analytics events visible in the dashboard.
