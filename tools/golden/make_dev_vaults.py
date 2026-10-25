"""Source of truth for the hand-built dev vaults in unity/Assets/_Wick/Resources/DevVaults.

Usage (from the repo root):
    python tools/golden/make_dev_vaults.py

Each vault is 12 x 16 with three rooms, built from an ASCII map (see make_goldens.vault) drawn at
9 x 13 and widened by make_goldens.grow.
Standard library only; Python 3.8+.
"""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from make_goldens import REPO, grow, vault  # noqa: E402

OUT_DIR = REPO / "unity" / "Assets" / "_Wick" / "Resources" / "DevVaults"

VAULTS = {
    # Learn the traps: a drip on the way in, two spike floors guarding the hoard door,
    # a brazier to recover a flicker.
    "1-training-crypt": vault("v_dev_training", [
        "#########",
        "#.......#",
        "#.......#",
        "#.......#",
        "####D####",
        "#.......#",
        "#.......#",
        "#.......#",
        "###.#####",
        "#.......#",
        "#.......#",
        "#.......#",
        "####E####",
    ], [
        {"id": 1, "type": "drip_pipe", "x": 3, "y": 9, "cycle": 45},
        {"id": 2, "type": "spike_floor", "x": 4, "y": 5, "cycle": 60, "offset": 20},
        {"id": 3, "type": "spike_floor", "x": 3, "y": 7, "cycle": 70},
        {"id": 4, "type": "treasure", "x": 4, "y": 2, "value": 100},
        {"id": 5, "type": "treasure", "x": 1, "y": 1, "value": 100},
        {"id": 6, "type": "brazier", "x": 0, "y": 6},
    ], level=1, hoard=[1, 1, 7, 3]),

    # Sneak past a patrolling owl; a wall bellows sweeps the entry hall, crates give cover.
    "2-owl-gallery": vault("v_dev_owl", [
        "#########",
        "#.......#",
        "#.......#",
        "#.......#",
        "###D#####",
        "#.......#",
        "#.......#",
        "#.......#",
        "#######.#",
        "#.......#",
        "#.......#",
        "#.......#",
        "####E####",
    ], [
        {"id": 1, "type": "night_owl", "x": 1, "y": 6, "rot": 1, "patrol": [[1, 6], [7, 6], [7, 5], [1, 5]]},
        {"id": 2, "type": "bellows", "x": 0, "y": 10, "rot": 1, "cycle": 75},  # in the west wall
        {"id": 3, "type": "crate", "x": 5, "y": 10},  # on the gust row: (6,10) and (7,10) are sheltered
        {"id": 4, "type": "crate", "x": 2, "y": 9},
        {"id": 5, "type": "treasure", "x": 2, "y": 2, "value": 200},
        {"id": 6, "type": "treasure", "x": 6, "y": 2, "value": 200},
        {"id": 7, "type": "treasure", "x": 4, "y": 1, "value": 200},
        {"id": 8, "type": "brazier", "x": 8, "y": 10},
    ], level=2, hoard=[1, 1, 7, 3]),

    # Wax Moths chase bright flames: tiptoe or decoy them. A Snuffer Bell guards a shortcut.
    "3-moth-den": vault("v_dev_moths", [
        "#########",
        "#..#....#",
        "#..#....#",
        "#.......#",
        "#.#####.#",
        "#.......#",
        "#.......#",
        "#.....#.#",
        "#.....#.#",
        "##.####.#",
        "#.......#",
        "#.......#",
        "####E####",
    ], [
        {"id": 1, "type": "wax_moths", "x": 2, "y": 6},
        {"id": 2, "type": "wax_moths", "x": 6, "y": 2},
        {"id": 3, "type": "snuffer_bell", "x": 7, "y": 9},
        {"id": 4, "type": "drip_pipe", "x": 7, "y": 6, "cycle": 40, "offset": 15},
        {"id": 5, "type": "treasure", "x": 5, "y": 1, "value": 300},
        {"id": 6, "type": "treasure", "x": 7, "y": 2, "value": 300},
        {"id": 7, "type": "treasure", "x": 1, "y": 1, "value": 300},
        {"id": 8, "type": "brazier", "x": 0, "y": 5},
    ], level=3, hoard=[4, 1, 7, 2]),
}


# Vault Level budgets and the 60% minimum from Level 2 (VaultRules), and what each object costs.
BUDGETS = [120, 160, 210, 250, 300, 340, 400, 440, 480, 540]
COSTS = {"drip_pipe": 10, "spike_floor": 12, "bellows": 15, "crate": 4, "snuffer_bell": 20, "night_owl": 30, "wax_moths": 25}


def widened(doc):
    """The maps below were drawn for the vertical slice's 9 x 13 grid; M10's smallest is 12 x 16.
    From Vault Level 2 a vault must use 60% of its budget, so Spike Floors fill the new east columns."""
    rows, objects, hoard = grow(doc["_map"], doc["objects"], doc.get("hoard"))
    level = doc["vaultLevel"]
    need = BUDGETS[level - 1] * 60 // 100 if level >= 2 else 0
    spent = sum(COSTS.get(o["type"], 0) for o in objects)
    used = {(o["x"], o["y"]) for o in objects}
    next_id = max(o["id"] for o in objects) + 1
    for y in range(len(rows) - 2, 0, -1):
        for x in (10, 9):
            if spent >= need:
                break
            if rows[y][x] == "." and (x, y) not in used:
                objects.append({"id": next_id, "type": "spike_floor", "x": x, "y": y, "cycle": 60, "offset": (next_id * 13) % 60})
                next_id += 1
                spent += COSTS["spike_floor"]
    return vault(doc["vaultId"], rows, objects, level=level, hoard=hoard)


def main():
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    for name, doc in VAULTS.items():
        doc = widened(doc)
        (OUT_DIR / (name + ".json")).write_text(json.dumps(doc, indent=2) + "\n", encoding="utf-8", newline="\n")
        print("wrote", name + ".json")


if __name__ == "__main__":
    main()
