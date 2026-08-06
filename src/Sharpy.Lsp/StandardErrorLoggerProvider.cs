using System.Globalization;
using Microsoft.Extensions.Logging;

namespace Sharpy.Lsp;

/// <summary>
/// Writes log records to standard error, one line each (#1225).
/// </summary>
/// <remarks>
/// <para>
/// The server registers no logging provider unless a level was explicitly asked for, so before
/// #1225 every <c>ILogger</c> record — including the per-stage analysis attribution #1140 added —
/// went nowhere no matter what the minimum level was. This is the destination when someone does ask.
/// </para>
/// <para>
/// Standard error rather than the two obvious alternatives. Standard <i>output</i> is the LSP
/// transport, so a provider that touched it would corrupt the protocol stream. And routing records
/// through <c>window/logMessage</c> would put a notification on the very connection whose latency
/// the attribution measures — an instrument that perturbs what it measures. Editors that spawn a
/// stdio server (VS Code's language client among them) surface the server's standard error in the
/// output channel, so this is field-visible without either hazard.
/// </para>
/// </remarks>
internal sealed class StandardErrorLoggerProvider : ILoggerProvider
{
    private readonly TextWriter _writer;

    public StandardErrorLoggerProvider(TextWriter writer) => _writer = writer;

    public ILogger CreateLogger(string categoryName) => new StandardErrorLogger(_writer, categoryName);

    public void Dispose()
    {
        // The writer is Console.Error, owned by the process.
    }

    private sealed class StandardErrorLogger : ILogger
    {
        private readonly TextWriter _writer;
        private readonly string _category;

        public StandardErrorLogger(TextWriter writer, string category)
        {
            _writer = writer;
            _category = category;
        }

        // Scopes carry no information this format shows, and the level filter is applied by the
        // logging infrastructure from the configured minimum — the provider logs what reaches it.
        public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var message = formatter(state, exception);
            var line = string.Create(CultureInfo.InvariantCulture,
                $"[{Abbreviate(logLevel)}] {_category}: {message}");

            // One Write per record: the writer is shared across analysis threads, and TextWriter
            // only guarantees atomicity per call.
            _writer.Write(line + Environment.NewLine);
            if (exception != null)
                _writer.Write(exception + Environment.NewLine);
            _writer.Flush();
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }

        private static string Abbreviate(LogLevel level) => level switch
        {
            LogLevel.Trace => "trce",
            LogLevel.Debug => "dbug",
            LogLevel.Information => "info",
            LogLevel.Warning => "warn",
            LogLevel.Error => "fail",
            LogLevel.Critical => "crit",
            _ => "none",
        };
    }
}
