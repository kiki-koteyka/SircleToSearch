using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Wpf.Ui.Controls;

namespace SircleToSearch;

public partial class SettingsWindow : FluentWindow
{
    private const string IssuesUrl = "https://github.com/kiki-koteyka/SircleToSearch/issues/new";
    private const string RepoUrl = "https://github.com/kiki-koteyka/SircleToSearch";
    private const string AuthorUrl = "https://github.com/kiki-koteyka";
    private bool _loading = true;
    private bool _recordingHotkey;

    private double _scrollBaseOffset;
    private double _scrollPendingTarget;
    private bool _scrollAnimating;
    private int _scrollGeneration;
    private double _pendingWheelDelta;
    private bool _wheelUpdateScheduled;

    public event Action? LanguageChanged;
    public event Action? HotkeyRebound;

    public SettingsWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            LanguageCombo.SelectedIndex = AppSettings.Current.Language == "ru" ? 1 : 0;
            AutostartToggle.IsChecked = Autostart.IsEnabled();
            SearchEngineCombo.SelectedIndex = AppSettings.Current.Engine == SearchEngine.Yandex ? 1 : 0;
            FastSearchToggle.IsChecked = AppSettings.Current.FastSearch;
            AutoUpdateToggle.IsChecked = AppSettings.Current.AutoUpdate;
            ApplyStrings();
            RefreshHotkeyDisplay();
            UpdateFastSearchVisibility();
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

        if (!_scrollAnimating)
            ScrollThumbTransform.Y = OffsetToThumbY(SettingsScrollViewer.VerticalOffset);
    }

    private void SettingsScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;
        _pendingWheelDelta += e.Delta;

        if (_wheelUpdateScheduled) return;
        _wheelUpdateScheduled = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(ApplyPendingWheelScroll));
    }

    private void ApplyPendingWheelScroll()
    {
        _wheelUpdateScheduled = false;
        var delta = _pendingWheelDelta;
        _pendingWheelDelta = 0;

        if (!_scrollAnimating)
        {
            _scrollBaseOffset = SettingsScrollViewer.VerticalOffset;
            _scrollPendingTarget = _scrollBaseOffset;
        }

        _scrollPendingTarget = Math.Clamp(_scrollPendingTarget - delta, 0, SettingsScrollViewer.ScrollableHeight);

        var duration = TimeSpan.FromMilliseconds(280);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        var contentAnimation = new DoubleAnimation(SettingsContentTransform.Y, _scrollBaseOffset - _scrollPendingTarget, duration)
        {
            EasingFunction = ease
        };
        var thumbAnimation = new DoubleAnimation(ScrollThumbTransform.Y, OffsetToThumbY(_scrollPendingTarget), duration)
        {
            EasingFunction = ease
        };

        _scrollAnimating = true;
        var generation = ++_scrollGeneration;
        contentAnimation.Completed += (_, _) =>
        {
            if (generation != _scrollGeneration) return;
            _scrollAnimating = false;
            SettingsContentTransform.BeginAnimation(TranslateTransform.YProperty, null);
            SettingsContentTransform.Y = 0;
            SettingsScrollViewer.ScrollToVerticalOffset(_scrollPendingTarget);
            ScrollThumbTransform.BeginAnimation(TranslateTransform.YProperty, null);
            ScrollThumbTransform.Y = OffsetToThumbY(_scrollPendingTarget);
        };

        SettingsContentTransform.BeginAnimation(TranslateTransform.YProperty, contentAnimation, HandoffBehavior.SnapshotAndReplace);
        ScrollThumbTransform.BeginAnimation(TranslateTransform.YProperty, thumbAnimation, HandoffBehavior.SnapshotAndReplace);
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

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        AppSettings.Current.FirstRunCompleted = true;
        AppSettings.Current.Save();
        Close();
    }
}
