using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Toolbelt.Blazor.WebAssembly.PreRendering.Build.Models;

public class AssetsManifestFile
{
    public string? version { get; set; }

    public List<AssetsManifestFileEntry>? assets { get; set; }

    public static async ValueTask<AssetsManifestFile?> LoadAsync(string assetsManifestFilePath)
    {
        var serviceWorkerAssetsJs = await File.ReadAllTextAsync(assetsManifestFilePath);
        serviceWorkerAssetsJs = Regex.Replace(serviceWorkerAssetsJs, @"^self\.assetsManifest\s*=\s*", "");
        serviceWorkerAssetsJs = Regex.Replace(serviceWorkerAssetsJs, ";\\s*$", "");
        var assetsManifestFile = JsonSerializer.Deserialize<AssetsManifestFile>(serviceWorkerAssetsJs);
        return assetsManifestFile;
    }

    public async Task SaveAsync(string serviceWorkerAssetsJsPath)
    {
        await using var serviceWorkerAssetsStream = File.Create(serviceWorkerAssetsJsPath);
        await using var streamWriter = new StreamWriter(serviceWorkerAssetsStream, Encoding.UTF8, 50, leaveOpen: true);
        streamWriter.Write("self.assetsManifest = ");
        streamWriter.Flush();
        using var jsonWriter = JsonReaderWriterFactory.CreateJsonWriter(serviceWorkerAssetsStream, Encoding.UTF8, ownsStream: false, indent: true);
        new DataContractJsonSerializer(typeof(AssetsManifestFile)).WriteObject(jsonWriter, this);
        jsonWriter.Flush();
        streamWriter.WriteLine(";");
    }
}
