import bpy
import bmesh
import math
import os
from mathutils import Vector, Euler, Matrix

BASE_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "../../../.."))
ASSETS_DIR = os.path.join(BASE_DIR, "Assets", "_Project", "Art")
SOURCE_ART_DIR = os.path.join(ASSETS_DIR, "SourceArt")
ENEMIES_MODELS_DIR = os.path.join(ASSETS_DIR, "Models", "Enemies")

os.makedirs(ENEMIES_MODELS_DIR, exist_ok=True)
os.makedirs(SOURCE_ART_DIR, exist_ok=True)

def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)

def create_pbr_material(name, base_color=(0.8, 0.8, 0.8, 1.0), metallic=0.0, roughness=0.5, emission_color=(0,0,0,1), emission_strength=0.0):
    mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
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
    
    if "Emission Color" in bsdf.inputs:
        bsdf.inputs["Emission Color"].default_value = emission_color
        if "Emission Strength" in bsdf.inputs:
            bsdf.inputs["Emission Strength"].default_value = emission_strength
    elif "Emission" in bsdf.inputs:
        bsdf.inputs["Emission"].default_value = emission_color
        
    return mat

def create_mesh_part(name, primitive_type, loc=(0,0,0), scale=(1,1,1), rot=(0,0,0), material=None, parent=None):
    mesh = bpy.data.meshes.new(name + "_Mesh")
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    
    bm = bmesh.new()
    if primitive_type == 'cube':
        bmesh.ops.create_cube(bm, size=1.0)
    elif primitive_type == 'cylinder':
        bmesh.ops.create_cone(bm, cap_ends=True, radius1=0.5, radius2=0.5, depth=1.0, segments=24)
    elif primitive_type == 'sphere':
        bmesh.ops.create_uvsphere(bm, u_segments=24, v_segments=16, radius=0.5)
    elif primitive_type == 'cone':
        bmesh.ops.create_cone(bm, cap_ends=True, radius1=0.5, radius2=0.0, depth=1.0, segments=24)
    
    bm.to_mesh(mesh)
    bm.free()
    
    obj.location = loc
    obj.scale = scale
    obj.rotation_euler = rot
    
    if material:
        obj.data.materials.append(material)
        
    for p in obj.data.polygons:
        p.use_smooth = True
        
    if parent:
        obj.parent = parent
        
    return obj

def setup_studio_lighting_and_camera(cam_pos=(0, -3.2, 1.4), cam_target=(0, 0, 1.1), render_path="render.png"):
    # Camera
    cam_data = bpy.data.cameras.new("RenderCam")
    cam_data.lens = 65
    cam_obj = bpy.data.objects.new("RenderCam", cam_data)
    bpy.context.collection.objects.link(cam_obj)
    cam_obj.location = cam_pos
    
    direction = Vector(cam_target) - Vector(cam_pos)
    rot_quat = direction.to_track_quat('-Z', 'Y')
    cam_obj.rotation_euler = rot_quat.to_euler()
    bpy.context.scene.camera = cam_obj
    
    # Key Light
    key_data = bpy.data.lights.new("KeyLight", type='AREA')
    key_data.energy = 450
    key_data.size = 2.0
    key_obj = bpy.data.objects.new("KeyLight", key_data)
    bpy.context.collection.objects.link(key_obj)
    key_obj.location = (1.8, -2.5, 2.8)
    key_obj.rotation_euler = (math.radians(45), 0, math.radians(35))
    
    # Fill Light
    fill_data = bpy.data.lights.new("FillLight", type='AREA')
    fill_data.energy = 180
    fill_data.size = 2.5
    fill_obj = bpy.data.objects.new("FillLight", fill_data)
    bpy.context.collection.objects.link(fill_obj)
    fill_obj.location = (-2.0, -2.2, 1.6)
    
    # Rim Light
    rim_data = bpy.data.lights.new("RimLight", type='SPOT')
    rim_data.energy = 380
    rim_obj = bpy.data.objects.new("RimLight", rim_data)
    bpy.context.collection.objects.link(rim_obj)
    rim_obj.location = (-0.8, 1.8, 2.6)
    rim_obj.rotation_euler = (math.radians(-50), 0, math.radians(150))
    
    scene = bpy.context.scene
    scene.render.resolution_x = 1080
    scene.render.resolution_y = 1440
    scene.render.image_settings.file_format = 'PNG'
    scene.render.filepath = render_path
    bpy.ops.render.render(write_still=True)
    print(f"Rendered: {render_path}")

# ==============================================================================
# 1. BUILD BOSS: IRONCLAD JUGGERNAUT
# ==============================================================================
def build_boss_juggernaut():
    clear_scene()
    
    # Materials
    mat_armor = create_pbr_material("M_Boss_ObsidianArmor", base_color=(0.14, 0.14, 0.17, 1.0), metallic=0.95, roughness=0.28)
    mat_armor_dark = create_pbr_material("M_Boss_BlackSteel", base_color=(0.08, 0.08, 0.10, 1.0), metallic=0.90, roughness=0.35)
    mat_suit = create_pbr_material("M_Boss_BloodRedFabric", base_color=(0.58, 0.05, 0.08, 1.0), metallic=0.05, roughness=0.65)
    mat_gold = create_pbr_material("M_Boss_ImperialGold", base_color=(0.95, 0.80, 0.22, 1.0), metallic=0.92, roughness=0.22)
    mat_visor = create_pbr_material("M_Boss_CrimsonVisor", base_color=(1.0, 0.05, 0.05, 1.0), roughness=0.1, emission_color=(1.0, 0.08, 0.08, 1.0), emission_strength=5.0)
    mat_gun = create_pbr_material("M_Boss_HeavyWeapon", base_color=(0.11, 0.11, 0.13, 1.0), metallic=0.85, roughness=0.3)

    root = bpy.data.objects.new("Boss_Root", None)
    bpy.context.collection.objects.link(root)
    root.scale = (1.28, 1.28, 1.28)

    torso = create_mesh_part("Torso_Suit", "cube", loc=(0, 0, 1.25), scale=(0.60, 0.45, 0.65), material=mat_suit, parent=root)
    # Heavy Dual Chest Slabs
    create_mesh_part("Chest_Slab_L", "cube", loc=(-0.16, 0.22, 1.35), scale=(0.26, 0.12, 0.36), material=mat_armor, parent=root)
    create_mesh_part("Chest_Slab_R", "cube", loc=(0.16, 0.22, 1.35), scale=(0.26, 0.12, 0.36), material=mat_armor, parent=root)
    create_mesh_part("Chest_Core", "cylinder", loc=(0, 0.25, 1.35), scale=(0.14, 0.14, 0.08), rot=(math.radians(90), 0, 0), material=mat_gold, parent=root)
    create_mesh_part("Back_Armor", "cube", loc=(0, -0.22, 1.30), scale=(0.55, 0.14, 0.50), material=mat_armor_dark, parent=root)
    
    # Heavy Neck Blast Collar
    create_mesh_part("Blast_Collar", "cylinder", loc=(0, 0.02, 1.62), scale=(0.38, 0.38, 0.18), material=mat_armor, parent=root)
    create_mesh_part("Collar_Trim", "cylinder", loc=(0, 0.02, 1.70), scale=(0.40, 0.40, 0.04), material=mat_gold, parent=root)

    # Head & Helmet with Crimson Visor
    create_mesh_part("Helmet_Base", "uvsphere", loc=(0, 0.02, 1.78), scale=(0.32, 0.34, 0.34), material=mat_armor, parent=root)
    create_mesh_part("Crimson_Visor_Slit", "cube", loc=(0, 0.18, 1.78), scale=(0.26, 0.04, 0.045), material=mat_visor, parent=root)
    create_mesh_part("Helmet_Crown", "cube", loc=(0, 0.02, 1.96), scale=(0.04, 0.30, 0.08), material=mat_gold, parent=root)

    # Massive Pauldrons
    create_mesh_part("Pauldron_L", "sphere", loc=(-0.42, 0, 1.50), scale=(0.32, 0.28, 0.26), material=mat_armor, parent=root)
    create_mesh_part("Pauldron_Trim_L", "cylinder", loc=(-0.42, 0, 1.52), scale=(0.34, 0.30, 0.06), material=mat_gold, parent=root)
    create_mesh_part("Pauldron_R", "sphere", loc=(0.42, 0, 1.50), scale=(0.32, 0.28, 0.26), material=mat_armor, parent=root)
    create_mesh_part("Pauldron_Trim_R", "cylinder", loc=(0.42, 0, 1.52), scale=(0.34, 0.30, 0.06), material=mat_gold, parent=root)

    # Heavy Arms & Vambraces
    create_mesh_part("Arm_L", "cylinder", loc=(-0.36, 0, 1.20), scale=(0.14, 0.14, 0.38), material=mat_suit, parent=root)
    create_mesh_part("Vambrace_L", "cylinder", loc=(-0.36, 0.04, 0.95), scale=(0.16, 0.16, 0.28), material=mat_armor, parent=root)
    create_mesh_part("Arm_R", "cylinder", loc=(0.36, 0.12, 1.20), scale=(0.14, 0.14, 0.38), rot=(math.radians(35), 0, 0), material=mat_suit, parent=root)
    create_mesh_part("Vambrace_R", "cylinder", loc=(0.36, 0.28, 0.98), scale=(0.16, 0.16, 0.28), rot=(math.radians(35), 0, 0), material=mat_armor, parent=root)

    # Heavy Legs with Hydraulic Struts
    create_mesh_part("Thigh_L", "cylinder", loc=(-0.18, 0, 0.72), scale=(0.18, 0.18, 0.44), material=mat_suit, parent=root)
    create_mesh_part("Knee_Shield_L", "cube", loc=(-0.18, 0.10, 0.50), scale=(0.16, 0.10, 0.16), material=mat_armor, parent=root)
    create_mesh_part("Shin_L", "cylinder", loc=(-0.18, 0, 0.28), scale=(0.17, 0.17, 0.42), material=mat_armor, parent=root)
    create_mesh_part("Hydraulic_Piston_L", "cylinder", loc=(-0.26, -0.05, 0.50), scale=(0.04, 0.04, 0.40), material=mat_gold, parent=root)

    create_mesh_part("Thigh_R", "cylinder", loc=(0.18, 0, 0.72), scale=(0.18, 0.18, 0.44), material=mat_suit, parent=root)
    create_mesh_part("Knee_Shield_R", "cube", loc=(0.18, 0.10, 0.50), scale=(0.16, 0.10, 0.16), material=mat_armor, parent=root)
    create_mesh_part("Shin_R", "cylinder", loc=(0.18, 0, 0.28), scale=(0.17, 0.17, 0.42), material=mat_armor, parent=root)
    create_mesh_part("Hydraulic_Piston_R", "cylinder", loc=(0.26, -0.05, 0.50), scale=(0.04, 0.04, 0.40), material=mat_gold, parent=root)

    # Heavy Squad Support Weapon (Twin Drum SAW)
    gun = create_mesh_part("Boss_HeavySAW", "cube", loc=(0.25, 0.35, 1.05), scale=(0.12, 0.65, 0.18), material=mat_gun, parent=root)
    create_mesh_part("SAW_Barrel", "cylinder", loc=(0.25, 0.85, 1.08), scale=(0.05, 0.05, 0.40), rot=(math.radians(90), 0, 0), material=mat_armor, parent=root)
    create_mesh_part("SAW_Drum_L", "cylinder", loc=(0.16, 0.35, 0.95), scale=(0.12, 0.12, 0.16), rot=(0, math.radians(90), 0), material=mat_armor_dark, parent=root)
    create_mesh_part("SAW_Drum_R", "cylinder", loc=(0.34, 0.35, 0.95), scale=(0.12, 0.12, 0.16), rot=(0, math.radians(90), 0), material=mat_armor_dark, parent=root)

    # Save & Export
    fbx_out = os.path.join(ENEMIES_MODELS_DIR, "Boss_Ironclad_Juggernaut.fbx")
    render_out = os.path.join(SOURCE_ART_DIR, "Boss_Ironclad_Juggernaut_Render.png")
    bpy.ops.export_scene.fbx(filepath=fbx_out, use_selection=False, global_scale=1.0, axis_forward='-Z', axis_up='Y')
    setup_studio_lighting_and_camera(cam_pos=(0, -3.8, 1.6), cam_target=(0, 0, 1.3), render_path=render_out)
    print(f"Exported Boss to {fbx_out}")

# ==============================================================================
# 2. BUILD GUARD: FRONTLINE SENTINEL
# ==============================================================================
def build_guard_sentinel():
    clear_scene()
    
    mat_armor = create_pbr_material("M_Guard_SteelCeramic", base_color=(0.58, 0.62, 0.68, 1.0), metallic=0.82, roughness=0.35)
    mat_armor_dark = create_pbr_material("M_Guard_DarkSteel", base_color=(0.32, 0.35, 0.40, 1.0), metallic=0.75, roughness=0.45)
    mat_suit = create_pbr_material("M_Guard_RoyalNavy", base_color=(0.12, 0.22, 0.48, 1.0), metallic=0.05, roughness=0.75)
    mat_pouches = create_pbr_material("M_Guard_Webbing", base_color=(0.18, 0.20, 0.22, 1.0), metallic=0.0, roughness=0.8)
    mat_nvg_lens = create_pbr_material("M_Guard_NVG_Lens", base_color=(0.1, 0.8, 0.4, 1.0), roughness=0.1, emission_color=(0.1, 0.85, 0.4, 1.0), emission_strength=3.0)
    mat_gun = create_pbr_material("M_Guard_Carbine", base_color=(0.12, 0.13, 0.15, 1.0), metallic=0.85, roughness=0.32)

    root = bpy.data.objects.new("Guard_Root", None)
    bpy.context.collection.objects.link(root)
    root.scale = (1.0, 1.0, 1.0)

    torso = create_mesh_part("Torso_Navy", "cube", loc=(0, 0, 1.15), scale=(0.48, 0.34, 0.55), material=mat_suit, parent=root)
    create_mesh_part("Chest_PlateCarrier", "cube", loc=(0, 0.08, 1.22), scale=(0.44, 0.20, 0.38), material=mat_armor, parent=root)
    create_mesh_part("Back_PlateCarrier", "cube", loc=(0, -0.08, 1.22), scale=(0.42, 0.16, 0.38), material=mat_armor_dark, parent=root)
    
    # Triple Mag Pouches on Front
    create_mesh_part("Mag_Pouch_1", "cube", loc=(-0.11, 0.19, 1.14), scale=(0.08, 0.07, 0.15), material=mat_pouches, parent=root)
    create_mesh_part("Mag_Pouch_2", "cube", loc=(0, 0.19, 1.14), scale=(0.08, 0.07, 0.15), material=mat_pouches, parent=root)
    create_mesh_part("Mag_Pouch_3", "cube", loc=(0.11, 0.19, 1.14), scale=(0.08, 0.07, 0.15), material=mat_pouches, parent=root)
    # Tactical Radio Antenna
    create_mesh_part("Radio_Body", "cube", loc=(-0.16, -0.16, 1.30), scale=(0.08, 0.06, 0.14), material=mat_pouches, parent=root)
    create_mesh_part("Radio_Antenna", "cylinder", loc=(-0.16, -0.16, 1.54), scale=(0.012, 0.012, 0.35), material=mat_armor_dark, parent=root)

    # Head & Combat Helmet with Quad NVG
    create_mesh_part("Head_Base", "uvsphere", loc=(0, 0, 1.58), scale=(0.24, 0.26, 0.26), material=mat_suit, parent=root)
    create_mesh_part("Combat_Helmet", "uvsphere", loc=(0, 0.01, 1.64), scale=(0.27, 0.29, 0.27), material=mat_armor, parent=root)
    # Quad NVG Tube Mount
    create_mesh_part("NVG_Mount", "cube", loc=(0, 0.16, 1.66), scale=(0.18, 0.06, 0.05), material=mat_armor_dark, parent=root)
    create_mesh_part("NVG_Tube_1", "cylinder", loc=(-0.09, 0.21, 1.66), scale=(0.03, 0.03, 0.08), rot=(math.radians(90), 0, 0), material=mat_nvg_lens, parent=root)
    create_mesh_part("NVG_Tube_2", "cylinder", loc=(-0.03, 0.21, 1.66), scale=(0.03, 0.03, 0.08), rot=(math.radians(90), 0, 0), material=mat_nvg_lens, parent=root)
    create_mesh_part("NVG_Tube_3", "cylinder", loc=(0.03, 0.21, 1.66), scale=(0.03, 0.03, 0.08), rot=(math.radians(90), 0, 0), material=mat_nvg_lens, parent=root)
    create_mesh_part("NVG_Tube_4", "cylinder", loc=(0.09, 0.21, 1.66), scale=(0.03, 0.03, 0.08), rot=(math.radians(90), 0, 0), material=mat_nvg_lens, parent=root)

    # Shoulders, Arms & Legs
    create_mesh_part("Pauldron_L", "sphere", loc=(-0.30, 0, 1.34), scale=(0.18, 0.16, 0.18), material=mat_armor, parent=root)
    create_mesh_part("Pauldron_R", "sphere", loc=(0.30, 0, 1.34), scale=(0.18, 0.16, 0.18), material=mat_armor, parent=root)
    create_mesh_part("Arm_L", "cylinder", loc=(-0.28, 0, 1.08), scale=(0.11, 0.11, 0.35), material=mat_suit, parent=root)
    create_mesh_part("Arm_R", "cylinder", loc=(0.28, 0.12, 1.08), scale=(0.11, 0.11, 0.35), rot=(math.radians(30), 0, 0), material=mat_suit, parent=root)
    create_mesh_part("Leg_L", "cylinder", loc=(-0.14, 0, 0.48), scale=(0.13, 0.13, 0.65), material=mat_suit, parent=root)
    create_mesh_part("Knee_L", "cube", loc=(-0.14, 0.07, 0.48), scale=(0.11, 0.06, 0.12), material=mat_armor, parent=root)
    create_mesh_part("Leg_R", "cylinder", loc=(0.14, 0, 0.48), scale=(0.13, 0.13, 0.65), material=mat_suit, parent=root)
    create_mesh_part("Knee_R", "cube", loc=(0.14, 0.07, 0.48), scale=(0.11, 0.06, 0.12), material=mat_armor, parent=root)

    # Tactical Assault Carbine with Holo Sight
    gun = create_mesh_part("Guard_Carbine", "cube", loc=(0.20, 0.32, 1.02), scale=(0.06, 0.45, 0.12), material=mat_gun, parent=root)
    create_mesh_part("Carbine_Barrel", "cylinder", loc=(0.20, 0.68, 1.04), scale=(0.025, 0.025, 0.30), rot=(math.radians(90), 0, 0), material=mat_armor_dark, parent=root)
    create_mesh_part("Holo_Optic", "cube", loc=(0.20, 0.32, 1.13), scale=(0.04, 0.09, 0.06), material=mat_armor_dark, parent=root)

    fbx_out = os.path.join(ENEMIES_MODELS_DIR, "Guard_Frontline_Sentinel.fbx")
    render_out = os.path.join(SOURCE_ART_DIR, "Guard_Frontline_Sentinel_Render.png")
    bpy.ops.export_scene.fbx(filepath=fbx_out, use_selection=False, global_scale=1.0, axis_forward='-Z', axis_up='Y')
    setup_studio_lighting_and_camera(cam_pos=(0, -3.2, 1.4), cam_target=(0, 0, 1.1), render_path=render_out)
    print(f"Exported Guard to {fbx_out}")

# ==============================================================================
# 3. BUILD HUNTER: APEX STALKER
# ==============================================================================
def build_hunter_stalker():
    clear_scene()
    
    mat_carbon = create_pbr_material("M_Hunter_Carbon", base_color=(0.18, 0.19, 0.22, 1.0), metallic=0.88, roughness=0.35)
    mat_orange = create_pbr_material("M_Hunter_HazardOrange", base_color=(0.92, 0.38, 0.06, 1.0), metallic=0.08, roughness=0.55)
    mat_dark_suit = create_pbr_material("M_Hunter_UnderSuit", base_color=(0.10, 0.11, 0.12, 1.0), metallic=0.0, roughness=0.8)
    mat_amber_visor = create_pbr_material("M_Hunter_AmberVisor", base_color=(1.0, 0.65, 0.10, 1.0), roughness=0.1, emission_color=(1.0, 0.65, 0.10, 1.0), emission_strength=4.5)
    mat_blade_steel = create_pbr_material("M_Hunter_BladeSteel", base_color=(0.75, 0.78, 0.82, 1.0), metallic=0.95, roughness=0.15)
    mat_gun = create_pbr_material("M_Hunter_CQB_SMG", base_color=(0.14, 0.14, 0.16, 1.0), metallic=0.85, roughness=0.3)

    root = bpy.data.objects.new("Hunter_Root", None)
    bpy.context.collection.objects.link(root)
    root.scale = (1.05, 1.05, 1.05)

    torso = create_mesh_part("Torso_Suit", "cube", loc=(0, 0, 1.18), scale=(0.46, 0.32, 0.58), material=mat_dark_suit, parent=root)
    create_mesh_part("Chest_CarbonRig", "cube", loc=(0, 0.08, 1.24), scale=(0.42, 0.18, 0.36), material=mat_carbon, parent=root)
    # Hazard Tiger Stripes
    create_mesh_part("Hazard_Stripe_L", "cube", loc=(-0.14, 0.18, 1.26), scale=(0.04, 0.02, 0.24), material=mat_orange, parent=root)
    create_mesh_part("Hazard_Stripe_R", "cube", loc=(0.14, 0.18, 1.26), scale=(0.04, 0.02, 0.24), material=mat_orange, parent=root)

    # Dual Combat Karambit Knives on Chest
    create_mesh_part("Knife_Sheath_L", "cube", loc=(-0.12, 0.19, 1.35), scale=(0.03, 0.04, 0.16), rot=(0, 0, math.radians(-35)), material=mat_carbon, parent=root)
    create_mesh_part("Knife_Blade_L", "cube", loc=(-0.13, 0.20, 1.44), scale=(0.015, 0.02, 0.08), rot=(0, 0, math.radians(-35)), material=mat_blade_steel, parent=root)
    create_mesh_part("Knife_Sheath_R", "cube", loc=(0.12, 0.19, 1.35), scale=(0.03, 0.04, 0.16), rot=(0, 0, math.radians(35)), material=mat_carbon, parent=root)
    create_mesh_part("Knife_Blade_R", "cube", loc=(0.13, 0.20, 1.44), scale=(0.015, 0.02, 0.08), rot=(0, 0, math.radians(35)), material=mat_blade_steel, parent=root)

    # Predator Stalker Mask & Glowing Amber Visor
    create_mesh_part("Head_Base", "uvsphere", loc=(0, 0, 1.62), scale=(0.22, 0.25, 0.25), material=mat_dark_suit, parent=root)
    create_mesh_part("Predator_Mask", "cube", loc=(0, 0.06, 1.62), scale=(0.24, 0.20, 0.26), material=mat_carbon, parent=root)
    create_mesh_part("Amber_Visor_Curved", "cylinder", loc=(0, 0.16, 1.65), scale=(0.16, 0.06, 0.07), rot=(math.radians(90), 0, 0), material=mat_amber_visor, parent=root)
    create_mesh_part("Respirator_Filter_L", "cylinder", loc=(-0.08, 0.14, 1.53), scale=(0.04, 0.04, 0.06), rot=(math.radians(45), 0, 0), material=mat_orange, parent=root)
    create_mesh_part("Respirator_Filter_R", "cylinder", loc=(0.08, 0.14, 1.53), scale=(0.04, 0.04, 0.06), rot=(math.radians(45), 0, 0), material=mat_orange, parent=root)

    # Shoulders & Limbs
    create_mesh_part("Pauldron_L", "sphere", loc=(-0.28, 0, 1.36), scale=(0.16, 0.14, 0.16), material=mat_carbon, parent=root)
    create_mesh_part("Pauldron_Orange_L", "cylinder", loc=(-0.28, 0, 1.38), scale=(0.17, 0.15, 0.03), material=mat_orange, parent=root)
    create_mesh_part("Pauldron_R", "sphere", loc=(0.28, 0, 1.36), scale=(0.16, 0.14, 0.16), material=mat_carbon, parent=root)
    create_mesh_part("Pauldron_Orange_R", "cylinder", loc=(0.28, 0, 1.38), scale=(0.17, 0.15, 0.03), material=mat_orange, parent=root)

    create_mesh_part("Arm_L", "cylinder", loc=(-0.26, 0, 1.10), scale=(0.10, 0.10, 0.36), material=mat_dark_suit, parent=root)
    create_mesh_part("Vambrace_L", "cylinder", loc=(-0.26, 0.02, 0.90), scale=(0.11, 0.11, 0.22), material=mat_carbon, parent=root)
    create_mesh_part("Arm_R", "cylinder", loc=(0.26, 0.10, 1.10), scale=(0.10, 0.10, 0.36), rot=(math.radians(35), 0, 0), material=mat_dark_suit, parent=root)
    create_mesh_part("Vambrace_R", "cylinder", loc=(0.26, 0.22, 0.92), scale=(0.11, 0.11, 0.22), rot=(math.radians(35), 0, 0), material=mat_carbon, parent=root)

    create_mesh_part("Leg_L", "cylinder", loc=(-0.13, 0, 0.50), scale=(0.12, 0.12, 0.68), material=mat_dark_suit, parent=root)
    create_mesh_part("Knee_L", "cube", loc=(-0.13, 0.06, 0.50), scale=(0.10, 0.06, 0.12), material=mat_orange, parent=root)
    create_mesh_part("Leg_R", "cylinder", loc=(0.13, 0, 0.50), scale=(0.12, 0.12, 0.68), material=mat_dark_suit, parent=root)
    create_mesh_part("Knee_R", "cube", loc=(0.13, 0.06, 0.50), scale=(0.10, 0.06, 0.12), material=mat_orange, parent=root)

    # Compact CQB SMG
    gun = create_mesh_part("Hunter_SMG", "cube", loc=(0.18, 0.28, 1.04), scale=(0.05, 0.32, 0.10), material=mat_gun, parent=root)
    create_mesh_part("SMG_Barrel", "cylinder", loc=(0.18, 0.52, 1.05), scale=(0.022, 0.022, 0.18), rot=(math.radians(90), 0, 0), material=mat_carbon, parent=root)
    create_mesh_part("SMG_Mag", "cube", loc=(0.18, 0.26, 0.94), scale=(0.03, 0.06, 0.14), rot=(math.radians(15), 0, 0), material=mat_carbon, parent=root)

    fbx_out = os.path.join(ENEMIES_MODELS_DIR, "Hunter_Apex_Stalker.fbx")
    render_out = os.path.join(SOURCE_ART_DIR, "Hunter_Apex_Stalker_Render.png")
    bpy.ops.export_scene.fbx(filepath=fbx_out, use_selection=False, global_scale=1.0, axis_forward='-Z', axis_up='Y')
    setup_studio_lighting_and_camera(cam_pos=(0, -3.2, 1.4), cam_target=(0, 0, 1.1), render_path=render_out)
    print(f"Exported Hunter to {fbx_out}")

# ==============================================================================
# 4. BUILD FLANKER: GHOST MARKSMAN
# ==============================================================================
def build_flanker_marksman():
    clear_scene()
    
    mat_stealth_black = create_pbr_material("M_Flanker_CarbonBlack", base_color=(0.10, 0.11, 0.13, 1.0), metallic=0.35, roughness=0.65)
    mat_midnight_navy = create_pbr_material("M_Flanker_MidnightNavy", base_color=(0.05, 0.12, 0.22, 1.0), metallic=0.10, roughness=0.75)
    mat_cyan_optics = create_pbr_material("M_Flanker_CyanOptic", base_color=(0.15, 0.85, 1.0, 1.0), roughness=0.08, emission_color=(0.15, 0.85, 1.0, 1.0), emission_strength=5.0)
    mat_sniper_steel = create_pbr_material("M_Flanker_DMRSteel", base_color=(0.14, 0.15, 0.18, 1.0), metallic=0.92, roughness=0.25)

    root = bpy.data.objects.new("Flanker_Root", None)
    bpy.context.collection.objects.link(root)
    root.scale = (0.96, 0.96, 0.96)

    torso = create_mesh_part("Torso_Stealth", "cube", loc=(0, 0, 1.12), scale=(0.42, 0.28, 0.54), material=mat_midnight_navy, parent=root)
    create_mesh_part("Stealth_ChestHarness", "cube", loc=(0, 0.06, 1.18), scale=(0.38, 0.16, 0.34), material=mat_stealth_black, parent=root)

    # Sleek Recon Helmet with Multi-Lens Cyan Optics
    create_mesh_part("Head_Base", "uvsphere", loc=(0, 0, 1.54), scale=(0.22, 0.24, 0.24), material=mat_midnight_navy, parent=root)
    create_mesh_part("Recon_Helmet", "uvsphere", loc=(0, 0.01, 1.58), scale=(0.24, 0.26, 0.25), material=mat_stealth_black, parent=root)
    # Tri-Sensor Cyan Optic Lenses
    create_mesh_part("Cyan_Optic_Main", "cylinder", loc=(0, 0.16, 1.60), scale=(0.04, 0.04, 0.06), rot=(math.radians(90), 0, 0), material=mat_cyan_optics, parent=root)
    create_mesh_part("Cyan_Optic_L", "cylinder", loc=(-0.07, 0.15, 1.63), scale=(0.025, 0.025, 0.05), rot=(math.radians(90), 0, 0), material=mat_cyan_optics, parent=root)
    create_mesh_part("Cyan_Optic_R", "cylinder", loc=(0.07, 0.15, 1.63), scale=(0.025, 0.025, 0.05), rot=(math.radians(90), 0, 0), material=mat_cyan_optics, parent=root)

    # Low-drag sleek limbs
    create_mesh_part("Pauldron_L", "sphere", loc=(-0.25, 0, 1.30), scale=(0.14, 0.12, 0.14), material=mat_stealth_black, parent=root)
    create_mesh_part("Pauldron_R", "sphere", loc=(0.25, 0, 1.30), scale=(0.14, 0.12, 0.14), material=mat_stealth_black, parent=root)
    create_mesh_part("Arm_L", "cylinder", loc=(-0.24, 0, 1.05), scale=(0.09, 0.09, 0.34), material=mat_midnight_navy, parent=root)
    create_mesh_part("Arm_R", "cylinder", loc=(0.24, 0.12, 1.05), scale=(0.09, 0.09, 0.34), rot=(math.radians(35), 0, 0), material=mat_midnight_navy, parent=root)
    create_mesh_part("Leg_L", "cylinder", loc=(-0.12, 0, 0.46), scale=(0.11, 0.11, 0.62), material=mat_midnight_navy, parent=root)
    create_mesh_part("Leg_R", "cylinder", loc=(0.12, 0, 0.46), scale=(0.11, 0.11, 0.62), material=mat_midnight_navy, parent=root)

    # Suppressed Long-Barrel DMR Sniper Rifle with High-Power Optic & Bipod
    gun = create_mesh_part("Flanker_DMR", "cube", loc=(0.18, 0.30, 0.98), scale=(0.05, 0.58, 0.12), material=mat_sniper_steel, parent=root)
    create_mesh_part("DMR_SuppressedBarrel", "cylinder", loc=(0.18, 0.88, 1.02), scale=(0.035, 0.035, 0.60), rot=(math.radians(90), 0, 0), material=mat_stealth_black, parent=root)
    create_mesh_part("Sniper_Scope", "cylinder", loc=(0.18, 0.30, 1.12), scale=(0.032, 0.032, 0.28), rot=(math.radians(90), 0, 0), material=mat_stealth_black, parent=root)
    create_mesh_part("Scope_FrontLens", "cylinder", loc=(0.18, 0.44, 1.12), scale=(0.028, 0.028, 0.02), rot=(math.radians(90), 0, 0), material=mat_cyan_optics, parent=root)
    create_mesh_part("Bipod_Leg_L", "cylinder", loc=(0.14, 0.68, 0.90), scale=(0.012, 0.012, 0.18), rot=(0, 0, math.radians(20)), material=mat_stealth_black, parent=root)
    create_mesh_part("Bipod_Leg_R", "cylinder", loc=(0.22, 0.68, 0.90), scale=(0.012, 0.012, 0.18), rot=(0, 0, math.radians(-20)), material=mat_stealth_black, parent=root)

    fbx_out = os.path.join(ENEMIES_MODELS_DIR, "Flanker_Ghost_Marksman.fbx")
    render_out = os.path.join(SOURCE_ART_DIR, "Flanker_Ghost_Marksman_Render.png")
    bpy.ops.export_scene.fbx(filepath=fbx_out, use_selection=False, global_scale=1.0, axis_forward='-Z', axis_up='Y')
    setup_studio_lighting_and_camera(cam_pos=(0, -3.0, 1.3), cam_target=(0, 0, 1.05), render_path=render_out)
    print(f"Exported Flanker to {fbx_out}")

# ==============================================================================
# MASTER EXECUTION: GENERATE ALL 4 ARCHETYPES
# ==============================================================================
if __name__ == "__main__":
    print(">>> 1/4 Generating Boss: Ironclad Juggernaut...")
    build_boss_juggernaut()
    
    print(">>> 2/4 Generating Guard: Frontline Sentinel...")
    build_guard_sentinel()
    
    print(">>> 3/4 Generating Hunter: Apex Stalker...")
    build_hunter_stalker()
    
    print(">>> 4/4 Generating Flanker: Ghost Marksman...")
    build_flanker_marksman()
    
    print(">>> ALL 4 ENEMY ARCHETYPES GENERATED AND EXPORTED SUCCESSFULLY!")
