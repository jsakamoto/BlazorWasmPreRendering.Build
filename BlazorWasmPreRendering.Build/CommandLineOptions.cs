using Microsoft.AspNetCore.Mvc.Rendering;

namespace Toolbelt.Blazor.WebAssembly.PrerenderServer
{
    public class CommandLineOptions
    {
        public string? IntermediateDir { get; set; }

        public string? PublishedDir { get; set; }

        public string? AssemblyName { get; set; }

        public string? TypeNameOfRootComponent { get; set; }

        public string? SelectorOfRootComponent { get; set; } = "#app";

        public string? SelectorOfHeadOutletComponent { get; set; } = "head::after";

        public string? MiddlewarePackages { get; set; }

        public string? FrameworkName { get; set; }

        public string? ServiceWorkerAssetsManifest { get; set; }

        public string? Environment { get; set; }

        public RenderMode RenderMode { get; set; } = RenderMode.Static;

        public OutputStyle OutputStyle { get; set; } = OutputStyle.IndexHtmlInSubFolders;

        private bool _DeleteLoadingContents = false;

        public bool DeleteLoadingContents
        {
            get => this._DeleteLoadingContents || RenderMode != RenderMode.Static;
            set => this._DeleteLoadingContents = value;
        }

        public string? UrlPathToExplicitFetch { get; set; }

        public bool KeepRunning { get; init; }
    }
}
