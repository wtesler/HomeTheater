using System.Drawing;
using System.IO;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using TvKioskWrapper.Configuration;
using TvKioskWrapper.Scripts;

namespace TvKioskWrapper.WebView;

/// <summary>
/// Module B: WebView2 Engine Configuration with PlayReady SL3000, HEVC hardware decoding,
/// isolated user profile storage, local launcher virtual host, and navigation lifecycle management.
/// </summary>
public sealed class WebViewManager
{
    public const string BrowserArguments =
        "--enable-features=PlayReadyDrmWin10,PlatformHEVCDecoderSupport,HardwareMediaKeyHandling " +
        "--disable-features=WidevineDrm " +
        "--enable-gpu-rasterization " +
        "--enable-zero-copy " +
        "--autoplay-policy=no-user-gesture-required";

    private readonly WebView2 _webView;
    private readonly ScriptInjector _scriptInjector;
    private readonly AppConfig _config;
    private readonly UserAgentOverrideHandler _userAgentHandler = new();
    private readonly LauncherHostManager _launcherHostManager;

    public bool IsSpatialNavigationEnabled { get; private set; }

    public string? ActiveNavigator { get; private set; }

    public bool IsOnHomePage => LauncherHostManager.IsLauncherUrl(_webView.Source?.ToString());

    public event EventHandler<CoreWebView2NavigationCompletedEventArgs>? NavigationCompleted;
    public event Action<string>? ServiceLaunchRequested;
    public event Action<string>? ExternalAppLaunchRequested;
    public event Action? HomeRequested;
    public event Action? SleepRequested;

    public WebViewManager(WebView2 webView, ScriptInjector scriptInjector, AppConfig config)
    {
        _webView = webView;
        _scriptInjector = scriptInjector;
        _config = config;
        _launcherHostManager = new LauncherHostManager(config);
        _launcherHostManager.ServiceLaunchRequested += url =>
        {
            Navigate(url);
            ServiceLaunchRequested?.Invoke(url);
        };
        _launcherHostManager.ExternalAppLaunchRequested += exePath => ExternalAppLaunchRequested?.Invoke(exePath);
        _launcherHostManager.HomeRequested += () => HomeRequested?.Invoke();
        _launcherHostManager.SleepRequested += () => SleepRequested?.Invoke();
    }

    public static string GetIsolatedProfilePath()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "TvKioskWrapper", "Profile");
    }

    public async Task InitializeAsync()
    {
        _webView.DefaultBackgroundColor = Color.Black;

        string userDataFolder = GetIsolatedProfilePath();
        Directory.CreateDirectory(userDataFolder);

        var options = new CoreWebView2EnvironmentOptions
        {
            AdditionalBrowserArguments = BrowserArguments
        };

        CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(
            browserExecutableFolder: null,
            userDataFolder: userDataFolder,
            options: options);

        await _webView.EnsureCoreWebView2Async(environment);

        ConfigureCoreSettings(_webView.CoreWebView2);
        _userAgentHandler.Attach(_webView.CoreWebView2);
        await _launcherHostManager.AttachAsync(_webView.CoreWebView2);
        await _scriptInjector.RegisterPersistentScriptsAsync(_webView.CoreWebView2);

        _webView.CoreWebView2.NavigationCompleted += OnCoreWebViewNavigationCompleted;
        _webView.CoreWebView2.NewWindowRequested += OnNewWindowRequested;
        _webView.CoreWebView2.PermissionRequested += OnPermissionRequested;
        _webView.CoreWebView2.ContainsFullScreenElementChanged += OnContainsFullScreenElementChanged;

        EnsureBrowserFocus();
    }

    /// <summary>
    /// Ensures the native Chromium child HWND owns Win32 keyboard focus (equivalent to TabInto)
    /// and returns true if the browser already had Win32 focus prior to this call.
    /// </summary>
    public bool EnsureBrowserFocus()
    {
        if (_config.AutomationEnabled)
        {
            return false;
        }

        var sink = (IKeyboardInputSink)_webView;
        bool alreadyHadNativeFocus = sink.HasFocusWithin();

        _webView.Focus();
        Keyboard.Focus(_webView);

        if (!alreadyHadNativeFocus)
        {
            sink.TabInto(new TraversalRequest(FocusNavigationDirection.First));
        }

        return alreadyHadNativeFocus;
    }

    public void NavigateToHome()
    {
        if (IsOnHomePage || _webView.CoreWebView2 is null)
        {
            EnsureBrowserFocus();
            return;
        }

        _webView.CoreWebView2.Stop();
        Navigate(LauncherHostManager.HomePageUrl);
    }

    public void Navigate(string url, StreamingServicePreset? preset = null)
    {
        if (string.IsNullOrWhiteSpace(url) || _webView.CoreWebView2 is null)
        {
            return;
        }

        if (LauncherHostManager.IsLauncherUrl(url))
        {
            url = LauncherHostManager.HomePageUrl;
        }

        preset ??= _config.FindMatchingPreset(url);
        _userAgentHandler.ApplyForNavigation(_webView.CoreWebView2, preset);

        IsSpatialNavigationEnabled = !LauncherHostManager.IsLauncherUrl(url) &&
                                     (preset?.SpatialNavigation ?? true);
        ActiveNavigator = preset?.Navigator;

        _webView.CoreWebView2.Navigate(url);
        EnsureBrowserFocus();
    }

    private void ConfigureCoreSettings(CoreWebView2 coreWebView)
    {
        CoreWebView2Settings settings = coreWebView.Settings;
        settings.IsStatusBarEnabled = false;
        settings.AreDefaultContextMenusEnabled = false;
        settings.AreDevToolsEnabled = _config.Debug;
        settings.IsSwipeNavigationEnabled = false;
        settings.IsZoomControlEnabled = false;
        settings.IsBuiltInErrorPageEnabled = true;
        settings.AreHostObjectsAllowed = false;
    }

    private async void OnCoreWebViewNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        EnsureBrowserFocus();

        string? currentUrl = _webView.Source?.ToString();
        if (!LauncherHostManager.IsLauncherUrl(currentUrl))
        {
            StreamingServicePreset? matched = _config.FindMatchingPreset(currentUrl);
            if (matched is not null)
            {
                IsSpatialNavigationEnabled = matched.SpatialNavigation;
                ActiveNavigator = matched.Navigator;
            }
        }

        await _scriptInjector.InjectFocusAndScrollbarStylesAsync(
            _webView,
            IsSpatialNavigationEnabled,
            IsOnHomePage,
            ActiveNavigator);
        NavigationCompleted?.Invoke(this, e);
    }

    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        if (!string.IsNullOrWhiteSpace(e.Uri))
        {
            Navigate(e.Uri);
        }
    }

    private static void OnPermissionRequested(object? sender, CoreWebView2PermissionRequestedEventArgs e)
    {
        if (e.PermissionKind is CoreWebView2PermissionKind.Notifications or CoreWebView2PermissionKind.Geolocation)
        {
            e.State = CoreWebView2PermissionState.Deny;
            e.Handled = true;
        }
    }

    private void OnContainsFullScreenElementChanged(object? sender, object e)
    {
        EnsureBrowserFocus();
    }
}
