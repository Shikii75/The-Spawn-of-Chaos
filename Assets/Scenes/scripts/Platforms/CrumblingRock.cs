using System.Collections;
using UnityEngine;

namespace SpawnOfChaos.Platforms
{
    /// <summary>
    /// Attached to each rock sprite child in the Run Mechanic section.
    /// Provides ground collision for the player, telegraphs imminent collapse with a quick procedural shake,
    /// and drops with 2D physics gravity.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class CrumblingRock : MonoBehaviour
    {
        [Header("State")]
        [SerializeField] private bool isFalling = false;
        [SerializeField] private bool hasFallen = false;

        private Vector3 initialLocalPos;
        private Quaternion initialLocalRot;
        private Vector3 initialLocalScale;
        private Color initialColor;

        private SpriteRenderer sr;
        private Collider2D col;
        private Rigidbody2D rb;

        public bool IsFalling => isFalling;
        public bool HasFallen => hasFallen;
        public float WorldX => transform.position.x;

        private void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            initialLocalPos = transform.localPosition;
            initialLocalRot = transform.localRotation;
            initialLocalScale = transform.localScale;
            if (sr != null) initialColor = sr.color;

            EnsureComponents();
        }

        /// <summary>
        /// Ensures Ground tag, PolygonCollider2D (fitting exact sprite shape), and Rigidbody2D are configured.
        /// </summary>
        public void EnsureComponents()
        {
            if (gameObject.tag != "Ground")
            {
                gameObject.tag = "Ground";
            }

            // Remove any legacy BoxCollider2D on this rock
            BoxCollider2D legacyBox = GetComponent<BoxCollider2D>();
            if (legacyBox != null)
            {
                if (Application.isPlaying)
                    Destroy(legacyBox);
                else
                    DestroyImmediate(legacyBox);
            }

            PolygonCollider2D polyCol = GetComponent<PolygonCollider2D>();
            if (polyCol == null)
            {
                polyCol = gameObject.AddComponent<PolygonCollider2D>();
            }
            polyCol.enabled = true;
            col = polyCol;

            rb = GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody2D>();
            }
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.simulated = true;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.gravityScale = 2.5f;
        }

        /// <summary>
        /// Triggers the shake telegraph and subsequent fall.
        /// </summary>
        public void TriggerFall(float shakeDuration = 0.2f, float dropDelay = 0f)
        {
            if (isFalling || hasFallen) return;
            StartCoroutine(FallSequenceRoutine(shakeDuration, dropDelay));
        }

        private IEnumerator FallSequenceRoutine(float shakeDuration, float dropDelay)
        {
            isFalling = true;

            if (dropDelay > 0f)
            {
                yield return new WaitForSeconds(dropDelay);
            }

            // Phase 1: Procedural Shake / Vibration
            float elapsed = 0f;
            Vector3 originPos = transform.localPosition;
            while (elapsed < shakeDuration)
            {
                elapsed += Time.deltaTime;
                float intensity = 0.08f * (elapsed / shakeDuration);
                float offsetX = Random.Range(-intensity, intensity);
                float offsetY = Random.Range(-intensity, intensity);
                transform.localPosition = originPos + new Vector3(offsetX, offsetY, 0f);
                yield return null;
            }

            transform.localPosition = originPos;

            // Phase 2: Drop with physics & disable ground collider
            if (col != null) col.enabled = false;

            if (rb != null)
            {
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.gravityScale = 2.5f;
                rb.linearVelocity = new Vector2(Random.Range(-0.5f, 0.5f), -1f);
                rb.angularVelocity = Random.Range(-60f, 60f);
            }

            hasFallen = true;

            // Phase 3: Fade out sprite over 1.2 seconds, then deactivate
            if (sr != null)
            {
                float fadeTime = 1.2f;
                float fadeElapsed = 0f;
                Color startCol = sr.color;

                while (fadeElapsed < fadeTime)
                {
                    fadeElapsed += Time.deltaTime;
                    float alpha = Mathf.Lerp(startCol.a, 0f, fadeElapsed / fadeTime);
                    sr.color = new Color(startCol.r, startCol.g, startCol.b, alpha);
                    yield return null;
                }
            }

            gameObject.SetActive(false);
            isFalling = false;
        }

        /// <summary>
        /// Resets the rock back to its original resting state.
        /// </summary>
        public void ResetRock()
        {
            StopAllCoroutines();
            gameObject.SetActive(true);

            transform.localPosition = initialLocalPos;
            transform.localRotation = initialLocalRot;
            transform.localScale = initialLocalScale;

            if (sr != null)
            {
                sr.color = initialColor;
            }

            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.bodyType = RigidbodyType2D.Kinematic;
            }

            if (col != null)
            {
                col.enabled = true;
            }

            isFalling = false;
            hasFallen = false;
        }
    }
}
