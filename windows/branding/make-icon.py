"""Draws beta's icon exactly in Gamma Player's style: Gamma's is a dark serif capital gamma (Γ, Georgia) on
a light-gray rounded square that darkens toward the bottom; beta's is the capital beta (Β) the same way.
Also a white letter on transparent, like Gamma's assets/logo.png, for places that tint it.

python windows/branding/make-icon.py  ->  Assets/AppIcon.png (256), Assets/BetaMonitor.ico (16-256),
                                          branding/BetaMonitor-1024.png and branding/BetaMonitor-logo.png"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = Path(__file__).resolve().parent
ASSETS = HERE.parent / "src" / "BambuBar.Windows" / "Assets"
S = 1024                        # drawn large, scaled down
LETTER = "\u0392"               # Greek capital beta, as Gamma's is the capital gamma
FONT = "C:/Windows/Fonts/georgia.ttf"
INK = (33, 33, 33, 255)         # Gamma icon's letter  #212121
TOP, BOTTOM = 232, 188          # Gamma icon's gray, top to bottom
RADIUS = 0.21                   # of the side
HEIGHT = 0.63                   # the letter's height, of the side (Gamma's Γ: 161 of 256)


def letter(size, fill):
    """The letter, centred, at Gamma's height, on transparent."""
    im = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    probe = ImageFont.truetype(FONT, 1000)
    box = d.textbbox((0, 0), LETTER, font=probe)
    font = ImageFont.truetype(FONT, int(1000 * HEIGHT * size / (box[3] - box[1])))
    box = d.textbbox((0, 0), LETTER, font=font)
    w, h = box[2] - box[0], box[3] - box[1]
    d.text(((size - w) / 2 - box[0], (size - h) / 2 - box[1]), LETTER, font=font, fill=fill)
    return im


def icon(size=S):
    gradient = Image.new("RGBA", (size, size))
    for y in range(size):
        v = round(TOP + (BOTTOM - TOP) * y / (size - 1))
        ImageDraw.Draw(gradient).line([(0, y), (size, y)], fill=(v, v, v, 255))
    mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, size - 1, size - 1], radius=size * RADIUS, fill=255)
    im = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    im.paste(gradient, (0, 0), mask)
    # Gamma's thin mid-gray outline, just inside the edge (2 px at 256, 1 px in).
    inset, width = size / 256, max(1, round(size * 2 / 256))
    ImageDraw.Draw(im).rounded_rectangle([inset, inset, size - 1 - inset, size - 1 - inset],
                                         radius=size * RADIUS - inset, outline=(154, 154, 154, 255), width=width)
    # And the soft shadow the letter casts, a little below it.
    shadow = letter(size, (0, 0, 0, 40)).filter(ImageFilter.GaussianBlur(size * 5 / 256))
    im.alpha_composite(shadow, (0, round(size * 2 / 256)))
    im.alpha_composite(letter(size, INK))
    return im


big = icon()
big.resize((256, 256), Image.LANCZOS).save(ASSETS / "AppIcon.png")
big.resize((256, 256), Image.LANCZOS).save(ASSETS / "BetaMonitor.ico",
                                           sizes=[(16, 16), (20, 20), (24, 24), (32, 32), (40, 40), (48, 48), (64, 64), (128, 128), (256, 256)])
big.save(HERE / "BetaMonitor-1024.png")
white = letter(S, (255, 255, 255, 255))
white.crop(white.getbbox()).save(HERE / "BetaMonitor-logo.png")
print("written", ASSETS)
