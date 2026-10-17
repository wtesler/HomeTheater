using System.IO;
using System.Text.Json;

namespace TvKioskWrapper.Configuration;

/// <summary>
/// Loads <see cref="AppConfig"/> from config.json and applies command-line flag overrides.
/// </summary>
public static class ConfigLoader
{
    public static AppConfig Load(string[] commandLineArgs)
    {
        string configPath = Path.Combine(AppContext.BaseDirectory, "config.json");
        AppConfig config = LoadFromFile(configPath);

        ApplyCommandLineOverrides(config, commandLineArgs);
        return config;
    }

    private static AppConfig LoadFromFile(string configPath)
    {
        if (!File.Exists(configPath))
        {
            return new AppConfig();
        }

        string json = File.ReadAllText(configPath);
        AppConfig? parsed = JsonSerializer.Deserialize<AppConfig>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        });

        return parsed ?? new AppConfig();
    }

    private static void ApplyCommandLineOverrides(AppConfig config, string[] args)
    {
        foreach (string arg in args)
        {
            if (string.Equals(arg, "--debug", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "-d", StringComparison.OrdinalIgnoreCase))
            {
                config.Debug = true;
            }
            else if (string.Equals(arg, "--gpu", StringComparison.OrdinalIgnoreCase))
            {
                config.DefaultUrl = "edge://gpu";
            }
            else if (arg.StartsWith("--url=", StringComparison.OrdinalIgnoreCase))
            {
                string customUrl = arg["--url=".Length..].Trim('"');
                if (!string.IsNullOrWhiteSpace(customUrl))
                {
                    config.DefaultUrl = customUrl;
                }
            }
        }
    }
}
