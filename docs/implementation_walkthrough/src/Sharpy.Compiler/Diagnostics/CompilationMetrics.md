# Walkthrough: CompilationMetrics.cs

**Source File**: `src/Sharpy.Compiler/Diagnostics/CompilationMetrics.cs`

---

## 1. Overview

`CompilationMetrics.cs` is the performance monitoring backbone of the Sharpy compiler. It tracks **timing** and **memory usage** throughout the compilation pipeline, providing essential insights for:

- **Performance optimization**: Identifying slow compiler phases
- **Memory profiling**: Detecting memory-hungry operations
- **Benchmarking**: Comparing compiler performance across versions
- **Debugging**: Understanding where compilation time is spent

Think of this file as a stopwatch and memory meter for the compiler—it doesn't change *what* the compiler does, but tells you *how efficiently* it does it.

**Key Use Cases:**
- Single-file compilation metrics (via `CompilationMetrics`)
- Multi-file project aggregation (via `ProjectCompilationMetrics`)
- Export to human-readable text or machine-parseable JSON

---

## 2. Class/Type Structure

The file defines three main types organized in a hierarchical relationship:

```
PhaseMetric (individual measurement)
    ↓ used by
CompilationMetrics (single file)
    ↓ aggregated by
ProjectCompilationMetrics (entire project)
```

### 2.1 `PhaseMetric` Class

```csharp
public class PhaseMetric
{
    public required string Name { get; init; }
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; set; }
    public long MemoryBefore { get; init; }
    public long MemoryAfter { get; set; }
    
    public TimeSpan Duration => EndTime - StartTime;
    public long MemoryDelta => MemoryAfter - MemoryBefore;
}
```

**Purpose**: Represents a single compilation phase (e.g., "Lexical Analysis", "Type Checking").

**Design Notes:**
- Uses C# 11's `required` keyword for `Name` to enforce initialization
- `StartTime` and `MemoryBefore` are immutable (`init`) once set
- `EndTime` and `MemoryAfter` are mutable (`set`) since they're populated when the phase completes
- Computed properties (`Duration`, `MemoryDelta`) avoid storing redundant data

### 2.2 `CompilationMetrics` Class

**Purpose**: Tracks metrics for compiling a single `.spy` file through all compiler phases.

**Core Responsibilities:**
1. **Phase lifecycle management**: Start/End phase tracking
2. **Data collection**: Capture timing and memory snapshots
3. **Aggregation**: Sum totals across all phases
4. **Reporting**: Format as text or JSON

**State Management:**
```csharp
private readonly List<PhaseMetric> _phases = new();      // Completed phases
private PhaseMetric? _currentPhase;                       // Phase in progress
private readonly string? _fileName;                       // Optional context
private readonly string? _projectName;                    // Optional context
private readonly string? _configuration;                  // e.g., "Debug"/"Release"
private readonly DateTime _startTime;                     // Overall start timestamp
```

### 2.3 `ProjectCompilationMetrics` Class

**Purpose**: Aggregates metrics across multiple files in a project compilation.

**Dual Tracking Model:**
1. **File metrics**: One `CompilationMetrics` per `.spy` file
2. **Assembly metrics**: Separate tracking for the final .NET assembly generation phase

**Key Insight**: Project compilation is two-stage:
```
Stage 1: Compile each .spy file → C# code (tracked per file)
Stage 2: Compile all C# → .NET assembly (tracked separately)
```

---

## 3. Key Functions/Methods

### 3.1 Phase Tracking (`CompilationMetrics`)

#### `StartPhase(string phaseName)`

```csharp
public void StartPhase(string phaseName)
{
    if (_currentPhase != null)
    {
        throw new InvalidOperationException($"Cannot start phase '{phaseName}' while phase '{_currentPhase.Name}' is still running");
    }
    
    _currentPhase = new PhaseMetric
    {
        Name = phaseName,
        StartTime = DateTime.UtcNow,
        MemoryBefore = GC.GetTotalMemory(forceFullCollection: false)
    };
}
```

**What it does**: Begins tracking a named compilation phase.

**Key Parameters:**
- `phaseName`: Human-readable phase identifier (e.g., "Lexical Analysis", "Type Resolution")

**Important Details:**
- **Single-phase restriction**: Enforces that only one phase can be active at a time—prevents overlapping measurements
- **Memory snapshot**: Uses `GC.GetTotalMemory(forceFullCollection: false)` for fast memory reading without triggering GC
- **UTC timestamps**: Uses `DateTime.UtcNow` for consistency across timezones

**Usage Pattern in Compiler.cs:**
```csharp
fileMetrics.StartPhase("Lexical Analysis");
var tokens = lexer.Tokenize();
fileMetrics.EndPhase();

fileMetrics.StartPhase("Syntax Analysis");
var ast = parser.Parse(tokens);
fileMetrics.EndPhase();
```

#### `EndPhase()`

```csharp
public void EndPhase()
{
    if (_currentPhase == null)
    {
        throw new InvalidOperationException("No phase is currently running");
    }
    
    _currentPhase.EndTime = DateTime.UtcNow;
    _currentPhase.MemoryAfter = GC.GetTotalMemory(forceFullCollection: false);
    _phases.Add(_currentPhase);
    _currentPhase = null;
}
```

**What it does**: Completes the current phase and records its final metrics.

**State Transition**: Active phase → Completed phase (added to `_phases` list)

**Error Handling**: Throws if called without an active phase (prevents mismatched Start/End calls)

### 3.2 Aggregation Properties

#### `TotalDuration`

```csharp
public TimeSpan TotalDuration => _phases.Sum(p => p.Duration.TotalMilliseconds) > 0
    ? TimeSpan.FromMilliseconds(_phases.Sum(p => p.Duration.TotalMilliseconds))
    : TimeSpan.Zero;
```

**What it does**: Sums the duration of all completed phases.

**Design Choice**: Returns `TimeSpan.Zero` if no phases recorded (defensive programming—avoids edge case issues)

#### `TotalMemoryDelta`

```csharp
public long TotalMemoryDelta => _phases.Sum(p => p.MemoryDelta);
```

**What it does**: Sums memory deltas (can be positive or negative).

**Interpretation:**
- **Positive delta**: Phase allocated more memory than it freed
- **Negative delta**: Phase freed more memory (e.g., via GC during the phase)
- **Zero delta**: Net-neutral memory impact

### 3.3 Formatting Methods

#### `FormatAsText()`

**Purpose**: Generate human-readable report for console output.

**Output Structure:**
```
=== Compilation Metrics ===
Project: MyProject
File: example.spy
Configuration: Debug
Timestamp: 2025-12-27 00:45:00 UTC

Phase Breakdown:
Phase                          Duration        Memory Delta        
-----------------------------------------------------------------
Lexical Analysis               12.45 ms        +128.50 KB          
Syntax Analysis                23.67 ms        +256.75 KB          
Name Resolution                8.90 ms         +64.25 KB           
Type Resolution                15.32 ms        +92.00 KB           
Type Checking                  19.88 ms        +112.30 KB          
Code Generation                34.21 ms        +345.60 KB          
-----------------------------------------------------------------
TOTAL                          114.43 ms       +999.40 KB          
```

**Key Implementation Details:**
- Fixed-width columns using string interpolation: `$"{phase.Name,-30}"` (left-aligned, 30 chars)
- Calls `FormatMemoryDelta()` for human-friendly byte formatting
- Includes optional metadata (project, file, config) when available

#### `FormatAsJson()`

**Purpose**: Generate machine-parseable JSON for tooling integration.

**Output Schema:**
```json
{
  "timestamp": "2025-12-27T00:45:00.0000000Z",
  "compiler_version": "0.5.0",
  "project_name": "MyProject",
  "file_name": "example.spy",
  "configuration": "Debug",
  "total_duration_ms": 114.43,
  "total_memory_delta_bytes": 1023385,
  "phases": [
    {
      "phase": "Lexical Analysis",
      "duration_ms": 12.45,
      "memory_before_bytes": 5242880,
      "memory_after_bytes": 5374336,
      "memory_delta_bytes": 131456
    }
    // ... more phases
  ]
}
```

**Design Choices:**
- ISO 8601 timestamp format (`"o"`) for international compatibility
- Preserves raw byte counts (not formatted) for precise analysis
- Uses anonymous types with `JsonSerializer` for simplicity

**TODO Note**: Line 131 has a hardcoded version—should use `Assembly.GetExecutingAssembly().GetName().Version` instead.

#### `FormatMemoryDelta(long bytes)` (private helper)

```csharp
private static string FormatMemoryDelta(long bytes)
{
    if (bytes == 0)
        return "0 B";
    
    var sign = bytes < 0 ? "-" : "+";
    var absBytes = bytes < 0 ? -bytes : bytes;
    
    if (absBytes < 1024)
        return $"{sign}{absBytes} B";
    if (absBytes < 1024 * 1024)
        return $"{sign}{absBytes / 1024.0:F2} KB";
    if (absBytes < 1024 * 1024 * 1024)
        return $"{sign}{absBytes / (1024.0 * 1024.0):F2} MB";
    
    return $"{sign}{absBytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
}
```

**What it does**: Converts raw bytes to human-readable format (B/KB/MB/GB).

**Features:**
- **Sign preservation**: Shows `+` for allocations, `-` for deallocations
- **Cascading units**: Automatically scales to appropriate unit (1024-based, not 1000)
- **Two decimal precision**: `F2` format specifier for readability

### 3.4 Project-Level Methods (`ProjectCompilationMetrics`)

#### `AddFileMetrics(CompilationMetrics metrics)`

**Purpose**: Register metrics from a single compiled file.

**Usage Pattern:**
```csharp
var projectMetrics = new ProjectCompilationMetrics("MyApp", "Release");

foreach (var file in sharpyFiles)
{
    var fileMetrics = CompileFile(file);
    projectMetrics.AddFileMetrics(fileMetrics);
}
```

#### `SetAssemblyMetrics(CompilationMetrics metrics)`

**Purpose**: Store metrics from the final assembly compilation phase (C# → .NET IL).

**Why Separate?**: Assembly compilation is qualitatively different—it operates on generated C# code, not `.spy` files.

#### `AggregatePhaseMetrics` Property

```csharp
public Dictionary<string, (TimeSpan Duration, long MemoryDelta)> AggregatePhaseMetrics
{
    get
    {
        var aggregates = new Dictionary<string, (TimeSpan Duration, long MemoryDelta)>();
        
        foreach (var fileMetric in _fileMetrics)
        {
            foreach (var phase in fileMetric.Phases)
            {
                if (!aggregates.ContainsKey(phase.Name))
                {
                    aggregates[phase.Name] = (TimeSpan.Zero, 0);
                }
                
                var current = aggregates[phase.Name];
                aggregates[phase.Name] = (
                    current.Duration + phase.Duration,
                    current.MemoryDelta + phase.MemoryDelta
                );
            }
        }
        
        return aggregates;
    }
}
```

**What it does**: Merges same-named phases across all files.

**Example**: If you compile 10 files:
- "Lexical Analysis" duration = sum of 10 lexer runs
- "Type Checking" memory delta = sum of 10 type checker memory deltas

**Use Case**: Understanding which compiler phase is the bottleneck across the entire project.

---

## 4. Dependencies

### Internal Dependencies
- **`src/Sharpy.Compiler/Compiler.cs`**: Primary consumer—wraps each compiler phase with metrics
- **`src/Sharpy.Compiler/AssemblyCompiler.cs`**: Uses metrics for assembly-level compilation
- **`src/Sharpy.Cli/Program.cs`**: CLI entry point that displays metrics to users

### External Dependencies
- **`System.Diagnostics`**: For `Stopwatch`-like functionality (though not explicitly used—could be optimization opportunity)
- **`System.Text.Json`**: JSON serialization for machine-readable output
- **`System.Text.StringBuilder`**: Efficient string concatenation for reports

### .NET BCL Dependencies
- **`GC.GetTotalMemory()`**: Memory profiling API
- **`DateTime.UtcNow`**: High-resolution timestamps
- **LINQ**: Aggregation operations (`Sum`, `Select`, `OrderBy`)

---

## 5. Patterns and Design Decisions

### 5.1 Builder/Lifecycle Pattern

The `StartPhase()` / `EndPhase()` pair implements a **structured lifecycle pattern**:

```csharp
// Correct usage
metrics.StartPhase("Phase Name");
try {
    DoWork();
} finally {
    metrics.EndPhase();  // Ensures phase is closed even on exception
}
```

**Benefits:**
- Clear phase boundaries
- Impossible to forget to capture end metrics (enforced by exceptions)
- Works well with `try-finally` for exception safety

### 5.2 Immutable + Mutable Hybrid (PhaseMetric)

```csharp
public required string Name { get; init; }     // Immutable
public DateTime EndTime { get; set; }           // Mutable
```

**Rationale:**
- `Name`, `StartTime`, `MemoryBefore` are **set once** at phase start
- `EndTime`, `MemoryAfter` are **set later** at phase end
- Computed properties (`Duration`, `MemoryDelta`) derive from mutable state

This avoids creating a new object when ending a phase while maintaining partial immutability.

### 5.3 Defensive Nullability

```csharp
private PhaseMetric? _currentPhase;  // Nullable: may not be tracking a phase
```

**Pattern**: Null represents "no active phase", with explicit checks:
```csharp
if (_currentPhase != null)
    throw new InvalidOperationException(...);
```

This prevents silent bugs from mismatched Start/End calls.

### 5.4 Readonly Collections for Safety

```csharp
public IReadOnlyList<PhaseMetric> Phases => _phases.AsReadOnly();
```

**Why?**: Prevents external code from modifying the internal metrics list—only the `CompilationMetrics` class should control its data.

### 5.5 Optional Metadata Pattern

```csharp
public CompilationMetrics(string? fileName = null, string? projectName = null, string? configuration = null)
```

**Design Choice**: All context parameters are nullable/optional because:
- Single-file compilation may not have a project name
- Quick tests may not set configuration
- Flexibility for different compilation scenarios

### 5.6 DRY Violation (FormatMemoryDelta duplicated)

**Issue**: `FormatMemoryDelta()` appears identically in both `CompilationMetrics` and `ProjectCompilationMetrics`.

**Why Not Refactored?**: Likely an oversight—should be extracted to a shared static helper class.

**Opportunity for Improvement**: See "Contribution Guidelines" section below.

---

## 6. Debugging Tips

### 6.1 Mismatched Start/End Calls

**Symptom**: Exception: `"Cannot start phase 'X' while phase 'Y' is still running"`

**Cause**: A previous phase wasn't ended before starting a new one.

**Debug Strategy:**
1. Search for `StartPhase("Y")` in the codebase
2. Verify every path has a matching `EndPhase()`
3. Check for early returns or exceptions that skip `EndPhase()`
4. Use `try-finally` to ensure phases always close:

```csharp
metrics.StartPhase("Risky Operation");
try {
    MightThrowException();
} finally {
    metrics.EndPhase();
}
```

### 6.2 Memory Delta Anomalies

**Symptom**: Negative or unexpectedly large memory deltas.

**Possible Causes:**
1. **GC ran during phase**: `GC.Collect()` between snapshots causes negative delta
2. **Background GC**: .NET's concurrent GC can free memory mid-phase
3. **Measurement granularity**: `GC.GetTotalMemory(false)` is an estimate, not exact

**Mitigation:**
- Don't read too much into small negative deltas (< 1 MB)
- For accurate measurements, force GC before/after: `GC.Collect(); GC.WaitForPendingFinalizers()`
  - ⚠️ Warning: This is **slow**—only for profiling, not production

**Code Location to Modify:**
```csharp
// Line 41-42, 56 in CompilationMetrics.cs
MemoryBefore = GC.GetTotalMemory(forceFullCollection: false)  // Change to true for accuracy
```

### 6.3 Phase Name Typos

**Symptom**: Aggregate metrics show duplicate phases like "Lexical Analsis" and "Lexical Analysis".

**Root Cause**: Hardcoded strings in multiple locations.

**Debug Strategy:**
```bash
# Find all phase names
grep -r "StartPhase(" src/Sharpy.Compiler/ | grep -o '"[^"]*"' | sort -u
```

**Best Practice**: Define phase names as constants:
```csharp
public static class CompilerPhases
{
    public const string LexicalAnalysis = "Lexical Analysis";
    public const string SyntaxAnalysis = "Syntax Analysis";
    // ...
}
```

### 6.4 JSON Schema Changes

**Symptom**: External tools break after compiler updates.

**Prevention**: 
1. Version the JSON schema: Add `"schema_version": 1` field
2. Document breaking changes in release notes
3. Maintain backward compatibility when possible

### 6.5 Performance Overhead

**Question**: Does metrics collection slow down compilation?

**Answer**: Minimal overhead:
- `DateTime.UtcNow`: ~20-50 nanoseconds
- `GC.GetTotalMemory(false)`: ~1-10 microseconds
- Total overhead per phase: **< 10 microseconds** (negligible)

**When to Disable**: Never in normal usage—overhead is insignificant. For benchmarking the compiler itself, add a `--no-metrics` flag.

---

## 7. Contribution Guidelines

### 7.1 Potential Improvements

#### **Refactor Duplicate Code**
**Priority**: Medium  
**Effort**: Low (15 minutes)

```csharp
// Create new file: src/Sharpy.Compiler/Diagnostics/MetricsFormatting.cs
namespace Sharpy.Compiler.Diagnostics;

public static class MetricsFormatting
{
    public static string FormatMemoryDelta(long bytes)
    {
        // Move implementation here
    }
}
```

Then update both classes to use `MetricsFormatting.FormatMemoryDelta()`.

#### **Extract Compiler Version**
**Priority**: High (affects all output)  
**Effort**: Low (5 minutes)

```csharp
// Line 131 and 346: Replace hardcoded "0.5.0"
private static string CompilerVersion => 
    Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
```

#### **Add Phase Name Constants**
**Priority**: Medium (prevents typos)  
**Effort**: Medium (30 minutes—requires updating all call sites)

```csharp
// New file: src/Sharpy.Compiler/Diagnostics/CompilerPhases.cs
public static class CompilerPhases
{
    public const string LexicalAnalysis = "Lexical Analysis";
    public const string SyntaxAnalysis = "Syntax Analysis";
    public const string NameResolution = "Name Resolution";
    public const string TypeResolution = "Type Resolution";
    public const string TypeChecking = "Type Checking";
    public const string CodeGeneration = "Code Generation";
}
```

#### **Add CSV Export Format**
**Priority**: Low  
**Effort**: Medium (1 hour)

Useful for spreadsheet analysis:
```csharp
public string FormatAsCsv()
{
    var sb = new StringBuilder();
    sb.AppendLine("Phase,Duration_ms,MemoryBefore_bytes,MemoryAfter_bytes,MemoryDelta_bytes");
    foreach (var phase in _phases)
    {
        sb.AppendLine($"{phase.Name},{phase.Duration.TotalMilliseconds}," +
                     $"{phase.MemoryBefore},{phase.MemoryAfter},{phase.MemoryDelta}");
    }
    return sb.ToString();
}
```

#### **Add Benchmarking Mode**
**Priority**: Low  
**Effort**: High (2-3 hours)

For comparing compiler performance across commits:
```csharp
public class BenchmarkMetrics : CompilationMetrics
{
    private readonly Stopwatch _stopwatch = new();
    
    public override void StartPhase(string phaseName)
    {
        // Force GC for consistent measurements
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        base.StartPhase(phaseName);
    }
    
    // Add statistical measures (min, max, stddev across runs)
}
```

### 7.2 Testing Additions

**Current State**: No explicit tests for `CompilationMetrics.cs`.

**Recommended Tests** (create in `src/Sharpy.Compiler.Tests/Diagnostics/`):

```csharp
[Fact]
public void StartPhase_WithActivePhase_ThrowsException()
{
    var metrics = new CompilationMetrics();
    metrics.StartPhase("Phase 1");
    
    Assert.Throws<InvalidOperationException>(() => 
        metrics.StartPhase("Phase 2"));
}

[Fact]
public void EndPhase_WithoutActivePhase_ThrowsException()
{
    var metrics = new CompilationMetrics();
    
    Assert.Throws<InvalidOperationException>(() => 
        metrics.EndPhase());
}

[Fact]
public void TotalDuration_SumsAllPhases()
{
    var metrics = new CompilationMetrics();
    
    metrics.StartPhase("Phase 1");
    Thread.Sleep(100);  // Simulate work
    metrics.EndPhase();
    
    metrics.StartPhase("Phase 2");
    Thread.Sleep(150);
    metrics.EndPhase();
    
    Assert.True(metrics.TotalDuration.TotalMilliseconds >= 250);
}

[Fact]
public void FormatAsJson_ProducesValidJson()
{
    var metrics = new CompilationMetrics("test.spy", "TestProject", "Debug");
    metrics.StartPhase("Test Phase");
    metrics.EndPhase();
    
    var json = metrics.FormatAsJson();
    var doc = JsonDocument.Parse(json);  // Should not throw
    
    Assert.Equal("TestProject", doc.RootElement.GetProperty("project_name").GetString());
}
```

### 7.3 Documentation Enhancements

**Missing Documentation**:
1. **User guide**: How to interpret metrics output (add to `docs/guides/performance_profiling.md`)
2. **Benchmarking guide**: Best practices for comparing compiler versions
3. **JSON schema reference**: Document all fields for tool authors

### 7.4 When to Modify This File

**Add Metrics For:**
- ✅ New compiler phases (e.g., "Optimization", "Macro Expansion")
- ✅ New output formats (CSV, XML, binary protobuf)
- ✅ Additional metadata (CPU usage, disk I/O)

**Don't Modify For:**
- ❌ Changing phase names (breaks existing tools—deprecate old, add new)
- ❌ Removing fields from JSON output (breaks compatibility)

**Breaking Change Process:**
1. Add `schema_version` field to JSON
2. Increment version number
3. Support both old and new formats for 2 releases
4. Document migration path in release notes

### 7.5 Code Style Alignment

**Current Style:**
- ✅ Uses C# 11 features (`required` keyword)
- ✅ Expression-bodied members for computed properties
- ✅ Nullable reference types enabled
- ✅ Private fields prefixed with underscore: `_phases`

**To Match Sharpy Conventions:**
- Consider adding XML doc comments to private methods (`FormatMemoryDelta`)
- Extract magic numbers to named constants: `1024` → `BytesPerKilobyte`
- Add `#nullable enable` directive if not globally enabled

---

## Summary

`CompilationMetrics.cs` is a self-contained, well-structured performance monitoring system. It successfully separates instrumentation from compiler logic, making it easy to add metrics to new phases without polluting business logic.

**Key Takeaways for Newcomers:**
1. **Always match Start/End calls**: Use `try-finally` for safety
2. **Memory deltas are estimates**: Don't over-interpret small fluctuations
3. **Extend carefully**: Consider backward compatibility for JSON output
4. **Test manually**: Run the compiler with `--help` to see if metrics flags exist (CLI integration)

**Next Steps:**
- Read `src/Sharpy.Compiler/Compiler.cs` to see how metrics integrate with the compilation pipeline
- Experiment: Add a new phase in a test branch and observe the metrics output
- Profile: Run the compiler on `samples/` and analyze which phases are slowest
