using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Parser;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Model;

namespace Sharpy.Compiler.Project;

internal partial class ProjectCompiler
{
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
                    var skippedSource = File.ReadAllText(sourceFile);
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

                var source = File.ReadAllText(sourceFile);

                // Create CompilationUnit for this file
                var modulePath = CompilationUnitFactory.ComputeModulePath(sourceFile, config.ProjectDirectory);
                var compilationUnit = _projectModel!.CreateUnit(sourceFile, modulePath, source);

                cancellationToken.ThrowIfCancellationRequested();

                fileMetrics.StartPhase("Lexical Analysis");
                var sourceText = new Text.SourceText(source, sourceFile);
                var lexer = new Lexer.Lexer(sourceText, _logger, cancellationToken: cancellationToken);
                if (_maxErrors > 0)
                {
                    lexer.MaxErrors = _maxErrors;
                }
                var tokens = lexer.TokenizeAll();
                fileMetrics.EndPhase();

                // Capture token count immediately (available even if later phases fail)
                fileMetrics.TokenCount = tokens.Count;

                // Check if lexer collected any errors
                if (lexer.Diagnostics.HasErrors)
                {
                    compilationUnit.Diagnostics.Merge(lexer.Diagnostics);
                    compilationUnit.Phase = CompilationPhase.Failed;
                    fileMetrics.DiagnosticCount = lexer.Diagnostics.GetAll().Count;
                    _diagnostics.Merge(lexer.Diagnostics);
                    ProjectMetrics.AddFileMetrics(fileMetrics);
                    continue;
                }

                // Store tokens in CompilationUnit
                compilationUnit.Tokens = tokens;
                compilationUnit.Phase = CompilationPhase.Lexed;

                fileMetrics.StartPhase("Syntax Analysis");
                var parserMaxErrors = _maxErrors > 0 ? _maxErrors : 25;
                var parser = new Parser.Parser(tokens, _logger, parserMaxErrors, cancellationToken);
                var module = parser.ParseModule();
                fileMetrics.EndPhase();

                // Capture AST node count immediately (available even if later phases fail)
                if (module != null)
                {
                    fileMetrics.AstNodeCount = AstValidator.CountNodes(module);
                }

                // Check if parser collected any errors
                if (parser.Diagnostics.HasErrors)
                {
                    compilationUnit.Diagnostics.Merge(parser.Diagnostics);
                    compilationUnit.Phase = CompilationPhase.Failed;
                    fileMetrics.DiagnosticCount = parser.Diagnostics.GetAll().Count;
                    _diagnostics.Merge(parser.Diagnostics);
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
                    if (stmt is ImportStatement import)
                        imports.Add(import);
                    else if (stmt is FromImportStatement fromImport)
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

                    fileMetrics.StartPhase("Lexical Analysis");
                    var sourceText = new Text.SourceText(source, sourceFile);
                    var lexer = new Lexer.Lexer(sourceText, _logger);
                    if (_maxErrors > 0)
                    {
                        lexer.MaxErrors = _maxErrors;
                    }
                    var tokens = lexer.TokenizeAll();
                    fileMetrics.EndPhase();

                    // Capture token count immediately (available even if later phases fail)
                    fileMetrics.TokenCount = tokens.Count;

                    if (lexer.Diagnostics.HasErrors)
                    {
                        unit.Diagnostics.Merge(lexer.Diagnostics);
                        unit.Phase = CompilationPhase.Failed;
                        fileMetrics.DiagnosticCount = lexer.Diagnostics.GetAll().Count;
                        _diagnostics.Merge(lexer.Diagnostics);
                        ProjectMetrics.AddFileMetrics(fileMetrics);
                        continue;
                    }

                    unit.Tokens = tokens;
                    unit.Phase = CompilationPhase.Lexed;

                    fileMetrics.StartPhase("Syntax Analysis");
                    var parserMaxErrors = _maxErrors > 0 ? _maxErrors : 25;
                    var parser = new Parser.Parser(tokens, _logger, parserMaxErrors);
                    var module = parser.ParseModule();
                    fileMetrics.EndPhase();

                    // Capture AST node count immediately (available even if later phases fail)
                    if (module != null)
                    {
                        fileMetrics.AstNodeCount = AstValidator.CountNodes(module);
                    }

                    if (parser.Diagnostics.HasErrors)
                    {
                        unit.Diagnostics.Merge(parser.Diagnostics);
                        unit.Phase = CompilationPhase.Failed;
                        fileMetrics.DiagnosticCount = parser.Diagnostics.GetAll().Count;
                        _diagnostics.Merge(parser.Diagnostics);
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
                        if (stmt is ImportStatement import)
                            imports.Add(import);
                        else if (stmt is FromImportStatement fromImport)
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
