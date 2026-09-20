using System;
using System.IO;
using System.Text.Json;

namespace SircleToSearch;

public enum SearchEngine { Google, Yandex }

public sealed class AppSettings
{
    public string Language { get; set; } = "en";
    public bool FirstRunCompleted { get; set; }

    public SearchEngine Engine { get; set; } = SearchEngine.Google;

    public bool FastSearch { get; set; }

    public uint HotkeyModifiers { get; set; } = (uint)(HotkeyManager.Modifiers.Win | HotkeyManager.Modifiers.Shift);
    public uint HotkeyVk { get; set; } = 0x51;

    public bool AutoUpdate { get; set; }

    public double SelectionHug { get; set; } = 0;
    public double SelectionCornerRadius { get; set; } = 10;
    public double SelectionArmLength { get; set; } = 18;
    public double SelectionThickness { get; set; } = 3;
    public double SelectionGlowGap { get; set; } = 5;
    public double SelectionGlowThickness { get; set; } = 10;
    public double SelectionGlowBlur { get; set; } = 18;
    public double SelectionGlowOpacity { get; set; } = 40;
    public double SelectionRadius { get; set; } = 12;

    public string AccentColor { get; set; } = "#009FAA";

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SircleToSearch", "settings.json");

    public static AppSettings Current { get; private set; } = Load();

    private static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded is not null) return loaded;
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("Не удалось прочитать settings.json", ex);
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this));
        }
        catch (Exception ex)
        {
            AppLog.Error("Не удалось сохранить settings.json", ex);
        }
    }
}
