using System.Diagnostics;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Semantic.Validation;

namespace Sharpy.Compiler;

/// <summary>
/// Encapsulates the per-file semantic analysis phases that are shared between
/// single-file (<see cref="Compiler"/>) and multi-file (<see cref="Project.ProjectCompiler"/>) compilation.
///
/// This class handles:
/// <list type="bullet">
///   <item>Imported type inheritance resolution and materialization</item>
///   <item>Type resolution and type checking (with <see cref="SemanticAnalysisException"/> handling)</item>
///   <item>Post-type-checking materialization of CodeGenInfo and VariableTypes</item>
/// </list>
///
/// Name resolution and import resolution remain outside this pipeline because they differ
/// fundamentally between single-file and multi-file modes (single-file creates a fresh
/// NameResolver; multi-file uses a shared one across all files).
/// </summary>
internal class FileCompilationPipeline
{
    private readonly SymbolTable _symbolTable;
    private readonly SemanticInfo _semanticInfo;
    private readonly SemanticBinding _semanticBinding;
    private readonly ICompilerLogger _logger;

    public FileCompilationPipeline(
        SymbolTable symbolTable,
        SemanticInfo semanticInfo,
        SemanticBinding semanticBinding,
        ICompilerLogger logger)
    {
        _symbolTable = symbolTable;
        _semanticInfo = semanticInfo;
        _semanticBinding = semanticBinding;
        _logger = logger;
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
        CancellationToken cancellationToken = default)
    {
        var typeResolver = new TypeResolver(_symbolTable, _semanticInfo, _logger, cancellationToken);
        var pipeline = ValidationPipelineFactory.CreateDefault(_logger);
        var semanticMaxErrors = maxErrors > 0 ? maxErrors : 100;
        var typeChecker = new TypeChecker(_symbolTable, _semanticInfo, typeResolver, _logger, pipeline)
        {
            CurrentFilePath = filePath,
            SemanticBinding = _semanticBinding,
            MaxErrors = semanticMaxErrors
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
