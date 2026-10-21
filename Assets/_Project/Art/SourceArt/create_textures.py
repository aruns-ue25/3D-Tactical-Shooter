import os
import math
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter

TEXTURE_DIR = os.path.abspath(r"Assets/_Project/Art/Textures")
os.makedirs(TEXTURE_DIR, exist_ok=True)

def create_normal_map_from_height(height_arr, strength=2.0):
    """Generates a tangent-space normal map from a height/bump array (values 0..255)."""
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

def generate_player_rifle_textures():
    w, h = 1024, 1024
    albedo = Image.new('RGB', (w, h), (32, 35, 38))
    draw_alb = ImageDraw.Draw(albedo)
    height = Image.new('L', (w, h), 128)
    draw_h = ImageDraw.Draw(height)
    metallic = Image.new('L', (w, h), 180)
    draw_met = ImageDraw.Draw(metallic)
    smoothness = Image.new('L', (w, h), 120)
    draw_sm = ImageDraw.Draw(smoothness)
    emission = Image.new('RGB', (w, h), (0, 0, 0))
    draw_em = ImageDraw.Draw(emission)

    # 1. Receiver (top-left: x 0..512, y 0..512)
    # Background gunmetal
    for y in range(0, 512):
        grad = int(34 + 6 * math.sin(y * 0.05))
        draw_alb.line([(0, y), (512, y)], fill=(grad, grad + 3, grad + 6))
    
    # Receiver bevels & pin details
    draw_h.rectangle([10, 10, 502, 502], outline=210, width=4)
    # Ejection port
    draw_alb.rectangle([320, 180, 480, 280], fill=(16, 17, 19), outline=(48, 52, 56), width=3)
    draw_h.rectangle([320, 180, 480, 280], fill=60, outline=230, width=4)
    draw_met.rectangle([320, 180, 480, 280], fill=220)
    draw_sm.rectangle([320, 180, 480, 280], fill=160)
    # Bolt carrier inside ejection port with brass casing peek
    draw_alb.rectangle([340, 200, 460, 260], fill=(24, 26, 28))
    draw_alb.ellipse([360, 220, 430, 250], fill=(180, 140, 40)) # brass round
    draw_met.ellipse([360, 220, 430, 250], fill=240)
    draw_sm.ellipse([360, 220, 430, 250], fill=200)

    # Stamped text on receiver
    draw_alb.text((30, 80), "TS-556 TACTICAL", fill=(200, 205, 210))
    draw_alb.text((30, 105), "CAL: 5.56x45 NATO  |  SEMI-AUTO", fill=(150, 155, 160))
    draw_h.text((30, 80), "TS-556 TACTICAL", fill=220)
    
    # Fire selector switch
    draw_alb.ellipse([60, 220, 130, 290], fill=(22, 24, 26), outline=(60, 65, 70), width=3)
    draw_h.ellipse([60, 220, 130, 290], fill=200, outline=250, width=3)
    draw_alb.line([(95, 255), (95, 230)], fill=(220, 40, 40), width=4) # pointing to SEMI
    draw_alb.text((50, 200), "SAFE", fill=(140, 140, 140))
    draw_alb.text((100, 200), "SEMI", fill=(230, 60, 60))
    draw_alb.text((135, 240), "AUTO", fill=(140, 140, 140))

    # Takedown pins & screws
    for px, py in [(40, 450), (470, 450), (470, 50), (250, 50)]:
        draw_alb.ellipse([px-10, py-10, px+10, py+10], fill=(18, 20, 22), outline=(70, 75, 80), width=2)
        draw_h.ellipse([px-10, py-10, px+10, py+10], fill=240, outline=50, width=2)

    # 2. Barrel & Muzzle (top-right: x 512..1024, y 0..512)
    draw_alb.rectangle([512, 0, 1024, 512], fill=(26, 28, 30))
    draw_met.rectangle([512, 0, 1024, 512], fill=230)
    draw_sm.rectangle([512, 0, 1024, 512], fill=170)
    # Barrel fluting
    for fy in range(40, 480, 35):
        draw_alb.rectangle([530, fy, 980, fy+18], fill=(18, 20, 22), outline=(40, 44, 48), width=1)
        draw_h.rectangle([530, fy, 980, fy+18], fill=60, outline=200, width=2)
    # Muzzle brake ports
    draw_alb.rectangle([880, 20, 1010, 500], fill=(20, 22, 24), outline=(80, 85, 90), width=3)
    draw_h.rectangle([880, 20, 1010, 500], outline=240, width=4)
    for my in range(60, 480, 60):
        draw_alb.rounded_rectangle([900, my, 990, my+35], radius=6, fill=(10, 10, 10))
        draw_h.rounded_rectangle([900, my, 990, my+35], radius=6, fill=20, outline=240, width=3)

    # 3. Handguard & Rails (bottom-left: x 0..512, y 512..1024)
    draw_alb.rectangle([0, 512, 512, 1024], fill=(28, 31, 34))
    draw_met.rectangle([0, 512, 512, 1024], fill=190)
    draw_sm.rectangle([0, 512, 512, 1024], fill=130)
    # Picatinny rail ridges
    for rx in range(20, 500, 24):
        draw_alb.rectangle([rx, 530, rx+14, 620], fill=(36, 40, 44), outline=(18, 20, 22))
        draw_h.rectangle([rx, 530, rx+14, 620], fill=230, outline=70, width=2)
    # M-LOK ventilation slots
    for sx in range(40, 480, 80):
        for sy in [680, 780, 880]:
            draw_alb.rounded_rectangle([sx, sy, sx+60, sy+40], radius=8, fill=(12, 14, 15), outline=(45, 50, 55), width=2)
            draw_h.rounded_rectangle([sx, sy, sx+60, sy+40], radius=8, fill=30, outline=220, width=3)

    # 4. Grip, Stock, Magazine & Reflex Sight (bottom-right: x 512..1024, y 512..1024)
    # 4A Grip stippling (x 512..768, y 512..768)
    draw_alb.rectangle([512, 512, 768, 768], fill=(22, 24, 25))
    draw_met.rectangle([512, 512, 768, 768], fill=40)
    draw_sm.rectangle([512, 512, 768, 768], fill=80)
    for gx in range(525, 755, 12):
        for gy in range(525, 755, 12):
            draw_alb.ellipse([gx, gy, gx+4, gy+4], fill=(35, 38, 40))
            draw_h.ellipse([gx, gy, gx+4, gy+4], fill=210)

    # 4B Magazine (x 768..1024, y 512..768)
    draw_alb.rectangle([768, 512, 1024, 768], fill=(25, 27, 29))
    draw_met.rectangle([768, 512, 1024, 768], fill=80)
    draw_sm.rectangle([768, 512, 1024, 768], fill=110)
    # Ribbed horizontal ridges
    for my in range(530, 750, 25):
        draw_alb.rectangle([780, my, 990, my+14], fill=(32, 35, 38), outline=(18, 19, 20))
        draw_h.rectangle([780, my, 990, my+14], fill=220, outline=60, width=2)
    # Round count window
    draw_alb.rectangle([980, 550, 1005, 730], fill=(160, 130, 40), outline=(20, 20, 20), width=2)
    draw_met.rectangle([980, 550, 1005, 730], fill=200)
    draw_sm.rectangle([980, 550, 1005, 730], fill=190)

    # 4C Stock & Buttpad (x 512..768, y 768..1024)
    draw_alb.rectangle([512, 768, 768, 1024], fill=(26, 28, 30))
    draw_met.rectangle([512, 768, 768, 1024], fill=90)
    draw_sm.rectangle([512, 768, 768, 1024], fill=90)
    # Buttpad rubber ridges
    for by in range(780, 1010, 20):
        draw_alb.rectangle([530, by, 750, by+10], fill=(16, 17, 18))
        draw_h.rectangle([530, by, 750, by+10], fill=210, outline=80)

    # 4D Reflex Sight & Optic Lens (x 768..1024, y 768..1024)
    draw_alb.rectangle([768, 768, 1024, 1024], fill=(20, 22, 24))
    draw_met.rectangle([768, 768, 1024, 1024], fill=190)
    draw_sm.rectangle([768, 768, 1024, 1024], fill=150)
    # Optical glass lens in center
    lens_box = [810, 810, 980, 980]
    draw_alb.ellipse(lens_box, fill=(18, 35, 45), outline=(60, 65, 70), width=5)
    draw_met.ellipse(lens_box, fill=20)
    draw_sm.ellipse(lens_box, fill=245) # glossy glass
    draw_h.ellipse(lens_box, fill=110, outline=250, width=5)
    # Holographic Red Dot reticle
    cx, cy = 895, 895
    draw_alb.ellipse([cx-8, cy-8, cx+8, cy+8], fill=(255, 40, 40))
    draw_alb.ellipse([cx-28, cy-28, cx+28, cy+28], outline=(255, 60, 60), width=3)
    draw_em.ellipse([cx-8, cy-8, cx+8, cy+8], fill=(255, 50, 50))
    draw_em.ellipse([cx-28, cy-28, cx+28, cy+28], outline=(255, 80, 80), width=3)

    # Generate normal map
    h_arr = np.array(height)
    normal_map = create_normal_map_from_height(h_arr, strength=2.2)

    # Unity URP _MetallicGlossMap: R = Metallic, A = Smoothness
    m_arr = np.array(metallic)
    s_arr = np.array(smoothness)
    met_smooth = np.stack([m_arr, m_arr, m_arr, s_arr], axis=2).astype(np.uint8)
    met_smooth_img = Image.fromarray(met_smooth, mode='RGBA')

    # Save textures
    albedo_path = os.path.join(TEXTURE_DIR, "T_PlayerRifle_Albedo.png")
    met_smooth_path = os.path.join(TEXTURE_DIR, "T_PlayerRifle_MetallicSmoothness.png")
    normal_path = os.path.join(TEXTURE_DIR, "T_PlayerRifle_Normal.png")
    emission_path = os.path.join(TEXTURE_DIR, "T_PlayerRifle_Emission.png")

    albedo.save(albedo_path)
    met_smooth_img.save(met_smooth_path)
    normal_map.save(normal_path)
    emission.save(emission_path)
    print("Saved Player Rifle textures to", TEXTURE_DIR)

def generate_objective_terminal_textures():
    w, h = 1024, 1024
    albedo = Image.new('RGB', (w, h), (28, 32, 38))
    draw_alb = ImageDraw.Draw(albedo)
    height = Image.new('L', (w, h), 128)
    draw_h = ImageDraw.Draw(height)
    metallic = Image.new('L', (w, h), 160)
    draw_met = ImageDraw.Draw(metallic)
    smoothness = Image.new('L', (w, h), 140)
    draw_sm = ImageDraw.Draw(smoothness)
    emission = Image.new('RGB', (w, h), (0, 0, 0))
    draw_em = ImageDraw.Draw(emission)

    # 1. Main Display Screen (top-left: x 0..614, y 0..512)
    draw_alb.rectangle([0, 0, 614, 512], fill=(6, 12, 22))
    draw_h.rectangle([0, 0, 614, 512], fill=120)
    draw_met.rectangle([0, 0, 614, 512], fill=25)
    draw_sm.rectangle([0, 0, 614, 512], fill=245) # glossy screen

    # Screen border frame
    draw_alb.rectangle([8, 8, 606, 504], outline=(0, 180, 230), width=3)
    draw_em.rectangle([8, 8, 606, 504], outline=(0, 140, 190), width=2)

    # Top Header
    draw_alb.rectangle([10, 10, 604, 55], fill=(10, 24, 45))
    draw_alb.text((25, 20), "SECURE TERMINAL // PROJECT AEGIS LABS", fill=(0, 235, 255))
    draw_alb.text((450, 20), "NODE: 0x4F-LAB", fill=(255, 185, 20))
    draw_em.text((25, 20), "SECURE TERMINAL // PROJECT AEGIS LABS", fill=(0, 200, 230))
    draw_em.text((450, 20), "NODE: 0x4F-LAB", fill=(220, 160, 20))

    # Center Screen Radar / Target Telemetry
    rcx, rcy = 140, 200
    for r in [25, 55, 85]:
        draw_alb.ellipse([rcx-r, rcy-r, rcx+r, rcy+r], outline=(0, 150, 200), width=1)
        draw_em.ellipse([rcx-r, rcy-r, rcx+r, rcy+r], outline=(0, 100, 150), width=1)
    draw_alb.line([(rcx-95, rcy), (rcx+95, rcy)], fill=(0, 180, 220), width=1)
    draw_alb.line([(rcx, rcy-95), (rcx, rcy+95)], fill=(0, 180, 220), width=1)
    # Rotating scan sweep line
    draw_alb.line([(rcx, rcy), (rcx+70, rcy-45)], fill=(0, 255, 220), width=2)
    draw_em.line([(rcx, rcy), (rcx+70, rcy-45)], fill=(0, 255, 200), width=2)

    # Data download status window
    draw_alb.rectangle([260, 80, 580, 310], fill=(10, 18, 30), outline=(0, 140, 200), width=2)
    draw_em.rectangle([260, 80, 580, 310], outline=(0, 100, 160), width=1)
    draw_alb.text((275, 95), "SYSTEM STATUS: STANDBY / ARMED", fill=(255, 195, 30))
    draw_alb.text((275, 125), "ENCRYPTED MEMORY CORE: ONLINE", fill=(0, 230, 180))
    draw_alb.text((275, 150), "PAYLOAD: RESEARCH DATA ARCHIVE", fill=(180, 210, 235))
    draw_alb.text((275, 175), "CARRIER AUTH: AWAITING OPERATOR", fill=(255, 180, 30))
    draw_alb.text((275, 205), "[ HOLD 'E' TO TRANSMIT DATA ]", fill=(0, 255, 240))
    draw_em.text((275, 95), "SYSTEM STATUS: STANDBY / ARMED", fill=(210, 160, 25))
    draw_em.text((275, 125), "ENCRYPTED MEMORY CORE: ONLINE", fill=(0, 200, 150))
    draw_em.text((275, 205), "[ HOLD 'E' TO TRANSMIT DATA ]", fill=(0, 240, 230))

    # Download Progress Bar preview on screen
    draw_alb.rectangle([275, 240, 560, 275], fill=(15, 25, 40), outline=(0, 160, 220), width=2)
    draw_alb.rectangle([278, 243, 440, 272], fill=(0, 200, 240)) # 60% fill
    draw_em.rectangle([278, 243, 440, 272], fill=(0, 200, 240))
    draw_alb.text((455, 248), "62%", fill=(255, 255, 255))
    draw_em.text((455, 248), "62%", fill=(220, 220, 220))

    # Bottom Screen Telemetry Waveforms
    draw_alb.rectangle([25, 330, 580, 480], fill=(8, 16, 28), outline=(0, 100, 160), width=1)
    points = []
    for x in range(35, 570, 8):
        y = int(400 + 35 * math.sin(x * 0.08) * math.cos(x * 0.03))
        points.append((x, y))
    for i in range(len(points)-1):
        draw_alb.line([points[i], points[i+1]], fill=(0, 240, 180), width=2)
        draw_em.line([points[i], points[i+1]], fill=(0, 200, 150), width=2)
    draw_alb.text((35, 340), "QUANTUM ENCRYPTION BUS // TRANSMISSION TELEMETRY", fill=(0, 180, 220))

    # 2. Keypad & Controls Deck (top-right: x 614..1024, y 0..512)
    draw_alb.rectangle([614, 0, 1024, 512], fill=(35, 38, 44))
    draw_met.rectangle([614, 0, 1024, 512], fill=190)
    draw_sm.rectangle([614, 0, 1024, 512], fill=120)
    draw_h.rectangle([620, 10, 1014, 502], outline=210, width=4)
    # Keyboard keys grid
    for kx in range(640, 990, 36):
        for ky in range(40, 300, 32):
            draw_alb.rounded_rectangle([kx, ky, kx+28, ky+24], radius=4, fill=(18, 20, 24), outline=(65, 70, 78), width=1)
            draw_h.rounded_rectangle([kx, ky, kx+28, ky+24], radius=4, fill=220, outline=60, width=2)
            draw_em.rounded_rectangle([kx+2, ky+2, kx+26, ky+22], radius=3, outline=(0, 120, 160), width=1) # backlit keys
    # Command Trackball
    draw_alb.ellipse([650, 340, 750, 440], fill=(20, 45, 60), outline=(90, 100, 110), width=4)
    draw_h.ellipse([650, 340, 750, 440], fill=240, outline=40, width=4)
    draw_met.ellipse([650, 340, 750, 440], fill=230)
    draw_sm.ellipse([650, 340, 750, 440], fill=220)
    # Keycard authorization slot
    draw_alb.rectangle([800, 360, 980, 420], fill=(12, 14, 16), outline=(70, 75, 82), width=3)
    draw_h.rectangle([800, 360, 980, 420], fill=40, outline=230, width=3)
    draw_alb.rectangle([815, 385, 965, 395], fill=(5, 5, 5)) # slot opening
    draw_alb.text((820, 366), "KEYCARD AUTHORIZATION", fill=(170, 180, 190))
    # Green LED next to slot
    draw_alb.ellipse([985, 375, 1005, 395], fill=(30, 255, 60))
    draw_em.ellipse([985, 375, 1005, 395], fill=(30, 255, 60))

    # 3. Base Pedestal & Hazard Striping (bottom-left: x 0..512, y 512..1024)
    draw_alb.rectangle([0, 512, 512, 1024], fill=(28, 30, 35))
    draw_met.rectangle([0, 512, 512, 1024], fill=200)
    draw_sm.rectangle([0, 512, 512, 1024], fill=110)
    # Heavy border
    draw_h.rectangle([10, 522, 502, 1014], outline=240, width=6)
    # Yellow & Black Safety Hazard stripes around base rim
    for hx in range(-200, 700, 40):
        stripe_poly = [(hx, 920), (hx+25, 920), (hx-25, 1024), (hx-50, 1024)]
        draw_alb.polygon(stripe_poly, fill=(255, 185, 0))
    # Trim outlines over stripes
    draw_alb.rectangle([0, 920, 512, 926], fill=(40, 44, 50))
    draw_h.rectangle([0, 920, 512, 926], fill=230)
    # Floor anchor bolts in corners
    for bx, by in [(50, 560), (460, 560), (50, 870), (460, 870)]:
        draw_alb.ellipse([bx-18, by-18, bx+18, by+18], fill=(45, 50, 58), outline=(80, 85, 95), width=3)
        draw_h.ellipse([bx-18, by-18, bx+18, by+18], fill=250, outline=40, width=3)
        draw_met.ellipse([bx-18, by-18, bx+18, by+18], fill=240)

    # 4. Chassis Body, Vent Louvers, Cable Conduit, Top Beacon (bottom-right: x 512..1024, y 512..1024)
    draw_alb.rectangle([512, 512, 1024, 1024], fill=(32, 36, 42))
    draw_met.rectangle([512, 512, 1024, 1024], fill=190)
    draw_sm.rectangle([512, 512, 1024, 1024], fill=130)
    # Server cooling louvers
    for vy in range(540, 780, 24):
        draw_alb.rectangle([540, vy, 760, vy+14], fill=(14, 16, 18), outline=(50, 55, 62), width=1)
        draw_h.rectangle([540, vy, 760, vy+14], fill=40, outline=220, width=2)
    # Cable conduit ribs
    for cy in range(530, 1010, 18):
        draw_alb.rectangle([790, cy, 840, cy+10], fill=(20, 22, 25), outline=(60, 65, 72))
        draw_h.rectangle([790, cy, 840, cy+10], fill=230, outline=70)
    # Side auxiliary monitor (ECG / telemetry wave)
    draw_alb.rectangle([860, 540, 1010, 720], fill=(6, 16, 26), outline=(0, 160, 220), width=2)
    draw_h.rectangle([860, 540, 1010, 720], fill=100, outline=230, width=3)
    draw_alb.text((870, 550), "AUX TELEMETRY", fill=(0, 220, 240))
    draw_em.text((870, 550), "AUX TELEMETRY", fill=(0, 180, 200))
    aux_pts = [(870, 630), (895, 630), (905, 590), (915, 670), (925, 610), (935, 640), (945, 630), (1000, 630)]
    for i in range(len(aux_pts)-1):
        draw_alb.line([aux_pts[i], aux_pts[i+1]], fill=(30, 255, 120), width=2)
        draw_em.line([aux_pts[i], aux_pts[i+1]], fill=(30, 255, 100), width=2)
    # Top Beacon Light Bar (glowing amber/cyan status beacon)
    draw_alb.rectangle([540, 840, 760, 960], fill=(255, 170, 20), outline=(80, 60, 10), width=4)
    draw_em.rectangle([540, 840, 760, 960], fill=(255, 170, 20)) # High glow beacon
    draw_alb.text((560, 890), "OBJECTIVE BEACON", fill=(0, 0, 0))

    # Generate normal map
    h_arr = np.array(height)
    normal_map = create_normal_map_from_height(h_arr, strength=2.2)

    # Metallic / Smoothness map
    m_arr = np.array(metallic)
    s_arr = np.array(smoothness)
    met_smooth = np.stack([m_arr, m_arr, m_arr, s_arr], axis=2).astype(np.uint8)
    met_smooth_img = Image.fromarray(met_smooth, mode='RGBA')

    # Save textures
    albedo_path = os.path.join(TEXTURE_DIR, "T_ObjectiveTerminal_Albedo.png")
    met_smooth_path = os.path.join(TEXTURE_DIR, "T_ObjectiveTerminal_MetallicSmoothness.png")
    normal_path = os.path.join(TEXTURE_DIR, "T_ObjectiveTerminal_Normal.png")
    emission_path = os.path.join(TEXTURE_DIR, "T_ObjectiveTerminal_Emission.png")

    albedo.save(albedo_path)
    met_smooth_img.save(met_smooth_path)
    normal_map.save(normal_path)
    emission.save(emission_path)
    print("Saved Objective Terminal textures to", TEXTURE_DIR)

if __name__ == '__main__':
    generate_player_rifle_textures()
    generate_objective_terminal_textures()
