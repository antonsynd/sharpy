# Walkthrough: ICompilerLogger.cs

**Source File**: `src/Sharpy.Compiler/Logging/ICompilerLogger.cs`

---

## Overview

The `ICompilerLogger` interface defines the logging contract for the Sharpy compiler. It provides a unified abstraction for capturing diagnostic information, debug traces, and performance metrics across all compilation phases (lexing, parsing, semantic analysis, and code generation).

**Key Purpose**: Enable observability into the compiler's internal workings without coupling compiler components to specific logging implementations.

**Why it matters**: 
- Debugging complex compilation issues requires visibility into what the compiler is doing
- Performance profiling needs metrics from various compilation stages
- Different contexts (CLI, tests, IDE integration) need different logging strategies

---

## Class/Type Structure

### `ICompilerLogger` Interface

The main interface exposing 12 methods organized into four categories:

1. **Compiler-specific logging** (lexer/parser operations)
2. **Standard log levels** (error, warning, info, debug, trace)
3. **Performance metrics**
4. **Level checking** (optimization for expensive log operations)

### `CompilerLogLevel` Enum

Defines a hierarchical verbosity scale from 0 (None) to 5 (Trace):

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

**Design Note**: Higher numbers include all lower levels (hierarchical). Setting level to `Debug` (4) means you get Error, Warning, Info, AND Debug messages.

---

## Key Methods

### Lexer-Specific Logging

#### `LogTokenRead(string tokenType, int line, int column, string value)`

**What it does**: Records each token produced by the lexer.

**When to use**: Typically called at `Trace` level (5) from `Lexer.AddToken()` after creating a new token.

**Parameters**:
- `tokenType`: The token type as a string (e.g., "Identifier", "Plus", "Newline")
- `line`, `column`: Source location for the token
- `value`: The actual text value (e.g., "hello" for an identifier, "+" for Plus)

**Example usage** (from Lexer.cs):
```csharp
_logger.LogTokenRead(token.Type.ToString(), token.Line, token.Column, token.Value);
```

**Why it's important**: Trace-level token logging is critical for debugging lexer issues like incorrect tokenization or position tracking bugs.

---

#### `LogIndentChange(int oldLevel, int newLevel)`

**What it does**: Tracks indentation level changes (critical for Python-style significant whitespace).

**When to use**: Called when the lexer detects a change in indentation depth.

**Parameters**:
- `oldLevel`: Previous indentation level (in spaces)
- `newLevel`: New indentation level

**Why it's important**: Indentation bugs are common in Python-like languages. This helps debug issues like:
- Missing INDENT/DEDENT tokens
- Incorrect indentation counting
- Mixed tabs/spaces problems

**Example output** (ConsoleCompilerLogger):
```
[TRACE] [LEXER] Indent: 0 → 4
[TRACE] [LEXER] Indent: 4 → 0
```

---

### Parser-Specific Logging

#### `LogParseEnter(string rule, int tokenPosition)`

**What it does**: Marks entry into a parsing rule (for recursive descent parser tracing).

**When to use**: Called at the beginning of each parse method in the Parser.

**Parameters**:
- `rule`: Name of the parsing rule (e.g., "ParseExpression", "ParseIfStatement")
- `tokenPosition`: Current position in the token stream

**Why it's important**: 
- Visualizes the parser's recursive descent path
- Helps identify where parsing fails
- Useful for debugging ambiguous grammars or precedence issues

---

#### `LogParseExit(string rule, bool success)`

**What it does**: Marks exit from a parsing rule, indicating success or failure.

**Parameters**:
- `rule`: Name of the parsing rule (matches `LogParseEnter`)
- `success`: Whether the rule successfully parsed

**Typical usage pattern**:
```csharp
public Expr ParseExpression()
{
    _logger.LogParseEnter(nameof(ParseExpression), _currentTokenIndex);
    try 
    {
        var result = /* parsing logic */;
        _logger.LogParseExit(nameof(ParseExpression), true);
        return result;
    }
    catch
    {
        _logger.LogParseExit(nameof(ParseExpression), false);
        throw;
    }
}
```

**Example output**:
```
[DEBUG] [PARSER] Enter: ParseExpression @ token 5
[DEBUG] [PARSER] Exit:  ParseExpression ✓
```

---

### Standard Logging Methods

#### `LogError(string message, int line, int column)`

**What it does**: Logs compilation errors with source location.

**When to use**: For recoverable or reportable errors that should be shown to users.

**Important distinction**: 
- Use this for **diagnostic errors** (type mismatches, undefined variables)
- Don't use for **internal compiler bugs** (use exceptions for those)

---

#### `LogWarning(string message, int line, int column)`

**What it does**: Logs warnings (issues that don't prevent compilation but might indicate problems).

**Examples**: Unused variables, deprecated features, potential type issues.

---

#### `LogInfo(string message)`

**What it does**: Logs high-level compilation phase information.

**When to use**: At the start/end of major compilation phases.

**Example usage**:
```csharp
_logger.LogInfo("Starting semantic analysis...");
_logger.LogInfo($"Lexer initialized, source length: {source.Length}");
```

---

#### `LogDebug(string message)`

**What it does**: Logs detailed operational information for debugging.

**When to use**: For internal state changes, decision points, or important operations.

**Example**: "Resolving import: MyModule", "Type narrowing applied: x is now int"

---

#### `LogTrace(string message)`

**What it does**: Logs extremely verbose trace information.

**When to use**: For very fine-grained debugging (every operation).

**Warning**: Can generate massive amounts of output. Use sparingly.

---

### Metrics and Optimization

#### `LogMetrics(string metricsOutput)`

**What it does**: Logs performance metrics and statistics about compilation.

**Example metrics**:
- Tokens processed
- Parse tree node count
- Compilation time per phase
- Cache hit/miss ratios

---

#### `IsEnabled(CompilerLogLevel level)`

**What it does**: Checks if logging is enabled at a specific level.

**Why it exists**: **Performance optimization**. Allows you to skip expensive logging operations when they won't be recorded.

**Critical usage pattern**:
```csharp
// ❌ BAD: Always constructs the expensive string
_logger.LogDebug($"AST dump:\n{DumpEntireAst(module)}");

// ✅ GOOD: Only dumps AST if debug logging is enabled
if (_logger.IsEnabled(CompilerLogLevel.Debug))
{
    _logger.LogDebug($"AST dump:\n{DumpEntireAst(module)}");
}
```

**Return value**: `true` if the logger will record messages at that level, `false` otherwise.

---

## Dependencies

### Implementations

The codebase provides two implementations:

1. **`ConsoleCompilerLogger`** (`ConsoleCompilerLogger.cs`)
   - Writes to `Console.Out` and `Console.Error`
   - Formats messages with level tags and source locations
   - Used in production (CLI) and during development

2. **`NullLogger`** (`NullLogger.cs`)
   - Singleton instance: `NullLogger.Instance`
   - All methods are no-ops (do nothing)
   - Uses `[MethodImpl(MethodImplOptions.AggressiveInlining)]` for zero overhead
   - Used in tests and when logging is disabled

### Consumers

This interface is used throughout the compiler:

- **Lexer** (`Lexer.cs`): Token and indentation logging
- **Parser** (`Parser.cs`): Parse rule tracing
- **Semantic Analysis**: Type checker, name resolver, control flow validator
- **Code Generation**: (planned) emit C# code generation steps
- **Compiler** (`Compiler.cs`, `AssemblyCompiler.cs`): Top-level orchestration

**Injection pattern**: Loggers are injected via constructor:

```csharp
public class Lexer
{
    private readonly ICompilerLogger _logger;
    
    public Lexer(string source, ICompilerLogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
    }
}
```

---

## Patterns and Design Decisions

### 1. **Interface Segregation**

The interface is intentionally small and focused. It doesn't try to be a general-purpose logger—it's specifically designed for compiler operations.

**Alternative considered**: Using Microsoft.Extensions.Logging.ILogger
- **Rejected because**: Too generic, doesn't capture compiler-specific operations (token reads, parse rules)
- **Tradeoff**: Custom interface gives better semantics but requires custom implementations

---

### 2. **Null Object Pattern**

`NullLogger` implements the Null Object pattern:
- Eliminates null checks in compiler code
- Zero-overhead when logging is disabled (aggressive inlining)
- Default parameter: `ICompilerLogger? logger = null` → defaults to `NullLogger.Instance`

**Why this matters**: In hot paths (lexing millions of tokens), null checks add overhead. The Null Object pattern eliminates this.

---

### 3. **Hierarchical Log Levels**

The enum uses integers that form a natural hierarchy:
- Setting level to `Debug` (4) means `Error` (1), `Warning` (2), and `Info` (3) are also enabled
- Implementations check: `if (_minLevel >= CompilerLogLevel.Trace)`

**Typical configurations**:
- **Production CLI**: `Info` (show phases but not details)
- **Debugging**: `Debug` or `Trace` (show everything)
- **Tests**: `None` or `Error` (silence noise)

---

### 4. **Location-Aware Logging**

Errors and warnings include `line` and `column` parameters. This enables:
- IDE integration (jump to error location)
- Better error messages for users
- Correlation with source code

**Future possibility**: Could extend to include filename for multi-file projects.

---

### 5. **Performance-First Design**

The `IsEnabled()` method enables conditional logging:
- Skip expensive string formatting if logging is disabled
- Critical for trace-level logging in tight loops
- Follows best practices from high-performance logging frameworks

---

## Debugging Tips

### 1. **Trace Token Issues**

If you're debugging lexer problems:

```csharp
var logger = new ConsoleCompilerLogger(CompilerLogLevel.Trace);
var lexer = new Lexer(source, logger);
```

This will show every token read and indentation change. Look for:
- Unexpected token types
- Wrong positions (line/column)
- Missing tokens

---

### 2. **Trace Parse Failures**

For parser issues, use `Debug` level:

```csharp
var logger = new ConsoleCompilerLogger(CompilerLogLevel.Debug);
var parser = new Parser(tokens, logger);
```

Watch the Enter/Exit logs to see:
- Which rule fails (Exit with ✗)
- Where in the token stream it fails
- The call stack of parsing rules

---

### 3. **Compare Implementations**

When writing a new logger implementation:
1. Look at `ConsoleCompilerLogger` for formatting examples
2. Look at `NullLogger` for the minimal contract
3. Test with varying log levels to ensure hierarchical behavior

---

### 4. **Performance Profiling**

Use `LogMetrics()` to capture:
- Phase timings
- Memory allocations
- Cache performance

Combine with `IsEnabled()` to avoid overhead when not profiling:

```csharp
if (_logger.IsEnabled(CompilerLogLevel.Info))
{
    _logger.LogMetrics($"Lexing took {elapsed.TotalMilliseconds}ms, {tokenCount} tokens");
}
```

---

## Contribution Guidelines

### When to Modify This Interface

**✅ Good reasons to add methods**:
- New compiler phase with specific logging needs (e.g., `LogOptimizationApplied()`)
- Commonly needed structured data (e.g., `LogTypeInference()`)
- Performance-critical operations that need conditional logging

**❌ Bad reasons**:
- General-purpose logging (use standard levels instead)
- One-off debugging (use `LogDebug()` with descriptive messages)
- Implementation-specific needs (handle in implementation)

---

### How to Add a New Log Method

If you need to add a method:

1. **Add to interface** with XML documentation:
   ```csharp
   /// <summary>
   /// Log when an optimization is applied
   /// </summary>
   void LogOptimization(string optimizationName, string nodeType, int line);
   ```

2. **Implement in `ConsoleCompilerLogger`**:
   ```csharp
   public void LogOptimization(string optimizationName, string nodeType, int line)
   {
       if (_minLevel >= CompilerLogLevel.Debug)
       {
           _output.WriteLine($"[DEBUG] [OPTIMIZER] {optimizationName} applied to {nodeType} @ L{line}");
       }
   }
   ```

3. **Implement in `NullLogger`** (no-op with aggressive inlining):
   ```csharp
   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public void LogOptimization(string optimizationName, string nodeType, int line) { }
   ```

4. **Update tests** that use loggers
5. **Document** when and why to use the new method

---

### How to Create a New Logger Implementation

Example: File-based logger for CI/CD:

1. **Implement the interface**:
   ```csharp
   public class FileCompilerLogger : ICompilerLogger
   {
       private readonly StreamWriter _writer;
       private readonly CompilerLogLevel _minLevel;
       
       public FileCompilerLogger(string path, CompilerLogLevel minLevel)
       {
           _writer = new StreamWriter(path, append: true);
           _minLevel = minLevel;
       }
       
       // Implement all 12 methods...
   }
   ```

2. **Handle resource cleanup** (implement `IDisposable` if needed)

3. **Test all log levels** to ensure hierarchical behavior

4. **Consider thread safety** if used in parallel compilation

---

### Testing Guidelines

When writing tests:

**Option 1: Use NullLogger** (when you don't care about logs):
```csharp
var lexer = new Lexer(source, NullLogger.Instance);
```

**Option 2: Capture logs** (when you need to verify logging):
```csharp
var output = new StringWriter();
var logger = new ConsoleCompilerLogger(CompilerLogLevel.Debug, output);
var lexer = new Lexer(source, logger);
// Assert on output.ToString()
```

**Option 3: Mock logger** (for testing logging calls):
```csharp
var mockLogger = new Mock<ICompilerLogger>();
var lexer = new Lexer(source, mockLogger.Object);
// Verify specific methods were called
```

---

### Common Mistakes to Avoid

1. **Don't log in hot loops without `IsEnabled()` checks**:
   ```csharp
   // ❌ BAD: Formats string even when logging is disabled
   for (int i = 0; i < 1000000; i++)
   {
       _logger.LogTrace($"Processing item {i}: {items[i]}");
   }
   
   // ✅ GOOD: Only formats when needed
   if (_logger.IsEnabled(CompilerLogLevel.Trace))
   {
       for (int i = 0; i < 1000000; i++)
       {
           _logger.LogTrace($"Processing item {i}: {items[i]}");
       }
   }
   ```

2. **Don't use logging for control flow**:
   ```csharp
   // ❌ BAD: Side effects in logging
   _logger.LogDebug(PerformCriticalOperation());
   
   // ✅ GOOD: Log result, not operation
   var result = PerformCriticalOperation();
   _logger.LogDebug($"Operation result: {result}");
   ```

3. **Don't log sensitive information**:
   - User source code might contain secrets
   - Be careful with full AST dumps
   - Consider redaction for production loggers

---

## Related Files

- **`ConsoleCompilerLogger.cs`**: Primary implementation for CLI
- **`NullLogger.cs`**: No-op implementation for performance
- **`Lexer.cs`**: Major consumer of token/indent logging
- **`Parser.cs`**: Major consumer of parse rule logging
- **`Compiler.cs`**: Creates and configures loggers

---

## Summary

`ICompilerLogger` is a focused, performance-conscious abstraction that:
- Provides visibility into compiler internals
- Supports debugging across all compilation phases
- Uses hierarchical log levels for flexible verbosity
- Enables zero-overhead logging when disabled
- Serves as a contract between compiler components and logging implementations

When in doubt: use standard log levels (`LogInfo`, `LogDebug`, `LogTrace`) for most needs. Only add new methods if you're capturing something structurally different from general logging.
