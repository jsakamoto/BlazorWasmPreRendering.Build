namespace Toolbelt.Blazor.WebAssembly.PrerenderServer.Models
{
    internal class AssetsManifestFile
    {
        public string? version { get; set; }

        public AssetsManifestFileEntry[]? assets { get; set; }
    }
}
