using BlazorWasmApp2.Components.Services;

namespace BlazorWasmApp2.Client.Services;

public class AppTitle : IAppTitle
{
    public string GetAppTitle()
    {
        return "Blazor Wasm App 2";
    }
}
