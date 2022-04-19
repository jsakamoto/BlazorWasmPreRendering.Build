using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using BlazorWasmPreRendering.Build.Test.Constants;
using NUnit.Framework;
using Toolbelt;
using Toolbelt.Blazor.WebAssembly.PrerenderServer;
using Toolbelt.Blazor.WebAssembly.PrerenderServer.Models;
using Toolbelt.Diagnostics;
using static Toolbelt.Diagnostics.XProcess;

namespace BlazorWasmPreRendering.Build.Test;

public class ProgramE2ETest
{
    [Test]
    public async Task dotNET6_HeadOutlet_TestAsync()
    {
        // Given
        // Publish the sample app which sets its titles by .NET 6 <PageTitle>.
        using var publishDir = await SampleSite.BlazorWasmApp0.PublishAsync();

        // When
        // Execute prerenderer
        var exitCode = await Program.Main(new[] {
            "-a", "BlazorWasmApp0",
            "-t", "BlazorWasmApp0.App",
            "--selectorofrootcomponent", "#app,app",
            "--selectorofheadoutletcomponent", "head::after",
            "-p", publishDir,
            "-i", SampleSite.BlazorWasmApp0.IntermediateDir,
            "-m", "",
            "-f", "net6.0"
        });
        exitCode.Is(0);

        // Then
        // Validate prerendered contents.
        var wwwrootDir = Path.Combine(publishDir, "wwwroot");
        ValidatePrerenderedContents_of_BlazorWasmApp0(wwwrootDir, outputStyle: OutputStyle.IndexHtmlInSubFolders);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Including_ServerSide_Middleware_Legacy_TestAsync(bool deleteLoadingContents)
    {
        // Given
        // Publish the sample app which sets its titles by Toolbelt.Blazor.HeadElement
        using var publishDir = await SampleSite.BlazorWasmApp1.PublishAsync();

        // When
        // Execute prerenderer
        var exitCode = await Program.Main(new[] {
            "-a", "BlazorWasmApp1",
            "-t", "BlazorWasmApp1.App",
            "--selectorofrootcomponent", "#app,app",
            "--selectorofheadoutletcomponent", "head::after",
            "-p", publishDir,
            "-i", SampleSite.BlazorWasmApp1.IntermediateDir,
            "-m", "Toolbelt.Blazor.HeadElement.ServerPrerendering,,1.5.2",
            "-f", "net5.0",
            deleteLoadingContents ? "-d" : ""
        });
        exitCode.Is(0);

        // Then

        // Validate prerendered contents.

        var wwwrootDir = Path.Combine(publishDir, "wwwroot");
        var expectedHtmlFiles = GetFullPathList(wwwrootDir, "counter/index.html", "fetchdata/index.html", "index.html");

        var actualHtmlFiles = Directory.GetFiles(wwwrootDir, "*.html", SearchOption.AllDirectories).OrderBy(path => path).ToArray();
        actualHtmlFiles.Is(expectedHtmlFiles);

        // NOTICE: The document title was rendered by the Toolbelt.Blazor.HeadElement
        const string loadingContents = "Loading...";
        Validate(actualHtmlFiles[2], loadingContents, title_is: "Home", h1_is: "Hello, world!", deleteLoadingContents);
        Validate(actualHtmlFiles[0], loadingContents, title_is: "Counter", h1_is: "Counter", deleteLoadingContents);
        Validate(actualHtmlFiles[1], loadingContents, title_is: "Weather forecast", h1_is: "Weather forecast", deleteLoadingContents);
    }

    [Test]
    public async Task Including_ServerSide_Middleware_from_AssemblyMetaData_TestAsync()
    {
        using var sampleAppWorkDir = SampleSite.CreateSampleAppsWorkDir();
        var projectDir = Path.Combine(sampleAppWorkDir, "BlazorWasmApp0");

        using var dotnetCLI = Start(
            "dotnet", "publish -c:Release -p:BlazorWasmPrerenderingKeepServer=true -p:BlazorEnableCompression=false -p:UsingBrowserRuntimeWorkload=false",
            projectDir,
            options => options.WhenDisposing = XProcessTerminate.EntireProcessTree);
        var success = await dotnetCLI.WaitForOutputAsync(output => output.Trim().StartsWith("Start fetching..."), millsecondsTimeout: 15000);
        if (!success) { throw new Exception(dotnetCLI.Output); }

        using var httpClient = new HttpClient();
        var httpResponse = await httpClient.GetAsync("http://127.0.0.1:5050/");
        httpResponse.EnsureSuccessStatusCode();

        httpResponse.Headers.TryGetValues("X-Middleware1-Version", out var values1).IsTrue();
        values1.Is("1.0.0.0");

        httpResponse.Headers.TryGetValues("X-Middleware2-Version", out var values2).IsTrue();
        values2.Is("2.0.0.0");
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Including_EasterEggPage_TestAsync(bool deleteLoadingContents)
    {
        // Given
        using var publishDir = await SampleSite.BlazorWasmApp1.PublishAsync();

        // When
        // Execute prerenderer
        var exitCode = await Program.Main(new[] {
            "-a", "BlazorWasmApp1",
            "-t", "BlazorWasmApp1.App",
            "--selectorofrootcomponent", "#app,app",
            "--selectorofheadoutletcomponent", "head::after",
            "-p", publishDir,
            "-i", SampleSite.BlazorWasmApp1.IntermediateDir,
            "-m", "Toolbelt.Blazor.HeadElement.ServerPrerendering,,1.5.2",
            "-f", "net5.0",
            "-o", "AppendHtmlExtension",
            "-u", "/easter-egg",
            deleteLoadingContents? "-d" : ""
        });
        exitCode.Is(0);

        // Then

        // Validate prerendered contents.

        var wwwrootDir = Path.Combine(publishDir, "wwwroot");
        var expectedHtmlFiles = GetFullPathList(wwwrootDir, "counter.html", "easter-egg.html", "fetchdata.html", "index.html");

        var actualHtmlFiles = Directory.GetFiles(wwwrootDir, "*.html", SearchOption.AllDirectories).OrderBy(path => path).ToArray();
        actualHtmlFiles.Is(expectedHtmlFiles);

        // NOTICE: The document title was rendered by the Toolbelt.Blazor.HeadElement
        const string loadingContents = "Loading...";
        Validate(actualHtmlFiles[3], loadingContents, title_is: "Home", h1_is: "Hello, world!", deleteLoadingContents);
        Validate(actualHtmlFiles[0], loadingContents, title_is: "Counter", h1_is: "Counter", deleteLoadingContents);
        Validate(actualHtmlFiles[2], loadingContents, title_is: "Weather forecast", h1_is: "Weather forecast", deleteLoadingContents);
        Validate(actualHtmlFiles[1], loadingContents, title_is: "Easter Egg", h1_is: "Hello, Easter Egg!", deleteLoadingContents);
    }

    private static string[] GetFullPathList(string baseDir, params string[] pathList)
    {
        return pathList
            .Select(path => Path.Combine(path.Split('/')))
            .Select(path => Path.Combine(baseDir, path))
            .ToArray();
    }

    private static void Validate(string htmlPath, string loadingContents, string title_is, string h1_is, bool loadingContentsAreDeleted = false)
    {
        var htmlParser = new HtmlParser();
        using var html = htmlParser.ParseDocument(File.ReadAllText(htmlPath));

        html.Title.Is(title_is);

        html.QuerySelector("h1")!.TextContent.Trim().Is(h1_is);

        var appElement = html.QuerySelector("#app");
        if (appElement == null) throw new Exception("the element #app was not found.");

        if (loadingContentsAreDeleted)
        {
            appElement.InnerHtml.TrimStart().StartsWith(
                "<!-- %%-PRERENDERING-BEGIN-%% -->\n"
            ).IsTrue();
            appElement.InnerHtml.TrimStart().StartsWith(
                "<!-- %%-PRERENDERING-BEGIN-%% -->\n" +
                "<div style=\"opacity: 0; position: fixed; z-index: -1; top: 0; left: 0; bottom: 0; right: 0;\">"
            ).IsFalse();
        }
        else
        {
            appElement.InnerHtml.TrimStart().StartsWith(
                loadingContents + "\n" +
                "<!-- %%-PRERENDERING-BEGIN-%% -->\n" +
                "<div style=\"opacity: 0; position: fixed; z-index: -1; top: 0; left: 0; bottom: 0; right: 0;\">"
            ).IsTrue();
        }
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task AppComponent_is_in_the_other_Assembly_TestAsync(bool deleteLoadingContents)
    {
        // Given
        // Publish the sample app
        using var publishDir = await SampleSite.BlazorWasmApp2.PublishAsync();

        // When
        // Execute prerenderer
        var exitCode = await Program.Main(new[] {
            "-a", "BlazorWasmApp2.Client",
            "-t", "BlazorWasmApp2.Components.App, BlazorWasmApp2.Components", // INCLUDES ASSEMBLY NAME
            "--selectorofrootcomponent", "#app,app",
            "--selectorofheadoutletcomponent", "head::after",
            "-p", publishDir,
            "-i", SampleSite.BlazorWasmApp2.IntermediateDir,
            "-m", "",
            "-f", "net5.0",
            deleteLoadingContents? "-d" : ""
        });
        exitCode.Is(0);

        // Then

        // Validate prerendered contents.

        var wwwrootDir = Path.Combine(publishDir, "wwwroot");
        var expectedHtmlFiles = GetFullPathList(wwwrootDir, "about-this-site/index.html", "index.html");

        var actualHtmlFiles = Directory.GetFiles(wwwrootDir, "*.html", SearchOption.AllDirectories).OrderBy(path => path).ToArray();
        actualHtmlFiles.Is(expectedHtmlFiles);

        Validate(actualHtmlFiles[0], BlazorWasmApp2.LoadingContents, title_is: "BlazorWasmApp2.Client", h1_is: "About Page", deleteLoadingContents);
        Validate(actualHtmlFiles[1], BlazorWasmApp2.LoadingContents, title_is: "BlazorWasmApp2.Client", h1_is: "Welcome to Blazor Wasm App 2!", deleteLoadingContents);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task AppComponent_is_in_the_other_Assembly_and_FallBack_TestAsync(bool deleteLoadingContents)
    {
        // Given
        // Publish the sample app
        using var publishDir = await SampleSite.BlazorWasmApp2.PublishAsync();

        // When
        // Execute prerenderer
        var exitCode = await Program.Main(new[] {
            "-a", "BlazorWasmApp2.Client",
            "-t", "BlazorWasmApp2.Client.App", // INVALID TYPE NAME OF ROOT COMPONENT
            "--selectorofrootcomponent", "#app,app",
            "--selectorofheadoutletcomponent", "head::after",
            "-p", publishDir,
            "-i", SampleSite.BlazorWasmApp2.IntermediateDir,
            "-m", "",
            "-f", "net5.0",
            deleteLoadingContents ? "-d" : ""
        });
        exitCode.Is(0);

        // Then

        // Validate prerendered contents.

        var wwwrootDir = Path.Combine(publishDir, "wwwroot");
        var expectedHtmlFiles = GetFullPathList(wwwrootDir, "about-this-site/index.html", "index.html");

        var actualHtmlFiles = Directory.GetFiles(wwwrootDir, "*.html", SearchOption.AllDirectories).OrderBy(path => path).ToArray();
        actualHtmlFiles.Is(expectedHtmlFiles);

        Validate(actualHtmlFiles[0], BlazorWasmApp2.LoadingContents, title_is: "BlazorWasmApp2.Client", h1_is: "About Page", deleteLoadingContents);
        Validate(actualHtmlFiles[1], BlazorWasmApp2.LoadingContents, title_is: "BlazorWasmApp2.Client", h1_is: "Welcome to Blazor Wasm App 2!", deleteLoadingContents);
    }

    [Test]
    public async Task Publish_Test()
    {
        // Given
        var solutionDir = FileIO.FindContainerDirToAncestor("*.sln");
        var srcDir = Path.Combine(solutionDir, "SampleApps", "BlazorWasmApp0");
        using var workDir = WorkDirectory.CreateCopyFrom(srcDir, dst => dst.Name is not "obj" and not "bin");

        var expectedHomeTitles = new Dictionary<int, string> { [1] = "Home", [2] = "My Home" };
        var expectedEnvNames = new Dictionary<int, string> { [1] = "Prerendering", [2] = "Foo" };
        for (var i = 1; i <= 2; i++)
        {
            Console.WriteLine($"{(i == 1 ? "1st" : "2nd")} time publishing...");

            // When
            var arg = "publish -c:Debug -p:BlazorEnableCompression=false -o:bin/publish";
            // if 2nd time publishing, override the environment name.
            if (i == 2) arg += " -p:BlazorWasmPrerenderingEnvironment=" + expectedEnvNames[2];

            var dotnetCLI = await XProcess.Start("dotnet", arg, workDir).WaitForExitAsync();
            dotnetCLI.ExitCode.Is(0, message: dotnetCLI.StdOutput + dotnetCLI.StdError);

            // Then

            // Validate prerendered contents.
            var wwwrootDir = Path.Combine(workDir, "bin", "publish", "wwwroot");
            ValidatePrerenderedContents_of_BlazorWasmApp0(
                wwwrootDir,
                homeTitle: expectedHomeTitles[i],
                environment: expectedEnvNames[i]);

            // Validate PWA assets manifest.
            var indexHtmlBytes = File.ReadAllBytes(Path.Combine(wwwrootDir, "index.html"));
            using var sha256 = SHA256.Create();
            var hash = "sha256-" + Convert.ToBase64String(sha256.ComputeHash(indexHtmlBytes));

            var serviceWorkerAssetsJs = File.ReadAllText(Path.Combine(wwwrootDir, "my-assets.js"));
            serviceWorkerAssetsJs = Regex.Replace(serviceWorkerAssetsJs, @"^self\.assetsManifest\s*=\s*", "");
            serviceWorkerAssetsJs = Regex.Replace(serviceWorkerAssetsJs, ";\\s*$", "");
            var assetsManifestFile = JsonSerializer.Deserialize<AssetsManifestFile>(serviceWorkerAssetsJs);
            var assetManifestEntry = assetsManifestFile?.assets?.First(a => a.url == "index.html");
            assetManifestEntry.IsNotNull();
            assetManifestEntry!.hash.Is(hash);
        }
    }

    [Test]
    public async Task Publish_by_msbuild_Test()
    {
        // Given
        var solutionDir = FileIO.FindContainerDirToAncestor("*.sln");
        var srcDir = Path.Combine(solutionDir, "SampleApps", "BlazorWasmApp0");
        using var workDir = WorkDirectory.CreateCopyFrom(srcDir, dst => dst.Name is not "obj" and not "bin");

        for (var i = 0; i < 2; i++)
        {
            Console.WriteLine($"{(i == 0 ? "1st" : "2nd")} time publishing...");

            // When
            await XProcess.Start("dotnet", "restore", workDir).WaitForExitAsync();
            var dotnetCLI = await XProcess.Start("dotnet", "msbuild -p:Configuration=Debug -p:BlazorEnableCompression=false -p:DeployOnBuild=true -p:PublishUrl=bin/publish", workDir).WaitForExitAsync();
            dotnetCLI.ExitCode.Is(0, message: dotnetCLI.StdOutput + dotnetCLI.StdError);

            // Then

            // Validate prerendered contents.
            var wwwrootDir = Path.Combine(workDir, "bin", "publish", "wwwroot");
            ValidatePrerenderedContents_of_BlazorWasmApp0(wwwrootDir);
        }
    }

    [Test]
    public async Task Publish_with_HTTP500_Test()
    {
        // Given
        var solutionDir = FileIO.FindContainerDirToAncestor("*.sln");
        var srcDir = Path.Combine(solutionDir, "SampleApps", "BlazorWasmApp1");
        using var workDir = WorkDirectory.CreateCopyFrom(srcDir, dst => dst.Name is not "obj" and not "bin");

        // When (Set the hoting environment name to "ExceptionTest")
        var dotnetCLI = await XProcess.Start("dotnet", "publish -c:Release -p:BlazorWasmPrerenderingEnvironment=ExceptionTest -p:BlazorEnableCompression=false --nologo", workDir).WaitForExitAsync();

        // Then (Exit code is NOT 0)
        dotnetCLI.ExitCode.IsNot(0, message: dotnetCLI.Output);
    }

    [Test]
    public async Task AppSettings_Test()
    {
        // Given
        var solutionDir = FileIO.FindContainerDirToAncestor("*.sln");
        var srcDir = Path.Combine(solutionDir, "SampleApps", "BlazorWasmApp0");
        using var workDir = WorkDirectory.CreateCopyFrom(srcDir, dst => dst.Name is not "obj" and not "bin");

        File.WriteAllText(Path.Combine(workDir, "wwwroot", "appsettings.json"), @"{""HomeTitle"":""127.0.0.1""}");

        // When
        var dotnetCLI = await XProcess.Start("dotnet", "publish -c:Debug -p:BlazorEnableCompression=false -o:bin/publish", workDir).WaitForExitAsync();
        dotnetCLI.ExitCode.Is(0, message: dotnetCLI.StdOutput + dotnetCLI.StdError);

        // Then

        // Validate prerendered contents.
        var wwwrootDir = Path.Combine(workDir, "bin", "publish", "wwwroot");
        ValidatePrerenderedContents_of_BlazorWasmApp0(wwwrootDir, homeTitle: "127.0.0.1");
    }

    private static void ValidatePrerenderedContents_of_BlazorWasmApp0(string wwwrootDir, string homeTitle = "Home", string environment = "Prerendering", OutputStyle outputStyle = OutputStyle.AppendHtmlExtension)
    {
        var rootIndexHtmlPath = Path.Combine(wwwrootDir, "index.html");
        var aboutIndexHtmlPath = outputStyle == OutputStyle.AppendHtmlExtension ?
            Path.Combine(wwwrootDir, "about.html") :
            Path.Combine(wwwrootDir, "about", "index.html");
        File.Exists(rootIndexHtmlPath).IsTrue();
        File.Exists(aboutIndexHtmlPath).IsTrue();

        var htmlParser = new HtmlParser();
        using var rootIndexHtml = htmlParser.ParseDocument(File.ReadAllText(rootIndexHtmlPath));
        using var aboutIndexHtml = htmlParser.ParseDocument(File.ReadAllText(aboutIndexHtmlPath));

        // NOTICE: The document title was rendered by the <HeadOutlet> component of .NET 6.
        rootIndexHtml.Title.Is($"{homeTitle} | Blazor Wasm App 0");
        aboutIndexHtml.Title.Is("About | Blazor Wasm App 0");

        rootIndexHtml.QuerySelector("h1")!.TextContent.Is(homeTitle);
        aboutIndexHtml.QuerySelector("h1")!.TextContent.Is("About");

        rootIndexHtml.QuerySelector("a")!.TextContent.Is("about");
        (rootIndexHtml.QuerySelector("a") as IHtmlAnchorElement)!.Href.Is("about:///about");
        aboutIndexHtml.QuerySelector("a")!.TextContent.Is("home");
        (aboutIndexHtml.QuerySelector("a") as IHtmlAnchorElement)!.Href.Is("about:///");

        rootIndexHtml.QuerySelector(".environment")!.TextContent.Trim().Is($"Environment: {environment}");
    }
}
