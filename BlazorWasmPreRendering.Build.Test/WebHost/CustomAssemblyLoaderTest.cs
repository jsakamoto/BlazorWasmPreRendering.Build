using System.Runtime.Loader;
using NUnit.Framework;
using Toolbelt;
using Toolbelt.Blazor.WebAssembly.PreRendering.Build.WebHost;

namespace BlazorWasmPreRendering.Build.Test.WebHost;

internal class CustomAssemblyLoaderTest
{
    [TestCase("BlazorWasmAVP-None", ".bin", "(N/A)")]
    [TestCase("BlazorWasmAVP-ChangeHeaders", ".dll", "(N/A)")]
    [TestCase("BlazorWasmAVP-Xor-with-bwap", ".bin", "bwap")]
    [TestCase("BlazorWasmAVP-Xor-with-FizzBuzz", ".dll", "FizzBuzz")]
    public void Load_ObfuscatedDll_Test(string assemblyName, string ext, string xorKey)
    {
        using var workDir = new WorkDirectory();
        File.Copy(Assets.GetAssetPathOf(assemblyName + ".dll"), Path.Combine(workDir, "BlazorWasmAVP" + ext));

        var context = new AssemblyLoadContext(Guid.NewGuid().ToString(), true);
        try
        {
            // Given
            var loader = new CustomAssemblyLoader(context, xorKey, ext.TrimStart('.'));
            loader.AddSerachDir(workDir);

            // When
            var assembly = loader.LoadAssembly("BlazorWasmAVP");

            // Then
            assembly.IsNotNull();
            assembly.FullName.Is("BlazorWasmAVP, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
            assembly.GetType("BlazorWasmAVP.App").IsInstanceOf<Type>();
        }
        finally { context.Unload(); }
    }
}
