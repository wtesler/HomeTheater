using System.Windows;
using System.Windows.Input;
using TvKioskWrapper.Shell;
using Xunit;

namespace TvKioskWrapper.Tests.Shell;

public sealed class SettingsMenuTests
{
    private static Task RunInStaAsync(Action action)
    {
        var tcs = new TaskCompletionSource();
        var thread = new Thread(() =>
        {
            try
            {
                action();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return tcs.Task;
    }

    [Fact]
    public async Task SettingsMenu_VideoPresetSelection_TriggersEvent()
    {
        await RunInStaAsync(() =>
        {
            var owner = new Window();
            int selectedIndex = -1;
            string selectedName = string.Empty;

            var menu = new SettingsMenuModalWindow(
                owner,
                () => new[] { "Standard", "Cinema Home" },
                () => Array.Empty<AudioDeviceManager.AudioDeviceInfo>(),
                () => null);

            menu.VideoPresetSelected += (idx, name) =>
            {
                selectedIndex = idx;
                selectedName = name;
            };

            menu.ShowMenu();
            Assert.True(menu.IsMenuOpen);

            // On Main menu, index 0 is "Video". Press Enter to drill down into Video
            menu.HandleKey(Key.Enter);

            // In Video submenu, move down to "Cinema Home" (index 1)
            menu.HandleKey(Key.Down);

            // Press Enter to confirm selection
            menu.HandleKey(Key.Enter);

            Assert.Equal(1, selectedIndex);
            Assert.Equal("Cinema Home", selectedName);
            Assert.False(menu.IsMenuOpen);
        });
    }

    [Fact]
    public async Task SettingsMenu_AudioDeviceSelection_TriggersEvent()
    {
        await RunInStaAsync(() =>
        {
            var owner = new Window();
            var dummyDevices = new[]
            {
                new AudioDeviceManager.AudioDeviceInfo("id1", "Speakers (Realtek)"),
                new AudioDeviceManager.AudioDeviceInfo("id2", "LG TV (NVIDIA)")
            };

            AudioDeviceManager.AudioDeviceInfo? selectedDevice = null;

            var menu = new SettingsMenuModalWindow(
                owner,
                () => new[] { "Standard", "Cinema Home" },
                () => dummyDevices,
                () => "id1");

            menu.AudioDeviceSelected += dev => selectedDevice = dev;

            menu.ShowMenu();

            // Navigate Down to "Audio" (index 1)
            menu.HandleKey(Key.Down);

            // Press Right arrow to open Audio drilldown context menu
            menu.HandleKey(Key.Right);

            // Select second audio device
            menu.HandleKey(Key.Down);
            menu.HandleKey(Key.Enter);

            Assert.NotNull(selectedDevice);
            Assert.Equal("id2", selectedDevice.Id);
            Assert.Equal("LG TV (NVIDIA)", selectedDevice.Name);
            Assert.False(menu.IsMenuOpen);
        });
    }

    [Fact]
    public async Task SettingsMenu_ForceHomeTheaterSelection_TriggersEvent()
    {
        await RunInStaAsync(() =>
        {
            var owner = new Window();
            bool forceHomeTheaterFired = false;

            var menu = new SettingsMenuModalWindow(
                owner,
                () => new[] { "Standard", "Cinema Home" },
                () => Array.Empty<AudioDeviceManager.AudioDeviceInfo>(),
                () => null);

            menu.ForceHomeTheaterSelected += () => forceHomeTheaterFired = true;

            menu.ShowMenu();

            // Navigate Down twice to "System" (index 2)
            menu.HandleKey(Key.Down);
            menu.HandleKey(Key.Down);

            // Press Enter to drilldown into System
            menu.HandleKey(Key.Enter);

            // In System submenu, press Enter on "Force Home Theater"
            menu.HandleKey(Key.Enter);

            Assert.True(forceHomeTheaterFired);
            Assert.False(menu.IsMenuOpen);
        });
    }

    [Fact]
    public async Task SettingsMenu_LeftArrow_ReturnsToMainMenuFromSubmenu()
    {
        await RunInStaAsync(() =>
        {
            var owner = new Window();
            var menu = new SettingsMenuModalWindow(
                owner,
                () => new[] { "Standard", "Cinema Home" },
                () => Array.Empty<AudioDeviceManager.AudioDeviceInfo>(),
                () => null);

            menu.ShowMenu();

            // Drill down into Video
            menu.HandleKey(Key.Right);

            // Press Left arrow to return to Main Menu
            menu.HandleKey(Key.Left);

            // Press Escape to close from Main Menu
            menu.HandleKey(Key.Escape);

            Assert.False(menu.IsMenuOpen);
        });
    }

    [Fact]
    public async Task SettingsMenu_F2_ClosesMenuImmediately()
    {
        await RunInStaAsync(() =>
        {
            var owner = new Window();
            var menu = new SettingsMenuModalWindow(
                owner,
                () => new[] { "Standard", "Cinema Home" },
                () => Array.Empty<AudioDeviceManager.AudioDeviceInfo>(),
                () => null);

            menu.ShowMenu();
            Assert.True(menu.IsMenuOpen);

            // Pressing F2 again toggles / closes the menu
            menu.HandleKey(Key.F2);

            Assert.False(menu.IsMenuOpen);
        });
    }
}

