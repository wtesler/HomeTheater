using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using TvKioskWrapper.Configuration;

namespace TvKioskWrapper.WebView;

/// <summary>
/// Maps the local Assets/Launcher folder to https://launchpad.example/index.html, injects configured
/// service presets into the launcher page, and handles app launch and global hotkey messages from the DOM.
/// </summary>
public sealed class LauncherHostManager
{
    // Note: Avoid ".local" TLDs on Windows as they trigger a 3-5 second mDNS lookup timeout in Chromium.
    public const string VirtualHostName = "launchpad.example";
    public const string HomePageUrl = $"https://{VirtualHostName}/index.html";

    private readonly AppConfig _config;

    public event Action<string>? ServiceLaunchRequested;
    public event Action? HomeRequested;

    public LauncherHostManager(AppConfig config)
    {
        _config = config;
    }

    public static bool IsLauncherUrl(string? url)
    {
        return !string.IsNullOrWhiteSpace(url) &&
               (url.Contains(VirtualHostName, StringComparison.OrdinalIgnoreCase) ||
                url.Contains("kiosk.local", StringComparison.OrdinalIgnoreCase));
    }

    public async Task AttachAsync(CoreWebView2 coreWebView)
    {
        string launcherFolder = Path.Combine(AppContext.BaseDirectory, "Assets", "Launcher");
        Directory.CreateDirectory(launcherFolder);

        coreWebView.SetVirtualHostNameToFolderMapping(
            VirtualHostName,
            launcherFolder,
            CoreWebView2HostResourceAccessKind.Allow);

        string servicesJson = JsonSerializer.Serialize(_config.Services);
        string injectServicesScript = $$"""
            if (location.hostname === '{{VirtualHostName}}') {
                window.__KIOSK_SERVICES__ = {{servicesJson}};
                if (typeof window.__setKioskServices === 'function') {
                    window.__setKioskServices(window.__KIOSK_SERVICES__);
                }
            }
            """;

        await coreWebView.AddScriptToExecuteOnDocumentCreatedAsync(injectServicesScript);
        coreWebView.WebMessageReceived += OnWebMessageReceived;
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(e.WebMessageAsJson);
            JsonElement root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("type", out JsonElement typeProp))
            {
                return;
            }

            string? messageType = typeProp.GetString();

            if (string.Equals(messageType, "launchService", StringComparison.OrdinalIgnoreCase) &&
                root.TryGetProperty("url", out JsonElement urlProp))
            {
                string? targetUrl = urlProp.GetString();
                if (!string.IsNullOrWhiteSpace(targetUrl))
                {
                    ServiceLaunchRequested?.Invoke(targetUrl);
                }
            }
            else if (string.Equals(messageType, "hotkey", StringComparison.OrdinalIgnoreCase) &&
                     root.TryGetProperty("key", out JsonElement keyProp))
            {
                string? key = keyProp.GetString();
                if (string.Equals(key, "F12", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(key, "Exit", StringComparison.OrdinalIgnoreCase))
                {
                    Application.Current?.Dispatcher.BeginInvoke(() => Application.Current.Shutdown());
                }
                else if (string.Equals(key, "F1", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(key, "Home", StringComparison.OrdinalIgnoreCase))
                {
                    HomeRequested?.Invoke();
                }
            }
        }
        catch
        {
            // Ignore malformed messages from external web pages.
        }
    }
}
