using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace SircleToSearch;

public static class UpdateChecker
{
    private const string LatestReleaseApiUrl =
        "https://api.github.com/repos/kiki-koteyka/SircleToSearch/releases/latest";

    public sealed record Result(bool UpdateAvailable, string LatestVersion, string ReleaseUrl, string? AssetDownloadUrl);

    public static async Task<Result> CheckAsync()
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SircleToSearch-UpdateChecker");

        var json = await client.GetStringAsync(LatestReleaseApiUrl);
        using var doc = JsonDocument.Parse(json);

        var tag = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
        var url = doc.RootElement.TryGetProperty("html_url", out var urlProp)
            ? urlProp.GetString() ?? ""
            : "";
        var latestVersion = tag.TrimStart('v', 'V');

        string? assetUrl = null;
        if (doc.RootElement.TryGetProperty("assets", out var assets))
        {
            foreach (var asset in assets.EnumerateArray())
            {
                if (asset.GetProperty("name").GetString() == "SircleToSearch.exe")
                {
                    assetUrl = asset.GetProperty("browser_download_url").GetString();
                    break;
                }
            }
        }

        return new Result(IsNewer(latestVersion, AppVersion.Current), latestVersion, url, assetUrl);
    }

    private static bool IsNewer(string latest, string current)
    {
        if (Version.TryParse(PadForVersion(latest), out var latestV)
            && Version.TryParse(PadForVersion(current), out var currentV))
        {
            return latestV > currentV;
        }
        return !string.Equals(latest, current, StringComparison.OrdinalIgnoreCase);
    }

    private static string PadForVersion(string v) => v.Contains('.') ? v : v + ".0";
}
