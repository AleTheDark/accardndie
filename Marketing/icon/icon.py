from PIL import Image, ImageFilter, ImageEnhance, ImageDraw
import math

ROOT = r"C:/Users/accar/AccardND/AccardND"
FEAT = Image.open(ROOT + "/Marketing/accard-n-die-feature-graphic-1024x500.png").convert("RGB")
EMB  = Image.open(ROOT + "/Assets/Resources/UI/Sanctuary/sanctuary_classes_emblem_aaa.png").convert("RGBA")
S = 512

def trim_alpha(im):
    bbox = im.split()[3].getbbox()
    return im.crop(bbox)

def bg_from(box, blur=14, dark=0.55, sat=1.15):
    crop = FEAT.crop(box).resize((S, S), Image.LANCZOS).filter(ImageFilter.GaussianBlur(blur))
    crop = ImageEnhance.Color(crop).enhance(sat)
    crop = ImageEnhance.Brightness(crop).enhance(dark)
    return crop

def vignette(im, strength=0.85, inner=0.30):
    v = Image.new("L", (S, S))
    px = v.load()
    c = S / 2.0
    maxd = math.hypot(c, c)
    for y in range(S):
        for x in range(S):
            d = math.hypot(x - c, y - c) / maxd
            t = max(0.0, (d - inner) / (1 - inner))
            px[x, y] = int(255 * (1 - strength * t * t))
    return Image.composite(im, Image.new("RGB", (S, S), (6, 4, 12)), v)

def glow(rgba, radius, color, alpha):
    a = rgba.split()[3].filter(ImageFilter.GaussianBlur(radius))
    a = a.point(lambda p: min(255, int(p * alpha)))
    layer = Image.new("RGBA", rgba.size, color + (0,))
    layer.putalpha(a)
    return layer

def place(base, emb, scale=0.86, dy=0.0, glow_col=(150, 90, 255), glow_a=1.5):
    e = trim_alpha(emb)
    w, h = e.size
    k = (S * scale) / max(w, h)
    e = e.resize((int(w * k), int(h * k)), Image.LANCZOS)
    x = (S - e.size[0]) // 2
    y = int((S - e.size[1]) // 2 + dy * S)
    canvas = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    canvas.paste(e, (x, y), e)
    out = base.convert("RGBA")
    out = Image.alpha_composite(out, glow(canvas, 26, glow_col, glow_a))
    # contact shadow
    sh = glow(canvas, 10, (0, 0, 0), 1.6)
    out = Image.alpha_composite(out, sh.transform(sh.size, Image.AFFINE, (1, 0, -4, 0, 1, -8)))
    out = Image.alpha_composite(out, canvas)
    return out.convert("RGB")

# --- variant A: arena floor background (ornato dorato, centro destra del feature graphic)
a = place(vignette(bg_from((560, 60, 900, 400)), 0.9, 0.22), EMB, 0.84)
a.save("icon_A.png")

# --- variant B: dice corner background (viola/oro, angolo basso sinistro)
b = place(vignette(bg_from((0, 300, 300, 500), blur=18, dark=0.62), 0.82, 0.28), EMB, 0.84, glow_col=(255, 170, 60), glow_a=1.2)
b.save("icon_B.png")

# --- variant C: sfondo piatto profondo + emblema piu grande
flat = Image.new("RGB", (S, S), (26, 16, 46))
d = ImageDraw.Draw(flat)
for i in range(S // 2, 0, -1):
    t = i / (S / 2)
    col = (int(26 + 34 * (1 - t)), int(16 + 18 * (1 - t)), int(46 + 52 * (1 - t)))
    d.ellipse([S//2 - i, S//2 - i, S//2 + i, S//2 + i], fill=col)
c = place(vignette(flat.filter(ImageFilter.GaussianBlur(30)), 0.75, 0.2), EMB, 0.92)
c.save("icon_C.png")

sheet = Image.new("RGB", (S * 3 + 40, S), (20, 20, 20))
for i, im in enumerate([a, b, c]):
    sheet.paste(im, (i * (S + 20), 0))
sheet.save("icon_sheet.png")
for n in "ABC":
    import os; print(n, os.path.getsize(f"icon_{n}.png") / 1024, "KB")
