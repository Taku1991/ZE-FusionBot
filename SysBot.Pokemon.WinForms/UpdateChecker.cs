using Newtonsoft.Json;
using SysBot.Pokemon.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace SysBot.Pokemon.WinForms
{
    public class UpdateChecker
    {
        private const string RepositoryOwner = "Taku1991";
        private const string RepositoryName = "ZE-FusionBot";

        // Reuse HttpClient for better performance and socket management
        private static readonly HttpClient _httpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Cache to prevent hitting GitHub API rate limits
        private static ReleaseInfo? _cachedRelease;
        private static DateTime _cacheExpiry = DateTime.MinValue;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);
        private static readonly object _cacheLock = new();

        static UpdateChecker()
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "ZE-FusionBot");
        }

        public static async Task<(bool UpdateAvailable, bool UpdateRequired, string NewVersion)> CheckForUpdatesAsync(bool forceShow = false)
        {
            ReleaseInfo? latestRelease = await FetchLatestReleaseAsync();

            bool updateAvailable = latestRelease != null && latestRelease.TagName != PokeBot.Version;
            bool updateRequired = latestRelease?.Prerelease == false && IsUpdateRequired(latestRelease?.Body);
            string? newVersion = latestRelease?.TagName;

            if (updateAvailable || forceShow)
            {
                var updateForm = new UpdateForm(updateRequired, newVersion ?? "", updateAvailable);
                updateForm.ShowDialog();
            }

            return (updateAvailable, updateRequired, newVersion ?? string.Empty);
        }

        public static async Task<string> FetchChangelogAsync()
        {
            ReleaseInfo? latestRelease = await FetchLatestReleaseAsync();
            return latestRelease?.Body ?? "Failed to fetch the latest release information.";
        }

        public static async Task<string?> FetchDownloadUrlAsync()
        {
            ReleaseInfo? latestRelease = await FetchLatestReleaseAsync();
            if (latestRelease?.Assets == null)
                return null;

            return latestRelease.Assets
            .FirstOrDefault(a => a.Name?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true)
            ?.BrowserDownloadUrl;
        }

        private static async Task<ReleaseInfo?> FetchLatestReleaseAsync(bool forceRefresh = false)
        {
            // Check cache first (unless force refresh)
            if (!forceRefresh)
            {
                lock (_cacheLock)
                {
                    if (_cachedRelease != null && DateTime.UtcNow < _cacheExpiry)
                    {
                        Console.WriteLine($"Using cached release info: {_cachedRelease.TagName} (expires in {(_cacheExpiry - DateTime.UtcNow).TotalMinutes:F1} minutes)");
                        return _cachedRelease;
                    }
                }
            }

            try
            {
                string releasesUrl = $"https://api.github.com/repos/{RepositoryOwner}/{RepositoryName}/releases/latest";
                Console.WriteLine($"Fetching release info from GitHub: {releasesUrl}");

                HttpResponseMessage response = await _httpClient.GetAsync(releasesUrl);

                // Log rate limit info regardless of success
                if (response.Headers.TryGetValues("X-RateLimit-Remaining", out var remaining))
                    Console.WriteLine($"GitHub Rate Limit Remaining: {string.Join(", ", remaining)}");
                if (response.Headers.TryGetValues("X-RateLimit-Reset", out var reset))
                {
                    var resetTime = DateTimeOffset.FromUnixTimeSeconds(long.Parse(reset.First()));
                    Console.WriteLine($"GitHub Rate Limit Reset: {resetTime.ToLocalTime()}");
                }

                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"GitHub API Error: {response.StatusCode} - {errorContent}");
                    return null;
                }

                string jsonContent = await response.Content.ReadAsStringAsync();
                var releaseInfo = JsonConvert.DeserializeObject<ReleaseInfo>(jsonContent);

                if (releaseInfo != null)
                {
                    // Update cache
                    lock (_cacheLock)
                    {
                        _cachedRelease = releaseInfo;
                        _cacheExpiry = DateTime.UtcNow.Add(CacheDuration);
                        Console.WriteLine($"Successfully fetched and cached release info: {releaseInfo.TagName} (cache valid for {CacheDuration.TotalMinutes} minutes)");
                    }
                }

                return releaseInfo;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching release info: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");

                // Return cached version as fallback if available
                lock (_cacheLock)
                {
                    if (_cachedRelease != null)
                    {
                        Console.WriteLine($"Using stale cached release info as fallback: {_cachedRelease.TagName}");
                        return _cachedRelease;
                    }
                }

                return null;
            }
        }

        /// <summary>
        /// Clear the cached release info to force a fresh check on next call
        /// </summary>
        public static void ClearCache()
        {
            lock (_cacheLock)
            {
                _cachedRelease = null;
                _cacheExpiry = DateTime.MinValue;
                Console.WriteLine("Release info cache cleared");
            }
        }

        private static bool IsUpdateRequired(string? changelogBody)
        {
            return !string.IsNullOrWhiteSpace(changelogBody) &&
                   changelogBody.Contains("Required = Yes", StringComparison.OrdinalIgnoreCase);
        }

        private class ReleaseInfo
        {
            [JsonProperty("tag_name")]
            public string? TagName { get; set; }

            [JsonProperty("prerelease")]
            public bool Prerelease { get; set; }

            [JsonProperty("assets")]
            public List<AssetInfo>? Assets { get; set; }

            [JsonProperty("body")]
            public string? Body { get; set; }
        }

        private class AssetInfo
        {
            [JsonProperty("name")]
            public string? Name { get; set; }

            [JsonProperty("browser_download_url")]
            public string? BrowserDownloadUrl { get; set; }
        }
    }
}
