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

        private bool EnableGZipCompression { get; }

        private bool EnableBrotliCompression { get; }

        public StaticlizeCrawler(
            string baseUrl,
            string webRootPath,
            bool enableGZipCompression,
            bool enableBrotliCompression)
        {
            this.BaseUrl = baseUrl.TrimEnd('/');
            this.WebRootPath = webRootPath;
            this.EnableGZipCompression = enableGZipCompression;
            this.EnableBrotliCompression = enableBrotliCompression;
        }

        public Task SaveToStaticFileAsync() => SaveToStaticFileAsync("/");

        private async Task SaveToStaticFileAsync(string path)
        {
            if (this.SavedPathSet.Contains(path)) return;
            this.SavedPathSet.Add(path);

            var requestUrl = this.BaseUrl + path;
            Console.WriteLine($"Getting {requestUrl}...");
            var response = await this.HttpClient.GetAsync(requestUrl);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                Console.WriteLine($"  The HTTP status code was not OK. (it was {response.StatusCode}.)");
                return;
            }
            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (mediaType != "text/html")
            {
                Console.WriteLine($"  The content type was not text/html. (it was {mediaType}.)");
                return;
            }

            var htmlContent = await response.Content.ReadAsStringAsync();
            var targetDir = Path.Combine(this.WebRootPath, Path.Combine(path.Split('/')));
            Directory.CreateDirectory(targetDir);
            var indexHtmlPath = Path.Combine(targetDir, "index.html");
            File.WriteAllText(indexHtmlPath, htmlContent);

            RecompressStaticFile(indexHtmlPath);

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
                await SaveToStaticFileAsync(link);
            }
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
