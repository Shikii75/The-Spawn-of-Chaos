import os
import re

dir_path = r"c:\Users\tyram\The Spawn of Chaos\Assets\Scenes\animations\frames\sweepervillager-525173f8"
meta_files = sorted([f for f in os.listdir(dir_path) if f.endswith(".png.meta")])

results = []
for f in meta_files:
    full_path = os.path.join(dir_path, f)
    with open(full_path, "r", encoding="utf-8") as fp:
        content = fp.read()
    
    guid_match = re.search(r"guid:\s*([a-f0-9]+)", content)
    guid = guid_match.group(1) if guid_match else "none"
    
    id_match = re.search(r"213:\s*(-?\d+)", content)
    internal_id = id_match.group(1) if id_match else "none"
    
    results.append(f"{f[:-5]}: guid={guid}, id={internal_id}")

with open(r"c:\Users\tyram\The Spawn of Chaos\sweeper_frames_info.txt", "w", encoding="utf-8") as out:
    out.write("\n".join(results))
print("DONE, wrote", len(results), "frames")
