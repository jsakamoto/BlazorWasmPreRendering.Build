using Microsoft.Extensions.Logging;

namespace BlazorWasmPreRendering.Build.Test;

public class TestLogger : ILogger
{
    private readonly List<string> _LogLines = new();

    public IEnumerable<string> LogLines => this._LogLines;

    IDisposable ILogger.BeginScope<TState>(TState state) => throw new NotImplementedException();

    bool ILogger.IsEnabled(LogLevel logLevel) => true;

    void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var text = formatter.Invoke(state, exception);
        Console.WriteLine(text);
        this._LogLines.Add(text);
    }
}
