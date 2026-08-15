extern alias SharpyRT;
using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Diagnostics;

namespace Sharpy.Compiler;

/// <summary>
/// Compiles generated C# code into .NET assemblies
/// </summary>
internal class AssemblyCompiler
{
    private readonly ICompilerLogger _logger;

    // Process-lifetime cache of MetadataReference objects keyed by (assembly path, last-write
    // time UTC). MetadataReference.CreateFromFile reads and parses an assembly's metadata from
    // disk on every call; done over the full trusted-platform-assembly set on every compile it
    // is the dominant cold cost the D2 warm path removes (#1049). A reference for a given
    // (path, mtime) is a pure, immutable function of the file's bytes, so caching it process-wide
    // is determinism-safe: a changed assembly gets a new mtime and thus a new key
    // (content-addressed, append-only). Shared by every compile in the process — the keep-alive
    // server, the LSP, the REPL, and the test host all benefit after the first touch.
    private static readonly ConcurrentDictionary<(string Path, DateTime ModifiedUtc), MetadataReference>
        s_referenceCache = new();

    public AssemblyCompiler(ICompilerLogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>
    /// Return a <see cref="MetadataReference"/> for the assembly at <paramref name="assemblyPath"/>,
    /// reusing the process-lifetime cache (keyed by path + last-write time) so the metadata is read
    /// and parsed from disk at most once per (path, mtime) per process (#1049).
    /// </summary>
    private static MetadataReference GetOrCreateReference(string assemblyPath)
    {
        var mtime = File.GetLastWriteTimeUtc(assemblyPath);
        return s_referenceCache.GetOrAdd(
            (assemblyPath, mtime),
            static key => MetadataReference.CreateFromFile(key.Path));
    }

    /// <summary>
    /// Compile C# source code to a .NET assembly.
    /// </summary>
    /// <param name="csharpSources">Generated C# text keyed by output .cs path.</param>
    /// <param name="prebuiltTrees">
    /// Post-processed Roslyn <see cref="SyntaxTree"/>s the emitter already produced, keyed by
    /// the same paths as <paramref name="csharpSources"/> (D3, #1050). When a source has a
    /// prebuilt tree it is handed to <c>CSharpCompilation.Create</c> directly; only sources
    /// without one (incremental cache hits, restored as text) are reparsed here.
    /// </param>
    /// <param name="projectConfig">The project configuration.</param>
    /// <param name="compilationAlreadyFailed">
    /// True when the compilation's diagnostic bag already carries an error — semantic or codegen —
    /// before this assembly compile runs. Disarms the SPY0908 net for <c>CSxxxx</c> errors (#1387):
    /// see <see cref="MapGeneratedCodeDiagnostics"/>. Suppressed diagnostics are logged at debug
    /// level and returned on
    /// <see cref="AssemblyCompilationResult.SuppressedGeneratedCodeDiagnostics"/>.
    /// </param>
    public AssemblyCompilationResult CompileToAssembly(
        Dictionary<string, string> csharpSources,
        IReadOnlyDictionary<string, SyntaxTree> prebuiltTrees,
        ProjectConfig projectConfig,
        bool compilationAlreadyFailed = false)
    {
        _logger.LogInfo($"Compiling {csharpSources.Count} C# files to assembly");
        var metrics = new CompilationMetrics(
            projectName: projectConfig.RootNamespace,
            configuration: projectConfig.Configuration);

        try
        {
            // Feed emitter-built trees straight to Roslyn; reparse only string-only inputs
            // (incremental cache hits, whose tree was not carried across the cache boundary).
            metrics.StartPhase(CompilerPhaseNames.CSharpParsing);
            var syntaxTrees = new List<SyntaxTree>();
            foreach (var (filePath, sourceCode) in csharpSources)
            {
                if (prebuiltTrees.TryGetValue(filePath, out var prebuilt))
                {
                    syntaxTrees.Add(prebuilt);
                    continue;
                }

                var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode,
                    path: filePath,
                    encoding: System.Text.Encoding.UTF8);
                syntaxTrees.Add(syntaxTree);
            }
            metrics.EndPhase();

            // Gather metadata references
            metrics.StartPhase(CompilerPhaseNames.ReferenceResolution);
            var references = GetMetadataReferences(projectConfig, out var tpaCensus);
            metrics.EndPhase();

            // Post-condition: a reference set with no corlib cannot compile anything, and letting
            // it through produces a wall of CS0518 that the SPY0908 net misattributes to a compiler
            // bug. Stop here and say what actually went wrong (#1482).
            if (ValidateReferenceSet(references, tpaCensus) is { } referenceFailure)
            {
                var referenceDiagnostics = new DiagnosticBag();
                referenceDiagnostics.Add(referenceFailure);
                _logger.LogError(referenceFailure.Message, 0, 0);
                return new AssemblyCompilationResult
                {
                    Success = false,
                    Diagnostics = referenceDiagnostics,
                    Metrics = metrics
                };
            }

            // Determine output kind
            var outputKind = projectConfig.OutputType.ToLowerInvariant() == "exe"
                ? OutputKind.ConsoleApplication
                : OutputKind.DynamicallyLinkedLibrary;

            // Create compilation
            metrics.StartPhase(CompilerPhaseNames.RoslynCompilation);
            var assemblyName = projectConfig.AssemblyName ?? projectConfig.RootNamespace;
            var compilation = CSharpCompilation.Create(
                assemblyName,
                syntaxTrees,
                references,
                new CSharpCompilationOptions(outputKind)
                    .WithOptimizationLevel(projectConfig.Configuration == "Release"
                        ? OptimizationLevel.Release
                        : OptimizationLevel.Debug)
                    .WithPlatform(Platform.AnyCpu));
            metrics.EndPhase();

            // Ensure output directory exists
            var outputPath = projectConfig.OutputAssemblyPath;
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // Emit assembly to file
            metrics.StartPhase(CompilerPhaseNames.IlEmission);
            using var assemblyStream = new FileStream(outputPath, FileMode.Create);

            EmitResult emitResult;
            if (projectConfig.Configuration == "Debug")
            {
                // Emit with PDB for debugging
                var pdbPath = Path.ChangeExtension(outputPath, ".pdb");
                using var pdbStream = new FileStream(pdbPath, FileMode.Create);
                emitResult = compilation.Emit(assemblyStream, pdbStream);
            }
            else
            {
                // Release build without debug symbols
                emitResult = compilation.Emit(assemblyStream);
            }
            metrics.EndPhase();

            var mapping = MapGeneratedCodeDiagnostics(emitResult.Diagnostics, compilationAlreadyFailed);
            var diagnostics = new DiagnosticBag();
            diagnostics.AddRange(mapping.Reported);
            LogSuppressedGeneratedCodeDiagnostics(mapping.Suppressed);

            if (!emitResult.Success)
            {
                return new AssemblyCompilationResult
                {
                    Success = false,
                    Diagnostics = diagnostics,
                    SuppressedGeneratedCodeDiagnostics = mapping.Suppressed,
                    Metrics = metrics
                };
            }

            _logger.LogInfo($"Successfully compiled assembly to: {outputPath}");

            // Generate runtime configuration file
            GenerateRuntimeConfig(outputPath, projectConfig);

            // Generate dependencies file
            GenerateDepsFile(outputPath, projectConfig);

            // Generate test project scaffold for dotnet test integration
            Project.TestProjectScaffold.GenerateIfNeeded(projectConfig, outputPath, _logger);

            return new AssemblyCompilationResult
            {
                Success = true,
                OutputAssemblyPath = outputPath,
                Diagnostics = diagnostics,
                SuppressedGeneratedCodeDiagnostics = mapping.Suppressed,
                Metrics = metrics
            };
        }
        catch (Exception ex)
        {
            // Log full exception including stack trace for debugging
            _logger.LogError($"Assembly compilation failed with {ex.GetType().Name}: {ex}", 0, 0);

            // Create a user-facing error message that includes exception type for identification
            var errorMessage = ex is InternalCompilerErrorException ice
                ? $"Internal compiler error in {ice.Component} ({ex.GetType().Name}): {ex.Message}"
                : $"Assembly compilation failed ({ex.GetType().Name}): {ex.Message}";

            var errorDiagnostics = new DiagnosticBag();
            errorDiagnostics.AddError(errorMessage,
                code: DiagnosticCodes.Infrastructure.AssemblyCompilationFailed,
                phase: CompilerPhase.Assembly);
            return new AssemblyCompilationResult
            {
                Success = false,
                Diagnostics = errorDiagnostics,
                Metrics = metrics
            };
        }
    }

    /// <summary>
    /// Get metadata references for compilation
    /// </summary>
    internal static List<MetadataReference> GetDefaultReferences()
    {
        var references = new List<MetadataReference>();
        var addedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (!string.IsNullOrEmpty(trustedPlatformAssemblies))
        {
            foreach (var assemblyPath in trustedPlatformAssemblies.Split(Path.PathSeparator))
            {
                if (File.Exists(assemblyPath) && addedPaths.Add(assemblyPath))
                {
                    try
                    {
                        references.Add(GetOrCreateReference(assemblyPath));
                    }
                    catch
                    {
                    }
                }
            }
        }
        else
        {
            var coreLibPath = typeof(object).Assembly.Location;
            var coreLibDir = Path.GetDirectoryName(coreLibPath);

            if (!string.IsNullOrEmpty(coreLibDir))
            {
                references.Add(GetOrCreateReference(coreLibPath));
                addedPaths.Add(coreLibPath);
                foreach (var dll in new[] { "System.Runtime.dll", "System.Collections.dll", "System.Linq.dll" })
                {
                    var path = Path.Combine(coreLibDir, dll);
                    if (File.Exists(path) && addedPaths.Add(path))
                        references.Add(GetOrCreateReference(path));
                }
            }
        }

        var sharpyCorePath = typeof(SharpyRT::Sharpy.Builtins).Assembly.Location;
        if (addedPaths.Add(sharpyCorePath))
            references.Add(GetOrCreateReference(sharpyCorePath));

        return references;
    }

    private List<MetadataReference> GetMetadataReferences(ProjectConfig projectConfig)
        => GetMetadataReferences(projectConfig, out _);

    /// <param name="tpaCensus">
    /// What the trusted-platform-assembly walk saw: entry count, skip count, and the first few skip
    /// reasons. Carried out so <see cref="ValidateReferenceSet"/> can report it if the resulting set
    /// turns out to be unusable (#1482).
    /// </param>
    private List<MetadataReference> GetMetadataReferences(ProjectConfig projectConfig, out TpaCensus tpaCensus)
    {
        var references = new List<MetadataReference>();
        var addedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tpaSeen = 0;
        var tpaSkipped = 0;
        var skipReasons = new List<string>();

        // Reference all trusted platform assemblies (full .NET shared framework).
        // This ensures compiled assemblies can use any BCL type (Regex, HttpClient, etc.)
        // without requiring explicit assembly references in the .spyproj.
        var trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        var tpaWasPresent = !string.IsNullOrEmpty(trustedPlatformAssemblies);
        if (tpaWasPresent)
        {
            foreach (var assemblyPath in trustedPlatformAssemblies!.Split(Path.PathSeparator))
            {
                if (string.IsNullOrEmpty(assemblyPath))
                    continue;
                tpaSeen++;

                // File.Exists is false for an unreadable-but-present file as readily as for an
                // absent one — it cannot distinguish "gone" from "unobservable" — so count the
                // skip rather than letting it pass silently (#1482).
                if (!File.Exists(assemblyPath))
                {
                    tpaSkipped++;
                    RecordSkip(skipReasons, assemblyPath, "not found or not readable");
                    continue;
                }

                if (!addedPaths.Add(assemblyPath))
                    continue;

                try
                {
                    references.Add(GetOrCreateReference(assemblyPath));
                }
                catch (Exception ex)
                {
                    // A skipped framework reference is never routine: it silently removes types
                    // the generated C# is entitled to use, and the resulting CSxxxx errors point
                    // at code generation instead of at this (#1482).
                    tpaSkipped++;
                    RecordSkip(skipReasons, assemblyPath, $"{ex.GetType().Name}: {ex.Message}");
                    _logger.LogWarning(
                        $"Skipping framework reference '{assemblyPath}': {ex.GetType().Name}: {ex.Message}", 0, 0);
                }
            }
        }
        else
        {
            // Fallback: manually add essential references
            var coreLibPath = typeof(object).Assembly.Location;
            var coreLibDir = Path.GetDirectoryName(coreLibPath);

            if (!string.IsNullOrEmpty(coreLibDir))
            {
                references.Add(GetOrCreateReference(coreLibPath));
                addedPaths.Add(coreLibPath);
                foreach (var dll in new[] { "System.Runtime.dll", "System.Console.dll", "System.Collections.dll", "System.Linq.dll", "System.Text.RegularExpressions.dll" })
                {
                    var path = Path.Combine(coreLibDir, dll);
                    if (File.Exists(path) && addedPaths.Add(path))
                        references.Add(GetOrCreateReference(path));
                }
            }
        }

        // Add Sharpy.Core reference
        var sharpyCorePath = typeof(SharpyRT::Sharpy.Builtins).Assembly.Location;
        if (addedPaths.Add(sharpyCorePath))
            references.Add(GetOrCreateReference(sharpyCorePath));

        // Add netstandard reference (required because Sharpy.Core targets netstandard2.1/2.0)
        try
        {
            var netstandardAssembly = System.Reflection.Assembly.Load("netstandard");
            if (addedPaths.Add(netstandardAssembly.Location))
                references.Add(GetOrCreateReference(netstandardAssembly.Location));
        }
        catch
        {
            var coreLibDir2 = Path.GetDirectoryName(typeof(object).Assembly.Location);
            if (!string.IsNullOrEmpty(coreLibDir2))
            {
                var netstandardPath = Path.Combine(coreLibDir2, "netstandard.dll");
                if (File.Exists(netstandardPath) && addedPaths.Add(netstandardPath))
                {
                    references.Add(GetOrCreateReference(netstandardPath));
                }
            }
        }

        // Add project-specific references
        foreach (var referencePath in projectConfig.References)
        {
            if (File.Exists(referencePath))
            {
                if (addedPaths.Add(referencePath))
                {
                    references.Add(GetOrCreateReference(referencePath));
                    _logger.LogDebug($"Added reference: {referencePath}");
                }
            }
            else
            {
                _logger.LogWarning($"Reference not found: {referencePath}", 0, 0);
            }
        }

        // Resolve NuGet package references to assembly paths
        foreach (var packageRef in projectConfig.PackageReferences)
        {
            var packageAssemblies = Project.NuGetResolver.ResolvePackage(packageRef, projectConfig.TargetFramework, _logger);
            foreach (var assemblyPath in packageAssemblies)
            {
                if (addedPaths.Add(assemblyPath))
                {
                    references.Add(GetOrCreateReference(assemblyPath));
                    _logger.LogDebug($"Added package reference: {assemblyPath}");
                }
            }
        }

        tpaCensus = new TpaCensus(tpaWasPresent, tpaSeen, tpaSkipped, skipReasons);
        return references;
    }

    /// <summary>How many TPA entries the reference walk saw and how many it could not use.</summary>
    /// <param name="ListWasPresent">
    /// False when <c>TRUSTED_PLATFORM_ASSEMBLIES</c> was null/empty and the manual fallback ran —
    /// a materially different failure from "the list was there and nothing in it survived", which
    /// never reaches the fallback at all.
    /// </param>
    internal readonly record struct TpaCensus(
        bool ListWasPresent, int Seen, int Skipped, IReadOnlyList<string> SkipReasons)
    {
        internal static TpaCensus None => new(false, 0, 0, Array.Empty<string>());
    }

    /// <summary>Records at most a handful of skip reasons — enough to diagnose, not a log dump.</summary>
    private static void RecordSkip(List<string> skipReasons, string assemblyPath, string reason)
    {
        const int maxRecorded = 3;
        if (skipReasons.Count < maxRecorded)
            skipReasons.Add($"{Path.GetFileName(assemblyPath)}: {reason}");
    }

    /// <summary>
    /// Post-condition on the assembled reference set: it must contain an assembly defining
    /// <c>System.Object</c>. Returns null when the set is usable, else the diagnostic to report.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Without this the compiler proceeded into a doomed compilation and the user got a wall of
    /// <c>CS0518: Predefined type 'System.String' is not defined or imported</c> — which the SPY0908
    /// net dutifully relabelled "This is a Sharpy compiler bug — please report it". Measured cost of
    /// one occurrence: 181 test failures and roughly two hours establishing that the compiler was
    /// fine. The trigger was transient and never reproduced; the MISREPORTING is the defect, and it
    /// is what this fixes (#1482).
    /// </para>
    /// <para>
    /// The message deliberately does NOT carry the report-it line. This is an environment fault —
    /// a partially extracted or permission-restricted runtime directory — and telling the user to
    /// file a compiler bug is precisely the misdirection being removed.
    /// </para>
    /// </remarks>
    internal static CompilerDiagnostic? ValidateReferenceSet(
        IReadOnlyList<MetadataReference> references, TpaCensus tpaCensus)
    {
        if (DefinesSystemObject(references))
            return null;

        // "The list was empty" and "the list was full and nothing survived" are different faults
        // with different fixes, and only the first ever reached the manual fallback. Say which.
        var cause = !tpaCensus.ListWasPresent
            ? "the host runtime reported no trusted platform assemblies (TRUSTED_PLATFORM_ASSEMBLIES was empty), "
              + "and the manual fallback did not yield one either"
            : $"the host runtime listed {tpaCensus.Seen} trusted platform assembl{(tpaCensus.Seen == 1 ? "y" : "ies")} "
              + $"but {tpaCensus.Skipped} could not be used and none of the rest defines System.Object";

        var reasons = tpaCensus.SkipReasons.Count > 0
            ? $" First skips: {string.Join("; ", tpaCensus.SkipReasons)}."
            : string.Empty;

        return new CompilerDiagnostic(
            $"Reference acquisition failed: {cause}. Compilation cannot proceed — every predefined type "
            + $"would be undefined. References acquired: {references.Count}.{reasons} "
            + "This is a fault in the .NET runtime installation the compiler is running on, not in your program "
            + "or in the generated code; a partially extracted or permission-restricted shared-framework "
            + "directory is the usual cause.",
            CompilerDiagnosticSeverity.Error,
            Code: DiagnosticCodes.Infrastructure.ReferenceAcquisitionFailed,
            Phase: CompilerPhase.Assembly);
    }

    /// <summary>
    /// True when some reference in the set defines <c>System.Object</c>. Reads the assembly's own
    /// metadata rather than trusting a file name, so a renamed or stub corlib cannot pass.
    /// </summary>
    private static bool DefinesSystemObject(IReadOnlyList<MetadataReference> references)
    {
        if (references.Count == 0)
            return false;

        // Asking Roslyn is both the cheapest and the most faithful test available: this is the very
        // question CS0518 answers negatively, posed before the wall of errors instead of after it.
        var probe = CSharpCompilation.Create("__reference_probe__", Array.Empty<SyntaxTree>(), references);
        return probe.GetSpecialType(SpecialType.System_Object).TypeKind != TypeKind.Error;
    }

    // NuGet resolution moved to Project.NuGetResolver (shared with CompilerApi)

    /// <summary>
    /// Map Roslyn diagnostics produced by compiling <em>generated</em> C# into the
    /// compiler's own <see cref="CompilerDiagnostic"/> shape, applying the SPY0908 net.
    /// This is the single mapping shared by the on-disk assembly path
    /// (<see cref="CompileToAssembly"/>) and the REPL's in-memory emit
    /// (<c>ReplSession.CompileCSharp</c>) so neither front end can ever leak a raw
    /// <c>CSxxxx</c> code from generated C# to the user (#1059).
    /// </summary>
    /// <remarks>
    /// Filtering matches what a user can act on:
    /// <list type="bullet">
    ///   <item>Errors (CS or not) are kept — CS errors are remapped to SPY0908 with the
    ///   original CS id + text preserved in the message; non-CS errors pass through.</item>
    ///   <item>Non-CS warnings (e.g. analyzer warnings) are kept.</item>
    ///   <item>CS warnings are dropped — they come from the compiler's own generated C#
    ///   and are internal noise the user cannot act on.</item>
    ///   <item>Info/hidden diagnostics are dropped.</item>
    /// </list>
    /// <para>
    /// The net disarms itself when <paramref name="compilationAlreadyFailed"/> is true (#1387).
    /// SPY0908 says "this is a Sharpy compiler bug"; that claim is only honest when the compiler
    /// believed the program was good. Once the bag carries an error the user can act on, the
    /// generated C# handed to Roslyn is knowingly incomplete — a refused unit's C# is dropped
    /// (<c>ProjectCompiler.CodeGen.cs</c>) — so Roslyn's complaints about it are consequences of
    /// that refusal, not evidence of a bug. Suppressed CS errors are returned unmapped on
    /// <see cref="GeneratedCodeDiagnosticMapping.Suppressed"/> and logged at debug level, so the
    /// leak corpus the #1146 sweeps rely on keeps every diagnostic it had before.
    /// </para>
    /// </remarks>
    /// <param name="diagnostics">Roslyn's diagnostics from compiling the generated C#.</param>
    /// <param name="compilationAlreadyFailed">
    /// True when an error was already reported for this compilation before Roslyn ran.
    /// Compilation-wide by design: CS5001 ("no entry point"), the shape that motivated the gate,
    /// is a compilation-level Roslyn error carrying <see cref="Location.None"/>, so a per-file
    /// scope could never match it.
    /// </param>
    internal static GeneratedCodeDiagnosticMapping MapGeneratedCodeDiagnostics(
        IEnumerable<Diagnostic> diagnostics,
        bool compilationAlreadyFailed = false)
    {
        var mapped = new List<CompilerDiagnostic>();
        List<CompilerDiagnostic>? suppressed = null;
        foreach (var d in diagnostics)
        {
            if (d.Severity == DiagnosticSeverity.Error)
            {
                if (compilationAlreadyFailed && d.Id.StartsWith("CS", StringComparison.Ordinal))
                {
                    // Keep the raw CSxxxx id and text here: this list never reaches the user,
                    // and a bug report reconstructed from it wants Roslyn's own words, not the
                    // SPY0908 wrapper.
                    suppressed ??= new List<CompilerDiagnostic>();
                    suppressed.Add(ToCompilerDiagnostic(d, applyGeneratedCodeNet: false));
                    continue;
                }

                mapped.Add(ToCompilerDiagnostic(d));
            }
            else if (d.Severity == DiagnosticSeverity.Warning
                     && !d.Id.StartsWith("CS", StringComparison.Ordinal))
            {
                // Keep non-CS warnings (e.g. analyzer warnings) untouched. Warnings
                // with a CS id come from the compiler's own generated C# and are
                // internal noise the user cannot act on, so they are dropped.
                mapped.Add(ToCompilerDiagnostic(d));
            }
        }

        return new GeneratedCodeDiagnosticMapping(
            mapped,
            (IReadOnlyList<CompilerDiagnostic>?)suppressed ?? Array.Empty<CompilerDiagnostic>());
    }

    /// <summary>
    /// Debug-logs the CS errors the SPY0908 net swallowed (#1387) so a genuine emitter bug that
    /// happened to occur in a compilation the user had already broken is still recoverable from a
    /// verbose log rather than gone.
    /// </summary>
    private void LogSuppressedGeneratedCodeDiagnostics(IReadOnlyList<CompilerDiagnostic> suppressed)
    {
        if (suppressed.Count == 0 || !_logger.IsEnabled(CompilerLogLevel.Debug))
        {
            return;
        }

        foreach (var d in suppressed)
        {
            _logger.LogDebug(
                "SPY0908 net disarmed (errors already reported for this compilation, #1387); "
                + $"generated C# also failed with {d.Code}: {d.Message}");
        }
    }

    /// <summary>
    /// Convert a Roslyn diagnostic to a structured CompilerDiagnostic
    /// </summary>
    /// <param name="diagnostic">The Roslyn diagnostic from compiling generated C#.</param>
    /// <param name="applyGeneratedCodeNet">
    /// When true (the default), a <c>CSxxxx</c> error is remapped to SPY0908 so no raw Roslyn code
    /// can reach the user. Only the suppression path (#1387) passes false, and only for diagnostics
    /// that are recorded for diagnosis rather than reported.
    /// </param>
    internal static CompilerDiagnostic ToCompilerDiagnostic(
        Diagnostic diagnostic,
        bool applyGeneratedCodeNet = true)
    {
        var severity = diagnostic.Severity == DiagnosticSeverity.Error
            ? CompilerDiagnosticSeverity.Error
            : diagnostic.Severity == DiagnosticSeverity.Warning
                ? CompilerDiagnosticSeverity.Warning
                : CompilerDiagnosticSeverity.Info;

        int? line = null;
        int? column = null;
        string? filePath = null;

        var location = diagnostic.Location;
        if (location.IsInSource)
        {
            // Use GetMappedLineSpan to respect #line directives, which map
            // generated C# locations back to original .spy source files.
            var lineSpan = location.GetMappedLineSpan();
            if (!lineSpan.HasMappedPath && TryMapFromEnclosingRegion(location) is { } enclosing)
            {
                lineSpan = enclosing;
            }

            // A mapped path is the only coordinate the user can act on (#1427). Without one, the
            // span describes the generated C# — a file the user never wrote, cannot open, and did
            // not ask to be told about — so the position is omitted rather than reported. That is
            // strictly better than the generated-file fallback it replaces: a caret into
            // `probe_bracket_attr.cs:13:10` looks like a location and is not one, whereas an
            // omitted position lets the renderer fall back to naming the file it was handed. This
            // deliberately does not attempt to invent a mapping; the enclosing-region walk above
            // (#1237) is as far as the recovery goes.
            if (lineSpan.HasMappedPath)
            {
                filePath = lineSpan.Path;
                line = lineSpan.StartLinePosition.Line + 1;
                column = lineSpan.StartLinePosition.Character + 1;
            }
        }

        var rawMessage = diagnostic.GetMessage(CultureInfo.InvariantCulture);
        var code = diagnostic.Id;
        var message = rawMessage;

        // Last-chance guard: a raw Roslyn CS error means the compiler emitted invalid C#
        // that escaped every earlier check. Users must never see a bare CSxxxx code, so
        // remap it to the SPY0908 internal-error diagnostic. The mapped .spy location
        // (computed above) is preserved, and the original CS id + text stay in the
        // message so the bug report loses no information.
        if (applyGeneratedCodeNet
            && diagnostic.Severity == DiagnosticSeverity.Error
            && diagnostic.Id.StartsWith("CS", StringComparison.Ordinal))
        {
            code = DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError;
            message =
                $"internal error: generated C# failed to compile ({diagnostic.Id}: {rawMessage}). "
                + "This is a Sharpy compiler bug — please report it at "
                + "https://github.com/antonsynd/sharpy/issues";
        }

        return new CompilerDiagnostic(
            message,
            severity,
            line,
            column,
            filePath,
            code,
            CompilerPhase.Assembly);
    }

    /// <summary>
    /// Finds the <c>.spy</c> coordinate for a generated-C# location that carries no mapping of its own,
    /// by walking back to the nearest preceding mapped region (#1237).
    /// <para>
    /// <see cref="LineDirectivePostProcessor"/> anchors a <c>#line</c> at the first mapped line of each
    /// statement and frames the rest in <c>#line hidden</c>, so parts of a statement that are not
    /// themselves statements — a catch-clause header, a match pattern's type — sit in hidden gaps. A
    /// diagnostic landing there previously reported the generated file with no source line and no
    /// caret, which is useless in a bug report and, for SPY0908, actively misleading: it names a file
    /// the user never wrote.
    /// </para>
    /// <para>
    /// Fixed at the READER rather than by planting more <c>#line</c> anchors: the enclosing statement is
    /// the right granularity for "here is where in your code this happened", and adding per-node
    /// anchors would change debugger stepping behaviour (#609). Returns null when the tree carries no
    /// mapped regions at all — an <c>EmitLineDirectives</c>-off compilation, such as the REPL — so that
    /// path keeps its existing generated-file fallback.
    /// </para>
    /// </summary>
    internal static FileLinePositionSpan? TryMapFromEnclosingRegion(Location location)
    {
        var tree = location.SourceTree;
        if (tree == null)
        {
            return null;
        }

        // The UNmapped line: where the diagnostic actually sits in the generated C#.
        var actualLine = location.GetLineSpan().StartLinePosition.Line;

        FileLinePositionSpan? nearest = null;
        var nearestStart = -1;
        foreach (var mapping in tree.GetLineMappings())
        {
            if (mapping.IsHidden || !mapping.MappedSpan.HasMappedPath)
            {
                continue;
            }

            var start = mapping.Span.Start.Line;
            if (start <= actualLine && start > nearestStart)
            {
                nearestStart = start;
                nearest = mapping.MappedSpan;
            }
        }

        return nearest;
    }

    /// <summary>
    /// Generate a .runtimeconfig.json file for the assembly
    /// </summary>
    private void GenerateRuntimeConfig(string assemblyPath, ProjectConfig projectConfig)
    {
        try
        {
            var runtimeConfigPath = Path.ChangeExtension(assemblyPath, ".runtimeconfig.json");

            // Get the current runtime version
            var runtimeVersion = Environment.Version;
            var frameworkVersion = $"{runtimeVersion.Major}.{runtimeVersion.Minor}.{runtimeVersion.Build}";

            // Create runtime config JSON
            var runtimeConfig = $$"""
{
  "runtimeOptions": {
    "tfm": "{{projectConfig.TargetFramework}}",
    "framework": {
      "name": "Microsoft.NETCore.App",
      "version": "{{frameworkVersion}}"
    },
    "configProperties": {
      "System.Reflection.Metadata.MetadataUpdater.IsSupported": false
    }
  }
}
""";

            File.WriteAllText(runtimeConfigPath, runtimeConfig);
            _logger.LogDebug($"Generated runtime config: {runtimeConfigPath}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to generate runtime config: {ex.Message}", 0, 0);
        }
    }

    /// <summary>
    /// Generate a .deps.json file for the assembly
    /// </summary>
    private void GenerateDepsFile(string assemblyPath, ProjectConfig projectConfig)
    {
        try
        {
            var depsPath = Path.ChangeExtension(assemblyPath, ".deps.json");
            var assemblyName = projectConfig.AssemblyName ?? projectConfig.RootNamespace;

            var sharpyCoreAssembly = typeof(SharpyRT::Sharpy.Builtins).Assembly;
            var sharpyCoreLocation = sharpyCoreAssembly.Location;
            var sharpyCoreName = sharpyCoreAssembly.GetName();
            var sharpyCoreVersion = sharpyCoreName.Version?.ToString() ?? "1.0.0";

            var runtimeVersion = Environment.Version;

            var dependencyEntries = new List<string> { $@"""Sharpy.Core"": ""{sharpyCoreVersion}""" };
            var targetEntries = new List<string>();
            var libraryEntries = new List<string>();

            targetEntries.Add($$"""
      "Sharpy.Core/{{sharpyCoreVersion}}": {
        "runtime": {
          "{{Path.GetFileName(sharpyCoreLocation)}}": {
            "assemblyVersion": "{{sharpyCoreVersion}}",
            "fileVersion": "{{sharpyCoreVersion}}"
          }
        }
      }
""");
            libraryEntries.Add($$"""
    "Sharpy.Core/{{sharpyCoreVersion}}": {
      "type": "reference",
      "serviceable": false,
      "sha512": ""
    }
""");

            // Emit the full transitive managed runtime closure, not just the direct references:
            // a program that imports numpy pulls MathNet.Numerics (and sqlite3 pulls
            // SQLitePCLRaw.*, etc.) transitively, and with a deps.json present the host resolves
            // ONLY listed managed assemblies into the trusted-platform-assembly set — so an
            // omitted transitive dependency is invisible at runtime (#1084).
            // This deliberately declares the closure of ALL configured references — a superset of
            // what RuntimeDependencyHelper copies next to the output (it seeds from used modules
            // only). Declared-but-absent assemblies are harmless: the host resolves deps.json
            // entries lazily, so an entry is only consulted when the assembly actually loads.
            var runtimeClosure = RuntimeClosureResolver.Resolve(projectConfig.References);

            foreach (var refPath in runtimeClosure.ManagedAssemblies)
            {
                var fileName = Path.GetFileName(refPath);
                if (!File.Exists(refPath) || fileName == "Sharpy.Core.dll")
                    continue;

                try
                {
                    var refName = System.Reflection.AssemblyName.GetAssemblyName(refPath);
                    var refVersion = refName.Version?.ToString() ?? "1.0.0";
                    var refSimpleName = refName.Name ?? Path.GetFileNameWithoutExtension(refPath);

                    dependencyEntries.Add($@"""{refSimpleName}"": ""{refVersion}""");
                    targetEntries.Add($$"""
      "{{refSimpleName}}/{{refVersion}}": {
        "runtime": {
          "{{fileName}}": {
            "assemblyVersion": "{{refVersion}}",
            "fileVersion": "{{refVersion}}"
          }
        }
      }
""");
                    libraryEntries.Add($$"""
    "{{refSimpleName}}/{{refVersion}}": {
      "type": "reference",
      "serviceable": false,
      "sha512": ""
    }
""");
                }
                catch
                {
                }
            }

            var depsJson = $$"""
{
  "runtimeTarget": {
    "name": ".NETCoreApp,Version=v{{runtimeVersion.Major}}.{{runtimeVersion.Minor}}",
    "signature": ""
  },
  "compilationOptions": {},
  "targets": {
    ".NETCoreApp,Version=v{{runtimeVersion.Major}}.{{runtimeVersion.Minor}}": {
      "{{assemblyName}}/1.0.0": {
        "dependencies": {
          {{string.Join(",\n          ", dependencyEntries)}}
        },
        "runtime": {
          "{{Path.GetFileName(assemblyPath)}}": {}
        }
      },
{{string.Join(",\n", targetEntries)}}    }
  },
  "libraries": {
    "{{assemblyName}}/1.0.0": {
      "type": "project",
      "serviceable": false,
      "sha512": ""
    },
{{string.Join(",\n", libraryEntries)}}  }
}
""";

            File.WriteAllText(depsPath, depsJson);
            _logger.LogDebug($"Generated deps file: {depsPath}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to generate deps file: {ex.Message}", 0, 0);
        }
    }
}

/// <summary>
/// Result of assembly compilation
/// </summary>
internal class AssemblyCompilationResult
{
    public bool Success { get; init; }

    /// <summary>
    /// Structured diagnostics from assembly compilation.
    /// This is the primary way to access errors, warnings, and other diagnostics.
    /// </summary>
    public DiagnosticBag Diagnostics { get; init; } = new();

    /// <summary>
    /// CS errors from the generated C# that the SPY0908 net deliberately did not report because
    /// the compilation had already failed for a reason the user can act on (#1387). Never shown to
    /// the user; kept so the leak corpus the #1146 sweeps depend on stays observable to tests and
    /// so a debug log can carry the evidence a real emitter bug would have left.
    /// </summary>
    public IReadOnlyList<CompilerDiagnostic> SuppressedGeneratedCodeDiagnostics { get; init; }
        = Array.Empty<CompilerDiagnostic>();

    public string? OutputAssemblyPath { get; init; }
    public CompilationMetrics? Metrics { get; init; }
}

/// <summary>
/// Outcome of mapping a generated-C# compile's Roslyn diagnostics (#1387): what the user is told
/// (<see cref="Reported"/>), and the CS errors the SPY0908 net stood down from because errors were
/// already reported for this compilation (<see cref="Suppressed"/>, unmapped, for logs and tests).
/// </summary>
internal readonly record struct GeneratedCodeDiagnosticMapping(
    IReadOnlyList<CompilerDiagnostic> Reported,
    IReadOnlyList<CompilerDiagnostic> Suppressed);
