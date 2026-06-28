using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Shared;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

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
        // Prefer target type annotation if available (e.g., list[int] = [...])
        TypeSyntax elementType;
        if (_targetTypeContext != null &&
            _targetTypeContext.Name == BuiltinNames.List &&
            _targetTypeContext.TypeArguments.Length > 0)
        {
            // Use the declared element type from the target type annotation
            elementType = _typeMapper.MapType(_targetTypeContext.TypeArguments[0]);
        }
        else if (GetExpressionSemanticType(list) is GenericType listSemType &&
                 listSemType.Name == BuiltinNames.List &&
                 listSemType.TypeArguments.Count > 0 &&
                 listSemType.TypeArguments[0] is not UnknownType)
        {
            // Use the type inferred by the TypeChecker via SemanticInfo
            elementType = _typeMapper.MapSemanticType(listSemType.TypeArguments[0]);
        }
        else
        {
            // Fall back to inference from elements
            elementType = _typeMapper.InferElementType(list.Elements);
        }

        var listType = TypeSyntaxMapper.QualifiedGenericName(CSharpTypeNames.SharpyList, elementType);

        // If any element is a spread, use imperative builder pattern
        if (list.Elements.Any(e => e is SpreadElement))
        {
            CollectionTypeRegistry.TryGet(BuiltinNames.List, out var listInfo);
            return GenerateSpreadCollectionBuilder(list.Elements, listType, listInfo!.SpreadMethodName, listInfo.AddMethodName);
        }

        var elements = list.Elements.Select(elem => GenerateWithNestedTargetType(elem, _targetTypeContext));

        return ObjectCreationExpression(listType)
            .WithArgumentList(ArgumentList())
            .WithInitializer(InitializerExpression(
                SyntaxKind.CollectionInitializerExpression,
                SeparatedList(elements)));
    }

    private ExpressionSyntax GenerateDictLiteral(DictLiteral dict)
    {
        // new System.Collections.Generic.Dictionary<K,V> { { key1, value1 }, { key2, value2 } }
        // v0.1.x uses .NET types directly per phases.md
        // Prefer target type annotation if available (e.g., dict[str, int] = {...})
        TypeSyntax keyType, valueType;
        if (_targetTypeContext != null &&
            _targetTypeContext.Name == BuiltinNames.Dict &&
            _targetTypeContext.TypeArguments.Length >= 2)
        {
            keyType = _typeMapper.MapType(_targetTypeContext.TypeArguments[0]);
            valueType = _typeMapper.MapType(_targetTypeContext.TypeArguments[1]);
        }
        else if (GetExpressionSemanticType(dict) is GenericType dictSemType &&
                 dictSemType.Name == BuiltinNames.Dict &&
                 dictSemType.TypeArguments.Count >= 2 &&
                 dictSemType.TypeArguments[0] is not UnknownType &&
                 dictSemType.TypeArguments[1] is not UnknownType)
        {
            // Use the types inferred by the TypeChecker via SemanticInfo
            keyType = _typeMapper.MapSemanticType(dictSemType.TypeArguments[0]);
            valueType = _typeMapper.MapSemanticType(dictSemType.TypeArguments[1]);
        }
        else
        {
            keyType = _typeMapper.InferElementType(dict.Entries.Where(e => e.Key != null).Select(e => e.Key!));
            valueType = _typeMapper.InferElementType(dict.Entries.Select(e => e.Value));
        }

        var dictType = TypeSyntaxMapper.QualifiedGenericName(CSharpTypeNames.SharpyDict, keyType, valueType);

        // If any entry is a spread (**expr), use imperative builder pattern
        if (dict.Entries.Any(entry => entry.Key == null))
        {
            CollectionTypeRegistry.TryGet(BuiltinNames.Dict, out var dictInfo);
            return GenerateSpreadDictBuilder(dict.Entries, dictType, dictInfo!.SpreadMethodName);
        }

        var initializers = dict.Entries.Select(entry =>
            InitializerExpression(SyntaxKind.ComplexElementInitializerExpression,
                SeparatedList(new[]
                {
                    GenerateExpression(entry.Key!),
                    GenerateExpression(entry.Value)
                })));

        return ObjectCreationExpression(dictType)
            .WithArgumentList(ArgumentList())
            .WithInitializer(InitializerExpression(
                SyntaxKind.CollectionInitializerExpression,
                SeparatedList<ExpressionSyntax>(initializers)));
    }

    private ExpressionSyntax GenerateSetLiteral(SetLiteral set)
    {
        // new Sharpy.Set<T> { elem1, elem2, elem3 }
        // Prefer target type annotation if available (e.g., set[int] = {...})
        TypeSyntax elementType;
        if (_targetTypeContext != null &&
            _targetTypeContext.Name == BuiltinNames.Set &&
            _targetTypeContext.TypeArguments.Length > 0)
        {
            elementType = _typeMapper.MapType(_targetTypeContext.TypeArguments[0]);
        }
        else if (GetExpressionSemanticType(set) is GenericType setSemType &&
                 setSemType.Name == BuiltinNames.Set &&
                 setSemType.TypeArguments.Count > 0 &&
                 setSemType.TypeArguments[0] is not UnknownType)
        {
            // Use the type inferred by the TypeChecker via SemanticInfo
            elementType = _typeMapper.MapSemanticType(setSemType.TypeArguments[0]);
        }
        else
        {
            elementType = _typeMapper.InferElementType(set.Elements);
        }

        var setType = TypeSyntaxMapper.QualifiedGenericName(CSharpTypeNames.SharpySet, elementType);

        // If any element is a spread, use imperative builder pattern
        if (set.Elements.Any(e => e is SpreadElement))
        {
            CollectionTypeRegistry.TryGet(BuiltinNames.Set, out var setInfo);
            return GenerateSpreadCollectionBuilder(set.Elements, setType, setInfo!.SpreadMethodName, setInfo.AddMethodName);
        }

        var elements = set.Elements.Select(elem => GenerateWithNestedTargetType(elem, _targetTypeContext));

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
                        _hoistedStatements.Add(LocalDeclarationStatement(
                            VariableDeclaration(IdentifierName("var"))
                                .WithVariables(SingletonSeparatedList(
                                    VariableDeclarator(Identifier(tempName))
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
                        IdentifierName("System.ValueTuple"),
                        IdentifierName("Create")))
                    .WithArgumentList(ArgumentList(SeparatedList(expandedArgs)));
            }

            if (expandedArgs.Count == 0)
            {
                // Empty tuple — fall back to ValueTuple.Create()
                return InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName("System.ValueTuple"),
                        IdentifierName("Create")))
                    .WithArgumentList(ArgumentList());
            }

            // C#'s tuple literal syntax handles >7 elements automatically by nesting
            // ValueTuple<...,TRest>; no manual handling required.
            return TupleExpression(SeparatedList(expandedArgs));
        }

        var elements = tuple.Elements.Select(GenerateExpression).ToArray();

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
                    IdentifierName("System.ValueTuple"),
                    IdentifierName("Create")))
                .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(elements[0]))));
        }

        // Empty tuple: () → System.ValueTuple.Create()
        if (elements.Length == 0)
        {
            return InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName("System.ValueTuple"),
                    IdentifierName("Create")))
                .WithArgumentList(ArgumentList());
        }

        // Unnamed tuple: (elem1, elem2, ...)
        return TupleExpression(SeparatedList(
            elements.Select(e => Argument(e))));
    }

    private ExpressionSyntax GenerateFString(FStringLiteral fstring)
    {
        // f"Hello {name}" -> $"Hello {name}"
        var parts = new List<InterpolatedStringContentSyntax>();

        foreach (var part in fstring.Parts)
        {
            if (part.Text != null)
            {
                // Escape literal braces for C# interpolated strings:
                // The lexer already converts Python's {{ → { and }} → },
                // so we re-escape { → {{ and } → }} for C# interpolation syntax.
                var sourceText = EscapeForInterpolatedStringSource(
                    part.Text).Replace("{", "{{", StringComparison.Ordinal)
                              .Replace("}", "}}", StringComparison.Ordinal);
                parts.Add(InterpolatedStringText()
                    .WithTextToken(Token(
                        TriviaList(),
                        SyntaxKind.InterpolatedStringTextToken,
                        sourceText,
                        part.Text,
                        TriviaList())));
            }
            else if (part.Expression != null)
            {
                // IMPORTANT: All interpolation expressions are wrapped in parentheses to prevent
                // C# parser ambiguity with ':' in interpolation holes. Without parens, expressions
                // like global::Sharpy.Builtins.Len(x) would be misparsed as
                // expression 'global' with format '::Sharpy.Builtins.Len(x)'.

                // Effective conversion: an explicit !r/!s/!a always wins; otherwise a '='
                // self-documenting field defaults to repr() *unless* a format spec is present
                // (then the value side uses normal formatting). Matches CPython semantics.
                char? effectiveConversion = part.Conversion
                    ?? (part.IsSelfDocumenting && string.IsNullOrEmpty(part.FormatSpec) ? 'r' : null);

                // '=' self-documenting prefix: print the verbatim captured source (incl. '=')
                // before the value, e.g. f'{x = }' → "x = 42".
                if (part.IsSelfDocumenting && part.SourceText != null)
                {
                    var prefixSource = EscapeForInterpolatedStringSource(part.SourceText)
                        .Replace("{", "{{", StringComparison.Ordinal)
                        .Replace("}", "}}", StringComparison.Ordinal);
                    parts.Add(InterpolatedStringText()
                        .WithTextToken(Token(
                            TriviaList(),
                            SyntaxKind.InterpolatedStringTextToken,
                            prefixSource,
                            part.SourceText,
                            TriviaList())));
                }

                if (effectiveConversion != null)
                {
                    // Conversion turns the value into a string via Builtins.Repr/Str/Ascii; any
                    // format spec then applies to that string (alignment is the meaningful case).
                    parts.Add(GenerateConvertedInterpolation(part.Expression, effectiveConversion.Value, part.FormatSpec));
                }
                // Special handling for percent format (.N%) - Python's % format doesn't add
                // a space before %, but .NET's P format does (even with InvariantCulture).
                // Generate: {value * 100:FN}% instead of {value:PN}
                else if (!string.IsNullOrEmpty(part.FormatSpec) && IsPercentFormat(part.FormatSpec, out var percentPrecision))
                {
                    // Generate: value * 100
                    var multipliedExpr = BinaryExpression(
                        SyntaxKind.MultiplyExpression,
                        GenerateExpression(part.Expression),
                        LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(100)));

                    var interpolation = Interpolation(ParenthesizedExpression(multipliedExpr))
                        .WithFormatClause(
                            InterpolationFormatClause(
                                Token(SyntaxKind.ColonToken),
                                Token(
                                    TriviaList(),
                                    SyntaxKind.InterpolatedStringTextToken,
                                    "F" + percentPrecision,
                                    "F" + percentPrecision,
                                    TriviaList())));

                    parts.Add(interpolation);

                    // Add the literal "%" after the interpolation
                    parts.Add(InterpolatedStringText()
                        .WithTextToken(Token(
                            TriviaList(),
                            SyntaxKind.InterpolatedStringTextToken,
                            "%",
                            "%",
                            TriviaList())));
                }
                else if (!string.IsNullOrEmpty(part.FormatSpec))
                {
                    var result = TranslatePythonFormatSpec(part.FormatSpec);

                    if (result.NeedsExpressionRewrite && result.Base.HasValue)
                    {
                        // Binary/octal: f"{x:b}" -> $"{Convert.ToString(x, 2)}"
                        var innerExpr = GenerateExpression(part.Expression);
                        var convertCall = InvocationExpression(
                            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                IdentifierName("Convert"), IdentifierName("ToString")))
                            .WithArgumentList(ArgumentList(SeparatedList(new[]
                            {
                                Argument(innerExpr),
                                Argument(LiteralExpression(SyntaxKind.NumericLiteralExpression,
                                    Literal(result.Base.Value)))
                            })));
                        ExpressionSyntax formatted = convertCall;
                        if (result.Width.HasValue && result.Width.Value > 0)
                        {
                            if (result.AlignmentMode.HasValue && result.AlignmentMode.Value != '>')
                            {
                                // Non-right alignment: Sharpy.Builtins.FormatAlign(formatted, width, fill, align)
                                formatted = InvocationExpression(
                                    MakeGlobalQualifiedName("Sharpy", "Builtins", "FormatAlign"))
                                    .WithArgumentList(ArgumentList(SeparatedList(new[]
                                    {
                                        Argument(formatted),
                                        Argument(LiteralExpression(SyntaxKind.NumericLiteralExpression,
                                            Literal(result.Width.Value))),
                                        Argument(LiteralExpression(SyntaxKind.CharacterLiteralExpression,
                                            Literal(result.FillChar ?? ' '))),
                                        Argument(LiteralExpression(SyntaxKind.CharacterLiteralExpression,
                                            Literal(result.AlignmentMode!.Value)))
                                    })));
                            }
                            else
                            {
                                // Right-align (default): PadLeft
                                formatted = InvocationExpression(
                                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                        formatted, IdentifierName("PadLeft")))
                                    .WithArgumentList(ArgumentList(SeparatedList(new[]
                                    {
                                        Argument(LiteralExpression(SyntaxKind.NumericLiteralExpression,
                                            Literal(result.Width.Value))),
                                        Argument(LiteralExpression(SyntaxKind.CharacterLiteralExpression,
                                            Literal(result.FillChar ?? '0')))
                                    })));
                            }
                        }
                        parts.Add(Interpolation(ParenthesizedExpression(formatted)));
                    }
                    else if (result.NeedsExpressionRewrite && result.Grouping == '_')
                    {
                        // Underscore grouping: f"{x:_}" ->
                        //   $"{x.ToString("N0", System.Globalization.CultureInfo.InvariantCulture).Replace(",", "_")}"
                        var innerExpr = GenerateExpression(part.Expression);
                        var toStringCall = InvocationExpression(
                            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                innerExpr, IdentifierName("ToString")))
                            .WithArgumentList(ArgumentList(SeparatedList(new[]
                            {
                                Argument(LiteralExpression(SyntaxKind.StringLiteralExpression,
                                    Literal("N0"))),
                                Argument(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                            IdentifierName("System"),
                                            IdentifierName("Globalization")),
                                        IdentifierName("CultureInfo")),
                                    IdentifierName("InvariantCulture")))
                            })));
                        var replaceCall = InvocationExpression(
                            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                toStringCall, IdentifierName("Replace")))
                            .WithArgumentList(ArgumentList(SeparatedList(new[]
                            {
                                Argument(LiteralExpression(SyntaxKind.StringLiteralExpression,
                                    Literal(","))),
                                Argument(LiteralExpression(SyntaxKind.StringLiteralExpression,
                                    Literal("_")))
                            })));
                        parts.Add(Interpolation(ParenthesizedExpression(replaceCall)));
                    }
                    else if (result.NeedsExpressionRewrite && result.AlignmentMode.HasValue)
                    {
                        // Center-align or custom fill: f"{x:*^10}" ->
                        //   $"{Sharpy.Builtins.FormatAlign(x.ToString(), 10, '*', '^')}"
                        var innerExpr = GenerateExpression(part.Expression);
                        ExpressionSyntax toStringExpr;
                        if (!string.IsNullOrEmpty(result.FormatString))
                        {
                            toStringExpr = InvocationExpression(
                                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                    innerExpr, IdentifierName("ToString")))
                                .WithArgumentList(ArgumentList(SingletonSeparatedList(
                                    Argument(LiteralExpression(SyntaxKind.StringLiteralExpression,
                                        Literal(result.FormatString))))));
                        }
                        else
                        {
                            toStringExpr = InvocationExpression(
                                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                    innerExpr, IdentifierName("ToString")))
                                .WithArgumentList(ArgumentList());
                        }
                        var alignCall = InvocationExpression(
                            MakeGlobalQualifiedName("Sharpy", "Builtins", "FormatAlign"))
                            .WithArgumentList(ArgumentList(SeparatedList(new[]
                            {
                                Argument(toStringExpr),
                                Argument(LiteralExpression(SyntaxKind.NumericLiteralExpression,
                                    Literal(result.Width ?? 0))),
                                Argument(LiteralExpression(SyntaxKind.CharacterLiteralExpression,
                                    Literal(result.FillChar ?? ' '))),
                                Argument(LiteralExpression(SyntaxKind.CharacterLiteralExpression,
                                    Literal(result.AlignmentMode!.Value)))
                            })));
                        parts.Add(Interpolation(ParenthesizedExpression(alignCall)));
                    }
                    else
                    {
                        // General case: simple format string with optional C# alignment
                        var innerExpr = GenerateExpression(part.Expression);
                        var interpolation = Interpolation(ParenthesizedExpression(innerExpr));

                        if (result.Alignment.HasValue)
                        {
                            ExpressionSyntax alignmentExpr;
                            if (result.Alignment.Value < 0)
                            {
                                alignmentExpr = PrefixUnaryExpression(
                                    SyntaxKind.UnaryMinusExpression,
                                    LiteralExpression(SyntaxKind.NumericLiteralExpression,
                                        Literal(Math.Abs(result.Alignment.Value))));
                            }
                            else
                            {
                                alignmentExpr = LiteralExpression(
                                    SyntaxKind.NumericLiteralExpression,
                                    Literal(result.Alignment.Value));
                            }
                            interpolation = interpolation.WithAlignmentClause(
                                InterpolationAlignmentClause(
                                    Token(SyntaxKind.CommaToken),
                                    alignmentExpr));
                        }

                        if (!string.IsNullOrEmpty(result.FormatString))
                        {
                            interpolation = interpolation.WithFormatClause(
                                InterpolationFormatClause(
                                    Token(SyntaxKind.ColonToken),
                                    Token(
                                        TriviaList(),
                                        SyntaxKind.InterpolatedStringTextToken,
                                        result.FormatString,
                                        result.FormatString,
                                        TriviaList())));
                        }

                        parts.Add(interpolation);
                    }
                }
                else
                {
                    // No format spec — default formatting
                    var innerExpr = GenerateExpression(part.Expression);

                    // For floating-point types without a format spec, wrap in FormatFloat()
                    // to ensure Python-compatible formatting (e.g., 5.0 instead of 5).
                    var exprType = GetExpressionSemanticType(part.Expression);
                    if (exprType == SemanticType.Float ||
                        exprType == SemanticType.Double ||
                        exprType == SemanticType.Float32)
                    {
                        innerExpr = InvocationExpression(
                            MakeGlobalQualifiedName("Sharpy", "Builtins", "FormatFloat"))
                            .WithArgumentList(ArgumentList(SingletonSeparatedList(
                                Argument(innerExpr))));
                    }

                    parts.Add(Interpolation(ParenthesizedExpression(innerExpr)));
                }
            }
        }

        // Wrap with FormattableString.Invariant() to ensure consistent formatting
        // regardless of locale (e.g., percent format uses space before % in some locales)
        var interpolatedString = InterpolatedStringExpression(Token(SyntaxKind.InterpolatedStringStartToken))
            .WithContents(List(parts));

        // Wrap with FormattableString.Invariant($"...") to ensure consistent formatting
        var invariantCall = InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName("FormattableString"),
                IdentifierName("Invariant")))
            .WithArgumentList(ArgumentList(SingletonSeparatedList(
                Argument(interpolatedString))));

        return invariantCall;
    }

    /// <summary>
    /// Wraps an interpolation expression in the runtime conversion implied by an f-string
    /// conversion flag ('r' → repr, 's' → str, 'a' → ascii), producing a string value, then
    /// applies an optional (alignment-oriented) format spec to that string.
    /// </summary>
    private InterpolatedStringContentSyntax GenerateConvertedInterpolation(
        Expression expression, char conversion, string? formatSpec)
    {
        var method = conversion switch
        {
            'r' => "Repr",
            's' => "Str",
            'a' => "Ascii",
            _ => "Str",
        };

        var converted = InvocationExpression(
            MakeGlobalQualifiedName("Sharpy", "Builtins", method))
            .WithArgumentList(ArgumentList(SingletonSeparatedList(
                Argument(GenerateExpression(expression)))));

        var interpolation = Interpolation(ParenthesizedExpression(converted));

        if (string.IsNullOrEmpty(formatSpec))
            return interpolation;

        // The value is already a string, so only alignment/width is meaningful. Center
        // alignment ('^') and custom fills need FormatAlign; '>'/'<' map to C# alignment.
        var result = TranslatePythonFormatSpec(formatSpec);

        if (result.NeedsExpressionRewrite && result.AlignmentMode == '^')
        {
            var alignCall = InvocationExpression(
                MakeGlobalQualifiedName("Sharpy", "Builtins", "FormatAlign"))
                .WithArgumentList(ArgumentList(SeparatedList(new[]
                {
                    Argument(converted),
                    Argument(LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(result.Width ?? 0))),
                    Argument(LiteralExpression(SyntaxKind.CharacterLiteralExpression, Literal(result.FillChar ?? ' '))),
                    Argument(LiteralExpression(SyntaxKind.CharacterLiteralExpression, Literal('^'))),
                })));
            return Interpolation(ParenthesizedExpression(alignCall));
        }

        if (result.Alignment.HasValue)
        {
            ExpressionSyntax alignmentExpr = result.Alignment.Value < 0
                ? PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression,
                    LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(Math.Abs(result.Alignment.Value))))
                : LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(result.Alignment.Value));
            interpolation = interpolation.WithAlignmentClause(
                InterpolationAlignmentClause(Token(SyntaxKind.CommaToken), alignmentExpr));
        }

        return interpolation;
    }

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
    /// Result of parsing a Python format spec into C#-compatible components.
    /// </summary>
    private readonly record struct FormatSpecResult(
        string FormatString,
        int? Alignment,
        bool NeedsExpressionRewrite,
        char? FillChar,
        char? AlignmentMode,
        int? Width,
        int? Base,
        char? Grouping = null);

    /// <summary>
    /// Parses Python's format specification mini-language into C#-compatible components.
    /// Python: [[fill]align][sign][z][#][0][width][grouping_option][.precision][type]
    /// </summary>
    private static FormatSpecResult TranslatePythonFormatSpec(string pythonSpec)
    {
        if (string.IsNullOrEmpty(pythonSpec))
            return new FormatSpecResult(pythonSpec, null, false, null, null, null, null);

        var pos = 0;
        char? fillChar = null;
        char? alignmentMode = null;
        // char? sign = null; — sign is not currently mapped to C# output
        bool zeroPad = false;
        int? width = null;
        char? grouping = null;
        int? precision = null;
        char? typeChar = null;

        // Step 1: Parse optional [fill]align
        if (pos < pythonSpec.Length)
        {
            if (pos + 1 < pythonSpec.Length && IsAlignChar(pythonSpec[pos + 1]))
            {
                fillChar = pythonSpec[pos];
                alignmentMode = pythonSpec[pos + 1];
                pos += 2;
            }
            else if (IsAlignChar(pythonSpec[pos]))
            {
                alignmentMode = pythonSpec[pos];
                pos += 1;
            }
        }

        // Step 2: Parse optional sign (+, -, space)
        if (pos < pythonSpec.Length && (pythonSpec[pos] == '+' || pythonSpec[pos] == '-' || pythonSpec[pos] == ' '))
        {
            // sign = pythonSpec[pos]; — not mapped to C# output currently
            pos++;
        }

        // Step 3: Skip optional 'z' (coerce negative zero)
        if (pos < pythonSpec.Length && pythonSpec[pos] == 'z')
            pos++;

        // Step 4: Skip optional '#' (alternate form)
        if (pos < pythonSpec.Length && pythonSpec[pos] == '#')
            pos++;

        // Step 5: Parse optional '0' (zero-pad flag)
        if (pos < pythonSpec.Length && pythonSpec[pos] == '0')
        {
            zeroPad = true;
            pos++;
        }

        // Step 6: Parse optional width (digits)
        var widthStart = pos;
        while (pos < pythonSpec.Length && char.IsDigit(pythonSpec[pos]))
            pos++;
        if (pos > widthStart)
            width = int.Parse(pythonSpec.Substring(widthStart, pos - widthStart), CultureInfo.InvariantCulture);

        // Step 7: Parse optional grouping (, or _)
        if (pos < pythonSpec.Length && (pythonSpec[pos] == ',' || pythonSpec[pos] == '_'))
        {
            grouping = pythonSpec[pos];
            pos++;
        }

        // Step 8: Parse optional .precision
        if (pos < pythonSpec.Length && pythonSpec[pos] == '.')
        {
            pos++; // skip '.'
            var precStart = pos;
            while (pos < pythonSpec.Length && char.IsDigit(pythonSpec[pos]))
                pos++;
            if (pos > precStart)
                precision = int.Parse(pythonSpec.Substring(precStart, pos - precStart), CultureInfo.InvariantCulture);
            else
                precision = 0;
        }

        // Step 9: Parse optional type char
        if (pos < pythonSpec.Length && IsTypeChar(pythonSpec[pos]))
        {
            typeChar = pythonSpec[pos];
            pos++;
        }

        // Compose result
        return ComposeFormatSpecResult(fillChar, alignmentMode, zeroPad, width, grouping, precision, typeChar);
    }

    private static bool IsAlignChar(char c) => c == '<' || c == '>' || c == '^' || c == '=';

    private static bool IsTypeChar(char c) => "bcdeEfFgGnosxX%".IndexOf(c, StringComparison.Ordinal) >= 0;

    private static FormatSpecResult ComposeFormatSpecResult(
        char? fillChar, char? alignmentMode, bool zeroPad,
        int? width, char? grouping, int? precision, char? typeChar)
    {
        // Binary and octal need expression rewriting
        if (typeChar == 'b')
        {
            var padWidth = (zeroPad && width.HasValue) ? width.Value : (width ?? 0);
            var padChar = fillChar ?? (zeroPad ? '0' : ' ');
            return new FormatSpecResult("", null, true, padChar, alignmentMode, padWidth > 0 ? padWidth : null, 2);
        }

        if (typeChar == 'o')
        {
            var padWidth = (zeroPad && width.HasValue) ? width.Value : (width ?? 0);
            var padChar = fillChar ?? (zeroPad ? '0' : ' ');
            return new FormatSpecResult("", null, true, padChar, alignmentMode, padWidth > 0 ? padWidth : null, 8);
        }

        // Percent format — handled by special-case in caller (IsPercentFormat)
        if (typeChar == '%')
        {
            var prec = precision?.ToString(CultureInfo.InvariantCulture) ?? "6";
            return new FormatSpecResult("P" + prec, null, false, null, null, null, null);
        }

        // Build the C# format string from type + precision + grouping
        var formatString = BuildCSharpFormatString(typeChar, precision, grouping, zeroPad, width,
            alignmentMode.HasValue);

        // Alignment handling
        if (alignmentMode.HasValue)
        {
            bool needsRewrite = alignmentMode == '^' || alignmentMode == '='
                || (fillChar.HasValue && fillChar != ' ');
            if (needsRewrite)
            {
                return new FormatSpecResult(formatString, null, true, fillChar ?? ' ',
                    alignmentMode, width, null);
            }

            // Simple space-fill alignment: use C# alignment component
            int alignment = alignmentMode == '<' ? -(width ?? 0) : (width ?? 0);
            if (alignment != 0)
                return new FormatSpecResult(formatString, alignment, false, null, null, null, null);
        }

        // Underscore grouping needs expression rewrite
        if (grouping == '_')
        {
            return new FormatSpecResult(formatString, null, true, null, null, null, null, '_');
        }

        // Zero-pad without alignment for integer types: 05 -> D5
        if (zeroPad && width.HasValue && typeChar == null && precision == null && grouping == null)
            return new FormatSpecResult("D" + width.Value, null, false, null, null, null, null);

        return new FormatSpecResult(formatString, null, false, null, null, null, null);
    }

    private static string BuildCSharpFormatString(char? typeChar, int? precision, char? grouping,
        bool zeroPad, int? width, bool hasAlignment)
    {
        // Grouping only: "," -> "N0", ",.2f" -> "N2"
        if (grouping == ',')
        {
            if (typeChar == 'f' || typeChar == 'F')
                return "N" + (precision?.ToString(CultureInfo.InvariantCulture) ?? "0");
            if (typeChar == null)
                return "N" + (precision?.ToString(CultureInfo.InvariantCulture) ?? "0");
        }

        // Map type characters to C# format specifiers
        var csharpType = typeChar switch
        {
            'd' => "D",
            'f' or 'F' => "F",
            'e' => "E",
            'E' => "E",
            'g' => "G",
            'G' => "G",
            'x' => "x",
            'X' => "X",
            'n' => "N",
            's' => "",
            'c' => "",
            _ => ""
        };

        if (precision.HasValue && csharpType.Length > 0)
            return csharpType + precision.Value;

        if (csharpType.Length > 0)
            return csharpType;

        // Zero-pad without type: "05" -> "D5" (handled in ComposeFormatSpecResult for no-alignment case)
        if (zeroPad && width.HasValue && !hasAlignment)
            return "D" + width.Value;

        return "";
    }

    /// <summary>
    /// Checks if a Python format spec is a percent format (.N%) and extracts the precision.
    /// </summary>
    private static bool IsPercentFormat(string pythonSpec, out string precision)
    {
        precision = "6";
        if (string.IsNullOrEmpty(pythonSpec))
            return false;
        var result = TranslatePythonFormatSpec(pythonSpec);
        if (result.FormatString.StartsWith("P") && !result.NeedsExpressionRewrite)
        {
            precision = result.FormatString.Length > 1 ? result.FormatString.Substring(1) : "6";
            return true;
        }
        return false;
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

                // Generate the interpolation expression value
                var valueExpr = GenerateExpression(part.Expression);

                // Box value types to object for the Interpolation constructor
                var exprType = GetExpressionSemanticType(part.Expression);
                if (exprType is BuiltinType { IsValueType: true })
                {
                    valueExpr = CastExpression(
                        PredefinedType(Token(SyntaxKind.ObjectKeyword)),
                        valueExpr);
                }

                // Derive expression text from the AST node
                var exprText = DeriveExpressionText(part.Expression);

                // Format spec (empty string if none)
                var formatSpec = part.FormatSpec ?? string.Empty;

                // new global::Sharpy.Interpolation(value, "exprText", "formatSpec")
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
                        Argument(MakeStringLiteral(formatSpec))
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

        // Get the mangled variable name, registering it as a new declaration
        var varName = GetMangledVariableName(walrus.Target, isNewDeclaration: true);

        if (_walrusInlineMode)
        {
            // Inline mode: emit a typed pre-declaration (no initializer) and return
            // an inline assignment expression so the value is re-evaluated each iteration.
            var semType = GetExpressionSemanticType(walrus.Value);
            var typeSyntax = semType != null
                ? _typeMapper.MapSemanticType(semType)
                : IdentifierName("var");

            _walrusPreDeclarations.Add(
                LocalDeclarationStatement(
                    VariableDeclaration(typeSyntax)
                        .WithVariables(SingletonSeparatedList(
                            VariableDeclarator(Identifier(varName))))));
            _declaredVariables.Add(varName);

            // Return: (varName = value)
            return ParenthesizedExpression(
                AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    IdentifierName(varName),
                    value));
        }

        // Hoist: var varName = value;
        _hoistedStatements.Add(
            LocalDeclarationStatement(
                VariableDeclaration(IdentifierName("var"))
                    .WithVariables(SingletonSeparatedList(
                        VariableDeclarator(Identifier(varName))
                            .WithInitializer(EqualsValueClause(value))))));
        _declaredVariables.Add(varName);

        // The walrus expression evaluates to the variable itself
        return IdentifierName(varName);
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
        _hoistedStatements.Add(LocalDeclarationStatement(
            VariableDeclaration(IdentifierName("var"))
                .WithVariables(SingletonSeparatedList(
                    VariableDeclarator(Identifier(tempName))
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
                    _hoistedStatements.Add(LocalDeclarationStatement(
                        VariableDeclaration(IdentifierName("var"))
                            .WithVariables(SingletonSeparatedList(
                                VariableDeclarator(Identifier(tupTemp))
                                    .WithInitializer(EqualsValueClause(GenerateExpression(spread.Value)))))));
                    for (int i = 0; i < tupleType.ElementTypes.Count; i++)
                    {
                        _hoistedStatements.Add(ExpressionStatement(
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
                    _hoistedStatements.Add(ExpressionStatement(
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
                _hoistedStatements.Add(ExpressionStatement(
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
        _hoistedStatements.Add(LocalDeclarationStatement(
            VariableDeclaration(IdentifierName("var"))
                .WithVariables(SingletonSeparatedList(
                    VariableDeclarator(Identifier(tempName))
                        .WithInitializer(EqualsValueClause(
                            ObjectCreationExpression(dictType)
                                .WithArgumentList(ArgumentList())))))));

        foreach (var entry in entries)
        {
            if (entry.Key == null)
            {
                // __spread_N.{spreadMethodName}(spreadDict)
                _hoistedStatements.Add(ExpressionStatement(
                    InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName(tempName),
                            IdentifierName(spreadMethodName)))
                        .AddArgumentListArguments(Argument(GenerateExpression(entry.Value)))));
            }
            else
            {
                // __spread_N[key] = value
                _hoistedStatements.Add(ExpressionStatement(
                    AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        ElementAccessExpression(IdentifierName(tempName))
                            .WithArgumentList(BracketedArgumentList(
                                SingletonSeparatedList(Argument(GenerateExpression(entry.Key))))),
                        GenerateExpression(entry.Value))));
            }
        }

        return IdentifierName(tempName);
    }

    /// <summary>
    /// Generates an expression for a collection element, propagating the target type context
    /// for nested collection literals (e.g., list[list[int]] = [[1, 2], [3, 4]]).
    /// If the element is itself a collection literal and the parent target type has type arguments,
    /// the inner element's target type is set to the parent's first type argument.
    /// </summary>
    private ExpressionSyntax GenerateWithNestedTargetType(Expression element, TypeAnnotation? parentTargetType)
    {
        if (parentTargetType == null ||
            parentTargetType.TypeArguments.Length == 0 ||
            element is not (ListLiteral or SetLiteral or DictLiteral))
        {
            return GenerateExpression(element);
        }

        // For list[list[int]], the inner target type is list[int] (first type argument)
        // For set[set[str]], the inner target type is set[str] (first type argument)
        var innerTargetType = parentTargetType.TypeArguments[0];

        var previousTargetType = _targetTypeContext;
        _targetTypeContext = innerTargetType;
        try
        {
            return GenerateExpression(element);
        }
        finally
        {
            _targetTypeContext = previousTargetType;
        }
    }
}
