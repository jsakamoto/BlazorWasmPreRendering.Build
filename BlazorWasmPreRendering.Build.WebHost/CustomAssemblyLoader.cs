using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;

namespace Toolbelt.Blazor.WebAssembly.PreRendering.Build.WebHost
{
    public class CustomAssemblyLoader
    {
        private readonly List<string> _AssemblySearchDirs = new();

        private readonly AssemblyLoadContext _Context;

        private readonly string _XorKey;

        public CustomAssemblyLoader(AssemblyLoadContext? context = null, string? xorKey = null)
        {
            this._Context = context ?? AssemblyLoadContext.Default;
            this._XorKey = string.IsNullOrEmpty(xorKey) ? "bwap" : xorKey;
            this._Context.Resolving += (context, name) =>
            {
                return this._AssemblySearchDirs
                    .Select(dir => this.LoadAssemblyFrom(dir, name))
                    .Where(asm => asm != null)
                    .FirstOrDefault();
            };
        }

        private Assembly? LoadAssemblyFrom(string assemblyDir, AssemblyName assemblyName)
        {
            if (assemblyName.Name == null) return null;

            var assemblyPath = string.IsNullOrEmpty(assemblyName.CultureName) ?
                Path.Combine(assemblyDir, assemblyName.Name) :
                Path.Combine(assemblyDir, assemblyName.CultureName, assemblyName.Name);
            if (!assemblyPath.ToLower().EndsWith(".dll")) assemblyPath += ".dll";
            var secondaryAssemblyPath = Path.ChangeExtension(assemblyPath, ".bin");

            var foundAssemblyPath = new[] { assemblyPath, secondaryAssemblyPath }
                .Where(path => File.Exists(path))
                .FirstOrDefault();

            if (foundAssemblyPath == null)
            {
                // TODO: Console.WriteLine($"{assemblyName} in {assemblyDir} - not found.");
                return null;
            }
            // TODO: Console.WriteLine($"{assemblyName} in {assemblyDir} - FOUND.");
            var assemblyBytes = this.GetAssemblyBytes(foundAssemblyPath);
            var assembly = this._Context.LoadFromStream(new MemoryStream(assemblyBytes));
            return assembly;
        }

        private byte[] GetAssemblyBytes(string assemblyPath)
        {
            var assemblyBytes = File.ReadAllBytes(assemblyPath);

            // [Blazor Wasm Antivirus Protection support]
            // https://github.com/stavroskasidis/BlazorWasmAntivirusProtection
            // If the bytes of assembly start with "BZ", this loader assumes that the assembly is obfuscated with the "ChangeHeader" mode.
            if (assemblyBytes[0] == 0x42/*'B'*/ && assemblyBytes[1] == 0x5a/*'Z'*/)
            {
                assemblyBytes[0] = 0x4d; // 'M'
                assemblyBytes[1] = 0x5a; // 'Z'
            }

            // [Blazor Wasm Antivirus Protection support]
            // https://github.com/stavroskasidis/BlazorWasmAntivirusProtection
            // If the bytes of assembly doesn't start with "MZ", this loader assumes that the assembly is obfuscated with the "Xor" mode.
            if (assemblyBytes[0] != 0x4d/*'M'*/ || assemblyBytes[1] != 0x5a/*'Z'*/)
            {
                var key = Encoding.ASCII.GetBytes(this._XorKey);
                for (var i = 0; i < assemblyBytes.Length; i++)
                    assemblyBytes[i] = (byte)(assemblyBytes[i] ^ key[i % key.Length]);
            }

            return assemblyBytes;
        }

        public void AddSerachDir(string searchDir)
        {
            this._AssemblySearchDirs.Add(searchDir);
        }

        public Assembly LoadAssembly(string assemblyName)
        {
            try
            {
                return this._Context.LoadFromAssemblyName(new AssemblyName(assemblyName));
            }
            catch (Exception ex)
            {
                var pwd = Environment.CurrentDirectory;
                var searchDirs = string.Join('\n', this._AssemblySearchDirs);
                throw new Exception($"Could not load the assembly \"{assemblyName}\" in search directories below.\n{searchDirs}\n(pwd: {pwd})", ex);
            }
        }
    }
}
