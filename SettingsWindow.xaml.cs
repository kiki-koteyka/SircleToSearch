using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace SircleToSearch;

public partial class SettingsWindow : FluentWindow
{
    private const string IssuesUrl = "https://github.com/kiki-koteyka/SircleToSearch/issues/new";
    private const string RepoUrl = "https://github.com/kiki-koteyka/SircleToSearch";
    private const string AuthorUrl = "https://github.com/kiki-koteyka";
    private bool _loading = true;
    private MouseButtonEventHandler? _updateLinkHandler;

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
        CheckUpdateButton.Content = Strings.Get("SettingsCheckUpdate");
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
        CheckUpdateButton.IsEnabled = false;
        UpdateStatusText.Text = Strings.Get("SettingsCheckingUpdate");

        // Each check re-wires this link fresh — without removing the old handler first,
        // clicking "Check for updates" repeatedly stacked one MouseLeftButtonDown
        // subscription per check, so a single click on the link would open the download
        // page that many times over.
        if (_updateLinkHandler is not null)
        {
            UpdateStatusText.MouseLeftButtonDown -= _updateLinkHandler;
            _updateLinkHandler = null;
        }
        UpdateStatusText.Cursor = System.Windows.Input.Cursors.Arrow;
        UpdateStatusText.TextDecorations = null;

        try
        {
            var result = await UpdateChecker.CheckAsync();
            if (result.UpdateAvailable)
            {
                UpdateStatusText.Text = Strings.Get("SettingsUpdateAvailable", result.LatestVersion);
                var downloadUrl = result.ReleaseUrl;
                UpdateStatusText.Cursor = System.Windows.Input.Cursors.Hand;
                UpdateStatusText.TextDecorations = TextDecorations.Underline;
                _updateLinkHandler = (_, _) => OpenUrl(downloadUrl);
                UpdateStatusText.MouseLeftButtonDown += _updateLinkHandler;
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
