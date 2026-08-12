using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Services;

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
        SemanticTokenType.String,     // 13
        SemanticTokenType.Number,     // 14
    ];

    // Token modifiers — order must match bit positions.
    // "generated" is a custom modifier (the LSP spec allows servers to define them).
    // Clients can style this in VS Code via editor.semanticTokenColorCustomizations.
    private static readonly string[] TokenModifiers =
    [
        SemanticTokenModifier.Declaration,  // bit 0
        SemanticTokenModifier.Definition,   // bit 1
        SemanticTokenModifier.Static,       // bit 2
        SemanticTokenModifier.Async,        // bit 3
        SemanticTokenModifier.Readonly,     // bit 4
        "generated",                        // bit 5 (custom — for source-generator output)
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
    internal const int TString = 13;
    internal const int TNumber = 14;

    internal const int ModDeclaration = 1 << 0;
    internal const int ModDefinition = 1 << 1;
    internal const int ModStatic = 1 << 2;
    internal const int ModAsync = 1 << 3;
    internal const int ModReadonly = 1 << 4;
    internal const int ModGenerated = 1 << 5;

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

        // Prefer the full semantic result so we can tag generated declarations with the
        // custom "generated" modifier. Fall back to a parse-only result when semantic
        // analysis hasn't completed.
        ISemanticQuery? semanticQuery = null;
        Module? ast = null;

        var analysis = await _languageService.GetAnalysisAsync(uri, ct).ConfigureAwait(false);
        if (analysis?.Ast != null)
        {
            ast = analysis.Ast;
            semanticQuery = analysis.SemanticQuery;
        }
        else
        {
            var parseResult = await _languageService.GetParseResultAsync(uri, ct).ConfigureAwait(false);
            if (parseResult?.Ast == null)
                return;
            ast = parseResult.Ast;
        }

        var tokens = new System.Collections.Generic.List<RawToken>();
        CollectTokens(ast.Body, tokens, semanticQuery);

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
        CollectTokens(statements, tokens, semanticQuery: null);
    }

    internal static void CollectTokens(
        IEnumerable<Statement> statements,
        System.Collections.Generic.List<RawToken> tokens,
        ISemanticQuery? semanticQuery)
    {
        foreach (var stmt in statements)
        {
            CollectStatementTokens(stmt, tokens, parameterNames: null, semanticQuery);
        }
    }

    private static void CollectStatementTokens(
        Statement stmt,
        System.Collections.Generic.List<RawToken> tokens)
    {
        CollectStatementTokens(stmt, tokens, parameterNames: null, semanticQuery: null);
    }

    private static void CollectStatementTokens(
        Statement stmt,
        System.Collections.Generic.List<RawToken> tokens,
        HashSet<string>? parameterNames)
    {
        CollectStatementTokens(stmt, tokens, parameterNames, semanticQuery: null);
    }

    private static void CollectStatementTokens(
        Statement stmt,
        System.Collections.Generic.List<RawToken> tokens,
        HashSet<string>? parameterNames,
        ISemanticQuery? semanticQuery)
    {
        // Apply the "generated" modifier to declarations that were produced by a source
        // generator. The modifier propagates to nested members because generators emit
        // whole statements (a method, a property, a field) — we don't descend into
        // generated subtrees from non-generated ones, so this check at the statement
        // level is sufficient.
        var genMod = (semanticQuery != null && semanticQuery.IsGenerated(stmt)) ? ModGenerated : 0;

        switch (stmt)
        {
            case FunctionDef f:
                CollectFunctionTokens(f, tokens, genMod, semanticQuery);
                break;

            case ClassDef c:
                PushNameToken(tokens, c.NameLineStart, c.NameColumnStart, c.Name.Length, TClass, ModDeclaration | ModDefinition | genMod);
                CollectDecorators(c.Decorators, tokens);
                CollectTokens(c.Body, tokens, semanticQuery);
                break;

            case StructDef s:
                PushNameToken(tokens, s.NameLineStart, s.NameColumnStart, s.Name.Length, TStruct, ModDeclaration | ModDefinition | genMod);
                CollectDecorators(s.Decorators, tokens);
                CollectTokens(s.Body, tokens, semanticQuery);
                break;

            case InterfaceDef i:
                PushNameToken(tokens, i.NameLineStart, i.NameColumnStart, i.Name.Length, TInterface, ModDeclaration | ModDefinition | genMod);
                CollectDecorators(i.Decorators, tokens);
                CollectTokens(i.Body, tokens, semanticQuery);
                break;

            case EnumDef e:
                PushNameToken(tokens, e.NameLineStart, e.NameColumnStart, e.Name.Length, TEnum, ModDeclaration | ModDefinition | genMod);
                foreach (var member in e.Members)
                {
                    PushNameToken(tokens, member.LineStart, member.ColumnStart, member.Name.Length, TEnumMember, ModDeclaration | genMod);
                }
                break;

            case VariableDeclaration v:
                var varMods = ModDeclaration | genMod;
                if (v.IsConst)
                    varMods |= ModReadonly;
                PushNameToken(tokens, v.NameLineStart, v.NameColumnStart, v.Name.Length, TVariable, varMods);
                CollectDecorators(v.Decorators, tokens);
                if (v.InitialValue != null)
                    CollectExpressionTokens(v.InitialValue, tokens, parameterNames, semanticQuery);
                break;

            case PropertyDef p:
                PushNameToken(tokens, p.NameLineStart, p.NameColumnStart, p.Name.Length, TProperty, ModDeclaration | genMod);
                CollectDecorators(p.Decorators, tokens);
                CollectTokens(p.Body, tokens, semanticQuery);
                foreach (var observer in p.Observers)
                {
                    // Highlight the contextual observer keyword and its declared parameter, then
                    // its body (#416). before_set = 10 chars, after_set = 9.
                    int keywordLen = observer.Kind == ObserverKind.BeforeSet ? 10 : 9;
                    PushNameToken(tokens, observer.LineStart, observer.ColumnStart, keywordLen, TKeyword, 0);
                    PushNameToken(tokens, observer.ParamNameLine, observer.ParamNameColumn, observer.ParamName.Length, TParameter, ModDeclaration);
                    CollectTokens(observer.Body, tokens, semanticQuery);
                }
                break;

            case IfStatement ifStmt:
                CollectExpressionTokens(ifStmt.Test, tokens, parameterNames, semanticQuery);
                CollectStatementList(ifStmt.ThenBody, tokens, parameterNames, semanticQuery);
                foreach (var elif in ifStmt.ElifClauses)
                {
                    CollectExpressionTokens(elif.Test, tokens, parameterNames, semanticQuery);
                    CollectStatementList(elif.Body, tokens, parameterNames, semanticQuery);
                }
                CollectStatementList(ifStmt.ElseBody, tokens, parameterNames, semanticQuery);
                break;

            case ForStatement forStmt:
                CollectExpressionTokens(forStmt.Iterator, tokens, parameterNames, semanticQuery);
                CollectStatementList(forStmt.Body, tokens, parameterNames, semanticQuery);
                CollectStatementList(forStmt.ElseBody, tokens, parameterNames, semanticQuery);
                break;

            case WhileStatement whileStmt:
                CollectExpressionTokens(whileStmt.Test, tokens, parameterNames, semanticQuery);
                CollectStatementList(whileStmt.Body, tokens, parameterNames, semanticQuery);
                CollectStatementList(whileStmt.ElseBody, tokens, parameterNames, semanticQuery);
                break;

            case TryStatement tryStmt:
                CollectStatementList(tryStmt.Body, tokens, parameterNames, semanticQuery);
                foreach (var handler in tryStmt.Handlers)
                    CollectStatementList(handler.Body, tokens, parameterNames, semanticQuery);
                CollectStatementList(tryStmt.ElseBody, tokens, parameterNames, semanticQuery);
                CollectStatementList(tryStmt.FinallyBody, tokens, parameterNames, semanticQuery);
                break;

            case WithStatement withStmt:
                foreach (var item in withStmt.Items)
                    CollectExpressionTokens(item.ContextExpression, tokens, parameterNames, semanticQuery);
                CollectStatementList(withStmt.Body, tokens, parameterNames, semanticQuery);
                break;

            case MatchStatement matchStmt:
                CollectExpressionTokens(matchStmt.Scrutinee, tokens, parameterNames, semanticQuery);
                foreach (var matchCase in matchStmt.Cases)
                    CollectStatementList(matchCase.Body, tokens, parameterNames, semanticQuery);
                break;

            case ExpressionStatement exprStmt:
                CollectExpressionTokens(exprStmt.Expression, tokens, parameterNames, semanticQuery);
                break;

            case ReturnStatement retStmt:
                if (retStmt.Value != null)
                    CollectExpressionTokens(retStmt.Value, tokens, parameterNames, semanticQuery);
                break;

            case Assignment assignStmt:
                CollectExpressionTokens(assignStmt.Target, tokens, parameterNames, semanticQuery);
                CollectExpressionTokens(assignStmt.Value, tokens, parameterNames, semanticQuery);
                break;

            case AssertStatement assertStmt:
                CollectExpressionTokens(assertStmt.Test, tokens, parameterNames, semanticQuery);
                if (assertStmt.Message != null)
                    CollectExpressionTokens(assertStmt.Message, tokens, parameterNames, semanticQuery);
                break;

            case RaiseStatement raiseStmt:
                if (raiseStmt.Exception != null)
                    CollectExpressionTokens(raiseStmt.Exception, tokens, parameterNames, semanticQuery);
                break;

            case YieldStatement yieldStmt:
                CollectExpressionTokens(yieldStmt.Value, tokens, parameterNames, semanticQuery);
                break;

            case DecoratedStatement decorated:
                // Statement-scoped @suppress (#1024): color the decorators and the wrapped
                // statement exactly as if the statement were unwrapped.
                CollectDecorators(decorated.Decorators, tokens);
                CollectStatementTokens(decorated.Statement, tokens, parameterNames, semanticQuery);
                break;
        }
    }

    private static void CollectStatementList(
        IEnumerable<Statement> statements,
        System.Collections.Generic.List<RawToken> tokens,
        HashSet<string>? parameterNames,
        ISemanticQuery? semanticQuery)
    {
        foreach (var stmt in statements)
        {
            CollectStatementTokens(stmt, tokens, parameterNames, semanticQuery);
        }
    }

    private static void CollectFunctionTokens(
        FunctionDef f,
        System.Collections.Generic.List<RawToken> tokens)
    {
        CollectFunctionTokens(f, tokens, extraMods: 0, semanticQuery: null);
    }

    private static void CollectFunctionTokens(
        FunctionDef f,
        System.Collections.Generic.List<RawToken> tokens,
        int extraMods,
        ISemanticQuery? semanticQuery)
    {
        var mods = ModDeclaration | ModDefinition | extraMods;
        if (f.IsAsync)
            mods |= ModAsync;
        if (HasDecorator(f.Decorators, "static"))
            mods |= ModStatic;

        // Function name — use Method when inside a class, Function at top level
        PushNameToken(tokens, f.NameLineStart, f.NameColumnStart, f.Name.Length, TFunction, mods);

        CollectDecorators(f.Decorators, tokens);

        // Parameters — collect names for usage-site tracking. Inherit the "generated"
        // modifier from the enclosing function so callers can theme parameter declarations
        // of generated methods consistently.
        var paramMods = ModDeclaration | extraMods;
        HashSet<string>? parameterNames = null;
        foreach (var param in f.Parameters)
        {
            if (param.Name == "self" || param.Name == "cls")
                continue;
            PushNameToken(tokens, param.LineStart, param.ColumnStart, param.Name.Length, TParameter, paramMods);
            parameterNames ??= new HashSet<string>();
            parameterNames.Add(param.Name);
        }

        // Walk function body with parameter names for usage-site classification
        foreach (var stmt in f.Body)
        {
            CollectStatementTokens(stmt, tokens, parameterNames, semanticQuery);
        }
    }

    /// <summary>
    /// Recursively walks an expression tree to emit keyword tokens for operator-keywords
    /// (not, and, or, in, is) and parameter usage-site tokens.
    /// </summary>
    private static void CollectExpressionTokens(
        Expression expr,
        System.Collections.Generic.List<RawToken> tokens,
        HashSet<string>? parameterNames,
        ISemanticQuery? semanticQuery)
    {
        switch (expr)
        {
            case UnaryOp unary:
                if (unary.Operator == UnaryOperator.Not)
                {
                    // "not" keyword is at the UnaryOp node's start position
                    PushNameToken(tokens, unary.LineStart, unary.ColumnStart, 3, TKeyword, 0);
                }
                CollectExpressionTokens(unary.Operand, tokens, parameterNames, semanticQuery);
                break;

            case BinaryOp binary:
                CollectExpressionTokens(binary.Left, tokens, parameterNames, semanticQuery);
                // Emit keyword tokens for logical/membership operators
                if (binary.OperatorLine > 0)
                {
                    EmitOperatorKeywordFromPosition(tokens, binary.Operator, binary.OperatorLine, binary.OperatorColumn);
                }
                else
                {
                    // Fallback: infer position from right operand (same-line assumption)
                    switch (binary.Operator)
                    {
                        case BinaryOperator.And:
                            EmitInferredKeyword(tokens, binary.Right, 3);
                            break;
                        case BinaryOperator.Or:
                            EmitInferredKeyword(tokens, binary.Right, 2);
                            break;
                        case BinaryOperator.In:
                            EmitInferredKeyword(tokens, binary.Right, 2);
                            break;
                        case BinaryOperator.NotIn:
                            EmitNotInKeywords(tokens, binary.Right);
                            break;
                        case BinaryOperator.Is:
                            EmitInferredKeyword(tokens, binary.Right, 2);
                            break;
                        case BinaryOperator.IsNot:
                            EmitIsNotKeywords(tokens, binary.Right);
                            break;
                    }
                }
                CollectExpressionTokens(binary.Right, tokens, parameterNames, semanticQuery);
                break;

            case ComparisonChain chain:
                for (int i = 0; i < chain.Operands.Length; i++)
                {
                    CollectExpressionTokens(chain.Operands[i], tokens, parameterNames, semanticQuery);
                }
                // Emit keyword tokens for comparison operators that are keywords
                for (int i = 0; i < chain.Operators.Length; i++)
                {
                    if (!chain.OperatorPositions.IsEmpty)
                    {
                        var pos = chain.OperatorPositions[i];
                        EmitComparisonKeywordFromPosition(tokens, chain.Operators[i], pos.Line, pos.Column);
                    }
                    else
                    {
                        // Fallback: infer position from right operand
                        var rightOperand = chain.Operands[i + 1];
                        switch (chain.Operators[i])
                        {
                            case ComparisonOperator.In:
                                EmitInferredKeyword(tokens, rightOperand, 2);
                                break;
                            case ComparisonOperator.NotIn:
                                EmitNotInKeywords(tokens, rightOperand);
                                break;
                            case ComparisonOperator.Is:
                                EmitInferredKeyword(tokens, rightOperand, 2);
                                break;
                            case ComparisonOperator.IsNot:
                                EmitIsNotKeywords(tokens, rightOperand);
                                break;
                        }
                    }
                }
                break;

            case Identifier id:
                // Emit TParameter for identifiers that match a parameter name
                if (parameterNames != null && parameterNames.Contains(id.Name))
                {
                    PushNameToken(tokens, id.LineStart, id.ColumnStart, id.Name.Length, TParameter, 0);
                }
                break;

            case ConditionalExpression cond:
                CollectExpressionTokens(cond.ThenValue, tokens, parameterNames, semanticQuery);
                CollectExpressionTokens(cond.Test, tokens, parameterNames, semanticQuery);
                CollectExpressionTokens(cond.ElseValue, tokens, parameterNames, semanticQuery);
                break;

            case FunctionCall call:
                CollectExpressionTokens(call.Function, tokens, parameterNames, semanticQuery);
                foreach (var arg in call.Arguments)
                    CollectExpressionTokens(arg, tokens, parameterNames, semanticQuery);
                foreach (var kwArg in call.KeywordArguments)
                    CollectExpressionTokens(kwArg.Value, tokens, parameterNames, semanticQuery);
                break;

            case MemberAccess member:
                CollectExpressionTokens(member.Object, tokens, parameterNames, semanticQuery);
                PushMemberToken(member, tokens, semanticQuery);
                break;

            case IndexAccess idx:
                CollectExpressionTokens(idx.Object, tokens, parameterNames, semanticQuery);
                CollectExpressionTokens(idx.Index, tokens, parameterNames, semanticQuery);
                break;

            case SliceAccess slice:
                CollectExpressionTokens(slice.Object, tokens, parameterNames, semanticQuery);
                if (slice.Start != null)
                    CollectExpressionTokens(slice.Start, tokens, parameterNames, semanticQuery);
                if (slice.Stop != null)
                    CollectExpressionTokens(slice.Stop, tokens, parameterNames, semanticQuery);
                if (slice.Step != null)
                    CollectExpressionTokens(slice.Step, tokens, parameterNames, semanticQuery);
                break;

            case MultiAxisAccess multiAxis:
                CollectExpressionTokens(multiAxis.Object, tokens, parameterNames, semanticQuery);
                foreach (var dim in multiAxis.Dimensions)
                {
                    if (dim.IsSlice)
                    {
                        if (dim.Start != null)
                            CollectExpressionTokens(dim.Start, tokens, parameterNames, semanticQuery);
                        if (dim.Stop != null)
                            CollectExpressionTokens(dim.Stop, tokens, parameterNames, semanticQuery);
                        if (dim.Step != null)
                            CollectExpressionTokens(dim.Step, tokens, parameterNames, semanticQuery);
                    }
                    else if (dim.Index != null)
                    {
                        CollectExpressionTokens(dim.Index, tokens, parameterNames, semanticQuery);
                    }
                }
                break;

            case ListLiteral list:
                foreach (var el in list.Elements)
                    CollectExpressionTokens(el, tokens, parameterNames, semanticQuery);
                break;

            case DictLiteral dict:
                foreach (var entry in dict.Entries)
                {
                    if (entry.Key != null)
                        CollectExpressionTokens(entry.Key, tokens, parameterNames, semanticQuery);
                    CollectExpressionTokens(entry.Value, tokens, parameterNames, semanticQuery);
                }
                break;

            case SetLiteral set:
                foreach (var el in set.Elements)
                    CollectExpressionTokens(el, tokens, parameterNames, semanticQuery);
                break;

            case TupleLiteral tuple:
                foreach (var el in tuple.Elements)
                    CollectExpressionTokens(el, tokens, parameterNames, semanticQuery);
                break;

            case ListComprehension listComp:
                CollectExpressionTokens(listComp.Element, tokens, parameterNames, semanticQuery);
                foreach (var clause in listComp.Clauses)
                    CollectComprehensionClauseTokens(clause, tokens, parameterNames, semanticQuery);
                break;

            case SetComprehension setComp:
                CollectExpressionTokens(setComp.Element, tokens, parameterNames, semanticQuery);
                foreach (var clause in setComp.Clauses)
                    CollectComprehensionClauseTokens(clause, tokens, parameterNames, semanticQuery);
                break;

            case DictComprehension dictComp:
                CollectExpressionTokens(dictComp.Key, tokens, parameterNames, semanticQuery);
                CollectExpressionTokens(dictComp.Value, tokens, parameterNames, semanticQuery);
                foreach (var clause in dictComp.Clauses)
                    CollectComprehensionClauseTokens(clause, tokens, parameterNames, semanticQuery);
                break;

            case Parenthesized paren:
                CollectExpressionTokens(paren.Expression, tokens, parameterNames, semanticQuery);
                break;

            case LambdaExpression lambda:
                HashSet<string>? lambdaParamNames = null;
                foreach (var param in lambda.Parameters)
                {
                    PushNameToken(tokens, param.LineStart, param.ColumnStart, param.Name.Length, TParameter, ModDeclaration);
                    lambdaParamNames ??= new HashSet<string>();
                    lambdaParamNames.Add(param.Name);
                    if (param.Type != null)
                        CollectTypeAnnotationTokens(param.Type, tokens);
                }
                CollectExpressionTokens(lambda.Body, tokens, lambdaParamNames, semanticQuery);
                break;

            case TypeCoercion coercion:
                CollectExpressionTokens(coercion.Value, tokens, parameterNames, semanticQuery);
                // Color the cast operator keyword: `as!` / `as?` (3 chars, #1029). The legacy
                // `to` operator (2 chars) was retired in 0.8.0 (#1127), so the length is fixed.
                if (coercion.OperatorLine > 0)
                {
                    const int opLen = 3;
                    PushNameToken(tokens, coercion.OperatorLine, coercion.OperatorColumn, opLen, TKeyword, 0);
                }
                CollectTypeAnnotationTokens(coercion.TargetType, tokens);
                break;

            case TypeCheck check:
                CollectExpressionTokens(check.Value, tokens, parameterNames, semanticQuery);
                break;

            case WalrusExpression walrus:
                CollectExpressionTokens(walrus.Value, tokens, parameterNames, semanticQuery);
                break;

            case FStringLiteral fstr:
                foreach (var part in fstr.Parts)
                {
                    if (part.Expression != null)
                        CollectExpressionTokens(part.Expression, tokens, parameterNames, semanticQuery);
                }
                break;

            case TStringLiteral tstr:
                foreach (var part in tstr.Parts)
                {
                    if (part.Expression != null)
                        CollectExpressionTokens(part.Expression, tokens, parameterNames, semanticQuery);
                }
                break;

            case TryExpression tryExpr:
                CollectExpressionTokens(tryExpr.Operand, tokens, parameterNames, semanticQuery);
                break;

            case MaybeExpression maybeExpr:
                CollectExpressionTokens(maybeExpr.Operand, tokens, parameterNames, semanticQuery);
                break;

            case QuestionMarkExpression questionMark:
                CollectExpressionTokens(questionMark.Operand, tokens, parameterNames, semanticQuery);
                // The postfix "?" sits just before ColumnEnd (which is questionToken.Column + 1).
                // Highlight it as an operator-keyword, consistent with and/or/not/is/in.
                PushNameToken(tokens, questionMark.LineEnd, questionMark.ColumnEnd - 1, 1, TKeyword, 0);
                break;

            case StarExpression star:
                CollectExpressionTokens(star.Operand, tokens, parameterNames, semanticQuery);
                break;

            case SpreadElement spread:
                CollectExpressionTokens(spread.Value, tokens, parameterNames, semanticQuery);
                break;

            case StringLiteral strLit:
                EmitStringLiteralToken(tokens, strLit.LineStart, strLit.ColumnStart, strLit.LineEnd, strLit.ColumnEnd);
                break;

            case BytesLiteralExpression bytesLit:
                EmitStringLiteralToken(tokens, bytesLit.LineStart, bytesLit.ColumnStart, bytesLit.LineEnd, bytesLit.ColumnEnd);
                break;

            case ModifiedArgument modArg:
                // Emit the modifier keyword (ref/out/in) as a keyword token
                var modLen = modArg.Modifier switch
                {
                    ParameterModifier.In => 2,   // "in"
                    _ => 3                        // "ref" or "out"
                };
                PushNameToken(tokens, modArg.LineStart, modArg.ColumnStart, modLen, TKeyword, 0);
                if (modArg.InlineType != null)
                    CollectTypeAnnotationTokens(modArg.InlineType, tokens);
                // Recurse into the argument expression
                CollectExpressionTokens(modArg.Argument, tokens, parameterNames, semanticQuery);
                break;

        }
    }

    private static void CollectTypeAnnotationTokens(
        TypeAnnotation type,
        System.Collections.Generic.List<RawToken> tokens)
    {
        PushNameToken(tokens, type.LineStart, type.ColumnStart, type.Name.Length, TType, 0);
        foreach (var arg in type.TypeArguments)
            CollectTypeAnnotationTokens(arg, tokens);
    }

    private static void CollectComprehensionClauseTokens(
        ComprehensionClause clause,
        System.Collections.Generic.List<RawToken> tokens,
        HashSet<string>? parameterNames,
        ISemanticQuery? semanticQuery)
    {
        switch (clause)
        {
            case ForClause forClause:
                CollectExpressionTokens(forClause.Target, tokens, parameterNames, semanticQuery);
                CollectExpressionTokens(forClause.Iterator, tokens, parameterNames, semanticQuery);
                break;
            case IfClause ifClause:
                CollectExpressionTokens(ifClause.Condition, tokens, parameterNames, semanticQuery);
                break;
        }
    }

    /// <summary>
    /// Emits keyword token(s) for a BinaryOp operator using stored position from the AST.
    /// </summary>
    private static void EmitOperatorKeywordFromPosition(
        System.Collections.Generic.List<RawToken> tokens,
        BinaryOperator op,
        int line, int column)
    {
        switch (op)
        {
            case BinaryOperator.And:
                PushNameToken(tokens, line, column, 3, TKeyword, 0); // "and"
                break;
            case BinaryOperator.Or:
                PushNameToken(tokens, line, column, 2, TKeyword, 0); // "or"
                break;
            case BinaryOperator.In:
                PushNameToken(tokens, line, column, 2, TKeyword, 0); // "in"
                break;
            case BinaryOperator.NotIn:
                // "not in": position is at "not", "in" follows after "not "
                PushNameToken(tokens, line, column, 3, TKeyword, 0);     // "not"
                PushNameToken(tokens, line, column + 4, 2, TKeyword, 0); // "in"
                break;
            case BinaryOperator.Is:
                PushNameToken(tokens, line, column, 2, TKeyword, 0); // "is"
                break;
            case BinaryOperator.IsNot:
                // "is not": position is at "is", "not" follows after "is "
                PushNameToken(tokens, line, column, 2, TKeyword, 0);     // "is"
                PushNameToken(tokens, line, column + 3, 3, TKeyword, 0); // "not"
                break;
        }
    }

    /// <summary>
    /// Emits keyword token(s) for a ComparisonChain operator using stored position from the AST.
    /// </summary>
    private static void EmitComparisonKeywordFromPosition(
        System.Collections.Generic.List<RawToken> tokens,
        ComparisonOperator op,
        int line, int column)
    {
        switch (op)
        {
            case ComparisonOperator.In:
                PushNameToken(tokens, line, column, 2, TKeyword, 0); // "in"
                break;
            case ComparisonOperator.NotIn:
                PushNameToken(tokens, line, column, 3, TKeyword, 0);     // "not"
                PushNameToken(tokens, line, column + 4, 2, TKeyword, 0); // "in"
                break;
            case ComparisonOperator.Is:
                PushNameToken(tokens, line, column, 2, TKeyword, 0); // "is"
                break;
            case ComparisonOperator.IsNot:
                PushNameToken(tokens, line, column, 2, TKeyword, 0);     // "is"
                PushNameToken(tokens, line, column + 3, 3, TKeyword, 0); // "not"
                break;
        }
    }

    /// <summary>
    /// Emits a keyword token at the inferred position before the right operand.
    /// The keyword is assumed to be at (rightOperand.LineStart, rightOperand.ColumnStart - length - 1).
    /// Fallback for AST nodes without stored operator positions.
    /// </summary>
    private static void EmitInferredKeyword(
        System.Collections.Generic.List<RawToken> tokens,
        Expression rightOperand,
        int keywordLength)
    {
        var col = rightOperand.ColumnStart - keywordLength - 1;
        if (col >= 1) // 1-based compiler coordinates
        {
            PushNameToken(tokens, rightOperand.LineStart, col, keywordLength, TKeyword, 0);
        }
    }

    /// <summary>
    /// Emits "not" and "in" as separate keyword tokens for a "not in" operator.
    /// </summary>
    private static void EmitNotInKeywords(
        System.Collections.Generic.List<RawToken> tokens,
        Expression rightOperand)
    {
        // "in" is right before the right operand
        var inCol = rightOperand.ColumnStart - 3; // "in "
        if (inCol >= 1)
        {
            PushNameToken(tokens, rightOperand.LineStart, inCol, 2, TKeyword, 0);
        }
        // "not" is before "in"
        var notCol = inCol - 4; // "not "
        if (notCol >= 1)
        {
            PushNameToken(tokens, rightOperand.LineStart, notCol, 3, TKeyword, 0);
        }
    }

    /// <summary>
    /// Emits "is" and "not" as separate keyword tokens for an "is not" operator.
    /// </summary>
    private static void EmitIsNotKeywords(
        System.Collections.Generic.List<RawToken> tokens,
        Expression rightOperand)
    {
        // "not" is right before the right operand
        var notCol = rightOperand.ColumnStart - 4; // "not "
        if (notCol >= 1)
        {
            PushNameToken(tokens, rightOperand.LineStart, notCol, 3, TKeyword, 0);
        }
        // "is" is before "not"
        var isCol = notCol - 3; // "is "
        if (isCol >= 1)
        {
            PushNameToken(tokens, rightOperand.LineStart, isCol, 2, TKeyword, 0);
        }
    }

    private static void CollectDecorators(
        IEnumerable<Decorator> decorators,
        System.Collections.Generic.List<RawToken> tokens)
    {
        foreach (var dec in decorators)
        {
            var name = dec.Name;
            if (name.Length > 0)
            {
                // @[name regular: +2 for @ and [ (] is after args, not adjacent)
                // @name regular decorators: +1 for @
                var length = dec.IsBracketAttribute ? name.Length + 2 : name.Length + 1;
                PushNameToken(tokens, dec.LineStart, dec.ColumnStart, length, TDecorator, 0);
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

    /// <summary>
    /// Emits semantic tokens for a string literal span.
    /// For single-line literals, emits one token.
    /// For multi-line literals, emits per-line tokens.
    /// </summary>
    private static void EmitStringLiteralToken(
        System.Collections.Generic.List<RawToken> tokens,
        int lineStart,
        int colStart,
        int lineEnd,
        int colEnd)
    {
        if (lineStart == lineEnd)
        {
            // Single-line string literal
            var length = colEnd - colStart;
            if (length > 0)
                PushNameToken(tokens, lineStart, colStart, length, TString, 0);
        }
        else
        {
            // Multi-line: just emit first line to end, then skip interior.
            // Semantic tokens per-line are complex; emit the first line only.
            // VS Code TextMate grammar will handle the rest.
            PushNameToken(tokens, lineStart, colStart, 200, TString, 0); // conservative length
        }
    }

    /// <summary>
    /// Emits a token for the member name of a <see cref="MemberAccess"/> (#1376). The object is
    /// walked separately; before this, <c>math.pi</c>, <c>obj.field</c> and <c>Module.func</c> all
    /// left the member name untokenized and editors fell back to plain-identifier coloring.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Classification comes from the expression types, NOT from
    /// <c>ISemanticQuery.GetMemberAccessResolution</c>, which the issue's acceptance sketch assumed.
    /// Measured, that map answers only static/const/enum/union shapes — its six recording sites are
    /// all gated on one — so it returns null for <c>obj.field</c>, <c>obj.method()</c>,
    /// <c>o.inner.n</c> and every module member, i.e. for essentially everything this arm exists to
    /// color. Worse, the emitter treats a recorded resolution as a LOWERING DIRECTIVE: a
    /// <c>VariableSymbol</c> entry makes it rewrite <c>obj.X</c> to <c>Owner.X</c> with no static
    /// re-check, so populating it for instance members to serve tooling would change emitted C#.
    /// It is a codegen materialization channel, not a general "what does this resolve to" query.
    /// </para>
    /// <para>
    /// The node's own type is enough and needs no compiler change: a member that resolves to a
    /// <see cref="FunctionType"/> is a method, anything else is data. That covers instance, module,
    /// chained and escaped members uniformly, which the resolution map does not.
    /// </para>
    /// <para>
    /// <see cref="MemberAccess"/> has no child node for the member name, so the extent is computed:
    /// the object's <c>ColumnEnd</c> is the separator's column, and the node's own <c>ColumnEnd</c>
    /// is one past the member's last character. Null-conditional access spends an extra character
    /// (<c>?.</c>), which is why the offset is not simply +1 — measured on <c>c?.value</c>, where
    /// the member begins two columns after the object ends. A backticked member is covered by the
    /// same arithmetic: the extent includes both backticks, matching the recorded token span
    /// (cb429fdc1).
    /// </para>
    /// <para>
    /// No token is emitted without a semantic query. <c>Tokenize</c> falls back to a parse-only
    /// result when analysis is unavailable, and guessing a kind from syntax alone would color a
    /// method as a property roughly half the time.
    /// </para>
    /// </remarks>
    private static void PushMemberToken(
        MemberAccess member,
        System.Collections.Generic.List<RawToken> tokens,
        ISemanticQuery? semanticQuery)
    {
        if (semanticQuery == null)
            return;

        // Multi-line member chains would make column arithmetic on the object meaningless.
        if (member.Object.LineEnd != member.LineEnd)
            return;

        var separatorWidth = member.IsNullConditional ? 2 : 1;
        var startColumn = member.Object.ColumnEnd + separatorWidth;
        var length = member.ColumnEnd - startColumn;
        if (length <= 0)
            return;

        var tokenType = semanticQuery.GetEffectiveType(member) is Sharpy.Compiler.Semantic.FunctionType ? TMethod : TProperty;
        PushNameToken(tokens, member.LineEnd, startColumn, length, tokenType, modifiers: 0);
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
