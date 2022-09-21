using System;

namespace Toolbelt.Blazor.WebAssembly.PrerenderServer
{
    [Flags]
    internal enum StaticlizeCrawlingResult
    {
        Nothing = 0b_0000_0001,
        HasWarnings = 0b_0000_0010,
        HasErrors = 0b_0000_0100,
        HasErrorsOfServiceNotRegistered = 0b_0000_1000,
        HasErrorsOfJSInvokeOnServer = 0b_0001_0000,
    }
}
