using System.Reflection;
using System.Runtime.Loader;
using NuGet.Versioning;
using Toolbelt.Blazor.WebAssembly.PreRendering.Build.Shared;

namespace Toolbelt.Blazor.WebAssembly.PreRendering.Build;

public class MiddlewarePackageReferenceBuilder
{
    public static IEnumerable<MiddlewarePackageReference> Build(string folderToScan, string? middlewarePackages, ILogger? logger = null)
    {
        return BuildFromAssemblyMetadata(folderToScan)
            .Concat(Parse(middlewarePackages))
            .GroupBy(x => x.PackageIdentity)
            .Select(g => g.OrderByDescending(m => NuGetVersion.Parse(string.IsNullOrEmpty(m.Version) ? "0.0.0" : m.Version)).First())
            .ToArray();
    }

    private static IEnumerable<MiddlewarePackageReference> Parse(string? middlewarePackages)
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

    private static IEnumerable<MiddlewarePackageReference> BuildFromAssemblyMetadata(string folderToScan, ILogger? logger = null)
    {
        if (string.IsNullOrEmpty(folderToScan)) return Enumerable.Empty<MiddlewarePackageReference>();

        var ignoreCase = StringComparison.InvariantCultureIgnoreCase;
        var assembliesPath = Directory.GetFiles(folderToScan)
            .Where(path => path.EndsWith(".dll", ignoreCase) || path.EndsWith(".exe", ignoreCase))
            .Where(path => !Path.GetFileName(path).StartsWith("System.", ignoreCase))
            .Where(path => !Path.GetFileName(path).StartsWith("Microsoft.", ignoreCase));

        var context = new AssemblyLoadContext(Guid.NewGuid().ToString("N"), isCollectible: true);

        var packageReferences = new List<MiddlewarePackageReference>();

        foreach (var assemblyPath in assembliesPath)
        {
            try
            {
                var assemblyBytes = File.ReadAllBytes(assemblyPath);
                using var assemblyStream = new MemoryStream(assemblyBytes, writable: false);
                var assembly = context.LoadFromStream(assemblyStream);

                var assemblyMetadataAttributes = assembly
                    .GetCustomAttributes(typeof(AssemblyMetadataAttribute), inherit: true)
                    .OfType<AssemblyMetadataAttribute>()
                    .Where(attrib => attrib.Key == "BlazorWasmPreRendering.Build.MiddlewarePackageReference")
                    .SelectMany(attib => MiddlewarePackageReferenceBuilder.Parse(attib.Value))
                    .ToArray();
                if (assemblyMetadataAttributes.Any())
                {
                    packageReferences.AddRange(assemblyMetadataAttributes);
                }
            }
            catch (Exception e) { logger?.LogError(e, e.Message); }
        }

        context.Unload();

        return packageReferences;
    }
}