# Wax Bandits — M10.2 Lighting Lock Spec & Prompt Pack

Oct 25, 2026 · @Jamie

## Overview

M10.2 locks how vaults are lit, with real numbers, a test scene and signed-off screenshots, so final art is painted against the lighting it will actually sit in. Textures painted before the lighting is fixed either look flat or blow out once the real lights arrive, so this runs now, straight after M10.1 and before any final textures are approved.

The look is the one in `docs/art/references/sheets/sheet_lighting_vfx_v1.png`: a warm candle pool against cool, dark vaults, with soft bloom, light shafts, dust motes and low fog. The rule from M3 still holds: **Wick.Sim decides what is lit; the renderer only shows it.**

Three prompts, each ending with screenshots for sign-off:

1. **M10.2a Lighting stack and visibility mask:** the core layers, Wick's light, darkness levels.
2. **M10.2b Theme profiles, atmosphere and post:** per-theme values, fog, shafts, bloom, grading.
3. **M10.2c Lighting lab, captures and sign-off:** a test scene, a screenshot tool and device checks.

**Order (25 October 2026):** M10.2 runs after M10.1b and before M10.1c (pit presentation), so the pit visuals are made against the locked lighting. "M10.1 must be merged" below means M10.1a and M10.1b. The pit edge glow is built here, in 10.2a item 4. Sim light radii today are 4 / 3 / 2 tiles (SimTuning.LightRadius*), not the table's 3.0 / 2.5 / 2.0: 10.2a builds against the current values, and any change chosen in the lab is a separate SimVersion bump.

**Decisions (25 October 2026, M10.2a plan):**
- The scout flyover's view is temporary: when the clock starts every tile is Unseen, and tiles become Remembered only once Wick's own light has reached them. (This replaces "except areas shown in the scout flyover" in the darkness table.)
- Vaults are assembled at runtime from tiles, so there are no lightmaps. Static warm pools come from a per-tile static-light texture built when a vault loads, from wall sconces, players' torch decorations and the entrance.
- Sconces join the tile kit now, placed automatically along walls as ambient lighting. Their pools show faintly through the dark (a profile value), lighting walls and floor only: traps, guardians and props are still revealed only by Wick's light.

**Decisions (27 October 2026, M10.2b plan):**
- The scout flyover shows the outer rooms' layout only, at Remembered brightness: traps, guardians and props stay hidden until Wick's light reaches them (presentation only; the sim is unchanged).
- Frame rate: aim for 60 fps on the low-end Android at its auto tier as well as mid-range; 30 fps stays the hard floor (tech-design.md updated to match).
- No low-end phone yet: the per-tier device benchmark is built ready to run; the measured sign-off waits for the phone.
- There are no hand-placed vault markers: atmosphere (fog, shafts, dust) is placed from the layout, like the sconces, one room at a time (rooms are floor areas split at doors).

**M10.2b as built (27 October 2026):**
- Profiles: Classic Treasury (`Art/Themes/lighting_treasury.asset`), Crypt and a Sunken Ship placeholder (`Art/Lighting/`); Sunken Ship has no tile kit yet. Profiles are created once and tuned in place; rebuilds only fill in missing LUTs.
- LUTs: `Wick > Art > Build Lighting` bakes warm interior, cool vault depth, mixed lighting, dramatic contrast, victory, defeat and neutral strips (`Art/Lighting/Luts`, maths in `LutGrades`). Results blend in victory or defeat over the theme's grade.
- Atmosphere is placed from the layout (`Atmosphere`): fog in pits then far corners, shafts from a grate in each room's back wall (with a little dust), warm motes in Wick's light. Fog and shafts are dimmed by the mask like the room.
- Contact shadows (`Wick/ShadowBlob`) under Wick, guardians, traps and floor props; they hide with their owner.
- Quality tiers (`LightingQuality`): a 2-second benchmark at Home on first launch picks the tier; Settings > Lighting quality changes it (it replaced "Glow on flames"). Low: per-tile mask edge, no bloom (emissives brightened), no fog, shafts or dust.
- Screens: Home has no mask; the scout shows the outer rooms' layout at Remembered brightness, defences hidden; the editor's "Dark" button previews the start of a raid; clips lift exposure by `ClipExposureLift`.
- Device benchmark: dev menu > Lighting benchmark replays full-heist at each tier and a baseline (Low, no post, no atmosphere) and reports frame times and each tier's lighting+post cost. Whole-frame times (frame timing stats are off in Player Settings).
- Screenshots: `CaptureLightingOnly` adds the tiers side by side (`sheet-tiers`, with the sim's lit tiles overlaid in `sheet-tiers-overlay`), the screens (`sheet-screens`) and the editor preview (`sheet-editor`).
- Still open: the 60 fps check and the per-tier frame times on the low-end Android (no phone yet).

**M10.2a as built (25 October 2026):**
- `LightingProfile` (Game/Rendering) holds every value in the tables below; Classic Treasury's is `Art/Themes/lighting_treasury.asset`, created once by ThemeBuilder and then tuned in place. Crypt uses the same values until M10.2b. The visibility and flame fields left `RaidTuning`.
- Mask: one RG8 texel per tile, R = lit now, G = seen; Remembered is `RaidRunner.EverLit`, built on every simulated tick (a replay seek included). Tile centres always show their own state; floors blend across a half-tile band at edges, and wall faces read their own tile exactly.
- Wick's light: range = sim radius + 0.5 tile, intensity 2.0, ±5% flicker at 6 Hz, ±15% faster at 1 flicker, +40% flare for 0.4 s (gold and braziers), 0.25 s ease on tiptoe and gusts, 0.3 s fade on a snuff.
- Static pools fade with height (torchlight on the floor), so wall tops near a sconce stay dark.
- Always-readable glows are `ReadableGlow` parts that views don't hide. Mimic Chest's peeking eyes show only while it winds up to bite, so the dark never gives its disguise away; Sir Whiskers' eyes show while he's awake; the owl's always.
- Screenshots: `Wick > Capture Dev Screenshots` (or `-executeMethod Wick.Editor.WickScreenshots.CaptureLightingOnly`) writes the tile-state and flame-mood shots to `unity/Builds/Screenshots/lighting`.

This supersedes the lighting parts of M3 item 4, M9 item 5 and Asset Production Stage 6. Those still apply for shaders, but the values and rules here win.

## The lighting stack

Eight layers, from back to front. Only one of them is a realtime light; everything else is baked, painted, or a cheap shader effect, which is what keeps it fast on low-end phones.

| # | Layer | What it does | How it's made |
| --- | --- | --- | --- |
| 1 | Ambient | The cool base colour of the vault: deep blue-violet shadows | URP gradient ambient (sky, equator, ground) from the theme profile |
| 2 | Baked lighting | Static warm pools from wall torches and sconces, plus soft ambient occlusion | Lightmaps per vault theme kit, or vertex lighting on tiles; never realtime |
| 3 | Painted light | Top-down gradient and painted AO already in every albedo | Textures, per the Asset Production pack (Stage 4) |
| 4 | Wick's flame light | The warm pool that follows Wick; the star of the scene | One realtime point light, no realtime shadows, range driven by the sim light radius |
| 5 | Visibility mask | Darkens and desaturates every tile Wick can't currently light | Full-screen or per-material shader reading Wick.Sim's lit-tile set each tick |
| 6 | Rim and emissive | Warm rim on characters; glows for flames, eyes, gold, warnings and pit edges | `Wick/StylisedLit` mask texture (R emissive, G rim) plus `Wick/Flame` |
| 7 | Atmosphere | Depth and mood: dust motes, light shafts, low fog | Particle and card effects, a few per room, from the theme profile |
| 8 | Post-processing | Gentle glow, darker edges, final colour | URP bloom (low quality), vignette and a colour-grading LUT per theme |

Braziers can go out mid-raid, so they can't be baked, and they don't use realtime lights either. They use a **light blob**: a soft additive circle projected on the floor plus an emissive flame, switched on and off by sim events. It looks like light but costs almost nothing.

## Light and darkness rules

What the player can see is gameplay information, so these rules are fixed in code, not left to artists' taste.

**Wick's light radius** comes from the sim (in tiles) and the renderer matches it exactly:

| Wick's state | Sim light radius | Renderer |
| --- | --- | --- |
| 3 flickers | 3.0 tiles | Full warm pool, calm flame |
| 2 flickers | 2.5 tiles | Slightly smaller pool |
| 1 flicker | 2.0 tiles | Small pool, frantic flicker in intensity (±15%) |
| Tiptoe (any flickers) | Half the radius above | Pool shrinks over 0.25 s and the flame dims |
| Bellows gust hit | Temporarily reduced, per SimTuning | Pool gutters, then recovers |

These radius values are proposals to tune in the lighting lab; whatever is chosen, the sim and the renderer read the same number from SimTuning.

**Three levels of darkness**

| Tile state | Meaning | Look |
| --- | --- | --- |
| Lit | Inside Wick's current light | Full colour and detail |
| Remembered | Lit earlier in this raid, not now | About 35% brightness, 60% desaturated, cool tint |
| Unseen | Never lit this raid (except areas shown in the scout flyover) | About 12% brightness, mostly desaturated: shapes readable, details hidden |

The edge between lit and unlit is a soft falloff of about half a tile, never a hard circle.

**Always readable in the dark** (emissive, ignoring the mask, so the dark never feels unfair):

- Pit edges (faint glow)
- Trap warnings (red or teal glows during `warn`)
- Guardian eyes (Night Owl's lantern eyes, the Mimic Chest's peeking eyes, Sir Whiskers' eyes when awake)
- Lit braziers and their light blobs
- The entrance (a gentle "way out" glow)
- Treasure piles (a soft gold twinkle, so the goal is always findable)

Everything else in the dark (inactive traps, sleeping guardians' bodies, decor) is only revealed by Wick's light.

## Light sources

Starting values for the lighting lab, using the style bible palette. Intensities assume URP's linear colour space; tune them in the lab, then record the final numbers here.

| Source | Type | Colour | Starting values | Behaviour |
| --- | --- | --- | --- | --- |
| Wick's flame | Realtime point light, no shadows | Candle Flame `#FF9F1C`, core tint Flame Core `#FFE29A` | Range = sim radius + 0.5 tile; intensity 2.0 | Gentle flicker (±5%, about 6 Hz); flare +40% for 0.4 s on grabbing gold; frantic ±15% at 1 flicker; snuffs to 0 over 0.3 s |
| Wick's rim | Shader | Candle Flame | Rim strength 0.6 on characters near Wick | Strongest on the side facing his flame |
| Wall torches and sconces | Baked | Warm orange, slightly redder than Wick | Small pools, about 1.5 tiles | Static; small emissive flicker on the flame only |
| Braziers | Light blob plus emissive | Warm orange | Blob radius 1.5 tiles, 60% opacity | Lit until used; on use, blob fades out over 0.5 s and a smoke curl starts |
| Main light | Directional, very dim | Cool blue `#5B6B9A` | Intensity 0.15, from high above | Only gives forms a little shape in the dark; no shadows |
| Treasure | Emissive plus sparkle particles | Treasure Gold `#F2C14E` | Low emissive, occasional twinkle | Brighter and richer when lit by Wick |
| Trap warnings | Emissive | Danger Red `#E5484D` or Trap Teal `#3EC6E0` | Pulsing during `warn` only | Never warm orange, so danger never reads as light |
| Pit edges | Emissive | Cool teal-blue, low | Constant faint glow | Ignores the mask |
| Entrance | Emissive plus light blob | Soft warm white | Gentle pulse | Brighter when Wick carries loot ("head home") |
| Guardian eyes | Emissive | Owl: warm yellow; others per design | Brighter when alert | Ignores the mask |

**Hard rules**: Wick's flame is always the brightest, warmest thing on screen; danger is never shown in flame orange; no realtime shadows on mobile (use baked AO and contact-shadow decals under characters and props).

## Theme profiles and other screens

Each vault theme gets one `LightingProfile` asset holding every value below, so a new theme only changes data. Classic Treasury is locked first; the others follow when their tile kits are made.

| Setting | Classic Treasury | Crypt | Sunken Ship |
| --- | --- | --- | --- |
| Ambient sky / equator / ground | `#3A2F6B` / `#2A2550` / `#1E1B3A` | Purple-grey, darker ground | Teal-blue, green tint |
| Fog | Low, cool violet, light | Low, pale grey, medium | Low, teal, heavier |
| Light shafts | From high grates, cool white | Few, pale moonlight | Many, wavy teal from portholes |
| Dust motes | Warm in Wick's light | Grey ash | Small bubbles instead |
| Colour grade (LUT) | Warm interior (main) | Cool vault depth | Cool, slightly green |
| Accent light | Red crown banners catch the warm light | Purple candle emissives | Teal water caustics on walls |

**Lighting outside raids**

| Screen | Lighting |
| --- | --- |
| Home | Fully lit and cosy: no visibility mask, every torch lit, warm colour grade, hoard glowing |
| Editor | Fully lit, neutral and clear so builders can see every tile; a "Preview darkness" toggle shows the vault as raiders will see it |
| Scout flyover | The outer rooms shown at Remembered brightness, so the player sees the layout but not the details |
| Replays and clips | Same as the raid; clips may lift exposure by about 10% so they read on social feeds |
| Results | Warm "victory" grade on escape, cooler and darker on a snuff |

## Mobile performance budget

The whole lighting stack must hold 60 fps on the low-end Android test device. Three quality tiers are picked automatically from device performance and can be changed in Settings.

| Budget | High | Medium | Low |
| --- | --- | --- | --- |
| Realtime lights | 1 (Wick) | 1 (Wick) | 1 (Wick), simplified |
| Visibility mask | Per-pixel, soft edge | Per-pixel, soft edge | Per-tile (vertex colour) |
| Bloom | On, low quality | On, half resolution | Off (emissives brightened instead) |
| Fog cards | On | On | Off |
| Light shafts | On | Fewer | Off |
| Dust motes | Full | Half | Off |
| Colour grading LUT | On | On | On |
| Target frame time for lighting and post | Under 3 ms | Under 3 ms | Under 2 ms |

Lighting must never change gameplay between tiers: the lit-tile set, the radius and every always-readable glow are identical on all three.

## Decisions needed

All four are built as settings, so the prompts can run before you decide and you choose in the lighting lab.

- [ ] **Remembered tiles.** Should tiles Wick has already lit stay faintly visible (my recommendation: yes, at about 35%), or go fully dark again when he moves on? Remembering makes vaults feel less frustrating and helps players learn routes; full darkness is tenser.
- [ ] **How dark is unseen?** Direction from Jamie (25 October 2026): unlit areas get faint light, never pitch black (today's raids use 4%, `RaidTuning.UnlitBrightness`). The exact level is picked in the lab. Recommended 12% brightness, so wall shapes read but traps don't. Lower is moodier but risks looking broken on dim screens.
- [ ] **Light radius values.** Proposed 3.0 / 2.5 / 2.0 tiles for 3 / 2 / 1 flickers, halved when tiptoeing. These affect guardian sight too, so confirm after playtesting in the lab.
- [ ] **Treasure twinkle in the dark.** Recommended on, so the goal is always findable. Off makes the hoard a true discovery, but new players may wander.

## What the final artists need

Once M10.2 is signed off, hand every artist this list with the LightingLab scene and the sign-off screenshots.

- **Paint for this light.** Albedos keep their painted top-down gradient and AO, but no baked-in warm or cool colour cast: the lighting adds those. Paint in neutral to slightly warm tones.
- **Check value, not just colour.** Every asset must read as a silhouette in the Unseen state (12%) and show full detail in the Lit state. Test both in LightingLab before handing over.
- **Mask texture.** Fill the mask texture's emissive (R) channel only for the always-readable items listed above; rim strength (G) is high on characters and guardians, low on tiles and props.
- **No glow on ordinary props.** Only the always-readable list may glow; anything else that glows will read as important and confuse players.
- **Danger colours.** Red and teal are reserved for warnings, water and ice. Don't use them as decoration near traps.
- **Gold.** Paint gold rich in its midtones with small, sharp highlights; bloom does the rest, so don't paint glow into the texture.

## M10.2a Lighting stack and visibility mask

Save this pack as `docs/m10-2-lighting.md` in the repo first.

```text
Read CLAUDE.md, docs/m10-2-lighting.md (all sections), docs/tech-design.md
(Client) and docs/asset-production.md (Stage 6). Look at
docs/art/references/sheets/sheet_lighting_vfx_v1.png for the target look.
Plan first. M10.1 must be merged.

1. LightingProfile ScriptableObject holding every value in the spec's Light
   sources and Theme profiles tables (ambient gradient, fog, shafts, dust, LUT,
   accent, light colours and intensities, darkness levels, mask edge softness).
   Create the Classic Treasury profile with the starting values.
2. Wick's flame light: one realtime URP point light, no shadows, range from the
   current sim light radius (read from Sim state each tick, interpolated in
   presentation) + 0.5 tile; flicker, flare, frantic and snuff behaviours
   driven by sim events and flicker count, per the spec.
3. Visibility mask: three tile states (Lit, Remembered, Unseen) from Wick.Sim's
   lit-tile set. Remembered tracking is presentation-only (derived from the
   history of lit tiles, deterministic, never fed back into Sim). Brightness,
   desaturation and tint per state from the profile; soft half-tile edge;
   a toggle in the profile to turn Remembered off.
4. Always-readable emissives ignore the mask: pit edges, trap warn glows,
   guardian eyes, lit braziers and blobs, the entrance glow, treasure twinkle
   (each with its own on/off in the profile).
5. Light blobs for braziers: additive floor decal plus emissive flame, toggled
   by the brazier's lit and snuffed sim events, fading over 0.5 s.
6. Main directional light at the profile's dim cool values; baked or vertex
   lighting for static torches in the Classic Treasury tile kit.
7. Do not change any Sim light radius values. If the lab later changes them,
   that's a separate SimTuning change with a SimVersion bump and golden replay
   review.

Acceptance: the lit-tile debug overlay still matches the mask exactly; Wick's
light radius matches the sim at 3, 2 and 1 flickers and when tiptoeing;
screenshots of each tile state and each flame mood for me to compare with the
reference sheet.
```

## M10.2b Theme profiles, atmosphere and post

```text
Read CLAUDE.md and docs/m10-2-lighting.md (Theme profiles, Mobile performance
budget). Plan first. M10.2a must be merged.

1. Atmosphere driven by the LightingProfile: dust motes (warm inside Wick's
   light), light shaft cards from grates and windows, low fog cards in pits and
   far corners. A few per room, placed by vault markers, not by hand per vault.
2. Post-processing volume per profile: bloom (low quality), vignette, and a
   colour-grading LUT. Create LUTs for warm interior (main), cool vault depth,
   mixed lighting and dramatic contrast, plus a warm victory and a cool defeat
   grade for the results screen.
3. Contact-shadow decals under characters, guardians and props (no realtime
   shadows).
4. Screen modes: Home fully lit with no mask; Editor fully lit and neutral with
   a "Preview darkness" toggle; scout flyover at Remembered brightness; clips
   with an optional +10% exposure lift.
5. Quality tiers High, Medium and Low per the budget table, auto-selected on
   first launch from a quick device benchmark, changeable in Settings. Low uses
   the per-tile mask and brightens emissives in place of bloom.
6. Placeholder Crypt and Sunken Ship profiles with the spec's direction, to
   prove a theme swap only changes data.
7. Profile on the low-end Android device for each tier and report the lighting
   and post frame time.

Acceptance: 60 fps on the low-end device at its auto-selected tier; lighting
and post under the budget; identical lit-tile sets and always-readable glows on
all three tiers (show me a side-by-side).
```

## M10.2c Lighting lab, captures and sign-off

```text
Read CLAUDE.md and docs/m10-2-lighting.md (Decisions needed, What the final
artists need). Plan first. M10.2b must be merged.

1. LightingLab scene: a Classic Treasury test vault containing one of every
   trap, guardian, prop family, treasure pile, brazier, pit and the entrance;
   Wick on a scripted walk through Lit, Remembered and Unseen areas; plus a
   row of grey-value test props (white, mid-grey, dark) to judge brightness.
2. A runtime tuning panel (dev builds only) with sliders for every
   LightingProfile value and for the four open decisions (Remembered on/off and
   level, Unseen level, treasure twinkle). Save the panel's values back into
   the profile asset.
3. A capture tool that renders a fixed set of shots from the gameplay camera
   (each tile state, each flame mood, each trap warn, each guardian alert, home,
   editor, results) to docs/art/lighting-signoff/<date>/, at phone resolution,
   plus a contact sheet image of all of them.
4. Device checks: capture the same set on the low-end Android and an iPhone at
   50% screen brightness, and flag any shot where an always-readable item or an
   active trap isn't clearly visible.
5. Silhouette check: an option that renders the Unseen state only, so every
   asset can be checked for a readable shape in the dark.
6. Write the final values chosen into docs/m10-2-lighting.md (Light sources and
   Theme profiles) and mark the decisions taken.

Acceptance: I can tune the lighting live on a phone, save it, and get a
sign-off contact sheet to compare with the reference sheet and hand to artists.
```

## Sign-off checklist

Lighting is locked when every box is ticked. After that, changes to these values need the contact sheet re-captured and artists told.

- [ ] All four decisions taken in the lab and recorded in this doc.
- [ ] Wick's flame is the brightest, warmest thing in every shot.
- [ ] Lit, Remembered and Unseen are clearly different, and the edge is soft.
- [ ] Every always-readable item shows in the dark on both test phones at 50% brightness.
- [ ] No active trap or warning is ever hidden by darkness.
- [ ] Danger never appears in flame orange.
- [ ] Braziers light and snuff correctly, and the corner darkens when one is used.
- [ ] Home, editor, scout flyover, clips and results each use their own lighting.
- [ ] 60 fps on the low-end Android at its auto tier; identical gameplay visibility on all tiers.
- [ ] Contact sheet saved to `docs/art/lighting-signoff/` and compared with the lighting reference sheet.
- [ ] Artists handed the LightingLab scene, the contact sheet and the "What the final artists need" list.
