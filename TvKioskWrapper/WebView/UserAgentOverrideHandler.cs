using Microsoft.Web.WebView2.Core;
using TvKioskWrapper.Configuration;

namespace TvKioskWrapper.WebView;

/// <summary>
/// Manages per-service User-Agent switching configured in config.json and strips Windows Chromium
/// Client-Hint headers (Sec-CH-UA*) when a custom User-Agent is active.
/// </summary>
public sealed class UserAgentOverrideHandler
{
    private static readonly string[] ClientHintHeadersToRemove =
    [
        "Sec-CH-UA",
        "Sec-CH-UA-Mobile",
        "Sec-CH-UA-Platform",
        "Sec-CH-UA-Platform-Version",
        "Sec-CH-UA-Arch",
        "Sec-CH-UA-Bitness",
        "Sec-CH-UA-Model",
        "Sec-CH-UA-Full-Version",
        "Sec-CH-UA-Full-Version-List",
        "Sec-CH-UA-WoW64"
    ];

    private string _defaultUserAgent = string.Empty;
    private string? _activeCustomUserAgent;

    public void Attach(CoreWebView2 coreWebView)
    {
        _defaultUserAgent = coreWebView.Settings.UserAgent;

        coreWebView.AddWebResourceRequestedFilter(
            "*",
            CoreWebView2WebResourceContext.All);
        coreWebView.WebResourceRequested += OnWebResourceRequested;
    }

    public void ApplyForNavigation(CoreWebView2 coreWebView, StreamingServicePreset? preset)
    {
        _activeCustomUserAgent = !string.IsNullOrWhiteSpace(preset?.UserAgent)
            ? preset.UserAgent
            : null;

        coreWebView.Settings.UserAgent = _activeCustomUserAgent ?? _defaultUserAgent;
    }

    private void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_activeCustomUserAgent))
        {
            return;
        }

        CoreWebView2HttpRequestHeaders headers = e.Request.Headers;
        headers.SetHeader("User-Agent", _activeCustomUserAgent);

        foreach (string header in ClientHintHeadersToRemove)
        {
            if (headers.Contains(header))
            {
                headers.RemoveHeader(header);
            }
        }
    }
}
