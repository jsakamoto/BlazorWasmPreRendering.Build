using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Toolbelt.Blazor.WebAssembly.PreRendering.Build.WebHost
{
    internal class ServerSideRenderingWebHost
    {
        internal static async Task<IWebHost> StartWebHostAsync(ServerSideRenderingContext context)
        {
            var baseAddress = $"http://127.0.0.1:{context.ServerPort}";
            var hostEnvironment = new HostEnvironment(baseAddress, context.Environment ?? "Prerendering");

            var appsettingsPath = Path.Combine(context.WebRootPath, "appsettings.json");
            var appsettingsEnvironmentPath = Path.Combine(context.WebRootPath, $"appsettings.{hostEnvironment.Environment}.json");
            var configuration = new ConfigurationBuilder()
                .AddJsonFile(appsettingsPath, optional: true)
                .AddJsonFile(appsettingsEnvironmentPath, optional: true)
                .Build();

            var webHost = default(IWebHost);
            var hostBuilder = new WebHostBuilder()
                .UseConfiguration(configuration)
                .UseKestrel()
                .UseUrls(baseAddress)
                .UseWebRoot(context.WebRootPath)
                .ConfigureServices(services => services
                    .AddSingleton(context.AssemblyLoader)
                    .AddSingleton(hostEnvironment as IWebAssemblyHostEnvironment)
                    // NOTICE: The next line is important to expose the IWebHost object to the HTTP request handler. 
                    //         For more detail, see the source code inside the "Startup.cs".
                    .AddTransient(_ => webHost ?? throw new NullReferenceException()))
                .UseStartup(builderContext => new Startup(builderContext.Configuration, new Uri(baseAddress), hostEnvironment, context));
            webHost = hostBuilder.Build();
            await webHost.StartAsync();

            Console.WriteLine($"Listen to {baseAddress}/");
            return webHost;
        }
    }
}
