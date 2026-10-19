# M9 art review: checklist side by side

How each item on the art review checklists is covered once final art arrives in
`unity/Assets/_Wick/Art/Incoming/`. **Automatic** means a check fails in the Editor and in CI without
anyone looking. **Tool** means M9 built something that shows you the answer. **By eye** means a
person has to judge it.

Status is as of the end of M9 (19 October 2026). No final assets had been supplied yet, so every
system ran on generated stand-ins: rigged skins, tile kits, VFX, UI sprites and synthesised sounds.
These follow the spec's names, bones, budgets and palette.

## Art Direction pack: review checklist

| Checklist item | How M9 covers it | Kind | Status with stand-ins | When real art lands |
| --- | --- | --- | --- | --- |
| Silhouette readable as a solid black shape at 64 px | Not automated in Unity. The Blender toolkit's `render_preview()` (Asset Production, Stage 3) renders the 64 px thumbnail. | By eye | Not applicable to stand-ins | Check the 64 px preview of every asset before export |
| Looks right on the dark vault floor and under Wick's warm light | `Wick/StylisedLit` (two-step ramp, trilight ambient, warm rim from the mask's green channel). `Wick > Screenshots` renders every dev vault from the Raid scene. | Tool | Stand-ins render correctly in scout, live and lit-overlay screenshots | Run the screenshots, then look on a phone |
| Uses palette colours; danger never shown in flame orange | VFX: `VfxRules` rejects flame-orange danger effects and over-busy particle systems on every effect and every skin's snuff and sparks (`VfxTests`). Theme stand-ins and UI kit use the palette's hex values. | Automatic (VFX), by eye (textures) | Pass: 8 effects and 8 skins | Painted textures are judged by eye; VFX stay automatic |
| Matches Wick's reference proportions (characters and skins) | Import check: height 0.3 to 1.0 m (Wick 0.6 m, flame tip about 0.8 m), pivot at the base centre, every shared bone, all five face shapes. | Automatic (scale, rig), by eye (likeness) | Pass on all 8 stand-in skins | Likeness against `docs/art/references/` is by eye |
| Within the triangle and texture budget | `ArtImportChecks` on every import: triangle budget per category, texture size and power of two, one material (plus the flame), naming. Reported, never fixed. The CI test `EverythingInIncoming_MeetsTheHandoffSpecs` fails the build on any violation. | Automatic | Pass (nothing in Incoming yet) | Automatic: see `Wick > Art > Check Incoming Assets` |
| No resemblance to existing games' characters, logos or icons | Not automatable. | By eye | Not applicable | Review before shipping; keep the prompts free of other games' names |
| Checked on a real low-end phone, not just in the Editor | `PerfProbe`: average and 1%-low fps, peak draw calls, sim ms per tick. It shows in the dev menu and is logged at each raid's end. Replaying the `full-heist` golden is the benchmark. | Tool | Waiting for the low-end phone | Run the benchmark on the low-end phone and record the result here |

## Asset Production pack: acceptance checklist

| Checklist item | How M9 covers it | Kind | Status with stand-ins |
| --- | --- | --- | --- |
| Silhouette reads as a solid shape at 64 px | As above (Blender preview) | By eye | Not applicable |
| Follows The look decoded: bevelled, chunky, painted light, 2 to 4 main colours | Shaders carry the painted-light model; the shapes are judged by eye | By eye | Not applicable |
| Palette colours only; danger never in flame orange | As above | Automatic (VFX), by eye (textures) | Pass |
| Within triangle, texture and material budgets | `ArtImportChecks` and the CI gate | Automatic | Pass |
| Named to convention, pivot at base centre, correct scale | `ArtRules`: `<category>_<name>_<variant>`, folder matches category, texture suffixes, pivot within 2 cm, footprint within a tile | Automatic | Pass (13 tests, including one that imports real model files) |
| Characters bind to the shared rig and play every clip | `SkinTests`: every skin has the shared bones, face shapes and flame. Every curve of every clip binds to every skin. Every reaction plays (idle, dash, run, tiptoe, low health, hit, grab, fear face, escape to victory, snuff). | Automatic | Pass on all 8 skins (3 tests each) |
| Traps and guardians: `warn` or `alert` readable on its own | Clip names are checked against each family's list; whether the tell reads is judged by eye | Automatic (names), by eye (readability) | Not applicable until trap art |
| Reads clearly inside and at the edge of Wick's light | `ShaderVisibilityTests` render every golden raid and check each tile's pixel is lit exactly where the sim says. `ThemeTests` render each dev vault in each theme and require floors, walls, doors, pits and ice to stay distinct. | Automatic | Pass: 8 goldens, 2 themes × 3 vaults |
| No resemblance to other games' characters, logos or assets | Not automatable | By eye | Not applicable |
| Generated sources and tool licences noted in the asset's folder | The import report flags any asset folder without a notes file (`.md` or `.txt`) | Automatic | Pass (nothing in Incoming yet) |

## M9 acceptance

| Acceptance item | Result |
| --- | --- |
| All performance budgets met on the low-end device | Measurement is built (`PerfProbe`). The run waits for the low-end phone. |
| Golden replay hashes unchanged (art never touches Sim) | Unchanged. No file under `Sim/` changed in M9. All golden replays pass in Unity and in .NET (`Wick.Sim.Server.Tests`). `HitStopFreezesTheViews_NeverTheSim` proves a hit-stop leaves the final hash identical. |
| Install size under budget, seasonal content remote | Dev APK: 53 MB of the 150 MB budget (measured on every build, with every skin and theme still inside for testing). Skins, themes and seasonal content are remote Addressables groups for release builds. |
| Side-by-side checklist against the art review checklist | This document |

## What still needs a person

1. Pick the fonts (sample page: https://claude.ai/artifact/9b6rzh9HVTmCAgFoPsbtpe). The kit is wired; fonts drop in once chosen.
2. Supply assets into `Art/Incoming/` in the vertical-slice order from the Asset Production tracker. Then run
   `Wick > Rebuild Placeholder Content`, which swaps real art in wherever it exists.
3. Create the Cloudflare R2 bucket `wick-content` with the custom domain `cdn.uprisestudios.co.uk`, then upload
   release content with `tools/content/upload_r2.py` after `Wick > Art > Build Release Content (CDN)`.
4. Run the benchmark on the low-end phone and fill in the first row of the acceptance table.
