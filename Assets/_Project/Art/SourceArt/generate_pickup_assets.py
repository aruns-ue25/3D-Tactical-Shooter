import os
import uuid

def gen_guid():
    return uuid.uuid4().hex

BASE_DIR = os.path.abspath(r"Assets/_Project/Art")
TEX_DIR = os.path.join(BASE_DIR, "Textures")
MODELS_DIR = os.path.join(BASE_DIR, "Models")
MAT_DIR = os.path.join(BASE_DIR, "Materials")

guid_ammo_albedo = gen_guid()
guid_ammo_normal = gen_guid()
guid_ammo_met = gen_guid()
guid_ammo_em = gen_guid()
guid_ammo_mat = gen_guid()
guid_ammo_fbx = gen_guid()

guid_med_albedo = gen_guid()
guid_med_normal = gen_guid()
guid_med_met = gen_guid()
guid_med_em = gen_guid()
guid_med_mat = gen_guid()
guid_med_fbx = gen_guid()

TEXTURE_META_TEMPLATE = """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 1
    sRGBTexture: {srgb}
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 1024
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 0
    wrapV: 0
    wrapW: 0
  nPOTScale: 1
  lightmap: 0
  compressionQuality: 50
  spriteMode: 0
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 0
  spriteTessellationDetail: -1
  textureType: {tex_type}
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings: []
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    customData: 
    physicsShape: []
    bones: []
    spriteID: 
    internalID: 0
    vertices: []
    indices: 
    edges: []
    weights: []
    secondaryTextures: []
    spriteCustomMetadata:
      entries: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName: 
  pSDRemoveMatte: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

def write_meta(filepath, guid, is_normal=False):
    content = TEXTURE_META_TEMPLATE.format(
        guid=guid,
        srgb=0 if is_normal else 1,
        tex_type=1 if is_normal else 0
    )
    with open(filepath + ".meta", "w", encoding="utf-8") as f:
        f.write(content)

write_meta(os.path.join(TEX_DIR, "T_AmmoBox_Albedo.png"), guid_ammo_albedo)
write_meta(os.path.join(TEX_DIR, "T_AmmoBox_Normal.png"), guid_ammo_normal, is_normal=True)
write_meta(os.path.join(TEX_DIR, "T_AmmoBox_MetallicSmoothness.png"), guid_ammo_met)
write_meta(os.path.join(TEX_DIR, "T_AmmoBox_Emission.png"), guid_ammo_em)

write_meta(os.path.join(TEX_DIR, "T_Medkit_Albedo.png"), guid_med_albedo)
write_meta(os.path.join(TEX_DIR, "T_Medkit_Normal.png"), guid_med_normal, is_normal=True)
write_meta(os.path.join(TEX_DIR, "T_Medkit_MetallicSmoothness.png"), guid_med_met)
write_meta(os.path.join(TEX_DIR, "T_Medkit_Emission.png"), guid_med_em)

FBX_META_TEMPLATE = """fileFormatVersion: 2
guid: {guid}
ModelImporter:
  serializedVersion: 24300
  internalIDToNameTable: []
  externalObjects: {{}}
  materials:
    materialImportMode: 0
    materialName: 0
    materialSearch: 1
    materialLocation: 1
    searchTexturesGlobally: 0
  animations:
    legacyGenerateAnimations: 4
    bakeSimulation: 0
    resampleCurves: 1
    optimizeGameObjects: 0
    removeConstantScaleCurves: 0
    motionNodeName: 
    animationImportErrors: 
    animationImportWarnings: 
    animationRetargetingWarnings: 
    animationDoRetargetingWarnings: 0
    importAnimatedCustomProperties: 0
    importConstraints: 0
    animationCompression: 1
    animationRotationError: 0.5
    animationPositionError: 0.5
    animationScaleError: 0.5
    animationWrapMode: 0
    extraExposedTransformPaths: []
    extraUserProperties: []
    clipAnimations: []
    isReadable: 0
  meshes:
    lODScreenPercentages: []
    globalScale: 1
    meshCompression: 0
    addColliders: 0
    useSRGBMaterialColor: 1
    sortHierarchyByName: 1
    importPhysicalCameras: 1
    importVisibility: 1
    importBlendShapes: 1
    importCameras: 1
    importLights: 1
    nodeNameCollisionStrategy: 1
    fileIdsGeneration: 2
    swapUVChannels: 0
    generateSecondaryUV: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

with open(os.path.join(MODELS_DIR, "AmmoBox_Custom.fbx.meta"), "w", encoding="utf-8") as f:
    f.write(FBX_META_TEMPLATE.format(guid=guid_ammo_fbx))

with open(os.path.join(MODELS_DIR, "Medkit_Custom.fbx.meta"), "w", encoding="utf-8") as f:
    f.write(FBX_META_TEMPLATE.format(guid=guid_med_fbx))

MATERIAL_TEMPLATE = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &-1068510266806334203
MonoBehaviour:
  m_ObjectHideFlags: 11
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: d0353a89b1f911e48b9e16bdc9f2e058, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Unity.RenderPipelines.Universal.Editor::UnityEditor.Rendering.Universal.AssetVersion
  version: 10
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: {mat_name}
  m_Shader: {{fileID: 4800000, guid: 933532a4fcc9baf4fa0491de14d08ed7, type: 3}}
  m_Parent: {{fileID: 0}}
  m_ModifiedSerializedProperties: 0
  m_ValidKeywords:
  - _METALLICSPECGLOSSMAP
  - _NORMALMAP
  - _EMISSION
  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 0
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: -1
  stringTagMap:
    RenderType: Opaque
  disabledShaderPasses:
  - MOTIONVECTORS
  m_LockedProperties: 
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs:
    - _BaseMap:
        m_Texture: {{fileID: 2800000, guid: {albedo_guid}, type: 3}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    - _BumpMap:
        m_Texture: {{fileID: 2800000, guid: {normal_guid}, type: 3}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    - _EmissionMap:
        m_Texture: {{fileID: 2800000, guid: {emission_guid}, type: 3}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    - _MainTex:
        m_Texture: {{fileID: 2800000, guid: {albedo_guid}, type: 3}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    - _MetallicGlossMap:
        m_Texture: {{fileID: 2800000, guid: {met_guid}, type: 3}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    m_Ints: []
    m_Floats:
    - _BumpScale: 1
    - _Cull: 2
    - _Metallic: 1
    - _Smoothness: 0.75
    - _Surface: 0
    - _WorkflowMode: 1
    - _ZWrite: 1
    m_Colors:
    - _BaseColor: {{r: 1, g: 1, b: 1, a: 1}}
    - _Color: {{r: 1, g: 1, b: 1, a: 1}}
    - _EmissionColor: {{{em_r}, {em_g}, {em_b}, a: 1}}
  m_BuildTextureStacks: []
  m_AllowLocking: 1
"""

ammo_mat_content = MATERIAL_TEMPLATE.format(
    mat_name="M_AmmoBox_Custom",
    albedo_guid=guid_ammo_albedo,
    normal_guid=guid_ammo_normal,
    met_guid=guid_ammo_met,
    emission_guid=guid_ammo_em,
    em_r="r: 2.5", em_g="g: 1.8", em_b="b: 0.2"
)
with open(os.path.join(MAT_DIR, "M_AmmoBox_Custom.mat"), "w", encoding="utf-8") as f:
    f.write(ammo_mat_content)
with open(os.path.join(MAT_DIR, "M_AmmoBox_Custom.mat.meta"), "w", encoding="utf-8") as f:
    f.write(f"fileFormatVersion: 2\nguid: {guid_ammo_mat}\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")

med_mat_content = MATERIAL_TEMPLATE.format(
    mat_name="M_Medkit_Custom",
    albedo_guid=guid_med_albedo,
    normal_guid=guid_med_normal,
    met_guid=guid_med_met,
    emission_guid=guid_med_em,
    em_r="r: 0.4", em_g="g: 2.8", em_b="b: 1.1"
)
with open(os.path.join(MAT_DIR, "M_Medkit_Custom.mat"), "w", encoding="utf-8") as f:
    f.write(med_mat_content)
with open(os.path.join(MAT_DIR, "M_Medkit_Custom.mat.meta"), "w", encoding="utf-8") as f:
    f.write(f"fileFormatVersion: 2\nguid: {guid_med_mat}\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")

print("[OK] All pickup meta and material assets generated successfully!")
