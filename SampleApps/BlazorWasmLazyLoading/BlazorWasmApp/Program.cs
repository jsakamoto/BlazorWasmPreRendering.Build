using BlazorWasmApp;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
if (!builder.RootComponents.Any())
{
    builder.RootComponents.Add<App>("#app");
    builder.RootComponents.Add<HeadOutlet>("head::after");
}
ConfigureServices(builder.Services, builder.HostEnvironment.BaseAddress);
var host = builder.Build();

// 👇 Invoke the "PreloadAsync" method of the "LazyLoader" service
//    to preload lazy assemblies needed for the current URL path before running.
var lazyLoader = host.Services.GetRequiredService<LazyLoader>();
await lazyLoader.PreloadAsync();

await host.RunAsync();

static void ConfigureServices(IServiceCollection services, string baseAddress)
{
    // 👇 Register the "LazyLoader" service
    services.AddSingleton<LazyLoader>();

    services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(baseAddress) });
}