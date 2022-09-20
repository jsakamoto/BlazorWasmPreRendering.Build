using System;

namespace Toolbelt.Blazor.WebAssembly.PrerenderServer
{
    [Flags]
    internal enum StaticlizeCrawlingResult
    {
        Nothing,
        HasWarnings,
        HasErrors,
        HasErrorsOfServiceNotRegistered,
        HasErrorsOfJSInvokeOnServer,
    }
}
