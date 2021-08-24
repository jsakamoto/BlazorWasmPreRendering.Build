using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Toolbelt.Blazor.Extensions.DependencyInjection;

namespace BlazorWasmApp0
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("app");
            ConfigureServices(builder.Services, builder.HostEnvironment.BaseAddress);
            await builder.Build().RunAsync();
        }

        private static void ConfigureServices(IServiceCollection services, string baseAddress)
        {
            services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(baseAddress) });
            services.AddHeadElementHelper();
        }
    }
}
