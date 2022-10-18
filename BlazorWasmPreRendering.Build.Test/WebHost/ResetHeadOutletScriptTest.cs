using NUnit.Framework;
using Toolbelt.Blazor.WebAssembly.PreRendering.Build.WebHost;

namespace BlazorWasmPreRendering.Build.Test.WebHost;

public class ResetHeadOutletScriptTest
{
    [Test]
    public void Script_Test()
    {
        var script = new ResetHeadOutletScript();
        script.Text.StartsWith("(function(").IsTrue();
        script.Text.EndsWith("/^%%-PRERENDERING-HEADOUTLET-(BEGIN|END)-%%$/);").IsTrue();
    }
}
