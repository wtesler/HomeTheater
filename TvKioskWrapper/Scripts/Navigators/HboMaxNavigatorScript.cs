namespace TvKioskWrapper.Scripts.Navigators;

/// <summary>
/// Semantic D-Pad adapter tailored specifically to HBO Max (play.hbomax.com / play.max.com).
/// Mounts onto <see cref="SemanticNavigatorCoreScript"/> with HBO Max's DOM landmarks.
/// </summary>
public static class HboMaxNavigatorScript
{
    public static string Source =>
        ScriptFileLoader.Load("Scripts/Navigators/Js/hbomax-navigator.js");
}
