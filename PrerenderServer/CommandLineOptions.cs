namespace Toolbelt.Blazor.WebAssembly.PrerenderServer
{
    public class CommandLineOptions
    {
        public string? PublishedDir { get; set; }

        public string? AssemblyName { get; set; }

        public string? TypeNameOfRootComponent { get; set; }

        public string? SelectorOfRootComponent { get; set; } = "#app";

        public bool KeepRunning { get; init; }
    }
}
