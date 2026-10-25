"""Source of truth for the editor's room templates in
unity/Assets/_Wick/Resources/VaultTemplates.

Usage (from the repo root):
    python tools/golden/make_vault_templates.py

Each template is a valid Level 1 vault (12 x 16, entrance, hoard, one treasure pile, no traps,
a winding route of at least VaultRules.MinRouteTiles) for players to build on. Standard library only; Python 3.8+.
"""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from make_goldens import REPO, vault  # noqa: E402

OUT_DIR = REPO / "unity" / "Assets" / "_Wick" / "Resources" / "VaultTemplates"

TEMPLATES = {
    # Three halls up the middle, but the entry hall is a zigzag of corridors: the way in winds
    # back and forth before the straight run to the hoard.
    "1-three-halls": vault("v_template_halls", [
        "############",
        "#..........#",
        "#..........#",
        "#..........#",
        "####D#######",
        "#..........#",
        "#..........#",
        "#..........#",
        "#..........#",
        "####D#######",
        "####.......#",
        "##########.#",
        "#..........#",
        "#.##########",
        "#..........#",
        "##########E#",
    ], [{"id": 1, "type": "treasure", "x": 4, "y": 2, "value": 100}], hoard=[1, 1, 10, 3]),

    # The hoard hides in a side room off the top-right hall; the route winds right, left, right.
    "2-side-vault": vault("v_template_side", [
        "############",
        "#...#......#",
        "#...#......#",
        "#...D......#",
        "##########.#",
        "#..........#",
        "#..........#",
        "#D##########",
        "#..........#",
        "#..........#",
        "##########D#",
        "#..........#",
        "#..........#",
        "#..........#",
        "#..........#",
        "####E#######",
    ], [{"id": 1, "type": "treasure", "x": 2, "y": 2, "value": 100}], hoard=[1, 1, 3, 3]),

    # Four tall columns walked up, down and up again, with a side pocket off the first.
    "3-long-columns": vault("v_template_columns", [
        "############",
        "#..........#",
        "#..........#",
        "##########D#",
        "#..#..D..#.#",
        "#..#..#..#.#",
        "#..#..#..#.#",
        "#..#..#..#.#",
        "#..#..#..#.#",
        "#..#..#..#.#",
        "#..#..#..#.#",
        "#..#..#..#.#",
        "#..#..#..#.#",
        "#..#..#..#.#",
        "#..D..#..D.#",
        "####E#######",
    ], [{"id": 1, "type": "treasure", "x": 5, "y": 1, "value": 100}], hoard=[1, 1, 10, 2]),
}


def route(doc):
    """The shortest walk from the entrance to the hoard (VaultValidator.RouteTiles)."""
    rows, (ex, ey), (x0, y0, x1, y1) = doc["_map"], doc["entrance"], doc["hoard"]
    seen, frontier, steps = {(ex, ey)}, [(ex, ey)], 0
    while frontier:
        if any(x0 <= x <= x1 and y0 <= y <= y1 for x, y in frontier):
            return steps
        nxt = []
        for x, y in frontier:
            for n in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                if 0 <= n[1] < len(rows) and 0 <= n[0] < len(rows[0]) and rows[n[1]][n[0]] != "#" and n not in seen:
                    seen.add(n)
                    nxt.append(n)
        frontier, steps = nxt, steps + 1
    return -1


def main():
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    for name, doc in TEMPLATES.items():
        assert route(doc) >= 36, (name, route(doc))  # VaultRules.MinRouteTiles(1)
        (OUT_DIR / (name + ".json")).write_text(json.dumps(doc, indent=2) + "\n", encoding="utf-8", newline="\n")
        print("wrote", name + ".json", "route", route(doc))


if __name__ == "__main__":
    main()
