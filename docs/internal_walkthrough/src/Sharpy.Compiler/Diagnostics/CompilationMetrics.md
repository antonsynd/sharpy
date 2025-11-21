# Walkthrough: CompilationMetrics.cs

**Source File**: `src/Sharpy.Compiler/Diagnostics/CompilationMetrics.cs`

---

## Overview

The `CompilationMetrics.cs` file provides **performance instrumentation** for the Sharpy compiler. It tracks timing and memory usage across different compilation phases (lexing, parsing, semantic analysis, code generation, etc.), enabling developers to:

- **Profile compilation performance** to identify bottlenecks
- **Monitor memory consumption** during each phase
- **Compare performance** across different compiler versions or configurations
- **Generate reports** in both human-readable text and machine-parsable JSON formats

This is a crucial tool for compiler optimization work and performance regression testing. The file contains three main classes:
- **`CompilationMetrics`** - Tracks metrics for a single file compilation
- **`PhaseMetric`** - Represents timing/memory data for one compilation phase
- **`ProjectCompilationMetrics`** - Aggregates metrics across multiple files in a project

---

## Class/Type Structure

### 1. `CompilationMetrics` Class

**Purpose**: Track performance metrics for compiling a single Sharpy file through multiple phases.

**Key Fields**:
```csharp
private readonly List<PhaseMetric> _phases = new();
private PhaseMetric? _currentPhase;
private readonly string? _fileName;
private readonly string? _projectName;
private readonly string? _configuration;
private readonly DateTime _startTime;
```

**Design Pattern**: This class uses the **State Pattern** - it maintains a `_currentPhase` that transitions through different states (null → active phase → null) as phases start and end.

**Thread Safety**: ⚠️ **Not thread-safe**. Each file compilation should have its own `CompilationMetrics` instance.

---

### 2. `PhaseMetric` Class

**Purpose**: A simple data container (DTO) representing metrics for one compilation phase.

**Properties**:
```csharp
public required string Name { get; init; }        // e.g., "Lexer", "Parser"
public DateTime StartTime { get; init; }
public DateTime EndTime { get; set; }
public long MemoryBefore { get; init; }
public long MemoryAfter { get; set; }

// Computed properties
public TimeSpan Duration => EndTime - StartTime;
public long MemoryDelta => MemoryAfter - MemoryBefore;
```

**Design Note**: Uses C# 9+ `init` accessors for immutability where possible, with `set` for `EndTime` and `MemoryAfter` which are populated when the phase ends.

---

### 3. `ProjectCompilationMetrics` Class

**Purpose**: Aggregate metrics from multiple file compilations plus optional assembly generation phase.

**Key Fields**:
```csharp
private readonly List<CompilationMetrics> _fileMetrics = new();
private readonly string _projectName;
private readonly string _configuration;
private readonly DateTime _startTime;
private CompilationMetrics? _assemblyMetrics;  // Optional: final assembly compilation
```

**Use Case**: When compiling a `.spyproj` project with multiple `.spy` files, this class collects metrics from each file's compilation and provides aggregate statistics.

---

## Key Functions/Methods

### CompilationMetrics: Phase Tracking

#### `StartPhase(string phaseName)`

**What it does**: Begin tracking a new compilation phase.

**Important Implementation Details**:
```csharp
public void StartPhase(string phaseName)
{
    // Ensures phases don't overlap - defensive programming
    if (_currentPhase != null)
    {
        throw new InvalidOperationException(
            $"Cannot start phase '{phaseName}' while phase '{_currentPhase.Name}' is still running"
        );
    }

    _currentPhase = new PhaseMetric
    {
        Name = phaseName,
        StartTime = DateTime.UtcNow,
        // Captures memory BEFORE phase begins
        MemoryBefore = GC.GetTotalMemory(forceFullCollection: false)
    };
}
```

**Key Design Decision**: `forceFullCollection: false` for `GC.GetTotalMemory()` 
- **Why?** We want to measure *actual* memory pressure without triggering a GC
- A forced collection would distort the measurements
- The delta between start/end memory gives a realistic picture of allocations

**How it fits into the codebase**: Called at the start of each compiler phase:
```csharp
// Example usage in Compiler.cs (pseudocode)
metrics.StartPhase("Lexer");
var tokens = lexer.Tokenize(source);
metrics.EndPhase();

metrics.StartPhase("Parser");
var ast = parser.Parse(tokens);
metrics.EndPhase();
```

---

#### `EndPhase()`

**What it does**: Complete tracking of the current phase and record its metrics.

**Implementation**:
```csharp
public void EndPhase()
{
    if (_currentPhase == null)
    {
        throw new InvalidOperationException("No phase is currently running");
    }

    _currentPhase.EndTime = DateTime.UtcNow;
    _currentPhase.MemoryAfter = GC.GetTotalMemory(forceFullCollection: false);
    _phases.Add(_currentPhase);  // Archive the completed phase
    _currentPhase = null;         // Ready for next phase
}
```

**Error Handling**: Throws if called without a matching `StartPhase()` - prevents silent bugs.

---

### CompilationMetrics: Formatting and Output

#### `FormatAsText()`

**What it does**: Generate a human-readable table of metrics suitable for console output.

**Output Example**:
```
=== Compilation Metrics ===
Project: calculator
File: src/main.spy
Configuration: Debug
Timestamp: 2025-11-21 23:20:00 UTC

Phase Breakdown:
Phase                          Duration        Memory Delta        
-----------------------------------------------------------------
Lexer                          15.42 ms        +124.50 KB          
Parser                         28.13 ms        +256.75 KB          
Semantic Analysis              42.89 ms        +512.25 KB          
Code Generation                35.67 ms        +384.00 KB          
-----------------------------------------------------------------
TOTAL                          122.11 ms       +1.25 MB            
```

**Implementation Details**:
- Uses `StringBuilder` for efficient string concatenation
- Fixed-width columns with left-alignment (`{phase.Name,-30}`)
- Calls `FormatMemoryDelta()` for human-friendly sizes (KB/MB/GB)

---

#### `FormatAsJson()`

**What it does**: Generate machine-readable JSON for automated analysis/tooling.

**Output Structure**:
```json
{
  "timestamp": "2025-11-21T23:20:00.0000000Z",
  "compiler_version": "0.5.0",
  "project_name": "calculator",
  "file_name": "src/main.spy",
  "configuration": "Debug",
  "total_duration_ms": 122.11,
  "total_memory_delta_bytes": 1310720,
  "phases": [
    {
      "phase": "Lexer",
      "duration_ms": 15.42,
      "memory_before_bytes": 1048576,
      "memory_after_bytes": 1176064,
      "memory_delta_bytes": 127488
    }
    // ... more phases
  ]
}
```

**Use Cases**:
- Performance regression testing in CI/CD
- Feeding data into monitoring systems (Grafana, Prometheus)
- Comparing performance across compiler versions
- Generating performance reports

**TODO Note**: Line 130 has a hardcoded version `"0.5.0"` with a TODO to extract from assembly metadata.

---

#### `FormatMemoryDelta(long bytes)` (Private Helper)

**What it does**: Convert raw byte counts to human-readable formats (B, KB, MB, GB).

**Key Features**:
```csharp
private static string FormatMemoryDelta(long bytes)
{
    if (bytes == 0) return "0 B";

    var sign = bytes < 0 ? "-" : "+";  // Shows if memory was freed
    var absBytes = bytes < 0 ? -bytes : bytes;

    if (absBytes < 1024) return $"{sign}{absBytes} B";
    if (absBytes < 1024 * 1024) return $"{sign}{absBytes / 1024.0:F2} KB";
    // ... MB, GB cases
}
```

**Why the sign?** Memory delta can be negative if a phase triggers garbage collection, making it visually clear whether memory increased or decreased.

---

### ProjectCompilationMetrics: Aggregation

#### `AggregatePhaseMetrics` Property

**What it does**: Combines metrics from all files, grouping by phase name.

**Implementation**:
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
                    current.Duration + phase.Duration,      // Sum durations
                    current.MemoryDelta + phase.MemoryDelta // Sum memory deltas
                );
            }
        }

        return aggregates;
    }
}
```

**Example**: If compiling 10 files, this sums all "Lexer" phases, all "Parser" phases, etc., giving you total time spent in each phase across the entire project.

**Performance Note**: This is a computed property (no caching). Called multiple times in `FormatAsText()` and `FormatAsJson()`, so could be optimized with lazy evaluation if profiling shows it's a bottleneck.

---

#### `TotalDuration` Property

**What it does**: Calculate total compilation time including all files and assembly generation.

```csharp
public TimeSpan TotalDuration
{
    get
    {
        var fileTotal = _fileMetrics.Sum(m => m.TotalDuration.TotalMilliseconds);
        var assemblyTotal = _assemblyMetrics?.TotalDuration.TotalMilliseconds ?? 0;
        return TimeSpan.FromMilliseconds(fileTotal + assemblyTotal);
    }
}
```

**Key Insight**: Assembly metrics are optional (null-coalescing `??` operator). Some compilations might not have a separate assembly phase.

---

## Dependencies

### Internal Dependencies
This file is **remarkably self-contained** with minimal dependencies:
- No dependencies on other Sharpy.Compiler components
- No coupling to Lexer, Parser, or CodeGen
- Clean separation of concerns

### External Dependencies
- **System.Diagnostics** - For diagnostic utilities (though not actively used in current implementation)
- **System.Text** - For `StringBuilder`
- **System.Text.Json** - For JSON serialization

### Where It's Used
The metrics classes are likely consumed by:
- **`Compiler.cs`** - Single-file compilation pipeline
- **`AssemblyCompiler.cs`** - Multi-file/project compilation
- **`Sharpy.Cli/Program.cs`** - CLI might output metrics with `--verbose` flag
- **Performance tests** in `Sharpy.Compiler.Tests/Performance/`

---

## Patterns and Design Decisions

### 1. **Builder/Fluent Pattern for Phase Tracking**
```csharp
metrics.StartPhase("Lexer");
// ... work ...
metrics.EndPhase();
```
Simple, clear API that enforces correct usage through runtime checks.

### 2. **Immutability Where Possible**
- `PhaseMetric` uses `init` accessors for most properties
- `_phases` list is exposed as `IReadOnlyList<PhaseMetric>`
- Prevents accidental mutation of historical metrics

### 3. **Separation of Concerns**
- **Data collection** (tracking phases) separated from **presentation** (formatting)
- Can add new output formats (XML, CSV) without changing core tracking logic

### 4. **Value Tuples for Aggregation**
```csharp
Dictionary<string, (TimeSpan Duration, long MemoryDelta)>
```
Modern C# feature for lightweight, named data grouping without defining a dedicated class.

### 5. **Defensive Programming**
- Throws exceptions for misuse (`StartPhase` while phase running, `EndPhase` without start)
- Prevents silent failures that would corrupt metrics

### 6. **UTC Timestamps**
```csharp
_startTime = DateTime.UtcNow;
```
Always use UTC for timestamps to avoid timezone issues in distributed systems or CI/CD environments.

---

## Debugging Tips

### 1. **Diagnosing Missing Metrics**
If a phase isn't showing up in output:
```csharp
// Add logging in StartPhase/EndPhase
Console.WriteLine($"[METRICS] Starting phase: {phaseName}");
Console.WriteLine($"[METRICS] Ending phase: {_currentPhase?.Name}");
```

### 2. **Memory Delta Anomalies**
If you see unexpected negative memory deltas:
- **Likely cause**: Garbage collection triggered during the phase
- **Debugging**: Add `GC.GetGeneration()` calls to see if GC ran
- **Normal behavior**: Small negative deltas are fine; large ones indicate GC pressure

### 3. **Phase Name Typos**
Common bug: Inconsistent phase names break aggregation:
```csharp
// BAD: Typo means these won't aggregate properly
metrics.StartPhase("Lexer");
// vs
metrics.StartPhase("Lexing");
```
**Solution**: Define phase names as constants:
```csharp
public static class CompilerPhases
{
    public const string Lexer = nameof(Lexer);
    public const string Parser = nameof(Parser);
    // ...
}
```

### 4. **Overlapping Phases**
If you get "Cannot start phase while phase is still running":
- **Check**: Did you forget to call `EndPhase()`?
- **Check**: Is there an exception path that skips `EndPhase()`?
- **Solution**: Use try-finally pattern:
```csharp
metrics.StartPhase("Parser");
try
{
    var ast = parser.Parse(tokens);
}
finally
{
    metrics.EndPhase();
}
```

### 5. **JSON Serialization Issues**
If JSON output looks wrong:
- Check for circular references (though shouldn't happen here)
- Verify property names match your expectations (PascalCase vs camelCase)
- Use `JsonSerializerOptions.WriteIndented = true` for readability

---

## Contribution Guidelines

### When to Modify This File

#### ✅ **Good Reasons to Change**
1. **Add new output formats** (CSV, XML, Prometheus format)
2. **Add more detailed metrics** (CPU usage, disk I/O)
3. **Extract compiler version dynamically** (fix TODO on line 130)
4. **Add phase filtering** (only report phases longer than X ms)
5. **Add statistical analysis** (min/max/avg per phase across files)
6. **Optimize `AggregatePhaseMetrics`** (cache the computed result)
7. **Add configurable memory collection mode** (allow forced GC if desired)

#### ❌ **Not Recommended**
1. **Coupling to compiler internals** - Keep this class agnostic to what phases mean
2. **Adding business logic** - This is pure data collection/formatting
3. **Breaking existing JSON schema** - Could break automated tools

---

### Example: Adding CSV Output

```csharp
/// <summary>
/// Format metrics as CSV for spreadsheet analysis
/// </summary>
public string FormatAsCsv()
{
    var sb = new StringBuilder();
    
    // Header
    sb.AppendLine("Phase,Duration_ms,Memory_Before_bytes,Memory_After_bytes,Memory_Delta_bytes");
    
    // Data rows
    foreach (var phase in _phases)
    {
        sb.AppendLine($"{phase.Name},{phase.Duration.TotalMilliseconds}," +
                     $"{phase.MemoryBefore},{phase.MemoryAfter},{phase.MemoryDelta}");
    }
    
    return sb.ToString();
}
```

---

### Example: Adding Phase Filtering

```csharp
/// <summary>
/// Get phases that took longer than the specified duration
/// </summary>
public IEnumerable<PhaseMetric> GetSlowestPhases(TimeSpan threshold)
{
    return _phases.Where(p => p.Duration > threshold)
                  .OrderByDescending(p => p.Duration);
}
```

---

### Testing Considerations

When modifying this file, test:

1. **Phase tracking accuracy**
   ```csharp
   [Fact]
   public void TestPhaseTracking()
   {
       var metrics = new CompilationMetrics();
       metrics.StartPhase("Test");
       Thread.Sleep(100);  // Simulate work
       metrics.EndPhase();
       
       Assert.Single(metrics.Phases);
       Assert.True(metrics.Phases[0].Duration.TotalMilliseconds >= 100);
   }
   ```

2. **Error cases**
   ```csharp
   [Fact]
   public void TestStartPhaseThrowsWhenPhaseAlreadyRunning()
   {
       var metrics = new CompilationMetrics();
       metrics.StartPhase("Phase1");
       Assert.Throws<InvalidOperationException>(() => metrics.StartPhase("Phase2"));
   }
   ```

3. **Output format stability** (for JSON - important for tooling)
   ```csharp
   [Fact]
   public void TestJsonFormatStability()
   {
       var metrics = new CompilationMetrics();
       metrics.StartPhase("Test");
       metrics.EndPhase();
       
       var json = metrics.FormatAsJson();
       var doc = JsonDocument.Parse(json);
       
       Assert.True(doc.RootElement.TryGetProperty("total_duration_ms", out _));
       Assert.True(doc.RootElement.TryGetProperty("phases", out _));
   }
   ```

---

### Performance Optimization Opportunities

1. **Cache `AggregatePhaseMetrics`**: Currently recomputed on each access
2. **Use `Span<T>`** for memory formatting to reduce allocations
3. **Pooled `StringBuilder`** instances for formatting
4. **Lazy JSON serialization** - only compute when requested

---

### Integration Points

To use this in the compiler pipeline:

```csharp
// In Compiler.cs or AssemblyCompiler.cs
public Assembly Compile(string source, CompilerOptions options)
{
    var metrics = new CompilationMetrics(
        fileName: options.SourceFile,
        configuration: options.Configuration
    );
    
    try
    {
        metrics.StartPhase("Lexer");
        var tokens = _lexer.Tokenize(source);
        metrics.EndPhase();
        
        metrics.StartPhase("Parser");
        var ast = _parser.Parse(tokens);
        metrics.EndPhase();
        
        // ... more phases ...
        
        if (options.ShowMetrics)
        {
            Console.WriteLine(metrics.FormatAsText());
        }
        
        if (options.MetricsOutputPath != null)
        {
            File.WriteAllText(options.MetricsOutputPath, metrics.FormatAsJson());
        }
        
        return assembly;
    }
    catch (Exception)
    {
        // If an exception occurs mid-phase, metrics might be incomplete
        // Consider whether to still output partial metrics for debugging
        throw;
    }
}
```

---

## Summary

The `CompilationMetrics.cs` file is a **clean, self-contained performance instrumentation system** for the Sharpy compiler. Its key strengths are:

- ✅ Simple, defensive API that prevents misuse
- ✅ No dependencies on compiler internals
- ✅ Multiple output formats (text, JSON)
- ✅ Aggregation support for multi-file projects
- ✅ Memory and timing tracking

For newcomers, this is a **great file to study** because it demonstrates:
- Modern C# features (`init`, value tuples, nullable reference types)
- Clean separation of concerns
- Builder pattern for state management
- Defensive programming techniques

When working with this file, remember: **Keep it simple**. Its value comes from being a lightweight, reliable measurement tool, not a complex analytics engine.
