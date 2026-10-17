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

Replay CLI: re-simulates raid files and prints outcome, ticks and hash, plus
`OK`/`MISMATCH` when the file has an `expected` block. Exit code 0 = all OK,
1 = mismatch, 2 = load error. Add `--checkpoints` to print the hash for every
second, to find where two runs diverge.

```
dotnet run --project tools/replay-cli -- tests/golden/*.json
```

### Golden replays

Each file in `tests/golden/` holds a vault (with a readable `_map` next to the
base64 `tiles`), a seed, the input log (readable `commands`, or base64 `log`)
and the expected outcome, ticks and hash. Any sim rule change bumps
`SimInfo.Version`; re-record goldens deliberately: check the outcome and ticks
are still what you intend, then update `simVersion` and `hash` from replay-cli.

## CI

`.github/workflows/ci.yml` runs on every push and pull request:

- `sim-dotnet`: the .NET sim tests, plus a build and smoke run of replay-cli.
- `unity-editmode`: the Unity EditMode tests via GameCI. It needs the repo secrets
  `UNITY_LICENSE`, `UNITY_EMAIL` and `UNITY_PASSWORD`; see the GameCI activation
  guide (https://game.ci/docs/github/activation) to get or renew the licence file.
