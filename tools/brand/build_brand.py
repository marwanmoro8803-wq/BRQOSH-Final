"""يولّد أصولَ الهويّة من مصدرٍ واحد: assets/brand/brqosh-mark.svg (مسارٌ متّجهيّ، even-odd) وفق تصاميم المالك (BRQOSH Logo.html).

    python tools/brand/build_brand.py

التصاميم الثلاثة المعتمدة: الشعارُ العموديّ (درعٌ فوق «BRQOSH» وشريط)، والأفقيّ (درعٌ | شريطٌ رأسيّ | «BRQOSH»)، وأيقونةُ التطبيق (مربّعٌ مدوَّر بنيٌّ والدرعُ كريميّ).
اللونُ الأساس برونزيّ `#8B5A2B` وبدائلُه `#6B4420` و`#B07A3B` و`#141413`، والدرعُ على الأيقونة `#F7EFE4`. خطُّ الاسم Chakra Petch Bold (SIL OFL).

المخرجات (كلُّها مولَّدة — لا تُعدَّل يدويّاً):
  assets/brand/brqosh-mark-{bronze,cream,ink}.png      الدرعُ وحدَه بخلفيّةٍ شفّافة
  assets/brand/brqosh-logo-vertical.svg / .png          العموديّ على الأبيض (متّجهيّ: الاسمُ مسارٌ لا خطّ)
  assets/brand/brqosh-logo-vertical-dark.png            العموديّ على الحبر
  assets/brand/brqosh-logo-horizontal.svg / .png        الأفقيّ على الأبيض
  assets/brand/brqosh-app-icon.svg / -1024.png          أيقونةُ التطبيق
  assets/brand/brqosh.ico                               أيقونةُ البرنامج والمُنصِّب (٦ مقاسات)
  assets/brand/brqosh-uninstall.ico                     أيقونةُ «إزالة BRQOSH» (الأيقونة نفسُها بشارة سلّة مهملات)
  src/Brqosh.Desktop/Themes/BrandMark.xaml              هندسةُ الدرع لـWPF (تتلوّن بأيّ فرشاة)

يحتاج Pillow وfonttools.
"""
import os
import re
import sys

from fontTools.pens.svgPathPen import SVGPathPen
from fontTools.pens.transformPen import TransformPen
from fontTools.ttLib import TTFont
from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
BRAND = os.path.join(ROOT, "assets", "brand")
SVG = os.path.join(BRAND, "brqosh-mark.svg")
FONT_FILE = os.path.join(BRAND, "fonts", "ChakraPetch-Bold-latin.ttf")

BRONZE = "#8B5A2B"
BRONZE_DARK = "#6B4420"
BRONZE_LIGHT = "#B07A3B"
INK = "#141413"
CREAM = "#F7EFE4"
WHITE = "#FFFFFF"
VIEW_W, VIEW_H = 751.0, 1000.0


def parse_path(d):
    """يحلّل أوامر M/H/V/L/Z المطلقة إلى حلقاتٍ من نقاط."""
    tokens = re.findall(r"[MHVLZ]|-?\d+(?:\.\d+)?", d)
    rings, cur, x, y, i = [], [], 0.0, 0.0, 0
    cmd = None
    while i < len(tokens):
        t = tokens[i]
        if t in "MHVLZ":
            cmd = t
            i += 1
            if cmd == "Z":
                if cur:
                    rings.append(cur)
                cur = []
            continue
        if cmd == "M":
            x, y = float(tokens[i]), float(tokens[i + 1]); i += 2
            cur = [(x, y)]
            cmd = "L"
        elif cmd == "L":
            x, y = float(tokens[i]), float(tokens[i + 1]); i += 2
            cur.append((x, y))
        elif cmd == "H":
            x = float(tokens[i]); i += 1
            cur.append((x, y))
        elif cmd == "V":
            y = float(tokens[i]); i += 1
            cur.append((x, y))
        else:
            raise ValueError(f"أمرُ مسارٍ غيرُ مدعوم قرب {t}")
    return rings


def load_polygons():
    text = open(SVG, encoding="utf-8").read()
    d = re.search(r' d="([^"]+)"', text).group(1)
    return parse_path(d), d


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


# ── الاسمُ «BRQOSH» بخطّ Chakra Petch Bold ─────────────────────────────────────────────
_font = TTFont(FONT_FILE)
_upem = _font["head"].unitsPerEm
_cmap = _font.getBestCmap()
_glyphs = _font.getGlyphSet()
_hmtx = _font["hmtx"]


def word_layout(text, size, tracking_em):
    """مواضعُ الحروف (x) وعرضُ الكلمة بوحدة البكسل — التباعدُ كـ`letter-spacing` في CSS بعد كلّ حرفٍ."""
    scale = size / _upem
    xs, x = [], 0.0
    for ch in text:
        name = _cmap[ord(ch)]
        xs.append(x)
        x += _hmtx[name][0] * scale + tracking_em * size
    return xs, x


def word_svg_path(text, size, tracking_em, ox, baseline):
    """مسارُ SVG للكلمة (الحروفُ مخطَّطةٌ فلا يعتمد الملفُّ على خطٍّ مثبَّت)."""
    scale = size / _upem
    xs, _ = word_layout(text, size, tracking_em)
    parts = []
    for ch, x in zip(text, xs):
        pen = SVGPathPen(_glyphs)
        tpen = TransformPen(pen, (scale, 0, 0, -scale, ox + x, baseline))
        _glyphs[_cmap[ord(ch)]].draw(tpen)
        parts.append(pen.getCommands())
    return " ".join(parts)


def pil_font(size):
    return ImageFont.truetype(FONT_FILE, size)


def draw_word(draw, text, size, tracking_em, x0, top, fill):
    fnt = pil_font(size)
    xs, _ = word_layout(text, size, tracking_em)
    for ch, x in zip(text, xs):
        draw.text((x0 + x, top), ch, font=fnt, fill=fill)


# ── التصاميم ────────────────────────────────────────────────────────────────────────────
def vertical_logo(polys, brand, background, scale=1.5):
    """العموديّ: 640×800، الدرع 300×400، فجوةٌ 40، الاسم 88px بتباعد 0.12em، فجوةٌ 14، شريطٌ 120×6."""
    W, H = int(640 * scale), int(800 * scale)
    img = Image.new("RGBA", (W, H), background)
    mark_h = int(400 * scale)
    mark = colored(mark_mask(polys, mark_h), brand)
    font_px = int(88 * scale)
    tracking = 0.12
    _, text_w = word_layout("BRQOSH", font_px, tracking)
    text_w -= tracking * font_px                      # لا تباعدَ بعد الحرف الأخير
    total = mark_h + int(40 * scale) + font_px + int(14 * scale) + int(6 * scale)
    top = (H - total) // 2
    img.alpha_composite(mark, ((W - mark.width) // 2, top))
    d = ImageDraw.Draw(img)
    ty = top + mark_h + int(40 * scale)
    draw_word(d, "BRQOSH", font_px, tracking, (W - text_w) / 2, ty - int(font_px * 0.13), brand)
    by = ty + font_px + int(14 * scale)
    d.rectangle((W // 2 - int(60 * scale), by, W // 2 + int(60 * scale), by + int(6 * scale)), fill=brand)
    return img


def horizontal_logo(polys, brand, background, scale=1.5):
    """الأفقيّ: 1040×400، الدرع 180×240، فجوة 48، شريطٌ 6×200، فجوة 48، الاسم 120px بتباعد 0.1em."""
    W, H = int(1040 * scale), int(400 * scale)
    img = Image.new("RGBA", (W, H), background)
    mark_h = int(240 * scale)
    mark = colored(mark_mask(polys, mark_h), brand)
    font_px = int(120 * scale)
    tracking = 0.10
    _, text_w = word_layout("BRQOSH", font_px, tracking)
    text_w -= tracking * font_px
    gap = int(48 * scale)
    bar_w = int(6 * scale)
    total = mark.width + gap + bar_w + gap + text_w
    x0 = (W - total) / 2
    img.alpha_composite(mark, (int(x0), (H - mark_h) // 2))
    d = ImageDraw.Draw(img)
    bx = int(x0 + mark.width + gap)
    d.rectangle((bx, (H - int(200 * scale)) // 2, bx + bar_w, (H + int(200 * scale)) // 2), fill=brand)
    draw_word(d, "BRQOSH", font_px, tracking, bx + bar_w + gap, (H - font_px) // 2 - int(font_px * 0.04), brand)
    return img


def icon_image(polys, size):
    """أيقونةُ التطبيق: مربّعٌ مدوَّر (نصفُ القطر ٢٢٫٥٪) بنيٌّ والدرعُ الكريميّ بارتفاع ٧٠٪ من المربّع."""
    ss = 4
    s = size * ss
    base = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    ImageDraw.Draw(base).rounded_rectangle((0, 0, s - 1, s - 1), radius=int(s * 0.225), fill=BRONZE)
    mark = colored(mark_mask(polys, int(size * 0.70 * ss)), CREAM)
    base.alpha_composite(mark, ((s - mark.width) // 2, (s - mark.height) // 2))
    return base.resize((size, size), Image.LANCZOS)


def uninstall_icon_image(polys, size):
    """أيقونةُ «إزالة BRQOSH»: أيقونةُ التطبيق نفسُها وفي ركنها شارةٌ دائريّةٌ داكنةٌ بسلّةِ مهملات كريميّة — فيعرف المستخدمُ أنّها الإزالةُ لا البرنامج."""
    ss = 4
    base = icon_image(polys, size * ss).convert("RGBA")
    s = size * ss
    d = ImageDraw.Draw(base)
    r = int(s * 0.27)
    cx, cy = s - r - int(s * 0.03), s - r - int(s * 0.03)
    d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=INK, outline=CREAM, width=max(2, int(s * 0.012)))
    u = r / 10.0
    # ‏السلّة: غطاءٌ ومقبضٌ وجسمٌ بخطوطٍ رأسيّة
    d.rectangle((cx - 6 * u, cy - 5.2 * u, cx + 6 * u, cy - 3.8 * u), fill=CREAM)
    d.rectangle((cx - 2 * u, cy - 6.6 * u, cx + 2 * u, cy - 5.2 * u), fill=CREAM)
    d.polygon([(cx - 5 * u, cy - 3 * u), (cx + 5 * u, cy - 3 * u), (cx + 4 * u, cy + 6 * u), (cx - 4 * u, cy + 6 * u)], fill=CREAM)
    for dx in (-2 * u, 0, 2 * u):
        d.rectangle((cx + dx - 0.3 * u, cy - 1.8 * u, cx + dx + 0.3 * u, cy + 4.8 * u), fill=INK)
    return base.resize((size, size), Image.LANCZOS)


def installer_dialog(polys):
    """صورةُ الترحيب 493×312 لمعالج MSI: لوحةٌ بنّيّةٌ يساراً (164px — هي ما يعرضه WixUI) بدرعٍ كريميّ و«BRQOSH» تحته، والباقي أبيض."""
    W, H, panel = 493, 312, 164
    ss = 3
    img = Image.new("RGB", (W * ss, H * ss), WHITE)
    d = ImageDraw.Draw(img)
    d.rectangle((0, 0, panel * ss, H * ss), fill=BRONZE)
    d.rectangle(((panel - 4) * ss, 0, panel * ss, H * ss), fill=BRONZE_LIGHT)
    mark = colored(mark_mask(polys, 110 * ss), CREAM)
    img = img.convert("RGBA")
    img.alpha_composite(mark, (((panel - 4) * ss - mark.width) // 2, 62 * ss))
    d = ImageDraw.Draw(img)
    fpx = 21 * ss
    _, tw = word_layout("BRQOSH", fpx, 0.12)
    tw -= 0.12 * fpx
    draw_word(d, "BRQOSH", fpx, 0.12, ((panel - 4) * ss - tw) / 2, 186 * ss, CREAM)
    d.rectangle((((panel - 4) * ss) // 2 - 20 * ss, 222 * ss, ((panel - 4) * ss) // 2 + 20 * ss, 224 * ss), fill=CREAM)
    return img.resize((W, H), Image.LANCZOS).convert("RGB")


def installer_banner(polys):
    """لافتةُ أعلى النوافذ 493×58: بيضاء بخطٍّ بنّيٍّ سفليّ ودرعٍ عند الطرف."""
    W, H = 493, 58
    ss = 3
    img = Image.new("RGBA", (W * ss, H * ss), WHITE)
    d = ImageDraw.Draw(img)
    d.rectangle((0, 54 * ss, W * ss, 57 * ss), fill=BRONZE)
    d.rectangle((0, 57 * ss, W * ss, H * ss), fill=BRONZE_LIGHT)
    mark = colored(mark_mask(polys, 42 * ss), BRONZE)
    img.alpha_composite(mark, (440 * ss, 6 * ss))
    return img.resize((W, H), Image.LANCZOS).convert("RGB")


def xaml_geometry(polys):
    return " ".join("M" + " ".join(f"{x:g},{y:g}" for x, y in p) + " Z" for p in polys)


def write(path, text):
    open(path, "w", encoding="utf-8", newline="\n").write(text)


def main():
    polys, d = load_polygons()
    assert len(polys) == 7, f"المتوقَّع ٧ حلقات، وُجد {len(polys)}"

    for name, color in (("bronze", BRONZE), ("cream", CREAM), ("ink", INK)):
        colored(mark_mask(polys, 512), color).save(os.path.join(BRAND, f"brqosh-mark-{name}.png"))

    vertical_logo(polys, BRONZE, WHITE).save(os.path.join(BRAND, "brqosh-logo-vertical.png"))
    vertical_logo(polys, CREAM, INK).save(os.path.join(BRAND, "brqosh-logo-vertical-dark.png"))
    horizontal_logo(polys, BRONZE, WHITE).save(os.path.join(BRAND, "brqosh-logo-horizontal.png"))

    # ‏SVG العموديّ (الاسمُ مسارٌ مخطَّط).
    w_font, w_track = 88, 0.12
    _, w_total = word_layout("BRQOSH", w_font, w_track)
    w_total -= w_track * w_font
    word = word_svg_path("BRQOSH", w_font, w_track, (640 - w_total) / 2, 400 + 40 + 88 * 0.87 + 0)
    cx, top = (640 - 300) / 2, (800 - (400 + 40 + 88 + 14 + 6)) / 2
    word = word_svg_path("BRQOSH", w_font, w_track, (640 - w_total) / 2, top + 400 + 40 + w_font * 0.87)
    vert = (
        '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 640 800" role="img" aria-label="BRQOSH">\n'
        "  <title>BRQOSH</title>\n"
        '  <rect width="640" height="800" fill="#FFFFFF"/>\n'
        f'  <g transform="translate({cx:g} {top:g}) scale({300 / VIEW_W:.6f} {400 / VIEW_H:.6f})">'
        f'<path fill="{BRONZE}" fill-rule="evenodd" d="{d}"/></g>\n'
        f'  <path fill="{BRONZE}" d="{word}"/>\n'
        f'  <rect x="260" y="{top + 400 + 40 + w_font + 14:g}" width="120" height="6" fill="{BRONZE}"/>\n'
        "</svg>\n"
    )
    write(os.path.join(BRAND, "brqosh-logo-vertical.svg"), vert)

    # ‏SVG الأفقيّ.
    h_font, h_track = 120, 0.10
    _, h_total = word_layout("BRQOSH", h_font, h_track)
    h_total -= h_track * h_font
    crest_w = 180.0
    total = crest_w + 48 + 6 + 48 + h_total
    x0 = (1040 - total) / 2
    hword = word_svg_path("BRQOSH", h_font, h_track, x0 + crest_w + 48 + 6 + 48, 200 + h_font * 0.36)
    horiz = (
        '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1040 400" role="img" aria-label="BRQOSH">\n'
        "  <title>BRQOSH</title>\n"
        '  <rect width="1040" height="400" fill="#FFFFFF"/>\n'
        f'  <g transform="translate({x0:g} 80) scale({crest_w / VIEW_W:.6f} {240 / VIEW_H:.6f})">'
        f'<path fill="{BRONZE}" fill-rule="evenodd" d="{d}"/></g>\n'
        f'  <rect x="{x0 + crest_w + 48:g}" y="100" width="6" height="200" fill="{BRONZE}"/>\n'
        f'  <path fill="{BRONZE}" d="{hword}"/>\n'
        "</svg>\n"
    )
    write(os.path.join(BRAND, "brqosh-logo-horizontal.svg"), horiz)

    # ‏أيقونة التطبيق.
    icon_svg = (
        '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 320 320" role="img" aria-label="BRQOSH app icon">\n'
        f'  <rect width="320" height="320" rx="72" fill="{BRONZE}"/>\n'
        f'  <g transform="translate(76 48) scale({168 / VIEW_W:.6f} {224 / VIEW_H:.6f})">'
        f'<path fill="{CREAM}" fill-rule="evenodd" d="{d}"/></g>\n'
        "</svg>\n"
    )
    write(os.path.join(BRAND, "brqosh-app-icon.svg"), icon_svg)
    icon_image(polys, 1024).save(os.path.join(BRAND, "brqosh-app-icon-1024.png"))
    sizes = [256, 128, 64, 48, 32, 16]
    icon_image(polys, 256).save(os.path.join(BRAND, "brqosh.ico"), format="ICO", sizes=[(s, s) for s in sizes])
    uninstall_icon_image(polys, 256).save(os.path.join(BRAND, "brqosh-uninstall.ico"), format="ICO", sizes=[(s, s) for s in sizes])
    uninstall_icon_image(polys, 512).save(os.path.join(BRAND, "brqosh-uninstall-512.png"))

    os.makedirs(os.path.join(BRAND, "installer"), exist_ok=True)
    installer_dialog(polys).save(os.path.join(BRAND, "installer", "dialog.bmp"))
    installer_banner(polys).save(os.path.join(BRAND, "installer", "banner.bmp"))

    xaml = f"""<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <!-- مولَّدٌ من assets/brand/brqosh-mark.svg بـ tools/brand/build_brand.py — لا يُعدَّل يدويّاً. -->
    <!-- الدرعُ هندسةٌ لا صورة: يتلوّن بأيّ فرشاةٍ ويبقى حادّاً بأيّ مقاس. المقاسُ الطبيعيّ ٧٥١×١٠٠٠. -->
    <PathGeometry x:Key="BrqoshMarkGeometry" FillRule="EvenOdd" Figures="{xaml_geometry(polys)}"/>
</ResourceDictionary>
"""
    write(os.path.join(ROOT, "src", "Brqosh.Desktop", "Themes", "BrandMark.xaml"), xaml)

    # ‏الاسمُ «BRQOSH» هندسةً لـWPF (الحروفُ مخطَّطةٌ بتباعد 0.12em) فلا يحتاج التطبيقُ خطّاً مثبَّتاً ولا يتغيّر التباعد بين جهاز وآخر.
    size, tracking = 100, 0.12
    ox = 0.0
    wxs = word_svg_path("BRQOSH", size, tracking, ox, 0.0)
    pen_bounds = word_layout("BRQOSH", size, tracking)[1] - tracking * size
    cap = _font["OS/2"].sCapHeight * size / _upem
    wxaml = f"""<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <!-- مولَّدٌ بـ tools/brand/build_brand.py من Chakra Petch Bold (SIL OFL) — لا يُعدَّل يدويّاً. العرضُ {pen_bounds:.2f} والارتفاعُ (ارتفاع الحرف الكبير) {cap:.2f}. -->
    <PathGeometry x:Key="BrqoshWordmarkGeometry" FillRule="NonZero" Figures="{wxs}"/>
</ResourceDictionary>
"""
    write(os.path.join(ROOT, "src", "Brqosh.Desktop", "Themes", "BrandWordmark.xaml"), wxaml)
    print("wordmark width/cap:", round(pen_bounds, 2), round(cap, 2))
    print("ok:", len(polys), "حلقات")


if __name__ == "__main__":
    sys.exit(main())
