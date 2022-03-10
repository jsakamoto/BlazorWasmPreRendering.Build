using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml.Linq;
using CommandLineSwitchParser;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Toolbelt.Blazor.WebAssembly.PrerenderServer.WebHost;

namespace Toolbelt.Blazor.WebAssembly.PrerenderServer
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var commandLineOptions = CommandLineSwitch.Parse<CommandLineOptions>(ref args, options => options.EnumParserStyle = EnumParserStyle.OriginalCase);
            var assemblyLoader = new CustomAssemblyLoader();
            var prerenderingOptions = BuildPrerenderingOptions(assemblyLoader, commandLineOptions);

            var crawlingResult = await PreRenderToStaticFilesAsync(commandLineOptions, assemblyLoader, prerenderingOptions);
            return crawlingResult.HasFlag(StaticlizeCrawlingResult.HasErrors) ? 1 : 0;
        }

        private static async Task<StaticlizeCrawlingResult> PreRenderToStaticFilesAsync(CommandLineOptions commandLineOptions, CustomAssemblyLoader assemblyLoader, BlazorWasmPrerenderingOptions prerenderingOptions)
        {
            using var webHost = await ServerSideRenderingWebHost.StartWebHostAsync(
                assemblyLoader,
                commandLineOptions.Environment,
                prerenderingOptions);
            var server = webHost.Services.GetRequiredService<IServer>();
            var baseAddresses = server.Features.Get<IServerAddressesFeature>()!.Addresses;
            var baseUrl = baseAddresses.First();

            Console.WriteLine($"Start fetching...[{baseUrl}]");

            var crawler = new StaticlizeCrawler(
                baseUrl,
                commandLineOptions.UrlPathToExplicitFetch,
                prerenderingOptions.WebRootPath,
                commandLineOptions.OutputStyle,
                prerenderingOptions.EnableGZipCompression,
                prerenderingOptions.EnableBrotliCompression);
            var crawlingResult = await crawler.SaveToStaticFileAsync();


            if (crawlingResult != StaticlizeCrawlingResult.Nothing && !commandLineOptions.KeepRunning)
            {
                Console.WriteLine();
                Console.WriteLine("INFORMATION");
                Console.WriteLine("===========");
                Console.WriteLine("The crawler encountered errors and/or warnings.");
                Console.WriteLine("If you want to keep running the pre-rendering server process for debugging it on live, you can do that by setting the \"BlazorWasmPrerenderingKeepServer\" MSBuild property to \"true\".");
                Console.WriteLine();
                Console.WriteLine("ex) dotnet publish -p:BlazorWasmPrerenderingKeepServer=true");
                Console.WriteLine();
            }

            Console.WriteLine("Fetching complete.");

            await ServiceWorkerAssetsManifest.UpdateAsync(
                prerenderingOptions.WebRootPath,
                commandLineOptions.ServiceWorkerAssetsManifest);

            if (commandLineOptions.KeepRunning)
            {
                Console.WriteLine();
                Console.WriteLine("The pre-rendering server will keep running because the \"-k\" option switch is specified.");
                Console.WriteLine("To stop the pre - rendering server and stop build, press Ctrl + C.");
            }
            else await webHost.StopAsync();

            await webHost.WaitForShutdownAsync();

            return crawlingResult;
        }

        internal static BlazorWasmPrerenderingOptions BuildPrerenderingOptions(CustomAssemblyLoader assemblyLoader, CommandLineOptions commandLineOptions)
        {
            if (string.IsNullOrEmpty(commandLineOptions.IntermediateDir)) throw new ArgumentException("The -i|--intermediatedir parameter is required.");
            if (string.IsNullOrEmpty(commandLineOptions.PublishedDir)) throw new ArgumentException("The -p|--publisheddir parameter is required.");
            if (string.IsNullOrEmpty(commandLineOptions.AssemblyName)) throw new ArgumentException("The -a|--assemblyname parameter is required.");
            if (string.IsNullOrEmpty(commandLineOptions.TypeNameOfRootComponent)) throw new ArgumentException("The -t|--typenameofrootcomponent parameter is required.");
            if (string.IsNullOrEmpty(commandLineOptions.SelectorOfRootComponent)) throw new ArgumentException("The --selectorofrootcomponent parameter is required.");
#if ENABLE_HEADOUTLET
            if (string.IsNullOrEmpty(commandLineOptions.SelectorOfHeadOutletComponent)) throw new ArgumentException("The --selectorofheadoutletcomponent parameter is required.");
#endif
            if (string.IsNullOrEmpty(commandLineOptions.FrameworkName)) throw new ArgumentException("The -f|--frameworkname parameter is required.");

            var webRootPath = Path.Combine(commandLineOptions.PublishedDir, "wwwroot");

            var middlewarePackages = MiddlewarePackageReference.Parse(commandLineOptions.MiddlewarePackages);
            var middlewareDllsDir = PrepareMiddlewareDlls(middlewarePackages, commandLineOptions.IntermediateDir, commandLineOptions.FrameworkName);
            SetupCustomAssemblyLoader(assemblyLoader, webRootPath, middlewareDllsDir);

            var appAssembly = assemblyLoader.LoadAssembly(commandLineOptions.AssemblyName);
            if (appAssembly == null) throw new ArgumentException($"The application assembly \"{commandLineOptions.AssemblyName}\" colud not load.");
            var appComponentType = GetAppComponentType(assemblyLoader, commandLineOptions.TypeNameOfRootComponent, appAssembly);

            var indexHtmlPath = Path.Combine(webRootPath, "index.html");
            var enableGZipCompression = File.Exists(indexHtmlPath + ".gz");
            var enableBrotliCompression = File.Exists(indexHtmlPath + ".br");

            var htmlFragment = IndexHtmlFragments.Load(indexHtmlPath, commandLineOptions.SelectorOfRootComponent, commandLineOptions.SelectorOfHeadOutletComponent, commandLineOptions.DeleteLoadingContents);
            var options = new BlazorWasmPrerenderingOptions
            {
                WebRootPath = webRootPath,
                ApplicationAssembly = appAssembly,

                RootComponentType = appComponentType,
#if ENABLE_HEADOUTLET
                HeadOutletComponentType = typeof(Microsoft.AspNetCore.Components.Web.HeadOutlet),
#endif
                IndexHtmlFragments = htmlFragment,
                DeleteLoadingContents = commandLineOptions.DeleteLoadingContents,

                EnableGZipCompression = enableGZipCompression,
                EnableBrotliCompression = enableBrotliCompression,
                MiddlewarePackages = middlewarePackages
            };
            return options;
        }

        private static Type GetAppComponentType(CustomAssemblyLoader assemblyLoader, string typeNameOfRootComponent, Assembly appAssembly)
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

        private static void SetupCustomAssemblyLoader(CustomAssemblyLoader assemblyLoader, string webRootPath, string middlewareDllsDir)
        {
            var appAssemblyDir = Path.Combine(webRootPath, "_framework");
            assemblyLoader.AddSerachDir(appAssemblyDir);

            if (!string.IsNullOrEmpty(middlewareDllsDir))
                assemblyLoader.AddSerachDir(middlewareDllsDir);
        }

        private static string PrepareMiddlewareDlls(IEnumerable<MiddlewarePackageReference> middlewarePackages, string intermediateDir, string frameworkName)
        {
            var projectDir = GenerateProjectToGetMiddleware(middlewarePackages, intermediateDir, frameworkName);
            if (projectDir == null) return "";

            var middlewareDllsDir = GetMiddlewareDlls(projectDir, frameworkName);
            return middlewareDllsDir;
        }

        internal static string? GenerateProjectToGetMiddleware(IEnumerable<MiddlewarePackageReference> middlewarePackages, string intermediateDir, string frameworkName)
        {
            if (!middlewarePackages.Any()) return null;

            var projectFileDir = Path.Combine(intermediateDir, "BlazorWasmPrerendering", "Middleware");
            if (!Directory.Exists(projectFileDir)) Directory.CreateDirectory(projectFileDir);
            var projectFilePath = Path.Combine(projectFileDir, "Project.csproj");

            var project = new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"));

            var propertyGroup = new XElement("PropertyGroup",
                new XElement("TargetFramework", frameworkName),
                new XElement("CopyLocalLockFileAssemblies", "true"));

            var itemGroup = new XElement("ItemGroup");
            foreach (var package in middlewarePackages)
            {
                var packageRef = new XElement("PackageReference", new XAttribute("Include", package.PackageIdentity));
                if (!string.IsNullOrEmpty(package.Version)) packageRef.Add(new XAttribute("Version", package.Version));
                itemGroup.Add(packageRef);
            }

            project.Add(propertyGroup, itemGroup);

            var xdoc = new XDocument(project);
            xdoc.Save(projectFilePath);

            return projectFileDir;
        }

        internal static string GetMiddlewareDlls(string projectDir, string frameworkName)
        {
            var binDir = Path.Combine(projectDir, "bin");
            var objDir = Path.Combine(projectDir, "obj");
            foreach (var dir in new[] { binDir, objDir }.Where(d => Directory.Exists(d))) Directory.Delete(dir, recursive: true);
            try
            {
                using var buildProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = "dotnet",
                    ArgumentList = { "build", "-c:Release", "-v:q", "--nologo" },
                    WorkingDirectory = projectDir
                });
                if (buildProcess == null) throw new Exception("Starting \"dotnet build\" for retreive middle ware dlls was failed.");

                buildProcess.WaitForExit();
                if (buildProcess.ExitCode != 0) throw new Exception($"The exit code of \"dotnet build\" for retreive middle ware dlls was {buildProcess.ExitCode}");
            }
            finally
            {
                if (Directory.Exists(objDir)) Directory.Delete(objDir, recursive: true);
            }

            return Path.Combine(projectDir, "bin", "Release", frameworkName);
        }
    }
}
