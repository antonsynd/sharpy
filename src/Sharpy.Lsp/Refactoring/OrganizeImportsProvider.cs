using System.Text;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sharpy.Lsp.Refactoring;

/// <summary>
/// Provides a "source.organizeImports" code action that sorts and groups imports.
/// Stdlib imports are placed first, followed by project imports, separated by a blank line.
/// Unused imports (SPY0452) are removed.
/// </summary>
internal sealed class OrganizeImportsProvider : ICodeActionProvider
{
    /// <summary>
    /// Known standard library module names. Top-level module names only;
    /// dotted imports like "os.path" are classified by their first segment.
    /// </summary>
    private static readonly HashSet<string> StdlibModules = new(StringComparer.Ordinal)
    {
        "argparse",
        "collections",
        "datetime",
        "itertools",
        "json",
        "math",
        "operator",
        "os",
        "pathlib",
        "random",
        "re",
        "sys",
    };

    public Task<IReadOnlyList<CodeAction>> GetCodeActionsAsync(
        CodeActionProviderContext context,
        CancellationToken cancellationToken)
    {
        var ast = context.Analysis?.Ast;
        if (ast is null || context.SourceText is null)
            return Task.FromResult<IReadOnlyList<CodeAction>>(Array.Empty<CodeAction>());

        // Collect all import statements from the module body.
        var allImports = new System.Collections.Generic.List<Statement>();
        foreach (var s in ast.Body)
        {
            if (s is ImportStatement or FromImportStatement)
                allImports.Add(s);
        }

        if (allImports.Count == 0)
            return Task.FromResult<IReadOnlyList<CodeAction>>(Array.Empty<CodeAction>());

        // Build a set of line numbers that have SPY0452 (unused import) diagnostics.
        // We check both context.Diagnostics (from the client) and analysis diagnostics
        // to ensure comprehensive coverage.
        var unusedImportLines = new HashSet<int>();

        foreach (var diag in context.Diagnostics)
        {
            if (diag.Code?.String == DiagnosticCodes.Validation.UnusedImport)
            {
                // LSP lines are 0-based
                unusedImportLines.Add(diag.Range.Start.Line);
            }
        }

        if (context.Analysis?.Diagnostics is { } compilerDiags)
        {
            foreach (var diag in compilerDiags)
            {
                if (diag.Code == DiagnosticCodes.Validation.UnusedImport && diag.Line.HasValue)
                {
                    // Compiler lines are 1-based, convert to 0-based
                    unusedImportLines.Add(diag.Line.Value - 1);
                }
            }
        }

        // Classify imports, filtering out unused ones.
        var stdlibImports = new System.Collections.Generic.List<Statement>();
        var projectImports = new System.Collections.Generic.List<Statement>();

        foreach (var stmt in allImports)
        {
            if (IsUnusedImport(stmt, unusedImportLines))
                continue;

            if (IsStdlibImport(stmt))
                stdlibImports.Add(stmt);
            else
                projectImports.Add(stmt);
        }

        // Sort each group: import statements first, then from-import, alphabetically within each.
        SortImportGroup(stdlibImports);
        SortImportGroup(projectImports);

        // Generate the replacement text.
        var newText = GenerateImportText(stdlibImports, projectImports);

        // Determine the text range covering all existing imports.
        var sourceLines = context.SourceText.Split('\n');
        var importRange = GetImportRange(allImports, sourceLines);

        var edit = new WorkspaceEdit
        {
            Changes = new Dictionary<OmniSharp.Extensions.LanguageServer.Protocol.DocumentUri, IEnumerable<TextEdit>>
            {
                [context.DocumentUri] = new[]
                {
                    new TextEdit
                    {
                        Range = importRange,
                        NewText = newText
                    }
                }
            }
        };

        var action = new CodeAction
        {
            Title = "Organize Imports",
            Kind = CodeActionKind.SourceOrganizeImports,
            Edit = edit
        };

        return Task.FromResult<IReadOnlyList<CodeAction>>(new[] { action });
    }

    /// <summary>
    /// Checks whether an import statement falls on a line flagged as unused (SPY0452).
    /// </summary>
    private static bool IsUnusedImport(Statement stmt, HashSet<int> unusedImportLines)
    {
        // AST LineStart is 1-based; unusedImportLines is 0-based.
        return unusedImportLines.Contains(stmt.LineStart - 1);
    }

    /// <summary>
    /// Determines whether an import is from the standard library.
    /// </summary>
    private static bool IsStdlibImport(Statement stmt)
    {
        if (stmt is FromImportStatement fromImp)
            return IsStdlibModuleName(fromImp.Module);

        if (stmt is ImportStatement imp)
        {
            foreach (var n in imp.Names)
            {
                if (IsStdlibModuleName(n.Name))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if a module name (possibly dotted like "os.path") is a known stdlib module.
    /// Uses the first segment for classification.
    /// </summary>
    private static bool IsStdlibModuleName(string moduleName)
    {
        var dotIndex = moduleName.IndexOf('.', StringComparison.Ordinal);
        var firstSegment = dotIndex >= 0
            ? moduleName.Substring(0, dotIndex)
            : moduleName;
        return StdlibModules.Contains(firstSegment);
    }

    /// <summary>
    /// Sorts an import group: import statements before from-import statements,
    /// then alphabetically by module name within each sub-group.
    /// </summary>
    private static void SortImportGroup(System.Collections.Generic.List<Statement> imports)
    {
        imports.Sort(CompareImports);
    }

    private static int CompareImports(Statement a, Statement b)
    {
        // import statements come before from-import statements
        var aIsFrom = a is FromImportStatement;
        var bIsFrom = b is FromImportStatement;

        if (aIsFrom != bIsFrom)
            return aIsFrom ? 1 : -1;

        // Within the same kind, sort alphabetically by module name
        var aName = GetSortKey(a);
        var bName = GetSortKey(b);
        return string.Compare(aName, bName, StringComparison.Ordinal);
    }

    /// <summary>
    /// Gets the sort key for an import statement (the module name).
    /// </summary>
    private static string GetSortKey(Statement stmt)
    {
        return stmt switch
        {
            ImportStatement imp => imp.Names.Length > 0 ? imp.Names[0].Name : "",
            FromImportStatement fromImp => fromImp.Module,
            _ => ""
        };
    }

    /// <summary>
    /// Generates the replacement text for the organized imports.
    /// Stdlib imports come first, then a blank line, then project imports.
    /// </summary>
    private static string GenerateImportText(System.Collections.Generic.List<Statement> stdlibImports, System.Collections.Generic.List<Statement> projectImports)
    {
        var sb = new StringBuilder();

        foreach (var stmt in stdlibImports)
        {
            sb.AppendLine(RenderImportStatement(stmt));
        }

        // Add blank line separator if both groups have content
        if (stdlibImports.Count > 0 && projectImports.Count > 0)
        {
            sb.AppendLine();
        }

        foreach (var stmt in projectImports)
        {
            sb.AppendLine(RenderImportStatement(stmt));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Renders a single import statement back to source text.
    /// </summary>
    private static string RenderImportStatement(Statement stmt)
    {
        return stmt switch
        {
            ImportStatement imp => RenderImport(imp),
            FromImportStatement fromImp => RenderFromImport(fromImp),
            _ => ""
        };
    }

    private static string RenderImport(ImportStatement imp)
    {
        var parts = new System.Collections.Generic.List<string>(imp.Names.Length);
        foreach (var n in imp.Names)
            parts.Add(n.AsName != null ? $"{n.Name} as {n.AsName}" : n.Name);
        return $"import {string.Join(", ", parts)}";
    }

    private static string RenderFromImport(FromImportStatement fromImp)
    {
        if (fromImp.ImportAll)
            return $"from {fromImp.Module} import *";

        var parts = new System.Collections.Generic.List<string>(fromImp.Names.Length);
        foreach (var n in fromImp.Names)
            parts.Add(n.AsName != null ? $"{n.Name} as {n.AsName}" : n.Name);
        return $"from {fromImp.Module} import {string.Join(", ", parts)}";
    }

    /// <summary>
    /// Computes the LSP range covering all import statements, including any trailing blank lines
    /// between the last import and the first non-import statement.
    /// </summary>
    private static LspRange GetImportRange(System.Collections.Generic.List<Statement> allImports, string[] sourceLines)
    {
        // Find the first and last import lines (0-based for LSP).
        var firstLine = int.MaxValue;
        var lastImportLine = int.MinValue;
        foreach (var s in allImports)
        {
            if (s.LineStart < firstLine)
                firstLine = s.LineStart;
            if (s.LineEnd > lastImportLine)
                lastImportLine = s.LineEnd;
        }
        firstLine -= 1; // 1-based to 0-based
        lastImportLine -= 1; // 1-based to 0-based

        // Extend past any blank lines after the last import
        var endLine = lastImportLine + 1;
        while (endLine < sourceLines.Length && string.IsNullOrWhiteSpace(sourceLines[endLine]))
        {
            endLine++;
        }

        return new LspRange(
            new Position(firstLine, 0),
            new Position(endLine, 0));
    }
}
