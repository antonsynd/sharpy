using Microsoft.Extensions.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Services;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// Resolves the symbol under a cursor that sits on a declaration node (parameter, def, class, as-name,
/// etc.) rather than on an Identifier reference. Lifted from <c>RenameHandler</c> so that
/// <c>ReferencesHandler</c> and <c>DocumentHighlightHandler</c> share the same resolution (#1539).
/// </summary>
/// <remarks>
/// <c>DefinitionHandler.cs:56-62</c>, <c>ImplementationHandler.cs:83-87</c>, and
/// <c>CallHierarchyPrepareHandler.cs:60-66</c> carry the same two-arm <c>Identifier</c>/<c>FunctionCall</c>
/// switch — future consumers, out of #1539's stated scope.
/// </remarks>
internal static class DeclarationCursorResolver
{
    /// <summary>
    /// Returns the symbol the cursor sits on, considering both reference nodes (Identifier,
    /// FunctionCall) and declaration nodes (VariableDeclaration, FunctionDef, ClassDef, etc.).
    /// </summary>
    public static Symbol? Resolve(Node node, ISemanticQuery query, int line, int col, ILogger? logger)
    {
        if (node is Identifier id)
            return query.GetIdentifierSymbol(id);

        if (node is FunctionCall call)
            return query.GetCallTarget(call);

        if (node is VariableDeclaration varDecl
            && IsOnNameExtent(line, col, varDecl.NameLineStart, varDecl.NameColumnStart,
                varDecl.NameColumnEnd))
        {
            var bound = query.GetDeclarationSymbol(varDecl);
            if (bound != null)
                return bound;

            logger?.LogDebug(
                "DeclarationCursorResolver: no node-keyed symbol for variable declaration '{Name}' at {Line}:{Column}; "
                + "falling back to the declaration scan.",
                varDecl.Name, varDecl.NameLineStart, varDecl.NameColumnStart);

            return query.FindSymbolByDeclaration(varDecl.Name, varDecl.LineStart, varDecl.ColumnStart);
        }

        if (node is FunctionDef funcDef)
        {
            if (IsOnNameExtent(line, col, funcDef.NameLineStart, funcDef.NameColumnStart,
                    funcDef.NameColumnEnd))
            {
                var bound = query.GetFunctionDeclarationSymbol(funcDef);
                if (bound != null)
                    return bound;

                logger?.LogDebug(
                    "DeclarationCursorResolver: no node-keyed symbol for function definition '{Name}' at {Line}:{Column}; "
                    + "falling back to the declaration scan.",
                    funcDef.Name, funcDef.NameLineStart, funcDef.NameColumnStart);

                return query.FindSymbolByDeclaration(funcDef.Name, funcDef.LineStart, funcDef.ColumnStart);
            }

            var parameterSymbol = ResolveParameterSymbol(funcDef.Parameters, query, line, col, logger);
            if (parameterSymbol != null)
                return parameterSymbol;
        }

        if (node is LambdaExpression lambda)
        {
            var lambdaParameterSymbol = ResolveParameterSymbol(lambda.Parameters, query, line, col, logger);
            if (lambdaParameterSymbol != null)
                return lambdaParameterSymbol;
        }

        if (node is TryStatement tryStmt)
            return ResolveExceptHandlerSymbol(tryStmt, query, line, col, logger);

        if (node is WithStatement withStmt)
            return ResolveWithItemSymbol(withStmt, query, line, col, logger);

        if (node is TypeAlias typeAlias
            && IsOnNameExtent(line, col, typeAlias.NameLineStart, typeAlias.NameColumnStart,
                typeAlias.NameColumnEnd))
        {
            return query.FindSymbolByDeclaration(typeAlias.Name, typeAlias.LineStart, typeAlias.ColumnStart);
        }

        if (node is PropertyDef propDef
            && IsOnNameExtent(line, col, propDef.NameLineStart, propDef.NameColumnStart,
                propDef.NameColumnEnd))
        {
            return query.FindSymbolByDeclaration(propDef.Name, propDef.LineStart, propDef.ColumnStart);
        }

        // ClassDef-family: FindSymbolByDeclaration uses STATEMENT START (LineStart, ColumnStart),
        // not name start — the symbol table records the declaration at the statement position.
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
    /// Whether the cursor sits inside a RECORDED name extent — <paramref name="nameColEnd"/> is the
    /// parser's exclusive end, taken from the name token, so escaped spellings are already covered
    /// and no backtick compensation applies here (#1454). A node with no recorded extent reports
    /// 0/0, which no 1-based cursor can be inside.
    /// </summary>
    internal static bool IsOnNameExtent(
        int cursorLine, int cursorCol, int nameLineStart, int nameColStart, int nameColEnd)
    {
        return cursorLine == nameLineStart
            && cursorCol >= nameColStart
            && cursorCol < nameColEnd;
    }

    private static Symbol? ResolveParameterSymbol(
        System.Collections.Immutable.ImmutableArray<Parameter> parameters,
        ISemanticQuery query,
        int line,
        int col,
        ILogger? logger)
    {
        foreach (var param in parameters)
        {
            if (!IsOnNameExtent(line, col, param.NameLineStart, param.NameColumnStart, param.NameColumnEnd))
                continue;

            var bound = query.GetParameterSymbol(param);
            if (bound != null)
                return bound;

            logger?.LogDebug(
                "DeclarationCursorResolver: no node-keyed symbol for parameter '{Name}' at {Line}:{Column}.",
                param.Name, param.NameLineStart, param.NameColumnStart);

            return null;
        }

        return null;
    }

    private static Symbol? ResolveExceptHandlerSymbol(
        TryStatement t, ISemanticQuery query, int line, int col, ILogger? logger)
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

            logger?.LogDebug(
                "DeclarationCursorResolver: no node-keyed symbol for except-as name '{Name}' at {Line}:{Column}; "
                + "falling back to the declaration scan.",
                handler.Name, handler.NameLineStart, handler.NameColumnStart);

            return query.FindSymbolByDeclaration(handler.Name, handler.LineStart, handler.ColumnStart);
        }

        return null;
    }

    private static Symbol? ResolveWithItemSymbol(
        WithStatement w, ISemanticQuery query, int line, int col, ILogger? logger)
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

            logger?.LogDebug(
                "DeclarationCursorResolver: no node-keyed symbol for with-as name '{Name}' at {Line}:{Column}; "
                + "falling back to the declaration scan.",
                item.Name, item.NameLineStart, item.NameColumnStart);

            return query.FindSymbolByDeclaration(item.Name, item.LineStart, item.ColumnStart);
        }

        return null;
    }
}
