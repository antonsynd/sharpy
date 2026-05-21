extern alias SharpyRT;
using Sharpy.Compiler;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Text;

namespace Sharpy.Cli;

internal static class CliHelpers
{
    internal static readonly DiagnosticRenderer Renderer = new(DiagnosticRenderer.IsColorSupported());
    internal static readonly bool UseColor = DiagnosticRenderer.IsColorSupported();

    internal static CompilerApi CreateCompilerApi(ICompilerLogger logger)
    {
        return new CompilerApi(logger, GetDefaultReferences());
    }

    internal static string[] GetDefaultReferences()
    {
        var corePath = typeof(SharpyRT::Sharpy.Builtins).Assembly.Location;
        var coreDir = Path.GetDirectoryName(corePath)!;
        var stdlibPath = Path.Combine(coreDir, "Sharpy.Stdlib.dll");
        var refs = new List<string> { corePath };
        if (File.Exists(stdlibPath))
            refs.Add(stdlibPath);
        return refs.ToArray();
    }

    internal static readonly CompilerPhase[] PhaseOrder = new[]
    {
        CompilerPhase.Lexer,
        CompilerPhase.Parser,
        CompilerPhase.NameResolution,
        CompilerPhase.ImportResolution,
        CompilerPhase.TypeChecking,
        CompilerPhase.Validation,
        CompilerPhase.CodeGeneration,
        CompilerPhase.Assembly,
        CompilerPhase.Unknown
    };

    internal static ICompilerLogger CreateLogger(CompilerLogLevel logLevel, FileInfo? logFile)
    {
        if (logLevel == CompilerLogLevel.None)
        {
            return NullLogger.Instance;
        }
        else if (logFile != null)
        {
            var stream = new StreamWriter(logFile.FullName, append: false);
            return new ConsoleCompilerLogger(logLevel, stream, stream);
        }
        else
        {
            return new ConsoleCompilerLogger(logLevel);
        }
    }

    internal static void OutputVerboseTimingSummary(CompilationMetrics? metrics, ICompilerLogger logger)
    {
        if (metrics == null || !logger.IsEnabled(CompilerLogLevel.Info))
            return;

        Console.Error.WriteLine();
        Console.Error.WriteLine("--- Compilation Timing ---");

        foreach (var phase in metrics.Phases)
        {
            Console.Error.WriteLine($"  {phase.Name,-30} {phase.Duration.TotalMilliseconds,8:F2} ms");
        }

        Console.Error.WriteLine($"  {"TOTAL",-30} {metrics.TotalDuration.TotalMilliseconds,8:F2} ms");

        if (metrics.TokenCount > 0 || metrics.AstNodeCount > 0 || metrics.SymbolCount > 0)
        {
            Console.Error.WriteLine();
            if (metrics.TokenCount > 0)
                Console.Error.WriteLine($"  Tokens: {metrics.TokenCount:N0}");
            if (metrics.AstNodeCount > 0)
                Console.Error.WriteLine($"  AST Nodes: {metrics.AstNodeCount:N0}");
            if (metrics.SymbolCount > 0)
                Console.Error.WriteLine($"  Symbols: {metrics.SymbolCount:N0}");
        }

        if (logger.IsEnabled(CompilerLogLevel.Debug) && metrics.ValidatorTimes.Count > 0)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine("  Validator Breakdown:");

            foreach (var (validator, duration) in metrics.ValidatorTimes.OrderByDescending(kvp => kvp.Value))
            {
                Console.Error.WriteLine($"    {validator,-38} {duration.TotalMilliseconds,8:F2} ms");
            }
        }

        Console.Error.WriteLine();
    }

    internal static void OutputVerboseTimingSummary(ProjectCompilationMetrics? metrics, ICompilerLogger logger)
    {
        if (metrics == null || !logger.IsEnabled(CompilerLogLevel.Info))
            return;

        Console.Error.WriteLine();
        Console.Error.WriteLine("--- Project Compilation Timing ---");
        Console.Error.WriteLine($"  Files compiled: {metrics.TotalFiles}");

        if (metrics.SkippedFileCount > 0)
        {
            Console.Error.WriteLine($"  Files skipped (incremental): {metrics.SkippedFileCount}");
        }

        var aggregates = metrics.AggregatePhaseMetrics;
        foreach (var (phase, data) in aggregates.OrderBy(kvp => kvp.Key))
        {
            Console.Error.WriteLine($"  {phase,-30} {data.Duration.TotalMilliseconds,8:F2} ms");
        }

        Console.Error.WriteLine($"  {"TOTAL",-30} {metrics.TotalDuration.TotalMilliseconds,8:F2} ms");

        if (logger.IsEnabled(CompilerLogLevel.Debug) && metrics.FileMetrics.Count > 0)
        {
            var aggregatedValidatorTimes = new Dictionary<string, TimeSpan>();
            foreach (var fileMetric in metrics.FileMetrics)
            {
                foreach (var (validator, duration) in fileMetric.ValidatorTimes)
                {
                    if (!aggregatedValidatorTimes.ContainsKey(validator))
                        aggregatedValidatorTimes[validator] = TimeSpan.Zero;
                    aggregatedValidatorTimes[validator] += duration;
                }
            }

            if (aggregatedValidatorTimes.Count > 0)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine("  Validator Breakdown (aggregate):");

                foreach (var (validator, duration) in aggregatedValidatorTimes.OrderByDescending(kvp => kvp.Value))
                {
                    Console.Error.WriteLine($"    {validator,-38} {duration.TotalMilliseconds,8:F2} ms");
                }
            }
        }

        Console.Error.WriteLine();
    }

    internal static void OutputMetrics(CompilationMetrics? metrics, string? metricsFormat, FileInfo? metricsOutput)
    {
        if (metrics == null || metricsFormat == null)
            return;

        var format = metricsFormat.ToLowerInvariant();
        if (format != "text" && format != "json")
        {
            Console.Error.WriteLine($"Invalid metrics format: {metricsFormat}. Use 'text' or 'json'.");
            return;
        }

        var output = format == "json" ? metrics.FormatAsJson() : metrics.FormatAsText();

        if (metricsOutput != null)
        {
            try
            {
                File.WriteAllText(metricsOutput.FullName, output);
                Console.WriteLine($"Metrics written to: {metricsOutput.FullName}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to write metrics to file: {ex.Message}");
                Console.WriteLine(output);
            }
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine(output);
        }
    }

    internal static void OutputProjectMetrics(ProjectCompilationMetrics? metrics, string? metricsFormat, FileInfo? metricsOutput)
    {
        if (metrics == null || metricsFormat == null)
            return;

        var format = metricsFormat.ToLowerInvariant();
        if (format != "text" && format != "json")
        {
            Console.Error.WriteLine($"Invalid metrics format: {metricsFormat}. Use 'text' or 'json'.");
            return;
        }

        var output = format == "json" ? metrics.FormatAsJson() : metrics.FormatAsText();

        if (metricsOutput != null)
        {
            try
            {
                File.WriteAllText(metricsOutput.FullName, output);
                Console.WriteLine($"Metrics written to: {metricsOutput.FullName}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to write metrics to file: {ex.Message}");
                Console.WriteLine(output);
            }
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine(output);
        }
    }

    internal static void ValidateInputFile(FileInfo inputFile)
    {
        if (!inputFile.Exists)
        {
            Console.Error.WriteLine($"Error: Input file '{inputFile.FullName}' does not exist.");
            Environment.Exit(1);
        }

        if (inputFile.Extension != ".spy")
        {
            Console.Error.WriteLine($"Warning: Input file '{inputFile.Name}' does not have .spy extension.");
        }
    }

    internal static void RenderDiagnostic(CompilerDiagnostic diagnostic, SourceText? sourceText, TextWriter writer)
    {
        writer.WriteLine(Renderer.Render(diagnostic, sourceText));
    }

    internal static void RenderDiagnostics(IEnumerable<CompilerDiagnostic> diagnostics, SourceText? sourceText, TextWriter writer)
    {
        var diagList = diagnostics.ToList();
        var phases = diagList.Select(d => d.Phase).Distinct().ToList();
        var groupByPhase = phases.Count > 1;
        var isWarnings = diagList.Count > 0 && diagList.All(d => d.IsWarning);

        if (groupByPhase)
        {
            foreach (var phase in PhaseOrder.Where(p => diagList.Any(d => d.Phase == p)))
            {
                writer.WriteLine($"{PhaseLabel(phase, isWarnings)}:");
                foreach (var diagnostic in diagList.Where(d => d.Phase == phase))
                {
                    RenderDiagnostic(diagnostic, sourceText, writer);
                    writer.WriteLine();
                }
            }
        }
        else
        {
            foreach (var diagnostic in diagList)
            {
                RenderDiagnostic(diagnostic, sourceText, writer);
                writer.WriteLine();
            }
        }
    }

    internal static void RenderDiagnosticsFromFiles(IEnumerable<CompilerDiagnostic> diagnostics, TextWriter writer)
    {
        var sourceCache = new Dictionary<string, SourceText?>();
        var diagList = diagnostics.ToList();
        var phases = diagList.Select(d => d.Phase).Distinct().ToList();
        var groupByPhase = phases.Count > 1;
        var isWarnings = diagList.Count > 0 && diagList.All(d => d.IsWarning);

        if (groupByPhase)
        {
            foreach (var phase in PhaseOrder.Where(p => diagList.Any(d => d.Phase == p)))
            {
                writer.WriteLine($"{PhaseLabel(phase, isWarnings)}:");
                foreach (var diagnostic in diagList.Where(d => d.Phase == phase))
                {
                    RenderDiagnosticFromFile(diagnostic, sourceCache, writer);
                }
            }
        }
        else
        {
            foreach (var diagnostic in diagList)
            {
                RenderDiagnosticFromFile(diagnostic, sourceCache, writer);
            }
        }
    }

    internal static void RenderDiagnosticFromFile(CompilerDiagnostic diagnostic, Dictionary<string, SourceText?> sourceCache, TextWriter writer)
    {
        SourceText? sourceText = null;

        if (!string.IsNullOrEmpty(diagnostic.FilePath))
        {
            if (!sourceCache.TryGetValue(diagnostic.FilePath, out sourceText))
            {
                try
                {
                    if (File.Exists(diagnostic.FilePath))
                    {
                        var content = File.ReadAllText(diagnostic.FilePath);
                        sourceText = new SourceText(content, diagnostic.FilePath);
                    }
                }
                catch
                {
                }
                sourceCache[diagnostic.FilePath] = sourceText;
            }
        }

        RenderDiagnostic(diagnostic, sourceText, writer);
        writer.WriteLine();
    }

    internal static string PhaseLabel(CompilerPhase phase, bool isWarnings = false)
    {
        var suffix = isWarnings ? "warnings" : "errors";
        return phase switch
        {
            CompilerPhase.Lexer => $"Lexer {suffix}",
            CompilerPhase.Parser => $"Parse {suffix}",
            CompilerPhase.NameResolution => $"Name resolution {suffix}",
            CompilerPhase.ImportResolution => $"Import resolution {suffix}",
            CompilerPhase.TypeChecking => $"Type {suffix}",
            CompilerPhase.Validation => $"Validation {suffix}",
            CompilerPhase.CodeGeneration => $"Code generation {suffix}",
            CompilerPhase.Assembly => $"Assembly {suffix}",
            CompilerPhase.Unknown => $"Other {suffix}",
            _ => $"Other {suffix}",
        };
    }

    internal static HashSet<string> ParseNowarnCodes(string? nowarn)
    {
        if (string.IsNullOrWhiteSpace(nowarn))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return new HashSet<string>(
            nowarn.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.OrdinalIgnoreCase);
    }

    internal static string StripLineDirectives(string csharpCode)
    {
        var lines = csharpCode.Split('\n');
        var filtered = lines.Where(line => !line.TrimStart().StartsWith("#line "));
        return string.Join('\n', filtered);
    }

    internal static string CliBold(string text) => UseColor ? $"\x1b[1m{text}\x1b[0m" : text;

    internal static string CliColor(string text, string code, bool bold = false)
    {
        if (!UseColor)
            return text;
        var boldCode = bold ? "1;" : "";
        return $"\x1b[{boldCode}{code}m{text}\x1b[0m";
    }

    internal static string CategoryColor(string category) => category switch
    {
        "Lexer" => "33",
        "Parser" => "33",
        "Semantic" => "31",
        "Validation" => "34",
        "CodeGen" => "32",
        "Infrastructure" => "36",
        _ => "37"
    };

    internal static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }
}
