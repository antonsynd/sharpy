using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic;

internal partial class TypeChecker
{
    /// <summary>
    /// The two arm-3 steers a caller supplies. Both are REAL SOURCE the user can paste: the site
    /// noun ("list element", "binding 'q'") names WHERE the refusal is and is never interpolated
    /// INTO the steer — "annotate the target (e.g. 'conditional expression: T = ...')" is not a
    /// program.
    /// </summary>
    /// <param name="AnnotateSteer">
    /// The steer when the operands have unrelated types (`[1, "a"]` → `'xs: list[object] = ...'`).
    /// </param>
    /// <param name="NoneAnnotateSteer">
    /// The steer when an untyped bare <c>None</c> is the reason (R-AB). Falls back to
    /// <paramref name="AnnotateSteer"/> when the caller supplies only one. It offers BOTH spellings
    /// of absence — a .NET-nullable <c>T | None</c> and a Sharpy <c>T?</c> built with
    /// <c>None()</c> — because <c>x: T? = None</c> is itself refused (R-G, #1720).
    /// </param>
    internal readonly record struct BestCommonTypeOptions(
        string? AnnotateSteer = null,
        string? NoneAnnotateSteer = null);

    /// <summary>
    /// The <see cref="BestCommonTypeOptions.NoneAnnotateSteer"/> for a fresh binding named
    /// <paramref name="name"/> — the two annotations that make <c>x = None</c> legal.
    /// </summary>
    internal static string BindingNoneSteer(string name)
        => $"'{name}: T | None = None' for a .NET-nullable reference, "
            + $"or '{name}: T? = None()' for a Sharpy optional";

    internal SemanticType BestCommonType(
        IReadOnlyList<(Expression? Node, SemanticType Type)> operands,
        SemanticType? slot,
        StorePosition position,
        Node host,
        string siteNoun,
        BestCommonTypeOptions options = default)
    {
        if (operands.Count == 0)
            return slot ?? SemanticType.Unknown;

        // ── Void-call handling (R-AE) ──
        // A void-call operand (VoidType from a None-returning function, not a bare NoneLiteral)
        // is refused in a binding context (single operand → SPY0227 "produces no value"). In a
        // multi-operand context (conditional with two void arms), ALL void calls → VoidType (the
        // caller decides: an expression statement checker produces SPY0603, #1603).
        foreach (var (node, type) in operands)
        {
            if (type is VoidType && node != null && node is not NoneLiteral)
            {
                // Check if ALL operands are void calls (multi-operand → VoidType passthrough)
                bool allVoidCalls = operands.Count > 1
                    && operands.All(o => o.Type is VoidType or UnknownType);
                if (allVoidCalls)
                    return SemanticType.Void;

                AddError(
                    $"cannot infer a type for {siteNoun}: this expression produces no value, " +
                    "so there is nothing for " + siteNoun + " to hold. Call it as a statement, " +
                    "or bind the result of an expression that returns one",
                    host.LineStart, host.ColumnStart,
                    code: DiagnosticCodes.Semantic.CannotInferType, span: host.Span);
                return SemanticType.Unknown;
            }
        }

        // ── Unknown propagation (error recovery) ──
        bool allUnknown = true;
        foreach (var (_, type) in operands)
        {
            if (type is not UnknownType)
            {
                allUnknown = false;
                break;
            }
        }
        if (allUnknown)
            return SemanticType.Unknown;

        // ── Arm 1: slot-directed ──
        if (slot != null && slot is not UnknownType)
        {
            foreach (var (node, type) in operands)
            {
                if (type is UnknownType)
                    continue;

                if (type is VoidType && node is NoneLiteral)
                {
                    // None into a slot — the store seam decides (SPY0229 into non-nullable,
                    // SPY0604 into T?, accepted into T | None)
                    if (node != null)
                        CheckStore(position, node, type, slot, host, node.Span);
                    continue;
                }

                if (node != null)
                {
                    CheckStore(position, node, type, slot, host, node.Span);
                }
                else
                {
                    // Node-less operand (e.g. spread element): admissibility only
                    if (!IsAssignable(type, slot))
                    {
                        AddError(
                            $"Operand of type '{type.GetDisplayName()}' cannot be assigned to '{slot.GetDisplayName()}'",
                            host.LineStart, host.ColumnStart,
                            code: DiagnosticCodes.Semantic.TypeMismatch, span: host.Span);
                    }
                }
            }
            return slot;
        }

        // ── Gather typed candidates (not Void, not Unknown) ──
        var candidates = new List<(Expression? Node, SemanticType Type)>();
        bool hasNone = false;
        foreach (var (node, type) in operands)
        {
            if (type is UnknownType)
                continue;
            if (type is VoidType)
            {
                if (node is NoneLiteral)
                    hasNone = true;
                continue;
            }
            candidates.Add((node, type));
        }

        // All operands are None/void/unknown — refuse
        if (candidates.Count == 0)
        {
            if (hasNone)
            {
                AddError(
                    $"cannot infer a type for {siteNoun}: 'None' names no type on its own, " +
                    $"so {siteNoun} has nothing to be. Annotate the target ({NoneSteer(options)})",
                    host.LineStart, host.ColumnStart,
                    code: DiagnosticCodes.Semantic.CannotInferType, span: host.Span);
            }
            else
            {
                AddError(
                    $"cannot infer a type for {siteNoun}",
                    host.LineStart, host.ColumnStart,
                    code: DiagnosticCodes.Semantic.CannotInferType, span: host.Span);
            }
            return SemanticType.Unknown;
        }

        // ── Arm 2: one operand's type accepts all ──
        // For each distinct candidate type, check if every other operand is admissible into it
        var distinctTypes = new Dictionary<string, SemanticType>();
        foreach (var (_, type) in candidates)
        {
            var key = type.CanonicalKey;
            distinctTypes.TryAdd(key, type);
        }

        SemanticType? winner = null;
        int winnerCount = 0;
        foreach (var candidateType in distinctTypes.Values)
        {
            bool acceptsAll = true;
            foreach (var (node, type) in operands)
            {
                if (type is UnknownType)
                    continue;
                if (type is VoidType)
                {
                    if (node is NoneLiteral)
                    {
                        // None into a slot-less candidate: refuse (R-AB)
                        acceptsAll = false;
                        break;
                    }
                    continue; // void call already refused
                }
                if (type.CanonicalKey == candidateType.CanonicalKey)
                    continue;
                if (node != null)
                {
                    // Use the store seam to check admissibility (constant conversion etc.)
                    var verdict = ClassifyStore(position, node, type, candidateType);
                    if (verdict is StoreVerdict.Refused)
                    {
                        acceptsAll = false;
                        break;
                    }
                }
                else if (!IsAssignable(type, candidateType))
                {
                    acceptsAll = false;
                    break;
                }
            }
            if (acceptsAll)
            {
                winner = candidateType;
                winnerCount++;
            }
        }

        if (winnerCount == 1 && winner != null && !hasNone)
        {
            return winner;
        }

        // ── Arm 3: refuse by name ──
        var typeNames = new List<string>();
        foreach (var type in distinctTypes.Values)
        {
            var displayName = type.GetDisplayName();
            typeNames.Add($"'{displayName}'");
        }
        if (hasNone)
            typeNames.Add("'None'");

        var steerText = hasNone ? NoneSteer(options) : (options.AnnotateSteer ?? DefaultSteer);
        AddError(
            $"Cannot infer a type for {siteNoun}: its operands have no best common type " +
            $"({string.Join(", ", typeNames)}) — annotate the target (e.g. {steerText})",
            host.LineStart, host.ColumnStart,
            code: DiagnosticCodes.Semantic.CannotInferType, span: host.Span);
        return SemanticType.Unknown;
    }

    /// <summary>The steer a caller that supplied none falls back to — still real source.</summary>
    private const string DefaultSteer = "'x: T = ...' on the target";

    private static string NoneSteer(BestCommonTypeOptions options)
        => options.NoneAnnotateSteer ?? options.AnnotateSteer ?? BindingNoneSteer("x");

    /// <summary>
    /// Arm 1 of the R-W rule for one operand row of a collection literal, then arms 2–3.
    ///
    /// <para>The contextual element type is the SLOT: when every operand is admitted into it
    /// (<see cref="AdmitCollectionElements"/> already classifies each one through
    /// <c>ClassifyStore</c>, #1671/#1698) the literal adopts it; otherwise the row is decided
    /// slot-less by <see cref="BestCommonType"/>. Five literal rows (list, set, dict key, dict
    /// value, <c>dict(**kw)</c> value) carried this block by copy, so the slot-mismatch path had
    /// five owners that could drift; it has one.</para>
    /// </summary>
    private SemanticType JoinCollectionOperands(
        IReadOnlyList<(Expression? Node, SemanticType Type)> operands,
        SemanticType? expectation,
        Node host,
        string siteNoun,
        BestCommonTypeOptions options)
    {
        if (expectation != null
            && !ContainsTypeParameterType(expectation)
            && AdmitCollectionElements(operands, expectation) != ElementAdmissionResult.Refused)
        {
            return expectation;
        }

        return BestCommonType(operands, null, StorePosition.CollectionElement, host, siteNoun, options);
    }
}
