extern alias SharpyRT;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Sharpy.Compiler;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Shared;
using Sharpy.Compiler.Text;

namespace Sharpy.Cli;

internal static class CliHelpers
{
    internal static readonly DiagnosticRenderer Renderer = new(DiagnosticRenderer.IsColorSupported());
    internal static readonly bool UseColor = DiagnosticRenderer.IsColorSupported();

    /// <summary>Process exit code for a successful compile.</summary>
    internal const int ExitSuccess = 0;

    /// <summary>Process exit code for a normal (user-caused) compilation failure.</summary>
    internal const int ExitCompileError = 1;

    /// <summary>
    /// Process exit code when the compiler emitted C# that Roslyn rejected (SPY0908). Distinct
    /// from a normal error so CI/tooling can single out "generated C# failed to compile" cases.
    /// </summary>
    internal const int ExitGeneratedCSharpError = 2;

    /// <summary>
    /// Process exit code when an internal compiler error escaped a phase (SPY0909). Distinct so a
    /// crash (compiler bug) is never confused with a user error.
    /// </summary>
    internal const int ExitInternalError = 3;

    /// <summary>
    /// Maps a failed compilation's diagnostics to a process exit code, promoting internal-error
    /// codes (SPY0909, then SPY0908) above a plain compile error. ICE outranks escaped-C# because a
    /// true crash is the most severe outcome. Returns <see cref="ExitCompileError"/> for ordinary errors.
    /// </summary>
    internal static int MapFailureExitCode(IEnumerable<CompilerDiagnostic> diagnostics)
    {
        var hasIce = false;
        var hasGeneratedCSharp = false;
        foreach (var d in diagnostics)
        {
            if (!d.IsError)
                continue;
            if (d.Code == DiagnosticCodes.Infrastructure.InternalCompilerError)
                hasIce = true;
            else if (d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError)
                hasGeneratedCSharp = true;
        }

        if (hasIce)
            return ExitInternalError;
        if (hasGeneratedCSharp)
            return ExitGeneratedCSharpError;
        return ExitCompileError;
    }

    /// <summary>
    /// When set (from <c>--verbose</c>/<c>-v</c>), rendered diagnostics carry a provenance
    /// suffix identifying the producing phase/validator (e.g. <c>[type-check]</c>,
    /// <c>[validation:ProtocolValidator]</c>). Command handlers set this once from the parse
    /// result before any rendering; it defaults off so normal output is unchanged.
    /// </summary>
    internal static bool ShowDiagnosticProvenance;

    /// <summary>
    /// Exit code produced by the most recent failed <see cref="Commands.BuildCommand.CompileToBinary"/>
    /// call, mapped from its diagnostics (see <see cref="MapFailureExitCode"/>). Callers that treat a
    /// null <c>CompileResult</c> as failure read this to propagate the ICE (3) / escaped-C# (2) /
    /// ordinary-error (1) distinction. Defaults to <see cref="ExitCompileError"/>.
    /// </summary>
    internal static int LastFailureExitCode = ExitCompileError;

    internal static CompilerApi CreateCompilerApi(ICompilerLogger logger)
    {
        return new CompilerApi(logger, GetDefaultReferences());
    }

    /// <summary>
    /// The CLI's default references — Sharpy.Core plus the stdlib — from the one authority the
    /// in-process project test harness also reads (<see cref="DefaultReferences"/>, #2140).
    /// </summary>
    internal static string[] GetDefaultReferences()
        => DefaultReferences.Resolve(message => Console.Error.WriteLine(message));

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
            // Nothing disposes the logger, so a buffered writer lost every line at exit and
            // --log-file always left an empty file (#2173).
            var stream = new StreamWriter(logFile.FullName, append: false) { AutoFlush = true };
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
        WriteMetricsOutput(output, metricsOutput);
    }

    /// <summary>
    /// Emit combined front-end and assembly metrics for a single-file compile as one
    /// document. In JSON mode the result is <c>{ "frontend": {...}, "assembly": {...} }</c>,
    /// so that the front-end phases (module discovery, lexing, parsing, type checking,
    /// code generation) and the assembly phases (C# parsing, Roslyn compilation, IL
    /// emission) are reported together rather than the front-end metrics being discarded.
    /// In text mode the two <see cref="CompilationMetrics.FormatAsText"/> blocks are
    /// emitted under labeled sections.
    /// </summary>
    internal static void OutputCombinedMetrics(
        CompilationMetrics? frontend,
        CompilationMetrics? assembly,
        string? metricsFormat,
        FileInfo? metricsOutput)
    {
        if (metricsFormat == null || (frontend == null && assembly == null))
            return;

        var format = metricsFormat.ToLowerInvariant();
        if (format != "text" && format != "json")
        {
            Console.Error.WriteLine($"Invalid metrics format: {metricsFormat}. Use 'text' or 'json'.");
            return;
        }

        string output;
        if (format == "json")
        {
            var combined = new JsonObject
            {
                ["frontend"] = frontend != null ? JsonNode.Parse(frontend.FormatAsJson()) : null,
                ["assembly"] = assembly != null ? JsonNode.Parse(assembly.FormatAsJson()) : null
            };
            output = combined.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }
        else
        {
            var sections = new List<string>();
            if (frontend != null)
            {
                sections.Add("=== Front-End Compilation ===");
                sections.Add(frontend.FormatAsText());
            }
            if (assembly != null)
            {
                sections.Add("=== Assembly Compilation ===");
                sections.Add(assembly.FormatAsText());
            }
            output = string.Join(Environment.NewLine, sections);
        }

        WriteMetricsOutput(output, metricsOutput);
    }

    private static void WriteMetricsOutput(string output, FileInfo? metricsOutput)
    {
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
        WriteMetricsOutput(output, metricsOutput);
    }

    /// <summary>
    /// Validates that the input file exists and has a .spy extension. Returns
    /// <c>false</c> (after writing an error) if the file does not exist; a missing
    /// .spy extension only produces a warning and still returns <c>true</c>.
    /// </summary>
    internal static bool ValidateInputFile(FileInfo inputFile)
    {
        if (!inputFile.Exists)
        {
            Console.Error.WriteLine($"Error: Input file '{inputFile.FullName}' does not exist.");
            return false;
        }

        if (inputFile.Extension != ".spy")
        {
            Console.Error.WriteLine($"Warning: Input file '{inputFile.Name}' does not have .spy extension.");
        }

        return true;
    }

    /// <summary>
    /// A <c>--module-path</c> naming a directory that does not exist is an error, like a missing
    /// input file — it used to be accepted silently (#2173). Relative paths are checked against the
    /// working directory, the directory the compiler resolves them from. Prints one error per
    /// missing path and returns false when any is missing.
    /// </summary>
    internal static bool ValidateModulePaths(IEnumerable<string> modulePaths)
    {
        var valid = true;
        foreach (var modulePath in modulePaths)
        {
            if (!Directory.Exists(modulePath))
            {
                Console.Error.WriteLine($"Error: Module path '{Path.GetFullPath(modulePath)}' does not exist.");
                valid = false;
            }
        }

        return valid;
    }

    /// <summary>
    /// Spells each <c>--reference</c> as the full path the module registry resolves it to (as given,
    /// against the working directory, or as a name on a module path). The registry and the Roslyn
    /// assembly step must see the same file: handed a bare name the registry found on a module path,
    /// Roslyn (which takes the reference verbatim) could not, and the compile failed with SPY0908
    /// CS0400. A reference that resolves nowhere is kept as given, so the registry reports SPY0305.
    /// </summary>
    internal static string[] ResolveReferences(IEnumerable<string> references, IEnumerable<string> modulePaths)
    {
        var searchPaths = modulePaths.ToArray();
        return references
            .Select(reference => ModuleRegistry.ResolveAssemblyPath(reference, searchPaths) ?? reference)
            .ToArray();
    }

    /// <summary>
    /// <c>--project-reference</c> is parsed by <c>build</c>, <c>run</c> and <c>compile</c> but no
    /// mode builds a .NET project, so it was silently dropped (#2173). Refuse it instead. Prints the
    /// error and returns false when any project reference was given.
    /// </summary>
    internal static bool RefuseProjectReferences(IReadOnlyCollection<string> projectReferences)
    {
        if (projectReferences.Count == 0)
        {
            return true;
        }

        Console.Error.WriteLine($"Error: --project-reference is not supported: {ProjectReferenceUnsupportedReason}.");
        return false;
    }

    /// <summary>Why <c>--project-reference</c> is refused; shared by the refusal and <c>compile</c>'s option table.</summary>
    internal const string ProjectReferenceUnsupportedReason =
        "sharpyc does not build .NET projects; build it with `dotnet build` and pass the assembly with --reference";

    /// <summary>
    /// Splits a raw argument vector at the first bare <c>--</c>: everything before it is sharpyc's
    /// own command line, everything after it is the argument vector of the program being run
    /// (<c>sharpyc run prog.spy -- a b c</c>, #1215). The separator itself is dropped; a second
    /// <c>--</c> is not a separator and reaches the program verbatim, matching <c>dotnet run --</c>
    /// and <c>cargo run --</c>.
    /// <para>
    /// The split happens before parsing so tokens after <c>--</c> can never be read as sharpyc
    /// options, and so the parser stays strict about everything before it — an unknown option is
    /// still a parse error rather than something quietly forwarded to the program.
    /// </para>
    /// </summary>
    internal static (string[] CompilerArguments, string[] ProgramArguments) SplitAtDoubleDash(
        IReadOnlyList<string> arguments)
    {
        for (var i = 0; i < arguments.Count; i++)
        {
            if (arguments[i] != "--")
            {
                continue;
            }

            var compilerArguments = new string[i];
            for (var j = 0; j < i; j++)
            {
                compilerArguments[j] = arguments[j];
            }

            var programArguments = new string[arguments.Count - i - 1];
            for (var j = i + 1; j < arguments.Count; j++)
            {
                programArguments[j - i - 1] = arguments[j];
            }

            return (compilerArguments, programArguments);
        }

        return (arguments.ToArray(), Array.Empty<string>());
    }

    /// <summary>
    /// A program argument vector only means something to <c>sharpyc run</c>, which is the only
    /// command that executes anything. Tokens after <c>--</c> on any other command used to be a
    /// parse error ("Unrecognized command or argument"), so rejecting them here keeps that contract
    /// instead of silently discarding them (#1215). Returns <c>false</c> after writing the message.
    /// </summary>
    internal static bool ValidateProgramArgumentPlacement(
        System.CommandLine.ParseResult parseResult,
        System.CommandLine.Command runCommand,
        IReadOnlyList<string> programArguments)
    {
        if (programArguments.Count == 0 || ReferenceEquals(parseResult.CommandResult.Command, runCommand))
        {
            return true;
        }

        // A misspelled command resolves to whatever the parser could match, so complaining about
        // '--' here would bury the real error. Let the parse errors be reported first.
        if (parseResult.Errors.Count > 0)
        {
            return true;
        }

        Console.Error.WriteLine(
            $"Error: '{parseResult.CommandResult.Command.Name}' does not execute anything, so it "
            + "cannot accept the arguments after '--'. Only 'sharpyc run' can.");
        return false;
    }

    internal static void RenderDiagnostic(CompilerDiagnostic diagnostic, SourceText? sourceText, TextWriter writer)
    {
        writer.WriteLine(Renderer.Render(diagnostic, sourceText, ShowDiagnosticProvenance));
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

    /// <param name="fallbackFilePath">
    /// The entry/compiling file, used ONLY to name a diagnostic that carries no file of its own
    /// (#1494). Never used to resolve a position — see <see cref="RenderDiagnosticFromFile"/>.
    /// </param>
    internal static void RenderDiagnosticsFromFiles(IEnumerable<CompilerDiagnostic> diagnostics, TextWriter writer,
        string? fallbackFilePath = null)
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
                    RenderDiagnosticFromFile(diagnostic, sourceCache, writer, fallbackFilePath);
                }
            }
        }
        else
        {
            foreach (var diagnostic in diagList)
            {
                RenderDiagnosticFromFile(diagnostic, sourceCache, writer, fallbackFilePath);
            }
        }
    }

    /// <summary>
    /// Renders one diagnostic against its OWN file, loading and caching that file's text so the
    /// location and snippet come from the file the diagnostic names.
    /// </summary>
    /// <param name="fallbackFilePath">
    /// The entry/compiling file, used ONLY when the diagnostic names no file of its own (#1494).
    /// </param>
    /// <remarks>
    /// A position-less, path-less diagnostic — an unmappable SPY0908 is the shape that motivated
    /// this — used to render with no file name at all: <c>sourceText</c> stayed null, so the
    /// renderer's own <c>sourceText?.FilePath</c> fallback could never fire and the user was told
    /// something had gone wrong without being told in which program. The fallback names the entry
    /// file and stops there: it deliberately does NOT construct a <see cref="SourceText"/> from it,
    /// because a buffer would let the renderer derive a line/column and draw a snippet from a file
    /// the diagnostic was never about. That is the #1437 mistake in miniature. Losing context is
    /// acceptable; asserting a position that does not exist is not.
    /// </remarks>
    internal static void RenderDiagnosticFromFile(CompilerDiagnostic diagnostic,
        Dictionary<string, SourceText?> sourceCache, TextWriter writer, string? fallbackFilePath = null)
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
        else if (!string.IsNullOrEmpty(fallbackFilePath) && !diagnostic.Line.HasValue)
        {
            // Name the program, never invent a position. The diagnostic is given the entry file's
            // path and no buffer, so the renderer takes its path-only arm: `--> <entry.spy>` with
            // no line, no column, no snippet.
            //
            // Gated on having no position of its own. A diagnostic that knows a line but not a file
            // is a DIFFERENT defect: its line belongs to some file we cannot name, and pinning it to
            // the entry file would assert a location that may not hold there — the #1437 mistake in
            // miniature. That shape keeps rendering as `<source>:line:col`, which is at least honest
            // about not knowing. Span is dropped with the rest so no downstream re-derivation can
            // resurrect a position from it.
            diagnostic = diagnostic with { FilePath = fallbackFilePath, Column = null, Span = null };
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

    internal static FeatureFlags ParseFeatures(string[]? features) => FeatureFlags.None.Enable(features ?? Array.Empty<string>());

    private static readonly Regex ValidNamespaceRegex = new(
        @"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*$",
        RegexOptions.Compiled);

    /// <summary>
    /// Validates a <c>--namespace</c> value (a dotted C# identifier), writing the rejection message
    /// to stderr when it fails. A null value means "not supplied" and is accepted. Shared by every
    /// command that takes the option so they cannot disagree on what a namespace may look like.
    /// </summary>
    internal static bool ValidateNamespaceOption(string? namespaceName)
    {
        if (namespaceName == null || ValidNamespaceRegex.IsMatch(namespaceName))
            return true;

        Console.Error.WriteLine(
            $"Invalid namespace '{namespaceName}': must be a valid dotted identifier (e.g., 'Game.Scripts')");
        return false;
    }

    /// <summary>
    /// Maps one generated-C# unit to its output path under <paramref name="outputDir"/>, mirroring
    /// the unit's key, which is its source path relative to the project directory (the module
    /// root that also fixes the module's dotted name — <c>ProjectCompiler.GenerateCode</c>). So
    /// <c>src/lib.spy</c> and <c>src/pkg/lib.spy</c> go to <c>src/lib.cs</c> and
    /// <c>src/pkg/lib.cs</c>. Writing by file name alone sent both to <c>lib.cs</c>, and the second
    /// silently overwrote the first (#2060).
    /// A source outside the project directory (<c>SourceFile Include="../ext/*.spy"</c>) has a key
    /// that climbs out (<c>../ext/outer.cs</c>); its leading <c>..</c> segments are dropped, the same
    /// way <c>CompilationUnitFactory.ComputeModulePath</c> names that module <c>ext.outer</c>, so it
    /// lands at <c>ext/outer.cs</c> inside the output directory, never outside it. A rooted key
    /// (a source on another drive) keeps its path below the root.
    /// </summary>
    internal static string MirroredCSharpOutputPath(string outputDir, string unitKey)
    {
        var relative = Path.ChangeExtension(unitKey, ".cs");
        var root = Path.GetPathRoot(relative);
        if (!string.IsNullOrEmpty(root))
            relative = relative.Substring(root.Length);

        var segments = relative.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);
        var start = 0;
        while (start < segments.Length - 1 && segments[start] is ".." or ".")
            start++;

        var outputRoot = Path.GetFullPath(outputDir);
        var outputPath = Path.GetFullPath(Path.Combine(outputRoot, Path.Combine(segments[start..])));
        var check = Path.GetRelativePath(outputRoot, outputPath);
        if (Path.IsPathRooted(check) || check == ".." || check.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Generated C# key '{unitKey}' maps outside the output directory '{outputRoot}'.");

        return outputPath;
    }

    /// <summary>
    /// The one writer of generated-C# units for every command that saves them (<c>project
    /// --emit-cs-to</c>, <c>compile --emit-csharp</c>, <c>emit csharp</c>; #2060, #2159). Each unit
    /// goes to its <see cref="MirroredCSharpOutputPath"/> under <paramref name="outputDir"/>, so
    /// two units sharing a file stem in different directories land at distinct paths. A second
    /// unit that maps to an already-written path is refused with a stderr warning, never written
    /// over the first.
    /// </summary>
    /// <param name="units">Unit key → generated C#. A project compile keys units by their
    /// project-relative path; a single-file compile keys them by absolute source path, which is
    /// made relative to <paramref name="sourceRoot"/> (the entry file's directory, the module root
    /// that names the imported modules) before mirroring.</param>
    /// <param name="sourceRoot">Root that rooted unit keys are made relative to; null leaves keys as given.</param>
    /// <param name="transformCode">Applied to each unit's C# before writing (e.g. <see cref="StripLineDirectives"/>).</param>
    /// <param name="alreadyWritten">Output paths the caller has written itself (path → the unit key
    /// it holds), e.g. the <c>emit csharp --output</c> file; a unit mapping to one is refused.</param>
    /// <returns>The units written, in write order, with the path each landed at.</returns>
    internal static List<(string UnitKey, string OutputPath)> WriteMirroredCSharp(
        string outputDir,
        IEnumerable<KeyValuePair<string, string>> units,
        string? sourceRoot = null,
        Func<string, string>? transformCode = null,
        IEnumerable<KeyValuePair<string, string>>? alreadyWritten = null)
    {
        var written = new Dictionary<string, string>(
            OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
        if (alreadyWritten != null)
        {
            foreach (var (path, key) in alreadyWritten)
                written[Path.GetFullPath(path)] = key;
        }

        var result = new List<(string UnitKey, string OutputPath)>();
        var keyed = units
            .Select(kv => (Key: sourceRoot != null && Path.IsPathRooted(kv.Key)
                ? Path.GetRelativePath(sourceRoot, kv.Key)
                : kv.Key, Code: kv.Value))
            .OrderBy(u => u.Key, StringComparer.Ordinal);
        foreach (var (unitKey, code) in keyed)
        {
            var outputPath = MirroredCSharpOutputPath(outputDir, unitKey);
            if (written.TryGetValue(outputPath, out var firstKey))
            {
                Console.Error.WriteLine(
                    $"Warning: Not saving generated C# for '{unitKey}': it maps to {outputPath}, already written for '{firstKey}'");
                continue;
            }
            written[outputPath] = unitKey;

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllText(outputPath, transformCode != null ? transformCode(code) : code);
            result.Add((unitKey, outputPath));
        }
        return result;
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
