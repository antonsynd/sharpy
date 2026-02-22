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
                    collectionKind == "dict" ? CSharpTypeNames.SharpyDict : CSharpTypeNames.SharpySet,
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

                case ForClause:
                    var forError = EmitNotImplementedExpression(
                        "Nested comprehensions (multiple 'for' clauses) are not yet supported. Use a for loop instead.",
                        DiagnosticCodes.CodeGen.NestedComprehension, lineStart, columnStart);
                    return (null!, null!, null, forError);
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
                else
                {
                    var innerExpr = GenerateExpression(part.Expression);

                    // For floating-point types without a format spec, wrap in FormatFloat()
                    // to ensure Python-compatible formatting (e.g., 5.0 instead of 5).
                    // Format-specced interpolations already produce correct output.
                    if (string.IsNullOrEmpty(part.FormatSpec))
                    {
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
                    }

                    var interpolation = Interpolation(ParenthesizedExpression(innerExpr));

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
