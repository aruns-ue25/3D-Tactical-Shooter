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

def add_cylinder_geometry(bm, center, radius, length, segments=12, axis='Z'):
    verts1, verts2 = [], []
    half = length * 0.5
    for i in range(segments):
        ang = 2.0 * math.pi * i / segments
        ca = math.cos(ang) * radius
        sa = math.sin(ang) * radius
        if axis == 'Z':
            p1 = center + Vector((ca, sa, -half))
            p2 = center + Vector((ca, sa,  half))
        elif axis == 'Y':
            p1 = center + Vector((ca, -half, sa))
            p2 = center + Vector((ca,  half, sa))
        else:
            p1 = center + Vector((-half, ca, sa))
            p2 = center + Vector(( half, ca, sa))
        verts1.append(bm.verts.new(p1))
        verts2.append(bm.verts.new(p2))

    faces = []
    for i in range(segments):
        next_i = (i + 1) % segments
        faces.append(bm.faces.new((verts1[i], verts1[next_i], verts2[next_i], verts2[i])))
    c1 = bm.verts.new(center + (Vector((0, 0, -half)) if axis == 'Z' else (Vector((0, -half, 0)) if axis == 'Y' else Vector((-half, 0, 0)))))
    c2 = bm.verts.new(center + (Vector((0, 0,  half)) if axis == 'Z' else (Vector((0,  half, 0)) if axis == 'Y' else Vector(( half, 0, 0)))))
    for i in range(segments):
        next_i = (i + 1) % segments
        faces.append(bm.faces.new((c1, verts1[next_i], verts1[i])))
        faces.append(bm.faces.new((c2, verts2[i], verts2[next_i])))
    return faces

def build_gate():
    clear_scene()
    bm = bmesh.new()

    # Gate Dimensions
    width = 12.0
    height = 3.6
    depth = 0.3

    # Bottom Track Runner
    add_box_geometry(bm, Vector((0, 0, 0.12)), Vector((width, 0.35, 0.24)))

    # Top Structural Beam
    add_box_geometry(bm, Vector((0, 0, height - 0.15)), Vector((width, 0.35, 0.3)))

    # Left & Right Stile Columns
    add_box_geometry(bm, Vector((-width*0.5 + 0.15, 0, height*0.5)), Vector((0.3, 0.32, height)))
    add_box_geometry(bm, Vector(( width*0.5 - 0.15, 0, height*0.5)), Vector((0.3, 0.32, height)))

    # Mid Horizontal Rail
    add_box_geometry(bm, Vector((0, 0, height * 0.5)), Vector((width - 0.6, 0.25, 0.18)))

    # Vertical Anti-Climb Security Bars (spaced every 0.38m)
    num_bars = 28
    for i in range(num_bars):
        x = -width*0.5 + 0.5 + i * ((width - 1.0) / (num_bars - 1))
        add_cylinder_geometry(bm, Vector((x, 0, height*0.5)), radius=0.038, length=height - 0.5, segments=8, axis='Z')

    # Heavy Diagonal X-Trusses for Left & Right Gate Wings
    diag_pts = [
        (Vector((-5.7, 0.08, 0.3)), Vector((-0.2, 0.08, height - 0.3))),
        (Vector((-5.7, 0.08, height - 0.3)), Vector((-0.2, 0.08, 0.3))),
        (Vector(( 0.2, 0.08, 0.3)), Vector(( 5.7, 0.08, height - 0.3))),
        (Vector(( 0.2, 0.08, height - 0.3)), Vector(( 5.7, 0.08, 0.3))),
    ]
    for p_start, p_end in diag_pts:
        mid = (p_start + p_end) * 0.5
        v_diff = p_end - p_start
        length = v_diff.magnitude
        add_box_geometry(bm, mid, Vector((length, 0.06, 0.12)))

    # Center Reinforced Armor Plate (Hazard plate)
    add_box_geometry(bm, Vector((0, 0.05, height * 0.5)), Vector((3.4, 0.15, 1.8)))
    add_box_geometry(bm, Vector((0, -0.05, height * 0.5)), Vector((3.4, 0.15, 1.8)))

    # Overhead Drive Motor & Track Enclosure
    add_box_geometry(bm, Vector((0, 0, height + 0.2)), Vector((width + 0.4, 0.45, 0.35)))
    add_box_geometry(bm, Vector((0, 0.2, height + 0.2)), Vector((1.4, 0.4, 0.5))) # Motor gearbox

    # Red Perimeter Emergency Flashing Beacons
    add_cylinder_geometry(bm, Vector((-width*0.5 + 0.3, 0, height + 0.45)), radius=0.12, length=0.25, segments=12, axis='Z')
    add_cylinder_geometry(bm, Vector(( width*0.5 - 0.3, 0, height + 0.45)), radius=0.12, length=0.25, segments=12, axis='Z')

    # Security Biometric Access Column at right post
    add_box_geometry(bm, Vector((width*0.5 - 0.6, 0.35, 1.4)), Vector((0.25, 0.25, 1.1)))

    # UV Mapping
    uv_lay = bm.loops.layers.uv.new()
    for face in bm.faces:
        norm = face.normal
        for loop in face.loops:
            pos = loop.vert.co
            if abs(norm.y) > 0.5: # Front/Back
                u = (pos.x + width*0.5) / width
                v = pos.z / (height + 0.6)
            elif abs(norm.z) > 0.5: # Top/Bottom
                u = (pos.x + width*0.5) / width
                v = (pos.y + 0.5)
            else: # Sides
                u = (pos.y + 0.5)
                v = pos.z / (height + 0.6)
            loop[uv_lay].uv = (u, v)

    mesh = bpy.data.meshes.new("PerimeterSecurityGate_Custom_Mesh")
    bm.to_mesh(mesh)
    bm.free()

    obj = bpy.data.objects.new("PerimeterSecurityGate_Custom", mesh)
    bpy.context.collection.objects.link(obj)

    # Material
    mat = bpy.data.materials.new(name="M_PerimeterSecurityGate_Custom")
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs['Base Color'].default_value = (0.2, 0.22, 0.25, 1.0)
        bsdf.inputs['Metallic'].default_value = 0.85
        bsdf.inputs['Roughness'].default_value = 0.35
    obj.data.materials.append(mat)

    # Save Blend & Export FBX
    blend_path = os.path.join(SOURCE_ART_DIR, "PerimeterSecurityGate_Custom.blend")
    fbx_path = os.path.join(MODELS_DIR, "PerimeterSecurityGate_Custom.fbx")
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
    print("[OK] PerimeterSecurityGate_Custom exported to:", fbx_path)

if __name__ == "__main__":
    build_gate()
