using System;
using System.IO;
using System.Linq;

namespace BlazorWasmPreRendering.Build.Test.Fixtures
{
    public class WorkFolder : IDisposable
    {
        public static string GetSolutionDir()
        {
            var slnDir = AppDomain.CurrentDomain.BaseDirectory;
            while (slnDir != null && !Directory.GetFiles(slnDir, "*.sln", SearchOption.TopDirectoryOnly).Any()) slnDir = Path.GetDirectoryName(slnDir);
            if (slnDir == null) throw new Exception("The solution dir could not found.");
            return slnDir;
        }

        private string _Dir { get; }

        public WorkFolder()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            this._Dir = Path.Combine(baseDir, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this._Dir);
        }

        public static implicit operator string(WorkFolder folder)
        {
            return folder._Dir;
        }

        public override string ToString() => this._Dir;

        public void Dispose()
        {
            if (Directory.Exists(this._Dir))
            {
                try { Directory.Delete(this._Dir, recursive: true); } catch { }
            }
        }
    }
}
