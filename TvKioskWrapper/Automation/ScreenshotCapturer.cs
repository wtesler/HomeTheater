using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace TvKioskWrapper.Automation;

/// <summary>
/// Captures downscaled, compressed JPEG screenshots from WebView2 to minimize vision token usage.
/// </summary>
public static class ScreenshotCapturer
{
    public static async Task<(string Path, int Width, int Height, long Bytes)> CaptureDownscaledJpegAsync(
        WebView2 webView,
        string outputPath,
        int targetWidth = 960,
        int jpegQuality = 60)
    {
        if (webView.CoreWebView2 is null)
        {
            throw new InvalidOperationException("CoreWebView2 is not initialized.");
        }

        string? directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var rawStream = new MemoryStream();
        await webView.CoreWebView2.CapturePreviewAsync(
            CoreWebView2CapturePreviewImageFormat.Png,
            rawStream);

        rawStream.Position = 0;
        var decoder = new PngBitmapDecoder(
            rawStream,
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);

        BitmapSource source = decoder.Frames[0];
        double scale = source.PixelWidth > targetWidth
            ? (double)targetWidth / source.PixelWidth
            : 1.0;

        BitmapSource scaled = scale < 1.0
            ? new TransformedBitmap(source, new ScaleTransform(scale, scale))
            : source;

        var encoder = new JpegBitmapEncoder
        {
            QualityLevel = Math.Clamp(jpegQuality, 20, 95)
        };
        encoder.Frames.Add(BitmapFrame.Create(scaled));

        await using (var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
        {
            encoder.Save(fileStream);
        }

        var fileInfo = new FileInfo(outputPath);
        return (fileInfo.FullName, scaled.PixelWidth, scaled.PixelHeight, fileInfo.Length);
    }
}

