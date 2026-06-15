using UnityEngine;

public class Fog : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 0.5f;

    void Update()
    {
        transform.Translate(Vector3.right * moveSpeed * Time.deltaTime);
    }
}