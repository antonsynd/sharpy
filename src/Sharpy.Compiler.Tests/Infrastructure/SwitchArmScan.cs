using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Sharpy.Compiler.Tests.Infrastructure;

/// <summary>
/// Parses production C# source with Roslyn and extracts the set of type names
/// matched by case/arm patterns inside a named method's switch statements and
/// switch expressions. Consumed by totality tests so their rosters derive from
/// the dispatch they document (#1694).
/// </summary>
public static class SwitchArmScan
{
    /// <summary>
    /// Returns the set of type names matched by case patterns across every switch
    /// statement AND switch expression lexically inside every method with
    /// <paramref name="methodName"/> in the file at <paramref name="repoRelativePath"/>.
    /// Collects from DeclarationPattern, TypePattern, RecursivePattern (outer type),
    /// CasePatternSwitchLabel, and constant-pattern case labels that name a type.
    /// Throws if the file or method is not found or no switch exists.
    /// </summary>
    public static IReadOnlySet<string> CaseTypeNames(string repoRelativePath, string methodName)
    {
        var root = ParseFile(repoRelativePath);
        var methods = FindMethods(root, methodName, repoRelativePath);
        var typeNames = new HashSet<string>();
        bool foundSwitch = false;

        foreach (var method in methods)
        {
            foreach (var switchStmt in method.DescendantNodes().OfType<SwitchStatementSyntax>())
            {
                foundSwitch = true;
                foreach (var section in switchStmt.Sections)
                {
                    foreach (var label in section.Labels)
                    {
                        CollectTypeNamesFromSwitchLabel(label, typeNames);
                    }
                }
            }

            foreach (var switchExpr in method.DescendantNodes().OfType<SwitchExpressionSyntax>())
            {
                foundSwitch = true;
                foreach (var arm in switchExpr.Arms)
                {
                    CollectTypeNamesFromPattern(arm.Pattern, typeNames);
                }
            }
        }

        if (!foundSwitch)
        {
            throw new InvalidOperationException(
                $"No switch statement or expression found in method '{methodName}' " +
                $"in '{repoRelativePath}'.");
        }

        return typeNames;
    }

    /// <summary>
    /// Returns the set of type names matched by case patterns, scoped to methods
    /// inside the containing type whose metadata name matches <paramref name="containingTypeName"/>.
    /// Use arity-qualified names (e.g. "AstVisitor" vs "AstVisitor`1") to discriminate
    /// overloads in different generic classes sharing the same identifier.
    /// </summary>
    public static IReadOnlySet<string> CaseTypeNames(
        string repoRelativePath, string methodName, string containingTypeName)
    {
        var root = ParseFile(repoRelativePath);
        var methods = FindMethodsInType(root, methodName, containingTypeName, repoRelativePath);
        var typeNames = new HashSet<string>();
        bool foundSwitch = false;

        foreach (var method in methods)
        {
            foreach (var switchStmt in method.DescendantNodes().OfType<SwitchStatementSyntax>())
            {
                foundSwitch = true;
                foreach (var section in switchStmt.Sections)
                {
                    foreach (var label in section.Labels)
                    {
                        CollectTypeNamesFromSwitchLabel(label, typeNames);
                    }
                }
            }

            foreach (var switchExpr in method.DescendantNodes().OfType<SwitchExpressionSyntax>())
            {
                foundSwitch = true;
                foreach (var arm in switchExpr.Arms)
                {
                    CollectTypeNamesFromPattern(arm.Pattern, typeNames);
                }
            }
        }

        if (!foundSwitch)
        {
            throw new InvalidOperationException(
                $"No switch statement or expression found in method '{methodName}' " +
                $"in type '{containingTypeName}' in '{repoRelativePath}'.");
        }

        return typeNames;
    }

    /// <summary>
    /// Counts the switch statements and switch expressions inside the methods selected by
    /// (methodName, containingTypeName). Exists so self-tests can prove the containing-type
    /// filter actually discriminates: while the two AstVisitor.Visit overloads' arm ROSTERS
    /// coincide (94 == 94), a filter regression that silently merged the overloads is
    /// invisible to roster assertions — but the void overload holds exactly one switch
    /// STATEMENT and the generic overload exactly one switch EXPRESSION, so these counts
    /// tell the two scans apart (plan-e31e76 verify-round warning).
    /// </summary>
    public static (int SwitchStatements, int SwitchExpressions) DispatchFormCounts(
        string repoRelativePath, string methodName, string containingTypeName)
    {
        var root = ParseFile(repoRelativePath);
        var methods = FindMethodsInType(root, methodName, containingTypeName, repoRelativePath);
        var statements = methods.Sum(m => m.DescendantNodes().OfType<SwitchStatementSyntax>().Count());
        var expressions = methods.Sum(m => m.DescendantNodes().OfType<SwitchExpressionSyntax>().Count());
        return (statements, expressions);
    }

    /// <summary>
    /// Returns the whitespace-normalized pattern text for each arm of every switch
    /// expression AND every pattern case label of every switch statement in the named
    /// method. For tuple-pattern switches (e.g. AugmentedCollectionAssignment.Classify)
    /// and for refined-pattern arms that a type-name scan collapses (e.g. the six
    /// <c>BinaryOp { … }</c> arms of NarrowingConditionInterpreter — deleting one leaves
    /// the type name present, so only pattern-text pinning catches it).
    /// Discard arms (<c>_</c>) and <c>default:</c> labels: the discard is included
    /// (switch expressions), the default label is not a pattern and is skipped.
    /// </summary>
    public static IReadOnlyList<string> ArmPatternTexts(string repoRelativePath, string methodName)
    {
        var root = ParseFile(repoRelativePath);
        var methods = FindMethods(root, methodName, repoRelativePath);
        var texts = new List<string>();
        bool foundSwitch = false;

        foreach (var method in methods)
        {
            foreach (var switchExpr in method.DescendantNodes().OfType<SwitchExpressionSyntax>())
            {
                foundSwitch = true;
                foreach (var arm in switchExpr.Arms)
                {
                    var normalized = NormalizeWhitespace(arm.Pattern.ToString());
                    texts.Add(normalized);
                }
            }

            foreach (var switchStmt in method.DescendantNodes().OfType<SwitchStatementSyntax>())
            {
                foundSwitch = true;
                foreach (var section in switchStmt.Sections)
                {
                    foreach (var label in section.Labels.OfType<CasePatternSwitchLabelSyntax>())
                    {
                        texts.Add(NormalizeWhitespace(label.Pattern.ToString()));
                    }
                }
            }
        }

        if (!foundSwitch)
        {
            throw new InvalidOperationException(
                $"No switch expression or statement found in method '{methodName}' " +
                $"in '{repoRelativePath}'.");
        }

        return texts;
    }

    private static CompilationUnitSyntax ParseFile(string repoRelativePath)
    {
        var repoRoot = FindRepoRoot();
        var fullPath = Path.Combine(repoRoot, repoRelativePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                $"Source file not found: '{fullPath}' (repo root: '{repoRoot}').");
        }

        var source = File.ReadAllText(fullPath);
        var tree = CSharpSyntaxTree.ParseText(source);
        return tree.GetCompilationUnitRoot();
    }

    private static IReadOnlyList<MethodDeclarationSyntax> FindMethods(
        CompilationUnitSyntax root, string methodName, string repoRelativePath)
    {
        var methods = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Where(m => m.Identifier.Text == methodName)
            .ToList();

        if (methods.Count == 0)
        {
            throw new InvalidOperationException(
                $"Method '{methodName}' not found in '{repoRelativePath}'.");
        }

        return methods;
    }

    private static IReadOnlyList<MethodDeclarationSyntax> FindMethodsInType(
        CompilationUnitSyntax root, string methodName, string containingTypeName,
        string repoRelativePath)
    {
        var methods = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Where(m => m.Identifier.Text == methodName)
            .Where(m =>
            {
                var enclosing = m.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
                return enclosing != null && GetMetadataName(enclosing) == containingTypeName;
            })
            .ToList();

        if (methods.Count == 0)
        {
            throw new InvalidOperationException(
                $"Method '{methodName}' in type '{containingTypeName}' " +
                $"not found in '{repoRelativePath}'.");
        }

        return methods;
    }

    private static string GetMetadataName(TypeDeclarationSyntax type)
    {
        var name = type.Identifier.Text;
        if (type.TypeParameterList != null && type.TypeParameterList.Parameters.Count > 0)
            return $"{name}`{type.TypeParameterList.Parameters.Count}";
        return name;
    }

    private static void CollectTypeNamesFromSwitchLabel(SwitchLabelSyntax label, HashSet<string> typeNames)
    {
        switch (label)
        {
            case CasePatternSwitchLabelSyntax patternLabel:
                CollectTypeNamesFromPattern(patternLabel.Pattern, typeNames);
                break;
            case CaseSwitchLabelSyntax caseLabel:
                // case TypeName: — a constant label that names a type (rare, but handle it)
                if (caseLabel.Value is IdentifierNameSyntax id)
                    typeNames.Add(id.Identifier.Text);
                else if (caseLabel.Value is MemberAccessExpressionSyntax memberAccess)
                    typeNames.Add(memberAccess.Name.Identifier.Text);
                break;
        }
    }

    private static void CollectTypeNamesFromPattern(PatternSyntax pattern, HashSet<string> typeNames)
    {
        switch (pattern)
        {
            case DeclarationPatternSyntax decl:
                AddTypeName(decl.Type, typeNames);
                break;
            case TypePatternSyntax typePattern:
                AddTypeName(typePattern.Type, typeNames);
                break;
            case RecursivePatternSyntax recursive:
                if (recursive.Type != null)
                    AddTypeName(recursive.Type, typeNames);
                break;
            case BinaryPatternSyntax binary:
                CollectTypeNamesFromPattern(binary.Left, typeNames);
                CollectTypeNamesFromPattern(binary.Right, typeNames);
                break;
            case ParenthesizedPatternSyntax paren:
                CollectTypeNamesFromPattern(paren.Pattern, typeNames);
                break;
            case UnaryPatternSyntax unary:
                CollectTypeNamesFromPattern(unary.Pattern, typeNames);
                break;
            case DiscardPatternSyntax:
                // _ pattern — no type name
                break;
            case ConstantPatternSyntax constant:
                // In parse-only mode (no compilation), Roslyn may parse `case TypeName:` as a
                // ConstantPattern with an IdentifierNameSyntax rather than a TypePattern.
                if (constant.Expression is IdentifierNameSyntax constId)
                    typeNames.Add(constId.Identifier.Text);
                break;
            case RelationalPatternSyntax:
                // relational — no type name
                break;
            case VarPatternSyntax:
                // var — no type name
                break;
        }
    }

    private static void AddTypeName(TypeSyntax type, HashSet<string> typeNames)
    {
        switch (type)
        {
            case IdentifierNameSyntax id:
                typeNames.Add(id.Identifier.Text);
                break;
            case QualifiedNameSyntax qualified:
                typeNames.Add(qualified.Right.Identifier.Text);
                break;
            case GenericNameSyntax generic:
                typeNames.Add(generic.Identifier.Text);
                break;
        }
    }

    private static string NormalizeWhitespace(string text)
    {
        // Collapse all whitespace sequences to single spaces
        var chars = new List<char>();
        bool lastWasSpace = false;
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!lastWasSpace)
                {
                    chars.Add(' ');
                    lastWasSpace = true;
                }
            }
            else
            {
                chars.Add(c);
                lastWasSpace = false;
            }
        }
        return new string(chars.ToArray()).Trim();
    }

    private static string FindRepoRoot()
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current, ".git"))
                || File.Exists(Path.Combine(current, ".git")))
            {
                return current;
            }
            current = Directory.GetParent(current)?.FullName;
        }

        throw new InvalidOperationException(
            "Could not find repository root (no .git directory) " +
            $"starting from '{AppContext.BaseDirectory}'.");
    }
}
