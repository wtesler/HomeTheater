using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using Xunit;

namespace TvKioskWrapper.Tests.Infrastructure;

[CollectionDefinition(CollectionName, DisableParallelization = true)]
public sealed class KioskAutomationCollection : ICollectionFixture<KioskAutomationFixture>
{
    public const string CollectionName = "KioskAutomation";
}

/// <summary>
/// Shared test fixture that manages a single background <c>TvKioskWrapper.exe --automation</c>
/// instance across the test run and exposes compact JSON helpers for navigation and D-Pad simulation.
/// </summary>
public sealed class KioskAutomationFixture : IAsyncLifetime
{
    private readonly HttpClient _http = new()
    {
        BaseAddress = new Uri("http://127.0.0.1:9333"),
        Timeout = TimeSpan.FromSeconds(20)
    };

    private Process? _launchedProcess;

    public async Task InitializeAsync()
    {
        if (await IsServerListeningAsync())
        {
            return;
        }

        string exePath = ResolveExecutablePath();
        _launchedProcess = Process.Start(new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = "--automation",
            WorkingDirectory = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory,
            UseShellExecute = false
        });

        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(15))
        {
            if (await IsServerListeningAsync())
            {
                return;
            }
            await Task.Delay(250);
        }

        throw new TimeoutException("Timed out waiting for TvKioskWrapper --automation server on port 9333.");
    }

    public async Task DisposeAsync()
    {
        if (_launchedProcess is not null && !_launchedProcess.HasExited)
        {
            try
            {
                using var response = await _http.PostAsync("/quit", content: null);
                _launchedProcess.WaitForExit(3000);
            }
            catch
            {
                // Fallback to process kill below if HTTP shutdown fails.
            }

            if (!_launchedProcess.HasExited)
            {
                _launchedProcess.Kill(entireProcessTree: true);
            }
            _launchedProcess.Dispose();
        }

        _http.Dispose();
    }

    public async Task<AutomationStatus> GetStatusAsync()
    {
        var status = await _http.GetFromJsonAsync<AutomationStatus>("/status");
        return status ?? throw new InvalidOperationException("Null status returned from /status.");
    }

    public async Task<AutomationStatus> NavigateAndWaitForNavigatorAsync(
        string url,
        string expectedNavigatorMode,
        int timeoutMs = 15000)
    {
        using var navResponse = await _http.PostAsJsonAsync("/navigate", new { url, waitMs = 1500 });
        navResponse.EnsureSuccessStatusCode();

        var sw = Stopwatch.StartNew();
        AutomationStatus? latest = null;
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            latest = await GetStatusAsync();
            if (string.Equals(latest.NavigatorMode, expectedNavigatorMode, StringComparison.OrdinalIgnoreCase) &&
                latest.RowCount >= 2 &&
                latest.KioskFocusedElement is { Rect.Width: > 8, Rect.Height: > 8 })
            {
                return latest;
            }

            await Task.Delay(350);
        }

        throw new TimeoutException(
            $"Navigator '{expectedNavigatorMode}' did not become ready at '{url}'. " +
            $"Last state: mode={latest?.NavigatorMode ?? "null"}, rows={latest?.RowCount ?? 0}, " +
            $"focused={latest?.KioskFocusedElement?.Describe() ?? "null"}");
    }

    public async Task<KeyDispatchResult> SendKeyAsync(string key, int delayMs = 280)
    {
        using var response = await _http.PostAsJsonAsync("/key", new { key, delayMs });
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<KeyDispatchResult>();
        return result ?? throw new InvalidOperationException($"Null response from /key for '{key}'.");
    }

    /// <summary>Evaluates a script in the page. The script should return a small JSON-serializable value.</summary>
    public async Task<T> EvalAsync<T>(string script)
    {
        using var response = await _http.PostAsJsonAsync("/eval", new { script });
        response.EnsureSuccessStatusCode();

        T? result = await response.Content.ReadFromJsonAsync<T>();
        return result ?? throw new InvalidOperationException("Null response from /eval.");
    }

    public async Task WaitUntilAsync(Func<Task<bool>> condition, string description, int timeoutMs = 6000)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (await condition())
            {
                return;
            }
            await Task.Delay(200);
        }

        throw new TimeoutException($"Timed out waiting for: {description}");
    }

    private async Task<bool> IsServerListeningAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(600));
            using var response = await _http.GetAsync("/status", cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static string ResolveExecutablePath()
    {
        string localCandidate = Path.Combine(AppContext.BaseDirectory, "TvKioskWrapper.exe");
        if (File.Exists(localCandidate))
        {
            return localCandidate;
        }

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(
                dir.FullName,
                "TvKioskWrapper",
                "bin",
                "Debug",
                "net10.0-windows",
                "TvKioskWrapper.exe");
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate TvKioskWrapper.exe for UI automation tests.");
    }
}

