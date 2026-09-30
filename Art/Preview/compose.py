# Draws the first layout of the preview image (640x360, drawn at 2x).
# Needs: crate_1024.png (rendered by Art/CourierCrate/hardcase.py at 1024 px), the icon_*.png crops
# (taken from an in-game screenshot of the courier board) and the OFL fonts from npm:
#   npm pack @fontsource/saira-stencil-one @fontsource/oswald @fontsource/barlow-condensed, extracted to fonts/x/
import math, random
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageChops, ImageEnhance

W, H = 1280, 720           # drawn at 2x, saved at 640x360
FD = 'fonts/x/package/files/'
rng = np.random.default_rng(11)

# ---------- background: dim world-map terrain ----------
def value_noise(w, h, cell, rng):
    gw, gh = w // cell + 3, h // cell + 3
    g = rng.random((gh, gw))
    img = Image.fromarray((g * 255).astype(np.uint8)).resize((gw * cell, gh * cell), Image.BICUBIC)
    return np.asarray(img, dtype=np.float32)[:h, :w] / 255.0

hmap = sum(value_noise(W, H, c, rng) * a for c, a in [(300, .6), (150, .28), (70, .12)])
hmap = (hmap - hmap.min()) / (hmap.max() - hmap.min())
wet = value_noise(W, H, 220, rng) * .7 + value_noise(W, H, 90, rng) * .3
def biome(h, m):
    if h < 0.30: return (38, 56, 76)          # ocean
    if h < 0.34: return (50, 70, 88)          # shallows
    if h > 0.80: return (132, 128, 122)       # mountains
    if h > 0.70: return (112, 104, 86)        # hills
    if m < 0.32: return (150, 132, 92)        # desert
    if m < 0.45: return (124, 112, 78)        # arid shrubland
    if m < 0.62: return (92, 102, 64)         # temperate forest
    return (70, 88, 62)                       # boreal / wet forest
bg = Image.new('RGB', (W, H), (20, 22, 24)); gd = ImageDraw.Draw(bg)
r = 20; hw = math.sqrt(3) * r
trng = random.Random(5)
for row in range(-1, int(H / (1.5 * r)) + 2):
    for col in range(-1, int(W / hw) + 2):
        cx = col * hw + (hw / 2 if row % 2 else 0); cy = row * 1.5 * r
        sx, sy = min(max(int(cx), 0), W - 1), min(max(int(cy), 0), H - 1)
        c = biome(hmap[sy, sx], wet[sy, sx])
        k = 1 + trng.uniform(-0.05, 0.05)
        c = tuple(int(min(255, v * k)) for v in c)
        pts = [(cx + r * math.cos(math.radians(60 * i + 30)), cy + r * math.sin(math.radians(60 * i + 30))) for i in range(6)]
        gd.polygon(pts, fill=c, outline=tuple(int(v * 0.72) for v in c))
bg = bg.filter(ImageFilter.GaussianBlur(0.6))

# darken + desaturate, heavier on the right where the title sits, and a vignette
bg = ImageEnhance.Color(bg).enhance(0.75)
yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
vig = 1 - 0.55 * (((xx - W * 0.42) / (W * 0.75)) ** 2 + ((yy - H * 0.5) / (H * 0.8)) ** 2)
side = 1 - 0.35 * np.clip((xx - W * 0.45) / (W * 0.35), 0, 1)
dim = np.clip(vig * side * 0.62, 0.14, 1)[..., None]
bg = Image.fromarray(np.clip(np.asarray(bg, np.float32) * dim, 0, 255).astype(np.uint8)).convert('RGBA')

# ---------- caravan route + settlements ----------
def bezier(p0, p1, p2, p3, n=400):
    for i in range(n + 1):
        t = i / n
        yield ((1-t)**3*p0[0] + 3*(1-t)**2*t*p1[0] + 3*(1-t)*t**2*p2[0] + t**3*p3[0],
               (1-t)**3*p0[1] + 3*(1-t)**2*t*p1[1] + 3*(1-t)*t**2*p2[1] + t**3*p3[1])
A, B = (84, 640), (1192, 632)
route = Image.new('RGBA', (W, H), (0, 0, 0, 0)); rd = ImageDraw.Draw(route)
pts = list(bezier(A, (380, 760), (820, 520), B))
acc = 0; last = pts[0]
for p in pts[1:]:
    acc += math.dist(p, last); last = p
    if acc >= 16:
        acc = 0
        rd.ellipse((p[0]-4, p[1]-4, p[0]+4, p[1]+4), fill=(236, 214, 150, 220))
def settlement(d, c, col):
    x, y = c
    d.ellipse((x-24, y-24, x+24, y+24), fill=(18, 20, 22, 230))
    d.ellipse((x-19, y-19, x+19, y+19), fill=col)
    d.polygon([(x-10, y+9), (x-10, y-1), (x, y-11), (x+10, y-1), (x+10, y+9)], fill=(240, 236, 226))
settlement(rd, A, (70, 120, 170))
settlement(rd, B, (170, 120, 60))
bg.alpha_composite(route)

# ---------- crate ----------
crate = Image.open('crate_1024.png')
crate = crate.crop(crate.getbbox())
cw = 560
crate = crate.resize((cw, round(crate.height * cw / crate.width)), Image.LANCZOS)
cx, cy = 318, 372
bg.alpha_composite(crate, (cx - crate.width // 2, cy - crate.height // 2))

# ---------- title ----------
d = ImageDraw.Draw(bg)
def text_shadow(pos, s, font, fill, spacing=0, shadow=(0, 0, 0, 200), off=4, blur=6):
    layer = Image.new('RGBA', (W, H), (0, 0, 0, 0)); ld = ImageDraw.Draw(layer)
    draw_text(ld, (pos[0] + off, pos[1] + off), s, font, shadow, spacing)
    layer = layer.filter(ImageFilter.GaussianBlur(blur))
    bg.alpha_composite(layer)
    draw_text(ImageDraw.Draw(bg), pos, s, font, fill, spacing)
def text_width(s, font, spacing=0):
    return sum(font.getlength(ch) for ch in s) + spacing * (len(s) - 1)
def draw_text(dd, pos, s, font, fill, spacing=0):
    x, y = pos
    for ch in s:
        dd.text((x, y), ch, font=font, fill=fill)
        x += font.getlength(ch) + spacing

TX0, TX1 = 676, 1228
f_small = ImageFont.truetype(FD + 'oswald-latin-500-normal.woff', 50)
f_big = ImageFont.truetype(FD + 'saira-stencil-one-latin-400-normal.woff', 150)
f_tag = ImageFont.truetype(FD + 'barlow-condensed-latin-600-normal.woff', 42)

big = 'COURIER'
while text_width(big, f_big, 4) > TX1 - TX0:
    f_big = ImageFont.truetype(FD + 'saira-stencil-one-latin-400-normal.woff', f_big.size - 2)
bw = text_width(big, f_big, 4)
tx = TX0 + ((TX1 - TX0) - bw) / 2
sp = 22
sw = text_width('RIMWORLD', f_small, sp)
text_shadow((TX0 + ((TX1 - TX0) - sw) / 2, 150), 'RIMWORLD', f_small, (214, 190, 128, 255), sp)
text_shadow((tx, 200), big, f_big, (236, 232, 220, 255), 4, off=5, blur=7)
# red rule under the title, like the seal strip
bb = f_big.getbbox(big)
ry = 200 + bb[3] + 22
d = ImageDraw.Draw(bg)
d.rectangle((tx, ry, tx + bw, ry + 6), fill=(192, 57, 43, 255))
tag = 'Deliver sealed cargo between settlements'
while text_width(tag, f_tag) > TX1 - TX0:
    f_tag = ImageFont.truetype(FD + 'barlow-condensed-latin-600-normal.woff', f_tag.size - 1)
tw = text_width(tag, f_tag)
text_shadow((TX0 + ((TX1 - TX0) - tw) / 2, ry + 26), tag, f_tag, (200, 200, 196, 255), off=3, blur=4)

# ---------- cargo icons (from the in-game courier board) ----------
def keyed(path, top_fill=None):
    im = Image.open(path).convert('RGB')
    a = np.asarray(im, np.float32)
    if top_fill:
        a[:top_fill] = (42, 43, 44)
    dist = np.sqrt(((a - np.array([42, 43, 44])) ** 2).sum(-1))
    alpha = np.clip((dist - 5) / 18, 0, 1) * 255
    return Image.fromarray(np.dstack([a, alpha]).astype(np.uint8), 'RGBA')
icons = [keyed('icon_potato.png'), keyed('icon_medicine.png'), keyed('icon_grenade.png'), keyed('icon_book.png', top_fill=7)]
slot, gap = 96, 22
total = len(icons) * slot + (len(icons) - 1) * gap
x0 = TX0 + ((TX1 - TX0) - total) / 2
iy = ry + 100
for i, ic in enumerate(icons):
    x = round(x0 + i * (slot + gap))
    d.rectangle((x, iy, x + slot, iy + slot), fill=(30, 32, 34, 235), outline=(96, 98, 100, 255), width=2)
    ic2 = ic.resize((ic.width * 108 // 72, ic.height * 108 // 72), Image.LANCZOS)
    bg.alpha_composite(ic2, (x + (slot - ic2.width) // 2, iy + (slot - ic2.height) // 2))

out = bg.convert('RGB')
out.save('preview_2x.png')
small = out.resize((640, 360), Image.LANCZOS)
small.save('Preview.png', optimize=True)
