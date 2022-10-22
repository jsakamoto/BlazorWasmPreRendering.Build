using Microsoft.AspNetCore.Mvc.Rendering;
using NUnit.Framework;
using Toolbelt;
using Toolbelt.Blazor.WebAssembly.PreRendering.Build.Shared;
using BuildProgram = Toolbelt.Blazor.WebAssembly.PrerenderServer.Program;

namespace BlazorWasmPreRendering.Build.Test;

public class BuildProgramTests
{
    [Test]
    public void GenerateProjectToGetMiddleware_Test()
    {
        // Given
        using var workFolder = new WorkDirectory();
        var middlewarePackages = new MiddlewarePackageReference[] {
            new() { PackageIdentity = "Toolbelt.Blazor.HeadElement.ServerPrerendering", Assembly = "", Version = "1.5.1" }
        };

        // When
        var outputDir = BuildProgram.GenerateProjectToGetMiddleware(middlewarePackages, workFolder, "net5.0");

        // Then
        outputDir.IsNotNull();
        var generatedProjectFilePath = Path.Combine(outputDir, "Project.csproj");
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
        var outputDir = BuildProgram.GenerateProjectToGetMiddleware(middlewarePackages, workFolder, "net5.0");

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
        var outputDir = BuildProgram.GetMiddlewareDlls(workFolder, frameworkName: "net5.0");

        // Then
        outputDir.Is(Path.Combine(workFolder, "bin", "Release", "net5.0"));
        var targetAssemblyPath = Path.Combine(outputDir, "Toolbelt.Blazor.HeadElement.ServerPrerendering.dll");
        File.Exists(targetAssemblyPath).IsTrue();
    }

    [Test]
    public void StoreOptionsToEnvironment_Test()
    {
        var webHostOptions = new ServerSideRenderingOptions
        {
            AssemblyName = "Project",
            DeleteLoadingContents = true,
            Environment = "Production",
            IndexHtmlFragments = {
                FirstPart = "<html>\r\n<head>\r\n",
                LastPart = "</body>\r\n</html>",
                MiddlePart = "</head>\r\n<body>\r\n",
            },
            MiddlewareDllsDir = "C:\\project\\obj\\Release\\net6.0\\middleware",
            MiddlewarePackages = {
                new() { PackageIdentity = "Foo", Assembly = "Bar", Version = "1.0.0" },
                new() { PackageIdentity = "Fizz", Assembly = "Buzz", Version = "2.0.1" },
            },
            RenderMode = RenderMode.WebAssemblyPrerendered,
            RootComponentTypeName = "Project.App",
            ServerPort = 5678,
            WebRootPath = "C:\\project\\bin\\Release\\net6.0\\wwwroot",
        };
        var environment = new Dictionary<string, string?>();
        BuildProgram.StoreOptionsToEnvironment(webHostOptions, "X_", environment);

        environment
            .OrderBy(item => item.Key)
            .Select(item => $"{item.Key}|{item.Value}")
            .Is("X_AssemblyName|Project",
                "X_DeleteLoadingContents|True",
                "X_Environment|Production",
                "X_IndexHtmlFragments:FirstPart|<html>\r\n<head>\r\n",
                "X_IndexHtmlFragments:LastPart|</body>\r\n</html>",
                "X_IndexHtmlFragments:MiddlePart|</head>\r\n<body>\r\n",
                "X_MiddlewareDllsDir|C:\\project\\obj\\Release\\net6.0\\middleware",
                "X_MiddlewarePackages:0:Assembly|Bar",
                "X_MiddlewarePackages:0:PackageIdentity|Foo",
                "X_MiddlewarePackages:0:Version|1.0.0",
                "X_MiddlewarePackages:1:Assembly|Buzz",
                "X_MiddlewarePackages:1:PackageIdentity|Fizz",
                "X_MiddlewarePackages:1:Version|2.0.1",
                "X_RenderMode|WebAssemblyPrerendered",
                "X_RootComponentTypeName|Project.App",
                "X_ServerPort|5678",
                "X_WebRootPath|C:\\project\\bin\\Release\\net6.0\\wwwroot");
    }

    [Test]
    public void StoreOptionsToEnvironment_NoMiddlewarePackages_Test()
    {
        var webHostOptions = new ServerSideRenderingOptions
        {
            AssemblyName = "Project",
            DeleteLoadingContents = false,
            Environment = "Development",
            IndexHtmlFragments = {
                FirstPart = "<html>\r\n<head>\r\n",
                LastPart = "</body>\r\n</html>",
                MiddlePart = "</head>\r\n<body>\r\n"
            },
            MiddlewareDllsDir = "C:\\project\\obj\\Release\\net6.0\\middleware",
            RenderMode = RenderMode.Static,
            RootComponentTypeName = "Project.App",
            ServerPort = 5987,
            WebRootPath = "C:\\project\\bin\\Release\\net6.0\\wwwroot",
        };
        var environment = new Dictionary<string, string?>();
        BuildProgram.StoreOptionsToEnvironment(webHostOptions, "~", environment);

        environment
            .OrderBy(item => item.Key)
            .Select(item => $"{item.Key}|{item.Value}")
            .Is("~AssemblyName|Project",
                "~DeleteLoadingContents|False",
                "~Environment|Development",
                "~IndexHtmlFragments:FirstPart|<html>\r\n<head>\r\n",
                "~IndexHtmlFragments:LastPart|</body>\r\n</html>",
                "~IndexHtmlFragments:MiddlePart|</head>\r\n<body>\r\n",
                "~MiddlewareDllsDir|C:\\project\\obj\\Release\\net6.0\\middleware",
                "~RenderMode|Static",
                "~RootComponentTypeName|Project.App",
                "~ServerPort|5987",
                "~WebRootPath|C:\\project\\bin\\Release\\net6.0\\wwwroot");
    }
}
