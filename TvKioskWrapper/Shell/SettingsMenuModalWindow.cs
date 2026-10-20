using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using TvKioskWrapper.Shell;

namespace TvKioskWrapper.Shell;

/// <summary>
/// A 10-foot popup settings menu positioned in the top-left of the screen.
/// Provides D-Pad (Up, Down, Left, Right, Enter, Escape, Back, F2) and mouse navigation
/// for configuring Video display presets, Audio playback output devices, and System settings.
/// </summary>
public sealed class SettingsMenuModalWindow : Window
{
    public enum SettingsCategory
    {
        Video = 0,
        Sound = 1,
        Clarity = 2,
        Output = 3,
        System = 4
    }

    private enum MenuColumn
    {
        Main,
        Submenu
    }

    private readonly Window _ownerWindow;
    private readonly Func<IReadOnlyList<string>> _videoPresetsProvider;
    private readonly Func<IReadOnlyList<string>> _soundPresetsProvider;
    private readonly Func<IReadOnlyList<string>> _motionPresetsProvider;
    private readonly Func<IReadOnlyList<AudioDeviceManager.AudioDeviceInfo>> _audioDevicesProvider;
    private readonly Func<string?> _defaultAudioDeviceProvider;

    private readonly Border _mainMenuCard;
    private readonly Border _submenuCard;
    private readonly StackPanel _mainItemsStackPanel;
    private readonly StackPanel _submenuItemsStackPanel;

    private readonly TextBlock _submenuIconText;
    private readonly TextBlock _submenuTitleLabel;
    private readonly TextBlock _submenuSubtitleLabel;

    private readonly Storyboard _fadeInStoryboard;
    private readonly Storyboard _fadeOutStoryboard;

    private readonly List<Border> _mainItemBorders = new();
    private readonly List<Border> _submenuItemBorders = new();

    private MenuColumn _focusedColumn = MenuColumn.Main;
    private int _mainHighlightedIndex = 0;
    private int _subHighlightedIndex = 0;
    private SettingsCategory _activeCategory = SettingsCategory.Video;

    private IReadOnlyList<string> _currentVideoPresets = Array.Empty<string>();
    private IReadOnlyList<string> _currentSoundPresets = Array.Empty<string>();
    private IReadOnlyList<string> _currentMotionPresets = Array.Empty<string>();
    private IReadOnlyList<AudioDeviceManager.AudioDeviceInfo> _currentAudioDevices = Array.Empty<AudioDeviceManager.AudioDeviceInfo>();
    private int _subItemCount = 0;

    public bool IsMenuOpen { get; private set; }

    public event Action<int, string>? VideoPresetSelected;
    public event Action<int, string>? SoundPresetSelected;
    public event Action<int, string>? MotionPresetSelected;
    public event Action<AudioDeviceManager.AudioDeviceInfo>? AudioDeviceSelected;
    public event Action? ForceHomeTheaterSelected;
    public event Action? MenuClosed;

    public SettingsMenuModalWindow(
        Window ownerWindow,
        Func<IReadOnlyList<string>> videoPresetsProvider,
        Func<IReadOnlyList<string>> soundPresetsProvider,
        Func<IReadOnlyList<string>> motionPresetsProvider,
        Func<IReadOnlyList<AudioDeviceManager.AudioDeviceInfo>> audioDevicesProvider,
        Func<string?> defaultAudioDeviceProvider)
    {
        _ownerWindow = ownerWindow;
        _videoPresetsProvider = videoPresetsProvider;
        _soundPresetsProvider = soundPresetsProvider;
        _motionPresetsProvider = motionPresetsProvider;
        _audioDevicesProvider = audioDevicesProvider;
        _defaultAudioDeviceProvider = defaultAudioDeviceProvider;

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
        Topmost = true;
        ShowInTaskbar = false;
        Focusable = true;
        Opacity = 0;

        // 1. Build Main Menu Card
        _mainItemsStackPanel = new StackPanel { Orientation = Orientation.Vertical };
        _mainMenuCard = BuildMainMenuCard();

        // 2. Build Submenu Card (Drilldown)
        _submenuItemsStackPanel = new StackPanel { Orientation = Orientation.Vertical };
        (_submenuCard, _submenuIconText, _submenuTitleLabel, _submenuSubtitleLabel) = BuildSubmenuCard();
        _submenuCard.Visibility = Visibility.Collapsed;

        // 3. Container holding Main Menu and Drilldown Submenu side-by-side
        var menuContainer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(48, 48, 0, 0),
            Children = { _mainMenuCard, _submenuCard }
        };

        // 4. Root dimmed backdrop covering entire window
        var rootBackdrop = new Grid
        {
            Background = Brushes.Transparent,
            Children = { menuContainer }
        };

        rootBackdrop.MouseLeftButtonDown += (s, e) =>
        {
            if (ReferenceEquals(e.OriginalSource, rootBackdrop))
            {
                CloseMenu();
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
                MenuClosed?.Invoke();
            }
        };

        BuildMainMenuItems();
    }

    private Border BuildMainMenuCard()
    {
        // Header
        var iconText = new TextBlock
        {
            Text = "⚙️",
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
            Text = "SETTINGS",
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175))
        };

        var subtitleLabel = new TextBlock
        {
            Text = "Quick Settings",
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

        var topSeparator = new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
            Margin = new Thickness(0, 16, 0, 12)
        };

        var cardLayout = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Children =
            {
                headerPanel,
                topSeparator,
                _mainItemsStackPanel
            }
        };

        return new Border
        {
            Width = 280,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Background = new SolidColorBrush(Color.FromArgb(242, 21, 23, 34)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(64, 255, 255, 255)),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(20, 20, 20, 18),
            Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 36,
                ShadowDepth = 10,
                Opacity = 0.85
            },
            Child = cardLayout
        };
    }

    private (Border Card, TextBlock Icon, TextBlock Title, TextBlock Subtitle) BuildSubmenuCard()
    {
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
            Text = "CATEGORY",
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175))
        };

        var subtitleLabel = new TextBlock
        {
            Text = "Options",
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

        var topSeparator = new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
            Margin = new Thickness(0, 16, 0, 12)
        };

        var cardLayout = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Children =
            {
                headerPanel,
                topSeparator,
                _submenuItemsStackPanel
            }
        };

        var card = new Border
        {
            MinWidth = 360,
            MaxWidth = 460,
            Margin = new Thickness(16, 0, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Background = new SolidColorBrush(Color.FromArgb(242, 21, 23, 34)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(64, 255, 255, 255)),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(22, 20, 22, 18),
            Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 36,
                ShadowDepth = 10,
                Opacity = 0.85
            },
            Child = cardLayout
        };

        return (card, iconText, titleLabel, subtitleLabel);
    }

    private void BuildMainMenuItems()
    {
        _mainItemsStackPanel.Children.Clear();
        _mainItemBorders.Clear();

        (string name, string icon)[] categories =
        {
            ("Video", "📺"),
            ("Sound", "🎵"),
            ("Clarity", "✨"),
            ("Output", "🔊"),
            ("System", "⚙️")
        };

        for (int i = 0; i < categories.Length; i++)
        {
            int index = i;
            var (name, icon) = categories[i];

            var indicatorText = new TextBlock
            {
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Width = 18,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var iconText = new TextBlock
            {
                Text = icon,
                FontSize = 17,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };

            var nameText = new TextBlock
            {
                Text = name,
                FontSize = 17,
                VerticalAlignment = VerticalAlignment.Center
            };

            var chevronText = new TextBlock
            {
                Text = "›",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175))
            };

            var itemGrid = new Grid();
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Grid.SetColumn(indicatorText, 0);
            Grid.SetColumn(iconText, 1);
            Grid.SetColumn(nameText, 2);
            Grid.SetColumn(chevronText, 3);

            itemGrid.Children.Add(indicatorText);
            itemGrid.Children.Add(iconText);
            itemGrid.Children.Add(nameText);
            itemGrid.Children.Add(chevronText);

            var itemBorder = new Border
            {
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(0, 3, 0, 3),
                Padding = new Thickness(14, 11, 14, 11),
                Cursor = Cursors.Hand,
                Child = itemGrid,
                Tag = index
            };

            itemBorder.MouseEnter += (s, e) =>
            {
                if (_focusedColumn == MenuColumn.Main)
                {
                    _mainHighlightedIndex = index;
                    UpdateVisuals();
                }
            };

            itemBorder.MouseLeftButtonDown += (s, e) =>
            {
                _mainHighlightedIndex = index;
                OpenSubmenu((SettingsCategory)index);
            };

            _mainItemBorders.Add(itemBorder);
            _mainItemsStackPanel.Children.Add(itemBorder);
        }
    }

    private void OpenSubmenu(SettingsCategory category)
    {
        _activeCategory = category;
        _focusedColumn = MenuColumn.Submenu;
        _subHighlightedIndex = 0;

        _submenuItemsStackPanel.Children.Clear();
        _submenuItemBorders.Clear();

        switch (category)
        {
            case SettingsCategory.Video:
                PopulateVideoSubmenu();
                break;
            case SettingsCategory.Sound:
                PopulateSoundSubmenu();
                break;
            case SettingsCategory.Clarity:
                PopulateClaritySubmenu();
                break;
            case SettingsCategory.Output:
                PopulateOutputSubmenu();
                break;
            case SettingsCategory.System:
                PopulateSystemSubmenu();
                break;
        }

        _submenuCard.Visibility = Visibility.Visible;
        UpdateVisuals();
    }

    private void PopulateVideoSubmenu()
    {
        _submenuIconText.Text = "📺";
        _submenuTitleLabel.Text = "VIDEO";
        _submenuSubtitleLabel.Text = "Select Picture Mode";

        IReadOnlyList<string> presets = _videoPresetsProvider();
        if (presets.Count == 0)
        {
            presets = new[] { "Standard", "Cinema Home" };
        }
        _currentVideoPresets = presets;
        _subItemCount = presets.Count;

        for (int i = 0; i < presets.Count; i++)
        {
            int index = i;
            string presetName = presets[i];

            var indicatorText = new TextBlock
            {
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Width = 18,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var iconText = new TextBlock
            {
                Text = "📺",
                FontSize = 16,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };

            var nameText = new TextBlock
            {
                Text = presetName,
                FontSize = 17,
                VerticalAlignment = VerticalAlignment.Center
            };

            var itemGrid = new Grid();
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Grid.SetColumn(indicatorText, 0);
            Grid.SetColumn(iconText, 1);
            Grid.SetColumn(nameText, 2);

            itemGrid.Children.Add(indicatorText);
            itemGrid.Children.Add(iconText);
            itemGrid.Children.Add(nameText);

            var itemBorder = new Border
            {
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(0, 3, 0, 3),
                Padding = new Thickness(14, 11, 14, 11),
                Cursor = Cursors.Hand,
                Child = itemGrid,
                Tag = index
            };

            itemBorder.MouseEnter += (s, e) =>
            {
                _subHighlightedIndex = index;
                UpdateVisuals();
            };

            itemBorder.MouseLeftButtonDown += (s, e) =>
            {
                _subHighlightedIndex = index;
                ExecuteSubmenuSelection();
            };

            _submenuItemBorders.Add(itemBorder);
            _submenuItemsStackPanel.Children.Add(itemBorder);
        }
    }

    private void PopulateSoundSubmenu()
    {
        _submenuIconText.Text = "🎵";
        _submenuTitleLabel.Text = "SOUND";
        _submenuSubtitleLabel.Text = "Select Sound Mode";

        IReadOnlyList<string> presets = _soundPresetsProvider();
        if (presets.Count == 0)
        {
            presets = new[] { "Standard", "Voice", "Cinema", "AI" };
        }
        _currentSoundPresets = presets;
        _subItemCount = presets.Count;

        for (int i = 0; i < presets.Count; i++)
        {
            int index = i;
            string presetName = presets[i];

            var indicatorText = new TextBlock
            {
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Width = 18,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var iconText = new TextBlock
            {
                Text = "🎵",
                FontSize = 16,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };

            var nameText = new TextBlock
            {
                Text = presetName,
                FontSize = 17,
                VerticalAlignment = VerticalAlignment.Center
            };

            var itemGrid = new Grid();
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Grid.SetColumn(indicatorText, 0);
            Grid.SetColumn(iconText, 1);
            Grid.SetColumn(nameText, 2);

            itemGrid.Children.Add(indicatorText);
            itemGrid.Children.Add(iconText);
            itemGrid.Children.Add(nameText);

            var itemBorder = new Border
            {
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(0, 3, 0, 3),
                Padding = new Thickness(14, 11, 14, 11),
                Cursor = Cursors.Hand,
                Child = itemGrid,
                Tag = index
            };

            itemBorder.MouseEnter += (s, e) =>
            {
                _subHighlightedIndex = index;
                UpdateVisuals();
            };

            itemBorder.MouseLeftButtonDown += (s, e) =>
            {
                _subHighlightedIndex = index;
                ExecuteSubmenuSelection();
            };

            _submenuItemBorders.Add(itemBorder);
            _submenuItemsStackPanel.Children.Add(itemBorder);
        }
    }

    private void PopulateOutputSubmenu()
    {
        _submenuIconText.Text = "🔊";
        _submenuTitleLabel.Text = "OUTPUT";
        _submenuSubtitleLabel.Text = "Select Playback Device";

        IReadOnlyList<AudioDeviceManager.AudioDeviceInfo> devices = _audioDevicesProvider();
        string? defaultDeviceId = _defaultAudioDeviceProvider();

        _currentAudioDevices = devices;
        _subItemCount = devices.Count;

        if (devices.Count == 0)
        {
            var noDevicesText = new TextBlock
            {
                Text = "No active playback devices found",
                FontSize = 15,
                Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175)),
                Padding = new Thickness(14, 12, 14, 12)
            };
            _submenuItemsStackPanel.Children.Add(noDevicesText);
            return;
        }

        int activeIndex = 0;
        for (int i = 0; i < devices.Count; i++)
        {
            int index = i;
            AudioDeviceManager.AudioDeviceInfo device = devices[i];
            bool isCurrentDefault = !string.IsNullOrEmpty(defaultDeviceId) &&
                                   string.Equals(device.Id, defaultDeviceId, StringComparison.OrdinalIgnoreCase);

            if (isCurrentDefault)
            {
                activeIndex = i;
            }

            var indicatorText = new TextBlock
            {
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Width = 18,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var iconText = new TextBlock
            {
                Text = "🔊",
                FontSize = 16,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };

            var nameText = new TextBlock
            {
                Text = device.Name,
                FontSize = 16,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 300,
                VerticalAlignment = VerticalAlignment.Center
            };

            var textStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { nameText }
            };

            if (isCurrentDefault)
            {
                var activeLabel = new TextBlock
                {
                    Text = "Current Default",
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(74, 222, 128)),
                    Margin = new Thickness(0, 2, 0, 0)
                };
                textStack.Children.Add(activeLabel);
            }

            var itemGrid = new Grid();
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Grid.SetColumn(indicatorText, 0);
            Grid.SetColumn(iconText, 1);
            Grid.SetColumn(textStack, 2);

            itemGrid.Children.Add(indicatorText);
            itemGrid.Children.Add(iconText);
            itemGrid.Children.Add(textStack);

            if (isCurrentDefault)
            {
                itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var checkText = new TextBlock
                {
                    Text = "✓",
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(74, 222, 128)),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 0, 0)
                };
                Grid.SetColumn(checkText, 3);
                itemGrid.Children.Add(checkText);
            }

            var itemBorder = new Border
            {
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(0, 3, 0, 3),
                Padding = new Thickness(14, 10, 14, 10),
                Cursor = Cursors.Hand,
                Child = itemGrid,
                Tag = index
            };

            itemBorder.MouseEnter += (s, e) =>
            {
                _subHighlightedIndex = index;
                UpdateVisuals();
            };

            itemBorder.MouseLeftButtonDown += (s, e) =>
            {
                _subHighlightedIndex = index;
                ExecuteSubmenuSelection();
            };

            _submenuItemBorders.Add(itemBorder);
            _submenuItemsStackPanel.Children.Add(itemBorder);
        }

        _subHighlightedIndex = activeIndex;
    }

    private void PopulateClaritySubmenu()
    {
        _submenuIconText.Text = "✨";
        _submenuTitleLabel.Text = "CLARITY";
        _submenuSubtitleLabel.Text = "Select Motion Preset";

        IReadOnlyList<string> presets = _motionPresetsProvider();
        if (presets.Count == 0)
        {
            presets = new[] { "Off", "Cinematic", "Natural" };
        }
        _currentMotionPresets = presets;
        _subItemCount = presets.Count;

        for (int i = 0; i < presets.Count; i++)
        {
            int index = i;
            string presetName = presets[i];

            var indicatorText = new TextBlock
            {
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Width = 18,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var iconText = new TextBlock
            {
                Text = "✨",
                FontSize = 16,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };

            var nameText = new TextBlock
            {
                Text = presetName,
                FontSize = 17,
                VerticalAlignment = VerticalAlignment.Center
            };

            var itemGrid = new Grid();
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Grid.SetColumn(indicatorText, 0);
            Grid.SetColumn(iconText, 1);
            Grid.SetColumn(nameText, 2);

            itemGrid.Children.Add(indicatorText);
            itemGrid.Children.Add(iconText);
            itemGrid.Children.Add(nameText);

            var itemBorder = new Border
            {
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(0, 3, 0, 3),
                Padding = new Thickness(14, 11, 14, 11),
                Cursor = Cursors.Hand,
                Child = itemGrid,
                Tag = index
            };

            itemBorder.MouseEnter += (s, e) =>
            {
                _subHighlightedIndex = index;
                UpdateVisuals();
            };

            itemBorder.MouseLeftButtonDown += (s, e) =>
            {
                _subHighlightedIndex = index;
                ExecuteSubmenuSelection();
            };

            _submenuItemBorders.Add(itemBorder);
            _submenuItemsStackPanel.Children.Add(itemBorder);
        }
    }

    private void PopulateSystemSubmenu()
    {
        _submenuIconText.Text = "⚙️";
        _submenuTitleLabel.Text = "SYSTEM";
        _submenuSubtitleLabel.Text = "System Controls";

        _subItemCount = 1;

        var indicatorText = new TextBlock
        {
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Width = 18,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var iconText = new TextBlock
        {
            Text = "🎬",
            FontSize = 17,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };

        var titleText = new TextBlock
        {
            Text = "Force Home Theater",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };

        var subtitleText = new TextBlock
        {
            Text = "Apply Home Theater display preset",
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175)),
            Margin = new Thickness(0, 2, 0, 0)
        };

        var textStack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { titleText, subtitleText }
        };

        var itemGrid = new Grid();
        itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        Grid.SetColumn(indicatorText, 0);
        Grid.SetColumn(iconText, 1);
        Grid.SetColumn(textStack, 2);

        itemGrid.Children.Add(indicatorText);
        itemGrid.Children.Add(iconText);
        itemGrid.Children.Add(textStack);

        var itemBorder = new Border
        {
            CornerRadius = new CornerRadius(12),
            Margin = new Thickness(0, 3, 0, 3),
            Padding = new Thickness(14, 10, 14, 10),
            Cursor = Cursors.Hand,
            Child = itemGrid,
            Tag = 0
        };

        itemBorder.MouseEnter += (s, e) =>
        {
            _subHighlightedIndex = 0;
            UpdateVisuals();
        };

        itemBorder.MouseLeftButtonDown += (s, e) =>
        {
            _subHighlightedIndex = 0;
            ExecuteSubmenuSelection();
        };

        _submenuItemBorders.Add(itemBorder);
        _submenuItemsStackPanel.Children.Add(itemBorder);
    }

    private void ReturnToMainMenu()
    {
        _focusedColumn = MenuColumn.Main;
        _submenuCard.Visibility = Visibility.Collapsed;
        UpdateVisuals();
    }

    private void ExecuteSubmenuSelection()
    {
        if (!IsMenuOpen)
        {
            return;
        }

        switch (_activeCategory)
        {
            case SettingsCategory.Video:
                if (_subHighlightedIndex >= 0 && _subHighlightedIndex < _currentVideoPresets.Count)
                {
                    int index = _subHighlightedIndex;
                    string name = _currentVideoPresets[index];
                    CloseMenu();
                    VideoPresetSelected?.Invoke(index, name);
                }
                break;

            case SettingsCategory.Sound:
                if (_subHighlightedIndex >= 0 && _subHighlightedIndex < _currentSoundPresets.Count)
                {
                    int index = _subHighlightedIndex;
                    string name = _currentSoundPresets[index];
                    CloseMenu();
                    SoundPresetSelected?.Invoke(index, name);
                }
                break;

            case SettingsCategory.Clarity:
                if (_subHighlightedIndex >= 0 && _subHighlightedIndex < _currentMotionPresets.Count)
                {
                    int index = _subHighlightedIndex;
                    string name = _currentMotionPresets[index];
                    CloseMenu();
                    MotionPresetSelected?.Invoke(index, name);
                }
                break;

            case SettingsCategory.Output:
                if (_subHighlightedIndex >= 0 && _subHighlightedIndex < _currentAudioDevices.Count)
                {
                    AudioDeviceManager.AudioDeviceInfo device = _currentAudioDevices[_subHighlightedIndex];
                    CloseMenu();
                    AudioDeviceSelected?.Invoke(device);
                }
                break;

            case SettingsCategory.System:
                CloseMenu();
                ForceHomeTheaterSelected?.Invoke();
                break;
        }
    }

    private void UpdateVisuals()
    {
        // 1. Update Main Menu Item visuals
        for (int i = 0; i < _mainItemBorders.Count; i++)
        {
            Border border = _mainItemBorders[i];
            if (border.Child is not Grid grid)
            {
                continue;
            }

            var indicatorText = (TextBlock)grid.Children[0];
            var iconText = (TextBlock)grid.Children[1];
            var nameText = (TextBlock)grid.Children[2];
            var chevronText = (TextBlock)grid.Children[3];

            bool isMainFocused = (_focusedColumn == MenuColumn.Main && i == _mainHighlightedIndex);
            bool isCurrentCategory = (_submenuCard.Visibility == Visibility.Visible && i == (int)_activeCategory);

            if (isMainFocused)
            {
                border.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                border.BorderBrush = new SolidColorBrush(Color.FromRgb(147, 197, 253));
                border.BorderThickness = new Thickness(1);
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
                nameText.FontWeight = FontWeights.Normal;

                chevronText.Foreground = Brushes.White;
            }
            else if (isCurrentCategory)
            {
                border.Background = new SolidColorBrush(Color.FromArgb(70, 37, 99, 235));
                border.BorderBrush = new SolidColorBrush(Color.FromArgb(140, 147, 197, 253));
                border.BorderThickness = new Thickness(1);
                border.Effect = null;

                indicatorText.Text = "▶";
                indicatorText.Foreground = new SolidColorBrush(Color.FromRgb(147, 197, 253));

                nameText.Foreground = Brushes.White;
                nameText.FontWeight = FontWeights.Normal;

                chevronText.Foreground = Brushes.White;
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

                chevronText.Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120));
            }
        }

        // 2. Update Submenu Item visuals
        for (int i = 0; i < _submenuItemBorders.Count; i++)
        {
            Border border = _submenuItemBorders[i];
            if (border.Child is not Grid grid)
            {
                continue;
            }

            var indicatorText = (TextBlock)grid.Children[0];
            UIElement contentElement = grid.Children[2];

            bool isHighlighted = (_focusedColumn == MenuColumn.Submenu && i == _subHighlightedIndex);

            if (isHighlighted)
            {
                border.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                border.BorderBrush = new SolidColorBrush(Color.FromRgb(147, 197, 253));
                border.BorderThickness = new Thickness(1);
                border.Effect = new DropShadowEffect
                {
                    Color = Color.FromRgb(37, 99, 235),
                    BlurRadius = 16,
                    ShadowDepth = 0,
                    Opacity = 0.6
                };

                indicatorText.Text = "▶";
                indicatorText.Foreground = Brushes.White;

                SetTextColorRecursive(contentElement, Brushes.White, FontWeights.Normal);
            }
            else
            {
                border.Background = new SolidColorBrush(Color.FromArgb(16, 255, 255, 255));
                border.BorderBrush = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255));
                border.BorderThickness = new Thickness(1);
                border.Effect = null;

                indicatorText.Text = "";
                indicatorText.Foreground = Brushes.Transparent;

                SetTextColorRecursive(contentElement, new SolidColorBrush(Color.FromRgb(209, 213, 219)), FontWeights.Normal);
            }
        }
    }

    private static void SetTextColorRecursive(UIElement element, Brush brush, FontWeight weight)
    {
        if (element is TextBlock tb)
        {
            tb.Foreground = brush;
            tb.FontWeight = weight;
        }
        else if (element is Panel panel)
        {
            foreach (UIElement child in panel.Children)
            {
                if (child is TextBlock childTb)
                {
                    // Do not override green active labels
                    if (childTb.Text == "Current Default")
                    {
                        continue;
                    }

                    childTb.Foreground = brush;
                    if (panel.Children.IndexOf(childTb) == 0)
                    {
                        childTb.FontWeight = weight;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Displays the settings popup menu in the top-left of the kiosk window.
    /// </summary>
    public void ShowMenu()
    {
        _focusedColumn = MenuColumn.Main;
        _mainHighlightedIndex = 0;
        _activeCategory = SettingsCategory.Video;
        _submenuCard.Visibility = Visibility.Collapsed;

        BuildMainMenuItems();
        UpdateVisuals();

        double ownerLeft = _ownerWindow.WindowState == WindowState.Maximized ? 0 : _ownerWindow.Left;
        double ownerTop = _ownerWindow.WindowState == WindowState.Maximized ? 0 : _ownerWindow.Top;
        double ownerWidth = _ownerWindow.ActualWidth > 0 ? _ownerWindow.ActualWidth : 1920;
        double ownerHeight = _ownerWindow.ActualHeight > 0 ? _ownerWindow.ActualHeight : 1080;

        Left = ownerLeft;
        Top = ownerTop;
        Width = ownerWidth;
        Height = ownerHeight;

        IsMenuOpen = true;
        _fadeOutStoryboard.Stop();
        Show();
        Activate();
        Focus();
        _fadeInStoryboard.Begin();
    }

    /// <summary>
    /// Closes the popup settings menu.
    /// </summary>
    public void CloseMenu()
    {
        if (!IsMenuOpen)
        {
            return;
        }

        IsMenuOpen = false;
        _fadeInStoryboard.Stop();
        _fadeOutStoryboard.Begin();
    }

    /// <summary>
    /// Processes remote/keyboard navigation keys for the settings menu.
    /// </summary>
    public void HandleKey(Key key)
    {
        if (!IsMenuOpen)
        {
            return;
        }

        if (key == Key.F2)
        {
            CloseMenu();
            return;
        }

        int mainCategoriesCount = _mainItemBorders.Count > 0 ? _mainItemBorders.Count : 5;

        if (_focusedColumn == MenuColumn.Main)
        {
            switch (key)
            {
                case Key.Up:
                    _mainHighlightedIndex = (_mainHighlightedIndex - 1 + mainCategoriesCount) % mainCategoriesCount;
                    UpdateVisuals();
                    break;

                case Key.Down:
                    _mainHighlightedIndex = (_mainHighlightedIndex + 1) % mainCategoriesCount;
                    UpdateVisuals();
                    break;

                case Key.Right:
                case Key.Enter:
                    OpenSubmenu((SettingsCategory)_mainHighlightedIndex);
                    break;

                case Key.Escape:
                case Key.Back:
                case Key.BrowserBack:
                    CloseMenu();
                    break;
            }
        }
        else // MenuColumn.Submenu
        {
            switch (key)
            {
                case Key.Up:
                    if (_subItemCount > 0)
                    {
                        _subHighlightedIndex = (_subHighlightedIndex - 1 + _subItemCount) % _subItemCount;
                        UpdateVisuals();
                    }
                    break;

                case Key.Down:
                    if (_subItemCount > 0)
                    {
                        _subHighlightedIndex = (_subHighlightedIndex + 1) % _subItemCount;
                        UpdateVisuals();
                    }
                    break;

                case Key.Enter:
                    ExecuteSubmenuSelection();
                    break;

                case Key.Left:
                case Key.Escape:
                case Key.Back:
                case Key.BrowserBack:
                    ReturnToMainMenu();
                    break;
            }
        }
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (IsMenuOpen && e.Key is Key.Up or Key.Down or Key.Left or Key.Right or Key.Enter or Key.Escape or Key.Back or Key.BrowserBack or Key.F2)
        {
            e.Handled = true;
            HandleKey(e.Key);
        }
    }
}

