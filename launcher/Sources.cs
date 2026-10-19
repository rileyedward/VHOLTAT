using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace VHOLTAT.Launcher
{
    /// <summary>The newest VHOLTAT release on GitHub.</summary>
    public sealed class ModRelease
    {
        public Version Version { get; set; }
        public string Tag { get; set; }
        public string PageUrl { get; set; }
        public string ZipName { get; set; }
        public string ZipUrl { get; set; }
        /// <summary>The .sha256 file next to the zip, if the release has one.</summary>
        public string Sha256Url { get; set; }
        /// <summary>The launcher exe attached to the same release, if any.</summary>
        public string LauncherUrl { get; set; }
    }

    /// <summary>GitHub Releases (only when LauncherConfig.GitHubRepo is set) and the pinned BepInEx download.</summary>
    public sealed class Sources : IDisposable
    {
        private readonly HttpClient http;
        private readonly LauncherPaths paths;
        private readonly Action<string> log;

        public Sources(LauncherPaths paths, Action<string> log)
        {
            this.paths = paths;
            this.log = log;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            http = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate });
            http.DefaultRequestHeaders.UserAgent.ParseAdd($"VHOLTAT-Launcher/{LauncherConfig.LauncherVersion.ToString(3)}");
            http.Timeout = TimeSpan.FromMinutes(10);
        }

        public void Dispose() => http.Dispose();

        /// <summary>Reports download progress: file name, bytes so far, total (0 if unknown).</summary>
        public Action<string, long, long> Progress { get; set; }

        /// <summary>The latest (non-prerelease) GitHub release with a VHOLTAT zip, or null if there's none yet or no repo is set.</summary>
        public async Task<ModRelease> LatestModRelease()
        {
            if (!LauncherConfig.HasGitHub) return null;
            var url = $"https://api.github.com/repos/{LauncherConfig.GitHubRepo}/releases/latest";
            using var response = await http.GetAsync(url);
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();
            var json = Json.Parse(await response.Content.ReadAsStringAsync());

            var release = new ModRelease { Tag = json.Str("tag_name"), PageUrl = json.Str("html_url") };
            release.Version = ParseVersion(release.Tag);
            var assets = ((IEnumerable)json["assets"]).Cast<Dictionary<string, object>>().ToList();
            foreach (var asset in assets)
            {
                var name = asset.Str("name") ?? "";
                var download = asset.Str("browser_download_url");
                if (name.StartsWith(LauncherConfig.LauncherAssetPrefix, StringComparison.OrdinalIgnoreCase) && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) release.LauncherUrl = download;
                else if (name.StartsWith(LauncherConfig.ModZipPrefix, StringComparison.OrdinalIgnoreCase) && name.EndsWith(LauncherConfig.ModZipSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    release.ZipName = name;
                    release.ZipUrl = download;
                }
            }
            if (release.ZipUrl == null) return null;
            release.Sha256Url = assets.FirstOrDefault(a => string.Equals(a.Str("name"), release.ZipName + ".sha256", StringComparison.OrdinalIgnoreCase))?.Str("browser_download_url");
            release.Version ??= ParseVersion(VersionFromZipName(release.ZipName));
            return release;
        }

        /// <summary>Downloads the release zip into the cache (checking its .sha256 if there is one) and returns its path.</summary>
        public async Task<string> DownloadModZip(ModRelease release)
        {
            var target = Path.Combine(paths.Cache, release.ZipName);
            string expected = null;
            if (release.Sha256Url != null)
            {
                var text = await http.GetStringAsync(release.Sha256Url);
                expected = text.Trim().Split(' ', '\t')[0].ToLowerInvariant();
            }
            if (File.Exists(target) && (expected == null || Sha256(target) == expected))
            {
                log($"Using cached {release.ZipName}");
                return target;
            }
            await Download(release.ZipUrl, target);
            if (expected != null && Sha256(target) != expected)
            {
                File.Delete(target);
                throw new InvalidDataException($"{release.ZipName} doesn't match its .sha256 file. Try again, or download it from {release.PageUrl}.");
            }
            return target;
        }

        /// <summary>A pinned third-party file in the cache, downloaded (from the first mirror that works) if needed and
        /// always checked against its SHA-256.</summary>
        public async Task<string> Get(Pinned pinned)
        {
            var target = Path.Combine(paths.Cache, pinned.File);
            if (File.Exists(target) && Sha256(target) == pinned.Sha256)
            {
                log($"Using cached {pinned.File}");
                return target;
            }
            Exception last = null;
            foreach (var url in pinned.Urls)
            {
                try
                {
                    await Download(url, target);
                    if (Sha256(target) == pinned.Sha256) return target;
                    File.Delete(target);
                    last = new InvalidDataException($"{pinned.File} from {new Uri(url).Host} didn't match its expected checksum.");
                }
                catch (Exception e) when (e is HttpRequestException || e is IOException || e is TaskCanceledException)
                {
                    last = e;
                    log($"  {new Uri(url).Host} failed ({e.GetBaseException().Message}), trying the next mirror");
                }
            }
            throw new InvalidOperationException($"Couldn't download {pinned.File}: {last?.GetBaseException().Message}");
        }

        private async Task Download(string url, string target)
        {
            var name = Path.GetFileName(target);
            log($"Downloading {name} from {new Uri(url).Host}");
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            var partial = target + ".part";
            using (var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                var total = response.Content.Headers.ContentLength ?? 0;
                using var input = await response.Content.ReadAsStreamAsync();
                using var output = File.Create(partial);
                var buffer = new byte[1 << 16];
                long done = 0, lastReport = 0;
                int read;
                while ((read = await input.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await output.WriteAsync(buffer, 0, read);
                    done += read;
                    if (done - lastReport > (1 << 20) || done == total) { lastReport = done; Progress?.Invoke(name, done, total); }
                }
            }
            if (File.Exists(target)) File.Delete(target);
            File.Move(partial, target);
        }

        public static string Sha256(string file)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(file);
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>"VHOLTAT-0.1.0-manual.zip" -> "0.1.0", or null.</summary>
        public static string VersionFromZipName(string name)
        {
            if (name == null || !name.StartsWith(LauncherConfig.ModZipPrefix, StringComparison.OrdinalIgnoreCase)) return null;
            var rest = name.Substring(LauncherConfig.ModZipPrefix.Length);
            if (rest.EndsWith(LauncherConfig.ModZipSuffix, StringComparison.OrdinalIgnoreCase)) rest = rest.Substring(0, rest.Length - LauncherConfig.ModZipSuffix.Length);
            else if (rest.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) rest = rest.Substring(0, rest.Length - 4);
            return rest;
        }

        /// <summary>"v0.1.0", "0.1.0" or "0.1" as a Version, or null.</summary>
        public static Version ParseVersion(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            text = text.Trim().TrimStart('v', 'V');
            var dash = text.IndexOfAny(new[] { '-', '+' });
            if (dash >= 0) text = text.Substring(0, dash);
            return Version.TryParse(text, out var version) ? version : null;
        }
    }
}
