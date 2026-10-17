using TvKioskWrapper.Tests.Infrastructure;
using Xunit;

namespace TvKioskWrapper.Tests.Navigators;

[Collection(KioskAutomationCollection.CollectionName)]
[Trait("Navigator", "hbomax")]
public sealed class HboMaxNavigatorTests(KioskAutomationFixture kiosk)
{
    [Fact]
    public async Task HboMax_MountsSemanticNavigator_AndNavigatesTopNavAndContent()
    {
        AutomationStatus initial = await kiosk.NavigateAndWaitForNavigatorAsync(
            url: "https://play.hbomax.com",
            expectedNavigatorMode: "hbomax");

        Assert.Equal("hbomax", initial.NavigatorMode);
        Assert.True(initial.RowCount >= 2, $"Expected >= 2 semantic rows on HBO Max, got {initial.RowCount}.");
        Assert.NotNull(initial.KioskFocusedElement);

        // Move Up to the Top Navigation Bar
        KeyDispatchResult upResult = await kiosk.SendKeyAsync("Up");
        for (int i = 0; i < 3 && upResult.After?.CurrentRowId != "top-nav"; i++)
        {
            upResult = await kiosk.SendKeyAsync("Up");
        }
        Assert.Equal("top-nav", upResult.After?.CurrentRowId);

        // Move Right within Top Navigation Bar and verify focus moves to a new nav item
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

