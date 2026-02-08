using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;

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

    /// <summary>Optional timing and artifact count data.</summary>
    public CompilationMetrics? Metrics { get; init; }
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

    /// <summary>The symbol table from name resolution, or null if name resolution failed.</summary>
    public SymbolTable? SymbolTable { get; init; }
}
