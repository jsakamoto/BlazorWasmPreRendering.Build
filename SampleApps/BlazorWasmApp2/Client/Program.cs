using BlazorWasmApp2.Client.Services;
using BlazorWasmApp2.Components;
using BlazorWasmApp2.Components.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
ConfigureServices(builder.Services);

await builder.Build().RunAsync();

static void ConfigureServices(IServiceCollection services)
{
    services.AddSingleton<IAppTitle, AppTitle>();
}
