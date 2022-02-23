using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Toolbelt;
using Toolbelt.Blazor.WebAssembly.PrerenderServer;

namespace BlazorWasmPreRendering.Build.Test
{
    public class ProgramTests
    {
        [Test]
        public void BuildPrerenderingOptions_Test()
        {
            // Given
            using var workFolder = new WorkDirectory();
            var cmdlineOptions = new CommandLineOptions
            {
                IntermediateDir = workFolder,
                PublishedDir = workFolder,
                AssemblyName = "BlazorWasmApp1",
                TypeNameOfRootComponent = "BlazorWasmApp1.App",
                SelectorOfRootComponent = "#app",
                MiddlewarePackages = "",
                FrameworkName = "net5.0",
            };
            Directory.CreateDirectory(Path.Combine(cmdlineOptions.PublishedDir, "wwwroot", "_framework"));
            File.Copy(sourceFileName: Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "BlazorWasmApp1.dll"),
                      destFileName: Path.Combine(cmdlineOptions.PublishedDir, "wwwroot", "_framework", "BlazorWasmApp1.dll"));
            File.Copy(sourceFileName: Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "index.html"),
                      destFileName: Path.Combine(cmdlineOptions.PublishedDir, "wwwroot", "index.html"));

            // When
            var options = Program.BuildPrerenderingOptions(new CustomAssemblyLoader(), cmdlineOptions);

            // Then
            options.RootComponentType.FullName.Is("BlazorWasmApp1.App");
        }

        [Test]
        public void GenerateProjectToGetMiddleware_Test()
        {
            // Given
            using var workFolder = new WorkDirectory();
            var middlewarePackages = new MiddlewarePackageReference[] {
                new() { PackageIdentity = "Toolbelt.Blazor.HeadElement.ServerPrerendering", Assembly = "", Version = "1.5.1" }
            };

            // When
            var outputDir = Program.GenerateProjectToGetMiddleware(middlewarePackages, workFolder, "net5.0");

            // Then
            outputDir.IsNotNull();
            var generatedProjectFilePath = Path.Combine(outputDir!, "Project.csproj");
            File.Exists(generatedProjectFilePath).IsTrue();

            // Then
            var expectedProjectFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "ExpectedProjectFile.xml");
            var expectedProjectFileLines = File.ReadAllLines(expectedProjectFilePath).Select(line => line.Trim()).ToArray();
            var actualProjectFileLines = File.ReadAllLines(generatedProjectFilePath).Select(line => line.Trim()).ToArray();
            actualProjectFileLines.Is(expectedProjectFileLines);
        }

        [Test]
        public void GenerateProjectToGetMiddleware_IfEmpty_Test()
        {
            // Given
            using var workFolder = new WorkDirectory();
            var middlewarePackages = Enumerable.Empty<MiddlewarePackageReference>();

            // When
            var outputDir = Program.GenerateProjectToGetMiddleware(middlewarePackages, workFolder, "net5.0");

            // Then
            outputDir.IsNull();
        }

        [Test]
        public void GetMiddlewareDlls_Test()
        {
            // Given
            using var workFolder = new WorkDirectory();
            var templateProjectFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "ExpectedProjectFile.xml");
            var targetProjectFilePath = Path.Combine(workFolder, "Project.csproj");
            File.Copy(templateProjectFilePath, targetProjectFilePath);

            // When
            var outputDir = Program.GetMiddlewareDlls(workFolder, frameworkName: "net5.0");

            // Then
            outputDir.Is(Path.Combine(workFolder, "bin", "Release", "net5.0"));
            var targetAssemblyPath = Path.Combine(outputDir, "Toolbelt.Blazor.HeadElement.ServerPrerendering.dll");
            File.Exists(targetAssemblyPath).IsTrue();
        }
    }
}