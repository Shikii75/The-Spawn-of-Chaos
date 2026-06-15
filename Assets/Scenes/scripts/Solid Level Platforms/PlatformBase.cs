using UnityEngine;

public abstract class PlatformBase : MonoBehaviour
{
    public virtual void OnPlayerLand(GameObject player) { }
    public virtual void OnPlayerLeave(GameObject player) { }
}
