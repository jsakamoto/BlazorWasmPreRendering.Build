using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using NUnit.Framework;
using Toolbelt;
using Toolbelt.Blazor.WebAssembly.PreRendering.Build;
using Toolbelt.Blazor.WebAssembly.PreRendering.Build.Models;
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
        using var intermediateDir = new WorkDirectory();

        // When
        // Execute prerenderer
        var exitCode = await Program.Main(new[] {
            "-a", "BlazorWasmApp0",
            "-t", "BlazorWasmApp0.App",
            "--selectorofrootcomponent", "#app,app",
            "--selectorofheadoutletcomponent", "head::after",
            "-p", publishDir,
            "-i", intermediateDir,
            "-m", "",
            "-f", "net6.0"
        });
        exitCode.Is(0);

        // Then
        // Validate prerendered contents.
        var wwwrootDir = Path.Combine(publishDir, "wwwroot");
        ValidatePrerenderedContents(wwwrootDir, outputStyle: OutputStyle.IndexHtmlInSubFolders);
    }

    [TestCase(true)]
    [TestCase(false)]
    [Parallelizable(ParallelScope.Children)]
    public async Task Including_ServerSide_Middleware_Legacy_TestAsync(bool deleteLoadingContents)
    {
        // Given
        // Publish the sample app which sets its titles by Toolbelt.Blazor.HeadElement
        using var publishDir = await SampleSite.BlazorWasmApp1.PublishAsync();
        using var tcpPort = TcpPortPool.GetAvailableTcpPort();
        using var intermediateDir = new WorkDirectory();

        // When
        // Execute prerenderer
        var exitCode = await Program.Main(new[] {
            "-a", "BlazorWasmApp1",
            "-t", "BlazorWasmApp1.App",
            "--selectorofrootcomponent", "#app,app",
            "--selectorofheadoutletcomponent", "head::after",
            "-p", publishDir,
            "-i", intermediateDir,
            "-m", "Toolbelt.Blazor.HeadElement.ServerPrerendering,,1.5.2",
            "-f", "net5.0",
            "--serverport", tcpPort,
            deleteLoadingContents ? "-d" : ""
        });
        exitCode.Is(0);

        // Then

        // Validate prerendered contents.

        var wwwrootDir = Path.Combine(publishDir, "wwwroot");
        var expectedHtmlFiles = GetFullPathList(wwwrootDir, "about/.net/index.html", "counter/index.html", "fetchdata/index.html", "index.html");

        var actualHtmlFiles = Directory.GetFiles(wwwrootDir, "*.html", SearchOption.AllDirectories).OrderBy(path => path).ToArray();
        actualHtmlFiles.Is(expectedHtmlFiles);

        // NOTICE: The document title was rendered by the Toolbelt.Blazor.HeadElement
        const string loadingContents = "Loading...";
        Validate(actualHtmlFiles[3], loadingContents, title_is: "Home", h1_is: "Hello, world!", deleteLoadingContents);
        Validate(actualHtmlFiles[1], loadingContents, title_is: "Counter", h1_is: "Counter", deleteLoadingContents);
        Validate(actualHtmlFiles[2], loadingContents, title_is: "Weather forecast", h1_is: "Weather forecast", deleteLoadingContents);
        Validate(actualHtmlFiles[0], loadingContents, title_is: "About .NET", h1_is: "About .NET", deleteLoadingContents);
    }

    [Test]
    public async Task Including_ServerSide_Middleware_from_AssemblyMetaData_TestAsync()
    {
        using var sampleAppWorkDir = SampleSite.CreateSampleAppsWorkDir();
        var projectDir = Path.Combine(sampleAppWorkDir, "BlazorWasmApp0");
        var serverPort = Program.GetAvailableTcpPort("5050-5999");

        using var dotnetCLI = Start(
            "dotnet", $"publish -c:Release -p:BlazorWasmPrerenderingKeepServer=true -p:BlazorEnableCompression=false -p:UsingBrowserRuntimeWorkload=false -p:BlazorWasmPrerenderingServerPort={serverPort}",
            projectDir,
            options => options.WhenDisposing = XProcessTerminate.EntireProcessTree);
        var success = await dotnetCLI.WaitForOutputAsync(output => output.Trim().StartsWith("Start fetching..."), millsecondsTimeout: 15000);
        if (!success) { throw new Exception(dotnetCLI.Output); }

        var serverUrl = $"http://127.0.0.1:{serverPort}/";
        using var httpClient = new HttpClient();
        try
        {
            var httpResponse = await httpClient.GetAsync(serverUrl);
            httpResponse.EnsureSuccessStatusCode();

            httpResponse.Headers.TryGetValues("X-Middleware1-Version", out var values1).IsTrue();
            values1.Is("1.0.0.0");

            httpResponse.Headers.TryGetValues("X-Middleware2-Version", out var values2).IsTrue();
            values2.Is("2.0.0.0");
        }
        finally
        {
            // NOTICE: Killing the "dotnet publish" process doesn't kill the pre-rendering server process, even though it is a child process of the "dotnet publish" process.
            // Therefore, this test code kicks the back door of the pre-rendering server (sending "HTTP DELETE /" request) to terminate it.
            try { await httpClient.DeleteAsync(serverUrl); } catch { }
        }
    }

    [TestCase(true)]
    [TestCase(false)]
    [Parallelizable(ParallelScope.Children)]
    public async Task Including_EasterEggPage_TestAsync(bool deleteLoadingContents)
    {
        // Given
        using var intermediateDir = new WorkDirectory();
        using var publishDir = await SampleSite.BlazorWasmApp1.PublishAsync();
        using var tcpPort = TcpPortPool.GetAvailableTcpPort();

        // When
        // Execute prerenderer
        var exitCode = await Program.Main(new[] {
            "-a", "BlazorWasmApp1",
            "-t", "BlazorWasmApp1.App",
            "--selectorofrootcomponent", "#app,app",
            "--selectorofheadoutletcomponent", "head::after",
            "-p", publishDir,
            "-i", intermediateDir,
            "-m", "Toolbelt.Blazor.HeadElement.ServerPrerendering,,1.5.2",
            "-f", "net5.0",
            "-o", "AppendHtmlExtension",
            "-u", "/easter-egg",
            "--serverport", tcpPort,
            deleteLoadingContents? "-d" : ""
        });
        exitCode.Is(0);

        // Then

        // Validate prerendered contents.

        var wwwrootDir = Path.Combine(publishDir, "wwwroot");
        var expectedHtmlFiles = GetFullPathList(wwwrootDir, "about/.net.html", "counter.html", "easter-egg.html", "fetchdata.html", "index.html");

        var actualHtmlFiles = Directory.GetFiles(wwwrootDir, "*.html", SearchOption.AllDirectories).OrderBy(path => path).ToArray();
        actualHtmlFiles.Is(expectedHtmlFiles);

        // NOTICE: The document title was rendered by the Toolbelt.Blazor.HeadElement
        const string loadingContents = "Loading...";
        Validate(actualHtmlFiles[4], loadingContents, title_is: "Home", h1_is: "Hello, world!", deleteLoadingContents);
        Validate(actualHtmlFiles[1], loadingContents, title_is: "Counter", h1_is: "Counter", deleteLoadingContents);
        Validate(actualHtmlFiles[3], loadingContents, title_is: "Weather forecast", h1_is: "Weather forecast", deleteLoadingContents);
        Validate(actualHtmlFiles[0], loadingContents, title_is: "About .NET", h1_is: "About .NET", deleteLoadingContents);
        Validate(actualHtmlFiles[2], loadingContents, title_is: "Easter Egg", h1_is: "Hello, Easter Egg!", deleteLoadingContents);
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
    [Parallelizable(ParallelScope.Children)]
    public async Task AppComponent_is_in_the_other_Assembly_TestAsync(bool deleteLoadingContents)
    {
        // Given
        // Publish the sample app
        using var publishDir = await SampleSite.BlazorWasmApp2.PublishAsync();
        using var tcpPort = TcpPortPool.GetAvailableTcpPort();
        using var intermediateDir = new WorkDirectory();

        // When
        // Execute prerenderer
        var exitCode = await Program.Main(new[] {
            "-a", "BlazorWasmApp2.Client",
            "-t", "BlazorWasmApp2.Components.App, BlazorWasmApp2.Components", // INCLUDES ASSEMBLY NAME
            "--selectorofrootcomponent", "#app,app",
            "--selectorofheadoutletcomponent", "head::after",
            "-p", publishDir,
            "-i", intermediateDir,
            "-m", "",
            "-f", "net5.0",
            "--serverport", tcpPort,
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
    [Parallelizable(ParallelScope.Children)]
    public async Task AppComponent_is_in_the_other_Assembly_and_FallBack_TestAsync(bool deleteLoadingContents)
    {
        // Given
        // Publish the sample app
        using var publishDir = await SampleSite.BlazorWasmApp2.PublishAsync();
        using var tcpPort = TcpPortPool.GetAvailableTcpPort();
        using var intermediateDir = new WorkDirectory();

        // When
        // Execute prerenderer
        var exitCode = await Program.Main(new[] {
            "-a", "BlazorWasmApp2.Client",
            "-t", "BlazorWasmApp2.Client.App", // INVALID TYPE NAME OF ROOT COMPONENT
            "--selectorofrootcomponent", "#app,app",
            "--selectorofheadoutletcomponent", "head::after",
            "-p", publishDir,
            "-i", intermediateDir,
            "-m", "",
            "-f", "net5.0",
            "--serverport", tcpPort,
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
        using var sampleAppWorkDir = SampleSite.CreateSampleAppsWorkDir();
        var projectDir = Path.Combine(sampleAppWorkDir, "BlazorWasmApp0");

        var expectedHomeTitles = new Dictionary<int, string> { [1] = "Home", [2] = "My Home" };
        var expectedEnvNames = new Dictionary<int, string> { [1] = "Prerendering", [2] = "Foo" };
        for (var i = 1; i <= 2; i++)
        {
            Console.WriteLine($"{(i == 1 ? "1st" : "2nd")} time publishing...");

            // When
            var arg = "publish -c:Debug -p:BlazorEnableCompression=false -o:bin/publish";
            // if 2nd time publishing, override the environment name.
            if (i == 2) arg += " -p:BlazorWasmPrerenderingEnvironment=" + expectedEnvNames[2];

            var dotnetCLI = await Start("dotnet", arg, projectDir).WaitForExitAsync();
            dotnetCLI.ExitCode.Is(0, message: dotnetCLI.StdOutput + dotnetCLI.StdError);

            // Then

            // Validate prerendered contents.
            var wwwrootDir = Path.Combine(projectDir, "bin", "publish", "wwwroot");
            ValidatePrerenderedContents(
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
            ValidatePrerenderedContents(wwwrootDir);
        }
    }

    [TestCase("ExceptionTest", false, false)]
    [TestCase("ServiceNotRegisteredTest", true, false)]
    [TestCase("JSInvokeOnServerTest", false, true)]
    [Parallelizable(ParallelScope.Children)]
    public async Task Publish_with_HTTP500_Test(string env, bool msg1, bool msg2)
    {
        // Given
        using var sampleAppWorkDir = SampleSite.CreateSampleAppsWorkDir();
        var projectDir = Path.Combine(sampleAppWorkDir, "BlazorWasmApp1");
        using var tcpPort = TcpPortPool.GetAvailableTcpPort();

        // When (Set the hoting environment name to "ExceptionTest")
        var dotnetCLI = await Start("dotnet", "publish " +
            $"-c:Release " +
            $"-p:BlazorWasmPrerenderingEnvironment={env} " +
            $"-p:BlazorEnableCompression=false " +
            $"-p:BlazorWasmPrerenderingServerPort={tcpPort} " +
            $"--nologo",
            projectDir).WaitForExitAsync();

        // Then (Exit code is NOT 0)
        var output = dotnetCLI.Output;
        dotnetCLI.ExitCode.IsNot(0, message: output);

        // Then (guide messages was shown accroing to its situation)
        var title1 = "[ERROR] There is no registered service";
        var message1 = "If the \"Program.cs\" of your Blazor WebAssembly app is like this";
        var title2 = "[ERROR] JavaScript interop calls cannot be issued at this time";
        var message2 = "if you are calling JavaScript code in \"OnInitializedAsync()\" like this:";
        output.Contains(title1).Is(msg1);
        output.Contains(message1).Is(msg1);
        output.Contains(title2).Is(msg2);
        output.Contains(message2).Is(msg2);
    }

    [TestCase("", "FizzBuzz")]
    [TestCase("ChangeHeaders", "")]
    [TestCase("Xor", "")]
    [Parallelizable(ParallelScope.Children)]
    public async Task Publish_BlazorWasmAntivirusProtection_Test(string obfuscationMode, string xorKey)
    {
        // Given
        using var sampleAppWorkDir = SampleSite.CreateSampleAppsWorkDir();
        var projectDir = Path.Combine(sampleAppWorkDir, "BlazorWasmAVP");
        using var tcpPort = TcpPortPool.GetAvailableTcpPort();

        // When
        var args = new List<string>()
        {
            $"publish",
            $"-c:Release",
            $"-p:BlazorEnableCompression=false",
            $"-p:UsingBrowserRuntimeWorkload=false",
            $"-p:BlazorWasmPrerenderingServerPort={tcpPort}",
            $"-o:bin/publish",
        };
        if (obfuscationMode != "") args.Add($"-p:ObfuscationMode={obfuscationMode}");
        if (xorKey != "") args.Add($"-p:XorKey={xorKey}");

        var dotnetCLI = await Start(
            "dotnet", string.Join(" ", args),
            projectDir).WaitForExitAsync();

        // Then
        dotnetCLI.ExitCode.Is(0, message: dotnetCLI.Output);

        var wwwrootDir = Path.Combine(projectDir, "bin", "publish", "wwwroot");
        ValidatePrerenderedContents(wwwrootDir, homeTitle: "Home of BlazorWasmAVP", aboutTitle: "About of BlazorWasmAVP", outputStyle: OutputStyle.IndexHtmlInSubFolders);
    }

    [Test]
    public async Task AppSettings_Test()
    {
        // Given
        using var sampleAppWorkDir = SampleSite.CreateSampleAppsWorkDir();
        var projectDir = Path.Combine(sampleAppWorkDir, "BlazorWasmApp0");
        File.WriteAllText(Path.Combine(projectDir, "wwwroot", "appsettings.json"), @"{""HomeTitle"":""127.0.0.1""}");

        // When
        var dotnetCLI = await XProcess.Start("dotnet", "publish -c:Debug -p:BlazorEnableCompression=false -o:bin/publish", projectDir).WaitForExitAsync();
        dotnetCLI.ExitCode.Is(0, message: dotnetCLI.StdOutput + dotnetCLI.StdError);

        // Then

        // Validate prerendered contents.
        var wwwrootDir = Path.Combine(projectDir, "bin", "publish", "wwwroot");
        ValidatePrerenderedContents(wwwrootDir, homeTitle: "127.0.0.1");
    }

    [Test]
    public async Task Localization_TestAsync()
    {
        // Given
        // Publish the sample app that is localized.
        using var publishDir = await SampleSite.BlazorWasmApp0.PublishAsync();
        using var intermediateDir = new WorkDirectory();

        // When
        // Execute prerenderer
        var exitCode = await Program.Main(new[] {
            "-a", "BlazorWasmApp0",
            "-t", "BlazorWasmApp0.App",
            "--selectorofrootcomponent", "#app,app",
            "--selectorofheadoutletcomponent", "head::after",
            "-p", publishDir,
            "-i", intermediateDir,
            "-m", "",
            "-f", "net6.0",
            "--locale", "ja,en"
        });
        exitCode.Is(0);

        // Then
        // Validate prerendered contents: the title of the "/about" page is localized.
        var wwwrootDir = Path.Combine(publishDir, "wwwroot");
        ValidatePrerenderedContents(wwwrootDir, aboutTitle: "アバウト", outputStyle: OutputStyle.IndexHtmlInSubFolders);
    }

    [Test]
    public async Task ServeDotFile_Test()
    {
        // Given
        using var sampleAppWorkDir = SampleSite.CreateSampleAppsWorkDir();
        var projectDir = Path.Combine(sampleAppWorkDir, "BlazorWasmApp1");
        var serverPort = Program.GetAvailableTcpPort("5050-5999");

        using var dotnetCLI = Start(
            "dotnet", $"publish -c:Release -p:BlazorWasmPrerenderingKeepServer=true -p:BlazorEnableCompression=false -p:UsingBrowserRuntimeWorkload=false -p:BlazorWasmPrerenderingServerPort={serverPort}",
            projectDir,
            options => options.WhenDisposing = XProcessTerminate.EntireProcessTree);
        var success = await dotnetCLI.WaitForOutputAsync(output => output.Trim().StartsWith("Start fetching..."), millsecondsTimeout: 15000);
        if (!success) { throw new Exception(dotnetCLI.Output); }

        var serverUrl = $"http://127.0.0.1:{serverPort}/";
        using var httpClient = new HttpClient();
        try
        {
            // When
            var httpResponse = await httpClient.GetAsync(serverUrl + "sample-data/.dot.json");

            // Then: the json file that the name of it starts with "." can be read.
            httpResponse.EnsureSuccessStatusCode();
            var jsonText = await httpResponse.Content.ReadAsStringAsync();
            jsonText.Split('\n').Select(s => s.TrimEnd('\r')).Is(
                "{",
                "  \"name\": \".dot.json\"",
                "}");
        }
        finally
        {
            // NOTICE: Killing the "dotnet publish" process doesn't kill the pre-rendering server process, even though it is a child process of the "dotnet publish" process.
            // Therefore, this test code kicks the back door of the pre-rendering server (sending "HTTP DELETE /" request) to terminate it.
            try { await httpClient.DeleteAsync(serverUrl); } catch { }
        }
    }

    private static void ValidatePrerenderedContents(string wwwrootDir, string homeTitle = "Home", string aboutTitle = "About", string environment = "Prerendering", OutputStyle outputStyle = OutputStyle.AppendHtmlExtension)
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
        rootIndexHtml.Title.IsNotNull().StartsWith($"{homeTitle} | Blazor Wasm App").IsTrue(message: $"rootIndexHtml.Title is: \"{rootIndexHtml.Title}\"");
        aboutIndexHtml.Title.IsNotNull().StartsWith($"{aboutTitle} | Blazor Wasm App").IsTrue(message: $"aboutIndexHtml.Title is: \"{aboutIndexHtml.Title}\"");

        rootIndexHtml.QuerySelector("h1")!.TextContent.Is(homeTitle);
        aboutIndexHtml.QuerySelector("h1")!.TextContent.Is(aboutTitle);

        rootIndexHtml.QuerySelector("a")!.TextContent.Is("about");
        (rootIndexHtml.QuerySelector("a") as IHtmlAnchorElement)!.Href.Is("about:///about");
        aboutIndexHtml.QuerySelector("a")!.TextContent.Is("home");
        (aboutIndexHtml.QuerySelector("a") as IHtmlAnchorElement)!.Href.Is("about:///");

        rootIndexHtml.QuerySelector(".environment")!.TextContent.Trim().Is($"Environment: {environment}");
    }
}
