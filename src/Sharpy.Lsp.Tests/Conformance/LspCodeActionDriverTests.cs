using FluentAssertions;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Lsp.Refactoring;
using Xunit;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sharpy.Lsp.Tests.Conformance;

/// <summary>
/// The instrument the code-action shape sweep reads (#2232, plan P47): the provider roster's totality, one
/// request per provider through the real handler, and the driver's edit extraction and retention.
/// </summary>
public sealed class LspCodeActionDriverTests : IDisposable
{
    private readonly LspCodeActionDriver _driver = new(new CompilerApi(Sharpy.Compiler.Logging.NullLogger.Instance, StdlibReferences.Paths()));

    public void Dispose() => _driver.Dispose();

    private static LspRange R(int startLine, int startChar, int endLine, int endChar)
        => new(new Position(startLine, startChar), new Position(endLine, endChar));

    // ---- the roster ----

    /// <summary>
    /// Every concrete <see cref="ICodeActionProvider"/> of the <c>Sharpy.Lsp</c> assembly is in
    /// <see cref="LspCodeActionDriver.Providers"/> and nothing else is: a provider added to the server joins
    /// the sweep or this test is red. Anchored to the literal 7 so the reflection cannot agree with itself
    /// over an empty set.
    /// </summary>
    [Fact]
    public void Providers_AreTotalOverTheCodeActionProvidersOfTheLspAssembly()
    {
        var implementors = typeof(SharpyWorkspace).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface && typeof(ICodeActionProvider).IsAssignableFrom(t))
            .Select(t => t.FullName!)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        var roster = LspCodeActionDriver.Providers.Select(p => p.GetType().FullName!).ToArray();

        roster.Should().OnlyHaveUniqueItems();
        roster.OrderBy(n => n, StringComparer.Ordinal).Should().Equal(implementors,
            "LspCodeActionDriver.Providers must hold exactly the code-action providers the server can register");
        LspCodeActionDriver.Providers.Length.Should().Be(7);
    }

    // ---- one request per provider through the real handler ----

    private const string UnusedImport = "import math\n\ndef main():\n    print(1)\n";
    private const string Interface = "interface Drawable:\n    def draw(self) -> None:\n        ...\n\nclass Circle(Drawable):\n"
        + "    def __init__(self):\n        pass\n\ndef main():\n    c: Circle = Circle()\n    print(c)\n";
    private const string Expression = "def main():\n    x = 1 + 2\n    print(x)\n";
    private const string Statements = "def main():\n    a = 1\n    print(a)\n";
    private const string IfChain = "def main():\n    x = 1\n    if x == 1:\n        print(1)\n    elif x == 2:\n        print(2)\n";
    private const string Declaration = "def main():\n    k: int = 1 + 2\n    print(k)\n";

    /// <summary>
    /// Each provider, through the handler that aggregates all seven, offers its action on a hand document,
    /// and the action's edits apply strictly to a different text. The quick-fix row hands the handler the
    /// document's published SPY0452 diagnostic as its context, as a client does.
    /// </summary>
    [Theory]
    [InlineData(nameof(DiagnosticQuickFixProvider), UnusedImport, 0, 0, 0, 0, "Remove unused import")]
    [InlineData(nameof(OrganizeImportsProvider), UnusedImport, 0, 0, 0, 0, "Organize Imports")]
    [InlineData(nameof(ImplementInterfaceProvider), Interface, 4, 6, 4, 6, "Implement interface 'Drawable'")]
    [InlineData(nameof(ExtractVariableProvider), Expression, 1, 8, 1, 13, "Extract variable")]
    [InlineData(nameof(ExtractMethodProvider), Statements, 1, 0, 2, 12, "Extract method")]
    [InlineData(nameof(ConvertFormsProvider), IfChain, 2, 4, 2, 4, "Convert to match statement")]
    [InlineData(nameof(InlineProvider), Declaration, 1, 4, 1, 4, "Inline variable 'k'")]
    public void Request_EachProvider_OffersItsActionThroughTheHandler(
        string provider, string text, int sl, int sc, int el, int ec, string titlePrefix)
    {
        LspCodeActionDriver.Providers.Select(p => p.GetType().Name).Should().Contain(provider);
        string applied;
        using (var session = _driver.Open(text))
        {
            var context = provider == nameof(DiagnosticQuickFixProvider)
                ? session.Diagnostics.Where(d => d.Code?.String == DiagnosticCodes.Validation.UnusedImport).ToArray()
                : Array.Empty<Diagnostic>();
            if (provider == nameof(DiagnosticQuickFixProvider))
                context.Should().ContainSingle("the hand document has exactly one unused import");

            var actions = session.Request(R(sl, sc, el, ec), context);
            var action = actions.Should().ContainSingle(a => a.Title.StartsWith(titlePrefix, StringComparison.Ordinal),
                $"{provider} must offer '{titlePrefix}' on its hand document; offered: {string.Join(" | ", actions.Select(a => a.Title))}").Subject;
            applied = session.Apply(action);
            _driver.OpenDocumentCount.Should().Be(1, "the session holds its document open across requests");
        }

        applied.Should().NotBe(text, "the action's edits change the document");
        _driver.OpenDocumentCount.Should().Be(0, "closing the session closes its document");
    }

    [Fact]
    public void Request_OneShot_OpensHandlesAndCloses()
    {
        var actions = _driver.Request(IfChain, R(2, 4, 2, 4));
        actions.Select(a => a.Title).Should().Contain("Convert to match statement");
        _driver.OpenDocumentCount.Should().Be(0);
    }

    [Fact]
    public void Session_AnalysesOnceAcrossRequests()
    {
        using var session = _driver.Open(IfChain);
        var first = session.Analysis;
        session.Request(R(2, 4, 2, 4));
        session.Request(R(1, 4, 1, 4));
        first.Should().NotBeNull();
        session.Analysis.Should().BeSameAs(first);
    }

    // ---- edit extraction: the refusals (each paired with the accepted shape above) ----

    private static CodeAction ActionWith(WorkspaceEdit? edit) => new() { Title = "probe", Edit = edit };

    private static readonly DocumentUri Doc = DocumentUri.From("file:///probe/a.spy");
    private static readonly DocumentUri Other = DocumentUri.From("file:///probe/b.spy");

    private static WorkspaceEdit Changes(params (DocumentUri Uri, TextEdit[] Edits)[] entries) => new()
    {
        Changes = entries.ToDictionary(e => e.Uri, e => (IEnumerable<TextEdit>)e.Edits),
    };

    private static readonly TextEdit Insert = new() { Range = R(0, 0, 0, 0), NewText = "X" };

    [Fact]
    public void EditsOf_TheRequestedDocumentsEdits_Apply()
        => LspCodeActionDriver.Apply("ab", ActionWith(Changes((Doc, new[] { Insert }))), Doc).Should().Be("Xab");

    [Fact]
    public void EditsOf_NoEdit_Throws()
        => FluentActions.Invoking(() => LspCodeActionDriver.EditsOf(ActionWith(null), Doc))
            .Should().Throw<InvalidOperationException>().WithMessage("*no edit*");

    [Fact]
    public void EditsOf_AnotherDocument_Throws()
        => FluentActions.Invoking(() => LspCodeActionDriver.EditsOf(ActionWith(Changes((Other, new[] { Insert }))), Doc))
            .Should().Throw<InvalidOperationException>().WithMessage("*not exactly the requested document*");

    [Fact]
    public void EditsOf_TwoDocuments_Throws()
        => FluentActions.Invoking(() => LspCodeActionDriver.EditsOf(
                ActionWith(Changes((Doc, new[] { Insert }), (Other, new[] { Insert }))), Doc))
            .Should().Throw<InvalidOperationException>().WithMessage("*not exactly the requested document*");

    [Fact]
    public void EditsOf_DocumentChanges_Throws()
        => FluentActions.Invoking(() => LspCodeActionDriver.EditsOf(
                ActionWith(new WorkspaceEdit { DocumentChanges = new Container<WorkspaceEditDocumentChange>() }), Doc))
            .Should().Throw<InvalidOperationException>().WithMessage("*DocumentChanges*");

    [Fact]
    public void Apply_IllegalEdit_ThrowsTheStrictApplierRefusal()
        => FluentActions.Invoking(() => LspCodeActionDriver.Apply("ab",
                ActionWith(Changes((Doc, new[] { new TextEdit { Range = R(0, 3, 0, 3), NewText = "X" } }))), Doc))
            .Should().Throw<StrictEditApplicationException>();
}
