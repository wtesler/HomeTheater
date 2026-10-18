using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using TvKioskWrapper.Interop;

namespace TvKioskWrapper.Shell;

/// <summary>
/// A lightweight, non-activating 10-foot HUD toast window that displays the currently active audio device.
/// Designed specifically for TV viewing distances without stealing keyboard focus from WebView2.
/// </summary>
public sealed class AudioToastWindow : Window
{
    private readonly Window _ownerWindow;
    private readonly TextBlock _deviceNameText;
    private readonly DispatcherTimer _hideTimer;
    private readonly Storyboard _fadeInStoryboard;
    private readonly Storyboard _fadeOutStoryboard;

    public AudioToastWindow(Window ownerWindow)
    {
        _ownerWindow = ownerWindow;

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        Focusable = false;
        ShowActivated = false;
        SizeToContent = SizeToContent.WidthAndHeight;
        Opacity = 0;

        _deviceNameText = new TextBlock
        {
            Foreground = Brushes.White,
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            MaxWidth = 650,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var subtitleText = new TextBlock
        {
            Text = "AUDIO OUTPUT",
            Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 160)),
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 3)
        };

        var textPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { subtitleText, _deviceNameText }
        };

        var iconText = new TextBlock
        {
            Text = "🔊",
            FontSize = 18,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var iconBadge = new Border
        {
            Width = 40,
            Height = 40,
            CornerRadius = new CornerRadius(20),
            Background = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255)),
            Margin = new Thickness(0, 0, 16, 0),
            Child = iconText
        };

        var contentGrid = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { iconBadge, textPanel }
        };

        var rootCard = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(235, 18, 18, 24)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(64, 255, 255, 255)),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(24),
            Padding = new Thickness(22, 14, 28, 14),
            Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 24,
                ShadowDepth = 4,
                Opacity = 0.6
            },
            Child = contentGrid
        };

        Content = rootCard;

        _hideTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2.8)
        };
        _hideTimer.Tick += OnHideTimerTick;

        // Fade in animation
        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150));
        _fadeInStoryboard = new Storyboard();
        _fadeInStoryboard.Children.Add(fadeIn);
        Storyboard.SetTarget(fadeIn, this);
        Storyboard.SetTargetProperty(fadeIn, new PropertyPath(OpacityProperty));

        // Fade out animation
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(250));
        _fadeOutStoryboard = new Storyboard();
        _fadeOutStoryboard.Children.Add(fadeOut);
        Storyboard.SetTarget(fadeOut, this);
        Storyboard.SetTargetProperty(fadeOut, new PropertyPath(OpacityProperty));
        _fadeOutStoryboard.Completed += (s, e) =>
        {
            if (Opacity <= 0.05)
            {
                Hide();
            }
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        nint hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != nint.Zero)
        {
            int exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
            NativeMethods.SetWindowLong(
                hwnd,
                NativeMethods.GWL_EXSTYLE,
                exStyle | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_TRANSPARENT);
        }
    }

    /// <summary>
    /// Displays or updates the audio toast with the newly selected device name.
    /// </summary>
    public void ShowToast(string deviceName)
    {
        _deviceNameText.Text = deviceName;
        UpdateLayout();

        double ownerLeft = _ownerWindow.WindowState == WindowState.Maximized ? 0 : _ownerWindow.Left;
        double ownerTop = _ownerWindow.WindowState == WindowState.Maximized ? 0 : _ownerWindow.Top;
        double ownerWidth = _ownerWindow.ActualWidth > 0 ? _ownerWindow.ActualWidth : 1920;

        double targetWidth = ActualWidth > 0 ? ActualWidth : 420;
        Left = ownerLeft + Math.Max(0, (ownerWidth - targetWidth) / 2);
        Top = ownerTop + 48;

        _hideTimer.Stop();
        _fadeOutStoryboard.Stop();

        if (!IsVisible)
        {
            Show();
            _fadeInStoryboard.Begin();
        }
        else
        {
            Opacity = 1.0;
        }

        _hideTimer.Start();
    }

    private void OnHideTimerTick(object? sender, EventArgs e)
    {
        _hideTimer.Stop();
        _fadeOutStoryboard.Begin();
    }
}

