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

    public void Dispose()
    {
        _languageService.Dispose();
        _workspace.Dispose();
    }
}
