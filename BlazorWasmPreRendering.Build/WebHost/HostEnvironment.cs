using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace Toolbelt.Blazor.WebAssembly.PrerenderServer.WebHost
{
    internal record HostEnvironment(string BaseAddress, string Environment) : IWebAssemblyHostEnvironment;
}
