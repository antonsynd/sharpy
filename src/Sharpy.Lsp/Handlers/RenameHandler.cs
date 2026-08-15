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
        if (node is Identifier id)
            return query.GetIdentifierSymbol(id);

        // A variable declaration resolves through the node-keyed map the checker writes where it
        // binds the declaration, so a function-local nothing references still renames — the
        // name-and-position scan below cannot see one (#1232, the #1222 template).
        if (node is VariableDeclaration varDecl
            && IsOnNameExtent(line, col, varDecl.NameLineStart, varDecl.NameColumnStart,
                varDecl.NameColumnEnd))
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
        if (node is FunctionDef funcDef)
        {
            if (IsOnNameExtent(line, col, funcDef.NameLineStart, funcDef.NameColumnStart,
                    funcDef.NameColumnEnd))
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

            // A cursor on a PARAMETER name lands here, not on the parameter: `Parameter` is a
            // standalone record rather than a Node, so FindNodeAtPosition can only ever return the
            // enclosing definition (#1359). The extent is the one the parser recorded, so `*args`
            // hit-tests on `args` and an escaped name includes its backticks.
            var parameterSymbol = ResolveParameterSymbol(funcDef.Parameters, query, line, col);
            if (parameterSymbol != null)
                return parameterSymbol;
        }

        // Lambda parameters bind through the same node-keyed map, and a cursor on one resolves to
        // the lambda for the same reason.
        if (node is LambdaExpression lambda)
        {
            var lambdaParameterSymbol = ResolveParameterSymbol(lambda.Parameters, query, line, col);
            if (lambdaParameterSymbol != null)
                return lambdaParameterSymbol;
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
            ClassDef c when IsOnNameExtent(line, col, c.NameLineStart, c.NameColumnStart, c.NameColumnEnd)
                => (c.Name, c.LineStart, c.ColumnStart),
            StructDef s when IsOnNameExtent(line, col, s.NameLineStart, s.NameColumnStart, s.NameColumnEnd)
                => (s.Name, s.LineStart, s.ColumnStart),
            InterfaceDef i when IsOnNameExtent(line, col, i.NameLineStart, i.NameColumnStart, i.NameColumnEnd)
                => (i.Name, i.LineStart, i.ColumnStart),
            EnumDef e when IsOnNameExtent(line, col, e.NameLineStart, e.NameColumnStart, e.NameColumnEnd)
                => (e.Name, e.LineStart, e.ColumnStart),
            _ => null
        };

        if (decl is var (name, nameLine, nameCol))
            return query.FindSymbolByDeclaration(name, nameLine, nameCol);

        return null;
    }

    /// <summary>
    /// The symbol for whichever parameter's name extent the cursor sits in, or null when it sits on
    /// none of them. There is no declaration-scan fallback here: a parameter has no module-scope
    /// entry and an unreferenced one is in no reference collection either, so the node-keyed map is
    /// the only answer that exists (#1359).
    /// </summary>
    /// <remarks>
    /// Rename is the only handler with declaration-cursor arms at all. <c>ReferencesHandler</c> and
    /// <c>DocumentHighlightHandler</c> resolve <c>Identifier</c>/<c>FunctionCall</c> and nothing
    /// else, so a cursor on ANY declaration — parameter, def, class, <c>as</c> name — answers
    /// nothing there. That gap is general rather than parameter-specific and is tracked separately
    /// (#1539), including whether this resolution belongs in one shared place.
    /// </remarks>
    private Symbol? ResolveParameterSymbol(
        System.Collections.Immutable.ImmutableArray<Parameter> parameters,
        ISemanticQuery query,
        int line,
        int col)
    {
        foreach (var param in parameters)
        {
            if (!IsOnNameExtent(line, col, param.NameLineStart, param.NameColumnStart, param.NameColumnEnd))
                continue;

            var bound = query.GetParameterSymbol(param);
            if (bound != null)
                return bound;

            _logger.LogDebug(
                "Rename: no node-keyed symbol for parameter '{Name}' at {Line}:{Column}.",
                param.Name, param.NameLineStart, param.NameColumnStart);

            return null;
        }

        return null;
    }

    private Symbol? ResolveExceptHandlerSymbol(TryStatement t, ISemanticQuery query, int line, int col)
    {
        foreach (var handler in t.Handlers)
        {
            if (handler.Name == null
                || !IsOnNameExtent(line, col, handler.NameLineStart, handler.NameColumnStart,
                    handler.NameColumnEnd))
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
                || !IsOnNameExtent(line, col, item.NameLineStart, item.NameColumnStart,
                    item.NameColumnEnd))
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

    /// <summary>
    /// Whether the cursor sits inside a RECORDED name extent — <paramref name="nameColEnd"/> is the
    /// parser's exclusive end, taken from the name token, so escaped spellings are already covered
    /// and no backtick compensation applies here (#1454). A node with no recorded extent reports
    /// 0/0, which no 1-based cursor can be inside.
    /// </summary>
    private static bool IsOnNameExtent(
        int cursorLine, int cursorCol, int nameLineStart, int nameColStart, int nameColEnd)
    {
        return cursorLine == nameLineStart
            && cursorCol >= nameColStart
            && cursorCol < nameColEnd;
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
