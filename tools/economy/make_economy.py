"""Writes Wick's default economy config (first-pass numbers, tuned later in Remote Config).

Usage (from the repo root): python tools/economy/make_economy.py

Outputs, all the same document:
  unity/Assets/_Wick/Resources/Economy/economy-default.json  bundled with the game (offline fallback)
  (Cloud Code embeds that same file as its fallback)
  server/remote-config/economy.rc                            deploy with: ugs deploy server/remote-config

Fairness: every grant is currency, cosmetics, time or workshop slots. Nothing here is read by
RaidSim. The only thing that will change a raid value is the Lantern (light radius), and each
Lantern level needs a Vault Level, which needs raid stars, so paying only gets there sooner.
"""
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

# Raising Vault Level: gold, build time, and lifetime raid stars (the star gate).
VAULT_LEVELS = [
    # to level, gold, minutes, stars
    (2, 400, 2, 5),
    (3, 1_200, 15, 15),
    (4, 3_000, 60, 35),
    (5, 6_000, 180, 65),
    (6, 10_000, 360, 105),
    (7, 16_000, 600, 155),
    (8, 24_000, 960, 215),
    (9, 35_000, 1_440, 285),
    (10, 50_000, 2_160, 365),
]

# Lantern: a slightly bigger light radius per level (raid effect wired in M10). Each level needs a
# Vault Level, so the most anyone can have at a given Vault Level is the same for everyone.
LANTERN = [
    # level, wax, minutes, min vault level, light bonus (sub-tile units, 1 tile = 1000)
    (1, 100, 10, 2, 150),
    (2, 300, 60, 4, 300),
    (3, 700, 180, 6, 450),
    (4, 1_500, 480, 8, 600),
    (5, 3_000, 960, 10, 750),
]


def pass_rewards():
    """40 tiers. Free: a chest about every third tier. Premium: cosmetics, more resources, gems."""
    skins = {1: "skin_twist", 20: "skin_twist_gilded", 40: "skin_twist_flame"}
    cosmetics = {5: "emote_bow", 10: "theme_frozen_crypt", 15: "trapskin_dragon_bellows", 25: "taunt_wax_laugh",
                 30: "trapskin_skull_bell", 35: "snuff_effect_spiral"}
    rewards = []
    for tier in range(1, 41):
        free = {}
        if tier % 3 == 0:
            free = {"gold": 150 + 10 * tier, "wax": 20 + 2 * tier}
        if tier == 40:
            free = {"cosmetics": ["trapskin_rusty_pipe"], "gems": 30}
        elif tier in (12, 24, 36):
            free["gems"] = 40
        premium = {"gold": 100 + 8 * tier, "wax": 15 + 2 * tier} if tier % 2 == 0 else {"gems": 30}
        if tier in skins:
            premium = {"cosmetics": [skins[tier]]}
        elif tier in cosmetics:
            premium = {"cosmetics": [cosmetics[tier]], "gems": 20}
        rewards.append({"tier": tier, "free": free, "premium": premium})
    return rewards


ECONOMY = {
    "version": 1,
    "loot": {
        "capPerLevel": 200,
        "defenceBonusPerLevel": 10,
        "maxLossPercent": 20,
        "consolationPerLevel": 10,
        "waxPerStar": 10,
        "shieldMinutes": 120,
    },
    "vaultLevels": [{"level": l, "gold": g, "minutes": m, "stars": s} for (l, g, m, s) in VAULT_LEVELS],
    "lantern": [{"level": l, "wax": w, "minutes": m, "minVaultLevel": v, "lightBonus": b} for (l, w, m, v, b) in LANTERN],
    "workshops": {"base": 1, "max": 3},
    "speedUp": {"gemsPerHour": 20, "minGems": 1},
    "ads": {
        "dailyTotal": 9,
        "placements": [
            {"id": "raid_double_loot", "daily": 3, "windowMinutes": 10},
            {"id": "upgrade_speedup", "daily": 3, "minutes": 30},
            {"id": "daily_vault_retry", "daily": 1},
            {"id": "shop_chest", "daily": 2, "grant": {"gold": 120, "wax": 25, "gems": 3}},
        ],
    },
    "products": [
        {"id": "starter_pack", "kind": "nonConsumable", "price": "1.99",
         "grant": {"gems": 500, "workshops": 1, "cosmetics": ["skin_pip"]}},
        {"id": "heist_pass_s1", "kind": "nonConsumable", "price": "4.99", "grant": {"passPremium": "s1"}},
        {"id": "wick_club", "kind": "subscription", "price": "3.99", "grant": {"club": True}},
        {"id": "skin_drippy", "kind": "nonConsumable", "price": "2.99", "grant": {"cosmetics": ["skin_drippy"]}},
        {"id": "skin_cinder", "kind": "nonConsumable", "price": "4.99", "grant": {"cosmetics": ["skin_cinder"]}},
        {"id": "skin_ember", "kind": "nonConsumable", "price": "7.99", "grant": {"cosmetics": ["skin_ember"]}},
        {"id": "gems_80", "kind": "consumable", "price": "0.99", "grant": {"gems": 80}},
        {"id": "gems_500", "kind": "consumable", "price": "4.99", "grant": {"gems": 500}},
        {"id": "gems_1200", "kind": "consumable", "price": "9.99", "grant": {"gems": 1200}},
        {"id": "gems_2600", "kind": "consumable", "price": "19.99", "grant": {"gems": 2600}},
        {"id": "gems_7000", "kind": "consumable", "price": "49.99", "grant": {"gems": 7000}},
    ],
    "club": {"dailyGems": 15, "workshops": 1, "cosmetics": ["flame_trim_gold"]},
    "pass": {
        "season": "s1",
        "startsUtc": "2026-10-01T00:00:00Z",
        "endsUtc": "2026-11-15T00:00:00Z",
        "tiers": 40,
        "xpPerTier": 1000,
        "xp": {"raid": 40, "threeStarBonus": 40, "defence": 15, "dailyFirstWin": 150},
        "rewards": pass_rewards(),
    },
    "achievements": [
        {"id": "vault_snuffer", "stat": "snuffs", "title": "Snuff raiders with your vault",
         "tiers": [10, 50, 100], "grants": [{"gems": 10}, {"gems": 30}, {"gems": 60, "cosmetics": ["trophy_snuffer"]}]},
        {"id": "flawless", "stat": "perfectEscapes", "title": "Escape with all 3 flickers",
         "tiers": [5, 20, 50], "grants": [{"wax": 50}, {"gems": 25}, {"gems": 50, "cosmetics": ["title_flawless"]}]},
        {"id": "raider", "stat": "raids", "title": "Raid vaults",
         "tiers": [50, 200, 500], "grants": [{"gold": 500}, {"gems": 40}, {"cosmetics": ["skin_ember"]}]},
        {"id": "builder", "stat": "vaultLevel", "title": "Raise your Vault Level",
         "tiers": [2, 3, 4], "grants": [{"gold": 300}, {"wax": 100}, {"workshops": 1}]},
        {"id": "word_of_mouth", "stat": "linkOpens", "title": "Have your vault link opened by friends",
         "tiers": [3, 10, 25], "grants": [{"gems": 15}, {"gems": 40, "cosmetics": ["clip_frame_gold"]}, {"gems": 80}]},
    ],
}


def write(path, text):
    full = os.path.join(ROOT, path)
    os.makedirs(os.path.dirname(full), exist_ok=True)
    with open(full, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)
    print("wrote", path)


doc = json.dumps(ECONOMY, indent=2)
write("unity/Assets/_Wick/Resources/Economy/economy-default.json", doc + "\n")
write("server/remote-config/economy.rc", json.dumps({
    "$schema": "https://ugs-config-schemas.unity3d.com/v1/remote-config.schema.json",
    "entries": {"economy": ECONOMY},
}, indent=2) + "\n")
