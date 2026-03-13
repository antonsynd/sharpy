using System.Text;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Services;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;
using SCG = System.Collections.Generic;

namespace Sharpy.Lsp.Refactoring;

/// <summary>
/// Provides "Extract Method" refactoring code actions.
/// Offered when the user selects one or more complete statements that can be
/// safely moved into a new function.
/// </summary>
internal sealed class ExtractMethodProvider : ICodeActionProvider
{
    private const string DefaultMethodName = "extracted_method";

    public Task<IReadOnlyList<CodeAction>> GetCodeActionsAsync(
        CodeActionProviderContext context,
        CancellationToken cancellationToken)
    {
        var ast = context.Analysis?.Ast;
        if (ast is null || context.SourceText is null)
            return Task.FromResult<IReadOnlyList<CodeAction>>(Array.Empty<CodeAction>());

        // Extract method requires a non-empty range selection.
        if (SelectionAnalyzer.IsZeroWidthSelection(context.Range))
            return Task.FromResult<IReadOnlyList<CodeAction>>(Array.Empty<CodeAction>());

        // Find the statements fully contained within the selection.
        var selectedStatements = SelectionAnalyzer.FindSelectedStatements(
            ast, context.SourceText, context.Range);

        if (selectedStatements.Count == 0)
            return Task.FromResult<IReadOnlyList<CodeAction>>(Array.Empty<CodeAction>());

        // Find the containing function (if any) to get all statements for scope analysis.
        var (startLine, startCol) = PositionConverter.ToCompiler(context.Range.Start);
        var containingFunction = SelectionAnalyzer.FindContainingFunction(ast, startLine, startCol);

        // Determine the full body that contains the selected statements.
        IReadOnlyList<Statement> allStatements = containingFunction?.Body
            ?? (IReadOnlyList<Statement>)ast.Body;

        // Analyze scope to determine parameters and return values.
        var semanticQuery = context.Analysis?.SemanticQuery;
        var scopeInfo = ScopeAnalyzer.AnalyzeScope(selectedStatements, allStatements, semanticQuery);

        // Don't offer extract method if the selection contains control flow that
        // cannot be cleanly extracted (return, break, continue, yield).
        if (scopeInfo.ContainsReturn || scopeInfo.ContainsBreak ||
            scopeInfo.ContainsContinue || scopeInfo.ContainsYield)
        {
            return Task.FromResult<IReadOnlyList<CodeAction>>(Array.Empty<CodeAction>());
        }

        // Build parameter list from variables read from outer scope.
        var parameters = BuildParameters(scopeInfo.ReadsFromOuterScope, containingFunction, semanticQuery);

        // Build return values from variables written to outer scope.
        var returnValues = BuildReturnValues(scopeInfo.WritesToOuterScope, containingFunction, semanticQuery);

        // Determine return type.
        var returnType = DetermineReturnType(returnValues);

        // Determine indentation context.
        var containingClass = SelectionAnalyzer.FindContainingClass(ast, startLine, startCol);
        int functionIndentLevel = DetermineIndentLevel(containingFunction, containingClass);

        // Build the extracted function body from the selected source text.
        var bodyText = BuildFunctionBody(
            selectedStatements, returnValues, context.SourceText, functionIndentLevel);

        // Generate the new function definition.
        var functionDef = SharpySourceGenerator.FormatFunctionDef(
            DefaultMethodName,
            parameters,
            returnType,
            functionIndentLevel,
            bodyText);

        // Generate the call site that replaces the selected statements.
        var stmtIndent = SharpySourceGenerator.GetIndentation(
            context.SourceText, selectedStatements[0].LineStart - 1);
        var callSite = BuildCallSite(DefaultMethodName, parameters, returnValues, stmtIndent);

        // Build the two text edits.
        var edits = BuildTextEdits(
            context.DocumentUri,
            context.SourceText,
            selectedStatements,
            containingFunction,
            containingClass,
            functionDef,
            callSite);

        var action = new CodeAction
        {
            Title = "Extract method",
            Kind = CodeActionKind.RefactorExtract,
            Edit = new WorkspaceEdit
            {
                Changes = new Dictionary<OmniSharp.Extensions.LanguageServer.Protocol.DocumentUri, IEnumerable<TextEdit>>
                {
                    [context.DocumentUri] = edits
                }
            }
        };

        return Task.FromResult<IReadOnlyList<CodeAction>>(new[] { action });
    }

    /// <summary>
    /// Builds the parameter list for the extracted function.
    /// Attempts to resolve types from the semantic query or containing function parameters.
    /// </summary>
    private static IReadOnlyList<(string Name, SemanticType Type)> BuildParameters(
        IReadOnlySet<string> readsFromOuterScope,
        FunctionDef? containingFunction,
        ISemanticQuery? query)
    {
        if (readsFromOuterScope.Count == 0)
            return Array.Empty<(string Name, SemanticType Type)>();

        // Sort parameter names for deterministic output.
        var sortedNames = readsFromOuterScope.OrderBy(n => n, StringComparer.Ordinal).ToList();
        var parameters = new SCG.List<(string Name, SemanticType Type)>();

        foreach (var name in sortedNames)
        {
            var type = TryResolveVariableType(name, containingFunction, query);
            parameters.Add((name, type));
        }

        return parameters;
    }

    /// <summary>
    /// Builds the list of return values for the extracted function.
    /// </summary>
    private static IReadOnlyList<(string Name, SemanticType Type)> BuildReturnValues(
        IReadOnlySet<string> writesToOuterScope,
        FunctionDef? containingFunction,
        ISemanticQuery? query)
    {
        if (writesToOuterScope.Count == 0)
            return Array.Empty<(string Name, SemanticType Type)>();

        var sortedNames = writesToOuterScope.OrderBy(n => n, StringComparer.Ordinal).ToList();
        var returnValues = new SCG.List<(string Name, SemanticType Type)>();

        foreach (var name in sortedNames)
        {
            var type = TryResolveVariableType(name, containingFunction, query);
            returnValues.Add((name, type));
        }

        return returnValues;
    }

    /// <summary>
    /// Attempts to resolve the type of a variable by name.
    /// Searches the containing function's parameters first, then walks the
    /// function body for variable declarations and assignments, and finally
    /// falls back to the semantic query.
    /// </summary>
    private static SemanticType TryResolveVariableType(
        string name,
        FunctionDef? containingFunction,
        ISemanticQuery? query)
    {
        if (containingFunction is not null)
        {
            // Check function parameters.
            foreach (var param in containingFunction.Parameters)
            {
                if (param.Name == name && param.Type is not null && query is not null)
                {
                    var resolvedType = query.GetTypeAnnotation(param.Type);
                    if (resolvedType is not null)
                        return resolvedType;
                }
            }

            // Walk the function body for variable declarations.
            foreach (var stmt in containingFunction.Body)
            {
                if (stmt is VariableDeclaration varDecl && varDecl.Name == name)
                {
                    if (varDecl.Type is not null && query is not null)
                    {
                        var resolvedType = query.GetTypeAnnotation(varDecl.Type);
                        if (resolvedType is not null)
                            return resolvedType;
                    }

                    // Try to get the type from the initializer expression.
                    if (varDecl.InitialValue is not null && query is not null)
                    {
                        var exprType = query.GetExpressionType(varDecl.InitialValue);
                        if (exprType is not null)
                            return exprType;
                    }

                    break;
                }
            }
        }

        // Fallback: try finding an Identifier node with this name in the function
        // and getting its type from the semantic query.
        if (query is not null && containingFunction is not null)
        {
            var identifierFinder = new IdentifierFinder(name);
            identifierFinder.Visit(containingFunction);
            if (identifierFinder.Found is not null)
            {
                var idType = query.GetEffectiveType(identifierFinder.Found);
                if (idType is not null)
                    return idType;
            }
        }

        // Last resort: return unknown type (parameter will be emitted without type annotation).
        return SemanticType.Unknown;
    }

    /// <summary>
    /// Determines the return type based on the return values.
    /// </summary>
    private static SemanticType? DetermineReturnType(
        IReadOnlyList<(string Name, SemanticType Type)> returnValues)
    {
        return returnValues.Count switch
        {
            0 => null, // void/None
            1 => returnValues[0].Type,
            _ => new Sharpy.Compiler.Semantic.TupleType
            {
                ElementTypes = returnValues.Select(rv => rv.Type).ToList()
            }
        };
    }

    /// <summary>
    /// Determines the indentation level for the new function based on its
    /// insertion context (module-level, class-level, or nested).
    /// </summary>
    private static int DetermineIndentLevel(
        FunctionDef? containingFunction,
        Statement? containingClass)
    {
        if (containingFunction is not null && containingClass is not null)
        {
            // Method inside a class: the extracted function goes at class body level.
            return 1;
        }

        if (containingFunction is not null)
        {
            // Function at module level: the extracted function goes at module level.
            return 0;
        }

        // Module-level statements: extracted function goes at module level.
        return 0;
    }

    /// <summary>
    /// Builds the function body text from the selected statements' source text,
    /// re-indented appropriately, with an optional return statement appended.
    /// </summary>
    private static string BuildFunctionBody(
        IReadOnlyList<Statement> selectedStatements,
        IReadOnlyList<(string Name, SemanticType Type)> returnValues,
        string sourceText,
        int functionIndentLevel)
    {
        var lines = sourceText.Split('\n');
        var bodyIndent = new string(' ', (functionIndentLevel + 1) * SharpySourceGenerator.DefaultIndentWidth);

        // Determine the original indentation of the selected statements
        // by looking at the first selected statement's line.
        var firstStmtLineIndex = selectedStatements[0].LineStart - 1; // 1-based to 0-based
        var originalIndent = "";
        if (firstStmtLineIndex >= 0 && firstStmtLineIndex < lines.Length)
        {
            originalIndent = GetLeadingWhitespace(lines[firstStmtLineIndex]);
        }

        // Extract and re-indent the source lines for the selected statements.
        var firstLine = selectedStatements[0].LineStart - 1; // 0-based
        var lastLine = selectedStatements[selectedStatements.Count - 1].LineEnd - 1; // 0-based
        var sb = new StringBuilder();

        for (var i = firstLine; i <= lastLine && i < lines.Length; i++)
        {
            if (i > firstLine)
            {
                sb.AppendLine();
                sb.Append(bodyIndent);
            }

            // Strip original indentation and let the caller handle adding bodyIndent.
            var line = lines[i];
            if (line.StartsWith(originalIndent, StringComparison.Ordinal))
            {
                sb.Append(line.Substring(originalIndent.Length).TrimEnd('\r'));
            }
            else
            {
                sb.Append(line.TrimEnd('\r'));
            }
        }

        // Append return statement if there are return values.
        if (returnValues.Count > 0)
        {
            sb.AppendLine();
            sb.Append(bodyIndent);
            sb.Append("return ");
            if (returnValues.Count == 1)
            {
                sb.Append(returnValues[0].Name);
            }
            else
            {
                sb.Append(string.Join(", ", returnValues.Select(rv => rv.Name)));
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Builds the call site text that replaces the selected statements.
    /// </summary>
    private static string BuildCallSite(
        string functionName,
        IReadOnlyList<(string Name, SemanticType Type)> parameters,
        IReadOnlyList<(string Name, SemanticType Type)> returnValues,
        string indent)
    {
        var sb = new StringBuilder();

        // Build the function call expression.
        var callExpr = new StringBuilder();
        callExpr.Append(functionName);
        callExpr.Append('(');
        for (var i = 0; i < parameters.Count; i++)
        {
            if (i > 0)
                callExpr.Append(", ");
            callExpr.Append(parameters[i].Name);
        }
        callExpr.Append(')');

        if (returnValues.Count == 0)
        {
            // Simple call, no return value unpacking.
            sb.Append(indent);
            sb.Append(callExpr);
        }
        else if (returnValues.Count == 1)
        {
            // Single return value: x = extracted_method(...)
            sb.Append(indent);
            sb.Append(returnValues[0].Name);
            sb.Append(" = ");
            sb.Append(callExpr);
        }
        else
        {
            // Multiple return values: x, y = extracted_method(...)
            sb.Append(indent);
            sb.Append(string.Join(", ", returnValues.Select(rv => rv.Name)));
            sb.Append(" = ");
            sb.Append(callExpr);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Builds the TextEdits for the extract method refactoring:
    /// 1. Insert the new function definition before the containing function (or at appropriate scope).
    /// 2. Replace the selected statements with the call site.
    /// </summary>
    private static IEnumerable<TextEdit> BuildTextEdits(
        OmniSharp.Extensions.LanguageServer.Protocol.DocumentUri uri,
        string sourceText,
        IReadOnlyList<Statement> selectedStatements,
        FunctionDef? containingFunction,
        Statement? containingClass,
        string functionDef,
        string callSite)
    {
        var edits = new SCG.List<TextEdit>();

        // Edit 1: Insert new function definition.
        // Determine insertion point based on context.
        Position insertPosition;
        if (containingFunction is not null && containingClass is not null)
        {
            // Inside a class method: insert before the containing method, at class body level.
            insertPosition = PositionConverter.ToLsp(containingFunction.LineStart, 1);
        }
        else if (containingFunction is not null)
        {
            // Module-level function: insert before it.
            insertPosition = PositionConverter.ToLsp(containingFunction.LineStart, 1);
        }
        else
        {
            // Module-level statements: insert before the first selected statement.
            insertPosition = PositionConverter.ToLsp(selectedStatements[0].LineStart, 1);
        }

        var insertEdit = new TextEdit
        {
            Range = new LspRange(insertPosition, insertPosition),
            NewText = functionDef + "\n\n"
        };
        edits.Add(insertEdit);

        // Edit 2: Replace selected statements with the call site.
        var firstStmt = selectedStatements[0];
        var lastStmt = selectedStatements[selectedStatements.Count - 1];

        // Start at the beginning of the first statement's line.
        var replaceStart = PositionConverter.ToLsp(firstStmt.LineStart, 1);

        // End at the end of the last statement's line (start of next line).
        var replaceEnd = PositionConverter.ToLsp(lastStmt.LineEnd + 1, 1);

        var replaceEdit = new TextEdit
        {
            Range = new LspRange(replaceStart, replaceEnd),
            NewText = callSite + "\n"
        };
        edits.Add(replaceEdit);

        return edits;
    }

    /// <summary>
    /// Returns the leading whitespace of a string.
    /// </summary>
    private static string GetLeadingWhitespace(string line)
    {
        var i = 0;
        while (i < line.Length && (line[i] == ' ' || line[i] == '\t'))
            i++;
        return line[..i];
    }

    /// <summary>
    /// Visitor that finds the first Identifier node with a given name in an AST subtree.
    /// Used as a fallback for resolving variable types via the semantic query.
    /// </summary>
    private sealed class IdentifierFinder : AstVisitor
    {
        private readonly string _targetName;

        public IdentifierFinder(string targetName)
        {
            _targetName = targetName;
        }

        public Identifier? Found { get; private set; }

        public override void VisitIdentifier(Identifier node)
        {
            if (Found is null && node.Name == _targetName)
            {
                Found = node;
            }
        }
    }
}
