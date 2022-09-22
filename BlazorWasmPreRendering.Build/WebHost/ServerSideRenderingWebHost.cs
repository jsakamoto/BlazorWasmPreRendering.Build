using System;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Toolbelt.Blazor.WebAssembly.PrerenderServer.Internal;

namespace Toolbelt.Blazor.WebAssembly.PrerenderServer.WebHost
{
    internal class ServerSideRenderingWebHost
    {
        public static async Task<IWebHost> StartWebHostAsync(CustomAssemblyLoader assemblyLoader, string? environment, string serverPort, BlazorWasmPrerenderingOptions prerenderingOptions)
        {
            var usedTcpPorts = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Select(listener => listener.Port).ToHashSet();
            var availabeTcpPort = IntEnumerator.ParseRangeText(serverPort).FirstOrDefault(port => !usedTcpPorts.Contains(port));
            if (availabeTcpPort == 0) throw new Exception($"There is no avaliable TCP port in range \"{serverPort}\".");

            var baseAddress = $"http://127.0.0.1:{availabeTcpPort}";
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
