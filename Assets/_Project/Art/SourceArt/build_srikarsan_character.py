import bpy
import bmesh
import math
import os
from mathutils import Vector, Euler, Matrix

# File paths
BASE_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "../../../.."))
ASSETS_DIR = os.path.join(BASE_DIR, "Assets", "_Project", "Art")
SOURCE_ART_DIR = os.path.join(ASSETS_DIR, "SourceArt")
MODELS_DIR = os.path.join(ASSETS_DIR, "Models")
TEXTURES_DIR = os.path.join(ASSETS_DIR, "Textures")

os.makedirs(MODELS_DIR, exist_ok=True)
os.makedirs(SOURCE_ART_DIR, exist_ok=True)

BLEND_PATH = os.path.join(SOURCE_ART_DIR, "Srikarsan_Character.blend")
FBX_PATH = os.path.join(MODELS_DIR, "Srikarsan_Character.fbx")
RENDER_PATH = os.path.join(SOURCE_ART_DIR, "Srikarsan_Character_Blender_Render.png")

# 1. Clean scene
bpy.ops.wm.read_factory_settings(use_empty=True)

# 2. Material Creation Helper
def create_pbr_material(name, albedo_file=None, normal_file=None, ms_file=None, base_color=(0.8, 0.8, 0.8, 1.0), roughness=0.5, metallic=0.0, specular=0.5):
    mat = bpy.data.materials.new(name=name)
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    nodes.clear()
    
    output = nodes.new(type="ShaderNodeOutputMaterial")
    output.location = (400, 0)
    
    bsdf = nodes.new(type="ShaderNodeBsdfPrincipled")
    bsdf.location = (0, 0)
    links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])
    
    bsdf.inputs["Base Color"].default_value = base_color
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    if "Specular IOR Level" in bsdf.inputs:
        bsdf.inputs["Specular IOR Level"].default_value = specular
    
    if albedo_file and os.path.exists(os.path.join(TEXTURES_DIR, albedo_file)):
        tex_node = nodes.new(type="ShaderNodeTexImage")
        tex_node.location = (-400, 200)
        tex_node.image = bpy.data.images.load(os.path.join(TEXTURES_DIR, albedo_file))
        links.new(tex_node.outputs["Color"], bsdf.inputs["Base Color"])
        
    if normal_file and os.path.exists(os.path.join(TEXTURES_DIR, normal_file)):
        norm_img = nodes.new(type="ShaderNodeTexImage")
        norm_img.location = (-400, -200)
        norm_img.image = bpy.data.images.load(os.path.join(TEXTURES_DIR, normal_file))
        norm_img.image.colorspace_settings.name = "Non-Color"
        
        norm_node = nodes.new(type="ShaderNodeNormalMap")
        norm_node.location = (-150, -200)
        norm_node.inputs["Strength"].default_value = 1.0
        links.new(norm_img.outputs["Color"], norm_node.inputs["Color"])
        links.new(norm_node.outputs["Normal"], bsdf.inputs["Normal"])
        
    return mat

# Setup Materials
mat_shirt = create_pbr_material("M_Srikarsan_Shirt", "T_Srikarsan_Shirt_Albedo.png", "T_Srikarsan_Shirt_Normal.png", base_color=(0.28, 0.36, 0.25, 1.0), roughness=0.65)
mat_face  = create_pbr_material("M_Srikarsan_Face", "T_Srikarsan_Face_Albedo.png", "T_Srikarsan_Face_Normal.png", base_color=(0.71, 0.54, 0.42, 1.0), roughness=0.45, specular=0.25)
mat_jeans = create_pbr_material("M_Srikarsan_Jeans", "T_Srikarsan_Jeans_Albedo.png", "T_Srikarsan_Jeans_Normal.png", base_color=(0.36, 0.45, 0.55, 1.0), roughness=0.65)
mat_skin  = create_pbr_material("M_Srikarsan_Skin", base_color=(0.71, 0.54, 0.42, 1.0), roughness=0.45, specular=0.25)
mat_hair  = create_pbr_material("M_Srikarsan_Hair", "T_Srikarsan_Hair_Albedo.png", "T_Srikarsan_Hair_Normal.png", base_color=(0.12, 0.12, 0.14, 1.0), roughness=0.6, specular=0.2)
mat_watch = create_pbr_material("M_Srikarsan_Watch", "T_Srikarsan_Watch_Albedo.png", "T_Srikarsan_Watch_Normal.png", base_color=(0.85, 0.85, 0.88, 1.0), metallic=0.9, roughness=0.15)
mat_belt  = create_pbr_material("M_Srikarsan_Belt", base_color=(0.24, 0.16, 0.10, 1.0), roughness=0.5)
mat_shoes = create_pbr_material("M_Srikarsan_Shoes", base_color=(0.14, 0.15, 0.16, 1.0), roughness=0.5)
mat_sole  = create_pbr_material("M_Srikarsan_Sole", base_color=(0.94, 0.94, 0.96, 1.0), roughness=0.35)
mat_gun   = create_pbr_material("M_Tactical_Gun", base_color=(0.12, 0.13, 0.14, 1.0), metallic=0.75, roughness=0.35)
mat_red_band = create_pbr_material("M_Red_Band", base_color=(0.85, 0.15, 0.12, 1.0), roughness=0.5)
mat_black_band = create_pbr_material("M_Black_Band", base_color=(0.08, 0.08, 0.09, 1.0), roughness=0.4)

# Geometry Construction Helpers
def create_mesh_object(name, parent=None, loc=(0,0,0), rot=(0,0,0)):
    mesh = bpy.data.meshes.new(name + "_Mesh")
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.location = loc
    obj.rotation_euler = rot
    if parent:
        obj.parent = parent
    return obj

def apply_smooth(obj):
    for f in obj.data.polygons:
        f.use_smooth = True

def create_cylinder(bm, radius, depth, segments=16):
    return bmesh.ops.create_cone(bm, cap_ends=True, radius1=radius, radius2=radius, depth=depth, segments=segments)

# ==============================================================================
# CHARACTER HIERARCHY
# ==============================================================================
root_obj = bpy.data.objects.new("CharacterBody", None)
bpy.context.collection.objects.link(root_obj)
root_obj.location = (0, 0, 0)

# 1. PELVIS & WAIST (Z: 0.84m)
pelvis_obj = create_mesh_object("Pelvis", root_obj, loc=(0, 0, 0.84))
bm = bmesh.new()
bmesh.ops.create_uvsphere(bm, u_segments=16, v_segments=10, radius=0.17)
bm.transform(Matrix.Scale(1.0, 4, (1, 0, 0)) @ Matrix.Scale(0.68, 4, (0, 1, 0)) @ Matrix.Scale(0.72, 4, (0, 0, 1)))
bm.to_mesh(pelvis_obj.data)
bm.free()
pelvis_obj.data.materials.append(mat_jeans)
apply_smooth(pelvis_obj)

belt_obj = create_mesh_object("Belt", pelvis_obj, loc=(0, 0, 0.06))
bm = bmesh.new()
create_cylinder(bm, radius=0.165, depth=0.042)
bm.transform(Matrix.Scale(1.02, 4, (1, 0, 0)) @ Matrix.Scale(0.70, 4, (0, 1, 0)) @ Matrix.Scale(1.0, 4, (0, 0, 1)))
bm.to_mesh(belt_obj.data)
bm.free()
belt_obj.data.materials.append(mat_belt)
apply_smooth(belt_obj)

buckle_obj = create_mesh_object("BeltBuckle", pelvis_obj, loc=(0, -0.118, 0.06))
bm = bmesh.new()
bmesh.ops.create_cube(bm, size=1.0)
bm.transform(Matrix.Scale(0.055, 4, (1, 0, 0)) @ Matrix.Scale(0.018, 4, (0, 1, 0)) @ Matrix.Scale(0.038, 4, (0, 0, 1)))
bm.to_mesh(buckle_obj.data)
bm.free()
buckle_obj.data.materials.append(mat_watch)

# 2. LEGS & SNEAKERS
def build_leg(side, x_offset):
    thigh = create_mesh_object(f"Thigh_{side}", pelvis_obj, loc=(x_offset, 0, -0.06))
    bm = bmesh.new()
    create_cylinder(bm, radius=0.076, depth=0.34)
    bm.transform(Matrix.Translation((0, 0, -0.17)))
    bm.to_mesh(thigh.data)
    bm.free()
    thigh.data.materials.append(mat_jeans)
    apply_smooth(thigh)
    
    shin = create_mesh_object(f"Shin_{side}", thigh, loc=(0, 0, -0.34))
    bm = bmesh.new()
    create_cylinder(bm, radius=0.068, depth=0.34)
    bm.transform(Matrix.Translation((0, 0, -0.17)))
    bm.to_mesh(shin.data)
    bm.free()
    shin.data.materials.append(mat_jeans)
    apply_smooth(shin)
    
    cuff = create_mesh_object(f"Cuff_{side}", shin, loc=(0, 0, -0.32))
    bm = bmesh.new()
    create_cylinder(bm, radius=0.072, depth=0.035)
    bm.to_mesh(cuff.data)
    bm.free()
    cuff.data.materials.append(mat_jeans)
    
    foot = create_mesh_object(f"Foot_{side}", shin, loc=(0, -0.04, -0.35))
    sole = create_mesh_object(f"Sole_{side}", foot, loc=(0, 0, -0.02))
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bm.transform(Matrix.Scale(0.10, 4, (1, 0, 0)) @ Matrix.Scale(0.24, 4, (0, 1, 0)) @ Matrix.Scale(0.035, 4, (0, 0, 1)))
    bm.to_mesh(sole.data)
    bm.free()
    sole.data.materials.append(mat_sole)
    
    upper = create_mesh_object(f"Upper_{side}", foot, loc=(0, 0, 0.02))
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bm.transform(Matrix.Scale(0.095, 4, (1, 0, 0)) @ Matrix.Scale(0.22, 4, (0, 1, 0)) @ Matrix.Scale(0.06, 4, (0, 0, 1)))
    bm.to_mesh(upper.data)
    bm.free()
    upper.data.materials.append(mat_shoes)
    
    toe = create_mesh_object(f"Toe_{side}", foot, loc=(0, -0.11, 0.0))
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=16, v_segments=8, radius=0.048)
    bm.transform(Matrix.Scale(1.0, 4, (1, 0, 0)) @ Matrix.Scale(0.65, 4, (0, 1, 0)) @ Matrix.Scale(0.55, 4, (0, 0, 1)))
    bm.to_mesh(toe.data)
    bm.free()
    toe.data.materials.append(mat_sole)
    apply_smooth(toe)
    
    return thigh

build_leg("L", -0.11)
build_leg("R", 0.11)

# 3. TORSO - OLIVE GREEN T-SHIRT WITH "TOKYO" (Z: 1.16m)
torso_obj = create_mesh_object("Torso", root_obj, loc=(0, 0, 1.16))
bm = bmesh.new()
create_cylinder(bm, radius=0.18, depth=0.42, segments=24)
for v in bm.verts:
    v.co.y *= 0.65
    if v.co.z > 0.05:
        v.co.y = v.co.y * 1.05 - 0.02
        v.co.x *= 1.08
    elif v.co.z < -0.08:
        v.co.x *= 0.90
        v.co.y *= 0.92

uv_layer = bm.loops.layers.uv.new()
for face in bm.faces:
    for loop in face.loops:
        co = loop.vert.co
        u = (co.x / 0.36) * 0.48 + 0.50
        v = (co.z / 0.36) * 0.40 + 0.58
        loop[uv_layer].uv = (u, v)

bm.to_mesh(torso_obj.data)
bm.free()
torso_obj.data.materials.append(mat_shirt)
apply_smooth(torso_obj)

# Crewneck collar
collar_obj = create_mesh_object("CrewneckCollar", torso_obj, loc=(0, 0, 0.22))
bm = bmesh.new()
create_cylinder(bm, radius=0.088, depth=0.032, segments=20)
bm.transform(Matrix.Scale(1.0, 4, (1, 0, 0)) @ Matrix.Scale(0.85, 4, (0, 1, 0)) @ Matrix.Scale(1.0, 4, (0, 0, 1)))
bm.to_mesh(collar_obj.data)
bm.free()
collar_obj.data.materials.append(mat_shirt)
apply_smooth(collar_obj)

# 4. HEAD, FACE & HAIR (SRIKARSAN)
head_obj = create_mesh_object("Head", torso_obj, loc=(0, 0, 0.38))

# Neck
neck_obj = create_mesh_object("Neck", head_obj, loc=(0, 0, -0.06))
bm = bmesh.new()
create_cylinder(bm, radius=0.062, depth=0.10, segments=16)
bm.to_mesh(neck_obj.data)
bm.free()
neck_obj.data.materials.append(mat_skin)
apply_smooth(neck_obj)

# Head Base Mesh with Srikarsan's real photograph mapped onto the front
head_mesh_obj = create_mesh_object("HeadMesh", head_obj, loc=(0, 0, 0.08))
bm = bmesh.new()
bmesh.ops.create_uvsphere(bm, u_segments=24, v_segments=16, radius=0.12)
for v in bm.verts:
    v.co.y *= 1.10
    v.co.z *= 1.20
    if v.co.z < 0 and v.co.y < 0:
        v.co.x *= 0.88
        v.co.y -= 0.015

uv_layer = bm.loops.layers.uv.new()
for face in bm.faces:
    for loop in face.loops:
        co = loop.vert.co
        u = (co.x / 0.12) * 0.36 + 0.50
        v = (co.z / 0.14) * 0.41 + 0.53
        loop[uv_layer].uv = (u, v)

bm.to_mesh(head_mesh_obj.data)
bm.free()
head_mesh_obj.data.materials.append(mat_face)
apply_smooth(head_mesh_obj)

# Srikarsan's Pompadour / Quiff Hair (Sculpted crown cap)
hair_obj = create_mesh_object("HairMesh", head_obj, loc=(0, 0.012, 0.10))
bm = bmesh.new()
bmesh.ops.create_uvsphere(bm, u_segments=20, v_segments=14, radius=0.122)
for v in bm.verts:
    if v.co.z > 0:
        v.co.z *= 1.18
        v.co.y = v.co.y * 1.06 - 0.02
    else:
        v.co.z *= 0.65
        v.co.x *= 1.02
bm.to_mesh(hair_obj.data)
bm.free()
hair_obj.data.materials.append(mat_hair)
apply_smooth(hair_obj)

# 5. ARMS & ACCESSORIES
# Left Arm (with Wristwatch)
arm_l = create_mesh_object("UpperArm_L", torso_obj, loc=(-0.21, 0, 0.16), rot=(-0.15, 0, -0.15))
sleeve_l = create_mesh_object("Sleeve_L", arm_l, loc=(0, 0, -0.07))
bm = bmesh.new()
create_cylinder(bm, radius=0.072, depth=0.16)
bm.to_mesh(sleeve_l.data)
bm.free()
sleeve_l.data.materials.append(mat_shirt)
apply_smooth(sleeve_l)

bicep_l = create_mesh_object("Bicep_L", arm_l, loc=(0, 0, -0.16))
bm = bmesh.new()
create_cylinder(bm, radius=0.058, depth=0.12)
bm.to_mesh(bicep_l.data)
bm.free()
bicep_l.data.materials.append(mat_skin)
apply_smooth(bicep_l)

forearm_l = create_mesh_object("Forearm_L", arm_l, loc=(0, 0, -0.24), rot=(-0.40, 0, 0.10))
bm = bmesh.new()
create_cylinder(bm, radius=0.052, depth=0.22)
bm.transform(Matrix.Translation((0, 0, -0.11)))
bm.to_mesh(forearm_l.data)
bm.free()
forearm_l.data.materials.append(mat_skin)
apply_smooth(forearm_l)

# Srikarsan's Wristwatch (Leather Strap + Silver Case + White Dial)
watch_strap = create_mesh_object("WatchStrap", forearm_l, loc=(0, 0, -0.18))
bm = bmesh.new()
create_cylinder(bm, radius=0.058, depth=0.035)
bm.to_mesh(watch_strap.data)
bm.free()
watch_strap.data.materials.append(mat_belt)

watch_case = create_mesh_object("WatchCase", forearm_l, loc=(-0.055, 0, -0.18), rot=(0, 1.57, 0))
bm = bmesh.new()
create_cylinder(bm, radius=0.024, depth=0.012)
bm.to_mesh(watch_case.data)
bm.free()
watch_case.data.materials.append(mat_watch)

watch_dial = create_mesh_object("WatchDial", forearm_l, loc=(-0.062, 0, -0.18), rot=(0, 1.57, 0))
bm = bmesh.new()
create_cylinder(bm, radius=0.020, depth=0.005)
bm.to_mesh(watch_dial.data)
bm.free()
watch_dial.data.materials.append(mat_sole)

hand_l = create_mesh_object("Hand_L", forearm_l, loc=(0, 0, -0.24))
bm = bmesh.new()
bmesh.ops.create_uvsphere(bm, u_segments=12, v_segments=8, radius=0.048)
bm.transform(Matrix.Scale(0.8, 4, (1, 0, 0)) @ Matrix.Scale(1.1, 4, (0, 1, 0)) @ Matrix.Scale(0.9, 4, (0, 0, 1)))
bm.to_mesh(hand_l.data)
bm.free()
hand_l.data.materials.append(mat_skin)
apply_smooth(hand_l)

# Right Arm (with Wristband and Tactical Weapon in Low-Ready Pose)
arm_r = create_mesh_object("UpperArm_R", torso_obj, loc=(0.21, 0, 0.16), rot=(-0.15, -0.05, 0.12))
sleeve_r = create_mesh_object("Sleeve_R", arm_r, loc=(0, 0, -0.07))
bm = bmesh.new()
create_cylinder(bm, radius=0.072, depth=0.16)
bm.to_mesh(sleeve_r.data)
bm.free()
sleeve_r.data.materials.append(mat_shirt)
apply_smooth(sleeve_r)

bicep_r = create_mesh_object("Bicep_R", arm_r, loc=(0, 0, -0.16))
bm = bmesh.new()
create_cylinder(bm, radius=0.058, depth=0.12)
bm.to_mesh(bicep_r.data)
bm.free()
bicep_r.data.materials.append(mat_skin)
apply_smooth(bicep_r)

forearm_r = create_mesh_object("Forearm_R", arm_r, loc=(0, 0, -0.24), rot=(-0.35, 0, -0.10))
bm = bmesh.new()
create_cylinder(bm, radius=0.052, depth=0.22)
bm.transform(Matrix.Translation((0, 0, -0.11)))
bm.to_mesh(forearm_r.data)
bm.free()
forearm_r.data.materials.append(mat_skin)
apply_smooth(forearm_r)

# Red thread band & black wristband
red_band = create_mesh_object("RedBand", forearm_r, loc=(0, 0, -0.17))
bm = bmesh.new()
create_cylinder(bm, radius=0.056, depth=0.015)
bm.to_mesh(red_band.data)
bm.free()
red_band.data.materials.append(mat_red_band)

black_band = create_mesh_object("BlackBand", forearm_r, loc=(0, 0, -0.19))
bm = bmesh.new()
create_cylinder(bm, radius=0.058, depth=0.02)
bm.to_mesh(black_band.data)
bm.free()
black_band.data.materials.append(mat_black_band)

# Hand Right
hand_r = create_mesh_object("Hand_R", forearm_r, loc=(0, 0, -0.24), rot=(0.2, 0, 0))
bm = bmesh.new()
bmesh.ops.create_uvsphere(bm, u_segments=12, v_segments=8, radius=0.048)
bm.to_mesh(hand_r.data)
bm.free()
hand_r.data.materials.append(mat_skin)
apply_smooth(hand_r)

# Tactical Assault Rifle (low-ready stance pointing down and forward)
rifle = create_mesh_object("ThirdPersonRifle", hand_r, loc=(0.04, -0.10, -0.18), rot=(math.radians(35), math.radians(-15), math.radians(-30)))
bm = bmesh.new()
bmesh.ops.create_cube(bm, size=1.0)
bm.transform(Matrix.Scale(0.042, 4, (1, 0, 0)) @ Matrix.Scale(0.30, 4, (0, 1, 0)) @ Matrix.Scale(0.075, 4, (0, 0, 1)))
bmesh.ops.create_cone(bm, cap_ends=True, radius1=0.012, radius2=0.012, depth=0.22, segments=12)
for v in bm.verts[-26:]:
    v.co = Matrix.Rotation(1.57, 4, 'X') @ Matrix.Translation((0, -0.25, 0.01)) @ v.co
bmesh.ops.create_cube(bm, size=1.0)
for v in bm.verts[-8:]:
    v.co = Matrix.Scale(0.032, 4, (1, 0, 0)) @ Matrix.Scale(0.055, 4, (0, 1, 0)) @ Matrix.Scale(0.11, 4, (0, 0, 1)) @ Matrix.Translation((0, -0.03, -0.08)) @ v.co
bm.to_mesh(rifle.data)
bm.free()
rifle.data.materials.append(mat_gun)
apply_smooth(rifle)

muzzle = bpy.data.objects.new("ThirdPersonMuzzlePoint", None)
bpy.context.collection.objects.link(muzzle)
muzzle.parent = rifle
muzzle.location = (0, -0.36, 0.01)

# ==============================================================================
# SAVE BLEND & EXPORT FBX
# ==============================================================================
bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
print(f"Saved Blender project to: {BLEND_PATH}")

bpy.ops.export_scene.fbx(
    filepath=FBX_PATH,
    use_selection=False,
    global_scale=1.0,
    apply_unit_scale=True,
    axis_forward='-Z',
    axis_up='Y',
    bake_space_transform=True,
    object_types={'MESH', 'EMPTY'}
)
print(f"Exported FBX model to: {FBX_PATH}")

# ==============================================================================
# STUDIO LIGHTING & BEAUTY RENDER (Srikarsan Portrait)
# ==============================================================================
cam_data = bpy.data.cameras.new("RenderCam")
cam_data.lens = 75
cam_obj = bpy.data.objects.new("RenderCam", cam_data)
bpy.context.collection.objects.link(cam_obj)
cam_obj.location = (0.05, -2.2, 1.42)
cam_obj.rotation_euler = (math.radians(91), 0, math.radians(1.5))
bpy.context.scene.camera = cam_obj

key_data = bpy.data.lights.new("KeyLight", type='AREA')
key_data.energy = 320
key_data.size = 1.6
key_obj = bpy.data.objects.new("KeyLight", key_data)
bpy.context.collection.objects.link(key_obj)
key_obj.location = (1.2, -2.0, 2.0)
key_obj.rotation_euler = (math.radians(45), 0, math.radians(30))

fill_data = bpy.data.lights.new("FillLight", type='AREA')
fill_data.energy = 140
fill_data.size = 2.2
fill_obj = bpy.data.objects.new("FillLight", fill_data)
bpy.context.collection.objects.link(fill_obj)
fill_obj.location = (-1.4, -1.8, 1.4)

rim_data = bpy.data.lights.new("RimLight", type='SPOT')
rim_data.energy = 280
rim_obj = bpy.data.objects.new("RimLight", rim_data)
bpy.context.collection.objects.link(rim_obj)
rim_obj.location = (-0.5, 1.4, 2.0)
rim_obj.rotation_euler = (math.radians(-50), 0, math.radians(160))

scene = bpy.context.scene
scene.render.resolution_x = 1080
scene.render.resolution_y = 1440
scene.render.image_settings.file_format = 'PNG'
scene.render.filepath = RENDER_PATH

bpy.ops.render.render(write_still=True)
print(f"Beauty render saved to: {RENDER_PATH}")
