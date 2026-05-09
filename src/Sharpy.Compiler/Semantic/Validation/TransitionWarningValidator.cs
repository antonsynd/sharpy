using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Emits hint-severity transition diagnostics that inform Python and C# developers
/// about behavioral differences in Sharpy. Each hint targets a specific axis where
/// Sharpy's semantics diverge from one of its source languages — see
/// <c>docs/deviations.yaml</c> for the full catalog.
///
/// Currently emitted:
/// <list type="bullet">
///   <item>SPY0470 — <c>len()</c> on a string literal containing non-BMP characters
///         (UTF-16 code-unit count vs. Python's code-point count).</item>
///   <item>SPY0471 — assignment of a struct-typed value to a variable
///         (value-copy semantics vs. Python's reference semantics).</item>
///   <item>SPY0475 — <c>isinstance(x, (T1, T2))</c> tuple-of-types form
///         (Sharpy accepts only a single type argument).</item>
///   <item>SPY0477 — <c>@static</c> on a class/struct/interface method that already
///         lacks a <c>self</c> parameter (decorator is unnecessary; Sharpy auto-detects
///         static methods). Note: <c>@staticmethod</c> is a hard error via
///         DecoratorValidator, so it is not flagged here.</item>
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

    /// <summary>
    /// Tracks nesting inside class/struct/interface bodies. Used to scope
    /// the @static / @staticmethod hint to type members only — at module
    /// level, those decorators have different meaning and DecoratorValidator
    /// already governs validity.
    /// </summary>
    private int _typeDepth;

    public override void Validate(Module module, SemanticContext context)
    {
        _typeDepth = 0;
        base.Validate(module, context);
    }

    public override void VisitClassDef(ClassDef node)
    {
        _typeDepth++;
        try
        { base.VisitClassDef(node); }
        finally { _typeDepth--; }
    }

    public override void VisitStructDef(StructDef node)
    {
        _typeDepth++;
        try
        { base.VisitStructDef(node); }
        finally { _typeDepth--; }
    }

    public override void VisitInterfaceDef(InterfaceDef node)
    {
        _typeDepth++;
        try
        { base.VisitInterfaceDef(node); }
        finally { _typeDepth--; }
    }

    public override void VisitFunctionCall(FunctionCall node)
    {
        CheckUtf16StringLength(node);
        CheckIsinstanceSingleType(node);
        base.VisitFunctionCall(node);
    }

    public override void VisitAssignment(Assignment node)
    {
        CheckStructValueSemantics(node);
        base.VisitAssignment(node);
    }

    public override void VisitFunctionDef(FunctionDef node)
    {
        CheckUnnecessaryStaticDecorator(node);
        base.VisitFunctionDef(node);
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
        if (target != null)
        {
            var overloads = Context.Builtins.GetFunctionOverloads("len");
            return overloads != null && overloads.Contains(target);
        }

        // Builtin functions handled by BuiltinReturnTypeInference don't get a
        // call target recorded. Fall back to identifier resolution to detect shadowing.
        var resolved = Context.SemanticInfo.GetIdentifierSymbol(id);
        var builtinOverloads = Context.Builtins.GetFunctionOverloads("len");
        if (builtinOverloads == null || builtinOverloads.Count == 0)
            return false;

        if (resolved is VariableSymbol)
            return false;

        if (resolved is FunctionSymbol fs)
            return builtinOverloads.Contains(fs);

        return true;
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

    // ──────────────────────────────────────────────────────────────────────
    // SPY0475 — isinstance(x, (T1, T2)) tuple-of-types form
    // ──────────────────────────────────────────────────────────────────────

    private void CheckIsinstanceSingleType(FunctionCall node)
    {
        // Pattern: isinstance(<expr>, (T1, T2[, ...])).
        // We match by syntax (Identifier "isinstance" + TupleLiteral as second arg)
        // because the call typically fails to resolve a target (no overload accepts
        // a tuple), so SemanticInfo.GetCallTarget would return null and a strict
        // builtin-resolution check would suppress the hint exactly when it's needed.
        if (node.Function is not Identifier id || id.Name != BuiltinNames.Isinstance)
            return;

        // Skip if the user has shadowed `isinstance` with their own non-builtin
        // value — the resolved identifier symbol would be a VariableSymbol then.
        var resolvedSymbol = Context.SemanticInfo.GetIdentifierSymbol(id);
        if (resolvedSymbol is VariableSymbol)
            return;

        if (node.Arguments.Length < 2)
            return;

        if (node.Arguments[1] is not TupleLiteral tuple)
            return;

        var typeNames = string.Join(", ", tuple.Elements.Select(DescribeTypeArg));

        AddHint(
            $"isinstance() in Sharpy accepts only a single type argument, but a tuple of "
                + $"{tuple.Elements.Length} types ({typeNames}) was passed. "
                + "Unlike Python's `isinstance(x, (A, B))`, Sharpy keeps the form single-typed "
                + "so that successful checks narrow to one concrete type. "
                + "Combine multiple checks with `or` (e.g., "
                + "`isinstance(x, A) or isinstance(x, B)`), or use a tagged union with `match`.",
            node.LineStart, node.ColumnStart,
            code: DiagnosticCodes.Validation.SingleIsinstanceTypeHint,
            span: node.Span);
    }

    /// <summary>
    /// Best-effort textual rendering of a type-position expression inside the
    /// rejected <c>isinstance</c> tuple literal. Falls back to a placeholder
    /// when the expression isn't a simple identifier or member access.
    /// </summary>
    private static string DescribeTypeArg(Expression expr)
    {
        return expr switch
        {
            Identifier id => id.Name,
            MemberAccess ma => $"{DescribeTypeArg(ma.Object)}.{ma.Member}",
            _ => "?",
        };
    }

    // ──────────────────────────────────────────────────────────────────────
    // SPY0477 — Unnecessary @static on a method without self
    // ──────────────────────────────────────────────────────────────────────

    private void CheckUnnecessaryStaticDecorator(FunctionDef node)
    {
        if (node.Decorators.Length == 0)
            return;

        // Only emit for class/struct/interface members. At module level the
        // decorator has a different role (e.g., on a module-level field), and
        // DecoratorValidator already governs validity there.
        if (_typeDepth == 0)
            return;

        // The hint applies precisely when the method is already static-shaped:
        // either it has no parameters, or its first parameter is not the
        // implicit `self` (untyped first parameter named "self").
        if (HasSelfParameter(node))
            return;

        // Only match @static — @staticmethod is already rejected as a hard error
        // by DecoratorValidator (Order 60), so emitting an advisory hint here would
        // contradict the error diagnostic.
        Decorator? staticDecorator = null;
        foreach (var dec in node.Decorators)
        {
            if (dec.Name == DecoratorNames.Static)
            {
                staticDecorator = dec;
                break;
            }
        }

        if (staticDecorator is null)
            return;

        AddHint(
            $"@static is unnecessary on '{node.Name}': the method already lacks a "
                + "'self' parameter, and Sharpy automatically treats methods without a "
                + "'self' first parameter as static. "
                + "You can drop the decorator without changing behavior. "
                + "(Python's @staticmethod and C#'s 'static' keyword are explicit; "
                + "Sharpy infers the same fact from the parameter list.)",
            staticDecorator.LineStart, staticDecorator.ColumnStart,
            code: DiagnosticCodes.Validation.UnnecessaryStaticDecoratorHint,
            span: staticDecorator.Span);
    }

    /// <summary>
    /// Returns true when the function definition begins with the implicit
    /// instance receiver: a first parameter named <c>self</c> with no type
    /// annotation. A typed <c>self: T</c> first parameter is treated as a
    /// regular parameter, in which case the method is considered static-shaped.
    /// </summary>
    private static bool HasSelfParameter(FunctionDef node)
    {
        if (node.Parameters.Length == 0)
            return false;

        var first = node.Parameters[0];
        return first.Name == "self" && first.Type == null;
    }
}
