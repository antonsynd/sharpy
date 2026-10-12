using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Lsp.Handlers;
using Sharpy.Lsp.Refactoring;
// The enclosing `Sharpy` namespace exposes Core's Pythonic collections, which shadow System's; SCG-qualify.
using SCG = System.Collections.Generic;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sharpy.Lsp.Tests.Conformance;

/// <summary>
/// Drives the REAL <c>textDocument/codeAction</c> handler (<see cref="SharpyCodeActionHandler"/>) over the
/// seven production <see cref="ICodeActionProvider"/>s the way a client does, and applies an action's edits
/// the way a client must (#2232, plan P47): a document is opened under a fresh URI, requested against, and
/// closed in a <c>finally</c>; an action's <see cref="WorkspaceEdit"/> is applied by
/// <see cref="LspFormattingDriver.ApplyStrict"/>, which refuses any edit list a conforming client would
/// have to reject or could only guess at.
///
/// <para>
/// <b>One analysis per document.</b> A <see cref="Session"/> keeps its document open across any number of
/// requests: the handler reads the document's analysis through <see cref="LanguageService.GetAnalysisAsync"/>,
/// which the workspace caches per document, so a sweep pays one semantic analysis per (stem, twin), not per
/// request. <see cref="Request(string, LspRange, SCG.IEnumerable{Diagnostic}?)"/> is the one-shot form
/// (open → handle → close). The shape is P22e's <c>LspFormattingDriver.WithOpenDocument</c>, copied rather
/// than shared so the formatting driver's surface stays what P22e verified.
/// </para>
///
/// <para>
/// <b>Thread safety.</b> As <see cref="LspFormattingDriver"/>: the workspace's tables are concurrent, the
/// handler and providers hold no per-request state, and every session opens a fresh URI, so concurrent
/// sessions never see each other's text. Nothing outlives a session (<see cref="OpenDocumentCount"/>).
/// </para>
/// </summary>
internal sealed class LspCodeActionDriver : IDisposable
{
    private static long s_nextDocumentId;

    private readonly SharpyWorkspace _workspace;
    private readonly LanguageService _languageService;
    private readonly SharpyCodeActionHandler _handler;

    /// <param name="api">
    /// The compiler the workspace analyses with. A corpus sweep passes one carrying the Core and Stdlib
    /// references (<see cref="StdlibReferences"/>), as the server does; the default resolves neither.
    /// </param>
    public LspCodeActionDriver(CompilerApi? api = null)
    {
        api ??= new CompilerApi();
        _workspace = new SharpyWorkspace(api, NullLogger<SharpyWorkspace>.Instance);
        _languageService = new LanguageService(_workspace, api, NullLogger<LanguageService>.Instance);
        _handler = new SharpyCodeActionHandler(_languageService, Providers);
    }

    /// <summary>
    /// The roster: the seven production providers, in the order <c>Program.cs</c> registers them. Its totality
    /// against the <c>Sharpy.Lsp</c> assembly is a test (<c>LspCodeActionDriverTests</c>). The providers are
    /// stateless, so one instance of each serves every driver.
    /// </summary>
    public static ICodeActionProvider[] Providers { get; } =
    {
        new DiagnosticQuickFixProvider(),
        new OrganizeImportsProvider(),
        new ImplementInterfaceProvider(),
        new ExtractVariableProvider(),
        new ExtractMethodProvider(),
        new ConvertFormsProvider(),
        new InlineProvider(),
    };

    /// <summary>
    /// Documents currently open in the driver's workspace — zero between sessions, by construction.
    /// A sweep asserts it to show the driver retained nothing.
    /// </summary>
    public int OpenDocumentCount => _workspace.GetAllDocumentUris().Count;

    /// <summary>Opens <paramref name="text"/> under a fresh URI; dispose the session to close it.</summary>
    public Session Open(string text)
    {
        var id = Interlocked.Increment(ref s_nextDocumentId);
        var uri = $"file:///lsp-code-action-driver/doc{id}.spy";
        _workspace.OpenDocument(uri, text, 1);
        return new Session(this, uri, text);
    }

    /// <summary>One request on a document opened and closed for it: open → handle → close.</summary>
    public IReadOnlyList<CodeAction> Request(string text, LspRange range, SCG.IEnumerable<Diagnostic>? diagnostics = null)
    {
        using var session = Open(text);
        return session.Request(range, diagnostics);
    }

    /// <summary>
    /// The edits <paramref name="action"/> makes to the document at <paramref name="uri"/>. An action that
    /// edits through <c>DocumentChanges</c>, edits another document, runs a command instead of carrying an
    /// edit, or carries no edit for <paramref name="uri"/> is not something this driver can judge, and throws.
    /// </summary>
    public static IReadOnlyList<TextEdit> EditsOf(CodeAction action, DocumentUri uri)
    {
        ArgumentNullException.ThrowIfNull(action);
        var edit = action.Edit ?? throw new InvalidOperationException($"'{action.Title}' carries no edit");
        if (edit.DocumentChanges is not null)
            throw new InvalidOperationException($"'{action.Title}' edits through DocumentChanges, which this driver does not apply");
        var changes = edit.Changes ?? throw new InvalidOperationException($"'{action.Title}' carries no Changes");
        if (changes.Count != 1 || !changes.TryGetValue(uri, out var edits))
        {
            throw new InvalidOperationException(
                $"'{action.Title}' edits {{{string.Join(", ", changes.Keys)}}}, not exactly the requested document {uri}");
        }

        return edits.ToArray();
    }

    /// <summary>
    /// The text a client holds after applying <paramref name="action"/> to <paramref name="text"/>
    /// (<see cref="LspFormattingDriver.ApplyStrict"/>); throws <see cref="StrictEditApplicationException"/>
    /// when the edits are not legal for <paramref name="text"/>.
    /// </summary>
    public static string Apply(string text, CodeAction action, DocumentUri uri)
        => LspFormattingDriver.ApplyStrict(text, EditsOf(action, uri));

    public void Dispose()
    {
        _languageService.Dispose();
        _workspace.Dispose();
    }

    /// <summary>
    /// An open document: any number of code-action requests against one cached analysis. Disposing closes
    /// the document (and the debounced-analysis timer <c>OpenDocument</c> scheduled).
    /// </summary>
    internal sealed class Session : IDisposable
    {
        private readonly LspCodeActionDriver _driver;
        private SemanticResult? _analysis;
        private bool _analysed;
        private IReadOnlyList<Diagnostic>? _diagnostics;

        internal Session(LspCodeActionDriver driver, string uri, string text)
        {
            _driver = driver;
            UriString = uri;
            Uri = DocumentUri.From(uri);
            Text = text;
        }

        public string UriString { get; }

        public DocumentUri Uri { get; }

        public string Text { get; }

        /// <summary>The analysis the handler reads for this document (the workspace's cached one).</summary>
        public SemanticResult? Analysis
        {
            get
            {
                if (!_analysed)
                {
                    _analysis = Wait(_driver._languageService.GetAnalysisAsync(UriString, CancellationToken.None));
                    _analysed = true;
                }

                return _analysis;
            }
        }

        /// <summary>
        /// The diagnostics the server would publish for this document (<see cref="DiagnosticPublisher.ConvertDiagnostics"/>
        /// over <see cref="Analysis"/>) — what a client hands back as a request's <c>context.diagnostics</c>.
        /// </summary>
        public IReadOnlyList<Diagnostic> Diagnostics
        {
            get
            {
                if (_diagnostics is null)
                {
                    var analysis = Analysis;
                    _diagnostics = analysis is null
                        ? Array.Empty<Diagnostic>()
                        : DiagnosticPublisher.ConvertDiagnostics(
                            analysis.Diagnostics, _driver._languageService.GetSourceText(UriString), null, analysis.SemanticQuery, UriString);
                }

                return _diagnostics;
            }
        }

        /// <summary>
        /// One <c>textDocument/codeAction</c> request over <paramref name="range"/> with
        /// <paramref name="diagnostics"/> as its context; returns the code actions (commands, which no provider
        /// returns, are dropped).
        /// </summary>
        public IReadOnlyList<CodeAction> Request(LspRange range, SCG.IEnumerable<Diagnostic>? diagnostics = null)
        {
            var container = Wait(_driver._handler.Handle(
                new CodeActionParams
                {
                    TextDocument = new TextDocumentIdentifier(Uri),
                    Range = range,
                    Context = new CodeActionContext { Diagnostics = new Container<Diagnostic>(diagnostics ?? Array.Empty<Diagnostic>()) },
                },
                CancellationToken.None));
            return container is null
                ? Array.Empty<CodeAction>()
                : container.Where(c => c.IsCodeAction).Select(c => c.CodeAction!).ToArray();
        }

        /// <summary><see cref="LspCodeActionDriver.Apply"/> on this document's text.</summary>
        public string Apply(CodeAction action) => LspCodeActionDriver.Apply(Text, action, Uri);

        public void Dispose() => _driver._workspace.CloseDocument(UriString);

        // The handler awaits the document's analysis (run synchronously on first request, cached after); a
        // request is driven to completion before its document is closed, so blocking is the contract here.
#pragma warning disable VSTHRD002 // Synchronously waiting on tasks — see above
        private static T Wait<T>(Task<T> task) => task.GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
    }
}
