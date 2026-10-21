import os
from PIL import Image, ImageFilter, ImageEnhance, ImageDraw, ImageFont
import numpy as np

OUTPUT_DIR = "Assets/_Project/Art/Textures"
REF_IMAGE_PATH = "C:/Users/Admin/.gemini/antigravity/brain/51cc9be5-527b-427c-bb3f-326f1c4d04af/.user_uploaded/media_1791994765244_7092f761.jpg"

os.makedirs(OUTPUT_DIR, exist_ok=True)

ref_img = Image.open(REF_IMAGE_PATH).convert("RGBA")

def create_normal_map(albedo_img, strength=2.2):
    gray = albedo_img.convert("L")
    arr = np.array(gray, dtype=np.float32)
    
    # Sobel kernels
    sobel_x = np.array([[-1, 0, 1], [-2, 0, 2], [-1, 0, 1]], dtype=np.float32)
    sobel_y = np.array([[-1, -2, -1], [0, 0, 0], [1, 2, 1]], dtype=np.float32)
    
    padded = np.pad(arr, 1, mode='edge')
    dx = (padded[0:-2, 2:] + 2*padded[1:-1, 2:] + padded[2:, 2:] - 
          padded[0:-2, 0:-2] - 2*padded[1:-1, 0:-2] - padded[2:, 0:-2]) / 8.0
    dy = (padded[2:, 0:-2] + 2*padded[2:, 1:-1] + padded[2:, 2:] - 
          padded[0:-2, 0:-2] - 2*padded[0:-2, 1:-1] - padded[0:-2, 2:]) / 8.0
    
    dx = dx * (strength / 255.0)
    dy = dy * (strength / 255.0)
    dz = np.ones_like(dx)
    
    norm = np.sqrt(dx**2 + dy**2 + dz**2)
    dx /= norm
    dy /= norm
    dz /= norm
    
    r = ((dx * 0.5 + 0.5) * 255).astype(np.uint8)
    g = (((-dy) * 0.5 + 0.5) * 255).astype(np.uint8)
    b = ((dz * 0.5 + 0.5) * 255).astype(np.uint8)
    
    normal_arr = np.stack([r, g, b, np.full_like(r, 255)], axis=2)
    return Image.fromarray(normal_arr, 'RGBA')

def create_metallic_smoothness_map(albedo_img, base_metallic=0, base_smoothness=70, metallic_mask=None, smoothness_mask=None):
    w, h = albedo_img.size
    r = np.full((h, w), base_metallic, dtype=np.uint8)
    g = np.full((h, w), 255, dtype=np.uint8)
    b = np.zeros((h, w), dtype=np.uint8)
    a = np.full((h, w), base_smoothness, dtype=np.uint8)
    
    if metallic_mask is not None:
        r = np.array(metallic_mask, dtype=np.uint8)
    if smoothness_mask is not None:
        a = np.array(smoothness_mask, dtype=np.uint8)
        
    stack = np.stack([r, g, b, a], axis=2)
    return Image.fromarray(stack, 'RGBA')

# ==============================================================================
# 1. SRIKARSAN SHIRT TEXTURE (Smooth Olive Green + Crisp TOKYO Collegiate Text)
# ==============================================================================
print("Generating Srikarsan Shirt Texture...")
shirt_w, shirt_h = 1024, 1024
# Base olive color from photo: RGB ~ (72, 92, 64)
shirt_albedo = Image.new("RGBA", (shirt_w, shirt_h), (72, 92, 64, 255))
draw_shirt = ImageDraw.Draw(shirt_albedo)

# Generate fine cotton knit fabric texture with subtle Perlin-like noise
np.random.seed(42)
noise = np.random.normal(0, 4.0, (shirt_h, shirt_w))
# Apply cotton weave modulation
weave_x = np.sin(np.linspace(0, 120 * np.pi, shirt_w))[None, :]
weave_y = np.cos(np.linspace(0, 120 * np.pi, shirt_h))[:, None]
weave = (weave_x * weave_y) * 3.5

arr_shirt = np.array(shirt_albedo, dtype=np.float32)
for c in range(3):
    arr_shirt[:, :, c] = np.clip(arr_shirt[:, :, c] + noise + weave, 0, 255)
shirt_albedo = Image.fromarray(arr_shirt.astype(np.uint8), "RGBA")
draw_shirt = ImageDraw.Draw(shirt_albedo)

# Crewneck ribbed collar at top (y: 0 to 60)
collar_base = (60, 78, 54, 255)
collar_highlight = (78, 98, 70, 255)
for y in range(0, 60, 4):
    draw_shirt.rectangle([0, y, shirt_w, y+2], fill=collar_base)
    draw_shirt.rectangle([0, y+2, shirt_w, y+4], fill=collar_highlight)

# Add bold, clean collegiate "TOKYO" print on chest
# Letters: T, O, K, Y, O in clean slab-serif collegiate style
# Draw crisp vector-like text
def draw_collegiate_letter(draw, char, x, y, w, h, fill_col):
    th = 16 # stroke thickness
    s = 10  # serif size
    if char == 'T':
        draw.rectangle([x, y, x + w, y + th], fill=fill_col) # top bar
        draw.rectangle([x, y + th, x + s, y + th + s], fill=fill_col) # left serif
        draw.rectangle([x + w - s, y + th, x + w, y + th + s], fill=fill_col) # right serif
        cx = x + (w - th) // 2
        draw.rectangle([cx, y + th, cx + th, y + h], fill=fill_col) # stem
        draw.rectangle([cx - s, y + h - s, cx + th + s, y + h], fill=fill_col) # bottom serif
    elif char == 'O':
        draw.rounded_rectangle([x, y, x + w, y + h], radius=14, fill=fill_col)
        draw.rounded_rectangle([x + th, y + th, x + w - th, y + h - th], radius=8, fill=(72, 92, 64, 255))
    elif char == 'K':
        draw.rectangle([x, y, x + th, y + h], fill=fill_col) # stem
        draw.rectangle([x - s//2, y, x + th + s//2, y + s], fill=fill_col) # top serif
        draw.rectangle([x - s//2, y + h - s, x + th + s//2, y + h], fill=fill_col) # bottom serif
        # diagonal strokes
        draw.polygon([(x + th, y + h//2), (x + w - s, y), (x + w, y), (x + th, y + h//2 + th)], fill=fill_col)
        draw.polygon([(x + th + 4, y + h//2 - 4), (x + w - s, y + h), (x + w, y + h), (x + th + 10, y + h//2 + th)], fill=fill_col)
        draw.rectangle([x + w - s, y, x + w, y + s], fill=fill_col)
        draw.rectangle([x + w - s, y + h - s, x + w, y + h], fill=fill_col)
    elif char == 'Y':
        cx = x + (w - th) // 2
        draw.rectangle([cx, y + h//2, cx + th, y + h], fill=fill_col) # bottom stem
        draw.rectangle([cx - s, y + h - s, cx + th + s, y + h], fill=fill_col) # bottom serif
        draw.polygon([(x, y), (x + s, y), (cx + th, y + h//2), (cx, y + h//2)], fill=fill_col) # left arm
        draw.polygon([(x + w - s, y), (x + w, y), (cx + th, y + h//2), (cx, y + h//2)], fill=fill_col) # right arm
        draw.rectangle([x, y, x + s + 4, y + s], fill=fill_col)
        draw.rectangle([x + w - s - 4, y, x + w, y + s], fill=fill_col)

# Text placement
text_y = 360
letter_w = 64
letter_h = 88
spacing = 18
letters = ['T', 'O', 'K', 'Y', 'O']
total_text_w = len(letters) * letter_w + (len(letters) - 1) * spacing
start_x = (shirt_w - total_text_w) // 2

tokyo_color = (248, 248, 244, 255) # Clean off-white
for i, l in enumerate(letters):
    lx = start_x + i * (letter_w + spacing)
    draw_collegiate_letter(draw_shirt, l, lx, text_y, letter_w, letter_h, tokyo_color)

# Hem stitching at bottom and sleeves
stitch_color = (56, 74, 50, 255)
for x in range(0, shirt_w, 10):
    draw_shirt.line([(x, 970), (x+6, 970)], fill=stitch_color, width=2)
    draw_shirt.line([(x, 982), (x+6, 982)], fill=stitch_color, width=2)

shirt_albedo.save(f"{OUTPUT_DIR}/T_Srikarsan_Shirt_Albedo.png")
shirt_normal = create_normal_map(shirt_albedo, strength=2.2)
shirt_normal.save(f"{OUTPUT_DIR}/T_Srikarsan_Shirt_Normal.png")
shirt_ms = create_metallic_smoothness_map(shirt_albedo, base_metallic=0, base_smoothness=60)
shirt_ms.save(f"{OUTPUT_DIR}/T_Srikarsan_Shirt_MetallicSmoothness.png")

# ==============================================================================
# 2. SRIKARSAN FACE & HEAD TEXTURE (Clean Segmented Photo Grounded Composite)
# ==============================================================================
print("Generating Srikarsan Face & Head Texture...")
face_w, face_h = 1024, 1024
# Warm South Asian skin tone
skin_tone = (182, 138, 108, 255)
face_albedo = Image.new("RGBA", (face_w, face_h), skin_tone)

# Srikarsan photo crop: (160, 200, 295, 420)
crop = ref_img.crop((160, 200, 295, 420)).convert("RGBA")
arr_crop = np.array(crop)

# Clean wall background removal
r = arr_crop[:, :, 0].astype(int)
g = arr_crop[:, :, 1].astype(int)
b = arr_crop[:, :, 2].astype(int)
wall_mask = (r > 175) & (g > 180) & (b > 180) & (abs(r - g) < 20) & (abs(g - b) < 20) & (r - b < 15)
arr_crop[wall_mask, 3] = 0

# Also clean grey shadow on right wall edge
wall_edge = (b > 160) & (g > 160) & (abs(r - b) < 18)
arr_crop[wall_edge, 3] = 0

head_cutout = Image.fromarray(arr_crop, 'RGBA')

# Target scale for central face region
target_face_w = 440
target_face_h = int(head_cutout.height * (target_face_w / head_cutout.width))
head_scaled = head_cutout.resize((target_face_w, target_face_h), Image.Resampling.LANCZOS)

# Dark quiff/pompadour hair for the top of head
hair_crop = head_cutout.crop((10, 10, head_cutout.width - 20, 95))
hair_scaled = hair_crop.resize((face_w, 280), Image.Resampling.LANCZOS)
face_albedo.paste(hair_scaled, (0, 0), hair_scaled)

# Paste central face features (Srikarsan's real eyes, nose, lips, beard)
paste_x = (face_w - target_face_w) // 2
paste_y = 230
face_albedo.paste(head_scaled, (paste_x, paste_y), head_scaled)

# Mirror ears and temple for symmetrical 3D head wrapping
ear_w = 120
ear_patch = head_scaled.crop((0, 80, ear_w, target_face_h - 20))
ear_flipped = ear_patch.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
face_albedo.paste(ear_flipped, (paste_x + target_face_w - 40, paste_y + 80), ear_flipped)

# Blend edges smoothly with skin tone
face_albedo.save(f"{OUTPUT_DIR}/T_Srikarsan_Face_Albedo.png")
face_normal = create_normal_map(face_albedo, strength=2.6)
face_normal.save(f"{OUTPUT_DIR}/T_Srikarsan_Face_Normal.png")

smooth_mask = np.full((face_h, face_w), 90, dtype=np.uint8) # Natural skin
smooth_mask[0:280, :] = 40 # Hair is matte
face_ms = create_metallic_smoothness_map(face_albedo, base_metallic=0, smoothness_mask=smooth_mask)
face_ms.save(f"{OUTPUT_DIR}/T_Srikarsan_Face_MetallicSmoothness.png")

# ==============================================================================
# 3. SRIKARSAN JEANS & BELT TEXTURE
# ==============================================================================
print("Generating Srikarsan Jeans & Belt Texture...")
jeans_w, jeans_h = 1024, 1024
jeans_albedo = Image.new("RGBA", (jeans_w, jeans_h), (92, 115, 140, 255))
draw_jeans = ImageDraw.Draw(jeans_albedo)

# Denim weave pattern
for y in range(0, jeans_h, 3):
    shade = int(92 + 14 * np.sin(y * 0.12))
    draw_jeans.line([(0, y), (jeans_w, (y + jeans_w) % jeans_h)], fill=(shade, shade+20, shade+42, 110), width=1)

# Faded stone-wash thighs
fade_overlay = Image.new("RGBA", (jeans_w, jeans_h), (0, 0, 0, 0))
draw_fade = ImageDraw.Draw(fade_overlay)
draw_fade.ellipse([140, 220, 380, 750], fill=(142, 165, 188, 100))
draw_fade.ellipse([640, 220, 880, 750], fill=(142, 165, 188, 100))
fade_overlay = fade_overlay.filter(ImageFilter.GaussianBlur(28))
jeans_albedo = Image.alpha_composite(jeans_albedo, fade_overlay)

draw_jeans = ImageDraw.Draw(jeans_albedo)
# Leather belt at waist (y: 0 to 90)
draw_jeans.rectangle([0, 0, jeans_w, 90], fill=(60, 40, 25, 255))
# Belt loops
for lx in [120, 320, 520, 720, 920]:
    draw_jeans.rectangle([lx, 0, lx+24, 90], fill=(50, 34, 20, 255))
    draw_jeans.line([(lx, 0), (lx, 90)], fill=(185, 140, 58, 255), width=2)
    draw_jeans.line([(lx+24, 0), (lx+24, 90)], fill=(185, 140, 58, 255), width=2)

# Belt buckle in center
draw_jeans.rounded_rectangle([460, 10, 560, 80], radius=8, fill=(215, 218, 225, 255), outline=(130, 135, 145, 255), width=4)
draw_jeans.rounded_rectangle([480, 25, 540, 65], radius=4, fill=(60, 40, 25, 255))
draw_jeans.rectangle([506, 14, 514, 76], fill=(235, 238, 245, 255))

# Gold seam stitching
gold_stitch = (205, 160, 65, 255)
for y in range(95, 360, 10):
    draw_jeans.line([(508, y), (508, y+6)], fill=gold_stitch, width=2)
    draw_jeans.line([(514, y), (514, y+6)], fill=gold_stitch, width=2)

jeans_albedo.save(f"{OUTPUT_DIR}/T_Srikarsan_Jeans_Albedo.png")
jeans_normal = create_normal_map(jeans_albedo, strength=2.5)
jeans_normal.save(f"{OUTPUT_DIR}/T_Srikarsan_Jeans_Normal.png")

metal_arr = np.zeros((jeans_h, jeans_w), dtype=np.uint8)
metal_arr[10:80, 460:560] = 230
smooth_arr = np.full((jeans_h, jeans_w), 60, dtype=np.uint8)
smooth_arr[10:80, 460:560] = 210 # Metal buckle
smooth_arr[0:90, :] = 110 # Leather belt
jeans_ms = create_metallic_smoothness_map(jeans_albedo, metallic_mask=metal_arr, smoothness_mask=smooth_arr)
jeans_ms.save(f"{OUTPUT_DIR}/T_Srikarsan_Jeans_MetallicSmoothness.png")

# ==============================================================================
# 4. SRIKARSAN WATCH & ACCESSORIES TEXTURE
# ==============================================================================
print("Generating Srikarsan Watch & Accessories Texture...")
acc_w, acc_h = 512, 512
acc_albedo = Image.new("RGBA", (acc_w, acc_h), (50, 35, 22, 255))
draw_acc = ImageDraw.Draw(acc_albedo)

# Leather watch strap
for y in range(0, acc_h, 8):
    draw_acc.line([(20, y), (20, y+4)], fill=(195, 150, 70, 255), width=2)
    draw_acc.line([(230, y), (230, y+4)], fill=(195, 150, 70, 255), width=2)

watch_center = (380, 256)
radius = 110
draw_acc.ellipse([watch_center[0]-radius, watch_center[1]-radius, watch_center[0]+radius, watch_center[1]+radius], 
                 fill=(225, 228, 235, 255), outline=(160, 165, 175, 255), width=6)
dial_r = 92
draw_acc.ellipse([watch_center[0]-dial_r, watch_center[1]-dial_r, watch_center[0]+dial_r, watch_center[1]+dial_r], 
                 fill=(250, 250, 252, 255))
for deg in range(0, 360, 30):
    rad = np.deg2rad(deg)
    x1 = watch_center[0] + (dial_r - 18) * np.cos(rad)
    y1 = watch_center[1] + (dial_r - 18) * np.sin(rad)
    x2 = watch_center[0] + (dial_r - 4) * np.cos(rad)
    y2 = watch_center[1] + (dial_r - 4) * np.sin(rad)
    draw_acc.line([(x1, y1), (x2, y2)], fill=(30, 32, 35, 255), width=3 if deg % 90 == 0 else 2)

draw_acc.line([watch_center, (watch_center[0]-45, watch_center[1]-45)], fill=(30, 32, 35, 255), width=4)
draw_acc.line([watch_center, (watch_center[0]+55, watch_center[1]-40)], fill=(30, 32, 35, 255), width=3)
draw_acc.line([watch_center, (watch_center[0], watch_center[1]+65)], fill=(200, 30, 30, 255), width=2)
draw_acc.ellipse([watch_center[0]-6, watch_center[1]-6, watch_center[0]+6, watch_center[1]+6], fill=(220, 225, 230, 255))

acc_albedo.save(f"{OUTPUT_DIR}/T_Srikarsan_Watch_Albedo.png")
acc_normal = create_normal_map(acc_albedo, strength=2.0)
acc_normal.save(f"{OUTPUT_DIR}/T_Srikarsan_Watch_Normal.png")

acc_metal = np.zeros((acc_h, acc_w), dtype=np.uint8)
acc_smooth = np.full((acc_h, acc_w), 120, dtype=np.uint8)
for y in range(acc_h):
    for x in range(acc_w):
        dist = np.sqrt((x - watch_center[0])**2 + (y - watch_center[1])**2)
        if dist <= radius:
            acc_metal[y, x] = 230
            acc_smooth[y, x] = 230

acc_ms = create_metallic_smoothness_map(acc_albedo, metallic_mask=acc_metal, smoothness_mask=acc_smooth)
acc_ms.save(f"{OUTPUT_DIR}/T_Srikarsan_Watch_MetallicSmoothness.png")

print("All Srikarsan refined photorealistic PBR textures generated successfully!")
