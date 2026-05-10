using Microsoft.AspNetCore.Builder;
using Toolbelt;
using Toolbelt.Diagnostics;

namespace BlazorWasmPreRendering.Build.Test;

public static class TestSites
{
    private static string GetProjectDir() => FileIO.FindContainerDirToAncestor("*.csproj");

    private static string GetTestSitesDir() => Path.Combine(GetProjectDir(), "_Fixtures", "TestSites");

    private static async ValueTask<IAsyncDisposable> StartTestSite(string dirName, string baseUrl)
    {
        var baseUrlObject = new Uri(baseUrl);
        var listenAddress = baseUrlObject.GetLeftPart(UriPartial.Scheme | UriPartial.Authority);

        var webRootPath = Path.Combine(GetTestSitesDir(), dirName);
        var webServer = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = ["--urls", listenAddress],
            ContentRootPath = webRootPath
        }).Build();
        webServer
            .UsePathBase(baseUrlObject.AbsolutePath)
            .UseDefaultFiles()
            .UseStaticFiles();
        await webServer.StartAsync();
        return webServer;
    }

    /// <summary>
    /// Start a test site that is a very standard
    /// </summary>
    public static async ValueTask<IAsyncDisposable> StartTestSite1(string baseUrl) => await StartTestSite("Site1", baseUrl);

    /// <summary>
    /// Start a test site that has &lt;link rel="alternate" href="..."&gt; in the head, which should be crawled by StaticlizeCrawler.
    /// </summary>
    public static async ValueTask<IAsyncDisposable> StartTestSite3(string baseUrl) => await StartTestSite("Site3", baseUrl);

    /// <summary>
    /// Start a test site that has a non-root base href, which should be handled by StaticlizeCrawler.
    /// </summary>
    public static async ValueTask<IAsyncDisposable> StartTestSite4(string baseUrl) => await StartTestSite("Site4-nonRootBaseHref", baseUrl);

    private class Disposer : IDisposable
    {
        private readonly Action _Action;
        public Disposer(Action action) { this._Action = action; }
        public void Dispose() => this._Action.Invoke();
    }

    public static async ValueTask<IDisposable> StartTestSite2(
        string baseUrl,
        bool serviceNotRegistered1 = false,
        bool serviceNotRegistered2 = false,
        bool jsInvokeOnServer = false)
    {
        var site2Dir = Path.Combine(GetTestSitesDir(), "Site2-ServerErrors");
        var workDir = WorkDirectory.CreateCopyFrom(site2Dir, arg => arg.Name != "obj" && arg.Name != "bin");
        var dotnetCLI = XProcess.Start(
            "dotnet", "run " +
            $"--urls {baseUrl} " +
            $"--ServiceNotRegistered1={serviceNotRegistered1} " +
            $"--ServiceNotRegistered2={serviceNotRegistered2} " +
            $"--JSInvokeOnServer={jsInvokeOnServer} ",
            workDir);
        var success = await dotnetCLI.WaitForOutputAsync(output => output.Contains(baseUrl), millsecondsTimeout: 15000);
        if (!success)
        {
            try { dotnetCLI.Dispose(); } catch { }
            try { workDir.Dispose(); } catch { }
            var output = dotnetCLI.Output;
            throw new TimeoutException($"\"dotnet run\" did not respond \"Now listening on: {baseUrl}\".\r\n" + output);
        }
        await Task.Delay(200);

        return new Disposer(() =>
        {
            dotnetCLI.Dispose();
            workDir.Dispose();
        });
    }
}
