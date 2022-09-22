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
        internal static int GetAvailableTcpPort(string tcpPortRangeText)
        {
            var usedTcpPorts = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Select(listener => listener.Port).ToHashSet();
            var availabeTcpPort = IntEnumerator.ParseRangeText(tcpPortRangeText).FirstOrDefault(port => !usedTcpPorts.Contains(port));
            if (availabeTcpPort == 0) throw new Exception($"There is no avaliable TCP port in range \"{tcpPortRangeText}\".");
            return availabeTcpPort;
        }

        public static async Task<IWebHost> StartWebHostAsync(CustomAssemblyLoader assemblyLoader, string? environment, string serverPort, BlazorWasmPrerenderingOptions prerenderingOptions)
        {
            var availabeTcpPort = GetAvailableTcpPort(serverPort);
            var baseAddress = $"http://127.0.0.1:{availabeTcpPort}";
            var hostEnvironment = new HostEnvironment(baseAddress, environment ?? "Prerendering");

            var appsettingsPath = Path.Combine(prerenderingOptions.WebRootPath, "appsettings.json");
            var appsettingsEnvironmentPath = Path.Combine(prerenderingOptions.WebRootPath, $"appsettings.{hostEnvironment.Environment}.json");
            var configuration = new ConfigurationBuilder()
                .AddJsonFile(appsettingsPath, optional: true)
                .AddJsonFile(appsettingsEnvironmentPath, optional: true)
                .Build();

            var webHost = default(IWebHost);
            var hostBuilder = new WebHostBuilder()
                .UseConfiguration(configuration)
                .UseKestrel()
                .UseUrls(baseAddress)
                .UseWebRoot(prerenderingOptions.WebRootPath)
                .ConfigureServices(services => services
                    .AddSingleton(assemblyLoader)
                    .AddSingleton(hostEnvironment as IWebAssemblyHostEnvironment)
                    // NOTICE: The next line is important to expose the IWebHost object to the HTTP request handler. 
                    //         For more detail, see the source code inside the "Startup.cs".
                    .AddTransient(_ => webHost ?? throw new NullReferenceException()))
                .UseStartup(context => new Startup(context.Configuration, new Uri(baseAddress), hostEnvironment, prerenderingOptions));
            webHost = hostBuilder.Build();
            await webHost.StartAsync();
            return webHost;
        }
    }
}
