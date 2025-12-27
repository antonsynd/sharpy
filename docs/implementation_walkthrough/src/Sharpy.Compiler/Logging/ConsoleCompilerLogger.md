# Walkthrough: ConsoleCompilerLogger.cs

**Source File**: `src/Sharpy.Compiler/Logging/ConsoleCompilerLogger.cs`

---

## Overview

`ConsoleCompilerLogger` is the production logging implementation for the Sharpy compiler. It provides human-readable console output for all compiler operations, from low-level token reading to high-level compilation phases. This logger is what you see when you run `sharpyc` with verbose flags enabled.

**Key Role**: Acts as the debugging and observability layer for the entire compilation pipeline. It bridges the abstract compiler operations with concrete, timestamped console output that developers can use to understand what's happening during compilation.

**Design Philosophy**: 
- Simple, synchronous logging (no async complexity)
- Level-based filtering to control verbosity
- Separate streams for normal output vs. errors/warnings
- Structured output format for easy parsing and readability

---

## Class Structure

### ConsoleCompilerLogger

```csharp
public sealed class ConsoleCompilerLogger : ICompilerLogger
```

**Inheritance**: Implements `ICompilerLogger` interface (defined in same namespace)

**Sealed**: Cannot be inherited - this is a concrete, final implementation. The Sharpy team expects you to use this for console logging or `NullLogger` for no logging.

### Private Fields

```csharp
private readonly CompilerLogLevel _minLevel;
private readonly TextWriter _output;
private readonly TextWriter _errorOutput;
```

**`_minLevel`**: Threshold for filtering log messages. Only messages at or below this level are output.

**`_output`**: Where normal logs go (INFO, DEBUG, TRACE). Defaults to `Console.Out`.

**`_errorOutput`**: Where errors and warnings go. Defaults to `Console.Error`. This separation allows shell redirection like `sharpyc file.spy 2> errors.txt`.

---

## Constructor

```csharp
public ConsoleCompilerLogger(
    CompilerLogLevel minLevel, 
    TextWriter? output = null, 
    TextWriter? errorOutput = null)
{
    _minLevel = minLevel;
    _output = output ?? Console.Out;
    _errorOutput = errorOutput ?? Console.Error;
}
```

**Parameters**:
- `minLevel`: Required - sets the verbosity level
- `output`: Optional - for testing or custom output destinations
- `errorOutput`: Optional - separate error stream

**Design Decision**: The nullable `TextWriter` parameters with null-coalescing (`??`) allow dependency injection for testing while defaulting to standard console streams for production.

**Usage Example**:
```csharp
// Production: log only errors and warnings
var logger = new ConsoleCompilerLogger(CompilerLogLevel.Warning);

// Debugging: see everything
var logger = new ConsoleCompilerLogger(CompilerLogLevel.Trace);

// Testing: capture output
var output = new StringWriter();
var logger = new ConsoleCompilerLogger(CompilerLogLevel.Debug, output);
```

---

## Key Methods

### Lexer Logging

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

**Purpose**: Log every token the lexer produces.

**When It Runs**: Only at `Trace` level - this is extremely verbose.

**Output Format**:
```
[TRACE] [LEXER] Token: Identifier          @ L5:C10 = 'my_var'
[TRACE] [LEXER] Token: Colon               @ L5:C16
```

**Key Details**:
- `{tokenType,-20}`: Left-aligned with 20-character padding for visual column alignment
- Empty values (like punctuation) don't print `= ''`
- Location tracking (`L{line}:C{column}`) helps correlate tokens with source

**When to Use**: Debugging lexer issues, understanding tokenization, or investigating "unexpected token" errors.

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

**Purpose**: Track Python-style indentation changes (INDENT/DEDENT tokens).

**Why It Matters**: Sharpy uses significant whitespace like Python. Indentation bugs are common, and this log shows exactly when indent levels change.

**Output Example**:
```
[TRACE] [LEXER] Indent: 0 → 4
[TRACE] [LEXER] Indent: 4 → 0
```

---

### Parser Logging

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

**Purpose**: Record when the parser enters a grammar rule (e.g., "FunctionDef", "IfStatement").

**Level**: `Debug` (less noisy than `Trace`).

**Why Token Position**: Helps you correlate the parse tree with the token stream.

**Usage**: Understanding parse failures - you can see which rule the parser was trying when it failed.

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

**Purpose**: Record when parser exits a rule, with success/failure indicator.

**Output Example**:
```
[DEBUG] [PARSER] Enter: FunctionDef @ token 5
[DEBUG] [PARSER] Exit:  FunctionDef ✓
```

**Key Insight**: The `success` parameter tells you if the rule matched. A `✗` followed by trying another rule is normal (backtracking). But if all alternatives fail, you get a parse error.

---

### Error and Warning Logging

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

**Critical Details**:
- Goes to `_errorOutput` (stderr)
- Always includes source location
- Checked even at `Error` level (most restrictive non-None level)

**Output Example**:
```
[ERROR] @ L12:C5: Unexpected token 'else' - expected expression
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

**Similar to LogError** but for non-fatal issues (unused variables, deprecated syntax, etc.).

---

### General Logging

#### LogInfo / LogDebug / LogTrace

```csharp
public void LogInfo(string message)
{
    if (_minLevel >= CompilerLogLevel.Info)
    {
        _output.WriteLine($"[INFO]  {message}");
    }
}
```

**Purpose**: Generic logging at different verbosity levels.

**Typical Uses**:
- **Info**: "Compiling module 'math.spy'", "Code generation complete"
- **Debug**: "Resolved type 'List[int]'", "Imported 10 symbols from module"
- **Trace**: Implementation details, internal state dumps

**No Source Location**: These are free-form messages, not tied to specific source positions.

---

### Metrics Logging

#### LogMetrics

```csharp
public void LogMetrics(string metricsOutput)
{
    _output.WriteLine(metricsOutput);
}
```

**Special Case**: No level check! Metrics always print if you call this method.

**Why**: Compilation statistics (time, memory, lines processed) are valuable even in quiet mode.

**Expected Format**: Pre-formatted string, likely multi-line:
```
Compilation Metrics:
  Tokens: 1,523
  AST Nodes: 342
  Time: 45ms
```

---

### IsEnabled

```csharp
public bool IsEnabled(CompilerLogLevel level) => _minLevel >= level;
```

**Purpose**: Lets caller check if a log level is active before doing expensive work.

**Performance Optimization Pattern**:
```csharp
// Avoid expensive string formatting if not needed
if (_logger.IsEnabled(CompilerLogLevel.Trace))
{
    var details = BuildExpensiveDebugString();
    _logger.LogTrace(details);
}
```

**Note**: All the other log methods do this check internally, so this is mainly for avoiding expensive pre-processing.

---

## Dependencies

### Direct Dependencies

1. **`ICompilerLogger` interface** - Defines the logging contract
2. **`CompilerLogLevel` enum** - Defines verbosity levels (None, Error, Warning, Info, Debug, Trace)
3. **`System.IO.TextWriter`** - For flexible output redirection

### Usage in Compiler Pipeline

The logger is injected throughout the compiler:

```csharp
// From Compiler.cs
private readonly ICompilerLogger _logger;

public Compiler(ICompilerLogger? logger = null)
{
    _logger = logger ?? NullLogger.Instance;
}
```

**Key Pattern**: The compiler always has a logger (never null). If none provided, uses `NullLogger.Instance` (zero-overhead no-op implementation).

### Related Classes

- **`NullLogger`**: Null Object pattern - all methods are aggressively inlined no-ops. Used in production when logging disabled.
- **`Lexer`, `Parser`, `TypeChecker`, etc.**: All accept `ICompilerLogger` and call logging methods.

---

## Patterns and Design Decisions

### 1. **Strategy Pattern**

`ICompilerLogger` is the strategy interface. You can swap implementations:
- `ConsoleCompilerLogger` for human-readable output
- `NullLogger` for no logging
- `FileLogger` (hypothetical) for writing to files
- `StructuredLogger` (hypothetical) for JSON/XML output

### 2. **Dependency Injection**

Logger is always passed via constructor:
```csharp
var lexer = new Lexer(source, _logger);
var parser = new Parser(tokens, _logger);
```

**Why**: Testability, configurability, and following SOLID principles (Dependency Inversion).

### 3. **Null Object Pattern**

`NullLogger.Instance` eliminates null checks everywhere:
```csharp
_logger = logger ?? NullLogger.Instance;  // Never null after this
```

No need for `_logger?.LogError(...)` throughout the codebase.

### 4. **Level-Based Filtering**

Uses numeric comparison (`_minLevel >= CompilerLogLevel.Trace`) instead of flags/bitmasks:
```csharp
public enum CompilerLogLevel
{
    None = 0,
    Error = 1,
    Warning = 2,
    Info = 3,
    Debug = 4,
    Trace = 5
}
```

**Implication**: Setting level to `Info` includes all higher levels (Error, Warning) automatically. Simpler than managing flag combinations.

### 5. **Separate Output Streams**

Errors go to stderr, normal logs to stdout:
```bash
# User can redirect separately
sharpyc file.spy > output.txt 2> errors.txt

# Or suppress errors
sharpyc file.spy 2> /dev/null
```

### 6. **Structured Output Format**

Every log follows consistent pattern:
```
[LEVEL] [COMPONENT] Message
[ERROR] @ L5:C10: message
```

**Benefits**:
- Easy to parse with regex
- Visual scanning is quick
- Tools can filter by level or component

---

## Debugging Tips

### 1. **Tracking Down Parse Errors**

Set `CompilerLogLevel.Debug` and look for the last successful parse before failure:

```
[DEBUG] [PARSER] Enter: Statement @ token 42
[DEBUG] [PARSER] Enter: ExpressionStatement @ token 42
[DEBUG] [PARSER] Exit:  ExpressionStatement ✗
[DEBUG] [PARSER] Enter: AssignmentStatement @ token 42
[ERROR] @ L10:C5: Expected identifier after 'def'
```

The parser tried `ExpressionStatement` (failed), then `AssignmentStatement` (failed), then gave up.

### 2. **Understanding Indentation Issues**

Enable `Trace` level and watch indent changes:

```
[TRACE] [LEXER] Token: Def                 @ L5:C0
[TRACE] [LEXER] Indent: 0 → 4
[TRACE] [LEXER] Token: Return             @ L6:C4
[TRACE] [LEXER] Indent: 4 → 0
```

If you see unexpected indent changes, check for mixed tabs/spaces.

### 3. **Performance Profiling**

Use `LogMetrics` to track compilation time by phase:

```csharp
var sw = Stopwatch.StartNew();
// ... lexing ...
sw.Stop();
_logger.LogInfo($"Lexing completed in {sw.ElapsedMilliseconds}ms");
```

### 4. **Testing Logger Output**

Inject a `StringWriter` to capture and assert on output:

```csharp
var output = new StringWriter();
var logger = new ConsoleCompilerLogger(CompilerLogLevel.Debug, output);

// ... use logger ...

var result = output.ToString();
Assert.Contains("[DEBUG] [PARSER] Enter: FunctionDef", result);
```

### 5. **Filtering Logs in Real-Time**

Since output is structured, pipe through grep:

```bash
sharpyc file.spy --trace | grep LEXER     # Only lexer logs
sharpyc file.spy --debug | grep "✗"       # Only failed parses
```

---

## Contribution Guidelines

### What Changes Are Appropriate for This File?

#### ✅ **Good Changes**

1. **Adding new log points**:
   ```csharp
   public void LogSemanticPass(string passName, int symbolsResolved)
   {
       if (_minLevel >= CompilerLogLevel.Debug)
       {
           _output.WriteLine($"[DEBUG] [SEMANTIC] {passName}: {symbolsResolved} symbols");
       }
   }
   ```

2. **Improving output formatting**:
   - Better alignment
   - Color coding (using ANSI escape codes)
   - More compact formats for verbose logs

3. **Performance optimizations**:
   - Caching formatted strings
   - Avoiding allocations in hot paths
   - Using `StringBuilder` for complex formatting

4. **Adding structured logging support**:
   ```csharp
   public void LogStructured(string level, string component, object data)
   {
       var json = JsonSerializer.Serialize(data);
       _output.WriteLine($"[{level}] [{component}] {json}");
   }
   ```

#### ❌ **Changes to Avoid**

1. **Adding complex logic** - Keep this class simple. Complex formatting or filtering belongs elsewhere.

2. **Breaking the interface contract** - `ICompilerLogger` is used everywhere. Breaking changes require updating the entire codebase.

3. **Adding dependencies** - This is a low-level infrastructure class. Avoid dependencies on higher-level compiler components.

4. **Making it async** - The compiler is synchronous. Async logging adds complexity without clear benefit.

### Testing New Log Methods

When adding a new log method:

1. **Add to interface first**:
   ```csharp
   // In ICompilerLogger.cs
   void LogNewThing(string details);
   ```

2. **Implement in both loggers**:
   - `ConsoleCompilerLogger` - actual implementation
   - `NullLogger` - empty method with `[MethodImpl(MethodImplOptions.AggressiveInlining)]`

3. **Test with StringWriter**:
   ```csharp
   [Fact]
   public void LogNewThing_OutputsExpectedFormat()
   {
       var output = new StringWriter();
       var logger = new ConsoleCompilerLogger(CompilerLogLevel.Info, output);
       
       logger.LogNewThing("test data");
       
       var result = output.ToString();
       Assert.Contains("[INFO] test data", result);
   }
   ```

4. **Update documentation** - Add examples to this walkthrough.

### When to Add a New Log Level

Currently we have: None, Error, Warning, Info, Debug, Trace.

Consider a new level if:
- Existing levels don't capture a distinct verbosity tier
- There's a clear use case (e.g., "Performance" level for timing-only logs)
- You're willing to update all existing checks

**Caution**: More levels = more complexity. The current 6 levels cover most needs.

### Integration with Other Components

When adding logging to other compiler components:

```csharp
// In Semantic/TypeChecker.cs (example)
public class TypeChecker
{
    private readonly ICompilerLogger _logger;
    
    public TypeChecker(ICompilerLogger logger)
    {
        _logger = logger;
    }
    
    public void CheckTypes(AstNode node)
    {
        _logger.LogDebug($"Type checking node: {node.GetType().Name}");
        // ...
    }
}
```

**Key Points**:
- Always accept `ICompilerLogger`, not `ConsoleCompilerLogger`
- Never instantiate loggers inside components (inject via constructor)
- Choose appropriate log level for each message
- Include context (line, column) for errors/warnings

---

## Common Pitfalls

### 1. **Forgetting Level Checks**

```csharp
// ❌ Bad - always formats string even if not logged
_logger.LogDebug($"Resolved {complexObject.ExpensiveToString()}");

// ✅ Good - skip expensive work if debug disabled
if (_logger.IsEnabled(CompilerLogLevel.Debug))
{
    _logger.LogDebug($"Resolved {complexObject.ExpensiveToString()}");
}
```

### 2. **Using Wrong Output Stream**

```csharp
// ❌ Bad - errors should go to errorOutput
_output.WriteLine($"[ERROR] {message}");

// ✅ Good
_errorOutput.WriteLine($"[ERROR] {message}");
```

### 3. **Inconsistent Formatting**

Follow the established patterns:
- `[LEVEL]` always in brackets
- Component tags: `[LEXER]`, `[PARSER]`, `[SEMANTIC]`, etc.
- Location format: `@ L{line}:C{column}`

### 4. **Not Testing with NullLogger**

Always test that your code works with `NullLogger`:
```csharp
var compiler = new Compiler(NullLogger.Instance);
compiler.Compile(source); // Should work without issues
```

---

## Future Enhancements

Ideas for improving this logger (could be good starter tasks):

1. **Color-coded output** using ANSI escape codes
2. **Log file rotation** - write to files with automatic rotation
3. **Filtering by component** - `--log-only=lexer,parser`
4. **Structured JSON output** for machine parsing
5. **Performance metrics** - automatic timing of compilation phases
6. **Log level per component** - trace for lexer, info for everything else
7. **Interactive mode** - pause on errors, show context

---

## Summary

`ConsoleCompilerLogger` is a straightforward but essential piece of infrastructure. It provides observability into the entire compilation pipeline through simple, level-filtered console output. 

**Key Takeaways**:
- Uses Strategy pattern via `ICompilerLogger` interface
- Level-based filtering (None → Error → Warning → Info → Debug → Trace)
- Separate stdout/stderr for errors vs. normal logs
- Injected throughout compiler via dependency injection
- Paired with `NullLogger` for zero-overhead disabled logging

**When You'll Interact With It**:
- Adding new compiler features that need logging
- Debugging compilation issues (set to Debug/Trace)
- Writing tests that verify log output
- Extending the logging system with new capabilities

This class is stable and well-designed. Changes are typically additive (new log methods) rather than modifying existing behavior. If you're adding a new compiler phase or feature, you'll likely add new log methods here to make it observable.
