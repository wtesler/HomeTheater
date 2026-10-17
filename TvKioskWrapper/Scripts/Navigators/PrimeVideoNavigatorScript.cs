namespace TvKioskWrapper.Scripts.Navigators;

/// <summary>
/// Semantic D-Pad adapter tailored specifically to Amazon Prime Video (www.primevideo.com).
/// Mounts onto <see cref="SemanticNavigatorCoreScript"/> with Prime Video's DOM landmarks.
/// </summary>
public static class PrimeVideoNavigatorScript
{
    public static string Source =>
        ScriptFileLoader.Load("Scripts/Navigators/Js/primevideo-navigator.js");
}

