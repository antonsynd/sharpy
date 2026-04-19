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
        SemanticTokenType.String,     // 13
        SemanticTokenType.Number,     // 14
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
    internal const int TString = 13;
    internal const int TNumber = 14;

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
        CollectStatementTokens(stmt, tokens, null);
    }

    private static void CollectStatementTokens(
        Statement stmt,
        System.Collections.Generic.List<RawToken> tokens,
        HashSet<string>? parameterNames)
    {
        switch (stmt)
        {
            case FunctionDef f:
                CollectFunctionTokens(f, tokens);
                break;

            case ClassDef c:
                PushNameToken(tokens, c.NameLineStart, c.NameColumnStart, c.Name.Length, TClass, ModDeclaration | ModDefinition);
                CollectDecorators(c.Decorators, tokens);
                CollectTokens(c.Body, tokens);
                break;

            case StructDef s:
                PushNameToken(tokens, s.NameLineStart, s.NameColumnStart, s.Name.Length, TStruct, ModDeclaration | ModDefinition);
                CollectDecorators(s.Decorators, tokens);
                CollectTokens(s.Body, tokens);
                break;

            case InterfaceDef i:
                PushNameToken(tokens, i.NameLineStart, i.NameColumnStart, i.Name.Length, TInterface, ModDeclaration | ModDefinition);
                CollectDecorators(i.Decorators, tokens);
                CollectTokens(i.Body, tokens);
                break;

            case EnumDef e:
                PushNameToken(tokens, e.NameLineStart, e.NameColumnStart, e.Name.Length, TEnum, ModDeclaration | ModDefinition);
                foreach (var member in e.Members)
                {
                    PushNameToken(tokens, member.LineStart, member.ColumnStart, member.Name.Length, TEnumMember, ModDeclaration);
                }
                break;

            case VariableDeclaration v:
                var varMods = ModDeclaration;
                if (v.IsConst)
                    varMods |= ModReadonly;
                PushNameToken(tokens, v.NameLineStart, v.NameColumnStart, v.Name.Length, TVariable, varMods);
                CollectDecorators(v.Decorators, tokens);
                if (v.InitialValue != null)
                    CollectExpressionTokens(v.InitialValue, tokens, parameterNames);
                break;

            case PropertyDef p:
                PushNameToken(tokens, p.NameLineStart, p.NameColumnStart, p.Name.Length, TProperty, ModDeclaration);
                CollectDecorators(p.Decorators, tokens);
                CollectTokens(p.Body, tokens);
                break;

            case IfStatement ifStmt:
                CollectExpressionTokens(ifStmt.Test, tokens, parameterNames);
                CollectStatementList(ifStmt.ThenBody, tokens, parameterNames);
                foreach (var elif in ifStmt.ElifClauses)
                {
                    CollectExpressionTokens(elif.Test, tokens, parameterNames);
                    CollectStatementList(elif.Body, tokens, parameterNames);
                }
                CollectStatementList(ifStmt.ElseBody, tokens, parameterNames);
                break;

            case ForStatement forStmt:
                CollectExpressionTokens(forStmt.Iterator, tokens, parameterNames);
                CollectStatementList(forStmt.Body, tokens, parameterNames);
                CollectStatementList(forStmt.ElseBody, tokens, parameterNames);
                break;

            case WhileStatement whileStmt:
                CollectExpressionTokens(whileStmt.Test, tokens, parameterNames);
                CollectStatementList(whileStmt.Body, tokens, parameterNames);
                CollectStatementList(whileStmt.ElseBody, tokens, parameterNames);
                break;

            case TryStatement tryStmt:
                CollectStatementList(tryStmt.Body, tokens, parameterNames);
                foreach (var handler in tryStmt.Handlers)
                    CollectStatementList(handler.Body, tokens, parameterNames);
                CollectStatementList(tryStmt.ElseBody, tokens, parameterNames);
                CollectStatementList(tryStmt.FinallyBody, tokens, parameterNames);
                break;

            case WithStatement withStmt:
                foreach (var item in withStmt.Items)
                    CollectExpressionTokens(item.ContextExpression, tokens, parameterNames);
                CollectStatementList(withStmt.Body, tokens, parameterNames);
                break;

            case MatchStatement matchStmt:
                CollectExpressionTokens(matchStmt.Scrutinee, tokens, parameterNames);
                foreach (var matchCase in matchStmt.Cases)
                    CollectStatementList(matchCase.Body, tokens, parameterNames);
                break;

            case ExpressionStatement exprStmt:
                CollectExpressionTokens(exprStmt.Expression, tokens, parameterNames);
                break;

            case ReturnStatement retStmt:
                if (retStmt.Value != null)
                    CollectExpressionTokens(retStmt.Value, tokens, parameterNames);
                break;

            case Assignment assignStmt:
                CollectExpressionTokens(assignStmt.Target, tokens, parameterNames);
                CollectExpressionTokens(assignStmt.Value, tokens, parameterNames);
                break;

            case AssertStatement assertStmt:
                CollectExpressionTokens(assertStmt.Test, tokens, parameterNames);
                if (assertStmt.Message != null)
                    CollectExpressionTokens(assertStmt.Message, tokens, parameterNames);
                break;

            case RaiseStatement raiseStmt:
                if (raiseStmt.Exception != null)
                    CollectExpressionTokens(raiseStmt.Exception, tokens, parameterNames);
                break;

            case YieldStatement yieldStmt:
                CollectExpressionTokens(yieldStmt.Value, tokens, parameterNames);
                break;
        }
    }

    private static void CollectStatementList(
        IEnumerable<Statement> statements,
        System.Collections.Generic.List<RawToken> tokens,
        HashSet<string>? parameterNames)
    {
        foreach (var stmt in statements)
        {
            CollectStatementTokens(stmt, tokens, parameterNames);
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
        PushNameToken(tokens, f.NameLineStart, f.NameColumnStart, f.Name.Length, TFunction, mods);

        CollectDecorators(f.Decorators, tokens);

        // Parameters — collect names for usage-site tracking
        HashSet<string>? parameterNames = null;
        foreach (var param in f.Parameters)
        {
            if (param.Name == "self" || param.Name == "cls")
                continue;
            PushNameToken(tokens, param.LineStart, param.ColumnStart, param.Name.Length, TParameter, ModDeclaration);
            parameterNames ??= new HashSet<string>();
            parameterNames.Add(param.Name);
        }

        // Walk function body with parameter names for usage-site classification
        foreach (var stmt in f.Body)
        {
            CollectStatementTokens(stmt, tokens, parameterNames);
        }
    }

    /// <summary>
    /// Recursively walks an expression tree to emit keyword tokens for operator-keywords
    /// (not, and, or, in, is) and parameter usage-site tokens.
    /// </summary>
    private static void CollectExpressionTokens(
        Expression expr,
        System.Collections.Generic.List<RawToken> tokens,
        HashSet<string>? parameterNames)
    {
        switch (expr)
        {
            case UnaryOp unary:
                if (unary.Operator == UnaryOperator.Not)
                {
                    // "not" keyword is at the UnaryOp node's start position
                    PushNameToken(tokens, unary.LineStart, unary.ColumnStart, 3, TKeyword, 0);
                }
                CollectExpressionTokens(unary.Operand, tokens, parameterNames);
                break;

            case BinaryOp binary:
                CollectExpressionTokens(binary.Left, tokens, parameterNames);
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
                CollectExpressionTokens(binary.Right, tokens, parameterNames);
                break;

            case ComparisonChain chain:
                for (int i = 0; i < chain.Operands.Length; i++)
                {
                    CollectExpressionTokens(chain.Operands[i], tokens, parameterNames);
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
                CollectExpressionTokens(cond.ThenValue, tokens, parameterNames);
                CollectExpressionTokens(cond.Test, tokens, parameterNames);
                CollectExpressionTokens(cond.ElseValue, tokens, parameterNames);
                break;

            case FunctionCall call:
                CollectExpressionTokens(call.Function, tokens, parameterNames);
                foreach (var arg in call.Arguments)
                    CollectExpressionTokens(arg, tokens, parameterNames);
                foreach (var kwArg in call.KeywordArguments)
                    CollectExpressionTokens(kwArg.Value, tokens, parameterNames);
                break;

            case MemberAccess member:
                CollectExpressionTokens(member.Object, tokens, parameterNames);
                break;

            case IndexAccess idx:
                CollectExpressionTokens(idx.Object, tokens, parameterNames);
                CollectExpressionTokens(idx.Index, tokens, parameterNames);
                break;

            case SliceAccess slice:
                CollectExpressionTokens(slice.Object, tokens, parameterNames);
                if (slice.Start != null)
                    CollectExpressionTokens(slice.Start, tokens, parameterNames);
                if (slice.Stop != null)
                    CollectExpressionTokens(slice.Stop, tokens, parameterNames);
                if (slice.Step != null)
                    CollectExpressionTokens(slice.Step, tokens, parameterNames);
                break;

            case ListLiteral list:
                foreach (var el in list.Elements)
                    CollectExpressionTokens(el, tokens, parameterNames);
                break;

            case DictLiteral dict:
                foreach (var entry in dict.Entries)
                {
                    if (entry.Key != null)
                        CollectExpressionTokens(entry.Key, tokens, parameterNames);
                    CollectExpressionTokens(entry.Value, tokens, parameterNames);
                }
                break;

            case SetLiteral set:
                foreach (var el in set.Elements)
                    CollectExpressionTokens(el, tokens, parameterNames);
                break;

            case TupleLiteral tuple:
                foreach (var el in tuple.Elements)
                    CollectExpressionTokens(el, tokens, parameterNames);
                break;

            case ListComprehension listComp:
                CollectExpressionTokens(listComp.Element, tokens, parameterNames);
                foreach (var clause in listComp.Clauses)
                    CollectComprehensionClauseTokens(clause, tokens, parameterNames);
                break;

            case SetComprehension setComp:
                CollectExpressionTokens(setComp.Element, tokens, parameterNames);
                foreach (var clause in setComp.Clauses)
                    CollectComprehensionClauseTokens(clause, tokens, parameterNames);
                break;

            case DictComprehension dictComp:
                CollectExpressionTokens(dictComp.Key, tokens, parameterNames);
                CollectExpressionTokens(dictComp.Value, tokens, parameterNames);
                foreach (var clause in dictComp.Clauses)
                    CollectComprehensionClauseTokens(clause, tokens, parameterNames);
                break;

            case Parenthesized paren:
                CollectExpressionTokens(paren.Expression, tokens, parameterNames);
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
                CollectExpressionTokens(lambda.Body, tokens, lambdaParamNames);
                break;

            case TypeCoercion coercion:
                CollectExpressionTokens(coercion.Value, tokens, parameterNames);
                break;

            case TypeCheck check:
                CollectExpressionTokens(check.Value, tokens, parameterNames);
                break;

            case WalrusExpression walrus:
                CollectExpressionTokens(walrus.Value, tokens, parameterNames);
                break;

            case FStringLiteral fstr:
                foreach (var part in fstr.Parts)
                {
                    if (part.Expression != null)
                        CollectExpressionTokens(part.Expression, tokens, parameterNames);
                }
                break;

            case TryExpression tryExpr:
                CollectExpressionTokens(tryExpr.Operand, tokens, parameterNames);
                break;

            case MaybeExpression maybeExpr:
                CollectExpressionTokens(maybeExpr.Operand, tokens, parameterNames);
                break;

            case StarExpression star:
                CollectExpressionTokens(star.Operand, tokens, parameterNames);
                break;

            case SpreadElement spread:
                CollectExpressionTokens(spread.Value, tokens, parameterNames);
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
                CollectExpressionTokens(modArg.Argument, tokens, parameterNames);
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
        HashSet<string>? parameterNames)
    {
        switch (clause)
        {
            case ForClause forClause:
                CollectExpressionTokens(forClause.Target, tokens, parameterNames);
                CollectExpressionTokens(forClause.Iterator, tokens, parameterNames);
                break;
            case IfClause ifClause:
                CollectExpressionTokens(ifClause.Condition, tokens, parameterNames);
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
