using System.Net.Http.Headers;
using System.Text.Json;

namespace CrashLens.Desktop;

public sealed record ReleaseUpdate(Version Version, string InstallerUrl);

public sealed class UpdateService
{
    const string LatestReleaseUrl = "https://api.github.com/repos/jeonghayoon11/CrashLens/releases/latest";

    public async Task<ReleaseUpdate?> CheckAsync(Version currentVersion, CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("CrashLens", currentVersion.ToString()));
        using var response = await client.GetAsync(LatestReleaseUrl, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;

        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        var root = document.RootElement;
        if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean()) return null;
        var tag = root.GetProperty("tag_name").GetString()?.TrimStart('v', 'V');
        if (!Version.TryParse(tag, out var latest) || latest <= currentVersion) return null;

        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString();
            if (name is not null && name.StartsWith("CrashLens-Setup-", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                return new ReleaseUpdate(latest, asset.GetProperty("browser_download_url").GetString()!);
        }
        return null;
    }

    public async Task<string> DownloadInstallerAsync(ReleaseUpdate update, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        var updateDirectory = Path.Combine(Path.GetTempPath(), "CrashLens", "Updates");
        Directory.CreateDirectory(updateDirectory);
        var installerPath = Path.Combine(updateDirectory, $"CrashLens-Setup-{update.Version}.exe");

        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("CrashLens", update.Version.ToString()));
        using var response = await client.GetAsync(update.InstallerUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength;
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var destination = File.Create(installerPath);
        var buffer = new byte[81920]; long downloaded = 0; int count;
        while ((count = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
            downloaded += count;
            if (total is > 0) progress?.Report((int)(downloaded * 100 / total.Value));
        }
        return installerPath;
    }
}
