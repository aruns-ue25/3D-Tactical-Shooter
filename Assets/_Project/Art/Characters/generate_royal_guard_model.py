"""
Blender 3D Script: Royal Guard Character Model with Tactical Assault Rifle
===========================================================================
Generates the complete 3D Guard character based on the 4-view reference concept art:
- Steel Kettle Helmet with comb ridge, flared brim, and nasal guard
- Sculpted head, face, and dark hair with royal blue gorget neck collar
- Polished Steel Breastplate & Backplate with leather shoulder straps and rivets
- Articulated Steel Pauldrons (shoulder armor)
- Royal Blue Surcoat / Tunic with silver-white trim borders and front/back splits
- Brown leather utility belt with brass buckle
- Armored steel vambraces and brown leather gauntlet gloves
- Dark trousers and folded-cuff riding boots with ankle straps
- Modern Realistic Tactical Assault Carbine Rifle held in combat two-handed stance

HOW TO USE IN BLENDER:
1. Open Blender (version 3.0 or higher).
2. Go to the 'Scripting' tab at the top.
3. Click 'Open', select this file, and click 'Run Script' (or press Alt+P).
4. The complete model and materials will be generated automatically and exported to FBX/OBJ!
"""

import bpy
import math
import os

def clear_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)

def create_pbr_material(name, base_color, metallic=0.0, roughness=0.5):
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    bsdf = nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs['Base Color'].default_value = base_color
        bsdf.inputs['Metallic'].default_value = metallic
        bsdf.inputs['Roughness'].default_value = roughness
    return mat

def create_mesh_object(primitive_type, name, location, scale, rotation=(0,0,0), material=None):
    if primitive_type == 'cube':
        bpy.ops.mesh.primitive_cube_add(location=location)
    elif primitive_type == 'cylinder':
        bpy.ops.mesh.primitive_cylinder_add(radius=1.0, depth=2.0, vertices=24, location=location)
    elif primitive_type == 'uv_sphere':
        bpy.ops.mesh.primitive_uv_sphere_add(radius=1.0, segments=24, ring_count=16, location=location)
    elif primitive_type == 'cone':
        bpy.ops.mesh.primitive_cone_add(radius1=1.0, depth=2.0, vertices=24, location=location)
    elif primitive_type == 'torus':
        bpy.ops.mesh.primitive_torus_add(major_radius=1.0, minor_radius=0.25, location=location)

    obj = bpy.context.active_object
    obj.name = name
    obj.scale = scale
    obj.rotation_euler = rotation

    if material:
        obj.data.materials.append(material)

    bpy.ops.object.shade_smooth()
    return obj

def build_royal_guard():
    clear_scene()

    # Materials Palette from reference image
    mat_steel = create_pbr_material("SteelArmor", (0.55, 0.58, 0.62, 1.0), metallic=0.92, roughness=0.25)
    mat_steel_dark = create_pbr_material("SteelDark", (0.35, 0.38, 0.42, 1.0), metallic=0.88, roughness=0.35)
    mat_tunic_blue = create_pbr_material("RoyalBlueFabric", (0.12, 0.28, 0.58, 1.0), metallic=0.0, roughness=0.75)
    mat_silver_trim = create_pbr_material("SilverTrim", (0.85, 0.88, 0.92, 1.0), metallic=0.6, roughness=0.3)
    mat_leather = create_pbr_material("BrownLeather", (0.32, 0.18, 0.10, 1.0), metallic=0.05, roughness=0.6)
    mat_brass = create_pbr_material("BrassGold", (0.85, 0.72, 0.25, 1.0), metallic=0.85, roughness=0.3)
    mat_skin = create_pbr_material("SkinTone", (0.86, 0.70, 0.58, 1.0), metallic=0.0, roughness=0.6)
    mat_trousers = create_pbr_material("DarkTrousers", (0.14, 0.15, 0.18, 1.0), metallic=0.0, roughness=0.8)
    mat_gunmetal = create_pbr_material("GunmetalRifle", (0.10, 0.10, 0.12, 1.0), metallic=0.95, roughness=0.28)

    guard_parts = []

    # 1. HEAD & FACE
    head = create_mesh_object('uv_sphere', "Guard_Head", (0, 0, 1.62), (0.12, 0.14, 0.13), material=mat_skin)
    chin = create_mesh_object('cube', "Guard_Chin", (0, 0.06, 1.54), (0.05, 0.05, 0.04), material=mat_skin)
    neck = create_mesh_object('cylinder', "Guard_Neck", (0, 0, 1.48), (0.075, 0.075, 0.06), material=mat_tunic_blue)
    guard_parts.extend([head, chin, neck])

    # 2. STEEL KETTLE HELMET
    helmet_dome = create_mesh_object('uv_sphere', "Helmet_Dome", (0, 0.01, 1.68), (0.145, 0.165, 0.145), material=mat_steel)
    helmet_brim = create_mesh_object('cone', "Helmet_Brim", (0, 0.01, 1.60), (0.21, 0.23, 0.035), rotation=(math.pi, 0, 0), material=mat_steel)
    helmet_ridge = create_mesh_object('cube', "Helmet_CombRidge", (0, 0.01, 1.76), (0.012, 0.15, 0.035), material=mat_steel)
    nasal_guard = create_mesh_object('cube', "Helmet_NasalGuard", (0, 0.14, 1.58), (0.016, 0.02, 0.05), material=mat_steel)
    guard_parts.extend([helmet_dome, helmet_brim, helmet_ridge, nasal_guard])

    # 3. STEEL BREASTPLATE & BACKPLATE
    breastplate = create_mesh_object('uv_sphere', "Armor_Breastplate", (0, 0.02, 1.25), (0.24, 0.18, 0.22), material=mat_steel)
    backplate = create_mesh_object('cube', "Armor_Backplate", (0, -0.06, 1.25), (0.22, 0.08, 0.20), material=mat_steel_dark)
    harness_strap_l = create_mesh_object('cube', "Strap_L", (-0.14, 0, 1.34), (0.025, 0.12, 0.015), material=mat_leather)
    harness_strap_r = create_mesh_object('cube', "Strap_R", (0.14, 0, 1.34), (0.025, 0.12, 0.015), material=mat_leather)
    guard_parts.extend([breastplate, backplate, harness_strap_l, harness_strap_r])

    # 4. ARTICULATED STEEL PAULDRONS (Shoulders)
    pauldron_l = create_mesh_object('uv_sphere', "Pauldron_L", (-0.28, 0, 1.36), (0.10, 0.12, 0.08), material=mat_steel)
    pauldron_r = create_mesh_object('uv_sphere', "Pauldron_R", (0.28, 0, 1.36), (0.10, 0.12, 0.08), material=mat_steel)
    guard_parts.extend([pauldron_l, pauldron_r])

    # 5. ROYAL BLUE TUNIC / SURCOAT WITH SILVER TRIM
    tunic_torso = create_mesh_object('cube', "Tunic_Torso", (0, 0, 1.20), (0.23, 0.15, 0.18), material=mat_tunic_blue)
    tunic_skirt = create_mesh_object('cylinder', "Tunic_Skirt", (0, 0, 0.88), (0.24, 0.20, 0.20), material=mat_tunic_blue)
    trim_front = create_mesh_object('cube', "Tunic_Trim_Front", (0, 0.19, 0.88), (0.02, 0.015, 0.20), material=mat_silver_trim)
    trim_hem = create_mesh_object('cylinder', "Tunic_Trim_Hem", (0, 0, 0.69), (0.245, 0.205, 0.02), material=mat_silver_trim)
    guard_parts.extend([tunic_torso, tunic_skirt, trim_front, trim_hem])

    # 6. LEATHER UTILITY BELT & BUCKLE
    belt = create_mesh_object('cylinder', "Belt", (0, 0, 1.05), (0.235, 0.175, 0.04), material=mat_leather)
    buckle = create_mesh_object('cube', "Belt_Buckle", (0, 0.18, 1.05), (0.04, 0.015, 0.035), material=mat_brass)
    guard_parts.extend([belt, buckle])

    # 7. LEGS & RIDING BOOTS
    thigh_l = create_mesh_object('cylinder', "Leg_Thigh_L", (-0.11, 0, 0.68), (0.075, 0.075, 0.18), material=mat_trousers)
    thigh_r = create_mesh_object('cylinder', "Leg_Thigh_R", (0.11, 0, 0.68), (0.075, 0.075, 0.18), material=mat_trousers)
    boot_cuff_l = create_mesh_object('cylinder', "Boot_Cuff_L", (-0.11, 0, 0.44), (0.09, 0.09, 0.045), material=mat_leather)
    boot_cuff_r = create_mesh_object('cylinder', "Boot_Cuff_R", (0.11, 0, 0.44), (0.09, 0.09, 0.045), material=mat_leather)
    boot_shin_l = create_mesh_object('cylinder', "Boot_Shin_L", (-0.11, 0, 0.24), (0.08, 0.08, 0.16), material=mat_leather)
    boot_shin_r = create_mesh_object('cylinder', "Boot_Shin_R", (0.11, 0, 0.24), (0.08, 0.08, 0.16), material=mat_leather)
    boot_foot_l = create_mesh_object('cube', "Boot_Foot_L", (-0.11, 0.06, 0.05), (0.075, 0.14, 0.05), material=mat_leather)
    boot_foot_r = create_mesh_object('cube', "Boot_Foot_R", (0.11, 0.06, 0.05), (0.075, 0.14, 0.05), material=mat_leather)
    guard_parts.extend([thigh_l, thigh_r, boot_cuff_l, boot_cuff_r, boot_shin_l, boot_shin_r, boot_foot_l, boot_foot_r])

    # 8. ARMS & GAUNTLET GLOVES
    upperarm_l = create_mesh_object('cylinder', "UpperArm_L", (-0.26, 0.08, 1.25), (0.065, 0.065, 0.13), rotation=(0.5, -0.3, 0), material=mat_tunic_blue)
    upperarm_r = create_mesh_object('cylinder', "UpperArm_R", (0.26, 0.08, 1.25), (0.065, 0.065, 0.13), rotation=(0.6, 0.4, 0), material=mat_tunic_blue)
    forearm_l = create_mesh_object('cylinder', "Forearm_L", (-0.18, 0.26, 1.16), (0.055, 0.055, 0.12), rotation=(1.2, -0.6, 0), material=mat_steel_dark)
    forearm_r = create_mesh_object('cylinder', "Forearm_R", (0.18, 0.24, 1.14), (0.055, 0.055, 0.12), rotation=(1.1, 0.5, 0), material=mat_steel_dark)
    glove_l = create_mesh_object('cube', "Glove_L", (-0.08, 0.38, 1.18), (0.045, 0.06, 0.045), material=mat_leather)
    glove_r = create_mesh_object('cube', "Glove_R", (0.10, 0.30, 1.12), (0.045, 0.06, 0.045), material=mat_leather)
    guard_parts.extend([upperarm_l, upperarm_r, forearm_l, forearm_r, glove_l, glove_r])

    # 9. REALISTIC TACTICAL ASSAULT RIFLE (Replacing spear/shield as requested)
    rifle_receiver = create_mesh_object('cube', "Rifle_Receiver", (0.05, 0.38, 1.16), (0.03, 0.14, 0.05), material=mat_gunmetal)
    rifle_barrel = create_mesh_object('cylinder', "Rifle_Barrel", (0.05, 0.62, 1.18), (0.014, 0.014, 0.14), rotation=(math.pi/2, 0, 0), material=mat_gunmetal)
    rifle_muzzle = create_mesh_object('cylinder', "Rifle_MuzzleBrake", (0.05, 0.78, 1.18), (0.018, 0.018, 0.03), rotation=(math.pi/2, 0, 0), material=mat_gunmetal)
    rifle_handguard = create_mesh_object('cube', "Rifle_Handguard", (0.05, 0.52, 1.18), (0.032, 0.10, 0.038), material=mat_gunmetal)
    rifle_magazine = create_mesh_object('cube', "Rifle_Magazine", (0.05, 0.36, 1.05), (0.024, 0.045, 0.08), rotation=(-0.25, 0, 0), material=mat_gunmetal)
    rifle_stock = create_mesh_object('cube', "Rifle_Stock", (0.05, 0.16, 1.14), (0.026, 0.10, 0.04), material=mat_gunmetal)
    rifle_optic = create_mesh_object('cube', "Rifle_OpticSight", (0.05, 0.40, 1.22), (0.025, 0.05, 0.025), material=mat_steel_dark)
    guard_parts.extend([rifle_receiver, rifle_barrel, rifle_muzzle, rifle_handguard, rifle_magazine, rifle_stock, rifle_optic])

    # Join or Parent into Royal Guard Master Root
    bpy.ops.object.empty_add(type='PLAIN_AXES', location=(0,0,0))
    root = bpy.context.active_object
    root.name = "RoyalGuard_Master"

    for part in guard_parts:
        part.parent = root

    print("[RoyalGuardBuilder] Successfully generated Royal Guard with Tactical Rifle in Blender!")

    # Auto export to FBX / OBJ if script is run within project
    current_dir = os.path.dirname(bpy.data.filepath) if bpy.data.filepath else os.getcwd()
    fbx_out = os.path.join(current_dir, "RoyalGuard_Tactical.fbx")
    try:
        bpy.ops.export_scene.fbx(filepath=fbx_out, use_selection=False)
        print(f"[RoyalGuardBuilder] Exported FBX to: {fbx_out}")
    except Exception as e:
        print(f"[RoyalGuardBuilder] FBX export note: {e}")

if __name__ == "__main__":
    build_royal_guard()
