using Toolbelt;

namespace BlazorWasmPreRendering.Build.Test;

internal static class Assets
{
    public static string GetAssetsDir()
    {
        var projectDir = FileIO.FindContainerDirToAncestor("*.csproj");
        return Path.Combine(projectDir, "_Fixtures", "Assets");
    }

    public static string GetAssetPathOf(string fileName)
    {
        return Path.Combine(GetAssetsDir(), fileName);
    }
}
