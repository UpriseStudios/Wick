"""Source of truth for the hand-built dev vaults in unity/Assets/_Wick/Resources/DevVaults.

Usage (from the repo root):
    python tools/golden/make_dev_vaults.py

Each vault is 9 x 13 with three rooms, built from an ASCII map (see make_goldens.vault).
Standard library only; Python 3.8+.
"""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from make_goldens import REPO, vault  # noqa: E402

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
        {"id": 4, "type": "treasure", "x": 4, "y": 2, "value": 300},
        {"id": 5, "type": "treasure", "x": 1, "y": 1, "value": 150},
        {"id": 6, "type": "brazier", "x": 0, "y": 6},
    ], level=1, hoard=[1, 1, 7, 3]),

    # Sneak past a patrolling owl; a bellows sweeps the entry hall, crates give cover.
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
        {"id": 2, "type": "bellows", "x": 1, "y": 10, "rot": 1, "cycle": 75},
        {"id": 3, "type": "crate", "x": 4, "y": 9},
        {"id": 4, "type": "crate", "x": 4, "y": 11},
        {"id": 5, "type": "treasure", "x": 2, "y": 2, "value": 250},
        {"id": 6, "type": "treasure", "x": 6, "y": 2, "value": 250},
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
        {"id": 6, "type": "treasure", "x": 7, "y": 2, "value": 200},
        {"id": 7, "type": "treasure", "x": 1, "y": 1, "value": 100},
        {"id": 8, "type": "brazier", "x": 0, "y": 5},
    ], level=3, hoard=[4, 1, 7, 2]),
}


def main():
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    for name, doc in VAULTS.items():
        (OUT_DIR / (name + ".json")).write_text(json.dumps(doc, indent=2) + "\n", encoding="utf-8", newline="\n")
        print("wrote", name + ".json")


if __name__ == "__main__":
    main()
