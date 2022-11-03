using Microsoft.AspNetCore.Builder;
using Toolbelt;
using Toolbelt.Diagnostics;

namespace BlazorWasmPreRendering.Build.Test;

public static class TestSites
{
    private static string GetProjectDir() => FileIO.FindContainerDirToAncestor("*.csproj");

    private static string GetTestSitesDir() => Path.Combine(GetProjectDir(), "_Fixtures", "TestSites");

    public static async ValueTask<IAsyncDisposable> StartTestSite1(string baseUrl)
    {
        var webRootPath = Path.Combine(GetTestSitesDir(), "Site1");
        var webServer = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = new[] { "--urls", baseUrl },
            ContentRootPath = webRootPath
        }).Build();
        webServer.UseDefaultFiles().UseStaticFiles();
        await webServer.StartAsync();
        return webServer;
    }

    private class Disposer : IDisposable
    {
        private readonly Action _Action;
        public Disposer(Action action) { this._Action = action; }
        public void Dispose() => this._Action.Invoke();
    }

    public static async ValueTask<IDisposable> StartTestSite2(
        string baseUrl,
        bool serviceNotRegistered1 = false,
        bool serviceNotRegistered2 = false,
        bool jsInvokeOnServer = false)
    {
        var site2Dir = Path.Combine(GetTestSitesDir(), "Site2-ServerErrors");
        var workDir = WorkDirectory.CreateCopyFrom(site2Dir, arg => arg.Name != "obj" && arg.Name != "bin");
        var dotnetCLI = XProcess.Start(
            "dotnet", "run " +
            $"--urls {baseUrl} " +
            $"--ServiceNotRegistered1={serviceNotRegistered1} " +
            $"--ServiceNotRegistered2={serviceNotRegistered2} " +
            $"--JSInvokeOnServer={jsInvokeOnServer} ",
            workDir);
        var success = await dotnetCLI.WaitForOutputAsync(output => output.Contains(baseUrl), millsecondsTimeout: 15000);
        if (!success)
        {
            try { dotnetCLI.Dispose(); } catch { }
            try { workDir.Dispose(); } catch { }
            var output = dotnetCLI.Output;
            throw new TimeoutException($"\"dotnet run\" did not respond \"Now listening on: {baseUrl}\".\r\n" + output);
        }
        await Task.Delay(200);

        return new Disposer(() =>
        {
            dotnetCLI.Dispose();
            workDir.Dispose();
        });
    }
}
