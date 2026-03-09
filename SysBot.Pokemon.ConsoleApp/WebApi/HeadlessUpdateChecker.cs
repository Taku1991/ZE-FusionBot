using Newtonsoft.Json;
using SysBot.Base;
using SysBot.Pokemon.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace SysBot.Pokemon.ConsoleApp.WebApi;

/// <summary>
/// Platform-neutral Update Checker ohne WinForms-Dialoge (für headless LXC).
/// </summary>
internal static class HeadlessUpdateChecker
{
    private const string RepositoryOwner = "Taku1991";
    private const string RepositoryName = "ZE-FusionBot";

    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

    static HeadlessUpdateChecker()
    {
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "ZE-FusionBot");
    }

    public static async Task<(bool UpdateAvailable, bool UpdateRequired, string NewVersion, string? DownloadUrl)> CheckForUpdatesAsync(bool _ = false)
    {
        var latestRelease = await FetchLatestReleaseAsync();

        bool updateAvailable = latestRelease != null && latestRelease.TagName != PokeBot.Version;
        bool updateRequired = latestRelease?.Prerelease == false && IsUpdateRequired(latestRelease?.Body);
        string? newVersion = latestRelease?.TagName;
        string? downloadUrl = SelectBestAsset(latestRelease?.Assets);

        return (updateAvailable, updateRequired, newVersion ?? string.Empty, downloadUrl);
    }

    public static async Task<string> FetchChangelogAsync()
    {
        var latestRelease = await FetchLatestReleaseAsync();
        return latestRelease?.Body ?? "Failed to fetch the latest release information.";
    }

    public static async Task<string?> FetchDownloadUrlAsync()
    {
        var latestRelease = await FetchLatestReleaseAsync();
        return SelectBestAsset(latestRelease?.Assets);
    }

    private static string? SelectBestAsset(List<AssetInfo>? assets)
    {
        if (assets == null) return null;

        bool isLinux = !OperatingSystem.IsWindows();

        if (isLinux)
        {
            // Prefer Linux binary by exact name or no extension
            return assets.FirstOrDefault(a =>
                    a.Name == "SysBot.Pokemon.ConsoleApp" ||
                    a.Name == "ZE_FusionBot" ||
                    (a.Name != null && !a.Name.Contains('.') && a.Name.Contains("Linux", StringComparison.OrdinalIgnoreCase)))
                ?.BrowserDownloadUrl
                ?? assets.FirstOrDefault(a => a.Name?.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) == true)
                ?.BrowserDownloadUrl;
        }

        // Windows: prefer .exe
        return assets.FirstOrDefault(a => a.Name?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true)
            ?.BrowserDownloadUrl;
    }

    private static async Task<ReleaseInfo?> FetchLatestReleaseAsync()
    {
        try
        {
            bool isLinux = !OperatingSystem.IsWindows();

            if (isLinux)
            {
                // On Linux, search through all releases to find the latest one with a Linux asset
                var url = $"https://api.github.com/repos/{RepositoryOwner}/{RepositoryName}/releases?per_page=10";
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    LogUtil.LogError("HeadlessUpdateChecker", $"GitHub API error: {response.StatusCode}");
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                var releases = JsonConvert.DeserializeObject<List<ReleaseInfo>>(json);
                return releases?.FirstOrDefault(r => r.Assets?.Any(a =>
                    a.Name != null && (
                        a.Name == "SysBot.Pokemon.ConsoleApp" ||
                        a.Name == "ZE_FusionBot" ||
                        (!a.Name.Contains('.') && a.Name.Contains("Linux", StringComparison.OrdinalIgnoreCase)) ||
                        !a.Name.Contains('.')
                    )) == true);
            }
            else
            {
                var url = $"https://api.github.com/repos/{RepositoryOwner}/{RepositoryName}/releases/latest";
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    LogUtil.LogError("HeadlessUpdateChecker", $"GitHub API error: {response.StatusCode}");
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<ReleaseInfo>(json);
            }
        }
        catch (Exception ex)
        {
            LogUtil.LogError("HeadlessUpdateChecker", $"Error fetching release: {ex.Message}");
            return null;
        }
    }

    private static bool IsUpdateRequired(string? body) =>
        !string.IsNullOrWhiteSpace(body) &&
        body.Contains("Required = Yes", StringComparison.OrdinalIgnoreCase);

    private sealed class ReleaseInfo
    {
        [JsonProperty("tag_name")] public string? TagName { get; set; }
        [JsonProperty("prerelease")] public bool Prerelease { get; set; }
        [JsonProperty("assets")] public List<AssetInfo>? Assets { get; set; }
        [JsonProperty("body")] public string? Body { get; set; }
    }

    private sealed class AssetInfo
    {
        [JsonProperty("name")] public string? Name { get; set; }
        [JsonProperty("browser_download_url")] public string? BrowserDownloadUrl { get; set; }
    }
}
