using System.Collections.Concurrent;
using System.IO;

namespace TvKioskWrapper.Scripts;

/// <summary>
/// Loads JavaScript files from the application output directory so scripts live in real .js files
/// rather than large C# raw string literals.
/// </summary>
public static class ScriptFileLoader
{
    private static readonly ConcurrentDictionary<string, string> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static string Load(string relativePath)
    {
        return Cache.GetOrAdd(relativePath, static rel =>
        {
            string fullPath = Path.Combine(AppContext.BaseDirectory, rel.Replace('/', Path.DirectorySeparatorChar));
            return File.ReadAllText(fullPath);
        });
    }
}

