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

        public bool KeepRunning { get; init; }
    }
}
