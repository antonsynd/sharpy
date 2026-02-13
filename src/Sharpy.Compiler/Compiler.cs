using System.Diagnostics;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Parser;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Validation;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Services;
using Sharpy.Compiler.Text;
using Microsoft.CodeAnalysis.CSharp;

namespace Sharpy.Compiler;

/// <summary>
/// Main compiler driver orchestrating the compilation pipeline.
/// </summary>
/// <remarks>
/// <para>
/// This class is the primary public entry point for all compilation operations.
/// Use <see cref="Compile(string, string)"/> for single-file compilation and
/// <see cref="CompileProject(ProjectConfig)"/> for multi-file project compilation.
/// Both return comprehensive result objects (<see cref="CompilationResult"/> and
/// <see cref="ProjectCompilationResult"/>) that expose all intermediate artifacts
/// (tokens, AST, semantic info, generated C#, diagnostics) for tooling consumption.
/// </para>
/// <para>
/// Internal compiler components (<see cref="Lexer.Lexer"/>, <see cref="Parser.Parser"/>,
/// <see cref="Semantic.NameResolver"/>, <see cref="Semantic.TypeChecker"/>,
/// <see cref="CodeGen.RoslynEmitter"/>, etc.) should not be used directly by external
/// consumers. The only exception is diagnostic-only tools (e.g., <c>emit tokens</c>,
/// <c>emit ast</c>) that intentionally use only the lexer or parser stages.
/// </para>
/// </remarks>
public class Compiler
{
    private readonly ICompilerLogger _logger;
    private readonly ModuleRegistry? _moduleRegistry;
    private readonly CompilerOptions _options;

    // Phase timing for structured logging
    private readonly Stopwatch _phaseStopwatch = new();
    private string? _currentPhaseName;

    public Compiler(ICompilerLogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
        _moduleRegistry = null;
        _options = new CompilerOptions();
    }

    public Compiler(CompilerOptions options, ICompilerLogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
        _options = options ?? new CompilerOptions();
        _moduleRegistry = new ModuleRegistry(_logger);

        // Add module search paths
        if (_options.ModulePaths != null)
        {
            foreach (var path in _options.ModulePaths)
            {
                _moduleRegistry.AddModulePath(path);
                _logger.LogDebug($"Added module search path: {path}");
            }
        }

        // Load referenced assemblies
        if (_options.References != null)
        {
            foreach (var reference in _options.References)
            {
                var success = _moduleRegistry.LoadReference(reference);
                if (success)
                {
                    _logger.LogInfo($"Loaded module reference: {reference}");
                }
                else
                {
                    _logger.LogWarning($"Failed to load module reference: {reference}", 0, 0);
                }
            }
        }
    }

    /// <summary>
    /// Compile a Sharpy project from a .spyproj file
    /// </summary>
    public ProjectCompilationResult CompileProject(ProjectConfig projectConfig) =>
        CompileProject(projectConfig, CancellationToken.None);

    /// <summary>
    /// Compile a Sharpy project from a .spyproj file with cancellation support
    /// </summary>
    public ProjectCompilationResult CompileProject(ProjectConfig projectConfig, CancellationToken cancellationToken)
    {
        // Merge project-level and compiler-level warning/error settings
        var mergedSuppressed = new HashSet<string>(_options.SuppressedWarnings, StringComparer.OrdinalIgnoreCase);
        mergedSuppressed.UnionWith(projectConfig.SuppressedWarnings);
        var warnAsErrors = _options.WarningsAsErrors || projectConfig.WarningsAsErrors;

        var projectCompiler = new ProjectCompiler(_logger, _moduleRegistry,
            warnAsErrors, mergedSuppressed, _options.MaxErrors, _options.Incremental);
        return projectCompiler.Compile(projectConfig, cancellationToken);
    }

    public CompilationResult Compile(string sourceCode, string filePath) =>
        Compile(sourceCode, filePath, CancellationToken.None);

    public CompilationResult Compile(string sourceCode, string filePath, CancellationToken cancellationToken)
    {
        _logger.LogInfo($"Starting compilation of {filePath}");
        var metrics = new CompilationMetrics(fileName: filePath);
        var diagnostics = new DiagnosticBag(_options.WarningsAsErrors, _options.SuppressedWarnings);

        // Declare artifact variables outside the try block so they are accessible
        // in catch handlers. This ensures cancelled or crashed compilations still
        // return whatever artifacts were created before the failure point.
        SourceText? sourceText = null;
        List<Lexer.Token>? tokens = null;
        Module? module = null;
        SemanticBinding? semanticBinding = null;
        ImportResolver? importResolver = null;

        try
        {
            // Phase 1: Lexical Analysis
            _logger.LogInfo("Phase 1: Lexical Analysis");
            metrics.StartPhase("Lexical Analysis");
            LogPhaseStart("Lexical Analysis", filePath);
            sourceText = new SourceText(sourceCode, filePath);
            var lexer = new Lexer.Lexer(sourceText, _logger, cancellationToken: cancellationToken);
            if (_options.MaxErrors > 0)
            {
                lexer.MaxErrors = _options.MaxErrors;
            }
            tokens = lexer.TokenizeAll();
            LogPhaseEnd(filePath, lexer.Diagnostics.ErrorCount);
            metrics.EndPhase();

            // Capture token count immediately after lexing (available even if later phases fail)
            metrics.TokenCount = tokens.Count;

            // Assertion: Lexer must produce at least an EOF token
            Debug.Assert(tokens.Count > 0, "Lexer should produce at least one token (EOF)");

            // Check for lexer errors collected via DiagnosticBag
            if (lexer.Diagnostics.HasErrors)
            {
                diagnostics.Merge(lexer.Diagnostics);
                metrics.DiagnosticCount = diagnostics.GetAll().Count;
                return new CompilationResult
                {
                    Success = false,
                    Diagnostics = diagnostics,
                    Metrics = metrics,
                    SourceText = sourceText,
                    Tokens = tokens
                };
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Phase 2: Syntax Analysis
            _logger.LogInfo("Phase 2: Syntax Analysis");
            metrics.StartPhase("Syntax Analysis");
            LogPhaseStart("Syntax Analysis", filePath, tokens.Count);
            var parserMaxErrors = _options.MaxErrors > 0 ? _options.MaxErrors : 25;
            var parser = new Parser.Parser(tokens, _logger, parserMaxErrors, cancellationToken);
            module = parser.ParseModule();
            LogPhaseEnd(filePath, parser.Diagnostics.ErrorCount);
            metrics.EndPhase();

            // Capture AST node count immediately after parsing (available even if later phases fail)
            // This must be done before the error check so partial ASTs are counted
            if (module != null)
            {
                metrics.AstNodeCount = CountAstNodes(module);
            }

            // Check if parser collected any errors into DiagnosticBag
            if (parser.Diagnostics.HasErrors)
            {
                diagnostics.Merge(parser.Diagnostics);
                metrics.DiagnosticCount = diagnostics.GetAll().Count;
                return new CompilationResult
                {
                    Success = false,
                    Diagnostics = diagnostics,
                    Metrics = metrics,
                    SourceText = sourceText,
                    Tokens = tokens,
                    Module = module
                };
            }

            // Assertion: Parser must produce a valid module with span info
            Debug.Assert(module != null, "Parser should produce a non-null Module");
            Debug.Assert(module.Body != null, "Module.Body should not be null");
            var assertionTimer = Stopwatch.StartNew();
            CompilerInvariants.AssertPostParse(module, diagnostics);
            assertionTimer.Stop();
            _logger.LogDebug($"Post-parse assertions completed in {assertionTimer.ElapsedMilliseconds}ms");

            // Validate AST structural invariants (DEBUG-only, elided in Release)
            AstValidator.ValidateTree(module);

            cancellationToken.ThrowIfCancellationRequested();

            // Phase 3: Semantic Analysis
            _logger.LogInfo("Phase 3: Semantic Analysis");
            var builtinRegistry = new BuiltinRegistry(_logger);
            var symbolTable = new SymbolTable(builtinRegistry);
            var semanticInfo = new SemanticInfo();
            semanticBinding = new SemanticBinding();

            // Check for module registry errors
            if (_moduleRegistry != null && _moduleRegistry.Diagnostics.HasErrors)
            {
                diagnostics.Merge(_moduleRegistry.Diagnostics);
                metrics.DiagnosticCount = diagnostics.GetAll().Count;
                return new CompilationResult
                {
                    Success = false,
                    Diagnostics = diagnostics,
                    Metrics = metrics,
                    SourceText = sourceText,
                    Tokens = tokens,
                    Module = module,
                    SemanticBinding = semanticBinding
                };
            }

            // Pass 1: Name resolution (declarations)
            metrics.StartPhase("Name Resolution");
            LogPhaseStart("Name Resolution", filePath, module.Body.Length);
            var nameResolver = new NameResolver(symbolTable, _logger, semanticBinding);
            nameResolver.ResolveDeclarations(module, cancellationToken);
            nameResolver.ResolveInheritance(cancellationToken); // Second pass: resolve inheritance after all types are declared
            LogPhaseEnd(filePath, nameResolver.Diagnostics.ErrorCount);
            metrics.EndPhase();

            // Assertions: After name resolution, verify symbol table integrity
            assertionTimer.Restart();
            CompilerInvariants.AssertPostNameResolution(symbolTable, diagnostics);
            assertionTimer.Stop();
            _logger.LogDebug($"Post-name-resolution assertions completed in {assertionTimer.ElapsedMilliseconds}ms");

            if (nameResolver.Diagnostics.HasErrors)
            {
                diagnostics.Merge(nameResolver.Diagnostics);
                // Capture artifact counts even on error paths for better observability
                metrics.SymbolCount = symbolTable.GlobalScope.GetAllSymbols().Count();
                metrics.DiagnosticCount = diagnostics.GetAll().Count;
                return new CompilationResult
                {
                    Success = false,
                    Diagnostics = diagnostics,
                    Metrics = metrics,
                    SourceText = sourceText,
                    Tokens = tokens,
                    Module = module,
                    SemanticBinding = semanticBinding
                };
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Pass 1.5: Import resolution (resolves imports and registers symbols)
            metrics.StartPhase("Import Resolution");
            LogPhaseStart("Import Resolution", filePath);
            var moduleSearchPaths = _moduleRegistry?.GetModulePaths()?.ToArray() ?? Array.Empty<string>();
            _logger.LogDebug($"Module search paths: [{string.Join(", ", moduleSearchPaths)}]");
            var moduleResolver = new ModuleResolver(_logger, moduleSearchPaths);
            importResolver = new ImportResolver(_logger, _moduleRegistry, moduleResolver);
            importResolver.SetSemanticBinding(semanticBinding);
            importResolver.SetCurrentModule(filePath);

            // Get the directory of the current file as the search path
            var currentDir = Path.GetDirectoryName(Path.GetFullPath(filePath));
            _logger.LogDebug($"Current directory for import resolution: {currentDir}");

            importResolver.ResolveAllImports(module, symbolTable, currentDir, cancellationToken);

            // Resolve inheritance for imported types (transitive base types + imported type inheritance)
            var inheritanceResolver = new InheritanceResolver(symbolTable, _logger, semanticBinding);
            inheritanceResolver.ResolveAll(importResolver);

            // Materialize inheritance data onto Symbol properties, then verify and freeze
            semanticBinding.MaterializeInheritance();
            DualWriteAssertions.AssertInheritanceConsistency(symbolTable, semanticBinding);
            assertionTimer.Restart();
            CompilerInvariants.AssertPostInheritance(symbolTable, diagnostics);
            assertionTimer.Stop();
            _logger.LogDebug($"Post-inheritance assertions completed in {assertionTimer.ElapsedMilliseconds}ms");
            semanticBinding.FreezeInheritance();

            LogPhaseEnd(filePath, importResolver.Diagnostics.ErrorCount);
            metrics.EndPhase();

            // Always merge import diagnostics (errors + warnings) so they appear
            // in the final result. Continue to type checking even if imports failed,
            // so users see the full picture (import errors + type errors).
            if (importResolver.Diagnostics.GetAll().Count > 0)
            {
                diagnostics.Merge(importResolver.Diagnostics);
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Pass 2: Type resolution and type checking
            metrics.StartPhase("Type Resolution");
            LogPhaseStart("Type Resolution", filePath);
            var typeResolver = new TypeResolver(symbolTable, semanticInfo, _logger, cancellationToken);
            LogPhaseEnd(filePath);
            metrics.EndPhase();

            metrics.StartPhase("Type Checking");
            LogPhaseStart("Type Checking", filePath);
            var pipeline = ValidationPipelineFactory.CreateDefault(_logger);
            var semanticMaxErrors = _options.MaxErrors > 0 ? _options.MaxErrors : 100;
            var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, _logger, pipeline)
            {
                CurrentFilePath = filePath,
                SemanticBinding = semanticBinding,
                MaxErrors = semanticMaxErrors
            };

            // Import root causes from import resolution so TypeChecker can suppress cascading errors
            typeChecker.ImportRootCauses(diagnostics);

            var isEntryPoint = _options.OutputType.Equals("exe", StringComparison.OrdinalIgnoreCase);

            try
            {
                typeChecker.CheckModule(module, computeCodeGenInfo: true, isEntryPoint: isEntryPoint, cancellationToken);
            }
            catch (SemanticAnalysisException)
            {
                // Preserve all accumulated diagnostics from the type checker
                LogPhaseEnd(filePath, typeChecker.Diagnostics.ErrorCount);
                metrics.EndPhase();

                // Capture artifact counts even on error paths for better observability
                metrics.SymbolCount = symbolTable.GlobalScope.GetAllSymbols().Count();
                if (typeChecker.ValidatorTimes is Dictionary<string, TimeSpan> errorValidatorDict)
                {
                    metrics.SetValidatorTimes(errorValidatorDict);
                }
                metrics.DiagnosticCount = diagnostics.GetAll().Count + typeChecker.Diagnostics.GetAll().Count;

                diagnostics.Merge(typeChecker.Diagnostics);
                return new CompilationResult
                {
                    Success = false,
                    Diagnostics = diagnostics,
                    Metrics = metrics,
                    SourceText = sourceText,
                    Tokens = tokens,
                    Module = module,
                    SemanticBinding = semanticBinding,
                    ImportResolver = importResolver
                };
            }
            LogPhaseEnd(filePath, typeChecker.Diagnostics.ErrorCount);
            metrics.EndPhase();

            // Assertion: After successful type checking, warn if unknown types remain
            assertionTimer.Restart();
            CompilerInvariants.AssertPostTypeChecking(semanticInfo, typeChecker.Diagnostics);
            assertionTimer.Stop();
            _logger.LogDebug($"Post-type-checking assertions completed in {assertionTimer.ElapsedMilliseconds}ms");
            // Assertion: Type checking should have processed at least some expressions
            Debug.Assert(semanticInfo.ExpressionTypeCount > 0 || module.Body.Length == 0,
                "Type checker should record at least one expression type for non-empty modules");
            // Materialize CodeGenInfo and VariableType data onto Symbol properties, then verify and freeze
            semanticBinding.MaterializeCodeGenInfo();
            semanticBinding.MaterializeVariableTypes();
            assertionTimer.Restart();
            DualWriteAssertions.AssertCodeGenInfoConsistency(symbolTable, semanticBinding);
            DualWriteAssertions.AssertVariableTypeConsistency(symbolTable, semanticBinding);
            assertionTimer.Stop();
            _logger.LogDebug($"Post-materialization assertions completed in {assertionTimer.ElapsedMilliseconds}ms");
            semanticBinding.FreezeVariableTypes();
            semanticBinding.FreezeCodeGenInfo();

            // Capture symbol count and validator times after type checking
            // (available even if code generation fails)
            metrics.SymbolCount = symbolTable.GlobalScope.GetAllSymbols().Count();
            if (typeChecker.ValidatorTimes is Dictionary<string, TimeSpan> validatorDict)
            {
                metrics.SetValidatorTimes(validatorDict);
            }

            // Always merge type checking/validation diagnostics so warnings are
            // available in CompilationResult even when compilation succeeds.
            diagnostics.Merge(typeChecker.Diagnostics);

            if (diagnostics.HasErrors)
            {
                metrics.DiagnosticCount = diagnostics.GetAll().Count;
                return new CompilationResult
                {
                    Success = false,
                    Diagnostics = diagnostics,
                    Metrics = metrics,
                    SourceText = sourceText,
                    Tokens = tokens,
                    Module = module,
                    SemanticBinding = semanticBinding,
                    ImportResolver = importResolver
                };
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Phase 4: Code Generation - Generate C# code from AST using RoslynEmitter
            _logger.LogInfo("Phase 4: Code Generation");
            metrics.StartPhase("Code Generation");
            LogPhaseStart("Code Generation", filePath);

            var codeGenContext = new CodeGenContext(symbolTable, builtinRegistry)
            {
                SourceFilePath = filePath,
                // For single-file compilation, use global namespace (no namespace wrapper).
                // The file name becomes the module class name.
                ProjectNamespace = "",
                IsEntryPoint = isEntryPoint,
                Logger = _logger,
                SemanticInfo = semanticInfo,
                SemanticBinding = semanticBinding
            };
            var emitter = new RoslynEmitter(codeGenContext, cancellationToken);
            var compilationUnit = emitter.GenerateCompilationUnit(module);
            var csharpCode = compilationUnit.ToFullString();

            // Verify generated C# parses without syntax errors (always-on, not DEBUG-only)
            CompilerInvariants.AssertPostCodeGen(csharpCode, diagnostics);

            // Check for code generation errors
            if (codeGenContext.HasErrors)
            {
                diagnostics.Merge(codeGenContext.Diagnostics);
                metrics.DiagnosticCount = diagnostics.GetAll().Count;
                return new CompilationResult
                {
                    Success = false,
                    Diagnostics = diagnostics,
                    Metrics = metrics,
                    SourceText = sourceText,
                    Tokens = tokens,
                    Module = module,
                    SemanticBinding = semanticBinding,
                    ImportResolver = importResolver
                };
            }

            // Generate C# for all imported .spy modules
            var allGeneratedFiles = new Dictionary<string, string>();

            // Add entry file
            allGeneratedFiles[filePath] = csharpCode;

            // Add all imported modules
            foreach (var (modulePath, moduleInfo) in importResolver.LoadedSpyModules)
            {
                // Skip the entry file (already added)
                if (string.Equals(Path.GetFullPath(modulePath), Path.GetFullPath(filePath),
                    StringComparison.OrdinalIgnoreCase))
                    continue;

                var moduleCs = GenerateCSharpForModule(
                    moduleInfo, symbolTable, builtinRegistry,
                    codeGenContext.ProjectNamespace, diagnostics, semanticInfo, semanticBinding,
                    cancellationToken);

                if (moduleCs != null)
                {
                    allGeneratedFiles[modulePath] = moduleCs;
                    _logger.LogInfo($"Generated C# for imported module: {Path.GetFileName(modulePath)}");
                }
            }

            // Emit CodeGenEvent with the size of generated code
            if (_logger.SupportsStructuredLogging)
            {
                var totalBytes = allGeneratedFiles.Values.Sum(cs => System.Text.Encoding.UTF8.GetByteCount(cs));
                _logger.LogEvent(new CodeGenEvent("CSharp", totalBytes) { FilePath = filePath });
            }

            LogPhaseEnd(filePath, codeGenContext.Diagnostics.ErrorCount);
            metrics.EndPhase();

            // Update diagnostic count with final value
            // (TokenCount, AstNodeCount, SymbolCount, ValidatorTimes were set incrementally above)
            metrics.DiagnosticCount = diagnostics.GetAll().Count;

            return new CompilationResult
            {
                Success = !diagnostics.HasErrors,
                Diagnostics = diagnostics,
                Module = module,
                SymbolTable = symbolTable,
                SemanticInfo = semanticInfo,
                ModuleRegistry = _moduleRegistry,
                GeneratedCSharpCode = csharpCode,  // Keep for backward compatibility
                GeneratedCSharpFiles = allGeneratedFiles,
                Metrics = metrics,
                SourceText = sourceText,
                Tokens = tokens,
                SemanticBinding = semanticBinding,
                ImportResolver = importResolver
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogInfo("Compilation cancelled");
            diagnostics.AddError("Compilation cancelled", filePath: filePath, code: DiagnosticCodes.Infrastructure.CompilationCancelled);
            return new CompilationResult
            {
                Success = false,
                Diagnostics = diagnostics,
                Metrics = metrics,
                SourceText = sourceText,
                Tokens = tokens,
                Module = module,
                SemanticBinding = semanticBinding,
                ImportResolver = importResolver
            };
        }
        catch (Exception ex)
        {
            // Log full exception including stack trace for debugging
            _logger.LogError($"Compilation failed with {ex.GetType().Name}: {ex}", 0, 0);

            // Create a user-facing error message that includes exception type for identification
            var errorMessage = ex is InternalCompilerErrorException ice
                ? $"Internal compiler error in {ice.Component} ({ex.GetType().Name}): {ex.Message}"
                : $"Compilation failed ({ex.GetType().Name}): {ex.Message}";

            diagnostics.AddError(errorMessage, filePath: filePath, code: DiagnosticCodes.Infrastructure.CompilationFailed);
            return new CompilationResult
            {
                Success = false,
                Diagnostics = diagnostics,
                Metrics = metrics,
                SourceText = sourceText,
                Tokens = tokens,
                Module = module,
                SemanticBinding = semanticBinding,
                ImportResolver = importResolver
            };
        }
    }

    // ----- Phase Boundary Assertions (backward-compatible wrappers) -----
    // These methods delegate to CompilerInvariants for backward compatibility
    // with tests that directly call Compiler.AssertXxx() methods.

    /// <summary>
    /// Verify top-level statements have TextSpan populated.
    /// Emits SPY0904 if any statement is missing its span.
    /// </summary>
    /// <remarks>
    /// This method delegates to <see cref="CompilerInvariants.AssertStatementsHaveSpans"/>.
    /// Kept for backward compatibility with existing tests.
    /// </remarks>
    internal static void AssertStatementsHaveSpans(Module module, DiagnosticBag diagnostics)
        => CompilerInvariants.AssertStatementsHaveSpans(module, diagnostics);

    /// <summary>
    /// Verify all symbols in the global scope have non-empty names.
    /// Emits SPY0904 for any symbol with a null/empty name.
    /// </summary>
    /// <remarks>
    /// This method delegates to <see cref="CompilerInvariants.AssertAllSymbolsHaveNames"/>.
    /// Kept for backward compatibility with existing tests.
    /// </remarks>
    internal static void AssertAllSymbolsHaveNames(SymbolTable symbolTable, DiagnosticBag diagnostics)
        => CompilerInvariants.AssertAllSymbolsHaveNames(symbolTable, diagnostics);

    /// <summary>
    /// Verify no duplicate type definitions exist in the symbol table.
    /// </summary>
    /// <remarks>
    /// This method delegates to <see cref="CompilerInvariants.AssertNoDuplicateTypeNames"/>.
    /// Kept for backward compatibility with existing tests.
    /// </remarks>
    internal static void AssertNoDuplicateTypeNames(SymbolTable symbolTable, DiagnosticBag diagnostics)
        => CompilerInvariants.AssertNoDuplicateTypeNames(symbolTable, diagnostics);

    /// <summary>
    /// Verify all UnresolvedBaseName/UnresolvedInterfaceNames have been resolved.
    /// </summary>
    /// <remarks>
    /// This method delegates to <see cref="CompilerInvariants.AssertNoUnresolvedInheritance"/>.
    /// Kept for backward compatibility with existing tests.
    /// </remarks>
    internal static void AssertNoUnresolvedInheritance(SymbolTable symbolTable, DiagnosticBag diagnostics)
        => CompilerInvariants.AssertNoUnresolvedInheritance(symbolTable, diagnostics);

    /// <summary>
    /// Error if unexpected unknown expression types remain after successful type checking.
    /// Unknown types from error recovery are expected; others indicate compiler bugs (SPY0907).
    /// </summary>
    /// <remarks>
    /// This method delegates to <see cref="CompilerInvariants.WarnIfUnknownTypes"/>.
    /// Kept for backward compatibility with existing tests.
    /// </remarks>
    internal static void WarnIfUnknownTypes(SemanticInfo semanticInfo, DiagnosticBag diagnostics)
        => CompilerInvariants.WarnIfUnknownTypes(semanticInfo, diagnostics);

    /// <summary>
    /// Create CompilerServices from compilation state.
    /// </summary>
    private CompilerServices CreateServices(
        SymbolTable symbolTable,
        SemanticInfo semanticInfo,
        TypeResolver typeResolver,
        ClrMemberCache? clrCache = null)
    {
        return new CompilerServicesBuilder()
            .WithLogger(_logger)
            .WithSymbolTable(symbolTable)
            .WithSemanticInfo(semanticInfo)
            .WithTypeResolver(typeResolver)
            .WithClrCache(clrCache ?? new ClrMemberCache())
            .Build();
    }

    // ----- Structured Logging Helpers -----

    /// <summary>
    /// Starts tracking a compilation phase for structured logging.
    /// Emits a PhaseStartEvent if the logger supports structured logging.
    /// </summary>
    private void LogPhaseStart(string phaseName, string? filePath = null, int nodeCount = 0)
    {
        _currentPhaseName = phaseName;
        _phaseStopwatch.Restart();

        if (_logger.SupportsStructuredLogging)
        {
            _logger.LogEvent(new PhaseStartEvent(phaseName, nodeCount) { FilePath = filePath });
        }
    }

    /// <summary>
    /// Ends tracking the current compilation phase for structured logging.
    /// Emits a PhaseEndEvent if the logger supports structured logging.
    /// </summary>
    private void LogPhaseEnd(string? filePath = null, int errorCount = 0)
    {
        _phaseStopwatch.Stop();

        if (_logger.SupportsStructuredLogging && _currentPhaseName != null)
        {
            _logger.LogEvent(new PhaseEndEvent(_currentPhaseName, _phaseStopwatch.Elapsed, errorCount) { FilePath = filePath });
        }

        _currentPhaseName = null;
    }

    /// <summary>
    /// Simple PascalCase conversion for file names to namespace components.
    /// Handles snake_case, kebab-case, and ensures valid C# identifiers.
    /// </summary>
    private static string ToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        // Replace invalid identifier characters with underscores
        var sanitized = new System.Text.StringBuilder(name.Length);
        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
                sanitized.Append(c);
            else
                sanitized.Append('_');
        }

        // Split by underscore and capitalize each part
        var parts = sanitized.ToString().Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return "_";

        var result = string.Join("", parts.Select(p =>
            char.ToUpperInvariant(p[0]) + (p.Length > 1 ? p[1..] : "")
        ));

        // If result starts with a digit, prefix with underscore
        if (result.Length > 0 && char.IsDigit(result[0]))
        {
            result = "_" + result;
        }

        return result;
    }

    /// <summary>
    /// Counts the total number of AST nodes in a module for metrics.
    /// This provides a rough measure of program complexity.
    /// Uses the AST nodes' GetChildNodes() method for recursive traversal.
    /// </summary>
    private static int CountAstNodes(Parser.Ast.Module module)
    {
        var count = 1; // Count the module itself
        var stack = new Stack<Parser.Ast.Node>();

        // Initialize stack with module body
        foreach (var statement in module.Body)
        {
            stack.Push(statement);
        }

        // Iterative depth-first traversal (more efficient than recursion for large ASTs)
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            count++;

            // Push all children onto the stack
            foreach (var child in node.GetChildNodes())
            {
                stack.Push(child);
            }
        }

        return count;
    }

    /// <summary>
    /// Generate C# code for a single module that has already been parsed and type-checked.
    /// Used for generating code for imported modules discovered during compilation.
    /// </summary>
    private string? GenerateCSharpForModule(
        ModuleInfo moduleInfo,
        SymbolTable symbolTable,
        BuiltinRegistry builtinRegistry,
        string? projectNamespace,
        DiagnosticBag diagnostics,
        SemanticInfo? semanticInfo = null,
        SemanticBinding? semanticBinding = null,
        CancellationToken cancellationToken = default)
    {
        if (moduleInfo.Module == null || moduleInfo.IsNetModule)
            return null;

        // Register the module's own exported symbols into the SymbolTable so that
        // same-module references (e.g., ValidationResult(...) inside validators.spy)
        // can be resolved during code generation. In the single-file compilation path,
        // only explicitly imported symbols are in the SymbolTable; the module's own
        // types are stored in ModuleInfo.ExportedSymbols by ModuleLoader.
        var addedSymbols = new List<string>();
        foreach (var (name, sym) in moduleInfo.ExportedSymbols)
        {
            if (symbolTable.Lookup(name, searchParents: false) == null)
            {
                symbolTable.TryDefine(sym);
                addedSymbols.Add(name);
            }
        }

        var codeGenContext = new CodeGenContext(symbolTable, builtinRegistry)
        {
            SourceFilePath = moduleInfo.Path,
            ProjectNamespace = projectNamespace,
            // Imported modules are NOT entry points - no Main method
            IsEntryPoint = false,
            Logger = _logger,
            SemanticInfo = semanticInfo,
            SemanticBinding = semanticBinding ?? new SemanticBinding()
        };

        var emitter = new RoslynEmitter(codeGenContext, cancellationToken);
        var compilationUnit = emitter.GenerateCompilationUnit(moduleInfo.Module);

        if (codeGenContext.HasErrors)
        {
            diagnostics.Merge(codeGenContext.Diagnostics);
            return null;
        }

        return compilationUnit.ToFullString();
    }
}

/// <summary>
/// Result of compilation including success status, errors, and generated artifacts
/// </summary>
public class CompilationResult
{
    public bool Success { get; init; }

    /// <summary>
    /// Structured diagnostics from all compilation phases.
    /// This is the primary way to access errors, warnings, and other diagnostics.
    /// </summary>
    public DiagnosticBag Diagnostics { get; init; } = new();

    public Module? Module { get; init; }
    public SymbolTable? SymbolTable { get; init; }
    public SemanticInfo? SemanticInfo { get; init; }
    public ISemanticQuery? SemanticQuery => SemanticInfo;
    internal ModuleRegistry? ModuleRegistry { get; init; }
    public string? GeneratedCSharpCode { get; init; }

    /// <summary>
    /// All generated C# code files (entry point + all imported modules).
    /// Key is the source file path, value is the generated C# code.
    /// </summary>
    public Dictionary<string, string> GeneratedCSharpFiles { get; init; } = new();

    public CompilationMetrics? Metrics { get; init; }

    /// <summary>
    /// The source text used for compilation.
    /// Available for tooling that needs structured source access (e.g., LSP, diagnostic rendering).
    /// </summary>
    public Text.SourceText? SourceText { get; init; }

    /// <summary>
    /// The token list produced by the lexer.
    /// Available for tooling that needs token-level access (e.g., syntax highlighting, LSP).
    /// </summary>
    public IReadOnlyList<Lexer.Token>? Tokens { get; init; }

    /// <summary>
    /// The semantic binding data from semantic analysis.
    /// Available for tooling that needs semantic information (e.g., LSP go-to-definition, hover).
    /// </summary>
    public SemanticBinding? SemanticBinding { get; init; }

    /// <summary>
    /// The import resolver with loaded module information.
    /// Available for tooling that needs resolved module info (e.g., LSP go-to-definition across modules).
    /// </summary>
    internal ImportResolver? ImportResolver { get; init; }

    /// <summary>
    /// Read-only query interface for import resolution information.
    /// </summary>
    public IImportQuery? Imports => ImportResolver != null ? new ImportQueryAdapter(ImportResolver) : null;
}

/// <summary>
/// Result of project compilation
/// </summary>
public class ProjectCompilationResult
{
    public bool Success { get; init; }

    /// <summary>
    /// Structured diagnostics from all compilation phases.
    /// This is the primary way to access errors, warnings, and other diagnostics.
    /// </summary>
    public DiagnosticBag Diagnostics { get; init; } = new();

    public string? OutputAssemblyPath { get; init; }
    public Dictionary<string, string> GeneratedCSharpFiles { get; init; } = new();
    public ProjectCompilationMetrics? Metrics { get; init; }

    /// <summary>
    /// The dependency graph built during compilation.
    /// Available for tooling/analysis (e.g., incremental compilation, build order visualization).
    /// </summary>
    internal Project.DependencyGraph? DependencyGraph { get; init; }

    /// <summary>
    /// Read-only query interface for file dependency information.
    /// </summary>
    public IDependencyQuery? Dependencies => DependencyGraph;

    /// <summary>
    /// The ProjectModel containing all CompilationUnits.
    /// Available for tooling and analysis.
    /// </summary>
    public Model.ProjectModel? ProjectModel { get; init; }
}

/// <summary>
/// Options for configuring the compiler
/// </summary>
public class CompilerOptions
{
    /// <summary>
    /// Paths to search for module assemblies
    /// </summary>
    public string[]? ModulePaths { get; set; }

    /// <summary>
    /// Paths to .NET assemblies to reference
    /// </summary>
    public string[]? References { get; set; }

    /// <summary>
    /// Treat all warnings as errors. When true, any warning causes compilation
    /// to report failure (warnings are promoted to error severity).
    /// </summary>
    public bool WarningsAsErrors { get; set; }

    /// <summary>
    /// Warning codes to suppress (e.g., "SPY0451", "SPY0452").
    /// Suppressed warnings are silently discarded and do not appear in diagnostics.
    /// </summary>
    public HashSet<string> SuppressedWarnings { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Maximum number of errors before the compiler stops reporting.
    /// Applies to both parser and semantic analysis.
    /// Default: 0 (use component defaults: 25 for parser, 100 for semantic).
    /// </summary>
    public int MaxErrors { get; set; }

    /// <summary>
    /// Enable incremental compilation. When true, only files that have changed
    /// (or whose dependencies have changed) are recompiled. File content hashes
    /// are cached in the project's obj/ directory.
    /// </summary>
    public bool Incremental { get; set; }

    /// <summary>
    /// Output type: "exe" or "library". Controls whether the compiler requires
    /// a main() entry point and generates a Main method.
    /// Default: "exe" (entry point required).
    /// </summary>
    public string OutputType { get; set; } = "exe";
}
