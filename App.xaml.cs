using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using WinForms = System.Windows.Forms;

namespace SircleToSearch;

public partial class App : System.Windows.Application
{
    private Mutex? _singleInstanceMutex;
    private WinForms.NotifyIcon? _trayIcon;
    private HotkeyManager? _hotkeyManager;
    private OverlayWindow? _activeOverlay;
    private SettingsWindow? _settingsWindow;
    private UpdatePromptWindow? _updatePromptWindow;
    private string? _pendingUpdateAssetUrl;
    private string? _updateNotifiedVersion;

    public HotkeyManager? HotkeyManager => _hotkeyManager;

    protected override void OnStartup(StartupEventArgs e)
    {
        WinForms.Application.SetHighDpiMode(WinForms.HighDpiMode.PerMonitorV2);
        base.OnStartup(e);

        Wpf.Ui.Appearance.ApplicationAccentColorManager.Apply(
            System.Windows.Media.Color.FromRgb(0x00, 0x9F, 0xAA), Wpf.Ui.Appearance.ApplicationTheme.Light);

        DispatcherUnhandledException += (_, args) =>
        {
            AppLog.Error("Необработанное исключение (UI-поток)", args.Exception);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                AppLog.Error("Необработанное исключение (фон)", ex);
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppLog.Error("Необработанное исключение (Task)", args.Exception);
            args.SetObserved();
        };
        AppLog.ErrorRaised += message =>
        {
            Current?.Dispatcher.Invoke(() =>
                _trayIcon?.ShowBalloonTip(4000, "SircleToSearch", message, WinForms.ToolTipIcon.Error));
        };

        _singleInstanceMutex = new Mutex(true, "SircleToSearch_SingleInstance", out var isNew);
        if (!isNew)
        {
            System.Windows.MessageBox.Show(Strings.Get("AlreadyRunning"),
                "SircleToSearch", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        SetupTrayIcon();

        _hotkeyManager = new HotkeyManager(
            (HotkeyManager.Modifiers)AppSettings.Current.HotkeyModifiers, AppSettings.Current.HotkeyVk);
        _hotkeyManager.HotkeyPressed += ShowOverlay;

        if (!AppSettings.Current.FirstRunCompleted)
        {
            OpenSettings();
        }
    }

    private void SetupTrayIcon()
    {
        // Pulled from our own exe (baked in via ApplicationIcon in the csproj) instead
        // of the generic system icon - same code works before and after publish since
        // it just reads whatever icon is embedded in the running executable.
        System.Drawing.Icon icon;
        try
        {
            icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!)
                ?? System.Drawing.SystemIcons.Application;
        }
        catch
        {
            icon = System.Drawing.SystemIcons.Application;
        }

        _trayIcon = new WinForms.NotifyIcon
        {
            Icon = icon,
            Visible = true,
        };
        _trayIcon.DoubleClick += (_, _) => OpenSettings();
        _trayIcon.BalloonTipClicked += (_, _) => ShowUpdatePrompt();

        RebuildTrayMenu();
    }

    private void RebuildTrayMenu()
    {
        if (_trayIcon is null) return;

        var hotkeyDisplay = HotkeyManager.Format(
            (HotkeyManager.Modifiers)AppSettings.Current.HotkeyModifiers, AppSettings.Current.HotkeyVk);

        var menu = new WinForms.ContextMenuStrip();

        var settingsItem = new WinForms.ToolStripMenuItem(Strings.Get("MenuSettings"));
        settingsItem.Click += (_, _) => OpenSettings();
        menu.Items.Add(settingsItem);

        menu.Items.Add(new WinForms.ToolStripSeparator());

        var triggerItem = new WinForms.ToolStripMenuItem(Strings.Get("MenuFindNow", hotkeyDisplay));
        triggerItem.Click += (_, _) => ShowOverlay();
        menu.Items.Add(triggerItem);

        menu.Items.Add(new WinForms.ToolStripSeparator());

        var exitItem = new WinForms.ToolStripMenuItem(Strings.Get("MenuExit"));
        exitItem.Click += (_, _) => Shutdown();
        menu.Items.Add(exitItem);

        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.Text = Strings.Get("TrayTooltip", hotkeyDisplay);
    }

    public void OpenSettings()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow();
        _settingsWindow.LanguageChanged += RebuildTrayMenu;
        _settingsWindow.HotkeyRebound += RebuildTrayMenu;
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void ShowOverlay()
    {
        if (_activeOverlay is not null) return;

        CheckForUpdatesInBackground();

        Current.Dispatcher.Invoke(() =>
        {
            _activeOverlay = new OverlayWindow();
            _activeOverlay.Closed += (_, _) => _activeOverlay = null;
            _activeOverlay.Show();
        });
    }

    /// <summary>Fire-and-forget, run once per search request. Cheap and stateless on
    /// GitHub's end, so no need to throttle the check itself - only the user-visible
    /// notification is deduped (once per newly-seen version per run) so this doesn't
    /// spam a balloon on every single search.</summary>
    private async void CheckForUpdatesInBackground()
    {
        try
        {
            var result = await UpdateChecker.CheckAsync();
            if (!result.UpdateAvailable || result.AssetDownloadUrl is null) return;
            if (result.LatestVersion == _updateNotifiedVersion) return;
            _updateNotifiedVersion = result.LatestVersion;
            _pendingUpdateAssetUrl = result.AssetDownloadUrl;

            if (AppSettings.Current.AutoUpdate)
            {
                _ = ApplyUpdateWhenIdleAsync(result.AssetDownloadUrl);
            }
            else
            {
                _ = NotifyUpdateWhenIdleAsync(result.LatestVersion);
            }
        }
        catch (Exception ex)
        {
            AppLog.Info($"Фоновая проверка обновлений не удалась: {ex.Message}");
        }
    }

    /// <summary>Holds off the tray balloon until the search overlay (selection + result)
    /// is fully closed - popping it up mid-search was distracting, right when the user
    /// is trying to drag a selection or read the result.</summary>
    private async Task NotifyUpdateWhenIdleAsync(string version)
    {
        while (_activeOverlay is not null)
            await Task.Delay(500);

        _trayIcon?.ShowBalloonTip(8000, "SircleToSearch",
            Strings.Get("UpdateBalloonText", version), WinForms.ToolTipIcon.Info);
    }

    /// <summary>Entry point for anywhere in the app (background check, manual "Check for
    /// updates" in Settings) that's found a newer version and wants to offer it - always
    /// goes through the same confirm dialog and idle-deferred apply.</summary>
    public void OfferUpdate(string version, string assetUrl)
    {
        _updateNotifiedVersion = version;
        _pendingUpdateAssetUrl = assetUrl;
        ShowUpdatePrompt();
    }

    private void ShowUpdatePrompt()
    {
        if (_pendingUpdateAssetUrl is not { } assetUrl) return;

        if (_updatePromptWindow is not null)
        {
            _updatePromptWindow.Activate();
            return;
        }

        _updatePromptWindow = new UpdatePromptWindow(_updateNotifiedVersion ?? "");
        _updatePromptWindow.UpdateAccepted += () => _ = ApplyUpdateWhenIdleAsync(assetUrl);
        _updatePromptWindow.Closed += (_, _) => _updatePromptWindow = null;
        _updatePromptWindow.Show();
        _updatePromptWindow.Activate();
    }

    /// <summary>Waits for the search overlay to be fully closed and then a further 5
    /// idle seconds before actually downloading and swapping the exe - never yanks the
    /// app out from under an in-progress search.</summary>
    private async Task ApplyUpdateWhenIdleAsync(string assetUrl)
    {
        while (_activeOverlay is not null)
            await Task.Delay(500);

        await Task.Delay(5000);
        if (_activeOverlay is not null) return; // a new search started during the wait - next check will retry

        try
        {
            await SelfUpdater.DownloadAndRestartAsync(assetUrl);
        }
        catch (Exception ex)
        {
            AppLog.Error("Автообновление не удалось", ex);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeyManager?.Dispose();
        _trayIcon?.Dispose();
        _singleInstanceMutex?.ReleaseMutex();
        base.OnExit(e);
    }
}
