using System.Linq;
using NUnit.Framework;
using Toolbelt.Blazor.WebAssembly.PrerenderServer;

namespace BlazorWasmPreRendering.Build.Test;

public class MiddlewarePackageReferenceTest
{
    [Test]
    public void Parse_Test()
    {
        // When
        var middlewarePackages = MiddlewarePackageReference.Parse("Foo.Bar,,1.2.0.3;Fizz.Buzz,FizzBuzz,");

        // Then
        middlewarePackages
            .Select(p => $"{p.PackageIdentity},{p.Assembly},{p.Version}")
            .Is("Foo.Bar,,1.2.0.3",
                "Fizz.Buzz,FizzBuzz,");
    }
}
