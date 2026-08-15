using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Parser;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Model;
using Sharpy.Compiler.Services;

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
                    // Preserve partial metrics (token/AST-node counts already captured above) on the
                    // failed unit for error-path observability, matching the former single-file driver.
                    compilationUnit.Metrics = fileMetrics;
                    MergeWithPhase(compilationUnit.Diagnostics, parser.Diagnostics, CompilerPhase.Parser, sourceFile);
                    compilationUnit.Phase = CompilationPhase.Failed;
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

    /// <summary>
    /// Parses files that were invalidated after symbol validation.
    /// These files were previously skipped but their cached symbols are now stale.
    /// </summary>
    private bool ParseInvalidatedFiles(ProjectConfig config)
    {
        foreach (var sourceFile in config.SourceFiles)
        {
            var unit = _projectModel!.GetUnit(sourceFile);
            if (unit == null)
                continue;

            // Only parse files that need recompilation (phase was reset from Skipped)
            // Skip files that are still Skipped or already processed
            if (unit.Phase != CompilationPhase.Parsed && !_filesToSkip.Contains(sourceFile))
            {
                // This file was invalidated and needs to be parsed
                var fileMetrics = new CompilationMetrics(
                    fileName: Path.GetRelativePath(config.ProjectDirectory, sourceFile),
                    projectName: config.RootNamespace,
                    configuration: config.Configuration);

                try
                {
                    // The source text is already in the unit (created during initial parsing)
                    // We just need to re-parse it
                    var source = unit.SourceText;
                    unit.GeneratedCSharp = null; // Clear cached C#

                    fileMetrics.StartPhase(CompilerPhaseNames.LexicalAnalysis);
                    LogPhaseStartEvent(CompilerPhaseNames.LexicalAnalysis, sourceFile);
                    var sourceText = new Text.SourceText(source, sourceFile);
                    var lexer = new Lexer.Lexer(sourceText, _logger);
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

                    if (lexer.Diagnostics.HasErrors)
                    {
                        MergeWithPhase(unit.Diagnostics, lexer.Diagnostics, CompilerPhase.Lexer, sourceFile);
                        unit.Phase = CompilationPhase.Failed;
                        fileMetrics.DiagnosticCount = lexer.Diagnostics.GetAll().Count;
                        MergeWithPhase(_diagnostics, lexer.Diagnostics, CompilerPhase.Lexer, sourceFile);
                        ProjectMetrics.AddFileMetrics(fileMetrics);
                        continue;
                    }

                    unit.Tokens = tokens;
                    unit.Phase = CompilationPhase.Lexed;

                    fileMetrics.StartPhase(CompilerPhaseNames.SyntaxAnalysis);
                    LogPhaseStartEvent(CompilerPhaseNames.SyntaxAnalysis, sourceFile, tokens.Count);
                    var parserMaxErrors = _maxErrors > 0 ? _maxErrors : 25;
                    var parser = new Parser.Parser(tokens, _logger, parserMaxErrors, features: _features);
                    var module = parser.ParseModule();
                    fileMetrics.EndPhase();
                    LogPhaseEndEvent(fileMetrics, sourceFile, parser.Diagnostics.ErrorCount);

                    // Capture AST node count immediately (available even if later phases fail)
                    if (module != null)
                    {
                        fileMetrics.AstNodeCount = AstValidator.CountNodes(module);
                    }

                    if (parser.Diagnostics.HasErrors)
                    {
                        MergeWithPhase(unit.Diagnostics, parser.Diagnostics, CompilerPhase.Parser, sourceFile);
                        unit.Phase = CompilationPhase.Failed;
                        fileMetrics.DiagnosticCount = parser.Diagnostics.GetAll().Count;
                        MergeWithPhase(_diagnostics, parser.Diagnostics, CompilerPhase.Parser, sourceFile);
                        ProjectMetrics.AddFileMetrics(fileMetrics);
                        continue;
                    }

                    // Store AST (module is non-null at this point - parser always returns a Module)
                    unit.Ast = module!;
                    unit.Phase = CompilationPhase.Parsed;

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
                    unit.Imports = imports;
                    unit.FromImports = fromImports;
                    unit.Metrics = fileMetrics;

                    if (_logger.IsEnabled(CompilerLogLevel.Debug))
                    {
                        _logger.LogDebug($"Re-parsed {Path.GetFileName(sourceFile)} (invalidated): {fileMetrics.TotalDuration.TotalMilliseconds:F2} ms");
                    }

                    ProjectMetrics.AddFileMetrics(fileMetrics);
                }
                catch (Exception ex)
                {
                    // Log full exception for debugging
                    _logger.LogError($"Failed to re-parse {sourceFile} ({ex.GetType().Name}): {ex}", 0, 0);

                    // Create error message with exception type for identification
                    var errorMessage = $"Failed to re-parse file ({ex.GetType().Name}): {ex.Message}";

                    unit.Diagnostics.AddError(errorMessage, filePath: sourceFile, code: DiagnosticCodes.Infrastructure.FileReadError);
                    unit.Phase = CompilationPhase.Failed;
                    _diagnostics.AddError(errorMessage, filePath: sourceFile, code: DiagnosticCodes.Infrastructure.FileReadError);
                    ProjectMetrics.AddFileMetrics(fileMetrics);
                }
            }
        }

        return !_diagnostics.HasErrors;
    }
}
