using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using CommandLineSwitchParser;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Toolbelt.Blazor.WebAssembly.PrerenderServer
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var commandLineOptions = CommandLineSwitch.Parse<CommandLineOptions>(ref args);
            var prerenderingOptions = BuildPrerenderingOptions(commandLineOptions);

            SetupCustomAssemblyLoader(prerenderingOptions);

            using var webHost = await StartWebHostAsync(prerenderingOptions);
            var configuration = webHost.Services.GetRequiredService<IConfiguration>();

            Console.WriteLine("Start fetching...");

            var path = "/";
            var htmlParser = new HtmlParser();
            var httpClient = new HttpClient();
            var savedPathSet = new HashSet<string>();
            var prerenderingHostUrl = configuration["urls"]
                .Split(';')
                .First(url => !string.IsNullOrWhiteSpace(url))
                .TrimEnd('/');

            await SaveToStaticFileAsync(
                path,
                httpClient,
                htmlParser,
                savedPathSet,
                prerenderingHostUrl,
                prerenderingOptions);

            Console.WriteLine("Fetching complete.");

            if (!commandLineOptions.KeepRunning) await webHost.StopAsync();

            await webHost.WaitForShutdownAsync();
            return 0;
        }

        private static void SetupCustomAssemblyLoader(BlazorWasmPrerenderingOptions prerenderingOptions)
        {
            var frameworkDir = Path.Combine(prerenderingOptions.WebRootPath, "_framework");
            AssemblyLoadContext.Default.Resolving += (context, name) =>
            {
                var path = Path.Combine(frameworkDir, name.Name + ".dll");
                if (!File.Exists(path)) return null;
                return context.LoadFromAssemblyPath(path);
            };
        }

        private static BlazorWasmPrerenderingOptions BuildPrerenderingOptions(CommandLineOptions commandLineOptions)
        {
            if (string.IsNullOrEmpty(commandLineOptions.PublishedDir)) throw new ArgumentException("The --PublishedDir parameter is required.");
            if (string.IsNullOrEmpty(commandLineOptions.AssemblyName)) throw new ArgumentException("The --AssemblyName parameter is required.");
            if (string.IsNullOrEmpty(commandLineOptions.TypeNameOfRootComponent)) throw new ArgumentException("The --TypeNameOfRootComponent parameter is required.");
            if (string.IsNullOrEmpty(commandLineOptions.SelectorOfRootComponent)) throw new ArgumentException("The --SelectorOfRootComponent parameter is required.");

            var webRootPath = Path.Combine(commandLineOptions.PublishedDir, "wwwroot");
            var indexHtmlPath = Path.Combine(webRootPath, "index.html");
            var appAssemblyPath = Path.Combine(webRootPath, "_framework", commandLineOptions.AssemblyName);
            if (!appAssemblyPath.ToLower().EndsWith(".dll")) appAssemblyPath += ".dll";



            var enableGZipCompression = File.Exists(indexHtmlPath + ".gz");
            var enableBrotliCompression = File.Exists(indexHtmlPath + ".br");


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

            var parser = new HtmlParser();
            var indexHtmlDoc = parser.ParseDocument(indexHtmlText);
            var appRootComponentElement = indexHtmlDoc.QuerySelector(commandLineOptions.SelectorOfRootComponent);

            var outerHtml = appRootComponentElement.OuterHtml;
            var innerHtml = appRootComponentElement.InnerHtml;
            var indexOfInner = outerHtml.IndexOf(innerHtml);
            var marker = outerHtml.Substring(0, innerHtml.Length + indexOfInner);

            var indexOfMarker = indexHtmlText.IndexOf(marker);
            var indexHtmlTextFirstHalf = indexHtmlText[0..(indexOfMarker + marker.Length)];
            var indexHtmlTextSecondHalf = indexHtmlText[(indexOfMarker + marker.Length)..];

            var appAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(appAssemblyPath);
            var appComponentType = appAssembly.GetType(commandLineOptions.TypeNameOfRootComponent);
            if (appComponentType == null) throw new ArgumentException($"The component type \"{commandLineOptions.TypeNameOfRootComponent}\" was not found.");

            var options = new BlazorWasmPrerenderingOptions
            {
                IndexHtmlTextFirstHalf = indexHtmlTextFirstHalf,
                IndexHtmlTextSecondHalf = indexHtmlTextSecondHalf,
                WebRootPath = webRootPath,
                ApplicationAssembly = appAssembly,
                RootComponentType = appComponentType,
                EnableGZipCompression = enableGZipCompression,
                EnableBrotliCompression = enableBrotliCompression
            };
            return options;
        }

        private static async Task<IHost> StartWebHostAsync(BlazorWasmPrerenderingOptions prerenderingOptions)
        {
            var hostBuilder = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddSingleton(prerenderingOptions);
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder
                        .UseUrls("http://127.0.0.1:5050")
                        .UseWebRoot(prerenderingOptions.WebRootPath)
                        .UseStartup(context => new Startup(context.Configuration, prerenderingOptions));
                });
            var webHost = hostBuilder.Build();
            await webHost.StartAsync();
            return webHost;
        }

        public static async Task SaveToStaticFileAsync(
            string path,
            HttpClient httpClient,
            IHtmlParser htmlParser,
            HashSet<string> savedPathSet,
            string prerenderingHostUrl,
            BlazorWasmPrerenderingOptions options
        )
        {
            if (savedPathSet.Contains(path)) return;
            savedPathSet.Add(path);

            var requestUrl = prerenderingHostUrl + path;
            Console.WriteLine($"Getting {requestUrl}...");
            var response = await httpClient.GetAsync(requestUrl);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                Console.WriteLine($"  The HTTP status code was not OK. (it was {response.StatusCode}.)");
                return;
            }
            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (mediaType != "text/html")
            {
                Console.WriteLine($"  The content type was not text/html. (it was {mediaType}.)");
                return;
            }

            var htmlContent = await response.Content.ReadAsStringAsync();
            var targetDir = Path.Combine(options.WebRootPath, Path.Combine(path.Split('/')));
            Directory.CreateDirectory(targetDir);
            var indexHtmlPath = Path.Combine(targetDir, "index.html");
            File.WriteAllText(indexHtmlPath, htmlContent);

            RecompressStaticFile(options, indexHtmlPath);

            var htmlDoc = htmlParser.ParseDocument(htmlContent);
            var links = htmlDoc.Links
                .OfType<IHtmlAnchorElement>()
                .Where(link => string.IsNullOrEmpty(link.Origin))
                .Where(link => string.IsNullOrEmpty(link.Target))
                .Where(link => !string.IsNullOrEmpty(link.PathName))
                .Select(link => link.PathName)
                .ToArray();

            foreach (var link in links)
            {
                await SaveToStaticFileAsync(
                    link,
                    httpClient,
                    htmlParser,
                    savedPathSet,
                    prerenderingHostUrl,
                    options);
            }
        }

        private static void RecompressStaticFile(BlazorWasmPrerenderingOptions options, string indexHtmlPath)
        {
            if (!options.EnableGZipCompression && !options.EnableBrotliCompression) return;

            using var sourceStream = File.OpenRead(indexHtmlPath);

            if (options.EnableGZipCompression)
            {
                using var outputStream = File.Create(indexHtmlPath + ".gz");
                using var compressingStream = new GZipStream(outputStream, CompressionLevel.Optimal);
                sourceStream.Seek(0, SeekOrigin.Begin);
                sourceStream.CopyTo(compressingStream);
            }

            if (options.EnableBrotliCompression)
            {
                using var outputStream = File.Create(indexHtmlPath + ".br");
                using var compressingStream = new BrotliStream(outputStream, CompressionLevel.Optimal);
                sourceStream.Seek(0, SeekOrigin.Begin);
                sourceStream.CopyTo(compressingStream);
            }
        }
    }
}
