using System.Text.Json.Serialization;

namespace TvKioskWrapper.Tests.Infrastructure;

public sealed record ElementRect(
    [property: JsonPropertyName("x")] int X,
    [property: JsonPropertyName("y")] int Y,
    [property: JsonPropertyName("w")] int Width,
    [property: JsonPropertyName("h")] int Height);

public sealed record FocusedElementInfo(
    [property: JsonPropertyName("tag")] string? Tag,
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("testId")] string? TestId,
    [property: JsonPropertyName("ariaLabel")] string? AriaLabel,
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("href")] string? Href,
    [property: JsonPropertyName("rect")] ElementRect? Rect)
{
    public string Describe() =>
        $"{Tag}[testId={TestId ?? "-"}, aria={AriaLabel ?? "-"}, text={Text ?? "-"} @ ({Rect?.X},{Rect?.Y} {Rect?.Width}x{Rect?.Height})]";
}

public sealed record AutomationStatus(
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("navigatorMode")] string? NavigatorMode,
    [property: JsonPropertyName("currentRowId")] string? CurrentRowId,
    [property: JsonPropertyName("rowCount")] int RowCount,
    [property: JsonPropertyName("domActiveElement")] FocusedElementInfo? DomActiveElement,
    [property: JsonPropertyName("kioskFocusedElement")] FocusedElementInfo? KioskFocusedElement);

public sealed record KeyDispatchResult(
    [property: JsonPropertyName("keys")] IReadOnlyList<string> Keys,
    [property: JsonPropertyName("before")] AutomationStatus? Before,
    [property: JsonPropertyName("after")] AutomationStatus? After);

