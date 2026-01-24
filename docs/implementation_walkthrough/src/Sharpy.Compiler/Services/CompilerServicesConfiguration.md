# Walkthrough: CompilerServicesConfiguration.cs

**Source File**: `src/Sharpy.Compiler/Services/CompilerServicesConfiguration.cs`

---

## Overview

`CompilerServicesConfiguration` is an immutable configuration object that controls how the Sharpy compiler behaves during compilation. It serves as the "settings panel" for the compiler services layer, defining error thresholds, logging preferences, and compilation continuation behavior.

**Role in Pipeline**: This configuration sits at the initialization stage of compilation, controlling the behavior of `CompilerServices` throughout the Lexer → Parser → Semantic Analysis → CodeGen pipeline.

**Key Insight**: This is a simple data class with no business logic—it's a pure configuration container that gets set once at construction time and never changes. Think of it as the "constructor arguments" for the entire compiler services layer.

---

## Class Structure

### Main Class: `CompilerServicesConfiguration`

```csharp
public sealed class CompilerServicesConfiguration
{
    public int MaxErrors { get; init; } = 100;
    public bool ContinueAfterErrors { get; init; } = true;
    public bool VerboseLogging { get; init; } = false;
    public string? InitialFilePath { get; init; }
    public static CompilerServicesConfiguration Default { get; } = new();
}
```

**Design Pattern**: This is a classic **Configuration Object** pattern using C# 9.0's `init`-only properties to enforce immutability.

- **`sealed`**: Cannot be inherited—this is a final, concrete configuration class
- **`init` properties**: Can only be set during object initialization (object initializer or constructor)
- **Static `Default`**: Provides a sensible default configuration without requiring users to specify every setting

---

## Configuration Properties

### 1. `MaxErrors` (Default: 100)

```csharp
public int MaxErrors { get; init; } = 100;
```

**Purpose**: Sets the maximum number of errors the compiler will collect before aborting compilation.

**Why it matters**:
- Prevents runaway error cascades (one syntax error can trigger dozens of downstream errors)
- Keeps compiler performance reasonable even on severely broken code
- Default of 100 is generous enough to see all issues in most files

**Usage in practice**: Checked by `CompilerServices.ShouldContinue()` (see `CompilerServices.cs:159-166`):

```csharp
public bool ShouldContinue()
{
    if (DiagnosticReporter.Diagnostics.ErrorCount >= _config.MaxErrors)
        return false;
    return true;
}
```

### 2. `ContinueAfterErrors` (Default: `true`)

```csharp
public bool ContinueAfterErrors { get; init; } = true;
```

**Purpose**: Determines whether compilation should continue after encountering any error.

**Two modes**:
- **`true` (default)**: "Collect all errors" mode—keep compiling to show the user all issues at once
- **`false`**: "Fail fast" mode—stop immediately on first error (useful for CI/CD where you just need pass/fail)

**Usage**: Also checked in `CompilerServices.ShouldContinue()`:

```csharp
if (!_config.ContinueAfterErrors && DiagnosticReporter.HasErrors)
    return false;
```

### 3. `VerboseLogging` (Default: `false`)

```csharp
public bool VerboseLogging { get; init; } = false;
```

**Purpose**: Enables detailed diagnostic output for debugging compiler internals.

**When to use**:
- Debugging compiler bugs
- Understanding why compilation is slow
- Tracing symbol resolution issues

**Note**: This property is currently defined but not heavily used in the codebase. It's infrastructure for future detailed logging.

### 4. `InitialFilePath` (Default: `null`)

```csharp
public string? InitialFilePath { get; init; }
```

**Purpose**: Sets the starting file path for error reporting context.

**Lifecycle**:
1. Set during configuration construction if you know the initial file
2. Applied to `DiagnosticReporter.CurrentFilePath` in `CompilerServices` constructor (see `CompilerServices.cs:72-75`)
3. Updated dynamically as compilation moves between files via `CompilerServices.CurrentFilePath` property

**Example**: When compiling `main.spy`, you'd set `InitialFilePath = "main.spy"` so error messages show the correct file.

### 5. `Default` (Static Property)

```csharp
public static CompilerServicesConfiguration Default { get; } = new();
```

**Purpose**: Provides a pre-configured instance with sensible defaults.

**Usage pattern**:

```csharp
// Option 1: Use defaults
var config = CompilerServicesConfiguration.Default;

// Option 2: Customize from defaults
var config = CompilerServicesConfiguration.Default with
{
    MaxErrors = 50,
    VerboseLogging = true
};

// Option 3: Create new instance
var config = new CompilerServicesConfiguration
{
    MaxErrors = 10,
    ContinueAfterErrors = false
};
```

---

## Dependencies

### Upstream Dependencies (What uses this)

1. **`CompilerServicesBuilder`** (`CompilerServicesBuilder.cs:12`)
   - Stores configuration during builder construction
   - Default: `CompilerServicesConfiguration.Default`
   - Can be customized via `.WithConfiguration(config)`

2. **`CompilerServices`** (`CompilerServices.cs:19, 41`)
   - Receives configuration in constructor
   - Stores immutable reference in `_config` field
   - Exposes via `Configuration` property for read-only access

### Downstream Dependencies (What this affects)

This configuration controls behavior in:
- **DiagnosticReporter**: Error count thresholds
- **CompilerServices.ShouldContinue()**: Compilation continuation logic
- **Future logging infrastructure**: VerboseLogging flag

### No Direct Dependencies

This file has **zero external dependencies**—it's pure data. No imports beyond the `namespace` declaration.

---

## Patterns and Design Decisions

### 1. Immutability Pattern

**Why immutable?**
- Thread safety: Multiple compilation threads can share the same configuration
- Predictability: Configuration can't change mid-compilation
- Cacheable: Compiler can make optimization decisions based on fixed settings

**How enforced?**
- `init`-only properties (C# 9.0 feature)
- `sealed` class prevents inheritance-based mutation

### 2. Object Initializer Syntax

```csharp
var config = new CompilerServicesConfiguration
{
    MaxErrors = 50,        // Object initializer syntax
    VerboseLogging = true  // Clean, declarative configuration
};
```

This is more readable than a constructor with 4+ parameters.

### 3. Sensible Defaults

Every property has a default that makes sense for typical usage:
- `MaxErrors = 100`: Generous for development, not infinite
- `ContinueAfterErrors = true`: Show all errors (better UX)
- `VerboseLogging = false`: Quiet by default (cleaner output)
- `InitialFilePath = null`: Nullable (not always compiling from a file)

### 4. Static Default Instance

Avoids repeated allocation for the common case:

```csharp
// Without static default
var config1 = new CompilerServicesConfiguration();  // Allocates
var config2 = new CompilerServicesConfiguration();  // Allocates again (same values!)

// With static default
var config1 = CompilerServicesConfiguration.Default;  // Reuses instance
var config2 = CompilerServicesConfiguration.Default;  // Reuses instance
```

---

## Usage Patterns

### Pattern 1: Default Configuration (Most Common)

```csharp
var services = new CompilerServicesBuilder()
    .WithSymbolTable(symbolTable)
    .WithSemanticInfo(semanticInfo)
    .Build();  // Uses CompilerServicesConfiguration.Default implicitly
```

### Pattern 2: Fail-Fast Mode (CI/CD)

```csharp
var config = new CompilerServicesConfiguration
{
    MaxErrors = 1,
    ContinueAfterErrors = false  // Stop immediately
};

var services = new CompilerServicesBuilder()
    .WithConfiguration(config)
    .WithSymbolTable(symbolTable)
    .WithSemanticInfo(semanticInfo)
    .Build();
```

### Pattern 3: Verbose Debugging

```csharp
var config = CompilerServicesConfiguration.Default with
{
    VerboseLogging = true
};

var services = new CompilerServicesBuilder()
    .WithConfiguration(config)
    // ... rest of builder
    .Build();
```

### Pattern 4: File-Specific Configuration

```csharp
var config = new CompilerServicesConfiguration
{
    InitialFilePath = "src/main.spy",
    MaxErrors = 20
};

var services = new CompilerServicesBuilder()
    .WithConfiguration(config)
    .WithSymbolTable(symbolTable)
    .WithSemanticInfo(semanticInfo)
    .Build();

// DiagnosticReporter will use "src/main.spy" in error messages
```

---

## Debugging Tips

### 1. Diagnosing "Compilation stopped early" Issues

If compilation stops unexpectedly, check:

```csharp
// In debugger, inspect:
services.Configuration.MaxErrors          // Hit the limit?
services.Configuration.ContinueAfterErrors // Set to false?
services.DiagnosticReporter.Diagnostics.ErrorCount  // How many errors?
```

### 2. Testing Error Thresholds

```csharp
// Force low threshold for testing
var config = new CompilerServicesConfiguration { MaxErrors = 3 };
var services = CompilerServicesBuilder.CreateForTesting() // Returns a builder? No, returns CompilerServices

// Actually, for testing:
var services = new CompilerServicesBuilder()
    .WithConfiguration(new CompilerServicesConfiguration { MaxErrors = 3 })
    .WithSymbolTable(new SymbolTable(new BuiltinRegistry()))
    .WithSemanticInfo(new SemanticInfo())
    .Build();

// Now test that compilation stops after 3 errors
```

### 3. Tracing Configuration Flow

Set a breakpoint in `CompilerServices` constructor (`CompilerServices.cs:72-75`) to see when configuration is applied:

```csharp
// Apply initial configuration
if (config.InitialFilePath != null)
{
    CurrentFilePath = config.InitialFilePath;  // <- Breakpoint here
}
```

---

## Contribution Guidelines

### When to Modify This File

**Add new properties when you need to configure**:
- New compiler phases (e.g., `EnableOptimizations`)
- Output settings (e.g., `EmitDebugInfo`)
- Performance tuning (e.g., `MaxParallelTasks`)
- New diagnostic behaviors (e.g., `TreatWarningsAsErrors`)

### When NOT to Modify This File

**Don't add**:
- Runtime state (use `CompilerServices` fields instead)
- Mutable configuration (breaks immutability guarantee)
- Logic or behavior (this is data-only)

### Adding a New Configuration Property

1. **Add the property with a default**:
   ```csharp
   public bool EnableOptimizations { get; init; } = false;
   ```

2. **Update `CompilerServices` to use it**:
   ```csharp
   // In CompilerServices.cs
   public bool ShouldOptimize() => _config.EnableOptimizations;
   ```

3. **Document in XML comments**:
   ```csharp
   /// <summary>
   /// Enable code optimizations during compilation.
   /// Default: false (faster compilation, easier debugging)
   /// </summary>
   public bool EnableOptimizations { get; init; } = false;
   ```

### Example: Adding `TreatWarningsAsErrors`

```csharp
/// <summary>
/// Treat all warnings as errors.
/// Useful for enforcing code quality in CI/CD.
/// Default: false
/// </summary>
public bool TreatWarningsAsErrors { get; init; } = false;
```

Then in `DiagnosticReporter`:

```csharp
public void ReportWarning(string message, Node location)
{
    if (_services.Configuration.TreatWarningsAsErrors)
    {
        ReportError(message, location);  // Promote to error
    }
    else
    {
        _diagnostics.AddWarning(message, location);
    }
}
```

---

## Cross-References

### Related Files in Services Layer

- **[`CompilerServices.md`](CompilerServices.md)** - Main service container that consumes this configuration
- **[`CompilerServicesBuilder.md`](CompilerServicesBuilder.md)** - Builder pattern that uses this configuration
- **[`DiagnosticReporter.md`](DiagnosticReporter.md)** - Uses `MaxErrors` and `ContinueAfterErrors` for error thresholds

### Related Documentation

- **[Services README](../../Services/README.md)** - Overview of the entire services layer architecture
- Project README's "Custom Slash Commands" section for CLI usage examples

### Key Integration Points

1. **Initialization**: `CompilerServicesBuilder.cs:12` (stores config)
2. **Usage**: `CompilerServices.cs:19, 41` (reads config)
3. **Error Control**: `CompilerServices.cs:159-166` (`ShouldContinue()` method)
4. **File Path Setup**: `CompilerServices.cs:72-75` (applies `InitialFilePath`)

---

## Summary

`CompilerServicesConfiguration` is a simple but critical piece of infrastructure:

- **What it is**: Immutable data class with compiler settings
- **What it does**: Controls error thresholds, logging, and file context
- **Why it exists**: Centralized, type-safe configuration for the compiler services layer
- **Design philosophy**: Immutable, sensible defaults, minimal dependencies

**Mental Model**: Think of this as the "control panel" you set up before starting compilation. Once set, it never changes—ensuring predictable compiler behavior throughout the compilation pipeline.
