using System;
using Microsoft.Extensions.Logging;

namespace Toolbelt.Blazor.WebAssembly.PreRendering.Build.Internal.Services.Logger
{
    internal class TinyConsoleLogger : ILogger
    {
        IDisposable ILogger.BeginScope<TState>(TState state) => throw new NotImplementedException();

        bool ILogger.IsEnabled(LogLevel logLevel) => true;

        void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Console.WriteLine(formatter.Invoke(state, exception));
        }
    }
}
