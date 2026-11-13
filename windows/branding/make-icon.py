"""Draws PrintPulse's icon in Gamma Player's style: AMOLED black, a light serif letter, one accent line.
python windows/branding/make-icon.py  ->  Assets/AppIcon.png (256) and Assets/PrintPulse.ico (16-256)."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageFilter

HERE = Path(__file__).resolve().parent
ASSETS = HERE.parent / "src" / "BambuBar.Windows" / "Assets"
S = 1024  # drawn large, scaled down
INK = (241, 241, 241, 255)      # Gamma's text  #f1f1f1
ACCENT = (62, 166, 255, 255)    # Gamma's blue  #3ea6ff


def draw(size=S):
    im = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    r = size * 0.22
    d.rounded_rectangle([0, 0, size - 1, size - 1], radius=r, fill=(0, 0, 0, 255))
    d.rounded_rectangle([size * .012, size * .012, size * .988, size * .988], radius=r * .97,
                        outline=(255, 255, 255, 28), width=max(2, size // 128))
    font = ImageFont.truetype("C:/Windows/Fonts/georgiab.ttf", int(size * 0.74))
    box = d.textbbox((0, 0), "P", font=font)
    w, h = box[2] - box[0], box[3] - box[1]
    x, y = (size - w) / 2 - box[0] - size * 0.02, (size - h) / 2 - box[1] - size * 0.04
    d.text((x, y), "P", font=font, fill=INK)
    # The pulse: flat, a spike up, a dip, flat, across the lower third.
    yb = size * 0.76
    u = size / 100
    pts = [(12 * u, yb), (38 * u, yb), (46 * u, yb - 20 * u), (54 * u, yb + 12 * u), (60 * u, yb - 6 * u),
           (64 * u, yb), (88 * u, yb)]
    glow = Image.new("RGBA", im.size, (0, 0, 0, 0))
    ImageDraw.Draw(glow).line(pts, fill=ACCENT, width=int(9 * u), joint="curve")
    im.alpha_composite(glow.filter(ImageFilter.GaussianBlur(4 * u)))
    # Black under the line so it cuts through the letter.
    d.line(pts, fill=(0, 0, 0, 255), width=int(9 * u), joint="curve")
    d.line(pts, fill=ACCENT, width=int(4.5 * u), joint="curve")
    for p in (pts[0], pts[-1]):
        d.ellipse([p[0] - 2.6 * u, p[1] - 2.6 * u, p[0] + 2.6 * u, p[1] + 2.6 * u], fill=ACCENT)
    return im


big = draw()
big.resize((256, 256), Image.LANCZOS).save(ASSETS / "AppIcon.png")
big.resize((256, 256), Image.LANCZOS).save(ASSETS / "PrintPulse.ico",
                                           sizes=[(16, 16), (20, 20), (24, 24), (32, 32), (40, 40), (48, 48), (64, 64), (128, 128), (256, 256)])
big.save(HERE / "PrintPulse-1024.png")
print("written", ASSETS)
