using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Loader;
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
            this.Configuration = configuration;
            this.PrerenderingOptions = prerenderingOptions;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(this.PrerenderingOptions);

            var baseAddress = this.Configuration["urls"].Split(';').First(url => !string.IsNullOrWhiteSpace(url));
            this.ConfigureApplicationServices(services, baseAddress);

            services.TryAddScoped(sp => new HttpClient { BaseAddress = new Uri(baseAddress) });

            services.AddRazorPages();
            services.AddServerSideBlazor();
        }

        private void ConfigureApplicationServices(IServiceCollection services, string baseAddress)
        {
            var programClass = this.PrerenderingOptions.ApplicationAssembly
                .GetTypes()
                .FirstOrDefault(t => t.Name == "Program" || t.Name == "<Program>$");
            if (programClass == null) return;

            var configureServicesMethod = programClass
                .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.Name == "ConfigureServices" || m.Name.Split('|').First().EndsWith(">g__ConfigureServices"));
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

            this.ConfigureApplicationMiddleware(app);

            app.UseStaticFiles(new StaticFileOptions { ServeUnknownFileTypes = true });
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapRazorPages();
                endpoints.MapFallbackToPage("/_Host");
            });
        }

        internal void ConfigureApplicationMiddleware(IApplicationBuilder app)
        {
            foreach (var pack in this.PrerenderingOptions.MiddlewarePackages)
            {
                var assemblyName = string.IsNullOrEmpty(pack.Assembly) ? pack.PackageIdentity : pack.Assembly;
                var appAssembly = AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName(assemblyName));//.LoadFromAssemblyPath(appAssemblyPath);
                var useMethods = appAssembly.ExportedTypes
                    .Where(t => t.IsClass && t.IsSealed && t.IsAbstract) // means static class (https://stackoverflow.com/a/2639465/1268000)
                    .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                    .Where(m =>
                    {
                        if (!m.Name.StartsWith("Use")) return false;
                        var parameters = m.GetParameters();
                        if (parameters.Length != 1) return false;
                        if (parameters[0].ParameterType != typeof(IApplicationBuilder)) return false;
                        return true;
                    });

                foreach (var useMethod in useMethods)
                {
                    useMethod.Invoke(null, new object[] { app });
                }
            }
        }
    }
}
