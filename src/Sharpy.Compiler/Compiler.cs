using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Parser;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler;

/// <summary>
/// Main compiler driver orchestrating the compilation pipeline
/// </summary>
public class Compiler
{
    private readonly ICompilerLogger _logger;

    public Compiler(ICompilerLogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
    }

    public CompilationResult Compile(string sourceCode, string filePath)
    {
        _logger.LogInfo($"Starting compilation of {filePath}");

        try
        {
            // Phase 1: Lexical Analysis
            _logger.LogInfo("Phase 1: Lexical Analysis");
            var lexer = new Lexer.Lexer(sourceCode, _logger);
            var tokens = lexer.TokenizeAll();

            // Phase 2: Syntax Analysis
            _logger.LogInfo("Phase 2: Syntax Analysis");
            var parser = new Parser.Parser(tokens, _logger);
            var module = parser.ParseModule();

            // Phase 3: Semantic Analysis
            _logger.LogInfo("Phase 3: Semantic Analysis");
            var builtinRegistry = new BuiltinRegistry();
            var symbolTable = new SymbolTable(builtinRegistry);
            var semanticInfo = new SemanticInfo();

            // Pass 1: Name resolution (declarations)
            var nameResolver = new NameResolver(symbolTable, _logger);
            nameResolver.ResolveDeclarations(module);

            if (nameResolver.Errors.Any())
            {
                return new CompilationResult
                {
                    Success = false,
                    Errors = nameResolver.Errors.Select(e => e.Message).ToList()
                };
            }

            // Pass 2: Type resolution and type checking
            var typeResolver = new TypeResolver(symbolTable, semanticInfo, _logger);
            var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, _logger);
            typeChecker.CheckModule(module);

            if (typeChecker.Errors.Any())
            {
                return new CompilationResult
                {
                    Success = false,
                    Errors = typeChecker.Errors.Select(e => e.Message).ToList()
                };
            }

            // TODO: Pass 3: Semantic validation (will implement in Phase 3)

            // Phase 4: Code Generation (placeholder)
            _logger.LogInfo("Phase 4: Code Generation");
            // TODO: Implement code generation

            return new CompilationResult
            {
                Success = true,
                Module = module,
                SymbolTable = symbolTable,
                SemanticInfo = semanticInfo
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"Compilation failed with exception: {ex.Message}", 0, 0);
            return new CompilationResult
            {
                Success = false,
                Errors = new List<string> { $"Compilation failed: {ex.Message}" }
            };
        }
    }
}

/// <summary>
/// Result of compilation including success status, errors, and generated artifacts
/// </summary>
public class CompilationResult
{
    public bool Success { get; init; }
    public List<string> Errors { get; init; } = new();
    public Module? Module { get; init; }
    public SymbolTable? SymbolTable { get; init; }
    public SemanticInfo? SemanticInfo { get; init; }
}
