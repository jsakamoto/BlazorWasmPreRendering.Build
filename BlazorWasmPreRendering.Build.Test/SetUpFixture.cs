using NUnit.Framework;
using Toolbelt;
using Toolbelt.Diagnostics;

namespace BlazorWasmPreRendering.Build.Test;

[SetUpFixture]
public class SetUpFixture
{
    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        var slnDir = FileIO.FindContainerDirToAncestor("*.sln");
        var webHostProjDir = Path.Combine(slnDir, "BlazorWasmPreRendering.Build.WebHost");
        var targetDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".webhost");
        if (Directory.Exists(targetDir)) Directory.Delete(targetDir, recursive: true);

        var dotnetCLI = await XProcess.Start(
            "dotnet",
            $"publish -c:Release -f:net8.0 -o:\"{targetDir}\"",
            webHostProjDir)
            .WaitForExitAsync();
        dotnetCLI.ExitCode.Is(0, message: dotnetCLI.Output);

        foreach (var file in Directory.GetFiles(targetDir, "Microsoft.*.dll")) File.Delete(file);
        foreach (var file in Directory.GetFiles(targetDir, "web.config")) File.Delete(file);
        foreach (var file in Directory.GetFiles(targetDir, "*.deps.json")) File.Delete(file);
    }
}
