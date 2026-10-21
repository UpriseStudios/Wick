"""Builds the stand-in Android app icon from the locked Wick reference render.

The art direction uses the hero render as-is for the app icon. This crops it so Wick's face
and flame sit inside the adaptive icon's safe zone (the central 66 of 108 dp) and writes:

  unity/Assets/_Wick/Art/Icons/app_icon_background.png  432 x 432, full bleed (adaptive back layer)
  unity/Assets/_Wick/Art/Icons/app_icon_foreground.png  432 x 432, transparent (adaptive front layer)
  docs/store/play_icon_512.png                          512 x 512, the Play listing's hi-res icon

Final icon art replaces these files at the same paths. Run from the repo root:
  python tools/icons/make_app_icon.py
"""

import os

from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SOURCE = os.path.join(ROOT, "docs", "art", "References", "wick_reference_v1.png")
OUT = os.path.join(ROOT, "unity", "Assets", "_Wick", "Art", "Icons")
STORE = os.path.join(ROOT, "docs", "store")

# The square of the reference that the launcher shows (72 of 108 dp): flame tip to satchel strap.
VISIBLE_CENTRE = (570, 460)
VISIBLE_SIZE = 700
TOP_PAD = 200  # the full layer reaches above the image, so the top is extended with blurred sky


def padded_source():
    src = Image.open(SOURCE).convert("RGB")
    w, h = src.size
    strip = src.crop((0, 0, w, 40)).resize((w, TOP_PAD + 40)).filter(ImageFilter.GaussianBlur(24))
    canvas = Image.new("RGB", (w, h + TOP_PAD))
    canvas.paste(strip, (0, 0))
    canvas.paste(src, (0, TOP_PAD))
    return canvas


def vignette(img, strength):
    size = img.size[0]
    mask = Image.new("L", img.size, 0)
    ImageDraw.Draw(mask).ellipse((-size * 0.15, -size * 0.15, size * 1.15, size * 1.15), fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(size * 0.12))
    dark = Image.new("RGB", img.size, (12, 7, 4))
    shaded = Image.composite(img, dark, mask)
    return Image.blend(img, shaded, strength)


def crop(src, size):
    cx, cy = VISIBLE_CENTRE[0], VISIBLE_CENTRE[1] + TOP_PAD
    half = size // 2
    return src.crop((cx - half, cy - half, cx + half, cy + half))


def main():
    os.makedirs(OUT, exist_ok=True)
    os.makedirs(STORE, exist_ok=True)
    src = padded_source()

    layer = crop(src, VISIBLE_SIZE * 108 // 72).resize((432, 432), Image.LANCZOS)
    vignette(layer, 0.55).save(os.path.join(OUT, "app_icon_background.png"))
    Image.new("RGBA", (432, 432), (0, 0, 0, 0)).save(os.path.join(OUT, "app_icon_foreground.png"))

    visible = vignette(crop(src, VISIBLE_SIZE), 0.45)
    visible.resize((512, 512), Image.LANCZOS).save(os.path.join(STORE, "play_icon_512.png"))
    print("wrote", OUT, "and", STORE)


if __name__ == "__main__":
    main()
