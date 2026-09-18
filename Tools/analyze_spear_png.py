#!/usr/bin/env python3
"""
Spear / Weapon PNG Orientation & Tip Analyzer:
Identifies:
1. Long parts of the weapon PNG (Principal Long Axis using PCA / 2D Covariance analysis).
2. Which of both ends along the long side is the sharp TIP ("tio") vs the blunt handle/pommel.
3. The exact angle offset needed so that ANY spear sprite points its tip directly at the mouse.
"""

import sys, os
from PIL import Image
import numpy as np

def analyze_spear_png(path):
    if not os.path.exists(path):
        print(f"Error: File not found '{path}'")
        return None

    im = Image.open(path).convert("RGBA")
    W, H = im.size
    arr = np.array(im)
    alpha = arr[:, :, 3]

    y_idx, x_idx = np.where(alpha > 25)
    if len(x_idx) < 20:
        print(f"Error: Not enough opaque pixels in '{path}'")
        return None

    # Cartesian Coordinates (origin bottom-left, +X right, +Y up like in Unity)
    unity_y = H - 1 - y_idx
    cx = np.mean(x_idx)
    cy = np.mean(unity_y)

    # 1. 2D Covariance Matrix for Principal Long Axis
    dx = x_idx - cx
    dy = unity_y - cy
    cov_xx = np.mean(dx * dx)
    cov_yy = np.mean(dy * dy)
    cov_xy = np.mean(dx * dy)

    # Long axis angle theta
    theta = 0.5 * np.arctan2(2.0 * cov_xy, cov_xx - cov_yy)
    axis_dir = np.array([np.cos(theta), np.sin(theta)])
    perp_dir = np.array([-axis_dir[1], axis_dir[0]])

    # Project pixels onto long axis
    proj = dx * axis_dir[0] + dy * axis_dir[1]
    perp_proj = dx * perp_dir[0] + dy * perp_dir[1]

    p_min = np.min(proj)
    p_max = np.max(proj)
    total_length = p_max - p_min
    norm_proj = (proj - p_min) / total_length

    # Apex points for End A (+axis) and End B (-axis)
    idx_A = np.argmax(proj)
    apex_A = (int(x_idx[idx_A]), int(unity_y[idx_A]))
    idx_B = np.argmin(proj)
    apex_B = (int(x_idx[idx_B]), int(unity_y[idx_B]))

    # 2. Geometry & Sharpness Analysis of Both Ends
    mask_A3 = norm_proj > 0.97
    mask_A20 = norm_proj > 0.80
    mask_B3 = norm_proj < 0.03
    mask_B20 = norm_proj < 0.20

    slice_A3 = np.sum(mask_A3)
    slice_A20 = np.sum(mask_A20)
    slice_B3 = np.sum(mask_B3)
    slice_B20 = np.sum(mask_B20)

    width_A3 = np.ptp(perp_proj[mask_A3]) if np.any(mask_A3) else 999.0
    width_B3 = np.ptp(perp_proj[mask_B3]) if np.any(mask_B3) else 999.0

    ratio_A = slice_A20 / max(1, slice_A3)
    ratio_B = slice_B20 / max(1, slice_B3)

    # Tip has sharp apex (small width) and rapid blade expansion (high ratio)
    score_A = ratio_A / max(1.0, width_A3)
    score_B = ratio_B / max(1.0, width_B3)

    is_tip_A = score_A >= score_B

    tip_apex = apex_A if is_tip_A else apex_B
    butt_apex = apex_B if is_tip_A else apex_A

    tip_vector = (tip_apex[0] - cx, tip_apex[1] - cy)
    tip_angle_deg = np.degrees(np.arctan2(tip_vector[1], tip_vector[0]))

    return {
        "file": os.path.basename(path),
        "path": path,
        "dimensions": (W, H),
        "center": (round(float(cx), 1), round(float(cy), 1)),
        "long_axis_angle": round(float(theta * 180.0 / np.pi), 1),
        "tip_apex": tip_apex,
        "butt_apex": butt_apex,
        "tip_angle_deg": round(float(tip_angle_deg), 1),
        "tip_angle_offset": round(float(tip_angle_deg), 1),
        "tip_end_name": "End A (+Axis)" if is_tip_A else "End B (-Axis)",
        "score_A": round(float(score_A), 2),
        "score_B": round(float(score_B), 2)
    }

def print_analysis(res):
    if not res: return
    print(f"==================================================")
    print(f" SPEAR PNG ANALYSIS: {res['file']}")
    print(f"==================================================")
    print(f" Image Size:        {res['dimensions'][0]} x {res['dimensions'][1]} px")
    print(f" Centroid (cx, cy): {res['center']}")
    print(f" Long Axis Angle:   {res['long_axis_angle']}°")
    print(f" Identified TIP:    {res['tip_end_name']}")
    print(f" Tip Apex Coord:    {res['tip_apex']} (Unity origin bottom-left)")
    print(f" Handle/Butt Coord: {res['butt_apex']}")
    print(f" Tip Angle:         {res['tip_angle_deg']}°")
    print(f" Aim Rotation Offset: -{res['tip_angle_offset']}°")
    print(f" Sharpness Score:   End A={res['score_A']} vs End B={res['score_B']}")
    print(f"==================================================\n")

if __name__ == "__main__":
    if len(sys.argv) > 1:
        for p in sys.argv[1:]:
            res = analyze_spear_png(p)
            print_analysis(res)
    else:
        # Scan standard project weapons
        default_weapons = [
            r"Assets\Resources\LumiSpear.png",
            r"Assets\Resources\Weapons\DarkSpear.png",
            r"Assets\Resources\Weapons\BloodBlade.png",
            r"Assets\Resources\Weapons\DarkBladeSmall.png",
            r"Assets\Resources\Weapons\DarkAxe.png",
            r"Assets\Resources\Weapons\DarkDag.png"
        ]
        for w in default_weapons:
            if os.path.exists(w):
                res = analyze_spear_png(w)
                print_analysis(res)
