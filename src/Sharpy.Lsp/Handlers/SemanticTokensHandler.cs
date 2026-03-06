using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// Handles textDocument/semanticTokens requests.
/// Walks the AST and produces semantic tokens for syntax highlighting.
/// </summary>
internal sealed class SharplySemanticTokensHandler : SemanticTokensHandlerBase
{
    private readonly SharplyWorkspace _workspace;

    // Token types registered in the legend — order must match indices used in Push calls.
    private static readonly string[] TokenTypes =
    [
        SemanticTokenType.Function,   // 0
        SemanticTokenType.Class,      // 1
        SemanticTokenType.Struct,     // 2
        SemanticTokenType.Interface,  // 3
        SemanticTokenType.Enum,       // 4
        SemanticTokenType.EnumMember, // 5
        SemanticTokenType.Parameter,  // 6
        SemanticTokenType.Variable,   // 7
        SemanticTokenType.Decorator,  // 8
        SemanticTokenType.Type,       // 9
        SemanticTokenType.Property,   // 10
        SemanticTokenType.Method,     // 11
        SemanticTokenType.Keyword,    // 12
    ];

    // Token modifiers — order must match bit positions.
    private static readonly string[] TokenModifiers =
    [
        SemanticTokenModifier.Declaration,  // bit 0
        SemanticTokenModifier.Definition,   // bit 1
        SemanticTokenModifier.Static,       // bit 2
        SemanticTokenModifier.Async,        // bit 3
        SemanticTokenModifier.Readonly,     // bit 4
    ];

    private const int ModDeclaration = 1 << 0;
    private const int ModDefinition = 1 << 1;
    private const int ModStatic = 1 << 2;
    private const int ModAsync = 1 << 3;
    private const int ModReadonly = 1 << 4;

    public SharplySemanticTokensHandler(SharplyWorkspace workspace)
    {
        _workspace = workspace;
    }

    protected override SemanticTokensRegistrationOptions CreateRegistrationOptions(
        SemanticTokensCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new SemanticTokensRegistrationOptions
        {
            DocumentSelector = TextDocumentSelector.ForPattern("**/*.spy"),
            Full = new SemanticTokensCapabilityRequestFull { Delta = false },
            Legend = new SemanticTokensLegend
            {
                TokenTypes = new Container<SemanticTokenType>(
                    TokenTypes.Select(t => new SemanticTokenType(t))),
                TokenModifiers = new Container<SemanticTokenModifier>(
                    TokenModifiers.Select(m => new SemanticTokenModifier(m)))
            }
        };
    }

    protected override async Task Tokenize(
        SemanticTokensBuilder builder,
        ITextDocumentIdentifierParams identifier,
        CancellationToken ct)
    {
        var uri = identifier.TextDocument.Uri.ToString();
        var analysis = await _workspace.GetAnalysisAsync(uri, ct).ConfigureAwait(false);

        if (analysis?.Ast == null)
            return;

        var tokens = new System.Collections.Generic.List<RawToken>();
        CollectTokens(analysis.Ast.Body, analysis, tokens);

        // Sort by position (line, then column)
        tokens.Sort(static (a, b) =>
        {
            var lineCmp = a.Line.CompareTo(b.Line);
            return lineCmp != 0 ? lineCmp : a.Col.CompareTo(b.Col);
        });

        foreach (var token in tokens)
        {
            // builder.Push uses 0-based line/col
            builder.Push(token.Line, token.Col, token.Length, token.TokenType, token.Modifiers);
        }
    }

    protected override Task<SemanticTokensDocument> GetSemanticTokensDocument(
        ITextDocumentIdentifierParams @params,
        CancellationToken ct)
    {
        return Task.FromResult(new SemanticTokensDocument(RegistrationOptions.Legend));
    }

    private static void CollectTokens(
        IEnumerable<Statement> statements,
        SemanticResult analysis,
        System.Collections.Generic.List<RawToken> tokens)
    {
        foreach (var stmt in statements)
        {
            CollectStatementTokens(stmt, analysis, tokens);
        }
    }

    private static void CollectStatementTokens(
        Statement stmt,
        SemanticResult analysis,
        System.Collections.Generic.List<RawToken> tokens)
    {
        switch (stmt)
        {
            case FunctionDef f:
                CollectFunctionTokens(f, analysis, tokens);
                break;

            case ClassDef c:
                PushNameToken(tokens, c.LineStart, c.ColumnStart, c.Name.Length, 1, ModDeclaration | ModDefinition);
                CollectDecorators(c.Decorators, tokens);
                CollectTokens(c.Body, analysis, tokens);
                break;

            case StructDef s:
                PushNameToken(tokens, s.LineStart, s.ColumnStart, s.Name.Length, 2, ModDeclaration | ModDefinition);
                CollectDecorators(s.Decorators, tokens);
                CollectTokens(s.Body, analysis, tokens);
                break;

            case InterfaceDef i:
                PushNameToken(tokens, i.LineStart, i.ColumnStart, i.Name.Length, 3, ModDeclaration | ModDefinition);
                CollectDecorators(i.Decorators, tokens);
                CollectTokens(i.Body, analysis, tokens);
                break;

            case EnumDef e:
                PushNameToken(tokens, e.LineStart, e.ColumnStart, e.Name.Length, 4, ModDeclaration | ModDefinition);
                foreach (var member in e.Members)
                {
                    PushNameToken(tokens, member.LineStart, member.ColumnStart, member.Name.Length, 5, ModDeclaration);
                }
                break;

            case VariableDeclaration v:
                var varMods = ModDeclaration;
                if (v.IsConst)
                    varMods |= ModReadonly;
                PushNameToken(tokens, v.LineStart, v.ColumnStart, v.Name.Length, 7, varMods);
                CollectDecorators(v.Decorators, tokens);
                break;

            case PropertyDef p:
                PushNameToken(tokens, p.LineStart, p.ColumnStart, p.Name.Length, 10, ModDeclaration);
                CollectDecorators(p.Decorators, tokens);
                CollectTokens(p.Body, analysis, tokens);
                break;

            case IfStatement ifStmt:
                CollectTokens(ifStmt.ThenBody, analysis, tokens);
                foreach (var elif in ifStmt.ElifClauses)
                    CollectTokens(elif.Body, analysis, tokens);
                CollectTokens(ifStmt.ElseBody, analysis, tokens);
                break;

            case ForStatement forStmt:
                CollectTokens(forStmt.Body, analysis, tokens);
                CollectTokens(forStmt.ElseBody, analysis, tokens);
                break;

            case WhileStatement whileStmt:
                CollectTokens(whileStmt.Body, analysis, tokens);
                CollectTokens(whileStmt.ElseBody, analysis, tokens);
                break;

            case TryStatement tryStmt:
                CollectTokens(tryStmt.Body, analysis, tokens);
                foreach (var handler in tryStmt.Handlers)
                    CollectTokens(handler.Body, analysis, tokens);
                CollectTokens(tryStmt.ElseBody, analysis, tokens);
                CollectTokens(tryStmt.FinallyBody, analysis, tokens);
                break;

            case WithStatement withStmt:
                CollectTokens(withStmt.Body, analysis, tokens);
                break;

            case MatchStatement matchStmt:
                foreach (var matchCase in matchStmt.Cases)
                    CollectTokens(matchCase.Body, analysis, tokens);
                break;
        }
    }

    private static void CollectFunctionTokens(
        FunctionDef f,
        SemanticResult analysis,
        System.Collections.Generic.List<RawToken> tokens)
    {
        var mods = ModDeclaration | ModDefinition;
        if (f.IsAsync)
            mods |= ModAsync;
        if (HasDecorator(f.Decorators, "static"))
            mods |= ModStatic;

        // Function name — use Method (11) when inside a class, Function (0) at top level
        PushNameToken(tokens, f.LineStart, f.ColumnStart, f.Name.Length, 0, mods);

        CollectDecorators(f.Decorators, tokens);

        // Parameters
        foreach (var param in f.Parameters)
        {
            if (param.Name == "self" || param.Name == "cls")
                continue;
            PushNameToken(tokens, param.LineStart, param.ColumnStart, param.Name.Length, 6, ModDeclaration);
        }

        CollectTokens(f.Body, analysis, tokens);
    }

    private static void CollectDecorators(
        IEnumerable<Decorator> decorators,
        System.Collections.Generic.List<RawToken> tokens)
    {
        foreach (var dec in decorators)
        {
            // The decorator name starts at the decorator position (after @)
            var name = dec.Name;
            if (name.Length > 0)
            {
                PushNameToken(tokens, dec.LineStart, dec.ColumnStart, name.Length + 1, 8, 0); // +1 for @
            }
        }
    }

    private static bool HasDecorator(IEnumerable<Decorator> decorators, string name)
    {
        foreach (var d in decorators)
        {
            if (d.Name == name)
                return true;
        }
        return false;
    }

    private static void PushNameToken(
        System.Collections.Generic.List<RawToken> tokens,
        int compilerLine,
        int compilerCol,
        int length,
        int tokenType,
        int modifiers)
    {
        if (length <= 0)
            return;

        // Convert from 1-based compiler to 0-based LSP
        tokens.Add(new RawToken(
            compilerLine - 1,
            compilerCol - 1,
            length,
            tokenType,
            modifiers));
    }

    private static int CompareTokensByPosition(RawToken a, RawToken b)
    {
        var lineCmp = a.Line.CompareTo(b.Line);
        return lineCmp != 0 ? lineCmp : a.Col.CompareTo(b.Col);
    }

    /// <summary>
    /// A collected token before delta-encoding. Stored with 0-based line/col.
    /// </summary>
    private readonly record struct RawToken(int Line, int Col, int Length, int TokenType, int Modifiers);
}
