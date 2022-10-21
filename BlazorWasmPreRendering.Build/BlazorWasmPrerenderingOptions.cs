using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;
using Toolbelt.Blazor.WebAssembly.PreRendering.Build.Shared;

namespace Toolbelt.Blazor.WebAssembly.PrerenderServer
{
    public class BlazorWasmPrerenderingOptions
    {
        public string WebRootPath { get; init; } = "";

        public RenderMode RenderMode { get; init; }

        public IndexHtmlFragments IndexHtmlFragments { get; init; } = null!;

        public bool DeleteLoadingContents { get; init; }


        public bool EnableGZipCompression { get; init; }

        public bool EnableBrotliCompression { get; init; }

        public List<MiddlewarePackageReference> MiddlewarePackages { get; init; } = new();


        public string MiddlewareDllsDir { get; init; } = "";
    }
}
