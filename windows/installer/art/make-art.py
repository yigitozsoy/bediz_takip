#!/usr/bin/env python3
"""The installer's pictures (BambuBar.iss: WizardImageFile, WizardSmallImageFile), one per Windows scale.

    python windows/installer/art/make-art.py      # needs Pillow and windows/src/BambuBar.Windows/Assets/AppIcon.png

beta keeps them plain, as Gamma Player's look: the welcome and finish pages' column is the wizard's own white with the
app icon (the Georgia beta on its gray square) in the middle; the other pages' header shows the icon alone. 24-bit
BMPs, which every Inno Setup 6 reads.
"""
import os

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", "..", ".."))
ICON = os.path.join(REPO, "windows", "src", "BambuBar.Windows", "Assets", "AppIcon.png")

# Inno Setup's sizes for 100, 125, 150, 175, 200, 225 and 250 % (WizardImageFile / WizardSmallImageFile).
TALL = [(164, 314), (192, 386), (246, 459), (273, 556), (328, 628), (355, 700), (410, 797)]
SMALL = [(55, 55), (64, 68), (83, 80), (92, 97), (110, 106), (119, 123), (138, 140)]
PAGE = (255, 255, 255)    # the modern wizard's page colour


def on_page(icon, w, h, side):
    canvas = Image.new("RGB", (w, h), PAGE)
    art = icon.resize((side, side), Image.LANCZOS)
    canvas.paste(art, ((w - side) // 2, (h - side) // 2), art)
    return canvas


def main():
    icon = Image.open(ICON).convert("RGBA")
    for w, h in TALL:
        on_page(icon, w, h, round(w * 0.42)).save(os.path.join(HERE, f"wizard-{w}x{h}.bmp"))
    for w, h in SMALL:
        on_page(icon, w, h, round(min(w, h) * 0.78)).save(os.path.join(HERE, f"small-{w}x{h}.bmp"))
    print("written", HERE)


if __name__ == "__main__":
    main()
