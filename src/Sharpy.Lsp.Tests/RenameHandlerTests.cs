using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Lsp.Handlers;
using Xunit;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// Handler-based tests for SharpyRenameHandler verifying that rename edits
/// use the name token position (EffectiveNameLine/Column) rather than the
/// statement start (DeclarationLine/Column).
/// </summary>
public class RenameHandlerTests : IDisposable
{
    private readonly CompilerApi _api = new();
    private readonly SharpyWorkspace _workspace;
    private readonly LanguageService _languageService;
    private readonly SharpyRenameHandler _handler;

    public RenameHandlerTests()
    {
        _workspace = new SharpyWorkspace(_api, NullLogger<SharpyWorkspace>.Instance);
        _languageService = new LanguageService(_workspace, _api, NullLogger<LanguageService>.Instance);
        _handler = new SharpyRenameHandler(
            _workspace, _languageService, _api, NullLogger<SharpyRenameHandler>.Instance);
    }

    private async Task<WorkspaceEdit?> RenameAsync(string source, int line, int col, string newName)
    {
        var uri = "file:///test.spy";
        _workspace.OpenDocument(uri, source, 1);

        var request = new RenameParams
        {
            TextDocument = new TextDocumentIdentifier(uri),
            Position = new Position(line, col),
            NewName = newName
        };

        return await _handler.Handle(request, CancellationToken.None);
    }

    [Fact]
    public async Task Rename_AsyncFunction_RenamesNameNotKeyword()
    {
        // Line 0: "async def do_something() -> int:"
        //          0123456789...
        //          "async" at col 0, "def" at col 6, "do_something" at col 10
        var source = "async def do_something() -> int:\n    return 1\nasync def main():\n    await do_something()";

        // Cursor on "do_something" at definition site: line 0, col 10 (0-based)
        var result = await RenameAsync(source, 0, 10, "do_other");

        result.Should().NotBeNull("rename should produce edits");
        result!.Changes.Should().NotBeNull();

        var uri = DocumentUri.From("file:///test.spy");
        result.Changes.Should().ContainKey(uri);

        var edits = result.Changes![uri].ToList();
        edits.Should().NotBeEmpty();

        // The declaration edit should start at col 10 (the name "do_something"), NOT col 0 ("async")
        var declEdit = edits.FirstOrDefault(e => e.Range.Start.Line == 0);
        declEdit.Should().NotBeNull("should have an edit on declaration line");
        declEdit!.Range.Start.Character.Should().Be(10,
            "declaration edit should start at the name token 'do_something' (col 10), not at 'async' (col 0)");
        declEdit.Range.End.Character.Should().Be(10 + "do_something".Length,
            "declaration edit end should cover the full name");

        // There should also be an edit for the call site "do_something()" on line 3
        var callEdit = edits.FirstOrDefault(e => e.Range.Start.Line == 3);
        callEdit.Should().NotBeNull("should have an edit at the call site");
    }

    [Fact]
    public async Task Rename_DeclarationAlsoRecordedAsReference_EmitsOneEditPerOccurrence()
    {
        // Pins the invariant #1263 is about: no two edits may target the same range, because an
        // editor applying both writes the new name twice. MEASURED CAVEAT: with the dedupe disabled
        // this shape does NOT duplicate — five shapes were probed (function, local, module variable,
        // class, parameter) and none did, so this is a guard against the shape arising, not a
        // reproduction of it. See the #1263 comment for what the probe found instead.
        var source = "def target() -> int:\n    return 1\n\ndef main() -> None:\n    print(target())\n    print(target())";

        var result = await RenameAsync(source, 0, 4, "renamed");

        result.Should().NotBeNull();
        var uri = DocumentUri.From("file:///test.spy");
        var edits = result!.Changes![uri].ToList();

        edits.Select(e => (e.Range.Start.Line, e.Range.Start.Character, e.Range.End.Character))
            .Should().OnlyHaveUniqueItems("no two edits may target the same range");
        edits.Should().HaveCount(3, "one declaration edit and one per call site");
        edits.Should().OnlyContain(e => e.NewText == "renamed");
    }

    [Fact]
    public async Task Rename_DecoratedFunction_RenamesNameNotDecorator()
    {
        // Test a decorated top-level function.
        // The FunctionDef.LineStart points to the decorator, but the
        // declaration edit must use EffectiveNameLine/Column (the "bar" token).
        // Line 0: "@deprecated(\"use baz\")"
        // Line 1: "def bar() -> int:"
        //          "def " = 4 chars, "bar" starts at col 4
        // Line 2: "    return 1"
        // Line 3: "def main():"
        // Line 4: "    bar()"
        var source = "@deprecated(\"use baz\")\ndef bar() -> int:\n    return 1\ndef main():\n    bar()";

        // Cursor on "bar" at call site: line 4, col 4 (0-based)
        var result = await RenameAsync(source, 4, 4, "baz");

        result.Should().NotBeNull("rename should produce edits");
        result!.Changes.Should().NotBeNull();

        var uri = DocumentUri.From("file:///test.spy");
        result.Changes.Should().ContainKey(uri);

        var edits = result.Changes![uri].ToList();
        edits.Should().NotBeEmpty();

        // The declaration edit should be at line 1, col 4 ("bar"), NOT at line 0 ("@deprecated")
        var declEdit = edits.FirstOrDefault(e => e.Range.Start.Line == 1);
        declEdit.Should().NotBeNull("should have an edit on the function definition line (line 1), not the decorator line");
        declEdit!.Range.Start.Character.Should().Be(4,
            "declaration edit should start at the name token 'bar' (col 4), not at the decorator");
    }

    [Fact]
    public async Task Rename_ClassDefinition_RenamesFromDeclarationSite()
    {
        // Line 0: "class MyClass:"
        //          "class " = 6 chars, "MyClass" starts at col 6
        var source = "class MyClass:\n    def __init__(self):\n        pass\ndef main():\n    c = MyClass()";

        // Cursor on "MyClass" at declaration: line 0, col 6 (0-based)
        var result = await RenameAsync(source, 0, 6, "NewClass");

        result.Should().NotBeNull("rename should produce edits");
        result!.Changes.Should().NotBeNull();

        var uri = DocumentUri.From("file:///test.spy");
        result.Changes.Should().ContainKey(uri);

        var edits = result.Changes![uri].ToList();
        edits.Should().NotBeEmpty();

        // The declaration edit should start at col 6 ("MyClass"), NOT col 0 ("class")
        var declEdit = edits.FirstOrDefault(e => e.Range.Start.Line == 0);
        declEdit.Should().NotBeNull("should have an edit on declaration line");
        declEdit!.Range.Start.Character.Should().Be(6,
            "declaration edit should start at the name token 'MyClass' (col 6), not at 'class' (col 0)");
        declEdit.Range.End.Character.Should().Be(6 + "MyClass".Length,
            "declaration edit end should cover the full name");
    }

    [Fact]
    public async Task Rename_FunctionParameter_RenamesFromCallSite()
    {
        // Line 0: "def greet(name: str) -> str:"
        //          "greet" at col 4, "name" at col 10
        // Line 1: "    return name"
        //          "name" at col 11
        // Line 2: "def main():"
        // Line 3: "    greet(\"world\")"
        var source = "def greet(name: str) -> str:\n    return name\ndef main():\n    greet(\"world\")";

        // Cursor on "greet" at call site: line 3, col 4 (0-based)
        var result = await RenameAsync(source, 3, 4, "hello");

        result.Should().NotBeNull("rename should produce edits");
        result!.Changes.Should().NotBeNull();

        var uri = DocumentUri.From("file:///test.spy");
        result.Changes.Should().ContainKey(uri);

        var edits = result.Changes![uri].ToList();
        edits.Should().NotBeEmpty();

        // The declaration edit should be at line 0, col 4 ("greet"), NOT at "def" position
        var declEdit = edits.FirstOrDefault(e => e.Range.Start.Line == 0);
        declEdit.Should().NotBeNull("should have an edit on declaration line");
        declEdit!.Range.Start.Character.Should().Be(4,
            "declaration edit should start at the name token 'greet' (col 4), not at 'def' (col 0)");
        declEdit.NewText.Should().Be("hello");
    }

    [Fact]
    public async Task Rename_FunctionFromDeclarationSite_Works()
    {
        // Line 0: "def foo() -> int:"
        //          "def " = 4 chars, "foo" starts at col 4
        var source = "def foo() -> int:\n    return 1\ndef main():\n    foo()";

        // Cursor on "foo" at definition: line 0, col 4 (0-based)
        var result = await RenameAsync(source, 0, 4, "bar");

        result.Should().NotBeNull("rename should produce edits");
        result!.Changes.Should().NotBeNull();

        var uri = DocumentUri.From("file:///test.spy");
        result.Changes.Should().ContainKey(uri);

        var edits = result.Changes![uri].ToList();
        edits.Should().NotBeEmpty();

        // The declaration edit should start at col 4 ("foo"), NOT col 0 ("def")
        var declEdit = edits.FirstOrDefault(e => e.Range.Start.Line == 0);
        declEdit.Should().NotBeNull("should have an edit on declaration line");
        declEdit!.Range.Start.Character.Should().Be(4,
            "declaration edit should start at the name token 'foo' (col 4), not at 'def' (col 0)");
        declEdit.Range.End.Character.Should().Be(4 + "foo".Length,
            "declaration edit end should cover the full name");

        // Also verify the call site edit
        var callEdit = edits.FirstOrDefault(e => e.Range.Start.Line == 3);
        callEdit.Should().NotBeNull("should have an edit at the call site");
        callEdit!.NewText.Should().Be("bar");
    }

    [Fact]
    public async Task Rename_StructDefinition_RenamesFromDeclarationSite()
    {
        // Line 0: "struct Point:"
        //          "struct " = 7 chars, "Point" at col 7
        // Line 1: "    x: int = 0"
        // Line 2: "    y: int = 0"
        // Line 3: "def main():"
        // Line 4: "    p = Point()"  ("Point" at col 8)
        var source = "struct Point:\n    x: int = 0\n    y: int = 0\ndef main():\n    p = Point()";

        // Cursor on "Point" at declaration: line 0, col 7 (0-based)
        var result = await RenameAsync(source, 0, 7, "Vec2");

        result.Should().NotBeNull("rename should produce edits");
        result!.Changes.Should().NotBeNull();

        var uri = DocumentUri.From("file:///test.spy");
        result.Changes.Should().ContainKey(uri);

        var edits = result.Changes![uri].ToList();
        edits.Should().NotBeEmpty();

        // The declaration edit should start at col 7 ("Point"), NOT col 0 ("struct")
        var declEdit = edits.FirstOrDefault(e => e.Range.Start.Line == 0);
        declEdit.Should().NotBeNull("should have an edit on declaration line");
        declEdit!.Range.Start.Character.Should().Be(7,
            "declaration edit should start at the name token 'Point' (col 7), not at 'struct' (col 0)");
        declEdit.Range.End.Character.Should().Be(7 + "Point".Length,
            "declaration edit end should cover the full name");
    }

    /// <summary>
    /// Regression test for #597: Rename from an assignment declaration site.
    /// When the cursor is on `x` in `x = 5`, the Identifier AST node is resolved
    /// and the rename should produce edits at both the declaration and all references.
    /// </summary>
    [Fact]
    public async Task Rename_AssignmentVariable_RenamesFromDeclarationSite()
    {
        // Line 0: "def main():"
        // Line 1: "    x = 5"
        //          "    " = 4 chars, "x" at col 4
        // Line 2: "    print(x)"
        //          "print(" = 10 chars, "x" at col 10
        var source = "def main():\n    x = 5\n    print(x)";

        // Cursor on "x" at assignment (declaration): line 1, col 4 (0-based)
        var result = await RenameAsync(source, 1, 4, "value");

        result.Should().NotBeNull("rename from assignment declaration should produce edits");
        result!.Changes.Should().NotBeNull();

        var uri = DocumentUri.From("file:///test.spy");
        result.Changes.Should().ContainKey(uri);

        var edits = result.Changes![uri].ToList();
        edits.Should().HaveCount(2,
            "the declaration and its one reference; the assignment target is recorded as a reference "
            + "too and collapses onto the declaration's range (#1263)");
        edits.Select(e => (e.Range.Start.Line, e.Range.Start.Character, e.Range.End.Character))
            .Should().OnlyHaveUniqueItems();

        // Declaration edit at line 1, col 4
        var declEdit = edits.FirstOrDefault(e => e.Range.Start.Line == 1);
        declEdit.Should().NotBeNull("should have an edit on the assignment declaration line");
        declEdit!.Range.Start.Character.Should().Be(4,
            "declaration edit should start at the name 'x' (col 4)");
        declEdit.Range.End.Character.Should().Be(4 + "x".Length,
            "declaration edit end should cover the full name");
        declEdit.NewText.Should().Be("value");

        // Reference edit at line 2 (print(x))
        var refEdit = edits.FirstOrDefault(e => e.Range.Start.Line == 2);
        refEdit.Should().NotBeNull("should have an edit at the reference site");
        refEdit!.NewText.Should().Be("value");
    }

    /// <summary>
    /// Regression test for #597: Rename from a const declaration site.
    /// `const y: int = 10` is parsed as a VariableDeclaration with IsConst=true.
    /// ResolveSymbol handles VariableDeclaration via the node-keyed
    /// <c>SemanticInfo.GetDeclarationSymbol</c> map (#1232).
    /// </summary>
    [Fact]
    public async Task Rename_ConstVariable_RenamesFromDeclarationSite()
    {
        // Line 0: "const MAX_SIZE: int = 10"
        //          "const " = 6 chars, "MAX_SIZE" at col 6
        // Line 1: "def main():"
        // Line 2: "    print(MAX_SIZE)"
        //          "print(" = 10 chars, "MAX_SIZE" at col 10
        var source = "const MAX_SIZE: int = 10\ndef main():\n    print(MAX_SIZE)";

        // Cursor on "MAX_SIZE" at const declaration: line 0, col 6 (0-based)
        var result = await RenameAsync(source, 0, 6, "LIMIT");

        result.Should().NotBeNull("rename from const declaration should produce edits");
        result!.Changes.Should().NotBeNull();

        var uri = DocumentUri.From("file:///test.spy");
        result.Changes.Should().ContainKey(uri);

        var edits = result.Changes![uri].ToList();
        edits.Should().HaveCount(2, "the const declaration and its one reference");
        edits.Select(e => (e.Range.Start.Line, e.Range.Start.Character, e.Range.End.Character))
            .Should().OnlyHaveUniqueItems();

        // Declaration edit at line 0, col 6
        var declEdit = edits.FirstOrDefault(e => e.Range.Start.Line == 0);
        declEdit.Should().NotBeNull("should have an edit on the const declaration line");
        declEdit!.Range.Start.Character.Should().Be(6,
            "declaration edit should start at the name 'MAX_SIZE' (col 6), not at 'const' (col 0)");
        declEdit.Range.End.Character.Should().Be(6 + "MAX_SIZE".Length,
            "declaration edit end should cover the full name");
        declEdit.NewText.Should().Be("LIMIT");

        // Reference edit at line 2 (print(MAX_SIZE))
        var refEdit = edits.FirstOrDefault(e => e.Range.Start.Line == 2);
        refEdit.Should().NotBeNull("should have an edit at the reference site");
        refEdit!.NewText.Should().Be("LIMIT");
    }

    /// <summary>
    /// Regression test for #1232: rename from a function-local declaration that nothing reads.
    /// <para>
    /// The handler used to resolve every declaration node through
    /// <c>FindSymbolByDeclaration</c>, a name-and-position scan over two reference-populated
    /// collections plus module scope. A local nobody references appears in none of the three, so
    /// rename resolved no symbol and silently did nothing. It now resolves through the node-keyed
    /// map the checker writes at the binding site — the same fix #1222 applied to inlay hints.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Rename_UnreferencedLocalDeclaration_RenamesFromDeclarationSite()
    {
        // Line 0: "def main():"
        // Line 1: "    const LIMIT: int = 10"
        //          "    const " = 10 chars, "LIMIT" at col 10
        // Line 2: "    print(1)"
        // Nothing reads LIMIT — that is the point. The trailing newline is #1233's repro
        // requirement, unrelated to this fix.
        var source = "def main():\n    const LIMIT: int = 10\n    print(1)\n";

        var result = await RenameAsync(source, 1, 10, "MAX");

        result.Should().NotBeNull("a local nothing references is still a declaration you can rename");
        result!.Changes.Should().NotBeNull();

        var uri = DocumentUri.From("file:///test.spy");
        result.Changes.Should().ContainKey(uri);

        var edits = result.Changes![uri].ToList();
        edits.Should().ContainSingle("there is exactly one occurrence: the declaration itself");
        edits[0].Range.Start.Line.Should().Be(1);
        edits[0].Range.Start.Character.Should().Be(10,
            "the edit covers the name 'LIMIT' (col 10), not 'const' (col 4)");
        edits[0].Range.End.Character.Should().Be(10 + "LIMIT".Length);
        edits[0].NewText.Should().Be("MAX");
    }

    /// <summary>
    /// The other half of #1232: the scan the handler no longer uses for this kind genuinely cannot
    /// answer for it, so the migration above is load-bearing rather than a preference.
    /// </summary>
    [Fact]
    public async Task UnreferencedLocalDeclaration_IsInvisibleToTheDeclarationScan()
    {
        var source = "def main():\n    const LIMIT: int = 10\n    print(1)\n";
        _workspace.OpenDocument("file:///scan.spy", source, 1);

        var analysis = await _languageService.GetAnalysisAsync("file:///scan.spy", CancellationToken.None);
        analysis.Should().NotBeNull();

        // Compiler coordinates: line 2, column 5 — the statement start of `const LIMIT: int = 10`.
        analysis!.SemanticInfo!.FindSymbolByDeclaration("LIMIT", 2, 5).Should().BeNull(
            "the scan reads reference-populated collections and module scope; a function-local "
            + "binding nothing reads is in none of them (#1232)");
    }

    /// <summary>
    /// #1232, measured sibling kind: a nested <c>def</c> nothing calls. It is in no reference
    /// collection and not in module scope, so the declaration scan could not see it either; the
    /// checker now records it keyed on the definition node.
    /// </summary>
    [Fact]
    public async Task Rename_UnreferencedNestedFunction_RenamesFromDeclarationSite()
    {
        // Line 0: "def outer() -> int:"
        // Line 1: "    def helper() -> int:"
        //          "    def " = 8 chars, "helper" at col 8
        // Line 2: "        return 1"
        // Line 3: "    return 2"
        var source = "def outer() -> int:\n    def helper() -> int:\n        return 1\n    return 2\n";

        var result = await RenameAsync(source, 1, 8, "assist");

        result.Should().NotBeNull("a nested def nothing calls is still a declaration you can rename");
        var edits = result!.Changes![DocumentUri.From("file:///test.spy")].ToList();
        edits.Should().ContainSingle();
        edits[0].Range.Start.Line.Should().Be(1);
        edits[0].Range.Start.Character.Should().Be(8);
        edits[0].NewText.Should().Be("assist");
    }

    /// <summary>
    /// #1232, measured sibling kind: an <c>except ... as</c> name the handler body never reads.
    /// </summary>
    [Fact]
    public async Task Rename_UnreferencedExceptAsName_RenamesFromDeclarationSite()
    {
        // Line 3: "    except Exception as err:"
        //          "    except Exception as " = 24 chars, "err" at col 24
        var source = "def main():\n    try:\n        print(1)\n    except Exception as err:\n        print(2)\n";

        var result = await RenameAsync(source, 3, 24, "problem");

        result.Should().NotBeNull("an except-as name nothing reads is still renameable");
        var edits = result!.Changes![DocumentUri.From("file:///test.spy")].ToList();
        edits.Should().ContainSingle();
        edits[0].Range.Start.Line.Should().Be(3);
        edits[0].Range.Start.Character.Should().Be(24);
        edits[0].NewText.Should().Be("problem");
    }

    /// <summary>
    /// #1232, measured sibling kind: a <c>with ... as</c> name. Both the unreferenced and the
    /// referenced case failed before — the scan matches on the symbol's declaration position, which
    /// for a with-item is not the position the handler passed it. Resolving node-keyed (through the
    /// map the checker already wrote, which rename simply never used) removes the mismatch.
    /// </summary>
    [Fact]
    public async Task Rename_WithAsName_RenamesFromDeclarationSite()
    {
        // Line 1: "    with open(\"f.txt\") as handle:"
        //          "    with open(\"f.txt\") as " = 26 chars, "handle" at col 26
        // Line 2: "        print(handle)"
        //          "        print(" = 14 chars, "handle" at col 14
        var source = "def main():\n    with open(\"f.txt\") as handle:\n        print(handle)\n";

        var result = await RenameAsync(source, 1, 26, "fh");

        result.Should().NotBeNull();
        var edits = result!.Changes![DocumentUri.From("file:///test.spy")].ToList();
        edits.Should().HaveCount(2, "the declaration and its one reference");
        edits.Should().Contain(e => e.Range.Start.Line == 1 && e.Range.Start.Character == 26);
        edits.Should().Contain(e => e.Range.Start.Line == 2 && e.Range.Start.Character == 14);
        edits.Should().OnlyContain(e => e.NewText == "fh");
    }

    /// <summary>
    /// #1232, measured sibling kind: the same with-<c>as</c> name when nothing reads it.
    /// </summary>
    [Fact]
    public async Task Rename_UnreferencedWithAsName_RenamesFromDeclarationSite()
    {
        var source = "def main():\n    with open(\"f.txt\") as handle:\n        print(1)\n";

        var result = await RenameAsync(source, 1, 26, "fh");

        result.Should().NotBeNull();
        var edits = result!.Changes![DocumentUri.From("file:///test.spy")].ToList();
        edits.Should().ContainSingle();
        edits[0].Range.Start.Line.Should().Be(1);
        edits[0].Range.Start.Character.Should().Be(26);
        edits[0].NewText.Should().Be("fh");
    }

    /// <summary>
    /// Regression test for #597: Rename from a for-loop variable declaration site.
    /// `for i in range(5)` has the loop target as an Identifier AST node.
    /// ResolveSymbol resolves it via GetIdentifierSymbol.
    /// </summary>
    [Fact]
    public async Task Rename_ForLoopVariable_RenamesFromDeclarationSite()
    {
        // Line 0: "def main():"
        // Line 1: "    for idx in range(5):"
        //          "    for " = 8 chars, "idx" at col 8
        // Line 2: "        print(idx)"
        //          "        print(" = 14 chars, "idx" at col 14
        var source = "def main():\n    for idx in range(5):\n        print(idx)";

        // Cursor on "idx" at for-loop declaration: line 1, col 8 (0-based)
        var result = await RenameAsync(source, 1, 8, "index");

        result.Should().NotBeNull("rename from for-loop variable declaration should produce edits");
        result!.Changes.Should().NotBeNull();

        var uri = DocumentUri.From("file:///test.spy");
        result.Changes.Should().ContainKey(uri);

        var edits = result.Changes![uri].ToList();
        edits.Should().HaveCount(2,
            "the loop target and its one reference; the target is an Identifier, so it is recorded "
            + "as a reference at the same range the declaration edit uses (#1263)");
        edits.Select(e => (e.Range.Start.Line, e.Range.Start.Character, e.Range.End.Character))
            .Should().OnlyHaveUniqueItems();

        // Declaration edit at line 1, col 8
        var declEdit = edits.FirstOrDefault(e => e.Range.Start.Line == 1);
        declEdit.Should().NotBeNull("should have an edit on the for-loop declaration line");
        declEdit!.Range.Start.Character.Should().Be(8,
            "declaration edit should start at the name 'idx' (col 8)");
        declEdit.Range.End.Character.Should().Be(8 + "idx".Length,
            "declaration edit end should cover the full name");
        declEdit.NewText.Should().Be("index");

        // Reference edit at line 2 (print(idx))
        var refEdit = edits.FirstOrDefault(e => e.Range.Start.Line == 2);
        refEdit.Should().NotBeNull("should have an edit at the reference site");
        refEdit!.NewText.Should().Be("index");
    }

    /// <summary>
    /// Regression test for #597: Rename from a for-tuple unpacking variable declaration site.
    /// `for x, y in items` has TupleLiteral as the loop target, with Identifier children.
    /// When the cursor is on one of the Identifier children, ResolveSymbol resolves it
    /// via GetIdentifierSymbol.
    /// </summary>
    [Fact]
    public async Task Rename_ForTupleVariable_RenamesFromDeclarationSite()
    {
        // Line 0: "def main():"
        // Line 1: "    items: list[tuple[str, int]] = [(\"a\", 1), (\"b\", 2)]"
        // Line 2: "    for key, val in items:"
        //          "    for " = 8 chars, "key" at col 8, ", " at col 11, "val" at col 13
        // Line 3: "        print(key)"
        //          "        print(" = 14 chars, "key" at col 14
        var source = "def main():\n    items: list[tuple[str, int]] = [(\"a\", 1), (\"b\", 2)]\n    for key, val in items:\n        print(key)";

        // Cursor on "key" at for-tuple declaration: line 2, col 8 (0-based)
        var result = await RenameAsync(source, 2, 8, "name");

        result.Should().NotBeNull("rename from for-tuple variable declaration should produce edits");
        result!.Changes.Should().NotBeNull();

        var uri = DocumentUri.From("file:///test.spy");
        result.Changes.Should().ContainKey(uri);

        var edits = result.Changes![uri].ToList();
        edits.Should().HaveCount(2,
            "the 'key' half of the tuple target and its one reference — 'val' is a different symbol "
            + "and must not be edited");
        edits.Select(e => (e.Range.Start.Line, e.Range.Start.Character, e.Range.End.Character))
            .Should().OnlyHaveUniqueItems();

        // Declaration edit at line 2, col 8
        var declEdit = edits.FirstOrDefault(e => e.Range.Start.Line == 2);
        declEdit.Should().NotBeNull("should have an edit on the for-tuple declaration line");
        declEdit!.Range.Start.Character.Should().Be(8,
            "declaration edit should start at the name 'key' (col 8)");
        declEdit.Range.End.Character.Should().Be(8 + "key".Length,
            "declaration edit end should cover the full name");
        declEdit.NewText.Should().Be("name");

        // Reference edit at line 3 (print(key))
        var refEdit = edits.FirstOrDefault(e => e.Range.Start.Line == 3);
        refEdit.Should().NotBeNull("should have an edit at the reference site");
        refEdit!.NewText.Should().Be("name");
    }

    /// <summary>
    /// Regression test for #597: Rename from a module-level variable declaration site.
    /// TypeChecker sets DeclarationSpan/DeclaringFilePath on non-const module-level variables.
    /// Note: In single-file test context, the declaration edit may be stored under a
    /// different URI key (TypeChecker uses "&lt;source&gt;" as file path), so we collect
    /// all edits across all URI keys to verify both declaration and reference edits exist.
    /// </summary>
    [Fact]
    public async Task Rename_ModuleLevelVariable_RenamesFromDeclarationSite()
    {
        var source = "counter: int = 0\ndef main():\n    print(counter)";

        var result = await RenameAsync(source, 0, 0, "total");

        result.Should().NotBeNull("rename from module-level variable declaration should produce edits");
        result!.Changes.Should().NotBeNull();

        var allEdits = result.Changes!.SelectMany(kv => kv.Value).ToList();
        allEdits.Should().HaveCount(2, "the declaration and its one reference");
        allEdits.Select(e => (e.Range.Start.Line, e.Range.Start.Character, e.Range.End.Character))
            .Should().OnlyHaveUniqueItems();
        result.Changes.Should().HaveCount(1,
            "the declaration's placeholder path maps onto the request document (#1262)");

        var declEdit = allEdits.FirstOrDefault(e => e.Range.Start.Line == 0);
        declEdit.Should().NotBeNull("should have an edit on the module-level declaration line");
        declEdit!.NewText.Should().Be("total");

        var refEdit = allEdits.FirstOrDefault(e => e.Range.Start.Line == 2);
        refEdit.Should().NotBeNull("should have an edit at the reference site");
        refEdit!.NewText.Should().Be("total");
    }

    /// <summary>
    /// Documents handler limitation for #597: ExceptHandler.Name is a string property,
    /// not an Identifier AST node, so FindNodeAtPosition cannot locate it.
    /// Rename from a reference site (the Identifier in the handler body) works;
    /// rename from the declaration-site name in "except ... as err" does not yet.
    /// </summary>
    [Fact]
    public async Task Rename_ExceptVariable_WorksFromReferenceSite()
    {
        var source = "def main():\n    try:\n        x: int = int(\"abc\")\n    except ValueError as err:\n        print(err)";

        // Rename from the REFERENCE site (print(err)) — line 4, col 14
        var result = await RenameAsync(source, 4, 14, "error");

        result.Should().NotBeNull("rename from except-variable reference should produce edits");
        result!.Changes.Should().NotBeNull();

        var allEdits = result.Changes!.SelectMany(kv => kv.Value).ToList();
        allEdits.Should().Contain(e => e.NewText == "error");
    }

    [Fact]
    public async Task Rename_WithVariable_WorksFromReferenceSite()
    {
        var source = "from System.IO import StringWriter\ndef main():\n    with StringWriter() as writer:\n        print(writer)";

        // Rename from the REFERENCE site (print(writer)) — line 3, col 14
        var result = await RenameAsync(source, 3, 14, "output");

        result.Should().NotBeNull("rename from with-variable reference should produce edits");
        result!.Changes.Should().NotBeNull();

        var allEdits = result.Changes!.SelectMany(kv => kv.Value).ToList();
        allEdits.Should().Contain(e => e.NewText == "output");
    }

    [Fact]
    public async Task Rename_ExceptVariable_WorksFromDeclarationSite()
    {
        var source = "def main():\n    try:\n        x: int = int(\"abc\")\n    except ValueError as err:\n        print(err)";

        // Rename from the DECLARATION site — cursor on "err" in "except ValueError as err:"
        // Line 3: "    except ValueError as err:"
        //          0123456789012345678901234567
        var result = await RenameAsync(source, 3, 27, "error");

        result.Should().NotBeNull("rename from except-variable declaration should produce edits");
        result!.Changes.Should().NotBeNull();

        var allEdits = result.Changes!.SelectMany(kv => kv.Value).ToList();
        allEdits.Should().HaveCount(2, "the except-as name and its one reference in the handler body");
        allEdits.Select(e => (e.Range.Start.Line, e.Range.Start.Character, e.Range.End.Character))
            .Should().OnlyHaveUniqueItems();
        allEdits.Should().Contain(e => e.Range.Start.Line == 3 && e.Range.Start.Character == 25,
            "'err' starts at col 25 — '    except ValueError as ' is 25 characters");
        allEdits.Should().Contain(e => e.Range.Start.Line == 4 && e.Range.Start.Character == 14);
        allEdits.Should().OnlyContain(e => e.NewText == "error");
    }

    [Fact]
    public async Task Rename_WithVariable_WorksFromDeclarationSite()
    {
        var source = "from System.IO import StringWriter\ndef main():\n    with StringWriter() as writer:\n        print(writer)";

        // Rename from the DECLARATION site — cursor on "writer" in "with StringWriter() as writer:"
        // Line 2: "    with StringWriter() as writer:"
        //          0123456789012345678901234567
        var result = await RenameAsync(source, 2, 27, "output");

        result.Should().NotBeNull("rename from with-variable declaration should produce edits");
        result!.Changes.Should().NotBeNull();

        var allEdits = result.Changes!.SelectMany(kv => kv.Value).ToList();
        allEdits.Should().HaveCount(2, "the with-as name and its one reference in the body");
        allEdits.Select(e => (e.Range.Start.Line, e.Range.Start.Character, e.Range.End.Character))
            .Should().OnlyHaveUniqueItems();
        allEdits.Should().Contain(e => e.Range.Start.Line == 2 && e.Range.Start.Character == 27);
        allEdits.Should().Contain(e => e.Range.Start.Line == 3 && e.Range.Start.Character == 14);
        allEdits.Should().OnlyContain(e => e.NewText == "output");
    }

    [Fact]
    public async Task Rename_TypedVariableDeclaration_WorksFromDeclarationSite()
    {
        var source = "def main():\n    count: int = 5\n    print(count)";

        // Rename from declaration site — cursor on "count" in "count: int = 5"
        // Line 1: "    count: int = 5"
        //          01234
        var result = await RenameAsync(source, 1, 4, "total");

        result.Should().NotBeNull("rename from typed variable declaration should produce edits");
        result!.Changes.Should().NotBeNull();

        var allEdits = result.Changes!.SelectMany(kv => kv.Value).ToList();
        allEdits.Should().HaveCount(2, "the typed declaration and its one reference");
        allEdits.Select(e => (e.Range.Start.Line, e.Range.Start.Character, e.Range.End.Character))
            .Should().OnlyHaveUniqueItems();
        allEdits.Should().Contain(e => e.Range.Start.Line == 1 && e.Range.Start.Character == 4);
        allEdits.Should().Contain(e => e.Range.Start.Line == 2 && e.Range.Start.Character == 10);
        allEdits.Should().OnlyContain(e => e.NewText == "total");
    }

    [Fact]
    public async Task Rename_TypedVariableDeclaration_NoInitializer()
    {
        var source = "def main():\n    count: int\n    print(count)";

        // Rename from declaration site — cursor on "count" in "count: int"
        // Line 1: "    count: int"
        //          01234
        var result = await RenameAsync(source, 1, 4, "total");

        result.Should().NotBeNull("rename from typed variable declaration without initializer should produce edits");
        result!.Changes.Should().NotBeNull();

        var allEdits = result.Changes!.SelectMany(kv => kv.Value).ToList();
        allEdits.Should().Contain(e => e.NewText == "total");
    }

    // === Backtick-escaped names (#1281) ===
    //
    // An escaped name occupies two more columns than its text: `event` is seven characters of
    // source for a five-character name. Every edit length here is the SOURCE extent, because an
    // edit sized to Name.Length replaces all but the closing backtick and leaves debris behind.

    /// <summary>
    /// #1281: renaming an escaped local rewrites the whole backticked extent at every occurrence,
    /// and — the target being an ordinary identifier — writes it bare.
    /// </summary>
    [Fact]
    public async Task Rename_EscapedLocal_ReplacesWholeBacktickedExtent()
    {
        // Line 1: "    `event`: int = 1"
        //          0123456789...  '`' at col 4, "event" at 5-9, '`' at 10 — extent [4, 11)
        // Line 2: "    print(`event`)"
        //          "    print(" = 10 chars — extent [10, 17)
        var source = "def main() -> None:\n    `event`: int = 1\n    print(`event`)\n";

        // Cursor on the 'e' of the escaped name.
        var result = await RenameAsync(source, 1, 5, "handler");

        result.Should().NotBeNull("an escaped declaration is renameable like any other");
        var edits = result!.Changes![DocumentUri.From("file:///test.spy")].ToList();

        edits.Should().HaveCount(2, "the declaration and its one reference");
        edits.Select(e => (e.Range.Start.Line, e.Range.Start.Character, e.Range.End.Character))
            .Should().OnlyHaveUniqueItems();

        var declEdit = edits.Single(e => e.Range.Start.Line == 1);
        declEdit.Range.Start.Character.Should().Be(4, "the name starts at its opening backtick");
        declEdit.Range.End.Character.Should().Be(11,
            "the extent covers both backticks — ending at 9 would leave a stray '`' behind");

        var refEdit = edits.Single(e => e.Range.Start.Line == 2);
        refEdit.Range.Start.Character.Should().Be(10);
        refEdit.Range.End.Character.Should().Be(17, "the reference is backticked too");

        edits.Should().OnlyContain(e => e.NewText == "handler",
            "'handler' needs no escape, so the backticks are dropped rather than carried along");
    }

    /// <summary>
    /// #1281: the same rename onto a name that cannot be written bare keeps the escape.
    /// </summary>
    [Fact]
    public async Task Rename_EscapedLocal_ToKeywordName_KeepsBackticks()
    {
        var source = "def main() -> None:\n    `event`: int = 1\n    print(`event`)\n";

        var result = await RenameAsync(source, 1, 5, "class");

        result.Should().NotBeNull();
        var edits = result!.Changes![DocumentUri.From("file:///test.spy")].ToList();

        edits.Should().HaveCount(2);
        edits.Should().OnlyContain(e => e.NewText == "`class`",
            "'class' is a keyword: the escape is the only spelling that reaches the identifier "
            + "namespace, so rename writes it escaped");
        edits.Should().Contain(e =>
            e.Range.Start.Line == 1 && e.Range.Start.Character == 4 && e.Range.End.Character == 11);
        edits.Should().Contain(e =>
            e.Range.Start.Line == 2 && e.Range.Start.Character == 10 && e.Range.End.Character == 17);
    }

    /// <summary>
    /// #1281: a bare symbol renamed onto a keyword spelling gets the backticks inserted. Refusing
    /// the rename (what the handler used to do) is worse: the name is legal, just not bare.
    /// </summary>
    [Fact]
    public async Task Rename_BareLocal_ToKeywordName_InsertsBackticks()
    {
        // Line 1: "    value: int = 1"   — "value" at [4, 9)
        // Line 2: "    print(value)"     — "value" at [10, 15)
        var source = "def main() -> None:\n    value: int = 1\n    print(value)\n";

        var result = await RenameAsync(source, 1, 4, "class");

        result.Should().NotBeNull("a keyword-colliding target is renameable escaped");
        var edits = result!.Changes![DocumentUri.From("file:///test.spy")].ToList();

        edits.Should().HaveCount(2);
        edits.Should().OnlyContain(e => e.NewText == "`class`");
        edits.Should().Contain(e =>
            e.Range.Start.Line == 1 && e.Range.Start.Character == 4 && e.Range.End.Character == 9,
            "the OLD name is bare, so its extent is unchanged by the new name's escape");
        edits.Should().Contain(e =>
            e.Range.Start.Line == 2 && e.Range.Start.Character == 10 && e.Range.End.Character == 15);
    }

    /// <summary>
    /// #1281: an explicitly escaped request is honored even where the escape is not required —
    /// that is how a user asks to shadow a builtin deliberately rather than by accident (SPY0483).
    /// </summary>
    [Fact]
    public async Task Rename_ToExplicitlyEscapedBuiltinName_KeepsBackticks()
    {
        var source = "def main() -> None:\n    value: int = 1\n    print(value)\n";

        var result = await RenameAsync(source, 1, 4, "`len`");

        result.Should().NotBeNull();
        var edits = result!.Changes![DocumentUri.From("file:///test.spy")].ToList();

        edits.Should().HaveCount(2);
        edits.Should().OnlyContain(e => e.NewText == "`len`",
            "the request escaped the name, and dropping the backticks would silently change which "
            + "namespace the spelling denotes");
    }

    /// <summary>
    /// #1281: an escaped class — the declaration extent is the class-name token's, backticks
    /// included.
    /// </summary>
    [Fact]
    public async Task Rename_EscapedClass_ReplacesWholeBacktickedExtent()
    {
        // Line 0: "class `event`:"       — "class " = 6 chars, extent [6, 13)
        // Line 4: "    e = `event`()"    — "    e = " = 8 chars, extent [8, 15)
        var source = "class `event`:\n    def __init__(self):\n        pass\ndef main() -> None:\n    e = `event`()";

        var result = await RenameAsync(source, 0, 7, "Handler");

        result.Should().NotBeNull("an escaped class name is renameable");
        var edits = result!.Changes![DocumentUri.From("file:///test.spy")].ToList();

        edits.Should().HaveCount(2, "the declaration and the construction site");
        edits.Select(e => (e.Range.Start.Line, e.Range.Start.Character, e.Range.End.Character))
            .Should().OnlyHaveUniqueItems();

        var declEdit = edits.Single(e => e.Range.Start.Line == 0);
        declEdit.Range.Start.Character.Should().Be(6, "the name starts at its opening backtick");
        declEdit.Range.End.Character.Should().Be(13, "the extent covers both backticks");

        var refEdit = edits.Single(e => e.Range.Start.Line == 4);
        refEdit.Range.Start.Character.Should().Be(8);
        refEdit.Range.End.Character.Should().Be(15, "the construction site is backticked too");

        edits.Should().OnlyContain(e => e.NewText == "Handler");
    }

    /// <summary>
    /// #1281: an escaped <c>except ... as</c> name, the binding form whose escape flag reaches the
    /// symbol through the type checker rather than name resolution.
    /// </summary>
    [Fact]
    public async Task Rename_EscapedExceptAsName_ReplacesWholeBacktickedExtent()
    {
        // Line 3: "    except ValueError as `event`:"  — '`' at col 25, extent [25, 32)
        // Line 4: "        print(`event`)"             — '`' at col 14, extent [14, 21)
        var source = "def main() -> None:\n    try:\n        print(1)\n"
            + "    except ValueError as `event`:\n        print(`event`)\n";

        var result = await RenameAsync(source, 3, 26, "problem");

        result.Should().NotBeNull();
        var edits = result!.Changes![DocumentUri.From("file:///test.spy")].ToList();

        edits.Should().HaveCount(2, "the except-as name and its one reference");
        edits.Should().Contain(e =>
            e.Range.Start.Line == 3 && e.Range.Start.Character == 25 && e.Range.End.Character == 32);
        edits.Should().Contain(e =>
            e.Range.Start.Line == 4 && e.Range.Start.Character == 14 && e.Range.End.Character == 21);
        edits.Should().OnlyContain(e => e.NewText == "problem");
    }

    /// <summary>
    /// #1281: the cursor may sit anywhere on an escaped name, including the characters the bare
    /// spelling's length does not reach.
    /// </summary>
    [Theory]
    [InlineData(4)]   // opening backtick
    [InlineData(5)]   // 'e'
    [InlineData(9)]   // 't' — past `Name.Length` counted from the backtick
    public async Task Rename_EscapedLocal_ResolvesFromAnywhereOnTheName(int cursorCharacter)
    {
        var source = "def main() -> None:\n    `event`: int = 1\n    print(`event`)\n";

        var result = await RenameAsync(source, 1, cursorCharacter, "handler");

        result.Should().NotBeNull("the whole backticked extent is the name");
        result!.Changes![DocumentUri.From("file:///test.spy")].Should().HaveCount(2);
    }

    /// <summary>
    /// #1379: a NESTED def's escape never reaches its symbol — <c>TypeChecker.Definitions.cs</c>
    /// builds the nested <c>FunctionSymbol</c> without <c>IsNameBacktickEscaped</c>, where its
    /// module-level counterpart in <c>NameResolver.Members.cs</c> sets it.
    /// </summary>
    /// <remarks>
    /// MEASURED before the fix: the declaration edit came out <c>(1,8)-(1,13)</c> — sized from the
    /// bare name — while the reference edit was <c>(3,11)-(3,18)</c> and correct, because reference
    /// extents are read from the recorded token span rather than from the flag. The same missing
    /// flag made this program fail to compile at all (the nested def was emitted <c>int Event()</c>
    /// and called <c>@event()</c>, CS0103), so the rename gap is the smaller half of #1379 — and the
    /// two halves needed two edits, because the emitter mangles the AST node's name directly and
    /// never consults the symbol. The codegen half is pinned separately by the
    /// <c>basics/backtick_nested_def</c> fixture.
    /// </remarks>
    [Fact]
    public async Task Rename_NestedEscapedDef_ReplacesWholeBacktickedExtent()
    {
        // Line 1: "    def `event`() -> int:"  — '`' at col 8, extent [8, 15)
        // Line 3: "    return `event`()"       — '`' at col 11, extent [11, 18)
        var source = "def outer() -> int:\n    def `event`() -> int:\n        return 1\n    return `event`()\n";

        var result = await RenameAsync(source, 1, 9, "helper");

        result.Should().NotBeNull();
        var edits = result!.Changes![DocumentUri.From("file:///test.spy")].ToList();
        edits.Should().HaveCount(2);
        edits.Should().Contain(e =>
            e.Range.Start.Line == 1 && e.Range.Start.Character == 8 && e.Range.End.Character == 15);
        edits.Should().Contain(e =>
            e.Range.Start.Line == 3 && e.Range.Start.Character == 11 && e.Range.End.Character == 18);
    }

    /// <summary>
    /// #1281: a bare use of a builtin is the builtin, and the builtin is not the user's to rename.
    /// </summary>
    [Fact]
    public async Task Rename_BareBuiltinUse_IsRefused()
    {
        // Line 1: "    print(len([1, 2]))" — "len" at col 10
        var source = "def main() -> None:\n    print(len([1, 2]))\n";

        var result = await RenameAsync(source, 1, 10, "size");

        result.Should().BeNull("renaming 'len' would rewrite a call site of a symbol declared in no "
            + "document");
    }

    /// <summary>
    /// #1281: what the escape admits is a legal identifier wearing backticks — not any text at all.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1bad")]
    [InlineData("has space")]
    [InlineData("`")]
    [InlineData("``")]
    [InlineData("`1bad`")]
    [InlineData("`unterminated")]
    [InlineData("fo`o")]
    public async Task Rename_ToUnspellableName_IsRefused(string newName)
    {
        var source = "def main() -> None:\n    value: int = 1\n    print(value)\n";

        var result = await RenameAsync(source, 1, 4, newName);

        result.Should().BeNull($"'{newName}' is not a name Sharpy can spell");
    }

    public void Dispose()
    {
        _languageService.Dispose();
        _workspace.Dispose();
    }
}
