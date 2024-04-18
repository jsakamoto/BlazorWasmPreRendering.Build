using Microsoft.AspNetCore.Mvc.Rendering;
using Toolbelt.Blazor.WebAssembly.PreRendering.Build.Shared;

namespace Toolbelt.Blazor.WebAssembly.PreRendering.Build;

public class BlazorWasmPrerenderingOptions
{
    public string WebRootPath { get; init; } = "";

    /// <summary>
    /// The directory path where the application's assembly files are located.<br/>
    /// (ex."/project/bin/Release/net8.0/")
    /// </summary>
    public string AssemblyDir { get; init; } = "";

    public RenderMode RenderMode { get; init; }

    public IndexHtmlFragments IndexHtmlFragments { get; init; } = null!;

    public bool DeleteLoadingContents { get; init; }


    public bool EnableGZipCompression { get; init; }

    public bool EnableBrotliCompression { get; init; }

    public List<MiddlewarePackageReference> MiddlewarePackages { get; init; } = new();

    public string MiddlewareDllsDir { get; init; } = "";

    public List<string> Locales { get; init; } = new();
}
