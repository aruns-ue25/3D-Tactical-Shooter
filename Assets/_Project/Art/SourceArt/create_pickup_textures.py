import os
import math
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter

TEXTURE_DIR = os.path.abspath(r"Assets/_Project/Art/Textures")
os.makedirs(TEXTURE_DIR, exist_ok=True)

def create_normal_map_from_height(height_arr, strength=2.5):
    h = height_arr.astype(np.float32) / 255.0
    gy, gx = np.gradient(h)
    gx *= strength
    gy *= strength
    normal_x = -gx
    normal_y = -gy
    normal_z = 1.0
    norm = np.sqrt(normal_x**2 + normal_y**2 + normal_z**2)
    norm = np.maximum(norm, 1e-6)
    nx = (normal_x / norm * 0.5 + 0.5) * 255.0
    ny = (normal_y / norm * 0.5 + 0.5) * 255.0
    nz = (normal_z / norm * 0.5 + 0.5) * 255.0
    rgb = np.stack([nx, ny, nz], axis=2).astype(np.uint8)
    return Image.fromarray(rgb, mode='RGB')

def generate_ammo_box_textures():
    w, h = 1024, 1024
    # Olive drab military green: RGB(48, 56, 38)
    albedo = Image.new('RGB', (w, h), (46, 54, 38))
    draw_alb = ImageDraw.Draw(albedo)
    height = Image.new('L', (w, h), 128)
    draw_h = ImageDraw.Draw(height)
    metallic = Image.new('L', (w, h), 140)
    draw_met = ImageDraw.Draw(metallic)
    smoothness = Image.new('L', (w, h), 130)
    draw_sm = ImageDraw.Draw(smoothness)
    emission = Image.new('RGB', (w, h), (0, 0, 0))
    draw_em = ImageDraw.Draw(emission)

    # 1. Box Body (top half: y 0..512)
    # Stamped side ribs
    for y in [120, 200, 280, 360]:
        draw_h.rectangle([40, y, 984, y + 26], fill=190, outline=220, width=2)
        draw_alb.rectangle([40, y, 984, y + 26], fill=(54, 62, 44), outline=(36, 42, 30), width=2)
        draw_met.rectangle([40, y, 984, y + 26], fill=160)
        draw_sm.rectangle([40, y, 984, y + 26], fill=150)

    # Yellow Hazard chevron borders
    chevron_w = 40
    for x in range(40, 984, chevron_w * 2):
        draw_alb.polygon([(x, 30), (x + chevron_w, 30), (x + chevron_w - 15, 60), (x - 15, 60)], fill=(220, 180, 20))
        draw_alb.polygon([(x, 450), (x + chevron_w, 450), (x + chevron_w - 15, 480), (x - 15, 480)], fill=(220, 180, 20))

    # Military Stenciled Text
    draw_alb.text((120, 150), "5.56x45mm NATO BALL M855", fill=(235, 195, 30))
    draw_alb.text((120, 175), "30 RDS  |  10 CLIPS  |  COMBAT SURPLUS", fill=(210, 175, 25))
    draw_alb.text((120, 240), "LOT: TS-8842-A  |  NSN: 1305-01-522-8703", fill=(170, 180, 150))
    draw_alb.text((120, 320), "RESTRICTED ARMAMENT — DEFENSE PROTOCOL", fill=(235, 195, 30))

    # 2. Lid & Latches (bottom half: y 512..1024)
    # Dark steel lid plate
    draw_alb.rectangle([40, 530, 984, 760], fill=(32, 36, 30), outline=(22, 26, 20), width=4)
    draw_h.rectangle([40, 530, 984, 760], fill=170, outline=230, width=4)
    draw_met.rectangle([40, 530, 984, 760], fill=210)
    draw_sm.rectangle([40, 530, 984, 760], fill=160)

    # Steel latch buckles
    for lx in [220, 512, 800]:
        draw_alb.rectangle([lx - 45, 780, lx + 45, 960], fill=(70, 75, 80), outline=(30, 32, 35), width=3)
        draw_h.rectangle([lx - 45, 780, lx + 45, 960], fill=220, outline=255, width=3)
        draw_met.rectangle([lx - 45, 780, lx + 45, 960], fill=240)
        draw_sm.rectangle([lx - 45, 780, lx + 45, 960], fill=190)

        # Latch pin
        draw_alb.ellipse([lx - 15, 840, lx + 15, 870], fill=(130, 135, 140))
        draw_h.ellipse([lx - 15, 840, lx + 15, 870], fill=255)

        # Amber glowing status LED on center latch
        if lx == 512:
            draw_alb.ellipse([lx - 12, 630, lx + 12, 654], fill=(255, 180, 20))
            draw_em.ellipse([lx - 12, 630, lx + 12, 654], fill=(255, 175, 15))

    normal = create_normal_map_from_height(np.array(height))
    # Unity standard mask: R=Metallic, G=AmbientOcclusion (255), B=Detail(0), A=Smoothness
    met_arr = np.array(metallic)
    ao_arr = np.full((h, w), 255, dtype=np.uint8)
    det_arr = np.zeros((h, w), dtype=np.uint8)
    sm_arr = np.array(smoothness)
    mask_arr = np.stack([met_arr, ao_arr, det_arr, sm_arr], axis=2)
    mask_img = Image.fromarray(mask_arr, mode='RGBA')

    albedo.save(os.path.join(TEXTURE_DIR, "T_AmmoBox_Albedo.png"))
    normal.save(os.path.join(TEXTURE_DIR, "T_AmmoBox_Normal.png"))
    mask_img.save(os.path.join(TEXTURE_DIR, "T_AmmoBox_MetallicSmoothness.png"))
    emission.save(os.path.join(TEXTURE_DIR, "T_AmmoBox_Emission.png"))
    print("[OK] Ammo Box textures generated!")

def generate_medkit_textures():
    w, h = 1024, 1024
    # Tactical clean white composite: RGB(225, 228, 230)
    albedo = Image.new('RGB', (w, h), (225, 228, 230))
    draw_alb = ImageDraw.Draw(albedo)
    height = Image.new('L', (w, h), 128)
    draw_h = ImageDraw.Draw(height)
    metallic = Image.new('L', (w, h), 40)
    draw_met = ImageDraw.Draw(metallic)
    smoothness = Image.new('L', (w, h), 160)
    draw_sm = ImageDraw.Draw(smoothness)
    emission = Image.new('RGB', (w, h), (0, 0, 0))
    draw_em = ImageDraw.Draw(emission)

    # 1. Tactical Red Lid Frame
    draw_alb.rectangle([30, 30, 994, 994], outline=(195, 30, 30), width=32)
    draw_h.rectangle([30, 30, 994, 994], outline=210, width=32)
    draw_sm.rectangle([30, 30, 994, 994], fill=180)

    # Dark rubberized corner bumpers
    for bx, by in [(30, 30), (914, 30), (30, 914), (914, 914)]:
        draw_alb.rectangle([bx, by, bx + 80, by + 80], fill=(28, 30, 32))
        draw_h.rectangle([bx, by, bx + 80, by + 80], fill=240)
        draw_met.rectangle([bx, by, bx + 80, by + 80], fill=20)
        draw_sm.rectangle([bx, by, bx + 80, by + 80], fill=60)

    # 2. Large Glowing Emerald Medical Cross on Lid (center: 512, 360)
    cx, cy = 512, 360
    arm_w = 70
    arm_l = 180

    # Cross background red border
    draw_alb.rectangle([cx - arm_w - 12, cy - arm_l - 12, cx + arm_w + 12, cy + arm_l + 12], fill=(180, 20, 20))
    draw_alb.rectangle([cx - arm_l - 12, cy - arm_w - 12, cx + arm_l + 12, cy + arm_w + 12], fill=(180, 20, 20))

    # Glowing Vibrant Emerald Green Cross
    green_col = (40, 240, 110)
    draw_alb.rectangle([cx - arm_w, cy - arm_l, cx + arm_w, cy + arm_l], fill=green_col)
    draw_alb.rectangle([cx - arm_l, cy - arm_w, cx + arm_l, cy + arm_w], fill=green_col)
    draw_h.rectangle([cx - arm_w, cy - arm_l, cx + arm_w, cy + arm_l], fill=255)
    draw_h.rectangle([cx - arm_l, cy - arm_w, cx + arm_l, cy + arm_w], fill=255)

    # Emissive mask
    draw_em.rectangle([cx - arm_w, cy - arm_l, cx + arm_w, cy + arm_l], fill=(35, 235, 100))
    draw_em.rectangle([cx - arm_l, cy - arm_w, cx + arm_l, cy + arm_w], fill=(35, 235, 100))

    # 3. Medical Info & Vitals Monitor readout
    draw_alb.rectangle([140, 620, 884, 760], fill=(18, 22, 25), outline=(50, 58, 65), width=3)
    draw_h.rectangle([140, 620, 884, 760], fill=80, outline=200, width=3)
    draw_met.rectangle([140, 620, 884, 760], fill=120)
    draw_sm.rectangle([140, 620, 884, 760], fill=220)

    # Monitor texts and ECG heartbeat line
    draw_alb.text((160, 638), "TACTICAL TRAUMA KIT  |  FIELD SURGICAL COMPOSITE", fill=(220, 225, 230))
    draw_alb.text((160, 665), "NANOMEDIC STIMULANT INJECTION — RESTORES +30 HP", fill=(40, 240, 110))
    draw_alb.text((160, 700), "STATUS: STERILE  |  AUTOCLAVE SEAL INTACT  |  LEVEL 4 BIO-KIT", fill=(140, 160, 180))

    # Green glowing ECG monitor graph
    draw_em.text((160, 665), "NANOMEDIC STIMULANT INJECTION — RESTORES +30 HP", fill=(35, 235, 100))

    # 4. Latches and Handle at bottom
    for lx in [340, 684]:
        draw_alb.rectangle([lx - 35, 820, lx + 35, 940], fill=(65, 70, 75), outline=(35, 38, 42), width=3)
        draw_h.rectangle([lx - 35, 820, lx + 35, 940], fill=230, outline=255, width=3)
        draw_met.rectangle([lx - 35, 820, lx + 35, 940], fill=240)
        draw_sm.rectangle([lx - 35, 820, lx + 35, 940], fill=190)

    normal = create_normal_map_from_height(np.array(height))
    met_arr = np.array(metallic)
    ao_arr = np.full((h, w), 255, dtype=np.uint8)
    det_arr = np.zeros((h, w), dtype=np.uint8)
    sm_arr = np.array(smoothness)
    mask_arr = np.stack([met_arr, ao_arr, det_arr, sm_arr], axis=2)
    mask_img = Image.fromarray(mask_arr, mode='RGBA')

    albedo.save(os.path.join(TEXTURE_DIR, "T_Medkit_Albedo.png"))
    normal.save(os.path.join(TEXTURE_DIR, "T_Medkit_Normal.png"))
    mask_img.save(os.path.join(TEXTURE_DIR, "T_Medkit_MetallicSmoothness.png"))
    emission.save(os.path.join(TEXTURE_DIR, "T_Medkit_Emission.png"))
    print("[OK] Medkit textures generated!")

if __name__ == "__main__":
    generate_ammo_box_textures()
    generate_medkit_textures()
