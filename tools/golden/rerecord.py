"""Re-record golden replay hashes after a deliberate sim rule change.

Usage (from the repo root, after bumping SimInfo.Version and running make_goldens.py):
    python tools/golden/rerecord.py

Replays every tests/golden/*.json with replay-cli. A file's hash is updated only when its outcome,
ticks, stars and loot still match the hand-checked expectations in the file; anything else is
reported and left alone, and the script exits with status 1. Event ticks (expectedEvents) are
checked by the golden tests, so run the tests afterwards.

Standard library only; Python 3.8+.
"""
import json
import re
import subprocess
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
GOLDEN_DIR = REPO / "tests" / "golden"
LINE = re.compile(r"^(\S+): outcome=(\w+) ticks=(\d+) stars=(\d+) loot=(\d+) hash=([0-9a-f]{16})", re.M)


def main():
    files = sorted(GOLDEN_DIR.glob("*.json"))
    run = subprocess.run(
        ["dotnet", "run", "--project", str(REPO / "tools" / "replay-cli"), "--"] + [str(f) for f in files],
        capture_output=True, text=True)
    if not LINE.search(run.stdout or ""):
        print(run.stdout + run.stderr)
        sys.exit("replay-cli produced no results")

    ok = True
    for line in run.stdout.splitlines():
        m = LINE.match(line)
        if not m:
            continue
        name, outcome, ticks, stars, loot, new_hash = m.groups()
        path = GOLDEN_DIR / name
        expected = json.loads(path.read_text(encoding="utf-8"))["expected"]

        problems = []
        if expected["outcome"] != outcome:
            problems.append(f"outcome {outcome}, expected {expected['outcome']}")
        if expected["ticks"] != int(ticks):
            problems.append(f"ticks {ticks}, expected {expected['ticks']}")
        if "stars" in expected and expected["stars"] != int(stars):
            problems.append(f"stars {stars}, expected {expected['stars']}")
        if "loot" in expected and expected["loot"] != int(loot):
            problems.append(f"loot {loot}, expected {expected['loot']}")
        if problems:
            ok = False
            print(f"{name}: NOT re-recorded: " + "; ".join(problems))
            continue

        old_hash = expected["hash"]
        if old_hash == new_hash:
            print(f"{name}: unchanged")
            continue
        text = path.read_text(encoding="utf-8").replace(f'"hash": "{old_hash}"', f'"hash": "{new_hash}"')
        path.write_text(text, encoding="utf-8", newline="\n")
        print(f"{name}: hash {old_hash} -> {new_hash}")

    sys.exit(0 if ok else 1)


if __name__ == "__main__":
    main()
