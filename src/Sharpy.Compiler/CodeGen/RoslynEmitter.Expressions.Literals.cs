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
        else if (GetExpressionSemanticType(list) is GenericType listSemType &&
                 listSemType.Name == "list" &&
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

        var listType = TypeMapper.QualifiedGenericName(CSharpTypeNames.SharpyList, elementType);

        // If any element is a spread, use imperative builder pattern
        if (list.Elements.Any(e => e is SpreadElement))
        {
            return GenerateSpreadCollectionBuilder(list.Elements, listType, "Extend", "Add");
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
            _targetTypeContext.Name == "dict" &&
            _targetTypeContext.TypeArguments.Length >= 2)
        {
            keyType = _typeMapper.MapType(_targetTypeContext.TypeArguments[0]);
            valueType = _typeMapper.MapType(_targetTypeContext.TypeArguments[1]);
        }
        else if (GetExpressionSemanticType(dict) is GenericType dictSemType &&
                 dictSemType.Name == "dict" &&
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

        var dictType = TypeMapper.QualifiedGenericName(CSharpTypeNames.SharpyDict, keyType, valueType);

        // If any entry is a spread (**expr), use imperative builder pattern
        if (dict.Entries.Any(entry => entry.Key == null))
        {
            return GenerateSpreadDictBuilder(dict.Entries, dictType);
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
            _targetTypeContext.Name == "set" &&
            _targetTypeContext.TypeArguments.Length > 0)
        {
            elementType = _typeMapper.MapType(_targetTypeContext.TypeArguments[0]);
        }
        else if (GetExpressionSemanticType(set) is GenericType setSemType &&
                 setSemType.Name == "set" &&
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

        var setType = TypeMapper.QualifiedGenericName(CSharpTypeNames.SharpySet, elementType);

        // If any element is a spread, use imperative builder pattern
        if (set.Elements.Any(e => e is SpreadElement))
        {
            return GenerateSpreadCollectionBuilder(set.Elements, setType, "UnionWith", "Add");
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

    private ExpressionSyntax GenerateListComprehension(ListComprehension listComp)
    {
        // Multi-for comprehensions use imperative codegen (nested foreach loops)
        if (listComp.Clauses.Count(c => c is ForClause) > 1)
            return GenerateImperativeComprehension(listComp.Clauses, listComp.Element, null, null, "list");

        // Single-for: LINQ method chain
        var (chain, param, tupleTarget, errorExpr) = GenerateComprehensionChain(
            listComp.Clauses, "List", listComp.LineStart, listComp.ColumnStart);

        if (errorExpr != null)
            return errorExpr;

        // Apply .Select(x => element_expression)
        var elementExpr = GenerateExpression(listComp.Element);
        var selectLambda = MakeComprehensionLambda(param, tupleTarget, elementExpr);

        chain = InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                chain,
                IdentifierName("Select")))
            .AddArgumentListArguments(Argument(selectLambda));

        // Wrap in new Sharpy.List<T>(chain) using semantic type info for T
        var elementSemanticType = GetExpressionSemanticType(listComp.Element);

        var elementTypeSyntax = elementSemanticType != null
            ? _typeMapper.MapSemanticType(elementSemanticType)
            : PredefinedType(Token(SyntaxKind.ObjectKeyword));

        var listType = TypeMapper.QualifiedGenericName(CSharpTypeNames.SharpyList, elementTypeSyntax);

        return ObjectCreationExpression(listType)
            .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(chain))));
    }

    private ExpressionSyntax GenerateSetComprehension(SetComprehension setComp)
    {
        // Multi-for comprehensions use imperative codegen (nested foreach loops)
        if (setComp.Clauses.Count(c => c is ForClause) > 1)
            return GenerateImperativeComprehension(setComp.Clauses, setComp.Element, null, null, "set");

        // Single-for: LINQ method chain
        var (chain, param, tupleTarget, errorExpr) = GenerateComprehensionChain(
            setComp.Clauses, "Set", setComp.LineStart, setComp.ColumnStart);

        if (errorExpr != null)
            return errorExpr;

        // Apply .Select(x => element_expression)
        var elementExpr = GenerateExpression(setComp.Element);
        var selectLambda = MakeComprehensionLambda(param, tupleTarget, elementExpr);

        chain = InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                chain,
                IdentifierName("Select")))
            .AddArgumentListArguments(Argument(selectLambda));

        // Wrap in new Sharpy.Set<T>(chain) using semantic type info for T
        var elementSemanticType = GetExpressionSemanticType(setComp.Element);

        var elementTypeSyntax = elementSemanticType != null
            ? _typeMapper.MapSemanticType(elementSemanticType)
            : PredefinedType(Token(SyntaxKind.ObjectKeyword));

        var setType = TypeMapper.QualifiedGenericName(CSharpTypeNames.SharpySet, elementTypeSyntax);

        return ObjectCreationExpression(setType)
            .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(chain))));
    }

    private ExpressionSyntax GenerateDictComprehension(DictComprehension dictComp)
    {
        // Multi-for comprehensions use imperative codegen (nested foreach loops)
        if (dictComp.Clauses.Count(c => c is ForClause) > 1)
            return GenerateImperativeComprehension(dictComp.Clauses, null, dictComp.Key, dictComp.Value, "dict");

        // Single-for: LINQ method chain
        var (chain, param, tupleTarget, errorExpr) = GenerateComprehensionChain(
            dictComp.Clauses, "Dict", dictComp.LineStart, dictComp.ColumnStart);

        if (errorExpr != null)
            return errorExpr;

        // Generate key and value selector lambdas
        var keyExpr = GenerateExpression(dictComp.Key);
        var valueExpr = GenerateExpression(dictComp.Value);

        var keyLambda = MakeComprehensionLambda(param, tupleTarget, keyExpr);
        var valueLambda = MakeComprehensionLambda(param, tupleTarget, valueExpr);

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
        var keySemanticType = GetExpressionSemanticType(dictComp.Key);
        var valueSemanticType = GetExpressionSemanticType(dictComp.Value);

        if (keySemanticType != null && valueSemanticType != null)
        {
            var dictType = TypeMapper.QualifiedGenericName(CSharpTypeNames.SharpyDict,
                    _typeMapper.MapSemanticType(keySemanticType),
                    _typeMapper.MapSemanticType(valueSemanticType));
            return CastExpression(dictType, ParenthesizedExpression(toDictInvocation));
        }

        return toDictInvocation;
    }

    /// <summary>
    /// Generates imperative codegen for multi-for comprehensions.
    /// Produces a temp collection, nested foreach loops with .Add() calls,
    /// hoists the statements via _hoistedStatements, and returns the temp identifier.
    /// </summary>
    /// <param name="clauses">The comprehension clauses (for/if)</param>
    /// <param name="element">Element expression for list/set comprehensions (null for dict)</param>
    /// <param name="keyExpr">Key expression for dict comprehensions (null for list/set)</param>
    /// <param name="valueExpr">Value expression for dict comprehensions (null for list/set)</param>
    /// <param name="collectionKind">"list", "set", or "dict"</param>
    private ExpressionSyntax GenerateImperativeComprehension(
        ImmutableArray<ComprehensionClause> clauses,
        Expression? element,
        Expression? keyExpr,
        Expression? valueExpr,
        string collectionKind)
    {
        var tempName = GenerateTempVarName("comp");

        // Determine collection type
        TypeSyntax collectionType;
        if (collectionKind == "dict")
        {
            var kType = keyExpr != null ? GetExpressionSemanticType(keyExpr) : null;
            var vType = valueExpr != null ? GetExpressionSemanticType(valueExpr) : null;
            var kTypeSyntax = kType != null
                ? _typeMapper.MapSemanticType(kType)
                : PredefinedType(Token(SyntaxKind.ObjectKeyword));
            var vTypeSyntax = vType != null
                ? _typeMapper.MapSemanticType(vType)
                : PredefinedType(Token(SyntaxKind.ObjectKeyword));
            collectionType = TypeMapper.QualifiedGenericName(
                    CSharpTypeNames.SharpyDict,
                    kTypeSyntax, vTypeSyntax);
        }
        else
        {
            var elemType = element != null ? GetExpressionSemanticType(element) : null;
            var elemTypeSyntax = elemType != null
                ? _typeMapper.MapSemanticType(elemType)
                : PredefinedType(Token(SyntaxKind.ObjectKeyword));
            var typeName = collectionKind == "list" ? CSharpTypeNames.SharpyList : CSharpTypeNames.SharpySet;
            collectionType = TypeMapper.QualifiedGenericName(typeName, elemTypeSyntax);
        }

        // var __comp_N = new CollectionType();
        var tempDecl = LocalDeclarationStatement(
            VariableDeclaration(IdentifierName("var"))
                .WithVariables(SingletonSeparatedList(
                    VariableDeclarator(Identifier(tempName))
                        .WithInitializer(EqualsValueClause(
                            ObjectCreationExpression(collectionType)
                                .WithArgumentList(ArgumentList()))))));

        // Build the innermost statement: __comp_N.Add(element) or __comp_N[key] = value
        StatementSyntax innerStmt;
        if (collectionKind == "dict")
        {
            // __comp_N[key] = value;
            innerStmt = ExpressionStatement(
                AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    ElementAccessExpression(IdentifierName(tempName))
                        .WithArgumentList(BracketedArgumentList(
                            SingletonSeparatedList(Argument(GenerateExpression(keyExpr!))))),
                    GenerateExpression(valueExpr!)));
        }
        else
        {
            // __comp_N.Add(element);
            innerStmt = ExpressionStatement(
                InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName(tempName),
                        IdentifierName("Add")))
                    .AddArgumentListArguments(Argument(GenerateExpression(element!))));
        }

        // Build nested loops from the inside out by processing clauses in reverse
        // Clauses are: [for x in iter1, if cond1, for y in iter2, if cond2, ...]
        // We need to wrap innerStmt from the last clause backward
        var currentBody = new List<StatementSyntax> { innerStmt };

        for (int i = clauses.Length - 1; i >= 0; i--)
        {
            switch (clauses[i])
            {
                case IfClause ifClause:
                    // Wrap current body in if (condition) { ... }
                    var condition = GenerateExpression(ifClause.Condition);
                    var ifStmt = IfStatement(condition, Block(currentBody));
                    currentBody = new List<StatementSyntax> { ifStmt };
                    break;

                case ForClause forClause:
                    var iterExpr = GenerateExpression(forClause.Iterator);

                    if (forClause.Target is Identifier id)
                    {
                        var loopVar = NameMangler.ToCamelCase(id.Name);
                        var tempLoopVar = GenerateTempVarName("loopVar");

                        _declaredVariables.Add(loopVar);
                        _variableVersions[loopVar] = 0;

                        // var __loopVar_N = <iter element>;
                        // var loopVar = __loopVar_N;
                        var varInit = LocalDeclarationStatement(
                            VariableDeclaration(IdentifierName("var"))
                                .WithVariables(SingletonSeparatedList(
                                    VariableDeclarator(Identifier(loopVar))
                                        .WithInitializer(EqualsValueClause(
                                            IdentifierName(tempLoopVar))))));

                        var foreachBody = new List<StatementSyntax> { varInit };
                        foreachBody.AddRange(currentBody);

                        var foreachStmt = ForEachStatement(
                            IdentifierName("var"),
                            Identifier(tempLoopVar),
                            iterExpr,
                            Block(foreachBody));
                        currentBody = new List<StatementSyntax> { foreachStmt };
                    }
                    else if (forClause.Target is TupleLiteral tuple &&
                             tuple.Elements.All(e => e is Identifier))
                    {
                        var tempLoopVar = GenerateTempVarName("loopVar");
                        var tupleVars = tuple.Elements.Cast<Identifier>()
                            .Select(e => NameMangler.ToCamelCase(e.Name)).ToList();

                        foreach (var tv in tupleVars)
                        {
                            _declaredVariables.Add(tv);
                            _variableVersions[tv] = 0;
                        }

                        // var (a, b) = __loopVar_N;
                        var designations = tupleVars
                            .Select(name => (VariableDesignationSyntax)SingleVariableDesignation(Identifier(name)))
                            .ToList();
                        var deconstructStmt = ExpressionStatement(
                            AssignmentExpression(
                                SyntaxKind.SimpleAssignmentExpression,
                                DeclarationExpression(
                                    IdentifierName("var"),
                                    ParenthesizedVariableDesignation(SeparatedList(designations))),
                                IdentifierName(tempLoopVar)));

                        var foreachBody = new List<StatementSyntax> { deconstructStmt };
                        foreachBody.AddRange(currentBody);

                        var foreachStmt = ForEachStatement(
                            IdentifierName("var"),
                            Identifier(tempLoopVar),
                            iterExpr,
                            Block(foreachBody));
                        currentBody = new List<StatementSyntax> { foreachStmt };
                    }
                    break;
            }
        }

        // Hoist: temp declaration + outermost loop
        _hoistedStatements.Add(tempDecl);
        _hoistedStatements.AddRange(currentBody);

        return IdentifierName(tempName);
    }

    /// <summary>
    /// Creates a lambda expression for comprehension Select/Where/ToDictionary calls.
    /// For simple variables, returns a simple lambda: x => body.
    /// For tuple unpacking, returns a block lambda with deconstruction:
    ///   (__t_0) => { var (a, b) = __t_0; return body; }
    /// </summary>
    private ExpressionSyntax MakeComprehensionLambda(
        ParameterSyntax param,
        TupleLiteral? tupleTarget,
        ExpressionSyntax body)
    {
        if (tupleTarget == null)
        {
            return SimpleLambdaExpression(param)
                .WithExpressionBody(body);
        }

        // Tuple unpacking (supports nested): (__t_0) => { var a = __t_0.Item1; ... return body; }
        var paramName = param.Identifier.Text;
        var statements = new List<StatementSyntax>();
        GenerateComprehensionTupleUnpacking(tupleTarget.Elements, paramName, statements);
        statements.Add(ReturnStatement(body));

        return ParenthesizedLambdaExpression()
            .WithParameterList(ParameterList(SingletonSeparatedList(param)))
            .WithBlock(Block(statements));
    }

    /// <summary>
    /// Generates tuple unpacking statements for comprehension lambdas.
    /// Uses NameMangler.ToCamelCase directly (not GetMangledVariableName) because
    /// comprehension variables are pre-registered at fixed version 0.
    /// </summary>
    private void GenerateComprehensionTupleUnpacking(
        ImmutableArray<Expression> targets, string sourceVarName, List<StatementSyntax> statements)
    {
        for (int i = 0; i < targets.Length; i++)
        {
            var itemAccess = MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName(sourceVarName),
                IdentifierName($"Item{i + 1}"));

            if (targets[i] is Identifier id)
            {
                var varName = NameMangler.ToCamelCase(id.Name);
                statements.Add(LocalDeclarationStatement(
                    VariableDeclaration(IdentifierName("var"))
                        .WithVariables(SingletonSeparatedList(
                            VariableDeclarator(Identifier(varName))
                                .WithInitializer(EqualsValueClause(itemAccess))))));
            }
            else if (targets[i] is TupleLiteral nestedTuple)
            {
                var tempVarName = $"__t{_tempVarCounter++}";
                statements.Add(LocalDeclarationStatement(
                    VariableDeclaration(IdentifierName("var"))
                        .WithVariables(SingletonSeparatedList(
                            VariableDeclarator(Identifier(tempVarName))
                                .WithInitializer(EqualsValueClause(itemAccess))))));
                GenerateComprehensionTupleUnpacking(nestedTuple.Elements, tempVarName, statements);
            }
        }
    }

    /// <summary>
    /// Generates the common LINQ chain for comprehensions: validates the first for clause,
    /// extracts the loop variable, and applies all Where clauses. Returns the chain so far,
    /// the parameter syntax for lambdas, optional tuple variable names for deconstruction,
    /// and optionally an error expression if validation failed.
    /// </summary>
    private (ExpressionSyntax Chain, ParameterSyntax Param, TupleLiteral? TupleTarget, ExpressionSyntax? Error) GenerateComprehensionChain(
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
        TupleLiteral? tupleTarget = null;

        if (firstFor.Target is Identifier loopVar)
        {
            var varName = NameMangler.ToCamelCase(loopVar.Name);
            param = Parameter(Identifier(varName));
        }
        else if (firstFor.Target is TupleLiteral tuple)
        {
            // Tuple unpacking (simple or nested): use a temp parameter and deconstruct in lambda body
            var tempName = $"__t_{_tempVarCounter++}";
            param = Parameter(Identifier(tempName));
            tupleTarget = tuple;

            // Register all identifier variables recursively
            RegisterComprehensionTupleVars(tuple.Elements);
        }
        else
        {
            var error = EmitNotImplementedExpression(
                $"Unsupported for-loop target type '{firstFor.Target.GetType().Name}' in comprehension.",
                DiagnosticCodes.CodeGen.UnsupportedExpressionType, lineStart, columnStart);
            return (null!, null!, null, error);
        }

        // Start with the iterator expression
        ExpressionSyntax chain = GenerateExpression(firstFor.Iterator);

        // Enum iteration: replace identifier with Enum.GetValues<EnumType>()
        var iterType = GetExpressionSemanticType(firstFor.Iterator);
        if (iterType is Semantic.UserDefinedType { Symbol.TypeKind: Semantic.TypeKind.Enum } compEnumUdt)
        {
            var enumTypeSyntax = _typeMapper.MapSemanticType(compEnumUdt);
            chain = InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName("Enum"),
                    GenericName(Identifier("GetValues"))
                        .WithTypeArgumentList(TypeArgumentList(
                            SingletonSeparatedList(enumTypeSyntax)))));
        }

        // Apply each if clause as .Where(x => condition)
        foreach (var clause in clauses.Skip(1))
        {
            switch (clause)
            {
                case IfClause ifClause:
                    var condition = GenerateExpression(ifClause.Condition);
                    var lambda = MakeComprehensionLambda(param, tupleTarget, condition);

                    chain = InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            chain,
                            IdentifierName("Where")))
                        .AddArgumentListArguments(Argument(lambda));
                    break;

            }
        }

        return (chain, param, tupleTarget, null);
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

        return InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName("FormattableString"),
                IdentifierName("Invariant")))
            .WithArgumentList(ArgumentList(SingletonSeparatedList(
                Argument(interpolatedString))));
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
            width = int.Parse(pythonSpec.Substring(widthStart, pos - widthStart));

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
                precision = int.Parse(pythonSpec.Substring(precStart, pos - precStart));
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

    private static bool IsTypeChar(char c) => "bcdeEfFgGnosxX%".IndexOf(c) >= 0;

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
            var prec = precision?.ToString() ?? "6";
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
                return "N" + (precision?.ToString() ?? "0");
            if (typeChar == null)
                return "N" + (precision?.ToString() ?? "0");
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
    /// Recursively registers all identifier variables in a tuple target for comprehensions.
    /// Must be called before generating the comprehension body so that variable references resolve.
    /// </summary>
    private void RegisterComprehensionTupleVars(ImmutableArray<Expression> elements)
    {
        foreach (var element in elements)
        {
            if (element is Identifier id)
            {
                var name = NameMangler.ToCamelCase(id.Name);
                _declaredVariables.Add(name);
                _variableVersions[name] = 0;
            }
            else if (element is TupleLiteral nested)
            {
                RegisterComprehensionTupleVars(nested.Elements);
            }
        }
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
        TypeSyntax dictType)
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
                // __spread_N.Update(spreadDict)
                _hoistedStatements.Add(ExpressionStatement(
                    InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName(tempName),
                            IdentifierName("Update")))
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
