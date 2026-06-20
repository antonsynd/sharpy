using System.Diagnostics;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Services;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Semantic.Validation;
using Sharpy.Compiler.Text;

namespace Sharpy.Compiler;

/// <summary>
/// Encapsulates the per-file compilation phases that are shared between
/// single-file (<see cref="Compiler"/>) and multi-file (<see cref="Project.ProjectCompiler"/>) compilation.
///
/// This class handles:
/// <list type="bullet">
///   <item>Lexical analysis (static — no instance state needed)</item>
///   <item>Syntax analysis / parsing (static — no instance state needed)</item>
///   <item>Name resolution (declarations + inheritance)</item>
///   <item>Import resolution and imported-type inheritance materialization</item>
///   <item>Type resolution and type checking (with <see cref="SemanticAnalysisException"/> handling)</item>
///   <item>Type checking of imported .spy modules</item>
///   <item>Post-type-checking materialization of CodeGenInfo and VariableTypes</item>
///   <item>Code generation via RoslynEmitter</item>
/// </list>
/// </summary>
internal class FileCompilationPipeline
{
    private readonly SymbolTable _symbolTable;
    private readonly SemanticInfo _semanticInfo;
    private readonly SemanticBinding _semanticBinding;
    private readonly ICompilerLogger _logger;
    private readonly ICodeEmitterFactory _emitterFactory;

    public FileCompilationPipeline(
        SymbolTable symbolTable,
        SemanticInfo semanticInfo,
        SemanticBinding semanticBinding,
        ICompilerLogger logger,
        ICodeEmitterFactory? emitterFactory = null)
    {
        _symbolTable = symbolTable;
        _semanticInfo = semanticInfo;
        _semanticBinding = semanticBinding;
        _logger = logger;
        _emitterFactory = emitterFactory ?? new RoslynEmitterFactory();
    }

    /// <summary>
    /// Resolves inheritance for imported types (transitive base types from external modules),
    /// then materializes and freezes inheritance data on symbols.
    /// Call this after both name resolution and import resolution have completed.
    /// </summary>
    public void ResolveImportedInheritanceAndMaterialize(ImportResolver importResolver)
    {
        var inheritanceResolver = new InheritanceResolver(_symbolTable, _logger, _semanticBinding);
        inheritanceResolver.ResolveAll(importResolver);

        _semanticBinding.MaterializeInheritance();
        DualWriteAssertions.AssertInheritanceConsistency(_symbolTable, _semanticBinding);
        _semanticBinding.FreezeInheritance();
        _semanticBinding.FreezeNetModules();
    }

    /// <summary>
    /// Creates a configured <see cref="TypeChecker"/> for a single file, runs type checking,
    /// and handles <see cref="SemanticAnalysisException"/> for early abort on too many errors.
    /// </summary>
    /// <returns>
    /// The <see cref="TypeChecker"/> instance with populated diagnostics and validator times.
    /// The caller is responsible for merging diagnostics and recording metrics.
    /// </returns>
    public TypeCheckResult TypeCheck(
        Module module,
        string filePath,
        bool isEntryPoint,
        int maxErrors,
        DiagnosticBag existingDiagnostics,
        bool computeCodeGenInfo = true,
        CancellationToken cancellationToken = default,
        SemanticInfo? fileSemanticInfo = null,
        SemanticBinding? fileSemanticBinding = null,
        IReadOnlySet<string>? deferredCycleSymbols = null,
        IReadOnlySet<string>? deferredCycleFiles = null,
        ModuleRegistry? moduleRegistry = null)
    {
        var effectiveSemanticInfo = fileSemanticInfo ?? _semanticInfo;
        var effectiveBinding = fileSemanticBinding ?? _semanticBinding;
        var typeResolver = new TypeResolver(_symbolTable, effectiveSemanticInfo, _logger, cancellationToken);
        var pipeline = ValidationPipelineFactory.CreateDefault(_logger);
        var semanticMaxErrors = maxErrors > 0 ? maxErrors : 100;
        var typeChecker = new TypeChecker(_symbolTable, effectiveSemanticInfo, typeResolver, _logger, pipeline)
        {
            CurrentFilePath = filePath,
            SemanticBinding = effectiveBinding,
            MaxErrors = semanticMaxErrors,
            DeferredCycleSymbols = deferredCycleSymbols,
            DeferredCycleFiles = deferredCycleFiles,
            ModuleRegistry = moduleRegistry
        };

        // Import root causes so TypeChecker can suppress cascading errors
        typeChecker.ImportRootCauses(existingDiagnostics);

        bool aborted = false;
        try
        {
            typeChecker.CheckModule(module, computeCodeGenInfo, isEntryPoint, cancellationToken);
        }
        catch (SemanticAnalysisException)
        {
            aborted = true;
        }

        return new TypeCheckResult(typeChecker, aborted);
    }

    /// <summary>
    /// Materializes CodeGenInfo and VariableTypes onto symbol properties, verifies
    /// consistency via dual-write assertions, and freezes the data.
    /// Call this after all type checking has completed successfully.
    /// </summary>
    public void MaterializeTypeInfo()
    {
        _semanticBinding.MaterializeCodeGenInfo();
        _semanticBinding.MaterializeVariableTypes();
        DualWriteAssertions.AssertCodeGenInfoConsistency(_symbolTable, _semanticBinding);
        DualWriteAssertions.AssertVariableTypeConsistency(_symbolTable, _semanticBinding);
        _semanticBinding.FreezeVariableTypes();
        _semanticBinding.FreezeCodeGenInfo();
    }

    // ----- Static Phase Methods (no instance state) -----

    /// <summary>
    /// Phase 1: Lexical analysis. Tokenizes source code into a token stream.
    /// </summary>
    public static LexResult Lex(
        SourceText sourceText, ICompilerLogger logger, int maxErrors = 0,
        CancellationToken cancellationToken = default, bool preserveTrivia = false)
    {
        var lexer = new Lexer.Lexer(sourceText, logger, cancellationToken: cancellationToken, preserveTrivia: preserveTrivia);
        if (maxErrors > 0)
        {
            lexer.MaxErrors = maxErrors;
        }
        var tokens = lexer.TokenizeAll();

        Debug.Assert(tokens.Count > 0, "Lexer should produce at least one token (EOF)");

        return new LexResult(tokens, lexer.Diagnostics);
    }

    /// <summary>
    /// Phase 2: Syntax analysis. Parses tokens into an AST Module.
    /// </summary>
    public static PipelineParseResult Parse(
        IReadOnlyList<Token> tokens, ICompilerLogger logger, int maxErrors = 25,
        CancellationToken cancellationToken = default)
    {
        var tokenList = tokens as List<Token> ?? new List<Token>(tokens);
        var parser = new Parser.Parser(tokenList, logger, maxErrors, cancellationToken);
        var module = parser.ParseModule();

        if (module != null)
        {
            AstValidator.ValidateTree(module);
        }

        return new PipelineParseResult(module, parser.Diagnostics);
    }

    // ----- Instance Phase Methods (need semantic state) -----

    /// <summary>
    /// Phase 3a: Name resolution. Collects top-level declarations into the symbol table.
    /// Inheritance resolution is deferred to after imports are resolved.
    /// </summary>
    public NameResolveResult ResolveNames(Module module, CancellationToken cancellationToken = default)
    {
        var nameResolver = new NameResolver(_symbolTable, _logger, _semanticBinding);
        nameResolver.ResolveDeclarations(module, cancellationToken);
        return new NameResolveResult(nameResolver);
    }

    /// <summary>
    /// Phase 3b: Import resolution. Resolves imports, registers symbols, resolves
    /// inheritance (after imports so imported base types are available), and
    /// materializes inheritance data.
    /// </summary>
    public ImportResolveResult ResolveImports(
        Module module,
        NameResolver nameResolver,
        string filePath,
        ModuleRegistry? moduleRegistry,
        CancellationToken cancellationToken = default)
    {
        var moduleSearchPaths = moduleRegistry?.GetModulePaths()?.ToArray() ?? Array.Empty<string>();
        var moduleResolver = new ModuleResolver(_logger, moduleSearchPaths);
        var importResolver = new ImportResolver(_logger, moduleRegistry, moduleResolver,
            semanticBinding: _semanticBinding);

        var currentDir = Path.GetDirectoryName(Path.GetFullPath(filePath));
        importResolver.ResolveAllImports(module, _symbolTable, currentDir, cancellationToken,
            currentModulePath: filePath);

        // Resolve inheritance after imports so imported base types are available
        nameResolver.ResolveInheritance(cancellationToken);
        ResolveImportedInheritanceAndMaterialize(importResolver);

        return new ImportResolveResult(importResolver);
    }

    /// <summary>
    /// Type-check imported .spy modules so that SemanticInfo is populated for
    /// all their expressions. Without this, GetExpressionSemanticType() returns
    /// null for cross-module AST nodes during codegen.
    /// </summary>
    public void TypeCheckImportedModules(
        ImportResolver importResolver,
        string entryFilePath,
        DiagnosticBag diagnostics,
        CancellationToken cancellationToken = default,
        ModuleRegistry? moduleRegistry = null)
    {
        foreach (var (modulePath, moduleInfo) in importResolver.LoadedSpyModules)
        {
            if (string.Equals(Path.GetFullPath(modulePath), Path.GetFullPath(entryFilePath),
                StringComparison.OrdinalIgnoreCase))
                continue;

            if (moduleInfo.IsNetModule || moduleInfo.IsErrorRecovery || moduleInfo.Module == null)
                continue;

            // Temporarily register the module's own exported symbols so that
            // same-module references can be resolved during type checking.
            var addedSymbols = new List<string>();
            foreach (var (name, sym) in moduleInfo.ExportedSymbols)
            {
                if (_symbolTable.Lookup(name, searchParents: false) == null)
                {
                    _symbolTable.TryDefine(sym);
                    addedSymbols.Add(name);
                }
            }

            var moduleTypeResolver = new TypeResolver(_symbolTable, _semanticInfo, _logger, cancellationToken);
            var modulePipeline = ValidationPipelineFactory.CreateDefault(_logger);
            var moduleTypeChecker = new TypeChecker(
                _symbolTable, _semanticInfo, moduleTypeResolver, _logger, modulePipeline)
            {
                CurrentFilePath = modulePath,
                SemanticBinding = _semanticBinding,
                ContinueAfterError = true,
                ModuleRegistry = moduleRegistry
            };
            moduleTypeChecker.CheckModule(
                moduleInfo.Module,
                computeCodeGenInfo: true,
                isEntryPoint: false,
                cancellationToken);

            // Clean up temporarily added symbols
            foreach (var name in addedSymbols)
            {
                _symbolTable.Remove(name);
            }

            // Only merge warnings from imported module type-checking. Errors are
            // expected when the module has its own imports not in our SymbolTable.
            foreach (var diag in moduleTypeChecker.Diagnostics.GetAll())
            {
                if (diag.Severity != CompilerDiagnosticSeverity.Error)
                {
                    diagnostics.Add(diag);
                }
            }
        }
    }

    /// <summary>
    /// Phase 4: Code generation. Generates C# for the entry file and all imported modules.
    /// </summary>
    public CodeGenResult GenerateCode(
        Module module,
        string filePath,
        ImportResolver importResolver,
        BuiltinRegistry builtinRegistry,
        bool isEntryPoint,
        string projectNamespace,
        ICompilerLogger logger,
        CancellationToken cancellationToken = default)
    {
        var codeGenContext = new CodeGenContext(_symbolTable, builtinRegistry)
        {
            SourceFilePath = filePath,
            ProjectNamespace = projectNamespace,
            IsEntryPoint = isEntryPoint,
            Logger = logger,
            SemanticInfo = _semanticInfo,
            SemanticBinding = _semanticBinding
        };
        var emitter = _emitterFactory.Create(codeGenContext, cancellationToken);
        var compilationUnit = emitter.GenerateCompilationUnit(module);
        var csharpCode = compilationUnit.ToFullString();

        if (codeGenContext.EmitLineDirectives)
        {
            csharpCode = LineDirectivePostProcessor.Process(csharpCode);
        }

        var diagnostics = new DiagnosticBag();

        // Verify generated C# parses without syntax errors
        CompilerInvariants.AssertPostCodeGen(csharpCode, diagnostics);

        if (codeGenContext.HasErrors)
        {
            diagnostics.Merge(codeGenContext.Diagnostics);
            return new CodeGenResult(csharpCode, new Dictionary<string, string>(), diagnostics);
        }

        // Generate C# for all imported .spy modules
        var allGeneratedFiles = new Dictionary<string, string> { [filePath] = csharpCode };

        foreach (var (modulePath, moduleInfo) in importResolver.LoadedSpyModules)
        {
            if (string.Equals(Path.GetFullPath(modulePath), Path.GetFullPath(filePath),
                StringComparison.OrdinalIgnoreCase))
                continue;

            var moduleCs = GenerateCSharpForModule(
                moduleInfo, builtinRegistry, projectNamespace,
                logger, cancellationToken);

            if (moduleCs != null)
            {
                allGeneratedFiles[modulePath] = moduleCs;
            }
            // If moduleCs is null for a .spy module, codegen failed for that module.
            // Errors are silently skipped — matching ProjectCompiler behavior.
        }

        return new CodeGenResult(csharpCode, allGeneratedFiles, diagnostics);
    }

    /// <summary>
    /// Generate C# code for a single imported module.
    /// </summary>
    private string? GenerateCSharpForModule(
        ModuleInfo moduleInfo,
        BuiltinRegistry builtinRegistry,
        string? projectNamespace,
        ICompilerLogger logger,
        CancellationToken cancellationToken = default)
    {
        if (moduleInfo.Module == null || moduleInfo.IsNetModule)
            return null;

        // Register the module's own exported symbols into the SymbolTable so that
        // same-module references can be resolved during code generation.
        var addedSymbols = new List<string>();
        foreach (var (name, sym) in moduleInfo.ExportedSymbols)
        {
            if (_symbolTable.Lookup(name, searchParents: false) == null)
            {
                _symbolTable.TryDefine(sym);
                addedSymbols.Add(name);
            }
        }

        var codeGenContext = new CodeGenContext(_symbolTable, builtinRegistry)
        {
            SourceFilePath = moduleInfo.Path,
            ProjectNamespace = projectNamespace,
            IsEntryPoint = false,
            Logger = logger,
            SemanticInfo = _semanticInfo,
            SemanticBinding = _semanticBinding
        };

        var emitter = _emitterFactory.Create(codeGenContext, cancellationToken);
        var compilationUnit = emitter.GenerateCompilationUnit(moduleInfo.Module);

        // Clean up temporarily added symbols
        foreach (var name in addedSymbols)
        {
            _symbolTable.Remove(name);
        }

        if (codeGenContext.HasErrors)
            return null;

        var csharpCode = compilationUnit.ToFullString();
        if (codeGenContext.EmitLineDirectives)
        {
            csharpCode = LineDirectivePostProcessor.Process(csharpCode);
        }
        return csharpCode;
    }
}

// ----- Result Structs -----

/// <summary>
/// Result of lexical analysis via <see cref="FileCompilationPipeline.Lex"/>.
/// </summary>
internal readonly struct LexResult
{
    public LexResult(IReadOnlyList<Token> tokens, DiagnosticBag diagnostics)
    {
        Tokens = tokens;
        Diagnostics = diagnostics;
    }

    public IReadOnlyList<Token> Tokens { get; }
    public DiagnosticBag Diagnostics { get; }
    public bool HasErrors => Diagnostics.HasErrors;
}

/// <summary>
/// Result of syntax analysis via <see cref="FileCompilationPipeline.Parse"/>.
/// </summary>
internal readonly struct PipelineParseResult
{
    public PipelineParseResult(Module? module, DiagnosticBag diagnostics)
    {
        Module = module;
        Diagnostics = diagnostics;
    }

    public Module? Module { get; }
    public DiagnosticBag Diagnostics { get; }
    public bool HasErrors => Diagnostics.HasErrors;
}

/// <summary>
/// Result of name resolution via <see cref="FileCompilationPipeline.ResolveNames"/>.
/// </summary>
internal readonly struct NameResolveResult
{
    public NameResolveResult(NameResolver nameResolver)
    {
        NameResolver = nameResolver;
    }

    public NameResolver NameResolver { get; }
    public DiagnosticBag Diagnostics => NameResolver.Diagnostics;
    public bool HasErrors => Diagnostics.HasErrors;
}

/// <summary>
/// Result of import resolution via <see cref="FileCompilationPipeline.ResolveImports"/>.
/// </summary>
internal readonly struct ImportResolveResult
{
    public ImportResolveResult(ImportResolver importResolver)
    {
        ImportResolver = importResolver;
    }

    public ImportResolver ImportResolver { get; }
    public DiagnosticBag Diagnostics => ImportResolver.Diagnostics;
    public bool HasErrors => Diagnostics.HasErrors;
}

/// <summary>
/// Result of code generation via <see cref="FileCompilationPipeline.GenerateCode"/>.
/// </summary>
internal readonly struct CodeGenResult
{
    public CodeGenResult(
        string csharpCode,
        Dictionary<string, string> allGeneratedFiles,
        DiagnosticBag diagnostics)
    {
        CSharpCode = csharpCode;
        AllGeneratedFiles = allGeneratedFiles;
        Diagnostics = diagnostics;
    }

    public string CSharpCode { get; }
    public Dictionary<string, string> AllGeneratedFiles { get; }
    public DiagnosticBag Diagnostics { get; }
    public bool HasErrors => Diagnostics.HasErrors;
}

/// <summary>
/// Result of a single type-check operation via <see cref="FileCompilationPipeline.TypeCheck"/>.
/// </summary>
internal readonly struct TypeCheckResult
{
    public TypeCheckResult(TypeChecker typeChecker, bool aborted)
    {
        TypeChecker = typeChecker;
        Aborted = aborted;
    }

    /// <summary>The TypeChecker instance with populated diagnostics and validator times.</summary>
    public TypeChecker TypeChecker { get; }

    /// <summary>True if type checking was aborted early due to too many errors.</summary>
    public bool Aborted { get; }
}
