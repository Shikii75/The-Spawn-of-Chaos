using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpawnOfChaos.Props
{
    /// <summary>
    /// PurpleWater2D - Interactive 2D Spring-Mesh Purple Water System.
    /// Simulates real-time Hooke's Law spring node surface wave physics, velocity-based wake trails,
    /// entry/exit splash impulses, concentric pink glow rings, and fluid buoyancy drag on submerged entities.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class PurpleWater2D : MonoBehaviour
    {
        [Header("Water Body Dimensions")]
        public float width = 12f;
        public float depth = 4f;
        [Range(10, 120)]
        public int nodeCount = 50;

        [Header("Spring Surface Physics")]
        public float stiffness = 0.025f;
        public float damping = 0.07f;
        public float spread = 0.04f;

        [Header("Interaction & VFX")]
        public float splashImpulseMultiplier = 0.25f;
        public float wakeRippleMultiplier = 0.05f;

        [Header("Buoyancy & Fluid Drag")]
        public float buoyancyFactor = 14f;
        public float fluidLinearDamping = 3.5f;
        public float fluidAngularDamping = 2f;

        [Header("Color Palette")]
        public Color topSurfaceColor = new Color(0.85f, 0.07f, 0.49f, 0.9f); // Magenta #D8117E
        public Color bottomDeepColor = new Color(0.14f, 0.02f, 0.22f, 0.95f); // Deep Void #240438
        public Color foamGlowColor = new Color(0.88f, 0.25f, 0.98f, 1.0f); // Foam #E040FB

        public struct SpringNode
        {
            public float y;
            public float velocity;
            public float targetY;
        }

        private SpringNode[] nodes;
        private Mesh mesh;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private BoxCollider2D boxCollider;
        private Material waterMaterial;

        // Tracks submerged rigidbodies to restore original damping on exit
        private Dictionary<Rigidbody2D, float> originalLinearDampings = new Dictionary<Rigidbody2D, float>();
        private Dictionary<Rigidbody2D, float> originalAngularDampings = new Dictionary<Rigidbody2D, float>();

        void Awake()
        {
            EnsureComponents();
            SetupMaterial();
            GenerateMeshAndNodes();
            SetupBoxCollider();
        }

        private void EnsureComponents()
        {
            if (meshFilter == null) meshFilter = GetComponent<MeshFilter>() ?? gameObject.AddComponent<MeshFilter>();
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>() ?? gameObject.AddComponent<MeshRenderer>();
            if (boxCollider == null) boxCollider = GetComponent<BoxCollider2D>() ?? gameObject.AddComponent<BoxCollider2D>();
        }

        private void SetupMaterial()
        {
            EnsureComponents();
            Shader shader = Shader.Find("Sprites/RealisticPurpleWater");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            waterMaterial = new Material(shader);
            waterMaterial.SetColor("_TopColor", topSurfaceColor);
            waterMaterial.SetColor("_BottomColor", bottomDeepColor);
            waterMaterial.SetColor("_FoamColor", foamGlowColor);
            waterMaterial.SetFloat("_FoamHeight", 0.05f);

            meshRenderer.material = waterMaterial;
            meshRenderer.sortingLayerName = "Default";
            meshRenderer.sortingOrder = 5;
        }

        public void GenerateMeshAndNodes()
        {
            EnsureComponents();
            nodes = new SpringNode[nodeCount];
            for (int i = 0; i < nodeCount; i++)
            {
                nodes[i] = new SpringNode { y = 0f, velocity = 0f, targetY = 0f };
            }

            mesh = new Mesh();
            mesh.name = "PurpleWaterMesh";

            Vector3[] vertices = new Vector3[nodeCount * 2];
            Vector2[] uvs = new Vector2[nodeCount * 2];
            int[] triangles = new int[(nodeCount - 1) * 6];

            float dx = width / (nodeCount - 1);
            float startX = -width * 0.5f;

            for (int i = 0; i < nodeCount; i++)
            {
                float x = startX + i * dx;
                float u = (float)i / (nodeCount - 1);

                // Top surface vertex
                vertices[i] = new Vector3(x, 0f, 0f);
                uvs[i] = new Vector2(u, 1f);

                // Bottom vertex
                vertices[i + nodeCount] = new Vector3(x, -depth, 0f);
                uvs[i + nodeCount] = new Vector2(u, 0f);
            }

            int t = 0;
            for (int i = 0; i < nodeCount - 1; i++)
            {
                int topL = i;
                int topR = i + 1;
                int botL = i + nodeCount;
                int botR = i + nodeCount + 1;

                triangles[t++] = topL;
                triangles[t++] = topR;
                triangles[t++] = botL;

                triangles[t++] = topR;
                triangles[t++] = botR;
                triangles[t++] = botL;
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();

            meshFilter.mesh = mesh;
        }

        private void SetupBoxCollider()
        {
            EnsureComponents();
            if (boxCollider != null)
            {
                boxCollider.isTrigger = true;
                boxCollider.size = new Vector2(width, depth);
                boxCollider.offset = new Vector2(0f, -depth * 0.5f);
            }
        }

        void Update()
        {
            UpdateSpringNodes(Time.deltaTime);
            UpdateMeshVertices();
        }

        private float animTime = 0f;

        private void UpdateSpringNodes(float deltaTime)
        {
            if (nodes == null || nodes.Length == 0) return;
            animTime += deltaTime;

            // 1. Hooke's Law Spring Update with Multi-Harmonic Swells
            for (int i = 0; i < nodeCount; i++)
            {
                // Multi-frequency natural water swells
                float ambientSwell = Mathf.Sin(animTime * 2.2f + i * 0.15f) * 0.08f 
                                   + Mathf.Sin(animTime * 3.6f - i * 0.09f) * 0.04f 
                                   + Mathf.Cos(animTime * 1.4f + i * 0.05f) * 0.03f;

                nodes[i].targetY = ambientSwell;

                float dy = nodes[i].y - nodes[i].targetY;
                float accel = -stiffness * dy - damping * nodes[i].velocity;
                nodes[i].velocity += accel;
                nodes[i].velocity = Mathf.Clamp(nodes[i].velocity, -0.25f, 0.25f);
                nodes[i].y += nodes[i].velocity;
                nodes[i].y = Mathf.Clamp(nodes[i].y, -0.4f, 0.4f);
            }

            // 2. Wave Propagation Spread Pass (8 iterations for smooth liquid propagation)
            float[] leftDeltas = new float[nodeCount];
            float[] rightDeltas = new float[nodeCount];

            for (int pass = 0; pass < 8; pass++)
            {
                for (int i = 0; i < nodeCount; i++)
                {
                    if (i > 0)
                    {
                        leftDeltas[i] = spread * (nodes[i].y - nodes[i - 1].y);
                        nodes[i - 1].velocity += leftDeltas[i];
                    }
                    if (i < nodeCount - 1)
                    {
                        rightDeltas[i] = spread * (nodes[i].y - nodes[i + 1].y);
                        nodes[i + 1].velocity += rightDeltas[i];
                    }
                }

                for (int i = 0; i < nodeCount; i++)
                {
                    if (i > 0) nodes[i - 1].y += leftDeltas[i];
                    if (i < nodeCount - 1) nodes[i + 1].y += rightDeltas[i];
                }
            }
        }

        private void UpdateMeshVertices()
        {
            if (mesh == null || nodes == null) return;

            Vector3[] vertices = mesh.vertices;

            // Apply Catmull-Rom Spline Curve Smoothing across adjacent spring nodes
            for (int i = 0; i < nodeCount; i++)
            {
                int p0 = Mathf.Clamp(i - 1, 0, nodeCount - 1);
                int p1 = i;
                int p2 = Mathf.Clamp(i + 1, 0, nodeCount - 1);
                int p3 = Mathf.Clamp(i + 2, 0, nodeCount - 1);

                // Smooth cubic spline filtering
                float smoothY = 0.5f * (
                    (2f * nodes[p1].y) +
                    (-nodes[p0].y + nodes[p2].y) * 0.5f +
                    (2f * nodes[p0].y - 5f * nodes[p1].y + 4f * nodes[p2].y - nodes[p3].y) * 0.25f
                );

                vertices[i].y = smoothY;
            }

            mesh.vertices = vertices;
            mesh.RecalculateBounds();
        }

        public void SplashAtWorldPosition(Vector3 worldPos, float impulse)
        {
            float clampedImpulse = Mathf.Clamp(impulse, -0.3f, 0.3f);
            int index = WorldPositionToNodeIndex(worldPos);
            if (index >= 0 && index < nodeCount)
            {
                nodes[index].velocity += clampedImpulse;

                // Splash nearby nodes proportionally
                if (index > 0) nodes[index - 1].velocity += clampedImpulse * 0.4f;
                if (index < nodeCount - 1) nodes[index + 1].velocity += clampedImpulse * 0.4f;
            }
        }

        private int WorldPositionToNodeIndex(Vector3 worldPos)
        {
            float localX = transform.InverseTransformPoint(worldPos).x;
            float normalizedX = Mathf.InverseLerp(-width * 0.5f, width * 0.5f, localX);
            return Mathf.Clamp(Mathf.RoundToInt(normalizedX * (nodeCount - 1)), 0, nodeCount - 1);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            Rigidbody2D rb = other.attachedRigidbody;
            if (rb == null) return;

            float impactSpeed = Mathf.Max(rb.linearVelocity.magnitude, 3.5f);
            float vy = rb.linearVelocity.y != 0 ? rb.linearVelocity.y : -3.5f;
            SplashAtWorldPosition(other.transform.position, vy * splashImpulseMultiplier);

            // Trigger Purple Droplet VFX & Concentric Glow Rings
            Vector3 splashPos = new Vector3(other.transform.position.x, transform.position.y, transform.position.z);
            PurpleWaterSplashFX.SpawnSplash(splashPos, impactSpeed, topSurfaceColor, foamGlowColor);

            // Store original drag values
            if (!originalLinearDampings.ContainsKey(rb))
            {
                originalLinearDampings[rb] = rb.linearDamping;
                originalAngularDampings[rb] = rb.angularDamping;
            }

            rb.linearDamping = fluidLinearDamping;
            rb.angularDamping = fluidAngularDamping;
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            Rigidbody2D rb = other.attachedRigidbody;
            if (rb == null) return;

            // Velocity-based wake trail ripples (gentle, subtle ripple)
            if (Mathf.Abs(rb.linearVelocity.x) > 0.1f)
            {
                float wakeImpulse = -Mathf.Sign(rb.linearVelocity.x) * Mathf.Min(Mathf.Abs(rb.linearVelocity.x), 6f) * wakeRippleMultiplier;
                SplashAtWorldPosition(other.transform.position, wakeImpulse * Time.deltaTime * 3f);
            }

            // Upward Buoyancy Force
            float waterSurfaceY = transform.position.y;
            float subDepth = Mathf.Clamp(waterSurfaceY - rb.position.y, 0f, depth);
            if (subDepth > 0f)
            {
                float buoyancy = subDepth * buoyancyFactor;
                rb.AddForce(Vector2.up * buoyancy, ForceMode2D.Force);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            Rigidbody2D rb = other.attachedRigidbody;
            if (rb == null) return;

            float impactSpeed = Mathf.Max(rb.linearVelocity.magnitude, 3.5f);
            float vy = rb.linearVelocity.y != 0 ? rb.linearVelocity.y : 3.5f;
            SplashAtWorldPosition(other.transform.position, vy * splashImpulseMultiplier);

            Vector3 exitSplashPos = new Vector3(other.transform.position.x, transform.position.y, transform.position.z);
            PurpleWaterSplashFX.SpawnSplash(exitSplashPos, impactSpeed, topSurfaceColor, foamGlowColor);

            // Restore original drag values
            if (originalLinearDampings.TryGetValue(rb, out float origLinear))
            {
                rb.linearDamping = origLinear;
                originalLinearDampings.Remove(rb);
            }
            if (originalAngularDampings.TryGetValue(rb, out float origAngular))
            {
                rb.angularDamping = origAngular;
                originalAngularDampings.Remove(rb);
            }
        }
    }
}
