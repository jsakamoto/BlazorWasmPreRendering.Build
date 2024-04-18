using System.Collections;

namespace Toolbelt.Blazor.WebAssembly.PreRendering.Build.Internal;

internal static class DotNetCLI
{
    private static string? _Path = null;

    /// <summary>
    /// Gets the path of "dotnet" command. (ex: "C:\Program Files\dotnet\dotnet")
    /// </summary>
    public static string Path
    {
        get
        {
            if (_Path == null)
            {
                var dotnetRoot = Environment.GetEnvironmentVariables()
                   .Cast<DictionaryEntry>()
                   .FirstOrDefault(x => x.Key?.ToString()?.StartsWith("DOTNET_ROOT") == true)
                   .Value?
                   .ToString();

                _Path = dotnetRoot == null ? "dotnet" : System.IO.Path.Combine(dotnetRoot, "dotnet");
            }

            return _Path!;
        }
    }
}
