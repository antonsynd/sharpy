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
        _handler = new SharpyRenameHandler(_workspace, _languageService, _api);
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
        edits.Should().HaveCountGreaterThanOrEqualTo(2, "should have edits for declaration and reference");

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
    /// ResolveSymbol handles VariableDeclaration via FindSymbolByDeclaration.
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
        edits.Should().HaveCountGreaterThanOrEqualTo(2, "should have edits for declaration and reference");

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
        edits.Should().HaveCountGreaterThanOrEqualTo(2, "should have edits for declaration and reference");

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
        edits.Should().HaveCountGreaterThanOrEqualTo(2, "should have edits for declaration and reference");

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
        allEdits.Should().HaveCountGreaterThanOrEqualTo(2,
            "should have edits for declaration and reference (possibly across URI keys)");

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

    /// <summary>
    /// Documents handler limitation for #597: WithItem.Name is a string property,
    /// not an Identifier AST node, so FindNodeAtPosition cannot locate it.
    /// Rename from a reference site (the Identifier in the with body) works;
    /// rename from the declaration-site name in "with ... as writer" does not yet.
    /// </summary>
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

    public void Dispose()
    {
        _languageService.Dispose();
        _workspace.Dispose();
    }
}
