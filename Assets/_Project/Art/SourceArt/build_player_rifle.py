import bpy
import bmesh
import math
import os
import json
from mathutils import Vector, Matrix, Euler

# Paths
SOURCE_ART_DIR = os.path.abspath(r"Assets/_Project/Art/SourceArt")
MODELS_DIR = os.path.abspath(r"Assets/_Project/Art/Models")
TEXTURE_DIR = os.path.abspath(r"Assets/_Project/Art/Textures")
os.makedirs(SOURCE_ART_DIR, exist_ok=True)
os.makedirs(MODELS_DIR, exist_ok=True)

BLEND_PATH = os.path.join(SOURCE_ART_DIR, "PlayerRifle_Custom.blend")
FBX_PATH = os.path.join(MODELS_DIR, "PlayerRifle_Custom.fbx")
RENDER_PATH = os.path.join(SOURCE_ART_DIR, "PlayerRifle_Blender_Render.png")
UV_DATA_PATH = os.path.join(SOURCE_ART_DIR, "PlayerRifle_uv_data.json")

ALBEDO_TEX = os.path.join(TEXTURE_DIR, "T_PlayerRifle_Albedo.png")
NORMAL_TEX = os.path.join(TEXTURE_DIR, "T_PlayerRifle_Normal.png")
MET_TEX = os.path.join(TEXTURE_DIR, "T_PlayerRifle_MetallicSmoothness.png")
EMISSION_TEX = os.path.join(TEXTURE_DIR, "T_PlayerRifle_Emission.png")

def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)

def create_rifle():
    bm = bmesh.new()

    group_faces = {
        'receiver': [],
        'barrel': [],
        'handguard': [],
        'stock': [],
        'magazine': [],
        'sight_frame': [],
        'sight_lens': []
    }

    # In Blender: Forward is +Y, Up is +Z, Right is +X
    def add_box(center, size, group_name):
        hx, hy, hz = size.x * 0.5, size.y * 0.5, size.z * 0.5
        v1 = bm.verts.new(center + Vector((-hx, -hy, -hz)))
        v2 = bm.verts.new(center + Vector(( hx, -hy, -hz)))
        v3 = bm.verts.new(center + Vector(( hx,  hy, -hz)))
        v4 = bm.verts.new(center + Vector((-hx,  hy, -hz)))
        v5 = bm.verts.new(center + Vector((-hx, -hy,  hz)))
        v6 = bm.verts.new(center + Vector(( hx, -hy,  hz)))
        v7 = bm.verts.new(center + Vector(( hx,  hy,  hz)))
        v8 = bm.verts.new(center + Vector((-hx,  hy,  hz)))

        faces = [
            bm.faces.new((v4, v3, v2, v1)), # -Y rear
            bm.faces.new((v5, v6, v7, v8)), # +Y front
            bm.faces.new((v1, v2, v6, v5)), # -Z bottom
            bm.faces.new((v3, v4, v8, v7)), # +Z top
            bm.faces.new((v1, v5, v8, v4)), # -X left
            bm.faces.new((v2, v3, v7, v6)), # +X right
        ]
        for f in faces:
            f.smooth = False
            group_faces[group_name].append(f)
        return faces

    def add_cylinder(center, radius, length, segments=12, axis='Y', group_name='barrel'):
        verts_ring1 = []
        verts_ring2 = []
        half_len = length * 0.5
        for i in range(segments):
            angle = 2.0 * math.pi * i / segments
            cos_a = math.cos(angle) * radius
            sin_a = math.sin(angle) * radius
            if axis == 'Y':
                p1 = center + Vector((cos_a, -half_len, sin_a))
                p2 = center + Vector((cos_a,  half_len, sin_a))
            elif axis == 'Z':
                p1 = center + Vector((cos_a, sin_a, -half_len))
                p2 = center + Vector((cos_a, sin_a,  half_len))
            else:
                p1 = center + Vector((-half_len, cos_a, sin_a))
                p2 = center + Vector(( half_len, cos_a, sin_a))
            verts_ring1.append(bm.verts.new(p1))
            verts_ring2.append(bm.verts.new(p2))

        faces = []
        for i in range(segments):
            next_i = (i + 1) % segments
            v_a = verts_ring1[i]
            v_b = verts_ring1[next_i]
            v_c = verts_ring2[next_i]
            v_d = verts_ring2[i]
            f = bm.faces.new((v_a, v_b, v_c, v_d))
            f.smooth = True
            group_faces[group_name].append(f)
            faces.append(f)

        cap1 = bm.faces.new(reversed(verts_ring1))
        cap2 = bm.faces.new(verts_ring2)
        group_faces[group_name].append(cap1)
        group_faces[group_name].append(cap2)
        faces.extend([cap1, cap2])
        return faces

    # --- 1. RECEIVER (Y in [-0.12, 0.14], Z in [-0.07, 0.08]) ---
    # Upper Receiver
    add_box(Vector((0.0, 0.01, 0.04)), Vector((0.048, 0.26, 0.07)), 'receiver')
    # Lower Receiver
    add_box(Vector((0.0, -0.01, -0.03)), Vector((0.044, 0.22, 0.065)), 'receiver')
    # Top Picatinny Rail ridges on receiver
    for ry in range(-10, 14, 2):
        add_box(Vector((0.0, ry * 0.01, 0.082)), Vector((0.036, 0.012, 0.014)), 'receiver')
    # Trigger guard
    add_box(Vector((0.0, -0.04, -0.075)), Vector((0.018, 0.08, 0.008)), 'receiver')
    add_box(Vector((0.0, -0.078, -0.062)), Vector((0.018, 0.008, 0.022)), 'receiver')
    # Trigger blade
    add_box(Vector((0.0, -0.04, -0.055)), Vector((0.012, 0.012, 0.028)), 'receiver')
    # Magwell flare
    add_box(Vector((0.0, 0.05, -0.068)), Vector((0.042, 0.07, 0.025)), 'receiver')
    # Charging handle
    add_box(Vector((0.0, -0.13, 0.076)), Vector((0.06, 0.03, 0.014)), 'receiver')

    # --- 2. BARREL & MUZZLE (Y from 0.14 to 0.52) ---
    # Barrel tube
    add_cylinder(Vector((0.0, 0.30, 0.04)), radius=0.013, length=0.32, segments=12, axis='Y', group_name='barrel')
    # Gas block
    add_box(Vector((0.0, 0.32, 0.058)), Vector((0.028, 0.04, 0.035)), 'barrel')
    add_cylinder(Vector((0.0, 0.24, 0.065)), radius=0.006, length=0.20, segments=8, axis='Y', group_name='barrel')
    # Muzzle Brake
    add_cylinder(Vector((0.0, 0.49, 0.04)), radius=0.019, length=0.06, segments=12, axis='Y', group_name='barrel')
    # Side expansion ports
    add_box(Vector((0.0, 0.485, 0.04)), Vector((0.042, 0.025, 0.012)), 'barrel')

    # --- 3. HANDGUARD & RAILS (Y from 0.14 to 0.34) ---
    add_box(Vector((0.0, 0.24, 0.04)), Vector((0.056, 0.20, 0.068)), 'handguard')
    # Top handguard rail
    for hy in range(15, 34, 2):
        add_box(Vector((0.0, hy * 0.01, 0.079)), Vector((0.034, 0.012, 0.012)), 'handguard')
    # Bottom rail
    for by in range(16, 32, 3):
        add_box(Vector((0.0, by * 0.01, 0.002)), Vector((0.032, 0.014, 0.010)), 'handguard')

    # --- 4A. PISTOL GRIP & STOCK (rear / bottom) ---
    # Pistol grip segments angled back
    for step in range(5):
        gy = -0.085 - step * 0.012
        gz = -0.06 - step * 0.024
        add_box(Vector((0.0, gy, gz)), Vector((0.034, 0.048, 0.024)), 'stock')
    # Buffer tube extending rearward
    add_cylinder(Vector((0.0, -0.22, 0.04)), radius=0.018, length=0.18, segments=12, axis='Y', group_name='stock')
    # Tactical stock body
    add_box(Vector((0.0, -0.26, 0.035)), Vector((0.042, 0.12, 0.07)), 'stock')
    add_box(Vector((0.0, -0.28, -0.02)), Vector((0.038, 0.09, 0.08)), 'stock')
    # Shoulder buttpad
    add_box(Vector((0.0, -0.325, 0.01)), Vector((0.044, 0.02, 0.14)), 'stock')

    # --- 4B. CURVED MAGAZINE ---
    for m in range(5):
        my = 0.05 + m * 0.008
        mz = -0.08 - m * 0.028
        add_box(Vector((0.0, my, mz)), Vector((0.028, 0.068, 0.028)), 'magazine')
    # Magazine floorplate
    add_box(Vector((0.0, 0.09, -0.225)), Vector((0.034, 0.076, 0.012)), 'magazine')

    # --- 4C. REFLEX SIGHT & LENS ---
    # Sight mount clamp
    add_box(Vector((0.0, 0.04, 0.095)), Vector((0.044, 0.09, 0.015)), 'sight_frame')
    # Hood frame
    add_box(Vector(( 0.022, 0.04, 0.135)), Vector((0.008, 0.07, 0.065)), 'sight_frame') # right
    add_box(Vector((-0.022, 0.04, 0.135)), Vector((0.008, 0.07, 0.065)), 'sight_frame') # left
    add_box(Vector(( 0.0,   0.04, 0.168)), Vector((0.048, 0.07, 0.008)), 'sight_frame') # top
    # Lens disc in center
    add_cylinder(Vector((0.0, 0.04, 0.135)), radius=0.019, length=0.004, segments=12, axis='Y', group_name='sight_lens')

    # --- UV UNWRAPPING PER GROUP ---
    uv_layer = bm.loops.layers.uv.new("UVMap")
    uv_polygons = []

    group_bounds = {
        'receiver':    (0.04, 0.54, 0.46, 0.96),
        'barrel':      (0.54, 0.54, 0.96, 0.96),
        'handguard':   (0.04, 0.04, 0.46, 0.46),
        'stock':       (0.54, 0.04, 0.74, 0.46),
        'magazine':    (0.76, 0.26, 0.98, 0.48),
        'sight_frame': (0.76, 0.02, 0.98, 0.22),
        'sight_lens':  (0.82, 0.03, 0.94, 0.17)
    }

    for group_name, faces in group_faces.items():
        if not faces:
            continue
        u_min, v_min, u_max, v_max = group_bounds[group_name]

        # Calculate bounding box for the entire group
        all_verts = set(v for f in faces for v in f.verts)
        min_x = min(v.co.x for v in all_verts)
        max_x = max(v.co.x for v in all_verts)
        min_y = min(v.co.y for v in all_verts)
        max_y = max(v.co.y for v in all_verts)
        min_z = min(v.co.z for v in all_verts)
        max_z = max(v.co.z for v in all_verts)
        gx = max(max_x - min_x, 1e-4)
        gy = max(max_y - min_y, 1e-4)
        gz = max(max_z - min_z, 1e-4)

        for face in faces:
            fn = face.normal
            ax, ay, az = abs(fn.x), abs(fn.y), abs(fn.z)
            poly_uvs = []

            for loop in face.loops:
                co = loop.vert.co
                if ax >= ay and ax >= az:
                    # Side face: map Y (length) to U, Z (height) to V
                    pu = (co.y - min_y) / gy
                    pv = (co.z - min_z) / gz
                elif az >= ax and az >= ay:
                    # Top/Bottom face: map Y to U, X to V
                    pu = (co.y - min_y) / gy
                    pv = (co.x - min_x) / gx
                else:
                    # Front/Back face: map X to U, Z to V
                    pu = (co.x - min_x) / gx
                    pv = (co.z - min_z) / gz

                u_coord = u_min + pu * (u_max - u_min)
                v_coord = v_min + pv * (v_max - v_min)
                loop[uv_layer].uv = Vector((u_coord, v_coord))
                poly_uvs.append([round(u_coord, 4), round(v_coord, 4)])
            uv_polygons.append(poly_uvs)

    with open(UV_DATA_PATH, 'w') as f:
        json.dump(uv_polygons, f)

    mesh = bpy.data.meshes.new("PlayerRifle_Custom_Mesh")
    bm.to_mesh(mesh)
    bm.free()

    obj = bpy.data.objects.new("PlayerRifle_Custom", mesh)
    bpy.context.collection.objects.link(obj)
    return obj

def setup_material(obj):
    mat = bpy.data.materials.new(name="M_PlayerRifle_Custom")
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    nodes.clear()

    output = nodes.new(type='ShaderNodeOutputMaterial')
    output.location = (400, 0)
    bsdf = nodes.new(type='ShaderNodeBsdfPrincipled')
    bsdf.location = (0, 0)
    links.new(bsdf.outputs['BSDF'], output.inputs['Surface'])

    if os.path.exists(ALBEDO_TEX):
        tex_alb = nodes.new(type='ShaderNodeTexImage')
        tex_alb.image = bpy.data.images.load(ALBEDO_TEX)
        tex_alb.location = (-400, 200)
        links.new(tex_alb.outputs['Color'], bsdf.inputs['Base Color'])

    if os.path.exists(MET_TEX):
        tex_met = nodes.new(type='ShaderNodeTexImage')
        tex_met.image = bpy.data.images.load(MET_TEX)
        tex_met.image.colorspace_settings.name = 'Non-Color'
        tex_met.location = (-400, -100)
        links.new(tex_met.outputs['Color'], bsdf.inputs['Metallic'])
        inv = nodes.new(type='ShaderNodeInvert')
        inv.location = (-150, -100)
        links.new(tex_met.outputs['Alpha'], inv.inputs['Color'])
        links.new(inv.outputs['Color'], bsdf.inputs['Roughness'])

    if os.path.exists(NORMAL_TEX):
        tex_norm = nodes.new(type='ShaderNodeTexImage')
        tex_norm.image = bpy.data.images.load(NORMAL_TEX)
        tex_norm.image.colorspace_settings.name = 'Non-Color'
        tex_norm.location = (-400, -350)
        norm_node = nodes.new(type='ShaderNodeNormalMap')
        norm_node.location = (-150, -350)
        links.new(tex_norm.outputs['Color'], norm_node.inputs['Color'])
        links.new(norm_node.outputs['Normal'], bsdf.inputs['Normal'])

    if os.path.exists(EMISSION_TEX):
        tex_em = nodes.new(type='ShaderNodeTexImage')
        tex_em.image = bpy.data.images.load(EMISSION_TEX)
        tex_em.location = (-400, -600)
        links.new(tex_em.outputs['Color'], bsdf.inputs['Emission Color'])
        if 'Emission Strength' in bsdf.inputs:
            bsdf.inputs['Emission Strength'].default_value = 2.5

    obj.data.materials.append(mat)
    return mat

def create_muzzle_empty(parent_obj):
    empty = bpy.data.objects.new("MuzzlePoint", None)
    empty.empty_display_type = 'ARROWS'
    empty.empty_display_size = 0.05
    empty.location = (0.0, 0.52, 0.04)
    bpy.context.collection.objects.link(empty)
    empty.parent = parent_obj
    return empty

def render_showcase_screenshot():
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 720

    # Key light
    key_light_data = bpy.data.lights.new(name="KeyLight", type='AREA')
    key_light_data.energy = 120.0
    key_light_data.size = 1.2
    key_light = bpy.data.objects.new(name="KeyLight", object_data=key_light_data)
    key_light.location = (0.9, -0.4, 0.5)
    bpy.context.collection.objects.link(key_light)

    # Fill light
    fill_light_data = bpy.data.lights.new(name="FillLight", type='AREA')
    fill_light_data.energy = 50.0
    fill_light_data.size = 1.5
    fill_light = bpy.data.objects.new(name="FillLight", object_data=fill_light_data)
    fill_light.location = (-0.9, 0.3, 0.3)
    bpy.context.collection.objects.link(fill_light)

    # Rim light
    rim_light_data = bpy.data.lights.new(name="RimLight", type='POINT')
    rim_light_data.energy = 90.0
    rim_light = bpy.data.objects.new(name="RimLight", object_data=rim_light_data)
    rim_light.location = (0.1, 0.8, 0.4)
    bpy.context.collection.objects.link(rim_light)

    # Camera looking at 3/4 side profile:
    # Rifle runs along Y from -0.32 to +0.52.
    cam_data = bpy.data.cameras.new(name="RenderCamera")
    cam_data.lens = 38
    cam = bpy.data.objects.new(name="RenderCamera", object_data=cam_data)
    cam.location = (1.10, 0.08, 0.26)

    target = Vector((0.0, 0.08, -0.01))
    direction = target - cam.location
    rot_quat = direction.to_track_quat('-Z', 'Y')
    cam.rotation_euler = rot_quat.to_euler()
    bpy.context.collection.objects.link(cam)
    scene.camera = cam

    world = bpy.data.worlds.new("StudioWorld")
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs['Color'].default_value = (0.05, 0.07, 0.09, 1.0)
    scene.world = world

    scene.render.filepath = RENDER_PATH
    bpy.ops.render.render(write_still=True)
    print("Rendered showcase screenshot to", RENDER_PATH)

def export_assets(obj, muzzle_empty):
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    print("Saved Blender file to", BLEND_PATH)

    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    muzzle_empty.select_set(True)
    bpy.context.view_layer.objects.active = obj

    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)

    bpy.ops.export_scene.fbx(
        filepath=FBX_PATH,
        use_selection=True,
        axis_forward='-Z',
        axis_up='Y',
        apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_ALL',
        bake_space_transform=True,
        object_types={'MESH', 'EMPTY'},
        mesh_smooth_type='FACE'
    )
    print("Exported FBX to", FBX_PATH)

def main():
    clear_scene()
    rifle_obj = create_rifle()
    setup_material(rifle_obj)
    muzzle = create_muzzle_empty(rifle_obj)
    render_showcase_screenshot()
    export_assets(rifle_obj, muzzle)

if __name__ == '__main__':
    main()
