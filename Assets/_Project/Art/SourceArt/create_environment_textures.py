import os
import math
import numpy as np
from scipy.ndimage import gaussian_filter
from PIL import Image, ImageDraw, ImageFont

TEXTURE_DIR = os.path.abspath(r"Assets/_Project/Art/Textures")
os.makedirs(TEXTURE_DIR, exist_ok=True)

def create_normal_map_from_height(height_arr, strength=2.2):
    """Generates a tangent-space normal map from a height array (values 0..255)."""
    h = height_arr.astype(np.float32) / 255.0
    gy = np.gradient(h, axis=0)
    gx = np.gradient(h, axis=1)
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

# -------------------------------------------------------------------------
# 1. FLOOR TEXTURE: Weathered Rustic Wood Planks (Seamless Tiling)
# -------------------------------------------------------------------------
def generate_floor_textures():
    w, h = 1024, 1024
    num_planks = 10
    plank_width = w // num_planks  # ~102 px per plank

    np.random.seed(55)

    albedo_arr = np.zeros((h, w, 3), dtype=np.float32)
    height_arr = np.full((h, w), 128.0, dtype=np.float32)
    metallic_arr = np.zeros((h, w), dtype=np.float32)
    smoothness_arr = np.full((h, w), 70.0, dtype=np.float32)

    # Weathered rustic wood reference tone ratio (pure desaturated warm grey-brown)
    # R: 1.0, G: 0.94, B: 0.88
    base_lumas = [128.0, 116.0, 134.0, 122.0, 112.0, 130.0, 118.0, 126.0, 114.0, 125.0]

    butt_joints = [
        [320, 780],
        [640],
        [220, 720],
        [512],
        [160, 600],
        [820],
        [280, 760],
        [440],
        [190, 680],
        [580],
    ]

    y_coords, x_coords = np.indices((h, w))

    for p in range(num_planks):
        px0 = p * plank_width
        px1 = (p + 1) * plank_width if p < num_planks - 1 else w
        cur_w = px1 - px0
        luma = base_lumas[p % len(base_lumas)]

        cuts = sorted([0] + butt_joints[p] + [h])
        for s in range(len(cuts) - 1):
            y_start = cuts[s]
            y_end = cuts[s + 1]
            # Scalar brightness variation (keeps color hue 100% consistent)
            seg_factor = 1.0 + (np.random.rand() - 0.5) * 0.14
            seg_luma = np.clip(luma * seg_factor, 70.0, 180.0)

            # Assign RGB using exact wood taupe ratio
            albedo_arr[y_start:y_end, px0:px1, 0] = seg_luma
            albedo_arr[y_start:y_end, px0:px1, 1] = seg_luma * 0.94
            albedo_arr[y_start:y_end, px0:px1, 2] = seg_luma * 0.88

            board_elev = 128.0 + (np.random.rand() - 0.5) * 10.0
            height_arr[y_start:y_end, px0:px1] = board_elev

            board_sm = 65.0 + (np.random.rand() - 0.5) * 16.0
            smoothness_arr[y_start:y_end, px0:px1] = board_sm

        # Longitudinal wood grain
        freq1 = 0.09 + (p * 0.012)
        freq2 = 0.38 + (p * 0.025)
        grain_wave = np.sin(y_coords[:, px0:px1] * (2.0 * np.pi / h) * 4.0 + p * 1.5) * 3.5
        local_x = (x_coords[:, px0:px1] - px0) + grain_wave

        grain = np.sin(local_x * freq1 * 2.0 * np.pi) * 0.5 + np.sin(local_x * freq2 * 2.0 * np.pi) * 0.3
        
        fine_fibers = np.random.randn(h, cur_w) * 5.0
        fine_fibers = gaussian_filter(fine_fibers, sigma=[1.2, 0.4], mode=['wrap', 'nearest'])

        grain_mod = (grain * 10.0 + fine_fibers * 1.5)[:, :, np.newaxis]
        albedo_arr[:, px0:px1] = np.clip(albedo_arr[:, px0:px1] + grain_mod, 25, 235)
        height_arr[:, px0:px1] += grain * 5.5 + fine_fibers * 0.8

        # Bevel on plank edges
        bevel_w = 4
        for b in range(bevel_w):
            factor = (b / float(bevel_w))
            col_l = px0 + b
            albedo_arr[:, col_l] *= (0.68 + 0.32 * factor)
            height_arr[:, col_l] -= (bevel_w - b) * 3.5
            smoothness_arr[:, col_l] = 30.0

            col_r = px1 - 1 - b
            albedo_arr[:, col_r] *= (0.68 + 0.32 * factor)
            height_arr[:, col_r] -= (bevel_w - b) * 3.5
            smoothness_arr[:, col_r] = 30.0

        # Deep groove seam on plank borders
        albedo_arr[:, px0:px0+2] = [38, 34, 30]
        height_arr[:, px0:px0+2] = 40.0
        smoothness_arr[:, px0:px0+2] = 20.0

        if px1 < w:
            albedo_arr[:, px1-2:px1] = [38, 34, 30]
            height_arr[:, px1-2:px1] = 40.0
            smoothness_arr[:, px1-2:px1] = 20.0

        # Butt joints
        for j_y in butt_joints[p]:
            for dy in range(-2, 3):
                yy = (j_y + dy) % h
                albedo_arr[yy, px0:px1] = [38, 34, 30]
                height_arr[yy, px0:px1] = 45.0
                smoothness_arr[yy, px0:px1] = 20.0

            # Nail dots
            for nail_y in [(j_y - 10) % h, (j_y + 10) % h]:
                for nail_offset in [cur_w // 4, 3 * cur_w // 4]:
                    nx = px0 + nail_offset
                    for dy in range(-3, 4):
                        for dx in range(-3, 4):
                            dist = math.sqrt(dx*dx + dy*dy)
                            if dist <= 2.8:
                                ny = (nail_y + dy) % h
                                nxx = np.clip(nx + dx, 0, w - 1)
                                if dist <= 1.2:
                                    albedo_arr[ny, nxx] = [85, 82, 80]
                                    height_arr[ny, nxx] = 135.0
                                    metallic_arr[ny, nxx] = 200.0
                                    smoothness_arr[ny, nxx] = 160.0
                                else:
                                    albedo_arr[ny, nxx] = [30, 27, 24]
                                    height_arr[ny, nxx] = 65.0
                                    metallic_arr[ny, nxx] = 0.0
                                    smoothness_arr[ny, nxx] = 30.0

    # Ensure seamless wrap along top and bottom edges (Y axis)
    blend_h = 16
    for y in range(blend_h):
        alpha = y / float(blend_h)
        albedo_arr[y] = albedo_arr[y] * alpha + albedo_arr[h - blend_h + y] * (1.0 - alpha)
        height_arr[y] = height_arr[y] * alpha + height_arr[h - blend_h + y] * (1.0 - alpha)
        smoothness_arr[y] = smoothness_arr[y] * alpha + smoothness_arr[h - blend_h + y] * (1.0 - alpha)

    albedo_img = Image.fromarray(np.clip(albedo_arr, 0, 255).astype(np.uint8), mode='RGB')
    height_clip = np.clip(height_arr, 0, 255).astype(np.uint8)
    normal_img = create_normal_map_from_height(height_clip, strength=2.2)

    m_arr = np.clip(metallic_arr, 0, 255).astype(np.uint8)
    s_arr = np.clip(smoothness_arr, 0, 255).astype(np.uint8)
    met_smooth_arr = np.stack([m_arr, m_arr, m_arr, s_arr], axis=2)
    met_smooth_img = Image.fromarray(met_smooth_arr, mode='RGBA')

    alb_path = os.path.join(TEXTURE_DIR, "T_Floor_Planks_Albedo.png")
    nrm_path = os.path.join(TEXTURE_DIR, "T_Floor_Planks_Normal.png")
    ms_path  = os.path.join(TEXTURE_DIR, "T_Floor_Planks_MetallicSmoothness.png")

    albedo_img.save(alb_path)
    normal_img.save(nrm_path)
    met_smooth_img.save(ms_path)
    print("[SUCCESS] Successfully generated Floor Planks textures.")

# -------------------------------------------------------------------------
# 2. WALL TEXTURE: Weathered Plaster / Concrete Stucco (Seamless Tiling)
# -------------------------------------------------------------------------
def generate_wall_textures():
    w, h = 1024, 1024
    np.random.seed(101)

    base_r, base_g, base_b = 182.0, 180.0, 175.0

    def make_seamless_layer(sigma, scale):
        noise = np.random.randn(h, w)
        smooth = gaussian_filter(noise, sigma=sigma, mode='wrap')
        smooth = (smooth - smooth.mean()) / (smooth.std() + 1e-6) * scale
        return smooth

    mottle_large = make_seamless_layer(sigma=48.0, scale=12.0)
    mottle_med   = make_seamless_layer(sigma=16.0, scale=7.0)
    grit_fine    = make_seamless_layer(sigma=4.0,  scale=4.0)

    sand_noise = np.random.randn(h, w) * 5.0
    sand_noise = gaussian_filter(sand_noise, sigma=0.8, mode='wrap')

    combined_var = mottle_large + mottle_med + grit_fine + sand_noise

    r_chan = np.clip(base_r + combined_var * 0.95, 80, 240)
    g_chan = np.clip(base_g + combined_var * 0.95, 80, 240)
    b_chan = np.clip(base_b + combined_var * 0.90, 75, 235)

    grime = make_seamless_layer(sigma=32.0, scale=8.0)
    grime_mask = np.clip((grime - 2.0) / 6.0, 0.0, 1.0)
    r_chan -= grime_mask * 15.0
    g_chan -= grime_mask * 16.0
    b_chan -= grime_mask * 17.0

    albedo_arr = np.stack([r_chan, g_chan, b_chan], axis=2).astype(np.uint8)
    albedo_img = Image.fromarray(albedo_arr, mode='RGB')

    height_base = 128.0 + mottle_med * 0.4 + grit_fine * 1.5 + sand_noise * 1.5
    height_clip = np.clip(height_base, 0, 255).astype(np.uint8)
    normal_img = create_normal_map_from_height(height_clip, strength=1.8)

    m_arr = np.zeros((h, w), dtype=np.uint8)
    s_arr = np.clip(45.0 + mottle_med * 0.4 + grit_fine * 0.3, 20, 80).astype(np.uint8)
    met_smooth_arr = np.stack([m_arr, m_arr, m_arr, s_arr], axis=2)
    met_smooth_img = Image.fromarray(met_smooth_arr, mode='RGBA')

    alb_path = os.path.join(TEXTURE_DIR, "T_Wall_Plaster_Albedo.png")
    nrm_path = os.path.join(TEXTURE_DIR, "T_Wall_Plaster_Normal.png")
    ms_path  = os.path.join(TEXTURE_DIR, "T_Wall_Plaster_MetallicSmoothness.png")

    albedo_img.save(alb_path)
    normal_img.save(nrm_path)
    met_smooth_img.save(ms_path)
    print("[SUCCESS] Successfully generated Wall Plaster textures.")

# -------------------------------------------------------------------------
# 3. DOOR TEXTURE: Industrial Painted Metal Door (1:2 Aspect Ratio: 512 x 1024)
# -------------------------------------------------------------------------
def generate_door_textures():
    # 512 x 1024 matches the 1.5m x 3.5m door aspect ratio naturally
    w, h = 512, 1024
    albedo = Image.new('RGB', (w, h), (40, 95, 120))
    draw_alb = ImageDraw.Draw(albedo)
    height = Image.new('L', (w, h), 128)
    draw_h = ImageDraw.Draw(height)
    metallic = Image.new('L', (w, h), 0)
    draw_met = ImageDraw.Draw(metallic)
    smoothness = Image.new('L', (w, h), 95)
    draw_sm = ImageDraw.Draw(smoothness)

    # Reference image teal-blue industrial metal paint
    paint_blue = (42, 98, 124)
    paint_dark = (30, 72, 92)
    steel_metal = (165, 172, 178)
    dark_steel  = (55, 60, 65)

    # 1. Base metal surface with subtle vertical gradient
    for y in range(h):
        grad = int(math.sin(y * 0.015) * 5)
        c = (paint_blue[0] + grad, paint_blue[1] + grad, paint_blue[2] + grad)
        draw_alb.line([(0, y), (w, y)], fill=c)

    # 2. Outer heavy steel frame (margin 25px)
    draw_alb.rectangle([0, 0, w, h], outline=(24, 58, 76), width=25)
    draw_h.rectangle([25, 25, w-25, h-25], outline=215, width=5)

    # Rivets / bolts along outer frame
    # Top & bottom frame rivets
    for rx in range(45, w - 35, 55):
        # Top
        draw_alb.ellipse([rx-6, 7, rx+6, 19], fill=steel_metal, outline=dark_steel, width=1)
        draw_h.ellipse([rx-6, 7, rx+6, 19], fill=235, outline=40, width=2)
        draw_met.ellipse([rx-6, 7, rx+6, 19], fill=210)
        draw_sm.ellipse([rx-6, 7, rx+6, 19], fill=170)
        # Bottom
        draw_alb.ellipse([rx-6, h-19, rx+6, h-7], fill=steel_metal, outline=dark_steel, width=1)
        draw_h.ellipse([rx-6, h-19, rx+6, h-7], fill=235, outline=40, width=2)
        draw_met.ellipse([rx-6, h-19, rx+6, h-7], fill=210)
        draw_sm.ellipse([rx-6, h-19, rx+6, h-7], fill=170)

    # Left & right frame rivets
    for ry in range(50, h - 35, 60):
        # Left
        draw_alb.ellipse([7, ry-6, 19, ry+6], fill=steel_metal, outline=dark_steel, width=1)
        draw_h.ellipse([7, ry-6, 19, ry+6], fill=235, outline=40, width=2)
        draw_met.ellipse([7, ry-6, 19, ry+6], fill=210)
        draw_sm.ellipse([7, ry-6, 19, ry+6], fill=170)
        # Right
        draw_alb.ellipse([w-19, ry-6, w-7, ry+6], fill=steel_metal, outline=dark_steel, width=1)
        draw_h.ellipse([w-19, ry-6, w-7, ry+6], fill=235, outline=40, width=2)
        draw_met.ellipse([w-19, ry-6, w-7, ry+6], fill=210)
        draw_sm.ellipse([w-19, ry-6, w-7, ry+6], fill=170)

    # 3. Two Recessed Main Panels: Upper (y 45..470), Lower (y 530..975)
    # Upper Panel
    draw_alb.rectangle([45, 45, w-45, 470], fill=paint_dark, outline=(18, 45, 60), width=4)
    draw_h.rectangle([45, 45, w-45, 470], fill=85, outline=220, width=5)
    # Lower Panel
    draw_alb.rectangle([45, 530, w-45, 975], fill=paint_dark, outline=(18, 45, 60), width=4)
    draw_h.rectangle([45, 530, w-45, 975], fill=85, outline=220, width=5)

    # 4. Horizontal Ventilation Louvers on Upper Panel (Industrial locker / door vents)
    for ly in range(80, 220, 16):
        draw_alb.rectangle([80, ly, w-80, ly+7], fill=(16, 38, 50), outline=(45, 105, 135), width=1)
        draw_h.rectangle([80, ly, w-80, ly+7], fill=40, outline=210, width=2)
        draw_met.rectangle([80, ly, w-80, ly+7], fill=120)
        draw_sm.rectangle([80, ly, w-80, ly+7], fill=60)

    # Lower panel reinforcement cross-brace or louvers
    for ly in range(820, 945, 16):
        draw_alb.rectangle([80, ly, w-80, ly+7], fill=(16, 38, 50), outline=(45, 105, 135), width=1)
        draw_h.rectangle([80, ly, w-80, ly+7], fill=40, outline=210, width=2)
        draw_met.rectangle([80, ly, w-80, ly+7], fill=120)
        draw_sm.rectangle([80, ly, w-80, ly+7], fill=60)

    # 5. Stenciled Tactical Markings on Upper Panel
    draw_alb.text((120, 260), "RESTRICTED ACCESS", fill=(215, 225, 230))
    draw_h.text((120, 260), "RESTRICTED ACCESS", fill=190)
    draw_alb.text((120, 290), "SECTOR-B // RESEARCH LAB", fill=(160, 185, 200))
    draw_h.text((120, 290), "SECTOR-B // RESEARCH LAB", fill=180)

    # Yellow/Black hazard stripe bar
    for hx in range(120, w-120, 16):
        draw_alb.polygon([(hx, 325), (hx+10, 325), (hx+5, 345), (hx-5, 345)], fill=(225, 175, 25))
        draw_alb.polygon([(hx+10, 325), (hx+16, 325), (hx+11, 345), (hx+5, 345)], fill=(30, 30, 30))
    draw_h.rectangle([115, 323, w-115, 347], outline=220, width=2)

    # 6. Central Door Hardware: Heavy Steel Latch Plate & Handle (y 475..525, right side)
    draw_alb.rectangle([w-180, 465, w-55, 535], fill=dark_steel, outline=steel_metal, width=2)
    draw_h.rectangle([w-180, 465, w-55, 535], fill=175, outline=245, width=3)
    draw_met.rectangle([w-180, 465, w-55, 535], fill=220)
    draw_sm.rectangle([w-180, 465, w-55, 535], fill=160)

    # Mounting screws on plate corners
    for sx, sy in [(w-170, 475), (w-65, 475), (w-170, 525), (w-65, 525)]:
        draw_alb.ellipse([sx-4, sy-4, sx+4, sy+4], fill=steel_metal, outline=(30, 30, 30), width=1)
        draw_h.ellipse([sx-4, sy-4, sx+4, sy+4], fill=230, outline=60, width=2)
        draw_met.ellipse([sx-4, sy-4, sx+4, sy+4], fill=240)
        draw_sm.ellipse([sx-4, sy-4, sx+4, sy+4], fill=180)

    # Lever handle
    draw_alb.rounded_rectangle([w-155, 490, w-85, 512], radius=4, fill=steel_metal, outline=(40, 45, 50), width=2)
    draw_h.rounded_rectangle([w-155, 490, w-85, 512], radius=4, fill=230, outline=50, width=2)
    draw_met.rounded_rectangle([w-155, 490, w-85, 512], radius=4, fill=240)
    draw_sm.rounded_rectangle([w-155, 490, w-85, 512], radius=4, fill=190)

    # Green unlocked status LED
    draw_alb.ellipse([w-78, 497, w-70, 505], fill=(50, 220, 60))
    draw_sm.ellipse([w-78, 497, w-70, 505], fill=240)

    # 7. Paint Wear & Edge Chipping
    np.random.seed(88)
    alb_np = np.array(albedo)
    h_np   = np.array(height)
    m_np   = np.array(metallic)
    s_np   = np.array(smoothness)

    edge_noise = np.random.rand(h, w)
    wear_mask = np.zeros((h, w), dtype=bool)

    # Outer frame edges
    wear_mask[22:30, :] |= (edge_noise[22:30, :] > 0.65)
    wear_mask[h-30:h-22, :] |= (edge_noise[h-30:h-22, :] > 0.65)
    wear_mask[:, 22:30] |= (edge_noise[:, 22:30] > 0.65)
    wear_mask[:, w-30:w-22] |= (edge_noise[:, w-30:w-22] > 0.65)

    # Panel border wear
    wear_mask[42:50, 45:w-45] |= (edge_noise[42:50, 45:w-45] > 0.70)
    wear_mask[465:473, 45:w-45] |= (edge_noise[465:473, 45:w-45] > 0.70)
    wear_mask[525:533, 45:w-45] |= (edge_noise[525:533, 45:w-45] > 0.70)
    wear_mask[970:978, 45:w-45] |= (edge_noise[970:978, 45:w-45] > 0.70)

    alb_np[wear_mask] = dark_steel
    h_np[wear_mask] = 110
    m_np[wear_mask] = 220
    s_np[wear_mask] = 160

    albedo_img = Image.fromarray(alb_np, mode='RGB')
    normal_img = create_normal_map_from_height(h_np, strength=2.2)

    met_smooth_arr = np.stack([m_np, m_np, m_np, s_np], axis=2)
    met_smooth_img = Image.fromarray(met_smooth_arr, mode='RGBA')

    alb_path = os.path.join(TEXTURE_DIR, "T_Door_Metal_Albedo.png")
    nrm_path = os.path.join(TEXTURE_DIR, "T_Door_Metal_Normal.png")
    ms_path  = os.path.join(TEXTURE_DIR, "T_Door_Metal_MetallicSmoothness.png")

    albedo_img.save(alb_path)
    normal_img.save(nrm_path)
    met_smooth_img.save(ms_path)
    print("[SUCCESS] Successfully generated Door Metal textures.")

if __name__ == '__main__':
    generate_floor_textures()
    generate_wall_textures()
    generate_door_textures()
    print("All environment textures created successfully.")
