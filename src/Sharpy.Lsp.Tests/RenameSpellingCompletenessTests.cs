using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Lsp.Handlers;
using Xunit;
using LexerNs = Sharpy.Compiler.Lexer;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// A class guard over rename: whatever a rename produces must be the SAME PROGRAM under a different
/// spelling. Applies each fixture's edits for real, recompiles the result, and checks two things the
/// per-shape handler tests cannot — that the diagnostics did not move, and that no occurrence of the
/// old spelling survived in the renamed binding's function.
/// </summary>
/// <remarks>
/// <para>
/// This guards a CLASS, not a shape. The motivating defect (#1359) is silent by construction: a
/// rename that covers only part of a binding chain leaves source that still COMPILES and still runs
/// — it just means something else. No assertion about the edit list catches that; only recompiling
/// the renamed program does.
/// </para>
/// <para>
/// The diagnostic check is a MULTISET COMPARISON against the pre-rename source, not "zero
/// diagnostics": several fixtures emit SPY0451 (unused) by construction, and demanding silence would
/// make the guard unusable on exactly the fixtures worth guarding. What must never change is the set
/// of complaints — a rename that introduces or removes one has changed the program.
/// </para>
/// <para>
/// The residual-spelling check is scoped to the renamed binding's own function. Scoping it wider
/// fails spuriously on the shadowing fixtures, where the same spelling legitimately survives
/// elsewhere as a different variable.
/// </para>
/// </remarks>
public class RenameSpellingCompletenessTests : IDisposable
{
    private readonly CompilerApi _api = new();
    private readonly SharpyWorkspace _workspace;
    private readonly LanguageService _languageService;
    private readonly SharpyRenameHandler _handler;

    public RenameSpellingCompletenessTests()
    {
        _workspace = new SharpyWorkspace(_api, NullLogger<SharpyWorkspace>.Instance);
        _languageService = new LanguageService(_workspace, _api, NullLogger<LanguageService>.Instance);
        _handler = new SharpyRenameHandler(
            _workspace, _languageService, _api, NullLogger<SharpyRenameHandler>.Instance);
    }

    public static TheoryData<string, string, int, int, string, string> Fixtures() => new()
    {
        // label, source, cursor line, cursor col, old spelling, new spelling
        {
            "rebound local, from declaration",
            "def main() -> None:\n    count: int = 0\n    count = count + 1\n    print(count)\n",
            1, 4, "count", "total"
        },
        {
            "rebound local, from a use after the rebinding",
            "def main() -> None:\n    count: int = 0\n    count = count + 1\n    print(count)\n",
            3, 10, "count", "total"
        },
        {
            "rebound local, rebound twice",
            "def main() -> None:\n    n: int = 0\n    n = n + 1\n    n = n * 2\n    print(n)\n",
            1, 4, "n", "acc"
        },
        {
            "parameter, from declaration",
            "def f(target: int) -> int:\n    return target * 2\n",
            0, 6, "target", "scale"
        },
        {
            "parameter, from reference",
            "def f(target: int) -> int:\n    return target * 2\n",
            1, 11, "target", "scale"
        },
        {
            "parameter, rebound in the body",
            "def f(target: int) -> int:\n    target = target + 1\n    return target\n",
            0, 6, "target", "scale"
        },
        {
            "unreferenced parameter",
            "def f(unused: int) -> int:\n    return 1\n",
            0, 6, "unused", "ignored"
        },
        {
            "variadic parameter",
            "def f(*args: int) -> int:\n    return len(args)\n",
            0, 7, "args", "values"
        },
        {
            "escaped parameter",
            "def f(`event`: int) -> int:\n    return `event`\n",
            0, 7, "`event`", "handler"
        },
        {
            "escaped local",
            "def main() -> None:\n    `event`: int = 1\n    print(`event`)\n",
            1, 5, "`event`", "handler"
        },
        {
            "local nothing references",
            "def main() -> None:\n    solo: int = 1\n",
            1, 4, "solo", "only"
        },
        {
            "method parameter",
            "class Box:\n    def scale(self, factor: int) -> int:\n        return factor * 2\n",
            1, 20, "factor", "ratio"
        },
        {
            "shadowing: the same spelling in another function stays put",
            "def first() -> None:\n    count: int = 0\n    count = count + 1\n\n\n"
            + "def second() -> None:\n    count: int = 9\n    print(count)\n",
            1, 4, "count", "total"
        },
    };

    [Theory]
    [MemberData(nameof(Fixtures))]
    public async Task Rename_IsSpellingComplete_AndPreservesTheProgram(
        string label, string source, int line, int col, string oldSpelling, string newName)
    {
        var before = await DiagnosticCodesOfAsync(source, "file:///before.spy");

        var renamed = await ApplyRenameAsync(source, line, col, newName);
        renamed.Should().NotBeNull($"[{label}] rename must produce edits");

        var after = await DiagnosticCodesOfAsync(renamed!, "file:///after.spy");

        // (a) the program's complaints are unchanged — a rename that adds or drops one has changed
        //     what the program means, however well-formed the result looks.
        after.Should().BeEquivalentTo(before,
            $"[{label}] renaming must not move diagnostics.\n--- before ---\n{source}\n"
            + $"--- after ---\n{renamed}");

        // (b) no occurrence of the old spelling survives in the renamed binding's own function.
        var core = oldSpelling.Trim('`');
        var survivors = IdentifierOccurrencesInEnclosingFunction(renamed!, core, line + 1);

        survivors.Should().BeEmpty(
            $"[{label}] '{core}' still occurs at line(s) {string.Join(", ", survivors)} inside the "
            + $"renamed binding's function — a partially-applied rename that still compiles.\n"
            + $"--- after ---\n{renamed}");
    }

    /// <summary>Runs the rename and returns the source with its edits applied, or null if refused.</summary>
    private async Task<string?> ApplyRenameAsync(string source, int line, int col, string newName)
    {
        const string uri = "file:///rename.spy";
        _workspace.OpenDocument(uri, source, 1);

        var result = await _handler.Handle(
            new RenameParams
            {
                TextDocument = new TextDocumentIdentifier(uri),
                Position = new Position(line, col),
                NewName = newName
            },
            CancellationToken.None);

        if (result?.Changes == null
            || !result.Changes.TryGetValue(DocumentUri.From(uri), out var edits))
        {
            return null;
        }

        var lines = source.Split('\n');

        // Right-to-left within each line so earlier edits do not shift later offsets.
        foreach (var edit in edits
                     .OrderByDescending(e => e.Range.Start.Line)
                     .ThenByDescending(e => e.Range.Start.Character))
        {
            var target = lines[edit.Range.Start.Line];
            lines[edit.Range.Start.Line] =
                target[..edit.Range.Start.Character]
                + edit.NewText
                + target[edit.Range.End.Character..];
        }

        return string.Join("\n", lines);
    }

    private async Task<System.Collections.Generic.List<string>> DiagnosticCodesOfAsync(string source, string uri)
    {
        _workspace.OpenDocument(uri, source, 1);
        var analysis = await _workspace.GetAnalysisAsync(uri, CancellationToken.None);

        if (analysis == null)
            return new System.Collections.Generic.List<string>();

        return analysis.Diagnostics
            .Select(d => d.Code ?? "<no-code>")
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Lines within the function enclosing <paramref name="cursorLine"/> (1-based) that still carry
    /// <paramref name="name"/> as an identifier token. Lexed rather than string-searched so a
    /// substring of a longer name or a mention inside a string literal is not a false positive.
    /// </summary>
    private static IReadOnlyList<int> IdentifierOccurrencesInEnclosingFunction(
        string source, string name, int cursorLine)
    {
        var module = ParseModule(source);
        var enclosing = module.Body
            .OfType<FunctionDef>()
            .Concat(module.Body.OfType<ClassDef>().SelectMany(c => c.Body.OfType<FunctionDef>()))
            .FirstOrDefault(f => f.LineStart <= cursorLine && cursorLine <= f.LineEnd);

        // A module-level binding has no enclosing function; the whole module is its scope.
        var from = enclosing?.LineStart ?? 1;
        var to = enclosing?.LineEnd ?? int.MaxValue;

        return new LexerNs.Lexer(source).TokenizeAll()
            .Where(t => t.Type == LexerNs.TokenType.Identifier
                        && t.Value == name
                        && t.Line >= from
                        && t.Line <= to)
            .Select(t => t.Line)
            .ToList();
    }

    private static Module ParseModule(string source)
    {
        var tokens = new LexerNs.Lexer(source).TokenizeAll();
        return new Compiler.Parser.Parser(tokens).ParseModule();
    }

    public void Dispose()
    {
        _languageService.Dispose();
        _workspace.Dispose();
    }
}
