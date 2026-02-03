using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Sharpy.Compiler.Diagnostics;

/// <summary>
/// Tracks compilation phase metrics including timing and memory usage
/// </summary>
public class CompilationMetrics
{
    private readonly List<PhaseMetric> _phases = new();
    private PhaseMetric? _currentPhase;
    private readonly string? _fileName;
    private readonly string? _projectName;
    private readonly string? _configuration;
    private readonly DateTime _startTime;

    public CompilationMetrics(string? fileName = null, string? projectName = null, string? configuration = null)
    {
        _fileName = fileName;
        _projectName = projectName;
        _configuration = configuration;
        _startTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Start tracking a compilation phase
    /// </summary>
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

    /// <summary>
    /// End tracking the current compilation phase
    /// </summary>
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

    /// <summary>
    /// Get all recorded phase metrics
    /// </summary>
    public IReadOnlyList<PhaseMetric> Phases => _phases.AsReadOnly();

    /// <summary>
    /// Get total duration across all phases
    /// </summary>
    public TimeSpan TotalDuration => _phases.Sum(p => p.Duration.TotalMilliseconds) > 0
        ? TimeSpan.FromMilliseconds(_phases.Sum(p => p.Duration.TotalMilliseconds))
        : TimeSpan.Zero;

    /// <summary>
    /// Get total memory delta across all phases
    /// </summary>
    public long TotalMemoryDelta => _phases.Sum(p => p.MemoryDelta);

    /// <summary>
    /// Format metrics as human-readable text
    /// </summary>
    public string FormatAsText()
    {
        var sb = new StringBuilder();

        sb.AppendLine("=== Compilation Metrics ===");

        if (!string.IsNullOrEmpty(_projectName))
        {
            sb.AppendLine($"Project: {_projectName}");
        }

        if (!string.IsNullOrEmpty(_fileName))
        {
            sb.AppendLine($"File: {_fileName}");
        }

        if (!string.IsNullOrEmpty(_configuration))
        {
            sb.AppendLine($"Configuration: {_configuration}");
        }

        sb.AppendLine($"Timestamp: {_startTime:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();

        sb.AppendLine("Phase Breakdown:");
        sb.AppendLine($"{"Phase",-30} {"Duration",-15} {"Memory Delta",-20}");
        sb.AppendLine(new string('-', 65));

        foreach (var phase in _phases)
        {
            var durationStr = $"{phase.Duration.TotalMilliseconds:F2} ms";
            var memoryStr = FormatMemoryDelta(phase.MemoryDelta);
            sb.AppendLine($"{phase.Name,-30} {durationStr,-15} {memoryStr,-20}");
        }

        sb.AppendLine(new string('-', 65));
        sb.AppendLine($"{"TOTAL",-30} {TotalDuration.TotalMilliseconds:F2} ms{"",-4} {FormatMemoryDelta(TotalMemoryDelta),-20}");

        return sb.ToString();
    }

    /// <summary>
    /// Format metrics as JSON
    /// </summary>
    public string FormatAsJson()
    {
        var data = new
        {
            timestamp = _startTime.ToString("o"),
            compiler_version = "0.5.0", // See: #97
            project_name = _projectName,
            file_name = _fileName,
            configuration = _configuration,
            total_duration_ms = TotalDuration.TotalMilliseconds,
            total_memory_delta_bytes = TotalMemoryDelta,
            phases = _phases.Select(p => new
            {
                phase = p.Name,
                duration_ms = p.Duration.TotalMilliseconds,
                memory_before_bytes = p.MemoryBefore,
                memory_after_bytes = p.MemoryAfter,
                memory_delta_bytes = p.MemoryDelta
            }).ToList()
        };

        return JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

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
}

/// <summary>
/// Metrics for a single compilation phase
/// </summary>
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

/// <summary>
/// Aggregates metrics from multiple files in a project compilation
/// </summary>
public class ProjectCompilationMetrics
{
    private readonly List<CompilationMetrics> _fileMetrics = new();
    private readonly List<string> _skippedFiles = new();
    private readonly string _projectName;
    private readonly string _configuration;
    private readonly DateTime _startTime;
    private CompilationMetrics? _assemblyMetrics;

    public ProjectCompilationMetrics(string projectName, string configuration)
    {
        _projectName = projectName;
        _configuration = configuration;
        _startTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Add metrics for a compiled file
    /// </summary>
    public void AddFileMetrics(CompilationMetrics metrics)
    {
        _fileMetrics.Add(metrics);
    }

    /// <summary>
    /// Add a file that was skipped during incremental compilation
    /// </summary>
    public void AddSkippedFile(string filePath)
    {
        _skippedFiles.Add(filePath);
    }

    /// <summary>
    /// Set assembly compilation metrics
    /// </summary>
    public void SetAssemblyMetrics(CompilationMetrics metrics)
    {
        _assemblyMetrics = metrics;
    }

    /// <summary>
    /// Get metrics for all files
    /// </summary>
    public IReadOnlyList<CompilationMetrics> FileMetrics => _fileMetrics.AsReadOnly();

    /// <summary>
    /// Get list of files skipped during incremental compilation
    /// </summary>
    public IReadOnlyList<string> SkippedFiles => _skippedFiles.AsReadOnly();

    /// <summary>
    /// Get assembly compilation metrics
    /// </summary>
    public CompilationMetrics? AssemblyMetrics => _assemblyMetrics;

    /// <summary>
    /// Get total files compiled (not including skipped)
    /// </summary>
    public int TotalFiles => _fileMetrics.Count;

    /// <summary>
    /// Get number of files skipped during incremental compilation
    /// </summary>
    public int SkippedFileCount => _skippedFiles.Count;

    /// <summary>
    /// Get aggregate metrics across all phases
    /// </summary>
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

    /// <summary>
    /// Get total duration across all files and phases
    /// </summary>
    public TimeSpan TotalDuration
    {
        get
        {
            var fileTotal = _fileMetrics.Sum(m => m.TotalDuration.TotalMilliseconds);
            var assemblyTotal = _assemblyMetrics?.TotalDuration.TotalMilliseconds ?? 0;
            return TimeSpan.FromMilliseconds(fileTotal + assemblyTotal);
        }
    }

    /// <summary>
    /// Get total memory delta across all files
    /// </summary>
    public long TotalMemoryDelta
    {
        get
        {
            var fileTotal = _fileMetrics.Sum(m => m.TotalMemoryDelta);
            var assemblyTotal = _assemblyMetrics?.TotalMemoryDelta ?? 0;
            return fileTotal + assemblyTotal;
        }
    }

    /// <summary>
    /// Format project metrics as text
    /// </summary>
    public string FormatAsText()
    {
        var sb = new StringBuilder();

        sb.AppendLine("=== Project Compilation Metrics ===");
        sb.AppendLine($"Project: {_projectName}");
        sb.AppendLine($"Configuration: {_configuration}");
        sb.AppendLine($"Timestamp: {_startTime:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"Files Compiled: {TotalFiles}");
        sb.AppendLine();

        sb.AppendLine("Aggregate Phase Breakdown:");
        sb.AppendLine($"{"Phase",-30} {"Total Duration",-15} {"Total Memory Delta",-20}");
        sb.AppendLine(new string('-', 65));

        var aggregates = AggregatePhaseMetrics;
        foreach (var (phase, metrics) in aggregates.OrderBy(kvp => kvp.Key))
        {
            var durationStr = $"{metrics.Duration.TotalMilliseconds:F2} ms";
            var memoryStr = FormatMemoryDelta(metrics.MemoryDelta);
            sb.AppendLine($"{phase,-30} {durationStr,-15} {memoryStr,-20}");
        }

        // Add assembly compilation metrics if available
        if (_assemblyMetrics != null)
        {
            sb.AppendLine();
            sb.AppendLine("Assembly Compilation:");
            foreach (var phase in _assemblyMetrics.Phases)
            {
                var durationStr = $"{phase.Duration.TotalMilliseconds:F2} ms";
                var memoryStr = FormatMemoryDelta(phase.MemoryDelta);
                sb.AppendLine($"{phase.Name,-30} {durationStr,-15} {memoryStr,-20}");
            }
        }

        sb.AppendLine(new string('-', 65));
        sb.AppendLine($"{"TOTAL",-30} {TotalDuration.TotalMilliseconds:F2} ms{"",-4} {FormatMemoryDelta(TotalMemoryDelta),-20}");

        return sb.ToString();
    }

    /// <summary>
    /// Format project metrics as JSON
    /// </summary>
    public string FormatAsJson()
    {
        var aggregates = AggregatePhaseMetrics;

        var data = new
        {
            timestamp = _startTime.ToString("o"),
            compiler_version = "0.5.0", // See: #97
            project_name = _projectName,
            configuration = _configuration,
            total_files = TotalFiles,
            total_duration_ms = TotalDuration.TotalMilliseconds,
            total_memory_delta_bytes = TotalMemoryDelta,
            aggregate_phases = aggregates.Select(kvp => new
            {
                phase = kvp.Key,
                total_duration_ms = kvp.Value.Duration.TotalMilliseconds,
                total_memory_delta_bytes = kvp.Value.MemoryDelta
            }).ToList(),
            assembly_compilation = _assemblyMetrics != null ? new
            {
                phases = _assemblyMetrics.Phases.Select(p => new
                {
                    phase = p.Name,
                    duration_ms = p.Duration.TotalMilliseconds,
                    memory_before_bytes = p.MemoryBefore,
                    memory_after_bytes = p.MemoryAfter,
                    memory_delta_bytes = p.MemoryDelta
                }).ToList()
            } : null,
            files = _fileMetrics.Select(fm => new
            {
                file_name = fm.Phases.FirstOrDefault()?.Name, // Will be set properly when we add file tracking
                total_duration_ms = fm.TotalDuration.TotalMilliseconds,
                total_memory_delta_bytes = fm.TotalMemoryDelta,
                phases = fm.Phases.Select(p => new
                {
                    phase = p.Name,
                    duration_ms = p.Duration.TotalMilliseconds,
                    memory_before_bytes = p.MemoryBefore,
                    memory_after_bytes = p.MemoryAfter,
                    memory_delta_bytes = p.MemoryDelta
                }).ToList()
            }).ToList()
        };

        return JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

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
}
