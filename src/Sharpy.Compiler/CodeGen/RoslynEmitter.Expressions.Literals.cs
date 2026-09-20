using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Shared;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using static Sharpy.Compiler.CodeGen.EmittedTreePrecedence;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// RoslynEmitter partial class: Collection literals, f-strings, walrus
/// </summary>
internal partial class RoslynEmitter
{
    private ExpressionSyntax GenerateEllipsisLiteral()
    {
        // Ellipsis (...) in concrete method bodies generates throw NotImplementedException()
        // Note: For abstract methods/interface methods, the ellipsis is ignored and
        // the method has no body (handled in GenerateClassMethod/GenerateInterfaceMethod)
        return ThrowExpression(
            ObjectCreationExpression(MakeGlobalQualifiedName("System", "NotImplementedException"))
                .WithArgumentList(ArgumentList()));
    }

    private ExpressionSyntax GenerateListLiteral(ListLiteral list)
    {
        // new Sharpy.List<T> { elem1, elem2, elem3 }
        TypeSyntax elementType;
        if (GetExpressionSemanticType(list) is GenericType listSemType &&
            listSemType.Name == BuiltinNames.List &&
            listSemType.TypeArguments.Count > 0 &&
            listSemType.TypeArguments[0] is not UnknownType)
        {
            elementType = _typeMapper.MapSemanticType(listSemType.TypeArguments[0]);
        }
        else
        {
            elementType = PredefinedType(Token(SyntaxKind.ObjectKeyword));
        }

        // E3 stack-collections (opt_stack_collections, #1057): a list literal the pass proved cannot
        // escape (its only use is as a for-statement iterator) emits as a raw array instead of the
        // three-object Sharpy.List construction. StackAllocatedLiterals is empty unless the pass ran, so
        // the default path is byte-identical; the pass never marks a spread literal, so the array form
        // always has plain elements (no spread builder needed).
        if (_context.Ir.StackAllocatedLiterals.Contains(list))
        {
            var arrayElements = GenerateExpressionsInOrder(list.Elements);
            return ArrayCreationExpression(
                    ArrayType(elementType)
                        .WithRankSpecifiers(SingletonList(
                            ArrayRankSpecifier(SingletonSeparatedList<ExpressionSyntax>(OmittedArraySizeExpression())))))
                .WithInitializer(InitializerExpression(
                    SyntaxKind.ArrayInitializerExpression,
                    SeparatedList(arrayElements)));
        }

        var listType = TypeSyntaxMapper.QualifiedGenericName(CSharpTypeNames.SharpyList, elementType);

        // If any element is a spread, use imperative builder pattern
        if (list.Elements.Any(e => e is SpreadElement))
        {
            CollectionTypeRegistry.TryGet(BuiltinNames.List, out var listInfo);
            return GenerateSpreadCollectionBuilder(list.Elements, listType, listInfo!.SpreadMethodName, listInfo.AddMethodName);
        }

        var elements = GenerateExpressionsInOrder(list.Elements);

        return ObjectCreationExpression(listType)
            .WithArgumentList(ArgumentList())
            .WithInitializer(InitializerExpression(
                SyntaxKind.CollectionInitializerExpression,
                SeparatedList(elements)));
    }

    private ExpressionSyntax GenerateDictLiteral(DictLiteral dict)
    {
        // new System.Collections.Generic.Dictionary<K,V> { { key1, value1 }, { key2, value2 } }
        TypeSyntax keyType, valueType;
        if (GetExpressionSemanticType(dict) is GenericType dictSemType &&
            dictSemType.Name == BuiltinNames.Dict &&
            dictSemType.TypeArguments.Count >= 2 &&
            dictSemType.TypeArguments[0] is not UnknownType &&
            dictSemType.TypeArguments[1] is not UnknownType)
        {
            keyType = _typeMapper.MapSemanticType(dictSemType.TypeArguments[0]);
            valueType = _typeMapper.MapSemanticType(dictSemType.TypeArguments[1]);
        }
        else
        {
            keyType = PredefinedType(Token(SyntaxKind.ObjectKeyword));
            valueType = PredefinedType(Token(SyntaxKind.ObjectKeyword));
        }

        var dictType = TypeSyntaxMapper.QualifiedGenericName(CSharpTypeNames.SharpyDict, keyType, valueType);

        // If any entry is a spread (**expr), use imperative builder pattern
        if (dict.Entries.Any(entry => entry.Key == null))
        {
            CollectionTypeRegistry.TryGet(BuiltinNames.Dict, out var dictInfo);
            return GenerateSpreadDictBuilder(dict.Entries, dictType, dictInfo!.SpreadMethodName);
        }

        // key, value, key, value … is the source evaluation order of a dict display.
        var entryOperands = new List<Expression>(dict.Entries.Length * 2);
        foreach (var entry in dict.Entries)
        {
            entryOperands.Add(entry.Key!);
            entryOperands.Add(entry.Value);
        }

        var entryExprs = GenerateExpressionsInOrder(entryOperands);
        var initializers = new List<ExpressionSyntax>(dict.Entries.Length);
        for (int i = 0; i < dict.Entries.Length; i++)
        {
            initializers.Add(InitializerExpression(SyntaxKind.ComplexElementInitializerExpression,
                SeparatedList(new[] { entryExprs[i * 2], entryExprs[(i * 2) + 1] })));
        }

        return ObjectCreationExpression(dictType)
            .WithArgumentList(ArgumentList())
            .WithInitializer(InitializerExpression(
                SyntaxKind.CollectionInitializerExpression,
                SeparatedList<ExpressionSyntax>(initializers)));
    }

    /// <summary>
    /// Emits <c>dict(a=1, b=2)</c> as the collection-initializer form its equivalent literal
    /// <c>{"a": 1, "b": 2}</c> uses (#1220) — the keyword names become string-literal keys. Applies
    /// the <c>dict[str, V]</c> the TypeChecker resolved; it derives nothing.
    /// </summary>
    private ExpressionSyntax GenerateKeywordDictConstruction(FunctionCall call, GenericType dictType)
    {
        var keyType = _typeMapper.MapSemanticType(dictType.TypeArguments[0]);
        var valueType = _typeMapper.MapSemanticType(dictType.TypeArguments[1]);
        var csharpDictType = TypeSyntaxMapper.QualifiedGenericName(
            CSharpTypeNames.SharpyDict, keyType, valueType);

        var kwargValues = GenerateExpressionsInOrder(
            call.KeywordArguments.Select(kwarg => kwarg.Value).ToList());
        var initializers = call.KeywordArguments.Select((kwarg, i) =>
            InitializerExpression(SyntaxKind.ComplexElementInitializerExpression,
                SeparatedList(new[]
                {
                    (ExpressionSyntax)LiteralExpression(
                        SyntaxKind.StringLiteralExpression, Literal(kwarg.Name)),
                    kwargValues[i]
                })));

        return ObjectCreationExpression(csharpDictType)
            .WithArgumentList(ArgumentList())
            .WithInitializer(InitializerExpression(
                SyntaxKind.CollectionInitializerExpression,
                SeparatedList<ExpressionSyntax>(initializers)));
    }

    private ExpressionSyntax GenerateSetLiteral(SetLiteral set)
    {
        // new Sharpy.Set<T> { elem1, elem2, elem3 }
        TypeSyntax elementType;
        if (GetExpressionSemanticType(set) is GenericType setSemType &&
            setSemType.Name == BuiltinNames.Set &&
            setSemType.TypeArguments.Count > 0 &&
            setSemType.TypeArguments[0] is not UnknownType)
        {
            elementType = _typeMapper.MapSemanticType(setSemType.TypeArguments[0]);
        }
        else
        {
            elementType = PredefinedType(Token(SyntaxKind.ObjectKeyword));
        }

        var setType = TypeSyntaxMapper.QualifiedGenericName(CSharpTypeNames.SharpySet, elementType);

        // If any element is a spread, use imperative builder pattern
        if (set.Elements.Any(e => e is SpreadElement))
        {
            CollectionTypeRegistry.TryGet(BuiltinNames.Set, out var setInfo);
            return GenerateSpreadCollectionBuilder(set.Elements, setType, setInfo!.SpreadMethodName, setInfo.AddMethodName);
        }

        var elements = GenerateExpressionsInOrder(set.Elements);

        return ObjectCreationExpression(setType)
            .WithArgumentList(ArgumentList())
            .WithInitializer(InitializerExpression(
                SyntaxKind.CollectionInitializerExpression,
                SeparatedList(elements)));
    }

    private ExpressionSyntax GenerateTupleLiteral(TupleLiteral tuple)
    {
        // Spread handling: (*a, *b) → expand each spread's items into the result tuple
        // Each spread.Value must have a TupleType (enforced in TypeChecker).
        if (tuple.Elements.Any(e => e is SpreadElement))
        {
            var expandedArgs = new List<ArgumentSyntax>();
            foreach (var elem in tuple.Elements)
            {
                if (elem is SpreadElement spread)
                {
                    var spreadType = GetExpressionSemanticType(spread.Value);
                    var spreadExpr = GenerateExpression(spread.Value);

                    if (spreadType is Semantic.TupleType tt)
                    {
                        // Hoist the spread expression into a temp to avoid duplicate evaluation
                        var tempName = GenerateTempVarName("tspread");
                        HoistEvaluation(LocalDeclarationStatement(
                            VariableDeclaration(IdentifierName("var"))
                                .WithVariables(SingletonSeparatedList(
                                    VariableDeclarator(EscapedIdentifier(tempName))
                                        .WithInitializer(EqualsValueClause(spreadExpr))))));

                        for (int i = 0; i < tt.ElementTypes.Count; i++)
                        {
                            expandedArgs.Add(Argument(
                                MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    IdentifierName(tempName),
                                    IdentifierName($"Item{i + 1}"))));
                        }
                    }
                    else
                    {
                        // Defensive fallback — semantic phase should have rejected this.
                        expandedArgs.Add(Argument(spreadExpr));
                    }
                }
                else
                {
                    expandedArgs.Add(Argument(GenerateExpression(elem)));
                }
            }

            // A single-element tuple expression like `(x,)` is not representable as a C#
            // TupleExpression; use ValueTuple.Create(x) instead. C# tuple literals require
            // at least two arguments.
            if (expandedArgs.Count == 1)
            {
                return InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        ValueTupleTypeAccess(),
                        IdentifierName("Create")))
                    .WithArgumentList(ArgumentList(SeparatedList(expandedArgs)));
            }

            if (expandedArgs.Count == 0)
            {
                // Empty tuple — fall back to ValueTuple.Create()
                return InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        ValueTupleTypeAccess(),
                        IdentifierName("Create")))
                    .WithArgumentList(ArgumentList());
            }

            // C#'s tuple literal syntax handles >7 elements automatically by nesting
            // ValueTuple<...,TRest>; no manual handling required.
            return TupleExpression(SeparatedList(expandedArgs));
        }

        var elements = GenerateExpressionsInOrder(tuple.Elements, elem =>
        {
            var expr = GenerateExpression(elem);
            // R-T: a per-element OptionalStoreWrap fact means the element is a payload value
            // stored into a narrowed Optional slot — wrap it, mirroring the plain-store wrap
            // at RoslynEmitter.Statements.Assignments.cs:123 (#1785).
            if (_context.SemanticInfo?.GetOptionalStoreWrap(elem) is { } wrapOpt)
                expr = WrapInOptionalSome(expr, wrapOpt);
            return expr;
        });

        // Named tuple: (x: 1.0, y: 2.0)
        if (!tuple.ElementNames.IsEmpty)
        {
            var namedArgs = elements.Select((expr, i) =>
            {
                var arg = Argument(expr);
                var name = tuple.ElementNames[i];
                if (name != null)
                {
                    arg = arg.WithNameColon(NameColon(name));
                }
                return arg;
            });

            return TupleExpression(SeparatedList(namedArgs));
        }

        // A single-element tuple like `(x,)` is not representable as a C# TupleExpression
        // (tuple literals require at least two elements); use ValueTuple.Create(x).
        if (elements.Length == 1)
        {
            return InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    ValueTupleTypeAccess(),
                    IdentifierName("Create")))
                .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(elements[0]))));
        }

        // Empty tuple: () → System.ValueTuple.Create()
        if (elements.Length == 0)
        {
            return InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    ValueTupleTypeAccess(),
                    IdentifierName("Create")))
                .WithArgumentList(ArgumentList());
        }

        // Unnamed tuple: (elem1, elem2, ...)
        return TupleExpression(SeparatedList(
            elements.Select(e => Argument(e))));
    }

    /// <summary>
    /// Builds the <c>System.ValueTuple</c> receiver for a <c>ValueTuple.Create(...)</c> call as a
    /// real member-access spine (<c>IdentifierName("System") . IdentifierName("ValueTuple")</c>) —
    /// the shape Roslyn's parser produces in expression position. A single
    /// <c>IdentifierName("System.ValueTuple")</c> packs the dotted name into one identifier token
    /// that prints correctly but binds as CS0103 under direct <c>CSharpSyntaxTree.Create</c> handoff
    /// (#1095). The printed text is unchanged, so snapshots stay byte-identical.
    /// </summary>
    private static ExpressionSyntax ValueTupleTypeAccess() =>
        MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            IdentifierName("System"),
            IdentifierName("ValueTuple"));

    private ExpressionSyntax GenerateFString(FStringLiteral fstring)
    {
        // Every hole renders to a STRING — Builtins.Str/Repr/Ascii(v), or Sharpy.PyFormat.Apply(v,
        // spec) for a spec'd hole — so the FormattableString.Invariant($"...") shell is now a no-op
        // that only keeps the snapshot shape recognisable. Pre-generate every operand — hole
        // expressions AND nested spec expressions — in SOURCE order so a hoisting later operand does
        // not reorder an effectful earlier one (#1862, #1853).
        var operands = new List<Expression>();
        CollectInterpolationOperands(fstring.Parts, operands);
        var map = MapInterpolationOperands(operands);

        var parts = new List<InterpolatedStringContentSyntax>();
        foreach (var part in fstring.Parts)
        {
            if (part.Text != null)
            {
                parts.Add(InterpolatedTextFor(part.Text));
            }
            else if (part.Expression != null)
            {
                // '=' self-documenting prefix: print the verbatim captured source (incl. '=')
                // before the value, e.g. f'{x = }' -> "x = 42".
                if (part.IsSelfDocumenting && part.SourceText != null)
                    parts.Add(InterpolatedTextFor(part.SourceText));

                parts.Add(Interpolation(ParenthesizedExpression(RenderInterpolationHole(part, map))));
            }
        }

        var interpolatedString = InterpolatedStringExpression(Token(SyntaxKind.InterpolatedStringStartToken))
            .WithContents(List(parts));

        // Wrap with FormattableString.Invariant($"...") — a no-op over all-string holes, retained so
        // the emitted shape stays recognisable to snapshots.
        return InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName("FormattableString"),
                IdentifierName("Invariant")))
            .WithArgumentList(ArgumentList(SingletonSeparatedList(
                Argument(interpolatedString))));
    }

    /// <summary>
    /// Collects every interpolation operand — each hole expression followed by its spec's nested
    /// hole expressions, recursively — in the order CPython evaluates them (the value, then the
    /// spec's fields, left to right across holes), so <see cref="GenerateExpressionsInOrder"/> can
    /// capture an effectful earlier operand ahead of a later hoisting one.
    /// </summary>
    private static void CollectInterpolationOperands(
        ImmutableArray<FStringPart> parts, List<Expression> operands)
    {
        foreach (var part in parts)
        {
            if (part.Expression == null)
                continue;
            operands.Add(part.Expression);
            if (part.Spec is { } spec)
                CollectInterpolationOperands(spec, operands);
        }
    }

    /// <summary>
    /// Generates <paramref name="operands"/> through the source-order helper and returns a
    /// reference-keyed map from each operand node to its generated C# expression, so the parts can be
    /// re-walked and each hole built from the ALREADY-generated (correctly ordered) expression rather
    /// than regenerated (which would reintroduce the ordering bug and double-evaluate).
    /// </summary>
    private Dictionary<Expression, ExpressionSyntax> MapInterpolationOperands(List<Expression> operands)
    {
        var generated = GenerateExpressionsInOrder(operands);
        var map = new Dictionary<Expression, ExpressionSyntax>(ReferenceEqualityComparer.Instance);
        for (int i = 0; i < operands.Count; i++)
            map[operands[i]] = generated[i];
        return map;
    }

    /// <summary>
    /// Renders one replacement field to a string expression, reading the TypeChecker's
    /// <c>InterpolationLowering</c> (Critical Rule 2 pattern (b)): a plain hole is
    /// <c>Builtins.Str/Repr/Ascii(v)</c>; a spec'd hole is <c>Sharpy.PyFormat.Apply(base, spec)</c>
    /// where <c>base</c> is the converted string (<c>!s/!r/!a</c>) or the raw value, and <c>spec</c>
    /// is a string literal (static) or an interpolated string over the nested spec fields (dynamic).
    /// </summary>
    private ExpressionSyntax RenderInterpolationHole(
        FStringPart part, IReadOnlyDictionary<Expression, ExpressionSyntax> map)
    {
        var value = map[part.Expression!];

        // A bare `None` literal generates C# `null`, which binds Builtins.Str(null) to the more
        // specific Str(string) overload — that returns null and renders empty (#1814). Typing it as
        // object routes it to Str(object)/Repr/Ascii, which spell None. (Typed None holes — object,
        // int | None, int? — already carry an object-ish static type and need no cast.)
        if (part.Expression is NoneLiteral)
            value = Cast(PredefinedType(Token(SyntaxKind.ObjectKeyword)), value);

        var lowering = RequireInterpolationLowering(part.Expression!);
        var kind = lowering.Kind;

        if (part.Spec == null)
        {
            // No spec: str/repr/ascii of the value. Format (no conversion) is plain str().
            return kind switch
            {
                InterpolationKind.Repr => BuiltinConversionCall("Repr", value),
                InterpolationKind.Ascii => BuiltinConversionCall("Ascii", value),
                _ => BuiltinConversionCall("Str", value),
            };
        }

        // A spec: the base is the converted string for an explicit conversion, else the raw value.
        var baseValue = kind switch
        {
            InterpolationKind.Repr => BuiltinConversionCall("Repr", value),
            InterpolationKind.Str => BuiltinConversionCall("Str", value),
            InterpolationKind.Ascii => BuiltinConversionCall("Ascii", value),
            _ => value,
        };

        var specExpr = lowering.SpecIsStatic
            ? MakeStringLiteral(RequireStaticSpec(lowering, part.Expression!))
            : BuildDynamicSpec(part.Spec.Value, map);

        return PyFormatApplyCall(baseValue, specExpr);
    }

    /// <summary>
    /// The <see cref="InterpolationLowering"/> the TypeChecker recorded for a hole, or a throw by
    /// name. <c>TypeChecker.CheckInterpolationPart</c> records one for EVERY hole of an f-string and
    /// a t-string (including nested spec holes) and <see cref="SemanticInfo.MergeFrom"/> carries the
    /// dictionary into the project-level info, so a missing fact is a bug in the recording seam or
    /// the merge — never a licence for the emitter to pick a rendering (Critical Rule 2). The former
    /// <c>?? InterpolationKind.Format</c> / <c>?? ""</c> defaults degraded a dropped fact to
    /// <c>PyFormat.Apply(v, "")</c>, which prints something plausible for most values and so would
    /// have let the #1814 cells pass with the fact missing.
    /// </summary>
    private InterpolationLowering RequireInterpolationLowering(Expression hole)
    {
        var lowering = _context.SemanticInfo?.GetInterpolationLowering(hole);
        if (lowering == null)
        {
            throw new InvalidOperationException(
                "No InterpolationLowering recorded for an interpolation hole at "
                + $"line {hole.LineStart}, column {hole.ColumnStart} — "
                + "TypeChecker.CheckInterpolationPart records one for every f-string/t-string hole "
                + "(#1814, #1815) and SemanticInfo.MergeFrom carries it into the project info; the "
                + "emitter must not guess the rendering.");
        }

        return lowering;
    }

    /// <summary>
    /// The recorded static format spec of a spec'd hole, or a throw by name — a hole WITH a spec
    /// whose lowering says the spec is static always carries its text (see
    /// <see cref="RequireInterpolationLowering"/>).
    /// </summary>
    private static string RequireStaticSpec(InterpolationLowering lowering, Expression hole)
        => lowering.StaticSpec
           ?? throw new InvalidOperationException(
               "An interpolation hole with a static spec carries no StaticSpec text at "
               + $"line {hole.LineStart}, column {hole.ColumnStart} — TypeChecker.CheckInterpolationPart "
               + "records the collected spec text whenever the part has a spec (#1814).");

    /// <summary>
    /// Builds a dynamic format spec — one containing nested replacement fields — as an interpolated
    /// string over its parts: literal text verbatim and each nested field rendered (to a string) by
    /// <see cref="RenderInterpolationHole"/>, recursively (so depth-2 nesting works).
    /// </summary>
    private ExpressionSyntax BuildDynamicSpec(
        ImmutableArray<FStringPart> spec,
        IReadOnlyDictionary<Expression, ExpressionSyntax> map)
    {
        var contents = new List<InterpolatedStringContentSyntax>();
        foreach (var specPart in spec)
        {
            if (specPart.Expression != null)
                contents.Add(Interpolation(ParenthesizedExpression(RenderInterpolationHole(specPart, map))));
            else if (specPart.Text != null)
                contents.Add(InterpolatedTextFor(specPart.Text));
        }

        return InterpolatedStringExpression(Token(SyntaxKind.InterpolatedStringStartToken))
            .WithContents(List(contents));
    }

    /// <summary>
    /// Builds a literal-text segment of an interpolated string, re-escaping C# interpolation
    /// metacharacters (the lexer already collapsed Python's <c>{{</c>/<c>}}</c>, so braces are
    /// re-doubled here).
    /// </summary>
    private static InterpolatedStringContentSyntax InterpolatedTextFor(string text)
    {
        var sourceText = EscapeForInterpolatedStringSource(text)
            .Replace("{", "{{", StringComparison.Ordinal)
            .Replace("}", "}}", StringComparison.Ordinal);
        return InterpolatedStringText()
            .WithTextToken(Token(
                TriviaList(),
                SyntaxKind.InterpolatedStringTextToken,
                sourceText,
                text,
                TriviaList()));
    }

    /// <summary>
    /// <c>global::Sharpy.Builtins.&lt;method&gt;(value)</c> — the str/repr/ascii conversion a hole
    /// applies to its value (the same functions <c>str()</c>/<c>repr()</c>/<c>ascii()</c> use).
    /// </summary>
    private ExpressionSyntax BuiltinConversionCall(string method, ExpressionSyntax value) =>
        InvocationExpression(MakeGlobalQualifiedName("Sharpy", "Builtins", method))
            .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(value))));

    /// <summary>
    /// <c>global::Sharpy.PyFormat.Apply(value, spec)</c> — the one Python format-spec engine, shared
    /// with <c>str.format</c> and <c>format()</c>.
    /// </summary>
    private ExpressionSyntax PyFormatApplyCall(ExpressionSyntax value, ExpressionSyntax spec) =>
        InvocationExpression(MakeGlobalQualifiedName("Sharpy", "PyFormat", "Apply"))
            .WithArgumentList(ArgumentList(SeparatedList(new[] { Argument(value), Argument(spec) })));

    private static string EscapeForInterpolatedStringSource(string text)
    {
        return text.Replace("\\", "\\\\", StringComparison.Ordinal)
                   .Replace("\n", "\\n", StringComparison.Ordinal)
                   .Replace("\r", "\\r", StringComparison.Ordinal)
                   .Replace("\t", "\\t", StringComparison.Ordinal)
                   .Replace("\0", "\\0", StringComparison.Ordinal)
                   .Replace("\a", "\\a", StringComparison.Ordinal)
                   .Replace("\b", "\\b", StringComparison.Ordinal)
                   .Replace("\f", "\\f", StringComparison.Ordinal)
                   .Replace("\v", "\\v", StringComparison.Ordinal)
                   .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    /// <summary>
    /// The spec argument passed to a t-string <c>Interpolation</c> (PEP 750): an empty literal when
    /// the field had no <c>:</c>, a static-spec literal, or an interpolated string built from the
    /// nested spec fields for a dynamic spec.
    /// </summary>
    private ExpressionSyntax InterpolationSpecArgument(
        FStringPart part, IReadOnlyDictionary<Expression, ExpressionSyntax> map)
    {
        if (part.Spec == null)
            return MakeStringLiteral(string.Empty);

        var lowering = RequireInterpolationLowering(part.Expression!);
        return lowering.SpecIsStatic
            ? MakeStringLiteral(RequireStaticSpec(lowering, part.Expression!))
            : BuildDynamicSpec(part.Spec.Value, map);
    }

    // ============================================================
    // Template string (t"...") emission
    // ============================================================

    /// <summary>
    /// Generates code for a template string literal (PEP 750).
    /// t"Hello {name}" generates:
    /// new global::Sharpy.Template(
    ///     new string[] { "Hello ", "" },
    ///     new global::Sharpy.Interpolation[] { new global::Sharpy.Interpolation(name, "name", "") }
    /// )
    /// </summary>
    private ExpressionSyntax GenerateTString(TStringLiteral tstring)
    {
        // Build the strings array and interpolations array from parts.
        // For N interpolation expressions, we need N+1 string segments.
        var stringElements = new List<ExpressionSyntax>();
        var interpolationElements = new List<ExpressionSyntax>();

        // Track current text accumulator (for merging adjacent text parts)
        var currentText = string.Empty;

        // Pre-generate every operand — hole values AND nested spec fields — in SOURCE order (#1862).
        var operands = new List<Expression>();
        CollectInterpolationOperands(tstring.Parts, operands);
        var map = MapInterpolationOperands(operands);

        foreach (var part in tstring.Parts)
        {
            if (part.Text != null)
            {
                // Accumulate text segments
                currentText += part.Text;
            }
            else if (part.Expression != null)
            {
                // Flush accumulated text as a string element
                stringElements.Add(MakeStringLiteral(currentText));
                currentText = string.Empty;

                // The interpolation value, generated in source order.
                var valueExpr = map[part.Expression];

                // Box value types to object for the Interpolation constructor
                var exprType = GetExpressionSemanticType(part.Expression);
                if (exprType is BuiltinType { IsValueType: true })
                {
                    valueExpr = Cast(
                        PredefinedType(Token(SyntaxKind.ObjectKeyword)),
                        valueExpr);
                }

                // Derive expression text from the AST node
                var exprText = DeriveExpressionText(part.Expression);

                // The spec is a string evaluated at construction (PEP 750): a literal for a static
                // spec, an interpolated string over the nested fields for a dynamic one. Empty when
                // the field had no ':'. Interpolation.ToString() applies it through PyFormat.Apply.
                var specArg = InterpolationSpecArgument(part, map);

                // new global::Sharpy.Interpolation(value, "exprText", spec)
                var interpolationExpr = ObjectCreationExpression(
                    QualifiedName(
                        AliasQualifiedName(
                            IdentifierName(Token(SyntaxKind.GlobalKeyword)),
                            IdentifierName("Sharpy")),
                        IdentifierName("Interpolation")))
                    .WithArgumentList(ArgumentList(SeparatedList(new[]
                    {
                        Argument(valueExpr),
                        Argument(MakeStringLiteral(exprText)),
                        Argument(specArg)
                    })));

                interpolationElements.Add(interpolationExpr);
            }
        }

        // Flush any remaining text (the trailing string after the last interpolation)
        stringElements.Add(MakeStringLiteral(currentText));

        // Build: new string[] { "s0", "s1", ... }
        var stringsArray = ArrayCreationExpression(
            ArrayType(PredefinedType(Token(SyntaxKind.StringKeyword)))
                .WithRankSpecifiers(SingletonList(
                    ArrayRankSpecifier(SingletonSeparatedList<ExpressionSyntax>(
                        OmittedArraySizeExpression())))))
            .WithInitializer(InitializerExpression(
                SyntaxKind.ArrayInitializerExpression,
                SeparatedList(stringElements)));

        // Build: new global::Sharpy.Interpolation[] { ... }
        var interpolationsArray = ArrayCreationExpression(
            ArrayType(
                QualifiedName(
                    AliasQualifiedName(
                        IdentifierName(Token(SyntaxKind.GlobalKeyword)),
                        IdentifierName("Sharpy")),
                    IdentifierName("Interpolation")))
                .WithRankSpecifiers(SingletonList(
                    ArrayRankSpecifier(SingletonSeparatedList<ExpressionSyntax>(
                        OmittedArraySizeExpression())))))
            .WithInitializer(InitializerExpression(
                SyntaxKind.ArrayInitializerExpression,
                SeparatedList(interpolationElements)));

        // Build: new global::Sharpy.Template(stringsArray, interpolationsArray)
        return ObjectCreationExpression(
            QualifiedName(
                AliasQualifiedName(
                    IdentifierName(Token(SyntaxKind.GlobalKeyword)),
                    IdentifierName("Sharpy")),
                IdentifierName("Template")))
            .WithArgumentList(ArgumentList(SeparatedList(new[]
            {
                Argument(stringsArray),
                Argument(interpolationsArray)
            })));
    }

    /// <summary>
    /// Derives a human-readable expression text from an AST expression node.
    /// Used for the Interpolation.Expression field in template strings.
    /// </summary>
    private static string DeriveExpressionText(Expression expr)
    {
        return expr switch
        {
            Identifier id => id.Name,
            MemberAccess ma => $"{DeriveExpressionText(ma.Object)}.{ma.Member}",
            FunctionCall call => $"{DeriveExpressionText(call.Function)}()",
            BinaryOp bin => $"{DeriveExpressionText(bin.Left)} {bin.Operator} {DeriveExpressionText(bin.Right)}",
            UnaryOp unary => $"{unary.Operator}{DeriveExpressionText(unary.Operand)}",
            IntegerLiteral intLit => intLit.Value,
            FloatLiteral floatLit => floatLit.Value,
            StringLiteral strLit => $"\"{strLit.Value}\"",
            BooleanLiteral boolLit => boolLit.Value ? "True" : "False",
            IndexAccess idx => $"{DeriveExpressionText(idx.Object)}[{DeriveExpressionText(idx.Index)}]",
            _ => "<expr>"
        };
    }

    /// <summary>
    /// Creates a string literal expression from a string value.
    /// </summary>
    private static ExpressionSyntax MakeStringLiteral(string value)
    {
        return LiteralExpression(
            SyntaxKind.StringLiteralExpression,
            Literal(value));
    }

    // Walrus operator (:=) emission
    // ============================================================

    /// <summary>
    /// Generates code for a walrus/assignment expression (name := value).
    /// In normal mode, emits a hoisted <c>var name = value;</c> declaration that is prepended
    /// before the containing statement, and returns an <c>IdentifierName</c> referencing the variable.
    /// In inline mode (while-loop conditions), emits a typed pre-declaration and returns an
    /// inline <c>(varName = value)</c> assignment expression so it is re-evaluated each iteration.
    /// </summary>
    private ExpressionSyntax GenerateWalrusExpression(WalrusExpression walrus)
    {
        // Generate the value expression
        var value = GenerateExpression(walrus.Value);

        // The binding is a recorded fact (#1560 R2): the checker linked the walrus to its symbol
        // and classified it. A rebind assigns to the chain head's one C# local — inline in every
        // mode, since nothing needs declaring — and a fresh binding declares it below. The
        // name-based arm is for a walrus the checker never saw (AST-only unit tests).
        var info = _context.SemanticInfo;
        var symbol = info?.GetWalrusSymbol(walrus);
        string varName;
        if (symbol != null)
        {
            var binding = info!.GetTargetBinding(walrus)
                ?? throw new InvalidOperationException(
                    $"No TargetBinding recorded for walrus '{walrus.Target}' at {walrus.LineStart}:{walrus.ColumnStart}");
            varName = GetCSharpNameForSymbol(symbol);
            if (binding.Kind == TargetBindingKind.Rebinds)
            {
                var assignExpr = ParenthesizedExpression(
                    AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        StoreTargetName(walrus, varName),
                        value));
                return ApplyNarrowedReadLowering(walrus, assignExpr);
            }
        }
        else
        {
            varName = GetMangledVariableName(walrus.Target, isNewDeclaration: true,
                walrus.IsNameBacktickEscaped);
        }

        // Pre-declare the walrus variable at scope level (HoistDeclaration), then return
        // an inline assignment expression. This pattern lets manufactured sinks (and/or,
        // ternary, while) capture the assignment inside their conditional branch while
        // keeping the variable visible in the outer scope (#1680).
        var hoistValueType = GetExpressionSemanticType(walrus.Value);
        // Bare declaration: must use explicit type, not 'var' (CS0818)
        var declType = symbol?.Type != null
            ? _typeMapper.MapSemanticType(symbol.Type)
            : hoistValueType != null
                ? _typeMapper.MapSemanticType(hoistValueType)
                : IdentifierName("object");

        HoistDeclaration(
            LocalDeclarationStatement(
                VariableDeclaration(declType)
                    .WithVariables(SingletonSeparatedList(
                        VariableDeclarator(EscapedIdentifier(varName))
                            .WithInitializer(EqualsValueClause(
                                PostfixUnaryExpression(
                                    SyntaxKind.SuppressNullableWarningExpression,
                                    LiteralExpression(SyntaxKind.DefaultLiteralExpression))))))));

        return ParenthesizedExpression(
            AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                EscapedIdentifierName(varName),
                value));
    }

    /// <summary>
    /// Generates an imperative builder pattern for list/set literals containing spread elements.
    /// Hoists: var __t = new CollectionType(); then for each element either __t.Add(x) or
    /// __t.ExtendMethod(spread). Returns the temp variable identifier.
    /// </summary>
    /// <param name="elements">The collection elements (mix of regular and SpreadElement)</param>
    /// <param name="collectionType">The fully qualified C# collection type (e.g., Sharpy.List&lt;int&gt;)</param>
    /// <param name="spreadMethod">Method to call for spread elements ("Extend" or "UnionWith")</param>
    /// <param name="addMethod">Method to call for individual elements ("Add")</param>
    private ExpressionSyntax GenerateSpreadCollectionBuilder(
        ImmutableArray<Expression> elements,
        TypeSyntax collectionType,
        string spreadMethod,
        string addMethod)
    {
        var tempName = GenerateTempVarName("spread");

        // var __spread_N = new CollectionType();
        HoistEvaluation(LocalDeclarationStatement(
            VariableDeclaration(IdentifierName("var"))
                .WithVariables(SingletonSeparatedList(
                    VariableDeclarator(EscapedIdentifier(tempName))
                        .WithInitializer(EqualsValueClause(
                            ObjectCreationExpression(collectionType)
                                .WithArgumentList(ArgumentList())))))));

        foreach (var element in elements)
        {
            if (element is SpreadElement spread)
            {
                var spreadType = GetExpressionSemanticType(spread.Value);
                if (spreadType is Semantic.TupleType tupleType)
                {
                    // Tuple spread: expand to individual .Add(tup.ItemN) calls
                    var tupTemp = GenerateTempVarName("tspread");
                    HoistEvaluation(LocalDeclarationStatement(
                        VariableDeclaration(IdentifierName("var"))
                            .WithVariables(SingletonSeparatedList(
                                VariableDeclarator(EscapedIdentifier(tupTemp))
                                    .WithInitializer(EqualsValueClause(GenerateExpression(spread.Value)))))));
                    for (int i = 0; i < tupleType.ElementTypes.Count; i++)
                    {
                        HoistEvaluation(ExpressionStatement(
                            InvocationExpression(
                                MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    IdentifierName(tempName),
                                    IdentifierName(addMethod)))
                                .AddArgumentListArguments(Argument(
                                    MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        IdentifierName(tupTemp),
                                        IdentifierName($"Item{i + 1}"))))));
                    }
                }
                else
                {
                    // __spread_N.Extend(spreadValue) or __spread_N.UnionWith(spreadValue)
                    HoistEvaluation(ExpressionStatement(
                        InvocationExpression(
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                IdentifierName(tempName),
                                IdentifierName(spreadMethod)))
                            .AddArgumentListArguments(Argument(GenerateExpression(spread.Value)))));
                }
            }
            else
            {
                // __spread_N.Add(element)
                HoistEvaluation(ExpressionStatement(
                    InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName(tempName),
                            IdentifierName(addMethod)))
                        .AddArgumentListArguments(Argument(GenerateExpression(element)))));
            }
        }

        return IdentifierName(tempName);
    }

    /// <summary>
    /// Generates an imperative builder pattern for dict literals containing spread entries (**expr).
    /// Hoists: var __t = new DictType(); then for each entry either __t[key] = value or
    /// __t.Update(spread). Returns the temp variable identifier.
    /// </summary>
    private ExpressionSyntax GenerateSpreadDictBuilder(
        ImmutableArray<DictEntry> entries,
        TypeSyntax dictType,
        string spreadMethodName)
    {
        var tempName = GenerateTempVarName("spread");

        // var __spread_N = new DictType();
        HoistEvaluation(LocalDeclarationStatement(
            VariableDeclaration(IdentifierName("var"))
                .WithVariables(SingletonSeparatedList(
                    VariableDeclarator(EscapedIdentifier(tempName))
                        .WithInitializer(EqualsValueClause(
                            ObjectCreationExpression(dictType)
                                .WithArgumentList(ArgumentList())))))));

        foreach (var entry in entries)
        {
            if (entry.Key == null)
            {
                // __spread_N.{spreadMethodName}(spreadDict)
                HoistEvaluation(ExpressionStatement(
                    InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName(tempName),
                            IdentifierName(spreadMethodName)))
                        .AddArgumentListArguments(Argument(GenerateExpression(entry.Value)))));
            }
            else
            {
                // key and value are sibling operands of one dict entry — order them left-to-right so
                // a hoist producer in the value cannot run before an effectful key (#1853).
                var entryOperands = GenerateExpressionsInOrder(
                    new Expression[] { entry.Key!, entry.Value });
                // __spread_N[key] = value
                HoistEvaluation(ExpressionStatement(
                    AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        ElementAccessExpression(IdentifierName(tempName))
                            .WithArgumentList(BracketedArgumentList(
                                SingletonSeparatedList(Argument(entryOperands[0])))),
                        entryOperands[1])));
            }
        }

        return IdentifierName(tempName);
    }

}
