# Wax Bandits — Asset Production Prompt Pack

Oct 19, 2026 · @Jamie

## How this works with M9

This pack makes the assets; M9 integrates them. Every finished asset lands in `unity/Assets/_Wick/Art/Incoming/<category>/<asset_id>/`, where the M9 import checks enforce the budgets.

**Pipeline, per asset**

1. **Concept:** pick a winner from the Art Direction Prompt Pack (use its master style block).
2. **Model sheet:** turn the concept into clean front, side and back orthographic views (Stage 1).
3. **Base mesh:** generate a 3D draft with an image-to-3D tool, or block it out by hand (Stage 2).
4. **Cleanup:** retopologise, bevel, UV and scale in Blender, driven by Claude Code scripts (Stage 3).
5. **Texture:** hand-painted albedo with baked lighting gradients (Stage 4).
6. **Rig and animate:** shared rig for Wick and skins; simple rigs for traps and guardians (Stage 5).
7. **Export:** FBX plus textures into the Incoming folder, named to convention.
8. **Look check in Unity:** under the stylised shaders (Stage 6), on a real phone.

**Tools**

| Job | Suggested tool | Notes |
| --- | --- | --- |
| Concepts and model sheets | An image generator that supports reference images | Use the locked Wick reference every time |
| Base meshes | An image-to-3D generator | Drafts only; never ship raw output |
| Modelling, UVs, rigging, animation | Blender 4.x | Free; scriptable in Python, so Claude Code can automate it |
| Texturing | Blender texture paint, or a 3D painting app | Hand-painted look; no photo textures |
| VFX textures, UI | An image editor plus an image generator for drafts | Final UI as clean vector or 9-sliced sprites |
| Audio | An AI sound or music tool for drafts, a sound library for staples | Check each tool's commercial licence terms |

**Naming**: `<category>_<name>_<variant>`, lower case, for example `char_wick_default`, `trap_drip_pipe`, `tile_treasury_wall_corner`, `vfx_flame_core`. Textures add a suffix: `_albedo`, `_mask`, `_emissive`.

**Keep it ours**: prompts describe the qualities we want and never name existing games, studios or characters. Don't feed other games' screenshots or assets into any tool as references. Our own Wick reference sheet is the only character reference.

## The look decoded

The chunky mobile-strategy look comes from a handful of repeatable rules, not from detail. Apply these to every asset and the style holds together.

| Rule | What it means in practice |
| --- | --- |
| Big, simple forms | Readable from the gameplay camera at phone size; detail lives in the silhouette, not in small parts |
| Exaggerated proportions | Characters 2 to 3 heads tall; hands, feet and heads oversized; props about 1.3x real-world chunkiness |
| Everything bevelled | No hard 90° edges; bevel width about 4–8% of the object's smallest dimension |
| Painted-in light | Albedo carries a soft top-down gradient (lighter top, darker base) and painted ambient occlusion in creases |
| Broad, simple highlights | Metal and gold get one or two clean painted highlight bands, not noisy reflections |
| Saturated, limited palette | Colours from the style bible; each object uses 2 to 4 main colours |
| Warm rim light | A warm edge light separates characters from the dark vault floor |
| Low noise | Texture detail at large scale only; no photo grain, no fine noise |
| Readable at 64 px | If the silhouette fails as a solid shape at 64 px, simplify it |

**Numbers to work to**

| Spec | Target |
| --- | --- |
| Unit scale | 1 Blender unit = 1 m; one floor tile = 1 m x 1 m |
| Wick height | 0.6 m (flame tip at about 0.8 m) |
| Texel density | About 256 px per metre for props and tiles; 512 px per metre for characters |
| Character triangles | 3,000 to 5,000 |
| Trap and guardian triangles | 1,500 to 4,000 |
| Tile piece triangles | 200 to 800 |
| Texture sizes | Characters 1024 px atlas; traps and guardians 512 px; tile kits 1024 px per theme |
| Materials per asset | One (atlas everything) |

## Stage 1: Model sheets

Image-to-3D tools and modellers both work best from clean, flat orthographic views with even lighting. Generate the model sheet from your chosen concept, with the concept attached as a reference image.

**Character model sheet**

```text
[MASTER STYLE BLOCK]
Orthographic character model sheet of [CHARACTER, e.g. Wick the tiny living
candle thief], matching the reference image exactly. Four views in a row:
front, left side, back, three-quarter. Neutral A-pose with arms slightly away
from the body, feet flat, flame upright. Flat even studio lighting, no cast
shadows, no rim light, plain light grey background, same scale and height in
every view, centred, nothing cropped.
```

**Prop or trap model sheet**

```text
[MASTER STYLE BLOCK]
Orthographic prop sheet of [TRAP OR PROP], chunky bevelled stylised game prop.
Front, side, top and three-quarter views, same scale, flat even lighting, no
shadows, plain light grey background, centred, nothing cropped. Fits a 1 x 1
metre floor tile footprint.
```

**Single clean view for image-to-3D**

Most image-to-3D tools want one clear image. Use this, then feed the result in.

```text
[MASTER STYLE BLOCK]
Single three-quarter front view of [ASSET], whole object visible, isolated on
a plain white background, soft even lighting, no shadow on the ground, no
motion blur, no other objects, crisp edges, matching the reference image.
```

Checks before moving on: every view has the same proportions; nothing important is hidden; the flame and eyes sit in the same place as on the locked Wick reference.

## Stage 2: Base mesh

An image-to-3D draft saves hours on blocking out, but its topology, UVs and textures are never shippable. Treat it as clay to reshape in Stage 3.

**Settings to ask for** (names vary by tool)

- Stylised or cartoon mode rather than realistic.
- Low or medium polygon target, quad remesh if offered.
- Symmetry on for characters and most props.
- Texture generation on, but only as a colour guide for repainting.
- Export as GLB or FBX.

**Text prompt for tools that take one alongside the image**

```text
Stylised chunky cartoon game asset, soft rounded bevelled forms, simple clean
shapes, smooth surfaces, no fine detail, toy-like proportions, hand-painted
look, [ASSET NAME AND ONE-LINE DESCRIPTION].
```

**What to fix every time**

| Problem | Fix in Stage 3 |
| --- | --- |
| Dense, messy triangles | Retopologise or decimate to budget |
| Lumpy or melted surfaces | Smooth, then reshape key forms by hand |
| Missing or fused parts (arms merged to body) | Rebuild those parts as simple primitives |
| Broken or overlapping UVs | Re-unwrap |
| Baked-in lighting or noise in textures | Repaint (Stage 4) |
| Wrong scale or pivot | Normalise with the scale script |

**When to skip AI and block out by hand**: tile kit pieces, simple traps (pipes, plates, spikes) and anything modular. Primitives plus bevels are faster and cleaner, and Claude Code can generate them by script.

## Stage 3: Blender cleanup with Claude Code

Claude Code can write Blender Python scripts and run them headless (`blender -b file.blend --python script.py`). It can't see the viewport, so every script renders preview images for you to check. Keep these scripts in `tools/blender/` in the Wick repo.

**Set up the toolkit (run once)**

```text
Create tools/blender/, a set of Blender 4.x Python scripts for the Wick asset
pipeline, runnable headless. Read docs/asset-production.md (The look decoded)
for scale, triangle budgets and texel density.

1. common.py: unit setup (metric, 1 unit = 1 m), logging, and a render_preview()
   that renders front, side, three-quarter and top views with flat lighting and
   a 64 px silhouette thumbnail to <asset>/previews/.
2. normalise.py <in.glb|fbx> <asset_id> <category>: import, apply transforms,
   scale to category height (char = 0.6 m, tile = 1 m footprint), put the pivot
   at the base centre, face +Y forward, rename to convention, save .blend.
3. budget_report.py <file.blend>: triangle count, material count, UV islands,
   overlapping UVs, texel density, non-manifold edges, compared to the budget
   table. Exit non-zero on failure.
4. stylise.py <file.blend>: decimate to budget, weighted normals, bevel
   modifier sized to 4-8% of the smallest dimension, shade smooth with auto
   smooth, apply.
5. unwrap.py <file.blend>: smart UV project with margin for 1024/512 px, pack
   islands, scale to texel density.
6. export_fbx.py <file.blend>: export to unity/Assets/_Wick/Art/Incoming/
   <category>/<asset_id>/ with Unity-friendly axes, no cameras or lights,
   embedded animations if present, and textures copied beside it.
7. tile_kit.py <theme>: generate the modular tile kit procedurally from bevelled
   primitives (floor, wall straight, wall corner, door frame, pit edge, ice
   floor), each exactly 1 m, snapping cleanly, with UVs on one theme atlas.

Acceptance: run each on the sample mesh I provide in tools/blender/samples/,
show me the preview renders and the budget report, and document usage in
tools/blender/README.md.
```

**Per-asset cleanup**

```text
Using tools/blender, process the draft mesh at <PATH> as <ASSET_ID>
(<CATEGORY>). Run normalise, stylise, unwrap and budget_report, then render
previews. If any budget fails, propose the change (more decimation, removing a
part, merging pieces) and wait for me. Note any parts that look fused or melted
from the image-to-3D draft so I can fix them by hand.
```

**Things worth fixing by hand in the viewport**: face and eye placement on characters, the flame socket position, clean separation of arms and feet for rigging, and any silhouette the 64 px thumbnail shows as a blob.

## Stage 4: Hand-painted texturing

The style lives in the albedo: lighting is painted in, so the game's real lights only add warmth and the rim. Build each texture in the same five layers, in this order.

1. **Flat base colours** from the palette, 2 to 4 per object.
2. **Top-down gradient:** about 15% lighter at the top, 20% darker at the base.
3. **Painted ambient occlusion:** soft darkening in creases and where parts meet (bake AO in Blender, multiply at 40–60%, then soften).
4. **Edge highlights:** a thin, lighter band on bevelled edges (bake a curvature or edge mask and paint over it).
5. **Accent detail:** large painted wood grain, stone chips or wax drips, and one or two clean highlight bands on metal and gold.

**Bake helper (Claude Code)**

```text
Add tools/blender/bake_maps.py <file.blend>: bake ambient occlusion and an
edge/curvature mask at the asset's texture size into <asset>/textures/, plus a
flat-colour ID map from material slots. Then build a starter albedo by
compositing: base colours from the ID map, the top-down gradient, AO multiplied
at 50%, and edges lightened 20%. Save it as <asset_id>_albedo.png for me to
paint over. Also output a mask texture (R = emissive, G = rim strength,
B = spare) for the Unity shader.
```

**Draft texture prompt** (image generator, for colour ideas only)

```text
Hand-painted stylised game texture for [MATERIAL: rounded stone bricks / warm
wooden planks / creamy candle wax / polished brass / gold coins], soft painted
gradients, large simple shapes, low noise, painted highlights on edges, gentle
ambient occlusion in the cracks, tileable, saturated but soft, no photographic
detail, top-down flat view.
```

**Material notes**

| Material | Painting notes |
| --- | --- |
| Wax (Wick) | Creamy ivory, warmer and slightly translucent near the flame (paint a warm glow band at the top), soft drips with highlight tips |
| Stone | Cool blue-grey, large rounded bricks, chipped edges lightened, moss accents sparingly |
| Wood | Warm orange-brown, big painted grain, dark gaps between planks |
| Metal and brass | Two-tone with one broad highlight band; no noise |
| Gold | Saturated yellow, white-hot highlight spots, warm orange shadows |
| Fabric (satchel, mask) | Flat colour, soft folds, stitched edges suggested with a few strokes |

Working textures go in `<asset>/textures/` as layered source files; export flattened PNGs at final size for Unity.

## Stage 5: Rigging and animation

One shared rig for Wick and every skin means one set of animations works for all of them. Animation carries the personality, so exaggerate: strong squash and stretch, anticipation before every move, overshoot after it.

**Wick rig (shared by all skins)**

| Bone | Purpose |
| --- | --- |
| `root` | Ground position; never animated up and down |
| `body` | Squash and stretch (scale Y with volume-preserving X/Z) |
| `head` | Look direction, nods, leans |
| `arm.L` / `arm.R` (2 bones each) | Gestures, carrying the satchel |
| `foot.L` / `foot.R` | Tiptoe, steps, hops |
| `flame_base`, `flame_mid`, `flame_tip` | Flame sway; scale driven by flickers at runtime |
| `eye.L` / `eye.R` | Eye widening and darting |

Face blend shapes: `fear`, `greed`, `smug`, `wince`, `blink`.

**Rig script (Claude Code)**

```text
Add tools/blender/rig_wick.py <file.blend>: build the Wick rig exactly as in
the rig table, named as listed, with automatic weights then cleaned (body and
head weights smooth, arms and feet rigid). Add the face blend shapes as empty
shape keys I will sculpt. Validate that any skin mesh can bind to the same
skeleton: same bone names, rest pose and scale. Render a pose test (squash,
stretch, lean, arms up) to previews/.
```

**Wick animation list** (30 fps)

| Clip | Frames | Loop | Notes |
| --- | --- | --- | --- |
| `idle` | 60 | Yes | Gentle bob, flame sway, occasional peek |
| `idle_fidget_pat` | 45 | No | Pats his flame flat, it springs back |
| `idle_fidget_count` | 60 | No | Counts loot from the satchel |
| `dash` | 8 | No | Anticipation squash, stretch forward, land squash |
| `run` | 16 | Yes | Fast little legs, lean forward, flame trailing |
| `tiptoe` | 24 | Yes | High knees, arms tucked, eyes darting |
| `hit` | 12 | No | Wince, body squash, flame shrinks |
| `low_health` | 20 | Yes | Trembling, sweat, frantic flame flicker |
| `grab` | 18 | No | Lunge, hug the gold, big grin |
| `escape` | 24 | No | Smug hop, satchel jingle |
| `snuff` | 30 | No | Flame out, eyes dazed, smoke puff, wobble down |
| `victory` | 48 | No | For results screens |

Keep `dash` and `run` in step with the sim's movement speed so feet don't slide: ask Claude Code to read the speeds from `SimTuning` and print the matching stride length.

**Traps and guardians**

- Traps: simple bone or object animation per sim event, named `idle`, `warn`, `fire`, `reset`. The `warn` clip must be readable on its own, because that's the player's tell.
- Guardians: `idle`, `patrol`, `alert`, `chase`, `sleep`, `wake`, `hit_player`. Big, slow anticipation so players can react.

## Stage 6: Unity look shaders

Painted textures need a shader that doesn't fight them. Standard realistic shading flattens the painted light and adds unwanted specular, so build two small URP shaders and use them on every asset.

```text
Read CLAUDE.md and docs/asset-production.md (The look decoded, Stage 4). Write
URP Shader Graph or HLSL shaders in unity/Assets/_Wick/Art/Shaders/ for mobile:

1. Wick/StylisedLit (characters, traps, guardians, props, tiles):
   - Albedo carries painted lighting; main light adds a soft two-step ramp
     (lit / shadow) with a configurable softness, never full PBR.
   - Ambient from a gradient (vault blue-violet ground, slightly lighter sky).
   - Rim light in warm Candle Flame orange, strength from the mask texture G
     channel, stronger near Wick's point light.
   - Optional emissive from mask R (flames, glowing eyes, warnings).
   - Receives the visibility mask from M3 (tiles outside Wick's light darken),
     reading the same lit-tile data Sim produces, never computing its own.
   - No specular, no normal maps by default (optional toggle), SRP Batcher
     compatible, one pass plus shadow caster.
2. Wick/Flame (Wick's flame, braziers):
   - Vertex sway from a noise texture and time, scaled by a _Flickers property
     (3 = calm, 1 = frantic), additive core in Flame Core yellow over Candle
     Flame orange, soft edge.
3. A material preset per category and a LookCheck scene: dark vault floor,
   Wick's point light, one of each asset type, the gameplay camera, and a
   64 px silhouette render toggle.

Acceptance: LookCheck runs at 60 fps on the low-end Android device; shader
variants kept minimal (list them); screenshots of the LookCheck scene for me to
compare against the concept art.
```

## Asset briefs

Each brief is the job card for one asset family: what to make, the key shapes that sell it, and what to watch for. Concept prompts for all of these are in the Art Direction Prompt Pack.

**Wick and skins**

Locked references (full feature lists are in the Art Direction pack). Model from these, not from text. In the repo they are \`docs/art/references/\<name>\_reference\_v1.png\` for wick, drippy, cinder, pip, twist and ember.

&#91;image: Wick reference v1\]

&#91;image: Drippy reference v1\]

&#91;image: Cinder reference v1\]

&#91;image: Pip reference v1\]

&#91;image: Twist reference v1\]

&#91;image: Ember reference v1\]

**Turning the renders into game models**

- Both images are glossy hero renders. In-game wax is hand-painted: keep a soft warm glow near the flame but drop the wet shine.
- Reduce Wick's drips to 4 to 6 chunky ones that change the silhouette; paint the rest. Drippy keeps more and bigger drips, plus the foot puddle as a separate mesh.
- Build the flame as its own mesh using `Wick/Flame`, keeping the swept quiff shape from the reference.
- Strap, buckle and satchel are separate mesh parts on the shared rig (the satchel gets one jiggle bone), so all skins can use them or swap them.
- Eyes: large spheres with painted irises and two painted highlights; brows as separate meshes or blend shapes so expressions read clearly.

**Rig notes for the new skins**

- **Pip** is much shorter. Keep the shared skeleton, but rest his body bones lower in the cup and keep the eye line within the shared range. His oversized flame gets extra flame bones or a longer `flame_tip` so it can whip around. Check the light radius still matches the sim, because the flame's size is cosmetic.
- **Twist**'s spiral is a modelled shape, not a deformation. Skin it so squash and stretch compresses the spiral evenly rather than unwinding it.
- **Cinder**'s mask ties are two short dangle bones off `head`, for secondary motion when he dashes.
- **Hitbox rule**: every skin uses the same sim footprint. Pip's tiny body and Twist's lean are visual only.

* **Ember** is a squat, wider stump: rest the body bones lower, like Pip's, and model the chipped lower edge into the mesh. Note his flame is large but not as extreme as Pip's.

- Build `char_wick_default` first and fully: model, texture, rig, all 12 clips, in-engine look check. Every skin copies its proportions.
- Skins share the skeleton and animations. Only mesh, textures, flame colour and snuff effect change.
- Silhouette markers per skin: Drippy (leaning, heavy drips and foot puddle, plus one differentiator still to choose), Cinder (eye mask with trailing ties), Pip (silver tealight cup and a flame twice his height), Twist (spiral body and ribbon flame), Ember (short chipped stump, oversized flame).
- Keep the flame socket and eye line at the same height on all skins so gameplay reads the same.

**Traps** (`trap_*`)

| Asset | Key shapes | Watch for |
| --- | --- | --- |
| Drip Pipe | Fat brass pipe, valve wheel, droplet, puddle decal | Droplet must read clearly at `warn` |
| Bellows | Wood frame, leather pleats, painted face on nozzle | Gust VFX lines up with the row it hits |
| Spike Floor | Tile with rounded chunky spikes | Red glow in cracks during `warn` |
| Snuffer Bell | Huge brass bell on a chain, raised plate below | Plate slightly visible: it's the tell |
| Ice Tiles | Glossy pale-teal tile with frosted edge | Obviously different from normal floor |
| Swing Axe | Big rounded blade, pendulum chain | Arc decal on the floor shows the danger zone |
| Fan Maze | Chunky wooden fan on a stone base | Wind swirls show direction |
| Lever Doors | Lever post plus a matching coloured door | Lever and door share a colour gem |

**Guardians** (`guard_*`)

| Asset | Key shapes | Watch for |
| --- | --- | --- |
| Night Owl | Round fluffy body, tiny helmet, huge lantern eyes | Sight cone visible on the floor when alert |
| Wax Moths | Five plump fuzzy moths, glowing wings | Read as a single group, not five tiny things |
| Mimic Chest | Gold-filled chest, tongue, rounded teeth, peeking eyes | Subtle breathing loop as its tell |
| Sleepy Troll | Big soft moss-green body, nightcap, snore bubble | Fills a doorway exactly |

**Sir Whiskers** (`guard_sir_whiskers`, signature guardian)

- Key shapes: huge round fluffy body, smug half-closed eyes, long whiskers, plump tail, red velvet cushion with gold tassels. The cushion is a separate prop so it stays put when he moves.
- Budget: up to 5,000 triangles (the top of the guardian range), since he's a showpiece. Fur is painted as big clumped shapes, not strands.
- Rig: spine (3), tail (5, for twitching), 4 legs, head, ears (2), whisker blend shape, plus `swat` paw IK.
- Clips: `nap` (breathing loop), `wake`, `stalk` (crouched walk), `lose_interest` (yawn and stretch), `wiggle` (exactly 30 frames, the pounce tell), `pounce`, `swat`, `lick_paw` (smug), `chase_decoy` (batting it around), `return_to_cushion`.
- Readability: the `wiggle` must read on its own at gameplay zoom, because it's the player's only warning. Exaggerate the bottom wiggle and the tail.
- Variants: coat colours (ginger, black, tabby) and seasonal outfits as texture and accessory swaps only.

**Tile kits** (`tile_<theme>_*`): floor, floor variant x2, wall straight, wall corner (inner and outer), door frame and door, pit edge, ice floor, entrance arch. Each exactly 1 m, all on one 1024 px atlas per theme. Generate with `tile_kit.py`, then paint the atlas.

**Props** (`prop_*`): treasure pile (small, medium, large), single gold coin (for the fly-to-counter effect), wall brazier, crate, pillar, banner, torch, decoy wax drip.

**UI** (`ui_*`): button set (primary orange, secondary teal, danger red) as 9-sliced sprites, parchment panel, gold-trim frame, currency icons (gold, wax, gems), star badges, tab bar icons, the candle-shaped progress bar. Build final UI as vector shapes in an editor using the Art Direction UI kit as reference; export 2x and 3x.

**VFX textures** (`vfx_*`): flame core and outer flame, spark, smoke puff, coin sparkle, water splash, gust swirl, warning glow, all in one 512 px atlas. Few, large, hand-shaped particles.

## Audio assets

Sound sells the comedy: every snuff, near miss and gold grab needs a distinct, short, cartoony sound. Keep music light and playful, dropping into tension only when danger is close.

**Music prompts** (AI music tool)

```text
Playful sneaky heist music for a cartoon mobile game, pizzicato strings,
plucked bass, light woodwinds and soft percussion, tiptoe rhythm, cheeky and
mischievous, warm and family friendly, [MOOD: calm exploring / tense with a
slow heartbeat pulse / triumphant escape], [LENGTH] seconds, seamless loop,
no vocals.
```

Three layers that crossfade: calm, tense (adds a heartbeat and low strings) and escape (faster tempo). Same key and tempo so they blend.

**SFX prompts** (AI sound tool)

```text
Cartoon game sound effect: [SOUND], short, punchy, exaggerated, clean with no
background noise, playful, mobile game style, [LENGTH] seconds.
```

| Sound | `[SOUND]` |
| --- | --- |
| Wick footsteps | tiny soft waxy pitter-patter steps |
| Tiptoe | very quiet high-pitched creeping steps |
| Dash | quick whoosh with a little squeak |
| Flame flare | warm flame whoomph with sparkle |
| Flicker lost | flame sizzle with a small whimper |
| Snuff | comic fwump, smoke puff, dazed cartoon wobble |
| Gold grab | bright coins clinking, sparkle chime |
| Escape | triumphant little fanfare sting |
| Drip | fat water drop plop then hiss |
| Bellows | big comic puff of air |
| Spike warning | low stone rumble |
| Snuffer Bell | heavy brass bell clang |
| Owl alert | grumpy owl hoot with a startled ruffle |
| Moths | soft fuzzy flutter buzz |
| Troll snore | deep rumbling snore |
| UI tap | soft wooden button click |

Before using any generated audio commercially, check the tool's licence terms. Normalise loudness across all SFX and export as compressed mono for mobile (stereo for music).

## Acceptance checklist and tracker

An asset is done only when it passes every check below in the LookCheck scene and on a real low-end phone.

- [ ] Silhouette reads as a solid shape at 64 px.
- [ ] Follows The look decoded: bevelled, chunky, painted light, 2 to 4 main colours.
- [ ] Palette colours only; danger never in flame orange.
- [ ] Within triangle, texture and material budgets (`budget_report.py` passes).
- [ ] Named to convention, pivot at base centre, correct scale.
- [ ] Characters: bind to the shared rig and play every clip without breaking.
- [ ] Traps and guardians: `warn` or `alert` readable on its own.
- [ ] Reads clearly inside and at the edge of Wick's light.
- [ ] No resemblance to other games' characters, logos or assets.
- [ ] Generated sources and tool licences noted in the asset's folder.

**Vertical-slice asset tracker** (build in this order)

| Asset ID | Type | Stage |
| --- | --- | --- |
| `char_wick_default` | Character | Concept |
| `tile_treasury_*` | Tile kit | Concept |
| `prop_treasure_pile` | Prop | Concept |
| `prop_brazier` | Prop | Concept |
| `trap_drip_pipe` | Trap | Concept |
| `trap_bellows` | Trap | Concept |
| `trap_spike_floor` | Trap | Concept |
| `trap_snuffer_bell` | Trap | Concept |
| `guard_night_owl` | Guardian | Concept |
| `guard_wax_moths` | Guardian | Concept |
| `vfx_atlas` | VFX | Concept |
| `ui_kit` | UI | Concept |
| `char_drippy` | Skin | Concept |
| `audio_slice_set` | Audio | Concept |

Add rows for the M10 content and remaining skins as they start.
