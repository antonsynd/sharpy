using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler.Parser.Ast;
using SCG = System.Collections.Generic;

namespace Sharpy.Lsp.Refactoring;

/// <summary>
/// Provides a "source.organizeImports" code action that sorts and groups imports.
/// Stdlib imports are placed first, followed by project imports, separated by a blank line.
/// Unused imports (SPY0452) are removed.
/// </summary>
/// <remarks>
/// The action MOVES imports, it never re-renders one (P47, #2232, plan D4). The block is the run of
/// import statements that starts at the module's first import and is interrupted by no other statement.
/// Each import is a UNIT: the own-line comment lines directly above it (blank lines excluded; none for the
/// first import, so a file-header comment above the block never moves) and the import's whole lines through
/// its true last line, verbatim — parentheses, inner comments and the trailing comment included. Units are
/// sorted by the import's key and written with the document's line break; the blank lines after the block
/// are left alone. A statement whose every name is unused loses its lines and keeps its comment lines
/// (written before the unit that takes its slot, or last); a statement with some unused names loses only
/// those names' text (<see cref="ImportNames.RemovalSpans"/>). Decorated imports are pinned at their slot.
/// </remarks>
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

    /// <summary>One import of the block: its leading comment lines, its own lines (null when dropped) and its key.</summary>
    private sealed record Unit(
        IReadOnlyList<string> Comments,
        IReadOnlyList<string>? Lines,
        bool IsPinned,
        bool IsFrom,
        bool IsStdlib,
        string SortKey);

    public Task<IReadOnlyList<CodeAction>> GetCodeActionsAsync(
        CodeActionProviderContext context,
        CancellationToken cancellationToken)
    {
        var ast = context.Analysis?.Ast;
        if (ast is null || context.SourceText is null)
            return Task.FromResult<IReadOnlyList<CodeAction>>(Array.Empty<CodeAction>());

        // The block: the module's first import and every import-like statement that follows it with no other
        // statement in between. A suppress-decorated import (#1124) belongs to the block (so the block is not
        // truncated) but is pinned at its slot verbatim — v1 policy leaves decorated imports in place.
        var block = new SCG.List<Statement>();
        foreach (var s in ast.Body)
        {
            if (s.UnwrapDecorated() is ImportStatement or FromImportStatement)
                block.Add(s);
            else if (block.Count > 0)
                break;
        }

        if (block.Count == 0)
            return Task.FromResult<IReadOnlyList<CodeAction>>(Array.Empty<CodeAction>());

        var source = SourceLines.Of(context.SourceText);
        var unused = new UnusedImportNames(context, block.SelectMany(s => ImportNames.Of(s)));
        var units = BuildUnits(block, source, unused);
        var newLines = Arrange(units);

        var first = block[0].LineStart;
        var last = source.EndLine(block[^1]);
        var textEdit = source.WholeLineEdit(first, last, newLines);

        var edit = new WorkspaceEdit
        {
            Changes = new Dictionary<OmniSharp.Extensions.LanguageServer.Protocol.DocumentUri, IEnumerable<TextEdit>>
            {
                [context.DocumentUri] = new[] { textEdit }
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

    /// <summary>The block's units in document order.</summary>
    private static SCG.List<Unit> BuildUnits(SCG.List<Statement> block, SourceLines source, UnusedImportNames unused)
    {
        var units = new SCG.List<Unit>(block.Count);
        var previousEnd = block[0].LineStart - 1;
        foreach (var stmt in block)
        {
            var end = source.EndLine(stmt);
            var comments = new SCG.List<string>();
            for (var line = previousEnd + 1; line < stmt.LineStart; line++)
            {
                if (!source.IsBlank(line))
                    comments.Add(source.Line(line));
            }

            previousEnd = end;
            if (stmt is DecoratedStatement)
            {
                units.Add(new Unit(comments, source.Slice(stmt.LineStart, end).Lines, IsPinned: true, false, false, ""));
                continue;
            }

            var names = ImportNames.Of(stmt);
            var removed = new SCG.HashSet<int>();
            for (var i = 0; i < names.Length; i++)
            {
                if (unused.Contains(names[i]))
                    removed.Add(i);
            }

            if (names.Length > 0 && removed.Count == names.Length)
            {
                units.Add(new Unit(comments, null, false, false, false, ""));
                continue;
            }

            var lines = removed.Count == 0
                ? source.Slice(stmt.LineStart, end).Lines
                : ImportNames.LinesWithout(source, stmt.LineStart, end, ImportNames.RemovalSpans(source, names, removed));
            var kept = names.Where((_, i) => !removed.Contains(i)).ToList();
            units.Add(new Unit(comments, lines, false, stmt is FromImportStatement, IsStdlibImport(stmt, kept), SortKey(stmt, kept)));
        }

        return units;
    }

    /// <summary>
    /// The block's new lines: every plain slot (in document order) takes the next unit of the sorted plain
    /// units — stdlib group, then project group, separated by one blank line when no import is pinned — and
    /// a pinned unit keeps its slot. A dropped unit's comment lines go before the next unit written, or last.
    /// </summary>
    private static SCG.List<string> Arrange(SCG.List<Unit> units)
    {
        var plain = units.Where(u => !u.IsPinned && u.Lines is not null).ToList();
        var stdlib = plain.Where(u => u.IsStdlib).OrderBy(u => u, UnitOrder.Instance).ToList();
        var project = plain.Where(u => !u.IsStdlib).OrderBy(u => u, UnitOrder.Instance).ToList();
        var sorted = new Queue<Unit>(stdlib.Concat(project));
        var separate = !units.Any(u => u.IsPinned) && stdlib.Count > 0 && project.Count > 0;

        var lines = new SCG.List<string>();
        var orphans = new SCG.List<string>();
        var written = 0;
        foreach (var unit in units)
        {
            if (unit.IsPinned)
            {
                Write(unit);
                continue;
            }

            if (unit.Lines is null)
            {
                orphans.AddRange(unit.Comments);
                continue;
            }

            if (separate && written == stdlib.Count)
                lines.Add("");
            Write(sorted.Dequeue());
            written++;
        }

        lines.AddRange(orphans);
        return lines;

        void Write(Unit unit)
        {
            lines.AddRange(orphans);
            orphans.Clear();
            lines.AddRange(unit.Comments);
            lines.AddRange(unit.Lines!);
        }
    }

    /// <summary>
    /// Determines whether an import is from the standard library (by its kept names for an
    /// <c>import</c> statement).
    /// </summary>
    private static bool IsStdlibImport(Statement stmt, SCG.IReadOnlyList<ImportAlias> keptNames)
    {
        if (stmt is FromImportStatement fromImp)
            return IsStdlibModuleName(fromImp.Module);

        foreach (var n in keptNames)
        {
            if (IsStdlibModuleName(n.Name))
                return true;
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
    /// The sort key (the module name): a from-import's module, an import's first kept name. Rendering only
    /// (documented-by-design): no semantic decision keys on it.
    /// </summary>
    private static string SortKey(Statement stmt, SCG.IReadOnlyList<ImportAlias> keptNames) => stmt switch
    {
        FromImportStatement fromImp => fromImp.Module,
        _ => keptNames.Count > 0 ? keptNames[0].Name : "",
    };

    /// <summary><c>import</c> statements before from-imports, then the module name, ordinal.</summary>
    private sealed class UnitOrder : IComparer<Unit>
    {
        public static readonly UnitOrder Instance = new();

        public int Compare(Unit? a, Unit? b)
        {
            if (a!.IsFrom != b!.IsFrom)
                return a.IsFrom ? 1 : -1;
            return string.Compare(a.SortKey, b.SortKey, StringComparison.Ordinal);
        }
    }
}
