using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Diagnostics;
using System.Linq;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// Validates generated C# code for common issues and correctness
/// </summary>
public class CodeValidator
{
    private readonly DiagnosticBag _diagnostics = new();

    /// <summary>
    /// Structured diagnostics from code validation.
    /// </summary>
    public DiagnosticBag Diagnostics => _diagnostics;

    /// <summary>
    /// Validate a syntax tree
    /// </summary>
    public bool Validate(SyntaxTree syntaxTree)
    {
        _diagnostics.Clear();

        // Check for syntax errors
        var diagnostics = syntaxTree.GetDiagnostics();
        foreach (var diagnostic in diagnostics)
        {
            var lineSpan = diagnostic.Location.GetLineSpan();
            int? line = lineSpan.IsValid ? lineSpan.StartLinePosition.Line + 1 : null;
            int? column = lineSpan.IsValid ? lineSpan.StartLinePosition.Character + 1 : null;
            var filePath = lineSpan.IsValid && !string.IsNullOrEmpty(lineSpan.Path)
                ? lineSpan.Path : null;

            if (diagnostic.Severity == DiagnosticSeverity.Error)
            {
                _diagnostics.AddError(
                    $"Syntax error at {diagnostic.Location.GetLineSpan()}: {diagnostic.GetMessage()}",
                    line, column, filePath, diagnostic.Id, CompilerPhase.CodeGeneration);
            }
            else if (diagnostic.Severity == DiagnosticSeverity.Warning)
            {
                _diagnostics.AddWarning(
                    $"Syntax warning at {diagnostic.Location.GetLineSpan()}: {diagnostic.GetMessage()}",
                    line, column, filePath, diagnostic.Id, CompilerPhase.CodeGeneration);
            }
        }

        // Validate structure
        var root = syntaxTree.GetRoot();
        ValidateNode(root);

        return !_diagnostics.HasErrors;
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
            var location = classDecl.GetLocation().GetLineSpan();
            _diagnostics.AddError("Class declaration has empty name",
                location.IsValid ? location.StartLinePosition.Line + 1 : null,
                location.IsValid ? location.StartLinePosition.Character + 1 : null,
                code: DiagnosticCodes.CodeGen.EmptyClassName,
                phase: CompilerPhase.CodeGeneration);
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
            var location = classDecl.GetLocation().GetLineSpan();
            _diagnostics.AddWarning($"Class {classDecl.Identifier.Text} has duplicate member: {duplicate}",
                location.IsValid ? location.StartLinePosition.Line + 1 : null,
                location.IsValid ? location.StartLinePosition.Character + 1 : null,
                code: DiagnosticCodes.CodeGen.DuplicateMember,
                phase: CompilerPhase.CodeGeneration);
        }
    }

    private void ValidateMethodDeclaration(MethodDeclarationSyntax methodDecl)
    {
        // Check for empty method name
        if (string.IsNullOrWhiteSpace(methodDecl.Identifier.Text))
        {
            var location = methodDecl.GetLocation().GetLineSpan();
            _diagnostics.AddError("Method declaration has empty name",
                location.IsValid ? location.StartLinePosition.Line + 1 : null,
                location.IsValid ? location.StartLinePosition.Character + 1 : null,
                code: DiagnosticCodes.CodeGen.EmptyMethodName,
                phase: CompilerPhase.CodeGeneration);
        }

        // Check for abstract method with body
        if (methodDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.AbstractKeyword)) &&
            methodDecl.Body != null)
        {
            var location = methodDecl.GetLocation().GetLineSpan();
            _diagnostics.AddError($"Abstract method {methodDecl.Identifier.Text} cannot have a body",
                location.IsValid ? location.StartLinePosition.Line + 1 : null,
                location.IsValid ? location.StartLinePosition.Character + 1 : null,
                code: DiagnosticCodes.CodeGen.AbstractMethodWithBody,
                phase: CompilerPhase.CodeGeneration);
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
                var location = methodDecl.GetLocation().GetLineSpan();
                _diagnostics.AddError($"Non-abstract method {methodDecl.Identifier.Text} must have a body",
                    location.IsValid ? location.StartLinePosition.Line + 1 : null,
                    location.IsValid ? location.StartLinePosition.Character + 1 : null,
                    code: DiagnosticCodes.CodeGen.NonAbstractMethodWithoutBody,
                    phase: CompilerPhase.CodeGeneration);
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
                var location = variable.GetLocation().GetLineSpan();
                _diagnostics.AddWarning($"Variable {variable.Identifier.Text} uses 'var' without initializer",
                    location.IsValid ? location.StartLinePosition.Line + 1 : null,
                    location.IsValid ? location.StartLinePosition.Character + 1 : null,
                    code: DiagnosticCodes.CodeGen.VarWithoutInitializer,
                    phase: CompilerPhase.CodeGeneration);
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
