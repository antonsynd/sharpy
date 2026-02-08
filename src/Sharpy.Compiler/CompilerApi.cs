using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Services;
using Sharpy.Compiler.Text;

namespace Sharpy.Compiler;

/// <summary>
/// Clean, documented entry point for programmatic compilation.
/// Provides a stable interface for tooling consumers (LSP, IDE plugins, build systems)
/// without depending on CLI internals.
/// </summary>
/// <remarks>
/// <para>
/// This class wraps the internal <see cref="Compiler"/> pipeline and exposes
/// structured result types that are easy to consume programmatically.
/// All methods accept <see cref="CancellationToken"/> for cooperative cancellation.
/// </para>
/// <para>
/// Three levels of analysis are available:
/// <list type="bullet">
///   <item><see cref="Parse"/> — Lexer + Parser only (syntax information)</item>
///   <item><see cref="Analyze"/> — Lexer + Parser + Semantic (type information, no codegen)</item>
///   <item><see cref="Compile(string, CompilerOptions?, string?, CancellationToken)"/> — Full pipeline including C# code generation</item>
/// </list>
/// </para>
/// </remarks>
public sealed class CompilerApi
{
    private readonly AstPositionService _positionService = new();

    /// <summary>
    /// Compiles Sharpy source code through the full pipeline (Lexer → Parser → Semantic → CodeGen).
    /// </summary>
    /// <param name="source">The Sharpy source code to compile.</param>
    /// <param name="options">Optional compiler options. Uses sensible defaults if null.</param>
    /// <param name="filePath">Optional file path for diagnostics. Defaults to "&lt;source&gt;".</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>A <see cref="CompileResult"/> with the compilation outcome.</returns>
    public CompileResult Compile(
        string source,
        CompilerOptions? options = null,
        string? filePath = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedPath = filePath ?? "<source>";
        var opts = options ?? new CompilerOptions();
        var compiler = new Compiler(opts);

        var result = compiler.Compile(source, resolvedPath, cancellationToken);

        return new CompileResult
        {
            Success = result.Success,
            Diagnostics = result.Diagnostics.GetAll(),
            GeneratedCSharp = result.GeneratedCSharpCode,
            Ast = result.Module,
            SemanticInfo = result.SemanticInfo,
            Metrics = result.Metrics
        };
    }

    /// <summary>
    /// Compiles a Sharpy source file through the full pipeline.
    /// </summary>
    /// <param name="filePath">Path to the Sharpy source file.</param>
    /// <param name="options">Optional compiler options.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>A <see cref="CompileResult"/> with the compilation outcome.</returns>
    /// <exception cref="FileNotFoundException">If the source file does not exist.</exception>
    public CompileResult CompileFile(
        string filePath,
        CompilerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Source file not found: {filePath}", filePath);

        var source = File.ReadAllText(filePath);
        return Compile(source, options, filePath, cancellationToken);
    }

    /// <summary>
    /// Parses Sharpy source code (Lexer → Parser only).
    /// Returns the AST and any lexer/parser diagnostics. No semantic analysis or codegen.
    /// Useful for tooling that only needs syntax information (e.g., syntax highlighting, formatting).
    /// </summary>
    /// <param name="source">The Sharpy source code to parse.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>A <see cref="ParseResult"/> with the parse outcome.</returns>
    public ParseResult Parse(string source, CancellationToken cancellationToken = default)
    {
        var diagnostics = new DiagnosticBag();

        try
        {
            var sourceText = new SourceText(source, "<source>");

            // Phase 1: Lexical Analysis
            var lexer = new Lexer.Lexer(sourceText, NullLogger.Instance, cancellationToken: cancellationToken);
            var tokens = lexer.TokenizeAll();

            if (lexer.Diagnostics.HasErrors)
            {
                diagnostics.Merge(lexer.Diagnostics);
                return new ParseResult
                {
                    Success = false,
                    Diagnostics = diagnostics.GetAll()
                };
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Phase 2: Syntax Analysis
            var parser = new Parser.Parser(tokens, NullLogger.Instance, maxErrors: 25, cancellationToken);
            var module = parser.ParseModule();

            diagnostics.Merge(lexer.Diagnostics);
            diagnostics.Merge(parser.Diagnostics);

            return new ParseResult
            {
                Success = !diagnostics.HasErrors,
                Diagnostics = diagnostics.GetAll(),
                Ast = module
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            diagnostics.AddError($"Parse failed: {ex.Message}");
            return new ParseResult
            {
                Success = false,
                Diagnostics = diagnostics.GetAll()
            };
        }
    }

    /// <summary>
    /// Analyzes Sharpy source code (Lexer → Parser → Semantic). No codegen.
    /// Returns the AST, semantic info, and symbol table.
    /// Useful for LSP hover, completion, and go-to-definition.
    /// </summary>
    /// <param name="source">The Sharpy source code to analyze.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>A <see cref="SemanticResult"/> with the analysis outcome.</returns>
    public SemanticResult Analyze(string source, CancellationToken cancellationToken = default)
    {
        // Use the full Compile pipeline (which includes semantic analysis) and strip codegen.
        // This is simpler and more correct than duplicating the semantic pipeline.
        // The codegen overhead is minimal compared to semantic analysis.
        var opts = new CompilerOptions { OutputType = "library" };
        var compiler = new Compiler(opts);
        var result = compiler.Compile(source, "<source>", cancellationToken);

        return new SemanticResult
        {
            Success = !result.Diagnostics.HasErrors,
            Diagnostics = result.Diagnostics.GetAll(),
            Ast = result.Module,
            SemanticInfo = result.SemanticInfo,
            SymbolTable = result.SymbolTable
        };
    }

    /// <summary>
    /// Finds the innermost AST node at the given source position.
    /// Delegates to <see cref="AstPositionService.FindInnermostNode"/>.
    /// </summary>
    /// <param name="module">The parsed module to search.</param>
    /// <param name="line">1-based line number (matching LSP conventions).</param>
    /// <param name="column">1-based column number (matching LSP conventions).</param>
    /// <returns>The innermost node at the position, or null if outside all nodes.</returns>
    public Node? FindNodeAtPosition(Module module, int line, int column)
    {
        return _positionService.FindInnermostNode(module, line, column);
    }

    /// <summary>
    /// Finds the innermost AST node of a specific type at the given source position.
    /// Delegates to <see cref="AstPositionService.FindNodeOfType{T}"/>.
    /// </summary>
    /// <typeparam name="T">The AST node type to find.</typeparam>
    /// <param name="module">The parsed module to search.</param>
    /// <param name="line">1-based line number (matching LSP conventions).</param>
    /// <param name="column">1-based column number (matching LSP conventions).</param>
    /// <returns>The innermost node of type T at the position, or null if not found.</returns>
    public T? FindNodeOfType<T>(Module module, int line, int column) where T : Node
    {
        return _positionService.FindNodeOfType<T>(module, line, column);
    }

    /// <summary>
    /// Formats a diagnostic for display, with optional source context and underlines.
    /// Delegates to <see cref="DiagnosticRenderer"/>.
    /// </summary>
    /// <param name="diagnostic">The diagnostic to format.</param>
    /// <param name="source">Optional source code for context rendering (enables ^^^ underlines).</param>
    /// <returns>A formatted string suitable for display.</returns>
    public string FormatDiagnostic(CompilerDiagnostic diagnostic, string? source = null)
    {
        var renderer = new DiagnosticRenderer(useColor: false);
        SourceText? sourceText = source != null
            ? new SourceText(source, diagnostic.FilePath ?? "<source>")
            : null;
        return renderer.Render(diagnostic, sourceText);
    }
}
