# Walkthrough: NullLogger.cs

**Source File**: `src/Sharpy.Compiler/Logging/NullLogger.cs`

---

## Overview

`NullLogger.cs` implements the **Null Object pattern** for compiler logging. It provides a no-op (no operation) implementation of the `ICompilerLogger` interface that can be used when logging is disabled or not needed.

**Key Purpose**: This class allows the compiler to avoid checking for null loggers throughout the codebase. Instead of writing:

```csharp
if (logger != null)
    logger.LogInfo("Compiling...");
```

The code can simply write:

```csharp
logger.LogInfo("Compiling...");  // Safe even if logger is NullLogger
```

The NullLogger ensures zero runtime overhead through aggressive inlining, making it essentially "free" when logging is disabled.

---

## Class/Type Structure

### `NullLogger` Class

```csharp
public sealed class NullLogger : ICompilerLogger
```

**Key Characteristics**:
- **`sealed`**: Cannot be inherited (optimization + design enforcement)
- **Implements**: `ICompilerLogger` interface with 11 methods
- **Pattern**: Singleton via `Instance` property
- **Performance**: All methods use `[MethodImpl(MethodImplOptions.AggressiveInlining)]`

**Singleton Pattern**:
```csharp
public static readonly NullLogger Instance = new();
private NullLogger() { }
```

- **`Instance`**: Public static readonly field - the one and only instance
- **Private constructor**: Prevents external instantiation
- **Thread-safe**: C# guarantees static field initialization is thread-safe

---

## Key Methods/Functions

All methods in `NullLogger` are intentionally empty no-ops. Let's understand each category:

### 1. Lexer Logging Methods

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public void LogTokenRead(string tokenType, int line, int column, string value) { }
```

**Purpose**: Would normally log each token the lexer reads  
**Parameters**:
- `tokenType`: Type of token (e.g., "Identifier", "Keyword", "Operator")
- `line`, `column`: Location in source code
- `value`: Actual token text

**Usage in Lexer**: Called when tokenizing source code to track progress

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public void LogIndentChange(int oldLevel, int newLevel) { }
```

**Purpose**: Would log Python-style indentation changes  
**Why important**: Sharpy uses significant whitespace like Python - this helps debug indentation issues

### 2. Parser Logging Methods

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public void LogParseEnter(string rule, int tokenPosition) { }

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public void LogParseExit(string rule, bool success) { }
```

**Purpose**: Track parser rule execution (entry/exit)  
**Use case**: Debugging parser issues - see which rules succeed/fail  
**Parameters**:
- `rule`: Parser rule name (e.g., "FunctionDef", "IfStatement")
- `tokenPosition`: Current position in token stream
- `success`: Whether the rule matched successfully

### 3. Standard Logging Methods

```csharp
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
```

**Purpose**: Standard logging levels from Error (most severe) to Trace (most verbose)

**Hierarchy** (defined in `CompilerLogLevel` enum):
- `Error (1)`: Critical issues
- `Warning (2)`: Potential problems
- `Info (3)`: High-level phase information
- `Debug (4)`: Detailed operational info
- `Trace (5)`: Every token, every parse rule

### 4. Metrics Logging

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public void LogMetrics(string metricsOutput) { }
```

**Purpose**: Would log compilation performance metrics  
**Use case**: Profiling compilation time, memory usage, etc.

### 5. Level Check Method

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public bool IsEnabled(CompilerLogLevel level) => false;
```

**Purpose**: Check if a logging level is enabled  
**Return**: Always `false` for NullLogger (no logging enabled)

**Why needed**: Allows calling code to skip expensive string formatting:

```csharp
// Efficient - avoids string construction when logging disabled
if (logger.IsEnabled(CompilerLogLevel.Debug))
    logger.LogDebug($"Complex operation result: {ExpensiveToString()}");
```

---

## Dependencies

### Internal Dependencies

1. **`ICompilerLogger` interface** (`ICompilerLogger.cs` in same directory)
   - Defines the contract NullLogger implements
   - Contains 11 method signatures + `CompilerLogLevel` enum

2. **`System.Runtime.CompilerServices` namespace**
   - Provides `[MethodImpl]` attribute for aggressive inlining

### Used By (Consumers)

NullLogger is used as the default logger throughout the compiler:

**Compiler Core**:
- `Compiler.cs`: `_logger = logger ?? NullLogger.Instance;`
- `AssemblyCompiler.cs`: `_logger = logger ?? NullLogger.Instance;`

**Lexer/Parser**:
- `Lexer.cs`: Default when no logger provided
- `Parser.cs`: Default when no logger provided

**Semantic Analysis**:
- `TypeChecker.cs`
- `NameResolver.cs`
- `TypeResolver.cs`
- `ImportResolver.cs`
- `AccessValidator.cs`
- `ControlFlowValidator.cs`
- `ModuleRegistry.cs`

**Tests**:
- Most test files use `NullLogger.Instance` to suppress logging during test execution
- Enables fast test runs without console spam

**CLI**:
- `Program.cs`: Returns `NullLogger.Instance` when verbose mode is off

---

## Patterns and Design Decisions

### 1. Null Object Pattern

**What it is**: Provide a default object that does nothing instead of using `null`

**Benefits**:
- ✅ Eliminates null checks throughout codebase
- ✅ Reduces cognitive load (one less thing to worry about)
- ✅ Prevents `NullReferenceException`
- ✅ Makes API cleaner (`ICompilerLogger` instead of `ICompilerLogger?`)

**Alternative approach** (without Null Object):
```csharp
// Every call site needs null check - error-prone!
_logger?.LogInfo("Starting compilation");
_logger?.LogDebug("Parsed function");
// Repeat hundreds of times...
```

**With Null Object**:
```csharp
// Just use it - guaranteed safe
_logger.LogInfo("Starting compilation");
_logger.LogDebug("Parsed function");
```

### 2. Singleton Pattern

**Why singleton**: There's no reason to have multiple instances of a do-nothing logger

**Implementation choice**: `static readonly` field instead of lazy initialization
- Simpler than `Lazy<T>`
- Thread-safe by C# spec
- No performance overhead

### 3. Aggressive Inlining

**What it does**: `[MethodImpl(MethodImplOptions.AggressiveInlining)]` tells the JIT compiler to inline the method body at the call site

**For NullLogger**: Methods are empty, so inlining means:
```csharp
// Before inlining
logger.LogInfo("message");

// After inlining
{ } // Empty method body - completely optimized away!
```

**Result**: **Zero runtime overhead** - as if the logging calls don't exist at all

**Why on every method**: Ensure the JIT doesn't decide some methods are "too complex" to inline (though empty methods always inline anyway)

### 4. Sealed Class

**Why sealed**:
- **Performance**: Virtual method dispatch can be optimized away
- **Design**: There's no meaningful way to inherit from NullLogger
- **Intent**: Makes the design explicit - this is a leaf class

---

## Debugging Tips

### When NullLogger is Active

**Symptom**: "I'm not seeing any log output!"

**Check**:
1. Verify which logger is being used:
   ```csharp
   Console.WriteLine($"Logger type: {logger.GetType().Name}");
   // If "NullLogger", logging is disabled
   ```

2. In CLI, check if verbose mode is enabled:
   ```bash
   # Won't log anything
   sharpyc build myfile.spy
   
   # Will use ConsoleCompilerLogger instead
   sharpyc build myfile.spy --verbose
   ```

3. In tests, you might want actual logging:
   ```csharp
   // Don't use NullLogger for debugging tests
   var logger = new ConsoleCompilerLogger(CompilerLogLevel.Debug);
   var lexer = new Lexer(source, logger);
   ```

### Performance Investigation

**NullLogger should be free**. If profiling shows time in NullLogger:
- Check that `[MethodImpl]` attributes are present
- Verify in Release build (Debug mode might not inline)
- Use a profiler to confirm inlining happened

### Testing Logging Behavior

**To test that logging calls are made correctly**:
- Create a test logger implementation:
  ```csharp
  public class TestLogger : ICompilerLogger
  {
      public List<string> Messages = new();
      public void LogInfo(string message) => Messages.Add(message);
      // ... etc
  }
  ```
- Use it instead of NullLogger in tests that verify logging

---

## Contribution Guidelines

### When to Modify NullLogger

**Rarely!** This class should change only when `ICompilerLogger` changes.

**If adding a new method to `ICompilerLogger`**:
1. Add an empty implementation to NullLogger
2. Add `[MethodImpl(MethodImplOptions.AggressiveInlining)]`
3. Make it a no-op (empty body or `=> default` for value types)

**Example**: Adding a new method:
```csharp
// In ICompilerLogger.cs
void LogCodeGeneration(string nodeType, string generatedCode);

// Add to NullLogger.cs
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public void LogCodeGeneration(string nodeType, string generatedCode) { }
```

### What NOT to Do

❌ **Don't add logic to NullLogger**
```csharp
// WRONG - defeats the purpose!
public void LogInfo(string message) 
{ 
    if (message.Contains("error"))
        Console.WriteLine(message); 
}
```

❌ **Don't remove the `sealed` keyword**
```csharp
// WRONG - no reason to inherit
public class NullLogger : ICompilerLogger { }
```

❌ **Don't remove inlining attributes**
```csharp
// WRONG - loses zero-overhead guarantee
public void LogInfo(string message) { }
```

❌ **Don't add state or fields**
```csharp
// WRONG - violates Null Object pattern
private int callCount;
public void LogInfo(string message) { callCount++; }
```

### Testing Your Changes

If you modify NullLogger:

1. **Run all tests**: `dotnet test`
2. **Verify no behavior change**: Tests should still pass
3. **Check performance**: Compare Release build performance before/after
4. **Verify inlining**: Use a decompiler (ILSpy, dnSpy) to confirm methods inline

### Related Files to Update

If `ICompilerLogger` interface changes:
- ✅ **NullLogger.cs** - Add no-op implementations
- ✅ **ConsoleCompilerLogger.cs** - Add real implementations
- ⚠️ Update any tests that mock `ICompilerLogger`

---

## Summary

**NullLogger is a textbook example of the Null Object pattern**. It exists solely to eliminate null checks and provide zero-overhead "logging" when logging is disabled.

**Key Takeaways**:
1. **Thread-safe singleton** via `static readonly Instance`
2. **Zero runtime overhead** via aggressive inlining
3. **Prevents NullReferenceException** - always safe to call
4. **Used by default** throughout compiler when logging not enabled
5. **Rarely modified** - only when `ICompilerLogger` interface changes

**When reading compiler code**, if you see `NullLogger.Instance`, know that:
- Logging is disabled (typical for tests and non-verbose CLI usage)
- All log calls will be optimized away at runtime
- You can switch to `ConsoleCompilerLogger` for debugging

This simple 47-line class eliminates hundreds of null checks and makes the compiler codebase cleaner and safer! 🎯
