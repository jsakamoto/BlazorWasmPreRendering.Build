using System.Collections.Generic;

namespace Toolbelt.Blazor.WebAssembly.PrerenderServer.Models
{
    public class AssetsManifestFile
    {
        public string? version { get; set; }

        public List<AssetsManifestFileEntry>? assets { get; set; }
    }
}
