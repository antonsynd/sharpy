using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic;

internal partial class TypeChecker
{
    internal readonly record struct BestCommonTypeOptions(
        bool TruthinessPosition = false,
        string? AnnotateSteer = null);

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
                    $"so {siteNoun} has nothing to be. Annotate the binding with the type it " +
                    $"will hold ({siteNoun}: T? = None() for a Sharpy optional, " +
                    $"{siteNoun}: T | None = None for a .NET-nullable reference)",
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
            typeNames.Add($"'{type.GetDisplayName()}'");
        if (hasNone)
            typeNames.Add("'None'");

        var steerText = options.AnnotateSteer ?? $"'{siteNoun}: T = ...'";
        AddError(
            $"Cannot infer a type for {siteNoun}: its operands have no best common type " +
            $"({string.Join(", ", typeNames)}) — annotate the target (e.g. {steerText})",
            host.LineStart, host.ColumnStart,
            code: DiagnosticCodes.Semantic.CannotInferType, span: host.Span);
        return SemanticType.Unknown;
    }
}
