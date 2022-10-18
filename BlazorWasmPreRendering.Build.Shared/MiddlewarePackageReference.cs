namespace Toolbelt.Blazor.WebAssembly.PreRendering.Build.Shared
{
    public class MiddlewarePackageReference
    {
        public string PackageIdentity { get; init; } = "";

        public string Assembly { get; init; } = "";

        public string Version { get; init; } = "";

        public override string ToString() => $"{this.PackageIdentity},{this.Assembly},{this.Version}";
    }
}
