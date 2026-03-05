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
/// RoslynEmitter partial class: Statement generation (control flow, assignments, try/catch)
/// </summary>
internal partial class RoslynEmitter
{
    private StatementSyntax? GenerateBodyStatement(Statement stmt)
    {
        var statements = GenerateBodyStatements(stmt);
        // Flatten multiple statements (walrus hoisted declarations + main statement)
        // into a single statement. If there's exactly one, return it directly;
        // otherwise return null.
        return statements.Count switch
        {
            0 => null,
            1 => statements[0],
            // Multiple statements from walrus hoisting: emit them flat.
            // We return them wrapped in a block to maintain single-statement return type.
            // The caller sites that use SelectMany-style flattening will handle this correctly.
            _ => Block(statements)
        };
    }

    /// <summary>
    /// Generates zero or more C# statements for a single Sharpy statement.
    /// Returns multiple statements when walrus operator (:=) declarations
    /// need to be hoisted before the containing statement.
    /// </summary>
    private List<StatementSyntax> GenerateBodyStatements(Statement stmt)
    {
        _cancellationToken.ThrowIfCancellationRequested();

        // Save any walrus declarations from an outer scope so they aren't
        // accidentally consumed by inner body statement generation.
        List<StatementSyntax>? savedWalrus = null;
        if (_hoistedStatements.Count > 0)
        {
            savedWalrus = new List<StatementSyntax>(_hoistedStatements);
            _hoistedStatements.Clear();
        }

        var result = stmt switch
        {
            ReturnStatement ret => GenerateReturn(ret),
            YieldStatement yieldStmt => GenerateYield(yieldStmt),
            Assignment assign => GenerateAssignment(assign),
            VariableDeclaration varDecl => GenerateVariableDeclaration(varDecl),
            ExpressionStatement exprStmt => GenerateExpressionStatement(exprStmt),
            PassStatement => EmptyStatement(),
            Sharpy.Compiler.Parser.Ast.BreakStatement => SyntaxFactory.BreakStatement(),
            BreakWithFlagStatement breakWithFlag => GenerateBreakWithFlag(breakWithFlag),
            Sharpy.Compiler.Parser.Ast.ContinueStatement => SyntaxFactory.ContinueStatement(),
            AssertStatement assert => GenerateAssert(assert),
            RaiseStatement raise => GenerateRaise(raise),
            IfStatement ifStmt => GenerateIf(ifStmt),
            WhileStatement whileStmt => GenerateWhile(whileStmt),
            ForStatement forStmt => GenerateFor(forStmt),
            TryStatement tryStmt => GenerateTry(tryStmt),
            WithStatement withStmt => GenerateWith(withStmt),
            MatchStatement matchStmt => GenerateMatch(matchStmt),
            _ => null
        };

        if (result == null && stmt is not ImportStatement and not FromImportStatement and not TypeAlias and not PropertyDef)
        {
            _context.AddError(
                $"Internal: unrecognized statement type '{stmt.GetType().Name}' was not emitted. This is a compiler bug — please report it.",
                DiagnosticCodes.CodeGen.UnrecognizedStatementType,
                stmt.LineStart,
                stmt.ColumnStart);
        }

        var output = new List<StatementSyntax>();

        if (result == null)
        {
            // Restore saved walrus declarations
            if (savedWalrus != null)
                _hoistedStatements.AddRange(savedWalrus);
            return output;
        }

        result = AttachLineDirective(result, stmt);

        // If any walrus declarations were accumulated during this statement's
        // expression generation, prepend them as flat siblings.
        if (_hoistedStatements.Count > 0)
        {
            output.AddRange(_hoistedStatements);
            _hoistedStatements.Clear();
        }

        output.Add(result);

        // Restore saved walrus declarations from outer scope
        if (savedWalrus != null)
            _hoistedStatements.AddRange(savedWalrus);

        return output;
    }

    /// <summary>
    /// Attaches a #line directive as leading trivia to a generated C# statement.
    /// This enables .spy file names and line numbers in runtime stack traces.
    /// </summary>
    private StatementSyntax AttachLineDirective(StatementSyntax csharpStatement, Statement astNode)
    {
        if (!_context.EmitLineDirectives)
            return csharpStatement;

        if (string.IsNullOrEmpty(_context.SourceFilePath))
            return csharpStatement;

        if (astNode.LineStart <= 0)
            return csharpStatement;

        var lineDirective = CreateLineDirectiveTrivia(astNode.LineStart, _context.SourceFilePath);
        return csharpStatement.WithLeadingTrivia(lineDirective);
    }

    /// <summary>
    /// Creates #line directive trivia for source mapping.
    /// Produces: #line N "file.spy"
    /// </summary>
    private static SyntaxTriviaList CreateLineDirectiveTrivia(int line, string filePath)
    {
        // Escape backslashes in file path for the #line directive string
        var escapedPath = filePath.Replace("\\", "\\\\");
        return ParseLeadingTrivia($"#line {line} \"{escapedPath}\"\n");
    }

    /// <summary>
    /// Generates a C# statement from a Sharpy expression statement.
    /// In C#, only certain expressions are valid as statements (invocations, assignments, new, ++/--).
    /// For other expressions (literals, arithmetic, comparison, etc.), we use a discard: _ = expr;
    /// </summary>
    private StatementSyntax GenerateExpressionStatement(ExpressionStatement exprStmt)
    {
        var expr = exprStmt.Expression;

        // None as a statement is a no-op (like Python's None expression)
        // We generate an empty statement since `_ = null;` requires type annotation in C#
        if (expr is NoneLiteral)
        {
            return EmptyStatement();
        }

        // Ellipsis as a statement in a concrete method body generates a throw statement
        // Note: For abstract methods/interface methods, ellipsis is handled at the method level
        if (expr is EllipsisLiteral)
        {
            return ThrowStatement(
                ObjectCreationExpression(ParseTypeName("System.NotImplementedException"))
                    .WithArgumentList(ArgumentList()));
        }

        var generated = GenerateExpression(expr);

        // Check if the expression is valid as a C# statement
        if (IsValidCSharpStatementExpression(expr))
        {
            return ExpressionStatement(generated);
        }

        // Otherwise, wrap in a discard: _ = expr;
        return ExpressionStatement(
            AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                IdentifierName("_"),
                generated));
    }

    /// <summary>
    /// Determines if an expression is valid as a standalone C# statement.
    /// Valid statement expressions in C# are:
    /// - Invocation expressions (method calls)
    /// - Object creation expressions (new)
    /// - Assignment expressions
    /// - Increment/decrement expressions (++/--)
    /// - Await expressions
    /// </summary>
    private bool IsValidCSharpStatementExpression(Expression expr)
    {
        return expr switch
        {
            // Method calls are valid statements
            FunctionCall => true,

            // Await expressions are valid C# statement expressions
            Parser.Ast.AwaitExpression => true,

            // All other expressions need a discard
            _ => false
        };
    }

    private StatementSyntax GenerateReturn(ReturnStatement ret)
    {
        // In generator methods, bare return → yield break
        if (ret.Value == null && _isCurrentMethodGenerator)
        {
            return YieldStatement(SyntaxKind.YieldBreakStatement);
        }

        if (ret.Value != null)
        {
            return ReturnStatement(GenerateExpression(ret.Value));
        }
        return ReturnStatement();
    }

    private StatementSyntax GenerateYield(YieldStatement yieldStmt)
    {
        if (!yieldStmt.IsFrom)
        {
            // yield expr → yield return expr;
            return YieldStatement(SyntaxKind.YieldReturnStatement, GenerateExpression(yieldStmt.Value));
        }

        // yield from expr → foreach (var __yieldItem_N in expr) { yield return __yieldItem_N; }
        // In async generators, if the iterable is IAsyncEnumerable<T>, emit await foreach.
        var iterableExpr = GenerateExpression(yieldStmt.Value);
        var itemName = GenerateTempVarName("yieldItem");
        var itemIdentifier = Identifier(itemName);
        var yieldReturn = YieldStatement(
            SyntaxKind.YieldReturnStatement,
            IdentifierName(itemName));
        var foreachStmt = ForEachStatement(
            IdentifierName("var"),
            itemIdentifier,
            iterableExpr,
            Block(yieldReturn));

        // Check if we need await foreach: async context + async iterable type
        if (_isCurrentMethodAsync && IsAsyncEnumerableType(yieldStmt.Value))
        {
            return foreachStmt.WithAwaitKeyword(Token(SyntaxKind.AwaitKeyword));
        }

        return foreachStmt;
    }

    /// <summary>
    /// Checks if an expression's semantic type is IAsyncEnumerable&lt;T&gt;.
    /// Used to determine whether yield from should emit await foreach.
    /// </summary>
    private bool IsAsyncEnumerableType(Parser.Ast.Expression expr)
    {
        var type = GetExpressionSemanticType(expr);
        return type is GenericType { Name: Shared.CSharpTypeNames.IAsyncEnumerable };
    }

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
                    ClearNarrowing(name.Name);
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
                if (IsNarrowed(name.Name))
                {
                    if (IsNullableNarrowed(name.Name))
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
                    ClearNarrowing(path);
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

                // In Sharpy, tuple unpacking is always a new declaration/redefinition
                // Use: var (x, y) = expr
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

                // Create a declaration expression
                var declExpr = DeclarationExpression(
                    IdentifierName("var"),
                    tuplePattern);

                return ExpressionStatement(
                    AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        declExpr,
                        value));
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
            // x **= y → System.Math.Pow(x, y)
            // Note: We use fully qualified System.Math to avoid conflicts with Sharpy.Math namespace
            AssignmentOperator.PowerAssign =>
                InvocationExpression(
                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName("System"),
                            IdentifierName("Math")),
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

    /// <summary>
    /// Generate a break statement with flag assignment for loop else support.
    /// Generates: { flagName = false; break; }
    /// </summary>
    private StatementSyntax GenerateBreakWithFlag(BreakWithFlagStatement breakStmt)
    {
        return Block(
            ExpressionStatement(
                AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    IdentifierName(breakStmt.FlagName),
                    LiteralExpression(SyntaxKind.FalseLiteralExpression))),
            SyntaxFactory.BreakStatement());
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

        var modifiers = varDecl.IsConst
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
            varName = NameMangler.ToPascalCase(varDecl.Name);
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
        // Otherwise fall back to public static readonly
        // Regular variables become "public static"
        SyntaxTokenList modifiers;
        if (varDecl.IsConst && IsCompileTimeLiteral(varDecl.InitialValue))
        {
            // Use const for compile-time literals
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

    private static bool IsCompileTimeLiteral(Expression? expr)
    {
        // Check if the expression is a compile-time literal that can be used with C# const
        return expr switch
        {
            IntegerLiteral => true,
            FloatLiteral => true,
            StringLiteral => true,
            BooleanLiteral => true,
            NoneLiteral => true,
            _ => false
        };
    }

    private StatementSyntax GenerateAssert(AssertStatement assert)
    {
        // assert condition, message → Debug.Assert(condition, message)
        var condition = GenerateExpression(assert.Test);

        InvocationExpressionSyntax invocation;
        if (assert.Message != null)
        {
            var message = GenerateExpression(assert.Message);
            invocation = InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    ParseName("System.Diagnostics.Debug"),
                    IdentifierName("Assert")))
                .AddArgumentListArguments(
                    Argument(condition),
                    Argument(message));
        }
        else
        {
            invocation = InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    ParseName("System.Diagnostics.Debug"),
                    IdentifierName("Assert")))
                .AddArgumentListArguments(Argument(condition));
        }

        return ExpressionStatement(invocation);
    }

    private StatementSyntax GenerateRaise(RaiseStatement raise)
    {
        if (raise.Exception != null)
        {
            var exception = GenerateExpression(raise.Exception);
            return ThrowStatement(exception);
        }

        // Re-throw the current exception
        return ThrowStatement();
    }

    private StatementSyntax GenerateIf(IfStatement ifStmt)
    {
        // Detect Optional narrowing patterns:
        // if x is not None: → x is narrowed to T in the then-body
        // if x is None: → x is narrowed to T in the else-body
        // if x is not None and y is not None: → both narrowed in then-body
        var narrowingInfo = DetectOptionalNarrowings(ifStmt.Test);

        // Detect isinstance narrowing patterns:
        // if isinstance(x, MyType): → x is narrowed to MyType in the then-body
        // if not isinstance(x, MyType): → x is narrowed to MyType in the else-body
        var isInstanceNarrowingInfo = DetectIsInstanceNarrowings(ifStmt.Test);

        var condition = GenerateExpression(ifStmt.Test);

        // Generate then-block with narrowing if applicable
        BlockSyntax thenBlock;

        // Push isinstance narrowings for the then-body only when NarrowInThen is true
        if (isInstanceNarrowingInfo.HasValue && isInstanceNarrowingInfo.Value.NarrowInThen)
        {
            foreach (var (varName, typeName) in isInstanceNarrowingInfo.Value.Narrowings)
                PushIsInstanceNarrowing(varName, typeName);
        }

        if (narrowingInfo.HasValue && narrowingInfo.Value.NarrowInThen)
        {
            foreach (var name in narrowingInfo.Value.VariableNames)
                PushNarrowing(name);
            thenBlock = Block(ifStmt.ThenBody.SelectMany(GenerateBodyStatements));
            foreach (var name in narrowingInfo.Value.VariableNames)
                PopNarrowing(name);
        }
        else
        {
            thenBlock = Block(ifStmt.ThenBody.SelectMany(GenerateBodyStatements));
        }

        // Pop isinstance narrowings after then-body
        if (isInstanceNarrowingInfo.HasValue && isInstanceNarrowingInfo.Value.NarrowInThen)
        {
            foreach (var (varName, _) in isInstanceNarrowingInfo.Value.Narrowings)
                PopIsInstanceNarrowing(varName);
        }

        ElseClauseSyntax? elseClause = null;

        // Process elif clauses from last to first to build nested if-else structure
        if (ifStmt.ElifClauses.Length > 0 || ifStmt.ElseBody.Length > 0)
        {
            StatementSyntax? currentElse = null;

            // Start with the final else block if it exists
            if (ifStmt.ElseBody.Length > 0)
            {
                // Push isinstance narrowings for else-body when NarrowInThen is false
                // (i.e., `not isinstance(x, T)` → narrow x to T in else branch)
                if (isInstanceNarrowingInfo.HasValue && !isInstanceNarrowingInfo.Value.NarrowInThen)
                {
                    foreach (var (varName, typeName) in isInstanceNarrowingInfo.Value.Narrowings)
                        PushIsInstanceNarrowing(varName, typeName);
                }

                // Generate else-block with narrowing if applicable (is None → narrow in else)
                if (narrowingInfo.HasValue && !narrowingInfo.Value.NarrowInThen)
                {
                    foreach (var name in narrowingInfo.Value.VariableNames)
                        PushNarrowing(name);
                    currentElse = Block(ifStmt.ElseBody.SelectMany(GenerateBodyStatements));
                    foreach (var name in narrowingInfo.Value.VariableNames)
                        PopNarrowing(name);
                }
                else
                {
                    currentElse = Block(ifStmt.ElseBody.SelectMany(GenerateBodyStatements));
                }

                // Pop isinstance narrowings after else-body
                if (isInstanceNarrowingInfo.HasValue && !isInstanceNarrowingInfo.Value.NarrowInThen)
                {
                    foreach (var (varName, _) in isInstanceNarrowingInfo.Value.Narrowings)
                        PopIsInstanceNarrowing(varName);
                }
            }

            // Process elif clauses in reverse order
            for (int i = ifStmt.ElifClauses.Length - 1; i >= 0; i--)
            {
                var elif = ifStmt.ElifClauses[i];
                var elifCondition = GenerateExpression(elif.Test);

                // Detect and apply narrowing for this elif's condition
                var elifNarrowing = DetectOptionalNarrowings(elif.Test);
                var elifIsInstanceNarrowingInfo = DetectIsInstanceNarrowings(elif.Test);

                // Push isinstance narrowings for elif body (only when NarrowInThen is true)
                if (elifIsInstanceNarrowingInfo.HasValue && elifIsInstanceNarrowingInfo.Value.NarrowInThen)
                {
                    foreach (var (varName, typeName) in elifIsInstanceNarrowingInfo.Value.Narrowings)
                        PushIsInstanceNarrowing(varName, typeName);
                }

                BlockSyntax elifBody;
                if (elifNarrowing.HasValue && elifNarrowing.Value.NarrowInThen)
                {
                    foreach (var name in elifNarrowing.Value.VariableNames)
                        PushNarrowing(name);
                    elifBody = Block(elif.Body.SelectMany(GenerateBodyStatements));
                    foreach (var name in elifNarrowing.Value.VariableNames)
                        PopNarrowing(name);
                }
                else
                {
                    elifBody = Block(elif.Body.SelectMany(GenerateBodyStatements));
                }

                // Pop isinstance narrowings after elif body
                if (elifIsInstanceNarrowingInfo.HasValue && elifIsInstanceNarrowingInfo.Value.NarrowInThen)
                {
                    foreach (var (varName, _) in elifIsInstanceNarrowingInfo.Value.Narrowings)
                        PopIsInstanceNarrowing(varName);
                }

                var elifElseClause = currentElse != null ? ElseClause(currentElse) : null;
                var elifStatement = IfStatement(elifCondition, elifBody, elifElseClause);

                currentElse = elifStatement;
            }

            if (currentElse != null)
            {
                elseClause = ElseClause(currentElse);
            }
        }

        return IfStatement(condition, thenBlock, elseClause);
    }

    /// <summary>
    /// Detects Optional narrowing patterns in a condition expression.
    /// Returns the list of variable names to narrow and which branch they narrow in.
    /// Handles simple patterns (x is not None), compound patterns (x is not None and y is not None),
    /// and mixed compound patterns.
    /// </summary>
    private (IReadOnlyList<string> VariableNames, bool NarrowInThen)? DetectOptionalNarrowings(Expression test)
    {
        var isNotNone = new List<string>();
        var isNone = new List<string>();
        CollectNarrowingPatterns(test, isNotNone, isNone);

        // All `is not None` patterns → narrow in then-body
        if (isNotNone.Count > 0 && isNone.Count == 0)
            return (isNotNone, NarrowInThen: true);

        // Single `is None` pattern (no compound) → narrow in else-body
        if (isNone.Count > 0 && isNotNone.Count == 0)
            return (isNone, NarrowInThen: false);

        // Mixed or no narrowing patterns
        return null;
    }

    /// <summary>
    /// Recursively collects Optional narrowing patterns from an expression.
    /// Only traverses `and` operators (all must be true → all can be narrowed).
    /// `or` operators are not traversed (only one side needs to be true).
    /// </summary>
    private void CollectNarrowingPatterns(Expression expr, List<string> isNotNone, List<string> isNone)
    {
        if (expr is not BinaryOp binOp)
            return;

        // For `and`: both sides must be true, so collect from both
        if (binOp.Operator == BinaryOperator.And)
        {
            CollectNarrowingPatterns(binOp.Left, isNotNone, isNone);
            CollectNarrowingPatterns(binOp.Right, isNotNone, isNone);
            return;
        }

        // For `or`: don't collect (can't narrow individual variables)
        if (binOp.Operator == BinaryOperator.Or)
            return;

        if (binOp.Right is not NoneLiteral)
            return;

        // Extract narrowing key: simple identifier or dotted path (self.field)
        var narrowingKey = AstHelper.ExtractNarrowingKey(binOp.Left);
        if (narrowingKey == null)
            return;

        var leftType = GetExpressionSemanticType(binOp.Left);
        if (leftType is null)
            return;

        bool isValueTypeNullable = leftType is NullableType && leftType.IsValueType;
        bool isRefTypeNullable = leftType is NullableType && !leftType.IsValueType;

        // Reference-type nullables (string?, etc.) don't need codegen narrowing —
        // C# automatically narrows them after a null check.
        if (isRefTypeNullable)
            return;

        if (leftType is not OptionalType && !isValueTypeNullable)
            return;

        if (binOp.Operator == BinaryOperator.IsNot)
        {
            isNotNone.Add(narrowingKey);
            // Track value-type nullables so the emitter uses .Value instead of .Unwrap()
            if (isValueTypeNullable)
                _isNullableNarrowing.Add(narrowingKey);
        }
        else if (binOp.Operator == BinaryOperator.Is)
        {
            isNone.Add(narrowingKey);
            if (isValueTypeNullable)
                _isNullableNarrowing.Add(narrowingKey);
        }
    }

    /// <summary>
    /// Detects isinstance narrowing patterns in a condition expression.
    /// Returns (variableName, csharpTypeName) pairs for each isinstance(var, Type) found,
    /// along with a NarrowInThen flag indicating which branch the narrowing applies to.
    /// Handles negated patterns: not isinstance(x, T) → narrow in else branch.
    /// Handles compound `and` conditions: isinstance(x, A) and isinstance(y, B).
    /// </summary>
    private (IReadOnlyList<(string VariableName, string CSharpTypeName)> Narrowings, bool NarrowInThen)? DetectIsInstanceNarrowings(Expression test)
    {
        // Check for negated isinstance: not isinstance(x, T)
        if (test is UnaryOp { Operator: UnaryOperator.Not } unary)
        {
            var negatedResult = new List<(string, string)>();
            CollectIsInstancePatterns(unary.Operand, negatedResult);
            if (negatedResult.Count > 0)
                return (negatedResult, NarrowInThen: false);
        }

        var result = new List<(string, string)>();
        CollectIsInstancePatterns(test, result);
        if (result.Count > 0)
            return (result, NarrowInThen: true);

        return null;
    }

    private void CollectIsInstancePatterns(Expression expr, List<(string VariableName, string CSharpTypeName)> results)
    {
        // isinstance(var, Type) or isinstance(obj.member, Type)
        if (expr is FunctionCall call
            && call.Function is Identifier funcName
            && funcName.Name == "isinstance"
            && call.Arguments.Length == 2
            && call.Arguments[1] is Identifier typeId)
        {
            string? key = call.Arguments[0] switch
            {
                Identifier varId => varId.Name,
                MemberAccess ma => TryBuildDottedPath(ma),
                _ => null
            };

            if (key != null)
            {
                // Use TypeMapper to resolve builtin types (str→string, int→int)
                // and user-defined types (dog→Dog) to their C# names for casts.
                var typeAnnotation = new TypeAnnotation { Name = typeId.Name };
                var csharpType = _typeMapper.MapType(typeAnnotation).NormalizeWhitespace().ToFullString();
                results.Add((key, csharpType));
                return;
            }
        }

        // Compound `and`: both sides must be true
        if (expr is BinaryOp binOp && binOp.Operator == BinaryOperator.And)
        {
            CollectIsInstancePatterns(binOp.Left, results);
            CollectIsInstancePatterns(binOp.Right, results);
        }
    }

    /// <summary>
    /// Builds a narrowing key from a MemberAccess chain (e.g., self.value -> "self.value").
    /// Delegates to <see cref="AstHelper.ExtractNarrowingKey"/>.
    /// </summary>
    private static string? TryBuildDottedPath(MemberAccess ma)
        => AstHelper.ExtractNarrowingKey(ma);

    private StatementSyntax GenerateWhile(WhileStatement whileStmt)
    {
        // Detect Optional narrowing in while condition:
        // while x is not None: → x is narrowed to T in the loop body
        var narrowingInfo = DetectOptionalNarrowings(whileStmt.Test);

        // Detect isinstance narrowing in while condition:
        // while isinstance(x, MyType): → x is narrowed to MyType in the loop body
        // (negated isinstance doesn't apply to while — loop body runs when condition is true)
        var isInstanceNarrowingInfo = DetectIsInstanceNarrowings(whileStmt.Test);

        // For walrus operators in while conditions, use inline assignment mode so the
        // expression is re-evaluated each iteration instead of being hoisted once.
        var hasWalrus = AstHelper.ContainsWalrusExpression(whileStmt.Test);
        if (hasWalrus)
        {
            _walrusInlineMode = true;
            _walrusPreDeclarations.Clear();
        }

        var condition = GenerateExpression(whileStmt.Test);

        if (hasWalrus)
            _walrusInlineMode = false;

        // If there's no else clause, generate simple while loop
        if (whileStmt.ElseBody.IsEmpty)
        {
            if (isInstanceNarrowingInfo.HasValue && isInstanceNarrowingInfo.Value.NarrowInThen)
            {
                foreach (var (varName, typeName) in isInstanceNarrowingInfo.Value.Narrowings)
                    PushIsInstanceNarrowing(varName, typeName);
            }

            if (narrowingInfo.HasValue && narrowingInfo.Value.NarrowInThen)
            {
                foreach (var name in narrowingInfo.Value.VariableNames)
                    PushNarrowing(name);
                var body = Block(whileStmt.Body.SelectMany(GenerateBodyStatements));
                foreach (var name in narrowingInfo.Value.VariableNames)
                    PopNarrowing(name);

                if (isInstanceNarrowingInfo.HasValue && isInstanceNarrowingInfo.Value.NarrowInThen)
                {
                    foreach (var (varName, _) in isInstanceNarrowingInfo.Value.Narrowings)
                        PopIsInstanceNarrowing(varName);
                }
                return WrapWithWalrusPreDeclarations(WhileStatement(condition, body));
            }
            var simpleBody = Block(whileStmt.Body.SelectMany(GenerateBodyStatements));

            if (isInstanceNarrowingInfo.HasValue && isInstanceNarrowingInfo.Value.NarrowInThen)
            {
                foreach (var (varName, _) in isInstanceNarrowingInfo.Value.Narrowings)
                    PopIsInstanceNarrowing(varName);
            }
            return WrapWithWalrusPreDeclarations(WhileStatement(condition, simpleBody));
        }

        // Loop with else clause: use boolean flag pattern
        // bool _loopCompleted = true;
        // while (condition) { ... if (break) { _loopCompleted = false; break; } }
        // if (_loopCompleted) { elseBody }
        var flagName = GenerateTempVarName("loopCompleted");
        var statements = new List<StatementSyntax>();

        // bool _loopCompleted = true;
        statements.Add(LocalDeclarationStatement(
            VariableDeclaration(PredefinedType(Token(SyntaxKind.BoolKeyword)))
                .WithVariables(SingletonSeparatedList(
                    VariableDeclarator(Identifier(flagName))
                        .WithInitializer(EqualsValueClause(LiteralExpression(SyntaxKind.TrueLiteralExpression)))))));

        // Transform the body to set flag to false before break
        var transformedBody = TransformLoopBodyForElse(whileStmt.Body, flagName);
        BlockSyntax bodyBlock;

        if (isInstanceNarrowingInfo.HasValue && isInstanceNarrowingInfo.Value.NarrowInThen)
        {
            foreach (var (varName, typeName) in isInstanceNarrowingInfo.Value.Narrowings)
                PushIsInstanceNarrowing(varName, typeName);
        }

        if (narrowingInfo.HasValue && narrowingInfo.Value.NarrowInThen)
        {
            foreach (var name in narrowingInfo.Value.VariableNames)
                PushNarrowing(name);
            bodyBlock = Block(transformedBody.SelectMany(GenerateBodyStatements));
            foreach (var name in narrowingInfo.Value.VariableNames)
                PopNarrowing(name);
        }
        else
        {
            bodyBlock = Block(transformedBody.SelectMany(GenerateBodyStatements));
        }

        if (isInstanceNarrowingInfo.HasValue && isInstanceNarrowingInfo.Value.NarrowInThen)
        {
            foreach (var (varName, _) in isInstanceNarrowingInfo.Value.Narrowings)
                PopIsInstanceNarrowing(varName);
        }

        // while (condition) { transformedBody }
        statements.Add(WhileStatement(condition, bodyBlock));

        // if (_loopCompleted) { elseBody }
        var elseBodyBlock = Block(whileStmt.ElseBody.SelectMany(GenerateBodyStatements));
        statements.Add(IfStatement(IdentifierName(flagName), elseBodyBlock));

        return WrapWithWalrusPreDeclarations(Block(statements));
    }

    /// <summary>
    /// If walrus pre-declarations were accumulated during inline mode (while-loop conditions),
    /// wraps the statement in a block containing the pre-declarations followed by the statement.
    /// Otherwise returns the statement unchanged.
    /// </summary>
    private StatementSyntax WrapWithWalrusPreDeclarations(StatementSyntax statement)
    {
        if (_walrusPreDeclarations.Count == 0)
            return statement;

        var wrapped = new List<StatementSyntax>(_walrusPreDeclarations) { statement };
        _walrusPreDeclarations.Clear();
        return Block(wrapped);
    }

    private StatementSyntax GenerateFor(ForStatement forStmt)
    {
        // For-in loop: for item in items: → foreach (var item in items)
        var iteratorType = GetExpressionSemanticType(forStmt.Iterator);
        var iterator = GenerateExpression(forStmt.Iterator);

        // Enum iteration: `for c in Color:` → `foreach (var c in Enum.GetValues<Color>())`
        if (iteratorType is Semantic.UserDefinedType { Symbol.TypeKind: Semantic.TypeKind.Enum } enumUdt)
        {
            var enumTypeSyntax = _typeMapper.MapSemanticType(enumUdt);
            iterator = InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName("Enum"),
                    GenericName(Identifier("GetValues"))
                        .WithTypeArgumentList(TypeArgumentList(
                            SingletonSeparatedList(enumTypeSyntax)))));
        }

        // If there's no else clause, generate simple foreach loop
        if (forStmt.ElseBody.IsEmpty)
        {
            return GenerateForEachCore(forStmt.Target, iterator, forStmt.Body, iteratorType, forStmt.IsAsync);
        }

        // Loop with else clause: use boolean flag pattern
        var flagName = GenerateTempVarName("loopCompleted");
        var statements = new List<StatementSyntax>();

        // bool _loopCompleted = true;
        statements.Add(LocalDeclarationStatement(
            VariableDeclaration(PredefinedType(Token(SyntaxKind.BoolKeyword)))
                .WithVariables(SingletonSeparatedList(
                    VariableDeclarator(Identifier(flagName))
                        .WithInitializer(EqualsValueClause(LiteralExpression(SyntaxKind.TrueLiteralExpression)))))));

        // Transform the body to set flag to false before break
        var transformedBody = TransformLoopBodyForElse(forStmt.Body, flagName);

        // foreach (...) { transformedBody }
        statements.Add(GenerateForEachCore(forStmt.Target, iterator, transformedBody, iteratorType, forStmt.IsAsync));

        // if (_loopCompleted) { elseBody }
        var elseBodyBlock = Block(forStmt.ElseBody.SelectMany(GenerateBodyStatements));
        statements.Add(IfStatement(IdentifierName(flagName), elseBodyBlock));

        return Block(statements);
    }

    /// <summary>
    /// Generates a foreach statement from AST body statements.
    /// This overload registers loop variables before generating the body so that
    /// assignments to the loop variable inside are treated as updates.
    ///
    /// In C#, foreach iteration variables are read-only. To allow Python-like
    /// modification of the loop variable, we always use a pattern like:
    ///   foreach (var __loopVar in items) { var i = __loopVar; ... }
    /// This allows the user to modify 'i' inside the loop body.
    /// </summary>
    private StatementSyntax GenerateForEachCore(Expression target, ExpressionSyntax iterator, IReadOnlyList<Statement> bodyStatements, SemanticType? iteratorType = null, bool isAsync = false)
    {
        // Save scope so that loop variables and body-declared variables are
        // removed from scope after the loop (Sharpy: loop vars are block-scoped).
        var scopeSnapshot = SaveScope();

        try
        {
            return GenerateForEachCoreInner(target, iterator, bodyStatements, iteratorType, isAsync);
        }
        finally
        {
            RestoreScope(scopeSnapshot);
        }
    }

    private StatementSyntax GenerateForEachCoreInner(Expression target, ExpressionSyntax iterator, IReadOnlyList<Statement> bodyStatements, SemanticType? iteratorType = null, bool isAsync = false)
    {
        if (target is Identifier varName)
        {
            var loopVar = NameMangler.ToCamelCase(varName.Name);
            var tempLoopVar = GenerateTempVarName("loopVar");

            // Check if the variable is already declared in an enclosing scope
            bool varExistsInOuterScope = _declaredVariables.Contains(loopVar) || _variableVersions.ContainsKey(loopVar);

            // Register the loop variable BEFORE generating the body
            // so that assignments to it are treated as updates
            if (!varExistsInOuterScope)
            {
                _declaredVariables.Add(loopVar);
            }
            _variableVersions[loopVar] = 0;

            // Generate the body - assignments to loopVar will be updates, not declarations
            var body = Block(bodyStatements.SelectMany(GenerateBodyStatements));

            // String iteration: C# foreach yields char, but Sharpy types loop var as str.
            // Wrap with .ToString() to bridge the type gap.
            ExpressionSyntax loopVarValue = IdentifierName(tempLoopVar);
            if (iteratorType == Semantic.SemanticType.Str)
            {
                loopVarValue = InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        loopVarValue,
                        IdentifierName("ToString")));
            }

            // Create the assignment or declaration at the start of the body
            StatementSyntax loopVarInit;
            if (varExistsInOuterScope)
            {
                // Variable exists in outer scope - just assign to it
                loopVarInit = ExpressionStatement(
                    AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        IdentifierName(loopVar),
                        loopVarValue));
            }
            else
            {
                // Variable is new - declare and initialize it inside the loop body
                // This makes it a new variable scoped to the loop body, not the foreach iteration variable
                loopVarInit = LocalDeclarationStatement(
                    VariableDeclaration(IdentifierName("var"))
                        .WithVariables(SingletonSeparatedList(
                            VariableDeclarator(Identifier(loopVar))
                                .WithInitializer(EqualsValueClause(loopVarValue)))));
            }

            var newBodyStatements = new List<StatementSyntax> { loopVarInit };
            newBodyStatements.AddRange(body.Statements);
            var newBody = Block(newBodyStatements);

            var foreachStmt = ForEachStatement(
                IdentifierName("var"),
                Identifier(tempLoopVar),
                iterator,
                newBody);
            return isAsync
                ? foreachStmt.WithAwaitKeyword(Token(SyntaxKind.AwaitKeyword))
                : foreachStmt;
        }

        // Handle tuple unpacking in for loops: for x, y in items
        if (target is TupleLiteral tuple)
        {
            // Check if all elements are identifiers
            bool allIdentifiers = tuple.Elements.All(e => e is Identifier);

            if (allIdentifiers)
            {
                var identifiers = tuple.Elements.Cast<Identifier>().ToList();

                // Register all tuple element variables BEFORE generating body
                foreach (var id in identifiers)
                {
                    var name = NameMangler.ToCamelCase(id.Name);
                    _declaredVariables.Add(name);
                    _variableVersions[name] = 0;
                }

                // Now generate the body
                var body = Block(bodyStatements.SelectMany(GenerateBodyStatements));

                // Generate: foreach (var (x, y) in items)
                var variables = identifiers
                    .Select(id =>
                    {
                        var name = NameMangler.ToCamelCase(id.Name);
                        return SingleVariableDesignation(Identifier(name));
                    })
                    .ToList();

                var tuplePattern = ParenthesizedVariableDesignation(
                    SeparatedList<VariableDesignationSyntax>(variables));

                var declExpr = DeclarationExpression(
                    IdentifierName("var"),
                    tuplePattern);

                var foreachVarStmt = ForEachVariableStatement(
                    declExpr,
                    iterator,
                    body);
                return isAsync
                    ? foreachVarStmt.WithAwaitKeyword(Token(SyntaxKind.AwaitKeyword))
                    : foreachVarStmt;
            }

            // Complex tuple unpacking in for loop: for (a, b), c in items:
            // Generate: foreach (var __loopVar in items) { var __t0 = __loopVar.Item1; ... body }
            var tempLoopVar = GenerateTempVarName("loopVar");
            var unpackStatements = new List<StatementSyntax>();
            // Generate unpacking first — this declares variables (x, y, name)
            GenerateRecursiveTupleUnpacking(tuple.Elements, tempLoopVar, unpackStatements);

            // Now generate the body — variables are already declared so references resolve correctly
            var loopBody = Block(bodyStatements.SelectMany(GenerateBodyStatements));

            // Prepend unpacking to body
            var combinedStatements = new List<StatementSyntax>(unpackStatements);
            combinedStatements.AddRange(loopBody.Statements);

            var complexForeachStmt = ForEachStatement(
                IdentifierName("var"),
                Identifier(tempLoopVar),
                iterator,
                Block(combinedStatements));
            return isAsync
                ? complexForeachStmt.WithAwaitKeyword(Token(SyntaxKind.AwaitKeyword))
                : complexForeachStmt;
        }

        return EmitNotImplementedStatement(
            $"Unsupported expression type in code generation: for loop target type '{target.GetType().Name}'",
            DiagnosticCodes.CodeGen.UnsupportedExpressionType, target.LineStart, target.ColumnStart);
    }

    private StatementSyntax GenerateWith(WithStatement withStmt)
    {
        // Generate the body block
        var bodyStatements = withStmt.Body.SelectMany(GenerateBodyStatements).ToList();

        // Build using/try-finally statements from inside out (last item wraps the body,
        // first item wraps everything)
        StatementSyntax innermost = Block(bodyStatements);

        for (int i = withStmt.Items.Length - 1; i >= 0; i--)
        {
            var item = withStmt.Items[i];
            var cmKind = _context.SemanticInfo?.GetContextManagerKind(item.ContextExpression);

            if (cmKind is ContextManagerKind.DunderProtocol or ContextManagerKind.AsyncDunderProtocol)
            {
                innermost = GenerateWithDunderProtocol(item, innermost, cmKind.Value);
            }
            else
            {
                innermost = GenerateWithDisposable(item, innermost, withStmt.IsAsync);
            }
        }

        return innermost;
    }

    /// <summary>
    /// Generates a C# using statement for IDisposable/IAsyncDisposable context managers.
    /// </summary>
    private StatementSyntax GenerateWithDisposable(WithItem item, StatementSyntax innermost, bool isAsync)
    {
        var contextExpr = GenerateExpression(item.ContextExpression);

        if (item.Name != null)
        {
            // with expr as name: -> using (var name = expr) { ... }
            // async with expr as name: -> await using (var name = expr) { ... }
            var varName = GetMangledVariableName(item.Name, isNewDeclaration: true);
            _declaredVariables.Add(varName);

            var declaration = VariableDeclaration(IdentifierName("var"))
                .WithVariables(SingletonSeparatedList(
                    VariableDeclarator(Identifier(varName))
                        .WithInitializer(EqualsValueClause(contextExpr))));

            var usingStmt = UsingStatement(declaration, null, innermost is BlockSyntax block ? block : Block(innermost));
            return isAsync
                ? usingStmt.WithAwaitKeyword(Token(SyntaxKind.AwaitKeyword))
                : usingStmt;
        }
        else
        {
            // with expr: -> using (expr) { ... }
            // async with expr: -> await using (expr) { ... }
            var usingStmt = UsingStatement(null, contextExpr, innermost is BlockSyntax block ? block : Block(innermost));
            return isAsync
                ? usingStmt.WithAwaitKeyword(Token(SyntaxKind.AwaitKeyword))
                : usingStmt;
        }
    }

    /// <summary>
    /// Generates try/finally with explicit Enter()/Exit() calls for dunder-protocol context managers.
    /// Sync:  var __ctx_N = expr; var asVar = __ctx_N.Enter(); try { body } finally { __ctx_N.Exit(); }
    /// Async: var __ctx_N = expr; var asVar = await __ctx_N.AenterAsync(); try { body } finally { await __ctx_N.AexitAsync(); }
    /// </summary>
    private StatementSyntax GenerateWithDunderProtocol(WithItem item, StatementSyntax innermost, ContextManagerKind cmKind)
    {
        bool isAsync = cmKind == ContextManagerKind.AsyncDunderProtocol;
        var enterMethod = isAsync ? ProtocolConstants.AenterAsync : ProtocolConstants.Enter;
        var exitMethod = isAsync ? ProtocolConstants.AexitAsync : ProtocolConstants.Exit;

        var contextExpr = GenerateExpression(item.ContextExpression);
        var ctxVarName = GenerateTempVarName("ctx");
        var statements = new List<StatementSyntax>();

        // var __ctx_N = expr;
        statements.Add(LocalDeclarationStatement(
            VariableDeclaration(IdentifierName("var"))
                .WithVariables(SingletonSeparatedList(
                    VariableDeclarator(Identifier(ctxVarName))
                        .WithInitializer(EqualsValueClause(contextExpr))))));

        // Build the Enter() / AenterAsync() call
        ExpressionSyntax enterCall = InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName(ctxVarName),
                IdentifierName(enterMethod)));
        if (isAsync)
            enterCall = AwaitExpression(enterCall);

        if (item.Name != null)
        {
            // var asVar = __ctx_N.Enter();  (or await __ctx_N.AenterAsync())
            var varName = GetMangledVariableName(item.Name, isNewDeclaration: true);
            _declaredVariables.Add(varName);
            statements.Add(LocalDeclarationStatement(
                VariableDeclaration(IdentifierName("var"))
                    .WithVariables(SingletonSeparatedList(
                        VariableDeclarator(Identifier(varName))
                            .WithInitializer(EqualsValueClause(enterCall))))));
        }
        else
        {
            // Still call Enter() for side effects
            statements.Add(ExpressionStatement(enterCall));
        }

        // Build the Exit() / AexitAsync() call
        ExpressionSyntax exitCall = InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName(ctxVarName),
                IdentifierName(exitMethod)));
        if (isAsync)
            exitCall = AwaitExpression(exitCall);

        // try { body } finally { __ctx_N.Exit(); }
        var tryStmt = TryStatement(
            innermost is BlockSyntax blk ? blk : Block(innermost),
            List<CatchClauseSyntax>(),
            FinallyClause(Block(ExpressionStatement(exitCall))));
        statements.Add(tryStmt);

        return Block(statements);
    }

    private StatementSyntax GenerateTry(TryStatement tryStmt)
    {
        // If there's an else clause, we need to use a flag pattern:
        // bool __trySucceeded = false;
        // try { ... __trySucceeded = true; }
        // catch { ... }
        // finally { ... }
        // if (__trySucceeded) { else_body }
        if (tryStmt.ElseBody.Length > 0)
        {
            return GenerateTryWithElse(tryStmt);
        }

        var tryBlock = Block(tryStmt.Body.SelectMany(GenerateBodyStatements));
        var catchClauses = GenerateCatchClauses(tryStmt.Handlers);
        var finallyClause = GenerateFinallyClause(tryStmt.FinallyBody);

        return TryStatement(tryBlock, List(catchClauses), finallyClause);
    }

    private StatementSyntax GenerateTryWithElse(TryStatement tryStmt)
    {
        // Generate: bool __trySucceeded = false;
        var flagName = GenerateTempVarName("trySucceeded");
        var flagDecl = LocalDeclarationStatement(
            VariableDeclaration(PredefinedType(Token(SyntaxKind.BoolKeyword)))
                .WithVariables(SingletonSeparatedList(
                    VariableDeclarator(Identifier(flagName))
                        .WithInitializer(EqualsValueClause(LiteralExpression(SyntaxKind.FalseLiteralExpression))))));

        // Generate try body with flag set to true at the end.
        var tryBodyStatements = new List<StatementSyntax>();
        tryBodyStatements.AddRange(tryStmt.Body.SelectMany(GenerateBodyStatements));
        tryBodyStatements.Add(ExpressionStatement(
            AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                IdentifierName(flagName),
                LiteralExpression(SyntaxKind.TrueLiteralExpression))));
        var tryBlock = Block(tryBodyStatements);
        var catchClauses = GenerateCatchClauses(tryStmt.Handlers);
        var finallyClause = GenerateFinallyClause(tryStmt.FinallyBody);

        var tryCatchFinally = TryStatement(tryBlock, List(catchClauses), finallyClause);

        // Generate: if (__trySucceeded) { else_body }
        var elseBlock = Block(tryStmt.ElseBody.SelectMany(GenerateBodyStatements));
        var elseIf = IfStatement(IdentifierName(flagName), elseBlock);

        // Return a block containing all statements: flag + try + else-if
        var allStatements = new List<StatementSyntax>();
        allStatements.Add(flagDecl);
        allStatements.Add(tryCatchFinally);
        allStatements.Add(elseIf);

        // If the else body returns a value, add a dead-code throw to satisfy C#'s
        // reachability analysis. __trySucceeded is always true after a successful try,
        // so the if-body always executes and its return will be reached. C# can't prove
        // this statically, so the throw prevents CS0161 (not all code paths return).
        // Only added when the else body returns — void functions don't need this.
        if (ContainsReturnStatement(tryStmt.ElseBody))
        {
            allStatements.Add(ThrowStatement(
                ObjectCreationExpression(
                    ParseTypeName("System.InvalidOperationException"))
                    .WithArgumentList(ArgumentList(SingletonSeparatedList(
                        Argument(LiteralExpression(
                            SyntaxKind.StringLiteralExpression,
                            Literal("unreachable"))))))));
        }

        return Block(allStatements);
    }

    private List<CatchClauseSyntax> GenerateCatchClauses(ImmutableArray<ExceptHandler> handlers)
    {
        return handlers.Select(handler =>
        {
            var catchBlock = Block(handler.Body.SelectMany(GenerateBodyStatements));

            if (handler.ExceptionType != null)
            {
                var exceptionType = _typeMapper.MapType(handler.ExceptionType);

                if (handler.Name != null)
                {
                    var exceptionVar = NameMangler.ToCamelCase(handler.Name);
                    var declaration = CatchDeclaration(exceptionType, Identifier(exceptionVar));
                    return CatchClause(declaration, null, catchBlock);
                }
                else
                {
                    var declaration = CatchDeclaration(exceptionType);
                    return CatchClause(declaration, null, catchBlock);
                }
            }
            else
            {
                return CatchClause()
                    .WithBlock(catchBlock);
            }
        }).ToList();
    }

    private FinallyClauseSyntax? GenerateFinallyClause(ImmutableArray<Statement> finallyBody)
    {
        if (finallyBody.Length > 0)
        {
            var finallyBlock = Block(finallyBody.SelectMany(GenerateBodyStatements));
            return FinallyClause(finallyBlock);
        }
        return null;
    }

    /// <summary>
    /// Recursively checks if a statement list contains a return statement.
    /// Used to determine if a try/else block needs a dead-code throw for reachability.
    /// </summary>
    private static bool ContainsReturnStatement(ImmutableArray<Statement> statements)
    {
        foreach (var stmt in statements)
        {
            if (stmt is ReturnStatement)
                return true;

            if (stmt is IfStatement ifStmt)
            {
                if (ContainsReturnStatement(ifStmt.ThenBody) ||
                    ifStmt.ElifClauses.Any(e => ContainsReturnStatement(e.Body)) ||
                    ContainsReturnStatement(ifStmt.ElseBody))
                    return true;
            }
            else if (stmt is ForStatement forStmt)
            {
                if (ContainsReturnStatement(forStmt.Body) ||
                    ContainsReturnStatement(forStmt.ElseBody))
                    return true;
            }
            else if (stmt is WhileStatement whileStmt)
            {
                if (ContainsReturnStatement(whileStmt.Body) ||
                    ContainsReturnStatement(whileStmt.ElseBody))
                    return true;
            }
            else if (stmt is TryStatement tryStmt)
            {
                if (ContainsReturnStatement(tryStmt.Body) ||
                    tryStmt.Handlers.Any(h => ContainsReturnStatement(h.Body)) ||
                    ContainsReturnStatement(tryStmt.ElseBody) ||
                    ContainsReturnStatement(tryStmt.FinallyBody))
                    return true;
            }
            else if (stmt is MatchStatement matchStmt)
            {
                if (matchStmt.Cases.Any(c => ContainsReturnStatement(c.Body)))
                    return true;
            }
            else if (stmt is WithStatement withStmt)
            {
                if (ContainsReturnStatement(withStmt.Body))
                    return true;
            }
        }

        return false;
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

        // Determine element type for the Sharpy.List<T> wrapper
        TypeSyntax elementTypeSyntax = PredefinedType(Token(SyntaxKind.ObjectKeyword));
        if (valueType is GenericType { Name: BuiltinNames.List } listType && listType.TypeArguments.Count > 0)
        {
            elementTypeSyntax = _typeMapper.MapSemanticType(listType.TypeArguments[0]);
        }

        // Elements before star: var name = __t[i]
        for (int i = 0; i < numBefore; i++)
        {
            if (elements[i] is Identifier id)
            {
                var varName = GetMangledVariableName(id.Name, isNewDeclaration: true);
                _declaredVariables.Add(varName);
                statements.Add(LocalDeclarationStatement(
                    VariableDeclaration(IdentifierName("var"))
                        .WithVariables(SingletonSeparatedList(
                            VariableDeclarator(Identifier(varName))
                                .WithInitializer(EqualsValueClause(
                                    ElementAccessExpression(IdentifierName(sourceVar))
                                        .WithArgumentList(BracketedArgumentList(SingletonSeparatedList(
                                            Argument(LiteralExpression(
                                                SyntaxKind.NumericLiteralExpression,
                                                Literal(i))))))))))));
            }
        }

        // Star element: var rest = __t.GetSlice(new Sharpy.Slice(start, end))
        if (elements[starIndex] is StarExpression starExpr && starExpr.Operand is Identifier starId)
        {
            var starVarName = GetMangledVariableName(starId.Name, isNewDeclaration: true);
            _declaredVariables.Add(starVarName);

            // Build Slice constructor args: new Sharpy.Slice((int?)start, (int?)end)
            var startArg = numBefore > 0
                ? (ExpressionSyntax)CastExpression(
                    NullableType(PredefinedType(Token(SyntaxKind.IntKeyword))),
                    LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(numBefore)))
                : LiteralExpression(SyntaxKind.NullLiteralExpression);

            var endArg = numAfter > 0
                ? (ExpressionSyntax)CastExpression(
                    NullableType(PredefinedType(Token(SyntaxKind.IntKeyword))),
                    PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression,
                        LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(numAfter))))
                : LiteralExpression(SyntaxKind.NullLiteralExpression);

            // __t.GetSlice(new global::Sharpy.Slice(start, end))
            var newSlice = ObjectCreationExpression(MakeGlobalQualifiedName("Sharpy", "Slice"))
                .WithArgumentList(ArgumentList(SeparatedList(new[]
                {
                    Argument(startArg),
                    Argument(endArg)
                })));

            var sliceCall = InvocationExpression(
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName(sourceVar),
                    IdentifierName("GetSlice")))
                .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(newSlice))));

            statements.Add(LocalDeclarationStatement(
                VariableDeclaration(IdentifierName("var"))
                    .WithVariables(SingletonSeparatedList(
                        VariableDeclarator(Identifier(starVarName))
                            .WithInitializer(EqualsValueClause(sliceCall))))));
        }

        // Elements after star: var name = __t[-n]
        for (int i = 0; i < numAfter; i++)
        {
            int elemIndex = starIndex + 1 + i;
            int negIndex = numAfter - i; // distance from end: numAfter, ..., 1

            if (elements[elemIndex] is Identifier id)
            {
                var varName = GetMangledVariableName(id.Name, isNewDeclaration: true);
                _declaredVariables.Add(varName);
                statements.Add(LocalDeclarationStatement(
                    VariableDeclaration(IdentifierName("var"))
                        .WithVariables(SingletonSeparatedList(
                            VariableDeclarator(Identifier(varName))
                                .WithInitializer(EqualsValueClause(
                                    ElementAccessExpression(IdentifierName(sourceVar))
                                        .WithArgumentList(BracketedArgumentList(SingletonSeparatedList(
                                            Argument(PrefixUnaryExpression(
                                                SyntaxKind.UnaryMinusExpression,
                                                LiteralExpression(
                                                    SyntaxKind.NumericLiteralExpression,
                                                    Literal(negIndex)))))))))))));
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
                var varName = GetMangledVariableName(id.Name, isNewDeclaration: true);
                _declaredVariables.Add(varName);
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
                GenerateRecursiveTupleUnpacking(nestedTuple.Elements, tempVarName, statements);
            }
        }
    }


}
