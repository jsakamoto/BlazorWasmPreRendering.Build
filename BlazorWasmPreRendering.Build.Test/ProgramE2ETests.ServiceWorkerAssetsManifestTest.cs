using NUnit.Framework;
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
            "-f", "net6.0",
            "-o", "IndexHtmlInSubFolders",
            "--serviceworkerassetsmanifest", "my-assets.js",
        });
        exitCode.Is(0);

        // Then
        // Validate prerendered contents.
        var serviceworkerAssetsManifest = Path.Combine(publishDir, "wwwroot", "my-assets.js");
        var manifests = await AssetsManifestFile.LoadAsync(serviceworkerAssetsManifest);

        manifests.IsNotNull();
        manifests.assets.IsNotNull();
        manifests.assets.First(a => a.url == "index.html")
            .hash.Is("sha256-/RD0iXNwx+CA96bqbs/YilxbSW0SJs+77G/EMsQRWlQ=");
        manifests.assets.First(a => a.url == "about/index.html")
            .hash.Is("sha256-rLdD9LMgf/e9XJ3Gd8VgN4XM55kKbtXk0dReRbHNe80=");
    }

    [Test]
    public async Task UpdateServiceWorkerAssetsJs_AppendHtmlExtension_TestAsync()
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
            "-f", "net6.0",
            "-o", "AppendHtmlExtension",
            "--serviceworkerassetsmanifest", "my-assets.js",
        });
        exitCode.Is(0);

        // Then
        // Validate prerendered contents.
        var serviceworkerAssetsManifest = Path.Combine(publishDir, "wwwroot", "my-assets.js");
        var manifests = await AssetsManifestFile.LoadAsync(serviceworkerAssetsManifest);

        manifests.IsNotNull();
        manifests.assets.IsNotNull();
        manifests.assets.First(a => a.url == "index.html")
            .hash.Is("sha256-/RD0iXNwx+CA96bqbs/YilxbSW0SJs+77G/EMsQRWlQ=");
        manifests.assets.First(a => a.url == "about.html")
            .hash.Is("sha256-rLdD9LMgf/e9XJ3Gd8VgN4XM55kKbtXk0dReRbHNe80=");
    }
}

