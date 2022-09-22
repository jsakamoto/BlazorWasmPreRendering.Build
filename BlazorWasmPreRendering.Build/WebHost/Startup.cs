using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Toolbelt.Blazor.WebAssembly.PrerenderServer.WebHost
{
    public class Startup
    {
        private IConfiguration Configuration { get; }

        private Uri BaseAddress { get; }

        private IWebAssemblyHostEnvironment HostEnvironment { get; }

        private BlazorWasmPrerenderingOptions PrerenderingOptions { get; }

        public Startup(IConfiguration configuration, Uri baseAddress, IWebAssemblyHostEnvironment hostEnvironment, BlazorWasmPrerenderingOptions prerenderingOptions)
        {
            this.Configuration = configuration;
            this.BaseAddress = baseAddress;
            this.HostEnvironment = hostEnvironment;
            this.PrerenderingOptions = prerenderingOptions;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(this.PrerenderingOptions);

            this.ConfigureApplicationServices(services);

            services.TryAddScoped(sp => new HttpClient { BaseAddress = this.BaseAddress });

            services.AddRazorPages();
            services.AddServerSideBlazor();
            services.AddSingleton<ResetHeadOutletScript>();
        }

        private void ConfigureApplicationServices(IServiceCollection services)
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
                else if (methodParameter.ParameterType == typeof(IConfiguration))
                {
                    arguments.Add(this.Configuration);
                }
                else if (methodParameter.ParameterType == typeof(string) && string.Equals(methodParameter.Name, "baseAddress", StringComparison.InvariantCultureIgnoreCase))
                {
                    arguments.Add(this.BaseAddress.ToString().TrimEnd('/'));
                }
                else if (methodParameter.ParameterType == typeof(Uri))
                {
                    arguments.Add(this.BaseAddress);
                }
                else if (methodParameter.ParameterType == typeof(IWebAssemblyHostEnvironment))
                {
                    arguments.Add(this.HostEnvironment);
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
                // NOTICE: This is the back door of this pre-rendering server for the unit test purpose. 
                // When this server receives an "HTTP DELETE /" request, the server will be shut down even if the "KeepRunning" option was enabled.
                // This is important to certainly terminate this server during the clean-up process of the unit test.
                endpoints.MapDelete("/", async context =>
                {
                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    await context.Response.CompleteAsync();
                    var webHost = app.ApplicationServices.GetRequiredService<IWebHost>();
                    var _ = webHost.StopAsync().ConfigureAwait(false);
                });

                endpoints.MapRazorPages();
                endpoints.MapFallbackToPage("/_Host");
            });
        }

        internal void ConfigureApplicationMiddleware(IApplicationBuilder app)
        {
            var assemblyLoader = app.ApplicationServices.GetRequiredService<CustomAssemblyLoader>();
            foreach (var pack in this.PrerenderingOptions.MiddlewarePackages)
            {
                var assemblyName = string.IsNullOrEmpty(pack.Assembly) ? pack.PackageIdentity : pack.Assembly;
                var appAssembly = assemblyLoader.LoadAssembly(assemblyName);//.LoadFromAssemblyPath(appAssemblyPath);
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
