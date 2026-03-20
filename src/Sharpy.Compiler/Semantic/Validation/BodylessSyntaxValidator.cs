using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Emits SPY0464 deprecation warnings for body-less method declarations.
///
/// Body-less form (no colon, no body):
///   @abstract
///   def foo(self) -> int
///
/// This form is deprecated. Users should use the explicit ellipsis form instead:
///   @abstract
///   def foo(self) -> int: ...
///
/// Detection: The parser synthesizes an EllipsisLiteral with a null Span for body-less
/// methods. Explicit "..." syntax produces an EllipsisLiteral with a non-null Span.
/// </summary>
internal class BodylessSyntaxValidator : ValidatingAstWalker
{
    public override string Name => "BodylessSyntaxValidator";
    public override int Order => 62; // After DecoratorValidator (60), before SignatureValidator (150)

    private bool _insideTypeDefinition;

    public override void Validate(Module module, SemanticContext context)
    {
        _insideTypeDefinition = false;
        base.Validate(module, context);
    }

    public override void VisitClassDef(ClassDef node)
    {
        var previous = _insideTypeDefinition;
        _insideTypeDefinition = true;
        base.VisitClassDef(node);
        _insideTypeDefinition = previous;
    }

    public override void VisitInterfaceDef(InterfaceDef node)
    {
        var previous = _insideTypeDefinition;
        _insideTypeDefinition = true;
        base.VisitInterfaceDef(node);
        _insideTypeDefinition = previous;
    }

    public override void VisitFunctionDef(FunctionDef node)
    {
        if (_insideTypeDefinition && IsSynthesizedEllipsisBody(node))
        {
            AddWarning(
                $"Body-less method declaration '{node.Name}' is deprecated. Use 'def {node.Name}(...): ...' instead.",
                line: node.LineStart,
                column: node.ColumnStart,
                code: DiagnosticCodes.Validation.DeprecatedBodylessSyntax,
                span: node.Span);
        }

        base.VisitFunctionDef(node);
    }

    /// <summary>
    /// Detects if a FunctionDef has a synthesized ellipsis body (created by the parser
    /// for body-less declarations). Synthesized ellipses have a null Span, while
    /// explicit "..." written by the user have a non-null Span.
    /// </summary>
    private static bool IsSynthesizedEllipsisBody(FunctionDef node)
    {
        if (node.Body.Length != 1)
            return false;

        return node.Body[0] is ExpressionStatement { Expression: EllipsisLiteral ellipsis }
               && ellipsis.Span is null;
    }
}
