using System.Collections;
using UnityEngine;

namespace SpawnOfChaos.Systems
{
    /// <summary>
    /// TelepathyOrbItem - Interactive world collectible orb for the Telepathy Orb.
    /// Pulses with ethereal violet emission, floats gently with sine wave motion,
    /// and grants the player universal understanding of the Japanese-speaking clan NPCs upon contact or [E] interaction.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class TelepathyOrbItem : MonoBehaviour
    {
        [Header("Floating Animation")]
        public float hoverAmplitude = 0.25f;
        public float hoverFrequency = 2.5f;

        [Header("Interaction")]
        [Tooltip("If true, requires pressing [E] when nearby. If false, collects on trigger contact.")]
        public bool requireKeyPress = false;
        public KeyCode interactKey = KeyCode.E;

        [Header("FX")]
        public ParticleSystem collectParticles;

        private Vector3 startPos;
        private bool isCollected = false;
        private bool isPlayerNearby = false;

        private void Start()
        {
            startPos = transform.position;
            GetComponent<Collider2D>().isTrigger = true;

            // If already collected in this save, destroy or deactivate
            if (TelepathyOrbSystem.HasTelepathyOrb)
            {
                gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (isCollected) return;

            // Gentle vertical hover
            float offsetY = Mathf.Sin(Time.time * hoverFrequency) * hoverAmplitude;
            transform.position = startPos + new Vector3(0f, offsetY, 0f);

            // If requires key press
            if (requireKeyPress && isPlayerNearby && Input.GetKeyDown(interactKey))
            {
                Collect();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (isCollected) return;

            if (other.CompareTag("Player") || other.name.Contains("Player"))
            {
                isPlayerNearby = true;
                if (!requireKeyPress)
                {
                    Collect();
                }
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Player") || other.name.Contains("Player"))
            {
                isPlayerNearby = false;
            }
        }

        private void Collect()
        {
            if (isCollected) return;
            isCollected = true;

            TelepathyOrbSystem.EnsureExists();
            if (TelepathyOrbSystem.Instance != null)
            {
                TelepathyOrbSystem.Instance.AcquireTelepathyOrb();
            }

            if (collectParticles != null)
            {
                collectParticles.transform.SetParent(null);
                collectParticles.Play();
                Destroy(collectParticles.gameObject, 3f);
            }

            StartCoroutine(DisappearRoutine());
        }

        private IEnumerator DisappearRoutine()
        {
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            float elapsed = 0f;
            Vector3 origScale = transform.localScale;

            while (elapsed < 0.35f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / 0.35f;
                transform.localScale = Vector3.Lerp(origScale, origScale * 1.6f, t);
                if (sr != null)
                {
                    sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 1f - t);
                }
                yield return null;
            }

            gameObject.SetActive(false);
        }
    }
}
