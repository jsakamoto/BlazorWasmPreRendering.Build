using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;
using Toolbelt.Blazor.WebAssembly.PreRendering.Build.Shared;

namespace Toolbelt.Blazor.WebAssembly.PreRendering.Build.WebHost
{
    internal class CommandLineOptions
    {
        public string? WebRootPath { get; init; }

        public string? MiddlewareDllsDir { get; init; }

        public IEnumerable<MiddlewarePackageReference>? MiddlewarePackages { get; init; }

        public string? AssemblyName { get; init; }

        public string? RootComponentTypeName { get; init; }

        public RenderMode RenderMode { get; init; }

        public IndexHtmlFragments? IndexHtmlFragments { get; init; }

        public bool DeleteLoadingContents { get; init; }

        public string? Environment { get; init; }

        public int ServerPort { get; init; }
    }
}