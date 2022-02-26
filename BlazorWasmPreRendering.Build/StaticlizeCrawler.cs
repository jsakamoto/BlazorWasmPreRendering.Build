using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

namespace Toolbelt.Blazor.WebAssembly.PrerenderServer
{
    internal class StaticlizeCrawler
    {
        private HtmlParser HtmlParser { get; } = new();

        private HashSet<string> SavedPathSet { get; } = new();

        private string BaseUrl { get; }

        private HttpClient HttpClient { get; } = new();

        private string WebRootPath { get; }

        private OutputStyle OutputStyle { get; }

        private bool EnableGZipCompression { get; }

        private bool EnableBrotliCompression { get; }

        private IEnumerable<string> UrlPathToExplicitFetch { get; }

        public StaticlizeCrawler(
            string baseUrl,
            string? urlPathToExplicitFetch,
            string webRootPath,
            OutputStyle outputStyle,
            bool enableGZipCompression,
            bool enableBrotliCompression)
        {
            this.BaseUrl = baseUrl.TrimEnd('/');
            this.WebRootPath = webRootPath;
            this.OutputStyle = outputStyle;
            this.EnableGZipCompression = enableGZipCompression;
            this.EnableBrotliCompression = enableBrotliCompression;

            this.UrlPathToExplicitFetch = (urlPathToExplicitFetch ?? "")
                .Split(';')
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToArray();
        }

        public async Task SaveToStaticFileAsync()
        {
            await this.SaveToStaticFileAsync("/");

            foreach (var urlPathToExplicitFetch in this.UrlPathToExplicitFetch)
            {
                await this.SaveToStaticFileAsync(urlPathToExplicitFetch);
            }
        }

        private async Task SaveToStaticFileAsync(string path)
        {
            if (this.SavedPathSet.Contains(path)) return;
            this.SavedPathSet.Add(path);

            var requestUrl = this.BaseUrl + path;
            Console.WriteLine($"Getting {requestUrl}...");

            if (!Uri.TryCreate(requestUrl, UriKind.Absolute, out var _))
            {
                IndentedWriteLines($"[ERROR] The request URL ({requestUrl}) was not valid format.", indentSize: 2);
                return;
            }

            var response = await this.HttpClient.GetAsync(requestUrl);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                IndentedWriteLines($"[ERROR] The HTTP status code was not OK. (it was {response.StatusCode}.)", indentSize: 2);

                if (response.Content.Headers.ContentType?.MediaType?.StartsWith("text/") == true)
                {
                    try
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        IndentedWriteLines(content, indentSize: 4);
                    }
                    catch (Exception ex) { IndentedWriteLines(ex.ToString(), indentSize: 4); }
                }

                return;
            }
            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (mediaType != "text/html")
            {
                Console.WriteLine($"  The content type was not text/html. (it was {mediaType}.)");
                return;
            }

            var htmlContent = await response.Content.ReadAsStringAsync();
            var outputPath = this.GetOutputPath(path);

            File.WriteAllText(outputPath, htmlContent);
            this.RecompressStaticFile(outputPath);

            using var htmlDoc = this.HtmlParser.ParseDocument(htmlContent);
            var links = htmlDoc.Links
                .OfType<IHtmlAnchorElement>()
                .Where(link => string.IsNullOrEmpty(link.Origin))
                .Where(link => string.IsNullOrEmpty(link.Target))
                .Where(link => !string.IsNullOrEmpty(link.PathName))
                .Select(link => link.PathName)
                .ToArray();

            foreach (var link in links)
            {
                await this.SaveToStaticFileAsync(link);
            }
        }

        private static void IndentedWriteLines(string content, int indentSize)
        {
            var indentSpaces = new string(' ', indentSize);
            foreach (var contentLine in content.Split('\n').Select(s => s.TrimEnd('\r')))
            {
                Console.WriteLine(indentSpaces + contentLine);
            }
        }

        private string GetOutputPath(string path)
        {
            var indexHtmlPath = default(string);
            if (this.OutputStyle == OutputStyle.IndexHtmlInSubFolders)
            {
                indexHtmlPath = Path.Combine(this.WebRootPath, Path.Combine(path.Split('/')), "index.html");
            }
            else if (this.OutputStyle == OutputStyle.AppendHtmlExtension)
            {
                indexHtmlPath = path is "" or "/" ?
                    Path.Combine(this.WebRootPath, "index.html") :
                    Path.Combine(this.WebRootPath, Path.Combine(path.Split('/'))) + ".html";
            }
            if (indexHtmlPath == null) throw new NullReferenceException();

            var targetDir = Path.GetDirectoryName(indexHtmlPath);
            if (string.IsNullOrEmpty(targetDir)) throw new NullReferenceException();

            if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

            return indexHtmlPath;
        }

        private void RecompressStaticFile(string indexHtmlPath)
        {
            if (!this.EnableGZipCompression && !this.EnableBrotliCompression) return;

            using var sourceStream = File.OpenRead(indexHtmlPath);

            if (this.EnableGZipCompression)
            {
                using var outputStream = File.Create(indexHtmlPath + ".gz");
                using var compressingStream = new GZipStream(outputStream, CompressionLevel.Optimal);
                sourceStream.Seek(0, SeekOrigin.Begin);
                sourceStream.CopyTo(compressingStream);
            }

            if (this.EnableBrotliCompression)
            {
                using var outputStream = File.Create(indexHtmlPath + ".br");
                using var compressingStream = new BrotliStream(outputStream, CompressionLevel.Optimal);
                sourceStream.Seek(0, SeekOrigin.Begin);
                sourceStream.CopyTo(compressingStream);
            }
        }
    }
}
