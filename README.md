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
3. Clone the repo, then in Unity Hub choose **Add > Add project from disk** and pick
   the `unity/` folder (not the repo root).
4. Open the project. The first import takes a few minutes.

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
