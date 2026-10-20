using TvKioskWrapper.Configuration;

namespace TvKioskWrapper.Shell;

/// <summary>
/// Contract for shell-level actions invoked by input and browser controllers.
/// </summary>
public interface IKioskShellActions
{
    bool IsOnHomePage { get; }
    bool IsNativeTvSpaActive { get; }
    bool EnsureBrowserFocus();
    void NavigateToHome();
    void NavigateToUrl(string url, StreamingServicePreset? preset = null);
    void HideCursorImmediately();
    void ToggleAudioDevice();
    void EnableHomeTheaterMode();
    bool IsSettingsMenuOpen { get; }
    void ToggleSettingsMenu();
    void CloseSettingsMenu();
    void HandleSettingsMenuKey(System.Windows.Input.Key key);
}
