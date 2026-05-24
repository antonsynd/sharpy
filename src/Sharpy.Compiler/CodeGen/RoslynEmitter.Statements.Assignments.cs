using System.Collections.Immutable;
using System.IO;
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
/// RoslynEmitter partial class: Assignment statements, variable declarations, and unpacking
/// </summary>
internal partial class RoslynEmitter
{
    private StatementSyntax GenerateAssignment(Assignment assign)
    {
        // Check if this is an assignment of a lambda with default parameters to a simple
        // identifier (first declaration). Emit as a local function instead of a delegate
        // variable, because C# delegates / Func<> don't support optional parameters.
        if (assign.Operator == AssignmentOperator.Assign
            && assign.Target is Identifier lambdaTargetId
            && assign.Value is LambdaExpression lambdaWithDefaults
            && HasDefaultParameters(lambdaWithDefaults))
        {
            var baseName = NameMangler.ToCamelCase(lambdaTargetId.Name);
            var symbol = _context.LookupSymbol(lambdaTargetId.Name);
            var existsAsModuleLevel = symbol != null && GetCodeGenInfo(symbol)?.IsModuleLevel == true;
            var existsAsLocal = _variableVersions.ContainsKey(baseName);

            if (!existsAsModuleLevel && !existsAsLocal)
            {
                // First declaration — emit as local function
                var localFuncName = GetMangledVariableName(lambdaTargetId.Name, isNewDeclaration: true);
                _declaredVariables.Add(localFuncName);
                return GenerateLambdaAsLocalFunction(lambdaWithDefaults, localFuncName);
            }
        }

        var value = GenerateExpression(assign.Value);

        // Handle simple identifier assignment
        if (assign.Target is Identifier name)
        {
            // Check if this is a simple assignment or augmented assignment
            if (assign.Operator == AssignmentOperator.Assign)
            {
                // Simple assignment: x = value
                // In Sharpy, assignments can be redefinitions with type changes
                // However, inside a function/loop, we should update existing vars
                // Get the base name to check if already declared
                var baseName = NameMangler.ToCamelCase(name.Name);

                // Check if this variable was already declared in current scope
                // _variableVersions tracks local variables by base name
                // Also check if this is a module-level variable via CodeGenInfo
                var symbol = _context.LookupSymbol(name.Name);
                var existsAsModuleLevel = symbol != null && GetCodeGenInfo(symbol)?.IsModuleLevel == true;
                var existsAsLocal = _variableVersions.ContainsKey(baseName);

                if (existsAsModuleLevel || existsAsLocal)
                {
                    // Variable exists - just update it with a regular assignment
                    // Clear any Optional narrowing since the variable is being reassigned
                    _narrowing.ClearNarrowing(name.Name);
                    var currentName = GetMangledVariableName(name.Name, isNewDeclaration: false);
                    return ExpressionStatement(
                        AssignmentExpression(
                            SyntaxKind.SimpleAssignmentExpression,
                            IdentifierName(currentName),
                            value));
                }
                else
                {
                    // First declaration of this variable in this scope
                    var varName = GetMangledVariableName(name.Name, isNewDeclaration: true);
                    _declaredVariables.Add(varName);

                    // Check if the value is a lambda/function — C# can't infer delegate
                    // types with 'var'. Use explicit Func<>/Action<> from semantic type.
                    TypeSyntax declType;
                    var semanticType = GetExpressionSemanticType(assign.Value);
                    if (semanticType is not Semantic.FunctionType)
                    {
                        var varSymbol = _context.LookupSymbol(name.Name);
                        if (varSymbol is VariableSymbol vs && vs.Type is Semantic.FunctionType)
                            semanticType = vs.Type;
                    }
                    if (semanticType is Semantic.FunctionType ft && !ft.HasUnresolvedTypes())
                        declType = _typeMapper.MapSemanticType(semanticType);
                    else
                        declType = IdentifierName("var");

                    var declaration = VariableDeclaration(declType)
                        .WithVariables(SingletonSeparatedList(
                            VariableDeclarator(Identifier(varName))
                                .WithInitializer(EqualsValueClause(value))));

                    return LocalDeclarationStatement(declaration);
                }
            }
            else
            {
                // Augmented assignment: x += value
                // This references the current version and modifies it
                var varName = GetMangledVariableName(name.Name, isNewDeclaration: false);
                var target = IdentifierName(varName);

                // For the read side of augmented assignment, apply Optional/Nullable narrowing
                // so that x += 1 with narrowed Optional<int> reads as x.Unwrap() + 1
                // or with narrowed int? reads as x.Value + 1
                ExpressionSyntax readExpr;
                if (_narrowing.IsNarrowed(name.Name))
                {
                    if (_narrowing.IsNullableNarrowed(name.Name))
                    {
                        readExpr = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName(varName), IdentifierName("Value"));
                    }
                    else
                    {
                        readExpr = InvocationExpression(
                            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                IdentifierName(varName), IdentifierName(ProtocolConstants.Unwrap)))
                            .WithArgumentList(ArgumentList());
                    }
                }
                else
                {
                    readExpr = target;
                }

                var augmentedValue = GenerateAugmentedValue(assign.Operator, readExpr, value, assign.Target, assign.Value);

                return ExpressionStatement(
                    AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        target,
                        augmentedValue));
            }
        }

        // Handle index assignment: arr[0] = value
        if (assign.Target is IndexAccess indexAccess)
        {
            var obj = GenerateExpression(indexAccess.Object);
            var index = GenerateExpression(indexAccess.Index);

            // Array index assignment: route through ArrayHelpers for negative index support
            var objectType = GetExpressionSemanticType(indexAccess.Object);
            if (objectType is Semantic.GenericType { Name: BuiltinNames.Array })
            {
                var arrayHelpersSetItem = InvocationExpression(
                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        MakeGlobalQualifiedName("Sharpy", "ArrayHelpers"),
                        IdentifierName("SetItem")));

                if (assign.Operator == AssignmentOperator.Assign)
                {
                    return ExpressionStatement(
                        arrayHelpersSetItem.AddArgumentListArguments(
                            Argument(obj),
                            Argument(index),
                            Argument(value)));
                }

                // Compound assignment (+=, -=, etc.): read via GetItem, compute, write via SetItem
                var getItem = InvocationExpression(
                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        MakeGlobalQualifiedName("Sharpy", "ArrayHelpers"),
                        IdentifierName("GetItem")))
                    .AddArgumentListArguments(
                        Argument(obj),
                        Argument(index));

                var augmented = GenerateAugmentedValue(assign.Operator, getItem, value, assign.Target, assign.Value);

                return ExpressionStatement(
                    arrayHelpersSetItem.AddArgumentListArguments(
                        Argument(obj),
                        Argument(index),
                        Argument(augmented)));
            }

            var elementAccess = ElementAccessExpression(obj)
                .WithArgumentList(BracketedArgumentList(
                    SingletonSeparatedList(Argument(index))));

            var augmentedValue = assign.Operator == AssignmentOperator.Assign
                ? value
                : GenerateAugmentedValue(assign.Operator, elementAccess, value, assign.Target, assign.Value);

            return ExpressionStatement(
                AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    elementAccess,
                    augmentedValue));
        }

        // Handle member assignment: obj.field = value
        if (assign.Target is MemberAccess memberAccess)
        {
            // Event subscription/unsubscription: obj.on_change += handler / -= handler
            // Emit native C# event += / -= instead of desugaring through GenerateAugmentedValue
            if (_context.SemanticInfo?.IsEventAccess(memberAccess) == true
                && (assign.Operator == AssignmentOperator.PlusAssign
                    || assign.Operator == AssignmentOperator.MinusAssign))
            {
                var eventTarget = GenerateMemberAccess(memberAccess);
                var assignKind = assign.Operator == AssignmentOperator.PlusAssign
                    ? SyntaxKind.AddAssignmentExpression
                    : SyntaxKind.SubtractAssignmentExpression;

                return ExpressionStatement(
                    AssignmentExpression(assignKind, eventTarget, value));
            }

            // For simple assignments, clear narrowing on the target field so we emit
            // the raw field (e.g., this.BestScore) not the unwrapped version
            // (e.g., this.BestScore.Unwrap()). Narrowing only applies to reads.
            if (assign.Operator == AssignmentOperator.Assign)
            {
                var path = TryBuildDottedPath(memberAccess);
                if (path != null)
                    _narrowing.ClearNarrowing(path);
            }

            var target = GenerateMemberAccess(memberAccess);

            var assignmentValue = assign.Operator == AssignmentOperator.Assign
                ? value
                : GenerateAugmentedValue(assign.Operator, target, value, assign.Target, assign.Value);

            return ExpressionStatement(
                AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    target,
                    assignmentValue));
        }

        // Handle tuple unpacking: x, y = 1, 2
        if (assign.Target is TupleLiteral tuple)
        {
            // Star unpacking: first, *rest = items
            if (tuple.Elements.Any(e => e is StarExpression))
            {
                var starStmts = new List<StatementSyntax>();
                var starTempVar = $"__t{_tempVarCounter++}";
                starStmts.Add(LocalDeclarationStatement(
                    VariableDeclaration(IdentifierName("var"))
                        .WithVariables(SingletonSeparatedList(
                            VariableDeclarator(Identifier(starTempVar))
                                .WithInitializer(EqualsValueClause(value))))));

                var valueType = GetExpressionSemanticType(assign.Value);
                GenerateStarUnpacking(tuple.Elements, starTempVar, valueType, starStmts);

                for (int i = 0; i < starStmts.Count - 1; i++)
                    _hoistedStatements.Add(starStmts[i]);
                return starStmts[^1];
            }

            // Generate C# tuple deconstruction
            // C#: var (x, y) = (1, 2)

            // Check if all elements are identifiers
            bool allIdentifiers = tuple.Elements.All(e => e is Identifier);

            if (allIdentifiers)
            {
                var identifiers = tuple.Elements.Cast<Identifier>().ToList();

                // Check which variables already exist (mirrors simple assignment path)
                var existenceFlags = identifiers.Select(id =>
                {
                    var baseName = NameMangler.ToCamelCase(id.Name);
                    var symbol = _context.LookupSymbol(id.Name);
                    var existsAsModuleLevel = symbol != null && GetCodeGenInfo(symbol)?.IsModuleLevel == true;
                    var existsAsLocal = _variableVersions.ContainsKey(baseName);
                    return existsAsModuleLevel || existsAsLocal;
                }).ToList();

                bool allExist = existenceFlags.All(e => e);
                bool noneExist = existenceFlags.All(e => !e);

                if (noneExist)
                {
                    // All new — emit: var (a, b) = expr
                    var variables = identifiers
                        .Select(id =>
                        {
                            var varName = GetMangledVariableName(id.Name, isNewDeclaration: true);
                            _declaredVariables.Add(varName);
                            return SingleVariableDesignation(Identifier(varName));
                        })
                        .ToList();

                    var tuplePattern = ParenthesizedVariableDesignation(
                        SeparatedList<VariableDesignationSyntax>(variables));

                    var declExpr = DeclarationExpression(
                        IdentifierName("var"),
                        tuplePattern);

                    return ExpressionStatement(
                        AssignmentExpression(
                            SyntaxKind.SimpleAssignmentExpression,
                            declExpr,
                            value));
                }
                else if (allExist)
                {
                    // All existing — emit: (a, b) = expr (no var)
                    var arguments = identifiers
                        .Select(id =>
                        {
                            _narrowing.ClearNarrowing(id.Name);
                            var currentName = GetMangledVariableName(id.Name, isNewDeclaration: false);
                            return Argument(IdentifierName(currentName));
                        })
                        .ToList();

                    var tupleExpr = TupleExpression(SeparatedList(arguments));

                    return ExpressionStatement(
                        AssignmentExpression(
                            SyntaxKind.SimpleAssignmentExpression,
                            tupleExpr,
                            value));
                }
                else
                {
                    // Mixed — some new, some existing: use temp + individual assignments.
                    // .ItemN access is only valid on ValueTuple types. When the RHS is
                    // a non-tuple type (e.g., list), fall back to deconstruction into
                    // fresh temp variables, then assign each to the correct target.
                    var rhsType = GetExpressionSemanticType(assign.Value);
                    var isTupleRhs = rhsType is Semantic.TupleType;

                    var stmts = new List<StatementSyntax>();

                    if (isTupleRhs)
                    {
                        // ValueTuple RHS — use .ItemN access (common case: a, b = b, a + b)
                        var mixedTempName = $"__t{_tempVarCounter++}";
                        stmts.Add(LocalDeclarationStatement(
                            VariableDeclaration(IdentifierName("var"))
                                .WithVariables(SingletonSeparatedList(
                                    VariableDeclarator(Identifier(mixedTempName))
                                        .WithInitializer(EqualsValueClause(value))))));

                        for (int i = 0; i < identifiers.Count; i++)
                        {
                            var id = identifiers[i];
                            var itemAccess = MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                IdentifierName(mixedTempName),
                                IdentifierName($"Item{i + 1}"));

                            if (existenceFlags[i])
                            {
                                _narrowing.ClearNarrowing(id.Name);
                                var currentName = GetMangledVariableName(id.Name, isNewDeclaration: false);
                                stmts.Add(ExpressionStatement(
                                    AssignmentExpression(
                                        SyntaxKind.SimpleAssignmentExpression,
                                        IdentifierName(currentName),
                                        itemAccess)));
                            }
                            else
                            {
                                var varName = GetMangledVariableName(id.Name, isNewDeclaration: true);
                                _declaredVariables.Add(varName);
                                stmts.Add(LocalDeclarationStatement(
                                    VariableDeclaration(IdentifierName("var"))
                                        .WithVariables(SingletonSeparatedList(
                                            VariableDeclarator(Identifier(varName))
                                                .WithInitializer(EqualsValueClause(itemAccess))))));
                            }
                        }
                    }
                    else
                    {
                        // Non-tuple RHS — deconstruct into fresh temps, then assign
                        var tempNames = identifiers.Select(_ => $"__d{_tempVarCounter++}").ToList();
                        var tempDesignations = tempNames
                            .Select(n => (VariableDesignationSyntax)SingleVariableDesignation(Identifier(n)))
                            .ToList();

                        var deconstructPattern = ParenthesizedVariableDesignation(
                            SeparatedList(tempDesignations));
                        stmts.Add(ExpressionStatement(
                            AssignmentExpression(
                                SyntaxKind.SimpleAssignmentExpression,
                                DeclarationExpression(IdentifierName("var"), deconstructPattern),
                                value)));

                        for (int i = 0; i < identifiers.Count; i++)
                        {
                            var id = identifiers[i];
                            var tempRef = IdentifierName(tempNames[i]);

                            if (existenceFlags[i])
                            {
                                _narrowing.ClearNarrowing(id.Name);
                                var currentName = GetMangledVariableName(id.Name, isNewDeclaration: false);
                                stmts.Add(ExpressionStatement(
                                    AssignmentExpression(
                                        SyntaxKind.SimpleAssignmentExpression,
                                        IdentifierName(currentName),
                                        tempRef)));
                            }
                            else
                            {
                                var varName = GetMangledVariableName(id.Name, isNewDeclaration: true);
                                _declaredVariables.Add(varName);
                                stmts.Add(LocalDeclarationStatement(
                                    VariableDeclaration(IdentifierName("var"))
                                        .WithVariables(SingletonSeparatedList(
                                            VariableDeclarator(Identifier(varName))
                                                .WithInitializer(EqualsValueClause(tempRef))))));
                            }
                        }
                    }

                    // Hoist all but the last statement
                    for (int i = 0; i < stmts.Count - 1; i++)
                        _hoistedStatements.Add(stmts[i]);
                    return stmts[^1];
                }
            }

            // Complex tuple unpacking: (a, b), c = expr
            // Lower to temp variables + .ItemN access, hoisted as flat siblings
            var unpackStmts = new List<StatementSyntax>();
            var tempVarName = $"__t{_tempVarCounter++}";
            unpackStmts.Add(LocalDeclarationStatement(
                VariableDeclaration(IdentifierName("var"))
                    .WithVariables(SingletonSeparatedList(
                        VariableDeclarator(Identifier(tempVarName))
                            .WithInitializer(EqualsValueClause(value))))));
            GenerateRecursiveTupleUnpacking(tuple.Elements, tempVarName, unpackStmts);

            // Hoist all but the last statement; return the last as the result
            for (int i = 0; i < unpackStmts.Count - 1; i++)
                _hoistedStatements.Add(unpackStmts[i]);
            return unpackStmts[^1];
        }

        return EmitNotImplementedStatement(
            $"Unsupported expression type in code generation: assignment target type '{assign.Target.GetType().Name}'",
            DiagnosticCodes.CodeGen.UnsupportedExpressionType, assign.LineStart, assign.ColumnStart);
    }

    private SyntaxKind GetAugmentedAssignmentOperator(AssignmentOperator op)
    {
        return op switch
        {
            AssignmentOperator.PlusAssign => SyntaxKind.AddExpression,
            AssignmentOperator.MinusAssign => SyntaxKind.SubtractExpression,
            AssignmentOperator.StarAssign => SyntaxKind.MultiplyExpression,
            AssignmentOperator.PercentAssign => SyntaxKind.ModuloExpression,
            AssignmentOperator.AndAssign => SyntaxKind.BitwiseAndExpression,
            AssignmentOperator.OrAssign => SyntaxKind.BitwiseOrExpression,
            AssignmentOperator.XorAssign => SyntaxKind.ExclusiveOrExpression,
            AssignmentOperator.LeftShiftAssign => SyntaxKind.LeftShiftExpression,
            AssignmentOperator.RightShiftAssign => SyntaxKind.RightShiftExpression,
            // Special cases handled by GenerateAugmentedValue (require casts or method calls)
            AssignmentOperator.SlashAssign => SyntaxKind.None,  // True division needs cast to double
            AssignmentOperator.DoubleSlashAssign => SyntaxKind.None,
            AssignmentOperator.PowerAssign => SyntaxKind.None,
            AssignmentOperator.NullCoalesceAssign => SyntaxKind.None,
            _ => SyntaxKind.None
        };
    }

    /// <summary>
    /// Generates the value expression for an augmented assignment.
    /// Handles special cases like //= (floor divide) and **= (power) that require
    /// method calls or casts instead of simple binary expressions.
    /// </summary>
    /// <param name="op">The assignment operator</param>
    /// <param name="left">Generated C# expression for the target</param>
    /// <param name="right">Generated C# expression for the value</param>
    /// <param name="targetAst">Original AST target expression (for type inference)</param>
    /// <param name="valueAst">Original AST value expression (for type inference)</param>
    private ExpressionSyntax GenerateAugmentedValue(AssignmentOperator op, ExpressionSyntax left, ExpressionSyntax right, Expression? targetAst = null, Expression? valueAst = null)
    {
        return op switch
        {
            // x **= y → global::System.Math.Pow(x, y)
            AssignmentOperator.PowerAssign =>
                InvocationExpression(
                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        MakeGlobalQualifiedName("System", "Math"),
                        IdentifierName("Pow")))
                    .AddArgumentListArguments(
                        Argument(left),
                        Argument(right)),

            // x /= y → true division with Python semantics (always returns float64)
            // Cast left to double if both operands are integers
            AssignmentOperator.SlashAssign => GenerateTrueDivisionAugmented(left, right, targetAst, valueAst),

            // x //= y → floor division with Python semantics (toward negative infinity)
            // Integer operands: (long)Math.Floor((double)x / y) → result is int64
            // Float operands: Math.Floor(x / y) → result is float type
            AssignmentOperator.DoubleSlashAssign =>
                GenerateFloorDivision(left, right,
                    (targetAst != null && IsFloatExpression(targetAst)) ||
                    (valueAst != null && IsFloatExpression(valueAst))),

            // x ??= y → lowered null coalescing (Optional-aware)
            AssignmentOperator.NullCoalesceAssign =>
                GenerateNullCoalesceValue(left, right, targetAst),

            // All other operators use simple binary expressions
            _ => GenerateAugmentedBinaryExpression(op, left, right, targetAst)
        };
    }

    private ExpressionSyntax GenerateAugmentedBinaryExpression(AssignmentOperator op, ExpressionSyntax left, ExpressionSyntax right, Expression? sourceAst = null)
    {
        var kind = GetAugmentedAssignmentOperator(op);
        if (kind == SyntaxKind.None)
        {
            return EmitNotImplementedExpression(
                $"Unsupported operator in code generation: augmented assignment operator '{op}'",
                DiagnosticCodes.CodeGen.UnsupportedOperator,
                sourceAst?.LineStart, sourceAst?.ColumnStart);
        }
        return BinaryExpression(kind, left, right);
    }

    /// <summary>
    /// Generates a null-coalescing value, aware of Optional vs nullable types.
    /// For Optional&lt;T&gt;: left.IsSome ? left : right (both Optional) or left.UnwrapOr(right)
    /// For nullable/reference types: left ?? right
    /// </summary>
    private ExpressionSyntax GenerateNullCoalesceValue(ExpressionSyntax left, ExpressionSyntax right, Expression? targetAst)
    {
        if (targetAst != null && GetExpressionSemanticType(targetAst) is OptionalType)
        {
            // Optional ??= value → target.IsSome ? target : value (staying Optional)
            // or target.UnwrapOr(value) if rhs is raw T — but ??= assigns back to
            // the same variable, so both sides are Optional in practice.
            return ConditionalExpression(
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, left, IdentifierName("IsSome")),
                left,
                right);
        }
        return BinaryExpression(SyntaxKind.CoalesceExpression, left, right);
    }

    /// <summary>
    /// Generates true division for augmented assignment (x /= y).
    /// If both operands are integers, casts the left to double before division.
    /// </summary>
    private ExpressionSyntax GenerateTrueDivisionAugmented(ExpressionSyntax left, ExpressionSyntax right, Expression? targetAst, Expression? valueAst)
    {
        var targetIsFloat = targetAst != null && IsFloatExpression(targetAst);
        var valueIsFloat = valueAst != null && IsFloatExpression(valueAst);

        if (!targetIsFloat && !valueIsFloat)
        {
            // Both operands are integers: cast left to double
            return BinaryExpression(SyntaxKind.DivideExpression,
                CastExpression(PredefinedType(Token(SyntaxKind.DoubleKeyword)), ParenthesizedExpression(left)),
                right);
        }

        // At least one operand is float, so result will be float naturally
        return BinaryExpression(SyntaxKind.DivideExpression, left, right);
    }

    private StatementSyntax GenerateVariableDeclaration(VariableDeclaration varDecl)
    {
        // Track const variables by their original Sharpy name for consistent reference resolution
        if (varDecl.IsConst)
        {
            _constVariables.Add(varDecl.Name);
        }

        // Check if this is a lambda with default parameters — emit as a local function
        // instead of a delegate. C# delegates / Func<> don't support optional parameters,
        // but local functions do, so `f = lambda x: int, y: int = 10: x + y` becomes
        //   long f(long x, long y = 10) => x + y;
        if (varDecl.InitialValue is LambdaExpression lambdaWithDefaults
            && HasDefaultParameters(lambdaWithDefaults))
        {
            var localFuncName = GetMangledVariableName(varDecl.Name, isNewDeclaration: true);
            _declaredVariables.Add(localFuncName);
            return GenerateLambdaAsLocalFunction(lambdaWithDefaults, localFuncName);
        }

        // IMPORTANT: Generate the initializer expression FIRST, before updating version tracking.
        // This ensures that references to the same variable in the initializer (e.g., x: int = x + 1)
        // use the PREVIOUS version of the variable, not the new one being declared.
        ExpressionSyntax? initialValue = null;
        if (varDecl.InitialValue != null)
        {
            // Set target type context for collection literal type inference
            // This allows list/dict/set literals to use the declared type
            var previousTargetType = _targetTypeContext;
            _targetTypeContext = varDecl.Type;
            try
            {
                initialValue = GenerateExpression(varDecl.InitialValue);
            }
            finally
            {
                _targetTypeContext = previousTargetType;
            }
        }

        // NOW get the mangled variable name (which may update version tracking for redeclarations)
        var varName = varDecl.IsConst
            ? NameMangler.ToConstantCase(varDecl.Name)
            : GetMangledVariableName(varDecl.Name, isNewDeclaration: true);

        // Handle 'auto' type annotation - use 'var' in C#
        // For const without type annotation, infer type from initializer (C# const can't use 'var')
        TypeSyntax typeSyntax;
        if (varDecl.Type != null && varDecl.Type.Name == "auto")
        {
            // Check if the initializer is a lambda or function reference — C# can't
            // infer lambda/method-group types with 'var'. Use semantic type to emit
            // explicit Func<>/Action<> instead.
            var initSemanticType = varDecl.InitialValue != null
                ? GetExpressionSemanticType(varDecl.InitialValue)
                : null;

            // Also check the variable's own symbol type (may have better inference)
            if (initSemanticType is not Semantic.FunctionType)
            {
                var varSymbol = _context.LookupSymbol(varDecl.Name);
                if (varSymbol is VariableSymbol vs && vs.Type is Semantic.FunctionType)
                    initSemanticType = vs.Type;
            }

            if (initSemanticType is Semantic.FunctionType ft && !ft.HasUnresolvedTypes())
            {
                typeSyntax = _typeMapper.MapSemanticType(initSemanticType);
            }
            else
            {
                typeSyntax = IdentifierName("var");
            }
        }
        else if (varDecl.Type == null && varDecl.IsConst && varDecl.InitialValue != null)
        {
            // Infer type from initializer for const declarations without type annotation
            typeSyntax = _typeMapper.InferTypeFromExpression(varDecl.InitialValue);
        }
        else
        {
            typeSyntax = _typeMapper.MapType(varDecl.Type);
        }

        // Track this variable as declared
        _declaredVariables.Add(varName);

        VariableDeclaratorSyntax declarator = initialValue != null
            ? VariableDeclarator(Identifier(varName)).WithInitializer(EqualsValueClause(initialValue))
            : VariableDeclarator(Identifier(varName));

        var declaration = VariableDeclaration(typeSyntax)
            .WithVariables(SingletonSeparatedList(declarator));

        // C# const only works with predefined types (int, string, bool, etc.)
        var modifiers = varDecl.IsConst && IsConstEligibleType(typeSyntax)
            ? TokenList(Token(SyntaxKind.ConstKeyword))
            : TokenList();

        return LocalDeclarationStatement(declaration)
            .WithModifiers(modifiers);
    }

    private FieldDeclarationSyntax? GenerateModuleLevelField(VariableDeclaration varDecl)
    {
        // Check if this variable has execution order issues (assigned before declared, or multiple declarations)
        // If so, skip generating a field - it will be handled as a local variable in Main()
        // UNLESS _forceModuleLevelFields is true (when there's a user-defined main function)
        var symbol = _context.LookupSymbol(varDecl.Name);
        if (symbol != null && HasExecutionOrderIssues(symbol) && !_forceModuleLevelFields)
        {
            return null;
        }
        // Note: If symbol is null, we can't check execution order issues
        // This shouldn't happen in well-typed code that went through semantic analysis

        // Track const variables by their original Sharpy name for consistent reference resolution
        if (varDecl.IsConst)
        {
            _constVariables.Add(varDecl.Name);
        }

        // Module-level fields naming:
        // - Explicitly const declarations use CONSTANT_CASE
        // - Names that look like constants (ALL_CAPS) also use CONSTANT_CASE
        //   This supports Python-style convention where MAX_SIZE implies a constant
        // - Other names use PascalCase
        string varName;
        if (varDecl.IsConst || NameFormDetector.IsConstantCaseName(varDecl.Name))
        {
            varName = NameMangler.ToConstantCase(varDecl.Name);
        }
        else
        {
            varName = NameCasing.ResolveField(varDecl.Name, varDecl.IsNameBacktickEscaped);
        }

        // Check if we've already generated a field with this name (redefinition)
        // Sharpy allows variable redefinition at module level with different types.
        // When there are redefinitions, we return null to handle them as executable
        // statements in Main() to preserve proper execution order semantics.
        if (_moduleFieldNames.Contains(varName))
        {
            // This is a redefinition - handle as executable statement in Main
            return null;
        }

        // Track this field name to detect future redefinitions
        _moduleFieldNames.Add(varName);

        // Handle 'auto' type annotation - for fields, we must resolve to concrete type
        // For const without type annotation, infer type from initializer
        TypeSyntax typeSyntax;
        if (varDecl.Type != null && varDecl.Type.Name == "auto")
        {
            // Infer type from initializer
            if (varDecl.InitialValue != null)
            {
                typeSyntax = _typeMapper.InferTypeFromExpression(varDecl.InitialValue);
            }
            else
            {
                // No initializer - default to object
                typeSyntax = PredefinedType(Token(SyntaxKind.ObjectKeyword));
            }
        }
        else if (varDecl.Type == null && varDecl.IsConst && varDecl.InitialValue != null)
        {
            // Infer type from initializer for const declarations without type annotation
            typeSyntax = _typeMapper.InferTypeFromExpression(varDecl.InitialValue);
        }
        else
        {
            typeSyntax = _typeMapper.MapType(varDecl.Type);
        }

        VariableDeclaratorSyntax declarator;
        if (varDecl.InitialValue != null)
        {
            // Set target type context for collection literal type inference
            var previousTargetType = _targetTypeContext;
            _targetTypeContext = varDecl.Type;
            try
            {
                var value = GenerateExpression(varDecl.InitialValue);
                declarator = VariableDeclarator(Identifier(varName))
                    .WithInitializer(EqualsValueClause(value));
            }
            finally
            {
                _targetTypeContext = previousTargetType;
            }
        }
        else
        {
            declarator = VariableDeclarator(Identifier(varName));
        }

        var declaration = VariableDeclaration(typeSyntax)
            .WithVariables(SingletonSeparatedList(declarator));

        // Module-level fields must be static
        // For const variables, try to use C# const if the initializer is a compile-time literal
        // AND the type is const-eligible (C# predefined types only)
        // Otherwise fall back to public static readonly
        // Regular variables become "public static"
        SyntaxTokenList modifiers;
        if (varDecl.IsConst && IsCompileTimeLiteral(varDecl.InitialValue) && IsConstEligibleType(typeSyntax))
        {
            // Use const for compile-time literals with const-eligible types
            modifiers = TokenList(
                Token(SyntaxKind.PublicKeyword),
                Token(SyntaxKind.ConstKeyword));
        }
        else if (varDecl.IsConst)
        {
            // Use static readonly for non-literal const values
            modifiers = TokenList(
                Token(SyntaxKind.PublicKeyword),
                Token(SyntaxKind.StaticKeyword),
                Token(SyntaxKind.ReadOnlyKeyword));
        }
        else
        {
            // Regular variables become public static
            modifiers = TokenList(
                Token(SyntaxKind.PublicKeyword),
                Token(SyntaxKind.StaticKeyword));
        }

        return FieldDeclaration(declaration)
            .WithModifiers(modifiers);
    }

    /// <summary>
    /// Generates star/rest unpacking: first, *rest, last = items
    /// Lowers to indexed access for non-star elements and slicing for the star element.
    /// </summary>
    private void GenerateStarUnpacking(
        ImmutableArray<Expression> elements, string sourceVar, SemanticType? valueType,
        List<StatementSyntax> statements)
    {
        // Find star position
        int starIndex = -1;
        for (int i = 0; i < elements.Length; i++)
        {
            if (elements[i] is StarExpression)
            {
                starIndex = i;
                break;
            }
        }

        int numBefore = starIndex;
        int numAfter = elements.Length - starIndex - 1;

        // Check if source is a tuple (ValueTuple) — needs .ItemN access instead of indexing
        var isTupleSource = valueType is Semantic.TupleType;
        var tupleArity = isTupleSource ? ((Semantic.TupleType)valueType!).ElementTypes.Count : 0;

        // Determine element type for the Sharpy.List<T> wrapper
        TypeSyntax elementTypeSyntax = PredefinedType(Token(SyntaxKind.ObjectKeyword));
        if (valueType is GenericType { Name: BuiltinNames.List } listType && listType.TypeArguments.Count > 0)
        {
            elementTypeSyntax = _typeMapper.MapSemanticType(listType.TypeArguments[0]);
        }
        else if (valueType is Semantic.TupleType tupleType)
        {
            // Collect the rest element types (those that go into the star variable)
            var restTypes = new List<SemanticType>();
            for (int ri = numBefore; ri < tupleArity - numAfter; ri++)
            {
                if (ri >= 0 && ri < tupleType.ElementTypes.Count)
                    restTypes.Add(tupleType.ElementTypes[ri]);
            }

            if (restTypes.Count > 0 && restTypes.All(t => t.Equals(restTypes[0])))
            {
                elementTypeSyntax = _typeMapper.MapSemanticType(restTypes[0]);
            }
        }

        // Elements before star: name = __t[i] or __t.ItemN (for tuples)
        for (int i = 0; i < numBefore; i++)
        {
            if (elements[i] is Identifier id)
            {
                ExpressionSyntax indexExpr;
                if (isTupleSource)
                {
                    // ValueTuple uses 1-based .ItemN properties
                    indexExpr = MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName(sourceVar),
                        IdentifierName($"Item{i + 1}"));
                }
                else
                {
                    indexExpr = ElementAccessExpression(IdentifierName(sourceVar))
                        .WithArgumentList(BracketedArgumentList(SingletonSeparatedList(
                            Argument(LiteralExpression(
                                SyntaxKind.NumericLiteralExpression,
                                Literal(i))))));
                }

                var baseName = NameMangler.ToCamelCase(id.Name);
                var sym = _context.LookupSymbol(id.Name);
                var existsAsModuleLevel = sym != null && GetCodeGenInfo(sym)?.IsModuleLevel == true;
                var existsAsLocal = _variableVersions.ContainsKey(baseName);

                if (existsAsModuleLevel || existsAsLocal)
                {
                    _narrowing.ClearNarrowing(id.Name);
                    var currentName = GetMangledVariableName(id.Name, isNewDeclaration: false);
                    statements.Add(ExpressionStatement(
                        AssignmentExpression(
                            SyntaxKind.SimpleAssignmentExpression,
                            IdentifierName(currentName),
                            indexExpr)));
                }
                else
                {
                    var varName = GetMangledVariableName(id.Name, isNewDeclaration: true);
                    _declaredVariables.Add(varName);
                    statements.Add(LocalDeclarationStatement(
                        VariableDeclaration(IdentifierName("var"))
                            .WithVariables(SingletonSeparatedList(
                                VariableDeclarator(Identifier(varName))
                                    .WithInitializer(EqualsValueClause(indexExpr))))));
                }
            }
        }

        // Star element: rest = __t.GetSlice(...) or new Sharpy.List<T> { __t.ItemN, ... } (for tuples)
        if (elements[starIndex] is StarExpression starExpr && starExpr.Operand is Identifier starId)
        {
            var starBaseName = NameMangler.ToCamelCase(starId.Name);
            var starSym = _context.LookupSymbol(starId.Name);
            var starExistsAsModuleLevel = starSym != null && GetCodeGenInfo(starSym)?.IsModuleLevel == true;
            var starExistsAsLocal = _variableVersions.ContainsKey(starBaseName);
            var starIsExisting = starExistsAsModuleLevel || starExistsAsLocal;

            ExpressionSyntax starValueExpr;
            if (isTupleSource)
            {
                // Build: new Sharpy.List<T> { __t.ItemN, __t.ItemM, ... }
                var listTypeSyntax = TypeSyntaxMapper.QualifiedGenericName(
                    CSharpTypeNames.SharpyList, elementTypeSyntax);
                var restItems = new List<ExpressionSyntax>();
                for (int ri = numBefore; ri < tupleArity - numAfter; ri++)
                {
                    restItems.Add(MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName(sourceVar),
                        IdentifierName($"Item{ri + 1}")));
                }

                starValueExpr = ObjectCreationExpression(listTypeSyntax)
                    .WithArgumentList(ArgumentList())
                    .WithInitializer(InitializerExpression(
                        SyntaxKind.CollectionInitializerExpression,
                        SeparatedList(restItems)));
            }
            else
            {
                var startArg = numBefore > 0
                    ? (ExpressionSyntax)LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(numBefore))
                    : LiteralExpression(SyntaxKind.NullLiteralExpression);

                var endArg = numAfter > 0
                    ? (ExpressionSyntax)PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression,
                        LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(numAfter)))
                    : LiteralExpression(SyntaxKind.NullLiteralExpression);

                // __t.GetSlice(new global::Sharpy.Slice(start, end))
                var newSlice = ObjectCreationExpression(MakeGlobalQualifiedName("Sharpy", "Slice"))
                    .WithArgumentList(ArgumentList(SeparatedList(new[]
                    {
                        Argument(startArg),
                        Argument(endArg)
                    })));

                starValueExpr = InvocationExpression(
                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName(sourceVar),
                        IdentifierName("GetSlice")))
                    .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(newSlice))));
            }

            if (starIsExisting)
            {
                _narrowing.ClearNarrowing(starId.Name);
                var currentStarName = GetMangledVariableName(starId.Name, isNewDeclaration: false);
                statements.Add(ExpressionStatement(
                    AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        IdentifierName(currentStarName),
                        starValueExpr)));
            }
            else
            {
                var starVarName = GetMangledVariableName(starId.Name, isNewDeclaration: true);
                _declaredVariables.Add(starVarName);
                statements.Add(LocalDeclarationStatement(
                    VariableDeclaration(IdentifierName("var"))
                        .WithVariables(SingletonSeparatedList(
                            VariableDeclarator(Identifier(starVarName))
                                .WithInitializer(EqualsValueClause(starValueExpr))))));
            }
        }

        // Elements after star: name = __t[-n] or __t.ItemN (for tuples)
        for (int i = 0; i < numAfter; i++)
        {
            int elemIndex = starIndex + 1 + i;

            if (elements[elemIndex] is Identifier id)
            {
                ExpressionSyntax afterExpr;
                if (isTupleSource)
                {
                    // Compute 1-based index: tupleArity - numAfter + i + 1
                    int itemIndex = tupleArity - numAfter + i + 1;
                    afterExpr = MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName(sourceVar),
                        IdentifierName($"Item{itemIndex}"));
                }
                else
                {
                    int negIndex = numAfter - i; // distance from end: numAfter, ..., 1
                    afterExpr = ElementAccessExpression(IdentifierName(sourceVar))
                        .WithArgumentList(BracketedArgumentList(SingletonSeparatedList(
                            Argument(PrefixUnaryExpression(
                                SyntaxKind.UnaryMinusExpression,
                                LiteralExpression(
                                    SyntaxKind.NumericLiteralExpression,
                                    Literal(negIndex)))))));
                }

                var baseName = NameMangler.ToCamelCase(id.Name);
                var sym = _context.LookupSymbol(id.Name);
                var existsAsModuleLevel = sym != null && GetCodeGenInfo(sym)?.IsModuleLevel == true;
                var existsAsLocal = _variableVersions.ContainsKey(baseName);

                if (existsAsModuleLevel || existsAsLocal)
                {
                    _narrowing.ClearNarrowing(id.Name);
                    var currentName = GetMangledVariableName(id.Name, isNewDeclaration: false);
                    statements.Add(ExpressionStatement(
                        AssignmentExpression(
                            SyntaxKind.SimpleAssignmentExpression,
                            IdentifierName(currentName),
                            afterExpr)));
                }
                else
                {
                    var varName = GetMangledVariableName(id.Name, isNewDeclaration: true);
                    _declaredVariables.Add(varName);
                    statements.Add(LocalDeclarationStatement(
                        VariableDeclaration(IdentifierName("var"))
                            .WithVariables(SingletonSeparatedList(
                                VariableDeclarator(Identifier(varName))
                                    .WithInitializer(EqualsValueClause(afterExpr))))));
                }
            }
        }
    }

    private void GenerateRecursiveTupleUnpacking(
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
                var baseName = NameMangler.ToCamelCase(id.Name);
                var symbol = _context.LookupSymbol(id.Name);
                var existsAsModuleLevel = symbol != null && GetCodeGenInfo(symbol)?.IsModuleLevel == true;
                var existsAsLocal = _variableVersions.ContainsKey(baseName);

                if (existsAsModuleLevel || existsAsLocal)
                {
                    // Existing variable — update
                    _narrowing.ClearNarrowing(id.Name);
                    var currentName = GetMangledVariableName(id.Name, isNewDeclaration: false);
                    statements.Add(ExpressionStatement(
                        AssignmentExpression(
                            SyntaxKind.SimpleAssignmentExpression,
                            IdentifierName(currentName),
                            itemAccess)));
                }
                else
                {
                    // New variable — declare
                    var varName = GetMangledVariableName(id.Name, isNewDeclaration: true);
                    _declaredVariables.Add(varName);
                    statements.Add(LocalDeclarationStatement(
                        VariableDeclaration(IdentifierName("var"))
                            .WithVariables(SingletonSeparatedList(
                                VariableDeclarator(Identifier(varName))
                                    .WithInitializer(EqualsValueClause(itemAccess))))));
                }
            }
            else if (targets[i] is TupleLiteral nestedTuple)
            {
                var tempVarName = $"__t{_tempVarCounter++}";
                statements.Add(LocalDeclarationStatement(
                    VariableDeclaration(IdentifierName("var"))
                        .WithVariables(SingletonSeparatedList(
                            VariableDeclarator(Identifier(tempVarName))
                                .WithInitializer(EqualsValueClause(itemAccess))))));
                GenerateRecursiveTupleUnpacking(nestedTuple.Elements, tempVarName, statements);
            }
        }
    }
}
