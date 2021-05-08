using System;
using System.IO;

namespace BlazorWasmPreRendering.Build.Test.Fixtures
{
    public class WorkFolder : IDisposable
    {
        private string Dir { get; }

        public WorkFolder()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            this.Dir = Path.Combine(baseDir, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this.Dir);
        }

        public static implicit operator string(WorkFolder folder)
        {
            return folder.Dir;
        }

        public void Dispose()
        {
            if (Directory.Exists(this.Dir))
            {
                try { Directory.Delete(this.Dir, recursive: true); } catch { }
            }
        }
    }
}
