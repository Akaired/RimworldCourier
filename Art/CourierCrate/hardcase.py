import cairosvg, random
from PIL import Image, ImageFilter, ImageChops

OL = "#1c2733"
def svg(seal=True):
    return f'''<svg xmlns="http://www.w3.org/2000/svg" width="512" height="512" viewBox="0 0 256 256">
<defs>
  <linearGradient id="lid" x1="0" y1="0" x2="0" y2="1">
    <stop offset="0" stop-color="#6b8fb6"/><stop offset="1" stop-color="#4a6f98"/>
  </linearGradient>
  <linearGradient id="body" x1="0" y1="0" x2="0" y2="1">
    <stop offset="0" stop-color="#3d6189"/><stop offset="1" stop-color="#2a4566"/>
  </linearGradient>
  <linearGradient id="metal" x1="0" y1="0" x2="1" y2="0">
    <stop offset="0" stop-color="#5a626b"/><stop offset="0.5" stop-color="#8b949c"/><stop offset="1" stop-color="#4d545c"/>
  </linearGradient>
</defs>
<!-- ground shadow -->
<ellipse cx="128" cy="214" rx="112" ry="16" fill="#000" opacity="0.22"/>

<!-- side handles (behind the body) -->
<path d="M30,150 L14,150 Q8,150 8,156 L8,176 Q8,182 14,182 L30,182" fill="none" stroke="{OL}" stroke-width="10" stroke-linejoin="round"/>
<path d="M30,150 L14,150 Q8,150 8,156 L8,176 Q8,182 14,182 L30,182" fill="none" stroke="#2b3036" stroke-width="5" stroke-linejoin="round"/>
<path d="M226,150 L242,150 Q248,150 248,156 L248,176 Q248,182 242,182 L226,182" fill="none" stroke="{OL}" stroke-width="10" stroke-linejoin="round"/>
<path d="M226,150 L242,150 Q248,150 248,156 L248,176 Q248,182 242,182 L226,182" fill="none" stroke="#2b3036" stroke-width="5" stroke-linejoin="round"/>

<!-- body (front face) -->
<path d="M24,124 L232,124 L232,194 Q232,206 220,206 L36,206 Q24,206 24,194 Z" fill="url(#body)" stroke="{OL}" stroke-width="5" stroke-linejoin="round"/>
<!-- bottom rim -->
<path d="M28,192 L228,192" stroke="#23405f" stroke-width="5"/>
<!-- body ribs (vertical reinforcement) -->
<g stroke="#34557a" stroke-width="7" stroke-linecap="round"><line x1="60" y1="166" x2="60" y2="188"/><line x1="196" y1="166" x2="196" y2="188"/></g>

<!-- lid (top face, seen from above) -->
<path d="M44,40 Q46,32 56,32 L200,32 Q210,32 212,40 L234,120 Q236,130 224,130 L32,130 Q20,130 22,120 Z" fill="url(#lid)" stroke="{OL}" stroke-width="5" stroke-linejoin="round"/>
<!-- lid inner panel -->
<path d="M58,46 L198,46 L214,112 L42,112 Z" fill="#58799e" opacity="0.55"/>
<!-- lid ribs, running front to back -->
<g>
  <path d="M96,34 L88,128" stroke="#34587f" stroke-width="12" stroke-linecap="round"/>
  <path d="M93,34 L85,128" stroke="#8fb0d0" stroke-width="4" stroke-linecap="round"/>
  <path d="M160,34 L168,128" stroke="#34587f" stroke-width="12" stroke-linecap="round"/>
  <path d="M157,34 L165,128" stroke="#8fb0d0" stroke-width="4" stroke-linecap="round"/>
</g>
<!-- top-edge highlight -->
<path d="M58,36 L198,36" stroke="#a9c3dc" stroke-width="3" stroke-linecap="round" opacity="0.8"/>
<!-- lid lip over the seam -->
<path d="M22,122 L234,122 L234,134 Q234,138 229,138 L27,138 Q22,138 22,134 Z" fill="#44678f" stroke="{OL}" stroke-width="4" stroke-linejoin="round"/>

<!-- latches -->
<g stroke="{OL}" stroke-width="3.5" stroke-linejoin="round">
  <rect x="66" y="128" width="30" height="40" rx="4" fill="url(#metal)"/>
  <rect x="160" y="128" width="30" height="40" rx="4" fill="url(#metal)"/>
  <rect x="71" y="118" width="20" height="14" rx="3" fill="#6e767e"/>
  <rect x="165" y="118" width="20" height="14" rx="3" fill="#6e767e"/>
</g>
<g stroke="#2b3036" stroke-width="3" stroke-linecap="round"><line x1="72" y1="152" x2="90" y2="152"/><line x1="166" y1="152" x2="184" y2="152"/></g>
<!-- padlock hasp in the middle -->
<rect x="118" y="134" width="20" height="20" rx="3" fill="#6e767e" stroke="{OL}" stroke-width="3.5"/>
<circle cx="128" cy="144" r="4" fill="#2b3036"/>
''' + (f'''
<!-- courier seal: red tamper strip through the hasp + tag -->
<path d="M128,150 C126,170 120,178 112,186" fill="none" stroke="{OL}" stroke-width="7" stroke-linecap="round"/>
<path d="M128,150 C126,170 120,178 112,186" fill="none" stroke="#c0392b" stroke-width="4" stroke-linecap="round"/>
<g transform="rotate(-12 106 190)">
  <rect x="92" y="182" width="30" height="18" rx="3" fill="#e9dfc6" stroke="{OL}" stroke-width="3"/>
  <circle cx="98" cy="191" r="3" fill="#c0392b"/>
  <line x1="104" y1="188" x2="117" y2="188" stroke="#8c8270" stroke-width="2.5"/>
  <line x1="104" y1="194" x2="114" y2="194" stroke="#8c8270" stroke-width="2.5"/>
</g>''' if seal else '') + '''
</svg>'''

def weather(img, seed=7):
    """Painterly grain + edge wear, only where the sprite is opaque."""
    random.seed(seed)
    w, h = img.size
    alpha = img.getchannel("A")
    # fine grain
    noise = Image.effect_noise((w, h), 22).convert("L")
    noise = noise.filter(ImageFilter.GaussianBlur(0.8))
    grain = Image.merge("RGB", (noise, noise, noise))
    base = img.convert("RGB")
    mixed = ImageChops.overlay(base, grain)
    out = Image.blend(base, mixed, 0.35)
    # scuffs: light worn spots, clipped to the body/lid colours (bluish pixels)
    wear = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    from PIL import ImageDraw
    d = ImageDraw.Draw(wear)
    edges_y = [int(h*0.17), int(h*0.25), int(h*0.50), int(h*0.53), int(h*0.78)]
    for _ in range(70):
        if random.random() < 0.7:
            y = random.choice(edges_y) + random.randint(-10, 10)
        else:
            y = random.randint(int(h*0.15), int(h*0.8))
        x = random.randint(40, w - 40)
        r = random.randint(2, 5) * w // 512
        d.ellipse((x - r, y - r // 2, x + r, y + r // 2), fill=(190, 198, 206, random.randint(25, 70)))
    for _ in range(18):
        x = random.randint(60, w - 60); y = random.randint(int(h*0.18), int(h*0.75))
        d.line((x, y, x + random.randint(-22, 22), y + random.randint(-5, 5)), fill=(200, 208, 214, 55), width=2)
    # grime towards the bottom of the body
    for _ in range(60):
        x = random.randint(50, w - 50); y = random.randint(int(h*0.66), int(h*0.80))
        r = random.randint(4, 10) * w // 512
        d.ellipse((x - r, y - r // 2, x + r, y + r // 2), fill=(40, 45, 50, random.randint(20, 45)))
    px = base.load()
    mask = Image.new("L", (w, h), 0); mp = mask.load()
    for yy in range(h):
        for xx in range(w):
            r_, g_, b_ = px[xx, yy]
            if b_ > r_ + 25 and b_ > 80:   # only blue plastic
                mp[xx, yy] = 255
    wear.putalpha(ImageChops.multiply(wear.getchannel("A"), mask))
    out = out.convert("RGBA"); out.alpha_composite(wear)
    out.putalpha(alpha)
    return out

for seal in (True, False):
    name = "D_hardcase_sealed" if seal else "D_hardcase_plain"
    cairosvg.svg2png(bytestring=svg(seal).encode(), write_to=f"{name}_raw.png", output_width=512, output_height=512)
    img = Image.open(f"{name}_raw.png").convert("RGBA")
    img = weather(img)
    img.resize((256, 256), Image.LANCZOS).save(f"{name}_256.png")
    img.resize((128, 128), Image.LANCZOS).save(f"{name}_128.png")
print("ok")
