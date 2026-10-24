"""يولّد أصولَ الهويّة من مصدرٍ واحد: assets/brand/brqosh-mark.svg (مسارٌ متّجهيّ، even-odd).

    python tools/brand/build_brand.py

المخرجات (كلُّها مولَّدة — لا تُعدَّل يدويّاً):
  assets/brand/brqosh-mark-{gold,navy,bronze}.png     الدرعُ وحدَه بخلفيّةٍ شفّافة
  assets/brand/brqosh-logo-{dark,light}.png           الدرعُ و«BRQOSH» تحته (على كحليّ / على أبيض)
  assets/brand/brqosh-logo.svg                        الدرعُ و«BRQOSH» تحته — متّجهيّ
  assets/brand/brqosh.ico                             أيقونةُ البرنامج والمُنصِّب (٦ مقاسات)
  src/Brqosh.Desktop/Themes/BrandMark.xaml            هندسةُ الدرع لـWPF (تتلوّن بأيّ فرشاة)

يحتاج Pillow فقط. خطُّ الاسم: Segoe UI Bold من ويندوز (يُرسَم نقطيّاً في PNG).
"""
import os
import re
import sys

from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
BRAND = os.path.join(ROOT, "assets", "brand")
SVG = os.path.join(BRAND, "brqosh-mark.svg")

NAVY = "#0F3D5E"
GOLD = "#C9A227"
BRONZE = "#8B5A2B"
WHITE = "#FFFFFF"
VIEW_W, VIEW_H = 752.0, 1000.0


def load_polygons():
    text = open(SVG, encoding="utf-8").read()
    d = re.search(r' d="([^"]+)"', text).group(1)
    polys = []
    for sub in d.split("Z"):
        sub = sub.strip()
        if not sub:
            continue
        pts = re.findall(r"(-?\d+(?:\.\d+)?),(-?\d+(?:\.\d+)?)", sub)
        polys.append([(float(x), float(y)) for x, y in pts])
    return polys


def contains(poly, pt):
    x, y = pt
    inside = False
    for i in range(len(poly)):
        x1, y1 = poly[i]
        x2, y2 = poly[(i + 1) % len(poly)]
        if (y1 > y) != (y2 > y) and x < (x2 - x1) * (y - y1) / (y2 - y1) + x1:
            inside = not inside
    return inside


def depths(polys):
    return [sum(1 for j, q in enumerate(polys) if j != i and contains(q, p[0])) for i, p in enumerate(polys)]


def mark_mask(polys, height):
    """قناعٌ أحاديّ (٨ بت) للدرع بارتفاع `height` — تنعيمٌ بتكبيرٍ ٤× ثمّ تصغير."""
    ss = 4
    scale = height * ss / VIEW_H
    width = int(round(VIEW_W * scale))
    mask = Image.new("L", (width, int(height * ss)), 0)
    draw = ImageDraw.Draw(mask)
    for p, depth in zip(polys, depths(polys)):
        draw.polygon([(x * scale, y * scale) for x, y in p], fill=255 if depth % 2 == 0 else 0)
    return mask.resize((int(round(VIEW_W * height / VIEW_H)), int(height)), Image.LANCZOS)


def colored(mask, color):
    layer = Image.new("RGBA", mask.size, color)
    layer.putalpha(mask)
    return layer


def font(size):
    for name in ("segoeuib.ttf", "arialbd.ttf", "DejaVuSans-Bold.ttf"):
        for base in (r"C:\Windows\Fonts", "/usr/share/fonts/truetype/dejavu"):
            path = os.path.join(base, name)
            if os.path.exists(path):
                return ImageFont.truetype(path, size)
    return ImageFont.load_default()


def stacked(polys, mark_color, text_color, background, mark_h=520):
    """الدرعُ فوق «BRQOSH» بتباعد حروفٍ واسع."""
    mark = colored(mark_mask(polys, mark_h), mark_color)
    word = "BRQOSH"
    fnt = font(int(mark_h * 0.19))
    tracking = int(mark_h * 0.045)
    widths = [fnt.getlength(c) for c in word]
    text_w = int(sum(widths) + tracking * (len(word) - 1))
    text_h = int(mark_h * 0.27)
    pad = int(mark_h * 0.14)
    gap = int(mark_h * 0.09)
    canvas_w = max(mark.width, text_w) + pad * 2
    canvas_h = mark.height + gap + text_h + pad * 2
    canvas = Image.new("RGBA", (canvas_w, canvas_h), background or (0, 0, 0, 0))
    canvas.alpha_composite(mark, ((canvas_w - mark.width) // 2, pad))
    draw = ImageDraw.Draw(canvas)
    x = (canvas_w - text_w) / 2
    y = pad + mark.height + gap
    for ch, w in zip(word, widths):
        draw.text((x, y), ch, font=fnt, fill=text_color)
        x += w + tracking
    return canvas


def icon_image(polys, size):
    """أيقونةُ التطبيق: مربّعٌ كحليٌّ بزوايا مدوّرة والدرعُ الذهبيّ في وسطه."""
    ss = 4
    s = size * ss
    base = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    ImageDraw.Draw(base).rounded_rectangle((0, 0, s - 1, s - 1), radius=int(s * 0.22), fill=NAVY)
    mark = colored(mark_mask(polys, int(size * 0.66 * ss)), GOLD)
    base.alpha_composite(mark, ((s - mark.width) // 2, (s - mark.height) // 2))
    return base.resize((size, size), Image.LANCZOS)


def xaml_geometry(polys):
    parts = []
    for p in polys:
        parts.append("M" + " ".join(f"{x:g},{y:g}" for x, y in p) + " Z")
    return " ".join(parts)


def main():
    polys = load_polygons()
    assert len(polys) == 7, f"المتوقَّع ٧ حلقات، وُجد {len(polys)}"

    for name, color in (("gold", GOLD), ("navy", NAVY), ("bronze", BRONZE)):
        colored(mark_mask(polys, 512), color).save(os.path.join(BRAND, f"brqosh-mark-{name}.png"))

    stacked(polys, GOLD, WHITE, NAVY).save(os.path.join(BRAND, "brqosh-logo-dark.png"))
    stacked(polys, NAVY, NAVY, WHITE).save(os.path.join(BRAND, "brqosh-logo-light.png"))

    sizes = [256, 128, 64, 48, 32, 16]
    icon_image(polys, 256).save(os.path.join(BRAND, "brqosh.ico"), format="ICO", sizes=[(s, s) for s in sizes])

    d = re.search(r' d="([^"]+)"', open(SVG, encoding="utf-8").read()).group(1)
    logo = (
        '<svg xmlns="http://www.w3.org/2000/svg" viewBox="-120 -100 992 1420" role="img" aria-label="BRQOSH">\n'
        "  <title>BRQOSH</title>\n"
        f'  <rect x="-120" y="-100" width="992" height="1420" fill="{NAVY}"/>\n'
        f'  <path fill="{GOLD}" fill-rule="evenodd" d="{d}"/>\n'
        f'  <text x="376" y="1230" text-anchor="middle" fill="{WHITE}" font-family="Segoe UI, Arial, sans-serif" '
        'font-weight="700" font-size="190" letter-spacing="46">BRQOSH</text>\n'
        "</svg>\n"
    )
    open(os.path.join(BRAND, "brqosh-logo.svg"), "w", encoding="utf-8", newline="\n").write(logo)

    xaml = f"""<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <!-- مولَّدٌ من assets/brand/brqosh-mark.svg بـ tools/brand/build_brand.py — لا يُعدَّل يدويّاً. -->
    <!-- الدرعُ هندسةٌ لا صورة: يتلوّن بأيّ فرشاةٍ ويبقى حادّاً بأيّ مقاس. المقاسُ الطبيعيّ ٧٥٢×١٠٠٠. -->
    <PathGeometry x:Key="BrqoshMarkGeometry" FillRule="EvenOdd" Figures="{xaml_geometry(polys)}"/>
</ResourceDictionary>
"""
    out = os.path.join(ROOT, "src", "Brqosh.Desktop", "Themes", "BrandMark.xaml")
    open(out, "w", encoding="utf-8", newline="\n").write(xaml)
    print("ok:", len(polys), "حلقات")


if __name__ == "__main__":
    sys.exit(main())
