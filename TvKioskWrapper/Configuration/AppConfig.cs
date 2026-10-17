using System.Text.Json.Serialization;
using TvKioskWrapper.WebView;

namespace TvKioskWrapper.Configuration;

/// <summary>
/// Application configuration model loaded from config.json.
/// </summary>
public sealed class AppConfig
{
    [JsonPropertyName("defaultUrl")]
    public string DefaultUrl { get; set; } = LauncherHostManager.HomePageUrl;

    [JsonPropertyName("cursorHideDelaySeconds")]
    public double CursorHideDelaySeconds { get; set; } = 2.0;

    [JsonPropertyName("debug")]
    public bool Debug { get; set; }

    [JsonIgnore]
    public bool AutomationEnabled { get; set; }

    [JsonIgnore]
    public int AutomationPort { get; set; } = 9333;

    [JsonPropertyName("services")]
    public List<StreamingServicePreset> Services { get; set; } = [];

    /// <summary>
    /// Finds the most specific service preset matching the given URL, or falls back to host matching.
    /// </summary>
    public StreamingServicePreset? FindMatchingPreset(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        // 1. Prefix match (most specific URL first, e.g., youtube.com/tv before youtube.com)
        StreamingServicePreset? prefixMatch = Services
            .Where(s => !string.IsNullOrWhiteSpace(s.Url))
            .OrderByDescending(s => s.Url.Length)
            .FirstOrDefault(s => url.StartsWith(s.Url, StringComparison.OrdinalIgnoreCase));

        if (prefixMatch is not null)
        {
            return prefixMatch;
        }

        // 2. Hostname match (handles sub-paths once navigated inside a service)
        if (Uri.TryCreate(url, UriKind.Absolute, out Uri? targetUri))
        {
            return Services.FirstOrDefault(s =>
                Uri.TryCreate(s.Url, UriKind.Absolute, out Uri? serviceUri) &&
                string.Equals(serviceUri.Host, targetUri.Host, StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }
}
