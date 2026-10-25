# Wax Bandits — Master Asset List

Oct 24, 2026 · @Jamie

## At a glance

The full game world needs about 170 asset line items: 88 for the vertical slice, 75 more for launch and 8 for post-launch updates, plus a repeatable seasonal package. Several items are kits (each vault theme is about 21 tile pieces) or groups (about 45 icons, about 80 sound effects), so the file count is far higher.

| Category | Slice | Launch | Live | Total |
| --- | --- | --- | --- | --- |
| Characters | 4 | 7 | 1 | 12 |
| Guardians | 2 | 4 | 2 | 8 |
| Traps, puzzles and helpers | 8 | 4 | 0 | 12 |
| Vault environments and home base | 2 | 5 | 3 | 10 |
| Props and set dressing | 8 | 8 | 1 | 17 |
| Treasure and loot | 6 | 3 | 0 | 9 |
| Lighting and atmosphere | 8 | 3 | 0 | 11 |
| VFX | 18 | 9 | 0 | 27 |
| UI | 17 | 12 | 0 | 29 |
| Branding and store | 3 | 9 | 0 | 12 |
| Audio | 10 | 3 | 1 | 14 |
| Tutorial and onboarding | 2 | 8 | 0 | 10 |
| **Total** | **88** | **75** | **8** | **171** |

**What makes it look stunning** (spend effort here first)

1. **Lighting over detail:** the warm flame pool against cool, dark vaults, with bloom, dust motes and contact shadows. This lifts every other asset.
2. **Set dressing density:** candle clusters, banners, rubble, cobwebs and coin stacks in every corner and edge, so no tile looks bare.
3. **Gold that feels rich:** dense, sparkly treasure, coins that fly to the counter, and chests that burst open.
4. **Characters that move with personality:** squash and stretch, reactions and signature victories matter more than polygon count.
5. **Juicy feedback:** every hit, grab and escape gets a VFX, a sound and a small camera punch.

## Characters

Phase key used in every table: **Slice** = vertical slice, **Launch** = needed for soft or global launch, **Live** = post-launch updates.

| ID | Asset | Contents | Phase |
| --- | --- | --- | --- |
| `char_wick_default` | Wick | Model, 1024 px atlas, shared rig, face blend shapes, flame mesh | Slice |
| `char_drippy` | Drippy | Model and texture on the shared rig, foot-puddle mesh, honey wax if chosen | Slice |
| `char_cinder` | Cinder | Model, mask with two tie dangle bones | Launch |
| `char_pip` | Pip | Model, tealight cup, long flame bones | Launch |
| `char_twist` | Twist | Spiral body model skinned to compress evenly | Launch |
| `char_ember` | Ember | Squat stump with chipped edge | Launch |
| `char_kit_satchel` | Strap, buckle and satchel | Swappable rig part with jiggle bone; coin and gem spill variants | Slice |
| `anim_wick_core` | Core animations | idle, 2 fidgets, dash, run, tiptoe, hit, low\_health, grab, escape, snuff, victory (12 clips) | Slice |
| `anim_wick_meta` | Menu animations | home idle on gold pile, skin-reveal spin, level-up cheer, defeat sulk, shop wave (5 clips) | Launch |
| `anim_skin_signature` | Signature moves | One unique victory per skin (Drippy relieved collapse, Cinder chin tap, Pip fist pump, Twist finger guns, Ember stand-tall) | Launch |
| `char_portraits` | Character portraits | Head-and-shoulders render per Bandit for profile, shop and pass (6) | Launch |
| `char_skin_seasonal_*` | Seasonal skins | Pumpkin, frosted winter and others, each with flame colour and snuff effect | Live |

## Guardians

Each guardian is a model, a 512 px texture, a rig and the clips listed, plus a floor decal that shows its danger zone when alert.

| ID | Guardian | Clips and extras | Phase |
| --- | --- | --- | --- |
| `guard_night_owl` | Night Owl | idle, patrol, alert, chase, hit\_player, return; sight-cone decal; helmet glint | Slice |
| `guard_wax_moths` | Wax Moths | flutter loop, drift, swarm-to-light, nibble, scatter; group of 5 with shared glow | Slice |
| `guard_mimic_chest` | Mimic Chest | breathe (tell), snap open, bite, settle; gold spill on snap | Launch |
| `guard_sleepy_troll` | Sleepy Troll | snore loop, stir, wake, stomp, back to sleep; snore-bubble VFX; nightcap jiggle | Launch |
| `guard_sir_whiskers` | Sir Whiskers | nap, wake, stalk, lose\_interest, wiggle (30 frames), pounce, swat, lick\_paw, chase\_decoy, return\_to\_cushion | Launch |
| `prop_whiskers_cushion` | Velvet cushion | Separate prop with gold tassels and a sat-in dent | Launch |
| `guard_whiskers_coats` | Sir Whiskers coats | Ginger, black, tabby texture sets | Live |
| `guard_whiskers_outfits` | Sir Whiskers outfits | Seasonal jumper, crown variants | Live |

## Traps, puzzles and helpful objects

Every trap needs `idle`, `warn`, `fire` and `reset` clips, and its `warn` must read on its own. Each is reskinned per vault theme later (see Seasonal and live-ops content).

| ID | Asset | Extras | Phase |
| --- | --- | --- | --- |
| `trap_drip_pipe` | Drip Pipe | Droplet mesh, puddle decal, valve wheel spin | Slice |
| `trap_bellows` | Bellows | Painted face, gust ribbon VFX per row | Slice |
| `trap_spike_floor` | Spike Floor | Crack-glow warning decal | Slice |
| `trap_snuffer_bell` | Snuffer Bell | Chain, raised pressure plate, dust puff on drop | Slice |
| `trap_ice_tiles` | Ice Tiles | Frost edge decals, slide streak VFX | Launch |
| `trap_swing_axe` | Swing Axe | Chain, ceiling mount, floor arc decal | Launch |
| `trap_fan_maze` | Fan Maze | Rotating blades, wind swirl VFX | Launch |
| `trap_lever_doors` | Lever Doors | Lever post and door pairs in 4 gem colours | Launch |
| `prop_brazier` | Wall brazier (helpful) | Lit and used states, relight burst VFX | Slice |
| `prop_decoy_drip` | Decoy wax drip | Glowing wax blob, fade-out | Slice |
| `prop_entrance` | Vault entrance | Archway with "escape here" glow | Slice |
| `prop_pressure_plate` | Pressure plate | Shared plate mesh, raised and pressed states | Slice |

## Vault environments

Every theme is one modular kit of 1 m pieces on a single 1024 px atlas. Variants are what stop a vault looking tiled: three floor variants and two wall variants per theme make the biggest difference to how polished it looks.

**Pieces in every kit** (about 20 per theme)

| Piece | Count |
| --- | --- |
| Floor tiles (plain, cracked, worn) | 3 |
| Floor trim (edge where floor meets wall) | 1 |
| Wall straight (plain, detailed) | 2 |
| Wall corners (inner, outer) | 2 |
| Wall end cap and half wall | 2 |
| Door frame, door (open, closed), locked door | 3 |
| Entrance arch | 1 |
| Pit edge, pit corner, pit floor with glow | 3 |
| Ice floor | 1 |
| Pillar, archway between rooms | 2 |
| Vault backdrop (out-of-bounds dark walls and ceiling silhouettes) | 1 |

**Themes**

| ID | Theme | Signature look | Phase |
| --- | --- | --- | --- |
| `tile_treasury` | Classic Treasury (default) | Blue-grey stone, warm wood beams, red crown banners | Slice |
| `tile_crypt` | Crypt | Bone-white stone, cobwebs, friendly skulls, purple candles | Launch |
| `tile_sunken_ship` | Sunken Ship | Barnacled planks, portholes, teal water light | Launch |
| `tile_dragon_hoard` | Dragon Hoard | Volcanic rock, amber cracks, sleeping dragon tail backdrop | Live |
| `tile_frozen_crypt` | Frozen Crypt | Icy stone, icicles, frozen gold | Live (first Heist Pass) |
| `tile_haunted_bakery` | Haunted Bakery | Gingerbread walls, frosting drips, candy | Live |

**Home base extras** (seen on the Home screen, not in raids)

| ID | Asset | Phase |
| --- | --- | --- |
| `home_workshop` | Workshop corner with workbench, blueprints and tools, upgrade-in-progress scaffold | Launch |
| `home_hoard` | Central treasure hoard that grows with stored gold (5 size stages) | Launch |
| `home_level_dressing` | Extra decoration unlocked per Vault Level (banners, rugs, statues), 12 stages | Launch |
| `home_rug_crest` | Red crown rug and wall crest | Slice |

## Props and set dressing

Set dressing is what turns a grid into a place. These are non-gameplay props that fill corners, edges and the space outside the play area. All share the theme atlas or one shared props atlas.

| ID | Asset | Variants | Phase |
| --- | --- | --- | --- |
| `prop_banner` | Hanging banners (crown crest) | Short, long, torn | Slice |
| `prop_wall_torch` | Wall sconce torch | Lit, guttering | Slice |
| `prop_candle_cluster` | Melted candle clusters on floor and ledges | Small, medium, large | Slice |
| `prop_crate_barrel` | Crates and barrels | Crate, stacked crates, barrel, broken barrel | Slice |
| `prop_pots` | Clay pots and urns | 3 shapes, one breakable | Slice |
| `prop_chains` | Hanging chains and hooks | 2 lengths | Slice |
| `prop_cobwebs` | Cobweb cards for corners | 3 shapes | Slice |
| `prop_rubble` | Rubble, loose bricks, dust piles | 4 pieces | Slice |
| `prop_plants` | Potted ferns and creeping moss | 3 | Launch |
| `prop_furniture` | Desk with map, stool, bookshelf, weapon rack (blunt and comic) | 4 | Launch |
| `prop_skull_bones` | Friendly cartoon skull and bone pile | 2 | Launch |
| `prop_statue` | Pompous king statue and bust | 2 | Launch |
| `prop_pipes_valves` | Wall pipes and valves (ties visually to Drip Pipe) | 3 | Launch |
| `prop_rope_ladder` | Rope coils, ladders, scaffolding | 3 | Launch |
| `prop_portraits` | Framed portraits on walls (vault owner's chosen Bandit) | 2 frames | Live |
| `prop_rug` | Floor rugs | 3 patterns | Launch |
| `prop_out_of_bounds` | Background silhouettes beyond the play area: arches, columns, distant hoards | 1 set per theme | Launch |

## Treasure and loot

Gold is the reward the whole game is about, so it should be the most satisfying thing on screen: dense, sparkly and bouncy.

| ID | Asset | Variants | Phase |
| --- | --- | --- | --- |
| `prop_treasure_pile` | Treasure piles (the steal targets) | Small, medium, large, plus emptied state | Slice |
| `prop_coin` | Single gold coin | Spinning, for fly-to-counter and spills | Slice |
| `prop_coin_stack` | Coin stacks for dressing | 3 heights | Slice |
| `prop_chest` | Treasure chests | Closed, open overflowing, empty | Slice |
| `prop_gems` | Gems | Red (premium gem), plus blue and green for dressing | Slice |
| `prop_valuables` | Goblets, crowns, pearl strings, candlesticks | 4 | Launch |
| `prop_wax_drop` | Wax drop currency pickup | 1 | Slice |
| `prop_loot_sack` | Loot sack and spilled loot | 2 | Launch |
| `prop_reward_chests` | Reward chests for pass, shop and ads | Wooden, silver, gold, with opening animation | Launch |

## Lighting, atmosphere and post-processing

The mood is warm candlelight against cool, dark vaults, and it does more for the "stunning" factor than any single model. These are mostly shader and lighting assets rather than meshes.

| ID | Asset | Purpose | Phase |
| --- | --- | --- | --- |
| `shader_stylised_lit` | Stylised lit shader | Two-step ramp, warm rim, painted-light friendly | Slice |
| `shader_flame` | Flame shader | Sway and flicker-driven frenzy | Slice |
| `shader_visibility_mask` | Visibility mask | Darkens tiles outside Wick's light, driven by Sim | Slice |
| `light_wick_cookie` | Flame light cookie | Soft, slightly flickering warm pool shape | Slice |
| `light_baked_per_theme` | Baked vault lighting | Lightmaps or vertex lighting per theme | Slice |
| `post_lut_per_theme` | Colour grading LUT | One per theme, plus a warm "victory" LUT | Launch |
| `post_bloom_vignette` | Bloom and vignette profile | Gentle glow on flames and gold, dark corners | Slice |
| `fx_dust_motes` | Floating dust in light shafts | Adds depth near Wick's light | Slice |
| `fx_ground_fog` | Low fog cards | Cool fog in pits and far corners | Launch |
| `fx_light_shafts` | God-ray cards | From high windows and grates | Launch |
| `decal_ao_contact` | Contact shadow decals | Grounds props and characters cheaply | Slice |

## VFX

Few, large, hand-shaped particles; flame effects in Candle Flame and Flame Core colours, danger effects in red or teal. All textures share one 512 px VFX atlas.

| ID | Effect | Phase |
| --- | --- | --- |
| `vfx_flame_idle` | Wick's flame idle flicker and sparks | Slice |
| `vfx_flame_flare` | Joy flare when grabbing gold | Slice |
| `vfx_flame_frantic` | Sputtering low-health flame | Slice |
| `vfx_flicker_lost` | Hit sizzle and flame shrink | Slice |
| `vfx_snuff` | Comic smoke puff with tiny ghost wisp | Slice |
| `vfx_relight` | Brazier relight burst | Slice |
| `vfx_wax_sweat` | Sweat drops when scared | Slice |
| `vfx_coin_burst` | Gold pickup burst and sparkle | Slice |
| `vfx_coin_fly` | Coins flying to the counter | Slice |
| `vfx_water_splash` | Drip Pipe splash and puddle ripple | Slice |
| `vfx_gust` | Bellows gust ribbons | Slice |
| `vfx_warn_glow` | Trap warning glow and rumble dust | Slice |
| `vfx_bell_slam` | Snuffer Bell dust ring | Slice |
| `vfx_alert_mark` | Guardian "!" alert pop | Slice |
| `vfx_moth_glow` | Moth wing glow trails | Slice |
| `vfx_decoy` | Decoy glow and fade | Slice |
| `vfx_escape` | Escape burst at the entrance | Slice |
| `vfx_ice_slide` | Ice slide streaks | Launch |
| `vfx_axe_whoosh` | Swing Axe trail | Launch |
| `vfx_fan_wind` | Fan wind swirls | Launch |
| `vfx_troll_snore` | Snore bubble and Z's | Launch |
| `vfx_whiskers_swat` | Swat impact stars and fur tuft | Launch |
| `vfx_ui_upgrade` | Upgrade complete sparkle | Launch |
| `vfx_ui_chest_open` | Reward chest opening light burst | Launch |
| `vfx_ui_star_earn` | Star earned pop on results | Slice |
| `vfx_confetti` | Victory confetti and coin rain | Launch |
| `vfx_level_up` | Vault Level up celebration | Launch |

## UI

UI follows the screens reference: chunky gold-framed panels, round outlined buttons and bold rounded type, built as 9-sliced sprites with live text.

**Screens**

| ID | Screen | Phase |
| --- | --- | --- |
| `ui_scr_title` | Title and loading | Slice |
| `ui_scr_home` | Home (vault view, side buttons, Raid and Build) | Slice |
| `ui_scr_raid_hud` | Raid HUD (flickers, timer, loot, decoy, stealth) | Slice |
| `ui_scr_results` | Raid results (stars, loot, clip share, replay) | Slice |
| `ui_scr_editor` | Vault editor (tray, budget bar, test, publish) | Slice |
| `ui_scr_defence_log` | Defence log and replay viewer | Slice |
| `ui_scr_matchmaking` | Finding a vault | Slice |
| `ui_scr_shop` | Shop | Launch |
| `ui_scr_pass` | Heist Pass ladder | Launch |
| `ui_scr_skins` | Skins collection | Launch |
| `ui_scr_trophies` | Leaderboards and achievements | Launch |
| `ui_scr_guild` | Guild home, chat, members | Launch |
| `ui_scr_profile` | Player profile | Launch |
| `ui_scr_settings` | Settings, privacy, accessibility | Launch |
| `ui_scr_onboarding` | Tutorial overlays and ghost finger | Launch |
| `ui_scr_daily_vault` | Daily Vault and share card | Launch |

**Components**

| ID | Component | Phase |
| --- | --- | --- |
| `ui_btn_set` | Buttons: primary gold, secondary teal, danger red, round icon, disabled, pressed | Slice |
| `ui_panels` | Panels, frames, dialogs, tooltips, tabs | Slice |
| `ui_bars` | Candle progress bar, timer, budget bar | Slice |
| `ui_counters` | Currency, Crowns and star badges | Slice |
| `ui_badges` | Notification dots, "new" and "sale" ribbons | Launch |
| `ui_cards` | Reward, skin, vault and leaderboard cards | Launch |
| `ui_toggles` | Toggles, sliders, checkboxes | Launch |
| `ui_fonts` | Display font and body font with Latin Extended | Slice |

**Icons** (about 45 in total)

| ID | Group | Phase |
| --- | --- | --- |
| `ui_ico_currency` | Gold, wax, gems, Crowns, stars | Slice |
| `ui_ico_nav` | Defence, Trophies, Share, Shop, Pass, Skins, Settings, Guild, Friends | Slice |
| `ui_ico_hud` | Flicker flame (full, empty), decoy, stealth, timer, loot | Slice |
| `ui_ico_traps` | One editor icon per trap and guardian (about 17) | Slice |
| `ui_ico_misc` | Lock, info, close, back, share, replay, ad, clip, chest | Slice |

## Branding, store and marketing

| ID | Asset | Specs | Phase |
| --- | --- | --- | --- |
| `brand_wordmark` | Wax Bandits vector wordmark | Full colour, one-colour, and stacked versions | Launch |
| `brand_app_icon` | App icon | Wick close-up, master 1024 px, iOS and Android adaptive layers | Launch |
| `brand_splash` | Splash and loading screen | Portrait, safe-area aware | Slice |
| `brand_title_art` | Title screen art | All six Bandits under the logo | Launch |
| `store_screenshots` | Store screenshots | 5 to 8 portrait frames per store with captions | Launch |
| `store_feature_graphic` | Google Play feature graphic | 1024 x 500 | Launch |
| `store_preview_video` | App preview and trailer | 15 to 30 seconds of real gameplay | Launch |
| `mkt_key_art` | Key art | 16:9, 9:16 and 1:1 versions | Launch |
| `mkt_social_kit` | Social kit | Profile images, banners, character stickers, short loops | Launch |
| `mkt_share_card` | Vault link share card and web fallback page art | Open Graph image 1200 x 630 | Slice |
| `mkt_clip_frames` | Clip frames and watermark for shared raid clips | Free and pass versions | Slice |
| `mkt_press_kit` | Press kit | Logo pack, character renders, screenshots, fact sheet | Launch |

## Audio

**Music**

| ID | Track | Phase |
| --- | --- | --- |
| `mus_raid_layers` | Raid music in three crossfading layers: calm, tense, escape | Slice |
| `mus_home` | Home and menu loop | Slice |
| `mus_editor` | Vault editor loop, relaxed | Launch |
| `mus_title` | Title theme | Launch |
| `mus_stingers` | Victory, defeat, level-up, chest-open stingers | Slice |
| `mus_theme_variants` | A music variant per vault theme | Live |

**Sound effects** (about 80 in total, each with 2 to 3 variations so repeats don't grate)

| ID | Group | Phase |
| --- | --- | --- |
| `sfx_wick` | Footsteps, tiptoe, dash, squeaks, gasps, flare, flicker lost, snuff, grab, escape | Slice |
| `sfx_skin_voices` | A distinct squeak and gasp set per Bandit | Launch |
| `sfx_traps` | Warn and fire sounds per trap | Slice (4), Launch (rest) |
| `sfx_guardians` | Owl hoot, moth buzz, mimic snap, troll snore and stomp, Sir Whiskers purr, meow, swat | Slice (2), Launch (rest) |
| `sfx_loot` | Coin clinks, gem chimes, chest open, currency counting up | Slice |
| `sfx_ui` | Tap, back, open, close, purchase, error, notification | Slice |
| `sfx_editor` | Place, rotate, delete, invalid, publish | Slice |
| `sfx_ambience` | Vault room tone, distant drips, torch crackle, per-theme beds | Slice (default), Live (themes) |

## Tutorial and onboarding

The tutorial teaches through Wick's miming and a ghost finger, so most of its assets are animation, overlays and short clips rather than text.

| ID | Asset | Contents | Phase |
| --- | --- | --- | --- |
| `ui_tut_ghost_finger` | Ghost finger | Swipe, hold, tap and drag gestures, looping | Slice |
| `ui_tut_spotlight` | Spotlight overlay | Dimmed screen with a highlight ring, pulse and pointer arrow | Slice |
| `tut_vaults_1_5` | Tutorial vaults | Five hand-built, fully dressed vaults, one lesson each | Launch |
| `anim_wick_tutorial` | Wick's teaching mimes | Point, beckon, finger to lips (stealth), thumbs up, facepalm on fail (5 clips) | Launch |
| `ui_tut_prompts` | Hint bubbles | Speech-bubble prompts with an icon and one short line | Launch |
| `ui_tut_intro_cards` | Trap and guardian intro cards | Card frame plus a 3-second looping clip of each tell (about 17) | Launch |
| `ui_tut_first_build` | Guided build overlays | Step markers on the editor tray, budget bar and Publish button | Launch |
| `ui_tut_progress` | Tutorial progress and rewards | Step tracker, step reward pop, starter chest | Launch |
| `vfx_tut_success` | Step complete burst | Sparkle and gold confetti | Launch |
| `sfx_tut` | Tutorial sounds | Hint chime, step complete, tutorial finished fanfare | Launch |

## Seasonal and live-ops content

Each Heist Pass season repeats this package. Build one as a template, then every new season is a reskin.

| Per season | Contents |
| --- | --- |
| Evolving season skin | 3 stages, each with its own flame colour and snuff effect |
| Vault theme | Full tile kit and set dressing (see Vault environments) |
| Trap skins | 4 to 6 cosmetic trap reskins |
| Vault taunts | 3 animations shown to snuffed raiders |
| Emotes | 4 to 6 |
| Clip frames and stickers | 3 to 5 |
| Profile banner and guild badge | 1 each |
| Season key art | Pass screen header, store and social versions |
| Event UI | Event banner, icon and reward chest skin |

Also recurring: weekly featured-vault banners, Daily Vault hand-built layouts, and holiday dressing packs (Halloween, Christmas).
