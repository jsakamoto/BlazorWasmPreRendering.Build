using BlazorWasmApp1;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Toolbelt.Blazor.Extensions.DependencyInjection;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
if (!builder.RootComponents.Any())
{
    builder.RootComponents.Add<App>("#app");
}
ConfigureServices(builder.Services, builder.HostEnvironment.BaseAddress);
await builder.Build().RunAsync();

static void ConfigureServices(IServiceCollection services, string baseAddress, IWebAssemblyHostEnvironment? hostEnvironment = null)
{
    if (hostEnvironment?.Environment != "ServiceNotRegisteredTest")
    {
        services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(baseAddress) });
        services.AddHeadElementHelper();
    }
}
