using System.Collections.Immutable;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Shared;

/// <summary>
/// Recursively traverses compound statements (if/while/for/try/with/match)
/// while skipping nested definition scopes (FunctionDef, ClassDef, StructDef,
/// InterfaceDef, EnumDef).
/// </summary>
internal static class StatementWalker
{
    /// <summary>
    /// Returns true if any statement satisfies the predicate (with early exit).
    /// </summary>
    public static bool Any(
        ImmutableArray<Statement> statements,
        Func<Statement, bool> predicate)
    {
        foreach (var stmt in statements)
        {
            if (predicate(stmt))
                return true;

            if (TryRecurse(stmt, predicate))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Returns the first non-null result from the selector (with early exit).
    /// </summary>
    public static T? FirstOrDefault<T>(
        ImmutableArray<Statement> statements,
        Func<Statement, T?> selector) where T : class
    {
        foreach (var stmt in statements)
        {
            var result = selector(stmt);
            if (result != null)
                return result;

            var found = TryRecurseSelect(stmt, selector);
            if (found != null)
                return found;
        }

        return null;
    }

    private static bool TryRecurse(
        Statement stmt,
        Func<Statement, bool> predicate)
    {
        // Skip nested definition scopes
        if (stmt is FunctionDef or ClassDef or StructDef or InterfaceDef or EnumDef)
            return false;

        if (stmt is IfStatement ifStmt)
        {
            if (Any(ifStmt.ThenBody, predicate))
                return true;
            foreach (var elif in ifStmt.ElifClauses)
            {
                if (Any(elif.Body, predicate))
                    return true;
            }
            if (Any(ifStmt.ElseBody, predicate))
                return true;
        }
        else if (stmt is WhileStatement whileStmt)
        {
            if (Any(whileStmt.Body, predicate))
                return true;
            if (Any(whileStmt.ElseBody, predicate))
                return true;
        }
        else if (stmt is ForStatement forStmt)
        {
            if (Any(forStmt.Body, predicate))
                return true;
            if (Any(forStmt.ElseBody, predicate))
                return true;
        }
        else if (stmt is TryStatement tryStmt)
        {
            if (Any(tryStmt.Body, predicate))
                return true;
            foreach (var handler in tryStmt.Handlers)
            {
                if (Any(handler.Body, predicate))
                    return true;
            }
            if (Any(tryStmt.ElseBody, predicate))
                return true;
            if (tryStmt.FinallyBody != null && Any(tryStmt.FinallyBody, predicate))
                return true;
        }
        else if (stmt is WithStatement withStmt)
        {
            if (Any(withStmt.Body, predicate))
                return true;
        }
        else if (stmt is MatchStatement matchStmt)
        {
            foreach (var matchCase in matchStmt.Cases)
            {
                if (Any(matchCase.Body, predicate))
                    return true;
            }
        }

        return false;
    }

    private static T? TryRecurseSelect<T>(
        Statement stmt,
        Func<Statement, T?> selector) where T : class
    {
        // Skip nested definition scopes
        if (stmt is FunctionDef or ClassDef or StructDef or InterfaceDef or EnumDef)
            return null;

        if (stmt is IfStatement ifStmt)
        {
            var found = FirstOrDefault(ifStmt.ThenBody, selector);
            if (found != null)
                return found;
            foreach (var elif in ifStmt.ElifClauses)
            {
                found = FirstOrDefault(elif.Body, selector);
                if (found != null)
                    return found;
            }
            found = FirstOrDefault(ifStmt.ElseBody, selector);
            if (found != null)
                return found;
        }
        else if (stmt is WhileStatement whileStmt)
        {
            var found = FirstOrDefault(whileStmt.Body, selector);
            if (found != null)
                return found;
            found = FirstOrDefault(whileStmt.ElseBody, selector);
            if (found != null)
                return found;
        }
        else if (stmt is ForStatement forStmt)
        {
            var found = FirstOrDefault(forStmt.Body, selector);
            if (found != null)
                return found;
            found = FirstOrDefault(forStmt.ElseBody, selector);
            if (found != null)
                return found;
        }
        else if (stmt is TryStatement tryStmt)
        {
            var found = FirstOrDefault(tryStmt.Body, selector);
            if (found != null)
                return found;
            foreach (var handler in tryStmt.Handlers)
            {
                found = FirstOrDefault(handler.Body, selector);
                if (found != null)
                    return found;
            }
            found = FirstOrDefault(tryStmt.ElseBody, selector);
            if (found != null)
                return found;
            if (tryStmt.FinallyBody != null)
            {
                found = FirstOrDefault(tryStmt.FinallyBody, selector);
                if (found != null)
                    return found;
            }
        }
        else if (stmt is WithStatement withStmt)
        {
            var found = FirstOrDefault(withStmt.Body, selector);
            if (found != null)
                return found;
        }
        else if (stmt is MatchStatement matchStmt)
        {
            foreach (var matchCase in matchStmt.Cases)
            {
                var found = FirstOrDefault(matchCase.Body, selector);
                if (found != null)
                    return found;
            }
        }

        return null;
    }
}
