using System.Security.Cryptography;
using NUnit.Framework;
using Toolbelt;
using Toolbelt.Blazor.WebAssembly.PreRendering.Build;
using Toolbelt.Blazor.WebAssembly.PreRendering.Build.Models;

namespace BlazorWasmPreRendering.Build.Test.ProgramE2ETests;

public class ServiceWorkerAssetsManifestTest
{
    [Test]
    public async Task UpdateServiceWorkerAssetsJs_IndexHtmlInSubFolders_TestAsync()
    {
        // Given
        // Publish the sample app which sets its titles by .NET 6 <PageTitle>.
        using var publishDir = await SampleSite.BlazorWasmApp0.PublishAsync();
        using var intermediateDir = new WorkDirectory();
        var wwwroot = Path.Combine(publishDir, "wwwroot");

        // When
        // Execute prerenderer
        var exitCode = await Program.Main(new[] {
            "--assemblyname", "BlazorWasmApp0",
            "-t", "BlazorWasmApp0.App",
            "--selectorofrootcomponent", "#app,app",
            "--selectorofheadoutletcomponent", "head::after",
            "-p", publishDir,
            "-i", intermediateDir,
            "--assemblydir", SampleSite.BlazorWasmApp0.TargetDir,
            "-m", "",
            "-f", "net6.0",
            "--emulateauthme", "true",
            "-o", "IndexHtmlInSubFolders",
            "--serviceworkerassetsmanifest", "my-assets.js",
        });
        exitCode.Is(0);

        // Then
        using var sha256 = SHA256.Create();
        var hashOfIndexHtml = "sha256-" + Convert.ToBase64String(sha256.ComputeHash(File.ReadAllBytes(Path.Combine(wwwroot, "index.html"))));
        Console.WriteLine($"hashOfIndexHtml is [{hashOfIndexHtml}]");
        var hashOfAboutIndexHtml = "sha256-" + Convert.ToBase64String(sha256.ComputeHash(File.ReadAllBytes(Path.Combine(wwwroot, "about", "index.html"))));
        Console.WriteLine($"hashOfAboutIndexHtml is [{hashOfAboutIndexHtml}]");

        // Validate prerendered contents.
        var serviceworkerAssetsManifest = Path.Combine(publishDir, "wwwroot", "my-assets.js");
        var manifests = await AssetsManifestFile.LoadAsync(serviceworkerAssetsManifest);

        manifests.IsNotNull();
        manifests.assets.IsNotNull();
        manifests.assets.First(a => a.url == "index.html").hash.Is(hashOfIndexHtml);
        manifests.assets.First(a => a.url == "about/index.html").hash.Is(hashOfAboutIndexHtml);
    }

    [Test]
    public async Task UpdateServiceWorkerAssetsJs_AppendHtmlExtension_TestAsync()
    {
        // Given
        // Publish the sample app which sets its titles by .NET 6 <PageTitle>.
        using var publishDir = await SampleSite.BlazorWasmApp0.PublishAsync();
        using var intermediateDir = new WorkDirectory();
        var wwwroot = Path.Combine(publishDir, "wwwroot");

        // When
        // Execute prerenderer
        var exitCode = await Program.Main(new[] {
            "--assemblyname", "BlazorWasmApp0",
            "-t", "BlazorWasmApp0.App",
            "--selectorofrootcomponent", "#app,app",
            "--selectorofheadoutletcomponent", "head::after",
            "-p", publishDir,
            "-i", intermediateDir,
            "--assemblydir", SampleSite.BlazorWasmApp0.TargetDir,
            "-m", "",
            "-f", "net6.0",
            "--emulateauthme", "true",
            "-o", "AppendHtmlExtension",
            "--serviceworkerassetsmanifest", "my-assets.js",
        });
        exitCode.Is(0);

        // Then
        using var sha256 = SHA256.Create();
        var hashOfIndexHtml = "sha256-" + Convert.ToBase64String(sha256.ComputeHash(File.ReadAllBytes(Path.Combine(wwwroot, "index.html"))));
        Console.WriteLine($"hashOfIndexHtml is [{hashOfIndexHtml}]");
        var hashOfAboutHtml = "sha256-" + Convert.ToBase64String(sha256.ComputeHash(File.ReadAllBytes(Path.Combine(wwwroot, "about.html"))));
        Console.WriteLine($"hashOfAboutHtml is [{hashOfAboutHtml}]");

        // Validate prerendered contents.
        var serviceworkerAssetsManifest = Path.Combine(publishDir, "wwwroot", "my-assets.js");
        var manifests = await AssetsManifestFile.LoadAsync(serviceworkerAssetsManifest);

        manifests.IsNotNull();
        manifests.assets.IsNotNull();
        manifests.assets.First(a => a.url == "index.html").hash.Is(hashOfIndexHtml);
        manifests.assets.First(a => a.url == "about.html").hash.Is(hashOfAboutHtml);
    }
}

