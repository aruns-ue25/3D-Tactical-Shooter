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

BLEND_PATH = os.path.join(SOURCE_ART_DIR, "ObjectiveTerminal_Custom.blend")
FBX_PATH = os.path.join(MODELS_DIR, "ObjectiveTerminal_Custom.fbx")
RENDER_PATH = os.path.join(SOURCE_ART_DIR, "ObjectiveTerminal_Blender_Render.png")
UV_DATA_PATH = os.path.join(SOURCE_ART_DIR, "ObjectiveTerminal_uv_data.json")

ALBEDO_TEX = os.path.join(TEXTURE_DIR, "T_ObjectiveTerminal_Albedo.png")
NORMAL_TEX = os.path.join(TEXTURE_DIR, "T_ObjectiveTerminal_Normal.png")
MET_TEX = os.path.join(TEXTURE_DIR, "T_ObjectiveTerminal_MetallicSmoothness.png")
EMISSION_TEX = os.path.join(TEXTURE_DIR, "T_ObjectiveTerminal_Emission.png")

def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)

def create_terminal():
    bm = bmesh.new()

    group_faces = {
        'screen': [],
        'controls': [],
        'base': [],
        'chassis': [],
        'beacon': [],
        'cables': []
    }

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
            bm.faces.new((v4, v3, v2, v1)), # -Y front
            bm.faces.new((v5, v6, v7, v8)), # +Y back
            bm.faces.new((v1, v2, v6, v5)), # -Z bottom
            bm.faces.new((v3, v4, v8, v7)), # +Z top
            bm.faces.new((v1, v5, v8, v4)), # -X left
            bm.faces.new((v2, v3, v7, v6)), # +X right
        ]
        for f in faces:
            f.smooth = False
            group_faces[group_name].append(f)
        return faces

    def add_cylinder(center, radius, length, segments=12, axis='Z', group_name='chassis'):
        verts_ring1 = []
        verts_ring2 = []
        half_len = length * 0.5
        for i in range(segments):
            angle = 2.0 * math.pi * i / segments
            cos_a = math.cos(angle) * radius
            sin_a = math.sin(angle) * radius
            if axis == 'Z':
                p1 = center + Vector((cos_a, sin_a, -half_len))
                p2 = center + Vector((cos_a, sin_a,  half_len))
            elif axis == 'Y':
                p1 = center + Vector((cos_a, -half_len, sin_a))
                p2 = center + Vector((cos_a,  half_len, sin_a))
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

    # --- 1. BASE PEDESTAL (Z from 0.0 to 0.16) ---
    # Floor mount plate
    add_box(Vector((0.0, 0.0, 0.04)), Vector((1.10, 0.90, 0.08)), 'base')
    # Upper pedestal tier
    add_box(Vector((0.0, 0.0, 0.12)), Vector((0.95, 0.78, 0.08)), 'base')
    # 4 Corner anchor bolts
    for bx in [-0.46, 0.46]:
        for by in [-0.36, 0.36]:
            add_cylinder(Vector((bx, by, 0.10)), radius=0.035, length=0.05, segments=8, axis='Z', group_name='base')

    # --- 2. MAIN CHASSIS / PEDESTAL COLUMN (Z from 0.16 to 0.92) ---
    add_box(Vector((0.0, 0.0, 0.54)), Vector((0.76, 0.62, 0.76)), 'chassis')
    # Side ventilation louver blocks
    add_box(Vector((-0.39, 0.0, 0.52)), Vector((0.03, 0.45, 0.50)), 'chassis') # left vents
    add_box(Vector(( 0.39, 0.0, 0.52)), Vector((0.03, 0.45, 0.50)), 'chassis') # right vents
    # Mid-height support bracket trim
    add_box(Vector((0.0, -0.32, 0.45)), Vector((0.72, 0.04, 0.08)), 'chassis')
    add_box(Vector((0.0,  0.32, 0.45)), Vector((0.72, 0.04, 0.08)), 'chassis')

    # --- 3. ANGLED WORKSTATION CONTROL DECK (Z from 0.90 to 1.08, slanted towards -Y) ---
    # Deck housing
    add_box(Vector((0.0, -0.16, 0.96)), Vector((0.78, 0.42, 0.10)), 'controls')
    # Slanted keyboard face
    add_box(Vector((0.0, -0.20, 1.02)), Vector((0.55, 0.24, 0.03)), 'controls')
    # Keypad rows (raised button grid)
    for kx in range(-4, 5):
        for ky in range(-2, 3):
            add_box(Vector((kx * 0.05, -0.22 + ky * 0.04, 1.045)), Vector((0.038, 0.03, 0.015)), 'controls')
    # Command Trackball housing
    add_box(Vector((0.28, -0.20, 1.035)), Vector((0.14, 0.16, 0.03)), 'controls')
    add_cylinder(Vector((0.28, -0.20, 1.055)), radius=0.045, length=0.02, segments=12, axis='Z', group_name='controls')
    # Keycard reader slot
    add_box(Vector((-0.28, -0.20, 1.035)), Vector((0.12, 0.18, 0.03)), 'controls')
    add_box(Vector((-0.28, -0.20, 1.055)), Vector((0.09, 0.02, 0.01)), 'controls')

    # --- 4. UPPER CONSOLE HOUSING & MAIN DISPLAY SCREEN (Z from 1.05 to 1.52) ---
    # Upper console back housing
    add_box(Vector((0.0, 0.05, 1.28)), Vector((0.84, 0.32, 0.48)), 'chassis')
    # Main screen frame
    add_box(Vector((0.0, -0.11, 1.28)), Vector((0.78, 0.04, 0.44)), 'chassis')
    # Primary LCD Tactical Screen (faces -Y towards player approach)
    add_box(Vector((0.0, -0.132, 1.28)), Vector((0.72, 0.008, 0.38)), 'screen')
    # Left & Right Auxiliary Sub-Screens
    add_box(Vector((-0.38, -0.06, 1.28)), Vector((0.015, 0.18, 0.34)), 'chassis')
    add_box(Vector(( 0.38, -0.06, 1.28)), Vector((0.015, 0.18, 0.34)), 'chassis')

    # --- 5. TOP STATUS BEACON LIGHT BAR (Z from 1.52 to 1.62) ---
    # Beacon mount bracket
    add_box(Vector((0.0, 0.04, 1.54)), Vector((0.60, 0.18, 0.04)), 'chassis')
    # High-intensity indicator light bar
    add_box(Vector((0.0, 0.02, 1.58)), Vector((0.52, 0.12, 0.05)), 'beacon')
    # Top protective guard bars
    add_box(Vector((-0.27, 0.02, 1.58)), Vector((0.02, 0.14, 0.07)), 'chassis')
    add_box(Vector(( 0.27, 0.02, 1.58)), Vector((0.02, 0.14, 0.07)), 'chassis')

    # --- 6. REINFORCED CABLE CONDUITS (Rear down to base) ---
    for cx in [-0.22, 0.0, 0.22]:
        add_cylinder(Vector((cx, 0.22, 0.70)), radius=0.035, length=1.05, segments=10, axis='Z', group_name='cables')
        # Floor conduit junction box
        add_box(Vector((cx, 0.24, 0.12)), Vector((0.10, 0.10, 0.12)), 'base')

    # --- UV UNWRAPPING PER GROUP ---
    uv_layer = bm.loops.layers.uv.new("UVMap")
    uv_polygons = []

    group_bounds = {
        'screen':   (0.02, 0.52, 0.58, 0.98), # Main cyber telemetry screen
        'controls': (0.62, 0.52, 0.98, 0.98), # Keypad & workstation deck
        'base':     (0.02, 0.02, 0.48, 0.48), # Pedestal with hazard stripes
        'chassis':  (0.52, 0.22, 0.82, 0.48), # Metal frame & server louvers (avoids beacon at bottom)
        'beacon':   (0.54, 0.04, 0.74, 0.16), # Top glowing beacon
        'cables':   (0.77, 0.02, 0.82, 0.48)  # Ribbed conduit cables
    }

    for group_name, faces in group_faces.items():
        if not faces:
            continue
        u_min, v_min, u_max, v_max = group_bounds[group_name]

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
                if ay >= ax and ay >= az:
                    # Front/Back face (Screen faces -Y!): map X to U, Z to V
                    pu = (co.x - min_x) / gx
                    pv = (co.z - min_z) / gz
                elif ax >= ay and ax >= az:
                    # Side face: map Y to U, Z to V
                    pu = (co.y - min_y) / gy
                    pv = (co.z - min_z) / gz
                else:
                    # Top/Bottom face: map X to U, Y to V
                    pu = (co.x - min_x) / gx
                    pv = (co.y - min_y) / gy

                u_coord = u_min + pu * (u_max - u_min)
                v_coord = v_min + pv * (v_max - v_min)
                loop[uv_layer].uv = Vector((u_coord, v_coord))
                poly_uvs.append([round(u_coord, 4), round(v_coord, 4)])
            uv_polygons.append(poly_uvs)

    with open(UV_DATA_PATH, 'w') as f:
        json.dump(uv_polygons, f)

    mesh = bpy.data.meshes.new("ObjectiveTerminal_Custom_Mesh")
    bm.to_mesh(mesh)
    bm.free()

    obj = bpy.data.objects.new("ObjectiveTerminal_Custom", mesh)
    bpy.context.collection.objects.link(obj)
    return obj

def setup_material(obj):
    mat = bpy.data.materials.new(name="M_ObjectiveTerminal_Custom")
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
            bsdf.inputs['Emission Strength'].default_value = 3.0

    obj.data.materials.append(mat)
    return mat

def render_showcase_screenshot():
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 720

    # Key light
    key_light_data = bpy.data.lights.new(name="KeyLight", type='AREA')
    key_light_data.energy = 250.0
    key_light_data.size = 2.0
    key_light = bpy.data.objects.new(name="KeyLight", object_data=key_light_data)
    key_light.location = (1.8, -2.2, 2.2)
    bpy.context.collection.objects.link(key_light)

    # Fill light
    fill_light_data = bpy.data.lights.new(name="FillLight", type='AREA')
    fill_light_data.energy = 100.0
    fill_light_data.size = 2.5
    fill_light = bpy.data.objects.new(name="FillLight", object_data=fill_light_data)
    fill_light.location = (-2.0, -1.8, 1.5)
    bpy.context.collection.objects.link(fill_light)

    # Rim light from behind
    rim_light_data = bpy.data.lights.new(name="RimLight", type='POINT')
    rim_light_data.energy = 180.0
    rim_light = bpy.data.objects.new(name="RimLight", object_data=rim_light_data)
    rim_light.location = (0.2, 2.0, 2.2)
    bpy.context.collection.objects.link(rim_light)

    # Camera looking at 3/4 front view:
    # Front is -Y, screen is at Z ~ 1.28
    cam_data = bpy.data.cameras.new(name="RenderCamera")
    cam_data.lens = 36
    cam = bpy.data.objects.new(name="RenderCamera", object_data=cam_data)
    cam.location = (1.85, -2.75, 1.45)

    target = Vector((0.0, -0.05, 0.78))
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

def export_assets(obj):
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    print("Saved Blender file to", BLEND_PATH)

    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj

    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)

    bpy.ops.export_scene.fbx(
        filepath=FBX_PATH,
        use_selection=True,
        axis_forward='-Z',
        axis_up='Y',
        apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_ALL',
        bake_space_transform=False,
        object_types={'MESH'},
        mesh_smooth_type='FACE'
    )
    print("Exported FBX to", FBX_PATH)

def main():
    clear_scene()
    terminal_obj = create_terminal()
    setup_material(terminal_obj)
    render_showcase_screenshot()
    export_assets(terminal_obj)

if __name__ == '__main__':
    main()
