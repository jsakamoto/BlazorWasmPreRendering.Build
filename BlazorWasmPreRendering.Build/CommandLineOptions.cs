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

        public string? Locale { get; set; }

        public RenderMode RenderMode { get; set; } = RenderMode.Static;

        public OutputStyle OutputStyle { get; set; } = OutputStyle.IndexHtmlInSubFolders;

        private bool _DeleteLoadingContents = false;

        public bool DeleteLoadingContents
        {
            get => this._DeleteLoadingContents || this.RenderMode != RenderMode.Static;
            set => this._DeleteLoadingContents = value;
        }

        public string? UrlPathToExplicitFetch { get; set; }

        public bool KeepRunning { get; init; }

        public static readonly string DefaultServerPort = "5050-5999";

        private string _ServerPort = DefaultServerPort;

        public string ServerPort { get => this._ServerPort; set { this._ServerPort = string.IsNullOrEmpty(value) ? DefaultServerPort : value; } }
    }
}
