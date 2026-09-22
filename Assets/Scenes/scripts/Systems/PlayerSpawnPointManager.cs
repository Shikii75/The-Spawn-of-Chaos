using UnityEngine;

public static class PlayerSpawnPointManager
{
    public static string targetSpawnPointName = "";
    public static bool isRespawning = false;

    // Checkpoint & SaveSlot explicit position transport
    public static bool useExplicitSpawnPosition = false;
    public static Vector3 explicitSpawnPosition = Vector3.zero;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic()
    {
        targetSpawnPointName = "";
        isRespawning = false;
        useExplicitSpawnPosition = false;
        explicitSpawnPosition = Vector3.zero;
    }
}
