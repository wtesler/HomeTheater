using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using TvKioskWrapper.Interop;

namespace TvKioskWrapper.Shell;

/// <summary>
/// A 10-foot modal popup window for selecting LG TV video presets via D-Pad and Enter.
/// Renders a sleek, centered TV dialog over a dimmed backdrop without stealing keyboard focus permanently.
/// </summary>
public sealed class VideoPresetModalWindow : Window
{
    private readonly Window _ownerWindow;
    private readonly StackPanel _itemsStackPanel;
    private readonly Storyboard _fadeInStoryboard;
    private readonly Storyboard _fadeOutStoryboard;

    private IReadOnlyList<string> _presetNames = Array.Empty<string>();
    private readonly List<Border> _itemBorders = new();
    private int _highlightedIndex = 0;

    public bool IsModalOpen { get; private set; }

    public event Action<int>? PresetSelected;
    public event Action? ModalClosed;

    public VideoPresetModalWindow(Window ownerWindow)
    {
        _ownerWindow = ownerWindow;

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        Focusable = true;
        Opacity = 0;

        _itemsStackPanel = new StackPanel
        {
            Orientation = Orientation.Vertical
        };

        // Header
        var iconText = new TextBlock
        {
            Text = "📺",
            FontSize = 20,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var iconBadge = new Border
        {
            Width = 42,
            Height = 42,
            CornerRadius = new CornerRadius(21),
            Background = new SolidColorBrush(Color.FromArgb(34, 255, 255, 255)),
            Child = iconText
        };

        var titleLabel = new TextBlock
        {
            Text = "PICTURE PRESET",
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175))
        };

        var subtitleLabel = new TextBlock
        {
            Text = "Select Display Mode",
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 2, 0, 0)
        };

        var headerTextPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin = new Thickness(14, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Children = { titleLabel, subtitleLabel }
        };

        var headerPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { iconBadge, headerTextPanel }
        };

        // Top separator
        var topSeparator = new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
            Margin = new Thickness(0, 16, 0, 12)
        };

        // Bottom separator
        var bottomSeparator = new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
            Margin = new Thickness(0, 12, 0, 14)
        };

        // Dialog card layout
        var cardLayout = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Children =
            {
                headerPanel,
                topSeparator,
                _itemsStackPanel
            }
        };

        var card = new Border
        {
            Width = 460,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Background = new SolidColorBrush(Color.FromArgb(242, 21, 23, 34)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(64, 255, 255, 255)),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(22),
            Padding = new Thickness(26, 24, 26, 20),
            Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 36,
                ShadowDepth = 10,
                Opacity = 0.85
            },
            Child = cardLayout
        };

        // Dimmed backdrop covering full window
        var rootBackdrop = new Grid
        {
            Background = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)),
            Children = { card }
        };

        rootBackdrop.MouseLeftButtonDown += (s, e) =>
        {
            if (ReferenceEquals(e.OriginalSource, rootBackdrop))
            {
                CloseModal();
            }
        };

        Content = rootBackdrop;

        // Fade in animation
        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(120));
        _fadeInStoryboard = new Storyboard();
        _fadeInStoryboard.Children.Add(fadeIn);
        Storyboard.SetTarget(fadeIn, this);
        Storyboard.SetTargetProperty(fadeIn, new PropertyPath(OpacityProperty));

        // Fade out animation
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(120));
        _fadeOutStoryboard = new Storyboard();
        _fadeOutStoryboard.Children.Add(fadeOut);
        Storyboard.SetTarget(fadeOut, this);
        Storyboard.SetTargetProperty(fadeOut, new PropertyPath(OpacityProperty));
        _fadeOutStoryboard.Completed += (s, e) =>
        {
            if (Opacity <= 0.05)
            {
                Hide();
                ModalClosed?.Invoke();
            }
        };
    }

    private static FrameworkElement CreateHintPill(string key, string label)
    {
        var keyBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(6, 2, 6, 2),
            Child = new TextBlock
            {
                Text = key,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 220))
            }
        };

        var labelText = new TextBlock
        {
            Text = label,
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175)),
            Margin = new Thickness(6, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { keyBorder, labelText }
        };
    }

    /// <summary>
    /// Displays the modal with the supplied preset names.
    /// </summary>
    public void ShowModal(IReadOnlyList<string> presetNames)
    {
        if (presetNames.Count == 0)
        {
            return;
        }

        _presetNames = presetNames;
        _highlightedIndex = 0;

        BuildItems();
        UpdateVisuals();

        double ownerLeft = _ownerWindow.WindowState == WindowState.Maximized ? 0 : _ownerWindow.Left;
        double ownerTop = _ownerWindow.WindowState == WindowState.Maximized ? 0 : _ownerWindow.Top;
        double ownerWidth = _ownerWindow.ActualWidth > 0 ? _ownerWindow.ActualWidth : 1920;
        double ownerHeight = _ownerWindow.ActualHeight > 0 ? _ownerWindow.ActualHeight : 1080;

        Left = ownerLeft;
        Top = ownerTop;
        Width = ownerWidth;
        Height = ownerHeight;

        IsModalOpen = true;
        _fadeOutStoryboard.Stop();
        Show();
        Activate();
        Focus();
        _fadeInStoryboard.Begin();
    }

    /// <summary>
    /// Closes the modal popup without making changes.
    /// </summary>
    public void CloseModal()
    {
        if (!IsModalOpen)
        {
            return;
        }

        IsModalOpen = false;
        _fadeInStoryboard.Stop();
        _fadeOutStoryboard.Begin();
    }

    /// <summary>
    /// Processes remote/keyboard navigation keys for the modal popup.
    /// </summary>
    public void HandleKey(Key key)
    {
        if (!IsModalOpen)
        {
            return;
        }

        switch (key)
        {
            case Key.Up:
                MoveSelection(-1);
                break;
            case Key.Down:
                MoveSelection(1);
                break;
            case Key.Enter:
                ConfirmSelection();
                break;
            case Key.Escape:
            case Key.Back:
            case Key.BrowserBack:
            case Key.F3:
                CloseModal();
                break;
        }
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (IsModalOpen && e.Key is Key.Up or Key.Down or Key.Enter or Key.Escape or Key.Back or Key.F3)
        {
            e.Handled = true;
            HandleKey(e.Key);
        }
    }

    private void MoveSelection(int delta)
    {
        if (_presetNames.Count == 0)
        {
            return;
        }

        _highlightedIndex = (_highlightedIndex + delta + _presetNames.Count) % _presetNames.Count;
        UpdateVisuals();
    }

    private void ConfirmSelection()
    {
        if (!IsModalOpen || _highlightedIndex < 0 || _highlightedIndex >= _presetNames.Count)
        {
            return;
        }

        int selected = _highlightedIndex;
        CloseModal();
        PresetSelected?.Invoke(selected);
    }

    private void BuildItems()
    {
        _itemsStackPanel.Children.Clear();
        _itemBorders.Clear();

        for (int i = 0; i < _presetNames.Count; i++)
        {
            int index = i;
            string presetName = _presetNames[i];

            var indicatorText = new TextBlock
            {
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Width = 20,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var nameText = new TextBlock
            {
                Text = presetName,
                FontSize = 18,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 10, 0)
            };

            var itemGrid = new Grid();
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Grid.SetColumn(indicatorText, 0);
            Grid.SetColumn(nameText, 1);

            itemGrid.Children.Add(indicatorText);
            itemGrid.Children.Add(nameText);

            var itemBorder = new Border
            {
                CornerRadius = new CornerRadius(14),
                Margin = new Thickness(0, 4, 0, 4),
                Padding = new Thickness(16, 12, 16, 12),
                Cursor = Cursors.Hand,
                Child = itemGrid,
                Tag = index
            };

            itemBorder.MouseEnter += (s, e) =>
            {
                _highlightedIndex = index;
                UpdateVisuals();
            };

            itemBorder.MouseLeftButtonDown += (s, e) =>
            {
                _highlightedIndex = index;
                ConfirmSelection();
            };

            _itemBorders.Add(itemBorder);
            _itemsStackPanel.Children.Add(itemBorder);
        }
    }

    private void UpdateVisuals()
    {
        for (int i = 0; i < _itemBorders.Count; i++)
        {
            Border border = _itemBorders[i];
            if (border.Child is not Grid grid)
            {
                continue;
            }

            var indicatorText = (TextBlock)grid.Children[0];
            var nameText = (TextBlock)grid.Children[1];

            bool isHighlighted = (i == _highlightedIndex);

            if (isHighlighted)
            {
                border.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                border.BorderBrush = new SolidColorBrush(Color.FromRgb(147, 197, 253));
                border.BorderThickness = new Thickness(2);
                border.Effect = new DropShadowEffect
                {
                    Color = Color.FromRgb(37, 99, 235),
                    BlurRadius = 16,
                    ShadowDepth = 0,
                    Opacity = 0.6
                };

                indicatorText.Text = "▶";
                indicatorText.Foreground = Brushes.White;

                nameText.Foreground = Brushes.White;
                nameText.FontWeight = FontWeights.Bold;
            }
            else
            {
                border.Background = new SolidColorBrush(Color.FromArgb(16, 255, 255, 255));
                border.BorderBrush = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255));
                border.BorderThickness = new Thickness(1);
                border.Effect = null;

                indicatorText.Text = "";
                indicatorText.Foreground = Brushes.Transparent;

                nameText.Foreground = new SolidColorBrush(Color.FromRgb(209, 213, 219));
                nameText.FontWeight = FontWeights.Normal;
            }
        }
    }
}

