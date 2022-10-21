using NUnit.Framework;
using Toolbelt;
using Toolbelt.Blazor.WebAssembly.PreRendering.Build.Shared;

namespace BlazorWasmPreRendering.Build.Test;
public class WebHostProgramTest
{
    [Test]
    public void WebHostProgram_BuildPrerenderingContext_Test()
    {
        // Given
        using var workFolder = new WorkDirectory();
        var webRoot = Path.Combine(workFolder, "wwwroot");
        Directory.CreateDirectory(Path.Combine(webRoot, "_framework"));
        File.Copy(sourceFileName: Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "BlazorWasmApp1.dll"),
                  destFileName: Path.Combine(webRoot, "_framework", "BlazorWasmApp1.dll"));
        File.Copy(sourceFileName: Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "index.html"),
                  destFileName: Path.Combine(webRoot, "index.html"));

        var options = new ServerSideRenderingOptions
        {
            WebRootPath = webRoot,
            MiddlewareDllsDir = webRoot,
            AssemblyName = "BlazorWasmApp1",
            RootComponentTypeName = "BlazorWasmApp1.App",
        };

        // When
        var context = Toolbelt.Blazor.WebAssembly.PreRendering.Build.WebHost.Program.BuildPrerenderingContext(options);

        // Then
        context.RootComponentType.FullName.Is("BlazorWasmApp1.App");
    }
}
