using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using static Toolbelt.Blazor.WebAssembly.PreRendering.Build.StaticlizeCrawlingResult;

namespace Toolbelt.Blazor.WebAssembly.PreRendering.Build;

internal class StaticlizeCrawler
{
    private StaticlizeCrawlingResult CrawlingResult { get; set; } = Nothing;

    private HtmlParser HtmlParser { get; } = new();

    private HashSet<string> SavedPathSet { get; } = new();

    private string BaseUrl { get; }

    private HttpClient HttpClient { get; } = new();

    private string WebRootPath { get; }

    private OutputStyle OutputStyle { get; }

    private bool EnableGZipCompression { get; }

    private bool EnableBrotliCompression { get; }

    private ILogger Logger { get; }

    private IEnumerable<string> UrlPathToExplicitFetch { get; }

    private readonly List<string> _StaticalizedFiles = new List<string>();

    public IEnumerable<string> StaticalizedFiles => this._StaticalizedFiles;

    public StaticlizeCrawler(
        string baseUrl,
        string? urlPathToExplicitFetch,
        string webRootPath,
        IEnumerable<string> locales,
        OutputStyle outputStyle,
        bool enableGZipCompression,
        bool enableBrotliCompression,
        ILogger logger)
    {
        this.BaseUrl = baseUrl.TrimEnd('/');
        this.WebRootPath = webRootPath;
        this.OutputStyle = outputStyle;
        this.EnableGZipCompression = enableGZipCompression;
        this.EnableBrotliCompression = enableBrotliCompression;
        this.Logger = logger;

        this.UrlPathToExplicitFetch = (urlPathToExplicitFetch ?? "")
            .Split(';')
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToArray();

        if (locales.Any())
        {
            this.HttpClient.DefaultRequestHeaders.AcceptLanguage.Clear();
            foreach (var locale in locales)
            {
                this.HttpClient.DefaultRequestHeaders.AcceptLanguage.Add(StringWithQualityHeaderValue.Parse(locale));
            }
        }
    }

    public async Task<StaticlizeCrawlingResult> SaveToStaticFileAsync()
    {
        await this.SaveToStaticFileAsync("/");

        foreach (var urlPathToExplicitFetch in this.UrlPathToExplicitFetch)
        {
            await this.SaveToStaticFileAsync(urlPathToExplicitFetch);
        }

        return this.CrawlingResult;
    }

    private Task SaveToStaticFileAsync(string path)
    {
        return this.SaveToStaticFileAsync((Href: $"about://{path}", Protocol: "about:", PathName: path));
    }

    private async Task SaveToStaticFileAsync((string Href, string Protocol, string PathName) args)
    {
        var href = args.Href.Split('#').FirstOrDefault() ?? "";
        if (this.SavedPathSet.Contains(href)) return;
        this.SavedPathSet.Add(href);

        // DEBUG: Console.WriteLine($"Protocol:[{args.Protocol}], PathName:[{args.PathName}], Href:[{args.Href}]");

        if (args.Protocol != "about:")
        {
            this.IndentedWriteLines($"[INFORMATION] The requested URL ({args.Href}) was not navigatable.", indentSize: 0);
            return;
        }

        var requestUrl = this.BaseUrl + args.PathName;
        this.Logger.LogInformation($"Getting {requestUrl}...");

        if (!Uri.TryCreate(requestUrl, UriKind.Absolute, out var _))
        {
            this.CrawlingResult |= HasWarnings;
            this.IndentedWriteLines($"[WARNING] The requested URL ({requestUrl}) was not valid format.", indentSize: 2);
            return;
        }

        var response = await this.HttpClient.GetAsync(requestUrl);

        if (response.StatusCode != HttpStatusCode.OK)
        {
            var (resultFlag, label) = ((int)response.StatusCode >= 500) ? (HasErrors, "ERROR") : (HasWarnings, "WARNING");
            this.CrawlingResult |= resultFlag;
            this.IndentedWriteLines($"[{label}] The HTTP status code was not OK. (it was ({(int)response.StatusCode}){response.StatusCode}.)", indentSize: 2);

            if (response.Content.Headers.ContentType?.MediaType?.StartsWith("text/") == true)
            {
                try
                {
                    var content = await response.Content.ReadAsStringAsync();
                    this.IndentedWriteLines(content, indentSize: 4);

                    if (content.Contains("There is no registered service of type") ||// failed for property injection
                        content.Contains("Unable to resolve service for type") || // failed for middle ware injection
                        Regex.IsMatch(content, "No service for type .+ has been registered")) // failed for services.GetRequiredService<T>()
                    {
                        this.CrawlingResult |= HasErrorsOfServiceNotRegistered;
                    }
                    if (content.Contains("JavaScript interop calls can only be performed during the OnAfterRenderAsync lifecycle method"))
                    {
                        this.CrawlingResult |= HasErrorsOfJSInvokeOnServer;
                    }
                }
                catch (Exception ex) { this.IndentedWriteLines(ex.ToString(), indentSize: 4); }
            }

            return;
        }
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType != "text/html")
        {
            this.Logger.LogInformation($"  The content type was not text/html. (it was {mediaType}.)");
            return;
        }

        var htmlContent = await response.Content.ReadAsStringAsync();
        var outputPath = this.GetOutputPath(args.PathName);

        File.WriteAllText(outputPath, htmlContent);
        this._StaticalizedFiles.Add(outputPath);
        this.RecompressStaticFile(outputPath);

        using var htmlDoc = this.HtmlParser.ParseDocument(htmlContent);
        var links = htmlDoc.Links
            .OfType<IHtmlAnchorElement>()
            .Where(link => string.IsNullOrEmpty(link.Origin))
            .Where(link => string.IsNullOrEmpty(link.Target))
            .Where(link => !string.IsNullOrEmpty(link.PathName))
            .ToArray();

        foreach (var link in links)
        {
            await this.SaveToStaticFileAsync((link.Href, link.Protocol, link.PathName));
        }
    }

    private void IndentedWriteLines(string content, int indentSize)
    {
        var indentSpaces = new string(' ', indentSize);
        foreach (var contentLine in content.Split('\n').Select(s => s.TrimEnd('\r')))
        {
            this.Logger.LogInformation(indentSpaces + contentLine);
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
