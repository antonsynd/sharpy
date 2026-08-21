using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Services;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// Handles textDocument/rename and textDocument/prepareRename requests.
/// </summary>
internal sealed class SharpyRenameHandler : RenameHandlerBase
{
    private readonly SharpyWorkspace _workspace;
    private readonly LanguageService _languageService;
    private readonly CompilerApi _api;
    private readonly ILogger<SharpyRenameHandler> _logger;

    public SharpyRenameHandler(
        SharpyWorkspace workspace,
        LanguageService languageService,
        CompilerApi api,
        ILogger<SharpyRenameHandler> logger)
    {
        _workspace = workspace;
        _languageService = languageService;
        _api = api;
        _logger = logger;
    }

    public override async Task<WorkspaceEdit?> Handle(RenameParams request, CancellationToken ct)
    {
        var uri = request.TextDocument.Uri.ToString();
        var analysis = await _languageService.GetAnalysisAsync(uri, ct).ConfigureAwait(false);

        if (analysis?.Ast == null || analysis.SemanticQuery == null)
            return null;

        var newText = ResolveNewNameText(request.NewName);
        if (newText == null)
            return null;

        var (line, col) = PositionConverter.ToCompiler(request.Position);
        var node = _api.FindNodeAtPosition(analysis.Ast, line, col);

        if (node == null)
            return null;

        var symbol = ResolveSymbol(node, analysis.SemanticQuery, line, col);
        if (symbol == null)
            return null;

        // A builtin is not the user's to rename. Identity, not spelling: a user symbol that
        // shadows `len` is a different symbol wearing the same name, and it renames normally.
        if (analysis.SymbolTable?.BuiltinRegistry.IsBuiltinSymbol(symbol) == true)
            return null;

        // Anything else with no source location — a member discovered from a .NET assembly —
        // has no declaration any edit could reach either.
        if (symbol.DeclaringFilePath == null && symbol.DeclarationSpan == null)
            return null;

        var edits = new Dictionary<DocumentUri, System.Collections.Generic.IList<TextEdit>>();
        // The declaration is usually recorded as a reference too, so without this the same range
        // is emitted twice — two overlapping edits an editor may apply twice (#1263).
        var seen = new RangeDedupe();

        // A plainly-reassigned spelling is bound more than once, and each binding's references stop
        // at the next rebinding. Renaming one binding's occurrences would edit a FRAGMENT of the
        // variable and leave source that still compiles while meaning something else (#1359), so
        // every binding in the chain is edited as one unit. A spelling bound once is a chain of one.
        var chain = analysis.SemanticQuery.GetBindingChain(symbol);
        var bindings = chain.Count > 0
            ? chain.Cast<Symbol>().ToList()
            : new System.Collections.Generic.List<Symbol> { symbol };

        foreach (var binding in bindings)
        {
            // Edit declaration — use the name token position, not the statement start
            if (binding.DeclaringFilePath != null || binding.DeclarationSpan != null)
            {
                var declLine = System.Math.Max(0, (binding.EffectiveNameLine ?? 1) - 1);
                var declCol = System.Math.Max(0, (binding.EffectiveNameColumn ?? 1) - 1);

                AddEdit(edits, seen, ToDocumentUri(binding.DeclaringFilePath, uri),
                    declLine, declCol, NameExtentLength(binding), newText);
            }

            // Edit all references in current file
            AddReferenceEdits(
                edits, seen, analysis.SemanticQuery.GetReferences(binding), binding.Name, uri, newText);
        }

        // Edit references in other workspace files
        var allUris = _workspace.GetAllDocumentUris();
        var otherUris = allUris.Where(u => !string.Equals(u, uri, StringComparison.Ordinal)).ToList();

        if (otherUris.Count > 0)
        {
            var reporter = _languageService.ProgressReporter;
            using var progress = reporter != null
                ? await reporter.BeginAsync("Renaming across files", ct).ConfigureAwait(false)
                : ProgressScope.NoOp;

            for (var i = 0; i < otherUris.Count; i++)
            {
                var otherUri = otherUris[i];
                progress.Report(
                    $"Renaming in {System.IO.Path.GetFileName(UriToFilePath(otherUri) ?? otherUri)}",
                    (i + 1) * 100 / otherUris.Count);

                try
                {
                    var otherAnalysis = await _languageService.GetAnalysisAsync(otherUri, ct).ConfigureAwait(false);
                    if (otherAnalysis?.SemanticQuery == null)
                        continue;

                    var crossRefs = otherAnalysis.SemanticQuery.FindReferencesBySymbolIdentity(
                        symbol.Name, symbol.DeclaringFilePath);
                    AddReferenceEdits(edits, seen, crossRefs, symbol.Name, otherUri, newText);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    // Skip files that fail to analyze
                }
            }
        }

        return new WorkspaceEdit
        {
            Changes = edits.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value as IEnumerable<TextEdit>)
        };
    }

    private Symbol? ResolveSymbol(Node node, ISemanticQuery query, int line, int col)
    {
        return DeclarationCursorResolver.Resolve(node, query, line, col, _logger);
    }

    /// <summary>
    /// The two backticks an escaped spelling adds to the name's source extent.
    /// </summary>
    /// <remarks>
    /// This constant no longer CONSTRUCTS any extent — since #1454 every extent this handler uses is
    /// one the parser recorded from the name token. Its single remaining use is
    /// <see cref="ReferenceExtentLength"/>, which RECOGNIZES an already-recorded span as the escaped
    /// spelling (plan-80eee2 Design Decision 7). Recognition and reconstruction are different jobs:
    /// the first asks what a measured number means, the second invents one.
    /// </remarks>
    private const int BacktickPairLength = 2;

    /// <summary>
    /// How many characters this symbol's name occupies at its declaration, from the extent the
    /// parser recorded (#1454). Symbols with no parsed node — CLR imports — answer through
    /// <see cref="Symbol.EffectiveNameColumnEnd"/>'s fallback, which is where the old
    /// <c>Name.Length</c> + backtick-pair derivation now lives, once, on the symbol itself.
    /// </summary>
    /// <remarks>
    /// An edit sized to <c>Name.Length</c> against an escaped declaration replaces all but the last
    /// two characters and leaves backtick debris in the renamed source (#1281) — that is the defect
    /// the recorded extent removes the possibility of, rather than compensating for.
    /// </remarks>
    private static int NameExtentLength(Symbol symbol) =>
        (symbol.EffectiveNameColumnEnd - symbol.EffectiveNameColumn) ?? symbol.Name.Length;

    /// <summary>
    /// How many characters one reference occupies. The recorded span is the identifier token's,
    /// which since #1281 covers both backticks of an escaped spelling — so each occurrence is
    /// replaced as it is written, whether or not the declaration was escaped.
    /// </summary>
    /// <remarks>
    /// A span matching neither spelling of the name is not this reference's extent: the root of a
    /// dotted escape (<c>`System.IO.Path`</c>, #713) carries the whole token's span on a segment
    /// symbol. Editing to the bare name there is the conservative choice — it under-reaches
    /// rather than eating the following segments.
    /// </remarks>
    private static int ReferenceExtentLength(Compiler.Semantic.SymbolReference reference, string symbolName)
    {
        var spanLength = reference.Span.Length;

        return spanLength == symbolName.Length || spanLength == symbolName.Length + BacktickPairLength
            ? spanLength
            : symbolName.Length;
    }

    private static void AddReferenceEdits(
        Dictionary<DocumentUri, System.Collections.Generic.IList<TextEdit>> edits,
        RangeDedupe seen,
        IReadOnlyList<Compiler.Semantic.SymbolReference> references,
        string symbolName,
        string fallbackUri,
        string newText)
    {
        foreach (var refLoc in references)
        {
            var refLine = System.Math.Max(0, refLoc.Line - 1);
            var refCol = System.Math.Max(0, refLoc.Column - 1);

            AddEdit(edits, seen, ToDocumentUri(refLoc.FilePath, fallbackUri),
                refLine, refCol, ReferenceExtentLength(refLoc, symbolName), newText);
        }
    }

    /// <summary>
    /// The path single-file analysis gives the buffer it was handed (<c>CompilerApi</c>'s synthetic
    /// project-of-one-file). It names no document a client can open — it means "the source under
    /// analysis", which is the request URI.
    /// </summary>
    /// <remarks>
    /// Single-file analyze already strips the entry file's path identity for exactly this reason
    /// (#1087, <c>ProjectConfig.NullifyEntryFilePath</c>), but the stripping reaches the name
    /// resolver and the per-file <c>SemanticInfo</c> and not the type checker — so symbols the
    /// checker binds (every function-local) still carry this placeholder (#1262). Mapping it here
    /// keeps the edit on the document the user is editing; when #1262 lands the mapping becomes a
    /// no-op, not a conflict.
    /// </remarks>
    private const string InMemorySourcePath = "<source>";

    /// <summary>
    /// The document an edit belongs in: <paramref name="filePath"/> when it names a real file,
    /// and the request URI when analysis had no file to name (null, or the in-memory placeholder).
    /// </summary>
    private static DocumentUri ToDocumentUri(string? filePath, string requestUri)
    {
        var path = filePath is null or InMemorySourcePath ? requestUri : filePath;

        return path.StartsWith("file://", StringComparison.Ordinal)
            ? DocumentUri.From(path)
            : DocumentUri.FromFileSystemPath(path);
    }

    private static void AddEdit(
        Dictionary<DocumentUri, System.Collections.Generic.IList<TextEdit>> edits,
        RangeDedupe seen,
        DocumentUri uri,
        int line,
        int col,
        int oldNameLength,
        string newName)
    {
        var range = new LspRange(
            new Position(line, col),
            new Position(line, col + oldNameLength));

        if (!seen.IsFirst(uri, range))
            return;

        if (!edits.TryGetValue(uri, out var fileEdits))
        {
            fileEdits = new System.Collections.Generic.List<TextEdit>();
            edits[uri] = fileEdits;
        }

        fileEdits.Add(new TextEdit
        {
            Range = range,
            NewText = newName
        });
    }

    private static string? UriToFilePath(string uri)
    {
        if (Uri.TryCreate(uri, UriKind.Absolute, out var parsed) && parsed.IsFile)
            return parsed.LocalPath;

        return System.IO.Path.IsPathRooted(uri) ? uri : null;
    }

    /// <summary>
    /// The text a rename writes for the requested name, or null when the request names nothing
    /// Sharpy can spell.
    /// </summary>
    /// <remarks>
    /// The backticks are not part of a name — they are how a spelling the lexer would otherwise
    /// claim reaches the identifier namespace — so the request's two halves are judged separately.
    /// The core must be a legal identifier. The written spelling is escaped when the core is a
    /// keyword (the only way to write it at all) or when the request escaped it explicitly, which
    /// is how a user asks to shadow a builtin deliberately. Everything else is written bare, so
    /// renaming <c>`event`</c> to an ordinary name drops the backticks instead of carrying them
    /// along (#1281).
    /// </remarks>
    private static string? ResolveNewNameText(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            return null;

        var explicitlyEscaped = newName.Length >= 3 && newName[0] == '`' && newName[^1] == '`';
        var core = explicitlyEscaped ? newName[1..^1] : newName;

        // Rejects stray or unbalanced backticks along with every other non-identifier character.
        if (!IsValidIdentifier(core))
            return null;

        return explicitlyEscaped || Lexer.KeywordNames.Contains(core)
            ? $"`{core}`"
            : core;
    }

    private static bool IsValidIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        if (!char.IsLetter(name[0]) && name[0] != '_')
            return false;

        for (var i = 1; i < name.Length; i++)
        {
            if (!char.IsLetterOrDigit(name[i]) && name[i] != '_')
                return false;
        }

        return true;
    }

    protected override RenameRegistrationOptions CreateRegistrationOptions(
        RenameCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new RenameRegistrationOptions
        {
            DocumentSelector = TextDocumentSelector.ForPattern("**/*.spy"),
            PrepareProvider = false
        };
    }
}
