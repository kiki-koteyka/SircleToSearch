using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Wpf.Ui.Controls;
using Border = System.Windows.Controls.Border;
using Rectangle = System.Windows.Shapes.Rectangle;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace SircleToSearch;

public partial class SettingsWindow : FluentWindow
{
    private const string IssuesUrl = "https://github.com/kiki-koteyka/SircleToSearch/issues/new";
    private const string RepoUrl = "https://github.com/kiki-koteyka/SircleToSearch";
    private const string AuthorUrl = "https://github.com/kiki-koteyka";
    private static readonly Rect PreviewBounds = new(0, 0, 454, 190);
    private const double PreviewHandleSize = 16;
    private const double PreviewMinSize = 40;
    private enum PreviewDragMode { None, Moving, Resizing }

    private Rect _previewSelection = new(140, 45, 180, 95);
    private PreviewDragMode _previewMode = PreviewDragMode.None;
    private Point _previewDragAnchor;

    private bool _loading = true;
    private bool _recordingHotkey;
    private int _activePage = -1;
    private Border[] _navItems = [];
    private Rectangle[] _navAccents = [];
    private UIElement[] _pages = [];
    private System.Windows.Shapes.Ellipse[] _accentSwatches = [];

    private double _scrollBaseOffset;
    private double _scrollVirtualOffset;
    private double _scrollVelocity;
    private bool _scrollMomentumRunning;
    private DateTime _scrollLastFrameTime;

    public event Action? LanguageChanged;
    public event Action? HotkeyRebound;

    public SettingsWindow()
    {
        InitializeComponent();
        DebugInspector.Attach(this);
        Closed += (_, _) => CompositionTarget.Rendering -= OnScrollMomentumFrame;
        Loaded += (_, _) =>
        {
            _navItems = [NavGeneral, NavSearch, NavSelection, NavUpdates, NavAbout];
            _navAccents = [NavGeneralAccent, NavSearchAccent, NavSelectionAccent, NavUpdatesAccent, NavAboutAccent];
            _pages = [PageGeneral, PageSearch, PageSelection, PageUpdates, PageAbout];

            LanguageCombo.SelectedIndex = AppSettings.Current.Language == "ru" ? 1 : 0;
            AutostartToggle.IsChecked = Autostart.IsEnabled();
            SearchEngineCombo.SelectedIndex = AppSettings.Current.Engine == SearchEngine.Yandex ? 1 : 0;
            FastSearchToggle.IsChecked = AppSettings.Current.FastSearch;
            AutoUpdateToggle.IsChecked = AppSettings.Current.AutoUpdate;

            HugSlider.Value = AppSettings.Current.SelectionHug;
            CornerRadiusSlider.Value = AppSettings.Current.SelectionCornerRadius;
            ArmLengthSlider.Value = AppSettings.Current.SelectionArmLength;
            ThicknessSlider.Value = AppSettings.Current.SelectionThickness;
            GlowGapSlider.Value = AppSettings.Current.SelectionGlowGap;
            GlowThicknessSlider.Value = AppSettings.Current.SelectionGlowThickness;
            GlowBlurSlider.Value = AppSettings.Current.SelectionGlowBlur;
            GlowOpacitySlider.Value = AppSettings.Current.SelectionGlowOpacity;
            SelectionRadiusSlider.Value = AppSettings.Current.SelectionRadius;

            BuildAccentSwatches();
            AccentHexBox.Text = AppSettings.Current.AccentColor;

            ApplyStrings();
            RefreshHotkeyDisplay();
            UpdateFastSearchVisibility();
            UpdateSelectionValueLabels();
            ApplyAccentColor();
            SelectPage(0);
            _loading = false;

            _ = CheckForUpdatesAsync();
        };
    }

    private void ApplyStrings()
    {
        Title = Strings.Get("SettingsTitle");
        WelcomeText.Text = Strings.Get("SettingsWelcome");
        LanguageLabel.Text = Strings.Get("SettingsLanguage");
        AutostartLabel.Text = Strings.Get("SettingsAutostart");
        SearchEngineLabel.Text = Strings.Get("SettingsSearchEngine");
        SearchEngineGoogleItem.Content = Strings.Get("SettingsSearchEngineGoogle");
        FastSearchLabel.Text = Strings.Get("SettingsFastSearch");
        FastSearchHint.Text = Strings.Get("SettingsFastSearchHint");
        HotkeyLabel.Text = Strings.Get("SettingsHotkey");
        ReportBugButton.Content = Strings.Get("SettingsReportBug");
        CloseButton.Content = Strings.Get("SettingsClose");
        GitHubButton.Content = Strings.Get("SettingsGitHub");
        AuthorButton.Content = Strings.Get("SettingsAuthor");
        BySomeoneText.Text = Strings.Get("SettingsBySomeone");
        VersionText.Text = Strings.Get("SettingsVersion", AppVersion.Current);
        CheckUpdateButton.Content = Strings.Get("SettingsCheckUpdate");
        AutoUpdateLabel.Text = Strings.Get("SettingsAutoUpdate");

        SelectionHintText.Text = Strings.Get("SettingsSelectionHint");
        BracketsSectionLabel.Text = Strings.Get("SettingsSelectionBracketsSection");
        GlowSectionLabel.Text = Strings.Get("SettingsSelectionGlowSection");
        AreaSectionLabel.Text = Strings.Get("SettingsSelectionAreaSection");
        HugLabel.Text = Strings.Get("SettingsSelectionHug");
        CornerRadiusLabel.Text = Strings.Get("SettingsSelectionCornerRadius");
        ArmLengthLabel.Text = Strings.Get("SettingsSelectionArmLength");
        ThicknessLabel.Text = Strings.Get("SettingsSelectionThickness");
        GlowGapLabel.Text = Strings.Get("SettingsSelectionGlowGap");
        GlowThicknessLabel.Text = Strings.Get("SettingsSelectionGlowThickness");
        GlowBlurLabel.Text = Strings.Get("SettingsSelectionGlowBlur");
        GlowOpacityLabel.Text = Strings.Get("SettingsSelectionGlowOpacity");
        SelectionRadiusLabel.Text = Strings.Get("SettingsSelectionRadius");
        SelectionResetButton.Content = Strings.Get("SettingsSelectionReset");

        AccentColorLabel.Text = Strings.Get("SettingsAccentColor");
        AccentHexApplyButton.Content = Strings.Get("SettingsAccentHexApply");
    }

    private double ThumbMaxTravel => Math.Max(0, ScrollTrack.ActualHeight - ScrollThumb.ActualHeight);

    private double OffsetToThumbY(double offset)
    {
        var scrollable = SettingsScrollViewer.ScrollableHeight;
        return scrollable <= 0 ? 0 : offset / scrollable * ThumbMaxTravel;
    }

    private void SettingsScrollViewer_ScrollChanged(object sender, System.Windows.Controls.ScrollChangedEventArgs e)
    {
        var viewport = SettingsScrollViewer.ViewportHeight;
        var extent = SettingsScrollViewer.ExtentHeight;
        if (extent <= 0) return;

        var trackHeight = ScrollTrack.ActualHeight;
        ScrollThumb.Height = Math.Min(Math.Max(24, trackHeight * (viewport / extent)), trackHeight);
        ScrollThumb.Visibility = viewport >= extent ? Visibility.Collapsed : Visibility.Visible;

        if (!_scrollMomentumRunning)
            ScrollThumbTransform.Y = OffsetToThumbY(SettingsScrollViewer.VerticalOffset);
    }

    private void SettingsScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;

        if (!_scrollMomentumRunning)
        {
            _scrollBaseOffset = SettingsScrollViewer.VerticalOffset;
            _scrollVirtualOffset = _scrollBaseOffset;
        }

        _scrollVelocity += -e.Delta / 120.0 * 2200.0;

        if (!_scrollMomentumRunning)
        {
            _scrollMomentumRunning = true;
            _scrollLastFrameTime = DateTime.UtcNow;
            CompositionTarget.Rendering += OnScrollMomentumFrame;
        }
    }

    private void OnScrollMomentumFrame(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        var dt = Math.Min((now - _scrollLastFrameTime).TotalSeconds, 0.05);
        _scrollLastFrameTime = now;

        _scrollVirtualOffset += _scrollVelocity * dt;
        _scrollVelocity *= Math.Exp(-10.0 * dt);

        var max = SettingsScrollViewer.ScrollableHeight;
        if (_scrollVirtualOffset < 0) { _scrollVirtualOffset = 0; _scrollVelocity = 0; }
        else if (_scrollVirtualOffset > max) { _scrollVirtualOffset = max; _scrollVelocity = 0; }

        SettingsContentTransform.Y = _scrollBaseOffset - _scrollVirtualOffset;
        ScrollThumbTransform.Y = OffsetToThumbY(_scrollVirtualOffset);

        if (Math.Abs(_scrollVelocity) < 4.0)
        {
            CompositionTarget.Rendering -= OnScrollMomentumFrame;
            _scrollMomentumRunning = false;
            _scrollVelocity = 0;
            SettingsContentTransform.Y = 0;
            SettingsScrollViewer.ScrollToVerticalOffset(_scrollVirtualOffset);
            ScrollThumbTransform.Y = OffsetToThumbY(_scrollVirtualOffset);
        }
    }

    private void RefreshHotkeyDisplay()
    {
        HotkeyDisplayText.Text = HotkeyManager.Format(
            (HotkeyManager.Modifiers)AppSettings.Current.HotkeyModifiers, AppSettings.Current.HotkeyVk);
    }

    private void HotkeyBox_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => HotkeyBox.Focus();

    private void HotkeyBox_GotFocus(object sender, RoutedEventArgs e)
    {
        _recordingHotkey = true;
        HotkeyDisplayText.Text = Strings.Get("SettingsHotkeyRecording");
    }

    private void HotkeyBox_LostFocus(object sender, RoutedEventArgs e)
    {
        _recordingHotkey = false;
        RefreshHotkeyDisplay();
    }

    private void HotkeyBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (!_recordingHotkey) return;
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LWin or Key.RWin or Key.LeftCtrl or Key.RightCtrl
            or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift)
            return;

        if (key == Key.Escape)
        {
            HotkeyBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            return;
        }

        var modifiers = HotkeyManager.Modifiers.None;
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) modifiers |= HotkeyManager.Modifiers.Control;
        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) modifiers |= HotkeyManager.Modifiers.Shift;
        if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt)) modifiers |= HotkeyManager.Modifiers.Alt;
        if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin)) modifiers |= HotkeyManager.Modifiers.Win;

        if (modifiers == HotkeyManager.Modifiers.None)
        {
            HotkeyDisplayText.Text = Strings.Get("SettingsHotkeyNeedsModifier");
            return;
        }

        var vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        var app = (App)System.Windows.Application.Current;
        if (app.HotkeyManager is null || !app.HotkeyManager.TryRebind(modifiers, vk))
        {
            HotkeyDisplayText.Text = Strings.Get("SettingsHotkeyConflict");
            return;
        }

        AppSettings.Current.HotkeyModifiers = (uint)modifiers;
        AppSettings.Current.HotkeyVk = vk;
        AppSettings.Current.Save();
        HotkeyRebound?.Invoke();

        HotkeyBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
    }

    private void LanguageCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if (LanguageCombo.SelectedItem is not System.Windows.Controls.ComboBoxItem item) return;

        AppSettings.Current.Language = (string)item.Tag;
        AppSettings.Current.Save();
        ApplyStrings();
        LanguageChanged?.Invoke();
    }

    private void SearchEngineCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if (SearchEngineCombo.SelectedItem is not System.Windows.Controls.ComboBoxItem item) return;

        AppSettings.Current.Engine = (string)item.Tag == "Yandex" ? SearchEngine.Yandex : SearchEngine.Google;
        AppSettings.Current.Save();
        UpdateFastSearchVisibility();
    }

    private void UpdateFastSearchVisibility()
    {
        FastSearchCard.Visibility = AppSettings.Current.Engine == SearchEngine.Google
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void FastSearchToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;

        AppSettings.Current.FastSearch = FastSearchToggle.IsChecked == true;
        AppSettings.Current.Save();
    }

    private void AutostartToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;

        var wantEnabled = AutostartToggle.IsChecked == true;
        if (!Autostart.TrySet(wantEnabled))
        {
            _loading = true;
            AutostartToggle.IsChecked = !wantEnabled;
            _loading = false;
            System.Windows.MessageBox.Show(this, Strings.Get("AutostartFailed"),
                "SircleToSearch", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private async void CheckUpdateButton_Click(object sender, RoutedEventArgs e) => await CheckForUpdatesAsync();

    private async Task CheckForUpdatesAsync()
    {
        CheckUpdateButton.IsEnabled = false;
        UpdateStatusText.Text = Strings.Get("SettingsCheckingUpdate");

        try
        {
            var result = await UpdateChecker.CheckAsync();
            if (result.UpdateAvailable && result.AssetDownloadUrl is not null)
            {
                UpdateStatusText.Text = Strings.Get("SettingsUpdateAvailable", result.LatestVersion);
                ((App)System.Windows.Application.Current).OfferUpdate(result.LatestVersion, result.AssetDownloadUrl);
            }
            else if (result.UpdateAvailable)
            {
                UpdateStatusText.Text = Strings.Get("SettingsUpdateAvailable", result.LatestVersion);
                OpenUrl(result.ReleaseUrl);
            }
            else
            {
                UpdateStatusText.Text = Strings.Get("SettingsUpToDate");
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("Проверка обновлений не удалась", ex);
            UpdateStatusText.Text = Strings.Get("SettingsUpdateCheckFailed");
        }
        finally
        {
            CheckUpdateButton.IsEnabled = true;
        }
    }

    private void AutoUpdateToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        AppSettings.Current.AutoUpdate = AutoUpdateToggle.IsChecked == true;
        AppSettings.Current.Save();
    }

    private void ReportBugButton_Click(object sender, RoutedEventArgs e) => OpenUrl(IssuesUrl);
    private void GitHubButton_Click(object sender, RoutedEventArgs e) => OpenUrl(RepoUrl);
    private void AuthorButton_Click(object sender, RoutedEventArgs e) => OpenUrl(AuthorUrl);

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppLog.Error($"Не удалось открыть ссылку {url}", ex);
        }
    }

    private void NavGeneral_Click(object sender, MouseButtonEventArgs e) => SelectPage(0);
    private void NavSearch_Click(object sender, MouseButtonEventArgs e) => SelectPage(1);
    private void NavSelection_Click(object sender, MouseButtonEventArgs e) => SelectPage(2);
    private void NavUpdates_Click(object sender, MouseButtonEventArgs e) => SelectPage(3);
    private void NavAbout_Click(object sender, MouseButtonEventArgs e) => SelectPage(4);

    private void SelectPage(int index)
    {
        if (_activePage == index) return;
        _activePage = index;

        for (var i = 0; i < _pages.Length; i++)
        {
            _pages[i].Visibility = i == index ? Visibility.Visible : Visibility.Collapsed;
            _navAccents[i].Visibility = i == index ? Visibility.Visible : Visibility.Collapsed;
            var accent = AccentColor.Parse(AppSettings.Current.AccentColor);
            _navItems[i].Background = i == index
                ? new SolidColorBrush(Color.FromArgb(0x18, accent.R, accent.G, accent.B))
                : System.Windows.Media.Brushes.Transparent;
        }

        SettingsScrollViewer.ScrollToVerticalOffset(0);
        SettingsContentTransform.Y = 0;
    }

    private void SelectionSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;

        AppSettings.Current.SelectionHug = HugSlider.Value;
        AppSettings.Current.SelectionCornerRadius = CornerRadiusSlider.Value;
        AppSettings.Current.SelectionArmLength = ArmLengthSlider.Value;
        AppSettings.Current.SelectionThickness = ThicknessSlider.Value;
        AppSettings.Current.SelectionGlowGap = GlowGapSlider.Value;
        AppSettings.Current.SelectionGlowThickness = GlowThicknessSlider.Value;
        AppSettings.Current.SelectionGlowBlur = GlowBlurSlider.Value;
        AppSettings.Current.SelectionGlowOpacity = GlowOpacitySlider.Value;
        AppSettings.Current.SelectionRadius = SelectionRadiusSlider.Value;
        AppSettings.Current.Save();

        UpdateSelectionValueLabels();
        UpdateSelectionPreview();
    }

    private void UpdateSelectionValueLabels()
    {
        HugValueText.Text = $"{HugSlider.Value:0}";
        CornerRadiusValueText.Text = $"{CornerRadiusSlider.Value:0}";
        ArmLengthValueText.Text = $"{ArmLengthSlider.Value:0}";
        ThicknessValueText.Text = $"{ThicknessSlider.Value:0}";
        GlowGapValueText.Text = $"{GlowGapSlider.Value:0}";
        GlowThicknessValueText.Text = $"{GlowThicknessSlider.Value:0}";
        GlowBlurValueText.Text = $"{GlowBlurSlider.Value:0}";
        GlowOpacityValueText.Text = $"{GlowOpacitySlider.Value:0}%";
        SelectionRadiusValueText.Text = $"{SelectionRadiusSlider.Value:0}";
    }

    private void UpdateSelectionPreview()
    {
        var config = SelectionVisualConfig.FromSettings(AppSettings.Current);
        PreviewDim.Data = SelectionRenderer.BuildDimHole(PreviewBounds, _previewSelection, config.SelectionRadius);
        SelectionRenderer.ApplyGlow(PreviewGlow, _previewSelection, config);
        SelectionRenderer.DrawBrackets(PreviewHandles, _previewSelection, config);
    }

    private Point? PreviewHitCorner(Point pos)
    {
        var corners = new (Point Corner, Point Opposite)[]
        {
            (new Point(_previewSelection.Left, _previewSelection.Top), new Point(_previewSelection.Right, _previewSelection.Bottom)),
            (new Point(_previewSelection.Right, _previewSelection.Top), new Point(_previewSelection.Left, _previewSelection.Bottom)),
            (new Point(_previewSelection.Left, _previewSelection.Bottom), new Point(_previewSelection.Right, _previewSelection.Top)),
            (new Point(_previewSelection.Right, _previewSelection.Bottom), new Point(_previewSelection.Left, _previewSelection.Top)),
        };

        foreach (var (corner, opposite) in corners)
        {
            if ((corner - pos).Length <= PreviewHandleSize)
                return opposite;
        }
        return null;
    }

    private void PreviewCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(PreviewCard);

        var oppositeCorner = PreviewHitCorner(pos);
        if (oppositeCorner.HasValue)
        {
            _previewMode = PreviewDragMode.Resizing;
            _previewDragAnchor = oppositeCorner.Value;
        }
        else if (_previewSelection.Contains(pos))
        {
            _previewMode = PreviewDragMode.Moving;
            _previewDragAnchor = new Point(pos.X - _previewSelection.X, pos.Y - _previewSelection.Y);
        }
        else
        {
            return;
        }

        PreviewCard.CaptureMouse();
    }

    private void PreviewCard_MouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(PreviewCard);

        if (_previewMode == PreviewDragMode.None)
        {
            if (PreviewHitCorner(pos).HasValue)
                PreviewCard.Cursor = System.Windows.Input.Cursors.SizeNWSE;
            else if (_previewSelection.Contains(pos))
                PreviewCard.Cursor = System.Windows.Input.Cursors.SizeAll;
            else
                PreviewCard.Cursor = System.Windows.Input.Cursors.Arrow;
            return;
        }

        pos.X = Math.Clamp(pos.X, 0, PreviewBounds.Width);
        pos.Y = Math.Clamp(pos.Y, 0, PreviewBounds.Height);

        switch (_previewMode)
        {
            case PreviewDragMode.Resizing:
                var candidate = new Rect(
                    Math.Min(_previewDragAnchor.X, pos.X), Math.Min(_previewDragAnchor.Y, pos.Y),
                    Math.Abs(pos.X - _previewDragAnchor.X), Math.Abs(pos.Y - _previewDragAnchor.Y));
                if (candidate.Width >= PreviewMinSize && candidate.Height >= PreviewMinSize)
                    _previewSelection = candidate;
                break;

            case PreviewDragMode.Moving:
                var width = _previewSelection.Width;
                var height = _previewSelection.Height;
                var newX = Math.Clamp(pos.X - _previewDragAnchor.X, 0, Math.Max(0, PreviewBounds.Width - width));
                var newY = Math.Clamp(pos.Y - _previewDragAnchor.Y, 0, Math.Max(0, PreviewBounds.Height - height));
                _previewSelection = new Rect(newX, newY, width, height);
                break;
        }

        UpdateSelectionPreview();
    }

    private void PreviewCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _previewMode = PreviewDragMode.None;
        PreviewCard.ReleaseMouseCapture();
    }

    private void PreviewCard_LostMouseCapture(object sender, MouseEventArgs e)
    {
        _previewMode = PreviewDragMode.None;
    }

    private void BuildAccentSwatches()
    {
        AccentSwatchPanel.Children.Clear();
        var swatches = new System.Collections.Generic.List<System.Windows.Shapes.Ellipse>();

        foreach (var (hex, _, _) in AccentColor.Presets)
        {
            var color = AccentColor.Parse(hex);
            var swatch = new System.Windows.Shapes.Ellipse
            {
                Width = 26,
                Height = 26,
                Margin = new Thickness(0, 0, 8, 0),
                Fill = new SolidColorBrush(color),
                Stroke = System.Windows.Media.Brushes.White,
                StrokeThickness = 2,
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = hex,
            };
            swatch.MouseLeftButtonUp += AccentSwatch_Click;
            AccentSwatchPanel.Children.Add(swatch);
            swatches.Add(swatch);
        }

        _accentSwatches = swatches.ToArray();
    }

    private void AccentSwatch_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Shapes.Ellipse { Tag: string hex }) return;
        SetAccentColor(hex);
    }

    private void AccentHexBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        TryApplyAccentHex();
    }

    private void AccentHexBox_LostFocus(object sender, RoutedEventArgs e) => TryApplyAccentHex();

    private void AccentHexApplyButton_Click(object sender, RoutedEventArgs e) => TryApplyAccentHex();

    private void TryApplyAccentHex()
    {
        if (_loading) return;

        if (!AccentColor.TryParse(AccentHexBox.Text, out _))
        {
            System.Windows.MessageBox.Show(this, Strings.Get("SettingsAccentHexInvalid"),
                "SircleToSearch", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            AccentHexBox.Text = AppSettings.Current.AccentColor;
            return;
        }

        SetAccentColor(AccentHexBox.Text.Trim());
    }

    private void SetAccentColor(string hex)
    {
        if (!AccentColor.TryParse(hex, out var color)) return;
        var normalized = $"#{color.R:X2}{color.G:X2}{color.B:X2}";

        AppSettings.Current.AccentColor = normalized;
        AppSettings.Current.Save();
        AccentHexBox.Text = normalized;

        Wpf.Ui.Appearance.ApplicationAccentColorManager.Apply(color, Wpf.Ui.Appearance.ApplicationTheme.Light);
        ApplyAccentColor();
    }

    private void ApplyAccentColor()
    {
        var accent = AccentColor.Parse(AppSettings.Current.AccentColor);
        var accentBrush = new SolidColorBrush(accent);

        foreach (var rect in _navAccents)
            rect.Fill = accentBrush;

        foreach (var swatch in _accentSwatches)
        {
            var isSelected = swatch.Tag is string hex
                && string.Equals(hex, AppSettings.Current.AccentColor, StringComparison.OrdinalIgnoreCase);
            swatch.StrokeThickness = isSelected ? 3 : 2;
            swatch.Stroke = isSelected ? accentBrush : System.Windows.Media.Brushes.White;
        }

        if (_activePage >= 0)
        {
            _navItems[_activePage].Background = new SolidColorBrush(
                Color.FromArgb(0x18, accent.R, accent.G, accent.B));
        }

        UpdateSelectionPreview();
    }

    private void SelectionResetButton_Click(object sender, RoutedEventArgs e)
    {
        var defaults = new AppSettings();
        _loading = true;
        HugSlider.Value = defaults.SelectionHug;
        CornerRadiusSlider.Value = defaults.SelectionCornerRadius;
        ArmLengthSlider.Value = defaults.SelectionArmLength;
        ThicknessSlider.Value = defaults.SelectionThickness;
        GlowGapSlider.Value = defaults.SelectionGlowGap;
        GlowThicknessSlider.Value = defaults.SelectionGlowThickness;
        GlowBlurSlider.Value = defaults.SelectionGlowBlur;
        GlowOpacitySlider.Value = defaults.SelectionGlowOpacity;
        SelectionRadiusSlider.Value = defaults.SelectionRadius;
        _loading = false;

        SelectionSlider_ValueChanged(sender, null!);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        AppSettings.Current.FirstRunCompleted = true;
        AppSettings.Current.Save();
        Close();
    }
}
