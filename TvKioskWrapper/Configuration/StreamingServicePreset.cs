using System.Text.Json.Serialization;

namespace TvKioskWrapper.Configuration;

/// <summary>
/// Represents a streaming service preset displayed in the 10-foot Service Switcher HUD.
/// </summary>
public sealed class StreamingServicePreset
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("subtitle")]
    public string Subtitle { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("executablePath")]
    public string? ExecutablePath { get; set; }

    [JsonPropertyName("accentColor")]
    public string AccentColor { get; set; } = "#FFFFFF";

    [JsonPropertyName("userAgent")]
    public string? UserAgent { get; set; }

    [JsonPropertyName("navigator")]
    public string? Navigator { get; set; }

    [JsonPropertyName("spatialNavigation")]
    public bool SpatialNavigation { get; set; } = true;
}

