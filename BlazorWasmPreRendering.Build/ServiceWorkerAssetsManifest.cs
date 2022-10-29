using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Toolbelt.Blazor.WebAssembly.PreRendering.Build.Models;

namespace Toolbelt.Blazor.WebAssembly.PreRendering.Build
{
    internal class ServiceWorkerAssetsManifest
    {
        public static async ValueTask UpdateAsync(string wwwrootDir, string? serviceWorkerAssetsManifest, IEnumerable<string> staticalizedFiles)
        {
            if (serviceWorkerAssetsManifest == null) return;
            var serviceWorkerAssetsJsPath = Path.Combine(wwwrootDir, serviceWorkerAssetsManifest);
            if (!File.Exists(serviceWorkerAssetsJsPath)) return;

            var assetsManifestFile = await AssetsManifestFile.LoadAsync(serviceWorkerAssetsJsPath);
            if (assetsManifestFile == null) return;
            if (assetsManifestFile.assets == null) assetsManifestFile.assets = new List<AssetsManifestFileEntry>();

            foreach (var staticalizedFile in staticalizedFiles)
            {
                var relativeUrl = string.Join('/', Path.GetRelativePath(wwwrootDir, staticalizedFile).Split('\\'));
                var assetManifestEntry = assetsManifestFile.assets.FirstOrDefault(a => a.url == relativeUrl);
                if (assetManifestEntry == null)
                {
                    assetManifestEntry = new AssetsManifestFileEntry { url = relativeUrl };
                    assetsManifestFile.assets.Add(assetManifestEntry);
                }

                await using (var indexHtmlStream = File.OpenRead(staticalizedFile))
                {
                    using var sha256 = SHA256.Create();
                    assetManifestEntry.hash = "sha256-" + Convert.ToBase64String(await sha256.ComputeHashAsync(indexHtmlStream));
                }
            }

            await assetsManifestFile.SaveAsync(serviceWorkerAssetsJsPath);
        }
    }
}
