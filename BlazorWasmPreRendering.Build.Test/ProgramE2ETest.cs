using System;
using System.IO;
using System.Threading.Tasks;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using NUnit.Framework;
using Toolbelt;
using Toolbelt.Blazor.WebAssembly.PrerenderServer;
using Toolbelt.Diagnostics;

namespace BlazorWasmPreRendering.Build.Test
{
    public class ProgramE2ETest
    {
        [Test]
        public async Task dotNET6_HeadOutlet_TestAsync()
        {
            // Given

            // Publish the sample app which sets its titles by .NET 6 <PageTitle>.
            var solutionDir = FileIO.FindContainerDirToAncestor("*.sln");
            var sampleAppProjectDir = Path.Combine(solutionDir, "SampleApps", "BlazorWasmApp0");
            using var publishDir = new WorkDirectory();

            var publishProcess = XProcess.Start(
                "dotnet",
                $"publish -c:Debug -p:BlazorEnableCompression=false -p:BlazorWasmPrerendering=disable -o:\"{publishDir}\"",
                workingDirectory: sampleAppProjectDir);
            await publishProcess.WaitForExitAsync();
            publishProcess.ExitCode.Is(0, message: publishProcess.StdOutput + publishProcess.StdError);

            // When

            // Execute prerenderer
            var exitCode = await Program.Main(new[] {
                "-a", "BlazorWasmApp0",
                "-t", "BlazorWasmApp0.App",
                "--selectorofrootcomponent", "#app,app",
                "--selectorofheadoutletcomponent", "head::after",
                "-p", publishDir,
                "-i", Path.Combine(sampleAppProjectDir, "obj", "Debug", "net6.0"),
                "-m", "",
                "-f", "net6.0"
            });
            exitCode.Is(0);

            // Then

            // Validate prerendered contents.
            var wwwrootDir = Path.Combine(publishDir, "wwwroot");
            ValidatePrerenderedContents_of_BlazorWasmApp0(wwwrootDir);
        }

        [Test]
        public async Task Including_ServerSide_Middleware_TestAsync()
        {
            // Given

            // Publish the sample app which sets its titles by Toolbelt.Blazor.HeadElement
            var solutionDir = FileIO.FindContainerDirToAncestor("*.sln");
            var sampleAppProjectDir = Path.Combine(solutionDir, "SampleApps", "BlazorWasmApp1");
            using var publishDir = new WorkDirectory();

            var publishProcess = XProcess.Start(
                "dotnet",
                $"publish -c:Debug -p:BlazorEnableCompression=false -p:BlazorWasmPrerendering=disable -o:\"{publishDir}\"",
                workingDirectory: sampleAppProjectDir);
            await publishProcess.WaitForExitAsync();
            publishProcess.ExitCode.Is(0, message: publishProcess.StdOutput + publishProcess.StdError);

            // When

            // Execute prerenderer
            var exitCode = await Program.Main(new[] {
                "-a", "BlazorWasmApp1",
                "-t", "BlazorWasmApp1.App",
                "--selectorofrootcomponent", "#app,app",
                "--selectorofheadoutletcomponent", "head::after",
                "-p", publishDir,
                "-i", Path.Combine(sampleAppProjectDir, "obj", "Debug", "net5.0"),
                "-m", "Toolbelt.Blazor.HeadElement.ServerPrerendering,,1.5.2",
                "-f", "net5.0"
            });
            exitCode.Is(0);

            // Then

            // Validate prerendered contents.

            var wwwrootDir = Path.Combine(publishDir, "wwwroot");
            var rootIndexHtmlPath = Path.Combine(wwwrootDir, "index.html");
            var counterIndexHtmlPath = Path.Combine(wwwrootDir, "counter", "index.html");
            var fetchdataIndexHtmlPath = Path.Combine(wwwrootDir, "fetchdata", "index.html");
            File.Exists(rootIndexHtmlPath).IsTrue();
            File.Exists(counterIndexHtmlPath).IsTrue();
            File.Exists(fetchdataIndexHtmlPath).IsTrue();

            var htmlParser = new HtmlParser();
            using var rootIndexHtml = htmlParser.ParseDocument(File.ReadAllText(rootIndexHtmlPath));
            using var counterIndexHtml = htmlParser.ParseDocument(File.ReadAllText(counterIndexHtmlPath));
            using var fetchdataIndexHtml = htmlParser.ParseDocument(File.ReadAllText(fetchdataIndexHtmlPath));

            // NOTICE: The document title was rendered by the Toolbelt.Blazor.HeadElement
            rootIndexHtml.Title.Is("Home");
            counterIndexHtml.Title.Is("Counter");
            fetchdataIndexHtml.Title.Is("Weather forecast");

            rootIndexHtml.QuerySelector("h1").TextContent.Is("Hello, world!");
            counterIndexHtml.QuerySelector("h1").TextContent.Is("Counter");
            fetchdataIndexHtml.QuerySelector("h1").TextContent.Is("Weather forecast");
        }

        [Test]
        public async Task AppComponent_is_in_the_other_Assembly_TestAsync()
        {
            // Given

            // Publish the sample app
            var solutionDir = FileIO.FindContainerDirToAncestor("*.sln");
            var sampleAppProjectDir = Path.Combine(solutionDir, "SampleApps", "BlazorWasmApp2", "Client");
            using var publishDir = new WorkDirectory();

            var publishProcess = XProcess.Start(
                "dotnet",
                $"publish -c:Debug -p:BlazorEnableCompression=false -o:\"{publishDir}\"",
                workingDirectory: sampleAppProjectDir);
            await publishProcess.WaitForExitAsync();
            publishProcess.ExitCode.Is(0, message: publishProcess.StdOutput + publishProcess.StdError);

            // When

            // Execute prerenderer
            var exitCode = await Program.Main(new[] {
                "-a", "BlazorWasmApp2.Client",
                "-t", "BlazorWasmApp2.Components.App, BlazorWasmApp2.Components", // INCLUDES ASSEMBLY NAME
                "--selectorofrootcomponent", "#app,app",
                "--selectorofheadoutletcomponent", "head::after",
                "-p", publishDir,
                "-i", Path.Combine(sampleAppProjectDir, "obj", "Debug", "net5.0"),
                "-m", "",
                "-f", "net5.0"
            });
            exitCode.Is(0);

            // Then

            // Validate prerendered contents.

            var wwwrootDir = Path.Combine(publishDir, "wwwroot");
            var rootIndexHtmlPath = Path.Combine(wwwrootDir, "index.html");
            var aboutIndexHtmlPath = Path.Combine(wwwrootDir, "about-this-site", "index.html");
            File.Exists(rootIndexHtmlPath).IsTrue();
            File.Exists(aboutIndexHtmlPath).IsTrue();

            var htmlParser = new HtmlParser();
            using var rootIndexHtml = htmlParser.ParseDocument(File.ReadAllText(rootIndexHtmlPath));
            using var aboutIndexHtml = htmlParser.ParseDocument(File.ReadAllText(aboutIndexHtmlPath));

            rootIndexHtml.QuerySelector("h1").TextContent.Trim().Is("Welcome to Blazor Wasm App 2!");
            aboutIndexHtml.QuerySelector("h1").TextContent.Trim().Is("About Page");
        }

        [Test]
        public async Task AppComponent_is_in_the_other_Assembly_and_FallBack_TestAsync()
        {
            // Given

            // Publish the sample app
            var solutionDir = FileIO.FindContainerDirToAncestor("*.sln");
            var sampleAppProjectDir = Path.Combine(solutionDir, "SampleApps", "BlazorWasmApp2", "Client");
            using var publishDir = new WorkDirectory();

            var publishProcess = XProcess.Start(
                "dotnet",
                $"publish -c:Debug -p:BlazorEnableCompression=false -o:\"{publishDir}\"",
                workingDirectory: sampleAppProjectDir);
            await publishProcess.WaitForExitAsync();
            publishProcess.ExitCode.Is(0, message: publishProcess.StdOutput + publishProcess.StdError);

            // When

            // Execute prerenderer
            var exitCode = await Program.Main(new[] {
                "-a", "BlazorWasmApp2.Client",
                "-t", "BlazorWasmApp2.Client.App", // INVALID TYPE NAME OF ROOT COMPONENT
                "--selectorofrootcomponent", "#app,app",
                "--selectorofheadoutletcomponent", "head::after",
                "-p", publishDir,
                "-i", Path.Combine(sampleAppProjectDir, "obj", "Debug", "net5.0"),
                "-m", "",
                "-f", "net5.0"
            });
            exitCode.Is(0);

            // Then

            // Validate prerendered contents.

            var wwwrootDir = Path.Combine(publishDir, "wwwroot");
            var rootIndexHtmlPath = Path.Combine(wwwrootDir, "index.html");
            var aboutIndexHtmlPath = Path.Combine(wwwrootDir, "about-this-site", "index.html");
            File.Exists(rootIndexHtmlPath).IsTrue();
            File.Exists(aboutIndexHtmlPath).IsTrue();

            var htmlParser = new HtmlParser();
            using var rootIndexHtml = htmlParser.ParseDocument(File.ReadAllText(rootIndexHtmlPath));
            using var aboutIndexHtml = htmlParser.ParseDocument(File.ReadAllText(aboutIndexHtmlPath));

            rootIndexHtml.QuerySelector("h1").TextContent.Trim().Is("Welcome to Blazor Wasm App 2!");
            aboutIndexHtml.QuerySelector("h1").TextContent.Trim().Is("About Page");
        }

        [Test]
        public async Task Publish_Test()
        {
            // Given
            var solutionDir = FileIO.FindContainerDirToAncestor("*.sln");
            var srcDir = Path.Combine(solutionDir, "SampleApps", "BlazorWasmApp0");
            using var workDir = WorkDirectory.CreateCopyFrom(srcDir, dst => dst.Name is not "obj" and not "bin");

            for (var i = 0; i < 2; i++)
            {
                Console.WriteLine($"{(i == 0 ? "1st" : "2nd")} time publishing...");

                // When
                var dotnetCLI = await XProcess.Start("dotnet", "publish -c:Debug -p:BlazorEnableCompression=false -o:bin/publish", workDir).WaitForExitAsync();
                dotnetCLI.ExitCode.Is(0, message: dotnetCLI.StdOutput + dotnetCLI.StdError);

                // Then

                // Validate prerendered contents.
                var wwwrootDir = Path.Combine(workDir, "bin", "publish", "wwwroot");
                ValidatePrerenderedContents_of_BlazorWasmApp0(wwwrootDir);
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

        private static void ValidatePrerenderedContents_of_BlazorWasmApp0(string wwwrootDir)
        {
            var rootIndexHtmlPath = Path.Combine(wwwrootDir, "index.html");
            var aboutIndexHtmlPath = Path.Combine(wwwrootDir, "about", "index.html");
            File.Exists(rootIndexHtmlPath).IsTrue();
            File.Exists(aboutIndexHtmlPath).IsTrue();

            var htmlParser = new HtmlParser();
            using var rootIndexHtml = htmlParser.ParseDocument(File.ReadAllText(rootIndexHtmlPath));
            using var aboutIndexHtml = htmlParser.ParseDocument(File.ReadAllText(aboutIndexHtmlPath));

            // NOTICE: The document title was rendered by the <HeadOutlet> component of .NET 6.
            rootIndexHtml.Title.Is("Home | Blazor Wasm App 0");
            aboutIndexHtml.Title.Is("About | Blazor Wasm App 0");

            rootIndexHtml.QuerySelector("h1").TextContent.Is("Home");
            aboutIndexHtml.QuerySelector("h1").TextContent.Is("About");

            rootIndexHtml.QuerySelector("a").TextContent.Is("about");
            (rootIndexHtml.QuerySelector("a") as IHtmlAnchorElement)!.Href.Is("about:///about");
            aboutIndexHtml.QuerySelector("a").TextContent.Is("home");
            (aboutIndexHtml.QuerySelector("a") as IHtmlAnchorElement)!.Href.Is("about:///");
        }
    }
}
