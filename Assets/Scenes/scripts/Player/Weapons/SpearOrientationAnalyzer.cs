using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SpawnOfChaos.Weapons
{
    /// <summary>
    /// SpearOrientationAnalyzer:
    /// Analyzes spear and weapon PNG sprites to identify:
    /// 1. The long parts of the weapon (Principal Axis / Primary Long Side).
    /// 2. Which of both ends along the long side is the sharp TIP ("tio") vs the handle/butt.
    /// 3. The exact angle offset needed so that ANY spear sprite points its tip directly at the mouse.
    /// </summary>
    public static class SpearOrientationAnalyzer
    {
        public struct SpearAnalysisResult
        {
            public string spriteName;
            public Vector2 centerPixel;
            public Vector2 tipPixel;
            public Vector2 buttPixel;
            public float longAxisAngleDeg;
            public float tipAngleDeg;       // Angle in degrees from center to tip in sprite space (+X = 0 deg)
            public float tipAngleOffset;    // Offset to subtract from target aim angle
            public float tipSharpnessRatio;
            public bool isTipIdentified;
        }

        // Fast cache for analyzed sprites to eliminate runtime GC or performance overhead
        private static readonly Dictionary<string, SpearAnalysisResult> analysisCache = new Dictionary<string, SpearAnalysisResult>(StringComparer.OrdinalIgnoreCase);

        // Verified baseline offsets for core weapons
        private static readonly Dictionary<string, float> fallbackOffsets = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
        {
            { "LumiSpear", 3.8f },
            { "DarkSpear", 45.3f },
            { "BloodBlade", -89.3f },
            { "DarkBladeSmall", -138.3f },
            { "DarkAxe", 47.8f },
            { "DarkDag", 44.1f }
        };

        /// <summary>
        /// Gets the required rotation offset for the given sprite so its tip accurately faces the aim direction.
        /// </summary>
        public static float GetTipAngleOffset(Sprite sprite)
        {
            if (sprite == null) return 0f;
            return GetAnalysis(sprite).tipAngleOffset;
        }

        /// <summary>
        /// Gets the required rotation offset for a weapon by name.
        /// </summary>
        public static float GetTipAngleOffset(string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName)) return 0f;
            if (analysisCache.TryGetValue(spriteName, out var res)) return res.tipAngleOffset;
            if (fallbackOffsets.TryGetValue(spriteName, out float offset)) return offset;
            return 0f;
        }

        /// <summary>
        /// Gets the full analysis result for the given sprite, computing it if not yet cached.
        /// </summary>
        public static SpearAnalysisResult GetAnalysis(Sprite sprite)
        {
            if (sprite == null)
            {
                return new SpearAnalysisResult { isTipIdentified = false };
            }

            string key = sprite.name;
            if (analysisCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            SpearAnalysisResult result = AnalyzeSpriteInternal(sprite);
            analysisCache[key] = result;
            return result;
        }

        /// <summary>
        /// Computes world position of the weapon's tip based on analyzed tip pixel and current transform.
        /// </summary>
        public static Vector3 GetTipWorldPosition(Transform weaponTransform, SpriteRenderer sr, float defaultLength = 1.4f)
        {
            if (weaponTransform == null) return Vector3.zero;

            if (sr != null && sr.sprite != null)
            {
                SpearAnalysisResult res = GetAnalysis(sr.sprite);
                if (res.isTipIdentified)
                {
                    Vector2 pivot = sr.sprite.pivot;
                    Vector2 tipLocalPix = res.tipPixel - pivot;
                    float ppu = sr.sprite.pixelsPerUnit > 0 ? sr.sprite.pixelsPerUnit : 100f;
                    Vector3 localTip = new Vector3(tipLocalPix.x / ppu, tipLocalPix.y / ppu, 0f);
                    return weaponTransform.TransformPoint(localTip);
                }
            }

            // Fallback: project forward along current weapon rotation
            return weaponTransform.position + weaponTransform.right * defaultLength;
        }

        private static SpearAnalysisResult AnalyzeSpriteInternal(Sprite sprite)
        {
            SpearAnalysisResult result = new SpearAnalysisResult
            {
                spriteName = sprite.name,
                isTipIdentified = false
            };

            Texture2D tex = sprite.texture;
            Color32[] pixels = null;
            int width = 0;
            int height = 0;

            // Try reading texture pixels directly
            if (tex != null && tex.isReadable)
            {
                Rect r = sprite.rect;
                width = Mathf.RoundToInt(r.width);
                height = Mathf.RoundToInt(r.height);
                int startX = Mathf.RoundToInt(r.x);
                int startY = Mathf.RoundToInt(r.y);
                pixels = tex.GetPixels32();
                if (pixels != null && (startX != 0 || startY != 0 || width != tex.width || height != tex.height))
                {
                    // Extract sprite sub-rect
                    Color32[] subPixels = new Color32[width * height];
                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            subPixels[y * width + x] = pixels[(startY + y) * tex.width + (startX + x)];
                        }
                    }
                    pixels = subPixels;
                }
            }

            // If not readable via Unity, attempt loading directly from PNG file if in project
            if (pixels == null)
            {
                pixels = TryLoadPixelsFromFile(sprite, out width, out height);
            }

            // If still unreadable, utilize verified fallback offsets
            if (pixels == null || pixels.Length == 0 || width <= 0 || height <= 0)
            {
                if (fallbackOffsets.TryGetValue(sprite.name, out float fbOffset))
                {
                    result.tipAngleOffset = fbOffset;
                    result.tipAngleDeg = fbOffset;
                    result.isTipIdentified = true;
                }
                return result;
            }

            // 1. Collect all non-transparent pixels (alpha > 25)
            List<Vector2> opaqueCoords = new List<Vector2>();
            double sumX = 0;
            double sumY = 0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (pixels[y * width + x].a > 25)
                    {
                        Vector2 pt = new Vector2(x, y);
                        opaqueCoords.Add(pt);
                        sumX += x;
                        sumY += y;
                    }
                }
            }

            int n = opaqueCoords.Count;
            if (n < 20)
            {
                if (fallbackOffsets.TryGetValue(sprite.name, out float fbOffset))
                {
                    result.tipAngleOffset = fbOffset;
                    result.tipAngleDeg = fbOffset;
                    result.isTipIdentified = true;
                }
                return result;
            }

            float cx = (float)(sumX / n);
            float cy = (float)(sumY / n);
            result.centerPixel = new Vector2(cx, cy);

            // 2. Compute 2x2 Covariance Matrix to find Principal Long Axis
            double covXX = 0;
            double covYY = 0;
            double covXY = 0;

            for (int i = 0; i < n; i++)
            {
                double dx = opaqueCoords[i].x - cx;
                double dy = opaqueCoords[i].y - cy;
                covXX += dx * dx;
                covYY += dy * dy;
                covXY += dx * dy;
            }
            covXX /= n;
            covYY /= n;
            covXY /= n;

            // Long axis orientation angle theta: 0.5 * atan2(2 * covXY, covXX - covYY)
            float theta = 0.5f * Mathf.Atan2((float)(2.0 * covXY), (float)(covXX - covYY));
            Vector2 axisDir = new Vector2(Mathf.Cos(theta), Mathf.Sin(theta));
            Vector2 perpDir = new Vector2(-axisDir.y, axisDir.x);

            result.longAxisAngleDeg = theta * Mathf.Rad2Deg;

            // 3. Project all pixels onto Long Axis to find extreme End A and End B
            float minProj = float.MaxValue;
            float maxProj = float.MinValue;
            Vector2 apexA = Vector2.zero;
            Vector2 apexB = Vector2.zero;

            float[] projections = new float[n];
            float[] perpProjections = new float[n];

            for (int i = 0; i < n; i++)
            {
                Vector2 diff = opaqueCoords[i] - result.centerPixel;
                float p = Vector2.Dot(diff, axisDir);
                float perpP = Vector2.Dot(diff, perpDir);
                projections[i] = p;
                perpProjections[i] = perpP;

                if (p > maxProj)
                {
                    maxProj = p;
                    apexA = opaqueCoords[i];
                }
                if (p < minProj)
                {
                    minProj = p;
                    apexB = opaqueCoords[i];
                }
            }

            float totalLength = Mathf.Max(1f, maxProj - minProj);

            // 4. Determine which of both ends of the long side is the TIP
            // Criteria: A spear tip has a sharp apex with small cross-sectional width,
            // expanding rapidly into the blade body (high ratio of 20% area to 3% area).
            // Conversely, a shaft/handle has uniform width or blunt pommel.
            int sliceA3 = 0, sliceA20 = 0;
            int sliceB3 = 0, sliceB20 = 0;
            float minPerpA3 = float.MaxValue, maxPerpA3 = float.MinValue;
            float minPerpB3 = float.MaxValue, maxPerpB3 = float.MinValue;

            for (int i = 0; i < n; i++)
            {
                float normP = (projections[i] - minProj) / totalLength; // 0 at B, 1 at A
                float perpP = perpProjections[i];

                if (normP > 0.97f)
                {
                    sliceA3++;
                    if (perpP < minPerpA3) minPerpA3 = perpP;
                    if (perpP > maxPerpA3) maxPerpA3 = perpP;
                }
                if (normP > 0.80f) sliceA20++;

                if (normP < 0.03f)
                {
                    sliceB3++;
                    if (perpP < minPerpB3) minPerpB3 = perpP;
                    if (perpP > maxPerpB3) maxPerpB3 = perpP;
                }
                if (normP < 0.20f) sliceB20++;
            }

            float widthA3 = (sliceA3 > 0) ? (maxPerpA3 - minPerpA3) : 999f;
            float widthB3 = (sliceB3 > 0) ? (maxPerpB3 - minPerpB3) : 999f;

            float ratioA = (float)sliceA20 / Mathf.Max(1, sliceA3);
            float ratioB = (float)sliceB20 / Mathf.Max(1, sliceB3);

            float scoreA = ratioA / Mathf.Max(1f, widthA3);
            float scoreB = ratioB / Mathf.Max(1f, widthB3);

            bool isTipA = scoreA >= scoreB;

            Vector2 tipApex = isTipA ? apexA : apexB;
            Vector2 buttApex = isTipA ? apexB : apexA;

            Vector2 tipVector = tipApex - result.centerPixel;
            float tipAngle = Mathf.Atan2(tipVector.y, tipVector.x) * Mathf.Rad2Deg;

            result.tipPixel = tipApex;
            result.buttPixel = buttApex;
            result.tipAngleDeg = tipAngle;
            result.tipAngleOffset = tipAngle; // Subtract this offset when aiming at mouse
            result.tipSharpnessRatio = isTipA ? scoreA : scoreB;
            result.isTipIdentified = true;

            Debug.Log($"<color=#33FFAA>[SpearOrientationAnalyzer] Analyzed '{sprite.name}': Long Axis={result.longAxisAngleDeg:F1}°, TIP Angle={result.tipAngleDeg:F1}° (Score A={scoreA:F2} vs B={scoreB:F2})</color>");

            return result;
        }

        private static Color32[] TryLoadPixelsFromFile(Sprite sprite, out int width, out int height)
        {
            width = 0;
            height = 0;

            #if UNITY_EDITOR
            try
            {
                string assetPath = UnityEditor.AssetDatabase.GetAssetPath(sprite);
                if (!string.IsNullOrEmpty(assetPath) && File.Exists(assetPath))
                {
                    byte[] bytes = File.ReadAllBytes(assetPath);
                    Texture2D loadedTex = new Texture2D(2, 2);
                    if (loadedTex.LoadImage(bytes))
                    {
                        width = loadedTex.width;
                        height = loadedTex.height;
                        return loadedTex.GetPixels32();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SpearOrientationAnalyzer] File read fallback failed for {sprite.name}: {ex.Message}");
            }
            #endif

            return null;
        }

        #if UNITY_EDITOR
        [UnityEditor.MenuItem("Tools/Weapons/Analyze All Spear PNGs")]
        public static void AnalyzeAllProjectWeaponsMenu()
        {
            analysisCache.Clear();
            string[] weaponPaths = new string[]
            {
                "Assets/Resources/LumiSpear.png",
                "Assets/Resources/Weapons/DarkSpear.png",
                "Assets/Resources/Weapons/BloodBlade.png",
                "Assets/Resources/Weapons/DarkBladeSmall.png",
                "Assets/Resources/Weapons/DarkAxe.png",
                "Assets/Resources/Weapons/DarkDag.png"
            };

            Debug.Log("================ SPEAR ORIENTATION ANALYSIS REPORT ================");
            foreach (var path in weaponPaths)
            {
                Sprite sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sp != null)
                {
                    var res = GetAnalysis(sp);
                    Debug.Log($"Weapon: {sp.name} => Long Axis: {res.longAxisAngleDeg:F1}°, Tip Angle: {res.tipAngleDeg:F1}°, Offset: {res.tipAngleOffset:F1}°");
                }
                else
                {
                    Debug.LogWarning($"Could not load weapon sprite at {path}");
                }
            }
            Debug.Log("===================================================================");
        }
        #endif
    }
}
