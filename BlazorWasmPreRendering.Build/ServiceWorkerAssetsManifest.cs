using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Toolbelt.Blazor.WebAssembly.PrerenderServer.Models;

namespace Toolbelt.Blazor.WebAssembly.PrerenderServer
{
    internal class ServiceWorkerAssetsManifest
    {
        public static async ValueTask UpdateAsync(string wwwrootDir)
        {
            var serviceWorkerAssetsJsPath = Path.Combine(wwwrootDir, "service-worker-assets.js");
            if (!File.Exists(serviceWorkerAssetsJsPath)) return;

            var serviceWorkerAssetsJs = await File.ReadAllTextAsync(serviceWorkerAssetsJsPath);
            serviceWorkerAssetsJs = Regex.Replace(serviceWorkerAssetsJs, @"^self\.assetsManifest\s*=\s*", "");
            serviceWorkerAssetsJs = Regex.Replace(serviceWorkerAssetsJs, ";\\s*$", "");
            var assetsManifestFile = JsonSerializer.Deserialize<AssetsManifestFile>(serviceWorkerAssetsJs);
            if (assetsManifestFile == null) return;

            var assetManifestEntry = assetsManifestFile.assets?.First(a => a.url == "index.html");
            if (assetManifestEntry == null) return;

            await using (var indexHtmlStream = File.OpenRead(Path.Combine(wwwrootDir, "index.html")))
            {
                using var sha256 = SHA256.Create();
                assetManifestEntry.hash = "sha256-" + Convert.ToBase64String(await sha256.ComputeHashAsync(indexHtmlStream));
            }

            await using (var serviceWorkerAssetsStream = File.OpenWrite(serviceWorkerAssetsJsPath))
            {
                await using var streamWriter = new StreamWriter(serviceWorkerAssetsStream, Encoding.UTF8, 50, leaveOpen: true);
                streamWriter.Write("self.assetsManifest = ");
                streamWriter.Flush();
                using var jsonWriter = JsonReaderWriterFactory.CreateJsonWriter(serviceWorkerAssetsStream, Encoding.UTF8, ownsStream: false, indent: true);
                new DataContractJsonSerializer(typeof(AssetsManifestFile)).WriteObject(jsonWriter, assetsManifestFile);
                jsonWriter.Flush();
                streamWriter.WriteLine(";");
            }
        }
    }
}
