using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.WebAssembly.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders.Physical;
using Microsoft.JSInterop;
using Toolbelt.Blazor.WebAssembly.PreRendering.Build.WebHost.Services;

namespace Toolbelt.Blazor.WebAssembly.PreRendering.Build.WebHost;

internal static class ServerSideRenderingWebHost
{
    internal static async Task<IHost> StartWebHostAsync(ServerSideRenderingContext context)
    {
        var appsettingsPath = Path.Combine(context.WebRootPath, "appsettings.json");
        var appsettingsEnvironmentPath = Path.Combine(context.WebRootPath, $"appsettings.{context.HostEnvironment.Environment}.json");
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(appsettingsPath, optional: true)
            .AddJsonFile(appsettingsEnvironmentPath, optional: true)
            .Build();

        var hostBuilder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = context.HostEnvironment.Environment,
            ContentRootPath = Path.Combine(context.WebRootPath, "_framework"),
            WebRootPath = context.WebRootPath
        });

        hostBuilder.Services.ConfigureServices(context, configuration);
        hostBuilder.WebHost
            .UseConfiguration(configuration)
            .UseKestrel()
            .UseUrls(context.BaseAddress);
        var webHost = hostBuilder.Build();

        webHost.Configure(context);

        await webHost.StartAsync();

        Console.WriteLine($"Listen to {context.BaseAddress}/");
        return webHost;
    }

    private static void ConfigureServices(this IServiceCollection services, ServerSideRenderingContext context, IConfigurationRoot configuration)
    {
        services.AddSingleton(context.HostEnvironment);
        services.AddSingleton(context); // Pre-rendering context is used from _Host.cshtml and _Layout.cshtml via DI container.
        services.AddSingleton(context.AssemblyLoader);

        services.ConfigureApplicationServices(context, configuration);

        services.TryAddScoped(sp => new HttpClient { BaseAddress = new Uri(context.BaseAddress) });

        services.AddRazorPages();
        services.AddServerSideBlazor();
        services.AddLocalization();
        services.TryAddScoped<LazyAssemblyLoader>();

        var jsruntimeDescriptor = services.FirstOrDefault(sd => sd.ServiceType == typeof(IJSRuntime));
        if (jsruntimeDescriptor != null) services.Remove(jsruntimeDescriptor);
        services.AddScoped<IJSRuntime, ServerSideRenderingJSRuntime>();
    }

    private static void ConfigureApplicationServices(this IServiceCollection services, ServerSideRenderingContext context, IConfigurationRoot configuration)
    {
        var programClass = context.ApplicationAssembly
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
                arguments.Add(configuration);
            }
            else if (methodParameter.ParameterType == typeof(string) && string.Equals(methodParameter.Name, "baseAddress", StringComparison.InvariantCultureIgnoreCase))
            {
                arguments.Add(context.BaseAddress.ToString().TrimEnd('/'));
            }
            else if (methodParameter.ParameterType == typeof(Uri))
            {
                arguments.Add(context.BaseAddress);
            }
            else if (methodParameter.ParameterType == typeof(IWebAssemblyHostEnvironment))
            {
                arguments.Add(context.HostEnvironment);
            }
            else
            {
                arguments.Add(methodParameter.DefaultValue);
            }
        }

        configureServicesMethod.Invoke(null, arguments.ToArray());
    }

    private static void Configure(this IApplicationBuilder app, ServerSideRenderingContext context)
    {
        app.UseDeveloperExceptionPage();

        ConfigureApplicationMiddleware(app, context);

        if (context.Locales.Any())
        {
            app.UseRequestLocalization(new RequestLocalizationOptions()
                .AddSupportedCultures(context.Locales)
                .AddSupportedUICultures(context.Locales));
        }

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(context.WebRootPath, ExclusionFilters.None),
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
                var webHost = app.ApplicationServices.GetRequiredService<IHost>();
                Task.Delay(100).ConfigureAwait(false).GetAwaiter().OnCompleted(() =>
                {
                    webHost.StopAsync().ConfigureAwait(false);
                });
                return Task.CompletedTask;
            });

            MapAuthMe(endpoints, context);
            endpoints.MapRazorPages();
            endpoints.MapFallbackToPage(pattern: "/{*catch-all}", "/_Host");
        });
    }

    internal static void ConfigureApplicationMiddleware(IApplicationBuilder app, ServerSideRenderingContext context)
    {
        var assemblyLoader = context.AssemblyLoader;
        foreach (var pack in context.MiddlewarePackages)
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

    private static void MapAuthMe(IEndpointRouteBuilder endpoints, ServerSideRenderingContext context)
    {
        if (context.EmulateAuthMe)
        {
            endpoints.MapGet("/.auth/me", async context =>
            {
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"clientPrincipal\":null}");
            });
        }
    }

}
