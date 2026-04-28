using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Emits hint-severity transition diagnostics that inform Python and C# developers
/// about behavioral differences in Sharpy. Each hint targets a specific axis where
/// Sharpy's semantics diverge from one of its source languages — see
/// <c>docs/development/transition_diagnostics.md</c> for the full catalog.
///
/// Currently emitted:
/// <list type="bullet">
///   <item>SPY0470 — <c>len()</c> on a string literal containing non-BMP characters
///         (UTF-16 code-unit count vs. Python's code-point count).</item>
///   <item>SPY0471 — assignment of a struct-typed value to a variable
///         (value-copy semantics vs. Python's reference semantics).</item>
/// </list>
///
/// Hints share suppression with warnings but are NOT promoted to errors under
/// <c>TreatWarningsAsErrors</c>.
/// </summary>
internal sealed class TransitionWarningValidator : ValidatingAstWalker
{
    public override string Name => "TransitionWarning";

    // After NamingConventionValidator (55), before DecoratorValidator (60).
    public override int Order => 56;

    public override void VisitFunctionCall(FunctionCall node)
    {
        CheckUtf16StringLength(node);
        base.VisitFunctionCall(node);
    }

    public override void VisitAssignment(Assignment node)
    {
        CheckStructValueSemantics(node);
        base.VisitAssignment(node);
    }

    // ──────────────────────────────────────────────────────────────────────
    // SPY0470 — len() on a string literal with non-BMP characters
    // ──────────────────────────────────────────────────────────────────────

    private void CheckUtf16StringLength(FunctionCall node)
    {
        // Only positional len(<string-literal>) calls qualify. Keyword args or
        // argument counts != 1 fall through to type-checker validation.
        if (node.Arguments.Length != 1 || node.KeywordArguments.Length != 0)
            return;

        if (node.Arguments[0] is not StringLiteral strLit)
            return;

        if (!IsBuiltinLenCall(node))
            return;

        if (!ContainsNonBmpChar(strLit.Value))
            return;

        var utf16Length = strLit.Value.Length;
        var codePointCount = CountCodePoints(strLit.Value);

        AddHint(
            $"len() on this string returns the UTF-16 code-unit count ({utf16Length}), "
                + $"not the Python-style code-point count ({codePointCount}). "
                + "The literal contains non-BMP characters (surrogate pairs in UTF-16) "
                + "such as supplementary-plane symbols or emoji. "
                + "If you need the code-point count, iterate via runes or use a helper "
                + "such as 'sum(1 for _ in s)' over a code-point enumeration.",
            node.LineStart, node.ColumnStart,
            code: DiagnosticCodes.Validation.Utf16StringLengthHint,
            span: node.Span);
    }

    /// <summary>
    /// Returns true if the call's resolved target is one of the builtin <c>len</c>
    /// overloads. Returns false when the user has shadowed <c>len</c> with their
    /// own symbol or when the call could not be resolved.
    /// </summary>
    private bool IsBuiltinLenCall(FunctionCall call)
    {
        if (call.Function is not Identifier id || id.Name != "len")
            return false;

        var target = Context.SemanticInfo.GetCallTarget(call);
        if (target == null)
            return false;

        var overloads = Context.SymbolTable.BuiltinRegistry.GetFunctionOverloads("len");
        return overloads != null && overloads.Contains(target);
    }

    private static bool ContainsNonBmpChar(string value)
    {
        for (int i = 0; i < value.Length; i++)
        {
            if (char.IsHighSurrogate(value[i]))
                return true;
        }
        return false;
    }

    private static int CountCodePoints(string value)
    {
        int count = 0;
        for (int i = 0; i < value.Length;)
        {
            // A well-formed surrogate pair encodes a single non-BMP code point.
            if (i + 1 < value.Length
                && char.IsHighSurrogate(value[i])
                && char.IsLowSurrogate(value[i + 1]))
            {
                count++;
                i += 2;
            }
            else
            {
                count++;
                i++;
            }
        }
        return count;
    }

    // ──────────────────────────────────────────────────────────────────────
    // SPY0471 — Struct value-semantics on assignment
    // ──────────────────────────────────────────────────────────────────────

    private void CheckStructValueSemantics(Assignment node)
    {
        // Only plain `x = expr` assignments — compound operators (+=, etc.)
        // mutate in place rather than rebinding, so the value-copy framing
        // doesn't apply.
        if (node.Operator != AssignmentOperator.Assign)
            return;

        // Only flag identifier targets ("assigned to another variable"),
        // not member or indexed targets.
        if (node.Target is not Identifier)
            return;

        var rhsType = Context.SemanticInfo.GetExpressionType(node.Value);
        if (rhsType is not UserDefinedType udt)
            return;

        if (udt.Symbol?.TypeKind != TypeKind.Struct)
            return;

        AddHint(
            $"Assigning a value of struct type '{udt.Name}' creates a copy: "
                + "the new variable holds an independent value (struct value-semantics). "
                + "Mutations to one binding will not be visible through the other. "
                + "This differs from Python, where all object bindings are references.",
            node.LineStart, node.ColumnStart,
            code: DiagnosticCodes.Validation.StructValueSemanticsHint,
            span: node.Span);
    }
}
