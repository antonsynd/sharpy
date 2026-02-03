namespace Sharpy.Compiler.Logging;

/// <summary>
/// Console-based logger for compiler operations
/// </summary>
internal sealed class ConsoleCompilerLogger : ICompilerLogger
{
    private readonly CompilerLogLevel _minLevel;
    private readonly TextWriter _output;
    private readonly TextWriter _errorOutput;

    public ConsoleCompilerLogger(CompilerLogLevel minLevel, TextWriter? output = null, TextWriter? errorOutput = null)
    {
        _minLevel = minLevel;
        _output = output ?? Console.Out;
        _errorOutput = errorOutput ?? Console.Error;
    }

    public void LogTokenRead(string tokenType, int line, int column, string value)
    {
        if (_minLevel >= CompilerLogLevel.Trace)
        {
            var valueDisplay = string.IsNullOrEmpty(value) ? "" : $" = '{value}'";
            _output.WriteLine($"[TRACE] [LEXER] Token: {tokenType,-20} @ L{line}:C{column}{valueDisplay}");
        }
    }

    public void LogIndentChange(int oldLevel, int newLevel)
    {
        if (_minLevel >= CompilerLogLevel.Trace)
        {
            _output.WriteLine($"[TRACE] [LEXER] Indent: {oldLevel} → {newLevel}");
        }
    }

    public void LogParseEnter(string rule, int tokenPosition)
    {
        if (_minLevel >= CompilerLogLevel.Debug)
        {
            _output.WriteLine($"[DEBUG] [PARSER] Enter: {rule} @ token {tokenPosition}");
        }
    }

    public void LogParseExit(string rule, bool success)
    {
        if (_minLevel >= CompilerLogLevel.Debug)
        {
            var status = success ? "✓" : "✗";
            _output.WriteLine($"[DEBUG] [PARSER] Exit:  {rule} {status}");
        }
    }

    public void LogError(string message, int line, int column)
    {
        if (_minLevel >= CompilerLogLevel.Error)
        {
            _errorOutput.WriteLine($"[ERROR] @ L{line}:C{column}: {message}");
        }
    }

    public void LogWarning(string message, int line, int column)
    {
        if (_minLevel >= CompilerLogLevel.Warning)
        {
            _errorOutput.WriteLine($"[WARN]  @ L{line}:C{column}: {message}");
        }
    }

    public void LogInfo(string message)
    {
        if (_minLevel >= CompilerLogLevel.Info)
        {
            _output.WriteLine($"[INFO]  {message}");
        }
    }

    public void LogDebug(string message)
    {
        if (_minLevel >= CompilerLogLevel.Debug)
        {
            _output.WriteLine($"[DEBUG] {message}");
        }
    }

    public void LogTrace(string message)
    {
        if (_minLevel >= CompilerLogLevel.Trace)
        {
            _output.WriteLine($"[TRACE] {message}");
        }
    }

    public void LogMetrics(string metricsOutput)
    {
        _output.WriteLine(metricsOutput);
    }

    public bool IsEnabled(CompilerLogLevel level) => _minLevel >= level;
}
