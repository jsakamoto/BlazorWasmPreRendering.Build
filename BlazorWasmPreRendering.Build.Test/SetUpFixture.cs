using NUnit.Framework;
using Toolbelt;
using Toolbelt.Diagnostics;

namespace BlazorWasmPreRendering.Build.Test;

[SetUpFixture]
public class SetUpFixture
{
    // Inherited by every child "dotnet" process; they pin it to the parent's SDK (ex: MSB4216) or, from the VS Code
    // C# Dev Kit, inject targets that a pinned older SDK cannot load (ex: MSB4062 in the BlazorWasmAVP sample).
    private static readonly string[] _VariablesToClear = [
        "MSBuildSDKsPath",
        "MSBuildExtensionsPath",
        "MSBUILD_EXE_PATH",
        "DOTNET_HOST_PATH",
        "CustomAfterMicrosoftCommonTargets",
        "CustomAfterMicrosoftCommonCrossTargetingTargets",
        "EnableProjectDataOnBuild",
        "EnableProjectDataInProjectFolder",
        "CSDEVKIT_PROJECTDATA_TASKS_ASSEMBLY"];

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        foreach (var name in _VariablesToClear) Environment.SetEnvironmentVariable(name, null);

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
