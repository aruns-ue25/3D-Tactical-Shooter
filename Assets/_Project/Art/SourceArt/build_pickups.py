import bpy
import bmesh
import math
import os
from mathutils import Vector, Matrix, Euler

MODELS_DIR = os.path.abspath(r"Assets/_Project/Art/Models")
SOURCE_ART_DIR = os.path.abspath(r"Assets/_Project/Art/SourceArt")
TEXTURE_DIR = os.path.abspath(r"Assets/_Project/Art/Textures")
os.makedirs(MODELS_DIR, exist_ok=True)
os.makedirs(SOURCE_ART_DIR, exist_ok=True)

def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)

def add_box_geometry(bm, center, size):
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
        bm.faces.new((v4, v3, v2, v1)), # -Z bottom
        bm.faces.new((v5, v6, v7, v8)), # +Z top
        bm.faces.new((v1, v2, v6, v5)), # -Y front
        bm.faces.new((v3, v4, v8, v7)), # +Y back
        bm.faces.new((v4, v1, v5, v8)), # -X left
        bm.faces.new((v2, v3, v7, v6)), # +X right
    ]
    return faces

def add_cylinder_geometry(bm, center, radius, length, segments=12, axis='Y'):
    verts1, verts2 = [], []
    half = length * 0.5
    for i in range(segments):
        ang = 2.0 * math.pi * i / segments
        ca = math.cos(ang) * radius
        sa = math.sin(ang) * radius
        if axis == 'Y':
            p1 = center + Vector((ca, -half, sa))
            p2 = center + Vector((ca,  half, sa))
        elif axis == 'X':
            p1 = center + Vector((-half, ca, sa))
            p2 = center + Vector(( half, ca, sa))
        else:
            p1 = center + Vector((ca, sa, -half))
            p2 = center + Vector((ca, sa,  half))
        verts1.append(bm.verts.new(p1))
        verts2.append(bm.verts.new(p2))

    faces = []
    for i in range(segments):
        next_i = (i + 1) % segments
        faces.append(bm.faces.new((verts1[i], verts1[next_i], verts2[next_i], verts2[i])))
    c1 = bm.verts.new(center + (Vector((0, -half, 0)) if axis == 'Y' else (Vector((-half, 0, 0)) if axis == 'X' else Vector((0, 0, -half)))))
    c2 = bm.verts.new(center + (Vector((0,  half, 0)) if axis == 'Y' else (Vector(( half, 0, 0)) if axis == 'X' else Vector((0, 0,  half)))))
    for i in range(segments):
        next_i = (i + 1) % segments
        faces.append(bm.faces.new((c1, verts1[next_i], verts1[i])))
        faces.append(bm.faces.new((c2, verts2[i], verts2[next_i])))
    return faces

def build_ammo_box():
    clear_scene()
    bm = bmesh.new()

    # 1. Main Steel Body (width 0.34, depth 0.18, height 0.22)
    body_faces = add_box_geometry(bm, Vector((0, 0, 0.11)), Vector((0.34, 0.18, 0.22)))

    # 2. Lid Rim & Cap (overhanging lid: width 0.355, depth 0.195, height 0.04)
    lid_faces = add_box_geometry(bm, Vector((0, 0, 0.235)), Vector((0.355, 0.195, 0.04)))

    # 3. Top Carrying Handle (flat folding bar)
    handle_faces = add_box_geometry(bm, Vector((0, 0, 0.26)), Vector((0.16, 0.024, 0.012)))
    # Handle brackets
    add_box_geometry(bm, Vector((-0.075, 0, 0.258)), Vector((0.02, 0.035, 0.012)))
    add_box_geometry(bm, Vector(( 0.075, 0, 0.258)), Vector((0.02, 0.035, 0.012)))

    # 4. Front Latch Clasps
    add_box_geometry(bm, Vector((0, -0.098, 0.16)), Vector((0.05, 0.018, 0.08)))
    add_cylinder_geometry(bm, Vector((0, -0.098, 0.20)), 0.008, 0.05, segments=8, axis='X')

    # 5. Side Stamped Ribs
    add_box_geometry(bm, Vector((0, 0.091, 0.14)), Vector((0.32, 0.008, 0.02)))
    add_box_geometry(bm, Vector((0, 0.091, 0.08)), Vector((0.32, 0.008, 0.02)))

    # Planar UV unwrapping
    uv_lay = bm.loops.layers.uv.new("UVMap")
    for face in bm.faces:
        norm = face.normal
        for loop in face.loops:
            pos = loop.vert.co
            if abs(norm.z) > 0.6: # Top/Bottom
                u = (pos.x + 0.2) / 0.4
                v = 0.5 + (pos.y + 0.15) / 0.3 * 0.5
            elif abs(norm.y) > 0.6: # Front/Back
                u = (pos.x + 0.2) / 0.4
                v = (pos.z) / 0.28 * 0.5
            else: # Sides
                u = (pos.y + 0.15) / 0.3 * 0.5
                v = (pos.z) / 0.28 * 0.5
            loop[uv_lay].uv = (u, v)

    mesh = bpy.data.meshes.new("AmmoBox_Custom_Mesh")
    bm.to_mesh(mesh)
    bm.free()

    obj = bpy.data.objects.new("AmmoBox_Custom", mesh)
    bpy.context.collection.objects.link(obj)

    # Material
    mat = bpy.data.materials.new(name="M_AmmoBox_Custom")
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs['Base Color'].default_value = (0.18, 0.22, 0.15, 1.0) # OD Green
        bsdf.inputs['Metallic'].default_value = 0.85
        bsdf.inputs['Roughness'].default_value = 0.35
    obj.data.materials.append(mat)

    # Export
    blend_path = os.path.join(SOURCE_ART_DIR, "AmmoBox_Custom.blend")
    fbx_path = os.path.join(MODELS_DIR, "AmmoBox_Custom.fbx")
    bpy.ops.wm.save_as_mainfile(filepath=blend_path)
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(
        filepath=fbx_path,
        use_selection=True,
        axis_forward='-Z',
        axis_up='Y',
        apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_ALL',
        bake_space_transform=True,
        object_types={'MESH'},
        mesh_smooth_type='FACE'
    )
    print("[OK] AmmoBox_Custom exported to:", fbx_path)

def build_medkit():
    clear_scene()
    bm = bmesh.new()

    # 1. Main Bottom Shell (width 0.40, depth 0.28, height 0.09)
    add_box_geometry(bm, Vector((0, 0, 0.045)), Vector((0.40, 0.28, 0.09)))

    # 2. Upper Lid Shell (width 0.40, depth 0.28, height 0.065)
    add_box_geometry(bm, Vector((0, 0, 0.12)), Vector((0.40, 0.28, 0.065)))

    # 3. 4 Rubberized Corner Shock Bumpers
    for bx in [-0.19, 0.19]:
        for by in [-0.13, 0.13]:
            add_box_geometry(bm, Vector((bx, by, 0.08)), Vector((0.038, 0.038, 0.16)))

    # 4. Raised Medical Cross on Lid (center: 0, 0.02, 0.156)
    # Vertical arm
    add_box_geometry(bm, Vector((0, 0.02, 0.156)), Vector((0.05, 0.14, 0.012)))
    # Horizontal arm
    add_box_geometry(bm, Vector((0, 0.02, 0.156)), Vector((0.14, 0.05, 0.012)))

    # 5. Dual Front Latches
    add_box_geometry(bm, Vector((-0.10, -0.144, 0.085)), Vector((0.04, 0.016, 0.05)))
    add_box_geometry(bm, Vector(( 0.10, -0.144, 0.085)), Vector((0.04, 0.016, 0.05)))

    # 6. Ergonomic Molded Front Handle
    add_box_geometry(bm, Vector((0, -0.165, 0.085)), Vector((0.15, 0.025, 0.025)))
    add_box_geometry(bm, Vector((-0.065, -0.15, 0.085)), Vector((0.025, 0.035, 0.025)))
    add_box_geometry(bm, Vector(( 0.065, -0.15, 0.085)), Vector((0.025, 0.035, 0.025)))

    # 7. Side Twin Stim-Injector Tubes (clipped on right side)
    add_cylinder_geometry(bm, Vector((0.21, -0.05, 0.085)), 0.016, 0.16, segments=12, axis='Y')
    add_cylinder_geometry(bm, Vector((0.21,  0.05, 0.085)), 0.016, 0.16, segments=12, axis='Y')

    # Planar UV unwrapping
    uv_lay = bm.loops.layers.uv.new("UVMap")
    for face in bm.faces:
        norm = face.normal
        for loop in face.loops:
            pos = loop.vert.co
            if abs(norm.z) > 0.6: # Top/Bottom
                u = (pos.x + 0.22) / 0.44
                v = (pos.y + 0.16) / 0.32
            elif abs(norm.y) > 0.6: # Front/Back
                u = (pos.x + 0.22) / 0.44
                v = (pos.z) / 0.2 * 0.4
            else: # Sides
                u = (pos.y + 0.16) / 0.32
                v = (pos.z) / 0.2 * 0.4
            loop[uv_lay].uv = (u, v)

    mesh = bpy.data.meshes.new("Medkit_Custom_Mesh")
    bm.to_mesh(mesh)
    bm.free()

    obj = bpy.data.objects.new("Medkit_Custom", mesh)
    bpy.context.collection.objects.link(obj)

    # Material
    mat = bpy.data.materials.new(name="M_Medkit_Custom")
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs['Base Color'].default_value = (0.88, 0.89, 0.90, 1.0) # Tactical White
        bsdf.inputs['Metallic'].default_value = 0.2
        bsdf.inputs['Roughness'].default_value = 0.25
    obj.data.materials.append(mat)

    # Export
    blend_path = os.path.join(SOURCE_ART_DIR, "Medkit_Custom.blend")
    fbx_path = os.path.join(MODELS_DIR, "Medkit_Custom.fbx")
    bpy.ops.wm.save_as_mainfile(filepath=blend_path)
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(
        filepath=fbx_path,
        use_selection=True,
        axis_forward='-Z',
        axis_up='Y',
        apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_ALL',
        bake_space_transform=True,
        object_types={'MESH'},
        mesh_smooth_type='FACE'
    )
    print("[OK] Medkit_Custom exported to:", fbx_path)

if __name__ == "__main__":
    build_ammo_box()
    build_medkit()
