using System;
using System.IO;
using System.Linq;
using System.Text;

namespace Toolbelt.Blazor.WebAssembly.PreRendering.Build.WebHost
{
    public class ResetHeadOutletScript
    {
        public string Text { get; }

        public string Base64Text { get; }

        public ResetHeadOutletScript()
        {
            var assembly = this.GetType().Assembly;
            var scriptResName = assembly.GetManifestResourceNames().First(name => name.EndsWith(".reset-head-outlet.min.js"));
            using var resStream = assembly.GetManifestResourceStream(scriptResName);
            using var reader = new StreamReader(resStream!);
            this.Text = reader.ReadToEnd();
            this.Base64Text = Convert.ToBase64String(Encoding.UTF8.GetBytes(this.Text));
        }
    }
}