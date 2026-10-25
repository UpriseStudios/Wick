# Wax Bandits — Game Design Spec

Oct 17, 2026 · @Jamie

## Overview

Wax Bandits (store subtitle: "Tiny Thieves, Big Heists") is a mobile build-and-raid game starring Wick and his crew of candle thieves. You design a booby-trapped vault around your hoard, then sneak a tiny living candle through other players' vaults to steal theirs. Raids take 30 to 60 seconds, and players build the levels, so content never runs out.

**Design pillars**

- **One more raid:** short, tense runs where every failure is funny and every success feels earned.
- **Your vault is your voice:** building is self-expression, and every player who fails your vault is a small win for you.
- **Built to be shown off:** every raid makes a clip, and every vault is a challenge link.
- **Fair, not pay-to-win:** skill decides raids; spending buys style and speed, never power in a raid.

**Platform and audience:** iOS and Android, portrait, one-thumb play. Aimed at casual-to-midcore players aged 13 and up who enjoy Clash of Clans, Super Mario Maker and short-session puzzle games.

## The hero: Wick

Wick is a stubby, wide-eyed living candle and a career treasure thief. His flame is both his life and his light, so a single idea drives the look, the controls and the trap design.

- **Flame = health:** Wick starts each raid with a full flame of 3 flickers. Water, gusts and snuffers cost flickers; at zero he is snuffed out with a comic puff of smoke.
- **Flame = light:** vaults are dark. Wick's flame lights a radius around him, so a weaker flame means seeing less of what is coming.
- **Flame = risk choice:** toggling tiptoe dims the flame to sneak past sleeping guardians, but you also see less. Wall braziers relight one flicker, often placed as bait.
- **Personality:** Wick reacts to everything around him (see Reactive personality below). He never speaks, only squeaks and gasps.

Why a candle works: it is instantly readable as a silhouette and as an app icon, it has not been done to death the way goblins have, and "don't get snuffed out" explains the game in four words.

**Reactive personality**

Wick should feel alive in every frame, so his face and flame respond to what is happening, not only to button presses.

| Trigger | Reaction |
| --- | --- |
| Scared or near danger | Wax beads of sweat roll down his body |
| Approaching a trap | Eyes widen and he leans away from it |
| Grabbing gold | A big cheeky grin and a bright flare of flame |
| Losing a flicker | A wince, then the flame shrinks a step |
| Low health | The flame grows increasingly frantic: flickering, sputtering and gasping at 1 flicker |
| Escaping | A smug little hop, satchel jingling |
| Idle | Peeks around, pats his flame flat, counts his loot |

**Character concepts**

The goal is a character people recognise and want to collect, not just a functional avatar. One concept is the hero; the others become headline collectible skins that share the same rig and reactions.

| Concept | Look | Personality | Role |
| --- | --- | --- | --- |
| Classic Wick, the lovable thief | Rounded ivory candle with a melted, dripping top, swept quiff-like flame, large amber eyes, confident brows, stubby limbs, cross-body strap and satchel of gold (locked reference v1) | Cheeky and confident; the strongest all-round mascot | Default hero and app icon |
| Drippy, the nervous disaster | Heavily melted and leaning, drips over body and arms pooling at his feet, worried brows and darting eyes, hands clutched to chest (locked reference v1) | Panicky and hapless; made for funny failures and social clips | Early skin, ideal for promo clips |
| Cinder, the tiny criminal mastermind | Wick's body with a burglar eye mask tied at the side, sly glance and smirk, rope-tied satchel of gold (locked reference v1) | Cheeky and smug; communicates the heist instantly | Premium skin and the face of heist events |
| Pip, the fearless tealight | Very short candle in a silver tealight cup, flame about twice his height, determined grin (locked reference v1) | Tiny, bold, energetic; strong toy appeal | Starter-pack reward |
| Twist, the cheeky trickster | Spiral-twisted wax body, curling ribbon flame swept to one side, toothy smug grin (locked reference v1) | Playful and unusual; instantly recognisable shape | Season-pass skin |
| Ember, the determined survivor | Short squat stump with a chipped lower edge, big swept flame, optimistic determined face, satchel (locked reference v1) | Compact, endearing underdog | Achievement or loyalty skin |

Every skin keeps the same silhouette width and flame position so hitboxes and readability never change, only personality.

## Core loop

&#91;embedded content: core loop · 4 steps\]

Raiding is the fun at the centre; building gives raids a purpose and keeps paying out while the player is away.

## Raid mode

A raid is a 60-second sneak through another player's vault, viewed from a Clash-style isometric camera. Wick moves tile by tile on a grid, which keeps controls simple and makes every raid replayable from a short input log.

**Controls (one thumb)**

- Swipe in a direction: Wick dashes one tile.
- Swipe and hold: he keeps running until you release or hit a wall.
- Tap anywhere (without swiping): toggle tiptoe stealth, which dims the flame and lets him pass sleeping guardians. Swipes still move him while stealthy.
- Decoy button (bottom corner): drop a decoy wax drip (one per raid) that distracts guardians for 3 seconds.

**Run structure**

1. **Scout (5 seconds):** a quick flyover shows the vault's outer rooms only. Inner rooms stay dark.
2. **Infiltrate:** reach the hoard room through traps and guardians.
3. **Plunder:** grab treasure piles. Each one adds weight and slows Wick slightly.
4. **Escape:** get back to the entrance before the timer runs out. Loot only counts if you escape.

**Scoring: 3 stars**

| Star | Earned for |
| --- | --- |
| 1 | Reaching the hoard room |
| 2 | Taking at least half the treasure |
| 3 | Escaping with all 3 flickers left |

Loot paid out = share of treasure stolen, capped by the defender's vault level. Failing still pays a small consolation amount so a bad run never feels wasted.

## Tutorial and onboarding

A new player should raid, build and share a vault inside their first 8 minutes, learning by doing rather than reading. Wick teaches by miming (pointing, beckoning, a finger to his lips for stealth), with a ghost finger showing the gesture and at most one short line of text on screen.

| Step | Teaches | Done when |
| --- | --- | --- |
| Vault 1: "First steps" | Swipe to dash, hold to run | Wick reaches the treasure |
| Vault 2: "Shh!" | Tap to toggle stealth, sneak past a sleeping guardian | He passes it without waking it |
| Vault 3: "Look over there" | The decoy button | A guardian chases the decoy |
| Vault 4: "Keep the flame alive" | Flickers, taking a hit, relighting at a brazier | Wick relights to 3 flickers |
| Vault 5: "Get out!" | Grab loot and escape before the timer runs out | Escape with at least 1 star |
| First build | Place 3 traps, run the clear-check, publish | The vault goes live |
| First real raid | A seeded easy vault that's guaranteed beatable | Loot paid, results shown |
| Share prompt | "Dare a friend to crack your vault" | Shared, or skipped |

**Rules**

- Vaults 1 and 2 are required; skip is offered from vault 3 onwards.
- Each step pays a small reward, ending with a starter chest after the first real raid.
- Every trap and guardian gets a one-time **intro card** the first time a player meets it: a 3-second looping clip of its tell and how to beat it.
- Contextual hints return if a player fails the same vault 3 times (for example, "Try stealth" with a pulse on the stealth icon).
- The tutorial can be replayed from Settings.
- Every step sends an analytics event, so drop-off is visible per step.

## Vault building

You can only publish a vault you have beaten yourself. That one rule keeps every vault fair and gives the server a proof run to check against.

- **Grid:** vaults start at 12 x 16 tiles with 2 to 3 rooms and grow to 20 x 28 at Vault Level 10. Raising your Vault Level (the Town Hall equivalent) unlocks more space, rooms and trap slots.
- **Build budget:** each trap and guardian costs build points. The budget grows with Vault Level, so a level 3 vault can't be packed with level 10 traps.
- **Editor:** drag-and-drop from a tray, tap to rotate, pinch to zoom. A Test button drops you straight into a raid of your own vault.
- **Clear-check:** to publish, you must escape your own vault with at least 1 star. The input log of that run is uploaded and re-simulated on the server before the vault goes live.
- **Defence rewards:** each failed raid on your vault pays you a defence bonus. A successful raid on you triggers a short shield so you aren't robbed repeatedly.
- **Vault stats:** raids attempted, snuff rate, most deadly trap. These feed the share features below.

## Traps and guardians

Every trap attacks the flame or the path, and every one has a readable counter, so deaths feel like the player's fault rather than the game's. The first 6 rows are the vertical-slice set.

| Name | Type | What it does | Counter | Vault Level |
| --- | --- | --- | --- | --- |
| Drip Pipe | Trap | Drops water on a rhythm; a hit costs 1 flicker | Time your dash between drips | 1 |
| Bellows | Trap | Built into a wall; blasts a gust down the row in front of it, shrinking light and costing 1 flicker | Duck behind pillars or crates | 1 |
| Spike Floor | Trap | Spikes rise on a cycle; standing on them costs 1 flicker | Read the warning rumble and wait | 1 |
| Snuffer Bell | Trap | A giant bell drops on a pressure plate; instant snuff | Spot the slightly raised plate | 2 |
| Night Owl | Guardian | Patrols a route and chases any light it sees | Tiptoe out of its sight cone | 2 |
| Wax Moths | Guardian | A swarm drawn to the brightest light, nibbling flame | Dim the flame or lure them with a decoy | 3 |
| Ice Tiles | Trap | Wick slides until he hits something | Plan the route before stepping on | 3 |
| Swing Axe | Trap | Swings across a corridor on a timer | Timing | 4 |
| Mimic Chest | Guardian | A fake treasure pile that bites; costs 2 flickers | It breathes slightly when lit | 5 |
| Sleepy Troll | Guardian | Blocks a doorway; wakes in bright light and stomps | Tiptoe past while it snores | 6 |
| Fan Maze | Trap | Rotating fans that push Wick off his path | Learn the rotation | 7 |
| Lever Doors | Puzzle | Doors linked to levers elsewhere in the vault | Route planning | 8 |

Wall braziers (relight 1 flicker) are free to place but limited per vault. Smart builders use them as bait next to traps.

**Sir Whiskers: signature guardian (Vault Level 10)**

Sir Whiskers is a big, fluffy, smug vault cat who can't resist a flickering flame. Every other threat rewards moving well; he rewards holding your nerve and standing still.

|  | Rule |
| --- | --- |
| Idle | Naps on a velvet cushion; wakes when Wick's lit flame comes within 3 tiles (tiptoe shrinks the light, so sneaking past can keep him asleep) |
| Stalk | While Wick is moving, he follows the flame at a crouch |
| Freeze to evade | If Wick stays still for 1.5 seconds, he loses interest, yawns and returns to his cushion |
| Tell | About 1 second of butt-wiggle and tail-twitch before every pounce |
| Swat | Costs 1 flicker and bats Wick 2 tiles in the swat direction; Wick stops on the last free tile if a wall or object is in the way |
| Safety | After a swat, Wick gets a short invulnerability window, and knockback can never trigger instant-snuff traps such as the Snuffer Bell |
| Decoy | Sir Whiskers always chases a decoy over Wick, batting it around until it fades |
| Limit | One per vault; high build-point cost |
| Personality | Not evil, just playing. After a swat he sits and licks a paw, very pleased with himself |

Builders can place him to bat raiders into other traps, which gives high-level vaults new combos. Cosmetic coat variants (ginger, black, tabby, a seasonal jumper) can be sold as vault cosmetics; they never change his behaviour.

## Multiplayer and social

Multiplayer starts asynchronous, because it works with a small player base and on patchy mobile connections; live play is layered on top.

| Mode | How it works | Phase |
| --- | --- | --- |
| Async raids | Matchmaking serves vaults near your Crown rating (trophies). Raid any time; the owner gets the replay | Vertical slice |
| Friend and revenge raids | Raid a friend's vault from a shared link, or hit back at whoever robbed you | Launch |
| Heist Race | Live 1v1: both players raid the same vault at once, each seeing the other as a ghost. First out with the most loot wins | Post-launch |
| Guilds | Up to 30 players. Members donate wax, share vault tips and chat | Launch |
| Guild Heist | Weekly event: each guild builds one giant shared vault together, then raids rival guilds' vaults over the weekend | Post-launch |

Heist Race only needs each player's position relayed a few times a second, because the vault itself is deterministic. That keeps the netcode light.

## Shareability

The vault share link is the main growth engine: a challenge from a friend is the strongest install reason a game can have.

- **"Can you crack my vault?" links:** every published vault gets a short link and QR code. Tapping it opens the app straight into that raid, or the store page if the app isn't installed (deferred deep link).
- **Auto clips:** every raid records a 10 to 15 second vertical clip that cuts to the best moment, such as a slow-mo snuff, a last-second escape or a near miss. One tap shares it to TikTok, Reels, Shorts or WhatsApp, with a vault link watermark.
- **Snuff counter:** a vault's public card shows how many Wicks it has snuffed. It is built to be screenshotted.
- **Daily Vault:** one designer-made vault per day, the same for everyone. Players share a spoiler-free result card showing stars, time and flickers left.
- **Builder spotlights:** the hardest and funniest vaults each week are featured in-game, which gives builders a reason to share.

## Progression and monetisation

Money buys time, style and convenience; skill alone decides raids. Every paid item is either cosmetic or something a free player can earn by playing, just more slowly.

**Fairness rules**

- Trap and guardian stats, Wick's 3 flickers and raid tools are identical for every player at the same Vault Level, whatever they have spent.
- Nothing bought or earned from ads can be used inside a raid: no extra flickers, no revives, no extra time.
- Raising Vault Level needs **stars earned in raids** as well as gold. Payers can speed up building, but they can't skip playing.
- Matchmaking uses Vault Level and Crown rating, never spend.
- Design target: a spender reaches each Vault Level at most about twice as fast as a daily free player.

**Currencies**

| Currency | Earned from | Spent on |
| --- | --- | --- |
| Gold | Raids, defence bonuses, Daily Vault | Building and upgrading traps, raising Vault Level |
| Wax | Raid stars, guild donations, achievements | Wick upgrades such as a bigger light radius, unlocked by Vault Level |
| Gems (premium) | Purchase, achievements, Heist Pass free track, rewarded ads | Cosmetics, speed-ups, Heist Pass |

**Paid offers**

Prices are indicative, in GBP.

| Offer | Price | Why it's worth buying | Why it's fair |
| --- | --- | --- | --- |
| Starter pack | £1.99, one time | Pip skin, 500 gems and a second workshop for building two upgrades at once | The second workshop can also be earned through achievements by about Vault Level 4 |
| Heist Pass | £4.99 per roughly 6-week season | Season skin (Twist first), vault theme, emotes, unique snuff effect and extra gold and wax across 40 tiers | The free track runs alongside it; extra resources still hit the star gates |
| Wick Club | £3.99 per month | Daily gems, ad rewards without watching ads, a cosmetic gold flame trim and a third workshop | Only convenience and style; cancel any time |
| Skins and themes | £2.99 to £7.99 | Drippy, Cinder, Ember and seasonal skins, plus vault themes such as crypt or sunken ship | Cosmetic only, same hitbox and silhouette |
| Gem packs | £0.99 to £49.99 | Flexible top-ups for speed-ups and cosmetics | Gems can't buy stats or in-raid help |

**Heist Pass**

The pass is a 40-tier ladder per season (about 6 weeks) with a free row and a premium row side by side. It should feel like a whole season of personality for £4.99, with enough gems back to fund the next one.

- **Climbing:** Heist XP comes from raids (more for 3 stars), successful defences, a daily first-win bonus and weekly heist challenges such as "escape 5 vaults without losing a flicker". Every tier costs the same XP, sized so about 30 minutes of play a day finishes the pass.
- **Late buyers:** buying mid-season instantly unlocks every premium reward already passed.
- **Past tier 40:** a repeatable bonus chest every few tiers keeps active players earning.
- **Pass+ (£9.99):** the pass plus 10 tiers up front.

| Reward | Free row | Premium row |
| --- | --- | --- |
| Season skin | None | Evolving skin, such as Twist at tier 1, gilded at tier 20 and an animated flame at tier 40, each with its own snuff animation |
| Vault theme | None | Full cosmetic reskin of rooms, traps and treasure, such as Frozen Crypt |
| Trap skins | One, at tier 40 | Several, such as a dragon-head Bellows or a skull Snuffer Bell |
| Vault taunts | None | Animations raiders see when they are snuffed in your vault |
| Emotes | One | Several |
| Clip extras | None | Frames, stickers and a gold watermark for shared clips |
| Profile flair | None | Name banner and guild badge |
| Gold and wax | Chests about every third tier | About 50% more across the season, still subject to star gates |
| Gems | About 150 | About 650, more than the pass costs |

Season items return to the shop after about a year at full price, so they stay special without punishing players who missed them.

**Achievements**

Achievements are a permanent collection that never resets: the pass covers the season, achievements cover long-term bragging rights.

| Example achievement | Reward |
| --- | --- |
| Snuff 100 raiders with your vault | Gems and a vault trophy |
| Escape with all 3 flickers 50 times | Gems and a profile title |
| Raid 500 vaults | Ember skin |
| Reach Vault Level 4 | Second workshop |
| Have a vault link opened by 10 friends | Gems and a clip frame |

Each achievement has bronze, silver and gold tiers, and completed ones show on the player's profile.

**Rewarded ads only**

There are no banners, no interstitials and no forced ads. Every ad is a clearly labelled, opt-in button that pays a reward, and nothing is lost by ignoring it.

| Placement | Reward | Daily cap |
| --- | --- | --- |
| Raid results screen | Double the loot from that raid | 3 |
| Upgrade timer | Cut 30 minutes off one upgrade | 3 |
| Daily Vault | One extra attempt | 1 |
| Shop | Free small chest of gold, wax or a few gems | 2 |

- Total cap of 9 ads a day, so ads never become a grind.
- Never shown mid-raid or offered to change a raid's outcome.
- Wick Club members get these rewards instantly without watching.
- Mediation through Unity LevelPlay or AppLovin MAX, with UK and EU consent handled through Google's UMP and Apple's tracking prompt; non-personalised ads for players who decline or are under 18.

## Art and audio direction

The look is chunky, saturated 3D cartoon in the spirit of Clash of Clans, with one signature twist: warm candlelight against cool, dark vaults.

- **Camera:** fixed 3/4 isometric view, slightly zoomed in during raids so Wick reads clearly on a phone.
- **Shapes and materials:** oversized proportions, thick bevelled stone, rounded wood, soft hand-painted textures and gentle rim lighting.
- **Lighting as gameplay:** vault rooms sit in deep blues and purples. Wick's flame casts a warm orange pool, so the player's light and the danger in the dark are always visible at a glance.
- **Wick:** a short, round candle body, large expressive eyes, wax drips for sweat and a flame that acts like hair, flaring and shrinking with his mood. Squash-and-stretch on every move.
- **Guardians:** big, soft and slightly dim-witted rather than scary, so failing is funny for all ages.
- **UI:** chunky buttons with thick outlines, gold-and-parchment panels and bold numbers.
- **Audio:** playful plucked-string and pizzicato music that drops to a heartbeat in tense moments. Signature sounds: flame crackle, a soft whimper when a flicker is lost, and a comic "fwump" plus smoke puff on a snuff.

## Tech architecture

The key decision is a deterministic, grid-based raid simulation with no physics engine. A raid becomes a seed plus an input log of a few hundred bytes, which makes replays, clips, cheat checks and ghost races all cheap.

| Component | Approach |
| --- | --- |
| Client | Unity with URP, portrait, low-poly isometric 3D with baked lighting plus a light-radius effect for Wick's flame |
| Raid simulation | Plain C# library on a fixed 30-tick timestep, integer maths, no Unity physics or random calls outside a seeded generator. Shared by client and server |
| Vault format | Compact JSON: grid size, tiles, traps and settings, plus the owner's clear-check input log |
| Backend | A managed game backend such as Nakama, PlayFab or Unity Gaming Services for accounts, matchmaking, guilds, leaderboards and storage |
| Validation | A headless .NET worker re-runs every published clear-check and every raid result from its input log before paying out loot |
| Live Heist Race | Relay positions and loot at about 10 updates a second; both clients run the same vault locally |
| Clips | Re-render the raid from its input log at the end, rather than recording the screen live, then encode a vertical MP4 with a native encoder plugin |
| Share links | Deferred deep links from a link service, opening the app at a vault ID |

Server re-simulation rejects impossible raids. It can't prove a human played, so it is backed by single-use raid tokens, attempt limits, anomaly detection and platform integrity checks.

## Vertical slice and roadmap

The vertical slice must prove one thing: that raiding a stranger's vault is fun and that people want to send their own vault to friends.

**Vertical slice scope**

- [ ] Wick with swipe movement, tiptoe, decoy and the 3-flicker flame and light radius
- [ ] The first 6 traps and guardians from the catalogue, plus braziers
- [ ] One vault size (9 x 13, 3 rooms) and a working editor with Test mode
- [ ] Clear-check publishing with server re-simulation
- [ ] Async raids with simple Crown matchmaking, seeded with 20 designer-made vaults
- [ ] Raid replay for the defender and a shareable auto clip
- [ ] Vault share links that open a raid directly
- [ ] Final-quality art for Wick, one vault theme and three guardians

**Roadmap phases**

1. **Feel prototype:** grey-box raid with movement, flame and 3 traps. Gate: raids feel tense and fair to playtesters.
2. **Vertical slice:** the scope above. Gate: playtesters share vault links without being asked.
3. **Soft launch:** one or two small markets, with guilds, friend raids, Daily Vault, 12 traps and the first season. Gate: day-1 and day-7 retention targets met.
4. **Global launch:** monetisation tuned, Heist Race and Guild Heist added in early live updates.

## Risks and open questions

| Risk | Mitigation |
| --- | --- |
| Mean vaults: technically beatable but miserable, driving raiders away | Match by vault snuff rate, let raiders skip a vault for free, and reward builders for raids attempted rather than only for snuffs |
| Empty world at launch with too few player vaults | Seed with designer vaults and bot-tuned variations; async play needs far fewer players than live modes |
| The editor feels fiddly on a phone | Room templates to start from, undo, and a strong Test button; playtest the editor as early as the raid |
| Determinism bugs between client and server | Keep the simulation in one shared C# library with integer maths, and run every raid log through the server in automated tests |
| "Wax Bandits" name or brand conflict found later | Initial store and trademark checks are clear; file a UK trademark application and secure the domain and social handles before launch marketing |

**Open questions**

- [ ] Final game name: decided as Wax Bandits; Wick remains the hero character's name
- [ ] Should raids be strictly grid-based, or allow free movement with a grid-based simulation underneath?
- [ ] Is a 60-second timer right, or should larger vaults get more time?
- [ ] Which backend to choose, based on cost at soft-launch scale?
