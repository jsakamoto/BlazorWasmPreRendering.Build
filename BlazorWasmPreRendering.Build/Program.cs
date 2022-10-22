using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml.Linq;
using CommandLineSwitchParser;
using Microsoft.AspNetCore.Mvc.Rendering;
using Toolbelt.Blazor.WebAssembly.PreRendering.Build.Shared;
using Toolbelt.Blazor.WebAssembly.PrerenderServer.Internal;
using Toolbelt.Blazor.WebAssembly.PrerenderServer.Internal.Services.Logger;
using Toolbelt.Diagnostics;

namespace Toolbelt.Blazor.WebAssembly.PrerenderServer
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var commandLineOptions = CommandLineSwitch.Parse<CommandLineOptions>(ref args, options => options.EnumParserStyle = EnumParserStyle.OriginalCase);
            var prerenderingOptions = BuildPrerenderingOptions(commandLineOptions);

            var crawlingResult = await PreRenderToStaticFilesAsync(commandLineOptions, prerenderingOptions);
            return crawlingResult.HasFlag(StaticlizeCrawlingResult.HasErrors) ? 1 : 0;
        }

        private static async Task<StaticlizeCrawlingResult> PreRenderToStaticFilesAsync(CommandLineOptions commandLineOptions, BlazorWasmPrerenderingOptions prerenderingOptions)
        {
            var serverPort = GetAvailableTcpPort(commandLineOptions.ServerPort);
            var baseUrl = $"http://127.0.0.1:{serverPort}";

            using var webHostProcess = await StartWebHostAsync(commandLineOptions, prerenderingOptions, serverPort, baseUrl);
            if (webHostProcess.Process.HasExited)
            {
                Console.WriteLine(webHostProcess.Output);
                ReportErrorsOfCrawling(StaticlizeCrawlingResult.HasErrors, commandLineOptions.KeepRunning);
                return StaticlizeCrawlingResult.HasErrors;
            }

            Console.WriteLine($"Start fetching...[{baseUrl}]");

            var crawler = new StaticlizeCrawler(
                baseUrl,
                commandLineOptions.UrlPathToExplicitFetch,
                prerenderingOptions.WebRootPath,
                commandLineOptions.OutputStyle,
                prerenderingOptions.EnableGZipCompression,
                prerenderingOptions.EnableBrotliCompression,
                new TinyConsoleLogger());
            var crawlingResult = await crawler.SaveToStaticFileAsync();

            if (crawlingResult != StaticlizeCrawlingResult.Nothing)
            {
                ReportErrorsOfCrawling(crawlingResult, commandLineOptions.KeepRunning);
            }

            Console.WriteLine("Fetching complete.");

            await ServiceWorkerAssetsManifest.UpdateAsync(
                prerenderingOptions.WebRootPath,
                commandLineOptions.ServiceWorkerAssetsManifest,
                crawler.StaticalizedFiles);

            if (commandLineOptions.KeepRunning)
            {
                Console.WriteLine();
                Console.WriteLine("The pre-rendering server will keep running because the \"-k\" option switch is specified.");
                Console.WriteLine("To stop the pre - rendering server and stop build, press Ctrl + C.");

                ConsoleCancelEventHandler cancelKeyHandler = (object? sender, ConsoleCancelEventArgs args) => CancelKeyHandler(webHostProcess, baseUrl);
                Console.CancelKeyPress += cancelKeyHandler;
                await webHostProcess.WaitForExitAsync();
                Console.CancelKeyPress -= cancelKeyHandler;
            }

            await StopWebHostAsync(webHostProcess, baseUrl);

            return crawlingResult;
        }

        internal static BlazorWasmPrerenderingOptions BuildPrerenderingOptions(CommandLineOptions commandLineOptions)
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
            if (commandLineOptions.RenderMode != RenderMode.Static && commandLineOptions.RenderMode != RenderMode.WebAssemblyPrerendered)
                throw new ArgumentException($"The -r|--rendermode parameter value \"{commandLineOptions.RenderMode}\" is not supported. (Only \"Static\" and \"WebAssemblyPrerendered\" are supported.)");

            var webRootPath = Path.Combine(commandLineOptions.PublishedDir, "wwwroot");
            var frameworkDir = Path.Combine(webRootPath, "_framework");

            var middlewarePackages = MiddlewarePackageReferenceBuilder.Build(folderToScan: frameworkDir, commandLineOptions.MiddlewarePackages);
            var middlewareDllsDir = PrepareMiddlewareDlls(middlewarePackages, commandLineOptions.IntermediateDir, commandLineOptions.FrameworkName);

            var indexHtmlPath = Path.Combine(webRootPath, "index.html");
            var enableGZipCompression = File.Exists(indexHtmlPath + ".gz");
            var enableBrotliCompression = File.Exists(indexHtmlPath + ".br");

            var htmlFragment = IndexHtmlParser.Parse(indexHtmlPath, commandLineOptions.SelectorOfRootComponent, commandLineOptions.SelectorOfHeadOutletComponent, commandLineOptions.DeleteLoadingContents);

            var options = new BlazorWasmPrerenderingOptions
            {
                WebRootPath = webRootPath,
                RenderMode = commandLineOptions.RenderMode,
                IndexHtmlFragments = htmlFragment,
                DeleteLoadingContents = commandLineOptions.DeleteLoadingContents,

                EnableGZipCompression = enableGZipCompression,
                EnableBrotliCompression = enableBrotliCompression,
                MiddlewarePackages = middlewarePackages.ToList(),
                MiddlewareDllsDir = middlewareDllsDir,
            };
            return options;
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

        internal static int GetAvailableTcpPort(string tcpPortRangeText)
        {
            var usedTcpPorts = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Select(listener => listener.Port).ToHashSet();
            var availabeTcpPort = IntEnumerator.ParseRangeText(tcpPortRangeText).FirstOrDefault(port => !usedTcpPorts.Contains(port));
            if (availabeTcpPort == 0) throw new Exception($"There is no avaliable TCP port in range \"{tcpPortRangeText}\".");
            return availabeTcpPort;
        }

        internal static void StoreOptionsToEnvironment(object obj, string prefix, IDictionary<string, string?> dictionary)
        {
            var props = obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in props)
            {
                var value = prop.GetValue(obj, null);
                if (value == null) continue;

                switch (Type.GetTypeCode(prop.PropertyType))
                {
                    case TypeCode.Object:
                        if (value is IEnumerable enumerable)
                        {
                            var index = 0;
                            foreach (var item in enumerable)
                            {
                                StoreOptionsToEnvironment(item, prefix + prop.Name + ":" + index + ":", dictionary);
                                index++;
                            }
                        }
                        else
                        {
                            StoreOptionsToEnvironment(value, prefix + prop.Name + ":", dictionary);
                        }
                        break;
                    default:
                        dictionary.Add(prefix + prop.Name, value.ToString());
                        break;
                }
            }
        }

        private static async ValueTask<XProcess> StartWebHostAsync(CommandLineOptions commandLineOptions, BlazorWasmPrerenderingOptions prerenderingOptions, int serverPort, string baseUrl)
        {
            var webHostDllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".webhost", "BlazorWasmPreRendering.Build.WebHost.dll");
            var webHostStartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                ArgumentList = { "exec", webHostDllPath },
                WorkingDirectory = prerenderingOptions.WebRootPath
            };

            var webHostOptions = new ServerSideRenderingOptions
            {
                WebRootPath = prerenderingOptions.WebRootPath,
                MiddlewareDllsDir = prerenderingOptions.MiddlewareDllsDir,
                MiddlewarePackages = prerenderingOptions.MiddlewarePackages,
                AssemblyName = commandLineOptions.AssemblyName,
                RootComponentTypeName = commandLineOptions.TypeNameOfRootComponent,
                RenderMode = commandLineOptions.RenderMode,
                IndexHtmlFragments = prerenderingOptions.IndexHtmlFragments,
                DeleteLoadingContents = prerenderingOptions.DeleteLoadingContents,
                Environment = commandLineOptions.Environment,
                ServerPort = serverPort
            };
            StoreOptionsToEnvironment(webHostOptions, Constants.ConfigurationPrefix, webHostStartInfo.Environment);

            var webHostProcess = XProcess.Start(webHostStartInfo);
            await webHostProcess.WaitForOutputAsync(predicate: output => output.Contains(baseUrl), millsecondsTimeout: 20000);
            return webHostProcess;
        }

        private static async ValueTask StopWebHostAsync(XProcess webHostProcess, string baseUrl)
        {
            if (webHostProcess.Process.HasExited) return;

            using var httpClient = new HttpClient();
            try
            {
                await httpClient.DeleteAsync(baseUrl);
            }
            catch (Exception e) { Console.WriteLine(e.ToString()); }

            for (var i = 0; i < 50; i++)
            {
                if (webHostProcess.Process.HasExited) break;
                await Task.Delay(100);
            }
            if (!webHostProcess.Process.HasExited) webHostProcess.Process.Kill();
            await webHostProcess.WaitForExitAsync();
        }

        private static void CancelKeyHandler(XProcess webHostProcess, string baseUrl)
        {
            var awaiter = StopWebHostAsync(webHostProcess, baseUrl).ConfigureAwait(false).GetAwaiter();
            awaiter.OnCompleted(() =>
            {
                try { awaiter.GetResult(); } catch (Exception e) { Console.WriteLine(e.ToString()); }
            });
        }

        private static void ReportErrorsOfCrawling(StaticlizeCrawlingResult crawlingResult, bool keepRunning)
        {
            Console.WriteLine();
            Console.WriteLine("INFORMATION");
            Console.WriteLine("=============================");
            Console.WriteLine("The crawler encountered errors and/or warnings.");

            if (crawlingResult.HasFlag(StaticlizeCrawlingResult.HasErrorsOfServiceNotRegistered))
            {
                Console.WriteLine();
                Console.WriteLine("\x1b[91mX\x1b[0m [ERROR] There is no registered service");
                Console.WriteLine("-----------------------------");
                Console.WriteLine("If the \"Program.cs\" of your Blazor WebAssembly app is like this:");
                Console.WriteLine("");
                Console.WriteLine("  var builder = WebAssemblyHostBuilder.CreateDefault(args);");
                Console.WriteLine("  ...");
                Console.WriteLine("  builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });");
                Console.WriteLine("  builder.Services.AddScoped<IFooService, FooService>();");
                Console.WriteLine("  ...");
                Console.WriteLine("");
                Console.WriteLine("Change the above code to extract service registration into the static method named \"ConfigureServices()\" like below:");
                Console.WriteLine("");
                Console.WriteLine("  var builder = WebAssemblyHostBuilder.CreateDefault(args);");
                Console.WriteLine("  ...");
                Console.WriteLine("  ConfigureServices(builder.Services, builder.HostEnvironment)");
                Console.WriteLine("  ...");
                Console.WriteLine("  static void ConfigureServices(IServiceCollection services, IWebAssemblyHostEnvironment hostEnv) {");
                Console.WriteLine("    services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(hostEnv.BaseAddress) });");
                Console.WriteLine("    services.AddScoped<IFooService, FooService>();");
                Console.WriteLine("    ...");
                Console.WriteLine("  }");
                Console.WriteLine("");
                Console.WriteLine("For more detail, see also: https://github.com/jsakamoto/BlazorWasmPreRendering.Build#services-registration");
            }

            if (crawlingResult.HasFlag(StaticlizeCrawlingResult.HasErrorsOfJSInvokeOnServer))
            {
                Console.WriteLine();
                Console.WriteLine("\x1b[91mX\x1b[0m [ERROR] JavaScript interop calls cannot be issued at this time");
                Console.WriteLine("-----------------------------");
                Console.WriteLine("if you are calling JavaScript code in \"OnInitializedAsync()\" like this:");
                Console.WriteLine("");
                Console.WriteLine("  @inject IJSRuntime JS");
                Console.WriteLine("  ...");
                Console.WriteLine("  protected async Task OnInitializedAsync() {");
                Console.WriteLine("    await this.JS.InvokeVoidAsync(\"...\", ...);");
                Console.WriteLine("    ...");
                Console.WriteLine("");
                Console.WriteLine("Please consider changing the above code to like below.");
                Console.WriteLine("");
                Console.WriteLine("  @inject IJSRuntime JS");
                Console.WriteLine("  ...");
                Console.WriteLine("  protected async Task OnAfterRenderAsync(bool firstRender) {");
                Console.WriteLine("    if (firstRender) {");
                Console.WriteLine("      await this.JS.InvokeVoidAsync(\"...\", ...);");
                Console.WriteLine("      ...");
                Console.WriteLine("");
                Console.WriteLine("For more detail, see also: https://docs.microsoft.com/aspnet/core/blazor/javascript-interoperability/call-javascript-from-dotnet#prerendering");
            }

            if (!keepRunning)
            {
                Console.WriteLine();
                Console.WriteLine("TIPS");
                Console.WriteLine("-----------------------------");
                Console.WriteLine("If you want to keep running the pre-rendering server process for debugging it on live, you can do that by setting the \"BlazorWasmPrerenderingKeepServer\" MSBuild property to \"true\".");
                Console.WriteLine();
                Console.WriteLine("ex) dotnet publish -p:BlazorWasmPrerenderingKeepServer=true");
            }
            Console.WriteLine();
        }
    }
}
