# Wick — project rules

Mobile build-and-raid game (Unity 6 LTS, URP, portrait). Players build trap vaults
and raid others' vaults as Wick, a living candle thief. Specs: docs/game-spec.md,
docs/tech-design.md. Read the relevant section before starting any task.

## Architecture rules (never break these)
- unity/Assets/_Wick/Sim is pure C#. No UnityEngine, no float/double, no DateTime,
  no static mutable state, no Dictionary/HashSet iteration in game logic.
- Sim randomness only from the sim-owned seeded PCG32.
- Positions integer tiles + sub-tile units (1 tile = 1000). 30 ticks/second.
- Tick order is fixed (see docs/tech-design.md, Raid simulation). Do not reorder.
- Any change to sim rules bumps SimVersion and updates golden replays deliberately.
- Game/Services may reference Sim. Sim references nothing.
- server/Wick.CloudCode links Sim source files; never fork sim code for the server.
- Client never writes wallet, inventory or vault.live; only Cloud Code does, and
  always with Cloud Save write locks (optimistic concurrency).
- Visibility (which tiles are lit) is decided by Sim. Rendering only visualises it.
- Server re-simulation proves a raid is possible, not that a human played it.
  Never describe it as cheat-proof.

## Working style
- Pragmatic: build only what the current milestone needs. No speculative abstractions.
- Small, focused commits. Write or update tests with every sim change.
- Prefer plain C# classes and ScriptableObjects over complex frameworks.
- Ask before adding any package or third-party SDK.

## Commands
- Sim tests (.NET): dotnet test server/Wick.Sim.Server.Tests
- Unity tests: "C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe" -batchmode -projectPath unity -runTests
  -testPlatform EditMode -testResults results.xml
- Replay check: dotnet run --project tools/replay-cli -- raid.json

## Definition of done
- All tests pass in both Unity and .NET.
- Golden replay hashes unchanged unless the task says otherwise.
- Tell me exactly what to check by hand in the Editor or on device.