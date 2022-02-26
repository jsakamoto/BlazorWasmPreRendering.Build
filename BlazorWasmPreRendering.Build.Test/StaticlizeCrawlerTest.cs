using Microsoft.AspNetCore.Builder;
using NUnit.Framework;
using Toolbelt;
using Toolbelt.Blazor.WebAssembly.PrerenderServer;

namespace BlazorWasmPreRendering.Build.Test;

public class StaticlizeCrawlerTest
{
    private static async ValueTask<IAsyncDisposable> StartTestSite1(string baseUrl)
    {
        var solutionDir = FileIO.FindContainerDirToAncestor("*.sln");
        var webRootPath = Path.Combine(solutionDir, "BlazorWasmPreRendering.Build.Test", "TestSites", "Site1");
        var webServer = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = new[] { "--urls", baseUrl },
            ContentRootPath = webRootPath
        }).Build();
        webServer.UseDefaultFiles().UseStaticFiles();
        await webServer.StartAsync();
        return webServer;
    }

    [Test]
    public async Task SaveToStaticFileAsync_IndexHtmlInSubFolder_Style_Test()
    {
        const string baseUrl = "http://127.0.0.1:5051";
        await using var testSiteServer = await StartTestSite1(baseUrl);
        using var outDir = new WorkDirectory();

        var crawler = new StaticlizeCrawler(
            baseUrl,
            webRootPath: outDir,
            OutputStyle.IndexHtmlInSubFolders,
            enableBrotliCompression: false,
            enableGZipCompression: false);
        await crawler.SaveToStaticFileAsync();

        File.ReadAllText(Path.Combine(outDir, "index.html")).Contains("<h1>Home</h1>").IsTrue();
        File.ReadAllText(Path.Combine(outDir, "counter", "index.html")).Contains("<h1>Counter</h1>").IsTrue();
        File.ReadAllText(Path.Combine(outDir, "fetchdata", "weather-forecast", "index.html")).Contains("<h1>Fetch Data</h1>").IsTrue();
    }

    [Test]
    public async Task SaveToStaticFileAsync_AppendHtmlExtension_Style_Test()
    {
        const string baseUrl = "http://127.0.0.1:5052";
        await using var testSiteServer = await StartTestSite1(baseUrl);
        using var outDir = new WorkDirectory();

        var crawler = new StaticlizeCrawler(
            baseUrl,
            webRootPath: outDir,
            OutputStyle.AppendHtmlExtension,
            enableBrotliCompression: false,
            enableGZipCompression: false);
        await crawler.SaveToStaticFileAsync();

        File.ReadAllText(Path.Combine(outDir, "index.html")).Contains("<h1>Home</h1>").IsTrue();
        File.ReadAllText(Path.Combine(outDir, "counter.html")).Contains("<h1>Counter</h1>").IsTrue();
        File.ReadAllText(Path.Combine(outDir, "fetchdata", "weather-forecast.html")).Contains("<h1>Fetch Data</h1>").IsTrue();
    }
}
