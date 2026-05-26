using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Validation for memoization decorators: @lru_cache and @cache.
/// </summary>
internal partial class DecoratorValidator
{
    /// <summary>
    /// Validates @lru_cache and @cache decorator arguments.
    /// @cache must have no arguments. @lru_cache(maxsize=N) accepts a single optional
    /// 'maxsize' keyword (or single positional) that must be a non-negative integer
    /// literal or None.
    /// </summary>
    private void ValidateLruCacheArguments(IEnumerable<Decorator> decorators, string definitionName)
    {
        foreach (var decorator in decorators)
        {
            if (decorator.Name == DecoratorNames.Cache)
            {
                if (decorator.Arguments.Length > 0 || decorator.KeywordArguments.Length > 0)
                {
                    AddError(
                        $"'@cache' on '{definitionName}' does not accept arguments. " +
                        "Use '@lru_cache(maxsize=N)' to set a bound.",
                        decorator.LineStart,
                        decorator.ColumnStart,
                        code: DiagnosticCodes.Validation.LruCacheInvalidMaxSize,
                        span: decorator.Span);
                }
                continue;
            }

            if (decorator.Name != DecoratorNames.LruCache)
                continue;

            // No arguments → equivalent to @cache (unbounded). Allowed.
            if (decorator.Arguments.Length == 0 && decorator.KeywordArguments.Length == 0)
                continue;

            // Disallow multiple arguments
            int totalArgs = decorator.Arguments.Length + decorator.KeywordArguments.Length;
            if (totalArgs > 1)
            {
                AddError(
                    $"'@lru_cache' on '{definitionName}' accepts at most one 'maxsize' argument.",
                    decorator.LineStart,
                    decorator.ColumnStart,
                    code: DiagnosticCodes.Validation.LruCacheInvalidMaxSize,
                    span: decorator.Span);
                continue;
            }

            // Keyword arguments must be named 'maxsize'
            if (decorator.KeywordArguments.Length == 1)
            {
                var kw = decorator.KeywordArguments[0];
                if (kw.Name != "maxsize")
                {
                    AddError(
                        $"Unknown @lru_cache option '{kw.Name}' on '{definitionName}'. " +
                        "The only supported option is 'maxsize'.",
                        kw.Value.LineStart,
                        kw.Value.ColumnStart,
                        code: DiagnosticCodes.Validation.LruCacheInvalidMaxSize,
                        span: kw.Value.Span);
                    continue;
                }

                ValidateLruCacheMaxSizeValue(kw.Value, definitionName);
            }
            else
            {
                // Positional argument: must be the maxsize value
                ValidateLruCacheMaxSizeValue(decorator.Arguments[0], definitionName);
            }
        }
    }

    /// <summary>
    /// Validates that the maxsize argument is either a non-negative integer literal or None.
    /// </summary>
    private void ValidateLruCacheMaxSizeValue(Expression value, string definitionName)
    {
        switch (value)
        {
            case NoneLiteral:
                return;
            case IntegerLiteral:
                // Integer literals from the parser are always non-negative; the unary
                // minus case is handled below as a separate AST node.
                return;
            case UnaryOp { Operator: UnaryOperator.Minus, Operand: IntegerLiteral }:
                AddError(
                    $"'@lru_cache' on '{definitionName}' requires a non-negative 'maxsize' value.",
                    value.LineStart,
                    value.ColumnStart,
                    code: DiagnosticCodes.Validation.LruCacheInvalidMaxSize,
                    span: value.Span);
                return;
            default:
                AddError(
                    $"'@lru_cache' on '{definitionName}' requires 'maxsize' to be an integer literal or None.",
                    value.LineStart,
                    value.ColumnStart,
                    code: DiagnosticCodes.Validation.LruCacheInvalidMaxSize,
                    span: value.Span);
                return;
        }
    }

    /// <summary>
    /// Reports an error when @lru_cache or @cache is applied to a non-function definition.
    /// </summary>
    private void ValidateLruCacheNotOnNonFunction(
        IEnumerable<Decorator> decorators, string definitionName, string kind)
    {
        foreach (var decorator in decorators)
        {
            if (decorator.Name == DecoratorNames.LruCache || decorator.Name == DecoratorNames.Cache)
            {
                AddError(
                    $"'@{decorator.Name}' cannot be applied to {kind} '{definitionName}'. " +
                    "Memoization decorators only apply to functions and methods.",
                    decorator.LineStart,
                    decorator.ColumnStart,
                    code: DiagnosticCodes.Validation.LruCacheOnNonFunction,
                    span: decorator.Span);
            }
        }
    }
}
