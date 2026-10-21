import os
import math
from PIL import Image, ImageDraw

TEX_DIR = os.path.abspath(r"Assets/_Project/Art/Textures")
os.makedirs(TEX_DIR, exist_ok=True)

# 1. Generate PBR Textures for Security Gate
W, H = 1024, 1024

# Albedo
albedo = Image.new("RGBA", (W, H), (38, 42, 46, 255)) # Dark tactical steel
draw = ImageDraw.Draw(albedo)

# Top & bottom frame borders
draw.rectangle([0, 0, W, 80], fill=(28, 30, 34, 255))
draw.rectangle([0, H - 80, W, H], fill=(24, 26, 30, 255))

# Vertical bar zones
for x in range(60, W - 60, 48):
    draw.rectangle([x - 6, 80, x + 6, H - 80], fill=(55, 60, 65, 255))
    draw.rectangle([x - 2, 80, x + 2, H - 80], fill=(75, 82, 90, 255)) # Highlight

# Center reinforcement plate with Hazard Stripes
plate_x0, plate_y0, plate_x1, plate_y1 = W//2 - 180, H//2 - 120, W//2 + 180, H//2 + 120
draw.rectangle([plate_x0, plate_y0, plate_x1, plate_y1], fill=(45, 48, 52, 255))
# Hazard stripe pattern on plate
stripe_w = 32
for i in range(-20, 30):
    sx = plate_x0 + i * stripe_w * 2
    poly = [
        (sx, plate_y1),
        (sx + stripe_w, plate_y1),
        (sx + stripe_w + (plate_y1 - plate_y0), plate_y0),
        (sx + (plate_y1 - plate_y0), plate_y0)
    ]
    # Clip polygon to plate bounds
    draw.polygon(poly, fill=(215, 170, 20, 255))

# Plate border & dark overlay trim
draw.rectangle([plate_x0, plate_y0, plate_x1, plate_y1], outline=(20, 22, 25, 255), width=6)
draw.rectangle([plate_x0 + 15, plate_y0 + 15, plate_x1 - 15, plate_y1 - 15], outline=(30, 32, 35, 255), width=3)

# Warning badge in center
draw.rectangle([W//2 - 120, H//2 - 35, W//2 + 120, H//2 + 35], fill=(18, 20, 22, 240), outline=(180, 40, 40, 255), width=3)
draw.rectangle([W//2 - 110, H//2 - 25, W//2 + 110, H//2 + 25], fill=(22, 24, 26, 255))

albedo.save(os.path.join(TEX_DIR, "T_SecurityGate_Albedo.png"))

# Normal Map
normal = Image.new("RGBA", (W, H), (128, 128, 255, 255))
ndraw = ImageDraw.Draw(normal)
# Bevels on plates and borders
ndraw.rectangle([0, 0, W, 80], outline=(150, 110, 255, 255), width=3)
ndraw.rectangle([plate_x0, plate_y0, plate_x1, plate_y1], outline=(160, 95, 255, 255), width=5)
for x in range(60, W - 60, 48):
    ndraw.line([(x - 6, 80), (x - 6, H - 80)], fill=(100, 128, 255, 255), width=2)
    ndraw.line([(x + 6, 80), (x + 6, H - 80)], fill=(155, 128, 255, 255), width=2)
normal.save(os.path.join(TEX_DIR, "T_SecurityGate_Normal.png"))

# Metallic / Smoothness (R=Metallic, G=AO, B=Detail, A=Smoothness)
met = Image.new("RGBA", (W, H), (210, 255, 0, 160)) # Heavy metal, semi-gloss
met.save(os.path.join(TEX_DIR, "T_SecurityGate_MetallicSmoothness.png"))

# Emission Map (Red perimeter beacons + status light)
emission = Image.new("RGBA", (W, H), (0, 0, 0, 255))
edraw = ImageDraw.Draw(emission)
# Red warning beacons on top corners
edraw.ellipse([40, 20, 70, 50], fill=(255, 30, 20, 255))
edraw.ellipse([W - 70, 20, W - 40, 50], fill=(255, 30, 20, 255))
# Red status bars in center badge
edraw.rectangle([W//2 - 90, H//2 - 8, W//2 + 90, H//2 + 8], fill=(255, 25, 20, 255))
emission.save(os.path.join(TEX_DIR, "T_SecurityGate_Emission.png"))

print("[OK] Security Gate textures generated.")
