using TvKioskWrapper.Scripts;

namespace TvKioskWrapper.Automation;

/// <summary>
/// Provides compact, token-efficient JavaScript inspectors that summarize a webpage's semantic
/// structure, landmarks, rails/carousels, and active focus state into small JSON payloads.
/// </summary>
public static class SemanticDomInspector
{
    public static string ActiveElementScript =>
        ScriptFileLoader.Load("Automation/Js/active-element.js");

    public static string SemanticSummaryScript =>
        ScriptFileLoader.Load("Automation/Js/semantic-summary.js");
}

