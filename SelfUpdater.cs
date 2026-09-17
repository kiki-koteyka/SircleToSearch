using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace SircleToSearch;

/// <summary>
/// Real in-place self-update: downloads the new exe, then hands off to a small detached
/// script that waits for this process to exit, swaps the file in, and relaunches — a
/// running exe can't overwrite its own file, so that handoff has to happen outside it.
/// </summary>
public static class SelfUpdater
{
    public static async Task DownloadAndRestartAsync(string downloadUrl, IProgress<double>? progress = null)
    {
        var currentExePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(currentExePath))
            throw new InvalidOperationException("Не удалось определить путь к своему exe.");

        var updateDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SircleToSearch", "update");
        Directory.CreateDirectory(updateDir);
        var newExePath = Path.Combine(updateDir, "SircleToSearch.new.exe");

        using (var client = new HttpClient())
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("SircleToSearch-UpdateChecker");
            using var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            var totalBytes = response.Content.Headers.ContentLength ?? -1L;

            await using var httpStream = await response.Content.ReadAsStreamAsync();
            await using var fileStream = new FileStream(newExePath, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[81920];
            long totalRead = 0;
            int read;
            while ((read = await httpStream.ReadAsync(buffer)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read));
                totalRead += read;
                if (totalBytes > 0)
                    progress?.Report((double)totalRead / totalBytes);
            }
        }

        var pid = Environment.ProcessId;
        var scriptPath = Path.Combine(updateDir, "apply-update.ps1");
        // Waits for this process to actually exit (the file lock on currentExePath only
        // releases then), retries the copy a few times in case Windows is still flushing
        // the handle, then relaunches from the original path and cleans up after itself.
        var script = $$"""
            $ErrorActionPreference = 'SilentlyContinue'
            try { Wait-Process -Id {{pid}} -Timeout 30 } catch {}
            for ($i = 0; $i -lt 20; $i++) {
                try {
                    Copy-Item -Path '{{newExePath}}' -Destination '{{currentExePath}}' -Force -ErrorAction Stop
                    break
                } catch {
                    Start-Sleep -Milliseconds 500
                }
            }
            Remove-Item -Path '{{newExePath}}' -Force
            Start-Process -FilePath '{{currentExePath}}'
            Remove-Item -Path '{{scriptPath}}' -Force
            """;
        await File.WriteAllTextAsync(scriptPath, script);

        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File \"{scriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
        });

        System.Windows.Application.Current.Shutdown();
    }
}
