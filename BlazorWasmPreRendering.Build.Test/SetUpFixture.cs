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
        // [NOTE]: "dotnet test" launches this test process from inside MSBuild, so MSBuild-specific environment
        //         variables are inherited. Every "dotnet build"/"dotnet publish" process that the tests start
        //         would inherit them too, which breaks launching MSBuild task hosts. (ex: MSB4216)
        Environment.SetEnvironmentVariable("MSBuildSDKsPath", null);

        var slnDir = FileIO.FindContainerDirToAncestor("*.slnx");
        var webHostProjDir = Path.Combine(slnDir, "BlazorWasmPreRendering.Build.WebHost");
        var targetDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".webhost");
        if (Directory.Exists(targetDir)) Directory.Delete(targetDir, recursive: true);

        var dotnetCLI = await XProcess.Start(
            "dotnet",
            $"publish -c:Release -f:{SampleSite.BlazorWasmApp0.TargetFramework} -o:\"{targetDir}\"",
            webHostProjDir)
            .WaitForExitAsync();
        dotnetCLI.ExitCode.Is(0, message: dotnetCLI.Output);

        foreach (var file in Directory.GetFiles(targetDir, "Microsoft.*.dll")) File.Delete(file);
        foreach (var file in Directory.GetFiles(targetDir, "web.config")) File.Delete(file);
        foreach (var file in Directory.GetFiles(targetDir, "*.deps.json")) File.Delete(file);
    }
}
