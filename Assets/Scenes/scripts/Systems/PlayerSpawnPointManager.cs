using UnityEngine;

public static class PlayerSpawnPointManager
{
    public static string targetSpawnPointName = "";
    public static bool isRespawning = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic()
    {
        targetSpawnPointName = "";
        isRespawning = false;
    }
}
