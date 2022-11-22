using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.WebAssembly.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders.Physical;
using Microsoft.Extensions.Hosting;
using Microsoft.JSInterop;
using Toolbelt.Blazor.WebAssembly.PreRendering.Build.WebHost.Services;

namespace Toolbelt.Blazor.WebAssembly.PreRendering.Build.WebHost
{
    internal class Startup
    {
        private IConfiguration Configuration { get; }

        private Uri BaseAddress { get; }

        private IWebAssemblyHostEnvironment HostEnvironment { get; }

        private ServerSideRenderingContext PrerenderingContext { get; }

        public Startup(IConfiguration configuration, Uri baseAddress, IWebAssemblyHostEnvironment hostEnvironment, ServerSideRenderingContext prerenderingContext)
        {
            this.Configuration = configuration;
            this.BaseAddress = baseAddress;
            this.HostEnvironment = hostEnvironment;
            this.PrerenderingContext = prerenderingContext;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(this.HostEnvironment);
            services.AddSingleton(this.PrerenderingContext); // Pre-rendering context is used from _Host.cshtml and _Layout.cshtml via DI container.
            services.AddSingleton(this.PrerenderingContext.AssemblyLoader);

            this.ConfigureApplicationServices(services);

            services.TryAddScoped(sp => new HttpClient { BaseAddress = this.BaseAddress });

            services.AddRazorPages();
            services.AddServerSideBlazor();
            services.AddLocalization();
            services.AddSingleton<ResetHeadOutletScript>();
            services.TryAddScoped<LazyAssemblyLoader>();

            var jsruntimeDescriptor = services.FirstOrDefault(sd => sd.ServiceType == typeof(IJSRuntime));
            if (jsruntimeDescriptor != null) services.Remove(jsruntimeDescriptor);
            services.AddScoped<IJSRuntime, ServerSideRenderingJSRuntime>();
        }

        private void ConfigureApplicationServices(IServiceCollection services)
        {
            var programClass = this.PrerenderingContext.ApplicationAssembly
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

            if (this.PrerenderingContext.Locales.Any())
            {
                app.UseRequestLocalization(new RequestLocalizationOptions()
                    .AddSupportedCultures(this.PrerenderingContext.Locales)
                    .AddSupportedUICultures(this.PrerenderingContext.Locales));
            }

            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(env.WebRootPath, ExclusionFilters.None),
                ServeUnknownFileTypes = true
            });
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                // NOTICE: This is the back door of this pre-rendering server for the unit test purpose. 
                // When this server receives an "HTTP DELETE /" request, the server will be shut down even if the "KeepRunning" option was enabled.
                // This is important to certainly terminate this server during the clean-up process of the unit test.
                endpoints.MapDelete("/", context =>
                {
                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    var webHost = app.ApplicationServices.GetRequiredService<IWebHost>();
                    Task.Delay(100).ConfigureAwait(false).GetAwaiter().OnCompleted(() =>
                    {
                        webHost.StopAsync().ConfigureAwait(false);
                    });
                    return Task.CompletedTask;
                });

                this.MapAuthMe(endpoints);
                endpoints.MapRazorPages();
                endpoints.MapFallbackToPage(pattern: "/{*catch-all}", "/_Host");
            });
        }

        internal void ConfigureApplicationMiddleware(IApplicationBuilder app)
        {
            var assemblyLoader = this.PrerenderingContext.AssemblyLoader;
            foreach (var pack in this.PrerenderingContext.MiddlewarePackages)
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

        private void MapAuthMe(IEndpointRouteBuilder endpoints)
        {
            if (this.PrerenderingContext.EmulateAuthMe)
            {
                endpoints.MapGet("/.auth/me", async context =>
                {
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync("{\"clientPrincipal\":null}");
                });
            }
        }
    }
}
