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
/// RoslynEmitter partial class: List, set, dict comprehensions and supporting helpers
/// </summary>
internal partial class RoslynEmitter
{
    private ExpressionSyntax GenerateListComprehension(ListComprehension listComp)
    {
        bool elementIsSpread = listComp.Element is SpreadElement;

        // Multi-for comprehensions use imperative codegen (nested foreach loops)
        if (listComp.Clauses.Count(c => c is ForClause) > 1)
            return GenerateImperativeComprehension(listComp.Clauses, listComp.Element, null, null, BuiltinNames.List, elementIsSpread);

        // Single-for: LINQ method chain
        var (chain, param, tupleTarget, errorExpr) = GenerateComprehensionChain(
            listComp.Clauses, "List", listComp.LineStart, listComp.ColumnStart);

        if (errorExpr != null)
            return errorExpr;

        SemanticType? elementSemanticType;
        if (elementIsSpread && listComp.Element is SpreadElement spreadEl)
        {
            // PEP 798: [*it for it in its] — use SelectMany to flatten one level
            var spreadInnerExpr = GenerateExpression(spreadEl.Value);
            var selectManyLambda = MakeComprehensionLambda(param, tupleTarget, spreadInnerExpr);
            chain = InvocationExpression(
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, chain, IdentifierName("SelectMany")))
                .AddArgumentListArguments(Argument(selectManyLambda));

            // Element type T from the spread value type (list[T] → T)
            var spreadSemType = GetExpressionSemanticType(spreadEl);
            elementSemanticType = spreadSemType is GenericType gst && gst.TypeArguments.Count > 0
                ? gst.TypeArguments[0] : null;
        }
        else
        {
            var elementExpr = GenerateExpression(listComp.Element);
            var selectLambda = MakeComprehensionLambda(param, tupleTarget, elementExpr);
            chain = InvocationExpression(
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, chain, IdentifierName("Select")))
                .AddArgumentListArguments(Argument(selectLambda));
            elementSemanticType = GetExpressionSemanticType(listComp.Element);
        }

        var elementTypeSyntax = elementSemanticType != null
            ? _typeMapper.MapSemanticType(elementSemanticType)
            : PredefinedType(Token(SyntaxKind.ObjectKeyword));

        var listType = TypeSyntaxMapper.QualifiedGenericName(CSharpTypeNames.SharpyList, elementTypeSyntax);

        return ObjectCreationExpression(listType)
            .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(chain))));
    }

    private ExpressionSyntax GenerateSetComprehension(SetComprehension setComp)
    {
        bool elementIsSpread = setComp.Element is SpreadElement;

        // Multi-for comprehensions use imperative codegen (nested foreach loops)
        if (setComp.Clauses.Count(c => c is ForClause) > 1)
            return GenerateImperativeComprehension(setComp.Clauses, setComp.Element, null, null, BuiltinNames.Set, elementIsSpread);

        // Single-for: LINQ method chain
        var (chain, param, tupleTarget, errorExpr) = GenerateComprehensionChain(
            setComp.Clauses, "Set", setComp.LineStart, setComp.ColumnStart);

        if (errorExpr != null)
            return errorExpr;

        SemanticType? elementSemanticType;
        if (elementIsSpread && setComp.Element is SpreadElement spreadEl)
        {
            // PEP 798: {*it for it in its} — use SelectMany to flatten one level
            var spreadInnerExpr = GenerateExpression(spreadEl.Value);
            var selectManyLambda = MakeComprehensionLambda(param, tupleTarget, spreadInnerExpr);
            chain = InvocationExpression(
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, chain, IdentifierName("SelectMany")))
                .AddArgumentListArguments(Argument(selectManyLambda));

            // Element type T from the spread value type (set[T] → T, list[T] → T, etc.)
            var spreadSemType = GetExpressionSemanticType(spreadEl);
            elementSemanticType = spreadSemType is GenericType gst && gst.TypeArguments.Count > 0
                ? gst.TypeArguments[0] : null;
        }
        else
        {
            var elementExpr = GenerateExpression(setComp.Element);
            var selectLambda = MakeComprehensionLambda(param, tupleTarget, elementExpr);
            chain = InvocationExpression(
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, chain, IdentifierName("Select")))
                .AddArgumentListArguments(Argument(selectLambda));
            elementSemanticType = GetExpressionSemanticType(setComp.Element);
        }

        var elementTypeSyntax = elementSemanticType != null
            ? _typeMapper.MapSemanticType(elementSemanticType)
            : PredefinedType(Token(SyntaxKind.ObjectKeyword));

        var setType = TypeSyntaxMapper.QualifiedGenericName(CSharpTypeNames.SharpySet, elementTypeSyntax);

        return ObjectCreationExpression(setType)
            .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(chain))));
    }

    private ExpressionSyntax GenerateDictComprehension(DictComprehension dictComp)
    {
        // Multi-for comprehensions use imperative codegen (nested foreach loops)
        if (dictComp.Clauses.Count(c => c is ForClause) > 1)
            return GenerateImperativeComprehension(dictComp.Clauses, null, dictComp.Key, dictComp.Value, BuiltinNames.Dict);

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
            var dictType = TypeSyntaxMapper.QualifiedGenericName(CSharpTypeNames.SharpyDict,
                    _typeMapper.MapSemanticType(keySemanticType),
                    _typeMapper.MapSemanticType(valueSemanticType));
            return CastExpression(dictType, ParenthesizedExpression(toDictInvocation));
        }

        return toDictInvocation;
    }

    private ExpressionSyntax GenerateDictSpreadComprehension(DictSpreadComprehension dictSpreadComp)
    {
        var tempName = GenerateTempVarName("comp");

        // Determine K, V from the semantic type of the spread value (dict[K,V])
        var spreadSemanticType = GetExpressionSemanticType(dictSpreadComp.Spread);
        TypeSyntax kTypeSyntax = PredefinedType(Token(SyntaxKind.ObjectKeyword));
        TypeSyntax vTypeSyntax = PredefinedType(Token(SyntaxKind.ObjectKeyword));

        if (spreadSemanticType is GenericType gDictType
            && gDictType.Name == BuiltinNames.Dict
            && gDictType.TypeArguments.Count >= 2)
        {
            kTypeSyntax = _typeMapper.MapSemanticType(gDictType.TypeArguments[0]);
            vTypeSyntax = _typeMapper.MapSemanticType(gDictType.TypeArguments[1]);
        }

        var dictType = TypeSyntaxMapper.QualifiedGenericName(CSharpTypeNames.SharpyDict, kTypeSyntax, vTypeSyntax);

        // var __comp_N = new Dict<K,V>();
        var tempDecl = LocalDeclarationStatement(
            VariableDeclaration(IdentifierName("var"))
                .WithVariables(SingletonSeparatedList(
                    VariableDeclarator(Identifier(tempName))
                        .WithInitializer(EqualsValueClause(
                            ObjectCreationExpression(dictType)
                                .WithArgumentList(ArgumentList()))))));

        // Inner statement: __comp_N.Update(spread)
        var spreadExpr = GenerateExpression(dictSpreadComp.Spread);
        StatementSyntax innerStmt = ExpressionStatement(
            InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName(tempName),
                    IdentifierName("Update")))
            .AddArgumentListArguments(Argument(spreadExpr)));

        var currentBody = new List<StatementSyntax> { innerStmt };

        // Build nested loop structure from clauses in reverse order
        for (int i = dictSpreadComp.Clauses.Length - 1; i >= 0; i--)
        {
            switch (dictSpreadComp.Clauses[i])
            {
                case IfClause ifClause:
                    var condition = GenerateExpression(ifClause.Condition);
                    currentBody = new List<StatementSyntax> { IfStatement(condition, Block(currentBody)) };
                    break;

                case ForClause forClause:
                    var iterExpr = GenerateExpression(forClause.Iterator);

                    if (forClause.Target is Identifier id)
                    {
                        var loopVar = NameMangler.ToCamelCase(id.Name);
                        var tempLoopVar = GenerateTempVarName("loopVar");

                        _declaredVariables.Add(loopVar);
                        _variableVersions[loopVar] = 0;

                        var varInit = LocalDeclarationStatement(
                            VariableDeclaration(IdentifierName("var"))
                                .WithVariables(SingletonSeparatedList(
                                    VariableDeclarator(Identifier(loopVar))
                                        .WithInitializer(EqualsValueClause(IdentifierName(tempLoopVar))))));

                        var foreachBody = new List<StatementSyntax> { varInit };
                        foreachBody.AddRange(currentBody);

                        currentBody = new List<StatementSyntax>
                        {
                            ForEachStatement(
                                IdentifierName("var"),
                                Identifier(tempLoopVar),
                                iterExpr,
                                Block(foreachBody))
                        };
                    }
                    else if (forClause.Target is TupleLiteral tuple && tuple.Elements.All(e => e is Identifier))
                    {
                        var tempLoopVar = GenerateTempVarName("loopVar");
                        var tupleVars = tuple.Elements.Cast<Identifier>()
                            .Select(e => NameMangler.ToCamelCase(e.Name)).ToList();

                        foreach (var tv in tupleVars)
                        {
                            _declaredVariables.Add(tv);
                            _variableVersions[tv] = 0;
                        }

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

                        currentBody = new List<StatementSyntax>
                        {
                            ForEachStatement(
                                IdentifierName("var"),
                                Identifier(tempLoopVar),
                                iterExpr,
                                Block(foreachBody))
                        };
                    }
                    break;
            }
        }

        _hoistedStatements.Add(tempDecl);
        _hoistedStatements.AddRange(currentBody);

        return IdentifierName(tempName);
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
    /// <param name="collectionKind">BuiltinNames.List, BuiltinNames.Set, or BuiltinNames.Dict</param>
    /// <param name="elementIsSpread">When true, use SpreadMethodName (Extend/UnionWith) instead of AddMethodName</param>
    private ExpressionSyntax GenerateImperativeComprehension(
        ImmutableArray<ComprehensionClause> clauses,
        Expression? element,
        Expression? keyExpr,
        Expression? valueExpr,
        string collectionKind,
        bool elementIsSpread = false)
    {
        var tempName = GenerateTempVarName("comp");

        // Determine collection type via registry
        CollectionTypeRegistry.TryGet(collectionKind, out var collInfo);
        TypeSyntax collectionType;
        if (collectionKind == BuiltinNames.Dict)
        {
            var kType = keyExpr != null ? GetExpressionSemanticType(keyExpr) : null;
            var vType = valueExpr != null ? GetExpressionSemanticType(valueExpr) : null;
            var kTypeSyntax = kType != null
                ? _typeMapper.MapSemanticType(kType)
                : PredefinedType(Token(SyntaxKind.ObjectKeyword));
            var vTypeSyntax = vType != null
                ? _typeMapper.MapSemanticType(vType)
                : PredefinedType(Token(SyntaxKind.ObjectKeyword));
            collectionType = TypeSyntaxMapper.QualifiedGenericName(
                    collInfo!.CSharpTypeName,
                    kTypeSyntax, vTypeSyntax);
        }
        else
        {
            var elemType = element != null ? GetExpressionSemanticType(element) : null;
            var elemTypeSyntax = elemType != null
                ? _typeMapper.MapSemanticType(elemType)
                : PredefinedType(Token(SyntaxKind.ObjectKeyword));
            collectionType = TypeSyntaxMapper.QualifiedGenericName(collInfo!.CSharpTypeName, elemTypeSyntax);
        }

        // var __comp_N = new CollectionType();
        var tempDecl = LocalDeclarationStatement(
            VariableDeclaration(IdentifierName("var"))
                .WithVariables(SingletonSeparatedList(
                    VariableDeclarator(Identifier(tempName))
                        .WithInitializer(EqualsValueClause(
                            ObjectCreationExpression(collectionType)
                                .WithArgumentList(ArgumentList()))))));

        // Build the innermost statement: __comp_N.Add(element), __comp_N.Extend(it), or __comp_N[key] = value
        StatementSyntax innerStmt;
        if (collectionKind == BuiltinNames.Dict)
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
        else if (elementIsSpread && element is SpreadElement spreadElem)
        {
            // PEP 798: __comp_N.Extend(it) / __comp_N.UnionWith(it) for spread elements
            innerStmt = ExpressionStatement(
                InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName(tempName),
                        IdentifierName(collInfo!.SpreadMethodName)))
                    .AddArgumentListArguments(Argument(GenerateExpression(spreadElem.Value))));
        }
        else
        {
            // __comp_N.Add(element);
            innerStmt = ExpressionStatement(
                InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName(tempName),
                        IdentifierName(collInfo!.AddMethodName)))
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
            // When the parameter has an explicit type annotation, C# requires parenthesized
            // lambda syntax: (int x) => body. SimpleLambdaExpression only supports untyped: x => body.
            if (param.Type != null)
            {
                return ParenthesizedLambdaExpression()
                    .WithParameterList(ParameterList(SingletonSeparatedList(param)))
                    .WithExpressionBody(body);
            }

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

        // Add explicit type annotation to lambda parameter to help C# generic type inference.
        // Without this, C# may infer 'object' for the lambda parameter when the collection
        // element type is generic, causing compilation errors on member access.
        if (iterType is GenericType gt && gt.TypeArguments.Count > 0)
        {
            SemanticType? elemType = gt.Name switch
            {
                // DictItemsView[K,V] yields (K, V) tuples
                BuiltinNames.DictItemsView when gt.TypeArguments.Count == 2
                    => new Semantic.TupleType { ElementTypes = gt.TypeArguments.ToList() },
                // DictValuesView[K,V] yields V (second type arg)
                BuiltinNames.DictValuesView when gt.TypeArguments.Count == 2
                    => gt.TypeArguments[1],
                // list[T], set[T], dict[K,V] (keys), DictKeyView[K,V]: first type arg
                _ => gt.TypeArguments[0],
            };
            if (elemType is not UnknownType)
            {
                param = param.WithType(_typeMapper.MapSemanticType(elemType));
            }
        }
        else if (iterType is Semantic.TupleType tupleIterType)
        {
            // Iterating over a tuple — type the parameter with the tuple type itself
            param = param.WithType(_typeMapper.MapSemanticType(tupleIterType));
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
}
