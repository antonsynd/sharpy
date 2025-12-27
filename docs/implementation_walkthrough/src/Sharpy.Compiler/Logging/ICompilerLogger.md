# Walkthrough: ICompilerLogger.cs

**Source File**: `src/Sharpy.Compiler/Logging/ICompilerLogger.cs`

---

## 1. Overview

The `ICompilerLogger` interface is the **central logging abstraction** for the entire Sharpy compiler pipeline. It defines a contract for observing and recording compiler activities—from low-level tokenization in the lexer to high-level error reporting in semantic analysis.

### Purpose
- **Unified logging interface**: All compiler phases (Lexer, Parser, Semantic Analysis, CodeGen) use this single interface
- **Configurable verbosity**: Support for multiple log levels from silent to trace-level debugging
- **Zero-overhead option**: The `NullLogger` implementation ensures logging doesn't impact performance when disabled
- **Debugging visibility**: Helps compiler developers understand execution flow and diagnose issues

### Role in the Project
This is a **horizontal concern** that cuts across all compiler phases. Think of it as the "observability layer" that sits alongside the core compilation pipeline:

```
Sharpy Source (.spy)
    ↓
[Lexer] ← ICompilerLogger → LogTokenRead(), LogIndentChange()
    ↓
[Parser] ← ICompilerLogger → LogParseEnter(), LogParseExit()
    ↓
[Semantic] ← ICompilerLogger → LogError(), LogWarning()
    ↓
[CodeGen] ← ICompilerLogger → LogInfo(), LogMetrics()
    ↓
C# Output
```

---

## 2. Class/Type Structure

### `ICompilerLogger` Interface

```csharp
public interface ICompilerLogger
{
    // Lexer-specific logging
    void LogTokenRead(string tokenType, int line, int column, string value);
    void LogIndentChange(int oldLevel, int newLevel);
    
    // Parser-specific logging
    void LogParseEnter(string rule, int tokenPosition);
    void LogParseExit(string rule, bool success);
    
    // General logging
    void LogError(string message, int line, int column);
    void LogWarning(string message, int line, int column);
    void LogInfo(string message);
    void LogDebug(string message);
    void LogTrace(string message);
    
    // Special-purpose
    void LogMetrics(string metricsOutput);
    bool IsEnabled(CompilerLogLevel level);
}
```

The interface is intentionally **imperative** (void methods) rather than functional—it's about side effects (outputting logs) rather than returning values.

### `CompilerLogLevel` Enum

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

**Design Note**: This is an **ordered hierarchy**—each level implicitly includes all levels below it. `Warning` includes `Error`, `Debug` includes `Info`, `Warning`, and `Error`, etc.

---

## 3. Key Functions/Methods

### Lexer Methods

#### `LogTokenRead(string tokenType, int line, int column, string value)`

**Purpose**: Record every token produced by the lexer during tokenization.

**When it's called**: Inside `Lexer.cs`, after each token is recognized (identifiers, keywords, operators, literals, etc.)

**Parameters**:
- `tokenType`: The type of token (e.g., `"Identifier"`, `"Plus"`, `"String"`)
- `line`, `column`: Source location for the token
- `value`: The actual text/value of the token (e.g., `"hello_world"` for an identifier)

**Log Level**: `Trace` (most verbose)

**Example Output**:
```
[TRACE] [LEXER] Token: Identifier          @ L1:C1 = 'hello'
[TRACE] [LEXER] Token: LeftParen          @ L1:C6
[TRACE] [LEXER] Token: RightParen         @ L1:C7
```

**Why it matters**: When the lexer appears to be misinterpreting source code, examining token-by-token output reveals exactly what the lexer "sees." This is invaluable for debugging tokenization issues like:
- Wrong token types being assigned
- String literal edge cases
- F-string interpolation problems
- Indentation/dedentation bugs

---

#### `LogIndentChange(int oldLevel, int newLevel)`

**Purpose**: Track Python-style indentation changes that generate `INDENT`/`DEDENT` tokens.

**When it's called**: When the lexer detects a change in indentation level at the start of a new line (within `Lexer.cs`'s indentation tracking logic).

**Parameters**:
- `oldLevel`: Previous indentation level (number of spaces/tabs)
- `newLevel`: New indentation level

**Log Level**: `Trace`

**Example Output**:
```
[TRACE] [LEXER] Indent: 0 → 4
[TRACE] [LEXER] Indent: 4 → 8
[TRACE] [LEXER] Indent: 8 → 4
```

**Why it matters**: Sharpy uses significant whitespace like Python. Indentation bugs are notoriously difficult to debug without visibility into how the lexer interprets whitespace. This method reveals:
- When `INDENT` tokens are generated
- When `DEDENT` tokens are generated
- How many `DEDENT` tokens are needed for multi-level dedentation

---

### Parser Methods

#### `LogParseEnter(string rule, int tokenPosition)`

**Purpose**: Mark entry into a parsing rule during recursive descent parsing.

**When it's called**: At the beginning of each parsing method in `Parser.cs` (e.g., `ParseStatement()`, `ParseExpression()`, `ParseFunctionDef()`).

**Parameters**:
- `rule`: Name of the grammar rule being entered (e.g., `"FunctionDef"`, `"IfStatement"`)
- `tokenPosition`: Current position in the token stream

**Log Level**: `Debug`

**Example Output**:
```
[DEBUG] [PARSER] Enter: FunctionDef @ token 5
[DEBUG] [PARSER] Enter: ParameterList @ token 7
[DEBUG] [PARSER] Enter: TypeAnnotation @ token 10
```

**Why it matters**: Recursive descent parsers can be hard to trace mentally. This shows:
- The order in which parsing rules are attempted
- How deeply nested the parser recurses
- Which rule the parser is in when it fails

---

#### `LogParseExit(string rule, bool success)`

**Purpose**: Mark exit from a parsing rule with success/failure status.

**When it's called**: At the end of each parsing method, right before returning.

**Parameters**:
- `rule`: Name of the grammar rule being exited
- `success`: Whether the rule successfully parsed its input

**Log Level**: `Debug`

**Example Output**:
```
[DEBUG] [PARSER] Exit:  TypeAnnotation ✓
[DEBUG] [PARSER] Exit:  ParameterList ✓
[DEBUG] [PARSER] Exit:  FunctionDef ✓
```

**Why it matters**: Combined with `LogParseEnter`, you can see the **parse tree traversal** in real-time:
- Which rules succeed vs. fail
- How backtracking works (if implemented)
- Where parser gets stuck (last successful exit before error)

---

### Diagnostic Methods

#### `LogError(string message, int line, int column)`

**Purpose**: Report compilation errors that prevent successful compilation.

**When it's called**: Throughout the compiler when fatal issues are detected:
- Lexer: Unterminated strings, invalid characters
- Parser: Syntax errors, unexpected tokens
- Semantic: Type mismatches, undefined names, access violations

**Parameters**:
- `message`: Human-readable error description
- `line`, `column`: Source location of the error

**Log Level**: `Error` (always shown unless logging is completely disabled)

**Example Output**:
```
[ERROR] @ L5:C10: Undefined variable 'counter'
[ERROR] @ L12:C20: Type mismatch: expected 'int', got 'str'
```

**Why it matters**: These are the **user-facing errors** that prevent compilation. The line/column info is critical for IDEs and CLI error reporting.

---

#### `LogWarning(string message, int line, int column)`

**Purpose**: Report potential issues that don't prevent compilation but may indicate problems.

**When it's called**: For non-fatal issues detected during semantic analysis:
- Unused variables
- Shadowed names
- Suspicious type conversions

**Parameters**: Same as `LogError`

**Log Level**: `Warning`

**Example Output**:
```
[WARN]  @ L8:C5: Variable 'result' assigned but never used
[WARN]  @ L15:C10: Function parameter shadows outer variable 'x'
```

---

### General Methods

#### `LogInfo(string message)`

**Purpose**: Log high-level phase transitions and major operations.

**When it's called**: At the start/end of major compiler phases:
- "Starting lexical analysis"
- "Parser initialized, token count: 245"
- "Semantic analysis complete"
- "Code generation started"

**Parameters**:
- `message`: Free-form informational message

**Log Level**: `Info`

**Example Output**:
```
[INFO]  Starting module parsing
[INFO]  Semantic analysis complete in 45ms
[INFO]  Generated C# code for module 'myapp'
```

**Why it matters**: Gives high-level visibility into what the compiler is doing without drowning in details. Useful for performance profiling and understanding compilation flow.

---

#### `LogDebug(string message)`

**Purpose**: Log detailed operational information for debugging.

**When it's called**: For detailed internal state that's too verbose for `Info` but not token-by-token like `Trace`:
- Symbol table operations
- Type resolution steps
- Scope entering/exiting

**Parameters**:
- `message`: Detailed debug information

**Log Level**: `Debug`

**Example Output**:
```
[DEBUG] Resolving name 'MyClass' in module scope
[DEBUG] Type inference: variable 'x' inferred as 'int'
[DEBUG] Entering scope for function 'calculate'
```

---

#### `LogTrace(string message)`

**Purpose**: Log extremely verbose, trace-level information.

**When it's called**: For the most detailed possible logging—often every iteration of a loop or every step in an algorithm.

**Parameters**:
- `message`: Trace-level detail

**Log Level**: `Trace` (highest verbosity)

**Example Output**:
```
[TRACE] Checking symbol 'x' in current scope
[TRACE] No match found, checking parent scope
[TRACE] Symbol 'x' found in parent scope at depth 2
```

---

#### `LogMetrics(string metricsOutput)`

**Purpose**: Output compilation metrics and performance data.

**When it's called**: Typically at the end of compilation to report statistics like:
- Total compilation time
- Token count
- AST node count
- Memory usage

**Parameters**:
- `metricsOutput`: Pre-formatted metrics string

**Log Level**: This method **bypasses level checking**—metrics are always output if called.

**Example Output**:
```
=== Compilation Metrics ===
Total time:    123ms
Tokens:        1,450
AST nodes:     892
Peak memory:   45 MB
```

---

#### `IsEnabled(CompilerLogLevel level)`

**Purpose**: Check if a specific log level is enabled **before** doing expensive logging work.

**When it's called**: Before building complex log messages that require allocations or computation:

```csharp
// Avoid expensive string building if debug logging is disabled
if (_logger.IsEnabled(CompilerLogLevel.Debug))
{
    var detailedInfo = BuildExpensiveDebugInfo();
    _logger.LogDebug(detailedInfo);
}
```

**Parameters**:
- `level`: The log level to check

**Return Value**: `true` if that level (or higher) is enabled

**Why it matters**: **Performance optimization**—prevents unnecessary work when logging is disabled or at a lower level.

---

## 4. Dependencies

### Implementations in the Codebase

The interface has two concrete implementations:

#### `ConsoleCompilerLogger` (`ConsoleCompilerLogger.cs`)
- Writes logs to `Console.Out` (info/debug/trace) and `Console.Error` (errors/warnings)
- Respects minimum log level configuration
- Used for CLI compilation and development debugging
- Includes nice formatting with prefixes like `[ERROR]`, `[DEBUG]`, etc.

#### `NullLogger` (`NullLogger.cs`)
- **Null Object Pattern** implementation
- All methods are no-ops (do nothing)
- Uses `[MethodImpl(MethodImplOptions.AggressiveInlining)]` for zero overhead
- Singleton instance: `NullLogger.Instance`
- Used by default when no logger is specified (performance mode)

### Usage Across Compiler Phases

**Lexer** (`Lexer/Lexer.cs`):
```csharp
private readonly ICompilerLogger _logger;

// In constructor
_logger = logger ?? NullLogger.Instance;

// During tokenization
_logger.LogTokenRead(token.Type.ToString(), token.Line, token.Column, token.Value);
```

**Parser** (`Parser/Parser.cs`):
```csharp
private readonly ICompilerLogger _logger;

// Default to NullLogger if not provided
public Parser(List<Token> tokens, ICompilerLogger? logger = null)
{
    _logger = logger ?? NullLogger.Instance;
}

// At start of parsing methods
_logger.LogParseEnter("FunctionDef", _position);
```

**Semantic Analysis** (`Semantic/*`):
- `NameResolver.cs`: Uses logger for resolution steps
- `TypeChecker.cs`: Logs errors/warnings for type mismatches
- `TypeResolver.cs`: Logs type resolution progress

**Code Generation** (`CodeGen/RoslynEmitter.cs`):
- Logs info about C# generation phases
- Reports metrics on generated code

---

## 5. Patterns and Design Decisions

### Strategy Pattern
The `ICompilerLogger` is a classic **Strategy Pattern**—the compiler phases depend on an abstraction, and different implementations can be swapped at runtime:
- `ConsoleCompilerLogger` for interactive CLI use
- `NullLogger` for production/performance
- Future: `FileLogger`, `StructuredLogger`, `TestLogger`

### Null Object Pattern
`NullLogger.Instance` ensures **no null checks** are needed throughout the compiler:

```csharp
// No need for:
_logger?.LogInfo("Starting...");  // ❌

// Can always call directly:
_logger.LogInfo("Starting...");   // ✓
```

### Performance Considerations

#### Aggressive Inlining
`NullLogger` uses `[MethodImpl(MethodImplOptions.AggressiveInlining)]` on all methods. This tells the JIT compiler to inline these no-op methods, resulting in **zero runtime overhead** when logging is disabled.

#### Guard Pattern
The `IsEnabled()` method supports the **Guard Pattern** to avoid expensive work:

```csharp
// Bad: Always builds the string, even if not logged
_logger.LogDebug($"Complex info: {ExpensiveOperation()}");

// Good: Only does work if debug logging is enabled
if (_logger.IsEnabled(CompilerLogLevel.Debug))
{
    _logger.LogDebug($"Complex info: {ExpensiveOperation()}");
}
```

### Ordered Log Levels
The enum is **numerically ordered**—this enables simple comparison:

```csharp
// In ConsoleCompilerLogger
if (_minLevel >= CompilerLogLevel.Trace)
{
    // Log trace-level message
}
```

Higher numbers = more verbose. This is intuitive and efficient.

### Separation of Concerns
Notice that logging is **separate from error handling**:
- `LogError()` doesn't throw exceptions—it just records the error
- The caller is responsible for error handling logic
- This keeps logging as a pure observability concern

---

## 6. Debugging Tips

### Debugging the Lexer

**Problem**: Lexer produces wrong tokens or crashes

**Solution**: Run with `Trace` level and examine `LogTokenRead()` output:

```bash
# Enable trace logging
dotnet run --project src/Sharpy.Cli -- build myfile.spy --log-level trace

# Look for the token sequence around the error
```

Look for:
- Unexpected token types (e.g., `Identifier` when you expected `Keyword`)
- Missing tokens
- Wrong line/column numbers
- Incorrect token values (especially for strings and numbers)

### Debugging the Parser

**Problem**: Parser fails with "unexpected token" error

**Solution**: Enable `Debug` level to see parse rule entry/exit:

```bash
dotnet run --project src/Sharpy.Cli -- build myfile.spy --log-level debug
```

Look at the **last successful `LogParseExit`** before the error—that tells you which rule succeeded just before failure. Then look at what rule was entered but never exited successfully.

**Example**:
```
[DEBUG] [PARSER] Enter: FunctionDef @ token 10
[DEBUG] [PARSER] Enter: ParameterList @ token 12
[DEBUG] [PARSER] Exit:  ParameterList ✓
[DEBUG] [PARSER] Enter: TypeAnnotation @ token 20
[ERROR] @ L5:C10: Expected ':' after function signature
```

→ Problem is in `TypeAnnotation` parsing after `ParameterList` succeeded.

### Debugging Semantic Analysis

**Problem**: Type errors or undefined name errors

**Solution**: Look for `LogError` and `LogWarning` calls from semantic analysis passes:
- `NameResolver` logs when names can't be resolved
- `TypeChecker` logs type mismatches
- Look at the line/column to locate the source of the issue

### Adding Custom Debug Logging

When debugging a specific issue, add temporary logging:

```csharp
// In the code you're debugging
_logger.LogDebug($"My variable state: x={x}, y={y}");

if (_logger.IsEnabled(CompilerLogLevel.Trace))
{
    _logger.LogTrace($"Detailed state: {DumpComplexState()}");
}
```

Then run with appropriate log level to see your messages.

### Understanding Log Output Structure

**Format**: `[LEVEL] [COMPONENT] Message`

- `[ERROR]`, `[WARN]`, `[INFO]`, `[DEBUG]`, `[TRACE]`: Log level
- `[LEXER]`, `[PARSER]`, `[SEMANTIC]`: Compiler phase (in ConsoleCompilerLogger)
- `@ L{line}:C{column}`: Source location for errors/warnings

### Using Grep to Filter Logs

When logs are overwhelming:

```bash
# Only show errors
dotnet run ... 2>&1 | grep '\[ERROR\]'

# Only show parser activity
dotnet run ... 2>&1 | grep '\[PARSER\]'

# Only show line 42 issues
dotnet run ... 2>&1 | grep '@ L42:'
```

---

## 7. Contribution Guidelines

### Adding a New Log Method

If you need a new kind of logging (unlikely—the interface is quite complete):

1. **Add the method to `ICompilerLogger` interface**
2. **Implement in `ConsoleCompilerLogger`** with appropriate level checking
3. **Add a no-op to `NullLogger`** with `[MethodImpl(MethodImplOptions.AggressiveInlining)]`
4. **Update documentation** (this file!)

**Example**:
```csharp
// ICompilerLogger.cs
void LogOptimization(string pass, string transformation);

// ConsoleCompilerLogger.cs
public void LogOptimization(string pass, string transformation)
{
    if (_minLevel >= CompilerLogLevel.Debug)
    {
        _output.WriteLine($"[DEBUG] [OPTIMIZATION] {pass}: {transformation}");
    }
}

// NullLogger.cs
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public void LogOptimization(string pass, string transformation) { }
```

### Adding a New Logger Implementation

To create a new logger (e.g., `FileLogger`, `StructuredJsonLogger`):

1. **Create a new class implementing `ICompilerLogger`**
2. **Implement all interface methods**
3. **Consider performance**—don't make logging expensive
4. **Handle `IsEnabled()` correctly**
5. **Test with different log levels**

**Example skeleton**:
```csharp
public class FileLogger : ICompilerLogger
{
    private readonly StreamWriter _writer;
    private readonly CompilerLogLevel _minLevel;

    public FileLogger(string path, CompilerLogLevel minLevel)
    {
        _writer = new StreamWriter(path, append: true);
        _minLevel = minLevel;
    }

    public void LogError(string message, int line, int column)
    {
        if (_minLevel >= CompilerLogLevel.Error)
        {
            _writer.WriteLine($"{DateTime.UtcNow:O} [ERROR] @ L{line}:C{column}: {message}");
            _writer.Flush();  // Ensure errors are written immediately
        }
    }

    // ... implement other methods ...

    public bool IsEnabled(CompilerLogLevel level) => _minLevel >= level;

    // Don't forget IDisposable if you have resources!
    public void Dispose() => _writer?.Dispose();
}
```

### Adding Logging to New Compiler Phases

When adding a new compiler phase:

1. **Accept `ICompilerLogger?` in constructor**
2. **Default to `NullLogger.Instance` if null**
3. **Use appropriate log levels**:
   - `Error`/`Warning`: For diagnostics users need to see
   - `Info`: For phase start/end messages
   - `Debug`: For detailed internal operations
   - `Trace`: For extremely verbose step-by-step logging
4. **Use `IsEnabled()` guard** for expensive log message construction
5. **Include source locations** in errors/warnings when available

**Example**:
```csharp
public class MyNewPhase
{
    private readonly ICompilerLogger _logger;

    public MyNewPhase(ICompilerLogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
    }

    public void Process(Module module)
    {
        _logger.LogInfo("Starting MyNewPhase processing");

        foreach (var item in module.Items)
        {
            if (_logger.IsEnabled(CompilerLogLevel.Debug))
            {
                _logger.LogDebug($"Processing item: {item.Name}");
            }

            // ... processing logic ...

            if (error)
            {
                _logger.LogError($"Failed to process: {reason}",
                    item.Line, item.Column);
            }
        }

        _logger.LogInfo("MyNewPhase processing complete");
    }
}
```

### Testing Considerations

**Don't test logging in unit tests** (usually). Logging is a side effect, and testing side effects adds brittleness. Instead:

- Use `NullLogger.Instance` in tests by default
- If you need to verify specific errors were logged, create a `TestLogger` that captures messages:

```csharp
public class TestLogger : ICompilerLogger
{
    public List<(string message, int line, int column)> Errors { get; } = new();

    public void LogError(string message, int line, int column)
    {
        Errors.Add((message, line, column));
    }

    // ... other methods can be no-ops or capture as needed ...
}

// In test
var testLogger = new TestLogger();
var compiler = new Compiler(source, logger: testLogger);
compiler.Compile();

Assert.Contains(testLogger.Errors,
    e => e.message.Contains("undefined variable"));
```

### Performance Guidelines

**Do**:
- ✅ Use `IsEnabled()` before expensive operations
- ✅ Use `NullLogger` in production/release builds
- ✅ Keep log messages simple (avoid complex string building)
- ✅ Use string interpolation for simple cases

**Don't**:
- ❌ Don't call methods that do expensive work in log messages
- ❌ Don't log inside tight loops without `IsEnabled()` guard
- ❌ Don't allocate large objects just for logging
- ❌ Don't do I/O operations in log message construction

### Documentation

When making changes to this file or related logging code:

1. **Update this walkthrough** if behavior changes
2. **Update XML doc comments** in the source code
3. **Add examples** to demonstrate new functionality
4. **Note breaking changes** prominently

---

## Summary

The `ICompilerLogger` interface is the **observability backbone** of the Sharpy compiler. It provides:

- **Uniform logging contract** across all compiler phases
- **Flexible verbosity control** from silent to trace-level
- **Zero-overhead option** via `NullLogger` and aggressive inlining
- **Rich debugging support** for lexer, parser, and semantic analysis

When debugging compiler issues, appropriate use of logging levels can make the difference between hours of frustration and quick problem identification. The interface is stable and comprehensive—most contributions will involve *using* the logger effectively rather than modifying the interface itself.

**Key Takeaway**: Always accept `ICompilerLogger?` in new compiler components and default to `NullLogger.Instance`. Use guard patterns for expensive logging, and choose the right log level for your message's intended audience.
