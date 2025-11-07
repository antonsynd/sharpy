using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Maps AST nodes to their semantic information
/// Provides a way to annotate the AST without modifying it
/// </summary>
public class SemanticInfo
{
    // Map expressions to their resolved types
    private readonly Dictionary<Expression, SemanticType> _expressionTypes = new();

    // Map identifiers to their symbols
    private readonly Dictionary<Identifier, Symbol> _identifierSymbols = new();

    // Map function calls to resolved function symbols
    private readonly Dictionary<FunctionCall, FunctionSymbol> _callTargets = new();

    // Map type annotations to resolved semantic types
    private readonly Dictionary<TypeAnnotation, SemanticType> _typeAnnotations = new();

    public void SetExpressionType(Expression expr, SemanticType type)
    {
        _expressionTypes[expr] = type;
    }

    public SemanticType? GetExpressionType(Expression expr)
    {
        return _expressionTypes.TryGetValue(expr, out var type) ? type : null;
    }

    public void SetIdentifierSymbol(Identifier id, Symbol symbol)
    {
        _identifierSymbols[id] = symbol;
    }

    public Symbol? GetIdentifierSymbol(Identifier id)
    {
        return _identifierSymbols.TryGetValue(id, out var symbol) ? symbol : null;
    }

    public void SetCallTarget(FunctionCall call, FunctionSymbol target)
    {
        _callTargets[call] = target;
    }

    public FunctionSymbol? GetCallTarget(FunctionCall call)
    {
        return _callTargets.TryGetValue(call, out var target) ? target : null;
    }

    public void SetTypeAnnotation(TypeAnnotation annotation, SemanticType type)
    {
        _typeAnnotations[annotation] = type;
    }

    public SemanticType? GetTypeAnnotation(TypeAnnotation annotation)
    {
        return _typeAnnotations.TryGetValue(annotation, out var type) ? type : null;
    }
}
