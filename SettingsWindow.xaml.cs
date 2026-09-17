using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using Wpf.Ui.Controls;

namespace SircleToSearch;

public partial class SettingsWindow : FluentWindow
{
    private const string IssuesUrl = "https://github.com/kiki-koteyka/SircleToSearch/issues/new";
    private const string RepoUrl = "https://github.com/kiki-koteyka/SircleToSearch";
    private const string AuthorUrl = "https://github.com/kiki-koteyka";
    private bool _loading = true;
    private string? _pendingUpdateAssetUrl;

    public event Action? LanguageChanged;

    public SettingsWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            LanguageCombo.SelectedIndex = AppSettings.Current.Language == "ru" ? 1 : 0;
            AutostartToggle.IsChecked = Autostart.IsEnabled();
            FastSearchToggle.IsChecked = AppSettings.Current.FastSearch;
            ApplyStrings();
            _loading = false;
        };
    }

    private void ApplyStrings()
    {
        Title = Strings.Get("SettingsTitle");
        WelcomeText.Text = Strings.Get("SettingsWelcome");
        LanguageLabel.Text = Strings.Get("SettingsLanguage");
        AutostartLabel.Text = Strings.Get("SettingsAutostart");
        FastSearchLabel.Text = Strings.Get("SettingsFastSearch");
        FastSearchHint.Text = Strings.Get("SettingsFastSearchHint");
        HotkeyInfoText.Text = Strings.Get("SettingsHotkeyInfo");
        ReportBugButton.Content = Strings.Get("SettingsReportBug");
        CloseButton.Content = Strings.Get("SettingsClose");
        GitHubButton.Content = Strings.Get("SettingsGitHub");
        AuthorButton.Content = Strings.Get("SettingsAuthor");
        BySomeoneText.Text = Strings.Get("SettingsBySomeone");
        VersionText.Text = Strings.Get("SettingsVersion", AppVersion.Current);
        CheckUpdateButton.Content = _pendingUpdateAssetUrl is null
            ? Strings.Get("SettingsCheckUpdate")
            : Strings.Get("SettingsUpdateNow");
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
        // Second click, once an update's been found: apply it instead of checking again.
        if (_pendingUpdateAssetUrl is { } assetUrl)
        {
            await ApplyUpdateAsync(assetUrl);
            return;
        }

        CheckUpdateButton.IsEnabled = false;
        UpdateStatusText.Text = Strings.Get("SettingsCheckingUpdate");

        try
        {
            var result = await UpdateChecker.CheckAsync();
            if (result.UpdateAvailable && result.AssetDownloadUrl is not null)
            {
                _pendingUpdateAssetUrl = result.AssetDownloadUrl;
                UpdateStatusText.Text = Strings.Get("SettingsUpdateAvailable", result.LatestVersion);
                CheckUpdateButton.Content = Strings.Get("SettingsUpdateNow");
            }
            else if (result.UpdateAvailable)
            {
                // Newer tag exists but has no matching exe asset (e.g. a draft/partial
                // release) — nothing to self-update from, point at the release page instead.
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

    private async Task ApplyUpdateAsync(string assetUrl)
    {
        CheckUpdateButton.IsEnabled = false;
        var progress = new Progress<double>(p =>
            UpdateStatusText.Text = Strings.Get("SettingsDownloadingUpdate", (int)(p * 100)));

        try
        {
            UpdateStatusText.Text = Strings.Get("SettingsDownloadingUpdate", 0);
            // On success this shuts the whole app down to hand off to the relaunch
            // script — nothing after this line runs.
            await SelfUpdater.DownloadAndRestartAsync(assetUrl, progress);
        }
        catch (Exception ex)
        {
            AppLog.Error("Не удалось применить обновление", ex);
            UpdateStatusText.Text = Strings.Get("SettingsUpdateCheckFailed");
            CheckUpdateButton.IsEnabled = true;
        }
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
