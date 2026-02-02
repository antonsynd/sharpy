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
/// Main compiler driver orchestrating the compilation pipeline
/// </summary>
public class Compiler
{
    private readonly ICompilerLogger _logger;
    private readonly ModuleRegistry? _moduleRegistry;

    public Compiler(ICompilerLogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
        _moduleRegistry = null;
    }

    public Compiler(CompilerOptions options, ICompilerLogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
        _moduleRegistry = new ModuleRegistry(_logger);

        // Add module search paths
        if (options.ModulePaths != null)
        {
            foreach (var path in options.ModulePaths)
            {
                _moduleRegistry.AddModulePath(path);
                _logger.LogDebug($"Added module search path: {path}");
            }
        }

        // Load referenced assemblies
        if (options.References != null)
        {
            foreach (var reference in options.References)
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
        var projectCompiler = new ProjectCompiler(_logger, _moduleRegistry);
        return projectCompiler.Compile(projectConfig, cancellationToken);
    }

    public CompilationResult Compile(string sourceCode, string filePath) =>
        Compile(sourceCode, filePath, CancellationToken.None);

    public CompilationResult Compile(string sourceCode, string filePath, CancellationToken cancellationToken)
    {
        _logger.LogInfo($"Starting compilation of {filePath}");
        var metrics = new CompilationMetrics(fileName: filePath);
        var diagnostics = new DiagnosticBag();

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
            sourceText = new SourceText(sourceCode, filePath);
            var lexer = new Lexer.Lexer(sourceText, _logger);
            tokens = lexer.TokenizeAll();
            metrics.EndPhase();

            // Assertion: Lexer must produce at least an EOF token
            Debug.Assert(tokens.Count > 0, "Lexer should produce at least one token (EOF)");

            // Check for lexer errors collected via DiagnosticBag
            if (lexer.Diagnostics.HasErrors)
            {
                diagnostics.Merge(lexer.Diagnostics);
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
            var parser = new Parser.Parser(tokens, _logger);
            module = parser.ParseModule();
            metrics.EndPhase();

            // Check if parser collected any errors into DiagnosticBag
            if (parser.Diagnostics.HasErrors)
            {
                diagnostics.Merge(parser.Diagnostics);
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
            AssertStatementsHaveSpans(module, diagnostics);
            assertionTimer.Stop();
            _logger.LogDebug($"Post-parse assertions completed in {assertionTimer.ElapsedMilliseconds}ms");

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
            var nameResolver = new NameResolver(symbolTable, _logger, semanticBinding);
            nameResolver.ResolveDeclarations(module);
            nameResolver.ResolveInheritance(); // Second pass: resolve inheritance after all types are declared
            metrics.EndPhase();

            // Assertions: After name resolution, verify symbol table integrity
            assertionTimer.Restart();
            AssertAllSymbolsHaveNames(symbolTable, diagnostics);
            AssertNoDuplicateTypeNames(symbolTable, diagnostics);
            assertionTimer.Stop();
            _logger.LogDebug($"Post-name-resolution assertions completed in {assertionTimer.ElapsedMilliseconds}ms");

            if (nameResolver.Diagnostics.HasErrors)
            {
                diagnostics.Merge(nameResolver.Diagnostics);
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
            var moduleSearchPaths = _moduleRegistry?.GetModulePaths()?.ToArray() ?? Array.Empty<string>();
            _logger.LogDebug($"Module search paths: [{string.Join(", ", moduleSearchPaths)}]");
            var moduleResolver = new ModuleResolver(_logger, moduleSearchPaths);
            importResolver = new ImportResolver(_logger, _moduleRegistry, moduleResolver);
            importResolver.SetSemanticBinding(semanticBinding);
            importResolver.SetCurrentModule(filePath);

            // Get the directory of the current file as the search path
            var currentDir = Path.GetDirectoryName(Path.GetFullPath(filePath));
            _logger.LogDebug($"Current directory for import resolution: {currentDir}");

            importResolver.ResolveAllImports(module, symbolTable, currentDir);

            // Resolve inheritance for imported types (transitive base types + imported type inheritance)
            var inheritanceResolver = new InheritanceResolver(symbolTable, _logger, semanticBinding);
            inheritanceResolver.ResolveAll(importResolver);

            // Materialize inheritance data onto Symbol properties, then verify and freeze
            semanticBinding.MaterializeInheritance();
            DualWriteAssertions.AssertInheritanceConsistency(symbolTable, semanticBinding);
            assertionTimer.Restart();
            AssertNoUnresolvedInheritance(symbolTable, diagnostics);
            assertionTimer.Stop();
            _logger.LogDebug($"Post-inheritance assertions completed in {assertionTimer.ElapsedMilliseconds}ms");
            semanticBinding.FreezeInheritance();

            metrics.EndPhase();

            if (importResolver.Diagnostics.HasErrors)
            {
                diagnostics.Merge(importResolver.Diagnostics);
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

            // Pass 2: Type resolution and type checking
            metrics.StartPhase("Type Resolution");
            var typeResolver = new TypeResolver(symbolTable, semanticInfo, _logger);
            metrics.EndPhase();

            metrics.StartPhase("Type Checking");
            var pipeline = ValidationPipelineFactory.CreateDefault(_logger);
            var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, _logger, pipeline)
            {
                CurrentFilePath = filePath,
                SemanticBinding = semanticBinding
            };
            try
            {
                // Single-file compilation is always an entry point
                typeChecker.CheckModule(module, computeCodeGenInfo: true, isEntryPoint: true);
            }
            catch (SemanticAnalysisException)
            {
                // Preserve all accumulated diagnostics from the type checker
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
            metrics.EndPhase();

            // Assertion: After successful type checking, warn if unknown types remain
            assertionTimer.Restart();
            WarnIfUnknownTypes(semanticInfo, typeChecker.Diagnostics);
            assertionTimer.Stop();
            _logger.LogDebug($"Post-type-checking assertions completed in {assertionTimer.ElapsedMilliseconds}ms");
            // Assertion: Type checking should have processed at least some expressions
            Debug.Assert(semanticInfo.ExpressionTypeCount > 0 || module.Body.Length == 0,
                "Type checker should record at least one expression type for non-empty modules");
            // Materialize CodeGenInfo and VariableType data onto Symbol properties, then verify and freeze
            semanticBinding.MaterializeCodeGenInfo();
            semanticBinding.MaterializeVariableTypes();
            DualWriteAssertions.AssertCodeGenInfoConsistency(symbolTable, semanticBinding);
            DualWriteAssertions.AssertVariableTypeConsistency(symbolTable, semanticBinding);
            semanticBinding.FreezeVariableTypes();
            semanticBinding.FreezeCodeGenInfo();

            // Always merge type checking/validation diagnostics so warnings are
            // available in CompilationResult even when compilation succeeds.
            diagnostics.Merge(typeChecker.Diagnostics);

            if (diagnostics.HasErrors)
            {
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

            // For single-file compilation, derive a namespace from the file name
            var defaultNamespace = !string.IsNullOrEmpty(filePath)
                ? Path.GetFileNameWithoutExtension(filePath)
                : null;

            var codeGenContext = new CodeGenContext(symbolTable, builtinRegistry)
            {
                SourceFilePath = filePath,
                // For single-file, we only set ProjectNamespace (not ProjectRootPath)
                // This tells the emitter to use a simple file-based namespace
                ProjectNamespace = !string.IsNullOrEmpty(defaultNamespace)
                    ? $"Sharpy.{ToPascalCase(defaultNamespace)}"
                    : null,
                // Single-file compilation is always an entry point - generate Main method
                IsEntryPoint = true,
                Logger = _logger,
                SemanticInfo = semanticInfo,
                SemanticBinding = semanticBinding
            };
            var emitter = new RoslynEmitter(codeGenContext);
            var compilationUnit = emitter.GenerateCompilationUnit(module);
            var csharpCode = compilationUnit.ToFullString();

            // Verify generated C# parses without syntax errors (always-on, not DEBUG-only)
            AssertGeneratedCSharpParses(csharpCode, diagnostics);

            // Check for code generation errors
            if (codeGenContext.HasErrors)
            {
                diagnostics.Merge(codeGenContext.Diagnostics);
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
                    codeGenContext.ProjectNamespace, diagnostics, semanticInfo);

                if (moduleCs != null)
                {
                    allGeneratedFiles[modulePath] = moduleCs;
                    _logger.LogInfo($"Generated C# for imported module: {Path.GetFileName(modulePath)}");
                }
            }

            metrics.EndPhase();

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
            _logger.LogError($"Compilation failed with exception: {ex.Message}", 0, 0);
            diagnostics.AddError($"Compilation failed: {ex.Message}", filePath: filePath, code: DiagnosticCodes.Infrastructure.CompilationFailed);
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

    // ----- Phase Boundary Assertions (always-on, emit diagnostics) -----

    /// <summary>
    /// Verify top-level statements have TextSpan populated.
    /// Emits SHP0904 if any statement is missing its span.
    /// </summary>
    internal static void AssertStatementsHaveSpans(Module module, DiagnosticBag diagnostics)
    {
        foreach (var stmt in module.Body)
        {
            // Import statements may not have spans (they're processed before codegen)
            if (stmt is ImportStatement or FromImportStatement)
                continue;

            if (!stmt.Span.HasValue)
            {
                diagnostics.AddWarning(
                    $"Internal invariant violation: statement {stmt.GetType().Name} at line {stmt.LineStart} is missing TextSpan. This is a compiler bug — please report it.",
                    stmt.LineStart, stmt.ColumnStart, code: DiagnosticCodes.Infrastructure.InvariantViolation,
                    phase: CompilerPhase.Unknown);
            }
        }
    }

    /// <summary>
    /// Verify all symbols in the global scope have non-empty names.
    /// Emits SHP0904 for any symbol with a null/empty name.
    /// </summary>
    internal static void AssertAllSymbolsHaveNames(SymbolTable symbolTable, DiagnosticBag diagnostics)
    {
        foreach (var symbol in symbolTable.GlobalScope.GetAllSymbols())
        {
            if (string.IsNullOrEmpty(symbol.Name))
            {
                diagnostics.AddWarning(
                    $"Internal invariant violation: symbol with kind {symbol.Kind} has null/empty name. This is a compiler bug — please report it.",
                    code: DiagnosticCodes.Infrastructure.InvariantViolation,
                    phase: CompilerPhase.NameResolution);
            }
        }
    }

    /// <summary>
    /// Verify no duplicate type definitions exist in the symbol table.
    /// NameResolver should have emitted errors for duplicates, but this checks
    /// the resulting symbol table is clean.
    /// </summary>
    internal static void AssertNoDuplicateTypeNames(SymbolTable symbolTable, DiagnosticBag diagnostics)
    {
        var typeNames = new HashSet<string>();
        foreach (var symbol in symbolTable.GlobalScope.GetAllSymbols().OfType<TypeSymbol>())
        {
            // Skip CLR types - multiple modules can legitimately re-export the same CLR type
            if (symbol.ClrType != null)
                continue;

            if (!typeNames.Add(symbol.Name))
            {
                diagnostics.AddWarning(
                    $"Internal invariant violation: duplicate type definition '{symbol.Name}' in symbol table after name resolution. This is a compiler bug — please report it.",
                    code: DiagnosticCodes.Infrastructure.InvariantViolation,
                    phase: CompilerPhase.NameResolution);
            }
        }
    }

    /// <summary>
    /// Verify all UnresolvedBaseName/UnresolvedInterfaceNames have been resolved
    /// after inheritance resolution. A dangling unresolved name means the inheritance
    /// resolver failed to find or match a type.
    /// </summary>
    internal static void AssertNoUnresolvedInheritance(SymbolTable symbolTable, DiagnosticBag diagnostics)
    {
        foreach (var symbol in symbolTable.GlobalScope.GetAllSymbols().OfType<TypeSymbol>())
        {
            // Skip CLR types - they don't go through our resolution pipeline
            if (symbol.ClrType != null)
                continue;

            // If UnresolvedBaseName is set but BaseType is still null, resolution failed
            if (symbol.UnresolvedBaseName != null && symbol.BaseType == null)
            {
                diagnostics.AddWarning(
                    $"Internal invariant violation: type '{symbol.Name}' has UnresolvedBaseName '{symbol.UnresolvedBaseName}' but BaseType is null after inheritance resolution. This is a compiler bug — please report it.",
                    code: DiagnosticCodes.Infrastructure.InvariantViolation,
                    phase: CompilerPhase.NameResolution);
            }

            // If UnresolvedInterfaceNames has entries but Interfaces count doesn't match
            if (symbol.UnresolvedInterfaceNames.Count > 0 && symbol.Interfaces.Count < symbol.UnresolvedInterfaceNames.Count)
            {
                diagnostics.AddWarning(
                    $"Internal invariant violation: type '{symbol.Name}' has {symbol.UnresolvedInterfaceNames.Count} unresolved interface names but only {symbol.Interfaces.Count} resolved interfaces after inheritance resolution. This is a compiler bug — please report it.",
                    code: DiagnosticCodes.Infrastructure.InvariantViolation,
                    phase: CompilerPhase.NameResolution);
            }
        }
    }

    /// <summary>
    /// Warn if unknown expression types remain after successful type checking.
    /// Unknown types are acceptable when there are semantic errors (error recovery),
    /// in cross-module scenarios where imported types may not be fully resolved,
    /// and in some class member access patterns where the type checker doesn't
    /// record types for all intermediate expressions.
    /// </summary>
    internal static void WarnIfUnknownTypes(SemanticInfo semanticInfo, DiagnosticBag diagnostics)
    {
        if (!diagnostics.HasErrors && semanticInfo.HasUnknownExpressionTypes())
        {
            // Invariant (aspirational): if SemanticInfo contains UnknownType entries,
            // DiagnosticBag should contain at least one error from the source of that Unknown.
            // Currently, some intermediate expressions (member access chains, CLR interop)
            // may produce Unknown without errors. This warning tracks those gaps.
            diagnostics.AddWarning(
                "Internal invariant violation: unknown expression types remain after type checking with no errors (possible resolution gap). This is a compiler bug — please report it.",
                code: DiagnosticCodes.Infrastructure.InvariantViolation,
                phase: CompilerPhase.TypeChecking);
        }
    }

    /// <summary>
    /// Verify generated C# code parses without syntax errors.
    /// This catches codegen bugs that produce malformed C#.
    /// Always-on (not DEBUG-only) because invalid generated C# in Release builds
    /// would produce cryptic Roslyn compilation errors instead of a clear
    /// "internal compiler error" diagnostic.
    /// </summary>
    private static void AssertGeneratedCSharpParses(string csharpCode, DiagnosticBag diagnostics)
    {
        var tree = CSharpSyntaxTree.ParseText(csharpCode);
        var parseDiagnostics = tree.GetDiagnostics()
            .Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .ToList();
        if (parseDiagnostics.Count > 0)
        {
            var details = string.Join("; ", parseDiagnostics.Take(3).Select(d => d.GetMessage()));
            diagnostics.AddError(
                $"Internal error: generated C# contains {parseDiagnostics.Count} syntax error(s): {details}. This is a compiler bug -- please report it.",
                code: DiagnosticCodes.CodeGen.InternalGeneratedCSharpParseError,
                phase: CompilerPhase.CodeGeneration);
        }
    }

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
    /// Generate C# code for a single module that has already been parsed and type-checked.
    /// Used for generating code for imported modules discovered during compilation.
    /// </summary>
    private string? GenerateCSharpForModule(
        ModuleInfo moduleInfo,
        SymbolTable symbolTable,
        BuiltinRegistry builtinRegistry,
        string? projectNamespace,
        DiagnosticBag diagnostics,
        SemanticInfo? semanticInfo = null)
    {
        if (moduleInfo.Module == null || moduleInfo.IsNetModule)
            return null;

        var codeGenContext = new CodeGenContext(symbolTable, builtinRegistry)
        {
            SourceFilePath = moduleInfo.Path,
            ProjectNamespace = projectNamespace,
            // Imported modules are NOT entry points - no Main method
            IsEntryPoint = false,
            Logger = _logger,
            SemanticInfo = semanticInfo
        };

        var emitter = new RoslynEmitter(codeGenContext);
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
    public ModuleRegistry? ModuleRegistry { get; init; }
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
    public ImportResolver? ImportResolver { get; init; }
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
    public Project.DependencyGraph? DependencyGraph { get; init; }

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
}
