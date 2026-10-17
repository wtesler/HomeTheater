namespace TvKioskWrapper.Scripts;

/// <summary>
/// D-Pad navigable on-screen keyboard shown whenever a text field is focused in a web app.
/// Must be injected before the semantic navigator core so it can claim D-Pad keys while open.
/// </summary>
public static class OnScreenKeyboardScript
{
    public static string Source =>
        ScriptFileLoader.Load("Scripts/Js/on-screen-keyboard.js");
}

