"""Fails if game code uses any banner or interstitial ad API (Wick shows rewarded ads only).

Usage (from the repo root): python tools/check_rewarded_only.py
Run by CI. Scans our own code (unity/Assets, server, web), not third-party packages: the
LevelPlay SDK can show other formats, but nothing of ours ever asks it to.
"""
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCAN = ["unity/Assets", "server", "web"]
SKIP_DIRS = {"bin", "obj", "node_modules", "Library", "Temp"}
EXTENSIONS = (".cs", ".java", ".js", ".m", ".mm", ".kt")
FORBIDDEN = re.compile(
    r"LevelPlayBannerAd|LevelPlayInterstitialAd|LevelPlayNativeAd|BannerView|InterstitialAd|"
    r"ShowInterstitial|LoadInterstitial|LoadBanner|DisplayBanner|IronSourceBanner|IronSource\.Agent\.(load|display)Banner",
    re.IGNORECASE)
ALLOWED_FILES = {os.path.normpath("tools/check_rewarded_only.py")}

problems = []
for top in SCAN:
    for folder, dirs, files in os.walk(os.path.join(ROOT, top)):
        dirs[:] = [d for d in dirs if d not in SKIP_DIRS]
        for name in files:
            if not name.endswith(EXTENSIONS):
                continue

            path = os.path.join(folder, name)
            rel = os.path.normpath(os.path.relpath(path, ROOT))
            if rel in ALLOWED_FILES:
                continue

            with open(path, encoding="utf-8", errors="ignore") as f:
                for number, line in enumerate(f, 1):
                    if FORBIDDEN.search(line):
                        problems.append(f"{rel}:{number}: {line.strip()}")

if problems:
    print("Banner or interstitial ad code found (Wick is rewarded-only):")
    print("\n".join(problems))
    sys.exit(1)

print("Rewarded-only check passed: no banner or interstitial ad code.")
