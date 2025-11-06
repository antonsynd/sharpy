# Logging Architecture for Sharpy Compiler

## Overview

This document proposes strategies for adding optional logging/debugging capabilities to the Sharpy lexer and parser while maintaining performance in production scenarios.

## Design Goals

1. **Zero-cost abstraction** - No performance penalty when logging is disabled
2. **Compile-time elimination** - Logging code removed in release builds when possible
3. **Flexible verbosity levels** - Different detail levels for different debugging needs
4. **Structured output** - Machine-parseable log formats for tooling
5. **Performance metrics** - Optional timing and memory profiling
6. **Minimal code noise** - Don't clutter core logic with logging calls

---

## Proposed Approaches

### 1. Conditional Compilation with Preprocessor Directives

**Best for:** Complete elimination of logging overhead in release builds.

```csharp
public class Lexer
{
    #if DEBUG_LEXER
    private readonly ILogger? _logger;
    #endif

    public Lexer(string source, ILogger? logger = null)
    {
        _source = source;
        #if DEBUG_LEXER
        _logger = logger;
        #endif
    }

    private Token ReadString()
    {
        #if DEBUG_LEXER
        _logger?.LogTrace($"ReadString: Starting at L{_line}:C{_column}");
        #endif

        // ... actual logic ...

        #if DEBUG_LEXER
        _logger?.LogTrace($"ReadString: Completed token={token.Type}");
        #endif

        return token;
    }
}
```

**Pros:**
- True zero-cost in release builds
- No runtime checks
- Complete code elimination by compiler

**Cons:**
- Can't enable logging in release builds (for production debugging)
- Makes code harder to read with many `#if` blocks
- Requires recompilation to enable/disable

---

### 2. Static Readonly Flag with JIT Optimization

**Best for:** Allowing logging in release builds when needed, while maintaining performance.

```csharp
public class Lexer
{
    // Can be set via environment variable or compile-time constant
    private static readonly bool EnableLogging =
        Environment.GetEnvironmentVariable("SHARPY_DEBUG_LEXER") == "1";

    private readonly ILogger? _logger;

    public Lexer(string source, ILogger? logger = null)
    {
        _source = source;
        _logger = EnableLogging ? logger : null;
    }

    private Token ReadString()
    {
        if (EnableLogging && _logger != null)
        {
            _logger.LogTrace($"ReadString: Starting at L{_line}:C{_column}");
        }

        // ... actual logic ...

        if (EnableLogging && _logger != null)
        {
            _logger.LogTrace($"ReadString: Completed token={token.Type}");
        }

        return token;
    }
}
```

**Pros:**
- JIT can eliminate dead code when `EnableLogging` is false
- Can be controlled via environment variables
- Single build can support both modes
- Relatively clean code

**Cons:**
- Small constant check overhead (though JIT should optimize)
- Logger reference still stored in object

**JIT Optimization Note:** The .NET JIT recognizes `static readonly` boolean checks and can eliminate the entire `if` block when the value is `false`, achieving near-zero overhead.

---

### 3. Abstracted Logger Interface with Null Object Pattern

**Best for:** Clean code with minimal performance impact.

```csharp
public interface ICompilerLogger
{
    void LogTokenRead(TokenType type, int line, int column, string value);
    void LogIndentChange(int oldLevel, int newLevel);
    void LogParseEnter(string rule, int tokenPosition);
    void LogParseExit(string rule, bool success);
    void LogError(string message, int line, int column);
}

public sealed class NullLogger : ICompilerLogger
{
    public static readonly NullLogger Instance = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void LogTokenRead(TokenType type, int line, int column, string value) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void LogIndentChange(int oldLevel, int newLevel) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void LogParseEnter(string rule, int tokenPosition) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void LogParseExit(string rule, bool success) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void LogError(string message, int line, int column) { }
}

public class Lexer
{
    private readonly ICompilerLogger _logger;

    public Lexer(string source, ICompilerLogger? logger = null)
    {
        _source = source;
        _logger = logger ?? NullLogger.Instance;
    }

    private Token ReadString()
    {
        var startLine = _line;
        var startColumn = _column;

        // ... actual logic ...

        _logger.LogTokenRead(TokenType.String, startLine, startColumn, value);
        return token;
    }
}
```

**Pros:**
- Clean, idiomatic code
- No `if` checks in hot paths
- Logger interface can be rich with structured data
- Easy to swap implementations (JSON logger, file logger, etc.)
- `AggressiveInlining` makes null logger calls vanish

**Cons:**
- Virtual dispatch overhead (mitigated by sealed classes and devirtualization)
- Logger reference stored in object

---

### 4. Source Generators for Automatic Instrumentation

**Best for:** Maximum detail with minimal manual effort.

```csharp
// Lexer.cs
[DebugInstrument("Lexer")]
public partial class Lexer
{
    [Instrument]
    private Token ReadString()
    {
        // Just the actual logic, no logging code
        var quote = _source[_position];
        // ...
        return token;
    }
}

// Generated code (Lexer.g.cs)
public partial class Lexer
{
    private Token ReadString()
    {
        if (_logger?.IsEnabled ?? false)
        {
            _logger.LogMethodEnter("ReadString", _line, _column);
            try
            {
                var result = ReadStringCore();
                _logger.LogMethodExit("ReadString", result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogMethodException("ReadString", ex);
                throw;
            }
        }
        return ReadStringCore();
    }

    private Token ReadStringCore()
    {
        // Original method body moved here
        var quote = _source[_position];
        // ...
        return token;
    }
}
```

**Pros:**
- Zero manual logging code
- Consistent instrumentation
- Complete control over generated code
- Can generate different code for Debug vs Release

**Cons:**
- Complexity of source generator
- Debugging generated code can be tricky
- Compilation time increases

---

### 5. Event-Based Logging (for Performance Profiling)

**Best for:** Production performance analysis and telemetry.

```csharp
public sealed class CompilerEventSource : EventSource
{
    public static readonly CompilerEventSource Log = new();

    [Event(1, Level = EventLevel.Verbose)]
    public void TokenRead(string tokenType, int line, int column)
    {
        if (IsEnabled())
            WriteEvent(1, tokenType, line, column);
    }

    [Event(2, Level = EventLevel.Informational)]
    public void ParsePhaseStart(string phase)
    {
        if (IsEnabled())
            WriteEvent(2, phase);
    }

    [Event(3, Level = EventLevel.Informational,
           Task = Tasks.Parse, Opcode = EventOpcode.Start)]
    public void ParseStart(string filename, int tokenCount)
    {
        if (IsEnabled())
            WriteEvent(3, filename, tokenCount);
    }

    [Event(4, Level = EventLevel.Informational,
           Task = Tasks.Parse, Opcode = EventOpcode.Stop)]
    public void ParseStop(string filename, int nodeCount)
    {
        if (IsEnabled())
            WriteEvent(4, filename, nodeCount);
    }

    public static class Tasks
    {
        public const EventTask Parse = (EventTask)1;
        public const EventTask Lex = (EventTask)2;
    }
}

// Usage
public class Lexer
{
    private Token NextToken()
    {
        // ... logic ...
        CompilerEventSource.Log.TokenRead(token.Type.ToString(), token.Line, token.Column);
        return token;
    }
}
```

**Pros:**
- ETW/EventPipe integration for production monitoring
- Extremely low overhead (microseconds per event)
- Works with PerfView, dotnet-trace, etc.
- Structured, typed events
- Can correlate with system events

**Cons:**
- Less readable than traditional logs
- Requires external tools to view
- Learning curve for EventSource

---

## Recommended Hybrid Approach

Combine multiple strategies for different use cases:

### For Development & Unit Tests
Use **Approach 3 (Null Object Pattern)** with rich logging implementations:

```csharp
public class ConsoleCompilerLogger : ICompilerLogger
{
    private readonly CompilerLogLevel _minLevel;

    public void LogTokenRead(TokenType type, int line, int column, string value)
    {
        if (_minLevel <= CompilerLogLevel.Trace)
            Console.WriteLine($"[LEXER] Token: {type,-20} @ L{line}:C{column} = '{value}'");
    }
}

public class StructuredJsonLogger : ICompilerLogger
{
    private readonly StreamWriter _writer;

    public void LogTokenRead(TokenType type, int line, int column, string value)
    {
        var entry = new
        {
            timestamp = DateTime.UtcNow,
            component = "lexer",
            event_type = "token_read",
            token_type = type.ToString(),
            line,
            column,
            value
        };
        _writer.WriteLine(JsonSerializer.Serialize(entry));
    }
}
```

### For Production Performance Monitoring
Use **Approach 5 (EventSource)** for high-level metrics:

```csharp
public class Parser
{
    public Module ParseModule()
    {
        var sw = Stopwatch.StartNew();
        CompilerEventSource.Log.ParseStart(_filename, _tokens.Count);

        try
        {
            var module = ParseModuleCore();
            CompilerEventSource.Log.ParseStop(_filename, CountNodes(module));
            return module;
        }
        finally
        {
            CompilerEventSource.Log.ParseDuration(_filename, sw.ElapsedMilliseconds);
        }
    }
}
```

### For Release Builds (Optional)
Use **Approach 2 (Static Readonly Flag)** to allow emergency debugging:

```csharp
public class Lexer
{
    private static readonly bool _diagnosticsEnabled =
        Environment.GetEnvironmentVariable("SHARPY_DIAGNOSTICS") == "1" ||
        IsDebuggerAttached();

    private readonly ICompilerLogger _logger;

    public Lexer(string source, ICompilerLogger? logger = null)
    {
        _source = source;
        _logger = _diagnosticsEnabled ? (logger ?? NullLogger.Instance) : NullLogger.Instance;
    }
}
```

---

## Logging Levels

Define clear verbosity levels:

```csharp
public enum CompilerLogLevel
{
    None = 0,      // No logging
    Error = 1,     // Only errors
    Warning = 2,   // Errors and warnings
    Info = 3,      // High-level phase information
    Debug = 4,     // Detailed operational info
    Trace = 5      // Every token, every parse rule
}
```

**Examples by level:**

- **Error**: Lexer errors, parser errors
- **Warning**: Deprecated syntax, suspicious patterns
- **Info**: "Lexing started", "Parsing completed", "Generated 42 AST nodes"
- **Debug**: "Entering ParseFunctionDef", "Parsed parameter 'x: int'"
- **Trace**: "Token: Identifier 'foo'", "Indent level changed 0→4"

---

## Implementation in sharpyc CLI

Add logging options to the CLI:

```csharp
var logLevelOption = new Option<CompilerLogLevel>(
    name: "--log-level",
    description: "Set logging verbosity",
    getDefaultValue: () => CompilerLogLevel.None
);

var logFileOption = new Option<FileInfo?>(
    name: "--log-file",
    description: "Write logs to file instead of stderr"
);

var logFormatOption = new Option<LogFormat>(
    name: "--log-format",
    description: "Log format: text or json",
    getDefaultValue: () => LogFormat.Text
);

// Usage
ICompilerLogger logger = (logLevel, logFile, logFormat) switch
{
    (CompilerLogLevel.None, _, _) => NullLogger.Instance,
    (_, null, LogFormat.Text) => new ConsoleCompilerLogger(logLevel),
    (_, null, LogFormat.Json) => new JsonConsoleLogger(logLevel),
    (_, var file, LogFormat.Text) => new FileCompilerLogger(file, logLevel),
    (_, var file, LogFormat.Json) => new StructuredJsonLogger(file, logLevel),
};

var lexer = new Lexer(source, logger);
```

Example usage:
```bash
# No logging (fast)
sharpyc input.spy --emit-tokens

# Verbose logging to console
sharpyc input.spy --emit-tokens --log-level trace

# Structured JSON logs to file for analysis
sharpyc input.spy --emit-ast --log-level debug --log-format json --log-file debug.jsonl

# Production mode with EventSource (use external tools)
dotnet-trace collect --process-id $PID --providers Microsoft-Sharpy-Compiler:Informational
```

---

## Performance Considerations

### Measurement Results (Hypothetical)

Based on typical .NET JIT optimizations:

| Approach | Overhead (Logging Disabled) | Overhead (Logging Enabled) |
|----------|----------------------------|----------------------------|
| `#if DEBUG` | 0% (eliminated) | N/A |
| Static readonly | <0.5% (JIT eliminates) | Depends on logger |
| Null Object Pattern | <1% (inlined) | Depends on logger |
| EventSource | <0.1% (check only) | ~2-5% (fast path) |
| Source Generator | <0.1% (eliminated) | Depends on logger |

### Best Practices

1. **Avoid string allocations in hot paths:**
   ```csharp
   // BAD - allocates even if logging disabled
   _logger.LogTrace($"Processing token {token.Type}");

   // GOOD - uses structured logging
   _logger.LogTokenRead(token.Type, token.Line, token.Column);
   ```

2. **Batch operations when possible:**
   ```csharp
   // Collect tokens, log summary instead of each token
   var tokens = lexer.TokenizeAll();
   _logger.LogTokenizationComplete(tokens.Count, stopwatch.ElapsedMilliseconds);
   ```

3. **Use readonly structs for log data:**
   ```csharp
   public readonly struct TokenLogData
   {
       public TokenType Type { get; init; }
       public int Line { get; init; }
       public int Column { get; init; }
       public string Value { get; init; }
   }
   ```

4. **Leverage interpolated string handlers (.NET 6+):**
   ```csharp
   // Only allocates if logging is enabled
   _logger.LogTrace($"Token {token.Type} at {token.Line}:{token.Column}");
   // Compiler transforms to:
   // if (_logger.IsEnabled(LogLevel.Trace))
   //     _logger.Log(new InterpolatedStringHandler(...));
   ```

---

## Debugging Scenarios

### Scenario 1: "Why did this file fail to parse?"

```bash
sharpyc problematic.spy --log-level debug --log-file parse-debug.log
# Review parse-debug.log to see where parsing failed
```

### Scenario 2: "Which tokens are generated for this syntax?"

```bash
sharpyc test.spy --emit-tokens --log-level trace
# Shows both token output AND lexer internal state
```

### Scenario 3: "Performance regression investigation"

```bash
# Before change
dotnet-trace collect --process-id $PID --providers Microsoft-Sharpy-Compiler --output before.nettrace

# After change
dotnet-trace collect --process-id $PID --providers Microsoft-Sharpy-Compiler --output after.nettrace

# Compare in PerfView or dotnet-trace analyze
```

### Scenario 4: "AST visualization for test cases"

```bash
sharpyc complex.spy --emit-ast --log-format json | jq '.nodes[] | select(.type=="FunctionDef")'
# Extract all function definitions from AST log
```

---

## Future Enhancements

1. **Visualization Tools**
   - Web-based AST viewer that consumes JSON logs
   - Token stream visualizer showing position, type, value
   - Parse tree animator showing recursive descent

2. **Interactive Debugging**
   - REPL mode: `sharpyc repl --debug`
   - Step through parsing one rule at a time
   - Inspect parser state (token position, stack, etc.)

3. **Regression Testing**
   - Record logs during successful parse
   - Replay and diff logs to detect changes
   - Automated "golden file" testing

4. **OpenTelemetry Integration**
   - Distributed tracing for multi-file compilation
   - Metrics export (tokens/sec, parse time per file)
   - Correlation with build system telemetry

---

## Conclusion

**Recommended Implementation Plan:**

1. **Phase 1** (MVP): Implement Null Object Pattern (Approach 3)
   - Define `ICompilerLogger` interface
   - Create `NullLogger` and `ConsoleCompilerLogger`
   - Add logging to Lexer and Parser constructors
   - Add `--log-level` flag to sharpyc

2. **Phase 2**: Add EventSource (Approach 5)
   - High-level metrics only (parse start/stop, token count)
   - Document how to use with dotnet-trace
   - Add performance benchmarks

3. **Phase 3**: Optional enhancements
   - JSON structured logger
   - File output option
   - Visualization tools

This approach provides:
- ✅ Zero overhead in production (Null Object Pattern inlined)
- ✅ Rich debugging when needed (Console/File loggers)
- ✅ Performance monitoring (EventSource)
- ✅ Clean, maintainable code
- ✅ Easy to test and extend
