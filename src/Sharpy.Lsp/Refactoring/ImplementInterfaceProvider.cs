using System.Collections.Immutable;
using System.Text;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;
using SCG = System.Collections.Generic;

namespace Sharpy.Lsp.Refactoring;

/// <summary>
/// Provides "Implement Interface" code actions for classes and structs.
/// When the cursor is on a ClassDef or StructDef that implements interfaces with unimplemented
/// members, offers to generate stub implementations for the missing methods and properties.
/// </summary>
internal sealed class ImplementInterfaceProvider : ICodeActionProvider
{
    public Task<IReadOnlyList<CodeAction>> GetCodeActionsAsync(
        CodeActionProviderContext context,
        CancellationToken cancellationToken)
    {
        var analysis = context.Analysis;
        if (analysis?.Ast is null || analysis.SymbolTable is null || context.SourceText is null)
            return Task.FromResult<IReadOnlyList<CodeAction>>(Array.Empty<CodeAction>());

        var (line, col) = PositionConverter.ToCompiler(context.Range.Start);

        // Find the ClassDef or StructDef at the cursor position
        var containingClass = SelectionAnalyzer.FindContainingClass(analysis.Ast, line, col);
        if (containingClass is null)
            return Task.FromResult<IReadOnlyList<CodeAction>>(Array.Empty<CodeAction>());

        var (className, classBody, classLineEnd) = containingClass switch
        {
            ClassDef cd => (cd.Name, cd.Body, cd.LineEnd),
            StructDef sd => (sd.Name, sd.Body, sd.LineEnd),
            _ => (null, default, 0)
        };

        if (className is null)
            return Task.FromResult<IReadOnlyList<CodeAction>>(Array.Empty<CodeAction>());

        // Look up the TypeSymbol via the SymbolTable
        var typeSymbol = analysis.SymbolTable.LookupType(className);
        if (typeSymbol is null || typeSymbol.Interfaces.Count == 0)
            return Task.FromResult<IReadOnlyList<CodeAction>>(Array.Empty<CodeAction>());

        // Collect already-implemented method and property names from the class body
        var implementedMethods = CollectImplementedMethods(classBody);
        var implementedProperties = CollectImplementedProperties(classBody);

        // Check each interface for missing members
        var actions = new SCG.List<CodeAction>();

        foreach (var ifaceRef in typeSymbol.Interfaces)
        {
            var ifaceDef = ifaceRef.Definition;
            if (ifaceDef is null)
                continue;

            var missingMethods = GetMissingMethods(ifaceDef, implementedMethods);
            var missingProperties = GetMissingProperties(ifaceDef, implementedProperties);

            if (missingMethods.Count == 0 && missingProperties.Count == 0)
                continue;

            var action = CreateImplementInterfaceAction(
                context,
                ifaceDef.Name,
                missingMethods,
                missingProperties,
                classLineEnd,
                classBody);

            if (action is not null)
                actions.Add(action);
        }

        // Also offer a single "Implement all interfaces" action when multiple interfaces have missing members
        if (actions.Count > 1)
        {
            var allMissingMethods = new SCG.List<FunctionSymbol>();
            var allMissingProperties = new SCG.List<PropertySymbol>();

            foreach (var ifaceRef in typeSymbol.Interfaces)
            {
                var ifaceDef = ifaceRef.Definition;
                if (ifaceDef is null)
                    continue;

                allMissingMethods.AddRange(GetMissingMethods(ifaceDef, implementedMethods));
                allMissingProperties.AddRange(GetMissingProperties(ifaceDef, implementedProperties));
            }

            if (allMissingMethods.Count > 0 || allMissingProperties.Count > 0)
            {
                // Deduplicate by name (in case multiple interfaces share a member)
                var deduplicatedMethods = DeduplicateMethods(allMissingMethods);
                var deduplicatedProperties = DeduplicateProperties(allMissingProperties);

                var allAction = CreateImplementInterfaceAction(
                    context,
                    null, // null signals "all interfaces"
                    deduplicatedMethods,
                    deduplicatedProperties,
                    classLineEnd,
                    classBody);

                if (allAction is not null)
                    actions.Insert(0, allAction);
            }
        }

        return Task.FromResult<IReadOnlyList<CodeAction>>(actions);
    }

    /// <summary>
    /// Collects the names of methods already defined in the class body.
    /// </summary>
    private static HashSet<string> CollectImplementedMethods(ImmutableArray<Statement> body)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var stmt in body)
        {
            if (stmt is FunctionDef fd)
                names.Add(fd.Name);
        }
        return names;
    }

    /// <summary>
    /// Collects the names of properties already defined in the class body.
    /// </summary>
    private static HashSet<string> CollectImplementedProperties(ImmutableArray<Statement> body)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var stmt in body)
        {
            if (stmt is PropertyDef pd)
                names.Add(pd.Name);
        }
        return names;
    }

    /// <summary>
    /// Returns interface methods not yet implemented by the class.
    /// Excludes static methods and constructors.
    /// </summary>
    private static SCG.List<FunctionSymbol> GetMissingMethods(
        TypeSymbol interfaceDef,
        HashSet<string> implementedMethods)
    {
        var missing = new SCG.List<FunctionSymbol>();
        foreach (var method in interfaceDef.Methods)
        {
            if (method.IsStatic)
                continue;

            // Skip "self" parameter-only methods that are just markers
            if (implementedMethods.Contains(method.Name))
                continue;

            missing.Add(method);
        }
        return missing;
    }

    /// <summary>
    /// Returns interface properties not yet implemented by the class.
    /// </summary>
    private static SCG.List<PropertySymbol> GetMissingProperties(
        TypeSymbol interfaceDef,
        HashSet<string> implementedProperties)
    {
        var missing = new SCG.List<PropertySymbol>();
        foreach (var prop in interfaceDef.Properties)
        {
            if (prop.IsStatic)
                continue;

            if (implementedProperties.Contains(prop.Name))
                continue;

            missing.Add(prop);
        }
        return missing;
    }

    /// <summary>
    /// Deduplicates methods by name, keeping the first occurrence.
    /// This handles the case where multiple interfaces declare the same method.
    /// </summary>
    private static SCG.List<FunctionSymbol> DeduplicateMethods(SCG.List<FunctionSymbol> methods)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new SCG.List<FunctionSymbol>();
        foreach (var method in methods)
        {
            if (seen.Add(method.Name))
                result.Add(method);
        }
        return result;
    }

    /// <summary>
    /// Deduplicates properties by name, keeping the first occurrence.
    /// </summary>
    private static SCG.List<PropertySymbol> DeduplicateProperties(SCG.List<PropertySymbol> properties)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new SCG.List<PropertySymbol>();
        foreach (var prop in properties)
        {
            if (seen.Add(prop.Name))
                result.Add(prop);
        }
        return result;
    }

    /// <summary>
    /// Creates a code action that inserts stub implementations for missing interface members.
    /// </summary>
    /// <param name="context">The code action context.</param>
    /// <param name="interfaceName">
    /// The interface name for the action title, or null to generate an "implement all interfaces" action.
    /// </param>
    /// <param name="missingMethods">Methods to generate stubs for.</param>
    /// <param name="missingProperties">Properties to generate stubs for.</param>
    /// <param name="classLineEnd">The 1-based line number of the class closing (end of class body).</param>
    /// <param name="classBody">The class body statements, used to determine insertion point.</param>
    /// <returns>A code action, or null if no stubs would be generated.</returns>
    private static CodeAction? CreateImplementInterfaceAction(
        CodeActionProviderContext context,
        string? interfaceName,
        SCG.List<FunctionSymbol> missingMethods,
        SCG.List<PropertySymbol> missingProperties,
        int classLineEnd,
        ImmutableArray<Statement> classBody)
    {
        if (missingMethods.Count == 0 && missingProperties.Count == 0)
            return null;

        var sourceText = context.SourceText!;

        // Determine the indentation level (1 level inside the class)
        var classBodyIndentLevel = DetectClassBodyIndentLevel(sourceText, classBody);

        var stubText = GenerateStubs(missingMethods, missingProperties, classBodyIndentLevel);
        if (string.IsNullOrEmpty(stubText))
            return null;

        // Insert at the end of the class body (before the closing line).
        // The last statement in the body tells us where existing content ends.
        // We insert after the last body statement, or at the start of the class body if empty.
        var insertLine = DetermineInsertionLine(classBody, classLineEnd);

        // Convert to LSP 0-based line
        var lspInsertLine = insertLine - 1;

        // We want to insert at the beginning of the insert line (which is the line
        // just before the class ends)
        var insertPosition = new Position(lspInsertLine, 0);
        var insertRange = new LspRange(insertPosition, insertPosition);

        // Add a leading blank line to separate from existing content
        var textToInsert = "\n" + stubText + "\n";

        var edit = new WorkspaceEdit
        {
            Changes = new Dictionary<OmniSharp.Extensions.LanguageServer.Protocol.DocumentUri, IEnumerable<TextEdit>>
            {
                [context.DocumentUri] = new[]
                {
                    new TextEdit
                    {
                        Range = insertRange,
                        NewText = textToInsert
                    }
                }
            }
        };

        var title = interfaceName is not null
            ? $"Implement interface '{interfaceName}'"
            : "Implement all interfaces";

        return new CodeAction
        {
            Title = title,
            Kind = CodeActionKind.Refactor,
            Edit = edit
        };
    }

    /// <summary>
    /// Detects the indentation level of the class body by examining the first body statement.
    /// Falls back to 1 (4 spaces) if no body statements exist.
    /// </summary>
    private static int DetectClassBodyIndentLevel(string sourceText, ImmutableArray<Statement> classBody)
    {
        if (classBody.Length == 0)
            return 1;

        // Use the first body statement's line to detect indentation
        var firstStmt = classBody[0];
        if (firstStmt.LineStart <= 0)
            return 1;

        var indentation = SharplySourceGenerator.GetIndentation(sourceText, firstStmt.LineStart - 1);
        var indentUnit = SharplySourceGenerator.GetIndentUnit(sourceText);

        if (indentUnit.Length == 0)
            return 1;

        return indentation.Length / indentUnit.Length;
    }

    /// <summary>
    /// Determines the 1-based line number where stubs should be inserted.
    /// Inserts after the last statement in the class body, or at classLineEnd if the body is empty.
    /// </summary>
    private static int DetermineInsertionLine(ImmutableArray<Statement> classBody, int classLineEnd)
    {
        if (classBody.Length == 0)
            return classLineEnd;

        // Insert after the last statement
        var lastStmt = classBody[^1];
        return lastStmt.LineEnd + 1;
    }

    /// <summary>
    /// Generates Sharpy source stubs for missing methods and properties.
    /// </summary>
    private static string GenerateStubs(
        SCG.List<FunctionSymbol> missingMethods,
        SCG.List<PropertySymbol> missingProperties,
        int indentLevel)
    {
        var sb = new StringBuilder();
        var isFirst = true;

        // Generate property stubs first (convention: properties before methods)
        foreach (var prop in missingProperties)
        {
            if (!isFirst)
            {
                sb.AppendLine();
                sb.AppendLine();
            }
            isFirst = false;

            sb.Append(SharplySourceGenerator.FormatPropertyDef(
                prop.Name,
                prop.Type,
                prop.HasGetter,
                prop.HasSetter,
                indentLevel));
        }

        // Generate method stubs
        foreach (var method in missingMethods)
        {
            if (!isFirst)
            {
                sb.AppendLine();
                sb.AppendLine();
            }
            isFirst = false;

            sb.Append(FormatMethodStub(method, indentLevel));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Formats a method stub with proper handling of "self" parameter (no type annotation)
    /// and "raise NotImplementedError()" as the default body.
    /// </summary>
    private static string FormatMethodStub(FunctionSymbol method, int indentLevel)
    {
        var indent = new string(' ', indentLevel * SharplySourceGenerator.DefaultIndentWidth);
        var bodyIndent = new string(' ', (indentLevel + 1) * SharplySourceGenerator.DefaultIndentWidth);
        var sb = new StringBuilder();

        sb.Append(indent);
        sb.Append("def ");
        sb.Append(method.Name);
        sb.Append('(');

        var hasSelf = method.Parameters.Count > 0 &&
                      string.Equals(method.Parameters[0].Name, "self", StringComparison.Ordinal);

        if (!hasSelf)
        {
            // Instance methods always need self as the first parameter
            sb.Append("self");
        }

        for (var i = 0; i < method.Parameters.Count; i++)
        {
            var param = method.Parameters[i];

            if (i == 0 && hasSelf)
            {
                // "self" is written without a type annotation
                sb.Append("self");
            }
            else
            {
                // Add comma separator: always needed except before the very first token
                if (i > 0 || !hasSelf)
                    sb.Append(", ");

                sb.Append(SharplySourceGenerator.FormatParameter(param.Name, param.Type));
            }
        }

        sb.Append(')');

        var returnType = method.ReturnType;
        if (returnType is not null and not VoidType and not UnknownType)
        {
            sb.Append(" -> ");
            sb.Append(SharplySourceGenerator.FormatTypeAnnotation(returnType));
        }

        sb.Append(':');
        sb.AppendLine();
        sb.Append(bodyIndent);
        sb.Append("raise NotImplementedError()");

        return sb.ToString();
    }
}
