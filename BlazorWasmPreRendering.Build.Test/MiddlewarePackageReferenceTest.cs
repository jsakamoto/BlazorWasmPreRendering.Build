using NUnit.Framework;
using Toolbelt.Blazor.WebAssembly.PreRendering.Build;

namespace BlazorWasmPreRendering.Build.Test;

public class MiddlewarePackageReferenceTest
{
    [Test]
    public void Parse_Test()
    {
        // When
        var middlewarePackages = MiddlewarePackageReferenceBuilder.Build("", "Foo.Bar,,1.2.0.3;Fizz.Buzz,FizzBuzz,");

        // Then
        middlewarePackages
            .Select(packref => packref.ToString())
            .Is("Foo.Bar,,1.2.0.3",
                "Fizz.Buzz,FizzBuzz,");
    }

    [Test]
    public async Task BuildFromAssemblyMetadata_Test()
    {
        // Given
        using var publishDir = await SampleSite.BlazorWasmApp0.PublishAsync();

        // When
        var middlewarePackages = MiddlewarePackageReferenceBuilder.Build(folderToScan: SampleSite.BlazorWasmApp0.TargetDir, "");

        // Then
        middlewarePackages
            .Select(packref => packref.ToString())
            .OrderBy(packref => packref)
            .Is("MiddlewarePackage1,,1.0.0",
                "MiddlewarePackage2,Middleware2,2.0.0");
    }

    [Test]
    public async Task BuildFromAssemblyMetadata_with_Parse_Test()
    {
        // Given
        using var publishDir = await SampleSite.BlazorWasmApp0.PublishAsync();

        // When
        var middlewarePackages = MiddlewarePackageReferenceBuilder.Build(
            folderToScan: SampleSite.BlazorWasmApp0.TargetDir,
            "MiddlewarePackage2,,2.0.0-beta.2;MiddlewarePackage1,Middleware1,1.2.0-preview.1");

        // Then
        middlewarePackages
            .Select(packref => packref.ToString())
            .OrderBy(packref => packref)
            .Is("MiddlewarePackage1,Middleware1,1.2.0-preview.1",
                "MiddlewarePackage2,Middleware2,2.0.0");
    }
}
