using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// Metadata describing a @test.fixture-decorated function and the C# class generated from it.
/// </summary>
internal sealed record FixtureInfo(
    string SharpyName,           // Sharpy function name, e.g. "db_connection"
    string ClassName,            // C# fixture class name, e.g. "DbConnectionFixture"
    TypeSyntax ValueType,        // Type of the .Value property (the fixture function's return type)
    string FieldName,            // Field name used in consuming test classes, e.g. "_dbConnectionFixture"
    bool IsDisposable,           // True for yield-based fixtures (teardown after yield)
    bool IsAsync = false);       // True for async def fixtures (implements Xunit.IAsyncLifetime)

/// <summary>
/// RoslynEmitter partial class: @test.fixture code generation.
///
/// A @test.fixture function is transformed into a public C# class:
/// - Parameterless constructor runs the setup code (statements before any yield).
/// - A public read-only Value property holds the value the fixture exposes
///   (the return expression for non-yield fixtures, or the yielded expression for yield-based).
/// - For yield-based fixtures, the class implements System.IDisposable and the teardown
///   code (statements after the yield) is emitted in Dispose().
///
/// Test methods (in user classes or the synthesized sibling test class) that take a parameter
/// whose name matches a fixture function name are rewired: the parameter is removed from the
/// method signature, the consuming class gains Xunit.IClassFixture&lt;XFixture&gt;, a private
/// field captures the injected fixture, and the test body is prefixed with
/// "var name = _nameFixture.Value;".
/// </summary>
internal partial class RoslynEmitter
{
    /// <summary>
    /// Returns true if the function is decorated with @test.fixture.
    /// </summary>
    private static bool IsTestFixtureFunction(FunctionDef func)
        => func.Decorators.Any(d =>
            !d.IsBracketAttribute && d.Name == DecoratorNames.TestFixture);

    /// <summary>
    /// Computes (and caches) FixtureInfo for a @test.fixture function. Called when a
    /// fixture is collected during module-member generation, so consuming tests can look it up.
    /// </summary>
    private FixtureInfo RegisterFixture(FunctionDef func)
    {
        if (_fixtureRegistry.TryGetValue(func.Name, out var existing))
            return existing;

        var className = NameMangler.ToPascalCase(func.Name) + "Fixture";
        TypeSyntax valueType = func.ReturnType != null
            ? _typeMapper.MapType(func.ReturnType)
            : PredefinedType(Token(SyntaxKind.ObjectKeyword));

        var fieldName = "_" + NameMangler.ToCamelCase(func.Name) + "Fixture";
        bool hasYield = FindFirstYield(func.Body) >= 0;

        var info = new FixtureInfo(func.Name, className, valueType, fieldName, hasYield, func.IsAsync);
        _fixtureRegistry[func.Name] = info;
        return info;
    }

    /// <summary>
    /// Returns the index of the first top-level YieldStatement in the body, or -1 if none.
    /// Does not descend into nested function/class definitions.
    /// </summary>
    private static int FindFirstYield(IReadOnlyList<Statement> body)
    {
        for (int i = 0; i < body.Count; i++)
        {
            if (body[i] is YieldStatement)
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Generates a C# class for the given @test.fixture function.
    /// - Non-yield fixtures: parameterless ctor runs the body (with `return expr` rewritten
    ///   to `Value = expr;`); the class is a plain POCO.
    /// - Yield-based fixtures: ctor runs statements before the yield plus `Value = yieldExpr;`;
    ///   Dispose() runs statements after the yield. The class implements System.IDisposable.
    /// </summary>
    private ClassDeclarationSyntax GenerateFixtureClass(FunctionDef func)
    {
        var info = RegisterFixture(func);

        // Set up emission scope as if we were generating a function body.
        ResetMethodScope(func);
        CollectSourceVariableNames(func.Body);

        int yieldIndex = FindFirstYield(func.Body);

        List<Statement> setupStmts;
        List<Statement> teardownStmts;
        Expression? exposedValueExpr;

        if (yieldIndex >= 0)
        {
            setupStmts = func.Body.Take(yieldIndex).ToList();
            teardownStmts = func.Body.Skip(yieldIndex + 1).ToList();
            exposedValueExpr = ((YieldStatement)func.Body[yieldIndex]).Value;
        }
        else
        {
            // Find the (last) return statement at top level. Use its expression as Value.
            int returnIndex = -1;
            for (int i = func.Body.Length - 1; i >= 0; i--)
            {
                if (func.Body[i] is ReturnStatement)
                { returnIndex = i; break; }
            }

            if (returnIndex >= 0)
            {
                setupStmts = func.Body.Take(returnIndex).ToList();
                exposedValueExpr = ((ReturnStatement)func.Body[returnIndex]).Value;
            }
            else
            {
                setupStmts = func.Body.ToList();
                exposedValueExpr = null;
            }
            teardownStmts = new List<Statement>();
        }

        // Async fixtures implement Xunit.IAsyncLifetime instead of a ctor + IDisposable.
        if (info.IsAsync)
        {
            return GenerateAsyncFixtureClass(func, info, setupStmts, teardownStmts, exposedValueExpr);
        }

        // Body of the constructor: setup statements + `Value = expr;` (if any).
        var ctorStmts = new List<StatementSyntax>();
        foreach (var stmt in setupStmts)
        {
            ctorStmts.AddRange(GenerateBodyStatements(stmt));
        }
        if (exposedValueExpr != null)
        {
            ctorStmts.Add(ExpressionStatement(AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                IdentifierName("Value"),
                GenerateExpression(exposedValueExpr))));
        }

        var members = new List<MemberDeclarationSyntax>();

        // For yield-based fixtures, capture teardown code in a lambda stored as a field.
        // The lambda is assigned at the end of the constructor so it captures all locals
        // from setup by reference, and Dispose() just invokes it.
        FieldDeclarationSyntax? teardownField = null;
        MethodDeclarationSyntax? disposeMethod = null;
        if (info.IsDisposable)
        {
            // private System.Action? _teardown;
            teardownField = FieldDeclaration(
                    VariableDeclaration(NullableType(MakeGlobalQualifiedName("System", "Action")))
                        .WithVariables(SingletonSeparatedList(VariableDeclarator("_teardown"))))
                .WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword)));

            var teardownBodyStmts = new List<StatementSyntax>();
            foreach (var stmt in teardownStmts)
            {
                teardownBodyStmts.AddRange(GenerateBodyStatements(stmt));
            }

            // _teardown = () => { ...teardown... };  (appended to ctor body)
            ctorStmts.Add(ExpressionStatement(AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                IdentifierName("_teardown"),
                ParenthesizedLambdaExpression()
                    .WithBlock(Block(teardownBodyStmts)))));

            // public void Dispose() { _teardown?.Invoke(); }
            disposeMethod = MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), "Dispose")
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                .WithBody(Block(ExpressionStatement(
                    ConditionalAccessExpression(
                        IdentifierName("_teardown"),
                        InvocationExpression(MemberBindingExpression(IdentifierName("Invoke")))))));
        }

        // public T Value { get; private set; }
        var valueProp = PropertyDeclaration(info.ValueType, "Value")
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
            .WithAccessorList(AccessorList(List(new[]
            {
                AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)),
                AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                    .WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword)))
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)),
            })));
        valueProp = valueProp
            .WithInitializer(EqualsValueClause(
                PostfixUnaryExpression(SyntaxKind.SuppressNullableWarningExpression,
                    LiteralExpression(SyntaxKind.DefaultLiteralExpression, Token(SyntaxKind.DefaultKeyword)))))
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
        members.Add(valueProp);

        if (teardownField != null)
            members.Add(teardownField);

        // public XFixture() { ...setup...; Value = expr; [_teardown = () => {...}] }
        var ctor = ConstructorDeclaration(info.ClassName)
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
            .WithBody(Block(ctorStmts));
        members.Add(ctor);

        if (disposeMethod != null)
            members.Add(disposeMethod);

        var classDecl = ClassDeclaration(info.ClassName)
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
            .WithMembers(List(members));

        if (info.IsDisposable)
        {
            classDecl = classDecl.WithBaseList(BaseList(SingletonSeparatedList<BaseTypeSyntax>(
                SimpleBaseType(MakeGlobalQualifiedName("System", "IDisposable")))));
        }

        if (!string.IsNullOrEmpty(func.DocString))
        {
            classDecl = classDecl.WithLeadingTrivia(GenerateXmlDocComment(func.DocString));
        }

        return classDecl;
    }

    /// <summary>
    /// The built-in <c>tmp_path</c> per-test fixture descriptor. Unlike user fixtures it is
    /// not an <c>IClassFixture&lt;T&gt;</c> (which would be shared across the class); the test
    /// class instead holds a fresh <c>global::Sharpy.TmpPathFixture</c> per test method and
    /// disposes it after each (giving pytest's per-test lifecycle).
    /// </summary>
    private static readonly FixtureInfo BuiltinTmpPathFixture = new FixtureInfo(
        SharpyName: PythonNames.TmpPath,
        ClassName: "global::Sharpy.TmpPathFixture",
        ValueType: PredefinedType(Token(SyntaxKind.StringKeyword)),
        FieldName: "_tmpPathFixture",
        IsDisposable: false);

    /// <summary>
    /// Returns the parameters of a test function that bind to a built-in per-test fixture
    /// (currently only <c>tmp_path</c>). A user-defined <c>@test.fixture</c> of the same name
    /// takes precedence (the registry is consulted first), so <c>tmp_path</c> is treated as
    /// built-in only when no user fixture shadows it.
    /// </summary>
    private List<(Parameter Parameter, FixtureInfo Fixture)> GetConsumedBuiltinFixtures(FunctionDef func)
    {
        var result = new List<(Parameter, FixtureInfo)>();
        if (_fixtureRegistry.ContainsKey(PythonNames.TmpPath))
            return result;  // user fixture named tmp_path wins

        foreach (var p in func.Parameters)
        {
            if (string.Equals(p.Name, PythonNames.Self, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(p.Name, PythonNames.Cls, System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(p.Name, PythonNames.TmpPath, System.StringComparison.Ordinal))
            {
                result.Add((p, BuiltinTmpPathFixture));
            }
        }
        return result;
    }

    /// <summary>
    /// For a test function, returns the list of parameters whose names match a registered
    /// fixture. These parameters are stripped from the emitted method signature and
    /// replaced by a leading `var name = _fixtureField.Value;` statement.
    /// </summary>
    private List<(Parameter Parameter, FixtureInfo Fixture)> GetConsumedFixtures(FunctionDef func)
    {
        var result = new List<(Parameter, FixtureInfo)>();
        if (_fixtureRegistry.Count == 0)
            return result;

        foreach (var p in func.Parameters)
        {
            // Skip self/cls — they're not fixture parameters.
            if (string.Equals(p.Name, PythonNames.Self, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(p.Name, PythonNames.Cls, System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (_fixtureRegistry.TryGetValue(p.Name, out var fixture))
            {
                result.Add((p, fixture));
            }
        }
        return result;
    }

    /// <summary>
    /// Generates the prelude statements injecting fixture values into a test method body.
    /// For each consumed fixture, emits: <c>T name = _nameFixture.Value;</c>
    /// </summary>
    private IEnumerable<StatementSyntax> GenerateFixturePrelude(
        IReadOnlyList<(Parameter Parameter, FixtureInfo Fixture)> consumed)
    {
        foreach (var (parameter, fixture) in consumed)
        {
            var localName = NameMangler.ToCamelCase(parameter.Name);
            yield return LocalDeclarationStatement(
                VariableDeclaration(fixture.ValueType)
                    .WithVariables(SingletonSeparatedList(
                        VariableDeclarator(Identifier(localName))
                            .WithInitializer(EqualsValueClause(
                                MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    IdentifierName(fixture.FieldName),
                                    IdentifierName("Value")))))));
        }
    }

    /// <summary>
    /// Generates a C# class implementing <c>Xunit.IAsyncLifetime</c> for an async
    /// <c>@test.fixture</c> function. Setup (including awaits) and <c>Value = expr;</c> move into
    /// <c>InitializeAsync</c>; yield-based teardown runs in <c>DisposeAsync</c> via an async
    /// teardown lambda. Return-based async fixtures emit a <c>DisposeAsync</c> that returns
    /// <c>Task.CompletedTask</c>. Consuming test classes are unchanged — xUnit drives
    /// <c>IAsyncLifetime</c> automatically through <c>IClassFixture&lt;T&gt;</c>.
    /// </summary>
    private ClassDeclarationSyntax GenerateAsyncFixtureClass(
        FunctionDef func,
        FixtureInfo info,
        List<Statement> setupStmts,
        List<Statement> teardownStmts,
        Expression? exposedValueExpr)
    {
        var members = new List<MemberDeclarationSyntax>();

        // public T Value { get; private set; } = default!;  (identical to the sync path)
        var valueProp = PropertyDeclaration(info.ValueType, "Value")
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
            .WithAccessorList(AccessorList(List(new[]
            {
                AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)),
                AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                    .WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword)))
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)),
            })))
            .WithInitializer(EqualsValueClause(
                PostfixUnaryExpression(SyntaxKind.SuppressNullableWarningExpression,
                    LiteralExpression(SyntaxKind.DefaultLiteralExpression, Token(SyntaxKind.DefaultKeyword)))))
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
        members.Add(valueProp);

        // For yield-based fixtures: private System.Func<System.Threading.Tasks.Task>? _teardown;
        if (info.IsDisposable)
        {
            members.Add(FieldDeclaration(
                    VariableDeclaration(NullableType(FuncOfTaskType()))
                        .WithVariables(SingletonSeparatedList(VariableDeclarator("_teardown"))))
                .WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword))));
        }

        // Build InitializeAsync body under async scope so awaits lower correctly.
        var initStmts = new List<StatementSyntax>();
        using (SetAsyncScope(true))
        {
            foreach (var stmt in setupStmts)
                initStmts.AddRange(GenerateBodyStatements(stmt));

            if (exposedValueExpr != null)
            {
                initStmts.Add(ExpressionStatement(AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    IdentifierName("Value"),
                    GenerateExpression(exposedValueExpr))));
            }

            if (info.IsDisposable)
            {
                // _teardown = async () => { ...teardown... };
                var teardownBodyStmts = new List<StatementSyntax>();
                foreach (var stmt in teardownStmts)
                    teardownBodyStmts.AddRange(GenerateBodyStatements(stmt));

                // CS1998 guard for the async teardown lambda when its body has no await.
                if (!HasTopLevelAwait(teardownBodyStmts))
                    teardownBodyStmts.Add(ExpressionStatement(AwaitExpression(TaskCompletedTask())));

                initStmts.Add(ExpressionStatement(AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    IdentifierName("_teardown"),
                    ParenthesizedLambdaExpression()
                        .WithAsyncKeyword(Token(SyntaxKind.AsyncKeyword))
                        .WithBlock(Block(teardownBodyStmts)))));
            }
        }

        // Guard against CS1998 ("async method lacks await") when no top-level await was
        // emitted into InitializeAsync (awaits nested inside the teardown lambda don't count).
        if (!HasTopLevelAwait(initStmts))
        {
            initStmts.Add(ExpressionStatement(AwaitExpression(TaskCompletedTask())));
        }

        // public async System.Threading.Tasks.Task InitializeAsync() { ... }
        members.Add(MethodDeclaration(TaskType(), "InitializeAsync")
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.AsyncKeyword)))
            .WithBody(Block(initStmts)));

        // DisposeAsync
        if (info.IsDisposable)
        {
            // public async System.Threading.Tasks.Task DisposeAsync()
            // { if (_teardown != null) { await _teardown(); } }
            var disposeBody = Block(IfStatement(
                BinaryExpression(SyntaxKind.NotEqualsExpression,
                    IdentifierName("_teardown"),
                    LiteralExpression(SyntaxKind.NullLiteralExpression)),
                Block(ExpressionStatement(AwaitExpression(
                    InvocationExpression(IdentifierName("_teardown")))))));

            members.Add(MethodDeclaration(TaskType(), "DisposeAsync")
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.AsyncKeyword)))
                .WithBody(disposeBody));
        }
        else
        {
            // public System.Threading.Tasks.Task DisposeAsync()
            // { return System.Threading.Tasks.Task.CompletedTask; }
            members.Add(MethodDeclaration(TaskType(), "DisposeAsync")
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                .WithBody(Block(ReturnStatement(TaskCompletedTask()))));
        }

        var classDecl = ClassDeclaration(info.ClassName)
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
            .WithBaseList(BaseList(SingletonSeparatedList<BaseTypeSyntax>(
                SimpleBaseType(ParseName("Xunit.IAsyncLifetime")))))
            .WithMembers(List(members));

        if (!string.IsNullOrEmpty(func.DocString))
        {
            classDecl = classDecl.WithLeadingTrivia(GenerateXmlDocComment(func.DocString));
        }

        return classDecl;
    }

    /// <summary>global::System.Func&lt;System.Threading.Tasks.Task&gt;</summary>
    private static NameSyntax FuncOfTaskType()
        => QualifiedName(
            AliasQualifiedName(IdentifierName(Token(SyntaxKind.GlobalKeyword)), IdentifierName("System")),
            GenericName(Identifier("Func"))
                .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList<TypeSyntax>(TaskType()))));

    /// <summary>System.Threading.Tasks.Task.CompletedTask</summary>
    private static ExpressionSyntax TaskCompletedTask()
        => MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            MakeGlobalQualifiedName("System", "Threading", "Tasks", "Task"),
            IdentifierName("CompletedTask"));

    /// <summary>
    /// True if any of the given statements contains an await expression at the method's own
    /// level — i.e. not nested inside a lambda or local function (whose awaits belong to a
    /// different async context).
    /// </summary>
    private static bool HasTopLevelAwait(IEnumerable<StatementSyntax> statements)
    {
        foreach (var stmt in statements)
        {
            foreach (var node in stmt.DescendantNodesAndSelf(descendIntoChildren: n =>
                n is not AnonymousFunctionExpressionSyntax && n is not LocalFunctionStatementSyntax))
            {
                if (node is AwaitExpressionSyntax)
                    return true;
            }
        }
        return false;
    }
}
