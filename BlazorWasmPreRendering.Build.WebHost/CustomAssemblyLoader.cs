using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace Toolbelt.Blazor.WebAssembly.PreRendering.Build.WebHost
{
    public class CustomAssemblyLoader
    {
        private readonly List<string> _AssemblySearchDirs = new();

        public CustomAssemblyLoader()
        {
            AssemblyLoadContext.Default.Resolving += (context, name) =>
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

            if (!File.Exists(assemblyPath))
            {
                // TODO: Console.WriteLine($"{assemblyName} in {assemblyDir} - not found.");
                File.AppendAllText("c:\\temp\\log.txt", $"NOT FOUND: {assemblyName.Name}({assemblyName.CultureName}) in {assemblyDir}\r\n");
                return null;
            }
            // TODO: Console.WriteLine($"{assemblyName} in {assemblyDir} - FOUND.");
            File.AppendAllText("c:\\temp\\log.txt", $"FOUND    : {assemblyName.Name}({assemblyName.CultureName}) in {assemblyDir}\r\n");
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
