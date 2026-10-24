# Wax Bandits — Art Direction Prompt Pack

Oct 18, 2026 · @Jamie

## How to use

This pack keeps every concept, model and screen looking like one game. Every prompt is built from the same master style block, so outputs from different sessions and tools stay consistent.

1. **Lock Wick first.** Generate the turnaround and expression sheet, pick the best result and treat it as the reference image for everything after.
2. **Always prepend the master style block** (or the short version for tools with tight prompt limits) and attach the reference image where the tool supports image or character references.
3. **Fill the brackets.** Prompts use `[BRACKETS]` for the parts you swap; everything else stays fixed.
4. **Generate in batches of 4, pick one, iterate.** Change one thing at a time so you learn what each word does.
5. **Concept art is reference, not final assets.** Final 3D models, textures and UI are rebuilt by an artist or a 3D tool against these concepts.

**Tools these prompts suit:** image generators that accept a style or reference image (for concepts and key art), image-to-3D tools for quick blockouts, and human artists as a written brief.

**Keep it ours:** never name existing games, studios or characters in prompts. Describe the qualities we want instead, so the results stay original and safe to ship.

## Style bible

Chunky, saturated, toy-like 3D cartoon. The signature is **warm candlelight against cool, dark vaults**: the player's light is always the warmest, brightest thing on screen.

**Shapes**

- Oversized heads and hands, short bodies, thick limbs; roughly 2 to 3 heads tall for characters.
- Everything bevelled and rounded: no thin, sharp or realistic edges. Stone blocks look like soft clay bricks.
- Strong, readable silhouettes at 64 px. If it can't be recognised as a black shape, redesign it.
- Exaggerated proportions on danger: traps and guardians are big, heavy and slightly comic.

**Materials**

- Soft hand-painted textures with gentle gradients, minimal noise, no photographic detail.
- Wax is slightly translucent with a creamy subsurface glow near the flame.
- Metal is chunky and polished with broad, simple highlights. Gold is saturated and sparkly.
- Wood is warm, with large painted grain; stone is cool blue-grey.

**Lighting**

- Vaults sit in deep blues and violets with soft ambient occlusion.
- Wick's flame casts a warm orange pool with a soft falloff; rim light on characters in the same warm tone.
- Danger reads in red and teal accents, never in the warm flame colours.

**Palette**

| Role | Name | Hex |
| --- | --- | --- |
| Vault base | Vault Night | `#1E1B3A` |
| Vault shadow | Shadow Violet | `#3A2F6B` |
| Stone | Stone Blue | `#5B6B9A` |
| Flame | Candle Flame | `#FF9F1C` |
| Flame core | Flame Core | `#FFE29A` |
| Wax | Wax Ivory | `#F4E9D8` |
| Treasure | Treasure Gold | `#F2C14E` |
| Danger | Danger Red | `#E5484D` |
| Water and ice | Trap Teal | `#3EC6E0` |
| Guardians and nature | Moss Green | `#6BBF59` |
| UI panels | Parchment | `#F7E7C1` |
| UI outlines | Ink Brown | `#2B1D14` |

**Camera**: fixed 3/4 isometric, about 45 degrees down, slight perspective, tilt-shift-free. Characters always readable against the floor.

## Master style block

Prepend this to every image prompt. Use the short version where prompts are length-limited.

**Full version**

```text
Stylised 3D mobile game art, chunky toy-like cartoon proportions, soft bevelled
rounded shapes, hand-painted textures with gentle gradients, saturated colours,
warm orange candlelight against cool deep blue and violet shadows, soft ambient
occlusion, warm rim lighting, polished and premium, playful and cheeky mood,
clean readable silhouettes, high detail but simple forms, family friendly.
```

**Short version**

```text
chunky stylised 3D cartoon, soft rounded bevelled forms, hand-painted textures,
warm candlelight vs cool blue shadows, saturated, toy-like, polished mobile game art
```

**Negative prompt** (for tools that support one)

```text
photorealistic, realistic skin, gritty, grimdark, horror, gore, thin lines, sharp
spiky edges, anime, flat vector, pixel art, low poly faceted, noisy texture, film
grain, muted desaturated colours, text, watermark, logo, extra limbs, distorted face
```

## Locked Wick reference

This image is the canonical Wick, locked on 19 October 2026 as `wick_reference_v1.png`. Attach it to every character prompt; when a prompt and this image disagree, the image wins.

**Where the files live**: every reference image in this pack is in the repo at `docs/art/references/`, under the file names given in each heading. A Markdown export of this doc shows a placeholder where each image sits, so open the images from that folder.

&#91;image: Wick reference v1: ivory candle thief with melted top, confident brows, smile, brown satchel of gold\]

**What defines him** (keep these in every pose, skin and model)

| Feature | Locked detail |
| --- | --- |
| Body | Rounded rectangular ivory pillar, soft and slightly squashy, about 1.6x taller than wide |
| Top | Melted crown with thick wax drips over the rim and down the body, dark wick visible |
| Flame | Tall, swept to one side like a quiff, orange outside, yellow-white core, a few loose sparks |
| Face | Large amber-brown eyes with two highlights, one brow raised, confident half-smile, rosy cheeks |
| Limbs | Short stubby arms with mitten hands, short legs with rounded boot-like feet |
| Kit | Brown leather cross-body strap with a gold buckle; brown satchel on his left hip, spilling coins and a red gem |
| Personality | Cheeky and confident, not innocent: he knows he'll get away with it |

**Concept versus game**: this is a hero render, softer and glossier than the in-game look. Use it as-is for key art, the app icon and store art. For the game model, simplify per the Asset Production pack: fewer, chunkier drips (4 to 6 big ones), painted rather than glossy wax, and a simpler satchel and strap that read at 64 px.

**Drippy reference** (`drippy_reference_v1.png`, locked 19 October 2026)

&#91;image: Drippy reference v1: heavily melted, leaning candle with worried face, same satchel\]

| Feature | Locked detail |
| --- | --- |
| Body | Same base as Wick, but melting all over: drips run down the body and arms and pool at his feet |
| Pose | Leans to one side, knees bent, hands clutched to his chest |
| Flame | Same quiff shape, wilting slightly backwards, with more stray sparks |
| Face | Worried brows pulled up and together, eyes wide and darting sideways, mouth open in a nervous grimace |
| Kit | Same strap, buckle and satchel as Wick |

**To resolve before modelling (Drippy, and to a lesser extent Ember, whose squat shape helps)**: at gameplay size Drippy can read as Wick looking scared, because the colour, satchel and strap are identical. Pick one clear differentiator that survives at 64 px, such as a lopsided silhouette held in every animation, a warmer honey-yellow wax tint, or a different satchel colour or patch.

**Cinder reference** (`cinder_reference_v1.png`, locked 19 October 2026)

&#91;image: Cinder reference v1: Wick's body with a black burglar mask tied at the side, smug smirk, satchel of gold\]

| Feature | Locked detail |
| --- | --- |
| Body | Same ivory pillar, melted top and drips as Wick |
| Mask | Dark brown-black eye mask with knotted ties trailing on his right side; his main silhouette marker |
| Face | One brow arched, sly sideways glance, knowing closed-mouth smirk |
| Pose | Finger tapping his chin, scheming; other hand on hip |
| Kit | Same strap and buckle; satchel tied with rope, overflowing with gold and a red gem |

**Pip reference** (`pip_reference_v1.png`, locked 19 October 2026)

&#91;image: Pip reference v1: short candle in a metal tealight cup, huge flame, fist raised\]

| Feature | Locked detail |
| --- | --- |
| Body | Very short, squat block of wax sitting in a brushed silver tealight cup, wax dripping over the cup rim |
| Flame | Huge: roughly twice his body height, wild and wavy, the brightest flame of any skin |
| Face | Determined brows, big bright eyes, wide open grin |
| Pose | Fist raised in a fearless pump, wide stance |
| Kit | None: the cup is his whole look |

**Twist reference** (`twist_reference_v1.png`, locked 19 October 2026)

&#91;image: Twist reference v1: spiral twisted candle pointing at himself, smug grin\]

| Feature | Locked detail |
| --- | --- |
| Body | Spiral-twisted wax wrapping round from base to top, slightly flared melted crown |
| Flame | Curling, ribbon-like and swept off to one side |
| Face | Sharp angled brows, sideways glance, toothy smug grin |
| Pose | Thumb pointing at himself, "who, me?"; other hand on hip, leaning |
| Kit | None: the spiral is his silhouette |

**Ember reference** (`ember_reference_v1.png`, locked 19 October 2026)

&#91;image: Ember reference v1: short squat candle stump with a chipped base, big flame and satchel\]

| Feature | Locked detail |
| --- | --- |
| Body | Short, squat stump, wider and shorter than Wick, with a melted top and a chipped, broken lower edge |
| Flame | Big and tall for his size, flowing and swept to one side |
| Face | Determined brows with a gentle, optimistic smile and a sideways glance |
| Pose | Planted wide, hands on hips: small but standing his ground |
| Kit | Same strap, buckle and satchel as Wick |

**Kit rule across skins**: Wick, Drippy, Cinder and Ember share the strap and satchel. Pip and Twist go without, so their shapes stay clean. Treat the satchel as a swappable rig part, not part of the body mesh.

## Characters

**Wick turnaround** (generate first; this becomes the reference)

```text
[MASTER STYLE BLOCK]
Character turnaround sheet of Wick, a tiny living candle thief, matching the
attached reference exactly: rounded rectangular ivory candle body, melted top
with thick wax drips over the rim, tall orange flame swept to one side like a
quiff, large amber eyes, one confident raised eyebrow, cheeky half-smile, stubby
arms with mitten hands, short legs with rounded feet, brown leather cross-body
strap with a gold buckle and a brown satchel of gold coins on his left hip. Front, three-quarter, side and back views,
neutral standing pose, evenly spaced, plain mid-grey background, consistent
proportions across views.
```

**Expression sheet**

```text
[MASTER STYLE BLOCK]
Expression sheet for Wick the living candle thief (match reference image).
Nine head-and-shoulder poses in a 3x3 grid on plain grey: cheeky grin with flame
flaring bright; nervous with wax sweat drops rolling down; wide-eyed alarm
leaning away from danger; smug after escaping; wincing with flame shrinking;
frantic with flame sputtering wildly; sneaking on tiptoe with flame dimmed low;
greedy delight hugging a gold coin; snuffed out with a comic puff of smoke and
dazed eyes.
```

**Action poses**

```text
[MASTER STYLE BLOCK]
Wick the living candle thief (match reference image) in a dynamic [POSE: dashing
mid-stride / tiptoeing past a sleeping troll / leaping with a loot sack / sliding
on ice / ducking a swinging axe], squash-and-stretch exaggeration, motion lines,
warm flame light on his face, dark blue vault background softly blurred.
```

**Skins**

Use this template and swap in the skin's description. Keep the same height, flame position and face placement as Wick so the rig and hitbox never change.

```text
[MASTER STYLE BLOCK]
Collectible character skin for a tiny living candle thief, same size, rig and
face placement as the reference: [SKIN DESCRIPTION]. Hero pose, three-quarter
view, plain grey background, plus a small front and back view.
```

| Skin | `[SKIN DESCRIPTION]` |
| --- | --- |
| Drippy | match the attached Drippy reference: heavily melted candle leaning to one side, drips over his body and arms pooling at his feet, worried raised brows, wide darting eyes, nervous open-mouth grimace, hands clutched to chest, same strap and satchel as Wick |
| Cinder | match the attached Cinder reference: Wick's body with a dark burglar eye mask tied at the side, sly sideways glance, smirk, finger to chin, rope-tied satchel overflowing with gold, smug criminal mastermind |
| Pip | match the attached Pip reference: very short squat candle in a brushed silver tealight cup with wax over the rim, huge wild flame about twice his height, determined grin, fist raised, tiny and fearless |
| Twist | match the attached Twist reference: spiral twisted wax body, curling ribbon flame swept to one side, sharp angled brows, toothy smug grin, thumb pointing at himself, playful trickster |
| Ember | match the attached Ember reference: short squat candle stump with a chipped broken lower edge, big swept flame, determined brows with an optimistic smile, hands on hips, same strap and satchel as Wick, plucky underdog |
| Seasonal | \[THEME, e.g. carved pumpkin candle with a jack-o'-lantern face / frosted winter candle with a tiny scarf\] |

## Traps and guardians

Traps must read at a glance as dangerous yet funny, with a clear tell (glow, wind-up or rumble) the player can learn. Generate each as a single isolated prop first, then in context.

**Template**

```text
[MASTER STYLE BLOCK]
Isometric game prop for a dungeon vault: [TRAP OR GUARDIAN DESCRIPTION].
Chunky, heavy, slightly comic, clear danger tell in [Danger Red #E5484D or
Trap Teal #3EC6E0], fits on a single square floor tile, three-quarter view,
plain dark blue background, soft rim light.
```

| Name | `[TRAP OR GUARDIAN DESCRIPTION]` |
| --- | --- |
| Drip Pipe | fat brass pipe jutting from a stone wall with a leaky valve, a fat teal water droplet forming at the spout, puddle below |
| Bellows | giant wood-and-leather bellows on a sturdy frame, cheeky painted face on the nozzle, puffs of air swirling out |
| Spike Floor | square stone floor tile with rounded chunky spikes half-raised, faint red glow in the cracks as a warning |
| Snuffer Bell | enormous brass candle-snuffer bell hanging from a chain over a slightly raised pressure plate |
| Night Owl | round, fluffy, grumpy owl guard with huge glowing lantern eyes, tiny helmet, wings folded, patrolling |
| Wax Moths | swarm of five plump fuzzy moths with big googly eyes and glowing wings, drawn to light, more cute than scary |
| Ice Tiles | glossy pale-teal ice floor tiles with frost edges and a slippery sparkle |
| Swing Axe | oversized rounded cartoon axe on a pendulum chain, motion arc trail |
| Mimic Chest | treasure chest overflowing with gold, its lid slightly open revealing a tongue and rounded teeth, eyes peeking |
| Sleepy Troll | big soft moss-green troll slumped asleep across a doorway, snoring with a bubble, nightcap, gentle giant |
| Fan Maze | chunky wooden rotating fan on a stone base, blades mid-spin, wind swirls |
| Brazier (helpful) | squat iron wall brazier with a welcoming warm fire, inviting glow, safe and friendly |

**Sir Whiskers** (signature guardian): use this in place of the table description in the template.

```text
huge, round, fluffy vault cat named Sir Whiskers, smug half-closed eyes, long
whiskers, tiny gold crown or monocle, plump tail, napping on a red velvet
cushion with gold tassels; second pose crouched with bottom wiggling and tail
twitching, eyes locked on a small candle flame, one paw raised to swat;
playful and cheeky, never scary
```

## Vault environments

The default vault is a cosy-but-dangerous stone treasury. Themes reskin the same tile grid, so every theme must keep floors, walls and doors equally readable.

**Gameplay view**

```text
[MASTER STYLE BLOCK]
Isometric 3/4 top-down view of a small dungeon treasure vault on a square tile
grid, three connected rooms, rounded stone brick walls, wooden doors, traps
scattered along the path, piles of gold coins in the far room. The vault is in
deep blue and violet darkness; a tiny living candle thief near the entrance casts
a warm orange pool of light that reveals only the nearby tiles. Clear readable
floor paths, uncluttered, mobile portrait framing.
```

**Home base view**

```text
[MASTER STYLE BLOCK]
Cosy isometric view of a player's vault from above with the roof cut away,
warm torches, a workshop corner with tools and blueprints, treasure hoard glowing
in the centre, little candle thief proudly standing on a gold pile, inviting and
lived-in, portrait mobile framing with space at top and bottom for UI.
```

**Themes**

Add to the gameplay view prompt: "Theme: \[THEME DESCRIPTION\]."

| Theme | `[THEME DESCRIPTION]` |
| --- | --- |
| Classic Treasury (default) | blue-grey stone, warm wooden beams, red banners, gold coin piles |
| Crypt | pale bone-white stone, cobwebs, friendly cartoon skull decorations, purple candles |
| Sunken Ship | barnacled planks, portholes, teal water light, seaweed, pirate chests |
| Dragon Hoard | volcanic rock, glowing amber cracks, mountains of gold, giant sleeping dragon tail in the background |
| Frozen Crypt | icy blue stone, frost, icicles, snow drifts, glittering frozen gold |
| Haunted Bakery | gingerbread walls, frosting drips, candy traps, cake treasure, friendly ghost decorations |

## UI, icons and app icon

UI is chunky and tactile: thick Ink Brown outlines, Parchment panels, gold trim and big rounded buttons that look pressable. Use AI output as a mood reference only; build final UI as clean vector or 9-sliced sprites so text stays sharp.

**UI kit mood board**

```text
[MASTER STYLE BLOCK]
Mobile game UI kit sheet on a plain background: big rounded buttons with thick
dark brown outlines and a glossy top highlight (orange primary, teal secondary,
red danger), parchment panels with gold-trimmed corners, a chunky progress bar
shaped like a melting candle, currency counters for gold coins, wax drops and
gems, star rating badges, a tab bar with five icons. Tactile, readable, no text.
```

**Icon set**

```text
[MASTER STYLE BLOCK]
Set of [N] game icons in a grid, each a single chunky object with a thick dark
brown outline and soft shading, readable at 48 px, transparent background:
[ICON LIST, e.g. gold coin stack, wax drop, purple gem, star, shield, loot
sack, hourglass, trophy crown, candle flame, settings gear].
```

**App icon**

```text
[MASTER STYLE BLOCK]
Square mobile app icon, close-up of Wick the tiny living candle thief (match
reference image) peeking over a pile of gold coins with a cheeky grin, flame
flaring bright orange, deep blue vault background with a soft vignette, bold
silhouette readable at small sizes, no text, no border, centred composition.
```

App icon variants to test: Wick grinning with a coin, Wick mid-snuff with a smoke puff, and Wick's eyes and flame peeking from darkness. Check each at 60 px on a real home screen before choosing.

## Title screen and logo references

These two images (title\_screen\_reference\_v1.png and logo\_reference\_v1.png in docs/art/references/) are style references to attach to the title screen and logo prompts, not final art. They set the direction: "WAX" in dripping ivory wax letters, "BANDITS" in chunky gold, on a riveted wooden plaque with a red flame pennant, candles and treasure, with the crew of Bandits below.

&#91;image: Title screen v1, portrait: the Bandits crew under the Wax Bandits logo\]

&#91;image: Logo v1, landscape: Wax Bandits wordmark on a wooden plaque\]

**Use the references for the look, but make the final differ in these ways**

- [ ] **Masked figure on the logo**: the dark shadow figure with a red bandana and glowing eyes, top left of the plaque, isn't one of our characters. Replace it with Cinder (ivory wax, black eye mask) peeking over the plaque, so the logo only features the Bandits.
- [ ] **Ember is missing from the crew shot**: the bottom-right character mixes Twist's spiral body with Ember's chipped base. Regenerate or paint it as Twist, and add Ember separately so all six appear.
- [ ] **Rebuild the wordmark as vector art**: AI-generated lettering won't stay crisp at every size and can't be edited cleanly. Redraw "WAX BANDITS" in a vector editor using these letter shapes as reference, then texture the wax and gold. This also gives you a clean mark for the trademark filing.
- [ ] **Leave room for UI on the title screen**: keep the top 8% clear of the notch and status bar, and reserve the lower third for the Play button, settings and version text. Move the logo slightly lower, and keep the crew in the middle band.
- [ ] **Store crops**: the Google Play feature graphic is 1024 x 500 (about 2:1). Recompose the landscape logo with more side space rather than squashing it. Check the App Store and Play icons work as a separate square of Wick, not a shrunken logo.

**Title screen prompt** (for the fixed version)

```text
[MASTER STYLE BLOCK]
Portrait 9:16 mobile game title screen. Upper third: the "WAX BANDITS" logo on
a riveted wooden plaque with a red flame pennant, a masked ivory candle thief
peeking over the top left corner [attach logo and Cinder references]. Middle:
the six candle thieves standing together in a treasure vault, matching the
attached references exactly: Wick in the centre with his satchel, Drippy
nervous on the left, Cinder masked on the right, Pip in his tealight cup front
left, Twist with his spiral body front right, Ember the squat chipped stump
beside Wick. Lower third: a calm area of stone floor and scattered coins, low
contrast, left clear for buttons. Warm candlelight against cool blue shadows.
```

## VFX, key art and store screenshots

**VFX reference sheet**

```text
[MASTER STYLE BLOCK]
Stylised game VFX reference sheet, six panels on dark blue: candle flame idle
flicker; flame flare of joy with sparks; flame sputtering in panic; comic snuff
out with a round puff of grey smoke and a tiny ghost wisp; gold coin pickup burst
with sparkles; teal water splash on a candle. Chunky cartoon shapes, few large
particles, bold colours, readable on a phone screen.
```

VFX rules: few, large, hand-shaped particles rather than many small ones; flame effects always use the Candle Flame and Flame Core colours; danger effects use red or teal, never orange.

**Key art**

```text
[MASTER STYLE BLOCK]
Cinematic key art for a mobile heist game: Wick the tiny living candle thief
(match reference image) tiptoeing through a dark treasure vault, holding his
breath, a huge grumpy owl guard's glowing eyes just behind him, a swinging axe
above, a mountain of gold ahead lit by his warm flame. Dramatic warm-versus-cool
lighting, funny tension, [FORMAT: 16:9 store feature / 9:16 social / 1:1 ad].
Leave clear space at the [TOP/BOTTOM] for the logo.
```

**Store screenshot backgrounds**

Capture real gameplay for the screenshots themselves, then use this for the framing backgrounds and callouts:

```text
[MASTER STYLE BLOCK]
Portrait 9:16 app store screenshot background, deep blue vault gradient with
warm candle glow, scattered gold coins and soft bokeh, empty central area for a
phone gameplay capture, space at top for a short headline. Theme: [BUILD YOUR
VAULT / RAID YOUR FRIENDS / DON'T GET SNUFFED / COLLECT CANDLES].
```

## 3D production handoff

Concepts become game assets through these specs, so every model fits the same rig, camera and performance budget.

| Asset | Spec |
| --- | --- |
| Wick and skins | About 3,000 to 5,000 triangles; one shared humanoid-lite rig (root, body squash bone, 2 arms, 2 feet, head, flame bones); flame as a separate shader-driven mesh; one 1024 px texture atlas |
| Traps and guardians | 1,500 to 4,000 triangles; fits within one tile footprint (guardians may overhang); 512 px textures; animation clips named after sim events (Idle, Warn, Fire, Alert, Chase, Sleep) |
| Vault tiles | Modular 1 x 1 tile kit per theme: floor, wall straight, wall corner, door, pit, ice; shared 1024 px theme atlas |
| UI | Vector or 9-sliced sprites at 2x and 3x; fonts with full Latin Extended for localisation |
| VFX | Particle textures in one 512 px atlas; additive flame, alpha-blended smoke |

**Animation must-haves for Wick:** idle (peeking, patting flame), dash, run, tiptoe loop, hit wince, frantic low-health loop, grab-gold grin, escape hop, snuff, plus face blend shapes for Fear, Greed and Smug.

**Review checklist** (run on every asset before it ships)

- [ ] Silhouette readable as a solid black shape at 64 px.
- [ ] Looks right on the dark vault floor and under Wick's warm light.
- [ ] Uses palette colours; danger never shown in flame orange.
- [ ] Matches Wick's reference proportions (for characters and skins).
- [ ] Within the triangle and texture budget.
- [ ] No resemblance to existing games' characters, logos or icons.
- [ ] Checked on a real low-end phone, not just in the editor.
