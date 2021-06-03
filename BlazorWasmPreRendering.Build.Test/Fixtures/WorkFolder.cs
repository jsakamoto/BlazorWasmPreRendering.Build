using System;
using System.IO;

namespace BlazorWasmPreRendering.Build.Test.Fixtures
{
    public class WorkFolder : IDisposable
    {
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

        public override string ToString() => _Dir;

        public void Dispose()
        {
            if (Directory.Exists(this._Dir))
            {
                try { Directory.Delete(this._Dir, recursive: true); } catch { }
            }
        }
    }
}
