# Wick — Technical Design

Oct 17, 2026 · @Jamie

## Key decisions

One shared, deterministic C# simulation runs every raid on the phone and again on the server. Everything else (replays, clips, cheat checks, ghost races) falls out of that one choice.

| Decision | Choice | Why |
| --- | --- | --- |
| Engine | Unity 6 LTS, URP, portrait | Matches the team's Unity work; strong mobile tooling |
| Raid logic | Pure C# library, no `UnityEngine` references, fixed 30-tick loop, integer and fixed-point maths | Bit-identical results on every device and on the server |
| Movement | Tile-based grid, not physics | Simple controls, cheap determinism, readable traps |
| Raid record | Seed + vault version + compact input log | A 60-second raid fits in well under 1 KB |
| Backend | Unity Gaming Services (UGS): Authentication, Cloud Save, Cloud Code C# modules, Leaderboards, Remote Config, Analytics | Cloud Code runs the same C# sim library on the server, so validation needs no second codebase |
| Validation | Server re-simulates every clear-check and every raid before paying out | Rejects impossible results; abuse is mitigated, not eliminated (see Anti-cheat) |
| Clips | Re-render the raid from its log offscreen, then encode | Consistent clips with no live screen recording during play |
| Share links | Own domain with iOS Universal Links and Android App Links, plus a small web fallback page | Firebase Dynamic Links has shut down; owning the domain avoids lock-in |
| Ads and IAP | Unity LevelPlay (rewarded only) and Unity IAP with server receipt validation | One vendor stack, already integrated with Unity |

The guiding rule is pragmatic: build only what the vertical slice needs, but never let presentation code leak into the simulation.

## Architecture

&#91;embedded content: system architecture · client, UGS backend, third parties\]

The phone plays raids and sends only seeds and input logs; Cloud Code re-runs them with the identical Wick.Sim source (dashed line) before anything is paid out. Ads, receipts and push all settle on the server side.

## Project structure

The simulation lives in its own assembly that cannot reference Unity, enforced by an assembly definition with `noEngineReferences: true`. The same source is compiled into the Cloud Code module.

```
wick/
  unity/                          Unity project
    Assets/_Wick/
      Sim/          Wick.Sim.asmdef        pure C#, no UnityEngine
      Sim.Tests/    Wick.Sim.Tests.asmdef  NUnit edit-mode tests
      Game/         Wick.Game.asmdef       views, input, camera, UI
      Editor/       Wick.Editor.asmdef     vault editor tools, debug windows
      Services/     Wick.Services.asmdef   UGS, ads, IAP, links, analytics
      Art/ Audio/ Prefabs/ Scenes/ Data/
  server/
    Wick.CloudCode/   Cloud Code C# module (links Sim source)
    Wick.Sim.Server.Tests/  same golden replays, run in .NET
  tools/
    replay-cli/   re-simulates a raid file and prints its hash
  docs/           this spec, ADRs, CLAUDE.md
```

Dependency rule: `Game` and `Services` may use `Sim`; `Sim` uses nothing. `Wick.CloudCode` includes the `Sim` folder as linked source files so both sides always build from one copy.

## Raid simulation

A raid is a pure function: the same vault, seed and input log always produce the same result and the same state hash, on any device or server.

**Time and space**

- Fixed 30 ticks per second; a 60-second raid is 1,800 ticks. The client renders at 60 fps and interpolates between ticks.
- Positions are integer tiles plus sub-tile progress in integer units (1 tile = 1,000 units). No `float` or `double` anywhere in `Sim`.
- The 5-second scout flyover is presentation only; the simulation clock starts when it ends.

**Entities**

| Entity | State | Rules |
| --- | --- | --- |
| Wick | Tile, sub-tile progress, facing, flickers (0 to 3), tiptoe flag, carried treasure, decoy used | Dash speed drops with carried treasure; tiptoe halves light radius and speed |
| Traps | Type, tile, rotation, cycle length, phase offset, triggered flag | Timed traps are a pure function of tick, so they never drift |
| Guardians | Type, tile, patrol waypoints, alert state, target | Sight checked with integer grid line-of-sight, only against tiles inside Wick's light |
| Treasure piles | Tile, value, taken flag | Counted only if Wick escapes |
| Braziers | Tile, used flag | Relight 1 flicker, once each |

**Tick order** (fixed, never reordered)

```
1. apply inputs stamped for this tick
2. move Wick (and decoy timer), then recompute the tiles his flame lights
3. advance traps by tick
4. update guardians, sorted by entity id
5. resolve hits: flicker loss, snuff, invulnerability window
6. resolve pickups: treasure, braziers
7. check end: escaped, snuffed, or timer at 0
8. every 30 ticks: fold state into running hash
```

**Inputs**

Commands are `Dash(dir)`, `RunStart(dir)`, `RunStop`, `TiptoeOn`, `TiptoeOff` and `Decoy`. The client converts swipes and holds into commands stamped with the next tick. Each command is encoded as a delta-tick varint plus one byte, so a busy raid is a few hundred bytes.

**Determinism rules**

- Randomness only from a seeded PCG32 owned by the sim; the seed comes from the server.
- Never iterate a `Dictionary` or `HashSet` for game logic; use arrays sorted by entity id.
- No `DateTime`, no LINQ over unordered collections, no static mutable state.
- State hash: 64-bit FNV-1a over a fixed field order, recorded every 30 ticks and at the end, so mismatches can be pinpointed to a one-second window.

## Data model

Vaults and raids are small, versioned JSON documents, validated on both client and server against the same C# types.

**Vault**

```json
{
  "schema": 1,
  "vaultId": "v_8f3k2q",
  "ownerId": "p_19x...",
  "version": 7,
  "vaultLevel": 3,
  "theme": "crypt",
  "grid": { "w": 9, "h": 13 },
  "tiles": "base64 of 1 byte per tile: floor, wall, door, pit, ice",
  "entrance": [4, 12],
  "objects": [
    { "id": 1, "type": "drip_pipe", "x": 3, "y": 8, "rot": 0, "cycle": 45, "offset": 10 },
    { "id": 2, "type": "night_owl", "x": 6, "y": 4, "patrol": [[6,4],[6,9],[2,9]] },
    { "id": 3, "type": "treasure", "x": 4, "y": 1, "value": 400 }
  ],
  "buildPoints": 118,
  "clearCheck": { "seed": 99120, "log": "base64", "hash": "a91c...", "stars": 2 }
}
```

**Raid record**

```json
{
  "raidId": "r_...",
  "vaultId": "v_8f3k2q",
  "vaultVersion": 7,
  "raiderId": "p_...",
  "seed": 55021,
  "simVersion": 3,
  "log": "base64",
  "claimed": { "stars": 2, "loot": 260, "hash": "0c4e..." },
  "verified": { "stars": 2, "loot": 260, "ok": true }
}
```

**Player data (Cloud Save)**

| Key | Holds | Written by |
| --- | --- | --- |
| `profile` | Name, selected skin, Vault Level, Crowns, guild id | Server only |
| `wallet` | Gold, wax, gems | Server only |
| `inventory` | Owned skins, themes, trap skins, pass tier | Server only |
| `vault_draft` | Vault being edited | Client |
| `vault_live` | Published vault, read by matchmaking | Server only, after clear-check |
| `settings` | Audio, haptics, consent choices | Client |

Cloud Save keys allow only letters, digits, `_` and `-`. Server-only keys use the Protected access class: the player can read them, only Cloud Code writes them.

`simVersion` is bumped whenever sim rules change. The server keeps old sim builds long enough to verify raids started before an update.

## Client

The Unity side only draws and listens: views read sim state each frame and never change it.

**Scenes**

| Scene | Purpose |
| --- | --- |
| `Boot` | Init UGS, consent, auth, Remote Config, deep-link routing |
| `Home` | Your vault view, shop, pass, guild, raid button |
| `Raid` | Scout flyover, live raid, results, replay viewer |
| `Editor` | Vault building and Test mode (loads `Raid` additively) |
| `ClipRender` | Offscreen raid re-render for clips |

**Presentation**

- `RaidRunner` steps the sim on a fixed accumulator and exposes the previous and current state; views interpolate between them.
- Each sim entity type maps to a view prefab through a `ViewRegistry` ScriptableObject. Views raise animation and audio events from sim events such as `FlickerLost`, `TreasureTaken` and `TrapFired`.
- Isometric 3/4 camera with a fixed angle, gentle follow and a short zoom punch on hits.
- Flame light: standard 3D URP lighting (baked, dark-toned vault lighting plus a point light on Wick for warmth), with a separate shader-driven visibility mask. Wick.Sim decides which tiles are lit each tick; the mask only visualises that set, so what the player sees always matches what guardians and replays use. Prototype the mask early on a low-end Android device.

**Input**

`RaidInput` reads touch through the Input System. Every action works with one thumb, including moving while staying stealthy:

- A swipe over 40 dp becomes `Dash`; a swipe held at the end becomes `RunStart`, then `RunStop` on release.
- A tap anywhere that isn't a swipe toggles stealth (`TiptoeOn` / `TiptoeOff`). Swipes keep working while stealthy, so the player sneaks with ordinary swipes. A clear on-screen state (dimmed flame, tiptoe pose, highlighted stealth icon) shows the mode.
- A dedicated decoy button in the bottom corner fires `Decoy`, because tapping a small character on an isometric screen is unreliable.

Thresholds live in Remote Config for tuning. Prototype tap-to-toggle first, with a fallback of a stealth toggle button beside the decoy button if accidental taps prove a problem.

**Character reactions**

One Animator per skin shares a common controller. A `ReactionDriver` reads sim state and sets parameters:

| Parameter | Driven by |
| --- | --- |
| `Fear` (0 to 1) | Distance to nearest active trap or alert guardian |
| `Flickers` | Current flickers; drives flame frenzy and sweat |
| `Greed` | Treasure picked up this second, for the grin |
| `Tiptoe` | Tiptoe flag |
| `Outcome` | Escaped or snuffed, for end poses |

Skins swap the mesh, materials and flame VFX but keep the same rig, so every reaction works for every skin.

## Backend

The client never writes wallet, inventory or live-vault data directly; every change to them goes through a Cloud Code function that checks it first.

**Services**

| Need | Service |
| --- | --- |
| Accounts | UGS Authentication: anonymous first, then Apple, Google Play Games or Google sign-in to link |
| Storage | UGS Cloud Save for player data and published vaults (custom items with queryable indexes) |
| Game logic | UGS Cloud Code C# module, sharing the `Sim` source |
| Rankings | UGS Leaderboards for Crowns, weekly vault snuffs and Daily Vault times |
| Friends | UGS Friends |
| Guilds | Custom: Cloud Save custom items plus Cloud Code functions, until a native groups service is worth adopting |
| Tuning | UGS Remote Config for economy numbers, ad caps, input thresholds |
| Push | Firebase Cloud Messaging for "your vault was raided" and pass reminders |

**Cloud Code API**

| Function | Does |
| --- | --- |
| `GetRaidTarget()` | Picks a live vault near the player's Crowns from a server-only matchmaking directory (custom items indexed on pool and Crowns; the band starts at ±100 and doubles to ±1600, then any; players before seed vaults), excluding own, shielded and raided-in-24-h vaults; returns vault, seed and a raid token that expires in 5 minutes. 20 raid tokens per player per hour |
| `StartFriendRaid(vaultId)` | Same, for a specific vault from a link or friend list |
| `SubmitRaid(token, log, claimed)` | Re-simulates; if the result matches, pays loot, updates Crowns and the defender's stats and bonus, stores the raid record and sends a push |
| `PublishVault(draft)` | Parses with `VaultJson`, runs `VaultValidator` (schema, build budget, Vault Level limits, placement, reachability), re-simulates the clear-check with `ClearCheck.Verify` (needs at least 1 star), then writes `vault_live`. `VaultJson` alone only checks what the sim needs to run safely, so the validator must always run too |
| `GetDefenceLog()` / `GetReplay(raidId)` | Raids on your vault and their logs, for replay (defender and raider only) |
| `SeedVault(name, vault, crowns)` | Service accounts only: lists a designer vault as a bot-owned live vault after the same checks as `PublishVault` |
| `StartUpgrade(item)` / `FinishUpgrade(item)` | Server-timed building and upgrades, including star gates and workshop slots |
| `ClaimAdReward(placement)` | Pays only after the ad network's server-to-server callback confirms the view, within daily caps |
| `ValidatePurchase(receipt)` | Verifies store receipts, then grants items |
| `ClaimPassTier(tier)` / `ClaimAchievement(id)` | Grants rewards once, idempotently |

Every function is idempotent on a client-supplied request id, so retries on a bad connection never pay twice. Idempotency alone doesn't stop two concurrent requests overwriting each other's balance, so every wallet, inventory and reward write uses Cloud Save write locks (optimistic concurrency): read with the lock value, write conditionally, and re-read and retry on conflict.

## Replays, clips and share links

Because a raid is just a log, replays and clips are re-runs, not recordings.

- **Replays:** the defence log and the raid results screen load the vault, seed and log into `RaidRunner` with input disabled. Scrubbing works by re-simulating from tick 0, which takes milliseconds.
- **Highlight detection:** during a raid the sim emits scored events (near miss, flicker lost, snuff, last-second escape, big grab). The highest-scoring 10 to 15 second window becomes the clip.
- **Clip render:** the `ClipRender` scene re-runs that window offscreen at 720 x 1280, 30 fps, with a cinematic camera, slow motion on the key moment and the vault link watermark. Frames go to a native encoder plugin (Android MediaCodec, iOS AVAssetWriter) and out through the native share sheet.
- **Share links:** `https://wick.uprisestudios.co.uk/v/{vaultId}` (subdomain to confirm). The domain serves an `apple-app-site-association` file and `assetlinks.json`, so an installed app opens the raid directly. Without the app, a light web page shows the vault card and store buttons.
- **Deferred deep links:** for the first install from a link, Android passes the vault id through the Play Install Referrer; on iOS the web page copies a short vault code that the app offers to paste on first launch. An attribution SDK such as Branch can replace this later if needed.

## Ads, IAP, economy and anti-cheat

The server is the only source of truth for money and rewards; the client only displays what the server grants.

- **Rewarded ads:** Unity LevelPlay with rewarded format only; banner and interstitial modules are never added to the build. Rewards are granted by `ClaimAdReward` after LevelPlay's server-to-server callback. Caps per placement and per day come from Remote Config.
- **Consent:** Google UMP for UK and EU consent, Apple's App Tracking Transparency prompt on iOS, and non-personalised ads for anyone who declines or is under 18.
- **IAP:** Unity IAP for the starter pack, Heist Pass, Wick Club subscription, skins and gem packs. Receipts go to `ValidatePurchase`; subscriptions are re-checked on each session start.
- **Economy config:** prices, upgrade times, star gates, loot formulas and pass tiers live in versioned JSON in Remote Config, read by both client (for display) and Cloud Code (for truth).

**Anti-cheat**

Server re-simulation proves a submitted raid is possible under the rules, not that a human actually played it. The full simulation and vault data live on the client, so a modified client could compute a valid winning input sequence and submit it. This is an inherent limit of the design; the defences below make that costly and detectable rather than impossible.

| Threat | Defence |
| --- | --- |
| Impossible raid results | Server re-simulation; mismatched hashes pay nothing and are logged |
| Machine-generated winning logs (solver or bot) | Single-use raid tokens that expire in 5 minutes, raid attempt limits per hour, minimum real elapsed time per raid, and anomaly detection on input timing, precision, win rates and duration against vault difficulty |
| Modified or emulated clients | Platform integrity checks: Play Integrity API on Android and App Attest on iOS, verified server-side; failures route to a restricted pool rather than an instant ban |
| Impossible or unbeatable vaults | Clear-check re-simulation and build-budget checks before publishing |
| Edited wallet or inventory | Server-only Cloud Save keys |
| Concurrent requests overwriting balances | Cloud Save write locks on every wallet, inventory and reward write, with retry on conflict |
| Replaying an old raid log | One-time raid tokens tied to vault version and seed |
| Speed hacks on timers | All upgrade timers checked against server time |
| Faked ad rewards | Rewards only after the server-to-server callback |
| Repeat offenders | Flag accounts on anomaly scores and repeated mismatches; route them to a separate matchmaking pool, with manual review before bans |

## Analytics and live ops

UGS Analytics collects a small set of custom events from day one, chosen to answer the vertical slice's one question: is raiding fun, and do people share vaults?

| Event | Key fields | Answers |
| --- | --- | --- |
| `raid_end` | vault id, outcome, stars, duration, flickers left, cause of snuff | Is raiding fun and fair? Which traps feel cheap? |
| `raid_quit` | tick, flickers left | Where do players give up? |
| `vault_published` | traps used, build points, clear-check attempts | Is the editor usable? |
| `vault_shared` | channel (link, clip) | Do players share? |
| `link_opened` | vault id, installed or not | Do shares bring installs? |
| `ad_reward` | placement | Which rewards players value |
| `purchase` | product, price | What sells |
| `pass_tier` | tier, days into season | Is the pass paced right? |

**Live ops**

- Remote Config holds economy numbers, ad caps, featured vaults and the Daily Vault id, so most tuning ships without an app update.
- Seasons and events are data: pass tiers, rewards and challenges are JSON plus asset bundles delivered with Unity Addressables over a CDN.

## Testing and performance budgets

Determinism is tested on every commit, because a single mismatch between phone and server would silently stop rewards from paying out.

**Testing**

- **Sim unit tests:** movement, each trap's timing, guardian sight, hits, pickups and end conditions, as NUnit edit-mode tests in `Wick.Sim.Tests`.
- **Golden replays:** a folder of saved raids (vault, seed, log, expected hash). CI runs them through the Unity tests and through `Wick.Sim.Server.Tests` in plain .NET; both must produce the same hashes.
- **Device check:** a debug menu replays all golden raids on a real iOS and Android device and reports any hash mismatch.
- **Fuzzing:** random inputs on random generated vaults for thousands of raids, checking for exceptions and client-server agreement.
- **Play mode tests:** smoke tests that boot, run a scripted raid, publish a vault and reach the results screen.

**Performance budgets**

| Budget | Target |
| --- | --- |
| Frame rate | 60 fps on a mid-range 2021 Android phone; 30 fps floor on low-end |
| Sim step | Under 0.2 ms per tick on device |
| Server re-sim | Under 50 ms for a full 60-second raid |
| Draw calls in a raid | Under 150 |
| Install size | Under 150 MB, with seasonal content in Addressables |
| Cold start to Home | Under 5 seconds on mid-range devices |

## Build order and open questions

Build the simulation first and prove determinism before any art or backend work. Each milestone matches a stage in the Claude Code prompt pack.

1. **M0 Foundations:** repo, assemblies, CI running sim tests.
2. **M1 Sim core:** grid, Wick movement, flame, inputs, hashing, golden replays.
3. **M2 Traps and guardians:** the six slice entities, braziers, scoring and end conditions.
4. **M3 Playable raid:** Unity views, camera, touch input, flame light, reactions, results screen. First feel test.
5. **M4 Vault editor:** building, budget, Test mode, local clear-check.
6. **M5 Backend:** UGS auth, Cloud Save, Cloud Code module with shared sim, `PublishVault` and `SubmitRaid`.
7. **M6 Async raiding:** matchmaking, raid tokens, defence log and replays, 20 seeded designer vaults.
8. **M7 Sharing:** clip highlights, render and share, vault links and web fallback.
9. **M8 Economy shell:** gold and wax, upgrades with star gates, rewarded ads, IAP stubs, analytics events.

**Open technical questions**

- [ ] Confirm Cloud Code C# module limits (execution time, memory, call rates) and cost at soft-launch scale in an M5 spike.
- [ ] Confirm Cloud Save query indexes can serve matchmaking by Crown range fast enough, or add a small vault index.
- [ ] Choose the native video encoder plugin: buy one or write thin platform wrappers.
- [ ] Pick the share-link domain.
- [ ] Decide whether Heist Race uses UGS Relay or a lightweight WebSocket service.
