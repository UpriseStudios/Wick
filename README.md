# Wick

Mobile build-and-raid game: build trap vaults, raid other players' vaults as Wick,
a living candle thief. Unity 6 LTS, URP, portrait.

- Game design: [docs/game-spec.md](docs/game-spec.md)
- Technical design: [docs/tech-design.md](docs/tech-design.md)
- Rules for contributors (human and AI): [CLAUDE.md](CLAUDE.md)

## Layout

```
unity/                       Unity project (open this folder in the Hub)
  Assets/_Wick/
    Sim/          Wick.Sim         pure C#, no UnityEngine (noEngineReferences)
    Sim.Tests/    Wick.Sim.Tests   NUnit EditMode tests
    Game/         Wick.Game        views, input, camera, UI          -> Sim
    Services/     Wick.Services    UGS, ads, IAP, links, analytics   -> Sim
    Editor/       Wick.Editor      vault editor tools, debug windows -> Sim, Game
    Art/ Audio/ Prefabs/ Scenes/ Data/
server/
  Wick.Sim.Server.Tests/     .NET 8 NUnit; compiles Sim and Sim.Tests as linked source
tools/
  replay-cli/                .NET 8 console; compiles Sim as linked source
tests/
  golden/                    golden replays (vault, seed, log, expected hash)
```

`Wick.Sim` is the one deterministic simulation shared by the client, Cloud Code
and tools. It is never copied: the .NET projects include
`unity/Assets/_Wick/Sim/**/*.cs` directly. Two compilers guard it:

- Unity: `noEngineReferences: true` on `Wick.Sim.asmdef`, so `using UnityEngine;`
  fails with CS0246.
- .NET: the server and tools projects have no Unity references and use
  `LangVersion 9.0` with implicit usings off, matching Unity's compiler, so code
  that builds there also builds in Unity.

## Setup

1. Install **Unity Hub** and Unity **6000.6.4f1** (the version in
   `unity/ProjectSettings/ProjectVersion.txt`), with Android and iOS build support.
2. Install the **.NET 8 SDK** (`dotnet --list-sdks` should show 8.0.x).
3. Install **Git LFS** (`git lfs version`), then clone the repo. Binary assets
   (images, meshes, audio, fonts) are stored in LFS; see `.gitattributes`.
4. Once per clone, turn on Unity's smart merge for scenes and prefabs:

   ```
   git config merge.unityyamlmerge.name "Unity SmartMerge"
   git config merge.unityyamlmerge.driver '"C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Data/Tools/UnityYAMLMerge.exe" merge -p %O %B %A %A'
   git config merge.unityyamlmerge.recursive binary
   ```

5. In Unity Hub choose **Add > Add project from disk** and pick the `unity/`
   folder (not the repo root).
6. Open the project. The first import takes a few minutes.

## Playing a raid

Open `Assets/_Wick/Scenes/Raid.unity` and press Play. Pick a dev vault; after
the 5-second scout the clock starts. In the Editor the mouse stands in for a
thumb:

- **Drag** past 40 dp: dash one tile. **Drag and hold** at the end: run;
  release to stop. Dragging while running steers.
- **Click** (no drag): toggle stealth. Swipes keep moving Wick while stealthy.
- **Decoy** button, bottom right.
- **DEV** button, top left (development builds and the Editor): FPS, tick,
  current hash, tile grid, the sim's lit tiles (should match the lit area
  exactly), the fallback stealth-button scheme, and golden replays.

The results screen shows stars and loot, and in development builds the live
hash next to a headless replay of the recorded log (`MATCH` expected).

Content is generated from code and committed. After changing
`Assets/_Wick/Editor/WickContentBuilder.cs`, run **Wick > Rebuild Placeholder
Content**. Dev vaults come from `tools/golden/make_dev_vaults.py`. Tuning lives
in `Assets/_Wick/Data/RaidTuning.asset` and `InputTuning.asset`.

## Vault editor

From the Raid scene's vault picker choose **Vault editor** (or open
`Assets/_Wick/Scenes/Editor.unity`). Start from one of three templates. Two tabs:

- **Defences** (the default) works like a base builder, with no modes. Drag a
  card from the shop onto the vault to place it; drag an item to move it; drag
  empty floor to look around; pinch to zoom. A drop that would break a rule
  (over budget, a crate in a doorway, blocking the only way to the hoard...) springs back
  and says why. Tap an item for its bar: **Rotate** (Bellows and Night Owl),
  **Patrol** (owl: tap tiles to add points) and **Remove**. Shop cards show the
  cost, or a count for treasure and braziers, and are greyed when blocked.
  Braziers and Bellows go on walls; a Bellows always faces open floor.
- **Rooms** paints the layout with one finger: Wall, Floor, Door, Entrance, and
  Hoard room (drag from corner to corner). Two fingers pan and zoom.

The top bar shows Vault Level, build points and a **Ready / N problems** pill;
tap it and then a problem to jump to it. Undo and Redo cover every change and
drafts save automatically. **Test** plays the vault: escaping with at least 1
star records the clear-check, and **Publish** sends the vault to the server,
which runs the building rules and replays the clear-check before the vault goes
live. Any edit after a test needs a new test.

Build rules (costs, budgets, unlock levels, treasure) are in
`Assets/_Wick/Sim/Vault/VaultRules.cs`; the checks are in `VaultValidator.cs`.
Templates come from `tools/golden/make_vault_templates.py`.

## Backend (Unity Gaming Services)

The Cloud Code module is `server/Wick.CloudCode` (module name `WickModule`). It
compiles the Sim source as linked files and exposes `PublishVault`,
`StartFriendRaid` and `SubmitRaid`.

```
ugs config set project-id 71e95e2b-a983-4b74-96f4-16d2974b9c78
ugs deploy server/Wick.CloudCode/Wick.CloudCode.sln -e development
dotnet test server/Wick.CloudCode/WickModule.Tests
```

A new version takes up to a minute to replace the old one after a deploy.
Development builds use the `development` environment, where you may raid your
own vault: in the Raid scene pick **My live vault (server)**. The results
screen then shows the server's verdict. Loot is paid only when the server's
re-simulation of your input log reproduces your result. That shows the raid is
possible under the rules, not that a person played it.

Player data the server owns (`wallet`, `stats`, `profile`, `defence`, `push`,
`vault_live`, `raid_tokens`, `idempotency`) is in Cloud Save's Protected class:
the player can read it, only Cloud Code writes it, always under write locks.

**Async raiding (M6).** Home has **Raid** (matchmaking by Crowns), **Raids on
your vault** (the defence log, with Watch replay and Revenge) and the editor.
Live vaults are listed in a server-only matchmaking directory (custom items
`mm_<vaultId>`, compound index `mmPool_1_mmCrowns_1`, which must exist before
anything is listed). Economy and matchmaking numbers are in
`WickModule/Logic/Rules.cs`; Crown deltas are `Sim/Rating/CrownRules.cs`.

**Seed vaults.** 20 bot-owned vaults keep matchmaking from coming up empty.
Regenerate them with `dotnet run --project tools/seedgen` (writes
`Assets/_Wick/Data/SeedVaults`), then upload with **Wick > Upload Seed
Vaults** in the Editor, using a service account key (kept in EditorPrefs on
your machine only).

**Push and integrity** are off until their Secret Manager keys exist:
`FCM_SERVICE_ACCOUNT` and `PLAY_INTEGRITY_SERVICE_ACCOUNT` (Google service
account key files). Without them, pushes are batched but not sent and
integrity verdicts read "not configured". App Attest (iOS) isn't wired yet.

## Sharing (M7)

After a raid or replay, **Share clip** renders the best 10-15 s (highlight
window, cinematic camera, 0.3x slow motion on the key moment, vault link burnt
in) and opens the share sheet; **Share link** and Home's **Share my vault** share
`https://wick.uprisestudios.co.uk/v/CODE`. Clips encode with hardware H.264 on
Android (`Assets/Plugins/Android/WickClipEncoder.java`); iOS isn't wired yet.
**Wick > Preview Share Clip** renders a golden in the Editor and writes sample
frames to `Builds/Screenshots/clip-preview-frames`.

Links open the app through Android App Links (custom main manifest) and are
routed by the `Boot` scene; a first launch reads the Play install referrer
(Android) or offers a copied code (iOS). The site lives in `web/` (Cloudflare
Pages); see `web/README.md`.

## Building for Android

**Wick > Build Android Dev APK** writes `unity/Builds/Wick-dev.apk` (development
build, IL2CPP, ARM64). From the command line:

```
"C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe" -batchmode -quit -projectPath unity -buildTarget Android -executeMethod Wick.Editor.AndroidBuild.BuildDev
adb install -r unity/Builds/Wick-dev.apk
```

**Wick > Capture Dev Screenshots** renders each dev vault (scout, live, lit
overlay) to `unity/Builds/Screenshots`, which is handy for checking rendering
changes without a device.

## Running tests

Sim tests in .NET (fast, the same as CI):

```
dotnet test server/Wick.Sim.Server.Tests
```

Unity EditMode tests: in the Editor, use **Window > General > Test Runner > EditMode
> Run All**. From the command line:

```
"C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe" -batchmode -projectPath unity -runTests -testPlatform EditMode -testResults results.xml
```

The same `Sim.Tests` source runs in both, including the golden replays in
`tests/golden/`, so both sides must produce identical hashes.

Replay CLI: re-simulates raid files and prints outcome, ticks, stars, loot and
hash, plus `OK`/`MISMATCH` when the file has an `expected` block. Exit code
0 = all OK, 1 = mismatch, 2 = load error. Add `--checkpoints` to print the hash
for every second (to find where two runs diverge) and `--events` to print every
sim event with its tick.

```
dotnet run --project tools/replay-cli -- tests/golden/*.json
```

### Golden replays

Each file in `tests/golden/` holds a vault (with a readable `_map` next to the
base64 `tiles`), a seed, the input log (readable `commands`, or base64 `log`)
and the expected outcome, ticks, stars, loot and hash. An optional
`expectedEvents` block pins the ticks of key events (for example
`"FlickerLost": [64]`); the `_note` explains how they were worked out. Any sim
rule change bumps `SimInfo.Version` and re-records goldens deliberately.

The goldens are generated from `tools/golden/make_goldens.py`, which is their
source of truth (Python 3.8+, standard library only). After a rule change:

1. Bump `SimInfo.Version` and `SIM_VERSION` in `make_goldens.py`. If outcomes,
   ticks, stars, loot or event ticks should change, update them there by hand
   and explain why in the `_note`.
2. `python tools/golden/make_goldens.py` rewrites the files, keeping their
   current hashes.
3. `python tools/golden/rerecord.py` replays them and updates a hash only when
   that golden's outcome, ticks, stars and loot still match. Anything else is
   reported and left alone.
4. Run the tests: they check `expectedEvents`, and `FuzzTests.cs` has a corpus
   hash that must be updated by hand from the test's failure message.

To add a golden, add an entry to `GOLDENS` in `make_goldens.py`, work out the
expected values by hand, then run steps 2–4.

## CI

`.github/workflows/ci.yml` runs on every push and pull request:

- `sim-dotnet`: the .NET sim tests, plus a build and smoke run of replay-cli.
- `unity-editmode`: the Unity EditMode tests via GameCI. It needs the repo secrets
  `UNITY_LICENSE`, `UNITY_EMAIL` and `UNITY_PASSWORD`; see the GameCI activation
  guide (https://game.ci/docs/github/activation) to get or renew the licence file.
