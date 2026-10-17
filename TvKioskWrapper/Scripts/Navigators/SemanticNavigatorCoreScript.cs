namespace TvKioskWrapper.Scripts.Navigators;

/// <summary>
/// Shared semantic D-Pad navigation engine used by per-app navigators (HBO Max, Plex, etc.).
/// Manages the focus ring/badge overlay, row/column memory, sidebar &amp; drawer transitions,
/// dropdown listbox navigation, lazy rail hydration, and virtual scroll pagination.
/// </summary>
public static class SemanticNavigatorCoreScript
{
    public static string Source =>
        ScriptFileLoader.Load("Scripts/Navigators/Js/semantic-navigator-core.js");
}
