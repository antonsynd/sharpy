using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// Handles textDocument/semanticTokens requests.
/// Walks the AST and produces semantic tokens for syntax highlighting.
/// </summary>
internal sealed class SharpySemanticTokensHandler : SemanticTokensHandlerBase
{
    private readonly LanguageService _languageService;

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

    // Token type indices — must match order of TokenTypes array above.
    internal const int TFunction = 0;
    internal const int TClass = 1;
    internal const int TStruct = 2;
    internal const int TInterface = 3;
    internal const int TEnum = 4;
    internal const int TEnumMember = 5;
    internal const int TParameter = 6;
    internal const int TVariable = 7;
    internal const int TDecorator = 8;
    internal const int TType = 9;
    internal const int TProperty = 10;
    internal const int TMethod = 11;
    internal const int TKeyword = 12;

    internal const int ModDeclaration = 1 << 0;
    internal const int ModDefinition = 1 << 1;
    internal const int ModStatic = 1 << 2;
    internal const int ModAsync = 1 << 3;
    internal const int ModReadonly = 1 << 4;

    public SharpySemanticTokensHandler(LanguageService languageService)
    {
        _languageService = languageService;
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
        var parseResult = await _languageService.GetParseResultAsync(uri, ct).ConfigureAwait(false);

        if (parseResult?.Ast == null)
            return;

        var tokens = new System.Collections.Generic.List<RawToken>();
        CollectTokens(parseResult.Ast.Body, tokens);

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

    internal static void CollectTokens(
        IEnumerable<Statement> statements,
        System.Collections.Generic.List<RawToken> tokens)
    {
        foreach (var stmt in statements)
        {
            CollectStatementTokens(stmt, tokens);
        }
    }

    private static void CollectStatementTokens(
        Statement stmt,
        System.Collections.Generic.List<RawToken> tokens)
    {
        switch (stmt)
        {
            case FunctionDef f:
                CollectFunctionTokens(f, tokens);
                break;

            case ClassDef c:
                PushNameToken(tokens, c.LineStart, c.ColumnStart, c.Name.Length, TClass, ModDeclaration | ModDefinition);
                CollectDecorators(c.Decorators, tokens);
                CollectTokens(c.Body, tokens);
                break;

            case StructDef s:
                PushNameToken(tokens, s.LineStart, s.ColumnStart, s.Name.Length, TStruct, ModDeclaration | ModDefinition);
                CollectDecorators(s.Decorators, tokens);
                CollectTokens(s.Body, tokens);
                break;

            case InterfaceDef i:
                PushNameToken(tokens, i.LineStart, i.ColumnStart, i.Name.Length, TInterface, ModDeclaration | ModDefinition);
                CollectDecorators(i.Decorators, tokens);
                CollectTokens(i.Body, tokens);
                break;

            case EnumDef e:
                PushNameToken(tokens, e.LineStart, e.ColumnStart, e.Name.Length, TEnum, ModDeclaration | ModDefinition);
                foreach (var member in e.Members)
                {
                    PushNameToken(tokens, member.LineStart, member.ColumnStart, member.Name.Length, TEnumMember, ModDeclaration);
                }
                break;

            case VariableDeclaration v:
                var varMods = ModDeclaration;
                if (v.IsConst)
                    varMods |= ModReadonly;
                PushNameToken(tokens, v.LineStart, v.ColumnStart, v.Name.Length, TVariable, varMods);
                CollectDecorators(v.Decorators, tokens);
                break;

            case PropertyDef p:
                PushNameToken(tokens, p.LineStart, p.ColumnStart, p.Name.Length, TProperty, ModDeclaration);
                CollectDecorators(p.Decorators, tokens);
                CollectTokens(p.Body, tokens);
                break;

            case IfStatement ifStmt:
                CollectTokens(ifStmt.ThenBody, tokens);
                foreach (var elif in ifStmt.ElifClauses)
                    CollectTokens(elif.Body, tokens);
                CollectTokens(ifStmt.ElseBody, tokens);
                break;

            case ForStatement forStmt:
                CollectTokens(forStmt.Body, tokens);
                CollectTokens(forStmt.ElseBody, tokens);
                break;

            case WhileStatement whileStmt:
                CollectTokens(whileStmt.Body, tokens);
                CollectTokens(whileStmt.ElseBody, tokens);
                break;

            case TryStatement tryStmt:
                CollectTokens(tryStmt.Body, tokens);
                foreach (var handler in tryStmt.Handlers)
                    CollectTokens(handler.Body, tokens);
                CollectTokens(tryStmt.ElseBody, tokens);
                CollectTokens(tryStmt.FinallyBody, tokens);
                break;

            case WithStatement withStmt:
                CollectTokens(withStmt.Body, tokens);
                break;

            case MatchStatement matchStmt:
                foreach (var matchCase in matchStmt.Cases)
                    CollectTokens(matchCase.Body, tokens);
                break;
        }
    }

    private static void CollectFunctionTokens(
        FunctionDef f,
        System.Collections.Generic.List<RawToken> tokens)
    {
        var mods = ModDeclaration | ModDefinition;
        if (f.IsAsync)
            mods |= ModAsync;
        if (HasDecorator(f.Decorators, "static"))
            mods |= ModStatic;

        // Function name — use Method when inside a class, Function at top level
        PushNameToken(tokens, f.LineStart, f.ColumnStart, f.Name.Length, TFunction, mods);

        CollectDecorators(f.Decorators, tokens);

        // Parameters
        foreach (var param in f.Parameters)
        {
            if (param.Name == "self" || param.Name == "cls")
                continue;
            PushNameToken(tokens, param.LineStart, param.ColumnStart, param.Name.Length, TParameter, ModDeclaration);
        }

        CollectTokens(f.Body, tokens);
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
                PushNameToken(tokens, dec.LineStart, dec.ColumnStart, name.Length + 1, TDecorator, 0); // +1 for @
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

    /// <summary>
    /// A collected token before delta-encoding. Stored with 0-based line/col.
    /// </summary>
    internal readonly record struct RawToken(int Line, int Col, int Length, int TokenType, int Modifiers);
}
