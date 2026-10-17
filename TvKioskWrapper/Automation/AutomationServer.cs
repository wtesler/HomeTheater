using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Wpf;
using TvKioskWrapper.Scripts;
using TvKioskWrapper.Shell;

namespace TvKioskWrapper.Automation;

/// <summary>
/// Lightweight localhost HTTP server (enabled via --automation) for credit-efficient
/// semantic DOM inspection, simulated D-Pad navigation, live script injection, and downscaled screenshots.
/// </summary>
public sealed class AutomationServer : IDisposable
{
    private readonly WebView2 _webView;
    private readonly ScriptInjector _scriptInjector;
    private readonly IKioskShellActions _shell;
    private readonly HttpListener _listener = new();
    private CancellationTokenSource? _cts;

    public int Port { get; }

    public AutomationServer(
        WebView2 webView,
        ScriptInjector scriptInjector,
        IKioskShellActions shell,
        int port = 9333)
    {
        _webView = webView;
        _scriptInjector = scriptInjector;
        _shell = shell;
        Port = port;
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
    }

    public void Start()
    {
        if (_listener.IsListening)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _listener.Start();
        _ = Task.Run(() => ListenLoopAsync(_cts.Token));
    }

    private async Task ListenLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _listener.IsListening)
        {
            try
            {
                HttpListenerContext context = await _listener.GetContextAsync();
                _ = Task.Run(() => HandleRequestAsync(context), token);
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch
            {
                // Keep listener alive on transient errors.
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        string path = context.Request.Url?.AbsolutePath.TrimEnd('/').ToLowerInvariant() ?? "";
        try
        {
            switch (path)
            {
                case "":
                case "/status":
                    await HandleStatusAsync(context);
                    break;

                case "/semantic-tree":
                    await HandleSemanticTreeAsync(context);
                    break;

                case "/navigate":
                    await HandleNavigateAsync(context);
                    break;

                case "/key":
                    await HandleKeyAsync(context);
                    break;

                case "/eval":
                    await HandleEvalAsync(context);
                    break;

                case "/screenshot":
                    await HandleScreenshotAsync(context);
                    break;

                case "/quit":
                    await WriteJsonAsync(context.Response, 200, new { ok = true, action = "shutting_down" });
                    _ = Application.Current.Dispatcher.BeginInvoke(() => Application.Current.Shutdown());
                    break;

                default:
                    await WriteJsonAsync(context.Response, 404, new { error = $"Unknown endpoint: {path}" });
                    break;
            }
        }
        catch (Exception ex)
        {
            await WriteJsonAsync(context.Response, 500, new { error = ex.Message });
        }
    }

    private async Task HandleStatusAsync(HttpListenerContext context)
    {
        string activeJson = await Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            if (_webView.CoreWebView2 is null)
            {
                return "{}";
            }
            return await _webView.CoreWebView2.ExecuteScriptAsync(SemanticDomInspector.ActiveElementScript);
        }).Task.Unwrap();

        await WriteRawJsonAsync(context.Response, 200, activeJson);
    }

    private async Task HandleSemanticTreeAsync(HttpListenerContext context)
    {
        string treeJson = await Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            if (_webView.CoreWebView2 is null)
            {
                return "{}";
            }
            return await _webView.CoreWebView2.ExecuteScriptAsync(SemanticDomInspector.SemanticSummaryScript);
        }).Task.Unwrap();

        await WriteRawJsonAsync(context.Response, 200, treeJson);
    }

    private async Task HandleNavigateAsync(HttpListenerContext context)
    {
        using JsonDocument body = await ReadJsonBodyAsync(context.Request);
        string? url = body.RootElement.TryGetProperty("url", out JsonElement u) ? u.GetString() : null;
        int waitMs = body.RootElement.TryGetProperty("waitMs", out JsonElement w) ? w.GetInt32() : 2500;

        if (string.IsNullOrWhiteSpace(url))
        {
            await WriteJsonAsync(context.Response, 400, new { error = "Missing 'url' field." });
            return;
        }

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            if (string.Equals(url, "home", StringComparison.OrdinalIgnoreCase))
            {
                _shell.NavigateToHome();
            }
            else
            {
                _shell.NavigateToUrl(url);
            }
        });

        await Task.Delay(Math.Clamp(waitMs, 100, 15000));
        await HandleStatusAsync(context);
    }

    private async Task HandleKeyAsync(HttpListenerContext context)
    {
        using JsonDocument body = await ReadJsonBodyAsync(context.Request);
        var keys = new List<string>();

        if (body.RootElement.TryGetProperty("key", out JsonElement singleKey) &&
            singleKey.GetString() is string k && !string.IsNullOrWhiteSpace(k))
        {
            keys.Add(k);
        }
        else if (body.RootElement.TryGetProperty("keys", out JsonElement multiKeys) &&
                 multiKeys.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in multiKeys.EnumerateArray())
            {
                if (item.GetString() is string mk && !string.IsNullOrWhiteSpace(mk))
                {
                    keys.Add(mk);
                }
            }
        }

        int delayMs = body.RootElement.TryGetProperty("delayMs", out JsonElement d) ? d.GetInt32() : 160;

        string beforeJson = await Application.Current.Dispatcher.InvokeAsync(async () =>
            await _webView.CoreWebView2.ExecuteScriptAsync(SemanticDomInspector.ActiveElementScript)).Task.Unwrap();

        foreach (string keyName in keys)
        {
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                if (string.Equals(keyName, "F1", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(keyName, "Home", StringComparison.OrdinalIgnoreCase))
                {
                    _shell.NavigateToHome();
                    return;
                }

                if (Enum.TryParse<Key>(keyName, ignoreCase: true, out Key parsedKey))
                {
                    await _scriptInjector.DispatchNavigationKeyAsync(_webView, parsedKey);
                }
            }).Task.Unwrap();

            await Task.Delay(Math.Clamp(delayMs, 40, 2000));
        }

        string afterJson = await Application.Current.Dispatcher.InvokeAsync(async () =>
            await _webView.CoreWebView2.ExecuteScriptAsync(SemanticDomInspector.ActiveElementScript)).Task.Unwrap();

        string combined = $$"""
            {
              "keys": {{JsonSerializer.Serialize(keys)}},
              "before": {{beforeJson}},
              "after": {{afterJson}}
            }
            """;
        await WriteRawJsonAsync(context.Response, 200, combined);
    }

    private async Task HandleEvalAsync(HttpListenerContext context)
    {
        using JsonDocument body = await ReadJsonBodyAsync(context.Request);
        string? script = body.RootElement.TryGetProperty("script", out JsonElement s) ? s.GetString() : null;
        if (string.IsNullOrWhiteSpace(script))
        {
            await WriteJsonAsync(context.Response, 400, new { error = "Missing 'script' field." });
            return;
        }

        string resultJson = await Application.Current.Dispatcher.InvokeAsync(async () =>
            await _webView.CoreWebView2.ExecuteScriptAsync(script)).Task.Unwrap();

        await WriteRawJsonAsync(context.Response, 200, resultJson);
    }

    private async Task HandleScreenshotAsync(HttpListenerContext context)
    {
        int width = int.TryParse(context.Request.QueryString["width"], out int w) ? w : 960;
        int quality = int.TryParse(context.Request.QueryString["quality"], out int q) ? q : 60;
        string? customPath = context.Request.QueryString["path"];

        string outputPath = !string.IsNullOrWhiteSpace(customPath)
            ? customPath
            : Path.Combine(Path.GetTempPath(), "TvKioskWrapper", "latest_screenshot.jpg");

        var result = await Application.Current.Dispatcher.InvokeAsync(async () =>
            await ScreenshotCapturer.CaptureDownscaledJpegAsync(_webView, outputPath, width, quality)).Task.Unwrap();

        await WriteJsonAsync(context.Response, 200, new
        {
            path = result.Path,
            width = result.Width,
            height = result.Height,
            bytes = result.Bytes
        });
    }

    private static async Task<JsonDocument> ReadJsonBodyAsync(HttpListenerRequest request)
    {
        using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
        string text = await reader.ReadToEndAsync();
        return JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text);
    }

    private static async Task WriteJsonAsync(HttpListenerResponse response, int statusCode, object payload)
    {
        string json = JsonSerializer.Serialize(payload);
        await WriteRawJsonAsync(response, statusCode, json);
    }

    private static async Task WriteRawJsonAsync(HttpListenerResponse response, int statusCode, string json)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        response.StatusCode = statusCode;
        response.ContentType = "application/json; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
        response.Close();
    }

    public void Dispose()
    {
        _cts?.Cancel();
        if (_listener.IsListening)
        {
            _listener.Stop();
        }
        _listener.Close();
    }
}

