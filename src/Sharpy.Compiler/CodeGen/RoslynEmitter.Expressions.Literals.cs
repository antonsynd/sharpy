using System.Collections.Immutable;
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
/// RoslynEmitter partial class: Collection literals, comprehensions, f-strings, walrus
/// </summary>
internal partial class RoslynEmitter
{
    private ExpressionSyntax GenerateEllipsisLiteral()
    {
        // Ellipsis (...) in concrete method bodies generates throw NotImplementedException()
        // Note: For abstract methods/interface methods, the ellipsis is ignored and
        // the method has no body (handled in GenerateClassMethod/GenerateInterfaceMethod)
        return ThrowExpression(
            ObjectCreationExpression(ParseTypeName("System.NotImplementedException"))
                .WithArgumentList(ArgumentList()));
    }

    private ExpressionSyntax GenerateListLiteral(ListLiteral list)
    {
        // new Sharpy.List<T> { elem1, elem2, elem3 }
        // Prefer target type annotation if available (e.g., list[int] = [...])
        TypeSyntax elementType;
        if (_targetTypeContext != null &&
            _targetTypeContext.Name == "list" &&
            _targetTypeContext.TypeArguments.Length > 0)
        {
            // Use the declared element type from the target type annotation
            elementType = _typeMapper.MapType(_targetTypeContext.TypeArguments[0]);
        }
        else
        {
            // Fall back to inference from elements
            elementType = _typeMapper.InferElementType(list.Elements);
        }

        var elements = list.Elements.Select(GenerateExpression);

        var listType = GenericName("Sharpy.List")
            .AddTypeArgumentListArguments(elementType);

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
            _targetTypeContext.Name == "dict" &&
            _targetTypeContext.TypeArguments.Length >= 2)
        {
            keyType = _typeMapper.MapType(_targetTypeContext.TypeArguments[0]);
            valueType = _typeMapper.MapType(_targetTypeContext.TypeArguments[1]);
        }
        else
        {
            keyType = _typeMapper.InferElementType(dict.Entries.Select(e => e.Key));
            valueType = _typeMapper.InferElementType(dict.Entries.Select(e => e.Value));
        }

        var initializers = dict.Entries.Select(entry =>
            InitializerExpression(SyntaxKind.ComplexElementInitializerExpression,
                SeparatedList(new[]
                {
                    GenerateExpression(entry.Key),
                    GenerateExpression(entry.Value)
                })));

        var dictType = GenericName("Sharpy.Dict")
            .AddTypeArgumentListArguments(keyType, valueType);

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
            _targetTypeContext.Name == "set" &&
            _targetTypeContext.TypeArguments.Length > 0)
        {
            elementType = _typeMapper.MapType(_targetTypeContext.TypeArguments[0]);
        }
        else
        {
            elementType = _typeMapper.InferElementType(set.Elements);
        }

        var elements = set.Elements.Select(GenerateExpression);

        var setType = GenericName("Sharpy.Set")
            .AddTypeArgumentListArguments(elementType);

        return ObjectCreationExpression(setType)
            .WithArgumentList(ArgumentList())
            .WithInitializer(InitializerExpression(
                SyntaxKind.CollectionInitializerExpression,
                SeparatedList(elements)));
    }

    private ExpressionSyntax GenerateTupleLiteral(TupleLiteral tuple)
    {
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

        // Unnamed tuple: (elem1, elem2, ...)
        return TupleExpression(SeparatedList(
            elements.Select(e => Argument(e))));
    }

    // See: #100 (consider imperative code generation for complex comprehensions)

    private ExpressionSyntax GenerateListComprehension(ListComprehension listComp)
    {
        // Generate: new Sharpy.List<T>(iterator.Where(...).Select(...))
        // Example: [x * 2 for x in items if x > 0]
        // becomes: new Sharpy.List<int>(items.Where(x => x > 0).Select(x => x * 2))

        var (chain, param, tupleVarNames, errorExpr) = GenerateComprehensionChain(
            listComp.Clauses, "List", listComp.LineStart, listComp.ColumnStart);

        if (errorExpr != null)
            return errorExpr;

        // Apply .Select(x => element_expression)
        var elementExpr = GenerateExpression(listComp.Element);
        var selectLambda = MakeComprehensionLambda(param, tupleVarNames, elementExpr);

        chain = InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                chain,
                IdentifierName("Select")))
            .AddArgumentListArguments(Argument(selectLambda));

        // Wrap in new Sharpy.List<T>(chain) using semantic type info for T
        var elementSemanticType = GetComprehensionSubExpressionType(listComp.Element, listComp);

        var elementTypeSyntax = elementSemanticType != null
            ? _typeMapper.MapSemanticType(elementSemanticType)
            : PredefinedType(Token(SyntaxKind.ObjectKeyword));

        var listType = GenericName("Sharpy.List")
            .AddTypeArgumentListArguments(elementTypeSyntax);

        return ObjectCreationExpression(listType)
            .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(chain))));
    }

    private ExpressionSyntax GenerateSetComprehension(SetComprehension setComp)
    {
        // Generate: new Sharpy.Set<T>(iterator.Where(...).Select(...))
        // Example: {x * 2 for x in items if x > 0}
        // becomes: new Sharpy.Set<int>(items.Where(x => x > 0).Select(x => x * 2))

        var (chain, param, tupleVarNames, errorExpr) = GenerateComprehensionChain(
            setComp.Clauses, "Set", setComp.LineStart, setComp.ColumnStart);

        if (errorExpr != null)
            return errorExpr;

        // Apply .Select(x => element_expression)
        var elementExpr = GenerateExpression(setComp.Element);
        var selectLambda = MakeComprehensionLambda(param, tupleVarNames, elementExpr);

        chain = InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                chain,
                IdentifierName("Select")))
            .AddArgumentListArguments(Argument(selectLambda));

        // Wrap in new Sharpy.Set<T>(chain) using semantic type info for T
        var elementSemanticType = GetComprehensionSubExpressionType(setComp.Element, setComp);

        var elementTypeSyntax = elementSemanticType != null
            ? _typeMapper.MapSemanticType(elementSemanticType)
            : PredefinedType(Token(SyntaxKind.ObjectKeyword));

        var setType = GenericName("Sharpy.Set")
            .AddTypeArgumentListArguments(elementTypeSyntax);

        return ObjectCreationExpression(setType)
            .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(chain))));
    }

    private ExpressionSyntax GenerateDictComprehension(DictComprehension dictComp)
    {
        // Generate LINQ method chain: iterator.Where(...).ToDictionary(x => key, x => value)
        // Example: {k: v for k, v in pairs if v > 0}
        // becomes: pairs.Where(p => ...).ToDictionary(p => k, p => v)

        var (chain, param, tupleVarNames, errorExpr) = GenerateComprehensionChain(
            dictComp.Clauses, "Dict", dictComp.LineStart, dictComp.ColumnStart);

        if (errorExpr != null)
            return errorExpr;

        // Generate key and value selector lambdas
        var keyExpr = GenerateExpression(dictComp.Key);
        var valueExpr = GenerateExpression(dictComp.Value);

        var keyLambda = MakeComprehensionLambda(param, tupleVarNames, keyExpr);
        var valueLambda = MakeComprehensionLambda(param, tupleVarNames, valueExpr);

        // Apply .ToDictionary(x => key, x => value) and cast to Dict<K,V>.
        // .ToDictionary() returns Dictionary<K,V> which must be explicitly cast
        // so that 'var' declarations infer Dict<K,V>, not Dictionary<K,V>.
        // Dict<K,V> has an implicit conversion operator from Dictionary<K,V>.
        var toDictInvocation = InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                chain,
                IdentifierName("ToDictionary")))
            .AddArgumentListArguments(
                Argument(keyLambda),
                Argument(valueLambda));

        // Wrap in (Dict<K,V>)expr so the result type is always Dict, not Dictionary
        var (keySemanticType, valueSemanticType) = GetDictComprehensionTypes(dictComp);

        if (keySemanticType != null && valueSemanticType != null)
        {
            var dictType = GenericName("Sharpy.Dict")
                .AddTypeArgumentListArguments(
                    _typeMapper.MapSemanticType(keySemanticType),
                    _typeMapper.MapSemanticType(valueSemanticType));
            return CastExpression(dictType, ParenthesizedExpression(toDictInvocation));
        }

        return toDictInvocation;
    }

    /// <summary>
    /// Gets the semantic type of a comprehension sub-expression (element, key, or value),
    /// falling back to the comprehension's overall generic type arguments if the
    /// sub-expression's type is not found in SemanticInfo due to cross-module
    /// reference identity issues.
    /// </summary>
    private SemanticType? GetComprehensionSubExpressionType(
        Expression subExpression,
        Expression comprehension,
        int typeArgIndex = 0)
    {
        var type = GetExpressionSemanticType(subExpression);
        // TODO(#167): Fallback for cross-module reference identity issue in SemanticInfo.
        if (type == null)
        {
            var compType = GetExpressionSemanticType(comprehension);
            if (compType is Semantic.GenericType gt && gt.TypeArguments.Count > typeArgIndex)
                type = gt.TypeArguments[typeArgIndex];
        }
        return type;
    }

    /// <summary>
    /// Gets key and value semantic types for a dict comprehension, with cross-module fallback.
    /// </summary>
    private (SemanticType? key, SemanticType? value) GetDictComprehensionTypes(
        DictComprehension dictComp)
    {
        var keyType = GetExpressionSemanticType(dictComp.Key);
        var valueType = GetExpressionSemanticType(dictComp.Value);
        // TODO(#167): Fallback for cross-module reference identity issue in SemanticInfo.
        if (keyType == null || valueType == null)
        {
            var compType = GetExpressionSemanticType(dictComp);
            if (compType is Semantic.GenericType gt && gt.TypeArguments.Count >= 2)
            {
                keyType ??= gt.TypeArguments[0];
                valueType ??= gt.TypeArguments[1];
            }
        }
        return (keyType, valueType);
    }

    /// <summary>
    /// Creates a lambda expression for comprehension Select/Where/ToDictionary calls.
    /// For simple variables, returns a simple lambda: x => body.
    /// For tuple unpacking, returns a block lambda with deconstruction:
    ///   (__t_0) => { var (a, b) = __t_0; return body; }
    /// </summary>
    private ExpressionSyntax MakeComprehensionLambda(
        ParameterSyntax param,
        List<string>? tupleVarNames,
        ExpressionSyntax body)
    {
        if (tupleVarNames == null)
        {
            return SimpleLambdaExpression(param)
                .WithExpressionBody(body);
        }

        // Tuple unpacking: (__t_0) => { var (a, b) = __t_0; return body; }
        var paramName = param.Identifier.Text;

        var designations = tupleVarNames
            .Select(name => (VariableDesignationSyntax)SingleVariableDesignation(Identifier(name)))
            .ToList();

        var tupleDesignation = ParenthesizedVariableDesignation(
            SeparatedList(designations));

        // var (a, b) = __t_0;
        var deconstructStmt = ExpressionStatement(
            AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                DeclarationExpression(
                    IdentifierName("var"),
                    tupleDesignation),
                IdentifierName(paramName)));

        var returnStmt = ReturnStatement(body);

        return ParenthesizedLambdaExpression()
            .WithParameterList(ParameterList(SingletonSeparatedList(param)))
            .WithBlock(Block(deconstructStmt, returnStmt));
    }

    /// <summary>
    /// Generates the common LINQ chain for comprehensions: validates the first for clause,
    /// extracts the loop variable, and applies all Where clauses. Returns the chain so far,
    /// the parameter syntax for lambdas, optional tuple variable names for deconstruction,
    /// and optionally an error expression if validation failed.
    /// </summary>
    private (ExpressionSyntax Chain, ParameterSyntax Param, List<string>? TupleVarNames, ExpressionSyntax? Error) GenerateComprehensionChain(
        ImmutableArray<ComprehensionClause> clauses,
        string comprehensionType,
        int lineStart,
        int columnStart)
    {
        if (clauses.IsEmpty || clauses[0] is not ForClause firstFor)
        {
            throw new InvalidOperationException($"{comprehensionType} comprehension must start with a for clause");
        }

        ParameterSyntax param;
        List<string>? tupleVarNames = null;

        if (firstFor.Target is Identifier loopVar)
        {
            var varName = NameMangler.ToCamelCase(loopVar.Name);
            param = Parameter(Identifier(varName));
        }
        else if (firstFor.Target is TupleLiteral tuple &&
                 tuple.Elements.All(e => e is Identifier))
        {
            // Tuple unpacking: use a temp parameter and deconstruct in the lambda body
            var tempName = $"__t_{_tempVarCounter++}";
            param = Parameter(Identifier(tempName));

            tupleVarNames = new List<string>();
            foreach (var elem in tuple.Elements)
            {
                var elemId = (Identifier)elem;
                var name = NameMangler.ToCamelCase(elemId.Name);
                tupleVarNames.Add(name);
                _declaredVariables.Add(name);
                _variableVersions[name] = 0;
            }
        }
        else
        {
            var error = EmitNotImplementedExpression(
                "Complex tuple unpacking in comprehensions is not yet supported. Use a for loop instead.",
                DiagnosticCodes.CodeGen.TupleUnpackingComprehension, lineStart, columnStart);
            return (null!, null!, null, error);
        }

        // Start with the iterator expression
        ExpressionSyntax chain = GenerateExpression(firstFor.Iterator);

        // Apply each if clause as .Where(x => condition)
        foreach (var clause in clauses.Skip(1))
        {
            switch (clause)
            {
                case IfClause ifClause:
                    var condition = GenerateExpression(ifClause.Condition);
                    var lambda = MakeComprehensionLambda(param, tupleVarNames, condition);

                    chain = InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            chain,
                            IdentifierName("Where")))
                        .AddArgumentListArguments(Argument(lambda));
                    break;

                case ForClause:
                    var forError = EmitNotImplementedExpression(
                        "Nested comprehensions (multiple 'for' clauses) are not yet supported. Use a for loop instead.",
                        DiagnosticCodes.CodeGen.NestedComprehension, lineStart, columnStart);
                    return (null!, null!, null, forError);
            }
        }

        return (chain, param, tupleVarNames, null);
    }

    private ExpressionSyntax GenerateFString(FStringLiteral fstring)
    {
        // f"Hello {name}" -> $"Hello {name}"
        var parts = new List<InterpolatedStringContentSyntax>();

        foreach (var part in fstring.Parts)
        {
            if (part.Text != null)
            {
                parts.Add(InterpolatedStringText()
                    .WithTextToken(Token(
                        TriviaList(),
                        SyntaxKind.InterpolatedStringTextToken,
                        part.Text,
                        part.Text,
                        TriviaList())));
            }
            else if (part.Expression != null)
            {
                // IMPORTANT: All interpolation expressions are wrapped in parentheses to prevent
                // C# parser ambiguity with ':' in interpolation holes. Without parens, expressions
                // like global::Sharpy.Builtins.Len(x) would be misparsed as
                // expression 'global' with format '::Sharpy.Builtins.Len(x)'.

                // Special handling for percent format (.N%) - Python's % format doesn't add
                // a space before %, but .NET's P format does (even with InvariantCulture).
                // Generate: {value * 100:FN}% instead of {value:PN}
                if (!string.IsNullOrEmpty(part.FormatSpec) && IsPercentFormat(part.FormatSpec, out var percentPrecision))
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
                else
                {
                    var interpolation = Interpolation(ParenthesizedExpression(GenerateExpression(part.Expression)));

                    // Add format specifier if present (e.g., ".2f" in f"{value:.2f}")
                    if (!string.IsNullOrEmpty(part.FormatSpec))
                    {
                        var csharpFormatSpec = TranslatePythonFormatSpec(part.FormatSpec);
                        interpolation = interpolation.WithFormatClause(
                            InterpolationFormatClause(
                                Token(SyntaxKind.ColonToken),
                                Token(
                                    TriviaList(),
                                    SyntaxKind.InterpolatedStringTextToken,
                                    csharpFormatSpec,
                                    csharpFormatSpec,
                                    TriviaList())));
                    }

                    parts.Add(interpolation);
                }
            }
        }

        // Wrap with FormattableString.Invariant() to ensure consistent formatting
        // regardless of locale (e.g., percent format uses space before % in some locales)
        var interpolatedString = InterpolatedStringExpression(Token(SyntaxKind.InterpolatedStringStartToken))
            .WithContents(List(parts));

        return InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName("FormattableString"),
                IdentifierName("Invariant")))
            .WithArgumentList(ArgumentList(SingletonSeparatedList(
                Argument(interpolatedString))));
    }

    /// <summary>
    /// Translates Python format specifiers to C# format specifiers.
    /// Python: [[fill]align][sign][#][0][width][grouping_option][.precision][type]
    ///
    /// Supported conversions:
    /// - .Nf -> FN (fixed-point, N decimal places)
    /// - .Ne -> EN (scientific notation)
    /// - .N% -> PN (percent)
    /// - 0N -> DN (zero-padded integer width N)
    /// - , -> N0 (number with thousand separators)
    /// - .Ng -> GN (general format)
    /// </summary>
    private static string TranslatePythonFormatSpec(string pythonSpec)
    {
        if (string.IsNullOrEmpty(pythonSpec))
            return pythonSpec;

        // Handle thousand separator only: "," -> "N0"
        if (pythonSpec == ",")
            return "N0";

        // Handle .Nf (fixed-point): ".2f" -> "F2"
        if (pythonSpec.StartsWith(".") && pythonSpec.EndsWith("f"))
        {
            var precision = pythonSpec.Substring(1, pythonSpec.Length - 2);
            if (int.TryParse(precision, out _))
                return "F" + precision;
        }

        // Handle .Ne (scientific): ".2e" -> "E2"
        if (pythonSpec.StartsWith(".") && pythonSpec.EndsWith("e"))
        {
            var precision = pythonSpec.Substring(1, pythonSpec.Length - 2);
            if (int.TryParse(precision, out _))
                return "E" + precision;
        }

        // Handle .N% (percent): ".1%" -> "P1"
        if (pythonSpec.StartsWith(".") && pythonSpec.EndsWith("%"))
        {
            var precision = pythonSpec.Substring(1, pythonSpec.Length - 2);
            if (int.TryParse(precision, out _))
                return "P" + precision;
        }

        // Handle .Ng (general): ".3g" -> "G3"
        if (pythonSpec.StartsWith(".") && pythonSpec.EndsWith("g"))
        {
            var precision = pythonSpec.Substring(1, pythonSpec.Length - 2);
            if (int.TryParse(precision, out _))
                return "G" + precision;
        }

        // Handle 0N (zero-padded): "05" -> "D5" for integers
        if (pythonSpec.StartsWith("0") && pythonSpec.Length > 1)
        {
            var width = pythonSpec.Substring(1);
            if (int.TryParse(width, out _))
                return "D" + width;
        }

        // Fall back to passing through (may not work, but allows custom C# formats)
        return pythonSpec;
    }

    /// <summary>
    /// Checks if a Python format spec is a percent format (.N%) and extracts the precision.
    /// </summary>
    private static bool IsPercentFormat(string pythonSpec, out string precision)
    {
        precision = "0";
        if (pythonSpec.StartsWith(".") && pythonSpec.EndsWith("%"))
        {
            precision = pythonSpec.Substring(1, pythonSpec.Length - 2);
            return int.TryParse(precision, out _);
        }
        return false;
    }

    // ============================================================
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
        _walrusDeclarations.Add(
            LocalDeclarationStatement(
                VariableDeclaration(IdentifierName("var"))
                    .WithVariables(SingletonSeparatedList(
                        VariableDeclarator(Identifier(varName))
                            .WithInitializer(EqualsValueClause(value))))));
        _declaredVariables.Add(varName);

        // The walrus expression evaluates to the variable itself
        return IdentifierName(varName);
    }
}
