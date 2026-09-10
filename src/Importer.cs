using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ArkBoard
{
    public sealed class ImportSource
    {
        public string Location;
        public string Name;
        public byte[] Bytes;
    }
    public static class Importer
    {
        static readonly HttpClient client = CreateClient();
        static HttpClient CreateClient()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var c = new HttpClient(); c.Timeout = TimeSpan.FromSeconds(30);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("ArkBoard/1.3"); return c;
        }
        public static bool CanRead(IDataObject data)
        {
            try { return data.GetDataPresent(DataFormats.FileDrop) || data.GetDataPresent(DataFormats.Bitmap) ||
                data.GetDataPresent(DataFormats.Html) || data.GetDataPresent(DataFormats.UnicodeText) || data.GetDataPresent(DataFormats.Text); }
            catch { return false; }
        }
        public static List<ImportSource> Extract(IDataObject data)
        {
            if (data == null) return new List<ImportSource>();
            if (data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] paths = data.GetData(DataFormats.FileDrop) as string[];
                if (paths != null && paths.Length > 0)
                    return paths.Select(p => new ImportSource { Location = p, Name = Path.GetFileName(p) }).ToList();
            }
            // Browser drags often offer HTML as well as the enclosing page's URL.
            // Resolve the image src first, never treat the page itself as an image.
            if (data.GetDataPresent(DataFormats.Html))
            {
                string html = data.GetData(DataFormats.Html) as string;
                var urls = ExtractHtmlImages(html);
                if (urls.Count > 0) return urls.Select(FromUrl).ToList();
            }
            if (data.GetDataPresent(DataFormats.Bitmap))
            {
                BitmapSource bitmap = data.GetData(DataFormats.Bitmap) as BitmapSource;
                if (bitmap != null) return new List<ImportSource> {
                    new ImportSource { Name = "Pasted image.png", Bytes = AssetData.FromBitmap(bitmap).Bytes } };
            }
            foreach (string format in new[] { DataFormats.UnicodeText, DataFormats.Text })
            {
                string text = data.GetDataPresent(format) ? data.GetData(format) as string : null;
                if (string.IsNullOrWhiteSpace(text)) continue;
                var list = new List<ImportSource>();
                foreach (string line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Take(100))
                {
                    string value = line.Trim(); Uri uri;
                    if (value.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)) list.Add(FromUrl(value));
                    else if (Uri.TryCreate(value, UriKind.Absolute, out uri) && (uri.Scheme == "http" || uri.Scheme == "https")) list.Add(FromUrl(value));
                    else if (File.Exists(value)) list.Add(new ImportSource { Location = value, Name = Path.GetFileName(value) });
                }
                if (list.Count > 0) return list;
            }
            return new List<ImportSource>();
        }
        public static List<string> ExtractHtmlImages(string html)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(html) || html.Length > 8 * 1024 * 1024) return result;
            Match source = Regex.Match(html, @"SourceURL:([^\r\n]+)", RegexOptions.IgnoreCase);
            Uri baseUri; Uri.TryCreate(source.Success ? source.Groups[1].Value.Trim() : "", UriKind.Absolute, out baseUri);
            foreach (Match tag in Regex.Matches(html, @"<img\b[^>]*>", RegexOptions.IgnoreCase))
            {
                Match src = Regex.Match(tag.Value, @"(?:\s)src\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s>]+))", RegexOptions.IgnoreCase);
                if (!src.Success) continue;
                string value = WebUtility.HtmlDecode(src.Groups[1].Success ? src.Groups[1].Value : src.Groups[2].Success ? src.Groups[2].Value : src.Groups[3].Value);
                if (value.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)) result.Add(value);
                else
                {
                    Uri url;
                    if (Uri.TryCreate(value, UriKind.Absolute, out url) || (baseUri != null && Uri.TryCreate(baseUri, value, out url)))
                        if (url.Scheme == "http" || url.Scheme == "https") result.Add(url.AbsoluteUri);
                }
                if (result.Count >= 100) break;
            }
            return result.Distinct().ToList();
        }
        static ImportSource FromUrl(string url)
        {
            string name = "Web image"; Uri uri;
            if (Uri.TryCreate(url, UriKind.Absolute, out uri) && (uri.Scheme == "http" || uri.Scheme == "https"))
            { name = Uri.UnescapeDataString(Path.GetFileName(uri.AbsolutePath)); if (string.IsNullOrWhiteSpace(name)) name = uri.Host; }
            return new ImportSource { Location = url, Name = name };
        }
        public static async Task<AssetData> LoadAsync(ImportSource source)
        {
            byte[] bytes = source.Bytes;
            if (bytes == null)
            {
                string location = source.Location;
                if (location.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
                {
                    int comma = location.IndexOf(',');
                    if (comma < 0 || comma > 256 || !location.Substring(0, comma).EndsWith(";base64", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Only base64 image data URLs are supported.");
                    if (location.Length > 140 * 1024 * 1024) throw new InvalidDataException("Image is too large.");
                    bytes = Convert.FromBase64String(location.Substring(comma + 1));
                }
                else if (File.Exists(location))
                {
                    bytes = await Task.Run(() => { using (Stream s = File.OpenRead(location)) return BoardDocument.ReadLimited(s, AssetData.MaxBytes); });
                }
                else
                {
                    Uri url;
                    if (!Uri.TryCreate(location, UriKind.Absolute, out url) || (url.Scheme != "http" && url.Scheme != "https"))
                        throw new InvalidDataException("File does not exist or URL is unsupported.");
                    using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();
                        if (response.Content.Headers.ContentLength > 50 * 1024 * 1024) throw new InvalidDataException("Download exceeds 50 MB.");
                        string type = response.Content.Headers.ContentType == null ? "" : response.Content.Headers.ContentType.MediaType;
                        if (type == "text/html") throw new InvalidDataException("This link points to a web page. Drag the image itself or use Copy Image and Ctrl+V.");
                        using (var cancel = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(30)))
                        using (Stream input = await response.Content.ReadAsStreamAsync())
                        using (MemoryStream output = new MemoryStream())
                        {
                            byte[] buffer = new byte[81920]; int n;
                            while ((n = await input.ReadAsync(buffer, 0, buffer.Length, cancel.Token)) > 0)
                            {
                                if (output.Length + n > 50 * 1024 * 1024) throw new InvalidDataException("Download exceeds 50 MB.");
                                await output.WriteAsync(buffer, 0, n, cancel.Token);
                            }
                            bytes = output.ToArray();
                        }
                    }
                }
            }
            try { return await Task.Run(() => AssetData.Create(bytes)); }
            catch (NotSupportedException) { throw new InvalidDataException("Image format is not supported by the installed Windows codecs. Try PNG/JPEG or Copy Image in your browser."); }
        }
    }
}
