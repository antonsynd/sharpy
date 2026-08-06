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

        var newName = request.NewName;

        // Validate new name
        if (string.IsNullOrWhiteSpace(newName) || Lexer.KeywordNames.Contains(newName))
            return null;

        if (!IsValidIdentifier(newName))
            return null;

        var (line, col) = PositionConverter.ToCompiler(request.Position);
        var node = _api.FindNodeAtPosition(analysis.Ast, line, col);

        if (node == null)
            return null;

        var symbol = ResolveSymbol(node, analysis.SemanticQuery, line, col);
        if (symbol == null)
            return null;

        // Don't rename builtins
        if (symbol.DeclaringFilePath == null && symbol.DeclarationSpan == null)
            return null;

        var edits = new Dictionary<DocumentUri, System.Collections.Generic.IList<TextEdit>>();

        // Edit declaration — use the name token position, not the statement start
        if (symbol.DeclaringFilePath != null || symbol.DeclarationSpan != null)
        {
            var declLine = System.Math.Max(0, (symbol.EffectiveNameLine ?? 1) - 1);
            var declCol = System.Math.Max(0, (symbol.EffectiveNameColumn ?? 1) - 1);

            AddEdit(edits, ToDocumentUri(symbol.DeclaringFilePath, uri),
                declLine, declCol, symbol.Name.Length, newName);
        }

        // Edit all references in current file
        var references = analysis.SemanticQuery.GetReferences(symbol);
        AddReferenceEdits(edits, references, symbol.Name, uri, newName);

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
                    AddReferenceEdits(edits, crossRefs, symbol.Name, otherUri, newName);
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
        if (node is Identifier id)
            return query.GetIdentifierSymbol(id);

        // A variable declaration resolves through the node-keyed map the checker writes where it
        // binds the declaration, so a function-local nothing references still renames — the
        // name-and-position scan below cannot see one (#1232, the #1222 template).
        if (node is VariableDeclaration varDecl
            && IsOnName(line, col, varDecl.NameLineStart, varDecl.NameColumnStart, varDecl.Name.Length))
        {
            var bound = query.GetDeclarationSymbol(varDecl);
            if (bound != null)
                return bound;

            // Defensive: a declaration the checker never bound (e.g. a body whose checking bailed
            // out). Logged rather than silent, so a future gap in the map shows up in the server
            // log as a fallback instead of as "rename quietly does nothing".
            _logger.LogDebug(
                "Rename: no node-keyed symbol for variable declaration '{Name}' at {Line}:{Column}; "
                + "falling back to the declaration scan.",
                varDecl.Name, varDecl.NameLineStart, varDecl.NameColumnStart);

            return query.FindSymbolByDeclaration(varDecl.Name, varDecl.LineStart, varDecl.ColumnStart);
        }

        // A function definition, likewise: a nested def nothing calls is in no reference collection
        // and not in module scope, so only the node-keyed map can answer for it (#1232).
        if (node is FunctionDef funcDef
            && IsOnName(line, col, funcDef.NameLineStart, funcDef.NameColumnStart, funcDef.Name.Length))
        {
            var bound = query.GetFunctionDeclarationSymbol(funcDef);
            if (bound != null)
                return bound;

            _logger.LogDebug(
                "Rename: no node-keyed symbol for function definition '{Name}' at {Line}:{Column}; "
                + "falling back to the declaration scan.",
                funcDef.Name, funcDef.NameLineStart, funcDef.NameColumnStart);

            return query.FindSymbolByDeclaration(funcDef.Name, funcDef.LineStart, funcDef.ColumnStart);
        }

        // The 'as' names of try/except and with: both bind in one place in the checker, and both
        // scopes are gone by the time this runs, so both are answered node-keyed (#1232). The with
        // map predates this — rename simply never used it, which is why even a *referenced*
        // with-'as' name failed to resolve from its declaration.
        if (node is TryStatement tryStmt)
            return ResolveExceptHandlerSymbol(tryStmt, query, line, col);

        if (node is WithStatement withStmt)
            return ResolveWithItemSymbol(withStmt, query, line, col);

        // Handle declaration nodes where the name is a string property, not an Identifier child
        (string name, int nameLine, int nameCol)? decl = node switch
        {
            ClassDef c when IsOnName(line, col, c.NameLineStart, c.NameColumnStart, c.Name.Length)
                => (c.Name, c.LineStart, c.ColumnStart),
            StructDef s when IsOnName(line, col, s.NameLineStart, s.NameColumnStart, s.Name.Length)
                => (s.Name, s.LineStart, s.ColumnStart),
            InterfaceDef i when IsOnName(line, col, i.NameLineStart, i.NameColumnStart, i.Name.Length)
                => (i.Name, i.LineStart, i.ColumnStart),
            EnumDef e when IsOnName(line, col, e.NameLineStart, e.NameColumnStart, e.Name.Length)
                => (e.Name, e.LineStart, e.ColumnStart),
            _ => null
        };

        if (decl is var (name, nameLine, nameCol))
            return query.FindSymbolByDeclaration(name, nameLine, nameCol);

        return null;
    }

    private Symbol? ResolveExceptHandlerSymbol(TryStatement t, ISemanticQuery query, int line, int col)
    {
        foreach (var handler in t.Handlers)
        {
            if (handler.Name == null
                || !IsOnName(line, col, handler.NameLineStart, handler.NameColumnStart, handler.Name.Length))
            {
                continue;
            }

            var bound = query.GetExceptHandlerSymbol(handler);
            if (bound != null)
                return bound;

            _logger.LogDebug(
                "Rename: no node-keyed symbol for except-as name '{Name}' at {Line}:{Column}; "
                + "falling back to the declaration scan.",
                handler.Name, handler.NameLineStart, handler.NameColumnStart);

            return query.FindSymbolByDeclaration(handler.Name, handler.LineStart, handler.ColumnStart);
        }

        return null;
    }

    private Symbol? ResolveWithItemSymbol(WithStatement w, ISemanticQuery query, int line, int col)
    {
        foreach (var item in w.Items)
        {
            if (item.Name == null
                || !IsOnName(line, col, item.NameLineStart, item.NameColumnStart, item.Name.Length))
            {
                continue;
            }

            var bound = query.GetWithItemSymbol(item);
            if (bound != null)
                return bound;

            _logger.LogDebug(
                "Rename: no node-keyed symbol for with-as name '{Name}' at {Line}:{Column}; "
                + "falling back to the declaration scan.",
                item.Name, item.NameLineStart, item.NameColumnStart);

            return query.FindSymbolByDeclaration(item.Name, item.LineStart, item.ColumnStart);
        }

        return null;
    }

    private static bool IsOnName(int cursorLine, int cursorCol, int nameLineStart, int nameColStart, int nameLength)
    {
        return cursorLine == nameLineStart
            && cursorCol >= nameColStart
            && cursorCol < nameColStart + nameLength;
    }

    private static void AddReferenceEdits(
        Dictionary<DocumentUri, System.Collections.Generic.IList<TextEdit>> edits,
        IReadOnlyList<Compiler.Semantic.SymbolReference> references,
        string symbolName,
        string fallbackUri,
        string newName)
    {
        foreach (var refLoc in references)
        {
            var refLine = System.Math.Max(0, refLoc.Line - 1);
            var refCol = System.Math.Max(0, refLoc.Column - 1);

            AddEdit(edits, ToDocumentUri(refLoc.FilePath, fallbackUri),
                refLine, refCol, symbolName.Length, newName);
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
        DocumentUri uri,
        int line,
        int col,
        int oldNameLength,
        string newName)
    {
        if (!edits.TryGetValue(uri, out var fileEdits))
        {
            fileEdits = new System.Collections.Generic.List<TextEdit>();
            edits[uri] = fileEdits;
        }

        fileEdits.Add(new TextEdit
        {
            Range = new LspRange(
                new Position(line, col),
                new Position(line, col + oldNameLength)),
            NewText = newName
        });
    }

    private static string? UriToFilePath(string uri)
    {
        if (Uri.TryCreate(uri, UriKind.Absolute, out var parsed) && parsed.IsFile)
            return parsed.LocalPath;

        return System.IO.Path.IsPathRooted(uri) ? uri : null;
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
