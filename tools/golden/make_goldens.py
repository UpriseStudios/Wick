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

SIM_VERSION = 3
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
