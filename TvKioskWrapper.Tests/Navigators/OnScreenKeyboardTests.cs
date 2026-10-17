using TvKioskWrapper.Tests.Infrastructure;
using Xunit;

namespace TvKioskWrapper.Tests.Navigators;

/// <summary>
/// Verifies the D-Pad on-screen keyboard: it opens for a focused text field, types via Enter,
/// and closes on Escape. Uses compact /eval queries only (no screenshots).
/// </summary>
[Collection(KioskAutomationCollection.CollectionName)]
[Trait("Feature", "osk")]
public sealed class OnScreenKeyboardTests(KioskAutomationFixture kiosk)
{
    private sealed record OskState(bool Visible, string? Value, bool IsInputFocused);

    private static string GetStateScript(string inputExpression) =>
        $"({{ visible: !!(window.__tvKioskOsk && window.__tvKioskOsk.isVisible()), value: {inputExpression} ? {inputExpression}.value : null, isInputFocused: document.activeElement === {inputExpression} }})";

    [Fact]
    [Trait("Navigator", "primevideo")]
    public async Task PrimeVideo_SearchField_ShowsKeyboard_TypesAndCloses()
    {
        await kiosk.NavigateAndWaitForNavigatorAsync("https://www.primevideo.com", "primevideo");

        // Open the search dropdown, then focus its input (as the navigator does on Enter).
        await kiosk.EvalAsync<bool>("(document.querySelector('[data-testid=\"pv-nav-search-dropdown-trigger\"]').click(), true)");
        await kiosk.WaitUntilAsync(
            async () => await kiosk.EvalAsync<bool>("!!document.querySelector('#pv-search-nav')"),
            "Prime Video search input");

        await AssertKeyboardTypesAndClosesAsync("document.querySelector('#pv-search-nav')");
    }

    [Fact]
    [Trait("Navigator", "hbomax")]
    public async Task HboMax_SearchField_ShowsKeyboard_TypesAndCloses()
    {
        await kiosk.NavigateAndWaitForNavigatorAsync("https://play.hbomax.com/search", "hbomax");

        await kiosk.WaitUntilAsync(
            async () => await kiosk.EvalAsync<bool>("!!document.querySelector('input[data-testid=\"searchBar_field\"]')"),
            "HBO Max search input");

        await AssertKeyboardTypesAndClosesAsync("document.querySelector('input[data-testid=\"searchBar_field\"]')");
    }

    private async Task AssertKeyboardTypesAndClosesAsync(string inputExpression)
    {
        string stateScript = GetStateScript(inputExpression);

        // Blur first so Enter reliably (re)opens the keyboard regardless of auto-open on focus.
        await kiosk.EvalAsync<bool>($"(document.activeElement && document.activeElement.blur(), {inputExpression}.focus(), true)");

        // Enter on a focused text field opens the keyboard.
        await kiosk.SendKeyAsync("Enter");
        OskState opened = await kiosk.EvalAsync<OskState>(stateScript);
        Assert.True(opened.Visible, "On-screen keyboard did not open for the focused text field.");

        // Cursor starts on 'q'; Enter types it into the real input.
        await kiosk.SendKeyAsync("Enter");
        OskState typed = await kiosk.EvalAsync<OskState>(stateScript);
        Assert.True(typed.Visible);
        Assert.Equal("q", typed.Value);

        // Right then Enter types the next key ('w').
        await kiosk.SendKeyAsync("Right");
        await kiosk.SendKeyAsync("Enter");
        OskState typedMore = await kiosk.EvalAsync<OskState>(stateScript);
        Assert.Equal("qw", typedMore.Value);

        // Escape closes the keyboard and relinquishes focus on the input.
        await kiosk.SendKeyAsync("Escape");
        OskState closed = await kiosk.EvalAsync<OskState>(stateScript);
        string activeTag = await kiosk.EvalAsync<string>("(document.activeElement && document.activeElement.tagName) || 'null'");
        string activeTestId = await kiosk.EvalAsync<string>("(document.activeElement && document.activeElement.getAttribute('data-testid')) || 'null'");
        Assert.False(closed.Visible, "On-screen keyboard did not close on Escape.");
        Assert.False(closed.IsInputFocused, $"Focus on input was not relinquished on Escape (activeTag={activeTag}, activeTestId={activeTestId}).");
        Assert.Equal("qw", closed.Value);
    }
}

