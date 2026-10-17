namespace TvKioskWrapper.Scripts;

/// <summary>
/// Provides the 2D geometric spatial D-Pad navigation script and focus ring overlay for desktop websites.
/// </summary>
public static class SpatialNavigationScript
{
    public static string Source =>
        ScriptFileLoader.Load("Scripts/Js/spatial-navigation.js");
}

