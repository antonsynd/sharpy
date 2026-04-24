using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Text;

namespace Sharpy.Compiler.Model;

/// <summary>
/// Accumulates compilation artifacts progressively and builds a <see cref="CompilationResult"/>.
/// Each phase of compilation sets its artifacts on the builder; early exit points call
/// <see cref="BuildFailure"/> which captures whatever has been set so far.
/// </summary>
internal class CompilationResultBuilder
{
    private readonly DiagnosticBag _diagnostics;
    private readonly CompilationMetrics _metrics;

    private bool _success;
    private SourceText? _sourceText;
    private IReadOnlyList<Token>? _tokens;
    private Module? _module;
    private SymbolTable? _symbolTable;
    private SemanticInfo? _semanticInfo;
    private ModuleRegistry? _moduleRegistry;
    private string? _generatedCSharpCode;
    private Dictionary<string, string>? _generatedCSharpFiles;
    private SemanticBinding? _semanticBinding;
    private ImportResolver? _importResolver;
    private IReadOnlyList<CommentSpan>? _commentSpans;

    public CompilationResultBuilder(DiagnosticBag diagnostics, CompilationMetrics metrics)
    {
        _diagnostics = diagnostics;
        _metrics = metrics;
    }

    // Direct setters for artifacts accumulated during compilation phases
    public SourceText? SourceText { set => _sourceText = value; }
    public IReadOnlyList<Token>? Tokens { set => _tokens = value; }
    public Module? Module { set => _module = value; }
    public SemanticBinding? SemanticBinding { set => _semanticBinding = value; }
    public ImportResolver? ImportResolver { set => _importResolver = value; }
    public IReadOnlyList<CommentSpan>? CommentSpans { set => _commentSpans = value; }

    // Fluent setters for properties only set on the success path
    public CompilationResultBuilder WithSuccess(bool success) { _success = success; return this; }
    public CompilationResultBuilder WithSymbolTable(SymbolTable? symbolTable) { _symbolTable = symbolTable; return this; }
    public CompilationResultBuilder WithSemanticInfo(SemanticInfo? semanticInfo) { _semanticInfo = semanticInfo; return this; }
    public CompilationResultBuilder WithModuleRegistry(ModuleRegistry? moduleRegistry) { _moduleRegistry = moduleRegistry; return this; }
    public CompilationResultBuilder WithGeneratedCSharpCode(string? code) { _generatedCSharpCode = code; return this; }
    public CompilationResultBuilder WithGeneratedCSharpFiles(Dictionary<string, string>? files) { _generatedCSharpFiles = files; return this; }

    /// <summary>
    /// Build a failure result using all artifacts accumulated so far.
    /// </summary>
    public CompilationResult BuildFailure()
    {
        _success = false;
        return Build();
    }

    /// <summary>
    /// Build the final <see cref="CompilationResult"/> with all accumulated artifacts.
    /// </summary>
    public CompilationResult Build()
    {
        return new CompilationResult
        {
            Success = _success,
            Diagnostics = _diagnostics,
            Metrics = _metrics,
            SourceText = _sourceText,
            Tokens = _tokens,
            Module = _module,
            SymbolTable = _symbolTable,
            SemanticInfo = _semanticInfo,
            ModuleRegistry = _moduleRegistry,
            GeneratedCSharpCode = _generatedCSharpCode,
            GeneratedCSharpFiles = _generatedCSharpFiles ?? new(),
            SemanticBinding = _semanticBinding,
            ImportResolver = _importResolver,
            CommentSpans = _commentSpans
        };
    }
}
