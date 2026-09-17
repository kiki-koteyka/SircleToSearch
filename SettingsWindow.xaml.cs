using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Wpf.Ui.Controls;

namespace SircleToSearch;

public partial class SettingsWindow : FluentWindow
{
    private const string IssuesUrl = "https://github.com/kiki-koteyka/SircleToSearch/issues/new";
    private const string RepoUrl = "https://github.com/kiki-koteyka/SircleToSearch";
    private const string AuthorUrl = "https://github.com/kiki-koteyka";
    private bool _loading = true;
    private bool _recordingHotkey;

    // ScrollViewer.VerticalOffset isn't a real DependencyProperty (it's a plain CLR
    // property backed by internal scroll info), so it can't be animated directly -
    // this mediator is the standard workaround: it exposes an animatable DP and pushes
    // every interpolated value straight into ScrollToVerticalOffset.
    private sealed class ScrollViewerOffsetMediator : FrameworkElement
    {
        public static readonly DependencyProperty VerticalOffsetProperty =
            DependencyProperty.Register(nameof(VerticalOffset), typeof(double), typeof(ScrollViewerOffsetMediator),
                new PropertyMetadata(0.0, (d, e) =>
                    ((ScrollViewerOffsetMediator)d).ScrollViewer?.ScrollToVerticalOffset((double)e.NewValue)));

        public System.Windows.Controls.ScrollViewer? ScrollViewer { get; set; }

        public double VerticalOffset
        {
            get => (double)GetValue(VerticalOffsetProperty);
            set => SetValue(VerticalOffsetProperty, value);
        }
    }

    private readonly ScrollViewerOffsetMediator _scrollMediator = new();
    private double _scrollTarget;
    private bool _scrollAnimating;

    public event Action? LanguageChanged;
    public event Action? HotkeyRebound;

    public SettingsWindow()
    {
        InitializeComponent();
        _scrollMediator.ScrollViewer = SettingsScrollViewer;
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

    /// <summary>Animates the scroll instead of the instant per-notch jump WPF does by
    /// default - each wheel tick eases toward an accumulating target rather than
    /// snapping, so a burst of scrolling reads as one smooth glide.</summary>
    private void SettingsScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;

        // Base the new target on where the last animation was headed (not the current
        // mid-flight position) so a quick burst of wheel ticks accumulates into one
        // smooth glide instead of retargeting from wherever the easing happens to be.
        var baseOffset = _scrollAnimating ? _scrollTarget : SettingsScrollViewer.VerticalOffset;
        _scrollTarget = Math.Clamp(baseOffset - e.Delta, 0, SettingsScrollViewer.ScrollableHeight);

        var animation = new DoubleAnimation(SettingsScrollViewer.VerticalOffset, _scrollTarget, TimeSpan.FromMilliseconds(320))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        _scrollAnimating = true;
        animation.Completed += (_, _) => _scrollAnimating = false;
        _scrollMediator.BeginAnimation(ScrollViewerOffsetMediator.VerticalOffsetProperty, animation, HandoffBehavior.SnapshotAndReplace);
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
            return; // still just a modifier on its own - keep waiting for a real key

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
            return; // keep recording - a bare letter key isn't a usable global hotkey
        }

        var vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        var app = (App)System.Windows.Application.Current;
        if (app.HotkeyManager is null || !app.HotkeyManager.TryRebind(modifiers, vk))
        {
            HotkeyDisplayText.Text = Strings.Get("SettingsHotkeyConflict");
            return; // keep recording so they can try a different combo
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

    /// <summary>The fast-vs-reliable tradeoff only exists for Google's captcha-avoidance
    /// dance - Yandex's upload flow doesn't have an equivalent, so hide the toggle
    /// instead of leaving a control on screen that does nothing.</summary>
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

    private async void CheckUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        CheckUpdateButton.IsEnabled = false;
        UpdateStatusText.Text = Strings.Get("SettingsCheckingUpdate");

        try
        {
            var result = await UpdateChecker.CheckAsync();
            if (result.UpdateAvailable && result.AssetDownloadUrl is not null)
            {
                UpdateStatusText.Text = Strings.Get("SettingsUpdateAvailable", result.LatestVersion);
                // Same confirm-dialog + "apply once the search overlay is idle" path as
                // a background-detected update - no separate immediate-download button
                // here, so there's exactly one way updates ever get installed.
                ((App)System.Windows.Application.Current).OfferUpdate(result.LatestVersion, result.AssetDownloadUrl);
            }
            else if (result.UpdateAvailable)
            {
                // Newer tag exists but has no matching exe asset (e.g. a draft/partial
                // release) - nothing to self-update from, point at the release page instead.
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
