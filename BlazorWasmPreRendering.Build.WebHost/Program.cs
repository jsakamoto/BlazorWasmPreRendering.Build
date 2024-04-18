using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Toolbelt.Blazor.WebAssembly.PreRendering.Build.Shared;

namespace Toolbelt.Blazor.WebAssembly.PreRendering.Build.WebHost
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var options = new ServerSideRenderingOptions();
            new ConfigurationBuilder()
                .AddEnvironmentVariables(prefix: Constants.ConfigurationPrefix)
                .AddCommandLine(args)
                .Build()
                .Bind(options);

            var context = BuildPrerenderingContext(options);

            var webHost = await ServerSideRenderingWebHost.StartWebHostAsync(context);
            await webHost.WaitForShutdownAsync();
        }

        internal static ServerSideRenderingContext BuildPrerenderingContext(ServerSideRenderingOptions options)
        {
            if (string.IsNullOrEmpty(options.WebRootPath)) throw new ArgumentException("The WebRootPath parameter is required.");
            if (string.IsNullOrEmpty(options.AssemblyDir)) throw new ArgumentException("The AssemblyDir parameter is required.");
            if (options.MiddlewareDllsDir == null) throw new ArgumentException("The MiddlewareDllsDir parameter is required.");
            if (string.IsNullOrEmpty(options.AssemblyName)) throw new ArgumentException("The AssemblyName parameter is required.");
            if (string.IsNullOrEmpty(options.RootComponentTypeName)) throw new ArgumentException("The RootComponentTypeName parameter is required.");
            if (options.IndexHtmlFragments == null) throw new ArgumentException("The IndexHtmlFragments parameter is required.");
            if (options.MiddlewarePackages == null) throw new ArgumentException("The MiddlewarePackages parameter is required.");

            var assemblyLoader = SetupCustomAssemblyLoader(options.WebRootPath, options.AssemblyDir, options.MiddlewareDllsDir, options.BWAPOptionsDllExt);

            var appAssembly = assemblyLoader.LoadAssembly(options.AssemblyName);
            if (appAssembly == null) throw new ArgumentException($"The application assembly \"{options.AssemblyName}\" colud not load.");

            var rootComponentType = GetRootComponentType(assemblyLoader, options.RootComponentTypeName, appAssembly);

            var context = new ServerSideRenderingContext
            {
                AssemblyLoader = assemblyLoader,
                WebRootPath = options.WebRootPath,
                ApplicationAssembly = appAssembly,
                RootComponentType = rootComponentType,
                HeadOutletComponentType = typeof(Microsoft.AspNetCore.Components.Web.HeadOutlet),
                RenderMode = options.RenderMode,
                IndexHtmlFragments = options.IndexHtmlFragments,

                DeleteLoadingContents = options.DeleteLoadingContents,
                MiddlewarePackages = options.MiddlewarePackages,

                Environment = options.Environment,
                EmulateAuthMe = options.EmulateAuthMe,
                Locales = options.Locales.ToArray(),
                ServerPort = options.ServerPort
            };
            return context;
        }

        private static Type GetRootComponentType(CustomAssemblyLoader assemblyLoader, string typeNameOfRootComponent, Assembly appAssembly)
        {
            var rootComponentAssembly = appAssembly;

            var rootComponentTypeNameParts = typeNameOfRootComponent.Split(',');
            var rootComponentTypeName = rootComponentTypeNameParts[0].Trim();
            var rootComponentAssemblyName = rootComponentTypeNameParts.Length > 1 ? rootComponentTypeNameParts[1].Trim() : "";
            if (rootComponentAssemblyName != "")
            {
                rootComponentAssembly = assemblyLoader.LoadAssembly(rootComponentAssemblyName);
                if (rootComponentAssembly == null) throw new ArgumentException($"The assembly that has component type \"{typeNameOfRootComponent}\" colud not load.");
            }

            var appComponentType = rootComponentAssembly.GetType(rootComponentTypeName);

            if (appComponentType == null)
            {
                var assemblies = appAssembly.GetReferencedAssemblies()
                    .Where(asmname => !string.IsNullOrEmpty(asmname.Name))
                    .Where(asmname => !asmname.Name!.StartsWith("Microsoft."))
                    .Where(asmname => !asmname.Name!.StartsWith("System."))
                    .Select(asmname => assemblyLoader.LoadAssembly(asmname.Name!))
                    .Where(asm => asm != null)
                    .Prepend(appAssembly) as IEnumerable<Assembly>;

                appComponentType = assemblies
                    .SelectMany(asm => asm.GetTypes())
                    .Where(t => t.Name == "App")
                    .Where(t => t.IsSubclassOf(typeof(ComponentBase)))
                    .FirstOrDefault();
            }

            if (appComponentType == null) throw new ArgumentException($"The component type \"{typeNameOfRootComponent}\" was not found.");
            return appComponentType;
        }

        private static CustomAssemblyLoader SetupCustomAssemblyLoader(string webRootPath, string assemblyDir, string middlewareDllsDir, string? secondaryDllExt)
        {
            var xorKey = "bwap";
            var avpSettingsPath = Path.Combine(webRootPath, "avp-settings.json");
            if (File.Exists(avpSettingsPath))
            {
                var avpSettingsJson = File.ReadAllText(avpSettingsPath);
                var avpSettings = JsonSerializer.Deserialize<AntiVirusProtectionSettings>(avpSettingsJson);
                xorKey = avpSettings?.xorKey ?? xorKey;
            }

            var assemblyLoader = new CustomAssemblyLoader(xorKey: xorKey, secondaryDllExt: secondaryDllExt);

            assemblyLoader.AddSerachDir(assemblyDir);

            if (!string.IsNullOrEmpty(middlewareDllsDir))
                assemblyLoader.AddSerachDir(middlewareDllsDir);

            return assemblyLoader;
        }
    }
}