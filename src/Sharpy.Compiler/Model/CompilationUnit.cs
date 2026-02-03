using System.Collections.Immutable;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Model;

/// <summary>
/// Represents a single Sharpy source file and all its compilation artifacts.
/// This is the fundamental unit of compilation.
/// </summary>
/// <remarks>
/// <para>
/// CompilationUnit is designed for:
/// - <b>Incremental compilation</b>: Track content hash for staleness detection
/// - <b>Parallel compilation</b>: Immutable after construction (except GeneratedCSharp)
/// - <b>LSP support</b>: Store tokens for hover/completion, diagnostics with file context
/// - <b>Debugging</b>: Preserve source mapping information
/// </para>
/// </remarks>
public class CompilationUnit
{
    /// <summary>
    /// Creates a new CompilationUnit for a source file.
    /// </summary>
    /// <param name="filePath">Full path to the source file.</param>
    /// <param name="modulePath">Dotted module path (e.g., "mypackage.helpers").</param>
    /// <param name="sourceText">The raw source code text.</param>
    public CompilationUnit(string filePath, string modulePath, string sourceText)
    {
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        ModulePath = modulePath ?? throw new ArgumentNullException(nameof(modulePath));
        SourceText = sourceText ?? throw new ArgumentNullException(nameof(sourceText));
        ContentHash = ComputeHash(sourceText);
        Diagnostics = new DiagnosticBag();
    }

    #region Source Information

    /// <summary>
    /// Full path to the source file.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Dotted module path derived from the file's location relative to project root.
    /// Example: "mypackage.helpers" for src/mypackage/helpers.spy
    /// </summary>
    public string ModulePath { get; }

    /// <summary>
    /// The raw source code text.
    /// </summary>
    public string SourceText { get; }

    /// <summary>
    /// SHA-256 hash of source content for change detection in incremental compilation.
    /// </summary>
    public string ContentHash { get; }

    #endregion

    #region Parsing Artifacts

    /// <summary>
    /// The tokenized representation of the source file.
    /// Null until lexical analysis completes.
    /// </summary>
    /// <remarks>
    /// Stored for future LSP support (hover, completion, syntax highlighting).
    /// </remarks>
    public IReadOnlyList<Token>? Tokens { get; internal set; }

    /// <summary>
    /// The abstract syntax tree for this file.
    /// Null until parsing completes.
    /// </summary>
    public Module? Ast { get; internal set; }

    #endregion

    #region Semantic Artifacts

    /// <summary>
    /// The module-level scope containing all declarations from this file.
    /// Null until name resolution completes.
    /// </summary>
    public Scope? ModuleScope { get; internal set; }

    /// <summary>
    /// Type symbols declared in this file (classes, structs, interfaces, enums).
    /// Empty until name resolution completes.
    /// </summary>
    public IReadOnlyList<TypeSymbol> DeclaredTypes { get; internal set; } = Array.Empty<TypeSymbol>();

    /// <summary>
    /// Function symbols declared at module level in this file.
    /// Empty until name resolution completes.
    /// </summary>
    public IReadOnlyList<FunctionSymbol> DeclaredFunctions { get; internal set; } = Array.Empty<FunctionSymbol>();

    /// <summary>
    /// Import statements in this file.
    /// Empty until parsing completes.
    /// </summary>
    public IReadOnlyList<ImportStatement> Imports { get; internal set; } = Array.Empty<ImportStatement>();

    /// <summary>
    /// FromImport statements in this file.
    /// Empty until parsing completes.
    /// </summary>
    public IReadOnlyList<FromImportStatement> FromImports { get; internal set; } = Array.Empty<FromImportStatement>();

    #endregion

    #region Dependencies

    /// <summary>
    /// File paths that this unit directly depends on (imports).
    /// Populated during import resolution.
    /// </summary>
    public ImmutableHashSet<string> DirectDependencies { get; internal set; } = ImmutableHashSet<string>.Empty;

    #endregion

    #region Code Generation

    /// <summary>
    /// The generated C# source code for this file.
    /// Null until code generation completes.
    /// </summary>
    public string? GeneratedCSharp { get; internal set; }

    #endregion

    #region Diagnostics

    /// <summary>
    /// Diagnostics (errors/warnings) specific to this file.
    /// Thread-safe for potential future parallel compilation.
    /// </summary>
    public DiagnosticBag Diagnostics { get; }

    /// <summary>
    /// Indicates whether this unit has any errors.
    /// </summary>
    public bool HasErrors => Diagnostics.HasErrors;

    #endregion

    #region Metrics

    /// <summary>
    /// Compilation metrics (timing, memory) for this file.
    /// Null until compilation begins.
    /// </summary>
    public CompilationMetrics? Metrics { get; internal set; }

    #endregion

    #region Compilation State

    /// <summary>
    /// The current compilation phase of this unit.
    /// </summary>
    public CompilationPhase Phase { get; internal set; } = CompilationPhase.Created;

    #endregion

    #region Helper Methods

    /// <summary>
    /// Checks if this unit's content has changed compared to a cached hash.
    /// </summary>
    /// <param name="cachedHash">The previously stored content hash.</param>
    /// <returns>True if the content has changed (is stale), false otherwise.</returns>
    public bool IsStale(string? cachedHash)
    {
        if (string.IsNullOrEmpty(cachedHash))
            return true;
        return !string.Equals(ContentHash, cachedHash, StringComparison.Ordinal);
    }

    private static string ComputeHash(string content)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var hashBytes = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hashBytes);
    }

    #endregion
}

/// <summary>
/// Represents the compilation phase of a CompilationUnit.
/// </summary>
public enum CompilationPhase
{
    /// <summary>Unit created but not yet processed.</summary>
    Created,

    /// <summary>Lexical analysis (tokenization) completed.</summary>
    Lexed,

    /// <summary>Parsing completed, AST available.</summary>
    Parsed,

    /// <summary>Name resolution completed, symbols declared.</summary>
    NamesResolved,

    /// <summary>Type checking and semantic analysis completed.</summary>
    TypeChecked,

    /// <summary>Code generation completed, C# output available.</summary>
    CodeGenerated,

    /// <summary>Compilation failed with errors.</summary>
    Failed
}
