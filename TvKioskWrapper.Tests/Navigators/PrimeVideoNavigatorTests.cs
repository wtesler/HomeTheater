using TvKioskWrapper.Tests.Infrastructure;
using Xunit;

namespace TvKioskWrapper.Tests.Navigators;

[Collection(KioskAutomationCollection.CollectionName)]
[Trait("Navigator", "primevideo")]
public sealed class PrimeVideoNavigatorTests(KioskAutomationFixture kiosk)
{
    [Fact]
    public async Task PrimeVideo_MountsSemanticNavigator_AndNavigatesTopNavAndContent()
    {
        AutomationStatus initial = await kiosk.NavigateAndWaitForNavigatorAsync(
            url: "https://www.primevideo.com",
            expectedNavigatorMode: "primevideo");

        Assert.Equal("primevideo", initial.NavigatorMode);
        Assert.True(initial.RowCount >= 2, $"Expected >= 2 semantic rows on Prime Video, got {initial.RowCount}.");
        Assert.NotNull(initial.KioskFocusedElement);

        // Move Up to the Top Navigation Bar
        KeyDispatchResult upResult = await kiosk.SendKeyAsync("Up");
        for (int i = 0; i < 3 && upResult.After?.CurrentRowId != "top-nav"; i++)
        {
            upResult = await kiosk.SendKeyAsync("Up");
        }
        Assert.Equal("top-nav", upResult.After?.CurrentRowId);

        // Move Right within Top Navigation Bar and verify focus moves to the next nav item
        KeyDispatchResult rightResult = await kiosk.SendKeyAsync("Right");
        Assert.Equal("top-nav", rightResult.After?.CurrentRowId);
        Assert.NotEqual(
            rightResult.Before?.KioskFocusedElement?.Describe(),
            rightResult.After?.KioskFocusedElement?.Describe());

        // Move Down back into main content (Hero or first Rail)
        KeyDispatchResult downResult = await kiosk.SendKeyAsync("Down");
        Assert.NotNull(downResult.After?.CurrentRowId);
        Assert.NotEqual("top-nav", downResult.After?.CurrentRowId);
        Assert.NotNull(downResult.After?.KioskFocusedElement);
    }
}

