using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// Validates generated C# code for common issues and correctness
/// </summary>
public class CodeValidator
{
    private readonly List<string> _errors = new();
    private readonly List<string> _warnings = new();

    /// <summary>
    /// Get all validation errors
    /// </summary>
    public IReadOnlyList<string> Errors => _errors;

    /// <summary>
    /// Get all validation warnings
    /// </summary>
    public IReadOnlyList<string> Warnings => _warnings;

    /// <summary>
    /// Validate a syntax tree
    /// </summary>
    public bool Validate(SyntaxTree syntaxTree)
    {
        _errors.Clear();
        _warnings.Clear();

        // Check for syntax errors
        var diagnostics = syntaxTree.GetDiagnostics();
        foreach (var diagnostic in diagnostics)
        {
            if (diagnostic.Severity == DiagnosticSeverity.Error)
            {
                _errors.Add($"Syntax error at {diagnostic.Location.GetLineSpan()}: {diagnostic.GetMessage()}");
            }
            else if (diagnostic.Severity == DiagnosticSeverity.Warning)
            {
                _warnings.Add($"Syntax warning at {diagnostic.Location.GetLineSpan()}: {diagnostic.GetMessage()}");
            }
        }

        // Validate structure
        var root = syntaxTree.GetRoot();
        ValidateNode(root);

        return _errors.Count == 0;
    }

    private void ValidateNode(SyntaxNode node)
    {
        // Check for common issues in specific node types
        switch (node)
        {
            case ClassDeclarationSyntax classDecl:
                ValidateClassDeclaration(classDecl);
                break;
            case MethodDeclarationSyntax methodDecl:
                ValidateMethodDeclaration(methodDecl);
                break;
            case VariableDeclarationSyntax varDecl:
                ValidateVariableDeclaration(varDecl);
                break;
        }

        // Recursively validate children
        foreach (var child in node.ChildNodes())
        {
            ValidateNode(child);
        }
    }

    private void ValidateClassDeclaration(ClassDeclarationSyntax classDecl)
    {
        // Check for empty class name
        if (string.IsNullOrWhiteSpace(classDecl.Identifier.Text))
        {
            _errors.Add("Class declaration has empty name");
        }

        // Check for duplicate non-method members (fields, properties)
        // Note: Methods can be overloaded in C#, so we only check non-method members
        var nonMethodMembers = classDecl.Members
            .Where(m => m is not MethodDeclarationSyntax)
            .Select(m => GetMemberName(m))
            .Where(name => name != null)
            .ToList();

        var duplicates = nonMethodMembers
            .GroupBy(name => name)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        foreach (var duplicate in duplicates)
        {
            _warnings.Add($"Class {classDecl.Identifier.Text} has duplicate member: {duplicate}");
        }
    }

    private void ValidateMethodDeclaration(MethodDeclarationSyntax methodDecl)
    {
        // Check for empty method name
        if (string.IsNullOrWhiteSpace(methodDecl.Identifier.Text))
        {
            _errors.Add("Method declaration has empty name");
        }

        // Check for abstract method with body
        if (methodDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.AbstractKeyword)) &&
            methodDecl.Body != null)
        {
            _errors.Add($"Abstract method {methodDecl.Identifier.Text} cannot have a body");
        }

        // Check for non-abstract method without body or expression
        if (!methodDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.AbstractKeyword)) &&
            methodDecl.Body == null &&
            methodDecl.ExpressionBody == null)
        {
            // Allow interface methods and partial methods to have no body
            var parent = methodDecl.Parent;
            if (parent is not InterfaceDeclarationSyntax &&
                !methodDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
            {
                _errors.Add($"Non-abstract method {methodDecl.Identifier.Text} must have a body");
            }
        }
    }

    private void ValidateVariableDeclaration(VariableDeclarationSyntax varDecl)
    {
        // Check for var without initializer
        if (varDecl.Type.IsVar)
        {
            foreach (var variable in varDecl.Variables.Where(v => v.Initializer == null))
            {
                _warnings.Add($"Variable {variable.Identifier.Text} uses 'var' without initializer");
            }
        }
    }

    private string? GetMemberName(MemberDeclarationSyntax member)
    {
        return member switch
        {
            MethodDeclarationSyntax method => method.Identifier.Text,
            PropertyDeclarationSyntax property => property.Identifier.Text,
            FieldDeclarationSyntax field => field.Declaration.Variables.FirstOrDefault()?.Identifier.Text,
            _ => null
        };
    }
}
