using System.Reflection;
using Microsoft.AspNetCore.Mvc.Rendering;
using Toolbelt.Blazor.WebAssembly.PreRendering.Build.Shared;

namespace Toolbelt.Blazor.WebAssembly.PreRendering.Build.WebHost;

public class ServerSideRenderingContext
{
    public CustomAssemblyLoader AssemblyLoader { get; init; } = null!;

    public string WebRootPath { get; init; } = "";

    public Assembly ApplicationAssembly { get; init; } = null!;

    public IEnumerable<MiddlewarePackageReference> MiddlewarePackages { get; init; } = Enumerable.Empty<MiddlewarePackageReference>();


    public Type RootComponentType { get; init; } = null!;

    public Type? HeadOutletComponentType { get; init; } = null;

    public RenderMode RenderMode { get; init; }

    public IndexHtmlFragments IndexHtmlFragments { get; init; } = null!;

    public bool DeleteLoadingContents { get; init; }


    public string? Environment { get; init; }

    public bool EmulateAuthMe { get; init; }

    public string[] Locales { get; init; } = new string[0];

    public int ServerPort { get; init; }
}