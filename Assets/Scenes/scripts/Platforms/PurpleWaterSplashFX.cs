using UnityEngine;

namespace SpawnOfChaos.Props
{
    /// <summary>
    /// PurpleWaterSplashFX - High-performance Unity ParticleSystem VFX engine for purple water.
    /// Spawns vibrant purple droplet bursts, glowing pink splash rings (#E040FB), and surface foam sparkles.
    /// </summary>
    public static class PurpleWaterSplashFX
    {
        public static void SpawnSplash(Vector3 position, float impactVelocity, Color baseColor, Color glowRingColor)
        {
            float intensity = Mathf.Clamp(Mathf.Abs(impactVelocity) / 4f, 0.6f, 2.5f);

            // 1. Spawn Deep Purple Droplets Particle Burst
            SpawnDropletParticles(position, baseColor, intensity);

            // 2. Spawn Expanding Concentric Pink Glow Ring Particle Burst
            SpawnGlowRingParticles(position, glowRingColor, intensity);
        }

        private static void SpawnDropletParticles(Vector3 position, Color dropletColor, float intensity)
        {
            GameObject splashObj = new GameObject("PurpleWater_DropletSplash");
            splashObj.transform.position = position + Vector3.up * 0.1f;

            ParticleSystem ps = splashObj.AddComponent<ParticleSystem>();
            ParticleSystemRenderer psr = splashObj.GetComponent<ParticleSystemRenderer>();

            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f * intensity, 6f * intensity);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.35f);
            main.startColor = dropletColor;
            main.gravityModifier = 1.8f;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            int count = Mathf.RoundToInt(16 * intensity);
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, count) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.3f;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0f, 1f);
            curve.AddKey(1f, 0f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

            psr.material = new Material(Shader.Find("Sprites/Default"));
            psr.sortingLayerName = "Default";
            psr.sortingOrder = 15;

            ps.Play();
            Object.Destroy(splashObj, 1.5f);
        }

        private static void SpawnGlowRingParticles(Vector3 position, Color ringColor, float intensity)
        {
            GameObject ringObj = new GameObject("PurpleWater_GlowRingVFX");
            ringObj.transform.position = position + Vector3.up * 0.05f;

            ParticleSystem ps = ringObj.AddComponent<ParticleSystem>();
            ParticleSystemRenderer psr = ringObj.GetComponent<ParticleSystemRenderer>();

            var main = ps.main;
            main.duration = 0.4f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f * intensity, 1.8f * intensity);
            main.startColor = new ParticleSystem.MinMaxGradient(ringColor, Color.white);
            main.gravityModifier = 0f;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 6) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.2f;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 0.3f);
            sizeCurve.AddKey(1f, 1.8f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            psr.material = new Material(Shader.Find("Sprites/Default"));
            psr.sortingLayerName = "Default";
            psr.sortingOrder = 14;

            ps.Play();
            Object.Destroy(ringObj, 1.2f);
        }
    }
}
