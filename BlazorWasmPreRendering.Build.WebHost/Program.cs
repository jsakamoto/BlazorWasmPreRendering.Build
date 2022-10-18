using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;

namespace Toolbelt.Blazor.WebAssembly.PreRendering.Build.WebHost
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var commandLineOptions = new CommandLineOptions();
            var configuration = new ConfigurationBuilder().AddCommandLine(args).Build();
            configuration.Bind(commandLineOptions);

            var context = BuildPrerenderingContext(commandLineOptions);

            await ServerSideRenderingWebHost.StartWebHostAsync(context);
        }

        private static ServerSideRenderingContext BuildPrerenderingContext(CommandLineOptions commandLineOptions)
        {
            if (string.IsNullOrEmpty(commandLineOptions.WebRootPath)) throw new ArgumentException("The WebRootPath parameter is required.");
            if (string.IsNullOrEmpty(commandLineOptions.MiddlewareDllsDir)) throw new ArgumentException("The MiddlewareDllsDir parameter is required.");
            if (string.IsNullOrEmpty(commandLineOptions.AssemblyName)) throw new ArgumentException("The --assembly-name parameter is required.");
            if (string.IsNullOrEmpty(commandLineOptions.RootComponentTypeName)) throw new ArgumentException("The --root-component-type-name parameter is required.");
            if (commandLineOptions.IndexHtmlFragments == null) throw new ArgumentException("The IndexHtmlFragments parameter is required.");
            if (commandLineOptions.MiddlewarePackages == null) throw new ArgumentException("The MiddlewarePackages parameter is required.");

            var assemblyLoader = SetupCustomAssemblyLoader(commandLineOptions.WebRootPath, commandLineOptions.MiddlewareDllsDir);

            var appAssembly = assemblyLoader.LoadAssembly(commandLineOptions.AssemblyName);
            if (appAssembly == null) throw new ArgumentException($"The application assembly \"{commandLineOptions.AssemblyName}\" colud not load.");

            var rootComponentType = GetRootComponentType(assemblyLoader, commandLineOptions.RootComponentTypeName, appAssembly);

            var options = new ServerSideRenderingContext
            {
                AssemblyLoader = assemblyLoader,
                WebRootPath = commandLineOptions.WebRootPath,
                ApplicationAssembly = appAssembly,
                RootComponentType = rootComponentType,

#if ENABLE_HEADOUTLET
                HeadOutletComponentType = typeof(Microsoft.AspNetCore.Components.Web.HeadOutlet),
#endif
                RenderMode = commandLineOptions.RenderMode,
                IndexHtmlFragments = commandLineOptions.IndexHtmlFragments,

                DeleteLoadingContents = commandLineOptions.DeleteLoadingContents,
                MiddlewarePackages = commandLineOptions.MiddlewarePackages,

                Environment = commandLineOptions.Environment,
                ServerPort = commandLineOptions.ServerPort
            };
            return options;
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

        private static CustomAssemblyLoader SetupCustomAssemblyLoader(string webRootPath, string middlewareDllsDir)
        {
            var assemblyLoader = new CustomAssemblyLoader();

            var appAssemblyDir = Path.Combine(webRootPath, "_framework");
            assemblyLoader.AddSerachDir(appAssemblyDir);

            if (!string.IsNullOrEmpty(middlewareDllsDir))
                assemblyLoader.AddSerachDir(middlewareDllsDir);

            return assemblyLoader;
        }
    }
}