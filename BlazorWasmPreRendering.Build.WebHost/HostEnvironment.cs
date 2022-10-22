using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace Toolbelt.Blazor.WebAssembly.PreRendering.Build.WebHost
{
    internal record HostEnvironment(string BaseAddress, string Environment) : IWebAssemblyHostEnvironment;
}
