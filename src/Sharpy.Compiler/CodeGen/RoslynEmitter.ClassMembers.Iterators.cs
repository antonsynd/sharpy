using System.Collections.Immutable;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Shared;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// RoslynEmitter partial class: Iterator protocol generation
/// </summary>
internal partial class RoslynEmitter
{
    /// <summary>
    /// Generates iterator protocol members for a class defining __next__.
    /// Produces: private _current field, private NextImpl() method,
    /// public MoveNext(), Current property, Reset(), Dispose(),
    /// and explicit IEnumerator.Current.
    /// </summary>
    private List<MemberDeclarationSyntax> GenerateIteratorProtocolMembers(FunctionDef funcDef)
    {
        var members = new List<MemberDeclarationSyntax>();

        // Determine element type T from __next__ return type
        TypeSyntax elementType = funcDef.ReturnType != null
            ? _typeMapper.MapType(funcDef.ReturnType)
            : PredefinedType(Token(SyntaxKind.ObjectKeyword));

        // 1. Private _current field of type T
        members.Add(FieldDeclaration(
            VariableDeclaration(elementType)
                .WithVariables(SingletonSeparatedList(
                    VariableDeclarator(Identifier("_current")))))
            .WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword))));

        // 2. Private NextImpl() method containing the user's __next__ body
        {
            ResetMethodScope();
            CollectSourceVariableNames(funcDef.Body);

            // Track parameters (skip self)
            foreach (var param in funcDef.Parameters)
            {
                if (string.Equals(param.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase))
                    continue;
                var paramName = NameMangler.Transform(param.Name, NameContext.Parameter);
                _declaredVariables.Add(paramName);
                var baseName = NameMangler.ToCamelCase(param.Name);
                _variableVersions[baseName] = 0;
            }

            var body = Block(funcDef.Body.SelectMany(GenerateBodyStatements));

            var nextImpl = MethodDeclaration(elementType, "NextImpl")
                .WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword)))
                .WithBody(body);

            members.Add(nextImpl);
        }

        // 3. public bool MoveNext() — wraps NextImpl in try/catch(StopIteration)
        {
            // try { _current = NextImpl(); return true; }
            var tryStatements = new StatementSyntax[]
            {
                ExpressionStatement(
                    AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        IdentifierName("_current"),
                        InvocationExpression(IdentifierName("NextImpl")))),
                ReturnStatement(LiteralExpression(SyntaxKind.TrueLiteralExpression))
            };

            // catch (Sharpy.StopIteration) { return false; }
            var catchClause = CatchClause()
                .WithDeclaration(CatchDeclaration(
                    QualifiedName(IdentifierName("Sharpy"), IdentifierName("StopIteration"))))
                .WithBlock(Block(ReturnStatement(LiteralExpression(SyntaxKind.FalseLiteralExpression))));

            var tryStatement = TryStatement()
                .WithBlock(Block(tryStatements))
                .WithCatches(SingletonList(catchClause));

            var moveNext = MethodDeclaration(
                    PredefinedType(Token(SyntaxKind.BoolKeyword)), "MoveNext")
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                .WithBody(Block(tryStatement));

            members.Add(moveNext);
        }

        // 4. public T Current => _current; (needed for foreach duck-typing)
        members.Add(PropertyDeclaration(elementType, Identifier("Current"))
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
            .WithExpressionBody(ArrowExpressionClause(IdentifierName("_current")))
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));

        // 5. object System.Collections.IEnumerator.Current => _current;
        members.Add(PropertyDeclaration(
                PredefinedType(Token(SyntaxKind.ObjectKeyword)), "Current")
            .WithExplicitInterfaceSpecifier(ExplicitInterfaceSpecifier(
                QualifiedName(
                    QualifiedName(IdentifierName("System"), IdentifierName("Collections")),
                    IdentifierName("IEnumerator"))))
            .WithExpressionBody(ArrowExpressionClause(IdentifierName("_current")))
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));

        // 6. public void Reset() => throw new System.NotSupportedException();
        members.Add(MethodDeclaration(
                PredefinedType(Token(SyntaxKind.VoidKeyword)), "Reset")
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
            .WithExpressionBody(ArrowExpressionClause(
                ThrowExpression(
                    ObjectCreationExpression(ParseTypeName("System.NotSupportedException"))
                        .WithArgumentList(ArgumentList()))))
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));

        // 7. public void Dispose() { }
        members.Add(MethodDeclaration(
                PredefinedType(Token(SyntaxKind.VoidKeyword)), "Dispose")
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
            .WithBody(Block()));

        return members;
    }

    /// <summary>
    /// Generates GetReverseEnumerator() for __reversed__ with correct
    /// IEnumerator&lt;T&gt; return type to satisfy IReverseEnumerable&lt;T&gt;.
    /// </summary>
    private MethodDeclarationSyntax GenerateReverseEnumeratorMethod(FunctionDef funcDef)
    {
        // Element type T from __reversed__ return type annotation (defaults to object if absent)
        TypeSyntax elementType = _typeMapper.MapType(funcDef.ReturnType);

        var returnType = WrapInIEnumerator(elementType);

        // Set up method scope — same pattern as GenerateClassMethod
        ResetMethodScope();
        CollectSourceVariableNames(funcDef.Body);

        // Track parameters (skip self) — same as GenerateClassMethod
        foreach (var param in funcDef.Parameters)
        {
            if (string.Equals(param.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase))
                continue;
            var paramName = NameMangler.Transform(param.Name, NameContext.Parameter);
            _declaredVariables.Add(paramName);
            var baseName = NameMangler.ToCamelCase(param.Name);
            _variableVersions[baseName] = 0;
        }

        // Generate body from user's __reversed__ implementation
        var body = Block(funcDef.Body.SelectMany(GenerateBodyStatements));

        // Build parameter list — skip self
        var parameters = funcDef.Parameters
            .Where(p => !string.Equals(p.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase))
            .Select(GenerateParameter)
            .ToArray();

        var method = MethodDeclaration(returnType, "GetReverseEnumerator")
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
            .WithParameterList(ParameterList(SeparatedList(parameters)))
            .WithBody(body);

        // Add XML documentation from docstring if present
        if (!string.IsNullOrEmpty(funcDef.DocString))
        {
            method = method.WithLeadingTrivia(GenerateXmlDocComment(funcDef.DocString));
        }

        return method;
    }

    /// <summary>
    /// Generates IEnumerator&lt;T&gt; GetEnumerator() with the user's generator body
    /// plus the non-generic IEnumerable.GetEnumerator() bridge for generator __iter__.
    /// </summary>
    private List<MemberDeclarationSyntax> GenerateGeneratorIterMethod(FunctionDef funcDef)
    {
        var members = new List<MemberDeclarationSyntax>();

        // Element type from __iter__'s return type annotation (defaults to object if absent)
        TypeSyntax elementType = funcDef.ReturnType != null
            ? _typeMapper.MapType(funcDef.ReturnType)
            : PredefinedType(Token(SyntaxKind.ObjectKeyword));

        var returnType = WrapInIEnumerator(elementType);

        // Set up method scope
        ResetMethodScope();
        CollectSourceVariableNames(funcDef.Body);

        // Track parameters (skip self)
        foreach (var param in funcDef.Parameters)
        {
            if (string.Equals(param.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase))
                continue;
            var paramName = NameMangler.Transform(param.Name, NameContext.Parameter);
            _declaredVariables.Add(paramName);
            var baseName = NameMangler.ToCamelCase(param.Name);
            _variableVersions[baseName] = 0;
        }

        // Set generator and async flags so yield statements and bare returns emit correctly
        using var _gen = SetGeneratorScope(true);
        using var _asyncIter = SetAsyncScope(funcDef.IsAsync);

        var body = Block(funcDef.Body.SelectMany(GenerateBodyStatements));

        var parameters = funcDef.Parameters
            .Where(p => !string.Equals(p.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase))
            .Select(GenerateParameter)
            .ToArray();

        var method = MethodDeclaration(returnType, "GetEnumerator")
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
            .WithParameterList(ParameterList(SeparatedList(parameters)))
            .WithBody(body);

        if (!string.IsNullOrEmpty(funcDef.DocString))
        {
            method = method.WithLeadingTrivia(GenerateXmlDocComment(funcDef.DocString));
        }

        members.Add(method);

        // Non-generic IEnumerable.GetEnumerator() bridge
        members.Add(GenerateNonGenericGetEnumeratorBridge());

        return members;
    }

    /// <summary>
    /// Generates IEnumerable bridge members for a self-iterating class
    /// (one that defines both __iter__ and __next__).
    /// Produces: GetEnumerator() => this, IEnumerable.GetEnumerator() bridge.
    /// </summary>
    private List<MemberDeclarationSyntax> GenerateEnumerableBridgeMembers(TypeSyntax elementType)
    {
        var members = new List<MemberDeclarationSyntax>();

        // public IEnumerator<T> GetEnumerator() => this;
        var returnType = QualifiedName(
            QualifiedName(
                QualifiedName(IdentifierName("System"), IdentifierName("Collections")),
                IdentifierName("Generic")),
            GenericName("IEnumerator")
                .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(elementType))));

        members.Add(MethodDeclaration(returnType, "GetEnumerator")
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
            .WithExpressionBody(ArrowExpressionClause(ThisExpression()))
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));

        // IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        members.Add(GenerateNonGenericGetEnumeratorBridge());

        return members;
    }

    /// <summary>
    /// Generates the non-generic IEnumerable.GetEnumerator() explicit interface bridge.
    /// </summary>
    private MethodDeclarationSyntax GenerateNonGenericGetEnumeratorBridge()
    {
        return MethodDeclaration(
                QualifiedName(
                    QualifiedName(IdentifierName("System"), IdentifierName("Collections")),
                    IdentifierName("IEnumerator")),
                "GetEnumerator")
            .WithExplicitInterfaceSpecifier(ExplicitInterfaceSpecifier(
                QualifiedName(
                    QualifiedName(IdentifierName("System"), IdentifierName("Collections")),
                    IdentifierName("IEnumerable"))))
            .WithExpressionBody(ArrowExpressionClause(
                InvocationExpression(IdentifierName("GetEnumerator"))))
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
    }

}
