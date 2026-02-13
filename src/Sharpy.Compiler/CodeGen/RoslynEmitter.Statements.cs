using System.Collections.Immutable;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// RoslynEmitter partial class: Statement generation (control flow, assignments, try/catch)
/// </summary>
internal partial class RoslynEmitter
{
    private StatementSyntax? GenerateBodyStatement(Statement stmt)
    {
        _cancellationToken.ThrowIfCancellationRequested();

        var result = stmt switch
        {
            ReturnStatement ret => GenerateReturn(ret),
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
            _ => null
        };

        if (result == null && stmt is not ImportStatement and not FromImportStatement and not TypeAlias)
        {
            _context.AddError(
                $"Internal: unrecognized statement type '{stmt.GetType().Name}' was not emitted. This is a compiler bug — please report it.",
                DiagnosticCodes.CodeGen.UnrecognizedStatementType,
                stmt.LineStart,
                stmt.ColumnStart);
        }

        return result != null ? AttachLineDirective(result, stmt) : null;
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

            // Await expressions are valid (if we had them in AST)
            // AwaitExpression => true,

            // All other expressions need a discard
            _ => false
        };
    }

    private ReturnStatementSyntax GenerateReturn(ReturnStatement ret)
    {
        if (ret.Value != null)
        {
            return ReturnStatement(GenerateExpression(ret.Value));
        }
        return ReturnStatement();
    }

    private StatementSyntax GenerateAssignment(Assignment assign)
    {
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
                    var declaration = VariableDeclaration(IdentifierName("var"))
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

                // For the read side of augmented assignment, apply Optional narrowing
                // so that x += 1 with narrowed Optional<int> reads as x.Unwrap() + 1
                ExpressionSyntax readExpr = IsNarrowed(name.Name)
                    ? InvocationExpression(
                        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName(varName), IdentifierName("Unwrap")))
                        .WithArgumentList(ArgumentList())
                    : target;

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

            var assignmentValue = assign.Operator == AssignmentOperator.Assign
                ? value
                : GenerateAugmentedValue(assign.Operator, elementAccess, value, assign.Target, assign.Value);

            return ExpressionStatement(
                AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    elementAccess,
                    assignmentValue));
        }

        // Handle member assignment: obj.field = value
        if (assign.Target is MemberAccess memberAccess)
        {
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

            return EmitNotImplementedStatement(
                "Complex tuple unpacking (non-identifier targets) is not yet supported. Use intermediate variables to unpack in multiple steps.",
                DiagnosticCodes.CodeGen.ComplexTupleUnpacking, assign.LineStart, assign.ColumnStart);
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
            typeSyntax = IdentifierName("var");
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

        var condition = GenerateExpression(ifStmt.Test);

        // Generate then-block with narrowing if applicable
        BlockSyntax thenBlock;
        if (narrowingInfo.HasValue && narrowingInfo.Value.NarrowInThen)
        {
            foreach (var name in narrowingInfo.Value.VariableNames)
                PushNarrowing(name);
            thenBlock = Block(ifStmt.ThenBody.Select(GenerateBodyStatement).OfType<StatementSyntax>());
            foreach (var name in narrowingInfo.Value.VariableNames)
                PopNarrowing(name);
        }
        else
        {
            thenBlock = Block(ifStmt.ThenBody.Select(GenerateBodyStatement).OfType<StatementSyntax>());
        }

        ElseClauseSyntax? elseClause = null;

        // Process elif clauses from last to first to build nested if-else structure
        if (ifStmt.ElifClauses.Length > 0 || ifStmt.ElseBody.Length > 0)
        {
            StatementSyntax? currentElse = null;

            // Start with the final else block if it exists
            if (ifStmt.ElseBody.Length > 0)
            {
                // Generate else-block with narrowing if applicable (is None → narrow in else)
                if (narrowingInfo.HasValue && !narrowingInfo.Value.NarrowInThen)
                {
                    foreach (var name in narrowingInfo.Value.VariableNames)
                        PushNarrowing(name);
                    currentElse = Block(ifStmt.ElseBody.Select(GenerateBodyStatement).OfType<StatementSyntax>());
                    foreach (var name in narrowingInfo.Value.VariableNames)
                        PopNarrowing(name);
                }
                else
                {
                    currentElse = Block(ifStmt.ElseBody.Select(GenerateBodyStatement).OfType<StatementSyntax>());
                }
            }

            // Process elif clauses in reverse order
            for (int i = ifStmt.ElifClauses.Length - 1; i >= 0; i--)
            {
                var elif = ifStmt.ElifClauses[i];
                var elifCondition = GenerateExpression(elif.Test);

                // Detect and apply narrowing for this elif's condition
                var elifNarrowing = DetectOptionalNarrowings(elif.Test);
                BlockSyntax elifBody;
                if (elifNarrowing.HasValue && elifNarrowing.Value.NarrowInThen)
                {
                    foreach (var name in elifNarrowing.Value.VariableNames)
                        PushNarrowing(name);
                    elifBody = Block(elif.Body.Select(GenerateBodyStatement).OfType<StatementSyntax>());
                    foreach (var name in elifNarrowing.Value.VariableNames)
                        PopNarrowing(name);
                }
                else
                {
                    elifBody = Block(elif.Body.Select(GenerateBodyStatement).OfType<StatementSyntax>());
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
        if (binOp.Left is not Identifier id)
            return;
        if (GetExpressionSemanticType(binOp.Left) is not OptionalType)
            return;

        if (binOp.Operator == BinaryOperator.IsNot)
            isNotNone.Add(id.Name);
        else if (binOp.Operator == BinaryOperator.Is)
            isNone.Add(id.Name);
    }

    private StatementSyntax GenerateWhile(WhileStatement whileStmt)
    {
        // Detect Optional narrowing in while condition:
        // while x is not None: → x is narrowed to T in the loop body
        var narrowingInfo = DetectOptionalNarrowings(whileStmt.Test);

        var condition = GenerateExpression(whileStmt.Test);

        // If there's no else clause, generate simple while loop
        if (whileStmt.ElseBody.IsEmpty)
        {
            if (narrowingInfo.HasValue && narrowingInfo.Value.NarrowInThen)
            {
                foreach (var name in narrowingInfo.Value.VariableNames)
                    PushNarrowing(name);
                var body = Block(whileStmt.Body.Select(GenerateBodyStatement).OfType<StatementSyntax>());
                foreach (var name in narrowingInfo.Value.VariableNames)
                    PopNarrowing(name);
                return WhileStatement(condition, body);
            }
            var simpleBody = Block(whileStmt.Body.Select(GenerateBodyStatement).OfType<StatementSyntax>());
            return WhileStatement(condition, simpleBody);
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
        if (narrowingInfo.HasValue && narrowingInfo.Value.NarrowInThen)
        {
            foreach (var name in narrowingInfo.Value.VariableNames)
                PushNarrowing(name);
            bodyBlock = Block(transformedBody.Select(GenerateBodyStatement).OfType<StatementSyntax>());
            foreach (var name in narrowingInfo.Value.VariableNames)
                PopNarrowing(name);
        }
        else
        {
            bodyBlock = Block(transformedBody.Select(GenerateBodyStatement).OfType<StatementSyntax>());
        }

        // while (condition) { transformedBody }
        statements.Add(WhileStatement(condition, bodyBlock));

        // if (_loopCompleted) { elseBody }
        var elseBodyBlock = Block(whileStmt.ElseBody.Select(GenerateBodyStatement).OfType<StatementSyntax>());
        statements.Add(IfStatement(IdentifierName(flagName), elseBodyBlock));

        return Block(statements);
    }

    private StatementSyntax GenerateFor(ForStatement forStmt)
    {
        // For-in loop: for item in items: → foreach (var item in items)
        var iterator = GenerateExpression(forStmt.Iterator);

        // If there's no else clause, generate simple foreach loop
        if (forStmt.ElseBody.IsEmpty)
        {
            return GenerateForEachCore(forStmt.Target, iterator, forStmt.Body);
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
        statements.Add(GenerateForEachCore(forStmt.Target, iterator, transformedBody));

        // if (_loopCompleted) { elseBody }
        var elseBodyBlock = Block(forStmt.ElseBody.Select(GenerateBodyStatement).OfType<StatementSyntax>());
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
    private StatementSyntax GenerateForEachCore(Expression target, ExpressionSyntax iterator, IReadOnlyList<Statement> bodyStatements)
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
            var body = Block(bodyStatements.Select(GenerateBodyStatement).OfType<StatementSyntax>());

            // Create the assignment or declaration at the start of the body
            StatementSyntax loopVarInit;
            if (varExistsInOuterScope)
            {
                // Variable exists in outer scope - just assign to it
                loopVarInit = ExpressionStatement(
                    AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        IdentifierName(loopVar),
                        IdentifierName(tempLoopVar)));
            }
            else
            {
                // Variable is new - declare and initialize it inside the loop body
                // This makes it a new variable scoped to the loop body, not the foreach iteration variable
                loopVarInit = LocalDeclarationStatement(
                    VariableDeclaration(IdentifierName("var"))
                        .WithVariables(SingletonSeparatedList(
                            VariableDeclarator(Identifier(loopVar))
                                .WithInitializer(EqualsValueClause(IdentifierName(tempLoopVar))))));
            }

            var newBodyStatements = new List<StatementSyntax> { loopVarInit };
            newBodyStatements.AddRange(body.Statements);
            var newBody = Block(newBodyStatements);

            return ForEachStatement(
                IdentifierName("var"),
                Identifier(tempLoopVar),
                iterator,
                newBody);
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
                var body = Block(bodyStatements.Select(GenerateBodyStatement).OfType<StatementSyntax>());

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

                return ForEachVariableStatement(
                    declExpr,
                    iterator,
                    body);
            }

            return EmitNotImplementedStatement(
                "Complex tuple unpacking (non-identifier targets) is not yet supported. Use intermediate variables to unpack in multiple steps.",
                DiagnosticCodes.CodeGen.ComplexTupleUnpacking, target.LineStart, target.ColumnStart);
        }

        return EmitNotImplementedStatement(
            $"Unsupported expression type in code generation: for loop target type '{target.GetType().Name}'",
            DiagnosticCodes.CodeGen.UnsupportedExpressionType, target.LineStart, target.ColumnStart);
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

        var tryBlock = Block(tryStmt.Body.Select(GenerateBodyStatement).OfType<StatementSyntax>());

        // Generate catch clauses
        var catchClauses = tryStmt.Handlers.Select(handler =>
        {
            var catchBlock = Block(handler.Body.Select(GenerateBodyStatement).OfType<StatementSyntax>());

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
                // Catch all exceptions
                return CatchClause()
                    .WithBlock(catchBlock);
            }
        }).ToList();

        // Generate finally block if present
        FinallyClauseSyntax? finallyClause = null;
        if (tryStmt.FinallyBody.Length > 0)
        {
            var finallyBlock = Block(tryStmt.FinallyBody.Select(GenerateBodyStatement).OfType<StatementSyntax>());
            finallyClause = FinallyClause(finallyBlock);
        }

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

        // Generate try body with flag set to true at the end
        var tryBodyStatements = tryStmt.Body.Select(GenerateBodyStatement).OfType<StatementSyntax>().ToList();
        tryBodyStatements.Add(ExpressionStatement(
            AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                IdentifierName(flagName),
                LiteralExpression(SyntaxKind.TrueLiteralExpression))));
        var tryBlock = Block(tryBodyStatements);

        // Generate catch clauses
        var catchClauses = tryStmt.Handlers.Select(handler =>
        {
            var catchBlock = Block(handler.Body.Select(GenerateBodyStatement).OfType<StatementSyntax>());

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
                // Catch all exceptions
                return CatchClause()
                    .WithBlock(catchBlock);
            }
        }).ToList();

        // Generate finally block if present
        FinallyClauseSyntax? finallyClause = null;
        if (tryStmt.FinallyBody.Length > 0)
        {
            var finallyBlock = Block(tryStmt.FinallyBody.Select(GenerateBodyStatement).OfType<StatementSyntax>());
            finallyClause = FinallyClause(finallyBlock);
        }

        var tryCatchFinally = TryStatement(tryBlock, List(catchClauses), finallyClause);

        // Generate: if (__trySucceeded) { else_body }
        var elseBlock = Block(tryStmt.ElseBody.Select(GenerateBodyStatement).OfType<StatementSyntax>());
        var elseIf = IfStatement(IdentifierName(flagName), elseBlock);

        // Return a block containing all statements
        return Block(flagDecl, tryCatchFinally, elseIf);
    }

}
