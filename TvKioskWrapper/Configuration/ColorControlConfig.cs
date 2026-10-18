using System.Text.Json.Serialization;

namespace TvKioskWrapper.Configuration;

/// <summary>
/// Configuration for ColorControl LG TV preset automation.
/// </summary>
public sealed class ColorControlConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("executablePath")]
    public string ExecutablePath { get; set; } = @"W:\Programs\ColorControl\ColorControl.exe";

    [JsonPropertyName("enableArguments")]
    public string EnableArguments { get; set; } = "--nogui --lgpreset HomeTheater";

    [JsonPropertyName("disableArguments")]
    public string DisableArguments { get; set; } = "--nogui --lgpreset PC";
}

