using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Services;

namespace Sharpy.Compiler;

/// <summary>
/// Result of a full compilation (Lexer → Parser → Semantic → CodeGen).
/// </summary>
public sealed record CompileResult
{
    /// <summary>Whether compilation completed without errors.</summary>
    public bool Success { get; init; }

    /// <summary>All diagnostics (errors and warnings) from all compilation phases.</summary>
    public IReadOnlyList<CompilerDiagnostic> Diagnostics { get; init; } = Array.Empty<CompilerDiagnostic>();

    /// <summary>The emitted C# source code for the entry file, or null if codegen failed or was not reached.</summary>
    public string? GeneratedCSharp { get; init; }

    /// <summary>
    /// All generated C# code files (entry point + all imported modules).
    /// Key is the source file path, value is the generated C# code.
    /// Empty if codegen was not reached or failed.
    /// </summary>
    public IReadOnlyDictionary<string, string> GeneratedCSharpFiles { get; init; } =
        new Dictionary<string, string>();

    /// <summary>The parsed AST, or null if parsing failed.</summary>
    public Module? Ast { get; init; }

    /// <summary>Type information from semantic analysis, or null if semantic analysis failed.</summary>
    public SemanticInfo? SemanticInfo { get; init; }

    /// <summary>Read-only query interface for semantic information, for LSP/tooling consumers.</summary>
    public ISemanticQuery? SemanticQuery => SemanticInfo;

    /// <summary>Optional timing and artifact count data.</summary>
    public CompilationMetrics? Metrics { get; init; }

    /// <summary>
    /// File paths of stdlib assemblies that were actually used during compilation.
    /// Enables selective dependency copying — only assemblies in this set need to be
    /// included in the output directory at runtime.
    /// </summary>
    public IReadOnlySet<string> UsedAssemblyPaths { get; init; } = new HashSet<string>();
}

/// <summary>
/// Result of parsing only (Lexer → Parser). No semantic analysis or codegen.
/// </summary>
public sealed record ParseResult
{
    /// <summary>Whether parsing completed without errors.</summary>
    public bool Success { get; init; }

    /// <summary>All diagnostics from lexing and parsing.</summary>
    public IReadOnlyList<CompilerDiagnostic> Diagnostics { get; init; } = Array.Empty<CompilerDiagnostic>();

    /// <summary>The parsed AST, or null if parsing failed.</summary>
    public Module? Ast { get; init; }
}

/// <summary>
/// Result of semantic analysis (Lexer → Parser → Semantic). No codegen.
/// </summary>
public sealed record SemanticResult
{
    /// <summary>Whether semantic analysis completed without errors.</summary>
    public bool Success { get; init; }

    /// <summary>All diagnostics from all phases up to and including semantic analysis.</summary>
    public IReadOnlyList<CompilerDiagnostic> Diagnostics { get; init; } = Array.Empty<CompilerDiagnostic>();

    /// <summary>The parsed AST, or null if parsing failed.</summary>
    public Module? Ast { get; init; }

    /// <summary>Type information from semantic analysis, or null if semantic analysis failed.</summary>
    public SemanticInfo? SemanticInfo { get; init; }

    /// <summary>Read-only query interface for semantic information, for LSP/tooling consumers.</summary>
    public ISemanticQuery? SemanticQuery => SemanticInfo;

    /// <summary>The symbol table from name resolution, or null if name resolution failed.</summary>
    public SymbolTable? SymbolTable { get; init; }

    /// <summary>
    /// Source spans of comments in the analyzed file (1-based line and column).
    /// Used by LSP hover to suppress hover inside comments. Empty when no comments
    /// were collected (e.g., when analysis failed before lexing completed).
    /// </summary>
    public IReadOnlyList<CommentSpan> CommentSpans { get; init; } = Array.Empty<CommentSpan>();
}

/// <summary>
/// A single-line comment span in 1-based line/column coordinates.
/// <c>StartColumn</c> points at the <c>#</c> character; <c>EndColumn</c> is exclusive
/// (one past the last character of the comment).
/// </summary>
public readonly record struct CommentSpan(int Line, int StartColumn, int EndColumn);
