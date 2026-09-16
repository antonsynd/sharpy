using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// TypeChecker partial class: the ONE unpacking rule, at every depth and in all four positions.
/// </summary>
internal partial class TypeChecker
{
    /// <summary>
    /// Type-checks and binds a tuple/list-display unpacking target against its source, at every
    /// depth and in all four positions (assignment, <c>for</c>, <c>with … as</c>, comprehension
    /// for-clause). This is the single authority for the star rule: "a star absorbs ≥ 0 elements"
    /// holds identically everywhere (#1845, #1846). It owns
    /// <list type="bullet">
    ///   <item>the bare sole-starred group refusal — a <c>(*a)</c> that survived canonicalization as
    ///     a <see cref="SpreadElement"/> (SPY0225, Python's wording);</item>
    ///   <item>the multiple-star refusal at THIS level (SPY0356);</item>
    ///   <item>the arity check with the position suffix (SPY0239);</item>
    ///   <item>the star's <c>list[T]</c> slice from a tuple source, or <c>T</c> from a
    ///     <c>list[T]</c>/<c>array[T]</c> source;</item>
    ///   <item>recursion into nested tuple targets with the element's own type.</item>
    /// </list>
    /// </summary>
    /// <param name="source">
    /// The already-resolved source element type for the for/with/comprehension positions, or a
    /// placeholder the routine re-derives from <paramref name="valueNode"/> for an assignment.
    /// </param>
    /// <param name="valueNode">
    /// The assignment RHS (or a nested RHS literal). When it is a tuple literal, each element is
    /// checked under the DECLARED slot of the target it lands on, so a bare <c>None</c> types under
    /// that slot instead of as <c>void</c> (#1707, #1812). Null for for/with/comprehension.
    /// </param>
    private void CheckUnpackingTargets(
        ImmutableArray<Expression> targets, SemanticType source,
        UnpackingPosition position, int errLine, int errCol, Text.TextSpan? errSpan,
        Expression? valueNode = null, bool nested = false)
    {
        // A bare sole-starred group `(*a)` survives canonicalization as a SpreadElement (a comma
        // `(*a,)` or a list display `[*a]` would have become a StarExpression). Python refuses it in
        // every position — "cannot use starred expression here" (#1845).
        foreach (var t in targets)
        {
            if (t is SpreadElement)
            {
                AddError(InvalidAssignmentTargetMessage(t),
                    t.LineStart, t.ColumnStart, code: DiagnosticCodes.Semantic.InvalidAssignmentTarget,
                    span: t.Span);
                return;
            }
        }

        int starIndex = -1;
        int starCount = 0;
        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] is StarExpression)
            {
                if (starIndex < 0)
                    starIndex = i;
                starCount++;
            }
        }

        // More than one star at THIS depth is a Python SyntaxError at every depth and position
        // ("multiple starred expressions in assignment"), SPY0356.
        if (starCount > 1)
        {
            AddError("Only one starred expression is allowed in an unpacking assignment"
                    + UnpackingPositionSuffix(position),
                errLine, errCol, code: DiagnosticCodes.Semantic.MultipleStarExpressions,
                span: errSpan);
            return;
        }

        bool hasStar = starIndex >= 0;
        int targetsBefore = hasStar ? starIndex : targets.Length;
        int targetsAfter = hasStar ? targets.Length - starIndex - 1 : 0;

        // Assignment value-side: check each RHS node under its target's declared slot and recompose
        // the value tuple's own type (#1707, #1812). This mirrors the flat path so the star and
        // non-star assignment forms classify identically.
        IReadOnlyList<SemanticType>? perElement = null;
        var valueNodes = TupleLiteralElements(valueNode);
        if (position == UnpackingPosition.Assignment && valueNode != null)
        {
            if (valueNodes != null && valueNodes.Count >= targetsBefore + targetsAfter
                && (hasStar || valueNodes.Count == targets.Length))
            {
                perElement = CheckStarValueElements(targets, valueNodes, targetsBefore, targetsAfter);
                RecomposeTupleLiteralType(valueNode, valueNodes);
                source = new TupleType { ElementTypes = perElement.ToList() };
                _semanticInfo.SetExpressionType(valueNode, source);
            }
            else
            {
                source = CheckExpression(valueNode);
            }
        }

        if (source is TupleType tupleType)
        {
            int arity = tupleType.ElementTypes.Count;
            bool arityOk = hasStar
                ? arity >= targetsBefore + targetsAfter
                : arity == targets.Length;
            if (!arityOk)
            {
                AddError(UnpackArityMessage(arity, targets.Length, position, nested),
                    errLine, errCol, code: DiagnosticCodes.Semantic.InvalidTupleUnpacking, span: errSpan);
                return;
            }

            var restType = StarRestType(tupleType, targetsBefore, targetsAfter);
            for (int i = 0; i < targets.Length; i++)
            {
                SemanticType elemType;
                if (hasStar && i == starIndex)
                    elemType = ListOf(restType);
                else if (!hasStar || i < starIndex)
                    elemType = tupleType.ElementTypes[i];
                else
                    elemType = tupleType.ElementTypes[arity - targetsAfter + (i - starIndex - 1)];

                BindUnpackingLeaf(targets[i], elemType, position, targets, starIndex, perElement,
                    ValueNodeForTarget(valueNodes, i, starIndex, targetsBefore, targetsAfter));
            }
        }
        else if (hasStar && SequenceElementType(source) is { } seqElem)
        {
            var listType = ListOf(seqElem);
            for (int i = 0; i < targets.Length; i++)
            {
                var elemType = i == starIndex ? listType : seqElem;
                BindUnpackingLeaf(targets[i], elemType, position, targets, starIndex, perElement, null);
            }
        }
        else
        {
            AddError(UnpackNonTupleMessage(source.GetDisplayName(), position, nested),
                errLine, errCol, code: DiagnosticCodes.Semantic.InvalidTupleUnpacking, span: errSpan);
        }
    }

    /// <summary>The T of a <c>list[T]</c>/<c>array[T]</c> source, or null for any other type.</summary>
    private static SemanticType? SequenceElementType(SemanticType source) => source switch
    {
        GenericType { Name: BuiltinNames.List, TypeArguments: { Count: > 0 } a } => a[0],
        GenericType { Name: BuiltinNames.Array, TypeArguments: { Count: > 0 } a } => a[0],
        _ => null,
    };

    private static GenericType ListOf(SemanticType elem) =>
        new() { Name = BuiltinNames.List, TypeArguments = new List<SemanticType> { elem } };

    /// <summary>
    /// The element type of the star's <c>list[T]</c> from a tuple source: the join of the tuple
    /// slice the star absorbs (a homogeneous slice keeps its type; a heterogeneous one falls back to
    /// <c>object</c>; an empty slice reuses the first element's type).
    /// </summary>
    private static SemanticType StarRestType(TupleType source, int targetsBefore, int targetsAfter)
    {
        var restTypes = new List<SemanticType>();
        for (int ri = targetsBefore; ri < source.ElementTypes.Count - targetsAfter; ri++)
            restTypes.Add(source.ElementTypes[ri]);

        if (restTypes.Count == 0)
            return source.ElementTypes.Count > 0 ? source.ElementTypes[0] : SemanticType.Unknown;
        if (restTypes.All(t => t.Equals(restTypes[0])))
            return restTypes[0];
        return BuiltinType.Object;
    }

    /// <summary>The RHS node a non-star target at <paramref name="targetIndex"/> lands on, or null.</summary>
    private static Expression? ValueNodeForTarget(
        IReadOnlyList<Expression>? valueNodes, int targetIndex, int starIndex,
        int targetsBefore, int targetsAfter)
    {
        if (valueNodes == null)
            return null;
        if (starIndex < 0 || targetIndex < starIndex)
            return targetIndex < valueNodes.Count ? valueNodes[targetIndex] : null;
        if (targetIndex == starIndex)
            return null;
        int fromEnd = targetsAfter - (targetIndex - starIndex - 1);
        int valueIndex = valueNodes.Count - fromEnd;
        return valueIndex >= 0 && valueIndex < valueNodes.Count ? valueNodes[valueIndex] : null;
    }

    /// <summary>Binds one unpacking target (star, identifier, nested tuple, or complex store).</summary>
    private void BindUnpackingLeaf(
        Expression target, SemanticType elemType, UnpackingPosition position,
        ImmutableArray<Expression> siblings, int starIndex,
        IReadOnlyList<SemanticType>? perElement, Expression? valueNode)
    {
        switch (target)
        {
            case StarExpression { Operand: Identifier starId } starExpr:
                DefineUnpackingSymbol(starId, elemType, TargetBindingKind.Declares, predecessor: null);
                _semanticInfo.SetExpressionType(starExpr, elemType);
                break;

            case Identifier id when position == UnpackingPosition.Assignment:
                BindAssignmentUnpackingIdentifier(id, elemType, siblings, perElement);
                break;

            case Identifier id:
                DefineUnpackingSymbol(id, elemType, TargetBindingKind.Declares, predecessor: null);
                if (elemType is UnknownType)
                    MarkExpressionAsErrorRecovery(id,
                        ErrorRecoveryReason.Propagated("the unpacked tuple element's type"));
                break;

            case TupleLiteral nested:
                CheckUnpackingTargets(nested.Elements, elemType, position,
                    nested.LineStart, nested.ColumnStart, nested.Span,
                    valueNode: valueNode, nested: true);
                break;

            default:
                // A member/index element (`b[k], y = …` / `obj.f, y = …`) is a store, not a binding.
                CheckExpression(target);
                break;
        }
    }

    /// <summary>
    /// The assignment-position identifier binding: a target with a declared binding is a STORE into
    /// it and keeps its declared type (#1706); a fresh target takes its own element's checked type
    /// (via <see cref="NonStarTargetType"/>) or the derived slice type.
    /// </summary>
    private void BindAssignmentUnpackingIdentifier(
        Identifier id, SemanticType elemType,
        ImmutableArray<Expression> siblings, IReadOnlyList<SemanticType>? perElement)
    {
        var predecessor = (_symbolTable.Lookup(id.Name, searchParents: false)
            ?? _symbolTable.Lookup(id.Name, searchParents: true)) as VariableSymbol;

        var boundType = predecessor != null
            ? DeclaredBindingType(predecessor)
            : NonStarTargetType(siblings, id, perElement, elemType);

        DefineUnpackingSymbol(id, boundType,
            predecessor != null ? TargetBindingKind.Rebinds : TargetBindingKind.Declares,
            predecessor);
    }

    /// <summary>Defines (or rebinds) one unpacking-target identifier symbol and records its facts.</summary>
    private void DefineUnpackingSymbol(
        Identifier id, SemanticType type, TargetBindingKind bindingKind, VariableSymbol? predecessor)
    {
        var symbol = new VariableSymbol
        {
            Name = id.Name,
            Kind = SymbolKind.Variable,
            Type = type,
            IsConstant = false,
            IsNameBacktickEscaped = id.IsNameBacktickEscaped,
            AccessLevel = AccessLevel.Public,
            DeclarationLine = id.LineStart,
            DeclarationColumn = id.ColumnStart,
            NameDeclarationLine = id.LineStart,
            NameDeclarationColumn = id.ColumnStart,
            DeclarationSpan = id.Span,
            DeclaringFilePath = _currentFilePath,
        };
        _symbolTable.Define(symbol);
        SemanticBinding.SetVariableType(symbol, type);
        _semanticInfo.SetIdentifierSymbol(id, symbol);
        if (predecessor != null && bindingKind == TargetBindingKind.Rebinds)
            _semanticInfo.SetRebindingPredecessor(symbol, predecessor);
        _semanticInfo.SetTargetBinding(id, new TargetBinding(bindingKind));
        _semanticInfo.SetExpressionType(id, type);
    }
}
