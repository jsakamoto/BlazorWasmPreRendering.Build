using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Toolbelt.Blazor.WebAssembly.PrerenderServer
{
    public class BlazorWasmPrerenderingOptions
    {
        public string WebRootPath { get; init; } = "";

        public Assembly ApplicationAssembly { get; init; } = null!;



        public Type RootComponentType { get; init; } = null!;

        public Type? HeadOutletComponentType { get; init; } = null;

        public RenderMode RenderMode { get; init; }

        public IndexHtmlFragments IndexHtmlFragments { get; init; } = null!;

        public bool DeleteLoadingContents { get; init; }



        public bool EnableGZipCompression { get; init; }

        public bool EnableBrotliCompression { get; init; }

        public IEnumerable<MiddlewarePackageReference> MiddlewarePackages { get; init; } = Enumerable.Empty<MiddlewarePackageReference>();
    }
}
