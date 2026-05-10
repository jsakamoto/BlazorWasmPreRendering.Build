using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders.Physical;

namespace Toolbelt.Blazor.WebAssembly.PreRendering.Build.WebHost;

internal sealed class NotStaticFileConstraint : IRouteConstraint
{
    private readonly IFileProvider _fileProvider;

    public NotStaticFileConstraint(ServerSideRenderingContext context)
    {
        this._fileProvider = new PhysicalFileProvider(context.WebRootPath, ExclusionFilters.None);
    }

    public bool Match(HttpContext? httpContext, IRouter? route, string routeKey, RouteValueDictionary values, RouteDirection routeDirection)
    {
        if (routeDirection == RouteDirection.UrlGeneration) return true;
        if (!values.TryGetValue(routeKey, out var raw) || raw is not string path) return true;
        if (string.IsNullOrEmpty(path)) return true;

        var fileInfo = this._fileProvider.GetFileInfo(path);
        return !fileInfo.Exists || fileInfo.IsDirectory;
    }
}
