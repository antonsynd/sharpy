using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Parser;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Model;
using Sharpy.Compiler.Services;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Project;

internal partial class ProjectCompiler
{
    /// <summary>
    /// Reads the source text for <paramref name="sourceFile"/>, preferring an in-memory override
    /// (<see cref="ProjectConfig.InMemorySources"/>) over the file on disk. The synthetic
    /// project-of-one-file (#1038) uses this so inline single-file source keeps the caller's
    /// path verbatim without a temp file.
    /// </summary>
    private static string ReadSource(string sourceFile, ProjectConfig config)
    {
        if (config.InMemorySources != null
            && config.InMemorySources.TryGetValue(sourceFile, out var inMemory))
        {
            return inMemory;
        }
        return File.ReadAllText(sourceFile);
    }

    /// <summary>
    /// SPY0526 (#1948, Decision 28 (d)): the package layouts module-as-namespace cannot emit. Directories
    /// are C# namespaces, so a directory and a module class nested in one another never collide
    /// (<c>lib/lib.spy</c> is <c>namespace …Lib { class Lib }</c>). What survives: (1) a module file beside
    /// a same-named package directory, (2) a package's <c>__init__</c> top-level name that is one of its
    /// own submodules or subpackages, and (3) a submodule or subpackage spelled like the package's
    /// <c>__init__</c> module class (<c>pkg/pkg_module.spy</c> beside <c>pkg/__init__.spy</c>'s
    /// <c>PkgModule</c>). Refused by name after parsing, before any analysis, for EVERY source file (an
    /// un-imported file is still emitted). Rung 4 by necessity: no CLR surface spells "these two paths
    /// emit one identifier". Every name comes from <see cref="ModuleIdentifiers"/>, the authority the
    /// emitter spells them with, relative to the same common source root
    /// (<see cref="ComputeSourceRootPath"/>), so the refusal and the emission cannot disagree.
    /// </summary>
    /// <returns><c>true</c> when at least one collision was reported.</returns>
    private bool ReportPackageModuleNameCollisions(ProjectConfig config)
    {
        var sourceRoot = ComputeSourceRootPath(config);
        var reported = false;

        // Refusal 1 (#1948): a module file beside a same-named package directory. Python imports
        // only one of them, so the other's modules are unreachable; C# spells a class and a namespace
        // of one name in one scope (CS0101). A main.spy declaring main() emits "Program" — the one
        // entry predicate the emitter reads (#2013); a unit served from the incremental cache has no
        // AST, and its cold build recorded the bit.
        bool WillGenerateMain(string file)
        {
            var body = _projectModel?.GetUnit(file)?.Ast?.Body;
            return body != null
                ? ModuleIdentifiers.DeclaresEntryMain(body)
                : _incrementalCache?.GetFileCache(file)?.DeclaresEntryMain ?? false;
        }

        foreach (var (file, directory, identifier) in
                 ModuleIdentifiers.FindModuleBesideSameNamedPackage(sourceRoot, config.SourceFiles, WillGenerateMain))
        {
            _diagnostics.AddError(
                $"Module '{Path.GetFileName(file)}' and the package directory '{directory}' beside it both emit " +
                $"the C# identifier '{identifier}' (python imports only one of them, so the other's modules " +
                "could never be imported). Rename the file or the directory.",
                line: 1,
                column: 1,
                filePath: file,
                code: DiagnosticCodes.CodeGen.PackageModuleNameCollision,
                phase: CompilerPhase.CodeGeneration);
            reported = true;
        }

        // Refusal 2 (#1948): a package's __init__ declaring a top-level name whose emitted identifier
        // is one of its own submodules or subpackages. Python's `pkg.lib` then names two things (the
        // submodule import rebinds the attribute); in C# the two share the package's scope. Refused
        // arity-blind by emitted identifier. A cache-served __init__ has no AST: it is re-parsed, so
        // a warm build refuses exactly what a cold one does when a sibling module is added.
        foreach (var initFile in config.SourceFiles.Where(f => Path.GetFileNameWithoutExtension(f) == Sharpy.Compiler.Semantic.DunderNames.Init))
        {
            var children = ModuleIdentifiers.PackageChildIdentifiers(
                sourceRoot, Path.GetDirectoryName(initFile)!, config.SourceFiles);
            if (children.Count == 0)
                continue;

            // Refusal 3: a child spelled like the package's own module class — both are members of the
            // package namespace (CS0101). Python has no such conflict; the name is Sharpy's <X>.
            var membersClass = ModuleIdentifiers.ModuleClassName(initFile, willGenerateMainMethod: false);
            if (children.TryGetValue(membersClass, out var twin))
            {
                var twinIsPackage = twin.EndsWith('/');
                _diagnostics.AddError(
                    $"The package's __init__.spy emits its module class '{membersClass}', which its " +
                    $"{(twinIsPackage ? "subpackage" : "submodule")} '{twin.TrimEnd('/')}' also emits. Rename " +
                    $"the {(twinIsPackage ? "subpackage" : "submodule")}.",
                    line: 1,
                    column: 1,
                    filePath: initFile,
                    code: DiagnosticCodes.CodeGen.PackageModuleNameCollision,
                    phase: CompilerPhase.CodeGeneration);
                reported = true;
            }

            var body = _projectModel?.GetUnit(initFile)?.Ast?.Body ?? ParseModuleBodyForCheck(initFile, config);
            if (body == null)
                continue;

            foreach (var (name, identifier, declaration) in ModuleIdentifiers.TopLevelMemberIdentifiers(body))
            {
                if (!children.TryGetValue(identifier, out var child))
                    continue;

                var isPackage = child.EndsWith('/');
                var childName = isPackage ? child.TrimEnd('/') : Path.GetFileNameWithoutExtension(child);
                var pythonReason = childName == name
                    ? $" (python's `{Path.GetFileName(Path.GetDirectoryName(initFile))}.{name}` would name both)"
                    : "";
                _diagnostics.AddError(
                    $"'{name}' in the package's __init__.spy emits the C# identifier '{identifier}', which its " +
                    $"{(isPackage ? "subpackage" : "submodule")} '{child.TrimEnd('/')}' also emits{pythonReason}. " +
                    $"Rename the declaration or the {(isPackage ? "subpackage" : "submodule")}.",
                    line: declaration.LineStart,
                    column: declaration.ColumnStart,
                    filePath: initFile,
                    code: DiagnosticCodes.CodeGen.PackageModuleNameCollision,
                    phase: CompilerPhase.CodeGeneration);
                reported = true;
            }
        }

        return reported;
    }

    /// <summary>
    /// Parses <paramref name="filePath"/> for the SPY0526 pre-emission check when the unit has no AST
    /// (served from the incremental cache). Null when the file cannot be read; parse errors are not
    /// reported here (the file parsed cleanly when it was cached).
    /// </summary>
    private static System.Collections.Immutable.ImmutableArray<Statement>? ParseModuleBodyForCheck(
        string filePath, ProjectConfig config)
    {
        try
        {
            var text = new Sharpy.Compiler.Text.SourceText(ReadSource(filePath, config), filePath);
            var tokens = new Sharpy.Compiler.Lexer.Lexer(text, NullLogger.Instance).TokenizeAll();
            return new Sharpy.Compiler.Parser.Parser(tokens, NullLogger.Instance).ParseModule().Body;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>
    /// Phase 1: Parse all source files into AST modules
    /// </summary>
    private bool ParseAllFiles(ProjectConfig config, CancellationToken cancellationToken = default)
    {
        var filesToParse = config.SourceFiles.Count - _filesToSkip.Count;
        _logger.LogInfo($"Phase 1: Parsing {filesToParse} source files ({_filesToSkip.Count} skipped)");

        foreach (var sourceFile in config.SourceFiles)
        {
            var fileMetrics = new CompilationMetrics(
                fileName: Path.GetRelativePath(config.ProjectDirectory, sourceFile),
                projectName: config.RootNamespace,
                configuration: config.Configuration);

            try
            {
                // Skip unchanged files in incremental mode
                if (_filesToSkip.Contains(sourceFile))
                {
                    var skippedModulePath = CompilationUnitFactory.ComputeModulePath(sourceFile, config.ProjectDirectory);
                    var skippedSource = ReadSource(sourceFile, config);
                    var unit = _projectModel!.CreateUnit(sourceFile, skippedModulePath, skippedSource);

                    // Restore cached generated C# code
                    var cached = _incrementalCache?.GetFileCache(sourceFile);
                    if (cached != null)
                    {
                        unit.GeneratedCSharp = cached.GeneratedCSharp;
                    }

                    // Replay cached diagnostics into both bags (#1553)
                    if (cached?.Diagnostics is { Count: > 0 } cachedDiags)
                    {
                        foreach (var cd in cachedDiags)
                        {
                            var severity = Enum.TryParse<CompilerDiagnosticSeverity>(cd.Severity, out var s)
                                ? s : CompilerDiagnosticSeverity.Warning;
                            var phase = Enum.TryParse<CompilerPhase>(cd.Phase, out var p)
                                ? p : CompilerPhase.Unknown;
                            var span = cd is { SpanStart: { } spanStart, SpanLength: { } spanLength }
                                ? new Sharpy.Compiler.Text.TextSpan(spanStart, spanLength)
                                : (Sharpy.Compiler.Text.TextSpan?)null;
                            var related = cd.RelatedLocations?
                                .Select(rl => new DiagnosticRelatedLocation(
                                    rl.Message, rl.Line, rl.Column, rl.FilePath,
                                    rl is { SpanStart: { } rs, SpanLength: { } rn }
                                        ? new Sharpy.Compiler.Text.TextSpan(rs, rn)
                                        : (Sharpy.Compiler.Text.TextSpan?)null))
                                .ToList();
                            var diag = new CompilerDiagnostic(
                                cd.Message, severity, cd.Line, cd.Column, cd.FilePath,
                                cd.Code, phase, span, cd.Data, related);

                            unit.Diagnostics.Add(diag);
                            _diagnostics.Add(diag);
                        }
                    }

                    unit.Phase = CompilationPhase.Skipped;
                    ProjectMetrics.AddSkippedFile(sourceFile);

                    if (_logger.IsEnabled(CompilerLogLevel.Debug))
                    {
                        _logger.LogDebug($"Skipping {Path.GetFileName(sourceFile)} (unchanged)");
                    }
                    continue;
                }

                cancellationToken.ThrowIfCancellationRequested();

                // Reuse discovery-pass artifacts when available: the import-closure walk already
                // lexed and parsed this file cleanly, so re-lexing/re-parsing here is exactly the
                // #1086 double parse. The cache holds only pristine parses — error-bearing files are
                // absent and fall through to a normal parse that surfaces their diagnostics.
                if (config.PreParsedUnits != null
                    && config.PreParsedUnits.TryGetValue(sourceFile, out var preParsed))
                {
                    ConsumePreParsedUnit(sourceFile, config, preParsed, fileMetrics);
                    continue;
                }

                var source = ReadSource(sourceFile, config);

                // Create CompilationUnit for this file
                var modulePath = CompilationUnitFactory.ComputeModulePath(sourceFile, config.ProjectDirectory);
                var compilationUnit = _projectModel!.CreateUnit(sourceFile, modulePath, source);

                fileMetrics.StartPhase(CompilerPhaseNames.LexicalAnalysis);
                LogPhaseStartEvent(CompilerPhaseNames.LexicalAnalysis, sourceFile);
                var sourceText = new Text.SourceText(source, sourceFile);
                var lexer = new Lexer.Lexer(sourceText, _logger, cancellationToken: cancellationToken,
                    preserveTrivia: config.PreserveTrivia);
                if (_maxErrors > 0)
                {
                    lexer.MaxErrors = _maxErrors;
                }
                lexer.Features = _features;
                var tokens = lexer.TokenizeAll();
                fileMetrics.EndPhase();
                LogPhaseEndEvent(fileMetrics, sourceFile, lexer.Diagnostics.ErrorCount);

                // Capture token count immediately (available even if later phases fail)
                fileMetrics.TokenCount = tokens.Count;

                // Check if lexer collected any errors
                if (lexer.Diagnostics.HasErrors)
                {
                    // Preserve partial artifacts on the failed unit so per-file metrics and tokens
                    // remain observable on early failure (parity with the former single-file driver;
                    // also benefits project mode, where failed files previously carried neither).
                    compilationUnit.Tokens = tokens;
                    compilationUnit.Metrics = fileMetrics;
                    MergeWithPhase(compilationUnit.Diagnostics, lexer.Diagnostics, CompilerPhase.Lexer, sourceFile);
                    compilationUnit.Phase = CompilationPhase.Failed;
                    fileMetrics.DiagnosticCount = lexer.Diagnostics.GetAll().Count;
                    MergeWithPhase(_diagnostics, lexer.Diagnostics, CompilerPhase.Lexer, sourceFile);
                    ProjectMetrics.AddFileMetrics(fileMetrics);
                    continue;
                }

                // Store tokens in CompilationUnit
                compilationUnit.Tokens = tokens;
                compilationUnit.Phase = CompilationPhase.Lexed;

                // Surface comment locations for the single-file analyze path (LSP hover).
                // Only computed when trivia preservation is requested; the flag is off for
                // ordinary project/CLI compiles, keeping their output byte-identical.
                if (config.PreserveTrivia)
                    compilationUnit.CommentSpans = CommentSpanExtractor.Extract(tokens);

                fileMetrics.StartPhase(CompilerPhaseNames.SyntaxAnalysis);
                LogPhaseStartEvent(CompilerPhaseNames.SyntaxAnalysis, sourceFile, tokens.Count);
                var parserMaxErrors = _maxErrors > 0 ? _maxErrors : 25;
                var parser = new Parser.Parser(tokens, _logger, parserMaxErrors, cancellationToken, _features);
                var module = parser.ParseModule();
                fileMetrics.EndPhase();
                LogPhaseEndEvent(fileMetrics, sourceFile, parser.Diagnostics.ErrorCount);

                // Capture AST node count immediately (available even if later phases fail)
                if (module != null)
                {
                    fileMetrics.AstNodeCount = AstValidator.CountNodes(module);
                }

                // Check if parser collected any errors
                if (parser.Diagnostics.HasErrors)
                {
                    // Preserve partial AST and metrics so downstream phases (semantic analysis)
                    // can still resolve receivers for LSP completion (#1360).
                    compilationUnit.Ast = module;
                    compilationUnit.Metrics = fileMetrics;
                    MergeWithPhase(compilationUnit.Diagnostics, parser.Diagnostics, CompilerPhase.Parser, sourceFile);
                    compilationUnit.Phase = CompilationPhase.ParsedWithErrors;
                    fileMetrics.DiagnosticCount = parser.Diagnostics.GetAll().Count;
                    MergeWithPhase(_diagnostics, parser.Diagnostics, CompilerPhase.Parser, sourceFile);
                    ProjectMetrics.AddFileMetrics(fileMetrics);
                    continue;
                }

                // Store AST in CompilationUnit (module is non-null at this point - parser always returns a Module)
                compilationUnit.Ast = module!;
                compilationUnit.Phase = CompilationPhase.Parsed;

                // Validate parse output (same invariants as single-file Compiler)
                CompilerInvariants.AssertPostParse(module!, _diagnostics);
                AstValidator.ValidateTree(module!);

                // Extract imports from AST
                var imports = new List<ImportStatement>();
                var fromImports = new List<FromImportStatement>();
                foreach (var stmt in module!.Body)
                {
                    // Unwrap suppress-decorated imports (#1124) so dependency extraction sees them.
                    var scanned = stmt.UnwrapDecorated();
                    if (scanned is ImportStatement import)
                        imports.Add(import);
                    else if (scanned is FromImportStatement fromImport)
                        fromImports.Add(fromImport);
                }
                compilationUnit.Imports = imports;
                compilationUnit.FromImports = fromImports;

                // Store metrics in CompilationUnit
                compilationUnit.Metrics = fileMetrics;

                // Log per-file metrics at Debug level
                if (_logger.IsEnabled(CompilerLogLevel.Debug))
                {
                    _logger.LogDebug($"Parsed {Path.GetFileName(sourceFile)}: {fileMetrics.TotalDuration.TotalMilliseconds:F2} ms");
                }

                ProjectMetrics.AddFileMetrics(fileMetrics);
            }
            catch (OperationCanceledException)
            {
                // Re-throw so the Compile() method's handler records CompilationCancelled
                throw;
            }
            catch (Exception ex)
            {
                // Log full exception for debugging
                _logger.LogError($"Failed to parse {sourceFile} ({ex.GetType().Name}): {ex}", 0, 0);

                // Create error message with exception type for identification
                var errorMessage = $"Failed to parse file ({ex.GetType().Name}): {ex.Message}";

                // Add to CompilationUnit diagnostics if available
                var unit = _projectModel!.GetUnit(sourceFile);
                if (unit != null)
                {
                    unit.Diagnostics.AddError(errorMessage, filePath: sourceFile, code: DiagnosticCodes.Infrastructure.FileReadError);
                    unit.Phase = CompilationPhase.Failed;
                }

                _diagnostics.AddError(errorMessage, filePath: sourceFile, code: DiagnosticCodes.Infrastructure.FileReadError);
                ProjectMetrics.AddFileMetrics(fileMetrics);
            }
        }

        return !_diagnostics.HasErrors;
    }

    /// <summary>
    /// Reuses a discovery-pass parse (<see cref="ProjectConfig.PreParsedUnits"/>) for
    /// <paramref name="sourceFile"/> rather than lexing and parsing it a second time (#1086).
    /// The unit is populated exactly as the normal parse path would leave it — same tokens, AST,
    /// CommentSpans, import lists, and post-parse invariant checks — and the lexical/syntax phases
    /// are still bracketed (near-zero durations) so per-file metrics and structured logging observe
    /// them.
    /// </summary>
    private void ConsumePreParsedUnit(
        string sourceFile, ProjectConfig config, PreParsedUnit preParsed, CompilationMetrics fileMetrics)
    {
        // Compile the exact text the artifacts were built from (no disk re-read) so tokens, AST,
        // and content hash stay consistent — the same no-TOCTOU contract as InMemorySources.
        var modulePath = CompilationUnitFactory.ComputeModulePath(sourceFile, config.ProjectDirectory);
        var compilationUnit = _projectModel!.CreateUnit(sourceFile, modulePath, preParsed.Source);

        // The discovery pass already lexed/parsed this file cleanly, so these brackets close
        // with near-zero durations. They still fire (metric + structured event) so per-file
        // observability sees every phase; a clean cached parse carries zero phase errors.
        fileMetrics.StartPhase(CompilerPhaseNames.LexicalAnalysis);
        LogPhaseStartEvent(CompilerPhaseNames.LexicalAnalysis, sourceFile);
        fileMetrics.EndPhase();
        LogPhaseEndEvent(fileMetrics, sourceFile);
        fileMetrics.TokenCount = preParsed.Tokens.Count;
        compilationUnit.Tokens = preParsed.Tokens;
        compilationUnit.Phase = CompilationPhase.Lexed;

        // CommentSpans were extracted at discovery only when trivia preservation was on; the
        // cached list already matches what the parse path would produce for this source.
        if (config.PreserveTrivia && preParsed.CommentSpans != null)
            compilationUnit.CommentSpans = preParsed.CommentSpans;

        fileMetrics.StartPhase(CompilerPhaseNames.SyntaxAnalysis);
        LogPhaseStartEvent(CompilerPhaseNames.SyntaxAnalysis, sourceFile, preParsed.Tokens.Count);
        fileMetrics.EndPhase();
        LogPhaseEndEvent(fileMetrics, sourceFile);
        fileMetrics.AstNodeCount = AstValidator.CountNodes(preParsed.Ast);
        compilationUnit.Ast = preParsed.Ast;
        compilationUnit.Phase = CompilationPhase.Parsed;

        // Same post-parse invariants the parse path enforces (read-only over the AST).
        CompilerInvariants.AssertPostParse(preParsed.Ast, _diagnostics);
        AstValidator.ValidateTree(preParsed.Ast);

        // Recompute import lists from the cached AST (identical extraction to the parse path).
        var imports = new List<ImportStatement>();
        var fromImports = new List<FromImportStatement>();
        foreach (var stmt in preParsed.Ast.Body)
        {
            // Unwrap suppress-decorated imports (#1124) so dependency extraction sees them.
            var scanned = stmt.UnwrapDecorated();
            if (scanned is ImportStatement import)
                imports.Add(import);
            else if (scanned is FromImportStatement fromImport)
                fromImports.Add(fromImport);
        }
        compilationUnit.Imports = imports;
        compilationUnit.FromImports = fromImports;

        compilationUnit.Metrics = fileMetrics;

        if (_logger.IsEnabled(CompilerLogLevel.Debug))
            _logger.LogDebug($"Reused discovery parse for {Path.GetFileName(sourceFile)} (no re-lex/parse)");

        ProjectMetrics.AddFileMetrics(fileMetrics);
    }
}
