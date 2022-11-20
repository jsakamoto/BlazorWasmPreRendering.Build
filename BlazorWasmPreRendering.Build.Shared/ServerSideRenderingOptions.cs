using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Toolbelt.Blazor.WebAssembly.PreRendering.Build.Shared
{
    public class ServerSideRenderingOptions
    {
        public string? WebRootPath { get; init; }

        public string? MiddlewareDllsDir { get; init; }

        public List<MiddlewarePackageReference> MiddlewarePackages { get; set; } = new();

        public string? AssemblyName { get; init; }

        public string? RootComponentTypeName { get; init; }

        public RenderMode RenderMode { get; init; }

        public IndexHtmlFragments IndexHtmlFragments { get; set; } = new("", "", "");

        public bool DeleteLoadingContents { get; init; }

        public string? Environment { get; init; }

        public List<string> Locales { get; init; } = new();

        public int ServerPort { get; init; }

        /// <summary>Options for Blazor Wasm Antivirus Protection - XorKey</summary>
        public string? BWAPOptionsXorKey { get; init; }

        /// <summary>Options for Blazor Wasm Antivirus Protection - Dll Extension</summary>
        public string? BWAPOptionsDllExt { get; set; }

        public bool EmulateAuthMe { get; init; }
    }
}