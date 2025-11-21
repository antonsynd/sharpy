# Walkthrough: ConsoleCompilerLogger.cs

**Source File**: `src/Sharpy.Compiler/Logging/ConsoleCompilerLogger.cs`

---

## 1. Overview

`ConsoleCompilerLogger` is the primary implementation of the compiler's logging infrastructure, providing human-readable console output for debugging and monitoring the compilation process. It's a concrete implementation of the `ICompilerLogger` interface that writes formatted log messages to configurable text streams (typically `Console.Out` and `Console.Error`).

**Role in the Project:**
- Provides visibility into the compilation pipeline (lexer → parser → semantic analysis → code generation)
- Enables debugging through different verbosity levels (Error → Warning → Info → Debug → Trace)
- Helps developers understand what's happening during compilation
- Used throughout the compiler components (Lexer, Parser, TypeChecker, etc.)

**Key Design Points:**
- Supports configurable log levels for filtering noise
- Separates normal output from error output (stdout vs stderr)
- Testable through dependency injection of TextWriter streams
- Thread-safe for concurrent logging scenarios

---

## 2. Class/Type Structure

### ConsoleCompilerLogger Class

```csharp
public sealed class ConsoleCompilerLogger : ICompilerLogger
```

**Access Modifier:** `public sealed` - This class is publicly accessible but cannot be inherited. The `sealed` modifier prevents subclassing, which is appropriate since this is a concrete implementation with no intended extension points.

**Fields:**

```csharp
private readonly CompilerLogLevel _minLevel;
private readonly TextWriter _output;
private readonly TextWriter _errorOutput;
```

- **`_minLevel`**: The minimum log level required for a message to be output. Acts as a filter - messages below this level are silently discarded.
- **`_output`**: TextWriter for normal log messages (Info, Debug, Trace). Defaults to `Console.Out`.
- **`_errorOutput`**: TextWriter for error/warning messages. Defaults to `Console.Error`.

**Why separate output streams?** This follows Unix conventions where normal output and error output are separated, allowing users to redirect them independently (e.g., `sharpyc build file.spy 2> errors.txt`).

### Related Types (in ICompilerLogger.cs)

**CompilerLogLevel Enum:**

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

This is an **ordered enum** where higher values indicate more verbose logging. The comparison `_minLevel >= CompilerLogLevel.Trace` works because of this ordering.

---

## 3. Key Functions/Methods

### Constructor

```csharp
public ConsoleCompilerLogger(CompilerLogLevel minLevel, TextWriter? output = null, TextWriter? errorOutput = null)
{
    _minLevel = minLevel;
    _output = output ?? Console.Out;
    _errorOutput = errorOutput ?? Console.Error;
}
```

**Purpose:** Initialize the logger with a minimum log level and optional custom output streams.

**Parameters:**
- `minLevel`: The verbosity threshold. Only messages at or above this level will be logged.
- `output`: Optional TextWriter for normal messages. If `null`, uses `Console.Out`.
- `errorOutput`: Optional TextWriter for error/warning messages. If `null`, uses `Console.Error`.

**Why nullable parameters with default values?**
- Enables testing by injecting `StringWriter` instead of actual console
- Allows redirecting output to files or other streams
- Provides sensible defaults for normal CLI usage

**Usage Example (from Sharpy.Cli):**
```csharp
// Production use - log to console
var logger = new ConsoleCompilerLogger(CompilerLogLevel.Info);

// Testing - capture output
var testOutput = new StringWriter();
var logger = new ConsoleCompilerLogger(CompilerLogLevel.Debug, testOutput, testOutput);
```

### Lexer Logging Methods

#### LogTokenRead

```csharp
public void LogTokenRead(string tokenType, int line, int column, string value)
{
    if (_minLevel >= CompilerLogLevel.Trace)
    {
        var valueDisplay = string.IsNullOrEmpty(value) ? "" : $" = '{value}'";
        _output.WriteLine($"[TRACE] [LEXER] Token: {tokenType,-20} @ L{line}:C{column}{valueDisplay}");
    }
}
```

**Purpose:** Log each token as it's recognized by the lexer.

**Key Details:**
- **Guard clause:** `if (_minLevel >= CompilerLogLevel.Trace)` - Early return if trace logging is disabled, avoiding string formatting overhead
- **String formatting:** `{tokenType,-20}` uses left-aligned 20-character padding for readable column alignment
- **Conditional value display:** Only shows token value if it's not empty (e.g., identifiers, literals)
- **Location info:** `L{line}:C{column}` provides source code position for debugging

**Example Output:**
```
[TRACE] [LEXER] Token: Identifier          @ L1:C1 = 'hello'
[TRACE] [LEXER] Token: LeftParen           @ L1:C6
[TRACE] [LEXER] Token: StringLiteral       @ L1:C7 = 'world'
```

**When is this useful?** When debugging lexer issues or understanding how source code is tokenized. At `Trace` level, you see *every* token.

#### LogIndentChange

```csharp
public void LogIndentChange(int oldLevel, int newLevel)
{
    if (_minLevel >= CompilerLogLevel.Trace)
    {
        _output.WriteLine($"[TRACE] [LEXER] Indent: {oldLevel} → {newLevel}");
    }
}
```

**Purpose:** Log Python-style indentation changes (critical in Sharpy's syntax).

**Why this matters:** Sharpy uses significant whitespace like Python. Indentation changes trigger `INDENT` and `DEDENT` tokens. This logging helps debug indentation-related parsing errors.

**Example Output:**
```
[TRACE] [LEXER] Indent: 0 → 4
[TRACE] [LEXER] Indent: 4 → 0
```

### Parser Logging Methods

#### LogParseEnter

```csharp
public void LogParseEnter(string rule, int tokenPosition)
{
    if (_minLevel >= CompilerLogLevel.Debug)
    {
        _output.WriteLine($"[DEBUG] [PARSER] Enter: {rule} @ token {tokenPosition}");
    }
}
```

**Purpose:** Log when the parser enters a grammar rule (e.g., parsing an if-statement, function definition).

**Key Details:**
- Uses `Debug` level (less verbose than `Trace`)
- `rule` is typically the grammar rule name like `"ParseIfStatement"` or `"ParseFunctionDef"`
- `tokenPosition` helps correlate with token stream

**Example Output:**
```
[DEBUG] [PARSER] Enter: ParseFunctionDef @ token 15
[DEBUG] [PARSER] Enter: ParseParameterList @ token 18
```

#### LogParseExit

```csharp
public void LogParseExit(string rule, bool success)
{
    if (_minLevel >= CompilerLogLevel.Debug)
    {
        var status = success ? "✓" : "✗";
        _output.WriteLine($"[DEBUG] [PARSER] Exit:  {rule} {status}");
    }
}
```

**Purpose:** Log when the parser exits a grammar rule, indicating success or failure.

**Visual feedback:** Uses Unicode check mark (✓) or cross (✗) for quick visual scanning.

**Example Output:**
```
[DEBUG] [PARSER] Exit:  ParseFunctionDef ✓
[DEBUG] [PARSER] Exit:  ParseIfStatement ✗
```

**Debugging tip:** A `✗` doesn't always mean an error - it might indicate the parser tried a rule speculatively and backtracked. This is normal in recursive descent parsing.

### Error and Warning Methods

#### LogError

```csharp
public void LogError(string message, int line, int column)
{
    if (_minLevel >= CompilerLogLevel.Error)
    {
        _errorOutput.WriteLine($"[ERROR] @ L{line}:C{column}: {message}");
    }
}
```

**Purpose:** Log compilation errors with source location.

**Key Details:**
- Writes to `_errorOutput` (stderr), not `_output` (stdout)
- Always includes line and column information for IDE integration
- Only filtered if log level is `None` (which is rare)

**Example Output:**
```
[ERROR] @ L10:C5: Type mismatch: expected 'int', got 'str'
```

#### LogWarning

```csharp
public void LogWarning(string message, int line, int column)
{
    if (_minLevel >= CompilerLogLevel.Warning)
    {
        _errorOutput.WriteLine($"[WARN]  @ L{line}:C{column}: {message}");
    }
}
```

**Purpose:** Log non-fatal issues that might indicate problems.

**Difference from errors:** Warnings don't stop compilation, but suggest potential issues (e.g., unused variables, deprecated syntax).

### General Logging Methods

#### LogInfo

```csharp
public void LogInfo(string message)
{
    if (_minLevel >= CompilerLogLevel.Info)
    {
        _output.WriteLine($"[INFO]  {message}");
    }
}
```

**Purpose:** Log high-level compilation phase information.

**Typical use cases:**
- "Starting compilation of project X"
- "Resolved 15 modules"
- "Code generation complete"

**Example Output:**
```
[INFO]  Compiling project: calculator.spyproj
[INFO]  Found 3 source files
[INFO]  Compilation successful
```

#### LogDebug

```csharp
public void LogDebug(string message)
{
    if (_minLevel >= CompilerLogLevel.Debug)
    {
        _output.WriteLine($"[DEBUG] {message}");
    }
}
```

**Purpose:** Log detailed operational information for debugging.

**Example usage:**
```csharp
_logger.LogDebug($"Resolving type reference: {typeName}");
_logger.LogDebug($"Symbol table scope pushed: {scopeName}");
```

#### LogTrace

```csharp
public void LogTrace(string message)
{
    if (_minLevel >= CompilerLogLevel.Trace)
    {
        _output.WriteLine($"[TRACE] {message}");
    }
}
```

**Purpose:** Log extremely detailed trace information.

**Warning:** At `Trace` level, output can be massive (thousands of lines for small programs). Use sparingly and only when needed.

### Utility Methods

#### LogMetrics

```csharp
public void LogMetrics(string metricsOutput)
{
    _output.WriteLine(metricsOutput);
}
```

**Purpose:** Output compilation metrics unconditionally (bypasses log level checks).

**Why bypass the filter?** Metrics are typically requested explicitly (e.g., `--show-metrics` flag) and should always be displayed when requested.

**Example usage:**
```csharp
var metrics = $"Compilation time: {elapsed.TotalMilliseconds}ms\nTokens: {tokenCount}\nAST nodes: {nodeCount}";
logger.LogMetrics(metrics);
```

#### IsEnabled

```csharp
public bool IsEnabled(CompilerLogLevel level) => _minLevel >= level;
```

**Purpose:** Check if a specific log level is enabled before expensive operations.

**Performance optimization pattern:**
```csharp
if (logger.IsEnabled(CompilerLogLevel.Debug))
{
    // Avoid expensive string building if debug logging is disabled
    var detailedMessage = BuildExpensiveDebugMessage();
    logger.LogDebug(detailedMessage);
}
```

**When to use:** When constructing log messages is expensive (e.g., serializing large data structures, complex string formatting).

---

## 4. Dependencies

### Internal Dependencies

**Direct dependency:**
- **`ICompilerLogger`** interface (same namespace) - Defines the logging contract

**Used by (consumers throughout the codebase):**
- `Sharpy.Compiler.Compiler` - Main single-file compiler
- `Sharpy.Compiler.AssemblyCompiler` - Multi-file/project compiler
- `Sharpy.Compiler.Lexer.Lexer` - Tokenization logging
- `Sharpy.Compiler.Parser.Parser` - Parse rule tracing
- `Sharpy.Compiler.Semantic.TypeChecker` - Type checking logs
- `Sharpy.Compiler.Semantic.NameResolver` - Symbol resolution logs
- `Sharpy.Compiler.Semantic.ImportResolver` - Module import logs
- `Sharpy.Compiler.Semantic.ModuleRegistry` - Module caching logs
- `Sharpy.Cli.Program` - CLI initialization

### Framework Dependencies

- **`System.IO.TextWriter`** - Abstract base for output streams
- **`System.Console`** - Default output/error streams

### Sibling Implementations

- **`NullLogger`** - Null object pattern for zero-overhead no-op logging (used in production when logging is disabled)

---

## 5. Patterns and Design Decisions

### 1. Interface Segregation & Dependency Injection

**Pattern:** The logger is injected via the `ICompilerLogger` interface, not the concrete type.

```csharp
public Parser(List<Token> tokens, ICompilerLogger? logger = null)
{
    _tokens = tokens;
    _logger = logger ?? NullLogger.Instance;
}
```

**Benefits:**
- **Testability:** Tests can inject `StringWriter` to capture and verify log output
- **Flexibility:** Easy to add new logger implementations (file logger, structured logger, etc.)
- **Performance:** Production can use `NullLogger` for zero overhead when logging is disabled

### 2. Guard Clauses for Performance

Every log method checks the log level first:

```csharp
if (_minLevel >= CompilerLogLevel.Trace)
{
    // Only format strings if we're actually going to log
    _output.WriteLine($"[TRACE] {expensive_operation()}");
}
```

**Why this matters:** String interpolation and formatting are expensive. The guard clause prevents this overhead when logging is disabled.

**Comparison with alternatives:**
- ❌ Without guard: Always formats strings, then discards them
- ✅ With guard: Skips formatting entirely when disabled

### 3. Separation of Concerns: stdout vs stderr

The logger uses separate streams for normal vs error output:

```csharp
_output.WriteLine($"[INFO] {message}");        // Normal operations
_errorOutput.WriteLine($"[ERROR] {message}");   // Errors/warnings
```

**Real-world benefit:** Users can redirect error output separately:
```bash
sharpyc build file.spy 2> errors.log   # Errors to file, info to console
sharpyc build file.spy > /dev/null     # Suppress info, show errors
```

### 4. Null Object Pattern Integration

Constructor accepts nullable logger but never stores null:

```csharp
_logger = logger ?? NullLogger.Instance;
```

**Advantage:** Eliminates null checks throughout the codebase:
```csharp
// No need for: if (_logger != null) _logger.LogDebug(...)
_logger.LogDebug("message");  // Always safe to call
```

### 5. Sealed Class Design

The class is marked `sealed`, preventing inheritance.

**Rationale:**
- This is a concrete implementation, not an abstraction
- Extension should happen through new `ICompilerLogger` implementations, not inheritance
- Prevents the fragile base class problem
- Slightly improves performance (JIT can devirtualize calls)

### 6. Format Consistency

All log messages follow a consistent format:

```
[LEVEL] [COMPONENT] Message content @ location
```

**Examples:**
```
[TRACE] [LEXER] Token: Identifier @ L1:C5 = 'x'
[DEBUG] [PARSER] Enter: ParseIfStatement @ token 10
[ERROR] @ L5:C12: Type mismatch
[INFO]  Compilation successful
```

**Benefits:**
- Easy to grep/filter logs: `grep '\[ERROR\]' build.log`
- Parseable by log analysis tools
- Consistent user experience

---

## 6. Debugging Tips

### Choosing the Right Log Level

**For typical development:**
```csharp
var logger = new ConsoleCompilerLogger(CompilerLogLevel.Info);
```
Shows high-level progress without overwhelming detail.

**For debugging parser issues:**
```csharp
var logger = new ConsoleCompilerLogger(CompilerLogLevel.Debug);
```
Shows which parse rules are being entered/exited.

**For debugging lexer issues:**
```csharp
var logger = new ConsoleCompilerLogger(CompilerLogLevel.Trace);
```
Shows every token. Warning: Very verbose!

### Capturing Logs in Tests

```csharp
[Fact]
public void TestCompilationLogging()
{
    var output = new StringWriter();
    var logger = new ConsoleCompilerLogger(CompilerLogLevel.Debug, output, output);
    
    // Compile something
    var compiler = new Compiler(logger);
    compiler.Compile("x = 1");
    
    // Verify logs
    var logs = output.ToString();
    Assert.Contains("[DEBUG] [PARSER]", logs);
}
```

### Finding Where Errors Come From

When you see an error log:
```
[ERROR] @ L10:C5: Type mismatch: expected 'int', got 'str'
```

**How to trace it:**
1. Search codebase for the exact error message text
2. Set a breakpoint in `LogError` method
3. Run with debugger to see the call stack
4. Check line 10, column 5 in the source file

### Performance Profiling

If compilation is slow, compare with and without logging:

```csharp
// Baseline - no logging
var noLog = new NullLogger();

// With logging
var logger = new ConsoleCompilerLogger(CompilerLogLevel.Info);
```

Measure the difference. If logging adds significant time, consider:
- Reducing log level
- Using `IsEnabled()` checks before expensive operations
- Using `NullLogger` in production builds

### Common Issues

**Issue:** Logs appear out of order
**Cause:** Mixing `Console.WriteLine()` with logger calls
**Solution:** Always use the logger, never write directly to Console

**Issue:** Trace logs are overwhelming
**Cause:** Using `Trace` level on a large file
**Solution:** Start with `Info`, then `Debug`, only use `Trace` for specific small code sections

**Issue:** Error messages missing from output
**Cause:** Log level set too high (e.g., `None`)
**Solution:** Use at least `Error` level for production

---

## 7. Contribution Guidelines

### Adding New Log Methods

If you need to add a new specialized logging method:

1. **Add to `ICompilerLogger` interface first:**
```csharp
// In ICompilerLogger.cs
void LogSemanticAnalysisPhase(string phaseName, int nodeCount);
```

2. **Implement in `ConsoleCompilerLogger`:**
```csharp
public void LogSemanticAnalysisPhase(string phaseName, int nodeCount)
{
    if (_minLevel >= CompilerLogLevel.Debug)
    {
        _output.WriteLine($"[DEBUG] [SEMANTIC] Phase: {phaseName}, Nodes: {nodeCount}");
    }
}
```

3. **Update `NullLogger`:**
```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public void LogSemanticAnalysisPhase(string phaseName, int nodeCount) { }
```

4. **Document the appropriate log level and format conventions**

### Improving Log Formatting

Current formatting is simple but functional. Potential improvements:

**Color coding** (for terminal output):
```csharp
private string GetLevelColor(CompilerLogLevel level) => level switch
{
    CompilerLogLevel.Error => "\u001b[31m",    // Red
    CompilerLogLevel.Warning => "\u001b[33m",  // Yellow
    CompilerLogLevel.Info => "\u001b[32m",     // Green
    _ => ""
};
```

**Timestamps** (for long-running compilations):
```csharp
var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
_output.WriteLine($"[{timestamp}] [INFO] {message}");
```

**Structured logging** (JSON output for tools):
```csharp
var logEntry = new { Level = "ERROR", Line = line, Column = column, Message = message };
_output.WriteLine(JsonSerializer.Serialize(logEntry));
```

### Adding New Logger Implementations

Want to log to a file or database instead of console?

1. **Create a new class implementing `ICompilerLogger`:**
```csharp
public sealed class FileLogger : ICompilerLogger
{
    private readonly StreamWriter _writer;
    private readonly CompilerLogLevel _minLevel;
    
    // Implement all interface methods...
}
```

2. **Register in the CLI or compiler initialization**

3. **Consider composite pattern for multiple loggers:**
```csharp
public sealed class CompositeLogger : ICompilerLogger
{
    private readonly ICompilerLogger[] _loggers;
    
    public void LogError(string message, int line, int column)
    {
        foreach (var logger in _loggers)
            logger.LogError(message, line, column);
    }
}
```

### Testing Considerations

When modifying this class:

1. **Verify log level filtering works correctly:**
```csharp
var output = new StringWriter();
var logger = new ConsoleCompilerLogger(CompilerLogLevel.Info, output, output);

logger.LogTrace("Should not appear");
logger.LogInfo("Should appear");

Assert.DoesNotContain("Should not appear", output.ToString());
Assert.Contains("Should appear", output.ToString());
```

2. **Test stdout vs stderr separation:**
```csharp
var stdout = new StringWriter();
var stderr = new StringWriter();
var logger = new ConsoleCompilerLogger(CompilerLogLevel.Error, stdout, stderr);

logger.LogInfo("Info message");
logger.LogError("Error message", 1, 1);

Assert.Contains("Info message", stdout.ToString());
Assert.Contains("Error message", stderr.ToString());
```

3. **Verify format consistency:**
- All levels should follow `[LEVEL]` prefix convention
- Location info should use `@ L{line}:C{column}` format
- Component tags should be uppercase: `[LEXER]`, `[PARSER]`, etc.

### Performance Best Practices

When adding logging calls in performance-critical code:

**DO:**
```csharp
if (_logger.IsEnabled(CompilerLogLevel.Debug))
{
    var details = ExpensiveStringBuilder();
    _logger.LogDebug(details);
}
```

**DON'T:**
```csharp
_logger.LogDebug(ExpensiveStringBuilder());  // Always calls ExpensiveStringBuilder()
```

The guard clause in the logger prevents the *output*, but not the string building overhead.

### Documentation

When making changes to logging:

1. Update this walkthrough document if behavior changes significantly
2. Add XML comments for any new public methods
3. Update the CLI documentation if new log levels or flags are added
4. Consider adding examples to `docs/manual/` for user-facing changes

---

## Quick Reference

### Log Levels (in order of verbosity)

| Level     | Use For                                        | Example                                  |
|-----------|------------------------------------------------|------------------------------------------|
| `None`    | Disable all logging                            | Production builds                        |
| `Error`   | Compilation errors only                        | CI/CD pipelines                          |
| `Warning` | Errors + warnings                              | Production with basic diagnostics        |
| `Info`    | High-level compilation phases                  | Normal development (default)             |
| `Debug`   | Detailed parser/semantic operations            | Debugging type checking or parsing       |
| `Trace`   | Every token, every parse rule                  | Debugging lexer or specific parse issues |

### Common Usage Patterns

**CLI initialization:**
```csharp
var logger = new ConsoleCompilerLogger(CompilerLogLevel.Info);
var compiler = new Compiler(logger);
```

**Test setup:**
```csharp
var output = new StringWriter();
var logger = new ConsoleCompilerLogger(CompilerLogLevel.Debug, output, output);
```

**Performance-sensitive logging:**
```csharp
if (_logger.IsEnabled(CompilerLogLevel.Debug))
{
    _logger.LogDebug($"Complex: {ExpensiveOperation()}");
}
```

### Output Format Examples

```
[TRACE] [LEXER] Token: Identifier          @ L1:C1 = 'hello'
[TRACE] [LEXER] Indent: 0 → 4
[DEBUG] [PARSER] Enter: ParseIfStatement @ token 15
[DEBUG] [PARSER] Exit:  ParseIfStatement ✓
[INFO]  Compiling project: myapp.spyproj
[WARN]  @ L10:C5: Unused variable 'x'
[ERROR] @ L15:C12: Type mismatch: expected 'int', got 'str'
```

---

**Last Updated:** 2025-11-21  
**Related Files:**
- `src/Sharpy.Compiler/Logging/ICompilerLogger.cs` - Interface definition
- `src/Sharpy.Compiler/Logging/NullLogger.cs` - No-op implementation
- `src/Sharpy.Cli/Program.cs` - CLI usage example
