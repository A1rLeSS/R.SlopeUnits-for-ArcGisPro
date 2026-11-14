using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace BasinSlopeTools
{
    internal sealed class ReleaseAssetInfo
    {
        public string Name { get; set; }
        public string DownloadUrl { get; set; }
        public long Size { get; set; }
    }

    internal sealed class ReleaseInfo
    {
        public string TagName { get; set; }
        public string Version { get; set; }
        public string Body { get; set; }
        public string HtmlUrl { get; set; }
        public List<ReleaseAssetInfo> Assets { get; set; } = new List<ReleaseAssetInfo>();
    }

    internal sealed class UpdateCheckResult
    {
        public ReleaseInfo Release { get; set; }
        public bool UpdateAvailable { get; set; }
        public string Error { get; set; }
    }

    internal static class UpdateService
    {
        private static readonly HttpClient Http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(12)
        };
        private static readonly string CacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BasinSlopeTools");
        private static readonly string CachePath = Path.Combine(CacheDir, "update_cache.json");
        private static int _autoStarted;

        public static string CurrentVersion => AddinInfo.Version;

        public static async Task<UpdateCheckResult> CheckAsync(bool force)
        {
            try
            {
                if (!force)
                {
                    var cached = ReadCache();
                    if (cached != null &&
                        (DateTimeOffset.UtcNow - cached.CheckedAt).TotalHours < 12)
                        return BuildResult(cached.Release);
                }

                using var request = new HttpRequestMessage(
                    HttpMethod.Get, AddinInfo.LatestReleaseApi);
                request.Headers.UserAgent.ParseAdd("BasinSlopeTools/" + CurrentVersion);
                request.Headers.Accept.ParseAdd("application/vnd.github+json");
                using var response = await Http.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                    return new UpdateCheckResult
                    {
                        Error = "GitHub API HTTP " + (int)response.StatusCode
                    };
                var json = await response.Content.ReadAsStringAsync();
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                var release = new ReleaseInfo
                {
                    TagName = root.GetProperty("tag_name").GetString() ?? "",
                    Body = root.TryGetProperty("body", out var body)
                        ? body.GetString() ?? "" : "",
                    HtmlUrl = root.TryGetProperty("html_url", out var html)
                        ? html.GetString() ?? "" : AddinInfo.ReleasesPage
                };
                release.Version = release.TagName.TrimStart('v', 'V');
                if (root.TryGetProperty("assets", out var assets))
                {
                    foreach (var asset in assets.EnumerateArray())
                    {
                        release.Assets.Add(new ReleaseAssetInfo
                        {
                            Name = asset.GetProperty("name").GetString() ?? "",
                            DownloadUrl = asset.GetProperty("browser_download_url").GetString() ?? "",
                            Size = asset.TryGetProperty("size", out var size) ? size.GetInt64() : 0
                        });
                    }
                }
                WriteCache(release);
                return BuildResult(release);
            }
            catch (Exception ex)
            {
                return new UpdateCheckResult { Error = ex.Message };
            }
        }

        private static UpdateCheckResult BuildResult(ReleaseInfo release)
        {
            return new UpdateCheckResult
            {
                Release = release,
                UpdateAvailable = IsNewer(release.Version, CurrentVersion)
            };
        }

        private static bool IsNewer(string candidate, string current)
        {
            if (!Version.TryParse(candidate, out var newer) ||
                !Version.TryParse(current, out var local))
                return false;
            return newer > local;
        }

        public static void AutoCheckInBackground()
        {
            if (Interlocked.Exchange(ref _autoStarted, 1) == 1)
                return;
            _ = Task.Run(async () =>
            {
                var result = await CheckAsync(false);
                if (!result.UpdateAvailable || result.Release == null)
                    return;
                if (Application.Current == null)
                    return;
                await Application.Current.Dispatcher.InvokeAsync(
                    () => UpdateWindow.ShowRelease(result.Release));
            });
        }

        public static async Task<string> DownloadAndVerifyAsync(
            ReleaseInfo release, IProgress<int> progress, CancellationToken token)
        {
            var installer = release.Assets.FirstOrDefault(
                a => a.Name.StartsWith("BasinSlopeTools_Setup_", StringComparison.OrdinalIgnoreCase)
                     && a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
            var checksums = release.Assets.FirstOrDefault(
                a => a.Name.Equals("SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase));
            if (installer == null)
                throw new InvalidOperationException("Release 中没有找到安装器资产。");
            if (checksums == null)
                throw new InvalidOperationException("Release 中没有找到 SHA256SUMS.txt。");

            Directory.CreateDirectory(CacheDir);
            var destination = Path.Combine(CacheDir, installer.Name);
            using var response = await Http.GetAsync(
                installer.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? installer.Size;
            await using (var source = await response.Content.ReadAsStreamAsync(token))
            await using (var target = new FileStream(
                destination, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[1024 * 256];
                long copied = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                {
                    await target.WriteAsync(buffer, 0, read, token);
                    copied += read;
                    if (total > 0)
                        progress?.Report((int)Math.Min(100, copied * 100 / total));
                }
            }

            var checksumText = await Http.GetStringAsync(checksums.DownloadUrl, token);
            var expected = ParseChecksum(checksumText, installer.Name);
            if (string.IsNullOrWhiteSpace(expected))
            {
                File.Delete(destination);
                throw new InvalidOperationException("校验文件中没有安装器 SHA256。");
            }
            using var sha = SHA256.Create();
            await using (var stream = File.OpenRead(destination))
            {
                var actual = Convert.ToHexString(await sha.ComputeHashAsync(stream, token));
                if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(destination);
                    throw new InvalidOperationException("安装器 SHA256 校验失败。");
                }
            }
            return destination;
        }

        private static string ParseChecksum(string text, string fileName)
        {
            foreach (var raw in text.Split(new[] { '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries))
            {
                var line = raw.Trim();
                var marker = line.IndexOf(' ');
                if (marker <= 0)
                    continue;
                var hash = line.Substring(0, marker).Trim();
                var name = line.Substring(marker).Trim().TrimStart('*');
                if (name.Equals(fileName, StringComparison.OrdinalIgnoreCase))
                    return hash;
            }
            return null;
        }

        public static void LaunchDeferredInstaller(string installerPath)
        {
            var proPath = Process.GetCurrentProcess().MainModule?.FileName ?? "ArcGISPro.exe";
            var scriptPath = Path.Combine(CacheDir, "run_update.ps1");
            Directory.CreateDirectory(CacheDir);
            var script = @"
$ErrorActionPreference = 'Stop'
$pidToWait = " + Process.GetCurrentProcess().Id + @"
while (Get-Process -Id $pidToWait -ErrorAction SilentlyContinue) {
    Start-Sleep -Seconds 2
}
$p = Start-Process -FilePath " + PsQuote(installerPath) + @" -ArgumentList '--silent','--accept-license' -Wait -PassThru
if ($p.ExitCode -eq 0) {
    Start-Process -FilePath " + PsQuote(proPath) + @"
}
";
            File.WriteAllText(scriptPath, script, new UTF8Encoding(false));
            var info = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -File " + PsQuote(scriptPath),
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            Process.Start(info);
        }

        private static string PsQuote(string value)
        {
            return "'" + value.Replace("'", "''") + "'";
        }

        private static CachedRelease ReadCache()
        {
            try
            {
                if (!File.Exists(CachePath))
                    return null;
                using var doc = JsonDocument.Parse(File.ReadAllText(CachePath));
                var root = doc.RootElement;
                var release = new ReleaseInfo
                {
                    TagName = root.GetProperty("tag").GetString(),
                    Version = root.GetProperty("version").GetString(),
                    Body = root.GetProperty("body").GetString(),
                    HtmlUrl = root.GetProperty("html").GetString()
                };
                if (root.TryGetProperty("assets", out var assets))
                {
                    foreach (var item in assets.EnumerateArray())
                    {
                        release.Assets.Add(new ReleaseAssetInfo
                        {
                            Name = item.GetProperty("name").GetString(),
                            DownloadUrl = item.GetProperty("url").GetString(),
                            Size = item.GetProperty("size").GetInt64()
                        });
                    }
                }
                return new CachedRelease
                {
                    CheckedAt = root.GetProperty("checked_at").GetDateTimeOffset(),
                    Release = release
                };
            }
            catch
            {
                return null;
            }
        }

        private static void WriteCache(ReleaseInfo release)
        {
            try
            {
                Directory.CreateDirectory(CacheDir);
                var data = new
                {
                    checked_at = DateTimeOffset.UtcNow,
                    tag = release.TagName,
                    version = release.Version,
                    body = release.Body,
                    html = release.HtmlUrl,
                    assets = release.Assets.Select(a => new
                    {
                        name = a.Name,
                        url = a.DownloadUrl,
                        size = a.Size
                    })
                };
                File.WriteAllText(CachePath,
                    JsonSerializer.Serialize(data), new UTF8Encoding(false));
            }
            catch
            {
                // An update check must never block the add-in.
            }
        }

        private sealed class CachedRelease
        {
            public DateTimeOffset CheckedAt { get; set; }
            public ReleaseInfo Release { get; set; }
        }
    }
}
