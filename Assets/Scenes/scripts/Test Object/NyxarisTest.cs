using UnityEngine;

public class NyxarisTest : MonoBehaviour
{
    void Start()
    {
        NyxarisManager manager = FindFirstObjectByType<NyxarisManager>();
        if (manager != null)
        {
            manager.SendInputMessage();
        }
    }
}