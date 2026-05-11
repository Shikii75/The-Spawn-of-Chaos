using UnityEngine;

public class NyxarisTest : MonoBehaviour
{
    void Start()
    {
        FindObjectOfType<NyxarisManager>();
            FindObjectOfType<NyxarisManager>().SendInputMessage();
    }
}