using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;
using TvKioskWrapper.Automation;
using TvKioskWrapper.Configuration;
using TvKioskWrapper.Input;
using TvKioskWrapper.Interop;
using TvKioskWrapper.Scripts;
using TvKioskWrapper.Shell;
using TvKioskWrapper.WebView;

namespace TvKioskWrapper;

/// <summary>
/// Module A: Root kiosk window coordinating shell lifecycle, cursor auto-hiding,
/// remote input routing, and WebView2 navigation.
/// </summary>
public partial class MainWindow : Window, IKioskShellActions
{
    private readonly AppConfig _config;
    private readonly ScriptInjector _scriptInjector;
    private readonly WebViewManager _webViewManager;
    private readonly CursorAutoHider _cursorAutoHider;
    private readonly InputController _inputController;
    private readonly ExternalAppManager _externalAppManager;
    private readonly ColorControlManager _colorControlManager;
    private readonly AudioDeviceManager _audioDeviceManager;
    private readonly SettingsMenuModalWindow _settingsMenuWindow;
    private readonly AutomationServer? _automationServer;

    private bool _isInitialized;

    public bool IsOnHomePage => _webViewManager.IsOnHomePage;

    public bool IsNativeTvSpaActive => !_webViewManager.IsSpatialNavigationEnabled && !IsOnHomePage;

    public MainWindow()
    {
        InitializeComponent();

        string[] args = Environment.GetCommandLineArgs().Skip(1).ToArray();
        _config = ConfigLoader.Load(args);
        _scriptInjector = new ScriptInjector();
        _webViewManager = new WebViewManager(KioskWebView, _scriptInjector, _config);
        _webViewManager.NavigationCompleted += OnWebViewNavigationCompleted;
        _webViewManager.ExternalAppLaunchRequested += LaunchExternalApp;
        _webViewManager.HomeRequested += NavigateToHome;

        _externalAppManager = new ExternalAppManager(this);
        _externalAppManager.ReturnToLauncherRequested += NavigateToHome;
        _externalAppManager.SettingsMenuRequested += ToggleSettingsMenu;



        _colorControlManager = new ColorControlManager(_config.ColorControl, isAutomation: _config.AutomationEnabled);
        _audioDeviceManager = new AudioDeviceManager();
        _settingsMenuWindow = new SettingsMenuModalWindow(
            this,
            () => _colorControlManager.VideoPresetNames,
            () => _colorControlManager.SoundPresetNames,
            () => _colorControlManager.MotionPresetNames,
            () => _audioDeviceManager.GetActivePlaybackDevices(),
            () => _audioDeviceManager.GetDefaultPlaybackDeviceId());
        _settingsMenuWindow.VideoPresetSelected += OnVideoPresetSelected;
        _settingsMenuWindow.SoundPresetSelected += OnSoundPresetSelected;
        _settingsMenuWindow.MotionPresetSelected += OnMotionPresetSelected;
        _settingsMenuWindow.AudioDeviceSelected += OnAudioDeviceSelected;
        _settingsMenuWindow.ForceHomeTheaterSelected += OnForceHomeTheaterSelected;
        _settingsMenuWindow.MenuClosed += OnSettingsMenuClosed;



        _cursorAutoHider = new CursorAutoHider(
            this,
            _config.CursorHideDelaySeconds,
            hidden => _isInitialized
                ? _scriptInjector.SetDomCursorHiddenAsync(KioskWebView, hidden)
                : Task.CompletedTask);

        _inputController = new InputController(KioskWebView, _scriptInjector, this);

        if (_config.AutomationEnabled)
        {
            Topmost = false;
            ShowActivated = false;
            ShowInTaskbar = false;
            WindowState = WindowState.Normal;
            Width = 1920;
            Height = 1080;
            Left = -3200;
            Top = -3200;
            _automationServer = new AutomationServer(KioskWebView, _scriptInjector, this, _config.AutomationPort);
        }

        Loaded += OnWindowLoaded;
    }

    private async void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (!_config.AutomationEnabled)
        {
            ActivateNativeWindow();
            _cursorAutoHider.Start();
            _colorControlManager.EnableHomeTheaterMode();
        }

        await _webViewManager.InitializeAsync();
        _isInitialized = true;
        _automationServer?.Start();
        NavigateToUrl(_config.DefaultUrl);
    }

    private async void Window_Activated(object sender, EventArgs e)
    {
        if (_externalAppManager.IsAppRunning)
        {
            return;
        }

        if (!_config.AutomationEnabled)
        {
            ActivateNativeWindow();
        }
        if (_isInitialized)
        {
            if (!_config.AutomationEnabled)
            {
                _webViewManager.EnsureBrowserFocus();
            }
            await _scriptInjector.InjectFocusAndScrollbarStylesAsync(
                KioskWebView,
                _webViewManager.IsSpatialNavigationEnabled,
                IsOnHomePage,
                _webViewManager.ActiveNavigator);
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        _inputController.HandlePreviewKeyDown(sender, e);
    }

    private void Window_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        _cursorAutoHider.NotifyMouseMoved();
    }

    private void Window_Closed(object sender, EventArgs e)
    {
        _settingsMenuWindow.Close();
        _colorControlManager.DisableHomeTheaterMode();
        _colorControlManager.Dispose();
        _externalAppManager.Dispose();
        _automationServer?.Dispose();
        _cursorAutoHider.StopAndRestore();
    }

    public void ToggleAudioDevice()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(ToggleAudioDevice);
            return;
        }

        _audioDeviceManager.ToggleNextPlaybackDevice();
    }

    public bool IsSettingsMenuOpen => _settingsMenuWindow.IsMenuOpen;

    public void ToggleSettingsMenu()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(ToggleSettingsMenu);
            return;
        }

        if (_settingsMenuWindow.IsMenuOpen)
        {
            _settingsMenuWindow.CloseMenu();
            return;
        }


        _settingsMenuWindow.ShowMenu();
    }

    public void CloseSettingsMenu()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(CloseSettingsMenu);
            return;
        }

        _settingsMenuWindow.CloseMenu();
    }

    public void HandleSettingsMenuKey(Key key)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => HandleSettingsMenuKey(key));
            return;
        }

        _settingsMenuWindow.HandleKey(key);
    }

    private void OnVideoPresetSelected(int selectedIndex, string presetName)
    {
        _colorControlManager.SetVideoPreset(selectedIndex);
    }

    private void OnAudioDeviceSelected(AudioDeviceManager.AudioDeviceInfo device)
    {
        _audioDeviceManager.SetDefaultPlaybackDevice(device.Id);
    }

    private void OnSoundPresetSelected(int selectedIndex, string presetName)
    {
        _colorControlManager.SetSoundPreset(selectedIndex);
    }

    private void OnMotionPresetSelected(int selectedIndex, string presetName)
    {
        _colorControlManager.SetMotionPreset(selectedIndex);
    }

    private void OnForceHomeTheaterSelected()
    {
        _colorControlManager.EnableHomeTheaterMode();
    }

    private void OnSettingsMenuClosed()
    {
        RestoreFocusAfterModal();
    }

    private void RestoreFocusAfterModal()
    {
        if (_externalAppManager.IsAppRunning)
        {
            _externalAppManager.RestoreActiveAppFocus();
        }
        else
        {
            EnsureBrowserFocus();
        }
    }

    public void EnableHomeTheaterMode()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(EnableHomeTheaterMode);
            return;
        }

        _colorControlManager.EnableHomeTheaterMode();
    }

    public void HideCursorImmediately() => _cursorAutoHider.HideImmediately();

    public bool EnsureBrowserFocus() => _isInitialized && _webViewManager.EnsureBrowserFocus();

    public void LaunchExternalApp(string executablePath)
    {
        // Yield Topmost status so the external app (e.g. Plex HTPC) appears in front of TvKioskWrapper.
        Topmost = false;
        if (!_externalAppManager.Launch(executablePath))
        {
            if (!_config.AutomationEnabled)
            {
                Topmost = true;
                ActivateNativeWindow();
            }
        }
    }

    public void NavigateToHome()
    {
        _externalAppManager.CloseActiveApp();

        if (!_config.AutomationEnabled)
        {
            Topmost = true;
            ActivateNativeWindow();
        }

        if (!_isInitialized)
        {
            return;
        }

        _webViewManager.NavigateToHome();
    }

    public void NavigateToUrl(string url, StreamingServicePreset? preset = null)
    {
        if (!_isInitialized || string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        _webViewManager.Navigate(url, preset);
    }

    private async void OnWebViewNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (_cursorAutoHider.IsCursorHidden)
        {
            await _scriptInjector.SetDomCursorHiddenAsync(KioskWebView, hidden: true);
        }
    }

    private void ActivateNativeWindow()
    {
        nint hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != nint.Zero)
        {
            NativeMethods.SetForegroundWindow(hwnd);
        }
    }
}