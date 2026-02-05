using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Sharpy.Compiler.Diagnostics;

/// <summary>
/// Tracks compilation phase metrics including timing and memory usage.
/// </summary>
/// <remarks>
/// <para>
/// This class provides granular timing data for each compilation phase,
/// per-validator timing during semantic analysis, and artifact counts.
/// Use the per-phase timing properties (e.g., <see cref="LexerTime"/>,
/// <see cref="ParserTime"/>) for quick access to specific phase durations.
/// </para>
/// <para>
/// For performance analysis, the <see cref="ValidatorTimes"/> dictionary
/// provides per-validator timing during the validation phase, and the
/// count properties (<see cref="TokenCount"/>, <see cref="AstNodeCount"/>,
/// <see cref="SymbolCount"/>, <see cref="DiagnosticCount"/>) provide
/// context for interpreting the timing data.
/// </para>
/// </remarks>
public class CompilationMetrics
{
    private readonly List<PhaseMetric> _phases = new();
    private PhaseMetric? _currentPhase;
    private readonly string? _fileName;
    private readonly string? _projectName;
    private readonly string? _configuration;
    private readonly DateTime _startTime;
    private Dictionary<string, TimeSpan>? _validatorTimes;

    public CompilationMetrics(string? fileName = null, string? projectName = null, string? configuration = null)
    {
        _fileName = fileName;
        _projectName = projectName;
        _configuration = configuration;
        _startTime = DateTime.UtcNow;
    }

    // ===== Per-Phase Timing Properties =====
    // These provide direct access to specific compilation phase durations.

    /// <summary>
    /// Gets the duration of the lexical analysis phase (tokenization).
    /// Returns <see cref="TimeSpan.Zero"/> if the phase was not recorded.
    /// </summary>
    public TimeSpan LexerTime => GetPhaseDuration("Lexical Analysis");

    /// <summary>
    /// Gets the duration of the syntax analysis phase (parsing).
    /// Returns <see cref="TimeSpan.Zero"/> if the phase was not recorded.
    /// </summary>
    public TimeSpan ParserTime => GetPhaseDuration("Syntax Analysis");

    /// <summary>
    /// Gets the duration of the name resolution phase.
    /// Returns <see cref="TimeSpan.Zero"/> if the phase was not recorded.
    /// </summary>
    public TimeSpan NameResolutionTime => GetPhaseDuration("Name Resolution");

    /// <summary>
    /// Gets the duration of the import resolution phase.
    /// Returns <see cref="TimeSpan.Zero"/> if the phase was not recorded.
    /// </summary>
    public TimeSpan ImportResolutionTime => GetPhaseDuration("Import Resolution");

    /// <summary>
    /// Gets the duration of the type resolution phase.
    /// Returns <see cref="TimeSpan.Zero"/> if the phase was not recorded.
    /// </summary>
    public TimeSpan TypeResolutionTime => GetPhaseDuration("Type Resolution");

    /// <summary>
    /// Gets the duration of the type checking phase (includes validation).
    /// Returns <see cref="TimeSpan.Zero"/> if the phase was not recorded.
    /// </summary>
    public TimeSpan TypeCheckingTime => GetPhaseDuration("Type Checking");

    /// <summary>
    /// Gets the duration of the code generation phase.
    /// Returns <see cref="TimeSpan.Zero"/> if the phase was not recorded.
    /// </summary>
    public TimeSpan CodeGenTime => GetPhaseDuration("Code Generation");

    /// <summary>
    /// Gets the aggregate duration of all validation during the type checking phase.
    /// This is the sum of all individual validator times in <see cref="ValidatorTimes"/>.
    /// Returns <see cref="TimeSpan.Zero"/> if no validators were recorded.
    /// </summary>
    /// <remarks>
    /// Validation runs within the type checking phase, so this time is a subset of
    /// <see cref="TypeCheckingTime"/>. Use this property to understand how much of
    /// type checking was spent on validation vs. type inference.
    /// </remarks>
    public TimeSpan ValidationTime =>
        _validatorTimes != null
            ? TimeSpan.FromTicks(_validatorTimes.Values.Sum(ts => ts.Ticks))
            : TimeSpan.Zero;

    /// <summary>
    /// Gets the duration of a specific phase by name.
    /// </summary>
    /// <param name="phaseName">The name of the phase to look up.</param>
    /// <returns>The duration of the phase, or <see cref="TimeSpan.Zero"/> if not found.</returns>
    public TimeSpan GetPhaseDuration(string phaseName)
    {
        var phase = _phases.FirstOrDefault(p => p.Name == phaseName);
        return phase?.Duration ?? TimeSpan.Zero;
    }

    // ===== Validator Timing =====

    /// <summary>
    /// Gets per-validator timing data from the validation phase.
    /// Each key is a validator name (e.g., "ControlFlowValidator"),
    /// and each value is the time spent in that validator.
    /// </summary>
    public IReadOnlyDictionary<string, TimeSpan> ValidatorTimes =>
        _validatorTimes ?? (IReadOnlyDictionary<string, TimeSpan>)new Dictionary<string, TimeSpan>();

    /// <summary>
    /// Sets the per-validator timing data.
    /// </summary>
    /// <param name="validatorTimes">Dictionary of validator names to durations.</param>
    public void SetValidatorTimes(Dictionary<string, TimeSpan> validatorTimes)
    {
        _validatorTimes = validatorTimes;
    }

    // ===== Artifact Counts =====

    /// <summary>
    /// Gets or sets the number of tokens produced by the lexer.
    /// </summary>
    public int TokenCount { get; set; }

    /// <summary>
    /// Gets or sets the number of AST nodes produced by the parser.
    /// </summary>
    public int AstNodeCount { get; set; }

    /// <summary>
    /// Gets or sets the number of symbols in the symbol table after semantic analysis.
    /// </summary>
    public int SymbolCount { get; set; }

    /// <summary>
    /// Gets or sets the total number of diagnostics (errors + warnings) produced.
    /// </summary>
    public int DiagnosticCount { get; set; }

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
    /// Get total duration across all phases.
    /// </summary>
    public TimeSpan TotalDuration
    {
        get
        {
            var totalMs = _phases.Sum(p => p.Duration.TotalMilliseconds);
            return totalMs > 0 ? TimeSpan.FromMilliseconds(totalMs) : TimeSpan.Zero;
        }
    }

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

        // Validator timing breakdown (if available)
        if (_validatorTimes != null && _validatorTimes.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Validator Breakdown:");
            sb.AppendLine($"{"Validator",-40} {"Duration",-15}");
            sb.AppendLine(new string('-', 55));

            foreach (var (validator, duration) in _validatorTimes.OrderByDescending(kvp => kvp.Value))
            {
                var durationStr = $"{duration.TotalMilliseconds:F2} ms";
                sb.AppendLine($"{validator,-40} {durationStr,-15}");
            }
        }

        // Artifact counts (if any are set)
        if (TokenCount > 0 || AstNodeCount > 0 || SymbolCount > 0 || DiagnosticCount > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Artifact Counts:");
            if (TokenCount > 0)
                sb.AppendLine($"  Tokens: {TokenCount:N0}");
            if (AstNodeCount > 0)
                sb.AppendLine($"  AST Nodes: {AstNodeCount:N0}");
            if (SymbolCount > 0)
                sb.AppendLine($"  Symbols: {SymbolCount:N0}");
            if (DiagnosticCount > 0)
                sb.AppendLine($"  Diagnostics: {DiagnosticCount:N0}");
        }

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

            // Per-phase timing shortcuts
            lexer_time_ms = LexerTime.TotalMilliseconds,
            parser_time_ms = ParserTime.TotalMilliseconds,
            name_resolution_time_ms = NameResolutionTime.TotalMilliseconds,
            import_resolution_time_ms = ImportResolutionTime.TotalMilliseconds,
            type_resolution_time_ms = TypeResolutionTime.TotalMilliseconds,
            type_checking_time_ms = TypeCheckingTime.TotalMilliseconds,
            validation_time_ms = ValidationTime.TotalMilliseconds,
            codegen_time_ms = CodeGenTime.TotalMilliseconds,

            // Artifact counts
            token_count = TokenCount,
            ast_node_count = AstNodeCount,
            symbol_count = SymbolCount,
            diagnostic_count = DiagnosticCount,

            // Validator times (if available)
            validator_times = _validatorTimes?.Select(kvp => new
            {
                validator = kvp.Key,
                duration_ms = kvp.Value.TotalMilliseconds
            }).ToList(),

            // Detailed phase breakdown
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
    /// Add metrics for a compiled file.
    /// </summary>
    /// <param name="metrics">The metrics to add. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="metrics"/> is null.</exception>
    public void AddFileMetrics(CompilationMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);
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
