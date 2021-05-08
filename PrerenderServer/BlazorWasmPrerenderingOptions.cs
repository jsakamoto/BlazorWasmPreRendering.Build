using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Toolbelt.Blazor.WebAssembly.PrerenderServer
{
    public class BlazorWasmPrerenderingOptions
    {
        public string WebRootPath { get; init; } = "";

        public Assembly ApplicationAssembly { get; init; } = null!;

        public Type RootComponentType { get; init; } = null!;

        public string IndexHtmlTextFirstHalf { get; init; } = "";

        public string IndexHtmlTextSecondHalf { get; init; } = "";

        public bool EnableGZipCompression { get; init; }

        public bool EnableBrotliCompression { get; init; }

        public string IntermediateDir { get; init; } = "";

        public string FrameworkName { get; init; } = "";

        public IEnumerable<MiddlewarePackageReference> MiddlewarePackages { get; init; } = Enumerable.Empty<MiddlewarePackageReference>();
    }
}
