using System;
using System.Windows;
using Wpf.Ui.Controls;

namespace SircleToSearch;

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
