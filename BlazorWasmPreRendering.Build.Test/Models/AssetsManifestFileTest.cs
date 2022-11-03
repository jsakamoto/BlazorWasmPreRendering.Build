using NUnit.Framework;
using Toolbelt;
using Toolbelt.Blazor.WebAssembly.PreRendering.Build.Models;

namespace BlazorWasmPreRendering.Build.Test.Models;

public class AssetsManifestFileTest
{
    [Test]
    public async Task LoadAsync_Test()
    {
        // Given
        var assetsManifestFilePath = Assets.GetAssetPathOf("service-worker-assets.js");

        // When
        var assetsManifestFile = await AssetsManifestFile.LoadAsync(assetsManifestFilePath);

        // Then
        assetsManifestFile.IsNotNull();
        assetsManifestFile.version.Is("0NWDkJdM");
        assetsManifestFile.assets
            .IsNotNull()
            .Select(a => $"{a.url}|{a.hash}")
            .Is("images/social-preview.png|sha256-9ypkDLoAy6RmO7iZHmZc3dXAZaItavrLA3c35f4puyU=",
                "index.html|sha256-KhzlBHhh8jmcEoE/oSCHbgBXLJM+NtAIeedzu+JQILw=",
                "site.min.css|sha256-WxemB69D598vpN6bhltce1nR6942A8hm0GmDlVOsZw0=");
    }

    [Test]
    public async Task SaveAsync_Test()
    {
        // Given
        var assetsManifestFile = new AssetsManifestFile()
        {
            version = "0NWDkJdM",
            assets = new() {
                new(){ url = "images/social-preview.png", hash = "sha256-9ypkDLoAy6RmO7iZHmZc3dXAZaItavrLA3c35f4puyU=" },
                new(){ url = "index.html", hash = "sha256-KhzlBHhh8jmcEoE/oSCHbgBXLJM+NtAIeedzu+JQILw=" },
                new(){ url = "site.min.css", hash = "sha256-WxemB69D598vpN6bhltce1nR6942A8hm0GmDlVOsZw0=" },
            }
        };

        // When
        using var workDir = new WorkDirectory();
        var savedAssetsManifestFilePath = Path.Combine(workDir, "service-worker-assets.js");
        await assetsManifestFile.SaveAsync(savedAssetsManifestFilePath);

        // Then
        var actualLines = File.ReadLines(savedAssetsManifestFilePath);

        var expectedAssetsManifestFilePath = Assets.GetAssetPathOf("service-worker-assets.js");
        var expectedLines = File.ReadLines(expectedAssetsManifestFilePath);

        actualLines.Is(expectedLines);
    }
}
