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
    /// #1380/#1454: the cursor on the CLOSING backtick of an escaped occurrence is on the name.
    ///
    /// <para>Both halves are covered because they answer through different machinery. The
    /// declaration cursor hit-tests the extent the PARSER recorded on the node
    /// (<c>NameColumnStart</c>..<c>NameColumnEnd</c>), while the reference cursor is resolved by
    /// <c>FindNodeAtPosition</c> against the identifier token's recorded span (#1281). Before #1454
    /// the declaration half was a handler-side reconstruction — start plus <c>Name.Length</c> plus a
    /// backtick constant — that happened to agree; the point of the recording is that agreement is
    /// no longer a coincidence to be maintained. That is what the mutation check on
    /// <c>NameExtentLength</c> measures.</para>
    /// </summary>
    [Theory]
    // Line 1: "    `event`: int = 1"  — declaration extent [4, 11), closing backtick at 10
    [InlineData(1, 10, "the declaration's closing backtick")]
    // Line 2: "    print(`event`)"    — reference extent [10, 17), closing backtick at 16
    [InlineData(2, 16, "the reference's closing backtick")]
    public async Task Rename_CursorOnClosingBacktick_RenamesTheWholeName(
        int cursorLine, int cursorChar, string because)
    {
        var source = "def main() -> None:\n    `event`: int = 1\n    print(`event`)\n";

        var result = await RenameAsync(source, cursorLine, cursorChar, "handler");

        result.Should().NotBeNull($"a cursor on {because} is a cursor on the name");
        var edits = result!.Changes![DocumentUri.From("file:///test.spy")].ToList();

        edits.Should().HaveCount(2, "the declaration and its one reference, wherever the cursor sat");
        edits.Should().Contain(e =>
            e.Range.Start.Line == 1 && e.Range.Start.Character == 4 && e.Range.End.Character == 11);
        edits.Should().Contain(e =>
            e.Range.Start.Line == 2 && e.Range.Start.Character == 10 && e.Range.End.Character == 17);
        edits.Should().OnlyContain(e => e.NewText == "handler");
    }

    /// <summary>
    /// The negative control for the cell above: one column past the closing backtick is NOT on the
    /// name. Without this, an extent that over-reached by any amount would still pass.
    /// </summary>
    [Fact]
    public async Task Rename_CursorPastTheClosingBacktick_IsNotOnTheName()
    {
        // Line 1: "    `event`: int = 1" — column 11 is the ':' following the extent [4, 11).
        var source = "def main() -> None:\n    `event`: int = 1\n    print(`event`)\n";

        var result = await RenameAsync(source, 1, 11, "handler");

        result.Should().BeNull(
            "column 11 is the ':' after the name — an extent that answered here would be reaching "
            + "past what the parser measured");
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

    // ── parameter rename (#1359) ─────────────────────────────────────────────
    // Before this, `RenameHandler` contained zero occurrences of "Parameter" and the checker built
    // parameter symbols with all four position fields explicitly null — so rename returned no edits
    // from EITHER cursor, bailing at the no-source-location guard before any resolution arm ran.

    /// <summary>The issue's repro: renaming a parameter from its DECLARATION.</summary>
    [Fact]
    public async Task Rename_Parameter_FromDeclaration_RenamesBothOccurrences()
    {
        // Line 0: "def f(target: int) -> int:"   'target' at cols 6-11
        // Line 1: "    return target * 2"        'target' at cols 11-16
        var source = "def f(target: int) -> int:\n    return target * 2\n";

        var result = await RenameAsync(source, 0, 6, "scale");

        result.Should().NotBeNull("a parameter is a renameable binding like any other (#1359)");
        var edits = result!.Changes![DocumentUri.From("file:///test.spy")].ToList();

        edits.Should().HaveCount(2, "the declaration and its one reference");
        edits.Should().OnlyContain(e => e.NewText == "scale");

        var declEdit = edits.Single(e => e.Range.Start.Line == 0);
        declEdit.Range.Start.Character.Should().Be(6);
        declEdit.Range.End.Character.Should().Be(12);

        var refEdit = edits.Single(e => e.Range.Start.Line == 1);
        refEdit.Range.Start.Character.Should().Be(11);
        refEdit.Range.End.Character.Should().Be(17);
    }

    /// <summary>The same rename driven from a REFERENCE cursor.</summary>
    [Fact]
    public async Task Rename_Parameter_FromReference_RenamesBothOccurrences()
    {
        var source = "def f(target: int) -> int:\n    return target * 2\n";

        var result = await RenameAsync(source, 1, 11, "scale");

        result.Should().NotBeNull(
            "resolution succeeds from a reference; it was the missing declaration position that "
            + "made the handler bail before emitting anything");
        var edits = result!.Changes![DocumentUri.From("file:///test.spy")].ToList();

        edits.Should().HaveCount(2);
        edits.Select(e => e.Range.Start.Line).Should().BeEquivalentTo(new[] { 0, 1 });
    }

    /// <summary>
    /// A parameter nothing references still renames. This is what the node-keyed map buys: there is
    /// no reference collection to find it in and no module-scope entry either, so the
    /// name-and-position declaration scan cannot answer for it.
    /// </summary>
    [Fact]
    public async Task Rename_UnreferencedParameter_StillRenamesItsDeclaration()
    {
        var source = "def f(unused: int) -> int:\n    return 1\n";

        var result = await RenameAsync(source, 0, 6, "ignored");

        result.Should().NotBeNull("the node-keyed map is the only route to this symbol");
        var edits = result!.Changes![DocumentUri.From("file:///test.spy")].ToList();

        edits.Should().ContainSingle();
        edits[0].Range.Start.Line.Should().Be(0);
        edits[0].Range.Start.Character.Should().Be(6);
        edits[0].Range.End.Character.Should().Be(12);
        edits[0].NewText.Should().Be("ignored");
    }

    /// <summary>Renaming <c>*args</c> rewrites the name and leaves the star alone.</summary>
    [Fact]
    public async Task Rename_VariadicParameter_DoesNotEatTheStar()
    {
        // Line 0: "def f(*args: int) -> int:"  '*' at col 6, 'args' at cols 7-10
        var source = "def f(*args: int) -> int:\n    return len(args)\n";

        var result = await RenameAsync(source, 0, 7, "values");

        result.Should().NotBeNull();
        var edits = result!.Changes![DocumentUri.From("file:///test.spy")].ToList();

        var declEdit = edits.Single(e => e.Range.Start.Line == 0);
        declEdit.Range.Start.Character.Should().Be(7,
            "the edit starts at 'args', not at the '*' the statement start points to");
        declEdit.Range.End.Character.Should().Be(11);
    }

    /// <summary>An escaped parameter renames across its whole backticked extent.</summary>
    [Fact]
    public async Task Rename_EscapedParameter_ReplacesWholeBacktickedExtent()
    {
        // Line 0: "def f(`event`: int) -> int:"  '`' at col 6, extent [6, 13)
        var source = "def f(`event`: int) -> int:\n    return `event`\n";

        var result = await RenameAsync(source, 0, 7, "handler");

        result.Should().NotBeNull();
        var edits = result!.Changes![DocumentUri.From("file:///test.spy")].ToList();

        var declEdit = edits.Single(e => e.Range.Start.Line == 0);
        declEdit.Range.Start.Character.Should().Be(6, "the name starts at its opening backtick");
        declEdit.Range.End.Character.Should().Be(13,
            "the extent covers both backticks — ending at 11 would leave a stray '`' behind");
        declEdit.NewText.Should().Be("handler");
    }

    /// <summary>
    /// A cursor on the closing backtick of an escaped parameter is still on the name. This is the
    /// recorded extent doing the work — nothing here re-derives it from <c>Name.Length</c>.
    /// </summary>
    [Fact]
    public async Task Rename_EscapedParameter_FromClosingBacktick_Resolves()
    {
        var source = "def f(`event`: int) -> int:\n    return `event`\n";

        var result = await RenameAsync(source, 0, 12, "handler");

        result.Should().NotBeNull("column 12 is the closing backtick, which is part of the name");
        result!.Changes![DocumentUri.From("file:///test.spy")].Should().NotBeEmpty();
    }

    /// <summary>A method parameter reaches the same arm through the ordinary walk.</summary>
    [Fact]
    public async Task Rename_MethodParameter_RenamesBothOccurrences()
    {
        var source = "class Box:\n    def scale(self, factor: int) -> int:\n        return factor * 2\n";

        var result = await RenameAsync(source, 1, 20, "ratio");

        result.Should().NotBeNull();
        var edits = result!.Changes![DocumentUri.From("file:///test.spy")].ToList();

        edits.Should().HaveCount(2);
        edits.Should().OnlyContain(e => e.NewText == "ratio");
    }

    /// <summary>
    /// Negative control: a cursor in the gap between the parameter list and the body is on no name,
    /// so the arm must decline rather than claim the nearest parameter.
    /// </summary>
    [Fact]
    public async Task Rename_CursorOnParameterTypeAnnotation_IsNotTheParameter()
    {
        // Line 0: "def f(target: int) -> int:"  'int' annotation at cols 14-16
        var source = "def f(target: int) -> int:\n    return target * 2\n";

        var result = await RenameAsync(source, 0, 14, "scale");

        if (result?.Changes != null && result.Changes.TryGetValue(
                DocumentUri.From("file:///test.spy"), out var edits))
        {
            edits.Should().NotContain(e => e.Range.Start.Line == 0 && e.Range.Start.Character == 6,
                "the cursor is on the type annotation, not on the parameter's name");
        }
    }

    // ── spelling-complete rename over a binding chain (#1359 defect 2) ───────
    // A plain `x = ...` defines a FRESH symbol replacing the previous one, so each binding's
    // reference collection stops at the next rebinding. Measured before the fix, the issue's repro
    // did not merely "leave an occurrence behind" — it SPLIT into two disjoint halves (decl+RHS
    // read, then rebind-target+later use), and each half's edit produced source that still compiled
    // while meaning something else.

    /// <summary>Every cursor in a rebound spelling renames the whole chain, identically.</summary>
    [Theory]
    [InlineData(1, 4)]   // the declaration
    [InlineData(2, 4)]   // the rebinding's target
    [InlineData(2, 12)]  // the read on the rebinding's right-hand side
    [InlineData(3, 10)]  // a use after the rebinding
    public async Task Rename_ReboundLocal_RenamesEveryOccurrence_FromAnyCursor(int line, int col)
    {
        // L1: "    count: int = 0"      'count' at 4-8
        // L2: "    count = count + 1"   'count' at 4-8 and 12-16
        // L3: "    print(count)"        'count' at 10-14
        var source = "def main() -> None:\n    count: int = 0\n    count = count + 1\n    print(count)\n";

        var result = await RenameAsync(source, line, col, "total");

        result.Should().NotBeNull();
        var edits = result!.Changes![DocumentUri.From("file:///test.spy")].ToList();

        edits.Select(e => (e.Range.Start.Line, e.Range.Start.Character))
            .Should().BeEquivalentTo(new[] { (1, 4), (2, 4), (2, 12), (3, 10) },
                "all four occurrences are the same variable — the emitter assigns to one C# local "
                + "and versions only on redeclaration, so renaming a fragment is silently wrong");
        edits.Should().OnlyContain(e => e.NewText == "total");
    }

    /// <summary>
    /// Negative control: the same spelling bound in a DIFFERENT function is a different variable and
    /// must not be dragged along.
    /// </summary>
    [Fact]
    public async Task Rename_ReboundLocal_LeavesTheSameSpellingInAnotherFunctionAlone()
    {
        var source =
            "def first() -> None:\n"
            + "    count: int = 0\n"
            + "    count = count + 1\n"
            + "\n"
            + "\n"
            + "def second() -> None:\n"
            + "    count: int = 9\n"
            + "    print(count)\n";

        var result = await RenameAsync(source, 1, 4, "total");

        result.Should().NotBeNull();
        var edits = result!.Changes![DocumentUri.From("file:///test.spy")].ToList();

        edits.Select(e => e.Range.Start.Line).Should().OnlyContain(l => l == 1 || l == 2,
            "second()'s 'count' is a different variable that happens to share a spelling");
    }

    /// <summary>
    /// A REDECLARATION is a different variable and must not chain: the emitter versions those
    /// (<c>x</c>, <c>x_1</c>) rather than assigning to one local, so renaming one must not touch
    /// the other. This is the boundary that keeps the chain honest.
    /// </summary>
    [Fact]
    public async Task Rename_Redeclaration_DoesNotChainToTheOtherBinding()
    {
        // Two DECLARATIONS of `value`, not a reassignment: separate variables to the emitter.
        var source =
            "def main() -> None:\n"
            + "    value: int = 1\n"
            + "    print(value)\n"
            + "    value: str = \"a\"\n"
            + "    print(value)\n";

        var result = await RenameAsync(source, 1, 4, "first_value");

        result.Should().NotBeNull();
        var edits = result!.Changes![DocumentUri.From("file:///test.spy")].ToList();

        edits.Select(e => e.Range.Start.Line).Should().NotContain(3,
            "the second declaration binds a different variable — chaining it would rename across "
            + "two distinct C# locals");
    }

    public void Dispose()
    {
        _languageService.Dispose();
        _workspace.Dispose();
    }
}
