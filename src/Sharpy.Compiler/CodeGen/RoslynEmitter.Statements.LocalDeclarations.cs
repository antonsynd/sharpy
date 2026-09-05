using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// RoslynEmitter partial class: the declared C# type of a fresh local — one seam for every
/// declaration site (plain first assignment, tuple/star/nested unpacking, <c>GenerateStore</c>,
/// the walrus hoist).
/// </summary>
internal partial class RoslynEmitter
{
    /// <summary>
    /// The type a fresh C# local is declared with. <c>var</c> is the default; the recorded type is
    /// printed instead exactly where C#'s inference from the initializer lands on a DIFFERENT type
    /// than the one semantic analysis recorded for the variable — every later use of the variable
    /// was checked against the recorded type, so the C# local must carry it:
    /// <list type="bullet">
    /// <item><description>A lambda or function value: C# cannot infer a delegate type from a lambda,
    /// so the recorded <see cref="Semantic.FunctionType"/> is printed (<c>Func&lt;…&gt;</c> /
    /// <c>Action&lt;…&gt;</c>).</description></item>
    /// <item><description>A user union: the initializer of a union-typed local is typically a case
    /// construction (<c>new Box&lt;int&gt;.Full(7)</c>) whose C# static type is the CASE class, a
    /// subtype of the union. Under <c>var</c> the local IS the case class, so a later <c>match</c>
    /// arm for another case is CS8121 and a store of another case is CS0029 (#1770). The recorded
    /// union type is printed instead: <c>Box&lt;int&gt; b = new Box&lt;int&gt;.Full(7);</c>.</description></item>
    /// </list>
    /// Rule 2: this reads the recorded symbol type (or the recorded value type at a site that has
    /// no symbol) and maps it. What the variable's type IS was decided by the checker; the emitter
    /// only stops letting C# re-derive it more narrowly.
    /// </summary>
    /// <param name="target">The symbol of the variable being declared, when the checker recorded one.</param>
    /// <param name="valueType">The recorded type of the initializer expression, when the site has one.</param>
    private TypeSyntax LocalDeclarationType(Symbol? target, SemanticType? valueType)
        => ExplicitLocalDeclarationType(target, valueType) ?? IdentifierName("var");

    /// <summary>
    /// The symbol the checker bound to an assignment-target identifier — the recorded fact
    /// (<c>SetIdentifierSymbol</c> at every plain, tuple-element and star target), which carries the
    /// declared type of the local being created. The name lookup is the fallback for a target the
    /// checker never saw (AST-only unit tests); it resolves module-level symbols only.
    /// </summary>
    private Symbol? DeclaredTargetSymbol(Identifier target)
        => _context.SemanticInfo?.GetIdentifierSymbol(target) ?? _context.LookupSymbol(target.Name);

    /// <summary>
    /// <see cref="LocalDeclarationType"/> with <c>null</c> standing for <c>var</c>, for sites that
    /// choose a different syntax shape when every element can stay <c>var</c> (tuple deconstruction).
    /// </summary>
    private TypeSyntax? ExplicitLocalDeclarationType(Symbol? target, SemanticType? valueType)
    {
        var declaredType = (target as VariableSymbol)?.Type;

        var functionType = valueType as Semantic.FunctionType ?? declaredType as Semantic.FunctionType;
        if (functionType != null && !functionType.HasUnresolvedTypes())
            return _typeMapper.MapSemanticType(functionType);

        var unionType = IsClosedUserUnionType(declaredType) ? declaredType
            : IsClosedUserUnionType(valueType) ? valueType
            : null;
        if (unionType != null)
            return _typeMapper.MapSemanticType(unionType);

        return null;
    }

    /// <summary>
    /// Whether a recorded type is a user-declared union — generic and closed, or non-generic. A
    /// case type (<c>Box.Full</c>) is NOT a union type: its symbol's kind is the case class, so a
    /// local the checker typed as one specific case keeps <c>var</c>. An open type argument
    /// (<see cref="UnknownType"/>) means an error was already reported; <c>var</c> is the safe print.
    /// </summary>
    private static bool IsClosedUserUnionType(SemanticType? type) => type switch
    {
        GenericType { GenericDefinition: { TypeKind: Semantic.TypeKind.Union } } generic
            => generic.TypeArguments.All(t => t is not UnknownType),
        UserDefinedType { Symbol: { TypeKind: Semantic.TypeKind.Union } } => true,
        _ => false,
    };
}
