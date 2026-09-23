import os
import glob
import re
import uuid

BASE_DIR = r"C:\Users\tyram\The Spawn of Chaos"

SWORD_IDLE_DIR = os.path.join(BASE_DIR, r"Assets\Scenes\animations\frames\newidlestraw-838517e4")
SWORD_STANCE_DIR = os.path.join(BASE_DIR, r"Assets\Scenes\animations\frames\newstancestraw-5fb4cdc3")
SWORD_WALK_DIR = os.path.join(BASE_DIR, r"Assets\Scenes\animations\frames\newwalkstraw-9edabc38")
SWORD_ATTACK_DIR = os.path.join(BASE_DIR, r"Assets\Scenes\animations\frames\newattackstraw-ba1be727")

BRUTE_IDLE_DIR = os.path.join(BASE_DIR, r"Assets\Scenes\animations\frames\newfat-e5a73ac3")
BRUTE_WALK_DIR = os.path.join(BASE_DIR, r"Assets\Scenes\animations\frames\newfatwalk-5b766109")
BRUTE_ATTACK_DIR = os.path.join(BASE_DIR, r"Assets\Scenes\animations\frames\newfatattack-68451e5a")

ANIM_DIR = os.path.join(BASE_DIR, r"Assets\Scenes\animations\animators")
PREFAB_DIR = os.path.join(BASE_DIR, r"Assets\Prefabs\Enemies")
RESOURCES_PREFAB_DIR = os.path.join(BASE_DIR, r"Assets\Resources\Prefabs\Enemies")

SWORD_SCRIPT_GUID = "e8a21f63b4094e4a9547d2f9b8c31001"
BRUTE_SCRIPT_GUID = "f1b32a74c5184f5ba658e3a0c9d42002"
SETUP_SCRIPT_GUID = "a7d43e91b5284c6da081f2c9e7b45003"
HEALTH_SCRIPT_GUID = "4be95743e3964c04fbdb066318ca31ec"
SPRITE_MAT_GUID = "a97c105638bdf8b4a8650670310a4cd3"

def make_meta(path, guid, importer="NativeFormatImporter", main_fid=None):
    if os.path.exists(path):
        return
    content = f"fileFormatVersion: 2\nguid: {guid}\n"
    if importer == "MonoImporter":
        content += "MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    elif importer == "PrefabImporter":
        content += "PrefabImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    elif importer == "NativeFormatImporter":
        content += f"NativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: {main_fid}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    with open(path, "w", encoding="utf-8") as f:
        f.write(content)

def configure_png_meta(png_path):
    meta_path = png_path + ".meta"
    guid = None
    if os.path.exists(meta_path):
        with open(meta_path, "r", encoding="utf-8", errors="ignore") as f:
            for line in f:
                if line.startswith("guid:"):
                    guid = line.split(":")[1].strip()
                    break
    if not guid:
        guid = uuid.uuid4().hex

    # Ensure single sprite mode (spriteMode: 1, textureType: 8, spritePixelsToUnits: 100)
    meta_content = f"""fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
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
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbook: 0
  ignorePngGamma: 0
  cookieLightType: 0
  platformSettings: []
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
    with open(meta_path, "w", encoding="utf-8") as f:
        f.write(meta_content)
    return guid

def get_folder_sprites(folder):
    files = sorted(glob.glob(os.path.join(folder, "*.png")))
    guids = []
    for f in files:
        g = configure_png_meta(f)
        guids.append(g)
    return guids

def generate_anim_clip(name, guids, fps, loop, out_path, guid):
    stop_time = (len(guids) - 1) / float(fps)
    curve_lines = []
    mapping_lines = []
    for i, g in enumerate(guids):
        t = i / float(fps)
        curve_lines.append(f"    - time: {t:.4f}\n      value: {{fileID: 21300000, guid: {g}, type: 3}}")
        mapping_lines.append(f"    - {{fileID: 21300000, guid: {g}, type: 3}}")

    curve_str = "\n".join(curve_lines)
    mapping_str = "\n".join(mapping_lines)
    loop_val = 1 if loop else 0

    content = f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!74 &7400000
AnimationClip:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: {name}
  serializedVersion: 7
  m_Legacy: 0
  m_Compressed: 0
  m_UseHighQualityCurve: 1
  m_RotationCurves: []
  m_CompressedRotationCurves: []
  m_EulerCurves: []
  m_PositionCurves: []
  m_ScaleCurves: []
  m_FloatCurves: []
  m_PPtrCurves:
  - serializedVersion: 2
    curve:
{curve_str}
    attribute: m_Sprite
    path: 
    classID: 212
    script: {{fileID: 0}}
    flags: 2
  m_SampleRate: {fps}
  m_WrapMode: 0
  m_Bounds:
    m_Center: {{x: 0, y: 0, z: 0}}
    m_Extent: {{x: 0, y: 0, z: 0}}
  m_ClipBindingConstant:
    genericBindings:
    - serializedVersion: 2
      path: 0
      attribute: 0
      script: {{fileID: 0}}
      typeID: 212
      customType: 23
      isPPtrCurve: 1
      isIntCurve: 0
      isSerializeReferenceCurve: 0
    pptrCurveMapping:
{mapping_str}
  m_AnimationClipSettings:
    serializedVersion: 2
    m_AdditiveReferencePoseClip: {{fileID: 0}}
    m_AdditiveReferencePoseTime: 0
    m_StartTime: 0
    m_StopTime: {stop_time:.4f}
    m_OrientationOffsetY: 0
    m_Level: 0
    m_CycleOffset: 0
    m_HasAdditiveReferencePose: 0
    m_LoopTime: {loop_val}
    m_LoopBlend: 0
    m_LoopBlendOrientation: 0
    m_LoopBlendPositionY: 0
    m_LoopBlendPositionXZ: 0
    m_KeepOriginalOrientation: 0
    m_KeepOriginalPositionY: 1
    m_KeepOriginalPositionXZ: 0
    m_HeightFromFeet: 0
    m_Mirror: 0
  m_EditorCurves: []
  m_EulerEditorCurves: []
  m_HasGenericRootTransform: 0
  m_HasMotionFloatCurves: 0
  m_Events: []
"""
    with open(out_path, "w", encoding="utf-8") as f:
        f.write(content)
    make_meta(out_path + ".meta", guid, importer="NativeFormatImporter", main_fid=7400000)

def generate_sword_controller(out_path, guid, idle_guid, stance_guid, walk_guid, attack_guid):
    content = f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!91 &9100000
AnimatorController:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: StrawhatSwordController
  serializedVersion: 5
  m_AnimatorParameters:
  - m_Name: isWalking
    m_Type: 4
    m_DefaultFloat: 0
    m_DefaultInt: 0
    m_DefaultBool: 0
    m_Controller: {{fileID: 9100000}}
  - m_Name: isStance
    m_Type: 4
    m_DefaultFloat: 0
    m_DefaultInt: 0
    m_DefaultBool: 0
    m_Controller: {{fileID: 9100000}}
  - m_Name: Attack
    m_Type: 9
    m_DefaultFloat: 0
    m_DefaultInt: 0
    m_DefaultBool: 0
    m_Controller: {{fileID: 9100000}}
  - m_Name: Parry
    m_Type: 9
    m_DefaultFloat: 0
    m_DefaultInt: 0
    m_DefaultBool: 0
    m_Controller: {{fileID: 9100000}}
  m_AnimatorLayers:
  - serializedVersion: 5
    m_Name: Base Layer
    m_StateMachine: {{fileID: 110700001}}
    m_Mask: {{fileID: 0}}
    m_Motions: []
    m_Behaviours: []
    m_BlendingMode: 0
    m_SyncedLayerIndex: -1
    m_DefaultWeight: 0
    m_IKPass: 0
    m_SyncedLayerAffectsTiming: 0
    m_Controller: {{fileID: 9100000}}
--- !u!1101 &110100001
AnimatorStateTransition:
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: 
  m_Conditions:
  - m_ConditionMode: 1
    m_ConditionEvent: isWalking
    m_EventTreshold: 0
  m_DstStateMachine: {{fileID: 0}}
  m_DstState: {{fileID: 110200002}}
  m_Solo: 0
  m_Mute: 0
  m_IsExit: 0
  serializedVersion: 3
  m_TransitionDuration: 0.05
  m_TransitionOffset: 0
  m_ExitTime: 0.9
  m_HasExitTime: 0
  m_HasFixedDuration: 1
  m_InterruptionSource: 0
  m_OrderedInterruption: 1
  m_CanTransitionToSelf: 1
--- !u!1101 &110100002
AnimatorStateTransition:
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: 
  m_Conditions:
  - m_ConditionMode: 2
    m_ConditionEvent: isWalking
    m_EventTreshold: 0
  m_DstStateMachine: {{fileID: 0}}
  m_DstState: {{fileID: 110200001}}
  m_Solo: 0
  m_Mute: 0
  m_IsExit: 0
  serializedVersion: 3
  m_TransitionDuration: 0.05
  m_TransitionOffset: 0
  m_ExitTime: 0.9
  m_HasExitTime: 0
  m_HasFixedDuration: 1
  m_InterruptionSource: 0
  m_OrderedInterruption: 1
  m_CanTransitionToSelf: 1
--- !u!1101 &110100003
AnimatorStateTransition:
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: 
  m_Conditions:
  - m_ConditionMode: 1
    m_ConditionEvent: isStance
    m_EventTreshold: 0
  m_DstStateMachine: {{fileID: 0}}
  m_DstState: {{fileID: 110200003}}
  m_Solo: 0
  m_Mute: 0
  m_IsExit: 0
  serializedVersion: 3
  m_TransitionDuration: 0.05
  m_TransitionOffset: 0
  m_ExitTime: 0.9
  m_HasExitTime: 0
  m_HasFixedDuration: 1
  m_InterruptionSource: 0
  m_OrderedInterruption: 1
  m_CanTransitionToSelf: 1
--- !u!1101 &110100004
AnimatorStateTransition:
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: 
  m_Conditions:
  - m_ConditionMode: 2
    m_ConditionEvent: isStance
    m_EventTreshold: 0
  m_DstStateMachine: {{fileID: 0}}
  m_DstState: {{fileID: 110200001}}
  m_Solo: 0
  m_Mute: 0
  m_IsExit: 0
  serializedVersion: 3
  m_TransitionDuration: 0.05
  m_TransitionOffset: 0
  m_ExitTime: 0.9
  m_HasExitTime: 0
  m_HasFixedDuration: 1
  m_InterruptionSource: 0
  m_OrderedInterruption: 1
  m_CanTransitionToSelf: 1
--- !u!1101 &110100005
AnimatorStateTransition:
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: 
  m_Conditions:
  - m_ConditionMode: 1
    m_ConditionEvent: Attack
    m_EventTreshold: 0
  m_DstStateMachine: {{fileID: 0}}
  m_DstState: {{fileID: 110200004}}
  m_Solo: 0
  m_Mute: 0
  m_IsExit: 0
  serializedVersion: 3
  m_TransitionDuration: 0.02
  m_TransitionOffset: 0
  m_ExitTime: 0.75
  m_HasExitTime: 0
  m_HasFixedDuration: 1
  m_InterruptionSource: 0
  m_OrderedInterruption: 1
  m_CanTransitionToSelf: 1
--- !u!1101 &110100006
AnimatorStateTransition:
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: 
  m_Conditions: []
  m_DstStateMachine: {{fileID: 0}}
  m_DstState: {{fileID: 110200001}}
  m_Solo: 0
  m_Mute: 0
  m_IsExit: 0
  serializedVersion: 3
  m_TransitionDuration: 0.05
  m_TransitionOffset: 0
  m_ExitTime: 0.92
  m_HasExitTime: 1
  m_HasFixedDuration: 1
  m_InterruptionSource: 0
  m_OrderedInterruption: 1
  m_CanTransitionToSelf: 1
--- !u!1102 &110200001
AnimatorState:
  serializedVersion: 6
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: Idle
  m_Speed: 1
  m_CycleOffset: 0
  m_Transitions:
  - {{fileID: 110100001}}
  - {{fileID: 110100003}}
  m_StateMachineBehaviours: []
  m_Position: {{x: 200, y: 0, z: 0}}
  m_IKOnFeet: 0
  m_WriteDefaultValues: 1
  m_Mirror: 0
  m_SpeedParameterActive: 0
  m_MirrorParameterActive: 0
  m_CycleOffsetParameterActive: 0
  m_TimeParameterActive: 0
  m_Motion: {{fileID: 7400000, guid: {idle_guid}, type: 2}}
  m_Tag: 
  m_SpeedParameter: 
  m_MirrorParameter: 
  m_CycleOffsetParameter: 
  m_TimeParameter: 
--- !u!1102 &110200002
AnimatorState:
  serializedVersion: 6
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: Walk
  m_Speed: 1
  m_CycleOffset: 0
  m_Transitions:
  - {{fileID: 110100002}}
  m_StateMachineBehaviours: []
  m_Position: {{x: 235, y: 65, z: 0}}
  m_IKOnFeet: 0
  m_WriteDefaultValues: 1
  m_Mirror: 0
  m_SpeedParameterActive: 0
  m_MirrorParameterActive: 0
  m_CycleOffsetParameterActive: 0
  m_TimeParameterActive: 0
  m_Motion: {{fileID: 7400000, guid: {walk_guid}, type: 2}}
  m_Tag: 
  m_SpeedParameter: 
  m_MirrorParameter: 
  m_CycleOffsetParameter: 
  m_TimeParameter: 
--- !u!1102 &110200003
AnimatorState:
  serializedVersion: 6
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: Stance
  m_Speed: 1
  m_CycleOffset: 0
  m_Transitions:
  - {{fileID: 110100004}}
  m_StateMachineBehaviours: []
  m_Position: {{x: 270, y: 130, z: 0}}
  m_IKOnFeet: 0
  m_WriteDefaultValues: 1
  m_Mirror: 0
  m_SpeedParameterActive: 0
  m_MirrorParameterActive: 0
  m_CycleOffsetParameterActive: 0
  m_TimeParameterActive: 0
  m_Motion: {{fileID: 7400000, guid: {stance_guid}, type: 2}}
  m_Tag: 
  m_SpeedParameter: 
  m_MirrorParameter: 
  m_CycleOffsetParameter: 
  m_TimeParameter: 
--- !u!1102 &110200004
AnimatorState:
  serializedVersion: 6
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: Attack
  m_Speed: 1
  m_CycleOffset: 0
  m_Transitions:
  - {{fileID: 110100006}}
  m_StateMachineBehaviours: []
  m_Position: {{x: 300, y: 195, z: 0}}
  m_IKOnFeet: 0
  m_WriteDefaultValues: 1
  m_Mirror: 0
  m_SpeedParameterActive: 0
  m_MirrorParameterActive: 0
  m_CycleOffsetParameterActive: 0
  m_TimeParameterActive: 0
  m_Motion: {{fileID: 7400000, guid: {attack_guid}, type: 2}}
  m_Tag: 
  m_SpeedParameter: 
  m_MirrorParameter: 
  m_CycleOffsetParameter: 
  m_TimeParameter: 
--- !u!1107 &110700001
AnimatorStateMachine:
  serializedVersion: 6
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: Base Layer
  m_ChildStates:
  - serializedVersion: 1
    m_State: {{fileID: 110200001}}
    m_Position: {{x: 200, y: 0, z: 0}}
  - serializedVersion: 1
    m_State: {{fileID: 110200002}}
    m_Position: {{x: 235, y: 65, z: 0}}
  - serializedVersion: 1
    m_State: {{fileID: 110200003}}
    m_Position: {{x: 270, y: 130, z: 0}}
  - serializedVersion: 1
    m_State: {{fileID: 110200004}}
    m_Position: {{x: 300, y: 195, z: 0}}
  m_ChildStateMachines: []
  m_AnyStateTransitions:
  - {{fileID: 110100005}}
  m_EntryTransitions: []
  m_StateMachineTransitions: {{}}
  m_StateMachineBehaviours: []
  m_AnyStatePosition: {{x: 50, y: 20, z: 0}}
  m_EntryPosition: {{x: 50, y: 120, z: 0}}
  m_ExitPosition: {{x: 800, y: 120, z: 0}}
  m_ParentStateMachinePosition: {{x: 800, y: 20, z: 0}}
  m_DefaultState: {{fileID: 110200001}}
"""
    with open(out_path, "w", encoding="utf-8") as f:
        f.write(content)
    make_meta(out_path + ".meta", guid, importer="NativeFormatImporter", main_fid=9100000)

def generate_brute_controller(out_path, guid, idle_guid, walk_guid, attack_guid):
    content = f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!91 &9100000
AnimatorController:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: StrawhatBruteController
  serializedVersion: 5
  m_AnimatorParameters:
  - m_Name: isWalking
    m_Type: 4
    m_DefaultFloat: 0
    m_DefaultInt: 0
    m_DefaultBool: 0
    m_Controller: {{fileID: 9100000}}
  - m_Name: Attack
    m_Type: 9
    m_DefaultFloat: 0
    m_DefaultInt: 0
    m_DefaultBool: 0
    m_Controller: {{fileID: 9100000}}
  m_AnimatorLayers:
  - serializedVersion: 5
    m_Name: Base Layer
    m_StateMachine: {{fileID: 110700001}}
    m_Mask: {{fileID: 0}}
    m_Motions: []
    m_Behaviours: []
    m_BlendingMode: 0
    m_SyncedLayerIndex: -1
    m_DefaultWeight: 0
    m_IKPass: 0
    m_SyncedLayerAffectsTiming: 0
    m_Controller: {{fileID: 9100000}}
--- !u!1101 &110100001
AnimatorStateTransition:
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: 
  m_Conditions:
  - m_ConditionMode: 1
    m_ConditionEvent: isWalking
    m_EventTreshold: 0
  m_DstStateMachine: {{fileID: 0}}
  m_DstState: {{fileID: 110200002}}
  m_Solo: 0
  m_Mute: 0
  m_IsExit: 0
  serializedVersion: 3
  m_TransitionDuration: 0.06
  m_TransitionOffset: 0
  m_ExitTime: 0.9
  m_HasExitTime: 0
  m_HasFixedDuration: 1
  m_InterruptionSource: 0
  m_OrderedInterruption: 1
  m_CanTransitionToSelf: 1
--- !u!1101 &110100002
AnimatorStateTransition:
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: 
  m_Conditions:
  - m_ConditionMode: 2
    m_ConditionEvent: isWalking
    m_EventTreshold: 0
  m_DstStateMachine: {{fileID: 0}}
  m_DstState: {{fileID: 110200001}}
  m_Solo: 0
  m_Mute: 0
  m_IsExit: 0
  serializedVersion: 3
  m_TransitionDuration: 0.06
  m_TransitionOffset: 0
  m_ExitTime: 0.9
  m_HasExitTime: 0
  m_HasFixedDuration: 1
  m_InterruptionSource: 0
  m_OrderedInterruption: 1
  m_CanTransitionToSelf: 1
--- !u!1101 &110100003
AnimatorStateTransition:
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: 
  m_Conditions:
  - m_ConditionMode: 1
    m_ConditionEvent: Attack
    m_EventTreshold: 0
  m_DstStateMachine: {{fileID: 0}}
  m_DstState: {{fileID: 110200003}}
  m_Solo: 0
  m_Mute: 0
  m_IsExit: 0
  serializedVersion: 3
  m_TransitionDuration: 0.02
  m_TransitionOffset: 0
  m_ExitTime: 0.75
  m_HasExitTime: 0
  m_HasFixedDuration: 1
  m_InterruptionSource: 0
  m_OrderedInterruption: 1
  m_CanTransitionToSelf: 1
--- !u!1101 &110100004
AnimatorStateTransition:
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: 
  m_Conditions: []
  m_DstStateMachine: {{fileID: 0}}
  m_DstState: {{fileID: 110200001}}
  m_Solo: 0
  m_Mute: 0
  m_IsExit: 0
  serializedVersion: 3
  m_TransitionDuration: 0.06
  m_TransitionOffset: 0
  m_ExitTime: 0.92
  m_HasExitTime: 1
  m_HasFixedDuration: 1
  m_InterruptionSource: 0
  m_OrderedInterruption: 1
  m_CanTransitionToSelf: 1
--- !u!1102 &110200001
AnimatorState:
  serializedVersion: 6
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: Idle
  m_Speed: 1
  m_CycleOffset: 0
  m_Transitions:
  - {{fileID: 110100001}}
  m_StateMachineBehaviours: []
  m_Position: {{x: 200, y: 0, z: 0}}
  m_IKOnFeet: 0
  m_WriteDefaultValues: 1
  m_Mirror: 0
  m_SpeedParameterActive: 0
  m_MirrorParameterActive: 0
  m_CycleOffsetParameterActive: 0
  m_TimeParameterActive: 0
  m_Motion: {{fileID: 7400000, guid: {idle_guid}, type: 2}}
  m_Tag: 
  m_SpeedParameter: 
  m_MirrorParameter: 
  m_CycleOffsetParameter: 
  m_TimeParameter: 
--- !u!1102 &110200002
AnimatorState:
  serializedVersion: 6
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: Walk
  m_Speed: 1
  m_CycleOffset: 0
  m_Transitions:
  - {{fileID: 110100002}}
  m_StateMachineBehaviours: []
  m_Position: {{x: 235, y: 65, z: 0}}
  m_IKOnFeet: 0
  m_WriteDefaultValues: 1
  m_Mirror: 0
  m_SpeedParameterActive: 0
  m_MirrorParameterActive: 0
  m_CycleOffsetParameterActive: 0
  m_TimeParameterActive: 0
  m_Motion: {{fileID: 7400000, guid: {walk_guid}, type: 2}}
  m_Tag: 
  m_SpeedParameter: 
  m_MirrorParameter: 
  m_CycleOffsetParameter: 
  m_TimeParameter: 
--- !u!1102 &110200003
AnimatorState:
  serializedVersion: 6
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: Attack
  m_Speed: 1
  m_CycleOffset: 0
  m_Transitions:
  - {{fileID: 110100004}}
  m_StateMachineBehaviours: []
  m_Position: {{x: 270, y: 130, z: 0}}
  m_IKOnFeet: 0
  m_WriteDefaultValues: 1
  m_Mirror: 0
  m_SpeedParameterActive: 0
  m_MirrorParameterActive: 0
  m_CycleOffsetParameterActive: 0
  m_TimeParameterActive: 0
  m_Motion: {{fileID: 7400000, guid: {attack_guid}, type: 2}}
  m_Tag: 
  m_SpeedParameter: 
  m_MirrorParameter: 
  m_CycleOffsetParameter: 
  m_TimeParameter: 
--- !u!1107 &110700001
AnimatorStateMachine:
  serializedVersion: 6
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: Base Layer
  m_ChildStates:
  - serializedVersion: 1
    m_State: {{fileID: 110200001}}
    m_Position: {{x: 200, y: 0, z: 0}}
  - serializedVersion: 1
    m_State: {{fileID: 110200002}}
    m_Position: {{x: 235, y: 65, z: 0}}
  - serializedVersion: 1
    m_State: {{fileID: 110200003}}
    m_Position: {{x: 270, y: 130, z: 0}}
  m_ChildStateMachines: []
  m_AnyStateTransitions:
  - {{fileID: 110100003}}
  m_EntryTransitions: []
  m_StateMachineTransitions: {{}}
  m_StateMachineBehaviours: []
  m_AnyStatePosition: {{x: 50, y: 20, z: 0}}
  m_EntryPosition: {{x: 50, y: 120, z: 0}}
  m_ExitPosition: {{x: 800, y: 120, z: 0}}
  m_ParentStateMachinePosition: {{x: 800, y: 20, z: 0}}
  m_DefaultState: {{fileID: 110200001}}
"""
    with open(out_path, "w", encoding="utf-8") as f:
        f.write(content)
    make_meta(out_path + ".meta", guid, importer="NativeFormatImporter", main_fid=9100000)

def generate_sword_prefab(out_path, prefab_guid, controller_guid, initial_sprite_guid, idles, stances, walks, attacks):
    def format_sprite_list(guids):
        return "\n".join([f"  - {{fileID: 21300000, guid: {g}, type: 3}}" for g in guids])

    idles_str = format_sprite_list(idles)
    stances_str = format_sprite_list(stances)
    walks_str = format_sprite_list(walks)
    attacks_str = format_sprite_list(attacks)

    content = f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &1000001
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 1000002}}
  - component: {{fileID: 1000003}}
  - component: {{fileID: 1000004}}
  - component: {{fileID: 1000005}}
  - component: {{fileID: 1000006}}
  - component: {{fileID: 1000007}}
  - component: {{fileID: 1000008}}
  m_Layer: 0
  m_Name: StrawhatSwordMob
  m_TagString: enemy
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &1000002
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000001}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 0.38, y: 0.38, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!212 &1000003
SpriteRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000001}}
  m_Enabled: 1
  m_CastShadows: 0
  m_ReceiveShadows: 0
  m_DynamicOccludee: 1
  m_StaticShadowCaster: 0
  m_MotionVectors: 1
  m_LightProbeUsage: 1
  m_ReflectionProbeUsage: 1
  m_RayTracingMode: 0
  m_RayTraceProcedural: 0
  m_Materials:
  - {{fileID: 2100000, guid: {SPRITE_MAT_GUID}, type: 2}}
  m_StaticBatchInfo:
    firstSubMesh: 0
    subMeshCount: 0
  m_SortingLayerID: 0
  m_SortingLayer: 0
  m_SortingOrder: 5
  m_Sprite: {{fileID: 21300000, guid: {initial_sprite_guid}, type: 3}}
  m_Color: {{r: 1, g: 1, b: 1, a: 1}}
  m_FlipX: 0
  m_FlipY: 0
  m_DrawMode: 0
--- !u!95 &1000004
Animator:
  serializedVersion: 7
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000001}}
  m_Enabled: 1
  m_Avatar: {{fileID: 0}}
  m_Controller: {{fileID: 9100000, guid: {controller_guid}, type: 2}}
  m_CullingMode: 0
  m_UpdateMode: 0
  m_ApplyRootMotion: 0
  m_LinearVelocityBlending: 0
  m_StabilizeFeet: 0
  m_WarningMessage: 
  m_HasTransformHierarchy: 1
  m_AllowConstantClipSamplingOptimization: 1
  m_KeepAnimatorStateOnDisable: 0
  m_WriteDefaultValuesOnDisable: 0
--- !u!50 &1000005
Rigidbody2D:
  serializedVersion: 4
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000001}}
  m_BodyType: 0
  m_Simulated: 1
  m_UseFullKinematicContacts: 0
  m_UseAutoMass: 0
  m_Mass: 1
  m_LinearDrag: 0
  m_AngularDrag: 0.05
  m_GravityScale: 2.5
  m_Material: {{fileID: 0}}
  m_Interpolate: 0
  m_SleepingMode: 1
  m_CollisionDetection: 1
  m_Constraints: 4
--- !u!61 &1000006
BoxCollider2D:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000001}}
  m_Enabled: 1
  serializedVersion: 3
  m_Density: 1
  m_Material: {{fileID: 0}}
  m_IsTrigger: 0
  m_Offset: {{x: 0, y: 0}}
  m_Size: {{x: 2.0, y: 3.6}}
  m_EdgeRadius: 0
--- !u!114 &1000007
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000001}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {SWORD_SCRIPT_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::StrawhatSwordAI
  currentState: 0
  maxHealth: 110
  slashDamage: 20
  playerKnockbackForce: 6.8
  hitStunDuration: 0.18
  detectionRange: 11
  attackRange: 4.8
  runSpeed: 4.2
  patrolSpeed: 2
  patrolDistance: 4.5
  rushDashSpeed: 7.5
  rushDuration: 0.4
  attackCooldown: 2.2
  slashHitboxSize: {{x: 3.2, y: 2.8}}
  slashHitboxOffset: {{x: 0.9, y: 0}}
  enableParry: 1
  parryCountPerFive: 3
  parryDuration: 0.32
  bladeArcColor: {{r: 0.9, g: 0.25, b: 0.95, a: 0.95}}
  hitFlashColor: {{r: 1, g: 0.3, b: 0.3, a: 1}}
  droppedOrbsCount: 3
  idleSprites:
{idles_str}
  stanceSprites:
{stances_str}
  walkSprites:
{walks_str}
  attackSprites:
{attacks_str}
--- !u!114 &1000008
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1000001}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {HEALTH_SCRIPT_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::Health
  maxHealth: 110
"""
    with open(out_path, "w", encoding="utf-8") as f:
        f.write(content)
    make_meta(out_path + ".meta", prefab_guid, importer="PrefabImporter")

def generate_brute_prefab(out_path, prefab_guid, controller_guid, initial_sprite_guid, idles, walks, attacks):
    def format_sprite_list(guids):
        return "\n".join([f"  - {{fileID: 21300000, guid: {g}, type: 3}}" for g in guids])

    idles_str = format_sprite_list(idles)
    walks_str = format_sprite_list(walks)
    attacks_str = format_sprite_list(attacks)

    content = f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &2000001
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 2000002}}
  - component: {{fileID: 2000003}}
  - component: {{fileID: 2000004}}
  - component: {{fileID: 2000005}}
  - component: {{fileID: 2000006}}
  - component: {{fileID: 2000007}}
  - component: {{fileID: 2000008}}
  m_Layer: 0
  m_Name: StrawhatBruteMob
  m_TagString: enemy
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &2000002
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 2000001}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 0.42, y: 0.42, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!212 &2000003
SpriteRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 2000001}}
  m_Enabled: 1
  m_CastShadows: 0
  m_ReceiveShadows: 0
  m_DynamicOccludee: 1
  m_StaticShadowCaster: 0
  m_MotionVectors: 1
  m_LightProbeUsage: 1
  m_ReflectionProbeUsage: 1
  m_RayTracingMode: 0
  m_RayTraceProcedural: 0
  m_Materials:
  - {{fileID: 2100000, guid: {SPRITE_MAT_GUID}, type: 2}}
  m_StaticBatchInfo:
    firstSubMesh: 0
    subMeshCount: 0
  m_SortingLayerID: 0
  m_SortingLayer: 0
  m_SortingOrder: 5
  m_Sprite: {{fileID: 21300000, guid: {initial_sprite_guid}, type: 3}}
  m_Color: {{r: 1, g: 1, b: 1, a: 1}}
  m_FlipX: 0
  m_FlipY: 0
  m_DrawMode: 0
--- !u!95 &2000004
Animator:
  serializedVersion: 7
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 2000001}}
  m_Enabled: 1
  m_Avatar: {{fileID: 0}}
  m_Controller: {{fileID: 9100000, guid: {controller_guid}, type: 2}}
  m_CullingMode: 0
  m_UpdateMode: 0
  m_ApplyRootMotion: 0
  m_LinearVelocityBlending: 0
  m_StabilizeFeet: 0
  m_WarningMessage: 
  m_HasTransformHierarchy: 1
  m_AllowConstantClipSamplingOptimization: 1
  m_KeepAnimatorStateOnDisable: 0
  m_WriteDefaultValuesOnDisable: 0
--- !u!50 &2000005
Rigidbody2D:
  serializedVersion: 4
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 2000001}}
  m_BodyType: 0
  m_Simulated: 1
  m_UseFullKinematicContacts: 0
  m_UseAutoMass: 0
  m_Mass: 1
  m_LinearDrag: 0
  m_AngularDrag: 0.05
  m_GravityScale: 3.0
  m_Material: {{fileID: 0}}
  m_Interpolate: 0
  m_SleepingMode: 1
  m_CollisionDetection: 1
  m_Constraints: 4
--- !u!61 &2000006
BoxCollider2D:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 2000001}}
  m_Enabled: 1
  serializedVersion: 3
  m_Density: 1
  m_Material: {{fileID: 0}}
  m_IsTrigger: 0
  m_Offset: {{x: 0, y: 0}}
  m_Size: {{x: 2.6, y: 3.8}}
  m_EdgeRadius: 0
--- !u!114 &2000007
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 2000001}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {BRUTE_SCRIPT_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::StrawhatBruteAI
  currentState: 0
  maxHealth: 180
  slamDamage: 30
  playerKnockbackForce: 9
  hitStunDuration: 0.15
  detectionRange: 10
  attackRange: 3.8
  chaseSpeed: 3
  patrolSpeed: 1.6
  patrolDistance: 4.5
  attackCooldown: 2.8
  cleaveHitboxSize: {{x: 3.6, y: 3.4}}
  cleaveHitboxOffset: {{x: 1.2, y: -0.1}}
  shadowAuraColor: {{r: 0.02, g: 0.01, b: 0.03, a: 0.85}}
  tremorColor: {{r: 0.04, g: 0.02, b: 0.06, a: 0.95}}
  eyeGlowColor: {{r: 1, g: 0.2, b: 0.85, a: 1}}
  hitFlashColor: {{r: 1, g: 0.3, b: 0.3, a: 1}}
  droppedOrbsCount: 4
  idleSprites:
{idles_str}
  walkSprites:
{walks_str}
  attackSprites:
{attacks_str}
--- !u!114 &2000008
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 2000001}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {HEALTH_SCRIPT_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Assembly-CSharp::Health
  maxHealth: 180
"""
    with open(out_path, "w", encoding="utf-8") as f:
        f.write(content)
    make_meta(out_path + ".meta", prefab_guid, importer="PrefabImporter")

def main():
    print("Ensuring script metas...")
    make_meta(os.path.join(BASE_DIR, r"Assets\Scenes\scripts\Enemies\StrawhatSwordAI.cs.meta"), SWORD_SCRIPT_GUID, "MonoImporter")
    make_meta(os.path.join(BASE_DIR, r"Assets\Scenes\scripts\Enemies\StrawhatBruteAI.cs.meta"), BRUTE_SCRIPT_GUID, "MonoImporter")
    make_meta(os.path.join(BASE_DIR, r"Assets\Scenes\scripts\Editor\StrawhatClanMobsSetupTool.cs.meta"), SETUP_SCRIPT_GUID, "MonoImporter")

    print("Configuring sprite frames and extracting GUIDs...")
    sword_idles = get_folder_sprites(SWORD_IDLE_DIR)
    sword_stances = get_folder_sprites(SWORD_STANCE_DIR)
    sword_walks = get_folder_sprites(SWORD_WALK_DIR)
    sword_attacks = get_folder_sprites(SWORD_ATTACK_DIR)

    brute_idles = get_folder_sprites(BRUTE_IDLE_DIR)
    brute_walks = get_folder_sprites(BRUTE_WALK_DIR)
    brute_attacks = get_folder_sprites(BRUTE_ATTACK_DIR)

    print(f"Sword sprites: {len(sword_idles)} idle, {len(sword_stances)} stance, {len(sword_walks)} walk, {len(sword_attacks)} attack")
    print(f"Brute sprites: {len(brute_idles)} idle, {len(brute_walks)} walk, {len(brute_attacks)} attack")

    # Fixed deterministic GUIDs for animations, controllers, prefabs
    SWORD_IDLE_CLIP_GUID = "a1100001111122223333444455556661"
    SWORD_STANCE_CLIP_GUID = "a1100001111122223333444455556662"
    SWORD_WALK_CLIP_GUID = "a1100001111122223333444455556663"
    SWORD_ATTACK_CLIP_GUID = "a1100001111122223333444455556664"
    SWORD_CONTROLLER_GUID = "c1100001111122223333444455556661"
    SWORD_PREFAB_GUID = "p1100001111122223333444455556661"

    BRUTE_IDLE_CLIP_GUID = "b2200002222233334444555566667771"
    BRUTE_WALK_CLIP_GUID = "b2200002222233334444555566667772"
    BRUTE_ATTACK_CLIP_GUID = "b2200002222233334444555566667773"
    BRUTE_CONTROLLER_GUID = "c2200002222233334444555566667772"
    BRUTE_PREFAB_GUID = "p2200002222233334444555566667772"

    print("Generating Sword mob animation clips...")
    generate_anim_clip("StrawhatSwordIdle", sword_idles, 12, True, os.path.join(ANIM_DIR, "StrawhatSwordIdle.anim"), SWORD_IDLE_CLIP_GUID)
    generate_anim_clip("StrawhatSwordStance", sword_stances, 14, True, os.path.join(ANIM_DIR, "StrawhatSwordStance.anim"), SWORD_STANCE_CLIP_GUID)
    generate_anim_clip("StrawhatSwordWalk", sword_walks, 14, True, os.path.join(ANIM_DIR, "StrawhatSwordWalk.anim"), SWORD_WALK_CLIP_GUID)
    generate_anim_clip("StrawhatSwordAttack", sword_attacks, 18, False, os.path.join(ANIM_DIR, "StrawhatSwordAttack.anim"), SWORD_ATTACK_CLIP_GUID)

    print("Generating Sword AnimatorController...")
    generate_sword_controller(os.path.join(ANIM_DIR, "StrawhatSwordController.controller"), SWORD_CONTROLLER_GUID,
                              SWORD_IDLE_CLIP_GUID, SWORD_STANCE_CLIP_GUID, SWORD_WALK_CLIP_GUID, SWORD_ATTACK_CLIP_GUID)

    print("Generating Brute mob animation clips...")
    generate_anim_clip("StrawhatBruteIdle", brute_idles, 12, True, os.path.join(ANIM_DIR, "StrawhatBruteIdle.anim"), BRUTE_IDLE_CLIP_GUID)
    generate_anim_clip("StrawhatBruteWalk", brute_walks, 12, True, os.path.join(ANIM_DIR, "StrawhatBruteWalk.anim"), BRUTE_WALK_CLIP_GUID)
    generate_anim_clip("StrawhatBruteAttack", brute_attacks, 14, False, os.path.join(ANIM_DIR, "StrawhatBruteAttack.anim"), BRUTE_ATTACK_CLIP_GUID)

    print("Generating Brute AnimatorController...")
    generate_brute_controller(os.path.join(ANIM_DIR, "StrawhatBruteController.controller"), BRUTE_CONTROLLER_GUID,
                              BRUTE_IDLE_CLIP_GUID, BRUTE_WALK_CLIP_GUID, BRUTE_ATTACK_CLIP_GUID)

    print("Generating Prefabs...")
    generate_sword_prefab(os.path.join(PREFAB_DIR, "StrawhatSwordMob.prefab"), SWORD_PREFAB_GUID, SWORD_CONTROLLER_GUID, sword_idles[0],
                          sword_idles, sword_stances, sword_walks, sword_attacks)
    generate_sword_prefab(os.path.join(RESOURCES_PREFAB_DIR, "StrawhatSwordMob.prefab"), SWORD_PREFAB_GUID, SWORD_CONTROLLER_GUID, sword_idles[0],
                          sword_idles, sword_stances, sword_walks, sword_attacks)

    generate_brute_prefab(os.path.join(PREFAB_DIR, "StrawhatBruteMob.prefab"), BRUTE_PREFAB_GUID, BRUTE_CONTROLLER_GUID, brute_idles[0],
                          brute_idles, brute_walks, brute_attacks)
    generate_brute_prefab(os.path.join(RESOURCES_PREFAB_DIR, "StrawhatBruteMob.prefab"), BRUTE_PREFAB_GUID, BRUTE_CONTROLLER_GUID, brute_idles[0],
                          brute_idles, brute_walks, brute_attacks)

    print("SUCCESS: All Strawhat Clan Mobs built and baked completely!")

if __name__ == "__main__":
    main()
