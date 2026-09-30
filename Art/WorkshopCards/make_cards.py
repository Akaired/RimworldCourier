"""
Steam Workshop section cards for Rimworld Courier, drawn in RimWorld's own UI style:
- font: Arial, extracted from RimWorld's resources.assets (the game's Small/Medium UI font)
- icons: RimWorld's own UI textures (resources.assets), plus the mod's courier crate art
- colours sampled from in-game screenshots: window #15191D, section #2A2B2C, border #878787,
  button texture (tan/brown) behind the main icon like a gizmo.

The game textures (_game_textures/) and the font (fonts/) are not included in the repository:
they belong to Ludeon Studios and Microsoft. Extract them from your own RimWorld install to rebuild.
"""
import math, os, sys
from PIL import Image, ImageDraw, ImageFont, ImageFilter

TEX = '_game_textures/'
FONT = 'fonts/Arial.ttf'              # Arial Regular, as shipped inside RimWorld
CRATE = '../Preview/crate_1024.png'
OUT = 'out'
W, H = 1200, 200

WINDOW = (21, 25, 29)
SECTION = (42, 43, 44)
BORDER = (135, 135, 135)
TITLE = (240, 240, 238)
GOLD = (242, 209, 115)

def tex(name):
    return Image.open(TEX + name + '.png').convert('RGBA')

def fit(im, box):
    im = im.copy()
    im.thumbnail((box, box), Image.LANCZOS) if max(im.size) > box else None
    if max(im.size) < box:
        s = box / max(im.size)
        im = im.resize((max(1, round(im.width * s)), max(1, round(im.height * s))), Image.LANCZOS)
    return im

def shadowed(canvas, im, xy, blur=5, off=(3, 4), alpha=150):
    sh = Image.new('RGBA', canvas.size, (0, 0, 0, 0))
    a = im.getchannel('A').point(lambda v: v * alpha // 255)
    blk = Image.new('RGBA', im.size, (0, 0, 0, 255)); blk.putalpha(a)
    sh.alpha_composite(blk, (xy[0] + off[0], xy[1] + off[1]))
    canvas.alpha_composite(sh.filter(ImageFilter.GaussianBlur(blur)))
    canvas.alpha_composite(im, xy)

def hex_pattern(size, r=18, color=(255, 255, 255), alpha=14):
    layer = Image.new('RGBA', size, (0, 0, 0, 0)); d = ImageDraw.Draw(layer)
    hw = math.sqrt(3) * r
    for row in range(-1, int(size[1] / (1.5 * r)) + 2):
        for col in range(-1, int(size[0] / hw) + 2):
            cx = col * hw + (hw / 2 if row % 2 else 0); cy = row * 1.5 * r
            pts = [(cx + r * math.cos(math.radians(60 * k + 30)), cy + r * math.sin(math.radians(60 * k + 30))) for k in range(7)]
            d.line(pts, fill=color + (alpha,), width=1)
    # stronger on the right (decorations), almost nothing behind the title
    mask = Image.linear_gradient('L').rotate(90, expand=True).resize(size)  # 255 on the left -> 0 on the right
    layer.putalpha(Image.composite(layer.getchannel('A'), Image.new('L', size, 0), mask))
    return layer

def card(title, main_icon, deco):
    img = Image.new('RGBA', (W, H), WINDOW + (255,))
    d = ImageDraw.Draw(img)
    # outer window frame
    d.rectangle((0, 0, W - 1, H - 1), outline=(62, 66, 71), width=2)
    # inner menu section, like the rows of the courier board
    sx0, sy0, sx1, sy1 = 12, 12, W - 13, H - 13
    d.rectangle((sx0, sy0, sx1, sy1), fill=SECTION + (255,), outline=BORDER + (255,), width=1)
    img.alpha_composite(hex_pattern((sx1 - sx0 - 2, sy1 - sy0 - 2)), (sx0 + 1, sy0 + 1))

    # main icon on the tan gizmo background (the game's ButtonBG texture)
    slot = 136
    gx, gy = 34, (H - slot) // 2
    bg = tex('ButtonBG').convert('RGBA').resize((slot, slot), Image.LANCZOS)
    shadowed(img, bg, (gx, gy), blur=4, off=(2, 3), alpha=160)
    ic = fit(main_icon, 100)
    img.alpha_composite(ic, (gx + (slot - ic.width) // 2, gy + (slot - ic.height) // 2))

    # title
    font = ImageFont.truetype(FONT, 76)
    tx = gx + slot + 34
    bbox = d.textbbox((0, 0), title, font=font)
    th = bbox[3] - bbox[1]
    ty = (H - th) // 2 - bbox[1] - 10
    sh = Image.new('RGBA', img.size, (0, 0, 0, 0))
    ImageDraw.Draw(sh).text((tx + 3, ty + 4), title, font=font, fill=(0, 0, 0, 170))
    img.alpha_composite(sh.filter(ImageFilter.GaussianBlur(3)))
    ImageDraw.Draw(img).text((tx, ty), title, font=font, fill=TITLE)
    # gold rule under the title (the board's job-title colour), fading out to the right
    ry = ty + bbox[3] + 18
    rule = Image.new('RGBA', (430, 4), GOLD + (255,))
    fade = Image.linear_gradient('L').rotate(90, expand=True).resize((430, 4)).point(lambda v: 255 - v)
    rule.putalpha(fade)
    img.alpha_composite(rule, (tx, ry))

    # decorations on the right
    x = sx1 - 26
    for item in reversed(deco):
        im, box = item
        im = fit(im, box)
        x -= im.width
        shadowed(img, im, (x, (H - im.height) // 2), blur=4, off=(2, 3), alpha=140)
        x -= 22
    return img.convert('RGB')

crate = Image.open(CRATE).convert('RGBA'); crate = crate.crop(crate.getbbox())

cards = {
    'About': (tex('InfoButton'), [(tex('WorldRoutePlanner'), 110), (crate, 150)]),
    'Features': (tex('FulfillTradeRequest'), [(tex('LetterUnopened'), 70), (tex('Silver_c'), 80), (tex('CheckOn'), 64)]),
    'Compatibility': (tex('OptionsMod'), [(tex('ExpansionIcon_Royalty'), 64), (tex('ExpansionIcon_Ideology'), 64),
                                          (tex('ExpansionIcon_Biotech'), 64), (tex('ExpansionIcon_Anomaly'), 64),
                                          (tex('ExpansionIcon_Odyssey'), 64)]),
}
os.makedirs(OUT, exist_ok=True)
for name, (icon, deco) in cards.items():
    im = card(name, icon, deco)
    im.save(f'{OUT}/Card_{name}.png', optimize=True)
    print(name, im.size, os.path.getsize(f'{OUT}/Card_{name}.png'))
