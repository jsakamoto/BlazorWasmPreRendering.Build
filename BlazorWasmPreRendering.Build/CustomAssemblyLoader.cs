using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace Toolbelt.Blazor.WebAssembly.PrerenderServer
{
    internal class CustomAssemblyLoader
    {
        private readonly List<string> _AssemblySearchDirs = new List<string>();

        public CustomAssemblyLoader()
        {
            AssemblyLoadContext.Default.Resolving += (context, name) =>
            {
                if (name.Name == null) return null;
                return this._AssemblySearchDirs
                    .Select(dir => this.LoadAssemblyFrom(dir, name.Name))
                    .Where(asm => asm != null)
                    .FirstOrDefault();
            };
        }

        private Assembly? LoadAssemblyFrom(string assemblyDir, string assemblyName)
        {
            var assemblyPath = Path.Combine(assemblyDir, assemblyName);
            if (!assemblyPath.ToLower().EndsWith(".dll")) assemblyPath += ".dll";
            if (!File.Exists(assemblyPath))
            {
                // TODO: Console.WriteLine($"{assemblyName} in {assemblyDir} - not found.");
                return null;
            }
            // TODO: Console.WriteLine($"{assemblyName} in {assemblyDir} - FOUND.");
            var assembly = AssemblyLoadContext.Default.LoadFromStream(new MemoryStream(File.ReadAllBytes(assemblyPath)));
            return assembly;
        }

        public void AddSerachDir(string searchDir)
        {
            this._AssemblySearchDirs.Add(searchDir);
        }

        public Assembly LoadAssembly(string assemblyName)
        {
            try
            {
                return AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName(assemblyName));
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
