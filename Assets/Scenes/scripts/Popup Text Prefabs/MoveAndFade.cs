using UnityEngine;
using System.Collections;

public class MoveAndFade : MonoBehaviour
{
    public float moveSpeed = 0.1f;
    public float fadeDuration = 5f;
    public float waitBeforeRespawn = 10f;
    public Vector3 startPosition; // Store the initial position

    private Renderer rend;

    private void Start()
    {
        // Save the starting position
        startPosition = transform.position;
        rend = GetComponent<Renderer>();
        
        StartCoroutine(LoopRoutine());
    }

    private IEnumerator LoopRoutine()
    {
        while (true) // Infinite loop
        {
            // 1. Reset Position and Opacity
            transform.position = startPosition;
            Color color = rend.material.color;
            color.a = 1f;
            rend.material.color = color;
            
            // Ensure object is visible
            gameObject.SetActive(true);

            // 2. Move Logic
            while (transform.position.y > 0)
            {
                transform.position += Vector3.down * moveSpeed;
                yield return null;
            }

            // 3. Fade Logic
            float timer = 0;
            while (timer < fadeDuration)
            {
                timer += Time.deltaTime;
                float alpha = Mathf.Clamp01(1 - (timer / fadeDuration));
                color.a = alpha;
                rend.material.color = color;
                yield return null;
            }

            // 4. Wait for 10 seconds before looping back
            yield return new WaitForSeconds(waitBeforeRespawn);
        }
    }
}