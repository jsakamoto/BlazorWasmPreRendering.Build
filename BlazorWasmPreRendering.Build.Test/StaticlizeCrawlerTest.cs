using NUnit.Framework;
using Toolbelt;
using Toolbelt.Blazor.WebAssembly.PreRendering.Build;

namespace BlazorWasmPreRendering.Build.Test;

[Parallelizable(ParallelScope.Children)]
public class StaticlizeCrawlerTest
{
    [Test]
    public async Task SaveToStaticFileAsync_IndexHtmlInSubFolder_Style_Test()
    {
        // Given
        const string baseUrl = "http://127.0.0.1:5051";
        await using var testSiteServer = await TestSites.StartTestSite1(baseUrl);
        using var outDir = new WorkDirectory();
        var logger = new TestLogger();

        // When
        var crawler = new StaticlizeCrawler(
            baseUrl,
            urlPathToExplicitFetch: null,
            webRootPath: outDir,
            locales: new string[] { },
            OutputStyle.IndexHtmlInSubFolders,
            enableBrotliCompression: false,
            enableGZipCompression: false,
            logger: logger);
        var result = await crawler.SaveToStaticFileAsync();

        // Then
        result.Is(StaticlizeCrawlingResult.Nothing);
        File.ReadAllText(Path.Combine(outDir, "index.html")).Contains("<h1>Home</h1>").IsTrue();
        File.ReadAllText(Path.Combine(outDir, "counter", "index.html")).Contains("<h1>Counter</h1>").IsTrue();
        File.ReadAllText(Path.Combine(outDir, "fetchdata", "weather-forecast", "index.html")).Contains("<h1>Fetch Data</h1>").IsTrue();

        logger.LogLines.OrderBy(line => line)
            .Is($"Getting {baseUrl}/...",
                $"Getting {baseUrl}/counter...",
                $"Getting {baseUrl}/fetchdata/weather-forecast...");
    }

    [Test]
    public async Task SaveToStaticFileAsync_AppendHtmlExtension_Style_Test()
    {
        // Given
        const string baseUrl = "http://127.0.0.1:5052";
        await using var testSiteServer = await TestSites.StartTestSite1(baseUrl);
        using var outDir = new WorkDirectory();

        // When
        var logger = new TestLogger();
        var crawler = new StaticlizeCrawler(
            baseUrl,
            urlPathToExplicitFetch: null,
            webRootPath: outDir,
            locales: new[] { "en" },
            OutputStyle.AppendHtmlExtension,
            enableBrotliCompression: false,
            enableGZipCompression: false,
            logger: logger);
        var result = await crawler.SaveToStaticFileAsync();

        // Then
        result.Is(StaticlizeCrawlingResult.Nothing);
        File.ReadAllText(Path.Combine(outDir, "index.html")).Contains("<h1>Home</h1>").IsTrue();
        File.ReadAllText(Path.Combine(outDir, "counter.html")).Contains("<h1>Counter</h1>").IsTrue();
        File.ReadAllText(Path.Combine(outDir, "fetchdata", "weather-forecast.html")).Contains("<h1>Fetch Data</h1>").IsTrue();

        logger.LogLines.OrderBy(line => line)
            .Is($"Getting {baseUrl}/...",
                $"Getting {baseUrl}/counter...",
                $"Getting {baseUrl}/fetchdata/weather-forecast...");
    }

    [TestCase(5055, true, false)]
    [TestCase(5056, false, true)]
    public async Task SaveToStaticFileAsync_ServiceNotRegisteredError_Test(int port, bool serviceNotRegistered1, bool serviceNotRegistered2)
    {
        // Given
        var baseUrl = $"http://127.0.0.1:{port}";
        using var site = await TestSites.StartTestSite2(baseUrl, serviceNotRegistered1, serviceNotRegistered2);
        using var outDir = new WorkDirectory();

        // When
        var logger = new TestLogger();
        var crawler = new StaticlizeCrawler(
            baseUrl,
            urlPathToExplicitFetch: null,
            webRootPath: outDir,
            locales: new string[] { },
            OutputStyle.AppendHtmlExtension,
            enableBrotliCompression: false,
            enableGZipCompression: false,
            logger: logger);
        var result = await crawler.SaveToStaticFileAsync();

        // Then
        result.HasFlag(StaticlizeCrawlingResult.HasErrors).IsTrue();
        result.HasFlag(StaticlizeCrawlingResult.HasErrorsOfServiceNotRegistered).IsTrue();
    }

    [Test]
    public async Task SaveToStaticFileAsync_JsInvokeOnServerError_Test()
    {
        // Given
        const string baseUrl = "http://127.0.0.1:5054";
        using var site = await TestSites.StartTestSite2(baseUrl, jsInvokeOnServer: true);
        using var outDir = new WorkDirectory();

        // When
        var logger = new TestLogger();
        var crawler = new StaticlizeCrawler(
            baseUrl,
            urlPathToExplicitFetch: null,
            webRootPath: outDir,
            locales: new string[] { },
            OutputStyle.AppendHtmlExtension,
            enableBrotliCompression: false,
            enableGZipCompression: false,
            logger: logger);
        var result = await crawler.SaveToStaticFileAsync();

        // Then
        result.HasFlag(StaticlizeCrawlingResult.HasErrors).IsTrue();
        result.HasFlag(StaticlizeCrawlingResult.HasErrorsOfJSInvokeOnServer).IsTrue();
    }
}
