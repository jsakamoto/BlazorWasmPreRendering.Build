using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml.Linq;
using AngleSharp.Html.Parser;
using CommandLineSwitchParser;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Hosting;

namespace Toolbelt.Blazor.WebAssembly.PrerenderServer
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var commandLineOptions = CommandLineSwitch.Parse<CommandLineOptions>(ref args);
            var assemblyLoader = new CustomAssemblyLoader();

            var prerenderingOptions = BuildPrerenderingOptions(assemblyLoader, commandLineOptions);

            SetupCustomAssemblyLoader(assemblyLoader, prerenderingOptions);

            using var webHost = await StartWebHostAsync(prerenderingOptions);
            var serverAddresses = webHost.ServerFeatures.Get<IServerAddressesFeature>()!;
            var baseUrl = serverAddresses.Addresses.First();

            Console.WriteLine("Start fetching...");

            var crawler = new StaticlizeCrawler(
                baseUrl,
                prerenderingOptions.WebRootPath,
                prerenderingOptions.EnableGZipCompression,
                prerenderingOptions.EnableBrotliCompression);
            await crawler.SaveToStaticFileAsync();

            Console.WriteLine("Fetching complete.");

            if (!commandLineOptions.KeepRunning) await webHost.StopAsync();

            await webHost.WaitForShutdownAsync();
            return 0;
        }

        internal static BlazorWasmPrerenderingOptions BuildPrerenderingOptions(CustomAssemblyLoader assemblyLoader, CommandLineOptions commandLineOptions)
        {
            if (string.IsNullOrEmpty(commandLineOptions.IntermediateDir)) throw new ArgumentException("The -i|--intermediatedir parameter is required.");
            if (string.IsNullOrEmpty(commandLineOptions.PublishedDir)) throw new ArgumentException("The -p|--publisheddir parameter is required.");
            if (string.IsNullOrEmpty(commandLineOptions.AssemblyName)) throw new ArgumentException("The -a|--assemblyname parameter is required.");
            if (string.IsNullOrEmpty(commandLineOptions.TypeNameOfRootComponent)) throw new ArgumentException("The -t|--typenameofrootcomponent parameter is required.");
            if (string.IsNullOrEmpty(commandLineOptions.SelectorOfRootComponent)) throw new ArgumentException("The -s|--selectorofrootcomponent parameter is required.");
            if (string.IsNullOrEmpty(commandLineOptions.FrameworkName)) throw new ArgumentException("The -f|--frameworkname parameter is required.");

            var webRootPath = Path.Combine(commandLineOptions.PublishedDir, "wwwroot");
            var indexHtmlPath = Path.Combine(webRootPath, "index.html");
            var appAssemblyDir = Path.Combine(webRootPath, "_framework");
            assemblyLoader.AddSerachDir(appAssemblyDir);

            var enableGZipCompression = File.Exists(indexHtmlPath + ".gz");
            var enableBrotliCompression = File.Exists(indexHtmlPath + ".br");

            var indexHtmlText = GetIndexHtmlText(indexHtmlPath, commandLineOptions.SelectorOfRootComponent);
            var appAssembly = assemblyLoader.LoadAssembly(commandLineOptions.AssemblyName);
            if (appAssembly == null) throw new ArgumentException($"The application assembly \"{commandLineOptions.AssemblyName}\" colud not load.");
            var appComponentType = GetAppComponentType(assemblyLoader, commandLineOptions.TypeNameOfRootComponent, appAssembly);

            var middlewarePackages = Enumerable.Empty<MiddlewarePackageReference>();
            if (!string.IsNullOrEmpty(commandLineOptions.MiddlewarePackages))
            {
                middlewarePackages = commandLineOptions.MiddlewarePackages
                    .Split(';')
                    .Select(pack => pack.Split(','))
                    .Select(parts => new MiddlewarePackageReference
                    {
                        PackageIdentity = parts.First(),
                        Assembly = parts.Skip(1).FirstOrDefault() ?? "",
                        Version = parts.Skip(2).FirstOrDefault() ?? ""
                    })
                    .ToArray();
            }

            var options = new BlazorWasmPrerenderingOptions
            {
                IntermediateDir = commandLineOptions.IntermediateDir,
                FrameworkName = commandLineOptions.FrameworkName,
                IndexHtmlTextFirstHalf = indexHtmlText.FirstHalf,
                IndexHtmlTextSecondHalf = indexHtmlText.SecondHalf,
                WebRootPath = webRootPath,
                ApplicationAssembly = appAssembly,
                RootComponentType = appComponentType,
                EnableGZipCompression = enableGZipCompression,
                EnableBrotliCompression = enableBrotliCompression,
                MiddlewarePackages = middlewarePackages
            };
            return options;
        }

        internal static (string FirstHalf, string SecondHalf) GetIndexHtmlText(string indexHtmlPath, string selectorOfRootComponent)
        {
            var indexHtmlText = File.ReadAllText(indexHtmlPath);
            indexHtmlText = indexHtmlText.Replace("\r\n", "\n");

            const string preRenderMarkerBegin = "\n<!-- %%-PRERENDERING-BEGIN-%% -->\n";
            const string preRenderMarkerEnd = "\n<!-- %%-PRERENDERING-END-%% -->\n";
            var indexOfPreRenderMarkerBegin = indexHtmlText.IndexOf(preRenderMarkerBegin);
            var indexOfPreRenderMarkerEnd = indexHtmlText.IndexOf(preRenderMarkerEnd);
            if (indexOfPreRenderMarkerBegin != -1 && indexOfPreRenderMarkerEnd != -1)
            {
                indexHtmlText =
                    indexHtmlText[0..indexOfPreRenderMarkerBegin] +
                    indexHtmlText[(indexOfPreRenderMarkerEnd + preRenderMarkerEnd.Length)..];
            }

            const string markerText = "%%-INSERT-PRERENDERING-HERE-%%";
            const string markerComment = "<!--" + markerText + "-->";
            var parser = new HtmlParser();
            var indexHtmlDoc = parser.ParseDocument(indexHtmlText);
            var appRootComponentElement = indexHtmlDoc.QuerySelector(selectorOfRootComponent);
            appRootComponentElement.AppendChild(indexHtmlDoc.CreateComment(markerText));

            using var stringWriter = new StringWriter();
            indexHtmlDoc.ToHtml(stringWriter, new CustomHtmlMarkupFormatter());
            indexHtmlText = stringWriter.ToString();

            var indexOfMarker = indexHtmlText.IndexOf(markerComment);
            var indexHtmlTextFirstHalf = indexHtmlText[0..indexOfMarker];
            var indexHtmlTextSecondHalf = indexHtmlText[(indexOfMarker + markerComment.Length)..];

            return (indexHtmlTextFirstHalf, indexHtmlTextSecondHalf);
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

        private static void SetupCustomAssemblyLoader(CustomAssemblyLoader assemblyLoader, BlazorWasmPrerenderingOptions options)
        {
            var projectDir = GenerateProjectToGetMiddleware(options);
            if (projectDir == null) return;

            var middlewareDllsDir = GetMiddlewareDlls(projectDir, options.FrameworkName);
            assemblyLoader.AddSerachDir(middlewareDllsDir);
        }

        internal static string? GenerateProjectToGetMiddleware(BlazorWasmPrerenderingOptions option)
        {
            if (!option.MiddlewarePackages.Any()) return null;

            var projectFileDir = Path.Combine(option.IntermediateDir, "BlazorWasmPrerendering", "Middleware");
            if (!Directory.Exists(projectFileDir)) Directory.CreateDirectory(projectFileDir);
            var projectFilePath = Path.Combine(projectFileDir, "Project.csproj");

            var project = new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"));

            var propertyGroup = new XElement("PropertyGroup",
                new XElement("TargetFramework", option.FrameworkName),
                new XElement("CopyLocalLockFileAssemblies", "true"));

            var itemGroup = new XElement("ItemGroup");
            foreach (var package in option.MiddlewarePackages)
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

        private static async Task<IWebHost> StartWebHostAsync(BlazorWasmPrerenderingOptions prerenderingOptions)
        {
            var hostBuilder = new WebHostBuilder()
                .UseKestrel()
                .UseUrls("http://127.0.0.1:5050")
                .UseWebRoot(prerenderingOptions.WebRootPath)
                .UseStartup(context => new Startup(context.Configuration, prerenderingOptions));
            var webHost = hostBuilder.Build();
            await webHost.StartAsync();
            return webHost;
        }
    }
}