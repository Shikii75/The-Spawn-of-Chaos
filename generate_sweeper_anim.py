import os

lines = []
with open(r"c:\Users\tyram\The Spawn of Chaos\sweeper_frames_info.txt", "r", encoding="utf-8") as f:
    for line in f:
        line = line.strip()
        if not line: continue
        # frame_001.png: guid=2fb6f5a6d965e51499546e9e7ee50037, id=8115644005664255767
        parts = line.split(", ")
        guid = parts[0].split("guid=")[1]
        fid = parts[1].split("id=")[1]
        lines.append((guid, fid))

total_frames = len(lines)
sample_rate = 14
frame_delay = 1.0 / sample_rate

yaml_lines = [
    "%YAML 1.1",
    "%TAG !u! tag:unity3d.com,2011:",
    "--- !u!74 &7400000",
    "AnimationClip:",
    "  m_ObjectHideFlags: 0",
    "  m_CorrespondingSourceObject: {fileID: 0}",
    "  m_PrefabInstance: {fileID: 0}",
    "  m_PrefabAsset: {fileID: 0}",
    "  m_Name: SweeperVillagerSweep",
    "  serializedVersion: 7",
    "  m_Legacy: 0",
    "  m_Compressed: 0",
    "  m_UseHighQualityCurve: 1",
    "  m_RotationCurves: []",
    "  m_CompressedRotationCurves: []",
    "  m_EulerCurves: []",
    "  m_PositionCurves: []",
    "  m_ScaleCurves: []",
    "  m_FloatCurves: []",
    "  m_PPtrCurves:",
    "  - serializedVersion: 2",
    "    curve:"
]

for i, (guid, fid) in enumerate(lines):
    t = round(i * frame_delay, 4)
    yaml_lines.append(f"    - time: {t}")
    yaml_lines.append(f"      value: {{fileID: {fid}, guid: {guid}, type: 3}}")

yaml_lines.extend([
    "    attribute: m_Sprite",
    "    path: ",
    "    classID: 212",
    "    script: {fileID: 0}",
    "    flags: 2",
    f"  m_SampleRate: {sample_rate}",
    "  m_WrapMode: 0",
    "  m_Bounds:",
    "    m_Center: {x: 0, y: 0, z: 0}",
    "    m_Extent: {x: 0, y: 0, z: 0}",
    "  m_ClipBindingConstant:",
    "    genericBindings:",
    "    - serializedVersion: 2",
    "      path: 0",
    "      attribute: 0",
    "      script: {fileID: 0}",
    "      typeID: 212",
    "      customType: 23",
    "      isPPtrCurve: 1",
    "      isIntCurve: 0",
    "      isSerializeReferenceCurve: 0",
    "    pptrCurveMapping:"
])

for guid, fid in lines:
    yaml_lines.append(f"    - {{fileID: {fid}, guid: {guid}, type: 3}}")

stop_time = round(total_frames * frame_delay, 4)
yaml_lines.extend([
    "  m_AnimationClipSettings:",
    "    serializedVersion: 2",
    "    m_AdditiveReferencePoseClip: {fileID: 0}",
    "    m_AdditiveReferencePoseTime: 0",
    "    m_StartTime: 0",
    f"    m_StopTime: {stop_time}",
    "    m_OrientationOffsetY: 0",
    "    m_Level: 0",
    "    m_CycleOffset: 0",
    "    m_HasAdditiveReferencePose: 0",
    "    m_LoopTime: 1",
    "    m_LoopBlend: 0",
    "    m_LoopBlendOrientation: 0",
    "    m_LoopBlendPositionY: 0",
    "    m_LoopBlendPositionXZ: 0",
    "    m_KeepOriginalOrientation: 0",
    "    m_KeepOriginalPositionY: 1",
    "    m_KeepOriginalPositionXZ: 0",
    "    m_HeightFromFeet: 0",
    "    m_Mirror: 0",
    "  m_EditorCurves: []",
    "  m_EulerEditorCurves: []",
    "  m_HasGenericRootTransform: 0",
    "  m_HasMotionFloatCurves: 0",
    "  m_Events: []"
])

anim_content = "\n".join(yaml_lines) + "\n"
out_anim = r"c:\Users\tyram\The Spawn of Chaos\Assets\Scenes\animations\animators\SweeperVillagerSweep.anim"
with open(out_anim, "w", encoding="utf-8") as f:
    f.write(anim_content)

print("Generated SweeperVillagerSweep.anim successfully!")
