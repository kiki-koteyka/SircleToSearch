using System;
using System.Windows;
using Wpf.Ui.Controls;

namespace SircleToSearch;

/// <summary>
/// Asks whether to install a newer version - shown when the user clicks the tray
/// notification for an update found during a background check (never pops up on its
/// own). "Update" doesn't download anything itself: it just tells the caller to go
/// ahead via <see cref="UpdateAccepted"/>, which App defers until the search overlay is
/// closed and idle.
/// </summary>
public partial class UpdatePromptWindow : FluentWindow
{
    private bool _loading = true;

    public event Action? UpdateAccepted;

    public UpdatePromptWindow(string version)
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            QuestionText.Text = Strings.Get("UpdatePromptQuestion", version);
            HintText.Text = Strings.Get("UpdatePromptHint");
            AutoUpdateLabel.Text = Strings.Get("UpdatePromptAutoUpdate");
            LaterButton.Content = Strings.Get("UpdatePromptLater");
            UpdateButton.Content = Strings.Get("UpdatePromptUpdate");
            AutoUpdateToggle.IsChecked = AppSettings.Current.AutoUpdate;
            _loading = false;
        };
    }

    private void AutoUpdateToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        AppSettings.Current.AutoUpdate = AutoUpdateToggle.IsChecked == true;
        AppSettings.Current.Save();
    }

    private void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        UpdateAccepted?.Invoke();
        Close();
    }

    private void LaterButton_Click(object sender, RoutedEventArgs e) => Close();
}
