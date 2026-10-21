import os
import json
from PIL import Image, ImageDraw

def render_uv_layout_image(json_path, output_png, width=1024, height=1024, line_color=(0, 235, 255), bg_color=(18, 22, 28)):
    if not os.path.exists(json_path):
        print("UV data not found:", json_path)
        return

    with open(json_path, 'r') as f:
        polygons = json.load(f)

    img = Image.new('RGB', (width, height), bg_color)
    draw = ImageDraw.Draw(img, 'RGBA')

    # Draw grid
    for x in range(0, width, 64):
        draw.line([(x, 0), (x, height)], fill=(28, 34, 44), width=1)
    for y in range(0, height, 64):
        draw.line([(0, y), (width, y)], fill=(28, 34, 44), width=1)

    # 4 Quadrants major divider
    draw.line([(width // 2, 0), (width // 2, height)], fill=(48, 58, 72), width=2)
    draw.line([(0, height // 2), (width, height // 2)], fill=(48, 58, 72), width=2)

    # Draw each UV polygon
    for poly in polygons:
        if len(poly) < 3:
            continue
        pixel_pts = []
        for pt in poly:
            u, v = pt[0], pt[1]
            px = int(u * (width - 1))
            py = int((1.0 - v) * (height - 1))
            pixel_pts.append((px, py))
        
        fill_col = (line_color[0], line_color[1], line_color[2], 28)
        draw.polygon(pixel_pts, fill=fill_col)
        for i in range(len(pixel_pts)):
            p1 = pixel_pts[i]
            p2 = pixel_pts[(i + 1) % len(pixel_pts)]
            draw.line([p1, p2], fill=line_color, width=1)

    img.save(output_png)
    print("Saved UV Layout image to", output_png)

if __name__ == '__main__':
    render_uv_layout_image(
        r"Assets/_Project/Art/SourceArt/PlayerRifle_uv_data.json",
        r"Assets/_Project/Art/SourceArt/PlayerRifle_UV_Layout.png",
        line_color=(0, 230, 255)
    )
    render_uv_layout_image(
        r"Assets/_Project/Art/SourceArt/ObjectiveTerminal_uv_data.json",
        r"Assets/_Project/Art/SourceArt/ObjectiveTerminal_UV_Layout.png",
        line_color=(255, 185, 30)
    )
