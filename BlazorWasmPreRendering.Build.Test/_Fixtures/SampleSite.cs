using NUnit.Framework;
using Toolbelt;
using Toolbelt.Diagnostics;

namespace BlazorWasmPreRendering.Build.Test;

[SetUpFixture]
public class SampleSite : IDisposable
{
    /// <summary>
    /// - .NET 6<br/>
    /// - its titles by .NET 6 &lt;PageTitle&gt;.<br/>
    /// - PWA
    /// </summary>
    public static SampleSite BlazorWasmApp0 { get; } = new SampleSite("BlazorWasmApp0", "net6.0");

    /// <summary>
    /// - .NET 5<br/>
    /// - its titles by Toolbelt.Blazor.HeadElemnt.
    /// </summary>
    public static SampleSite BlazorWasmApp1 { get; } = new SampleSite("BlazorWasmApp1", "net5.0");

    /// <summary>
    /// - .NET 5<br/>
    /// - Consist with separated component project.
    /// </summary>
    public static SampleSite BlazorWasmApp2 { get; } = new SampleSite("BlazorWasmApp2/Client", "net5.0");

    public string ProjectName { get; } = "";

    /// <summary>
    /// Get the target framework of this sample site project. (ex."net5.0", "net6.0")
    /// </summary>
    public string TargetFramework { get; } = "";

    public string Configuration { get; } = "";

    public string ProjectDir { get; } = "";

    /// <summary>
    /// Get the intermediate directory path of this sample site project. (ex. "~/obj/Release/net6.0")
    /// </summary>
    public string IntermediateDir { get; } = "";

    private string PublishSrcDir { get; } = "";

    private WorkDirectory? SampleAppsWorkDir { get; }

    private bool PublishedOnce { get; set; } = false;

    public SampleSite()
    {
    }

    public SampleSite(string projectName, string targetFramework, string configuration = "Release")
    {
        this.ProjectName = projectName;
        this.TargetFramework = targetFramework;
        this.Configuration = configuration;

        this.SampleAppsWorkDir = CreateSampleAppsWorkDir();

        this.ProjectDir = Path.Combine($"{this.SampleAppsWorkDir}/{projectName}".Split('/'));
        this.IntermediateDir = Path.Combine(this.ProjectDir, "obj", this.Configuration, this.TargetFramework);
        this.PublishSrcDir = Path.Combine(this.ProjectDir, "bin", this.Configuration, this.TargetFramework, "publish");
    }

    public async ValueTask<WorkDirectory> PublishAsync()
    {
        if (!this.PublishedOnce)
        {
            var publishProcess = XProcess.Start(
                "dotnet",
                $"publish -c:{this.Configuration} -p:BlazorWasmPrerendering=disable -p:BlazorEnableCompression=false -p:UsingBrowserRuntimeWorkload=false",
                workingDirectory: this.ProjectDir);
            await publishProcess.WaitForExitAsync();
            publishProcess.ExitCode.Is(0, message: publishProcess.StdOutput + publishProcess.StdError);
            this.PublishedOnce = true;
        }

        return WorkDirectory.CreateCopyFrom(this.PublishSrcDir, _ => true);
    }

    public static WorkDirectory CreateSampleAppsWorkDir()
    {
        var solutionDir = FileIO.FindContainerDirToAncestor("*.sln");
        var srcSampleAppsDir = Path.Combine(solutionDir, "SampleApps");

        return WorkDirectory.CreateCopyFrom(srcSampleAppsDir, arg => arg.Name is (not "obj" and not "bin"));
    }

    public void Dispose()
    {
        this.SampleAppsWorkDir?.Dispose();
    }

    [OneTimeTearDown]
    public void Cleanup()
    {
        BlazorWasmApp0.Dispose();
        BlazorWasmApp1.Dispose();
        BlazorWasmApp2.Dispose();
    }
}
