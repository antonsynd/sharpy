using System.Collections.Immutable;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Lowering;
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
        // The Xunit rewrites are the TEST-HOST lowering, not the `@test` lowering (#1495). Outside a
        // test host there is no Xunit reference, so emitting them made any program containing a
        // `@test` function uncompilable under `sharpyc run` — CS0246 behind SPY0908. A `@test`
        // program is an ordinary runnable program, and its asserts fall through to the
        // framework-free arm below: the same runtime check every other assert gets, raising
        // `Sharpy.AssertionError`. That arm already handles the shapes the rewrites special-case —
        // `approx` through `TryGetApproxParts`, and everything else (isinstance, ==, in, is,
        // startswith, endswith, not) through ordinary truthiness on the expression the user wrote —
        // so nothing is lost but xUnit's failure formatting.
        if (_isInTestFunction && _context.TargetsTestHost)
        {
            return GenerateTestAssert(assert);
        }

        // Outside @test, `assert` is a real runtime check (#1070): it lowers to
        //   if (!cond) throw new global::Sharpy.AssertionError(msg?)
        // The former Debug.Assert lowering never executed — AssemblyCompiler parses with no
        // preprocessor symbols in any configuration, so [Conditional("DEBUG")] stripped every
        // assert. A future -O-analogue strip flag is spec'd but unbuilt (see the assert section
        // of docs/language_specification/statements.md).
        //
        // approx(...) comparisons generalize to a tolerance form here too (#1074), so
        // `assert x == approx(y)` works in plain functions and helpers, not only @test bodies.
        var successCondition = TryGetApproxParts(assert.Test) is { } parts
            ? BuildApproxSuccessCondition(parts)
            : WrapTruthinessIfNeeded(GenerateExpression(assert.Test), assert.Test);

        var ctorArgs = assert.Message != null
            ? ArgumentList(SingletonSeparatedList(Argument(GenerateExpression(assert.Message))))
            : ArgumentList();
        var throwStmt = ThrowStatement(
            ObjectCreationExpression(MakeGlobalQualifiedName("Sharpy", "AssertionError"))
                .WithArgumentList(ctorArgs));

        var guard = IfStatement(
            PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, ParenthesizedExpression(successCondition)),
            Block(throwStmt));

        // Narrowing an assert contributes to following statements (e.g. `assert x is not None`
        // narrows x for the rest of the scope) is materialized per-read-node by the TypeChecker
        // (#1081); the reads apply their own accessors, so no emitter-side flow update is needed.
        return guard;
    }

    /// <summary>
    /// Decompose an assert statement inside a @test-decorated function into the most
    /// appropriate xUnit assertion. Uses fully qualified Xunit.Assert to avoid ambiguity
    /// with System.Diagnostics.Debug.Assert.
    /// </summary>
    private StatementSyntax GenerateTestAssert(AssertStatement assert)
    {
        var xunitAssert = ParseQualifiedName("Xunit.Assert");
        var test = assert.Test;

        // The @test assert rewrites below select a Xunit assertion from the shape of `test`, and
        // several of them key on a CALL's callee. Those read this canonical (paren-stripped) callee so
        // `assert (isinstance)(x, (int, str))` lowers exactly like the unparenthesized form (#1170) —
        // it used to miss the tuple rewrite and fall through to a raw Builtins.Isinstance call with a
        // tuple argument, which does not bind (CS1503). Only the pattern match is normalized; the call
        // node itself stays intact, so the argument expressions keep their SemanticInfo identity.
        var testCallee = test switch
        {
            FunctionCall directCall => Shared.AstHelper.UnwrapParenthesized(directCall.Function),
            UnaryOp { Operator: UnaryOperator.Not, Operand: FunctionCall negatedCall }
                => Shared.AstHelper.UnwrapParenthesized(negatedCall.Function),
            _ => null
        };

        // The BUILTINS-QUALIFIED spelling reaches the same three rewrites (#1381). `builtins.isinstance`
        // denotes what bare `isinstance` denotes (#1322), so a @test body spelling it qualified must
        // lower identically — otherwise the phase establishes agreement everywhere EXCEPT here, and a
        // documented exception is the residue that becomes folklore.
        //
        // Reads the routing the TypeChecker RECORDED rather than deciding what the receiver is: a
        // builtins-qualified call carries CalleeRouting.Builtin, so Critical Rule 2 holds. Computed
        // once and consumed by all three arms, so a fourth arm inherits it instead of carrying its
        // own copy of the pattern.
        var isinstanceCall = test switch
        {
            UnaryOp { Operator: UnaryOperator.Not, Operand: FunctionCall negatedIsinstance } => negatedIsinstance,
            FunctionCall directIsinstance => directIsinstance,
            _ => null
        };
        var isIsinstanceCallee = testCallee is Identifier { Name: "isinstance" }
            || (testCallee is MemberAccess { IsMemberBacktickEscaped: false, Member: "isinstance" }
                && isinstanceCall != null
                && _context.SemanticInfo?.GetCalleeRouting(isinstanceCall) == CalleeRouting.Builtin);

        // assert x == approx(y[, places=n | abs=d]) → tolerance/precision-based Xunit.Assert.Equal.
        // Checked ahead of the generic `==` pattern. abs (a double) selects
        // Assert.Equal(double, double, double tolerance); places (an int) selects
        // Assert.Equal(double, double, int precision). Only the `==` form is rewritten;
        // `assert x != approx(y)` falls through to NotEqual.
        if (TryGetApproxParts(test) is { } approxParts)
        {
            return ExpressionStatement(InvocationExpression(
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, xunitAssert, IdentifierName("Equal")))
                .AddArgumentListArguments(
                    Argument(approxParts.Expected),
                    Argument(approxParts.Actual),
                    Argument(approxParts.Tolerance)));
        }

        // assert a == None / a != None on a reference-semantics operand → Xunit.Assert.Null / NotNull.
        // Honors the semantic-recorded NoneCheck lowering (#901): reference-type ==/!= None is a null
        // check, not an operator== call (see RoslynEmitter.Expressions.Operators.cs's NoneCheck branch).
        // Checked ahead of the generic ==/!= patterns so the comparison maps to a dedicated null
        // assertion. Operand order is irrelevant — detect the non-None side from the AST. NullableType/
        // OptionalType operands never reach here: the type checker rejects their ==/!= None comparisons
        // with SPY0222. The literal-shape guard below is an invariant assertion (mirrored in
        // RoslynEmitter.Expressions.Operators.cs's NoneCheck branch): NoneCheck classifies by VoidType,
        // but the #911 semantic gate (SPY0329) now rejects any non-literal VoidType comparison operand
        // before lowering, so a NoneCheck always has exactly one NoneLiteral. Requiring that here is
        // defense-in-depth — a future regression would fall through to the generic ==/!= patterns
        // rather than mis-selecting an operand.
        if (test is BinaryOp { Operator: BinaryOperator.Equal or BinaryOperator.NotEqual } noneEq
            && (noneEq.Left is NoneLiteral) != (noneEq.Right is NoneLiteral)
            && GetIrBinaryOpLowering(noneEq) == BinaryOpLowering.NoneCheck)
        {
            var nonNoneOperand = noneEq.Left is NoneLiteral ? noneEq.Right : noneEq.Left;
            var nullAssert = noneEq.Operator == BinaryOperator.Equal ? "Null" : "NotNull";
            return ExpressionStatement(InvocationExpression(
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, xunitAssert, IdentifierName(nullAssert)))
                .AddArgumentListArguments(Argument(GenerateExpression(nonNoneOperand))));
        }

        // assert a == b → Xunit.Assert.Equal(b, a)  (expected, actual order)
        if (test is BinaryOp { Operator: BinaryOperator.Equal } eq)
        {
            return ExpressionStatement(InvocationExpression(
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, xunitAssert, IdentifierName("Equal")))
                .AddArgumentListArguments(
                    Argument(GenerateExpression(eq.Right)),
                    Argument(GenerateExpression(eq.Left))));
        }

        // assert a != b → Xunit.Assert.NotEqual(b, a)
        if (test is BinaryOp { Operator: BinaryOperator.NotEqual } neq)
        {
            return ExpressionStatement(InvocationExpression(
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, xunitAssert, IdentifierName("NotEqual")))
                .AddArgumentListArguments(
                    Argument(GenerateExpression(neq.Right)),
                    Argument(GenerateExpression(neq.Left))));
        }

        // assert a is None → Xunit.Assert.Null(a)
        if (test is BinaryOp { Operator: BinaryOperator.Is, Right: NoneLiteral } isNone)
        {
            return ExpressionStatement(InvocationExpression(
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, xunitAssert, IdentifierName("Null")))
                .AddArgumentListArguments(Argument(GenerateExpression(isNone.Left))));
        }

        // assert a is not None → Xunit.Assert.NotNull(a)
        if (test is BinaryOp { Operator: BinaryOperator.IsNot, Right: NoneLiteral } isNotNone)
        {
            return ExpressionStatement(InvocationExpression(
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, xunitAssert, IdentifierName("NotNull")))
                .AddArgumentListArguments(Argument(GenerateExpression(isNotNone.Left))));
        }

        // assert a is b → Xunit.Assert.Same(b, a)
        if (test is BinaryOp { Operator: BinaryOperator.Is } isSame)
        {
            return ExpressionStatement(InvocationExpression(
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, xunitAssert, IdentifierName("Same")))
                .AddArgumentListArguments(
                    Argument(GenerateExpression(isSame.Right)),
                    Argument(GenerateExpression(isSame.Left))));
        }

        // assert a is not b → Xunit.Assert.NotSame(b, a)
        if (test is BinaryOp { Operator: BinaryOperator.IsNot } isNotSame)
        {
            return ExpressionStatement(InvocationExpression(
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, xunitAssert, IdentifierName("NotSame")))
                .AddArgumentListArguments(
                    Argument(GenerateExpression(isNotSame.Right)),
                    Argument(GenerateExpression(isNotSame.Left))));
        }

        // assert a in b → Xunit.Assert.Contains(a, b)
        if (test is BinaryOp { Operator: BinaryOperator.In } inOp)
        {
            return ExpressionStatement(InvocationExpression(
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, xunitAssert, IdentifierName("Contains")))
                .AddArgumentListArguments(
                    Argument(GenerateExpression(inOp.Left)),
                    Argument(GenerateExpression(inOp.Right))));
        }

        // assert a not in b → Xunit.Assert.DoesNotContain(a, b)
        if (test is BinaryOp { Operator: BinaryOperator.NotIn } notInOp)
        {
            return ExpressionStatement(InvocationExpression(
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, xunitAssert, IdentifierName("DoesNotContain")))
                .AddArgumentListArguments(
                    Argument(GenerateExpression(notInOp.Left)),
                    Argument(GenerateExpression(notInOp.Right))));
        }

        // assert isinstance(a, T) → Xunit.Assert.IsAssignableFrom<T>(a)
        //
        // WHAT THE OPERAND DENOTES IS NOT DECIDED HERE (Critical Rule 2). This arm used to carry its
        // own type resolution — a bare-name collection-erasure check falling back to
        // MapTypeFromExpression — which is a second derivation of what the classifier already decides,
        // and it emitted a bare `Box` for a generic operand (CS0305). It now reads the classifier's
        // answer, so the #912 erasure rule and the open-generic refusal reach @test asserts for free
        // (#1235, #1254).
        //
        // Tuples are excluded so the tuple arm below still claims them: that spelling is refused in
        // expression position (SPY0344) but lowered correctly here to `a is T1 || a is T2`, and the
        // classifier skips exactly that shape under a @test assert for the same reason.
        if (isIsinstanceCallee && test is FunctionCall isinstCall
            && isinstCall.Arguments.Length == 2
            && isinstCall.Arguments[1] is not TupleLiteral)
        {
            var typeSyntax = MapTestAssertTypeOperand(isinstCall.Arguments[1]);
            return ExpressionStatement(InvocationExpression(
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, xunitAssert,
                    GenericName(Identifier("IsAssignableFrom"))
                        .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(typeSyntax)))))
                .AddArgumentListArguments(Argument(GenerateExpression(isinstCall.Arguments[0]))));
        }

        // assert isinstance(a, (T1, T2, ...)) → Xunit.Assert.True(a is T1 || a is T2 || ...)
        if (isIsinstanceCallee && test is FunctionCall isinstTupleCall
            && isinstTupleCall.Arguments.Length == 2
            && isinstTupleCall.Arguments[1] is TupleLiteral typeTuple)
        {
            var subject = GenerateExpression(isinstTupleCall.Arguments[0]);
            var isChecks = typeTuple.Elements.Select(typeExpr =>
                (ExpressionSyntax)BinaryExpression(
                    SyntaxKind.IsExpression, subject, MapTestAssertTypeOperand(typeExpr)));
            var combined = isChecks.Aggregate((left, right) =>
                BinaryExpression(SyntaxKind.LogicalOrExpression, left, right));
            return ExpressionStatement(InvocationExpression(
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, xunitAssert, IdentifierName("True")))
                .AddArgumentListArguments(Argument(combined)));
        }

        // assert not isinstance(a, T) → Xunit.Assert.False(a is T)
        if (isIsinstanceCallee
            && test is UnaryOp { Operator: UnaryOperator.Not, Operand: FunctionCall negIsinstCall }
            && negIsinstCall.Arguments.Length == 2)
        {
            var subject = GenerateExpression(negIsinstCall.Arguments[0]);
            ExpressionSyntax isCheck;
            if (negIsinstCall.Arguments[1] is TupleLiteral negTypeTuple)
            {
                isCheck = negTypeTuple.Elements
                    .Select(typeExpr => (ExpressionSyntax)BinaryExpression(
                        SyntaxKind.IsExpression, subject, MapTestAssertTypeOperand(typeExpr)))
                    .Aggregate((left, right) => BinaryExpression(SyntaxKind.LogicalOrExpression, left, right));
            }
            else
            {
                isCheck = BinaryExpression(
                    SyntaxKind.IsExpression, subject, MapTestAssertTypeOperand(negIsinstCall.Arguments[1]));
            }
            return ExpressionStatement(InvocationExpression(
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, xunitAssert, IdentifierName("False")))
                .AddArgumentListArguments(Argument(isCheck)));
        }

        // assert s.startswith(p) → Xunit.Assert.StartsWith(p, s)
        // assert s.endswith(p)   → Xunit.Assert.EndsWith(p, s)
        // Type-gated: only when the receiver is typed `str` and there is exactly one
        // positional argument (no start/end slice args, no keyword args). User-defined
        // types with a startswith/endswith method fall through to the Assert.True fallback.
        if (testCallee is MemberAccess { Member: "startswith" or "endswith" } affixReceiver
            && test is FunctionCall affixCall
            && affixCall.Arguments.Length == 1
            && affixCall.KeywordArguments.Length == 0)
        {
            var receiverType = _context.SemanticInfo?.GetEffectiveType(affixReceiver.Object);
            if (receiverType == SemanticType.Str)
            {
                var affixMethod = affixReceiver.Member == "startswith" ? "StartsWith" : "EndsWith";
                return ExpressionStatement(InvocationExpression(
                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, xunitAssert, IdentifierName(affixMethod)))
                    .AddArgumentListArguments(
                        Argument(GenerateExpression(affixCall.Arguments[0])),
                        Argument(GenerateExpression(affixReceiver.Object))));
            }
        }

        // assert not expr → Xunit.Assert.False(expr)
        if (test is UnaryOp { Operator: UnaryOperator.Not } notOp)
        {
            var innerExpr = WrapTruthinessIfNeeded(GenerateExpression(notOp.Operand), notOp.Operand);
            var falseArgs = new List<ArgumentSyntax> { Argument(innerExpr) };
            if (assert.Message != null)
            {
                falseArgs.Add(Argument(GenerateExpression(assert.Message)));
            }
            return ExpressionStatement(InvocationExpression(
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, xunitAssert, IdentifierName("False")))
                .WithArgumentList(ArgumentList(SeparatedList(falseArgs))));
        }

        // assert a > b, a < b, a >= b, a <= b → Xunit.Assert.True(expr, message?)
        if (test is BinaryOp
            {
                Operator: BinaryOperator.GreaterThan or BinaryOperator.LessThan
                    or BinaryOperator.GreaterThanOrEqual or BinaryOperator.LessThanOrEqual
            } cmpOp)
        {
            var cmpExpr = GenerateExpression(cmpOp);
            var cmpArgs = new List<ArgumentSyntax> { Argument(cmpExpr) };
            if (assert.Message != null)
            {
                cmpArgs.Add(Argument(GenerateExpression(assert.Message)));
            }
            return ExpressionStatement(InvocationExpression(
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, xunitAssert, IdentifierName("True")))
                .WithArgumentList(ArgumentList(SeparatedList(cmpArgs))));
        }

        // Fallback: assert expr → Xunit.Assert.True(expr, message?)
        var truthyExpr = WrapTruthinessIfNeeded(GenerateExpression(test), test);
        var trueArgs = new List<ArgumentSyntax> { Argument(truthyExpr) };
        if (assert.Message != null)
        {
            trueArgs.Add(Argument(GenerateExpression(assert.Message)));
        }
        return ExpressionStatement(InvocationExpression(
            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, xunitAssert, IdentifierName("True")))
            .WithArgumentList(ArgumentList(SeparatedList(trueArgs))));
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
        // Narrowing of reads inside each branch is materialized per-read-node by the TypeChecker
        // (#1081); the emitter no longer re-derives which variables a branch condition narrows.
        var condition = WrapTruthinessIfNeeded(GenerateExpression(ifStmt.Test), ifStmt.Test);

        // Save scope before the if statement so each branch (then/elif/else)
        // gets an independent copy. This prevents variable declarations in one
        // branch from leaking into sibling branches (fixes #363).
        var preIfScope = SaveScope();

        var thenBlock = GenerateSuiteBlock(ifStmt.ThenBody);

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
                currentElse = GenerateSuiteBlock(ifStmt.ElseBody);
            }

            // Process elif clauses in reverse order
            for (int i = ifStmt.ElifClauses.Length - 1; i >= 0; i--)
            {
                // Restore to pre-if scope so elif doesn't see then-block or other elif variables (#363)
                RestoreScope(preIfScope);

                var elif = ifStmt.ElifClauses[i];
                var elifCondition = WrapTruthinessIfNeeded(GenerateExpression(elif.Test), elif.Test);
                var elifBody = GenerateSuiteBlock(elif.Body);

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

    private StatementSyntax GenerateWhile(WhileStatement whileStmt)
    {
        // Narrowing of reads inside the loop body is materialized per-read-node by the TypeChecker
        // (#1081); the emitter no longer re-derives which variables the condition narrows.

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
            var simpleBody = GenerateSuiteBlock(whileStmt.Body);
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
                    VariableDeclarator(EscapedIdentifier(flagName))
                        .WithInitializer(EqualsValueClause(LiteralExpression(SyntaxKind.TrueLiteralExpression)))))));

        // Transform the body to set flag to false before break
        var transformedBody = TransformLoopBodyForElse(whileStmt.Body, flagName);
        var bodyBlock = GenerateSuiteBlock(transformedBody);

        // while (condition) { transformedBody }
        statements.Add(WhileStatement(condition, bodyBlock));

        // if (_loopCompleted) { elseBody }
        var elseBodyBlock = GenerateSuiteBlock(whileStmt.ElseBody);
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
        //
        // This test once carried an exception for variadic parameters, which the emitter recognised
        // by tracking their names per scope: `*args: str` was typed as the ELEMENT type `str` and
        // would otherwise have been iterated as one string's characters. The exception is gone with
        // its cause — a variadic binds as `array[str]` (#1292), so it never reaches this test at all,
        // and the emitter reads the semantic type without knowing which parameters were variadic.
        if (iteratorType == SemanticType.Str)
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
            iterator = GenerateEnumValuesIterator(enumUdt);
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
                    VariableDeclarator(EscapedIdentifier(flagName))
                        .WithInitializer(EqualsValueClause(LiteralExpression(SyntaxKind.TrueLiteralExpression)))))));

        // Transform the body to set flag to false before break
        var transformedBody = TransformLoopBodyForElse(forStmt.Body, flagName);

        // foreach (...) { transformedBody }
        statements.Add(GenerateForEachCore(forStmt.Target, iterator, transformedBody, iteratorType, forStmt.IsAsync));

        // if (_loopCompleted) { elseBody }
        var elseBodyBlock = GenerateSuiteBlock(forStmt.ElseBody);
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
            var loopVar = LocalBaseName(varName.Name, varName.IsNameBacktickEscaped);
            var tempLoopVar = GenerateTempVarName("loopVar");

            // Check if the variable is already declared in an enclosing scope
            bool varExistsInOuterScope = _declaredVariables.Contains(loopVar) || _variableVersions.ContainsKey(loopVar);

            // Register the loop variable BEFORE generating the body
            // so that assignments to it are treated as updates
            if (!varExistsInOuterScope)
            {
                _declaredVariables.Add(loopVar);
            }
            RegisterLocalSlot(loopVar, varName.Name);

            // Generate the body - assignments to loopVar will be updates, not declarations
            var body = GenerateSuiteBlock(bodyStatements);

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
                            VariableDeclarator(EscapedIdentifier(loopVar))
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
                    var name = LocalBaseName(id.Name, id.IsNameBacktickEscaped);
                    _declaredVariables.Add(name);
                    RegisterLocalSlot(name, id.Name);
                }

                // Now generate the body
                var body = GenerateSuiteBlock(bodyStatements);

                // Generate: foreach (var (x, y) in items)
                var variables = identifiers
                    .Select(id =>
                    {
                        var name = LocalBaseName(id.Name, id.IsNameBacktickEscaped);
                        return SingleVariableDesignation(EscapedIdentifier(name));
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
            var loopBody = GenerateSuiteBlock(bodyStatements);

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

    /// <summary>
    /// Reads a with-item's context-manager kind from the lowering IR (E2 #1056, migrates
    /// <c>_contextManagerKinds</c>). Returns <c>null</c> when no <see cref="IrWithItem"/> was recorded
    /// (an error case), which the caller treats as the default disposable protocol.
    /// </summary>
    private ContextManagerKind? GetIrContextManagerKind(WithItem item)
    {
        return _context.Ir?.WithItems.TryGetValue(item, out var withItem) == true
            ? withItem.Kind
            : null;
    }

    private StatementSyntax GenerateWith(WithStatement withStmt)
    {
        //   with assert_raises(ExceptionType): body
        // is a marker with no runtime implementation — codegen rewrites it into a flag, a try/catch
        // and a `Sharpy.AssertionError` (#1413). Every form of it is intercepted in
        // GenerateBodyStatements, which can emit the several FLAT statements that lowering needs;
        // GenerateWith returns a single StatementSyntax, and a block to hold them would hide an
        // `as` capture from the statements that follow the `with`. So there is deliberately no
        // assert_raises arm here.

        // Generate the body block
        var bodyStatements = GenerateSuite(withStmt.Body).ToList();

        // Build using/try-finally statements from inside out (last item wraps the body,
        // first item wraps everything)
        StatementSyntax innermost = Block(bodyStatements);

        for (int i = withStmt.Items.Length - 1; i >= 0; i--)
        {
            var item = withStmt.Items[i];
            var cmKind = GetIrContextManagerKind(item);

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
    /// If the expression is a call to unittest.approx (bare <c>approx(...)</c> or qualified
    /// <c>m.approx(...)</c>), returns the call; otherwise null. Used by the approx equality
    /// assert rewrite.
    /// </summary>
    private static FunctionCall? AsApproxCall(Expression expr)
    {
        if (expr is FunctionCall call
            && call.Function is Identifier { Name: "approx" } or MemberAccess { Member: "approx" })
        {
            return call;
        }
        return null;
    }

    /// <summary>
    /// The materialized operands of an <c>assert x == approx(y[, places | abs])</c> comparison:
    /// the approx expected value, the actual value, and the tolerance. <see cref="IsPlaces"/>
    /// distinguishes an integer <c>places</c> precision from a floating-point <c>abs</c> tolerance.
    /// </summary>
    private readonly record struct ApproxParts(
        ExpressionSyntax Expected, ExpressionSyntax Actual, ExpressionSyntax Tolerance, bool IsPlaces);

    /// <summary>
    /// Recognizes <c>x == approx(y[, places=n | abs=d])</c> and materializes its operands. The
    /// approx(...) call's first argument is the expected value; the other operand of <c>==</c> is
    /// the actual value. abs (keyword or 3rd positional) wins over places (keyword or 2nd
    /// positional); the default is <c>places=7</c>. Returns null for any other test expression.
    /// Shared by the @test (xUnit) and non-@test (AssertionError) assert lowerings (#1074).
    /// </summary>
    private ApproxParts? TryGetApproxParts(Expression test)
    {
        if (test is not BinaryOp { Operator: BinaryOperator.Equal } approxEq
            || (AsApproxCall(approxEq.Left) ?? AsApproxCall(approxEq.Right)) is not FunctionCall approxCall
            || approxCall.Arguments.Length < 1)
        {
            return null;
        }

        var actualExpr = AsApproxCall(approxEq.Left) != null ? approxEq.Right : approxEq.Left;
        var expected = GenerateExpression(approxCall.Arguments[0]);
        var actual = GenerateExpression(actualExpr);

        var absKw = approxCall.KeywordArguments.FirstOrDefault(k => k.Name == "abs");
        var placesKw = approxCall.KeywordArguments.FirstOrDefault(k => k.Name == "places");

        ExpressionSyntax toleranceArg;
        bool isPlaces;
        if (absKw != null)
        { toleranceArg = GenerateExpression(absKw.Value); isPlaces = false; }
        else if (approxCall.Arguments.Length >= 3)
        { toleranceArg = GenerateExpression(approxCall.Arguments[2]); isPlaces = false; }
        else if (placesKw != null)
        { toleranceArg = GenerateExpression(placesKw.Value); isPlaces = true; }
        else if (approxCall.Arguments.Length >= 2)
        { toleranceArg = GenerateExpression(approxCall.Arguments[1]); isPlaces = true; }
        else
        { toleranceArg = LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(7)); isPlaces = true; }

        return new ApproxParts(expected, actual, toleranceArg, isPlaces);
    }

    /// <summary>
    /// Builds the boolean success condition for an approx assertion outside a @test function,
    /// matching Python's <c>assertAlmostEqual</c>/<c>approx</c> tolerance semantics:
    /// <c>abs</c> → <c>Math.Abs(actual - expected) &lt;= tolerance</c>; <c>places</c> →
    /// <c>Math.Round(Math.Abs(actual - expected), places) == 0</c>. The caller negates this to
    /// decide whether to throw <c>AssertionError</c>.
    /// </summary>
    private ExpressionSyntax BuildApproxSuccessCondition(ApproxParts parts)
    {
        var doubleType = PredefinedType(Token(SyntaxKind.DoubleKeyword));
        var diff = InvocationExpression(
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    MakeGlobalQualifiedName("System", "Math"), IdentifierName("Abs")))
            .AddArgumentListArguments(Argument(
                BinaryExpression(SyntaxKind.SubtractExpression,
                    CastExpression(doubleType, ParenthesizedExpression(parts.Actual)),
                    CastExpression(doubleType, ParenthesizedExpression(parts.Expected)))));

        if (parts.IsPlaces)
        {
            var rounded = InvocationExpression(
                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        MakeGlobalQualifiedName("System", "Math"), IdentifierName("Round")))
                .AddArgumentListArguments(
                    Argument(diff),
                    Argument(CastExpression(PredefinedType(Token(SyntaxKind.IntKeyword)), ParenthesizedExpression(parts.Tolerance))));
            return BinaryExpression(SyntaxKind.EqualsExpression, rounded,
                LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0.0)));
        }

        return BinaryExpression(SyntaxKind.LessThanOrEqualExpression, diff,
            CastExpression(doubleType, ParenthesizedExpression(parts.Tolerance)));
    }

    /// <summary>
    /// Returns true if the given call targets unittest.assert_raises (bare or qualified).
    /// </summary>
    private static bool IsAssertRaisesCall(FunctionCall call)
        => AssertRaisesForm.IsCall(call);

    /// <summary>
    /// If the statement is <c>with assert_raises(E[, match=...]) [as exc]:</c>, emits the flat
    /// statements it lowers to and returns true. Both forms come here: the lowering is several
    /// statements either way, and emitting them flat is what keeps an `as` capture visible to the
    /// statements after the <c>with</c>.
    /// </summary>
    private bool TryGenerateAssertRaises(WithStatement withStmt, out List<StatementSyntax> statements)
    {
        statements = null!;
        if (withStmt.Items.Length != 1)
            return false;
        if (withStmt.Items[0].ContextExpression is not FunctionCall call || !IsAssertRaisesCall(call))
            return false;
        if (call.Arguments.Length == 0)
            return false;

        statements = GenerateAssertThrowsStatements(
            call.Arguments[0],
            withStmt.Body,
            withStmt.Items[0].Name,
            GetAssertRaisesMatchArgument(call),
            withStmt.Items[0].IsNameBacktickEscaped);
        return true;
    }

    /// <summary>
    /// Returns the match pattern expression for an assert_raises call, or null if none.
    /// Accepts either a <c>match=</c> keyword argument or a second positional argument
    /// (mirroring pytest's <c>raises(E, match=...)</c> / <c>raises(E, "pattern")</c>).
    /// </summary>
    private static Expression? GetAssertRaisesMatchArgument(FunctionCall call)
    {
        foreach (var kw in call.KeywordArguments)
        {
            if (kw.Name == "match")
                return kw.Value;
        }
        if (call.Arguments.Length >= 2)
            return call.Arguments[1];
        return null;
    }

    /// <summary>
    /// Generates the statement(s) for an assert_raises block, supporting an optional
    /// regex <paramref name="matchExpr"/> (re.search semantics, like pytest's
    /// <c>raises(E, match=...)</c>). Returns a flat list (never a wrapping block) so that an
    /// <c>as</c> capture remains visible to statements following the <c>with</c>:
    /// <code>
    /// bool raised_1 = false;
    /// try { body } catch (E caught_1) { raised_1 = true; exc = caught_1; }
    /// if (!raised_1) throw new Sharpy.AssertionError("Expected E to be raised, ...");
    /// if (!Regex.IsMatch(exc.Message, matchExpr)) throw new Sharpy.AssertionError(...);
    /// </code>
    /// When there is no capture but a match is present, a temporary local is introduced. Nothing
    /// here names a test framework, which is what lets the form appear outside a @test (#1413).
    /// </summary>
    private List<StatementSyntax> GenerateAssertThrowsStatements(
        Expression exceptionTypeExpr,
        IReadOnlyList<Statement> body,
        string? captureName,
        Expression? matchExpr,
        bool captureIsEscaped = false)
    {
        TypeSyntax exceptionType = exceptionTypeExpr switch
        {
            Identifier typeId => IdentifierName(NameMangler.Transform(typeId.Name, NameContext.Type)),
            // Module-qualified exception type (e.g. zoneinfo.ZoneInfoNotFoundError):
            // resolve the module-exported TypeSymbol to its fully-qualified C# name so we emit
            // Throws<global::...Error> instead of MapTypeFromExpression's object fallback.
            MemberAccess exceptionMemberAccess when TryResolveModuleExportedType(exceptionMemberAccess) is { } moduleExceptionType =>
                BuildTypeNameFromFqn(GetFullyQualifiedTypeName(moduleExceptionType.Symbol, moduleExceptionType.OriginalName)),
            _ => _typeMapper.MapTypeFromExpression(exceptionTypeExpr)
        };

        // The user's own spelling of the exception, for the failure message. `exceptionType` is the
        // MANGLED C# name (and may be fully qualified), which is not what the user wrote.
        var exceptionDisplayName = exceptionTypeExpr switch
        {
            Identifier typeId => typeId.Name,
            MemberAccess memberAccess => memberAccess.Member,
            _ => exceptionType.ToString()
        };

        var bodyStatements = GenerateSuite(body).ToList();
        var result = new List<StatementSyntax>();

        // A local is needed when the caller captures the exception (`as exc`) or when a match
        // assertion must reference the raised exception's Message. It is declared ABOVE the try —
        // not inside it — so an `as` capture stays visible to statements FOLLOWING the with, which
        // is the scope the TypeChecker's capture arm defines it in.
        string? localName = null;
        if (captureName != null)
        {
            localName = GetMangledVariableName(captureName, isNewDeclaration: true, captureIsEscaped);
            _declaredVariables.Add(localName);
        }
        else if (matchExpr != null)
        {
            localName = GenerateTempVarName("ex");
        }

        if (localName != null)
        {
            result.Add(LocalDeclarationStatement(
                VariableDeclaration(exceptionType)
                    .WithVariables(SingletonSeparatedList(
                        VariableDeclarator(EscapedIdentifier(localName))
                            .WithInitializer(EqualsValueClause(
                                PostfixUnaryExpression(
                                    SyntaxKind.SuppressNullableWarningExpression,
                                    LiteralExpression(SyntaxKind.NullLiteralExpression))))))));
        }

        // A FLAG, not a bare `try { body; throw ... } catch (E) { }`. The naive shape self-swallows:
        // when E is AssertionError — or any base of it, such as Exception — the synthetic
        // AssertionError thrown at the end of the try is caught by the same catch, and a body that
        // raised nothing passes silently. The flag is read after the try, where no catch can reach it.
        var raisedFlag = GenerateTempVarName("raised");
        result.Add(LocalDeclarationStatement(
            VariableDeclaration(PredefinedType(Token(SyntaxKind.BoolKeyword)))
                .WithVariables(SingletonSeparatedList(
                    VariableDeclarator(Identifier(raisedFlag))
                        .WithInitializer(EqualsValueClause(
                            LiteralExpression(SyntaxKind.FalseLiteralExpression)))))));

        var catchBody = new List<StatementSyntax>
        {
            ExpressionStatement(AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                IdentifierName(raisedFlag),
                LiteralExpression(SyntaxKind.TrueLiteralExpression)))
        };

        // The caught exception is named only when something reads it — an `as` capture, or the
        // temporary a match= introduces. Naming it unconditionally would leave an unused identifier
        // in every plain `with assert_raises(E):`.
        var catchDeclaration = CatchDeclaration(exceptionType);
        if (localName != null)
        {
            var caughtName = GenerateTempVarName("caught");
            catchDeclaration = catchDeclaration.WithIdentifier(Identifier(caughtName));
            catchBody.Add(ExpressionStatement(AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                EscapedIdentifierName(localName),
                IdentifierName(caughtName))));
        }

        result.Add(TryStatement(
            Block(bodyStatements),
            SingletonList(CatchClause()
                .WithDeclaration(catchDeclaration)
                .WithBlock(Block(catchBody))),
            @finally: null));

        // An exception of the WRONG type is deliberately not caught — it propagates with its own
        // message and stack, which says more than a wrapper would.
        result.Add(IfStatement(
            PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, IdentifierName(raisedFlag)),
            ThrowStatement(
                ObjectCreationExpression(MakeGlobalQualifiedName("Sharpy", "AssertionError"))
                    .WithArgumentList(ArgumentList(SingletonSeparatedList(
                        Argument(LiteralExpression(
                            SyntaxKind.StringLiteralExpression,
                            Literal($"Expected {exceptionDisplayName} to be raised, but no exception was raised")))))))));

        if (matchExpr != null)
        {
            // re.search semantics — the pattern matches anywhere in .Message, unanchored, which is
            // what pytest's raises(match=...) does and what Xunit.Assert.Matches did here before.
            // NOTE the argument order flips: Assert.Matches(pattern, actual) vs
            // Regex.IsMatch(input, pattern).
            var messageAccess = MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                EscapedIdentifierName(localName!),
                IdentifierName("Message"));

            var isMatchCall = InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        MakeGlobalQualifiedName("System", "Text", "RegularExpressions", "Regex"),
                        IdentifierName("IsMatch")))
                .AddArgumentListArguments(
                    Argument(messageAccess),
                    Argument(GenerateExpression(matchExpr)));

            // "Expected the raised {E}'s message to match <pattern>, but it was: <message>"
            var failureMessage = BinaryExpression(
                SyntaxKind.AddExpression,
                BinaryExpression(
                    SyntaxKind.AddExpression,
                    BinaryExpression(
                        SyntaxKind.AddExpression,
                        LiteralExpression(
                            SyntaxKind.StringLiteralExpression,
                            Literal($"Expected the raised {exceptionDisplayName}'s message to match ")),
                        GenerateExpression(matchExpr)),
                    LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(", but it was: "))),
                messageAccess);

            result.Add(IfStatement(
                PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, ParenthesizedExpression(isMatchCall)),
                ThrowStatement(
                    ObjectCreationExpression(MakeGlobalQualifiedName("Sharpy", "AssertionError"))
                        .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(failureMessage)))))));
        }

        return result;
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
            var varName = GetMangledVariableName(item.Name, isNewDeclaration: true, item.IsNameBacktickEscaped);
            _declaredVariables.Add(varName);

            var declaration = VariableDeclaration(IdentifierName("var"))
                .WithVariables(SingletonSeparatedList(
                    VariableDeclarator(EscapedIdentifier(varName))
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
    /// Generates try/finally (or try/catch/finally) with explicit Enter()/Exit() calls for
    /// dunder-protocol context managers.
    ///
    /// 1-arg __exit__ (sync):
    ///   var __ctx_N = expr; var asVar = __ctx_N.Enter(); try { body } finally { __ctx_N.Exit(); }
    /// 1-arg __aexit__ (async):
    ///   var __ctx_N = expr; var asVar = await __ctx_N.AenterAsync(); try { body } finally { await __ctx_N.AexitAsync(); }
    /// 4-arg __exit__ (sync) — with exception suppression support:
    ///   var __ctx_N = expr;
    ///   var asVar = __ctx_N.Enter();
    ///   Exception? __exc_N = null;
    ///   try { body }
    ///   catch (Exception __e_N) {
    ///       __exc_N = __e_N;
    ///       var __suppress_N = __ctx_N.Exit(Optional&lt;T1&gt;.Some(__e_N.GetType()), Optional&lt;T2&gt;.Some(__e_N), Optional&lt;T3&gt;.None);
    ///       if (!__suppress_N) throw;
    ///   }
    ///   finally { if (__exc_N == null) __ctx_N.Exit(Optional&lt;T1&gt;.None, Optional&lt;T2&gt;.None, Optional&lt;T3&gt;.None); }
    /// 4-arg __aexit__ (async): analogous, with await on Enter/Exit calls.
    /// </summary>
    private StatementSyntax GenerateWithDunderProtocol(WithItem item, StatementSyntax innermost, ContextManagerKind cmKind)
    {
        bool isAsync = cmKind == ContextManagerKind.AsyncDunderProtocol;
        var enterMethod = isAsync ? ProtocolConstants.AenterAsync : ProtocolConstants.Enter;
        var exitMethod = isAsync ? ProtocolConstants.AexitAsync : ProtocolConstants.Exit;

        // Determine which __exit__ form was declared (1-arg vs 4-arg).
        var exitMethodSymbol = TryGetExitMethod(item.ContextExpression, isAsync);
        bool isFourArgExit = exitMethodSymbol != null && exitMethodSymbol.Parameters.Count == 4;

        var contextExpr = GenerateExpression(item.ContextExpression);
        var ctxVarName = GenerateTempVarName("ctx");
        var statements = new List<StatementSyntax>();

        // var __ctx_N = expr;
        statements.Add(LocalDeclarationStatement(
            VariableDeclaration(IdentifierName("var"))
                .WithVariables(SingletonSeparatedList(
                    VariableDeclarator(EscapedIdentifier(ctxVarName))
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
            var varName = GetMangledVariableName(item.Name, isNewDeclaration: true, item.IsNameBacktickEscaped);
            _declaredVariables.Add(varName);
            statements.Add(LocalDeclarationStatement(
                VariableDeclaration(IdentifierName("var"))
                    .WithVariables(SingletonSeparatedList(
                        VariableDeclarator(EscapedIdentifier(varName))
                            .WithInitializer(EqualsValueClause(enterCall))))));
        }
        else
        {
            // Still call Enter() for side effects
            statements.Add(ExpressionStatement(enterCall));
        }

        var bodyBlock = innermost is BlockSyntax blk ? blk : Block(innermost);

        if (isFourArgExit)
        {
            statements.Add(GenerateFourArgExitTry(ctxVarName, exitMethod, bodyBlock, isAsync, exitMethodSymbol!));
        }
        else
        {
            // 1-arg form: simple try { body } finally { __ctx_N.Exit(); }
            ExpressionSyntax exitCall = InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName(ctxVarName),
                    IdentifierName(exitMethod)));
            if (isAsync)
                exitCall = AwaitExpression(exitCall);

            var tryStmt = TryStatement(
                bodyBlock,
                List<CatchClauseSyntax>(),
                FinallyClause(Block(ExpressionStatement(exitCall))));
            statements.Add(tryStmt);
        }

        return Block(statements);
    }

    /// <summary>
    /// Resolves the FunctionSymbol for the context manager's __exit__ (or __aexit__) method.
    /// Returns null if the symbol cannot be resolved.
    /// </summary>
    private FunctionSymbol? TryGetExitMethod(Expression contextExpression, bool isAsync)
    {
        var exprType = _context.SemanticInfo?.GetExpressionType(contextExpression);
        if (exprType == null)
            return null;

        var typeSymbol = ExtractTypeSymbolForContextManager(exprType);
        if (typeSymbol == null)
            return null;

        var exitName = isAsync ? DunderNames.Aexit : DunderNames.Exit;
        return typeSymbol.Methods.FirstOrDefault(m => m.Name == exitName);
    }

    /// <summary>
    /// Extracts the TypeSymbol underlying a SemanticType, unwrapping nullable/optional layers.
    /// </summary>
    private static TypeSymbol? ExtractTypeSymbolForContextManager(SemanticType type)
    {
        return type switch
        {
            UserDefinedType udt => udt.Symbol,
            NullableType nullable => ExtractTypeSymbolForContextManager(nullable.UnderlyingType),
            OptionalType optional => ExtractTypeSymbolForContextManager(optional.UnderlyingType),
            _ => null
        };
    }

    /// <summary>
    /// Generates the try/catch/finally block for the 4-arg __exit__ form. See
    /// <see cref="GenerateWithDunderProtocol"/> for the emitted shape.
    /// </summary>
    private StatementSyntax GenerateFourArgExitTry(string ctxVarName, string exitMethod, BlockSyntax bodyBlock, bool isAsync, FunctionSymbol exitMethodSymbol)
    {
        var excVarName = GenerateTempVarName("exc");
        var ePrime = GenerateTempVarName("e");
        var suppressVarName = GenerateTempVarName("suppress");

        // Build TypeSyntax wrappers for each of the three exception parameters of __exit__.
        // Parameters: [self, exc_type, exc_val, exc_tb]. Indices 1..3 are the exception args.
        // Each parameter is expected to be OptionalType<T>; if not, we fall back to passing
        // the raw value (best-effort, the validator will already have flagged this).
        var excTypeOptInner = GetOptionalInnerSyntax(exitMethodSymbol.Parameters[1].Type);
        var excValOptInner = GetOptionalInnerSyntax(exitMethodSymbol.Parameters[2].Type);
        var excTbOptInner = GetOptionalInnerSyntax(exitMethodSymbol.Parameters[3].Type);

        // Exception? __exc_N = null;
        var excTypeSyntax = NullableType(
            QualifiedName(
                AliasQualifiedName(IdentifierName(Token(SyntaxKind.GlobalKeyword)), IdentifierName("System")),
                IdentifierName("Exception")));
        var excDecl = LocalDeclarationStatement(
            VariableDeclaration(excTypeSyntax)
                .WithVariables(SingletonSeparatedList(
                    VariableDeclarator(EscapedIdentifier(excVarName))
                        .WithInitializer(EqualsValueClause(LiteralExpression(SyntaxKind.NullLiteralExpression))))));

        // __e_N.GetType()
        var getTypeCall = InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName(ePrime),
                IdentifierName("GetType")));

        // __ctx_N.Exit(Optional<T1>.Some(__e_N.GetType()), Optional<T2>.Some(__e_N), Optional<T3>.None)
        var exitCallInCatch = InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName(ctxVarName),
                IdentifierName(exitMethod)))
            .WithArgumentList(ArgumentList(SeparatedList(new[]
            {
                Argument(MakeOptionalArg(excTypeOptInner, getTypeCall, isSome: true)),
                Argument(MakeOptionalArg(excValOptInner, IdentifierName(ePrime), isSome: true)),
                Argument(MakeOptionalArg(excTbOptInner, null, isSome: false))
            })));
        ExpressionSyntax exitCallInCatchExpr = isAsync ? AwaitExpression(exitCallInCatch) : exitCallInCatch;

        // var __suppress_N = __ctx_N.Exit(...)
        var suppressDecl = LocalDeclarationStatement(
            VariableDeclaration(IdentifierName("var"))
                .WithVariables(SingletonSeparatedList(
                    VariableDeclarator(EscapedIdentifier(suppressVarName))
                        .WithInitializer(EqualsValueClause(exitCallInCatchExpr)))));

        // __exc_N = __e_N;
        var excAssign = ExpressionStatement(
            AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                IdentifierName(excVarName),
                IdentifierName(ePrime)));

        // if (!__suppress_N) throw;
        var ifThrow = IfStatement(
            PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, IdentifierName(suppressVarName)),
            ThrowStatement());

        var catchBlock = Block(excAssign, suppressDecl, ifThrow);

        // catch (Exception __e_N) { ... }
        var catchClause = CatchClause()
            .WithDeclaration(CatchDeclaration(
                QualifiedName(
                    AliasQualifiedName(IdentifierName(Token(SyntaxKind.GlobalKeyword)), IdentifierName("System")),
                    IdentifierName("Exception")))
                .WithIdentifier(Identifier(ePrime)))
            .WithBlock(catchBlock);

        // __ctx_N.Exit(Optional<T1>.None, Optional<T2>.None, Optional<T3>.None)
        var exitCallInFinally = InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName(ctxVarName),
                IdentifierName(exitMethod)))
            .WithArgumentList(ArgumentList(SeparatedList(new[]
            {
                Argument(MakeOptionalArg(excTypeOptInner, null, isSome: false)),
                Argument(MakeOptionalArg(excValOptInner, null, isSome: false)),
                Argument(MakeOptionalArg(excTbOptInner, null, isSome: false))
            })));
        ExpressionSyntax exitCallInFinallyExpr = isAsync ? AwaitExpression(exitCallInFinally) : exitCallInFinally;

        // if (__exc_N == null) __ctx_N.Exit(...)
        var finallyIf = IfStatement(
            BinaryExpression(
                SyntaxKind.EqualsExpression,
                IdentifierName(excVarName),
                LiteralExpression(SyntaxKind.NullLiteralExpression)),
            ExpressionStatement(exitCallInFinallyExpr));

        var finallyBlock = Block(finallyIf);

        var tryStmt = TryStatement(
            bodyBlock,
            SingletonList(catchClause),
            FinallyClause(finallyBlock));

        return Block(excDecl, tryStmt);
    }

    /// <summary>
    /// Returns the inner TypeSyntax (T) for an OptionalType&lt;T&gt; parameter, or null if the
    /// parameter is not declared as Optional. When null, the caller falls back to passing the
    /// raw value/null literal.
    /// </summary>
    private TypeSyntax? GetOptionalInnerSyntax(SemanticType paramType)
    {
        if (paramType is OptionalType opt)
            return _typeMapper.MapSemanticType(opt.UnderlyingType);
        return null;
    }

    /// <summary>
    /// Builds an argument expression for an Optional&lt;T&gt; parameter.
    /// - isSome=true:  Optional&lt;T&gt;.Some(value)
    /// - isSome=false: Optional&lt;T&gt;.None
    /// When innerType is null (parameter not declared Optional), falls back to passing the value
    /// directly or 'null'.
    /// </summary>
    private ExpressionSyntax MakeOptionalArg(TypeSyntax? innerType, ExpressionSyntax? value, bool isSome)
    {
        if (innerType == null)
        {
            // Fallback: pass value or null literal directly
            return isSome && value != null
                ? value
                : LiteralExpression(SyntaxKind.NullLiteralExpression);
        }

        // global::Sharpy.Optional<T>
        var optionalT = QualifiedName(
            AliasQualifiedName(IdentifierName(Token(SyntaxKind.GlobalKeyword)), IdentifierName("Sharpy")),
            GenericName(Identifier("Optional"))
                .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(innerType))));

        if (isSome && value != null)
        {
            // Optional<T>.Some(value)
            return InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    optionalT,
                    IdentifierName("Some")))
                .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(value))));
        }

        // Optional<T>.None
        return MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            optionalT,
            IdentifierName("None"));
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

        var tryBlock = GenerateSuiteBlock(tryStmt.Body);
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
                    VariableDeclarator(EscapedIdentifier(flagName))
                        .WithInitializer(EqualsValueClause(LiteralExpression(SyntaxKind.FalseLiteralExpression))))));

        // Generate try body with flag set to true at the end.
        var tryBodyStatements = new List<StatementSyntax>();
        tryBodyStatements.AddRange(GenerateSuite(tryStmt.Body));
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
        var elseBlock = GenerateSuiteBlock(tryStmt.ElseBody);
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
                    MakeGlobalQualifiedName("System", "InvalidOperationException"))
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
                        var catchBlock = GenerateSuiteBlock(handler.Body);
                        var declaration = CatchDeclaration(MapClassifiedTypeOperand(typeArg));
                        result.Add(CatchClause(declaration, filterClause, catchBlock));
                    }
                    continue;
                }

                // `except (A, B) as e:` — C# has no multi-type catch, and mapping the tuple annotation
                // emitted `catch (ValueTuple<A, B> e)` (CS0155). The semantic phase decided the base to
                // bind at and the alternatives to discriminate on, so this catches at that base and
                // filters: catch (Base e) when (e is A || e is B). Handler order is unchanged, which is
                // what keeps CPython's "first matching handler wins" (verified with python3).
                if (_context.SemanticInfo?.GetTypeTestLowering(handler.ExceptionType) is
                    { Kind: TypeTestLoweringKind.ExceptionAlternation, Alternatives: { } alternatives }
                    alternation
                    && handler.Name != null)
                {
                    result.Add(GenerateAlternationCatchClause(handler, alternation, alternatives, filterClause));
                    continue;
                }

                var exceptionType = MapClassifiedTypeOperand(handler.ExceptionType);

                if (handler.Name != null)
                {
                    var baseName = LocalBaseName(handler.Name, handler.IsNameBacktickEscaped);

                    // Track exception variable in the slot table so nested catch clauses with the
                    // same name get versioned (e, e_1, ...) to avoid CS0136 in generated C#. The
                    // binding is scoped to the handler body, so the previous slot state is captured
                    // and put back verbatim afterwards — spelling included (#1386).
                    var saved = CaptureSlot(baseName);
                    var version = saved.Existed ? saved.Version + 1 : 0;
                    SetSlotVersion(baseName, version, handler.Name);

                    var exceptionVar = saved.Existed ? $"{baseName}_{version}" : baseName;

                    var catchBlock = GenerateSuiteBlock(handler.Body);
                    var declaration = CatchDeclaration(exceptionType, Identifier(exceptionVar));

                    RestoreSlot(baseName, saved);

                    result.Add(CatchClause(declaration, filterClause, catchBlock));
                }
                else
                {
                    var catchBlock = GenerateSuiteBlock(handler.Body);
                    var declaration = CatchDeclaration(exceptionType);
                    result.Add(CatchClause(declaration, filterClause, catchBlock));
                }
            }
            else
            {
                // Bare except — with filter we still need a declaration to attach the filter to
                var catchBlock = GenerateSuiteBlock(handler.Body);
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

    /// <summary>
    /// Renders one type operand of an <c>isinstance</c> inside a <c>@test</c> assert, applying the
    /// classification the TypeChecker recorded for it (#1235, #1254).
    /// <para>
    /// The tuple spelling is the one shape the classifier deliberately skips here — the rewrite lowers
    /// it to <c>a is T1 || a is T2</c>, which is correct and which SPY0344 would otherwise forbid — so
    /// its elements have no recorded decision and fall back to mapping the written expression, exactly
    /// as they did before. Every other spelling reads the decision.
    /// </para>
    /// </summary>
    private TypeSyntax MapTestAssertTypeOperand(Expression typeOperand)
        => _context.SemanticInfo?.GetTypeTestLowering(typeOperand) is { } lowering
            ? MapTypeTestTarget(lowering)
            : _typeMapper.MapTypeFromExpression(typeOperand);

    /// <summary>
    /// Emits <c>except (A, B) as e:</c> as <c>catch (Base e) when (e is A || e is B)</c>, applying the
    /// base and the alternatives the semantic phase decided (#1235). Any user <c>when</c> filter is
    /// composed with <c>&amp;&amp;</c> so both conditions still have to hold.
    /// </summary>
    private CatchClauseSyntax GenerateAlternationCatchClause(
        ExceptHandler handler,
        TypeTestLowering alternation,
        IReadOnlyList<SemanticType> alternatives,
        CatchFilterClauseSyntax? userFilter)
    {
        var baseName = LocalBaseName(handler.Name!, handler.IsNameBacktickEscaped);

        // Same versioning as the single-type bound handler below: nested catch clauses binding the
        // same name would otherwise collide (CS0136).
        var saved = CaptureSlot(baseName);
        var version = saved.Existed ? saved.Version + 1 : 0;
        SetSlotVersion(baseName, version, handler.Name!);
        var exceptionVar = saved.Existed ? $"{baseName}_{version}" : baseName;

        var catchBlock = GenerateSuiteBlock(handler.Body);

        RestoreSlot(baseName, saved);

        var alternationTest = alternatives
            .Select(alternative => (ExpressionSyntax)BinaryExpression(
                SyntaxKind.IsExpression, IdentifierName(exceptionVar), MapTypeTestTypeName(alternative)))
            .Aggregate((left, right) => BinaryExpression(SyntaxKind.LogicalOrExpression, left, right));

        var filter = userFilter == null
            ? CatchFilterClause(alternationTest)
            : CatchFilterClause(BinaryExpression(
                SyntaxKind.LogicalAndExpression,
                ParenthesizedExpression(alternationTest),
                ParenthesizedExpression(userFilter.FilterExpression)));

        return CatchClause(
            CatchDeclaration(MapTypeTestTypeName(alternation.TestType), Identifier(exceptionVar)),
            filter,
            catchBlock);
    }

    private CatchFilterClauseSyntax? GenerateCatchFilterClause(ExceptHandler handler)
    {
        if (handler.Filter == null)
            return null;

        var filterExpr = GenerateExpression(handler.Filter);
        return CatchFilterClause(filterExpr);
    }

    /// <summary>
    /// Builds the parser-shaped type <c>System.Collections.Generic.List&lt;System.Exception&gt;</c> used
    /// by except* lowering. <c>GenericName(Identifier("System.Collections.Generic.List"))</c> packs the
    /// dotted name into a single identifier token that prints correctly but fails to bind (CS1070)
    /// when the emitter tree is handed straight to <c>CSharpSyntaxTree.Create</c> (#1095). Building the
    /// qualified spine via <see cref="TypeSyntaxMapper.QualifiedGenericName(string, TypeSyntax[])"/>
    /// keeps the printed text identical, so snapshots stay byte-identical.
    /// </summary>
    private static NameSyntax SystemExceptionListType() =>
        TypeSyntaxMapper.QualifiedGenericName(
            "System.Collections.Generic.List", MakeGlobalQualifiedName("System", "Exception"));

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
            VariableDeclaration(SystemExceptionListType())
            .WithVariables(SingletonSeparatedList(
                VariableDeclarator(EscapedIdentifier(allMatchedVar))
                    .WithInitializer(EqualsValueClause(
                        ObjectCreationExpression(SystemExceptionListType())
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
                        VariableDeclarator(EscapedIdentifier(matchedVar))
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
                var baseName = LocalBaseName(handler.Name, handler.IsNameBacktickEscaped);

                var saved = CaptureSlot(baseName);
                var version = saved.Existed ? saved.Version + 1 : 0;
                SetSlotVersion(baseName, version, handler.Name);

                var asVar = saved.Existed ? $"{baseName}_{version}" : baseName;

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
                                        MakeGlobalQualifiedName("System", "Exception")))))),
                        IdentifierName("ToList")));

                var egCreation = ObjectCreationExpression(MakeGlobalQualifiedName("Sharpy", "ExceptionGroup"))
                    .WithArgumentList(ArgumentList(SeparatedList(new[]
                    {
                        Argument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(""))),
                        Argument(castCall)
                    })));

                handlerBodyStatements.Add(LocalDeclarationStatement(
                    VariableDeclaration(IdentifierName("var"))
                        .WithVariables(SingletonSeparatedList(
                            VariableDeclarator(EscapedIdentifier(asVar))
                                .WithInitializer(EqualsValueClause(egCreation))))));

                // Generate handler body statements
                handlerBodyStatements.AddRange(GenerateSuite(handler.Body));

                // Restore version state
                RestoreSlot(baseName, saved);
            }
            else
            {
                // No 'as' variable — just emit the handler body
                handlerBodyStatements.AddRange(GenerateSuite(handler.Body));
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
                            Parameter(EscapedIdentifier(whereParam)),
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
                    VariableDeclarator(EscapedIdentifier(unmatchedVar))
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
            ObjectCreationExpression(MakeGlobalQualifiedName("System", "AggregateException"))
                .WithArgumentList(ArgumentList(SingletonSeparatedList(
                    Argument(IdentifierName(unmatchedVar))))));

        catchBodyStatements.Add(IfStatement(unmatchedCondition, rethrowStmt));

        // Build the single catch clause: catch (System.AggregateException __eg_N) { ... }
        var catchDeclaration = CatchDeclaration(
            MakeGlobalQualifiedName("System", "AggregateException"),
            Identifier(egVar));

        result.Add(CatchClause(catchDeclaration, null, Block(catchBodyStatements)));

        return result;
    }

    private FinallyClauseSyntax? GenerateFinallyClause(ImmutableArray<Statement> finallyBody)
    {
        if (finallyBody.Length > 0)
        {
            var finallyBlock = GenerateSuiteBlock(finallyBody);
            return FinallyClause(finallyBlock);
        }
        return null;
    }
}
