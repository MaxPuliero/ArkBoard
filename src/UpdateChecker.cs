using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;

namespace ArkBoard
{
    [DataContract]
    internal sealed class GitHubRelease
    {
        [DataMember(Name = "tag_name")] internal string Tag { get; set; }
        [DataMember(Name = "assets")] internal List<GitHubAsset> Assets { get; set; }
    }

    [DataContract]
    internal sealed class GitHubAsset
    {
        [DataMember(Name = "name")] internal string Name { get; set; }
        [DataMember(Name = "browser_download_url")] internal string DownloadUrl { get; set; }
    }

    internal sealed class UpdateInfo
    {
        internal Version Version;
        internal string Tag;
        internal string FileName;
        internal string DownloadUrl;
    }

    internal static class UpdateChecker
    {
        internal const string ReleasesUrl = "https://github.com/MaxPuliero/ArkBoard/releases/latest";
        const string ApiUrl = "https://api.github.com/repos/MaxPuliero/ArkBoard/releases/latest";

        internal static Version ParseVersionTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return null;
            string value = tag.Trim();
            if (value.StartsWith("v", StringComparison.OrdinalIgnoreCase)) value = value.Substring(1);
            int suffix = value.IndexOf('-'); if (suffix >= 0) value = value.Substring(0, suffix);
            Version version; return Version.TryParse(value, out version) ? version : null;
        }

        internal static async Task<UpdateInfo> CheckAsync(Version current)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(6) })
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("ArkBoard/" + current);
                byte[] json = await client.GetByteArrayAsync(ApiUrl);
                return ParseRelease(json, current);
            }
        }

        internal static UpdateInfo ParseRelease(byte[] json, Version current)
        {
            GitHubRelease release;
            using (var stream = new MemoryStream(json))
                release = (GitHubRelease)new DataContractJsonSerializer(typeof(GitHubRelease)).ReadObject(stream);
            Version latest = ParseVersionTag(release == null ? null : release.Tag);
            if (latest == null || latest <= current) return null;
            GitHubAsset asset = null;
            if (release.Assets != null)
            {
                asset = release.Assets.Find(a => string.Equals(a.Name, "ArkBoard.exe", StringComparison.OrdinalIgnoreCase));
                if (asset == null) asset = release.Assets.Find(a => a.Name != null && a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
                if (asset == null) asset = release.Assets.Find(a => a.Name != null && a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
            }
            return new UpdateInfo { Version = latest, Tag = release.Tag,
                FileName = asset == null ? null : asset.Name, DownloadUrl = asset == null ? null : asset.DownloadUrl };
        }

        internal static bool IsTrustedDownloadUrl(string value)
        {
            Uri uri;
            return Uri.TryCreate(value, UriKind.Absolute, out uri) && uri.Scheme == Uri.UriSchemeHttps &&
                string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase) &&
                uri.AbsolutePath.StartsWith("/MaxPuliero/ArkBoard/releases/download/", StringComparison.OrdinalIgnoreCase);
        }

        internal static async Task DownloadAsync(UpdateInfo update, string targetPath, IProgress<double> progress)
        {
            if (update == null || !IsTrustedDownloadUrl(update.DownloadUrl)) throw new InvalidDataException("The release does not contain a trusted download.");
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            string temporary = targetPath + ".download";
            try
            {
                if (File.Exists(temporary)) File.Delete(temporary);
                using (var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) })
                {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("ArkBoard/" + update.Version);
                    using (HttpResponseMessage response = await client.GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();
                        long total = response.Content.Headers.ContentLength ?? -1;
                        using (Stream input = await response.Content.ReadAsStreamAsync())
                        using (var output = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                        {
                            var buffer = new byte[81920]; long readTotal = 0; int read;
                            while ((read = await input.ReadAsync(buffer, 0, buffer.Length)) > 0)
                            {
                                await output.WriteAsync(buffer, 0, read); readTotal += read;
                                if (total > 0 && progress != null) progress.Report((double)readTotal / total);
                            }
                        }
                    }
                }
                if (File.Exists(targetPath)) File.Delete(targetPath);
                File.Move(temporary, targetPath);
                if (progress != null) progress.Report(1);
            }
            catch
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); } catch { }
                throw;
            }
        }
    }
}
