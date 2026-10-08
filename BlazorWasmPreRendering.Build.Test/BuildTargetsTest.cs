using System.Text.Json;
using NUnit.Framework;
using Toolbelt;
using static Toolbelt.Diagnostics.XProcess;

namespace BlazorWasmPreRendering.Build.Test;

public class BuildTargetsTest
{
    [Test]
    public async Task DefineProperties_Normalizes_MultiLine_Values_Test()
    {
        // Given: property values written across several lines with tabs (Issue #69)
        (string Name, string Value, string Expected)[] properties = [
            ("UrlPathToExplicitFetch", "\r\n\t\t/events;\r\n\t\t/articles;\r\n\t", "/events; /articles;"),
            ("UrlPathRegexToIgnore", "\r\n\t\t^/fetchdata$|\r\n\t\t^/about/\\.net$\r\n\t", "^/fetchdata$|^/about/\\.net$"),
        ];

        using var workDir = new WorkDirectory();
        var solutionDir = FileIO.FindContainerDirToAncestor("*.slnx");
        var targetsFilePath = Path.Combine(solutionDir, "BlazorWasmPreRendering.Build", "build", "BlazorWasmPreRendering.Build.targets");
        var propertyLines = properties.Select(p => $"    <BlazorWasmPrerendering{p.Name}>{p.Value}</BlazorWasmPrerendering{p.Name}>");
        File.WriteAllText(Path.Combine(workDir, "Test.proj"), $"""
            <Project>
              <Import Project="{targetsFilePath}" />
              <PropertyGroup>
                <PublishDir>bin/publish/</PublishDir>
                <TargetDir>bin/</TargetDir>
            {string.Join("\r\n", propertyLines)}
              </PropertyGroup>
            </Project>
            """);

        // When
        var getPropertyArgs = string.Join(" ", properties.Select(p => $"-getProperty:BlazorWasmPrerendering{p.Name}"));
        using var dotnetCLI = await Start("dotnet", $"msbuild Test.proj -nologo -t:BlazorWasmPrerendering_DefineProperties {getPropertyArgs}", workDir).WaitForExitAsync();
        dotnetCLI.ExitCode.Is(0, message: dotnetCLI.Output);

        // Then
        var actualProperties = JsonDocument.Parse(dotnetCLI.StdOutput).RootElement.GetProperty("Properties");
        properties
            .Select(p => $"{p.Name}|{actualProperties.GetProperty("BlazorWasmPrerendering" + p.Name).GetString()}")
            .Is(properties.Select(p => $"{p.Name}|{p.Expected}"));
    }
}
