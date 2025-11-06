using System.Runtime.CompilerServices;

namespace Sharpy.Compiler.Logging;

/// <summary>
/// Null object pattern implementation - all methods are no-ops.
/// Methods are aggressively inlined for zero overhead.
/// </summary>
public sealed class NullLogger : ICompilerLogger
{
    public static readonly NullLogger Instance = new();

    private NullLogger() { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void LogTokenRead(string tokenType, int line, int column, string value) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void LogIndentChange(int oldLevel, int newLevel) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void LogParseEnter(string rule, int tokenPosition) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void LogParseExit(string rule, bool success) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void LogError(string message, int line, int column) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void LogWarning(string message, int line, int column) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void LogInfo(string message) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void LogDebug(string message) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void LogTrace(string message) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsEnabled(CompilerLogLevel level) => false;
}
