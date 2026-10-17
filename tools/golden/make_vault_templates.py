"""Source of truth for the editor's room templates in
unity/Assets/_Wick/Resources/VaultTemplates.

Usage (from the repo root):
    python tools/golden/make_vault_templates.py

Each template is a valid Level 1 vault (9 x 13, 3 rooms, entrance, hoard, one treasure pile,
no traps) for players to build on. Standard library only; Python 3.8+.
"""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from make_goldens import REPO, vault  # noqa: E402

OUT_DIR = REPO / "unity" / "Assets" / "_Wick" / "Resources" / "VaultTemplates"

TEMPLATES = {
    # Three rooms stacked, doors in the middle: a straight run to the hoard.
    "1-three-halls": vault("v_template_halls", [
        "#########",
        "#.......#",
        "#.......#",
        "#.......#",
        "####D####",
        "#.......#",
        "#.......#",
        "#.......#",
        "#.......#",
        "####D####",
        "#.......#",
        "#.......#",
        "####E####",
    ], [{"id": 1, "type": "treasure", "x": 4, "y": 2, "value": 100}], hoard=[1, 1, 7, 3]),

    # The hoard hides in a side room off the top-right hall; the route winds left then right.
    "2-side-vault": vault("v_template_side", [
        "#########",
        "#...#...#",
        "#...#...#",
        "#...D...#",
        "#####...#",
        "#.......#",
        "#.......#",
        "#.......#",
        "#...#####",
        "#...D...#",
        "#...#...#",
        "#.......#",
        "####E####",
    ], [{"id": 1, "type": "treasure", "x": 2, "y": 2, "value": 100}], hoard=[1, 1, 3, 3]),

    # Two wings side by side between the entry hall and the hoard: pick a side.
    "3-twin-wings": vault("v_template_wings", [
        "#########",
        "#.......#",
        "#.......#",
        "###D#D###",
        "#...#...#",
        "#...#...#",
        "#...#...#",
        "#...#...#",
        "#...#...#",
        "##D###D##",
        "#.......#",
        "#.......#",
        "####E####",
    ], [{"id": 1, "type": "treasure", "x": 4, "y": 1, "value": 100}], hoard=[1, 1, 7, 2]),
}


def main():
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    for name, doc in TEMPLATES.items():
        (OUT_DIR / (name + ".json")).write_text(json.dumps(doc, indent=2) + "\n", encoding="utf-8", newline="\n")
        print("wrote", name + ".json")


if __name__ == "__main__":
    main()
