using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Toolbelt.Blazor.WebAssembly.PrerenderServer.WebHost
{
    internal class ServerSideRenderingWebHost
    {
        public static async Task<IWebHost> StartWebHostAsync(CustomAssemblyLoader assemblyLoader, string? environment, BlazorWasmPrerenderingOptions prerenderingOptions)
        {
            const string baseAddress = "http://127.0.0.1:5050";
            var hostEnvironment = new HostEnvironment(baseAddress, environment ?? "Prerendering");

            var appsettingsPath = Path.Combine(prerenderingOptions.WebRootPath, "appsettings.json");
            var appsettingsEnvironmentPath = Path.Combine(prerenderingOptions.WebRootPath, $"appsettings.{hostEnvironment.Environment}.json");
            var configuration = new ConfigurationBuilder()
                .AddJsonFile(appsettingsPath, optional: true)
                .AddJsonFile(appsettingsEnvironmentPath, optional: true)
                .Build();

            var hostBuilder = new WebHostBuilder()
                .UseConfiguration(configuration)
                .UseKestrel()
                .UseUrls(baseAddress)
                .UseWebRoot(prerenderingOptions.WebRootPath)
                .ConfigureServices(services => services
                    .AddSingleton(assemblyLoader)
                    .AddSingleton(hostEnvironment as IWebAssemblyHostEnvironment))
                .UseStartup(context => new Startup(context.Configuration, new Uri(baseAddress), hostEnvironment, prerenderingOptions));
            var webHost = hostBuilder.Build();
            await webHost.StartAsync();
            return webHost;
        }
    }
}
