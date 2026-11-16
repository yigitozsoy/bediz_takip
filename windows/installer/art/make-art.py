#!/usr/bin/env python3
"""The installer's pictures (BambuBar.iss: WizardImageFile, WizardSmallImageFile and their dark twins).

    python windows/installer/art/make-art.py      # needs Pillow and Windows' Georgia font

beta's setup looks like Gamma Player's: the Windows 11 wizard on plain white (or YouTube black when Windows is dark),
with the logo's letter alone, Georgia's capital beta, drawn dark on light and white on dark. Transparent PNGs at
100-200 % scale; Inno Setup 6.7 picks the size for the screen.
"""
import os

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
FONT = os.path.join(os.environ.get("WINDIR", r"C:\Windows"), "Fonts", "georgia.ttf")
SCALES = (100, 125, 150, 175, 200)
# (kind, size at 100 %, letter height / short side, letter's centre from the top (None = middle))
KINDS = (("small", (58, 58), 0.62, None), ("large", (202, 386), 0.36, 0.30))
MODES = (("light", (15, 15, 15)), ("dark", (255, 255, 255)))


def letter(color):
    font = ImageFont.truetype(FONT, 600)
    canvas = Image.new("RGBA", (900, 900), color + (0,))
    ImageDraw.Draw(canvas).text((150, 50), "\u0392", font=font, fill=color + (255,))
    return canvas.crop(canvas.getbbox())


def main():
    for old in os.listdir(HERE):
        if old.endswith((".bmp", ".png")):
            os.remove(os.path.join(HERE, old))
    for mode, color in MODES:
        glyph = letter(color)
        for kind, (W, H), frac, top in KINDS:
            for scale in SCALES:
                w, h = W * scale // 100, H * scale // 100
                lh = int(min(w, h) * frac)
                lw = int(glyph.width * lh / glyph.height)
                image = Image.new("RGBA", (w, h), (0, 0, 0, 0))
                y = (h - lh) // 2 if top is None else int(h * top - lh / 2)
                image.alpha_composite(glyph.resize((lw, lh), Image.LANCZOS), ((w - lw) // 2, y))
                image.save(os.path.join(HERE, f"{kind}-{mode}-{scale}.png"))
    print("written", HERE)


if __name__ == "__main__":
    main()
