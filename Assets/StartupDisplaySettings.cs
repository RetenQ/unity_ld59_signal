using UnityEngine;

public static class StartupDisplaySettings
{
    private const int TargetWidth = 1920;
    private const int TargetHeight = 1080;

    // Apply once at app startup (before the first scene loads).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyStartupResolution()
    {
#if UNITY_STANDALONE
        Screen.SetResolution(TargetWidth, TargetHeight, FullScreenMode.ExclusiveFullScreen);
#endif
    }
}
