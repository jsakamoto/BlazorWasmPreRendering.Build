using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using BlazorWasmPreRendering.Build.Test.Fixtures;
using NUnit.Framework;
using Toolbelt.Blazor.WebAssembly.PrerenderServer;
using Toolbelt.Diagnostics;

namespace BlazorWasmPreRendering.Build.Test
{
    public class ProgramE2ETest
    {
        [Test]
        public async Task ExecuteBlazorWasmPrerendering_TestAsync()
        {
            // Given

            // Publish the sample app
            var slnDir = AppDomain.CurrentDomain.BaseDirectory;
            while (slnDir != null && !Directory.GetFiles(slnDir, "*.sln", SearchOption.TopDirectoryOnly).Any()) slnDir = Path.GetDirectoryName(slnDir);
            if (slnDir == null) throw new Exception("The solution dir could not found.");
            var sampleAppProjectDir = Path.Combine(slnDir, "SampleApps", "BlazorWasmApp0");
            using var publishDir = new WorkFolder();

            var publishProcess = XProcess.Start(
                "dotnet",
                $"publish -c:Debug -p:BlazorEnableCompression=false -o:\"{publishDir}\"",
                workingDirectory: sampleAppProjectDir);
            await publishProcess.WaitForExitAsync();
            publishProcess.ExitCode.Is(0, message: publishProcess.StdOutput + publishProcess.StdError);

            // When

            // Execute prerenderer
            var exitCode = await Program.Main(new[] {
                "-a", "BlazorWasmApp0",
                "-t", "BlazorWasmApp0.App",
                "-s", "#app",
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
            var aboutIndexHtmlPath = Path.Combine(wwwrootDir, "about", "index.html");
            File.Exists(rootIndexHtmlPath).IsTrue();
            File.Exists(aboutIndexHtmlPath).IsTrue();

            var htmlParser = new HtmlParser();
            using var rootIndexHtml = htmlParser.ParseDocument(File.OpenRead(rootIndexHtmlPath));
            using var aboutIndexHtml = htmlParser.ParseDocument(File.OpenRead(aboutIndexHtmlPath));

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
