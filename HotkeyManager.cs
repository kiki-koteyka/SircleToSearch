using System;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace SircleToSearch;

public sealed class HotkeyManager : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const int HOTKEY_ID = 0xA11CE;

    [Flags]
    public enum Modifiers : uint
    {
        None = 0,
        Alt = 0x0001,
        Control = 0x0002,
        Shift = 0x0004,
        Win = 0x0008,
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly HwndSource _source;
    private bool _registered;

    public event Action? HotkeyPressed;
    public Modifiers CurrentModifiers { get; private set; }
    public uint CurrentVk { get; private set; }

    public HotkeyManager(Modifiers modifiers, uint vk)
    {
        // Plain invisible top-level window (0x0 size, no WS_VISIBLE). HWND_MESSAGE
        // windows are unreliable for WM_HOTKEY delivery on some setups — this is the
        // pattern every WPF global-hotkey library actually ships.
        var parameters = new HwndSourceParameters("SircleToSearchHotkeySink")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0,
            ExtendedWindowStyle = 0x80, // WS_EX_TOOLWINDOW — keeps it out of alt-tab/taskbar
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);

        if (!Register(modifiers, vk, out var win32Error))
        {
            MessageBox.Show(Strings.Get("HotkeyFailed", win32Error),
                "SircleToSearch", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>Switches to a new key combo, e.g. after the user rebinds it in Settings.
    /// Returns false (and leaves the previous binding registered, since nothing new took
    /// its place) if the new combo is already claimed by another program.</summary>
    public bool TryRebind(Modifiers modifiers, uint vk)
    {
        var previousModifiers = CurrentModifiers;
        var previousVk = CurrentVk;
        var wasRegistered = _registered;

        if (_registered)
        {
            UnregisterHotKey(_source.Handle, HOTKEY_ID);
            _registered = false;
        }

        if (Register(modifiers, vk, out _)) return true;

        if (wasRegistered) Register(previousModifiers, previousVk, out _);
        return false;
    }

    private bool Register(Modifiers modifiers, uint vk, out int win32Error)
    {
        _registered = RegisterHotKey(_source.Handle, HOTKEY_ID, (uint)modifiers, vk);
        win32Error = _registered ? 0 : Marshal.GetLastWin32Error();
        if (_registered)
        {
            CurrentModifiers = modifiers;
            CurrentVk = vk;
        }

        var log = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SircleToSearch", "hotkey.log");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(log)!);
        System.IO.File.AppendAllText(log,
            $"{DateTime.Now:O} hwnd={_source.Handle} registered={_registered} win32Error={win32Error}\n");

        return _registered;
    }

    public static string Format(Modifiers modifiers, uint vk)
    {
        var parts = new System.Collections.Generic.List<string>();
        if (modifiers.HasFlag(Modifiers.Win)) parts.Add("Win");
        if (modifiers.HasFlag(Modifiers.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(Modifiers.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(Modifiers.Shift)) parts.Add("Shift");
        parts.Add(KeyInterop.KeyFromVirtualKey((int)vk).ToString().ToUpperInvariant());
        return string.Join("+", parts);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            var log = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SircleToSearch", "hotkey.log");
            System.IO.File.AppendAllText(log, $"{DateTime.Now:O} WM_HOTKEY fired\n");

            HotkeyPressed?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_registered)
        {
            UnregisterHotKey(_source.Handle, HOTKEY_ID);
            _registered = false;
        }
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
