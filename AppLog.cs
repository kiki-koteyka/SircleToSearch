using System;
using System.IO;

namespace SircleToSearch;

public static class AppLog
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SircleToSearch", "app.log");

    public static event Action<string>? ErrorRaised;

    public static void Error(string message, Exception ex)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"{DateTime.Now:O} {message}: {ex}\n\n");
        }
        catch
        {
        }

        ErrorRaised?.Invoke(message);
    }

    public static void Info(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"{DateTime.Now:O} {message}\n");
        }
        catch
        {
        }
    }
}
