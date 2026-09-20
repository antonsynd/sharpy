using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Shared;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// RoslynEmitter partial class: the ONE unpacking lowering, at every depth.
/// </summary>
internal partial class RoslynEmitter
{
    /// <summary>
    /// Whether an unpacking target may be lowered as an in-place C# deconstruction
    /// (<c>var (a, b) = src</c>, <c>(a, b) = src</c>, <c>foreach (var (a, b) in xs)</c>) instead of
    /// through <see cref="GenerateUnpackingStores"/>.
    /// <para>Two conditions, both about what C# syntax admits — arity is the one this predicate
    /// exists for: a parenthesized variable designation, a tuple expression and a foreach variable
    /// statement each need at least TWO designations, so a sole-element group like <c>(a,) = t</c>
    /// has no deconstruction spelling at all (<c>var (a) = t;</c> is not a tuple pattern and does
    /// not parse). A target this predicate declines falls through to the one unpacking rule, which
    /// reads <c>src.ItemN</c> / <c>src[i]</c> per element and so is total over arity (#1846).</para>
    /// </summary>
    private static bool CanDeconstructInPlace(ImmutableArray<Expression> elements) =>
        elements.Length >= 2 && elements.All(e => e is Parser.Ast.Identifier);

    /// <summary>
    /// Lowers a tuple/list-display unpacking target into a flat list of store statements, at every
    /// depth (#1846). It is a pure SyntaxFactory translator (Rule 2): the semantic layer already
    /// decided the shape, so this reads the source type it is handed and emits
    /// <list type="bullet">
    ///   <item><c>__src.ItemN</c> for a tuple source, or <c>__src[i]</c> / <c>__src[-n]</c> for a
    ///     <c>list[T]</c>/<c>array[T]</c> source;</item>
    ///   <item>the star's <c>new Sharpy.List&lt;T&gt; { … }</c> (tuple) or <c>__src.GetSlice(…)</c>
    ///     (sequence);</item>
    ///   <item>a fresh temp and a recursive call for a nested tuple target — so a nested star
    ///     lowers identically to a top-level one.</item>
    /// </list>
    /// Leaf identifiers, members, index targets and the star operand are stored through the one
    /// <see cref="GenerateStore(Expression, ExpressionSyntax)"/> seam.
    /// </summary>
    private void GenerateUnpackingStores(
        ImmutableArray<Expression> elements, string sourceVar, SemanticType? sourceType,
        List<StatementSyntax> statements)
    {
        int starIndex = -1;
        for (int i = 0; i < elements.Length; i++)
        {
            if (elements[i] is StarExpression)
            {
                starIndex = i;
                break;
            }
        }
        bool hasStar = starIndex >= 0;
        int numBefore = hasStar ? starIndex : elements.Length;
        int numAfter = hasStar ? elements.Length - starIndex - 1 : 0;

        var tupleType = sourceType as TupleType;
        bool isTupleSource = tupleType != null;
        int tupleArity = tupleType?.ElementTypes.Count ?? 0;

        for (int i = 0; i < elements.Length; i++)
        {
            // The star absorbs the middle slice: a new Sharpy.List<T> of the tuple items, or a
            // GetSlice of the sequence. Its operand is bound through the one store seam.
            if (hasStar && i == starIndex)
            {
                if (elements[i] is StarExpression { Operand: Parser.Ast.Identifier } starExpr)
                {
                    var starValue = StarSliceValue(sourceVar, tupleType, numBefore, numAfter);
                    statements.Add(GenerateStore(starExpr.Operand, starValue));
                }
                continue;
            }

            int itemIndex = (!hasStar || i < starIndex)
                ? i
                : tupleArity - numAfter + (i - starIndex - 1);

            ExpressionSyntax valueExpr;
            SemanticType? elemSourceType;
            if (isTupleSource)
            {
                valueExpr = MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName(sourceVar),
                    IdentifierName($"Item{itemIndex + 1}"));
                elemSourceType = itemIndex < tupleArity ? tupleType!.ElementTypes[itemIndex] : null;
            }
            else
            {
                int index = (!hasStar || i < starIndex) ? i : -(numAfter - (i - starIndex - 1));
                var indexArg = index < 0
                    ? (ExpressionSyntax)PrefixUnaryExpression(
                        SyntaxKind.UnaryMinusExpression,
                        LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(-index)))
                    : LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(index));
                valueExpr = ElementAccessExpression(IdentifierName(sourceVar))
                    .WithArgumentList(BracketedArgumentList(SingletonSeparatedList(Argument(indexArg))));
                elemSourceType = SequenceElementSemanticType(sourceType);
            }

            if (elements[i] is TupleLiteral nested)
            {
                var tempVarName = $"__t{_tempVarCounter++}";
                statements.Add(LocalDeclarationStatement(
                    VariableDeclaration(LocalDeclarationType(target: null, elemSourceType))
                        .WithVariables(SingletonSeparatedList(
                            VariableDeclarator(EscapedIdentifier(tempVarName))
                                .WithInitializer(EqualsValueClause(valueExpr))))));
                GenerateUnpackingStores(nested.Elements, tempVarName, elemSourceType, statements);
            }
            else
            {
                statements.Add(GenerateStore(elements[i], valueExpr));
            }
        }
    }

    /// <summary>The value the star target absorbs: a Sharpy.List of the tuple slice, or a GetSlice.</summary>
    private ExpressionSyntax StarSliceValue(
        string sourceVar, TupleType? tupleType, int numBefore, int numAfter)
    {
        if (tupleType != null)
        {
            int arity = tupleType.ElementTypes.Count;
            var restTypes = new List<SemanticType>();
            for (int ri = numBefore; ri < arity - numAfter; ri++)
                if (ri >= 0 && ri < arity)
                    restTypes.Add(tupleType.ElementTypes[ri]);

            TypeSyntax elementTypeSyntax = PredefinedType(Token(SyntaxKind.ObjectKeyword));
            if (restTypes.Count > 0 && restTypes.All(t => t.Equals(restTypes[0])))
                elementTypeSyntax = _typeMapper.MapSemanticType(restTypes[0]);

            var listTypeSyntax = TypeSyntaxMapper.QualifiedGenericName(
                CSharpTypeNames.SharpyList, elementTypeSyntax);
            var restItems = new List<ExpressionSyntax>();
            for (int ri = numBefore; ri < arity - numAfter; ri++)
                restItems.Add(MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName(sourceVar),
                    IdentifierName($"Item{ri + 1}")));

            return ObjectCreationExpression(listTypeSyntax)
                .WithArgumentList(ArgumentList())
                .WithInitializer(InitializerExpression(
                    SyntaxKind.CollectionInitializerExpression,
                    SeparatedList(restItems)));
        }

        var startArg = numBefore > 0
            ? (ExpressionSyntax)LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(numBefore))
            : LiteralExpression(SyntaxKind.NullLiteralExpression);
        var endArg = numAfter > 0
            ? (ExpressionSyntax)PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression,
                LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(numAfter)))
            : LiteralExpression(SyntaxKind.NullLiteralExpression);

        var newSlice = ObjectCreationExpression(MakeGlobalQualifiedName("Sharpy", "Slice"))
            .WithArgumentList(ArgumentList(SeparatedList(new[]
            {
                Argument(startArg),
                Argument(endArg),
            })));

        return InvocationExpression(
            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName(sourceVar),
                IdentifierName("GetSlice")))
            .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(newSlice))));
    }

    /// <summary>The T of a <c>list[T]</c>/<c>array[T]</c> source, else null.</summary>
    private static SemanticType? SequenceElementSemanticType(SemanticType? source) => source switch
    {
        GenericType { Name: BuiltinNames.List, TypeArguments: { Count: > 0 } a } => a[0],
        GenericType { Name: BuiltinNames.Array, TypeArguments: { Count: > 0 } a } => a[0],
        _ => null,
    };
}
