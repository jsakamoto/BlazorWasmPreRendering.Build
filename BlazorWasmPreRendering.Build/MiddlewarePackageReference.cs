using System.Collections.Generic;
using System.Linq;

namespace Toolbelt.Blazor.WebAssembly.PrerenderServer
{
    public class MiddlewarePackageReference
    {
        public string PackageIdentity { get; init; } = "";
        public string Assembly { get; init; } = "";
        public string Version { get; init; } = "";

        public static IEnumerable<MiddlewarePackageReference> Parse(string? middlewarePackages)
        {
            if (string.IsNullOrEmpty(middlewarePackages)) return Enumerable.Empty<MiddlewarePackageReference>();

            return middlewarePackages
                .Split(';')
                .Select(pack => pack.Split(','))
                .Select(parts => new MiddlewarePackageReference
                {
                    PackageIdentity = parts.First(),
                    Assembly = parts.Skip(1).FirstOrDefault() ?? "",
                    Version = parts.Skip(2).FirstOrDefault() ?? ""
                })
                .ToArray();
        }
    }
}
