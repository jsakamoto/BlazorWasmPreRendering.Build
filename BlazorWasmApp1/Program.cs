using System;
using System.Net.Http;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace BlazorWasmApp1
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = CreateBuilder(args);
            await builder.Build().RunAsync();
        }

        private static WebAssemblyHostBuilder CreateBuilder(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("#app");
            var rootComponents = builder.RootComponents.First();
            //rootComponents.

            Console.WriteLine("builder.HostEnvironment.BaseAddress");
            Console.WriteLine(builder.HostEnvironment.BaseAddress);

            builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
            return builder;
        }
    }
}
