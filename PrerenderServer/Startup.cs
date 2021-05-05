using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Toolbelt.Blazor.WebAssembly.PrerenderServer
{
    public class Startup
    {
        private IConfiguration Configuration { get; }
        private BlazorWasmPrerenderingOptions PrerenderingOptions { get; }

        public Startup(IConfiguration configuration, BlazorWasmPrerenderingOptions prerenderingOptions)
        {
            Configuration = configuration;
            PrerenderingOptions = prerenderingOptions;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            var baseAddress = this.Configuration["urls"].Split(';').First(url => !string.IsNullOrWhiteSpace(url));
            this.ConfigureApplicationServices(services, baseAddress);

            services.TryAddScoped(sp => new HttpClient { BaseAddress = new Uri(baseAddress) });

            services.AddRazorPages();
            services.AddServerSideBlazor();
        }

        private void ConfigureApplicationServices(IServiceCollection services, string baseAddress)
        {
            var programClass = this.PrerenderingOptions.ApplicationAssembly.GetTypes().FirstOrDefault(t => t.Name == "Program");
            if (programClass == null) return;

            var configureServicesMethod = programClass.GetMethod("ConfigureServices", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (configureServicesMethod == null) return;

            var arguments = new List<object?>();
            var methodParameters = configureServicesMethod.GetParameters();
            foreach (var methodParameter in methodParameters)
            {
                if (methodParameter.ParameterType == typeof(IServiceCollection))
                {
                    arguments.Add(services);
                }
                else if (methodParameter.ParameterType == typeof(string) && string.Equals(methodParameter.Name, nameof(baseAddress), StringComparison.InvariantCultureIgnoreCase))
                {
                    arguments.Add(baseAddress);
                }
                else
                {
                    arguments.Add(methodParameter.DefaultValue);
                }
            }

            configureServicesMethod.Invoke(null, arguments.ToArray());
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
            }

            app.UseDeveloperExceptionPage();

            app.UseStaticFiles(new StaticFileOptions { ServeUnknownFileTypes = true });
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapRazorPages();
                endpoints.MapFallbackToPage("/_Host");
            });
        }
    }
}
