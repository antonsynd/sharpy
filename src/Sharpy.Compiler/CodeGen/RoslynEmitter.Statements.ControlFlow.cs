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
/// RoslynEmitter partial class: Control flow statements (if, while, for, try, with, assert, raise)
/// </summary>
internal partial class RoslynEmitter
{
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

    private StatementSyntax GenerateAssert(AssertStatement assert)
    {
        // assert condition, message → Debug.Assert(condition, message)
        var condition = WrapTruthinessIfNeeded(GenerateExpression(assert.Test), assert.Test);

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
        // Read narrowing decisions from SemanticInfo (computed by TypeChecker)
        var narrowingInfo = GetOptionalNarrowingsFromDecision(ifStmt.Test, narrowInThen: true);
        var isInstanceNarrowingInfo = GetIsInstanceNarrowingsFromDecision(ifStmt.Test, narrowInThen: true);

        var condition = WrapTruthinessIfNeeded(GenerateExpression(ifStmt.Test), ifStmt.Test);

        // Save scope before the if statement so each branch (then/elif/else)
        // gets an independent copy. This prevents variable declarations in one
        // branch from leaking into sibling branches (fixes #363).
        var preIfScope = SaveScope();

        // Generate then-block with narrowing if applicable
        BlockSyntax thenBlock;

        // Push isinstance narrowings for the then-body only when NarrowInThen is true
        if (isInstanceNarrowingInfo.HasValue && isInstanceNarrowingInfo.Value.NarrowInThen)
        {
            foreach (var (varName, typeName) in isInstanceNarrowingInfo.Value.Narrowings)
                _narrowing.PushIsInstanceNarrowing(varName, typeName);
        }

        if (narrowingInfo.HasValue && narrowingInfo.Value.NarrowInThen)
        {
            foreach (var name in narrowingInfo.Value.VariableNames)
                _narrowing.PushNarrowing(name);
            thenBlock = Block(ifStmt.ThenBody.SelectMany(GenerateBodyStatements));
            foreach (var name in narrowingInfo.Value.VariableNames)
                _narrowing.PopNarrowing(name);
        }
        else
        {
            thenBlock = Block(ifStmt.ThenBody.SelectMany(GenerateBodyStatements));
        }

        // Pop isinstance narrowings after then-body
        if (isInstanceNarrowingInfo.HasValue && isInstanceNarrowingInfo.Value.NarrowInThen)
        {
            foreach (var (varName, _) in isInstanceNarrowingInfo.Value.Narrowings)
                _narrowing.PopIsInstanceNarrowing(varName);
        }

        // Save scope after then-block so we can restore it after all branches.
        // Post-if code needs to see then-block's variable declarations for correct
        // C# redeclaration handling (e.g., versioning same-named variables).
        //
        // DESIGN NOTE (#363): This is deliberately asymmetric — only the then-branch's
        // scope is preserved for post-if code. Variable declarations in elif/else
        // branches are discarded. This is correct for C# variable versioning because
        // the emitter needs a single consistent variable version after the if-statement.
        // The then-branch is chosen as the "winner" because it is the first branch
        // encountered and thus the most natural continuation of the variable version
        // sequence. SaveScope()/RestoreScope() snapshot and restore the _variableVersions
        // and _scopeVariables dictionaries, enabling branch-isolated code generation.
        var postThenScope = SaveScope();

        ElseClauseSyntax? elseClause = null;

        // Process elif clauses from last to first to build nested if-else structure
        if (ifStmt.ElifClauses.Length > 0 || ifStmt.ElseBody.Length > 0)
        {
            StatementSyntax? currentElse = null;

            // Start with the final else block if it exists
            if (ifStmt.ElseBody.Length > 0)
            {
                // Restore to pre-if scope so else doesn't see then-block variables (#363)
                RestoreScope(preIfScope);

                // Read else-branch narrowings from SemanticInfo
                var elseIsInstanceNarrowingInfo = GetIsInstanceNarrowingsFromDecision(ifStmt.Test, narrowInThen: false);
                var elseNarrowingInfo = GetOptionalNarrowingsFromDecision(ifStmt.Test, narrowInThen: false);

                // Push isinstance narrowings for else-body
                if (elseIsInstanceNarrowingInfo.HasValue)
                {
                    foreach (var (varName, typeName) in elseIsInstanceNarrowingInfo.Value.Narrowings)
                        _narrowing.PushIsInstanceNarrowing(varName, typeName);
                }

                // Generate else-block with narrowing if applicable (is None → narrow in else)
                if (elseNarrowingInfo.HasValue)
                {
                    foreach (var name in elseNarrowingInfo.Value.VariableNames)
                        _narrowing.PushNarrowing(name);
                    currentElse = Block(ifStmt.ElseBody.SelectMany(GenerateBodyStatements));
                    foreach (var name in elseNarrowingInfo.Value.VariableNames)
                        _narrowing.PopNarrowing(name);
                }
                else
                {
                    currentElse = Block(ifStmt.ElseBody.SelectMany(GenerateBodyStatements));
                }

                // Pop isinstance narrowings after else-body
                if (elseIsInstanceNarrowingInfo.HasValue)
                {
                    foreach (var (varName, _) in elseIsInstanceNarrowingInfo.Value.Narrowings)
                        _narrowing.PopIsInstanceNarrowing(varName);
                }

            }

            // Process elif clauses in reverse order
            for (int i = ifStmt.ElifClauses.Length - 1; i >= 0; i--)
            {
                // Restore to pre-if scope so elif doesn't see then-block or other elif variables (#363)
                RestoreScope(preIfScope);

                var elif = ifStmt.ElifClauses[i];
                var elifCondition = WrapTruthinessIfNeeded(GenerateExpression(elif.Test), elif.Test);

                // Read narrowing decisions for this elif's condition from SemanticInfo
                var elifNarrowing = GetOptionalNarrowingsFromDecision(elif.Test, narrowInThen: true);
                var elifIsInstanceNarrowingInfo = GetIsInstanceNarrowingsFromDecision(elif.Test, narrowInThen: true);

                // Push isinstance narrowings for elif body (only when NarrowInThen is true)
                if (elifIsInstanceNarrowingInfo.HasValue && elifIsInstanceNarrowingInfo.Value.NarrowInThen)
                {
                    foreach (var (varName, typeName) in elifIsInstanceNarrowingInfo.Value.Narrowings)
                        _narrowing.PushIsInstanceNarrowing(varName, typeName);
                }

                BlockSyntax elifBody;
                if (elifNarrowing.HasValue && elifNarrowing.Value.NarrowInThen)
                {
                    foreach (var name in elifNarrowing.Value.VariableNames)
                        _narrowing.PushNarrowing(name);
                    elifBody = Block(elif.Body.SelectMany(GenerateBodyStatements));
                    foreach (var name in elifNarrowing.Value.VariableNames)
                        _narrowing.PopNarrowing(name);
                }
                else
                {
                    elifBody = Block(elif.Body.SelectMany(GenerateBodyStatements));
                }

                // Pop isinstance narrowings after elif body
                if (elifIsInstanceNarrowingInfo.HasValue && elifIsInstanceNarrowingInfo.Value.NarrowInThen)
                {
                    foreach (var (varName, _) in elifIsInstanceNarrowingInfo.Value.Narrowings)
                        _narrowing.PopIsInstanceNarrowing(varName);
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

        // Restore to post-then scope so code after the if sees then-block's
        // variable declarations for correct C# redeclaration handling.
        // See DESIGN NOTE at postThenScope declaration above for why only the
        // then-branch scope is restored (deliberate asymmetry for variable versioning).
        RestoreScope(postThenScope);

        return IfStatement(condition, thenBlock, elseClause);
    }

    /// <summary>
    /// Reads Optional narrowing decisions from SemanticInfo for a given condition expression.
    /// Filters entries by <paramref name="narrowInThen"/> to get either then-branch or else-branch narrowings.
    /// Also registers value-type nullable narrowings via <see cref="NarrowingState.AddNullableNarrowing"/>
    /// so the emitter uses <c>.Value</c> instead of <c>.Unwrap()</c>.
    /// </summary>
    private (IReadOnlyList<string> VariableNames, bool NarrowInThen)? GetOptionalNarrowingsFromDecision(
        Expression test, bool narrowInThen)
    {
        var decision = _context.SemanticInfo?.GetNarrowingDecision(test);
        if (decision == null || decision.OptionalNarrowings.Count == 0)
            return null;

        var varNames = new List<string>();
        foreach (var n in decision.OptionalNarrowings)
        {
            if (n.NarrowInThenBranch == narrowInThen)
            {
                varNames.Add(n.VariableName);
                // Preserve AddNullableNarrowing side effect for value-type nullables
                if (n.IsValueTypeNullable)
                    _narrowing.AddNullableNarrowing(n.VariableName);
            }
        }

        return varNames.Count > 0 ? (varNames, narrowInThen) : null;
    }

    /// <summary>
    /// Reads isinstance narrowing decisions from SemanticInfo for a given condition expression.
    /// Filters entries by <paramref name="narrowInThen"/> to get either then-branch or else-branch narrowings.
    /// Maps <see cref="SemanticType"/> to C# type names via <see cref="TypeSyntaxMapper"/>.
    /// </summary>
    private (IReadOnlyList<(string VariableName, string CSharpTypeName)> Narrowings, bool NarrowInThen)?
        GetIsInstanceNarrowingsFromDecision(Expression test, bool narrowInThen)
    {
        var decision = _context.SemanticInfo?.GetNarrowingDecision(test);
        if (decision == null || decision.IsInstanceNarrowings.Count == 0)
            return null;

        var narrowings = new List<(string, string)>();
        foreach (var n in decision.IsInstanceNarrowings)
        {
            if (n.NarrowInThenBranch == narrowInThen)
            {
                var csharpType = _typeMapper.MapSemanticType(n.NarrowedType)
                    .NormalizeWhitespace().ToFullString();
                narrowings.Add((n.VariableName, csharpType));
            }
        }

        return narrowings.Count > 0 ? (narrowings, narrowInThen) : null;
    }

    private StatementSyntax GenerateWhile(WhileStatement whileStmt)
    {
        // Read narrowing decisions from SemanticInfo (computed by TypeChecker)
        var narrowingInfo = GetOptionalNarrowingsFromDecision(whileStmt.Test, narrowInThen: true);
        var isInstanceNarrowingInfo = GetIsInstanceNarrowingsFromDecision(whileStmt.Test, narrowInThen: true);

        // For walrus operators in while conditions, use inline assignment mode so the
        // expression is re-evaluated each iteration instead of being hoisted once.
        var hasWalrus = AstHelper.ContainsWalrusExpression(whileStmt.Test);
        if (hasWalrus)
        {
            _walrusInlineMode = true;
            _walrusPreDeclarations.Clear();
        }

        var condition = WrapTruthinessIfNeeded(GenerateExpression(whileStmt.Test), whileStmt.Test);

        if (hasWalrus)
            _walrusInlineMode = false;

        // If there's no else clause, generate simple while loop
        if (whileStmt.ElseBody.IsEmpty)
        {
            if (isInstanceNarrowingInfo.HasValue && isInstanceNarrowingInfo.Value.NarrowInThen)
            {
                foreach (var (varName, typeName) in isInstanceNarrowingInfo.Value.Narrowings)
                    _narrowing.PushIsInstanceNarrowing(varName, typeName);
            }

            if (narrowingInfo.HasValue && narrowingInfo.Value.NarrowInThen)
            {
                foreach (var name in narrowingInfo.Value.VariableNames)
                    _narrowing.PushNarrowing(name);
                var body = Block(whileStmt.Body.SelectMany(GenerateBodyStatements));
                foreach (var name in narrowingInfo.Value.VariableNames)
                    _narrowing.PopNarrowing(name);

                if (isInstanceNarrowingInfo.HasValue && isInstanceNarrowingInfo.Value.NarrowInThen)
                {
                    foreach (var (varName, _) in isInstanceNarrowingInfo.Value.Narrowings)
                        _narrowing.PopIsInstanceNarrowing(varName);
                }
                return WrapWithWalrusPreDeclarations(WhileStatement(condition, body));
            }
            var simpleBody = Block(whileStmt.Body.SelectMany(GenerateBodyStatements));

            if (isInstanceNarrowingInfo.HasValue && isInstanceNarrowingInfo.Value.NarrowInThen)
            {
                foreach (var (varName, _) in isInstanceNarrowingInfo.Value.Narrowings)
                    _narrowing.PopIsInstanceNarrowing(varName);
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
                _narrowing.PushIsInstanceNarrowing(varName, typeName);
        }

        if (narrowingInfo.HasValue && narrowingInfo.Value.NarrowInThen)
        {
            foreach (var name in narrowingInfo.Value.VariableNames)
                _narrowing.PushNarrowing(name);
            bodyBlock = Block(transformedBody.SelectMany(GenerateBodyStatements));
            foreach (var name in narrowingInfo.Value.VariableNames)
                _narrowing.PopNarrowing(name);
        }
        else
        {
            bodyBlock = Block(transformedBody.SelectMany(GenerateBodyStatements));
        }

        if (isInstanceNarrowingInfo.HasValue && isInstanceNarrowingInfo.Value.NarrowInThen)
        {
            foreach (var (varName, _) in isInstanceNarrowingInfo.Value.Narrowings)
                _narrowing.PopIsInstanceNarrowing(varName);
        }

        // while (condition) { transformedBody }
        statements.Add(WhileStatement(condition, bodyBlock));

        // if (_loopCompleted) { elseBody }
        var elseBodyBlock = Block(whileStmt.ElseBody.SelectMany(GenerateBodyStatements));
        statements.Add(IfStatement(IdentifierName(flagName), elseBodyBlock));

        return WrapWithWalrusPreDeclarations(Block(statements));
    }

    private StatementSyntax GenerateFor(ForStatement forStmt)
    {
        // For-in loop: for item in items: → foreach (var item in items)
        var iteratorType = GetExpressionSemanticType(forStmt.Iterator);
        var iterator = GenerateExpression(forStmt.Iterator);

        // String iteration: `for c in s:` → `foreach (var c in StringHelpers.Iterate(s))`
        // Yields string elements (single-character strings), not char.
        // Skip for variadic parameters (*args: str) — those are string[] at C# level,
        // and iterating over string[] already yields string elements.
        if (iteratorType == SemanticType.Str && !IsVariadicParameterReference(forStmt.Iterator))
        {
            iterator = InvocationExpression(
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    MakeGlobalQualifiedName("Sharpy", "StringHelpers"),
                    IdentifierName("Iterate")))
                .AddArgumentListArguments(Argument(iterator));
        }

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

            ExpressionSyntax loopVarValue = IdentifierName(tempLoopVar);

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

                // Register all tuple element variables BEFORE generating body.
                // For-loop variables are always new declarations in the loop scope —
                // no need to check _variableVersions existence unlike the assignment
                // tuple unpacking path which must distinguish new vs existing variables.
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
        if (StatementWalker.Any(tryStmt.ElseBody, s => s is ReturnStatement))
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
        // Check if any handlers are except* (PEP 654)
        if (handlers.Length > 0 && handlers[0].IsExceptStar)
        {
            return GenerateExceptStarCatchClauses(handlers);
        }

        var result = new List<CatchClauseSyntax>();

        foreach (var handler in handlers)
        {
            var filterClause = GenerateCatchFilterClause(handler);

            if (handler.ExceptionType != null)
            {
                // Tuple exception type: except (T1, T2): or except T1, T2:
                // Expand into one catch clause per type (no 'as' binding allowed without parens per PEP 758).
                if (handler.ExceptionType.Name == BuiltinNames.Tuple
                    && handler.ExceptionType.TypeArguments.Length > 0
                    && handler.Name == null)
                {
                    foreach (var typeArg in handler.ExceptionType.TypeArguments)
                    {
                        var catchBlock = Block(handler.Body.SelectMany(GenerateBodyStatements));
                        var declaration = CatchDeclaration(_typeMapper.MapType(typeArg));
                        result.Add(CatchClause(declaration, filterClause, catchBlock));
                    }
                    continue;
                }

                var exceptionType = _typeMapper.MapType(handler.ExceptionType);

                if (handler.Name != null)
                {
                    var baseName = NameMangler.ToCamelCase(handler.Name);

                    // Track exception variable in _variableVersions so nested
                    // catch clauses with the same name get versioned (e, e_1, ...)
                    // to avoid CS0136 in generated C#.
                    var hadPrevious = _variableVersions.TryGetValue(baseName, out var previousVersion);
                    if (hadPrevious)
                    {
                        var newVersion = previousVersion + 1;
                        _variableVersions[baseName] = newVersion;
                    }
                    else
                    {
                        _variableVersions[baseName] = 0;
                    }

                    var exceptionVar = hadPrevious
                        ? $"{baseName}_{_variableVersions[baseName]}"
                        : baseName;

                    var catchBlock = Block(handler.Body.SelectMany(GenerateBodyStatements));
                    var declaration = CatchDeclaration(exceptionType, Identifier(exceptionVar));

                    // Restore previous version state after generating the catch body
                    if (hadPrevious)
                    {
                        _variableVersions[baseName] = previousVersion;
                    }
                    else
                    {
                        _variableVersions.Remove(baseName);
                    }

                    result.Add(CatchClause(declaration, filterClause, catchBlock));
                }
                else
                {
                    var catchBlock = Block(handler.Body.SelectMany(GenerateBodyStatements));
                    var declaration = CatchDeclaration(exceptionType);
                    result.Add(CatchClause(declaration, filterClause, catchBlock));
                }
            }
            else
            {
                // Bare except — with filter we still need a declaration to attach the filter to
                var catchBlock = Block(handler.Body.SelectMany(GenerateBodyStatements));
                if (filterClause != null)
                {
                    var declaration = CatchDeclaration(IdentifierName("Exception"));
                    result.Add(CatchClause(declaration, filterClause, catchBlock));
                }
                else
                {
                    result.Add(CatchClause().WithBlock(catchBlock));
                }
            }
        }

        return result;
    }

    private CatchFilterClauseSyntax? GenerateCatchFilterClause(ExceptHandler handler)
    {
        if (handler.Filter == null)
            return null;

        var filterExpr = GenerateExpression(handler.Filter);
        return CatchFilterClause(filterExpr);
    }

    /// <summary>
    /// Generate catch clauses for except* handlers (PEP 654).
    /// All except* handlers are combined into a single catch(AggregateException) block
    /// that filters inner exceptions by type, dispatches to matching handler bodies,
    /// and re-throws unmatched exceptions.
    /// </summary>
    private List<CatchClauseSyntax> GenerateExceptStarCatchClauses(ImmutableArray<ExceptHandler> handlers)
    {
        var result = new List<CatchClauseSyntax>();

        var egVar = GenerateTempVarName("eg");
        var allMatchedVar = GenerateTempVarName("allMatched");

        var catchBodyStatements = new List<StatementSyntax>();

        // var __allMatched_N = new System.Collections.Generic.List<System.Exception>();
        catchBodyStatements.Add(LocalDeclarationStatement(
            VariableDeclaration(
                GenericName(Identifier("System.Collections.Generic.List"))
                    .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList<TypeSyntax>(
                        ParseTypeName("System.Exception")))))
            .WithVariables(SingletonSeparatedList(
                VariableDeclarator(Identifier(allMatchedVar))
                    .WithInitializer(EqualsValueClause(
                        ObjectCreationExpression(
                            GenericName(Identifier("System.Collections.Generic.List"))
                                .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList<TypeSyntax>(
                                    ParseTypeName("System.Exception")))))
                        .WithArgumentList(ArgumentList())))))));

        foreach (var handler in handlers)
        {
            if (handler.ExceptionType == null)
                continue;

            var exceptionType = _typeMapper.MapType(handler.ExceptionType);
            var matchedVar = GenerateTempVarName("matched");

            // var __matched_N = __eg_N.InnerExceptions.OfType<ExType>().ToList();
            var ofTypeCall = InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName(egVar),
                        IdentifierName("InnerExceptions")),
                    GenericName(Identifier("OfType"))
                        .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(exceptionType)))));

            var toListCall = InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    ofTypeCall,
                    IdentifierName("ToList")));

            catchBodyStatements.Add(LocalDeclarationStatement(
                VariableDeclaration(IdentifierName("var"))
                    .WithVariables(SingletonSeparatedList(
                        VariableDeclarator(Identifier(matchedVar))
                            .WithInitializer(EqualsValueClause(toListCall))))));

            // if (__matched_N.Count > 0) { ... handler body ... }
            var ifCondition = BinaryExpression(
                SyntaxKind.GreaterThanExpression,
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName(matchedVar),
                    IdentifierName("Count")),
                LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0)));

            var handlerBodyStatements = new List<StatementSyntax>();

            // __allMatched_N.AddRange(__matched_N);
            handlerBodyStatements.Add(ExpressionStatement(
                InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName(allMatchedVar),
                        IdentifierName("AddRange")))
                .WithArgumentList(ArgumentList(SingletonSeparatedList(
                    Argument(IdentifierName(matchedVar)))))));

            // If there's an 'as' variable, create the ExceptionGroup wrapper
            if (handler.Name != null)
            {
                var baseName = NameMangler.ToCamelCase(handler.Name);

                var hadPrevious = _variableVersions.TryGetValue(baseName, out var previousVersion);
                if (hadPrevious)
                {
                    _variableVersions[baseName] = previousVersion + 1;
                }
                else
                {
                    _variableVersions[baseName] = 0;
                }

                var asVar = hadPrevious
                    ? $"{baseName}_{_variableVersions[baseName]}"
                    : baseName;

                // var eg = new Sharpy.ExceptionGroup("", __matched_N.Cast<System.Exception>().ToList());
                var castCall = InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        InvocationExpression(
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                IdentifierName(matchedVar),
                                GenericName(Identifier("Cast"))
                                    .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList<TypeSyntax>(
                                        ParseTypeName("System.Exception")))))),
                        IdentifierName("ToList")));

                var egCreation = ObjectCreationExpression(ParseTypeName("Sharpy.ExceptionGroup"))
                    .WithArgumentList(ArgumentList(SeparatedList(new[]
                    {
                        Argument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(""))),
                        Argument(castCall)
                    })));

                handlerBodyStatements.Add(LocalDeclarationStatement(
                    VariableDeclaration(IdentifierName("var"))
                        .WithVariables(SingletonSeparatedList(
                            VariableDeclarator(Identifier(asVar))
                                .WithInitializer(EqualsValueClause(egCreation))))));

                // Generate handler body statements
                handlerBodyStatements.AddRange(handler.Body.SelectMany(GenerateBodyStatements));

                // Restore version state
                if (hadPrevious)
                {
                    _variableVersions[baseName] = previousVersion;
                }
                else
                {
                    _variableVersions.Remove(baseName);
                }
            }
            else
            {
                // No 'as' variable — just emit the handler body
                handlerBodyStatements.AddRange(handler.Body.SelectMany(GenerateBodyStatements));
            }

            catchBodyStatements.Add(IfStatement(ifCondition, Block(handlerBodyStatements)));
        }

        // Re-throw unmatched exceptions:
        // var __unmatched = __eg_N.InnerExceptions
        //     .Where(e => !__allMatched_N.Contains(e)).ToList();
        var unmatchedVar = GenerateTempVarName("unmatched");
        var whereParam = GenerateTempVarName("ex");

        var whereCall = InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName(egVar),
                            IdentifierName("InnerExceptions")),
                        IdentifierName("Where")))
                .WithArgumentList(ArgumentList(SingletonSeparatedList(
                    Argument(
                        SimpleLambdaExpression(
                            Parameter(Identifier(whereParam)),
                            PrefixUnaryExpression(
                                SyntaxKind.LogicalNotExpression,
                                InvocationExpression(
                                    MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        IdentifierName(allMatchedVar),
                                        IdentifierName("Contains")))
                                .WithArgumentList(ArgumentList(SingletonSeparatedList(
                                    Argument(IdentifierName(whereParam))))))))))),
                IdentifierName("ToList")));

        catchBodyStatements.Add(LocalDeclarationStatement(
            VariableDeclaration(IdentifierName("var"))
                .WithVariables(SingletonSeparatedList(
                    VariableDeclarator(Identifier(unmatchedVar))
                        .WithInitializer(EqualsValueClause(whereCall))))));

        // if (__unmatched.Count > 0) throw new System.AggregateException(__unmatched);
        var unmatchedCondition = BinaryExpression(
            SyntaxKind.GreaterThanExpression,
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName(unmatchedVar),
                IdentifierName("Count")),
            LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0)));

        var rethrowStmt = ThrowStatement(
            ObjectCreationExpression(ParseTypeName("System.AggregateException"))
                .WithArgumentList(ArgumentList(SingletonSeparatedList(
                    Argument(IdentifierName(unmatchedVar))))));

        catchBodyStatements.Add(IfStatement(unmatchedCondition, rethrowStmt));

        // Build the single catch clause: catch (System.AggregateException __eg_N) { ... }
        var catchDeclaration = CatchDeclaration(
            ParseTypeName("System.AggregateException"),
            Identifier(egVar));

        result.Add(CatchClause(catchDeclaration, null, Block(catchBodyStatements)));

        return result;
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
}
