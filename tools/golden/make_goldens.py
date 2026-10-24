"""Source of truth for tests/golden/*.json.

Usage (from the repo root):
    python tools/golden/make_goldens.py            # rewrite every golden from the definitions below
    python tools/golden/rerecord.py                # then update hashes where outcomes still match

Each golden's expected outcome, ticks, stars, loot and expectedEvents are worked out by hand from
the sim rules (see its _note). Hashes are never written by hand: this script keeps the hash already
in the file (or zeros for a new one) and rerecord.py replaces it after checking everything else.

Standard library only; Python 3.8+.
"""
import base64
import json
import sys
from pathlib import Path

SIM_VERSION = 4
REPO = Path(__file__).resolve().parents[2]
GOLDEN_DIR = REPO / "tests" / "golden"

TILE = {".": 0, "E": 0, "#": 1, "D": 2, "O": 3, "~": 4}
TYPES = ["Dash", "RunStart", "RunStop", "TiptoeOn", "TiptoeOff", "Decoy"]
DIRS = ["N", "E", "S", "W"]
NO_HASH = "0000000000000000"


def vault(vault_id, rows, objects, level=1, hoard=None):
    h, w = len(rows), len(rows[0])
    assert all(len(r) == w for r in rows), vault_id
    entrance = next((r.index("E"), y) for y, r in enumerate(rows) if "E" in r)
    doc = {
        "schema": 1, "vaultId": vault_id, "version": 1, "vaultLevel": level, "theme": "crypt",
        "grid": {"w": w, "h": h}, "_map": rows,
        "tiles": base64.b64encode(bytes(TILE[c] for r in rows for c in r)).decode(),
        "entrance": list(entrance), "objects": objects,
    }
    if hoard:
        doc["hoard"] = hoard
    return doc


def grow(rows, objects, hoard=None, cols=3, rows_after=10, add_rows=3):
    """Widens a 9 x 13 map to 12 x 16 for the M10 grid: inserts `cols` columns before the east
    wall (floor where the room runs into it, wall elsewhere) and repeats row `rows_after`
    `add_rows` times. Objects, patrols and the hoard move with the tiles they stood on."""
    east = len(rows[0]) - 1
    wide = [r[:east] + ("." if r[east - 1] == "." else "#") * cols + r[east:] for r in rows]
    tall = wide[:rows_after + 1] + [wide[rows_after]] * add_rows + wide[rows_after + 1:]

    def move(x, y):
        return x + (cols if x >= east else 0), y + (add_rows if y > rows_after else 0)

    moved = []
    for o in objects:
        o = dict(o)
        o["x"], o["y"] = move(o["x"], o["y"])
        if "patrol" in o:
            o["patrol"] = [list(move(px, py)) for px, py in o["patrol"]]
        moved.append(o)
    if hoard:
        x0, y0 = move(hoard[0], hoard[1])
        x1, y1 = move(hoard[2], hoard[3])
        hoard = [x0, y0, x1 + (cols if hoard[2] == east - 1 else 0), y1]
    return tall, moved, hoard


def cmds(*items):
    out = []
    for item in items:
        c = {"tick": item[0], "type": item[1]}
        if item[1] in ("Dash", "RunStart"):
            c["dir"] = item[2]
        out.append(c)
    return out


def encode_log(commands):
    """Independent implementation of InputLog.Encode (varint delta + type | dir << 3)."""
    out = bytearray()
    prev = 0
    for c in commands:
        delta = c["tick"] - prev
        while delta >= 0x80:
            out.append((delta & 0x7F) | 0x80)
            delta >>= 7
        out.append(delta)
        d = DIRS.index(c["dir"]) if "dir" in c else 0
        out.append(TYPES.index(c["type"]) | (d << 3))
        prev = c["tick"]
    return base64.b64encode(bytes(out)).decode()


def raid(note, seed, v, commands, outcome, ticks, stars, loot, events, use_base64=False):
    doc = {"_note": note, "simVersion": SIM_VERSION, "seed": seed, "vault": v}
    if use_base64:
        doc["_commands"] = commands  # readable copy; the sim reads "log"
        doc["log"] = encode_log(commands)
    else:
        doc["commands"] = commands
    doc["expected"] = {"outcome": outcome, "ticks": ticks, "stars": stars, "loot": loot, "hash": NO_HASH}
    doc["expectedEvents"] = events
    return doc


# ---- Maps ------------------------------------------------------------------------------------

ROOMS = [
    "#########",
    "#...#...#",
    "#...#...#",
    "#.......#",
    "###.#####",
    "#.......#",
    "#.......#",
    "#.......#",
    "#####.###",
    "#.......#",
    "#.......#",
    "#.......#",
    "####E####",
]

HAZARDS = [
    "#########",
    "#.~~~~~.#",
    "#.~...~.#",
    "#.~.O.~.#",
    "#.......#",
    "####D####",
    "#.......#",
    "#.......#",
    "#.......#",
    "#.......#",
    "#.......#",
    "#.......#",
    "####E####",
]

MAZE = [
    "#########",
    "#.......#",
    "#.##.##.#",
    "#.......#",
    "#.#####.#",
    "#...#...#",
    "#.#.#.#.#",
    "#.#...#.#",
    "#.#####.#",
    "#.......#",
    "###.#.###",
    "#.......#",
    "####E####",
]

OPEN = ["#########"] + ["#.......#"] * 11 + ["####E####"]

HEIST = [
    "#########",
    "#.......#",
    "#.......#",
    "###.#####",
    "#.......#",
    "#.......#",
    "#.......#",
    "#.......#",
    "#.......#",
    "#.......#",
    "#.......#",
    "#.......#",
    "####E####",
]

# ---- Goldens ---------------------------------------------------------------------------------

GOLDENS = {
    # M1: movement.
    "escape-run.json": raid(
        "Run north into the wall at (4,9), sidestep east, run north through the gap to (5,5), "
        "run back south to (5,11), dash west then south onto the entrance. Escapes at the end of tick 159.",
        55021,
        vault("v_golden_rooms", ROOMS, []),
        cmds(
            (0, "RunStart", "N"),   # (4,11) t9, (4,10) t19, (4,9) t29, blocked by (4,8)
            (30, "Dash", "E"),      # cancels the held run; (5,9) t34
            (35, "RunStart", "N"),  # (5,8) t44 ... (5,5) t74, blocked by (5,4)
            (80, "RunStop"),
            (90, "RunStart", "S"),  # (5,6) t99 ... (5,11) t149, blocked by (5,12)
            (150, "Dash", "W"),     # (4,11) t154
            (155, "Dash", "S"),     # (4,12) t159 -> Escaped
        ),
        "Escaped", 160, 1, 0,
        {"FlickerLost": [], "TreasureTaken": []},
    ),
    "timeout-wander.json": raid(
        "Run north through the door, across the pit and onto the ice (all walkable in M1), "
        "then a tiptoe dash west and a decoy, and never come back. Times out at 1800 ticks.",
        99120,
        vault("v_golden_hazards", HAZARDS, [
            {"id": 7, "type": "treasure", "x": 4, "y": 2, "value": 250},
            {"id": 2, "type": "drip_pipe", "x": 6, "y": 8, "rot": 1, "cycle": 45, "offset": 10},
        ]),
        cmds(
            (0, "RunStart", "N"),   # (4,3) t89; (4,2) occupied t94: pile taken, slowed 10%
            (120, "TiptoeOn"),
            (130, "Dash", "W"),
            (140, "Decoy"),
            (141, "RunStop"),
        ),
        "TimedOut", 1800, 2, 0,
        {"TreasureTaken": [94], "FlickerLost": []},
    ),
    "walls-tiptoe.json": raid(
        "Dash into a wall, run into a wall, tiptoe dashes west and north, decoy, run north into a wall, "
        "run south into a wall, then dash east and south onto the entrance. Escapes at the end of tick 79. "
        "Stores its log as base64 (readable copy in _commands).",
        7,
        vault("v_golden_maze", MAZE, [
            {"id": 3, "type": "treasure", "x": 4, "y": 7, "value": 400},
            {"id": 1, "type": "night_owl", "x": 1, "y": 3, "patrol": [[1, 3], [7, 3], [7, 1]]},
        ]),
        cmds(
            (0, "Dash", "E"),       # (5,12) is wall: only turns
            (1, "RunStart", "N"),   # (4,11) t10, blocked by (4,10)
            (12, "TiptoeOn"),
            (13, "Dash", "W"),      # tiptoe dash: (3,11) t22
            (23, "Dash", "N"),      # tiptoe dash: (3,10) t32
            (33, "Decoy"),
            (34, "TiptoeOff"),
            (35, "RunStart", "N"),  # (3,9) t44, blocked by (3,8)
            (50, "RunStart", "S"),  # (3,10) t59, (3,11) t69, blocked by (3,12)
            (70, "Dash", "E"),      # (4,11) t74
            (75, "Dash", "S"),      # (4,12) t79 -> Escaped
        ),
        "Escaped", 80, 1, 0,
        {"GuardianAlerted": [], "FlickerLost": [], "TreasureTaken": []},
        use_base64=True,
    ),

    # M2: traps, guardians, pickups, scoring.
    "drip-dash.json": raid(
        "Drip Pipe at (4,10), cycle 30: drips on ticks 0-5 and 30-35. Dashes north: (4,10) is "
        "occupied on ticks 13-17, between drips. Waits at (4,9); the drip at tick 30 fires next to "
        "him (NearMiss 30). Dashes back south at 30, mistimed: occupied (4,10) on tick 32, hit. "
        "Home on tick 44 with 2 flickers.",
        4101,
        vault("v_golden_drip", OPEN, [{"id": 1, "type": "drip_pipe", "x": 4, "y": 10, "cycle": 30}], level=3),
        cmds((6, "Dash", "N"), (11, "Dash", "N"), (16, "Dash", "N"),
             (30, "Dash", "S"), (35, "Dash", "S"), (40, "Dash", "S")),
        "Escaped", 45, 0, 0,
        {"TrapFired": [0, 30], "NearMiss": [30], "FlickerLost": [32]},
    ),
    "bellows-crate.json": raid(
        "Bellows at (7,9) facing west, cycle 60: gusts on ticks 0-14, 60-74, 120-134 along row 9 "
        "until the crate at (3,9). Runs north and stops on (4,9) (occupied from tick 24): the tick-60 "
        "gust hits and dims him. Goes round via row 10 to shelter at (2,9) behind the crate "
        "(arrives tick 80); the tick-120 gust misses. Home on tick 164.",
        4102,
        vault("v_golden_bellows", OPEN, [
            {"id": 1, "type": "bellows", "x": 7, "y": 9, "rot": 3, "cycle": 60},
            {"id": 2, "type": "crate", "x": 3, "y": 9},
        ], level=3),
        cmds((0, "RunStart", "N"), (25, "RunStop"),
             (61, "Dash", "S"), (66, "Dash", "W"), (71, "Dash", "W"), (76, "Dash", "N"),
             (140, "Dash", "S"), (145, "Dash", "E"), (150, "Dash", "E"), (155, "Dash", "S"), (160, "Dash", "S")),
        "Escaped", 165, 0, 0,
        {"TrapFired": [0, 60, 120], "NearMiss": [], "FlickerLost": [60]},
    ),
    "spikes-bell.json": raid(
        "Spike Floor at (4,10), cycle 60: raised ticks 0-19 and 60-79, warning rumble at 45. Steps "
        "onto it at tick 42 (occupied), hears the warning at 45, is hit when it rises at 60. Moves "
        "on north and steps on the Snuffer Bell plate at (4,8): occupied on tick 72, instant snuff.",
        4103,
        vault("v_golden_spikes", OPEN, [
            {"id": 1, "type": "spike_floor", "x": 4, "y": 10, "cycle": 60},
            {"id": 2, "type": "snuffer_bell", "x": 4, "y": 8},
        ], level=3),
        cmds((20, "Dash", "N"), (40, "Dash", "N"), (61, "Dash", "N"), (70, "Dash", "N")),
        "Snuffed", 73, 0, 0,
        {"TrapWarning": [45], "TrapFired": [0, 60, 72], "FlickerLost": [60], "Snuffed": [72]},
    ),
    "owl-moths-decoy.json": raid(
        "Night Owl guarding (4,7) facing south; Wax Moths at (1,8). One dash north puts the owl "
        "inside Wick's light: alerted on tick 4 (hand-worked). He drops a decoy at (4,11) on tick 5 (it "
        "burns until tick 93) and slips west to (2,11). The decoy pulls the owl and the moths; when it burns "
        "out the owl walks back north and the moths drift onto Wick, draining a flicker on tick 246 "
        "(contact from tick 187; inspected against the 25 ticks/tile drift, not hand-traced). He then "
        "dashes home: escapes on tick 274 (hand-worked).",
        4104,
        vault("v_golden_owl", OPEN, [
            {"id": 1, "type": "night_owl", "x": 4, "y": 7, "rot": 2},
            {"id": 2, "type": "wax_moths", "x": 1, "y": 8},
        ], level=3),
        cmds((0, "Dash", "N"), (5, "Decoy"), (6, "Dash", "W"), (11, "Dash", "W"),
             (260, "Dash", "E"), (265, "Dash", "E"), (270, "Dash", "S")),
        "Escaped", 275, 0, 0,
        {"GuardianAlerted": [4], "FlickerLost": [246]},
    ),
    "full-heist.json": raid(
        "Hoard is rows 1-2. Runs up column 3; the Drip Pipe at (3,5) (ticks 60-65) catches him on "
        "tick 64. Reaches the hoard, takes the 300 pile at (3,1) on tick 104 (now 10% slower), "
        "relights at the wall brazier above (4,1) on tick 113 and takes the 200 pile at (5,1) on "
        "tick 119 (20% slower). Runs home through the drip's gap and escapes with 3 flickers: "
        "3 stars, 500 loot.",
        4105,
        vault("v_golden_heist", HEIST, [
            {"id": 1, "type": "drip_pipe", "x": 3, "y": 5, "cycle": 60},
            {"id": 2, "type": "treasure", "x": 3, "y": 1, "value": 300},
            {"id": 3, "type": "treasure", "x": 5, "y": 1, "value": 200},
            {"id": 4, "type": "brazier", "x": 4, "y": 0},
        ], level=3, hoard=[1, 1, 7, 2]),
        cmds((0, "Dash", "N"), (5, "Dash", "W"), (10, "RunStart", "N"),
             (111, "Dash", "E"), (117, "Dash", "E"),
             (123, "RunStart", "W"), (140, "RunStart", "S"),
             (300, "Dash", "E"), (310, "Dash", "S")),
        "Escaped", 317, 3, 500,
        {"FlickerLost": [64], "TreasureTaken": [104, 119], "BrazierUsed": [113]},
    ),

    # M10: new traps and guardians.
    "ice-slide.json": raid(
        "Ice on (2,1)-(4,1). A dash east reaches the ice on tick 4 and slides at 250 a tick "
        "(4 ticks a tile): (3,1) t8, (4,1) t12, then off the ice onto (5,1) t16, where he stops. "
        "A dash west reaches the ice on tick 24 and slides home: (3,1) t28, (2,1) t32, and onto "
        "the entrance on tick 36, which counts as walking out. 1 star (all flickers), no loot.",
        2026,
        vault("v_golden_ice", [
            "#######",
            "#E~~~.#",
            "#######",
        ], [], level=3),
        cmds((0, "Dash", "E"), (20, "Dash", "W")),
        "Escaped", 37, 1, 0,
        {"FlickerLost": []},
    ),
    "axe-timing.json": raid(
        "A Swing Axe over (3,1), cycle 60: its blade is live there on ticks 0-5 and 30-35 of each "
        "cycle. Wick dashes to (2,1) (t4), then onto the axe's tile (his on t8, arriving t10), "
        "waits, and is caught by the return swing on tick 30. He dashes on to (4,1) inside his "
        "invulnerability, then comes back across on safe ticks (on the axe's tile t72) and walks "
        "out on tick 86 with 2 flickers: 0 stars, no loot.",
        4404,
        vault("v_golden_axe", [
            "#######",
            "#E....#",
            "#######",
        ], [
            {"id": 1, "type": "swing_axe", "x": 3, "y": 1, "rot": 1, "cycle": 60},
        ], level=4),
        cmds((0, "Dash", "E"), (6, "Dash", "E"), (31, "Dash", "E"),
             (70, "Dash", "W"), (76, "Dash", "W"), (82, "Dash", "W")),
        "Escaped", 87, 0, 0,
        {"FlickerLost": [30]},
    ),
    "mimic-bite.json": raid(
        "A Mimic Chest on (3,1). Wick dashes east; from tick 2 he occupies (2,1), beside it, and it "
        "winds up one tick at a time (1 on t2 ... 18 on t19). On tick 19 it bites for 2 flickers. "
        "Still beside it on tick 20, it starts winding up again (a second alert), but he is away by "
        "tick 22 and walks out on tick 24 with 1 flicker.",
        5115,
        vault("v_golden_mimic", [
            "#######",
            "#E....#",
            "#######",
        ], [
            {"id": 1, "type": "mimic_chest", "x": 3, "y": 1},
        ], level=5),
        cmds((0, "Dash", "E"), (20, "Dash", "W")),
        "Escaped", 25, 0, 0,
        {"FlickerLost": [19], "GuardianAlerted": [2, 20]},
    ),
    "troll-tiptoe.json": raid(
        "A Sleepy Troll on (3,1). Tiptoeing halves Wick's light to 2 tiles, below the 3 that wakes "
        "it, so he creeps past and back at tiptoe speed (10 ticks a tile): (2,1) t9, onto the "
        "troll's tile t19, (4,1) t29, (5,1) t39, then home: (4,1) t49, (3,1) t59, (2,1) t69, and out "
        "on tick 79. It never wakes. 1 star (all flickers), no loot.",
        6116,
        vault("v_golden_troll", [
            "#######",
            "#E....#",
            "#######",
        ], [
            {"id": 1, "type": "sleepy_troll", "x": 3, "y": 1},
        ], level=6),
        cmds((0, "TiptoeOn"), (0, "Dash", "E"), (10, "Dash", "E"), (20, "Dash", "E"), (30, "Dash", "E"),
             (40, "Dash", "W"), (50, "Dash", "W"), (60, "Dash", "W"), (70, "Dash", "W")),
        "Escaped", 80, 1, 0,
        {"GuardianAlerted": [], "FlickerLost": []},
    ),
    "fan-push.json": raid(
        "A fan on (4,1), cycle 120: it faces north (into the wall) for ticks 0-29, then east, south "
        "and west a quarter each, blowing 3 tiles for the first 10 ticks of each quarter. Wick dashes "
        "east past it to (6,1) by tick 24. At tick 30 it turns east and blows: he is pushed to (7,1) "
        "(knockback, arriving t34); at rest in the wind again that same tick, he is pushed on to (8,1) "
        "(t38), and the gust ends at t40. He dashes home (the fan faces south, at the wall, while he passes) and walks out "
        "on tick 74. No flickers lost: 1 star.",
        7117,
        vault("v_golden_fan", [
            "############",
            "#E.........#",
            "############",
        ], [
            {"id": 1, "type": "fan", "x": 4, "y": 1, "rot": 0, "cycle": 120},
        ], level=7),
        cmds((0, "Dash", "E"), (5, "Dash", "E"), (10, "Dash", "E"), (15, "Dash", "E"), (20, "Dash", "E"),
             (40, "Dash", "W"), (45, "Dash", "W"), (50, "Dash", "W"), (55, "Dash", "W"),
             (60, "Dash", "W"), (65, "Dash", "W"), (70, "Dash", "W")),
        "Escaped", 75, 1, 0,
        {"KnockedBack": [30, 34], "FlickerLost": []},
    ),
}


def main(out_dir):
    out_dir.mkdir(parents=True, exist_ok=True)
    for name, doc in GOLDENS.items():
        existing = GOLDEN_DIR / name
        if existing.exists():
            # Keep the recorded hash; rerecord.py is the only thing that changes it.
            doc["expected"]["hash"] = json.loads(existing.read_text(encoding="utf-8"))["expected"]["hash"]
        (out_dir / name).write_text(json.dumps(doc, indent=2) + "\n", encoding="utf-8", newline="\n")
        print("wrote", name)


if __name__ == "__main__":
    main(Path(sys.argv[1]) if len(sys.argv) > 1 else GOLDEN_DIR)
